using LBAAssembler.LbaScript;

namespace LBAAssembler.Scenes;

internal enum SceneIssueSeverity { Error, Warning }

// One finding. Where names the part of the scene ("actor 5 life script", "zone 3", "header").
internal sealed record SceneIssue(SceneIssueSeverity Severity, string Where, string Message)
{
    public override string ToString() => $"{Severity.ToString().ToLowerInvariant()}: {Where}: {Message}";
}

internal sealed class SceneValidationOptions
{
    // How many scenes the game has (cube-change targets must be below it); unknown = not checked.
    public int? SceneCount;
    // LBA1 (0..119) or LBA2 (0..221) scene numbers that exist, when the caller knows more than a count.
    public Func<int, bool>? SceneExists;
}

// The engine's own limits and assumptions, taken from its source (COMMON.H, DISKFUNC.C / .CPP, GERELIFE, GRILLE) and
// checked against every retail scene: retail scenes produce no errors. Errors are things the engine would misread or
// overflow; warnings are things that are legal but almost certainly not what was meant.
internal static class SceneValidator
{
    public const int MaxObjects = 100;   // MAX_OBJETS
    public const int MaxZones = 255;     // MAX_ZONES
    public const int MaxTrackPoints = 255; // MAX_TRACKS

    public static List<SceneIssue> Validate(SceneModel scene, SceneValidationOptions? options = null)
    {
        options ??= new SceneValidationOptions();
        var issues = new List<SceneIssue>();
        void Error(string where, string message) => issues.Add(new(SceneIssueSeverity.Error, where, message));
        void Warn(string where, string message) => issues.Add(new(SceneIssueSeverity.Warning, where, message));

        var lba1 = scene.Game == SceneGame.Lba1;
        int maxScript = lba1 ? ushort.MaxValue : short.MaxValue;
        var coordMax = lba1 ? short.MaxValue : int.MaxValue;

        // ---- counts ----
        if (scene.Actors.Count < 1) { Error("scene", "the scene has no hero."); return issues; }
        if (SceneOps.MecaPenguinSlotTaken(scene))
            Warn("actor 1", "actor 1 is the slot the game keeps for the Meca-penguin, and it hides whatever is there: this actor never appears (adding an actor in Build > Actors and zones puts the slot back).");
        if (scene.Actors.Count > MaxObjects) Error("scene", $"{scene.Actors.Count} actors; the engine holds at most {MaxObjects} (the hero counts as one).");
        if (scene.Zones.Count > MaxZones) Error("scene", $"{scene.Zones.Count} zones; the engine's limit is {MaxZones}.");
        if (scene.TrackPoints.Count > MaxTrackPoints) Error("scene", $"{scene.TrackPoints.Count} track points; the engine's limit is {MaxTrackPoints}.");

        // ---- header ----
        if (!lba1)
        {
            if (scene.CubeMode is not (0 or 1)) Warn("header", $"cube mode {scene.CubeMode}: 0 is an interior, 1 an exterior.");
            if (scene.CubeMode == 1 && (scene.CubeX > 15 || scene.CubeY > 15)) Warn("header", $"island cube ({scene.CubeX}, {scene.CubeY}) lies outside the 16 x 16 cube grid.");
        }
        if (scene.AlphaLight is < 0 or > 4095 || scene.BetaLight is < 0 or > 4095) Warn("header", "light angles are 0..4095.");
        if (lba1 && options.SceneCount is { } gameOver && scene.GameOverScene >= gameOver) Warn("header", $"game over scene {scene.GameOverScene} doesn't exist.");

        // ---- actors ----
        using (Opcodes.Use(lba1 ? Opcodes.Lba1 : Opcodes.Lba2))
        {
            // (where each script's instructions start: a SET_COMPORTEMENT / SET_TRACK offset, its own or another actor's, has to be one)
            var starts = scene.Actors.Select(a => (Life: Starts(a.Life, ScriptKind.Life), Track: Starts(a.Track, ScriptKind.Track))).ToList();
            for (var n = 0; n < scene.Actors.Count; n++)
                CheckOffsets(scene, n, n == 0 ? "hero" : $"actor {n}", starts, Error, Warn);
            for (var n = 0; n < scene.Actors.Count; n++)
            {
                var a = scene.Actors[n];
                var name = n == 0 ? "hero" : $"actor {n}";
                if (Math.Abs((long)a.X) > coordMax || Math.Abs((long)a.Z) > coordMax) Error(name, "position out of range.");
                if (a.X < 0 || a.Z < 0 || a.X > 32767 || a.Z > 32767) Warn(name, $"position ({a.X}, {a.Y}, {a.Z}) lies outside the scene's 64 x 64 cell area.");
                if (n > 0)
                {
                    if (a.IsSprite && a.Entity != -1 && lba1) Warn(name, "a sprite actor normally has entity -1.");
                    if (!a.IsSprite && a.Entity < 0 && (a.Flags & 0x0200) == 0) Warn(name, "a 3D actor without an entity can't be drawn.");
                    if ((a.Flags & 0x0008) != 0)
                    {
                        if (!a.IsSprite) Warn(name, "SPRITE_CLIP (a door) is set on an actor that isn't a sprite.");
                        else if (lba1 && (a.Info[0] >= a.Info[2] || a.Info[1] >= a.Info[3])) Warn(name, "the door's screen clip rectangle (info 0..3) is empty or reversed.");
                    }
                }
                CheckScript(scene, n, name, "track", a.Track, ScriptKind.Track, maxScript, Error, Warn);
                CheckScript(scene, n, name, "life", a.Life, ScriptKind.Life, maxScript, Error, Warn);
            }
        }

        // ---- zones ----
        for (var i = 0; i < scene.Zones.Count; i++)
        {
            var z = scene.Zones[i];
            var name = $"zone {i}";
            if (z.Info.Length != (lba1 ? 4 : 8)) Error(name, $"{z.Info.Length} info values; this game's zones have {(lba1 ? 4 : 8)}.");
            if (z.X0 > z.X1 || z.Y0 > z.Y1 || z.Z0 > z.Z1) Warn(name, "the box is reversed (a min above a max), so nothing can stand in it.");
            if (z.Type == 0)
            {
                var target = lba1 ? (z.Info.Length > 0 ? z.Info[0] : -1) : z.Num;
                if (target < 0) Error(name, "a cube change to a negative scene number.");
                else if (options.SceneExists is { } exists && !exists(target)) Error(name, $"a cube change to scene {target}, which doesn't exist.");
                else if (options.SceneCount is { } count && target >= count) Error(name, $"a cube change to scene {target}; the game has {count} scenes.");
            }
        }
        return issues;
    }

