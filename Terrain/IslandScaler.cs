using System.Buffers.Binary;
using System.IO;
using LBAAssembler.Scenes;

namespace LBAAssembler.Terrain;

// An island made bigger, everything on it with it (RaceTrackIsland.Scale): the Island of the Francos twice its size each way, four times
// its area, for its race track (the user, 2026-10-07: "make the whole island bigger, maybe 4 times its original size, let's scale
// everything up"). About the corner of its first cube (Anchor), every place goes k times as far from it and k times as high: each cube of
// the island becomes k x k cubes, each of its outside scenes k x k scenes (the scene keeps its number for the cube its Twinsen starts in,
// the others are numbered from RaceTrackIsland.MoreScenes on -- past 254: the engine's holomap arrows stop there, CubeArrowFlags), its
// objects' bodies k times their size.
//
//  - the ground: each cell becomes k x k cells cut the same way, each of their triangles inside one of the old cell's, its texture the
//    same part of the old triangle's (its look the same, k times bigger); heights k times, the light and the game codes as they were;
//  - the objects: k times as far from the anchor, k times as high, their boxes k times; every body of the island's OBL k times its size;
//  - the scenes: the zones, track points and actors k times as far, each in the new scene of the cube it is in (a zone in every one it
//    reaches); an actor that isn't in a new scene's cube keeps its place in the scene's list -- the scripts' references to actors are by
//    place -- as an inert stand-in (out of sight, no scripts); the cube-change zones between the island's cubes made afresh; and every
//    other scene's way onto the island (a door's) to the place it is now.
internal static class IslandScaler
{
    // the island's cubes as (cube x, cube z) -> the k x k cubes each becomes: (anchor + k (c - anchor) + (i, j))
    public static (int X, int Z) NewCube(int cx, int cz, int i, int j, int k, (int X, int Z) anchor) => (anchor.X + k * (cx - anchor.X) + i, anchor.Z + k * (cz - anchor.Z) + j);

    // The anchor: the corner of the island's first cube (lowest x, then z).
    public static (int X, int Z) Anchor(IslandFile island)
    {
        var cubes = Present(island);
        return (cubes.Min(c => c.X), cubes.Min(c => c.Z));
    }

    private static List<(int X, int Z)> Present(IslandFile island)
    {
        var list = new List<(int, int)>();
        for (var cz = 0; cz < IslandFile.MapSize; cz++)
            for (var cx = 0; cx < IslandFile.MapSize; cx++)
                if (island.CubeAt(cx, cz) is not null) list.Add((cx, cz));
        return list;
    }

    // ---- the ground and the objects -----------------------------------------------------------------------------------------------

