using System.Numerics;
using LBAAssembler.Lba1;
using LBAAssembler.Terrain.Palace;
using LBAAssembler.Terrain.Polar;
using LbaBodyStudio;

namespace LBAAssembler.Terrain.Temple;

// The Temple of Bú's cells as LBA2 island objects. LBA1 draws a cell's brick as an isometric picture of its top and of the two sides its
// camera sees (+x, +z); each cell here is a box -- the part of the cell its brick fills (PolarTextures.Sprite.Footprint: a lamp's pole a thin
// one) -- each face of it textured with that face of the brick's picture, unskewed (the -x and -z sides, which LBA1 never draws, with the
// opposite side's), so that from LBA1's own angle the boxes draw the picture LBA1 draws. Where a brick draws nothing on a face -- the gaps
// of a railing, a cobweb's strands, a skull lamp's surroundings -- the face is see-through (an LBA2 "incrusted" texture: its colour 0 is not
// drawn), as LBA1's picture shows what is behind. A face is left out where what is beyond it hides it: a box as wide that is solid there.
//
// The round things' cells -- barrels, columns, amphorae, goblets, fire bowls (TempleShapes) -- are no boxes: each is its part of the thing
// turned, textured with the thing's own picture the same way, and no box beside it counts it as solid in front of its face. Nor are the
// shaped things' (TempleModels: rocks, grilles, spiked rails, iron fans, statues, stairs, spikes): a cell of one draws the faces of its
// placement's model that lie in it -- but a face against a box that hides it (a step's side against a wall), or turned to a solid box just
// behind it (a rock's back against its wall).
//
// Every face is drawn as it is (the palette's normal level: TemplePalette's row 12, LBA1's colours), unlit -- LBA1's bricks have their light
// drawn in. The bodies only draw (their decors' boxes touch nothing, BrickBuilding.NoBoxTop); what is solid is a set of plain boxes of their
// own, the cells merged (Boxes), each a decor with an empty body -- a round thing's cells' as their boxes were.
internal static class TempleObjects
{
    // a body's cells: at most this many LBA1 columns each way (split again while it is too big for a body)
    public const int Chunk = 6;
    private const int MaxPoints = 500, MaxFaces = 500;
    // a face the brick draws this much of and more is solid (its few gaps -- the picture's jagged edges -- the nearest drawn pixel's
    // colour); less, see-through where it draws nothing
    public const double Solid = 0.9;
    // a face the brick hardly draws (a wall brick's top, a few pixels of its edge) against a cell beside it: left out
    public const double Sliver = 0.15;
    // a brick that draws less than this of each of its box's faces is a wisp (a cobweb's strands): nothing to bump into
    public const double Wisp = 0.35;
    // how far (world units) a box's face goes on past an edge where the same face of the block beside it goes on in its plane: the two
    // overlap a little, a sixth of a texel, instead of meeting edge to edge
    public const double Seam = 8;
    // the engine's polygon types: a textured face drawn as its texture is, and one whose colour 0 isn't drawn (POLY_TEXTURE_SOLID(_INC))
    private const int TextureSolid = 8, TextureSeeThrough = 12;

    private static uint TileEntry(int x, int y) => 0x1F1F0000u | (uint)(y * 256 + x);

    public sealed record Built(List<byte[]> Bodies, int Decors, int Boxes, int Pages, List<string> Log);

    // How many of the temple's pixels each LBA1 colour is: each brick's picture as often as the temple places it (for its palette).
    public static long[] ColourCounts(Lba1Game game, TempleLayout layout)
    {
        var counts = new long[256];
        foreach (var g in layout.Cells.Values.GroupBy(c => c.Brick))
            foreach (var px in PolarTextures.Sprite.Decode(game.ReadBrick(g.Key)).Pixels)
                if (px >= 0) counts[px] += g.Count();
        return counts;
    }

