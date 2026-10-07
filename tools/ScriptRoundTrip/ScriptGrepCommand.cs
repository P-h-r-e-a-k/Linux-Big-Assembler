using System.Text.RegularExpressions;
using LBAAssembler;
using LBAAssembler.LbaScript;
using LBAAssembler.Scenes;

namespace ScriptRoundTrip;

// lba2scriptgrep <game folder> <regex>: every script line (life and track, every actor of every LBA2 scene) matching the regex, with where.
internal static class ScriptGrepCommand
{
    public static int Run(string[] args)
    {
        var store = new SceneStore(SceneGame.Lba2, args[1]);
        var rx = new Regex(args[2]);
        for (var scene = 0; scene < store.SceneCount; scene++)
        {
            if (!store.SceneExists(scene)) continue;
            SceneScripts scripts;
            try { scripts = SceneScripts.Load(SceneSerializer.Write(store.Load(scene)), scene); }
            catch (Exception e) { if (Environment.GetEnvironmentVariable("SCRIPTGREP_ERRORS") == "1") Console.WriteLine($"s{scene}: UNLOADED {e.Message}"); continue; }
            for (var a = 0; a < scripts.ActorCount; a++)
                foreach (var kind in new[] { ScriptKind.Life, ScriptKind.Track })
                {
                    string text;
                    try { text = scripts.GetText(a, kind); } catch (Exception e) { if (Environment.GetEnvironmentVariable("SCRIPTGREP_ERRORS") == "1") Console.WriteLine($"s{scene} a{a} {kind}: UNDECODED {e.Message}"); continue; }
                    foreach (var line in text.Split('\n').Where(l => rx.IsMatch(l)))
                        Console.WriteLine($"s{scene} a{a} {kind}: {line.Trim()}");
                }
        }
        return 0;
    }
}
