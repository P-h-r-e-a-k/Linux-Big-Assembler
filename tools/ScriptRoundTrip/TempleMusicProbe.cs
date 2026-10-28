using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using LBAAssembler;
using LBAAssembler.Lba1;
using LBAAssembler.Terrain.Temple;

namespace ScriptRoundTrip;

// PROBE (2026-10-18): the Temple of Bú's race music (Temple.TempleMusic), checked.
//   templemusic find <LBA1 folder> <out dir>: LBA1's temple scenes' music numbers, where the build finds LBA1's CD track for it, the
//     converted WAV written to <out dir> (as the build writes it) with its whole track beside it (untrimmed), their lengths, and a picture of
//     each (waveform over spectrogram); the trimmed track checked to be the image's own bytes
//   templemusic picture <wav> <png>: a WAV's waveform over its spectrogram
//   templemusic installs: the LBA1s GOG and Steam installed on this PC (TempleMusic.InstalledLba1Folders), and what each one's music is
//   templemusic decode <wav|mp3> <out wav>: an audio file as TempleMusic reads it (the CD's format: 16-bit stereo 44.1 kHz)
//   templemusic compare <a wav> <b wav>: whether two recordings are the same music -- the lag that lines them up (FFT cross-correlation),
//     their correlation there (whole, and the worst 10 s), the gain between them and what is left when one is taken from the other
//   templemusic capture <raw> <source wav> <png> [rate]: a recording of the engine's output (SDL's disk audio driver: 16-bit stereo, the
//     rate given, 44100 by default) against the music it should be: where it is silent, and which stretch of the source each stretch of it
//     is (the music played again behind itself shows as the source's end followed by its start)
internal static class TempleMusicProbe
{
    public static int Run(string[] args) => args[1] switch
    {
        "find" => Find(args[2], args[3]),
        "picture" => PictureCommand(args[2], args[3]),
        "capture" => Capture(args),
        "installs" => Installs(),
        "decode" => Decode(args[2], args[3]),
        "compare" => Compare(args[2], args[3]),
        "midi" => Midi(args[2], args[3]),
        "mididump" => MidiDump(args[2], args[3]),
        _ => throw new ArgumentException("templemusic find|picture|capture|installs|decode|compare|midi"),
    };

    // templemusic midi <LBA1 folder> <out dir>: MIDI_MI.HQR's scores -- each one's length, notes, and what its XMIDI stream holds besides
    // notes (meta events by type, sysex, controllers XMIDI gives a meaning of its own: 110-120), each written as entry_NN.mid; and the
    // temple's (TempleMusic.MidiScore) as the build writes it
    private static int Midi(string lba1, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var archive = HqrArchive.Open(Path.Combine(lba1, TempleMusic.MidiArchiveName));
        Console.WriteLine($"{TempleMusic.MidiArchiveName}: {archive.Count} entries");
        foreach (var i in archive.ValidIndices)
        {
            var xmi = archive.Read(i);
            var smf = LBAAssembler.Lba1.Runtime.Lba1Xmi.ToMidi(xmi);
            var (seconds, notes, events) = smf is null ? (0.0, 0, 0) : TempleMusic.MidiFacts(smf);
            var extra = XmiExtras(xmi);
            if (smf is not null) File.WriteAllBytes(Path.Combine(outDir, $"entry_{i:00}.mid"), smf);
            Console.WriteLine($"  {i,2}: {xmi.Length,6} bytes XMI -> {(smf is null ? "not XMIDI" : $"{(int)(seconds / 60)}:{seconds % 60:00.0} ({seconds:0.0} s), {notes} notes, {events} channel events")}{extra}");
        }
        var music = TempleMusic.MusicNumber(lba1);
        var score = TempleMusic.MidiScore(new[] { lba1 }, music);
        Console.WriteLine($"the temple's music {music}: {(score is { } s ? $"{s.Archive} entry {music}, {s.Midi.Length} bytes as a MIDI file" : "none")}");
        if (score is { } got) File.WriteAllBytes(Path.Combine(outDir, "LBA1_TEMPLE.MID"), got.Midi);
        return 0;
    }

