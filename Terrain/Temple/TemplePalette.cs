using System.Buffers.Binary;
using System.IO;

namespace LBAAssembler.Terrain.Temple;

// The Temple of Bú's palette: an LBA2 island palette (RESS.HQR's XPL, the engine's ChoicePalette) in which LBA1's own colours for the temple
// are exact. An island's palette colours everything drawn there -- the cars, Twinsen, the race's bodies and the screen's text all by palette
// index, in the colours LBA2's palettes share (the islands' are one palette, tuned) -- so LBA1's whole palette won't do. Instead the island's
// (Otringal's, Source: the temple's scenes are island 4's) has the temple's colours written into it: each, the most drawn first, into the
// slot of the island's palette nearest it that no other has taken, when that slot is no further from it than MaxShift -- a colour an actor
// drawn there hardly sees change. The few the temple hardly draws that have no slot near them are drawn in the nearest colour.
//
// The tables after the palette are made for it: the shading and fog table (a row a light level, 0..15, and a fog level -- row level + 16 *
// fog: the engine's PtrCLUTFog), each level's colours the palette's own times the retail palettes' ratio for it, faded toward the fog's
// colour by fog / 15 -- its row 12 (ShadeNormalLevel, the level objects' textures are drawn at) the palette itself, so the temple's bricks
// are drawn in LBA1's colours; and the transparency table (two colours' half-way). The fog is black: far rooms fade into the dark round them,
// as LBA1 draws the temple in the black.
internal static class TemplePalette
{
    // RESS.HQR: Otringal's palette (island 4's)
    public const int Source = 31;
    public const int HeaderSize = 44, PaletteAt = 44, FogAt = 812, TranspAt = 66348, Size = 131884;
    // (an 8-bit RGB distance, weighted as PolarTextures.Colours does: 3 red, 4 green, 2 blue -- about 24 plain)
    public const double MaxShift = 24;
    // the shading's ratio for each light level, as the retail palettes have it (RESS 31's rows, measured: 12 the colours as they are)
    private static readonly double[] Ratio = { 0.277, 0.333, 0.394, 0.455, 0.511, 0.568, 0.625, 0.688, 0.750, 0.806, 0.870, 0.935, 1.0, 1.154, 1.350, 1.511 };
    public const int NormalLevel = 12, FogColour = 0;

    // Xpl: the palette's RESS entry. Map: each LBA1 colour's index in it. Placed: LBA1 colours given a slot of their own (exact), of Used;
    // Pixels: the share of the temple's pixels in an exact colour.
    public sealed record Result(byte[] Xpl, int[] Map, byte[] Palette, int Placed, int Used, double Pixels, double MeanShift, double WorstShift, List<string> Log);

    // `counts`: how many of the temple's pixels each LBA1 colour is (TempleObjects.ColourCounts).
    public static Result Build(byte[] sourceXpl, byte[] lba1Palette, long[] counts)
    {
        var log = new List<string>();
        if (sourceXpl.Length < Size) throw new InvalidDataException($"RESS.HQR entry {Source} is not an island palette ({sourceXpl.Length} bytes).");
        var paletteAt = BinaryPrimitives.ReadInt32LittleEndian(sourceXpl.AsSpan(4));
        var pal = new int[256, 3];
        for (var i = 0; i < 256; i++) for (var k = 0; k < 3; k++) pal[i, k] = sourceXpl[paletteAt + i * 3 + k];
        var lba1 = new (int R, int G, int B)[256];
        for (var c = 0; c < 256; c++) lba1[c] = Polar.PolarTextures.Colours.Rgb(lba1Palette, c);
        static double D((int R, int G, int B) a, int r, int g, int b) => Math.Sqrt(((a.R - r) * (a.R - r) * 3 + (a.G - g) * (a.G - g) * 4 + (a.B - b) * (a.B - b) * 2) / 3.0);

        // the temple's colours into the island's slots (16..239: the island's lit colours, as PolarTextures.Colours matches them)
        var map = new int[256];
        Array.Fill(map, -1);
        var taken = new bool[256];
        double shiftSum = 0, worst = 0; long placedPixels = 0, total = counts.Sum();
        var used = Enumerable.Range(0, 256).Where(c => counts[c] > 0).OrderByDescending(c => counts[c]).ToList();
        foreach (var c in used)
        {
            int best = -1; var bestD = double.MaxValue;
            for (var p = 16; p <= 239; p++)
            {
                if (taken[p]) continue;
                var d = D(lba1[c], pal[p, 0], pal[p, 1], pal[p, 2]);
                if (d < bestD) { bestD = d; best = p; }
            }
            if (best < 0 || bestD > MaxShift) continue;
            taken[best] = true;
            pal[best, 0] = lba1[c].R; pal[best, 1] = lba1[c].G; pal[best, 2] = lba1[c].B;
            map[c] = best;
            shiftSum += bestD; worst = Math.Max(worst, bestD); placedPixels += counts[c];
        }
        var placed = used.Count(c => map[c] >= 0);
        // (the rest: the nearest of the palette as it is now)
        for (var c = 0; c < 256; c++)
            if (map[c] < 0) map[c] = Nearest(pal, lba1[c].R, lba1[c].G, lba1[c].B, 16, 239);
        log.Add($"the palette: RESS.HQR {Source}'s (Otringal's) with {placed} of the temple's {used.Count} LBA1 colours in slots of their own ({100.0 * placedPixels / Math.Max(1, total):0.00} % of its pixels exact; " +
                $"slots moved {(placed > 0 ? shiftSum / placed : 0):0.0} on average, {worst:0.0} at most); the rest the nearest");

        // the tables
        var xpl = new byte[Size];
        sourceXpl.AsSpan(0, HeaderSize).CopyTo(xpl);
        BinaryPrimitives.WriteInt32LittleEndian(xpl.AsSpan(4), PaletteAt);
        BinaryPrimitives.WriteInt32LittleEndian(xpl.AsSpan(12), FogAt);
        BinaryPrimitives.WriteInt32LittleEndian(xpl.AsSpan(16), TranspAt);
        BinaryPrimitives.WriteInt32LittleEndian(xpl.AsSpan(24), NormalLevel);
        BinaryPrimitives.WriteInt32LittleEndian(xpl.AsSpan(32), FogColour);
        for (var i = 0; i < 256; i++) for (var k = 0; k < 3; k++) xpl[PaletteAt + i * 3 + k] = (byte)pal[i, k];
        var cache = new Dictionary<int, byte>();
        byte Near(double r, double g, double b)
        {
            int ri = (int)Math.Clamp(Math.Round(r), 0, 255), gi = (int)Math.Clamp(Math.Round(g), 0, 255), bi = (int)Math.Clamp(Math.Round(b), 0, 255);
            var key = ri << 16 | gi << 8 | bi;
            if (cache.TryGetValue(key, out var known)) return known;
            return cache[key] = (byte)Nearest(pal, ri, gi, bi, 1, 239);
        }
        int fr = pal[FogColour, 0], fg = pal[FogColour, 1], fb = pal[FogColour, 2];
        for (var fog = 0; fog < 16; fog++)
            for (var level = 0; level < 16; level++)
            {
                var row = FogAt + (level + 16 * fog) * 256;
                var f = fog / 15.0;
                for (var c = 0; c < 256; c++)
                {
                    if (c == 0) { xpl[row] = 0; continue; }
                    if (fog == 0 && level == NormalLevel) { xpl[row + c] = (byte)c; continue; }
                    var s = Ratio[level];
                    double r = Math.Min(255, pal[c, 0] * s), g = Math.Min(255, pal[c, 1] * s), b = Math.Min(255, pal[c, 2] * s);
                    xpl[row + c] = Near(r + (fr - r) * f, g + (fg - g) * f, b + (fb - b) * f);
                }
            }
        for (var a = 0; a < 256; a++)
            for (var b = 0; b < 256; b++)
                xpl[TranspAt + a * 256 + b] = Near((pal[a, 0] + pal[b, 0]) / 2.0, (pal[a, 1] + pal[b, 1]) / 2.0, (pal[a, 2] + pal[b, 2]) / 2.0);
        var palette = xpl.AsSpan(PaletteAt, 768).ToArray();
        return new Result(xpl, map, palette, placed, used.Count, (double)placedPixels / Math.Max(1, total), placed > 0 ? shiftSum / placed : 0, worst, log);
    }

