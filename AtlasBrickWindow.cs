using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LBAAssembler.Lba1;
using LBAAssembler.Terrain.Polar;

namespace LBAAssembler;

// An LBA1 brick's face as a tile of an island's ground atlas (the terrain editor's Ground atlas > Put an LBA1 brick here…; 2026-10-23,
// recreating LBA1's Hamalayi by hand: its snow, its ice cliffs and its gravel are LBA1's bricks). LBA1 draws a brick in an isometric
// diamond: its top face is unskewed into a square, a side into a square half as tall -- or two bricks' sides one over the other, a
// cliff two layers high -- each pixel the nearest of the island palette's colours. The brick's number is LBA_BRK.HQR's (the Bricks and
// sprites editor and the LBA1 scenes' pointer show them).
internal sealed class AtlasBrickWindow : Window
{
    public enum FaceKind { Top, SideX, SideZ, TwoSides }

    private readonly Lba1Game lba1;
    private readonly byte[] islandPalette;
    private readonly int size;
    private readonly TextBox brickBox = new() { Text = "2713", Width = 80, Padding = new Thickness(3) };
    private readonly TextBox underBox = new() { Text = "", Width = 80, Padding = new Thickness(3), IsEnabled = false };
    private readonly ComboBox faceBox = new() { Width = 260 };
    private readonly Image brickImage = new() { Width = 96, Height = 96, Stretch = Stretch.Uniform };
    private readonly Image tileImage = new() { Width = 128, Height = 128, Stretch = Stretch.Fill };
    private readonly TextBlock note = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };

    // The tile chosen (the island palette's indices, size x size), once OK is pressed.
    public byte[]? Tile { get; private set; }
    public string Description { get; private set; } = "";

    public AtlasBrickWindow(Lba1Game lba1, byte[] islandPalette, int size)
    {
        this.lba1 = lba1; this.islandPalette = islandPalette; this.size = size;
        Title = "Put an LBA1 brick in the ground atlas";
        SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "ThemeFieldBrush");
        var root = new StackPanel { Margin = new Thickness(14) };
        Content = root;
        TextBlock Label(string t) { var l = new TextBlock { Text = t, Margin = new Thickness(0, 8, 0, 2) }; l.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush"); return l; }
        root.Children.Add(Label("The brick (LBA_BRK.HQR's number)"));
        root.Children.Add(brickBox);
        root.Children.Add(Label("Which of its faces"));
        foreach (var (kind, text) in new[] { (FaceKind.Top, "Its top (the ground)"), (FaceKind.SideX, "Its side facing +X (a wall)"), (FaceKind.SideZ, "Its side facing +Z (a wall)"),
                                             (FaceKind.TwoSides, "Its side over another brick's (a cliff two layers high)") })
            faceBox.Items.Add(new ComboBoxItem { Content = text, Tag = kind });
        faceBox.SelectedIndex = 0;
        root.Children.Add(faceBox);
        root.Children.Add(Label("The brick under it (for a side over another's)"));
        root.Children.Add(underBox);
        var previews = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        previews.Children.Add(new Border { Child = brickImage, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, Margin = new Thickness(0, 0, 10, 0) });
        previews.Children.Add(new Border { Child = tileImage, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray });
        root.Children.Add(previews);
        note.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextMutedBrush");
        root.Children.Add(note);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = "Put it in", IsDefault = true, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(12, 4, 12, 4) };
        ok.Click += (_, _) => { if (Tile is not null) { DialogResult = true; } };
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        foreach (var (control, name) in new (Control, string)[] { (brickBox, "Brick number"), (faceBox, "Face"), (underBox, "Brick under it") })
            System.Windows.Automation.AutomationProperties.SetName(control, name);
        brickBox.TextChanged += (_, _) => Refresh();
        underBox.TextChanged += (_, _) => Refresh();
        faceBox.SelectionChanged += (_, _) => { underBox.IsEnabled = Face == FaceKind.TwoSides; Refresh(); };
        Loaded += (_, _) => Refresh();
    }

    private FaceKind Face => faceBox.SelectedItem is ComboBoxItem { Tag: FaceKind k } ? k : FaceKind.Top;

    private void Refresh()
    {
        Tile = null;
        if (!int.TryParse(brickBox.Text.Trim(), out var brick) || lba1.ReadBrick(brick) is not { } data) { note.Text = "No LBA1 brick of that number."; brickImage.Source = null; tileImage.Source = null; return; }
        var sprite = PolarTextures.Sprite.Decode(data);
        brickImage.Source = SpriteBitmap(sprite);
        int[] lba1Tile; int w, h;
        switch (Face)
        {
            case FaceKind.Top: lba1Tile = sprite.Tile(PolarTextures.Face.Top); (w, h) = PolarTextures.Size(PolarTextures.Face.Top); break;
            case FaceKind.SideX: lba1Tile = sprite.Tile(PolarTextures.Face.SideX); (w, h) = PolarTextures.Size(PolarTextures.Face.SideX); break;
            case FaceKind.SideZ: lba1Tile = sprite.Tile(PolarTextures.Face.SideZ); (w, h) = PolarTextures.Size(PolarTextures.Face.SideZ); break;
            default:
                var top = sprite.Tile(PolarTextures.Face.SideX);
                var underSprite = int.TryParse(underBox.Text.Trim(), out var u) && lba1.ReadBrick(u) is { } ud ? PolarTextures.Sprite.Decode(ud) : sprite;
                lba1Tile = top.Concat(underSprite.Tile(PolarTextures.Face.SideZ)).ToArray();
                (w, h) = (PolarTextures.TileSize, PolarTextures.TileSize);
                break;
        }
        // (into the chosen square: stretched to it)
        var tile = new byte[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var c = lba1Tile[Math.Min(h - 1, y * h / size) * w + Math.Min(w - 1, x * w / size)];
                var (r, g, b) = PolarTextures.Colours.Rgb(lba1.Palette, Math.Max(0, c));
                tile[y * size + x] = (byte)Terrain.Snow.SnowColour.Nearest(islandPalette, r, g, b);
            }
        Tile = tile;
        Description = $"LBA1 brick {brick}'s {(Face switch { FaceKind.Top => "top", FaceKind.SideX => "+X side", FaceKind.SideZ => "+Z side", _ => "side over " + underBox.Text.Trim() })}";
        tileImage.Source = TileBitmap(tile, size, islandPalette);
        note.Text = $"{Description}, {size} x {size} pixels in the island's colours.";
    }

    private BitmapSource SpriteBitmap(PolarTextures.Sprite sprite)
    {
        var px = new byte[sprite.Width * sprite.Lines * 4];
        for (var i = 0; i < sprite.Width * sprite.Lines; i++)
        {
            var c = sprite.Pixels[i];
            if (c < 0) continue;
            var (r, g, b) = PolarTextures.Colours.Rgb(lba1.Palette, c);
            px[i * 4] = (byte)b; px[i * 4 + 1] = (byte)g; px[i * 4 + 2] = (byte)r; px[i * 4 + 3] = 255;
        }
        var bmp = BitmapSource.Create(Math.Max(1, sprite.Width), Math.Max(1, sprite.Lines), 96, 96, PixelFormats.Bgra32, null, px, Math.Max(1, sprite.Width) * 4);
        bmp.Freeze();
        return bmp;
    }

    public static BitmapSource TileBitmap(byte[] tile, int size, byte[] palette)
    {
        var px = new byte[size * size * 4];
        for (var i = 0; i < size * size; i++)
        {
            var (r, g, b) = PolarTextures.Colours.Rgb(palette, tile[i]);
            px[i * 4] = (byte)b; px[i * 4 + 1] = (byte)g; px[i * 4 + 2] = (byte)r; px[i * 4 + 3] = 255;
        }
        var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, px, size * 4);
        bmp.Freeze();
        return bmp;
    }
}
