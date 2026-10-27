using LBAAssembler;
using LBAAssembler.Lba1;
using LBAAssembler.Terrain;
using LBAAssembler.Terrain.Polar;
using LBAAssembler.Terrain.Temple;

namespace ScriptRoundTrip;

// Studies for the Temple of Bú's port (Terrain/Temple) -- read only:
//   templecolours <LBA1 folder> <LBA2 game folder>: the colours the temple's bricks draw (pixels, weighted by how often each brick is placed),
//     and how near the retail island palettes' colours come to them
//   templeregion <LBA1 folder> <out.png> [x0 x1 z0 z1]: the temple (or a region of it) as LBA1 draws it
//   templemap <LBA1 folder> <out.png> [scale]: the temple from above -- each column's top layer as a shade, its top brick's code
//   templebuild <LBA1 folder> <LBA2 game folder>: builds the island into the game folder (TempleIsland.Add) -- a sandbox's, never the game's
internal static class TempleStudy
{
    // templebricks <LBA1 folder> [n]: the temple's most placed bricks -- how often, their box, how much of each face they draw
    public static int Bricks(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var layout = TempleLayout.Build(game);
        var n = args.Length > 2 ? int.Parse(args[2]) : 30;
        foreach (var g in layout.Cells.Values.GroupBy(c => c.Brick).OrderByDescending(g => g.Count()).Take(n))
        {
            var s = PolarTextures.Sprite.Decode(game.ReadBrick(g.Key));
            var box = s.Footprint();
            var fit = PolarTextures.Sprite.LastFit;
            Console.WriteLine($"brick {g.Key,4} x{g.Count(),5} shape {g.First().Shape} code {g.First().Code:X2} sprite {s.Width}x{s.Lines} hot ({s.HotX},{s.HotY}) box {box.U0:0.00}-{box.U1:0.00} x {box.V0:0.00}-{box.V1:0.00} " +
                $"(full {fit.Full:0.00} best {fit.Best:0.00}) drawn top {s.Coverage(PolarTextures.Face.Top):0.00} +x {s.Coverage(PolarTextures.Face.SideX):0.00} +z {s.Coverage(PolarTextures.Face.SideZ):0.00}");
        }
        return 0;
    }

    // templeblocks <LBA1 folder> <out folder>: the library blocks (LBA1's layouts) the temple's grids place -- each block's size, how often
    // placed, its bricks, and a picture of it alone (b<scene>_<block>.png), for finding the round and shaped things the boxes draw badly
    public static int Blocks(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var outDir = args[2];
        Directory.CreateDirectory(outDir);
        var area = game.Areas.First(a => a.Tiles.Any(t => t.Scene == TempleLayout.FirstScene));
        // (every placement, in the layout's cells: blocks.csv)
        using var csv = new StreamWriter(Path.Combine(outDir, "blocks.csv"));
        csv.WriteLine("scene,block,x,y,z,dx,dy,dz,cells");
        foreach (var tile in area.Tiles.Where(t => TempleLayout.Scenes.Contains(t.Scene)))
        {
            int ox = tile.OffsetX / 512, oy = tile.OffsetY / 256, oz = tile.OffsetZ / 512;
            var cells = Lba1GridCodec.Decode(game.ReadGrid(tile.Scene));
            var lib = game.ReadBlocks(tile.Scene);
            var instances = new Dictionary<(int Block, int X, int Y, int Z), int>();
            for (var z = 0; z < 64; z++) for (var x = 0; x < 64; x++) for (var y = 0; y < 25; y++)
            {
                var i = ((z * 64 + x) * 25 + y) * 2;
                int block = cells[i], pos = cells[i + 1];
                if (block == 0 || !tile.Holds(x, z)) continue;
                var at = BitConverter.ToInt32(lib, (block - 1) * 4);
                int dx = lib[at], dy = lib[at + 1];
                instances[(block, x - pos % dx, y - pos / dx % dy, z - pos / dx / dy)] = instances.GetValueOrDefault((block, x - pos % dx, y - pos / dx % dy, z - pos / dx / dy)) + 1;
            }
            foreach (var g in instances.GroupBy(i => i.Key.Block).OrderBy(g => g.Key))
            {
                var at = BitConverter.ToInt32(lib, (g.Key - 1) * 4);
                int dx = lib[at], dy = lib[at + 1], dz = lib[at + 2];
                var placements = new List<Lba1Placement>();
                var bricks = new List<int>();
                for (var p = 0; p < dx * dy * dz; p++)
                {
                    var number = BitConverter.ToUInt16(lib, at + 3 + p * 4 + 2);
                    if (number == 0) continue;
                    placements.Add(new Lba1Placement(p % dx, p / dx % dy, p / dx / dy, number - 1));
                    bricks.Add(number - 1);
                }
                foreach (var ((_, x, y, z), n) in g) csv.WriteLine($"{tile.Scene},{g.Key},{x + ox},{y + oy},{z + oz},{dx},{dy},{dz},{n}");
                var first = g.First().Key;
                Console.WriteLine($"scene {tile.Scene} block {g.Key}: {dx} x {dy} x {dz}, {g.Count()} placed (first at {first.X},{first.Y},{first.Z}), " +
                                  $"shape {lib[at + 3]} code {lib[at + 4]:X2}, bricks {string.Join(",", bricks.Distinct().Take(12))}");
                if (placements.Count == 0) continue;
                var image = Lba1GridRenderer.Render(new[] { new Lba1Tile(placements, 0, 0, 0) }, game.ReadBrick, game.Palette);
                PngWriter.Write(Path.Combine(outDir, $"b{tile.Scene}_{g.Key}.png"), image.Bgra, image.Width, image.Height);
            }
        }
        return 0;
    }

