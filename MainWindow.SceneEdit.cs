using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ellipse = System.Windows.Shapes.Ellipse;
using Polygon = System.Windows.Shapes.Polygon;
using LBAAssembler.Lba1;
using LBAAssembler.Scenes;
using LBAAssembler.Terrain;

namespace LBAAssembler;

// Editing a scene's actors, zones and track points in the main window's view (Build > Actors and zones, and every interior and LBA1
// scene in Build): the scene panel (ScenePanel) lists and inspects them; here they are picked, dragged and placed where the view shows
// them -- an island's 3D view, an interior's or an LBA1 scene's isometric picture, a joined map. (The scene editor windows, which drew a
// scene on a plan of their own, went on 2026-10-24.)
public partial class MainWindow : IScenePanelHost
{
    private ScenePanel? scenePanel;
    private (int Scene, int Index)? selectedPoint;
    private Action<int, int, int, int>? scenePlace;
    private FrameworkElement? scenePlaceGhost;
    private Dictionary<int, (int Scene, int Slot)> lastNativeActorScenes = new();        // native actor index -> its scene and slot (exteriors)
    private readonly Dictionary<Lba1AreaTile, int[]> sceneTops = new();                  // column tops of a joined map's tiles, for placing
    private IslandFile? sceneGround;
    private (string Path, DateTime Written)? sceneGroundOf;

    // The panel is there whenever Build edits the scene rather than the island's ground: "Actors and zones", any interior, any LBA1 scene.
    private bool ScenePanelActive => editMode == EditMode.Build && !terrainToolsActive && scenePanel is not null && SceneStoreInView() is not null;

    private void CreateScenePanel()
    {
        scenePanel = new ScenePanel(this);
        ScenePanelHost.Content = scenePanel;
    }

    // ---- IScenePanelHost

    Window IScenePanelHost.HostWindow => this;

    void IScenePanelHost.SetStatus(string text, bool warning) => SetStatus(text, warning ? StatusKind.Warning : StatusKind.Info);

    IReadOnlyList<(int Scene, string Label)> IScenePanelHost.ScenesInView()
    {
        if (currentGame == GameKind.Lba1 && lba1CurrentTiles is { } lba1Tiles)
            return lba1Tiles.Select(t => t.Scene).Distinct().Select(s => (s, $"{s}: {lba1Game?.Description(s) ?? "scene"}")).ToList();
        if (currentGame != GameKind.Lba2) return Array.Empty<(int, string)>();
        if (lba2JoinedView && lba2CurrentTiles is { } tiles)
            return tiles.Select(t => t.Scene).Distinct().Select(s => (s, allSceneEntries.FirstOrDefault(e => e.Option.Index == s)?.Option.Display ?? $"{s}")).ToList();
        if (interiorSceneActive) return interiorSceneNumber >= 0 ? new[] { (interiorSceneNumber, allSceneEntries.FirstOrDefault(e => e.Option.Index == interiorSceneNumber)?.Option.Display ?? $"{interiorSceneNumber}") } : Array.Empty<(int, string)>();
        var island = SceneIsland(activeFile);
        return allSceneEntries.Where(e => !e.IsInterior && string.Equals(e.IslandFile, island, StringComparison.OrdinalIgnoreCase)).Select(e => (e.Option.Index, e.Option.Display)).ToList();
    }

    void IScenePanelHost.ShowSelection(int scene, SceneItemKind kind, int index, bool center)
    {
        var game = currentGame == GameKind.Lba1 ? 1 : 2;
        selectedPoint = kind == SceneItemKind.Point ? (scene, index) : null;
        if (kind == SceneItemKind.Actor) selectedActorIndex = ViewKeyOfActor(scene, index);
        if (kind == SceneItemKind.Zone) SelectZone(new ZoneRef(game, scene, index), showTab: false);
        else if (selectedZoneRef is not null) SelectZone(null, showTab: false);
        RefreshActorOverlayForSelection();
        // (the view moves only to bring it into sight: something just placed under the pointer, or already on screen, stays put)
        if (center && ScenePointOf(scene, kind, index) is { } at && !OnScreen(scene, at)) CenterViewOn(scene, at);
    }