    // `map`: each LBA1 colour's index in the island's palette (TemplePalette). `firstBody`: the OBL entry the bodies start at; `emptyBody`:
    // an empty body for the boxes (its own entry, made by the caller). `shapes`: the round things' bricks (TempleShapes.Find), whose cells
    // are the parts of turned shapes, not boxes; `models`: the shaped things' (TempleModels.Find), whose cells draw their models.
    // `uncut`: the temple as LBA1 has it, when `layout` is a race track's build of it with doorways cut (TempleIsland.Build) -- what LBA1's own
    // picture shows, the columns' cells under a lintel the cuts took. `deck`: that race track's raised road, which the models keep out of
    // (TempleModels.Placed: a row of rocks' lobes cut flat under it).
    public static Built Build(Lba1Game game, TempleLayout layout, IslandFile island, TempleFrame frame, int[] map, int firstBody, int emptyBody,
        Dictionary<int, TempleShapes.Part> shapes, Dictionary<int, TempleModels.Part> models, TempleLayout? uncut = null, TempleModels.DeckCap? deck = null)
    {
        var log = new List<string>();
        var cells = layout.Cells;
        var whole = (uncut ?? layout).Cells;
        var sprites = new Dictionary<int, PolarTextures.Sprite>();
        PolarTextures.Sprite Sprite(int brick) => sprites.TryGetValue(brick, out var s) ? s : sprites[brick] = PolarTextures.Sprite.Decode(game.ReadBrick(brick));
        var boxes = new Dictionary<int, PolarTextures.Box>();
        var wholeBricks = new Dictionary<int, bool>();
        // (LBA1 draws of a brick only the faces its grid leaves to be seen -- a floor's tiles their tops, a wall's bricks the side they face:
        // a brick that draws a face whole is a whole block; one that draws none whole -- a lamp's pole, a rail -- the box its outline fits)
        bool WholeBrick(int brick) => wholeBricks.TryGetValue(brick, out var w) ? w
            : wholeBricks[brick] = Enum.GetValues<PolarTextures.Face>().Any(f => Sprite(brick).Coverage(f) >= Solid);
        PolarTextures.Box BoxOf(int brick)
        {
            if (boxes.TryGetValue(brick, out var b)) return b;
            return boxes[brick] = WholeBrick(brick) ? PolarTextures.Box.Full : Sprite(brick).Footprint();
        }

        // (the cells of LBA1's invisible fillers -- bricks that draw nothing, the slopes it walks its stairs on: nothing to see, nothing to bump
        // into -- left standing on nothing where a race track's doorway took the cells under them: left out with them)
        if (uncut is not null)
        {
            var dropped = new HashSet<(int X, int Y, int Z)>();
            foreach (var c in cells.Keys.OrderBy(k => k.Y))
            {
                var below = (c.X, c.Y - 1, c.Z);
                if (Sprite(cells[c].Brick).Pixels.Any(p => p >= 0)) continue;
                if (dropped.Contains(below) || whole.ContainsKey(below) && !cells.ContainsKey(below)) dropped.Add(c);
            }
            if (dropped.Count > 0)
            {
                cells = cells.Where(c => !dropped.Contains(c.Key)).ToDictionary(c => c.Key, c => c.Value);
                log.Add($"{dropped.Count} cells of LBA1's invisible fillers left over the doorways cut under them taken out: " + string.Join(" ", dropped.OrderBy(c => c.Z).ThenBy(c => c.X).ThenBy(c => c.Y)));
            }
        }

        // each brick's three faces as tiles, in the island's colours (0 where the face is see-through), and whether each is solid
        var faceTiles = new Dictionary<(int Brick, PolarTextures.Face Face), (int[] Tile, bool Solid, double Drawn)>();
        (int[] Tile, bool Solid, double Drawn) TileOf(int brick, PolarTextures.Face face)
        {
            if (faceTiles.TryGetValue((brick, face), out var known)) return known;
            var s = Sprite(brick); var box = BoxOf(brick);
            var (w, h) = PolarTextures.Size(face);
            var tile = new int[w * h];
            var drawn = new List<(int I, int J, int C)>();
            for (var j = 0; j < h; j++)
                for (var i = 0; i < w; i++)
                {
                    var (sx, sy) = PolarTextures.Sprite.Where(face, (i + 0.5) / w, (j + 0.5) / h, box);
                    var c = s.At(sx, sy);
                    tile[j * w + i] = c;
                    if (c >= 0) drawn.Add((i, j, c));
                }
            var share = (double)drawn.Count / tile.Length;
            var solid = share >= Solid;
            for (var k = 0; k < tile.Length; k++)
            {
                if (tile[k] >= 0) { tile[k] = map[tile[k]]; continue; }
                if (!solid || drawn.Count == 0) { tile[k] = 0; continue; }
                int i = k % w, j = k / w;
                tile[k] = map[drawn.MinBy(d => (d.I - i) * (d.I - i) + (d.J - j) * (d.J - j)).C];
            }
            return faceTiles[(brick, face)] = (tile, solid, share);
        }
        // (a whole block's face open to view that its brick draws little or none of -- a floor's edge at a drop, a wall's end, a wall's top
        // where a doorway took what stood on it: drawn whole, as a block's face is -- the face's own picture, its gaps the nearest drawn
        // pixel's colour, or where it draws too little of it, the face it draws most of, at this one's size: a floor's edge in its tiles'
        // colours, a wall's top in its stones', PolarTextures.Sprite.Tile)
        var openTiles = new Dictionary<(int Brick, PolarTextures.Face Face), int[]>();
        int[] OpenTileOf(int brick, PolarTextures.Face face)
            => openTiles.TryGetValue((brick, face), out var known) ? known : openTiles[(brick, face)] = Sprite(brick).Tile(face).Select(c => map[c]).ToArray();

        // the round things (TempleShapes): a cell of one is the part of the thing in it, not a box -- its tiles in the island's colours
        var shapeTiles = new Dictionary<(TempleShapes.Part Of, TempleShapes.Tile Tile), (int[] Tile, bool Solid)>();
        (int[] Tile, bool Solid) ShapeTile(TempleShapes.Part of, TempleShapes.Tile which)
        {
            if (shapeTiles.TryGetValue((of, which), out var known)) return known;
            var (pixels, solid) = TempleShapes.Finished(which, TempleShapes.TileOf(of, which));
            return shapeTiles[(of, which)] = (pixels.Select(c => c >= 0 ? map[c] : 0).ToArray(), solid);
        }

        // the faces each cell shows: its top, its four sides, and its bottom where there is something under it in LBA1's column (an arch's
        // underside, a bridge's, a lintel's over a race track's doorway) or, a whole block's, over the void beside a room (Overhang) -- each
        // left out where it lies on its cell's side and the box beyond is solid and as wide there
        var lowest = new Dictionary<(int X, int Z), int>();
        foreach (var (x, y, z) in whole.Keys) lowest[(x, z)] = lowest.TryGetValue((x, z), out var l) ? Math.Min(l, y) : y;
        // (a column the race fills where LBA1 has none -- the secret party's bridge, TempleHalloween.PartyWalls: its own lowest)
        var own = new Dictionary<(int X, int Z), int>();
        foreach (var (x, y, z) in layout.Cells.Keys)
            if (!lowest.ContainsKey((x, z))) own[(x, z)] = own.TryGetValue((x, z), out var o) ? Math.Min(o, y) : y;
        foreach (var (column, y) in own) lowest[column] = y;

        // LBA1's own picture of the temple as it has it (Lba1GridRenderer's order: by z, by x, up the layers; not the cells that close the
        // 8|40 join's slot, TempleLayout.Filled), each pixel the last brick to draw it (its place in that order, from 1; 0 none) -- where a
        // face's brick draws nothing, does LBA1 show what is behind it (no later brick covering it), or is it out of LBA1's sight?
        var lba1Order = new Dictionary<(int X, int Y, int Z), int>();
        ushort[]? picture = null;
        int pictureW = 0, pictureH = 0, pictureU0 = 0, pictureV0 = 0;
        void Picture()
        {
            var filled = (uncut ?? layout).Filled;
            var ordered = whole.Keys.Where(k => !filled.ContainsKey(k)).OrderBy(k => k.Z).ThenBy(k => k.X).ThenBy(k => k.Y).ToList();
            if (ordered.Count >= ushort.MaxValue) throw new InvalidOperationException("The temple has too many cells for its picture.");
            int u0 = int.MaxValue, u1 = int.MinValue, v0 = int.MaxValue, v1 = int.MinValue;
            foreach (var k in ordered)
            {
                var s = Sprite(whole[k].Brick);
                int ax = 24 * (k.X - k.Z) + s.HotX, ay = 12 * (k.X + k.Z) - 15 * k.Y + s.HotY;
                u0 = Math.Min(u0, ax); u1 = Math.Max(u1, ax + s.Width); v0 = Math.Min(v0, ay); v1 = Math.Max(v1, ay + s.Lines);
            }
            pictureU0 = u0; pictureV0 = v0; pictureW = u1 - u0; pictureH = v1 - v0;
            picture = new ushort[pictureW * pictureH];
            for (var i = 0; i < ordered.Count; i++)
            {
                var k = ordered[i];
                var s = Sprite(whole[k].Brick);
                int ax = 24 * (k.X - k.Z) + s.HotX - u0, ay = 12 * (k.X + k.Z) - 15 * k.Y + s.HotY - v0;
                for (var y = 0; y < s.Lines; y++)
                    for (var x = 0; x < s.Width; x++)
                        if (s.Pixels[y * s.Width + x] >= 0) picture[(ay + y) * pictureW + ax + x] = (ushort)(i + 1);
                lba1Order[k] = i + 1;
            }
        }
        var gapsSeen = new Dictionary<((int X, int Y, int Z) Cell, int Dir), bool>();
        // (a face LBA1 sees -- the top, +x, +z: is any texel its brick leaves undrawn a pixel no brick drawn after it covers?)
        bool Lba1SeesGaps((int X, int Y, int Z) c, int dir)
        {
            if (gapsSeen.TryGetValue((c, dir), out var known)) return known;
            if (picture is null) Picture();
            // (a cell LBA1 hasn't: none of its faces in sight)
            if (!lba1Order.TryGetValue(c, out var mine)) return gapsSeen[(c, dir)] = false;
            var face = FaceOf(dir);
            var s = Sprite(whole[c].Brick);
            var (w, h) = PolarTextures.Size(face);
            int ax = 24 * (c.X - c.Z) - pictureU0, ay = 12 * (c.X + c.Z) - 15 * c.Y - pictureV0;
            for (var j = 0; j < h; j++)
                for (var i = 0; i < w; i++)
                {
                    var (sx, sy) = PolarTextures.Sprite.Where(face, (i + 0.5) / w, (j + 0.5) / h);
                    if (s.At(sx, sy) >= 0) continue;
                    int px = ax + (int)Math.Floor(sx), py = ay + (int)Math.Floor(sy);
                    if (px < 0 || py < 0 || px >= pictureW || py >= pictureH || picture![py * pictureW + px] < mine) return gapsSeen[(c, dir)] = true;
                }
            return gapsSeen[(c, dir)] = false;
        }
        // (a whole block's face its brick draws too little of, open to view: does LBA1 show it as it draws it -- what is behind seen through
        // its gaps? A face LBA1 sees (top, +x, +z), if it shows what is behind any of its gaps (a railing on a wall's top, a floor's edge at
        // the picture's edge); one it never sees (-x, -z, the bottom: the other side's picture), if that side is a picture with holes LBA1
        // shows through (an arch). Then it is drawn as LBA1 has it; otherwise whole -- a face LBA1 never shows, which only a race track's
        // doorways, the joins or another angle open)
        bool AsLba1Has((int X, int Y, int Z) c, int dir, double drawn)
            => dir is 0 or 1 or 3 ? Lba1SeesGaps(c, dir) : drawn >= Sliver && Lba1SeesGaps(c, Opposite(dir));
        bool Hidden((int X, int Y, int Z) c, int dir)
        {
            var (dx, dy, dz) = Dirs[dir];
            var box = BoxOf(cells[c].Brick);
            var onSide = dir switch { 1 => box.U1 >= 1, 2 => box.U0 <= 0, 3 => box.V1 >= 1, 4 => box.V0 <= 0, _ => true };
            if (!onSide || !cells.TryGetValue((c.X + dx, c.Y + dy, c.Z + dz), out var other)) return false;
            // (the face of the box beyond that lies against this one: solid? -- a round thing's cell has no box, nor a shaped thing's)
            if (shapes.ContainsKey(other.Brick) || models.ContainsKey(other.Brick) || !TileOf(other.Brick, FaceOf(Opposite(dir))).Solid) return false;
            var nb = BoxOf(other.Brick);
            return dir switch
            {
                0 or 5 => nb.U0 <= box.U0 && nb.U1 >= box.U1 && nb.V0 <= box.V0 && nb.V1 >= box.V1,
                1 => nb.U0 <= 0 && nb.V0 <= box.V0 && nb.V1 >= box.V1,
                2 => nb.U1 >= 1 && nb.V0 <= box.V0 && nb.V1 >= box.V1,
                3 => nb.V0 <= 0 && nb.U0 <= box.U0 && nb.U1 >= box.U1,
                _ => nb.V1 >= 1 && nb.U0 <= box.U0 && nb.U1 >= box.U1,
            };
        }
        var shown = new Dictionary<(int X, int Y, int Z), List<int>>();
        // (a round thing's cell: no box faces, its part's faces -- its bottom where nothing is under it -- and those of its thing's parts
        // another round thing's cells took, TempleShapes.Overlapped; each with its thing's block's point in the layout)
        var shapeFaces = new Dictionary<(int X, int Y, int Z), List<(TempleShapes.ShapeFace Face, Vector3 Anchor)>>();
        var orphans = TempleShapes.Overlapped(shapes, cells);
        // (a shaped thing's cell: the faces of its placement's model that lie in it, with the placement's point in the layout -- but those
        // a box hides: one beyond the cell's side the face lies on, whose own face there is solid and covers it; a whole solid block a little
        // way out from the face, a fifth of a cell -- a rock's back against its wall, a statue's foot on its floor)
        bool SolidBlock((int X, int Y, int Z) at) => cells.TryGetValue(at, out var o) && !shapes.ContainsKey(o.Brick) && !models.ContainsKey(o.Brick)
            && BoxOf(o.Brick).IsFull && Enum.GetValues<PolarTextures.Face>().Any(f => TileOf(o.Brick, f).Solid);
        bool ModelFaceHidden((int X, int Y, int Z) c, TempleModels.ModelFace face, Vector3 anchor)
        {
            if (face.Side >= 0)
            {
                var (dx, dy, dz) = Dirs[face.Side];
                if (!cells.TryGetValue((c.X + dx, c.Y + dy, c.Z + dz), out var o) || shapes.ContainsKey(o.Brick) || models.ContainsKey(o.Brick)
                    || !TileOf(o.Brick, FaceOf(Opposite(face.Side))).Solid) return false;
                var nb = BoxOf(o.Brick);
                return face.Side switch
                {
                    0 or 5 => nb.IsFull,
                    1 => nb.U0 <= 0 && nb.V0 <= 0 && nb.V1 >= 1,
                    2 => nb.U1 >= 1 && nb.V0 <= 0 && nb.V1 >= 1,
                    3 => nb.V0 <= 0 && nb.U0 <= 0 && nb.U1 >= 1,
                    _ => nb.V1 >= 1 && nb.U0 <= 0 && nb.U1 >= 1,
                };
            }
            var mid = face.Corners.Aggregate(Vector3.Zero, (a, b) => a + b) / face.Corners.Length + anchor;
            // (out from it: a layer is half a cell)
            var q = mid + Vector3.Normalize(face.Normal * new Vector3(1, 2, 1)) * new Vector3(0.2f, 0.4f, 0.2f);
            return SolidBlock(((int)Math.Floor(q.X), (int)Math.Floor(q.Y), (int)Math.Floor(q.Z)));
        }
        var modelFaces = new Dictionary<(int X, int Y, int Z), List<(TempleModels.ModelFace Face, TempleModels.Model Model, Vector3 Anchor)>>();
        var modelHidden = 0;
        foreach (var (c, placed) in TempleModels.Placed(models, cells, deck, log))
        {
            var kept = placed.Where(f => !ModelFaceHidden(c, f.Face, f.Anchor)).ToList();
            modelHidden += placed.Count - kept.Count;
            if (kept.Count > 0) modelFaces[c] = kept;
        }
        // (a whole block beyond a face hides it, as LBA1's grid has it -- a face between two is inside a wall; anything else -- nothing, a cell
        // a race track's doorway took, a pole, a cobweb, an invisible filler, a round or a shaped thing -- leaves it open to view)
        bool Open((int X, int Y, int Z) c, int dir) => !SolidBlock((c.X + Dirs[dir].Dx, c.Y + Dirs[dir].Dy, c.Z + Dirs[dir].Dz));
        // (a whole block's bottom with nothing under it in its column: the underside of a ledge over a room beside it, seen from the room --
        // but the temple's foot, which nothing is under, and a block whose column beside it holds nothing lower either way out to Reach)
        const int Reach = 3;
        bool Overhang((int X, int Y, int Z) c)
        {
            if (c.Y <= (uncut ?? layout).MinY) return false;
            for (var dz = -Reach; dz <= Reach; dz++)
                for (var dx = -Reach; dx <= Reach; dx++)
                    if ((dx != 0 || dz != 0) && lowest.TryGetValue((c.X + dx, c.Z + dz), out var low) && low < c.Y) return true;
            return false;
        }
        var faceCount = 0;
        // (the faces drawn whole that the brick draws too little of, OpenTileOf; the bottoms over the void)
        var opaque = new HashSet<((int X, int Y, int Z) Cell, int Dir)>();
        var overhangs = 0;
        foreach (var c in cells.Keys)
        {
            if (models.ContainsKey(cells[c].Brick))
            {
                if (modelFaces.ContainsKey(c)) shown[c] = new List<int>();
                continue;
            }
            if (shapes.ContainsKey(cells[c].Brick))
            {
                var part = TempleShapes.PartAt(shapes, cells, c);
                var made = new List<(TempleShapes.ShapeFace, Vector3)>();
                foreach (var (p, (ax, ay, az)) in orphans.GetValueOrDefault(c, new()).Prepend((part, (c.X - part.I, c.Y - part.J, c.Z - part.K))))
                    made.AddRange(TempleShapes.CellFaces(p, bottom: !cells.ContainsKey((ax + p.I, ay + p.J - 1, az + p.K))).Select(f => (f, new Vector3(ax, ay, az))));
                if (made.Count > 0) { shapeFaces[c] = made; shown[c] = new List<int>(); }
                continue;
            }
            var brick = cells[c].Brick;
            var block = SolidBlock(c);
            var list = new List<int>();
            for (var d = 0; d < Dirs.Length; d++)
            {
                var beyond = (c.X + Dirs[d].Dx, c.Y + Dirs[d].Dy, c.Z + Dirs[d].Dz);
                if (d == 5)
                {
                    if (block ? !Open(c, 5) : cells.ContainsKey(beyond)) continue;
                    if (lowest[(c.X, c.Z)] >= c.Y)
                    {
                        if (!block || !Overhang(c)) continue;
                        overhangs++;
                    }
                }
                if (Hidden(c, d)) continue;
                var tile = TileOf(brick, FaceOf(d));
                if (block && !tile.Solid && Open(c, d) && !AsLba1Has(c, d, tile.Drawn))
                {
                    list.Add(d);
                    opaque.Add((c, d));
                    continue;
                }
                // (a face the brick draws nothing of: nothing to draw; hardly anything, against another cell: nothing to see)
                if (tile.Drawn <= 0 || tile.Drawn < Sliver && cells.ContainsKey(beyond)) continue;
                list.Add(d);
            }
            if (list.Count > 0) { shown[c] = list; faceCount += list.Count; }
        }
        var shownFaces = shown.SelectMany(s => s.Value.Where(d => !opaque.Contains((s.Key, d))).Select(d => (cells[s.Key].Brick, FaceOf(d)))).Distinct().ToList();
        log.Add($"{cells.Count} cells, {shown.Count} with faces to draw ({faceCount} faces); {shownFaces.Count(f => !faceTiles[f].Solid)} of the {shownFaces.Count} faces of bricks drawn see-through");
        log.Add($"{opaque.Count} faces of whole blocks open to view that their bricks draw too little of drawn whole (" +
                string.Join(", ", opaque.GroupBy(o => o.Dir).OrderBy(g => g.Key).Select(g => $"{DirNames[g.Key]} {g.Count()}")) +
                $"), LBA1's picture seen through {gapsSeen.Count(g => g.Value)} of {gapsSeen.Count} faces' gaps; {overhangs} bottoms of ledges over the void beside a room");
        var thin =cells.Values.Select(c => c.Brick).Distinct().Count(b => !BoxOf(b).IsFull);
        log.Add($"{thin} of the {cells.Values.Select(c => c.Brick).Distinct().Count()} bricks fill part of their cell (poles, rails, lamps): thinner boxes");
        log.Add("the round things turned: " + string.Join(", ", shapeFaces.GroupBy(s => shapes[cells[s.Key].Brick].Shape.Kind.Name)
            .Select(g => $"{g.Key} {g.Count()} cells ({g.Sum(s => s.Value.Count)} faces)")) +
            $"; {orphans.Values.Sum(o => o.Count)} parts of them drawn whole where another one's cells overlap them");
        log.Add("the shaped things made models: " + string.Join(", ", modelFaces.SelectMany(s => s.Value).GroupBy(f => f.Model.Kind.Name)
            .Select(g => $"{g.Key} {g.Select(f => (f.Model, f.Anchor)).Distinct().Count()} placed ({g.Count()} faces)")) + $"; {modelHidden} of their faces hidden by boxes");

        // the chunks: the cells with faces, by the island's cubes and Chunk x Chunk columns in them
        var chunks = shown.Keys.GroupBy(c => (CubeX: frame.IslandX(c.X) / 64, CubeZ: frame.IslandZ(c.Z) / 64, Cx: Math.DivRem(c.X - frame.FirstColumnOfCubeX(c.X), Chunk).Quotient,
                Cz: Math.DivRem(c.Z - frame.FirstColumnOfCubeZ(c.Z), Chunk).Quotient))
            .OrderBy(g => g.Key.CubeZ).ThenBy(g => g.Key.CubeX).ThenBy(g => g.Key.Cz).ThenBy(g => g.Key.Cx).Select(g => g.ToList()).ToList();

        // the tiles on pages: each tile once a page (the same picture on many bricks' faces once), a chunk's tiles all on one page -- the
        // page being filled, or a new one when they don't fit
        var pages = new PolarTextures.Pages(island.ObjectTexture, PolarTextures.Colours.AsTheyAre(Array.Empty<byte>()), IslandFile.MaxObjectPages);
        // (a face's tile as its brick draws it, or drawn whole: `open`, OpenTileOf)
        var contentKey = new Dictionary<(int Brick, PolarTextures.Face Face, bool Open), string>();
        int[] FaceTile(int brick, PolarTextures.Face face, bool open) => open ? OpenTileOf(brick, face) : TileOf(brick, face).Tile;
        string KeyOf(int brick, PolarTextures.Face face, bool open)
        {
            if (contentKey.TryGetValue((brick, face, open), out var k)) return k;
            var t = FaceTile(brick, face, open);
            return contentKey[(brick, face, open)] = $"{(open ? "o" : "")}{(int)face}:{Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(t.Select(v => (byte)v).ToArray()))}";
        }
        var shapeKey = new Dictionary<(TempleShapes.Part Of, TempleShapes.Tile Tile), string>();
        string ShapeKey(TempleShapes.Part of, TempleShapes.Tile which)
        {
            if (shapeKey.TryGetValue((of, which), out var k)) return k;
            var t = ShapeTile(of, which).Tile;
            return shapeKey[(of, which)] = $"r{(int)which}:{Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(t.Select(v => (byte)v).ToArray()))}";
        }
        // (a shaped thing's tile: its key, its texels in the island's colours, solid)
        var modelTiles = new Dictionary<(TempleModels.Model Model, int Tile), (string Key, int[] Tile, bool Solid)>();
        (string Key, int[] Tile, bool Solid) ModelTile(TempleModels.Model model, int index)
        {
            if (modelTiles.TryGetValue((model, index), out var known)) return known;
            var t = model.Tiles[index];
            var pixels = t.Pixels.Select(c => c >= 0 ? map[c] : 0).ToArray();
            return modelTiles[(model, index)] = ($"m{t.W}x{t.H}:{Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(pixels.Select(v => (byte)v).ToArray()))}", pixels, t.Solid);
        }
        // a brick's tiles: its box's faces', or a round thing's part's, or a shaped thing's (those its cells' faces use)
        var brickFaces = shown.GroupBy(s => cells[s.Key].Brick).ToDictionary(g => g.Key, g => g.SelectMany(s => s.Value.Select(d => (Face: FaceOf(d), Open: opaque.Contains((s.Key, d)))))
            .Distinct().OrderBy(f => f.Face).ThenBy(f => f.Open).ToList());
        var brickShapeTiles = shapeFaces.GroupBy(s => cells[s.Key].Brick).ToDictionary(g => g.Key, g => g.SelectMany(s => s.Value.Select(f => (f.Face.Of, f.Face.Tile))).Distinct().ToList());
        var brickModelTiles = modelFaces.GroupBy(s => cells[s.Key].Brick).ToDictionary(g => g.Key, g => g.SelectMany(s => s.Value.Select(f => (f.Model, f.Face.Tile))).Distinct().ToList());
        List<(string Key, int W, int H, int[] Tile)> TilesOf(int brick)
            => brickModelTiles.TryGetValue(brick, out var modelled)
                ? modelled.Select(t => (ModelTile(t.Model, t.Tile).Key, t.Model.Tiles[t.Tile].W, t.Model.Tiles[t.Tile].H, ModelTile(t.Model, t.Tile).Tile)).DistinctBy(t => t.Item1).ToList()
                : brickShapeTiles.TryGetValue(brick, out var used)
                ? used.Select(t => (ShapeKey(t.Of, t.Tile), TempleShapes.Size(t.Tile).W, TempleShapes.Size(t.Tile).H, ShapeTile(t.Of, t.Tile).Tile)).DistinctBy(t => t.Item1).ToList()
                : brickFaces[brick].Select(f => (KeyOf(brick, f.Face, f.Open), PolarTextures.Size(f.Face).W, PolarTextures.Size(f.Face).H, FaceTile(brick, f.Face, f.Open))).DistinctBy(t => t.Item1).ToList();
        // a cell's polygons: a shaped thing's model's faces, a round thing's part's, or its box's shown faces (corners in the layout's cells
        // and layers)
        List<Polygon> PolygonsOf((int X, int Y, int Z) c)
        {
            var brick = cells[c].Brick;
            if (modelFaces.TryGetValue(c, out var modelled))
                return modelled.Select(m => new Polygon(m.Face.Corners.Select(p => p + m.Anchor).ToArray(), m.Face.Uv, m.Face.Normal, ModelTile(m.Model, m.Face.Tile).Key,
                    m.Model.Tiles[m.Face.Tile].W, m.Model.Tiles[m.Face.Tile].H, ModelTile(m.Model, m.Face.Tile).Solid)).ToList();
            if (shapeFaces.TryGetValue(c, out var made))
                return made.Select(m => new Polygon(m.Face.Corners.Select(p => p + m.Anchor).ToArray(), m.Face.Uv, m.Face.Normal, ShapeKey(m.Face.Of, m.Face.Tile),
                    TempleShapes.Size(m.Face.Tile).W, TempleShapes.Size(m.Face.Tile).H, ShapeTile(m.Face.Of, m.Face.Tile).Solid)).ToList();
            var b = BoxOf(brick);
            double x = c.X + b.U0, xe = c.X + b.U1, z = c.Z + b.V0, ze = c.Z + b.V1;
            double y0 = c.Y, y1 = c.Y + 1;
            var list = new List<Polygon>();
            foreach (var dir in shown[c])
            {
                // (each edge where the same face of the whole block beside it goes on in its plane a hair past it, Seam: the engine's filler
                // rounds a polygon's left and right edges its own ways, which left a crack of the void here and there between two)
                double Past(int ddx, int ddy, int ddz)
                {
                    var at = (c.X + ddx, c.Y + ddy, c.Z + ddz);
                    return b.IsFull && shown.TryGetValue(at, out var other) && other.Contains(dir) && BoxOf(cells[at].Brick).IsFull ? 1 : 0;
                }
                double across = Seam / (512.0 * TempleFrame.Scale), up = Seam / (256.0 * TempleFrame.Scale);
                var (ax, bx) = dir is 1 or 2 ? (x, xe) : (x - across * Past(-1, 0, 0), xe + across * Past(1, 0, 0));
                var (az, bz) = dir is 3 or 4 ? (z, ze) : (z - across * Past(0, 0, -1), ze + across * Past(0, 0, 1));
                var (ay, by) = dir is 0 or 5 ? (y0, y1) : (y0 - up * Past(0, -1, 0), y1 + up * Past(0, 1, 0));
                // corners in order (outward winding); their (u, v) in the tile: the top's u along x, v along z; a side's u along it, v down
                (double X, double Y, double Z)[] corners = dir switch
                {
                    0 => new[] { (ax, by, bz), (bx, by, bz), (bx, by, az), (ax, by, az) },
                    1 => new[] { (bx, by, az), (bx, by, bz), (bx, ay, bz), (bx, ay, az) },
                    2 => new[] { (ax, ay, az), (ax, ay, bz), (ax, by, bz), (ax, by, az) },
                    3 => new[] { (ax, ay, bz), (bx, ay, bz), (bx, by, bz), (ax, by, bz) },
                    4 => new[] { (ax, by, az), (bx, by, az), (bx, ay, az), (ax, ay, az) },
                    _ => new[] { (ax, ay, az), (bx, ay, az), (bx, ay, bz), (ax, ay, bz) },
                };
                (double U, double V) Uv((double X, double Y, double Z) k) => dir switch
                {
                    0 or 5 => ((k.X - x) / (xe - x), (k.Z - z) / (ze - z)),
                    1 or 2 => ((k.Z - z) / (ze - z), (y1 - k.Y) / (y1 - y0)),
                    _ => ((k.X - x) / (xe - x), (y1 - k.Y) / (y1 - y0)),
                };
                var face = FaceOf(dir);
                var open = opaque.Contains((c, dir));
                var (dx, dy, dz) = Dirs[dir];
                list.Add(new Polygon(corners.Select(k => new Vector3((float)k.X, (float)k.Y, (float)k.Z)).ToArray(), corners.Select(Uv).ToArray(), new Vector3(dx, dy, dz),
                    KeyOf(brick, face, open), PolarTextures.Size(face).W, PolarTextures.Size(face).H, open || TileOf(brick, face).Solid));
            }
            return list;
        }
        {
            var all = shown.SelectMany(s => s.Value.Select(d => (Key: KeyOf(cells[s.Key].Brick, FaceOf(d), opaque.Contains((s.Key, d))), Face: FaceOf(d)))).DistinctBy(t => t.Key).ToList();
            log.Add($"the faces shown: {all.Count} different pictures ({all.Count(t => t.Face == PolarTextures.Face.Top)} tops), " +
                    $"{all.Sum(t => PolarTextures.Pages.HalvesOf(PolarTextures.Size(t.Face).H))} half-slots of the {IslandFile.MaxObjectPages * PolarTextures.Pages.Halves} the pages hold");
        }
        // (each brick's faces on one page -- the page being filled when they fit on it, else the next -- in the order the chunks first use
        // the bricks, so that a chunk's are mostly on one page; a picture several bricks share once on a page; a chunk whose bricks are on
        // more than one page a body for each)
        var onPage = new List<HashSet<string>> { new() };
        var pageOf = new Dictionary<int, int>();
        var filling = 0;
        foreach (var chunk in chunks)
            foreach (var brick in chunk.Select(c => cells[c].Brick).Distinct())
            {
                if (pageOf.ContainsKey(brick)) continue;
                var tiles = TilesOf(brick);
                // (a half-slot more than they take: a top needs a slot's two halves together)
                if (tiles.Where(t => !onPage[filling].Contains(t.Key)).Sum(t => PolarTextures.Pages.HalvesOf(t.H)) + 1 > pages.Free(filling))
                {
                    filling++;
                    if (filling >= IslandFile.MaxObjectPages) throw new InvalidOperationException($"The temple's textures take more than {IslandFile.MaxObjectPages} pages.");
                    onPage.Add(new());
                }
                foreach (var t in tiles.Where(t => !onPage[filling].Contains(t.Key)))
                {
                    if (!pages.Add((t.Key, filling), t.Tile, t.W, t.H, filling)) throw new InvalidOperationException("A page took fewer tiles than it had room for.");
                    onPage[filling].Add(t.Key);
                }
                pageOf[brick] = filling;
            }
        var placedChunks = chunks.SelectMany(chunk => chunk.GroupBy(c => pageOf[cells[c].Brick]).Select(g => (Cells: g.ToList(), Page: g.Key))).ToList();
        log.Add($"the textures: {contentKey.Values.Distinct().Count()} different brick faces ({faceTiles.Count} faces of bricks), {shapeKey.Values.Distinct().Count()} of round " +
                $"things and {modelTiles.Values.Select(t => t.Key).Distinct().Count()} of shaped things at {PolarTextures.TileSize} pixels to a cell, on {pages.Count} pages");

        // the bodies: each chunk's faces (halved while too big for a body), a drawn-only decor in its cube
        var bodies = new List<byte[]>();
        var decors = 0; var left = 0;
        var work = new Queue<(List<(int X, int Y, int Z)> Cells, int Page)>(placedChunks);
        while (work.Count > 0)
        {
            var (chunk, page) = work.Dequeue();
            var body = Mesh(chunk.SelectMany(PolygonsOf).ToList(), frame, key => { var (_, x, y) = pages.Place[(key, page)]; return (x, y); }, out var lo, out var hi, out var origin);
            if (body is null)
            {
                // (a cell too big for a body on its own: never, but split no further)
                if (chunk.Count == 1) throw new InvalidOperationException($"The temple's cell {chunk[0]} has too many faces for a body.");
                var alongX = chunk.Max(c => c.X) - chunk.Min(c => c.X) >= chunk.Max(c => c.Z) - chunk.Min(c => c.Z);
                var mid = (int)Math.Floor(alongX ? (chunk.Min(c => c.X) + chunk.Max(c => c.X)) / 2.0 : (chunk.Min(c => c.Z) + chunk.Max(c => c.Z)) / 2.0);
                var first = chunk.Where(c => (alongX ? c.X : c.Z) <= mid).ToList();
                if (first.Count == 0 || first.Count == chunk.Count)
                {
                    // (one column too big for a body: by layers)
                    var midY = (chunk.Min(c => c.Y) + chunk.Max(c => c.Y)) / 2;
                    first = chunk.Where(c => c.Y <= midY).ToList();
                }
                var firstSet = first.ToHashSet();
                work.Enqueue((first, page));
                work.Enqueue((chunk.Where(c => !firstSet.Contains(c)).ToList(), page));
                continue;
            }
            if (IslandDecors.Locate(island, origin.X, origin.Z) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCubeOwnEngine) { left++; continue; }
            var d = IslandDecors.Blank(firstBody + bodies.Count | page << IslandFile.DecorPageShift, at.X, (int)Math.Round(origin.Y), at.Z);
            d.XMin = (int)Math.Floor(at.X + lo.X - origin.X); d.XMax = (int)Math.Ceiling(at.X + hi.X - origin.X);
            d.ZMin = (int)Math.Floor(at.Z + lo.Z - origin.Z); d.ZMax = (int)Math.Ceiling(at.Z + hi.Z - origin.Z);
            d.YMin = (int)Math.Floor(lo.Y); d.YMax = BrickBuilding.NoBoxTop;
            at.Cube.Decors.Add(d);
            bodies.Add(body.Write());
            decors++;
        }
        if (left > 0) log.Add($"{left} bodies left out: their cubes full");
        island.ObjectPages.Clear();
        island.ObjectPages.AddRange(pages.List.Skip(1));

        // what is solid: each cell's box (but a wisp's), the cells that fill theirs merged into as few boxes as will do; the steps of a
        // stair (LBA1's slopes, shapes 2-5) a box each step of four
        var full = new HashSet<(int X, int Y, int Z)>();
        var solidBoxes = new List<(double X0, double Y0, double Z0, double X1, double Y1, double Z1)>();
        var wisps = 0;
        foreach (var (c, cell) in cells)
        {
            var box = BoxOf(cell.Brick);
            // (a floor's tile draws only its top, a wall's brick one side: the most it draws of any face)
            var drawn = Math.Max(TileOf(cell.Brick, PolarTextures.Face.Top).Drawn, Math.Max(TileOf(cell.Brick, PolarTextures.Face.SideX).Drawn, TileOf(cell.Brick, PolarTextures.Face.SideZ).Drawn));
            if (drawn < Wisp) { wisps++; continue; }
            if (cell.Shape is >= 2 and <= 5)
            {
                // (a plank ramp's -- TempleModels' planks -- under its slope, none over it)
                var underSlope = models.TryGetValue(cell.Brick, out var ramp) && ramp.Model.Kind.How == TempleModels.Make.Ramp;
                foreach (var step in Steps(c, cell.Shape, frame, underSlope)) solidBoxes.Add(step);
                continue;
            }
            if (box.IsFull) { full.Add(c); continue; }
            solidBoxes.Add((frame.WorldX(c.X + box.U0), frame.WorldY(c.Y), frame.WorldZ(c.Z + box.V0), frame.WorldX(c.X + box.U1), frame.WorldY(c.Y + 1), frame.WorldZ(c.Z + box.V1)));
        }
        foreach (var (x0, y0, z0, x1, y1, z1) in BrickBuilding.Boxes(full))
            solidBoxes.Add((frame.WorldX(x0), frame.WorldY(y0), frame.WorldZ(z0), frame.WorldX(x1 + 1), frame.WorldY(y1 + 1), frame.WorldZ(z1 + 1)));
        var (placedBoxes, boxesLeft) = BrickBuilding.AddBoxes(island, emptyBody, solidBoxes, IslandDecors.MaxPerCubeOwnEngine, everyCube: true);
        log.Add($"the solid: {solidBoxes.Count} boxes ({wisps} wisps of cells left out), {placedBoxes} placed" + (boxesLeft > 0 ? $", {boxesLeft} copies left out (cubes full)" : ""));
        log.Add($"{decors} bodies drawn; the busiest cube {IslandOps.CubeCells(island).Max(c => c.Item3.Decors.Count)} decors");
        return new Built(bodies, decors, placedBoxes, pages.Count, log);
    }

