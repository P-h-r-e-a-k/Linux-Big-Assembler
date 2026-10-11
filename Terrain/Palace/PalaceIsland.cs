using System.IO;
using System.Numerics;
using LbaBodyStudio;
using LBAAssembler.Lba1;

namespace LBAAssembler.Terrain.Palace;

// The Palace island (the user, 2026-10-08: "We need to create a new track based on the Palace that's featured on Otringal, this will need to
// be recreated as a new island. The idea behind this track is that the cars race through the inside of the palace, through the window by
// the chest that Twinsen normally leaves by then around the roof tops. To accomodate all of this we'll probably need to scale up everything
// ... let's just get our Palace island created for now"). The Emperor's palace is a maze of sixteen rooms of 13 x 13 cells, four by four,
// and the last room (80, the chest and the window Twinsen leaves by) off its fourth row: interiors, grids of bricks -- pictures, not shapes.
// Here they become a 3D building on an island of its own:
//   - the rooms placed as the zones between them put them (the editor's joined map, Lba2Areas, without the few cells it moves them apart to
//     keep their shared walls from overlapping on its picture: here neighbours share their walls), every filled cell a box Across times
//     a brick across and Up times a layer high, in the average colour of its brick (the exporter's blocky map, GridMesher) matched in
//     Otringal's palette, its faces shaded by the way they face; only the faces that show, merged into rectangles of one colour;
//   - one body for each room's faces (more where they are many), each a decor whose box nothing touches (it is drawn, not walked on);
//   - the walls, floors and furniture as collision boxes: the filled cells merged into boxes, each a decor with an empty body;
//   - a roof over each room, level with the top of its walls -- the rooftops -- walkable;
//   - the island: OTRINGAL.ILE's atlases and palette (so the game draws it as Otringal when it is raced -- the race-track mode's island_file=),
//     its cubes round the palace a paved plateau at the rooms' floor, falling to the sea at its edge.
// PALACE.OBL is OTRINGAL.OBL with the palace's bodies on its end.
internal static class PalaceIsland
{
    public const string IleFile = "PALACE.ILE", OblFile = "PALACE.OBL";
    public const string SourceIle = "OTRINGAL.ILE", SourceObl = "OTRINGAL.OBL";
    public const int PaletteEntry = 31;                 // RESS.HQR: Otringal's
    public const int Across = 3;                        // a brick's 512 across, this many times
    public const double Up = 2.5;                       // a layer's 256 up, this many times
    public const int Floor = 2200;                      // the rooms' bottom layer's foot, over the island's zero
    public const int Margin = 28;                       // cells of paved ground round the palace
    public const int Slope = 10;                        // ... the last of them falling to the sea
    public const int FirstCube = 6;                     // the island's cubes from (6, 6) on
    // Otringal's paved palace courtyard, whose ground the plateau is painted with (an island cell)
    private static readonly (int Gx, int Gz) PavingCell = (494, 474);
    private static readonly (float R, float G, float B) RoofColour = (176, 78, 52), RoofEdge = (120, 50, 36);

    public sealed record Built(int Rooms, int Cells, int Bodies, int Faces, int Boxes, int Roofs, (int X, int Z) FirstCube, (int X, int Z) Cubes, string Log);

