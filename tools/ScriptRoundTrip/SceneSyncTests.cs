using LBAAssembler;
using LBAAssembler.LbaScript;
using LBAAssembler.Maps;
using LBAAssembler.Scenes;

namespace ScriptRoundTrip;

// What the scene editor windows save is what the rest of the editor reads (the tester, 2026-10-17: "I can edit a scene and click save but
// it does not update in the main editor window, the same with actors"; "if I put some rabbibunny skeletons in a scene they load up as
// twinsun with no ai"), on temporary copies of the retail files:
//   - SceneDocument.Saved is raised for every write a document makes (Save, Save as, each AutoSave step) with the scenes written, and only
//     then: the main window and the other editor windows reload on it;
//   - a fresh read of the folder (what those reloads do) sees moved, added and deleted actors -- also for a scene of a map's workspace,
//     which File > Save map captures;
//   - a new actor is valid for the engine: its scripts are a lone END, not empty (an empty one runs whatever follows it in the record);
//   - LBA2: an actor made as the scene editor makes one (Lba2EntityTable.KindForBody) is drawn with the body picked (BodyOf), where a
//     BODY.HQR entry under Twinsen's entity 0 (the old editor's) is drawn as Twinsen;
//   - LBA2: actor 1 is the Meca-penguin's slot, never shown: an actor the actor window adds to a scene without it (Lba2ActorPersistence) goes
//     after it (SceneOps.EnsureMecaPenguinSlot), the scene's other actors one number up with their references.
//   dotnet run -- scenesync
internal static class SceneSyncTests
{
    private static readonly string Lba1Dir = Environment.GetEnvironmentVariable("LBA1_DIR") ?? @"E:\GOG Games\Little Big Adventure";
    private static readonly string Lba2Dir = Environment.GetEnvironmentVariable("LBA2_DIR") ?? @"E:\GOG Games\Little Big Adventure 2 - Level viewer";
    private static int failures, checks;

    private static void Check(bool ok, string what)
    {
        checks++;
        if (ok) return;
        failures++;
        Console.WriteLine($"  FAIL {what}");
    }

    public static int Run(string[] args)
    {
        Lba2();
        Lba1();
        Lba1Map();
        PenguinSlot();
        RetailScripts();
        Console.WriteLine(failures == 0 ? $"scene sync tests: all {checks} checks passed" : $"scene sync tests: {failures} of {checks} checks FAILED");
        return failures == 0 ? 0 : 1;
    }

    // Every SceneDocument.Saved while `body` runs: (the document, the scenes it wrote).
    private static List<(SceneDocument Document, int[] Scenes)> Watch(Action body)
    {
        var seen = new List<(SceneDocument, int[])>();
        void Note(SceneDocument d, IReadOnlyList<int> s) => seen.Add((d, s.ToArray()));
        SceneDocument.Saved += Note;
        try { body(); }
        finally { SceneDocument.Saved -= Note; }
        return seen;
    }

    private static bool IsLoneEnd(byte[] script, ScriptKind kind)
    {
        if (script.Length != 1 || script[0] != 0) return false;
        var decoded = kind == ScriptKind.Life ? Bytecode.DecodeLife(script, out var error) : Bytecode.DecodeTrack(script, out error);
        return error is null && decoded.Count == 1;
    }

