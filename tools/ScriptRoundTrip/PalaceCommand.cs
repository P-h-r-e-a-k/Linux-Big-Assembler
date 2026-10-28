using LBAAssembler.Terrain.Palace;

namespace ScriptRoundTrip;

// palace <game folder> <out folder> [source folder] [--no-roofs] [--outside] [--no-furniture] [--no-upstairs]: the Palace island
// (Terrain/Palace/PalaceIsland) built from the game folder's palace scenes and Otringal's island files (the source folder's, the game folder's
// by default) into the out folder: PALACE.ILE and PALACE.OBL. (--no-roofs leaves the roofs off, and the outside with them: a look inside;
// --outside puts Otringal's palace on the roof -- the race's dropped outside, 2026-10-15; --no-furniture the rooms' furniture out;
// --no-upstairs the palace one storey, as the game's)
internal static class PalaceCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 3) { Console.WriteLine("palace <game folder> <out folder> [source folder] [--no-roofs] [--outside] [--no-furniture] [--no-upstairs]"); return 1; }
        var rest = args.Skip(3).Where(a => !a.StartsWith("--")).ToArray();
        var built = PalaceIsland.Build(args[1], args[2], rest.Length > 0 ? rest[0] : null, roofed: !args.Contains("--no-roofs"),
            outside: args.Contains("--outside"), furnished: !args.Contains("--no-furniture"), upstairs: !args.Contains("--no-upstairs"));
        Console.WriteLine(built.Log);
        return 0;
    }
}
