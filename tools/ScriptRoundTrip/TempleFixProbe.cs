using System.Numerics;
using LBAAssembler;
using LBAAssembler.Lba1;
using LBAAssembler.Terrain.Polar;
using LBAAssembler.Terrain.Temple;

namespace ScriptRoundTrip;

// MEASURING PROBE for the Temple's shaped and round things (not product code):
//   fixbricks <LBA1> <brick...>: each brick's sprite (hot spot, footprint, how much of each face it draws) and its pixels as text
//   fixplace <LBA1>: the placements the layout makes of each model and round thing -- whole, partial (cells of the block the layout leaves
//     empty), and those of a brick its block has at more than one place
//   fixfit <LBA1> <out dir> [scale]: each model and round thing drawn from LBA1's angle exactly as LBA1 projects (orthographic, 24 px a
//     cell across, 15 a layer up) against its block's picture: the picture's pixels it leaves void, the pixels it draws where the picture
//     has none, the mean colour distance where both draw; fit_<name>.png: the picture | the model | the difference (magenta void, cyan
//     extra), then the model from low down three ways round
internal static class TempleFixProbe
{
    public static int Bricks(string[] args)
    {
        var game = new Lba1Game(args[1]);
        foreach (var a in args.Skip(2))
        {
            var brick = int.Parse(a);
            var s = PolarTextures.Sprite.Decode(game.ReadBrick(brick));
            var box = s.Footprint();
            var fit = PolarTextures.Sprite.LastFit;
            Console.WriteLine($"brick {brick}: sprite {s.Width}x{s.Lines} hot ({s.HotX},{s.HotY}) box {box.U0:0.00}-{box.U1:0.00} x {box.V0:0.00}-{box.V1:0.00} " +
                              $"(full {fit.Full:0.00} best {fit.Best:0.00}) drawn top {s.Coverage(PolarTextures.Face.Top):0.00} +x {s.Coverage(PolarTextures.Face.SideX):0.00} +z {s.Coverage(PolarTextures.Face.SideZ):0.00}");
            for (var y = 0; y < s.Lines; y++)
                Console.WriteLine($"  {y + s.HotY,3} " + new string(Enumerable.Range(0, s.Width).Select(x => s.Pixels[y * s.Width + x] >= 0 ? '#' : '.').ToArray()));
        }
        return 0;
    }

    public static int Place(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var layout = TempleLayout.Build(game);
        var cells = layout.Cells;
        var models = TempleModels.Find(game);
        var placed = TempleModels.Placed(models, cells);
        // (each placement: its model and anchor, the faces it draws)
        var byPlacement = placed.SelectMany(p => p.Value.Select(f => (f.Model, f.Anchor, Cell: p.Key))).GroupBy(f => (f.Model, f.Anchor)).ToList();
        foreach (var g in byPlacement.GroupBy(p => p.Key.Model.Kind.Name + " " + p.Key.Model.Kind.Brick).OrderBy(g => g.Key))
        {
            var model = g.First().Key.Model;
            var partial = 0; var cellsMissing = 0;
            foreach (var p in g)
            {
                var a = p.Key.Anchor;
                var missing = model.Bricked.Count(b => !cells.ContainsKey(((int)a.X + b.I, (int)a.Y + b.J, (int)a.Z + b.K)));
                if (missing > 0) { partial++; cellsMissing += missing; }
            }
            Console.WriteLine($"{g.Key}: {g.Count()} placements drawn, {partial} partial ({cellsMissing} of their block's cells empty in the layout), {g.Sum(p => p.Count())} faces");
        }
        // (the cells of a brick its block has at more than one place)
        foreach (var (brick, part) in models)
        {
            var places = part.Model.Places(brick);
            if (places.Count < 2) continue;
            var at = cells.Where(c => c.Value.Brick == brick).Select(c => c.Key).ToList();
            Console.WriteLine($"brick {brick} ({part.Model.Kind.Name}) at {places.Count} places of its block: {string.Join(" ", places)}; {at.Count} cells hold it, " +
                              string.Join(" ", places.Select(q => $"{q}: {at.Count(c => TempleModels.PartAt(models, cells, c) is var r && (r.I, r.J, r.K) == q)}")));
        }
        return 0;
    }

