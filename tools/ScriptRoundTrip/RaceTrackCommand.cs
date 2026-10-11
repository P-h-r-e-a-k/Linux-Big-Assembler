using LBAAssembler;
using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// Builds a race track into a copy of an island file and draws the result.
//   buildtrack <plan.json> <pristine folder> <game folder> [png] [scale]      RT_ISLAND=Desert island|Citadel Island picks the island
// The files the track changes (DESERT.ILE, DESERT.OBL, SCENE.HQR, and for a jump ANIM.HQR and RESS.HQR) are copied from the pristine folder to the game
// folder first, so a build always starts clean. RT_CROSSING=Bridge|Jump|Viaduct|Level picks another crossing style than the island's own
// (RaceTrackIsland.Crossing: the Desert island's jump, the town circuit's bridge). For Citadel Island the plan is
// the storm file's (CITADEL: citadel_storm_track_plan.json) and CITABAU gets the town circuit built into the program (RT_TWINPLAN=<plan> for
// another); RT_DUMP / RT_TWINDUMP write each lap's centre line.
internal static class RaceTrackCommand
{
    public static int Run(string[] args)
    {
        var plan = RaceTrackPlan.Load(args[1]);
        var pristine = args[2]; var game = args[3];
        var where = RaceTrackIsland.ByName(Environment.GetEnvironmentVariable("RT_ISLAND") ?? RaceTrackIsland.Desert.Name);
        foreach (var f in RaceTrackService.AllFiles)
            if (File.Exists(Path.Combine(pristine, f))) RaceTrackService.CopyWritable(Path.Combine(pristine, f), Path.Combine(game, f));
        var options = RaceTrackOptions.For(where);
        if (Environment.GetEnvironmentVariable("RT_CLEARANCE") is { } rc) options.RoadBridgeClearance = double.Parse(rc);
        if (Environment.GetEnvironmentVariable("RT_TILELEN") is { } tl) options.RoadBridgeTileLength = double.Parse(tl);
        if (Environment.GetEnvironmentVariable("RT_CROSSING") is { } cs) options.Crossing = Enum.Parse<CrossingStyle>(cs, ignoreCase: true);
        if (Environment.GetEnvironmentVariable("RT_JUMPANGLE") is { } ja) options.JumpCrossingAngle = double.Parse(ja, System.Globalization.CultureInfo.InvariantCulture);
        if (Environment.GetEnvironmentVariable("RT_JUMPGAP") is { } jg) options.JumpGap = double.Parse(jg, System.Globalization.CultureInfo.InvariantCulture);
        // (RT_TWINPLAN: a plan file for the island's other-weather file instead of the one built into the program)
        var twinPlan = Environment.GetEnvironmentVariable("RT_TWINPLAN") is { Length: > 0 } tp ? RaceTrackPlan.Load(tp) : null;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var built = RaceTrackService.BuildFiles(game, Path.Combine(game, where.IleFile), plan, options, twinPlan);
        var (report, scenes, extra) = (built.Report, built.Scenes, built.Log);
        if (Environment.GetEnvironmentVariable("RT_DUMP") is { Length: > 0 } dump)
            File.WriteAllLines(dump, report.LapX.Select((x, i) => FormattableString.Invariant($"{x:0.###},{report.LapZ[i]:0.###}")));
        if (Environment.GetEnvironmentVariable("RT_TWINDUMP") is { Length: > 0 } twinDump && built.Twin is { Own: true } own)
            File.WriteAllLines(twinDump, own.Report.LapX.Select((x, i) => FormattableString.Invariant($"{x:0.###},{own.Report.LapZ[i]:0.###}")));
        foreach (var l in extra) Console.WriteLine("  " + l);
        foreach (var l in scenes.Log) Console.WriteLine("  " + l);
        Console.WriteLine($"  {scenes.ActorsRemoved} actors removed from {scenes.ScenesChanged} scenes");
        Console.WriteLine($"built in {watch.Elapsed.TotalSeconds:0.0}s: lap {report.Length:0} cells, {report.Vertices} vertices levelled, {report.Cells} cells painted, {report.DecorsRemoved} props and {report.SolidDecorsRemoved} solid decors removed");
        foreach (var (a, b) in report.BridgeSpans) Console.WriteLine($"  bridge span: points {a}..{b}");
        foreach (var r in report.Removed.Where(r => r.Kind == "solid")) Console.WriteLine($"  removed solid decor: body {r.Body} in cube ({r.CubeX},{r.CubeZ})");
        foreach (var note in report.Notes) Console.WriteLine("  " + note);
        Console.WriteLine($"  {report.Arrows.Count} arrows");
        if (Environment.GetEnvironmentVariable("RT_ARROWS") == "1") foreach (var a in report.Arrows) Console.WriteLine($"  arrow at cell ({a[0]:0.0}, {a[1]:0.0}) heading ({a[2]:0.00}, {a[3]:0.00})");
        foreach (var c in report.Crossings) Console.WriteLine($"  crossing at cell ({c.X:0.0}, {c.Z:0.0}), height {c.Y:0}, angle {c.Angle:0} degrees");
        foreach (var b in report.BridgeCoords) Console.WriteLine($"  bridge from ({b.X0:0.0}, {b.Z0:0.0}) to ({b.X1:0.0}, {b.Z1:0.0})");
        foreach (var l in report.StartLine) Console.WriteLine($"  start line at cell ({l.X:0.0}, {l.Z:0.0}), height {l.Y:0}, heading ({l.DirX:0.00}, {l.DirZ:0.00})");
        foreach (var l in report.Placed) Console.WriteLine("  placed " + l);
        if (report.RoadBridge is { } rb) Console.WriteLine($"  road bridge: cell ({rb.X:0.0},{rb.Z:0.0}) dir ({rb.DirX:0.00},{rb.DirZ:0.00}) height {rb.Height:0} size {rb.Width / 512:0.#}x{rb.Length / 512:0.#} cells, deck body {options.DeckBodyIndex}");
        if (built.Twin is { Own: true } twin)
        {
            var t = twin.Report;
            Console.WriteLine($"{where.TwinIleFile}'s own track: lap {t.Length:0} cells, {t.Vertices} vertices levelled, {t.Cells} cells painted, {t.DecorsRemoved} props and {t.SolidDecorsRemoved} solid decors removed");
            foreach (var note in t.Notes) Console.WriteLine("  " + note);
            foreach (var l in t.StartLine) Console.WriteLine($"  start line at cell ({l.X:0.0}, {l.Z:0.0}), height {l.Y:0}, heading ({l.DirX:0.00}, {l.DirZ:0.00})");
            foreach (var l in t.Placed) Console.WriteLine("  placed " + l);
            if (t.RoadBridge is { } trb) Console.WriteLine($"  road bridge: cell ({trb.X:0.0},{trb.Z:0.0}) dir ({trb.DirX:0.00},{trb.DirZ:0.00}) height {trb.Height:0} size {trb.Width / 512:0.#}x{trb.Length / 512:0.#} cells, deck body {twin.Options.DeckBodyIndex}");
        }
        if (args.Length > 4)
        {
            var dir = game;
            var scale = args.Length > 5 ? int.Parse(args[5]) : 3;
            var island = IslandFile.Load(Path.Combine(game, where.IleFile));
            var renderer = new IslandMapRenderer(island, IslandMapRenderer.LoadPalette(dir, Path.GetFileNameWithoutExtension(where.IleFile)), scale);
            renderer.RenderAll(MapView.Terrain);
            PngWriter.Write(args[4], renderer.Pixels, renderer.PixelWidth, renderer.PixelHeight);
            Console.WriteLine($"{args[4]}: {renderer.PixelWidth}x{renderer.PixelHeight}");
        }
        return 0;
    }

