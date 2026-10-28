using System.Buffers.Binary;
using System.Text;
using LBAAssembler;
using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// PROBE (2026-10-19): the race items' sounds (Terrain.RaceSounds), checked.
//   racesounds install <dir>: the sounds the program carries written into <dir>\RACESOUNDS as the race track's build writes them, each one's
//     length and level
//   racesounds levels <SAMPLES.HQR> [entry...]: the game's own sound effects' levels (peak and RMS of full scale; PCM and IMA ADPCM WAVs, VOC)
//     -- what RaceSounds.Peak is set against (by default the thunder 100, the bonus 2 and 3, the found object 6, the magic ball's 8)
//   racesounds wav <wav...>: a WAV's level, as levels says it
//   racesounds capture <raw> [window ms]: a recording of the engine's output (SDL's disk audio driver: 16-bit stereo 44.1 kHz) -- its loud
//     stretches, each one's start, end and peak
//   racesounds find <raw> <wav...>: where each sound (a 16-bit mono WAV, RACESOUNDS') is in a recording of the engine's output -- the
//     recording's left channel matched against it (normalised cross-correlation, FFT, at 11025 Hz; RACESOUNDS_RATE=22050 finer): each place it matches well (0.6 or
//     more of a perfect match, the sound alone with nothing over it; RACESOUNDS_MATCH=<x> another) and the gain it plays at there; with
//     RACESOUNDS_FIRST_MS=<ms> only the sound's first <ms> looked for (a looped one cut short)
//   racesounds sample <SAMPLES.HQR> <entry> <out>: a game sample written out as its file (a WAV), for find
internal static class RaceSoundsProbe
{
    public static int Run(string[] args) => args[1] switch
    {
        "install" => Install(args[2]),
        "levels" => Levels(args[2], args.Skip(3).Select(int.Parse).DefaultIfEmpty(-1).ToArray()),
        "wav" => Wavs(args.Skip(2)),
        "capture" => Capture(args[2], args.Length > 3 ? int.Parse(args[3]) : 50),
        "find" => Find(args[2], args.Skip(3)),
        "sample" => SampleOut(args[2], int.Parse(args[3]), args[4]),
        _ => throw new ArgumentException("racesounds install|levels|wav|capture|find"),
    };