    private static void Lba2()
    {
        SceneHistory.Clear();
        var dir = Temp("lba2_sync_");
        File.Copy(Original(Lba2Dir, "SCENE.HQR"), Path.Combine(dir, "SCENE.HQR"));
        try
        {
            var table = Lba2EntityTable.Load(Lba2Dir);
            Check(table is not null, "lba2: the entity table reads");
            if (table is null) return;
            var store = new SceneStore(SceneGame.Lba2, dir);
            const int scene = 7;                // Citadel Island, Mr. Paul's house: Twinsen, a helper, Mr. Paul
            var original = store.Load(scene);
            var doc = SceneDocument.Open(store, scene);

            // unsaved edits stay the document's own
            var seen = Watch(() => doc.Edit("Move actor 2", m => m.Actors[2].X += 512));
            Check(seen.Count == 0, "lba2: an unsaved edit raises no Saved");
            Check(new SceneStore(SceneGame.Lba2, dir).Load(scene).Actors[2].X == original.Actors[2].X, "lba2: an unsaved edit isn't in the files");

            // a Rabbibunny skeleton added as the scene editor adds one (New 3D actor: the body picked gives the kind of actor)
            const int skeletonBody = 35;        // BODY.HQR: "Rabbibunny skeleton"
            var kind = table.KindForBody(skeletonBody)!.Value;
            var added = SceneOps.BlankActor(SceneGame.Lba2, 2560, 768, 3072, kind.Entity);
            added.Body = kind.Body; added.Anim = kind.Anim;
            var index = -1;
            doc.Edit("Add actor", m => { SceneOps.EnsureMecaPenguinSlot(m); index = SceneOps.AddActor(m, added); });
            seen = Watch(() => doc.Save());
            Check(seen.Count == 1 && ReferenceEquals(seen[0].Document, doc) && seen[0].Scenes.SequenceEqual(new[] { scene }), "lba2: Save raises Saved once, for its scene");

            // what the main window's reload reads
            var reread = new SceneStore(SceneGame.Lba2, dir).Load(scene);
            Check(reread.Actors.Count == original.Actors.Count + 1, "lba2: the saved scene has the new actor");
            Check(reread.Actors[2].X == original.Actors[2].X + 512, "lba2: the saved scene has the moved actor");
            var skeleton = reread.Actors[index];
            Check(skeleton.Entity == kind.Entity && table.BodyOf(skeleton.Entity, skeleton.Body) == skeletonBody, $"lba2: the new actor is drawn as the skeleton (entity {skeleton.Entity}, body {skeleton.Body})");
            Check(IsLoneEnd(skeleton.Life, ScriptKind.Life) && IsLoneEnd(skeleton.Track, ScriptKind.Track), "lba2: the new actor's scripts are a lone END");
            Check(!store.Validate(scene, reread).Any(i => i.Severity == SceneIssueSeverity.Error), "lba2: the saved scene validates");
            var emptied = reread.Clone();
            emptied.Actors[index].Life = Array.Empty<byte>();
            Check(store.Validate(scene, emptied).Any(i => i.Severity == SceneIssueSeverity.Warning && i.Where == $"actor {index} life script"), "lba2: an empty script (the old editor's new actors) is warned about");
            // the old scene editor's actor: BODY.HQR 35 under Twinsen's entity 0 -- drawn as Twinsen's usual body, which is what the game showed
            Check(table.BodyOf(0, skeletonBody) == table.BodyOf(0, 0), "lba2: a BODY.HQR entry under entity 0 is drawn as Twinsen");

            // a second editor window on the scene reads the save when it reverts (it does on Saved)
            var other = SceneDocument.Open(new SceneStore(SceneGame.Lba2, dir), scene);
            Check(other.Scene.Actors.Count == reread.Actors.Count, "lba2: another document opened after the save sees it");

            // deleting, saving as another slot, and the AutoSave steps
            doc.Edit("Delete actor", m => SceneOps.DeleteActor(m, index));
            seen = Watch(() => doc.Save());
            Check(seen.Count == 1 && new SceneStore(SceneGame.Lba2, dir).Load(scene).Actors.Count == original.Actors.Count, "lba2: a deletion saved is in the files");
            seen = Watch(() => doc.SaveAs(8));
            Check(seen.Count == 1 && seen[0].Scenes.SequenceEqual(new[] { 8 }), "lba2: Save as raises Saved for the slot written");
            doc.AutoSave = true;
            seen = Watch(() => { doc.Edit("Move actor 2 again", m => m.Actors[2].Z += 512); doc.Undo(); doc.Redo(); });
            Check(seen.Count == 3 && seen.All(s => s.Scenes.SequenceEqual(new[] { scene })), "lba2: AutoSave's edit, undo and redo each raise Saved");
            doc.AutoSave = false;
            seen = Watch(() => doc.Revert());
            Check(seen.Count == 0, "lba2: Revert writes nothing and raises no Saved");
        }
        finally { Cleanup(dir); SceneHistory.Clear(); }
    }

    private static void Lba1()
    {
        SceneHistory.Clear();
        var dir = TempLba1("lba1_sync_");
        try
        {
            var store = new SceneStore(SceneGame.Lba1, dir);
            const int scene = 16;               // Citadel Island, inside a Rabbibunny house
            var original = store.Load(scene);
            var doc = SceneDocument.Open(store, scene, withGrid: true);
            doc.Edit("Move actor 1", m => m.Actors[1].X += 512);
            var index = -1;
            doc.Edit("Add actor", m => index = SceneOps.AddActor(m, SceneOps.BlankActor(SceneGame.Lba1, 2048, 256, 2048, entity: 22)));
            var seen = Watch(() => doc.Save());
            Check(seen.Count == 1 && seen[0].Scenes.SequenceEqual(new[] { scene }), "lba1: Save raises Saved once, for its scene");
            var reread = new SceneStore(SceneGame.Lba1, dir).Load(scene);
            Check(reread.Actors.Count == original.Actors.Count + 1 && reread.Actors[index].Entity == 22, "lba1: the saved scene has the new actor");
            Check(reread.Actors[1].X == original.Actors[1].X + 512, "lba1: the saved scene has the moved actor");
            using (Opcodes.Use(Opcodes.Lba1))
                Check(IsLoneEnd(reread.Actors[index].Life, ScriptKind.Life) && IsLoneEnd(reread.Actors[index].Track, ScriptKind.Track), "lba1: the new actor's scripts are a lone END");
            Check(!store.Validate(scene, reread, store.LoadGrid(scene)).Any(i => i.Severity == SceneIssueSeverity.Error), "lba1: the saved scene validates");
        }
        finally { Cleanup(dir); SceneHistory.Clear(); }
    }