    // fixpalette <LBA1> <LBA2 game>: the temple's colours that got no slot of their own, and the free slots nearest them
    public static int Palette(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var layout = TempleLayout.Build(game);
        var counts = TempleObjects.ColourCounts(game, layout);
        var src = HqrArchive.Open(Path.Combine(args[2], "RESS.HQR")).Read(TemplePalette.Source);
        var r = TemplePalette.Build(src, game.Palette, counts);
        foreach (var l in r.Log) Console.WriteLine(l);
        var srcPal = src.AsSpan(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(src.AsSpan(4)), 768).ToArray();
        var taken = new HashSet<int>(Enumerable.Range(0, 256).Where(c => counts[c] > 0).Select(c => r.Map[c]));
        static double D((int R, int G, int B) a, int r, int g, int b) => Math.Sqrt(((a.R - r) * (a.R - r) * 3 + (a.G - g) * (a.G - g) * 4 + (a.B - b) * (a.B - b) * 2) / 3.0);
        foreach (var c in Enumerable.Range(0, 256).Where(c => counts[c] > 0).OrderByDescending(c => counts[c]))
        {
            var rgb = PolarTextures.Colours.Rgb(game.Palette, c);
            var m = r.Map[c];
            var exact = r.Palette[m * 3] == rgb.R && r.Palette[m * 3 + 1] == rgb.G && r.Palette[m * 3 + 2] == rgb.B;
            if (exact) continue;
            var free = Enumerable.Range(16, 224).Where(p => !taken.Contains(p)).Select(p => (P: p, D: D(rgb, srcPal[p * 3], srcPal[p * 3 + 1], srcPal[p * 3 + 2]))).OrderBy(t => t.D).Take(3).ToList();
            Console.WriteLine($"LBA1 colour {c} {rgb}: {counts[c]} px -> slot {m} ({r.Palette[m * 3]},{r.Palette[m * 3 + 1]},{r.Palette[m * 3 + 2]}), distance {D(rgb, r.Palette[m * 3], r.Palette[m * 3 + 1], r.Palette[m * 3 + 2]):0.0}; " +
                              "nearest free slots " + string.Join(", ", free.Select(f => $"{f.P} ({srcPal[f.P * 3]},{srcPal[f.P * 3 + 1]},{srcPal[f.P * 3 + 2]}) at {f.D:0.0}")));
        }
        return 0;
    }

    // ---- the fit from LBA1's angle

    private sealed class Canvas
    {
        public readonly int W, H, Ox, Oy;
        public readonly int[] Px; public readonly double[] Depth;
        public Canvas(int w, int h, int ox, int oy) { W = w; H = h; Ox = ox; Oy = oy; Px = new int[w * h]; Depth = new double[w * h]; Array.Fill(Px, -1); Array.Fill(Depth, double.MinValue); }

        // A face (corners in cells and layers, uv, its tile) drawn with LBA1's projection (screen = picture's), nearest wins.
        public void Draw(Vector3[] c, (double U, double V)[] uv, int[] tile, int tw, int th, Func<Vector3, (double X, double Y)> screen)
        {
            for (var t = 1; t + 1 < c.Length; t++) Tri(new[] { c[0], c[t], c[t + 1] }, new[] { uv[0], uv[t], uv[t + 1] }, tile, tw, th, screen);
        }

