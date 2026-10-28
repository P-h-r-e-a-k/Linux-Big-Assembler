using System.Diagnostics;
using System.Text.Json;
using LBAAssembler;
using LBAAssembler.LbaScript;
using LBAAssembler.Maps;
using LBAAssembler.Scenes;

namespace ScriptRoundTrip;

// A map's actors and its Play, the way to the game (a tester, 2026-10-17: "if I place zoe in a scene it does not load her in the actual
// game ... if I put some rabbibunny skeletons in a scene they load up as twinsun with no ai"; "The ability to load a custom map inside the
// editor to play also does not work, it goes black and then goes back to the editor").
//   mapactors <game folder>: a sandbox -- it makes a mirror inside it (removed again) and only reads the rest
//   - the mirror (LiveDataRoot: File > Open map's workspace, Test edits) has the engine's data folders too: VIDEO\VIDEO.HQR, without which
//     the engine stops at once ("required game data is missing"), so Play in a map's workspace ended before it started
//   - every body a kind of actor has: Lba2EntityTable.KindForBody gives an entity, and a body number in it the engine draws that body from
//   - a new interior map has actor 1, the Meca-penguin's slot (the engine hides whatever is there); putting the slot back in a scene that
//     lacks it renumbers the scripts' references to the actors after it
//   - two interior maps installed in the mirror, as a map's workspace has them, played headless by the engine on the mirror: one with actors
//     made the old way (a blank scene with the hero alone, entity 0 with the raw BODY.HQR index as its body: Zoé first, then a skeleton) --
//     Zoé is hidden as the penguin's slot, the skeleton is Twinsen; one made as the editor makes them now -- the LBA2 scene editor's New 3D
//     actor (Zoé, a rabbibunny skeleton) and Actor from another scene (scene 184's skeleton, scripts and all), the actor attributes window's
//     body change to Zoé with her umbrella (Lba2ActorPersistence) -- each drawn with its body
internal static class MapActorsTest
{
    private const int Zoe = 26, ZoeUmbrella = 27, Skeleton = 35, TwinsenBody = 0;

