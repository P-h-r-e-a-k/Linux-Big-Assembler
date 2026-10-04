using LBAAssembler.Scenes;

namespace ScriptRoundTrip;

// nuke <lba1|lba2> <game folder> <scene> [island .ILE] [--whole] [--scenes a,b,...] [--dry]: Build > Nuke without the window (SceneNuke):
// what goes, then (not with --dry) writes it as one undo step. --whole: every cube of the scene's island; --scenes: these scenes (a
// joined map's, LBA1 or LBA2 interiors), in the order they go off, after <scene>. For a copy of the game, never the real folder.
internal static class NukeCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 4) { Console.WriteLine("nuke <lba1|lba2> <game folder> <scene> [island .ILE] [--whole] [--scenes a,b,...] [--dry]"); return 1; }
        var lba1 = args[1].Equals("lba1", StringComparison.OrdinalIgnoreCase);
        var dir = args[2]; var scene = int.Parse(args[3]);
        var at = Array.IndexOf(args, "--scenes");
        var more = at > 0 && at + 1 < args.Length ? args[at + 1].Split(',').Select(int.Parse).ToList() : new List<int>();
        var island = args.Skip(4).Where((a, i) => !a.StartsWith("--") && (at < 0 || i + 4 != at + 1)).FirstOrDefault();
        var scenes = new[] { scene }.Concat(more.Where(s => s != scene)).ToList();
        var nuke = lba1 ? SceneNuke.ForLba1(dir, scenes, "joined map")
            : args.Contains("--whole") ? SceneNuke.ForLba2Island(dir, island ?? throw new ArgumentException("--whole needs the island file"), scene, wholeIsland: true)
            : scenes.Count > 1 ? SceneNuke.ForLba2Interiors(dir, scenes, "joined map")
            : SceneNuke.ForLba2(dir, scene, island);
        Console.WriteLine($"{nuke.Where}: goes: {nuke.Summary}; exits kept {nuke.Exits}; floored columns {nuke.Columns}; island objects {nuke.Decors}; level {nuke.Level}");
        foreach (var stage in nuke.Stages) Console.WriteLine($"  ring {stage.Ring}: scene{(stage.Scenes.Count > 1 ? "s" : "")} {string.Join(", ", stage.Scenes)}{(stage.CubeX >= 0 ? $" (cube {stage.CubeX},{stage.CubeY})" : "")}");
        foreach (var w in nuke.Warnings) Console.WriteLine("  warning: " + w);
        if (args.Contains("--dry")) return 0;
        nuke.Commit();
        Console.WriteLine($"written: {SceneHistory.UndoDescription}");
        return 0;
    }
}
