using System.Globalization;
using System.Text;
using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// trackmaps <game folder> <out folder>: a map of each race track the folder has built, to draw the checkpoints on -- the island from above
// (IslandMapRenderer: the painted road shows on the ground), a raised road's deck drawn over it, numbered markers and arrows along the lap
// (the opponent's line, from the start line on), the start line and the checkpoints the build placed (red, C1..). A lap that winds over
// itself (Celebration Island's spiral round the statue, the Elevator Platform's helix) gets a map for each level of it, the others faint.
// Each map is <name>.png (the terrain, cropped) and <name>.txt (what trackmaps_paint.ps1 draws over it, in its pixels), and maps.txt
// keeps how each map's pixels turn back into island cells -- for reading back lines drawn on the maps.
internal static class TrackMapsCommand
{
    private const double MarginCells = 14, TargetPixels = 1700, LayerGap = 1500, LayerReach = 7;

    public static int Run(string[] args)
    {
        var game = args[1]; var outDir = args[2];
        Directory.CreateDirectory(outDir);
        var info = RaceTrackService.ReadInfo(game);
        if (info is null) { Console.WriteLine($"{game}: no RACETRACK.JSON"); return 1; }
        var index = new StringBuilder("# map  island_file  origin_cell_x  origin_cell_z  pixels_per_cell  marker_step_cells  level  (an island cell = origin + pixel / pixels_per_cell)\n");
        var n = 0;
        foreach (var t in RaceTrackService.Tracks(info))
        {
            var island = RaceTrackIsland.ByName(t.Island);
            var tracks = new List<(RaceTrackService.TrackInfo Track, string Name, string File)>();
            if (t.Twin is { } twin)
            {
                tracks.Add((t, "Citadel Island, storm track", island.IleFile));
                tracks.Add((twin, "Citadel Island, town circuit", island.TwinIleFile ?? island.IleFile));
            }
            else tracks.Add((t, island.Shown, island.IleFile));
            foreach (var (track, name, file) in tracks)
            {
                n++;
                foreach (var line in Maps(game, outDir, n, track, name, Path.GetFileNameWithoutExtension(file).ToUpperInvariant())) index.AppendLine(line);
            }
        }
        File.WriteAllText(Path.Combine(outDir, "maps.txt"), index.ToString());
        Console.WriteLine(index.ToString());
        return 0;
    }

