using LBAAssembler.LbaScript;

namespace LBAAssembler.Scenes;

// Where a script refers to an actor or a track point: (actor whose script it is, life or track, byte offset, what).
internal sealed record SceneReference(int Actor, ScriptKind Kind, int Offset, ArgRole Role, int Target)
{
    public override string ToString() => $"{(Actor == 0 ? "hero" : $"actor {Actor}")} {Kind.ToString().ToLowerInvariant()} script, byte {Offset}: {(Role == ArgRole.Obj ? "actor" : "track point")} {Target}";
}

// Edits that change the numbering of things scripts refer to. Actors, and the track points routes use, are referred to by
// number, so deleting one has to renumber every reference to those after it in every actor's scripts; this walks the
// decoded scripts (the same operand roles the translator uses) and patches the operand bytes, leaving everything else
// exactly as it was.
internal static class SceneOps
{
    // Every reference in the scene's scripts to actor `actor` (or to track point `point` when role is Point).
    public static List<SceneReference> ReferencesTo(SceneModel scene, ArgRole role, int target)
    {
        var found = new List<SceneReference>();
        Rewrite(scene, (r, value, _) => value, (reference) => { if (reference.Role == role && reference.Target == target) found.Add(reference); });
        return found;
    }

    // Deletes actor `index` (not the hero). Scripts that refer to it are re-pointed to `retarget` (an actor, by its number
    // before the deletion) or, when that is null, the deletion is refused with the list of references.
    public static void DeleteActor(SceneModel scene, int index, int? retarget = null)
    {
        if (index <= 0 || index >= scene.Actors.Count) throw new SceneEditException("There is no such actor to delete (the hero can't be deleted).");
        var refs = ReferencesTo(scene, ArgRole.Obj, index).Where(r => r.Actor != index).ToList();
        if (refs.Count > 0 && retarget is null)
            throw new SceneEditException($"Other scripts refer to actor {index}:\n  " + string.Join("\n  ", refs.Take(8)) + (refs.Count > 8 ? $"\n  ... and {refs.Count - 8} more" : ""));

        var replacement = retarget ?? 0;
        if (replacement == index) throw new SceneEditException("References can't be re-pointed to the actor being deleted.");
        if (replacement > index) replacement--;

        var oldCount = scene.Actors.Count;
        scene.Actors.RemoveAt(index);
        Rewrite(scene, (role, value, _) =>
        {
            // numbers that name no actor (scripts compare with such values) are not references, so they are left alone
            if (role != ArgRole.Obj || value >= oldCount) return value;
            if (value == index) return replacement;
            return value > index ? value - 1 : value;
        }, null);
        // actors that follow another (move type follow) keep the target in Info[3]
        foreach (var a in scene.Actors.Skip(1))
            if (a.Move == 2 && a.Info.Length > 3) a.Info[3] = a.Info[3] == index ? replacement : a.Info[3] > index ? a.Info[3] - 1 : a.Info[3];
    }

    public static int AddActor(SceneModel scene, SceneActorModel actor)
    {
        if (scene.Actors.Count >= SceneValidator.MaxObjects) throw new SceneEditException($"The scene already has {SceneValidator.MaxObjects} actors, the most the engine allows.");
        scene.Actors.Add(actor);
        return scene.Actors.Count - 1;
    }

    // Inserts `actor` as actor `index` (after the hero), the actors from there on one number up: the scripts' references to them are
    // renumbered as DeleteActor does.
    public static void InsertActor(SceneModel scene, int index, SceneActorModel actor)
    {
        if (index <= 0 || index > scene.Actors.Count) throw new SceneEditException($"An actor goes after the hero and at most after the last actor ({scene.Actors.Count}).");
        if (scene.Actors.Count >= SceneValidator.MaxObjects) throw new SceneEditException($"The scene already has {SceneValidator.MaxObjects} actors, the most the engine allows.");
        var oldCount = scene.Actors.Count;
        Rewrite(scene, (role, value, _) => role != ArgRole.Obj || value < index || value >= oldCount ? value : value + 1, null);
        foreach (var a in scene.Actors.Skip(1))
            if (a.Move == 2 && a.Info.Length > 3 && a.Info[3] >= index && a.Info[3] < oldCount) a.Info[3]++;
        scene.Actors.Insert(index, actor);
    }

