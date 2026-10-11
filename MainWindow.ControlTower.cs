using System.IO;
using System.Windows;
using LBAAssembler.Terrain.ControlTower;

namespace LBAAssembler;

// Tools > LBA2: Island CX control tower (3D): Island CX twice its size with its control tower's lower level -- the two halls round the control
// room (scene 181) and the secret passage under their floor (180) -- built in 3D where its buildings were (Terrain/ControlTower: CXTOWER.ILE and
// CXTOWER.OBL), for a race track in through a door and out again -- added to the game folder, built again, or taken out.
public partial class MainWindow
{
    private const string ControlTowerTitle = "Island CX control tower";

    private async void Lba2ControlTower_Click(object sender, RoutedEventArgs e)
    {
        if (TestEditsActive || Demo96Active)
        {
            MessageBox.Show(this, "Leave test edits or the 1996 demo first: the island is added to the game folder itself.", ControlTowerTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!Lba2Engine.IsGameFolder(gameRoot))
        {
            MessageBox.Show(this, "The LBA2 game folder isn't set. Choose it under File > Settings.", ControlTowerTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        bool remove;
        if (!ControlTowerIsland.IsInstalled(gameRoot))
        {
            var ok = MessageBox.Show(this,
                "Build Island CX's control tower, lower level, in 3D?\n\n" +
                $"Island CX is made {ControlTowerIsland.Times} times its size, with its buildings replaced by what is inside them (scene 181: the two halls round " +
                "the control room) brick for brick, roofed, its three doors opening onto the yards where the landing pads are. The secret passage (scene 180) hangs " +
                "under the floor, stretched so that each of its two shafts comes up under one of the grates. The control tower stands on the control room.\n\n" +
                "No scenes or track yet: a race track in through a door and out again comes later.\n\n" +
                $"Folder: {gameRoot}\nNew: {ControlTowerIsland.IleFile}, {ControlTowerIsland.OblFile} (nothing else changes; this menu takes them out again)",
                ControlTowerTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (ok != MessageBoxResult.OK) return;
            remove = false;
        }
        else
        {
            var choice = MessageBox.Show(this,
                "The Island CX control tower island is in this game folder.\n\nYes: build it again.\nNo: take it out of the folder.\nCancel: leave it.",
                ControlTowerTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice is MessageBoxResult.Cancel or MessageBoxResult.None) return;
            remove = choice == MessageBoxResult.No;
        }
        List<string> log;
        using (UiBusy.Progress(BusyPanel, BusyLabel, remove ? "Taking the control tower island out…" : "Building the control tower island…"))
        {
            var folder = gameRoot;
            try { log = await Task.Run(() => remove ? ControlTowerIsland.Remove(folder) : ControlTowerIsland.Add(folder)); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                DebugLog.Log($"Control tower island: {(remove ? "remove" : "add")} failed: {error}");
                MessageBox.Show(this, $"Not done: {error.Message}", ControlTowerTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        foreach (var line in log) DebugLog.Log("Control tower island: " + line);
        RaceTrackChanged();
        SetStatus(remove ? "The control tower island taken out of the game folder." : $"The control tower island built: {ControlTowerIsland.IleFile} in the Island list.", StatusKind.Info);
    }
}
