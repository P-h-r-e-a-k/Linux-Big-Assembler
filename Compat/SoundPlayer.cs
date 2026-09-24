using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;

namespace LBAAssembler;

// The part of System.Media.SoundPlayer the editor uses (a WAV given as a Stream or a file, played once or looped, stopped),
// on every platform: the samples go to SDL3's audio streams, which mix as many players as are open through the default
// output device. Sound is a nicety, never a requirement -- when SDL3 isn't installed, no audio device exists, or the data
// isn't a WAV the player understands, the failure is logged once and the player simply stays silent.
//
// Thread-safe: Play/Stop/Dispose come from the UI thread while a looped sound's feeder timer runs on the pool.
public sealed class SoundPlayer : IDisposable
{
    // ---- the WAV ----

    // Set before Play(): the player reads it (from the start) when asked to play, like System.Media.SoundPlayer does.
    public Stream? Stream { get; set; }

    // The alternative to Stream: a WAV file's path.
    public string? SoundLocation { get; set; }

    private sealed record Wave(int Format, int Channels, int Rate, byte[] Data, int BytesPerSecond);

    // Loads whatever Stream / SoundLocation currently names; null (after a log line) for anything that isn't a WAV the
    // player can send on. "fmt " and "data" are found by walking the chunks, so a LIST or fact chunk in between is fine.
    private Wave? Load()
    {
        byte[] bytes;
        try
        {
            if (Stream is { } stream)
            {
                if (stream.CanSeek) stream.Position = 0;
                if (stream is MemoryStream memory && memory.TryGetBuffer(out var segment) && segment.Offset == 0 && segment.Count == segment.Array!.Length)
                    bytes = segment.Array;
                else
                {
                    using var copy = new MemoryStream();
                    stream.CopyTo(copy);
                    bytes = copy.ToArray();
                }
            }
            else if (!string.IsNullOrEmpty(SoundLocation)) bytes = File.ReadAllBytes(SoundLocation);
            else return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ObjectDisposedException or NotSupportedException)
        {
            DebugLog.Log($"SoundPlayer: couldn't read the sound: {error.Message}");
            return null;
        }
        var wave = Parse(bytes);
        if (wave is null) DebugLog.Log("SoundPlayer: the data isn't a PCM WAV the player can play; ignored.");
        return wave;
    }

