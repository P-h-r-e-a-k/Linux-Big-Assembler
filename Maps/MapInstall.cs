using System.Buffers.Binary;
using System.IO;
using LBAAssembler.Grids;
using LBAAssembler.Scenes;

using LBAAssembler.Characters;

namespace LBAAssembler.Maps;

// Where a map is in a game folder once installed there: its scenes' numbers (its own scenes in order), an LBA2 interior's grid, an island's
// file name.
internal sealed record InstalledMap(LbaMap Map, string Directory, IReadOnlyList<int> Scenes, int? Grid)
{
    public int FirstScene => Scenes[0];
}

// A map put into a game folder, and read back out of one (File > Open map's workspace, File > Install map into game, File > Export map).
//   - LBA2: its scenes appended to SCENE.HQR from FirstMapScene on (past the game's and the race tracks' scenes: SCENE.HQR is padded with
//     empty entries up to them), named in the folder's SCENE.HQD; an interior's grid added to LBA_BKG.HQR after its last grid (the grid
//     fragments, block libraries and bricks after it move up an entry, entry 0 saying where they start) and named in its scene-to-grid
//     table; an island's ILE and OBL written under its file name, and ISLANDS.TXT telling the engine its scenes are drawn from that file
//     (EXTFUNC.CPP SceneIslandFile) and the editor's renderer which island's look it has (RENDERER_API.CPP).
//   - LBA1: its scene, grid and block library into its slot (LBA1's scenes can't be added to).
// Every file is written beside itself and swapped in (FileTransaction): a workspace's files are hard links to the game folder's until written,
// and writing one in place would write the game's own.
internal static class MapInstall
{
    public const int FirstMapScene = 400;
    // LBA_BKG.HQR's scene-to-grid table holds this many scenes in LBA Assembler's engine (DEFINES.H MAX_GRID_SCENES; 256 retail)
    public const int MaxGridScenes = 1024;
    public const string IslandsFile = "ISLANDS.TXT";

    public static InstalledMap Install(LbaMap map, string directory)
    {
        map.Check();
        // (its characters first: its scenes' actors of them get their numbers here)
        var entities = map.IsLba1 ? new List<int>() : PutCharacters(map, directory);
        var installed = map.Kind switch
        {
            MapKind.Lba1Interior => InstallLba1(map, directory),
            MapKind.Lba2Interior => InstallLba2Interior(map, directory, entities),
            _ => InstallLba2Island(map, directory, entities),
        };
        PutComments(map, directory, installed.Scenes);
        PutText(map, directory);
        return installed;
    }

    // ---- the script editor's comments on the map's scripts: the folder's comments file (beside SCENE.HQR, ScriptSession's) under the numbers
    // the scenes have there, and back under their places in the map
    // ---- an installed island's settings changed (File > Map settings): ISLANDS.TXT's lines for it, the engine reads them there
    public static void PutIslandSettings(LbaMap map, string directory)
    {
        if (map.Kind != MapKind.Lba2Island || map.IslandFile is not { Length: > 0 } file) return;
        var name = file.ToUpperInvariant();
        var islands = IslandFiles.Load(directory);
        if (map.Weather is { Length: > 0 } weather) islands.Weather[name] = weather; else islands.Weather.Remove(name);
        if (map.Ledges is { Length: > 0 } ledges) islands.Ledges[name] = ledges; else islands.Ledges.Remove(name);
        new FileTransaction().Write(Path.Combine(directory, IslandsFile), System.Text.Encoding.ASCII.GetBytes(islands.Text())).Commit();
    }

    // ---- the map's own lines of dialogue: <stem>_TEXT.TXT beside its files (an island's, the engine reads there: MAPPACK.CPP MapPack_Text)
    public static string TextPath(LbaMap map, string directory) => Path.Combine(directory, map.Stem + MapText.Suffix);

    private static void PutText(LbaMap map, string directory)
    {
        var path = TextPath(map, directory);
        if (map.Text is { } text) new FileTransaction().Write(path, text).Commit();
        else if (File.Exists(path)) File.Delete(path);
    }