    // buildtwice <pristine> <game folder> <island 1> <island 2>: two builds of the built-in plans in one process, as the race track window
    // does when one track is built after another, each from the pristine files; prints each build's start lines.
    // (RT_MENU=1: the way the menu builds -- RaceTrackService.Build, keeping the originals beside the files and building from them -- the
    // game folder given its pristine files once, first)
    public static int BuildTwice(string[] args)
    {
        var pristine = args[1]; var game = args[2];
        var menu = Environment.GetEnvironmentVariable("RT_MENU") == "1";
        if (menu)
        {
            foreach (var f in Directory.GetFiles(game, "*" + RaceTrackService.BackupSuffix)) File.Delete(f);
            foreach (var f in RaceTrackService.AllFiles.Append("LBA2.HQR"))
                if (File.Exists(Path.Combine(pristine, f))) RaceTrackService.CopyWritable(Path.Combine(pristine, f), Path.Combine(game, f));
        }
        foreach (var name in args.Skip(3))
        {
            var where = RaceTrackIsland.ByName(name);
            var options = RaceTrackOptions.For(where);
            RaceTrackReport report;
            if (menu)
            {
                var result = RaceTrackService.Build(game, RaceTrackPlan.Built(where), options);
                Console.WriteLine($"{where.Name}: {result.Summary} {result.Log.FirstOrDefault()}");
                report = result.Report!;
            }
            else
            {
                foreach (var f in RaceTrackService.AllFiles)
                    if (File.Exists(Path.Combine(pristine, f))) RaceTrackService.CopyWritable(Path.Combine(pristine, f), Path.Combine(game, f));
                report = RaceTrackService.BuildFiles(game, Path.Combine(game, where.IleFile), RaceTrackPlan.Built(where), options).Report;
            }
            var info = RaceTrackService.ReadInfo(game)!;
            Console.WriteLine($"{where.Name}: start {info.StartLine}");
            Console.WriteLine($"  twin start {info.Twin?.StartLine}");
            foreach (var l in report.StartLine) Console.WriteLine($"  report start line ({l.X:0.00}, {l.Z:0.00}) dir ({l.DirX:0.000}, {l.DirZ:0.000}), square {report.StartLineSquare}");
        }
        return 0;
    }

