using System.Diagnostics;
using System.Globalization;
using LBAAssembler;
using LBAAssembler.Maps;
using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// Heightmaps for LBA2 islands (Terrain/IslandHeightmap, HeightmapFile; the terrain editor's Import / Export heightmap):
//   heightmap size <ile or folder>...             the picture size each island needs and which pixel is where
//   heightmap export <ile> <out> [png16|png8|r16|csv]
//   heightmap import <ile> <picture> <out.ile> [--black n] [--white n] [--sea n] [--fit exact|resample|crop] [--textures keep|sea|slope]
//                    [--smooth n] [--steep degrees] [--bake] [--nodiag]
//   heightmap roundtrip <ile or folder>...        export -> import gives every island its own heights back (PNG 16-bit, raw, CSV), byte for byte
//   heightmap selftest <source ile>               synthetic pictures (gradient, cone, 8-bit, resample, crop, transparency, sea, steep, undo)
//   heightmap make <out.png> <kind> <w> <h>        a synthetic picture: gradient, cone, crater, hillcrater (the engine test's)
//   heightmap engine <sandbox game folder> [out folder]   a new map island from a picture of a hill and a crater, played headless from MAPS:
//                    Twinsen walks over it and stays on the ground; screenshots kept in the out folder
internal static class HeightmapCommand
{
    public static int Run(string[] args)
    {
        var what = args.Length > 1 ? args[1] : "";
        return what switch
        {
            "size" => Size(args.Skip(2)),
            "export" => Export(args),
            "import" => Import(args),
            "roundtrip" => RoundTrip(args.Skip(2)),
            "selftest" => SelfTest(args[2]),
            "make" => Make(args),
            "stamp" => Stamp(args),
            "decodecheck" => DecodeCheck(args),
            "engine" => Engine(args),
            _ => Usage(),
        };
    }

    private static int Usage()
    {
        Console.WriteLine("heightmap size|export|import|roundtrip|selftest|make|engine ... (see HeightmapCommand.cs)");
        return 1;
    }

    private static IEnumerable<string> Islands(IEnumerable<string> paths) =>
        paths.SelectMany(p => Directory.Exists(p) ? Directory.GetFiles(p, "*.ILE").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray() : new[] { p });

    private static int Size(IEnumerable<string> paths)
    {
        foreach (var path in Islands(paths))
        {
            var island = IslandFile.Load(path);
            var l = HeightmapLayout.Of(island);
            var (lo, hi) = IslandOps.HeightRange(island);
            var (black, white) = IslandHeightmap.DefaultRange(island);
            Console.WriteLine($"{Path.GetFileName(path),-14} {l.CubesX} x {l.CubesZ} cubes from ({l.CubeX0},{l.CubeZ0}) -> {l.Width} x {l.Height} pixels; heights {lo}..{hi}; default black {black} white {white}");
            Console.WriteLine("    " + IslandHeightmap.SizeText(island, Path.GetFileName(path)));
            Console.WriteLine("    " + IslandHeightmap.LayoutText(island));
            var (sea, flat, steep) = IslandGround.CommonGround(island);
            string P(IslandGround.Sample? s) => s is null ? "none" : $"tex {s.Polygon.TexFlag} poly {s.Polygon.PolyFlag} code {s.Polygon.CodeJeu} index {s.Polygon.TextureIndex}";
            Console.WriteLine($"    sea: {P(sea)}; flat: {P(flat)}; steep: {P(steep)}");
        }
        return 0;
    }

    private static int Export(string[] args)
    {
        var island = IslandFile.Load(args[2]);
        var format = (args.Length > 4 ? args[4] : Path.GetExtension(args[3]).ToLowerInvariant()) switch
        {
            "png8" => IslandHeightmap.ExportFormat.Png8, "r16" or ".r16" or ".raw" => IslandHeightmap.ExportFormat.Raw16, "csv" or ".csv" => IslandHeightmap.ExportFormat.Csv,
            _ => IslandHeightmap.ExportFormat.Png16,
        };
        var (black, white) = IslandHeightmap.Export(island, args[3], format);
        var l = HeightmapLayout.Of(island);
        Console.WriteLine($"{args[3]}: {l.Width} x {l.Height} ({format}), black {black} white {white}");
        return 0;
    }

    private static string? Opt(string[] args, string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }

    private static int Import(string[] args)
    {
        var island = IslandFile.Load(args[2]);
        var l = HeightmapLayout.Of(island);
        var picture = HeightmapFile.Load(args[3], (l.Width, l.Height));
        var (black, white) = picture.Range ?? IslandHeightmap.DefaultRange(island);
        var options = new HeightmapImportOptions
        {
            Black = Opt(args, "--black") is { } b ? double.Parse(b, CultureInfo.InvariantCulture) : black,
            White = Opt(args, "--white") is { } w ? double.Parse(w, CultureInfo.InvariantCulture) : white,
            SeaLevel = Opt(args, "--sea") is { } s ? int.Parse(s, CultureInfo.InvariantCulture) : 0,
            Fit = Opt(args, "--fit") switch { "exact" => HeightmapFit.Exact, "crop" => HeightmapFit.Crop, _ => HeightmapFit.Resample },
            Textures = Opt(args, "--textures") switch { "keep" => HeightmapTextures.Keep, "slope" => HeightmapTextures.AllBySlope, _ => HeightmapTextures.SeaAndShore },
            SmoothPasses = Opt(args, "--smooth") is { } n ? int.Parse(n) : 0,
            SteepAngle = Opt(args, "--steep") is { } a ? double.Parse(a, CultureInfo.InvariantCulture) : null,
            Bake = args.Contains("--bake") || args.Contains("--shadows") ? BakeOptions.For(island.Cubes.Values.First()) : null,
            FixDiagonals = !args.Contains("--nodiag"),
            ObjectsFollow = !args.Contains("--nofollow"),
        };
        if (options.Bake is { } light && args.Contains("--shadows")) light.TerrainShadows = true;
        var clock = Stopwatch.StartNew();
        var result = IslandHeightmap.Apply(island, picture, options);
        Console.WriteLine($"{picture.Kind} {picture.Width} x {picture.Height} -> {l.Width} x {l.Height}: {result} in {clock.ElapsedMilliseconds} ms");
        island.Save(args[4]);
        return 0;
    }

