using System.Numerics;
using LBAAssembler.Lba1;
using LBAAssembler.Terrain.Polar;

namespace LBAAssembler.Terrain.Temple;

// The Temple of Bú's round things -- its barrels, columns, amphorae, goblets and fire bowls -- as solids of revolution, where TempleObjects
// makes every other cell a box with its brick's picture on its sides (2026-10-17, the user's: "improve the graphics for the temple track as
// some things like barrels aren't correctly reproduced in 3D"; a barrel was a block with the barrel's picture painted on each side).
//
// LBA1 places each as a block of its scene's library (a column 1 x 8 x 1 cells, the barrel 2 x 4 x 2), the thing's picture split among the
// block's bricks. The block's bricks drawn together (Picture) are the thing as LBA1's camera sees it: looking down at 30 degrees, so that a
// ring of radius r (cells) about an upright axis is an ellipse 2 r √2 x 24 pixels wide and half as tall, 15 pixels higher on the screen for
// each layer up. Row by row, the picture's width about the axis is the thing's width at the height whose ring is centred on that row; its
// top and bottom are the lowest top and the highest bottom the outline allows (a barrel's lid, a column's capital, flat).
//
// Each cell of it is the part of the thing in the cell (CellFaces) -- the ring between the cell's layer's heights, and of the barrel, whose
// axis stands between four cells, the quarter in the cell (and the quarter of a cell LBA1 gives no brick, its back, Drawing) -- so that a
// cell left out leaves out its part, as TempleObjects leaves out a box. Where the race track's doorways cut through a round thing, the rest
// of it goes too (CutRemains: no column's top hanging over the road); where LBA1 placed two overlapping, each is drawn whole (Overlapped).
// Its texture is LBA1's picture on it: each texel the picture's pixel where that point of the surface falls from LBA1's angle, on the half
// of the ring LBA1's camera sees (two tiles: the quarter facing +x, the one facing +z); the back half is its mirror image (Fold), so that
// from LBA1's angle it draws the picture LBA1 draws, its rims' shading too, and all round the barrel's staves and hoops. Where the picture
// has nothing (a goblet's foot, between its points), the texture is see-through. A square plinth LBA1 draws under it (a column's) is a
// square plate, not part of the ring.
internal static class TempleShapes
{
    // a kind of round thing: its name, and the first brick (LBA_BRK entry) of its block -- each of the temple's bricks is in one block --
    // and whether its width at a height is no more than the widest ring's ellipse its picture holds whole there (Inscribed: an amphora,
    // whose handles meet its mouth; not a goblet, whose foot is points with gaps between)
    public sealed record Kind(string Name, int FirstBrick, bool Inscribed = true);
    public static readonly Kind[] Kinds =
    {
        new("barrel", 2631), new("column", 2342), new("amphora", 2616), new("goblet", 2644, Inscribed: false), new("fire bowl", 2255),
    };
    // the scenes whose libraries are looked through for the blocks (the temple's; they share the bricks)
    private static readonly int[] LibraryScenes = TempleLayout.Scenes;
    // the profile's points: a ring this far off the straight line between its neighbours (cells) is kept
    private const double Tolerance = 0.02;
    // the sides of a full ring: one about every this many world units round (a multiple of 4, from 8 to 32)
    private const double SideLength = 380;
    // a bar of the picture this many pixels tall and less isn't the thing's body (above)
    private const int Bar = 6;
    private static readonly double Root2 = Math.Sqrt(2);

    // A kind as its block has it.
    public sealed class Shape
    {
        public required Kind Kind { get; init; }
        public int Dx, Dy, Dz;
        // the axis, in the block's cells (the middle of its footprint)
        public double Ax, Az;
        // the block's picture as LBA1 draws it (LBA1 palette indices, -1 where nothing), and where the block's point (0, 0, 0) is drawn
        public int[] Pixels = Array.Empty<int>();
        public int Width, Height, OriginX, OriginY;
        // the profile: the rings (height in layers from the block's foot, radius in cells), bottom to top; the first is the bottom, the last
        // the top
        public List<(double Y, double R)> Rings = new();
        public int Sides;
        public double Bottom => Rings[0].Y;
        public double Top => Rings[^1].Y;
        // the block's cells that have a brick (LBA1 leaves out a cell its picture never shows: the barrel's back quarter, but on its top)
        public HashSet<(int I, int J, int K)> Bricked = new();
        // those bricks, each at its place in the block (a brick may be at more than one: PartAt)
        public List<(int Brick, int I, int J, int K)> Bricks = new();
        // the square plinth it stands on, its top's height (layers; 0: none) -- a plate the block's footprint wide, under its bottom ring
        public double Plinth;

