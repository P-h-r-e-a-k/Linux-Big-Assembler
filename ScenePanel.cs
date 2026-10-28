using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LBAAssembler.Grids;
using LBAAssembler.Lba1;
using LBAAssembler.Scenes;

namespace LBAAssembler;

internal enum SceneItemKind { None, Actor, Zone, Point }

// What the scene panel needs from the window it sits in: the 3D view (show the selection, place by a click on the ground), the views read
// again after a write, and the other windows (scripts, the full attribute window, another scene).
internal interface IScenePanelHost
{
    Window HostWindow { get; }
    // The scenes the view shows (an island's outdoor scenes, a joined map's), for the panel's scene box.
    IReadOnlyList<(int Scene, string Label)> ScenesInView();
    void ShowSelection(int scene, SceneItemKind kind, int index, bool center);
    // A click on the ground in the view calls `place` with the scene under it and the point there (scene coordinates).
    void BeginPlacement(string hint, Action<int, int, int, int> place);
    void EndPlacement();
    void Written(SceneGame game, IReadOnlyList<int> scenes);
    void OpenScript(int scene, int actor);
    bool CanOpenAttributes(int scene, int actor);
    void OpenAttributes(int scene, int actor);
    void GoToScene(int scene);
    void SetStatus(string text, bool warning);
}

// Build > Actors and zones: one scene's actors, zones and track points, edited where the view shows them (the scene editor windows
// before 2026-10-24: "our current scene viewer isn't practical for a human to actually use" -- the user). The lists pick and the
// inspector edits; Add places a new one by a click on the ground in the view (or at a place typed in); Delete and Duplicate. Every
// change is written at once -- one step on Edit > Undo, as the rest of the main window's edits -- and the view shows it.
internal sealed class ScenePanel : Grid
{
    private readonly IScenePanelHost host;
    private SceneStore? store;
    private SceneModel? model;
    private int scene = -1;
    private SceneItemKind selKind = SceneItemKind.None;
    private int selIndex = -1;
    private bool loading, writing;
    private Action<int, int, int, int>? pendingPlace;

    // LBA2: the bodies' names and the kinds of actor; LBA1: the game (entity names, scene descriptions)
    private IReadOnlyList<string?> bodyNames = Array.Empty<string?>();
    private Lba2EntityTable? entities;
    private Lba1Game? lba1;

    private readonly ComboBox sceneBox = new() { MinWidth = 120 };
    private readonly Button addButton, deleteButton, duplicateButton;
    private readonly TabControl tabs = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly ListBox actorList = new(), zoneList = new(), pointList = new();
    private readonly StackPanel headerPanel = new() { Margin = new Thickness(4) };
    private readonly TextBlock inspectorTitle = new() { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock inspectorNote = new() { FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) };
    private readonly Grid inspectorGrid = new();
    private readonly WrapPanel inspectorButtons = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly Border placeBar = new() { Visibility = Visibility.Collapsed, Padding = new Thickness(6), Margin = new Thickness(0, 6, 0, 0), BorderThickness = new Thickness(1) };
    private readonly TextBlock placeHint = new() { TextWrapping = TextWrapping.Wrap, FontSize = 10.5 };
    private readonly TextBox placeX = new() { Width = 64 }, placeY = new() { Width = 64 }, placeZ = new() { Width = 64 };
    private readonly TextBlock status = new() { FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly List<(TextBox Box, Func<string> Get)> liveFields = new();

    public ScenePanel(IScenePanelHost host)
    {
        this.host = host;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // scene + toolbar
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // the place bar
        // (the Build tab scrolls the panel as a whole: the lists keep a height of their own, which the splitter under them changes; the
        // inspector is as tall as its fields)
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(180), MinHeight = 80 });  // the lists
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // splitter
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // the inspector
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // status

