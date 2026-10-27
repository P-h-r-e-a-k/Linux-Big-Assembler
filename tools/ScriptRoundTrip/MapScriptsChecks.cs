using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using LBAAssembler;
using LBAAssembler.Lba1.Runtime;
using LBAAssembler.LbaScript;
using LBAAssembler.Maps;
using LBAAssembler.Scenes;

namespace ScriptRoundTrip;

//   mapscripts <LBA2 game folder (a sandbox)> [<LBA1 game folder (a sandbox)>]: the checks (each folder only read: the maps go into copies
//   of it, LiveDataRoot mirrors made inside it and removed again)
//   - a map's scripts are saved into the map: the script editor's session (ScriptSession on the workspace, as the editor has it) writes the
//     map's scene in the workspace, File > Save map (MapInstall.Capture) puts it into the .lbamap's .LS2, the game folder's own SCENE.HQR
//     stays byte for byte as it was; while the map is open nothing writes into the game folder's own files (FileTransaction's guard)
//   - unsaved script edits stay with the folder they were made in: after Close map an LBA1 map's edit (its slot, scene 28) is neither shown as
//     the game's scene 28 nor saved over it (it was, before 2026-10-17)
//   - behaviours: every kind of actor's animations read; every template compiles for every kind; every behaviour of the game's own actors
//     carried into a new scene comes out valid (SceneValidator: no errors, its references in range); the skeleton's usual behaviour attacks
//   - the engine: a new map's skeleton with its kind's usual behaviour, and one with the "spots, chases and attacks" template, go for Twinsen
//     and hit him (headless, dumpstate); a talker with the "talks when spoken to" template says its line when Twinsen presses action near it
//   - LBA1: the same in LBA1's C# runtime (Lba1Runtime): a new soldier with the attack template comes for Twinsen and hits him
internal static partial class MapScriptsTest
{
    private static int failures;

    private static void Check(string what, bool ok, string detail = "")
    {
        Console.WriteLine($"  {what}: {(ok ? "ok" : "FAILED")}{(detail.Length > 0 ? "  " + detail : "")}");
        if (!ok) failures++;
    }

    private static string Md5(string path) => File.Exists(path) ? Convert.ToHexString(MD5.HashData(File.ReadAllBytes(path))) : "(none)";

    private const string TalkLife = "void comportement_0()\n{\n    // Twinsen speaks to it\n    if (1 == action() && 1200 > distance(0))\n    {\n        message(0);\n    }\n}\n";

    public static int Checks(string[] args)
    {
        var game = Path.GetFullPath(args[1]);
        var lba1 = args.Length > 2 ? Path.GetFullPath(args[2]) : null;
        failures = 0;
        Console.WriteLine("-- a map's scripts saved into the map (LBA2)");
        SavedIntoMap(game);
        if (lba1 is not null)
        {
            Console.WriteLine("-- an LBA1 map's unsaved script edit after Close map");
            Lba1StaleEdit(lba1);
        }
        Console.WriteLine("-- behaviours (LBA2)");
        Behaviours(SceneGame.Lba2, game);
        if (lba1 is not null)
        {
            Console.WriteLine("-- behaviours (LBA1)");
            Behaviours(SceneGame.Lba1, lba1);
        }
        Console.WriteLine("-- the engine plays them (LBA2, headless)");
        Engine(game);
        if (lba1 is not null)
        {
            Console.WriteLine("-- LBA1's runtime plays them");
            Lba1Runtime(lba1);
        }
        Console.WriteLine(failures == 0 ? "map scripts: all passed" : $"map scripts: {failures} FAILED");
        return failures == 0 ? 0 : 1;
    }

    // A new LBA2 interior map (from scene 4) with actors made by `add` (after the Meca-penguin's slot).
    private static LbaMap Lba2Map(string game, string name, Action<SceneModel> add)
    {
        var map = MapNew.Lba2Interior(game, 4, name);
        var model = SceneSerializer.Parse(SceneGame.Lba2, map.Scenes[0].Record);
        SceneOps.EnsureMecaPenguinSlot(model);
        add(model);
        map.Scenes[0] = map.Scenes[0] with { Record = SceneSerializer.Write(model) };
        return map;
    }

