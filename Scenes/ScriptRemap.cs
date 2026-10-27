using LBAAssembler.LbaScript;

namespace LBAAssembler.Scenes;

// One actor's scripts carried from its scene into another (a behaviour applied from a game's actor, Add > Actor from another scene). The
// scripts name things of their own scene by number, and those mean something else -- or nothing -- in the new one, so each is remapped or
// made harmless, and every change is said in a note:
//   - actors: the actor itself becomes the new actor, Twinsen (0) stays Twinsen, LBA2's actor 1 (the Meca-penguin's slot, in every scene)
//     stays 1; any other actor isn't in the new scene: a statement about it goes, a condition on it is never true
//   - track points: the ones it walks to are copied into the new scene, moved as the actor is (its route keeps its shape round it)
//   - scenaric zones it waits for (zone() / zone_obj() == n): copied the same way, under a zone number free in the new scene; statements that
//     switch zones of its old scene (cameras, fragments, ladders, rails, cube changes) go
//   - texts: kept when the new scene's island has the same dialogue file (TEXT.HQR's file 3 + island), else its lines go
//   - cube variables: renumbered clear of the ones the new scene's other actors use
//   - the game's story: statements that change it (game flags, chapter, inventory, holomap, money, scene changes, films) go -- conditions that
//     read it stay
// Statements go by being cut out of the bytecode, every jump and every SET_COMPORTEMENT / SET_TRACK offset after them moved to match; a
// condition that can't hold any more compares its value with one it can never be below.
internal sealed class ScriptRemap
{
    public sealed record Result(byte[] Life, byte[] Track, IReadOnlyList<string> Notes);

    private readonly SceneModel source, target;
    private readonly int sourceActor, targetActor;
    private readonly bool lba1;
    private readonly bool sameTexts;
    private readonly int dx, dy, dz;
    private readonly List<string> notes = new();
    private readonly Dictionary<int, int?> points = new();
    private readonly Dictionary<int, int?> zones = new();
    private readonly Dictionary<int, int?> cubeVars = new();
    private readonly HashSet<int> usedCubeVars = new();

