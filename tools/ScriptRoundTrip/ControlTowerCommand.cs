using LBAAssembler.Terrain;
using LBAAssembler.Terrain.ControlTower;

namespace ScriptRoundTrip;

// cxtower <game folder> <out folder> [source folder] [--roofs] [--closed-grates] [--race [plan.json]]: Island CX with its control tower's
// lower level as a 3D building under it (Terrain/ControlTower/ControlTowerIsland), built from the game folder's scenes 180 and 181 and Island
// CX's files (the source folder's, pristine ILOTCX.ILE/.OBL; the game folder's by default), CXTOWER.ILE and CXTOWER.OBL written to the out
// folder. --race: the race track's island (ControlTowerIsland.RaceRoom), its roof open where the plan's road comes down through it -- the
// plan given, or the one built into the program (CxTowerTrack.RoofHoles).
internal static class ControlTowerCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 3) { Console.WriteLine("cxtower <game folder> <out folder> [source folder] [--roofs] [--closed-grates] [--race [plan.json]]"); return 1; }
        var at = Array.IndexOf(args, "--race");
        var planPath = at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("--") ? args[at + 1] : null;
        var rest = args.Skip(3).Where((a, i) => !a.StartsWith("--") && a != planPath).ToArray();
        ControlTowerIsland.RaceRoom? race = null;
        if (at >= 0)
        {
            var plan = planPath is not null ? RaceTrackPlan.Load(planPath) : RaceTrackPlan.Built(RaceTrackIsland.CxTower);
            race = new ControlTowerIsland.RaceRoom(CxTowerTrack.RoofHoles(plan));
        }
        var built = ControlTowerIsland.Build(args[1], args[2], rest.Length > 0 ? rest[0] : null, roofed: args.Contains("--roofs"), openGrates: !args.Contains("--closed-grates"),
            race: race);
        Console.WriteLine(built.Log);
        return 0;
    }
}
