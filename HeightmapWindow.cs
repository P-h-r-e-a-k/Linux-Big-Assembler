using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LBAAssembler.Terrain;
using Microsoft.Win32;

namespace LBAAssembler;

// Import heightmap (the tester SIN, 2026-10-17: "maybe have the ability to import a heightmap for custom 3d environments? have it tell you what
// pixel x pixel it needs because it can be tedious to sculpt manually"): says what size of picture the island needs and which pixel is where,
// reads the picture, shows where it lands and the ground it makes, and -- with "Show it in the 3D view" -- has the terrain editor apply it to the
// island live (Preview), which Cancel takes back. Import hands the picture and the options back for the editor to apply as one undo step.
// Built in code, as NewMapWindow is.
internal sealed class HeightmapWindow : Window
{
    public sealed record Defaults(BakeOptions Bake, bool BakeLight, bool Steep, double SteepAngle, bool Follow);

    private readonly IslandFile island;
    private readonly HeightmapLayout layout;
    private readonly Action<HeightmapPicture?, HeightmapImportOptions?>? preview;
    private readonly (IslandGround.Sample? Sea, IslandGround.Sample? Flat, IslandGround.Sample? Steep) ground;
    private readonly BakeOptions bake;
    private readonly (int Lo, int Hi) heights;
    private readonly bool hasObjects;
    private HeightmapPicture? picture;
    private string? picturePath;
    private bool loading;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(250) };

    // what Import hands back
    public HeightmapPicture? Picture => picture;
    public HeightmapImportOptions Options => Read(forPreview: false);
    public string? PicturePath => picturePath;

    private readonly TextBox pathBox = new() { IsReadOnly = true, Padding = new Thickness(4, 3, 4, 3) };
    private readonly TextBlock pictureInfo = Note("");
    private readonly TextBlock sizeVerdict = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 2) };
    private readonly ComboBox fitBox = new() { Padding = new Thickness(4, 3, 4, 3) };
    private readonly TextBox blackBox = Field("0"), whiteBox = Field("4000"), seaBox = Field("0"), angleBox = Field("45");
    private readonly TextBlock rangeNote = Note("");
    private readonly ComboBox smoothBox = new() { Padding = new Thickness(4, 3, 4, 3) };
    private readonly ComboBox texturesBox = new() { Padding = new Thickness(4, 3, 4, 3) };
    private readonly CheckBox diagonalBox = Check("Re-cut the cells along their flatter diagonal", true);
    private readonly CheckBox steepBox = Check("Block ground steeper than", false);
    private readonly CheckBox bakeBox = Check("", true);
    private readonly CheckBox shadowBox = Check("Shadows under the objects, as the retail islands have under buildings", false);
    private readonly CheckBox followBox = Check("Objects standing on the ground go up and down with it", true);
    private readonly CheckBox liveBox = Check("Show it in the 3D view now (Cancel puts the ground back)", true);
    private readonly Image pictureImage = new() { Stretch = Stretch.Uniform, MaxWidth = 330, MaxHeight = 250, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Image groundImage = new() { Stretch = Stretch.Uniform, MaxWidth = 330, MaxHeight = 250, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock stats = Note("");
    private readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Button importButton = new() { Content = "Import", Padding = new Thickness(18, 4, 18, 4), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };

    public HeightmapWindow(IslandFile island, string islandName, string path, Defaults defaults, Action<HeightmapPicture?, HeightmapImportOptions?>? preview)
    {
        this.island = island;
        this.preview = preview;
        layout = HeightmapLayout.Of(island);
        // (taken before any preview changes the island)
        ground = IslandGround.CommonGround(island);
        var (lo, hi) = IslandOps.HeightRange(island);
        heights = (lo, hi);
        hasObjects = island.Cubes.Values.Any(c => c.Decors.Count > 0);
        bake = defaults.Bake;

        Title = "Import heightmap";
        Width = 900; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "ThemeWindowBrush");
        SetResourceReference(ForegroundProperty, "ThemeTextBrush");
        error.SetResourceReference(TextBlock.ForegroundProperty, "ThemeWarningBrush");

        var root = new StackPanel { Margin = new Thickness(16) };
        var statement = new TextBlock { Text = IslandHeightmap.SizeText(island, islandName), TextWrapping = TextWrapping.Wrap, FontSize = 13.5, FontWeight = FontWeights.SemiBold };
        root.Children.Add(statement);
        root.Children.Add(Note(IslandHeightmap.LayoutText(island) + " A picture of 64 pixels a cube (one a cell) works as well: it is stretched by one pixel."));

        var columns = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        var left = new StackPanel();
        var right = new StackPanel();
        Grid.SetColumn(right, 2);
        columns.Children.Add(left); columns.Children.Add(right);
        root.Children.Add(columns);

        // the picture
        left.Children.Add(Label("Picture"));
        var choose = new Button { Content = "Choose…", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(6, 0, 0, 0) };
        choose.Click += (_, _) => ChoosePicture();
        var pathRow = new DockPanel();
        DockPanel.SetDock(choose, Dock.Right);
        pathRow.Children.Add(choose); pathRow.Children.Add(pathBox);
        left.Children.Add(pathRow);
        left.Children.Add(pictureInfo);
        left.Children.Add(sizeVerdict);
        fitBox.Items.Add($"Resample it to {layout.Width} × {layout.Height} (stretched or shrunk corner to corner)");
        fitBox.Items.Add("Crop it: its pixels one for one from the top-left corner (ground beyond it is left as it is)");
        fitBox.Items.Add("Refuse it: only a picture of the island's exact size is imported");
        fitBox.SelectedIndex = 0;
        left.Children.Add(fitBox);

        // the heights
        left.Children.Add(Label("Heights (world units; Twinsen is about 1,000 tall, a cell 512 across)"));
        var levels = new WrapPanel();
        levels.Children.Add(Inline("Black is")); levels.Children.Add(blackBox);
        levels.Children.Add(Inline("   white is")); levels.Children.Add(whiteBox);
        levels.Children.Add(Inline("   sea level")); levels.Children.Add(seaBox);
        left.Children.Add(levels);
        left.Children.Add(rangeNote);
        var smoothRow = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        smoothRow.Children.Add(Inline("Smooth it"));
        foreach (var s in new[] { "not at all", "once (takes the steps out of an 8-bit picture)", "twice", "three times" }) smoothBox.Items.Add(s);
        smoothBox.SelectedIndex = 0; smoothBox.MinWidth = 300;
        smoothRow.Children.Add(smoothBox);
        left.Children.Add(smoothRow);

        // what follows from the heights
        left.Children.Add(Label("Then"));
        texturesBox.Items.Add("Keep the ground's pictures and game codes as they are");
        texturesBox.Items.Add("Sea where the ground sinks to the sea level, land where it rises out of it");
        texturesBox.Items.Add("Every cell again by its height and slope: sea, flat ground, steep ground");
        texturesBox.SelectedIndex = 1;
        texturesBox.ToolTip = "With the island's own most common sea, flat and steep ground" + (ground.Sea is null ? " (it has no sea cells to copy: the sea is left as it is)" : "");
        left.Children.Add(texturesBox);
        left.Children.Add(diagonalBox);
        var steepRow = new WrapPanel();
        steepBox.IsChecked = defaults.Steep; angleBox.Text = defaults.SteepAngle.ToString(CultureInfo.InvariantCulture);
        steepRow.Children.Add(steepBox); steepRow.Children.Add(angleBox); steepRow.Children.Add(Inline("° (Twinsen can't step onto it)"));
        steepRow.ToolTip = "Sets the collision bit of every changed ground triangle steeper than this, and clears it on the others (as the terrain editor's own option does)";
        left.Children.Add(steepRow);
        bakeBox.Content = new TextBlock { Text = $"Bake the light again: sun from {bake.Azimuth:F0}°, {bake.Elevation:F0}° up{(bake.TerrainShadows ? ", the terrain casting shadows" : "")} (the Baked light settings)", TextWrapping = TextWrapping.Wrap };
        bakeBox.IsChecked = defaults.BakeLight;
        left.Children.Add(bakeBox);
        shadowBox.IsChecked = hasObjects; shadowBox.IsEnabled = hasObjects;
        left.Children.Add(shadowBox);
        followBox.IsChecked = defaults.Follow && hasObjects; followBox.IsEnabled = hasObjects;
        if (!hasObjects) shadowBox.ToolTip = followBox.ToolTip = "The island has no objects";
        left.Children.Add(followBox);
        left.Children.Add(Note("Only ground whose height changes is touched: the rest keeps its light, diagonals and pictures."));
        liveBox.IsEnabled = preview is not null; liveBox.IsChecked = preview is not null;
        liveBox.Margin = new Thickness(0, 10, 0, 0);
        left.Children.Add(liveBox);

        // the previews
        right.Children.Add(Label("The picture on the island", 0));
        right.Children.Add(Framed(pictureImage));
        right.Children.Add(Label("The ground it makes"));
        right.Children.Add(Framed(groundImage));
        right.Children.Add(stats);

        root.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 4, 14, 4), IsCancel = true };
        buttons.Children.Add(importButton); buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        MaxHeight = SystemParameters.WorkArea.Height - 40;

        importButton.Click += (_, _) =>
        {
            if (Problem() is { } problem) { ShowError(problem); return; }
            refresh.Stop();
            DialogResult = true;
        };
        refresh.Tick += (_, _) => { refresh.Stop(); Refresh(); };
        foreach (var box in new[] { blackBox, whiteBox, seaBox, angleBox }) box.TextChanged += (_, _) => Changed();
        foreach (var combo in new[] { fitBox, smoothBox, texturesBox }) combo.SelectionChanged += (_, _) => Changed();
        foreach (var check in new[] { diagonalBox, steepBox, bakeBox, shadowBox, followBox })
        { check.Checked += (_, _) => Changed(); check.Unchecked += (_, _) => Changed(); }
        liveBox.Checked += (_, _) => Changed();
        liveBox.Unchecked += (_, _) => { this.preview?.Invoke(null, null); };
        Closed += (_, _) => refresh.Stop();

        Load(path);
    }

    // ---- the picture ------------------------------------------------------------------------------------------------------------------------

    public const string Filter = HeightmapFile.ReadFilter + "|Other pictures (*.bmp;*.jpg;*.jpeg;*.tif;*.tiff;*.gif)|*.bmp;*.jpg;*.jpeg;*.tif;*.tiff;*.gif|All files (*.*)|*.*";

    private void ChoosePicture()
    {
        var dialog = new OpenFileDialog { Filter = Filter, Title = "Choose a heightmap", InitialDirectory = picturePath is { } p ? Path.GetDirectoryName(p) : null };
        if (dialog.ShowDialog(this) == true) Load(dialog.FileName);
    }

    private void Load(string path)
    {
        try
        {
            picture = LoadPicture(path, (layout.Width, layout.Height));
            picturePath = path;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException)
        {
            picture = null; picturePath = null;
            pathBox.Text = "";
            pictureInfo.Text = "";
            ShowError($"Couldn't read {Path.GetFileName(path)}: {e.Message}");
            Changed();
            return;
        }
        loading = true;
        pathBox.Text = path;
        var exact = picture.Width == layout.Width && picture.Height == layout.Height;
        pictureInfo.Text = $"{Path.GetFileName(path)}: {picture.Width} × {picture.Height}, {picture.Kind}." + (picture.HasTransparency ? " Its transparent pixels leave the ground as it is." : "");
        sizeVerdict.Text = exact ? $"The island's own size: one pixel per height point." : $"Not the island's size ({layout.Width} × {layout.Height}). Use it:";
        sizeVerdict.SetResourceReference(TextBlock.ForegroundProperty, exact ? "ThemeSuccessBrush" : "ThemeWarningBrush");
        fitBox.Visibility = exact ? Visibility.Collapsed : Visibility.Visible;
        // black and white: as the picture says it was exported, else the island's own lowest and highest ground (a flat island: 0 .. 4000)
        var (black, white) = picture.Range ?? IslandHeightmap.DefaultRange(island);
        blackBox.Text = black.ToString(CultureInfo.InvariantCulture); whiteBox.Text = white.ToString(CultureInfo.InvariantCulture);
        blackBox.IsEnabled = whiteBox.IsEnabled = !picture.Absolute;
        rangeNote.Text = picture.Absolute ? "The file holds world heights: black and white don't apply."
            : $"The island's ground goes from {heights.Lo:N0} to {heights.Hi:N0} now." + (picture.Range is { } r ? $" The picture says it was exported with black {r.Black:N0} and white {r.White:N0}." : "")
              + " To make the ground higher than the white level, raise White." + (picture.Bits == 8 ? $" An 8-bit picture has 256 levels: steps of {Math.Abs(white - black) / 255.0:F0} units." : "");
        smoothBox.SelectedIndex = picture.Bits == 8 ? 1 : 0;
        loading = false;
        Changed();
    }

    // A heightmap file, or any picture WPF reads (BMP, JPEG, TIFF -- 16-bit ones too -- GIF), as grey levels.
    public static HeightmapPicture LoadPicture(string path, (int Width, int Height)? expected)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".png" or ".r16" or ".raw" or ".csv" or ".txt") return HeightmapFile.Load(path, expected);
        BitmapFrame frame;
        using (var stream = File.OpenRead(path))
            frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        if (frame.PixelWidth > HeightmapFile.MaxSide || frame.PixelHeight > HeightmapFile.MaxSide)
            throw new InvalidDataException($"The picture is {frame.PixelWidth} × {frame.PixelHeight}: heightmaps up to {HeightmapFile.MaxSide} pixels a side are read.");
        var bits = frame.Format.BitsPerPixel / Math.Max(1, frame.Format.Masks.Count) >= 16 ? 16 : 8;
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Rgba64, null, 0);
        int w = converted.PixelWidth, h = converted.PixelHeight;
        var pixels = new ushort[w * h * 4];
        converted.CopyPixels(pixels, w * 8, 0);
        var values = new double[w * h];
        for (var i = 0; i < w * h; i++)
        {
            int r = pixels[i * 4], g = pixels[i * 4 + 1], b = pixels[i * 4 + 2], a = pixels[i * 4 + 3];
            values[i] = a < 32768 ? double.NaN : r == g && g == b ? r / 65535.0 : (0.299 * r + 0.587 * g + 0.114 * b) / 65535.0;
        }
        return new HeightmapPicture { Width = w, Height = h, Values = values, Bits = bits, Kind = $"{extension.TrimStart('.').ToUpperInvariant()} picture, {bits}-bit (read as grey)" };
    }

    // ---- the options ----------------------------------------------------------------------------------------------------------------------

    private static double Number(TextBox box, double fallback) => double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    // `forPreview`: the light baked without cast shadows (rays from every point are slow; the import itself casts them)
    private HeightmapImportOptions Read(bool forPreview)
    {
        BakeOptions? light = null;
        if (bakeBox.IsChecked == true)
        {
            light = new BakeOptions
            {
                Azimuth = bake.Azimuth, Elevation = bake.Elevation, Gain = bake.Gain, Offset = bake.Offset, ShadowLevel = bake.ShadowLevel,
                TerrainShadows = bake.TerrainShadows && !forPreview, DecorShadows = bake.DecorShadows && !forPreview,
            };
        }
        return new HeightmapImportOptions
        {
            Black = Number(blackBox, 0), White = Number(whiteBox, IslandHeightmap.DefaultWhite), SeaLevel = (int)Math.Round(Number(seaBox, 0)),
            Fit = picture is not null && picture.Width == layout.Width && picture.Height == layout.Height ? HeightmapFit.Exact
                : fitBox.SelectedIndex switch { 1 => HeightmapFit.Crop, 2 => HeightmapFit.Exact, _ => HeightmapFit.Resample },
            SmoothPasses = Math.Max(0, smoothBox.SelectedIndex),
            Textures = texturesBox.SelectedIndex switch { 0 => HeightmapTextures.Keep, 2 => HeightmapTextures.AllBySlope, _ => HeightmapTextures.SeaAndShore },
            Ground = ground,
            FixDiagonals = diagonalBox.IsChecked == true,
            SteepAngle = steepBox.IsChecked == true ? Math.Clamp(Number(angleBox, 45), 1, 89) : null,
            Bake = light,
            ObjectShadowDepth = light is not null && shadowBox.IsChecked == true && hasObjects ? 5 : null,
            ObjectsFollow = followBox.IsChecked == true && hasObjects,
        };
    }

    // What stops the import, or null.
    private string? Problem()
    {
        if (picture is null) return "Choose a picture first.";
        if (!double.TryParse(blackBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var black)
            || !double.TryParse(whiteBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var white)) return "Black and white are heights in world units (numbers).";
        if (!picture.Absolute && black == white) return "Black and white can't be the same height.";
        if (!int.TryParse(seaBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return "The sea level is a height in world units (a whole number).";
        if (steepBox.IsChecked == true && (!double.TryParse(angleBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var a) || a is <= 0 or >= 90)) return "The steepest walkable angle is between 0 and 90 degrees.";
        if ((picture.Width != layout.Width || picture.Height != layout.Height) && fitBox.SelectedIndex == 2)
            return $"The picture is {picture.Width} × {picture.Height}; the island needs {layout.Width} × {layout.Height} (choose Resample or Crop to use it anyway).";
        return null;
    }

    private void ShowError(string? text)
    {
        error.Text = text ?? "";
        error.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Changed()
    {
        if (loading) return;
        refresh.Stop();
        refresh.Start();
    }

    // The previews, and the 3D view's.
    private void Refresh()
    {
        var problem = Problem();
        ShowError(picture is null ? null : problem);
        importButton.IsEnabled = problem is null;
        if (problem is not null || picture is null)
        {
            preview?.Invoke(null, null);
            return;
        }
        var options = Read(forPreview: true);
        try
        {
            var targets = IslandHeightmap.Targets(island, picture, options, out var clamped);
            pictureImage.Source = Bitmap(IslandHeightmap.PicturePreview(island, picture, options));
            var shaded = IslandHeightmap.ShadedPreview(island, targets, options.SeaLevel, out var s, options.SteepAngle ?? 45);
            groundImage.Source = Bitmap(shaded);
            stats.Text = $"Ground from {s.Lo:N0} to {s.Hi:N0}; {s.SeaPercent}% at or under the sea level; {s.SteepPercent}% steeper than {options.SteepAngle ?? 45:F0}°."
                         + (clamped > 0 ? $" {clamped:N0} heights are past the game's limits (-32,768 to 32,767) and are cut to them." : "");
        }
        catch (InvalidDataException e) { ShowError(e.Message); importButton.IsEnabled = false; return; }
        if (liveBox.IsChecked == true) preview?.Invoke(picture, options);
    }

    private BitmapSource Bitmap(byte[] bgra)
    {
        var bitmap = BitmapSource.Create(layout.Width, layout.Height, 96, 96, PixelFormats.Bgra32, null, bgra, layout.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    // ---- small parts ----------------------------------------------------------------------------------------------------------------------

    private static TextBlock Label(string text, double top = 12) => new() { Text = text, Margin = new Thickness(0, top, 0, 4), FontWeight = FontWeights.SemiBold };

    private static TextBlock Note(string text)
    {
        var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        t.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
        return t;
    }

    private static TextBlock Inline(string text) => new() { Text = text + " ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 2, 2) };

    private static TextBox Field(string text)
    {
        var box = new TextBox { Text = text, Width = 64, Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(0, 2, 4, 2), VerticalAlignment = VerticalAlignment.Center };
        box.SetResourceReference(Control.BackgroundProperty, "ThemeFieldBrush");
        box.SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush");
        box.SetResourceReference(Control.BorderBrushProperty, "ThemeBorderBrush");
        return box;
    }

    private static CheckBox Check(string text, bool on)
    {
        var box = new CheckBox { Content = text, IsChecked = on, Margin = new Thickness(0, 4, 4, 2), VerticalAlignment = VerticalAlignment.Center };
        box.SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush");
        return box;
    }

    private static Border Framed(Image image)
    {
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        var border = new Border { Child = image, BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left, Width = 332, Height = 252 };
        border.SetResourceReference(Border.BorderBrushProperty, "ThemeBorderBrush");
        border.SetResourceReference(Border.BackgroundProperty, "ThemeFieldBrush");
        return border;
    }
}