    private static SceneActorModel NewActor(SceneGame game, string folder, SceneModel scene, int body, int dx, int dz)
    {
        var hero = scene.Hero;
        SceneActorModel actor;
        if (game == SceneGame.Lba2)
        {
            var kind = Lba2EntityTable.Load(folder)!.KindForBody(body)!.Value;
            actor = SceneOps.BlankActor(game, hero.X + dx, hero.Y, hero.Z + dz, kind.Entity);
            actor.Body = kind.Body; actor.Anim = kind.Anim;
        }
        else actor = SceneOps.BlankActor(game, hero.X + dx, hero.Y, hero.Z + dz, body);
        // (facing Twinsen: 0 = +z, 1024 = +x)
        actor.Beta = (int)Math.Round(Math.Atan2(-dx, -dz) * 2048 / Math.PI) & 4095;
        return actor;
    }

    // ---- saved into the map
    private static void SavedIntoMap(string game)
    {
        var sceneHqr = Path.Combine(game, "SCENE.HQR");
        var before = Md5(sceneHqr);
        const string folderName = "_MAPSCRIPTS_TEST";
        LiveDataRoot.CleanStale(game, folderName);
        var mirror = LiveDataRoot.Create(game, null, folderName) ?? throw new IOException("couldn't make the mirror");
        var file = Path.Combine(Path.GetTempPath(), $"mapscripts_{Guid.NewGuid():N}.lbamap");
        try
        {
            var map = Lba2Map(game, "Script test", m => SceneOps.AddActor(m, NewActor(SceneGame.Lba2, game, m, Skeleton, 2048, 0)));
            var at = MapInstall.Install(map, mirror.Directory);
            var scene = at.FirstScene;
            // (File > Open map: the workspace stands in for the game folder, the game folder guarded)
            MapWorkspace.Opened(map.Name, null, mirror.Directory, game, lba1: false);
            var root = mirror.Directory;
            var session = new ScriptSession(() => root);
            var scripts = session.GetScene(scene)!;
            scripts.SetText(2, ScriptKind.Life, TalkLife);
            var result = session.SaveAll();
            Check("the script editor saves the skeleton's script into the map", result.Ok && result.Message.Contains("into the map \"Script test\""), result.Message);
            Check("the game folder's own SCENE.HQR is byte for byte as it was", Md5(sceneHqr) == before);
            Check("the map's working copy has it", new SceneStore(SceneGame.Lba2, mirror.Directory).Load(scene).Actors[2].Life.Length > 1);
            var captured = MapInstall.Capture(at);
            captured.Save(file);
            var saved = SceneSerializer.Parse(SceneGame.Lba2, LbaMap.Load(file).Scenes[0].Record).Actors[2];
            var text = SceneScripts.Load(LbaMap.Load(file).Scenes[0].Record).GetText(2, ScriptKind.Life);
            Check("File > Save map writes it into the map file's .LS2", saved.Life.Length > 1 && text.Contains("message(0);"), $"{saved.Life.Length} bytes");
            // its comments too: the map opened again (installed into a fresh copy, maybe under another number) shows them on its script
            var reopened = LbaMap.Load(file);
            Check("and its comments into the map file", reopened.Comments is { Length: > 0 } && System.Text.Encoding.UTF8.GetString(reopened.Comments).Contains("Twinsen speaks to it"));
            var again = LiveDataRoot.Create(game, null, folderName + "2") ?? throw new IOException("couldn't make the second mirror");
            try
            {
                // (a second copy inside the game folder, which the open map guards: this test's own write, let through)
                InstalledMap at2;
                using (FileTransaction.AllowWrites()) at2 = MapInstall.Install(reopened, again.Directory);
                var shownAgain = new ScriptSession(() => again.Directory).GetScene(at2.FirstScene)!.GetText(2, ScriptKind.Life);
                Check("opened again, the script shows with its comment", shownAgain.Contains("// Twinsen speaks to it") && shownAgain.Contains("message(0);"));
            }
            finally { again.Dispose(); }

            // the game folder guarded while the map is open
            var store = new SceneStore(SceneGame.Lba2, game);
            string? refused = null;
            try { store.Save(0, store.Load(0), description: "test"); }
            catch (IOException e) { refused = e.Message; }
            Check("while the map is open, a save into the game folder's own SCENE.HQR is refused", refused is not null && Md5(sceneHqr) == before, refused ?? "it was written");
            using (FileTransaction.AllowWrites())
                Check("File > Install map into game may write there (allowed on purpose)", FileTransaction.Refusal(sceneHqr) is null);

            // an unsaved edit, then Close map: it belongs to the map's copy, not to the game
            session.GetScene(scene)!.SetText(2, ScriptKind.Life, TalkLife.Replace("message(0)", "message(1)"));
            MapWorkspace.Closed();
            root = game;
            Check("after Close map the session holds the edit for the map's copy", session.OtherFolder is { } other && other.EndsWith(folderName, StringComparison.OrdinalIgnoreCase));
            Check("and shows none of the game's scenes until it is settled", session.GetScene(0) is null);
            var late = session.SaveAll();
            Check("saved late, it goes into the map's copy, not the game's SCENE.HQR", late.Ok && Md5(sceneHqr) == before
                && SceneScripts.Load(new SceneStore(SceneGame.Lba2, mirror.Directory).LoadRecord(scene)).GetText(2, ScriptKind.Life).Contains("message(1);"), late.Message);
            session.DiscardAll();
            Check("dropped, the game's own scenes show again", session.GetScene(0) is not null && session.OtherFolder is null);
        }
        finally
        {
            MapWorkspace.Closed();
            mirror.Dispose();
            try { File.Delete(file); } catch (IOException) { }
        }
        Check("the game folder isn't guarded once the map is closed", FileTransaction.Refusal(sceneHqr) is null);
        Check("the game's SCENE.HQR at the end: as at the start", Md5(sceneHqr) == before, before);
    }