    private static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    private static IEnumerable<string> Maps(string game, string outDir, int number, RaceTrackService.TrackInfo track, string name, string ileName)
    {
        var path = track.Path ?? new();
        if (path.Count < 3) yield break;
        // the lap in cells, from the start line on
        double[] xs = path.Select(p => p[0] / 512.0).ToArray(), zs = path.Select(p => p[1] / 512.0).ToArray(), ys = path.Select(p => (double)p[2]).ToArray();
        var count = xs.Length;
        var along = new double[count];
        for (var i = 1; i < count; i++) along[i] = along[i - 1] + Math.Sqrt((xs[i] - xs[i - 1]) * (xs[i] - xs[i - 1]) + (zs[i] - zs[i - 1]) * (zs[i] - zs[i - 1]));
        var length = along[^1];
        var raised = (track.Raised ?? new()).Select(r => (X: r[0] / 512.0, Z: r[1] / 512.0, Y: (double)r[2], Half: r[3] / 512.0)).ToList();
        // the levels: a new one each time the lap comes over (or under) a part of the level it is on
        var level = new int[count];
        var levels = 1;
        var overlapping = 0;
        for (var i = 0; i < count; i++)
            for (var j = 0; j < count; j++)
                if (Math.Min(Math.Abs(i - j), count - Math.Abs(i - j)) > 30 && Math.Abs(ys[i] - ys[j]) > LayerGap && Near(i, j)) { overlapping++; break; }
        if (overlapping > count / 10)
        {
            var from = 0;
            for (var i = 1; i < count; i++)
            {
                for (var j = from; j < i - 30; j++)
                    if (Math.Abs(ys[i] - ys[j]) > LayerGap && Near(i, j)) { levels++; from = i; break; }
                level[i] = levels - 1;
            }
        }
        bool Near(int a, int b) => (xs[a] - xs[b]) * (xs[a] - xs[b]) + (zs[a] - zs[b]) * (zs[a] - zs[b]) < LayerReach * LayerReach;
        // the frame: the whole lap (the same for each level), the island's cells, scaled to about TargetPixels
        double x0 = xs.Min(), x1 = xs.Max(), z0 = zs.Min(), z1 = zs.Max();
        foreach (var r in raised) { x0 = Math.Min(x0, r.X - r.Half); x1 = Math.Max(x1, r.X + r.Half); z0 = Math.Min(z0, r.Z - r.Half); z1 = Math.Max(z1, r.Z + r.Half); }
        var cx0 = (int)Math.Floor(x0 - MarginCells); var cz0 = (int)Math.Floor(z0 - MarginCells);
        var cx1 = (int)Math.Ceiling(x1 + MarginCells); var cz1 = (int)Math.Ceiling(z1 + MarginCells);
        var scale = Math.Clamp((int)Math.Floor(TargetPixels / Math.Max(cx1 - cx0, cz1 - cz0)), 2, 16);
        // (no narrower than the notes over it: a tall, thin lap -- Citadel Island's storm track -- widened either side)
        var wide = (int)Math.Ceiling(1400.0 / scale) - (cx1 - cx0);
        if (wide > 0) { cx0 -= wide / 2; cx1 += wide - wide / 2; }
        var island = IslandFile.Load(Path.Combine(game, ileName + ".ILE"));
        var renderer = new IslandMapRenderer(island, IslandMapRenderer.LoadPalette(game, ileName), scale);
        renderer.Render(MapView.Terrain, cx0, cz0, cx1, cz1);
        // (the map's pixels for the frame; outside the island's cubes, the sea's dark blue)
        int width = (cx1 - cx0) * scale, height = (cz1 - cz0) * scale + 120;
        var pixels = new byte[width * height * 4];
        for (var k = 0; k < width * height; k++) { pixels[k * 4] = 0x50; pixels[k * 4 + 1] = 0x30; pixels[k * 4 + 2] = 0x20; pixels[k * 4 + 3] = 255; }
        for (var py = 0; py < height - 120; py++)
            for (var px = 0; px < width; px++)
            {
                int sx = (cx0 - renderer.OriginX) * scale + px, sy = (cz0 - renderer.OriginZ) * scale + py;
                if (sx < 0 || sy < 0 || sx >= renderer.PixelWidth || sy >= renderer.PixelHeight) continue;
                Array.Copy(renderer.Pixels, (sy * renderer.PixelWidth + sx) * 4, pixels, ((py + 120) * width + px) * 4, 4);
            }
        double Px(double cellX) => (cellX - cx0) * scale;
        double Py(double cellZ) => (cellZ - cz0) * scale + 120;
        // the markers: about 30 round the lap, at a round number of cells apart
        var step = new[] { 5, 10, 15, 20, 25, 30, 40, 50, 60, 75, 100 }.First(s => length / s <= 34 || s == 100);
        var markers = new List<(double Along, int I)>();
        for (double a = step; a < length - step / 2.0; a += step) markers.Add((a, Array.FindIndex(along, v => v >= a)));
        // where the lap's points of a raised road's middle are: the nearest of the lap's points, for its level
        int LapIndexOf(double x, double z, double y)
        {
            var best = 0; var bestD = double.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var d = (xs[i] - x) * (xs[i] - x) + (zs[i] - z) * (zs[i] - z) + Math.Pow((ys[i] - y) / 512.0, 2);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
        var raisedLevel = raised.Select(r => level[LapIndexOf(r.X, r.Z, r.Y)]).ToList();
        string Line(RaceTrackService.StartLineInfo l, out double y, out double mx, out double mz, out double ex, out double ez)
        {
            double ax = (l.CubeX * 32768 + l.X0) / 512.0, az = (l.CubeZ * 32768 + l.Z0) / 512.0, bx = (l.CubeX * 32768 + l.X1) / 512.0, bz = (l.CubeZ * 32768 + l.Z1) / 512.0;
            mx = (ax + bx) / 2; mz = (az + bz) / 2; ex = bx; ez = bz;
            y = l.Y ?? ys[LapIndexOf(mx, mz, l.Y ?? 0)];
            return $"{F(Px(ax))} {F(Py(az))} {F(Px(bx))} {F(Py(bz))}";
        }
        var stem = $"{number:00}_{Slug(name)}";
        for (var lv = 0; lv < levels; lv++)
        {
            var file = levels > 1 ? $"{stem}_level{lv + 1}" : stem;
            PngWriter.Write(Path.Combine(outDir, file + ".png"), pixels, width, height);
            var plan = new StringBuilder($"size {width} {height}\n");
            var lapAt = Enumerable.Range(0, count).Where(i => level[i] == lv).ToList();
            var heights = levels > 1 ? $", level {lv + 1} of {levels} (heights {ys.Where((_, i) => level[i] == lv).Min():0} to {ys.Where((_, i) => level[i] == lv).Max():0}; the other levels faint)" : "";
            plan.Append($"title 12 8 {name}{heights}\n");
            plan.Append($"note 12 44 Draw a line across the road (a bright colour, not red) wherever a checkpoint should be. Red lines C1, C2 ... are the checkpoints the build placed.\n");
            plan.Append($"note 12 70 Numbers along the lap: markers every {step} cells from the start line (S); arrows: the way the race runs. {F(length)} cells a lap; 1 cell = {scale} pixels.\n");
            // the raised road's deck: the other levels' faint, then this level's, low before high
            foreach (var pass in new[] { false, true })
                foreach (var k in Enumerable.Range(0, Math.Max(0, raised.Count - 1)).OrderBy(k => raised[k].Y))
                {
                    var mine = raisedLevel[k] == lv || raisedLevel[k + 1] == lv;
                    if (mine != pass) continue;
                    var (a, b) = (raised[k], raised[k + 1]);
                    if ((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z) > 25) continue;
                    plan.Append($"road {F(Px(a.X))} {F(Py(a.Z))} {F(Px(b.X))} {F(Py(b.Z))} {F((a.Half + b.Half) * scale)} {(mine ? 0 : 1)}\n");
                }
            // the lap's line, thin, this level's only
            for (var i = 0; i + 1 < count; i++)
                if (level[i] == lv && level[i + 1] == lv) plan.Append($"line {F(Px(xs[i]))} {F(Py(zs[i]))} {F(Px(xs[i + 1]))} {F(Py(zs[i + 1]))} 1.5 255 255 255 110\n");
            // the markers: a dot on the line, the number off to the right of the way the lap runs; an arrow halfway to the next
            for (var m = 0; m < markers.Count; m++)
            {
                var i = markers[m].I;
                if (i < 0 || level[i] != lv) continue;
                var (dx, dz) = Dir(i);
                plan.Append($"dot {F(Px(xs[i]))} {F(Py(zs[i]))} 4 255 230 0\n");
                var off = 4.5 + 12.0 / scale;   // (cells: just past the road's edge)
                plan.Append($"label {F(Px(xs[i] - dz * off))} {F(Py(zs[i] + dx * off))} {m + 1}\n");
            }
            for (var m = 0; m <= markers.Count; m++)
            {
                var a = (m == 0 ? 0 : markers[m - 1].Along) + step / 2.0;
                var i = Array.FindIndex(along, v => v >= a);
                if (i < 0 || level[i] != lv) continue;
                var (dx, dz) = Dir(i);
                plan.Append($"arrow {F(Px(xs[i]))} {F(Py(zs[i]))} {F(dx)} {F(dz)} {F(Math.Max(10, scale * 2.2))}\n");
            }
            // the start line and the checkpoints (on this level)
            if (track.StartLine is { } s)
            {
                var seg = Line(s, out var y, out var mx, out var mz, out var ex, out var ez);
                if (level[LapIndexOf(mx, mz, y)] == lv) { plan.Append($"start {seg}\n"); plan.Append($"tag {F(Px(ex))} {F(Py(ez))} 0 0 0 S\n"); }
            }
            var c = 0;
            foreach (var cp in track.Checkpoints ?? new())
            {
                c++;
                var seg = Line(cp, out var y, out var mx, out var mz, out var ex, out var ez);
                if (level[LapIndexOf(mx, mz, y)] != lv) continue;
                plan.Append($"cp {seg}\n");
                plan.Append($"tag {F(Px(ex))} {F(Py(ez))} 200 0 0 C{c}\n");
            }
            File.WriteAllText(Path.Combine(outDir, file + ".txt"), plan.ToString());
            yield return $"{file}  {ileName}  {cx0}  {(cz0 - 120.0 / scale).ToString("0.###", CultureInfo.InvariantCulture)}  {scale}  {step}  {lv + 1}/{levels}";
        }
        (double, double) Dir(int i)
        {
            int a = Math.Max(0, i - 2), b = Math.Min(count - 1, i + 2);
            double dx = xs[b] - xs[a], dz = zs[b] - zs[a], d = Math.Sqrt(dx * dx + dz * dz) + 1e-9;
            return (dx / d, dz / d);
        }
    }

    private static string Slug(string name) => new string(name.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray()).Replace("__", "_").Trim('_');
}
