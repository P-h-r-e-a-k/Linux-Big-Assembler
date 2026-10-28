using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LBAAssembler.Maps;

// What a map is: an LBA2 island (its ground and its objects -- ILE and OBL -- and an outside scene for each of its cubes), an LBA2 interior
// (a scene and its grid of blocks, with its block library and bricks or in one of the game's) or an LBA1 interior (a scene, its grid and its
// block library).
internal enum MapKind { Lba2Island, Lba2Interior, Lba1Interior }

// One scene of a map: its record as SCENE.HQR holds it, and -- an island's -- the cube it is the outside scene of. Its cube-change and door
// zones to the map's own scenes name them LocalScene + their place in the map, not a game scene number: the numbers a map gets in a game
// folder are only known when it is installed there (MapInstall), or read from its MAP.TXT by the engine (MAPPACK.CPP).
internal sealed record MapScene(byte[] Record, int CubeX = 0, int CubeZ = 0);

// A map in a file of its own, `.lbamap` (the user, 2026-10-15: "Let's also add the ability to load new maps from a file instead of them
// having to be in a HQR file"; then: "save it as two files a gr2 and an ls2 ... a zip with a custom extension so it can keep .lbamap but it
// is actually a zip file and can just be opened with a file extractor"): a zip, its files stored (not compressed: the engine reads them in
// place), of
//   MAP.TXT          what it is, "key = value" lines a person can edit (Manifest below)
//   <NAME>.LS2       each scene's record as SCENE.HQR holds it (LBA1: .LS1); an island's, <NAME>_<k>.LS2
//   <NAME>.GR2       an interior's grid (LBA2: LBA_BKG.HQR's entry, its 34-byte header and all; LBA1: LBA_GRI.HQR's, .GR1)
//   <NAME>.BL2       its own block library (LBA1: .BL1, LBA_BLL.HQR's entry: each LBA1 scene has its own)
//   <NAME>.BRK       its own bricks, an HQR: brick n its entry n, as its library's brick numbers count them
//   <NAME>.GRM       its own grid fragments, an HQR, fragment n its entry n
//   <NAME>.ILE/.OBL  an island's ground and objects
//   <NAME>_COMMENTS.JSON  the script editor's comments on its scenes' scripts (2026-10-17; the engine leaves it alone)
//   <NAME>_CHARACTERS.TXT, _BODIES.HQR, _ANIMS.HQR, _ENTITIES.HQR  its own characters (the Character maker, 2026-10-23): their list
//                    (MapCharacters's lines, numbered as the map's own: entity k, body and animation entries k of its HQRs), their bodies,
//                    their animations and their entity records (those entries' numbers the map's own too); a scene's actor of the map's
//                    character k is of entity LocalEntity + k
//   <NAME>_TEXT.TXT  its own lines of dialogue (2026-10-23), UTF-8, "<number> = <line>" a line ("<number>:<attribute> = ..." for a line
//                    shown other than as an ordinary one): its scenes' messages of those numbers are its lines (MAPPACK.CPP MapPack_Text)
// The engine plays an LBA2 map put in the game folder's MAPS folder (native MAPPACK.CPP). The editor opens it into a copy of the game folder
// with the map installed in its HQRs (MapInstall), where every editor and Play work on it as on any of the game's own, and saves it back out
// of it. A map of the first version -- map.json, scene<k>.bin, grid.bin, library.bin, island.ile/.obl, compressed -- still opens.
internal sealed class LbaMap
{
    public const string Extension = ".lbamap";
    public const string FileFilter = "LBA Assembler maps (*.lbamap)|*.lbamap";
    public const string ManifestName = "MAP.TXT";
    public const int FormatVersion = 2;
    // (a zone's number from LocalScene on: the map's own scene, by its place in Scenes)
    public const int LocalScene = 30000;
    // (an actor's entity from LocalEntity on: the map's own character, by its place in its list)
    public const int LocalEntity = 30000;