    public static int Run(string[] args)
    {
        if (args.Length < 2 || !File.Exists(Path.Combine(args[1], "SCENE.HQR"))) { Console.WriteLine("mapactors <LBA2 game folder (a sandbox)>"); return 1; }
        var game = Path.GetFullPath(args[1]);
        var failures = 0;
        void Check(string what, bool ok, string detail = "")
        {
            Console.WriteLine($"  {what}: {(ok ? "ok" : "FAILED")}{(detail.Length > 0 ? "  " + detail : "")}");
            if (!ok) failures++;
        }

        // ---- the kinds of actor
        var table = Lba2EntityTable.Load(game);
        if (table is null) { Console.WriteLine("no entity table (RESS.HQR 44)"); return 1; }
        var bodies = table.Entities.SelectMany(e => e.Bodies.Select(b => b.Body)).Distinct().ToList();
        var wrong = bodies.Where(b => table.KindForBody(b) is not { } k || table.BodyOf(k.Entity, k.Body) != b).ToList();
        Check($"each of the {bodies.Count} bodies kinds of actor have: a kind and a body number the engine draws it from", wrong.Count == 0,
            wrong.Count > 0 ? $"wrong for {string.Join(", ", wrong.Take(10))}" : "");
        var zoe = table.KindForBody(Zoe); var skeleton = table.KindForBody(Skeleton);
        Check("Zoé's body is entity 15's normal body, the skeleton's entity 22's", zoe is (15, 0, _) && skeleton is (22, 0, _), $"Zoé {zoe}, skeleton {skeleton}");
        Check("the old way (entity 0 with body number 26) is drawn with Twinsen's body", table.BodyOf(0, Zoe) == TwinsenBody);

        // ---- actor 1, the Meca-penguin's slot
        var blank = MapNew.Lba2Interior(game, 4, "Actor test");
        var fresh = SceneSerializer.Parse(SceneGame.Lba2, blank.Scenes[0].Record);
        Check("a new interior map has the hero and actor 1, the Meca-penguin's slot", fresh.Actors.Count == 2 && SceneOps.IsMecaPenguinSlot(fresh.Actors[1]));
        var retail = new SceneStore(SceneGame.Lba2, game).Load(184);
        var tralu = retail.Actors[3];                                                         // (Tralu's skeleton: its life script names actors 0 and 1)
        var lacking = fresh.Clone();
        lacking.Actors.RemoveAt(1);
        SceneOps.AddActor(lacking, SceneOps.BlankActor(SceneGame.Lba2, 1000, 256, 1000));     // actor 1, hidden by the game
        SceneOps.AddActor(lacking, tralu.Clone());                                            // actor 2, its script naming actor 1
        Check("a scene whose actor 1 is an actor of its own gets a warning", SceneValidator.Validate(lacking).Any(i => i.Where == "actor 1"));
        var refsBefore = SceneOps.ReferencesTo(lacking, ArgRole.Obj, 1).Count(r => r.Actor == 2);
        var put = SceneOps.EnsureMecaPenguinSlot(lacking);
        Check("putting the slot back moves the actors up one, the scripts' references with them",
            put && lacking.Actors.Count == 4 && SceneOps.IsMecaPenguinSlot(lacking.Actors[1]) && refsBefore > 0
            && SceneOps.ReferencesTo(lacking, ArgRole.Obj, 2).Count(r => r.Actor == 3) == refsBefore && !SceneOps.ReferencesTo(lacking, ArgRole.Obj, 1).Any(r => r.Actor == 3),
            $"{refsBefore} reference(s)");
        Check("and a scene that has the slot is left as it is", !SceneOps.EnsureMecaPenguinSlot(lacking) && lacking.Actors.Count == 4);

        // ---- the mirror a map's workspace is
        const string folderName = "_MAPACTORS_TEST";
        LiveDataRoot.CleanStale(game, folderName);
        var mirror = LiveDataRoot.Create(game, null, folderName);
        if (mirror is null) { Console.WriteLine("couldn't make the mirror"); return 1; }
        var user = Path.Combine(Path.GetTempPath(), "mapactors_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var video = Path.Combine("VIDEO", "VIDEO.HQR");
            if (File.Exists(Path.Combine(game, video)))
                Check("the mirror has VIDEO\\VIDEO.HQR (the engine doesn't start without it)", File.Exists(Path.Combine(mirror.Directory, video))
                    && new FileInfo(Path.Combine(mirror.Directory, video)).Length == new FileInfo(Path.Combine(game, video)).Length);
            else Console.WriteLine("  (the folder has no VIDEO\\VIDEO.HQR: the engine runs below would stop at once)");

            var hero = fresh.Hero;
            // the old way: a blank scene of the hero alone, actors of entity 0 with raw BODY.HQR indices as their bodies
            var oldMap = MapNew.Lba2Interior(game, 4, "Old way");
            var oldScene = SceneSerializer.Parse(SceneGame.Lba2, oldMap.Scenes[0].Record);
            oldScene.Actors.RemoveAt(1);
            foreach (var (body, dx) in new[] { (Zoe, -1024), (Skeleton, 1024) })
            {
                var actor = SceneOps.BlankActor(SceneGame.Lba2, hero.X + dx, hero.Y, hero.Z);
                actor.Body = body;
                SceneOps.AddActor(oldScene, actor);
            }
            oldMap.Scenes[0] = oldMap.Scenes[0] with { Record = SceneSerializer.Write(oldScene) };

            // the editor's way
            var map = MapNew.Lba2Interior(game, 4, "Actor test");
            var model = SceneSerializer.Parse(SceneGame.Lba2, map.Scenes[0].Record);
            SceneActorModel Made(int dx, int dz, int body)
            {
                var kind = table.KindForBody(body)!.Value;
                var actor = SceneOps.BlankActor(SceneGame.Lba2, hero.X + dx, hero.Y, hero.Z + dz, kind.Entity);
                actor.Body = kind.Body; actor.Anim = kind.Anim;
                return actor;
            }
            SceneOps.AddActor(model, Made(-1024, 0, Zoe));                                    // actor 2: New 3D actor, Zoé
            SceneOps.AddActor(model, Made(0, 1024, Skeleton));                                // actor 3: New 3D actor, the skeleton
            var copy = tralu.Clone(); copy.X = hero.X; copy.Y = hero.Y; copy.Z = hero.Z - 1536;
            SceneOps.AddActor(model, copy);                                                   // actor 4: Actor from another scene
            SceneOps.AddActor(model, Made(1024, 1024, TwinsenBody));                          // actor 5: Twinsen's kind, then given Zoé's umbrella body
            map.Scenes[0] = map.Scenes[0] with { Record = SceneSerializer.Write(model) };

            var oldAt = MapInstall.Install(oldMap, mirror.Directory).FirstScene;
            var installed = MapInstall.Install(map, mirror.Directory);
            var scene = installed.FirstScene;

            // the actor attributes window's Apply: the body of another kind of actor
            var five = new SceneStore(SceneGame.Lba2, mirror.Directory).Load(scene).Actors[5];
            var before = new Lba2ActorPersistence.Snapshot(five.X, five.Y, five.Z, five.Beta, TwinsenBody, 0, 255, 1, 0, 0, five.Flags);
            var error = Lba2ActorPersistence.Save(mirror.Directory, scene, 4, before, before with { Body = ZoeUmbrella }, out var note);
            var saved = new SceneStore(SceneGame.Lba2, mirror.Directory).Load(scene).Actors[5];
            Check("a body change to another kind's body is saved, the actor made that kind", error is null && note is null && table.BodyOf(saved.Entity, saved.Body) == ZoeUmbrella,
                $"entity {saved.Entity}, body {saved.Body}" + ((error ?? note) is { } why ? ": " + why : ""));

            // ---- the engine on the mirror
            var engine = Lba2Engine.Find();
            if (engine is null) { Console.WriteLine("  no lba2cc.exe found: the engine checks are not run"); failures++; }
            else
            {
                Directory.CreateDirectory(user);
                var old = Drawn(engine, mirror.Directory, user, oldAt, out var oldEntered);
                Check($"the engine starts on the mirror and enters the old way's scene {oldAt}", oldEntered);
                int OldBody(int actor) => old.TryGetValue(actor, out var o) ? o.Body : -2;
                Check("the old way: Zoé, actor 1, is hidden (the penguin's slot), the skeleton is Twinsen", OldBody(1) == -1 && OldBody(2) == TwinsenBody, $"bodies {OldBody(1)}, {OldBody(2)}");
                var drawn = Drawn(engine, mirror.Directory, user, scene, out var entered);
                Check($"the engine enters the map's scene {scene}", entered);
                int Body(int actor) => drawn.TryGetValue(actor, out var d) ? d.Body : -2;
                Check("Zoé (New 3D actor) comes out as Zoé", Body(2) == Zoe, $"body {Body(2)}");
                Check("the skeleton (New 3D actor) comes out as the skeleton", Body(3) == Skeleton, $"body {Body(3)}");
                Check("scene 184's skeleton (Actor from another scene) comes out as the skeleton", Body(4) == Skeleton, $"body {Body(4)}");
                // (its own life script: Twinsen in its view, it follows him -- set_dir(MOVE_FOLLOW, 0))
                Check("and its script runs: it goes for Twinsen (move 2, follow)", drawn.TryGetValue(4, out var four) && four.Move == 2, $"move {(drawn.TryGetValue(4, out var f) ? f.Move : -2)}");
                Check("the actor given Zoé's umbrella body comes out with it", Body(5) == ZoeUmbrella, $"body {Body(5)}");

                // File > Save map, then Install map into game (Yes: the MAPS folder, MAPPACK.CPP) -- the mirror's MAPS: a file of its own there
                var (_, numbers) = MapInstall.InstallInMapsFolder(MapInstall.Capture(installed), mirror.Directory, "mapactors_test");
                var fromMaps = Drawn(engine, mirror.Directory, user, numbers[0], out var mapsEntered);
                Check($"saved and put in the MAPS folder (scene {numbers[0]}), the engine plays it with the same actors", mapsEntered
                    && Enumerable.Range(1, 5).All(a => fromMaps.TryGetValue(a, out var m) && drawn.TryGetValue(a, out var w) && m.Body == w.Body),
                    string.Join(", ", Enumerable.Range(1, 5).Select(a => fromMaps.TryGetValue(a, out var m) ? m.Body : -2)));
            }
        }
        finally
        {
            mirror.Dispose();
            try { if (Directory.Exists(user)) Directory.Delete(user, true); } catch (IOException) { }
        }
        Check("the mirror is removed, the folder's own VIDEO\\VIDEO.HQR still there", !Directory.Exists(mirror.Directory)
            && (!Directory.Exists(Path.Combine(game, "VIDEO")) || File.Exists(Path.Combine(game, "VIDEO", "VIDEO.HQR"))));
        Console.WriteLine(failures == 0 ? "map actors: all passed" : $"map actors: {failures} FAILED");
        return failures == 0 ? 0 : 1;
    }

