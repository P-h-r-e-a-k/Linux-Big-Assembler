using System.Buffers.Binary;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain.Palace;

// The dungeon's stone (the user, 2026-10-17: "Let's make our palace basement more like a medieval dungeon"): its walls, floor and ceiling
// textured instead of flat -- a page of the island's object textures painted here (Paint), in the island's palette:
//   - the walls: courses of rough grey ashlar, blocks of uneven length and tone in dark mortar, each block's top edge catching the light
//     and its foot in shadow, chipped, damp and green at the foot, sooty towards the ceiling -- two of them, as light as the rooms' walls
//     are shaded (east and west, north and south), 128 x 128 pixels (4,096 world units: two cells along, the wall's height and more up);
//   - the floor: big flagstones of uneven size in four rows, worn, cracked here and there, in dark joints -- two of them, a cell each, in
//     turns and turned about (Variant) so that the floor doesn't show a pattern;
//   - the ceiling (the ground floor's floor's underside): dark timber planks, sooty.
// Each face of the dungeon's shell (Faces: its walls a column of a cell's layers, its floor and ceiling a cell) one perspective-correct
// textured polygon (TEXTUREZ), the stone's run on from cell to cell (its place on the page from where the face is in the world), the
// bodies of them (Add) drawn only, drawn close to the camera (BrickBuilding.CloseFlag), their page in their decors' body numbers.
internal static class PalaceDungeon
{
    // (the page: the walls' two at (0, 0) and (128, 0), 128 x 128; the floor's two at (0, 128) and (64, 128), the ceiling's at (128, 128),
    // 64 x 64 each -- with their repeat masks)
    public enum Tile { WallLight, WallDark, FloorA, FloorB, Ceiling }
    private static readonly (int X, int Y, int Wide, int High)[] Tiles = { (0, 0, 128, 128), (128, 0, 128, 128), (0, 128, 64, 64), (64, 128, 64, 64), (128, 128, 64, 64) };
    private static uint Entry(Tile t) { var (x, y, w, h) = Tiles[(int)t]; return (uint)(((h - 1) << 8 | (w - 1)) << 16) | (uint)(y * 256 + x); }
    private const double Texel = 32;                     // world units a pixel

    // the stones' colours: the walls' tones (grey, a little warm), their mortar, the damp's green; the flagstones', their joints; the planks'
    private static readonly (float R, float G, float B)[] WallStones = { (96, 92, 86), (86, 83, 79), (104, 98, 90), (80, 78, 76), (92, 88, 84), (100, 96, 92) };
    private static readonly (float R, float G, float B) Mortar = (38, 36, 34), Damp = (52, 60, 44);
    private static readonly (float R, float G, float B)[] FlagStones = { (98, 94, 88), (88, 85, 80), (106, 100, 92), (92, 90, 86), (82, 80, 76) };
    private static readonly (float R, float G, float B) Joint = (44, 42, 40), Plank = (70, 48, 30), PlankDark = (48, 32, 20);

