using System.Numerics;
using LBAAssembler.Terrain.Polar;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// An island object made of LBA1 bricks cell by cell, as LBA1's own builder made its huts, posts and trees (the terrain editor's Object maker;
// 2026-10-23, recreating LBA1's Hamalayi by hand: its hut, the ski lift's pylons, its trees and rock walls are bricks). A cell is a 512-unit
// square of the island a layer (256) high; each brick fills the part of its cell its picture shows (a post a thin box: PolarTextures
// .Sprite.Footprint), and each face of it nothing of the object covers is textured with the brick's own face (its top, or the side LBA1
// draws -- the two sides LBA1 never drew, -x and -z, with the opposite side's), the same unprojection Polar Island's objects use. The faces
// become one LBA2 body; its tiles go on an object page of the island's own (a page the maker added, or a new one), each tile in a free slot.
internal static class BrickObject
{
    public const int CellUnits = 512, LayerUnits = 256;
    // the engine's limits for a body (550 points, 550 polygons), with some room
    public const int MaxPoints = 500, MaxFaces = 500;

    // A made object: the body (OBL entry), the object page its tiles are on, and its box round its origin (the middle of its footprint, at
    // its foot) -- the decor's collision box.
    public sealed record Made(byte[] Body, int Page, int XMin, int XMax, int YMax, int ZMin, int ZMax, int Faces, int Points, int NewTiles);

    // the faces a cell shows: its top and its four sides (nothing sees under an object)
    private static readonly (int Dx, int Dy, int Dz)[] Dirs = { (0, 1, 0), (1, 0, 0), (-1, 0, 0), (0, 0, 1), (0, 0, -1) };
    private static PolarTextures.Face FaceOf(int dir) => dir switch { 0 => PolarTextures.Face.Top, 1 or 2 => PolarTextures.Face.SideX, _ => PolarTextures.Face.SideZ };
    // a tile's entry in the body's texture table: its place in the page and a repeat mask of 32 pixels each way (PolarObjects.TileEntry)
    private static uint TileEntry(int x, int y) => 0x1F1F0000u | (uint)(y * 256 + x);