    private static Wave? Parse(byte[] wav)
    {
        if (wav.Length < 12 || wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F' || wav[8] != 'W' || wav[9] != 'A' || wav[10] != 'V' || wav[11] != 'E') return null;
        int format = 0, channels = 0, rate = 0, bits = 0, dataAt = -1, dataLength = 0;
        for (var at = 12; at + 8 <= wav.Length;)
        {
            var id = System.Text.Encoding.ASCII.GetString(wav, at, 4);
            var length = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(at + 4));
            if (length < 0) length = int.MaxValue;
            var body = at + 8;
            var available = Math.Min(length, wav.Length - body);
            if (id == "fmt " && available >= 16)
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(body));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(body + 2));
                rate = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(body + 4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(body + 14));
                // WAVE_FORMAT_EXTENSIBLE: the real format tag is the first two bytes of the sub-format GUID
                if (format == 0xFFFE && available >= 26) format = BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(body + 24));
            }
            else if (id == "data") { dataAt = body; dataLength = available; break; }
            if (length > wav.Length) break;                                 // a damaged length: don't wrap around
            at = body + length + (length & 1);
        }
        if (dataAt < 0 || channels is < 1 or > 8 || rate is < 1000 or > 384000) return null;
        var sdlFormat = (format, bits) switch
        {
            (1, 8) => SDL_AUDIO_U8,
            (1, 16) => SDL_AUDIO_S16LE,
            (1, 32) => SDL_AUDIO_S32LE,
            (3, 32) => SDL_AUDIO_F32LE,
            _ => 0,
        };
        if (sdlFormat == 0) return null;
        var frame = channels * bits / 8;
        dataLength -= dataLength % frame;
        if (dataLength <= 0) return null;
        var data = new byte[dataLength];
        Buffer.BlockCopy(wav, dataAt, data, 0, dataLength);
        return new Wave(sdlFormat, channels, rate, data, rate * frame);
    }

    // ---- playing ----

    private readonly object gate = new();
    private IntPtr stream;                  // the SDL audio stream (with its own logical device), 0 until first needed
    private Wave? streamWave;               // the format the stream was opened for
    private Wave? playing;
    private bool looping;
    private int fed;                        // bytes of `playing` already queued (looping: wraps to 0)
    private Timer? feeder;
    private bool disposed;

    // The bytes the feeder keeps queued ahead of the device when looping (about a second); the data is put in pieces of
    // this size so that a minutes-long CD track doesn't sit in SDL's queue twice over and a Stop() comes at once.
    private static int Ahead(Wave wave) => Math.Max(4096, wave.BytesPerSecond);

    // Plays the sound once, from the start, at once and asynchronously; a sound still playing is started over.
    public void Play() => Start(false);

    // Plays the sound from the start and keeps it going, without a gap at the join, until Stop().
    public void PlayLooping() => Start(true);

    public bool IsPlaying
    {
        get
        {
            lock (gate)
            {
                if (disposed || playing is null || stream == IntPtr.Zero) return false;
                if (looping) return true;
                // the output side: the input queue keeps a few bytes of resampler history behind for good once the
                // sound's rate differs from the device's, while the converted output does run down to nothing
                return SdlCall(() => SDL_GetAudioStreamAvailable(stream)) > 0;
            }
        }
    }

    private void Start(bool loop)
    {
        var wave = Load();
        lock (gate)
        {
            if (disposed) return;
            StopLocked();
            if (wave is null || !Sdl.Ready()) return;
            try
            {
                if (!Open(wave)) return;
                playing = wave;
                looping = loop;
                var first = loop ? Math.Min(wave.Data.Length, Ahead(wave) * 2) : wave.Data.Length;
                if (!SDL_PutAudioStreamData(stream, wave.Data, first)) { Fail("queue"); return; }
                fed = first;
                if (!SDL_ResumeAudioStreamDevice(stream)) { Fail("start"); return; }
                if (loop) feeder = new Timer(_ => Feed(), null, 100, 100);
            }
            catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                Sdl.Broken(error);
                playing = null;
            }
        }
    }

    // Opens the audio stream for this format if the current one (if any) is for another.
    private bool Open(Wave wave)
    {
        if (stream != IntPtr.Zero && streamWave is { } current && current.Format == wave.Format && current.Channels == wave.Channels && current.Rate == wave.Rate) return true;
        if (stream != IntPtr.Zero) { SDL_DestroyAudioStream(stream); stream = IntPtr.Zero; streamWave = null; }
        var spec = new SDL_AudioSpec { format = wave.Format, channels = wave.Channels, freq = wave.Rate };
        stream = SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, ref spec, IntPtr.Zero, IntPtr.Zero);
        if (stream == IntPtr.Zero)
        {
            Sdl.LogOnce($"SoundPlayer: no audio output ({wave.Rate} Hz, {wave.Channels} ch): {Sdl.Error()}; sounds are off.");
            return false;
        }
        streamWave = wave;
        return true;
    }

    private void Fail(string what)
    {
        Sdl.LogOnce($"SoundPlayer: couldn't {what} the sound: {Sdl.Error()}");
        playing = null;
        looping = false;
    }

    // The loop's feeder (pool timer): tops the queue up while it is short, wrapping round at the end of the data.
    private void Feed()
    {
        lock (gate)
        {
            if (disposed || !looping || playing is not { } wave || stream == IntPtr.Zero) return;
            try
            {
                var ahead = Ahead(wave);
                for (var guard = 0; guard < 64 && SDL_GetAudioStreamQueued(stream) < ahead; guard++)
                {
                    if (fed >= wave.Data.Length) fed = 0;
                    var piece = Math.Min(ahead, wave.Data.Length - fed);
                    if (!SDL_PutAudioStreamData(stream, ref wave.Data[fed], piece)) { Fail("keep feeding"); return; }
                    fed += piece;
                }
            }
            catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                Sdl.Broken(error);
                looping = false;
            }
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            if (disposed) return;
            StopLocked();
        }
    }

    private void StopLocked()
    {
        feeder?.Dispose();
        feeder = null;
        looping = false;
        playing = null;
        fed = 0;
        if (stream == IntPtr.Zero) return;
        try
        {
            SDL_ClearAudioStream(stream);
            SDL_PauseAudioStreamDevice(stream);
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { Sdl.Broken(error); }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            StopLocked();
            disposed = true;
            if (stream != IntPtr.Zero)
            {
                try { SDL_DestroyAudioStream(stream); }
                catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { Sdl.Broken(error); }
                stream = IntPtr.Zero;
                streamWave = null;
            }
        }
    }

    private static int SdlCall(Func<int> call)
    {
        try { return call(); }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { Sdl.Broken(error); return 0; }
    }

    // ---- SDL3 ----

    // The library's one-time state: loaded and its audio started once, and one log line for the first thing that goes
    // wrong (the library missing, no device...), not one per sound.
    private static class Sdl
    {
        private static readonly object gate = new();
        private static bool? ready;
        private static bool logged;

        public static bool Ready()
        {
            lock (gate)
            {
                if (ready is { } known) return known;
                try
                {
                    NativeLibrary.SetDllImportResolver(typeof(SoundPlayer).Assembly, Resolve);
                }
                catch (InvalidOperationException)
                {
                    // a resolver is already installed for the assembly; the "SDL3" name then goes through it (and the
                    // runtime's own probing), which finds the library when it sits next to the executable
                }
                try
                {
                    if (SDL_Init(SDL_INIT_AUDIO))
                    {
                        ready = true;
                    }
                    else
                    {
                        LogOnce($"SoundPlayer: SDL audio didn't start: {Error()}; sounds are off.");
                        ready = false;
                    }
                }
                catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
                {
                    LogOnce($"SoundPlayer: SDL3 isn't available ({error.GetType().Name}: {error.Message}); sounds are off.");
                    ready = false;
                }
                return ready.Value;
            }
        }

        // The library went missing under a call that had been working: sounds are off from here on.
        public static void Broken(Exception error)
        {
            lock (gate)
            {
                ready = false;
                LogOnce($"SoundPlayer: SDL3 stopped working ({error.GetType().Name}: {error.Message}); sounds are off.");
            }
        }

        public static void LogOnce(string message)
        {
            lock (gate)
            {
                if (logged) return;
                logged = true;
                DebugLog.Log(message);
            }
        }

        public static string Error()
        {
            try { return Marshal.PtrToStringUTF8(SDL_GetError()) ?? ""; }
            catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { return error.Message; }
        }

        // "SDL3" as the import name: the platform's own file name, in the usual places, or a copy next to the executable.
        private static IntPtr Resolve(string name, System.Reflection.Assembly assembly, DllImportSearchPath? path)
        {
            if (name != Library) return IntPtr.Zero;
            var candidates = OperatingSystem.IsWindows() ? new[] { "SDL3.dll", Path.Combine(AppContext.BaseDirectory, "SDL3.dll") }
                : OperatingSystem.IsMacOS() ? new[] { "libSDL3.dylib", "libSDL3.0.dylib", Path.Combine(AppContext.BaseDirectory, "libSDL3.dylib"), "/usr/local/lib/libSDL3.dylib", "/opt/homebrew/lib/libSDL3.dylib" }
                : new[] { "libSDL3.so.0", Path.Combine(AppContext.BaseDirectory, "libSDL3.so.0"), Path.Combine(AppContext.BaseDirectory, "libSDL3.so"), "/usr/local/lib/libSDL3.so.0", "/usr/lib/libSDL3.so.0", "/usr/lib/x86_64-linux-gnu/libSDL3.so.0", "/usr/lib/aarch64-linux-gnu/libSDL3.so.0", "libSDL3.so" };
            foreach (var candidate in candidates)
                if (NativeLibrary.TryLoad(candidate, out var handle)) return handle;
            return IntPtr.Zero;                                             // let the runtime's default probing have a go
        }
    }

    private const string Library = "SDL3";
    private const uint SDL_INIT_AUDIO = 0x10;
    private const uint SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK = 0xFFFFFFFF;
    private const int SDL_AUDIO_U8 = 0x0008;
    private const int SDL_AUDIO_S16LE = 0x8010;
    private const int SDL_AUDIO_S32LE = 0x8020;
    private const int SDL_AUDIO_F32LE = 0x8120;

    [StructLayout(LayoutKind.Sequential)]
    private struct SDL_AudioSpec
    {
        public int format;
        public int channels;
        public int freq;
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_Init(uint flags);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_GetError();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_OpenAudioDeviceStream(uint device, ref SDL_AudioSpec spec, IntPtr callback, IntPtr userdata);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_PutAudioStreamData(IntPtr stream, byte[] data, int length);

    // the same entry point, for a slice of an array (data pinned at the given element for the call)
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_PutAudioStreamData(IntPtr stream, ref byte data, int length);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_ResumeAudioStreamDevice(IntPtr stream);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_PauseAudioStreamDevice(IntPtr stream);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_GetAudioStreamQueued(IntPtr stream);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_GetAudioStreamAvailable(IntPtr stream);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL_ClearAudioStream(IntPtr stream);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_DestroyAudioStream(IntPtr stream);
}