    // The offsets an instruction of a script may start at (its end too); null when the script can't be read (CheckScript says so).
    private static HashSet<int>? Starts(byte[] code, ScriptKind kind)
    {
        if (code.Length == 0) return null;
        var list = kind == ScriptKind.Life ? Bytecode.DecodeLife(code, out var failure) : Bytecode.DecodeTrack(code, out failure);
        return failure is null ? new HashSet<int>(list.Select(i => i.Offset)) { code.Length } : null;
    }

    // A life script's SET_COMPORTEMENT / SET_TRACK (and _OBJ: into another actor's) offsets land on an instruction of the script they name --
    // the engine runs from wherever they point (a behaviour carried in from another scene, ScriptRemap, moves them when it cuts statements
    // out); a condition about an actor names one of the scene's.
    private static void CheckOffsets(SceneModel scene, int actorIndex, string actorName, List<(HashSet<int>? Life, HashSet<int>? Track)> starts,
        Action<string, string> error, Action<string, string> warn)
    {
        var code = scene.Actors[actorIndex].Life;
        if (code.Length == 0) return;
        var instructions = Bytecode.DecodeLife(code, out var failure);
        if (failure is not null) return;
        var where = $"{actorName} life script";
        foreach (var ins in instructions)
        {
            var def = Opcodes.Life(ins.Op);
            if (def is null) continue;
            if (def.Form is LifeForm.Cond or LifeForm.Switch && Opcodes.Cond(ins.Func) is { OperandName: "obj" } && ins.FuncArg >= scene.Actors.Count && ins.FuncArg < MaxObjects)
                warn(where, $"byte {ins.Offset}: a condition about actor {ins.FuncArg}, but the scene has {scene.Actors.Count} actors (0..{scene.Actors.Count - 1}).");
            if (def.Form != LifeForm.Plain) continue;
            var objArg = Array.FindIndex(def.Args, a => a.Role == ArgRole.Obj);
            var lifeArg = def.Name is "SET_LIFE" ? 0 : def.Name is "SET_LIFE_OBJ" ? 1 : Array.FindIndex(def.Args, a => a.Role == ArgRole.LifeOffset);
            var trackArg = Array.FindIndex(def.Args, a => a.Role == ArgRole.TrackOffset);
            if (lifeArg < 0 && trackArg < 0) continue;
            var target = objArg >= 0 && objArg < ins.A.Length ? (int)ins.A[objArg] : actorIndex;
            if (target < 0 || target >= scene.Actors.Count) continue;           // (CheckScript warns about the actor)
            var offsetArg = lifeArg >= 0 ? lifeArg : trackArg;
            if (offsetArg >= ins.A.Length) continue;
            var offset = (int)ins.A[offsetArg];
            var into = lifeArg >= 0 ? starts[target].Life : starts[target].Track;
            if (into is null || offset < 0) continue;                         // (-1: none; a script that can't be read is said elsewhere)
            // (a warning: an actor deleted with its references re-pointed at Twinsen, or a scene emptied but for its hero, leaves such offsets
            // in scripts that may never run them -- the save isn't refused for it)
            if (!into.Contains(offset))
                warn(where, $"byte {ins.Offset}: {def.Name} points at byte {offset} of {(target == actorIndex ? "its own" : $"actor {target}'s")} {(lifeArg >= 0 ? "life" : "track")} script, which isn't the start of an instruction: the engine would run it from there.");
        }
    }

    private static void CheckScript(SceneModel scene, int actorIndex, string actorName, string label, byte[] code, ScriptKind kind, int maxLength,
        Action<string, string> error, Action<string, string> warn)
    {
        var where = $"{actorName} {label} script";
        if (code.Length > maxLength) { error(where, $"{code.Length} bytes; the record stores a script length in {(maxLength == ushort.MaxValue ? 16 : 15)} bits."); return; }
        // (no retail script is empty: the loader points the script at where its bytes would start, so the engine runs whatever follows it)
        if (code.Length == 0) { warn(where, "is empty, so the game runs whatever follows it in the scene as this script; give it an end."); return; }

        List<Instr> instructions;
        ScriptFormatException? failure;
        instructions = kind == ScriptKind.Life ? Bytecode.DecodeLife(code, out failure) : Bytecode.DecodeTrack(code, out failure);
        if (failure is not null) { error(where, failure.Message); return; }

        var starts = new HashSet<int>(instructions.Select(i => i.Offset)) { code.Length };
        foreach (var ins in instructions)
        {
            // a track GOTO to -1 is legal (LBA1 uses it in retail scripts to mean "the start")
            if (Bytecode.TryGetTarget(kind, ins, out var target) && !starts.Contains(target) && !(kind == ScriptKind.Track && target == -1))
                error(where, $"the jump at byte {ins.Offset} lands on {target}, which isn't the start of an instruction.");

            var args = kind == ScriptKind.Life ? Opcodes.Life(ins.Op)?.Args : Opcodes.Track(ins.Op)?.Args;
            if (args is null) continue;
            for (var k = 0; k < args.Length && k < ins.A.Length; k++)
            {
                if (args[k].Role == ArgRole.Obj && (ins.A[k] < 0 || ins.A[k] >= scene.Actors.Count))
                    warn(where, $"byte {ins.Offset}: refers to actor {ins.A[k]}, but the scene has {scene.Actors.Count} actors (0..{scene.Actors.Count - 1}).");
                else if (args[k].Role == ArgRole.Point && (ins.A[k] < 0 || ins.A[k] >= scene.TrackPoints.Count))
                    warn(where, $"byte {ins.Offset}: refers to track point {ins.A[k]}, but the scene has {scene.TrackPoints.Count}.");
            }
        }
    }
}
