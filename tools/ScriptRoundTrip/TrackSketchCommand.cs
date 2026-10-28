using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// trackmap <game> <island file name, e.g. DESERT> <out.png> [scale]: the island's map as the track generator shows it (its original file,
// before a race track build, when the folder has one).
// sketchtest <game> <island name> <out folder> [jump a b] [loop a b] [pit a b side] [reverse]: a drawing made as a user would make it -- the
// island's built-in lap drawn on its map in green a pen's width wide, its points a..b pink (a jump), blue (a loop), an orange stroke beside
// points a..b `side` cells off (the pit lane) -- read back as the generator reads a picture (TrackSketch.FromImage), followed
// (TrackSketchTracer) and written as a plan, sketch.png (the drawing) and traced.png (the route found, its start, jumps and loops).
internal static class TrackSketchCommand
{
    public const int MapScale = 3;

    public static (IslandFile Island, IslandMapRenderer Map) Map(string game, string ile, int scale)
    {
        var path = Path.Combine(game, ile + ".ILE");
        var original = path + RaceTrackService.BackupSuffix;
        var island = IslandFile.Load(File.Exists(original) ? original : path);
        var renderer = new IslandMapRenderer(island, IslandMapRenderer.LoadPalette(game, ile), scale);
        renderer.RenderAll(MapView.Terrain);
        return (island, renderer);
    }

    public static int MapCommand(string[] args)
    {
        var (_, map) = Map(args[1], args[2].ToUpperInvariant(), args.Length > 4 ? int.Parse(args[4]) : MapScale);
        PngWriter.Write(args[3], map.Pixels, map.PixelWidth, map.PixelHeight);
        Console.WriteLine($"{args[3]}: {map.PixelWidth} x {map.PixelHeight}, {map.Scale} pixels a cell, cell ({map.OriginX}, {map.OriginZ}) top left");
        return 0;
    }

    // sketchimage <game> <ILE name> <picture.bgra> <width> <height> <out folder>: a picture drawn on the island's map (raw BGRA -- ffmpeg -i
    // x.png -f rawvideo -pix_fmt bgra; any size: taken to the map's) traced as the generator traces it -- traced.png and the notes
    public static int Image(string[] args)
    {
        var (island, map) = Map(args[1], args[2].ToUpperInvariant(), MapScale);
        var pic = File.ReadAllBytes(args[3]); int pw = int.Parse(args[4]), ph = int.Parse(args[5]);
        var sketch = TrackSketch.FromImage(pic, pw, ph, map.Pixels, map.PixelWidth, map.PixelHeight, MapScale, map.OriginX, map.OriginZ);
        Console.WriteLine($"{sketch.Kinds.Count(k => k != SketchKind.None)} pixels drawn");
        var result = TrackSketchTracer.Trace(sketch, island);
        foreach (var n in result.Notes) Console.WriteLine("  " + n);
        foreach (var p in result.Problems) Console.WriteLine("  PROBLEM: " + p);
        Directory.CreateDirectory(args[6]);
        var traced = (byte[])map.Pixels.Clone();
        int w = map.PixelWidth, h = map.PixelHeight;
        for (var i = 0; i < w * h; i++) if (sketch.Kinds[i] != SketchKind.None) { traced[i * 4] = 60; traced[i * 4 + 1] = 160; traced[i * 4 + 2] = 60; }
        for (var i = 0; i < result.Route.Count; i++)
        {
            var (cx, cz) = result.Route[i];
            int x = (int)((cx - map.OriginX) * MapScale), y = (int)((cz - map.OriginZ) * MapScale);
            var rad = i == result.Start ? 3 : 1;
            for (var yy = y - rad; yy <= y + rad; yy++)
                for (var xx = x - rad; xx <= x + rad; xx++)
                    if (xx >= 0 && yy >= 0 && xx < w && yy < h) { var o = (yy * w + xx) * 4; traced[o] = 0; traced[o + 1] = 230; traced[o + 2] = 255; }
        }
        PngWriter.Write(Path.Combine(args[6], "traced.png"), traced, w, h);
        return result.Plan is null ? 1 : 0;
    }

