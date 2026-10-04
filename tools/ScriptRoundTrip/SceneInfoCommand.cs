using LBAAssembler;
using LBAAssembler.Scenes;

namespace ScriptRoundTrip;

// sceneinfo <game folder> <scene>...: each LBA2 scene's island, cube, mode and actors, and its zones (type, destination, box).
internal static class SceneInfoCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 3) { Console.WriteLine("sceneinfo <game folder> <scene>..."); return 1; }
        var store = new SceneStore(SceneGame.Lba2, args[1]);
        foreach (var number in args.Skip(2).Select(int.Parse))
        {
            var m = store.Load(number);
            Console.WriteLine($"scene {number}: island {m.Island}, cube ({m.CubeX},{m.CubeY}), mode {m.CubeMode}, {m.Actors.Count} actors, {m.Zones.Count} zones, {m.TrackPoints.Count} track points");
            foreach (var z in m.Zones)
                Console.WriteLine($"  zone type {z.Type} num {z.Num}: x {z.X0}..{z.X1} y {z.Y0}..{z.Y1} z {z.Z0}..{z.Z1} info {string.Join(",", z.Info)}");
        }
        return 0;
    }
}