    private static string CommentsPath(string directory) => Path.Combine(directory, "SCENE.HQR") + ".comments.json";

    private static void PutComments(LbaMap map, string directory, IReadOnlyList<int> numbers)
    {
        var path = CommentsPath(directory);
        var store = LbaScript.CommentStore.Load(path);
        // (a scene installed over is the map's now: the comments it had go, the map's take their place)
        foreach (var (scene, actor, kind, _) in store.Entries.ToList())
            if (numbers.Contains(scene)) store.Set(scene, actor, kind, null);
        if (map.Comments is { } json)
            foreach (var (k, actor, kind, set) in LbaScript.CommentStore.FromJson(System.Text.Encoding.UTF8.GetString(json), path).Entries.ToList())
                if (k >= 0 && k < numbers.Count) store.Set(numbers[k], actor, kind, set);
        if (store.LoadError is null) store.Save();      // (written beside and moved over: a workspace's file stays clear of the game's)
    }

    private static byte[]? TakeComments(InstalledMap at)
    {
        var path = CommentsPath(at.Directory);
        if (!File.Exists(path)) return null;
        var folder = LbaScript.CommentStore.Load(path);
        var own = LbaScript.CommentStore.FromJson("{}", path);
        foreach (var (scene, actor, kind, set) in folder.Entries)
            if (at.Scenes.ToList().IndexOf(scene) is var k and >= 0) own.Set(k, actor, kind, set);
        return own.Count == 0 ? null : System.Text.Encoding.UTF8.GetBytes(own.ToJson());
    }

    // ---- LBA1: into its slot
    private static InstalledMap InstallLba1(LbaMap map, string directory)
    {
        var slot = map.Slot!.Value;
        var record = WithSceneNumbers(map.Scenes[0].Record, SceneGame.Lba1, new[] { slot });
        var transaction = new FileTransaction();
        foreach (var (file, bytes) in new[] { ("SCENE.HQR", record), ("LBA_GRI.HQR", map.Grid!), ("LBA_BLL.HQR", map.Library!) })
        {
            var path = Path.Combine(directory, file);
            var hqr = HqrFile.Parse(File.ReadAllBytes(path));
            if (slot >= hqr.Count) throw new SceneEditException($"{file} has no entry {slot}: LBA1's scenes are 0 to {hqr.Count - 1}.");
            hqr.SetStored(slot, bytes);
            transaction.Write(path, hqr.ToBytes());
        }
        transaction.Commit();
        return new InstalledMap(map, directory, new[] { slot }, null);
    }

