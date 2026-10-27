using System.IO;
using System.Text;
using LBAAssembler.LbaScript;

namespace LBAAssembler.Scenes;

// Ready-made behaviours for an actor (a tester, 2026-10-17: "make a selection of pre existing scripts to apply to actors? so what would
// technically be the default for that npc? for example a rabbi skeleton should see, seekout, and try to kill the player? when you spawn a new
// npc it does not have a script"). A behaviour is an actor's life and track scripts and the settings they work with (its move, turning speed,
// life points, armour, collisions):
//   - the game's own actors of the same kind (entity): their scripts as the game has them, folded where several actors share them, each
//     with a line saying what it does, carried into the new scene by ScriptRemap (actors, track points, zones, texts, scene variables
//     remapped; the story left alone)
//   - templates for any kind: stands still, wanders about, patrols between two track points, talks when spoken to (a text of the scene's
//     island), spots, chases and attacks Twinsen, keeps away from him -- written in the script language and compiled, with the walking and
//     attacking animations the kind has (KindAnims)
//   - "no script" (END scripts: it stands as placed)
// A new actor of a kind gets the kind's usual behaviour (DefaultFor): an enemy kind -- at least half the game's own actors of it attack -- its
// most used attacking behaviour; any other kind a behaviour of it whose lines this scene can show (talkers first, then the one bringing least
// along), else it stands still (a kind the game has no actor of too: Twinsen's own kinds have attacking animations).
internal sealed class ActorBehaviour
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string Source { get; init; } = "";
    public int Uses { get; init; }
    public bool Attacks { get; init; }
    public bool Talks { get; init; }
    public bool NeedsText { get; init; }
    // a game actor's: where it is (its scene, read once for the catalogue)
    internal SceneModel? SourceScene { get; init; }
    internal int SourceActor { get; init; }
    internal int SourceSceneNumber { get; init; } = -1;
    internal string? Template { get; init; }

    public override string ToString() => $"{Name}: {Description}";
}

internal static class ActorBehaviours
{
    public const string NoScriptId = "none";
    public const int Lba2RetailScenes = 222, Lba1RetailScenes = 120;

    // ---- the game's actors, by kind (cached per scene file and its time)
    private sealed record Retail(int Scene, int Index, SceneModel Model);
    private static readonly Dictionary<string, (DateTime Stamp, List<Retail> Actors)> retailCache = new(StringComparer.OrdinalIgnoreCase);

