using System.Buffers.Binary;
using System.IO;
using LBAAssembler.Scenes;

namespace LBAAssembler.Terrain;

// Sendell's Well: island 1 of the engine's island list ("sendell"), an outside island the game was to have and that was cut -- no
// SENDELL.ILE ships and no scene has island 1, though the holomap still names it and knows where on the planet it is. The game takes a
// new one (2026-10-01, ScriptRoundTrip's `sendell build`), and the race track window builds a track on it (RaceTrackIsland.Sendell): the
// build makes the island first, every time, as it makes the rest of the folder from the originals --
//   - SENDELL.ILE: one cube, at (7, 7) of its map, made from the fine-weather Citadel Island's (CITABAU: its texture atlas and palette, so
//     the grass, rock and paving are its own): the cube of the Dome of the Slate -- a round rock in the sea west of the island -- reshaped
//     into a round island with a beach, grassy slopes, a paved rim round a stone well, and water at the bottom of the well. SENDELL.OBL is
//     a copy of CITABAU.OBL (no decor uses it; the build's own bodies go on its end).
//   - RESS.HQR: island 1's sky (entry 12, empty in the retail file: the engine would load nothing) and palette (28, a copy of the Desert
//     island's) become the fine-weather Citadel's, whose atlas the island is drawn with.
//   - HOLOMAP.HQR: island 1's picture and camera (20 and 21, empty in the retail file: zoomed in, the holomap showed nothing): the
//     island drawn through Francos Island's camera (also one cube at (7, 7)) over the sea of the fine-weather Citadel's picture.
//   - SCENE.HQR: a scene for it (Scene), a copy of scene 44's (the Dome of the Slate's) with island 1, the new cube and nobody but Twinsen
//     (and the engine's Zoe placeholder), on the paving south of the well; and its place on the holomap.
// The build's backups don't cover SENDELL.ILE and SENDELL.OBL (there are no originals): RaceTrackService makes them afresh and deletes
// them when the folder is put back or built without the island (RaceTrackIsland.Created).
internal static class SendellWell
{
    public const string IleFile = "SENDELL.ILE", OblFile = "SENDELL.OBL";
    public const int IslandByte = 1, CubeX = 7, CubeZ = 7;
    // the scene: past the retail game's 0..221, the race track story's holomap arrow (222) and the lava lake's scene (223)
    public const int Scene = 224;
    private const string SourceIle = "CITABAU.ILE", SourceObl = "CITABAU.OBL";
    private const int SourceCubeX = 6, SourceCubeZ = 8, SourceScene = 44;
    public const int SkyEntry = 12, PaletteEntry = 28;            // RESS.HQR: RESS_SKYSEA1 and RESS_XPL1, island 1's own
    private const int CitabauSky = 26, CitabauPalette = 42;       // RESS_SKYSEA00 and RESS_XPL00, the fine-weather Citadel's
    // HOLOMAP.HQR entry 12, the positions: record 1 is island 1's label on the globe ("Well of Sendell"), whose place on the planet a
    // scene of the island takes (a scene's record: where on the island, then where on the planet)
    private const int PositionsEntry = 12, PositionSize = 32, SendellLabel = 1;

    // the atlas tiles (32 x 32 pixels, CITABAU's ground atlas), one to a cell
    private static readonly (int X, int Y) Sand = (0, 32), Grass = (32, 96), Flowers = (192, 0), Rock = (128, 32), Cliff = (160, 32),
        Cobbles = (0, 0), Bricks = (128, 0), Water = (32, 0);

    // The shape, in cells from the cube's middle: the coast (with a little wave to it), the beach, the slope up to the rim, the well.
    public const double Coast = 26, BeachTop = 22.5, Plateau = 11, RimOut = 6.5, RimIn = 4.5, WellBottom = 3.5;
    public const int BeachHeight = 350, PlateauHeight = 1900, RimHeight = 2250, BottomHeight = 120;
    private const int Middle = 32;

    private static double Wobble(double angle) => 1 + 0.07 * Math.Sin(3 * angle + 0.4) + 0.045 * Math.Sin(5 * angle + 1.9) + 0.03 * Math.Sin(8 * angle);

    private static double Smooth(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }

    // The ground's height at a vertex, cells from the middle (the coast and the slope wobble; the rim and the well are round).
    public static double HeightAt(double dx, double dz)
    {
        var r = Math.Sqrt(dx * dx + dz * dz);
        var w = Wobble(Math.Atan2(dz, dx));
        var coast = Coast * w; var beachTop = BeachTop * w;
        if (r >= coast) return 0;
        if (r >= beachTop) return BeachHeight * Smooth((coast - r) / (coast - beachTop));
        if (r >= Plateau) return BeachHeight + (PlateauHeight - BeachHeight) * Smooth((beachTop - r) / (beachTop - Plateau));
        if (r >= RimOut) return PlateauHeight;
        if (r >= RimIn) return RimHeight;
        if (r >= WellBottom) return BottomHeight + (RimHeight - BottomHeight) * Smooth((r - WellBottom) / (RimIn - WellBottom));
        return BottomHeight;
    }

    // What a cell is painted with, from where its middle is and how steep it is.
    private static ((int X, int Y) Tile, int CodeJeu) Paint(double dx, double dz, double slopeDegrees)
    {
        var r = Math.Sqrt(dx * dx + dz * dz);
        var w = Wobble(Math.Atan2(dz, dx));
        if (r < WellBottom) return (Water, 1);
        if (r < RimIn) return (Bricks, 0);
        if (r < RimOut) return (Cobbles, 0);
        if (r < RimOut + 1.5) return (Flowers, 0);
        if (r >= BeachTop * w - 0.5) return (Sand, 0);
        if (slopeDegrees > 38) return (Cliff, 0);
        if (slopeDegrees > 26) return (Rock, 0);
        return (Grass, 0);
    }

    // A region of whole cells (their top-left vertices, weight 1).
    private sealed class CellsRegion(IEnumerable<(int Gx, int Gz)> cells) : IslandRegion
    {
        private readonly List<(int Gx, int Gz)> list = cells.ToList();
        public override IEnumerable<(int Gx, int Gz, double Weight)> Vertices(IslandFile island) => list.Select(c => (c.Gx, c.Gz, 1.0));
        public override (double Gx, double Gz) Center => list.Count == 0 ? (0, 0) : (list.Average(c => c.Gx), list.Average(c => c.Gz));
    }

    // The original of one of the folder's files: the copy a race track build kept, else the file.
    private static string Original(string gameDirectory, string file)
    {
        var path = Path.Combine(gameDirectory, file);
        return File.Exists(path + RaceTrackService.BackupSuffix) ? path + RaceTrackService.BackupSuffix : path;
    }

    // The island from CITABAU.ILE (the file at `citabauIle`).
    public static IslandFile MakeIsland(string citabauIle, List<string>? log = null)
    {
        var island = IslandFile.Load(citabauIle);
        var cube = island.CubeAt(SourceCubeX, SourceCubeZ) ?? throw new InvalidDataException($"{SourceIle} has no cube ({SourceCubeX}, {SourceCubeZ})");
        var flag = island.Map[SourceCubeZ * IslandFile.MapSize + SourceCubeX] & 0x80;
        Array.Clear(island.Map);
        island.Map[CubeZ * IslandFile.MapSize + CubeX] = (byte)(cube.Id | flag);
        cube.Decors.Clear();

        // the ground's heights
        for (var z = 0; z <= 64; z++)
        for (var x = 0; x <= 64; x++)
            cube.Heights[z * IslandCube.Vertices + x] = (short)Math.Round(HeightAt(x - Middle, z - Middle));

        // the cells: land (and the well's water) painted, the sea left as it was
        var gx0 = CubeX * IslandCube.Cells; var gz0 = CubeZ * IslandCube.Cells;
        var land = new List<(int, int)>();
        for (var z = 0; z < 64; z++)
        for (var x = 0; x < 64; x++)
        {
            var hs = new[] { cube.Height(x, z), cube.Height(x + 1, z), cube.Height(x, z + 1), cube.Height(x + 1, z + 1) };
            if (hs.All(h => h == 0)) continue;
            land.Add((gx0 + x, gz0 + z));
        }
        IslandGround.OptimiseDiagonals(island, new CellsRegion(land));
        // the sea: the rest of the cube, as its own corner cell is (the dome's rock reached past the new coast in places)
        var onLand = land.ToHashSet();
        var seaCells = new List<(int, int)>();
        for (var z = 0; z < 64; z++) for (var x = 0; x < 64; x++) if (!onLand.Contains((gx0 + x, gz0 + z))) seaCells.Add((gx0 + x, gz0 + z));
        if (IslandGround.Pick(island, gx0, gz0, 0) is { } sea) IslandGround.Paint(island, new CellsRegion(seaCells), sea, PolygonFields.All);
        var groups = new Dictionary<((int, int) Tile, int CodeJeu), List<(int, int)>>();
        foreach (var (gx, gz) in land)
        {
            var x = gx - gx0; var z = gz - gz0;
            var hs = new[] { cube.Height(x, z), cube.Height(x + 1, z), cube.Height(x, z + 1), cube.Height(x + 1, z + 1) };
            var rise = Math.Max(Math.Abs(hs[0] - hs[3]), Math.Abs(hs[1] - hs[2])) / (512.0 * Math.Sqrt(2));
            var slope = Math.Atan(rise) * 180 / Math.PI;
            var key = Paint(x + 0.5 - Middle, z + 0.5 - Middle, slope);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new();
            list.Add((gx, gz));
        }
        foreach (var ((tile, code), cells) in groups)
        {
            var region = new CellsRegion(cells);
            IslandGround.PaintTile(island, region, tile.Item1, tile.Item2, 32, 32);
            IslandGround.PaintGameCode(island, region, code);
            log?.Add($"{cells.Count,5} cells: tile ({tile.Item1},{tile.Item2}){(code != 0 ? $", game code {code}" : "")}");
        }
        // the light, from the cube's own light direction, with the terrain's shadows (the well's walls shade its water)
        var bake = BakeOptions.For(cube);
        bake.TerrainShadows = true;
        var vertices = new List<(int, int)>();
        for (var z = 0; z <= 64; z++) for (var x = 0; x <= 64; x++) vertices.Add((gx0 + x, gz0 + z));
        IslandBake.Bake(island, new CellsRegion(vertices), bake);
        return island;
    }