    public static IslandFile Scale(IslandFile source, int k, List<string> log)
    {
        if (k != 2) throw new ArgumentException("Islands are made bigger 2 times only.");
        if (source.GroundPages.Count > 0) throw new InvalidDataException("An island with more ground texture pages can't be made bigger.");
        var anchor = Anchor(source);
        var old = Present(source);
        var cubes = old.SelectMany(c => Enumerable.Range(0, k * k).Select(q => (Old: c, I: q % k, J: q / k, New: NewCube(c.X, c.Z, q % k, q / k, k, anchor)))).ToList();
        if (cubes.Any(c => c.New.X >= IslandFile.MapSize || c.New.Z >= IslandFile.MapSize))
            throw new InvalidDataException($"The island {k} times bigger is past the island map's {IslandFile.MapSize} x {IslandFile.MapSize} cubes.");
        if (cubes.Count > 127) throw new InvalidDataException("Too many cubes.");
        var island = IslandFile.Parse(NewFile(source, cubes.Select(c => c.New).ToList()), source.Path);
        var defs = 0;
        foreach (var (o, i, j, n) in cubes)
        {
            var from = source.CubeAt(o.X, o.Z)!;
            var to = island.CubeAt(n.X, n.Z)!;
            // its settings the old cube's: the sea drawn in all its patches (the old ones' are k times the size), no animated ground
            to.Info = (int[])from.Info.Clone();
            to.BitField = 0xFFFF;
            for (var w = 6; w < IslandCube.InfoSize; w++) to.Info[w] = -1;
            // heights and light: a new vertex on an old one takes it; between two, their middle; in a cell's middle, its diagonal's
            for (var vz = 0; vz < IslandCube.Vertices; vz++)
                for (var vx = 0; vx < IslandCube.Vertices; vx++)
                {
                    var ox2 = i * 64 + vx; var oz2 = j * 64 + vz;   // twice the old vertex coordinates
                    var (h, l, water) = Sample(from, ox2, oz2);
                    to.Heights[vz * 65 + vx] = (short)Math.Clamp(Math.Round(h * k), short.MinValue, short.MaxValue);
                    to.Intensity[vz * 65 + vx] = (byte)((water << 4) | Math.Clamp((int)Math.Round(l), 0, 15));
                }
            // the cells: each a quarter of an old one, cut the same way, each triangle inside one of the old cell's
            var table = new Dictionary<string, int>();
            var list = new List<ushort>();
            for (var z = 0; z < 64; z++)
                for (var x = 0; x < 64; x++)
                {
                    int ox = (i * 64 + x) / 2, oz = (j * 64 + z) / 2;   // the old cell
                    int a = (i * 64 + x) % 2, b = (j * 64 + z) % 2;     // which quarter of it
                    var p0 = new IslandPolygon(from.Polygon(ox, oz, 0));
                    var diagonal = p0.Diagonal;
                    for (var half = 0; half < 2; half++)
                    {
                        // the new triangle's corners, in the old cell's units (0..1)
                        var corners = HalfCorners[(diagonal ? 2 : 0) + half].Select(c => ((a + CornerOffsets[c].X) / 2.0, (b + CornerOffsets[c].Z) / 2.0)).ToArray();
                        double cx = corners.Average(c => c.Item1), cz = corners.Average(c => c.Item2);
                        var oldHalf = diagonal ? (cx + cz < 1 ? 0 : 1) : (cz > cx ? 0 : 1);
                        var polygon = new IslandPolygon(from.Polygon(ox, oz, oldHalf));
                        var index = polygon.TextureIndex;
                        var uv = new ushort[6];
                        if (index * 6 + 6 <= from.TextureDefs.Length)
                        {
                            var oc = HalfCorners[(diagonal ? 2 : 0) + oldHalf];
                            var (u0, v0) = ((double)from.TextureDefs[index * 6], (double)from.TextureDefs[index * 6 + 1]);
                            var (u1, v1) = ((double)from.TextureDefs[index * 6 + 2], (double)from.TextureDefs[index * 6 + 3]);
                            var (u2, v2) = ((double)from.TextureDefs[index * 6 + 4], (double)from.TextureDefs[index * 6 + 5]);
                            var (P0, P1, P2) = (CornerOffsets[oc[0]], CornerOffsets[oc[1]], CornerOffsets[oc[2]]);
                            for (var c = 0; c < 3; c++)
                            {
                                var (s, t) = Barycentric(corners[c], P0, P1, P2);
                                var r = 1 - s - t;
                                uv[c * 2] = (ushort)Math.Clamp(Math.Round(r * u0 + s * u1 + t * u2), 0, 65535);
                                uv[c * 2 + 1] = (ushort)Math.Clamp(Math.Round(r * v0 + s * v1 + t * v2), 0, 65535);
                            }
                        }
                        var key = string.Join(',', uv);
                        if (!table.TryGetValue(key, out var newIndex))
                        {
                            newIndex = table.Count;
                            table[key] = newIndex;
                            list.AddRange(uv);
                        }
                        to.SetPolygon(x, z, half, polygon.With(textureIndex: newIndex, diagonal: diagonal).Raw);
                    }
                }
            if (table.Count > 0x2000) throw new InvalidDataException($"Cube ({n.X}, {n.Z}) needs {table.Count} ground texture definitions, more than 8,192.");
            to.TextureDefs = list.ToArray();
            defs = Math.Max(defs, table.Count);
        }
        // the objects: each in the new cube it stands in, k times as far from the anchor and as high, its box k times
        var objects = 0;
        foreach (var o in old)
        {
            var from = source.CubeAt(o.X, o.Z)!;
            foreach (var d in from.Decors)
            {
                double wx = o.X * 32768.0 + d.X, wz = o.Z * 32768.0 + d.Z;
                double nx = Far(wx, anchor.X, k), nz = Far(wz, anchor.Z, k);
                int cx = (int)Math.Floor(nx / 32768), cz = (int)Math.Floor(nz / 32768);
                if (island.CubeAt(cx, cz) is not { } to) continue;
                var copy = d.Clone();
                int Lx(double v) => (int)Math.Round(Far(o.X * 32768.0 + v, anchor.X, k) - cx * 32768.0);
                int Lz(double v) => (int)Math.Round(Far(o.Z * 32768.0 + v, anchor.Z, k) - cz * 32768.0);
                copy.X = Lx(d.X); copy.Z = Lz(d.Z); copy.Y = d.Y * k;
                copy.XMin = Lx(d.XMin); copy.XMax = Lx(d.XMax); copy.ZMin = Lz(d.ZMin); copy.ZMax = Lz(d.ZMax);
                copy.YMin = d.YMin * k; copy.YMax = d.YMax * k;
                if (to.Decors.Count >= IslandDecors.MaxPerCube) continue;
                to.Decors.Add(copy);
                objects++;
            }
        }
        var name = source.Path is { Length: > 0 } p ? Path.GetFileName(p).Replace(".before-racetrack", "") : "the island";
        log.Add($"{name} {k} times bigger: {old.Count} cubes became {cubes.Count} (cubes {cubes.Min(c => c.New.X)}..{cubes.Max(c => c.New.X)} x {cubes.Min(c => c.New.Z)}..{cubes.Max(c => c.New.Z)}), " +
                $"{objects} objects, at most {defs} ground texture definitions in a cube");
        return island;
    }

