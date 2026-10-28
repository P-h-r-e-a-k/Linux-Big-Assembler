using System.IO;
using System.Text.Json;

namespace LBAAssembler.Terrain;

// Builds an island's race track (RaceTrackIsland) into an LBA2 game folder, and puts the folder back. The files it changes are kept beside the originals as
// *.before-racetrack the first time; every later build starts from those copies, so building again never piles a track on a track, and Restore
// puts them back. DESERT.ILE, DESERT.OBL and SCENE.HQR always change; ANIM.HQR and RESS.HQR for a jump (its flight, RaceTrackJumpAnim), BODY.HQR
// and RESS.HQR for Baldino's car (RaceTrackBaldinoCar); they are kept from the first build on too (a folder built before one of them changed keeps
// it from its next build on, while it is still the original), so every build starts from the originals. RACETRACK.JSON, beside them, tells Play
// where the start line, the checkpoints and the opponents' lines are (what the engine's race-track mode uses).
// A build can carry the tracks of several islands at once (each island's scenes, ground and decor bodies are its own; what they share --
// SCENE.HQR's other scenes, the cars, the holomap -- each adds to): they are built one after another from the originals, and
// RACETRACK.JSON has the first as its own record and the others under Others. Play races the one of the island the editor has open
// (RaceFor).
internal static class RaceTrackService
{
    public const string BackupSuffix = ".before-racetrack";

    // The doors whose decor the build took away, every piece of it (RaceTrackReport.LostDoors)
    // (the pieces it had: the build's own new ones there -- the road's deck over a lift -- are no door)
    // ... or buried, every piece left of it under the ground the build raised: Citadel Island's sewer lift by the storm track's start line,
    // under the town circuit's bridge ramp in the fine weather's file
    private static void NoteLostDoors(IslandFile island, List<(double X0, double Z0, double X1, double Z1, double Y0, double Y1)> doors, List<HashSet<IslandDecor>> before, RaceTrackReport report)
    {
        var at = new Dictionary<IslandDecor, (int X, int Z)>(ReferenceEqualityComparer.Instance);
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
            foreach (var d in cube.Decors) at[d] = (cx, cz);
        bool Standing(IslandDecor d) => at.TryGetValue(d, out var c)
            && !(IslandOps.Altitude(island, c.X * (double)IslandFile.CubeSize + (d.XMin + d.XMax) / 2.0, c.Z * (double)IslandFile.CubeSize + (d.ZMin + d.ZMax) / 2.0) is { } g && g > d.YMax + BuriedDoor);
        for (var i = 0; i < doors.Count; i++)
            if (before[i].Count > 0 && !before[i].Any(Standing))
            {
                report.LostDoors.Add((doors[i].X0, doors[i].Z0, doors[i].X1, doors[i].Z1));
                if (before[i].Any(at.ContainsKey)) report.BuriedDoors.Add((doors[i].X0, doors[i].Z0, doors[i].X1, doors[i].Z1));
            }
    }
    private const int BuriedDoor = 256;

