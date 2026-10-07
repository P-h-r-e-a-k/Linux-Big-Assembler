using LBAAssembler.Lba1;

namespace LBAAssembler.Terrain.Polar;

// LBA1's Polar Island (island 10) as one piece of ground, for its port to an LBA2 island (2026-10-05, the user's: "port the connected
// scenes from LBA1's Polar Island into a new Island for LBA2"). Its outside scenes are separate grids of 64 x 25 x 64 cells; their
// cube-change zones say where each lies beside the others:
//
//   115 1st scene (the dock, from Fortress Island) -- 106 2nd scene -- 107 3rd scene -- 108 before the rocky peak (west of 107)
//                                                                                    -- 109 4th scene (north of 107)
//
// and 108's gate leads to 110, the rocky peak, a crystal mountain in the water with a path from the gate, from which 111 ("on the rocky
// peak", the plateau of pillars on its top) is reached. But put where its zones say, 110 lies over 107 -- whose north corner holds the same
// crystal mountain, though not where 110's is: the two scenes disagree. So 110 is left out and 107's mountain is the peak (the user's:
// "107 replicates a copy of this anyway"): 111's plateau goes on top of it, placed by matching 110's mountain to 107's (PeakCopyOffset), and
// the channel of water between 108's gate and 107's ground is bridged, so the gate opens onto 107's path up to the mountain (Bridge).
//
// The island's frame: cells (x, layer, z) with 107's grid at the origin; a layer is 256 world units, a cell 512, as in LBA2.
internal sealed class PolarLayout
{
    // Where a scene's grid lies in the island's frame (its cell 0,0,0), in cells and layers.
    public sealed record Placed(int Scene, int X, int Y, int Z, string Name);

    // the scenes placed by where their car tracks meet (ScriptRoundTrip polarexits / polarfit). The cube-change zones put them nearly
    // there, but a few cells out: LBA1 sets Twinsen down at one point of the next scene wherever he crossed the zone, so a zone and its
    // arrival give a scene's place only within a cell or two, and they overlapped grids that meet edge to edge. 106, 108 and 109 meet 107
    // edge to edge (the rows the zones overlapped have nothing in common), each where its tracks run on from 107's at the same height --
    // 106 a cell west of the zones' place, 108 a cell south; 115, the dock, shares a strip of ground with 106 (228 columns, 132 of them
    // the same top brick and layer), so it lies over it there, a cell south of the zones' place.
    public static readonly Placed[] Zoned =
    {
        new(107, 0, 0, 0, "3rd scene"), new(106, -16, 0, 64, "2nd scene"), new(108, -64, 0, 0, "Before the rocky peak"),
        new(109, -6, 1, -64, "4th scene"), new(115, 9, 0, 91, "1st scene (the dock)"),
    };
    public const int PeakScene = 110, PlateauScene = 111, GateScene = 108;
    // 111's grid from 110's, by their zones (LinkStudy: 110 <-> 111 (-21, 8, -9), both ways agree)
    public static readonly (int X, int Y, int Z) PlateauFromPeak = (-21, 8, -9);
    // 111's own floor (layers 0-1, a teal backdrop seen far below the plateau) is not ground: left out (111 is added after the peak is
    // built, on top of it)
    public const int PlateauFloorTop = 1;
    // a column of 110 this high and more is its crystal mountain
    public const int MountainTop = 8;
    // the rocky peak twice as high as LBA1 has it (the user's: "stretch it upwards" -- LBA2 has the height LBA1's 25 layers didn't): its
    // every layer two, from the sea up; 111's plateau lifted as much, onto its top
    public const int PeakStretch = 2;
    // The island twice LBA1's size, every way (2026-10-06, the user's: "widen the island and make the whole thing bigger" -- 108's car
    // tracks go back and forth up its terraces five times, their ways six to eight cells apart, and the race track's road, eight cells
    // wide, could only take three of them): a cell of the layout is Scale x Scale cells of the island, a layer Scale layers. The ground's
    // heights are the layout's corners' scaled (its slopes as steep as LBA1's), the objects' boxes scaled, LBA1's places scaled.
    public const int Scale = 2;

