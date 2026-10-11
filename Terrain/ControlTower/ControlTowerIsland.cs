using System.IO;
using System.Numerics;
using LbaBodyStudio;
using LBAAssembler.Terrain.Palace;

namespace LBAAssembler.Terrain.ControlTower;

// Island CX's control tower, lower level, as a 3D building on the island itself (the user, 2026-10-08: "Let's convert the 'control tower,
// lower level' into a 3D island ... 180: Island CX, Secret passage needs moving underneath 181: Island CX, Room in the emperor, and we'll
// probably need to do a little bit of stretching so it all fits where it's meant to. This is going to be an extra area for our island CX
// track, we're going to start outside, enter through a door, drive around inside, and come back out again").
//
// Island CX is one cube: a plateau with two landing pads (the octagons), a block in the middle under the control tower, and two L-shaped
// buildings between them, sunk in trenches, roofed, a door at the ends of their arms. The room (181) is the inside of those buildings and
// the middle -- two L-shaped halls round the control room -- the same shape as the plateau at twice the size: its 64 cells are the plateau's
// 32. So the island is made twice as big (IslandScaler, as the Island of the Francos' track did) and the room put in it brick for brick:
//   - the ground: Island CX twice its size, every height half again as high (the plateau 7,680 over the sea: room for the passage under the
//     room), the plateau flat at the doors' thresholds -- the yards where the pads are, outside the doors -- and open under the room down to
//     the passage's floor (the pit);
//   - the room: every filled cell a box a brick across and a layer high, in its brick's colour matched in Island CX's palette (BrickBuilding,
//     as the Palace island's); the old buildings' objects taken away (the tower and the landing pads kept); a roof over each hall and the
//     control room at the height of most of its walls; the control tower on the control room, on the pillars under it; the two grates in
//     the floor open;
//   - the secret passage (180) underneath: a walkway hung between two shafts, each shaft's top one of the grates. The grates are 37 cells
//     apart east and 32 north, the shafts 28 east, so it is stretched to fit: the first shaft under the first grate, its posts lengthened by
//     7 layers (its top reaches the floor as the second's does), the walkway repeated along an L -- east, a corner, north -- and the second
//     shaft under the second grate. (The editor's joined map puts the passage under the room the same way, its first shaft under the first
//     grate: Lba2Areas.)
// CXTOWER.OBL is ILOTCX.OBL twice its size with the building's bodies on its end. No scenes yet: a race track on it comes later.
internal static class ControlTowerIsland
{
    public const string IleFile = "CXTOWER.ILE", OblFile = "CXTOWER.OBL";
    public const string SourceIle = "ILOTCX.ILE", SourceObl = "ILOTCX.OBL";
    public const int PaletteEntry = 36;                 // RESS.HQR: Island CX's
    public const int Room = 181, Passage = 180;
    public const int Times = 2;                         // the island twice its size
    public const double Stretch = 1.5;                  // ... and its heights half again as high
    public const int Yard = 7680;                       // the plateau: the doors' thresholds (the room's layer 2, its top 768)
    public const int Threshold = 768;
    public const int PitFloor = 768;                    // the ground under the room: the passage's own floor's top
    public const int PassageDrop = 25;                  // the passage's layers under the room's (its highest, 24, just under the room's floor)
    public const int RoofBelow = 25;                    // a roof's height: the walls round it, this percentile (most are low; the tall ones stand through)
    // the room's corner in the island twice its size: the plateau's (the original's 16th cell), from the island's first cube's corner
    private const int RoomCorner = 16384;
    // the plateau in vertices of the island twice its size, from its first cube's corner: the original's cells 16-48 (the rim round it)
    private const int PlateauFrom = 32, PlateauTo = 96;
    // the grates in the room's floor (the zones down to the passage): their holes
    private static readonly (int X, int Z)[] Grates = { (9, 34), (10, 34), (9, 35), (10, 35), (46, 2), (47, 2), (46, 3), (47, 3) };
    private static readonly (float R, float G, float B) RoofColour = (112, 120, 128), RoofEdge = (72, 78, 86);

    public sealed record Built(int RoomCells, int PassageCells, int Bodies, int Faces, int Boxes, int Roofs, int Pit, string Log);