    // ---- the LBA1 map's edit carried back (the bug of 2026-10-17)
    private static void Lba1StaleEdit(string lba1)
    {
        var sceneHqr = Path.Combine(lba1, "SCENE.HQR");
        var before = Md5(sceneHqr);
        const string folderName = "_MAPSCRIPTS_TEST";
        LiveDataRoot.CleanStale(lba1, folderName);
        var mirror = LiveDataRoot.Create(lba1, null, folderName) ?? throw new IOException("couldn't make the mirror");
        try
        {
            var map = MapNew.Lba1Interior(lba1, 28, "LBA1 script test");
            var model = SceneSerializer.Parse(SceneGame.Lba1, map.Scenes[0].Record);
            SceneOps.AddActor(model, NewActor(SceneGame.Lba1, lba1, model, 7, 1024, 0));
            map.Scenes[0] = map.Scenes[0] with { Record = SceneSerializer.Write(model) };
            MapInstall.Install(map, mirror.Directory);
            MapWorkspace.Opened(map.Name, null, mirror.Directory, lba1, lba1: true);
            var root = mirror.Directory;
            var session = new ScriptSession(() => root, lba1: true);
            session.GetScene(28)!.SetText(1, ScriptKind.Life, "void comportement_0()\n{\n    if (1 == action())\n    {\n        message(5);\n    }\n}\n");
            // File > Close map without saving it
            MapWorkspace.Closed();
            root = lba1;
            var shown = session.GetScene(28);
            Check("the game's own scene 28 isn't shown with the map's edit", shown is null, shown is null ? "" : $"it shows {shown.ActorCount} actors");
            var save = session.SaveAll();
            Check("saving it doesn't write the game's SCENE.HQR (it goes into the map's copy)", Md5(sceneHqr) == before, save.Message);
            session.DiscardAll();
            var game = session.GetScene(28);
            Check("dropped, the game's scene 28 is its own again", game is not null && game.ActorCount == new SceneStore(SceneGame.Lba1, lba1).Load(28).Actors.Count, $"{game?.ActorCount} actors");
        }
        finally
        {
            MapWorkspace.Closed();
            mirror.Dispose();
        }
        Check("the LBA1 game's SCENE.HQR at the end: as at the start", Md5(sceneHqr) == before);
    }

