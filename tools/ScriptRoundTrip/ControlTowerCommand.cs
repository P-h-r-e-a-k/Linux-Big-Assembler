using LBAAssembler.Terrain.ControlTower;

namespace ScriptRoundTrip;

// cxtower <game folder> <out folder> [source folder] [--no-roofs] [--closed-grates]: Island CX with its control tower's lower level as a 3D
// building (Terrain/ControlTower/ControlTowerIsland) built from the game folder's scenes 180 and 181 and Island CX's files (the source folder's,
// pristine ILOTCX.ILE/.OBL; the game folder's by default), CXTOWER.ILE and CXTOWER.OBL written to the out folder.
internal static class ControlTowerCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 3) { Console.WriteLine("cxtower <game folder> <out folder> [source folder] [--no-roofs] [--closed-grates]"); return 1; }
        var rest = args.Skip(3).Where(a => !a.StartsWith("--")).ToArray();
        var built = ControlTowerIsland.Build(args[1], args[2], rest.Length > 0 ? rest[0] : null, roofed: !args.Contains("--no-roofs"), openGrates: !args.Contains("--closed-grates"));
        Console.WriteLine(built.Log);
        return 0;
    }
}
