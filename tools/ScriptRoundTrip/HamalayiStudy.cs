using LBAAssembler;
using LBAAssembler.Lba1;
using LBAAssembler.Terrain.Polar;

namespace ScriptRoundTrip;

// hamalayi <LBA1 folder> <out folder> [scene...]: a reference study of LBA1's Hamalayi Mountains (islands 5 and 6) for a snow island design
// (2026-10-22): the scenes and their descriptions and exits, the joined maps (areas) with each tile's offset, each area and each outside scene
// drawn as LBA1 draws it, and for the given scenes (default: every scene of the Hamalayi outside areas) the bricks by code (material) --
// how many cells, how many column tops, mean colour -- the most used top bricks of each code with their top face's mean colour (sampled as
// PolarTextures samples a brick's top into a 32 x 32 tile), and a sheet of them (hamalayi_top_bricks.png) and their tiles (hamalayi_tiles.png).
internal static class HamalayiStudy
{
    public static int Run(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var outDir = args[2];
        Directory.CreateDirectory(outDir);
        var scale = game.Palette.Take(768).Max() <= 63 ? 4 : 1;
        (int R, int G, int B) Rgb(int c) => (game.Palette[c * 3] * scale, game.Palette[c * 3 + 1] * scale, game.Palette[c * 3 + 2] * scale);

        Console.WriteLine("== scenes of islands 5 and 6 (Hamalayi Mountains)");
        foreach (var s in game.Scenes.Where(s => s.Island is 5 or 6))
            Console.WriteLine($"scene {s.Index,3} (island {s.Island}): {game.Description(s.Index)}; {s.ActorCount} actors, {s.ZoneCount} zones; exits to {string.Join(",", s.Exits)}");

        Console.WriteLine("== areas");
        var areas = game.Areas.Where(a => a.Island is 5 or 6).ToList();
        var n = 0;
        foreach (var area in areas)
        {
            Console.WriteLine($"area {n} '{area.Name}' (island {area.Island}): {area.Tiles.Count} tiles");
            foreach (var t in area.Tiles)
                Console.WriteLine($"   scene {t.Scene,3} at cells ({t.OffsetX / 512}, {t.OffsetY / 256}, {t.OffsetZ / 512}){(t.Window is { } w ? $" window {w}" : "")}{(t.Without is { } wo ? $" without {wo}" : "")}  {game.Description(t.Scene)}");
            var image = game.RenderArea(area);
            var path = Path.Combine(outDir, $"area{n}_{string.Join("-", area.Tiles.Select(t => t.Scene))}.png");
            PngWriter.Write(path, image.Bgra, image.Width, image.Height);
            Console.WriteLine($"   {path}: {image.Width} x {image.Height}");
            n++;
        }

        var outside = args.Length > 3 ? args.Skip(3).Select(int.Parse).ToList()
            : areas.Where(a => a.Name == "Outside").SelectMany(a => a.Tiles.Select(t => t.Scene)).Distinct().ToList();
        Console.WriteLine($"== scenes studied: {string.Join(",", outside)}");
        foreach (var scene in outside)
        {
            var image = game.RenderScene(scene);
            var path = Path.Combine(outDir, $"scene{scene}.png");
            PngWriter.Write(path, image.Bgra, image.Width, image.Height);
            var bricks = PolarLayout.Bricks(game, scene);
            var tops = bricks.GroupBy(b => (b.X, b.Z)).Select(g => g.MaxBy(b => b.Y)).ToList();
            var layers = tops.GroupBy(t => t.Y).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}");
            Console.WriteLine($"scene {scene}: {path} {image.Width} x {image.Height}; {bricks.Count} bricks, {tops.Count} columns; column tops by layer {string.Join(" ", layers)}");
        }

        // bricks of the studied scenes, by code
        var all = outside.SelectMany(s => PolarLayout.Bricks(game, s).Select(b => (Scene: s, B: b))).ToList();
        var topSet = outside.SelectMany(s => PolarLayout.Bricks(game, s).GroupBy(b => (b.X, b.Z)).Select(g => (s, g.MaxBy(b => b.Y)))).ToHashSet();
        var sprites = new Dictionary<int, PolarTextures.Sprite>();
        PolarTextures.Sprite Sprite(int brick) => sprites.TryGetValue(brick, out var sp) ? sp : sprites[brick] = PolarTextures.Sprite.Decode(game.ReadBrick(brick));
        string Mean(IEnumerable<int> colours)
        {
            var list = colours.Where(c => c >= 0).Select(Rgb).ToList();
            return list.Count == 0 ? "-" : $"({list.Average(c => c.R):0},{list.Average(c => c.G):0},{list.Average(c => c.B):0})";
        }
        string Dominant(IEnumerable<int> colours, int k)
            => string.Join(" ", colours.Where(c => c >= 0).GroupBy(c => c).OrderByDescending(g => g.Count()).Take(k).Select(g => $"#{g.Key}{Rgb(g.Key)}"));

