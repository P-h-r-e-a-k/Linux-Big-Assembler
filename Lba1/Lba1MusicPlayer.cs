using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace LBAAssembler.Lba1;

// Plays a Standard MIDI File, looping until told to stop, as the game's music does. One piece at a time; a piece asked for
// again while it plays is left alone.
//
// On Windows the file goes through Windows' own MIDI sequencer (MCI). Elsewhere it is handed to an external synthesiser
// as a child process -- fluidsynth (with a General MIDI soundfont found on the machine) or, failing that, timidity -- and
// Tick() starts the process over when the piece has ended. Without either program the music is silent (logged once).
//
// The CD's own recordings of the tunes (WAVs) go through SoundPlayer, looped.
internal sealed class Lba1MusicPlayer : IDisposable
{
    private readonly string file = Path.Combine(Path.GetTempPath(), $"lba1_music_{Environment.ProcessId}.mid");
    private int current = -1;

    public int Current => current;

    private byte[]? lastData;
    private bool lastIsCd;
    private double volume = 1;

    // 0..1. The piece playing is started again at the new level (the volume is part of the data it plays).
    public double Volume
    {
        get => volume;
        set
        {
            value = Math.Clamp(value, 0, 1);
            if (Math.Abs(volume - value) < 0.005) return;
            volume = value;
            if (lastData is null || current < 0 || !(open || cdPlaying)) return;
            var number = current; var data = lastData; var cd = lastIsCd;
            Stop();
            if (cd) PlayCd(number, data); else Play(number, data);
        }
    }

    // ---- CD tracks ----

    private readonly SoundPlayer cdPlayer = new();
    private bool cdPlaying;

    public void PlayCd(int number, byte[] wav)
    {
        if (cdPlaying && number == current) return;
        Stop();
        try
        {
            lastData = wav; lastIsCd = true;
            cdPlayer.Stream = new MemoryStream(PcmVolume.Scale(wav, volume));
            cdPlayer.PlayLooping();
            cdPlaying = true;
            current = number;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            DebugLog.Log($"Lba1MusicPlayer: CD track for music {number} didn't start: {error.Message}");
        }
    }

    // ---- MIDI ----

    // true while a piece is on: MCI has the file open (Windows) or a synthesiser process is meant to be running (elsewhere)
    private bool open;

    public void Play(int number, byte[] midi)
    {
        if (open && number == current) return;
        Stop();
        try
        {
            lastData = midi; lastIsCd = false;
            File.WriteAllBytes(file, MidiVolume.Scale(midi, volume));
            if (OperatingSystem.IsWindows())
            {
                if (!Mci.Open(file)) return;
            }
            else
            {
                if (!synth.Start(file)) return;
            }
            open = true;
            current = number;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            DebugLog.Log($"Lba1MusicPlayer: music {number} didn't start: {error.Message}");
        }
    }

    public void Stop()
    {
        if (open)
        {
            if (OperatingSystem.IsWindows()) Mci.Close(); else synth.Stop();
        }
        open = false;
        if (cdPlaying) { try { cdPlayer.Stop(); } catch (InvalidOperationException) { } }
        cdPlaying = false;
        current = -1;
    }

    // Called now and then: starts the piece over when it has finished.
    public void Tick()
    {
        if (!open) return;
        if (OperatingSystem.IsWindows()) Mci.RestartIfStopped(); else synth.RestartIfEnded();
    }

    public void Dispose()
    {
        Stop();
        cdPlayer.Dispose();
        try { File.Delete(file); } catch (IOException) { }
    }