    // A cell of the island: the scene its brick comes from (-1: a cell the layout made, the causeway), the brick (LBA_BRK entry), and from
    // the scene's block library its shape (1 solid, 2-13 the slopes and steps) and its code (high nibble F: a game code -- F1 water to
    // drown in, F0 nothing; otherwise the high nibble is the footstep's material: 6 dirt, A the crystal ...), and the block it is part of.
    public readonly record struct Cell(int Scene, int Brick, int Shape, int Code, int Block)
    {
        public bool Water => Code == 0xF1;
        public int Material => (Code & 0xF0) == 0xF0 ? -1 : Code >> 4;
    }

    // A brick of a scene's grid at (x, layer, z), with its library entry's shape and code and its block.
    public readonly record struct SceneBrick(int X, int Y, int Z, int Brick, int Shape, int Code, int Block);

    public static List<SceneBrick> Bricks(Lba1Game game, int scene)
    {
        var cells = Lba1GridCodec.Decode(game.ReadGrid(scene));
        var lib = game.ReadBlocks(scene);
        var count = BitConverter.ToInt32(lib, 0) / 4;
        var list = new List<SceneBrick>(8192);
        for (var z = 0; z < 64; z++)
        for (var x = 0; x < 64; x++)
        for (var y = 0; y < 25; y++)
        {
            var i = ((z * 64 + x) * 25 + y) * 2;
            int block = cells[i], pos = cells[i + 1];
            if (block == 0 || block > count) continue;
            var at = BitConverter.ToInt32(lib, (block - 1) * 4);
            if (pos >= lib[at] * lib[at + 1] * lib[at + 2]) continue;
            var e = at + 3 + pos * 4;
            var number = BitConverter.ToUInt16(lib, e + 2);
            if (number == 0) continue;
            list.Add(new SceneBrick(x, y, z, number - 1, lib[e], lib[e + 1], block));
        }
        return list;
    }

    public required IReadOnlyList<Placed> Placements { get; init; }
    public required Dictionary<(int X, int Y, int Z), Cell> Cells { get; init; }
    // where 110's mountain lies in 107 (110's frame from 107's), and how well it matched (columns of the same height / of the mountain)
    public required (int X, int Y, int Z) PeakCopyOffset { get; init; }
    public required (int Matched, int Of) PeakMatch { get; init; }
    public required List<(int X, int Z)> Bridge { get; init; }
    // the rocky peak's columns: built as objects with straight walls (PolarObjects), the sea under them, not ground
    public required HashSet<(int X, int Z)> Peak { get; init; }
    public List<string> Log { get; } = new();

