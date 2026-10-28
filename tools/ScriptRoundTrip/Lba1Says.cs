using LBAAssembler.Lba1;

namespace ScriptRoundTrip;

// lba1says <LBA1 folder> <first scene> <last scene> <text id>...: the actors whose life scripts hold MESSAGE (opcode 25, its id the next two
// bytes) of those texts -- a rough scan of the bytes, for finding who says what
internal static class Lba1Says
{
    public static int Run(string[] args)
    {
        var game = new Lba1Game(args[1]);
        int first = int.Parse(args[2]), last = int.Parse(args[3]);
        var ids = args.Skip(4).Select(int.Parse).ToHashSet();
        for (var n = first; n <= last; n++)
        {
            Lba1Scene scene;
            try { scene = game.LoadScene(n); } catch (Exception) { continue; }
            foreach (var a in scene.Actors)
            {
                var life = a.LifeScript ?? Array.Empty<byte>();
                for (var i = 0; i + 2 < life.Length; i++)
                    if (life[i] == 25 && ids.Contains(life[i + 1] | life[i + 2] << 8))
                        Console.WriteLine($"scene {n} actor {a.Index}: entity {a.Entity} \"{game.EntityName(a.Entity)}\" body {a.Body} (BODY.HQR {game.BodyIndex(a.Entity, a.Body)}) at cell ({a.X / 512.0:0.0}, {a.Y / 256.0:0.0}, {a.Z / 512.0:0.0}) says {life[i + 1] | life[i + 2] << 8} at byte {i}");
            }
        }
        return 0;
    }
}