    // ---- LBA2 interior: a new scene and a new grid
    private static InstalledMap InstallLba2Interior(LbaMap map, string directory, IReadOnlyList<int> entities)
    {
        var scenePath = Path.Combine(directory, "SCENE.HQR");
        var scenes = HqrFile.Parse(File.ReadAllBytes(scenePath));
        var bkgPath = Path.Combine(directory, "LBA_BKG.HQR");
        var bkg = HqrFile.Parse(File.ReadAllBytes(bkgPath));
        var beforeGrid = InstalledBefore(map, directory) is [var was] ? new Lba2GridBackend(directory).GridOfScene(was) : null;
        // (its own block library, bricks and fragments first -- MapBricks: they move the entries after them; the grid's header names them)
        var (library, fragments) = MapBricks.Install(map, bkg);
        var gridBytes = (byte[])map.Grid!.Clone();
        gridBytes[0] = (byte)library; gridBytes[1] = (byte)fragments;
        var header = bkg.Read(0);
        int gri = U16(header, 0), grm = U16(header, 2), bll = U16(header, 4), brk = U16(header, 6), maxBrk = U16(header, 8);

        // (installed before -- a scene of the maps' with its name: it and its grid replaced, where they are)
        if (InstalledBefore(map, directory) is [var before] && beforeGrid is not null)
        {
            bkg.SetStored(gri + beforeGrid.Value, gridBytes);
            if (header.Length >= 16 && BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12)) < (uint)gridBytes.Length)
            { BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), (uint)gridBytes.Length); bkg.SetStored(0, header); }
            PutScene(scenes, before, WithSceneNumbers(map.Scenes[0].Record, SceneGame.Lba2, new[] { before }, entities));
            new FileTransaction().Write(bkgPath, bkg.ToBytes()).Write(scenePath, scenes.ToBytes()).Commit();
            return new InstalledMap(map, directory, new[] { before }, beforeGrid);
        }

        var number = FreeScenes(scenes, 1, MapsFolderScenes(directory))[0];
        if (number >= MaxGridScenes) throw new SceneEditException($"The game's scene-to-grid table holds {MaxGridScenes} scenes, and the first free one is {number}.");
        var grid = grm - gri;
        if (grid > 255) throw new SceneEditException("LBA_BKG.HQR has 256 grids already: the game can't name another.");
        // (the type the scene-to-grid table gives the scene the map was made from, else an interior's)
        var table = bkg.Read(brk + maxBrk);
        var type = map.Slot is { } from && from * 2 + 1 < table.Length ? table[from * 2] : (byte)1;
        // the grid after the last one; what follows it one entry on
        bkg.InsertAt(grm, gridBytes);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2), (ushort)(grm + 1));
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), (ushort)(bll + 1));
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), (ushort)(brk + 1));
        // (the biggest grid's size: the engine's buffer for one)
        if (header.Length >= 16 && BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12)) < (uint)gridBytes.Length)
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), (uint)gridBytes.Length);
        bkg.SetStored(0, header);
        if (table.Length < (number + 1) * 2) Array.Resize(ref table, (number + 1) * 2);
        table[number * 2] = type; table[number * 2 + 1] = (byte)grid;
        bkg.SetStored(brk + 1 + maxBrk, table);

        PutScene(scenes, number, WithSceneNumbers(map.Scenes[0].Record, SceneGame.Lba2, new[] { number }, entities));
        var transaction = new FileTransaction().Write(bkgPath, bkg.ToBytes()).Write(scenePath, scenes.ToBytes());
        Name(transaction, directory, new[] { (number, map.Name) });
        transaction.Commit();
        return new InstalledMap(map, directory, new[] { number }, grid);
    }

    // ---- LBA2 island: its files, its scenes, ISLANDS.TXT
    private static InstalledMap InstallLba2Island(LbaMap map, string directory, IReadOnlyList<int> entities)
    {
        var name = map.IslandFile!.ToUpperInvariant();
        var scenePath = Path.Combine(directory, "SCENE.HQR");
        var scenes = HqrFile.Parse(File.ReadAllBytes(scenePath));
        var islands = IslandFiles.Load(directory);
        // (installed before -- scenes ISLANDS.TXT draws on its file: those again if as many, else they go and it has new ones)
        var before = islands.Scenes.Where(s => s.Value == name).Select(s => s.Key).Order().ToList();
        List<int> numbers;
        if (before.Count == map.Scenes.Count) numbers = before;
        else
        {
            foreach (var n in before) { islands.Scenes.Remove(n); if (n + 1 < scenes.Count) scenes.Clear(n + 1); }
            numbers = FreeScenes(scenes, map.Scenes.Count, MapsFolderScenes(directory));
        }
        for (var k = 0; k < map.Scenes.Count; k++) PutScene(scenes, numbers[k], WithSceneNumbers(map.Scenes[k].Record, SceneGame.Lba2, numbers, entities));
        islands.Look[name] = map.IslandByte;
        if (map.Weather is { Length: > 0 } weather) islands.Weather[name] = weather; else islands.Weather.Remove(name);
        if (map.Ledges is { Length: > 0 } ledges) islands.Ledges[name] = ledges; else islands.Ledges.Remove(name);
        foreach (var n in numbers) islands.Scenes[n] = name;
        var transaction = new FileTransaction()
            .Write(Path.Combine(directory, name + ".ILE"), map.Ile!)
            .Write(Path.Combine(directory, name + ".OBL"), map.Obl!)
            .Write(scenePath, scenes.ToBytes())
            .Write(Path.Combine(directory, IslandsFile), System.Text.Encoding.ASCII.GetBytes(islands.Text()));
        Name(transaction, directory, numbers.Select((n, k) => (n, $"{map.Name}: cube ({map.Scenes[k].CubeX},{map.Scenes[k].CubeZ})")).ToList());
        transaction.Commit();
        return new InstalledMap(map, directory, numbers, null);
    }

    // The scenes an install of the map would replace in the folder, or null (it is added): an island's, the scenes ISLANDS.TXT draws on its
    // file; an LBA2 interior's, a scene of the maps' (from FirstMapScene on) named as it is; an LBA1 map's, its slot.
    public static IReadOnlyList<int>? InstalledBefore(LbaMap map, string directory)
    {
        switch (map.Kind)
        {
            case MapKind.Lba1Interior: return new[] { map.Slot!.Value };
            case MapKind.Lba2Island:
                var file = map.IslandFile!.ToUpperInvariant();
                var scenes = IslandFiles.Load(directory).Scenes.Where(s => s.Value == file).Select(s => s.Key).Order().ToList();
                return scenes.Count > 0 ? scenes : null;
            default:
                var sidecar = Path.Combine(directory, HqdWriter.SidecarName("SCENE.HQR"));
                if (!File.Exists(sidecar)) return null;
                var lines = File.ReadAllLines(sidecar, System.Text.Encoding.Latin1);
                var store = new SceneStore(SceneGame.Lba2, directory);
                // (SCENE.HQD: line 0 its header, line N + 1 entry N's, entry N + 1 scene N's)
                for (var n = FirstMapScene; n + 2 < lines.Length && n < MaxGridScenes; n++)
                    if (lines[n + 2] == map.Name && store.SceneExists(n) && store.LoadRecord(n) is { Length: > 5 } r && r[5] == 0) return new[] { n };
                return null;
        }
    }

    // ---- read back: the map as the folder now has it (its scenes, grid, library, island files), with its own scenes named as its own again
    public static LbaMap Capture(InstalledMap at)
    {
        var map = at.Map;
        var captured = new LbaMap { Kind = map.Kind, Name = map.Name, Description = map.Description, IslandByte = map.IslandByte, Slot = map.Slot, IslandFile = map.IslandFile, SceneNumbers = map.SceneNumbers,
            Weather = map.Weather, Ledges = map.Ledges };
        captured.Comments = TakeComments(at);
        if (!map.IsLba1) TakeCharacters(captured, at.Directory);
        var ownEntities = map.IsLba1 ? null : MapCharacters.Load(MapCharacters.SidecarPath(at.Directory, map.Stem)).Select(c => c.Entity).ToList();
        var textPath = TextPath(map, at.Directory);
        captured.Text = File.Exists(textPath) ? File.ReadAllBytes(textPath) : null;
        var game = map.IsLba1 ? SceneGame.Lba1 : SceneGame.Lba2;
        var store = new SceneStore(game, at.Directory);
        for (var k = 0; k < at.Scenes.Count; k++)
            captured.Scenes.Add(new MapScene(WithLocalScenes(store.LoadRecord(at.Scenes[k]), game, at.Scenes, ownEntities), map.Scenes[k].CubeX, map.Scenes[k].CubeZ));
        switch (map.Kind)
        {
            case MapKind.Lba1Interior:
                captured.Grid = store.LoadGrid(at.FirstScene);
                captured.Library = store.LoadLibrary(at.FirstScene);
                break;
            case MapKind.Lba2Interior:
                var bkg = HqrFile.Parse(File.ReadAllBytes(Path.Combine(at.Directory, "LBA_BKG.HQR")));
                captured.Grid = bkg.Read(U16(bkg.Read(0), 0) + at.Grid!.Value);
                // (its own block library, bricks and fragments: as the folder has them now)
                MapBricks.Capture(captured, map, bkg);
                break;
            default:
                captured.Ile = File.ReadAllBytes(Path.Combine(at.Directory, map.IslandFile!.ToUpperInvariant() + ".ILE"));
                captured.Obl = File.ReadAllBytes(Path.Combine(at.Directory, map.IslandFile!.ToUpperInvariant() + ".OBL"));
                break;
        }
        return captured;
    }

    // ---- export: a map of the game's own -- an interior scene, or an island with its outside scenes -- out of a game folder
    public static LbaMap ExportLba2Interior(string directory, int scene, string name)
    {
        var store = new SceneStore(SceneGame.Lba2, directory);
        var record = store.LoadRecord(scene);
        if (record.Length < 6 || record[5] != 0) throw new SceneEditException($"Scene {scene} is an island scene, not an interior.");
        var backend = new Lba2GridBackend(directory);
        var grid = backend.GridOfScene(scene) ?? throw new SceneEditException($"Scene {scene} has no interior grid.");
        var map = new LbaMap { Kind = MapKind.Lba2Interior, Name = name, IslandByte = record[0], Slot = scene, Grid = backend.RawGrid(grid) };
        map.Scenes.Add(new MapScene(WithLocalScenes(record, SceneGame.Lba2, new[] { scene })));
        // (with its block library, bricks and grid fragments: a map of its own, whatever the game folder it goes into)
        MapBricks.Package(map, directory);
        return map;
    }

    public static LbaMap ExportLba1Interior(string directory, int scene, string name)
    {
        var store = new SceneStore(SceneGame.Lba1, directory);
        var record = store.LoadRecord(scene);
        var map = new LbaMap { Kind = MapKind.Lba1Interior, Name = name, IslandByte = record[0], Slot = scene, Grid = store.LoadGrid(scene), Library = store.LoadLibrary(scene) };
        map.Scenes.Add(new MapScene(WithLocalScenes(record, SceneGame.Lba1, new[] { scene })));
        return map;
    }

    // An island and its outside scenes: those ISLANDS.TXT draws on it, or -- one of the game's own -- those carrying its island number
    // that ISLANDS.TXT doesn't take elsewhere.
    public static LbaMap ExportLba2Island(string directory, string islandFile, string name)
    {
        var file = islandFile.ToUpperInvariant();
        var islands = IslandFiles.Load(directory);
        var own = islands.Scenes.Where(s => s.Value.Equals(file, StringComparison.OrdinalIgnoreCase)).Select(s => s.Key).ToList();
        var islandByte = islands.Look.TryGetValue(file, out var look) ? look
            : MapNew.GameIslands.FirstOrDefault(g => g.File == file) is { File: not null } g ? g.Byte : -1;
        var store = new SceneStore(SceneGame.Lba2, directory);
        var numbers = new List<int>();
        for (var n = 0; n < store.SceneCount; n++)
        {
            if (!store.SceneExists(n)) continue;
            var record = store.LoadRecord(n);
            if (record.Length < 6 || record[5] != 1) continue;
            if (own.Contains(n) || own.Count == 0 && record[0] == islandByte && !islands.Scenes.ContainsKey(n)) numbers.Add(n);
        }
        var map = new LbaMap
        {
            Kind = MapKind.Lba2Island, Name = name, IslandByte = Math.Max(0, islandByte), IslandFile = LbaMap.ValidIslandFile(file) ? file : LbaMap.FileNameFrom(name, 8),
            Ile = File.ReadAllBytes(Path.Combine(directory, file + ".ILE")), Obl = File.ReadAllBytes(Path.Combine(directory, file + ".OBL")),
        };
        foreach (var n in numbers)
        {
            var record = store.LoadRecord(n);
            map.Scenes.Add(new MapScene(WithLocalScenes(record, SceneGame.Lba2, numbers), record[1], record[2]));
        }
        if (map.Scenes.Count == 0) throw new SceneEditException($"No outside scene is on {file}: an island map needs one at least.");
        return map;
    }

    // ---- the game folder's MAPS folder: the engine plays a map from there (MAPPACK.CPP), the game's own files untouched
    public const string MapsFolderName = "MAPS";

    // The map written into `directory`'s MAPS folder as `fileName`, its scenes numbered as no scene of SCENE.HQR's or of another map's there
    // is -- the numbers of the map it replaces there (the same file name) kept. The file it replaces keeps a .bak (the engine reads only
    // .lbamap files). Returns where it went and its scenes' numbers.
    public static (string Path, IReadOnlyList<int> Scenes) InstallInMapsFolder(LbaMap map, string directory, string fileName)
    {
        if (map.IsLba1) throw new SceneEditException("The engine's MAPS folder is LBA2's: an LBA1 map goes into its scene slot.");
        map.Check();
        var folder = Path.Combine(directory, MapsFolderName);
        var path = Path.Combine(folder, Path.GetFileNameWithoutExtension(fileName) + LbaMap.Extension);
        var scenes = HqrFile.Parse(File.ReadAllBytes(Path.Combine(directory, "SCENE.HQR")));
        var others = MapsFolderScenes(directory, except: path);
        List<int> numbers;
        if (File.Exists(path) && ReadNumbers(path) is { } before && before.Count == map.Scenes.Count && before.All(n => !others.Contains(n) && n < MaxGridScenes))
            numbers = before;
        else numbers = FreeScenes(scenes, map.Scenes.Count, others);
        if (numbers[^1] >= MaxGridScenes) throw new SceneEditException($"The engine plays scenes up to {MaxGridScenes - 1}, and the map's would go to {numbers[^1]}.");
        var copy = map.Numbered(numbers);
        Directory.CreateDirectory(folder);
        new FileTransaction().Write(path, copy.ToZip()).Commit();
        return (path, numbers);
    }

    // The scene numbers the maps in `directory`'s MAPS folder take (but `except`'s).
    public static HashSet<int> MapsFolderScenes(string directory, string? except = null)
    {
        var taken = new HashSet<int>();
        var folder = Path.Combine(directory, MapsFolderName);
        if (!Directory.Exists(folder)) return taken;
        foreach (var file in Directory.EnumerateFiles(folder, "*" + LbaMap.Extension))
            if (except is null || !string.Equals(Path.GetFullPath(file), Path.GetFullPath(except), StringComparison.OrdinalIgnoreCase))
                if (ReadNumbers(file) is { } numbers) taken.UnionWith(numbers);
        return taken;
    }

    private static List<int>? ReadNumbers(string file)
    {
        try { return LbaMap.Load(file).SceneNumbers; }
        catch (InvalidDataException) { return null; }
    }

    // ---- scene numbers
    // `count` scene numbers SCENE.HQR has no scene at (nor a map of the MAPS folder: `taken`), from FirstMapScene on, together where it can.
    private static List<int> FreeScenes(HqrFile scenes, int count, IReadOnlySet<int>? taken = null)
    {
        bool Free(int n) => (n + 1 >= scenes.Count || scenes.IsEmpty(n + 1)) && taken?.Contains(n) != true;
        for (var first = FirstMapScene; ; first++)
            if (Enumerable.Range(first, count).All(Free)) return Enumerable.Range(first, count).ToList();
    }

    private static void PutScene(HqrFile scenes, int number, byte[] record)
    {
        var entry = number + 1;
        while (scenes.Count <= entry) scenes.Slots.Add(new HqrFile.Slot());
        scenes.SetStored(entry, record);
        // (entry 0: the biggest scene's size, the engine's buffer for one)
        var largest = BinaryPrimitives.ReadInt32LittleEndian(scenes.Read(0));
        if (record.Length > largest) { var size = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(size, record.Length); scenes.SetStored(0, size); }
    }

    // A scene record with its zones into the map's own scenes (LbaMap.LocalScene + k) given the numbers they have.
    private static byte[] WithSceneNumbers(byte[] record, SceneGame game, IReadOnlyList<int> numbers, IReadOnlyList<int>? entities = null)
    {
        var model = SceneSerializer.Parse(game, record);
        var changed = false;
        foreach (var zone in model.Zones.Where(z => z.Type == 0 && z.Num >= LbaMap.LocalScene && z.Num - LbaMap.LocalScene < numbers.Count))
        { zone.Num = numbers[zone.Num - LbaMap.LocalScene]; changed = true; }
        // (actors of the map's own characters: their entities as installed)
        if (entities is not null)
            foreach (var actor in model.Actors.Where(a => a.Entity >= LbaMap.LocalEntity && a.Entity - LbaMap.LocalEntity < entities.Count))
            { actor.Entity = entities[actor.Entity - LbaMap.LocalEntity]; changed = true; }
        return changed ? SceneSerializer.Write(model) : record;
    }

    // ... and back: zones into those numbers named as the map's own scenes.
    private static byte[] WithLocalScenes(byte[] record, SceneGame game, IReadOnlyList<int> numbers, IReadOnlyList<int>? entities = null)
    {
        if (game == SceneGame.Lba1) return record;
        var model = SceneSerializer.Parse(game, record);
        var changed = false;
        foreach (var zone in model.Zones.Where(z => z.Type == 0))
        {
            var k = numbers.ToList().IndexOf(zone.Num);
            if (k < 0) continue;
            zone.Num = LbaMap.LocalScene + k; changed = true;
        }
        if (entities is not null)
            foreach (var actor in model.Actors)
            {
                var k = entities.ToList().IndexOf(actor.Entity);
                if (k < 0) continue;
                actor.Entity = LbaMap.LocalEntity + k; changed = true;
            }
        return changed ? SceneSerializer.Write(model) : record;
    }

    // ---- the map's own characters (MapCharacters): into the folder -- each as the character it was there before (an install of the
    // map again: the same entity, its entries reused) or a new one -- and back out, numbered as the map's own
    private static List<int> PutCharacters(LbaMap map, string directory)
    {
        var entities = new List<int>();
        var sidecar = MapCharacters.SidecarPath(directory, map.Stem);
        if (map.Characters is null) return entities;
        var own = MapCharacters.Parse(System.Text.Encoding.UTF8.GetString(map.Characters));
        var there = MapCharacters.Load(sidecar);
        for (var k = 0; k < own.Count; k++)
        {
            var c = own[k];
            var record = k < map.CharacterEntities.Count ? map.CharacterEntities[k] : Array.Empty<byte>();
            var boxes = MapCharacters.BodyBoxes(record);
            var bodies = c.Bodies.Select(b => new MapCharacters.MadeBody(b.Generic, b.Name, map.CharacterBodies[b.Entry],
                boxes.TryGetValue(b.Generic, out var box) ? box : ((short)-256, (short)0, (short)-256, (short)256, (short)1500, (short)256))).ToList();
            var anims = c.Anims.Select(a => new MapCharacters.MadeAnim(a.Generic, a.Name, map.CharacterAnims[a.Entry])).ToList();
            var put = MapCharacters.Put(directory, sidecar, k < there.Count ? there[k].Entity : -1, c.Name, bodies, anims);
            there = MapCharacters.Load(sidecar);
            entities.Add(put.Entity);
        }
        return entities;
    }

    private static void TakeCharacters(LbaMap captured, string directory)
    {
        var characters = MapCharacters.Load(MapCharacters.SidecarPath(directory, captured.Stem));
        if (characters.Count == 0) return;
        var bodyHqr = HqrArchive.Open(Path.Combine(directory, "BODY.HQR"));
        var animHqr = HqrArchive.Open(Path.Combine(directory, "ANIM.HQR"));
        var table = HqrArchive.Open(Path.Combine(directory, "RESS.HQR")).Read(44);
        var local = new List<MapCharacters.Character>();
        foreach (var c in characters)
        {
            var l = new MapCharacters.Character { Entity = local.Count, Name = c.Name };
            var bodyOf = new Dictionary<int, int>(); var animOf = new Dictionary<int, int>();
            foreach (var b in c.Bodies) { bodyOf[b.Entry] = captured.CharacterBodies.Count; l.Bodies.Add(b with { Entry = captured.CharacterBodies.Count }); captured.CharacterBodies.Add(bodyHqr.Read(b.Entry)); }
            foreach (var a in c.Anims) { animOf[a.Entry] = captured.CharacterAnims.Count; l.Anims.Add(a with { Entry = captured.CharacterAnims.Count }); captured.CharacterAnims.Add(animHqr.Read(a.Entry)); }
            // (its record's entries the map's own: the engine numbers them after the game folder's it plays in -- MAPPACK.CPP)
            captured.CharacterEntities.Add(MapCharacters.Renumber(MapCharacters.EntityOf(table, c.Entity), e => bodyOf.GetValueOrDefault(e, 0), e => animOf.GetValueOrDefault(e, 0)));
            local.Add(l);
        }
        captured.Characters = System.Text.Encoding.UTF8.GetBytes(MapCharacters.Text(local));
    }

    // The scenes' names in the folder's SCENE.HQD (the editor's scene list shows them).
    private static void Name(FileTransaction transaction, string directory, IReadOnlyList<(int Scene, string Name)> names)
    {
        string? content = null;
        foreach (var (scene, name) in names)
            content = HqdWriter.Describe(directory, "SCENE.HQR", SceneGame.Lba2, scene + 1, name, content);
        if (content is not null) transaction.Write(Path.Combine(directory, HqdWriter.SidecarName("SCENE.HQR")), System.Text.Encoding.Latin1.GetBytes(content));
    }

    private static int U16(byte[] b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));
}