    // A stair's cell (LBA1's slopes, shapes 2-5) as four steps, each a quarter of the cell along the way up and that much of its layer higher.
    // (LBA1's OBJECT.C ReajustPos: shape 2 rises toward +x, 3 toward +z, 4 toward -z, 5 toward -x; 6-13, two slopes meeting, are left whole)
    // `under`: a plank ramp's slope (TempleModels' ramp) -- each step a quarter further up, so that none stands over the slope (three steps).
    private static IEnumerable<(double, double, double, double, double, double)> Steps((int X, int Y, int Z) c, int shape, TempleFrame frame, bool under = false)
    {
        for (var k = 0; k < (under ? 3 : 4); k++)
        {
            var top = frame.WorldY(c.Y + (k + 1) / 4.0);
            var a = (under ? k + 1 : k) / 4.0;
            yield return shape switch
            {
                2 => (frame.WorldX(c.X + a), frame.WorldY(c.Y), frame.WorldZ(c.Z), frame.WorldX(c.X + 1), top, frame.WorldZ(c.Z + 1)),
                3 => (frame.WorldX(c.X), frame.WorldY(c.Y), frame.WorldZ(c.Z + a), frame.WorldX(c.X + 1), top, frame.WorldZ(c.Z + 1)),
                4 => (frame.WorldX(c.X), frame.WorldY(c.Y), frame.WorldZ(c.Z), frame.WorldX(c.X + 1), top, frame.WorldZ(c.Z + 1 - a)),
                _ => (frame.WorldX(c.X), frame.WorldY(c.Y), frame.WorldZ(c.Z), frame.WorldX(c.X + 1 - a), top, frame.WorldZ(c.Z + 1)),
            };
        }
    }