        // The screen point of a point of the block (cells, layers).
        public (double X, double Y) Screen(double x, double y, double z) => (OriginX + 24 + 24 * (x - z), OriginY + 15 + 12 * (x + z) - 15 * y);
        public int At(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? -1 : Pixels[y * Width + x];

        // The radius at a height (between the rings, along the straight line between them).
        public double RadiusAt(double y)
        {
            if (y <= Rings[0].Y) return Rings[0].R;
            for (var i = 1; i < Rings.Count; i++)
                if (y <= Rings[i].Y)
                {
                    var (y0, r0) = Rings[i - 1]; var (y1, r1) = Rings[i];
                    return y1 - y0 < 1e-9 ? r1 : r0 + (r1 - r0) * (y - y0) / (y1 - y0);
                }
            return Rings[^1].R;
        }

        // The colour of the picture where a point of the surface falls from LBA1's angle (-1: nothing there) -- of the half LBA1's camera
        // sees (from -45 to 135 degrees round, the x axis's way to the z axis's); the back half is its mirror image (Fold): a barrel's
        // staves all round, and no smear of the outline's few pixels where LBA1 sees the ring edge on.
        public int Colour(double angle, double y, double r)
        {
            angle = Fold(angle);
            return Sample(Ax + r * Math.Cos(angle), y, Az + r * Math.Sin(angle));
        }

        // The picture's pixel at a point of the block.
        public int Sample(double x, double y, double z)
        {
            var (sx, sy) = Screen(x, y, z);
            return At((int)Math.Floor(sx), (int)Math.Floor(sy));
        }
    }

    // An angle round the ring (radians) as the angle of the half LBA1's camera sees (-π/4..3π/4) whose texture it has: the back half's
    // mirrored across the plane through the axis square to LBA1's view. (2026-10-18: the front quarter alone, 0..π/2, mirrored to the
    // quarters either side, drew the bright middle of the picture again where LBA1 shades the rims dark -- columns, amphorae, goblets
    // light-rimmed from LBA1's angle.)
    public static double Fold(double angle)
    {
        var a = angle % (2 * Math.PI);
        if (a < 0) a += 2 * Math.PI;
        if (a >= 7 * Math.PI / 4) return a - 2 * Math.PI;
        return a <= 3 * Math.PI / 4 ? a : 3 * Math.PI / 2 - a;
    }

    // The side tile of a folded angle (the quarter facing +x, -π/4..π/4, or the one facing +z, π/4..3π/4) and its place across it (0..1).
    private static (Tile Tile, double U) SideOf(double folded, bool facingZ)
        => facingZ ? (Tile.SideZ, (folded - Math.PI / 4) / (Math.PI / 2)) : (Tile.SideX, (folded + Math.PI / 4) / (Math.PI / 2));

    // A cell of a round thing: its kind and its place in the block.
    public readonly record struct Part(Shape Shape, int I, int J, int K);

    // The part a cell of the layout is: its brick's (Find) -- or, of a brick its block has at more than one place, the place whose block's
    // other bricks the layout has round it (TempleModels.Place: none of the round things' blocks has one now, but a grille's had).
    public static Part PartAt(Dictionary<int, Part> parts, Dictionary<(int X, int Y, int Z), PolarLayout.Cell> cells, (int X, int Y, int Z) at)
    {
        var brick = cells[at].Brick;
        var part = parts[brick];
        var places = part.Shape.Bricks.Where(b => b.Brick == brick).Select(b => (b.I, b.J, b.K)).ToList();
        if (places.Count < 2) return part;
        var (i, j, k) = TempleModels.Place(part.Shape.Bricks, places, at, cells);
        return part with { I = i, J = j, K = k };
    }