        Console.WriteLine("== codes (block library entry's second byte: high nibble the footstep material, F1 water, F0 none)");
        var sheet = new List<int>();
        foreach (var g in all.GroupBy(a => a.B.Code).OrderByDescending(g => g.Count()))
        {
            var topCount = g.Count(a => topSet.Contains((a.Scene, a.B)));
            Console.WriteLine($"code {g.Key:X2}: {g.Count(),6} cells, {topCount,5} column tops, {g.Select(a => a.B.Brick).Distinct().Count(),4} bricks, shapes {string.Join(",", g.Select(a => a.B.Shape).Distinct().Order())}, layers {g.Min(a => a.B.Y)}..{g.Max(a => a.B.Y)}");
            foreach (var bg in g.GroupBy(a => a.B.Brick).OrderByDescending(b => b.Count()).Take(8))
            {
                var sp = Sprite(bg.Key);
                var top = sp.Tile(PolarTextures.Face.Top);
                var sx = sp.Tile(PolarTextures.Face.SideX);
                Console.WriteLine($"    brick {bg.Key,4}: {bg.Count(),5} cells ({bg.Count(a => topSet.Contains((a.Scene, a.B))),4} tops) top cover {sp.Coverage(PolarTextures.Face.Top):0.00} side cover {sp.Coverage(PolarTextures.Face.SideX):0.00}/{sp.Coverage(PolarTextures.Face.SideZ):0.00}; " +
                                  $"top {Mean(top)} side {Mean(sx)}; dominant {Dominant(sp.Pixels, 3)}; blocks {string.Join(",", bg.Select(a => a.Scene + ":" + a.B.Block).Distinct().Take(4))}");
                if (sheet.Count < 64 && !sheet.Contains(bg.Key) && bg.Count() >= 20) sheet.Add(bg.Key);
            }
        }