    // templecells <LBA1 folder> <plan.json> <out.csv>: every cell of the temple as the race track's build has it -- its place (the layout's
    // cells and layers), the LBA1 block it is part of (scene, block), its brick and shape, the round thing or the shaped thing it is part
    // of, the box of its cell it fills and the most it draws of a face (TempleObjects' reading), and whether the road's doorways cut it (1)
    // or what they left hanging went with them (2), or it was a piece they split off over the road (3) -- for the survey of what the chase
    // camera sees of each kind of thing (a ray cast through the cells' boxes: the temple before its shaped things were made models); then
    // the floor cells the build lays again where the cuts left pits (TempleIsland.Carve), each a row of its own (cut 0, fill 1)
    public static int Cells(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var layout = TempleLayout.Build(game);
        var plan = RaceTrackPlan.Load(args[2]);
        var shapes = TempleShapes.Find(game);
        var models = TempleModels.Find(game);
        var carved = TempleIsland.Carve(layout, shapes, models, plan.TempleCuts ?? Array.Empty<int[]>(), plan.TempleFloors, plan.TempleSkirts, plan.TempleWalls);
        var boxes = new Dictionary<int, (PolarTextures.Box Box, double Drawn)>();
        using var csv = new StreamWriter(args[3]);
        csv.WriteLine("x,y,z,scene,block,brick,shape,round,model,u0,u1,v0,v1,drawn,cut,fill");
        void Row((int X, int Y, int Z) at, PolarLayout.Cell c, int gone, int fill)
        {
            if (!boxes.TryGetValue(c.Brick, out var b))
            {
                var s = PolarTextures.Sprite.Decode(game.ReadBrick(c.Brick));
                var drawn = Enum.GetValues<PolarTextures.Face>().Max(f => s.Coverage(f));
                boxes[c.Brick] = b = (drawn >= TempleObjects.Solid ? PolarTextures.Box.Full : s.Footprint(), drawn);
            }
            var round = shapes.TryGetValue(c.Brick, out var part) ? part.Shape.Kind.Name : "";
            var model = models.TryGetValue(c.Brick, out var modelled) ? modelled.Model.Kind.Name : "";
            csv.WriteLine($"{at.X},{at.Y},{at.Z},{c.Scene},{c.Block},{c.Brick},{c.Shape},{round},{model},{b.Box.U0:0.###},{b.Box.U1:0.###},{b.Box.V0:0.###},{b.Box.V1:0.###},{b.Drawn:0.###},{gone},{fill}");
        }
        foreach (var (at, c) in layout.Cells.OrderBy(c => c.Key.Z).ThenBy(c => c.Key.X).ThenBy(c => c.Key.Y))
            Row(at, c, carved.Cut.Contains(at) ? 1 : carved.Remains.Contains(at) ? 2 : carved.Loose.Contains(at) ? 3 : 0, 0);
        foreach (var (at, c) in carved.Fills.OrderBy(c => c.Key.Z).ThenBy(c => c.Key.X).ThenBy(c => c.Key.Y)) Row(at, c, 0, 1);
        foreach (var (at, c) in carved.Skirts.OrderBy(c => c.Key.Z).ThenBy(c => c.Key.X).ThenBy(c => c.Key.Y)) Row(at, c, 0, 2);
        foreach (var line in carved.Log) Console.WriteLine(line);
        Console.WriteLine($"{args[3]}: {layout.Cells.Count} cells ({carved.Cut.Count(layout.Cells.ContainsKey)} cut, {carved.Remains.Count} hanging, {carved.Loose.Count} split off), " +
                          $"{carved.Fills.Count} floors laid again, {carved.Skirts.Count} cells of trench walls; layers from {layout.MinY}");
        return 0;
    }