    private bool OnScreen(int scene, (double X, double Y, double Z) at)
    {
        if (!TryProjectScenePoint(scene, at.X, at.Y, at.Z, out var view)) return false;
        const double margin = 40;
        return view.X >= margin && view.Y >= margin && view.X <= ActorMarkerCanvas.ActualWidth - margin && view.Y <= ActorMarkerCanvas.ActualHeight - margin;
    }

    void IScenePanelHost.BeginPlacement(string hint, Action<int, int, int, int> place)
    {
        scenePlace = place;
        ViewportHost.Cursor = Cursors.Cross;
        // (the markers let the click through to the ground under them)
        ActorMarkerCanvas.IsHitTestVisible = false;
        SetStatus(hint);
    }

    void IScenePanelHost.EndPlacement() => EndScenePlacement();

    private void EndScenePlacement()
    {
        scenePlace = null;
        ViewportHost.Cursor = null;
        ActorMarkerCanvas.IsHitTestVisible = true;
        if (scenePlaceGhost is not null) { BrushOverlayCanvas.Children.Remove(scenePlaceGhost); scenePlaceGhost = null; }
    }

    void IScenePanelHost.Written(SceneGame game, IReadOnlyList<int> scenes) => ReloadScenesAfterWrite(game, scenes);

    void IScenePanelHost.OpenScript(int scene, int actor)
    {
        if (currentGame == GameKind.Lba1) EditLba1ActorFromEditor(scene, actor, script: true);
        else EditLba2ScriptFromEditor(scene, actor);
    }

    bool IScenePanelHost.CanOpenAttributes(int scene, int actor) => currentGame == GameKind.Lba1 || (!lba2JoinedView && ViewKeyOfActor(scene, actor) is not null);

    void IScenePanelHost.OpenAttributes(int scene, int actor)
    {
        if (currentGame == GameKind.Lba1) { EditLba1ActorFromEditor(scene, actor, script: false); return; }
        if (ViewKeyOfActor(scene, actor) is { } key) OpenActorAttributesWindow(key, edit: true);
    }

    void IScenePanelHost.GoToScene(int scene)
    {
        if (currentGame == GameKind.Lba1) { SetStatus(GoToLba1Scene(scene)); return; }
        if (allSceneEntries.FirstOrDefault(e => e.Option.Index == scene) is { IsInterior: false } outdoor
            && string.Equals(outdoor.IslandFile, SceneIsland(activeFile), StringComparison.OrdinalIgnoreCase))
        {
            FocusExteriorScene(outdoor);
            if (SceneStoreInView() is { } store) scenePanel?.Show(store, scene);
            return;
        }
        SetStatus(GoToLba2Scene(scene));
    }

    // ---- which scenes, which store

    private SceneStore? SceneStoreInView()
    {
        if (currentGame == GameKind.Lba1)
            return lba1CurrentTiles is { Count: > 0 } && Lba1Game.IsInstalled(EditorSettings.Current.Lba1Directory) ? new SceneStore(SceneGame.Lba1, EditorSettings.Current.Lba1Directory) : null;
        if (currentGame != GameKind.Lba2 || !Lba2Engine.IsGameFolder(gameRoot)) return null;
        return interiorSceneActive || lba2JoinedView || currentIsland is not null ? new SceneStore(SceneGame.Lba2, gameRoot) : null;
    }

    // Keeps the panel on a scene of the view: the one it shows while it is still there, else the view's own (the scene under the camera,
    // the interior, the first of a joined map).
    private void SyncScenePanelToView()
    {
        if (scenePanel is null || editMode != EditMode.Build || terrainToolsActive) return;
        if (SceneStoreInView() is not { } store) { scenePanel.Clear(); return; }
        var inView = ((IScenePanelHost)this).ScenesInView();
        if (inView.Count == 0) { scenePanel.Clear(); return; }
        var keep = scenePanel.Game == store.Game && inView.Any(s => s.Scene == scenePanel.Scene);
        var scene = keep ? scenePanel.Scene
            : currentGame == GameKind.Lba1 ? lba1CurrentTiles![0].Scene
            : lba2JoinedView ? lba2CurrentTiles![0].Scene
            : Lba2SceneToPlay();
        if (inView.All(s => s.Scene != scene)) scene = inView[0].Scene;
        if (!keep) sceneTops.Clear();
        scenePanel.Show(store, scene);
    }

