using LBAAssembler;
using LBAAssembler.Maps;
using LBAAssembler.Scenes;

namespace ScriptRoundTrip;

// A tester (SIN, 2026-10-17): "if I was to give an npc a script it would not save it to the custom maps files but instead to the main game
// directorys scene.hqr ... the .ls2 it already gives you that file, so it'd just need to also save the npc scripts to it"; "make a selection
// of pre existing scripts to apply to actors? so what would technically be the default for that npc? for example a rabbi skeleton should
// see, seekout, and try to kill the player?"
//   mapscripts fixture <LBA2 game folder> <out.lbamap> [lba1 <LBA1 folder> <out.lbamap>]: maps to try it on by hand -- an LBA2 interior
//     from scene 4 with a skeleton (actor 2, END scripts), an LBA1 interior from slot 28 with a soldier (actor 1, END scripts)
internal static partial class MapScriptsTest
{
    public const int Skeleton = 35;           // BODY.HQR: the rabbibunny skeleton (entity 22)

    public static int Run(string[] args)
    {
        if (args.Length >= 4 && args[1] == "fixture") return Fixture(args);
        if (args.Length >= 4 && args[1] == "show") return ShowMapActor(args);
        if (args.Length >= 4 && args[1] == "presets") return Presets(args);
        if (args.Length >= 4 && args[1] == "kinds") return Kinds(args);
        if (args.Length >= 2 && File.Exists(Path.Combine(args[1], "SCENE.HQR"))) return Checks(args);
        Console.WriteLine("mapscripts <LBA2 game folder (a sandbox)> [<LBA1 game folder (a sandbox)>]: the checks");
        Console.WriteLine("mapscripts fixture <LBA2 game folder> <out.lbamap> [lba1 <LBA1 folder> <out.lbamap>]");
        Console.WriteLine("mapscripts show <map.lbamap> <actor> [scene k]: an actor of a map's scene, its settings and scripts as the map file has them");
        return 1;
    }

    //   mapscripts presets lba1|lba2 <game folder> <entity> [apply <id>]: the behaviours an actor of that kind can have (ActorBehaviours), its
    //     kind's usual one; with apply, one of them put on a new actor of a new interior map's scene, the result checked and printed
    private static int Presets(string[] args)
    {
        var game = args[2] == "lba1" ? SceneGame.Lba1 : SceneGame.Lba2;
        var folder = args[3];
        var entity = int.Parse(args[4]);
        var store = new SceneStore(game, folder);
        var map = game == SceneGame.Lba1 ? MapNew.Lba1Interior(folder, 28, "Presets") : MapNew.Lba2Interior(folder, 4, "Presets");
        var scene = SceneSerializer.Parse(game, map.Scenes[0].Record);
        var anims = KindAnims.Of(game, folder, entity);
        Console.WriteLine($"entity {entity}: animations {(anims is null ? "(none)" : string.Join(", ", anims.Anims.Keys.Order().Select(g => anims.Hits(g) ? $"{g}*hit" : anims.ThrowsSomething(g) ? $"{g}*throw" : $"{g}")))}; attack {anims?.Attack}");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var all = ActorBehaviours.For(store, scene, entity);
        Console.WriteLine($"{all.Count} behaviours ({sw.ElapsedMilliseconds} ms):");
        foreach (var b in all) Console.WriteLine($"  {b.Id,-12} {b.Name}: {b.Description}{(b.Source.Length > 0 ? $"  [{b.Source}]" : "")}");
        var usual = ActorBehaviours.DefaultFor(store, scene, entity);
        Console.WriteLine($"usual: {usual.Id} {usual.Name}: {usual.Description}");
        if (args.Length > 6 && args[5] == "apply")
        {
            var chosen = all.First(b => b.Id == args[6]);
            var actor = SceneOps.BlankActor(game, scene.Hero.X + 2048, scene.Hero.Y, scene.Hero.Z, entity);
            SceneOps.EnsureMecaPenguinSlot(scene);
            var index = SceneOps.AddActor(scene, actor);
            int? text = chosen.NeedsText ? ActorBehaviours.Texts(folder, game, scene.Island).Select(t => (int?)t.Id).FirstOrDefault() : null;
            var notes = ActorBehaviours.Apply(store, scene, index, chosen, text);
            foreach (var n in notes) Console.WriteLine($"  note: {n}");
            foreach (var issue in SceneValidator.Validate(scene)) Console.WriteLine($"  {issue}");
            var scripts = LBAAssembler.LbaScript.SceneScripts.Load(SceneSerializer.Write(scene), -1, null, game == SceneGame.Lba1);
            Console.WriteLine(scripts.GetText(index, LBAAssembler.LbaScript.ScriptKind.Life));
            Console.WriteLine(scripts.GetText(index, LBAAssembler.LbaScript.ScriptKind.Track));
            var a = scene.Actors[index];
            Console.WriteLine($"actor {index}: move {a.Move} flags 0x{a.Flags:X} life {a.LifePoints} armour {a.Armor} srot {a.SRot}; {scene.TrackPoints.Count} track points, {scene.Zones.Count} zones");
        }
        return 0;
    }