    // A scene of a map, edited in its workspace (File > Open map's copy of the game folder) and saved by a document: File > Save map's capture has it.
    private static void Lba1Map()
    {
        SceneHistory.Clear();
        var dir = TempLba1("lba1_syncmap_");
        try
        {
            var map = MapNew.Lba1Interior(dir, 28, "Scene sync test");
            var at = MapInstall.Install(map, dir);
            var store = new SceneStore(SceneGame.Lba1, dir);
            var doc = SceneDocument.Open(store, at.FirstScene, withGrid: true);
            var before = doc.Scene.Actors.Count;
            doc.Edit("Add actor", m => SceneOps.AddActor(m, SceneOps.BlankActor(SceneGame.Lba1, 2048, 256, 2048, entity: 22)));
            var seen = Watch(() => doc.Save());
            Check(seen.Count == 1 && seen[0].Scenes.SequenceEqual(new[] { at.FirstScene }), "map: the workspace scene's save raises Saved");
            var captured = MapInstall.Capture(at);
            var scene = SceneSerializer.Parse(SceneGame.Lba1, captured.Scenes[0].Record);
            Check(scene.Actors.Count == before + 1 && scene.Actors[^1].Entity == 22, "map: Save map captures the actor the document added");
        }
        finally { Cleanup(dir); SceneHistory.Clear(); }
    }

