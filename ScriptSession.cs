using System.IO;
using LBAAssembler.LbaScript;

namespace LBAAssembler;

// Where a UI actor's scripts live in SCENE.HQR: scene number (LBA2: HQR entry - 1, LBA1: the entry
// itself) and object slot inside that scene's record (0 = hero, 1.. = scene actors).
internal readonly record struct ActorSource(int Scene, int Slot);

internal sealed record SessionSaveResult(bool Ok, string Message, IReadOnlyList<ScriptDiagnostic> Errors);

// The editor's working set of scene scripts: loads scenes from SCENE.HQR on
// demand, keeps each scene's edits (as C text) for the whole session, and writes
// edited scenes back to SCENE.HQR on Save.
internal sealed class ScriptSession
{
    private readonly Func<string> gameRoot;
    private readonly bool lba1;
    private readonly Dictionary<int, SceneScripts> scenes = new();
    private readonly Dictionary<int, List<ActorSource>> exteriorMaps = new();
    private readonly string? commentsPathOverride;
    private CommentStore? commentStore;
    // The game folder the scenes held here were read from. The folder open can change under the session (a map opened or closed, test
    // edits started or ended: the editor's game folder is then a working copy, or the game's own again) and scene numbers mean another
    // scene there -- an LBA1 map takes a scene slot of the game. Scenes are only ever shown from, and saved into, the folder they were
    // read from: an LBA1 map's unsaved script edit, kept after Close map, showed as the game's own scene 28 and was saved over it
    // (2026-10-17). MainWindow settles the edits (saves or drops them) before it changes the folder; this is the safety net.
    private string? cacheFolder;

    // commentsPath: where comments are kept; by default a sidecar next to SCENE.HQR.
    // lba1: the game folder holds Little Big Adventure 1 (scene N in HQR entry N, LBA1 opcodes and scene layout).
    public ScriptSession(Func<string> gameRoot, string? commentsPath = null, bool lba1 = false)
    {
        this.gameRoot = gameRoot;
        this.lba1 = lba1;
        commentsPathOverride = commentsPath;
    }

    public bool IsLba1 => lba1;

    // SCENE.HQR entry of a scene: LBA2 keeps a size record in entry 0.
    private int Entry(int scene) => lba1 ? scene : scene + 1;

    public string HqrPath => Path.Combine(gameRoot(), "SCENE.HQR");

    // The game folder open now (an open map's workspace while a map is open).
    public string Folder => Normal(gameRoot());

    // Where the scenes held here (and their edits) come from, when that isn't the folder open now; null when it is (or nothing is held).
    public string? OtherFolder => cacheFolder is { } f && !string.Equals(f, Folder, StringComparison.OrdinalIgnoreCase) ? f : null;

    private static string Normal(string folder)
        => string.IsNullOrEmpty(folder) ? "" : Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    // Forgets every scene held, edits and all (the caller has saved or given up what it wanted kept).
    public void DiscardAll()
    {
        scenes.Clear();
        exteriorMaps.Clear();
        cacheFolder = null;
        commentStore = null;
    }

    // Before a scene is read: scenes held from another folder go (they belong to it, not to the one open now) -- unless they have unsaved
    // edits, which are never dropped behind the user's back: then nothing is read from this folder until they are settled.
    private bool FolderReady()
    {
        if (OtherFolder is null) return true;
        if (HasUnsavedEdits)
        {
            DebugLog.Log($"ScriptSession: unsaved script edits of {cacheFolder} (scene {string.Join(", ", EditedScenes)}) are held; {Folder} isn't read until they are saved or dropped");
            return false;
        }
        DiscardAll();
        return true;
    }

    public string CommentsPath => commentsPathOverride ?? Path.Combine(cacheFolder ?? gameRoot(), "SCENE.HQR") + ".comments.json";

    // Loaded on first use. If the file exists but could not be read, LoadError says
    // why and nothing is overwritten without keeping a copy first.
    private CommentStore Comments
    {
        get
        {
            if (commentStore is null || commentStore.Path != CommentsPath) commentStore = CommentStore.Load(CommentsPath);
            return commentStore;
        }
    }

    public string? CommentsLoadError => Comments.LoadError;