    // templeshapes <LBA1 folder> <out folder> [scale]: the round things TempleShapes turns (barrels, columns ...) -- each one's profile, and
    // shape_<name>.png: LBA1's picture of it, then its turned shape drawn with its textures from LBA1's angle, and from low down (10
    // degrees) from three ways round, as a chase camera sees it
    public static int Shapes(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var outDir = args[2];
        var k = args.Length > 3 ? int.Parse(args[3]) : 3;
        Directory.CreateDirectory(outDir);
        var log = new List<string>();
        var parts = TempleShapes.Find(game, log);
        foreach (var l in log) Console.WriteLine(l);
        foreach (var shape in parts.Values.Select(p => p.Shape).Distinct())
        {
            Console.WriteLine($"{shape.Kind.Name}: rings " + string.Join(" ", shape.Rings.Select(r => $"{r.Y:0.00}:{r.R:0.00}")));
            var mine = parts.Values.Where(p => p.Shape == shape).ToList();
            // (each part's faces, bottoms too, with their tiles in LBA1's colours)
            var faces = mine.SelectMany(p => TempleShapes.CellFaces(p, bottom: true).Select(f => (Face: f, Tile: TempleShapes.Finished(f.Tile, TempleShapes.TileOf(f.Of, f.Tile)).Pixels))).ToList();
            foreach (var p in mine.OrderBy(p => p.J).ThenBy(p => p.I).ThenBy(p => p.K))
                Console.WriteLine($"  part ({p.I},{p.J},{p.K}): " + string.Join(", ", TempleShapes.Tiles(p).Select(t => $"{t.Tile} of ({t.Of.I},{t.Of.K}) {TempleShapes.TileOf(t.Of, t.Tile).Drawn:0.00} drawn")) +
                                  $", {TempleShapes.CellFaces(p, bottom: false).Count} faces");
            var views = new List<(int[] Px, int W, int H)> { (shape.Pixels, shape.Width, shape.Height) };
            foreach (var (azimuth, elevation) in new[] { (45.0, 30.0), (45.0, 10.0), (135.0, 10.0), (225.0, 10.0) })
                views.Add(Splat(shape, faces, azimuth, elevation));
            int w = views.Sum(v => v.W * k + 8), h = views.Max(v => v.H) * k;
            var px = new byte[w * h * 4];
            for (var i = 0; i < px.Length; i += 4) { px[i] = 60; px[i + 1] = 40; px[i + 2] = 40; px[i + 3] = 255; }
            var x0 = 0;
            foreach (var (v, vw, vh) in views)
            {
                for (var y = 0; y < vh * k; y++)
                    for (var x = 0; x < vw * k; x++)
                    {
                        var c = v[y / k * vw + x / k];
                        if (c < 0) continue;
                        var (r, g, b) = PolarTextures.Colours.Rgb(game.Palette, c);
                        var i = (y * w + x0 + x) * 4;
                        px[i] = (byte)b; px[i + 1] = (byte)g; px[i + 2] = (byte)r;
                    }
                x0 += vw * k + 8;
            }
            PngWriter.Write(Path.Combine(outDir, $"shape_{shape.Kind.Name.Replace(' ', '_')}.png"), px, w, h);
        }
        return 0;
    }