    // The engine run headless on `directory` into `scene`: each actor's body as it draws it (its BODY.HQR entry, -1 none) and its move
    // (COMMON.H: 0 none, 2 MOVE_FOLLOW ...) a second after.
    private static Dictionary<int, (int Body, int Move)> Drawn(string engine, string directory, string user, int scene, out bool entered)
    {
        var state = Path.Combine(user, $"state{scene}.json");
        var start = new ProcessStartInfo(engine) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = directory };
        foreach (var a in new[] { "--headless", "--no-audio", "--no-autosave", "--game-dir", directory, "--user-dir", user,
                                  "--exec-at", "5", $"cube {scene}", "--exec-at", "40", $"dumpstate {state}", "--exec-at", "41", "status", "--tick", "60", "--exit" })
            start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { try { process.Kill(true); } catch (InvalidOperationException) { } }
        entered = (output.Result + errors.Result).Contains($"Cube: {scene}");
        var drawn = new Dictionary<int, (int, int)>();
        if (File.Exists(state))
            foreach (var actor in JsonDocument.Parse(File.ReadAllText(state)).RootElement.GetProperty("actors").EnumerateArray())
                drawn[actor.GetProperty("index").GetInt32()] = (actor.GetProperty("body").GetInt32(), actor.GetProperty("move").GetInt32());
        return drawn;
    }
}