    // ---- behaviours over every kind and every game actor
    private static void Behaviours(SceneGame game, string folder)
    {
        var kinds = KindAnims.All(game, folder)!;
        var unread = kinds.Where(k => k is not null).Sum(k => k!.Unread.Count);
        var anims = kinds.Where(k => k is not null).Sum(k => k!.Anims.Count);
        Check($"every kind's animations read ({kinds.Count} kinds, {anims} animations)", unread * 100 <= anims, $"{unread} whose actions weren't read to their end");
        var store = new SceneStore(game, folder);
        var map = game == SceneGame.Lba1 ? MapNew.Lba1Interior(folder, 28, "Behaviours") : MapNew.Lba2Interior(folder, 4, "Behaviours");
        var blank = SceneSerializer.Parse(game, map.Scenes[0].Record);
        SceneOps.EnsureMecaPenguinSlot(blank);
        var lines = ActorBehaviours.Texts(folder, game, blank.Island);
        Check($"the talking template's lines: island {blank.Island}'s texts", lines.Count > 10, $"{lines.Count} texts");

        int applied = 0, invalid = 0, kindsTried = 0, retailGroups = 0, notes = 0;
        var problems = new List<string>();
        for (var entity = 0; entity < kinds.Count; entity++)
        {
            if (kinds[entity] is null) continue;
            var retail = ActorBehaviours.OfKind(store, blank, entity);
            if (retail.Count == 0 && game == SceneGame.Lba2 && entity > 120) continue;     // (LBA2's later kinds are mostly decor: a sample of them)
            kindsTried++;
            retailGroups += retail.Count;
            foreach (var behaviour in ActorBehaviours.Templates(game, folder, entity).Concat(retail).Append(ActorBehaviours.NoScript()))
            {
                var scene = blank.Clone();
                var actor = SceneOps.BlankActor(game, scene.Hero.X + 1500, scene.Hero.Y, scene.Hero.Z, entity);
                var index = SceneOps.AddActor(scene, actor);
                try
                {
                    var said = ActorBehaviours.Apply(store, scene, index, behaviour, behaviour.NeedsText ? lines[0].Id : null);
                    notes += said.Count;
                    applied++;
                    var issues = SceneValidator.Validate(scene).Where(i => i.Severity == SceneIssueSeverity.Error || i.Where.StartsWith($"actor {index} ")).ToList();
                    // (and it reads back as C text, the script editor's view)
                    var text = SceneScripts.Load(SceneSerializer.Write(scene), -1, null, game == SceneGame.Lba1);
                    try { text.GetText(index, ScriptKind.Life); text.GetText(index, ScriptKind.Track); }
                    catch (InvalidOperationException e) { if (scene.Actors[index].Life.Length > 1) issues.Add(new SceneIssue(SceneIssueSeverity.Warning, "C text", e.Message)); }
                    if (issues.Count > 0) { invalid++; if (problems.Count < 8) problems.Add($"entity {entity} {behaviour.Id}: {issues[0]}"); }
                }
                catch (Exception e) when (e is SceneEditException or InvalidDataException or InvalidOperationException or ScriptFormatException)
                {
                    invalid++;
                    if (problems.Count < 8) problems.Add($"entity {entity} {behaviour.Id}: {e.Message}");
                }
            }
        }
        Check($"every template and every game actor's behaviour on a new actor of {kindsTried} kinds ({retailGroups} of the game's): valid, read back as C", invalid == 0,
            $"{applied} applied, {notes} remapping notes" + (problems.Count > 0 ? "; " + string.Join(" | ", problems) : ""));

        if (game == SceneGame.Lba2)
        {
            var skeleton = Lba2EntityTable.Load(folder)!.KindForBody(Skeleton)!.Value.Entity;
            var list = ActorBehaviours.For(store, blank, skeleton);
            var usual = ActorBehaviours.DefaultFor(store, blank, skeleton);
            Check("the skeleton's behaviours: no script, the templates, the game's skeletons (scene 184's among them)", list.Any(b => b.Id == "none") && list.Any(b => b.Id == "t:attack") && list.Any(b => b.Id == "r:184:3"),
                string.Join(", ", list.Select(b => b.Id)));
            Check("a skeleton's usual behaviour goes for Twinsen and attacks him", usual.Attacks && usual.SourceScene is not null, $"{usual.Name}: {usual.Description} [{usual.Source}]");
            var zoe = Lba2EntityTable.Load(folder)!.KindForBody(26)!.Value.Entity;
            var zoeUsual = ActorBehaviours.DefaultFor(store, blank, zoe);
            Check("Zoé's usual behaviour doesn't attack", !zoeUsual.Attacks, $"{zoeUsual.Name}: {zoeUsual.Description}");
            // Add > Actor from another scene: scene 184's skeleton carried over
            var s184 = store.Load(184);
            var scene = blank.Clone();
            var copy = s184.Actors[3].Clone(); copy.X = scene.Hero.X + 1500; copy.Z = scene.Hero.Z;
            var at = SceneOps.AddActor(scene, copy);
            var carried = ActorBehaviours.Apply(store, scene, at, ActorBehaviours.FromActor(game, folder, s184, 184, 3));
            var carriedText = SceneScripts.Load(SceneSerializer.Write(scene)).GetText(at, ScriptKind.Life);
            Check("Actor from another scene: scene 184's skeleton keeps its hunt (Twinsen and the Meca-penguin's slot are in every scene)", carried.Count == 0
                && carriedText.Contains("set_dir(MOVE_FOLLOW, 0);") && carriedText.Contains("distance(1)") && !SceneValidator.Validate(scene).Any(i => i.Severity == SceneIssueSeverity.Error),
                carried.Count > 0 ? string.Join("; ", carried) : "");
        }
    }