    private static int Install(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var (kind, name, res) in RaceSounds.Carried()) Console.WriteLine($"carried: {kind} {name} ({res})");
        foreach (var line in RaceSounds.Install(dir)) Console.WriteLine(line);
        return Wavs(Directory.GetFiles(Path.Combine(dir, RaceSounds.Folder), "*.WAV").Order());
    }

    private static int Wavs(IEnumerable<string> files)
    {
        foreach (var f in files)
        {
            var (s, rate) = Decode(File.ReadAllBytes(f));
            Console.WriteLine($"{Path.GetFileName(f),-28} {Say(s, rate)}");
        }
        return 0;
    }

    private static int Levels(string hqr, int[] entries)
    {
        var archive = HqrArchive.Open(hqr);
        if (entries is [-1]) entries = new[] { 100, 2, 3, 6, 8, 99, 101 };
        foreach (var n in entries)
        {
            byte[] data;
            try { data = archive.Read(n); } catch (Exception e) { Console.WriteLine($"{n}: {e.Message}"); continue; }
            var (s, rate) = Decode(data);
            Console.WriteLine($"SAMPLES {n,4}: {Encoding.ASCII.GetString(data, 0, Math.Min(4, data.Length)).Replace('\0', '.')} {Say(s, rate)}");
        }
        return 0;
    }

    private static string Say(float[] s, int rate)
    {
        if (s.Length == 0) return "(nothing decoded)";
        double peak = 0, sum = 0;
        foreach (var x in s) { peak = Math.Max(peak, Math.Abs(x)); sum += x * x; }
        var rms = Math.Sqrt(sum / s.Length);
        // (the loudest 100 ms's RMS: a short sound's loudness as heard)
        var win = Math.Max(1, rate / 10);
        double best = 0, acc = 0;
        for (var i = 0; i < s.Length; i++)
        {
            acc += s[i] * s[i];
            if (i >= win) acc -= s[i - win] * s[i - win];
            if (i >= win - 1) best = Math.Max(best, acc / win);
        }
        if (s.Length < win) best = sum / s.Length;
        return $"{s.Length / (double)rate:0.00} s at {rate} Hz, peak {peak:0.000} ({Db(peak):0.0} dB), RMS {Db(rms):0.0} dB, loudest 100 ms {Db(Math.Sqrt(best)):0.0} dB";
    }

    private static double Db(double v) => v <= 0 ? -120 : 20 * Math.Log10(v);

    // A WAV (PCM 8/16, IMA ADPCM) or VOC's samples (the first channel), -1..1, and its rate
    private static (float[], int) Decode(byte[] b)
    {
        if (b.Length > 26 && Encoding.ASCII.GetString(b, 0, 19) == "Creative Voice File")
        {
            int at = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(20));
            var list = new List<float>();
            var rate = 11025;
            while (at + 4 <= b.Length && b[at] != 0)
            {
                var type = b[at];
                var size = b[at + 1] | b[at + 2] << 8 | b[at + 3] << 16;
                if (type == 1 && at + 6 <= b.Length)
                {
                    rate = 1000000 / (256 - b[at + 4]);
                    for (var i = at + 6; i < Math.Min(b.Length, at + 4 + size); i++) list.Add((b[i] - 128) / 128f);
                }
                at += 4 + size;
            }
            return (list.ToArray(), rate);
        }
        if (b.Length < 12 || Encoding.ASCII.GetString(b, 8, 4) != "WAVE") return (Array.Empty<float>(), 1);
        int pos = 12, tag = 1, ch = 1, hz = 22050, align = 0, bits = 16;
        while (pos + 8 <= b.Length)
        {
            var id = Encoding.ASCII.GetString(b, pos, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(pos + 4));
            if (id == "fmt ")
            {
                tag = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(pos + 8)); ch = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(pos + 10));
                hz = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(pos + 12)); align = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(pos + 20));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(pos + 22));
            }
            if (id == "data")
            {
                var data = b.AsSpan(pos + 8, Math.Min(size, b.Length - pos - 8));
                if (tag == 17) return (Ima(data, align), hz);
                var list = new List<float>();
                var step = bits / 8 * ch;
                for (var i = 0; i + step <= data.Length; i += step)
                    list.Add(bits == 8 ? (data[i] - 128) / 128f : BinaryPrimitives.ReadInt16LittleEndian(data[i..]) / 32768f);
                return (list.ToArray(), hz);
            }
            pos += 8 + ((size + 1) & ~1);
        }
        return (Array.Empty<float>(), 1);
    }

    private static readonly int[] Steps =
    {
        7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143, 157, 173,
        190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066,
        2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289,
        16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767,
    };
    private static readonly int[] Index = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };

    // (mono IMA ADPCM, as LIB386/AIL/SDL/SAMPLE.CPP DecodeImaAdpcm)
    private static float[] Ima(ReadOnlySpan<byte> d, int align)
    {
        var list = new List<float>();
        for (var blk = 0; blk + 8 <= d.Length; blk += align)
        {
            var size = Math.Min(align, d.Length - blk);
            int pred = (short)(d[blk] | d[blk + 1] << 8), idx = Math.Clamp((int)d[blk + 2], 0, 88);
            list.Add(pred / 32768f);
            for (var i = 4; i < size; i++)
                for (var k = 0; k < 2; k++)
                {
                    var nib = k == 0 ? d[blk + i] & 15 : d[blk + i] >> 4;
                    var st = Steps[idx];
                    var diff = st >> 3;
                    if ((nib & 1) != 0) diff += st >> 2;
                    if ((nib & 2) != 0) diff += st >> 1;
                    if ((nib & 4) != 0) diff += st;
                    if ((nib & 8) != 0) diff = -diff;
                    pred = Math.Clamp(pred + diff, -32768, 32767);
                    list.Add(pred / 32768f);
                    idx = Math.Clamp(idx + Index[nib], 0, 88);
                }
        }
        return list.ToArray();
    }

    // (RACESOUNDS_RATE=22050: a finer match, for a noisy sound -- a sound at 22050 Hz or more only)
    private static readonly int FindRate = Environment.GetEnvironmentVariable("RACESOUNDS_RATE") == "22050" ? 22050 : 11025;
    private static readonly double MinMatch = double.TryParse(Environment.GetEnvironmentVariable("RACESOUNDS_MATCH"), System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : 0.6;

    private static int Find(string raw, IEnumerable<string> wavs)
    {
        var b = File.ReadAllBytes(raw);
        var frames = b.Length / 4;
        var d = 44100 / FindRate;
        var cap = new double[frames / d];
        for (var i = 0; i < cap.Length; i++)
        {
            double s = 0;
            for (var k = 0; k < d; k++) s += BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan((i * d + k) * 4)) / 32768.0;
            cap[i] = s / d;
        }
        Console.WriteLine($"{raw}: {frames / 44100.0:0.00} s");
        var total = 0;
        foreach (var wav in wavs)
        {
            var (s, rate) = Decode(File.ReadAllBytes(wav));
            var step = Math.Max(1, rate / FindRate);
            if (int.TryParse(Environment.GetEnvironmentVariable("RACESOUNDS_FIRST_MS"), out var firstMs) && firstMs > 0)
                s = s.Take(Math.Min(s.Length, rate * firstMs / 1000)).ToArray();
            var t = new double[s.Length / step];
            for (var i = 0; i < t.Length; i++) { double v = 0; for (var k = 0; k < step; k++) v += s[i * step + k]; t[i] = v / step; }
            var hits = Match(cap, t);
            total += hits.Count;
            Console.WriteLine($"{Path.GetFileName(wav),-28} {hits.Count} match(es){(hits.Count > 0 ? ": " : "")}{string.Join(", ", hits.Select(h => $"{h.At / (double)FindRate:0.00} s (match {h.Score:0.00}, gain {h.Gain:0.00})"))}");
        }
        return total > 0 ? 0 : 1;
    }

    // Where template t is in x: the places its normalised cross-correlation peaks at 0.6 or more (one per template length), with the gain
    // that best fits it there
    private static List<(int At, double Score, double Gain)> Match(double[] x, double[] t)
    {
        var n = 1;
        while (n < x.Length + t.Length) n <<= 1;
        var fx = new System.Numerics.Complex[n];
        var ft = new System.Numerics.Complex[n];
        for (var i = 0; i < x.Length; i++) fx[i] = x[i];
        for (var i = 0; i < t.Length; i++) ft[i] = t[i];
        Fft(fx, false); Fft(ft, false);
        for (var i = 0; i < n; i++) fx[i] *= System.Numerics.Complex.Conjugate(ft[i]);
        Fft(fx, true);
        double tt = 0;
        foreach (var v in t) tt += v * v;
        // (x's energy under the template at each lag: a running sum)
        var e = new double[x.Length + 1];
        for (var i = 0; i < x.Length; i++) e[i + 1] = e[i] + x[i] * x[i];
        var hits = new List<(int, double, double)>();
        var last = -t.Length;
        var best = (At: -1, Score: 0.0, Gain: 0.0);
        for (var lag = 0; lag + t.Length <= x.Length; lag++)
        {
            var c = fx[lag].Real / n;
            var ex = e[lag + t.Length] - e[lag];
            if (ex <= 1e-9 || tt <= 0) continue;
            var score = c / Math.Sqrt(ex * tt);
            if (score >= MinMatch && score > best.Score) best = (lag, score, c / tt);
            if (best.At >= 0 && lag - best.At > t.Length / 2)
            {
                if (best.At - last >= t.Length / 2) { hits.Add(best); last = best.At; }
                best = (-1, 0, 0);
            }
        }
        if (best.At >= 0) hits.Add(best);
        return hits;
    }

    private static void Fft(System.Numerics.Complex[] a, bool inverse)
    {
        var n = a.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (a[i], a[j]) = (a[j], a[i]);
        }
        for (var len = 2; len <= n; len <<= 1)
        {
            var ang = 2 * Math.PI / len * (inverse ? 1 : -1);
            var wl = new System.Numerics.Complex(Math.Cos(ang), Math.Sin(ang));
            for (var i = 0; i < n; i += len)
            {
                var w = System.Numerics.Complex.One;
                for (var k = 0; k < len / 2; k++)
                {
                    var u = a[i + k];
                    var v = a[i + k + len / 2] * w;
                    a[i + k] = u + v;
                    a[i + k + len / 2] = u - v;
                    w *= wl;
                }
            }
        }
    }

    private static int SampleOut(string hqr, int entry, string output)
    {
        File.WriteAllBytes(output, HqrArchive.Open(hqr).Read(entry));
        return 0;
    }

    private static int Capture(string raw, int windowMs)
    {
        var b = File.ReadAllBytes(raw);
        var frames = b.Length / 4;
        var win = 44100 * windowMs / 1000;
        Console.WriteLine($"{raw}: {frames / 44100.0:0.00} s");
        var on = false;
        int start = 0;
        double peak = 0;
        for (var w = 0; w * win < frames; w++)
        {
            double p = 0;
            for (var i = w * win; i < Math.Min(frames, (w + 1) * win); i++)
                p = Math.Max(p, Math.Max(Math.Abs(BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(i * 4))), Math.Abs(BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(i * 4 + 2)))) / 32768.0);
            var loud = p > 0.01;
            if (loud && !on) { on = true; start = w; peak = 0; }
            if (on) peak = Math.Max(peak, p);
            if (!loud && on)
            {
                on = false;
                Console.WriteLine($"  {start * win / 44100.0,8:0.00} - {w * win / 44100.0,8:0.00} s  peak {peak:0.000}");
            }
        }
        if (on) Console.WriteLine($"  {start * win / 44100.0,8:0.00} - end  peak {peak:0.000}");
        return 0;
    }
}