    // The round things' bricks: each, the thing and its place in the thing's block (its first, of a brick at more than one: PartAt). (Kinds
    // whose block no library has are left out.)
    public static Dictionary<int, Part> Find(Lba1Game game, List<string>? log = null)
    {
        var parts = new Dictionary<int, Part>();
        var libraries = LibraryScenes.Select(game.ReadBlocks).ToList();
        foreach (var kind in Kinds)
        {
            foreach (var lib in libraries)
            {
                var count = BitConverter.ToInt32(lib, 0) / 4;
                for (var block = 0; block < count; block++)
                {
                    var at = BitConverter.ToInt32(lib, block * 4);
                    int dx = lib[at], dy = lib[at + 1], dz = lib[at + 2];
                    var bricks = new List<(int Brick, int I, int J, int K)>();
                    for (var p = 0; p < dx * dy * dz; p++)
                    {
                        var number = BitConverter.ToUInt16(lib, at + 3 + p * 4 + 2);
                        if (number != 0) bricks.Add((number - 1, p % dx, p / dx % dy, p / dx / dy));
                    }
                    if (bricks.Count == 0 || bricks.Min(b => b.Brick) != kind.FirstBrick) continue;
                    var shape = Analyse(game, kind, dx, dy, dz, bricks);
                    foreach (var (brick, i, j, k) in bricks)
                        if (!parts.TryGetValue(brick, out var had) || had.Shape != shape) parts[brick] = new Part(shape, i, j, k);
                    log?.Add($"{kind.Name}: block of {dx} x {dy} x {dz} cells, {shape.Sides} sides round, from {shape.Bottom:0.00} to {shape.Top:0.00} layers, " +
                             $"{shape.Rings.Count} rings, widest {shape.Rings.Max(r => r.R):0.00} cells" + (shape.Plinth > 0 ? $", on a square plinth {shape.Plinth:0.00} layers tall" : ""));
                    goto found;
                }
            }
            log?.Add($"{kind.Name}: no block of the temple's libraries starts with brick {kind.FirstBrick}");
            found: ;
        }
        return parts;
    }

    // The cells of round things a race track's cuts (TempleIsland: its road's doorways) leave hanging: of a thing the cuts take cells of,
    // the rest -- a column's top over the road where its foot was -- and of a thing standing on a cell the cuts or this take (a fire bowl on
    // that column, a goblet on a wall cut through), all of it.
    public static HashSet<(int X, int Y, int Z)> CutRemains(Dictionary<int, Part> parts, Dictionary<(int X, int Y, int Z), PolarLayout.Cell> cells,
        ICollection<(int X, int Y, int Z)> cut)
    {
        var gone = new HashSet<(int X, int Y, int Z)>(cut.Where(cells.ContainsKey));
        var remains = new HashSet<(int X, int Y, int Z)>();
        var hit = new Queue<(int X, int Y, int Z)>(gone);
        while (hit.Count > 0)
        {
            var c = hit.Dequeue();
            // (the thing this cell is part of, and the thing standing on it)
            foreach (var at in new (int X, int Y, int Z)[] { c, (c.X, c.Y + 1, c.Z) })
            {
                if (!cells.TryGetValue(at, out var cell) || !parts.ContainsKey(cell.Brick)) continue;
                var part = PartAt(parts, cells, at);
                if (at != c && (part.J != 0 || gone.Contains(at))) continue;
                var anchor = (X: at.X - part.I, Y: at.Y - part.J, Z: at.Z - part.K);
                foreach (var (brick, i, j, k) in part.Shape.Bricks)
                {
                    var other = (anchor.X + i, anchor.Y + j, anchor.Z + k);
                    if (!cells.TryGetValue(other, out var o) || o.Brick != brick || !gone.Add(other)) continue;
                    remains.Add(other);
                    hit.Enqueue(other);
                }
            }
        }
        return remains;
    }

    // The parts of round things whose cells another round thing's part took (LBA1 placing two barrels or columns overlapping, one's picture
    // in front of the other's): each with its thing's block's point (0, 0, 0) in the layout, drawn by a cell its thing still has -- so
    // that both are whole, not one with a quarter or its lid's back missing.
    public static Dictionary<(int X, int Y, int Z), List<(Part Part, (int X, int Y, int Z) Anchor)>> Overlapped(Dictionary<int, Part> parts,
        Dictionary<(int X, int Y, int Z), PolarLayout.Cell> cells)
    {
        var things = cells.Where(c => parts.ContainsKey(c.Value.Brick))
            .GroupBy(c => { var p = PartAt(parts, cells, c.Key); return (p.Shape, Anchor: (X: c.Key.X - p.I, Y: c.Key.Y - p.J, Z: c.Key.Z - p.K)); });
        var orphans = new Dictionary<(int X, int Y, int Z), List<(Part, (int, int, int))>>();
        foreach (var thing in things)
        {
            var (shape, anchor) = thing.Key;
            var kept = thing.Select(c => c.Key).OrderBy(c => c.Y).ThenBy(c => c.Z).ThenBy(c => c.X).ToList();
            foreach (var (brick, i, j, k) in shape.Bricks)
            {
                var at = (anchor.X + i, anchor.Y + j, anchor.Z + k);
                if (!cells.TryGetValue(at, out var other) || other.Brick == brick || !parts.ContainsKey(other.Brick)) continue;
                // (drawn by its thing's cell in the same layer, or the nearest)
                var keeper = kept.MinBy(c => Math.Abs(c.Y - at.Item2) * 100 + Math.Abs(c.X - at.Item1) + Math.Abs(c.Z - at.Item3));
                if (!orphans.TryGetValue(keeper, out var list)) orphans[keeper] = list = new();
                list.Add((new Part(shape, i, j, k), anchor));
            }
        }
        return orphans;
    }

