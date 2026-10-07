using System.IO;
using LBAAssembler.LbaScript;
using LBAAssembler.Scenes;

namespace LBAAssembler.Terrain;

// The scene side of a race track (the Desert island's or Citadel Island's): every exterior scene of the island loses its actors except Twinsen and the buggy,
// and the scene the start line lies in starts Twinsen and the buggy on the line.
internal static class RaceTrackScenes
{
    public const int BuggyEntity = 152;
    public const int ZoeEntity = 14;

    // Drivers: the track's drivers that have a car (RaceTrackOptions.Racers, less a time to beat's), each with, for each scene, the index of
    // its copy of the car (the race-track mode drives whichever the player's scene has); StartScene: the scene the start line is in; Grid:
    // the grid spots, pole first, and Pits: the spots in the pit lane the opponents wait on while the player qualifies, each [cube x, cube z,
    // x, y, z, turn] in its cube's world units. Twin: the same for the track of the island's other-weather file, when it has one of its own
    // (the scenes carry both).
    // Mushrooms: the small brown mushrooms along the lap with the power-ups in them, Penguins: a nitro penguin in each scene, and Oil: a few
    // oil slicks in each (RaceTrackOil), out of sight until dropped, each [scene, actor] (RACEMOD.CPP's power-ups).
    public sealed record Result(List<string> Log, int ScenesChanged, int ActorsRemoved, List<(RaceDriver Driver, Dictionary<int, int> Actors)> Drivers, int StartScene,
        List<int[]> Grid, List<int[]> Pits, Result? Twin = null, List<int[]>? Mushrooms = null, List<int[]>? Penguins = null, List<int[]>? Oil = null);

    // The buggy's own script removes it until the quest that mends it is done (game variable 74 >= 3). The compare is
    //   IF VAR_GAME(74) >= 3   =   0C 0F 4A 03 03 00 ..
    // and reads >= 0 with the 3 zeroed, so the buggy is there from the start of any game. True when it was that script.
    internal static bool BuggyAlwaysThere(SceneActorModel buggy)
    {
        if (buggy.Life.Length <= 6 || buggy.Life[0] != 0x0C || buggy.Life[1] != 0x0F || buggy.Life[2] != 0x4A || buggy.Life[3] != 0x03 || buggy.Life[4] != 0x03 || buggy.Life[5] != 0x00) return false;
        buggy.Life[4] = 0x00;
        return true;
    }

    // ---- cube edges ----
    // An island's outside is one scene per cube, and the engine holds the hero at a cube's edge (EXTFUNC: Nxw clamped to the cube, with
    // FlagHeroOut set) unless a cube-change zone (type 0) to the next cube's scene covers the spot, at his height, on that edge
    // (GereZoneChangeCube: the arrival value 512 / 31744 names the edge). The retail zones only cover the edges where the retail paths
    // cross them -- scene 42's south edge has none for 14 cells where there was a cliff -- so wherever the lap crosses an edge the road
    // is checked, both ways, and given a zone of its own where the island's don't reach all of it: an invisible wall otherwise.
    private sealed record EdgeCrossing(int FromScene, int ToScene, int CubeX, int CubeZ, char Side, double Along, double Height);

    // the half width of road a crossing zone covers either side of where the lap's middle crosses (the road and its curbs), and how far
    // below and above the road it reaches
    private const double EdgeHalfCells = 8;
    private const int EdgeBelow = 2048, EdgeAbove = 4096;
    // how far below and above the road's middle an island's own zone must reach to count as covering it: a car on the road can stand a
    // little under the road's middle (the ground between the height points, the banking), and Citadel Island's zone into scene 42 from
    // 48 starts exactly at the street's height, 250 -- the car on the storm track's road there stood at 245 and was held at the edge
    private const int CoverBelow = 512, CoverAbove = 1024;

    // The island's outside scenes by cube, each [cube x, cube z, scene]: for the race-track mode's carried jumps, which change the cube
    // themselves where their flight crosses an edge (RACEMOD.CPP cube_scene=).
    public static List<int[]> CubeScenes(string gameDirectory, RaceTrackIsland island)
    {
        var store = new SceneStore(SceneGame.Lba2, gameDirectory);
        var list = new List<int[]>();
        foreach (var scene in island.Scenes)
        {
            if (!store.SceneExists(scene)) continue;
            try
            {
                var m = store.Load(scene);
                if (m.Island == island.IslandByte && m.CubeMode == 1 && !list.Any(c => c[0] == m.CubeX && c[1] == m.CubeY)) list.Add(new[] { m.CubeX, m.CubeY, scene });
            }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { }
        }
        return list;
    }

    private static List<EdgeCrossing> EdgeCrossings(SceneStore store, RaceTrackReport report, RaceTrackOptions options, int island)
    {
        var result = new List<EdgeCrossing>();
        if (report.LapX.Length < 2 || report.GroundAfter is not { } ground) return result;
        var sceneOf = new Dictionary<(int, int), int>();
        foreach (var scene in options.Island.Scenes)
        {
            if (!store.SceneExists(scene)) continue;
            try
            {
                var m = store.Load(scene);
                if (m.Island == island && m.CubeMode == 1) sceneOf.TryAdd((m.CubeX, m.CubeY), scene);
            }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { }
        }
        var n = report.LapX.Length;
        // (where the lap is a raised road, at the road's own height -- the Emerald Moon's, 5,000 over the base's roof, 7,700 over the rim --
        // and nowhere in a carried jump's flight: the race-track mode changes the cube itself there, RACEMOD.CPP)
        var up = report.LapRaised is { } r && r.Length == n && report.LapY.Length == n ? r : null;
        // (a sprint's route has two ends: its last point doesn't lead back to its first)
        for (var i = 0; i < (report.Open ? n - 1 : n); i++)
        {
            var j = (i + 1) % n;
            double x0 = report.LapX[i], z0 = report.LapZ[i], x1 = report.LapX[j], z1 = report.LapZ[j];
            int ax = (int)Math.Floor(x0 / 64), az = (int)Math.Floor(z0 / 64), bx = (int)Math.Floor(x1 / 64), bz = (int)Math.Floor(z1 / 64);
            if (ax == bx && az == bz) continue;
            if (report.LapArc is { } arc && arc.Length == n && (arc[i] || arc[j])) continue;
            if (!sceneOf.TryGetValue((ax, az), out var from) || !sceneOf.TryGetValue((bx, bz), out var to)) continue;
            double Height(double x, double z, double t) => up is not null && up[i] && up[j] ? report.LapY[i] + (report.LapY[j] - report.LapY[i]) * t : ground(x, z);
            // (where the segment meets the edge: an x edge, a z edge, or -- through a corner -- both, each taken at its own point)
            if (ax != bx)
            {
                var ex = Math.Max(ax, bx) * 64.0; var t = (ex - x0) / (x1 - x0); var z = z0 + (z1 - z0) * t;
                var h = Height(ex, z, t);
                result.Add(new EdgeCrossing(from, to, ax, az, bx > ax ? 'E' : 'W', z - az * 64.0, h));
                result.Add(new EdgeCrossing(to, from, bx, bz, bx > ax ? 'W' : 'E', z - bz * 64.0, h));
            }
            if (az != bz)
            {
                var ez = Math.Max(az, bz) * 64.0; var t = (ez - z0) / (z1 - z0); var x = x0 + (x1 - x0) * t;
                var h = Height(x, ez, t);
                result.Add(new EdgeCrossing(from, to, ax, az, bz > az ? 'S' : 'N', x - ax * 64.0, h));
                result.Add(new EdgeCrossing(to, from, bx, bz, bz > az ? 'N' : 'S', x - bx * 64.0, h));
            }
        }
        return result;
    }

    // Gives `model` a crossing zone for each place the lap leaves its cube that no zone of its own to the next scene covers (the whole
    // road's width, at the road's height). Its box and arrival are the retail edge zones' own: the last cell before the edge, the arrival
    // naming the edge (512 into the next cube's near side, 32768 - 1024 its far side), the other coordinate and the height carried over.
    private static int CoverEdges(SceneModel model, int scene, List<EdgeCrossing> edges, List<string> log)
    {
        var added = 0;
        const int Cube = IslandFile.CubeSize, Cell = 512, Near = 512, Far = Cube - 1024;
        foreach (var e in edges.Where(e => e.FromScene == scene))
        {
            int lo = (int)Math.Round((e.Along - EdgeHalfCells) * Cell), hi = (int)Math.Round((e.Along + EdgeHalfCells) * Cell);
            int y0 = Math.Max(0, (int)Math.Round(e.Height) - EdgeBelow), y1 = (int)Math.Round(e.Height) + EdgeAbove;
            var road = (int)Math.Round(e.Height);
            bool Covers(SceneZoneModel z)
            {
                if (z.Type != 0 || z.Num != e.ToScene || z.Info.Length < 3) return false;
                if (road - CoverBelow < Math.Min(z.Y0, z.Y1) || road + CoverAbove > Math.Max(z.Y0, z.Y1)) return false;
                int a0, a1; bool edge;
                switch (e.Side)
                {
                    case 'S': edge = z.Info[2] == Near && Math.Max(z.Z0, z.Z1) >= Cube - Cell; a0 = Math.Min(z.X0, z.X1); a1 = Math.Max(z.X0, z.X1); break;
                    case 'N': edge = z.Info[2] == Far && Math.Min(z.Z0, z.Z1) <= Cell; a0 = Math.Min(z.X0, z.X1); a1 = Math.Max(z.X0, z.X1); break;
                    case 'E': edge = z.Info[0] == Near && Math.Max(z.X0, z.X1) >= Cube - Cell; a0 = Math.Min(z.Z0, z.Z1); a1 = Math.Max(z.Z0, z.Z1); break;
                    default: edge = z.Info[0] == Far && Math.Min(z.X0, z.X1) <= Cell; a0 = Math.Min(z.Z0, z.Z1); a1 = Math.Max(z.Z0, z.Z1); break;
                }
                return edge && a0 <= lo && a1 >= hi;
            }
            if (model.Zones.Any(Covers)) continue;
            var zone = new SceneZoneModel { Type = 0, Num = e.ToScene, Info = new int[8], Y0 = y0, Y1 = y1 };
            zone.Info[1] = y0; zone.Info[7] = 1;
            switch (e.Side)
            {
                case 'S': zone.X0 = lo; zone.X1 = hi; zone.Z0 = Cube - Cell; zone.Z1 = Cube; zone.Info[0] = lo; zone.Info[2] = Near; break;
                case 'N': zone.X0 = lo; zone.X1 = hi; zone.Z0 = 0; zone.Z1 = Cell; zone.Info[0] = lo; zone.Info[2] = Far; break;
                case 'E': zone.Z0 = lo; zone.Z1 = hi; zone.X0 = Cube - Cell; zone.X1 = Cube; zone.Info[2] = lo; zone.Info[0] = Near; break;
                default: zone.Z0 = lo; zone.Z1 = hi; zone.X0 = 0; zone.X1 = Cell; zone.Info[2] = lo; zone.Info[0] = Far; break;
            }
            SceneOps.AddZone(model, zone);
            added++;
            log.Add($"scene {scene}: the road leaves the cube over its {e.Side} edge where no crossing zone reached it -- one added to scene {e.ToScene} ({EdgeHalfCells * 2:0} cells of edge, height {y0}..{y1})");
        }
        return added;
    }

