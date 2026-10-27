using System.IO;
using LBAAssembler.Grids;
using LBAAssembler.Lba1;
using LBAAssembler.Scenes;
using LBAAssembler.Terrain;

namespace LBAAssembler.Maps;

// New maps (File > New map; the user, 2026-10-15: "Do we have the ability to create new maps for both games? Perhaps add a File>New option then
// we can specify the type ile, lba2 inside, lba1"), each made from one of the game's own: its look -- an island's ground and sea textures,
// light and sky; an interior's block library, light and music -- is that one's.
internal static class MapNew
{
    // ---- an LBA2 interior: the base scene's header and block library, 32 x 32 cells of its most used floor, Twinsen alone on it
    // (Lba2BlankScene's), his own scripts emptied (the base scene's would take him elsewhere)
    public static LbaMap Lba2Interior(string gameDirectory, int baseScene, string name)
    {
        var store = new SceneStore(SceneGame.Lba2, gameDirectory);
        var backend = new Lba2GridBackend(gameDirectory);
        var (scene, grid) = Lba2BlankScene.Create(store, backend, baseScene);
        var gridId = backend.GridOfScene(baseScene) ?? throw new SceneEditException($"Scene {baseScene} has no interior grid.");
        scene.Hero.Life = new byte[] { 0 }; scene.Hero.Track = new byte[] { 0 };
        var map = new LbaMap { Kind = MapKind.Lba2Interior, Name = name, IslandByte = scene.Island, Slot = baseScene, Grid = Lba2GridBackend.FromLba1Shape(backend.RawGrid(gridId), grid) };
        map.Scenes.Add(new MapScene(SceneSerializer.Write(scene)));
        // (with the base scene's block library and its bricks: its own, what it can be built of)
        MapBricks.Package(map, gameDirectory);
        return map;
    }

    // ---- an LBA1 interior: the slot's island, music, light and block library, 32 x 32 cells of its most used floor (Lba1BlankScene's); it
    // takes that slot in a game folder (LBA1's scenes can't be added to)
    public static LbaMap Lba1Interior(string lba1Directory, int slot, string name)
    {
        var store = new SceneStore(SceneGame.Lba1, lba1Directory);
        var (scene, grid) = Lba1BlankScene.Create(store, slot);
        var map = new LbaMap { Kind = MapKind.Lba1Interior, Name = name, IslandByte = scene.Island, Slot = slot, Grid = grid, Library = store.LoadLibrary(slot) };
        map.Scenes.Add(new MapScene(SceneSerializer.Write(scene)));
        return map;
    }

    // ---- an LBA2 island: `cubesX` x `cubesZ` cubes in the middle of the island's map, land `landHeight` high with a beach all round
    // sloping down to the sea (SeaMargin cells of sea at its edge, Beach cells of slope), its ground, slopes and sea textured as the base
    // island's most common flat ground, steep ground and sea are, its light baked from the base island's sun. Its objects' file (OBL) is the
    // base island's, so its objects can be placed on it. An outside scene for each cube, from the base island's first one (light, music,
    // ambience), Twinsen alone in the first cube's middle, cube-change zones between the cubes.
    public const int SeaMargin = 4, Beach = 12;
    public static readonly (string File, int Byte)[] GameIslands =
    {
        ("CITADEL", 0), ("CITABAU", 0), ("DESERT", 2), ("EMERAUDE", 3), ("OTRINGAL", 4), ("CELEBRAT", 5), ("CELEBRA2", 5), ("PLATFORM", 6),
        ("MOSQUIBE", 7), ("KNARTAS", 8), ("ILOTCX", 9), ("ASCENCE", 10), ("SOUSCELB", 11),
    };