    // A block's picture, and the profile read from it.
    private static Shape Analyse(Lba1Game game, Kind kind, int dx, int dy, int dz, List<(int Brick, int I, int J, int K)> bricks)
    {
        if (!(dx == dz && dx is 1 or 2)) throw new InvalidOperationException($"The {kind.Name}'s block is {dx} x {dz}: only 1 x 1 and 2 x 2 footprints are turned.");
        var shape = new Shape { Kind = kind, Dx = dx, Dy = dy, Dz = dz, Ax = dx / 2.0, Az = dz / 2.0, Bricked = bricks.Select(b => (b.I, b.J, b.K)).ToHashSet(), Bricks = bricks };

        // ---- the picture: the block's bricks drawn as LBA1 draws them, far to near (z, then x, then y)
        var sprites = bricks.Select(b => (b.I, b.J, b.K, Sprite: PolarTextures.Sprite.Decode(game.ReadBrick(b.Brick)))).OrderBy(b => b.K).ThenBy(b => b.I).ThenBy(b => b.J).ToList();
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var (i, j, k, s) in sprites)
        {
            int left = 24 * (i - k) + s.HotX, top = 12 * (i + k) - 15 * j + s.HotY;
            minX = Math.Min(minX, left); minY = Math.Min(minY, top); maxX = Math.Max(maxX, left + s.Width); maxY = Math.Max(maxY, top + s.Lines);
        }
        shape.Width = maxX - minX; shape.Height = maxY - minY; shape.OriginX = -minX; shape.OriginY = -minY;
        shape.Pixels = new int[shape.Width * shape.Height];
        Array.Fill(shape.Pixels, -1);
        foreach (var (i, j, k, s) in sprites)
        {
            int left = 24 * (i - k) + s.HotX + shape.OriginX, top = 12 * (i + k) - 15 * j + s.HotY + shape.OriginY;
            for (var y = 0; y < s.Lines; y++)
                for (var x = 0; x < s.Width; x++)
                    if (s.Pixels[y * s.Width + x] is >= 0 and var c) shape.Pixels[(top + y) * shape.Width + left + x] = c;
        }

        // ---- the width row by row: the run of drawn pixels about the axis (gaps of two pixels closed: LBA1's dithering), as far as it
        // reaches the less far way (a handle on one side isn't the body); of the picture without its thin bars (fewer than Bar pixels
        // tall: the top of an amphora's handles, which meet its mouth)
        var (axisX, _) = shape.Screen(shape.Ax, 0, shape.Az);
        var thick = new bool[shape.Pixels.Length];
        for (var y = 0; y < shape.Height; y++)
            for (var x = 0; x < shape.Width; x++)
            {
                // (an opening up and down: a pixel of some Bar pixels drawn one over another)
                for (var y0 = y - Bar + 1; y0 <= y && !thick[y * shape.Width + x]; y0++)
                    if (Enumerable.Range(y0, Bar).All(r => shape.At(x, r) >= 0)) thick[y * shape.Width + x] = true;
            }
        bool Thick(int x, int y) => x >= 0 && y >= 0 && x < shape.Width && y < shape.Height && thick[y * shape.Width + x];
        bool Drawn(int x, int y) => Thick(x, y) || (Thick(x - 1, y) || Thick(x - 2, y)) && (Thick(x + 1, y) || Thick(x + 2, y));
        var half = new double[shape.Height];
        int topRow = int.MaxValue, bottomRow = -1;
        for (var y = 0; y < shape.Height; y++)
        {
            int c0 = (int)Math.Floor(axisX - 0.5), c1 = c0 + 1;
            if (!Drawn(c0, y) && !Drawn(c1, y)) continue;
            int left = Drawn(c0, y) ? c0 : c1, right = Drawn(c1, y) ? c1 : c0;
            while (Drawn(left - 1, y)) left--;
            while (Drawn(right + 1, y)) right++;
            half[y] = Math.Min(axisX - left, right + 1 - axisX);
            topRow = Math.Min(topRow, y); bottomRow = Math.Max(bottomRow, y);
        }
        var (_, foot) = shape.Screen(shape.Ax, 0, shape.Az);
        // ---- a square plinth under it: a row near its foot as wide as the footprint's diamond, and the rows over it as wide as the diamond
        // is there (LBA1's column stands on one, a plate). The rings stop over it; the plate is drawn square (CellFaces).
        // (the rows' runs about the axis as the picture draws them, its thin tips too)
        double RawHalf(int y)
        {
            int c = (int)Math.Floor(axisX);
            if (shape.At(c, y) < 0) return 0;
            int left = c, right = c;
            while (shape.At(left - 1, y) >= 0) left--;
            while (shape.At(right + 1, y) >= 0) right++;
            return Math.Min(axisX - left, right + 1 - axisX);
        }
        var plinthRow = -1;
        if (dx == 1)
        {
            var widestFoot = Enumerable.Range(Math.Max(topRow, bottomRow - 16), bottomRow - Math.Max(topRow, bottomRow - 16) + 1).MaxBy(RawHalf);
            if (RawHalf(widestFoot) >= 24 * dx - 2) plinthRow = widestFoot;
        }
        if (plinthRow >= 0)
        {
            var y = plinthRow;
            while (y - 1 >= topRow && Math.Abs(RawHalf(y - 1) - (24 * dx - 2 * (plinthRow - y + 1))) <= 1.5) y--;
            bottomRow = y - 1;
            shape.Plinth = Math.Max(0.06, (foot - (plinthRow + 0.5)) / 15);
        }
        // (each row's radius -- a median of five rows against stray pixels -- at the height whose ring is centred on it, no wider than the
        // footprint and a little more: a column's capital overhangs its cell)
        var limit = dx / 2.0 * 1.12;
        var profile = new List<(int Row, double Y, double R)>();
        for (var y = topRow; y <= bottomRow; y++)
        {
            var near = Enumerable.Range(y - 2, 5).Select(r => r >= 0 && r <= bottomRow ? half[r] : 0).OrderBy(v => v).ElementAt(2);
            // (with Inscribed, no wider than the widest ellipse centred there that the picture holds whole)
            if (kind.Inscribed) near = Math.Min(near, InscribedHalfWidth(axisX, y + 0.5, near, Drawn));
            profile.Add((y, (foot - (y + 0.5)) / 15, Math.Min(limit, near / (24 * Root2))));
        }