    // The object's body, its tiles put on a page of the island (`island.ObjectPages`: a page added when none has room). Throws
    // InvalidOperationException with a message for the user when it can't be made.
    public static Made Make(IReadOnlyDictionary<(int X, int Layer, int Z), int> cells, Func<int, byte[]?> readBrick, byte[] lba1Palette, byte[] islandPalette, IslandFile island)
    {
        if (cells.Count == 0) throw new InvalidOperationException("The object has no bricks yet.");
        var sprites = new Dictionary<int, PolarTextures.Sprite>();
        PolarTextures.Sprite Sprite(int brick) => sprites.TryGetValue(brick, out var s) ? s : sprites[brick] = PolarTextures.Sprite.Decode(readBrick(brick));
        foreach (var brick in cells.Values.Distinct())
            if (Sprite(brick).Pixels.Length == 0) throw new InvalidOperationException($"LBA1 has no brick {brick}.");
        var boxes = new Dictionary<int, PolarTextures.Box>();
        PolarTextures.Box BoxOf(int brick) => boxes.TryGetValue(brick, out var b) ? b : boxes[brick] = Sprite(brick).Footprint();

        // the faces that show: a face is hidden where it lies on its cell's side and the next cell's brick covers it
        bool Hidden((int X, int Layer, int Z) c, int dir)
        {
            var (dx, dy, dz) = Dirs[dir];
            if (!cells.TryGetValue((c.X + dx, c.Layer + dy, c.Z + dz), out var other)) return false;
            var box = BoxOf(cells[c]);
            var onSide = dir switch { 1 => box.U1 >= 1, 2 => box.U0 <= 0, 3 => box.V1 >= 1, 4 => box.V0 <= 0, _ => true };
            if (!onSide) return false;
            var nb = BoxOf(other);
            return dir switch
            {
                0 => nb.U0 <= box.U0 && nb.U1 >= box.U1 && nb.V0 <= box.V0 && nb.V1 >= box.V1,
                1 => nb.U0 <= 0 && nb.V0 <= box.V0 && nb.V1 >= box.V1,
                2 => nb.U1 >= 1 && nb.V0 <= box.V0 && nb.V1 >= box.V1,
                3 => nb.V0 <= 0 && nb.U0 <= box.U0 && nb.U1 >= box.U1,
                _ => nb.V1 >= 1 && nb.U0 <= box.U0 && nb.U1 >= box.U1,
            };
        }
        var shown = new List<((int X, int Layer, int Z) Cell, int Dir)>();
        var tiles = new Dictionary<(int Brick, PolarTextures.Face Face), (int[] Tile, int W, int H)>();
        foreach (var c in cells.Keys.OrderBy(k => k.Layer).ThenBy(k => k.Z).ThenBy(k => k.X))
            for (var d = 0; d < Dirs.Length; d++)
            {
                if (Hidden(c, d)) continue;
                shown.Add((c, d));
                var key = (cells[c], FaceOf(d));
                if (!tiles.ContainsKey(key)) { var (w, h) = PolarTextures.Size(key.Item2); tiles[key] = (Sprite(cells[c]).Tile(key.Item2, BoxOf(cells[c])), w, h); }
            }
        if (shown.Count > MaxFaces) throw new InvalidOperationException($"The object shows {shown.Count} faces; a body holds {MaxFaces} at the most. Make it as two objects.");

        // the page: one of the island's added object pages with a free half-slot (32 x 16, all colour 0: nothing put there) for each tile,
        // else a new page
        var colours = new PolarTextures.Colours(lba1Palette, islandPalette);
        var need = tiles.Values.Sum(t => PolarTextures.Pages.HalvesOf(t.H));
        var pageIndex = -1;
        for (var p = 0; p < island.ObjectPages.Count && pageIndex < 0; p++)
            if (FreeHalves(island.ObjectPages[p]).Count >= need) pageIndex = p;
        if (pageIndex < 0)
        {
            if (island.ObjectPages.Count >= IslandFile.MaxObjectPages - 1) throw new InvalidOperationException($"The island's {IslandFile.MaxObjectPages - 1} object pages are full.");
            island.ObjectPages.Add(new byte[256 * 256]);
            pageIndex = island.ObjectPages.Count - 1;
        }
        var page = island.ObjectPages[pageIndex];
        var free = FreeHalves(page);
        var place = new Dictionary<(int, PolarTextures.Face), (int X, int Y)>();
        // (a whole slot for a top -- two halves one over the other -- a half for a side)
        foreach (var (key, (tile, w, h)) in tiles.OrderByDescending(t => t.Value.H))
        {
            int at;
            if (PolarTextures.Pages.HalvesOf(h) == 2)
            {
                at = free.FirstOrDefault(i => i % 2 == 0 && free.Contains(i + 1), -1);
                if (at < 0) throw new InvalidOperationException("No whole free slot on the object page for a brick's top.");
                free.Remove(at); free.Remove(at + 1);
            }
            else { at = free.Min(); free.Remove(at); }
            int slot = at / 2, x = slot % 8 * 32, y = slot / 8 * 32 + at % 2 * 16;
            for (var j = 0; j < h; j++)
                for (var i = 0; i < w; i++)
                    page[(y + j) * 256 + x + i] = (byte)colours.Nearest[Math.Max(0, tile[j * w + i])];
            place[key] = (x, y);
        }

        // the body: the faces as textured quads round the origin, the middle of the footprint at the foot of the lowest layer
        int x0 = cells.Keys.Min(c => c.X), x1 = cells.Keys.Max(c => c.X) + 1, z0 = cells.Keys.Min(c => c.Z), z1 = cells.Keys.Max(c => c.Z) + 1;
        int l0 = cells.Keys.Min(c => c.Layer), l1 = cells.Keys.Max(c => c.Layer) + 1;
        double ox = (x0 + x1) / 2.0, oz = (z0 + z1) / 2.0;
        var points = new List<Vector3>();
        var index = new Dictionary<(int, int, int), int>();
        int P(double x, int layer, double z)
        {
            // (the same point to 1/64 of a cell)
            var k = ((int)Math.Round(x * 64), layer, (int)Math.Round(z * 64));
            if (index.TryGetValue(k, out var i)) return i;
            points.Add(new Vector3((float)((x - ox) * CellUnits), (layer - l0) * LayerUnits, (float)((z - oz) * CellUnits)));
            return index[k] = points.Count - 1;
        }
        var faces = new List<Face>();
        var handles = new Dictionary<(int, int), int>();
        var textures = new List<uint>();
        foreach (var (c, dir) in shown)
        {
            var b = BoxOf(cells[c]);
            double x = c.X + b.U0, xe = c.X + b.U1, z = c.Z + b.V0, ze = c.Z + b.V1;
            int y = c.Layer + 1;
            var face = FaceOf(dir);
            var (tx, ty) = place[(cells[c], face)];
            var (w, h) = PolarTextures.Size(face);
            // corners (their u, v in the tile) as PolarObjects.Mesh lays them
            (double X, int Y, double Z, double U, double V)[] corners = dir switch
            {
                0 => new[] { (x, y, ze, 0.0, 1.0), (xe, y, ze, 1.0, 1.0), (xe, y, z, 1.0, 0.0), (x, y, z, 0.0, 0.0) },
                1 => new[] { (xe, y, z, 0.0, 0.0), (xe, y, ze, 1.0, 0.0), (xe, y - 1, ze, 1.0, 1.0), (xe, y - 1, z, 0.0, 1.0) },
                2 => new[] { (x, y - 1, z, 0.0, 1.0), (x, y - 1, ze, 1.0, 1.0), (x, y, ze, 1.0, 0.0), (x, y, z, 0.0, 0.0) },
                3 => new[] { (x, y - 1, ze, 0.0, 1.0), (xe, y - 1, ze, 1.0, 1.0), (xe, y, ze, 1.0, 0.0), (x, y, ze, 0.0, 0.0) },
                _ => new[] { (x, y, z, 0.0, 0.0), (xe, y, z, 1.0, 0.0), (xe, y - 1, z, 1.0, 1.0), (x, y - 1, z, 0.0, 1.0) },
            };
            var ids = corners.Select(k => P(k.X, k.Y, k.Z)).ToArray();
            if (!handles.TryGetValue((tx, ty), out var handle)) { handle = handles[(tx, ty)] = textures.Count; textures.Add(TileEntry(tx, ty)); }
            var uv = corners.SelectMany(k => new[] { (int)Math.Round((0.4 + k.U * (w - 0.8)) * 256), (int)Math.Round((0.4 + k.V * (h - 0.8)) * 256) }).ToArray();
            // (wound so (p1 - p0) x (p2 - p0) points out, as the engine takes a face's front)
            var p = ids.Select(i => points[i]).ToArray();
            var (dx, dy, dz) = Dirs[dir];
            if (Vector3.Dot(Vector3.Cross(p[1] - p[0], p[2] - p[0]), new Vector3(dx, dy, dz)) < 0)
            {
                ids = ids.Reverse().ToArray();
                uv = Enumerable.Range(0, 4).Reverse().SelectMany(i => new[] { uv[i * 2], uv[i * 2 + 1] }).ToArray();
            }
            faces.Add(new Face(ids, 0, Texture: new FaceTexture(handle, uv), Lba2Type: 8));
        }
        if (points.Count > MaxPoints) throw new InvalidOperationException($"The object has {points.Count} corners; a body holds {MaxPoints} at the most. Make it as two objects.");
        // (the header's first byte: the body format's version, 0x10 as every retail body has it -- the engine reads textured polygons by it)
        var header = new byte[96];
        header[0] = 0x10;
        var body = new Body
        {
            Game = 2, Static = true, Lit = false, Header = header,
            Vertices = points,
            Bones = new List<Bone> { new(0, points.Count, 0, -1, new byte[8]) },
            Faces = faces,
            Textures = textures.ToArray(),
        };
        return new Made(body.Write(), pageIndex + 1, (int)Math.Round((x0 - ox) * CellUnits), (int)Math.Round((x1 - ox) * CellUnits), (l1 - l0) * LayerUnits,
            (int)Math.Round((z0 - oz) * CellUnits), (int)Math.Round((z1 - oz) * CellUnits), faces.Count, points.Count, tiles.Count);
    }

