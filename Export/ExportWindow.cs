using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LBAAssembler.Export;

// Tools > Export 3D models: pick what to export (islands, single cubes, single buildings or trees where they stand, the objects of an island,
// interiors, every actor and body, furniture and items), see it in the preview, choose a format (glTF / OBJ / PLY / STL) and a folder. The files are
// for other 3D tools, not for the game.
internal sealed class ExportWindow : Window
{
    private readonly ExportCatalog catalog;
    private readonly ComboBox categoryBox = new() { MinWidth = 300 };
    private readonly ComboBox formatBox = new() { MinWidth = 90 };
    private readonly TextBox filterBox = new() { Padding = new Thickness(3), ToolTip = "Show only the items whose name contains this text" };
    private readonly ListBox list = new() { SelectionMode = SelectionMode.Extended, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12 };
    private readonly TextBox folderBox = new() { Padding = new Thickness(3) };
    private readonly TextBox scaleBox = new() { Text = "0.001", Width = 60, Padding = new Thickness(3), ToolTip = "File units per game unit. 0.001 makes 1000 game units (about Twinsen's height) one unit, i.e. roughly a metre" };
    private readonly CheckBox terrainBox = new() { Content = "Ground", IsChecked = true, ToolTip = "Islands and cubes: the terrain" };
    private readonly CheckBox objectsBox = new() { Content = "Objects on it", IsChecked = true, ToolTip = "Islands and cubes: the buildings, trees and props standing on it" };
    private readonly TextBox marginBox = new() { Text = "0", Width = 40, Padding = new Thickness(3), ToolTip = "Single objects: how many ground cells (512 units each) to take round the object, so it stands on a patch of its island; 0 = the object alone" };
    private readonly StackPanel marginRow = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel islandRow = new() { Orientation = Orientation.Horizontal };
    private readonly CheckBox recentreBox = new() { Content = "Stand on the origin", IsChecked = true, ToolTip = "Move each model so its lowest point is at height 0 and its middle over the origin, instead of where it lies in the game's world" };
    private readonly CheckBox combineBox = new() { Content = "Combine the selected into one file", ToolTip = "One file with everything selected: objects of one island keep their places, other models stand in a row" };
    private readonly CheckBox keepPlacesBox = new() { Content = "Keep their places in the game's world (unticked: in a row)", ToolTip = "Ticked: each item stays where it stands, relative to the others (whole islands, cubes). Unticked: the items stand side by side in a row" };
    private readonly TextBox combinedName = new() { Text = "combined", Width = 130, Padding = new Thickness(3), ToolTip = "The file name for the combined model" };
    private readonly TextBlock description = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
    private readonly TextBlock countText = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
    private readonly TextBlock formatText = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Button exportButton = new() { Content = "Export", Padding = new Thickness(18, 5, 18, 5), IsDefault = true };
    private readonly Button cancelButton = new() { Content = "Stop", Padding = new Thickness(14, 5, 14, 5), IsEnabled = false };
    private readonly ProgressBar progressBar = new() { Height = 8, Margin = new Thickness(0, 8, 0, 6) };
    private readonly TextBox log = new() { IsReadOnly = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 11, Height = 96 };
    private readonly ScenePreview preview = new() { MinHeight = 260 };
    private readonly DispatcherTimer previewTimer = new() { Interval = TimeSpan.FromMilliseconds(260) };

    private List<ExportItem> items = new();
    private CancellationTokenSource? running;
    private readonly string? preselectFile;
    private ExportItem? lastPicked;
    private const int PreviewLimit = 12;