    // The view's key of an actor (what its marker is tagged with) and back.
    private int? ViewKeyOfActor(int scene, int index)
    {
        if (currentGame == GameKind.Lba1 || lba2JoinedView) return scene * 1000 + index;
        if (interiorSceneActive) return scene == interiorSceneNumber && index > 0 ? index - 1 : null;
        foreach (var (key, at) in lastNativeActorScenes) if (at.Scene == scene && at.Slot == index) return key;
        return null;
    }

    private (int Scene, int Index)? SceneOfActorKey(int key)
    {
        if (currentGame == GameKind.Lba1 || lba2JoinedView) return (key / 1000, key % 1000);
        if (interiorSceneActive) return interiorSceneNumber >= 0 ? (interiorSceneNumber, key + 1) : null;
        return lastNativeActorScenes.TryGetValue(key, out var at) ? at : null;
    }

    // An actor, zone or track point picked in the view: the panel shows it (Build), and the panel's scene follows.
    private void SceneItemPickedInView(int scene, SceneItemKind kind, int index)
    {
        if (!ScenePanelActive || SceneStoreInView() is not { } store) return;
        scenePanel!.SelectFromView(store, scene, kind, index);
        selectedPoint = kind == SceneItemKind.Point ? (scene, index) : null;
    }

    // ---- from the scene to the view and back

    private (double X, double Z)? ExteriorOrigin(int scene)
        => allSceneEntries.FirstOrDefault(e => e.Option.Index == scene && !e.IsInterior) is { } entry ? (entry.CubeX * 32768.0, entry.CubeY * 32768.0) : null;

    private Lba1AreaTile? TileOf(int scene, double x, double z)
    {
        var tiles = currentGame == GameKind.Lba1 ? lba1CurrentTiles : lba2JoinedView ? lba2CurrentTiles : null;
        return tiles?.FirstOrDefault(t => t.Scene == scene && t.HoldsPoint((int)x, (int)z)) ?? tiles?.FirstOrDefault(t => t.Scene == scene);
    }

    private Lba1SceneImage? ManagedImage => currentGame == GameKind.Lba1 ? lba1ShownImage : lba2JoinedView ? lba2JoinedImage : null;

    // Where a point of a scene (its own coordinates) is in the view (ActorMarkerCanvas pixels).
    private bool TryProjectScenePoint(int scene, double x, double y, double z, out Point view)
    {
        view = default;
        if (ManagedImage is { } image)
        {
            if (TileOf(scene, x, z) is not { } tile) return false;
            view = InteriorCanvasToView(image.Project(x + tile.OffsetX, y + tile.OffsetY, z + tile.OffsetZ));
            return true;
        }
        if (interiorSceneActive)
        {
            if (scene != interiorSceneNumber || nativeRenderer.RendererLibrary is not { } library || !library.ProjectInteriorPoint((int)x, (int)y, (int)z, out var cx, out var cy)) return false;
            view = InteriorCanvasToView(new Point(cx, cy));
            return true;
        }
        if (!nativeViewActive || cameraModel is not { } model || ExteriorOrigin(scene) is not { } origin) return false;
        if (!model.Project(origin.X + x, y, origin.Z + z, out var u, out var v)) return false;
        view = ToWidget(model, u, v);
        return true;
    }

    // The point of a scene (its own coordinates) at height `y` that the view shows at `view`.
    private bool TryViewToScenePoint(int scene, Point view, double y, out double x, out double z)
    {
        x = z = 0;
        if (ManagedImage is { } image)
        {
            if (interiorZoom <= 0 || TileOf(scene, 0, 0) is not { } anyTile) return false;
            var tiles = currentGame == GameKind.Lba1 ? lba1CurrentTiles! : lba2CurrentTiles!;
            var canvas = InteriorCanvasOf(view);
            double u = (canvas.X - image.OriginX) * 512 / 24;
            // (the tile the point lands in, when the scene has several pieces: the one holding it, else the first)
            foreach (var tile in tiles.Where(t => t.Scene == scene).Append(anyTile))
            {
                var v = (canvas.Y - image.OriginY + (y + tile.OffsetY) * 15 / 256) * 512 / 12;
                double sx = (u + v) / 2 - tile.OffsetX, sz = (v - u) / 2 - tile.OffsetZ;
                if (tile.HoldsPoint((int)sx, (int)sz) || ReferenceEquals(tile, anyTile)) { x = sx; z = sz; return true; }
            }
            return false;
        }
        if (interiorSceneActive)
        {
            if (scene != interiorSceneNumber || nativeRenderer.RendererLibrary is not { } library || interiorZoom <= 0) return false;
            if (!SolveInteriorGround(library, InteriorCanvasOf(view), (int)y, out var ix, out var iz)) return false;
            x = ix; z = iz;
            return true;
        }
        if (cameraModel is not { } model || ExteriorOrigin(scene) is not { } origin) return false;
        double w = TerrainViewport.ActualWidth, h = TerrainViewport.ActualHeight;
        if (w < 1 || h < 1 || !model.RayToPlane(view.X * model.FrameWidth / w, view.Y * model.FrameHeight / h, y, out var wx, out var wz)) return false;
        x = wx - origin.X; z = wz - origin.Z;
        return true;
    }