    public static PolarLayout Build(Lba1Game game)
    {
        var placements = Zoned.ToList();
        var bricks = new Dictionary<int, List<SceneBrick>>();
        List<SceneBrick> Of(int scene) => bricks.TryGetValue(scene, out var b) ? b : bricks[scene] = Bricks(game, scene);

        // 110's mountain in 107: its columns of 10 layers and more, tried at every offset near the zones' guess; the offset where the most of
        // them stand as high in 107 is where 107 copies it
        static Dictionary<(int X, int Z), int> Tops(IEnumerable<SceneBrick> cells) => cells.GroupBy(c => (c.X, c.Z)).ToDictionary(g => g.Key, g => g.Max(c => c.Y));
        var peakTops = Tops(Of(PeakScene)).Where(t => t.Value >= 10).ToDictionary(t => t.Key, t => t.Value);
        var tops107 = Tops(Of(107));
        (int X, int Y, int Z) best = (0, 0, 0); var bestScore = -1;
        for (var dx = -40; dx <= 40; dx++)
            for (var dz = -40; dz <= 40; dz++)
                for (var dy = -3; dy <= 3; dy++)
                {
                    var score = 0;
                    foreach (var ((x, z), y) in peakTops)
                        if (tops107.TryGetValue((x + dx, z + dz), out var t) && t == y + dy) score++;
                    if (score > bestScore) { bestScore = score; best = (dx, dy, dz); }
                }
        var plateau = (X: best.X + PlateauFromPeak.X, Y: best.Y + PlateauFromPeak.Y, Z: best.Z + PlateauFromPeak.Z);
        placements.Add(new(PlateauScene, plateau.X, plateau.Y, plateau.Z, "On the rocky peak (on 107's mountain)"));

        // the island's cells: each scene's bricks moved into place. Where two scenes' grids cover the same column (the dock's strip shared
        // with 106), the whole column is the scene it lies deepest in -- a column's bricks never from two scenes, one's ground under the
        // other's wall. 111's plateau, on the peak, is added over them.
        var cells = new Dictionary<(int, int, int), Cell>();
        var owner = new Dictionary<(int X, int Z), (int Scene, int Depth)>();
        foreach (var p in placements.Where(p => p.Scene != PlateauScene))
            foreach (var (x, z) in Of(p.Scene).Select(c => (c.X, c.Z)).Distinct())
            {
                var depth = Math.Min(Math.Min(x, 63 - x), Math.Min(z, 63 - z));
                var at = (x + p.X, z + p.Z);
                if (!owner.TryGetValue(at, out var o) || depth > o.Depth) owner[at] = (p.Scene, depth);
            }
        foreach (var p in placements.Where(p => p.Scene != PlateauScene))
            foreach (var c in Of(p.Scene))
            {
                if (owner[(c.X + p.X, c.Z + p.Z)].Scene != p.Scene) continue;
                cells.TryAdd((c.X + p.X, c.Y + p.Y, c.Z + p.Z), new Cell(p.Scene, c.Brick, c.Shape, c.Code, c.Block));
            }

        // Each scene shows only the part of the mountain inside its own grid -- 107 its south half, cut off at its north corner, and 108
        // and 109 none of it, low ground where the rest of it would stand (polarmountain: of its 152 columns, 47 are as high in 107, 40
        // lower, 57 low ground of 108 and 109). The whole mountain is 110's (its columns of MountainTop layers and more), at the place it
        // matched: in each of its columns lower than it, the mountain's own bricks take the place of the scenes' (111's plateau stays).
        var filled = 0;
        var peakTopsAll = Tops(Of(PeakScene));
        var mountain = peakTopsAll.Where(t => t.Value >= MountainTop).Select(t => t.Key).ToHashSet();
        var islandTops = cells.GroupBy(c => (c.Key.Item1, c.Key.Item3)).ToDictionary(g => g.Key, g => g.Max(c => c.Key.Item2));
        foreach (var (x, z) in mountain)
        {
            var at = (X: x + best.X, Z: z + best.Z);
            var height = peakTopsAll[(x, z)] + best.Y;
            if (islandTops.TryGetValue(at, out var t) && t >= height) continue;
            foreach (var key in cells.Keys.Where(k => k.Item1 == at.X && k.Item3 == at.Z && k.Item2 <= height && cells[k].Scene != PlateauScene).ToList()) cells.Remove(key);
            foreach (var c in Of(PeakScene).Where(c => c.X == x && c.Z == z))
                if (cells.TryAdd((at.X, c.Y + best.Y, at.Z), new Cell(PeakScene, c.Brick, c.Shape, c.Code, c.Block))) filled++;
        }

        var peak = mountain.Select(m => (X: m.X + best.X, Z: m.Z + best.Z)).ToHashSet();
        // (107 and 109 each draw part of the mountain too, a few cells from where 110 has it: their teal columns above MountainTop near the
        // peak are 110's there instead -- its water or its path -- or, past 110's grid, gone down to MountainTop)
        var copies = 0;
        var nearPeak = peak.SelectMany(q => Enumerable.Range(-8, 17).SelectMany(dx => Enumerable.Range(-8, 17).Select(dz => (X: q.X + dx, Z: q.Z + dz)))).Where(q => !peak.Contains(q)).ToHashSet();
        var tops110 = Tops(Of(PeakScene));
        foreach (var (x, z) in nearPeak)
        {
            var column = cells.Where(c => c.Key.Item1 == x && c.Key.Item3 == z).ToList();
            if (column.Count == 0) continue;
            var top = column.MaxBy(c => c.Key.Item2);
            var teal = (top.Value.Code & 0xF0) == 0xA0 || top.Value.Code == 0x00 && BrickColour(game, top.Value.Brick) is var (r, _, bl) && bl > r;
            if (!teal || top.Key.Item2 <= MountainTop) continue;
            copies++;
            if (tops110.ContainsKey((x - best.X, z - best.Z)))
            {
                foreach (var c in column) cells.Remove(c.Key);
                foreach (var c in Of(PeakScene).Where(c => c.X == x - best.X && c.Z == z - best.Z))
                    cells[(x, c.Y + best.Y, z)] = new Cell(PeakScene, c.Brick, c.Shape, c.Code, c.Block);
            }
            else foreach (var c in column.Where(c => c.Key.Item2 > MountainTop)) cells.Remove(c.Key);
        }

        // the rocky peak: its columns (the mountain's, made whole) twice as high -- each layer two of the same brick -- and 111's plateau
        // on it, lifted as much as the peak's top rose (over the peak's own bricks where they meet)
        // (LBA1 leaves a column hollow where its camera never looks -- under a tier, behind the front ones: a peak seen from all round is
        // filled from the sea to its lowest brick, with that brick)
        foreach (var (px, pz) in peak)
        {
            var column = cells.Where(c => c.Key.Item1 == px && c.Key.Item3 == pz && c.Value.Scene != PlateauScene && c.Key.Item2 >= 1).OrderBy(c => c.Key.Item2).ToList();
            if (column.Count == 0) continue;
            for (var i = column.Count - 1; i >= 0; i--)
            {
                var below = i > 0 ? column[i - 1].Key.Item2 : 0;
                for (var y = column[i].Key.Item2 - 1; y > below; y--) cells.TryAdd((px, y, pz), column[i].Value);
            }
        }
        var peakCells = cells.Where(c => peak.Contains((c.Key.Item1, c.Key.Item3)) && c.Value.Scene != PlateauScene && c.Key.Item2 >= 1).ToList();
        var peakTop = peakCells.Max(c => c.Key.Item2);
        foreach (var c in peakCells) cells.Remove(c.Key);
        foreach (var ((x, y, z), cell) in peakCells)
            for (var k = 0; k < PeakStretch; k++)
                cells[(x, PeakStretch * y - k, z)] = cell;
        var lift = (PeakStretch - 1) * peakTop;
        foreach (var c in Of(PlateauScene).Where(c => c.Y > PlateauFloorTop))
            cells[(c.X + plateau.X, c.Y + plateau.Y + lift, c.Z + plateau.Z)] = new Cell(PlateauScene, c.Brick, c.Shape, c.Code, c.Block);

        var layout = new PolarLayout
        {
            Placements = placements, Cells = cells, PeakCopyOffset = best, PeakMatch = (bestScore, peakTops.Count), Bridge = new(), Peak = peak,
        };
        layout.Log.Add($"the rocky peak: {peak.Count} columns, {PeakStretch} times as high (its top layer {peakTop} now {PeakStretch * peakTop}); 111's plateau lifted {lift} layers onto it; {copies} columns of the other scenes' copies of the mountain made 110's");
        layout.Log.Add($"the mountain made whole from 110's: {filled} cells where the scenes had it lower or not at all");
        layout.Log.Add($"110's mountain ({peakTops.Count} columns of 10 layers and more) matches 107 at {best} ({bestScore} columns as high); the zones put 110 at (-4, 0, -11)");
        layout.Log.Add($"111 on it at {plateau}");
        layout.BridgeGate(game);
        return layout;
    }

