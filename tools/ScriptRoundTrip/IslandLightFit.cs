using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// How the retail islands' baked light relates to their terrain: a Lambert light from the cube's AlphaLight (elevation)
// and BetaLight (azimuth), regressed against the stored brightness.
internal static class IslandLightFit
{
    public static int Run(string[] args)
    {
        var dir = Environment.GetEnvironmentVariable("LBA2_DIR") ?? @"E:\GOG Games\Little Big Adventure 2 - Level viewer";
        var only = args.Length > 2 ? args[2].ToUpperInvariant() : "DESERT";
        var island = IslandFile.Load(Path.Combine(dir, only + ".ILE"));
        foreach (var cube in island.Cubes.Values.Take(8))
        {
            var normals = IslandLight.Normals(cube);
            var light = cube.Intensity.Select(v => (double)(v & 15)).ToArray();
            var best = (Rmse: 1e9, Az: 0.0, El: 0.0, Gain: 0.0, Offset: 0.0);
            for (var az = 0.0; az < 360; az += 2.5)
            for (var el = 10.0; el <= 90; el += 2.5)
            {
                var lam = normals.Select(n => IslandLight.Lambert(n, az, el)).ToArray();
                var (gain, offset, rmse) = Regress(lam, light);
                if (rmse < best.Rmse) best = (rmse, az, el, gain, offset);
            }
            var alpha = cube.AlphaLight * 360.0 / 4096; var beta = cube.BetaLight * 360.0 / 4096;
            Console.WriteLine($"cube {cube.Id}: best az {best.Az} el {best.El} gain {best.Gain:F2} offset {best.Offset:F2} rmse {best.Rmse:F2} | alpha {alpha:F1} beta {beta:F1} (360-beta {(360 - beta) % 360:F1})");
        }
        return 0;
    }

    private static (double Gain, double Offset, double Rmse) Regress(double[] x, double[] y)
    {
        var mx = x.Average(); var my = y.Average();
        double sxy = 0, sxx = 0;
        for (var i = 0; i < x.Length; i++) { sxy += (x[i] - mx) * (y[i] - my); sxx += (x[i] - mx) * (x[i] - mx); }
        var gain = sxx < 1e-12 ? 0 : sxy / sxx;
        var offset = my - gain * mx;
        double se = 0;
        for (var i = 0; i < x.Length; i++) { var e = y[i] - (offset + gain * x[i]); se += e * e; }
        return (gain, offset, Math.Sqrt(se / x.Length));
    }
}

// planprobe <ISLAND> <plan.json>: the ground along a track plan's centre line -- its height, whether the cell is drawn at all (sea or a
// hole in the island), and how steeply the line climbs -- so a route can be checked before anything is built.
internal static class PlanProbeCommand
{
    public static int Run(string[] args)
    {
        var dir = Environment.GetEnvironmentVariable("LBA2_DIR") ?? @"E:\GOG Games\Little Big Adventure 2 - Level viewer";
        var island = IslandFile.Load(Path.Combine(dir, args[1].ToUpperInvariant() + ".ILE"));
        var plan = LBAAssembler.Terrain.RaceTrackPlan.Load(args[2]);
        var pts = plan.Points.Select(p => (X: p[0] + plan.OriginCellX, Z: p[1] + plan.OriginCellZ)).ToList();
        var missing = new List<(double X, double Z)>();
        var heights = new List<double>();
        foreach (var (x, z) in pts)
        {
            var h = LBAAssembler.Terrain.IslandOps.Altitude(island, x * 512, z * 512);
            if (h is null) missing.Add((x, z));
            heights.Add(h ?? double.NaN);
        }
        Console.WriteLine($"{pts.Count} points; {missing.Count} over no ground (sea or outside the island)");
        foreach (var m in missing.Take(10)) Console.WriteLine($"  no ground at cell ({m.X:0.0}, {m.Z:0.0})");
        var known = heights.Where(h => !double.IsNaN(h)).ToList();
        if (known.Count > 0) Console.WriteLine($"  height {known.Min():0} .. {known.Max():0}");
        var steps = new List<(double Grade, double X, double Z)>();
        for (var i = 0; i < pts.Count; i++)
        {
            var j = (i + 1) % pts.Count;
            if (double.IsNaN(heights[i]) || double.IsNaN(heights[j])) continue;
            var run = Math.Sqrt(Math.Pow(pts[j].X - pts[i].X, 2) + Math.Pow(pts[j].Z - pts[i].Z, 2)) * 512;
            if (run > 1) steps.Add((Math.Abs(heights[j] - heights[i]) / run, pts[i].X, pts[i].Z));
        }
        foreach (var s in steps.OrderByDescending(s => s.Grade).Take(8))
            Console.WriteLine($"  steepest {s.Grade * 100:0}% at cell ({s.X:0.0}, {s.Z:0.0})");
        return 0;
    }
}