    // LBA2's actor 1 is the Meca-penguin's slot (SceneOps.IsMecaPenguinSlot): the main window's actor window (Add Actor Here, then Apply:
    // Lba2ActorPersistence) adding an actor to a scene without it -- a new map's room from before 2026-10-17 -- puts it in first, as the scene
    // editor does, or the game never shows the actor; the numbers that moves are what ActorAttributesWindow.SceneRenumbered reports.
    private static void PenguinSlot()
    {
        SceneHistory.Clear();
        var dir = Temp("lba2_syncslot_");
        foreach (var name in new[] { "SCENE.HQR", "RESS.HQR" }) File.Copy(Original(Lba2Dir, name), Path.Combine(dir, name));
        try
        {
            var store = new SceneStore(SceneGame.Lba2, dir);
            var table = Lba2EntityTable.Load(dir)!;
            const int scene = 7;
            bool WarnsActor1(SceneModel m) => SceneValidator.Validate(m, new SceneValidationOptions()).Any(i => i.Where == "actor 1" && i.Severity == SceneIssueSeverity.Warning);
            var retail = store.Load(scene);
            Check(!WarnsActor1(retail), "penguin: a retail scene's actor 1 isn't warned about");

            // Zoé, as the actor window has her: BODY.HQR 26 and an animation of her kind (ANIM.HQR entries), her kind chosen
            var zoe = table.KindForBody(26)!.Value;
            var zoeAnim = table.Entities[zoe.Entity].Anims.First(a => a.Generic == zoe.Anim).Anim;
            var blank = new Lba2ActorPersistence.Snapshot(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            var placed = new Lba2ActorPersistence.Snapshot(2048, 768, 2048, 0, 26, zoeAnim, 50, 1, 0, 0, 0x0806);

            // a room with Twinsen alone: the session has the new actor as actor 1 (its index in the scene 0)
            var room = retail.Clone();
            room.Actors.RemoveRange(1, room.Actors.Count - 1);
            store.Save(scene, room);
            var error = Lba2ActorPersistence.Save(dir, scene, 0, blank, placed, out _, zoe.Entity);
            var saved = store.Load(scene);
            Check(error is null && saved.Actors.Count == 3 && SceneOps.IsMecaPenguinSlot(saved.Actors[1]) && saved.Actors[2].Entity == zoe.Entity && table.BodyOf(saved.Actors[2].Entity, saved.Actors[2].Body) == 26,
                $"penguin: the actor window's new actor in a room with Twinsen alone goes after the penguin's slot ({error})");
            Check(saved.Actors.Count - 1 != 0 + 1, "penguin: and the actor window sees the scene's numbers moved");
            Check(IsLoneEnd(saved.Actors[2].Life, ScriptKind.Life) && !WarnsActor1(saved), "penguin: with scripts that end, and nothing warned about");

            // an older map's scene whose actor 1 is an actor of its own (Mr. Paul here): the slot goes in front of it, its references renumbered
            var old = retail.Clone();
            try { SceneOps.DeleteActor(old, 1); } catch (SceneEditException) { SceneOps.DeleteActor(old, 1, retarget: 0); }
            Check(WarnsActor1(old), "penguin: an actor in the penguin's slot is warned about");
            store.Save(scene, old);
            var paul = old.Actors[1];
            error = Lba2ActorPersistence.Save(dir, scene, old.Actors.Count - 1, blank, placed, out _, zoe.Entity);
            saved = store.Load(scene);
            Check(error is null && saved.Actors.Count == old.Actors.Count + 2 && SceneOps.IsMecaPenguinSlot(saved.Actors[1])
                  && saved.Actors[2].Entity == paul.Entity && saved.Actors[2].X == paul.X && saved.Actors[^1].Entity == zoe.Entity && !WarnsActor1(saved),
                $"penguin: an older map's actor in the slot moves up one, the new actor after it ({error})");

            Check(!SceneOps.EnsureMecaPenguinSlot(new SceneStore(SceneGame.Lba1, Lba1Dir, Path.GetFileName(Original(Lba1Dir, "SCENE.HQR"))).Load(16)), "penguin: LBA1 has no such slot");
        }
        finally { Cleanup(dir); SceneHistory.Clear(); }
    }

    // No retail actor has an empty script (they all end in END): an empty one is only ever an editor's mistake.
    private static void RetailScripts()
    {
        foreach (var (game, store, count) in new[] { (SceneGame.Lba1, new SceneStore(SceneGame.Lba1, Lba1Dir, Path.GetFileName(Original(Lba1Dir, "SCENE.HQR"))), 120),
                                                    (SceneGame.Lba2, new SceneStore(SceneGame.Lba2, Lba2Dir, Path.GetFileName(Original(Lba2Dir, "SCENE.HQR"))), 222) })
        {
            var empty = 0;
            for (var s = 0; s < count; s++)
            {
                SceneModel scene;
                try { scene = store.Load(s); }
                catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException) { continue; }
                empty += scene.Actors.Count(a => a.Life.Length == 0 || a.Track.Length == 0);
            }
            Check(empty == 0, $"{game}: no retail actor has an empty script ({empty} found)");
        }
        // LBA2's object 1 is the engine's Nitro-Meca-Penguin (NUM_PINGOUIN, COMMON.H): every scene start makes it dead and body-less
        // (RestartMecaPingouin, OBJECT.CPP), so every retail scene keeps a placeholder there
        var lba2 = new SceneStore(SceneGame.Lba2, Lba2Dir, Path.GetFileName(Original(Lba2Dir, "SCENE.HQR")));
        var odd = new List<string>();
        for (var s = 0; s < 222; s++)
        {
            SceneModel scene;
            try { scene = lba2.Load(s); }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException) { continue; }
            if (scene.Actors.Count < 2) { odd.Add($"{s}: no actor 1"); continue; }
            var a = scene.Actors[1];
            if (!SceneOps.IsMecaPenguinSlot(a)) odd.Add($"{s}: entity {a.Entity} flags 0x{a.Flags:X} move {a.Move} at ({a.X},{a.Y},{a.Z}) life {a.Life.Length}");
        }
        Check(odd.Count == 0, $"LBA2: every retail scene's actor 1 is the meca penguin's placeholder ({odd.Count} not: {string.Join("; ", odd.Take(6))})");
    }

    private static string Temp(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    // A game file as it was before the editor first changed it: the one-time .bak copy saves leave, when the folder has one.
    private static string Original(string directory, string name)
        => File.Exists(Path.Combine(directory, name + ".bak")) ? Path.Combine(directory, name + ".bak") : Path.Combine(directory, name);

    // The LBA1 scene files, and what the grid validator reads.
    private static string TempLba1(string prefix)
    {
        var dir = Temp(prefix);
        foreach (var name in new[] { "SCENE.HQR", "LBA_GRI.HQR", "LBA_BLL.HQR", "LBA_BRK.HQR" }) File.Copy(Original(Lba1Dir, name), Path.Combine(dir, name));
        return dir;
    }

    private static void Cleanup(string dir)
    {
        // only ever the temp folders made above
        if (dir.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(dir).StartsWith("lba", StringComparison.Ordinal))
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