    // Number of scene entries in SCENE.HQR (entry 0 is metadata, not a scene).
    private int SceneEntryCount() => Math.Max(0, HqrArchive.CountEntries(HqrPath) - (lba1 ? 0 : 1));

    public SceneScripts? GetScene(int scene)
    {
        if (!FolderReady()) return null;
        if (scenes.TryGetValue(scene, out var loaded)) return loaded;
        try
        {
            if (!File.Exists(HqrPath)) return null;
            var archive = HqrArchive.Open(HqrPath);
            if (scene < 0 || scene >= SceneEntryCount() || !archive.IsValid(Entry(scene))) return null;
            var s = SceneScripts.Load(archive.Read(Entry(scene)), scene, (actor, kind) => Comments.Get(scene, actor, kind), lba1);
            scenes[scene] = s;
            cacheFolder = Folder;
            return s;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ScriptFormatException or UnauthorizedAccessException)
        {
            DebugLog.Log($"ScriptSession: could not load scene {scene}: {e.Message}");
            return null;
        }
    }

    // Reprint every loaded scene's unedited scripts (after a ScriptStyle change).
    public void RefreshStyle()
    {
        foreach (var s in scenes.Values) s.InvalidateTextCache();
    }

    public bool HasUnsavedEdits => scenes.Values.Any(s => s.HasEdits);

    // Drops the cached copy of a scene so it is read from SCENE.HQR again (after something else changed the file).
    // False, and nothing dropped, while the scene has unsaved script edits.
    public bool ForgetScene(int scene)
    {
        if (scenes.TryGetValue(scene, out var loaded) && loaded.HasEdits) return false;
        scenes.Remove(scene);
        exteriorMaps.Clear();
        return true;
    }

    public IReadOnlyList<int> EditedScenes => scenes.Where(p => p.Value.HasEdits).Select(p => p.Key).Order().ToList();

    // The exterior actor list the native scan builds for an island (see
    // RendererScanIslandActors): scenes 0..221 in order, keeping those whose
    // record says Island == islandIndex and CubeMode == exterior, and for each
    // its objects 1..N-1. Actors the editor adds later are appended after these
    // and have no scene record yet, so they resolve to null.
    public ActorSource? ExteriorActor(int islandIndex, int actorIndex)
    {
        if (islandIndex < 0 || actorIndex < 0 || !FolderReady()) return null;
        if (!exteriorMaps.TryGetValue(islandIndex, out var map)) exteriorMaps[islandIndex] = map = BuildExteriorMap(islandIndex);
        return actorIndex < map.Count ? map[actorIndex] : null;
    }

    public int ExteriorActorCount(int islandIndex)
    {
        if (!FolderReady()) return 0;
        if (!exteriorMaps.TryGetValue(islandIndex, out var map)) exteriorMaps[islandIndex] = map = BuildExteriorMap(islandIndex);
        return map.Count;
    }

