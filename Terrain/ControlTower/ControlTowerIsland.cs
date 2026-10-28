using System.IO;
using System.Numerics;
using LbaBodyStudio;
using LBAAssembler.Terrain.Palace;

namespace LBAAssembler.Terrain.ControlTower;

// Island CX's control tower, lower level, as a 3D building under the island (the user, 2026-10-08: "Let's convert the 'control tower,
// lower level' into a 3D island ... 180: Island CX, Secret passage needs moving underneath 181: Island CX, Room in the emperor, and we'll
// probably need to do a little bit of stretching so it all fits where it's meant to. This is going to be an extra area for our island CX
// track, we're going to start outside, enter through a door, drive around inside, and come back out again").
//
// 2026-10-12: "The CX tower we created looks terrible, the top part should be identical ILOTCX.ILE. The other sections we combined into it
// should be below the top part vertically, almost as though they're under the gas layer". So the island is Island CX as it is -- its ground,
// its buildings, the tower and the landing pads untouched -- and the lower level is underneath it, under the gas the island stands in:
//   - the room (181), the inside of the two L-shaped buildings and the block between them -- two L-shaped halls round the control room -- is
//     the same shape as the plateau at twice its size: its 64 cells are the whole of Island CX's one cube, the plateau's 32 in its middle,
//     so the room lies under the island cell for cell, each hall under its building. Brick for brick (BrickBuilding, as the Palace island's):
//     every filled cell a box a brick across and a layer high, in its brick's colour matched in Island CX's palette; its highest wall a
//     little under the gas (height 0, the sea round the island);
//   - the secret passage (180) under the room: a walkway hung between two shafts, each shaft's top one of the grates. The grates are 37 cells
//     apart east and 32 north, the shafts 28 east, so it is stretched to fit: the first shaft under the first grate, its posts lengthened by
//     7 layers (its top reaches the floor as the second's does), the walkway repeated along an L -- east, a corner, north -- and the second
//     shaft under the second grate. (The editor's joined map puts the passage under the room the same way: Lba2Areas.)
// That is some 480 objects in the one cube -- the building's bodies and its collision boxes -- more than the retail engine's 200 a cube, as
// many as this editor's engine draws (IslandDecors.MaxPerCubeOwnEngine). CXTOWER.OBL is ILOTCX.OBL with the building's bodies on its end. No
// scenes yet: a race track on it comes later (and a way down to the halls with it).
internal static class ControlTowerIsland
{
    public const string IleFile = "CXTOWER.ILE", OblFile = "CXTOWER.OBL";
    public const string SourceIle = "ILOTCX.ILE", SourceObl = "ILOTCX.OBL";
    public const int PaletteEntry = 36;                 // RESS.HQR: Island CX's
    public const int Room = 181, Passage = 180;
    public const int GasLevel = 0;                      // the gas round the island: the height of its sea
    public const int UnderGas = 512;                    // the room's highest wall's top this far under it
    public const int PassageDrop = 25;                  // the passage's layers under the room's (its highest, 24, just under the room's floor)
    public const int RoofBelow = 25;                    // a roof's height: the walls round it, this percentile (most are low; the tall ones stand through)
    // the grates in the room's floor (the zones down to the passage): their holes
    private static readonly (int X, int Z)[] Grates = { (9, 34), (10, 34), (9, 35), (10, 35), (46, 2), (47, 2), (46, 3), (47, 3) };
    private static readonly (float R, float G, float B) RoofColour = (112, 120, 128), RoofEdge = (72, 78, 86);

    public sealed record Built(int RoomCells, int PassageCells, int Bodies, int Faces, int Boxes, int Roofs, int Floor, string Log);