    // The ground under a point of the view: the scene there and the point on its floor (its own coordinates).
    private (int Scene, int X, int Y, int Z)? PickSceneGround(Point view)
    {
        if (ManagedImage is { } image)
        {
            var tiles = currentGame == GameKind.Lba1 ? lba1CurrentTiles : lba2CurrentTiles;
            if (tiles is null || interiorZoom <= 0) return null;
            Func<Lba1AreaTile, int[]> topsOf;
            if (currentGame == GameKind.Lba1) { var game = lba1Game; if (game is null) return null; topsOf = t => game.ColumnTops(t.Scene).TopY; }
            else { var interiors = lba2Interiors; if (interiors is null) return null; topsOf = t => TopsOf(interiors.Placements(t)); }
            return PickManagedGroundOn(image, tiles, topsOf, sceneTops, InteriorCanvasOf(view)) is { } hit
                ? (hit.Tile.Scene, (int)Math.Round(hit.X), (int)Math.Round(hit.Y), (int)Math.Round(hit.Z)) : null;
        }
        if (interiorSceneActive)
        {
            if (nativeRenderer.RendererLibrary is not { } library || interiorSceneNumber < 0) return null;
            return PickInteriorFloor(library, view) is { } floor ? (interiorSceneNumber, floor.X, floor.Y, floor.Z) : null;
        }
        if (!nativeViewActive || currentIsland is null || SceneGround() is not { } ground) return null;
        var (lo, hi) = IslandOps.HeightRange(ground);
        if (!PickGroundWith(view, lo, hi, (x, z) => IslandOps.Altitude(ground, x, z) ?? double.NaN, out var wx, out var wz)) return null;
        if (IslandOps.Altitude(ground, wx, wz) is not { } altitude) return null;
        var island = SceneIsland(activeFile);
        int cubeX = (int)Math.Floor(wx / 32768.0), cubeZ = (int)Math.Floor(wz / 32768.0);
        if (allSceneEntries.FirstOrDefault(s => !s.IsInterior && s.CubeX == cubeX && s.CubeY == cubeZ && string.Equals(s.IslandFile, island, StringComparison.OrdinalIgnoreCase)) is not { } there) return null;
        return (there.Option.Index, Math.Clamp((int)Math.Round(wx - cubeX * 32768.0), 0, 32767), (int)Math.Round(altitude), Math.Clamp((int)Math.Round(wz - cubeZ * 32768.0), 0, 32767));
    }