    // A page's free half-slots (32 x 16, numbered two to a 32 x 32 slot, row by row): those nothing was put in (all colour 0 -- a tile's
    // colours are the palette's lit ones, never 0).
    public static SortedSet<int> FreeHalves(byte[] page)
    {
        var free = new SortedSet<int>();
        for (var half = 0; half < 128; half++)
        {
            int slot = half / 2, x = slot % 8 * 32, y = slot / 8 * 32 + half % 2 * 16;
            var empty = true;
            for (var j = 0; j < 16 && empty; j++)
                for (var i = 0; i < 32; i++)
                    if (page[(y + j) * 256 + x + i] != 0) { empty = false; break; }
            if (empty) free.Add(half);
        }
        return free;
    }

    // ---- the design as a file (the Object maker's Save / Open): one line a cell, "x layer z brick"

    public static string ToText(IReadOnlyDictionary<(int X, int Layer, int Z), int> cells) =>
        "# LBA Assembler object: one brick a line -- cell x, layer, cell z, LBA1 brick\n" +
        string.Join("\n", cells.OrderBy(c => c.Key.Layer).ThenBy(c => c.Key.Z).ThenBy(c => c.Key.X).Select(c => $"{c.Key.X} {c.Key.Layer} {c.Key.Z} {c.Value}")) + "\n";

    public static Dictionary<(int X, int Layer, int Z), int> FromText(string text)
    {
        var cells = new Dictionary<(int X, int Layer, int Z), int>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var f = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 4 || !int.TryParse(f[0], out var x) || !int.TryParse(f[1], out var l) || !int.TryParse(f[2], out var z) || !int.TryParse(f[3], out var b))
                throw new FormatException($"Not a cell: \"{line}\"");
            cells[(x, l, z)] = b;
        }
        return cells;
    }
}
