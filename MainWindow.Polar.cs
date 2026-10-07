using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LBAAssembler.Terrain.Polar;

namespace LBAAssembler;

// Tools > LBA2: Polar Island: LBA1's Polar Island as LBA2's island 12 (Terrain/Polar: its outside scenes joined into one island, the
// ground a height map, the objects bodies, a scene to a cube) -- added to the game folder, built again, or taken out.
public partial class MainWindow
{
    private const string PolarTitle = "Polar Island";

    private async void Lba2PolarIsland_Click(object? sender, RoutedEventArgs e)
    {
        if (TestEditsActive || Demo96Active)
        {
            MessageBox.Show(this, "Leave test edits or the 1996 demo first: Polar Island is added to the game folder itself.", PolarTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!Lba2Engine.IsGameFolder(gameRoot))
        {
            MessageBox.Show(this, "The LBA2 game folder isn't set. Choose it under File > Settings.", PolarTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (scriptSession.EditedScenes.Any(s => s >= PolarScenes.FirstScene && s < PolarScenes.FirstScene + PolarScenes.Count))
        {
            MessageBox.Show(this, $"Polar Island's scenes ({PolarScenes.FirstScene}-{PolarScenes.FirstScene + PolarScenes.Count - 1}) have unsaved script edits. Save or discard them first.",
                PolarTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var lba1 = EditorSettings.Current.Lba1Directory;
        var installed = PolarIsland.IsInstalled(gameRoot);
        bool remove;
        if (!installed)
        {
            if (!Lba1Configured)
            {
                MessageBox.Show(this, "Polar Island is built from LBA1's own scenes: choose the LBA1 game folder under File > Settings first.", PolarTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var ok = MessageBox.Show(this,
                "Add LBA1's Polar Island to LBA2 as a new island (island 12)?\n\n" +
                "Its outside scenes are joined into one island you can turn round: the dock, the mines, the huts, the crystal mountain with the plateau of pillars on top " +
                "(the rocky peak's scene is left out: the third scene has the same mountain), and a causeway from the gate. " +
                $"It has a scene for each of its cubes with land ({PolarScenes.FirstScene + 1}-{PolarScenes.FirstScene + PolarScenes.Count - 1}); Twinsen starts on the dock beside his car. No characters yet.\n\n" +
                "Only the LBA Assembler's own engine (Play) knows island 12: the original game doesn't load it.\n\n" +
                $"Folder: {gameRoot}\nNew: POLAR.ILE, POLAR.OBL\nChanged: {string.Join(", ", PolarIsland.SharedFiles)} (first copied to *{PolarIsland.BackupSuffix}; " +
                "this menu takes the island out again)",
                PolarTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (ok != MessageBoxResult.OK) return;
            remove = false;
        }
        else
        {
            var choice = MessageBox.Show(this,
                "Polar Island is in this game folder.\n\n" +
                (Lba1Configured ? "Yes: build it again from LBA1's scenes.\n" : "") +
                "No: take it out of the folder (its files, scenes, holomap map and texts; the rest of the folder stays as it is now).\n" +
                "Cancel: leave it.",
                PolarTitle, Lba1Configured ? MessageBoxButton.YesNoCancel : MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (choice is MessageBoxResult.Cancel or MessageBoxResult.None) return;
            remove = choice is MessageBoxResult.No or MessageBoxResult.OK;
        }

        if (playing) StopPlay();
        List<string> log;
        using (UiBusy.Progress(BusyPanel, BusyLabel, remove ? "Taking Polar Island out…" : "Building Polar Island…"))
        {
            var folder = gameRoot;
            try { log = await Task.Run(() => remove ? PolarIsland.Remove(folder) : PolarIsland.Add(lba1, folder)); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                DebugLog.Log($"Polar Island: {(remove ? "remove" : "add")} failed: {error}");
                MessageBox.Show(this, $"Not done: {error.Message}", PolarTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        foreach (var line in log) DebugLog.Log("Polar Island: " + line);
        // (the same refresh as a race track build's: an island and its scenes made or taken away)
        RaceTrackChanged();
        SetStatus(remove ? "Polar Island taken out of the game folder." : $"Polar Island added: POLAR.ILE in the Island list, its scenes {PolarScenes.FirstScene + 1}-{PolarScenes.FirstScene + PolarScenes.Count - 1}; Play starts it.",
            StatusKind.Info);
    }
}