        // the wall bricks (cliff faces, building walls): cells under another of their column whose brick draws its sides
        Console.WriteLine("== side bricks by code (cells not the top of their column, side cover >= 0.5): the cliff faces and walls");
        var sideSheet = new List<int>();
        foreach (var g in all.Where(a => !topSet.Contains((a.Scene, a.B)) && Math.Max(Sprite(a.B.Brick).Coverage(PolarTextures.Face.SideX), Sprite(a.B.Brick).Coverage(PolarTextures.Face.SideZ)) >= 0.5)
                     .GroupBy(a => a.B.Code).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"code {g.Key:X2}: {g.Count(),6} side cells, {g.Select(a => a.B.Brick).Distinct().Count()} bricks");
            foreach (var bg in g.GroupBy(a => a.B.Brick).OrderByDescending(b => b.Count()).Take(8))
            {
                var sp = Sprite(bg.Key);
                Console.WriteLine($"    brick {bg.Key,4}: {bg.Count(),5} cells; side x {Mean(sp.Tile(PolarTextures.Face.SideX))} side z {Mean(sp.Tile(PolarTextures.Face.SideZ))} top {Mean(sp.Tile(PolarTextures.Face.Top))}; dominant {Dominant(sp.Pixels, 3)}; scenes {string.Join(",", bg.Select(a => a.Scene).Distinct())}");
                if (sideSheet.Count < 16 && bg.Count() >= 20) sideSheet.Add(bg.Key);
            }
        }
        if (sideSheet.Count > 0) PolarStudy.Sheet(new[] { "polarsheet", args[1], Path.Combine(outDir, "hamalayi_side_bricks.png") }.Concat(sideSheet.Select(b => b.ToString())).ToArray());

        // the palette's colours most drawn over the studied scenes' column tops (weighted by top face pixels)
        Console.WriteLine("== colours of the column tops' top faces (share of texels)");
        var texels = new Dictionary<int, long>();
        foreach (var (scene, b) in topSet)
            foreach (var c in Sprite(b.Brick).Tile(PolarTextures.Face.Top)) if (c >= 0) texels[c] = texels.GetValueOrDefault(c) + 1;
        var sum = texels.Values.Sum();
        foreach (var (c, k) in texels.OrderByDescending(t => t.Value).Take(24)) Console.WriteLine($"  #{c,3} {Rgb(c)}: {100.0 * k / sum:0.0}%");
        Console.WriteLine("== colours of every drawn brick pixel");
        var pix = new Dictionary<int, long>();
        foreach (var (scene, b) in all) foreach (var c in Sprite(b.Brick).Pixels) if (c >= 0) pix[c] = pix.GetValueOrDefault(c) + 1;
        sum = pix.Values.Sum();
        foreach (var (c, k) in pix.OrderByDescending(t => t.Value).Take(32)) Console.WriteLine($"  #{c,3} {Rgb(c)}: {100.0 * k / sum:0.0}%");

        // the top tiles of the sheet's bricks, 32 x 32 each, scaled 3x, 8 a row
        const int z = 3, per = 8;
        int tw = PolarTextures.TileSize * z + 6, rows = (sheet.Count + per - 1) / per;
        var tp = new byte[per * tw * rows * tw * 4];
        for (var i = 0; i < sheet.Count; i++)
        {
            var tile = Sprite(sheet[i]).Tile(PolarTextures.Face.Top);
            for (var j = 0; j < PolarTextures.TileSize * z; j++)
                for (var k = 0; k < PolarTextures.TileSize * z; k++)
                {
                    var c = tile[(j / z) * PolarTextures.TileSize + k / z];
                    var (r, gg, b) = c >= 0 ? Rgb(c) : (0, 0, 0);
                    int x = (i % per) * tw + 3 + k, y = (i / per) * tw + 3 + j;
                    var o = (y * per * tw + x) * 4; tp[o] = (byte)b; tp[o + 1] = (byte)gg; tp[o + 2] = (byte)r; tp[o + 3] = 255;
                }
        }
        var tilesPath = Path.Combine(outDir, "hamalayi_tiles.png");
        PngWriter.Write(tilesPath, tp, per * tw, Math.Max(1, rows) * tw);
        Console.WriteLine($"{tilesPath}: top tiles of bricks {string.Join(" ", sheet)} (8 a row)");
        PolarStudy.Sheet(new[] { "polarsheet", args[1], Path.Combine(outDir, "hamalayi_top_bricks_a.png") }.Concat(sheet.Take(16).Select(b => b.ToString())).ToArray());
        if (sheet.Count > 16) PolarStudy.Sheet(new[] { "polarsheet", args[1], Path.Combine(outDir, "hamalayi_top_bricks_b.png") }.Concat(sheet.Skip(16).Take(16).Select(b => b.ToString())).ToArray());
        if (sheet.Count > 32) PolarStudy.Sheet(new[] { "polarsheet", args[1], Path.Combine(outDir, "hamalayi_top_bricks_c.png") }.Concat(sheet.Skip(32).Take(16).Select(b => b.ToString())).ToArray());
        return 0;
    }

    // hamalayiregion <LBA1 folder> <out.png> <scene> <x0> <x1> <z0> <z1> [code,code...]: a region of a scene (its own cells), optionally
    // only the cells of those codes (hex), drawn as LBA1 draws it.
    public static int Region(string[] args)
    {
        var game = new Lba1Game(args[1]);
        int x0 = int.Parse(args[4]), x1 = int.Parse(args[5]), z0 = int.Parse(args[6]), z1 = int.Parse(args[7]);
        var codes = args.Length > 8 ? args[8].Split(',').Select(c => Convert.ToInt32(c, 16)).ToHashSet() : null;
        var cells = PolarLayout.Bricks(game, int.Parse(args[3])).Where(c => c.X >= x0 && c.X <= x1 && c.Z >= z0 && c.Z <= z1 && (codes is null || codes.Contains(c.Code)))
            .Select(c => new Lba1Placement(c.X - x0, c.Y, c.Z - z0, c.Brick)).ToList();
        var image = Lba1GridRenderer.Render(new[] { new Lba1Tile(cells, 0, 0, 0) }, game.ReadBrick, game.Palette);
        PngWriter.Write(args[2], image.Bgra, image.Width, image.Height);
        Console.WriteLine($"{args[2]}: {cells.Count} cells, {image.Width} x {image.Height}");
        return 0;
    }
}