    // buildhere <game folder> <island>...: the islands' tracks built into the folder as it is -- as the race track window's Build does, its
    // backups and all (2026-10-06: Polar Island at twice its size over a folder with the earlier island and its track)
    public static int BuildHere(string[] args)
    {
        var game = args[1];
        var tracks = args.Skip(2).Select(RaceTrackIsland.ByName).Select(where => new RaceTrackService.TrackBuild(RaceTrackPlan.Built(where), RaceTrackOptions.For(where))).ToList();
        var result = RaceTrackService.Build(game, tracks);
        Console.WriteLine(result.Summary);
        foreach (var l in result.Log) Console.WriteLine("  " + l);
        return result.Ok ? 0 : 1;
    }

    // buildtogether <pristine> <game folder> <island>...: the islands' tracks (their built-in plans) built into one folder by one build, as
    // the race track window does with several islands ticked -- the game folder given its pristine files first, its backups removed -- and
    // for each track the start scene and line RACETRACK.JSON has. RT_CROSSING picks the crossing style of the ones that take it.
    public static int BuildTogether(string[] args)
    {
        var pristine = args[1]; var game = args[2];
        foreach (var f in Directory.GetFiles(game, "*" + RaceTrackService.BackupSuffix)) File.Delete(f);
        foreach (var f in RaceTrackService.AllFiles.Append("LBA2.HQR"))
            if (File.Exists(Path.Combine(pristine, f))) RaceTrackService.CopyWritable(Path.Combine(pristine, f), Path.Combine(game, f));
        var tracks = args.Skip(3).Select(RaceTrackIsland.ByName).Select(where =>
        {
            var options = RaceTrackOptions.For(where);
            if (Environment.GetEnvironmentVariable("RT_CROSSING") is { } cs) options.Crossing = Enum.Parse<CrossingStyle>(cs, ignoreCase: true);
            return new RaceTrackService.TrackBuild(RaceTrackPlan.Built(where), options);
        }).ToList();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = RaceTrackService.Build(game, tracks);
        Console.WriteLine($"{result.Summary} ({watch.Elapsed.TotalSeconds:0.0} s)");
        if (Environment.GetEnvironmentVariable("RT_LOG") == "1") foreach (var l in result.Log) Console.WriteLine("  " + l);
        foreach (var t in RaceTrackService.Tracks(RaceTrackService.ReadInfo(game)))
            Console.WriteLine($"  {t.Island}: start scene {t.StartScene}, line {t.StartLine}" + (t.Twin is { } tw ? $"; twin start scene {tw.StartScene}" : ""));
        return result.Ok ? 0 : 1;
    }

