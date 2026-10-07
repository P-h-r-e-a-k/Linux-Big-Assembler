using System.IO;
using LBAAssembler.Lba1;

namespace LBAAssembler.Terrain.Polar;

// LBA1's Polar Island as LBA2's island 12 (the engine's island list: EXTFUNC.CPP IleLst, "polar"), into a game folder (Tools > LBA2:
// Polar Island, or ScriptRoundTrip's polarinstall):
//   - POLAR.ILE: the ground (PolarTerrain, the file made from MOON.ILE's) -- and POLAR.OBL, the bodies of its objects (PolarObjects);
//   - SCENE.HQR, HOLOMAP.HQR, TEXT.HQR: its scenes, one to a cube, and what they need (PolarScenes, PolarHolomap);
//   - RESS.HQR: island 12's sky (RESS_SKYSEA0 + 12 = entry 23) and palette (RESS_XPL0 + 12 = 39), two slots the retail file leaves
//     empty: the palette its ground was made in, and a retail island's sky.
// Only the LBA Assembler's own engine knows island 12 (its island list, text and holomap tables): the retail game never loads it.
// Before the first time, the four shared files are copied to *.before-polar; Remove takes out exactly what Add put in (with those copies'
// entries where it replaced something), so whatever else changed in the folder since stays.
internal static class PolarIsland
{
    public const int IslandByte = 12, SkyEntry = 23, PaletteEntry = 39;
    // the sky: the fine-weather Citadel's (RESS_SKYSEA00)
    public static int SkySource = 26;

    public sealed record Built(Lba1Game Game, PolarLayout Layout, PolarTerrain.Result Terrain, PolarObjects.Built Objects, List<string> Log);

    public const string BackupSuffix = ".before-polar";
    public static readonly string[] SharedFiles = { "RESS.HQR", "SCENE.HQR", "HOLOMAP.HQR", "TEXT.HQR" };
    public static bool IsInstalled(string gameDirectory) => File.Exists(Path.Combine(gameDirectory, PolarTerrain.IleFile));

    // Builds the island from the LBA1 folder's scenes and puts it in the game folder (again, when it is there: a rebuild). Returns lines
    // for the log. On an error, what it wrote is taken out again.
    public static List<string> Add(string lba1Directory, string gameDirectory)
    {
        if (!Lba1Game.IsInstalled(lba1Directory)) throw new InvalidDataException($"No LBA1 game in {lba1Directory}.");
        foreach (var f in SharedFiles.Append(PolarTerrain.SourceIle).Append("CITABAU.ILE"))
            if (!File.Exists(Path.Combine(gameDirectory, f))) throw new InvalidDataException($"The game folder has no {f}.");
        foreach (var f in SharedFiles)
        {
            var path = Path.Combine(gameDirectory, f);
            if (!File.Exists(path + BackupSuffix)) File.Copy(path, path + BackupSuffix);
            Writable(path);
        }
        var built = Build(lba1Directory, gameDirectory);
        try { return Install(gameDirectory, built); }
        catch
        {
            try { Remove(gameDirectory); } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { }
            throw;
        }
    }

