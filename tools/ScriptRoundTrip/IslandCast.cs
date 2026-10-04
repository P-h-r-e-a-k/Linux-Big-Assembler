using System.Buffers.Binary;
using LBAAssembler;
using LBAAssembler.Scenes;

namespace ScriptRoundTrip;

// islandcast <game folder> <BODY2.HQD> [SCENE2.HQD]: who lives on each island -- for every island index of the scenes, its scenes and the
// bodies (BODY.HQR entries, by name) its actors wear, with the scenes each is in. For choosing an island's drivers.
internal static class IslandCast
{
    public static int Run(string[] args)
    {
        var game = args[1];
        var bodyNames = File.ReadAllLines(args[2], System.Text.Encoding.Latin1).Skip(1).ToArray();
        var sceneNames = args.Length > 3 ? File.ReadAllLines(args[3], System.Text.Encoding.Latin1).Skip(2).ToArray() : Array.Empty<string>();
        string BodyName(int i) => i >= 0 && i < bodyNames.Length ? bodyNames[i] : "?";
        string SceneName(int i) => i >= 0 && i < sceneNames.Length ? sceneNames[i] : "";

        // the entity table: each entity's body records (1, generic, size, BODY.HQR index...)
        var table = HqrArchive.Open(Path.Combine(game, "RESS.HQR")).Read(44);
        var entities = BinaryPrimitives.ReadInt32LittleEndian(table) / 4 - 1;
        var bodies = new Dictionary<(int Entity, int Generic), int>();
        for (var e = 0; e < entities; e++)
        {
            var p = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(e * 4)); var end = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan((e + 1) * 4));
            while (p < end && table[p] != 255)
            {
                var command = table[p];
                if (command == 1) bodies[(e, table[p + 1])] = BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(p + 3));
                p += command == 3 ? 3 + table[p + 3] : 2 + table[p + 2];
            }
        }

        var store = new SceneStore(SceneGame.Lba2, game);
        var islands = new SortedDictionary<int, (List<int> Outside, List<int> Inside, Dictionary<int, SortedSet<int>> Cast)>();
        for (var scene = 0; scene < 222; scene++)
        {
            if (!store.SceneExists(scene)) continue;
            SceneModel model;
            try { model = store.Load(scene); } catch (Exception) { continue; }
            if (SceneName(scene).StartsWith("Demo Scene")) continue;
            if (!islands.TryGetValue(model.Island, out var island)) islands[model.Island] = island = (new(), new(), new());
            (model.CubeMode == 1 ? island.Outside : island.Inside).Add(scene);
            foreach (var actor in model.Actors.Skip(1))
            {
                if ((actor.Flags & (1u << 10)) != 0) continue;       // a sprite
                if (!bodies.TryGetValue((actor.Entity, actor.Body), out var body)) continue;
                // (BODYOF=<n>: where each actor with that BODY.HQR entry is)
                if (Environment.GetEnvironmentVariable("BODYOF") is { } of && int.Parse(of) == body)
                    Console.WriteLine($"body {body}: scene {scene} actor {model.Actors.IndexOf(actor)} entity {actor.Entity} at ({actor.X},{actor.Y},{actor.Z}) flags 0x{actor.Flags:X}");
                if (!island.Cast.TryGetValue(body, out var where)) island.Cast[body] = where = new SortedSet<int>();
                where.Add(scene);
            }
        }
        foreach (var (index, island) in islands)
        {
            Console.WriteLine($"=== island {index}: outside scenes {string.Join(",", island.Outside)}; inside {string.Join(",", island.Inside)}");
            foreach (var s in island.Outside) Console.WriteLine($"      {s}: {SceneName(s)}");
            foreach (var (body, where) in island.Cast.OrderBy(c => c.Key))
                Console.WriteLine($"  {body,3} {BodyName(body),-62} scenes {string.Join(",", where)}");
        }
        return 0;
    }
}