// islandheights <ISLAND> <out.csv>: the island's ground height at every grid point of its present cubes ("x,z,height", blank where no cube
// is), so a route drawn on a picture can be fitted and planned against the real ground.
internal static class IslandHeightsCommand
{
    public static int Run(string[] args)
    {
        var dir = Environment.GetEnvironmentVariable("LBA2_DIR") ?? @"E:\GOG Games\Little Big Adventure 2 - Level viewer";
        var island = IslandFile.Load(Path.Combine(dir, args[1].ToUpperInvariant() + ".ILE"));
        var (minX, minZ, maxX, maxZ) = island.PresentBounds();
        using var w = new StreamWriter(args[2]);
        w.WriteLine("x,z,height");
        for (var z = minZ * IslandCube.Cells; z <= (maxZ + 1) * IslandCube.Cells; z++)
        for (var x = minX * IslandCube.Cells; x <= (maxX + 1) * IslandCube.Cells; x++)
            w.WriteLine($"{x},{z},{island.HeightAt(x, z)?.ToString() ?? ""}");
        Console.WriteLine($"{args[2]}: cells {minX * IslandCube.Cells}..{(maxX + 1) * IslandCube.Cells} x {minZ * IslandCube.Cells}..{(maxZ + 1) * IslandCube.Cells}");
        return 0;
    }
}

// cellprobe <ISLAND> <x0> <z0> <x1> <z1>: each cell's two ground triangles (texture flag, polygon flag, game code, blocking bit) and its
// highest corner, one row of cells a line: "tp" per triangle ('.' for undrawn: texture and polygon flags 0), '#' when blocking.
internal static class CellProbeCommand
{
    public static int Run(string[] args)
    {
        var dir = Environment.GetEnvironmentVariable("LBA2_DIR") ?? @"E:\GOG Games\Little Big Adventure 2 - Level viewer";
        var island = IslandFile.Load(Path.Combine(dir, args[1].ToUpperInvariant() + ".ILE"));
        int x0 = int.Parse(args[2]), z0 = int.Parse(args[3]), x1 = int.Parse(args[4]), z1 = int.Parse(args[5]);
        foreach (var (cx, cz, c) in IslandOps.CubeCells(island))
            Console.WriteLine($"cube ({cx},{cz}): sea squares {Convert.ToString(c.BitField, 2).PadLeft(16, '0')}, texture definitions {c.TextureDefs.Length / 6}, decors {c.Decors.Count}");
        for (var z = z0; z <= z1; z++)
        {
            var line = $"{z,4} ";
            for (var x = x0; x <= x1; x++)
            {
                var cube = island.CubeAt(x / IslandCube.Cells, z / IslandCube.Cells);
                if (cube is null) { line += "  ----  "; continue; }
                var s = "";
                for (var h = 0; h < 2; h++)
                {
                    var p = new IslandPolygon(cube.Polygon(x % IslandCube.Cells, z % IslandCube.Cells, h));
                    s += p.TexFlag == 0 && p.PolyFlag == 0 ? ".." : $"{p.TexFlag}{p.PolyFlag}";
                    s += p.Col ? "#" : " ";
                }
                var hi = Enumerable.Range(0, 4).Max(k => island.HeightAt(x + k % 2, z + k / 2) ?? 0);
                var lit = island.LightAt(x, z) ?? -1;
                line += s + $"{hi / 100,3}/{lit,-2} ";
            }
            Console.WriteLine(line);
        }
        return 0;
    }
}