    // A pixel's own little noise (a hash: the same every build).
    private static float Noise(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)(x * 73856093 ^ y * 19349663 ^ seed * 83492791);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xFFFF) / 65535f;
        }
    }

    // The page (256 x 256 indices of the island's palette): `nearest` a colour's nearest.
    public static byte[] Paint(Func<(float R, float G, float B), int> nearest)
    {
        var page = new byte[256 * 256];
        void Put(int x, int y, (float R, float G, float B) c, float k) =>
            page[y * 256 + x] = (byte)nearest((MathF.Round(Math.Clamp(c.R * k, 0, 255)), MathF.Round(Math.Clamp(c.G * k, 0, 255)), MathF.Round(Math.Clamp(c.B * k, 0, 255))));
        // ---- the walls: courses from the floor up (v 100 the floor, 0 the ceiling: Faces), the blocks' joints staggered, wrapping round
        int[] courses = { 18, 16, 15, 17, 14, 16, 16, 16 };
        foreach (var (tile, shade) in new[] { (Tile.WallLight, 0.84f), (Tile.WallDark, 0.70f) })
        {
            var (ox, oy, w, h) = Tiles[(int)tile];
            var bottom = 100;
            for (var c = 0; c < courses.Length; c++)
            {
                int top = bottom - courses[c];
                // (its blocks: lengths of 22 to 40 pixels round the 128, from an offset of its own)
                var joints = new List<int>();
                var at = (int)(Noise(c, 1, 7) * 40);
                while (joints.Count == 0 || at < joints[0] + w) { joints.Add(at); at += 22 + (int)(Noise(c, joints.Count, 11) * 19); }
                for (var y = Math.Max(0, top); y < bottom && y < h + top; y++)
                {
                    var py = ((y % h) + h) % h;
                    for (var x = 0; x < w; x++)
                    {
                        // (which block: the last joint at or before x, round the wrap)
                        var xx = x < joints[0] ? x + w : x;
                        var block = joints.FindLastIndex(j => j <= xx);
                        int left = xx - joints[block], right = (block + 1 < joints.Count ? joints[block + 1] : joints[0] + w) - xx;
                        int fromTop = y - top, fromBottom = bottom - 1 - y;
                        (float R, float G, float B) col;
                        float k;
                        if (fromTop < 1 || left < 2 || fromBottom < 1) { col = Mortar; k = 1; }
                        else
                        {
                            col = WallStones[(int)(Noise(c, block, 3) * WallStones.Length) % WallStones.Length];
                            // (rough: speckles, a chip now and then; its top edge lit, its foot and right side in shadow)
                            k = 0.92f + 0.16f * Noise(x, y, 5);
                            if (Noise(x / 3, y / 3, 9) > 0.93f) k *= 0.8f;
                            if (fromTop < 2) k *= 1.12f;
                            if (fromBottom < 2 || right < 3) k *= 0.82f;
                        }
                        // (damp at the foot, soot up under the ceiling)
                        if (y > 88 && Noise(x / 4, y / 2, 13) > 0.45f + (100 - y) * 0.04f) col = ((col.R + Damp.R) / 2, (col.G + Damp.G) / 2, (col.B + Damp.B) / 2);
                        var soot = y < 30 ? 0.62f + 0.38f * y / 30f : 1;
                        Put(ox + x, oy + py, col, k * soot * shade);
                    }
                }
                bottom = top;
                if (bottom <= 0) break;
            }
            // (the rows past the floor -- never shown -- as the foot's)
            for (var y = 100; y < h; y++) for (var x = 0; x < w; x++) page[(oy + y) * 256 + ox + x] = page[(oy + 99) * 256 + ox + x];
        }
        // ---- the floor: four rows of flagstones of uneven length, wrapping
        foreach (var (tile, seed) in new[] { (Tile.FloorA, 21), (Tile.FloorB, 37) })
        {
            var (ox, oy, w, h) = Tiles[(int)tile];
            int[] rows = { 0, 17, 31, 48, 64 };
            for (var r = 0; r + 1 < rows.Length; r++)
            {
                var joints = new List<int>();
                var at = (int)(Noise(r, seed, 1) * 24);
                while (joints.Count == 0 || at < joints[0] + w) { joints.Add(at); at += 18 + (int)(Noise(r, joints.Count + seed, 2) * 16); }
                for (var y = rows[r]; y < rows[r + 1]; y++)
                    for (var x = 0; x < w; x++)
                    {
                        var xx = x < joints[0] ? x + w : x;
                        var slab = joints.FindLastIndex(j => j <= xx);
                        int left = xx - joints[slab], fromTop = y - rows[r];
                        (float R, float G, float B) col;
                        var k = 1f;
                        if (fromTop < 2 || left < 2) col = Joint;
                        else
                        {
                            col = FlagStones[(int)(Noise(r, slab + seed, 4) * FlagStones.Length) % FlagStones.Length];
                            k = 0.9f + 0.14f * Noise(x, y, seed);
                            // (worn smooth in the middle, darker at its edges; a crack across some)
                            int toEdge = Math.Min(Math.Min(left, fromTop), Math.Min(rows[r + 1] - 1 - y, (slab + 1 < joints.Count ? joints[slab + 1] : joints[0] + w) - 1 - xx));
                            if (toEdge < 4) k *= 0.88f + 0.03f * toEdge;
                            var crack = Noise(r, slab, seed + 5) > 0.6f && Math.Abs(fromTop - (int)(left * 0.6f) - 3) < 1 && left < 14;
                            if (crack) k *= 0.6f;
                        }
                        Put(ox + x, oy + y, col, k);
                    }
            }
        }
        // ---- the ceiling: planks along u, eight pixels wide, a dark gap between, grain along them, sooty
        {
            var (ox, oy, w, h) = Tiles[(int)Tile.Ceiling];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var gap = y % 8 == 0;
                    var k = gap ? 1 : 0.85f + 0.2f * Noise(x / 6, y, 17) * Noise(x, y / 2, 19) + 0.05f * (y % 8 == 1 ? 1 : 0);
                    Put(ox + x, oy + y, gap ? PlankDark : Plank, k * 0.55f);
                }
        }
        return page;
    }

    // A face of the dungeon's shell, textured: its four corners (the world's), the way it looks (into the dungeon), its tile and each
    // corner's place on it (pixels).
    public sealed record Stone(Vector3[] Quad, Vector3 Normal, Tile Tile, (double U, double V)[] Uv);

    // The faces of the shell round the dungeon's air (`air`), as Stones: the walls' columns of faces (a cell along, every layer of it) and
    // each cell of the floor and the ceiling. `corner`: a cell corner's place in the world; `ceilingY`: the ceiling's height (the walls'
    // stone runs down from it). Their cells' faces, `faces`: (cell, the way it looks -- BrickBuilding.Directions) -- to leave out of the
    // rooms' own.
    public static List<Stone> Faces(IReadOnlyDictionary<(int X, int Y, int Z), (int Brick, int Tile)> cells, HashSet<(int X, int Y, int Z)> air,
        Func<int, int, int, Vector3> corner, double ceilingY, HashSet<((int X, int Y, int Z) Cell, int Dir)> faces)
    {
        var stones = new List<Stone>();
        var walls = new Dictionary<(int Dir, int X, int Z), List<int>>();
        foreach (var a in air)
            for (var d = 0; d < BrickBuilding.Directions.Length; d++)
            {
                var (dx, dy, dz, _) = BrickBuilding.Directions[d];
                var n = (a.X + dx, a.Y + dy, a.Z + dz);
                if (!cells.ContainsKey(n)) continue;
                var back = d ^ 1;
                if (!faces.Add((n, back))) continue;
                if (dy == 0) { var key = (back, n.Item1, n.Item3); if (!walls.TryGetValue(key, out var ys)) walls[key] = ys = new(); ys.Add(n.Item2); continue; }
                // (the floor's -- dy < 0, the cell under the air -- and the ceiling's)
                var (x, y, z) = n;
                var top = dy < 0 ? y + 1 : y;
                var quad = new[] { corner(x, top, z), corner(x + 1, top, z), corner(x + 1, top, z + 1), corner(x, top, z + 1) };
                var tile = dy < 0 ? (Variant(x, z) % 2 == 0 ? Tile.FloorA : Tile.FloorB) : Tile.Ceiling;
                // (a cell's own tile, turned and flipped by turns: no pattern across the floor)
                var turn = dy < 0 ? Variant(x, z) / 2 : 0;
                var uv = quad.Select(q =>
                {
                    double u = (q.X - quad[0].X) / Texel, v = (q.Z - quad[0].Z) / Texel, s = Math.Round((quad[2].X - quad[0].X) / Texel);
                    return turn switch { 1 => (v, s - u), 2 => (s - u, s - v), 3 => (s - v, u), _ => (u, v) };
                }).ToArray();
                stones.Add(new Stone(quad, new Vector3(0, -dy, 0), tile, uv));
            }
        foreach (var ((dir, x, z), ys) in walls)
        {
            ys.Sort();
            var (dx, _, dz, _) = BrickBuilding.Directions[dir];
            for (var i = 0; i < ys.Count;)
            {
                var j = i;
                while (j + 1 < ys.Count && ys[j + 1] == ys[j] + 1) j++;
                int y0 = ys[i], y1 = ys[j] + 1;
                i = j + 1;
                // (the plane at the cells' face on that side; along it, a cell)
                var at = dx != 0 ? x + (dx > 0 ? 1 : 0) : z + (dz > 0 ? 1 : 0);
                Vector3 P(int along, int y) => dx != 0 ? corner(at, y, along) : corner(along, y, at);
                var a0 = dx != 0 ? z : x;
                var quad = new[] { P(a0, y0), P(a0 + 1, y0), P(a0 + 1, y1), P(a0, y1) };
                // (the stone run on along the wall -- the tile's two cells -- and down from the ceiling)
                double Along(Vector3 q) => dx != 0 ? q.Z : q.X;
                var baseAlong = Math.Floor(Along(quad[0]) / (Texel * 128)) * Texel * 128;
                var uv = quad.Select(q => ((Along(q) - baseAlong) / Texel, (ceilingY - q.Y) / Texel)).ToArray();
                stones.Add(new Stone(quad, new Vector3(dx, 0, dz), dx != 0 ? Tile.WallLight : Tile.WallDark, uv));
            }
        }
        return stones;
    }

    // a cell's turn of the floor's tiles: 0-7 (its tile by the low bit, its turn by the rest)
    private static int Variant(int x, int z) => (int)(Noise(x, z, 29) * 8) & 7;

    // The stones as bodies (appended to `newBodies`, numbered from `firstBody` on): those of a cube together, up to a body's most, each a
    // decor drawn only, its object page `page` (IslandFile.DecorPageShift), in every cube it reaches and within `reach` of it. Returns the
    // bodies and the decors left out (a cube full).
    public static (int Bodies, int Left) Add(IslandFile island, List<Stone> stones, List<byte[]> newBodies, int firstBody, int page, int maxPerCube, double reach)
    {
        int bodies = 0, left = 0;
        var entries = Enum.GetValues<Tile>().Select(Entry).ToArray();
        foreach (var cube in stones.GroupBy(s => ((int)Math.Floor(s.Quad.Average(q => q.X) / IslandFile.CubeSize), (int)Math.Floor(s.Quad.Average(q => q.Z) / IslandFile.CubeSize)))
                     .OrderBy(g => g.Key.Item2).ThenBy(g => g.Key.Item1))
            foreach (var chunk in cube.Chunk(BrickBuilding.MaxFaces / 4))
            {
                var lo = chunk.SelectMany(s => s.Quad).Aggregate(Vector3.Min);
                var hi = chunk.SelectMany(s => s.Quad).Aggregate(Vector3.Max);
                var origin = (lo + hi) / 2;
                var points = new List<Vector3>();
                var faces = new List<Face>();
                foreach (var s in chunk)
                {
                    var ids = new[] { 0, 1, 2, 3 };
                    // (wound so that the plain cross product points the way it looks: BrickBuilding.AddBodies)
                    var n = Vector3.Cross(s.Quad[1] - s.Quad[0], s.Quad[2] - s.Quad[0]);
                    if (Vector3.Dot(n, s.Normal) < 0) Array.Reverse(ids);
                    var first = points.Count;
                    points.AddRange(ids.Select(i => s.Quad[i] - origin));
                    var uv = ids.SelectMany(i => new[] { (int)Math.Clamp(Math.Round(s.Uv[i].U * 256), 0, 65535), (int)Math.Clamp(Math.Round(s.Uv[i].V * 256), 0, 65535) }).ToArray();
                    faces.Add(new Face(new[] { first, first + 1, first + 2, first + 3 }, 0, Texture: new FaceTexture((int)s.Tile, uv), Lba2Type: 16));
                }
                var header = new byte[96];
                BinaryPrimitives.WriteInt32LittleEndian(header, 0x10 | BrickBuilding.CloseFlag);
                var body = new LbaBodyStudio.Body
                {
                    Game = 2, Static = true, Lit = false, Header = header, Vertices = points,
                    Bones = new List<Bone> { new(0, points.Count, 0, -1, new byte[8]) },
                    Faces = faces, Textures = entries,
                };
                var number = firstBody + newBodies.Count;
                newBodies.Add(body.Write());
                bodies++;
                left += BrickBuilding.AddToCubes(island, number | page << IslandFile.DecorPageShift, 0, origin.X, (int)Math.Round(origin.Y), origin.Z,
                    lo.X, hi.X, (int)Math.Floor(lo.Y), BrickBuilding.NoBoxTop, lo.Z, hi.Z, maxPerCube, reach).Full;
            }
        return (bodies, left);
    }
}