    // ---- the engine
    private sealed record EngineRun(bool Entered, List<JsonDocument> States, string Output);

    private static void Engine(string game)
    {
        var engine = Lba2Engine.Find();
        if (engine is null) { Check("lba2cc.exe found", false); return; }
        const string folderName = "_MAPSCRIPTS_ENGINE";
        LiveDataRoot.CleanStale(game, folderName);
        var mirror = LiveDataRoot.Create(game, null, folderName) ?? throw new IOException("couldn't make the mirror");
        var user = Path.Combine(Path.GetTempPath(), "mapscripts_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(user);
        try
        {
            var store = new SceneStore(SceneGame.Lba2, game);
            var table = Lba2EntityTable.Load(game)!;
            var skeleton = table.KindForBody(Skeleton)!.Value.Entity;

            foreach (var (label, pick) in new (string, Func<SceneModel, ActorBehaviour>)[]
            {
                ("its kind's usual behaviour", s => ActorBehaviours.DefaultFor(store, s, skeleton)),
                ("\"Spots, chases and attacks Twinsen\"", s => ActorBehaviours.Templates(SceneGame.Lba2, game, skeleton).First(t => t.Template == "attack")),
            })
            {
                ActorBehaviour? chosen = null;
                var map = Lba2Map(game, "Skeleton", m =>
                {
                    var i = SceneOps.AddActor(m, NewActor(SceneGame.Lba2, game, m, Skeleton, 2560, 0));
                    chosen = pick(m);
                    ActorBehaviours.Apply(store, m, i, chosen);
                });
                var at = MapInstall.Install(map, mirror.Directory);
                var run = Play(engine, mirror.Directory, user, at.FirstScene, new[] { (6, "lifetrace 2"), (12, "dumpstate {0}"), (600, "dumpstate {0}"), (1600, "dumpstate {0}") }, 1620);
                Check($"the engine enters the map's scene {at.FirstScene} (a skeleton with {label}: {chosen!.Name})", run.Entered && run.States.Count == 3, $"{run.States.Count} states");
                Hunts(run);
                if (label.StartsWith("its kind"))
                {
                    // File > Save map, then Install map into game (Yes: the MAPS folder, MAPPACK.CPP): the engine plays it from the .lbamap's .LS2
                    var (_, numbers) = MapInstall.InstallInMapsFolder(MapInstall.Capture(at), mirror.Directory, "mapscripts_skeleton");
                    var fromMaps = Play(engine, mirror.Directory, user, numbers[0], new[] { (12, "dumpstate {0}"), (600, "dumpstate {0}"), (1600, "dumpstate {0}") }, 1620);
                    Check($"saved into a .lbamap and played from the MAPS folder (scene {numbers[0]})", fromMaps.Entered && fromMaps.States.Count == 3);
                    Hunts(fromMaps);
                }
            }

            void Hunts(EngineRun run)
            {
                if (run.States.Count == 3)
                {
                    double Dist(JsonDocument d)
                    {
                        var hero = d.RootElement.GetProperty("hero");
                        var a = d.RootElement.GetProperty("actors").EnumerateArray().First(x => x.GetProperty("index").GetInt32() == 2);
                        return Math.Sqrt(Math.Pow(a.GetProperty("x").GetInt32() - hero.GetProperty("x").GetInt32(), 2) + Math.Pow(a.GetProperty("z").GetInt32() - hero.GetProperty("z").GetInt32(), 2));
                    }
                    int Life(JsonDocument d) => d.RootElement.GetProperty("hero").GetProperty("life").GetInt32();
                    int Move(JsonDocument d) => d.RootElement.GetProperty("actors").EnumerateArray().First(x => x.GetProperty("index").GetInt32() == 2).GetProperty("move").GetInt32();
                    Check("  it sees Twinsen and goes for him (move: follow)", Move(run.States[1]) == 2, $"move {Move(run.States[0])} -> {Move(run.States[1])}");
                    Check("  it closes in on him", Dist(run.States[1]) < Dist(run.States[0]) - 600, $"distance {Dist(run.States[0]):0} -> {Dist(run.States[1]):0} -> {Dist(run.States[2]):0}");
                    Check("  and hits him", Life(run.States[2]) < Life(run.States[0]), $"Twinsen's life {Life(run.States[0])} -> {Life(run.States[1])} -> {Life(run.States[2])}");
                }
            }

            // a talker: Zoé with the "talks when spoken to" template, Twinsen presses action beside her
            var zoe = table.KindForBody(26)!.Value.Entity;
            var lines = ActorBehaviours.Texts(game, SceneGame.Lba2, 0);
            var line = lines.First(l => l.Text.Length > 10);
            var talkMap = Lba2Map(game, "Talker", m =>
            {
                var i = SceneOps.AddActor(m, NewActor(SceneGame.Lba2, game, m, 26, 0, 900));
                ActorBehaviours.Apply(store, m, i, ActorBehaviours.Templates(SceneGame.Lba2, game, zoe).First(t => t.Template == "talk"), line.Id);
            });
            var talkAt = MapInstall.Install(talkMap, mirror.Directory);
            var talk = Play(engine, mirror.Directory, user, talkAt.FirstScene, new[] { (6, "lifetrace 2"), (7, "skipmodals 1"), (40, "input action 20"), (120, "dumpstate {0}") }, 140);
            Check($"the talker's scene {talkAt.FirstScene} plays", talk.Entered);
            Check($"Twinsen presses action beside Zoé: she says her line ({line.Id}: \"{line.Text[..Math.Min(40, line.Text.Length)]}…\")", talk.Output.Contains($"MESSAGE dial={line.Id}"),
                talk.Output.Split('\n').FirstOrDefault(l => l.Contains("MESSAGE dial="))?.Trim() ?? "no MESSAGE in its life trace");
            var quiet = Play(engine, mirror.Directory, user, talkAt.FirstScene, new[] { (6, "lifetrace 2"), (7, "skipmodals 1"), (120, "dumpstate {0}") }, 140);
            Check("  and not before he does", quiet.Entered && !quiet.Output.Contains("MESSAGE dial="));
        }
        finally
        {
            mirror.Dispose();
            try { Directory.Delete(user, true); } catch (IOException) { }
        }
    }

    // The engine headless on `directory` in `scene`, the console commands at their ticks ("{0}" a dumpstate's file), every dumpstate read.
    private static EngineRun Play(string engine, string directory, string user, int scene, IReadOnlyList<(int Tick, string Command)> commands, int ticks)
    {
        var states = new List<string>();
        var start = new ProcessStartInfo(engine) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = directory };
        // (--fixed-dt: the clock steps 16 ms a tick, so a run is the same however fast the machine is)
        foreach (var a in new[] { "--headless", "--no-audio", "--no-autosave", "--fixed-dt", "16", "--game-dir", directory, "--user-dir", user, "--exec-at", "5", $"cube {scene}", "--exec-at", "11", "status" })
            start.ArgumentList.Add(a);
        foreach (var (tick, command) in commands)
        {
            var text = command;
            if (command.Contains("{0}")) { var state = Path.Combine(user, $"state{scene}_{tick}.json"); File.Delete(state); states.Add(state); text = string.Format(command, state); }
            start.ArgumentList.Add("--exec-at"); start.ArgumentList.Add(tick.ToString()); start.ArgumentList.Add(text);
        }
        foreach (var a in new[] { "--tick", ticks.ToString(), "--exit" }) start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120000)) { try { process.Kill(true); } catch (InvalidOperationException) { } }
        var all = output.Result + errors.Result;
        if (Environment.GetEnvironmentVariable("MAPSCRIPTS_VERBOSE") == "1") Console.WriteLine(all);
        return new EngineRun(all.Contains($"Cube: {scene}"), states.Where(File.Exists).Select(s => JsonDocument.Parse(File.ReadAllText(s))).ToList(), all);
    }

    // ---- LBA1: the C# runtime
    private static void Lba1Runtime(string lba1)
    {
        const string folderName = "_MAPSCRIPTS_RUNTIME";
        LiveDataRoot.CleanStale(lba1, folderName);
        var mirror = LiveDataRoot.Create(lba1, null, folderName) ?? throw new IOException("couldn't make the mirror");
        try
        {
            var store = new SceneStore(SceneGame.Lba1, lba1);
            // an LBA1 kind that attacks in the game, and the template on it
            var kinds = KindAnims.All(SceneGame.Lba1, lba1)!;
            var map = MapNew.Lba1Interior(lba1, 28, "LBA1 attack");
            var model = SceneSerializer.Parse(SceneGame.Lba1, map.Scenes[0].Record);
            var soldier = Enumerable.Range(0, kinds.Count).First(e => kinds[e]?.Attack is not null && kinds[e]!.Has(KindAnims.Walk)
                && ActorBehaviours.OfKind(store, model, e).Any(b => b.Attacks) && e > 4);
            var index = SceneOps.AddActor(model, NewActor(SceneGame.Lba1, lba1, model, soldier, 2048, 0));
            var template = ActorBehaviours.Templates(SceneGame.Lba1, lba1, soldier).First(t => t.Template == "attack");
            ActorBehaviours.Apply(store, model, index, template);
            Check($"the attack template on an LBA1 kind (entity {soldier}) is a valid scene", !SceneValidator.Validate(model).Any(i => i.Severity == SceneIssueSeverity.Error));
            map.Scenes[0] = map.Scenes[0] with { Record = SceneSerializer.Write(model) };
            MapInstall.Install(map, mirror.Directory);
            var r = new LBAAssembler.Lba1.Runtime.Lba1Runtime(new Lba1RuntimeData(mirror.Directory));
            r.ChangeCube(28);
            r.Run(5);
            double Dist() => Math.Sqrt(Math.Pow(r.Objects[index].PosX - r.Hero.PosX, 2) + Math.Pow(r.Objects[index].PosZ - r.Hero.PosZ, 2));
            int life0 = r.Hero.LifePoint; var d0 = Dist();
            r.Run(150);
            var d1 = Dist();
            r.Run(450);
            Check("LBA1's runtime: it comes for Twinsen", d1 < d0 - 400, $"distance {d0:0} -> {d1:0} -> {Dist():0}");
            Check("  and hits him", r.Hero.LifePoint < life0, $"Twinsen's life {life0} -> {r.Hero.LifePoint}");
        }
        finally { mirror.Dispose(); }
    }
}