// holopic <game folder> <entry> <out.png>: a holomap island picture (HOLOMAP.HQR entry 18 + 2 x island: 640 x 480 in the game's
// palette, RESS.HQR entry 0), to look at.
internal static class HoloPicCommand
{
    public static int Run(string[] args)
    {
        var pic = LBAAssembler.HqrArchive.Open(Path.Combine(args[1], "HOLOMAP.HQR")).Read(int.Parse(args[2]));
        var pal = LBAAssembler.HqrArchive.Open(Path.Combine(args[1], "RESS.HQR")).Read(0);
        const int w = 640, h = 480;
        var six = pal.Take(768).Max() <= 63;
        var pixels = new byte[w * h * 4];
        for (var i = 0; i < w * h && i < pic.Length; i++)
        {
            var c = pic[i];
            byte C(int k) => (byte)(six ? pal[c * 3 + k] * 4 : pal[c * 3 + k]);
            pixels[i * 4] = C(2); pixels[i * 4 + 1] = C(1); pixels[i * 4 + 2] = C(0); pixels[i * 4 + 3] = 255;
        }
        PngWriter.Write(args[3], pixels, w, h);
        Console.WriteLine($"{args[3]}: HOLOMAP.HQR entry {args[2]}, {pic.Length} bytes");
        return 0;
    }
}

// islandfreetex <ISLAND> [out.png]: which 8 x 8 blocks of the island's ground texture page no cube's polygon reads, so a new tile can be
// put there. Every cube's texture definitions give the (u, v) corners its polygons sample; the page is 256 x 256.
internal static class IslandFreeTextureCommand
{
    public static int Run(string[] args)
    {
        var dir = Environment.GetEnvironmentVariable("LBA2_DIR") ?? @"E:\GOG Games\Little Big Adventure 2 - Level viewer";
        var name = args[1].ToUpperInvariant();
        var island = IslandFile.Load(Path.Combine(dir, name + ".ILE"));
        var used = new bool[256, 256];
        var defs = 0;
        // (REFERENCED=1: only the definitions a textured polygon still reads -- a build leaves the ones of the ground it repainted behind)
        var referencedOnly = Environment.GetEnvironmentVariable("REFERENCED") == "1";
        foreach (var cube in island.Cubes.Values)
        {
            var t = cube.TextureDefs;
            var read = new HashSet<int>();
            if (referencedOnly && cube.HasPolygons)
                for (var z = 0; z < IslandCube.Cells; z++)
                for (var x = 0; x < IslandCube.Cells; x++)
                for (var half = 0; half < 2; half++)
                {
                    var p = new IslandPolygon(cube.Polygon(x, z, half));
                    if (p.TexFlag != 0) read.Add(p.TextureIndex * 6);
                }
            for (var i = 0; i + 5 < t.Length; i += 6)
            {
                if (referencedOnly && !read.Contains(i)) continue;
                defs++;
                int u0 = 65535, v0 = 65535, u1 = 0, v1 = 0;
                for (var k = 0; k < 3; k++)
                {
                    u0 = Math.Min(u0, t[i + k * 2]); u1 = Math.Max(u1, t[i + k * 2]);
                    v0 = Math.Min(v0, t[i + k * 2 + 1]); v1 = Math.Max(v1, t[i + k * 2 + 1]);
                }
                for (var v = v0 / 256; v <= Math.Min(255, v1 / 256); v++)
                for (var u = u0 / 256; u <= Math.Min(255, u1 / 256); u++) used[u, v] = true;
            }
        }
        // the free 8 x 8 blocks, and the biggest free square
        var free = 0;
        var grid = new bool[32, 32];
        for (var by = 0; by < 32; by++)
        for (var bx = 0; bx < 32; bx++)
        {
            var any = false;
            for (var y = by * 8; y < by * 8 + 8 && !any; y++)
            for (var x = bx * 8; x < bx * 8 + 8 && !any; x++) any = used[x, y];
            grid[bx, by] = !any;
            if (!any) free++;
        }
        Console.WriteLine($"{name}: {defs} texture definitions, {free} of 1024 blocks of 8 x 8 pixels unused ({free * 100 / 1024}%)");
        for (var by = 0; by < 32; by++)
        {
            var line = "";
            for (var bx = 0; bx < 32; bx++) line += grid[bx, by] ? "." : "#";
            Console.WriteLine($"  y {by * 8,3}  {line}");
        }
        return 0;
    }
}