    // templemodels <LBA1 folder> <out folder> [scale]: the shaped things TempleModels makes (rocks, grilles, statues, stairs ...) -- what each
    // read from its picture, and model_<name>_<brick>.png: LBA1's picture of it, then the model drawn with its textures from LBA1's angle,
    // and from low down (10 degrees) from three ways round, as a chase camera sees it
    public static int Models(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var outDir = args[2];
        var k = args.Length > 3 ? int.Parse(args[3]) : 3;
        Directory.CreateDirectory(outDir);
        var log = new List<string>();
        var parts = TempleModels.Find(game, log);
        foreach (var l in log) Console.WriteLine(l);
        foreach (var model in parts.Values.Select(p => p.Model).Distinct())
        {
            var faces = model.Faces.Select(f => (f.Corners, f.Uv, model.Tiles[f.Tile].Pixels, model.Tiles[f.Tile].W, model.Tiles[f.Tile].H)).ToList();
            var views = new List<(int[] Px, int W, int H)> { (model.Picture.Pixels, model.Picture.Width, model.Picture.Height) };
            foreach (var (azimuth, elevation) in new[] { (45.0, 30.0), (45.0, 10.0), (135.0, 10.0), (225.0, 10.0), (315.0, 10.0) })
                views.Add(Splat(faces, azimuth, elevation));
            WriteViews(Path.Combine(outDir, $"model_{model.Kind.Name.Replace(' ', '_')}_{model.Kind.Brick}.png"), views, k, game.Palette);
        }
        return 0;
    }

    // Pictures side by side, `k` times as big, in LBA1's colours.
    private static void WriteViews(string path, List<(int[] Px, int W, int H)> views, int k, byte[] palette)
    {
        int w = views.Sum(v => v.W * k + 8), h = views.Max(v => v.H) * k;
        var px = new byte[w * h * 4];
        for (var i = 0; i < px.Length; i += 4) { px[i] = 60; px[i + 1] = 40; px[i + 2] = 40; px[i + 3] = 255; }
        var x0 = 0;
        foreach (var (v, vw, vh) in views)
        {
            for (var y = 0; y < vh * k; y++)
                for (var x = 0; x < vw * k; x++)
                {
                    var c = v[y / k * vw + x / k];
                    if (c < 0) continue;
                    var (r, g, b) = PolarTextures.Colours.Rgb(palette, c);
                    var i = (y * w + x0 + x) * 4;
                    px[i] = (byte)b; px[i + 1] = (byte)g; px[i + 2] = (byte)r;
                }
            x0 += vw * k + 8;
        }
        PngWriter.Write(path, px, w, h);
    }

    // A shape's faces drawn by points (a few to a texel) with a depth buffer, looking from `azimuth` round the upright (45: LBA1's, from +x +z)
    // and `elevation` down, at LBA1's scale (24 pixels to a cell along x - z from LBA1's angle).
    private static (int[] Px, int W, int H) Splat(TempleShapes.Shape shape, List<(TempleShapes.ShapeFace Face, int[] Tile)> faces, double azimuth, double elevation)
        => Splat(faces.Select(f => (f.Face.Corners, f.Face.Uv, f.Tile, TempleShapes.Size(f.Face.Tile).W, TempleShapes.Size(f.Face.Tile).H)).ToList(), azimuth, elevation);