        // ---- its top and bottom: from its widest ring, up to the first whose ellipse reaches the picture's top row (its far edge there),
        // down to the first whose near edge reaches the bottom row
        var widest = profile.IndexOf(profile.MaxBy(p => p.R));
        int topAt = widest, bottomAt = widest;
        while (topAt > 0 && profile[topAt].Row + 0.5 - 12 * Root2 * profile[topAt].R > topRow + 1.5) topAt--;
        while (bottomAt < profile.Count - 1 && profile[bottomAt].Row + 0.5 + 12 * Root2 * profile[bottomAt].R < bottomRow - 0.5) bottomAt++;
        // (inside the block's layers)
        var points = profile.Skip(topAt).Take(bottomAt - topAt + 1).Select(p => (p.Y, p.R)).Reverse().Where(p => p.Y >= 0 && p.Y <= dy).ToList();
        if (points.Count < 2) throw new InvalidOperationException($"The {kind.Name}'s picture gives no profile.");

        // ---- the rings: the profile's points simplified (Douglas-Peucker), and every layer's height between (the cells' parts end there)
        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        void Simplify(int a, int b)
        {
            if (b <= a + 1) return;
            var (worst, at) = (0.0, -1);
            for (var i = a + 1; i < b; i++)
            {
                var t = (points[i].Y - points[a].Y) / Math.Max(1e-9, points[b].Y - points[a].Y);
                var d = Math.Abs(points[i].R - (points[a].R + t * (points[b].R - points[a].R)));
                if (d > worst) (worst, at) = (d, i);
            }
            if (worst <= Tolerance) return;
            keep[at] = true;
            Simplify(a, at); Simplify(at, b);
        }
        Simplify(0, points.Count - 1);
        var rings = points.Where((_, i) => keep[i]).ToList();
        // (down to its plinth's top)
        if (shape.Plinth > 0 && rings[0].Y > shape.Plinth + 1e-6) rings.Insert(0, (shape.Plinth, rings[0].R));
        shape.Rings = rings;
        foreach (var layer in Enumerable.Range(1, dy - 1).Where(l => l > rings[0].Y && l < rings[^1].Y && rings.All(r => Math.Abs(r.Y - l) > 1e-6)))
            rings.Add((layer, shape.RadiusAt(layer)));
        shape.Rings = rings.OrderBy(r => r.Y).ToList();

        // ---- its sides: about SideLength apart round its widest ring, a multiple of 4 (the quarters' ends on corners)
        var round = 2 * Math.PI * shape.Rings.Max(r => r.R) * TempleFrame.Scale * 512;
        shape.Sides = Math.Clamp((int)Math.Round(round / SideLength / 4) * 4, 8, 32);
        return shape;
    }