        // ---- the scene and the toolbar
        var top = new StackPanel();
        var sceneRow = new DockPanel { LastChildFill = true };
        var sceneLabel = Muted(new TextBlock { Text = "Scene", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        DockPanel.SetDock(sceneLabel, Dock.Left);
        sceneRow.Children.Add(sceneLabel);
        Avalonia.Automation.AutomationProperties.SetName(sceneBox, "Scene");
        Avalonia.Automation.AutomationProperties.SetAutomationId(sceneBox, "ScenePanelScene");
        sceneBox.ToolTip = "The scene whose actors, zones and track points are listed: the scenes in the view (another scene: Go to scene… under Scene)";
        // (the pick is acted on once the box has finished committing it: Open refills this very box, and Avalonia -- unlike WPF -- throws
        // out of its own selection model when a list's items are replaced while it is still inside that event)
        sceneBox.SelectionChanged += (_, _) => { if (!loading && sceneBox.SelectedItem is ComboBoxItem { Tag: int s } && s != scene) Dispatcher.UIThread.Post(() => { if (s != scene) Open(s); }, DispatcherPriority.Background); };
        sceneRow.Children.Add(sceneBox);
        top.Children.Add(sceneRow);
        var toolbar = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        addButton = ToolButton("Add ▾", "Place a new actor, zone or track point: pick what, then click the ground in the view (or type where)", ShowAddMenu);
        deleteButton = ToolButton("Delete", "Delete the selected actor, zone or track point (Delete key)", () => DeleteSelected());
        duplicateButton = ToolButton("Duplicate", "A copy of the selected one, a cell (512) along X", Duplicate);
        toolbar.Children.Add(addButton); toolbar.Children.Add(deleteButton); toolbar.Children.Add(duplicateButton);
        top.Children.Add(toolbar);
        SetRow(top, 0);
        Children.Add(top);

        // ---- the place bar: what is being placed, and where typed in instead of clicked
        var bar = new StackPanel();
        bar.Children.Add(placeHint);
        var typed = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        typed.Children.Add(Muted(new TextBlock { Text = "Or type where:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }));
        foreach (var (box, name) in new[] { (placeX, "Place X"), (placeY, "Place Y"), (placeZ, "Place Z") })
        {
            Avalonia.Automation.AutomationProperties.SetName(box, name);
            typed.Children.Add(Muted(new TextBlock { Text = name[^1..], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 2, 0) }));
            typed.Children.Add(box);
        }
        typed.Children.Add(ToolButton("Place here", "Put it at the X, Y, Z typed (in this scene's own coordinates)", PlaceTyped, new Thickness(6, 0, 0, 0)));
        typed.Children.Add(ToolButton("Cancel", "Don't place it (Esc)", CancelPlacement, new Thickness(4, 0, 0, 0)));
        bar.Children.Add(typed);
        placeBar.Child = bar;
        placeBar.SetResourceReference(Border.BorderBrushProperty, "ThemeAccentBrush");
        placeBar.SetResourceReference(Border.BackgroundProperty, "ThemeRaisedBrush");
        SetRow(placeBar, 1);
        Children.Add(placeBar);

        // ---- the lists
        foreach (var (list, name, kind) in new[] { (actorList, "Actors", SceneItemKind.Actor), (zoneList, "Zones", SceneItemKind.Zone), (pointList, "Points", SceneItemKind.Point) })
        {
            var k = kind;
            var l = list;
            l.FontFamily = UiFonts.Mono; l.FontSize = 11;
            // (a scene has a few hundred items at most: every row is made, so each can be reached from the keyboard and accessibility tools
            // -- Avalonia spells that as a plain stack panel for the items, where WPF had VirtualizingPanel.IsVirtualizing = false)
            l.ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel());
            Avalonia.Automation.AutomationProperties.SetAutomationId(l, $"Scene{name}List");
            Avalonia.Automation.AutomationProperties.SetName(l, name);
            // (the pick is acted on once the list has finished raising this: Select rebuilds all three lists, and Avalonia -- unlike WPF --
            // throws out of its own selection model when a list's items are replaced while it is still inside that event)
            l.SelectionChanged += (_, _) => { if (!loading && l.SelectedIndex >= 0) { var at = l.SelectedIndex; Dispatcher.UIThread.Post(() => Select(k, at, fromView: false), DispatcherPriority.Background); } };
            if (k == SceneItemKind.Actor) l.DoubleTapped += (_, _) => { if (selKind == SceneItemKind.Actor && selIndex >= 0) OpenFullAttributes(); };
            tabs.Items.Add(new TabItem { Header = name, Content = l });
        }
        tabs.Items.Add(new TabItem { Header = "Scene", Content = new ScrollViewer { Content = headerPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        SetRow(tabs, 2);
        Children.Add(tabs);

        var splitter = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch, ResizeDirection = GridResizeDirection.Rows, Background = Brushes.Transparent, Cursor = Cursors.SizeNS };
        SetRow(splitter, 3);
        Children.Add(splitter);

        // ---- the inspector
        inspectorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        inspectorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var inspector = new StackPanel { Margin = new Thickness(0, 2, 4, 4) };
        inspectorTitle.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        Muted(inspectorNote);
        inspector.Children.Add(inspectorTitle);
        inspector.Children.Add(inspectorNote);
        inspector.Children.Add(inspectorGrid);
        inspector.Children.Add(inspectorButtons);
        SetRow(inspector, 4);
        Children.Add(inspector);

        Avalonia.Automation.AutomationProperties.SetAutomationId(status, "ScenePanelStatus");
        Muted(status);
        SetRow(status, 5);
        Children.Add(status);

        SceneHistory.Changed += OnFilesChanged;
        BuildInspector();
        UpdateToolbar();
    }

    public SceneGame? Game => store?.Game;
    public int Scene => scene;
    public SceneItemKind SelectedKind => selKind;
    public int SelectedIndex => selIndex;
    public SceneModel? Model => model;
    public bool Placing => pendingPlace is not null;

    private static T Muted<T>(T text) where T : TextBlock { text.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush"); return text; }

    private Button ToolButton(string text, string tip, Action click, Thickness? margin = null)
    {
        // (QuietButton is a style class here, not a WPF Style resource: Theme.axaml's "Button.QuietButton" selector)
        var b = new Button { Content = text, ToolTip = tip, Classes = { "QuietButton" }, Padding = new Thickness(10, 3, 10, 3), Margin = margin ?? new Thickness(0, 0, 6, 0) };
        b.Click += (_, _) => click();
        return b;
    }

    private void Say(string text, bool warning = false)
    {
        status.Text = text;
        host.SetStatus(text, warning);
    }

    // ---- which scene

    // Shows the folder's scene `number` (the store's game and folder); the selection goes. The scene already shown stays as it is (the
    // files' changes reach it through Reload).
    public void Show(SceneStore with, int number)
    {
        var same = store is not null && number == scene && store.Game == with.Game && string.Equals(store.Directory, with.Directory, StringComparison.OrdinalIgnoreCase);
        if (same && model is not null) { RefreshSceneBox(); return; }
        var sameFolder = store is not null && store.Game == with.Game && string.Equals(store.Directory, with.Directory, StringComparison.OrdinalIgnoreCase);
        store = with;
        if (!sameFolder) { bodyNames = Array.Empty<string?>(); entities = null; lba1 = null; }
        if (with.Game == SceneGame.Lba1 && lba1 is null) { try { lba1 = new Lba1Game(with.Directory); } catch (Exception error) when (error is IOException or InvalidDataException) { lba1 = null; } }
        scene = number; selKind = SceneItemKind.None; selIndex = -1;
        Reload(rebuild: true);
    }

    public void Clear()
    {
        store = null; model = null; scene = -1; selKind = SceneItemKind.None; selIndex = -1;
        CancelPlacement();
        RefreshAll(rebuild: true);
    }

    private void Open(int number)
    {
        if (store is null) return;
        scene = number; selKind = SceneItemKind.None; selIndex = -1;
        Reload(rebuild: true);
        host.ShowSelection(scene, SceneItemKind.None, -1, center: false);
    }

    // The scene read again from the files (after this panel's write, an undo, another window's save).
    public void Reload(bool rebuild = false)
    {
        if (store is null || scene < 0) { model = null; RefreshAll(rebuild: true); return; }
        try { model = store.Load(scene); }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or SceneEditException or IndexOutOfRangeException)
        {
            model = null;
            Say($"Couldn't read scene {scene}: {error.Message}", warning: true);
        }
        if (model is not null && ((selKind == SceneItemKind.Actor && selIndex >= model.Actors.Count) || (selKind == SceneItemKind.Zone && selIndex >= model.Zones.Count) || (selKind == SceneItemKind.Point && selIndex >= model.TrackPoints.Count)))
        {
            selKind = SceneItemKind.None; selIndex = -1; rebuild = true;
        }
        RefreshAll(rebuild);
    }

    private void OnFilesChanged(object? sender, EventArgs e)
    {
        if (writing) return;
        Dispatcher.UIThread.BeginInvoke(() => Reload());
    }

    private void RefreshAll(bool rebuild)
    {
        RefreshSceneBox();
        RefreshLists();
        if (rebuild) { BuildInspector(); BuildHeader(); }
        else RefreshFieldValues();
        UpdateToolbar();
    }

    private void RefreshSceneBox()
    {
        loading = true;
        sceneBox.Items.Clear();
        if (store is not null)
        {
            var listed = host.ScenesInView().ToList();
            if (scene >= 0 && listed.All(s => s.Scene != scene)) listed.Insert(0, (scene, SceneLabel(scene)));
            foreach (var (s, label) in listed) sceneBox.Items.Add(new ComboBoxItem { Content = label, Tag = s });
            sceneBox.SelectedItem = sceneBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag is int s && s == scene);
        }
        loading = false;
    }

    private string SceneLabel(int number)
    {
        if (store?.Game == SceneGame.Lba1) return $"{number}: {lba1?.Description(number) ?? "scene"}";
        return store is null ? $"{number}" : Lba2SceneList.Load(store.Directory).FirstOrDefault(s => s.Id == number).Label ?? $"{number}";
    }

    private IReadOnlyList<(int Id, string Label)> AllScenes()
    {
        if (store is null) return Array.Empty<(int, string)>();
        if (store.Game == SceneGame.Lba1) return Enumerable.Range(0, store.SceneCount).Select(k => (k, $"{k,3}  {lba1?.Description(k) ?? "scene"}")).ToList();
        return Lba2SceneList.Load(store.Directory);
    }

    private void UpdateToolbar()
    {
        addButton.IsEnabled = model is not null;
        var canEdit = model is not null && selKind != SceneItemKind.None && !(selKind == SceneItemKind.Actor && selIndex == 0);
        deleteButton.IsEnabled = canEdit;
        duplicateButton.IsEnabled = canEdit;
    }

    // ---- the lists and the selection

    private string BodyName(int body) => body >= 0 && body < bodyNames.Count && bodyNames[body] is { } name ? name : $"body {body}";

    public string DescribeActor(int i, SceneActorModel a)
    {
        if (i == 0) return "Twinsen (start position)";
        if (store?.Game == SceneGame.Lba1)
        {
            if (a.IsSprite) return (a.Flags & 8) != 0 ? $"door, sprite {a.Sprite}" : $"sprite {a.Sprite}";
            var name = a.Entity >= 0 ? lba1?.EntityName(a.Entity) : null;
            return name is not null ? $"{name} (entity {a.Entity})" : $"entity {a.Entity}";
        }
        if (i == 1 && SceneOps.IsMecaPenguinSlot(a)) return "the Meca-penguin's slot (the game keeps actor 1 for it)";
        if (a.IsSprite) return $"sprite {a.Sprite}";
        if (a.Body < 0) return "(no body)";
        return entities?.BodyOf(a.Entity, a.Body) is { } body ? BodyName(body) : $"entity {a.Entity}, body {a.Body}";
    }

    private void RefreshLists()
    {
        loading = true;
        if (store?.Game == SceneGame.Lba2 && store is not null && bodyNames.Count == 0) LoadBodies();
        actorList.Items.Clear(); zoneList.Items.Clear(); pointList.Items.Clear();
        if (model is not null)
        {
            for (var i = 0; i < model.Actors.Count; i++) actorList.Items.Add($"{i,3}: {DescribeActor(i, model.Actors[i])}");
            for (var i = 0; i < model.Zones.Count; i++)
            {
                var z = model.Zones[i];
                var detail = store?.Game == SceneGame.Lba1
                    ? z.Type == 0 ? $" -> scene {z.Info[0]}" : ""
                    : z.Type == 0 ? $" -> scene {z.Num}" : z.Type == 2 ? $" #{z.Num}" : "";
                zoneList.Items.Add($"{i,3}: {ZoneStyle.NameOf(z.Type)}{detail}");
            }
            for (var i = 0; i < model.TrackPoints.Count; i++) pointList.Items.Add($"{i,3}: ({model.TrackPoints[i].X}, {model.TrackPoints[i].Y}, {model.TrackPoints[i].Z})");
        }
        actorList.SelectedIndex = selKind == SceneItemKind.Actor ? selIndex : -1;
        zoneList.SelectedIndex = selKind == SceneItemKind.Zone ? selIndex : -1;
        pointList.SelectedIndex = selKind == SceneItemKind.Point ? selIndex : -1;
        if (actorList.SelectedItem is { } a) actorList.ScrollIntoView(a);
        if (zoneList.SelectedItem is { } zz) zoneList.ScrollIntoView(zz);
        if (pointList.SelectedItem is { } p) pointList.ScrollIntoView(p);
        loading = false;
    }

    // Selects an item of the scene shown: from the lists (the view shows it and centres on it) or from the view (a click there).
    public void Select(SceneItemKind kind, int index, bool fromView)
    {
        if (model is null) return;
        if (kind == SceneItemKind.Actor && index >= model.Actors.Count || kind == SceneItemKind.Zone && index >= model.Zones.Count || kind == SceneItemKind.Point && index >= model.TrackPoints.Count)
            kind = SceneItemKind.None;
        if (kind == SceneItemKind.None) index = -1;
        var changed = kind != selKind || index != selIndex;
        selKind = kind; selIndex = index;
        if (changed) { RefreshLists(); BuildInspector(); UpdateToolbar(); }
        var tab = kind switch { SceneItemKind.Zone => 1, SceneItemKind.Point => 2, SceneItemKind.Actor => 0, _ => -1 };
        if (tab >= 0 && tabs.SelectedIndex != 3) tabs.SelectedIndex = tab;
        if (!fromView) host.ShowSelection(scene, kind, index, center: kind != SceneItemKind.None);
    }

    // A scene and one of its items picked in the view: that scene shown, the item selected.
    public void SelectFromView(SceneStore with, int number, SceneItemKind kind, int index)
    {
        Show(with, number);
        Select(kind, index, fromView: true);
    }

    // ---- writing

    // One change to the scene, written at once (one step on Edit > Undo). Returns whether it was written.
    private bool Edit(string description, Action<SceneModel> change, byte[]? grid = null)
    {
        if (store is null || model is null) return false;
        var next = model.Clone();
        try { change(next); }
        catch (SceneEditException error) { MessageBox.Show(host.HostWindow, error.Message, "Scene", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        writing = true;
        try
        {
            var result = store.Save(scene, next, grid, description: description);
            model = next;
            var warnings = result.Issues.Where(i => i.Severity == SceneIssueSeverity.Warning).ToList();
            Say($"{description}: written{SavedInto()}." + (warnings.Count > 0 ? $"  {warnings.Count} warning(s): {warnings[0]}" : ""));
        }
        catch (SceneValidationException error)
        {
            MessageBox.Show(host.HostWindow, "Not written: the engine would misread the scene like that.\n\n" + string.Join("\n", error.Issues.Where(i => i.Severity == SceneIssueSeverity.Error).Take(10)),
                "Scene", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(host.HostWindow, $"Not written: {error.Message}", "Scene", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        finally { writing = false; }
        host.Written(store.Game, new[] { scene });
        RefreshAll(rebuild: false);
        return true;
    }

    // An open map's scene is written into the map's working copy (File > Save map writes the map's file).
    private string SavedInto() => store is not null && Maps.MapWorkspace.For(store.Directory) is { } map ? $" into the map \"{map.Name}\" (File > Save map writes its file)" : "";

    // ---- the inspector

    private static int Number(string text)
    {
        text = text.Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? int.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : int.Parse(text, CultureInfo.InvariantCulture);
    }

    private void AddRow(Grid grid, string label, Func<string> get, Action<string> commit, string? hint = null)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var row = grid.RowDefinitions.Count - 1;
        var caption = Muted(new TextBlock { Text = label, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        var box = new TextBox { Text = Safe(get), Margin = new Thickness(0, 1, 0, 1), ToolTip = hint };
        // (named as its label: reachable by name from the keyboard's and accessibility tools' side)
        Avalonia.Automation.AutomationProperties.SetName(box, label);
        SetRow(caption, row); SetColumn(caption, 0);
        SetRow(box, row); SetColumn(box, 1);
        grid.Children.Add(caption); grid.Children.Add(box);
        void Commit()
        {
            // (an emptied Avalonia text box reads null, where WPF's read "")
            var typed = box.Text ?? "";
            if (typed == Safe(get)) return;
            try { commit(typed); }
            catch (Exception error) when (error is FormatException or OverflowException or ArgumentException or InvalidOperationException or IOException)
            {
                Say($"{label}: {error.Message}", warning: true);
            }
            box.Text = Safe(get);
        }
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); box.SelectAll(); e.Handled = true; } };
        liveFields.Add((box, get));
    }

    private static string Safe(Func<string> get) { try { return get(); } catch (ArgumentOutOfRangeException) { return ""; } }

    private void RefreshFieldValues()
    {
        foreach (var (box, get) in liveFields)
            if (!box.IsKeyboardFocusWithin) box.Text = Safe(get);
        inspectorTitle.Text = InspectorTitleText();
    }

    private string InspectorTitleText()
    {
        if (model is null) return store is null ? "No scene" : $"Scene {scene} can't be read";
        return selKind switch
        {
            SceneItemKind.Actor when selIndex < model.Actors.Count => selIndex == 0 ? "Twinsen (start position)" : $"Actor {selIndex}: {DescribeActor(selIndex, model.Actors[selIndex])}",
            SceneItemKind.Zone when selIndex < model.Zones.Count => $"Zone {selIndex}: {ZoneStyle.NameOf(model.Zones[selIndex].Type)}",
            SceneItemKind.Point when selIndex < model.TrackPoints.Count => $"Track point {selIndex}",
            _ => $"Scene {scene}",
        };
    }

    private static ZoneData ZoneDataOf(SceneZoneModel z, int scene, int index)
        => new() { Game = 2, Scene = scene, Index = index, Type = z.Type, Num = z.Num, Info = (int[])z.Info.Clone(), Snap = z.Snap, X0 = z.X0, Y0 = z.Y0, Z0 = z.Z0, X1 = z.X1, Y1 = z.Y1, Z1 = z.Z1 };

    private void InspectorButton(string text, string? tip, Action click)
    {
        var b = new Button { Content = text, ToolTip = tip, Classes = { "QuietButton" }, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 6, 4) };
        b.Click += (_, _) => click();
        inspectorButtons.Children.Add(b);
    }

    private void BuildInspector()
    {
        liveFields.RemoveAll(f => inspectorGrid.Children.Contains(f.Box));
        var g = inspectorGrid;
        g.Children.Clear(); g.RowDefinitions.Clear();
        inspectorButtons.Children.Clear();
        inspectorTitle.Text = InspectorTitleText();
        inspectorNote.Text = "";
        if (model is null || store is null)
        {
            inspectorNote.Text = "Choose an island scene, an interior or an LBA1 scene in the view.";
            return;
        }
        var lba1Game = store.Game == SceneGame.Lba1;
        var i = selIndex;
        switch (selKind)
        {
            case SceneItemKind.Actor when i >= 0 && i < model.Actors.Count:
            {
                void Num(string label, Func<SceneActorModel, int> get, Action<SceneActorModel, int> set, string? hint = null)
                    => AddRow(g, label, () => get(model!.Actors[i]).ToString(CultureInfo.InvariantCulture), text =>
                    {
                        var v = Number(text);
                        Edit($"Set {label.ToLowerInvariant()} of actor {i} of scene {scene}", m => set(m.Actors[i], v));
                    }, hint);
                Num("X", a => a.X, (a, v) => a.X = v, "position in the scene; 512 units = one cell");
                Num("Y", a => a.Y, (a, v) => a.Y = v, "256 units = one layer");
                Num("Z", a => a.Z, (a, v) => a.Z = v);
                if (i > 0)
                {
                    if (lba1Game) Num("Facing", a => a.Beta, (a, v) => a.Beta = v & 1023, "0..1023: 0 = +z, 256 = +x, 512 = -z, 768 = -x");
                    else Num("Facing", a => a.Beta, (a, v) => a.Beta = v & 4095, "0..4095: 0 = +z, 1024 = +x, 2048 = -z, 3072 = -x");
                    Num("Entity", a => a.Entity, (a, v) => a.Entity = v, lba1Game ? "the FILE3D entity: which creature it is" : "the kind of actor (RESS.HQR's entity table): which bodies and animations it has");
                    Num("Body", a => a.Body, (a, v) => a.Body = v, lba1Game ? "the entity's body variant" : "the body's number in its entity, not a BODY.HQR entry (Choose body… picks one by name); -1 = none");
                    Num("Animation", a => a.Anim, (a, v) => a.Anim = v, "the animation's number in its entity");
                    Num("Sprite", a => a.Sprite, (a, v) => a.Sprite = v, "for sprite actors");
                    AddRow(g, "Flags", () => "0x" + model!.Actors[i].Flags.ToString(lba1Game ? "X4" : "X8"), text =>
                    {
                        var v = (uint)Number(text);
                        Edit($"Set flags of actor {i} of scene {scene}", m => m.Actors[i].Flags = v);
                    }, lba1Game ? "hex; 0x0400 = sprite, 0x0008 = sprite clip (door), 0x0004 = check zones ..." : "hex");
                    Num("Move", a => a.Move, (a, v) => a.Move = v, "the control mode: 0 none, 1 manual, 2 follow, 3 track ...");
                    Num("Speed", a => a.SRot, (a, v) => a.SRot = v);
                    Num("Life", a => a.LifePoints, (a, v) => a.LifePoints = v);
                    Num("Armour", a => a.Armor, (a, v) => a.Armor = v);
                    Num("Hit force", a => a.HitForce, (a, v) => a.HitForce = v);
                    Num("Text colour", a => a.CoulObj, (a, v) => a.CoulObj = v);
                    Num("Bonus option", a => a.OptionFlags, (a, v) => a.OptionFlags = v);
                    Num("Bonus count", a => a.NbBonus, (a, v) => a.NbBonus = v);
                    for (var k = 0; k < 4; k++) { var slot = k; Num($"Info {k}", a => a.Info[slot], (a, v) => a.Info[slot] = v, lba1Game ? "a door's screen clip rectangle; the target of a follow move" : "parameters some moves use"); }
                    if (lba1Game) InspectorButton("Choose entity…", "Which creature it is (FILE3D.HQR); its body and animation go back to the entity's first", ChooseEntity);
                    else InspectorButton("Choose body…", "Its body by name; a body its kind of actor hasn't makes it the kind that has it", ChooseBody);
                    var penguin = !lba1Game && i == 1 && SceneOps.IsMecaPenguinSlot(model.Actors[i]);
                    if (!penguin && !model.Actors[i].IsSprite)
                        InspectorButton("Behaviour…", "Ready-made scripts for it: the game's own actors of its kind, templates (stands, wanders, patrols, talks, attacks, keeps away), or none", ChooseBehaviour);
                    inspectorNote.Text = $"Life script {model.Actors[i].Life.Length} bytes, track script {model.Actors[i].Track.Length} bytes.";
                }
                else inspectorNote.Text = "Where Twinsen starts when the scene is entered without a cube-change zone.";
                InspectorButton("Edit script…", "Its life and track scripts in the script editor", () => host.OpenScript(scene, i));
                if (i > 0 && host.CanOpenAttributes(scene, i)) InspectorButton("Full attributes…", "The actor window: its body seen turning, flags as checkboxes", OpenFullAttributes);
                break;
            }

            case SceneItemKind.Zone when i >= 0 && i < model.Zones.Count:
            {
                void Num(string label, Func<SceneZoneModel, int> get, Action<SceneZoneModel, int> set, string? hint = null)
                    => AddRow(g, label, () => get(model!.Zones[i]).ToString(CultureInfo.InvariantCulture), text =>
                    {
                        var v = Number(text);
                        Edit($"Set {label.ToLowerInvariant()} of zone {i} of scene {scene}", m => set(m.Zones[i], v));
                    }, hint);
                AddRow(g, "Type", () => $"{model!.Zones[i].Type}", text =>
                {
                    var v = Number(text);
                    if (Edit($"Set type of zone {i} of scene {scene}", m => m.Zones[i].Type = v)) Dispatcher.UIThread.BeginInvoke(new Action(BuildInspector));
                }, string.Join(", ", ZoneStyle.TypeNames.Select((n, k) => $"{k} = {n}")));
                Num("X min", s => s.X0, (s, v) => s.X0 = v); Num("X max", s => s.X1, (s, v) => s.X1 = v);
                Num("Y min", s => s.Y0, (s, v) => s.Y0 = v); Num("Y max", s => s.Y1, (s, v) => s.Y1 = v);
                Num("Z min", s => s.Z0, (s, v) => s.Z0 = v); Num("Z max", s => s.Z1, (s, v) => s.Z1 = v);
                if (lba1Game)
                {
                    var fields = ZoneFields.For(new ZoneData { Game = 1, Scene = scene, Index = i, Type = model.Zones[i].Type, Info = new int[4] });
                    for (var k = 0; k < 4; k++) { var slot = k; Num(fields[k].Label, s => s.Info[slot], (s, v) => s.Info[slot] = v, fields[k].Hint); }
                    Num("Snap", s => s.Snap, (s, v) => s.Snap = v);
                }
                else
                {
                    foreach (var field in ZoneFields.For(ZoneDataOf(model.Zones[i], scene, i)))
                    {
                        var f = field;
                        AddRow(g, f.Label, () => f.Get(ZoneDataOf(model!.Zones[i], scene, i)).ToString(CultureInfo.InvariantCulture), text =>
                        {
                            var v = Number(text);
                            Edit($"Set {f.Label.ToLowerInvariant()} of zone {i} of scene {scene}", m =>
                            {
                                var data = ZoneDataOf(m.Zones[i], scene, i);
                                f.Set(data, v);
                                m.Zones[i].Num = data.Num;
                                m.Zones[i].Info = data.Info;
                            });
                        }, f.Hint);
                    }
                }
                if (model.Zones[i].Type == 0)
                {
                    InspectorButton("Go to destination", "Shows the scene this zone leads to", () => host.GoToScene(lba1Game ? model!.Zones[i].Info[0] : model!.Zones[i].Num));
                    if (lba1Game) inspectorNote.Text = "Twinsen arrives at the destination corner plus his offset inside this zone.";
                }
                break;
            }

            case SceneItemKind.Point when i >= 0 && i < model.TrackPoints.Count:
            {
                void Num(string label, Func<SceneTrackPoint, int> get, Func<SceneTrackPoint, int, SceneTrackPoint> set)
                    => AddRow(g, label, () => get(model!.TrackPoints[i]).ToString(CultureInfo.InvariantCulture), text =>
                    {
                        var v = Number(text);
                        Edit($"Set {label} of track point {i} of scene {scene}", m => m.TrackPoints[i] = set(m.TrackPoints[i], v));
                    });
                Num("X", p => p.X, (p, v) => p with { X = v });
                Num("Y", p => p.Y, (p, v) => p with { Y = v });
                Num("Z", p => p.Z, (p, v) => p with { Z = v });
                inspectorNote.Text = "Waypoints that actors' track scripts walk between (goto_point, pos_point ...).";
                break;
            }

            default:
                inspectorNote.Text = "Click an actor, a zone or a track point in the view (or pick one above) to edit it; drag it in the view to move it. Add places something new where you click the ground.";
                break;
        }
    }

    // The Scene tab: the scene's header, and what is done to the scene as a whole.
    private void BuildHeader()
    {
        liveFields.RemoveAll(f => IsInside(f.Box, headerPanel));
        headerPanel.Children.Clear();
        if (model is null || store is null) return;
        var lba1Game = store.Game == SceneGame.Lba1;
        var buttons = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        void Action(string text, string tip, Action click)
        {
            var b = new Button { Content = text, ToolTip = tip, Classes = { "QuietButton" }, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 6, 4) };
            b.Click += (_, _) => click();
            buttons.Children.Add(b);
        }
        Action("Check the scene…", "The scene against the engine's rules: what it would misread", CheckScene);
        Action("Save into another slot…", "The game's scene table is fixed: this replaces another scene with this one", SaveIntoSlot);
        Action("Go to scene…", "Shows another scene in the view", GoToOtherScene);
        if (lba1Game)
        {
            Action("Edit the map (grid editor)…", "This scene's grid in the grid editor: paint blocks, edit the block library", OpenGridEditor);
            Action("Make the scene blank…", "A flat floor and Twinsen, nothing else (one undoable step)", MakeBlank);
        }
        else if (model.CubeMode == 0 && File.Exists(Path.Combine(store.Directory, "LBA_BKG.HQR")))
        {
            Action("Edit the interior's map…", "This interior's grid in the grid editor: paint blocks, edit the block library", OpenGridEditor);
            Action("Make a blank interior…", "A flat floor and Twinsen, nothing else", MakeBlank);
        }
        headerPanel.Children.Add(buttons);

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void Num(string label, Func<SceneModel, int> get, Action<SceneModel, int> set, string? hint = null)
            => AddRow(g, label, () => get(model!).ToString(CultureInfo.InvariantCulture), text =>
            {
                var v = Number(text);
                Edit($"Set {label.ToLowerInvariant()} of scene {scene}", m => set(m, v));
            }, hint);
        if (lba1Game)
        {
            Num("Island", m => m.Island, (m, v) => m.Island = v, string.Join(", ", Lba1Game.IslandNames.Select((n, k) => $"{k} = {n}")));
            Num("Game over scene", m => m.GameOverScene, (m, v) => m.GameOverScene = v, "the scene Twinsen wakes up in after dying without a clover leaf");
        }
        else
        {
            Num("Island", m => m.Island, (m, v) => m.Island = v);
            Num("Cube mode", m => m.CubeMode, (m, v) => m.CubeMode = v, "0 = an interior, 1 = an exterior (a cube of an island)");
            Num("Cube X", m => m.CubeX, (m, v) => m.CubeX = v, "island cube (exteriors)");
            Num("Cube Y", m => m.CubeY, (m, v) => m.CubeY = v);
        }
        Num("Light alpha", m => m.AlphaLight, (m, v) => m.AlphaLight = v);
        Num("Light beta", m => m.BetaLight, (m, v) => m.BetaLight = v);
        if (!lba1Game)
        {
            Num("Shadow level", m => m.ShadowLevel, (m, v) => m.ShadowLevel = v);
            Num("Labyrinth mode", m => m.LabyrinthMode, (m, v) => m.LabyrinthMode = v);
        }
        Num("Music", m => m.Music, (m, v) => m.Music = v, "the jingle number");
        Num("Ambient delay min", m => m.SecondMin, (m, v) => m.SecondMin = v);
        Num("Ambient delay range", m => m.SecondEcart, (m, v) => m.SecondEcart = v);
        for (var k = 0; k < 4; k++)
        {
            var n = k;
            Num($"Ambient {k} sample", m => m.Ambient[n].Sample, (m, v) => m.Ambient[n].Sample = v);
            Num($"Ambient {k} repeat", m => m.Ambient[n].Repeat, (m, v) => m.Ambient[n].Repeat = v);
            Num($"Ambient {k} round", m => m.Ambient[n].Round, (m, v) => m.Ambient[n].Round = v);
            if (!lba1Game)
            {
                Num($"Ambient {k} frequency", m => m.Ambient[n].Frequency, (m, v) => m.Ambient[n].Frequency = v);
                Num($"Ambient {k} volume", m => m.Ambient[n].Volume, (m, v) => m.Ambient[n].Volume = v);
            }
        }
        headerPanel.Children.Add(g);
        headerPanel.Children.Add(Muted(new TextBlock
        {
            Text = $"{model.Actors.Count} actors, {model.Zones.Count} zones, {model.TrackPoints.Count} track points.",
            FontSize = 10.5, Margin = new Thickness(0, 10, 0, 0),
        }));
    }

    // (WPF walked the logical tree and fell back to the visual one; Avalonia's Panel puts a child in both as it is added, so the visual
    // parent alone reaches the same container -- and it does so whether or not the Scene tab has been shown yet)
    private static bool IsInside(Visual element, Visual container)
    {
        for (Visual? e = element; e is not null; e = e.GetVisualParent())
            if (ReferenceEquals(e, container)) return true;
        return false;
    }

    // ---- placing

    private void Place(string hint, Action<int, int, int, int> place)
    {
        if (model is null) return;
        pendingPlace = place;
        placeHint.Text = hint;
        // (the place typed in instead of clicked: Twinsen's start to begin with)
        placeY.Text = model.Hero.Y.ToString(CultureInfo.InvariantCulture);
        if ((placeX.Text ?? "").Length == 0) { placeX.Text = model.Hero.X.ToString(CultureInfo.InvariantCulture); placeZ.Text = model.Hero.Z.ToString(CultureInfo.InvariantCulture); }
        placeBar.Visibility = Visibility.Visible;
        Say(hint);
        host.BeginPlacement(hint, (s, x, y, z) => { if (pendingPlace is { } p) { FinishPlacement(); p(s, x, y, z); } });
    }

    private void FinishPlacement()
    {
        pendingPlace = null;
        placeBar.Visibility = Visibility.Collapsed;
        host.EndPlacement();
    }

    public void CancelPlacement()
    {
        if (pendingPlace is null) { placeBar.Visibility = Visibility.Collapsed; return; }
        FinishPlacement();
        Say("Not placed.");
    }

    private void PlaceTyped()
    {
        if (pendingPlace is not { } place) return;
        if (!int.TryParse((placeX.Text ?? "").Trim(), out var x) || !int.TryParse((placeY.Text ?? "").Trim(), out var y) || !int.TryParse((placeZ.Text ?? "").Trim(), out var z))
        {
            Say("Type X, Y and Z as whole numbers.", warning: true);
            return;
        }
        FinishPlacement();
        place(scene, x, y, z);
    }

    // A placement landed in another scene of the view (another cube of the island, another scene of a joined map): that one is shown first.
    private bool InScene(int target)
    {
        if (target == scene) return true;
        if (store is null) return false;
        Show(store, target);
        return model is not null;
    }

    // ---- adding

    private void ShowAddMenu()
    {
        if (model is null || store is null) return;
        var menu = new ContextMenu { PlacementTarget = addButton, Placement = PlacementMode.Bottom };
        MenuItem Item(string header, Action click, bool enabled = true, string? tip = null) { var item = new MenuItem { Header = header, IsEnabled = enabled, ToolTip = tip }; item.Click += (_, _) => click(); return item; }
        var lba1Game = store.Game == SceneGame.Lba1;
        // (a new actor gets its kind's usual behaviour -- ActorBehaviours.DefaultFor: a skeleton goes for Twinsen -- or none at all)
        foreach (var usual in new[] { true, false })
        {
            var withUsual = usual;
            menu.Items.Add(Item(usual ? "New 3D actor…" : "New 3D actor without a script…", () =>
            {
                if (lba1Game)
                {
                    if (PickEntity(0) is not { } entity) return;
                    Place($"Click the ground in the view to place the new actor (entity {entity}{(withUsual ? ", with its kind's usual behaviour" : ", no script")}). Esc cancels.",
                        (s, x, y, z) => { if (InScene(s)) AddActor(SceneOps.BlankActor(SceneGame.Lba1, x, y, z, entity), "Add actor", withUsual ? DefaultBehaviour(entity) : null); });
                    return;
                }
                if (PickBody(0) is not { } body || KindOf(body) is not { } kind) return;
                Place($"Click the ground in the view to place the new actor ({BodyName(body)}{(withUsual ? ", with its kind's usual behaviour" : ", no script")}). Esc cancels.", (s, x, y, z) =>
                {
                    if (!InScene(s)) return;
                    var actor = SceneOps.BlankActor(SceneGame.Lba2, x, y, z, kind.Entity);
                    actor.Body = kind.Body; actor.Anim = kind.Anim;
                    AddActor(actor, "Add actor", withUsual ? DefaultBehaviour(kind.Entity) : null);
                });
            }));
        }
        menu.Items.Add(Item("Copy of the selected actor", () =>
        {
            var source = selIndex;
            var from = scene;
            var sourceModel = model;
            Place($"Click the ground in the view to place the copy of actor {source}. Esc cancels.", (s, x, y, z) =>
            {
                if (!InScene(s)) return;
                var copy = sourceModel.Actors[source].Clone(); copy.X = x; copy.Y = y; copy.Z = z;
                // (into another scene its scripts are fitted to that scene, as an actor from another scene's are)
                AddActor(copy, $"Copy actor {source}{(s != from ? $" of scene {from}" : "")}", s != from ? ActorBehaviours.FromActor(store.Game, store.Directory, sourceModel, from, source) : null);
            });
        }, selKind == SceneItemKind.Actor && selIndex > 0));
        menu.Items.Add(Item("Actor from another scene…", () =>
        {
            if (PickActorOfScene() is not { } picked) return;
            Place($"Click the ground in the view to place the copy of scene {picked.Scene}'s actor {picked.Index} ({DescribeActor(picked.Index, picked.Actor)}). Esc cancels.", (s, x, y, z) =>
            {
                if (!InScene(s)) return;
                var copy = picked.Actor.Clone(); copy.X = x; copy.Y = y; copy.Z = z;
                // (its scripts come with it, carried into this scene -- ScriptRemap; from this very scene they stay as they are)
                var carried = picked.Scene == scene ? null : ActorBehaviours.FromActor(store.Game, store.Directory, picked.Model, picked.Scene, picked.Index);
                AddActor(copy, $"Copy actor {picked.Index} of scene {picked.Scene}", carried);
            });
        }));
        if (lba1Game)
        {
            menu.Items.Add(new Separator());
            foreach (var prefab in ActorPrefabs.For(SceneGame.Lba1))
            {
                var chosen = prefab;
                menu.Items.Add(Item(chosen.Name, () => Place($"Click the ground in the view to place: {chosen.Name}. {chosen.Description}", (s, x, y, z) =>
                {
                    if (!InScene(s)) return;
                    var index = -1;
                    try
                    {
                        if (Edit($"Add {chosen.Name} to scene {scene}", m => index = ActorPrefabs.Place(m, chosen, x, y, z))) Select(SceneItemKind.Actor, index, fromView: false);
                    }
                    catch (Exception error) when (error is InvalidOperationException or IOException) { MessageBox.Show(host.HostWindow, error.Message, "Scene", MessageBoxButton.OK, MessageBoxImage.Warning); }
                }), tip: chosen.Description));
            }
        }
        menu.Items.Add(new Separator());
        var zones = new MenuItem { Header = "Zone" };
        for (var type = 0; type < ZoneStyle.TypeCount; type++)
        {
            var t = type;
            zones.Items.Add(Item(ZoneStyle.NameOf(t), () => Place($"Click the ground in the view to place the {ZoneStyle.NameOf(t)} zone (two cells square). Esc cancels.", (s, x, y, z) => { if (InScene(s)) AddZone(t, x, y, z); })));
        }
        menu.Items.Add(zones);
        menu.Items.Add(Item("Track point", () => Place("Click the ground in the view to place the track point. Esc cancels.", (s, x, y, z) =>
        {
            if (!InScene(s)) return;
            var index = -1;
            if (Edit($"Add track point to scene {scene}", m => index = SceneOps.AddTrackPoint(m, new SceneTrackPoint(x, y, z)))) Select(SceneItemKind.Point, index, fromView: false);
        })));
        menu.Open(menu.PlacementTarget as Control);     // (a context menu is opened here; Avalonia's own IsOpen is read-only)
    }

    // Starts placing one kind of thing at a point the view already chose (its right-click menu): the same as Add, without the click.
    public void AddAt(string what, int atScene, int x, int y, int z)
    {
        if (store is null) return;
        if (!InScene(atScene)) return;
        switch (what)
        {
            case "actor" when store.Game == SceneGame.Lba1:
                if (PickEntity(0) is { } entity) AddActor(SceneOps.BlankActor(SceneGame.Lba1, x, y, z, entity), "Add actor", DefaultBehaviour(entity));
                break;
            case "actor":
                if (PickBody(0) is { } body && KindOf(body) is { } kind)
                {
                    var actor = SceneOps.BlankActor(SceneGame.Lba2, x, y, z, kind.Entity);
                    actor.Body = kind.Body; actor.Anim = kind.Anim;
                    AddActor(actor, "Add actor", DefaultBehaviour(kind.Entity));
                }
                break;
            case "point":
                var index = -1;
                if (Edit($"Add track point to scene {scene}", m => index = SceneOps.AddTrackPoint(m, new SceneTrackPoint(x, y, z)))) Select(SceneItemKind.Point, index, fromView: false);
                break;
            default:
                if (what.StartsWith("zone:", StringComparison.Ordinal) && int.TryParse(what[5..], out var type)) AddZone(type, x, y, z);
                break;
        }
    }

    // behaviour: what it does (a kind's usual behaviour, an actor of another scene carried over), put on it in the same step; null: its
    // scripts as they are
    private void AddActor(SceneActorModel actor, string description, ActorBehaviour? behaviour = null)
    {
        if (store is null) return;
        var index = -1;
        var slotPut = false;
        List<string> notes = new();
        var lba2 = store.Game == SceneGame.Lba2;
        if (!Edit((behaviour is null ? description : $"{description} ({behaviour.Name.ToLowerInvariant()})") + $" to scene {scene}", m =>
            {
                // (LBA2: an actor added to a scene without the Meca-penguin's slot at actor 1 puts it there first)
                if (lba2) slotPut = SceneOps.EnsureMecaPenguinSlot(m);
                index = SceneOps.AddActor(m, actor);
                if (behaviour is not null) notes = ActorBehaviours.Apply(store, m, index, behaviour);
            })) return;
        Select(SceneItemKind.Actor, index, fromView: false);
        if (behaviour is not null)
            Say($"Actor {index}: {behaviour.Name} -- {behaviour.Description}." + (notes.Count > 0 ? $" ({notes.Count} change{(notes.Count > 1 ? "s" : "")} to fit this scene: {notes[0]}{(notes.Count > 1 ? "; ..." : "")})" : "") +
                " Behaviour… gives it another (or none)." + (slotPut ? " Actor 1 is the Meca-penguin's slot now, which the game hides whatever is in." : ""));
        else if (slotPut) Say("Actor 1 is the Meca-penguin's slot, which the game hides whatever is in: the scene's own actors start at 2.");
    }

    // The kind's usual behaviour (ActorBehaviours.DefaultFor), or none when it can't be worked out.
    private ActorBehaviour? DefaultBehaviour(int entity)
    {
        if (store is null || model is null) return null;
        try { using (UiBusy.Cursor()) return ActorBehaviours.DefaultFor(store, model, entity); }
        catch (Exception error) when (error is IOException or InvalidDataException or SceneEditException)
        {
            DebugLog.Log($"ScenePanel: no usual behaviour for entity {entity}: {error.Message}");
            return null;
        }
    }

    private void AddZone(int type, int x, int y, int z)
    {
        if (store is null) return;
        // two cells square, standing on the clicked ground (LBA2 zones have eight info values, LBA1's four)
        var zone = store.Game == SceneGame.Lba1
            ? new SceneZoneModel { X0 = x - 512, X1 = x + 512, Y0 = y, Y1 = y + 512, Z0 = z - 512, Z1 = z + 512, Type = type }
            : new SceneZoneModel { X0 = x - 512, X1 = x + 512, Y0 = y, Y1 = y + 1024, Z0 = z - 512, Z1 = z + 512, Type = type, Info = new int[8] };
        var index = -1;
        if (Edit($"Add {ZoneStyle.NameOf(type)} zone to scene {scene}", m => index = SceneOps.AddZone(m, zone))) Select(SceneItemKind.Zone, index, fromView: false);
    }

    // ---- bodies, entities, behaviours

    // The bodies' names and the entity table, read again each time a body is picked: a character the Character maker saved meanwhile is
    // among them.
    private void LoadBodies()
    {
        if (store is null) return;
        var bodyPath = Path.Combine(store.Directory, "BODY.HQR");
        var names = (File.Exists(bodyPath) ? HqdDescriptions.Load("BODY2.HQD", HqrArchive.CountEntries(bodyPath)).Names : Array.Empty<string?>()).ToList();
        // (a map's own characters' bodies by their names: the Character maker's, past the game's -- <stem>_CHARACTERS.TXT beside its files)
        foreach (var sidecar in Directory.EnumerateFiles(store.Directory, "*" + Characters.MapCharacters.Suffix))
            foreach (var c in Characters.MapCharacters.Load(sidecar))
                foreach (var b in c.Bodies)
                {
                    while (names.Count <= b.Entry) names.Add(null);
                    names[b.Entry] = $"{c.Name}: {b.Name} (the map's own)";
                }
        bodyNames = names;
        entities = Lba2EntityTable.Load(store.Directory);
    }

    private int? PickBody(int? current)
    {
        if (store is null) return null;
        LoadBodies();
        var bodies = Path.Combine(store.Directory, "BODY.HQR");
        var count = File.Exists(bodies) ? HqrArchive.CountEntries(bodies) : 0;
        var items = Enumerable.Range(0, count).Select(k => (k, $"{k,4}  {(k < bodyNames.Count ? bodyNames[k] : null) ?? "(unnamed)"}")).ToList();
        return ListPickWindow.Pick(host.HostWindow, "Choose a body", items, current,
            "Bodies are the models of BODY.HQR. The actor becomes the kind of actor (entity) that has the body. New 3D actor gives it what the game's " +
            "actors of that kind usually do (a skeleton goes for Twinsen); Behaviour… gives it another, or none.");
    }

    // The kind of actor that shows BODY.HQR entry `body` (Lba2EntityTable.KindForBody), or null after saying why there is none.
    private (int Entity, int Body, int Anim)? KindOf(int body)
    {
        if (entities?.KindForBody(body) is { } kind) return kind;
        MessageBox.Show(host.HostWindow, entities is null
                ? "The game folder's RESS.HQR has no table of kinds of actor (entity 44), so the body can't be given to an actor."
                : $"No kind of actor (RESS.HQR's entity table) has body {body}, so the game can't draw it on an actor: choose another body.",
            "Scene", MessageBoxButton.OK, MessageBoxImage.Information);
        return null;
    }

    // An actor's body is a number in its entity's list of bodies: a body its entity has is that number; any other changes the kind of actor
    // to one that has it (and the animation to that one's standing animation).
    private void ChooseBody()
    {
        if (model is null || selKind != SceneItemKind.Actor || selIndex <= 0) return;
        var i = selIndex;
        var actor = model.Actors[i];
        if (PickBody(entities?.BodyOf(actor.Entity, actor.Body)) is not { } body) return;
        if (entities?.Entities.ElementAtOrDefault(actor.Entity) is { } own && own.Bodies.Any(b => b.Body == body))
        {
            var generic = own.Bodies.Where(b => b.Body == body).Min(b => b.Generic);
            Edit($"Set body of actor {i} of scene {scene}", m => m.Actors[i].Body = generic);
        }
        else if (KindOf(body) is { } kind && Edit($"Set body of actor {i} of scene {scene}", m => { m.Actors[i].Entity = kind.Entity; m.Actors[i].Body = kind.Body; m.Actors[i].Anim = kind.Anim; }))
            Say($"Actor {i} is now entity {kind.Entity}, the kind of actor with {BodyName(body)}; its animation is that one's standing animation.");
        BuildInspector();
        RefreshLists();
    }

    private int? PickEntity(int? current)
    {
        if (lba1 is null) return null;
        var items = Enumerable.Range(0, lba1.EntityCount).Select(k => (k, $"{k,3}  {lba1.EntityName(k) ?? "(unnamed)"}")).ToList();
        return ListPickWindow.Pick(host.HostWindow, "Choose an entity", items, current, "Entities are the creatures and objects of FILE3D.HQR. The new actor takes its first body and animation; change them in the inspector.");
    }

    private void ChooseEntity()
    {
        if (model is null || selKind != SceneItemKind.Actor || selIndex <= 0) return;
        var i = selIndex;
        if (PickEntity(model.Actors[i].Entity) is not { } entity) return;
        Edit($"Set entity of actor {i} of scene {scene}", m => { m.Actors[i].Entity = entity; m.Actors[i].Body = 0; m.Actors[i].Anim = 0; });
        BuildInspector();
        RefreshLists();
    }

    private void ChooseBehaviour()
    {
        if (store is null || model is null || selKind != SceneItemKind.Actor || selIndex <= 0) return;
        var i = selIndex;
        if (BehaviourPickWindow.ForActor(host.HostWindow, store, model, i, $"scene {scene}") is not { } chosen) return;
        List<string> notes = new();
        if (!Edit($"Give actor {i} of scene {scene} a behaviour ({chosen.Behaviour.Name.ToLowerInvariant()})", m => notes = ActorBehaviours.Apply(store, m, i, chosen.Behaviour, chosen.Text))) return;
        BuildInspector();
        Say($"Actor {i}: {chosen.Behaviour.Name} -- {chosen.Behaviour.Description}." + (notes.Count > 0 ? $" ({notes.Count} change{(notes.Count > 1 ? "s" : "")} to fit this scene)" : ""));
        if (notes.Count > 1)
            MessageBox.Show(host.HostWindow, $"Actor {i}: {chosen.Behaviour.Name}. To fit this scene:\n\n- " + string.Join("\n- ", notes.Take(14)) + (notes.Count > 14 ? $"\n... {notes.Count - 14} more" : ""),
                "Behaviour", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenFullAttributes()
    {
        if (selKind == SceneItemKind.Actor && selIndex > 0 && host.CanOpenAttributes(scene, selIndex)) host.OpenAttributes(scene, selIndex);
    }

    // A scene and one of its actors (not its hero), to copy here.
    private (int Scene, int Index, SceneActorModel Actor, SceneModel Model)? PickActorOfScene()
    {
        if (store is null) return null;
        var number = ListPickWindow.Pick(host.HostWindow, "Copy an actor from a scene", AllScenes().ToList(), scene,
            "First the scene, then its actor. The copy has the actor's kind, body, animation, settings and scripts.");
        if (number is not { } s) return null;
        SceneModel from;
        try { from = store.Load(s); }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or SceneEditException)
        {
            MessageBox.Show(host.HostWindow, $"Couldn't read scene {s}: {error.Message}", "Scene", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        if (from.Actors.Count < 2) { MessageBox.Show(host.HostWindow, $"Scene {s} has no actor but Twinsen.", "Scene", MessageBoxButton.OK, MessageBoxImage.Information); return null; }
        var items = Enumerable.Range(1, from.Actors.Count - 1)
            .Select(k => (k, $"{k,3}  {DescribeActor(k, from.Actors[k])}{(from.Actors[k].Life.Length > 1 ? "  (life script)" : "")}")).ToList();
        var index = ListPickWindow.Pick(host.HostWindow, $"Copy an actor of scene {s}", items, null,
            "Its scripts come with it, fitted to this scene: Twinsen and itself stay, the track points and zones it uses come along, what it does to other actors, " +
            "to its old scene's zones and to the story is left out, its lines too when this scene's island isn't its own.");
        return index is { } k2 ? (s, k2, from.Actors[k2], from) : null;
    }

    // ---- deleting, duplicating, moving

    public bool DeleteSelected()
    {
        if (model is null || store is null) return false;
        var index = selIndex;
        var written = false;
        switch (selKind)
        {
            case SceneItemKind.Actor when store.Game == SceneGame.Lba2 && index == 1 && SceneOps.IsMecaPenguinSlot(model.Actors[1]):
                MessageBox.Show(host.HostWindow, "Actor 1 is the slot the game keeps for the Meca-penguin: it hides whatever is there, so the actor after it would vanish. It stays.",
                    "Delete actor", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            case SceneItemKind.Actor when index > 0:
            {
                // (an actor others' scripts name: asked first, then those references point at Twinsen)
                int? retarget = null;
                try { SceneOps.DeleteActor(model.Clone(), index); }
                catch (SceneEditException error) when (error.GetType() == typeof(SceneEditException))
                {
                    if (MessageBox.Show(host.HostWindow, error.Message + "\n\nDelete it anyway? Those references will point at Twinsen (actor 0) instead.", "Delete actor", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return false;
                    retarget = 0;
                }
                written = Edit($"Delete actor {index} of scene {scene}", m => SceneOps.DeleteActor(m, index, retarget));
                break;
            }
            case SceneItemKind.Zone:
                written = Edit($"Delete zone {index} of scene {scene}", m => SceneOps.DeleteZone(m, index));
                break;
            case SceneItemKind.Point:
            {
                int? retarget = null;
                try { SceneOps.DeleteTrackPoint(model.Clone(), index); }
                catch (SceneEditException error) when (error.GetType() == typeof(SceneEditException))
                {
                    if (MessageBox.Show(host.HostWindow, error.Message + "\n\nDelete it anyway? Those references will use another track point instead.", "Delete track point", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return false;
                    retarget = index == 0 ? 1 : 0;
                }
                written = Edit($"Delete track point {index} of scene {scene}", m => SceneOps.DeleteTrackPoint(m, index, retarget));
                break;
            }
            default: return false;
        }
        if (!written) return false;
        selKind = SceneItemKind.None; selIndex = -1;
        RefreshAll(rebuild: true);
        host.ShowSelection(scene, SceneItemKind.None, -1, center: false);
        return true;
    }

    private void Duplicate()
    {
        if (model is null || store is null) return;
        var i = selIndex;
        var index = -1;
        switch (selKind)
        {
            case SceneItemKind.Actor when i > 0:
                var slotPut = false;
                var lba2 = store.Game == SceneGame.Lba2;
                if (Edit($"Duplicate actor {i} of scene {scene}", m => { var copy = m.Actors[i].Clone(); copy.X += 512; if (lba2) slotPut = SceneOps.EnsureMecaPenguinSlot(m); index = SceneOps.AddActor(m, copy); }))
                {
                    Select(SceneItemKind.Actor, index, fromView: false);
                    if (slotPut) Say("Actor 1 is the Meca-penguin's slot, which the game hides whatever is in: the scene's own actors start at 2.");
                }
                break;
            case SceneItemKind.Zone:
                if (Edit($"Duplicate zone {i} of scene {scene}", m => { var copy = m.Zones[i].Clone(); copy.X0 += 512; copy.X1 += 512; index = SceneOps.AddZone(m, copy); })) Select(SceneItemKind.Zone, index, fromView: false);
                break;
            case SceneItemKind.Point:
                if (Edit($"Duplicate track point {i} of scene {scene}", m => index = SceneOps.AddTrackPoint(m, m.TrackPoints[i] with { X = m.TrackPoints[i].X + 512 }))) Select(SceneItemKind.Point, index, fromView: false);
                break;
        }
    }

    // A drag in the view let go: the item moved to (x, y, z) -- a zone by its box's least corner, keeping its size.
    public bool MoveTo(SceneItemKind kind, int index, int x, int y, int z)
    {
        if (model is null) return false;
        return kind switch
        {
            SceneItemKind.Actor when index < model.Actors.Count => Edit($"Move actor {index} of scene {scene}", m => { var a = m.Actors[index]; a.X = x; a.Y = y; a.Z = z; }),
            SceneItemKind.Point when index < model.TrackPoints.Count => Edit($"Move track point {index} of scene {scene}", m => m.TrackPoints[index] = new SceneTrackPoint(x, y, z)),
            SceneItemKind.Zone when index < model.Zones.Count => Edit($"Move zone {index} of scene {scene}", m =>
            {
                var zone = m.Zones[index];
                int dx = x - Math.Min(zone.X0, zone.X1), dy = y - Math.Min(zone.Y0, zone.Y1), dz = z - Math.Min(zone.Z0, zone.Z1);
                zone.X0 += dx; zone.X1 += dx; zone.Y0 += dy; zone.Y1 += dy; zone.Z0 += dz; zone.Z1 += dz;
            }),
            _ => false,
        };
    }

    // ---- the scene as a whole

    private void CheckScene()
    {
        if (store is null || model is null) return;
        var issues = store.Validate(scene, model, store.Game == SceneGame.Lba1 ? store.LoadGrid(scene) : null);
        MessageBox.Show(host.HostWindow, issues.Count == 0 ? "No problems found." : string.Join("\n", issues.Take(25).Select(i => i.ToString())) + (issues.Count > 25 ? $"\n... {issues.Count - 25} more" : ""),
            "Scene check", MessageBoxButton.OK, issues.Any(i => i.Severity == SceneIssueSeverity.Error) ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    // The game's scene table is fixed, so a "new" scene replaces one of the existing ones.
    private void SaveIntoSlot()
    {
        if (store is null || model is null) return;
        if (ListPickWindow.Pick(host.HostWindow, "Save into which scene?", AllScenes().ToList(), scene,
                "The scene table is fixed, so this replaces the chosen scene (Edit > Undo puts it back).") is not { } slot || slot == scene) return;
        if (MessageBox.Show(host.HostWindow, $"Replace scene {slot} with scene {scene}?", "Save into another slot", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        writing = true;
        try
        {
            var grid = store.Game == SceneGame.Lba1 ? store.LoadGrid(scene) : null;
            var result = store.Save(slot, model, grid, description: $"Save scene {scene} into scene {slot}");
            Say($"Scene {scene} saved into scene {slot}{SavedInto()} ({result.RecordBytes} bytes).");
        }
        catch (Exception error) when (error is SceneValidationException or SceneEditException or IOException or InvalidOperationException or InvalidDataException)
        {
            MessageBox.Show(host.HostWindow, $"Not saved: {error.Message}", "Save into another slot", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        finally { writing = false; }
        host.Written(store.Game, new[] { slot });
    }

    private void GoToOtherScene()
    {
        if (ListPickWindow.Pick(host.HostWindow, "Go to which scene?", AllScenes().ToList(), scene, "The view shows it, and this panel its actors, zones and track points.") is { } s) host.GoToScene(s);
    }

    private void OpenGridEditor()
    {
        if (store is null || model is null) return;
        try
        {
            if (store.Game == SceneGame.Lba1) { new GridEditorWindow(store.Directory, null, null, null, scene).WithOwner(host.HostWindow).ShowOwned(); return; }
            var gridId = new Lba2GridBackend(store.Directory).GridOfScene(scene);
            if (gridId is null) { MessageBox.Show(host.HostWindow, $"Scene {scene} has no interior grid.", "Interior map", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            new GridEditorWindow(null, store.Directory, null, gridId).WithOwner(host.HostWindow).ShowOwned();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException)
        {
            MessageBox.Show(host.HostWindow, error.Message, "Grid editor", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // The scene made a flat floor and Twinsen (LBA1: its grid too, one undoable step; LBA2 interior: the map is written to LBA_BKG.HQR,
    // then the scene).
    private void MakeBlank()
    {
        if (store is null || model is null) return;
        if (MessageBox.Show(host.HostWindow, $"Replace scene {scene}'s map with a flat floor, and its actors, zones and track points with nothing but Twinsen?\n\nEdit > Undo brings it back.",
                "Blank scene", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        try
        {
            if (store.Game == SceneGame.Lba1)
            {
                var (blank, grid) = Lba1BlankScene.Create(store, scene);
                Edit($"Make scene {scene} blank", m =>
                {
                    m.Island = blank.Island; m.GameOverScene = blank.GameOverScene; m.AlphaLight = blank.AlphaLight; m.BetaLight = blank.BetaLight;
                    m.Music = blank.Music; m.SecondMin = blank.SecondMin; m.SecondEcart = blank.SecondEcart; m.Ambient = blank.Ambient;
                    m.Actors.Clear(); m.Actors.AddRange(blank.Actors);
                    m.Zones.Clear(); m.Zones.AddRange(blank.Zones);
                    m.TrackPoints.Clear(); m.TrackPoints.AddRange(blank.TrackPoints);
                }, grid);
            }
            else
            {
                var backend = new Lba2GridBackend(store.Directory);
                var gridId = backend.GridOfScene(scene) ?? throw new SceneEditException($"Scene {scene} has no interior grid.");
                var (blank, grid) = Lba2BlankScene.Create(store, backend, scene);
                backend.SaveGrid(gridId, grid);
                Edit($"Make scene {scene} a blank interior", m =>
                {
                    m.Actors.Clear(); m.Actors.AddRange(blank.Actors);
                    m.Zones.Clear(); m.Zones.AddRange(blank.Zones);
                    m.TrackPoints.Clear(); m.TrackPoints.AddRange(blank.TrackPoints);
                });
            }
        }
        catch (Exception error) when (error is SceneEditException or IOException or InvalidDataException or InvalidOperationException)
        {
            MessageBox.Show(host.HostWindow, error.Message, "Blank scene", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        selKind = SceneItemKind.None; selIndex = -1;
        RefreshAll(rebuild: true);
        Say("The scene is a flat floor and Twinsen now. Add actors, zones and track points with Add.");
    }
}
