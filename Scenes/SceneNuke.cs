using System.IO;
using LBAAssembler.Grids;
using LBAAssembler.Lba1;
using LBAAssembler.Lba1.Runtime;
using LBAAssembler.Terrain;

namespace LBAAssembler.Scenes;

// Build > Nuke: everything in a scene goes, and a flat empty scene is left. Planned first (what goes, and what else it touches), then
// written as one save and one undo step ("Nuke scene N"): the scene's record with, as the scene has them, its grid (LBA1: LBA_GRI.HQR;
// LBA2 interiors: LBA_BKG.HQR) or its island cube (LBA2 outside scenes: the .ILE's records that change).
// A nuke can take several scenes at once, in a chain reaction out from the one in focus (Stages: the order they go off in): every scene
// of a joined map (either game), or every cube of an LBA2 island, levelled to one height.
// An island cube can be several scenes -- the same place at other points of the story (Desert cube 8,9 is scenes 61 and 201) -- and the
// 3D view draws one of them, not always the one the editor names: they all share the levelled ground, so they are all emptied.
//
// What is left, and why:
//   - Twinsen, where he stood, on the new ground: a scene can't be without its hero. On an island, the engine's Zoe stand-in in slot 1
//     too (entity 14 at 0,0: the engine treats that slot specially -- RaceTrackScenes, SendellWell keep it). The scripts of what is kept
//     are emptied (END): they refer to actors and points that are gone, and the game stuck or blanked on such scripts (the race track
//     build, docs/LBA2_DESERT_RACE_TRACK_BUILD.md).
//   - The exits: the zones that lead to other scenes (type 0). Without them the scene would be a trap the game can't leave, and on an
//     island every edge of the cube an invisible wall.
//   - The ground, flat: a grid scene gets one layer of its own most used floor block under every column it had anything in (so it keeps
//     its size); an island scene's cube has its objects removed and its land levelled to its usual height, eased into the neighbouring
//     cubes over a few cells, painted with its most common flat ground, its water and lava drained, and its light baked again.
internal sealed class SceneNuke
{
    private const int FeatherCells = 4;          // an island cube's level eases into its neighbours over this many cells

    // A step of the chain reaction: the scenes that go off together (a scene of a joined map, or an island cube's scenes), Ring steps
    // after the first. CubeX/CubeY: the island cube (-1 for a grid scene), Height: its land's height before (for where it is in the view).
    public sealed record Stage(int Ring, IReadOnlyList<int> Scenes, int CubeX = -1, int CubeY = -1, int Height = 0);

    public SceneGame Game { get; }
    public int Scene { get; }                    // the scene in focus: the chain reaction starts there
    public IReadOnlyList<int> Scenes => changes.Select(c => c.Scene).ToList();
    public List<Stage> Stages { get; } = new();
    public string Where { get; private set; } = "";
    public int Actors { get; private set; }
    public int Zones { get; private set; }
    public int Exits { get; private set; }
    public int TrackPoints { get; private set; }
    public int Columns { get; private set; }     // grid columns that had something in them (now floor)
    public int Decors { get; private set; }      // island objects removed
    public int Level { get; private set; }       // the island's new ground height
    public List<string> Warnings { get; } = new();

    private readonly string directory;
    private readonly List<SceneChange> changes = new();
    private readonly List<HqrEntryStore.Edit> edits = new();

    private SceneNuke(SceneGame game, string directory, int scene)
    {
        Game = game; this.directory = directory; Scene = scene;
    }

    // ---- planning -----------------------------------------------------------------------------------------------------------------

    public static SceneNuke ForLba1(string directory, int scene) => ForLba1(directory, new[] { scene }, null);

