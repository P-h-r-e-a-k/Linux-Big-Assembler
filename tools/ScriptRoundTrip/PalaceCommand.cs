using LBAAssembler.Terrain.Palace;

namespace ScriptRoundTrip;

// palace <game folder> <out folder> [source folder] [--no-roofs]: the Palace island (Terrain/Palace/PalaceIsland) built from the game folder's palace scenes
// and Otringal's island files (the source folder's, the game folder's by default) into the out folder: PALACE.ILE and PALACE.OBL.
internal static class PalaceCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 3) { Console.WriteLine("palace <game folder> <out folder> [source folder]"); return 1; }
        var rest = args.Skip(3).Where(a => a != "--no-roofs").ToArray();
        var built = PalaceIsland.Build(args[1], args[2], rest.Length > 0 ? rest[0] : null, roofed: !args.Contains("--no-roofs"));
        Console.WriteLine(built.Log);
        return 0;
    }
}