    // The game's story and inventory: statements that change them (and statements that run the game's own films or leave the scene)
    private static readonly HashSet<string> StoryOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "SET_VAR_GAME", "ADD_VAR_GAME", "SUB_VAR_GAME", "INC_CHAPTER", "FOUND_OBJECT", "SET_HOLO_POS", "CLR_HOLO_POS", "MEMO_ARDOISE",
        "INC_CLOVER_BOX", "SET_MAGIC_LEVEL", "SUB_MAGIC_POINT", "STATE_INVENTORY", "SET_USED_INVENTORY", "CHANGE_CUBE", "PLAY_ACF", "PLAY_FLA",
        "THE_END", "BRUTAL_EXIT", "TRACK_TO_VAR_GAME", "VAR_GAME_TO_TRACK", "INIT_BUGGY", "HOLOMAP_TRAJ", "INIT_PINGOUIN", "USE_ONE_LITTLE_KEY",
        "GIVE_GOLD_PIECES", "ADD_GOLD_PIECES", "ADD_FUEL", "SUB_FUEL", "SAVE_HERO", "RESTORE_HERO", "COMPORTEMENT_HERO", "MESSAGE_SENDELL",
        "SET_FLAG_GAME",
    };
    // Statements about zones (by number) of the old scene
    private static readonly HashSet<string> ZoneOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "SET_CAMERA", "SET_GRM", "GRM_OFF", "SET_CHANGE_CUBE", "ESCALATOR", "ECHELLE", "SET_RAIL", "SET_HIT_ZONE",
    };
    // Statements that say a text of the scene's island's dialogue file
    private static readonly HashSet<string> TextOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "MESSAGE", "MESSAGE_OBJ", "ADD_CHOICE", "ASK_CHOICE", "ASK_CHOICE_OBJ", "MESSAGE_ZOE", "ADD_MESSAGE", "ADD_MESSAGE_OBJ", "PCX_MESS_OBJ",
        "BIG_MESSAGE", "SAY_MESSAGE", "SAY_MESSAGE_OBJ", "TEXT",
    };
    // Conditions whose compared value is an actor's number ("which actor did I hit / carries me")
    private static readonly HashSet<string> ActorValueConds = new(StringComparer.OrdinalIgnoreCase) { "COL", "COL_OBJ", "HIT_BY", "CARRY_BY", "HIT_OBJ_BY", "CARRY_OBJ_BY" };

    private ScriptRemap(SceneModel source, int sourceActor, SceneModel target, int targetActor)
    {
        this.source = source; this.sourceActor = sourceActor; this.target = target; this.targetActor = targetActor;
        lba1 = target.Game == SceneGame.Lba1;
        sameTexts = source.Island == target.Island;
        var from = source.Actors[sourceActor]; var to = target.Actors[targetActor];
        dx = to.X - from.X; dy = to.Y - from.Y; dz = to.Z - from.Z;
        // (the cube variables the new scene's other actors use)
        using var scope = Opcodes.Use(lba1 ? Opcodes.Lba1 : Opcodes.Lba2);
        for (var n = 0; n < target.Actors.Count; n++)
            if (n != targetActor) usedCubeVars.UnionWith(CubeVarsOf(target.Actors[n]));
    }

    // Carries source.Actors[sourceActor]'s scripts into target.Actors[targetActor] (which stands where it stands): the track points and zones
    // they need are added to `target`, the scripts are returned -- the caller puts them on the actor.
    public static Result Carry(SceneModel source, int sourceActor, SceneModel target, int targetActor)
    {
        if (source.Game != target.Game) throw new SceneEditException("A script can only be carried between scenes of the same game.");
        var remap = new ScriptRemap(source, sourceActor, target, targetActor);
        using var scope = Opcodes.Use(remap.lba1 ? Opcodes.Lba1 : Opcodes.Lba2);
        var actor = source.Actors[sourceActor];
        var (track, trackMap) = remap.Track(actor.Track);
        var life = remap.Life(actor.Life, trackMap);
        return new Result(life, track, remap.notes.Distinct().ToList());
    }

    // ---- the mappings
    private int? Actor(int v)
    {
        if (v == sourceActor) return targetActor;
        if (v == 0) return 0;
        if (!lba1 && v == 1 && target.Actors.Count > 1 && SceneOps.IsMecaPenguinSlot(target.Actors[1])) return 1;
        return null;
    }

    // (a number that names no actor of its scene -- 255 "nobody" -- isn't a reference)
    private bool IsActorNumber(long v) => v >= 0 && v < source.Actors.Count;

    private int? Point(int v)
    {
        if (points.TryGetValue(v, out var known)) return known;
        int? mapped = null;
        if (v >= 0 && v < source.TrackPoints.Count && target.TrackPoints.Count < SceneValidator.MaxTrackPoints)
        {
            var p = source.TrackPoints[v];
            // (kept inside the scene's area: the route of an actor far from where it is placed here would run off it)
            int x = Math.Clamp(p.X + dx, 0, AreaMax), y = Math.Clamp(p.Y + dy, lba1 ? 0 : int.MinValue / 2, lba1 ? short.MaxValue : int.MaxValue / 2), z = Math.Clamp(p.Z + dz, 0, AreaMax);
            target.TrackPoints.Add(new SceneTrackPoint(x, y, z));
            mapped = target.TrackPoints.Count - 1;
            notes.Add($"track point {v} of its scene is track point {mapped} here (moved with the actor{(x != p.X + dx || z != p.Z + dz ? ", and into the scene" : "")})");
        }
        else notes.Add($"track point {v} of its scene couldn't be brought: what used it was left out");
        return points[v] = mapped;
    }

    private static int ZoneNum(SceneZoneModel z, bool lba1) => lba1 ? (z.Info.Length > 0 ? z.Info[0] : 0) : z.Num;

    // The scene's own area (64 x 64 cells of 512): what is copied in is kept inside it.
    private const int AreaMax = 32767;

    // Moves [a, b] inside [min, max] (shifted, its length kept when it fits, else clamped); whether it moved.
    private static bool Inside(ref int a, ref int b, int min, int max)
    {
        int lo = Math.Min(a, b), hi = Math.Max(a, b);
        var shift = lo < min ? min - lo : hi > max ? max - hi : 0;
        if (shift == 0) return false;
        lo = Math.Clamp(lo + shift, min, max); hi = Math.Clamp(hi + shift, min, max);
        if (a <= b) { a = lo; b = hi; } else { a = hi; b = lo; }
        return true;
    }

    private int? Zone(int num)
    {
        if (zones.TryGetValue(num, out var known)) return known;
        var found = source.Zones.Where(z => z.Type == 2 && ZoneNum(z, lba1) == num).ToList();
        int? mapped = null;
        if (found.Count > 0 && target.Zones.Count + found.Count <= SceneValidator.MaxZones)
        {
            var taken = target.Zones.Where(z => z.Type == 2).Select(z => ZoneNum(z, lba1)).ToHashSet();
            var free = !taken.Contains(num) ? num : Enumerable.Range(0, lba1 ? 256 : 128).Cast<int?>().FirstOrDefault(n => !taken.Contains(n!.Value));
            if (free is { } n)
            {
                var moved = false;
                foreach (var z in found)
                {
                    var copy = z.Clone();
                    copy.X0 += dx; copy.X1 += dx; copy.Y0 += dy; copy.Y1 += dy; copy.Z0 += dz; copy.Z1 += dz;
                    // (inside the scene's area, its size kept where it can be: a zone of its old scene far from the actor would land off it)
                    moved |= Inside(ref copy.X0, ref copy.X1, 0, AreaMax) | Inside(ref copy.Z0, ref copy.Z1, 0, AreaMax)
                             | (lba1 && Inside(ref copy.Y0, ref copy.Y1, 0, short.MaxValue));
                    if (lba1) copy.Info[0] = n; else copy.Num = n;
                    target.Zones.Add(copy);
                }
                mapped = n;
                notes.Add($"its scene's zone {num} (where it waits for Twinsen) is zone {n} here, placed round the actor as it was{(moved ? " (moved into the scene)" : "")}");
            }
        }
        if (mapped is null) notes.Add($"its scene's zone {num} couldn't be brought: what waited for it never happens");
        return zones[num] = mapped;
    }

    private int CubeVar(int v)
    {
        if (cubeVars.TryGetValue(v, out var known) && known is { } k) return k;
        var n = !usedCubeVars.Contains(v) ? v : Enumerable.Range(0, 80).FirstOrDefault(c => !usedCubeVars.Contains(c) && !cubeVars.Values.Contains(c), v);
        if (n != v) notes.Add($"its scene variable {v} is {n} here (another actor of this scene uses {v})");
        usedCubeVars.Add(n);
        cubeVars[v] = n;
        return n;
    }

    private static IEnumerable<int> CubeVarsOf(SceneActorModel actor)
    {
        if (actor.Life.Length == 0) yield break;
        var code = Bytecode.DecodeLife(actor.Life, out var failure);
        if (failure is not null) yield break;
        foreach (var ins in code)
        {
            var def = Opcodes.Life(ins.Op);
            if (def is null) continue;
            if (def.Name is "SET_VAR_CUBE" or "ADD_VAR_CUBE" or "SUB_VAR_CUBE" or "SET_FLAG_CUBE" && ins.A.Length > 0) yield return (int)ins.A[0];
            if (def.Form is LifeForm.Cond or LifeForm.Switch && Opcodes.Cond(ins.Func)?.Name is "VAR_CUBE" or "FLAG_CUBE" && ins.FuncArg >= 0) yield return ins.FuncArg;
        }
    }

    private void Note(string what) => notes.Add(what);

    // ---- the track script
    private (byte[] Bytes, Func<int, int> Map) Track(byte[] bytes)
    {
        if (bytes.Length == 0) return (bytes, o => o);
        var code = Bytecode.DecodeTrack(bytes, out var failure);
        if (failure is not null) throw new SceneEditException($"Its track script can't be read ({failure.Message}).");
        var removed = new HashSet<Instr>();
        foreach (var ins in code)
        {
            var def = Opcodes.Track(ins.Op);
            if (def is null) continue;
            if (def.Name is "PLAY_ACF" or "PLAY_FLA") { removed.Add(ins); Note($"its film ({ins.Str}) was left out"); continue; }
            for (var k = 0; k < def.Args.Length && k < ins.A.Length; k++)
                if (def.Args[k].Role == ArgRole.Point)
                {
                    if (Point((int)ins.A[k]) is { } p) ins.A[k] = p;
                    else removed.Add(ins);
                }
        }
        var (encoded, map) = Relocate(code, removed, ScriptKind.Track, bytes.Length, null);
        return (encoded, map);
    }

    // ---- the life script
    private byte[] Life(byte[] bytes, Func<int, int> trackMap)
    {
        if (bytes.Length == 0) return bytes;
        var code = Bytecode.DecodeLife(bytes, out var failure);
        if (failure is not null) throw new SceneEditException($"Its life script can't be read ({failure.Message}).");
        var removed = new HashSet<Instr>();
        foreach (var ins in code)
        {
            var def = Opcodes.Life(ins.Op);
            if (def is null) continue;
            switch (def.Form)
            {
                case LifeForm.Plain:
                case LifeForm.Dir:
                    if (StoryOps.Contains(def.Name)) { removed.Add(ins); Note("what it did to the game's story, inventory or scenes (game flags, items, holomap, films, scene changes) was left out"); continue; }
                    if (ZoneOps.Contains(def.Name)) { removed.Add(ins); Note("what it switched in its own scene's zones (cameras, fragments, ladders, rails, cube changes) was left out"); continue; }
                    if (TextOps.Contains(def.Name) && !sameTexts) { removed.Add(ins); Note($"its lines are island {source.Island}'s texts, which this scene (island {target.Island}) doesn't show: left out"); continue; }
                    if (!PlainOperands(ins, def)) removed.Add(ins);
                    break;
                case LifeForm.Cond:
                case LifeForm.Switch:
                    Condition(ins, def.Form == LifeForm.Switch);
                    break;
            }
        }
        // (offsets into its own scripts follow them; offsets into another actor's -- set_track_obj(other, ...) -- went above)
        var lifeOffsets = new HashSet<Instr>(code.Where(i => !removed.Contains(i) && IsOwnLifeOffset(i)));
        var trackOffsets = new HashSet<Instr>(code.Where(i => !removed.Contains(i) && IsOwnTrackOffset(i)));
        var (encoded, _) = Relocate(code, removed, ScriptKind.Life, bytes.Length, (lifeOffsets, trackOffsets, trackMap));
        return encoded;
    }

    // An action's operands mapped; false when it can't stay (it names an actor, a point, or another actor's script that isn't here).
    private bool PlainOperands(Instr ins, LifeOpDef def)
    {
        for (var k = 0; k < def.Args.Length && k < ins.A.Length; k++)
        {
            switch (def.Args[k].Role)
            {
                case ArgRole.Obj when IsActorNumber(ins.A[k]):
                    if (Actor((int)ins.A[k]) is not { } a) { Note($"what it did to actor {ins.A[k]} of its scene (not in this one) was left out"); return false; }
                    // (an offset into another actor's script: that script isn't this scene's)
                    if (a != targetActor && def.Args.Any(x => x.Role is ArgRole.LifeOffset or ArgRole.TrackOffset)) { Note("what it set in another actor's scripts was left out"); return false; }
                    if (a != targetActor && def.Name is "SET_LIFE_OBJ") { Note("what it set in another actor's scripts was left out"); return false; }
                    ins.A[k] = a;
                    break;
                case ArgRole.Point:
                    if (Point((int)ins.A[k]) is not { } p) return false;
                    ins.A[k] = p;
                    break;
            }
        }
        if (def.Name is "SET_VAR_CUBE" or "ADD_VAR_CUBE" or "SUB_VAR_CUBE" or "SET_FLAG_CUBE" && ins.A.Length > 0) ins.A[0] = CubeVar((int)ins.A[0]);
        // SET_DIR / SET_DIR_OBJ: a follow move's actor, a circle move's point
        if (def.Form == LifeForm.Dir && ins.A.Length > def.Args.Length)
        {
            var move = ins.A[def.Args.Length - 1];
            var param = (int)ins.A[def.Args.Length];
            var circle = !lba1 && move is 9 or 10;
            if (circle)
            {
                if (Point(param) is not { } p) return false;
                ins.A[def.Args.Length] = p;
            }
            else if (IsActorNumber(param))
            {
                if (Actor(param) is not { } a) { Note($"following actor {param} of its scene (not in this one) was left out"); return false; }
                ins.A[def.Args.Length] = a;
            }
        }
        return true;
    }

    private void Condition(Instr ins, bool isSwitch)
    {
        var cond = Opcodes.Cond(ins.Func);
        if (cond is null) return;
        var never = false;
        if (cond.OperandName == "obj" && ins.FuncArg >= 0 && IsActorNumber(ins.FuncArg))
        {
            if (Actor(ins.FuncArg) is { } a) ins.FuncArg = a;
            else { Note($"what it checked about actor {ins.FuncArg} of its scene (not in this one) never holds here"); ins.FuncArg = targetActor; never = true; }
        }
        if (cond.Name is "VAR_CUBE" or "FLAG_CUBE" && ins.FuncArg >= 0) ins.FuncArg = CubeVar(ins.FuncArg);
        if (!isSwitch)
        {
            if (ActorValueConds.Contains(cond.Name) && IsActorNumber(ins.Value))
            {
                if (Actor(ins.Value) is { } a) ins.Value = a;
                else { Note($"what it checked about actor {ins.Value} of its scene (not in this one) never holds here"); never = true; }
            }
            if (cond.Name is "ZONE" or "ZONE_OBJ" && ins.Value >= 0)
            {
                if (Zone(ins.Value) is { } z) ins.Value = z; else never = true;
            }
            if (cond.Name is "ECHELLE" or "RAIL") { Note("what it checked about its scene's ladders or rails never holds here"); never = true; }
            if (cond.Name == "CHOICE" && !sameTexts) never = true;
        }
        if (never && !isSwitch)
        {
            // (compared with the lowest value of its kind: nothing is below it)
            ins.Test = 2;
            ins.Value = cond.Value switch { ValueKind.S8 => sbyte.MinValue, ValueKind.U8 => 0, _ => short.MinValue };
        }
    }

    private bool IsOwnLifeOffset(Instr ins)
    {
        var def = Opcodes.Life(ins.Op);
        if (def is null || def.Form != LifeForm.Plain) return false;
        if (def.Name is "SET_LIFE") return true;
        if (def.Name is "SET_LIFE_OBJ") return ins.A.Length > 0 && ins.A[0] == targetActor;
        for (var k = 0; k < def.Args.Length; k++)
            if (def.Args[k].Role == ArgRole.LifeOffset)
                return !def.Args.Any(a => a.Role == ArgRole.Obj) || ins.A[0] == targetActor;
        return false;
    }

    private bool IsOwnTrackOffset(Instr ins)
    {
        var def = Opcodes.Life(ins.Op);
        if (def is null || def.Form != LifeForm.Plain) return false;
        for (var k = 0; k < def.Args.Length; k++)
            if (def.Args[k].Role == ArgRole.TrackOffset)
                return !def.Args.Any(a => a.Role == ArgRole.Obj) || ins.A[0] == targetActor;
        return false;
    }

    // The script without the removed instructions: jumps (and, in a life script, its own SET_COMPORTEMENT / SET_TRACK offsets) moved to where
    // their targets went -- a target that was removed becomes the instruction after it. Returns the bytes and the old -> new offset map.
    private static (byte[] Bytes, Func<int, int> Map) Relocate(List<Instr> code, HashSet<Instr> removed, ScriptKind kind, int oldLength,
        (HashSet<Instr> Life, HashSet<Instr> Track, Func<int, int> TrackMap)? offsets)
    {
        var oldOffset = code.ToDictionary(i => i, i => i.Offset);
        var kept = code.Where(i => !removed.Contains(i)).ToList();
        // the targets, read before anything moves
        var jumps = new Dictionary<Instr, int>();
        foreach (var ins in kept) if (Bytecode.TryGetTarget(kind, ins, out var t)) jumps[ins] = t;
        var lifeArgs = new Dictionary<Instr, (int Arg, int Value)>();
        var trackArgs = new Dictionary<Instr, (int Arg, int Value)>();
        if (offsets is { } o)
        {
            foreach (var ins in o.Life) if (OffsetArg(ins, ArgRole.LifeOffset, true) is { } a) lifeArgs[ins] = (a, (int)ins.A[a]);
            foreach (var ins in o.Track) if (OffsetArg(ins, ArgRole.TrackOffset, false) is { } a) trackArgs[ins] = (a, (int)ins.A[a]);
        }
        var bytes = kind == ScriptKind.Life ? Bytecode.EncodeLife(kept) : Bytecode.EncodeTrack(kept);
        // old offset -> new: an instruction's own new place, a removed one's the next kept instruction's, the end the new end
        var map = new SortedDictionary<int, int> { [oldLength] = bytes.Length };
        Instr? nextKept = null;
        for (var i = code.Count - 1; i >= 0; i--)
        {
            if (!removed.Contains(code[i])) nextKept = code[i];
            map[oldOffset[code[i]]] = nextKept?.Offset ?? bytes.Length;
        }
        int Map(int old) => map.TryGetValue(old, out var n) ? n : old;
        if (removed.Count == 0 && offsets is null) return (bytes, Map);
        foreach (var (ins, t) in jumps) Bytecode.SetTarget(kind, ins, Map(t));
        foreach (var (ins, (arg, value)) in lifeArgs) ins.A[arg] = Map(value);
        if (offsets is { } o2) foreach (var (ins, (arg, value)) in trackArgs) ins.A[arg] = o2.TrackMap(value);
        bytes = kind == ScriptKind.Life ? Bytecode.EncodeLife(kept) : Bytecode.EncodeTrack(kept);
        return (bytes, Map);
    }

    private static int? OffsetArg(Instr ins, ArgRole role, bool life)
    {
        var def = Opcodes.Life(ins.Op)!;
        if (life && def.Name is "SET_LIFE") return 0;
        if (life && def.Name is "SET_LIFE_OBJ") return 1;
        for (var k = 0; k < def.Args.Length && k < ins.A.Length; k++) if (def.Args[k].Role == role) return k;
        return null;
    }
}