    public ExportWindow(ExportCatalog catalog, string? categoryHint = null, string? preselectFile = null)
    {
        this.catalog = catalog;
        this.preselectFile = preselectFile;
        Title = "Export 3D models";
        Width = 1240; Height = 860;
        MinWidth = 980; MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "ThemeWindowBrush");
        SetResourceReference(ForegroundProperty, "ThemeTextBrush");
        BuildLayout();
        foreach (var c in catalog.Categories) categoryBox.Items.Add(c.Title);
        foreach (var f in new[] { ExportFormat.Glb, ExportFormat.Obj, ExportFormat.Ply, ExportFormat.Stl }) formatBox.Items.Add(new ComboBoxItem { Content = SceneWriters.Extension(f).TrimStart('.').ToUpperInvariant(), Tag = f });
        formatBox.SelectedIndex = 0;
        formatBox.SelectionChanged += (_, _) => formatText.Text = SceneWriters.Describe(SelectedFormat);
        formatText.Text = SceneWriters.Describe(SelectedFormat);
        folderBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LBA Assembler 3D export");
        categoryBox.SelectionChanged += (_, _) => LoadCategory();
        filterBox.TextChanged += (_, _) => FillList();
        list.SelectionChanged += (_, e) =>
        {
            if (e.AddedItems.OfType<ItemRow>().LastOrDefault() is { } added) lastPicked = added.Item;
            UpdateCount(); SchedulePreview();
        };
        foreach (var box in new[] { terrainBox, objectsBox, recentreBox, combineBox, keepPlacesBox })
        { box.Checked += (_, _) => SchedulePreview(); box.Unchecked += (_, _) => SchedulePreview(); }
        marginBox.TextChanged += (_, _) => SchedulePreview();
        previewTimer.Tick += async (_, _) => { previewTimer.Stop(); await RefreshPreview(); };
        Loaded += (_, _) =>
        {
            if (catalog.Categories.Count == 0) { log.Text = "No LBA1 or LBA2 game folder is set (File > Settings)."; return; }
            var start = categoryHint is null ? -1 : catalog.Categories.ToList().FindIndex(c => c.Title.Contains(categoryHint, StringComparison.OrdinalIgnoreCase));
            categoryBox.SelectedIndex = Math.Max(0, start);
        };
        Closing += (_, _) => running?.Cancel();
    }

    private ExportFormat SelectedFormat => formatBox.SelectedItem is ComboBoxItem { Tag: ExportFormat f } ? f : ExportFormat.Glb;

    private void BuildLayout()
    {
        foreach (var t in new TextBlock[] { description, countText, formatText }) t.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        foreach (var box in new TextBox[] { filterBox, folderBox, scaleBox, log, marginBox, combinedName })
        {
            box.SetResourceReference(BackgroundProperty, "ThemeFieldBrush");
            box.SetResourceReference(ForegroundProperty, "ThemeTextBrush");
            box.SetResourceReference(BorderBrushProperty, "ThemeBorderBrush");
        }
        list.SetResourceReference(BackgroundProperty, "ThemeFieldBrush");
        list.SetResourceReference(ForegroundProperty, "ThemeTextBrush");
        foreach (var check in new CheckBox[] { terrainBox, objectsBox, recentreBox, combineBox, keepPlacesBox }) check.SetResourceReference(ForegroundProperty, "ThemeTextBrush");

        Label Caption(string text) { var l = new Label { Content = text, Padding = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center }; l.SetResourceReference(ForegroundProperty, "ThemeTextMutedBrush"); return l; }

        var root = new DockPanel { Margin = new Thickness(14) };

        // top: what / format / scale
        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
        DockPanel.SetDock(top, Dock.Top);
        top.Children.Add(Caption("What")); top.Children.Add(categoryBox);
        top.Children.Add(new Label { Width = 14 });
        top.Children.Add(Caption("Format")); top.Children.Add(formatBox);
        top.Children.Add(new Label { Width = 14 });
        top.Children.Add(Caption("Scale")); top.Children.Add(scaleBox);
        root.Children.Add(top);
        var descriptionHost = new Border { Child = description };
        DockPanel.SetDock(descriptionHost, Dock.Top);
        root.Children.Add(descriptionHost);

        // bottom: folder, format note, buttons, progress, log
        var bottom = new StackPanel();
        DockPanel.SetDock(bottom, Dock.Bottom);
        var folderRow = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var browse = new Button { Content = "Browse…", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(6, 0, 0, 0) };
        browse.Click += (_, _) => BrowseFolder();
        DockPanel.SetDock(browse, Dock.Right);
        var folderCaption = Caption("Save into");
        DockPanel.SetDock(folderCaption, Dock.Left);
        folderRow.Children.Add(folderCaption); folderRow.Children.Add(browse); folderRow.Children.Add(folderBox);
        bottom.Children.Add(folderRow);
        bottom.Children.Add(formatText);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        exportButton.Click += (_, _) => StartExport();
        cancelButton.Click += (_, _) => running?.Cancel();
        buttons.Children.Add(exportButton); buttons.Children.Add(new Label { Width = 8 }); buttons.Children.Add(cancelButton); buttons.Children.Add(countText);
        bottom.Children.Add(buttons);
        bottom.Children.Add(progressBar);
        bottom.Children.Add(log);
        root.Children.Add(bottom);

        // left: filter, select buttons, list
        var left = new DockPanel { Width = 470, Margin = new Thickness(0, 0, 12, 0) };
        var selectRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(selectRow, Dock.Top);
        selectRow.Children.Add(Caption("Filter")); filterBox.Width = 190; selectRow.Children.Add(filterBox);
        foreach (var (text, action) in new (string, Action)[] { ("All", () => list.SelectAll()), ("None", () => list.UnselectAll()) })
        {
            var b = new Button { Content = text, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(6, 0, 0, 0) };
            var a = action; b.Click += (_, _) => { a(); list.Focus(); };
            selectRow.Children.Add(b);
        }
        left.Children.Add(selectRow);
        left.Children.Add(list);
        DockPanel.SetDock(left, Dock.Left);
        root.Children.Add(left);

        // right: preview above, options below
        var right = new DockPanel();
        var options = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(options, Dock.Bottom);
        islandRow.Children.Add(terrainBox); islandRow.Children.Add(new Label { Width = 14 }); islandRow.Children.Add(objectsBox);
        options.Children.Add(islandRow);
        marginRow.Children.Add(Caption("Ground round the object (cells)")); marginRow.Children.Add(marginBox);
        options.Children.Add(marginRow);
        options.Children.Add(recentreBox);
        var combineRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        combineRow.Children.Add(combineBox); combineRow.Children.Add(new Label { Width = 8 }); combineRow.Children.Add(Caption("File name")); combineRow.Children.Add(combinedName);
        options.Children.Add(combineRow);
        keepPlacesBox.Margin = new Thickness(18, 2, 0, 0);
        options.Children.Add(keepPlacesBox);
        right.Children.Add(options);
        var previewBorder = new Border { Child = preview, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2), ClipToBounds = true };
        previewBorder.SetResourceReference(Border.BorderBrushProperty, "ThemeBorderBrush");
        right.Children.Add(previewBorder);
        root.Children.Add(right);

        Content = root;
    }

    private ExportOptions MakeOptions(Action<string>? logTo = null) => new()
    {
        IslandTerrain = terrainBox.IsChecked == true,
        IslandObjects = objectsBox.IsChecked == true,
        GroundMargin = int.TryParse(marginBox.Text.Trim(), out var margin) ? Math.Clamp(margin, 0, 64) : 0,
        Recentre = recentreBox.IsChecked == true,
        Combine = combineBox.IsChecked == true,
        CombinedName = combinedName.Text.Trim().Length > 0 ? combinedName.Text.Trim() : "combined",
        KeepPlaces = keepPlacesBox.IsChecked == true,
        Log = logTo,
    };

    private void LoadCategory()
    {
        if (categoryBox.SelectedIndex < 0) return;
        var category = catalog.Categories[categoryBox.SelectedIndex];
        description.Text = category.Description;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            items = category.Load();
            var islandKind = category.Title.Contains("islands (", StringComparison.OrdinalIgnoreCase) || category.Title.Contains("ground by cube", StringComparison.OrdinalIgnoreCase);
            islandRow.Visibility = islandKind ? Visibility.Visible : Visibility.Collapsed;
            marginRow.Visibility = category.Title.Contains("one object at a time", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
            keepPlacesBox.IsChecked = islandKind;          // (whole islands and cubes belong together in the world; objects and bodies are more use side by side)
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
        {
            items = new();
            log.Text = $"Couldn't read {category.Title}: {error.Message}";
        }
        finally { Mouse.OverrideCursor = null; }
        filterBox.Clear();
        FillList();
        if (preselectFile is not null && items.FirstOrDefault(i => i.FileName.Equals(preselectFile, StringComparison.OrdinalIgnoreCase)) is { } wanted)
        {
            list.SelectedItem = list.Items.OfType<ItemRow>().FirstOrDefault(r => r.Item == wanted);
            if (list.SelectedItem is not null) list.ScrollIntoView(list.SelectedItem);
        }
        else if (list.Items.Count > 0) preview.ShowMessage("Pick an item to see it here.");
    }

    private sealed record ItemRow(ExportItem Item) { public override string ToString() => Item.Label; }

    private void FillList()
    {
        var text = filterBox.Text.Trim();
        var keep = list.SelectedItems.OfType<ItemRow>().Select(r => r.Item).ToHashSet();
        list.Items.Clear();
        foreach (var item in items.Where(i => text.Length == 0 || i.Label.Contains(text, StringComparison.OrdinalIgnoreCase)))
        {
            var row = new ItemRow(item);
            list.Items.Add(row);
            if (keep.Contains(item)) list.SelectedItems.Add(row);
        }
        UpdateCount();
    }

    private void UpdateCount() => countText.Text = $"{list.SelectedItems.Count} selected of {items.Count}";

    // ---- preview -------------------------------------------------------------------------------------------------------------------------

    private void SchedulePreview() { previewTimer.Stop(); previewTimer.Start(); }

    private async Task RefreshPreview()
    {
        if (running is not null) return;          // (the catalog's readers are shared with a running export)
        var chosen = list.SelectedItems.OfType<ItemRow>().Select(r => r.Item).ToList();
        if (chosen.Count == 0) { preview.ShowMessage("Pick an item to see it here."); return; }
        var options = MakeOptions();
        if (chosen.Count > 1 && options.Combine)
        {
            var shown = chosen.Take(PreviewLimit).ToList();
            await preview.ShowAsync(() => ExportRunner.BuildCombined(shown, options), $"{chosen.Count} items as one model" + (chosen.Count > shown.Count ? $" (the first {PreviewLimit} shown)" : ""));
            return;
        }
        var item = chosen.Contains(lastPicked!) ? lastPicked! : chosen[0];
        await preview.ShowAsync(() => ExportRunner.BuildOne(item, options), item.Label.Trim());
    }

    // ---- exporting -----------------------------------------------------------------------------------------------------------------------

    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Where to save the 3D files", InitialDirectory = Directory.Exists(folderBox.Text) ? folderBox.Text : null };
        if (dialog.ShowDialog(this) == true) folderBox.Text = dialog.FolderName;
    }

    private async void StartExport()
    {
        var chosen = list.SelectedItems.OfType<ItemRow>().Select(r => r.Item).ToList();
        if (chosen.Count == 0) { log.Text = "Select at least one item in the list (All selects every one)."; return; }
        if (!float.TryParse(scaleBox.Text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scale) || scale <= 0)
        { log.Text = "The scale must be a positive number (0.001 is the default)."; return; }
        var folder = folderBox.Text.Trim();
        if (folder.Length == 0) { log.Text = "Choose a folder to save into."; return; }
        var format = SelectedFormat;
        try { Directory.CreateDirectory(folder); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { log.Text = $"Can't use that folder: {error.Message}"; return; }

        exportButton.IsEnabled = false; cancelButton.IsEnabled = true;
        progressBar.Maximum = chosen.Count; progressBar.Value = 0;
        log.Clear();
        running = new CancellationTokenSource();
        var token = running.Token;
        var extraLog = new Progress<string>(message => Append("  " + message));
        var options = MakeOptions(message => ((IProgress<string>)extraLog).Report(message));
        var progress = new Progress<(int Index, string Message)>(report => { progressBar.Value = Math.Min(chosen.Count, report.Index + 1); Append(report.Message); });
        (int Done, int Failed, int Skipped) result = default;
        try { result = await Task.Run(() => ExportRunner.Run(chosen, format, scale, folder, options, progress, token)); }
        catch (Exception error) { Append("Export stopped: " + error.Message); DebugLog.Log($"Export window: {error}"); }
        var cancelled = token.IsCancellationRequested;
        running = null;
        exportButton.IsEnabled = true; cancelButton.IsEnabled = false;
        Append($"{(cancelled ? "Stopped: " : "Done: ")}{result.Done} exported{(result.Failed > 0 ? $", {result.Failed} failed" : "")}{(result.Skipped > 0 ? $", {result.Skipped} empty in the game's data" : "")}. Files are in {folder}");
    }

    private void Append(string message)
    {
        log.AppendText(message + Environment.NewLine);
        log.ScrollToEnd();
    }

    // Opens the window from another one (or the main window).
    public static void Show(Window owner, string? lba1Directory, string? lba2Directory, string? categoryHint = null, string? preselectFile = null)
    {
        var catalog = new ExportCatalog(lba1Directory, lba2Directory);
        if (catalog.Categories.Count == 0)
        {
            MessageBox.Show(owner, "Neither game folder is set. Choose them under File > Settings.", "Export 3D models", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var window = new ExportWindow(catalog, categoryHint, preselectFile) { Owner = owner };
        WindowLifecycle.Register(window, "ExportWindow");
        window.Show();
    }
}