    // The motorbike Rabbibunny to copy: the first of Citadel Island's outside scenes that has him (SCENE.HQR holds every island's scenes, so
    // the Desert island's track gets him too). Null when none can be read.
    private static SceneActorModel? BikerTemplate(SceneStore store, RaceTrackOptions options, int island, List<string> log)
    {
        foreach (var scene in Enumerable.Range(RaceTrackIsland.Citadel.FirstScene, RaceTrackIsland.Citadel.LastScene - RaceTrackIsland.Citadel.FirstScene + 1))
        {
            try
            {
                if (!store.SceneExists(scene)) continue;
                if (store.Load(scene).Actors.Skip(1).FirstOrDefault(a => a.Entity == BikerEntity) is { } bike) return bike.Clone();
            }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { }
        }
        log.Add("no biker: no outside scene of Citadel Island has the motorbike Rabbibunny");
        return null;
    }

    // The Desert island's own buggy (scene 67), for an island whose scenes have none: the same actor, so its script and flags are the
    // game's own. Null when that scene cannot be read.
    private static SceneActorModel? BuggyTemplate(SceneStore store, List<string> log)
    {
        try
        {
            var buggy = store.Load(BuggyScene).Actors.Skip(1).FirstOrDefault(a => a.Entity == BuggyEntity)?.Clone();
            if (buggy is null) log.Add($"no buggy: scene {BuggyScene} has none");
            else log.Add($"the buggy: a copy of the one in scene {BuggyScene} (this island has none of its own)");
            return buggy;
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException)
        {
            log.Add($"no buggy: scene {BuggyScene} could not be read ({e.Message})");
            return null;
        }
    }

    // The retail track's racer (scene 57, actor 4: "Car with racer", which drives the retail lap by its route points), copied into every
    // outside scene as the opponent the race-track mode drives round the lap. The copies neither collide, fall, check zones nor react to
    // hits (NO_CHOC), and are drawn with the depth buffer like the car (OBJ_ZBUFFER, NO_PRE_CLIP); their scripts are a single END. Each
    // waits 20000 below the ground, except the one on the grid beside the player's car: without the race-track mode (the retail engine)
    // that is all there is, a parked car.
    public const int RacerEntity = 157, RacerScene = 57;
    // how many cells behind the start line Baldino starts (the second row); his line's grid point (a point a cell)
    public const int BaldinoGridBack = 8;
    // The motorbike Rabbibunny: Citadel Island's bike taxi (entity 100: body 0 the bunny on his bike; animation 0 standing astride it, 317
    // riding -- ANIM.HQR 764 and 766), the island's own actor copied into every outside scene as the third opponent. The taxi's own copies go
    // from the outside scenes: he races now (one stood on the road under the bridge). His line's grid point, as Baldino's.
    public const int BikerEntity = 100, BikerIdleAnim = 0, BikerRideAnim = 317;
    public const int BikerGridBack = 12;

    // The grid: spots staggered either side of the road's middle, pole GridFirst cells behind the start line and each GridStep behind the one
    // before, GridSide cells to its side -- so two cars side by side are 2 x GridSide apart across (the cars are 2.6 cells wide: 0.9 cells
    // between them) and a car is 2 x GridStep behind the one on its own side. The race-track mode puts the cars on them in the order the
    // qualifying decides; the build puts Twinsen's buggy on pole, the racer on the second spot and Baldino on the third.
    // (A plan may stand them nearer: RaceTrackOptions.GridStep.)
    public const double GridFirst = 3, DefaultGridStep = 3.5, GridSide = 1.75;
    public const int GridSpots = 6;
    public static (double Back, double Side) GridSpot(int k, double step = DefaultGridStep) => (GridFirst + step * k, k % 2 == 0 ? -GridSide : GridSide);
    // The opponents' actors: no shock animation, not clipped before drawing, drawn against the depth buffer (so the bridge and the hills hide
    // them as they should) and, as every actor the game itself draws that way (Twinsen's buggy: BUGGY.CPP), without a shadow -- the engine
    // draws a depth-buffered actor's shadow over it, a dark patch across the car.
    internal const uint OpponentFlags = 0x1A1000;

    public const int DesertIsland = 2;
    // The buggy's own scene on the Desert island: an island with no buggy of its own (Citadel) gets a copy of that actor on the grid.
    public const int BuggyScene = 67;

    // One of the tracks the island's scenes carry -- its own file's, and (Citadel Island) its other-weather file's when that has a track of
    // its own -- and what the scenes were given for it.
    private sealed class Track(RaceTrackReport report, RaceTrackOptions options)
    {
        public RaceTrackReport Report { get; } = report;
        public RaceTrackOptions Options { get; } = options;
        public (double X, double Z, double Y, double DirX, double DirZ)? Start => Options.StartAtLine && Report.StartLine.Count > 0 ? Report.StartLine[0] : null;
        public SceneActorModel? Racer, BikerTemplate;
        public List<EdgeCrossing> Edges = new();
        // the drivers with a car, and each one's copies of it, scene by scene
        public List<RaceDriver> Drivers = new();
        public List<Dictionary<int, int>> Cars = new();
        public int StartScene = -1;
        public List<int[]> Grid = new(), Pits = new();
        // the spots the opponents' cars wait on in the start line's scene (null elsewhere)
        public List<(int X, int Z, int Beta, int Y)?> CarAt = new();
        // the power-ups: where the mushrooms go along the lap (island cells, the road's height), and the scenes' copies of them and of the penguin
        public List<(double X, double Z, double Y)> MushroomSpots = new();
        public List<int[]> Mushrooms = new(), Penguins = new(), Oil = new();
        public Result Result(List<string> log, int changed, int removed, Result? twin = null) =>
            new(log, changed, removed, Drivers.Select((d, k) => (d, Cars[k])).ToList(), StartScene, Grid, Pits, twin, Mushrooms, Penguins, Oil);
    }

