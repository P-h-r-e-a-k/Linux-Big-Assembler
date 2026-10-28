using System.IO;
using Avalonia.Interactivity;
using LBAAssembler.Maps;

namespace LBAAssembler;

// Maps in files of their own (the user, 2026-10-15: "Do we have the ability to create new maps for both games? Perhaps add a File>New option
// then we can specify the type ile, lba2 inside, lba1"; "Let's also add the ability to load new maps from a file instead of them having to be
// in a HQR file"). A map (Maps/LbaMap: an LBA2 island, an LBA2 interior or an LBA1 interior, in a .lbamap file) is opened into a workspace: a
// copy of the game folder (LiveDataRoot's mirror, as Test edits makes) with the map installed in it (MapInstall) -- LBA2's from scene 400 on,
// LBA1's in its slot -- which stands in for the game folder as Test edits' does, so every editor, the 3D view and Play work on it as on the
// game's own maps. File > Save map reads it back out of the workspace into its file; File > Install map into game puts it into the real game
// folder; File > Close map goes back to the game folder.
public partial class MainWindow
{
    private const string MapWorkspaceFolderName = "_MAP_WORKSPACE";
    // the map as its file last had it (or as made), its file (null: not saved yet), where it is in the workspace
    private LbaMap? openMap;
    private string? openMapPath;
    private InstalledMap? openMapInstalled;
    private LiveDataRoot? mapRoot;
    private string? mapRealDirectory;          // the game folder the workspace copies (LBA2's or LBA1's)

    private bool MapOpen => openMap is not null;

    // Where map files go by default: the open map's own folder, else a Maps folder beside the program (made when first wanted) -- not
    // Documents on the system drive (the user, 2026-10-22: nothing goes on C:\ without asking; until then it was Documents\LBA Assembler\Maps).
    private string MapsFolder()
    {
        if (openMapPath is not null && Path.GetDirectoryName(openMapPath) is { Length: > 0 } own && Directory.Exists(own)) return own;
        var folder = Path.Combine(AppContext.BaseDirectory, "Maps");
        try { Directory.CreateDirectory(folder); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return AppContext.BaseDirectory; }
        return folder;
    }

    // ---- File > New map
    private async void NewMap_Click(object? sender, RoutedEventArgs e)
    {
        if (!ReadyForMap()) return;
        var islands = Lba2Configured
            ? Directory.EnumerateFiles(gameRoot, "*.ILE").Select(p => Path.GetFileNameWithoutExtension(p).ToUpperInvariant())
                .Where(n => MapNew.GameIslands.Any(g => g.File == n) && File.Exists(Path.Combine(gameRoot, n + ".OBL"))).Order().ToList()
            : new List<string>();
        var allIslands = Lba2Configured ? Directory.EnumerateFiles(gameRoot, "*.ILE").Select(p => Path.GetFileNameWithoutExtension(p).ToUpperInvariant()).ToList() : new List<string>();
        var interiors = currentGame == GameKind.Lba2 || allSceneEntries.Count > 0
            ? allSceneEntries.Where(s => s.IsInterior).Select(s => (s.Option.Index, s.Option.Display)).ToList() : new List<(int, string)>();
        if (interiors.Count == 0 && Lba2Configured) interiors = Lba2InteriorLabels();
        var lba1Scenes = Lba1SceneLabels();
        var dialog = new NewMapWindow(Lba2Configured, Lba1Configured, islands, interiors, lba1Scenes).WithOwner(this);
        // (the island list the dialog checks a new file name against: every ILE the folder has)
        if (dialog.ShowDialog() != true) return;
        if (dialog.Kind == MapKind.Lba2Island && allIslands.Contains(dialog.IslandFileName, StringComparer.OrdinalIgnoreCase))
        { SetStatus($"The game folder already has {dialog.IslandFileName}.ILE: choose another name.", StatusKind.Warning); return; }

        LbaMap map;
        using (UiBusy.Progress(BusyPanel, BusyLabel, "Making the map…"))
        {
            try
            {
                var lba2 = gameRoot; var lba1 = EditorSettings.Current.Lba1Directory;
                map = await Task.Run(() => dialog.Kind switch
                {
                    MapKind.Lba2Island => MapNew.Lba2Island(lba2, dialog.MapName, dialog.IslandFileName, dialog.BaseIsland, dialog.CubesX, dialog.CubesZ, dialog.LandHeight, dialog.Heightmap, dialog.HeightmapOptions),
                    MapKind.Lba2Interior => MapNew.Lba2Interior(lba2, dialog.BaseScene, dialog.MapName),
                    _ => MapNew.Lba1Interior(lba1, dialog.BaseScene, dialog.MapName),
                });
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or Scenes.SceneEditException or InvalidOperationException)
            {
                DebugLog.Log($"New map failed: {error}");
                SetStatus($"Couldn't make the map: {error.Message}", StatusKind.Warning);
                return;
            }
        }
        OpenMapWorkspace(map, null);
    }