    // LBA1 scenes, in the order they go off (the first is the one in focus); `area`: the joined map's name, for the question.
    public static SceneNuke ForLba1(string directory, IReadOnlyList<int> scenes, string? area)
    {
        var nuke = new SceneNuke(SceneGame.Lba1, directory, scenes[0]);
        var store = new SceneStore(SceneGame.Lba1, directory);
        for (var k = 0; k < scenes.Count; k++)
        {
            var scene = scenes[k];
            var grid = store.LoadGrid(scene);
            var library = store.LoadLibrary(scene);
            var floor = PickFloor(grid, library)
                ?? (Lba1BlankScene.PickFloor(grid, library) is { } single ? new Floor(single.Block, 1, 1, 0) : null)
                ?? throw new SceneEditException($"Scene {scene}'s block library has no plain floor block to leave behind.");
            var model = nuke.Empty(store.Load(scene), keepZoe: false);
            var (flat, floored) = FlatGrid(grid, library, floor);
            nuke.Columns += floored.Count;
            StandOnFloor(model.Hero, floored, floor.Layer);
            nuke.changes.Add(new SceneChange(scene, model, flat));
            nuke.Stages.Add(new Stage(k, new[] { scene }));
        }
        nuke.Where = scenes.Count == 1 ? $"LBA1 scene {scenes[0]}" : $"LBA1 {area ?? "joined map"}: scenes {List(scenes)}";
        return nuke;
    }

    // An LBA2 scene: an interior (its grid) or a scene of an island (`islandFile`, the .ILE the scene's island is drawn from: that
    // cube's scenes, all of them).
    public static SceneNuke ForLba2(string directory, int scene, string? islandFile)
    {
        var store = new SceneStore(SceneGame.Lba2, directory);
        if (store.Load(scene).CubeMode == 0) return ForLba2Interiors(directory, new[] { scene }, null);
        return ForLba2Island(directory, islandFile ?? throw new SceneEditException($"Scene {scene} is on an island, and no island file is open for it."),
            scene, wholeIsland: false);
    }

    // LBA2 interiors, in the order they go off (the first is the one in focus); `area`: the joined map's name, for the question.
    public static SceneNuke ForLba2Interiors(string directory, IReadOnlyList<int> scenes, string? area)
    {
        var nuke = new SceneNuke(SceneGame.Lba2, directory, scenes[0]);
        var store = new SceneStore(SceneGame.Lba2, directory);
        var backend = new Lba2GridBackend(directory);
        var grids = new List<int>();
        for (var k = 0; k < scenes.Count; k++)
        {
            var scene = scenes[k];
            var old = store.Load(scene);
            if (old.CubeMode != 0) throw new SceneEditException($"Scene {scene} is on an island, not an interior.");
            var gridId = backend.GridOfScene(scene) ?? throw new SceneEditException($"Scene {scene} has no interior grid.");
            var grid = backend.LoadGrid(gridId);
            var library = backend.LoadLibrary(gridId);
            var floor = PickFloor(grid, library)
                ?? (Lba2BlankScene.PickFloor(grid, library) is { } single ? new Floor(single.Block, 1, 1, single.Layer) : null)
                ?? throw new SceneEditException($"The block library of scene {scene}'s interior has no solid floor block to leave behind.");
            var model = nuke.Empty(old, keepZoe: false);
            var (flat, floored) = FlatGrid(grid, library, floor);
            StandOnFloor(model.Hero, floored, floor.Layer);
            nuke.changes.Add(new SceneChange(scene, model));
            nuke.Stages.Add(new Stage(k, new[] { scene }));
            // (two scenes of the map on one grid: the same flat grid, written once)
            if (grids.Contains(gridId)) continue;
            grids.Add(gridId);
            nuke.Columns += floored.Count;
            nuke.edits.Add(new HqrEntryStore.Edit("LBA_BKG.HQR", backend.GridEntry(gridId), Lba2GridBackend.FromLba1Shape(backend.RawGrid(gridId), flat)));
            var sharing = backend.ScenesOfGrid(gridId).Where(s => !scenes.Contains(s) && IsInterior(store, s)).ToList();
            if (sharing.Count > 0)
                nuke.Warnings.Add($"The interior map of scene {scene} (grid {gridId}) is also scene{(sharing.Count > 1 ? "s" : "")} {List(sharing)}'s: {(sharing.Count > 1 ? "they lose" : "it loses")} its buildings and furniture too.");
        }
        nuke.Where = scenes.Count == 1 ? $"LBA2 interior scene {scenes[0]} (grid {grids[0]})" : $"LBA2 {area ?? "joined map"}: interior scenes {List(scenes)}";
        return nuke;
    }

    private static bool IsInterior(SceneStore store, int scene) => TryLoad(store, scene) is { CubeMode: 0 };