// ISLANDS.TXT in a game folder: which scenes are drawn on an island file of their own ("scene <number> <file>", EXTFUNC.CPP SceneIslandFile)
// and which island's look -- sky, palette -- each such file has ("island <file> <island number>", RENDERER_API.CPP and the editor's
// palette).
internal sealed class IslandFiles
{
    public SortedDictionary<int, string> Scenes { get; } = new();
    public SortedDictionary<string, int> Look { get; } = new(StringComparer.OrdinalIgnoreCase);
    // ("weather <file> snow": snow falls on it -- EXTFUNC.CPP SceneSnows)
    public SortedDictionary<string, string> Weather { get; } = new(StringComparer.OrdinalIgnoreCase);
    // ("ledges <file> drop": its cliffs can be walked off -- EXTFUNC.CPP SceneDropLedges)
    public SortedDictionary<string, string> Ledges { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static IslandFiles Load(string directory)
    {
        var files = new IslandFiles();
        var path = Path.Combine(directory, MapInstall.IslandsFile);
        if (!File.Exists(path)) return files;
        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || parts[0].StartsWith('#')) continue;
            if (parts[0] == "scene" && int.TryParse(parts[1], out var scene)) files.Scenes[scene] = parts[2].ToUpperInvariant();
            else if (parts[0] == "island" && int.TryParse(parts[2], out var look)) files.Look[parts[1].ToUpperInvariant()] = look;
            else if (parts[0] == "weather") files.Weather[parts[1].ToUpperInvariant()] = parts[2];
            else if (parts[0] == "ledges") files.Ledges[parts[1].ToUpperInvariant()] = parts[2];
        }
        return files;
    }

    public string Text()
    {
        var text = new System.Text.StringBuilder("# LBA Assembler's maps: islands of their own (File > New map) -- each one's look (its sky and palette: an island\r\n" +
                                                  "# of the game's), then the scenes drawn on it\r\n");
        foreach (var (file, look) in Look) text.Append($"island {file} {look}\r\n");
        foreach (var (file, weather) in Weather) text.Append($"weather {file} {weather}\r\n");
        foreach (var (file, ledges) in Ledges) text.Append($"ledges {file} {ledges}\r\n");
        foreach (var (scene, file) in Scenes) text.Append($"scene {scene} {file}\r\n");
        return text.ToString();
    }
}