    // The race track's island (RaceTrackIsland.CxTower, the user, 2026-10-15: "Create a track for our CXTower island ... the track enters
    // the underground section through a slope down through one of the doors ... loops around the underground section a few times then back
    // out a door on the other side"): the room one ring of a hall round the control room -- the walls between the two halls and the empty
    // corners under the island taken down, the corners floored and walled round as the halls are, what stood on the halls' floors gone (the
    // doors' landings, the consoles, the grates' rims; the grates shut), the low outer walls built up to the tall ones' height -- under one
    // roof just under the gas, open only where the road comes down through it (RoofHoles, the room's cells: CxTowerTrack.RoofHoles). No
    // secret passage under it.
    // (NoBox: cells of the roof drawn but with nothing to hit, over the road where it runs close under the roof: CxTowerTrack.RoofLow)
    public sealed record RaceRoom(HashSet<(int X, int Z)> RoofHoles, HashSet<(int X, int Z)>? NoBox = null);
    public const int ControlLo = 20, ControlHi = 41;   // the control room in the room's middle (its cells, both ways): left as it is
    public const int TopLayer = 19;                     // the tall walls' top layer: the ring's walls all that high, its roof on them
    private const int Side = 64;

    // Builds CXTOWER.ILE and CXTOWER.OBL into `outDirectory` from the game folder's scenes 180 and 181 and Island CX's island files
    // (`sourceDirectory`: where pristine ILOTCX.ILE/.OBL are, the game folder itself by default). (`roofed`: a roof over each hall and the
    // control room -- under the gas there is nothing to hide them from, so none by default; `openGrates`: false leaves the grates' bricks
    // in the floor.)
    public static Built Build(string gameDirectory, string outDirectory, string? sourceDirectory = null, bool roofed = false, bool openGrates = true,
        RaceRoom? race = null)
    {
        sourceDirectory ??= gameDirectory;
        if (race is not null) { openGrates = false; roofed = false; }
        var log = new List<string>();
        var interiors = new Lba2Interiors(gameDirectory);

        // ---- the room's cells and the passage's, in the room's cells and layers
        var room = new Dictionary<(int X, int Y, int Z), int>();
        foreach (var p in interiors.Placements(Room)) room.TryAdd((p.X, p.Y, p.Z), p.Brick);
        if (room.Count == 0) throw new InvalidDataException("The game folder has no control tower room (scene 181).");
        if (openGrates) foreach (var (x, z) in Grates) room.Remove((x, 0, z));
        if (race is not null) log.Add(RingRoom(room));
        var passage = race is null ? PassageCells(interiors) : new Dictionary<(int X, int Y, int Z), int>();
        log.Add($"the control tower's lower level: the room (scene {Room}) {room.Count} cells" +
                (race is null ? $", the secret passage (scene {Passage}) {passage.Count} under it, stretched to its two grates" : ", the secret passage left out"));

        // ---- the island: Island CX as it is; the room under its cube, cell for cell, its highest wall a little under the gas
        var island = IslandFile.Load(Path.Combine(sourceDirectory, SourceIle));
        var anchor = IslandScaler.Anchor(island);
        double X0 = anchor.X * 32768.0, Z0 = anchor.Z * 32768.0;
        var highest = room.Keys.Max(c => c.Y);
        var top = room.GroupBy(c => (c.Key.X, c.Key.Z)).ToDictionary(g => g.Key, g => g.Max(c => c.Key.Y));
        var roofLayers = roofed ? Halls(top).Select(h => RoofLayer(h, top)).DefaultIfEmpty(0).Max() : race is not null ? TopLayer : 0;
        double Y0 = GasLevel - UnderGas - (Math.Max(highest, roofLayers) + 1) * 256.0;
        log.Add($"{SourceIle} as it is; the room's floor at {Y0:0}, its highest wall (layer {highest}) {UnderGas} under the gas at {GasLevel}, the passage's floor at {Y0 + (2 - PassageDrop) * 256.0:0}");

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

        // ---- the roofs (when asked for): over each hall and the control room, at the height of most of its walls
        var roofs = 0;
        var roofBoxes = new List<(double X0, double Y0, double Z0, double X1, double Y1, double Z1)>();
        if (roofed)
        {
            var covered = new HashSet<(int X, int Z)>();
            foreach (var hall in Halls(top))
            {
                var under = Under(hall, top, covered);
                var layer = RoofLayer(hall, top);
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
        if (race is not null)
        {
            // (the race track's: one roof over the whole ring, on its walls' tops, open where the road comes down through it)
            double ya = Y0 + (TopLayer + 1) * 256.0, yb = ya + 128;
            int roofTop = Nearest(RoofColour), roofSide = Nearest(RoofEdge);
            // (in bodies of 16 x 16 cells, as the room's: one body the whole cube wide was never drawn -- the engine leaves out a decor whose
            // box's corners are all out of sight, and from under its middle they are)
            for (var tz = 0; tz < Side / 16; tz++)
                for (var tx = 0; tx < Side / 16; tx++)
                {
                    var list = new List<BrickBuilding.Face>();
                    var plane = new Dictionary<(int U, int V), int>();
                    for (var z = tz * 16; z < tz * 16 + 16; z++)
                        for (var x = tx * 16; x < tx * 16 + 16; x++)
                            if (!race.RoofHoles.Contains((x, z))) plane[(x, z)] = 0;
                    // (seen only from under it -- the gas hides its top: its underside a face a cell, as the palace's floors are; the
                    // engine's filler runs a face's depth straight across the screen, and the island's ground over the roof showed through
                    // big faces running away from the camera, 2026-10-15)
                    foreach (var (x, z) in plane.Keys)
                    {
                        var a = Corner(x, 0, z); var b = Corner(x + 1, 0, z + 1);
                        list.Add(new BrickBuilding.Face(new[] { new Vector3(a.X, (float)ya, a.Z), new Vector3(b.X, (float)ya, a.Z), new Vector3(b.X, (float)ya, b.Z), new Vector3(a.X, (float)ya, b.Z) },
                            roofSide, -Vector3.UnitY));
                    }
                    var solid = race.NoBox is { Count: > 0 } noBox ? plane.Where(c => !noBox.Contains(c.Key)).ToDictionary(c => c.Key, c => c.Value) : plane;
                    foreach (var (u0, v0, u1, v1, _) in BrickBuilding.Rectangles(solid))
                    {
                        var lo = Corner(u0, 0, v0); var hi = Corner(u1 + 1, 0, v1 + 1);
                        roofBoxes.Add((lo.X, ya, lo.Z, hi.X, yb, hi.Z));
                    }
                    if (list.Count > 0) faces[200 + roofs++] = list;
                }
            log.Add($"one roof over the ring at {ya:0}, open over {race.RoofHoles.Count} cells where the road comes down through it");
        }

        // ---- the bodies: ILOTCX.OBL, the building's on its end
        Directory.CreateDirectory(outDirectory);
        var oblPath = Path.Combine(outDirectory, OblFile);
        File.Copy(Path.Combine(sourceDirectory, SourceObl), oblPath, true);
        var oblBytes = File.ReadAllBytes(oblPath);
        var next = HqrArchive.CountEntries(oblPath);
        var newBodies = new List<byte[]>();
        var (bodyCount, faceCount, decorsLeft) = BrickBuilding.AddBodies(island, faces, newBodies, next, g => g >= 200 ? "a roof" : g >= 100 ? "the passage" : "the room",
            IslandDecors.MaxPerCubeOwnEngine, drawnFrom: g => g >= 200 ? (int)Y0 : null, close: race is not null);

        // ---- the collision: the filled cells merged into boxes, and the roofs, each a decor with an empty body
        var empty = next + newBodies.Count;
        newBodies.Add(RaceTrackPipes.Write(new List<Vector3> { Vector3.Zero }, new List<Face>(), lit: false));
        var boxes = BrickBuilding.Boxes(cells.Keys.ToHashSet())
            .Select(b => { var lo = Corner(b.X0, b.Y0, b.Z0); var hi = Corner(b.X1 + 1, b.Y1 + 1, b.Z1 + 1); return ((double)lo.X, (double)lo.Y, (double)lo.Z, (double)hi.X, (double)hi.Y, (double)hi.Z); })
            .Concat(roofBoxes).ToList();
        var (boxCount, boxesLeft) = BrickBuilding.AddBoxes(island, empty, boxes, IslandDecors.MaxPerCubeOwnEngine);
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
        log.Add($"{IleFile}: Island CX as it is, the lower level under it; objects per cube: {perCube} (this editor's engine draws {IslandDecors.MaxPerCubeOwnEngine} a cube, the retail one {IslandDecors.MaxPerCube})");
        log.Add($"{OblFile}: {bodyCount} bodies of the building ({faceCount} faces{(roofs > 0 ? $", {roofs} roofs among them" : "")}) and an empty one for the {boxCount} collision boxes, from body {next} on" +
                (decorsLeft > 0 ? $"; WARNING: {decorsLeft} decors left out (a cube full)" : ""));
        return new Built(room.Count, passage.Count, bodyCount, faceCount, boxCount, roofs, (int)Y0, string.Join("\n", log));
    }

    // The race track's ring (RaceRoom): inside the room's outer walls (its cells 1 to 61 each way) everything over the floor taken away, but
    // the control room in the middle; the floor laid where there is none (the empty corners); the outer walls -- the room's edge cells, two
    // deep on the east and south as the halls' are -- built up to TopLayer, the bricks of a tall wall's column (the north-east hall's east
    // wall) where a wall has none of its own.
    private static string RingRoom(Dictionary<(int X, int Y, int Z), int> room)
    {
        static bool Control(int x, int z) => x >= ControlLo && x <= ControlHi && z >= ControlLo && z <= ControlHi;
        static bool Edge(int x, int z) => x <= 0 || z <= 0 || x >= Side - 2 || z >= Side - 2;
        var floor = room.Where(c => c.Key.Y == 0 && !Edge(c.Key.X, c.Key.Z) && !Control(c.Key.X, c.Key.Z)).GroupBy(c => c.Value)
            .OrderByDescending(g => g.Count()).Select(g => g.Key).First();
        // (the wall's column: the north-east hall's east wall, its bricks layer by layer -- the one under where it has none)
        var column = new int[TopLayer + 1];
        for (var y = 0; y <= TopLayer; y++)
            column[y] = room.TryGetValue((Side - 2, y, 10), out var b) ? b : y > 0 ? column[y - 1] : floor;
        int cleared = 0, floored = 0, walled = 0;
        var byColumn = room.Keys.GroupBy(c => (c.X, c.Z)).ToDictionary(g => g.Key, g => g.ToList());
        for (var z = 0; z < Side; z++)
            for (var x = 0; x < Side; x++)
            {
                if (Control(x, z) && byColumn.ContainsKey((x, z))) continue;   // (the control room; the empty cells round its corner the ring's)
                if (Edge(x, z))
                {
                    for (var y = 0; y <= TopLayer; y++)
                        if (!room.ContainsKey((x, y, z))) { room[(x, y, z)] = y > 0 && room.TryGetValue((x, y - 1, z), out var under) ? under : column[y]; walled++; }
                    continue;
                }
                if (byColumn.TryGetValue((x, z), out var cells))
                    foreach (var c in cells.Where(c => c.Y > 0)) { room.Remove(c); cleared++; }
                if (!room.ContainsKey((x, 0, z))) { room[(x, 0, z)] = floor; floored++; }
            }
        return $"the race track's ring: {cleared} cells taken off the floor between the outer walls and the control room, {floored} of floor laid, {walled} added to the outer walls (all {TopLayer + 1} layers high)";
    }

    // A hall's floor and its walls round it, two cells deep, as far as the room goes (those a roof before it covers left out).
    private static HashSet<(int X, int Z)> Under(HashSet<(int X, int Z)> hall, Dictionary<(int X, int Z), int> top, HashSet<(int X, int Z)>? covered = null)
    {
        var under = new HashSet<(int X, int Z)>();
        foreach (var (x, z) in hall)
            for (var dz = -2; dz <= 2; dz++) for (var dx = -2; dx <= 2; dx++)
                if (top.ContainsKey((x + dx, z + dz)) && covered?.Contains((x + dx, z + dz)) != true) under.Add((x + dx, z + dz));
        return under;
    }

    // The layer a hall's roof is at: as high as its low walls (the halls' are 9 or 10 layers on two sides, 19 on the others, standing through it).
    private static int RoofLayer(HashSet<(int X, int Z)> hall, Dictionary<(int X, int Z), int> top)
    {
        var under = Under(hall, top);
        var walls = under.Where(c => !hall.Contains(c) && top[c] > 8).Select(c => top[c]).OrderBy(h => h).ToList();
        return (walls.Count > 0 ? walls[walls.Count * RoofBelow / 100] : under.Max(c => top[c])) + 1;
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
