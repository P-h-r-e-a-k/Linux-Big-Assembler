using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LBAAssembler.Lba1;
using LBAAssembler.Scenes;

namespace LBAAssembler;

// Build > Nuke: everything in the scene that is open blows up, and a flat empty scene is left (SceneNuke: what goes and what stays;
// NukeOverlay: the explosion). Saved to the game folder at once, as one undo step: Edit > Undo brings it all back. With "Join connected
// areas" ticked it takes everything connected to what is in view -- every scene of a joined map (either game), every cube of an LBA2
// island -- in a chain reaction out from the scene in focus; unticked, the one scene (on an island: its cube).
public partial class MainWindow
{
    private bool nuking;

    // Whether there is a scene open here to nuke.
    private bool CanNuke => editMode == EditMode.Build && (currentGame == GameKind.Lba1
        ? Lba1Configured && lba1CurrentTiles is { Count: > 0 }
        : Lba2Configured && (interiorSceneActive ? interiorSceneNumber >= 0 : currentIsland is not null));

    private async void Nuke_Click(object? sender, RoutedEventArgs e)
    {
        if (nuking) return;
        if (!CanNuke) { Refuse("Open a scene first (an LBA1 scene, an LBA2 interior or an island) in Build mode."); return; }
        SceneNuke nuke;
        var lba1 = currentGame == GameKind.Lba1;
        // (the island's file is written: unsaved terrain edits would be lost -- ask, as leaving the island does)
        if (!lba1 && !interiorSceneActive && !ConfirmTerrainDiscard()) return;
        try
        {
            if (PlanNuke(lba1) is not { } planned) return;
            nuke = planned;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)      // (SceneEditException is an IOException)
        {
            Refuse($"This scene can't be nuked: {error.Message}");
            return;
        }
        var pendingScripts = lba1 ? lba1Session.EditedScenes : scriptSession.EditedScenes;
        if (nuke.Scenes.FirstOrDefault(pendingScripts.Contains, -1) is var edited and >= 0)
        {
            Refuse($"Scene {edited} has unsaved script edits. Save or discard them first.");
            return;
        }

        var left = nuke.Exits > 0 ? $"Twinsen on flat ground, and the {nuke.Exits} exit{(nuke.Exits == 1 ? "" : "s")} to other scenes (so the game can still leave)" : "Twinsen on flat ground";
        var warnings = nuke.Warnings.Count == 0 ? "" : "\n\n" + string.Join("\n", nuke.Warnings);
        var chain = nuke.Scenes.Count > 1 && nuke.Stages.Count > 1
            ? $"\n\nThey go off in a chain reaction, out from scene {nuke.Scene}. (Untick Join connected areas to nuke {(lba1 || interiorSceneActive ? "one scene" : "one cube")} alone.)" : "";
        var question = $"Nuke {nuke.Where}?\n\nEverything in it goes: {nuke.Summary}.\nWhat is left: {left}.{warnings}{chain}\n\n" +
                       "It is saved to the game folder straight away. Edit > Undo (Ctrl+Z) brings it all back.";
        if (MessageBox.Show(this, question, "Nuke", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

        // open actor windows would be left editing actors that are gone (one with unsaved edits asks first, and may refuse)
        foreach (var window in openAttributesWindows.Values.ToList()) window.Close();
        foreach (var window in openLba1ActorWindows.Values.ToList()) window.Close();
        if (openAttributesWindows.Count > 0 || openLba1ActorWindows.Count > 0) { Refuse("Close the actor windows first."); return; }

        nuking = true;
        NukeButton.IsEnabled = false;
        var view = terrainShown ? (Control)TerrainMapHost : ViewportHost;
        NukeOverlay? overlay = null;
        try
        {
            // the view as it is, held on screen while the scene is emptied and drawn again underneath
            var charges = terrainShown ? null : NukeCharges(nuke);
            overlay = NukeOverlay.Cover(view, NukeOverlay.Snapshot(view));
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            nuke.Commit();
            ReloadAfterNuke(nuke, lba1);
            await WaitForViewToSettle();
            await overlay.Detonate(NukeOverlay.Snapshot(view), charges);
            overlay = null;
            var kept = nuke.Exits > 0 ? $" (its {nuke.Exits} exit{(nuke.Exits == 1 ? "" : "s")} kept)" : "";
            SetStatus($"Nuked {nuke.Where}: {nuke.Summary} gone{kept}. Edit > Undo brings it back.", StatusKind.Success);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            overlay?.Remove();
            DebugLog.Log($"MainWindow: nuke failed: {error}");
            MessageBox.Show(this, $"Not nuked: {error.Message}", "Nuke", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            nuking = false;
            NukeButton.IsEnabled = true;
            Keyboard.Focus(this);          // (the overlay had it for Esc, and is gone: Ctrl+Z is to work straight away)
        }
    }

    private void Refuse(string message) => MessageBox.Show(this, message, "Nuke", MessageBoxButton.OK, MessageBoxImage.Information);

    // What a nuke here takes (null: nothing to nuke), in the order its scenes go off.
    private SceneNuke? PlanNuke(bool lba1)
    {
        if (lba1)
        {
            if (Lba1FocusScene() is not { } focus || lba1CurrentTiles is not { } tiles) return null;
            return tiles.Select(t => t.Scene).Distinct().Count() > 1
                ? SceneNuke.ForLba1(EditorSettings.Current.Lba1Directory, ChainOrder(tiles, focus), DocumentTitle.Text)
                : SceneNuke.ForLba1(EditorSettings.Current.Lba1Directory, focus);
        }
        if (lba2JoinedView && lba2CurrentTiles is { Count: > 0 } joined)
            return SceneNuke.ForLba2Interiors(gameRoot, ChainOrder(joined, JoinedFocusScene(joined)), lba2AreaName);
        if (interiorSceneActive) return SceneNuke.ForLba2(gameRoot, interiorSceneNumber, null);

        // an island: the scene of the cube the camera is over (for the whole island, failing that, the nearest cube that has one)
        var island = Path.GetFileNameWithoutExtension(activeFile);
        var outdoor = allSceneEntries.Where(s => !s.IsInterior && string.Equals(s.IslandFile, island, StringComparison.OrdinalIgnoreCase)).ToList();
        if (outdoor.Count == 0) throw new SceneEditException($"{activeFile} has no scenes of its own to nuke: the game draws this island's scenes from another of its files.");
        int cubeX = (int)Math.Floor(targetX / 32768.0), cubeZ = (int)Math.Floor(targetZ / 32768.0);
        var here = SceneUnderCamera();
        if (!lba1JoinAreas)
            return here is { } entry ? SceneNuke.ForLba2(gameRoot, entry.Option.Index, activeFile)
                : throw new SceneEditException("There is no scene on the cube under the middle of the view. Move the view over the part of the island to nuke.");
        here ??= outdoor.MinBy(s => Math.Max(Math.Abs(s.CubeX - cubeX), Math.Abs(s.CubeY - cubeZ)));
        return SceneNuke.ForLba2Island(gameRoot, activeFile, here!.Option.Index, wholeIsland: true);
    }

    // A joined map's scenes, the one in focus first, then out from it (by how far each tile's middle is from the focus's).
    private List<int> ChainOrder(IReadOnlyList<Lba1AreaTile> tiles, int focus)
    {
        var from = TileMiddle(tiles.First(t => t.Scene == focus));
        double Distance(Lba1AreaTile t) { var m = TileMiddle(t); return Math.Sqrt(Math.Pow(m.X - from.X, 2) + Math.Pow(m.Z - from.Z, 2)); }
        return tiles.GroupBy(t => t.Scene).OrderBy(g => g.Key == focus ? -1 : g.Min(Distance)).ThenBy(g => g.Key).Select(g => g.Key).ToList();
    }

    // The middle of what a joined map's tile draws (world units): of the grid columns it has anything in, at their mean top -- a scene's
    // rooms are often in a corner of its grid. (Cell N is centred on N * 512: the engines' own rule, Lba1AreaTile.HoldsPoint.)
    private (double X, double Y, double Z) TileMiddle(Lba1AreaTile t)
    {
        var tops = new List<(int X, int Z, int Top)>();
        try
        {
            if (currentGame == GameKind.Lba1 && lba1Game is { } game)
            {
                var top = game.ColumnTops(t.Scene).TopY;
                for (var z = 0; z < 64; z++)
                for (var x = 0; x < 64; x++)
                    if (top[z * 64 + x] >= 0 && t.Holds(x, z)) tops.Add((x, z, top[z * 64 + x]));
            }
            else if (lba2Interiors is { } interiors)
                tops.AddRange(interiors.Placements(t).GroupBy(p => (p.X, p.Z)).Select(g => (g.Key.X, g.Key.Z, g.Max(p => p.Y))));
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException)
        {
            DebugLog.Log($"MainWindow: nuke: scene {t.Scene}'s columns: {error.Message}");
        }
        if (tops.Count == 0)
            return t.Window is { } w ? (t.OffsetX + (w.X0 + w.X1) * 256.0, t.OffsetY + 256, t.OffsetZ + (w.Z0 + w.Z1) * 256.0) : (t.OffsetX + 32 * 512.0, t.OffsetY + 256, t.OffsetZ + 32 * 512.0);
        return (t.OffsetX + tops.Average(c => c.X) * 512, t.OffsetY + (tops.Average(c => c.Top) + 1) * 256, t.OffsetZ + tops.Average(c => c.Z) * 512);
    }

    // The joined LBA2 map's scene in focus: the tile whose middle is nearest the middle of the view.
    private int JoinedFocusScene(IReadOnlyList<Lba1AreaTile> tiles)
    {
        if (lba2JoinedImage is not { } image) return tiles[0].Scene;
        return tiles.MinBy(t =>
        {
            var m = TileMiddle(t);
            var p = image.Project(m.X, m.Y, m.Z);
            return Math.Pow(p.X - interiorCenter.X, 2) + Math.Pow(p.Y - interiorCenter.Y, 2);
        })!.Scene;
    }

    // The chain reaction's charges (where each stage is in the view, and when it goes off: a ring of the chain at a time, the whole chain
    // in under three seconds), or null for a nuke of one scene. Taken from the view as it is, before the nuke.
    private List<NukeOverlay.Charge>? NukeCharges(SceneNuke nuke)
    {
        if (nuke.Stages.Count < 2) return null;
        var step = Math.Min(0.45, 2.6 / Math.Max(1, nuke.Stages.Max(s => s.Ring)));
        double vw = ViewportHost.ActualWidth, vh = ViewportHost.ActualHeight;
        var image = currentGame == GameKind.Lba1 ? lba1ShownImage : lba2JoinedImage;
        var tiles = currentGame == GameKind.Lba1 ? lba1CurrentTiles : lba2CurrentTiles;
        var charges = new List<NukeOverlay.Charge>();
        foreach (var stage in nuke.Stages)
        {
            var delay = stage.Ring * step;
            if (stage.CubeX >= 0)
            {
                // (an island cube: its middle, through the camera of the 3D view)
                if (cameraModel is { } model && model.Project(stage.CubeX * 32768.0 + 16384, stage.Height, stage.CubeY * 32768.0 + 16384, out var u, out var v))
                    charges.Add(new(new Point(u * vw / model.FrameWidth, v * vh / model.FrameHeight), delay));
                continue;
            }
            if (image is null || tiles is null) continue;
            foreach (var t in tiles.Where(t => stage.Scenes.Contains(t.Scene)))
            {
                var m = TileMiddle(t);
                var p = image.Project(m.X, m.Y, m.Z);
                charges.Add(new(new Point((p.X - interiorCenter.X) * interiorZoom + vw / 2, (p.Y - interiorCenter.Y) * interiorZoom + vh / 2), delay));
            }
        }
        return charges.Count > 1 ? charges : null;
    }

    // The views after the nuke's files were written (as after an undo step: RunHistoryStep).
    private void ReloadAfterNuke(SceneNuke nuke, bool lba1)
    {
        zoneCache.Clear();
        if (lba1)
        {
            foreach (var scene in nuke.Scenes) lba1Session.ForgetScene(scene);
            ReloadLba1AfterEdit();
        }
        else
        {
            foreach (var scene in nuke.Scenes) scriptSession.ForgetScene(scene);
            ResetLba2Areas();          // (the joined interior maps are drawn from their own copy of the scenes and grids)
            if (lba2JoinedView) RedrawLba2JoinedMap();
            else if (interiorSceneActive) ShowInteriorScene(interiorSceneNumber, keepView: true);
            else if (nuke.ChangesIsland) ReloadIslandFromDisk();
            else
            {
                InvalidateNativeIsland();
                actorMarkersIsland = null;
                if (nativeViewActive) RenderNativeCamera();
            }
        }
        RefreshZoneListIfVisible();
    }

    // The island's file changed on disk behind the views (a nuke, or undoing one): the terrain editor, the 3D view, the minimap and the
    // actor dots read it again.
    private void ReloadIslandFromDisk()
    {
        if (currentGame != GameKind.Lba2 || interiorSceneActive || currentIsland is null) return;
        // (the terrain editor holds its own copy of the island; it was saved or thrown away before the file changed)
        if (terrainEditor is not null && (terrainToolsActive || string.Equals(terrainEditor.CurrentName, activeFile, StringComparison.OrdinalIgnoreCase)))
            terrainEditor.Open(activeFile, reload: true);
        InvalidateNativeIsland();
        UpdateLive();
        if (live is not null) PushLive();      // (the live preview's island is a copy of its own, which Sync leaves alone)
        actorMarkersIsland = null;
        sceneViewStale = true;
        if (!terrainShown) RefreshSceneViewIfStale();
    }

    // Waits for the view to show the scene as it now is: the island's 3D view is drawn on a render thread and arrives a moment later
    // (an interior, an LBA1 scene and the terrain map are drawn at once). Gives up after a few seconds -- the animation then reveals
    // whatever is there.
    private async Task WaitForViewToSettle()
    {
        var start = Environment.TickCount64;
        while (nativeViewActive && !interiorSceneActive && !terrainShown && Environment.TickCount64 - start < 5000)
        {
            await Task.Delay(40);
            bool busy;
            lock (nativeRenderGate) busy = nativeRenderInFlight || nativeRenderDirty;
            if (!busy) break;
        }
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
    }
}
