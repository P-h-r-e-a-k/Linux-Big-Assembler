using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LBAAssembler.LbaScript;

// The comments sidecar: one JSON file holding every script's comments, keyed by
// scene / actor slot / life-or-track. Kept beside SCENE.HQR as
// "SCENE.HQR.comments.json" so it travels with the game data being edited.
//
//   {
//     "version": 1,
//     "scenes": [ { "scene": 2, "actors": [ { "actor": 0,
//         "life": { "fingerprints": "0A1B2C3D4E 11...", "comments": [
//             { "at": 5, "place": "Before", "lines": [" walk to the door"] } ] } } ] } ]
//   }
//
// Fingerprints are one space-separated string (see Fingerprint); "at" is an
// instruction index into the script those fingerprints describe.
internal sealed class CommentStore
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<(int Scene, int Actor, ScriptKind Kind), CommentSet> sets = new();
    private bool dirty;

    public string Path { get; }

    // Non-null when the file existed but could not be read: nothing was loaded, and
    // Save() will keep a copy of the unreadable file (Path + ".bad") instead of
    // silently overwriting it.
    public string? LoadError { get; private set; }

    private CommentStore(string path) => Path = path;

    public int Count => sets.Count;

    public static CommentStore Load(string path)
    {
        var store = new CommentStore(path);
        if (!File.Exists(path)) return store;
        try
        {
            var dto = JsonSerializer.Deserialize<StoreDto>(File.ReadAllText(path), Json) ?? throw new JsonException("empty file");
            if (dto.Version > CurrentVersion) throw new JsonException($"written by a newer editor (version {dto.Version})");
            foreach (var scene in dto.Scenes)
                foreach (var actor in scene.Actors)
                {
                    if (actor.Life is not null) store.sets[(scene.Scene, actor.Actor, ScriptKind.Life)] = actor.Life.ToSet();
                    if (actor.Track is not null) store.sets[(scene.Scene, actor.Actor, ScriptKind.Track)] = actor.Track.ToSet();
                }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            store.sets.Clear();
            store.LoadError = e.Message;
        }
        return store;
    }

    public CommentSet? Get(int scene, int actor, ScriptKind kind) =>
        sets.TryGetValue((scene, actor, kind), out var s) ? s : null;

    // A null or empty set removes the entry.
    public void Set(int scene, int actor, ScriptKind kind, CommentSet? set)
    {
        var key = (scene, actor, kind);
        if (set is null || set.IsEmpty) { if (sets.Remove(key)) dirty = true; return; }
        sets[key] = set;
        dirty = true;
    }

    public bool Dirty => dirty;

    // Writes the file (atomically: temp file, then move over the target). With
    // nothing stored the file is removed rather than left empty.
    public void Save()
    {
        if (!dirty) return;
        // (the old .bad removed first, not copied over: in a map's or test edits' copy of the game folder it can be a hard link to the
        // game folder's own, and copying over a hard link writes the file both names share)
        if (LoadError is not null && File.Exists(Path))
        {
            if (File.Exists(Path + ".bad")) File.Delete(Path + ".bad");
            File.Copy(Path, Path + ".bad");
        }

        if (sets.Count == 0)
        {
            if (File.Exists(Path)) File.Delete(Path);
            dirty = false;
            LoadError = null;
            return;
        }

        var temp = Path + ".tmp";
        File.WriteAllText(temp, ToJson());
        File.Move(temp, Path, overwrite: true);
        dirty = false;
        LoadError = null;
    }

    // ---- the sets as data (a map file keeps its scenes' comments: MapInstall)

    public IEnumerable<(int Scene, int Actor, ScriptKind Kind, CommentSet Set)> Entries => sets.Select(p => (p.Key.Scene, p.Key.Actor, p.Key.Kind, p.Value));

    public string ToJson()
    {
        var dto = new StoreDto { Version = CurrentVersion };
        foreach (var byScene in sets.GroupBy(p => p.Key.Scene).OrderBy(g => g.Key))
        {
            var scene = new SceneDto { Scene = byScene.Key };
            foreach (var byActor in byScene.GroupBy(p => p.Key.Actor).OrderBy(g => g.Key))
            {
                var actor = new ActorDto { Actor = byActor.Key };
                foreach (var (key, set) in byActor)
                {
                    var script = ScriptDto.From(set);
                    if (key.Kind == ScriptKind.Life) actor.Life = script; else actor.Track = script;
                }
                scene.Actors.Add(actor);
            }
            dto.Scenes.Add(scene);
        }
        return JsonSerializer.Serialize(dto, Json);
    }

    // Sets read from JSON text (ToJson's), kept in memory under `path` (nothing is read from it). Unreadable text gives an empty store.
    public static CommentStore FromJson(string json, string path)
    {
        var store = new CommentStore(path);
        try
        {
            var dto = JsonSerializer.Deserialize<StoreDto>(json, Json) ?? throw new JsonException("empty");
            foreach (var scene in dto.Scenes)
                foreach (var actor in scene.Actors)
                {
                    if (actor.Life is not null) store.sets[(scene.Scene, actor.Actor, ScriptKind.Life)] = actor.Life.ToSet();
                    if (actor.Track is not null) store.sets[(scene.Scene, actor.Actor, ScriptKind.Track)] = actor.Track.ToSet();
                }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { store.sets.Clear(); store.LoadError = e.Message; }
        return store;
    }

    // ---- file shape ----------------------------------------------------------

    private sealed class StoreDto
    {
        public int Version { get; set; }
        public List<SceneDto> Scenes { get; set; } = new();
    }

    private sealed class SceneDto
    {
        public int Scene { get; set; }
        public List<ActorDto> Actors { get; set; } = new();
    }

    private sealed class ActorDto
    {
        public int Actor { get; set; }
        public ScriptDto? Life { get; set; }
        public ScriptDto? Track { get; set; }
    }

    private sealed class ScriptDto
    {
        public string Fingerprints { get; set; } = "";
        public List<CommentDto> Comments { get; set; } = new();

        public static ScriptDto From(CommentSet s) => new()
        {
            Fingerprints = string.Join(' ', s.Fingerprints),
            Comments = s.Comments.Select(c => new CommentDto { At = c.Instr, Place = c.Place, Lines = c.Lines.ToList() }).ToList(),
        };

        public CommentSet ToSet() => new()
        {
            Fingerprints = Fingerprints.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
            Comments = Comments.Select(c => new ScriptComment(c.At, c.Place, c.Lines)).ToList(),
        };
    }

    private sealed class CommentDto
    {
        public int At { get; set; }
        public CommentPlace Place { get; set; }
        public List<string> Lines { get; set; } = new();
    }
}