    // The island's doors into inside scenes, as its scenes have them before any build (RaceTrackOptions.DoorBoxes): each of its outside
    // scenes' cube-change zones to a scene that isn't one of them, in island cells and heights
    public static List<(double X0, double Z0, double X1, double Z1, double Y0, double Y1)> DoorBoxes(string gameDirectory, RaceTrackIsland island)
    {
        var boxes = new List<(double, double, double, double, double, double)>();
        var store = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, gameDirectory);
        if (File.Exists(store.ScenePath + BackupSuffix)) store = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, gameDirectory, "SCENE.HQR" + BackupSuffix);
        for (var scene = island.FirstScene; scene <= island.LastScene; scene++)
        {
            LBAAssembler.Scenes.SceneModel model;
            try { if (!store.SceneExists(scene)) continue; model = store.Load(scene); }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { continue; }
            if (model.Island != island.IslandByte || model.CubeMode != 1) continue;
            foreach (var z in model.Zones)
                if (z.Type == 0 && !island.HasScene(z.Num))
                    boxes.Add((model.CubeX * 64 + z.X0 / 512.0, model.CubeY * 64 + z.Z0 / 512.0, model.CubeX * 64 + z.X1 / 512.0, model.CubeY * 64 + z.Z1 / 512.0, z.Y0, z.Y1));
        }
        return boxes;
    }
    // Whether a build installs the private special cars (the toy dump truck) as well: the editor's builds do; the race game's installer
    // turns it off, so the game it installs carries none of them.
    public static bool PrivateCars { get; set; } = true;
    // What every build changes, whichever island it is on; the island's own ground and decor bodies are added to these (RaceTrackIsland).
    public static readonly string[] Files = { "SCENE.HQR" };
    // (and the holomap's pictures and arrows, and the texts the story adds to)
    public static readonly string[] ExtraFiles = { "ANIM.HQR", "RESS.HQR", "BODY.HQR", RaceTrackHolomap.File, "TEXT.HQR", "OBJFIX.HQR" };
    public static string[] FilesFor(RaceTrackIsland island) => Files.Concat(island.KeptFiles).ToArray();
    public static string[] AllFiles => Files.Concat(ExtraFiles).Concat(RaceTrackIsland.All.SelectMany(i => i.KeptFiles)).Distinct().ToArray();
    public const string InfoFile = "RACETRACK.JSON";

    public sealed record BuildResult(bool Ok, string Summary, List<string> Log, RaceTrackReport? Report);
    // Where a build has got to (Build's `progress`, for RaceTrackWindow's bar): the part of the whole build done (0..1) and what it is doing.
    public readonly record struct BuildProgress(double Done, string Doing);

    // Where a lap is counted, in the terms the engine uses: the island cube the line is in, its ends in that cube's own world units, the way
    // a lap crosses it.
    // Y: the road's height at the line, for a lap that passes over itself (the line only counts for a car near that height).
    public sealed record StartLineInfo(int CubeX, int CubeZ, int X0, int Z0, int X1, int Z1, int DirX, int DirZ,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? Y = null);
    // Checkpoints: the same kind of lines, in the order a lap crosses them. Path: the opponent's line from the start line round the lap,
    // each point [x, z, y, speed, bend radius] in world units (x and z counted from the island's corner, 32768 to a cube). PathGrid: how many points
    // before the start line the opponent starts. Opponent: scene -> the index of that scene's copy of the racer. StartScene: the scene
    // the start line is in. Rivals: the other opponents (Baldino), each with a line, a grid and the scenes' copies of its car of its own.
    // Grid: the grid spots, pole first, each [cube x, cube z, x, y, z, turn] (the race-track mode lines the cars up on them), and Pits:
    // the spots in the pit lane the opponents wait on while the player qualifies. Twin: the track of the island's other-weather file when it
    // has one of its own (Citadel Island's town circuit, in CITABAU; the rest of the record is then CITADEL's, the storm's) -- Play races
    // the one the car setup's weather shows (Raced).
    public sealed record TrackInfo(string Crossing, StartLineInfo? StartLine, List<StartLineInfo>? Checkpoints = null, List<int[]>? Path = null,
        int PathGrid = 0, Dictionary<int, int>? Opponent = null, int StartScene = -1, List<RivalInfo>? Rivals = null, List<int[]>? Grid = null,
        List<int[]>? Pits = null, string Island = "Desert island", int StoryArrow = -1,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] TrackInfo? Twin = null,
        // Raised: a raised road's middle, point by point in lap order, [x, z, y, half width] in world units from the island's corner:
        // the race-track mode's floor there (RACEMOD.CPP).
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Raised = null,
        // Gravity: how much a raised road's grade changes a car's speed there (RaceTrackPlan.Gravity; a banked road's points carry a
        // fifth number, its banking in ten-thousandths).
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? Gravity = null,
        // RailCamera: the camera rides the raised road behind the car (RaceTrackPlan.RailCamera: cells behind, units up, cells ahead[, sight,
        // aim[, side]]).
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double[]? RailCamera = null,
        // Others: the tracks of the other islands built into the folder with this one, each a record of its own (with no Others).
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<TrackInfo>? Others = null,
        // Loops: the vertical loops (RaceTrackPlan.Loops), each [cube x, cube z, x, y, z (the ring's foot, cube-local), the lap's way there
        // (x and z, a thousand long), the ring's radius, how far across the car comes out (world units; negative: in on the right, out on
        // the left), the gap at its top (degrees), and the ring's band from its middle to its rails (world units: the car is steered across
        // it); a twelfth number, -1, when the camera watches the ring from the side the car comes out on].
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Loops = null,
        // JumpCameras: where the camera stands while the car flies a drop (RaceTrackBuilder.PlaceJumpCameras), each [the flight's generic
        // animation, cube x, cube z, x, y, z (cube-local)].
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? JumpCameras = null,
        // ArcJumps: the jumps the race-track mode carries the car over (RaceTrackPlan.ArcJumps), each [its ramp's foot, its lip, the
        // landing lip, the landing hill's foot] as places in Raised; CubeScenes: the island's outside scenes, each [cube x, cube z, scene],
        // for the cube changes such a flight makes itself.
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? ArcJumps = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? CubeScenes = null,
        // OpenAreas: where the cars drive where they like (RaceTrackPlan.OpenAreas), each [x0, z0, x1, z1, its floor's height], world units.
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? OpenAreas = null,
        // Drivers: who races (RaceDriver, the track's line-up), in the engine's order, each with its line and the scenes' copies of its car
        // (since 2026-10-03; a track built before has its racer in Path and Opponent and the others in Rivals).
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<DriverInfo>? Drivers = null,
        // Story: the story's texts the race-track mode needs (Citadel Island's: RaceTrackStory).
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] StoryInfo? Story = null,
        // Mushrooms, Penguins: the power-ups' mushrooms along the lap and the nitro penguin of each scene, each [scene, actor] -- a
        // mushroom [scene, actor, y], the height the race-track mode stands it at, out of sight until then (since 2026-10-05)
        // (RaceTrackScenes.MushroomSpots; RACEMOD.CPP's power-ups).
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Mushrooms = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Penguins = null,
        // Oil: the oil slicks of each scene, out of sight until a car drops oil, each [scene, actor] (RaceTrackOil)
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Oil = null,
        // OilIcon: the oil's model in OBJFIX.HQR, which the item box shows (RaceTrackOil.InstallIcon)
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? OilIcon = null,
        // SuperJetModel: the super jet-pack the car turns into while it drives it, in OBJFIX.HQR (RaceTrackSuperJet)
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? SuperJetModel = null,
        // TwinsenSmall: Twinsen's buggy at half its size, its generic body (RaceTrackSmallCars.HeroSmall), for an opponent's lightning
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? TwinsenSmall = null,
        // Dream: a sprint dreamt at the start of the game (Polar Island's: RaceTrackIsland.Dream, Polar.PolarDream), since 2026-10-06
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] DreamInfo? Dream = null,
        // Steam, Drips: the pipes over the road (RaceTrackPipes, the Island of the Francos' refinery): steam puffing from their tops, each
        // [x, y, z, every (ms)], and oil dripping onto the road, each [x, y, z, the road's y under it, every (ms)] -- world units from the
        // island's corner (RACEMOD.CPP steam= and drip=), since 2026-10-07
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Steam = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Drips = null,
        // Jets: steam jets across the road (RaceTrackPipes.PlaceJets), each [x, y, z (the road's middle), its half width, the reach across,
        // blowing (ms), not (ms), phase (ms), its way x and z (a thousand long)] (RACEMOD.CPP jet=)
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Jets = null,
        // Tunnels: tunnels over the raised road (RaceTrackTunnel), each [its first point, its last (the raised road's), its roof's height over
        // the deck, and (since 2026-10-16, when it has one) the height the camera's eye is kept under] (RACEMOD.CPP tunnel=), since 2026-10-08
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Tunnels = null,
        // Shuttles: the scenes' actors the race-track mode flies (RaceTrackIsland.Shuttles), each [scene, actor (the built scene's), the pad
        // it lands on: x, y (the road's surface there), z, cube-local] (RACEMOD.CPP shuttle=), since 2026-10-08
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Shuttles = null,
        // LavaBalls: the lava's balls of fire (RaceTrackPlan.LavaBalls), each [x, y, z of the source (island units), the raised road's first
        // and last point of the stretch they rain on, every (ms), phase (ms)] (RACEMOD.CPP lavaball=), since 2026-10-08
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? LavaBalls = null,
        // Fakes: the scenes' copies of the mushroom that show the fake mushrooms cars drop, each [scene, actor] (RACEMOD.CPP fake=), since
        // 2026-10-08
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Fakes = null,
        // LaserModels: the laser cars' bolts in OBJFIX.HQR, [green, red] (RaceTrackLasers; RACEMOD.CPP laser_models=), since 2026-10-08
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int[]? LaserModels = null,
        // Gunners: the machine guns on knolls (RaceTrackGunner; RACEMOD.CPP gunner=, gunner_target=), each [scene, the gun's actor, its
        // Franco's, the muzzle's x, y, z (island units), then x, z, y and radius of each place its bullets land], since 2026-10-09
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Gunners = null,
        // Roulette: the roulette wheel the lap runs through (RaceTrackRoulette; RACEMOD.CPP roulette=): its decors' cube x and z, its middle's
        // x and z (island units), how fast it turns (tenths of a degree a second), its ball's body (the island's) and radius, the place in
        // the raised road's file where the drop through its hole lands, its floor's height at the hole's edge, its rise a cell outwards
        // there and its steepening a cell a cell (hundredths), the hole's radius, the wall's and its outside's (units), how far its
        // underside is under its floor, then its turning bodies; since 2026-10-09
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int[]? Roulette = null,
        // GasMonsters: the gas monsters (RaceTrackGasMonster; RACEMOD.CPP gasmonster=), each [its scene, its actor there, its body, where it
        // rises x and z, where it bites x and z and the road's height there, its foot's height (island units), its first animation, then
        // the scene and actor of each other scene it is seen from], since 2026-10-09
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? GasMonsters = null,
        // RouletteBalls: the roulette wheel's balls (bodies Roulette's ball on; RACEMOD.CPP roulette_balls=), since 2026-10-10
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? RouletteBalls = null,
        // RoulettePockets: its pockets (RACEMOD.CPP roulette_pockets=); GoldIcon: the golden Gazogem's model in OBJFIX.HQR (gold_icon=), since 2026-10-10
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? RoulettePockets = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? GoldIcon = null,
        // LavaBallModel: the game's lava ball made bigger in OBJFIX.HQR (RaceTrackOil.InstallLavaBall; RACEMOD.CPP lava_ball_model=); LavaFalls:
        // across each lava fall's face, [its lip's x, y, z, the foot's x, y, z (island units), the way it looks] (RaceTrackLavaFall;
        // lavafall=); BoostPanels: each [its middle's x, y, z, the road's way x and z (thousandths), half its length, half its width]
        // (RACEMOD.CPP boostpanel=), since 2026-10-10
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? LavaBallModel = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? LavaFalls = null,
        // LavaGeysers: [x, y, z of the source, the raised road's first and last point of the stretch, every, phase (ms), balls a shower]
        // (RACEMOD.CPP lavageyser=), since 2026-10-11
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? LavaGeysers = null,
        // CrystalShards: as LavaGeysers (RACEMOD.CPP crystalshards=); CrystalShardModels: the shard's and its pieces' models in OBJFIX.HQR
        // (RaceTrackCrystals.InstallCrystalShards; crystal_shard_model=), since 2026-10-11
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? CrystalShards = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int[]? CrystalShardModels = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? BoostPanels = null,
        // Guards: the people in the cars' way (RaceTrackGuards; RACEMOD.CPP guard=), each [kind, its post x and z, the floor's height, the way
        // it faces x and z (thousandths), how far it steps out (island units), its animations -- standing, walking, knocked back -- its four
        // strikes (-1 none), then the scene and actor of each scene it has one in, its own first], since 2026-10-13
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Guards = null,
        // Teleports: each [the box a car is taken in by: x0, z0, x1, z1, y0, y1; where it comes out: x, y, z, the way it looks] in island units
        // (RACEMOD.CPP teleport=), since 2026-10-13; and the speed it comes out at the most, its passage's time (ms), where the plan gives them
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Teleports = null,
        // Frogs: the giant frogs the lap drives through (RaceTrackFrog.Line; RACEMOD.CPP frog=), since 2026-10-14
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Frogs = null,
        // Hunters: the island's own guns that fire at the cars outside (RaceTrackIsland.HuntingGuns; RACEMOD.CPP gunner= with gunner_hunt=), each
        // [scene, the gun's actor, the muzzle's x, y, z (island units), its reach (units)] -- Island CX's cannons, since 2026-10-15
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Hunters = null,
        // Flyers: the flying gas monsters (RaceTrackFlyers; RACEMOD.CPP flyer=), each [scene, its actor, its height, then its way's points x and
        // z (island units)] -- Island CX's hall, since 2026-10-15
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Flyers = null,
        // Spooks: the Halloween race's witches, ghosts and bats (Temple.TempleHalloween; RACEMOD.CPP spook=), each [its body, its flags, its
        // height, then its way's points x and z (island units)] -- the Temple of Bú's, since 2026-10-16
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Spooks = null,
        // RaisedLap: the lap's own points in Raised, the branches after them; RaisedBranches: each [its first and last point in Raised, the
        // lap's points there it leaves from and comes back onto] (RACEMOD.CPP raised_lap=, raised_branch=), since 2026-10-14
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? RaisedLap = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? RaisedBranches = null,
        // Dives: the places a witch dives at Twinsen's car from, each [the witch's body, x, z (island units), the road's height there]; Scares:
        // what jumps up out of the floor beside the road, each [its body, x, z, the floor's height, the way it faces]; Haunt: the ghost
        // power-up's ghost and its icon (BODY.HQR) (Temple.TempleHalloween; RACEMOD.CPP dive=, scare=, haunt=) -- the Temple of Bú's, since
        // 2026-10-17
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Dives = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Scares = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int[]? Haunt = null,
        // GiantWebs: the giant webs the lap drives through, each [its foot x, y, z (island units), the lap's way through it x and z (thousandths),
        // its posts' half width, its height, its hub's, the hole a car tears in it (half width, height), its spider's two bodies]; WebParts:
        // each [the web it is of, its kind, its body, its place across and up the web, its turn] (Temple.TempleHalloween; RACEMOD.CPP web=,
        // web_part=) -- the Temple of Bú's, since 2026-10-17
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? GiantWebs = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? WebParts = null,
        // Marshal: the flag marshal by the start line (Temple.TempleMarshal.Line; RACEMOD.CPP marshal=): her entity, its body and animations
        // (ready, the raise, the drop, the wave), where she stands (island units: x, z, the top of her plinth) and the way she faces -- the
        // Temple of Bú's, since 2026-10-17; then (2026-10-18) her plinth's body (marshal_plinth=) and, when she gives the start from the
        // road between the grid's columns, where she stands for it (x, z, the road's height, the way she faces: marshal_start=)
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int[]? Marshal = null,
        // Music: the race's own music, a file in the game folder (RACEMOD.CPP music=) -- the Temple of Bú's LBA1 temple music, which the build
        // makes from the player's LBA1 (Temple.TempleMusic), since 2026-10-18
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Music = null,
        // OnFoot: Twinsen may get out of his car and walk about while the race goes on (RACEMOD.CPP on_foot=) -- the Temple of Bú's secret
        // party (Temple.TempleParty), since 2026-10-19
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] bool? OnFoot = null,
        // RailOpens: where the raised road's rail lets a car go off its edge, down to the road under it (RACEMOD.CPP rail_open=), each [y0, y1,
        // then a polygon's corners x, z] in island units -- the Temple of Bú's spiral drop, since 2026-10-20
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? RailOpens = null,
        // MapRoads: the roads the race's mini-map draws that the race doesn't drive by -- the pit lane on the ground (RaceTrackBuilder's
        // "pit" road), each [x, z, x, z, ...] in world units (RACEMOD.CPP map_road=); since 2026-10-26, the user: "All tracks appear to be
        // missing the pitlane"
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? MapRoads = null,
        // DoorsOff: the doors shut while one of the island's files is shown, each "<file> <scene> <x0> <z0> <x1> <z1>" (RaceTrackScenes
        // Result.DoorsOff, RACEMOD.CPP door_off=); since 2026-10-27
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<string>? DoorsOff = null,
        // ActorsAt: actors put aside while one of the island's files is shown (RaceTrackScenes Result.ActorsAt, RACEMOD.CPP actor_at=)
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<string>? ActorsAt = null,
        // ChaseCar: the story's start's one to beat outside his car -- the car's empty body (BODY.HQR), its animation, and the entity of him
        // standing beside it (RaceTrackIsland.ChaseDriver, RACEMOD.CPP chase_car=); since 2026-10-27
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int[]? ChaseCar = null,
        // DeckLifts: the lifts on the raised road (RaceTrackScenes Result.DeckLifts, RACEMOD.CPP deck_lift=); since 2026-10-27
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<string>? DeckLifts = null);
    // A dreamt sprint: its finish line (a line as the start line is), the car's top speed for it (km/h: the setup's gears scaled to it),
    // the intro and the loss's line (texts of the island's), and where a win wakes Twinsen up -- the scene, the text of its island's its
    // actor says there.
    public sealed record DreamInfo(StartLineInfo Finish, int TopKmh, int IntroText, int LoseText, int WakeScene, int WakeText, int WakeActor,
        // (since 2026-10-06, later: the game variable a win sets, which scene 0's opening wakes Twinsen up in his bed by -- it says the
        // wake line itself then, and WakeText is -1; the Emperor's palace's since 2026-10-13: Celebration Island's statue risen)
        int WinVar = -1,
        // (since the island was made twice its size: how long after the finish line -- a jump's lip -- Twinsen wakes up, in mid-flight; 0, the
        // car stops at the line and he wakes up 3.5 s later)
        int WakeFlight = 0);
    public sealed record RivalInfo(string Name, List<int[]> Path, int Grid, Dictionary<int, int> Actors);
    // A driver: its name, its line ([x, z, y, speed, bend radius] as Path's), how many points before the start line it starts without a
    // grid, the scenes' copies of its car (none for a time to beat: Ghost), its character (Top, Grip: shares of the player's car's top
    // speed and cornering), whether it is the one to beat (Main), the motorbike, and its car's body (the racer entity's generic body).
    public sealed record DriverInfo(string Name, List<int[]> Path, int Grid, Dictionary<int, int> Actors, double Top, double Grip, bool Main, bool Bike, bool Ghost, int Body = -1)
    {
        // (a car that flies -- the Temple of Bú's witch on her broomstick --: how far it bobs over its line, RaceDriver.Hover; left out of
        // the file for every other driver)
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public int Hover { get; init; }
    }
    // The story's: the tired line everyone says until Twinsen sleeps (a text of Citadel Island's), and the holomap arrow to the town
    // circuit's start line.
    // (Seller: Celebration Island's lava lake, whose race the souvenir seller has to lose: RaceTrackStory.ApplyCelebration)
    // (RaphTime: Raph's line with the time to beat in it, RACEMOD.CPP beat_text=; RaphPark: where his car stands once he has stopped lapping
    // the storm track, [scene, x, y, z, turn] -- RaceTrackStory)
    public sealed record StoryInfo(int TiredText, int TownArrow, bool Seller = false, int RaphTime = -1, int[]? RaphPark = null, int RaphYell = -1, int RaphEntity = -1);

    // Play's race-track mode on a folder with a race track built: writes the engine's car file (the car setup in the settings, and the track's
    // start line, checkpoints and opponents from RACETRACK.JSON: `track`, one of the folder's tracks as RaceFor picks it, or else the
    // first); null for any other folder, which plays the game as it is.
    // `story`: the game played as a game (a new game, or a scene entered as it is, not a race started on its line): every track of the
    // folder in a set, each raced where and when the game is, with the story's gates and results (RaceCarEngineFile.WriteStorySet).
    // (`fine`: the story in the weather once the storm is over -- Play from Citadel Island's town circuit file, CITABAU.ILE)
    public static Action<string>? CarFileWriter(string gameDirectory, TrackInfo? track = null, bool story = false, bool fine = false)
    {
        if (!HasBackups(gameDirectory)) return null;
        var car = EditorSettings.Current.RaceCar.Clone();
        car.DriveAsKey = RaceCarEngineFile.DriveAsLine(gameDirectory, car.DriveAs);
        car.DriveAsIslandKeys = RaceCarEngineFile.DriveAsIslandLines(gameDirectory, car.DriveAs);
        car.TrackCarKeys = RaceCarEngineFile.TrackCarLines(gameDirectory, car.DriveAs);
        if (story && ReadInfo(gameDirectory) is { } all) return path => car.WriteStorySet(path, all, fine);
        return path => car.WriteEngineFile(path, track ?? ReadInfo(gameDirectory));
    }

    // A race track built by an older version of the editor, before the grid (and with it the qualifying lap and the count-down): its
    // RACETRACK.JSON has no grid spots, or there is none. The race-track mode then starts the old way; building it again brings them.
    // (an island with a track in each weather file has its grid in the one that carries the race; `track`: the one to be raced, else the
    // first. A track with no opponents -- the old moon's loops -- has no grid to line them up on.)
    public static bool IsOutdated(string gameDirectory, TrackInfo? track = null) =>
        HasBackups(gameDirectory) && (track ?? ReadInfo(gameDirectory)) is var info && (info?.Twin ?? info)?.Grid is not { Count: > 0 }
        && !(info is not null && RaceTrackIsland.ByName(info.Island).NoOpponents);

    // The folder's tracks: the first built, then the others (each with no Others of its own).
    public static List<TrackInfo> Tracks(TrackInfo? info) =>
        info is null ? new() : new List<TrackInfo> { info with { Others = null } }.Concat(info.Others ?? new()).ToList();

    // The islands the folder has tracks on, in the order they were built.
    public static List<RaceTrackIsland> BuiltIslands(string gameDirectory) =>
        File.Exists(Path.Combine(gameDirectory, InfoFile)) ? Tracks(ReadInfo(gameDirectory)).Select(t => RaceTrackIsland.ByName(t.Island)).ToList() : new();

    // The track Play races, of the folder's: the one on the island file the editor has open (`shownFile`, e.g. MOSQUIBE.ILE; for an island
    // with a track in each of its files -- Citadel Island -- that file's: CITADEL.ILE the storm track, CITABAU.ILE the town circuit), else
    // the one on the island of the scene that is open (`sceneIslandFile`, for an inside scene: the file its island byte names; that island's
    // track as it was built to be raced), else the first built. Null when the folder has none. (Celebration Island's statue track is on
    // CELEBRA2, the island with the statue, and its lava lake's on CELEBRAT, the island before it rises, under which the editor lists the
    // island's scenes: with no lava lake track built, CELEBRAT races the statue's.)
    public static TrackInfo? RaceFor(string gameDirectory, string? shownFile, string? sceneIslandFile = null)
    {
        var tracks = Tracks(ReadInfo(gameDirectory));
        if (tracks.Count == 0) return null;
        static bool Same(string? a, string? b) => a is not null && b is not null && string.Equals(Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b), StringComparison.OrdinalIgnoreCase);
        static bool Island(RaceTrackIsland i, string? file) => Same(file, i.IleFile) || Same(file, i.TwinIleFile);
        static bool Statue(RaceTrackIsland i, string? file) => i.Statue && Same(file, RaceTrackIsland.CelebrationLava.IleFile);
        foreach (var t in tracks)
        {
            var island = RaceTrackIsland.ByName(t.Island);
            if (t.Twin is not null && (Same(shownFile, island.IleFile) || Same(shownFile, island.TwinIleFile)))
            {
                // (the entry of the island that races the file shown: RacesTwin for the twin's)
                var twin = Same(shownFile, island.TwinIleFile);
                var entry = RaceTrackIsland.All.FirstOrDefault(i => i.IleFile == island.IleFile && i.RacesTwin == twin) ?? island;
                return t with { Island = entry.Name };
            }
            if (Island(island, shownFile)) return t;
        }
        return tracks.FirstOrDefault(t => Statue(RaceTrackIsland.ByName(t.Island), shownFile))
               ?? tracks.FirstOrDefault(t => Island(RaceTrackIsland.ByName(t.Island), sceneIslandFile))
               ?? tracks.FirstOrDefault(t => Statue(RaceTrackIsland.ByName(t.Island), sceneIslandFile)) ?? tracks[0];
    }

    // The same for a scene played on its own (the scene editor's Play): the track that races in that scene (Celebration Island's lava lake
    // has a scene of its own), else the track of the scene's island (its record's island byte), else the first built.
    public static TrackInfo? RaceForScene(string gameDirectory, int scene)
    {
        var tracks = Tracks(ReadInfo(gameDirectory));
        if (tracks.Count == 0) return null;
        if (tracks.FirstOrDefault(t => RaceTrackIsland.ByName(t.Island) is var i && (i.CopiesScene is not null || i.CopiesScenes is not null) && scene >= i.FirstScene && scene <= i.LastScene) is { } its) return its;
        try
        {
            var record = HqrArchive.Open(Path.Combine(gameDirectory, "SCENE.HQR")).Read(scene + 1);
            if (record.Length > 0 && tracks.FirstOrDefault(t => RaceTrackIsland.ByName(t.Island) is var i && i.IslandByte == record[0] && i.CopiesScene is null && i.CopiesScenes is null) is { } own) return own;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException) { }
        return tracks[0];
    }

    // The track Play races: for an island whose two files carry different tracks, the one the race track window was built with (Citadel
    // Island's town circuit, raced once the storm is over, or its storm track, raced in the rain: RaceTrackIsland.RacesTwin); else the one
    // there is.
    public static TrackInfo Raced(TrackInfo info) => info.Twin is { } twin && RaceTrackIsland.ByName(info.Island).RacesTwin ? twin : info;

    // Whether the race needs the weather once the storm is over: for an island with a track in each file, the one of the file raced (the
    // twin's is the fine weather's); for any other, as the car setup says.
    public static bool FineWeather(TrackInfo? info, bool setup) => info?.Twin is { } twin ? ReferenceEquals(Raced(info), twin) : setup;

    // A copy the build can then write to: a game folder taken off a disc (or from a reference set kept read-only) has read-only files, and
    // File.Copy carries that to the copy, so the next build would fail on its own backup.
    public static void CopyWritable(string from, string to)
    {
        File.Copy(from, to, overwrite: true);
        var info = new FileInfo(to);
        if (info.IsReadOnly) info.IsReadOnly = false;
    }

    // A folder has a race track when the files of the island its RACETRACK.JSON names (the first, of several) were kept.
    public static bool HasBackups(string gameDirectory) => BuiltIsland(gameDirectory) is { } island && FilesFor(island).All(f => File.Exists(Path.Combine(gameDirectory, f + BackupSuffix)));

    // The island the folder's track was built on (its RACETRACK.JSON says; a track built before there was a choice is the Desert island's).
    public static RaceTrackIsland? BuiltIsland(string gameDirectory)
    {
        if (!File.Exists(Path.Combine(gameDirectory, InfoFile))) return null;
        return RaceTrackIsland.ByName(ReadInfo(gameDirectory)?.Island ?? RaceTrackIsland.Desert.Name);
    }

    public static string? Problem(string gameDirectory)
    {
        if (!Directory.Exists(gameDirectory)) return "The LBA2 game folder isn't set. Choose it under File > Settings.";
        // (Polar Island's files are the build's to add when they aren't there: EnsurePolar)
        var added = RaceTrackIsland.All.Where(i => i.Dream).SelectMany(i => i.IslandFiles).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var f in AllFiles.Where(f => !added.Contains(f))) if (!File.Exists(Path.Combine(gameDirectory, f))) return $"{f} isn't in the game folder.";
        return null;
    }

    // One island's track for Build: its plan, the options it is built with (an object of its own: the build fills it in), and for an
    // island with a track in each of its files, the other file's plan when it isn't the one built into the program.
    public sealed record TrackBuild(RaceTrackPlan Plan, RaceTrackOptions Options, RaceTrackPlan? TwinPlan = null);

    public static BuildResult Build(string gameDirectory, RaceTrackPlan plan, RaceTrackOptions options, RaceTrackPlan? twinPlan = null) =>
        Build(gameDirectory, new[] { new TrackBuild(plan, options, twinPlan) });

    // Builds the tracks into the folder, in the order given, all from the originals: what the folder had before -- any island's track,
    // this build's or another's -- goes. (Two entries for one island -- Citadel Island's town circuit and its storm track, which build the
    // same files -- build it once, as the first of them.)
    // `progress` hears where it has got to: the folder's originals first, then each track its share, step by step (BuildSession.Step).
    public static BuildResult Build(string gameDirectory, IReadOnlyList<TrackBuild> tracks, IProgress<BuildProgress>? progress = null)
    {
        if (Problem(gameDirectory) is { } problem) return new(false, problem, new(), null);
        tracks = tracks.DistinctBy(t => t.Options.Island.IleFile, StringComparer.OrdinalIgnoreCase).ToList();
        if (tracks.Count == 0) return new(false, "No island is chosen: nothing was changed.", new(), null);
        // (the bar's shares, by how long each part takes: the originals, Polar Island added when the folder hasn't got it, Sendell's Well
        // made, then each track its own -- BuildSeconds)
        var addPolar = tracks.Any(t => t.Options.Island.Dream) && !PolarInOriginals(gameDirectory);
        var setup = 0.5 + (addPolar ? PolarAddSeconds : 0) + (tracks.Any(t => t.Options.Island == RaceTrackIsland.Sendell) ? 0.4 : 0)
                    + (tracks.Any(t => t.Options.Island == RaceTrackIsland.Palace) ? PalaceAddSeconds : 0)
                    + (tracks.Any(t => t.Options.Island == RaceTrackIsland.CxTower) ? CxTowerAddSeconds : 0)
                    + (tracks.Any(t => t.Options.Island == RaceTrackIsland.Temple) ? TempleAddSeconds : 0);
        var whole = setup + tracks.Sum(t => BuildSeconds(t.Options.Island));
        try
        {
            // (Polar Island's track needs the island: added to the originals when they haven't got it)
            if (addPolar) progress?.Report(new(0, "Polar Island: making the island from LBA1's scenes"));
            var polar = tracks.Any(t => t.Options.Island.Dream) ? EnsurePolar(gameDirectory) : new List<string>();
            progress?.Report(new((setup - 0.4) / whole, "Keeping the original files and starting from them"));
            var files = tracks.SelectMany(t => FilesFor(t.Options.Island)).Concat(ExtraFiles).Distinct().ToArray();
            foreach (var f in files)
            {
                var backup = Path.Combine(gameDirectory, f + BackupSuffix);
                if (!File.Exists(backup)) CopyWritable(Path.Combine(gameDirectory, f), backup);
            }
            // every build starts from the originals: this build's files and whatever an earlier build changed (another island's track that
            // this build leaves out goes); each island's own ground is loaded from its copy below
            var grounds = tracks.Select(t => t.Options.Island.IleFile).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var f in AllFiles.Where(f => !grounds.Contains(f) && File.Exists(Path.Combine(gameDirectory, f + BackupSuffix))))
                CopyWritable(Path.Combine(gameDirectory, f + BackupSuffix), Path.Combine(gameDirectory, f));
            // (an island the build makes -- Sendell's Well, the Emperor's palace -- is made afresh, its files and its scenes, from those
            // originals; Sendell's Well goes when this build leaves it out -- the Palace island stays, an island of its own too: Tools)
            var made = new List<string>();
            if (tracks.Any(t => t.Options.Island == RaceTrackIsland.Sendell)) { progress?.Report(new((setup - 0.3) / whole, "Sendell's Well: making the island")); made.AddRange(SendellWell.Install(gameDirectory)); }
            else SendellWell.Remove(gameDirectory);
            if (tracks.Any(t => t.Options.Island == RaceTrackIsland.Palace))
            {
                progress?.Report(new((setup - 0.3) / whole, "The Emperor's palace: making the island and its scenes"));
                made.AddRange(LBAAssembler.Terrain.Palace.PalaceTrack.Install(gameDirectory, BackupSuffix));
            }
            if (tracks.Any(t => t.Options.Island == RaceTrackIsland.CxTower))
            {
                progress?.Report(new((setup - 0.3) / whole, "Island CX: making the island, its halls one ring under the gas"));
                made.AddRange(LBAAssembler.Terrain.ControlTower.CxTowerTrack.Install(gameDirectory, BackupSuffix));
            }
            if (tracks.Any(t => t.Options.Island == RaceTrackIsland.Temple))
            {
                progress?.Report(new((setup - 0.3) / whole, "The Temple of Bú: making the island from LBA1's scenes, and its scenes"));
                if (Lba1Problem("The Temple of Bú's track is built on LBA1's temple, which the build makes from LBA1's own scenes") is { } noLba1) throw new InvalidDataException(noLba1);
                made.AddRange(LBAAssembler.Terrain.Temple.TempleTrack.Install(gameDirectory, Lba1Folder()));
            }
            // (one a build left or Tools made, with no Halloween race this time: as Tools makes it, without its road's doorways)
            else if (LBAAssembler.Terrain.Temple.TempleIsland.IsInstalled(gameDirectory)) PlainTemple(gameDirectory);

            var session = new BuildSession { Together = true };
            var log = new List<string>();
            var starts = new List<string>();
            RaceTrackReport? first = null;
            var before = setup;
            foreach (var (track, k) in tracks.Select((t, k) => (t, k)))
            {
                var options = track.Options;
                var shown = tracks.Count > 1 ? $"{options.Island.Name} ({k + 1} of {tracks.Count})" : options.Island.Name;
                var (from, share) = (before, BuildSeconds(options.Island));
                before += share;
                session.Progress = progress is null ? null : (f, what) => progress.Report(new((from + share * Math.Clamp(f, 0, 1)) / whole, $"{shown} — {what}"));
                var source = Path.Combine(gameDirectory, options.Island.IleFile + (options.Island.Created ? "" : BackupSuffix));
                var built = BuildFiles(gameDirectory, source, track.Plan, options, track.TwinPlan, session);
                var (report, scenes) = (built.Report, built.Scenes);
                first ??= report;
                if (tracks.Count > 1) log.Add($"==== {options.Island.Shown} ====");
                log.Add($"The lap is {report.Length:0} cells ({report.Length * 512:0} game units) long: {report.Vertices} ground points levelled, {report.Cells} cells painted, {report.DecorsRemoved + report.SolidDecorsRemoved} decor objects taken off the road.");
                if (options.Island.Created) log.AddRange(made);
                if (options.Island.Dream) log.AddRange(polar);
                log.AddRange(built.Log);
                log.AddRange(report.Notes);
                foreach (var placed in report.Placed) log.Add(placed);
                if (built.Twin is { Own: true } twin)
                {
                    log.Add($"{options.Island.TwinIleFile}'s own track:");
                    log.AddRange(twin.Report.Notes.Select(n => "  " + n));
                    log.AddRange(twin.Report.Placed.Select(p => "  " + p));
                }
                log.AddRange(scenes.Log.Where(l => !l.Contains("actors removed,") && !l.Contains("no longer waits") && !l.Contains("demo scene")));
                log.Add($"{scenes.ActorsRemoved} actors removed from {scenes.ScenesChanged} scenes.");
                var where = scenes.StartScene >= 0 ? $"Scene {scenes.StartScene} ({options.Island.Name}) starts on the grid." : "";
                if (built.Twin is { Own: true } && scenes.Twin is { StartScene: >= 0 } fine)
                    where = $"{options.Island.Name}'s track in the storm ({options.Island.IleFile}) starts in scene {scenes.StartScene}, its track once the storm is over ({options.Island.TwinIleFile}) in scene {fine.StartScene}.";
                if (where.Length > 0) starts.Add(where);
            }
            // (the palace dungeon's torturers' body and animations after every track's: Finish)
            if (session.Torturers) log.AddRange(LBAAssembler.Terrain.Palace.PalaceTorturers.Install(gameDirectory));
            // (Twinsen's pumpkin F1, the Temple of Bú's own car and anyone's to drive, after every track's bodies: no body before it moves)
            log.AddRange(RaceTrackCharacterCars.InstallPumpkin(gameDirectory));
            // (Twinsen's Halloween buggy -- his own buggy painted orange, with bats: the race game's first car while the pumpkin F1 waits
            // to be won -- bodies of the buggy's entity, after it: RaceTrackCharacterCars.InstallHalloweenBuggy)
            log.AddRange(RaceTrackCharacterCars.InstallHalloweenBuggy(gameDirectory));
            // (the toy dump truck after it -- a private special: left out where PrivateCars is off, the race game's own install --
            // RaceTrackCharacterCars.InstallToyTruck)
            if (PrivateCars) log.AddRange(RaceTrackCharacterCars.InstallToyTruck(gameDirectory));
            // (the Temple of Bú's flag marshal's body and animations after everything else's: Finish placed her)
            if (session.Marshal) log.AddRange(LBAAssembler.Terrain.Temple.TempleMarshal.Install(gameDirectory));
            // (and the toy truck's violet-wheeled copy, for the islands whose palette draws the violet, after even hers: no body moves --
            // RaceTrackCharacterCars.InstallToyTruckViolet)
            if (PrivateCars) log.AddRange(RaceTrackCharacterCars.InstallToyTruckViolet(gameDirectory));
            // (and the Temple of Bú's risers' body -- the rabbibunny skeleton made lighter -- after even that: no body moves)
            if (session.Risers) log.AddRange(LBAAssembler.Terrain.Temple.TempleHalloween.InstallRisers(gameDirectory));
            // (Halloween's monsters' cars -- the Temple of Bú's opponents' and anyone's to drive -- after even that: no body moves, the race
            // game's install (PrivateCars off) the same but for the toy truck's -- RaceTrackCharacterCars.InstallMonsters. The Temple's
            // scenes have their cars already: the racer entity's bodies VampireGeneric.., which this gives it.)
            log.AddRange(RaceTrackCharacterCars.InstallMonsters(gameDirectory));
            // (and LBA1's digger, Polar Island's tractor, from the LBA1 folder -- RaceTrackCharacterCars.InstallDigger -- after even those)
            log.AddRange(RaceTrackCharacterCars.InstallDigger(gameDirectory, Lba1Folder()));
            // (and Polar Island's grobo's body as this version makes it -- the folder's originals may have an earlier one: until 2026-10-27
            // it was written unlit, his grey skin black)
            if (LBAAssembler.Terrain.Polar.PolarIsland.IsInstalled(gameDirectory)) log.AddRange(LBAAssembler.Terrain.Polar.PolarGrobo.InstallBody(gameDirectory));
            // (and the Temple of Bú's secret party's disco ball and bubbles after even those: no body moves -- TempleParty)
            if (session.Party) log.AddRange(LBAAssembler.Terrain.Temple.TempleHalloween.PartyInstall(gameDirectory));
            // (the race items' sounds, every track's: RACESOUNDS\, which the engine plays as an item comes into the item box and as one is
            // used -- RaceSounds)
            log.AddRange(RaceSounds.Install(gameDirectory));
            progress?.Report(new(1, "Done"));
            var count = tracks.Sum(t => t.Options.Island.Tracks);
            var built1 = count == 1 ? "The race track is built." : $"{count} race tracks are built ({string.Join(", ", tracks.Select(t => t.Options.Island.Built))}); Play races the one of the island the editor has open.";
            return new(true, $"{built1} {string.Join(" ", starts)}".Trim(), log, first);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or LBAAssembler.LbaScript.ScriptCompileException)
        {
            // leave the folder as it was (and where it went wrong, for the log)
            DebugLog.Log($"RaceTrackService.Build failed: {error}");
            try { Restore(gameDirectory); } catch (Exception again) when (again is IOException or UnauthorizedAccessException) { /* the backups are still there */ }
            return new(false, $"Nothing was changed: {error.Message}", new(), null);
        }
    }

    // What a build made: the track of the island's own file and, for an island with a file for other weather, that file's -- the same
    // track, or (Own) one of its own. Log: lines for the build's log besides the reports' notes.
    public sealed record TwinTrack(RaceTrackReport Report, RaceTrackOptions Options, bool Own, string Log);
    public sealed record Built(RaceTrackReport Report, RaceTrackOptions Options, TwinTrack? Twin, RaceTrackScenes.Result Scenes, List<string> Log);

    // What the tracks of one build share: the cars made after the game's characters and Baldino's, put into BODY.HQR and RESS.HQR once for
    // all of them, and each track's record for RACETRACK.JSON, in the order built.
    public sealed class BuildSession
    {
        public bool CharacterCars, Baldino, SmallCars, Oil, CastCars, GuardBodies, Torturers, Marshal, Risers, Party;
        // (Build's: the palace dungeon's torturers made when every track is -- Finish)
        public bool Together;
        public int? OilIcon, SuperJetModel, TwinsenSmall, GoldIcon, LavaBallModel;
        public int[]? CrystalShardModels;
        // FunFrock's car empty: its BODY.HQR entry and the racer's driving animation (TrackInfo.ChaseCar)
        public int[]? FunFrockEmpty;
        public int[]? LaserModels;
        public List<TrackInfo> Tracks { get; } = new();
        // where the track being built has got to (the part of it done, 0..1, and what it is doing), for Build's progress; null: no one asks
        public Action<double, string>? Progress;
        public void Step(double done, string doing) => Progress?.Invoke(done, doing);
    }

    // Builds the track into the game folder's files, which are the originals when this runs (the island's own ground is read from
    // `islandSource`: the original kept beside it, or the file itself) -- or, for the second and later tracks of a `session`, the originals
    // with the tracks before it built in. The menu's Build and the command line's buildtrack both come here.
    // An island whose other-weather file has a track of its own (Citadel Island: RaceTrackIsland.TwinPlanResource, or `twinPlan`) gets
    // both: `plan` in its own file, with no opponents, and the other in the twin, which carries the race -- the opponents and the story.
    public static Built BuildFiles(string gameDirectory, string islandSource, RaceTrackPlan plan, RaceTrackOptions options, RaceTrackPlan? twinPlan = null, BuildSession? session = null)
    {
        session ??= new BuildSession();
        session.Step(0, "reading the island");
        twinPlan ??= RaceTrackPlan.BuiltTwin(options.Island);
        // (the twin's options as they were chosen, before this file's plan settles its crossing style and bodies)
        var twinOptions = options.Copy();
        // (the twin's line-up is its own: Citadel Island's town circuit's)
        if (options.Island.TwinRoster is { } town && options.Drivers is not null) twinOptions.Drivers = town;
        if (twinPlan is not null) { options.AddOpponent = false; options.AddBaldino = false; options.AddBiker = false; }
        FollowPlan(plan, options);
        // (an island made bigger for its track -- the Island of the Francos twice its size: IslandScaler -- its ground, its objects' bodies
        // and its scenes, from the originals, before the build adds its own)
        var island = IslandFile.Load(islandSource);
        var scaled = new List<string>();
        if (options.Island.Scale > 1)
        {
            var anchor = IslandScaler.Anchor(island);
            island = IslandScaler.Scale(island, options.Island.Scale, scaled);
            scaled.Add(IslandScaler.ScaleObl(Path.Combine(gameDirectory, options.Island.OblFile), options.Island.Scale));
            scaled.AddRange(IslandScaler.ScaleScenes(gameDirectory, options.Island, island, anchor, options.Island.Scale));
            scaled.Add(IslandScaler.ScaleHolomap(gameDirectory, options.Island, anchor, options.Island.Scale));
        }
        // (cubes of its ground the game has no scene for, that the lap drives over: a scene each -- Otringal's south-west islets)
        if (options.Island.NewCubes.Length > 0) scaled.AddRange(IslandScaler.AddCubeScenes(gameDirectory, options.Island, island));
        var extra = Prepare(gameDirectory, options);
        extra.InsertRange(0, scaled);
        session.Step(0.05, "the road's textures");
        var themed = RaceTrackTextures.Import(island, options.Island, gameDirectory);
        options.Theme = themed.Theme;
        if (themed.Log.Length > 0) extra.Add(themed.Log);
        session.Step(0.1, "the road, its ground and its scenery");
        options.DoorBoxes = DoorBoxes(gameDirectory, options.Island);
        var atDoors = options.DoorBoxes.Select(b => RaceTrackOptions.DecorsAtDoor(island, b)).ToList();
        var report = RaceTrackBuilder.Build(island, plan, options);
        NoteLostDoors(island, options.DoorBoxes, atDoors, report);
        // (an island with more ground pages -- POLAR.ILE -- has a texture index of a page and a definition, IslandFile.GroundPageShift, and
        // the triangle's Wide bit: the road's definitions, the smooth kerbs' a triangle each on a bend, must fit in a cube's 2,048)
        if (island.GroundPages.Count > 0)
        {
            var full = IslandOps.CubeCells(island).MaxBy(c => c.Item3.TextureDefs.Length);
            var most = full.Item3.TextureDefs.Length / 6;
            if (most > island.MaxGroundDefinitions)
                throw new InvalidDataException($"{options.Island.IleFile}: cube ({full.Item1}, {full.Item2}) needs {most} ground texture definitions with the road, more than the {island.MaxGroundDefinitions} an island with more texture pages has room for.");
            extra.Add($"{options.Island.IleFile}: at most {most} ground texture definitions in a cube with the road (room for {island.MaxGroundDefinitions})");
        }
        session.Step(0.65, "saving the island");
        island.Save(Path.Combine(gameDirectory, options.Island.IleFile));
        AppendBodies(Path.Combine(gameDirectory, options.Island.OblFile), report, options);
        if (options.Island.TwinIleFile is not null) session.Step(0.67, $"{options.Island.TwinIleFile}, the island in the other weather");
        var twin = BuildTwin(gameDirectory, twinPlan ?? plan, twinPlan is not null, twinOptions, options, report);
        if (twin is not null) extra.Add(twin.Log);
        session.Step(0.7, "the holomap, the jumps and the cars");
        extra.AddRange(Finish(gameDirectory, report, options, twin, session));
        var own = twin is { Own: true } ? twin : null;
        session.Step(0.92, "the scenes");
        if (RaceTrackScenes.AddScene(gameDirectory, options.Island) is { } added) extra.Add(added);
        var scenes = RaceTrackScenes.Apply(gameDirectory, report, options, own is null ? null : (own.Report, own.Options));
        session.Step(0.98, "the story and the race's settings");
        var story = Story(gameDirectory, report, options, own, scenes);
        extra.AddRange(story.Log);
        // (a dreamt sprint's texts, and where it ends; or the Emperor's palace's, its end the Dark Monk's teleporter to Celebration Island)
        DreamInfo? dream = null;
        if (options.Island.Sprint && report.FinishLine is { } end)
        {
            var (textLog, intro, lose, wake) = LBAAssembler.Terrain.Palace.PalaceTrack.Texts(gameDirectory);
            extra.Add(textLog);
            dream = new DreamInfo(LineInfo(end, report.FinishLineHeight), 0, intro, lose, LBAAssembler.Terrain.Palace.PalaceTrack.WakeScene, wake,
                LBAAssembler.Terrain.Palace.PalaceTrack.WakeActor, LBAAssembler.Terrain.Palace.PalaceTrack.StatueVar, LBAAssembler.Terrain.Palace.PalaceTrack.WakeFlightMs);
        }
        if (options.Island.Dream && report.FinishLine is { } finish)
        {
            var (dreamLog, wake, inOpening) = LBAAssembler.Terrain.Polar.PolarDream.Apply(gameDirectory);
            extra.AddRange(dreamLog);
            dream = new DreamInfo(LineInfo(finish, report.FinishLineHeight), LBAAssembler.Terrain.Polar.PolarDream.TopKmh, LBAAssembler.Terrain.Polar.PolarDream.IntroText,
                LBAAssembler.Terrain.Polar.PolarDream.LoseText, LBAAssembler.Terrain.Polar.PolarDream.WakeScene, inOpening ? -1 : wake, LBAAssembler.Terrain.Polar.PolarDream.WakeActor,
                inOpening ? LBAAssembler.Terrain.Polar.PolarDream.DreamVar : -1, LBAAssembler.Terrain.Polar.PolarDream.WakeFlightMs);
        }
        WriteInfo(gameDirectory, report, options, scenes, own, session, story.Info, dream);
        return new Built(report, options, twin, scenes, extra);
    }

    // The island's fine-weather file (Citadel Island's CITABAU), built after the main one. With the same plan (`own` false) the ground is
    // the same, so the road comes out the same, but its decors, its texture page and palette and its decor bodies are its own -- the tiles
    // are copied in its own palette, the deck gets a body in its own OBL, and whatever of its own decor is on the road is cleared; the
    // scenes are shared and are given the main build's cars, grid and zones. With a plan of its own it is a track of its own, built with
    // `twinOptions` (the options as chosen: its crossing style is the one chosen, unless its plan draws its own). Null for an island with no twin.
    public static TwinTrack? BuildTwin(string gameDirectory, RaceTrackPlan plan, bool own, RaceTrackOptions twinOptions, RaceTrackOptions options, RaceTrackReport main)
    {
        if (options.Island.TwinIleFile is not { } ile || options.Island.TwinOblFile is not { } obl) return null;
        var source = Path.Combine(gameDirectory, ile + BackupSuffix);
        var twin = IslandFile.Load(File.Exists(source) ? source : Path.Combine(gameDirectory, ile));
        twinOptions = own ? twinOptions.Copy() : options.Copy();
        twinOptions.DoorBoxes = options.DoorBoxes;      // (the same scenes, the same doors)
        if (own) FollowPlan(plan, twinOptions);
        // (a track of its own has a jump of its own: a flight of its own too)
        if (own) twinOptions.JumpAnim = RaceTrackJumpAnim.GenericFor(options.Island, twin: true);
        twinOptions.RetailBodies = new();
        CopyRetailBodies(gameDirectory, obl, twinOptions);
        if (twinOptions.Crossing == CrossingStyle.Bridge)
            twinOptions.DeckBodyIndex = RaceTrackDeckBody.AppendTo(Path.Combine(gameDirectory, obl), twinOptions);
        twinOptions.Theme = RaceTrackTextures.Import(twin, options.Island, gameDirectory, ile).Theme;
        twinOptions.NewBodyBase = HqrArchive.CountEntries(Path.Combine(gameDirectory, obl));
        twinOptions.SceneryObl = Path.Combine(gameDirectory, obl);
        var atDoors = twinOptions.DoorBoxes.Select(b => RaceTrackOptions.DecorsAtDoor(twin, b)).ToList();
        var museum = options.Island.TwinMuseum is { } tm ? RaceTrackBuilder.FindDecor(twin, tm.Body, tm.FromX, tm.FromZ) : null;
        var report = RaceTrackBuilder.Build(twin, plan, twinOptions);
        NoteLostDoors(twin, twinOptions.DoorBoxes, atDoors, report);
        if (museum is { } front) RaceTrackBuilder.PlaceTwinMuseum(twin, front, twinOptions, report);
        twin.Save(Path.Combine(gameDirectory, ile));
        AppendBodies(Path.Combine(gameDirectory, obl), report, twinOptions);
        if (own)
            return new TwinTrack(report, twinOptions, true, $"{ile} (the island once the storm is over) built with its own track: lap {report.Length:0} cells, " +
                                                            $"{report.DecorsRemoved + report.SolidDecorsRemoved} of its own decors taken off the road");
        var same = Math.Abs(report.Length - main.Length) < 0.01 && report.StartLine.SequenceEqual(main.StartLine) && report.Pits.SequenceEqual(main.Pits) && report.Checkpoints.Count == main.Checkpoints.Count;
        return new TwinTrack(report, twinOptions, false, $"{ile} (the island once the storm is over) built with the same track: lap {report.Length:0} cells, {report.DecorsRemoved + report.SolidDecorsRemoved} of its own decors taken off the road" +
                                                         (same ? "" : " -- WARNING: its road came out different from the main build's"));
    }

    // The retail race track's gantry (64-66) and viaduct arch (68-70) are bodies of DESERT.OBL; on another island those numbers are that
    // island's own objects, so the Desert's are copied to the end of its OBL (as they are: the island palettes put the same colours at
    // those indices -- Citadel's fine-weather one exactly, its storm one a shade darker, as its whole island is) and options.RetailBodies
    // maps each to its copy. Nothing for the Desert island itself. Returns a line for the log.
    public static readonly int[] RetailBodyNumbers = { 64, 65, 66, 68, 69, 70 };
    // Citadel Island's white fence: a section a thousand units long, its post at its start, and the post that ends a run of them
    public const int FenceSectionBody = 53, FencePostBody = 54;

    public static string? CopyRetailBodies(string gameDirectory, string oblFile, RaceTrackOptions options)
    {
        if (string.Equals(oblFile, RaceTrackIsland.Desert.OblFile, StringComparison.OrdinalIgnoreCase)) return null;
        var desertPath = Path.Combine(gameDirectory, RaceTrackIsland.Desert.OblFile);
        var desert = HqrArchive.Open(File.Exists(desertPath + BackupSuffix) ? desertPath + BackupSuffix : desertPath);
        var path = Path.Combine(gameDirectory, oblFile);
        var index = HqrArchive.CountEntries(path);
        var hqr = File.ReadAllBytes(path);
        foreach (var body in RetailBodyNumbers)
        {
            hqr = HqrWriter.AppendEntry(hqr, HqrWriter.StoredEntry(desert.Read(body)));
            options.RetailBodies[body] = index++;
        }
        File.WriteAllBytes(path, hqr);
        return $"the start gantry and the viaduct arch: the Desert track's own bodies copied into {oblFile} (as {options.RetailBodies[64]}-{options.RetailBodies[70]})";
    }

    // A plan that draws its own bridge and jump (RaceTrackPlan.Planned: Mosquibees Island's) takes no crossing style -- the window greys
    // the choice out -- but the style still decides whether the deck gets a body (Prepare, BuildTwin), so it follows the plan: a bridge
    // when the plan draws one. Not whatever the window last showed: that is read back from the folder's previous build, and a Jump left
    // there built the lap with no deck under its bridge. Before Prepare.
    public static void FollowPlan(RaceTrackPlan plan, RaceTrackOptions options)
    {
        if (plan.Planned) options.Crossing = plan.Deck is not null ? CrossingStyle.Bridge : CrossingStyle.Level;
        if (plan.Planned) plan.ApplyTo(options);
    }

    // The decor bodies a build made (a raised road's pieces and piers: RaceTrackReport.NewBodies) go on the end of the island's OBL, where
    // the build's decors expect them (from RaceTrackOptions.NewBodyBase on).
    private static void AppendBodies(string oblPath, RaceTrackReport report, RaceTrackOptions options)
    {
        if (report.NewBodies.Count == 0) return;
        if (HqrArchive.CountEntries(oblPath) != options.NewBodyBase)
            throw new InvalidDataException($"{Path.GetFileName(oblPath)} has {HqrArchive.CountEntries(oblPath)} bodies, not the {options.NewBodyBase} the raised road's pieces were numbered from.");
        var hqr = File.ReadAllBytes(oblPath);
        foreach (var body in report.NewBodies) hqr = HqrWriter.AppendEntry(hqr, HqrWriter.StoredEntry(body));
        File.WriteAllBytes(oblPath, hqr);
    }

    // What a crossing style needs in the files besides the island and the scenes (the files are the originals when these run): before the
    // island is built, the bridge deck's bodies in DESERT.OBL; after it, the jump's flight in ANIM.HQR and RESS.HQR, as long as the layout
    // made it, and Baldino's car in BODY.HQR and RESS.HQR. Return lines for the build's log.
    public static List<string> Prepare(string gameDirectory, RaceTrackOptions options)
    {
        var log = new List<string>();
        if (CopyRetailBodies(gameDirectory, options.Island.OblFile, options) is { } copied) log.Add(copied);
        if (options.Crossing == CrossingStyle.Bridge)
            options.DeckBodyIndex = RaceTrackDeckBody.AppendTo(Path.Combine(gameDirectory, options.Island.OblFile), options);
        // (the island's own flight, for a jump its plan draws or its crossing makes)
        options.JumpAnim = RaceTrackJumpAnim.GenericFor(options.Island);
        // (a fence along the pit lane: Citadel Island's white one -- a section and its end post -- as they are: the island palettes share
        // their colours)
        if (options.PitFence)
        {
            var citadel = Path.Combine(gameDirectory, RaceTrackIsland.Citadel.OblFile);
            var bodies = HqrArchive.Open(File.Exists(citadel + BackupSuffix) ? citadel + BackupSuffix : citadel);
            options.FenceSection = bodies.Read(FenceSectionBody); options.FencePost = bodies.Read(FencePostBody);
            log.Add($"the pit lane's fence: Citadel Island's white fence (CITADEL.OBL bodies {FenceSectionBody} and {FencePostBody})");
        }
        // (where a raised road's own bodies go: after whatever was appended above)
        options.NewBodyBase = HqrArchive.CountEntries(Path.Combine(gameDirectory, options.Island.OblFile));
        options.SceneryObl = Path.Combine(gameDirectory, options.Island.OblFile);
        return log;
    }

    // The mod's story, on the island that has one (Citadel Island: RaceTrackStory, its storm track and its town circuit). After the scenes,
    // which it adds to.
    // Celebration Island's lava lake has one too: the souvenir seller tells what he knows once Twinsen has beaten him there.
    public static (List<string> Log, StoryInfo? Info) Story(string gameDirectory, RaceTrackReport report, RaceTrackOptions options, TwinTrack? town, RaceTrackScenes.Result scenes)
    {
        if (!options.Story) return (new List<string>(), null);
        if (options.Island.IleFile == RaceTrackIsland.Citadel.IleFile && town is { Own: true } && scenes.Twin is { } townScenes)
            return RaceTrackStory.Apply(gameDirectory, report, town.Report, scenes, townScenes);
        if (options.Island == RaceTrackIsland.CelebrationLava) return (RaceTrackStory.ApplyCelebration(gameDirectory, scenes), new StoryInfo(-1, -1, true));
        return (new List<string>(), null);
    }

    // After the island's files: the track on the holomap's pictures (a twin with a track of its own on its own picture, the fine weather's),
    // the jump's flight (each jump its own: RaceTrackJumpAnim.GenericFor), Baldino's car and the characters' cars (RaceTrackCharacterCars) --
    // the cars once for all the tracks of a build (`session`).
    public static List<string> Finish(string gameDirectory, RaceTrackReport report, RaceTrackOptions options, TwinTrack? twin = null, BuildSession? session = null)
    {
        session ??= new BuildSession();
        var log = new List<string>();
        var own = twin is { Own: true } ? twin : null;
        if (options.DrawOnHolomap && File.Exists(Path.Combine(gameDirectory, RaceTrackHolomap.File)))
        {
            RaceTrackHolomap.UsePalette(HqrArchive.Open(Path.Combine(gameDirectory, "RESS.HQR")).Read(0));
            var pictures = RaceTrackHolomap.Pictures(options.Island);
            if (own is null || pictures.Length < 2)
                log.Add(RaceTrackHolomap.Draw(gameDirectory, options.Island, IslandFile.Load(Path.Combine(gameDirectory, options.Island.IleFile)), report));
            else
            {
                log.Add(RaceTrackHolomap.Draw(gameDirectory, options.Island, IslandFile.Load(Path.Combine(gameDirectory, options.Island.IleFile)), report, pictures[..1]));
                log.Add(RaceTrackHolomap.Draw(gameDirectory, options.Island, IslandFile.Load(Path.Combine(gameDirectory, options.Island.TwinIleFile!)), own.Report, pictures[1..]));
            }
        }
        foreach (var a in report.Jumps) log.Add(a.Drop > 0 ? RaceTrackJumpAnim.InstallDrop(gameDirectory, a.FlightCells, a.Drop, a.Beta, a.Anim) : RaceTrackJumpAnim.Install(gameDirectory, a.FlightScale, a.Anim));
        foreach (var b in own?.Report.Jumps ?? new())
        {
            if (report.Jumps.FirstOrDefault(a => a.Anim == b.Anim) is not { } a) log.Add(RaceTrackJumpAnim.Install(gameDirectory, b.FlightScale, b.Anim));
            else if (Math.Abs(a.FlightScale - b.FlightScale) > 0.001)
                log.Add($"WARNING: both files' tracks have a jump, of different lengths, with one flight, made for {options.Island.IleFile}'s");
        }
        // (Baldino's rocket car, when a track's line-up races it)
        var racers = options.Racers().Concat(own?.Options.Racers() ?? new List<RaceDriver>());
        if (racers.Any(d => !d.Bike && !d.Ghost && d.Cast < 0 && d.Body == RaceTrackBaldinoCar.Generic) && !session.Baldino) { log.Add(RaceTrackBaldinoCar.Install(gameDirectory).Log); session.Baldino = true; }
        // the cars after the game's characters, three or more for each island: in the game's files for whoever is to drive them (no actor
        // has one yet)
        if (!session.CharacterCars)
        {
            var (carIndex, carLog) = RaceTrackCharacterCars.Install(gameDirectory);
            log.AddRange(carLog);
            if (carIndex.TryGetValue(RaceTrackCharacterCars.FunFrockEmptyName, out var emptyCar) && RaceTrackCharacterCars.RacerDrive(gameDirectory) is { } drive)
                session.FunFrockEmpty = new[] { emptyCar, drive };
            session.CharacterCars = true;
        }
        // ... and the people in the cars' way twice their size (RaceTrackGuards), when a track has them
        if (report.Guards.Count > 0 && !session.GuardBodies) { log.AddRange(RaceTrackGuards.Install(gameDirectory)); session.GuardBodies = true; }
        // ... and each of the racer entity's cars shrunk to half its size, for the lightning spell (RaceTrackSmallCars)
        if (!session.SmallCars)
        {
            var small = RaceTrackSmallCars.Install(gameDirectory);
            log.Add(small);
            if (!small.Contains("not found")) session.TwinsenSmall = RaceTrackSmallCars.HeroSmall;
            session.SmallCars = true;
        }
        // ... and the cars of the rest of the game's characters, each with its half-size copy, and RACECARS.JSON with every car
        // (RaceTrackCharacterCars.InstallCast: the car setup's "drive as" goes by it)
        if (!session.CastCars) { log.AddRange(RaceTrackCharacterCars.InstallCast(gameDirectory)); session.CastCars = true; }
        // ... and the oil slick the power-ups' oil leaves on the road (RaceTrackOil)
        if (!session.Oil)
        {
            log.Add(RaceTrackOil.Install(gameDirectory));
            // (and the power-ups' mushroom made bigger, beside it in the mushroom's entity)
            log.Add(RaceTrackOil.InstallBigMushroom(gameDirectory));
            // (and its drum in the item box)
            var (icon, iconLog) = RaceTrackOil.InstallIcon(gameDirectory);
            log.Add(iconLog);
            session.OilIcon = icon;
            // (and the golden Gazogem, the roulette wheel's super jackpot)
            var (gold, goldLog) = RaceTrackOil.InstallGoldIcon(gameDirectory);
            log.Add(goldLog);
            session.GoldIcon = gold;
            // (and the game's lava ball, bigger, for the lava's balls)
            var (lavaBall, lavaBallLog) = RaceTrackOil.InstallLavaBall(gameDirectory);
            log.Add(lavaBallLog);
            session.LavaBallModel = lavaBall;
            // (and the crystal shards the volcano throws)
            var (shard, shardBit, shardLog) = RaceTrackCrystals.InstallCrystalShards(gameDirectory);
            log.Add(shardLog);
            session.CrystalShardModels = new[] { shard, shardBit };
            // ... and the super jet-pack the car turns into
            var (jet, jetLog) = RaceTrackSuperJet.Install(gameDirectory);
            log.Add(jetLog);
            session.SuperJetModel = jet;
            // ... and the laser cars' bolts
            var (green, red, laserLog) = RaceTrackLasers.Install(gameDirectory);
            log.Add(laserLog);
            session.LaserModels = new[] { green, red };
            session.Oil = true;
        }
        // ... and each gas monster's moves, fitted to the road beside it
        log.AddRange(RaceTrackGasMonster.InstallPoses(gameDirectory, report));
        // ... and the giant frog the lap drives through
        log.AddRange(RaceTrackFrog.Install(gameDirectory, report));
        // ... and Halloween's spooks and pumpkins
        log.AddRange(LBAAssembler.Terrain.Temple.TempleHalloween.Install(gameDirectory, report));
        // ... and its giant webs
        log.AddRange(LBAAssembler.Terrain.Temple.TempleHalloween.InstallGiantWebs(gameDirectory, report));
        // ... and the guests of its secret party (the spooks' bodies are in now); their own bodies at the end of a build of several tracks
        // (Build: every other track's bodies where they were), or here
        if (report.OnFoot)
        {
            log.AddRange(LBAAssembler.Terrain.Temple.TempleHalloween.PartyGuests(gameDirectory, report));
            if (!session.Party)
            {
                session.Party = true;
                if (!session.Together) log.AddRange(LBAAssembler.Terrain.Temple.TempleHalloween.PartyInstall(gameDirectory));
            }
        }
        // ... and the palace dungeon's torturers, their whips in hand: at the end of a build of several tracks (Build: every other track's
        // bodies and animations where they were), or here
        if (report.Guards.Any(g => (int)g[0] == RaceTrackGuards.Torturer) && !session.Torturers)
        {
            session.Torturers = true;
            if (!session.Together) log.AddRange(LBAAssembler.Terrain.Palace.PalaceTorturers.Install(gameDirectory));
        }
        // ... and the Temple of Bú's flag marshal by its start line: where she stands now (the race file's marshal=), her body and animations
        // at the end of a build of several tracks (Build: every other track's where they were), or here
        if (options.Island.FlagMarshal && LBAAssembler.Terrain.Temple.TempleMarshal.Place(gameDirectory, report, options.GridFirst, options.GridSide) is { } marshal)
        {
            report.Marshal = marshal;
            if (!session.Marshal)
            {
                session.Marshal = true;
                if (!session.Together) log.AddRange(LBAAssembler.Terrain.Temple.TempleMarshal.Install(gameDirectory));
            }
        }
        // ... and the Temple of Bú's risers' body (the race file names it by its entity), at the end of a build of several tracks, or here
        if (report.Spooks.Any(s => s.Kind == "riser" && s.Entity >= 0) && !session.Risers)
        {
            session.Risers = true;
            if (!session.Together) log.AddRange(LBAAssembler.Terrain.Temple.TempleHalloween.InstallRisers(gameDirectory));
        }
        return log;
    }

    // RACETRACK.JSON: the crossing style and the start line, for Play; and, for a twin with a track of its own, that track as Twin. The
    // tracks built before this one in `session` stay in it: the first is the file's own record, the rest its Others.
    public static void WriteInfo(string gameDirectory, RaceTrackReport report, RaceTrackOptions options, RaceTrackScenes.Result scenes, TwinTrack? twin = null, BuildSession? session = null,
        StoryInfo? story = null, DreamInfo? dream = null)
    {
        session ??= new BuildSession();
        var info = Info(report, options, scenes) with { Story = story, OilIcon = session.OilIcon, GoldIcon = session.GoldIcon, SuperJetModel = session.SuperJetModel, TwinsenSmall = session.TwinsenSmall, Dream = dream,
            ChaseCar = options.Island.ChaseDriver is { } chaseDriver && session.FunFrockEmpty is [var ce, var ca] ? new[] { ce, ca, chaseDriver.Entity } : null,
            LaserModels = session.LaserModels, LavaBallModel = session.LavaBallModel, CrystalShardModels = session.CrystalShardModels };
        // (and for a teleporter into another cube -- the Temple of Bú's hidden passage: the scene it puts the car into)
        if (info.ArcJumps is not null || info.Teleports is not null) info = info with { CubeScenes = RaceTrackScenes.CubeScenes(gameDirectory, options.Island) };
        if (Shuttles(gameDirectory, report, options) is { Count: > 0 } shuttles) info = info with { Shuttles = shuttles };
        // (the race's own music: the Temple of Bú's LBA1 temple music, when the build found it)
        if (options.Island == RaceTrackIsland.Temple && LBAAssembler.Terrain.Temple.TempleMusic.Installed(gameDirectory) is { } music) info = info with { Music = music };
        if (twin is { Own: true } && scenes.Twin is { } twinScenes)
        {
            // (the story belongs to the twin's track, which carries the race; either race clears its arrow once Twinsen drives)
            var fine = Info(twin.Report, twin.Options, twinScenes);
            // (the town circuit's own arrow, which the bed switches on)
            if (story is not null) fine = fine with { StoryArrow = story.TownArrow };
            info = info with { Twin = fine with { OilIcon = session.OilIcon, GoldIcon = session.GoldIcon, SuperJetModel = session.SuperJetModel, TwinsenSmall = session.TwinsenSmall, LaserModels = session.LaserModels,
                LavaBallModel = session.LavaBallModel, CrystalShardModels = session.CrystalShardModels } };
        }
        session.Tracks.Add(info);
        var all = session.Tracks[0] with { Others = session.Tracks.Count > 1 ? session.Tracks.Skip(1).ToList() : null };
        File.WriteAllText(Path.Combine(gameDirectory, InfoFile), JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }

    // The island's shuttles (RaceTrackIsland.Shuttles) as the built scenes have them: each one's actor there (the scene's actor of its
    // entity nearest where it stood -- removing the others renumbers the scene's actors, and the build moves one off the road) and its pad's
    // height, the road's surface there.
    private static List<int[]> Shuttles(string gameDirectory, RaceTrackReport report, RaceTrackOptions options)
    {
        var list = new List<int[]>();
        if (options.Island.Shuttles.Length == 0) return list;
        var store = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, gameDirectory);
        foreach (var (scene, actor, entity, x, z) in options.Island.Shuttles)
        {
            LBAAssembler.Scenes.SceneModel model;
            try { model = store.Load(scene); } catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { continue; }
            var found = Enumerable.Range(1, Math.Max(0, model.Actors.Count - 1)).Where(i => model.Actors[i].Entity == entity)
                .OrderBy(i => Math.Abs(i - actor)).ThenBy(i => Math.Abs(model.Actors[i].X - x) + Math.Abs(model.Actors[i].Z - z)).FirstOrDefault(-1);
            if (found < 0) continue;
            double cx = model.CubeX * 64 + x / 512.0, cz = model.CubeY * 64 + z / 512.0;
            // (the deck there as the engine has it -- the raised road's file, world units: the nearest of its segments within its rails --
            // else the ground)
            double wx = cx * 512, wz = cz * 512, best = double.MaxValue;
            double? y = null;
            for (var i = 0; i + 1 < report.Raised.Count; i++)
            {
                int[] a = report.Raised[i], b = report.Raised[i + 1];
                if (a.Length < 4 || b.Length < 4 || a[3] <= 0 || b[3] <= 0) continue;
                double sx = b[0] - a[0], sz = b[1] - a[1], len2 = sx * sx + sz * sz;
                if (len2 < 1) continue;
                var t = Math.Clamp(((wx - a[0]) * sx + (wz - a[1]) * sz) / len2, 0, 1);
                var d = Math.Sqrt((wx - a[0] - sx * t) * (wx - a[0] - sx * t) + (wz - a[1] - sz * t) * (wz - a[1] - sz * t));
                if (d > a[3] + (b[3] - a[3]) * t || d >= best) continue;
                best = d; y = a[2] + (b[2] - a[2]) * t;
            }
            y ??= report.GroundAfter?.Invoke(cx, cz) ?? model.Actors[found].Y;
            list.Add(new[] { scene, found, x, (int)Math.Round(y.Value), z });
        }
        return list;
    }

    // A line across the road (island cells) as the engine has it: in the cube its middle is in.
    private static StartLineInfo LineInfo((double X0, double Z0, double X1, double Z1, double DirX, double DirZ) l, double? height = null)
    {
        var cx = (int)Math.Floor((l.X0 + l.X1) / 2 / 64); var cz = (int)Math.Floor((l.Z0 + l.Z1) / 2 / 64);
        int Local(double cells, int cube) => (int)Math.Round((cells - cube * 64) * 512);
        return new StartLineInfo(cx, cz, Local(l.X0, cx), Local(l.Z0, cz), Local(l.X1, cx), Local(l.Z1, cz), (int)Math.Round(l.DirX * 1000), (int)Math.Round(l.DirZ * 1000),
            height is { } h ? (int)Math.Round(h) : null);
    }

    private static TrackInfo Info(RaceTrackReport report, RaceTrackOptions options, RaceTrackScenes.Result scenes)
    {
        static StartLineInfo Line((double X0, double Z0, double X1, double Z1, double DirX, double DirZ) l, double? height = null) => LineInfo(l, height);
        // (every line with its height: the engine counts it only for a car near that height, so a line under a bridge, or a raised road's
        // other levels, is not crossed from the road over it; since 2026-10-01 for every lap, not only a raised road's)
        var raised = report.Raised.Count > 0;
        var line = report.LapLine is { } l ? Line(l, report.LapLineHeight) : null;
        var checkpoints = report.Checkpoints.Select((c, i) => Line(c, i < report.CheckpointHeights.Count ? report.CheckpointHeights[i] : null)).ToList();
        static List<int[]> Points(List<(double X, double Z, double Y, double Speed, double Radius)> line)
            => line.Select(p => new[] { (int)Math.Round(p.X * 512), (int)Math.Round(p.Z * 512), (int)Math.Round(p.Y), (int)Math.Round(p.Speed), (int)Math.Round(Math.Min(p.Radius, 1e6)) }).ToList();
        var path = Points(report.RacePath);
        // the drivers: each one's line, and its car's copies (a time to beat has none)
        var drivers = new List<DriverInfo>();
        var racers = options.Racers();
        for (var k = 0; k < racers.Count && k < report.DriverPaths.Count; k++)
        {
            var d = racers[k];
            var actors = d.Ghost ? new Dictionary<int, int>() : scenes.Drivers.FirstOrDefault(c => c.Driver == d).Actors;
            if (actors is null || !d.Ghost && actors.Count == 0 || report.DriverPaths[k].Count == 0) continue;
            drivers.Add(new DriverInfo(d.Name, Points(report.DriverPaths[k]), 4 * (k + 1), actors, d.Line.Top, d.Line.Grip, d.Main, d.Bike, d.Ghost, d.Bike ? -1 : d.Body) { Hover = d.Hover });
        }
        return new TrackInfo(options.Crossing.ToString(), line, checkpoints, path.Count > 0 ? path : null, 4, null, scenes.StartScene,
            null, scenes.Grid.Count > 0 ? scenes.Grid : null, scenes.Pits.Count > 0 ? scenes.Pits : null, options.Island.Name,
            options.Story && options.Island.IleFile == RaceTrackIsland.Citadel.IleFile ? RaceTrackStory.StartArrow : -1,
            Raised: raised ? report.Raised : null, Gravity: raised ? report.Gravity : null, RailCamera: raised ? report.RailCamera : null,
            Loops: report.Loops.Count > 0 ? report.Loops.Select(LoopRecord).ToList() : null,
            JumpCameras: report.JumpCameras.Count > 0
                ? report.JumpCameras.Select(c => new[] { c.Anim, c.CubeX, c.CubeZ, (int)Math.Round((c.X - c.CubeX * 64) * 512), (int)Math.Round(c.Y), (int)Math.Round((c.Z - c.CubeZ * 64) * 512) }).ToList()
                : null,
            ArcJumps: raised && report.ArcRaised.Count > 0 ? report.ArcRaised.ToList() : null, Drivers: drivers.Count > 0 ? drivers : null,
            OpenAreas: report.OpenAreas.Count > 0 ? report.OpenAreas.ToList() : null,
            Mushrooms: scenes.Mushrooms is { Count: > 0 } mushrooms ? mushrooms : null, Penguins: scenes.Penguins is { Count: > 0 } penguins ? penguins : null,
            Oil: scenes.Oil is { Count: > 0 } oil ? oil : null, Fakes: scenes.Fakes is { Count: > 0 } fakes ? fakes : null,
            Steam: report.Steam.Count > 0 ? report.Steam.ToList() : null, Drips: report.Drips.Count > 0 ? report.Drips.ToList() : null,
            Jets: report.Jets.Count > 0 ? report.Jets.ToList() : null,
            Tunnels: raised && report.Tunnels.Count > 0
                ? report.Tunnels.Select(t => (t.Ceiling is { } c
                        ? new[] { report.RaisedSpan.IndexOf(t.From), report.RaisedSpan.IndexOf(t.To), (int)Math.Round(t.Roof), (int)Math.Round(c) }
                        : new[] { report.RaisedSpan.IndexOf(t.From), report.RaisedSpan.IndexOf(t.To), (int)Math.Round(t.Roof) })).Where(t => t[0] >= 0 && t[1] >= 0).ToList()
                : null,
            LavaBalls: raised && report.LavaBalls.Count > 0
                ? report.LavaBalls.Select(b => new[] { (int)Math.Round(b.X), (int)Math.Round(b.Y), (int)Math.Round(b.Z), report.RaisedSpan.IndexOf(b.From), report.RaisedSpan.IndexOf(b.To), b.Every, b.Phase })
                    .Where(b => b[3] >= 0 && b[4] >= 0).ToList()
                : null,
            Gunners: scenes.Gunners is { Count: > 0 } gunners
                ? gunners.Where(g => g[3] >= 0 && g[3] < report.Gunners.Count).Select(g => GunnerRecord(g, report.Gunners[g[3]])).ToList()
                : null,
            Roulette: raised && report.Roulette is { } wheel && report.RaisedSpan.IndexOf(wheel.Landing) is var landing and >= 0
                ? new[] { wheel.CubeX, wheel.CubeZ, (int)Math.Round(wheel.X), (int)Math.Round(wheel.Z), (int)Math.Round(wheel.Spin * 10), wheel.Ball, wheel.BallRadius, landing,
                          (int)Math.Round(wheel.HoleY), (int)Math.Round(wheel.Slope), (int)Math.Round(wheel.Steepen * 100), (int)Math.Round(wheel.Hole * 512),
                          (int)Math.Round(wheel.Rim * 512), (int)Math.Round(wheel.Outer * 512), (int)Math.Round(wheel.Under) }
                    .Concat(wheel.Turning).ToArray()
                : null,
            LavaFalls: report.LavaFalls.Count > 0 ? report.LavaFalls.ToList() : null,
            CrystalShards: raised && report.CrystalShards.Count > 0
                ? report.CrystalShards.Select(b => new[] { (int)Math.Round(b.X), (int)Math.Round(b.Y), (int)Math.Round(b.Z), report.RaisedSpan.IndexOf(b.From), report.RaisedSpan.IndexOf(b.To), b.Every, b.Phase, b.Balls })
                    .Where(b => b[3] >= 0 && b[4] >= 0).ToList()
                : null,
            LavaGeysers: raised && report.LavaGeysers.Count > 0
                ? report.LavaGeysers.Select(b => new[] { (int)Math.Round(b.X), (int)Math.Round(b.Y), (int)Math.Round(b.Z), report.RaisedSpan.IndexOf(b.From), report.RaisedSpan.IndexOf(b.To), b.Every, b.Phase, b.Balls })
                    .Where(b => b[3] >= 0 && b[4] >= 0).ToList()
                : null,
            BoostPanels: raised && report.BoostPanels.Count > 0 ? report.BoostPanels.ToList() : null,
            RouletteBalls: raised && report.Roulette is { Balls: > 1 } balled ? balled.Balls : null,
            RoulettePockets: raised && report.Roulette is { } pocketed ? pocketed.Pockets : null,
            GasMonsters: scenes.GasMonsters is { Count: > 0 } monsters
                ? monsters.Where(g => g[3] >= 0 && g[3] < report.GasMonsters.Count).GroupBy(g => g[3]).Select(pairs =>
                {
                    // (one line a monster: its own scene's actor first, then its actor in each scene it is seen from)
                    var s = report.GasMonsters[pairs.Key];
                    var home = pairs.OrderByDescending(g => g.Length > 4 ? g[4] : 1).ToList();
                    return new[] { home[0][0], home[0][1], home[0][2],
                            (int)Math.Round(s.X * 512), (int)Math.Round(s.Z * 512), (int)Math.Round(s.BiteX * 512), (int)Math.Round(s.BiteZ * 512), (int)Math.Round(s.BiteY), (int)Math.Round(s.FootY), s.FirstAnim }
                        .Concat(home.Skip(1).SelectMany(g => new[] { g[0], g[1] })).ToArray();
                }).ToList()
                : null,
            Guards: scenes.Guards is { Count: > 0 } guards
                ? guards.Where(g => g[2] >= 0 && g[2] < report.Guards.Count).GroupBy(g => g[2]).Select(pairs =>
                {
                    // (one line a guard: its post, its look, then its actor in each scene -- its own scene's first)
                    var s = report.Guards[pairs.Key];
                    var look = RaceTrackGuards.Of((int)s[0], (int)s[1]);
                    var strikes = Enumerable.Range(0, 4).Select(k => k < look.Strikes.Length ? look.Strikes[k] : -1);
                    var seen = pairs.OrderByDescending(g => g[3]).SelectMany(g => new[] { g[0], g[1] });
                    return new[] { (int)s[0], (int)Math.Round(s[2] * 512), (int)Math.Round(s[3] * 512), (int)Math.Round(s[4]),
                            (int)Math.Round(s[5] * 1000), (int)Math.Round(s[6] * 1000), (int)Math.Round(s[7] * 512), look.Idle, look.Walk, look.Hit }
                        .Concat(strikes).Concat(seen).ToArray();
                }).ToList()
                : null,
            Teleports: report.Teleports.Count > 0
                ? report.Teleports.Select(t => new[] { (int)Math.Round(t[0] * 512), (int)Math.Round(t[1] * 512), (int)Math.Round(t[2] * 512), (int)Math.Round(t[3] * 512),
                    (int)Math.Round(t[4]), (int)Math.Round(t[5]), (int)Math.Round(t[6] * 512), (int)Math.Round(t[8]), (int)Math.Round(t[7] * 512), (int)Math.Round(t[9]) }
                    // (the speed it comes out at the most and its passage's time, when the plan gives them: teleport='s eleventh and twelfth)
                    .Concat(t.Skip(10).Take(2).Select(v => (int)Math.Round(v))).ToArray()).ToList()
                : null,
            Frogs: report.Frogs.Count(f => f.Body >= 0) > 0 ? report.Frogs.Where(f => f.Body >= 0).Select(RaceTrackFrog.Line).ToList() : null,
            Hunters: scenes.Hunters is { Count: > 0 } hunters ? hunters.ToList() : null,
            Flyers: scenes.Flyers is { Count: > 0 } flyers
                ? flyers.Where(f => f[2] >= 0 && f[2] < report.Flyers.Count).Select(f =>
                    new[] { f[0], f[1], (int)Math.Round(report.Flyers[f[2]].Y) }
                        .Concat(report.Flyers[f[2]].Way.SelectMany(p => new[] { (int)Math.Round(p.X * 512), (int)Math.Round(p.Z * 512) })).ToArray()).ToList()
                : null,
            Spooks: report.Spooks.Count(s => s.Body >= 0) > 0 ? report.Spooks.Where(s => s.Body >= 0).Select(LBAAssembler.Terrain.Temple.TempleHalloween.Line).ToList() : null,
            RaisedLap: raised && report.RaisedBranches.Count > 0 ? report.RaisedLap : null,
            RaisedBranches: raised && report.RaisedBranches.Count > 0 ? report.RaisedBranches.ToList() : null,
            Dives: report.Dives.Count(d => d.Body >= 0) > 0 ? report.Dives.Where(d => d.Body >= 0).Select(LBAAssembler.Terrain.Temple.TempleHalloween.Line).ToList() : null,
            Scares: report.Scares.Count(s => s.Body >= 0) > 0 ? report.Scares.Where(s => s.Body >= 0).Select(LBAAssembler.Terrain.Temple.TempleHalloween.Line).ToList() : null,
            Haunt: report.HauntBodies is { } haunt ? new[] { haunt.Ghost, haunt.Icon } : null,
            GiantWebs: report.GiantWebs.Count(w => w.SpiderA >= 0) > 0 ? report.GiantWebs.Where(w => w.SpiderA >= 0).Select(LBAAssembler.Terrain.Temple.TempleHalloween.Line).ToList() : null,
            WebParts: report.GiantWebs.Count(w => w.SpiderA >= 0) > 0
                ? report.GiantWebs.Where(w => w.SpiderA >= 0).SelectMany((w, i) => LBAAssembler.Terrain.Temple.TempleHalloween.PartLines(w).Select(p => new[] { i }.Concat(p).ToArray())).ToList()
                : null,
            Marshal: report.Marshal,
            OnFoot: report.OnFoot ? true : null,
            RailOpens: report.RailOpens.Count > 0 ? report.RailOpens.ToList() : null,
            MapRoads: MapRoads(report), DoorsOff: scenes.DoorsOff, ActorsAt: scenes.ActorsAt, DeckLifts: scenes.DeckLifts);
    }

    // A machine gun on its knoll as the car file has it: its scene and actors, the muzzle (the gun stands 0.8 cells towards where it fires
    // from the knoll's middle, as RaceTrackScenes stands it; its muzzle 214 ahead of the gun's middle, 420 up), and where its bullets land.
    private static int[] GunnerRecord(int[] placed, GunnerSpot g)
    {
        double fx = g.FaceX - g.X, fz = g.FaceZ - g.Z, fl = Math.Max(1e-9, Math.Sqrt(fx * fx + fz * fz));
        fx /= fl; fz /= fl;
        // (the second gun 1.6 cells beside the first, as RaceTrackScenes stands it)
        var side = placed.Length > 4 && placed[4] > 0 ? 1.6 * 512 : 0;
        double mx = g.X * 512 + fx * (0.8 * 512 + 214) + fz * side, mz = g.Z * 512 + fz * (0.8 * 512 + 214) - fx * side, my = g.Top + 420;
        return placed.Take(3).Concat(new[] { (int)Math.Round(mx), (int)Math.Round(my), (int)Math.Round(mz) })
            .Concat(g.Targets.SelectMany(t => new[] { (int)Math.Round(t.X), (int)Math.Round(t.Z), (int)Math.Round(t.Y), (int)Math.Round(t.R) })).ToArray();
    }

    // The pit lanes as built (the report's "pit" roads), for the race's mini-map: [x, z, ...] world units, a point every cell or so
    private static List<int[]>? MapRoads(RaceTrackReport report)
    {
        var roads = new List<int[]>();
        foreach (var road in report.Roads.Where(r => r.Name == "pit" && r.Count > 1))
        {
            var step = Math.Max(1, road.Count / 200);
            var xz = new List<int>();
            for (var i = 0; i < road.Count; i += step) { xz.Add((int)Math.Round(road.X[i] * 512)); xz.Add((int)Math.Round(road.Z[i] * 512)); }
            if ((road.Count - 1) % step != 0) { xz.Add((int)Math.Round(road.X[^1] * 512)); xz.Add((int)Math.Round(road.Z[^1] * 512)); }
            roads.Add(xz.ToArray());
        }
        return roads.Count > 0 ? roads : null;
    }

    private static int[] LoopRecord(LoopInfo l)
    {
        int cx = (int)Math.Floor(l.X / 64), cz = (int)Math.Floor(l.Z / 64);
        var record = new[]
        {
            cx, cz, (int)Math.Round((l.X - cx * 64) * 512), (int)Math.Round(l.Y), (int)Math.Round((l.Z - cz * 64) * 512),
            (int)Math.Round(l.DirX * 1000), (int)Math.Round(l.DirZ * 1000), (int)Math.Round(l.Radius * 512), (int)Math.Round(l.Shift * 512), (int)Math.Round(l.Gap),
            (int)Math.Round(l.Band * 512),
        };
        // (a twelfth number, -1: the camera watches the ring from the side the car comes out on)
        return l.Camera < 0 ? record.Append(-1).ToArray() : record;
    }

    // The folder's RACETRACK.JSON, or null when there is none (or it can't be read: a track built before the file existed).
    // (kept while the file is the same -- its time and length -- as the Play button asks for it whenever the view moves, and a folder with
    // every island's track has a file of a megabyte and a half)
    private static (string Path, DateTime Time, long Length, TrackInfo? Info)? readCache;
    private static readonly object readLock = new();

    public static TrackInfo? ReadInfo(string gameDirectory)
    {
        try
        {
            var path = Path.Combine(gameDirectory, InfoFile);
            var file = new FileInfo(path);
            if (!file.Exists) return null;
            lock (readLock)
            {
                if (readCache is { } c && c.Path == file.FullName && c.Time == file.LastWriteTimeUtc && c.Length == file.Length) return c.Info;
                var info = JsonSerializer.Deserialize<TrackInfo>(File.ReadAllText(path));
                readCache = (file.FullName, file.LastWriteTimeUtc, file.Length, info);
                return info;
            }
        }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException)
        {
            DebugLog.Log($"RaceTrackService.ReadInfo: {error.Message}");
            return null;
        }
    }

    // Polar Island, for its track (RaceTrackIsland.Polar): when the originals the build starts from haven't got it (the folder's files, or
    // the copies an earlier build kept), the folder is put back to its originals and the island added to them (Polar.PolarIsland, from
    // the LBA1 folder in the settings; LBA1_DIR for the command line), so the copies the build keeps from now on have it. Putting the
    // track back later leaves the island. Returns lines for the log.
    private static List<string> EnsurePolar(string gameDirectory)
    {
        if (PolarInOriginals(gameDirectory)) return new List<string> { "Polar Island is in the folder: the track is built on it" };
        if (PolarProblem(gameDirectory) is { } problem) throw new InvalidDataException(problem);
        var lba1 = Lba1Folder();
        var log = new List<string>();
        if (AllFiles.Any(f => File.Exists(Path.Combine(gameDirectory, f + BackupSuffix))))
            log.Add("The folder put back to its originals first: " + Restore(gameDirectory));
        var added = LBAAssembler.Terrain.Polar.PolarIsland.Add(lba1, gameDirectory);
        log.Add($"Polar Island added to the folder from LBA1's scenes ({added.Count} steps: Tools > LBA2: Polar Island has the island on its own)");
        return log;
    }

    // About how long each island's track takes to build, in seconds (2026-10-11: the thirteen built together in 62 s, the Desert island's
    // ground the longest), and adding Polar Island to a folder: each one's share of the progress bar.
    private const double PolarAddSeconds = 4;
    // (and making the Emperor's palace's island: its rooms, furniture, domes and hill)
    private const double PalaceAddSeconds = 3.5;
    // (and making Island CX's: the control tower's halls under it)
    private const double CxTowerAddSeconds = 2;
    // (and the Temple of Bú's, from LBA1's scenes)
    private const double TempleAddSeconds = 2;
    private static double BuildSeconds(RaceTrackIsland island) =>
        island == RaceTrackIsland.Desert ? 16 : island == RaceTrackIsland.Citadel ? 8.3 : island == RaceTrackIsland.Knartas ? 8.1 : island == RaceTrackIsland.Otringal ? 8.4
        : island == RaceTrackIsland.Polar ? 5.4 : island == RaceTrackIsland.Volcano ? 2.5 : island == RaceTrackIsland.Palace ? 6 : island == RaceTrackIsland.Sendell || island == RaceTrackIsland.Moon ? 0.8 : 1.4;

    internal static string Lba1Folder() => Environment.GetEnvironmentVariable("LBA1_DIR") is { Length: > 0 } env ? env : EditorSettings.Current.Lba1Directory;

    // The Temple of Bú as Tools makes it (no road, no doorways, no Halloween), from the LBA1 folder in the settings; without one, taken out.
    // True when it is there.
    private static bool PlainTemple(string gameDirectory)
    {
        var lba1 = Lba1Folder();
        if (!LBAAssembler.Lba1.Lba1Game.IsInstalled(lba1)) { LBAAssembler.Terrain.Temple.TempleIsland.Remove(gameDirectory); return false; }
        LBAAssembler.Terrain.Temple.TempleIsland.Add(lba1, gameDirectory);
        return true;
    }

    // Why an island the build makes from LBA1's scenes (the Temple of Bú: `what` says so) can't be built, or null: there is no LBA1 game folder
    // (File > Settings; LBA1_DIR for the command line).
    public static string? Lba1Problem(string what)
    {
        var lba1 = Lba1Folder();
        if (LBAAssembler.Lba1.Lba1Game.IsInstalled(lba1)) return null;
        return $"{what}, so it needs the LBA1 game folder. " +
               (string.IsNullOrWhiteSpace(lba1) ? "No LBA1 folder is set: choose it under File > Settings (the folder with LBA1's SCENE.HQR, LBA_GRI.HQR, LBA_BLL.HQR, LBA_BRK.HQR and RESS.HQR) and build again."
                                                : $"The LBA1 folder in the settings ({lba1}) hasn't got LBA1's SCENE.HQR, LBA_GRI.HQR, LBA_BLL.HQR, LBA_BRK.HQR and RESS.HQR: choose the right folder under File > Settings and build again.");
    }

    // Why Polar Island's track can't be built into the folder, or null: the folder hasn't got the island and there is no LBA1 game folder
    // (File > Settings; LBA1_DIR for the command line) to make it from. The window asks before it builds anything -- a build that found
    // this out half-way changed nothing, and its message went by unseen ("it causes nothing to build", a user's report, 2026-10-11).
    public static string? PolarProblem(string gameDirectory)
    {
        if (PolarInOriginals(gameDirectory)) return null;
        var lba1 = Lba1Folder();
        if (LBAAssembler.Lba1.Lba1Game.IsInstalled(lba1)) return null;
        return "Polar Island's track is built on Polar Island, which isn't in this folder yet: the build makes it from LBA1's own scenes, so it needs the LBA1 game folder. " +
               (string.IsNullOrWhiteSpace(lba1) ? "No LBA1 folder is set: choose it under File > Settings (the folder with LBA1's SCENE.HQR, LBA_GRI.HQR, LBA_BLL.HQR, LBA_BRK.HQR and RESS.HQR) and build again."
                                                : $"The LBA1 folder in the settings ({lba1}) hasn't got LBA1's SCENE.HQR, LBA_GRI.HQR, LBA_BLL.HQR, LBA_BRK.HQR and RESS.HQR: choose the right folder under File > Settings and build again.");
    }

    // Whether the originals a build starts from have Polar Island: its files and its scenes.
    private static bool PolarInOriginals(string gameDirectory)
    {
        string Original(string f) { var p = Path.Combine(gameDirectory, f); return File.Exists(p + BackupSuffix) ? p + BackupSuffix : p; }
        if (!File.Exists(Original(LBAAssembler.Terrain.Polar.PolarTerrain.IleFile)) || !File.Exists(Original(LBAAssembler.Terrain.Polar.PolarTerrain.OblFile))) return false;
        try
        {
            var path = Original("SCENE.HQR");
            var count = HqrArchive.CountEntries(path);
            var scenes = HqrArchive.Open(path);
            var island = RaceTrackIsland.Polar;
            // (as this version numbers its scenes, each the scene of its own cube; an earlier version's, 229-240, is added again)
            for (var n = LBAAssembler.Terrain.Polar.PolarScenes.OldFirstScene; n < island.FirstScene && n + 1 < count; n++)
            {
                var old = scenes.Read(n + 1);
                if (old.Length > 0 && old[0] == island.IslandByte) return false;
            }
            // (and as this version builds it: an earlier build's island, kept in the originals, is made again -- 2026-10-27, the user tested the
            // island in the editor and found no grobo: the folder's originals had the island from before him. Its sea ring of cubes and the
            // grobo in its start scene are this version's)
            var ile = IslandFile.Load(Original(LBAAssembler.Terrain.Polar.PolarTerrain.IleFile));
            if (ile.CubeAt(LBAAssembler.Terrain.Polar.PolarTerrain.CubeX0, LBAAssembler.Terrain.Polar.PolarTerrain.CubeZ0) is null) return false;
            bool? numbered = null;
            var grobo = false;
            for (var n = island.FirstScene; n <= island.LastScene && n + 1 < count; n++)
            {
                var record = scenes.Read(n + 1);
                if (record.Length == 0 || record[0] != island.IslandByte) continue;
                var model = LBAAssembler.Scenes.SceneSerializer.Parse(LBAAssembler.Scenes.SceneGame.Lba2, record);
                numbered ??= LBAAssembler.Terrain.Polar.PolarScenes.SceneOf(model.CubeX, model.CubeY) == n;
                grobo |= model.Actors.Any(a => a.Entity == LBAAssembler.Terrain.Polar.PolarGrobo.Entity);
            }
            return numbered == true && grobo;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException) { }
        return false;
    }

    // Puts the changed files back from the copies made by the first build and removes the copies (and RACETRACK.JSON, RACECARS.JSON).
    public static string Restore(string gameDirectory)
    {
        if (!HasBackups(gameDirectory)) return "There is no race track build to undo in this folder.";
        var back = new List<string>();
        foreach (var f in AllFiles)
        {
            var backup = Path.Combine(gameDirectory, f + BackupSuffix);
            if (!File.Exists(backup)) continue;
            CopyWritable(backup, Path.Combine(gameDirectory, f));
            back.Add(f);
        }
        foreach (var f in back) File.Delete(Path.Combine(gameDirectory, f + BackupSuffix));
        // (an island the build made has no originals: it goes -- the Palace island, an island of its own too, made again without its track)
        var gone = SendellWell.Remove(gameDirectory) ? $" {SendellWell.IleFile} and {SendellWell.OblFile}, which the build made, are gone." : "";
        // (the Temple of Bú's island, made from LBA1's scenes with the track's doorways cut: the island again as Tools makes it -- or gone, with no
        // LBA1 folder to make it from)
        if (LBAAssembler.Terrain.Temple.TempleIsland.IsInstalled(gameDirectory))
            gone += PlainTemple(gameDirectory) ? $" {LBAAssembler.Terrain.Temple.TempleIsland.IleFile} is the Temple of Bú again, without its track."
                                               : $" {LBAAssembler.Terrain.Temple.TempleIsland.IleFile} and {LBAAssembler.Terrain.Temple.TempleIsland.OblFile}, which the build made, are gone (no LBA1 folder to make them again).";
        if (File.Exists(Path.Combine(gameDirectory, LBAAssembler.Terrain.Palace.PalaceIsland.IleFile)))
        {
            LBAAssembler.Terrain.Palace.PalaceIsland.Build(gameDirectory, gameDirectory);
            gone += $" {LBAAssembler.Terrain.Palace.PalaceIsland.IleFile} is the Palace island again, without its track.";
        }
        // (and Island CX's: the control tower's lower level under it again, as Tools makes it)
        if (File.Exists(Path.Combine(gameDirectory, LBAAssembler.Terrain.ControlTower.ControlTowerIsland.IleFile)))
        {
            LBAAssembler.Terrain.ControlTower.ControlTowerIsland.Build(gameDirectory, gameDirectory);
            gone += $" {LBAAssembler.Terrain.ControlTower.ControlTowerIsland.IleFile} is Island CX with its control tower's lower level again, without its track.";
        }
        foreach (var made in new[] { InfoFile, RaceTrackCharacterCars.CatalogueFile })
            if (File.Exists(Path.Combine(gameDirectory, made))) File.Delete(Path.Combine(gameDirectory, made));
        return $"The original {string.Join(", ", back)} are back.{gone}";
    }
}