    private static double Far(double w, int anchorCube, int k) => anchorCube * 32768.0 + k * (w - anchorCube * 32768.0);

    private static readonly int[][] HalfCorners = { new[] { 0, 1, 2 }, new[] { 2, 3, 0 }, new[] { 3, 0, 1 }, new[] { 1, 2, 3 } };
    private static readonly (int X, int Z)[] CornerOffsets = { (0, 0), (0, 1), (1, 1), (1, 0) };

    // (s, t) of point p in the triangle p0, p1, p2: p = p0 + s (p1 - p0) + t (p2 - p0)
    private static (double S, double T) Barycentric((double X, double Z) p, (int X, int Z) p0, (int X, int Z) p1, (int X, int Z) p2)
    {
        double ax = p1.X - p0.X, az = p1.Z - p0.Z, bx = p2.X - p0.X, bz = p2.Z - p0.Z;
        double px = p.X - p0.X, pz = p.Z - p0.Z;
        var det = ax * bz - az * bx;
        return ((px * bz - pz * bx) / det, (ax * pz - az * px) / det);
    }

    // An old cube's height, light and water depth at twice-vertex coordinates (x2, z2): on a vertex, its; on an edge's middle, the two
    // ends'; in a cell's middle, its diagonal's two ends' (the ground there is on the diagonal).
    private static (double H, double L, int Water) Sample(IslandCube c, int x2, int z2)
    {
        int x = x2 / 2, z = z2 / 2;
        double H(int vx, int vz) => c.Heights[Math.Min(vz, 64) * 65 + Math.Min(vx, 64)];
        double L(int vx, int vz) => c.Intensity[Math.Min(vz, 64) * 65 + Math.Min(vx, 64)] & 15;
        int W(int vx, int vz) => c.Intensity[Math.Min(vz, 64) * 65 + Math.Min(vx, 64)] >> 4;
        bool ex = x2 % 2 == 0, ez = z2 % 2 == 0;
        if (ex && ez) return (H(x, z), L(x, z), W(x, z));
        if (ez) return ((H(x, z) + H(x + 1, z)) / 2, (L(x, z) + L(x + 1, z)) / 2, Math.Max(W(x, z), W(x + 1, z)));
        if (ex) return ((H(x, z) + H(x, z + 1)) / 2, (L(x, z) + L(x, z + 1)) / 2, Math.Max(W(x, z), W(x, z + 1)));
        var diagonal = new IslandPolygon(c.Polygon(Math.Min(x, 63), Math.Min(z, 63), 0)).Diagonal;
        var (a, b) = diagonal ? ((x + 1, z), (x, z + 1)) : ((x, z), (x + 1, z + 1));
        return ((H(a.Item1, a.Item2) + H(b.Item1, b.Item2)) / 2, (L(a.Item1, a.Item2) + L(b.Item1, b.Item2)) / 2, Math.Max(W(a.Item1, a.Item2), W(b.Item1, b.Item2)));
    }