    private static SceneModel? TryLoad(SceneStore store, int scene)
    {
        // (the table runs on past the scenes the game has: a number with no scene, or no readable one, isn't one)
        try { return store.Load(scene); }
        catch (Exception e) when (e is InvalidDataException or IOException or SceneFormatException or ArgumentException or OverflowException or IndexOutOfRangeException) { return null; }
    }

    // Scene numbers for a sentence: "3, 1, 2 and 4", or the first ten and how many more.
    private static string List(IReadOnlyList<int> scenes)
    {
        if (scenes.Count == 1) return scenes[0].ToString();
        if (scenes.Count > 10) return $"{string.Join(", ", scenes.Take(10))} and {scenes.Count - 10} more";
        return $"{string.Join(", ", scenes.Take(scenes.Count - 1))} and {scenes[^1]}";
    }

    // An LBA2 island's scenes: the cube of scene `focus`, or (wholeIsland) every cube of the island that has a scene, with every scene
    // of each such cube (the island's scenes are those with the focus scene's island number: the editor's own list). The cubes go off in
    // rings out from the focus's cube, each ring its neighbours.
    public static SceneNuke ForLba2Island(string directory, string islandFile, int focus, bool wholeIsland)
    {
        var nuke = new SceneNuke(SceneGame.Lba2, directory, focus);
        var store = new SceneStore(SceneGame.Lba2, directory);
        var first = store.Load(focus);
        if (first.CubeMode == 0) throw new SceneEditException($"Scene {focus} is an interior, not a scene of an island.");
        var path = Path.Combine(directory, islandFile);
        var island = IslandFile.Load(path);
        var name = Path.GetFileNameWithoutExtension(islandFile);

        // the scenes, by cube (the focus's first)
        var byCube = new Dictionary<(int X, int Z), List<(int Scene, SceneModel Model)>>();
        for (var s = 0; s < store.SceneCount; s++)
        {
            var model = s == focus ? first : TryLoad(store, s);
            if (model is not { CubeMode: not 0 } || model.Island != first.Island) continue;
            var cube = (model.CubeX, model.CubeY);
            if (!wholeIsland && cube != (first.CubeX, first.CubeY)) continue;
            if (island.CubeAt(cube.CubeX, cube.CubeY) is null) { nuke.Warnings.Add($"Scene {s}'s cube ({cube.CubeX}, {cube.CubeY}) isn't on {islandFile}: left alone."); continue; }
            if (!byCube.TryGetValue(cube, out var list)) byCube[cube] = list = new();
            list.Add((s, model));
        }
        foreach (var list in byCube.Values) list.Sort((a, b) => a.Scene == focus ? -1 : b.Scene == focus ? 1 : a.Scene.CompareTo(b.Scene));

        // the rings: out from the focus's cube through neighbouring cubes (a cube no neighbour reaches: after them, by distance)
        var rings = new Dictionary<(int X, int Z), int> { [(first.CubeX, first.CubeY)] = 0 };
        var queue = new Queue<(int X, int Z)>();
        queue.Enqueue((first.CubeX, first.CubeY));
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var n in new[] { (c.X + 1, c.Z), (c.X - 1, c.Z), (c.X, c.Z + 1), (c.X, c.Z - 1) })
                if (byCube.ContainsKey(n) && !rings.ContainsKey(n)) { rings[n] = rings[c] + 1; queue.Enqueue(n); }
        }
        var reached = rings.Values.Max();
        foreach (var c in byCube.Keys.Where(c => !rings.ContainsKey(c)).OrderBy(c => Math.Max(Math.Abs(c.X - first.CubeX), Math.Abs(c.Z - first.CubeY))))
            rings[c] = ++reached;
        var cubes = byCube.Keys.OrderBy(c => rings[c]).ThenBy(c => c.Z).ThenBy(c => c.X).ToList();
        foreach (var c in cubes)
        {
            var land = island.CubeAt(c.X, c.Z)!.Heights.Where(h => h > 0).ToList();
            nuke.Stages.Add(new Stage(rings[c], byCube[c].Select(m => m.Scene).ToList(), c.X, c.Z, land.Count == 0 ? 0 : (int)land.Average(h => h)));
        }

        var models = cubes.SelectMany(c => byCube[c].Select(m => (m.Scene, Model: nuke.Empty(m.Model, keepZoe: true), Cube: c))).ToList();
        var scenes = models.Select(m => m.Scene).ToList();
        if (wholeIsland)
            nuke.Where = $"all of {name} (LBA2: {cubes.Count} cube{(cubes.Count == 1 ? "" : "s")}, scenes {List(scenes)})";
        else
        {
            var also = scenes.Skip(1).ToList();
            nuke.Where = also.Count == 0 ? $"LBA2 scene {focus} ({name}, cube {first.CubeX},{first.CubeY})" : $"LBA2 scenes {focus} and {string.Join(", ", also)} ({name}, cube {first.CubeX},{first.CubeY})";
            if (also.Count > 0)
                nuke.Warnings.Add($"The cube is also scene{(also.Count > 1 ? "s" : "")} {List(also)} (the same place at another point of the story): emptied too, since {(also.Count > 1 ? "they share" : "it shares")} the ground.");
        }
        foreach (var c in cubes)
        {
            var cube = island.CubeAt(c.X, c.Z)!;
            var elsewhere = island.CellsOf(cube.Id).Where(cell => !byCube.ContainsKey(cell)).ToList();
            if (elsewhere.Count > 0)
                nuke.Warnings.Add($"The island shows cube ({c.X}, {c.Z}) again at {string.Join(", ", elsewhere.Select(e => $"({e.X}, {e.Z})"))}: {(elsewhere.Count > 1 ? "they change" : "it changes")} too.");
        }

        nuke.LevelCubes(island, cubes);
        nuke.PlaceHeroes(island, models);
        nuke.changes.AddRange(models.Select(m => new SceneChange(m.Scene, m.Model)));

        // the island's records that changed, as edits beside the scenes'
        var before = HqrFile.Parse(File.ReadAllBytes(path));
        var after = HqrFile.Parse(island.ToBytes());
        for (var i = 0; i < after.Count; i++)
        {
            var a = i < before.Count && !before.IsEmpty(i) ? before.Read(i) : null;
            var b = after.IsEmpty(i) ? null : after.Read(i);
            if (b is null || a is not null && a.AsSpan().SequenceEqual(b)) continue;
            nuke.edits.Add(new HqrEntryStore.Edit(islandFile, i, b));
        }
        return nuke;
    }

    private List<(int Gx, int Gz)> land = new();

    // The cubes' objects removed and their land levelled to one height (the middle of their land's heights), eased into the cubes around
    // them over FeatherCells, painted with their most common flat ground, drained of water and lava, and baked again.
    private void LevelCubes(IslandFile island, IReadOnlyList<(int X, int Z)> cubes)
    {
        const int N = IslandFile.GridSize + 1;                   // vertices along the island
        const int C = IslandCube.Cells;
        var set = cubes.ToHashSet();
        foreach (var c in cubes) { var cube = island.CubeAt(c.X, c.Z)!; Decors += cube.Decors.Count; cube.Decors.Clear(); }

        // the island's heights (short.MinValue: no cube there, open sea)
        var heights = new short[N * N];
        Array.Fill(heights, short.MinValue);
        foreach (var (cx, cz) in Enumerable.Range(0, IslandFile.MapSize * IslandFile.MapSize).Select(i => (i % IslandFile.MapSize, i / IslandFile.MapSize)))
            if (island.CubeAt(cx, cz) is { } cube)
                for (var z = 0; z <= C; z++)
                for (var x = 0; x <= C; x++)
                    heights[(cz * C + z) * N + cx * C + x] = cube.Height(x, z);
        // the sea: what the open water reaches -- from where there is no cube and from the island's edge, through ground no higher than
        // the sea. (Ground as low inside the land -- a pit, a well, the sewers' way in -- is land: Citadel's sewer hole was left as a
        // dark square on the new plain.)
        var sea = new bool[N * N];
        var queue = new Queue<int>();
        bool Low(int v) => heights[v] <= 0;
        for (var v = 0; v < N * N; v++)
        {
            int gx = v % N, gz = v / N;
            if (heights[v] == short.MinValue || (gx == 0 || gz == 0 || gx == N - 1 || gz == N - 1) && Low(v)) { sea[v] = true; queue.Enqueue(v); }
        }
        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            int gx = v % N, gz = v / N;
            foreach (var (nx, nz) in new[] { (gx + 1, gz), (gx - 1, gz), (gx, gz + 1), (gx, gz - 1) })
            {
                if (nx < 0 || nz < 0 || nx >= N || nz >= N) continue;
                var n = nz * N + nx;
                if (!sea[n] && Low(n)) { sea[n] = true; queue.Enqueue(n); }
            }
        }
        // a vertex of the nuked cubes (one on a cube's edge belongs to the cubes either side)
        bool Nuked(int gx, int gz)
        {
            foreach (var cx in gx % C == 0 && gx > 0 ? new[] { gx / C, gx / C - 1 } : new[] { gx / C })
            foreach (var cz in gz % C == 0 && gz > 0 ? new[] { gz / C, gz / C - 1 } : new[] { gz / C })
                if (set.Contains((cx, cz))) return true;
            return false;
        }
        bool Land(int gx, int gz) => !sea[gz * N + gx];

        // the land: the cells with a corner on land, and the level it is brought to
        var levels = new List<short>();
        foreach (var (cx, cz) in cubes)
        {
            for (var z = 0; z < C; z++)
            for (var x = 0; x < C; x++)
            {
                int gx = cx * C + x, gz = cz * C + z;
                if (Land(gx, gz) || Land(gx + 1, gz) || Land(gx, gz + 1) || Land(gx + 1, gz + 1)) land.Add((gx, gz));
            }
            for (var z = 0; z <= C; z++)
            for (var x = 0; x <= C; x++)
            {
                // (an edge vertex once: by the cube on its left and top unless that cube isn't nuked)
                int gx = cx * C + x, gz = cz * C + z;
                if (x == 0 && set.Contains((cx - 1, cz)) || z == 0 && set.Contains((cx, cz - 1))) continue;
                if (Land(gx, gz) && heights[gz * N + gx] > 0) levels.Add(heights[gz * N + gx]);
            }
        }
        if (land.Count == 0 || levels.Count == 0)
        {
            Warnings.Add(cubes.Count == 1 ? "The cube is all sea: its objects go, the sea stays." : "The cubes are all sea: their objects go, the sea stays.");
            Level = 0;
            return;
        }
        levels.Sort();
        Level = levels[levels.Count / 2];
        var ground = MostCommonFlatGround(island, land);

        // levelled: the cubes' land, then the land around them eased towards it over FeatherCells
        int x0 = cubes.Min(c => c.X) * C - FeatherCells, x1 = (cubes.Max(c => c.X) + 1) * C + FeatherCells;
        int z0 = cubes.Min(c => c.Z) * C - FeatherCells, z1 = (cubes.Max(c => c.Z) + 1) * C + FeatherCells;
        var touched = new List<(int Gx, int Gz)>();
        for (var gz = Math.Max(0, z0); gz <= Math.Min(N - 1, z1); gz++)
        for (var gx = Math.Max(0, x0); gx <= Math.Min(N - 1, x1); gx++)
        {
            var h = heights[gz * N + gx];
            if (h == short.MinValue) continue;
            double weight;
            if (Nuked(gx, gz))
            {
                if (!Land(gx, gz)) continue;
                weight = 1;
            }
            else
            {
                // (beyond them: the land above the sea, by how far it is from the nearest nuked cube)
                if (h <= 0) continue;
                var outside = cubes.Min(c => Math.Max(Math.Max(c.X * C - gx, gx - (c.X + 1) * C), Math.Max(c.Z * C - gz, gz - (c.Z + 1) * C)));
                if (outside > FeatherCells) continue;
                weight = 1 - (double)outside / (FeatherCells + 1);
                weight = weight * weight * (3 - 2 * weight);
            }
            var to = (int)Math.Round(h + (Level - h) * weight);
            if (to != h) island.SetHeight(gx, gz, to);
            touched.Add((gx, gz));
        }
        // the ground: one plain walkable ground everywhere on the land, no water, lava or blocking rock left
        var region = new CellsRegion(land);
        if (ground is not null) IslandGround.Paint(island, region, ground, PolygonFields.Texture);
        IslandGround.PaintGameCode(island, region, 0);
        foreach (var (gx, gz) in land)
        {
            var cube = island.CubeAt(gx / C, gz / C)!;
            for (var half = 0; half < 2; half++)
                cube.SetPolygon(gx % C, gz % C, half, new IslandPolygon(cube.Polygon(gx % C, gz % C, half)).With(col: false).Raw);
        }
        // the light, as each cube's own light falls on flat ground with nothing standing on it
        foreach (var group in touched.GroupBy(v => island.CubeAt(Math.Min(v.Gx / C, IslandFile.MapSize - 1), Math.Min(v.Gz / C, IslandFile.MapSize - 1))))
        {
            if (group.Key is not { } cube) continue;
            var bake = BakeOptions.For(cube);
            bake.TerrainShadows = true;
            IslandBake.Bake(island, new CellsRegion(group), bake);
        }
    }

    // Twinsen on the new ground of his scene's cube (or, where he stood in the sea, in the middle of the cube's land).
    private void PlaceHeroes(IslandFile island, IEnumerable<(int Scene, SceneModel Model, (int X, int Z) Cube)> models)
    {
        const int C = IslandCube.Cells;
        foreach (var (_, model, (cx, cz)) in models)
        {
            var cubeLand = land.Where(c => c.Gx / C == cx && c.Gz / C == cz).ToList();
            if (cubeLand.Count == 0) continue;
            var hero = model.Hero;
            double wx = cx * (double)IslandFile.CubeSize + hero.X, wz = cz * (double)IslandFile.CubeSize + hero.Z;
            if ((IslandOps.Altitude(island, wx, wz) ?? 0) <= 0)
            {
                var (mx, mz) = cubeLand.OrderBy(c => Math.Abs(c.Gx - (cx * C + C / 2)) + Math.Abs(c.Gz - (cz * C + C / 2))).First();
                hero.X = (mx - cx * C) * IslandFile.CellSize + IslandFile.CellSize / 2; hero.Z = (mz - cz * C) * IslandFile.CellSize + IslandFile.CellSize / 2;
                wx = cx * (double)IslandFile.CubeSize + hero.X; wz = cz * (double)IslandFile.CubeSize + hero.Z;
            }
            hero.Y = (int)Math.Round(IslandOps.Altitude(island, wx, wz) ?? Level);
        }
    }

    // The ground most of the land's flat cells wear (a triangle to paint with), or null when there is no flat land to go by.
    private static IslandGround.Sample? MostCommonFlatGround(IslandFile island, List<(int Gx, int Gz)> land)
    {
        var counts = new Dictionary<string, (IslandGround.Sample Sample, int Count)>();
        foreach (var (gx, gz) in land)
        {
            var hs = new[] { island.HeightAt(gx, gz), island.HeightAt(gx + 1, gz), island.HeightAt(gx, gz + 1), island.HeightAt(gx + 1, gz + 1) };
            if (hs.Any(h => h is null) || hs.Max()!.Value - hs.Min()!.Value > 160) continue;      // (flat: under about 17 degrees)
            if (IslandGround.Pick(island, gx, gz, 0) is not { Texture: not null } sample) continue;
            var p = sample.Polygon;
            if (p.CodeJeu != 0 || p.Col || p.TexFlag == 0 && p.PolyFlag == 0) continue;
            var key = $"{p.TexFlag}/{p.PolyFlag}/{p.Bank}/{p.SampleStep}/{string.Join(",", sample.Texture!)}";
            counts[key] = counts.TryGetValue(key, out var c) ? (c.Sample, c.Count + 1) : (sample, 1);
        }
        return counts.Count == 0 ? null : counts.Values.MaxBy(c => c.Count).Sample;
    }

    // The scene with everything gone but the hero (and on an island the Zoe stand-in), scripts emptied, and the exits.
    private SceneModel Empty(SceneModel old, bool keepZoe)
    {
        var scene = old.Clone();
        var keep = keepZoe && scene.Actors.Count > 1 && scene.Actors[1].Entity == 14 && scene.Actors[1].X == 0 && scene.Actors[1].Z == 0 ? 2 : 1;
        Actors += scene.Actors.Count - keep;
        // (all at once: one by one would trip over the scripts that point at each other -- Lba2BlankScene)
        scene.Actors.RemoveRange(keep, scene.Actors.Count - keep);
        foreach (var actor in scene.Actors) { actor.Life = new byte[] { 0 }; actor.Track = new byte[] { 0 }; }
        var exits = scene.Zones.Where(z => z.Type == 0).ToList();
        Zones += scene.Zones.Count - exits.Count;
        Exits += exits.Count;
        scene.Zones.Clear();
        scene.Zones.AddRange(exits);
        TrackPoints += scene.TrackPoints.Count;
        scene.TrackPoints.Clear();
        return scene;
    }

    // A floor block: Dx x 1 x Dz cells, laid at Layer.
    private sealed record Floor(int Block, int Dx, int Dz, int Layer);

    // The floor the scene is walked on most: of the blocks one layer tall, solid in every cell and plain ground (no water or other special
    // code), the one with the most cells that have nothing on them, in the lowest layers -- whatever its size: a room's tiles are often a
    // block of 2 x 2 or more (the Desert bar's are), and the blocks of one cell are then the stones under them, not what is seen.
    private static Floor? PickFloor(byte[] grid, byte[] library)
    {
        var cube = new Lba1Cube(grid, library);
        var counts = new Dictionary<(int Block, int Layer), int>();
        for (var y = 0; y < 4; y++)
        for (var z = 0; z < Lba1Cube.SizeZ; z++)
        for (var x = 0; x < Lba1Cube.SizeX; x++)
        {
            var (block, _) = cube.Cell(x, y, z);
            if (block == 0 || cube.Cell(x, y + 1, z).Block != 0) continue;
            counts[(block, y)] = counts.GetValueOrDefault((block, y)) + 1;
        }
        foreach (var ((block, layer), _) in counts.OrderByDescending(c => c.Value))
        {
            if (GridPaint.Info(library, block) is not { Dy: 1, Dx: > 0 and <= 8, Dz: > 0 and <= 8 } info) continue;
            if (Enumerable.Range(0, cube.ExtentOf(block)).Any(p => cube.ShapeOf(block, p) != 1)) continue;
            var code = cube.GameCodeOf(block);
            if (code != 0xF0 && (code & 0xF0) == 0xF0) continue;          // (0xF1..0xFF: water and other special ground -- WorldCodeBrick)
            return new Floor(block, info.Dx, info.Dz, layer);
        }
        return null;
    }

    // An empty grid with one layer of the floor under every column the old grid had anything in, and the holes those enclose (a pit, a
    // well: a hole in a flat plain is a trap) -- or, when it had nothing, the blank scenes' 32 x 32 in the middle -- its cells numbered as
    // if blocks were laid side by side from the grid's corner. Returns the grid and the floored columns.
    private static (byte[] Grid, List<(int X, int Z)> Floored) FlatGrid(byte[] grid, byte[] library, Floor floor)
    {
        const int N = Lba1Cube.SizeX;
        var cube = new Lba1Cube(grid, library);
        var used = new bool[N * N];
        for (var z = 0; z < N; z++)
        for (var x = 0; x < N; x++)
            for (var y = 0; y < Lba1Cube.SizeY; y++)
                if (cube.Cell(x, y, z).Block != 0) { used[z * N + x] = true; break; }
        // the outside: the empty columns the grid's edge reaches through empty columns; every other column is floor
        var outside = new bool[N * N];
        var queue = new Queue<int>();
        for (var i = 0; i < N; i++)
            foreach (var c in new[] { i, (N - 1) * N + i, i * N, i * N + N - 1 })
                if (!used[c] && !outside[c]) { outside[c] = true; queue.Enqueue(c); }
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            int x = c % N, z = c / N;
            foreach (var (nx, nz) in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
                if (nx >= 0 && nz >= 0 && nx < N && nz < N && !used[nz * N + nx] && !outside[nz * N + nx]) { outside[nz * N + nx] = true; queue.Enqueue(nz * N + nx); }
        }
        var floored = new List<(int X, int Z)>();
        if (used.Any(u => u))
            for (var z = 0; z < N; z++)
            for (var x = 0; x < N; x++)
                if (!outside[z * N + x]) floored.Add((x, z));
        if (floored.Count == 0)
            for (var z = Lba1BlankScene.FloorFirst; z <= Lba1BlankScene.FloorLast; z++)
            for (var x = Lba1BlankScene.FloorFirst; x <= Lba1BlankScene.FloorLast; x++)
                floored.Add((x, z));
        var cells = floored.Select(c => new Lba1GridCell(c.X, floor.Layer, c.Z, floor.Block, c.X % floor.Dx + floor.Dx * (c.Z % floor.Dz))).ToList();
        return (Lba1GridEdit.SetCells(Lba1BlankScene.EmptyGrid(), cells), floored);
    }

    // The hero on the floor: where he stood when there is floor there, else on the nearest floored cell (with floor all round it, when
    // there is such a cell, so that he isn't on the brink). The engines find the cell under a point as (x + 256) / 512 (GRILLE.C,
    // GRILLE_A.CPP): cell N is centred on N * 512, not N * 512 + 256 -- placed by the other rule, Twinsen fell through LBA1 scene 3.
    private static void StandOnFloor(SceneActorModel hero, List<(int X, int Z)> floored, int layer)
    {
        int cx = (hero.X + 256) / 512, cz = (hero.Z + 256) / 512;
        var set = floored.ToHashSet();
        if (!set.Contains((cx, cz)))
        {
            bool Inner((int X, int Z) c) => set.Contains((c.X + 1, c.Z)) && set.Contains((c.X - 1, c.Z)) && set.Contains((c.X, c.Z + 1)) && set.Contains((c.X, c.Z - 1));
            var choices = floored.Where(Inner).ToList();
            var (fx, fz) = (choices.Count > 0 ? choices : floored).MinBy(c => (c.X - cx) * (c.X - cx) + (c.Z - cz) * (c.Z - cz));
            hero.X = fx * 512; hero.Z = fz * 512;
        }
        hero.Y = (layer + 1) * 256;
    }

    // ---- writing ------------------------------------------------------------------------------------------------------------------

    // The nuke in a sentence or two, for the question before it and the status after.
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (Actors > 0) parts.Add($"{Actors} actor{(Actors == 1 ? "" : "s")}");
            if (Zones > 0) parts.Add($"{Zones} zone{(Zones == 1 ? "" : "s")}");
            if (TrackPoints > 0) parts.Add($"{TrackPoints} track point{(TrackPoints == 1 ? "" : "s")}");
            if (Columns > 0) parts.Add("every building, wall and piece of furniture");
            if (Decors > 0) parts.Add($"{Decors} building{(Decors == 1 ? "" : "s")} and object{(Decors == 1 ? "" : "s")}");
            var gone = parts.Count == 0 ? "nothing but the ground" : string.Join(", ", parts.Take(parts.Count - 1)) + (parts.Count > 1 ? " and " : "") + parts[^1];
            return $"{gone}";
        }
    }

    // Writes it: one transaction, one undo step.
    public void Commit()
    {
        var store = new SceneStore(Game, directory);
        var scenes = Scenes;
        var description = scenes.Count == 1 ? $"Nuke scene {Scene}" : scenes.Count <= 4 ? $"Nuke scenes {string.Join(", ", scenes)}" : $"Nuke {scenes.Count} scenes, from scene {Scene}";
        store.SaveMany(changes, allowErrors: false, description: description, extraEdits: edits);
    }

    // Whether this nuke wrote island records (the views of the island are to be read again).
    public bool ChangesIsland => edits.Any(e => e.RelativePath.EndsWith(".ILE", StringComparison.OrdinalIgnoreCase));

    // A region of whole cells, or vertices (their top-left vertex, weight 1).
    private sealed class CellsRegion(IEnumerable<(int Gx, int Gz)> cells) : IslandRegion
    {
        private readonly List<(int Gx, int Gz)> list = cells.ToList();
        public override IEnumerable<(int Gx, int Gz, double Weight)> Vertices(IslandFile island) => list.Select(c => (c.Gx, c.Gz, 1.0));
        public override (double Gx, double Gz) Center => list.Count == 0 ? (0, 0) : (list.Average(c => c.Gx), list.Average(c => c.Gz));
    }
}
