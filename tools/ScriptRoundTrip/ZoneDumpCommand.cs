namespace ScriptRoundTrip;

// zonedump <game folder> <scene> [type]: a scene's zones with every field (box, number, info words), for copying a retail zone's shape.
internal static class ZoneDumpCommand
{
    public static int Run(string[] args)
    {
        var model = new LBAAssembler.Lba2Interiors(args[1]).LoadScene(int.Parse(args[2]))!;
        int? type = args.Length > 3 ? int.Parse(args[3]) : null;
        Console.WriteLine($"scene {args[2]}: cube ({model.CubeX},{model.CubeY}), {model.Zones.Count} zones");
        for (var i = 0; i < model.Zones.Count; i++)
        {
            var z = model.Zones[i];
            if (type is { } t && z.Type != t) continue;
            Console.WriteLine($"  {i}: type {z.Type} num {z.Num} box ({z.X0},{z.Y0},{z.Z0})-({z.X1},{z.Y1},{z.Z1}) info [{string.Join(",", z.Info)}]");
        }
        return 0;
    }

    // zoneon <game folder> <scene> <zone>: a scene's zone switched on from the start (its info word 7's bit 1) -- a test folder's, to come
    // through a door a script would open
    public static int On(string[] args)
    {
        var store = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, args[1]);
        var scene = int.Parse(args[2]);
        var model = store.Load(scene);
        var z = model.Zones[int.Parse(args[3])];
        z.Info[7] |= 1;
        store.Save(scene, model, allowErrors: true);
        Console.WriteLine($"scene {scene} zone {args[3]}: on ({string.Join(",", z.Info)})");
        return 0;
    }
}
