using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LBAAssembler.Terrain.Temple;

namespace LBAAssembler;

// Tools > LBA2: Temple of Bú: LBA1's Temple of Bú -- its three scenes -- ported as an island of its own (Terrain/Temple: TEMPLE.ILE and
// TEMPLE.OBL, its palette and sky in RESS.HQR), drawn in LBA1's own colours so that from LBA1's angle it looks as LBA1 draws it -- added to
// the game folder from the LBA1 folder in the settings, built again, or taken out. (The race track's build makes it again with its
// Halloween race's doorways.)
public partial class MainWindow
{
    private const string TempleTitle = "Temple of Bú";

    private async void Lba2TempleIsland_Click(object? sender, RoutedEventArgs e)
    {
        if (TestEditsActive || Demo96Active)
        {
            MessageBox.Show(this, "Leave test edits or the 1996 demo first: the Temple of Bú is added to the game folder itself.", TempleTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!Lba2Engine.IsGameFolder(gameRoot))
        {
            MessageBox.Show(this, "The LBA2 game folder isn't set. Choose it under File > Settings.", TempleTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var lba1 = EditorSettings.Current.Lba1Directory;
        bool remove;
        if (!TempleIsland.IsInstalled(gameRoot))
        {
            if (!LBAAssembler.Lba1.Lba1Game.IsInstalled(lba1))
            {
                MessageBox.Show(this, "The Temple of Bú is made from LBA1's own scenes: choose the LBA1 game folder under File > Settings first.", TempleTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var ok = MessageBox.Show(this,
                "Port LBA1's Temple of Bú to LBA2 as an island of its own?\n\n" +
                $"Its three scenes are joined as their doors put them, every brick a box {TempleFrame.Scale} times its size each way, each face of it the brick's own " +
                "picture -- see-through where LBA1 draws nothing -- in LBA1's own colours (an island palette of its own, Otringal's with LBA1's colours written " +
                "in, so the game's people keep theirs), in the dark under a night sky. Seen from LBA1's angle it looks as LBA1 draws it. No scenes: the race " +
                "track window's Temple of Bú builds its Halloween race on it.\n\n" +
                $"Folder: {gameRoot}\nLBA1 folder: {lba1}\nNew: {TempleIsland.IleFile}, {TempleIsland.OblFile}, {TempleIsland.LayoutFile}; RESS.HQR's empty entries " +
                $"{TempleIsland.PaletteEntry} (the palette) and {TempleIsland.SkyEntry} (the sky) -- this menu takes them out again",
                TempleTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (ok != MessageBoxResult.OK) return;
            remove = false;
        }
        else
        {
            var choice = MessageBox.Show(this,
                "The Temple of Bú is in this game folder.\n\nYes: build it again.\nNo: take it out of the folder.\nCancel: leave it.",
                TempleTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice is MessageBoxResult.Cancel or MessageBoxResult.None) return;
            remove = choice == MessageBoxResult.No;
            if (!remove && !LBAAssembler.Lba1.Lba1Game.IsInstalled(lba1))
            {
                MessageBox.Show(this, "The Temple of Bú is made from LBA1's own scenes: choose the LBA1 game folder under File > Settings first.", TempleTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }
        List<string> log;
        using (UiBusy.Progress(BusyPanel, BusyLabel, remove ? "Taking the Temple of Bú out…" : "Building the Temple of Bú from LBA1's scenes…"))
        {
            var folder = gameRoot;
            try { log = await Task.Run(() => remove ? (TempleIsland.Remove(folder) ? new List<string> { "taken out" } : new List<string>()) : TempleIsland.Add(lba1, folder)); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                DebugLog.Log($"Temple of Bú: {(remove ? "remove" : "add")} failed: {error}");
                MessageBox.Show(this, $"Not done: {error.Message}", TempleTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        foreach (var line in log) DebugLog.Log("Temple of Bú: " + line);
        RaceTrackChanged();
        SetStatus(remove ? "The Temple of Bú taken out of the game folder." : $"The Temple of Bú built: {TempleIsland.IleFile} in the Island list.", StatusKind.Info);
    }
}