    public MapKind Kind { get; set; }
    public string Name { get; set; } = "New map";
    public string Description { get; set; } = "";
    // LBA2: the game's island its scenes carry the number of -- whose sky, palette, music and holomap they borrow (0 Citadel ... 11 the
    // Undergas Island, 12 Polar Island)
    public int IslandByte { get; set; }
    // An LBA1 map: the scene slot it takes in a game folder (LBA1's scenes can't be added to: LBA_GRI.HQR's entries after its 120 grids are
    // grid fragments); an LBA2 interior's: the game scene it was made or exported from, its library and look
    public int? Slot { get; set; }
    // An island: the name of its ILE and OBL in a game folder (eight letters at the most)
    public string? IslandFile { get; set; }
    public List<MapScene> Scenes { get; } = new();
    // The game scene numbers MAP.TXT gives its scenes (the engine plays them as those), or null: not given yet
    public List<int>? SceneNumbers { get; set; }
    // An interior's grid (LBA2: LBA_BKG.HQR's entry, its 34-byte header -- library, fragments, used blocks -- and all; LBA1: LBA_GRI.HQR's)
    public byte[]? Grid { get; set; }
    // Its block library: an LBA1 interior's (LBA_BLL.HQR's entry), an LBA2 interior's own (LBA_BKG.HQR's library entry, its brick numbers
    // Bricks's); null: an LBA2 interior's is the game's its grid's header names
    public byte[]? Library { get; set; }
    // An LBA2 interior's own bricks (an HQR, brick n its entry n), and its own grid fragments (an HQR); null: the game's
    public byte[]? Bricks { get; set; }
    public byte[]? Fragments { get; set; }
    // Of its own bricks, the one the engine never draws (LBA_BKG.HQR's header's ForbidenBrick), or null
    public int? ForbiddenBrick { get; set; }
    public byte[]? Ile { get; set; }
    public byte[]? Obl { get; set; }
    // An island's weather, or null: "snow" -- snow falls on it (the engine's SNOW.CPP; MAP.TXT's weather =, ISLANDS.TXT's weather lines)
    public string? Weather { get; set; }
    // An island's cliffs, or null: walls, as the game's islands' (but in a jump); "drop": they can be walked or ridden off, as LBA1's
    // ledges can (the engine's EXTFUNC.CPP DropLedgesHere; MAP.TXT's ledges =, ISLANDS.TXT's ledges lines; 2026-10-23)
    public string? Ledges { get; set; }
    // The script editor's comments on its scenes' scripts (CommentStore's JSON, a scene by its place in Scenes), or null: none. The engine
    // doesn't read it.
    public byte[]? Comments { get; set; }
    // Its own lines of dialogue (TEXT.TXT's UTF-8: MapText), or null: none -- its scenes say the island's
    public byte[]? Text { get; set; }
    // Its own characters (MapCharacters, numbered as the map's own), their bodies, animations and entity records, or null: none
    public byte[]? Characters { get; set; }
    public List<byte[]> CharacterBodies { get; set; } = new();
    public List<byte[]> CharacterAnims { get; set; } = new();
    public List<byte[]> CharacterEntities { get; set; } = new();

    public bool IsLba1 => Kind == MapKind.Lba1Interior;
    public string KindName => Kind switch { MapKind.Lba2Island => "LBA2 island", MapKind.Lba2Interior => "LBA2 interior", _ => "LBA1 interior" };
    public bool OwnBricks => Kind == MapKind.Lba2Interior && Library is not null && Bricks is not null;

    // The name its files have in the zip: an island's file name, else eight letters of its name.
    public string Stem => Kind == MapKind.Lba2Island && IslandFile is { Length: > 0 } f ? f.ToUpperInvariant() : FileNameFrom(Name, 8);