    public static int Test(string[] args)
    {
        var game = args[1];
        var where = RaceTrackIsland.ByName(args[2]);
        var outDir = args[3];
        Directory.CreateDirectory(outDir);
        int Arg(string key, int k) { var i = Array.IndexOf(args, key); return i > 0 && i + k < args.Length ? int.Parse(args[i + k]) : -1; }
        var (island, map) = Map(game, Path.GetFileNameWithoutExtension(where.IleFile), MapScale);
        var plan = RaceTrackPlan.Built(where);
        var pts = plan.Points.Select(p => ((p[0] + plan.OriginCellX - map.OriginX) * MapScale, (p[1] + plan.OriginCellZ - map.OriginZ) * MapScale)).ToList();
        var picture = (byte[])map.Pixels.Clone();
        int w = map.PixelWidth, h = map.PixelHeight;
        void Stroke(IEnumerable<(double X, double Y)> line, SketchKind kind, double radius)
        {
            var (r, g, b) = TrackSketch.Colour(kind);
            var list = line.ToList();
            for (var i = 0; i + 1 < list.Count; i++)
            {
                var (ax, ay) = list[i]; var (bx, by) = list[i + 1];
                var n = (int)Math.Ceiling(Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay))) + 1;
                for (var s = 0; s <= n; s++)
                {
                    double x = ax + (bx - ax) * s / n, y = ay + (by - ay) * s / n;
                    for (var yy = (int)(y - radius); yy <= (int)(y + radius); yy++)
                        for (var xx = (int)(x - radius); xx <= (int)(x + radius); xx++)
                        {
                            if (xx < 0 || yy < 0 || xx >= w || yy >= h || (xx - x) * (xx - x) + (yy - y) * (yy - y) > radius * radius) continue;
                            var o = (yy * w + xx) * 4; picture[o] = b; picture[o + 1] = g; picture[o + 2] = r; picture[o + 3] = 255;
                        }
                }
            }
        }
        Stroke(pts.Append(pts[0]), SketchKind.Road, 4);
        if (Arg("jump", 1) >= 0) Stroke(pts.Skip(Arg("jump", 1)).Take(Arg("jump", 2) - Arg("jump", 1)), SketchKind.Jump, 4);
        if (Arg("loop", 1) >= 0) Stroke(pts.Skip(Arg("loop", 1)).Take(Arg("loop", 2) - Arg("loop", 1)), SketchKind.Loop, 4);
        if (Arg("pit", 1) >= 0)
        {
            int a = Arg("pit", 1), b = Arg("pit", 2), side = Arg("pit", 3);
            var pit = new List<(double X, double Y)>();
            for (var i = a; i < b; i++)
            {
                var (x0, y0) = pts[i]; var (x1, y1) = pts[Math.Min(pts.Count - 1, i + 1)];
                double dx = x1 - x0, dy = y1 - y0, l = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy));
                pit.Add((x0 - dy / l * side * MapScale, y0 + dx / l * side * MapScale));
            }
            Stroke(pit, SketchKind.Pit, 3);
        }
        PngWriter.Write(Path.Combine(outDir, "sketch.png"), picture, w, h);

        var sketch = TrackSketch.FromImage(picture, w, h, map.Pixels, w, h, MapScale, map.OriginX, map.OriginZ);
        var result = TrackSketchTracer.Trace(sketch, island, args.Contains("reverse"));
        foreach (var n in result.Notes) Console.WriteLine("  " + n);
        foreach (var p in result.Problems) Console.WriteLine("  PROBLEM: " + p);
        if (result.Plan is { } made)
        {
            File.WriteAllText(Path.Combine(outDir, "plan.json"), System.Text.Json.JsonSerializer.Serialize(made,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault }));
            Console.WriteLine($"{Path.Combine(outDir, "plan.json")}: jumps {string.Join(" ", (made.GapJumps ?? Array.Empty<int[]>()).Select(j => $"[{j[0]},{j[1]}]"))} loops {string.Join(" ", (made.Loops ?? Array.Empty<double[]>()).Select(l => l[0]))}");
        }
        // the route found, over the map: yellow, its jumps magenta, its loops cyan, its start white
        var traced = (byte[])map.Pixels.Clone();
        for (var i = 0; i < result.Route.Count; i++)
        {
            var (cx, cz) = result.Route[i];
            int x = (int)((cx - map.OriginX) * MapScale), y = (int)((cz - map.OriginZ) * MapScale);
            var (r, g, b) = i == result.Start ? ((byte)255, (byte)255, (byte)255) : result.RouteKinds[i] switch
            {
                SketchKind.Jump => ((byte)255, (byte)0, (byte)255), SketchKind.Loop => ((byte)0, (byte)255, (byte)255), _ => ((byte)255, (byte)230, (byte)0),
            };
            var rad = i == result.Start ? 4 : 1;
            for (var yy = y - rad; yy <= y + rad; yy++)
                for (var xx = x - rad; xx <= x + rad; xx++)
                    if (xx >= 0 && yy >= 0 && xx < w && yy < h) { var o = (yy * w + xx) * 4; traced[o] = b; traced[o + 1] = g; traced[o + 2] = r; }
        }
        PngWriter.Write(Path.Combine(outDir, "traced.png"), traced, w, h);
        return result.Plan is null ? 1 : 0;
    }
}
