using LBAAssembler.Lba1;
using LBAAssembler.Terrain.Polar;

namespace ScriptRoundTrip;

// lba1ground <LBA1 folder> <scene> <out folder>: an LBA1 outside scene's ground as a recreation checks itself against -- for each column
// the top of its highest ground brick (layers; a slope brick half a layer lower), the brick and its code; and what stands on the ground
// (the bricks over it that aren't ground: posts, walls, huts, trees), by code. Writes ground.csv (x,z,top,brick,code,shape), things.csv
// (x,y,z,brick,code,shape,block) and a text map of the tops (a letter a layer: 0-9 then A-Z, '.' none).
internal static class Lba1Ground
{
    // ground: snow (4x), dirt and gravel (6x), the gravel-snow edges (46), water (F1); the rest stands on it
    public static bool IsGround(int code) => code is 0xF1 || (code & 0xF0) is 0x40 or 0x60 || code is 0x06 or 0x77 or 0x88;

    public static int Run(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var scene = int.Parse(args[2]);
        var outDir = args[3];
        Directory.CreateDirectory(outDir);
        var bricks = PolarLayout.Bricks(game, scene);
        var top = new Dictionary<(int, int), PolarLayout.SceneBrick>();
        foreach (var b in bricks.Where(b => IsGround(b.Code)))
            if (!top.TryGetValue((b.X, b.Z), out var t) || b.Y > t.Y) top[(b.X, b.Z)] = b;
        var g = new System.Text.StringBuilder("x,z,top,brick,code,shape\n");
        var map = new System.Text.StringBuilder();
        const string digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        for (var z = 0; z < 64; z++)
        {
            for (var x = 0; x < 64; x++)
            {
                if (!top.TryGetValue((x, z), out var t)) { map.Append('.'); continue; }
                // (a slope or step brick's top is lower than a whole brick's)
                var height = t.Y + (t.Shape is >= 2 and <= 13 ? 0.5 : 1);
                g.AppendLine($"{x},{z},{height},{t.Brick},{t.Code:X2},{t.Shape}");
                map.Append(digits[Math.Min(35, (int)Math.Floor(height))]);
            }
            map.AppendLine();
        }
        File.WriteAllText(Path.Combine(outDir, "ground.csv"), g.ToString());
        File.WriteAllText(Path.Combine(outDir, "tops.txt"), map.ToString());
        var things = bricks.Where(b => !IsGround(b.Code) && (!top.TryGetValue((b.X, b.Z), out var t) || b.Y >= t.Y)).ToList();
        File.WriteAllText(Path.Combine(outDir, "things.csv"), "x,y,z,brick,code,shape,block\n" + string.Join("\n", things.Select(b => $"{b.X},{b.Y},{b.Z},{b.Brick},{b.Code:X2},{b.Shape},{b.Block}")));
        // (and every brick of the scene: the objects' bricks for the Object maker's reference)
        File.WriteAllText(Path.Combine(outDir, "bricks.csv"), "x,y,z,brick,code,shape,block\n" + string.Join("\n", bricks.Select(b => $"{b.X},{b.Y},{b.Z},{b.Brick},{b.Code:X2},{b.Shape},{b.Block}")));
        Console.Write(map);
        Console.WriteLine($"{top.Count} columns of ground, {things.Count} bricks of things on it ({string.Join(", ", things.GroupBy(b => b.Code).OrderByDescending(x => x.Count()).Select(x => $"{x.Count()}x{x.Key:X2}"))})");
        return 0;
    }
}