    // Builds PALACE.ILE and PALACE.OBL into `outDirectory` from the game folder's palace scenes and Otringal's island files (`sourceDirectory`:
    // where pristine OTRINGAL.ILE/.OBL are, the game folder itself by default).
    // (`roofed`: false leaves the roofs off -- a look inside)
    public static Built Build(string gameDirectory, string outDirectory, string? sourceDirectory = null, bool roofed = true)
    {
        sourceDirectory ??= gameDirectory;
        var log = new List<string>();
        // ---- the rooms' cells, as the joined map places them (without its separations)
        var interiors = new Lba2Interiors(gameDirectory);
        var area = Lba2Areas.Find(interiors.LoadScene).FirstOrDefault(a => a.Tiles.Any(t => t.Scene == 151))
                   ?? throw new InvalidDataException("The game folder has no Emperor's palace (scenes 151-166 and 80).");
        var cells = new Dictionary<(int X, int Y, int Z), (int Brick, int Tile)>();
        for (var ti = 0; ti < area.Tiles.Count; ti++)
        {
            var tile = area.Tiles[ti];
            var sep = Lba2Areas.Separations.Where(s => s.Scene == tile.Scene).Select(s => (s.Dx, s.Dy, s.Dz)).FirstOrDefault();
            int ox = tile.OffsetX / 512 - sep.Dx, oy = tile.OffsetY / 256 - sep.Dy, oz = tile.OffsetZ / 512 - sep.Dz;
            foreach (var p in interiors.Placements(tile))
                cells.TryAdd((p.X + ox, p.Y + oy, p.Z + oz), (p.Brick, ti));
        }
        if (cells.Count == 0) throw new InvalidDataException("The palace's scenes have no cells.");
        int minX = cells.Keys.Min(c => c.X), maxX = cells.Keys.Max(c => c.X), minY = cells.Keys.Min(c => c.Y), maxY = cells.Keys.Max(c => c.Y);
        int minZ = cells.Keys.Min(c => c.Z), maxZ = cells.Keys.Max(c => c.Z);
        log.Add($"the palace: {area.Tiles.Count} rooms (scenes {string.Join(", ", area.Tiles.Select(t => t.Scene))}), {cells.Count} filled cells, " +
                $"{maxX - minX + 1} x {maxZ - minZ + 1} cells and {maxY - minY + 1} layers");

        // ---- the island: its cubes round the palace
        const double U = 512.0 * Across, V = 256.0 * Up;
        var spanX = (maxX - minX + 1) * Across + 2 * Margin; var spanZ = (maxZ - minZ + 1) * Across + 2 * Margin;
        int cubesX = (spanX + 63) / 64, cubesZ = (spanZ + 63) / 64;
        if (FirstCube + cubesX > 15 || FirstCube + cubesZ > 15) throw new InvalidDataException($"The palace needs {cubesX} x {cubesZ} cubes: more than the map holds from cube {FirstCube}.");
        // (the palace in the middle of its cubes: world units of its first cell's west and north faces)
        double x0 = FirstCube * 32768.0 + (cubesX * 64 - (maxX - minX + 1) * Across) / 2.0 * 512;
        double z0 = FirstCube * 32768.0 + (cubesZ * 64 - (maxZ - minZ + 1) * Across) / 2.0 * 512;
        double WX(int x) => x0 + (x - minX) * U;
        double WZ(int z) => z0 + (z - minZ) * U;
        double WY(int y) => Floor + (y - minY) * V;
        var source = IslandFile.Load(Path.Combine(sourceDirectory, SourceIle));
        var cubeList = new List<(int X, int Z)>();
        for (var cz = 0; cz < cubesZ; cz++) for (var cx = 0; cx < cubesX; cx++) cubeList.Add((FirstCube + cx, FirstCube + cz));
        var island = IslandFile.Parse(IslandScaler.NewFile(source, cubeList), Path.Combine(outDirectory, IleFile));
        Ground(island, source, cubeList, x0, z0, (maxX - minX + 1) * U, (maxZ - minZ + 1) * U);

        // ---- colours: each brick's average in the interiors' palette, matched in Otringal's, shaded by the way a face looks
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

        // ---- the faces that show, merged into rectangles of one colour, room by room
        var roomFaces = BrickBuilding.Faces(cells, ColourOf, (x, y, z) => new Vector3((float)WX(x), (float)WY(y), (float)WZ(z)));

        // ---- the roofs: over each room, level with the top of its walls
        var roofs = 0;
        var roofBoxes = new List<(double X0, double Y0, double Z0, double X1, double Y1, double Z1)>();
        foreach (var ti in roofed ? roomFaces.Keys.ToList() : new List<int>())
        {
            var mine = cells.Where(c => c.Value.Tile == ti).Select(c => c.Key).ToList();
            if (mine.Count == 0) continue;
            int rx0 = mine.Min(c => c.X), rx1 = mine.Max(c => c.X), rz0 = mine.Min(c => c.Z), rz1 = mine.Max(c => c.Z);
            // (the walls' top: the highest layer that the room's edge cells reach, most of the way round)
            var edge = mine.Where(c => c.X == rx0 || c.X == rx1 || c.Z == rz0 || c.Z == rz1).GroupBy(c => (c.X, c.Z)).Select(g => g.Max(c => c.Y)).OrderBy(h => h).ToList();
            var top = edge.Count > 0 ? edge[(int)(edge.Count * 0.8)] + 1 : mine.Max(c => c.Y) + 1;
            double ya = WY(top), yb = ya + V * 0.4;
            int roofTop = Nearest(RoofColour), roofSide = Nearest(RoofEdge);
            var list = roomFaces[ti];
            // (in slabs of MaxRun cells at the most: a body holds no more)
            for (var sx = rx0; sx <= rx1; sx += BrickBuilding.MaxRun)
            for (var sz = rz0; sz <= rz1; sz += BrickBuilding.MaxRun)
            {
            double xa = WX(sx), xb = WX(Math.Min(rx1, sx + BrickBuilding.MaxRun - 1) + 1), za = WZ(sz), zb = WZ(Math.Min(rz1, sz + BrickBuilding.MaxRun - 1) + 1);
            BrickBuilding.AddBox(list, new((float)xa, (float)ya, (float)za), new((float)xb, (float)yb, (float)zb), roofTop, roofSide);
            roofBoxes.Add((xa, ya, za, xb, yb, zb));
            }
            roofs++;
        }

        // ---- the bodies: each room's faces (a few bodies where they are many), each a decor nothing touches
        var oblBytes = File.ReadAllBytes(Path.Combine(sourceDirectory, SourceObl));
        var next = HqrArchive.CountEntries(Path.Combine(sourceDirectory, SourceObl));
        var newBodies = new List<byte[]>();
        var (bodyCount, faceCount, decorsLeft) = BrickBuilding.AddBodies(island, roomFaces, newBodies, next, ti => $"room {area.Tiles[ti].Scene}");

        // ---- the collision: the filled cells merged into boxes, and the roofs, each a decor with an empty body
        var empty = next + newBodies.Count;
        newBodies.Add(RaceTrackPipes.Write(new List<Vector3> { Vector3.Zero }, new List<Face>(), lit: false));
        var boxes = BrickBuilding.Boxes(cells.Keys.ToHashSet())
            .Select(b => (WX(b.X0), WY(b.Y0), WZ(b.Z0), WX(b.X1 + 1), WY(b.Y1 + 1), WZ(b.Z1 + 1))).Concat(roofBoxes).ToList();
        var (boxCount, boxesLeft) = BrickBuilding.AddBoxes(island, empty, boxes);
        decorsLeft += boxesLeft;
        foreach (var cube in island.Cubes.Values) cube.Info[IslandCube.InfoNbDecors] = cube.Decors.Count;

        // ---- written
        Directory.CreateDirectory(outDirectory);
        // (a build's own file replaced: no .bak of it left beside it)
        var ilePath = Path.Combine(outDirectory, IleFile);
        if (File.Exists(ilePath)) File.Delete(ilePath);
        island.Save(ilePath);
        foreach (var b in newBodies) oblBytes = HqrWriter.AppendEntry(oblBytes, HqrWriter.StoredEntry(b));
        File.WriteAllBytes(Path.Combine(outDirectory, OblFile), oblBytes);
        log.Add($"{IleFile}: {cubesX} x {cubesZ} cubes from cube ({FirstCube}, {FirstCube}), a paved plateau at {Floor} round the palace, the sea round it; " +
                $"the palace {Across} times a brick across and {Up} times a layer up ({(maxX - minX + 1) * Across} x {(maxZ - minZ + 1) * Across} cells)");
        log.Add($"{OblFile}: {bodyCount} bodies of the rooms ({faceCount} faces, {roofs} roofs among them) and an empty one for the {boxCount} collision boxes, from body {next} on" +
                (decorsLeft > 0 ? $"; WARNING: {decorsLeft} decors left out (a cube full)" : ""));
        return new Built(area.Tiles.Count, cells.Count, bodyCount, faceCount, boxCount, roofs, (FirstCube, FirstCube), (cubesX, cubesZ), string.Join("\n", log));
    }

