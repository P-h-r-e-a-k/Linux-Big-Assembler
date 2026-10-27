using System.Windows;

namespace LBAAssembler;

// File > Import heightmap / Export heightmap: the terrain editor's own (IslandEditorView, its Heightmap section in Build mode), on the island that
// is open -- a game island or a map's (File > New map / Open map: the map's workspace stands in for the game folder, so Save writes the island
// there, never into the real game folder). Build mode's terrain tools are opened for it, so the 3D view shows the import live.
public partial class MainWindow
{
    private void ImportHeightmap_Click(object sender, RoutedEventArgs e)
    {
        if (OpenTerrainForHeightmap(sender, e)) terrainEditor!.ImportHeightmap();
    }

    private void ExportHeightmap_Click(object sender, RoutedEventArgs e)
    {
        if (OpenTerrainForHeightmap(sender, e)) terrainEditor!.ExportHeightmap();
    }

    private bool OpenTerrainForHeightmap(object sender, RoutedEventArgs e)
    {
        if (!(editMode == EditMode.Build && terrainToolsActive && !buildDecorView)) IslandEditor_Click(sender, e);
        return terrainToolsActive && terrainEditor is { CurrentName: not null };
    }
}