    // ---- Windows: MCI ----

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, StringBuilder? result, int length, IntPtr callback);

    private static class Mci
    {
        private const string Alias = "lba1music";

        public static bool Open(string file)
        {
            if (mciSendString($"open \"{file}\" type sequencer alias {Alias}", null, 0, IntPtr.Zero) != 0) return false;
            mciSendString($"play {Alias} from 0", null, 0, IntPtr.Zero);
            return true;
        }

        public static void Close()
        {
            mciSendString($"stop {Alias}", null, 0, IntPtr.Zero);
            mciSendString($"close {Alias}", null, 0, IntPtr.Zero);
        }

        public static void RestartIfStopped()
        {
            var mode = new StringBuilder(64);
            if (mciSendString($"status {Alias} mode", mode, mode.Capacity, IntPtr.Zero) == 0 && mode.ToString() == "stopped")
                mciSendString($"play {Alias} from 0", null, 0, IntPtr.Zero);
        }
    }

    // ---- Linux / macOS: an external synthesiser ----

    private readonly Synth synth = new();

    // Runs the synthesiser as a child process, one at a time. fluidsynth is tried first (it needs a soundfont), timidity
    // second; the audio driver for fluidsynth is chosen by trying them: a driver it can't open makes it quit at once with a
    // failure code, on which a watcher (pool thread) starts the piece again with the next one. The driver that works is
    // remembered for the rest of the session. LBA1_MIDI_AUDIO_DRIVER, when set, names the driver instead ("dummy" and
    // "file" render to nowhere, for tests on machines without sound).
    private sealed class Synth
    {
        private readonly object gate = new();
        private Process? process;
        private string? file;
        private int generation;                             // bumps on every launch and stop; a watcher for an older one stays quiet
        private Stopwatch? started;
        private bool onTrial;                               // the running fluidsynth's driver is not yet known to work: its watcher decides
        private int fastFailures;                           // in a row, since the last Start(); a few and the piece is given up

        private static readonly object choice = new();      // guards the static knowledge below
        private static string? driverInUse;                 // fluidsynth: the driver found to work
        private static int nextDriver;                      // ... or the next candidate to try
        private static bool loggedMissing;
        private static readonly string[] Drivers = { "pulseaudio", "pipewire", "alsa", "" };   // "" = fluidsynth's own default

        // Starts the file. false only when there is no synthesiser at all (logged once); a driver that fails is handled later.
        public bool Start(string midiFile)
        {
            lock (gate)
            {
                StopLocked();
                file = midiFile;
                fastFailures = 0;
                return Launch();
            }
        }

        public void Stop()
        {
            lock (gate)
            {
                StopLocked();
                file = null;
            }
        }

        // Tick(): the piece has ended (the process exited) -> play it again.
        public void RestartIfEnded()
        {
            lock (gate)
            {
                if (file is null) return;
                if (process is null) { Launch(); return; }
                if (onTrial) return;                        // the watcher reads the verdict and relaunches
                try { if (!process.HasExited) return; }
                catch (InvalidOperationException) { }
                var code = ExitCode(process);
                var elapsed = RunMilliseconds(process);
                Forget();
                if (code == 0 || elapsed > 2000) { fastFailures = 0; Launch(); return; }
                if (++fastFailures < 3) { Launch(); return; }
                DebugLog.Log($"Lba1MusicPlayer: the synthesiser keeps quitting at once (exit code {code}); giving this piece up.");
                file = null;
            }
        }

        private void StopLocked()
        {
            generation++;
            onTrial = false;
            if (process is null) return;
            try { if (!process.HasExited) process.Kill(true); }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { DebugLog.Log($"Lba1MusicPlayer: the synthesiser didn't stop: {error.Message}"); }
            Forget();
        }

        private void Forget()
        {
            try { process?.Dispose(); } catch (InvalidOperationException) { }
            process = null;
            started = null;
            onTrial = false;
        }

        private static int ExitCode(Process p)
        {
            try { return p.ExitCode; } catch (InvalidOperationException) { return -1; }
        }

        // How long the synthesiser actually ran: its own start to its own exit. Not "since it was started, as of now" -- the
        // play view only asks every few seconds, so a fluidsynth that quit after 0.2 s for want of an audio device looked like
        // a piece that had played out, and was started again every tick for ever instead of being given up (and logged).
        private long RunMilliseconds(Process p)
        {
            try { return (long)(p.ExitTime - p.StartTime).TotalMilliseconds; }
            catch (Exception error) when (error is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
            {
                return started?.ElapsedMilliseconds ?? long.MaxValue;
            }
        }

        // Under the lock: starts the synthesiser for `file` with the current driver choice.
        private bool Launch()
        {
            if (file is null) return false;
            var info = Command(file, out var trial);
            if (info is null) return false;
            var p = new Process { StartInfo = info, EnableRaisingEvents = true };
            var mine = ++generation;
            if (trial) p.Exited += (_, _) => DriverVerdict(p, mine);      // attached before Start: an instant exit is seen too
            try
            {
                if (!p.Start())
                {
                    DebugLog.Log("Lba1MusicPlayer: the synthesiser didn't start.");
                    p.Dispose();
                    return false;
                }
            }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
            {
                DebugLog.Log($"Lba1MusicPlayer: {info.FileName} didn't start: {error.Message}");
                p.Dispose();
                return false;
            }
            // drain both pipes so a chatty synthesiser can never block on a full one
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            process = p;
            started = Stopwatch.StartNew();
            onTrial = trial;
            return true;
        }

        // The watcher for a fluidsynth started with a driver still on trial: a quick failure means "not this driver",
        // and the piece starts again with the next; anything else means the driver works.
        private void DriverVerdict(Process p, int mine)
        {
            lock (gate)
            {
                if (mine != generation || !ReferenceEquals(p, process)) return;
                var elapsed = RunMilliseconds(p);
                var code = ExitCode(p);
                onTrial = false;
                lock (choice)
                {
                    if (code == 0 || elapsed > 2000)
                    {
                        driverInUse = Drivers[Math.Min(nextDriver, Drivers.Length - 1)];
                        return;                                     // ended by itself: RestartIfEnded() starts it over
                    }
                    Forget();
                    if (nextDriver + 1 >= Drivers.Length)
                    {
                        driverInUse = Drivers[^1];                  // nothing else to try: stay with the default, quietly
                        if (!loggedMissing) { loggedMissing = true; DebugLog.Log($"Lba1MusicPlayer: fluidsynth couldn't open any audio driver ({string.Join(", ", Drivers.Where(d => d.Length > 0))}); music is off."); }
                        file = null;
                        return;
                    }
                    nextDriver++;
                }
                Launch();
            }
        }

        // ---- the programs ----

        private static string? fluid, timidity, soundfont;
        private static bool looked;

        // The command for the file; `trial` says the fluidsynth driver it names is not yet known to work.
        private static ProcessStartInfo? Command(string file, out bool trial)
        {
            trial = false;
            ProcessStartInfo info;
            lock (choice)
            {
                if (!looked)
                {
                    looked = true;
                    fluid = FindOnPath("fluidsynth");
                    timidity = FindOnPath("timidity");
                    soundfont = FindSoundfont();
                    if (fluid is not null && soundfont is null) DebugLog.Log("Lba1MusicPlayer: fluidsynth is installed but no General MIDI soundfont (.sf2) was found; set SOUNDFONT to one.");
                }
                var forced = Environment.GetEnvironmentVariable("LBA1_MIDI_AUDIO_DRIVER");
                if (fluid is not null && soundfont is not null)
                {
                    info = new ProcessStartInfo(fluid);
                    string driver;
                    if (!string.IsNullOrEmpty(forced)) { driver = forced; driverInUse = forced; }
                    else if (driverInUse is not null) driver = driverInUse;
                    else { driver = Drivers[nextDriver]; trial = true; }
                    if (driver == "dummy") driver = "file";          // fluidsynth has no silent driver; "file" to nowhere is one
                    if (driver.Length > 0) { info.ArgumentList.Add("-a"); info.ArgumentList.Add(driver); }
                    if (driver == "file") { info.ArgumentList.Add("-o"); info.ArgumentList.Add("audio.file.name=" + (OperatingSystem.IsWindows() ? "NUL" : "/dev/null")); }
                    info.ArgumentList.Add("-i");                     // no shell
                    info.ArgumentList.Add("-n");                     // no MIDI input
                    info.ArgumentList.Add("-q");
                    info.ArgumentList.Add(soundfont);
                    info.ArgumentList.Add(file);
                }
                else if (timidity is not null)
                {
                    info = new ProcessStartInfo(timidity);
                    info.ArgumentList.Add("-idqq");                  // dumb interface, quiet
                    if (forced is "dummy" or "file") { info.ArgumentList.Add("-Ow"); info.ArgumentList.Add("-o"); info.ArgumentList.Add(OperatingSystem.IsWindows() ? "NUL" : "/dev/null"); }
                    info.ArgumentList.Add(file);
                }
                else
                {
                    if (!loggedMissing) { loggedMissing = true; DebugLog.Log("Lba1MusicPlayer: neither fluidsynth (with a soundfont) nor timidity is installed; music is off."); }
                    return null;
                }
            }
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.RedirectStandardInput = false;
            return info;
        }

        private static string? FindOnPath(string name)
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var candidate = Path.Combine(dir, name);
                    if (File.Exists(candidate)) return candidate;
                    if (OperatingSystem.IsWindows() && File.Exists(candidate + ".exe")) return candidate + ".exe";
                }
                catch (ArgumentException) { }
            }
            return null;
        }

        private static string? FindSoundfont()
        {
            if (Environment.GetEnvironmentVariable("SOUNDFONT") is { Length: > 0 } configured && File.Exists(configured)) return configured;
            foreach (var known in new[] { "/usr/share/sounds/sf2/FluidR3_GM.sf2", "/usr/share/sounds/sf2/default-GM.sf2", "/usr/share/sounds/sf2/TimGM6mb.sf2", "/usr/local/share/sounds/sf2/FluidR3_GM.sf2" })
                if (File.Exists(known)) return known;
            foreach (var dir in new[] { "/usr/share/soundfonts", "/usr/local/share/soundfonts", "/usr/share/sounds/sf2", Path.Combine(AppContext.BaseDirectory, "soundfonts"), AppContext.BaseDirectory })
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    var found = Directory.EnumerateFiles(dir, "*.sf2").Concat(Directory.EnumerateFiles(dir, "*.SF2")).OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
                    if (found is not null) return found;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
            return null;
        }
    }
}