    // Edits the outside scenes of the island as the options say, from what the build of the island found (start line, jump, road). With
    // `twin`, the track of the island's other-weather file when it has one of its own (Citadel Island's town circuit): the scenes are the
    // same for both files, so they are given both -- each track's start (the buggy and Twinsen in its own start scene), jump, cube-edge
    // crossings and opponents, and the zones on either road go. What the twin's track was given is the result's Twin.
    public static Result Apply(string gameDirectory, RaceTrackReport report, RaceTrackOptions options, (RaceTrackReport Report, RaceTrackOptions Options)? twin = null)
    {
        var island = options.Island.IslandByte;
        var demoFrom = 190;
        var tracks = new List<Track> { new(report, options) };
        if (twin is { } other) tracks.Add(new(other.Report, other.Options));
        var roadReach = 6.5;
        // (with two tracks: the nearer road, and the ground its file has there -- the scenes' actors stand in both)
        Func<double, double, double> distanceToRoad = tracks.Count == 1 ? report.DistanceToRoad : (x, z) => tracks.Min(t => t.Report.DistanceToRoad(x, z));
        Func<double, double, double>? groundAfter = tracks.Count == 1 ? report.GroundAfter
            : report.GroundAfter is null ? null : (x, z) => (tracks.MinBy(t => t.Report.DistanceToRoad(x, z))!.Report.GroundAfter ?? report.GroundAfter)(x, z);
        var store = new SceneStore(SceneGame.Lba2, gameDirectory);
        // the actors copied from other islands' scenes (the racer, the biker, the buggy) are taken from the original scenes: another island's
        // track built before this one in the same build has changed its own (RaceTrackService.Build)
        var originals = File.Exists(store.ScenePath + RaceTrackService.BackupSuffix) ? new SceneStore(SceneGame.Lba2, gameDirectory, "SCENE.HQR" + RaceTrackService.BackupSuffix) : store;
        var log = new List<string>();
        var changes = new List<SceneChange>();
        var removed = 0; var zonesRemoved = 0; var camerasRemoved = 0; var mushroomsLeftOut = 0;
        foreach (var t in tracks)
        {
            t.Drivers = t.Options.Racers().Where(d => !d.Ghost).ToList();
            t.Cars = t.Drivers.Select(_ => new Dictionary<int, int>()).ToList();
            if (t.Drivers.Any(d => d.Bike)) t.BikerTemplate = BikerTemplate(originals, t.Options, island, log);
            if (t.Drivers.Any(d => !d.Bike))
                try { t.Racer = originals.Load(RacerScene).Actors.Skip(1).FirstOrDefault(a => a.Entity == RacerEntity)?.Clone(); }
                catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { log.Add($"no opponent: scene {RacerScene} could not be read ({e.Message})"); }
            t.Edges = EdgeCrossings(store, t.Report, t.Options, island);
            t.MushroomSpots = MushroomSpots(t.Report, t.Options.RaisedHalfWidth);
        }
        // the mushrooms' and the penguin's actors to copy: the game's own (Citadel Island's mushroom by the weather wizard's tent, the
        // nitro penguin in its shop)
        SceneActorModel? Template(int scene, int actor)
        {
            try { return originals.Load(scene).Actors.ElementAtOrDefault(actor)?.Clone(); }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { return null; }
        }
        var mushroomTemplate = Template(MushroomScene, MushroomActor);
        var penguinTemplate = Template(PenguinScene, PenguinActor);
        if (mushroomTemplate is null || penguinTemplate is null) log.Add($"no power-ups: the game's mushroom (scene {MushroomScene}) or nitro penguin (scene {PenguinScene}) could not be read");
        var biking = tracks.Any(t => t.BikerTemplate is not null);
        var edgeZonesAdded = 0; var doorsKept = 0;
        // Whether any track's road surface (asphalt and curbs; a raised road's deck at its own height) covers part of a zone's box at the
        // zone's height -- a car there stands in it. (A jump's gap has no surface.)
        bool Paved(SceneZoneModel zone, int cubeX, int cubeZ)
        {
            double x0 = cubeX * 64 + zone.X0 / 512.0, x1 = cubeX * 64 + zone.X1 / 512.0, z0 = cubeZ * 64 + zone.Z0 / 512.0, z1 = cubeZ * 64 + zone.Z1 / 512.0;
            foreach (var t in tracks)
                foreach (var r in t.Report.Roads)
                    for (var i = 0; i < r.Count; i++)
                    {
                        if (r.Void.Length > i && r.Void[i]) continue;
                        var dx = Math.Max(Math.Max(x0 - r.X[i], 0), r.X[i] - x1); var dz = Math.Max(Math.Max(z0 - r.Z[i], 0), r.Z[i] - z1);
                        if (dx * dx + dz * dz > r.CurbHalf * r.CurbHalf) continue;
                        if (r.H[i] >= zone.Y0 - 256 && r.H[i] <= zone.Y1) return true;
                    }
            return false;
        }
        // the island's own outside scenes: a cube change to any other scene is a door (Mosquibees Island's inside scene 104, the Queen's
        // throne, is numbered between its outside ones)
        var outside = new HashSet<int>();
        foreach (var scene in options.Island.Scenes)
        {
            if (!store.SceneExists(scene)) continue;
            try { var m = store.Load(scene); if (m.Island == island && m.CubeMode == 1) outside.Add(scene); }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { }
        }
        for (var scene = 0; scene < store.SceneCount; scene++)
        {
            if (!store.SceneExists(scene)) continue;
            SceneModel model;
            try { model = store.Load(scene); } catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { continue; }
            if (model.Island != island || model.CubeMode != 1) continue;
            if (!options.Island.HasScene(scene))
            {
                // the demo scenes are copies the game plays as films; they are left as they are
                log.Add($"scene {scene}: {(scene >= demoFrom ? "demo scene" : "not one of the island's own outside scenes")}, left alone");
                continue;
            }
            var count = 0;
            if (options.RemoveActors)
            {
                // slot 1 of every scene is the engine's own placeholder for Zoe (entity 14, no body, parked at 0,0,0); the engine treats that slot
                // specially -- a buggy that took its place came up with no life -- so it stays
                var needed = CutsceneActors(model);
                // (and the people the story needs, where the game has them: Celebration Island's souvenir seller stands on its lava lake's
                // rim, before the statue rises -- the statue's track, in the other file, is not his to make room for)
                var storyKept = new HashSet<int>();
                if (options.Story && options.Island.StoryEntities is { } story)
                    for (var i = 1; i < model.Actors.Count; i++) if (story.Contains(model.Actors[i].Entity)) storyKept.Add(i);
                var doomed = Enumerable.Range(1, model.Actors.Count - 1)
                    .Where(i => model.Actors[i].Entity != BuggyEntity && !(i == 1 && model.Actors[i].Entity == ZoeEntity && model.Actors[i].X == 0 && model.Actors[i].Z == 0))
                    .Where(i => !needed.Contains(i) && !storyKept.Contains(i) || biking && model.Actors[i].Entity == BikerEntity)
                    .ToList();
                if (biking && needed.Any(i => i < model.Actors.Count && model.Actors[i].Entity == BikerEntity))
                    log.Add($"scene {scene}: the bike taxi goes (he races now), though Twinsen's script rides with him in a cutscene");
                needed.RemoveWhere(i => biking && i < model.Actors.Count && model.Actors[i].Entity == BikerEntity);
                if (needed.Count > 0) log.Add($"scene {scene}: actors {string.Join(", ", needed.Order())} kept -- Twinsen's own script waits on them in its travel cutscenes (the ferry, the Dino-Fly), which would never end without them");
                if (storyKept.Count > 0) log.Add($"scene {scene}: actors {string.Join(", ", storyKept.Order())} kept where they are -- the story needs them");
                foreach (var i in needed.Order())
                    if (i < model.Actors.Count) ShiftOffRoad(model.Actors[i], model, i, distanceToRoad, report.GroundBefore, groundAfter, report.WasGround, roadReach, scene, log);
                var standIn = AddStandIn(model, doomed);
                foreach (var i in doomed.OrderByDescending(i => i))
                {
                    SceneOps.DeleteActor(model, i, retarget: standIn is { } stand ? stand - count : 0);
                    count++;
                }
                if (standIn is { } si)
                {
                    TidyStandIn(model, si - count);
                    log.Add($"scene {scene}: the scripts that stay referred to removed actors; those references now go to a stand-in (actor {si - count})");
                }
            }
            removed += count;
            if (report.GroundBefore is { } groundBefore && groundAfter is not null && report.WasGround is { } wasGround)
            {
                // (the own file's story places: on its ground -- Citadel Island's lighthouse door, scene 46)
                var own = options.Island.OwnGroundAt?.Where(o => o.Scene == scene).ToList();
                var after = own is { Count: > 0 } && report.GroundAfter is { } ownGround
                    ? (x, z) => own.Any(o => (x - o.X) * (x - o.X) + (z - o.Z) * (z - o.Z) <= o.Reach * o.Reach) ? ownGround(x, z) : groundAfter(x, z)
                    : groundAfter;
                Reseat(model, groundBefore, after, wasGround, scene, log);
            }
            // The buggy's own script removes it until the quest that mends it is done (game variable 74 >= 3). The compare is
            //   IF VAR_GAME(74) >= 3   =   0C 0F 4A 03 03 00 ..
            // and reads >= 0 with the 3 zeroed, so the buggy is there from the start of any game.
            if (options.BuggyAlways)
                foreach (var actor in model.Actors.Skip(1).Where(a => a.Entity == BuggyEntity))
                    if (BuggyAlwaysThere(actor)) log.Add($"scene {model.CubeX},{model.CubeY}: the buggy no longer waits for the car quest");
            log.Add($"scene {scene} (cube {model.CubeX},{model.CubeY}): {count} actors removed, {model.Actors.Count - 1} left");

            foreach (var t in tracks)
            {
                if (t.Start is not { } s) continue;
                var wx = s.X * 512; var wz = s.Z * 512;
                var cx = (int)Math.Floor(wx / IslandFile.CubeSize); var cz = (int)Math.Floor(wz / IslandFile.CubeSize);
                if (cx != model.CubeX || cz != model.CubeY) continue;
                var lx = (int)Math.Round(wx - cx * (double)IslandFile.CubeSize); var lz = (int)Math.Round(wz - cz * (double)IslandFile.CubeSize);
                var beta = (int)Math.Round(Math.Atan2(s.DirX, s.DirZ) / (2 * Math.PI) * 4096); beta = ((beta % 4096) + 4096) % 4096;
                var y = (int)Math.Round(s.Y);
                // the buggy stands a few cells before the line, Twinsen beside it; each on the ground at its own spot (the road climbs there).
                // Straight back from the line, or, where the lap bends behind it (the lava lake's line is just out of a corner), on the lap.
                var curved = Behind(t.Report, s, GridSpot(GridSpots - 1, t.Options.GridStep).Back + 2, 0) is { } far && Math.Abs((far.X - s.X) * -s.DirZ + (far.Z - s.Z) * s.DirX) > 0.25;
                int At(double back, double side, bool z)
                {
                    if (curved && Behind(t.Report, s, back, side) is { } on) return (int)Math.Round(((z ? on.Z : on.X) - (z ? cz : cx) * 64.0) * 512);
                    return (int)Math.Round((z ? lz : lx) + (z ? (-s.DirZ * back + s.DirX * side) : (-s.DirX * back - s.DirZ * side)) * 512);
                }
                int Facing(double back)
                {
                    if (!curved || Behind(t.Report, s, back, 0) is not { } on) return beta;
                    var b = (int)Math.Round(Math.Atan2(on.DirX, on.DirZ) / (2 * Math.PI) * 4096); return ((b % 4096) + 4096) % 4096;
                }
                // (on a raised road -- a start line in the air -- the road's own surface, at the start line's level)
                int Ground(int x, int z) => t.Report.RaisedFloor?.Invoke(cx * 64 + x / 512.0, cz * 64 + z / 512.0, s.Y) is { } floor ? (int)Math.Round(floor)
                    : t.Report.GroundAfter is { } g ? (int)Math.Round(g(cx * 64 + x / 512.0, cz * 64 + z / 512.0)) : y;
                if (tracks.Any(o => o != t && o.StartScene == scene))
                    log.Add($"scene {scene}: WARNING: both files' tracks start in this scene; the buggy and Twinsen are put at the last one's start line");
                var buggy = model.Actors.Skip(1).FirstOrDefault(a => a.Entity == BuggyEntity);
                // an island with no buggy of its own (Citadel): a copy of the Desert island's own buggy actor, on the grid
                if (buggy is null && BuggyTemplate(originals, log) is { } spare)
                {
                    // (the quest patch above ran before this copy was here)
                    if (options.BuggyAlways) BuggyAlwaysThere(spare);
                    buggy = spare;
                    SceneOps.AddActor(model, buggy);
                }
                var buggyIndex = buggy is null ? -1 : model.Actors.IndexOf(buggy);
                // the grid and the pits, for a track that races opponents (the race-track mode lines the cars up there)
                var racing = t.Drivers.Count > 0 && (t.Racer is not null || t.BikerTemplate is not null);
                // (moved across onto the race lanes, where the plan says: the Emerald Moon's pit lane shares its deck)
                var shift = t.Options.GridShift;
                if (racing)
                    for (var k = 0; k < GridSpots; k++)
                    {
                        var (back, side) = GridSpot(k, t.Options.GridStep);
                        side += shift;
                        int gx = At(back, side, false), gz = At(back, side, true);
                        t.Grid.Add(new[] { cx, cz, gx, Ground(gx, gz), gz, Facing(back) });
                    }
                // the pits: where the opponents' cars wait while the player qualifies (RaceTrackBuilder.PlacePits, in the pit lane
                // beside the start line). The build parks them there; the race-track mode puts them on the grid for the race.
                if (racing)
                    foreach (var p in t.Report.Pits)
                    {
                        var wpx = p.X * 512; var wpz = p.Z * 512;
                        if ((int)Math.Floor(wpx / IslandFile.CubeSize) != cx || (int)Math.Floor(wpz / IslandFile.CubeSize) != cz) continue;
                        int px = (int)Math.Round(wpx - cx * (double)IslandFile.CubeSize), pz = (int)Math.Round(wpz - cz * (double)IslandFile.CubeSize);
                        var pbeta = (int)Math.Round(Math.Atan2(p.DirX, p.DirZ) / (2 * Math.PI) * 4096); pbeta = ((pbeta % 4096) + 4096) % 4096;
                        // (a spot whose height the plan gives stands on a deck of decor, which the ground knows nothing of)
                        t.Pits.Add(new[] { cx, cz, px, t.Report.PitHeights ? (int)Math.Round(p.Y) : Ground(px, pz), pz, pbeta });
                    }
                var pole = GridSpot(0);
                if (buggy is not null) { buggy.X = At(pole.Back, pole.Side + shift, false); buggy.Z = At(pole.Back, pole.Side + shift, true); buggy.Y = Ground(buggy.X, buggy.Z); buggy.Beta = Facing(pole.Back); }
                // Twinsen right behind his car: he comes into the scene facing the way the lap runs (the scene's start keeps no
                // facing), so he faces the car and the action key gets him in. (Beside it, 1.3 cells from its middle, the car's box
                // pushed him off and he faced away from it.) The next car is on the other side, clear of him.
                model.Hero.X = At(pole.Back + 1.8, pole.Side + shift, false); model.Hero.Z = At(pole.Back + 1.8, pole.Side + shift, true); model.Hero.Y = Ground(model.Hero.X, model.Hero.Z) + 100;
                model.Hero.Beta = beta;
                t.StartScene = scene;
                // the opponents wait in the pit lane (the race-track mode puts them on the grid when the race is about to start); with
                // no pit lane they stand on the grid spots behind the player
                var grid = t.Grid; var pits = t.Pits;
                t.CarAt = Enumerable.Range(0, t.Drivers.Count).Select(Waiting).ToList();
                (int X, int Z, int Beta, int Y)? Waiting(int k) =>
                    pits.Count > k ? (pits[k][2], pits[k][4], pits[k][5], pits[k][3])
                    : grid.Count > k + 1 ? (grid[k + 1][2], grid[k + 1][4], beta, grid[k + 1][3]) : null;
                log.Add($"scene {scene}: Twinsen at ({model.Hero.X},{model.Hero.Y},{model.Hero.Z}) turn {beta}, buggy at ({buggy?.X},{buggy?.Y},{buggy?.Z})" +
                        (tracks.Count > 1 ? $" -- the start of {(t == tracks[0] ? options.Island.IleFile : options.Island.TwinIleFile)}'s track" : ""));
                // (an island with a track in each weather: in the other weather the car parks where that file's ground has room for it)
                (int X, int Y, int Z, int Beta, int When)? park = null;
                if (tracks.Count > 1 && (t == tracks[0] ? options.Island.ParkOwn : options.Island.ParkTwin) is { } pk)
                {
                    if ((int)Math.Floor(pk.X / 64) != cx || (int)Math.Floor(pk.Z / 64) != cz)
                        log.Add($"scene {scene}: the car's parking place for the other weather, cell ({pk.X}, {pk.Z}), is not in this scene's cube: left out");
                    else
                    {
                        int px = (int)Math.Round((pk.X - cx * 64) * 512), pz = (int)Math.Round((pk.Z - cz * 64) * 512);
                        var otherTrack = tracks.First(o => o != t);
                        var py = otherTrack.Report.GroundAfter is { } og ? (int)Math.Round(og(pk.X, pk.Z)) : Ground(px, pz);
                        park = (px, py, pz, pk.Beta, pk.When);
                    }
                }
                if (buggyIndex > 0 && StartBuggyScript(model, scene, buggyIndex, log, park) is { } withBuggy) model = withBuggy;
            }
            // zones that would act on a car driving along the road: doors into buildings (cube changes to scenes that are not part of the island's
            // outside), hit, ladder, escalator, grid and rail zones; and, with RemoveTrackCameras, the fixed cameras (type 1) the car would
            // drive into. The cube-edge changes, scenario, giver and message zones stay.
            if (options.RemoveRoadZones || options.RemoveTrackCameras)
                for (var z = model.Zones.Count - 1; z >= 0; z--)
                {
                    var zone = model.Zones[z];
                    var kind = zone.Type;
                    // (a change to another of the island's own outside scenes is the way across a cube edge: the engine holds the hero at the
                    // edge unless such a zone takes him over, so removing one walls the road off -- which is what happened on Citadel Island
                    // while this was the Desert island's scene numbers)
                    var door = kind == 0 && !outside.Contains(zone.Num);
                    // (only cameras that are on from the start: one that starts off is switched on only by a cutscene's script -- the
                    // ferry's arrival, a call of the car -- which then needs it, and the car never meets it)
                    var camera = kind == 1 && options.RemoveTrackCameras && zone.Info.Length > 7 && (zone.Info[7] & 1) != 0;
                    if (!(camera || options.RemoveRoadZones && (door || kind is 3 or 6 or 7 or 8 or 9))) continue;
                    // (tested at the centres of the cells the box covers: half a cell's diagonal more makes it the box itself that counts)
                    var reach = camera ? roadReach + options.CameraMargin + Math.Sqrt(0.5) : roadReach;
                    var ox = model.CubeX * (double)IslandFile.CubeSize; var oz = model.CubeY * (double)IslandFile.CubeSize;
                    var hit = false;
                    for (var cz = (int)Math.Floor((oz + zone.Z0) / 512); cz <= (int)Math.Floor((oz + zone.Z1) / 512) && !hit; cz++)
                    for (var cx = (int)Math.Floor((ox + zone.X0) / 512); cx <= (int)Math.Floor((ox + zone.X1) / 512) && !hit; cx++)
                        if (distanceToRoad(cx + 0.5, cz + 0.5) <= reach) hit = true;
                    if (!hit) continue;
                    // A door into a building stays (2026-10-06: the user wanted every entrance kept whose building is still there). The engine
                    // never takes the car through one (OBJECT.CPP GereZoneChangeCube: from the buggy only into an outside scene), and most
                    // need Twinsen to walk into the building's wall too (Info5 bit 0, ZONE_TEST_BRICK) -- with the building gone it does
                    // nothing. Only a door without that, a hole in the ground (a sewer's grate), goes where a road's surface now covers it
                    // at its height: a walker on the road would drop through it.
                    if (door)
                    {
                        var wall = zone.Info.Length > 5 && (zone.Info[5] & 1) != 0;
                        if (wall || !Paved(zone, model.CubeX, model.CubeY)) { doorsKept++; continue; }
                        log.Add($"scene {scene}: door zone {z} (into scene {zone.Num}, no wall to walk into) lies under the road's surface and is removed");
                        SceneOps.DeleteZone(model, z);
                        zonesRemoved++;
                        continue;
                    }
                    log.Add(camera ? $"scene {scene}: fixed camera zone {z} (number {zone.Num}) reaches the track and is removed"
                                   : $"scene {scene}: zone {z} (type {kind}, number {zone.Num}) lies on the road and is removed");
                    SceneOps.DeleteZone(model, z);
                    if (camera) camerasRemoved++; else zonesRemoved++;
                }
            foreach (var t in tracks)
            {
                // each driver's car: the racer's entity with the driver's body (Baldino's rocket car, RaceTrackBaldinoCar, and the cars made
                // after the characters, RaceTrackCharacterCars), so the racer's animations drive it; or the motorbike Rabbibunny -- the same
                // flags and empty scripts, standing astride his bike until the race-track mode moves him
                for (var k = 0; k < t.Drivers.Count; k++)
                {
                    var d = t.Drivers[k];
                    var template = d.Bike ? t.BikerTemplate : t.Racer;
                    if (template is null) continue;
                    var car = template.Clone();
                    car.Body = d.Bike ? 0 : d.Body;
                    car.Flags = OpponentFlags; car.Move = 0; car.Anim = d.Bike ? BikerIdleAnim : 0; car.Life = new byte[] { 0 }; car.Track = new byte[] { 0 };
                    car.X = IslandFile.CubeSize / 2; car.Z = IslandFile.CubeSize / 2; car.Y = -20000; car.Beta = 0;
                    if (k < t.CarAt.Count && t.CarAt[k] is { } g) { car.X = g.X; car.Z = g.Z; car.Beta = g.Beta; car.Y = g.Y; }
                    t.Cars[k][scene] = SceneOps.AddActor(model, car);
                }
                t.CarAt.Clear();
            }
            // the power-ups: the mushrooms of the lap in this scene's cube, on the road, and a penguin out of sight
            if (mushroomTemplate is not null && penguinTemplate is not null)
                foreach (var t in tracks)
                {
                    if (t.MushroomSpots.Count == 0) continue;
                    foreach (var (x, z, y) in t.MushroomSpots)
                    {
                        if ((int)Math.Floor(x / 64) != model.CubeX || (int)Math.Floor(z / 64) != model.CubeY) continue;
                        // (the scene's actors run out at the engine's hundred: the penguins and the slicks keep their room, and a row
                        // that doesn't fit is shorter)
                        if (model.Actors.Count + PenguinsPerScene + OilPerScene >= SceneValidator.MaxObjects) { mushroomsLeftOut++; continue; }
                        // (out of sight, as the penguins and the slicks are, its height on the road kept with it: the race-track mode stands
                        // it there. A scene can be drawn with another island file than its track's -- Celebration Island's 95 is the statue's
                        // track's, and before the statue rises the game draws it without the raised road, as the editor draws the island's
                        // other file with it; Citadel Island's scenes carry both weathers' tracks -- and a mushroom standing on a road that
                        // isn't there hung in the air)
                        var mushroom = mushroomTemplate.Clone();
                        mushroom.Flags = OpponentFlags; mushroom.Move = 0; mushroom.Life = new byte[] { 0 }; mushroom.Track = new byte[] { 0 };
                        mushroom.X = (int)Math.Round((x - model.CubeX * 64) * 512); mushroom.Z = (int)Math.Round((z - model.CubeY * 64) * 512);
                        mushroom.Y = -20000; mushroom.Beta = 0;
                        t.Mushrooms.Add(new[] { scene, SceneOps.AddActor(model, mushroom), (int)Math.Round(y) });
                    }
                    // (a few: the penguins dropped walk the track, several in one scene at once)
                    for (var k = 0; k < PenguinsPerScene; k++)
                    {
                        var penguin = penguinTemplate.Clone();
                        penguin.Flags = OpponentFlags; penguin.Move = 0; penguin.Life = new byte[] { 0 }; penguin.Track = new byte[] { 0 };
                        penguin.X = IslandFile.CubeSize / 2; penguin.Z = IslandFile.CubeSize / 2; penguin.Y = -20000; penguin.Beta = 0;
                        t.Penguins.Add(new[] { scene, SceneOps.AddActor(model, penguin) });
                    }
                    // the oil slicks: the mushroom's entity with the slick's body
                    for (var k = 0; k < OilPerScene; k++)
                    {
                        var oil = mushroomTemplate.Clone();
                        oil.Body = RaceTrackOil.Generic;
                        oil.Flags = OpponentFlags; oil.Move = 0; oil.Life = new byte[] { 0 }; oil.Track = new byte[] { 0 };
                        oil.X = IslandFile.CubeSize / 2; oil.Z = IslandFile.CubeSize / 2; oil.Y = -20000; oil.Beta = 0;
                        t.Oil.Add(new[] { scene, SceneOps.AddActor(model, oil) });
                    }
                }
            // (an island with a track in each weather: each jump only in its own file's -- the scenes are both's, and Citadel Island's
            // storm jump, at the rampart's height since 2026-10-06, reached the town circuit's bridge over it in the fine weather)
            foreach (var t in tracks)
                foreach (var jump in t.Report.Jumps)
                {
                    int? weather = tracks.Count > 1 && options.Island.OwnWeather is { } own ? (t == tracks[0] ? own : 1 - own) : null;
                    if (model.CubeX == jump.CubeX && model.CubeY == jump.CubeZ && AddJump(model, scene, jump, log, weather) is { } jumped) model = jumped;
                }
            foreach (var t in tracks)
                foreach (var mine in t.Report.Mines)
                    if ((int)Math.Floor(mine.X / 64) == model.CubeX && (int)Math.Floor(mine.Z / 64) == model.CubeY && AddMine(model, scene, mine, t.Report, originals, log) is { } mined) model = mined;
            foreach (var t in tracks) edgeZonesAdded += CoverEdges(model, scene, t.Edges, log);
            changes.Add(new SceneChange(scene, model, null));
        }
        var edges = tracks.Sum(t => t.Edges.Count);
        if (edges > 0) log.Add($"the lap{(tracks.Count > 1 ? "s cross" : " crosses")} {edges / 2} cube edges; {edgeZonesAdded} crossing zones added where the island's own did not cover the road");
        if (changes.Count > 0) store.SaveMany(changes, allowErrors: true);
        log.Add($"{zonesRemoved} zones on the road removed");
        if (doorsKept > 0) log.Add($"{doorsKept} doors into buildings near the road kept (the car is never taken through one; a building's needs its wall)");
        if (options.RemoveTrackCameras) log.Add($"{camerasRemoved} fixed camera zones along the track removed");
        foreach (var t in tracks)
        {
            var whose = tracks.Count > 1 ? $" ({(t == tracks[0] ? options.Island.IleFile : options.Island.TwinIleFile)}'s track)" : "";
            if (t.Mushrooms.Count > 0) log.Add($"the power-ups: {t.Mushrooms.Count} mushrooms in rows across the road (the game's own, scene {MushroomScene}), {PenguinsPerScene} nitro penguins and {OilPerScene} oil slicks in each of {t.Penguins.Count / PenguinsPerScene} scenes{whose}");
            if (mushroomsLeftOut > 0) log.Add($"{mushroomsLeftOut} mushrooms left out: their scenes had no room for more actors");
            for (var k = 0; k < t.Drivers.Count; k++)
                if (t.Cars[k].Count > 0) log.Add($"{t.Drivers[k].Name}: a copy of {(t.Drivers[k].Bike ? "the motorbike Rabbibunny" : $"the car (the racer's body {t.Drivers[k].Body})")} in {t.Cars[k].Count} scenes{whose}");
            if (t.Grid.Count > 0) log.Add($"the grid: {t.Grid.Count} spots, pole {GridFirst} cells behind the start line, each {t.Options.GridStep} behind the last, {GridSide} either side of the middle{whose}");
            if (t.Pits.Count > 0) log.Add($"the pits: {t.Pits.Count} spots in the pit lane, where the opponents wait while the player qualifies{whose}");
        }
        var main = tracks[0];
        return main.Result(log, changes.Count, removed, tracks.Count > 1 ? tracks[1].Result(log, changes.Count, removed) : null);
    }

    // The place `back` cells behind the start line along the lap's centre line (the built one: RaceTrackReport.LapX/LapZ), `side` cells
    // across it as GridSpot's side is, and the lap's heading there (the way it runs). Null without the lap.
    private static (double X, double Z, double DirX, double DirZ)? Behind(RaceTrackReport report, (double X, double Z, double Y, double DirX, double DirZ) line, double back, double side)
    {
        var xs = report.LapX; var zs = report.LapZ; var n = xs.Length;
        if (n < 3) return null;
        var i = Enumerable.Range(0, n).MinBy(k => (xs[k] - line.X) * (xs[k] - line.X) + (zs[k] - line.Z) * (zs[k] - line.Z));
        // (a start line in a pit lane is not on the lap: its grid is straight back along the lane)
        if ((xs[i] - line.X) * (xs[i] - line.X) + (zs[i] - line.Z) * (zs[i] - line.Z) > 1) return null;
        // (the way the lap's points run, against the line's direction)
        var way = (xs[(i + 1) % n] - xs[i]) * line.DirX + (zs[(i + 1) % n] - zs[i]) * line.DirZ >= 0 ? 1 : -1;
        double x = line.X, z = line.Z, left = back;
        var k = i;
        while (true)
        {
            if (report.Open && (k - way < 0 || k - way >= n)) return null;
            var j = ((k - way) % n + n) % n;
            var step = Math.Sqrt((xs[j] - x) * (xs[j] - x) + (zs[j] - z) * (zs[j] - z));
            if (step >= left || step < 1e-9 && left <= 0)
            {
                var f = step < 1e-9 ? 0 : left / step;
                var px = x + (xs[j] - x) * f; var pz = z + (zs[j] - z) * f;
                double dx = xs[k] - xs[j], dz = zs[k] - zs[j]; var dl = Math.Sqrt(dx * dx + dz * dz) + 1e-12; dx /= dl; dz /= dl;
                return (px - dz * side, pz + dx * side, dx, dz);
            }
            left -= step; x = xs[j]; z = zs[j]; k = j;
            if (k == i) return null;
        }
    }

    // The power-ups' mushrooms: the game's small brown mushroom (Citadel Island's by the weather wizard's tent, scene 45 actor 7: entity
    // 112, BODY.HQR 171, which gives a bonus when Twinsen walks into it), copied onto the road in a row across it every MushroomSpacing
    // cells round the lap from MushroomFirst after the start line -- three side by side where the road is wide enough, else two (or one),
    // MushroomGap apart and MushroomEdge in from its edges -- not on or near a jump, a loop or a carried jump; the race-track mode hides a
    // power-up in each (a car takes one from a row: the others it passes give way). And the nitro penguin a car drops: the shop's (scene
    // 14, actor 5: entity 46).
    public const int MushroomScene = 45, MushroomActor = 7, PenguinScene = 14, PenguinActor = 5;
    // (the oil slicks one scene can show at once: RACEMOD.CPP keeps six on the whole lap)
    private const int OilPerScene = 5, PenguinsPerScene = 3;   // (oil 3 until 2026-10-07: the refinery's drips lie in slicks too)
    private const double MushroomSpacing = 40, MushroomFirst = 30, MushroomClear = 14;
    // (the gap is more than the engine's reach for taking one, RACEMOD.CPP RACE_MUSHROOM_REACH: 2 cells; a car down the middle of one takes
    // only that one)
    private const double MushroomGap = 2.4, MushroomEdge = 1.0;

    // How many mushrooms a row across a road this wide (half its width, cells) has, and how far apart.
    internal static (int Count, double Gap) MushroomRow(double half)
    {
        var room = half - MushroomEdge;
        var n = room >= 2.0 ? 3 : room >= 0.9 ? 2 : 1;
        return (n, n > 1 ? Math.Min(MushroomGap, 2 * room / (n - 1)) : 0);
    }

    private static List<(double X, double Z, double Y)> MushroomSpots(RaceTrackReport report, double raisedHalf)
    {
        var spots = new List<(double, double, double)>();
        var xs = report.LapX; var zs = report.LapZ; var ys = report.LapY; var n = xs.Length;
        if (n < 10 || ys.Length != n || report.StartLine.Count == 0) return spots;
        var s = report.StartLine[0];
        var i0 = Enumerable.Range(0, n).MinBy(k => (xs[k] - s.X) * (xs[k] - s.X) + (zs[k] - s.Z) * (zs[k] - s.Z));
        // (the way the lap runs from the line)
        var way = (xs[(i0 + 1) % n] - xs[i0]) * s.DirX + (zs[(i0 + 1) % n] - zs[i0]) * s.DirZ >= 0 ? 1 : -1;
        bool Clear(double x, double z)
        {
            foreach (var j in report.Jumps)
                if (Near(j.StartX, j.StartZ) || Near(j.LandX, j.LandZ) || Near((j.StartX + j.LandX) / 2, (j.StartZ + j.LandZ) / 2)) return false;
            foreach (var l in report.Loops) if (Math.Sqrt((l.X - x) * (l.X - x) + (l.Z - z) * (l.Z - z)) < l.Radius * 2 + MushroomClear) return false;
            return true;
            bool Near(double ax, double az) => (ax - x) * (ax - x) + (az - z) * (az - z) < MushroomClear * MushroomClear;
        }
        double along = 0, next = MushroomFirst;
        // (the road's half width at a point of the lap: the asphalt's, or a raised road's to its rail)
        var road = report.Roads.FirstOrDefault();
        double Half(int i)
        {
            if (road is null || road.Count != n) return road?.AsphaltHalf ?? 3.5;
            if (road.Raised is { } up && up[i]) return road.RaisedHalfs is { } halfs && i < halfs.Length ? halfs[i] : raisedHalf;
            return road.AsphaltHalf;
        }
        for (var step = 1; step < n; step++)
        {
            if (report.Open && (i0 + way * step < 0 || i0 + way * step >= n)) break;
            int a = ((i0 + way * (step - 1)) % n + n) % n, b = ((i0 + way * step) % n + n) % n;
            along += Math.Sqrt((xs[b] - xs[a]) * (xs[b] - xs[a]) + (zs[b] - zs[a]) * (zs[b] - zs[a]));
            if (along < next) continue;
            if (along > report.Length - 20) break;
            next = along + MushroomSpacing;
            if (report.LapArc is { } arc && b < arc.Length && arc[b] || !Clear(xs[b], zs[b])) continue;
            // the row: across the road (the lap's way turned a quarter), its middle on the road's
            double dx = xs[b] - xs[a], dz = zs[b] - zs[a], d = Math.Sqrt(dx * dx + dz * dz) + 1e-9;
            var (count, gap) = MushroomRow(Half(b));
            for (var j = 0; j < count; j++)
            {
                var side = (j - (count - 1) / 2.0) * gap;
                spots.Add((xs[b] - dz / d * side, zs[b] + dx / d * side, ys[b]));
            }
        }
        return spots;
    }

    // A land mine: a copy of the retail Desert island's own (scene 66, the minefield the story arms in chapter 4: entity 16, body 30),
    // where the plan puts it, with its own track point. The retail one beeps when Twinsen comes within 1,250 and goes off half a second
    // later (impact 15, at its point), then is gone; a car is 4 cells past it by then, so this one goes off as a car comes within
    // MineReach, with no beep. Gone for the rest of the scene -- the lap's next cube change brings it back.
    public const int MineScene = 66, MineActor = 7, MineImpact = 15, MineReach = 900;

    private static SceneModel? AddMine(SceneModel model, int scene, (double X, double Z) mine, RaceTrackReport report, SceneStore originals, List<string> log)
    {
        SceneActorModel? template;
        try { template = originals.Load(MineScene).Actors.ElementAtOrDefault(MineActor)?.Clone(); }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { log.Add($"scene {scene}: no land mine: scene {MineScene} could not be read ({e.Message})"); return null; }
        if (template is null) { log.Add($"scene {scene}: no land mine: scene {MineScene} has no actor {MineActor}"); return null; }
        int x = (int)Math.Round((mine.X - model.CubeX * 64) * 512), z = (int)Math.Round((mine.Z - model.CubeY * 64) * 512);
        var y = (int)Math.Round(report.GroundAfter?.Invoke(mine.X, mine.Z) ?? 0);
        var point = SceneOps.AddTrackPoint(model, new SceneTrackPoint(x, y, z));
        template.X = x; template.Y = y; template.Z = z; template.Beta = 0;
        var index = SceneOps.AddActor(model, template);
        var life = $@"void comportement_0()
{{
    pos_point({point});
    set_comportement(comportement_1);
}}

void comportement_1()
{{
    if ({MineReach} > distance(0))
    {{
        impact_point({point}, {MineImpact});
        suicide();
    }}
}}
";
        try
        {
            var scripts = LBAAssembler.LbaScript.SceneScripts.Load(SceneSerializer.Write(model), scene);
            scripts.SetText(index, LBAAssembler.LbaScript.ScriptKind.Life, life);
            scripts.SetText(index, LBAAssembler.LbaScript.ScriptKind.Track, "label(0);\nstop();\n");
            var built = scripts.Build();
            if (!built.Ok) { foreach (var e in built.Errors) log.Add($"scene {scene}: land mine script: {e}"); return null; }
            log.Add($"scene {scene}: a land mine at cell ({mine.X:0.0}, {mine.Z:0.0}), actor {index}, track point {point}");
            return SceneSerializer.Parse(SceneGame.Lba2, built.Record!);
        }
        catch (Exception error) when (error is LBAAssembler.LbaScript.ScriptCompileException or InvalidDataException or ArgumentException)
        {
            log.Add($"scene {scene}: the land mine could not be built: {error.Message}");
            return null;
        }
    }

    // An island whose track races in a scene the game hasn't got (Celebration Island's lava lake, RaceTrackIsland.CopiesScene: the island's
    // one outside scene carries the statue's track) gets it: a copy of the original scene it copies, numbered as the island's FirstScene
    // (the numbers between the game's last scene and it left empty: 222 is the holomap position of the story's arrow), and on the holomap
    // that scene's place (HOLOMAP.HQR entry 12, record 50 + the scene: where "Twinsen is here" goes). An island the build makes (Sendell's
    // Well) gets one of its own (SendellWell.AddScene). Several scenes copied (the old moon's, RaceTrackIsland.CopiesScenes: the Emerald
    // Moon's four outside scenes) have their cube changes to one another pointed at one another's copies. Before Apply, which then edits
    // them as the island's own. Returns a line for the build's log, or null for an island with scenes of its own.
    public static string? AddScene(string gameDirectory, RaceTrackIsland island)
    {
        if (island.Created)
        {
            SendellWell.AddScene(gameDirectory, island.FirstScene);
            return $"scene {island.FirstScene}: {island.Name}'s own, made from scene 44's (island {island.IslandByte}), with its place on the holomap";
        }
        var pairs = island.AddedScenes.ToList();
        if (pairs.Count == 0) return null;
        var path = Path.Combine(gameDirectory, "SCENE.HQR");
        var original = HqrArchive.Open(File.Exists(path + RaceTrackService.BackupSuffix) ? path + RaceTrackService.BackupSuffix : path);
        var hqr = HqrFile.Parse(File.ReadAllBytes(path));
        var holoPath = Path.Combine(gameDirectory, RaceTrackHolomap.File);
        var arrows = HqrArchive.Open(holoPath).Read(12);
        const int size = 32;
        var copyOf = pairs.ToDictionary(p => p.From, p => p.To);
        var retargeted = 0;
        foreach (var (from, to) in pairs)
        {
            var record = original.Read(from + 1);
            if (pairs.Count > 1)
            {
                var model = SceneSerializer.Parse(SceneGame.Lba2, record);
                foreach (var zone in model.Zones.Where(z => z.Type == 0 && copyOf.ContainsKey(z.Num))) { zone.Num = copyOf[zone.Num]; retargeted++; }
                // (and Twinsen's own life script goes: the Emerald Moon's scenes put him in his space suit whenever he comes in -- out of
                // his car, as he drove into the next cube)
                model.Hero.Life = new byte[] { 0 };
                record = SceneSerializer.Write(model);
            }
            var entry = to + 1;
            if (entry < hqr.Count && !hqr.IsEmpty(entry))
                throw new InvalidDataException($"The game already has a scene {to}: {island.Shown} races in a copy of scene {from} numbered {to}.");
            while (hqr.Count < entry) hqr.Slots.Add(new HqrFile.Slot());
            if (hqr.Count == entry) hqr.Add(record); else hqr.SetStored(entry, record);
            int at = (50 + from) * size, mine = (50 + to) * size;
            if (arrows.Length < Math.Max(at, mine) + size) throw new InvalidDataException("The holomap's position table is shorter than the game's own.");
            arrows.AsSpan(at, size).CopyTo(arrows.AsSpan(mine));
        }
        File.WriteAllBytes(path, hqr.ToBytes());
        File.WriteAllBytes(holoPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(holoPath), 12, HqrWriter.StoredEntry(arrows)));
        return pairs.Count == 1
            ? $"scene {island.FirstScene}: a copy of scene {pairs[0].From} for {island.Shown}, with its place on the holomap"
            : $"scenes {pairs[0].To}-{pairs[^1].To}: copies of scenes {pairs[0].From}-{pairs[^1].From} for {island.Shown}, with their places on the holomap ({retargeted} cube changes between them pointed at the copies, Twinsen's own life scripts emptied)";
    }

    // Where the scripts that stay (Twinsen's own life script, mostly) refer to actors about to be removed -- "if Twinsen is near the
    // shopkeeper and presses Action, talk, then send the shopkeeper's track to @45" -- those references need somewhere harmless to go.
    // Pointing them at Twinsen (actor 0) made his own scripts act on him: in scene 67 every press of Action (which is also how he gets
    // into the car) found him at distance 0 from "the shopkeeper", made him speak and jumped his own track script to a foreign
    // offset. The stand-in is an invisible actor with no body and no shadow, 20000 below the ground (the engine's distance() and
    // distance_message() give 32000, "far away", when the heights differ by 1500 or more), whose life and track scripts are a single
    // END (TidyStandIn sends every jump into them to that END). Added at the end of the list; returns its index before the
    // removals, or null when nothing that stays refers to a removed actor.
    private static int? AddStandIn(SceneModel model, List<int> doomed)
    {
        if (doomed.Count == 0) return null;
        var gone = doomed.ToHashSet();
        var referred = doomed.Any(i => SceneOps.ReferencesTo(model, ArgRole.Obj, i).Any(r => !gone.Contains(r.Actor)));
        if (!referred || model.Actors.Count >= SceneValidator.MaxObjects) return null;
        // where it stands across the ground: where the removed actor Twinsen's script turns him towards (set_dir(follow, n) -- the car
        // he calls, the bell he rings, the telescope) stood, so he still faces the right way
        var (sx, sz) = (512, 512);
        using (Opcodes.Use(Opcodes.Lba2))
        {
            var code = Bytecode.DecodeLife(model.Hero.Life, out var failure);
            if (failure is null)
                foreach (var ins in code)
                {
                    var def = Opcodes.Life(ins.Op);
                    if (def?.Form != LifeForm.Dir || ins.A.Length <= def.Args.Length || ins.A[def.Args.Length - 1] != 2) continue;
                    var followed = (int)ins.A[def.Args.Length];
                    if (!gone.Contains(followed)) continue;
                    (sx, sz) = (model.Actors[followed].X, model.Actors[followed].Z);
                    break;
                }
        }
        var standIn = SceneOps.BlankActor(SceneGame.Lba2, sx, -20000, sz, entity: 16);
        standIn.Life = new byte[] { 0 }; standIn.Track = new byte[] { 0 };
        standIn.Flags = InvisibleNoShadow; standIn.Body = -1; standIn.Armor = 51; standIn.LifePoints = -1; standIn.CoulObj = 4;
        return SceneOps.AddActor(model, standIn);
    }

    // INVISIBLE | NO_SHADOW, as the retail invisible helper actors ("Dots") have: a script's BODY_OBJ can give the stand-in a body.
    private const uint InvisibleNoShadow = 0x1200;
    // OBJ_FALLABLE: the actor falls onto the ground below it.
    private const uint Fallable = 0x0800;

    // After the removals: every set_track_obj / set_comportement_obj the kept scripts aim at the stand-in goes to offset 0 (its one
    // END), and a camera told to follow it follows Twinsen instead -- it stands 20000 below the ground, and a cutscene's
    // cam_follow blanked the screen. (Its scripts could be ENDs as long as the removed ones' instead, but the editor can't show
    // such a script as text.)
    private static void TidyStandIn(SceneModel model, int standIn)
    {
        using var scope = Opcodes.Use(Opcodes.Lba2);
        for (var n = 0; n < model.Actors.Count; n++)
        {
            var actor = model.Actors[n];
            if (n == standIn || actor.Life.Length == 0) continue;
            var code = Bytecode.DecodeLife(actor.Life, out var failure);
            if (failure is not null) continue;
            var changed = false;
            foreach (var ins in code)
            {
                if (ins.A.Length < 1 || ins.A[0] != standIn) continue;
                var name = Opcodes.Life(ins.Op)?.Name;
                if (name is "SET_TRACK_OBJ" or "SET_COMPORTEMENT_OBJ" && ins.A.Length > 1 && ins.A[1] != 0) { ins.A[1] = 0; changed = true; }
                else if (name == "CAM_FOLLOW") { ins.A[0] = 0; changed = true; }
            }
            if (changed) actor.Life = Bytecode.EncodeLife(code);
        }
    }

    // The actors a normal game can't do without: the ones Twinsen's own life script waits on (a test of their track's label,
    // l_track_obj(n) == k) or points the camera at (cam_follow(n)) -- the ferry, the Dino-Fly and their helpers, in the cutscenes
    // of arriving on and leaving the island. With them removed, arriving on the island by ferry or Dino-Fly, or leaving it, left
    // the player stuck for good. Not the actors that drive them: in scene 60 that would keep the game ending's whole cast -- the
    // Temple Park guard waits for its hidden director and the bowl players, who play petanque beside the road in normal play --
    // so the ending (played in scene 60) doesn't finish on a race track build; putting the original files back undoes it.
    private static HashSet<int> CutsceneActors(SceneModel model)
    {
        var needed = new HashSet<int>();
        var count = model.Actors.Count;
        using (Opcodes.Use(Opcodes.Lba2))
        {
            var code = Bytecode.DecodeLife(model.Hero.Life, out var failure);
            if (failure is not null) return needed;
            foreach (var ins in code)
            {
                var def = Opcodes.Life(ins.Op);
                if (def is null) continue;
                if (def.Name == "CAM_FOLLOW" && ins.A.Length > 0 && ins.A[0] > 0 && ins.A[0] < count) needed.Add((int)ins.A[0]);
                if (def.Form is LifeForm.Cond or LifeForm.Switch && Opcodes.Cond(ins.Func)?.Name == "L_TRACK_OBJ" && ins.FuncArg > 0 && ins.FuncArg < count
                    && (def.Form == LifeForm.Switch || ins.Test == 0))
                    needed.Add(ins.FuncArg);
            }
        }
        return needed;
    }

    // Track points, actors and Twinsen's start that stood on ground the build reshaped go with the ground (keeping their height
    // above it): left where the ground used to be they end up buried -- scene 57's buggy recovery point (42) was 911 under the new
    // road, and a buggy put there by its script showed only as a shadow. Only what stood on drawn ground, and only actors that
    // fall onto the ground (OBJ_FALLABLE): the sea is height 0 too, and the harbour ferry and its route, which the water
    // bridge's causeway now crosses, were lifted 1300 above the water.
    // A kept actor standing where the road now runs (Mosquibees Island's mountain lap took the second loop straight through a
    // Mosquibee's nest, which then sat on the asphalt) is moved to the nearest place clear of it, on the new ground: the actors kept
    // are the ones Twinsen's own script waits on in a cutscene, so they stay in the scene rather than being deleted with the rest.
    // Searched outwards from where it stood, in its own cube.
    private static void ShiftOffRoad(SceneActorModel actor, SceneModel model, int index, Func<double, double, double> distanceToRoad,
        Func<double, double, double>? groundBefore, Func<double, double, double>? ground, Func<double, double, bool>? wasGround,
        double roadReach, int scene, List<string> log)
    {
        var ox = model.CubeX * 64.0; var oz = model.CubeY * 64.0;
        var clear = roadReach + 2;
        double fromX = ox + actor.X / 512.0, fromZ = oz + actor.Z / 512.0;
        // only what stands on land: the harbour ferry waits on the water and sails a route of its own (Reseat leaves it alone too),
        // while a prop that doesn't fall -- a nest on the hillside -- stays the height above the ground it had
        var stood = groundBefore is null ? 0 : groundBefore(fromX, fromZ);
        if ((actor.Flags & Fallable) == 0 && stood <= 0) return;
        if (wasGround is not null && !wasGround(fromX, fromZ)) return;
        if (distanceToRoad(fromX, fromZ) > clear) return;
        var offset = (actor.Flags & Fallable) != 0 || groundBefore is null ? 0 : actor.Y - stood;
        for (var out_ = 1.0; out_ <= 24; out_ += 0.5)
        for (var turn = 0; turn < 24; turn++)
        {
            var angle = turn * Math.PI / 12;
            int x = (int)Math.Round(actor.X + Math.Cos(angle) * out_ * 512), z = (int)Math.Round(actor.Z + Math.Sin(angle) * out_ * 512);
            if (x < 512 || z < 512 || x > IslandFile.CubeSize - 512 || z > IslandFile.CubeSize - 512) continue;   // stay in its own cube
            if (distanceToRoad(ox + x / 512.0, oz + z / 512.0) <= clear) continue;
            if (wasGround is not null && !wasGround(ox + x / 512.0, oz + z / 512.0)) continue;   // not out onto the sea
            var y = ground is null ? actor.Y : (int)Math.Round(ground(ox + x / 512.0, oz + z / 512.0) + offset);
            log.Add($"scene {scene}: actor {index} (entity {actor.Entity}) stood on the road and is moved {out_:0.#} cells aside, to ({x},{y},{z})");
            actor.X = x; actor.Z = z; actor.Y = y;
            return;
        }
        log.Add($"scene {scene}: WARNING: actor {index} (entity {actor.Entity}) stands on the road and no clear place was found near it");
    }

    private static void Reseat(SceneModel model, Func<double, double, double> before, Func<double, double, double> after, Func<double, double, bool> wasGround, int scene, List<string> log)
    {
        var ox = model.CubeX * 64.0; var oz = model.CubeY * 64.0;
        int Rise(int x, int y, int z)
        {
            var cx = ox + x / 512.0; var cz = oz + z / 512.0;
            if (!wasGround(cx, cz)) return 0;
            var b = before(cx, cz); var a = after(cx, cz);
            return Math.Abs(a - b) > 100 && Math.Abs(y - b) < 400 ? (int)Math.Round(a - b) : 0;
        }
        var moved = 0;
        for (var i = 0; i < model.TrackPoints.Count; i++)
        {
            var p = model.TrackPoints[i]; var d = Rise(p.X, p.Y, p.Z);
            if (d != 0) { model.TrackPoints[i] = p with { Y = p.Y + d }; moved++; }
        }
        foreach (var a in model.Actors)
        {
            if ((a.Flags & Fallable) == 0 && a != model.Hero) continue;
            var d = Rise(a.X, a.Y, a.Z);
            if (d != 0) { a.Y += d; moved++; }
        }
        if (moved > 0) log.Add($"scene {scene}: {moved} track points and actors moved up or down with the reshaped ground");
    }

    // Scene 67, the start: the buggy stands on the start line whenever the scene starts on foot (INIT_BUGGY 2 puts the game's one
    // buggy at the actor's own place; the island's scenes run INIT_BUGGY 0, which only shows it where it already is) -- but not
    // when Twinsen drives back into the scene on the next lap: forcing it then parked a second, solid buggy on the start line
    // for the car to crash into.
    // `park`: in the other weather (game variable 206 is When: RACEMOD.CPP RaceMod_CitadelWeather), the car stands there instead (a track
    // point of its own).
    private static SceneModel? StartBuggyScript(SceneModel model, int scene, int buggy, List<string> log, (int X, int Y, int Z, int Beta, int When)? park = null)
    {
        try
        {
            var parkPoint = -1;
            if (park is { } pk) { parkPoint = model.TrackPoints.Count; model.TrackPoints.Add(new SceneTrackPoint(pk.X, pk.Y, pk.Z)); }
            var scripts = SceneScripts.Load(SceneSerializer.Write(model), scene);
            var text = scripts.GetText(buggy, ScriptKind.Life);
            // (Polar Island's buggy has INIT_BUGGY 1, which makes the car the first time: Polar.PolarScenes)
            var pattern = new System.Text.RegularExpressions.Regex(@"init_buggy\(([01])\);\s*if \(12 == comportement_hero\(\)\)\s*\{\s*set_comportement\(comportement_2\);\s*\}\s*else\s*\{\s*set_comportement\(comportement_1\);\s*\}");
            if (!pattern.IsMatch(text)) { log.Add($"scene {scene}: the buggy's script isn't the expected one; it is left to show the buggy where it is"); return null; }
            var parked = park is { } p ? $"            if ({p.When} == var_game(206))\n            {{\n                pos_point({parkPoint});\n                beta({p.Beta});\n            }}\n" : "";
            text = pattern.Replace(text, "if (12 == comportement_hero())\n        {\n            init_buggy($1);\n            set_comportement(comportement_2);\n        }\n        else\n        {\n" + parked +
                "            init_buggy(2);\n            set_comportement(comportement_1);\n        }", 1);
            scripts.SetText(buggy, ScriptKind.Life, text);
            var built = scripts.Build();
            if (!built.Ok) { foreach (var e in built.Errors) log.Add($"scene {scene}: buggy script: {e}"); return null; }
            log.Add($"scene {scene}: the buggy is put on the start line whenever the scene starts on foot (INIT_BUGGY 2), not when Twinsen drives in" +
                    (park is { } q ? $"; in the other weather (game variable 206 = {q.When}) it parks at ({q.X}, {q.Y}, {q.Z}) turn {q.Beta}, track point {parkPoint}" : ""));
            return SceneSerializer.Parse(SceneGame.Lba2, built.Record!);
        }
        catch (Exception error) when (error is ScriptCompileException or InvalidDataException or ArgumentException or InvalidOperationException)
        {
            log.Add($"scene {scene}: the buggy's start could not be set: {error.Message}");
            return null;
        }
    }

    // The retail "car jump" (scene 62 of the Desert island) is not physics -- the ground is one height map and a falling object does not move
    // sideways -- but a scripted flight: the hero's own life script watches for the buggy (behaviour 12) in a scenario zone while heading
    // roughly the right way, then sets the hero's movement to 12 (MOVE_BUGGY, moved by its animation alone) and starts a track that plays
    // animation 67 (ANIM.HQR 51: about 14 cells forward, up and down), and when the track reaches its last label gives the controls back
    // (movement 13). Here a small actor does what the retail hero script does, so no scene's own hero script has to be edited; the hero's
    // track script only gets the two labels -- each of a lap's jumps its own pair (90 and 91, then 92 and 93), its own zone number and
    // controller, and its own flight.
    // (`weather`: the value game variable 206 has when the jump's file is shown -- RACEMOD.CPP RaceMod_CitadelWeather -- or null for any)
    private static SceneModel? AddJump(SceneModel model, int scene, JumpInfo jump, List<string> log, int? weather = null)
    {
        var ox = jump.CubeX * 64.0; var oz = jump.CubeZ * 64.0;
        var y = (int)Math.Round(jump.Height);
        var template = model.Zones.FirstOrDefault(z => z.Type == 2);
        foreach (var b in jump.Boxes)
        {
            var zone = template?.Clone() ?? new SceneZoneModel { Info = new int[8] };
            zone.Type = 2; zone.Num = jump.Zone; zone.Info = new int[8]; zone.Info[7] = 1;
            zone.X0 = (int)((b.X0 - ox) * 512); zone.X1 = (int)((b.X1 - ox) * 512) - 1;
            zone.Z0 = (int)((b.Z0 - oz) * 512); zone.Z1 = (int)((b.Z1 - oz) * 512) - 1;
            // (a car on the ramp is within a couple of hundred of it; 900 above reached the deck of Citadel Island's town circuit, whose
            // bridge runs the same way over the storm track's jump, 890 higher -- the zones are the scene's in either weather)
            zone.Y0 = y - 300; zone.Y1 = y + 500;
            model.Zones.Add(zone);
        }
        var controller = SceneOps.BlankActor(SceneGame.Lba2, (int)((jump.StartX - ox) * 512), y, (int)((jump.StartZ - oz) * 512), entity: 16);
        controller.Life = new byte[] { 0 }; controller.Track = new byte[] { 0 };
        controller.Flags = InvisibleNoShadow; controller.Body = -1; controller.Armor = 51; controller.LifePoints = -1; controller.CoulObj = 4;
        var index = SceneOps.AddActor(model, controller);

        // the turn window: 640 units (56 degrees) either side of the road's heading
        var lo = ((jump.Beta - 640) % 4096 + 4096) % 4096; var hi = (jump.Beta + 640) % 4096;
        var window = lo < hi ? $"{lo} < beta_obj(0) && {hi} > beta_obj(0)" : $"{lo} < beta_obj(0) || {hi} > beta_obj(0)";
        int startLabel = 90 + 2 * jump.Index, endLabel = startLabel + 1;
        var life = $@"void comportement_0()
{{
    set_comportement(comportement_1);
}}

void comportement_1()
{{
    if (12 == comportement_hero() && {jump.Zone} == zone_obj(0){(weather is { } w ? $" && {w} == var_game(206)" : "")})
    {{
        if ({window})
        {{
            set_dir_obj(0, 12);
            set_track_obj(0, label_{startLabel});
            set_comportement(comportement_2);
        }}
    }}
}}

void comportement_2()
{{
    if ({endLabel} == l_track_obj(0))
    {{
        set_dir_obj(0, 13);
        set_comportement(comportement_1);
    }}
}}
";
        try
        {
            var scripts = LBAAssembler.LbaScript.SceneScripts.Load(SceneSerializer.Write(model), scene);
            var heroTrack = scripts.GetText(0, LBAAssembler.LbaScript.ScriptKind.Track).TrimEnd();
            if (heroTrack.Contains($"label({startLabel})") || heroTrack.Contains($"label({endLabel})")) { log.Add($"scene {scene}: the hero's track script already uses labels {startLabel}/{endLabel}; no jump"); return null; }
            heroTrack += $@"

label({startLabel});
beta({jump.Beta});
anim({jump.Anim});
wait_anim();
anim(0);

label({endLabel});
stop();
";
            scripts.SetText(0, LBAAssembler.LbaScript.ScriptKind.Track, heroTrack);
            scripts.SetText(index, LBAAssembler.LbaScript.ScriptKind.Life, life);
            var built = scripts.Build();
            if (!built.Ok) { foreach (var e in built.Errors) log.Add($"scene {scene}: jump script: {e}"); return null; }
            log.Add($"scene {scene}: jump added -- {jump.Boxes.Count} zone boxes numbered {jump.Zone}, controller actor {index}, hero track labels {startLabel}/{endLabel}, heading turn {jump.Beta}");
            return SceneSerializer.Parse(SceneGame.Lba2, built.Record!);
        }
        catch (Exception error) when (error is LBAAssembler.LbaScript.ScriptCompileException or InvalidDataException or ArgumentException)
        {
            log.Add($"scene {scene}: the jump could not be built: {error.Message}");
            return null;
        }
    }
}