    // ---- LBA2's actor 1, the Meca-penguin's
    // An LBA2 scene's actor 1 is the Meca-penguin's (COMMON.H NUM_PINGOUIN): at every scene start the engine makes whatever is there dead and
    // bodiless (StartInitAllObjs -> RestartMecaPingouin), and Twinsen's penguin takes the slot when he uses one. Every retail scene has this
    // placeholder there -- entity 14, the penguin's, at 0, 0, 0 -- and its own actors from 2 on. (A new map's interior had the hero alone, so
    // the first actor put in it was actor 1, which the game never showed: a tester's Zoé, 2026-10-17.)
    public static SceneActorModel MecaPenguinSlot() => new()
    {
        Flags = 0x8847, Entity = 14, Body = 0, Anim = 0, Sprite = 0, X = 0, Y = 0, Z = 0, HitForce = 0, OptionFlags = 96, Beta = 0, SRot = 30, Move = 7,
        Info = new[] { -1, -1, -1, -1 }, NbBonus = 5, CoulObj = 0, Armor = 0, LifePoints = -1, Track = new byte[] { 0 }, Life = new byte[] { 0 },
    };

    public static bool IsMecaPenguinSlot(SceneActorModel actor) => actor.Entity == 14 && actor.X == 0 && actor.Y == 0 && actor.Z == 0;

    // An LBA2 scene whose actor 1 is an actor of its own, which the game hides.
    public static bool MecaPenguinSlotTaken(SceneModel scene) => scene.Game == SceneGame.Lba2 && scene.Actors.Count > 1 && !IsMecaPenguinSlot(scene.Actors[1]);

    // Gives an LBA2 scene the penguin's slot as actor 1 when it hasn't it: after the hero of a scene with the hero alone, else in front of the
    // actor there (the actors from 1 on one number up, InsertActor). Returns whether it did.
    public static bool EnsureMecaPenguinSlot(SceneModel scene)
    {
        if (scene.Game != SceneGame.Lba2 || scene.Actors.Count == 0 || scene.Actors.Count > 1 && IsMecaPenguinSlot(scene.Actors[1])) return false;
        InsertActor(scene, 1, MecaPenguinSlot());
        return true;
    }

    // A blank 3D actor: a still figure of the given entity, its usual body, whose scripts do nothing. Each script is a lone END (LM_END /
    // TM_END, 0 in both games), not empty: the loaders (DISKFUNC) point the script at the record where its bytes start, so an empty one
    // runs whatever follows it -- the next actor's data, the zones -- as script (it took new actors' bodies away in the 3D view).
    public static SceneActorModel BlankActor(SceneGame game, int x, int y, int z, int entity = 0)
    {
        return new SceneActorModel
        {
            Flags = game == SceneGame.Lba1 ? 0x0806u : 0x0806u,   // CHECK_BRICK_COL | CHECK_ZONE | OBJ_FALLABLE
            Entity = entity, Body = 0, Anim = 0, Sprite = 0,
            X = x, Y = y, Z = z, Beta = 0, SRot = 40, Move = 0,
            CoulObj = 4, Armor = 1, LifePoints = 50,
            Life = new byte[] { 0 }, Track = new byte[] { 0 },
        };
    }

    public static int AddZone(SceneModel scene, SceneZoneModel zone)
    {
        if (scene.Zones.Count >= SceneValidator.MaxZones) throw new SceneEditException($"The scene already has {SceneValidator.MaxZones} zones, the most the engine allows.");
        scene.Zones.Add(zone);
        return scene.Zones.Count - 1;
    }

    public static void DeleteZone(SceneModel scene, int index)
    {
        if (index < 0 || index >= scene.Zones.Count) throw new SceneEditException("There is no such zone.");
        scene.Zones.RemoveAt(index);
    }

    public static int AddTrackPoint(SceneModel scene, SceneTrackPoint point)
    {
        if (scene.TrackPoints.Count >= SceneValidator.MaxTrackPoints) throw new SceneEditException($"The scene already has {SceneValidator.MaxTrackPoints} track points, the most the engine allows.");
        scene.TrackPoints.Add(point);
        return scene.TrackPoints.Count - 1;
    }