    private static List<Retail> RetailActors(SceneStore store)
    {
        var path = store.ScenePath;
        var stamp = File.GetLastWriteTimeUtc(path);
        lock (retailCache)
        {
            if (retailCache.TryGetValue(path, out var hit) && hit.Stamp == stamp) return hit.Actors;
            var list = new List<Retail>();
            var archive = HqrArchive.Open(path);
            var lba1 = store.Game == SceneGame.Lba1;
            var count = lba1 ? Lba1RetailScenes : Lba2RetailScenes;
            for (var scene = 0; scene < count; scene++)
            {
                var entry = lba1 ? scene : scene + 1;
                if (!archive.IsValid(entry)) continue;
                SceneModel model;
                try { model = SceneSerializer.Parse(store.Game, archive.Read(entry)); }
                catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException) { continue; }
                for (var n = 1; n < model.Actors.Count; n++)
                {
                    if (!lba1 && n == 1 && SceneOps.IsMecaPenguinSlot(model.Actors[1])) continue;
                    var a = model.Actors[n];
                    if (a.IsSprite || a.Entity < 0) continue;
                    if (a.Life.Length <= 1 && a.Track.Length <= 1) continue;      // (nothing to copy: "stands still" is a template)
                    list.Add(new Retail(scene, n, model));
                }
            }
            retailCache[path] = (stamp, list);
            return list;
        }
    }

    // Every behaviour an actor of `entity` in `target` can be given: no script, the templates, then the game's actors of that kind (most
    // used first).
    public static List<ActorBehaviour> For(SceneStore store, SceneModel target, int entity)
    {
        var list = new List<ActorBehaviour> { NoScript() };
        list.AddRange(Templates(store.Game, store.Directory, entity));
        list.AddRange(OfKind(store, target, entity));
        return list;
    }

    // An actor of another scene as a behaviour (Add > Actor from another scene: its scripts carried over, remapped).
    public static ActorBehaviour FromActor(SceneGame game, string directory, SceneModel scene, int sceneNumber, int index)
    {
        var analysis = Analyse(game, scene.Actors[index], KindAnims.Of(game, directory, scene.Actors[index].Entity));
        return new ActorBehaviour
        {
            Id = $"r:{sceneNumber}:{index}", Name = $"As in scene {sceneNumber}", Description = analysis.Text, Source = $"scene {sceneNumber}, actor {index}", Uses = 1,
            Attacks = analysis.Attacks, Talks = analysis.Talks, SourceScene = scene, SourceActor = index, SourceSceneNumber = sceneNumber,
        };
    }

    public static ActorBehaviour NoScript() => new()
    {
        Id = NoScriptId, Name = "No script", Description = "does nothing: it stands as placed (END scripts)", Source = "",
    };

    // The game's actors of this kind, their scripts folded (the same bytes are one behaviour, its uses counted), most used first.
    public static List<ActorBehaviour> OfKind(SceneStore store, SceneModel target, int entity)
    {
        var anims = KindAnims.Of(store.Game, store.Directory, entity);
        var groups = RetailActors(store).Where(r => r.Model.Actors[r.Index].Entity == entity)
            .GroupBy(r => Convert.ToHexString(r.Model.Actors[r.Index].Life) + "|" + Convert.ToHexString(r.Model.Actors[r.Index].Track) + "|" + r.Model.Actors[r.Index].Move)
            .Select(g => g.ToList()).ToList();
        var result = new List<ActorBehaviour>();
        foreach (var g in groups)
        {
            var first = g[0];
            var actor = first.Model.Actors[first.Index];
            var analysis = Analyse(store.Game, actor, anims);
            var where = $"scene {first.Scene}, actor {first.Index}" + (g.Count > 1 ? $" (and {g.Count - 1} more)" : "");
            result.Add(new ActorBehaviour
            {
                Id = $"r:{first.Scene}:{first.Index}", Name = $"As in scene {first.Scene}", Description = analysis.Text, Source = where, Uses = g.Count,
                Attacks = analysis.Attacks, Talks = analysis.Talks, SourceScene = first.Model, SourceActor = first.Index, SourceSceneNumber = first.Scene,
            });
        }
        return result.OrderByDescending(b => b.Attacks).ThenByDescending(b => b.Uses).ThenBy(b => b.SourceSceneNumber).ToList();
    }

    // ---- templates
    public static List<ActorBehaviour> Templates(SceneGame game, string directory, int entity)
    {
        var anims = KindAnims.Of(game, directory, entity);
        var walk = anims?.Has(KindAnims.Walk) == true;
        var attack = anims?.Attack;
        var list = new List<ActorBehaviour>
        {
            new() { Id = "t:still", Name = "Stands still", Description = "stands where it is placed, in its standing animation", Source = "template", Template = "still" },
            new() { Id = "t:wander", Name = "Wanders about", Description = walk ? "walks a little way, stops, turns a random way and walks on" : "turns a random way now and then (this kind has no walking animation)", Source = "template", Template = "wander" },
            new() { Id = "t:patrol", Name = "Patrols back and forth", Description = "walks between two track points (added beside it, to be moved where wanted), pausing at each", Source = "template", Template = "patrol" },
            new() { Id = "t:talk", Name = "Talks when spoken to", Description = "says a text you choose when Twinsen is near and presses the action key", Source = "template", Template = "talk", Talks = true, NeedsText = true },
            new()
            {
                Id = "t:attack", Name = "Spots, chases and attacks Twinsen",
                Description = attack is { } a
                    ? $"waits until it sees Twinsen (or he hits it), then goes for him{(walk ? "" : " (turning: this kind has no walking animation)")} and attacks him (its animation {a}) when he is close"
                    : "waits until it sees Twinsen (or he hits it), then goes for him -- this kind has no attacking animation, so it only catches him up",
                Source = "template", Template = "attack", Attacks = attack is not null,
            },
        };
        if (anims?.Has(KindAnims.Back) == true)
            list.Add(new() { Id = "t:flee", Name = "Keeps away from Twinsen", Description = "backs away from Twinsen, facing him, when he comes near; stands again once he is far", Source = "template", Template = "flee" });
        return list;
    }

    // ---- the usual behaviour of a kind
    public static ActorBehaviour DefaultFor(SceneStore store, SceneModel target, int entity)
    {
        var retail = OfKind(store, target, entity);
        var templates = Templates(store.Game, store.Directory, entity);
        // (an enemy kind: at least half the game's own actors of it attack -- a townsfolk kind has the odd fighter in a story scene; a kind the
        // game has no actor of stands: Twinsen's own kinds can strike too)
        var attackers = retail.Where(r => r.Attacks).Sum(r => r.Uses);
        if (attackers > 0 && attackers * 2 >= retail.Sum(r => r.Uses))
            return retail.Where(r => r.Attacks).OrderByDescending(r => r.Uses).ThenBy(r => Fit(store, target, r)).First();
        // (a townsperson: a behaviour of its kind whose lines this scene's island shows -- talkers first, then the one that brings least along
        // into the scene: routes, zones, references to its old scene's actors -- else it stands)
        var usable = retail.Where(r => !r.Talks || r.SourceScene!.Island == target.Island)
            .OrderByDescending(r => r.Talks).ThenBy(r => Fit(store, target, r)).ThenByDescending(r => r.Uses).FirstOrDefault();
        return usable ?? templates.First(t => t.Template == "still");
    }

    // How much of a game actor's behaviour wouldn't come along into `target` (notes ScriptRemap makes): fewer is a better fit.
    private static int Fit(SceneStore store, SceneModel target, ActorBehaviour b)
    {
        if (b.SourceScene is null) return 0;
        try
        {
            var scratch = target.Clone();
            var actor = SceneOps.BlankActor(target.Game, scratch.Hero.X, scratch.Hero.Y, scratch.Hero.Z, b.SourceScene.Actors[b.SourceActor].Entity);
            scratch.Actors.Add(actor);
            return ScriptRemap.Carry(b.SourceScene, b.SourceActor, scratch, scratch.Actors.Count - 1).Notes.Count;
        }
        catch (Exception e) when (e is SceneEditException or InvalidDataException or InvalidOperationException) { return int.MaxValue; }
    }

    // ---- applying one
    // Gives target.Actors[index] the behaviour (its scripts and the settings they work with), adding what they need to the scene (track
    // points, zones). Returns notes on what changed on the way. `text`: the talking template's line.
    public static List<string> Apply(SceneStore store, SceneModel target, int index, ActorBehaviour behaviour, int? text = null)
    {
        if (index <= 0 || index >= target.Actors.Count) throw new SceneEditException("A behaviour goes on an actor of the scene (not Twinsen).");
        var actor = target.Actors[index];
        var notes = new List<string>();
        if (behaviour.Id == NoScriptId)
        {
            actor.Life = new byte[] { 0 }; actor.Track = new byte[] { 0 }; actor.Move = 0;
            return notes;
        }
        if (behaviour.SourceScene is { } source)
        {
            var from = source.Actors[behaviour.SourceActor];
            var carried = ScriptRemap.Carry(source, behaviour.SourceActor, target, index);
            actor.Life = carried.Life; actor.Track = carried.Track;
            notes.AddRange(carried.Notes);
            // (its settings: how it moves and turns, how tough it is, what it collides with -- its kind, body, place and looks stay)
            const uint behaviourFlags = 0x1 | 0x2 | 0x4 | 0x10 | 0x20 | 0x40 | 0x80 | 0x800 | 0x1000 | 0x4000 | 0x8000 | 0x20000;
            actor.Flags = (actor.Flags & ~behaviourFlags) | (from.Flags & behaviourFlags);
            actor.Move = from.Move; actor.SRot = from.SRot; actor.LifePoints = from.LifePoints; actor.Armor = from.Armor; actor.HitForce = from.HitForce;
            actor.NbBonus = from.NbBonus; actor.OptionFlags = from.OptionFlags;
            actor.Info = (int[])from.Info.Clone();
            if (from.Move == 2 && actor.Info.Length > 3)
            {
                // (following someone: Twinsen, itself or the Meca-penguin's slot stay; another actor of its scene isn't here)
                var followed = actor.Info[3];
                if (followed == behaviour.SourceActor) actor.Info[3] = index;
                else if (followed != 0 && !(followed == 1 && target.Game == SceneGame.Lba2))
                {
                    actor.Move = 0;
                    notes.Add($"it followed actor {followed} of its scene, which isn't in this one: it starts standing");
                }
            }
            return notes;
        }
        ApplyTemplate(store.Game, store.Directory, target, index, behaviour.Template ?? "still", text, notes);
        return notes;
    }

    private static void ApplyTemplate(SceneGame game, string directory, SceneModel target, int index, string template, int? text, List<string> notes)
    {
        var lba1 = game == SceneGame.Lba1;
        var actor = target.Actors[index];
        var anims = KindAnims.Of(game, directory, actor.Entity);
        const int stand = KindAnims.Stand;
        var walk = anims?.Has(KindAnims.Walk) == true ? KindAnims.Walk : stand;
        var pause = lba1 ? "wait_nb_second(1);" : "wait_nb_dizieme(5);";
        string life = "", track = "";
        var move = 0;
        switch (template)
        {
            case "still":
                break;
            case "wander":
                move = 3;           // MOVE_TRACK: the track runs from the start
                track = $"label(0);\nanim({walk});\nwait_nb_second(2);\nanim({stand});\nwait_nb_second(2);\nangle_rnd(1024);\ngoto(label_0);\n";
                break;
            case "patrol":
            {
                move = 3;
                static int Clamp(int v) => Math.Clamp(v, 512, 32255);
                var first = SceneOps.AddTrackPoint(target, new SceneTrackPoint(Clamp(actor.X - 1536), actor.Y, actor.Z));
                var second = SceneOps.AddTrackPoint(target, new SceneTrackPoint(Clamp(actor.X + 1536), actor.Y, actor.Z));
                notes.Add($"its route is track points {first} and {second}, beside it: move them where it should walk");
                track = $"label(0);\nanim({walk});\ngoto_point({first});\nanim({stand});\nwait_nb_second(2);\nanim({walk});\ngoto_point({second});\nanim({stand});\nwait_nb_second(2);\ngoto(label_0);\n";
                break;
            }
            case "talk":
                if (text is not { } id) throw new SceneEditException("Choose the text it says.");
                life = $"void comportement_0()\n{{\n    if (1 == action() && 1500 > distance(0))\n    {{\n        message({id});\n    }}\n}}\n";
                break;
            case "attack":
            {
                var attack = anims?.Attack;
                actor.Flags |= 0x1;                 // CHECK_OBJ_COL: its blows land on what it touches
                track = $"label(1);\nanim({walk});\nstop();\n\nlabel(2);\n" +
                        (attack is { } a ? $"anim({a});\nwait_anim();\nanim({stand});\n{pause}\ngoto(label_2);\n" : $"anim({stand});\nstop();\n");
                life = "void comportement_0()\n{\n    if (0 == hit_by() || 6000 > cone_view(0))\n    {\n        set_dir(MOVE_FOLLOW, 0);\n        set_track(label_1);\n        set_comportement(comportement_1);\n    }\n}\n\n" +
                       // (it walks up close -- an attack swung from further off misses -- and goes after him again once he is out of reach)
                       "void comportement_1()\n{\n    if (700 > distance(0))\n    {\n        set_track(label_2);\n        set_comportement(comportement_2);\n    }\n}\n\n" +
                       "void comportement_2()\n{\n    if (1000 < distance(0))\n    {\n        set_track(label_1);\n        set_comportement(comportement_1);\n    }\n}\n";
                if (attack is null) notes.Add("this kind has no attacking animation: it goes for Twinsen but can't hit him");
                if (anims?.Has(KindAnims.Walk) != true) notes.Add("this kind has no walking animation: it turns to face Twinsen but doesn't come after him");
                if (actor.LifePoints <= 0) actor.LifePoints = 50;
                break;
            }
            case "flee":
                track = $"label(0);\nanim({stand});\nstop();\n\nlabel(1);\nanim({KindAnims.Back});\nstop();\n";
                life = "void comportement_0()\n{\n    if (2000 > distance(0))\n    {\n        set_dir(MOVE_FOLLOW, 0);\n        set_track(label_1);\n        set_comportement(comportement_1);\n    }\n}\n\n" +
                       "void comportement_1()\n{\n    if (3500 < distance(0))\n    {\n        set_dir(NO_MOVE);\n        set_track(label_0);\n        set_comportement(comportement_0);\n    }\n}\n";
                break;
            default:
                throw new SceneEditException($"There is no behaviour \"{template}\".");
        }
        var (lifeBytes, trackBytes) = Compile(target, index, life, track);
        actor.Life = lifeBytes; actor.Track = trackBytes; actor.Move = move;
        actor.Anim = anims?.Has(stand) == true ? stand : actor.Anim;
    }

    // C text -> the actor's bytes, through the scene's own compiler (its set_track(label_n) names resolved against its new track script).
    private static (byte[] Life, byte[] Track) Compile(SceneModel scene, int index, string life, string track)
    {
        var scratch = scene.Clone();
        scratch.Actors[index].Life = new byte[] { 0 };
        scratch.Actors[index].Track = new byte[] { 0 };
        var scripts = SceneScripts.Load(SceneSerializer.Write(scratch), -1, null, scene.Game == SceneGame.Lba1);
        if (track.Length > 0) scripts.SetText(index, ScriptKind.Track, track);
        if (life.Length > 0) scripts.SetText(index, ScriptKind.Life, life);
        if (!scripts.HasEdits) return (new byte[] { 0 }, new byte[] { 0 });
        var built = scripts.Build();
        if (!built.Ok) throw new SceneEditException("The behaviour's scripts didn't compile: " + string.Join("; ", built.Errors));
        var model = SceneSerializer.Parse(scene.Game, built.Record!);
        return (model.Actors[index].Life, model.Actors[index].Track);
    }

    // ---- what a script does, in a line
    internal sealed record Analysis(string Text, bool Attacks, bool Talks);

    public static Analysis Analyse(SceneGame game, SceneActorModel actor, KindAnims? anims)
    {
        using var scope = Opcodes.Use(game == SceneGame.Lba1 ? Opcodes.Lba1 : Opcodes.Lba2);
        var life = actor.Life.Length > 0 ? Bytecode.DecodeLife(actor.Life, out _) : new List<Instr>();
        var track = actor.Track.Length > 0 ? Bytecode.DecodeTrack(actor.Track, out _) : new List<Instr>();
        bool follows = actor.Move == 2 && actor.Info.Length > 3 && actor.Info[3] == 0, sees = false, hit = false, near = false, action = false, zone = false, story = false,
             wanders = false, door = false, hitsHero = actor.Move == 5, gives = false, dies = false, changesStory = false;
        var messages = new HashSet<int>();
        var animsUsed = new HashSet<int>();
        var points = new HashSet<long>();
        foreach (var ins in life)
        {
            var def = Opcodes.Life(ins.Op);
            if (def is null) continue;
            // (set_dir(MOVE_FOLLOW, 0): it turns to Twinsen wherever he goes)
            if (def.Name == "SET_DIR" && ins.A.Length > def.Args.Length && ins.A[def.Args.Length - 1] == 2 && ins.A[def.Args.Length] == 0) follows = true;
            if (def.Form is LifeForm.Cond or LifeForm.Switch && Opcodes.Cond(ins.Func) is { } c)
            {
                if (c.Name == "CONE_VIEW" && ins.FuncArg == 0) sees = true;
                if (c.Name == "HIT_BY") hit = true;
                if (c.Name is "DISTANCE" or "DISTANCE_3D" && ins.FuncArg == 0) near = true;
                if (c.Name == "ACTION") action = true;
                if (c.Name == "ZONE_OBJ" && ins.FuncArg == 0) zone = true;
                if (c.Name is "VAR_GAME" or "CHAPTER" or "FLAG_GAME") story = true;
                if (c.Name == "RND") wanders = true;
            }
            switch (def.Name)
            {
                case "MESSAGE" or "ADD_MESSAGE" or "ASK_CHOICE" or "BIG_MESSAGE" or "SAY_MESSAGE" or "TEXT" when ins.A.Length > 0: messages.Add((int)ins.A[0]); break;
                case "MESSAGE_OBJ" or "ASK_CHOICE_OBJ" or "ADD_MESSAGE_OBJ" or "SAY_MESSAGE_OBJ" when ins.A.Length > 1: messages.Add((int)ins.A[1]); break;
                case "ANIM" when ins.A.Length > 0: animsUsed.Add((int)ins.A[0]); break;
                case "HIT_OBJ" or "SUB_LIFE_POINT_OBJ" when ins.A.Length > 0 && ins.A[0] == 0: hitsHero = true; break;
                case "GIVE_BONUS": gives = true; break;
                case "SUICIDE": dies = true; break;
                case "SET_VAR_GAME" or "INC_CHAPTER" or "FOUND_OBJECT" or "CHANGE_CUBE" or "PLAY_ACF" or "PLAY_FLA" or "SET_FLAG_GAME": changesStory = true; break;
            }
        }
        foreach (var ins in track)
        {
            var def = Opcodes.Track(ins.Op);
            if (def is null) continue;
            switch (def.Name)
            {
                case "ANIM" when ins.A.Length > 0: animsUsed.Add((int)ins.A[0]); break;
                case "GOTO_POINT" or "GOTO_POINT_3D" or "GOTO_SYM_POINT" when ins.A.Length > 0: points.Add(ins.A[0]); break;
                case "ANGLE_RND" or "WAIT_NB_SECOND_RND" or "WAIT_NB_DIZIEME_RND": wanders = true; break;
                case "OPEN_LEFT" or "OPEN_RIGHT" or "OPEN_UP" or "OPEN_DOWN" or "CLOSE": door = true; break;
                case "PLAY_ACF" or "PLAY_FLA": changesStory = true; break;
            }
        }
        var attackAnims = animsUsed.Where(a => anims?.Hits(a) == true || anims?.ThrowsSomething(a) == true).ToList();
        var attacks = hitsHero || (attackAnims.Count > 0 && (follows || near || sees || hit));
        var talks = messages.Count > 0;
        var parts = new List<string>();
        var when = sees ? " when it sees him" : hit ? " when he hits it" : near ? " when he comes near" : "";
        if (attacks) parts.Add(follows ? $"goes for Twinsen{when} and attacks him" : $"attacks Twinsen{when}");
        else if (follows) parts.Add($"follows Twinsen{when}");
        if (talks) parts.Add($"talks ({messages.Count} line{(messages.Count > 1 ? "s" : "")}){(action ? " when spoken to" : zone ? " when Twinsen comes near" : "")}");
        if (points.Count > 0) parts.Add($"walks a route of {points.Count} point{(points.Count > 1 ? "s" : "")}");
        else if (wanders && !attacks) parts.Add("wanders about");
        if (door) parts.Add("opens and closes like a door");
        if (gives) parts.Add("drops a bonus");
        if (dies && !attacks) parts.Add("goes away");
        if (parts.Count == 0) parts.Add(animsUsed.Count > 0 ? $"plays its animation{(animsUsed.Count > 1 ? "s" : "")} {string.Join(", ", animsUsed.Order())}" : "stands");
        if (story) parts.Add("depending on the story so far");
        if (changesStory) parts.Add("(its story changes are left out here)");
        var text = string.Join(", ", parts);
        return new Analysis(text.Length > 0 ? char.ToUpperInvariant(text[0]) + text[1..] : text, attacks, talks);
    }

    // ---- the texts the talking template can say: the scene's island's dialogue file, in English
    public static IReadOnlyList<(int Id, string Text)> Texts(string directory, SceneGame game, int island)
    {
        var path = Path.Combine(directory, "TEXT.HQR");
        if (!File.Exists(path)) return Array.Empty<(int, string)>();
        try
        {
            var archive = HqrArchive.Open(path);
            if (game == SceneGame.Lba1)
            {
                var bank = Lba1.Runtime.Lba1TextBank.Load(archive, 0, 3 + island);
                return bank is null ? Array.Empty<(int, string)>() : bank.Ids.Select(id => (id, OneLine(bank.Get(id) ?? ""))).ToList();
            }
            var lba2 = Lba2TextBank.Load(archive, Lba2TextBank.English(archive), 3 + island);
            return lba2.Texts.Select(t => (t.Id, OneLine(Dos.GetString(t.Bytes)))).ToList();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or IndexOutOfRangeException)
        {
            DebugLog.Log($"ActorBehaviours: texts of island {island}: {e.Message}");
            return Array.Empty<(int, string)>();
        }
    }

    private static string OneLine(string text)
    {
        var one = text.Replace("@P", " ").Replace('@', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();
        return one.Length > 110 ? one[..110] + "…" : one;
    }

    private static Encoding? dos;
    private static Encoding Dos
    {
        get
        {
            if (dos is not null) return dos;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return dos = Encoding.GetEncoding(850);
        }
    }
}