    // Builds CXTOWER.ILE and CXTOWER.OBL into `outDirectory` from the game folder's scenes 180 and 181 and Island CX's island files
    // (`sourceDirectory`: where pristine ILOTCX.ILE/.OBL are, the game folder itself by default). (`roofed`: false leaves the roofs off -- a
    // look inside; `openGrates`: false leaves the grates' bricks in the floor.)
    public static Built Build(string gameDirectory, string outDirectory, string? sourceDirectory = null, bool roofed = true, bool openGrates = true)
    {
        sourceDirectory ??= gameDirectory;
        var log = new List<string>();
        var interiors = new Lba2Interiors(gameDirectory);

        // ---- the room's cells and the passage's, in the room's cells and layers
        var room = new Dictionary<(int X, int Y, int Z), int>();
        foreach (var p in interiors.Placements(Room)) room.TryAdd((p.X, p.Y, p.Z), p.Brick);
        if (room.Count == 0) throw new InvalidDataException("The game folder has no control tower room (scene 181).");
        if (openGrates) foreach (var (x, z) in Grates) room.Remove((x, 0, z));
        var passage = PassageCells(interiors);
        log.Add($"the control tower's lower level: the room (scene {Room}) {room.Count} cells, the secret passage (scene {Passage}) {passage.Count} under it, stretched to its two grates");

        // ---- the island: Island CX twice its size, half again as high, the plateau flat at the thresholds and open under the room
        var source = IslandFile.Load(Path.Combine(sourceDirectory, SourceIle));
        var island = IslandScaler.Scale(source, Times, log);
        var anchor = IslandScaler.Anchor(source);
        double ax = anchor.X * 32768.0, az = anchor.Z * 32768.0;
        double X0 = ax + RoomCorner, Z0 = az + RoomCorner, Y0 = Yard - Threshold;
        var footprint = room.Keys.Select(c => (c.X, c.Z)).Concat(passage.Keys.Select(c => (c.X, c.Z))).ToHashSet();
        var pit = 0;
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island).ToList())
        {
            for (var vz = 0; vz < IslandCube.Vertices; vz++)
                for (var vx = 0; vx < IslandCube.Vertices; vx++)
                {
                    var i = vz * IslandCube.Vertices + vx;
                    // (the vertex in the island twice its size, from its first cube's corner; the room's cell corner it is)
                    int lx = (cx - anchor.X) * 64 + vx, lz = (cz - anchor.Z) * 64 + vz;
                    int rx = lx - RoomCorner / 512, rz = lz - RoomCorner / 512;
                    double h;
                    if (lx >= PlateauFrom && lx <= PlateauTo && lz >= PlateauFrom && lz <= PlateauTo)
                    {
                        var under = footprint.Contains((rx - 1, rz - 1)) && footprint.Contains((rx, rz - 1)) && footprint.Contains((rx - 1, rz)) && footprint.Contains((rx, rz));
                        h = under ? PitFloor : Yard;
                        if (under) { cube.Intensity[i] = (byte)(cube.Intensity[i] & 15); pit++; }
                    }
                    else h = cube.Heights[i] * Stretch;
                    cube.Heights[i] = (short)Math.Clamp(Math.Round(h), short.MinValue, short.MaxValue);
                }
        }

        // ---- the objects: the old buildings' taken away; the landing pads as high as their ground now is; the tower on the control room
        var removed = 0; IslandDecor? tower = null; (int X, int Z) towerCube = default;
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island).ToList())
            foreach (var d in cube.Decors.ToList())
            {
                double wx = cx * 32768.0 + d.X - ax, wz = cz * 32768.0 + d.Z - az;
                var onPlateau = wx >= PlateauFrom * 512 && wx <= PlateauTo * 512 && wz >= PlateauFrom * 512 && wz <= PlateauTo * 512;
                if (onPlateau && d.Body == 0 && tower is null) { tower = d; towerCube = (cx, cz); continue; }
                if (onPlateau) { cube.Decors.Remove(d); removed++; continue; }
                var up = (int)Math.Round(d.Y * Stretch) - d.Y;
                d.Y += up; d.YMin += up; if (d.YMax != BrickBuilding.NoBoxTop) d.YMax += up;
            }
        var top = room.GroupBy(c => (c.Key.X, c.Key.Z)).ToDictionary(g => g.Key, g => g.Max(c => c.Key.Y));
        if (tower is not null)
        {
            // (on the highest of the room's columns under it: the control room's pillars and its tall walls, layer 19 -- the original stands
            // a few layers higher, on the block over the trenches the buildings are sunk in)
            double tx = towerCube.X * 32768.0 + tower.X, tz = towerCube.Z * 32768.0 + tower.Z;
            double half = Math.Max(tower.XMax - tower.XMin, tower.ZMax - tower.ZMin) / 2.0 / 512;
            int cx = (int)Math.Floor((tx - X0) / 512), cz = (int)Math.Floor((tz - Z0) / 512), reach = (int)Math.Ceiling(half);
            var under = top.Where(t => Math.Abs(t.Key.X - cx) <= reach && Math.Abs(t.Key.Z - cz) <= reach).Select(t => t.Value).DefaultIfEmpty(0).Max();
            var y = (int)Math.Round(Y0 + (under + 1) * 256.0);
            var up = y - tower.Y;
            tower.Y += up; tower.YMin += up; tower.YMax += up;
            log.Add($"the control tower on the control room's pillars, at {y} (their top, layer {under})");
        }

        // ---- colours: each brick's average in the interiors' palette, matched in Island CX's, shaded by the way a face looks
        var palette = interiors.Palette;
        var islandPalette = IslandMapRenderer.LoadPaletteEntry(gameDirectory, PaletteEntry);
        var averages = new Dictionary<int, (float R, float G, float B)>();
        var matched = new Dictionary<(float, float, float), int>();
        int Nearest((float R, float G, float B) c) => BrickBuilding.Nearest(islandPalette, c, matched);
        int ColourOf(int brick, float shade)
        {
            if (!averages.TryGetValue(brick, out var a)) averages[brick] = a = BrickBuilding.BrickAverage(interiors.ReadBrick(brick), palette);
            return Nearest((MathF.Round(a.R * shade), MathF.Round(a.G * shade), MathF.Round(a.B * shade)));
        }
        Vector3 Corner(double x, double y, double z) => new((float)(X0 + x * 512), (float)(Y0 + y * 256), (float)(Z0 + z * 512));

        // ---- the faces that show, in bodies by 16 x 16 cells (the passage's apart)
        var cells = new Dictionary<(int X, int Y, int Z), (int Brick, int Group)>();
        foreach (var (c, b) in room) cells[c] = (b, (c.X >> 4) + 4 * (c.Z >> 4));
        foreach (var (c, b) in passage) cells[c] = (b, 100 + (c.X >> 4) + 4 * (c.Z >> 4));
        var faces = BrickBuilding.Faces(cells, ColourOf, (x, y, z) => Corner(x, y, z));

        // ---- the roofs: over each hall and the control room, at the height of most of its walls
        var roofs = 0;
        var roofBoxes = new List<(double X0, double Y0, double Z0, double X1, double Y1, double Z1)>();
        if (roofed)
        {
            var covered = new HashSet<(int X, int Z)>();
            foreach (var hall in Halls(top))
            {
                // (the hall's floor and its walls round it, two cells deep, as far as the room goes; the roof as high as the low walls -- the
                // halls' are 9 or 10 layers on two sides, 19 on the others, standing through it)
                var under = new HashSet<(int X, int Z)>();
                foreach (var (x, z) in hall)
                    for (var dz = -2; dz <= 2; dz++) for (var dx = -2; dx <= 2; dx++)
                        if (top.ContainsKey((x + dx, z + dz)) && !covered.Contains((x + dx, z + dz))) under.Add((x + dx, z + dz));
                var walls = under.Where(c => !hall.Contains(c) && top[c] > 8).Select(c => top[c]).OrderBy(h => h).ToList();
                var layer = (walls.Count > 0 ? walls[walls.Count * RoofBelow / 100] : under.Max(c => top[c])) + 1;
                double ya = Y0 + layer * 256.0, yb = ya + 128;
                int roofTop = Nearest(RoofColour), roofSide = Nearest(RoofEdge);
                var list = new List<BrickBuilding.Face>();
                foreach (var (u0, v0, u1, v1, _) in BrickBuilding.Rectangles(under.ToDictionary(c => (c.X, c.Z), _ => 0)))
                {
                    var lo = Corner(u0, 0, v0); var hi = Corner(u1 + 1, 0, v1 + 1);
                    BrickBuilding.AddBox(list, new Vector3(lo.X, (float)ya, lo.Z), new Vector3(hi.X, (float)yb, hi.Z), roofTop, roofSide);
                    roofBoxes.Add((lo.X, ya, lo.Z, hi.X, yb, hi.Z));
                }
                faces[200 + roofs] = list;
                covered.UnionWith(under);
                log.Add($"a roof over {hall.Count} cells of floor at layer {layer} ({ya:0})");
                roofs++;
            }
        }

        // ---- the bodies: ILOTCX.OBL twice its size, the building's on its end
        Directory.CreateDirectory(outDirectory);
        var oblPath = Path.Combine(outDirectory, OblFile);
        File.Copy(Path.Combine(sourceDirectory, SourceObl), oblPath, true);
        log.Add(IslandScaler.ScaleObl(oblPath, Times));
        var oblBytes = File.ReadAllBytes(oblPath);
        var next = HqrArchive.CountEntries(oblPath);
        var newBodies = new List<byte[]>();
        var (bodyCount, faceCount, decorsLeft) = BrickBuilding.AddBodies(island, faces, newBodies, next, g => g >= 200 ? "a roof" : g >= 100 ? "the passage" : "the room");

        // ---- the collision: the filled cells merged into boxes, and the roofs, each a decor with an empty body
        var empty = next + newBodies.Count;
        newBodies.Add(RaceTrackPipes.Write(new List<Vector3> { Vector3.Zero }, new List<Face>(), lit: false));
        var boxes = BrickBuilding.Boxes(cells.Keys.ToHashSet())
            .Select(b => { var lo = Corner(b.X0, b.Y0, b.Z0); var hi = Corner(b.X1 + 1, b.Y1 + 1, b.Z1 + 1); return ((double)lo.X, (double)lo.Y, (double)lo.Z, (double)hi.X, (double)hi.Y, (double)hi.Z); })
            .Concat(roofBoxes).ToList();
        var (boxCount, boxesLeft) = BrickBuilding.AddBoxes(island, empty, boxes);
        decorsLeft += boxesLeft;
        foreach (var cube in island.Cubes.Values) cube.Info[IslandCube.InfoNbDecors] = cube.Decors.Count;

        // ---- written
        // (a build's own file replaced: no .bak of it left beside it)
        var ilePath = Path.Combine(outDirectory, IleFile);
        if (File.Exists(ilePath)) File.Delete(ilePath);
        island.Save(ilePath);
        foreach (var b in newBodies) oblBytes = HqrWriter.AppendEntry(oblBytes, HqrWriter.StoredEntry(b));
        File.WriteAllBytes(oblPath, oblBytes);
        var perCube = string.Join(", ", IslandOps.CubeCells(island).Select(c => $"({c.X},{c.Z}) {c.Cube.Decors.Count}"));
        log.Add($"{IleFile}: Island CX {Times} times its size, its heights {Stretch} times, the plateau flat at {Yard} (the doors' thresholds), open under the room down to {PitFloor} ({pit} vertices); " +
                $"{removed} objects of the old buildings taken away; objects per cube: {perCube}");
        log.Add($"{OblFile}: {bodyCount} bodies of the building ({faceCount} faces, {roofs} roofs among them) and an empty one for the {boxCount} collision boxes, from body {next} on" +
                (decorsLeft > 0 ? $"; WARNING: {decorsLeft} decors left out (a cube full)" : ""));
        return new Built(room.Count, passage.Count, bodyCount, faceCount, boxCount, roofs, pit, string.Join("\n", log));
    }

    // The secret passage's cells in the room's cells and layers: the walkway between two shafts (scene 180, x 21-52, rows 34-37: the shafts
    // x 21-24 and 49-52 up to layers 17 and 24, the walkway between them up to layer 10; the rest of the scene is the floor far below and the
    // clouds over it), PassageDrop layers down, stretched between the room's two grates.
    private static Dictionary<(int X, int Y, int Z), int> PassageCells(Lba2Interiors interiors)
    {
        var src = new Dictionary<(int X, int Y, int Z), int>();
        foreach (var p in interiors.Placements(Passage))
            if (p.Y >= 1 && p.Z >= 34 && p.Z <= 37 && p.X >= 21 && p.X <= 52 && (p.Y <= 10 || p.X <= 24 || p.X >= 49)) src.TryAdd((p.X, p.Y, p.Z), p.Brick);
        if (src.Count == 0) throw new InvalidDataException("The game folder has no secret passage (scene 180).");
        var cells = new Dictionary<(int X, int Y, int Z), int>();
        void Put(int x, int y, int z, int brick) => cells[(x, y - PassageDrop, z)] = brick;
        foreach (var ((x, y, z), b) in src)
        {
            if (x <= 24)
            {
                // the first shaft under the first grate (13 cells west, a row north: as both zones between the scenes put it), its posts 7
                // layers longer and its rim on top of them
                if (y <= 15) Put(x - 13, y, z - 1, b);
                if (y == 15) for (var k = 16; k <= 22; k++) Put(x - 13, k, z - 1, b);
                if (y >= 16) Put(x - 13, y + 7, z - 1, b);
            }
            else if (x >= 49) Put(x - 4, y, z - 33, b);     // the second under the second grate
        }
        // the walkway: its 24 cells, repeated along an L under the room -- east from the first shaft, a corner, north to the second
        int? W(int i, int across, int y) => src.TryGetValue((25 + i % 24, y, 34 + across), out var b) ? b : null;
        for (var x = 12; x <= 44; x++)
            for (var across = 0; across < 4; across++)
                for (var y = 1; y <= 10; y++)
                    if (W(x - 12, across, y) is { } b) Put(x, y, 32 + across, b);
        for (var z = 31; z >= 5; z--)
            for (var across = 0; across < 4; across++)
                for (var y = 1; y <= 10; y++)
                    if (W(31 - z, across, y) is { } b) Put(45 + across, y, z, b);
        // the corner (x 45-48, rows 32-35): the walkway's floor, a post at each corner, its rails along the two outer sides
        int Brick(int x, int y, int z) => src.TryGetValue((x, y, z), out var b) ? b : src.Values.First();
        int floor = Brick(26, 2, 35), post = Brick(28, 5, 34), rail = Brick(26, 10, 34);
        for (var x = 45; x <= 48; x++) for (var z = 32; z <= 35; z++) Put(x, 2, z, floor);
        foreach (var (x, z) in new[] { (45, 32), (48, 32), (48, 35), (45, 35) }) for (var y = 1; y <= 10; y++) Put(x, y, z, post);
        for (var k = 0; k < 4; k++) { Put(48, 10, 32 + k, rail); Put(45 + k, 10, 35, rail); }
        return cells;
    }

    // The room's halls and the control room: the floor (columns no higher than layer 8) in pieces the walls (taller) part, the big ones.
    private static List<HashSet<(int X, int Z)>> Halls(Dictionary<(int X, int Z), int> top)
    {
        var halls = new List<HashSet<(int X, int Z)>>();
        var seen = new HashSet<(int X, int Z)>();
        foreach (var start in top.Keys.OrderBy(c => c.Z).ThenBy(c => c.X))
        {
            if (top[start] > 8 || !seen.Add(start)) continue;
            var hall = new HashSet<(int X, int Z)> { start };
            var queue = new Queue<(int X, int Z)>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var (x, z) = queue.Dequeue();
                foreach (var n in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
                    if (top.TryGetValue(n, out var h) && h <= 8 && seen.Add(n)) { hall.Add(n); queue.Enqueue(n); }
            }
            if (hall.Count >= 60) halls.Add(hall);
        }
        return halls;
    }

    // Tools > LBA2: Island CX control tower -- into the game folder (its own new files: no backups), from the folder's own scenes and Island CX.
    public static bool IsInstalled(string gameDirectory) => File.Exists(Path.Combine(gameDirectory, IleFile));
    public static List<string> Add(string gameDirectory) => new() { Build(gameDirectory, gameDirectory).Log };
    public static List<string> Remove(string gameDirectory)
    {
        var log = new List<string>();
        foreach (var f in new[] { IleFile, OblFile, IleFile + ".bak" })
        {
            var path = Path.Combine(gameDirectory, f);
            if (File.Exists(path)) { File.Delete(path); log.Add($"{f} deleted"); }
        }
        return log;
    }
}