    // The island's ground, read from its file (again when the file was written).
    private IslandFile? SceneGround()
    {
        var path = Path.Combine(gameRoot, activeFile);
        try
        {
            var written = File.GetLastWriteTimeUtc(path);
            if (sceneGround is null || sceneGroundOf is not { } of || of.Path != path || of.Written != written)
            {
                sceneGround = IslandFile.Load(path);
                sceneGroundOf = (path, written);
            }
            return sceneGround;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
        {
            DebugLog.Log($"MainWindow: no ground to place on ({path}): {error.Message}");
            return null;
        }
    }

    // A scene item's own position (an actor's feet, a track point, a zone's middle), in its scene's coordinates.
    private (double X, double Y, double Z)? ScenePointOf(int scene, SceneItemKind kind, int index)
    {
        if (scenePanel?.Model is not { } model || scenePanel.Scene != scene) return null;
        switch (kind)
        {
            case SceneItemKind.Actor when index >= 0 && index < model.Actors.Count: var a = model.Actors[index]; return (a.X, a.Y, a.Z);
            case SceneItemKind.Point when index >= 0 && index < model.TrackPoints.Count: var p = model.TrackPoints[index]; return (p.X, p.Y, p.Z);
            case SceneItemKind.Zone when index >= 0 && index < model.Zones.Count: var z = model.Zones[index]; return ((z.X0 + z.X1) / 2.0, Math.Min(z.Y0, z.Y1), (z.Z0 + z.Z1) / 2.0);
            default: return null;
        }
    }

    // Brings a scene point into the middle of the view.
    private void CenterViewOn(int scene, (double X, double Y, double Z) at)
    {
        if (ManagedImage is { } image)
        {
            if (TileOf(scene, at.X, at.Z) is not { } tile) return;
            interiorCenter = image.Project(at.X + tile.OffsetX, at.Y + tile.OffsetY, at.Z + tile.OffsetZ);
            ApplyInteriorView();
            return;
        }
        if (interiorSceneActive)
        {
            if (scene == interiorSceneNumber && nativeRenderer.RendererLibrary is { } library && library.ProjectInteriorPoint((int)at.X, (int)at.Y, (int)at.Z, out var cx, out var cy))
            {
                interiorCenter = new Point(cx, cy);
                ApplyInteriorView();
            }
            return;
        }
        if (!nativeViewActive || ExteriorOrigin(scene) is not { } origin) return;
        targetX = origin.X + at.X; targetZ = origin.Z + at.Z;
        SyncPanScrollBars();
        RenderNativeCamera();
    }

    // ---- the track points, drawn in the view while the panel is there

    private void DrawSceneItems()
    {
        if (!ScenePanelActive || scenePanel?.Model is not { } model) return;
        var scene = scenePanel.Scene;
        for (var i = 0; i < model.TrackPoints.Count; i++)
        {
            var p = model.TrackPoints[i];
            if (!TryProjectScenePoint(scene, p.X, p.Y, p.Z, out var at)) continue;
            if (at.X < -20 || at.Y < -20 || at.X > ActorMarkerCanvas.ActualWidth + 20 || at.Y > ActorMarkerCanvas.ActualHeight + 20) continue;
            var selected = selectedPoint == (scene, i);
            var diamond = new Polygon
            {
                Points = new PointCollection { new(0, -7), new(7, 0), new(0, 7), new(-7, 0) },
                Fill = selected ? Brushes.White : Brushes.Gold, Stroke = Brushes.Black, StrokeThickness = 1,
                Cursor = Cursors.Hand, Tag = (scene, i), ToolTip = $"Track point {i} ({p.X}, {p.Y}, {p.Z})",
            };
            diamond.MouseLeftButtonDown += ScenePoint_MouseLeftButtonDown;
            Canvas.SetLeft(diamond, at.X); Canvas.SetTop(diamond, at.Y);
            ActorMarkerCanvas.Children.Add(diamond);
            var label = new TextBlock { Text = i.ToString(), Foreground = selected ? Brushes.White : Brushes.Gold, FontFamily = new FontFamily("Consolas"), FontSize = 10, IsHitTestVisible = false };
            Canvas.SetLeft(label, at.X + 8); Canvas.SetTop(label, at.Y - 15);
            ActorMarkerCanvas.Children.Add(label);
        }
    }

    private void ScenePoint_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not ValueTuple<int, int> tag) return;
        e.Handled = true;
        selectedPoint = tag;
        SceneItemPickedInView(tag.Item1, SceneItemKind.Point, tag.Item2);
        RefreshActorOverlayForSelection();
        BeginSceneDrag(SceneItemKind.Point, tag.Item1, tag.Item2, e.GetPosition(ActorMarkerCanvas));
    }

    // ---- dragging a track point, a zone (or a joined map's actor) in the view: its new place is written when it is let go

    private sealed class SceneDrag
    {
        public SceneItemKind Kind;
        public int Scene, Index;
        public Point Start;
        public bool Moved;
        public (double X, double Y, double Z) From;                 // the point moved: the item's own (a zone's least corner)
        public (double X, double Z) GrabbedAt;                      // the pointer at the start, put at the item's height
        public (int X, int Y, int Z)? To;
        public FrameworkElement? Ghost;
    }
    private SceneDrag? sceneDrag;

    private void BeginSceneDrag(SceneItemKind kind, int scene, int index, Point at)
    {
        if (!ScenePanelActive || scenePanel?.Model is not { } model || scenePanel.Scene != scene) return;
        (double X, double Y, double Z) from;
        switch (kind)
        {
            case SceneItemKind.Point when index < model.TrackPoints.Count: var p = model.TrackPoints[index]; from = (p.X, p.Y, p.Z); break;
            case SceneItemKind.Zone when index < model.Zones.Count: var z = model.Zones[index]; from = (Math.Min(z.X0, z.X1), Math.Min(z.Y0, z.Y1), Math.Min(z.Z0, z.Z1)); break;
            case SceneItemKind.Actor when index < model.Actors.Count: var a = model.Actors[index]; from = (a.X, a.Y, a.Z); break;
            default: return;
        }
        if (!TryViewToScenePoint(scene, at, from.Y, out var gx, out var gz)) return;
        sceneDrag = new SceneDrag { Kind = kind, Scene = scene, Index = index, Start = at, From = from, GrabbedAt = (gx, gz) };
        ActorMarkerCanvas.CaptureMouse();
    }

    private void UpdateSceneDrag(Point at)
    {
        if (sceneDrag is not { } drag) return;
        if (!drag.Moved && Math.Abs(at.X - drag.Start.X) <= ActorDragThresholdPx && Math.Abs(at.Y - drag.Start.Y) <= ActorDragThresholdPx) return;
        drag.Moved = true;
        if (!TryViewToScenePoint(drag.Scene, at, drag.From.Y, out var x, out var z)) return;
        var to = ((int)Math.Round(drag.From.X + x - drag.GrabbedAt.X), (int)Math.Round(drag.From.Y), (int)Math.Round(drag.From.Z + z - drag.GrabbedAt.Z));
        drag.To = to;
        if (drag.Ghost is not null) BrushOverlayCanvas.Children.Remove(drag.Ghost);
        drag.Ghost = SceneDragGhost(drag, to);
        if (drag.Ghost is not null) BrushOverlayCanvas.Children.Add(drag.Ghost);
        SetStatus($"Moving {drag.Kind.ToString().ToLowerInvariant()} {drag.Index} to ({to.Item1}, {to.Item2}, {to.Item3}): let go to put it there.");
    }

    // What follows the pointer: the point's diamond, the zone's box, a ring for an actor.
    private FrameworkElement? SceneDragGhost(SceneDrag drag, (int X, int Y, int Z) to)
    {
        if (drag.Kind == SceneItemKind.Zone && scenePanel?.Model is { } model && drag.Index < model.Zones.Count)
        {
            var zone = model.Zones[drag.Index];
            int sx = Math.Abs(zone.X1 - zone.X0), sy = Math.Abs(zone.Y1 - zone.Y0), sz = Math.Abs(zone.Z1 - zone.Z0);
            var corners = ZoneStyle.Corners(to.X, to.Y, to.Z, to.X + sx, to.Y + sy, to.Z + sz);
            var points = new Point[8];
            for (var c = 0; c < 8; c++) if (!TryProjectScenePoint(drag.Scene, corners[c].X, corners[c].Y, corners[c].Z, out points[c])) return null;
            return new System.Windows.Shapes.Path { Data = ZoneStyle.Wireframe(new ProjectedZone(zone.Type, zone.Num, points), p => p), Stroke = Brushes.Yellow, StrokeThickness = 2.5, IsHitTestVisible = false };
        }
        if (!TryProjectScenePoint(drag.Scene, to.X, to.Y, to.Z, out var at)) return null;
        FrameworkElement ghost = drag.Kind == SceneItemKind.Point
            ? new Polygon { Points = new PointCollection { new(0, -8), new(8, 0), new(0, 8), new(-8, 0) }, Fill = new SolidColorBrush(Color.FromArgb(150, 255, 255, 0)), Stroke = Brushes.Yellow, StrokeThickness = 2, IsHitTestVisible = false }
            : new Ellipse { Width = 28, Height = 28, Margin = new Thickness(-14, -14, 0, 0), Stroke = Brushes.Yellow, StrokeThickness = 3, Fill = new SolidColorBrush(Color.FromArgb(70, 255, 255, 0)), IsHitTestVisible = false };
        Canvas.SetLeft(ghost, at.X); Canvas.SetTop(ghost, at.Y);
        return ghost;
    }

    private void EndSceneDrag()
    {
        if (sceneDrag is not { } drag) return;
        sceneDrag = null;
        ActorMarkerCanvas.ReleaseMouseCapture();
        if (drag.Ghost is not null) BrushOverlayCanvas.Children.Remove(drag.Ghost);
        if (drag.Moved && drag.To is { } to && scenePanel is not null) scenePanel.MoveTo(drag.Kind, drag.Index, to.X, to.Y, to.Z);
    }

    // ---- placing (Add in the panel, then a click on the ground)

    private bool HandleScenePlacementDown(MouseButtonEventArgs e)
    {
        if (scenePlace is not { } place) return false;
        e.Handled = true;
        if (e.ChangedButton == MouseButton.Right) { scenePanel?.CancelPlacement(); return true; }
        if (e.ChangedButton != MouseButton.Left) return true;
        if (PickSceneGround(e.GetPosition(ActorMarkerCanvas)) is not { } hit)
        {
            SetStatus("That isn't on the ground of a scene in the view: click the floor or the ground.", StatusKind.Warning);
            return true;
        }
        EndScenePlacement();
        place(hit.Scene, hit.X, hit.Y, hit.Z);
        return true;
    }

    private bool HandleScenePlacementMove(MouseEventArgs e)
    {
        if (scenePlace is null) return false;
        var at = e.GetPosition(ActorMarkerCanvas);
        if (scenePlaceGhost is not null) { BrushOverlayCanvas.Children.Remove(scenePlaceGhost); scenePlaceGhost = null; }
        if (PickSceneGround(at) is not { } hit || !TryProjectScenePoint(hit.Scene, hit.X, hit.Y, hit.Z, out var on)) return true;
        scenePlaceGhost = new Ellipse { Width = 22, Height = 11, Margin = new Thickness(-11, -5.5, 0, 0), Stroke = Brushes.Yellow, StrokeThickness = 2.5, Fill = new SolidColorBrush(Color.FromArgb(70, 255, 255, 0)), IsHitTestVisible = false };
        Canvas.SetLeft(scenePlaceGhost, on.X); Canvas.SetTop(scenePlaceGhost, on.Y);
        BrushOverlayCanvas.Children.Add(scenePlaceGhost);
        FileLabel.Text = $"Scene {hit.Scene}: ({hit.X}, {hit.Y}, {hit.Z}) -- click to place here, Esc or right-click cancels.";
        return true;
    }

    // Right-click on the ground in Build: add an actor, a zone or a track point right there.
    private bool ShowSceneGroundMenu(MouseButtonEventArgs e)
    {
        if (!ScenePanelActive || scenePanel is null) return false;
        if (PickSceneGround(e.GetPosition(ActorMarkerCanvas)) is not { } hit) return false;
        e.Handled = true;
        var menu = new ContextMenu();
        MenuItem Item(string header, Action click) { var item = new MenuItem { Header = header }; item.Click += (_, _) => click(); return item; }
        menu.Items.Add(new MenuItem { Header = $"Scene {hit.Scene} at ({hit.X}, {hit.Y}, {hit.Z})", IsEnabled = false });
        menu.Items.Add(Item("Add an actor here…", () => scenePanel.AddAt("actor", hit.Scene, hit.X, hit.Y, hit.Z)));
        var zones = new MenuItem { Header = "Add a zone here" };
        for (var t = 0; t < ZoneStyle.TypeCount; t++) { var type = t; zones.Items.Add(Item(ZoneStyle.NameOf(type), () => scenePanel.AddAt($"zone:{type}", hit.Scene, hit.X, hit.Y, hit.Z))); }
        menu.Items.Add(zones);
        menu.Items.Add(Item("Add a track point here", () => scenePanel.AddAt("point", hit.Scene, hit.X, hit.Y, hit.Z)));
        ViewportHost.ContextMenu = menu;
        menu.Closed += (_, _) => ViewportHost.ContextMenu = null;
        menu.IsOpen = true;
        return true;
    }
}