    // The channel between 108's gate (its zone to 110) and 107's ground: water bricks level with the ground (a column whose top brick is
    // water: IsWater). Every row of the gate's width is carried on east from the gate as far as the row with the furthest to go needs (to
    // its first column that isn't water), each column the peninsula's own ground just inside the gate (two cells west of it: dirt; the
    // gate's own column has the peninsula's rocky border).
    private void BridgeGate(Lba1Game game)
    {
        var gateScene = Placements.First(p => p.Scene == GateScene);
        var zone = game.LoadScene(GateScene).Zones.First(z => z.Type == 0 && z.Info[0] == PeakScene);
        int x0 = zone.X0 / 512 + gateScene.X, x1 = zone.X1 / 512 + gateScene.X, z0 = zone.Z0 / 512 + gateScene.Z, z1 = zone.Z1 / 512 + gateScene.Z;
        int Top(int x, int z) { for (var y = 24; y >= 0; y--) if (Cells.ContainsKey((x, y, z))) return y; return -1; }
        var inside = x0 - 2;
        var ground = Enumerable.Range(z0, z1 - z0 + 1).Max(z => Top(inside, z));
        if (ground < 0) { Log.Add("108's gate: no ground inside it, not bridged"); return; }
        bool Wet(int x, int z) { var t = Top(x, z); return t < 0 || t < ground || Cells.TryGetValue((x, t, z), out var c) && c.Water; }
        // the causeway: past the peninsula (which runs on east of the gate), as long as the shortest stretch of water any row of the gate's
        // width has before land again (row 28: four cells, onto the strip of 107's ground towards the mountain), the gate's whole width
        var gap = int.MaxValue; var from = int.MaxValue;
        for (var z = z0; z <= z1; z++)
        {
            var x = x1 + 1;
            while (x < x1 + 40 && !Wet(x, z)) x++;
            var start = x;
            while (x < x1 + 40 && Wet(x, z)) x++;
            from = Math.Min(from, start);
            if (x < x1 + 40) gap = Math.Min(gap, x - start);
        }
        if (gap == int.MaxValue) { Log.Add("108's gate: no land across the water from it, not bridged"); return; }
        for (var z = z0; z <= z1; z++)
        {
            var column = Enumerable.Range(0, ground + 1).Select(y => Cells.TryGetValue((inside, y, z), out var c) ? c : (Cell?)null).ToArray();
            for (var x = from; x < from + gap; x++)
            {
                if (!Wet(x, z)) continue;
                foreach (var key in Cells.Keys.Where(k => k.X == x && k.Z == z && k.Y <= ground).ToList()) Cells.Remove(key);
                for (var y = 0; y <= ground; y++)
                    if (column[y] is { } c) Cells[(x, y, z)] = c with { Scene = -1 };
                Bridge.Add((x, z));
            }
        }
        Log.Add($"108's gate (cells x {x0}..{x1}, z {z0}..{z1} of the island): a causeway {gap} cells long from x {from}, {Bridge.Count} columns, at layer {ground}");
    }