    private List<ActorSource> BuildExteriorMap(int islandIndex)
    {
        var map = new List<ActorSource>();
        cacheFolder ??= Folder;         // (the list is this folder's: another folder's numbering isn't kept for it)
        try
        {
            if (!File.Exists(HqrPath)) return map;
            var archive = HqrArchive.Open(HqrPath);
            var last = Math.Min(221, SceneEntryCount() - 1);
            for (var scene = 0; scene <= last; scene++)
            {
                if (!archive.IsValid(scene + 1)) continue;
                var rec = SceneRecord.Parse(archive.Read(scene + 1));
                if (rec.CubeMode != 1 || rec.Island != islandIndex) continue;
                for (var slot = 1; slot < rec.Actors.Count; slot++) map.Add(new ActorSource(scene, slot));
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ScriptFormatException or UnauthorizedAccessException)
        {
            DebugLog.Log($"ScriptSession: could not map island {islandIndex}: {e.Message}");
            map.Clear();
        }
        return map;
    }

    // Compiles every edited scene and writes the result into SCENE.HQR. Nothing
    // is written unless every edited script compiles and every cross-reference
    // resolves. The first save keeps the untouched original as SCENE.HQR.bak.
    public SessionSaveResult SaveAll()
    {
        var edited = EditedScenes;
        if (edited.Count == 0) return new SessionSaveResult(true, "No edited scripts to save.", Array.Empty<ScriptDiagnostic>());
        // (edits go back into the folder their scenes were read from, never into another one's: scene N there is another scene here -- a
        // map's working copy that is gone can't take them)
        var folder = cacheFolder ?? Folder;
        if (!File.Exists(Path.Combine(folder, "SCENE.HQR")))
            return new SessionSaveResult(false, $"Not saved: the edited scripts (scene {string.Join(", ", edited)}) were made in {folder}, which isn't there any more.", Array.Empty<ScriptDiagnostic>());

        var built = new List<(int Scene, byte[] Record, IReadOnlyDictionary<(int Actor, ScriptKind Kind), CommentSet?> Comments)>();
        var errors = new List<ScriptDiagnostic>();
        foreach (var scene in edited)
        {
            var r = scenes[scene].Build();
            if (r.Ok) built.Add((scene, r.Record!, r.Comments ?? new Dictionary<(int, ScriptKind), CommentSet?>()));
            else errors.AddRange(r.Errors.Select(e => e with { Scene = scene }));
        }
        if (errors.Count > 0)
            return new SessionSaveResult(false, $"Not saved: {errors.Count} script problem(s). First: {errors[0]}", errors);

        try
        {
            var path = Path.Combine(folder, "SCENE.HQR");
            var archive = HqrArchive.Open(path);

            // Only scenes whose record really changed need SCENE.HQR rewritten: a save
            // that only touched comments leaves the game file alone.
            var changed = built.Where(b => !archive.Read(Entry(b.Scene)).AsSpan().SequenceEqual(b.Record)).ToList();

            // Comments first. They are low-risk, and if writing them fails nothing else has been touched.
            var store = Comments;
            foreach (var (scene, _, comments) in built)
                foreach (var ((actor, kind), set) in comments)
                    store.Set(scene, actor, kind, set);
            var commentsChanged = store.Dirty;
            store.Save();

            var parts = new List<string>();
            if (changed.Count > 0)
            {
                // Through the scene store: the records are checked against the engine's limits, LBA2's patch table and
                // largest-scene record are brought up to date (the rebuilt scripts move things), all in one
                // verified transaction, and the save goes on the undo log.
                var game = lba1 ? Scenes.SceneGame.Lba1 : Scenes.SceneGame.Lba2;
                var sceneStore = new Scenes.SceneStore(game, Path.GetDirectoryName(path)!);
                sceneStore.SaveMany(
                    changed.Select(c => new Scenes.SceneChange(c.Scene, Scenes.SceneSerializer.Parse(game, c.Record))).ToList(),
                    description: $"Save scripts of scene {string.Join(", ", changed.Select(b => b.Scene))}");
                parts.Add(Maps.MapWorkspace.For(folder) is { } map
                    ? $"scene {string.Join(", ", changed.Select(b => b.Scene))} written into the map \"{map.Name}\" ({Maps.MapWorkspace.AfterSave(folder)})"
                    : $"scene {string.Join(", ", changed.Select(b => b.Scene))} written to {Path.GetFileName(path)} (backup: {Path.GetFileName(path)}.bak)");
            }
            else parts.Add($"script bytes unchanged, {Maps.MapWorkspace.Target(folder)} left alone");
            if (commentsChanged) parts.Add($"comments saved to {Path.GetFileName(store.Path)}");

            // What was saved is the new baseline for those scenes.
            foreach (var (scene, _, _) in built) scenes.Remove(scene);
            exteriorMaps.Clear();
            return new SessionSaveResult(true, "Saved: " + string.Join("; ", parts) + ".", Array.Empty<ScriptDiagnostic>());
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ScriptFormatException)
        {
            try { File.Delete(Path.Combine(folder, "SCENE.HQR") + ".tmp"); } catch (IOException) { }
            if (e is Scenes.SceneValidationException) DebugLog.Log($"ScriptSession: save refused by the scene validator: {e.Message}");
            return new SessionSaveResult(false, $"Not saved: {e.Message}", Array.Empty<ScriptDiagnostic>());
        }
    }
}