    // herostart <game folder> <cell x> <cell z> [turn]: puts Twinsen's start in the scene of the cube that holds an island cell, on the ground there
    // (for looking at a place of the track in the game).
    // racecarfile <game folder> <out file> [pace]: (RT_RACE=<island file>: the track of that island, of a folder with several) the engine's car setup file Play would write for that folder (the default car, the
    // folder's RACETRACK.JSON: start line, checkpoints, the opponent's line beside it as racepath.txt), for headless tests. RT_WEATHER=rain:
    // Citadel Island left raining (its storm track).
    // baldinocar <game folder> <scratch folder>: Baldino's car built from the game folder's BODY.HQR and installed into copies of its BODY.HQR and
    // RESS.HQR in the scratch folder (for a look at it: BodyPipeline hqrpreview <scratch>\BODY.HQR <index>)
    public static int BaldinoCar(string[] args)
    {
        Directory.CreateDirectory(args[2]);
        CopyWritable(args[1], args[2], "BODY.HQR", "RESS.HQR");
        var (index, log) = RaceTrackBaldinoCar.Install(args[2]);
        Console.WriteLine(log);
        return index > 0 ? 0 : 1;
    }

    // charactercars <game folder> <scratch folder>: the Queen's, the Emperor's and Zoe's cars (RaceTrackCharacterCars) built from the game
    // folder's BODY.HQR and installed into copies of its BODY.HQR and RESS.HQR in the scratch folder (for a look at them: BodyPipeline
    // carviews <scratch>\BODY.HQR <index> <game folder> <png>)
    public static int CharacterCars(string[] args)
    {
        Directory.CreateDirectory(args[2]);
        CopyWritable(args[1], args[2], "BODY.HQR", "RESS.HQR");
        // each car on its own first, so that one that does not fit the engine's limits says so and the others can still be looked at
        var bodies = HqrArchive.Open(Path.Combine(args[1], "BODY.HQR"));
        var racer = LbaBodyStudio.Body.Read(bodies.Read(RaceTrackBaldinoCar.RacerBody), 2);
        var good = new List<RaceTrackCharacterCars.Car>(); var failed = 0;
        foreach (var car in RaceTrackCharacterCars.All)
        {
            try { car.Build(LbaBodyStudio.Body.Read(bodies.Read(car.Character), 2), racer); good.Add(car); }
            catch (Exception e)
            {
                failed++;
                var counts = "";
                Console.WriteLine($"FAILED {car.Name} (body {car.Generic}): {e.Message}{counts}");
            }
        }
        var (_, log) = RaceTrackCharacterCars.Install(args[2], good);
        foreach (var line in log) Console.WriteLine(line);
        return failed;
    }

    // castcars <game folder> <scratch folder> [car number...]: the cast's cars (RaceTrackCharacterCars.Cast) installed into a copy of the
    // folder's BODY.HQR and RESS.HQR (a folder whose race tracks are built: the cars made by hand are in it), with RACECARS.JSON; each car's
    // points and polygons, and any not made.
    public static int CastCars(string[] args)
    {
        Directory.CreateDirectory(args[2]);
        CopyWritable(args[1], args[2], "BODY.HQR", "RESS.HQR");
        var only = args.Length > 3 ? args.Skip(3).Select(int.Parse).ToList() : null;
        foreach (var line in RaceTrackCharacterCars.InstallCast(args[2], only)) Console.WriteLine(line);
        var bodies = HqrArchive.Open(Path.Combine(args[2], "BODY.HQR"));
        foreach (var e in RaceTrackCharacterCars.Catalogue(args[2]).Where(e => e.Number >= RaceTrackCharacterCars.CastFirst))
        {
            var body = LbaBodyStudio.Body.Read(bodies.Read(e.Body), 2);
            Console.WriteLine($"  {e.Number,3} {e.Name}: body {e.Body} (small {e.Small}), entity {e.Entity} body {e.Generic}; {body.Vertices.Count} points, {body.Faces.Count + body.Lines.Count + body.Spheres.Count} primitives");
        }
        return 0;
    }

