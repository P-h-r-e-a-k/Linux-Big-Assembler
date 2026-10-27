using System.Globalization;
using System.IO;

namespace LBAAssembler.Terrain;

// Where a heightmap's pixels go on an island: one pixel per height point (vertex) of the cubes' bounding box on the island's 16 x 16 map.
// A cube is 64 x 64 cells, so 65 x 65 points, and neighbouring cubes share the points along their common edge: W x H cubes are
// W * 64 + 1 by H * 64 + 1 pixels. The picture lies as the editor's top-down map and minimap show the island: its top-left pixel is the
// corner of the box with the lowest world X and Z, going right is +X and going down is +Z, 512 world units (one cell) a pixel.
internal readonly record struct HeightmapLayout(int CubeX0, int CubeZ0, int CubesX, int CubesZ)
{
    public int Width => CubesX * IslandCube.Cells + 1;
    public int Height => CubesZ * IslandCube.Cells + 1;
    public int Gx0 => CubeX0 * IslandCube.Cells;
    public int Gz0 => CubeZ0 * IslandCube.Cells;

    public static HeightmapLayout Of(IslandFile island)
    {
        var (x0, z0, x1, z1) = island.PresentBounds();
        return new HeightmapLayout(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
    }

    // The picture size for an island of `cubesX` x `cubesZ` cubes.
    public static (int Width, int Height) SizeFor(int cubesX, int cubesZ) => (cubesX * IslandCube.Cells + 1, cubesZ * IslandCube.Cells + 1);

    // How many cubes a picture of this size makes (a new island from it): 64n + 1 pixels (or 64n, one a cell) is n cubes.
    public static int CubesFor(int pixels) => Math.Max(1, (pixels - 1 + IslandCube.Cells / 2) / IslandCube.Cells);
}

internal enum HeightmapFit
{
    Exact,          // only a picture of the island's own size (refuse any other)
    Resample,       // stretched or shrunk to the island's size (corner to corner)
    Crop,           // pixel for pixel from the top-left corner: what lies beyond the island is left out, ground beyond the picture is left alone
}

internal enum HeightmapTextures
{
    Keep,           // the ground's pictures and game codes stay as they are
    SeaAndShore,    // cells that sink to the sea level become sea, cells that rise out of it become land (the island's own sea / ground)
    AllBySlope,     // every cell again by its height and slope: sea, flat ground, steep ground (as a new island is textured)
}

internal sealed class HeightmapImportOptions
{
    // world heights of a black (level 0) and a white (level 1) pixel; a file of numbers holds heights and ignores them
    public double Black { get; set; }
    public double White { get; set; } = 4000;
    public HeightmapFit Fit { get; set; } = HeightmapFit.Resample;
    // ground at or below it is sea (the retail islands' sea floor is 0; the game draws the sea plane at -50, TERRAIN.H Y_SEA)
    public int SeaLevel { get; set; }
    public int SmoothPasses { get; set; }
    public HeightmapTextures Textures { get; set; } = HeightmapTextures.SeaAndShore;
    // the sea, flat ground and steep ground to texture with; null = the island's own most common ones (IslandGround.CommonGround)
    public (IslandGround.Sample? Sea, IslandGround.Sample? Flat, IslandGround.Sample? Steep)? Ground { get; set; }
    public bool FixDiagonals { get; set; } = true;
    // the triangles steeper than this many degrees blocked (Col bit) and the others cleared; null = the Col bits stay as they are
    public double? SteepAngle { get; set; }
    // the light baked again with these settings; null = the stored light stays
    public BakeOptions? Bake { get; set; }
    // with Bake: shadows under the objects (footprints, as the retail islands have under buildings), this many levels deep; null = none
    public int? ObjectShadowDepth { get; set; }
    public bool ObjectsFollow { get; set; } = true;
}

internal sealed record HeightmapImportResult(int Vertices, int Changed, int Clamped, int Lowest, int Highest, int SeaCells, int LandCells, int Retextured, int Blocked, int Relit, int ObjectsMoved);

internal static class IslandHeightmap
{
    public const int DefaultWhite = 4000;

    // ---- what the island needs ------------------------------------------------------------------------------------------------------------

    // The size statement the dialogs show.
    public static string SizeText(IslandFile island, string? name = null)
    {
        var l = HeightmapLayout.Of(island);
        var missing = MissingCubes(island, l);
        return $"{name ?? "This island"} is {l.CubesX} × {l.CubesZ} cubes, so its heightmap is {l.Width} × {l.Height} pixels: one pixel per height point, "
             + $"{IslandCube.Cells} cells a cube plus one, as neighbouring cubes share the points along their common edge."
             + (missing > 0 ? $" {missing} of the {l.CubesX * l.CubesZ} map squares in that box {(missing == 1 ? "has" : "have")} no cube: {(missing == 1 ? "its" : "their")} pixels are left out." : "");
    }

    // Which pixel is which point of the world.
    public static string LayoutText(IslandFile island)
    {
        var l = HeightmapLayout.Of(island);
        int x0 = l.Gx0 * IslandFile.CellSize, z0 = l.Gz0 * IslandFile.CellSize, x1 = (l.Gx0 + l.Width - 1) * IslandFile.CellSize, z1 = (l.Gz0 + l.Height - 1) * IslandFile.CellSize;
        return $"It lies as the top-down map and the minimap show the island: the top-left pixel is world X {x0:N0}, Z {z0:N0} (the corner of cube {l.CubeX0}, {l.CubeZ0} "
             + $"of the island's 16 × 16 map), going right is +X, going down is +Z, {IslandFile.CellSize} world units a pixel; the bottom-right pixel is X {x1:N0}, Z {z1:N0}. "
             + "Black is the lowest ground, white the highest. Transparent pixels leave the ground under them as it is.";
    }

    private static int MissingCubes(IslandFile island, HeightmapLayout l)
    {
        var missing = 0;
        for (var z = 0; z < l.CubesZ; z++) for (var x = 0; x < l.CubesX; x++) if (island.CubeAt(l.CubeX0 + x, l.CubeZ0 + z) is null) missing++;
        return missing;
    }

    // The black and white heights a picture is read with when nothing else says: the island's own lowest and highest ground, or 0 .. 4000 for a
    // flat one.
    public static (int Black, int White) DefaultRange(IslandFile island)
    {
        var (lo, hi) = IslandOps.HeightRange(island);
        return hi - lo >= 256 ? (lo, hi) : (Math.Min((int)lo, 0), Math.Max((int)lo, 0) + DefaultWhite);
    }

    // ---- export ---------------------------------------------------------------------------------------------------------------------------

    // The island's heights over its layout (NaN where no cube is).
    public static double[] Heights(IslandFile island, HeightmapLayout? at = null)
    {
        var l = at ?? HeightmapLayout.Of(island);
        var heights = new double[l.Width * l.Height];
        for (var j = 0; j < l.Height; j++)
        for (var i = 0; i < l.Width; i++)
            heights[j * l.Width + i] = island.HeightAt(l.Gx0 + i, l.Gz0 + j) is { } h ? h : double.NaN;
        return heights;
    }

    // ... as 0..1 levels between `black` and `white`.
    public static double[] Levels(double[] heights, double black, double white)
    {
        var levels = new double[heights.Length];
        var span = white - black;
        for (var i = 0; i < heights.Length; i++)
            levels[i] = double.IsNaN(heights[i]) ? double.NaN : span == 0 ? 0 : Math.Clamp((heights[i] - black) / span, 0, 1);
        return levels;
    }

    public enum ExportFormat { Png16, Png8, Raw16, Csv }

    // Writes the island's heightmap; returns the black and white heights used (the island's lowest and highest ground unless given).
    public static (int Black, int White) Export(IslandFile island, string path, ExportFormat format, (int Black, int White)? range = null, string? islandName = null)
    {
        var l = HeightmapLayout.Of(island);
        var heights = Heights(island, l);
        var (black, white) = range ?? DefaultRange(island);
        byte[] bytes;
        switch (format)
        {
            case ExportFormat.Csv:
                bytes = System.Text.Encoding.UTF8.GetBytes(HeightmapFile.EncodeCsv(l.Width, l.Height, heights));
                break;
            case ExportFormat.Raw16:
                // (no transparency in a raw file: where no cube is, black)
                bytes = HeightmapFile.EncodeRaw(Levels(heights, black, white).Select(v => double.IsNaN(v) ? 0 : v).ToArray());
                break;
            default:
                var text = new Dictionary<string, string>
                {
                    [HeightmapFile.TextKey] = string.Create(CultureInfo.InvariantCulture, $"black={black} white={white} island={islandName ?? Path.GetFileName(island.Path)} cubes={l.CubeX0},{l.CubeZ0} size={l.CubesX}x{l.CubesZ}"),
                    ["Software"] = "LBA Assembler",
                };
                bytes = HeightmapFile.EncodePng(l.Width, l.Height, Levels(heights, black, white), format == ExportFormat.Png8 ? 8 : 16, text);
                break;
        }
        // (written beside it and moved over it: a picture being replaced is never left half written; no .bak, it is not a game file)
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
        return (black, white);
    }

    // ---- import ---------------------------------------------------------------------------------------------------------------------------

    // Whether the picture is the island's own size.
    public static bool ExactSize(IslandFile island, HeightmapPicture picture)
    {
        var l = HeightmapLayout.Of(island);
        return picture.Width == l.Width && picture.Height == l.Height;
    }

    // The height each point of the layout gets (NaN: left as it is), from the picture placed as `options.Fit` says; `clamped` counts the
    // heights cut to the game's range (-32768 .. 32767).
    public static double[] Targets(IslandFile island, HeightmapPicture picture, HeightmapImportOptions options, out int clamped)
    {
        var l = HeightmapLayout.Of(island);
        if (options.Fit == HeightmapFit.Exact && (picture.Width != l.Width || picture.Height != l.Height))
            throw new InvalidDataException($"The picture is {picture.Width} × {picture.Height}; the island needs {l.Width} × {l.Height} exactly.");
        var targets = new double[l.Width * l.Height];
        clamped = 0;
        var resample = options.Fit == HeightmapFit.Resample && (picture.Width != l.Width || picture.Height != l.Height);
        for (var j = 0; j < l.Height; j++)
        for (var i = 0; i < l.Width; i++)
        {
            double v;
            if (resample)
            {
                var u = l.Width > 1 ? i * (picture.Width - 1) / (double)(l.Width - 1) : 0;
                var w = l.Height > 1 ? j * (picture.Height - 1) / (double)(l.Height - 1) : 0;
                v = SampleAt(picture, u, w, (picture.Width - 1) / (double)Math.Max(1, l.Width - 1), (picture.Height - 1) / (double)Math.Max(1, l.Height - 1));
            }
            else v = i < picture.Width && j < picture.Height ? picture.At(i, j) : double.NaN;
            if (double.IsNaN(v) || island.HeightAt(l.Gx0 + i, l.Gz0 + j) is null) { targets[j * l.Width + i] = double.NaN; continue; }
            var h = picture.Absolute ? v : options.Black + v * (options.White - options.Black);
            if (h < short.MinValue || h > short.MaxValue) { clamped++; h = Math.Clamp(h, short.MinValue, short.MaxValue); }
            targets[j * l.Width + i] = Math.Round(h);
        }
        return targets;
    }

    // The picture's value at (u, v) pixels: bilinear when the picture is about the island's size or smaller, the mean of the pixels under the
    // point's footprint when it is shrunk (so a big picture doesn't alias). NaN when most of what is under it is transparent.
    private static double SampleAt(HeightmapPicture p, double u, double v, double stepX, double stepY)
    {
        if (stepX > 1.5 || stepY > 1.5)
        {
            // (the footprint kept symmetric round the point at the picture's edges, so the edge points aren't pulled inwards)
            var rx = Math.Min(Math.Max(stepX, 1) / 2, Math.Min(u, p.Width - 1 - u) + 0.5);
            var ry = Math.Min(Math.Max(stepY, 1) / 2, Math.Min(v, p.Height - 1 - v) + 0.5);
            int x0 = (int)Math.Ceiling(u - rx + 0.5 - 1e-9), x1 = (int)Math.Floor(u + rx - 0.5 + 1e-9), y0 = (int)Math.Ceiling(v - ry + 0.5 - 1e-9), y1 = (int)Math.Floor(v + ry - 0.5 + 1e-9);
            if (x1 < x0) x0 = x1 = (int)Math.Round(u);
            if (y1 < y0) y0 = y1 = (int)Math.Round(v);
            double sum = 0; int count = 0, all = 0;
            for (var y = Math.Max(0, y0); y <= Math.Min(p.Height - 1, y1); y++)
            for (var x = Math.Max(0, x0); x <= Math.Min(p.Width - 1, x1); x++)
            {
                all++;
                var s = p.At(x, y);
                if (double.IsNaN(s)) continue;
                sum += s; count++;
            }
            if (all > 0) return count * 2 < all || count == 0 ? double.NaN : sum / count;
        }
        int ix = Math.Clamp((int)Math.Floor(u), 0, p.Width - 1), iy = Math.Clamp((int)Math.Floor(v), 0, p.Height - 1);
        int ix1 = Math.Min(ix + 1, p.Width - 1), iy1 = Math.Min(iy + 1, p.Height - 1);
        double fx = Math.Clamp(u - ix, 0, 1), fy = Math.Clamp(v - iy, 0, 1);
        double total = 0, weights = 0;
        void Add(int x, int y, double w) { if (w <= 0) return; var s = p.At(x, y); if (double.IsNaN(s)) return; total += s * w; weights += w; }
        Add(ix, iy, (1 - fx) * (1 - fy)); Add(ix1, iy, fx * (1 - fy)); Add(ix, iy1, (1 - fx) * fy); Add(ix1, iy1, fx * fy);
        return weights < 0.5 ? double.NaN : total / weights;
    }

    // Puts the picture's heights on the island and brings what follows from the heights up to date as asked: the cells' diagonals, the sea and
    // land textures, the blocked (too steep) triangles, the baked light, the objects standing on the ground. Everything goes through the
    // editor's own operations (IslandOps, IslandGround, IslandBake), so the imported ground is as valid as sculpted ground. Only ground whose
    // height changes is touched: an exported heightmap edited in one corner and imported again leaves the rest of the island (its hand-made
    // light, diagonals, pictures) exactly as it was.
    public static HeightmapImportResult Apply(IslandFile island, HeightmapPicture picture, HeightmapImportOptions options)
    {
        var l = HeightmapLayout.Of(island);
        var targets = Targets(island, picture, options, out var clamped);

        // the points the picture covers; those whose height it changes; the cells round those (a cell changes when any of its corners does)
        var covered = 0;
        var changedPoints = new List<(int Gx, int Gz, int H)>();
        var cells = new HashSet<(int, int)>();
        for (var j = 0; j < l.Height; j++)
        for (var i = 0; i < l.Width; i++)
        {
            if (double.IsNaN(targets[j * l.Width + i])) continue;
            int gx = l.Gx0 + i, gz = l.Gz0 + j;
            covered++;
            var h = (int)targets[j * l.Width + i];
            if (island.HeightAt(gx, gz) == h) continue;
            changedPoints.Add((gx, gz, h));
            for (var dz = -1; dz <= 0; dz++)
            for (var dx = -1; dx <= 0; dx++)
            {
                int cx = gx + dx, cz = gz + dz;
                if (cx < 0 || cz < 0 || cx >= IslandFile.GridSize || cz >= IslandFile.GridSize) continue;
                if (island.CubeAt(cx / IslandCube.Cells, cz / IslandCube.Cells) is { HasPolygons: true }) cells.Add((cx, cz));
            }
        }
        var cellRegion = new PointsRegion(cells);
        var sea = options.SeaLevel;
        bool IsSea(int cx, int cz) =>
            island.HeightAt(cx, cz) is { } a && island.HeightAt(cx + 1, cz) is { } b && island.HeightAt(cx, cz + 1) is { } c && island.HeightAt(cx + 1, cz + 1) is { } d
            && Math.Max(Math.Max(a, b), Math.Max(c, d)) <= sea;
        var wasSea = options.Textures == HeightmapTextures.SeaAndShore ? cells.Where(c => IsSea(c.Item1, c.Item2)).ToHashSet() : new HashSet<(int, int)>();
        // (only the objects whose origin stands on a cell that changes: the others' ground stays where it is)
        var changedSet = changedPoints.Select(p => (p.Gx, p.Gz)).ToHashSet();
        bool OverChange(double wx, double wz)
        {
            int gx = (int)Math.Floor(wx / IslandFile.CellSize), gz = (int)Math.Floor(wz / IslandFile.CellSize);
            return changedSet.Contains((gx, gz)) || changedSet.Contains((gx + 1, gz)) || changedSet.Contains((gx, gz + 1)) || changedSet.Contains((gx + 1, gz + 1));
        }
        var follow = options.ObjectsFollow && changedPoints.Count > 0 ? new IslandOps.DecorFollow(island, near: OverChange) : null;

        // the heights
        foreach (var (gx, gz, h) in changedPoints) island.SetHeight(gx, gz, h);
        var changed = changedPoints.Count;
        var points = new PointsRegion(changedPoints.Select(p => (p.Gx, p.Gz)));
        for (var k = 0; k < options.SmoothPasses && changed > 0; k++) IslandOps.Smooth(island, points, 0.5);

        if (options.FixDiagonals) IslandGround.OptimiseDiagonals(island, cellRegion);

        // the ground's pictures: sea where the ground went under the sea level, land where it came out of it (or everything by slope)
        var retextured = 0;
        int seaCells = 0, landCells = 0;
        foreach (var (cx, cz) in cells) if (IsSea(cx, cz)) seaCells++; else landCells++;
        if (options.Textures != HeightmapTextures.Keep)
        {
            var (seaSample, flatSample, steepSample) = options.Ground ?? IslandGround.CommonGround(island);
            var toSea = new List<(int, int)>(); var toFlat = new List<(int, int)>(); var toSteep = new List<(int, int)>();
            foreach (var (cx, cz) in cells)
            {
                var nowSea = IsSea(cx, cz);
                if (options.Textures == HeightmapTextures.SeaAndShore && nowSea == wasSea.Contains((cx, cz))) continue;
                if (nowSea) { toSea.Add((cx, cz)); continue; }
                (Slope(island, cx, cz) > SteepGround && steepSample is not null ? toSteep : toFlat).Add((cx, cz));
            }
            if (seaSample is not null && toSea.Count > 0) retextured += IslandGround.Paint(island, new PointsRegion(toSea), seaSample, PolygonFields.Texture | PolygonFields.GameCode);
            if (flatSample is not null && toFlat.Count > 0) retextured += IslandGround.Paint(island, new PointsRegion(toFlat), flatSample, PolygonFields.Texture | PolygonFields.GameCode);
            if (steepSample is not null && toSteep.Count > 0) retextured += IslandGround.Paint(island, new PointsRegion(toSteep), steepSample, PolygonFields.Texture | PolygonFields.GameCode);
        }

        var blocked = 0;
        if (options.SteepAngle is { } angle)
        {
            IslandGround.SetSteepCollision(island, cellRegion, angle);
            foreach (var (cx, cz) in cells)
                if (island.CubeAt(cx / IslandCube.Cells, cz / IslandCube.Cells) is { } cube)
                    for (var half = 0; half < 2; half++) if (new IslandPolygon(cube.Polygon(cx % IslandCube.Cells, cz % IslandCube.Cells, half)).Col) blocked++;
        }

        var moved = follow?.Apply() ?? 0;

        // the light: the points whose ground (or a neighbour's: the normal) changed
        var relit = 0;
        if (options.Bake is { } bake && changed > 0)
        {
            var lit = new HashSet<(int, int)>();
            foreach (var (gx, gz, _) in changedPoints)
                for (var dz = -1; dz <= 1; dz++) for (var dx = -1; dx <= 1; dx++) if (island.HasVertex(gx + dx, gz + dz)) lit.Add((gx + dx, gz + dz));
            relit = IslandBake.Bake(island, new PointsRegion(lit), bake);
            // (the shadows under the objects standing on the ground that was lit again: the retail islands' footprints)
            if (options.ObjectShadowDepth is { } depth && depth > 0)
            {
                var field = new IslandHeightField(island);
                foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
                    foreach (var d in cube.Decors)
                    {
                        if (d.XMax - d.XMin < 1024 || d.ZMax - d.ZMin < 1024) continue;
                        var (x0, z0, x1, z1) = IslandBake.Footprint(cx, cz, d);
                        var touched = false;
                        for (var gz = z0; gz <= z1 && !touched; gz++) for (var gx = x0; gx <= x1 && !touched; gx++) touched = lit.Contains((gx, gz));
                        if (touched) relit += IslandBake.FootprintShadow(island, cx, cz, d, bake, depth, remove: false, field: field);
                    }
            }
        }

        int lowest = int.MaxValue, highest = int.MinValue;
        for (var j = 0; j < l.Height; j++)
        for (var i = 0; i < l.Width; i++)
        {
            if (double.IsNaN(targets[j * l.Width + i]) || island.HeightAt(l.Gx0 + i, l.Gz0 + j) is not { } h) continue;
            lowest = Math.Min(lowest, h); highest = Math.Max(highest, h);
        }
        if (covered == 0) lowest = highest = 0;
        return new HeightmapImportResult(covered, changed, clamped, lowest, highest, seaCells, landCells, retextured, blocked, relit, moved);
    }

    // Ground steeper than this (degrees, over the cell's diagonals) is textured as steep ground, as a new island's is (MapNew).
    public const double SteepGround = 22;

    public static double Slope(IslandFile island, int cx, int cz)
    {
        if (island.HeightAt(cx, cz) is not { } a || island.HeightAt(cx + 1, cz) is not { } b || island.HeightAt(cx, cz + 1) is not { } c || island.HeightAt(cx + 1, cz + 1) is not { } e) return 0;
        return Math.Atan(Math.Max(Math.Abs(a - e), Math.Abs(b - c)) / (IslandFile.CellSize * Math.Sqrt(2))) * 180 / Math.PI;
    }

    // ---- the dialog's preview -------------------------------------------------------------------------------------------------------------

    // The ground the import makes (or the island's ground where the picture leaves it), as a BGRA picture of the layout's size: shaded from the
    // top-left, coloured by height, the sea blue, no-cube squares dark.
    public static byte[] ShadedPreview(IslandFile island, double[] targets, int seaLevel, out (int Lo, int Hi, int SeaPercent, int SteepPercent) stats, double steepDegrees = 45)
    {
        var l = HeightmapLayout.Of(island);
        int w = l.Width, h = l.Height;
        var heights = Heights(island, l);
        for (var i = 0; i < heights.Length; i++) if (!double.IsNaN(targets[i])) heights[i] = targets[i];
        double lo = double.MaxValue, hi = double.MinValue;
        foreach (var v in heights) if (!double.IsNaN(v)) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
        if (lo > hi) { lo = 0; hi = 1; }
        var landTop = Math.Max(hi, seaLevel + 1);
        var bgra = new byte[w * h * 4];
        int seaCount = 0, landCount = 0, steepCount = 0;
        var limit = Math.Tan(steepDegrees * Math.PI / 180);
        // light from the top-left, 45 degrees up
        double lx = -0.5, ly = 0.7071, lz = -0.5;
        double H(int x, int z, double fallback) => x < 0 || z < 0 || x >= w || z >= h || double.IsNaN(heights[z * w + x]) ? fallback : heights[z * w + x];
        for (var z = 0; z < h; z++)
        for (var x = 0; x < w; x++)
        {
            var i = z * w + x;
            var y = heights[i];
            byte r, g, b;
            if (double.IsNaN(y)) { r = 28; g = 32; b = 40; }
            else
            {
                double dx = (H(x + 1, z, y) - H(x - 1, z, y)) / (2.0 * IslandFile.CellSize), dz = (H(x, z + 1, y) - H(x, z - 1, y)) / (2.0 * IslandFile.CellSize);
                var len = Math.Sqrt(dx * dx + dz * dz + 1);
                var shade = 0.45 + 0.75 * Math.Max(0, (-dx * lx + ly - dz * lz) / len);
                if (Math.Sqrt(dx * dx + dz * dz) > limit) steepCount++;
                double cr, cg, cb;
                if (y <= seaLevel) { seaCount++; var t = Math.Clamp((seaLevel - y) / 1500.0, 0, 1); cr = 60 - 30 * t; cg = 120 - 50 * t; cb = 190 - 50 * t; shade = 0.85 + 0.15 * shade; }
                else
                {
                    landCount++;
                    var t = Math.Clamp((y - seaLevel) / (landTop - seaLevel), 0, 1);
                    // sand, grass, rock, snow
                    (cr, cg, cb) = t < 0.08 ? (214.0, 200.0, 150.0) : t < 0.55 ? Lerp((110, 160, 80), (150, 130, 90), (t - 0.08) / 0.47) : Lerp((150, 130, 90), (235, 235, 235), (t - 0.55) / 0.45);
                }
                r = (byte)Math.Clamp(cr * shade, 0, 255); g = (byte)Math.Clamp(cg * shade, 0, 255); b = (byte)Math.Clamp(cb * shade, 0, 255);
                if (double.IsNaN(targets[i])) { r = (byte)(r * 0.6 + 40); g = (byte)(g * 0.6 + 40); b = (byte)(b * 0.6 + 40); }      // left as it is: washed out
            }
            bgra[i * 4] = b; bgra[i * 4 + 1] = g; bgra[i * 4 + 2] = r; bgra[i * 4 + 3] = 255;
        }
        var total = Math.Max(1, seaCount + landCount);
        stats = ((int)Math.Round(lo), (int)Math.Round(hi), (int)Math.Round(seaCount * 100.0 / total), (int)Math.Round(steepCount * 100.0 / total));
        return bgra;
    }

    private static (double, double, double) Lerp((double R, double G, double B) a, (double R, double G, double B) b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return (a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    }

    // The picture as it lands on the island (grey levels over the layout; transparent and no-cube points checkered), BGRA.
    public static byte[] PicturePreview(IslandFile island, HeightmapPicture picture, HeightmapImportOptions options)
    {
        var l = HeightmapLayout.Of(island);
        var targets = Targets(island, picture, options, out _);
        double lo = double.MaxValue, hi = double.MinValue;
        foreach (var v in targets) if (!double.IsNaN(v)) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
        if (!picture.Absolute) { lo = Math.Min(options.Black, options.White); hi = Math.Max(options.Black, options.White); }
        var span = Math.Max(1, hi - lo);
        var bgra = new byte[l.Width * l.Height * 4];
        for (var j = 0; j < l.Height; j++)
        for (var i = 0; i < l.Width; i++)
        {
            var k = j * l.Width + i;
            var v = targets[k];
            byte c = double.IsNaN(v) ? (byte)(((i / 6 + j / 6) & 1) == 0 ? 70 : 100) : (byte)Math.Clamp((v - lo) / span * 255, 0, 255);
            if (!picture.Absolute && options.White < options.Black && !double.IsNaN(v)) c = (byte)(255 - c);
            bgra[k * 4] = c; bgra[k * 4 + 1] = c; bgra[k * 4 + 2] = c; bgra[k * 4 + 3] = 255;
        }
        return bgra;
    }
}

// A set of island points (vertices, or cells by their top-left vertex) as a region for the editor's operations.
internal sealed class PointsRegion : IslandRegion
{
    private readonly List<(int Gx, int Gz)> points;
    public PointsRegion(IEnumerable<(int Gx, int Gz)> points) => this.points = points.ToList();
    public override IEnumerable<(int Gx, int Gz, double Weight)> Vertices(IslandFile island) => points.Select(p => (p.Gx, p.Gz, 1.0));
    public override (double Gx, double Gz) Center => points.Count == 0 ? (0, 0) : (points.Average(p => p.Gx), points.Average(p => p.Gz));
}
