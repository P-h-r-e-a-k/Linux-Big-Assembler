using LBAAssembler.Scenes;
using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// itemdiff <game folder A> <game folder B>: two builds of the same race tracks compared scene by scene with the power-ups' actors (each
// track's mushrooms, penguins, oil slicks and fake mushrooms, RACETRACK.JSON) taken out of both -- what is left the same in every scene,
// or where not, which actors differ -- and each track's mushrooms in A and B, scene by scene (2026-10-17: the item spacing's regression
// check, tools/RaceTrackStress/items.py).
internal static class ItemDiffCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 3) { Console.WriteLine("itemdiff <game folder A> <game folder B>"); return 1; }
        string a = args[1], b = args[2];
        // (each island's track and its other weather's twin, Citadel Island's town circuit)
        static List<RaceTrackService.TrackInfo> All(string game) =>
            RaceTrackService.Tracks(RaceTrackService.ReadInfo(game)).SelectMany(t => t.Twin is { } twin ? new[] { t, twin } : new[] { t }).ToList();
        var tracksA = All(a); var tracksB = All(b);
        static Dictionary<int, HashSet<int>> PowerUps(List<RaceTrackService.TrackInfo> tracks)
        {
            var map = new Dictionary<int, HashSet<int>>();
            foreach (var t in tracks)
                foreach (var m in (t.Mushrooms ?? new()).Concat(t.Penguins ?? new()).Concat(t.Oil ?? new()).Concat(t.Fakes ?? new()))
                {
                    if (!map.TryGetValue(m[0], out var set)) map[m[0]] = set = new();
                    set.Add(m[1]);
                }
            return map;
        }
        var upA = PowerUps(tracksA); var upB = PowerUps(tracksB);
        for (var k = 0; k < Math.Min(tracksA.Count, tracksB.Count); k++)
        {
            int ma = tracksA[k].Mushrooms?.Count ?? 0, mb = tracksB[k].Mushrooms?.Count ?? 0;
            var scenes = (tracksA[k].Mushrooms ?? new()).Concat(tracksB[k].Mushrooms ?? new()).Select(m => m[0]).Distinct().OrderBy(s => s)
                .Select(s => $"{s}:{tracksA[k].Mushrooms?.Count(m => m[0] == s) ?? 0}->{tracksB[k].Mushrooms?.Count(m => m[0] == s) ?? 0}");
            Console.WriteLine($"{tracksA[k].Island}{(k > 0 && tracksA[k].Island == tracksA[k - 1].Island ? " (twin)" : "")}: mushrooms {ma} -> {mb} ({string.Join(' ', scenes)})");
        }
        var storeA = new SceneStore(SceneGame.Lba2, a); var storeB = new SceneStore(SceneGame.Lba2, b);
        int same = 0, differ = 0, scenesChanged = 0;
        for (var s = 0; s < Math.Min(storeA.SceneCount, storeB.SceneCount); s++)
        {
            if (!storeA.SceneExists(s) || !storeB.SceneExists(s)) continue;
            var ra = storeA.LoadRecord(s); var rb = storeB.LoadRecord(s);
            if (ra.AsSpan().SequenceEqual(rb)) { same++; continue; }
            scenesChanged++;
            var sa = storeA.Load(s); var sb = storeB.Load(s);
            var keepA = sa.Actors.Where((_, i) => !(upA.TryGetValue(s, out var u) && u.Contains(i))).ToList();
            var keepB = sb.Actors.Where((_, i) => !(upB.TryGetValue(s, out var u) && u.Contains(i))).ToList();
            sa.Actors.Clear(); sa.Actors.AddRange(keepA); sb.Actors.Clear(); sb.Actors.AddRange(keepB);
            if (SceneSerializer.Write(sa).AsSpan().SequenceEqual(SceneSerializer.Write(sb))) { same++; continue; }
            differ++;
            var why = new List<string>();
            if (keepA.Count != keepB.Count) why.Add($"{keepA.Count} other actors against {keepB.Count}");
            for (var i = 0; i < Math.Min(keepA.Count, keepB.Count); i++)
            {
                var x = keepA[i]; var y = keepB[i];
                if (x.Entity != y.Entity || x.Body != y.Body || x.X != y.X || x.Y != y.Y || x.Z != y.Z || x.Flags != y.Flags || !x.Life.AsSpan().SequenceEqual(y.Life) || !x.Track.AsSpan().SequenceEqual(y.Track))
                    why.Add($"actor {i} (entity {x.Entity}{(x.Entity != y.Entity ? $"/{y.Entity}" : "")}){(x.Life.AsSpan().SequenceEqual(y.Life) ? "" : " life")}{(x.Track.AsSpan().SequenceEqual(y.Track) ? "" : " track")}{(x.X != y.X || x.Y != y.Y || x.Z != y.Z ? " place" : "")}");
            }
            if (sa.Zones.Count != sb.Zones.Count) why.Add($"{sa.Zones.Count} zones against {sb.Zones.Count}");
            if (sa.TrackPoints.Count != sb.TrackPoints.Count) why.Add($"{sa.TrackPoints.Count} track points against {sb.TrackPoints.Count}");
            Console.WriteLine($"scene {s}: differs beyond the power-ups: {string.Join("; ", why.Take(8))}");
        }
        Console.WriteLine($"{scenesChanged} scenes' records differ; {same} scenes the same once the power-ups are taken out, {differ} not");
        return differ == 0 ? 0 : 2;
    }
}
