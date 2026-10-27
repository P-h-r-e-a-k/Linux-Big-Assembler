using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using LBAAssembler.Terrain;

namespace LBAAssembler;

// The island terrain editor, hosted by the main window's Build mode (it is not a window of its own). An island (.ILE) from
// above, edited with brushes: heights (with levelling tools for uneven islands), the baked light and shadows, the ground
// texture / game codes / water depth, and the decor objects. Works on IslandFile (Terrain/), saves through it (a .bak of the
// original, only changed records rewritten).
//
// It hands out two pieces for the host to place: Panel (undo / save, tools, brush, settings, object, baked light, ground atlas)
// and MapArea (an optional top-down map of the island with its height profile). The host's own 3D view is the main way to edit:
// it finds the ground point under the mouse and feeds it to PointerDown / PointerMove / PointerUp; every change raises Edited so
// the host can show it live. The host says which island to open and hears about status text and saves through events.
internal sealed class IslandEditorView
{
    private const string Caption = "Island terrain editor";

    private enum Tool
    {
        Navigate,
        Raise, Lower, Smooth, Flatten, LevelPlane, Ramp, Terrace, Relief,
        PaintLight, Darken, Lighten, RemoveShadows, CastShadows, BlobShadow, WaterDepth,
        Eyedropper, PaintTile, PaintTexture, PaintCode, FixDiagonals,
        SelectDecor, RotateDecor, AddDecor, ObjectShadow, ClearObjectShadow,
    }

    private static readonly (string Group, (Tool Tool, string Name, string Tip)[] Items)[] ToolGroups =
    {
        ("View", new[]
        {
            (Tool.Navigate, "Move the view", "No editing: drag to orbit, the wheel zooms, the middle button pans (the view is never changed by clicking)"),
        }),
        ("Height", new[]
        {
            (Tool.Raise, "Raise", "Hold to build the ground up under the brush"),
            (Tool.Lower, "Lower", "Hold to dig the ground down"),
            (Tool.Smooth, "Smooth", "Blends each vertex towards its neighbours"),
            (Tool.Flatten, "Flatten to level", "Pulls the ground to the Level value (Alt-click reads the level from the ground under the pointer)"),
            (Tool.LevelPlane, "Level to plane", "Fits a plane to the ground under the brush when the stroke starts and pulls the stroke onto it: removes the bumps and keeps the slope (tick 'Horizontal' to flatten it too)"),
            (Tool.Ramp, "Ramp", "Click the two ends: the ground between them becomes a straight ramp, as wide as the brush"),
            (Tool.Terrace, "Terrace", "Snaps heights to multiples of the Terrace step"),
            (Tool.Relief, "Relief x", "Exaggerates (>1) or flattens (<1) the relief under the brush around its mean height"),
        }),
        ("Light & shadow", new[]
        {
            (Tool.PaintLight, "Set light", "Paints the brightness (0-15) given by the Light value"),
            (Tool.Darken, "Add shadow", "Hold to darken: paint a shadow"),
            (Tool.Lighten, "Lighten", "Hold to brighten"),
            (Tool.RemoveShadows, "Remove shadows", "Lifts vertices that are darker than the terrain's plain lighting back up to it (only brightens)"),
            (Tool.CastShadows, "Cast shadows", "Adds the shadows the terrain (and decors, if ticked) cast for the light angle in the bake settings (only darkens)"),
            (Tool.BlobShadow, "Blob shadow", "Click to drop a round shadow (the Light value sets its depth)"),
            (Tool.WaterDepth, "Water depth", "Sets how far Twinsen sinks on water and marsh polygons (0-15 steps of 200 units)"),
        }),
        ("Ground", new[]
        {
            (Tool.Eyedropper, "Pick triangle", "Click to take the texture, game code and diagonal of a triangle"),
            (Tool.PaintTexture, "Paint picked", "Paints the picked triangle's texture (and diagonal if ticked) onto the cells"),
            (Tool.PaintTile, "Paint atlas tile", "Paints the tile selected on the ground texture atlas (drag a square on it) onto the cells"),
            (Tool.PaintCode, "Paint game code", "Sets what the ground does (water, lava, electric, conveyor ...) without touching the picture"),
            (Tool.FixDiagonals, "Fix diagonals", "Re-cuts the cells under the brush along their flatter diagonal (after big height edits)"),
        }),
        ("Objects", new[]
        {
            (Tool.SelectDecor, "Select / move", "Click an object to select it, drag to move it, Delete removes it. Q and E turn the selected object 15 degrees (Shift: 90)"),
            (Tool.RotateDecor, "Rotate", "Press on an object and drag around it to turn it: it snaps to 15 degrees (hold Shift to turn freely). Q and E turn the selected object 15 degrees (Shift: 90)"),
            (Tool.AddDecor, "Add object", "Click to place a new object: a copy of the selected one, or the Body number of the island's OBL file typed under Selected object"),
            (Tool.ObjectShadow, "Shadow under object", "Click an object to bake a shadow under its bounding box, as the retail islands have under buildings (depth = Shadow depth)"),
            (Tool.ClearObjectShadow, "Clear object shadow", "Click an object to lift the baked shadow under it back to plain lighting"),
        }),
    };

    private string gameDirectory;
    private IslandFile? island;
    private IslandHistory? history;
    private IslandMapRenderer? renderer;
    private WriteableBitmap? bitmap;
    private byte[] palette = Array.Empty<byte>();
    private MapView view = MapView.Terrain;
    private Tool tool = Tool.Navigate;

    // view
    private double zoom = 1;
    private Point pan;
    private bool panning; private Point panStart; private Point panOrigin;