    // `heightmap`: the ground from a picture instead (IslandHeightmap: resampled to the island's size, `shape` its black and white heights and
    // sea level), textured by slope with the base island's sea, flat and steep ground and lit from its sun as the flat island is.
    public static LbaMap Lba2Island(string gameDirectory, string name, string fileName, string baseIsland, int cubesX, int cubesZ, int landHeight,
        HeightmapPicture? heightmap = null, HeightmapImportOptions? shape = null)
    {
        if (!LbaMap.ValidIslandFile(fileName)) throw new SceneEditException($"\"{fileName}\" can't be an island's file name: one to eight letters or digits.");
        if (cubesX is < 1 or > 6 || cubesZ is < 1 or > 6) throw new SceneEditException("An island is 1 to 6 cubes each way.");
        var islandByte = GameIslands.FirstOrDefault(g => g.File.Equals(baseIsland, StringComparison.OrdinalIgnoreCase)).Byte;
        var source = IslandFile.Load(Path.Combine(gameDirectory, baseIsland + ".ILE"));
        var firstX = (IslandFile.MapSize - cubesX) / 2; var firstZ = (IslandFile.MapSize - cubesZ) / 2;
        var cubes = new List<(int X, int Z)>();
        for (var cz = 0; cz < cubesZ; cz++) for (var cx = 0; cx < cubesX; cx++) cubes.Add((firstX + cx, firstZ + cz));
        var island = IslandFile.Parse(IslandScaler.NewFile(source, cubes), fileName + ".ILE");

        // the cubes' settings (light, fog, sky): the base island's cube with the most land
        var settings = source.Cubes.Values.Where(c => c.HasPolygons).OrderByDescending(c => c.Heights.Count(h => h > 0)).First();
        foreach (var (cx, cz) in cubes)
            if (island.CubeAt(cx, cz) is { } cube) { cube.Info = (int[])settings.Info.Clone(); cube.Info[IslandCube.InfoNbDecors] = 0; }

        // the ground: land, a beach round it down to the sea
        int width = cubesX * IslandCube.Cells, depth = cubesZ * IslandCube.Cells;
        int gx0 = firstX * IslandCube.Cells, gz0 = firstZ * IslandCube.Cells;
        double HeightAt(int x, int z)
        {
            double d = Math.Min(Math.Min(x, z), Math.Min(width - x, depth - z));
            var t = Math.Clamp((d - SeaMargin) / Beach, 0, 1);
            return landHeight * t * t * (3 - 2 * t);
        }
        foreach (var (cx, cz) in cubes)
        {
            var cube = island.CubeAt(cx, cz)!;
            for (var z = 0; z <= IslandCube.Cells; z++)
                for (var x = 0; x <= IslandCube.Cells; x++)
                    cube.Heights[z * IslandCube.Vertices + x] = (short)Math.Round(HeightAt((cx - firstX) * IslandCube.Cells + x, (cz - firstZ) * IslandCube.Cells + z));
        }

        // its textures: the base island's most common sea, flat ground and steep ground
        var (sea, flat, steep) = Samples(source);
        var seaCells = new List<(int, int)>(); var flatCells = new List<(int, int)>(); var steepCells = new List<(int, int)>();
        for (var z = 0; z < depth; z++)
            for (var x = 0; x < width; x++)
            {
                double a = HeightAt(x, z), b = HeightAt(x + 1, z), c = HeightAt(x, z + 1), e = HeightAt(x + 1, z + 1);
                if (Math.Max(Math.Max(a, b), Math.Max(c, e)) <= 0) { seaCells.Add((gx0 + x, gz0 + z)); continue; }
                var slope = Math.Atan(Math.Max(Math.Abs(a - e), Math.Abs(b - c)) / (512 * Math.Sqrt(2))) * 180 / Math.PI;
                (slope > 22 && steep is not null ? steepCells : flatCells).Add((gx0 + x, gz0 + z));
            }
        if (sea is not null) IslandGround.Paint(island, new Cells(seaCells), sea, PolygonFields.Texture | PolygonFields.GameCode);
        if (flat is not null) IslandGround.Paint(island, new Cells(flatCells), flat, PolygonFields.Texture | PolygonFields.GameCode);
        if (steep is not null && steepCells.Count > 0) IslandGround.Paint(island, new Cells(steepCells), steep, PolygonFields.Texture | PolygonFields.GameCode);
        IslandGround.OptimiseDiagonals(island, new Cells(flatCells.Concat(steepCells)));
        // the light, from the cubes' own sun, the beach shading itself
        var bake = BakeOptions.For(island.CubeAt(cubes[0].X, cubes[0].Z)!);
        bake.TerrainShadows = true;
        var vertices = new List<(int, int)>();
        for (var z = 0; z <= depth; z++) for (var x = 0; x <= width; x++) vertices.Add((gx0 + x, gz0 + z));
        IslandBake.Bake(island, new Cells(vertices), bake);
        if (heightmap is not null)
        {
            var options = shape ?? new HeightmapImportOptions();
            options.Fit = HeightmapFit.Resample; options.Textures = HeightmapTextures.AllBySlope; options.Ground = (sea, flat, steep);
            options.FixDiagonals = true; options.Bake = bake; options.ObjectsFollow = false;
            IslandHeightmap.Apply(island, heightmap, options);
        }

        return IslandMap(gameDirectory, name, fileName, island.ToBytes(), File.ReadAllBytes(Path.Combine(gameDirectory, baseIsland + ".OBL")), islandByte);
    }