    // An HQR entry set: a retail slot with no data of its own (RESS.HQR's island 1 sky, HOLOMAP.HQR's island 1 picture) is given some; one
    // that has data is replaced.
    private static byte[] Set(byte[] hqr, int slot, byte[] data)
    {
        try { return HqrWriter.FillEntry(hqr, slot, HqrWriter.StoredEntry(data)); }
        catch (InvalidDataException) { return HqrWriter.ReplaceEntry(hqr, slot, HqrWriter.StoredEntry(data)); }
    }

    // The island's files into the game folder: SENDELL.ILE and SENDELL.OBL, its sky and palette in RESS.HQR and its holomap picture and
    // camera in HOLOMAP.HQR (those two the folder's own, which a race track build has just put back to the originals). Returns lines for
    // the build's log.
    public static List<string> Install(string gameDirectory)
    {
        var log = new List<string>();
        var island = MakeIsland(Original(gameDirectory, SourceIle));
        island.Save(Path.Combine(gameDirectory, IleFile));
        RaceTrackService.CopyWritable(Original(gameDirectory, SourceObl), Path.Combine(gameDirectory, OblFile));
        log.Add($"{IleFile}: made from {SourceIle}'s cube ({SourceCubeX},{SourceCubeZ}), a round island at ({CubeX},{CubeZ}) round a well; {OblFile} a copy of {SourceObl}");

        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var ress = HqrArchive.Open(ressPath);
        var bytes = File.ReadAllBytes(ressPath);
        bytes = Set(bytes, SkyEntry, ress.Read(CitabauSky));
        bytes = Set(bytes, PaletteEntry, ress.Read(CitabauPalette));
        File.WriteAllBytes(ressPath, bytes);
        log.Add($"RESS.HQR: island 1's sky (entry {SkyEntry}) and palette ({PaletteEntry}) the fine-weather Citadel's ({CitabauSky}, {CitabauPalette})");

        var holoPath = Path.Combine(gameDirectory, RaceTrackHolomap.File);
        var holo = HqrArchive.Open(holoPath);
        var ress0 = HqrArchive.Open(ressPath).Read(0);
        var camera = holo.Read(RaceTrackHolomap.FirstMap + 2 * 9 + 1);
        var seaPicture = HolomapPicture.SeaBackground(holo.Read(RaceTrackHolomap.FirstMap + 2 * 12), ress0, 300, 435, 639, 479);
        var picture = HolomapPicture.Draw(IslandFile.Load(Path.Combine(gameDirectory, IleFile)), IslandMapRenderer.LoadPalette(gameDirectory, "SENDELL"), ress0[..768], camera, seaPicture);
        var holoBytes = File.ReadAllBytes(holoPath);
        holoBytes = Set(holoBytes, RaceTrackHolomap.FirstMap + 2 * IslandByte, picture);
        holoBytes = Set(holoBytes, RaceTrackHolomap.FirstMap + 2 * IslandByte + 1, camera);
        File.WriteAllBytes(holoPath, holoBytes);
        log.Add($"HOLOMAP.HQR: island 1's picture and camera (entries {RaceTrackHolomap.FirstMap + 2 * IslandByte} and {RaceTrackHolomap.FirstMap + 2 * IslandByte + 1}), drawn through Francos Island's camera");
        return log;
    }