// islandtexture <ISLAND> <out.png>: the island's 256 x 256 ground texture page, in its own palette, to look at.
internal static class IslandTextureCommand
{
    public static int Run(string[] args)
    {
        var dir = Environment.GetEnvironmentVariable("LBA2_DIR") ?? @"E:\GOG Games\Little Big Adventure 2 - Level viewer";
        var name = args[1].ToUpperInvariant();
        var island = IslandFile.Load(Path.Combine(dir, name + ".ILE"));
        var palette = IslandMapRenderer.LoadPalette(dir, name);
        var page = island.GroundTexture;
        var pixels = new byte[256 * 256 * 4];
        for (var i = 0; i < 256 * 256 && i < page.Length; i++)
        {
            var c = page[i];
            pixels[i * 4] = palette[c * 3 + 2]; pixels[i * 4 + 1] = palette[c * 3 + 1]; pixels[i * 4 + 2] = palette[c * 3]; pixels[i * 4 + 3] = 255;
        }
        PngWriter.Write(args[2], pixels, 256, 256);
        Console.WriteLine($"{args[2]}: {name} ground texture, {page.Length} bytes");
        return 0;
    }
}

// island render <ISLAND> <view> <out.png> [scale]: the map renderer's output, to look at.
internal static class IslandRenderCommand
{
    public static int Run(string[] args)
    {
        var dir = Environment.GetEnvironmentVariable("LBA2_DIR") ?? @"E:\GOG Games\Little Big Adventure 2 - Level viewer";
        var name = args.Length > 2 ? args[2].ToUpperInvariant() : "DESERT";
        var view = args.Length > 3 ? Enum.Parse<MapView>(args[3], true) : MapView.Terrain;
        var output = args.Length > 4 ? args[4] : Path.Combine(Path.GetTempPath(), name + "_" + view + ".png");
        var scale = args.Length > 5 ? int.Parse(args[5]) : 3;
        var island = IslandFile.Load(Path.Combine(dir, name + ".ILE"));
        var renderer = new IslandMapRenderer(island, IslandMapRenderer.LoadPalette(dir, name), scale);
        renderer.RenderAll(view);
        PngWriter.Write(output, renderer.Pixels, renderer.PixelWidth, renderer.PixelHeight);
        Console.WriteLine($"{output}: {renderer.PixelWidth}x{renderer.PixelHeight}");
        return 0;
    }
}

// island footprints <ISLAND>: do the retail baked shadows sit under the decors' bounding boxes?
internal static class IslandFootprintStudy
{
    public static int Run(string[] args)
    {
        var dir = Environment.GetEnvironmentVariable("LBA2_DIR") ?? @"E:\GOG Games\Little Big Adventure 2 - Level viewer";
        foreach (var name in args.Length > 2 ? new[] { args[2].ToUpperInvariant() } : new[] { "DESERT", "CITABAU", "OTRINGAL", "KNARTAS" })
        {
            var island = IslandFile.Load(Path.Combine(dir, name + ".ILE"));
            var options = BakeOptions.For(island.Cubes.Values.First());
            var field = new IslandHeightField(island);
            double insideSum = 0, outsideSum = 0; int inside = 0, outside = 0, dark = 0, boxes = 0;
            var inBox = new HashSet<(int, int)>();
            foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
                foreach (var d in cube.Decors)
                {
                    if ((d.XMax - d.XMin) < 1024 || (d.ZMax - d.ZMin) < 1024) continue;
                    boxes++;
                    int x0 = (cx * 32768 + d.XMin) / 512, x1 = (cx * 32768 + d.XMax) / 512, z0 = (cz * 32768 + d.ZMin) / 512, z1 = (cz * 32768 + d.ZMax) / 512;
                    for (var z = z0; z <= z1; z++) for (var x = x0; x <= x1; x++) inBox.Add((x, z));
                }
            foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
                for (var z = 0; z < 65; z++) for (var x = 0; x < 65; x++)
                {
                    var gx = cx * 64 + x; var gz = cz * 64 + z;
                    var diff = cube.Light(x, z) - IslandBake.Lit(field, gx, gz, options);
                    if (inBox.Contains((gx, gz))) { insideSum += diff; inside++; if (diff < -3) dark++; } else { outsideSum += diff; outside++; }
                }
            Console.WriteLine($"{name}: {boxes} large boxes, {inside} vertices inside (mean light minus plain light {insideSum / Math.Max(1, inside):F2}, {dark} darker than -3), {outside} outside (mean {outsideSum / Math.Max(1, outside):F2})");
        }
        return 0;
    }
}