    // A brick's mean colour (8-bit RGB) over its drawn pixels.
    private static readonly Dictionary<int, (double R, double G, double B)> colours = new();
    public static (double R, double G, double B) BrickColour(Lba1Game game, int brick)
    {
        if (colours.TryGetValue(brick, out var known)) return known;
        var data = game.ReadBrick(brick);
        double r = 0, g = 0, b = 0; var n = 0;
        // (LBA1's palette is 6-bit, 0..63: four times brighter as 8-bit)
        var scale = game.Palette.Take(768).Max() <= 63 ? 4 : 1;
        if (data is { Length: > 4 })
        {
            // LBA_BRK: width, lines, hot x, hot y, then per line a count of runs, each a control byte (top two bits: 0 skip, 1 that many
            // colours follow, 2 or 3 one colour that many times; the length in the low six bits plus one) -- as Meshers.BrickAverage reads it
            int lines = data[1], p = 4;
            for (var line = 0; line < lines && p < data.Length; line++)
            {
                int runs = data[p++];
                for (var k = 0; k < runs && p < data.Length; k++)
                {
                    int control = data[p++], length = (control & 0x3F) + 1;
                    switch (control >> 6)
                    {
                        case 0: break;
                        case 1: for (var i = 0; i < length && p < data.Length; i++) Add(data[p++], 1); break;
                        default: if (p < data.Length) Add(data[p++], length); break;
                    }
                }
            }
        }
        void Add(int colour, int times) { var at = colour * 3; r += game.Palette[at] * scale * times; g += game.Palette[at + 1] * scale * times; b += game.Palette[at + 2] * scale * times; n += times; }
        var result = n > 0 ? (r / n, g / n, b / n) : (128.0, 128.0, 128.0);
        colours[brick] = result;
        return result;
    }

    // The island's bricks as one tile, for Lba1GridRenderer.
    public Lba1Tile Tile() => new(Cells.Select(c => new Lba1Placement(c.Key.X, c.Key.Y, c.Key.Z, c.Value.Brick)).ToList(), 0, 0, 0);
}