    // stroke state
    private bool stroking;
    private (double Gx, double Gz) pointer;
    private (double A, double B, double C)? strokePlane;
    private IslandOps.DecorFollow? follow;
    private (double Gx, double Gz, double H)? rampStart;
    private IslandGround.Sample? picked;
    private (IslandCube Cube, IslandDecor Decor)? selected;
    private bool draggingDecor;
    private (int X, int Y, int W, int H)? tile;
    private readonly DispatcherTimer strokeTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    // Coalesces the overlay rebuild during a zoom/pan gesture: overlay is a child of `world` (see its
    // constructor wiring), so it already moves and scales for free under world's own RenderTransform --
    // RebuildOverlay only needs to run again to keep grid/marker stroke widths crisp (thin = 1/zoom), not to
    // reposition anything. Rebuilding on every wheel notch or mouse-move pixel (both fire dozens of times a
    // second) was walking every island cube's decor list and reallocating a Rectangle+Ellipse per decor each
    // time, which is what made zooming/panning feel stuttery. One rebuild ~80ms after the gesture settles
    // looks identical and costs a tiny fraction as much.
    private readonly DispatcherTimer overlayRebuildTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };

    private void ScheduleOverlayRebuild()
    {
        overlayRebuildTimer.Stop();
        overlayRebuildTimer.Start();
    }

    // controls
    private readonly ComboBox viewBox = new();
    private readonly CheckBox mapBox = new() { Content = "Show the top-down map instead of the 3D view" };
    private readonly Canvas world = new();
    private readonly Image mapImage = new();
    private readonly Canvas overlay = new();
    private readonly Grid viewport = new() { Background = Brushes.Transparent, ClipToBounds = true };
    private readonly Rectangle brushCircle = new() { Stroke = Brushes.White, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Slider radius = new() { Minimum = 0, Maximum = 60, Value = 8, Width = 200 };
    // A square brush, its sides along the cells and its middle on the vertex nearest the pointer: every vertex in it at full strength,
    // `radius` vertices out each way (0: the one vertex) -- for ground in steps and terraces with straight edges, as LBA1's grids are built
    // (2026-10-23, recreating LBA1's Hamalayi by hand: a round brush's soft edge leaves no straight terrace edge or cliff).
    private readonly CheckBox squareBox = new() { Content = "Square brush (whole cells, full strength)" };
    private readonly CheckBox slopeTileBox = new() { Content = "Stand the tile up the slope (cliffs)" };
    private readonly TextBox tileXBox = new() { Text = "0" };
    private readonly TextBox tileYBox = new() { Text = "0" };
    private readonly TextBox tileSizeBox = new() { Text = "32" };
    private readonly TextBox vertexXBox = new() { Text = "0" };
    private readonly TextBox vertexZBox = new() { Text = "0" };
    // where the tool was last used (a vertex): a typed stroke starts there
    private (double Gx, double Gz)? lastUsed;
    private readonly Slider hardness = new() { Minimum = 0, Maximum = 0.95, Value = 0.4, Width = 200 };
    private readonly Slider strength = new() { Minimum = 1, Maximum = 100, Value = 40, Width = 200 };
    private readonly TextBox levelBox = new() { Text = "1000" };
    private readonly TextBox lightBox = new() { Text = "9" };
    private readonly TextBox stepBox = new() { Text = "400" };
    private readonly TextBox reliefBox = new() { Text = "0.8" };
    private readonly TextBox bodyBox = new() { Text = "0" };
    private readonly ComboBox codeBox = new();
    private readonly ComboBox usedBodiesBox = new();
    // the Object maker's window, and the boxes of the bodies it made that no object uses yet (Add object copies an object of the same body
    // for its box; a new body has none)
    private ObjectMakerWindow? maker;
    private readonly Dictionary<int, IslandDecor> madeTemplates = new();
    private string usedBodiesKey = "";
    private readonly CheckBox horizontalBox = new() { Content = "Horizontal (flatten the slope too)" };
    private readonly CheckBox followBox = new() { Content = "Objects follow the ground", IsChecked = true };
    private readonly CheckBox diagonalBox = new() { Content = "Also copy the diagonal" };
    private readonly CheckBox steepUnwalkableBox = new() { Content = "Block terrain steeper than the angle below" };
    private readonly TextBox steepAngleBox = new() { Text = "45" };
    private readonly TextBox azimuthBox = new() { Text = "0" };
    private readonly TextBox elevationBox = new() { Text = "45" };
    private readonly TextBox gainBox = new() { Text = "11" };
    private readonly TextBox offsetBox = new() { Text = "1.2" };
    private readonly TextBox shadowLevelBox = new() { Text = "3" };
    private readonly TextBox shadowDepthBox = new() { Text = "5" };
    private readonly CheckBox terrainShadowBox = new() { Content = "Terrain casts shadows", IsChecked = true };
    private readonly CheckBox decorShadowBox = new() { Content = "Objects cast shadows" };
    private readonly TextBlock info = new() { TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), FontSize = 11 };
    private readonly TextBlock heightmapSize = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly Canvas profile = new() { Height = 96, ClipToBounds = true };
    private readonly Image atlasImage = new() { Width = 256, Height = 256, Stretch = Stretch.Fill };
    private readonly Canvas atlasCanvas = new() { Width = 256, Height = 256 };
    private readonly Rectangle atlasSelection = new() { Stroke = Brushes.Yellow, StrokeThickness = 1, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private readonly Button undoButton = new() { Content = "Undo" };
    private readonly Button redoButton = new() { Content = "Redo" };
    private readonly Button saveButton = new() { Content = "Save" };
    private readonly Dictionary<Tool, RadioButton> toolButtons = new();
    private readonly TextBox[] decorFields = Enumerable.Range(0, 8).Select(_ => new TextBox { Padding = new Thickness(2) }).ToArray();
    private readonly StackPanel decorPanel = new();
    private bool loadingFields;
    private string? currentName;

    public event Action<string>? StatusChanged;
    public event Action? Saved;
    public event Action? StateChanged;
    public event Action? Edited;                 // the island changed (a stroke tick, a commit, an undo): the host can refresh its own view
    public event Action<bool>? MapRequested;     // the top-down map was ticked / unticked in the panel
    public event Action? BodiesAdded;            // a body was added to the island's OBL (the Object maker): the host's 3D view reads it again

    public UIElement MapArea { get; private set; } = null!;
    public UIElement Panel { get; private set; } = null!;
    public string? CurrentName => currentName;
    public bool Dirty => history is { Dirty: true };
    public string? UndoLabel => history?.CanUndo == true ? history.UndoLabel ?? "" : null;
    public string? RedoLabel => history?.CanRedo == true ? history.RedoLabel ?? "" : null;
    private Window? Owner => Window.GetWindow(MapArea);
    private bool mapActive;
    // Whether the top-down map is the visible surface; while it isn't nothing is drawn for it.
    public bool MapActive
    {
        get => mapActive;
        set
        {
            if (mapActive == value) return;
            mapActive = value;
            if (value && renderer is not null) { RedrawAll(); FitView(); }
            if (mapBox.IsChecked != value) mapBox.IsChecked = value;
        }
    }


    public IslandEditorView(string gameDirectory)
    {
        this.gameDirectory = gameDirectory;
        BuildLayout();
        strokeTimer.Tick += (_, _) => { if (stroking) StrokeTick(); };
        overlayRebuildTimer.Tick += (_, _) => { overlayRebuildTimer.Stop(); RebuildOverlay(); };
    }

    // The game folder can change under File > Settings.
    public string GameDirectory { get => gameDirectory; set => gameDirectory = value; }

    // ---- layout ---------------------------------------------------------------------------------------------------------------------------

    // Resource key names, not resolved Brush values, so DynamicResource inside these Setters keeps tracking
    // the active theme once the Style is applied to a live Button -- WPF resolves a Setter's DynamicResource
    // against whatever element the Style ends up on, not against anything at XamlReader.Parse time.
    private static readonly Style ButtonStyle = (Style)System.Windows.Markup.XamlReader.Parse(
        "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>" +
        "<Setter Property='Foreground' Value='{DynamicResource ThemeTextBrush}'/><Setter Property='Background' Value='{DynamicResource ThemeHoverBrush}'/><Setter Property='BorderBrush' Value='{DynamicResource ThemeBorderBrush}'/>" +
        "<Setter Property='Padding' Value='9,3'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>" +
        "<Border x:Name='B' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' Padding='{TemplateBinding Padding}'>" +
        "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers>" +
        "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Background' Value='{DynamicResource ThemeHoverBrush}'/></Trigger>" +
        "<Trigger Property='IsPressed' Value='True'><Setter TargetName='B' Property='Background' Value='{DynamicResource ThemeBorderBrush}'/></Trigger>" +
        "<Trigger Property='IsEnabled' Value='False'><Setter TargetName='B' Property='Opacity' Value='0.45'/></Trigger>" +
        "</ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");

    private void BuildLayout()
    {
        info.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        // Fully qualified: this class's own Panel property (the map-area piece it hands out) would otherwise
        // shadow the System.Windows.Controls.Panel type name here.
        profile.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty, "ThemeFieldBrush");
        // action bar at the top of the panel: undo / redo / save, weld, check, and the optional top-down map
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
        undoButton.ToolTip = "Undo (Ctrl+Z)"; redoButton.ToolTip = "Redo (Ctrl+Y)"; saveButton.ToolTip = "Save the island (Ctrl+S); the original is kept as .bak";
        foreach (var (button, handler, tip) in new (Button, RoutedEventHandler, string?)[]
        {
            (undoButton, (_, _) => DoUndo(), null), (redoButton, (_, _) => DoRedo(), null), (saveButton, (_, _) => Save(), null),
            (new Button { Content = "Weld borders" }, (_, _) => Weld(), "Makes the shared vertices on cube borders agree again"),
            (new Button { Content = "Check" }, (_, _) => Check(), "Looks for anything the game may not like"),
        })
        {
            button.Style = ButtonStyle; button.Margin = new Thickness(0, 0, 6, 6);
            if (tip is not null) button.ToolTip = tip;
            button.Click += handler;
            actions.Children.Add(button);
        }
        var liveNote = new TextBlock { Text = "The 3D view shows your edits as you make them (from a preview copy). Save writes the island into the game folder.", TextWrapping = TextWrapping.Wrap, FontSize = 10, Margin = new Thickness(0, 2, 0, 6) };
        liveNote.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
        foreach (var (v, name) in new[] { (MapView.Terrain, "Terrain (as lit)"), (MapView.Height, "Height"), (MapView.Light, "Baked light"), (MapView.Shadows, "Baked shadows (vs plain light)"), (MapView.GameCode, "Game codes"), (MapView.WaterDepth, "Water depth") })
            viewBox.Items.Add(new ComboBoxItem { Content = name, Tag = v });
        viewBox.SelectedIndex = 0; viewBox.Margin = new Thickness(0, 4, 0, 0);
        viewBox.SelectionChanged += (_, _) => { if (viewBox.SelectedItem is ComboBoxItem { Tag: MapView v }) { view = v; if (mapActive) RedrawAll(); } };
        mapBox.SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush"); mapBox.Margin = new Thickness(0, 2, 0, 0);
        mapBox.ToolTip = "Replace the 3D view with a map from above that can also show the height, the baked light and shadows, the game codes and the water depth";
        mapBox.Checked += (_, _) => MapRequested?.Invoke(true);
        mapBox.Unchecked += (_, _) => MapRequested?.Invoke(false);
        var mapRow = new StackPanel();
        mapRow.Children.Add(mapBox);
        mapRow.Children.Add(viewBox);
        var fitButton = new Button { Content = "Fit the map", Style = ButtonStyle, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        fitButton.Click += (_, _) => FitView();
        mapRow.Children.Add(fitButton);

        // panel: actions, tools, brush, settings, pointer info, object, baked light, ground atlas
        var stack = new StackPanel { Margin = new Thickness(14, 10, 14, 14) };
        stack.Children.Add(actions);
        stack.Children.Add(liveNote);
        stack.Children.Add(Section("Top-down map", mapRow, open: false, out _));
        var tools = new StackPanel();
        foreach (var (group, items) in ToolGroups)
        {
            var heading = Heading(group);
            tools.Children.Add(heading);
            var grid = new UniformGrid { Columns = 2 };
            if (group is not ("View" or "Objects")) { terrainOnlyParts.Add(heading); terrainOnlyParts.Add(grid); }
            foreach (var (t, name, tip) in items)
            {
                var button = new RadioButton { Content = name, GroupName = "tool", ToolTip = tip, Margin = new Thickness(0, 1, 4, 1) };
                button.SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush");
                var captured = t;
                button.Checked += (_, _) => SelectTool(captured);
                toolButtons[t] = button;
                grid.Children.Add(button);
            }
            tools.Children.Add(grid);
        }
        stack.Children.Add(Section("Tools", tools, open: true, out _));

        // a heightmap picture in and out (the tester SIN, 2026-10-17: "have it tell you what pixel x pixel it needs")
        var heightmap = new StackPanel();
        heightmapSize.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        heightmap.Children.Add(heightmapSize);
        var heightmapButtons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var (text, tip, handler) in new (string, string, Action)[]
        {
            ("Import heightmap…", "Shapes the island's ground from a greyscale picture (PNG, 8 or 16 bits; raw 16-bit; numbers in a CSV): black the lowest ground, white the highest. Shows the result live; one undo step", () => ImportHeightmap()),
            ("Export heightmap…", "Writes the island's heights as a picture of exactly the size Import wants (16-bit PNG keeps every height), to edit in a paint program and import again", ExportHeightmap),
        })
        {
            var b = new Button { Content = text, ToolTip = tip, Style = ButtonStyle, Margin = new Thickness(0, 0, 6, 6) };
            b.Click += (_, _) => handler();
            heightmapButtons.Children.Add(b);
        }
        heightmap.Children.Add(heightmapButtons);
        var heightmapSection = Section("Heightmap", heightmap, open: true, out _);
        terrainOnlyParts.Add(heightmapSection);
        stack.Children.Add(heightmapSection);

        var brush = new StackPanel();
        brush.Children.Add(SliderRow("Radius", radius, "vertices (cells); [ and ] change it"));
        squareBox.SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush");
        squareBox.ToolTip = "The brush a square along the cells, its middle on the vertex nearest the pointer, every vertex in it at full strength (Hardness doesn't apply): Radius 0 is one vertex, 1 a 3 x 3, 2 a 5 x 5. For straight terrace edges and cliffs.";
        brush.Children.Add(squareBox);
        brush.Children.Add(SliderRow("Hardness", hardness, "how much of the radius is full strength"));
        brush.Children.Add(SliderRow("Strength", strength, "rate while held"));
        var brushSection = Section("Brush", brush, open: true, out _);
        terrainOnlyParts.Add(brushSection);
        stack.Children.Add(brushSection);

        var settings = new StackPanel();
        settings.Children.Add(FieldRow("Level", levelBox, "world units; the height Flatten pulls to"));
        settings.Children.Add(FieldRow("Light value", lightBox, "0-15: Set light / Blob depth / Water depth"));
        settings.Children.Add(FieldRow("Terrace step", stepBox, "world units"));
        settings.Children.Add(FieldRow("Relief factor", reliefBox, "<1 flatten, >1 exaggerate"));
        for (var i = 0; i < 16; i++) codeBox.Items.Add($"{i}: {IslandPolygon.CodeJeuNames[i]}");
        codeBox.SelectedIndex = 1;
        settings.Children.Add(FieldRow("Game code", codeBox, "what the ground does"));
        foreach (Control c in new Control[] { horizontalBox, followBox, diagonalBox, steepUnwalkableBox, terrainShadowBox, decorShadowBox })
            c.SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush");
        settings.Children.Add(horizontalBox); settings.Children.Add(diagonalBox);
        steepUnwalkableBox.ToolTip = "While raising, lowering, smoothing or otherwise reshaping the ground, marks any ground triangle steeper than this as blocked (Twinsen can't step onto it) -- and un-marks one that flattens back below it.";
        settings.Children.Add(steepUnwalkableBox);
        settings.Children.Add(FieldRow("Unwalkable angle °", steepAngleBox, "from horizontal; a cliff face is close to 90°"));
        // (the same, all over the island at once: ground shaped before the box was ticked, or by another tool)
        var steepAll = new Button { Content = "Block steep ground on the whole island", Style = ButtonStyle, Margin = new Thickness(0, 4, 6, 6), HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = "Marks every ground triangle steeper than the angle as blocked, and every one flatter as walkable, all over the island (one undo step)" };
        System.Windows.Automation.AutomationProperties.SetHelpText(steepAll, (string)steepAll.ToolTip);
        steepAll.Click += (_, _) =>
        {
            if (island is null || history is null) return;
            history.Begin();
            var n = IslandGround.SetSteepCollision(island, new WholeIslandRegion(), Number(steepAngleBox, 45));
            history.Commit("block steep ground");
            RedrawAll(); UpdateButtons();
            SetStatus($"Steep ground on the whole island: {n} triangles changed (blocked where steeper than {Number(steepAngleBox, 45):0}°, walkable where flatter).");
        };
        settings.Children.Add(steepAll);
        var settingsSection = Section("Tool settings", settings, open: true, out _);
        terrainOnlyParts.Add(settingsSection);
        stack.Children.Add(settingsSection);

        stack.Children.Add(Section("Under the pointer", info, open: true, out _));

        // the tool at a vertex typed in: one dab, or a straight stroke from the last place to it -- exact without a steady hand, and from the
        // keyboard (the arrow keys move the brush a vertex, Shift eight; Space dabs: HandleKey)
        var at = new StackPanel();
        at.Children.Add(FieldRow("Vertex X", vertexXBox, "the island's vertex number across (as 'Under the pointer' shows it)"));
        at.Children.Add(FieldRow("Vertex Z", vertexZBox, "the island's vertex number down"));
        var atButtons = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var (text, tip, handler) in new (string, string, Action)[]
                 {
                     ("Apply the tool here", "One dab of the chosen tool at that vertex (as one click of the mouse there)", () => ApplyAtTyped(stroke: false)),
                     ("Stroke to here", "A straight stroke of the chosen tool from where it was last used to that vertex (as a drag of the mouse)", () => ApplyAtTyped(stroke: true)),
                 })
        {
            var b = new Button { Content = text, ToolTip = tip, Style = ButtonStyle, Margin = new Thickness(0, 0, 6, 6) };
            System.Windows.Automation.AutomationProperties.SetHelpText(b, tip);
            b.Click += (_, _) => handler();
            atButtons.Children.Add(b);
        }
        at.Children.Add(atButtons);
        // (in Buildings and decor too: Add object or Select at a vertex typed in)
        var atSection = Section("Brush at a vertex", at, open: true, out _);
        stack.Children.Add(atSection);

        var place = new StackPanel();
        place.Children.Add(FieldRow("Object body", bodyBox, "Body number in the island's OBL file, for Add object (selecting an object fills it in)"));
        usedBodiesBox.ToolTip = "The bodies this island already uses, with how many objects each has: pick one to place more of it";
        usedBodiesBox.SelectionChanged += (_, _) => { if (usedBodiesBox.SelectedItem is ComboBoxItem { Tag: int body }) bodyBox.Text = body.ToString(); };
        place.Children.Add(FieldRow("Used here", usedBodiesBox, "The bodies this island already uses, with how many objects each has: pick one to place more of it"));
        place.Children.Add(followBox);
        var makeButton = new Button { Content = "Make an object…", Style = ButtonStyle, Margin = new Thickness(0, 6, 6, 6), HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = "The Object maker: build a new object of LBA1's bricks, cell by cell, and add it to the island's bodies for Add object to place" };
        System.Windows.Automation.AutomationProperties.SetHelpText(makeButton, (string)makeButton.ToolTip);
        makeButton.Click += (_, _) => OpenMaker();
        place.Children.Add(makeButton);
        stack.Children.Add(Section("Place objects", place, open: true, out _));

        BuildDecorPanel();
        stack.Children.Add(Section("Selected object", decorPanel, open: true, out _));

        var bake = new StackPanel();
        bake.Children.Add(FieldRow("Azimuth °", azimuthBox, "direction the light comes from (the cube's BetaLight is 360 - azimuth)"));
        bake.Children.Add(FieldRow("Elevation °", elevationBox, "height of the light above the horizon"));
        bake.Children.Add(FieldRow("Gain", gainBox, "brightness = offset + gain x (normal . light)"));
        bake.Children.Add(FieldRow("Offset", offsetBox, ""));
        bake.Children.Add(FieldRow("Shadow level", shadowLevelBox, "the brightest a shadowed vertex may be (0-15)"));
        bake.Children.Add(FieldRow("Shadow depth", shadowDepthBox, "levels a footprint shadow under an object darkens by (retail buildings: about 5)"));
        bake.Children.Add(terrainShadowBox); bake.Children.Add(decorShadowBox);
        var bakeButtons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        void BakeButton(string text, string tip, RoutedEventHandler handler)
        {
            var b = new Button { Content = text, ToolTip = tip, Style = ButtonStyle, Margin = new Thickness(0, 0, 6, 6) };
            b.Click += handler; bakeButtons.Children.Add(b);
        }
        BakeButton("Use the cube's light", "Reads the azimuth and elevation from the island's first cube", (_, _) => LoadBakeFromCube());
        BakeButton("Bake all light", "Recomputes the whole island's brightness from its terrain (replaces the stored light, shadows included)", (_, _) => BakeWhole(false));
        BakeButton("Add cast shadows", "Darkens vertices the terrain / objects shade, everywhere", (_, _) => BakeWhole(true));
        BakeButton("Shadows under objects", "Bakes a footprint shadow under every object at least 2 cells across (like the retail buildings)", (_, _) => FootprintsAll(false));
        BakeButton("Remove all shadows", "Lifts every shadowed vertex back to the plain lighting", (_, _) => RemoveAllShadows());
        bake.Children.Add(bakeButtons);
        var bakeSection = Section("Baked light and shadows", bake, open: false, out _);
        terrainOnlyParts.Add(bakeSection);
        stack.Children.Add(bakeSection);

        var atlas = new StackPanel();
        var atlasNote = new TextBlock { Text = "Drag a square, then use Paint atlas tile.", FontSize = 10, Margin = new Thickness(0, 0, 0, 4) };
        atlasNote.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
        atlas.Children.Add(atlasNote);
        atlasCanvas.Children.Add(atlasImage); atlasCanvas.Children.Add(atlasSelection);
        atlasCanvas.MouseLeftButtonDown += AtlasDown; atlasCanvas.MouseMove += AtlasMove; atlasCanvas.MouseLeftButtonUp += (_, _) => atlasCanvas.ReleaseMouseCapture();
        var atlasBorder = new Border { BorderThickness = new Thickness(1), Child = atlasCanvas, HorizontalAlignment = HorizontalAlignment.Left };
        atlasBorder.SetResourceReference(Border.BorderBrushProperty, "ThemeTextMutedBrush");
        atlas.Children.Add(atlasBorder);
        // (the tile typed in too -- its corner and size in pixels -- and a picture or an LBA1 brick's face put into it: new ground for the
        // island, 2026-10-23, recreating LBA1's Hamalayi by hand needed its snow and ice cliffs on the ground)
        slopeTileBox.SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush");
        slopeTileBox.ToolTip = "Paint atlas tile stands the tile up each cell's slope -- its top along the cell's high edge -- so a cliff's picture hangs down the cliff whichever way it faces (level cells get it as usual)";
        atlas.Children.Add(slopeTileBox);
        atlas.Children.Add(FieldRow("Tile X", tileXBox, "the tile's left edge in the atlas (pixels, 0-255)"));
        atlas.Children.Add(FieldRow("Tile Y", tileYBox, "the tile's top edge in the atlas (pixels, 0-255)"));
        atlas.Children.Add(FieldRow("Tile size", tileSizeBox, "the tile's width and height (pixels: 32 is a retail island's tile)"));
        var atlasButtons = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var (text, tip, handler) in new (string, string, Action)[]
                 {
                     ("Choose this tile", "The tile typed in above, for Paint atlas tile (as dragging a square on the atlas)", ChooseTypedTile),
                     ("Put an LBA1 brick here…", "One of LBA1's bricks -- its top, a side, or a side over another brick's -- into the chosen tile, in the island's colours", PutLba1Brick),
                     ("Put a picture here…", "A picture (PNG, BMP, JPG) into the chosen tile, scaled to it, in the island's colours", PutPicture),
                 })
        {
            var b = new Button { Content = text, ToolTip = tip, Style = ButtonStyle, Margin = new Thickness(0, 0, 6, 6) };
            System.Windows.Automation.AutomationProperties.SetHelpText(b, tip);
            b.Click += (_, _) => handler();
            atlasButtons.Children.Add(b);
        }
        atlas.Children.Add(atlasButtons);
        var atlasSection = Section("Ground atlas", atlas, open: false, out openAtlas);
        terrainOnlyParts.Add(atlasSection);
        stack.Children.Add(atlasSection);
        Panel = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = stack };

        // map area: the map and the profile strip
        DockPanel.SetDock(profile, Dock.Bottom);
        var centre = new DockPanel { ClipToBounds = true };
        centre.Children.Add(profile);
        RenderOptions.SetBitmapScalingMode(mapImage, BitmapScalingMode.NearestNeighbor);
        world.Children.Add(mapImage); world.Children.Add(overlay); world.Children.Add(brushCircle);
        world.RenderTransformOrigin = new Point(0, 0);
        viewport.Children.Add(world);
        viewport.MouseWheel += (_, e) => ZoomAt(e.GetPosition(viewport), e.Delta > 0 ? 1.2 : 1 / 1.2);
        viewport.MouseLeftButtonDown += ViewDown; viewport.MouseLeftButtonUp += ViewUp;
        viewport.MouseMove += ViewMove; viewport.MouseLeave += (_, _) => brushCircle.Visibility = Visibility.Collapsed;
        viewport.MouseRightButtonDown += (_, e) => { panning = true; panStart = e.GetPosition(viewport); panOrigin = pan; viewport.CaptureMouse(); };
        viewport.MouseRightButtonUp += (_, _) => { panning = false; viewport.ReleaseMouseCapture(); };
        viewport.MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { panning = true; panStart = e.GetPosition(viewport); panOrigin = pan; viewport.CaptureMouse(); } };
        viewport.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { panning = false; viewport.ReleaseMouseCapture(); } };
        viewport.SizeChanged += (_, _) => { if (renderer is not null && zoom == 1 && pan == default) FitView(); };
        centre.Children.Add(viewport);
        MapArea = centre;

        toolButtons[Tool.Navigate].IsChecked = true;
    }

    private Action<bool> openAtlas = _ => { };

    // A collapsible group in the side panel.
    private static UIElement Section(string title, UIElement content, bool open, out Action<bool> setOpen)
    {
        var body = new Border { Child = content, Visibility = open ? Visibility.Visible : Visibility.Collapsed, Padding = new Thickness(0, 2, 0, 4) };
        var header = new TextBlock { Text = (open ? "▾  " : "▸  ") + title, FontFamily = new FontFamily("Consolas"), FontSize = 10.5 };
        header.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
        // (a button drawn as the heading alone -- reached by Tab, opened with Space or Enter, and by screen readers and UI Automation as
        // "<title>", which a heading the mouse alone could click wasn't, 2026-10-23)
        var toggle = new Button { Content = header, Cursor = Cursors.Hand, Margin = new Thickness(0, 12, 0, 2), HorizontalAlignment = HorizontalAlignment.Left,
            Template = HeadingTemplate, Focusable = true };
        System.Windows.Automation.AutomationProperties.SetName(toggle, title);
        void Set(bool show)
        {
            body.Visibility = show ? Visibility.Visible : Visibility.Collapsed; header.Text = (show ? "▾  " : "▸  ") + title;
            System.Windows.Automation.AutomationProperties.SetHelpText(toggle, show ? "open: click to close" : "closed: click to open");
        }
        Set(open);
        toggle.Click += (_, _) => Set(body.Visibility != Visibility.Visible);
        setOpen = Set;
        var panel = new StackPanel();
        panel.Children.Add(toggle); panel.Children.Add(body);
        return panel;
    }

    // a button that is only its content (a section's heading), a dotted outline when it has the keyboard's focus
    private static readonly ControlTemplate HeadingTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
        "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>" +
        "<Border x:Name='b' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='Transparent' BorderThickness='1' BorderBrush='Transparent'><ContentPresenter/></Border>" +
        "<ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='Gray'/></Trigger></ControlTemplate.Triggers>" +
        "</ControlTemplate>");

    private static TextBlock Label(string text)
    {
        var t = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        t.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
        return t;
    }

    private static TextBlock Heading(string text)
    {
        var t = new TextBlock { Text = text, FontSize = 10, Margin = new Thickness(0, 12, 0, 4) };
        t.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
        return t;
    }

    private static UIElement FieldRow(string label, Control box, string tip)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var text = Label(label); text.ToolTip = tip;
        grid.Children.Add(text);
        box.ToolTip = tip; box.Padding = new Thickness(3);
        // (named for screen readers and UI Automation by its label)
        System.Windows.Automation.AutomationProperties.SetName(box, label);
        System.Windows.Automation.AutomationProperties.SetHelpText(box, tip);
        if (box is TextBox)
        {
            box.SetResourceReference(Control.BackgroundProperty, "ThemeFieldBrush");
            box.SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush");
            box.SetResourceReference(Control.BorderBrushProperty, "ThemeBorderBrush");
        }
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);
        return grid;
    }

    private static UIElement SliderRow(string label, Slider slider, string tip)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var text = Label(label); text.ToolTip = tip;
        grid.Children.Add(text);
        slider.Width = double.NaN; slider.ToolTip = tip;
        System.Windows.Automation.AutomationProperties.SetName(slider, label);
        System.Windows.Automation.AutomationProperties.SetHelpText(slider, tip);
        Grid.SetColumn(slider, 1);
        grid.Children.Add(slider);
        // (its value beside it: a brush set to an exact size needs to show it)
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        var value = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0), FontSize = 11 };
        value.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        void Show() => value.Text = slider.Maximum <= 1 ? slider.Value.ToString("0.00") : slider.Value.ToString("0.#");
        slider.ValueChanged += (_, _) => Show();
        Show();
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);
        return grid;
    }

    private void BuildDecorPanel()
    {
        var names = new[] { "Body", "X", "Y", "Z", "Angle °", "Game code", "Hide var", "Cube" };
        for (var i = 0; i < names.Length; i++) decorPanel.Children.Add(FieldRow(names[i], decorFields[i], i == 6 ? "A game variable that hides the object while set (negative: while clear); 0 = always shown" : ""));
        decorFields[7].IsReadOnly = true;
        var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        void B(string text, string tip, RoutedEventHandler handler)
        {
            var b = new Button { Content = text, ToolTip = tip, Style = ButtonStyle, Margin = new Thickness(0, 0, 6, 6) };
            b.Click += handler; buttons.Children.Add(b);
        }
        B("Apply", "Writes the fields to the object (the ZV moves with it)", (_, _) => ApplyDecorFields());
        B("Drop to ground", "", (_, _) => EditSelected("drop object", (c, d) => IslandDecors.DropToGround(island!, c, d)));
        B("Shadow under", "Bakes a footprint shadow under this object", (_, _) => FootprintSelected(false));
        B("Clear shadow", "Lifts the shadow under this object back to plain lighting", (_, _) => FootprintSelected(true));
        B("Duplicate", "", (_, _) => DuplicateSelected());
        B("Delete", "", (_, _) => DeleteSelected());
        decorPanel.Children.Add(buttons);
        decorPanel.IsEnabled = false;
    }

    // ---- opening, saving --------------------------------------------------------------------------------------------------------------------

    // Opens an island of the game folder (by file name, e.g. "DESERT.ILE"); false when the unsaved changes of the island now open
    // were neither saved nor discarded (the user cancelled) or the file couldn't be read.
    public bool Open(string name, bool reload = false)
    {
        if (!reload && island is not null && string.Equals(name, currentName, StringComparison.OrdinalIgnoreCase)) return true;
        if (!ConfirmDiscard()) return false;
        try
        {
            var path = System.IO.Path.Combine(gameDirectory, name);
            island = IslandFile.Load(path);
            history = new IslandHistory(island);
            history.Changed += () => { atlasImage.Source = AtlasBitmap(); RedrawAll(); UpdateButtons(); };
            palette = IslandMapRenderer.LoadPalette(gameDirectory, System.IO.Path.GetFileNameWithoutExtension(name));
            var (mapCells, _) = (Math.Max(island.PresentBounds().MaxX - island.PresentBounds().MinX + 1, island.PresentBounds().MaxZ - island.PresentBounds().MinZ + 1) * 64, 0);
            renderer = new IslandMapRenderer(island, palette, Math.Clamp(1800 / mapCells, 2, 8));
            bitmap = new WriteableBitmap(renderer.PixelWidth, renderer.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
            mapImage.Source = bitmap; mapImage.Width = renderer.PixelWidth; mapImage.Height = renderer.PixelHeight;
            overlay.Width = world.Width = renderer.PixelWidth; overlay.Height = world.Height = renderer.PixelHeight;
            LoadBakeFromCube();
            atlasImage.Source = AtlasBitmap();
            selected = null; rampStart = null; picked = null; tile = null; atlasSelection.Visibility = Visibility.Collapsed;
            decorPanel.IsEnabled = false;
            RedrawAll(); FitView(); UpdateButtons();
            var (lo, hi) = IslandOps.HeightRange(island);
            SetStatus($"{name}: {island.Cubes.Count} cubes, height {lo}..{hi}, {island.Cubes.Values.Sum(c => c.Decors.Count)} objects. Left button edits, right/middle drag pans, wheel zooms.");
            var layout = HeightmapLayout.Of(island);
            heightmapSize.Text = $"{System.IO.Path.GetFileNameWithoutExtension(name)} is {layout.CubesX} × {layout.CubesZ} cubes, so its heightmap is {layout.Width} × {layout.Height} pixels "
                + $"(64 cells a cube plus one: neighbouring cubes share their edge). It lies as the top-down map shows the island: top-left = lowest X and Z.";
            currentName = name;
            StateChanged?.Invoke();
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(Owner, $"Couldn't open {name}: {e.Message}", Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private BitmapSource AtlasBitmap()
    {
        var sixBit = palette.Take(768).Max() <= 63;
        var pixels = new byte[256 * 256 * 4];
        for (var i = 0; i < 256 * 256; i++)
        {
            var p = island!.GroundTexture[i] * 3;
            pixels[i * 4] = (byte)Math.Min(255, palette[p + 2] * (sixBit ? 4 : 1));
            pixels[i * 4 + 1] = (byte)Math.Min(255, palette[p + 1] * (sixBit ? 4 : 1));
            pixels[i * 4 + 2] = (byte)Math.Min(255, palette[p] * (sixBit ? 4 : 1));
            pixels[i * 4 + 3] = 255;
        }
        var bmp = BitmapSource.Create(256, 256, 96, 96, PixelFormats.Bgra32, null, pixels, 256 * 4);
        bmp.Freeze();
        return bmp;
    }

    // Asks what to do with unsaved changes; false = the user cancelled (or the save failed), so the caller must stay where it is.
    public bool ConfirmDiscard()
    {
        if (history is not { Dirty: true }) return true;
        var answer = MessageBox.Show(Owner, $"Save the changes to {currentName ?? "this island"} first?\n\nYes saves them, No throws them away.", Caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel) return false;
        if (answer == MessageBoxResult.Yes) return Save();
        // thrown away: drop the edited copy, so the island is read from disk again next time and nothing asks twice
        island = null; history = null; renderer = null; bitmap = null; currentName = null; selected = null;
        mapImage.Source = null; overlay.Children.Clear(); profile.Children.Clear(); decorPanel.IsEnabled = false;
        StateChanged?.Invoke();
        return true;
    }

    public bool Save()
    {
        if (island is null || history is null) return true;
        var problems = IslandValidator.Validate(island).Where(p => p.IsError).ToList();
        if (problems.Count > 0 && MessageBox.Show(Owner, "The island has problems the game may not like:\n\n" + string.Join("\n", problems.Take(8).Select(p => "• " + p.Message)) + "\n\nSave anyway?", Caption, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return false;
        try
        {
            island.Save();
            history.MarkSaved();
            UpdateButtons();
            SetStatus($"Saved {System.IO.Path.GetFileName(island.Path)} (the original is kept as .bak).");
            Saved?.Invoke();
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(Owner, $"Couldn't save: {e.Message}", Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void Check()
    {
        if (island is null) return;
        var problems = IslandValidator.Validate(island);
        MessageBox.Show(Owner, problems.Count == 0 ? "No problems found." : string.Join("\n", problems.Take(30).Select(p => (p.IsError ? "Error  " : "note   ") + p.Message)), "Island check", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Weld()
    {
        if (island is null || history is null) return;
        history.Begin();
        var fixedCount = IslandOps.WeldBorders(island);
        history.Commit("weld borders");
        RedrawAll();
        SetStatus($"Welded {fixedCount} vertices on cube borders.");
    }

    private void UpdateButtons()
    {
        undoButton.IsEnabled = history?.CanUndo == true; redoButton.IsEnabled = history?.CanRedo == true;
        undoButton.ToolTip = history?.UndoLabel is { } u ? $"Undo {u} (Ctrl+Z)" : "Undo (Ctrl+Z)";
        redoButton.ToolTip = history?.RedoLabel is { } r ? $"Redo {r} (Ctrl+Y)" : "Redo (Ctrl+Y)";
        StateChanged?.Invoke();
        saveButton.Content = history?.Dirty == true ? "Save *" : "Save";
        RefreshUsedBodies();
        Edited?.Invoke();
    }

    // The "Used here" list: each body the island's objects use and how many there are of it (rebuilt only when that changes).
    private void RefreshUsedBodies()
    {
        var used = island is null ? new List<(int Body, int Count)>()
            : island.Cubes.Values.SelectMany(c => c.Decors).GroupBy(d => d.Body & 0xFFFF).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => (Body: g.Key, Count: g.Count())).ToList();
        var key = string.Join(",", used.Select(u => $"{u.Body}x{u.Count}"));
        if (key == usedBodiesKey) return;
        usedBodiesKey = key;
        usedBodiesBox.Items.Clear();
        foreach (var (body, count) in used) usedBodiesBox.Items.Add(new ComboBoxItem { Content = $"{body}  (x{count})", Tag = body });
    }

    private void DoUndo() { if (history?.Undo() == true) { ReselectAfterHistory(); } }
    private void DoRedo() { if (history?.Redo() == true) { ReselectAfterHistory(); } }
    private void ReselectAfterHistory() { selected = null; decorPanel.IsEnabled = false; RedrawAll(); UpdateButtons(); }

    // ---- drawing --------------------------------------------------------------------------------------------------------------------------

    private void SetStatus(string text) => StatusChanged?.Invoke(text);

    private void RedrawAll()
    {
        if (renderer is null || bitmap is null || island is null) return;
        if (mapActive)
        {
            var (lo, hi) = IslandOps.HeightRange(island);
            renderer.HeightMin = lo; renderer.HeightMax = Math.Max(lo + 1, hi);
            renderer.RenderAll(view);
            bitmap.WritePixels(new Int32Rect(0, 0, renderer.PixelWidth, renderer.PixelHeight), renderer.Pixels, renderer.PixelWidth * 4, 0);
            RebuildOverlay();
            DrawProfile();
        }
        UpdateButtons();
    }

    // Redraws the cells around a brush stamp.
    private void RedrawAround(double gx, double gz, double reach)
    {
        if (!mapActive || renderer is null || bitmap is null) return;
        var x0 = (int)Math.Floor(gx - reach) - 2; var x1 = (int)Math.Ceiling(gx + reach) + 1;
        var z0 = (int)Math.Floor(gz - reach) - 2; var z1 = (int)Math.Ceiling(gz + reach) + 1;
        x0 = Math.Max(x0, renderer.OriginX); z0 = Math.Max(z0, renderer.OriginZ);
        x1 = Math.Min(x1, renderer.OriginX + renderer.CellsX - 1); z1 = Math.Min(z1, renderer.OriginZ + renderer.CellsZ - 1);
        if (x1 < x0 || z1 < z0) return;
        renderer.Render(view, x0, z0, x1, z1);
        var rect = new Int32Rect((x0 - renderer.OriginX) * renderer.Scale, (z0 - renderer.OriginZ) * renderer.Scale, (x1 - x0 + 1) * renderer.Scale, (z1 - z0 + 1) * renderer.Scale);
        bitmap.WritePixels(rect, renderer.Pixels, renderer.PixelWidth * 4, rect.X, rect.Y);
    }

    private double CellToPixelX(double cellX) => (cellX - renderer!.OriginX) * renderer.Scale;
    private double CellToPixelZ(double cellZ) => (cellZ - renderer!.OriginZ) * renderer.Scale;

    private void RebuildOverlay()
    {
        overlay.Children.Clear();
        if (!mapActive || renderer is null || island is null) return;
        var thin = 1 / zoom;
        // cube borders
        var grid = new GeometryGroup();
        var (minX, minZ, maxX, maxZ) = island.PresentBounds();
        for (var cx = minX; cx <= maxX + 1; cx++) grid.Children.Add(new LineGeometry(new Point(CellToPixelX(cx * 64), 0), new Point(CellToPixelX(cx * 64), renderer.PixelHeight)));
        for (var cz = minZ; cz <= maxZ + 1; cz++) grid.Children.Add(new LineGeometry(new Point(0, CellToPixelZ(cz * 64)), new Point(renderer.PixelWidth, CellToPixelZ(cz * 64))));
        overlay.Children.Add(new System.Windows.Shapes.Path { Data = grid, Stroke = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), StrokeThickness = thin, IsHitTestVisible = false });

        // objects
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
            foreach (var d in cube.Decors)
            {
                double wx = (cx * IslandFile.CubeSize + d.X) / (double)IslandFile.CellSize, wz = (cz * IslandFile.CubeSize + d.Z) / (double)IslandFile.CellSize;
                var isSelected = selected is { } s && s.Decor == d;
                var box = new Rectangle
                {
                    Width = Math.Max(1, (d.XMax - d.XMin) / (double)IslandFile.CellSize * renderer.Scale), Height = Math.Max(1, (d.ZMax - d.ZMin) / (double)IslandFile.CellSize * renderer.Scale),
                    Stroke = isSelected ? Brushes.Yellow : new SolidColorBrush(Color.FromArgb(200, 255, 140, 60)), StrokeThickness = isSelected ? thin * 2 : thin, IsHitTestVisible = false,
                };
                Canvas.SetLeft(box, CellToPixelX((cx * IslandFile.CubeSize + d.XMin) / (double)IslandFile.CellSize)); Canvas.SetTop(box, CellToPixelZ((cz * IslandFile.CubeSize + d.ZMin) / (double)IslandFile.CellSize));
                overlay.Children.Add(box);
                var dot = new Ellipse { Width = 5 * thin, Height = 5 * thin, Fill = isSelected ? Brushes.Yellow : Brushes.OrangeRed, IsHitTestVisible = false };
                Canvas.SetLeft(dot, CellToPixelX(wx) - 2.5 * thin); Canvas.SetTop(dot, CellToPixelZ(wz) - 2.5 * thin);
                overlay.Children.Add(dot);
            }

        if (rampStart is { } r)
        {
            var mark = new Ellipse { Width = 8 * thin, Height = 8 * thin, Fill = Brushes.Cyan, IsHitTestVisible = false };
            Canvas.SetLeft(mark, CellToPixelX(r.Gx) - 4 * thin); Canvas.SetTop(mark, CellToPixelZ(r.Gz) - 4 * thin);
            overlay.Children.Add(mark);
        }
        brushCircle.StrokeThickness = thin;
    }

    private void ApplyTransform()
    {
        world.RenderTransform = new MatrixTransform(zoom, 0, 0, zoom, pan.X, pan.Y);
        // overlay is a child of world (see BuildLayout), so it already tracks this transform for free --
        // only stroke widths (thin = 1/zoom in RebuildOverlay) actually go stale here, and only on a zoom
        // change, not a pure pan. Scheduling instead of rebuilding inline is what keeps a wheel/drag gesture
        // smooth; see ScheduleOverlayRebuild's own comment.
        ScheduleOverlayRebuild();
    }

    private void FitView()
    {
        if (renderer is null || viewport.ActualWidth < 10) return;
        zoom = Math.Min(viewport.ActualWidth / renderer.PixelWidth, viewport.ActualHeight / renderer.PixelHeight) * 0.98;
        pan = new Point((viewport.ActualWidth - renderer.PixelWidth * zoom) / 2, (viewport.ActualHeight - renderer.PixelHeight * zoom) / 2);
        ApplyTransform();
    }

    private void ZoomAt(Point at, double factor)
    {
        // Only a UX guard rail, not a correctness limit: nothing downstream (stroke widths already go
        // through Math.Max(1, ...), pixel math is all doubles) breaks at either end. Kept finite rather than
        // fully unbounded so a runaway scroll can't zoom to a size that's merely a wall of degenerate
        // 1px boxes with no visible content -- FitView is the recovery either way.
        var next = Math.Clamp(zoom * factor, 0.02, 100);
        factor = next / zoom;
        pan = new Point(at.X - (at.X - pan.X) * factor, at.Y - (at.Y - pan.Y) * factor);
        zoom = next;
        ApplyTransform();
    }

    private (double Gx, double Gz) ToCell(Point screen)
    {
        var px = (screen.X - pan.X) / zoom; var pz = (screen.Y - pan.Y) / zoom;
        return (px / renderer!.Scale + renderer.OriginX, pz / renderer.Scale + renderer.OriginZ);
    }

    private void DrawProfile()
    {
        profile.Children.Clear();
        if (island is null || renderer is null || profile.ActualWidth < 10) return;
        var gz = (int)Math.Round(pointer.Gz);
        var w = profile.ActualWidth; var h = profile.ActualHeight;
        var (lo, hi) = IslandOps.HeightRange(island);
        var span = Math.Max(1, hi - lo);
        var points = new PointCollection();
        for (var gx = renderer.OriginX; gx <= renderer.OriginX + renderer.CellsX; gx++)
        {
            if (island.HeightAt(gx, gz) is not { } height) continue;
            points.Add(new Point((gx - renderer.OriginX) / (double)renderer.CellsX * w, h - 6 - (height - lo) / span * (h - 12)));
        }
        if (points.Count > 1) profile.Children.Add(new Polyline { Points = points, Stroke = Brushes.LightGreen, StrokeThickness = 1.2 });
        if (double.TryParse(levelBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var level))
        {
            var y = h - 6 - (level - lo) / span * (h - 12);
            profile.Children.Add(new Line { X1 = 0, X2 = w, Y1 = y, Y2 = y, Stroke = Brushes.Orange, StrokeDashArray = new DoubleCollection { 4, 4 }, StrokeThickness = 1 });
        }
        var px = (pointer.Gx - renderer.OriginX) / renderer.CellsX * w;
        profile.Children.Add(new Line { X1 = px, X2 = px, Y1 = 0, Y2 = h, Stroke = Brushes.White, StrokeThickness = 1, Opacity = 0.6 });
        var profileLabel = new TextBlock { Text = $"height along row {gz}   ({lo}..{hi}, orange = Level)", FontSize = 10, Margin = new Thickness(4, 1, 0, 0) };
        profileLabel.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
        profile.Children.Add(profileLabel);
    }

    // ---- tools ----------------------------------------------------------------------------------------------------------------------------

    private void SelectTool(Tool t)
    {
        tool = t; rampStart = null;
        if (renderer is not null) RebuildOverlay();
        brushCircle.Visibility = t is Tool.SelectDecor or Tool.RotateDecor or Tool.AddDecor or Tool.ObjectShadow or Tool.ClearObjectShadow ? Visibility.Collapsed : brushCircle.Visibility;
        if (t == Tool.PaintTile) openAtlas(true);
        var tip = ToolGroups.SelectMany(g => g.Items).First(i => i.Tool == t).Tip;
        SetStatus(tip);
    }

    private static double Number(TextBox box, double fallback) => double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private BakeOptions BakeSettings() => new()
    {
        Azimuth = Number(azimuthBox, 0), Elevation = Math.Clamp(Number(elevationBox, 45), 1, 89), Gain = Number(gainBox, 11), Offset = Number(offsetBox, 1.2),
        ShadowLevel = (int)Math.Clamp(Number(shadowLevelBox, 3), 0, 15), TerrainShadows = terrainShadowBox.IsChecked == true, DecorShadows = decorShadowBox.IsChecked == true,
    };

    private void LoadBakeFromCube()
    {
        if (island is null || island.Cubes.Count == 0) return;
        var o = BakeOptions.For(island.Cubes.Values.First());
        azimuthBox.Text = o.Azimuth.ToString("F1", CultureInfo.InvariantCulture); elevationBox.Text = o.Elevation.ToString("F1", CultureInfo.InvariantCulture);
        gainBox.Text = o.Gain.ToString(CultureInfo.InvariantCulture); offsetBox.Text = o.Offset.ToString(CultureInfo.InvariantCulture);
        if (renderer is not null) { renderer.Baseline = BakeSettings(); if (view is MapView.Shadows or MapView.Height) RedrawAll(); }
    }

    private void Whole(string label, Func<int> action)
    {
        if (island is null || history is null) return;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            history.Begin();
            var n = action();
            history.Commit(label);
            SetStatus($"{label}: {n} vertices changed.");
        }
        finally { Mouse.OverrideCursor = null; }
        RedrawAll();
    }

    private void BakeWhole(bool shadowsOnly)
    {
        if (island is null) return;
        var options = BakeSettings();
        renderer!.Baseline = options;
        Whole(shadowsOnly ? "cast shadows" : "bake light", () => shadowsOnly ? IslandBake.CastShadows(island, new WholeIslandRegion(), options) : IslandBake.Bake(island, new WholeIslandRegion(), options));
    }

    private int ShadowDepth => (int)Math.Clamp(Number(shadowDepthBox, 5), 1, 15);

    private void FootprintsAll(bool remove)
    {
        if (island is null) return;
        var options = BakeSettings();
        Whole(remove ? "clear object shadows" : "shadows under objects", () => IslandBake.FootprintShadowAll(island, options, ShadowDepth, remove));
    }

    private void FootprintSelected(bool remove)
    {
        if (selected is not { } s || island is null || history is null) return;
        var cell = island.CellsOf(s.Cube.Id).FirstOrDefault();
        history.Begin();
        var n = IslandBake.FootprintShadow(island, cell.X, cell.Z, s.Decor, BakeSettings(), ShadowDepth, remove);
        history.Commit(remove ? "clear object shadow" : "object shadow");
        RedrawAll();
        SetStatus($"{(remove ? "Cleared" : "Added")} the shadow under the object: {n} vertices changed.");
    }

    private void RemoveAllShadows()
    {
        if (island is null) return;
        var options = BakeSettings();
        Whole("remove shadows", () => IslandBake.LiftShadows(island, new WholeIslandRegion(), options));
    }

    private IslandRegion Brush()
    {
        if (squareBox.IsChecked != true) return new BrushRegion(pointer.Gx, pointer.Gz, radius.Value, hardness.Value);
        var (cx, cz, r) = SquareBrush();
        return new RectRegion(cx - r, cz - r, cx + r, cz + r);
    }

    // the square brush's middle vertex and reach (vertices)
    private (int Cx, int Cz, int R) SquareBrush() => ((int)Math.Round(pointer.Gx), (int)Math.Round(pointer.Gz), (int)Math.Round(radius.Value));

    // ---- the pointer: from the top-down map here, or from the host's 3D view through PointerDown / Move / Up ----------------------------------

    private double pointerTolerance = 3;      // how close (cells) a click must be to an object to pick it

    private void Capture() { if (mapActive) viewport.CaptureMouse(); }
    private void ReleaseCapture() { if (mapActive) viewport.ReleaseMouseCapture(); }

    public bool Busy => stroking || draggingDecor || rotatingDecor || previewing;
    public bool NavigateTool => tool == Tool.Navigate;
    // tools that paint with the round brush (the host draws the brush ring for them)
    public bool BrushTool => tool is not (Tool.Navigate or Tool.Eyedropper or Tool.Ramp or Tool.SelectDecor or Tool.RotateDecor or Tool.AddDecor or Tool.ObjectShadow or Tool.ClearObjectShadow);
    public double BrushRadius => radius.Value;
    public (double X, double Y, double Z)? SelectedObjectWorld => selected is { } s ? WorldOf(s) : null;

    // The selected object's bounding box (its ZV) in island world units, as its eight corners: the bottom four then the top four.
    public (double X, double Y, double Z)[]? SelectedObjectBox
    {
        get
        {
            if (selected is not { } s) return null;
            var (ox, _, oz) = WorldOf(s);
            double offX = ox - s.Decor.X, offZ = oz - s.Decor.Z;
            var (x0, x1, z0, z1, y0, y1) = (s.Decor.XMin + offX, s.Decor.XMax + offX, s.Decor.ZMin + offZ, s.Decor.ZMax + offZ, (double)s.Decor.YMin, (double)s.Decor.YMax);
            return new[] { (x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1), (x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1) };
        }
    }

    // The editor shows only what places and turns buildings and other objects (true), or all of its tools (false).
    private bool decorOnly;
    private readonly List<UIElement> terrainOnlyParts = new();
    public bool DecorOnly
    {
        get => decorOnly;
        set
        {
            if (decorOnly == value) return;
            decorOnly = value;
            foreach (var part in terrainOnlyParts) part.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            if (value && tool is not (Tool.SelectDecor or Tool.RotateDecor or Tool.AddDecor or Tool.ObjectShadow or Tool.ClearObjectShadow)) toolButtons[Tool.SelectDecor].IsChecked = true;
            else if (!value && tool is Tool.SelectDecor or Tool.RotateDecor or Tool.AddDecor) toolButtons[Tool.Navigate].IsChecked = true;
        }
    }
    public string? IslandPath => island?.Path;
    public (int Lo, int Hi) HeightRange => island is null ? (0, 0) : IslandOps.HeightRange(island);
    public byte[]? ToBytes() => island?.ToBytes();
    public double GroundAltitude(double worldX, double worldZ) => island is null ? double.NaN : IslandOps.Altitude(island, worldX, worldZ) ?? double.NaN;

    private (double X, double Y, double Z) WorldOf((IslandCube Cube, IslandDecor Decor) s)
    {
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island!))
            if (cube == s.Cube) return (cx * IslandFile.CubeSize + s.Decor.X, s.Decor.Y, cz * IslandFile.CubeSize + s.Decor.Z);
        return (0, 0, 0);
    }

    // The ground point (island cell units) under the pointer, from a host surface. Returns whether a stroke / drag started (so the host keeps the mouse).
    public bool PointerDown(double gx, double gz, double toleranceCells)
    {
        if (island is null || history is null) return false;
        pointer = (gx, gz);
        pointerTolerance = Math.Max(1, toleranceCells);
        PointerDownCore();
        return Busy;
    }

    public void PointerMove(double gx, double gz)
    {
        pointer = (gx, gz);
        if (island is null) return;
        if (draggingDecor && selected is { } s) MoveDecorTo(s);
        else if (rotatingDecor && selected is { } r) RotateTo(r);
        UpdateInfo();
    }

    public void PointerUp() => PointerUpCore();

    private void ViewDown(object sender, MouseButtonEventArgs e)
    {
        if (island is null || history is null || renderer is null) return;
        viewport.Focus();
        if (tool == Tool.Navigate) { panning = true; panStart = e.GetPosition(viewport); panOrigin = pan; viewport.CaptureMouse(); return; }
        pointer = ToCell(e.GetPosition(viewport));
        pointerTolerance = Math.Max(3, 12 / (zoom * renderer.Scale));
        PointerDownCore();
    }

    private void PointerDownCore()
    {
        if (island is null || history is null) return;
        if (Keyboard.Modifiers == ModifierKeys.Alt) { SampleLevel(); return; }
        switch (tool)
        {
            case Tool.Navigate: return;
            case Tool.Eyedropper: Pick(); return;
            case Tool.Ramp: RampClick(); return;
            case Tool.SelectDecor: SelectAt(); return;
            case Tool.RotateDecor: RotateStart(); return;
            case Tool.AddDecor: AddAt(); return;
            case Tool.ObjectShadow or Tool.ClearObjectShadow:
                selected = DecorAt(pointer.Gx, pointer.Gz);
                decorPanel.IsEnabled = selected is not null;
                LoadDecorFields();
                if (selected is null) SetStatus("No object under the pointer.");
                else FootprintSelected(tool == Tool.ClearObjectShadow);
                RebuildOverlay();
                return;
            case Tool.BlobShadow: RunOnce("blob shadow", () => IslandLightOps.BlobShadow(island, pointer.Gx, pointer.Gz, radius.Value, Math.Clamp(Number(lightBox, 6), 1, 15))); return;
        }
        stroking = true;
        history.Begin();
        strokePlane = null;
        follow = ChangesHeight(tool) && followBox.IsChecked == true ? new IslandOps.DecorFollow(island) : null;
        if (tool == Tool.LevelPlane) strokePlane = IslandOps.FitPlane(island, Brush());
        Capture();
        StrokeTick();
        strokeTimer.Start();
    }

    private void ViewUp(object sender, MouseButtonEventArgs e)
    {
        if (tool == Tool.Navigate && panning) { panning = false; viewport.ReleaseMouseCapture(); return; }
        PointerUpCore();
    }

    private void PointerUpCore()
    {
        if (draggingDecor) { draggingDecor = false; history?.Commit("move object"); ReleaseCapture(); RebuildOverlay(); UpdateButtons(); return; }
        if (rotatingDecor) { FinishRotate(); return; }
        if (!stroking) return;
        stroking = false;
        lastUsed = (pointer.Gx, pointer.Gz);
        strokeTimer.Stop();
        ReleaseCapture();
        var label = ToolGroups.SelectMany(g => g.Items).First(i => i.Tool == tool).Name.ToLowerInvariant();
        if (follow is not null) follow.Apply();
        history!.Commit(label);
        follow = null;
        RebuildOverlay();
        UpdateButtons(); DrawProfile();
        if (view is MapView.Shadows or MapView.Height) RedrawAll();
    }

    private void ViewMove(object sender, MouseEventArgs e)
    {
        var screen = e.GetPosition(viewport);
        if (panning)
        {
            pan = new Point(panOrigin.X + screen.X - panStart.X, panOrigin.Y + screen.Y - panStart.Y);
            ApplyTransform();
            return;
        }
        if (!mapActive || renderer is null || island is null) return;
        pointer = ToCell(screen);
        // (the square brush's outline: round its vertices, half a cell out -- the cells whose corners it moves)
        PlaceBrushOutline();
        if (draggingDecor && selected is { } s) MoveDecorTo(s);
        else if (rotatingDecor && selected is { } turning) RotateTo(turning);
        UpdateInfo();
        if (!stroking) DrawProfile();
    }

    // The height-sculpting tools: the ones that move vertices, so "objects follow the ground" and the
    // auto-unwalkable-slope option both apply to exactly this set (and not, say, texture or game-code painting).
    private static bool ChangesHeight(Tool t) => t is Tool.Raise or Tool.Lower or Tool.Smooth or Tool.Flatten or Tool.LevelPlane or Tool.Terrace or Tool.Relief;

    private void StrokeTick()
    {
        if (island is null || renderer is null) return;
        var region = Brush();
        var s = strength.Value;
        var n = 0;
        switch (tool)
        {
            case Tool.Raise: n = IslandOps.Raise(island, region, s * 0.25); break;
            case Tool.Lower: n = IslandOps.Raise(island, region, -s * 0.25); break;
            case Tool.Smooth: n = IslandOps.Smooth(island, region, s / 100 * 0.6); break;
            case Tool.Flatten: n = IslandOps.FlattenTo(island, region, Number(levelBox, 1000), Math.Min(1, s / 100 + 0.2)); break;
            case Tool.LevelPlane: n = strokePlane is { } plane ? IslandOps.LevelToPlane(island, region, plane, Math.Min(1, s / 100 + 0.2), horizontalBox.IsChecked == true ? 1 : 0) : 0; break;
            case Tool.Terrace: n = IslandOps.Terrace(island, region, Math.Max(1, Number(stepBox, 400)), s / 100); break;
            case Tool.Relief: n = IslandOps.ScaleRelief(island, region, 1 + (Number(reliefBox, 0.8) - 1) * s / 100); break;
            case Tool.PaintLight: n = IslandLightOps.Paint(island, region, IslandLightOps.Mode.Set, Math.Clamp(Number(lightBox, 9), 0, 15)); break;
            case Tool.Darken: n = IslandLightOps.Paint(island, region, IslandLightOps.Mode.Darken, 1 + s / 34); break;
            case Tool.Lighten: n = IslandLightOps.Paint(island, region, IslandLightOps.Mode.Lighten, 1 + s / 34); break;
            case Tool.RemoveShadows: n = IslandBake.LiftShadows(island, region, BakeSettings(), 1); break;
            case Tool.CastShadows: n = IslandBake.CastShadows(island, region, BakeSettings()); break;
            case Tool.WaterDepth: n = IslandLightOps.PaintWaterDepth(island, region, (int)Math.Clamp(Number(lightBox, 3), 0, 15)); break;
            case Tool.PaintTexture:
                if (picked is null) { SetStatus("Pick a triangle first (Pick triangle), then paint it."); return; }
                n = IslandGround.Paint(island, region, picked, PolygonFields.Texture | (diagonalBox.IsChecked == true ? PolygonFields.Diagonal : PolygonFields.None)); break;
            case Tool.PaintTile:
                if (tile is not { } t) { SetStatus("Drag a square on the ground atlas (right panel) to choose the tile."); return; }
                n = slopeTileBox.IsChecked == true ? IslandGround.PaintTileOnSlope(island, region, t.X, t.Y, t.W, t.H) : IslandGround.PaintTile(island, region, t.X, t.Y, t.W, t.H); break;
            case Tool.PaintCode: n = IslandGround.PaintGameCode(island, region, codeBox.SelectedIndex); break;
            case Tool.FixDiagonals: n = IslandGround.OptimiseDiagonals(island, region); break;
        }
        if (follow is not null && n > 0) follow.Apply();
        if (n > 0 && ChangesHeight(tool) && steepUnwalkableBox.IsChecked == true) IslandGround.SetSteepCollision(island, region, Number(steepAngleBox, 45));
        if (n > 0) { RedrawAround(pointer.Gx, pointer.Gz, radius.Value + 1); Edited?.Invoke(); }
        if (view is MapView.Shadows or MapView.Height && n > 0) RedrawAround(pointer.Gx, pointer.Gz, radius.Value + 3);
        if (follow is not null && n > 0) RebuildOverlay();
        UpdateInfo();
    }

    // A single action bracketed as one undo step.
    private void RunOnce(string label, Func<int> action)
    {
        if (island is null || history is null) return;
        history.Begin();
        var n = action();
        history.Commit(label);
        RedrawAround(pointer.Gx, pointer.Gz, radius.Value + 2);
        UpdateButtons();
        SetStatus($"{label}: {n} vertices changed.");
    }

    private void SampleLevel()
    {
        if (island is null) return;
        var h = IslandOps.Altitude(island, pointer.Gx * 512, pointer.Gz * 512);
        if (h is null) return;
        levelBox.Text = Math.Round(h.Value).ToString(CultureInfo.InvariantCulture);
        SetStatus($"Level set to {levelBox.Text} from the ground under the pointer.");
        DrawProfile();
    }

    private void Pick()
    {
        if (island is null) return;
        var gx = (int)Math.Floor(pointer.Gx); var gz = (int)Math.Floor(pointer.Gz);
        var u = pointer.Gx - gx; var v = pointer.Gz - gz;
        var cube = island.CubeAt(gx / 64, gz / 64);
        var diagonal = cube is { HasPolygons: true } && new IslandPolygon(cube.Polygon(gx % 64, gz % 64, 0)).Diagonal;
        var half = diagonal ? (u + v < 1 ? 0 : 1) : (u < v ? 0 : 1);
        picked = IslandGround.Pick(island, gx, gz, half);
        if (picked is null) { SetStatus("Nothing to pick there."); return; }
        SetStatus($"Picked: texture {picked.Polygon.TextureIndex}, flags tex {picked.Polygon.TexFlag} poly {picked.Polygon.PolyFlag}, game code {picked.Polygon.CodeJeu} ({IslandPolygon.CodeJeuNames[picked.Polygon.CodeJeu]}), diagonal {(picked.Polygon.Diagonal ? "1-3" : "0-2")}. Choose Paint picked to use it.");
        codeBox.SelectedIndex = picked.Polygon.CodeJeu;
        if (picked.Texture is { } t)
        {
            var xs = new[] { t[0], t[2], t[4] }.Select(x => x / 256.0); var ys = new[] { t[1], t[3], t[5] }.Select(y => y / 256.0);
            Canvas.SetLeft(atlasSelection, xs.Min()); Canvas.SetTop(atlasSelection, ys.Min());
            atlasSelection.Width = Math.Max(1, xs.Max() - xs.Min()); atlasSelection.Height = Math.Max(1, ys.Max() - ys.Min());
            atlasSelection.Stroke = Brushes.Cyan; atlasSelection.Visibility = Visibility.Visible;
        }
    }

    private void RampClick()
    {
        if (island is null || history is null) return;
        var h = IslandOps.Altitude(island, pointer.Gx * 512, pointer.Gz * 512);
        if (h is null) return;
        if (rampStart is null) { rampStart = (pointer.Gx, pointer.Gz, h.Value); RebuildOverlay(); SetStatus($"Ramp starts at height {h.Value:F0}. Click where it ends."); return; }
        var from = rampStart.Value;
        rampStart = null;
        RunOnce("ramp", () =>
        {
            var f = followBox.IsChecked == true ? new IslandOps.DecorFollow(island) : null;
            var halfWidth = Math.Max(1, radius.Value * hardness.Value + 0.5);
            var feather = Math.Max(1, radius.Value * (1 - hardness.Value));
            var n = IslandOps.Ramp(island, from, (pointer.Gx, pointer.Gz, h.Value), halfWidth, feather);
            f?.Apply();
            if (n > 0 && steepUnwalkableBox.IsChecked == true)
            {
                var reach = halfWidth + feather;
                var region = new RectRegion((int)Math.Floor(Math.Min(from.Gx, pointer.Gx) - reach), (int)Math.Floor(Math.Min(from.Gz, pointer.Gz) - reach),
                    (int)Math.Ceiling(Math.Max(from.Gx, pointer.Gx) + reach), (int)Math.Ceiling(Math.Max(from.Gz, pointer.Gz) + reach));
                IslandGround.SetSteepCollision(island, region, Number(steepAngleBox, 45));
            }
            return n;
        });
        RedrawAll();
    }

    // ---- objects ----------------------------------------------------------------------------------------------------------------------------

    // The object under a ground point (cells): the smallest bounding box the point is inside, else the object whose box edge or origin is
    // nearest within the pointer tolerance (so a building is picked by clicking its wall or roof, not only its exact origin).
    // Set by a host that can see the objects on screen: given a bounding box's eight corners (island world units) it says how far from the
    // viewer the box is when the pointer is over it (smaller = nearer), or null when the pointer is not over it.
    public Func<(double X, double Y, double Z)[], double?>? ScreenPick { get; set; }

    private (IslandCube Cube, IslandDecor Decor)? DecorAt(double gx, double gz)
    {
        if (island is null) return null;
        // (every object there, the likeliest first: the nearest on screen, or the smallest whose box the point is in)
        var under = new List<((IslandCube Cube, IslandDecor Decor) Pick, double Score)>();
        if (ScreenPick is { } onScreen)
        {
            foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
            {
                double ox = cx * IslandFile.CubeSize, oz = cz * IslandFile.CubeSize;
                foreach (var d in cube.Decors)
                {
                    double x0 = ox + d.XMin, x1 = ox + d.XMax, z0 = oz + d.ZMin, z1 = oz + d.ZMax, y0 = d.YMin, y1 = d.YMax;
                    if (onScreen(new[] { (x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1), (x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1) }) is { } depth)
                        under.Add(((cube, d), depth));
                }
            }
            if (under.Count > 0) return NextUnder(gx, gz, under);
        }
        const double cell = IslandFile.CellSize;
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
        {
            double ox = cx * IslandFile.CubeSize, oz = cz * IslandFile.CubeSize;
            foreach (var d in cube.Decors)
            {
                double wx = (ox + d.X) / cell, wz = (oz + d.Z) / cell;
                double x0 = (ox + d.XMin) / cell, x1 = (ox + d.XMax) / cell, z0 = (oz + d.ZMin) / cell, z1 = (oz + d.ZMax) / cell;
                var origin = Math.Sqrt((wx - gx) * (wx - gx) + (wz - gz) * (wz - gz));
                double ex = Math.Max(Math.Max(x0 - gx, gx - x1), 0), ez = Math.Max(Math.Max(z0 - gz, gz - z1), 0);
                var edge = Math.Sqrt(ex * ex + ez * ez);
                if (edge > pointerTolerance && origin > pointerTolerance) continue;
                under.Add(((cube, d), edge == 0 ? Math.Max(0, x1 - x0) * Math.Max(0, z1 - z0) : 1e6 + Math.Min(edge, origin)));
            }
        }
        return under.Count > 0 ? NextUnder(gx, gz, under) : null;
    }

    // Objects standing one in another (two at one spot, a tree by a hut) would always give the likeliest of them: picking again at the very
    // same point (the same vertex typed in, a click where the mouse hasn't moved) while one of them is selected gives the next, so each can be
    // reached. A click anywhere else gives the likeliest again, so pressing on a selected object to drag it still drags that one.
    private (double Gx, double Gz)? lastPickAt;

    private (IslandCube Cube, IslandDecor Decor) NextUnder(double gx, double gz, List<((IslandCube Cube, IslandDecor Decor) Pick, double Score)> under)
    {
        var order = under.OrderBy(u => u.Score).Select(u => u.Pick).ToList();
        var samePoint = lastPickAt is { } last && last.Gx == gx && last.Gz == gz;
        lastPickAt = (gx, gz);
        if (order.Count < 2) return order[0];
        var at = samePoint && selected is { } s ? order.FindIndex(e => ReferenceEquals(e.Decor, s.Decor)) : -1;
        var next = order[(at + 1) % order.Count];
        SetStatus($"{order.Count} objects here: object {order.IndexOf(next) + 1} of them selected -- select at this very point again for the next.");
        return next;
    }

    private (double X, double Z) grab;          // the picked object's origin minus the pointer (cells): it moves with the pointer, it doesn't jump to it

    private void SelectAt()
    {
        if (island is null || history is null) return;
        selected = DecorAt(pointer.Gx, pointer.Gz);
        decorPanel.IsEnabled = selected is not null;
        LoadDecorFields();
        RebuildOverlay();
        Edited?.Invoke();
        if (selected is { } s)
        {
            var (wx, _, wz) = WorldOf(s);
            grab = (wx / IslandFile.CellSize - pointer.Gx, wz / IslandFile.CellSize - pointer.Gz);
            history.Begin();
            draggingDecor = true;
            Capture();
        }
    }

    private void MoveDecorTo((IslandCube Cube, IslandDecor Decor) s)
    {
        if (island is null) return;
        var wx = (pointer.Gx + grab.X) * 512; var wz = (pointer.Gz + grab.Z) * 512;
        var y = (int)Math.Round(IslandOps.Altitude(island, wx, wz) ?? s.Decor.Y);
        if (IslandDecors.Move(island, s.Cube, s.Decor, wx, wz, followBox.IsChecked == true ? y : null))
        {
            // the object may have changed cube
            var cube = island.Cubes.Values.First(c => c.Decors.Contains(s.Decor));
            selected = (cube, s.Decor);
            RebuildOverlay(); LoadDecorFields(); Edited?.Invoke();
        }
    }

    // ---- turning an object ----------------------------------------------------------------------------------------------------------------------

    private const int TurnUnits = 4096;         // the engine's full turn
    private bool rotatingDecor;
    private double rotStartPointer;             // the pointer's bearing from the object when the drag began (radians)
    private int rotStartBeta;
    // +1: the object's Beta grows the way the pointer's bearing atan2(dz, dx) does; -1: the other way round
    private const int BetaSign = -1;

    private double PointerBearing((IslandCube Cube, IslandDecor Decor) s)
    {
        var (wx, _, wz) = WorldOf(s);
        return Math.Atan2(pointer.Gz * IslandFile.CellSize - wz, pointer.Gx * IslandFile.CellSize - wx);
    }

    private void RotateStart()
    {
        if (island is null || history is null) return;
        selected = DecorAt(pointer.Gx, pointer.Gz);
        decorPanel.IsEnabled = selected is not null;
        LoadDecorFields();
        RebuildOverlay();
        Edited?.Invoke();
        if (selected is not { } s) { SetStatus("No object under the pointer: press on the one to turn."); return; }
        history.Begin();
        rotatingDecor = true;
        rotStartBeta = s.Decor.Beta & 0xFFFF;
        rotStartPointer = PointerBearing(s);
        Capture();
    }

    private void RotateTo((IslandCube Cube, IslandDecor Decor) s)
    {
        var (wx, _, wz) = WorldOf(s);
        double dx = pointer.Gx * IslandFile.CellSize - wx, dz = pointer.Gz * IslandFile.CellSize - wz;
        if (dx * dx + dz * dz < 256 * 256) return;          // (right on top of the object the bearing is meaningless)
        var turned = (PointerBearing(s) - rotStartPointer) * TurnUnits / (2 * Math.PI) * BetaSign;
        var beta = rotStartBeta + turned;
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) beta = Math.Round(beta / (TurnUnits / 24.0)) * (TurnUnits / 24.0);
        SetFacing(s.Decor, (int)Math.Round(beta));
        LoadDecorFields();
        Edited?.Invoke();
    }

    private void FinishRotate()
    {
        rotatingDecor = false;
        ReleaseCapture();
        if (selected is { } s)
        {
            var total = (((s.Decor.Beta & 0xFFFF) - rotStartBeta) % TurnUnits + TurnUnits) % TurnUnits;
            if (total is TurnUnits / 4 or TurnUnits * 3 / 4) TurnFootprint(s.Decor, total);
        }
        history?.Commit("rotate object");
        RebuildOverlay(); UpdateButtons();
    }

    // The facing is the low 16 bits of Beta; the high 16 (the hide variable) stay.
    private static void SetFacing(IslandDecor d, int units)
        => d.Beta = (d.Beta & ~0xFFFF) | (((units % TurnUnits) + TurnUnits) % TurnUnits);

    // A quarter turn puts the bounding box's long side the other way. The engine turns a body about its origin as Rotate(x, z, beta) does
    // (x' = x cos + z sin, z' = z cos - x sin), so a box corner (dx, dz) from the origin goes to (dz, -dx) for +90 degrees and (-dz, dx) for +270.
    private static void TurnFootprint(IslandDecor d, int turnedUnits)
    {
        int x0 = d.XMin - d.X, x1 = d.XMax - d.X, z0 = d.ZMin - d.Z, z1 = d.ZMax - d.Z;
        if (turnedUnits == TurnUnits / 4) { d.XMin = d.X + z0; d.XMax = d.X + z1; d.ZMin = d.Z - x1; d.ZMax = d.Z - x0; }
        else { d.XMin = d.X - z1; d.XMax = d.X - z0; d.ZMin = d.Z + x0; d.ZMax = d.Z + x1; }
    }

    // Q / E: the next multiple of `degrees` either way from the object's own facing (a quarter turn also turns its box).
    private void TurnSelected(int direction, double degrees)
    {
        EditSelected("rotate object", (_, d) =>
        {
            var current = (d.Beta & 0xFFFF) * 360.0 / TurnUnits;
            var next = direction > 0 ? Math.Floor(current / degrees + 1e-6) * degrees + degrees : Math.Ceiling(current / degrees - 1e-6) * degrees - degrees;
            var units = (int)Math.Round(next * TurnUnits / 360.0);
            var moved = (((units - (d.Beta & 0xFFFF)) % TurnUnits) + TurnUnits) % TurnUnits;
            SetFacing(d, units);
            if (moved is TurnUnits / 4 or TurnUnits * 3 / 4) TurnFootprint(d, moved);
        });
        Edited?.Invoke();
    }

    private void AddAt()
    {
        if (island is null || history is null) return;
        var body = (int)Number(bodyBox, 0);
        history.Begin();
        var like = selected is { } s && (s.Decor.Body & 0xFFFF) == body ? s.Decor : island.Cubes.Values.SelectMany(c => c.Decors).FirstOrDefault(d => (d.Body & 0xFFFF) == body)
            ?? madeTemplates.GetValueOrDefault(body);
        var added = IslandDecors.Add(island, body, pointer.Gx * 512, pointer.Gz * 512, null, like);
        if (added is null) { history.Cancel(); SetStatus("Can't add an object there (off the island, or the cube already holds 200)."); return; }
        history.Commit("add object");
        selected = added; decorPanel.IsEnabled = true;
        LoadDecorFields(); RebuildOverlay(); UpdateButtons();
        SetStatus(like is null ? "Object added with a default 1x1 cell bounding box; adjust it in the game or copy an existing object of the same body." : "Object added (its bounding box copied from another object of the same body).");
    }

    // ---- the Object maker ----------------------------------------------------------------------------------------------------------------

    private void OpenMaker()
    {
        if (island is null) { SetStatus("Open an island first."); return; }
        var directory = EditorSettings.Current.Lba1Directory;
        if (!Lba1.Lba1Game.IsInstalled(directory)) { MessageBox.Show(Owner, "Choose the LBA1 game folder under File > Settings first: the Object maker builds with LBA1's bricks.", Caption, MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (maker is { IsLoaded: true }) { maker.Activate(); return; }
        maker = new ObjectMakerWindow(new Lba1.Lba1Game(directory), AddMadeObject) { Owner = Owner };
        maker.Closed += (_, _) => maker = null;
        maker.Show();
    }

    // The maker's object made a body: its tiles on an object page of the island (an undoable edit, saved with the island), the body added
    // to the island's OBL beside it straight away (a body nothing places is harmless: an undo leaves it there unused), and Object body set
    // to it for Add object. Returns what to tell the user.
    private string AddMadeObject(IReadOnlyDictionary<(int X, int Layer, int Z), int> cells)
    {
        if (island is null || history is null) return "Open an island first.";
        var oblPath = System.IO.Path.ChangeExtension(island.Path, ".OBL");
        if (!File.Exists(oblPath)) return $"The island has no {System.IO.Path.GetFileName(oblPath)} beside it to add the body to.";
        history.Begin();
        BrickObject.Made made;
        try { made = BrickObject.Make(cells, new Lba1.Lba1Game(EditorSettings.Current.Lba1Directory).ReadBrick, new Lba1.Lba1Game(EditorSettings.Current.Lba1Directory).Palette, palette, island); }
        catch (InvalidOperationException e) { history.RestorePending(); history.Cancel(); return e.Message; }
        int body;
        try
        {
            var obl = File.ReadAllBytes(oblPath);
            body = HqrArchive.CountEntries(oblPath);
            new FileTransaction().Write(oblPath, HqrWriter.AppendEntry(obl, HqrWriter.StoredEntry(made.Body))).Commit();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            history.RestorePending(); history.Cancel();
            return $"Couldn't add the body to {System.IO.Path.GetFileName(oblPath)}: {e.Message}";
        }
        history.Commit("make object");
        var template = IslandDecors.Blank(body | (made.Page << IslandFile.DecorPageShift), 0, 0, 0);
        template.XMin = made.XMin; template.XMax = made.XMax; template.YMin = 0; template.YMax = made.YMax; template.ZMin = made.ZMin; template.ZMax = made.ZMax;
        madeTemplates[body] = template;
        bodyBox.Text = body.ToString();
        if (toolButtons.TryGetValue(Tool.AddDecor, out var addTool)) addTool.IsChecked = true;
        UpdateButtons();
        BodiesAdded?.Invoke();
        var text = $"Body {body} added to {System.IO.Path.GetFileName(oblPath)} ({made.Faces} faces, {made.NewTiles} brick faces on object page {made.Page}). Add object now places it: click the ground, or type a vertex under Brush at a vertex and Apply the tool here.";
        SetStatus(text);
        return text;
    }

    private void LoadDecorFields()
    {
        loadingFields = true;
        if (selected is { } s)
        {
            var d = s.Decor;
            var values = new[] { (d.Body & 0xFFFF).ToString(), d.X.ToString(), d.Y.ToString(), d.Z.ToString(), Math.Round((d.Beta & 0xFFFF) * 360.0 / 4096).ToString(CultureInfo.InvariantCulture), d.CodeJeu.ToString(), (d.Beta >> 16).ToString(), $"{s.Cube.Id} ({string.Join(", ", island!.CellsOf(s.Cube.Id).Select(c => $"{c.X},{c.Z}"))})" };
            for (var i = 0; i < values.Length; i++) decorFields[i].Text = values[i];
            bodyBox.Text = values[0];          // (Add object then places a copy of what is selected)
        }
        else foreach (var f in decorFields) f.Text = "";
        loadingFields = false;
    }

    private void ApplyDecorFields()
    {
        if (selected is not { } s || island is null || history is null) return;
        history.Begin();
        var d = s.Decor;
        d.Body = (d.Body & ~0xFFFF) | ((int)Number(decorFields[0], d.Body & 0xFFFF) & 0xFFFF);
        d.MoveTo((int)Number(decorFields[1], d.X), (int)Number(decorFields[2], d.Y), (int)Number(decorFields[3], d.Z));
        var angle = ((int)Math.Round(Number(decorFields[4], 0) * 4096 / 360) % 4096 + 4096) % 4096;
        d.Beta = (d.Beta & ~0xFFFF) | angle;
        d.CodeJeu = (int)Number(decorFields[5], d.CodeJeu);
        d.Beta = (d.Beta & 0xFFFF) | ((int)Number(decorFields[6], d.Beta >> 16) << 16);
        history.Commit("edit object");
        RebuildOverlay(); UpdateButtons();
    }

    private void EditSelected(string label, Action<IslandCube, IslandDecor> action)
    {
        if (selected is not { } s || history is null) return;
        history.Begin(); action(s.Cube, s.Decor); history.Commit(label);
        LoadDecorFields(); RebuildOverlay(); UpdateButtons();
    }

    private void DuplicateSelected()
    {
        if (selected is not { } s || island is null || history is null) return;
        history.Begin();
        var copy = s.Decor.Clone();
        if (s.Cube.Decors.Count >= IslandDecors.MaxPerCube) { history.Cancel(); SetStatus("The cube already holds 200 objects."); return; }
        copy.MoveTo(Math.Min(32767, copy.X + 512), copy.Y, Math.Min(32767, copy.Z + 512));
        s.Cube.Decors.Add(copy);
        history.Commit("duplicate object");
        selected = (s.Cube, copy);
        LoadDecorFields(); RebuildOverlay(); UpdateButtons();
    }

    private void DeleteSelected()
    {
        if (selected is not { } s || history is null) return;
        history.Begin();
        IslandDecors.Remove(s.Cube, s.Decor);
        history.Commit("delete object");
        selected = null; decorPanel.IsEnabled = false; LoadDecorFields();
        RebuildOverlay(); UpdateButtons();
    }

    // ---- atlas ------------------------------------------------------------------------------------------------------------------------------

    private Point atlasStart;
    private void AtlasDown(object sender, MouseButtonEventArgs e)
    {
        atlasStart = e.GetPosition(atlasCanvas);
        atlasCanvas.CaptureMouse();
        UpdateAtlasSelection(atlasStart);
    }

    private void AtlasMove(object sender, MouseEventArgs e)
    {
        if (atlasCanvas.IsMouseCaptured) UpdateAtlasSelection(e.GetPosition(atlasCanvas));
    }

    // The tile typed in (Tile X / Y / size), chosen as a drag would choose it.
    private void ChooseTypedTile()
    {
        if (island is null) return;
        if (!int.TryParse(tileXBox.Text.Trim(), out var x) || !int.TryParse(tileYBox.Text.Trim(), out var y) || !int.TryParse(tileSizeBox.Text.Trim(), out var size)
            || size < 4 || size > 128 || x < 0 || y < 0 || x + size > 256 || y + size > 256)
        { SetStatus("Type the tile's corner (0-255) and size (4-128) so it lies inside the 256 x 256 atlas."); return; }
        ShowTile(x, y, size);
    }

    private void ShowTile(int x, int y, int size)
    {
        tile = (x, y, size, size);
        tileXBox.Text = x.ToString(); tileYBox.Text = y.ToString(); tileSizeBox.Text = size.ToString();
        Canvas.SetLeft(atlasSelection, x); Canvas.SetTop(atlasSelection, y);
        atlasSelection.Width = size; atlasSelection.Height = size;
        atlasSelection.Stroke = Brushes.Yellow; atlasSelection.Visibility = Visibility.Visible;
        SetStatus($"Atlas tile {size}x{size} at ({x}, {y}). Choose Paint atlas tile and paint.");
        toolButtons[Tool.PaintTile].IsChecked = true;
    }

    // A tile's pixels (the island palette's indices) written into the atlas where the chosen tile is, as one undo step.
    private void PutTile(byte[] pixels, string what)
    {
        if (island is null || history is null || tile is not { } t) return;
        history.Begin();
        for (var y = 0; y < t.H; y++)
            for (var x = 0; x < t.W; x++)
                island.GroundTexture[(t.Y + y) * 256 + t.X + x] = pixels[y * t.W + x];
        history.Commit($"put {what} in the atlas");
        atlasImage.Source = AtlasBitmap();
        RedrawAll(); UpdateButtons();
        Edited?.Invoke();
        SetStatus($"{what} put in the atlas at ({t.X}, {t.Y}), {t.W}x{t.H}: paint it with Paint atlas tile.");
    }

    private void PutLba1Brick()
    {
        if (island is null || tile is not { } t) { SetStatus("Choose a tile first (drag a square on the atlas, or type it and Choose this tile)."); return; }
        var directory = EditorSettings.Current.Lba1Directory;
        if (!Lba1.Lba1Game.IsInstalled(directory)) { MessageBox.Show(Owner, "Choose the LBA1 game folder under File > Settings first: the bricks are LBA1's.", Caption, MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var window = new AtlasBrickWindow(new Lba1.Lba1Game(directory), palette, t.W) { Owner = Owner };
        if (window.ShowDialog() == true && window.Tile is { } pixels) PutTile(pixels, window.Description);
    }

    private void PutPicture()
    {
        if (island is null || tile is not { } t) { SetStatus("Choose a tile first (drag a square on the atlas, or type it and Choose this tile)."); return; }
        var open = new Microsoft.Win32.OpenFileDialog { Filter = "Pictures (*.png;*.bmp;*.jpg;*.jpeg;*.gif;*.tif)|*.png;*.bmp;*.jpg;*.jpeg;*.gif;*.tif|All files|*.*", Title = "Put a picture in the ground atlas" };
        if (open.ShowDialog(Owner) != true) return;
        BitmapSource picture;
        try
        {
            var decoder = BitmapDecoder.Create(new Uri(open.FileName), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            picture = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        }
        catch (Exception e) when (e is System.IO.IOException or NotSupportedException or ArgumentException or UnauthorizedAccessException)
        { SetStatus($"Couldn't read {System.IO.Path.GetFileName(open.FileName)}: {e.Message}"); return; }
        var (pw, ph) = (picture.PixelWidth, picture.PixelHeight);
        var src = new byte[pw * ph * 4];
        picture.CopyPixels(src, pw * 4, 0);
        // (scaled to the tile: each of its pixels the picture's under it)
        var pixels = new byte[t.W * t.H];
        for (var y = 0; y < t.H; y++)
            for (var x = 0; x < t.W; x++)
            {
                var i = (Math.Min(ph - 1, y * ph / t.H) * pw + Math.Min(pw - 1, x * pw / t.W)) * 4;
                pixels[y * t.W + x] = (byte)Terrain.Snow.SnowColour.Nearest(palette, src[i + 2], src[i + 1], src[i]);
            }
        PutTile(pixels, System.IO.Path.GetFileName(open.FileName));
    }

    private void UpdateAtlasSelection(Point now)
    {
        // a square from the press point towards the pointer
        var size = (int)Math.Clamp(Math.Max(Math.Abs(now.X - atlasStart.X), Math.Abs(now.Y - atlasStart.Y)), 4, 128);
        var x = (int)Math.Clamp(now.X < atlasStart.X ? atlasStart.X - size : atlasStart.X, 0, 256 - size);
        var y = (int)Math.Clamp(now.Y < atlasStart.Y ? atlasStart.Y - size : atlasStart.Y, 0, 256 - size);
        ShowTile(x, y, size);
    }

    // ---- heightmaps -------------------------------------------------------------------------------------------------------------------------

    private static string? heightmapFolder;
    private bool previewing;            // the import dialog's result is on the island for the 3D view to show (history open since the first preview)

    // Import heightmap: a picture onto the island's ground (HeightmapWindow says what size it needs and shows the result, live in the host's 3D
    // view as it is changed), applied with every option as one undo step. `path`: the picture (else a file dialog asks).
    public void ImportHeightmap(string? path = null)
    {
        if (island is null || history is null || Busy) return;
        if (path is null)
        {
            var open = new Microsoft.Win32.OpenFileDialog { Filter = HeightmapWindow.Filter, Title = $"Import a heightmap into {currentName}", InitialDirectory = heightmapFolder };
            if (open.ShowDialog(Owner) != true) return;
            path = open.FileName;
        }
        heightmapFolder = System.IO.Path.GetDirectoryName(path);
        var defaults = new HeightmapWindow.Defaults(BakeSettings(), BakeLight: true, steepUnwalkableBox.IsChecked == true, Number(steepAngleBox, 45), followBox.IsChecked == true);
        var window = new HeightmapWindow(island, currentName ?? "This island", path, defaults, PreviewHeightmap) { Owner = Owner };
        PlaceAtRight(window, Owner);
        var accepted = false;
        try { accepted = window.ShowDialog() == true && window.Picture is not null; }
        finally { if (!accepted) EndHeightmapPreview(); }
        if (!accepted) { SetStatus("Heightmap import cancelled: the ground is as it was."); return; }

        var picture = window.Picture!;
        var options = window.Options;
        HeightmapImportResult result;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            // the island as it was, then the import with every option (cast shadows too): one undo step from the state before the dialog
            if (previewing) history.RestorePending(); else history.Begin();
            try { result = IslandHeightmap.Apply(island, picture, options); }
            catch (InvalidDataException e)
            {
                history.RestorePending(); history.Cancel(); previewing = false;
                ReselectAfterHistory();
                MessageBox.Show(Owner, $"Couldn't import the heightmap: {e.Message}", Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            history.Commit("import heightmap");
            previewing = false;
        }
        finally { Mouse.OverrideCursor = null; }
        ReselectAfterHistory();
        DrawProfile();
        SetStatus($"Imported {System.IO.Path.GetFileName(path)}: {result.Vertices:N0} height points ({result.Changed:N0} changed), ground {result.Lowest:N0} to {result.Highest:N0}, "
                  + $"{result.SeaCells:N0} sea and {result.LandCells:N0} land cells ({result.Retextured:N0} given new ground)"
                  + (options.SteepAngle is { } a ? $", {result.Blocked:N0} triangles over {a:F0}° blocked" : "")
                  + (options.Bake is not null ? $", light baked again ({result.Relit:N0} points)" : "")
                  + (result.ObjectsMoved > 0 ? $", {result.ObjectsMoved} objects moved with the ground" : "")
                  + (result.Clamped > 0 ? $"; {result.Clamped:N0} heights cut to the game's limits" : "")
                  + ". Undo takes it back; Save writes the island.");
    }

    // The import dialog's live preview: the island put back as it was before the dialog, then (unless null) the picture applied with the dialog's
    // options; the host's 3D view follows through Edited (Busy while previewing, so it is pushed even when nothing is committed yet).
    private void PreviewHeightmap(HeightmapPicture? picture, HeightmapImportOptions? options)
    {
        if (island is null || history is null) return;
        if (picture is null || options is null)
        {
            if (!previewing) return;
            history.RestorePending();
        }
        else
        {
            if (!previewing) { history.Begin(); previewing = true; }
            else history.RestorePending();
            try { IslandHeightmap.Apply(island, picture, options); }
            catch (InvalidDataException) { history.RestorePending(); }
        }
        selected = null; decorPanel.IsEnabled = false;
        RedrawAll();
    }

    // Cancel: the ground put back (and shown) as it was, nothing recorded.
    private void EndHeightmapPreview()
    {
        if (!previewing || history is null) return;
        history.RestorePending();
        RedrawAll();               // (still previewing: the host's 3D view is given the ground back)
        history.Cancel();
        previewing = false;
        UpdateButtons();
    }

    // The dialog at the right of the main window, so the 3D view showing the preview stays in sight.
    private static void PlaceAtRight(Window window, Window? owner)
    {
        if (owner is null || PresentationSource.FromVisual(owner) is not { CompositionTarget: { } target }) return;
        var topRight = target.TransformFromDevice.Transform(owner.PointToScreen(new Point(owner.ActualWidth, 0)));
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = Math.Max(SystemParameters.WorkArea.Left, topRight.X - window.Width - 16);
        window.Top = Math.Max(SystemParameters.WorkArea.Top, topRight.Y + 60);
    }

    // Export heightmap: the island's heights as a picture of exactly the size Import wants, black its lowest ground and white its highest (a
    // PNG keeps the two in a text chunk, so Import reads them back and the heights come back exactly).
    public void ExportHeightmap()
    {
        if (island is null) return;
        var stem = System.IO.Path.GetFileNameWithoutExtension(currentName ?? "island");
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "16-bit greyscale PNG, every height kept (*.png)|*.png|8-bit greyscale PNG, 256 levels (*.png)|*.png|Raw 16-bit heights, little-endian (*.r16)|*.r16|Heights as numbers, world units (*.csv)|*.csv",
            FileName = stem.ToLowerInvariant() + "_heightmap.png", Title = $"Export {stem}'s heightmap", InitialDirectory = heightmapFolder,
        };
        if (dialog.ShowDialog(Owner) != true) return;
        var format = dialog.FilterIndex switch { 2 => IslandHeightmap.ExportFormat.Png8, 3 => IslandHeightmap.ExportFormat.Raw16, 4 => IslandHeightmap.ExportFormat.Csv, _ => IslandHeightmap.ExportFormat.Png16 };
        heightmapFolder = System.IO.Path.GetDirectoryName(dialog.FileName);
        try
        {
            var (black, white) = IslandHeightmap.Export(island, dialog.FileName, format, null, currentName);
            var layout = HeightmapLayout.Of(island);
            SetStatus($"Exported {stem}'s heightmap to {dialog.FileName}: {layout.Width} × {layout.Height} pixels"
                      + (format == IslandHeightmap.ExportFormat.Csv ? ", heights in world units." : $", black = {black:N0}, white = {white:N0}"
                      + (format == IslandHeightmap.ExportFormat.Raw16 ? " (a raw file can't say so: give Import the same two)." : " (kept in the picture: Import reads them back)."))
                      + " Edit it and use Import heightmap to bring it back.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(Owner, $"Couldn't write the heightmap: {e.Message}", Caption, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---- keyboard, info ---------------------------------------------------------------------------------------------------------------------

    // The tool used at the vertex typed in (Brush at a vertex): one dab, or a stroke from where it was last used.
    private void ApplyAtTyped(bool stroke)
    {
        if (island is null || history is null) return;
        if (!int.TryParse(vertexXBox.Text.Trim(), out var x) || !int.TryParse(vertexZBox.Text.Trim(), out var z)) { SetStatus("Type the vertex's X and Z (whole numbers)."); return; }
        if (!island.HasVertex(x, z)) { SetStatus($"Vertex {x}, {z} isn't on the island."); return; }
        ApplyAt(x, z, stroke);
    }

    // One dab of the tool at a vertex, or (`stroke`) a straight stroke to it from where the tool was last used: as the mouse would do it.
    private void ApplyAt(double x, double z, bool stroke)
    {
        if (island is null || history is null) return;
        (double Gx, double Gz) from = stroke && lastUsed is { } last ? last : (x, z);
        pointer = (from.Gx, from.Gz);
        PointerDownCore();
        if (stroking)
        {
            // (the stroke a dab every half vertex along the way, as a drag's moves are)
            var steps = (int)Math.Ceiling(Math.Max(Math.Abs(x - from.Gx), Math.Abs(z - from.Gz)) * 2);
            for (var i = 1; i <= steps; i++)
            {
                pointer = (from.Gx + (x - from.Gx) * i / steps, from.Gz + (z - from.Gz) * i / steps);
                StrokeTick();
            }
        }
        PointerUpCore();
        lastUsed = (x, z);
        PlaceBrushOutline();
        UpdateInfo();
    }

    // The brush's outline where the pointer is (the mouse's moves, the typed place, the arrow keys).
    private void PlaceBrushOutline()
    {
        if (renderer is null) return;
        var r = radius.Value * renderer.Scale;
        brushCircle.Visibility = !BrushTool ? Visibility.Collapsed : Visibility.Visible;
        if (squareBox.IsChecked == true)
        {
            var (cx, cz, sr) = SquareBrush();
            var half = (sr + 0.5) * renderer.Scale;
            brushCircle.RadiusX = brushCircle.RadiusY = 0;
            brushCircle.Width = brushCircle.Height = half * 2;
            Canvas.SetLeft(brushCircle, CellToPixelX(cx) - half); Canvas.SetTop(brushCircle, CellToPixelZ(cz) - half);
        }
        else
        {
            brushCircle.RadiusX = brushCircle.RadiusY = r;
            brushCircle.Width = brushCircle.Height = r * 2;
            Canvas.SetLeft(brushCircle, CellToPixelX(pointer.Gx) - r); Canvas.SetTop(brushCircle, CellToPixelZ(pointer.Gz) - r);
        }
    }

    // Keys while the host shows this editor (false = not one of ours).
    public bool HandleKey(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox) return false;
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        // (the brush by the keyboard: the arrows a vertex, Shift eight; Space a dab there)
        if (!ctrl && BrushTool && island is not null && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 8 : 1;
            var (gx, gz) = ((int)Math.Round(pointer.Gx), (int)Math.Round(pointer.Gz));
            if (e.Key == Key.Left) gx -= step; else if (e.Key == Key.Right) gx += step; else if (e.Key == Key.Up) gz -= step; else gz += step;
            pointer = (gx, gz);
            vertexXBox.Text = gx.ToString(); vertexZBox.Text = gz.ToString();
            PlaceBrushOutline(); UpdateInfo();
            return true;
        }
        if (!ctrl && e.Key == Key.Space && island is not null) { ApplyAt(Math.Round(pointer.Gx), Math.Round(pointer.Gz), stroke: false); return true; }
        if (ctrl && e.Key == Key.Z) DoUndo();
        else if (ctrl && e.Key == Key.Y) DoRedo();
        else if (ctrl && e.Key == Key.S) Save();
        else if (e.Key == Key.Delete && selected is not null) DeleteSelected();
        else if (e.Key is Key.Q or Key.E && !ctrl && selected is not null) TurnSelected(e.Key == Key.E ? 1 : -1, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 90 : 15);
        else if (e.Key == Key.OemOpenBrackets) radius.Value = Math.Max(1, radius.Value - 1);
        else if (e.Key == Key.OemCloseBrackets) radius.Value = Math.Min(60, radius.Value + 1);
        else return false;
        return true;
    }

    public void Undo() => DoUndo();
    public void Redo() => DoRedo();
    public void Fit() => FitView();
    public void ZoomBy(double factor) { if (viewport.ActualWidth > 0) ZoomAt(new Point(viewport.ActualWidth / 2, viewport.ActualHeight / 2), factor); }
    public double ZoomPercent => zoom * 100;

    private void UpdateInfo()
    {
        if (island is null) { info.Text = ""; return; }
        var gx = (int)Math.Round(pointer.Gx); var gz = (int)Math.Round(pointer.Gz);
        var cx = Math.Clamp((int)Math.Floor(pointer.Gx) / 64, 0, 15); var cz = Math.Clamp((int)Math.Floor(pointer.Gz) / 64, 0, 15);
        var cube = island.CubeAt(cx, cz);
        if (cube is null || island.HeightAt(gx, gz) is not { } h) { info.Text = $"vertex {gx}, {gz}\noff the island"; return; }
        var ground = IslandOps.Altitude(island, pointer.Gx * 512, pointer.Gz * 512);
        var cell = IslandGround.Pick(island, Math.Min((int)Math.Floor(pointer.Gx), IslandFile.GridSize - 1), Math.Min((int)Math.Floor(pointer.Gz), IslandFile.GridSize - 1), 0);
        var light = island.LightAt(gx, gz);
        info.Text = $"vertex {gx}, {gz}   cube {cube.Id} ({cx},{cz})\nworld X {gx * 512}  Z {gz * 512}\nheight {h}  (ground {ground:F0})\nlight {light}  water depth {IslandLightOps.WaterDepthAt(island, gx, gz)}\n"
            + (cell is null ? "" : $"code {cell.Polygon.CodeJeu} {IslandPolygon.CodeJeuNames[cell.Polygon.CodeJeu]}  tex {cell.Polygon.TextureIndex}  diag {(cell.Polygon.Diagonal ? "1-3" : "0-2")}  {(cell.Polygon.Col ? "BLOCKED (unwalkable)" : "walkable")}");
    }
}