    // Faces (corners in cells and layers, their places in their tiles, the tiles in LBA1's colours) drawn by points with a depth buffer.
    private static (int[] Px, int W, int H) Splat(List<(System.Numerics.Vector3[] Corners, (double U, double V)[] Uv, int[] Tile, int TW, int TH)> faces, double azimuth, double elevation)
    {
        double a = azimuth * Math.PI / 180, e = elevation * Math.PI / 180, k = 24 * Math.Sqrt(2) / 512;
        (double X, double Y, double D) Project(System.Numerics.Vector3 p)
        {
            double x = p.X * 512, y = p.Y * 256, z = p.Z * 512;
            var right = x * Math.Sin(a) - z * Math.Cos(a);
            var toward = x * Math.Cos(a) + z * Math.Sin(a);
            return (right * k, (toward * Math.Sin(e) - y * Math.Cos(e)) * k, toward * Math.Cos(e) + y * Math.Sin(e));
        }
        var all = faces.SelectMany(f => f.Corners.Select(Project)).ToList();
        if (all.Count == 0) return (new[] { -1 }, 1, 1);
        double minX = all.Min(p => p.X) - 4, minY = all.Min(p => p.Y) - 4;
        int w = (int)Math.Ceiling(all.Max(p => p.X) - minX + 4), h = (int)Math.Ceiling(all.Max(p => p.Y) - minY + 4);
        var px = new int[w * h]; var depth = new double[w * h];
        Array.Fill(px, -1); Array.Fill(depth, double.MinValue);
        foreach (var (c, uv, tile, tw, th) in faces)
        {
            const int n = 48;
            for (var i = 0; i <= n; i++)
                for (var j = 0; j <= n; j++)
                {
                    double s = (double)i / n, t = (double)j / n;
                    System.Numerics.Vector3 p; double u, v;
                    if (c.Length == 4)
                    {
                        p = System.Numerics.Vector3.Lerp(System.Numerics.Vector3.Lerp(c[0], c[1], (float)s), System.Numerics.Vector3.Lerp(c[3], c[2], (float)s), (float)t);
                        u = (uv[0].U * (1 - s) + uv[1].U * s) * (1 - t) + (uv[3].U * (1 - s) + uv[2].U * s) * t;
                        v = (uv[0].V * (1 - s) + uv[1].V * s) * (1 - t) + (uv[3].V * (1 - s) + uv[2].V * s) * t;
                    }
                    else
                    {
                        if (s + t > 1) continue;
                        p = c[0] + (c[1] - c[0]) * (float)s + (c[2] - c[0]) * (float)t;
                        u = uv[0].U + (uv[1].U - uv[0].U) * s + (uv[2].U - uv[0].U) * t;
                        v = uv[0].V + (uv[1].V - uv[0].V) * s + (uv[2].V - uv[0].V) * t;
                    }
                    var col = tile[Math.Clamp((int)(v * th), 0, th - 1) * tw + Math.Clamp((int)(u * tw), 0, tw - 1)];
                    if (col < 0) continue;
                    var (sx, sy, d) = Project(p);
                    int x = (int)(sx - minX), y = (int)(sy - minY);
                    if (x < 0 || y < 0 || x >= w || y >= h || d <= depth[y * w + x]) continue;
                    depth[y * w + x] = d; px[y * w + x] = col;
                }
        }
        return (px, w, h);
    }