        private void Tri(Vector3[] p, (double U, double V)[] uv, int[] tile, int tw, int th, Func<Vector3, (double X, double Y)> screen)
        {
            var s = p.Select(screen).ToArray();
            var d = p.Select(q => (q.X + q.Z) * 512 * 0.6124 + q.Y * 256 * 0.5).ToArray();
            double x0 = s.Min(q => q.X), x1 = s.Max(q => q.X), y0 = s.Min(q => q.Y), y1 = s.Max(q => q.Y);
            var area = (s[1].X - s[0].X) * (s[2].Y - s[0].Y) - (s[2].X - s[0].X) * (s[1].Y - s[0].Y);
            if (Math.Abs(area) < 1e-9) return;
            for (var y = (int)Math.Floor(y0); y <= (int)Math.Ceiling(y1); y++)
                for (var x = (int)Math.Floor(x0); x <= (int)Math.Ceiling(x1); x++)
                {
                    double px = x + 0.5, py = y + 0.5;
                    var w1 = ((px - s[0].X) * (s[2].Y - s[0].Y) - (s[2].X - s[0].X) * (py - s[0].Y)) / area;
                    var w2 = ((s[1].X - s[0].X) * (py - s[0].Y) - (px - s[0].X) * (s[1].Y - s[0].Y)) / area;
                    var w0 = 1 - w1 - w2;
                    if (w0 < -1e-6 || w1 < -1e-6 || w2 < -1e-6) continue;
                    int X = x + Ox, Y = y + Oy;
                    if (X < 0 || Y < 0 || X >= W || Y >= H) continue;
                    var depth = w0 * d[0] + w1 * d[1] + w2 * d[2];
                    if (depth <= Depth[Y * W + X]) continue;
                    double u = w0 * uv[0].U + w1 * uv[1].U + w2 * uv[2].U, v = w0 * uv[0].V + w1 * uv[1].V + w2 * uv[2].V;
                    var col = tile[Math.Clamp((int)(v * th), 0, th - 1) * tw + Math.Clamp((int)(u * tw), 0, tw - 1)];
                    if (col < 0) continue;
                    Depth[Y * W + X] = depth; Px[Y * W + X] = col;
                }
        }
    }