    // the faces a box shows: its top, its four sides, its bottom
    private static readonly (int Dx, int Dy, int Dz)[] Dirs = { (0, 1, 0), (1, 0, 0), (-1, 0, 0), (0, 0, 1), (0, 0, -1), (0, -1, 0) };
    private static readonly string[] DirNames = { "top", "+x", "-x", "+z", "-z", "bottom" };
    private static int Opposite(int dir) => dir switch { 0 => 5, 1 => 2, 2 => 1, 3 => 4, 4 => 3, _ => 0 };
    private static PolarTextures.Face FaceOf(int dir) => dir switch { 0 or 5 => PolarTextures.Face.Top, 1 or 2 => PolarTextures.Face.SideX, _ => PolarTextures.Face.SideZ };

    // A face to draw: its corners in order round it, either way (the layout's cells and layers), their places in its tile (0..1), the way it
    // faces (cells and layers), and its tile -- its key on the pages, its size, and whether it is solid.
    private readonly record struct Polygon(Vector3[] Corners, (double U, double V)[] Uv, Vector3 Facing, string Key, int W, int H, bool Solid);

    // A chunk's body: its faces as textured quads and triangles, its points relative to its origin (the middle of what it covers, island
    // world units). Null when it is too big for a body. `lo`, `hi`: its points' extent (world units).
    private static Body? Mesh(List<Polygon> polygons, TempleFrame frame, Func<string, (int X, int Y)> place, out Vector3 lo, out Vector3 hi, out Vector3 origin)
    {
        var faces = new List<(Vector3[] Quad, Polygon Polygon)>();
        lo = new Vector3(float.MaxValue); hi = new Vector3(float.MinValue);
        foreach (var polygon in polygons)
        {
            var quad = polygon.Corners.Select(k => new Vector3((float)frame.WorldX(k.X), (float)frame.WorldY(k.Y), (float)frame.WorldZ(k.Z))).ToArray();
            foreach (var p in quad) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
            faces.Add((quad, polygon));
        }
        origin = new Vector3(MathF.Round((lo.X + hi.X) / 2), MathF.Round((lo.Y + hi.Y) / 2), MathF.Round((lo.Z + hi.Z) / 2));
        if (hi.X - lo.X > 64000 || hi.Y - lo.Y > 64000 || hi.Z - lo.Z > 64000) return null;
        var o = origin;
        var points = new List<Vector3>();
        var index = new Dictionary<(int, int, int), int>();
        int P(Vector3 v)
        {
            var key = ((int)Math.Round(v.X), (int)Math.Round(v.Y), (int)Math.Round(v.Z));
            if (index.TryGetValue(key, out var i)) return i;
            points.Add(new Vector3(key.Item1 - o.X, key.Item2 - o.Y, key.Item3 - o.Z));
            return index[key] = points.Count - 1;
        }
        var bodyFaces = new List<Face>();
        var handles = new Dictionary<(int, int), int>();
        var textures = new List<uint>();
        foreach (var (corners, polygon) in faces)
        {
            var (tx, ty) = place(polygon.Key);
            var (w, h) = (polygon.W, polygon.H);
            var quad = corners;
            var ids = quad.Select(P).ToArray();
            var uvs = polygon.Uv;
            // (corners that came out at one point, a round thing's narrowest: a quad with one fewer, a triangle; less, nothing to draw)
            var distinct = Enumerable.Range(0, ids.Length).Where(i => !ids.Take(i).Contains(ids[i])).ToArray();
            if (distinct.Length < 3) continue;
            if (distinct.Length < ids.Length)
            {
                quad = distinct.Select(i => quad[i]).ToArray(); uvs = distinct.Select(i => uvs[i]).ToArray(); ids = distinct.Select(i => ids[i]).ToArray();
            }
            if (!handles.TryGetValue((tx, ty), out var handle)) { handle = handles[(tx, ty)] = textures.Count; textures.Add(TileEntry(tx, ty)); }
            var uv = uvs.SelectMany(k => new[] { (int)Math.Round((0.4 + k.U * (w - 0.8)) * 256), (int)Math.Round((0.4 + k.V * (h - 0.8)) * 256) }).ToArray();
            // (wound so the plain cross product points the way it faces: the engine's front faces -- the frame scales a layer half as much as
            // a cell, so a slope's facing is steeper in the world)
            var n = Vector3.Cross(quad[1] - quad[0], quad[2] - quad[0]);
            if (Vector3.Dot(n, polygon.Facing * new Vector3(1, 2, 1)) < 0)
            {
                ids = ids.Reverse().ToArray();
                uv = Enumerable.Range(0, ids.Length).Reverse().SelectMany(i => new[] { uv[i * 2], uv[i * 2 + 1] }).ToArray();
            }
            bodyFaces.Add(new Face(ids, 0, Texture: new FaceTexture(handle, uv), Lba2Type: polygon.Solid ? TextureSolid : TextureSeeThrough));
        }
        if (points.Count > MaxPoints || bodyFaces.Count > MaxFaces) return null;
        if (points.Any(p => Math.Abs(p.X) > 32000 || Math.Abs(p.Y) > 32000 || Math.Abs(p.Z) > 32000)) return null;
        // (the header's first word: the body format's version, 0x10 as every retail body has it, and the close flag -- cut at the near plane
        // close in front of the camera, which drives among these walls: BrickBuilding.CloseFlag)
        var header = new byte[96];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header, 0x10 | BrickBuilding.CloseFlag);
        return new Body
        {
            Game = 2, Static = true, Lit = false, Header = header,
            Vertices = points,
            Bones = new List<Bone> { new(0, points.Count, 0, -1, new byte[8]) },
            Faces = bodyFaces,
            Textures = textures.ToArray(),
        };
    }
}