    // The island's scene, numbered `number` (Scene for the race track; SCENE.HQR is padded with empty entries up to it), from the original
    // scene 44: island 1, its cube, Twinsen alone on the paving south of the well facing it, no zones or track points; and its place on the
    // holomap (its record of HOLOMAP.HQR entry 12: Twinsen's place on the island, and island 1's on the planet). Returns the scene, as
    // SCENE.HQR now has it, for whatever is to be added to it.
    public static SceneModel AddScene(string gameDirectory, int number = Scene)
    {
        var path = Path.Combine(gameDirectory, "SCENE.HQR");
        var model = SceneSerializer.Parse(SceneGame.Lba2, HqrArchive.Open(Original(gameDirectory, "SCENE.HQR")).Read(SourceScene + 1));
        model.Island = IslandByte; model.CubeX = CubeX; model.CubeY = CubeZ;
        var hero = model.Hero;
        var keepZoe = model.Actors.Count > 1 && model.Actors[1].Entity == 14 && model.Actors[1].X == 0 && model.Actors[1].Z == 0;
        model.Actors.RemoveRange(keepZoe ? 2 : 1, model.Actors.Count - (keepZoe ? 2 : 1));
        model.Zones.Clear(); model.TrackPoints.Clear();
        foreach (var a in model.Actors) { a.Life = new byte[] { 0 }; a.Track = new byte[] { 0 }; }
        hero.X = Middle * 512 + 256; hero.Z = (int)((Middle + RimOut + 2.5) * 512); hero.Y = PlateauHeight; hero.Beta = 2048;
        var record = SceneSerializer.Write(model);
        var hqr = HqrFile.Parse(File.ReadAllBytes(path));
        var entry = number + 1;
        if (entry < hqr.Count && !hqr.IsEmpty(entry))
            throw new InvalidDataException($"The game already has a scene {number}: Sendell's Well's scene is numbered {number}.");
        while (hqr.Count < entry) hqr.Slots.Add(new HqrFile.Slot());
        if (hqr.Count == entry) hqr.Add(record); else hqr.SetStored(entry, record);
        var largest = BinaryPrimitives.ReadInt32LittleEndian(hqr.Read(0));
        if (record.Length > largest)
        {
            var size = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(size, record.Length);
            hqr.SetStored(0, size);
        }
        File.WriteAllBytes(path, hqr.ToBytes());

        var holoPath = Path.Combine(gameDirectory, RaceTrackHolomap.File);
        var positions = HqrArchive.Open(holoPath).Read(PositionsEntry);
        int at = (50 + number) * PositionSize, from = (50 + SourceScene) * PositionSize, label = SendellLabel * PositionSize;
        if (positions.Length < at + PositionSize) throw new InvalidDataException("The holomap's position table is shorter than the game's own.");
        positions.AsSpan(from, PositionSize).CopyTo(positions.AsSpan(at));
        void Put(int field, int value) => BinaryPrimitives.WriteInt32LittleEndian(positions.AsSpan(at + field * 4), value);
        Put(0, CubeX * 32768 + hero.X); Put(1, hero.Y); Put(2, CubeZ * 32768 + hero.Z);
        for (var f = 3; f < 6; f++) Put(f, BinaryPrimitives.ReadInt32LittleEndian(positions.AsSpan(label + f * 4)));
        Put(6, -1);
        positions[at + 31] = IslandByte;
        File.WriteAllBytes(holoPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(holoPath), PositionsEntry, HqrWriter.StoredEntry(positions)));
        return model;
    }

    // Puts the folder back: the island's own files go (there are no originals), and the copy of the island the build's second save of it
    // kept (IslandFile.Save: its .bak, the island before the road). True when there were any.
    public static bool Remove(string gameDirectory)
    {
        var any = false;
        foreach (var f in new[] { IleFile, OblFile, IleFile + ".bak" })
        {
            var path = Path.Combine(gameDirectory, f);
            if (!File.Exists(path)) continue;
            File.Delete(path);
            any = true;
        }
        return any;
    }
}