    public static int Fit(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var outDir = args[2];
        var k = args.Length > 3 ? int.Parse(args[3]) : 3;
        Directory.CreateDirectory(outDir);
        var pal = game.Palette;
        (int, int, int) Rgb(int c) => PolarTextures.Colours.Rgb(pal, c);
        var rows = new List<string>();
        void Measure(string name, int[] pic, int w, int h, Func<Vector3, (double X, double Y)> screen, List<(Vector3[] C, (double U, double V)[] Uv, int[] Tile, int TW, int TH)> faces,
            List<(int I, int J, int K, int Brick)>? cellsOf, Func<int, int, int>? cellAt)
        {
            var canvas = new Canvas(w, h, 0, 0);
            foreach (var f in faces) canvas.Draw(f.C, f.Uv, f.Tile, f.TW, f.TH, screen);
            int drawn = 0, voids = 0, extra = 0, both = 0; double dist = 0;
            var perCell = new Dictionary<int, (int Drawn, int Void)>();
            var diff = new int[w * h];
            for (var i = 0; i < w * h; i++)
            {
                bool a = pic[i] >= 0, b = canvas.Px[i] >= 0;
                if (a) drawn++;
                if (a && !b) { voids++; diff[i] = 1; }
                if (!a && b) { extra++; diff[i] = 2; }
                if (a && b)
                {
                    both++;
                    var (r1, g1, b1) = Rgb(pic[i]); var (r2, g2, b2) = Rgb(canvas.Px[i]);
                    dist += Math.Sqrt((r1 - r2) * (r1 - r2) + (g1 - g2) * (g1 - g2) + (b1 - b2) * (b1 - b2));
                }
                if (a && cellAt is not null)
                {
                    var cell = cellAt(i % w, i / w);
                    var (dd, vv) = perCell.GetValueOrDefault(cell);
                    perCell[cell] = (dd + 1, vv + (b ? 0 : 1));
                }
            }
            var line = $"{name}: picture {drawn} px, void {voids} ({100.0 * voids / Math.Max(1, drawn):0.0} %), extra {extra} ({100.0 * extra / Math.Max(1, drawn):0.0} %), mean colour distance {dist / Math.Max(1, both):0.0}, {faces.Count} faces";
            Console.WriteLine(line); rows.Add(line);
            if (cellsOf is not null)
                foreach (var (cell, (dd, vv)) in perCell.OrderBy(c => c.Key))
                    if (cell >= 0 && dd > 0) Console.WriteLine($"    brick {cellsOf[cell].Brick} ({cellsOf[cell].I},{cellsOf[cell].J},{cellsOf[cell].K}): {dd} px, void {vv} ({100.0 * vv / dd:0.0} %)");
            // the sheet: picture | model | difference
            var views = new List<(int[] Px, int W, int H)> { (pic, w, h), (canvas.Px, w, h) };
            var dpx = new int[w * h];
            for (var i = 0; i < w * h; i++) dpx[i] = diff[i] == 1 ? -2 : diff[i] == 2 ? -3 : pic[i];
            views.Add((dpx, w, h));
            WriteSheet(Path.Combine(outDir, $"fit_{name.Replace(' ', '_')}.png"), views, k, pal);
        }

        // ---- the models (and those of what LBA1 placed of a block where the layout leaves some of its cells empty)
        var models = TempleModels.Find(game);
        var all = models.Values.Select(p => p.Model).Distinct().ToList();
        var layout = TempleLayout.Build(game);
        var variants = TempleModels.Placed(models, layout.Cells).SelectMany(c => c.Value.Select(f => f.Model)).Distinct().Where(v => !all.Contains(v)).ToList();
        var variantNames = new Dictionary<TempleModels.Model, string>();
        foreach (var v in variants)
            variantNames[v] = $"{v.Kind.Brick} part{variantNames.Count(n => n.Key.Kind == v.Kind)} ({v.Reading.Split(" of ")[0]})";
        Console.WriteLine($"{variants.Count} models of parts of blocks: " + string.Join(", ", variants.Select(v => $"{v.Kind.Name} {variantNames[v]}")));
        foreach (var model in all.Concat(variants))
        {
            var p = model.Picture;
            var bricks = model.Bricks;
            // (a model of some layers of a block: against the picture of those layers' bricks)
            if (variantNames.ContainsKey(model) && System.Text.RegularExpressions.Regex.Match(model.Reading, @"^layers (\d+) to (\d+)") is { Success: true } lm)
            {
                int lo = int.Parse(lm.Groups[1].Value), hi = int.Parse(lm.Groups[2].Value);
                bricks = model.Bricks.Where(b => b.J >= lo && b.J < hi).ToList();
                p = TempleModels.Picture.Draw(game, bricks);
            }
            var faces = model.Faces.Select(f => (f.Corners, f.Uv, model.Tiles[f.Tile].Pixels, model.Tiles[f.Tile].W, model.Tiles[f.Tile].H)).ToList();
            // (which brick drew each pixel of the picture: drawn again, far to near)
            var owner = new int[p.Width * p.Height]; Array.Fill(owner, -1);
            var order = bricks.Select((b, i) => (b, i)).OrderBy(t => t.b.K).ThenBy(t => t.b.I).ThenBy(t => t.b.J).ToList();
            foreach (var (b, i) in order)
            {
                var s = PolarTextures.Sprite.Decode(game.ReadBrick(b.Brick));
                int left = 24 * (b.I - b.K) + s.HotX + p.OriginX, top = 12 * (b.I + b.K) - 15 * b.J + s.HotY + p.OriginY;
                for (var y = 0; y < s.Lines; y++) for (var x = 0; x < s.Width; x++) if (s.Pixels[y * s.Width + x] >= 0) owner[(top + y) * p.Width + left + x] = i;
            }
            Measure(variantNames.TryGetValue(model, out var vn) ? $"{model.Kind.Name} {vn.Split(' ')[0]} {vn.Split(' ')[1]}" : $"{model.Kind.Name} {model.Kind.Brick}", p.Pixels, p.Width, p.Height, q => p.Screen(q), faces,
                bricks.Select(b => (b.I, b.J, b.K, b.Brick)).ToList(), (x, y) => owner[y * p.Width + x]);
        }
        // ---- the round things
        var shapes = TempleShapes.Find(game);
        foreach (var shape in shapes.Values.Select(p => p.Shape).Distinct())
        {
            var faces = shapes.Values.Where(p => p.Shape == shape).Distinct().SelectMany(p => TempleShapes.CellFaces(p, bottom: true))
                .Select(f => (f.Corners, f.Uv, TempleShapes.Finished(f.Tile, TempleShapes.TileOf(f.Of, f.Tile)).Pixels, TempleShapes.Size(f.Tile).W, TempleShapes.Size(f.Tile).H)).ToList();
            Measure(shape.Kind.Name, shape.Pixels, shape.Width, shape.Height, q => shape.Screen(q.X, q.Y, q.Z), faces, null, null);
            Rims(shape, faces, Rgb);
        }
        File.WriteAllLines(Path.Combine(outDir, "fit.txt"), rows);
        return 0;
    }