    // A new island file with `cubes` (in that order, ids 1..): the source's atlases; each cube's records blank (settings, no objects, cells,
    // one texture definition, heights 0, light 10) for Scale to fill.
    private static byte[] NewFile(IslandFile source, List<(int X, int Z)> cubes)
    {
        var entries = new List<byte[]>();
        var map = new byte[IslandFile.MapSize * IslandFile.MapSize];
        for (var c = 0; c < cubes.Count; c++) map[cubes[c].Z * IslandFile.MapSize + cubes[c].X] = (byte)(c + 1);
        entries.Add(map);
        entries.Add((byte[])source.GroundTexture.Clone());
        entries.Add((byte[])source.ObjectTexture.Clone());
        var info = new byte[IslandCube.InfoSize * 4];
        var polygons = new byte[IslandCube.Cells * IslandCube.Cells * 2 * 4];
        var textures = new byte[12];
        var heights = new byte[IslandCube.Vertices * IslandCube.Vertices * 2];
        var light = Enumerable.Repeat((byte)10, IslandCube.Vertices * IslandCube.Vertices).ToArray();
        for (var c = 0; c < cubes.Count; c++)
        {
            entries.Add((byte[])info.Clone());
            entries.Add(Array.Empty<byte>());
            entries.Add((byte[])polygons.Clone());
            entries.Add((byte[])textures.Clone());
            entries.Add((byte[])heights.Clone());
            entries.Add((byte[])light.Clone());
        }
        var stored = entries.Select(e => e.Length == 0 ? null : HqrWriter.StoredEntry(e)).ToList();
        var tableBytes = (stored.Count + 1) * 4;
        var total = tableBytes + stored.Sum(e => e?.Length ?? 0);
        var result = new byte[total];
        var at = tableBytes;
        for (var i = 0; i < stored.Count; i++)
        {
            if (stored[i] is not { } e) continue;
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4), (uint)at);
            e.CopyTo(result.AsSpan(at));
            at += e.Length;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(stored.Count * 4), (uint)total);
        return result;
    }

    // Every body of an OBL file k times its size (in the folder: the island's, before the build appends its own).
    public static string ScaleObl(string oblPath, int k)
    {
        var archive = HqrArchive.Open(oblPath);
        var count = HqrArchive.CountEntries(oblPath);
        var bodies = new List<byte[]>();
        var clipped = 0;
        for (var i = 0; i < count; i++)
        {
            var body = archive.Read(i);
            bodies.Add(body.Length >= 96 ? ScaledBody(body, k, ref clipped) : body);
        }
        File.WriteAllBytes(oblPath, Polar.PolarIsland.Hqr(bodies));
        return $"{Path.GetFileName(oblPath)}: its {count} bodies {k} times their size" + (clipped > 0 ? $" ({clipped} coordinates past what a body holds, kept at its edge)" : "");
    }

    // A body k times its size: its box (header 8-31), its points (count at 40, offset at 44: x, y, z, bone, 8 bytes each) and its spheres'
    // radii (count at 80, offset at 84: radius at 6).
    public static byte[] ScaledBody(byte[] body, double k, ref int clipped)
    {
        var b = (byte[])body.Clone();
        int I(int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
        var lost = 0;
        void Scale16(int at)
        {
            var v = Math.Round(BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at)) * k);
            if (v > short.MaxValue || v < short.MinValue) lost++;
            BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(at), (short)Math.Clamp(v, short.MinValue, short.MaxValue));
        }
        for (var at = 8; at < 32; at += 4) BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), (int)Math.Round(I(at) * k));
        int points = I(40), p = I(44);
        if (p < 0 || p + points * 8 > b.Length) return body;
        for (var i = 0; i < points; i++, p += 8) { Scale16(p); Scale16(p + 2); Scale16(p + 4); }
        int spheres = I(80), q = I(84);
        if (spheres > 0 && q > 0 && q + spheres * 8 <= b.Length)
            for (var i = 0; i < spheres; i++, q += 8)
                BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(q + 6), (ushort)Math.Min(65535, Math.Round(BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(q + 6)) * k)));
        clipped += lost;
        return b;
    }

    // ---- the holomap ---------------------------------------------------------------------------------------------------------------

    // The island's holomap k times as big (HOLOMAP.HQR in the folder, its original): its picture's camera (RaceTrackHolomap.Camera: the
    // target, two angles, a distance) the target k times as far from the anchor and the distance k times -- the camera's projection is a
    // perspective, so the island k times as big about the anchor seen from k times as far comes out the same: the game's own picture
    // still fits it, and the track drawn into it and Twinsen's place on it come out where they are -- and the places of its scenes'
    // arrows (the position records from 50 on whose island it is: island coordinates) k times as far.
    public static string ScaleHolomap(string gameDirectory, RaceTrackIsland where, (int X, int Z) anchor, int k)
    {
        var path = Path.Combine(gameDirectory, RaceTrackHolomap.File);
        var hqr = File.ReadAllBytes(path);
        var archive = HqrArchive.Open(path);
        var cameras = 0;
        foreach (var picture in RaceTrackHolomap.Pictures(where))
        {
            var record = archive.Read(picture + 1);
            if (record.Length < 28) continue;
            int V(int i) => BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(i * 4));
            void Set(int i, int v) => BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(i * 4), v);
            double tx = Far(V(0) * 32768.0 + V(2), anchor.X, k), tz = Far(V(1) * 32768.0 + V(3), anchor.Z, k);
            int cx = (int)Math.Floor(tx / 32768), cz = (int)Math.Floor(tz / 32768);
            Set(0, cx); Set(1, cz); Set(2, (int)Math.Round(tx - cx * 32768.0)); Set(3, (int)Math.Round(tz - cz * 32768.0)); Set(6, V(6) * k);
            hqr = HqrWriter.ReplaceEntry(hqr, picture + 1, HqrWriter.StoredEntry(record));
            cameras++;
        }
        const int Positions = 12, Size = 32, First = 50;
        var positions = archive.Read(Positions);
        var moved = 0;
        for (var at = First * Size; at + Size <= positions.Length; at += Size)
        {
            if (positions[at + 31] != where.IslandByte) continue;
            int x = BinaryPrimitives.ReadInt32LittleEndian(positions.AsSpan(at)), y = BinaryPrimitives.ReadInt32LittleEndian(positions.AsSpan(at + 4)), z = BinaryPrimitives.ReadInt32LittleEndian(positions.AsSpan(at + 8));
            // (island coordinates only: on the island's cubes)
            if (x < (anchor.X - 1) * 32768 || x > (anchor.X + 4) * 32768 || z < (anchor.Z - 1) * 32768 || z > (anchor.Z + 4) * 32768) continue;
            BinaryPrimitives.WriteInt32LittleEndian(positions.AsSpan(at), (int)Math.Round(Far(x, anchor.X, k)));
            BinaryPrimitives.WriteInt32LittleEndian(positions.AsSpan(at + 4), y * k);
            BinaryPrimitives.WriteInt32LittleEndian(positions.AsSpan(at + 8), (int)Math.Round(Far(z, anchor.Z, k)));
            moved++;
        }
        hqr = HqrWriter.ReplaceEntry(hqr, Positions, HqrWriter.StoredEntry(positions));
        File.WriteAllBytes(path, hqr);
        return $"{RaceTrackHolomap.File}: {where.Name}'s holomap camera{(cameras == 1 ? "" : "s")} {k} times as far (its picture fits the island as it is now), {moved} of its scenes' arrows moved with the island";
    }

    // ---- the scenes ----------------------------------------------------------------------------------------------------------------

    private const int Invisible = 1 << 9, NoShadow = 1 << 12;
    private const int EdgeTop = 32000;

    // The island's outside scenes k times as big (SCENE.HQR in the folder, its originals): each scene of an old cube becomes the k x k
    // scenes of its new ones. `island`: the scaled ground (where Twinsen stands in a new scene). Returns lines for the log.
    public static List<string> ScaleScenes(string gameDirectory, RaceTrackIsland where, IslandFile island, (int X, int Z) anchor, int k)
    {
        var log = new List<string>();
        var scenePath = Path.Combine(gameDirectory, "SCENE.HQR");
        var hqr = HqrFile.Parse(File.ReadAllBytes(scenePath));
        SceneModel? Load(int scene) => scene + 1 < hqr.Count && !hqr.IsEmpty(scene + 1) && hqr.Read(scene + 1) is { Length: > 0 } r ? SceneSerializer.Parse(SceneGame.Lba2, r) : null;
        // the island's outside scenes and the cubes they are of
        var olds = new List<(int Scene, SceneModel Model)>();
        for (var s = where.FirstScene; s <= where.LastScene; s++)
            if (Load(s) is { } m && m.Island == where.IslandByte && m.CubeMode == 1) olds.Add((s, m));
        if (olds.Count == 0) throw new InvalidDataException($"{where.Name} has no outside scenes {where.FirstScene}-{where.LastScene} in SCENE.HQR.");
        var oldOf = olds.ToDictionary(o => (o.Model.CubeX, o.Model.CubeY), o => o.Scene);
        // each old scene's new ones: its own number for the cube its Twinsen starts in, the others' from MoreScenes
        var more = new Queue<int>(where.MoreScenes);
        var sceneOf = new Dictionary<(int X, int Z), int>();
        var plan = new List<(int Old, SceneModel Model, int I, int J, (int X, int Z) Cube, int Scene)>();
        foreach (var (scene, model) in olds)
        {
            var hi = Math.Clamp(model.Hero.X * k / 32768, 0, k - 1); var hj = Math.Clamp(model.Hero.Z * k / 32768, 0, k - 1);
            for (var j = 0; j < k; j++)
                for (var i = 0; i < k; i++)
                {
                    var cube = NewCube(model.CubeX, model.CubeY, i, j, k, anchor);
                    var number = (i, j) == (hi, hj) ? scene : more.Count > 0 ? more.Dequeue() : throw new InvalidDataException($"{where.Name}: too few scene numbers for its cubes {k} times bigger (RaceTrackIsland.MoreScenes).");
                    sceneOf[cube] = number;
                    plan.Add((scene, model, i, j, cube, number));
                }
        }
        var outside = sceneOf.Values.ToHashSet();
        var largest = BinaryPrimitives.ReadInt32LittleEndian(hqr.Read(0));
        int zonesCopied = 0, actorsHere = 0, standIns = 0, edges = 0;
        foreach (var (oldScene, source, i, j, cube, number) in plan)
        {
            var model = source.Clone();
            model.CubeX = cube.X; model.CubeY = cube.Z;
            // where something of the old scene is in this one: k times as far from the old cube's corner, less this cube's place in it
            int X(int v) => v * k - i * 32768;
            int Z(int v) => v * k - j * 32768;
            bool Here(int x, int z) => x >= 0 && x < 32768 && z >= 0 && z < 32768;
            // Twinsen: where he starts, or (another cube of it) on the ground nearest its middle
            var hero = model.Hero;
            if (Here(X(source.Hero.X), Z(source.Hero.Z))) { hero.X = X(source.Hero.X); hero.Y = source.Hero.Y * k; hero.Z = Z(source.Hero.Z); }
            else { var (sx, sy, sz) = StandingPlace(island, cube); hero.X = sx; hero.Y = sy; hero.Z = sz; }
            // the others: theirs in this cube; a stand-in in the place of one that isn't
            for (var a = 1; a < model.Actors.Count; a++)
            {
                var actor = model.Actors[a];
                var orig = source.Actors[a];
                // (slot 1 parked at 0,0 is the engine's placeholder for Zoe: as it is)
                if (a == 1 && orig.Entity == 14 && orig.X == 0 && orig.Z == 0) continue;
                int ax = X(orig.X), az = Z(orig.Z);
                if (Here(ax, az)) { actor.X = ax; actor.Y = Math.Clamp(orig.Y * k, short.MinValue, short.MaxValue); actor.Z = az; actorsHere++; continue; }
                actor.Flags = (uint)(Invisible | NoShadow);
                actor.X = Math.Clamp(ax, 0, 32767); actor.Z = Math.Clamp(az, 0, 32767); actor.Y = -16000;
                actor.Life = new byte[] { 0 }; actor.Track = new byte[] { 0 };
                standIns++;
            }
            // the zones: each it reaches, k times as big; the island's own cube changes are made afresh below
            model.Zones.Clear();
            foreach (var zone in source.Zones)
            {
                if (zone.Type == 0 && outside.Contains(zone.Num) || zone.Type == 0 && oldOf.ContainsValue(zone.Num)) continue;
                var copy = zone.Clone();
                copy.X0 = X(zone.X0); copy.X1 = X(zone.X1); copy.Z0 = Z(zone.Z0); copy.Z1 = Z(zone.Z1);
                copy.Y0 = zone.Y0 * k; copy.Y1 = zone.Y1 * k;
                if (copy.X1 < 0 || copy.X0 >= 32768 || copy.Z1 < 0 || copy.Z0 >= 32768) continue;
                model.Zones.Add(copy);
                zonesCopied++;
            }
            // the track points: all of them (scripts use them by number), k times as far
            model.TrackPoints.Clear();
            foreach (var t in source.TrackPoints) model.TrackPoints.Add(new SceneTrackPoint(X(t.X), t.Y * k, Z(t.Z)));
            // the cube changes to its neighbours: east (x + 1), west, south (z + 1), north
            void Edge(int dx, int dz, int x0, int z0, int x1, int z1, int info0, int info2)
            {
                if (!sceneOf.TryGetValue((cube.X + dx, cube.Z + dz), out var next)) return;
                var zone = new SceneZoneModel { Type = 0, Num = next, X0 = x0, Y0 = 0, Z0 = z0, X1 = x1, Y1 = EdgeTop, Z1 = z1, Info = new int[8] };
                zone.Info[0] = info0; zone.Info[2] = info2; zone.Info[7] = 1;   // ZONE_ON
                model.Zones.Add(zone);
                edges++;
            }
            const int Side = 32768, EdgeWidth = 512, Near = 512, Far = 32768 - 1024;
            Edge(1, 0, Side - EdgeWidth, 0, Side, Side, Near, 0);
            Edge(-1, 0, 0, 0, EdgeWidth, Side, Far, 0);
            Edge(0, 1, 0, Side - EdgeWidth, Side, Side, 0, Near);
            Edge(0, -1, 0, 0, Side, EdgeWidth, 0, Far);
            var record = SceneSerializer.Write(model);
            var entry = number + 1;
            while (hqr.Count < entry) hqr.Slots.Add(new HqrFile.Slot());
            if (hqr.Count == entry) hqr.Add(record); else hqr.SetStored(entry, record);
            largest = Math.Max(largest, record.Length);
        }
        // every other scene's ways onto the island (an inside scene's doors out, a ferry's arrival): to where they lead now
        var doors = 0;
        for (var s = 0; s + 1 < hqr.Count; s++)
        {
            if (outside.Contains(s) || hqr.IsEmpty(s + 1)) continue;
            SceneModel model;
            try { model = SceneSerializer.Parse(SceneGame.Lba2, hqr.Read(s + 1)); }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException) { continue; }
            var changed = false;
            foreach (var zone in model.Zones)
            {
                if (zone.Type != 0 || !olds.Any(o => o.Scene == zone.Num)) continue;
                var from = olds.First(o => o.Scene == zone.Num).Model;
                int nx = zone.Info[0] * k, nz = zone.Info[2] * k;
                int i = Math.Clamp(nx / 32768, 0, k - 1), j = Math.Clamp(nz / 32768, 0, k - 1);
                var cube = NewCube(from.CubeX, from.CubeY, i, j, k, anchor);
                zone.Num = sceneOf[cube];
                zone.Info[0] = nx - i * 32768; zone.Info[2] = nz - j * 32768; zone.Info[1] *= k;
                changed = true;
                doors++;
            }
            if (!changed) continue;
            var record = SceneSerializer.Write(model);
            hqr.SetStored(s + 1, record);
            largest = Math.Max(largest, record.Length);
        }
        var size = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(size, largest);
        hqr.SetStored(0, size);
        File.WriteAllBytes(scenePath, hqr.ToBytes());
        // their names, for the editor's scene list
        string? names = null;
        foreach (var (oldScene, _, i, j, cube, number) in plan.Where(p => p.Scene != p.Old))
            names = HqdWriter.Describe(gameDirectory, "SCENE.HQR", SceneGame.Lba2, number + 1, $"{where.Name} ({k} times its size), cube ({cube.X},{cube.Z}) -- part of scene {oldScene}'s", names);
        if (names is not null) File.WriteAllText(Path.Combine(gameDirectory, HqdWriter.SidecarName("SCENE.HQR")), names, System.Text.Encoding.Latin1);
        log.Add($"SCENE.HQR: {where.Name}'s {olds.Count} outside scenes {k} times as big, {plan.Count} scenes ({string.Join(", ", plan.Select(p => $"{p.Scene} ({p.Cube.X},{p.Cube.Z})"))}): " +
                $"{zonesCopied} zones, {edges} cube changes between them, {actorsHere} actors where they were and {standIns} stand-ins in others' places; {doors} ways onto the island from other scenes moved");
        return log;
    }

    // A place for Twinsen in a new cube: the ground nearest its middle that is over the sea, on a vertex (cube-local; the height the ground's).
    private static (int X, int Y, int Z) StandingPlace(IslandFile island, (int X, int Z) cube)
    {
        var best = (X: 16384, Y: 0, Z: 16384);
        var bestD = double.MaxValue;
        if (island.CubeAt(cube.X, cube.Z) is not { } c) return best;
        for (var vz = 2; vz <= 62; vz++)
            for (var vx = 2; vx <= 62; vx++)
            {
                var h = c.Heights[vz * 65 + vx];
                if (h < 400) continue;
                var d = (vx - 32) * (vx - 32) + (vz - 32) * (vz - 32);
                if (d < bestD) { bestD = d; best = (vx * 512, h, vz * 512); }
            }
        return best;
    }
}
