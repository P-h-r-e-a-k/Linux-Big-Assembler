using System.Text.RegularExpressions;
using LBAAssembler;
using LBAAssembler.Scenes;
using LBAAssembler.LbaScript;

namespace ScriptRoundTrip;

// holopos <game folder>: which scene numbers exist, and every set_holo_pos / clr_holo_pos in every script (the holomap position numbers
// the game uses), to find a number free for a new arrow.
internal static class HoloPosCommand
{
    public static int Run(string[] args)
    {
        var store = new SceneStore(SceneGame.Lba2, args[1]);
        var exists = new SortedSet<int>(); var used = new SortedDictionary<int, List<string>>();
        var rx = new Regex(@"(set|clr)_holo_pos\((\d+)\)");
        for (var scene = 0; scene < store.SceneCount; scene++)
        {
            if (!store.SceneExists(scene)) continue;
            exists.Add(scene);
            SceneScripts scripts;
            try { scripts = SceneScripts.Load(SceneSerializer.Write(store.Load(scene)), scene); } catch (Exception) { continue; }
            for (var a = 0; a < scripts.ActorCount; a++)
                foreach (var kind in new[] { ScriptKind.Life, ScriptKind.Track })
                {
                    string text;
                    try { text = scripts.GetText(a, kind); } catch (Exception) { continue; }
                    foreach (Match m in rx.Matches(text))
                    {
                        var n = int.Parse(m.Groups[2].Value);
                        if (!used.TryGetValue(n, out var l)) used[n] = l = new();
                        l.Add($"{m.Groups[1].Value} s{scene}a{a}");
                    }
                }
        }
        Console.WriteLine($"scenes that exist ({exists.Count}): {string.Join(" ", exists.Where(s => s < 100))} ...");
        foreach (var (n, where) in used) Console.WriteLine($"holo pos {n}: {string.Join(", ", where.Distinct().Take(6))}");
        var free = Enumerable.Range(0, 100).Where(n => !exists.Contains(n) && !used.ContainsKey(n)).ToList();
        Console.WriteLine($"free (no scene, no script): {string.Join(" ", free)}");
        return 0;
    }
}
