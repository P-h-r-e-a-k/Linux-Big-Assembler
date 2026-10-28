using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LBAAssembler.Terrain.Snow;

namespace LBAAssembler;

// Tools > LBA2: Snow island (Hamalayi): a new snowy island in the spirit of LBA1's Hamalayi Mountains (Terrain/Snow), built as a map of its
// own -- SNOWISLE.lbamap in the maps folder -- and opened in a map workspace to look round and Play (the user, 2026-10-22: "a separate mod to
// our racing mod, but for now let's build it into LBAAssembler for testing"). File > Install map into game puts it into the game's MAPS.
public partial class MainWindow
{
    private const string SnowTitle = "Snow island";

    private async void Lba2SnowIsland_Click(object? sender, RoutedEventArgs e)
    {
        // (where it goes: the maps folder -- the open map's own folder while one is open, as File > Save map as starts in)
        var path = Path.Combine(MapsFolder(), SnowIsland.FileName + Maps.LbaMap.Extension);
        if (!ReadyForMap()) return;
        if (!Lba2Configured)
        {
            MessageBox.Show(this, "The LBA2 game folder isn't set. Choose it under File > Settings.", SnowTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var lba1 = EditorSettings.Current.Lba1Directory;
        if (!LBAAssembler.Lba1.Lba1Game.IsInstalled(lba1))
        {
            MessageBox.Show(this, "The snow island's ground is textured with LBA1's own Hamalayi bricks: choose the LBA1 game folder under File > Settings first.", SnowTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var ok = MessageBox.Show(this,
            "Build the snow island?\n\n" +
            "A new island the Desert island's size (19 cubes) in the spirit of LBA1's Hamalayi Mountains: terraces of snow cut by ice cliffs, a harbour, " +
            "the Rabbibunnies' village of stone domes, a frozen pond with ice spikes, a canyon with a mine rail up to the prison gate, a ski slope with " +
            "its chair lift and slalom gates, puffy snow-laden trees, and snowmen. Its look (sky, sea, palette) is the Desert island's.\n\n" +
            $"It is a map of its own, {path}" + (File.Exists(path) ? " (built before: made again -- a .bak of the old one is kept)" : "") +
            ", opened in a copy of the game folder to look round and Play. File > Install map into game puts it into the game.",
            SnowTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (ok != MessageBoxResult.OK) return;

        SnowIsland.Built built;
        using (UiBusy.Progress(BusyPanel, BusyLabel, "Building the snow island…"))
        {
            var game = gameRoot;
            try { built = await Task.Run(() => SnowIsland.Build(lba1, game)); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException
                                              or Scenes.SceneEditException)
            {
                DebugLog.Log($"Snow island: build failed: {error}");
                MessageBox.Show(this, $"Not built: {error.Message}", SnowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        foreach (var line in built.Log) DebugLog.Log("Snow island: " + line);
        try
        {
            if (File.Exists(path)) File.Copy(path, path + ".bak", overwrite: true);
            built.Map.Save(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Not saved: {error.Message}", SnowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        OpenMapWorkspace(built.Map, path);
        if (openMapInstalled is { } installed && built.StartScene >= 0 && built.StartScene < installed.Scenes.Count)
        {
            // (the harbour's scene in view, where Twinsen starts: Play plays it)
            if (allSceneEntries.FirstOrDefault(s => s.Option.Index == installed.Scenes[built.StartScene]) is { } start)
                OpenLba2Area(IslandDisplayOf(start), installed.Scenes[built.StartScene]);
            SetStatus($"The snow island: {path}, scenes {installed.Scenes[0]}-{installed.Scenes[^1]}. Twinsen starts on the harbour's quay in scene {installed.Scenes[built.StartScene]} -- " +
                      "Play it there. File > Install map into game puts it into your game.", StatusKind.Success);
        }
    }
}