    // Deletes track point `index`; scripts that use it are re-pointed to `retarget` or the deletion is refused.
    public static void DeleteTrackPoint(SceneModel scene, int index, int? retarget = null)
    {
        if (index < 0 || index >= scene.TrackPoints.Count) throw new SceneEditException("There is no such track point.");
        var refs = ReferencesTo(scene, ArgRole.Point, index);
        if (refs.Count > 0 && retarget is null)
            throw new SceneEditException($"Scripts use track point {index}:\n  " + string.Join("\n  ", refs.Take(8)) + (refs.Count > 8 ? $"\n  ... and {refs.Count - 8} more" : ""));
        var replacement = retarget ?? 0;
        if (replacement == index) throw new SceneEditException("References can't be re-pointed to the point being deleted.");
        if (replacement > index) replacement--;
        var oldCount = scene.TrackPoints.Count;
        scene.TrackPoints.RemoveAt(index);
        Rewrite(scene, (role, value, _) =>
        {
            if (role != ArgRole.Point || value >= oldCount) return value;
            if (value == index) return replacement;
            return value > index ? value - 1 : value;
        }, null);
    }

    // ---- the walker ----

    private const int MaxActorNumber = SceneValidator.MaxObjects;   // larger values (255) mean "nobody"

    // Decodes every script of the scene, asks `map` for the new value of each actor / track point operand (role, value,
    // the referencing actor), and re-encodes the scripts that changed. `visit` sees every operand.
    private static void Rewrite(SceneModel scene, Func<ArgRole, int, int, int> map, Action<SceneReference>? visit)
    {
        var lba1 = scene.Game == SceneGame.Lba1;
        using var scope = Opcodes.Use(lba1 ? Opcodes.Lba1 : Opcodes.Lba2);
        for (var n = 0; n < scene.Actors.Count; n++)
        {
            var actor = scene.Actors[n];
            foreach (var kind in new[] { ScriptKind.Life, ScriptKind.Track })
            {
                var code = kind == ScriptKind.Life ? actor.Life : actor.Track;
                if (code.Length == 0) continue;
                var instructions = kind == ScriptKind.Life ? Bytecode.DecodeLife(code, out var failure) : Bytecode.DecodeTrack(code, out failure);
                if (failure is not null) throw new SceneEditException($"Actor {n}'s {kind.ToString().ToLowerInvariant()} script can't be read ({failure.Message}), so its references can't be renumbered.");

                var changed = false;
                void Operand(Instr ins, ArgRole role, int value, Action<int> set)
                {
                    visit?.Invoke(new SceneReference(n, kind, ins.Offset, role, value));
                    var replaced = map(role, value, n);
                    if (replaced == value) return;
                    set(replaced);
                    changed = true;
                }

                foreach (var ins in instructions)
                {
                    if (kind == ScriptKind.Life)
                    {
                        var def = Opcodes.Life(ins.Op);
                        if (def is null) continue;
                        for (var k = 0; k < def.Args.Length && k < ins.A.Length; k++)
                        {
                            var slot = k;
                            if (def.Args[k].Role is ArgRole.Obj or ArgRole.Point) Operand(ins, def.Args[k].Role, (int)ins.A[k], v => ins.A[slot] = v);
                        }
                        if (def.Form == LifeForm.Dir && ins.A.Length > def.Args.Length && ins.A[def.Args.Length - 1] == 2)
                            Operand(ins, ArgRole.Obj, (int)ins.A[def.Args.Length], v => ins.A[def.Args.Length] = v);
                        if (def.Form is LifeForm.Cond or LifeForm.Switch)
                        {
                            var cond = Opcodes.Cond(ins.Func);
                            if (ins.FuncArg >= 0 && cond?.OperandName == "obj")
                                Operand(ins, ArgRole.Obj, ins.FuncArg, v => ins.FuncArg = v);
                            // "which actor did I hit / am I carried by": the value compared with is an actor number
                            if (def.Form == LifeForm.Cond && cond?.Name is "COL" or "COL_OBJ" or "HIT_BY" or "CARRY_BY" or "HIT_OBJ_BY" or "CARRY_OBJ_BY" && ins.Value >= 0 && ins.Value < MaxActorNumber)
                                Operand(ins, ArgRole.Obj, ins.Value, v => ins.Value = v);
                        }
                    }
                    else
                    {
                        var def = Opcodes.Track(ins.Op);
                        if (def is null) continue;
                        for (var k = 0; k < def.Args.Length && k < ins.A.Length; k++)
                        {
                            var slot = k;
                            if (def.Args[k].Role is ArgRole.Obj or ArgRole.Point) Operand(ins, def.Args[k].Role, (int)ins.A[k], v => ins.A[slot] = v);
                        }
                    }
                }

                if (!changed) continue;
                var encoded = kind == ScriptKind.Life ? Bytecode.EncodeLife(instructions) : Bytecode.EncodeTrack(instructions);
                if (kind == ScriptKind.Life) actor.Life = encoded; else actor.Track = encoded;
            }
        }
    }
}
