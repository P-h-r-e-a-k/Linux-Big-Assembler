using System.IO;
using System.Windows;
using LBAAssembler.Demo96;
using Microsoft.Win32;

namespace LBAAssembler;

// The 1996 LBA2 demo, viewed as the game is: its folder converted once into the retail layout (Demo96Converter) in a folder of the
// editor's own, which then stands in for the LBA2 game folder -- as "Test edits" does with its scratch copy (EditorSettings.TestModeActive:
// nothing saves the settings with the demo's folder in them) -- until File > Close the 1996 demo puts the game folder back. The demo's
// islands, interiors and scenes show in the editor and the engine's renderer as the game's do; its scenes are named after the retail
// scenes they became (the converted folder's marker file), and the joined interior maps -- made for the retail scenes' numbers -- are off.
public partial class MainWindow
{
    // the converted folder while the demo is open, and the game folder it stands in for
    private string? demo96Root;
    private string? demo96GameDirectory;

    private bool Demo96Active => demo96Root is not null;

    // where the converted demo goes: the editor's own folder for what it makes (beside the engine binaries it unpacks)
    private static string Demo96Directory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LBAAssembler", "Demo96");

    private void OpenDemo96_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "The 1996 LBA2 demo's folder (with LBA2DEMO.EXE)" };
        if (dialog.ShowDialog(this) == true) OpenDemo96(dialog.FolderName);
    }

    // "--demo96 <folder> [scene]" on the command line: the demo opened as the window first shows (once), at one of its scenes if given
    private bool demo96FromCommandLineDone;
    private void OpenDemo96FromCommandLine()
    {
        if (demo96FromCommandLineDone) return;
        demo96FromCommandLineDone = true;
        var args = Environment.GetCommandLineArgs();
        var at = Array.FindIndex(args, a => a.Equals("--demo96", StringComparison.OrdinalIgnoreCase));
        if (at >= 0 && at + 1 < args.Length) OpenDemo96(args[at + 1], at + 2 < args.Length && int.TryParse(args[at + 2], out var scene) ? scene : null);
    }

    private async void OpenDemo96(string source, int? scene = null)
    {
        if (TestEditsActive) { SetStatus("Leave test edits (File > Test edits) before opening the 1996 demo.", StatusKind.Warning); return; }
        // (what the demo has none of, the engine's start-up files, comes from the game: Demo96Converter)
        var game = Demo96Active ? demo96GameDirectory! : EditorSettings.Current.GameDirectory;
        if (!Lba2Folder.IsValid(game))
        {
            MessageBox.Show(this, "The 1996 demo is shown with a few of the LBA2 game's own files beside its own (the engine's start-up files it has none of). " +
                                  "Choose the LBA2 game folder under File > Settings first.", "The 1996 LBA2 demo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!Demo96Converter.IsDemoFolder(source))
        {
            MessageBox.Show(this, "That isn't the 1996 LBA2 demo's folder: it has LBA2DEMO.EXE (or FILE3D.HQR) beside SCENE.HQR and RESS.HQR.",
                "The 1996 LBA2 demo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (Demo96Active) CloseDemo96();
        if (Demo96Active) return;
        var target = Demo96Directory;
        if (!Demo96Converter.IsConverted(target, source))
        {
            using var busy = UiBusy.Progress(BusyPanel, BusyLabel, "Converting the 1996 demo…");
            try
            {
                // (made fresh: a conversion of another version, or of another copy of the demo, goes)
                if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
                var log = await Task.Run(() => Demo96Converter.Convert(source, target, game));
                foreach (var line in log) DebugLog.Log("Demo96: " + line);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                DebugLog.Log($"Demo96: conversion failed: {error}");
                SetStatus($"Couldn't convert the 1996 demo: {error.Message}", StatusKind.Warning);
                return;
            }
        }
        EnterDemo96(target);
        if (Demo96Active && scene is { } n && allSceneEntries.FirstOrDefault(s => s.Option.Index == n) is { } entry) OpenLba2Area(IslandDisplayOf(entry), n);
    }

    private void CloseDemo96_Click(object sender, RoutedEventArgs e)
    {
        if (!Demo96Active) { SetStatus("The 1996 demo isn't open.", StatusKind.Info); return; }
        CloseDemo96();
    }

    private void EnterDemo96(string root)
    {
        if (!ConfirmTerrainDiscard()) return;
        StopLive();
        EndBodyPreviewLive();
        CloseDirectoryScopedEditors();
        demo96GameDirectory = EditorSettings.Current.GameDirectory;
        demo96Root = root;
        // (set before anything below can save the settings: EditorSettings.Save does nothing while it is)
        EditorSettings.TestModeActive = true;
        EditorSettings.Current.GameDirectory = root;
        ShowGameFolder();
        SetStatus("Viewing the 1996 LBA2 demo: its islands, interiors and scenes in the retail game's layout. File > Close the 1996 demo goes back to the game.", StatusKind.Info);
    }

    private void CloseDemo96()
    {
        if (!ConfirmTerrainDiscard()) return;
        StopLive();
        EndBodyPreviewLive();
        CloseDirectoryScopedEditors();
        EditorSettings.TestModeActive = false;
        EditorSettings.Current.GameDirectory = demo96GameDirectory!;
        demo96Root = null; demo96GameDirectory = null;
        ShowGameFolder();
        SetStatus("Back to the LBA2 game folder.", StatusKind.Info);
    }

    // The LBA2 view on whichever folder the settings now name.
    private void ShowGameFolder()
    {
        gameRoot = EditorSettings.Current.GameDirectory;
        nativeRenderer.SetGameDirectory(gameRoot);
        selectedLba2Scene = null;
        islandNameCache.Clear();
        SetGameSelection(GameKind.Lba2);
        SwitchGameCore(GameKind.Lba2);
        ApplyMode();
    }

    // A demo scene's name: the retail scene it became (the converted folder's map), or what it is.
    private static string Demo96SceneName(Dictionary<int, int> map, int numscene, IReadOnlyList<string?> retailNames, string? islandFile, bool interior)
    {
        if (map.TryGetValue(numscene, out var retail) && retail >= 0 && retail + 1 < retailNames.Count && retailNames[retail + 1] is { } name) return name;
        var island = islandFile is null ? "" : char.ToUpperInvariant(islandFile[0]) + islandFile[1..].ToLowerInvariant() + ", ";
        return interior ? $"{island}an interior only the 1996 demo has" : $"{island}outdoors";
    }
}