    // The tiles a cell's part is drawn with: its sides (a layer of the half of the ring LBA1 sees, a quarter each: the one facing +x, from
    // -45 to 45 degrees round, and the one facing +z, from 45 to 135; u round it from the x axis's way, v down the layer -- the back half
    // their mirror image, Fold), its top (the cell's square from above: u along x, v along z), its bottom (likewise), and its plinth's top
    // (the cell's square from above at the plinth's height), each in LBA1's colours (-1 where the picture has nothing).
    public enum Tile { SideX, SideZ, Top, Bottom, Plinth }
    public static (int W, int H) Size(Tile tile) => tile is Tile.SideX or Tile.SideZ ? (PolarTextures.TileSize, PolarTextures.TileSize / 2) : (PolarTextures.TileSize, PolarTextures.TileSize);

    // the tiles a cell's part is drawn with (its faces': CellFaces)
    public static IEnumerable<(Part Of, Tile Tile)> Tiles(Part part) => CellFaces(part, bottom: true).Select(f => (f.Of, f.Tile)).Distinct();

    // The parts of the thing a cell draws: its own; and in a 2 x 2 block, a quarter of the cell's layer that has no brick -- the barrel's
    // back quarter, which LBA1 never draws -- the brick across from it draws too (or another of the layer's).
    public static IEnumerable<Part> Drawing(Part part)
    {
        yield return part;
        var s = part.Shape;
        if (s.Dx == 1) yield break;
        foreach (var (i, k) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
        {
            if (s.Bricked.Contains((i, part.J, k))) continue;
            var keeper = s.Bricked.Contains((1 - i, part.J, 1 - k)) ? (1 - i, 1 - k)
                : new[] { (0, 0), (1, 0), (0, 1), (1, 1) }.First(c => s.Bricked.Contains((c.Item1, part.J, c.Item2)));
            if (keeper == (part.I, part.K)) yield return part with { I = i, K = k };
        }
    }

    // The arc of the ring in a cell of the block (radians): all of it in a 1 x 1 block, the quarter toward the cell in a 2 x 2 one -- its
    // corners' angles round, the side tiles' ends (45 degrees and every quarter turn on) among them: a 1 x 1 ring of 12 sides turned half
    // a side, else a corner put in where one falls between two.
    private static List<double> Arc(Part part)
    {
        var s = part.Shape;
        double from, to; int steps;
        if (s.Dx == 1) { from = s.Sides % 8 == 0 ? 0 : Math.PI / s.Sides; to = from + 2 * Math.PI; steps = s.Sides; }
        else { from = (part.I, part.K) switch { (1, 1) => 0.0, (0, 1) => 0.5, (0, 0) => 1.0, _ => 1.5 } * Math.PI; to = from + Math.PI / 2; steps = s.Sides / 4; }
        var angles = Enumerable.Range(0, steps + 1).Select(n => from + (to - from) * n / steps).ToList();
        for (var k = -1; k <= 8; k++)
        {
            var end = Math.PI / 4 + k * Math.PI / 2;
            if (end > from + 1e-6 && end < to - 1e-6 && angles.All(a => Math.Abs(a - end) > 1e-6)) angles.Add(end);
        }
        angles.Sort();
        return angles;
    }

    // A corner of a face between two angles round as its place in its side tile: the tile the face's middle folds into, u across it.
    private static (Tile Tile, double U0, double U1) SidePlace(double a0, double a1)
    {
        var facingZ = Fold((a0 + a1) / 2) > Math.PI / 4;
        var (tile, u0) = SideOf(Fold(a0), facingZ);
        var (_, u1) = SideOf(Fold(a1), facingZ);
        return (tile, Math.Clamp(u0, 0, 1), Math.Clamp(u1, 0, 1));
    }

    // The half-width (pixels) of the widest ellipse of a ring centred at (x, y) on the screen -- twice as wide as tall -- whose pixels are
    // all drawn, up to `most`.
    private static double InscribedHalfWidth(double x, double y, double most, Func<int, int, bool> drawn)
    {
        var a = 0.0;
        for (var tried = 0.5; tried <= most; tried += 0.5)
        {
            var b = tried / 2;
            for (var py = (int)Math.Floor(y - b); py <= (int)Math.Ceiling(y + b); py++)
                for (var px = (int)Math.Floor(x - tried); px <= (int)Math.Ceiling(x + tried); px++)
                {
                    double dx = (px + 0.5 - x) / tried, dy = (py + 0.5 - y) / b;
                    if (dx * dx + dy * dy <= 1 && !drawn(px, py)) return a;
                }
            a = tried;
        }
        return a;
    }

    // A tile of a cell's part (LBA1's colours, -1 where the picture has nothing), and how much of it the picture draws (of a cap, of the
    // part of the cell the cap covers).
    public static (int[] Pixels, double Drawn) TileOf(Part part, Tile tile)
    {
        var s = part.Shape;
        var (w, h) = Size(tile);
        var pixels = new int[w * h];
        int drawn = 0, counted = 0;
        for (var j = 0; j < h; j++)
            for (var i = 0; i < w; i++)
            {
                double u = (i + 0.5) / w, v = (j + 0.5) / h;
                int c;
                var inside = true;
                if (tile is Tile.SideX or Tile.SideZ)
                {
                    var y = Math.Clamp(part.J + 1 - v, s.Bottom, s.Top);
                    c = s.Colour((tile == Tile.SideX ? -Math.PI / 4 : Math.PI / 4) + u * Math.PI / 2, y, s.RadiusAt(y));
                }
                else if (tile == Tile.Plinth)
                {
                    // (the plinth's top, the cell's square from above as LBA1 sees it)
                    c = s.Sample(part.I + u, s.Plinth, part.K + v);
                }
                else
                {
                    // (a cap: the point of the cell's square at the thing's top, seen from above as LBA1 sees it; a bottom, which LBA1's
                    // angle never shows, the bottom ring's colour the way the point is from the axis)
                    double x = part.I + u, z = part.K + v, y = tile == Tile.Top ? s.Top : s.Bottom;
                    c = tile == Tile.Top ? s.Sample(x, y, z) : s.Colour(Math.Atan2(z - s.Az, x - s.Ax), y + 0.02, s.RadiusAt(y + 0.02));
                    inside = Math.Sqrt((x - s.Ax) * (x - s.Ax) + (z - s.Az) * (z - s.Az)) <= s.RadiusAt(y);
                }
                pixels[j * w + i] = c;
                if (!inside) continue;
                counted++;
                if (c >= 0) drawn++;
            }
        return (pixels, counted == 0 ? 0 : (double)drawn / counted);
    }

    // A tile as it is drawn: one the picture draws TempleObjects.Solid of and more, solid -- its few gaps the nearest drawn texel's colour;
    // less, see-through where the picture has nothing (-1).
    public static (int[] Pixels, bool Solid) Finished(Tile tile, (int[] Pixels, double Drawn) made)
    {
        var (w, _) = Size(tile);
        var solid = made.Drawn >= TempleObjects.Solid;
        var pixels = (int[])made.Pixels.Clone();
        var drawn = Enumerable.Range(0, pixels.Length).Where(k => pixels[k] >= 0).ToList();
        if (!solid || drawn.Count == 0) return (pixels, solid && drawn.Count > 0);
        for (var k = 0; k < pixels.Length; k++)
        {
            if (pixels[k] >= 0) continue;
            int i = k % w, j = k / w;
            pixels[k] = made.Pixels[drawn.MinBy(d => (d % w - i) * (d % w - i) + (d / w - j) * (d / w - j))];
        }
        return (pixels, true);
    }

    // A face of a cell's part: its corners (the block's cells and layers, from the block's point (0, 0, 0)), their places in its tile (0..1),
    // which tile of which part (Drawing), and the way it faces (out of the thing).
    public readonly record struct ShapeFace(Vector3[] Corners, (double U, double V)[] Uv, Part Of, Tile Tile, Vector3 Normal);

    // The faces of a cell's part (and of the quarters it draws for cells with no brick, Drawing): the ring's quads between the rings in its
    // layer, its top's fan where the thing ends in its layer (and its bottom's, when `bottom`: nothing under it to stand on), and its plinth,
    // a square plate (its top, its sides, its bottom when `bottom`).
    public static List<ShapeFace> CellFaces(Part part, bool bottom)
    {
        var s = part.Shape;
        var faces = new List<ShapeFace>();
        if (s.Top <= part.J || s.Bottom >= part.J + 1) return faces;
        foreach (var of in Drawing(part)) Faces(of, bottom, faces);
        return faces;
    }

    private static void Faces(Part part, bool bottom, List<ShapeFace> faces)
    {
        var s = part.Shape;
        double y0 = Math.Max(part.J, s.Bottom), y1 = Math.Min(part.J + 1, s.Top);
        var rings = new List<(double Y, double R)> { (y0, s.RadiusAt(y0)) };
        rings.AddRange(s.Rings.Where(r => r.Y > y0 + 1e-6 && r.Y < y1 - 1e-6));
        rings.Add((y1, s.RadiusAt(y1)));
        var arc = Arc(part);
        Vector3 At(double angle, double y, double r) => new((float)(s.Ax + r * Math.Cos(angle)), (float)y, (float)(s.Az + r * Math.Sin(angle)));
        for (var k = 0; k + 1 < rings.Count; k++)
        {
            var (ya, ra) = rings[k]; var (yb, rb) = rings[k + 1];
            if (ra < 1e-3 && rb < 1e-3) continue;
            for (var n = 0; n + 1 < arc.Count; n++)
            {
                double a0 = arc[n], a1 = arc[n + 1];
                // (u round its side tile: up it on the half LBA1 sees, back down on its mirror image)
                var (side, u0, u1) = SidePlace(a0, a1);
                double v0 = part.J + 1 - ya, v1 = part.J + 1 - yb;
                // (the way it faces, in cells and layers: out from the axis, tilted up or down as the ring narrows or widens)
                var mid = (a0 + a1) / 2;
                var normal = Vector3.Normalize(new Vector3((float)Math.Cos(mid), (float)((ra - rb) / Math.Max(1e-6, yb - ya)), (float)Math.Sin(mid)));
                var corners = new List<(Vector3 P, (double, double) Uv)>
                {
                    (At(a0, ya, ra), (u0, v0)), (At(a1, ya, ra), (u1, v0)), (At(a1, yb, rb), (u1, v1)), (At(a0, yb, rb), (u0, v1)),
                };
                // (a ring of no width -- a cone's tip -- makes it a triangle)
                if (ra < 1e-3) corners.RemoveAt(1);
                else if (rb < 1e-3) corners.RemoveAt(3);
                faces.Add(new ShapeFace(corners.Select(c => c.P).ToArray(), corners.Select(c => c.Uv).ToArray(), part, side, normal));
            }
        }
        // the caps: a fan of triangles from the axis
        void Cap(double y, Tile tile, Vector3 normal)
        {
            var r = s.RadiusAt(y);
            if (r < 1e-3) return;
            var centre = new Vector3((float)s.Ax, (float)y, (float)s.Az);
            (double, double) Uv(Vector3 p) => (p.X - part.I, p.Z - part.K);
            for (var n = 0; n + 1 < arc.Count; n++)
            {
                Vector3 p0 = At(arc[n], y, r), p1 = At(arc[n + 1], y, r);
                faces.Add(new ShapeFace(new[] { centre, p0, p1 }, new[] { Uv(centre), Uv(p0), Uv(p1) }, part, tile, normal));
            }
        }
        if (s.Top <= part.J + 1) Cap(s.Top, Tile.Top, Vector3.UnitY);
        if (s.Plinth > 0 && part.J == 0)
        {
            // the plinth: a square plate under the ring, the cell's square wide -- its top textured from above, its sides with its top's
            // edge there, its bottom where nothing is under it
            float h = (float)s.Plinth, x0 = part.I, x1 = part.I + 1, z0 = part.K, z1 = part.K + 1;
            const double e = 1 / 32.0;
            faces.Add(new ShapeFace(new[] { new Vector3(x0, h, z0), new Vector3(x1, h, z0), new Vector3(x1, h, z1), new Vector3(x0, h, z1) },
                new[] { (0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0) }, part, Tile.Plinth, Vector3.UnitY));
            faces.Add(new ShapeFace(new[] { new Vector3(x1, h, z0), new Vector3(x1, h, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0) },
                new[] { (1 - e, 0.0), (1 - e, 1.0), (1.0, 1.0), (1.0, 0.0) }, part, Tile.Plinth, Vector3.UnitX));
            faces.Add(new ShapeFace(new[] { new Vector3(x0, h, z1), new Vector3(x1, h, z1), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1) },
                new[] { (0.0, 1 - e), (1.0, 1 - e), (1.0, 1.0), (0.0, 1.0) }, part, Tile.Plinth, Vector3.UnitZ));
            faces.Add(new ShapeFace(new[] { new Vector3(x0, h, z0), new Vector3(x0, h, z1), new Vector3(x0, 0, z1), new Vector3(x0, 0, z0) },
                new[] { (e, 0.0), (e, 1.0), (0.0, 1.0), (0.0, 0.0) }, part, Tile.Plinth, -Vector3.UnitX));
            faces.Add(new ShapeFace(new[] { new Vector3(x0, h, z0), new Vector3(x1, h, z0), new Vector3(x1, 0, z0), new Vector3(x0, 0, z0) },
                new[] { (0.0, e), (1.0, e), (1.0, 0.0), (0.0, 0.0) }, part, Tile.Plinth, -Vector3.UnitZ));
            if (bottom)
                faces.Add(new ShapeFace(new[] { new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1) },
                    new[] { (0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0) }, part, Tile.Plinth, -Vector3.UnitY));
        }
        else if (bottom && s.Bottom >= part.J) Cap(s.Bottom, Tile.Bottom, -Vector3.UnitY);
    }
}
