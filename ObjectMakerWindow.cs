using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LBAAssembler.Lba1;
using LBAAssembler.Terrain;
using LBAAssembler.Terrain.Polar;

namespace LBAAssembler;

// The Object maker (Build > Buildings and decor > Make an object…; 2026-10-23, recreating LBA1's Hamalayi by hand needed its hut, its
// ski lift's pylons, its trees and its rock walls as island objects): an object built of LBA1's bricks cell by cell, as LBA1's own
// builder made them -- a brick typed into a cell (across, up a layer, down), or a box of cells filled with one -- seen as LBA1 would draw
// it, then made into a body of the island (BrickObject) for Add object to place. The design can be saved and opened again (a text file,
// a brick a line).
internal sealed class ObjectMakerWindow : Window
{
    private readonly Lba1Game lba1;
    private readonly Func<IReadOnlyDictionary<(int X, int Layer, int Z), int>, string> add;
    private Dictionary<(int X, int Layer, int Z), int> cells = new();
    private readonly List<Dictionary<(int X, int Layer, int Z), int>> undo = new();
    private readonly TextBox xBox = Field("0"), layerBox = Field("0"), zBox = Field("0"), brickBox = Field("1");
    private readonly TextBox toXBox = Field("0"), toLayerBox = Field("0"), toZBox = Field("0");
    private readonly ListBox cellList = new() { Height = 180, Width = 250 };
    private readonly Image brickImage = new() { Width = 72, Height = 72, Stretch = Stretch.Uniform };
    private readonly Image preview = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), MaxWidth = 300 };
    private readonly Dictionary<int, PolarTextures.Sprite> sprites = new();
    private bool listing;

    private static TextBox Field(string text) => new() { Text = text, Width = 70, Padding = new Thickness(3), Margin = new Thickness(0, 0, 6, 0) };

    // `add`: makes the object into a body of the island, returning what to tell the user
    public ObjectMakerWindow(Lba1Game lba1, Func<IReadOnlyDictionary<(int X, int Layer, int Z), int>, string> add)
    {
        this.lba1 = lba1; this.add = add;
        Title = "Object maker";
        Width = 900; Height = 640; MinWidth = 700; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "ThemeFieldBrush");
        var root = new DockPanel { Margin = new Thickness(12) };
        Content = root;

        var left = new StackPanel { Width = 310 };
        DockPanel.SetDock(left, Dock.Left);
        root.Children.Add(left);
        TextBlock Label(string t, bool head = false)
        {
            var l = new TextBlock { Text = t, Margin = new Thickness(0, head ? 10 : 4, 0, 2), FontWeight = head ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
            l.SetResourceReference(TextBlock.ForegroundProperty, head ? "ThemeTextBrush" : "ThemeTextMutedBrush");
            return l;
        }
        StackPanel Row(params UIElement[] items) { var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) }; foreach (var i in items) r.Children.Add(i); return r; }
        Button B(string text, string tip, Action click)
        {
            var b = new Button { Content = text, ToolTip = tip, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 6, 6) };
            System.Windows.Automation.AutomationProperties.SetHelpText(b, tip);
            b.Click += (_, _) => click();
            return b;
        }
        left.Children.Add(Label("A cell: across (X), up (layer), down (Z) -- each 512 across and a layer 256 high, as LBA1's grid", true));
        left.Children.Add(Row(Label("X"), xBox, Label("Layer"), layerBox, Label("Z"), zBox));
        left.Children.Add(Label("The LBA1 brick (LBA_BRK.HQR's number, as the Bricks and sprites editor and the LBA1 scenes' pointer show it)"));
        brickBox.VerticalAlignment = VerticalAlignment.Top;
        left.Children.Add(Row(brickBox, new Border { Child = brickImage, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray }));
        var cellButtons = new WrapPanel();
        cellButtons.Children.Add(B("Put the brick here", "The brick into the cell (replacing what was there)", PutOne));
        cellButtons.Children.Add(B("Empty this cell", "Takes the cell's brick out", EmptyOne));
        left.Children.Add(cellButtons);
        left.Children.Add(Label("A box of cells, from the cell above to this one", true));
        left.Children.Add(Row(Label("To X"), toXBox, Label("Layer"), toLayerBox, Label("Z"), toZBox));
        var boxButtons = new WrapPanel();
        boxButtons.Children.Add(B("Fill the box with the brick", "Every cell of the box gets the brick", () => FillBox(empty: false)));
        boxButtons.Children.Add(B("Empty the box", "Takes every brick of the box out", () => FillBox(empty: true)));
        left.Children.Add(boxButtons);
        var fileButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        fileButtons.Children.Add(B("Undo", "Takes the last change back (Ctrl+Z)", Undo));
        fileButtons.Children.Add(B("Start again", "Empties the whole object", () => { Remember(); cells = new(); Refresh(); }));
        fileButtons.Children.Add(B("Open design…", "Opens an object's design saved before (a text file: a brick a line)", OpenDesign));
        fileButtons.Children.Add(B("Save design…", "Saves the design to make it again or change it later", SaveDesign));
        left.Children.Add(fileButtons);
        var addButton = B("Add it to the island", "Makes the object into a body of the island (its OBL) and its textures into an object page; then place it with Add object", AddToIsland);
        addButton.FontWeight = FontWeights.SemiBold;
        left.Children.Add(addButton);
        status.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        left.Children.Add(status);

        var right = new DockPanel { Margin = new Thickness(12, 0, 0, 0) };
        root.Children.Add(right);
        var listPanel = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        DockPanel.SetDock(listPanel, Dock.Right);
        listPanel.Children.Add(Label("The bricks (pick one to edit its cell)", true));
        listPanel.Children.Add(cellList);
        right.Children.Add(listPanel);
        RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.NearestNeighbor);
        var previewBorder = new Border { Child = preview, BorderThickness = new Thickness(1), Background = Brushes.Black };
        previewBorder.SetResourceReference(Border.BorderBrushProperty, "ThemeTextMutedBrush");
        right.Children.Add(previewBorder);

        foreach (var (control, name) in new (Control, string)[] { (xBox, "Cell X"), (layerBox, "Layer"), (zBox, "Cell Z"), (brickBox, "Brick"), (toXBox, "To cell X"), (toLayerBox, "To layer"), (toZBox, "To cell Z"), (cellList, "Bricks") })
            System.Windows.Automation.AutomationProperties.SetName(control, name);
        // (an id, not a name: a text's name is what it says)
        System.Windows.Automation.AutomationProperties.SetAutomationId(status, "ObjectMakerStatus");
        brickBox.TextChanged += (_, _) => ShowBrick();
        cellList.SelectionChanged += (_, _) =>
        {
            if (listing || cellList.SelectedItem is not ListBoxItem { Tag: ValueTuple<int, int, int> c }) return;
            xBox.Text = c.Item1.ToString(); layerBox.Text = c.Item2.ToString(); zBox.Text = c.Item3.ToString(); brickBox.Text = cells[c].ToString();
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) { Undo(); e.Handled = true; } };
        Loaded += (_, _) => { ShowBrick(); Refresh(); };
    }

    private void Say(string text) => status.Text = text;

    private bool Cell(TextBox x, TextBox l, TextBox z, out (int X, int Layer, int Z) cell)
    {
        cell = default;
        if (!int.TryParse(x.Text.Trim(), out var cx) || !int.TryParse(l.Text.Trim(), out var cl) || !int.TryParse(z.Text.Trim(), out var cz)) return false;
        if (cl < 0 || cl > 99 || Math.Abs(cx) > 99 || Math.Abs(cz) > 99) return false;
        cell = (cx, cl, cz);
        return true;
    }

    private bool Brick(out int brick) => int.TryParse(brickBox.Text.Trim(), out brick) && Sprite(brick).Pixels.Length > 0;

    private PolarTextures.Sprite Sprite(int brick) => sprites.TryGetValue(brick, out var s) ? s : sprites[brick] = PolarTextures.Sprite.Decode(brick > 0 ? lba1.ReadBrick(brick) : null);

    private void Remember() { undo.Add(new Dictionary<(int X, int Layer, int Z), int>(cells)); if (undo.Count > 200) undo.RemoveAt(0); }

    private void Undo()
    {
        if (undo.Count == 0) { Say("Nothing to undo."); return; }
        cells = undo[^1]; undo.RemoveAt(undo.Count - 1);
        Refresh(); Say("Undone.");
    }

    private void PutOne()
    {
        if (!Cell(xBox, layerBox, zBox, out var c)) { Say("Type the cell: whole numbers, the layer 0 or more (0 the object's foot)."); return; }
        if (!Brick(out var brick)) { Say("No LBA1 brick of that number."); return; }
        Remember(); cells[c] = brick; Refresh();
        Say($"Brick {brick} put in cell {c.X}, {c.Layer}, {c.Z}.");
    }

    private void EmptyOne()
    {
        if (!Cell(xBox, layerBox, zBox, out var c)) { Say("Type the cell: whole numbers."); return; }
        if (!cells.ContainsKey(c)) { Say("That cell is empty."); return; }
        Remember(); cells.Remove(c); Refresh();
        Say($"Cell {c.X}, {c.Layer}, {c.Z} emptied.");
    }

    private void FillBox(bool empty)
    {
        if (!Cell(xBox, layerBox, zBox, out var a) || !Cell(toXBox, toLayerBox, toZBox, out var b)) { Say("Type both corners of the box: whole numbers, layers 0 or more."); return; }
        var brick = 0;
        if (!empty && !Brick(out brick)) { Say("No LBA1 brick of that number."); return; }
        Remember();
        var n = 0;
        for (var x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++)
            for (var l = Math.Min(a.Layer, b.Layer); l <= Math.Max(a.Layer, b.Layer); l++)
                for (var z = Math.Min(a.Z, b.Z); z <= Math.Max(a.Z, b.Z); z++)
                {
                    if (empty) { if (cells.Remove((x, l, z))) n++; }
                    else { cells[(x, l, z)] = brick; n++; }
                }
        Refresh();
        Say(empty ? $"{n} cells emptied." : $"{n} cells filled with brick {brick}.");
    }

    private void OpenDesign()
    {
        var open = new Microsoft.Win32.OpenFileDialog { Filter = "Object designs (*.lbaobject)|*.lbaobject|All files|*.*", Title = "Open an object's design" };
        if (open.ShowDialog(this) != true) return;
        try
        {
            var loaded = BrickObject.FromText(File.ReadAllText(open.FileName));
            Remember(); cells = loaded; Refresh();
            Say($"Opened {Path.GetFileName(open.FileName)}: {cells.Count} bricks.");
        }
        catch (Exception e) when (e is IOException or FormatException or UnauthorizedAccessException) { Say($"Couldn't open it: {e.Message}"); }
    }

    private void SaveDesign()
    {
        var save = new Microsoft.Win32.SaveFileDialog { Filter = "Object designs (*.lbaobject)|*.lbaobject", Title = "Save the object's design", DefaultExt = ".lbaobject" };
        if (save.ShowDialog(this) != true) return;
        try { File.WriteAllText(save.FileName, BrickObject.ToText(cells)); Say($"Saved {Path.GetFileName(save.FileName)}."); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Say($"Couldn't save it: {e.Message}"); }
    }

    private void AddToIsland() => Say(add(cells));

    private void ShowBrick()
    {
        brickImage.Source = int.TryParse(brickBox.Text.Trim(), out var b) && Sprite(b).Pixels.Length > 0 ? Picture(new[] { ((0, 0, 0), b) }) : null;
    }

    private void Refresh()
    {
        listing = true;
        cellList.Items.Clear();
        foreach (var (c, b) in cells.OrderBy(k => k.Key.Layer).ThenBy(k => k.Key.Z).ThenBy(k => k.Key.X))
            cellList.Items.Add(new ListBoxItem { Content = $"{c.X}, {c.Layer}, {c.Z}: brick {b}", Tag = (c.X, c.Layer, c.Z) });
        listing = false;
        preview.Source = cells.Count == 0 ? null : Picture(cells.Select(k => ((k.Key.X, k.Key.Layer, k.Key.Z), k.Value)));
    }

    // The bricks as LBA1 draws them: each brick's picture where its cell is on LBA1's isometric screen (24 pixels right and 12 down a cell
    // along x, 24 left and 12 down along z, 15 up a layer), the cells further back first.
    private BitmapSource? Picture(IEnumerable<((int X, int Layer, int Z) Cell, int Brick)> bricks)
    {
        var list = bricks.Select(b => (b.Cell, Sprite: Sprite(b.Brick))).Where(b => b.Sprite.Pixels.Length > 0)
            .OrderBy(b => b.Cell.X + b.Cell.Z).ThenBy(b => b.Cell.Layer).ThenBy(b => b.Cell.X).ToList();
        if (list.Count == 0) return null;
        (int X, int Y) At((int X, int Layer, int Z) c, PolarTextures.Sprite s) => (24 * (c.X - c.Z) + s.HotX, 12 * (c.X + c.Z) - 15 * c.Layer + s.HotY);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var (c, s) in list)
        {
            var (x, y) = At(c, s);
            minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x + s.Width); maxY = Math.Max(maxY, y + s.Lines);
        }
        int w = maxX - minX + 8, h = maxY - minY + 8;
        var px = new byte[w * h * 4];
        foreach (var (c, s) in list)
        {
            var (x0, y0) = At(c, s);
            x0 -= minX - 4; y0 -= minY - 4;
            for (var j = 0; j < s.Lines; j++)
                for (var i = 0; i < s.Width; i++)
                {
                    var colour = s.Pixels[j * s.Width + i];
                    if (colour < 0) continue;
                    var (r, g, b) = PolarTextures.Colours.Rgb(lba1.Palette, colour);
                    var k = ((y0 + j) * w + x0 + i) * 4;
                    px[k] = (byte)b; px[k + 1] = (byte)g; px[k + 2] = (byte)r; px[k + 3] = 255;
                }
        }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
        bmp.Freeze();
        return bmp;
    }
}