    //   mapscripts kinds lba1|lba2 <game folder>: every kind of actor's animations read (KindAnims), which hit, and the usual behaviour of each
    //     kind the game has actors of
    private static int Kinds(string[] args)
    {
        var game = args[2] == "lba1" ? SceneGame.Lba1 : SceneGame.Lba2;
        var folder = args[3];
        var kinds = KindAnims.All(game, folder)!;
        var anims = kinds.Where(k => k is not null).Sum(k => k!.Anims.Count);
        var unread = kinds.Where(k => k is not null).Sum(k => k!.Unread.Count);
        Console.WriteLine($"{kinds.Count} kinds, {anims} animations, {unread} whose actions couldn't be read to their end; " +
                          $"{kinds.Count(k => k?.Attack is not null)} kinds can attack");
        var store = new SceneStore(game, folder);
        var map = game == SceneGame.Lba1 ? MapNew.Lba1Interior(folder, 28, "Kinds") : MapNew.Lba2Interior(folder, 4, "Kinds");
        var scene = SceneSerializer.Parse(game, map.Scenes[0].Record);
        for (var e = 0; e < kinds.Count; e++)
        {
            if (kinds[e] is null) continue;
            var retail = ActorBehaviours.OfKind(store, scene, e);
            if (retail.Count == 0 && kinds[e]!.Attack is null) continue;
            var usual = ActorBehaviours.DefaultFor(store, scene, e);
            Console.WriteLine($"  entity {e,3}: {retail.Count} behaviour(s) of the game, attack anim {kinds[e]!.Attack?.ToString() ?? "-"}; usual: {usual.Name} -- {usual.Description}");
        }
        return 0;
    }

    //   mapscripts show <map.lbamap> <actor> [k]: the map's k-th scene's actor, as the file has it
    private static int ShowMapActor(string[] args)
    {
        var map = LbaMap.Load(args[2]);
        var k = args.Length > 4 ? int.Parse(args[4]) : 0;
        var game = map.IsLba1 ? SceneGame.Lba1 : SceneGame.Lba2;
        var model = SceneSerializer.Parse(game, map.Scenes[k].Record);
        var n = int.Parse(args[3]);
        Console.WriteLine($"{Path.GetFileName(args[2])}: \"{map.Name}\", scene {k} of {map.Scenes.Count}, {model.Actors.Count} actors, {model.Zones.Count} zones, {model.TrackPoints.Count} track points");
        if (n >= model.Actors.Count) { Console.WriteLine($"no actor {n}"); return 1; }
        var a = model.Actors[n];
        Console.WriteLine($"actor {n}: entity {a.Entity} body {a.Body} anim {a.Anim} move {a.Move} flags 0x{a.Flags:X} life points {a.LifePoints} armour {a.Armor} at ({a.X}, {a.Y}, {a.Z}); life {a.Life.Length} bytes, track {a.Track.Length} bytes");
        var scripts = LBAAssembler.LbaScript.SceneScripts.Load(map.Scenes[k].Record, -1, null, map.IsLba1);
        foreach (var kind in new[] { LBAAssembler.LbaScript.ScriptKind.Life, LBAAssembler.LbaScript.ScriptKind.Track })
        {
            try { Console.WriteLine(scripts.GetText(n, kind)); }
            catch (InvalidOperationException) { Console.WriteLine($"// {kind.ToString().ToLowerInvariant()} script: {BitConverter.ToString(kind == LBAAssembler.LbaScript.ScriptKind.Life ? a.Life : a.Track)} (no C text)"); }
        }
        return 0;
    }

    private static int Fixture(string[] args)
    {
        var game = args[2];
        var map = MapNew.Lba2Interior(game, 4, "Script test");
        var model = SceneSerializer.Parse(SceneGame.Lba2, map.Scenes[0].Record);
        var kind = Lba2EntityTable.Load(game)!.KindForBody(Skeleton)!.Value;
        var skeleton = SceneOps.BlankActor(SceneGame.Lba2, model.Hero.X + 2048, model.Hero.Y, model.Hero.Z, kind.Entity);
        skeleton.Body = kind.Body; skeleton.Anim = kind.Anim;
        SceneOps.EnsureMecaPenguinSlot(model);
        SceneOps.AddActor(model, skeleton);
        map.Scenes[0] = map.Scenes[0] with { Record = SceneSerializer.Write(model) };
        map.Save(args[3]);
        Console.WriteLine($"{args[3]}: LBA2 interior, actors {model.Actors.Count} (actor {model.Actors.Count - 1}: the skeleton, entity {kind.Entity})");
        if (args.Length >= 7 && args[4] == "lba1")
        {
            var lba1 = MapNew.Lba1Interior(args[5], 28, "LBA1 script test");
            var scene = SceneSerializer.Parse(SceneGame.Lba1, lba1.Scenes[0].Record);
            var soldier = SceneOps.BlankActor(SceneGame.Lba1, scene.Hero.X + 1024, scene.Hero.Y, scene.Hero.Z, 7);
            SceneOps.AddActor(scene, soldier);
            lba1.Scenes[0] = lba1.Scenes[0] with { Record = SceneSerializer.Write(scene) };
            lba1.Save(args[6]);
            Console.WriteLine($"{args[6]}: LBA1 interior (slot 28), actors {scene.Actors.Count}");
        }
        return 0;
    }
}