    // An island map round island files (a new island's, or an ILE opened from outside the game folder): an outside scene for each of its cubes,
    // from the game's first outside scene of the island whose look it has (its light, music, ambience), Twinsen alone in the first cube's
    // middle on the ground, cube-change zones between neighbouring cubes.
    public static LbaMap IslandMap(string gameDirectory, string name, string fileName, byte[] ile, byte[] obl, int islandByte)
    {
        var island = IslandFile.Parse(ile, fileName + ".ILE");
        var cubes = new List<(int X, int Z)>();
        for (var i = 0; i < island.Map.Length; i++)
            if ((island.Map[i] & 0x7F) != 0) cubes.Add((i % IslandFile.MapSize, i / IslandFile.MapSize));
        if (cubes.Count == 0) throw new InvalidDataException($"{fileName}.ILE has no cube.");
        var map = new LbaMap { Kind = MapKind.Lba2Island, Name = name, IslandByte = islandByte, IslandFile = fileName, Ile = ile, Obl = obl };
        int GroundAt(int cx, int cz) => island.CubeAt(cx, cz) is { } c ? Math.Max(0, (int)c.Height(32, 32)) : 0;

        // its scenes: one a cube, from the base island's first outside scene
        var store = new SceneStore(SceneGame.Lba2, gameDirectory);
        SceneModel? template = null;
        for (var n = 0; n < store.SceneCount && template is null; n++)
        {
            if (!store.SceneExists(n)) continue;
            var record = store.LoadRecord(n);
            if (record.Length > 5 && record[0] == islandByte && record[5] == 1) template = SceneSerializer.Parse(SceneGame.Lba2, record);
        }
        template ??= store.Load(Enumerable.Range(0, store.SceneCount).First(n => store.SceneExists(n) && store.LoadRecord(n) is { Length: > 5 } r && r[5] == 1));
        for (var k = 0; k < cubes.Count; k++)
        {
            var (cx, cz) = cubes[k];
            var scene = template.Clone();
            scene.Island = islandByte; scene.CubeX = cx; scene.CubeY = cz;
            // (actor 1 stays: the Meca-penguin's slot, which the engine hides whatever is in -- SceneOps.MecaPenguinSlot)
            var penguin = scene.Actors.Count > 1 && SceneOps.IsMecaPenguinSlot(scene.Actors[1]) ? scene.Actors[1] : SceneOps.MecaPenguinSlot();
            scene.Actors.RemoveRange(1, scene.Actors.Count - 1);
            scene.Actors.Add(penguin);
            scene.Zones.Clear(); scene.TrackPoints.Clear();
            foreach (var a in scene.Actors) { a.Life = new byte[] { 0 }; a.Track = new byte[] { 0 }; }
            scene.Hero.X = IslandFile.CubeSize / 2; scene.Hero.Z = IslandFile.CubeSize / 2; scene.Hero.Beta = 0;
            scene.Hero.Y = GroundAt(cx, cz);
            // (a cube-change zone along each edge shared with another of its cubes: into that one's scene, at the edge it comes in by)
            void Zone(int dx, int dz, int x0, int z0, int x1, int z1, int info0, int info2)
            {
                var next = cubes.IndexOf((cx + dx, cz + dz));
                if (next < 0) return;
                var zone = new SceneZoneModel { Type = 0, Num = LbaMap.LocalScene + next, X0 = x0, Y0 = 0, Z0 = z0, X1 = x1, Y1 = ZoneTop, Z1 = z1, Info = new int[8] };
                zone.Info[0] = info0; zone.Info[2] = info2; zone.Info[7] = 1;      // (ZONE_ON)
                scene.Zones.Add(zone);
            }
            Zone(1, 0, Side - Edge, 0, Side, Side, Near, 0);
            Zone(-1, 0, 0, 0, Edge, Side, Far, 0);
            Zone(0, 1, 0, Side - Edge, Side, Side, 0, Near);
            Zone(0, -1, 0, 0, Side, Edge, 0, Far);
            map.Scenes.Add(new MapScene(SceneSerializer.Write(scene), cx, cz));
        }
        return map;
    }
    // (a cube-change zone, as the race track's scenes have them: the last cell before the edge, arriving a cell in)
    private const int Near = 512, Far = 32768 - 1024, Edge = 512, Side = 32768, ZoneTop = 32000;

    // The base island's most common polygon (its texture and game code) on the sea, on flat ground and on steep ground.
    private static (IslandGround.Sample? Sea, IslandGround.Sample? Flat, IslandGround.Sample? Steep) Samples(IslandFile source) => IslandGround.CommonGround(source);

    private sealed class Cells(IEnumerable<(int Gx, int Gz)> cells) : IslandRegion
    {
        private readonly List<(int Gx, int Gz)> list = cells.ToList();
        public override IEnumerable<(int Gx, int Gz, double Weight)> Vertices(IslandFile island) => list.Select(c => (c.Gx, c.Gz, 1.0));
        public override (double Gx, double Gz) Center => list.Count == 0 ? (0, 0) : (list.Average(c => c.Gx), list.Average(c => c.Gz));
    }
}