    private static int Nearest(int[,] pal, int r, int g, int b, int from, int to)
    {
        var best = from; var bestD = long.MaxValue;
        for (var p = from; p <= to; p++)
        {
            long dr = r - pal[p, 0], dg = g - pal[p, 1], db = b - pal[p, 2];
            var d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
            if (d < bestD) { bestD = d; best = p; }
        }
        return best;
    }

    // The island's sky and sea (RESS.HQR's SKYSEA entry, 256 x 256): its sea (the left half of the top 128 rows) black, the void LBA1 draws
    // the temple in; its sky (their right half) a night's -- dark violet clouds drifting over near-black, a pattern that repeats seamlessly
    // across the 128 x 128 square as the engine tiles it; the rest (the shore's strip under them) black.
    public static byte[] Sky(byte[] palette)
    {
        var pal = new int[256, 3];
        for (var i = 0; i < 256; i++) for (var k = 0; k < 3; k++) pal[i, k] = palette[i * 3 + k];
        var sky = new byte[256 * 256];
        // (tileable value noise: a lattice that wraps every 128 pixels, several octaves)
        var random = new Random(1031);
        double[][] lattices = new[] { 4, 8, 16, 32 }.Select(n => Enumerable.Range(0, n * n).Select(_ => random.NextDouble()).ToArray()).ToArray();
        double Noise(int x, int y)
        {
            double sum = 0, weight = 0, amp = 1;
            foreach (var (lattice, n) in lattices.Zip(new[] { 4, 8, 16, 32 }))
            {
                double u = x * n / 128.0, v = y * n / 128.0;
                int i0 = (int)Math.Floor(u), j0 = (int)Math.Floor(v);
                double fu = u - i0, fv = v - j0;
                fu = fu * fu * (3 - 2 * fu); fv = fv * fv * (3 - 2 * fv);
                double L(int i, int j) => lattice[((j % n + n) % n) * n + (i % n + n) % n];
                var value = (L(i0, j0) * (1 - fu) + L(i0 + 1, j0) * fu) * (1 - fv) + (L(i0, j0 + 1) * (1 - fu) + L(i0 + 1, j0 + 1) * fu) * fv;
                sum += value * amp; weight += amp; amp *= 0.55;
            }
            return sum / weight;
        }
        var near = new Dictionary<int, byte>();
        for (var y = 0; y < 128; y++)
            for (var x = 0; x < 128; x++)
            {
                var cloud = Math.Clamp((Noise(x, y) - 0.42) / 0.35, 0, 1);
                cloud = cloud * cloud;
                // near-black blue night, clouds a dull violet lit faintly from beneath (a moon somewhere)
                double r = 8 + 58 * cloud, g = 6 + 34 * cloud, b = 18 + 70 * cloud;
                var key = (int)r << 16 | (int)g << 8 | (int)b;
                if (!near.TryGetValue(key, out var idx)) near[key] = idx = (byte)Nearest(pal, (int)r, (int)g, (int)b, 1, 239);
                sky[y * 256 + 128 + x] = idx;
            }
        return sky;
    }
}