    // carshow <game folder> <scene> <x> <y> <z> <turn> <dx> <dz> <body>...: stands cars of the racer's entity (its bodies: 0 its own, 1
    // Baldino's, 2-4 RaceTrackCharacterCars) in a row in a (test) game folder's scene, from (x, y, z) on by (dx, dz) a car, each turned `turn`;
    // y "ground": on the island's ground there (RT_ILE names its file, DESERT.ILE unless given); RT_FLAGS: the actors' flags (hex)
    public static int CarShow(string[] args)
    {
        var store = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, args[1]);
        var scene = int.Parse(args[2]);
        var model = store.Load(scene);
        int x = int.Parse(args[3]), z = int.Parse(args[5]), turn = int.Parse(args[6]), dx = int.Parse(args[7]), dz = int.Parse(args[8]);
        var island = args[4] == "ground" ? IslandFile.Load(Path.Combine(args[1], Environment.GetEnvironmentVariable("RT_ILE") ?? "DESERT.ILE")) : null;
        for (var i = 9; i < args.Length; i++)
        {
            int carX = x + (i - 9) * dx, carZ = z + (i - 9) * dz;
            var y = island is null ? int.Parse(args[4]) : (int)Math.Round(IslandOps.Altitude(island, model.CubeX * 32768.0 + carX, model.CubeY * 32768.0 + carZ) ?? 0);
            var car = LBAAssembler.Scenes.SceneOps.BlankActor(LBAAssembler.Scenes.SceneGame.Lba2, carX, y, carZ, RaceTrackBaldinoCar.RacerEntity);
            car.Body = int.Parse(args[i]); car.Flags = Environment.GetEnvironmentVariable("RT_FLAGS") is { Length: > 0 } fl ? Convert.ToUInt32(fl, 16) : RaceTrackScenes.OpponentFlags; car.Beta = turn; car.Life = new byte[] { 0 }; car.Track = new byte[] { 0 };
            Console.WriteLine($"scene {scene}: actor {LBAAssembler.Scenes.SceneOps.AddActor(model, car)} = body {car.Body} at ({car.X}, {car.Y}, {car.Z}) turn {turn}");
        }
        store.Save(scene, model, allowErrors: true);
        return 0;
    }

    // driverinfo <game folder> <body>...: what seating a character of BODY.HQR as a car's driver needs to know -- its size, the arms found
    // from its bones, and its bones (where each turns, what it spans, how many points)
    public static int DriverInfo(string[] args)
    {
        var bodies = HqrArchive.Open(Path.Combine(args[1], "BODY.HQR"));
        foreach (var index in args.Skip(2).Select(int.Parse))
        {
            LbaBodyStudio.Body body;
            try { body = LbaBodyStudio.Body.Read(bodies.Read(index), 2); } catch (Exception e) { Console.WriteLine($"=== {index}: {e.Message}"); continue; }
            var world = body.World();
            var driver = new CarDriver(body);
            var (right, left) = driver.FindArms();
            string Arm((int Upper, int[] Fore)? a) => a is { } arm ? $"{arm.Upper} -> [{string.Join(",", arm.Fore)}]" : "none";
            Console.WriteLine($"=== {index}: {body.Vertices.Count} points, {body.Faces.Count} polygons, {body.Lines.Count} lines, {body.Spheres.Count} spheres; x {world.Min(v => v.X):0}..{world.Max(v => v.X):0} y {world.Min(v => v.Y):0}..{world.Max(v => v.Y):0} z {world.Min(v => v.Z):0}..{world.Max(v => v.Z):0}; arms right {Arm(right)} left {Arm(left)}");
            for (var b = 0; b < body.Bones.Count; b++)
            {
                var bone = body.Bones[b];
                var pts = Enumerable.Range(bone.Start, bone.Count).Select(i => world[i]).ToList();
                var pivot = bone.Parent < 0 ? System.Numerics.Vector3.Zero : world[bone.Pivot];
                Console.WriteLine($"   {b,2} <- {bone.Parent,2} at ({pivot.X:0},{pivot.Y:0},{pivot.Z:0}) {bone.Count,2} pts x {pts.Min(v => v.X):0}..{pts.Max(v => v.X):0} y {pts.Min(v => v.Y):0}..{pts.Max(v => v.Y):0} z {pts.Min(v => v.Z):0}..{pts.Max(v => v.Z):0}");
            }
        }
        return 0;
    }

    // Copies of a game folder's files that can be written (the pristine folder's are read-only, and a copy keeps that).
    private static void CopyWritable(string from, string to, params string[] files)
    {
        foreach (var f in files)
        {
            var target = Path.Combine(to, f);
            if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal);
            File.Copy(Path.Combine(from, f), target, overwrite: true);
            File.SetAttributes(target, FileAttributes.Normal);
        }
    }

    // actorbeta <game folder> <scene> <actor> <turn> [dx dz]: turns an actor (and moves it by dx, dz world units) in a (test) game folder's scene
    public static int ActorBeta(string[] args)
    {
        var store = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, args[1]);
        var scene = int.Parse(args[2]);
        var model = store.Load(scene);
        var actor = model.Actors[int.Parse(args[3])];
        actor.Beta = int.Parse(args[4]);
        if (args.Length > 6) { actor.X += int.Parse(args[5]); actor.Z += int.Parse(args[6]); }
        store.Save(scene, model, allowErrors: true);
        Console.WriteLine($"scene {scene} actor {args[3]}: turn {actor.Beta} at ({actor.X},{actor.Y},{actor.Z})");
        return 0;
    }

    // scaleisland <game>: the island (RT_ISLAND, the Island of the Francos when not given) made bigger in the folder as a race build makes
    // it (IslandScaler: its ground, its OBL's bodies, its scenes) -- the folder's own files, which must be the originals.
    public static int ScaleIsland(string[] args)
    {
        var game = args[1];
        var where = RaceTrackIsland.ByName(Environment.GetEnvironmentVariable("RT_ISLAND") ?? RaceTrackIsland.Knartas.Name);
        var island = IslandFile.Load(Path.Combine(game, where.IleFile));
        var anchor = IslandScaler.Anchor(island);
        var log = new List<string>();
        var scaled = IslandScaler.Scale(island, Math.Max(2, where.Scale), log);
        scaled.Save(Path.Combine(game, where.IleFile));
        log.Add(IslandScaler.ScaleObl(Path.Combine(game, where.OblFile), Math.Max(2, where.Scale)));
        log.AddRange(IslandScaler.ScaleScenes(game, where, scaled, anchor, Math.Max(2, where.Scale)));
        log.Add(IslandScaler.ScaleHolomap(game, where, anchor, Math.Max(2, where.Scale)));
        foreach (var line in log) Console.WriteLine(line);
        return 0;
    }

    // palettechart <game> <RESS palette entry> <out.png>: the palette's 256 colours, 16 a row, 32 px squares (picking body colours)
    public static int PaletteChart(string[] args)
    {
        var palette = IslandMapRenderer.LoadPaletteEntry(args[1], int.Parse(args[2]));
        var six = palette.Take(768).Max() <= 63;
        var px = new byte[512 * 512 * 4];
        for (var y = 0; y < 512; y++)
            for (var x = 0; x < 512; x++)
            {
                var c = (y / 32) * 16 + x / 32; var i = (y * 512 + x) * 4;
                px[i] = (byte)(palette[c * 3 + 2] * (six ? 4 : 1)); px[i + 1] = (byte)(palette[c * 3 + 1] * (six ? 4 : 1)); px[i + 2] = (byte)(palette[c * 3] * (six ? 4 : 1)); px[i + 3] = 255;
            }
        PngWriter.Write(args[3], px, 512, 512);
        Console.WriteLine(args[3]);
        return 0;
    }

    // hqrentry <file.HQR> <entry> <out>: one entry of an HQR, unpacked, to a file (looking into the game's resources)
    public static int HqrEntry(string[] args)
    {
        var bytes = HqrArchive.Open(args[1]).Read(int.Parse(args[2]));
        File.WriteAllBytes(args[3], bytes);
        Console.WriteLine($"{args[3]}: {bytes.Length} bytes");
        return 0;
    }

    public static int RaceCarFile(string[] args)
    {
        var setup = new LBAAssembler.RaceCarSetup();
        if (args.Length > 3) setup.MainSkill = int.Parse(args[3]);
        // (RT_WEATHER=rain: Citadel Island left raining -- its storm track)
        if (Environment.GetEnvironmentVariable("RT_WEATHER") == "rain") setup.FineWeather = false;
        // (RT_NO_OPPONENTS=1: Twinsen alone on the track -- the car file then has the one to beat's line as its guide)
        if (Environment.GetEnvironmentVariable("RT_NO_OPPONENTS") == "1") setup.Opponent = false;
        // (RT_CHECKPOINTS=1: the checkpoints drawn as red lines across the road -- off by default)
        if (Environment.GetEnvironmentVariable("RT_CHECKPOINTS") == "1") setup.ShowCheckpoints = true;
        // (RT_DRIVE=<racer body>: driving as that opponent's car; -2 or -3 Twinsen's buggy firing green or red lasers)
        if (int.TryParse(Environment.GetEnvironmentVariable("RT_DRIVE"), out var drive)) { setup.DriveAs = drive; setup.DriveAsKey = RaceCarEngineFile.DriveAsLine(args[1], drive); }
        // (RT_RACE=<island file>: of a folder with several tracks, the one Play races with that island open in the editor, as RaceFor picks it)
        var track = Environment.GetEnvironmentVariable("RT_RACE") is { Length: > 0 } race ? RaceTrackService.RaceFor(args[1], race) : RaceTrackService.ReadInfo(args[1]);
        // (RT_STORY=1: the game played as a game -- every track in a set, raced where and when the game is, with the story's keys)
        if (Environment.GetEnvironmentVariable("RT_STORY") == "1") setup.WriteStorySet(Path.GetFullPath(args[2]), RaceTrackService.ReadInfo(args[1])!);
        else setup.WriteEngineFile(Path.GetFullPath(args[2]), track);
        Console.WriteLine($"{args[2]}: {File.ReadAllLines(args[2]).Length} lines");
        return 0;
    }

    public static int HeroStart(string[] args)
    {
        var game = args[1]; var cellX = double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture); var cellZ = double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
        var turn = args.Length > 4 ? int.Parse(args[4]) : 0;
        var island = IslandFile.Load(Path.Combine(game, "DESERT.ILE"));
        var store = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, game);
        var cx = (int)Math.Floor(cellX / 64); var cz = (int)Math.Floor(cellZ / 64);
        for (var scene = 55; scene <= 73; scene++)
        {
            var model = store.Load(scene);
            if (model.CubeMode != 1 || model.CubeX != cx || model.CubeY != cz) continue;
            var y = IslandOps.Altitude(island, cellX * 512, cellZ * 512) ?? 0;
            model.Hero.X = (int)Math.Round(cellX * 512 - cx * 32768.0); model.Hero.Z = (int)Math.Round(cellZ * 512 - cz * 32768.0); model.Hero.Y = (int)Math.Round(y) + 200; model.Hero.Beta = turn;
            store.Save(scene, model, allowErrors: true);
            Console.WriteLine($"scene {scene}: Twinsen at ({model.Hero.X},{model.Hero.Y},{model.Hero.Z})");
            return 0;
        }
        Console.WriteLine("no scene holds that cell");
        return 1;
    }

    // initbuggy <game folder> <scene>: the scene's buggy always exists (the car-quest test passes) and INIT_BUGGY(2) puts it at its own place.
    public static int InitBuggy(string[] args)
    {
        var store = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, args[1]);
        var scene = int.Parse(args[2]);
        var model = store.Load(scene);
        foreach (var actor in model.Actors.Skip(1).Where(a => a.Entity == RaceTrackScenes.BuggyEntity))
        {
            var life = actor.Life;
            if (life.Length > 10 && life[0] == 0x0C && life[1] == 0x0F && life[2] == 0x4A && life[4] == 0x03) life[4] = 0;
            for (var i = 6; i < Math.Min(30, life.Length - 1); i++)
                if (life[i] == 0x46 && life[i + 1] == 0x00) { life[i + 1] = 0x02; Console.WriteLine($"INIT_BUGGY(2) at byte {i}"); break; }
        }
        store.Save(scene, model, allowErrors: true);
        return 0;
    }

    // scripttext <game folder> <scene> <actor> life|track: the script as the editor's C text.
    public static int ScriptText(string[] args)
    {
        var store = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, args[1]);
        var scene = int.Parse(args[2]);
        var scripts = LBAAssembler.LbaScript.SceneScripts.Load(store.LoadRecord(scene), scene);
        Console.WriteLine(scripts.GetText(int.Parse(args[3]), args[4] == "life" ? LBAAssembler.LbaScript.ScriptKind.Life : LBAAssembler.LbaScript.ScriptKind.Track));
        return 0;
    }

    // driveprep <game folder> <cell x> <cell z> <turn>: the buggy stands on the ground at that island cell facing `turn`, always there (INIT_BUGGY 2), Twinsen two cells
    // behind it -- a place to start a test drive from.
    public static int DrivePrep(string[] args)
    {
        var game = args[1];
        var cellX = double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture); var cellZ = double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
        var turn = int.Parse(args[4]);
        var where = RaceTrackIsland.ByName(Environment.GetEnvironmentVariable("RT_ISLAND") ?? RaceTrackIsland.Desert.Name);
        var island = IslandFile.Load(Path.Combine(game, where.IleFile));
        var store = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, game);
        var cx = (int)Math.Floor(cellX / 64); var cz = (int)Math.Floor(cellZ / 64);
        foreach (var scene in where.Scenes)
        {
            var model = store.Load(scene);
            if (model.CubeMode != 1 || model.CubeX != cx || model.CubeY != cz) continue;
            var y = (int)Math.Round(IslandOps.Altitude(island, cellX * 512, cellZ * 512) ?? 0);
            var dx = Math.Sin(turn * 2 * Math.PI / 4096); var dz = Math.Cos(turn * 2 * Math.PI / 4096);
            // (a scene with no buggy of its own -- only the start line's scene has one -- gets a copy of the island's)
            if (!model.Actors.Skip(1).Any(a => a.Entity == RaceTrackScenes.BuggyEntity))
                foreach (var other in where.Scenes)
                    if (other != scene && store.SceneExists(other) && store.Load(other).Actors.Skip(1).FirstOrDefault(a => a.Entity == RaceTrackScenes.BuggyEntity) is { } buggy)
                    {
                        LBAAssembler.Scenes.SceneOps.AddActor(model, buggy.Clone());
                        Console.WriteLine($"scene {scene}: a copy of scene {other}'s buggy added");
                        break;
                    }
            foreach (var actor in model.Actors.Skip(1).Where(a => a.Entity == RaceTrackScenes.BuggyEntity))
            {
                actor.X = (int)Math.Round(cellX * 512 - cx * 32768.0); actor.Z = (int)Math.Round(cellZ * 512 - cz * 32768.0); actor.Y = y; actor.Beta = turn;
                var life = actor.Life;
                for (var i = 6; i < Math.Min(30, life.Length - 1); i++) if (life[i] == 0x46 && life[i + 1] == 0x00) { life[i + 1] = 0x02; break; }
                model.Hero.X = actor.X - (int)(dx * 1024); model.Hero.Z = actor.Z - (int)(dz * 1024); model.Hero.Y = y + 200; model.Hero.Beta = turn;
            }
            store.Save(scene, model, allowErrors: true);
            Console.WriteLine($"scene {scene}: buggy at ({cellX},{cellZ}) turn {turn}");
            return 0;
        }
        return 1;
    }

    // bodyname <game> <index...>: BODY2.HQD names for a list of body indices.
    public static int BodyName(string[] args)
    {
        var names = HqdDescriptions.Load("BODY2.HQD", 0).Names;
        foreach (var a in args.Skip(1)) { var i=int.Parse(a); Console.WriteLine($"{i}: {(i+1<names.Count?names[i+1]:"?")}"); }
        return 0;
    }

    // scenenames <game>: every SCENE2.HQD name containing a word.
    public static int SceneNames(string[] args)
    {
        var names = HqdDescriptions.Load("SCENE2.HQD", 0).Names;
        var word = args[1].ToLowerInvariant();
        for (var i=0;i<names.Count;i++) if (names[i] is { } n && n.ToLowerInvariant().Contains(word)) Console.WriteLine($"{i-1}: {n}");
        return 0;
    }
}
