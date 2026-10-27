using System.Runtime.InteropServices;
using LBAAssembler.Scenes;

namespace ScriptRoundTrip;

// The main window's native renderer (liblba2_renderer.dll, this worktree's build) after a scene's file was written behind it -- the scene
// editor's save, an undo -- and on a map's island (2026-10-17):
//   - lba2_renderer_forget_scene_edits: an interior's session edit (an actor moved in the actor window) was put back over the file at every
//     load of the scene, so the scene editor's later move of the same actor (or an undo) didn't show; forgotten, the load shows the file
//   - an island of a map (ISLANDS.TXT "scene <n> <file>") is scanned for its own scenes: its actors are listed (they weren't: only the game's
//     scenes 0..221, by island number), Add Actor Here finds its scene; a game island's scan leaves those scenes out
//   scenesyncnative <LBA2 game folder: a sandbox, a plain copy> -- writes scene 7 of its SCENE.HQR and puts it back
internal static class SceneSyncNativeTest
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IntFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void VoidFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IntArgFn(int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int StringFn([MarshalAs(UnmanagedType.LPStr)] string text);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetActorFn(int index, out int x, out int y, out int z, out int waypointCount);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetActorPositionFn(int index, int x, int y, int z);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int AddActorFn(int x, int y, int z, int beta, int body, int anim, int lifePoint, int armor, int hitForce, int move);

    public static int Run(string[] args)
    {
        if (args.Length < 2 || !File.Exists(Path.Combine(args[1], "SCENE.HQR"))) { Console.WriteLine("scenesyncnative <LBA2 game folder (a sandbox)>"); return 1; }
        var game = Path.GetFullPath(args[1]);
        var dll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "native", "lba2-classic-community", "out", "build", "windows_ucrt64_static", "SOURCES", "3DEXT", "liblba2_renderer.dll"));
        if (!File.Exists(dll)) { Console.WriteLine($"not found: {dll}"); return 1; }
        var handle = NativeLibrary.Load(dll);
        T Get<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(handle, name));
        var failures = 0;
        void Check(string what, bool ok, string detail = "")
        {
            Console.WriteLine($"  {what}: {(ok ? "ok" : "FAILED")}{(detail.Length > 0 ? "  " + detail : "")}");
            if (!ok) failures++;
        }

        if (Get<StringFn>("lba2_renderer_set_data_root")(game) != 1 || Get<IntFn>("lba2_renderer_initialize")() != 1) { Console.WriteLine("the renderer didn't start"); return 1; }
        var loadIsland = Get<StringFn>("lba2_renderer_load_island");
        var loadInterior = Get<IntArgFn>("lba2_renderer_load_interior_scene");
        var actorCount = Get<IntFn>("lba2_renderer_get_actor_count");
        var getActor = Get<GetActorFn>("lba2_renderer_get_actor");
        var actorScene = Get<IntArgFn>("lba2_renderer_get_actor_scene");
        var setPosition = Get<SetActorPositionFn>("lba2_renderer_set_actor_position");
        var addActor = Get<AddActorFn>("lba2_renderer_add_actor");
        var forget = Get<IntArgFn>("lba2_renderer_forget_scene_edits");

        // ---- an interior's session edit, then its file written behind it
        const int scene = 7, paul = 1;              // Mr. Paul's house; Mr. Paul is the renderer's actor 1 (the scene's actor 2)
        var store = new SceneStore(SceneGame.Lba2, game);
        var original = File.ReadAllBytes(store.ScenePath);
        try
        {
            loadInterior(scene);
            getActor(paul, out var x0, out _, out _, out _);
            setPosition(paul, x0 + 1536, 768, 2048);         // the actor window's Apply: a session edit (and its own save, left out here)
            var model = store.Load(scene);
            model.Actors[paul + 1].X = x0 + 3072;            // the scene editor's save of another place
            store.Save(scene, model);
            loadInterior(scene);
            getActor(paul, out var stale, out _, out _, out _);
            Check("an interior's session edit is put back over the file at its next load (what forgetting is for)", stale == x0 + 1536, $"x {stale}");
            Check("forget_scene_edits says the scene had edits", forget(scene) == 1);
            loadInterior(scene);
            getActor(paul, out var fresh, out _, out _, out _);
            Check("forgotten, the next load shows the file", fresh == x0 + 3072, $"x {fresh}, the file {x0 + 3072}");
            Check("and forgetting again finds nothing", forget(scene) == 0);
        }
        finally
        {
            // (back as it was: beside it and moved over, so a hard-linked sandbox's source isn't written through)
            var temp = store.ScenePath + ".scenesync.tmp";
            File.WriteAllBytes(temp, original);
            File.Move(temp, store.ScenePath, overwrite: true);
        }

        // ---- islands of maps (ISLANDS.TXT)
        var files = LBAAssembler.Maps.IslandFiles.Load(game);
        var mapIsland = files.Scenes.Values.Distinct().FirstOrDefault(f => File.Exists(Path.Combine(game, f + ".ILE")));
        if (mapIsland is null) Console.WriteLine("  (the folder has no island of a map in ISLANDS.TXT: that part left out)");
        else
        {
            var own = files.Scenes.Where(s => s.Value == mapIsland && store.SceneExists(s.Key) && store.Load(s.Key).CubeMode == 1).Select(s => s.Key).ToHashSet();
            loadIsland(mapIsland.ToLowerInvariant());
            var count = actorCount();
            var scenes = Enumerable.Range(0, count).Select(i => actorScene(i)).ToList();
            var expected = own.Sum(s => store.Load(s).Actors.Count - 1);
            Check($"{mapIsland}'s actors are listed, all of its own scenes ({string.Join(", ", own)})", count == expected && scenes.All(own.Contains), $"{count} actors, {expected} expected");
            var first = store.Load(own.Min());
            var added = addActor(first.CubeX * 32768 + 16384, 1000, first.CubeY * 32768 + 16384, 0, 0, 0, 255, 0, 0, 0);
            Check("Add Actor Here finds the map's scene under the camera", added >= 0 && actorScene(added) == own.Min(), $"actor {added}, scene {(added >= 0 ? actorScene(added) : -1)}");

            loadIsland("citadel");
            var citadel = Enumerable.Range(0, actorCount()).Select(i => actorScene(i)).ToList();
            Check("a game island's scan leaves out the scenes ISLANDS.TXT draws on a file of their own", citadel.Count > 0 && !citadel.Any(files.Scenes.ContainsKey), $"{citadel.Count} actors");
        }

        Get<VoidFn>("lba2_renderer_shutdown")();
        Console.WriteLine(failures == 0 ? "scene sync native: all passed" : $"scene sync native: {failures} FAILED");
        return failures == 0 ? 0 : 1;
    }
}