    public static int BuildInto(string[] args)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var line in TempleIsland.Add(args[1], args[2])) Console.WriteLine(line);
        Console.WriteLine($"built in {watch.Elapsed.TotalSeconds:0.0} s");
        return 0;
    }

    public static int Colours(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var layout = TempleLayout.Build(game);
        foreach (var l in layout.Log) Console.WriteLine(l);
        var placed = layout.Cells.Values.GroupBy(c => c.Brick).ToDictionary(g => g.Key, g => g.Count());
        var counts = new long[256];
        foreach (var (brick, n) in placed)
            foreach (var px in PolarTextures.Sprite.Decode(game.ReadBrick(brick)).Pixels)
                if (px >= 0) counts[px] += n;
        var used = Enumerable.Range(0, 256).Where(c => counts[c] > 0).OrderByDescending(c => counts[c]).ToList();
        var total = counts.Sum();
        Console.WriteLine($"{placed.Count} bricks, {used.Count} colours, {total} pixels");
        Console.WriteLine("ramps used (16 each): " + string.Join(" ", Enumerable.Range(0, 16).Where(r => used.Any(c => c / 16 == r))
            .Select(r => $"{r * 16}:{used.Count(c => c / 16 == r)}/{100.0 * Enumerable.Range(r * 16, 16).Sum(c => counts[c]) / total:0.0}%")));
        var cumulative = 0L; var k = 0;
        foreach (var c in used)
        {
            cumulative += counts[c]; k++;
            if (k is 16 or 32 or 48 or 64 or 96 or 128) Console.WriteLine($"  top {k} colours: {100.0 * cumulative / total:0.00}% of pixels");
        }
        foreach (var entry in new[] { 27, 30, 31, 35, 42 })
        {
            var pal = IslandMapRenderer.LoadPaletteEntry(args[2], entry);
            double sum = 0, worst = 0; var bad = 0L;
            foreach (var c in used)
            {
                var (r, g, b) = PolarTextures.Colours.Rgb(game.Palette, c);
                var best = double.MaxValue;
                for (var p = 16; p <= 239; p++)
                {
                    var (pr, pg, pb) = PolarTextures.Colours.Rgb(pal, p);
                    best = Math.Min(best, Math.Sqrt((r - pr) * (r - pr) + (g - pg) * (g - pg) + (b - pb) * (b - pb)));
                }
                sum += best * counts[c]; worst = Math.Max(worst, best);
                if (best > 12) bad += counts[c];
            }
            Console.WriteLine($"palette {entry}: mean distance {sum / total:0.0}, worst {worst:0.0}, {100.0 * bad / total:0.0}% of pixels further than 12");
        }
        return 0;
    }

    public static int Region(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var layout = TempleLayout.Build(game);
        var cells = layout.Cells.AsEnumerable();
        if (args.Length >= 7)
        {
            int x0 = int.Parse(args[3]), x1 = int.Parse(args[4]), z0 = int.Parse(args[5]), z1 = int.Parse(args[6]);
            cells = cells.Where(c => c.Key.X >= x0 && c.Key.X <= x1 && c.Key.Z >= z0 && c.Key.Z <= z1);
        }
        var list = cells.Select(c => new Lba1Placement(c.Key.X, c.Key.Y, c.Key.Z, c.Value.Brick)).ToList();
        var image = Lba1GridRenderer.Render(new[] { new Lba1Tile(list, 0, 0, 0) }, game.ReadBrick, game.Palette);
        PngWriter.Write(args[2], image.Bgra, image.Width, image.Height);
        Console.WriteLine($"{args[2]}: {list.Count} cells, {image.Width} x {image.Height}");
        return 0;
    }

    public static int Map(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var layout = TempleLayout.Build(game);
        var k = args.Length > 3 ? int.Parse(args[3]) : 4;
        int w = (layout.MaxX - layout.MinX + 1) * k, h = (layout.MaxZ - layout.MinZ + 1) * k;
        var px = new byte[w * h * 4];
        var tops = new Dictionary<(int, int), (int Y, PolarLayout.Cell Cell)>();
        foreach (var ((x, y, z), c) in layout.Cells)
            if (!tops.TryGetValue((x, z), out var t) || y > t.Y) tops[(x, z)] = (y, c);
        foreach (var ((x, z), (y, cell)) in tops)
        {
            // (a heat map of the top layer: blue low, through green, to red high; the brick's own colour with HEAT=0)
            var t = (y - layout.MinY) / (double)Math.Max(1, layout.MaxY - layout.MinY);
            var (r, g, b) = Environment.GetEnvironmentVariable("HEAT") == "0" ? PolarLayout.BrickColour(game, cell.Brick)
                : (255 * Math.Clamp(2 * t - 0.6, 0, 1), 255 * (1 - Math.Abs(2 * t - 1) * 0.9), 255 * Math.Clamp(1 - 2 * t, 0, 1));
            var shade = 1.0;
            for (var dz = 0; dz < k; dz++)
                for (var dx = 0; dx < k; dx++)
                {
                    var i = (((z - layout.MinZ) * k + dz) * w + (x - layout.MinX) * k + dx) * 4;
                    var edge = dx == 0 || dz == 0;
                    px[i] = (byte)Math.Min(255, b * shade * (edge ? 0.8 : 1)); px[i + 1] = (byte)Math.Min(255, g * shade * (edge ? 0.8 : 1));
                    px[i + 2] = (byte)Math.Min(255, r * shade * (edge ? 0.8 : 1)); px[i + 3] = 255;
                }
        }
        PngWriter.Write(args[2], px, w, h);
        Console.WriteLine($"{args[2]}: {w} x {h} (x {layout.MinX}.. across, z {layout.MinZ}.. down, {k} px a cell)");
        return 0;
    }
}