    // A round thing's rims from LBA1's angle: per row of its body, the outer 30 % of the drawn run each side against the middle 40 %:
    // the mean brightness (port and LBA1) and the mean colour distance there.
    private static void Rims(TempleShapes.Shape shape, List<(Vector3[] C, (double U, double V)[] Uv, int[] Tile, int TW, int TH)> faces, Func<int, (int, int, int)> rgb)
    {
        var canvas = new Canvas(shape.Width, shape.Height, 0, 0);
        foreach (var f in faces) canvas.Draw(f.C, f.Uv, f.Tile, f.TW, f.TH, q => shape.Screen(q.X, q.Y, q.Z));
        double Lum(int c) { var (r, g, b) = rgb(c); return (r + g + b) / 3.0; }
        double Dist(int a, int b) { var (r1, g1, b1) = rgb(a); var (r2, g2, b2) = rgb(b); return Math.Sqrt((r1 - r2) * (r1 - r2) + (g1 - g2) * (g1 - g2) + (b1 - b2) * (b1 - b2)); }
        double rimL = 0, rimP = 0, rimD = 0, midL = 0, midP = 0, midD = 0; int nr = 0, nm = 0;
        for (var y = 0; y < shape.Height; y++)
        {
            var xs = Enumerable.Range(0, shape.Width).Where(x => shape.At(x, y) >= 0).ToList();
            if (xs.Count < 10) continue;
            int x0 = xs.Min(), x1 = xs.Max(); var n = x1 - x0 + 1;
            for (var x = x0; x <= x1; x++)
            {
                int a = shape.At(x, y), b = canvas.Px[y * shape.Width + x];
                if (a < 0 || b < 0) continue;
                var t = (x - x0 + 0.5) / n;
                if (t < 0.3 || t > 0.7 && t >= 0.7) { if (t < 0.3 || t > 0.7) { rimL += Lum(a); rimP += Lum(b); rimD += Dist(a, b); nr++; } }
                if (t >= 0.3 && t <= 0.7) { midL += Lum(a); midP += Lum(b); midD += Dist(a, b); nm++; }
            }
        }
        Console.WriteLine($"    rims (outer 30 % each side): LBA1 brightness {rimL / Math.Max(1, nr):0.0}, port {rimP / Math.Max(1, nr):0.0} ({(rimP - rimL) / Math.Max(1, nr):+0.0;-0.0}), colour distance {rimD / Math.Max(1, nr):0.0}; " +
                          $"middle 40 %: LBA1 {midL / Math.Max(1, nm):0.0}, port {midP / Math.Max(1, nm):0.0} ({(midP - midL) / Math.Max(1, nm):+0.0;-0.0}), distance {midD / Math.Max(1, nm):0.0}");
    }

    private static void WriteSheet(string path, List<(int[] Px, int W, int H)> views, int k, byte[] palette)
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
                    if (c == -1) continue;
                    var (r, g, b) = c == -2 ? (255, 0, 255) : c == -3 ? (0, 255, 255) : PolarTextures.Colours.Rgb(palette, c);
                    var i = (y * w + x0 + x) * 4;
                    px[i] = (byte)b; px[i + 1] = (byte)g; px[i + 2] = (byte)r;
                }
            x0 += vw * k + 8;
        }
        PngWriter.Write(path, px, w, h);
    }
}
