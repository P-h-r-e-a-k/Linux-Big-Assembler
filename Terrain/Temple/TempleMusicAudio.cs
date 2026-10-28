using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace LBAAssembler.Terrain.Temple;

// TempleMusic's audio files read as the CD's samples (16-bit stereo 44.1 kHz, little-endian: what the race's WAV is): a WAV of any PCM or
// float format, rate and channel count (converted), an MP3 (Steam's and GOG's Classic: Common\Music\Track_NN.mp3 -- decoded with NLayer,
// MIT, a managed MPEG audio decoder: the engine streams WAV and OGG only).
internal static partial class TempleMusic
{
    // What a file is, by its first bytes: "wav", "ogg", "mp3", "flac", or "" (an MP3 by an ID3 tag or an MPEG audio frame's sync)
    private static string Sniff(string path)
    {
        try
        {
            using var f = File.OpenRead(path);
            var b = new byte[12];
            var n = f.Read(b, 0, 12);
            if (n >= 12 && Encoding.ASCII.GetString(b, 0, 4) == "RIFF" && Encoding.ASCII.GetString(b, 8, 4) == "WAVE") return "wav";
            if (n >= 4 && Encoding.ASCII.GetString(b, 0, 4) == "OggS") return "ogg";
            if (n >= 4 && Encoding.ASCII.GetString(b, 0, 4) == "fLaC") return "flac";
            if (n >= 3 && Encoding.ASCII.GetString(b, 0, 3) == "ID3") return "mp3";
            if (n >= 2 && b[0] == 0xFF && (b[1] & 0xE0) == 0xE0 && (b[1] & 0x06) != 0) return "mp3";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        return "";
    }

    // A WAV or MP3 file's samples as the CD's, or null (another kind of file)
    public static byte[]? ReadAudio(string path) => Sniff(path) switch { "wav" => ReadWav(path), "mp3" => DecodeMp3(path), _ => null };

    // A WAV file's samples as the CD's: the whole file, or CD sectors [fromSector, toSector) of it (toSector < 0: to its end; a sector is
    // 1/75 s). 8/16/24/32-bit PCM and 32/64-bit float, any rate and channels (mono doubled, more than two: the first two). Null: not a WAV
    // this reads.
    private static byte[]? ReadWav(string path, long fromSector = 0, long toSector = -1)
    {
        using var file = File.OpenRead(path);
        return ReadWav(file, fromSector, toSector);
    }

    // ... or a WAV in a stream (a seekable one: the race items' sounds, the program's own -- RaceSounds)
    public static byte[]? ReadWav(Stream f, long fromSector = 0, long toSector = -1)
    {
        using var r = new BinaryReader(f, Encoding.ASCII, true);
        if (f.Length < 12 || Encoding.ASCII.GetString(r.ReadBytes(4)) != "RIFF") return null;
        r.ReadInt32();
        if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "WAVE") return null;
        int format = 0, channels = 0, rate = 0, bits = 0, block = 0;
        long dataAt = -1, dataSize = 0;
        while (f.Position + 8 <= f.Length)
        {
            var id = Encoding.ASCII.GetString(r.ReadBytes(4));
            var size = r.ReadUInt32();
            var next = f.Position + size + (size & 1);
            if (id == "fmt " && size >= 16)
            {
                format = r.ReadUInt16(); channels = r.ReadUInt16(); rate = r.ReadInt32(); r.ReadInt32(); block = r.ReadUInt16(); bits = r.ReadUInt16();
                // (WAVE_FORMAT_EXTENSIBLE: the format is its sub-format GUID's first two bytes)
                if (format == 0xFFFE && size >= 40) { r.ReadUInt16(); r.ReadUInt16(); r.ReadUInt32(); format = r.ReadUInt16(); }
            }
            else if (id == "data")
            {
                dataAt = f.Position;
                dataSize = Math.Min(size, f.Length - f.Position);
                break;
            }
            f.Position = next;
        }
        if (dataAt < 0 || channels < 1 || rate < 8000 || block < channels || !(format == 1 && bits is 8 or 16 or 24 or 32 || format == 3 && bits is 32 or 64)) return null;
        var frames = dataSize / block;
        var first = Math.Clamp(fromSector * FramesPerSector * rate / Rate, 0, frames);
        var last = toSector < 0 ? frames : Math.Clamp(toSector * FramesPerSector * rate / Rate, first, frames);
        f.Position = dataAt + first * block;
        var raw = new byte[(last - first) * block];
        f.ReadExactly(raw);
        if (format == 1 && bits == 16 && channels == Channels && rate == Rate) return raw;
        var count = (int)(last - first);
        var bytes = bits / 8;
        var samples = new float[count * channels];
        for (var i = 0; i < count; i++)
            for (var c = 0; c < channels; c++)
            {
                var at = i * block + c * bytes;
                samples[i * channels + c] = (format, bits) switch
                {
                    (1, 8) => (raw[at] - 128) / 128f,
                    (1, 16) => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(at)) / 32768f,
                    (1, 24) => (raw[at] | raw[at + 1] << 8 | (sbyte)raw[at + 2] << 16) / 8388608f,
                    (1, 32) => BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(at)) / 2147483648f,
                    (3, 32) => BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(at)),
                    _ => (float)BinaryPrimitives.ReadDoubleLittleEndian(raw.AsSpan(at)),
                };
            }
        return ToCd(samples, channels, rate);
    }

    // An MP3 file decoded to the CD's samples (NLayer; resampled when it isn't 44.1 kHz)
    private static byte[] DecodeMp3(string path)
    {
        using var file = File.OpenRead(path);
        return DecodeMp3(file, Path.GetFileName(path));
    }

    // ... or an MP3 in a stream (the race items' sounds, the program's own: RaceSounds), `name` for the errors
    public static byte[] DecodeMp3(Stream stream, string name)
    {
        using var mp3 = new NLayer.MpegFile(stream);
        int channels = mp3.Channels, rate = mp3.SampleRate;
        if (channels < 1 || rate < 8000) throw new InvalidDataException($"{name}: {channels} channels at {rate} Hz");
        var samples = new float[Math.Max(1 << 16, (int)Math.Min(int.MaxValue / 2, (long)(mp3.Duration.TotalSeconds + 2) * rate * channels))];
        var count = 0;
        var chunk = new float[8192 * channels];
        int n;
        while ((n = mp3.ReadSamples(chunk, 0, chunk.Length)) > 0)
        {
            if (count + n > samples.Length) Array.Resize(ref samples, Math.Max(samples.Length * 2, count + n));
            Array.Copy(chunk, 0, samples, count, n);
            count += n;
        }
        if (count < rate * channels / 10) throw new InvalidDataException($"{name}: no MPEG audio in it");
        Array.Resize(ref samples, count - count % channels);
        return ToCd(samples, channels, rate);
    }

    // Interleaved samples (-1..1) of any channel count and rate as the CD's: two channels (a mono one doubled, the first two of more),
    // resampled to 44.1 kHz (a windowed sinc, its cut-off below the lower of the two rates' Nyquist), 16-bit with rounding and clipping
    private static byte[] ToCd(float[] samples, int channels, int rate)
    {
        var frames = samples.Length / channels;
        float At(int frame, int c) => samples[frame * channels + (channels == 1 ? 0 : c)];
        int outFrames = rate == Rate ? frames : (int)((long)frames * Rate / rate);
        var pcm = new byte[outFrames * 4];
        void Put(int frame, int c, double v)
        {
            var s = (int)Math.Round(v * 32767);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(frame * 4 + c * 2), (short)Math.Clamp(s, -32768, 32767));
        }
        if (rate == Rate)
        {
            for (var i = 0; i < frames; i++) for (var c = 0; c < 2; c++) Put(i, c, At(i, c));
            return pcm;
        }
        // (the kernel tabulated at Phases fractional positions: taps k = centre - half + 1 .. centre + half, d = x - k)
        const int Taps = 16, Phases = 1024;
        var ratio = (double)rate / Rate;
        var cutoff = 0.95 * Math.Min(1.0, 1.0 / ratio);
        var half = (int)Math.Ceiling(Taps / cutoff);
        var table = new float[(Phases + 1) * 2 * half];
        for (var p = 0; p <= Phases; p++)
        {
            double sum = 0;
            var row = p * 2 * half;
            for (var j = 0; j < 2 * half; j++)
            {
                var d = (double)p / Phases + half - 1 - j;
                var t = d * cutoff;
                var sinc = Math.Abs(t) < 1e-9 ? 1.0 : Math.Sin(Math.PI * t) / (Math.PI * t);
                var w = Math.Abs(d) >= half ? 0 : 0.42 + 0.5 * Math.Cos(Math.PI * d / half) + 0.08 * Math.Cos(2 * Math.PI * d / half);
                table[row + j] = (float)(sinc * w);
                sum += sinc * w;
            }
            // (normalised: no gain ripple from the window at either rate)
            for (var j = 0; j < 2 * half; j++) table[row + j] = (float)(table[row + j] / sum);
        }
        for (var i = 0; i < outFrames; i++)
        {
            var x = i * ratio;
            var centre = (int)Math.Floor(x);
            var row = (int)Math.Round((x - centre) * Phases) * 2 * half;
            double l = 0, r = 0;
            for (var j = 0; j < 2 * half; j++)
            {
                var k = centre - half + 1 + j;
                if (k < 0 || k >= frames) continue;
                var h = table[row + j];
                l += h * At(k, 0); r += h * At(k, 1);
            }
            Put(i, 0, l); Put(i, 1, r);
        }
        return pcm;
    }
}