    // ---- File > Open map (a .lbamap; an ILE from outside the game folder: an island map round it)
    private void OpenMap_Click(object? sender, RoutedEventArgs e)
    {
        if (!ReadyForMap()) return;
        var dialog = new OpenFileDialog { Filter = LbaMap.FileFilter + "|LBA2 islands from outside the game folder (*.ILE)|*.ILE", Title = "Open a map", InitialDirectory = MapsFolder() };
        if (dialog.ShowDialog(this) != true) return;
        OpenMapFile(dialog.FileName);
    }

    private void OpenMapFile(string path)
    {
        try
        {
            if (Path.GetExtension(path).Equals(".ILE", StringComparison.OrdinalIgnoreCase)) { OpenMapWorkspace(IslandFromFile(path), null); return; }
            OpenMapWorkspace(LbaMap.Load(path), path);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or Scenes.SceneEditException)
        {
            SetStatus($"Couldn't open the map: {error.Message}", StatusKind.Warning);
        }
    }

    // An island file from outside the game folder, as a map: its OBL beside it (else the island's whose look it takes), an outside scene for each
    // of its cubes as a new island's (MapNew.IslandScenes), its look -- the game's island it is named after, or Citadel's.
    private LbaMap IslandFromFile(string path)
    {
        if (!Lba2Configured) throw new InvalidDataException("An island opens into the LBA2 game folder: choose it under File > Settings first.");
        var name = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
        if (string.Equals(Path.GetDirectoryName(Path.GetFullPath(path))?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.GetFullPath(gameRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{name}.ILE is the game folder's own: open it from the island list (it is there), or File > Export map to make a map of it.");
        var look = MapNew.GameIslands.FirstOrDefault(g => g.File == name) is { File: not null } known ? known : ("CITADEL", 0);
        var obl = Path.ChangeExtension(path, ".OBL");
        var fileName = LbaMap.ValidIslandFile(name) && !File.Exists(Path.Combine(gameRoot, name + ".ILE")) ? name : LbaMap.FileNameFrom(name + "MAP", 8);
        return MapNew.IslandMap(gameRoot, name, fileName, File.ReadAllBytes(path), File.Exists(obl) ? File.ReadAllBytes(obl) : File.ReadAllBytes(Path.Combine(gameRoot, look.Item1 + ".OBL")), look.Item2);
    }

    // "--map <file>" on the command line, or a map file as the only argument (a .lbamap opened from Explorer): opened as the window first
    // shows (once)
    private bool mapFromCommandLineDone;
    private void OpenMapFromCommandLine()
    {
        if (mapFromCommandLineDone) return;
        mapFromCommandLineDone = true;
        var args = Environment.GetCommandLineArgs();
        var at = Array.FindIndex(args, a => a.Equals("--map", StringComparison.OrdinalIgnoreCase));
        var path = at >= 0 && at + 1 < args.Length ? args[at + 1]
            : args.Length == 2 && args[1].EndsWith(LbaMap.Extension, StringComparison.OrdinalIgnoreCase) ? args[1] : null;
        if (path is not null && ReadyForMap()) OpenMapFile(path);
    }

    // ---- the workspace
    private bool ReadyForMap()
    {
        if (TestEditsActive) { SetStatus("Leave test edits before opening a map: a map has a test copy of the game folder of its own.", StatusKind.Warning); return false; }
        if (Demo96Active) { SetStatus("Close the 1996 demo before opening a map.", StatusKind.Warning); return false; }
        if (MapOpen && !CloseMapWorkspace()) return false;
        if (!Lba2Configured && !Lba1Configured) { SetStatus("No game folder is set (File > Settings).", StatusKind.Warning); return false; }
        return true;
    }

    // ---- script edits and the folder they belong to
    // The script editors keep their unsaved edits (ScriptSession) for the game folder they were made in. Before that folder changes -- a map
    // opened or closed, test edits started or ended, the 1996 demo, another folder chosen in Settings -- they are saved there or dropped,
    // never carried over: an LBA1 map's script edit, left unsaved at Close map, showed as the game's own scene 28 and was saved over it
    // (2026-10-17). Returns false when the change is cancelled.
    private void CommitOpenScriptWindows()
    {
        foreach (var window in openScriptWindows.Values.ToList()) window.CommitForRestyle();
    }

    private string PendingScriptEdits()
    {
        CommitOpenScriptWindows();
        var parts = new List<string>();
        foreach (var (session, game) in new[] { (scriptSession, "LBA2"), (lba1Session, "LBA1") })
            if (session.HasUnsavedEdits)
                parts.Add($"{game} scene{(session.EditedScenes.Count > 1 ? "s" : "")} {string.Join(", ", session.EditedScenes)}{(MapWorkspace.For(session.OtherFolder ?? session.Folder) is { } map ? $" of the map \"{map.Name}\"" : "")}");
        return string.Join(" and ", parts);
    }

    // Saves every script edit into the folder it was made in; false (after saying why) when one can't be.
    private bool SaveScriptEdits()
    {
        foreach (var session in new[] { scriptSession, lba1Session })
        {
            if (!session.HasUnsavedEdits) continue;
            var result = session.SaveAll();
            if (!result.Ok)
            {
                var detail = result.Errors.Count > 1 ? "\n\n" + string.Join("\n", result.Errors.Take(8)) : "";
                MessageBox.Show(this, result.Message + detail, "Scripts not saved", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            SetStatus(result.Message, StatusKind.Success);
        }
        return true;
    }

    private void DropScriptEdits()
    {
        scriptSession.DiscardAll();
        lba1Session.DiscardAll();
    }

    private bool SettleScriptEdits(string before)
    {
        var pending = PendingScriptEdits();
        if (pending.Length == 0) { DropScriptEdits(); return true; }
        var targets = new[] { scriptSession, lba1Session }.Where(s => s.HasUnsavedEdits)
            // (the folder and the file joined the way this system writes a path, not WPF's "...\SCENE.HQR")
            .Select(s => MapWorkspace.For(s.OtherFolder ?? s.Folder) is { } map ? $"the map \"{map.Name}\"" : Path.Combine(s.OtherFolder ?? s.Folder, "SCENE.HQR")).Distinct();
        var answer = MessageBox.Show(this, $"The scripts of {pending} are edited and not saved yet.\n\nSave them into {string.Join(" and ", targets)} before {before}? No drops the edits.",
            "Unsaved script edits", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel || answer == MessageBoxResult.None) return false;
        if (answer == MessageBoxResult.Yes && !SaveScriptEdits()) return false;
        DropScriptEdits();
        return true;
    }

    private void OpenMapWorkspace(LbaMap map, string? path)
    {
        if (!ConfirmTerrainDiscard()) return;
        if (!SettleScriptEdits("the map opens")) return;
        StopPlay();
        StopLive();
        EndBodyPreviewLive();
        CloseDirectoryScopedEditors();
        var real = map.IsLba1 ? EditorSettings.Current.Lba1Directory : EditorSettings.Current.GameDirectory;
        if (map.IsLba1 ? !Lba1Configured : !Lba2Configured) { SetStatus($"An {map.KindName} map needs the {(map.IsLba1 ? "LBA1" : "LBA2")} game folder (File > Settings).", StatusKind.Warning); return; }
        LiveDataRoot.CleanStale(real, MapWorkspaceFolderName);
        var root = LiveDataRoot.Create(real, null, MapWorkspaceFolderName);
        if (root is null) { SetStatus("Couldn't make the map's copy of the game folder.", StatusKind.Warning); return; }
        InstalledMap installed;
        try { installed = MapInstall.Install(map, root.Directory); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or Scenes.SceneEditException)
        {
            root.Dispose();
            DebugLog.Log($"Map workspace: install failed: {error}");
            SetStatus($"Couldn't open the map: {error.Message}", StatusKind.Warning);
            return;
        }
        mapRoot = root; mapRealDirectory = real;
        openMap = map; openMapPath = path; openMapInstalled = installed;
        // (where saves go, for the editors' messages; and the game folder guarded: nothing writes it while the map is open)
        MapWorkspace.Opened(map.Name, path, root.Directory, real, map.IsLba1);
        // (set before anything below can save the settings: EditorSettings.Save does nothing while it is)
        EditorSettings.TestModeActive = true;
        if (map.IsLba1) EditorSettings.Current.Lba1Directory = root.Directory; else EditorSettings.Current.GameDirectory = root.Directory;
        gameRoot = EditorSettings.Current.GameDirectory;
        nativeRenderer.SetGameDirectory(gameRoot);
        lba1Game = null; lba1Images = null;
        selectedLba2Scene = null;
        islandNameCache.Clear();
        var game = map.IsLba1 ? GameKind.Lba1 : GameKind.Lba2;
        SetGameSelection(game);
        SwitchGameCore(game);
        ApplyMode();
        ShowOpenMap();
        SetStatus($"Map \"{map.Name}\" ({map.KindName}{(path is null ? ", not saved yet" : $", {Path.GetFileName(path)}")}): in a copy of the game folder, scene{(installed.Scenes.Count > 1 ? "s" : "")} " +
                  $"{string.Join(", ", installed.Scenes)}. Edit and play it as any map; File > Save map keeps it, File > Install map into game puts it into your game.", StatusKind.Info);
    }

    // The open map in the view: its first scene, on its island.
    private void ShowOpenMap()
    {
        if (openMapInstalled is not { } at) return;
        if (openMap!.IsLba1)
        {
            if (lba1Game?.Scenes.FirstOrDefault(s => s.Index == at.FirstScene) is { } scene) OpenLba1Area(scene.Island, at.FirstScene);
            return;
        }
        // (read again even when an island of that name is in view: the folder's own of that name, or the map's last workspace's -- the snow
        // island built again over itself showed nothing, 2026-10-22)
        if (allSceneEntries.FirstOrDefault(s => s.Option.Index == at.FirstScene) is { } entry) OpenLba2Area(IslandDisplayOf(entry), at.FirstScene, reload: true);
    }

    // Back to the game folder; false when kept open (unsaved changes kept, or the close cancelled).
    private bool CloseMapWorkspace(bool ask = true)
    {
        if (!MapOpen) return true;
        if (!ConfirmTerrainDiscard()) return false;
        // (the script editors' unsaved edits of the map's scenes are the map's changes too: Yes saves them into it first, No drops them --
        // they never stay behind for the game's own scenes of those numbers)
        var scripts = PendingScriptEdits();
        if (ask && (scripts.Length > 0 || MapChanged()))
        {
            var answer = MessageBox.Show(this, $"Save the changes to the map \"{openMap!.Name}\" before closing it?" +
                (scripts.Length > 0 ? $"\n\nThe scripts of {scripts} are edited and not saved yet: Yes saves them into the map too, No drops them." : ""),
                "Close map", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel || answer == MessageBoxResult.None) return false;
            if (answer == MessageBoxResult.Yes && (!SaveScriptEdits() || !SaveMap(saveAs: false))) return false;
        }
        DropScriptEdits();
        StopPlay();
        StopLive();
        EndBodyPreviewLive();
        CloseDirectoryScopedEditors();
        var lba1 = openMap!.IsLba1;
        MapWorkspace.Closed();
        EditorSettings.TestModeActive = false;
        if (lba1) EditorSettings.Current.Lba1Directory = mapRealDirectory!; else EditorSettings.Current.GameDirectory = mapRealDirectory!;
        gameRoot = EditorSettings.Current.GameDirectory;
        nativeRenderer.SetGameDirectory(gameRoot);
        // (the renderer's data root is the process's working folder -- RENDERER_API.CPP set_data_root's chdir: out of the workspace before
        // it goes, else it can't be removed)
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        RemoveWorkspace(mapRoot);
        mapRoot = null;
        openMap = null; openMapPath = null; openMapInstalled = null; mapRealDirectory = null;
        lba1Game = null; lba1Images = null;
        selectedLba2Scene = null;
        islandNameCache.Clear();
        SwitchGameCore(currentGame);
        ApplyMode();
        SetStatus("Map closed: back to the game folder.", StatusKind.Info);
        return true;
    }

    // The workspace removed; tried again a few times a little later while something still has a file of it open (the engine's last read).
    private void RemoveWorkspace(LiveDataRoot? root, int tries = 6)
    {
        if (root is null) return;
        root.Dispose();
        if (!Directory.Exists(root.Directory) || tries <= 1) return;
        var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => { timer.Stop(); RemoveWorkspace(root, tries - 1); };
        timer.Start();
    }

    // ---- File > Map text: the open map's own lines of dialogue (MapTextWindow), in the workspace's <stem>_TEXT.TXT until Save map
    private void MapText_Click(object? sender, RoutedEventArgs e)
    {
        if (!MapOpen || openMapInstalled is null) { SetStatus("Open a map first: File > Open map or New map. Its text is its own lines of dialogue.", StatusKind.Info); return; }
        var window = new MapTextWindow(MapInstall.TextPath(openMapInstalled.Map, openMapInstalled.Directory), openMapInstalled.Map.Name).WithOwner(this);
        window.ShowOwned();
    }

    // ---- Build > Actors and zones > Character maker: the open map's own characters (CharacterMakerWindow), in the workspace until Save map
    private void CharacterMaker_Click(object? sender, RoutedEventArgs e)
    {
        if (!MapOpen || openMapInstalled is null)
        {
            MessageBox.Show(this, "The Character maker makes a map's own characters: open a map first (File > Open map or New map).", "Character maker", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var directory = openMapInstalled.Directory;
        var window = new CharacterMakerWindow(directory, Characters.MapCharacters.SidecarPath(directory, openMapInstalled.Map.Stem)).WithOwner(this);
        // (the 3D view's renderer read the entity table, bodies and animations when it started: a character saved now is read again, or
        // placing an actor of it crashed the editor -- 2026-10-23)
        window.Saved += () => nativeRenderer.ReloadCharacters(directory);
        window.ShowOwned();
    }

    // ---- File > Map settings: the open map's name, description and, an island's, weather and cliffs
    private void MapSettings_Click(object? sender, RoutedEventArgs e)
    {
        if (!MapOpen || openMapInstalled is null) { SetStatus("Open a map first: File > Open map or New map.", StatusKind.Info); return; }
        var map = openMapInstalled.Map;
        var window = new MapSettingsWindow(map).WithOwner(this);
        if (window.ShowDialog() != true) return;
        try
        {
            MapInstall.PutIslandSettings(map, openMapInstalled.Directory);
            InvalidateNativeIsland();
            SetStatus($"Map settings of \"{map.Name}\" changed (File > Save map writes them into its file).", StatusKind.Info);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            SetStatus($"The map's settings couldn't be put into its workspace: {error.Message}", StatusKind.Warning);
        }
    }

    private void CloseMap_Click(object? sender, RoutedEventArgs e)
    {
        if (!MapOpen) { SetStatus("No map is open.", StatusKind.Info); return; }
        CloseMapWorkspace();
    }

    // Whether the workspace's map differs from its file's (or it has none).
    private bool MapChanged()
    {
        if (openMapInstalled is null || openMap is null) return false;
        if (openMapPath is null) return true;
        try { return !SameMap(MapInstall.Capture(openMapInstalled), openMap); }
        catch (Exception error) when (error is IOException or InvalidDataException) { DebugLog.Log($"Map: couldn't read it back: {error.Message}"); return true; }
    }

    private static bool SameMap(LbaMap a, LbaMap b)
    {
        static bool Same(byte[]? x, byte[]? y) => (x is null) == (y is null) && (x is null || x.AsSpan().SequenceEqual(y));
        return a.Scenes.Count == b.Scenes.Count && a.Scenes.Zip(b.Scenes).All(p => Same(p.First.Record, p.Second.Record))
            && Same(a.Grid, b.Grid) && Same(a.Library, b.Library) && Same(a.Ile, b.Ile) && Same(a.Obl, b.Obl)
            && Same(a.Bricks, b.Bricks) && Same(a.Fragments, b.Fragments) && a.ForbiddenBrick == b.ForbiddenBrick && Same(a.Comments, b.Comments) && Same(a.Text, b.Text) && a.Name == b.Name && a.Description == b.Description && a.Weather == b.Weather && a.Ledges == b.Ledges && Same(a.Characters, b.Characters) && a.CharacterBodies.Count == b.CharacterBodies.Count && a.CharacterBodies.Zip(b.CharacterBodies).All(p => Same(p.First, p.Second)) && a.CharacterAnims.Count == b.CharacterAnims.Count && a.CharacterAnims.Zip(b.CharacterAnims).All(p => Same(p.First, p.Second));
    }

    // ---- File > Save map / Save map as
    private void SaveMap_Click(object? sender, RoutedEventArgs e) => SaveMap(saveAs: false);
    private void SaveMapAs_Click(object? sender, RoutedEventArgs e) => SaveMap(saveAs: true);

    // Script edits not yet saved into the map, before the map is written out (Save map, Install map into game): saved into it first (Yes),
    // left out (No), or the whole thing called off (false).
    private bool ScriptEditsIntoMap(string action, string without)
    {
        var scripts = PendingScriptEdits();
        if (scripts.Length == 0) return true;
        var answer = MessageBox.Show(this, $"The scripts of {scripts} are edited but not saved into the map yet.\n\nSave them into it before {action}? No {without} (they stay edited in the script editor).",
            "Unsaved script edits", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel || answer == MessageBoxResult.None) return false;
        return answer == MessageBoxResult.No || SaveScriptEdits();
    }

    private bool SaveMap(bool saveAs)
    {
        if (openMapInstalled is not { } at) { SetStatus("No map is open: File > New map or File > Open map.", StatusKind.Info); return false; }
        if (!ScriptEditsIntoMap("saving the map", "saves the map without them")) return false;
        if (terrainEditor is { Dirty: true } editor) editor.Save();       // (the island's ground into the workspace first)
        var path = openMapPath;
        if (path is null || saveAs)
        {
            var dialog = new SaveFileDialog { Filter = LbaMap.FileFilter, FileName = LbaMap.FileNameFrom(openMap!.Name, 24).ToLowerInvariant() + LbaMap.Extension, Title = "Save the map", InitialDirectory = MapsFolder() };
            if (dialog.ShowDialog(this) != true) return false;
            path = dialog.FileName;
        }
        try
        {
            var map = MapInstall.Capture(at);
            map.Save(path);
            openMap = map; openMapPath = path;
            openMapInstalled = at with { Map = map };
            MapWorkspace.Saved(map.Name, path);
            SetStatus($"Map \"{map.Name}\" saved to {path}.", StatusKind.Success);
            return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            SetStatus($"Couldn't save the map: {error.Message}", StatusKind.Warning);
            return false;
        }
    }

    // ---- File > Install map into game
    private void InstallMap_Click(object? sender, RoutedEventArgs e)
    {
        if (openMapInstalled is not { } at) { SetStatus("No map is open to install.", StatusKind.Info); return; }
        if (!ScriptEditsIntoMap("installing it", "installs the map without them")) return;
        if (terrainEditor is { Dirty: true } editor) editor.Save();
        var map = MapInstall.Capture(at);
        var before = MapInstall.InstalledBefore(map, mapRealDirectory!);
        var where = map.Kind != MapKind.Lba1Interior && before is { Count: > 0 }
            ? $"It replaces the copy installed before (scene{(before.Count > 1 ? "s" : "")} {string.Join(", ", before)})" + (map.Kind == MapKind.Lba2Island ? $" and {map.IslandFile}.ILE and .OBL." : " and its grid.")
            : map.Kind switch
        {
            MapKind.Lba1Interior => $"It takes the place of scene {map.Slot} in your LBA1 game folder ({mapRealDirectory}): that scene, its grid and its block library are replaced.",
            MapKind.Lba2Interior => $"It is added to your LBA2 game folder ({mapRealDirectory}) as a new scene from {MapInstall.FirstMapScene} on, with a grid of its own.",
            _ => $"It is added to your LBA2 game folder ({mapRealDirectory}): {map.IslandFile}.ILE and .OBL, and an outside scene for each of its {map.Scenes.Count} cubes from scene {MapInstall.FirstMapScene} on."
                 + (File.Exists(Path.Combine(mapRealDirectory!, map.IslandFile + ".ILE")) ? $" The folder's {map.IslandFile}.ILE is replaced." : ""),
        };
        // LBA2: as a map file in the game folder's MAPS folder, which the engine plays it from (MAPPACK.CPP; the user, 2026-10-15: "save
        // them as a zip ... Makes it easy to save and load"), or into the game's own files as before
        var intoMapsFolder = false;
        string? mapsFile = null;
        if (!map.IsLba1)
        {
            mapsFile = openMapPath is not null ? Path.GetFileNameWithoutExtension(openMapPath) + LbaMap.Extension : LbaMap.FileNameFrom(map.Name, 24).ToLowerInvariant() + LbaMap.Extension;
            var there = Path.Combine(mapRealDirectory!, MapInstall.MapsFolderName, mapsFile);
            var files = map.Kind == MapKind.Lba2Island ? $"SCENE.HQR, {map.IslandFile}.ILE and .OBL, ISLANDS.TXT" : "SCENE.HQR and LBA_BKG.HQR";
            var answer = MessageBox.Show(this,
                $"Install the map \"{map.Name}\" into your game ({mapRealDirectory})?\n\n" +
                $"Yes: as a map file, {Path.Combine(MapInstall.MapsFolderName, mapsFile)}{(File.Exists(there) ? " (it replaces the one there, which keeps a .bak copy)" : "")}. " +
                "The engine plays it from there, as the scenes its MAP.TXT numbers; nothing else in the game folder changes.\n\n" +
                $"No: into the game's own files ({files}), where the editor lists it among the game's scenes. {where} Each file changed keeps a .bak copy.",
                "Install map into game", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel || answer == MessageBoxResult.None) return;
            intoMapsFolder = answer == MessageBoxResult.Yes;
        }
        if (intoMapsFolder)
        {
            try
            {
                // (the one write into the game folder while a map is open: FileTransaction refuses any other)
                using var allowed = FileTransaction.AllowWrites();
                var (path, scenes) = MapInstall.InstallInMapsFolder(map, mapRealDirectory!, mapsFile!);
                SetStatus($"Installed \"{map.Name}\" as {path}: the engine plays it as scene{(scenes.Count > 1 ? "s" : "")} {string.Join(", ", scenes)}.", StatusKind.Success);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or Scenes.SceneEditException)
            {
                SetStatus($"Couldn't install the map: {error.Message}", StatusKind.Warning);
            }
            return;
        }
        if (map.IsLba1 && MessageBox.Show(this, $"Install the map \"{map.Name}\" into your game? {where} Each file changed keeps a .bak copy of how it was.", "Install map into game",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try
        {
            using var allowed = FileTransaction.AllowWrites();
            var installed = MapInstall.Install(map, mapRealDirectory!);
            SetStatus($"Installed \"{map.Name}\" into your game: scene{(installed.Scenes.Count > 1 ? "s" : "")} {string.Join(", ", installed.Scenes)}" +
                      (installed.Grid is { } g ? $", grid {g}" : "") + ". Close the map to see it among the game's own.", StatusKind.Success);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or Scenes.SceneEditException)
        {
            SetStatus($"Couldn't install the map: {error.Message}", StatusKind.Warning);
        }
    }

    // ---- File > Export map: the scene or island in view, out of the game folder into a map file
    private void ExportMap_Click(object? sender, RoutedEventArgs e)
    {
        LbaMap map;
        try
        {
            if (currentGame == GameKind.Lba1)
            {
                if (lba1CurrentTiles is not { Count: 1 } tiles) { SetStatus("Choose one LBA1 scene to export first (not a joined area: Scenes > LBA1, a single scene).", StatusKind.Info); return; }
                var scene = tiles[0].Scene;
                map = MapInstall.ExportLba1Interior(EditorSettings.Current.Lba1Directory, scene, lba1Game?.Description(scene) ?? $"Scene {scene}");
            }
            else if (selectedLba2Scene is { } scene && allSceneEntries.FirstOrDefault(s => s.Option.Index == scene) is { IsInterior: true } entry)
                map = MapInstall.ExportLba2Interior(gameRoot, scene, entry.Option.Display);
            else if (File.Exists(Path.Combine(gameRoot, activeFile)) && Path.GetExtension(activeFile).Equals(".ILE", StringComparison.OrdinalIgnoreCase))
                map = MapInstall.ExportLba2Island(gameRoot, Path.GetFileNameWithoutExtension(activeFile), Path.GetFileNameWithoutExtension(activeFile));
            else { SetStatus("Choose an island or an interior scene to export first.", StatusKind.Info); return; }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or Scenes.SceneEditException)
        {
            SetStatus($"Couldn't export it: {error.Message}", StatusKind.Warning);
            return;
        }
        var dialog = new SaveFileDialog { Filter = LbaMap.FileFilter, FileName = LbaMap.FileNameFrom(map.Name, 24).ToLowerInvariant() + LbaMap.Extension, Title = "Export as a map", InitialDirectory = MapsFolder() };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            map.Save(dialog.FileName);
            SetStatus($"Exported {map.KindName} \"{map.Name}\" to {dialog.FileName} (File > Open map opens it).", StatusKind.Success);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { SetStatus($"Couldn't write the map: {error.Message}", StatusKind.Warning); }
    }

    // ---- lists for the New map dialog when the view is on the other game
    private List<(int Id, string Label)> Lba2InteriorLabels()
    {
        var store = new Scenes.SceneStore(Scenes.SceneGame.Lba2, gameRoot);
        var list = new List<(int, string)>();
        for (var n = 0; n < store.SceneCount; n++)
            if (store.SceneExists(n) && store.LoadRecord(n) is { Length: > 5 } r && r[5] == 0) list.Add((n, $"{n}"));
        return list;
    }

    private List<(int Id, string Label)> Lba1SceneLabels()
    {
        if (!Lba1Configured) return new();
        try
        {
            var game = lba1Game ?? new Lba1.Lba1Game(EditorSettings.Current.Lba1Directory);
            return game.Scenes.Select(s => (s.Index, game.Description(s.Index) is { } d ? $"{s.Index}: {d}" : $"{s.Index}")).ToList();
        }
        catch (Exception error) when (error is IOException or InvalidDataException) { DebugLog.Log($"New map: LBA1 scenes: {error.Message}"); return new(); }
    }
}
