using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LBAAssembler.Maps;

namespace LBAAssembler;

// File > New map: what kind of map -- an LBA2 island, an LBA2 interior or an LBA1 interior -- its name, and the game's own map it takes its
// look from (MapNew). Built in code, as ListPickWindow is.
internal sealed class NewMapWindow : Window
{
    public MapKind Kind { get; private set; }
    public string MapName { get; private set; } = "";
    public string IslandFileName { get; private set; } = "";
    public string BaseIsland { get; private set; } = "";
    public int CubesX { get; private set; } = 2;
    public int CubesZ { get; private set; } = 2;
    public int LandHeight { get; private set; } = 1200;
    public int BaseScene { get; private set; }
    // an island's ground from a heightmap picture instead of flat land (null: flat), with its black / white heights and sea level
    public Terrain.HeightmapPicture? Heightmap { get; private set; }
    public Terrain.HeightmapImportOptions? HeightmapOptions { get; private set; }

    public NewMapWindow(bool lba2, bool lba1, IReadOnlyList<string> islands, IReadOnlyList<(int Id, string Label)> lba2Interiors, IReadOnlyList<(int Id, string Label)> lba1Scenes)
    {
        Title = "New map";
        Width = 520; SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        this.SetResourceReference(BackgroundProperty, "ThemeWindowBrush");
        this.SetResourceReference(ForegroundProperty, "ThemeTextBrush");

        var root = new StackPanel { Margin = new Thickness(16) };
        TextBlock Label(string text, double top = 10) { var t = new TextBlock { Text = text, Margin = new Thickness(0, top, 0, 4) }; return t; }
        TextBlock Note(string text)
        {
            var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            t.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
            return t;
        }
        TextBox Box(string text, double width = 0) { var b = new TextBox { Text = text, Padding = new Thickness(4, 3, 4, 3) }; if (width > 0) { b.Width = width; b.HorizontalAlignment = HorizontalAlignment.Left; } return b; }
        ComboBox Combo<T>(IEnumerable<T> items) { var c = new ComboBox { Padding = new Thickness(4, 3, 4, 3) }; foreach (var i in items) c.Items.Add(i!); if (c.Items.Count > 0) c.SelectedIndex = 0; return c; }

        // the kind
        root.Children.Add(Label("What kind of map", 0));
        var island = new RadioButton { Content = "An LBA2 island (an ILE: ground, sea and objects, an outside scene for each cube)", IsEnabled = lba2, Margin = new Thickness(0, 2, 0, 2) };
        var interior = new RadioButton { Content = "An LBA2 interior (a scene and its grid of blocks)", IsEnabled = lba2 && lba2Interiors.Count > 0, Margin = new Thickness(0, 2, 0, 2) };
        var lba1Room = new RadioButton { Content = "An LBA1 interior (a scene, its grid and its block library)", IsEnabled = lba1 && lba1Scenes.Count > 0, Margin = new Thickness(0, 2, 0, 2) };
        root.Children.Add(island); root.Children.Add(interior); root.Children.Add(lba1Room);
        if (!lba2) root.Children.Add(Note("LBA2's maps need the LBA2 game folder (File > Settings)."));
        if (!lba1) root.Children.Add(Note("LBA1's maps need the LBA1 game folder (File > Settings)."));

        root.Children.Add(Label("Name"));
        var name = Box("New map");
        root.Children.Add(name);

        // an island's settings
        var islandPanel = new StackPanel();
        islandPanel.Children.Add(Label("Its file name in the game folder (up to eight letters or digits)"));
        var file = Box("NEWISLE", 140);
        islandPanel.Children.Add(file);
        islandPanel.Children.Add(Label("Its look (ground and sea textures, light, sky, music): the game's island"));
        var baseIsland = Combo(islands);
        if (islands.Contains("DESERT")) baseIsland.SelectedItem = "DESERT";
        islandPanel.Children.Add(baseIsland);
        islandPanel.Children.Add(Label("Its size, in cubes (64 x 64 cells each), and its land's height"));
        var size = new StackPanel { Orientation = Orientation.Horizontal };
        var cubesX = Box("2", 48); var cubesZ = Box("2", 48); var height = Box("1200", 70);
        size.Children.Add(cubesX); size.Children.Add(new TextBlock { Text = "  by  ", VerticalAlignment = VerticalAlignment.Center }); size.Children.Add(cubesZ);
        size.Children.Add(new TextBlock { Text = "  cubes,  land", VerticalAlignment = VerticalAlignment.Center }); size.Children.Add(new TextBlock { Width = 8 }); size.Children.Add(height);
        size.Children.Add(new TextBlock { Text = "  high", VerticalAlignment = VerticalAlignment.Center });
        islandPanel.Children.Add(size);
        // its ground: flat land, or a heightmap picture (its size says how many cubes)
        islandPanel.Children.Add(Label("Its ground"));
        var flatGround = new RadioButton { Content = new TextBlock { Text = "Flat land with a beach all round into the sea, Twinsen standing in the first cube (shape it in Build mode)", TextWrapping = TextWrapping.Wrap }, GroupName = "ground", IsChecked = true, Margin = new Thickness(0, 2, 0, 2) };
        var pictureGround = new RadioButton { Content = new TextBlock { Text = "From a heightmap picture: black the lowest ground, white the highest", TextWrapping = TextWrapping.Wrap }, GroupName = "ground", Margin = new Thickness(0, 2, 0, 2) };
        AutomationProperties.SetName(flatGround, "Flat land with a beach"); AutomationProperties.SetName(pictureGround, "From a heightmap picture");
        islandPanel.Children.Add(flatGround); islandPanel.Children.Add(pictureGround);
        var picturePanel = new StackPanel { Margin = new Thickness(18, 2, 0, 0) };
        var picturePath = new TextBox { IsReadOnly = true, Padding = new Thickness(4, 3, 4, 3) };
        var choosePicture = new Button { Content = "Choose…", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(6, 0, 0, 0) };
        var pictureRow = new DockPanel();
        DockPanel.SetDock(choosePicture, Dock.Right);
        pictureRow.Children.Add(choosePicture); pictureRow.Children.Add(picturePath);
        picturePanel.Children.Add(pictureRow);
        var pictureNote = Note("One pixel per height point: 64 pixels a cube plus one, so 129 × 129 for 2 × 2 cubes, 257 × 257 for 4 × 4. The picture's size sets the cubes; another size is stretched to them.");
        picturePanel.Children.Add(pictureNote);
        var levels = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var black = Box("-400", 64); var white = Box("3600", 64); var sea = Box("0", 56);
        // (each box named for screen readers and UI Automation -- its label is a separate text beside it, 2026-10-23)
        foreach (var (control, label) in new (Control, string)[]
                 {
                     (name, "Name"), (file, "File name"), (baseIsland, "Look: the game's island"), (cubesX, "Cubes across (X)"), (cubesZ, "Cubes down (Z)"),
                     (height, "Land height"), (picturePath, "Heightmap picture"), (black, "Height of black"), (white, "Height of white"), (sea, "Sea level"),
                 })
            AutomationProperties.SetName(control, label);
        levels.Children.Add(new TextBlock { Text = "Black is  ", VerticalAlignment = VerticalAlignment.Center }); levels.Children.Add(black);
        levels.Children.Add(new TextBlock { Text = "   white is  ", VerticalAlignment = VerticalAlignment.Center }); levels.Children.Add(white);
        levels.Children.Add(new TextBlock { Text = "   sea level  ", VerticalAlignment = VerticalAlignment.Center }); levels.Children.Add(sea);
        picturePanel.Children.Add(levels);
        picturePanel.Children.Add(Note("World units (Twinsen is about 1,000 tall). Ground at or under the sea level is sea."));
        islandPanel.Children.Add(picturePanel);
        void ShowGround() { picturePanel.IsEnabled = pictureGround.IsChecked == true; height.IsEnabled = pictureGround.IsChecked != true; }
        flatGround.Checked += (_, _) => ShowGround(); pictureGround.Checked += (_, _) => ShowGround();
        ShowGround();
        choosePicture.Click += (_, _) =>
        {
            var open = new OpenFileDialog { Filter = HeightmapWindow.Filter, Title = "Choose a heightmap" };
            if (open.ShowDialog(this) != true) return;
            try
            {
                var loaded = HeightmapWindow.LoadPicture(open.FileName, null);
                Heightmap = loaded;
                picturePath.Text = open.FileName;
                int x = Math.Clamp(Terrain.HeightmapLayout.CubesFor(loaded.Width), 1, 6), z = Math.Clamp(Terrain.HeightmapLayout.CubesFor(loaded.Height), 1, 6);
                cubesX.Text = x.ToString(); cubesZ.Text = z.ToString();
                var (w, h) = Terrain.HeightmapLayout.SizeFor(x, z);
                if (loaded.Range is { } r) { black.Text = r.Black.ToString(); white.Text = r.White.ToString(); }
                black.IsEnabled = white.IsEnabled = !loaded.Absolute;
                pictureNote.Text = $"{System.IO.Path.GetFileName(open.FileName)}: {loaded.Width} × {loaded.Height}, {loaded.Kind}: {x} × {z} cubes ({w} × {h} pixels"
                                   + (loaded.Width == w && loaded.Height == h ? ", its own size)." : ", so it is resampled to that).")
                                   + (loaded.Range is not null ? " Its black and white heights are the ones it was exported with." : "");
            }
            catch (Exception e) when (e is System.IO.IOException or System.IO.InvalidDataException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                Heightmap = null; picturePath.Text = "";
                pictureNote.Text = $"Couldn't read {System.IO.Path.GetFileName(open.FileName)}: {e.Message}";
            }
        };

        // an interior's
        var interiorPanel = new StackPanel();
        interiorPanel.Children.Add(Label("Built from the game's interior (its block library, light and music)"));
        var baseInterior = Combo(lba2Interiors.Select(s => s.Label));
        interiorPanel.Children.Add(baseInterior);
        interiorPanel.Children.Add(Note("A floor of 32 x 32 cells of that interior's own floor, Twinsen standing on it. Paint it in the grid editor."));

        var lba1Panel = new StackPanel();
        lba1Panel.Children.Add(Label("The scene it takes the place of in the LBA1 game (its island, music and block library)"));
        var baseLba1 = Combo(lba1Scenes.Select(s => s.Label));
        lba1Panel.Children.Add(baseLba1);
        lba1Panel.Children.Add(Note("LBA1 can't have scenes added: installed into the game, the map replaces that scene."));

        root.Children.Add(islandPanel); root.Children.Add(interiorPanel); root.Children.Add(lba1Panel);
        void Show()
        {
            islandPanel.Visibility = island.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            interiorPanel.Visibility = interior.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            lba1Panel.Visibility = lba1Room.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
        foreach (var r in new[] { island, interior, lba1Room }) r.Checked += (_, _) => Show();
        (island.IsEnabled ? island : interior.IsEnabled ? interior : lba1Room).IsChecked = true;
        Show();

        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
        error.SetResourceReference(TextBlock.ForegroundProperty, "ThemeWarningBrush");
        root.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var ok = new Button { Content = "Create", Padding = new Thickness(18, 4, 18, 4), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 4, 14, 4), IsCancel = true };
        cancel.Click += (_, _) => this.DialogResult = false;     // (WPF closed a dialog by its IsCancel button by itself; Avalonia only clicks it)
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        Content = root;

        // (an empty Avalonia text box has a null Text, where WPF's was "")
        name.TextChanged += (_, _) => { if (island.IsChecked == true && !fileEdited) { settingFile = true; file.Text = LbaMap.FileNameFrom(name.Text ?? "", 8); settingFile = false; } };
        file.TextChanged += (_, _) => { if (!settingFile) fileEdited = true; };
        ok.Click += (_, _) =>
        {
            string? problem = null;
            MapName = (name.Text ?? "").Trim().Length > 0 ? (name.Text ?? "").Trim() : "New map";
            if (island.IsChecked == true)
            {
                Kind = MapKind.Lba2Island;
                IslandFileName = (file.Text ?? "").Trim().ToUpperInvariant();
                BaseIsland = baseIsland.SelectedItem as string ?? "";
                if (!LbaMap.ValidIslandFile(IslandFileName)) problem = "The file name is one to eight letters, digits or underscores.";
                else if (islands.Contains(IslandFileName, StringComparer.OrdinalIgnoreCase)) problem = $"The game folder already has {IslandFileName}.ILE: choose another name.";
                else if (!int.TryParse(cubesX.Text, out var x) || !int.TryParse(cubesZ.Text, out var z) || x is < 1 or > 6 || z is < 1 or > 6) problem = "An island is 1 to 6 cubes each way.";
                else if (pictureGround.IsChecked != true && (!int.TryParse(height.Text, out var h) || h is < 300 or > 20000)) problem = "The land's height is 300 to 20,000.";
                else { CubesX = x; CubesZ = z; LandHeight = int.TryParse(height.Text, out var land) ? land : 1200; }
                if (BaseIsland.Length == 0) problem ??= "Choose the island it takes its look from.";
                if (pictureGround.IsChecked != true) { Heightmap = null; HeightmapOptions = null; }
                else if (Heightmap is null) problem ??= "Choose the heightmap picture (or flat land).";
                else if (!double.TryParse(black.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b)
                         || !double.TryParse(white.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w)
                         || !int.TryParse(sea.Text, out var s) || (!Heightmap.Absolute && b == w))
                    problem ??= "Black and white are two different heights, the sea level a whole number (world units).";
                else HeightmapOptions = new Terrain.HeightmapImportOptions { Black = b, White = w, SeaLevel = s };
            }
            else if (interior.IsChecked == true) { Kind = MapKind.Lba2Interior; BaseScene = lba2Interiors[Math.Max(0, baseInterior.SelectedIndex)].Id; }
            else { Kind = MapKind.Lba1Interior; BaseScene = lba1Scenes[Math.Max(0, baseLba1.SelectedIndex)].Id; }
            if (problem is not null) { error.Text = problem; error.Visibility = Visibility.Visible; return; }
            this.DialogResult = true;
        };
        Loaded += (_, _) => { name.Focus(); name.SelectAll(); };
    }

    private bool fileEdited, settingFile;
}