    // templemusic mididump <mid> <dump>: the engine's MIDI sequencer's messages (LBA2_MIDI_DUMP: "<wall ms> ms  at <place ms> ms  <bytes>")
    // against the file's own events (its one track at 120 ticks a second, as Lba1Xmi writes it): each of the file's channel messages found
    // in order in the dump, how late it was sent (the sequencer's place when it sent it, less the event's time), the wall clock against the
    // place (a steady clock: their difference constant, but for whole loops of the music), and the dump's other lines (volumes, notes off)
    private static int MidiDump(string midPath, string dumpPath)
    {
        var smf = File.ReadAllBytes(midPath);
        var division = smf[12] << 8 | smf[13];
        var expected = new List<(double Ms, string Bytes)>();
        var p = 22; var stop = smf.Length; long tick = 0; byte status = 0;
        int Vlq() { var v = 0; for (var i = 0; i < 4 && p < stop; i++) { var b = smf[p++]; v = v << 7 | (b & 0x7F); if ((b & 0x80) == 0) break; } return v; }
        var usPerTick = 500000.0 / division;
        double endMs = 0;
        while (p < stop)
        {
            tick += Vlq();
            if (smf[p] >= 0x80) status = smf[p++];
            if (status == 0xFF) { var type = smf[p++]; var len = Vlq(); if (type == 0x51) usPerTick = (smf[p] << 16 | smf[p + 1] << 8 | smf[p + 2]) / (double)division; p += len; if (type == 0x2F) { endMs = tick * usPerTick / 1000; break; } continue; }
            if (status is 0xF0 or 0xF7) { p += Vlq(); continue; }
            var n = (status & 0xF0) is 0xC0 or 0xD0 ? 1 : 2;
            expected.Add((tick * usPerTick / 1000, n == 1 ? $"{status:X2} {smf[p]:X2}" : $"{status:X2} {smf[p]:X2} {smf[p + 1]:X2}"));
            p += n;
        }
        Console.WriteLine($"{midPath}: {expected.Count} channel messages, {endMs / 1000:0.000} s");
        var lines = new List<(double Wall, double At, string Bytes, string Why)>();
        var rx = new System.Text.RegularExpressions.Regex(@"^\s*([\d.]+) ms\s+at\s+([\d.]+) ms\s+((?:[0-9A-F]{2} ?)+)\s*(.*)$");
        foreach (var line in File.ReadLines(dumpPath))
        {
            var m = rx.Match(line);
            if (m.Success) lines.Add((double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), m.Groups[3].Value.Trim(), m.Groups[4].Value.Trim()));
            else if (line.StartsWith('#') || line.StartsWith('(')) Console.WriteLine($"  dump: {line}");
        }
        Console.WriteLine($"{dumpPath}: {lines.Count} messages");
        foreach (var why in lines.Where(l => l.Why.Length > 0).GroupBy(l => l.Why)) Console.WriteLine($"  '{why.Key}': {why.Count()} time(s), first at {why.First().Wall / 1000:0.000} s (wall), the music at {why.First().At / 1000:0.000} s");
        // the file's events in order, through the loops
        var found = 0; var late = new List<double>(); var at = 0; var loops = 0; var drift = new List<double>();
        double? offset = null;
        for (var li = 0; li < lines.Count && found < 100000; li++)
        {
            var e = expected[at];
            if (lines[li].Bytes != e.Bytes) continue;
            late.Add(lines[li].At - e.Ms);
            var o = lines[li].Wall - lines[li].At - loops * endMs;
            offset ??= o;
            drift.Add(o - offset.Value);
            found++;
            if (++at == expected.Count) { at = 0; loops++; }
        }
        late.Sort();
        Console.WriteLine($"the file's messages found in order: {found} ({loops} whole loops and {at} more)");
        if (late.Count > 0)
            Console.WriteLine($"  sent late by (the sequencer's place less the event's time): median {late[late.Count / 2]:0.000} ms, 99th percentile {late[(int)(late.Count * 0.99)]:0.000} ms, " +
                              $"most {late[^1]:0.000} ms, least {late[0]:0.000} ms");
        if (drift.Count > 0) Console.WriteLine($"  wall clock less the music's place (less whole loops), from its first: {drift.Min():0.000} to {drift.Max():0.000} ms");
        return 0;
    }

    // What an XMIDI score's events hold besides notes: meta events by type, sysex, XMIDI's own controllers (110-120: channel lock, patch
    // bank, callback, FOR/NEXT loops ...), and its channels' programs
    private static string XmiExtras(byte[] d)
    {
        var start = -1; var length = 0;
        for (var p = 0; p + 8 <= d.Length; p++)
            if (d[p] == 'E' && d[p + 1] == 'V' && d[p + 2] == 'N' && d[p + 3] == 'T') { start = p + 8; length = d[p + 4] << 24 | d[p + 5] << 16 | d[p + 6] << 8 | d[p + 7]; break; }
        if (start < 0) return "";
        var metas = new SortedDictionary<int, int>(); var xcc = new SortedDictionary<int, int>(); var programs = new SortedSet<string>();
        var sysex = 0;
        var q = start; var end = Math.Min(d.Length, start + length);
        int Vlq() { var v = 0; for (var i = 0; i < 4 && q < end; i++) { var b = d[q++]; v = v << 7 | (b & 0x7F); if ((b & 0x80) == 0) break; } return v; }
        while (q < end)
        {
            var b = d[q];
            if (b < 0x80) { q++; continue; }
            q++;
            if (b == 0xFF) { var type = d[q++]; var len = Vlq(); metas[type] = metas.GetValueOrDefault(type) + 1; if (type == 0x51 && len == 3) programs.Add($"tempo {d[q] << 16 | d[q + 1] << 8 | d[q + 2]}"); q += len; if (type == 0x2F) break; continue; }
            if (b is 0xF0 or 0xF7) { sysex++; q += Vlq(); continue; }
            switch (b & 0xF0)
            {
                case 0x90: q += 2; Vlq(); break;
                case 0xC0: programs.Add($"ch{(b & 15) + 1}:{d[q]}"); q++; break;
                case 0xD0: q++; break;
                case 0xB0: if (d[q] is >= 110 and <= 120) xcc[d[q]] = xcc.GetValueOrDefault(d[q]) + 1; q += 2; break;
                default: q += 2; break;
            }
        }
        return $"; meta {string.Join(" ", metas.Select(m => $"{m.Key:X2}x{m.Value}"))}; sysex {sysex}; XMIDI CCs {string.Join(" ", xcc.Select(m => $"{m.Key}x{m.Value}"))}; {string.Join(" ", programs)}";
    }

    private static int Find(string lba1, string outDir)
    {
        Directory.CreateDirectory(outDir);
        if (File.Exists(Path.Combine(lba1, "SCENE.HQR")))
        {
            var archive = HqrArchive.Open(Path.Combine(lba1, "SCENE.HQR"));
            foreach (var s in new[] { 8, 40, 41 })
                Console.WriteLine($"LBA1 scene {s}: music {Lba1Scene.Parse(s, archive.Read(s)).Music}");
        }
        else Console.WriteLine($"(no SCENE.HQR in {lba1}: the temple's music taken as 5)");
        var music = TempleMusic.MusicNumber(lba1);
        Console.WriteLine($"the temple's music (scene {TempleMusic.TempleScene}): {music} -> CD track {music + 1}");
        Console.WriteLine($"looked in: {string.Join(" | ", TempleMusic.Places(lba1))}");
        var notes = new List<string>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var found = TempleMusic.Find(TempleMusic.Places(lba1), music + 1, notes);
        Console.WriteLine($"find took {watch.Elapsed.TotalSeconds:0.00} s");
        foreach (var n in notes) Console.WriteLine($"note: {n}");
        if (found is null) { Console.WriteLine("NOT FOUND"); return 1; }
        var (source, pcm, ogg, lead) = found.Value;
        Console.WriteLine($"found: {source}{(ogg is not null ? " (an OGG, copied as it is)" : "")}");
        if (pcm is null) return 0;
        var (trimmed, note) = TempleMusic.Cut(pcm, lead);
        Console.WriteLine($"read from {lead / 44100.0:0.000} s before the cue's INDEX 01; cut: {note}");
        var whole = Path.Combine(outDir, "track_whole.wav");
        var cut = Path.Combine(outDir, "LBA1_TEMPLE.WAV");
        TempleMusic.WriteWav(whole, pcm);
        TempleMusic.WriteWav(cut, trimmed);
        var at = pcm.AsSpan().IndexOf(trimmed.AsSpan(0, 1 << 16));
        var same = at >= 0 && pcm.AsSpan(at, trimmed.Length).SequenceEqual(trimmed);
        Console.WriteLine($"whole track: {pcm.Length} bytes = {pcm.Length / 4 / 44100.0:0.000} s; trimmed: {trimmed.Length} bytes = {trimmed.Length / 4 / 44100.0:0.000} s " +
                          $"(from {at / 4 / 44100.0:0.000} s; {(pcm.Length - at - trimmed.Length) / 4 / 44100.0:0.000} s off the end); the image's own bytes: {(same ? "yes" : "NO")}");
        Console.WriteLine($"sha256 of the trimmed samples: {Convert.ToHexString(SHA256.HashData(trimmed))[..16]}");
        Stats("whole", pcm);
        Stats("trimmed", trimmed);
        Picture(pcm, 44100, Path.Combine(outDir, "track_whole.png"), $"LBA1 CD track {music + 1} (music {music}) as the image has it");
        Picture(trimmed, 44100, Path.Combine(outDir, "LBA1_TEMPLE.png"), "LBA1_TEMPLE.WAV (trimmed)");
        return 0;
    }

    private static int Installs()
    {
        var installs = TempleMusic.InstalledLba1Folders();
        Console.WriteLine($"{installs.Count} LBA1 install(s) on this PC");
        foreach (var dir in installs)
        {
            var notes = new List<string>();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var found = TempleMusic.Find(TempleMusic.Places(dir), TempleMusic.MusicNumber(dir) + 1, notes);
            Console.WriteLine($"  {dir}: {(found is { } f ? f.Source : "no music")} ({watch.Elapsed.TotalSeconds:0.0} s)");
            foreach (var note in notes) Console.WriteLine($"    note: {note}");
        }
        return 0;
    }

    private static int Decode(string input, string output)
    {
        var pcm = TempleMusic.ReadAudio(input) ?? throw new InvalidDataException($"{input}: not a WAV or MP3 this reads");
        TempleMusic.WriteWav(output, pcm);
        Console.WriteLine($"{output}: {pcm.Length / 4 / 44100.0:0.000} s (sha256 {Convert.ToHexString(SHA256.HashData(pcm))[..16]})");
        return 0;
    }

    // Two recordings lined up (mono, 11025 Hz, FFT cross-correlation; then to the sample at 44.1 kHz) and compared where they overlap
    private static int Compare(string a, string b)
    {
        var pa = ReadWavPcm(a, out _); var pb = ReadWavPcm(b, out _);
        float[] Mono(byte[] p) { var n = p.Length / 4; var m = new float[n]; for (var i = 0; i < n; i++) m[i] = ((short)(p[4 * i] | p[4 * i + 1] << 8) + (short)(p[4 * i + 2] | p[4 * i + 3] << 8)) / 65536f; return m; }
        float[] Quarter(float[] m) { var q = new float[m.Length / 4]; for (var i = 0; i < q.Length; i++) q[i] = (m[4 * i] + m[4 * i + 1] + m[4 * i + 2] + m[4 * i + 3]) / 4; return q; }
        var ma = Mono(pa); var mb = Mono(pb);
        Console.WriteLine($"a {a}: {ma.Length / 44100.0:0.000} s; b {b}: {mb.Length / 44100.0:0.000} s (b - a: {(mb.Length - ma.Length) / 44100.0:+0.000;-0.000} s)");
        var qa = Quarter(ma); var qb = Quarter(mb);
        var n = 1; while (n < qa.Length + qb.Length) n <<= 1;
        var fa = new Complex[n]; var fb = new Complex[n];
        for (var i = 0; i < qa.Length; i++) fa[i] = qa[i];
        for (var i = 0; i < qb.Length; i++) fb[i] = qb[i];
        Fft(fa); Fft(fb);
        // (correlation c[k] = sum a[i + k] b[i]: the inverse FFT of A conj(B), as the conjugate of the forward FFT of its conjugate)
        for (var i = 0; i < n; i++) fa[i] = Complex.Conjugate(fa[i] * Complex.Conjugate(fb[i]));
        Fft(fa);
        var best = 0; var bestV = double.MinValue;
        for (var k = 0; k < n; k++) if (fa[k].Real > bestV) { bestV = fa[k].Real; best = k; }
        var lag = (best > n / 2 ? best - n : best) * 4;
        // (to the sample: the best of the 6 either side)
        double Corr(int shift, out double gain, out double snr, int from = 0, int to = int.MaxValue)
        {
            double sab = 0, saa = 0, sbb = 0;
            var i0 = Math.Max(Math.Max(0, shift), from); var i1 = Math.Min(Math.Min(ma.Length, mb.Length + shift), to);
            for (var i = i0; i < i1; i++) { double x = ma[i], y = mb[i - shift]; sab += x * y; saa += x * x; sbb += y * y; }
            gain = sbb > 0 ? sab / sbb : 0;
            var residual = saa - 2 * gain * sab + gain * gain * sbb;
            snr = 10 * Math.Log10(saa / Math.Max(1e-12, residual));
            return saa > 0 && sbb > 0 ? sab / Math.Sqrt(saa * sbb) : 0;
        }
        var bestShift = lag; var bestC = double.MinValue;
        for (var s = lag - 6; s <= lag + 6; s++) { var c = Corr(s, out _, out _); if (c > bestC) { bestC = c; bestShift = s; } }
        var whole = Corr(bestShift, out var g, out var snr);
        Console.WriteLine($"lined up: b starts {bestShift / 44100.0:+0.0000;-0.0000} s into a ({bestShift} samples); correlation {whole:0.00000}; gain a/b {g:0.0000} ({20 * Math.Log10(Math.Abs(g)):+0.00;-0.00} dB); " +
                          $"what's left of a with b taken out: {snr:0.0} dB below it");
        var worst = 1.0; var worstAt = 0;
        var overlapTo = Math.Min(ma.Length, mb.Length + bestShift);
        for (var t = Math.Max(0, bestShift); t + 441000 <= overlapTo; t += 441000)
        {
            var c = Corr(bestShift, out _, out _, t, t + 441000);
            if (c < worst) { worst = c; worstAt = t; }
        }
        Console.WriteLine($"worst 10 s: correlation {worst:0.00000} at {worstAt / 44100.0:0.0} s of a");
        // (stretch by stretch: 5 s of a every 10 s lined up on its own with b, within 0.5 s of the whole's lag -- a recording played at
        // another speed shows as a lag that grows; then the loudness envelopes, 50 ms windows in dB, which a different master or mix keeps)
        const int W = 5 * 44100, R = 22050;
        var m = 1; while (m < 2 * (W + 2 * R)) m <<= 1;
        var locals = new List<(double At, int Lag, double C)>();
        for (var t = Math.Max(0, bestShift + R); t + W <= Math.Min(ma.Length, mb.Length + bestShift - R); t += 441000)
        {
            var x = new Complex[m]; var y = new Complex[m];
            for (var i = 0; i < W; i++) x[i] = ma[t + i];
            var b0 = t - bestShift - R;
            for (var i = 0; i < W + 2 * R; i++) y[i] = mb[b0 + i];
            Fft(x); Fft(y);
            for (var i = 0; i < m; i++) x[i] = Complex.Conjugate(Complex.Conjugate(x[i]) * y[i]);
            Fft(x);
            var bk = 0; var bv = double.MinValue;
            for (var k = 0; k <= 2 * R; k++) if (x[k].Real > bv) { bv = x[k].Real; bk = k; }
            // (a[t + i] lines up with b[b0 + bk + i]: a's shift over b there)
            var shift = t - (b0 + bk);
            locals.Add((t / 44100.0, shift, Corr(shift, out _, out _, t, t + W)));
        }
        foreach (var (at, l, c) in locals) Console.WriteLine($"  at {at,6:0.0} s of a: lag {l - bestShift,+5} samples from the whole's, correlation {c:0.0000}");
        double[] Envelope(float[] mono, int shift)
        {
            var e = new double[Math.Max(0, (Math.Min(ma.Length, mono.Length + shift) - Math.Max(0, shift)) / 2205)];
            for (var w = 0; w < e.Length; w++)
            {
                double sum = 0;
                for (var i = 0; i < 2205; i++) { double v = mono[Math.Max(0, shift) + w * 2205 + i - shift]; sum += v * v; }
                e[w] = 10 * Math.Log10(sum / 2205 + 1e-10);
            }
            return e;
        }
        var ea = Envelope(ma, 0)[(Math.Max(0, bestShift) / 2205)..]; var eb = Envelope(mb, bestShift);
        var ne = Math.Min(ea.Length, eb.Length);
        double Mean(double[] v) => v.Take(ne).Average();
        double mA = Mean(ea), mB = Mean(eb), sab2 = 0, saa2 = 0, sbb2 = 0;
        for (var i = 0; i < ne; i++) { sab2 += (ea[i] - mA) * (eb[i] - mB); saa2 += (ea[i] - mA) * (ea[i] - mA); sbb2 += (eb[i] - mB) * (eb[i] - mB); }
        var envelope = sab2 / Math.Sqrt(saa2 * sbb2);
        Console.WriteLine($"loudness envelopes (50 ms, dB): correlation {envelope:0.0000}; b is {mB - mA:+0.0;-0.0;0.0} dB against a on average");
        var localMean = locals.Count > 0 ? locals.Average(l => l.C) : whole;
        // (the lag's drift: a straight line through the stretches' lags, in samples per second -- parts per million of the speed)
        double drift = 0;
        if (locals.Count > 2)
        {
            double mt = locals.Average(l => l.At), ml = locals.Average(l => (double)l.Lag);
            drift = locals.Sum(l => (l.At - mt) * (l.Lag - ml)) / locals.Sum(l => (l.At - mt) * (l.At - mt));
        }
        Console.WriteLine(whole > 0.9 ? "SAME RECORDING (sample for sample)"
            : localMean > 0.95 ? $"SAME RECORDING, stretch by stretch (correlation {localMean:0.000} on average), b's clock {-drift / 44100 * 1e6:+0.0;-0.0} ppm against a's (its lag drifts {drift:0.00} samples a second) -- resampled; {mB - mA:+0.0;-0.0;0.0} dB"
            : envelope > 0.9 ? $"SAME MUSIC (its envelope; sample for sample: {localMean:0.00} on average stretch by stretch -- another master or mix)" : "DIFFERENT");
        return 0;
    }

    private static void Stats(string name, byte[] pcm)
    {
        var frames = pcm.Length / 4;
        double sum = 0; var peak = 0;
        for (var i = 0; i < frames * 2; i++)
        {
            int v = (short)(pcm[2 * i] | pcm[2 * i + 1] << 8);
            sum += (double)v * v; peak = Math.Max(peak, Math.Abs(v));
        }
        Console.WriteLine($"{name}: peak {peak} ({20 * Math.Log10(Math.Max(1, peak) / 32768.0):0.0} dBFS), rms {20 * Math.Log10(Math.Sqrt(sum / (frames * 2)) / 32768.0):0.0} dBFS; " +
                          $"first/last 0.5 s rms {Rms(pcm, 0, 22050):0}/{Rms(pcm, frames - 22050, frames):0}");
    }

    private static double Rms(byte[] pcm, int from, int to)
    {
        double sum = 0; var n = 0;
        for (var f = Math.Max(0, from); f < Math.Min(to, pcm.Length / 4); f++)
            for (var c = 0; c < 2; c++) { int v = (short)(pcm[4 * f + 2 * c] | pcm[4 * f + 2 * c + 1] << 8); sum += (double)v * v; n++; }
        return n > 0 ? Math.Sqrt(sum / n) : 0;
    }

    private static byte[] ReadWavPcm(string path, out int rate)
    {
        var d = File.ReadAllBytes(path);
        rate = 44100;
        var p = 12;
        while (p + 8 <= d.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(d, p, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p + 4));
            if (id == "fmt ") rate = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p + 12));
            if (id == "data") return d.AsSpan(p + 8, Math.Min(size, d.Length - p - 8)).ToArray();
            p += 8 + size + (size & 1);
        }
        throw new InvalidDataException("no data chunk");
    }

    private static int PictureCommand(string wav, string png)
    {
        var pcm = ReadWavPcm(wav, out var rate);
        Picture(pcm, rate, png, Path.GetFileName(wav));
        return 0;
    }

    // Waveform (top, 160 px: min/max per column) over a spectrogram (0-11 kHz, log magnitude), 1600 px wide whatever the length; a tick
    // every 10 s; `marks`: seconds to draw a red line at
    public static void Picture(byte[] pcm, int rate, string png, string title, IReadOnlyList<double>? marks = null)
    {
        const int W = 1600, WaveH = 160, SpecH = 256, N = 1024;
        var H = WaveH + SpecH + 4;
        var frames = pcm.Length / 4;
        var img = new byte[W * H * 4];
        void Px(int x, int y, byte r, byte g, byte b) { if (x < 0 || y < 0 || x >= W || y >= H) return; var i = (y * W + x) * 4; img[i] = b; img[i + 1] = g; img[i + 2] = r; img[i + 3] = 255; }
        for (var y = 0; y < H; y++) for (var x = 0; x < W; x++) Px(x, y, 16, 16, 20);
        short Mono(int f) => (short)(((short)(pcm[4 * f] | pcm[4 * f + 1] << 8) + (short)(pcm[4 * f + 2] | pcm[4 * f + 3] << 8)) / 2);
        for (var x = 0; x < W; x++)
        {
            int f0 = (int)((long)frames * x / W), f1 = Math.Max(f0 + 1, (int)((long)frames * (x + 1) / W));
            int lo = 0, hi = 0;
            for (var f = f0; f < f1 && f < frames; f++) { var v = Mono(f); lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
            int y0 = WaveH / 2 - hi * (WaveH / 2) / 32768, y1 = WaveH / 2 - lo * (WaveH / 2) / 32768;
            for (var y = y0; y <= y1; y++) Px(x, y, 120, 200, 255);
            Px(x, WaveH / 2, 60, 60, 70);
        }
        var window = new double[N];
        for (var i = 0; i < N; i++) window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1));
        var buf = new Complex[N];
        var maxBin = (int)(N / 2 * Math.Min(1.0, 11025.0 / (rate / 2.0)));
        for (var x = 0; x < W; x++)
        {
            var at = (int)((long)(frames - N) * x / W);
            if (at < 0) break;
            for (var i = 0; i < N; i++) buf[i] = new Complex(Mono(at + i) / 32768.0 * window[i], 0);
            Fft(buf);
            for (var y = 0; y < SpecH; y++)
            {
                var bin = 1 + (int)((long)(maxBin - 1) * y / SpecH);
                var db = 20 * Math.Log10(buf[bin].Magnitude + 1e-9);
                var t = Math.Clamp((db + 40) / 70, 0, 1);
                Px(x, H - 1 - y, (byte)(255 * Math.Min(1, t * 1.6)), (byte)(255 * Math.Clamp(t * 1.6 - 0.6, 0, 1)), (byte)(255 * Math.Clamp(0.5 - Math.Abs(t - 0.3), 0, 1)));
            }
        }
        var seconds = frames / (double)rate;
        for (var s = 10; s < seconds; s += 10)
        {
            var x = (int)(W * s / seconds);
            for (var y = 0; y < 8; y++) Px(x, WaveH + y - 4, 255, 255, 255);
        }
        foreach (var m in marks ?? Array.Empty<double>())
        {
            var x = (int)(W * m / seconds);
            for (var y = 0; y < H; y += 2) Px(x, y, 255, 40, 40);
        }
        PngWriter.Write(png, img, W, H);
        Console.WriteLine($"{png}: {title}, {seconds:0.0} s ({W} px, a tick every 10 s{(marks is { Count: > 0 } ? $", {marks.Count} red marks" : "")})");
    }

    private static void Fft(Complex[] a)
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
            var w = Complex.FromPolarCoordinates(1, -2 * Math.PI / len);
            for (var i = 0; i < n; i += len)
            {
                var wn = Complex.One;
                for (var k = 0; k < len / 2; k++)
                {
                    var u = a[i + k]; var v = a[i + k + len / 2] * wn;
                    a[i + k] = u + v; a[i + k + len / 2] = u - v;
                    wn *= w;
                }
            }
        }
    }

    // A recording of the game's output against the music it plays: silences (50 ms windows quieter than -60 dBFS, a run of them 0.2 s or
    // more), and the source's stretches it is made of -- found by matching 16-frame fingerprints of the source (every 32nd frame of it
    // indexed) and followed sample by sample (within 2 of the source: SDL's mix of one stream at full volume is the source itself)
    private static int Capture(string[] args)
    {
        var raw = File.ReadAllBytes(args[2]);
        var source = ReadWavPcm(args[3], out _);
        var png = args[4];
        var rate = args.Length > 5 ? int.Parse(args[5]) : 44100;
        int cf = raw.Length / 4, sf = source.Length / 4;
        Console.WriteLine($"capture {args[2]}: {cf} frames = {cf / (double)rate:0.00} s; source {args[3]}: {sf} frames = {sf / 44100.0:0.00} s");
        short S(byte[] b, int f, int c) => (short)(b[4 * f + 2 * c] | b[4 * f + 2 * c + 1] << 8);
        // silences
        var win = rate / 20;
        var silentRun = 0; var runStart = 0;
        var silences = new List<(double From, double To)>();
        for (var w = 0; w * win < cf; w++)
        {
            double sum = 0; var n = 0;
            for (var f = w * win; f < Math.Min(cf, (w + 1) * win); f++) for (var c = 0; c < 2; c++) { double v = S(raw, f, c); sum += v * v; n++; }
            var db = 20 * Math.Log10(Math.Sqrt(sum / Math.Max(1, n)) / 32768 + 1e-12);
            if (db < -60) { if (silentRun++ == 0) runStart = w; }
            else
            {
                if (silentRun >= 4) silences.Add((runStart * win / (double)rate, w * win / (double)rate));
                silentRun = 0;
            }
        }
        if (silentRun >= 4) silences.Add((runStart * win / (double)rate, cf / (double)rate));
        Console.WriteLine($"silences of 0.2 s or more: {silences.Count}");
        foreach (var (from, to) in silences) Console.WriteLine($"  silent {from:0.00}-{to:0.00} s ({to - from:0.00} s)");
        // the source's stretches
        const int K = 32, L = 16;
        long Key(byte[] b, int f) { long h = 1469598103934665603; for (var i = 0; i < L; i++) { h = (h ^ (ushort)S(b, f + i, 0)) * 1099511628211; h = (h ^ (ushort)S(b, f + i, 1)) * 1099511628211; } return h; }
        // (quiet stretches -- silence, a fade -- match anything: not fingerprinted)
        bool Loud(byte[] b, int f) { for (var i = 0; i < L; i++) if (Math.Abs((int)S(b, f + i, 0)) > 64 || Math.Abs((int)S(b, f + i, 1)) > 64) return true; return false; }
        var index = new Dictionary<long, int>();
        for (var f = 0; f + L < sf; f += K) if (Loud(source, f)) index.TryAdd(Key(source, f), f);
        bool Near(int c, int s) => Math.Abs(S(raw, c, 0) - S(source, s, 0)) <= 2 && Math.Abs(S(raw, c, 1) - S(source, s, 1)) <= 2;
        var marks = new List<double>();
        var stretches = 0;
        var p = 0;
        while (p + L + K < cf)
        {
            // find where the source is at p (exact fingerprints: the first of K positions that hits)
            var hit = -1; var at = -1;
            for (var q = p; q < p + 4096 && q + L < cf && hit < 0; q++)
                if (Loud(raw, q) && index.TryGetValue(Key(raw, q), out var s)) { hit = q; at = s; }
            if (hit < 0) { p += 4096; continue; }
            // back to where this stretch began (the frames before the fingerprint)
            var c0 = hit; var s0 = at;
            while (c0 > p && s0 > 0 && Near(c0 - 1, s0 - 1)) { c0--; s0--; }
            // ... and on, round the end of the source to its start (played again behind itself)
            var c = hit; var sPos = at; var wraps = 0; var miss = 0;
            while (c < cf)
            {
                if (Near(c, sPos)) miss = 0;
                else if (++miss > 8) break;
                c++; sPos++;
                if (sPos == sf) { sPos = 0; wraps++; marks.Add(c / (double)rate); Console.WriteLine($"    at {c / (double)rate:0.000} s of the capture: the source's end, straight on into its start"); }
            }
            var c1 = c - miss;
            stretches++;
            Console.WriteLine($"  capture {c0 / (double)rate:0.000}-{c1 / (double)rate:0.000} s = the source from {s0 / 44100.0:0.000} s, {(c1 - c0) / (double)rate:0.000} s long{(wraps > 0 ? $", round its end {wraps} time(s)" : "")}");
            p = Math.Max(c1, p + 1);
        }
        Console.WriteLine($"{stretches} stretch(es) of the source");
        Picture(raw, rate, png, $"capture {Path.GetFileName(args[2])}", marks);
        return 0;
    }
}
