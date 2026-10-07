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
    // What every build changes, whichever island it is on; the island's own ground and decor bodies are added to these (RaceTrackIsland).
    public static readonly string[] Files = { "SCENE.HQR" };
    // (and the holomap's pictures and arrows, and the texts the story adds to)
    public static readonly string[] ExtraFiles = { "ANIM.HQR", "RESS.HQR", "BODY.HQR", RaceTrackHolomap.File, "TEXT.HQR", "OBJFIX.HQR" };
    public static string[] FilesFor(RaceTrackIsland island) => Files.Concat(island.KeptFiles).ToArray();
    public static string[] AllFiles => Files.Concat(ExtraFiles).Concat(RaceTrackIsland.All.SelectMany(i => i.KeptFiles)).Distinct().ToArray();
    public const string InfoFile = "RACETRACK.JSON";

    public sealed record BuildResult(bool Ok, string Summary, List<string> Log, RaceTrackReport? Report);

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
        // RailCamera: the camera rides the raised road behind the car (RaceTrackPlan.RailCamera: cells behind, units up, cells ahead).
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double[]? RailCamera = null,
        // Others: the tracks of the other islands built into the folder with this one, each a record of its own (with no Others).
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<TrackInfo>? Others = null,
        // Loops: the vertical loops (RaceTrackPlan.Loops), each [cube x, cube z, x, y, z (the ring's foot, cube-local), the lap's way there
        // (x and z, a thousand long), the ring's radius, how far across the car comes out (world units), the gap at its top (degrees), and
        // the ring's band from its middle to its rails (world units: the car is steered across it)].
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Loops = null,
        // JumpCameras: where the camera stands while the car flies a drop (RaceTrackBuilder.PlaceJumpCameras), each [the flight's generic
        // animation, cube x, cube z, x, y, z (cube-local)].
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? JumpCameras = null,
        // ArcJumps: the jumps the race-track mode carries the car over (RaceTrackPlan.ArcJumps), each [its ramp's foot, its lip, the
        // landing lip, the landing hill's foot] as places in Raised; CubeScenes: the island's outside scenes, each [cube x, cube z, scene],
        // for the cube changes such a flight makes itself.
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? ArcJumps = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? CubeScenes = null,
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
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] List<int[]>? Jets = null);
    // A dreamt sprint: its finish line (a line as the start line is), the car's top speed for it (km/h: the setup's gears scaled to it),
    // the intro and the loss's line (texts of the island's), and where a win wakes Twinsen up -- the scene, the text of its island's its
    // actor says there.
    public sealed record DreamInfo(StartLineInfo Finish, int TopKmh, int IntroText, int LoseText, int WakeScene, int WakeText, int WakeActor,
        // (since 2026-10-06, later: the game variable a win sets, which scene 0's opening wakes Twinsen up in his bed by -- it says the
        // wake line itself then, and WakeText is -1)
        int WinVar = -1,
        // (since the island was made twice its size: how long after the finish line -- a jump's lip -- Twinsen wakes up, in mid-flight; 0, the
        // car stops at the line and he wakes up 3.5 s later)
        int WakeFlight = 0);
    public sealed record RivalInfo(string Name, List<int[]> Path, int Grid, Dictionary<int, int> Actors);
    // A driver: its name, its line ([x, z, y, speed, bend radius] as Path's), how many points before the start line it starts without a
    // grid, the scenes' copies of its car (none for a time to beat: Ghost), its character (Top, Grip: shares of the player's car's top
    // speed and cornering), whether it is the one to beat (Main), the motorbike, and its car's body (the racer entity's generic body).
    public sealed record DriverInfo(string Name, List<int[]> Path, int Grid, Dictionary<int, int> Actors, double Top, double Grip, bool Main, bool Bike, bool Ghost, int Body = -1);
    // The story's: the tired line everyone says until Twinsen sleeps (a text of Citadel Island's), and the holomap arrow to the town
    // circuit's start line.
    // (Seller: Celebration Island's lava lake, whose race the souvenir seller has to lose: RaceTrackStory.ApplyCelebration)
    // (RaphTime: Raph's line with the time to beat in it, RACEMOD.CPP beat_text=; RaphPark: where his car stands once he has stopped lapping
    // the storm track, [scene, x, y, z, turn] -- RaceTrackStory)
    public sealed record StoryInfo(int TiredText, int TownArrow, bool Seller = false, int RaphTime = -1, int[]? RaphPark = null);

    // Play's race-track mode on a folder with a race track built: writes the engine's car file (the car setup in the settings, and the track's
    // start line, checkpoints and opponents from RACETRACK.JSON: `track`, one of the folder's tracks as RaceFor picks it, or else the
    // first); null for any other folder, which plays the game as it is.
    // `story`: the game played as a game (a new game, or a scene entered as it is, not a race started on its line): every track of the
    // folder in a set, each raced where and when the game is, with the story's gates and results (RaceCarEngineFile.WriteStorySet).
    public static Action<string>? CarFileWriter(string gameDirectory, TrackInfo? track = null, bool story = false)
    {
        if (!HasBackups(gameDirectory)) return null;
        var car = EditorSettings.Current.RaceCar.Clone();
        car.DriveAsKey = RaceCarEngineFile.DriveAsLine(gameDirectory, car.DriveAs);
        if (story && ReadInfo(gameDirectory) is { } all) return path => car.WriteStorySet(path, all);
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
    public static BuildResult Build(string gameDirectory, IReadOnlyList<TrackBuild> tracks)
    {
        if (Problem(gameDirectory) is { } problem) return new(false, problem, new(), null);
        tracks = tracks.DistinctBy(t => t.Options.Island.IleFile, StringComparer.OrdinalIgnoreCase).ToList();
        if (tracks.Count == 0) return new(false, "No island is chosen: nothing was changed.", new(), null);
        try
        {
            // (Polar Island's track needs the island: added to the originals when they haven't got it)
            var polar = tracks.Any(t => t.Options.Island.Dream) ? EnsurePolar(gameDirectory) : new List<string>();
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
            // (an island the build makes -- Sendell's Well -- is made afresh, its files and its scene, from those originals; one this
            // build leaves out goes)
            var made = new List<string>();
            if (tracks.Any(t => t.Options.Island.Created)) made.AddRange(SendellWell.Install(gameDirectory));
            else SendellWell.Remove(gameDirectory);

            var session = new BuildSession();
            var log = new List<string>();
            var starts = new List<string>();
            RaceTrackReport? first = null;
            foreach (var track in tracks)
            {
                var options = track.Options;
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
        public bool CharacterCars, Baldino, SmallCars, Oil, CastCars;
        public int? OilIcon, SuperJetModel, TwinsenSmall;
        public List<TrackInfo> Tracks { get; } = new();
    }

    // Builds the track into the game folder's files, which are the originals when this runs (the island's own ground is read from
    // `islandSource`: the original kept beside it, or the file itself) -- or, for the second and later tracks of a `session`, the originals
    // with the tracks before it built in. The menu's Build and the command line's buildtrack both come here.
    // An island whose other-weather file has a track of its own (Citadel Island: RaceTrackIsland.TwinPlanResource, or `twinPlan`) gets
    // both: `plan` in its own file, with no opponents, and the other in the twin, which carries the race -- the opponents and the story.
    public static Built BuildFiles(string gameDirectory, string islandSource, RaceTrackPlan plan, RaceTrackOptions options, RaceTrackPlan? twinPlan = null, BuildSession? session = null)
    {
        session ??= new BuildSession();
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
        var extra = Prepare(gameDirectory, options);
        extra.InsertRange(0, scaled);
        var themed = RaceTrackTextures.Import(island, options.Island, gameDirectory);
        options.Theme = themed.Theme;
        if (themed.Log.Length > 0) extra.Add(themed.Log);
        var report = RaceTrackBuilder.Build(island, plan, options);
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
        island.Save(Path.Combine(gameDirectory, options.Island.IleFile));
        AppendBodies(Path.Combine(gameDirectory, options.Island.OblFile), report, options);
        var twin = BuildTwin(gameDirectory, twinPlan ?? plan, twinPlan is not null, twinOptions, options, report);
        if (twin is not null) extra.Add(twin.Log);
        extra.AddRange(Finish(gameDirectory, report, options, twin, session));
        var own = twin is { Own: true } ? twin : null;
        if (RaceTrackScenes.AddScene(gameDirectory, options.Island) is { } added) extra.Add(added);
        var scenes = RaceTrackScenes.Apply(gameDirectory, report, options, own is null ? null : (own.Report, own.Options));
        var story = Story(gameDirectory, report, options, own, scenes);
        extra.AddRange(story.Log);
        // (a dreamt sprint's texts, and where it ends)
        DreamInfo? dream = null;
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
        var report = RaceTrackBuilder.Build(twin, plan, twinOptions);
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
        if (racers.Any(d => !d.Bike && !d.Ghost && d.Body == RaceTrackBaldinoCar.Generic) && !session.Baldino) { log.Add(RaceTrackBaldinoCar.Install(gameDirectory).Log); session.Baldino = true; }
        // the cars after the game's characters, three or more for each island: in the game's files for whoever is to drive them (no actor
        // has one yet)
        if (!session.CharacterCars) { log.AddRange(RaceTrackCharacterCars.Install(gameDirectory).Log); session.CharacterCars = true; }
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
            // (and its drum in the item box)
            var (icon, iconLog) = RaceTrackOil.InstallIcon(gameDirectory);
            log.Add(iconLog);
            session.OilIcon = icon;
            // ... and the super jet-pack the car turns into
            var (jet, jetLog) = RaceTrackSuperJet.Install(gameDirectory);
            log.Add(jetLog);
            session.SuperJetModel = jet;
            session.Oil = true;
        }
        return log;
    }

    // RACETRACK.JSON: the crossing style and the start line, for Play; and, for a twin with a track of its own, that track as Twin. The
    // tracks built before this one in `session` stay in it: the first is the file's own record, the rest its Others.
    public static void WriteInfo(string gameDirectory, RaceTrackReport report, RaceTrackOptions options, RaceTrackScenes.Result scenes, TwinTrack? twin = null, BuildSession? session = null,
        StoryInfo? story = null, DreamInfo? dream = null)
    {
        session ??= new BuildSession();
        var info = Info(report, options, scenes) with { Story = story, OilIcon = session.OilIcon, SuperJetModel = session.SuperJetModel, TwinsenSmall = session.TwinsenSmall, Dream = dream };
        if (info.ArcJumps is not null) info = info with { CubeScenes = RaceTrackScenes.CubeScenes(gameDirectory, options.Island) };
        if (twin is { Own: true } && scenes.Twin is { } twinScenes)
        {
            // (the story belongs to the twin's track, which carries the race; either race clears its arrow once Twinsen drives)
            var fine = Info(twin.Report, twin.Options, twinScenes);
            // (the town circuit's own arrow, which the bed switches on)
            if (story is not null) fine = fine with { StoryArrow = story.TownArrow };
            info = info with { Twin = fine with { OilIcon = session.OilIcon, SuperJetModel = session.SuperJetModel, TwinsenSmall = session.TwinsenSmall } };
        }
        session.Tracks.Add(info);
        var all = session.Tracks[0] with { Others = session.Tracks.Count > 1 ? session.Tracks.Skip(1).ToList() : null };
        File.WriteAllText(Path.Combine(gameDirectory, InfoFile), JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
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
            drivers.Add(new DriverInfo(d.Name, Points(report.DriverPaths[k]), 4 * (k + 1), actors, d.Line.Top, d.Line.Grip, d.Main, d.Bike, d.Ghost, d.Bike ? -1 : d.Body));
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
            Mushrooms: scenes.Mushrooms is { Count: > 0 } mushrooms ? mushrooms : null, Penguins: scenes.Penguins is { Count: > 0 } penguins ? penguins : null,
            Oil: scenes.Oil is { Count: > 0 } oil ? oil : null,
            Steam: report.Steam.Count > 0 ? report.Steam.ToList() : null, Drips: report.Drips.Count > 0 ? report.Drips.ToList() : null,
            Jets: report.Jets.Count > 0 ? report.Jets.ToList() : null);
    }

    private static int[] LoopRecord(LoopInfo l)
    {
        int cx = (int)Math.Floor(l.X / 64), cz = (int)Math.Floor(l.Z / 64);
        return new[]
        {
            cx, cz, (int)Math.Round((l.X - cx * 64) * 512), (int)Math.Round(l.Y), (int)Math.Round((l.Z - cz * 64) * 512),
            (int)Math.Round(l.DirX * 1000), (int)Math.Round(l.DirZ * 1000), (int)Math.Round(l.Radius * 512), (int)Math.Round(l.Shift * 512), (int)Math.Round(l.Gap),
            (int)Math.Round(l.Band * 512),
        };
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
        var lba1 = Environment.GetEnvironmentVariable("LBA1_DIR") is { Length: > 0 } env ? env : EditorSettings.Current.Lba1Directory;
        if (!LBAAssembler.Lba1.Lba1Game.IsInstalled(lba1))
            throw new InvalidDataException("Polar Island's track is built on Polar Island, which is made from LBA1's own scenes: choose the LBA1 game folder under File > Settings first.");
        var log = new List<string>();
        if (AllFiles.Any(f => File.Exists(Path.Combine(gameDirectory, f + BackupSuffix))))
            log.Add("The folder put back to its originals first: " + Restore(gameDirectory));
        var added = LBAAssembler.Terrain.Polar.PolarIsland.Add(lba1, gameDirectory);
        log.Add($"Polar Island added to the folder from LBA1's scenes ({added.Count} steps: Tools > LBA2: Polar Island has the island on its own)");
        return log;
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
            for (var n = island.FirstScene; n <= island.LastScene && n + 1 < count; n++)
            {
                var record = scenes.Read(n + 1);
                if (record.Length == 0 || record[0] != island.IslandByte) continue;
                var model = LBAAssembler.Scenes.SceneSerializer.Parse(LBAAssembler.Scenes.SceneGame.Lba2, record);
                return LBAAssembler.Terrain.Polar.PolarScenes.SceneOf(model.CubeX, model.CubeY) == n;
            }
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
        // (an island the build made has no originals: it goes)
        var gone = SendellWell.Remove(gameDirectory) ? $" {SendellWell.IleFile} and {SendellWell.OblFile}, which the build made, are gone." : "";
        foreach (var made in new[] { InfoFile, RaceTrackCharacterCars.CatalogueFile })
            if (File.Exists(Path.Combine(gameDirectory, made))) File.Delete(Path.Combine(gameDirectory, made));
        return $"The original {string.Join(", ", back)} are back.{gone}";
    }
}
