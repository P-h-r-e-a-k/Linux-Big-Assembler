using LBAAssembler.Lba1;
using LBAAssembler.Terrain.Polar;

namespace LBAAssembler.Terrain.Temple;

// LBA1's Temple of Bú (White Leaf Desert) as one piece, for its port to an LBA2 island (2026-10-16, the user's: "Port the temple of Bu from
// LBA1 into LBA2 as a separate island. Make sure it is visually the same with the same colour palette"). Its three scenes -- 8, the first
// floor, 40 and 41 -- are separate grids of 64 x 25 x 64 cells; the joined map (Lba1Areas, "Temple of Bú") places them where their
// cube-change zones meet: 40 eight layers under 8 to its south-east, down the corridor's stairs, 41 east of 8.
//
// The layout's frame: cells (x, layer, z) with 8's grid at the origin; a layer is 256 world units, a cell 512, as in LBA2.
internal sealed class TempleLayout
{
    public const int FirstScene = 8;
    public static readonly int[] Scenes = { 8, 40, 41 };

    // Where a scene's grid lies in the layout (its cell 0,0,0), in cells and layers.
    public sealed record Placed(int Scene, int X, int Y, int Z);

    public readonly List<Placed> Placements = new();
    public readonly Dictionary<(int X, int Y, int Z), PolarLayout.Cell> Cells = new();
    // the cells that close the slot a join leaves (Seams): each with the cell of the scene's first row it copies
    public readonly Dictionary<(int X, int Y, int Z), (int X, int Y, int Z)> Filled = new();
    public readonly List<string> Log = new();
    public int MinX, MaxX, MinY, MaxY, MinZ, MaxZ;

    public static TempleLayout Build(Lba1Game game)
    {
        var layout = new TempleLayout();
        var area = game.Areas.FirstOrDefault(a => a.Tiles.Any(t => t.Scene == FirstScene))
                   ?? throw new InvalidOperationException("The LBA1 folder's joined maps have no Temple of Bú (scene 8).");
        foreach (var tile in area.Tiles.Where(t => Scenes.Contains(t.Scene)).OrderBy(t => Array.IndexOf(Scenes, t.Scene)))
        {
            int ox = tile.OffsetX / 512, oy = tile.OffsetY / 256, oz = tile.OffsetZ / 512;
            layout.Placements.Add(new Placed(tile.Scene, ox, oy, oz));
            var added = 0;
            foreach (var b in PolarLayout.Bricks(game, tile.Scene))
            {
                if (!tile.Holds(b.X, b.Z)) continue;
                if (layout.Cells.TryAdd((b.X + ox, b.Y + oy, b.Z + oz), new PolarLayout.Cell(tile.Scene, b.Brick, b.Shape, b.Code, b.Block))) added++;
            }
            layout.Log.Add($"scene {tile.Scene} at ({ox}, {oy}, {oz}): {added} cells");
        }
        if (layout.Placements.Count != Scenes.Length)
            throw new InvalidOperationException($"The joined map has {layout.Placements.Count} of the temple's {Scenes.Length} scenes.");
        layout.Seams();
        var keys = layout.Cells.Keys;
        layout.MinX = keys.Min(k => k.X); layout.MaxX = keys.Max(k => k.X);
        layout.MinY = keys.Min(k => k.Y); layout.MaxY = keys.Max(k => k.Y);
        layout.MinZ = keys.Min(k => k.Z); layout.MaxZ = keys.Max(k => k.Z);
        layout.Log.Add($"the temple: {layout.Cells.Count} cells, x {layout.MinX}..{layout.MaxX}, layers {layout.MinY}..{layout.MaxY}, z {layout.MinZ}..{layout.MaxZ}");
        return layout;
    }

    // The slot a join leaves between two scenes: the joined map places 40 four rows further south than its zones do (48, -8, 64 against
    // 60), so the corridor down from 8 -- 8's last row (z 63) stops at its floor, layer 8 -- has no walls or ceiling for one row, a slit
    // the race camera looks through to the void. Where a column of a scene's first row stands against the row before it, which another
    // scene's cells hold, the scene's cells over the other's there are copied into that row: 40's corridor walls (x 49, 53, layers 9-16)
    // and its ceiling (x 50-52, layer 16). Nothing else moves; 41's join with 40 has no such slot (its last row is as tall as 40's first).
    private void Seams()
    {
        foreach (var p in Placements)
        {
            var row = p.Z - 1;
            var columns = Cells.Keys.Where(k => k.Z == p.Z && Cells[k].Scene == p.Scene).Select(k => k.X).Distinct().OrderBy(x => x).ToList();
            var added = 0;
            foreach (var x in columns)
            {
                var before = Cells.Where(c => c.Key.X == x && c.Key.Z == row && c.Value.Scene != p.Scene).Select(c => c.Key.Y).ToList();
                if (before.Count == 0) continue;
                var top = before.Max();
                foreach (var y in Cells.Keys.Where(k => k.X == x && k.Z == p.Z && k.Y > top).Select(k => k.Y).OrderBy(y => y).ToList())
                {
                    if (!Cells.TryAdd((x, y, row), Cells[(x, y, p.Z)])) continue;
                    Filled[(x, y, row)] = (x, y, p.Z);
                    added++;
                }
            }
            if (added > 0) Log.Add($"the join before scene {p.Scene}'s first row (z {p.Z}): {added} of its cells copied into the slot at z {row}");
        }
    }

    // The temple with some cells left out (a race track's doorways: TempleIsland.Build), its frame the same. A cell that closes a join's slot
    // (Filled) goes with the cell it copies: a doorway cut through the corridor's wall there is cut through its copy too.
    public TempleLayout Without(IEnumerable<(int X, int Y, int Z)> cells)
    {
        var copy = new TempleLayout { MinX = MinX, MaxX = MaxX, MinY = MinY, MaxY = MaxY, MinZ = MinZ, MaxZ = MaxZ };
        copy.Placements.AddRange(Placements);
        var gone = cells.ToHashSet();
        foreach (var (fill, from) in Filled) if (gone.Contains(from)) gone.Add(fill);
        foreach (var (k, v) in Cells) if (!gone.Contains(k)) copy.Cells[k] = v;
        foreach (var (k, v) in Filled) if (copy.Cells.ContainsKey(k)) copy.Filled[k] = v;
        return copy;
    }

    // The temple's bricks as one tile, for Lba1GridRenderer (LBA1's own picture of it).
    public Lba1Tile Tile() => new(Cells.Select(c => new Lba1Placement(c.Key.X, c.Key.Y, c.Key.Z, c.Value.Brick)).ToList(), 0, 0, 0);
}