    // ---- writing
    public void Save(string path)
    {
        // (written beside it and swapped in: a map file is never left half written)
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, ToZip());
        File.Move(temp, path, overwrite: true);
    }

    public byte[] ToZip()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in Members())
            {
                var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                entry.LastWriteTime = DateTimeOffset.Now;
                using var write = entry.Open();
                write.Write(bytes);
            }
        }
        return stream.ToArray();
    }

    // The map with its scenes numbered `numbers` in its MAP.TXT (MapInstall.InstallInMapsFolder).
    public LbaMap Numbered(IReadOnlyList<int> numbers)
    {
        var copy = (LbaMap)MemberwiseClone();
        copy.SceneNumbers = numbers.ToList();
        return copy;
    }

    // Its files as the zip holds them, MAP.TXT first.
    public List<(string Name, byte[] Bytes)> Members()
    {
        var stem = Stem;
        var ls = IsLba1 ? ".LS1" : ".LS2";
        var files = new List<(string, byte[])>();
        var sceneFiles = new List<string>();
        for (var k = 0; k < Scenes.Count; k++)
        {
            var name = Scenes.Count == 1 ? stem + ls : $"{stem}_{k}{ls}";
            sceneFiles.Add(name);
            files.Add((name, Scenes[k].Record));
        }
        string? Put(string extension, byte[]? bytes)
        {
            if (bytes is null) return null;
            files.Add((stem + extension, bytes));
            return stem + extension;
        }
        var grid = Put(IsLba1 ? ".GR1" : ".GR2", Grid);
        var library = Put(IsLba1 ? ".BL1" : ".BL2", Library);
        var bricks = Put(".BRK", Bricks);
        var fragments = Put(".GRM", Fragments);
        var ile = Put(".ILE", Ile);
        var obl = Put(".OBL", Obl);
        var comments = Put("_COMMENTS.JSON", Comments);
        var text = Put(MapText.Suffix, Text);
        string? characters = null, characterBodies = null, characterAnims = null, characterEntities = null;
        if (Characters is not null)
        {
            characters = Put(LBAAssembler.Characters.MapCharacters.Suffix, Characters);
            characterBodies = Put("_BODIES.HQR", HqrFileOf(CharacterBodies));
            characterAnims = Put("_ANIMS.HQR", HqrFileOf(CharacterAnims));
            characterEntities = Put("_ENTITIES.HQR", HqrFileOf(CharacterEntities));
        }
        files.Insert(0, (ManifestName, Encoding.UTF8.GetBytes(ManifestText(sceneFiles, grid, library, bricks, fragments, ile, obl, comments, text) +
                                                              (characters is null ? "" : $"characters = {characters}\r\ncharacter bodies = {characterBodies}\r\ncharacter anims = {characterAnims}\r\ncharacter entities = {characterEntities}\r\n"))));
        return files;
    }

    private string ManifestText(List<string> sceneFiles, string? grid, string? library, string? bricks, string? fragments, string? ile, string? obl, string? comments, string? text)
    {
        static string Line(string text) => text.Replace("\r", " ").Replace("\n", " ").Trim();
        var numbers = SceneNumbers is { } given && given.Count == Scenes.Count ? given
            : IsLba1 ? new List<int> { Slot ?? 0 } : Enumerable.Range(MapInstall.FirstMapScene, Scenes.Count).ToList();
        var t = new StringBuilder();
        t.Append("# An LBA Assembler map. This file is a zip: any zip program opens it.\r\n");
        if (IsLba1)
            t.Append("# An LBA1 map: LBA Assembler's File > Install map into game puts it into its scene slot.\r\n");
        else
        {
            t.Append("# Put it in the game folder's MAPS folder and the engine plays its scenes as the game's scenes numbered below\r\n");
            t.Append("# (\"scene <number> = <file>\"). Change a number to move a scene; two maps can't have a scene numbered the same.\r\n");
            t.Append("# The scene lines' order is the map's own: a zone into scene 30000 + k goes to the k-th (from 0).\r\n");
            if (Kind == MapKind.Lba2Interior)
                t.Append("# library, bricks, fragments: the map's own; without them, the game's its grid's header names.\r\n");
        }
        t.Append($"format = {FormatVersion}\r\n");
        t.Append($"game = {(IsLba1 ? "lba1" : "lba2")}\r\n");
        t.Append($"kind = {(Kind == MapKind.Lba2Island ? "island" : "interior")}\r\n");
        t.Append($"name = {Line(Name)}\r\n");
        if (Description.Length > 0) t.Append($"description = {Line(Description)}\r\n");
        t.Append($"island = {IslandByte}\r\n");
        if (Slot is { } slot) t.Append($"slot = {slot}\r\n");
        for (var k = 0; k < sceneFiles.Count; k++) t.Append($"scene {numbers[k]} = {sceneFiles[k]}\r\n");
        if (grid is not null) t.Append($"grid = {grid}\r\n");
        if (library is not null) t.Append($"library = {library}\r\n");
        if (bricks is not null) t.Append($"bricks = {bricks}\r\n");
        if (fragments is not null) t.Append($"fragments = {fragments}\r\n");
        if (ForbiddenBrick is { } forbidden && bricks is not null) t.Append($"forbidden brick = {forbidden}\r\n");
        if (ile is not null) t.Append($"ile = {ile}\r\n");
        if (obl is not null) t.Append($"obl = {obl}\r\n");
        if (Weather is { Length: > 0 } weather) t.Append($"weather = {Line(weather)}\r\n");
        if (Ledges is { Length: > 0 } ledges) t.Append($"ledges = {Line(ledges)}\r\n");
        if (comments is not null) t.Append($"comments = {comments}\r\n");
        if (text is not null) t.Append($"text = {text}\r\n");
        t.Append("program = LBA Assembler\r\n");
        return t.ToString();
    }

    // An HQR of entries (stored), and back.
    private static byte[] HqrFileOf(List<byte[]> entries)
    {
        // (an empty one: its table, the one slot that is its end)
        var hqr = BitConverter.GetBytes(4);
        foreach (var e in entries) hqr = HqrWriter.AppendEntry(hqr, HqrWriter.StoredEntry(e));
        return hqr;
    }
    private static List<byte[]> EntriesOf(byte[]? hqr)
    {
        if (hqr is null) return new List<byte[]>();
        var archive = HqrArchive.FromBytes(hqr);
        return Enumerable.Range(0, archive.Slots - 1).Select(archive.Read).ToList();   // (Slots: Count is four times the entries)
    }

    // ---- reading
    public static LbaMap Load(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            ZipArchiveEntry? Find(string name) =>
                zip.GetEntry(name) ?? zip.Entries.FirstOrDefault(e => e.FullName.Equals(name, StringComparison.OrdinalIgnoreCase));
            byte[]? Get(string? name)
            {
                if (name is null || Find(name) is not { } entry) return null;
                using var stream = entry.Open();
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                return copy.ToArray();
            }
            LbaMap map;
            if (Get(ManifestName) is { } text) map = FromManifest(Encoding.UTF8.GetString(text), Get);
            else if (Get("map.json") is { } json) map = FromJson(json, Get);
            else throw new InvalidDataException($"It has no {ManifestName}: it isn't an LBA Assembler map.");
            map.Check();
            return map;
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or IOException or FormatException)
        {
            throw new InvalidDataException($"{Path.GetFileName(path)} can't be read as a map: {error.Message}", error);
        }
    }

    // MAP.TXT: "key = value" lines, '#' a comment; scenes "scene <number> = <file>" in the map's own order.
    private static LbaMap FromManifest(string text, Func<string?, byte[]?> get)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var scenes = new List<(int Number, string File)>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=');
            if (line.Length == 0 || line[0] == '#' || eq < 0) continue;
            var key = System.Text.RegularExpressions.Regex.Replace(line[..eq].Trim(), @"\s+", " ");
            var value = line[(eq + 1)..].Trim();
            if (key.StartsWith("scene ", StringComparison.OrdinalIgnoreCase))
                scenes.Add((int.TryParse(key[6..].Trim(), out var n) ? n : -1, value));
            else values[key] = value;
        }
        string? Value(string key) => values.TryGetValue(key, out var v) && v.Length > 0 && !v.Equals("game", StringComparison.OrdinalIgnoreCase) ? v : null;
        int? Number(string key) => Value(key) is { } v ? int.Parse(v) : null;
        if (Number("format") is { } format && format > FormatVersion)
            throw new InvalidDataException($"It is a newer kind of map (format {format}) than this version of the program reads ({FormatVersion}).");
        var lba1 = string.Equals(Value("game"), "lba1", StringComparison.OrdinalIgnoreCase);
        var island = string.Equals(Value("kind"), "island", StringComparison.OrdinalIgnoreCase);
        var map = new LbaMap
        {
            Kind = lba1 ? MapKind.Lba1Interior : island ? MapKind.Lba2Island : MapKind.Lba2Interior,
            Name = Value("name") ?? "Map", Description = Value("description") ?? "", IslandByte = Number("island") ?? 0, Slot = Number("slot"),
            ForbiddenBrick = Number("forbidden brick"),
        };
        byte[]? Part(string key)
        {
            if (Value(key) is not { } file) return null;
            return get(file) ?? throw new InvalidDataException($"Its {key} {file} isn't in it.");
        }
        map.Grid = Part("grid"); map.Library = Part("library"); map.Bricks = Part("bricks"); map.Fragments = Part("fragments");
        map.Ile = Part("ile"); map.Obl = Part("obl");
        map.Comments = Part("comments");
        map.Text = Part("text");
        map.Characters = Part("characters");
        if (map.Characters is not null)
        {
            map.CharacterBodies = EntriesOf(Part("character bodies"));
            map.CharacterAnims = EntriesOf(Part("character anims"));
            map.CharacterEntities = EntriesOf(Part("character entities"));
        }
        map.Weather = Value("weather");
        map.Ledges = Value("ledges");
        if (Value("ile") is { } ile) map.IslandFile = Path.GetFileNameWithoutExtension(ile).ToUpperInvariant();
        foreach (var (_, file) in scenes)
        {
            var record = get(file) ?? throw new InvalidDataException($"Its scene {file} isn't in it.");
            map.Scenes.Add(map.Kind == MapKind.Lba2Island && record.Length > 2 ? new MapScene(record, record[1], record[2]) : new MapScene(record));
        }
        if (scenes.All(s => s.Number >= 0)) map.SceneNumbers = scenes.Select(s => s.Number).ToList();
        if (map.Kind == MapKind.Lba1Interior && map.Slot is null && map.SceneNumbers is [var slot]) map.Slot = slot;
        return map;
    }

    // ---- the first version: map.json
    private sealed class Manifest
    {
        public int Format { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))] public MapKind Kind { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int IslandByte { get; set; }
        public int? Slot { get; set; }
        public string? IslandFile { get; set; }
        public List<ManifestScene> Scenes { get; set; } = new();
    }
    private sealed class ManifestScene
    {
        public string File { get; set; } = "";
        public int CubeX { get; set; }
        public int CubeZ { get; set; }
    }

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static LbaMap FromJson(byte[] json, Func<string?, byte[]?> get)
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(json, Json) ?? throw new InvalidDataException("Its map.json is empty.");
        if (manifest.Format > 1) throw new InvalidDataException($"Its map.json is of a newer kind (format {manifest.Format}).");
        var map = new LbaMap
        {
            Kind = manifest.Kind, Name = manifest.Name, Description = manifest.Description, IslandByte = manifest.IslandByte, Slot = manifest.Slot,
            IslandFile = manifest.IslandFile, Grid = get("grid.bin"), Library = get("library.bin"), Ile = get("island.ile"), Obl = get("island.obl"),
        };
        foreach (var s in manifest.Scenes)
            map.Scenes.Add(new MapScene(get(s.File) ?? throw new InvalidDataException($"Its scene {s.File} is missing."), s.CubeX, s.CubeZ));
        return map;
    }

    // What a map of its kind must have.
    public void Check()
    {
        if (Scenes.Count == 0) throw new InvalidDataException("It has no scene.");
        switch (Kind)
        {
            case MapKind.Lba2Island:
                if (Ile is null || Obl is null) throw new InvalidDataException("An island map needs its ILE and its OBL.");
                if (!ValidIslandFile(IslandFile)) throw new InvalidDataException($"An island map's file name \"{IslandFile}\" isn't a name the game can load (eight letters or digits at the most).");
                break;
            case MapKind.Lba2Interior:
                if (Grid is null) throw new InvalidDataException("An interior map needs its grid.");
                if (Grid.Length < 34) throw new InvalidDataException("Its grid is too short.");
                if ((Library is null) != (Bricks is null)) throw new InvalidDataException("An interior map with a block library of its own needs its bricks too, and the other way round.");
                break;
            case MapKind.Lba1Interior:
                if (Grid is null || Library is null) throw new InvalidDataException("An LBA1 map needs its grid and its block library.");
                if (Slot is null) throw new InvalidDataException("An LBA1 map needs the scene slot it takes.");
                break;
        }
    }

    // An island's file name the game can load: one to eight letters, digits or underscores, not one of the game's own islands' (ILE files a
    // game folder has stay its own).
    public static bool ValidIslandFile(string? name) =>
        name is { Length: >= 1 and <= 8 } && name.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_') && !name.StartsWith('_');

    // A file name from the map's name (for a new island's ILE, or a map file's name).
    public static string FileNameFrom(string name, int most)
    {
        var letters = new string(name.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
        if (letters.Length == 0) letters = "NEWMAP";
        return letters.Length > most ? letters[..most] : letters;
    }
}