    // ---- round trip ---------------------------------------------------------------------------------------------------------------------------

    private static int RoundTrip(IEnumerable<string> paths)
    {
        var failures = 0;
        var temp = Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath(), "heightmap_rt_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(temp);
        try
        {
            foreach (var path in Islands(paths))
            {
                var original = File.ReadAllBytes(path);
                var name = Path.GetFileName(path);
                foreach (var format in new[] { IslandHeightmap.ExportFormat.Png16, IslandHeightmap.ExportFormat.Raw16, IslandHeightmap.ExportFormat.Csv })
                {
                    var island = IslandFile.Parse(original, path);
                    var l = HeightmapLayout.Of(island);
                    var file = Path.Combine(temp, name + "." + format + (format == IslandHeightmap.ExportFormat.Csv ? ".csv" : format == IslandHeightmap.ExportFormat.Raw16 ? ".r16" : ".png"));
                    var clock = Stopwatch.StartNew();
                    var range = IslandHeightmap.Export(island, file, format);
                    var exportMs = clock.ElapsedMilliseconds;
                    var picture = HeightmapFile.Load(file, (l.Width, l.Height));
                    var (black, white) = picture.Range ?? range;      // (a raw file doesn't say: the export's range, as the dialog's default would be)
                    var before = IslandHeightmap.Heights(island);
                    var result = IslandHeightmap.Apply(island, picture, new HeightmapImportOptions
                    {
                        Black = black, White = white, Fit = HeightmapFit.Exact, Textures = HeightmapTextures.Keep, FixDiagonals = false, ObjectsFollow = false,
                    });
                    var after = IslandHeightmap.Heights(island);
                    var differ = before.Zip(after).Count(p => !(double.IsNaN(p.First) && double.IsNaN(p.Second)) && p.First != p.Second);
                    var bytes = island.ToBytes();
                    var same = bytes.AsSpan().SequenceEqual(original);
                    var ok = differ == 0 && result.Changed == 0 && same && picture.Width == l.Width && picture.Height == l.Height;
                    Console.WriteLine($"  {(ok ? "ok    " : "FAILED")} {name,-13} {format,-6} {l.Width}x{l.Height} {new FileInfo(file).Length,9:N0} bytes (export {exportMs} ms), range {black}..{white}: "
                        + $"{differ} heights differ, {result.Changed} changed, file {(same ? "byte-identical" : "DIFFERENT")}");
                    if (!ok) failures++;
                }
            }
        }
        finally { try { Directory.Delete(temp, true); } catch (IOException) { } }
        Console.WriteLine(failures == 0 ? "heightmap round trip: passed" : $"heightmap round trip: {failures} FAILED");
        return failures == 0 ? 0 : 1;
    }

    // ---- synthetic pictures ---------------------------------------------------------------------------------------------------------------------

    private static HeightmapPicture Picture(int w, int h, Func<int, int, double> value, bool absolute = false) =>
        new() { Width = w, Height = h, Values = Enumerable.Range(0, w * h).Select(i => value(i % w, i / w)).ToArray(), Absolute = absolute, Kind = "synthetic" };