    // Tools > LBA2: Palace island -- into the game folder (its own new files: no backups), from the folder's own palace scenes and Otringal.
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

    // The ground: every cell painted with Otringal's courtyard paving, its cubes' settings Otringal's palace cube's; flat at the floor round
    // the palace (just under it), falling to the sea over the last Slope cells of the margin.
    private static void Ground(IslandFile island, IslandFile source, List<(int X, int Z)> cubes, double x0, double z0, double width, double depth)
    {
        var sample = IslandGround.Pick(source, PavingCell.Gx, PavingCell.Gz, 0);
        var settings = source.CubeAt(PavingCell.Gx / 64, PavingCell.Gz / 64);
        double gx0 = x0 / 512 - (Margin - Slope), gz0 = z0 / 512 - (Margin - Slope), gx1 = (x0 + width) / 512 + (Margin - Slope), gz1 = (z0 + depth) / 512 + (Margin - Slope);
        foreach (var (cx, cz) in cubes)
        {
            if (island.CubeAt(cx, cz) is not { } cube) continue;
            if (settings is not null) cube.Info = (int[])settings.Info.Clone();
            for (var vz = 0; vz < IslandCube.Vertices; vz++)
                for (var vx = 0; vx < IslandCube.Vertices; vx++)
                {
                    double gx = cx * 64 + vx, gz = cz * 64 + vz;
                    var outside = Math.Max(Math.Max(gx0 - gx, gx - gx1), Math.Max(gz0 - gz, gz - gz1));
                    var h = outside <= 0 ? Floor - 60 : outside >= Slope ? 0 : (Floor - 60) * (1 - outside / Slope);
                    cube.Heights[vz * IslandCube.Vertices + vx] = (short)Math.Round(h);
                    cube.Intensity[vz * IslandCube.Vertices + vx] = 10;
                }
            if (sample is null) continue;
            var region = new BrickBuilding.CubeCells(cx, cz);
            IslandGround.Paint(island, region, sample, PolygonFields.Texture | PolygonFields.GameCode);
        }
    }
}
