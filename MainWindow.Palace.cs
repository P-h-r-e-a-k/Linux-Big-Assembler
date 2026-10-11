using System.IO;
using System.Windows;
using LBAAssembler.Terrain.Palace;

namespace LBAAssembler;

// Tools > LBA2: Palace island: Otringal's palace -- its sixteen rooms and the last room -- made a 3D building on an island of its own
// (Terrain/Palace: PALACE.ILE and PALACE.OBL), for a race track through it and round its rooftops -- added to the game folder, built
// again, or taken out.
public partial class MainWindow
{
    private const string PalaceTitle = "Palace island";

    private async void Lba2PalaceIsland_Click(object sender, RoutedEventArgs e)
    {
        if (TestEditsActive || Demo96Active)
        {
            MessageBox.Show(this, "Leave test edits or the 1996 demo first: the Palace island is added to the game folder itself.", PalaceTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!Lba2Engine.IsGameFolder(gameRoot))
        {
            MessageBox.Show(this, "The LBA2 game folder isn't set. Choose it under File > Settings.", PalaceTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        bool remove;
        if (!PalaceIsland.IsInstalled(gameRoot))
        {
            var ok = MessageBox.Show(this,
                "Make the Emperor's palace on Otringal an island of its own?\n\n" +
                "Its sixteen rooms and the last room (the chest and the window Twinsen leaves by) are joined as the doors between them put them, every block " +
                $"of them made {PalaceIsland.Across} times as wide and {PalaceIsland.Up} times as high -- room for the cars -- with a roof over each room, " +
                "on a paved island in the sea. It is drawn with Otringal's colours. No scenes or track yet: a race track through it comes later.\n\n" +
                $"Folder: {gameRoot}\nNew: {PalaceIsland.IleFile}, {PalaceIsland.OblFile} (nothing else changes; this menu takes them out again)",
                PalaceTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (ok != MessageBoxResult.OK) return;
            remove = false;
        }
        else
        {
            var choice = MessageBox.Show(this,
                "The Palace island is in this game folder.\n\nYes: build it again.\nNo: take it out of the folder.\nCancel: leave it.",
                PalaceTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice is MessageBoxResult.Cancel or MessageBoxResult.None) return;
            remove = choice == MessageBoxResult.No;
        }
        List<string> log;
        using (UiBusy.Progress(BusyPanel, BusyLabel, remove ? "Taking the Palace island out…" : "Building the Palace island…"))
        {
            var folder = gameRoot;
            try { log = await Task.Run(() => remove ? PalaceIsland.Remove(folder) : PalaceIsland.Add(folder)); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                DebugLog.Log($"Palace island: {(remove ? "remove" : "add")} failed: {error}");
                MessageBox.Show(this, $"Not done: {error.Message}", PalaceTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        foreach (var line in log) DebugLog.Log("Palace island: " + line);
        RaceTrackChanged();
        SetStatus(remove ? "The Palace island taken out of the game folder." : $"The Palace island built: {PalaceIsland.IleFile} in the Island list.", StatusKind.Info);
    }
}