    // The engine test's picture: an island with a beach, a round hill and a crater, black -400 and white 3600.
    public const int EngineBlack = -400, EngineWhite = 3600;
    public static double HillCrater(int x, int y, int w, int h)
    {
        double Smooth(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
        var edge = Math.Min(Math.Min(x, y), Math.Min(w - 1 - x, h - 1 - y));
        var height = -300 + 900 * Smooth((edge - 3) / 10.0);                                          // sea floor -300 .. land 600
        // (Twinsen starts at pixel 32, 32 -- the first cube's middle -- facing -X: the hill lies straight ahead of him, the crater ahead to one side)
        var hill = Math.Sqrt((x - 14.0) * (x - 14.0) + (y - 30.0) * (y - 30.0));
        height += 1800 * Smooth(1 - hill / 13.0);                                                      // a round hill, 1800 higher
        var crater = Math.Sqrt((x - 24.0) * (x - 24.0) + (y - 54.0) * (y - 54.0));
        height += 700 * Smooth(1 - Math.Abs(crater - 10) / 4.0) - 500 * Smooth(1 - crater / 8.0);      // a crater: a rim round a bowl
        return Math.Clamp((height - EngineBlack) / (EngineWhite - EngineBlack), 0, 1);
    }

    private static int Make(string[] args)
    {
        var w = int.Parse(args[4]); var h = int.Parse(args[5]);
        Func<int, int, double> f = args[3] switch
        {
            "gradient" => (x, y) => x / (double)(w - 1),
            "cone" => (x, y) => Math.Max(0, 1 - Math.Sqrt((x - (w - 1) / 2.0) * (x - (w - 1) / 2.0) + (y - (h - 1) / 2.0) * (y - (h - 1) / 2.0)) / ((w - 1) / 2.0)),
            "crater" => (x, y) => { var d = Math.Sqrt((x - (w - 1) / 2.0) * (x - (w - 1) / 2.0) + (y - (h - 1) / 2.0) * (y - (h - 1) / 2.0)) / ((w - 1) / 2.0); return Math.Clamp(0.4 + 0.5 * Math.Exp(-(d - 0.45) * (d - 0.45) / 0.02) - 0.3 * Math.Exp(-d * d / 0.05), 0, 1); },
            _ => (x, y) => HillCrater(x, y, w, h),
        };
        var picture = Picture(w, h, f);
        var text = new Dictionary<string, string> { [HeightmapFile.TextKey] = $"black={EngineBlack} white={EngineWhite}" };
        File.WriteAllBytes(args[2], HeightmapFile.EncodePng(w, h, picture.Values, 16, args[3] == "hillcrater" ? text : null));
        Console.WriteLine($"{args[2]}: {w} x {h} {args[3]}");
        return 0;
    }

    // heightmap stamp <in.png> <out.png> <x> <y> <radius> <levels>: a round bump (or, negative, a dip) painted into a 16-bit heightmap, as a
    // paint program would -- its range text kept, so Import reads the same black and white
    private static int Stamp(string[] args)
    {
        var picture = HeightmapFile.Load(args[2]);
        double cx = double.Parse(args[4], CultureInfo.InvariantCulture), cy = double.Parse(args[5], CultureInfo.InvariantCulture), r = double.Parse(args[6], CultureInfo.InvariantCulture), amount = double.Parse(args[7], CultureInfo.InvariantCulture);
        var values = picture.Values.ToArray();
        for (var y = 0; y < picture.Height; y++)
        for (var x = 0; x < picture.Width; x++)
        {
            var d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
            if (d >= 1 || double.IsNaN(values[y * picture.Width + x])) continue;
            var t = 1 - d; t = t * t * (3 - 2 * t);
            values[y * picture.Width + x] = Math.Clamp(values[y * picture.Width + x] + amount * t, 0, 1);
        }
        var text = picture.Range is { } range ? new Dictionary<string, string> { [HeightmapFile.TextKey] = $"black={range.Black} white={range.White}" } : null;
        File.WriteAllBytes(args[3], HeightmapFile.EncodePng(picture.Width, picture.Height, values, picture.Bits == 8 ? 8 : 16, text));
        Console.WriteLine($"{args[3]}: {picture.Width} x {picture.Height}, a bump of {amount} at ({cx}, {cy}) radius {r}");
        return 0;
    }

    // heightmap decodecheck <png> <8|16> [alpha]: a picture another encoder wrote (E:\dump\TEMP\heightmap\pngtest.ps1, WPF's PNG encoder:
    // interlaced, palette, grey, colour) read back pixel for pixel against the pattern it was given -- level (x * 7 + y * 13) % 256 (8-bit) or
    // (x * 997 + y * 331) % 65536 (16-bit); with `alpha`, the pixels with x % 5 == 0 transparent
    private static int DecodeCheck(string[] args)
    {
        var picture = HeightmapFile.Load(args[2]);
        var bits = int.Parse(args[3]);
        var alpha = args.Length > 4 && args[4] == "alpha";
        var wrong = 0;
        for (var y = 0; y < picture.Height; y++)
        for (var x = 0; x < picture.Width; x++)
        {
            var expected = alpha && x % 5 == 0 ? double.NaN : bits == 16 ? (x * 997 + y * 331) % 65536 / 65535.0 : (x * 7 + y * 13) % 256 / 255.0;
            var got = picture.At(x, y);
            if (double.IsNaN(expected) != double.IsNaN(got) || !double.IsNaN(expected) && Math.Abs(expected - got) > 1e-9) { if (wrong++ < 3) Console.WriteLine($"    ({x},{y}) {got} expected {expected}"); }
        }
        Console.WriteLine($"  {(wrong == 0 ? "ok    " : "FAILED")} {Path.GetFileName(args[2])}: {picture.Width} x {picture.Height}, {picture.Kind}: {wrong} pixels wrong");
        return wrong == 0 ? 0 : 1;
    }

    // A 4-bit palette PNG built by hand: 16 greys (entry n = n * 17), entry 15 transparent (tRNS), two pixels a byte, no row filter.
    private static byte[] PalettePng(int width, int height, Func<int, int, int> entry)
    {
        var crcTable = Enumerable.Range(0, 256).Select(n => { var c = (uint)n; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; return c; }).ToArray();
        var file = new MemoryStream();
        file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        void Chunk(string type, byte[] data)
        {
            var b = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(b, data.Length); file.Write(b);
            var t = System.Text.Encoding.ASCII.GetBytes(type); file.Write(t); file.Write(data);
            var crc = 0xFFFFFFFFu;
            foreach (var x in t.Concat(data)) crc = crcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(b, crc ^ 0xFFFFFFFFu); file.Write(b);
        }
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, width); System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 4; header[9] = 3;
        Chunk("IHDR", header);
        Chunk("PLTE", Enumerable.Range(0, 16).SelectMany(n => new[] { (byte)(n * 17), (byte)(n * 17), (byte)(n * 17) }).ToArray());
        Chunk("tRNS", Enumerable.Range(0, 16).Select(n => (byte)(n == 15 ? 0 : 255)).ToArray());
        var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < width; x += 2) raw.WriteByte((byte)((entry(x, y) << 4) | (x + 1 < width ? entry(x + 1, y) : 0)));
        }
        var z = new MemoryStream();
        using (var deflate = new System.IO.Compression.ZLibStream(z, System.IO.Compression.CompressionLevel.Optimal, true)) deflate.Write(raw.ToArray());
        Chunk("IDAT", z.ToArray());
        Chunk("IEND", Array.Empty<byte>());
        return file.ToArray();
    }

    private static int failed;
    private static void Check(string what, bool ok) { Console.WriteLine($"  {(ok ? "ok    " : "FAILED")} {what}"); if (!ok) failed++; }

    private static int SelfTest(string sourcePath)
    {
        failed = 0;
        var source = IslandFile.Load(sourcePath);
        IslandFile Make(params (int X, int Z)[] cubes)
        {
            var island = IslandFile.Parse(IslandScaler.NewFile(source, cubes.ToList()), "TEST.ILE");
            return island;
        }

        // sizes
        var one = Make((7, 7));
        var four = Make((6, 6), (7, 6), (6, 7), (7, 7));
        var ell = Make((6, 6), (7, 6), (6, 7));
        var wide = Make((3, 5), (4, 5), (5, 5));
        Check($"one cube: {HeightmapLayout.Of(one).Width} x {HeightmapLayout.Of(one).Height} = 65 x 65", HeightmapLayout.Of(one) is { Width: 65, Height: 65, Gx0: 448, Gz0: 448 });
        Check($"2 x 2 cubes: 129 x 129", HeightmapLayout.Of(four) is { Width: 129, Height: 129 });
        Check($"3 x 1 cubes: 193 x 65", HeightmapLayout.Of(wide) is { Width: 193, Height: 65 });
        Check("an L of three cubes: its 2 x 2 box, 129 x 129, one square left out", HeightmapLayout.Of(ell) is { Width: 129, Height: 129 } && IslandHeightmap.SizeText(ell).Contains("1 of the 4"));
        Console.WriteLine("    " + IslandHeightmap.SizeText(four, "TEST.ILE"));
        Console.WriteLine("    " + IslandHeightmap.LayoutText(four));
        Check("SizeFor / CubesFor agree (65 -> 1, 129 -> 2, 128 -> 2, 257 -> 4, 385 -> 6)",
            HeightmapLayout.SizeFor(4, 3) == (257, 193) && HeightmapLayout.CubesFor(65) == 1 && HeightmapLayout.CubesFor(129) == 2 && HeightmapLayout.CubesFor(128) == 2 && HeightmapLayout.CubesFor(257) == 4 && HeightmapLayout.CubesFor(385) == 6);

        HeightmapImportOptions Plain(double black, double white, HeightmapFit fit = HeightmapFit.Exact) =>
            new() { Black = black, White = white, Fit = fit, Textures = HeightmapTextures.Keep, FixDiagonals = false, ObjectsFollow = false };

        // a horizontal gradient through a 16-bit PNG: 6400 over 64 cells = 100 a cell, so x * 100 exactly
        {
            var island = Make((7, 7));
            var gradient = Picture(65, 65, (x, y) => x / 64.0);
            var png = HeightmapFile.DecodePng(HeightmapFile.EncodePng(65, 65, gradient.Values, 16));
            IslandHeightmap.Apply(island, png, Plain(0, 6400));
            var l = HeightmapLayout.Of(island);
            int H(int i, int j) => island.HeightAt(l.Gx0 + i, l.Gz0 + j)!.Value;
            Check($"gradient corners: top-left {H(0, 0)} (0), top-right {H(64, 0)} (6400), bottom-left {H(0, 64)} (0), bottom-right {H(64, 64)} (6400)", H(0, 0) == 0 && H(64, 0) == 6400 && H(0, 64) == 0 && H(64, 64) == 6400);
            Check($"gradient: every point x * 100 (pixel x = world X): {H(17, 30)} at x 17", Enumerable.Range(0, 65).All(i => Enumerable.Range(0, 65).All(j => H(i, j) == i * 100)));
            Check("the picture's top-left pixel is the cube's (0, 0) corner, world X/Z = cube * 32768", island.CubeAt(7, 7)!.Height(64, 0) == 6400 && island.CubeAt(7, 7)!.Height(0, 64) == 0);
        }
        // a cone on a 2 x 2 island (shared cube edges get one value), with a negative black
        {
            var island = Make((6, 6), (7, 6), (6, 7), (7, 7));
            var cone = Picture(129, 129, (x, y) => Math.Max(0, 1 - Math.Sqrt((x - 64.0) * (x - 64.0) + (y - 64.0) * (y - 64.0)) / 64.0));
            IslandHeightmap.Apply(island, HeightmapFile.DecodePng(HeightmapFile.EncodePng(129, 129, cone.Values, 16)), Plain(-500, 3500));
            var l = HeightmapLayout.Of(island);
            int H(int i, int j) => island.HeightAt(l.Gx0 + i, l.Gz0 + j)!.Value;
            Check($"cone: peak {H(64, 64)} (3500) in the middle, where the four cubes meet; corners {H(0, 0)}, {H(128, 128)} (-500)", H(64, 64) == 3500 && H(0, 0) == -500 && H(128, 128) == -500 && H(128, 0) == -500);
            Check("cone: the four cubes' copies of the middle point agree", island.Owners(l.Gx0 + 64, l.Gz0 + 64).All(o => o.Cube.Height(o.X, o.Z) == 3500) && island.Owners(l.Gx0 + 64, l.Gz0 + 64).Count() == 4);
            Check($"cone: half way out {H(96, 64)} (1500)", Math.Abs(H(96, 64) - 1500) <= 1);
            Check("cone: no seams (the validator finds none)", !IslandValidator.Validate(island).Any(p => p.Message.Contains("cube borders")));
        }
        // 8-bit: within half a level
        {
            var island = Make((7, 7));
            var gradient = Picture(65, 65, (x, y) => y / 64.0);
            var png = HeightmapFile.DecodePng(HeightmapFile.EncodePng(65, 65, gradient.Values, 8));
            IslandHeightmap.Apply(island, png, Plain(0, 5100));
            var l = HeightmapLayout.Of(island);
            var worst = Enumerable.Range(0, 65).Max(j => Math.Abs(island.HeightAt(l.Gx0 + 3, l.Gz0 + j)!.Value - j * 5100 / 64.0));
            Check($"8-bit gradient (down = +Z): within {worst:F1} units (a level is 20)", png.Bits == 8 && worst <= 10.01);
        }
        // resample: a 33 x 33 gradient onto 65 x 65 (corner to corner) and a big 1000 x 1000 one shrunk
        {
            var island = Make((7, 7));
            IslandHeightmap.Apply(island, Picture(33, 33, (x, y) => x / 32.0), Plain(0, 6400, HeightmapFit.Resample));
            var l = HeightmapLayout.Of(island);
            Check($"resample 33 -> 65: corners {island.HeightAt(l.Gx0, l.Gz0)} and {island.HeightAt(l.Gx0 + 64, l.Gz0 + 64)}, middle of a stretched pixel {island.HeightAt(l.Gx0 + 1, l.Gz0)} (100)",
                island.HeightAt(l.Gx0, l.Gz0) == 0 && island.HeightAt(l.Gx0 + 64, l.Gz0 + 64) == 6400 && island.HeightAt(l.Gx0 + 1, l.Gz0) == 100);
            IslandHeightmap.Apply(island, Picture(1000, 1000, (x, y) => x / 999.0), Plain(0, 6400, HeightmapFit.Resample));
            var worst = Enumerable.Range(0, 65).Max(i => Math.Abs(island.HeightAt(l.Gx0 + i, l.Gz0 + 20)!.Value - i * 100));
            Check($"resample 1000 -> 65 (averaged): within {worst} of x * 100", worst <= 4);
        }
        // crop: a 40 x 30 picture from the top-left; the rest left alone
        {
            var island = Make((7, 7));
            IslandHeightmap.Apply(island, Picture(65, 65, (_, _) => 0.5), Plain(0, 2000));
            IslandHeightmap.Apply(island, Picture(40, 30, (_, _) => 1), Plain(0, 2000, HeightmapFit.Crop));
            var l = HeightmapLayout.Of(island);
            int H(int i, int j) => island.HeightAt(l.Gx0 + i, l.Gz0 + j)!.Value;
            Check($"crop 40 x 30: inside {H(39, 29)} (2000), beside {H(40, 29)} / below {H(39, 30)} (1000, left alone)", H(39, 29) == 2000 && H(0, 0) == 2000 && H(40, 29) == 1000 && H(39, 30) == 1000);
            var refused = false;
            try { IslandHeightmap.Apply(island, Picture(64, 64, (_, _) => 0), Plain(0, 1)); } catch (InvalidDataException) { refused = true; }
            Check("exact: a 64 x 64 picture for a 65 x 65 island is refused", refused);
        }
        // transparency: transparent pixels keep the ground; an L-shaped island's missing cube is skipped
        {
            var island = Make((6, 6), (7, 6), (6, 7));
            IslandHeightmap.Apply(island, Picture(129, 129, (_, _) => 0.25), Plain(0, 4000));
            var holes = Picture(129, 129, (x, y) => x < 10 ? double.NaN : 0.75);
            var png = HeightmapFile.DecodePng(HeightmapFile.EncodePng(129, 129, holes.Values, 16));
            var result = IslandHeightmap.Apply(island, png, Plain(0, 4000));
            var l = HeightmapLayout.Of(island);
            Check($"transparency survives the PNG (grey + alpha): {png.Kind}", png.HasTransparency && double.IsNaN(png.At(5, 5)) && !double.IsNaN(png.At(50, 5)));
            Check($"transparent pixels keep the ground ({island.HeightAt(l.Gx0 + 5, l.Gz0 + 5)} = 1000), the others take the picture ({island.HeightAt(l.Gx0 + 50, l.Gz0 + 5)} = 3000)",
                island.HeightAt(l.Gx0 + 5, l.Gz0 + 5) == 1000 && island.HeightAt(l.Gx0 + 50, l.Gz0 + 5) == 3000);
            Check($"the missing cube's square is skipped: {result.Vertices} points set (3 cubes less the transparent strip)", island.HeightAt(l.Gx0 + 100, l.Gz0 + 100) is null && result.Vertices < 3 * 65 * 65);
            var exported = Path.GetTempFileName();
            IslandHeightmap.Export(island, exported, IslandHeightmap.ExportFormat.Png16);
            var back = HeightmapFile.Load(exported);
            File.Delete(exported);
            Check($"an L-shaped island exports with its missing square transparent ({back.Kind})", back.HasTransparency && double.IsNaN(back.At(100, 100)) && !double.IsNaN(back.At(10, 10)) && back.Range is not null);
        }
        // CSV (world heights) and raw 16-bit
        {
            var island = Make((7, 7));
            var csv = HeightmapFile.DecodeCsv(string.Join("\n", Enumerable.Range(0, 65).Select(j => string.Join(",", Enumerable.Range(0, 65).Select(i => (i * 7 - j * 3).ToString())))));
            IslandHeightmap.Apply(island, csv, Plain(0, 1));
            var l = HeightmapLayout.Of(island);
            Check($"CSV: heights as written ({island.HeightAt(l.Gx0 + 10, l.Gz0 + 4)} = 58), black / white ignored", csv.Absolute && island.HeightAt(l.Gx0 + 10, l.Gz0 + 4) == 58 && island.HeightAt(l.Gx0 + 64, l.Gz0 + 64) == 64 * 4);
            var raw = HeightmapFile.DecodeRaw(HeightmapFile.EncodeRaw(Picture(65, 65, (x, y) => (x + y) / 128.0).Values), (65, 65));
            IslandHeightmap.Apply(island, raw, Plain(0, 12800));
            Check($"raw 16-bit: ({island.HeightAt(l.Gx0 + 64, l.Gz0 + 64)} = 12800, {island.HeightAt(l.Gx0 + 30, l.Gz0 + 2)} = 3200)", island.HeightAt(l.Gx0 + 64, l.Gz0 + 64) == 12800 && island.HeightAt(l.Gx0 + 30, l.Gz0 + 2) == 3200);
            var square = HeightmapFile.DecodeRaw(new byte[129 * 129 * 2], null);
            Check("raw 16-bit with no size given: a square (129 x 129)", square.Width == 129 && square.Height == 129 && square.Bits == 16);
            Check("out of range heights are clamped and counted", IslandHeightmap.Apply(island, Picture(65, 65, (x, _) => x == 0 ? 1 : 0), Plain(0, 99999)).Clamped == 65 && island.HeightAt(l.Gx0, l.Gz0 + 5) == short.MaxValue);
        }
        // colour pictures are read as their brightness; the PNG range text is read back
        {
            var rgba = Path.GetTempFileName();
            var bgra = new byte[65 * 65 * 4];
            for (var i = 0; i < 65 * 65; i++) { var v = (byte)(i % 65 * 255 / 64); bgra[i * 4] = v; bgra[i * 4 + 1] = v; bgra[i * 4 + 2] = v; bgra[i * 4 + 3] = 255; }
            PngWriter.Write(rgba, bgra, 65, 65);
            var picture = HeightmapFile.Load(rgba);
            File.Delete(rgba);
            Check($"an 8-bit RGBA picture of grey reads as its grey ({picture.Kind}: {picture.At(64, 3):F3} at the right edge)", picture.At(64, 3) == 1 && picture.At(0, 3) == 0 && Math.Abs(picture.At(32, 0) - 127 / 255.0) < 1e-9);
            var text = HeightmapFile.DecodePng(HeightmapFile.EncodePng(2, 2, new double[] { 0, 1, 0.5, 0.25 }, 16, new Dictionary<string, string> { [HeightmapFile.TextKey] = "black=-120 white=3400 island=X.ILE" }));
            Check($"the PNG's own range is read back ({text.Range})", text.Range == (-120, 3400));
            // a 4-bit palette picture (two pixels a byte) with a transparent entry, built by hand
            static int Entry(int x, int y) => (x + 5 * y) % 16;
            var palette = HeightmapFile.DecodePng(PalettePng(9, 3, Entry));
            Check($"a 4-bit palette PNG with tRNS (entry n = grey n x 17, entry 15 transparent): {palette.Kind}; every pixel right",
                palette.Width == 9 && palette.Height == 3 && double.IsNaN(palette.At(5, 2))
                && Enumerable.Range(0, 27).All(i => Entry(i % 9, i / 9) == 15 ? double.IsNaN(palette.At(i % 9, i / 9)) : Math.Abs(palette.At(i % 9, i / 9) - Entry(i % 9, i / 9) * 17 / 255.0) < 1e-9));
        }
        // sea and land: a cone whose foot is under the sea level gets the sea texture there and land above; steep triangles blocked; light baked
        {
            var island = Make((7, 7));
            var desert = IslandGround.CommonGround(source);
            var cone = Picture(65, 65, (x, y) => Math.Max(0, 1 - Math.Sqrt((x - 32.0) * (x - 32.0) + (y - 32.0) * (y - 32.0)) / 32.0));
            var lightBefore = island.CubeAt(7, 7)!.Intensity.ToArray();
            var result = IslandHeightmap.Apply(island, cone, new HeightmapImportOptions
            {
                Black = -1000, White = 20000, SeaLevel = 0, Fit = HeightmapFit.Exact, Textures = HeightmapTextures.AllBySlope, Ground = desert,
                SteepAngle = 45, Bake = BakeOptions.For(source.Cubes.Values.First()), ObjectsFollow = false,
            });
            var cube = island.CubeAt(7, 7)!;
            var corner = new IslandPolygon(cube.Polygon(0, 0, 0));
            var top = new IslandPolygon(cube.Polygon(31, 31, 0));
            Check($"sea: the corner cell (under the sea level) has the island's sea ground (code {corner.CodeJeu}, poly {corner.PolyFlag}, tex {corner.TexFlag})",
                desert.Sea is { } sea && corner.CodeJeu == sea.Polygon.CodeJeu && corner.PolyFlag == sea.Polygon.PolyFlag && corner.TexFlag == sea.Polygon.TexFlag);
            Check($"land: the peak cell has land ground (code {top.CodeJeu}, poly {top.PolyFlag}, tex {top.TexFlag}); {result.SeaCells} sea cells, {result.LandCells} land",
                (top.PolyFlag + top.TexFlag) > 0 && result.SeaCells > 0 && result.LandCells > 0 && result.Retextured == 64 * 64);
            Console.WriteLine($"    cone slope {IslandHeightmap.Slope(island, 40 + 7 * 64, 32 + 7 * 64):F1} degrees; {result.Blocked} triangles blocked over 45");
            Check("steep: the cone's 52-degree flanks are blocked (Col), the flat sea isn't", result.Blocked > 0 && !new IslandPolygon(cube.Polygon(0, 0, 0)).Col && new IslandPolygon(cube.Polygon(32, 45, 0)).Col);
            Check($"light: baked again ({result.Relit} points changed)", result.Relit > 0 && !cube.Intensity.AsSpan().SequenceEqual(lightBefore));
            Check("the island still saves and reads back the same", IslandFile.Parse(island.ToBytes()).Cubes[cube.Id].Polygons.AsSpan().SequenceEqual(cube.Polygons));
        }
        // sea and shore only: cells that stay land keep their pictures
        {
            var island = Make((7, 7));
            var desert = IslandGround.CommonGround(source);
            var land = Picture(65, 65, (_, _) => 0.5);
            IslandHeightmap.Apply(island, land, new HeightmapImportOptions { Black = -1000, White = 3000, Fit = HeightmapFit.Exact, Textures = HeightmapTextures.AllBySlope, Ground = desert, ObjectsFollow = false });
            var cube = island.CubeAt(7, 7)!;
            cube.SetPolygon(10, 10, 0, new IslandPolygon(cube.Polygon(10, 10, 0)).With(bank: 9).Raw);      // a hand-painted mark
            var lake = Picture(65, 65, (x, y) => x > 40 ? 0.1 : 0.6);
            var result = IslandHeightmap.Apply(island, lake, new HeightmapImportOptions { Black = -1000, White = 3000, Fit = HeightmapFit.Exact, Textures = HeightmapTextures.SeaAndShore, Ground = desert, ObjectsFollow = false });
            Check($"sea and shore: only the cells that went under the sea are retextured ({result.Retextured}), the hand-painted mark stays",
                new IslandPolygon(cube.Polygon(10, 10, 0)).Bank == 9 && result.Retextured > 0 && result.Retextured <= 64 * 25);
        }
        // a game island's own heightmap, exported and imported with every option, changes nothing; an edit in one corner touches only that corner
        {
            var original = File.ReadAllBytes(sourcePath);
            var island = IslandFile.Parse(original, sourcePath);
            var temp = Path.GetTempFileName();
            var (black, white) = IslandHeightmap.Export(island, temp, IslandHeightmap.ExportFormat.Png16);
            var picture = HeightmapFile.Load(temp);
            File.Delete(temp);
            HeightmapImportOptions All() => new()
            {
                Black = black, White = white, Fit = HeightmapFit.Exact, Textures = HeightmapTextures.SeaAndShore, FixDiagonals = true, SteepAngle = 45,
                Bake = BakeOptions.For(island.Cubes.Values.First()), ObjectShadowDepth = 5, ObjectsFollow = true, SmoothPasses = 1,
            };
            var result = IslandHeightmap.Apply(island, picture, All());
            Check($"{Path.GetFileName(sourcePath)} exported and imported with every option: {result.Changed} heights changed, the file byte for byte the same",
                result.Changed == 0 && island.ToBytes().AsSpan().SequenceEqual(original));
            var l = HeightmapLayout.Of(island);
            // a 20 x 20 pixel square raised by a tenth of the range, in the first cube that has land
            var (cubeX, cubeZ, cube) = IslandOps.CubeCells(island).First(c => c.Cube.Heights.Any(h => h > 200));
            int px = (cubeX - l.CubeX0) * 64 + 20, pz = (cubeZ - l.CubeZ0) * 64 + 20;
            var edited = new HeightmapPicture { Width = picture.Width, Height = picture.Height, Values = picture.Values.ToArray(), Range = picture.Range };
            for (var y = pz; y < pz + 20; y++) for (var x = px; x < px + 20; x++) edited.Values[y * edited.Width + x] = Math.Min(1, edited.Values[y * edited.Width + x] + 0.1);
            var before = IslandFile.Parse(original, sourcePath);
            result = IslandHeightmap.Apply(island, edited, All());
            var otherCubesSame = island.Cubes.Where(kv => kv.Value != cube).All(kv => kv.Value.Polygons.AsSpan().SequenceEqual(before.Cubes[kv.Key].Polygons)
                && kv.Value.Intensity.AsSpan().SequenceEqual(before.Cubes[kv.Key].Intensity) && kv.Value.Heights.AsSpan().SequenceEqual(before.Cubes[kv.Key].Heights));
            var c0 = before.Cubes[cube.Id];
            var farLight = Enumerable.Range(0, 65 * 65).Where(i => i % 65 > 50 || i / 65 > 50).All(i => cube.Intensity[i] == c0.Intensity[i]);
            var farCells = Enumerable.Range(0, 64 * 64).Where(i => i % 64 > 50 || i / 64 > 50).All(i => cube.Polygons[i * 2] == c0.Polygons[i * 2] && cube.Polygons[i * 2 + 1] == c0.Polygons[i * 2 + 1]);
            Check($"a 20 x 20 square edited in cube ({cubeX},{cubeZ}): {result.Changed} heights changed, the other cubes and the far side of that one untouched (light, cells)",
                result.Changed > 300 && result.Changed <= 400 && otherCubesSame && farLight && farCells);
        }
        // undo: the editor's history takes an import back in one step; a preview put back leaves the island as it was
        {
            var island = Make((7, 7));
            var history = new IslandHistory(island);
            var original = island.ToBytes();
            history.Begin();
            IslandHeightmap.Apply(island, Picture(65, 65, (x, _) => x / 64.0), Plain(0, 3000));
            history.RestorePending();
            Check("a preview put back (RestorePending) leaves the island byte for byte as it was", island.ToBytes().AsSpan().SequenceEqual(original) && history.Pending);
            IslandHeightmap.Apply(island, Picture(65, 65, (x, _) => x / 64.0), Plain(0, 3000));
            Check("committed as one step", history.Commit("import heightmap") && history.UndoLabel == "import heightmap");
            var applied = island.ToBytes();
            history.Undo();
            Check("undo puts every height back", island.ToBytes().AsSpan().SequenceEqual(original));
            history.Redo();
            Check("redo puts the import back", island.ToBytes().AsSpan().SequenceEqual(applied));
        }
        // the preview pictures
        {
            var island = Make((6, 6), (7, 6), (6, 7));
            var picture = Picture(129, 129, (x, y) => x / 128.0);
            var options = Plain(0, 4000);
            var targets = IslandHeightmap.Targets(island, picture, options, out _);
            var shaded = IslandHeightmap.ShadedPreview(island, targets, 0, out var stats);
            var grey = IslandHeightmap.PicturePreview(island, picture, options);
            Check($"previews: {shaded.Length / 4} and {grey.Length / 4} pixels (129 x 129), heights {stats.Lo}..{stats.Hi}, {stats.SeaPercent}% sea", shaded.Length == 129 * 129 * 4 && grey.Length == 129 * 129 * 4 && stats.Hi == 4000);
        }
        Console.WriteLine(failed == 0 ? "heightmap self-test: passed" : $"heightmap self-test: {failed} FAILED");
        return failed == 0 ? 0 : 1;
    }

    // ---- the engine: a heightmap island played headless ---------------------------------------------------------------------------------------

    private static int Engine(string[] args)
    {
        failed = 0;
        var game = args[2];
        var outDir = args.Length > 3 ? args[3] : Path.Combine(game, "..", "engine_out");
        Directory.CreateDirectory(outDir);
        var engine = Lba2Engine.Find();
        if (engine is null) { Console.WriteLine("no lba2cc.exe found"); return 1; }
        if (game.StartsWith(@"E:\GOG", StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(game, "SCENE.HQR"))) { Console.WriteLine("give a sandbox game folder"); return 1; }

        // the map: a 2 x 2 island from DESERT's look, its ground from the hill-and-crater picture
        var picture = Picture(129, 129, (x, y) => HillCrater(x, y, 129, 129));
        File.WriteAllBytes(Path.Combine(outDir, "hillcrater.png"), HeightmapFile.EncodePng(129, 129, picture.Values, 16));
        var map = MapNew.Lba2Island(game, "Heightmap test", "HMTEST", "DESERT", 2, 2, 1200, picture,
            new HeightmapImportOptions { Black = EngineBlack, White = EngineWhite, SeaLevel = 0, SteepAngle = 45 });
        var island = IslandFile.Parse(map.Ile!, "HMTEST.ILE");
        var l = HeightmapLayout.Of(island);
        int H(int i, int j) => island.HeightAt(l.Gx0 + i, l.Gz0 + j)!.Value;
        Check($"the map island is {l.CubesX} x {l.CubesZ} cubes and takes the picture's heights (hill top {H(14, 30)}, crater floor {H(24, 54)}, rim {H(24, 44)}, sea {H(0, 0)})",
            Math.Abs(H(14, 30) - (int)Math.Round(EngineBlack + HillCrater(14, 30, 129, 129) * (EngineWhite - EngineBlack))) <= 1 && H(0, 0) < 0);
        var mapFile = Path.Combine(outDir, "hmtest.lbamap");
        map.Save(mapFile);
        var (installed, scenes) = MapInstall.InstallInMapsFolder(map, game, "hmtest.lbamap");
        Console.WriteLine($"  installed {installed}: scenes {string.Join(", ", scenes)}");

        // Twinsen starts in the first cube's middle (pixel 32, 32, flat land) facing the hill, and walks straight up it
        var user = Path.Combine(outDir, "user");
        if (Directory.Exists(user)) Directory.Delete(user, true);
        Directory.CreateDirectory(user);
        var shot0 = Path.Combine(outDir, "engine_start.png"); var shotMid = Path.Combine(outDir, "engine_climbing.png"); var shot1 = Path.Combine(outDir, "engine_walked.png");
        foreach (var f in new[] { shot0, shotMid, shot1 }) if (File.Exists(f)) File.Delete(f);
        var start = new ProcessStartInfo(engine) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new Lba2PlayOptions { Scene = scenes[0], Sound = false, Width = 640, Height = 480 }.Arguments(game, user)) start.ArgumentList.Add(a);
        foreach (var a in new[]
        {
            "--headless", "--fixed-dt", "20", "--exec-at", "60", $"objtrace 0;screenshot {shot0}",
            "--exec-at", "70", "input up 430",
            "--exec-at", "330", $"screenshot {shotMid}",
            "--exec-at", "540", $"status;screenshot {shot1}", "--tick", "560", "--exit",
        }) start.ArgumentList.Add(a);
        var clock = Stopwatch.StartNew();
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit(240000);
        var output = stdout.Result + "\n" + stderr.Result;
        foreach (var log in Directory.GetFiles(user, "*.log", SearchOption.AllDirectories)) output += "\n" + File.ReadAllText(log);
        File.WriteAllText(Path.Combine(outDir, "engine_output.txt"), output);
        Console.WriteLine($"  engine ran {clock.Elapsed.TotalSeconds:F0} s, exit {process.ExitCode}");

        // where Twinsen was, each frame, against the ground under him
        var cube = map.Scenes[0];
        var track = new List<(int X, int Y, int Z)>();
        var seen = new HashSet<string>();         // (the engine's output and its log file both carry the lines: each clock value once)
        foreach (var line in output.Split('\n'))
        {
            var at = line.IndexOf("[obj] ", StringComparison.Ordinal);
            if (at < 0 || !line.Contains(" obj=0 ")) continue;
            var pos = line.IndexOf(" pos=", at, StringComparison.Ordinal);
            if (pos < 0 || !seen.Add(line[at..pos])) continue;
            var parts = line[(pos + 5)..].Split(' ')[0].Split(',');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y) || !int.TryParse(parts[2], out var z)) continue;
            // (a jump across the cube: he walked into the next cube's scene, whose coordinates are its own -- the trace stops there)
            if (track.Count > 0 && Math.Abs(x - track[^1].X) + Math.Abs(z - track[^1].Z) > 8000) { Console.WriteLine("  (he walked into the next cube: traced up to there)"); break; }
            track.Add((x, y, z));
        }
        Check($"Twinsen's position was traced ({track.Count} frames)", track.Count > 100);
        if (track.Count > 0)
        {
            double worstBelow = 0, worstAbove = 0; var onGround = 0; double climb = 0; var lowest = int.MaxValue; var highest = int.MinValue;
            foreach (var (x, y, z) in track)
            {
                if (IslandOps.Altitude(island, cube.CubeX * (double)IslandFile.CubeSize + x, cube.CubeZ * (double)IslandFile.CubeSize + z) is not { } ground) continue;
                var d = y - ground;
                worstBelow = Math.Min(worstBelow, d); worstAbove = Math.Max(worstAbove, d);
                if (Math.Abs(d) <= 64) onGround++;
                lowest = Math.Min(lowest, y); highest = Math.Max(highest, y);
            }
            for (var i = 1; i < track.Count; i++) climb += Math.Abs(track[i].Y - track[i - 1].Y);
            var walked = Math.Sqrt(Math.Pow(track[^1].X - track[0].X, 2) + Math.Pow(track[^1].Z - track[0].Z, 2));
            Console.WriteLine($"  Twinsen from ({track[0].X},{track[0].Y},{track[0].Z}) to ({track[^1].X},{track[^1].Y},{track[^1].Z}): {walked:F0} units away, heights {lowest}..{highest} ({climb:F0} up and down), "
                + $"{onGround}/{track.Count} frames within 64 of the ground, worst {worstBelow:F0} below / {worstAbove:F0} above");
            Check("he walked (over 2000 units)", walked > 2000);
            Check("he went up and down the slopes (over 300 units of climbing)", climb > 300);
            Check("he never sank into the ground (never over 100 below it)", worstBelow > -100);
            Check("he stayed on it (90% of frames within 64 units)", onGround >= track.Count * 9 / 10);
        }
        Check("the engine took screenshots", File.Exists(shot0) && File.Exists(shotMid) && File.Exists(shot1));
        Console.WriteLine(failed == 0 ? "heightmap engine test: passed" : $"heightmap engine test: {failed} FAILED");
        return failed == 0 ? 0 : 1;
    }
}