    // Takes the island out of the game folder: its files, its scenes and their holomap records, its text, map and label, its sky and
    // palette -- each as the *.before-polar copy had it (or as the retail files have it, when there is no copy) -- and the copies.
    public static List<string> Remove(string gameDirectory)
    {
        var log = new List<string>();
        string At(string f) => Path.Combine(gameDirectory, f);
        HqrFile? Backup(string f) => File.Exists(At(f) + BackupSuffix) ? HqrFile.Parse(File.ReadAllBytes(At(f) + BackupSuffix)) : null;
        // (an entry of the file as the copy has it, or emptied; slots past the copy's count that end up empty are dropped)
        void Put(HqrFile file, HqrFile? backup, int entry)
        {
            if (entry >= file.Count) return;
            if (backup is not null && entry < backup.Count && !backup.IsEmpty(entry)) file.SetEntry(entry, backup.ExtentOf(entry));
            else file.Clear(entry);
        }
        void Trim(HqrFile file, HqrFile? backup, int retail)
        {
            var keep = backup?.Count ?? retail;
            while (file.Count > keep && file.IsEmpty(file.Count - 1)) file.RemoveAt(file.Count - 1);
        }

        foreach (var f in SharedFiles) Writable(At(f));
        var scene = HqrFile.Parse(File.ReadAllBytes(At("SCENE.HQR")));
        var sceneBackup = Backup("SCENE.HQR");
        // (every number the island's scenes may have -- this version's and an earlier one's -- where the scene is island 12's)
        foreach (var n in PolarScenes.AllScenes.Where(n => PolarScenes.OldScene(scene, n))) Put(scene, sceneBackup, n + 1);
        // (and the scenes an earlier version numbered 229-232, where they are still the island's)
        var oldScenes = PolarScenes.OldScenes.Where(n => PolarScenes.OldScene(scene, n)).ToList();
        foreach (var n in oldScenes) Put(scene, sceneBackup, n + 1);
        Trim(scene, sceneBackup, 223);
        File.WriteAllBytes(At("SCENE.HQR"), scene.ToBytes());
        PolarScenes.ForgetNames(gameDirectory);
        log.Add($"SCENE.HQR: the island's scenes ({PolarScenes.FirstScene}..{PolarScenes.LastPossibleScene}) taken out (and their names from SCENE.HQD)");

        var holo = HqrFile.Parse(File.ReadAllBytes(At("HOLOMAP.HQR")));
        var holoBackup = Backup("HOLOMAP.HQR");
        Put(holo, holoBackup, PolarHolomap.PictureEntry); Put(holo, holoBackup, PolarHolomap.CameraEntry);
        var positions = holo.Read(PolarScenes.PositionsEntry);
        var before = holoBackup?.Read(PolarScenes.PositionsEntry);
        foreach (var record in PolarScenes.Records().Concat(PolarScenes.OldScenes.Concat(PolarScenes.AllScenes).Select(n => PolarScenes.MaxObjectif + n)).Distinct())
        {
            // (a scene's record only where it is island 12's)
            if (record >= PolarScenes.MaxObjectif + PolarScenes.OldFirstScene
                && (record * PolarScenes.PositionSize + PolarScenes.PositionSize > positions.Length || positions[record * PolarScenes.PositionSize + 31] != IslandByte)) continue;
            var at = record * PolarScenes.PositionSize;
            if (at + PolarScenes.PositionSize > positions.Length) continue;
            if (before is not null && at + PolarScenes.PositionSize <= before.Length) before.AsSpan(at, PolarScenes.PositionSize).CopyTo(positions.AsSpan(at));
            else PolarScenes.RetailRecord(record).CopyTo(positions.AsSpan(at));
        }
        holo.SetStored(PolarScenes.PositionsEntry, positions);
        Trim(holo, holoBackup, 46);
        File.WriteAllBytes(At("HOLOMAP.HQR"), holo.ToBytes());
        log.Add("HOLOMAP.HQR: the island's map and its records taken out");

        var text = HqrFile.Parse(File.ReadAllBytes(At("TEXT.HQR")));
        var textBackup = Backup("TEXT.HQR");
        for (var lang = 0; lang < PolarScenes.Languages; lang++) { Put(text, textBackup, PolarScenes.TextEntry(lang)); Put(text, textBackup, PolarScenes.TextEntry(lang) + 1); }
        Trim(text, textBackup, PolarScenes.Languages * PolarScenes.RetailFiles * 2);
        var bytes = text.ToBytes();
        for (var lang = 0; lang < PolarScenes.Languages; lang++)
        {
            var bank = Lba2TextBank.Load(HqrArchive.FromBytes(bytes), lang, PolarHolomap.GameTextFile);
            var had = textBackup is not null && Lba2TextBank.Load(HqrArchive.FromBytes(textBackup.ToBytes()), lang, PolarHolomap.GameTextFile).Find(PolarHolomap.LabelText) is not null;
            if (had || bank.Texts.RemoveAll(t => t.Id == PolarHolomap.LabelText) == 0) continue;
            bytes = bank.WriteInto(bytes);
        }
        File.WriteAllBytes(At("TEXT.HQR"), bytes);
        log.Add("TEXT.HQR: the island's text file and label taken out");

        var ress = HqrFile.Parse(File.ReadAllBytes(At("RESS.HQR")));
        var ressBackup = Backup("RESS.HQR");
        Put(ress, ressBackup, SkyEntry); Put(ress, ressBackup, PaletteEntry);
        File.WriteAllBytes(At("RESS.HQR"), ress.ToBytes());
        log.Add($"RESS.HQR: entries {SkyEntry} and {PaletteEntry} as they were");

        foreach (var f in new[] { PolarTerrain.IleFile, PolarTerrain.OblFile, PolarTerrain.IleFile + ".bak" }.Concat(SharedFiles.Select(f => f + BackupSuffix)))
            if (File.Exists(At(f))) { Writable(At(f)); File.Delete(At(f)); }
        log.Add($"{PolarTerrain.IleFile} and {PolarTerrain.OblFile} deleted, and the {BackupSuffix} copies");
        return log;
    }

