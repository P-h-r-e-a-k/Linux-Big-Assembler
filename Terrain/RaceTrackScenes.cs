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
    // oil slicks in each (RaceTrackOil), out of sight until dropped, each [scene, actor] (RACEMOD.CPP's power-ups); Fakes: a few copies of the
    // mushroom in each, the fake mushrooms a car drops (since 2026-10-08).
    public sealed record Result(List<string> Log, int ScenesChanged, int ActorsRemoved, List<(RaceDriver Driver, Dictionary<int, int> Actors)> Drivers, int StartScene,
        List<int[]> Grid, List<int[]> Pits, Result? Twin = null, List<int[]>? Mushrooms = null, List<int[]>? Penguins = null, List<int[]>? Oil = null,
        List<int[]>? Fakes = null, List<int[]>? Gunners = null, List<int[]>? GasMonsters = null, List<int[]>? Guards = null, List<int[]>? Hunters = null,
        List<int[]>? Flyers = null)
    {
        // the doors one weather's island file has lost and the other's has not, each "<file> <scene> <x0> <z0> <x1> <z1>" (RACEMOD.CPP
        // door_off=): the scenes keep the zone, the race-track mode shuts it while that file is shown
        public List<string>? DoorsOff { get; init; }
        // the actors put aside while one weather's island file is shown, each "<file> <scene> <actor> <x> <y> <z>" (RACEMOD.CPP actor_at=)
        public List<string>? ActorsAt { get; init; }
        // the lifts on the raised road, each "<scene> <x> <y> <z> <half> <pad body> <to scene> <to x> <to y> <to z>" (RACEMOD.CPP deck_lift=)
        public List<string>? DeckLifts { get; init; }
    }

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
            // (where the segment meets the edge: an x edge, a z edge, or -- through a corner -- both, each taken at its own point, between the
            // cubes either side of that edge there: a step through a corner meets one edge before the other. A zone into the cube across
            // the corner kept the coordinate along its edge and carried a car a whole cube along it -- the palace's spiral, 2026-10-13)
            if (ax != bx)
            {
                var ex = Math.Max(ax, bx) * 64.0; var t = (ex - x0) / (x1 - x0); var z = z0 + (z1 - z0) * t;
                var h = Height(ex, z, t);
                var row = az == bz ? az : (int)Math.Floor(z / 64);
                if (sceneOf.TryGetValue((ax, row), out var xFrom) && sceneOf.TryGetValue((bx, row), out var xTo))
                {
                    result.Add(new EdgeCrossing(xFrom, xTo, ax, row, bx > ax ? 'E' : 'W', z - row * 64.0, h));
                    result.Add(new EdgeCrossing(xTo, xFrom, bx, row, bx > ax ? 'W' : 'E', z - row * 64.0, h));
                }
            }
            if (az != bz)
            {
                var ez = Math.Max(az, bz) * 64.0; var t = (ez - z0) / (z1 - z0); var x = x0 + (x1 - x0) * t;
                var h = Height(x, ez, t);
                var col = ax == bx ? ax : (int)Math.Floor(x / 64);
                if (sceneOf.TryGetValue((col, az), out var zFrom) && sceneOf.TryGetValue((col, bz), out var zTo))
                {
                    result.Add(new EdgeCrossing(zFrom, zTo, col, az, bz > az ? 'S' : 'N', x - col * 64.0, h));
                    result.Add(new EdgeCrossing(zTo, zFrom, col, bz, bz > az ? 'N' : 'S', x - col * 64.0, h));
                }
            }
        }

        // ... and where the road's width reaches over an edge its middle doesn't cross: a crossing at each side of the road that does, each
        // way, unless one at about that place and height already covers it. Volcano Island's climb loop runs a cell inside the edge between
        // scenes 130 and 131 with its road reaching four cells over it: scene 130's own zone (its whole east edge up to 15,000) took a car on
        // the outside of the bend into 131, which had none back there, and the user's car sat at the edge on the third lap (2026-10-11).
        void Add(int fromScene, int toScene, int cubeX, int cubeZ, char side, double along, double h)
        {
            if (result.Any(e => e.FromScene == fromScene && e.ToScene == toScene && e.CubeX == cubeX && e.CubeZ == cubeZ && e.Side == side
                                && Math.Abs(e.Along - along) <= EdgeHalfCells - 2 && h >= e.Height - EdgeBelow + CoverBelow && h <= e.Height + EdgeAbove - CoverAbove))
                return;
            result.Add(new EdgeCrossing(fromScene, toScene, cubeX, cubeZ, side, along, h));
        }
        void Overhang(double cx, double cz, double ex, double ez, double h)
        {
            int ax = (int)Math.Floor(cx / 64), az = (int)Math.Floor(cz / 64), bx = (int)Math.Floor(ex / 64), bz = (int)Math.Floor(ez / 64);
            if (ax == bx && az == bz) return;
            // (each edge between the cubes either side of it where the line meets it, as the lap's own crossings above)
            if (ax != bx)
            {
                var edge = Math.Max(ax, bx) * 64.0; var z = cz + (ez - cz) * (edge - cx) / (ex - cx);
                var row = az == bz ? az : (int)Math.Floor(z / 64);
                if (sceneOf.TryGetValue((ax, row), out var from) && sceneOf.TryGetValue((bx, row), out var to))
                {
                    Add(from, to, ax, row, bx > ax ? 'E' : 'W', z - row * 64.0, h);
                    Add(to, from, bx, row, bx > ax ? 'W' : 'E', z - row * 64.0, h);
                }
            }
            if (az != bz)
            {
                var edge = Math.Max(az, bz) * 64.0; var x = cx + (ex - cx) * (edge - cz) / (ez - cz);
                var col = ax == bx ? ax : (int)Math.Floor(x / 64);
                if (sceneOf.TryGetValue((col, az), out var from) && sceneOf.TryGetValue((col, bz), out var to))
                {
                    Add(from, to, col, az, bz > az ? 'S' : 'N', x - col * 64.0, h);
                    Add(to, from, col, bz, bz > az ? 'N' : 'S', x - col * 64.0, h);
                }
            }
        }
        void Sides(double px, double pz, double dx, double dz, double half, double h)
        {
            var len = Math.Sqrt(dx * dx + dz * dz);
            if (len <= 0 || half <= 0) return;
            double nx = -dz / len * half, nz = dx / len * half;
            Overhang(px, pz, px + nx, pz + nz, h);
            Overhang(px, pz, px - nx, pz - nz, h);
        }
        // (the raised road by its own points and widths, as far as a car's middle goes on it -- the rail keeps it RailInset inside the edge
        // -- less the half cell inside the next cube a car arrives at: where a car can't be there, the zone would carry it over and its rail
        // straight back, a scene change a frame -- the Emerald Moon's straight, its rail a quarter cell short of the cube's edge, 2026-10-12;
        // and the strip beside it the same way. The ground's by the lap, to its curbs.)
        const double Reach = 1.25 + 0.5;
        var raised = report.Raised;
        for (var k = 0; k < raised.Count; k++)
        {
            if (raised[k][3] <= 0) continue;
            var a = raised[Math.Max(0, k - 1)]; var b = raised[Math.Min(raised.Count - 1, k + 1)];
            double px = raised[k][0] / 512.0, pz = raised[k][1] / 512.0, dx = (b[0] - a[0]) / 512.0, dz = (b[1] - a[1]) / 512.0;
            Sides(px, pz, dx, dz, raised[k][3] / 512.0 - Reach, raised[k][2]);
            if (raised[k].Length >= 7 && raised[k][6] > raised[k][5])
            {
                var len = Math.Sqrt(dx * dx + dz * dz);
                double lo = raised[k][5] / 512.0 + Reach, hi = raised[k][6] / 512.0 - Reach;
                if (len > 0 && hi > lo)
                    foreach (var u in new[] { lo, hi }) Overhang(px, pz, px - dz / len * u, pz + dx / len * u, raised[k][2]);
            }
        }
        for (var i = 0; i < n; i++)
        {
            if (up is not null && up[i]) continue;
            if (report.LapArc is { } arc && arc.Length == n && arc[i]) continue;
            int p = (i + n - 1) % n, q = (i + 1) % n;
            if (report.Open) { p = Math.Max(0, i - 1); q = Math.Min(n - 1, i + 1); }
            Sides(report.LapX[i], report.LapZ[i], report.LapX[q] - report.LapX[p], report.LapZ[q] - report.LapZ[p], options.CurbHalfWidth + 0.5, ground(report.LapX[i], report.LapZ[i]));
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
            // (the zone's start along the edge is also its arrival's other coordinate (Info0 of a north or south zone, Info2 of an east or
            // west one), and the engine reads 512 and 31744 there as the marks of the other edges' zones (OBJECT.CPP GereZoneChangeCube:
            // ZONE_EST, ZONE_OUEST, ...): a zone starting at 512 waited for the car to leave eastwards too, and never took it -- the Emerald
            // Moon's north edge out of scene 230, every lap held at the edge till the engine's fallback carried it over, 2026-10-16)
            if (lo == Near || lo == Far) lo--;
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
    // (A plan may stand them nearer: RaceTrackOptions.GridStep; or pole further back and the columns further apart: GridFirst, GridSide --
    // the Temple of Bú's, its flag marshal between them.)
    public const double GridFirst = 3, DefaultGridStep = 3.5, GridSide = 1.75;
    public const int GridSpots = 6;
    public static (double Back, double Side) GridSpot(int k, double step = DefaultGridStep, double first = GridFirst, double side = GridSide) =>
        (first + step * k, k % 2 == 0 ? -side : side);
    private static (double Back, double Side) GridSpot(int k, RaceTrackOptions o) => GridSpot(k, o.GridStep, o.GridFirst, o.GridSide);
    // The opponents' actors: no shock animation, not clipped before drawing, drawn against the depth buffer (so the bridge and the hills hide
    // them as they should) and, as every actor the game itself draws that way (Twinsen's buggy: BUGGY.CPP), without a shadow -- the engine
    // draws a depth-buffered actor's shadow over it, a dark patch across the car.
    internal const uint OpponentFlags = 0x1A1000;
    // how near a scene's cube a gas monster is to be one of its actors too (cells: seen from it)
    private const double GasMonsterSeen = 40;

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
        // ... how many rows across the road they make, and the lap's driving time they are spaced by (s: MushroomSpots)
        public int MushroomRows;
        public double LapTime;
        public List<int[]> Mushrooms = new(), Penguins = new(), Oil = new(), Fakes = new();
        // the machine guns on their knolls: each [scene, the gun's actor, its Franco's actor, its knoll in Report.Gunners, which gun on it]
        public List<int[]> Gunners = new();
        // the gas monsters: each [scene, its actor, its body, its place in Report.GasMonsters, 1 in its own scene's cube (0 seen from there)]
        public List<int[]> GasMonsters = new();
        // the people in the cars' way: each [scene, its actor, its place in Report.Guards, 1 in its own scene's cube (0 seen from there)]
        public List<int[]> Guards = new();
        // the island's own guns that fire at the cars (RaceTrackIsland.HuntingGuns): each [scene, its actor, its muzzle x, y, z (island units),
        // its reach (units)]
        public List<int[]> Hunters = new();
        // the flying gas monsters: each [scene, its actor, its place in Report.Flyers]
        public List<int[]> Flyers = new();
        public Result Result(List<string> log, int changed, int removed, Result? twin = null) =>
            new(log, changed, removed, Drivers.Select((d, k) => (d, Cars[k])).ToList(), StartScene, Grid, Pits, twin, Mushrooms, Penguins, Oil, Fakes, Gunners, GasMonsters, Guards, Hunters, Flyers);
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
        // (in the way of either road's cars: RaceTrackReport.InTheWay)
        Func<double, double, double, double, bool> inTheWay = (x, z, lo, hi) => tracks.Any(t => t.Report.InTheWay(x, z, lo, hi));
        Func<double, double, double>? groundAfter = tracks.Count == 1 ? report.GroundAfter
            : report.GroundAfter is null ? null : (x, z) => (tracks.MinBy(t => t.Report.DistanceToRoad(x, z))!.Report.GroundAfter ?? report.GroundAfter)(x, z);
        var store = new SceneStore(SceneGame.Lba2, gameDirectory);
        // the actors copied from other islands' scenes (the racer, the biker, the buggy) are taken from the original scenes: another island's
        // track built before this one in the same build has changed its own (RaceTrackService.Build)
        var originals = File.Exists(store.ScenePath + RaceTrackService.BackupSuffix) ? new SceneStore(SceneGame.Lba2, gameDirectory, "SCENE.HQR" + RaceTrackService.BackupSuffix) : store;
        var log = new List<string>();
        var changes = new List<SceneChange>();
        var removed = 0; var zonesRemoved = 0; var camerasRemoved = 0; var mushroomsLeftOut = 0;
        var doorsOff = new List<string>();
        var actorsAt = new List<(string File, int Scene, int Actor, int X, int Y, int Z)>();
        // ... and the scene's points their scripts stand them on (pos_point), moved with them in that weather only: shared by both files'
        // scenes, moved for the town circuit they stood the storm's Felix the fisherman by the ferry 17 cells off too, every frame (the
        // user, 2026-10-27: "Felix the fisherman seems to be spawning a long way from his normal spawn point on Citadel.ile")
        var pointsAt = new List<(string File, int Scene, int Point, int X, int Y, int Z)>();
        // (a weather's shift of an actor's copy: the scene's points it moved put back, and kept for that weather alone)
        void ShiftOneWeather(SceneModel m, string file, int sceneNumber, Action shift)
        {
            var before = m.TrackPoints.ToList();
            shift();
            for (var k = 0; k < before.Count && k < m.TrackPoints.Count; k++)
                if (m.TrackPoints[k] != before[k])
                {
                    var q = m.TrackPoints[k];
                    pointsAt.Add((file, sceneNumber, k, q.X, q.Y, q.Z));
                    m.TrackPoints[k] = before[k];
                }
        }
        var deckLifts = new List<string>();
        foreach (var t in tracks)
        {
            t.Drivers = t.Options.Racers().Where(d => !d.Ghost).ToList();
            t.Cars = t.Drivers.Select(_ => new Dictionary<int, int>()).ToList();
            if (t.Drivers.Any(d => d.Bike)) t.BikerTemplate = BikerTemplate(originals, t.Options, island, log);
            if (t.Drivers.Any(d => !d.Bike))
                try { t.Racer = originals.Load(RacerScene).Actors.Skip(1).FirstOrDefault(a => a.Entity == RacerEntity)?.Clone(); }
                catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { log.Add($"no opponent: scene {RacerScene} could not be read ({e.Message})"); }
            t.Edges = EdgeCrossings(store, t.Report, t.Options, island);
            t.MushroomSpots = MushroomSpots(t.Report, t.Options, out t.MushroomRows, out t.LapTime);
        }
        // the cast's cars the line-ups drive (RaceDriver.Cast): each one's entity and body there, as the folder's RACECARS.JSON lists them
        // (RaceTrackService.Finish installs them before the scenes); one it hasn't is not raced
        var castCars = tracks.Any(t => t.Drivers.Any(d => d.Cast >= 0))
            ? RaceTrackCharacterCars.Catalogue(gameDirectory).GroupBy(e => e.Number).ToDictionary(g => g.Key, g => g.First())
            : new Dictionary<int, RaceTrackCharacterCars.CatalogueEntry>();
        foreach (var t in tracks)
            foreach (var d in t.Drivers.Where(d => d.Cast >= 0))
            {
                if (!castCars.TryGetValue(d.Cast, out var e)) log.Add($"WARNING: {d.Name}'s car, car {d.Cast} of the cast's, isn't in the folder's {RaceTrackCharacterCars.CatalogueFile}: not raced");
                else if (e.Generic != d.Body) log.Add($"WARNING: {d.Name}'s car, car {d.Cast} ({e.Name}), is body {e.Generic} of entity {e.Entity}, not {d.Body}: its half-size copy is another's");
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
                // (and those the island's own story outside a race needs, whatever the build's story setting -- Polar Island's grobo with the
                // digger's key: the editor's track window has the story on for Citadel Island only, and he went, 2026-10-27)
                if (options.Island.KeptEntities is { } keep)
                    for (var i = 1; i < model.Actors.Count; i++) if (keep.Contains(model.Actors[i].Entity)) storyKept.Add(i);
                // (and the island's guns that fire at the cars: Island CX's cannons)
                if (options.Island.HuntingGuns is { } hunting)
                    for (var i = 1; i < model.Actors.Count; i++) if (model.Actors[i].Entity == hunting.Entity) storyKept.Add(i);
                // (and whoever switches a door on or off -- SET_CHANGE_CUBE: a lift, which opens the way down into the sewers once Twinsen
                // has ridden it down and shuts it as he comes back up, or whoever lets him into a cave. Taken away, the door stays as the scene
                // starts it: Citadel Island's lifts by the start line and over the school took Twinsen down as he walked onto their place, and
                // back up straight down again, 2026-10-27)
                var doorKeepers = new HashSet<int>();
                for (var i = 1; i < model.Actors.Count; i++)
                    if (!storyKept.Contains(i) && model.Actors[i].Entity != BuggyEntity && SwitchesDoors(model.Actors[i], i)) doorKeepers.Add(i);
                // (and the actors their scripts wait on or move, where those keep off the road: a script that takes Twinsen out of a door
                // waits for the actors walking him -- l_track_obj(6) -- before it lets the door back on. Citadel's scene 48's escorts walk
                // the road, and go)
                for (var grew = true; grew;)
                {
                    grew = false;
                    foreach (var k in doorKeepers.ToList())
                        foreach (var r in ActorsReferred(model.Actors[k], k))
                            if (r > 1 && r < model.Actors.Count && model.Actors[r].Entity != BuggyEntity && !storyKept.Contains(r) && !doorKeepers.Contains(r)
                                && OffRoad(model, r, inTheWay, out _) && doorKeepers.Add(r)) grew = true;
                }
                if (doorKeepers.Count > 0) log.Add($"scene {scene}: actors {string.Join(", ", doorKeepers.Order())} kept where they are -- their scripts switch doors on and off");
                foreach (var i in doorKeepers.Order())
                    if (!OffRoad(model, i, inTheWay, out var why)) log.Add($"scene {scene}: WARNING: actor {i} (entity {model.Actors[i].Entity}), kept for the doors it switches, is in the way: {why}");
                storyKept.UnionWith(doorKeepers);
                // (and on an island that keeps them, everything off the road: KeepOffRoadActors)
                var offRoad = new HashSet<int>();
                // (the ones put aside in one weather only, by their index before the removals: Result.ActorsAt once those are known)
                var pendingAt = new List<(int I, string File, int X, int Y, int Z)>();
                if (options.Island.KeepOffRoadActors)
                    for (var i = 1; i < model.Actors.Count; i++)
                    {
                        if (storyKept.Contains(i) || model.Actors[i].Entity == BuggyEntity || biking && model.Actors[i].Entity == BikerEntity) continue;
                        if (OffRoad(model, i, inTheWay, out var onIt)) offRoad.Add(i);
                        // (one that stands clear of the road but walks onto it -- the Desert island's camel, 2026-10-27: its way's points on
                        // the road moved off it, to the nearest clear place)
                        else if (onIt?.StartsWith("its scripts take it") == true
                                 && PointsOffRoad(model, i, inTheWay, report.GroundBefore, groundAfter, report.WasGround, scene, log)) offRoad.Add(i);
                        // (one seen -- a body, not hidden -- that stands on the road: moved aside, its way's points with it, and kept: Citadel
                        // Island's cow in its pasture, the town circuit through it -- the user, 2026-10-27, "The cow is still missing from the
                        // citadel track". The hidden ones on the road -- a script's helpers, with no body -- still go; one hidden with a body
                        // of its own shows up later: Felix's crab, which he fishes up and which then walks about -- gone, his rod threw the
                        // stand-in under the ground, the user, 2026-10-27)
                        else if (onIt?.StartsWith("it stands on the road") == true && model.Actors[i].Body >= 0
                                 && ((model.Actors[i].Flags & 0x200) == 0 || model.Actors[i].Body > 0)
                                 && MovedOffRoad(model, i)) offRoad.Add(i);
                        else log.Add($"scene {scene}: actor {i} (entity {model.Actors[i].Entity}) goes -- {onIt}");
                    }
                if (offRoad.Count > 0) log.Add($"scene {scene}: actors {string.Join(", ", offRoad.Order())} kept -- off the road, and their scripts keep them off it");
                storyKept.UnionWith(offRoad);
                var doomed = Enumerable.Range(1, model.Actors.Count - 1)
                    .Where(i => model.Actors[i].Entity != BuggyEntity && !(i == 1 && model.Actors[i].Entity == ZoeEntity && model.Actors[i].X == 0 && model.Actors[i].Z == 0))
                    .Where(i => !needed.Contains(i) && !storyKept.Contains(i) || biking && model.Actors[i].Entity == BikerEntity)
                    .ToList();
                if (biking && needed.Any(i => i < model.Actors.Count && model.Actors[i].Entity == BikerEntity))
                    log.Add($"scene {scene}: the bike taxi goes (he races now), though Twinsen's script rides with him in a cutscene");
                needed.RemoveWhere(i => biking && i < model.Actors.Count && model.Actors[i].Entity == BikerEntity);
                if (needed.Count > 0) log.Add($"scene {scene}: actors {string.Join(", ", needed.Order())} kept -- Twinsen's own script waits on them in its travel cutscenes (the ferry, the Dino-Fly), which would never end without them");
                if (storyKept.Count > 0) log.Add($"scene {scene}: actors {string.Join(", ", storyKept.Order())} kept where they are -- the story needs them");
                // (one in the way of one weather's road only -- the island's two files share the scenes -- stays where it is, and is put aside
                // while that file is shown: Result.ActorsAt. Citadel Island's bike taxi waits under the shelter by the shop in the storm, the
                // storm track's deck far over him; the town circuit runs through the shelter -- moved for it, he stood by a lamp post in the
                // rain, 2026-10-27)
                void Aside(int i)
                {
                    var a = model.Actors[i];
                    double cx = model.CubeX * 64 + a.X / 512.0, cz = model.CubeY * 64 + a.Z / 512.0;
                    var way = tracks.Select(t => t.Report.InTheWay(cx, cz, a.Y, a.Y + 1500)).ToList();
                    if (tracks.Count == 2 && way[0] != way[1])
                    {
                        var moved = a.Clone();
                        var file = Path.GetFileNameWithoutExtension(way[0] ? options.Island.IleFile : options.Island.TwinIleFile ?? options.Island.IleFile).ToLowerInvariant();
                        ShiftOneWeather(model, file, scene, () => ShiftOffRoad(moved, model, i, distanceToRoad, inTheWay, report.GroundBefore, groundAfter, report.WasGround, roadReach, scene, log));
                        if (moved.X == a.X && moved.Z == a.Z) return;
                        actorsAt.Add((file, scene, i - doomed.Count(d => d < i), moved.X, moved.Y, moved.Z));
                        log.Add($"scene {scene}: ... only while {file} is shown: in the other weather it stays where it was, at ({a.X},{a.Y},{a.Z})");
                        return;
                    }
                    ShiftOffRoad(a, model, i, distanceToRoad, inTheWay, report.GroundBefore, groundAfter, report.WasGround, roadReach, scene, log);
                }
                foreach (var i in needed.Order())
                    if (i < model.Actors.Count) Aside(i);
                bool MovedOffRoad(SceneModel m, int i)
                {
                    var a = m.Actors[i];
                    var (x, y, z) = (a.X, a.Y, a.Z);
                    // (in the way of one weather's road only -- Citadel Island's cow, the town circuit through its pasture and the storm track
                    // nowhere near it: "please put it back along with its scripts of giving Twinsen a clover and talking to Twinsen correctly",
                    // the user, 2026-10-27 -- it stays where it is, its scripts' points too, and is put aside while that weather's file is shown)
                    if (tracks.Count == 2)
                    {
                        double ax = m.CubeX * 64 + a.X / 512.0, az = m.CubeY * 64 + a.Z / 512.0;
                        var way = tracks.Select(t => t.Report.InTheWay(ax, az, a.Y, a.Y + 1500)).ToList();
                        if (way[0] != way[1])
                        {
                            var moved = a.Clone();
                            var file = Path.GetFileNameWithoutExtension(way[0] ? options.Island.IleFile : options.Island.TwinIleFile ?? options.Island.IleFile).ToLowerInvariant();
                            ShiftOneWeather(m, file, scene, () => ShiftOffRoad(moved, m, i, distanceToRoad, inTheWay, report.GroundBefore, groundAfter, report.WasGround, roadReach, scene, new List<string>()));
                            if (moved.X != a.X || moved.Z != a.Z) pendingAt.Add((i, file, moved.X, moved.Y, moved.Z));
                            log.Add($"scene {scene}: actor {i} (entity {a.Entity}) is on {file}'s road only: kept where it is, and put aside to ({moved.X},{moved.Y},{moved.Z}) while {file} is shown");
                            return true;
                        }
                    }
                    ShiftOffRoad(a, m, i, distanceToRoad, inTheWay, report.GroundBefore, groundAfter, report.WasGround, roadReach, scene, log);
                    if (OffRoad(m, i, inTheWay, out _)) return true;
                    if (PointsOffRoad(m, i, inTheWay, report.GroundBefore, groundAfter, report.WasGround, scene, log) && OffRoad(m, i, inTheWay, out _)) return true;
                    a.X = x; a.Y = y; a.Z = z;
                    return false;
                }
                // (the story's people stay, but not on the road: Polar Island's grobo with the digger's key stands on the dock)
                foreach (var i in storyKept.Order())
                    if (options.Island.KeptEntities?.Contains(model.Actors[i].Entity) == true && !offRoad.Contains(i))
                        Aside(i);
                foreach (var p in pendingAt) actorsAt.Add((p.File, scene, p.I - doomed.Count(d => d < p.I), p.X, p.Y, p.Z));
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
                // (not at a door: its decor keeps its height, and the inside scene's way out brings Twinsen up onto it -- the hero's place on
                // Citadel Island's sewer lift by the storm track's start line, carried up 1,562 by the town circuit's bridge ramp beside it, had
                // him in the air for a frame coming up in the storm, the user 2026-10-27)
                const double reach = RaceTrackOptions.DoorDecorReach;
                Reseat(model, groundBefore, after, wasGround, scene, log, raiseOnly: tracks.Count > 1,
                    keep: (x, z) => options.DoorBoxes.Any(b => x >= b.X0 - reach && x <= b.X1 + reach && z >= b.Z0 - reach && z <= b.Z1 + reach));
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
                var curved = Behind(t.Report, s, GridSpot(GridSpots - 1, t.Options).Back + 2, 0) is { } far && Math.Abs((far.X - s.X) * -s.DirZ + (far.Z - s.Z) * s.DirX) > 0.25;
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
                        var (back, side) = GridSpot(k, t.Options);
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
                        // (said: the Temple of Bú's plan once had all its spots in the cube north of the start's, none was written, and the
                        // race-track mode stood the opponents on their grid spots -- the racing lane -- through the qualifying lap, 2026-10-17)
                        if ((int)Math.Floor(wpx / IslandFile.CubeSize) != cx || (int)Math.Floor(wpz / IslandFile.CubeSize) != cz)
                        {
                            log.Add($"scene {scene}: WARNING: the waiting spot at cell ({p.X:0.0}, {p.Z:0.0}) is not in the start line's cube ({cx}, {cz}): left out -- an " +
                                    "opponent with no waiting spot stands on its grid spot, on the racing lane, while the player qualifies");
                            continue;
                        }
                        int px = (int)Math.Round(wpx - cx * (double)IslandFile.CubeSize), pz = (int)Math.Round(wpz - cz * (double)IslandFile.CubeSize);
                        var pbeta = (int)Math.Round(Math.Atan2(p.DirX, p.DirZ) / (2 * Math.PI) * 4096); pbeta = ((pbeta % 4096) + 4096) % 4096;
                        // (a spot whose height the plan gives stands on a deck of decor, which the ground knows nothing of)
                        t.Pits.Add(new[] { cx, cz, px, t.Report.PitHeights ? (int)Math.Round(p.Y) : Ground(px, pz), pz, pbeta });
                    }
                var pole = GridSpot(0, t.Options);
                if (buggy is not null) { buggy.X = At(pole.Back, pole.Side + shift, false); buggy.Z = At(pole.Back, pole.Side + shift, true); buggy.Y = Ground(buggy.X, buggy.Z); buggy.Beta = Facing(pole.Back); }
                // Twinsen right behind his car: he comes into the scene facing the way the lap runs (the scene's start keeps no
                // facing), so he faces the car and the action key gets him in. (Beside it, 1.3 cells from its middle, the car's box
                // pushed him off and he faced away from it.) The next car is on the other side, clear of him.
                model.Hero.X = At(pole.Back + 1.8, pole.Side + shift, false); model.Hero.Z = At(pole.Back + 1.8, pole.Side + shift, true); model.Hero.Y = Ground(model.Hero.X, model.Hero.Z) + 100;
                model.Hero.Beta = beta;
                t.StartScene = scene;
                // (the one the island's story has waiting at the start -- Polar Island's grobo with the digger's key, the user, 2026-10-27:
                // "have him spawn in behind the cars closer to Twinsen": three cells behind Twinsen and one to the side away from the next
                // car, facing him -- just out of the reach of his script, 1500, a few steps from him: the Action key at the car isn't his --
                // brought from the island's own scene of him
                // (every scene's copy goes: RemoveActors))
                if (options.Island.StartPerson is { } person && StartPersonActor(store, options.Island, person) is { } waiting)
                {
                    var away = Math.Sign(pole.Side - GridSpot(1, t.Options).Side) is var sg && sg != 0 ? sg : 1;
                    waiting.X = At(pole.Back + 5, pole.Side + shift + 1.2 * away, false); waiting.Z = At(pole.Back + 5, pole.Side + shift + 1.2 * away, true);
                    waiting.Y = Ground(waiting.X, waiting.Z);
                    waiting.Beta = ((int)Math.Round(Math.Atan2(model.Hero.X - waiting.X, model.Hero.Z - waiting.Z) * 4096 / (2 * Math.PI)) % 4096 + 4096) % 4096;
                    SceneOps.AddActor(model, waiting);
                    log.Add($"scene {scene}: entity {person} (the island's story's, at the start) behind Twinsen at ({waiting.X},{waiting.Y},{waiting.Z})");
                    // (and the one to beat beside his car on pole, his scripts gone -- the race-track mode walks him into it: chase_car=)
                    if (options.Island.ChaseDriver is { } cd && ChaseDriverActor(originals, cd) is { } own)
                    {
                        var driver = own.Clone();
                        driver.Life = new byte[] { 0 }; driver.Track = new byte[] { 0 }; driver.Move = 0;
                        driver.X = At(pole.Back + 0.3, pole.Side + shift - 1.9 * away, false); driver.Z = At(pole.Back + 0.3, pole.Side + shift - 1.9 * away, true);
                        driver.Y = Ground(driver.X, driver.Z);
                        driver.Beta = ((int)Math.Round(Math.Atan2(model.Hero.X - driver.X, model.Hero.Z - driver.Z) * 4096 / (2 * Math.PI)) % 4096 + 4096) % 4096;
                        SceneOps.AddActor(model, driver);
                        log.Add($"scene {scene}: entity {cd.Entity} (the one to beat himself, scene {cd.Scene}'s actor {cd.Actor}) beside his car on pole at ({driver.X},{driver.Y},{driver.Z})");
                    }
                    // (and Twinsen's car parked off the road beside him -- the story's start, chase=: FunFrock takes pole and drives off, and
                    // Twinsen takes the car the grobo gives him the key of. A race that starts in the car -- the race game's -- puts it on the
                    // grid as it forms)
                    if (buggy is not null)
                    {
                        buggy.X = At(pole.Back + 5, pole.Side + shift + 5.5 * away, false); buggy.Z = At(pole.Back + 5, pole.Side + shift + 5.5 * away, true);
                        buggy.Y = Ground(buggy.X, buggy.Z); buggy.Beta = Facing(pole.Back + 5);
                        log.Add($"scene {scene}: the car parked off the road beside it at ({buggy.X},{buggy.Y},{buggy.Z})");
                    }
                }
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
                // (the story's car: first at Twinsen's house, then where he left it -- CarHome)
                (int X, int Y, int Z, int Beta, bool Here)? home = null;
                if (options.Island.CarHome is { } ch)
                {
                    var here = (int)Math.Floor(ch.X / 64) == cx && (int)Math.Floor(ch.Z / 64) == cz;
                    int hx = (int)Math.Round((ch.X - cx * 64) * 512), hz = (int)Math.Round((ch.Z - cz * 64) * 512);
                    home = (hx, here ? Ground(hx, hz) : 0, hz, ch.Beta, here);
                }
                if (buggyIndex > 0 && StartBuggyScript(model, scene, buggyIndex, log, park, home) is { } withBuggy) model = withBuggy;
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
                    // (a door whose lift or house the road took away, every piece of it: RaceTrackReport.LostDoors -- in every file of the
                    // island's. Where the island has two weathers' files the scenes' one zone is both's door: one lost in one file only and
                    // walked into its wall (ZONE_TEST_BRICK) is shut while that file is shown (Result.DoorsOff) -- the storm track's deck runs
                    // over the lift by its start line, and walking onto its place took Twinsen down into the sewers and back up straight down
                    // again, while the museum's door, the top of the building taken for the storm track's deck, stands in the storm's file
                    // and not in the town's; one without a wall stays -- the town circuit takes the top of the Tralu's cave's mouth)
                    if (door)
                    {
                        double bx0 = model.CubeX * 64 + zone.X0 / 512.0, bz0 = model.CubeY * 64 + zone.Z0 / 512.0;
                        double bx1 = model.CubeX * 64 + zone.X1 / 512.0, bz1 = model.CubeY * 64 + zone.Z1 / 512.0;
                        var walled = zone.Info.Length > 5 && (zone.Info[5] & 1) != 0;
                        bool Same((double X0, double Z0, double X1, double Z1) l) => Math.Abs(l.X0 - bx0) < 0.01 && Math.Abs(l.Z0 - bz0) < 0.01 && Math.Abs(l.X1 - bx1) < 0.01 && Math.Abs(l.Z1 - bz1) < 0.01;
                        bool Lost(Track t) => t.Report.LostDoors.Any(Same);
                        // (a door buried under one file's raised ground is shut there with or without a wall: its way back out brought
                        // Twinsen up inside the ground -- Citadel Island's sewer lift by the storm track's start line, under the town
                        // circuit's bridge ramp in the fine weather)
                        if (tracks.Where(Lost).All(t => t.Report.BuriedDoors.Any(Same))) walled = true;
                        if (walled && !tracks.All(Lost))
                            foreach (var t in tracks.Where(Lost))
                            {
                                var file = Path.GetFileNameWithoutExtension(ReferenceEquals(t, tracks[0]) ? options.Island.IleFile : options.Island.TwinIleFile ?? options.Island.IleFile).ToLowerInvariant();
                                doorsOff.Add($"{file} {scene} {zone.X0} {zone.Z0} {zone.X1} {zone.Z1}");
                                var buried = t.Report.BuriedDoors.Any(Same);
                                log.Add($"scene {scene}: door zone {z} (into scene {zone.Num}) shut while {file} is shown -- the lift or building it was the door of " + (buried ? "is under that file's raised ground" : "went with that file's road"));
                                // (and the way back out of there to this door's place -- a lift's ride up onto a platform that isn't there: Twinsen
                                // up in the air and dropping to the ground, the user, 2026-10-27, "taking the elevator platform back up from the sewers
                                // by the track start line causes twinsen to momentarily be up in the air")
                                // ... not where the door is buried, or where the inside scene's own script takes Twinsen out (a lift's ride
                                // up, which ends his script for the change of scene): shut, he was left at the top of the shaft for good --
                                // Citadel Island's sewer lift by the storm track's start line, under the town circuit's bridge ramp in the
                                // fine weather. He comes out onto the ground there instead
                                try
                                {
                                    if (buried || !store.SceneExists(zone.Num)) continue;
                                    var inside = store.Load(zone.Num);
                                    string heroLife;
                                    try { heroLife = LbaScript.LifeText.Decompile(inside.Hero.Life, 0, LbaScript.NoSymbols.Instance); }
                                    catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or InvalidOperationException) { heroLife = ""; }
                                    if (heroLife.Contains($"set_change_cube({scene},")) { log.Add($"scene {zone.Num}: its way out to scene {scene}'s door zone {z} stays -- Twinsen's own script rides him out that way"); continue; }
                                    foreach (var back in inside.Zones.Where(bz => bz.Type == 0 && bz.Num == scene && bz.Info.Length >= 3
                                                 && bz.Info[0] >= zone.X0 - 1536 && bz.Info[0] <= zone.X1 + 1536 && bz.Info[2] >= zone.Z0 - 1536 && bz.Info[2] <= zone.Z1 + 1536))
                                    {
                                        doorsOff.Add($"{file} {zone.Num} {back.X0} {back.Z0} {back.X1} {back.Z1}");
                                        log.Add($"scene {zone.Num}: its way out to scene {scene}'s door zone {z} shut while {file} is shown too");
                                    }
                                }
                                catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { }
                            }
                        if (tracks.All(Lost))
                        {
                            log.Add($"scene {scene}: door zone {z} (into scene {zone.Num}) is removed -- the lift or building it was the door of went with the road");
                            SceneOps.DeleteZone(model, z);
                            zonesRemoved++;
                            continue;
                        }
                    }
                    if (!(camera || options.RemoveRoadZones && (door || kind is 3 or 6 or 7 or 8 or 9))) continue;
                    // (tested at the centres of the cells the box covers: half a cell's diagonal more makes it the box itself that counts. A camera
                    // by its reach round the road; any other zone only where it is in the way of the cars, at its own heights -- InTheWay: a zone
                    // beside the road, or over or under it, stays, 2026-10-27)
                    var reach = roadReach + options.CameraMargin + Math.Sqrt(0.5);
                    var ox = model.CubeX * (double)IslandFile.CubeSize; var oz = model.CubeY * (double)IslandFile.CubeSize;
                    var hit = false;
                    for (var cz = (int)Math.Floor((oz + zone.Z0) / 512); cz <= (int)Math.Floor((oz + zone.Z1) / 512) && !hit; cz++)
                    for (var cx = (int)Math.Floor((ox + zone.X0) / 512); cx <= (int)Math.Floor((ox + zone.X1) / 512) && !hit; cx++)
                        if (camera ? distanceToRoad(cx + 0.5, cz + 0.5) <= reach : inTheWay(cx + 0.5, cz + 0.5, zone.Y0, zone.Y1)) hit = true;
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
            // (a lift on the raised road, RaceTrackReport.DeckLifts: a door zone over its pad, into its building's scene)
            // (an island with a file for each weather: each file's lifts its own, and the building's window brings Twinsen up onto the lift of
            // the file shown -- door_to=; the storm's museum lift and the town circuit's, 2026-10-27)
            string FileOf(Track t) => Path.GetFileNameWithoutExtension(ReferenceEquals(t, tracks[0]) ? options.Island.IleFile : options.Island.TwinIleFile ?? options.Island.IleFile).ToLowerInvariant();
            foreach (var t in tracks)
                foreach (var l in t.Report.DeckLifts)
                {
                    if ((int)Math.Floor(l.X / 64) != model.CubeX || (int)Math.Floor(l.Z / 64) != model.CubeY) continue;
                    int lx = (int)Math.Round((l.X - model.CubeX * 64) * 512), lz = (int)Math.Round((l.Z - model.CubeY * 64) * 512), ly = (int)Math.Round(l.Y);
                    // (no door zone: the action key on the pad takes Twinsen down with it, the race-track mode's lift -- deck_lift=)
                    deckLifts.Add($"{scene} {lx} {ly} {lz} {RaceTrackBuilder.LiftZoneHalf} {l.Pad} {l.Scene} {l.ToX} {l.ToY} {l.ToZ}" + (tracks.Count > 1 ? $" {FileOf(t)}" : ""));
                    log.Add($"scene {scene}: a lift on the raised road at ({lx},{ly},{lz}), down into scene {l.Scene} with the action key");
                    // (and back up: the building's way out by its window -- its door into this scene nearest the place the lift brings him
                    // down to -- brings him onto the road beside the pad. The user, 2026-10-27: "If Twinsen leaves the museum by the window then
                    // the elevator should take him back up and onto the track")
                    if (!store.SceneExists(l.Scene)) continue;
                    var inside = store.Load(l.Scene);
                    var window = inside.Zones.Where(zz => zz.Type == 0 && zz.Num == scene)
                        .MinBy(zz => Math.Pow((zz.X0 + zz.X1) / 2.0 - l.ToX, 2) + Math.Pow((zz.Y0 + zz.Y1) / 2.0 - l.ToY, 2) + Math.Pow((zz.Z0 + zz.Z1) / 2.0 - l.ToZ, 2));
                    if (window is null || window.Info.Length < 3) continue;
                    if (tracks.Count > 1)
                    {
                        doorsOff.Add($"{FileOf(t)} {l.Scene} {window.X0} {window.Z0} {window.X1} {window.Z1} {lx} {ly} {lz}");
                        log.Add($"scene {l.Scene}: its window's way out into scene {scene} the lift's while {FileOf(t)} is shown: onto the road at ({lx},{ly},{lz})");
                        continue;
                    }
                    // (onto the pad, below the road: the lift brings him up)
                    window.Info[0] = lx; window.Info[1] = ly; window.Info[2] = lz;
                    changes.Add(new SceneChange(l.Scene, inside, null));
                    log.Add($"scene {l.Scene}: its window's way out into scene {scene} now the lift's: onto the raised road at ({window.Info[0]},{window.Info[1]},{window.Info[2]})");
                }
            // (a building's front put back elsewhere in one file -- RaceTrackReport.Moved, Citadel Island's museum in the town circuit's file: a
            // copy of its door zone moved with it, shut in the other file, and its way out to the old door brings Twinsen out at the new one
            // in that file -- the old door's own shut there, door_off=)
            foreach (var t in tracks)
            {
                if (t.Report.Moved is not { } mv || ReferenceEquals(t, tracks[0]) || tracks.Count < 2) continue;
                double fx = mv.FromX - model.CubeX * 64, fz = mv.FromZ - model.CubeY * 64;
                if (fx < 0 || fx >= 64 || fz < 0 || fz >= 64) continue;
                int dx = (int)Math.Round(mv.Dx * 512), dz = (int)Math.Round(mv.Dz * 512);
                var old = model.Zones.Where(zz => zz.Type == 0 && !outside.Contains(zz.Num))
                    .MinBy(zz => Math.Pow((zz.X0 + zz.X1) / 1024.0 - fx, 2) + Math.Pow((zz.Z0 + zz.Z1) / 1024.0 - fz, 2));
                if (old is null || Math.Pow((old.X0 + old.X1) / 1024.0 - fx, 2) + Math.Pow((old.Z0 + old.Z1) / 1024.0 - fz, 2) > 36) continue;
                var copy = old.Clone();
                copy.X0 += dx; copy.X1 += dx; copy.Z0 += dz; copy.Z1 += dz;
                model.Zones.Add(copy);
                doorsOff.Add($"{FileOf(tracks[0])} {scene} {copy.X0} {copy.Z0} {copy.X1} {copy.Z1}");
                log.Add($"scene {scene}: door zone into scene {old.Num} copied to ({copy.X0},{copy.Z0})-({copy.X1},{copy.Z1}) with the building's front, for {FileOf(t)} only");
                if (!store.SceneExists(old.Num)) continue;
                foreach (var back in store.Load(old.Num).Zones.Where(bz => bz.Type == 0 && bz.Num == scene && bz.Info.Length >= 3
                             && bz.Info[0] >= old.X0 - 1536 && bz.Info[0] <= old.X1 + 1536 && bz.Info[2] >= old.Z0 - 1536 && bz.Info[2] <= old.Z1 + 1536))
                {
                    doorsOff.RemoveAll(d => d == $"{FileOf(t)} {old.Num} {back.X0} {back.Z0} {back.X1} {back.Z1}");
                    doorsOff.Add($"{FileOf(t)} {old.Num} {back.X0} {back.Z0} {back.X1} {back.Z1} {back.Info[0] + dx} {back.Info[1]} {back.Info[2] + dz}");
                    log.Add($"scene {old.Num}: its way out to that door brings Twinsen out at the moved front while {FileOf(t)} is shown");
                }
            }
            foreach (var t in tracks)
            {
                // each driver's car: the racer's entity with the driver's body (Baldino's rocket car, RaceTrackBaldinoCar, and the cars made
                // after the characters, RaceTrackCharacterCars), so the racer's animations drive it -- or a cast's car, a body of an entity of
                // its own with the racer's animations; or the motorbike Rabbibunny -- the same flags and empty scripts, standing astride his
                // bike until the race-track mode moves him
                for (var k = 0; k < t.Drivers.Count; k++)
                {
                    var d = t.Drivers[k];
                    var template = d.Bike ? t.BikerTemplate : t.Racer;
                    if (template is null) continue;
                    RaceTrackCharacterCars.CatalogueEntry? cast = null;
                    if (d.Cast >= 0 && !castCars.TryGetValue(d.Cast, out cast)) continue;
                    var car = template.Clone();
                    car.Body = d.Bike ? 0 : cast?.Generic ?? d.Body;
                    if (cast is not null) car.Entity = cast.Entity;
                    car.Flags = OpponentFlags; car.Move = 0; car.Anim = d.Bike ? BikerIdleAnim : 0; car.Life = new byte[] { 0 }; car.Track = new byte[] { 0 };
                    car.X = IslandFile.CubeSize / 2; car.Z = IslandFile.CubeSize / 2; car.Y = -20000; car.Beta = 0;
                    if (k < t.CarAt.Count && t.CarAt[k] is { } g) { car.X = g.X; car.Z = g.Z; car.Beta = g.Beta; car.Y = g.Y; }
                    t.Cars[k][scene] = SceneOps.AddActor(model, car);
                }
                t.CarAt.Clear();
            }
            // the machine guns on their knolls (Report.Gunners): the scene's own guns (Otringal's Francos' machine guns, entity 200) moved up onto
            // the knoll inside the corner, without their scripts -- the guns' life scripts put them on the scene's points beside the road
            // every frame -- each manned by a Franco (entity 201, sitting at it) whose bursts the race-track mode fires (gunner=; the user,
            // 2026-10-10, of the second gun standing there alone: "let's either remove a gun or add a second gunner")
            foreach (var t in tracks)
                foreach (var g in t.Report.Gunners)
                {
                    if ((int)Math.Floor(g.X / 64) != model.CubeX || (int)Math.Floor(g.Z / 64) != model.CubeY) continue;
                    double kx = (g.X - model.CubeX * 64) * 512, kz = (g.Z - model.CubeY * 64) * 512;
                    var guns = Enumerable.Range(1, model.Actors.Count - 1).Where(i => model.Actors[i].Entity == GunEntity)
                        .OrderBy(i => Math.Abs(model.Actors[i].X - kx) + Math.Abs(model.Actors[i].Z - kz)).Take(2).ToList();
                    if (guns.Count == 0) { log.Add($"scene {scene}: WARNING: no machine gun to put on the knoll at ({g.X:0.#}, {g.Z:0.#})"); continue; }
                    // (facing the middle of where it fires; the second gun beside the first, the Franco behind the first's stock)
                    double fx = g.FaceX - g.X, fz = g.FaceZ - g.Z, fl = Math.Max(1e-9, Math.Sqrt(fx * fx + fz * fz));
                    fx /= fl; fz /= fl;
                    var beta = (int)Math.Round(Math.Atan2(fx, fz) * 4096 / (2 * Math.PI)) & 4095;
                    var y = (int)Math.Round(g.Top);
                    for (var k = 0; k < guns.Count; k++)
                    {
                        var gun = model.Actors[guns[k]];
                        double side = k == 0 ? 0 : 1.6 * 512, ahead = 0.8 * 512;
                        gun.X = (int)Math.Round(kx + fx * ahead + fz * side); gun.Z = (int)Math.Round(kz + fz * ahead - fx * side);
                        gun.Y = y; gun.Beta = beta; gun.Anim = 0; gun.Move = 0; gun.Life = new byte[] { 0 }; gun.Track = new byte[] { 0 };
                    }
                    var francos = new List<int>();
                    for (var k = 0; k < guns.Count; k++)
                    {
                        var at = model.Actors[guns[k]];
                        var franco = at.Clone();
                        franco.Entity = FrancoEntity; franco.Body = 0; franco.Anim = FrancoAtTheGun;
                        franco.X = (int)Math.Round(at.X - fx * FrancoBehind); franco.Z = (int)Math.Round(at.Z - fz * FrancoBehind); franco.Y = y;
                        franco.Beta = beta; franco.Move = 0; franco.Life = new byte[] { 0 }; franco.Track = new byte[] { 0 };
                        var fi = SceneOps.AddActor(model, franco);
                        francos.Add(fi);
                        t.Gunners.Add(new[] { scene, guns[k], fi, t.Report.Gunners.IndexOf(g), k });
                    }
                    log.Add($"scene {scene}: the machine guns (actors {string.Join(", ", guns)}) on the knoll at ({g.X:0.#}, {g.Z:0.#}), {y} high, a Franco at each (actors {string.Join(", ", francos)})");
                }
            // the island's own guns that fire at the cars (RaceTrackIsland.HuntingGuns: Island CX's cannons on its corner towers), where they
            // stand, their scripts emptied -- the race-track mode fires their bursts (gunner= and gunner_hunt=: at a car it picks outside)
            if (options.Island.HuntingGuns is { } guns_)
                foreach (var t in tracks.Take(1))
                    for (var i = 1; i < model.Actors.Count; i++)
                    {
                        var gun = model.Actors[i];
                        if (gun.Entity != guns_.Entity) continue;
                        gun.Life = new byte[] { 0 }; gun.Track = new byte[] { 0 }; gun.Move = 0;
                        t.Hunters.Add(new[] { scene, i, model.CubeX * IslandFile.CubeSize + gun.X, gun.Y + LBAAssembler.Terrain.ControlTower.CxTowerTrack.CannonMuzzle,
                                              model.CubeY * IslandFile.CubeSize + gun.Z, (int)Math.Round(guns_.Reach * 512) });
                        log.Add($"scene {scene}: the cannon (actor {i}) at ({gun.X}, {gun.Y}, {gun.Z}) fires at the cars outside within {guns_.Reach:0} cells");
                    }
            // the gas monsters (Report.GasMonsters): the game's gas monster, made bigger (RaceTrackGasMonster), out of sight until the
            // race-track mode raises it out of the gas (gasmonster=) -- an actor of each scene it can be seen from: its own, and every one
            // whose cube comes within GasMonsterSeen of it (the engine draws a scene's actors only while it is the one about: the islets'
            // monsters stood up out of nowhere as the car came into their scene -- the user, 2026-10-10: "Our gas monsters are invisible
            // until we're right at them, which takes the user by surprise so they have no chance to dodge"); the race-track mode poses
            // the one of the scene about
            if (mushroomTemplate is not null)
                foreach (var t in tracks)
                    for (var gi = 0; gi < t.Report.GasMonsters.Count; gi++)
                    {
                        var g = t.Report.GasMonsters[gi];
                        double ox = Math.Clamp(g.X, model.CubeX * 64.0, model.CubeX * 64.0 + 64), oz = Math.Clamp(g.Z, model.CubeY * 64.0, model.CubeY * 64.0 + 64);
                        if ((ox - g.X) * (ox - g.X) + (oz - g.Z) * (oz - g.Z) > GasMonsterSeen * GasMonsterSeen) continue;
                        if (model.Actors.Count + 1 + PenguinsPerScene + OilPerScene + FakesPerScene >= SceneValidator.MaxObjects) { log.Add($"scene {scene}: WARNING: no room for the gas monster at ({g.X:0.#}, {g.Z:0.#})"); continue; }
                        var monster = mushroomTemplate.Clone();
                        monster.Entity = RaceTrackGasMonster.Entity; monster.Body = 0; monster.Anim = Math.Max(0, g.FirstAnim);
                        monster.Flags = OpponentFlags; monster.Move = 0; monster.Life = new byte[] { 0 }; monster.Track = new byte[] { 0 };
                        // (its start in the cube -- a record's place is 16 bits, and one of the next cube's is past the edge: the race-track mode
                        // stands it where it is, every frame)
                        monster.X = Math.Clamp((int)Math.Round((g.X - model.CubeX * 64) * 512), 0, 32767); monster.Z = Math.Clamp((int)Math.Round((g.Z - model.CubeY * 64) * 512), 0, 32767);
                        monster.Y = -20000; monster.Beta = 0;
                        var actor = SceneOps.AddActor(model, monster);
                        var home = (int)Math.Floor(g.X / 64) == model.CubeX && (int)Math.Floor(g.Z / 64) == model.CubeY;
                        t.GasMonsters.Add(new[] { scene, actor, g.Body, gi, home ? 1 : 0 });
                        log.Add($"scene {scene}: a gas monster (actor {actor}) in the gas at ({g.X:0.#}, {g.Z:0.#}){(home ? "" : ", in the next cube")}");
                    }
            // the flying gas monsters (Report.Flyers: RaceTrackFlyers) -- an actor in the scene of the cube their way starts in, its scripts gone:
            // the race-track mode flies it (flyer=)
            if (mushroomTemplate is not null)
                foreach (var t in tracks)
                    for (var fi = 0; fi < t.Report.Flyers.Count; fi++)
                    {
                        var f = t.Report.Flyers[fi];
                        var (fx, fz) = f.Way[0];
                        if ((int)Math.Floor(fx / 64) != model.CubeX || (int)Math.Floor(fz / 64) != model.CubeY) continue;
                        if (model.Actors.Count + 1 + PenguinsPerScene + OilPerScene + FakesPerScene >= SceneValidator.MaxObjects) { log.Add($"scene {scene}: WARNING: no room for the flying gas monster at ({fx:0.#}, {fz:0.#})"); continue; }
                        var flyer = mushroomTemplate.Clone();
                        flyer.Entity = RaceTrackFlyers.Entity; flyer.Body = 0; flyer.Anim = 0;
                        flyer.Flags = OpponentFlags; flyer.Move = 0; flyer.Life = new byte[] { 0 }; flyer.Track = new byte[] { 0 };
                        flyer.X = Math.Clamp((int)Math.Round((fx - model.CubeX * 64) * 512), 0, 32767); flyer.Z = Math.Clamp((int)Math.Round((fz - model.CubeY * 64) * 512), 0, 32767);
                        flyer.Y = (int)Math.Round(f.Y); flyer.Beta = 0;
                        var actor = SceneOps.AddActor(model, flyer);
                        t.Flyers.Add(new[] { scene, actor, fi });
                        log.Add($"scene {scene}: a flying gas monster (actor {actor}) over ({fx:0.#}, {fz:0.#}), {f.Way.Count} points at {f.Y:0}");
                    }
            // the people in the cars' way (Report.Guards: RaceTrackGuards) -- an actor of each scene they can be seen from, as the gas monsters
            // are, their scripts gone, out of sight till the race-track mode stands them at their posts (guard=)
            if (mushroomTemplate is not null)
                foreach (var t in tracks)
                    for (var gi = 0; gi < t.Report.Guards.Count; gi++)
                    {
                        var g = t.Report.Guards[gi];
                        double gx = g[2], gz = g[3];
                        double ox = Math.Clamp(gx, model.CubeX * 64.0, model.CubeX * 64.0 + 64), oz = Math.Clamp(gz, model.CubeY * 64.0, model.CubeY * 64.0 + 64);
                        if ((ox - gx) * (ox - gx) + (oz - gz) * (oz - gz) > RaceTrackGuards.Seen * RaceTrackGuards.Seen) continue;
                        if (model.Actors.Count + 1 + PenguinsPerScene + OilPerScene + FakesPerScene >= SceneValidator.MaxObjects) { log.Add($"scene {scene}: WARNING: no room for the {RaceTrackGuards.KindName((int)g[0], 1)} at ({gx:0.#}, {gz:0.#})"); continue; }
                        var look = RaceTrackGuards.Of((int)g[0], (int)g[1]);
                        var guard = mushroomTemplate.Clone();
                        guard.Entity = look.Entity; guard.Body = look.Drawn; guard.Anim = look.Idle;   // (twice the size: RaceTrackGuards.Install; a torturer's his own)
                        guard.Flags = OpponentFlags; guard.Move = 0; guard.Life = new byte[] { 0 }; guard.Track = new byte[] { 0 };
                        guard.X = Math.Clamp((int)Math.Round((gx - model.CubeX * 64) * 512), 0, 32767); guard.Z = Math.Clamp((int)Math.Round((gz - model.CubeY * 64) * 512), 0, 32767);
                        guard.Y = -20000; guard.Beta = 0;
                        var actor = SceneOps.AddActor(model, guard);
                        var home = (int)Math.Floor(gx / 64) == model.CubeX && (int)Math.Floor(gz / 64) == model.CubeY;
                        t.Guards.Add(new[] { scene, actor, gi, home ? 1 : 0 });
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
                        if (model.Actors.Count + PenguinsPerScene + OilPerScene + FakesPerScene >= SceneValidator.MaxObjects) { mushroomsLeftOut++; continue; }
                        // (out of sight, as the penguins and the slicks are, its height on the road kept with it: the race-track mode stands
                        // it there. A scene can be drawn with another island file than its track's -- Celebration Island's 95 is the statue's
                        // track's, and before the statue rises the game draws it without the raised road, as the editor draws the island's
                        // other file with it; Citadel Island's scenes carry both weathers' tracks -- and a mushroom standing on a road that
                        // isn't there hung in the air)
                        var mushroom = mushroomTemplate.Clone();
                        mushroom.Body = report.Pumpkins ? LBAAssembler.Terrain.Temple.TempleHalloween.Pumpkin : RaceTrackOil.BigMushroom;      // (the game's mushroom half again its size: RaceTrackOil; Halloween's a mini pumpkin)
                        mushroom.Flags = OpponentFlags; mushroom.Move = 0; mushroom.Life = new byte[] { 0 }; mushroom.Track = new byte[] { 0 };
                        mushroom.X = (int)Math.Round((x - model.CubeX * 64) * 512); mushroom.Z = (int)Math.Round((z - model.CubeY * 64) * 512);
                        mushroom.Y = -20000; mushroom.Beta = 0;
                        // (and where it stands on the island: the race-track mode draws it from the next cube too, DrawFarMushrooms)
                        t.Mushrooms.Add(new[] { scene, SceneOps.AddActor(model, mushroom), (int)Math.Round(y), (int)Math.Round(x * 512), (int)Math.Round(z * 512) });
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
                    // the fake mushrooms: copies of the mushroom itself -- a fake looks just as a real one does
                    for (var k = 0; k < FakesPerScene; k++)
                    {
                        var fake = mushroomTemplate.Clone();
                        fake.Body = report.Pumpkins ? LBAAssembler.Terrain.Temple.TempleHalloween.Pumpkin : RaceTrackOil.BigMushroom;          // (as big as the real ones; Halloween's pumpkins)
                        fake.Flags = OpponentFlags; fake.Move = 0; fake.Life = new byte[] { 0 }; fake.Track = new byte[] { 0 };
                        fake.X = IslandFile.CubeSize / 2; fake.Z = IslandFile.CubeSize / 2; fake.Y = -20000; fake.Beta = 0;
                        t.Fakes.Add(new[] { scene, SceneOps.AddActor(model, fake) });
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
            if (t.Mushrooms.Count > 0) log.Add($"the power-ups: {t.Mushrooms.Count} mushrooms in {t.MushroomRows} rows across the road (the game's own, scene {MushroomScene}), a row about every {t.LapTime / Math.Max(1, t.MushroomRows):0.0} s of driving (a lap {t.LapTime:0.0} s), {PenguinsPerScene} nitro penguins and {OilPerScene} oil slicks in each of {t.Penguins.Count / PenguinsPerScene} scenes{whose}");
            if (mushroomsLeftOut > 0) log.Add($"{mushroomsLeftOut} mushrooms left out: their scenes had no room for more actors");
            for (var k = 0; k < t.Drivers.Count; k++)
                if (t.Cars[k].Count > 0) log.Add($"{t.Drivers[k].Name}: a copy of {(t.Drivers[k].Bike ? "the motorbike Rabbibunny" : t.Drivers[k].Cast >= 0 && castCars.TryGetValue(t.Drivers[k].Cast, out var c) ? $"{c.Name} (car {c.Number}: entity {c.Entity}'s body {c.Generic})" : $"the car (the racer's body {t.Drivers[k].Body})")} in {t.Cars[k].Count} scenes{whose}");
            if (t.Grid.Count > 0) log.Add($"the grid: {t.Grid.Count} spots, pole {t.Options.GridFirst} cells behind the start line, each {t.Options.GridStep} behind the last, {t.Options.GridSide} either side of the middle{whose}");
            if (t.Pits.Count > 0) log.Add($"the pits: {t.Pits.Count} spots in the pit lane, where the opponents wait while the player qualifies{whose}");
        }
        var main = tracks[0];
        var off = doorsOff.Count > 0 ? doorsOff : null;
        // (a point as "p<number>" in the actor's place: RACEMOD.CPP actor_at=)
        var putAside = actorsAt.Count + pointsAt.Count > 0
            ? actorsAt.Select(a => $"{a.File} {a.Scene} {a.Actor} {a.X} {a.Y} {a.Z}").Concat(pointsAt.Distinct().Select(q => $"{q.File} {q.Scene} p{q.Point} {q.X} {q.Y} {q.Z}")).ToList()
            : null;
        var lifts = deckLifts.Count > 0 ? deckLifts : null;
        return main.Result(log, changes.Count, removed, tracks.Count > 1 ? tracks[1].Result(log, changes.Count, removed) with { DoorsOff = off, ActorsAt = putAside, DeckLifts = lifts } : null) with { DoorsOff = off, ActorsAt = putAside, DeckLifts = lifts };
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
    // 112, BODY.HQR 171, which gives a bonus when Twinsen walks into it), copied onto the road in a row across it every few seconds of
    // driving round the lap (RaceTrackOptions.ItemSpacing, MushroomSpots) -- three side by side where the road is wide enough, else two (or
    // one), MushroomGap apart and MushroomEdge in from its edges -- not on or near a jump, a loop or a carried jump; the race-track mode
    // hides a power-up in each (a car takes one from a row: the others it passes give way). And the nitro penguin a car drops: the shop's
    // (scene 14, actor 5: entity 46).
    public const int MushroomScene = 45, MushroomActor = 7, PenguinScene = 14, PenguinActor = 5;
    // Otringal's machine gun (entity 200: its body faces +z, its muzzle 214 forward) and the Franco who mans it (entity 201, its generic
    // animation 474 sitting at the gun, 1,072 behind the gun's middle as the game has him: scene 88's points 18 and 19)
    public const int GunEntity = 200, FrancoEntity = 201, FrancoAtTheGun = 474;
    private const double FrancoBehind = 1072;
    // (the oil slicks one scene can show at once: RACEMOD.CPP keeps six on the whole lap)
    private const int OilPerScene = 5, PenguinsPerScene = 3, FakesPerScene = 3;   // (oil 3 until 2026-10-07: the refinery's drips lie in slicks too)
    // (MushroomFirst: the first row's distance after the start line by the cells rule, RaceTrackOptions.ItemSpacingCells)
    private const double MushroomFirst = 30, MushroomClear = 14;
    // (the gap is more than the engine's reach for taking one, RACEMOD.CPP RACE_MUSHROOM_REACH: 2 cells; a car down the middle of one takes
    // only that one)
    private const double MushroomGap = 2.4, MushroomEdge = 1.0;
    // (a wide road's row spread evenly across it, its mushrooms at least MushroomSpread apart, MushroomMost at the most -- the Temple of Bu's
    // widened road had its three in the middle, 2.4 apart, 2026-10-20: "spread them out evenly across the track")
    private const double MushroomSpread = 2.6;
    private const int MushroomMost = 5;
    // The car the rows' driving time is reckoned for: the standalone game's (and the stress test's base car, tools/RaceTrackStress) -- top
    // gear 110 km/h, steering 200 %, half a turn a second -- round each bend of the first opponent's line as fast as it steers and no faster
    // than its top gear, a vertical loop's ring at ItemTimingLoop (60 km/h). (Its laps come out within 17 % of the engine's planned laps
    // for that car, which brake into the bends and pull away after them -- the old moon's 26 % short, its loops' rings slower -- 2026-10-17.)
    private const double ItemTimingTop = 110 / 3.6 * 512, ItemTimingTurn = Math.PI, ItemTimingLoop = 60 / 3.6 * 512;

    // How many mushrooms a row across a road this wide (half its width, cells) has, and how far apart.
    internal static (int Count, double Gap) MushroomRow(double half)
    {
        var room = half - MushroomEdge;
        if (room < 2.0) { var few = room >= 0.9 ? 2 : 1; return (few, few > 1 ? Math.Min(MushroomGap, 2 * room / (few - 1)) : 0); }
        var n = Math.Clamp((int)Math.Floor(2 * room / MushroomSpread) + 1, 3, MushroomMost);
        return (n, 2 * room / (n - 1));
    }

    // Where the power-ups' rows stand round the lap. RaceTrackOptions.ItemSpacing seconds of driving apart: the lap's driving time (from the
    // start line, ItemTimingTop/ItemTimingTurn's car) cut into the whole number of even stretches nearest to it, at least one, and a row in
    // the middle of each -- or, where the middle is on or near a jump, a loop or a carried jump, at the clear point of the stretch nearest
    // it (none clear: no row). With RaceTrackOptions.ItemSpacingCells the rule until 2026-10-17: a row every that many cells from
    // MushroomFirst after the start line, one that would stand on a jump left out. `rows`: how many rows there are, `lapTime`: the lap's
    // driving time (s).
    private static List<(double X, double Z, double Y)> MushroomSpots(RaceTrackReport report, RaceTrackOptions options, out int rows, out double lapTime)
    {
        var spots = new List<(double, double, double)>();
        rows = 0; lapTime = 0;
        var raisedHalf = options.RaisedHalfWidth;
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
        bool Free(int b) => !(report.LapArc is { } arc && b < arc.Length && arc[b]) && Clear(xs[b], zs[b]);
        // (the road's half width at a point of the lap: the asphalt's, or a raised road's to its rail)
        var road = report.Roads.FirstOrDefault();
        double Half(int i)
        {
            if (road is null || road.Count != n) return road?.AsphaltHalf ?? 3.5;
            if (road.Raised is { } up && up[i]) return road.RaisedHalfs is { } halfs && i < halfs.Length ? halfs[i] : raisedHalf;
            return road.AsphaltHalf;
        }
        // the row: across the road (the lap's way at b, from a, turned a quarter), its middle on the road's
        IEnumerable<(double X, double Z)> RowAt(int a, int b)
        {
            double dx = xs[b] - xs[a], dz = zs[b] - zs[a], d = Math.Sqrt(dx * dx + dz * dz) + 1e-9;
            var (count, gap) = MushroomRow(Half(b));
            for (var j = 0; j < count; j++)
            {
                var side = (j - (count - 1) / 2.0) * gap;
                yield return (xs[b] - dz / d * side, zs[b] + dx / d * side);
            }
        }
        void Row(int a, int b)
        {
            foreach (var (x, z) in RowAt(a, b)) spots.Add((x, z, ys[b]));
        }
        // (and, where the plan asks, every one of the row options.ItemCubeMargin from its cube's edges: one actor's scene is its cube's)
        bool OffEdges(int a, int b)
        {
            if (options.ItemCubeMargin <= 0) return true;
            foreach (var (x, z) in RowAt(a, b))
            {
                double ex = x - Math.Floor(x / 64) * 64, ez = z - Math.Floor(z / 64) * 64;
                if (Math.Min(ex, 64 - ex) < options.ItemCubeMargin || Math.Min(ez, 64 - ez) < options.ItemCubeMargin) return false;
            }
            return true;
        }
        // how fast the car drives at a point of the lap (units a second): round the bend of the opponent's line there (its nearest point,
        // looked for a little way either side of the last one found)
        var path = report.RacePath;
        var cursor = path.Count > 0 ? Enumerable.Range(0, path.Count).MinBy(k => (path[k].X - xs[i0]) * (path[k].X - xs[i0]) + (path[k].Z - zs[i0]) * (path[k].Z - zs[i0])) : 0;
        double Speed(int i)
        {
            if (path.Count == 0) return ItemTimingTop;
            var best = cursor; var bd = double.MaxValue;
            for (var k = -8; k <= 24; k++)
            {
                var m = ((cursor + k) % path.Count + path.Count) % path.Count;
                var d2 = (path[m].X - xs[i]) * (path[m].X - xs[i]) + (path[m].Z - zs[i]) * (path[m].Z - zs[i]);
                if (d2 < bd) { bd = d2; best = m; }
            }
            cursor = best;
            return Math.Clamp(ItemTimingTurn * path[best].Radius, 400, ItemTimingTop);
        }
        // the lap's points in driving order from the start line: the one before, how far along the lap (cells) and the driving time (s)
        var steps = new List<(int A, int B, double Along, double Time)>();
        double along = 0, time = 0;
        for (var step = 1; step < n; step++)
        {
            if (report.Open && (i0 + way * step < 0 || i0 + way * step >= n)) break;
            int a = ((i0 + way * (step - 1)) % n + n) % n, b = ((i0 + way * step) % n + n) % n;
            var seg = Math.Sqrt((xs[b] - xs[a]) * (xs[b] - xs[a]) + (zs[b] - zs[a]) * (zs[b] - zs[a]));
            along += seg; time += seg * 512 / Speed(b);
            // (and round a vertical loop's ring there)
            foreach (var l in report.Loops) if (l.Point == b) time += 2 * Math.PI * l.Radius * 512 / ItemTimingLoop;
            steps.Add((a, b, along, time));
        }
        if (steps.Count == 0) return spots;
        // (round to the start line again)
        var end = steps[^1].B;
        lapTime = time + (report.Open ? 0 : Math.Sqrt((xs[i0] - xs[end]) * (xs[i0] - xs[end]) + (zs[i0] - zs[end]) * (zs[i0] - zs[end])) * 512 / Speed(i0));
        if (options.ItemSpacingCells is { } cells)
        {
            var next = MushroomFirst;
            foreach (var (a, b, at, _) in steps)
            {
                if (at < next) continue;
                if (at > report.Length - 20) break;
                next = at + cells;
                if (!Free(b) || !OffEdges(a, b)) continue;
                Row(a, b);
                rows++;
            }
            return spots;
        }
        var stretches = Math.Max(1, (int)Math.Round(lapTime / Math.Max(0.1, options.ItemSpacing)));
        var every = lapTime / stretches;
        // (each stretch's row at the clear point nearest its middle -- off the start line's first MushroomFirst cells and the lap's last 20,
        // and at least half a stretch after the row before)
        var last = double.NegativeInfinity;
        for (var r = 0; r < stretches; r++)
        {
            var due = (r + 0.5) * every;
            var pick = -1;
            for (var k = 0; k < steps.Count; k++)
            {
                var (a, b, at, t) = steps[k];
                if (t < r * every || t >= (r + 1) * every || t < last + every / 2 || at < MushroomFirst || at > report.Length - 20 || !Free(b) || !OffEdges(a, b)) continue;
                if (pick < 0 || Math.Abs(t - due) < Math.Abs(steps[pick].Time - due)) pick = k;
            }
            if (pick < 0) continue;
            Row(steps[pick].A, steps[pick].B);
            last = steps[pick].Time;
            rows++;
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
        // (the Emperor's palace's scenes are made with its island, PalaceTrack.Install, before the track is built; the Temple of Bú's too,
        // TempleTrack.Install)
        if (island == RaceTrackIsland.Palace || island == RaceTrackIsland.Temple) return null;
        // (an island the build makes that races in a copy of a game's scene -- Island CX's, CXTOWER -- gets that copy, as below)
        if (island.Created && island.CopiesScene is null && island.CopiesScenes is null)
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
            // (a copy past the table's end has no place on the holomap -- Island CX's, 300: drawn on none, NoHolomap)
            if (island.NoHolomap && arrows.Length < mine + size) continue;
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
    // The island's own actor of `entity`, as its scenes have it before the build (the store's records: the build saves its scenes at the
    // end), a copy -- or null
    private static SceneActorModel? StartPersonActor(SceneStore store, RaceTrackIsland island, int entity)
    {
        foreach (var scene in island.Scenes)
        {
            if (!store.SceneExists(scene)) continue;
            SceneModel model;
            try { model = store.Load(scene); } catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { continue; }
            if (model.Island != island.IslandByte) continue;
            if (model.Actors.Skip(1).FirstOrDefault(a => a.Entity == entity) is { } found) return found.Clone();
        }
        return null;
    }

    private static void ShiftOffRoad(SceneActorModel actor, SceneModel model, int index, Func<double, double, double> distanceToRoad,
        Func<double, double, double, double, bool> inTheWay,
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
        // (only what is in the way of the cars, at its own height: Citadel Island's lifts down into the sewers, by the town circuit's start
        // line and over the school, stand under the raised road or beside it -- moved 12 and 20 cells away, their doors stayed where they
        // were, and Twinsen walking there went down, 2026-10-27)
        if (distanceToRoad(fromX, fromZ) > clear || !inTheWay(fromX, fromZ, actor.Y, actor.Y + 1500)) { PointsOffRoad(); return; }
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
            // (and the scene's points its life script stands it on -- pos_point -- where they are where it stood, moved with it: Otringal's
            // machine guns, moved aside, were put back on the road by their scripts every frame, 2026-10-09)
            foreach (var point in PosPoints(actor, index).Where(p => p < model.TrackPoints.Count))
            {
                var p = model.TrackPoints[point];
                if (Math.Abs(p.X - actor.X) > 3 * 512 || Math.Abs(p.Z - actor.Z) > 3 * 512) continue;
                var (px, pz) = (p.X + x - actor.X, p.Z + z - actor.Z);
                var py = ground is null ? p.Y : (int)Math.Round(ground(ox + px / 512.0, oz + pz / 512.0) + (p.Y - actor.Y + offset));
                model.TrackPoints[point] = p with { X = px, Y = py, Z = pz };
                log.Add($"scene {scene}: ... and point {point}, where its script stands it, with it");
            }
            actor.X = x; actor.Z = z; actor.Y = y;
            PointsOffRoad();
            return;
        }
        log.Add($"scene {scene}: WARNING: actor {index} (entity {actor.Entity}) stands on the road and no clear place was found near it");

        // (the points its life script stands it on that are on the road themselves -- wherever it stood: Otringal's spaceport man,
        // kept for Twinsen's cutscenes, stood beside the road and his script put him on it, 2026-10-09 -- each moved to the nearest clear
        // place)
        void PointsOffRoad()
        {
            foreach (var point in PosPoints(actor, index).Where(p => p < model.TrackPoints.Count))
            {
                var p = model.TrackPoints[point];
                if (distanceToRoad(ox + p.X / 512.0, oz + p.Z / 512.0) > clear || !inTheWay(ox + p.X / 512.0, oz + p.Z / 512.0, p.Y, p.Y + 1500)) continue;
                var moved = false;
                for (var out_ = 1.0; out_ <= 24 && !moved; out_ += 0.5)
                for (var turn = 0; turn < 24 && !moved; turn++)
                {
                    var angle = turn * Math.PI / 12;
                    int x = (int)Math.Round(p.X + Math.Cos(angle) * out_ * 512), z = (int)Math.Round(p.Z + Math.Sin(angle) * out_ * 512);
                    if (x < 512 || z < 512 || x > IslandFile.CubeSize - 512 || z > IslandFile.CubeSize - 512) continue;
                    if (distanceToRoad(ox + x / 512.0, oz + z / 512.0) <= clear) continue;
                    if (wasGround is not null && !wasGround(ox + x / 512.0, oz + z / 512.0)) continue;
                    var b = groundBefore is null ? 0 : groundBefore(ox + p.X / 512.0, oz + p.Z / 512.0);
                    var y = ground is null ? p.Y : (int)Math.Round(ground(ox + x / 512.0, oz + z / 512.0) + (p.Y - b));
                    model.TrackPoints[point] = p with { X = x, Y = y, Z = z };
                    log.Add($"scene {scene}: point {point}, where actor {index}'s (entity {actor.Entity}) script stands it, was on the road and is moved {out_:0.#} cells aside");
                    moved = true;
                }
            }
        }
    }

    // The one to beat of a story's start, as the game has him (RaceTrackIsland.ChaseDriver): its scene's actor; else any of his entity's,
    // given his own body, animation and an ordinary actor's flags (a folder whose scenes another island's track has rebuilt)
    private static SceneActorModel? ChaseDriverActor(SceneStore originals, (int Scene, int Actor, int Entity) cd)
    {
        SceneActorModel? Of(int scene, Func<SceneActorModel, bool> want)
        {
            try { return originals.SceneExists(scene) ? originals.Load(scene).Actors.Skip(1).FirstOrDefault(want) : null; }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { return null; }
        }
        try
        {
            if (originals.SceneExists(cd.Scene) && originals.Load(cd.Scene).Actors.ElementAtOrDefault(cd.Actor) is { } own && own.Entity == cd.Entity) return own;
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { }
        for (var scene = 0; scene < originals.SceneCount; scene++)
            if (Of(scene, a => a.Entity == cd.Entity) is { } any)
            {
                var copy = any.Clone();
                copy.Body = 0; copy.Anim = 0; copy.Flags = 0x1807;
                return copy;
            }
        return null;
    }

    // Whether an actor is off the road (KeepOffRoadActors): it stands further than `reach` cells from the road's middle, and so does every
    // point its scripts send or put it to -- its move script's goto/pos points, its life script's pos_point -- a townsman who walks across
    // the road, or Raph's fiancee wandering behind the lighthouse on the town circuit, is on it.
    private static bool OffRoad(SceneModel model, int index, Func<double, double, double, double, bool> inTheWay, out string? onIt)
    {
        var actor = model.Actors[index];
        double ox = model.CubeX * 64.0, oz = model.CubeY * 64.0;
        // (as tall as a person, from where it stands)
        bool Clear(int x, int y, int z) => !inTheWay(ox + x / 512.0, oz + z / 512.0, y, y + 1500);
        onIt = null;
        if (!Clear(actor.X, actor.Y, actor.Z)) { onIt = $"it stands on the road, at ({actor.X},{actor.Y},{actor.Z})"; return false; }
        if (ScriptPoints(actor, index) is not { } points) { onIt = "its move script can't be read"; return false; }
        foreach (var p in points.Order())
            if (p >= 0 && p < model.TrackPoints.Count && !Clear(model.TrackPoints[p].X, model.TrackPoints[p].Y, model.TrackPoints[p].Z))
            {
                onIt = $"its scripts take it to point {p}, on the road at ({model.TrackPoints[p].X},{model.TrackPoints[p].Y},{model.TrackPoints[p].Z})";
                return false;
            }
        return true;
    }

    // The points an actor's scripts send or put it to: its move script's goto/pos points, its life script's pos_point (null: unreadable)
    private static HashSet<int>? ScriptPoints(SceneActorModel actor, int index)
    {
        var points = new HashSet<int>(PosPoints(actor, index));
        try
        {
            foreach (var i in LbaScript.Bytecode.DecodeTrack(actor.Track))
                if (i.Op is 4 or 8 or 12 or 15 && i.A.Length > 0) points.Add((int)i.A[0]);
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or InvalidOperationException) { return null; }
        return points;
    }

    // The points of an actor's way that are in the way of the cars moved off the road, each to the nearest clear place on the ground (its
    // own cube, not the sea; at its height over the ground there): false where one has nowhere to go
    private static bool PointsOffRoad(SceneModel model, int index, Func<double, double, double, double, bool> inTheWay, Func<double, double, double>? groundBefore,
        Func<double, double, double>? ground, Func<double, double, bool>? wasGround, int scene, List<string> log)
    {
        var actor = model.Actors[index];
        double ox = model.CubeX * 64.0, oz = model.CubeY * 64.0;
        if (ScriptPoints(actor, index) is not { } points) return false;
        var moves = new List<(int Point, SceneTrackPoint To, double Out)>();
        foreach (var point in points.Where(p => p >= 0 && p < model.TrackPoints.Count).Order())
        {
            var p = model.TrackPoints[point];
            if (!inTheWay(ox + p.X / 512.0, oz + p.Z / 512.0, p.Y, p.Y + 1500)) continue;
            SceneTrackPoint? to = null;
            var by = 0.0;
            for (var out_ = 1.0; out_ <= 24 && to is null; out_ += 0.5)
            for (var turn = 0; turn < 24 && to is null; turn++)
            {
                var angle = turn * Math.PI / 12;
                int x = (int)Math.Round(p.X + Math.Cos(angle) * out_ * 512), z = (int)Math.Round(p.Z + Math.Sin(angle) * out_ * 512);
                if (x < 512 || z < 512 || x > IslandFile.CubeSize - 512 || z > IslandFile.CubeSize - 512) continue;
                double cx = ox + x / 512.0, cz = oz + z / 512.0;
                if (wasGround is not null && !wasGround(cx, cz)) continue;
                var b = groundBefore is null ? 0 : groundBefore(ox + p.X / 512.0, oz + p.Z / 512.0);
                var y = ground is null ? p.Y : (int)Math.Round(ground(cx, cz) + (p.Y - b));
                if (inTheWay(cx, cz, y, y + 1500)) continue;
                to = p with { X = x, Y = y, Z = z };
                by = out_;
            }
            if (to is null) return false;
            moves.Add((point, to.Value, by));
        }
        foreach (var (point, to, by) in moves)
        {
            model.TrackPoints[point] = to;
            log.Add($"scene {scene}: point {point} of actor {index}'s (entity {actor.Entity}) way was on the road and is moved {by:0.#} cells aside");
        }
        return true;
    }

    // Whether an actor's life script switches the scene's doors on or off (SET_CHANGE_CUBE)
    private static bool SwitchesDoors(SceneActorModel actor, int index)
    {
        try { return LbaScript.LifeText.Decompile(actor.Life, index, LbaScript.NoSymbols.Instance).Contains("set_change_cube(", StringComparison.OrdinalIgnoreCase); }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or InvalidOperationException) { return false; }
    }

    // The actors an actor's life script refers to by number -- what it asks of them or does with them (l_track_obj(6), set_dir_obj(3, ...)) --
    // other than the hero (0); not those it only kills (kill_obj: Citadel Island's townsmen on the road, 2026-10-27)
    private static IEnumerable<int> ActorsReferred(SceneActorModel actor, int index)
    {
        string text;
        try { text = LbaScript.LifeText.Decompile(actor.Life, index, LbaScript.NoSymbols.Instance); }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or InvalidOperationException) { return Array.Empty<int>(); }
        return System.Text.RegularExpressions.Regex.Matches(text, @"\b(?!kill_)\w+_obj\((\d+)").Select(m => int.Parse(m.Groups[1].Value)).Where(n => n > 0).Distinct().ToList();
    }

    // The scene's points an actor's life script puts it on (pos_point), read from the script's text.
    private static IEnumerable<int> PosPoints(SceneActorModel actor, int index)
    {
        string text;
        try { text = LbaScript.LifeText.Decompile(actor.Life, index, LbaScript.NoSymbols.Instance); }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or InvalidOperationException) { return Array.Empty<int>(); }
        return System.Text.RegularExpressions.Regex.Matches(text, @"pos_point\((\d+)\)").Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToList();
    }

    // (`raiseOnly`: an island with a file for each weather sharing the scenes -- what the one file's road lowered is the other's ground still,
    // and lowered there it stood buried in it: Citadel Island's cow in its pasture, the town circuit's cut through it, under the storm's
    // ground. Raised only: in the file whose ground is lower it falls onto it)
    private static void Reseat(SceneModel model, Func<double, double, double> before, Func<double, double, double> after, Func<double, double, bool> wasGround, int scene, List<string> log,
        bool raiseOnly = false, Func<double, double, bool>? keep = null)
    {
        var ox = model.CubeX * 64.0; var oz = model.CubeY * 64.0;
        int Rise(int x, int y, int z)
        {
            var cx = ox + x / 512.0; var cz = oz + z / 512.0;
            if (!wasGround(cx, cz) || keep?.Invoke(cx, cz) == true) return 0;
            var b = before(cx, cz); var a = after(cx, cz);
            return Math.Abs(a - b) > 100 && Math.Abs(y - b) < 400 ? (int)Math.Round(a - b) : 0;
        }
        var moved = 0;
        for (var i = 0; i < model.TrackPoints.Count; i++)
        {
            var p = model.TrackPoints[i]; var d = Rise(p.X, p.Y, p.Z);
            if (d > 0 || d < 0 && !raiseOnly) { model.TrackPoints[i] = p with { Y = p.Y + d }; moved++; }
        }
        foreach (var a in model.Actors)
        {
            if ((a.Flags & Fallable) == 0 && a != model.Hero) continue;
            var d = Rise(a.X, a.Y, a.Z);
            if (d > 0 || d < 0 && !raiseOnly) { a.Y += d; moved++; }
        }
        if (moved > 0) log.Add($"scene {scene}: {moved} track points and actors moved up or down with the reshaped ground");
    }

    // Scene 67, the start: the buggy stands on the start line whenever the scene starts on foot (INIT_BUGGY 2 puts the game's one
    // buggy at the actor's own place; the island's scenes run INIT_BUGGY 0, which only shows it where it already is) -- but not
    // when Twinsen drives back into the scene on the next lap: forcing it then parked a second, solid buggy on the start line
    // for the car to crash into.
    // `park`: in the other weather (game variable 206 is When: RACEMOD.CPP RaceMod_CitadelWeather), the car stands there instead (a track
    // point of its own).
    // (`home`: the story's car -- RaceTrackIsland.CarHome: on the start line only when the race file starts the game there, game variable
    // GridCarVar 1; otherwise made at home the first time, in home's scene, and anywhere else shown where Twinsen left it)
    public const int GridCarVar = 211;
    private static SceneModel? StartBuggyScript(SceneModel model, int scene, int buggy, List<string> log, (int X, int Y, int Z, int Beta, int When)? park = null,
        (int X, int Y, int Z, int Beta, bool Here)? home = null)
    {
        try
        {
            var parkPoint = -1;
            if (park is { } pk) { parkPoint = model.TrackPoints.Count; model.TrackPoints.Add(new SceneTrackPoint(pk.X, pk.Y, pk.Z)); }
            var homePoint = -1;
            if (home is { Here: true } hm) { homePoint = model.TrackPoints.Count; model.TrackPoints.Add(new SceneTrackPoint(hm.X, hm.Y, hm.Z)); }
            var scripts = SceneScripts.Load(SceneSerializer.Write(model), scene);
            var text = scripts.GetText(buggy, ScriptKind.Life);
            // (Polar Island's buggy has INIT_BUGGY 1, which makes the car the first time: Polar.PolarScenes)
            var pattern = new System.Text.RegularExpressions.Regex(@"init_buggy\(([01])\);\s*if \(12 == comportement_hero\(\)\)\s*\{\s*set_comportement\(comportement_2\);\s*\}\s*else\s*\{\s*set_comportement\(comportement_1\);\s*\}");
            if (!pattern.IsMatch(text)) { log.Add($"scene {scene}: the buggy's script isn't the expected one; it is left to show the buggy where it is"); return null; }
            var parked = park is { } p ? $"            if ({p.When} == var_game(206))\n            {{\n                pos_point({parkPoint});\n                beta({p.Beta});\n            }}\n" : "";
            var story = home is { } h
                ? $"        else if (0 == var_game({GridCarVar}))\n        {{\n" +
                  (h.Here ? $"            pos_point({homePoint});\n            beta({h.Beta});\n            init_buggy(1);\n" : "            init_buggy(0);\n") +
                  "            set_comportement(comportement_1);\n        }\n"
                : "";
            text = pattern.Replace(text, "if (12 == comportement_hero())\n        {\n            init_buggy($1);\n            set_comportement(comportement_2);\n        }\n" + story + "        else\n        {\n" + parked +
                "            init_buggy(2);\n            set_comportement(comportement_1);\n        }", 1);
            // (and in only with the action key: the script also put Twinsen in when he stood on an actor -- carry_obj_by, the car's number
            // in the game's scene, another actor's once the build has taken some away -- the user, 2026-10-27: "Twinsen is currently getting
            // automatically put into his car on the start/finish line on Citadel if he's next to it")
            text = System.Text.RegularExpressions.Regex.Replace(text, @"if \(\d+ == carry_obj_by\(0\)\)\s*\{\s*set_dir_obj\(0, MOVE_BUGGY_MANUAL\);\s*set_comportement\(comportement_2\);\s*\}", "");
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
