using LBAAssembler;
using LBAAssembler.Lba1;
using LBAAssembler.LbaScript;

namespace ScriptRoundTrip;

// hamalayiscripts <LBA1 folder> <out folder> [scene...]: each of LBA1's Hamalayi scenes (outside and in) -- its actors (entity, body,
// flags, place, life points), zones (type, box, info) and track points, and every actor's life and track scripts as the script editor
// shows them -- one text file a scene, and an index of them; what a recreation in LBA2 has to carry over.
internal static class HamalayiScripts
{
    public static readonly int[] Scenes = { 9, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 82, 15, 73, 81, 91, 92, 96 };

    public static int Run(string[] args)
    {
        var game = new Lba1Game(args[1]);
        var outDir = args[2];
        Directory.CreateDirectory(outDir);
        var scenes = args.Length > 3 ? args.Skip(3).Select(int.Parse).ToArray() : Scenes;
        var archive = HqrArchive.Open(Path.Combine(args[1], "SCENE.HQR"));
        var index = new System.Text.StringBuilder();
        foreach (var n in scenes)
        {
            var scene = game.LoadScene(n);
            var info = game.Description(n);
            var record = archive.Read(n);
            var scripts = SceneScripts.Load(record, n, lba1: true);
            var t = new System.Text.StringBuilder();
            t.AppendLine($"scene {n}: {info ?? "?"} (island {scene.Island}, music {scene.Music}, light {scene.AlphaLight}/{scene.BetaLight})");
            t.AppendLine($"{scene.Actors.Count} actors, {scene.Zones.Count} zones, {scene.Tracks.Count} track points");
            t.AppendLine();
            foreach (var a in scene.Actors)
            {
                t.AppendLine($"== actor {a.Index}: entity {a.Entity} body {a.Body} anim {a.Anim} sprite {a.Sprite} flags 0x{a.StaticFlags:X4} at ({a.X},{a.Y},{a.Z}) angle {a.Angle} " +
                             $"speed {a.Speed} control {a.ControlMode} armor {a.Armor} life {a.LifePoints} talk {a.TalkColor}");
                t.AppendLine(scripts.GetText(a.Index, ScriptKind.Life).TrimEnd());
                t.AppendLine(scripts.GetText(a.Index, ScriptKind.Track).TrimEnd());
                t.AppendLine();
            }
            t.AppendLine("== zones");
            foreach (var z in scene.Zones)
                t.AppendLine($"zone {z.Num}: type {z.Type} ({z.X0},{z.Y0},{z.Z0})-({z.X1},{z.Y1},{z.Z1}) info [{string.Join(",", z.Info)}] snap {z.Snap}");
            t.AppendLine("== track points");
            for (var i = 0; i < scene.Tracks.Count; i++) t.AppendLine($"point {i}: ({scene.Tracks[i].X},{scene.Tracks[i].Y},{scene.Tracks[i].Z})");
            var file = Path.Combine(outDir, $"scene{n}.txt");
            File.WriteAllText(file, t.ToString());
            var zoneTypes = string.Join(" ", scene.Zones.GroupBy(z => z.Type).OrderBy(g => g.Key).Select(g => $"{g.Count()}x{g.Key}"));
            var lifeBytes = scene.Actors.Sum(a => a.LifeScript.Length);
            index.AppendLine($"scene {n,3} {info ?? "?",-50} actors {scene.Actors.Count,2} zones {scene.Zones.Count,2} ({zoneTypes}) tracks {scene.Tracks.Count,3} life bytes {lifeBytes}");
        }
        File.WriteAllText(Path.Combine(outDir, "index.txt"), index.ToString());
        Console.Write(index);
        return 0;
    }
}