    public static Built Build(string lba1Directory, string gameDirectory)
    {
        var game = new Lba1Game(lba1Directory);
        var layout = PolarLayout.Build(game);
        var terrain = PolarTerrain.Build(game, layout, gameDirectory);
        var objects = PolarObjects.Build(game, layout, terrain.Columns, terrain.Rocks, terrain.Island, terrain.OffsetX, terrain.OffsetZ, terrain.Colours);
        return new Built(game, layout, terrain, objects, layout.Log.Concat(terrain.Log).Concat(objects.Log).ToList());
    }

    // Writes the island into the game folder. Returns lines for the log.
    public static List<string> Install(string gameDirectory, Built built)
    {
        var log = new List<string>(built.Log);
        File.WriteAllBytes(Path.Combine(gameDirectory, PolarTerrain.IleFile), built.Terrain.Island.ToBytes());
        File.WriteAllBytes(Path.Combine(gameDirectory, PolarTerrain.OblFile), Hqr(built.Objects.Bodies));
        log.Add($"{PolarTerrain.IleFile} and {PolarTerrain.OblFile} ({built.Objects.Bodies.Count} bodies) written");

        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var ress = HqrArchive.Open(ressPath);
        var bytes = File.ReadAllBytes(ressPath);
        bytes = Set(bytes, SkyEntry, ress.Read(SkySource));
        bytes = Set(bytes, PaletteEntry, ress.Read(PolarTerrain.ChosenPalette));
        File.WriteAllBytes(ressPath, bytes);
        log.Add($"RESS.HQR: island 12's sky (entry {SkyEntry}) from entry {SkySource}, its palette ({PaletteEntry}) from entry {PolarTerrain.ChosenPalette}");
        log.AddRange(PolarScenes.Install(gameDirectory, built.Game, built.Layout, built.Terrain));
        return log;
    }

    // (the retail game's files are read-only on some installs, GOG's among them: the files the island changes are made writable)
    private static void Writable(string path)
    {
        var info = new FileInfo(path);
        if (info.Exists && info.IsReadOnly) info.IsReadOnly = false;
    }

    // An HQR of stored entries.
    public static byte[] Hqr(IReadOnlyList<byte[]> entries)
    {
        var stored = entries.Select(HqrWriter.StoredEntry).ToList();
        var tableBytes = (stored.Count + 1) * 4;
        var result = new byte[tableBytes + stored.Sum(e => e.Length)];
        var at = tableBytes;
        for (var i = 0; i < stored.Count; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4), (uint)at);
            stored[i].CopyTo(result.AsSpan(at));
            at += stored[i].Length;
        }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(stored.Count * 4), (uint)result.Length);
        return result;
    }

    // An HQR entry set: an empty retail slot is given data, one with data replaced.
    private static byte[] Set(byte[] hqr, int slot, byte[] data)
    {
        try { return HqrWriter.FillEntry(hqr, slot, HqrWriter.StoredEntry(data)); }
        catch (InvalidDataException) { return HqrWriter.ReplaceEntry(hqr, slot, HqrWriter.StoredEntry(data)); }
    }
}
