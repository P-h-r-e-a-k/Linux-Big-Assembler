using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LBAAssembler.Maps;

namespace LBAAssembler;

// File > Map text…: the open map's own lines of dialogue (MapText; 2026-10-23, recreating LBA1's Hamalayi by hand: its ski instructor's
// lines). Each line a number -- a script's message of that number says it in the map's scenes -- how it is shown, and its words. Saved
// into the map's workspace (its <stem>_TEXT.TXT, where the engine reads an installed island's) and so into the map with Save map.
internal sealed class MapTextWindow : Window
{
    private readonly string path;
    private MapText text;
    private bool dirty, listing;
    private readonly ListBox list = new() { MinHeight = 200 };
    private readonly TextBox numberBox = new() { Width = 90, Padding = new Thickness(3) };
    private readonly ComboBox shownBox = new() { Width = 220 };
    private readonly TextBox wordsBox = new() { AcceptsReturn = false, TextWrapping = TextWrapping.Wrap, Height = 90, Padding = new Thickness(3), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private static readonly (int Attribute, string Name)[] Ways =
        { (1, "An ordinary line"), (2, "A big message"), (4, "Full screen, no frame"), (8, "Said in a bubble (no pause)") };

    public MapTextWindow(string path, string mapName)
    {
        this.path = path;
        text = MapText.Load(path);
        Title = $"Map text — {mapName}";
        Width = 760; Height = 560; MinWidth = 560; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "ThemeFieldBrush");
        var root = new DockPanel { Margin = new Thickness(12) };
        Content = root;
        TextBlock Label(string t, bool muted = true)
        {
            var l = new TextBlock { Text = t, Margin = new Thickness(0, 6, 0, 2), TextWrapping = TextWrapping.Wrap };
            l.SetResourceReference(TextBlock.ForegroundProperty, muted ? "ThemeTextMutedBrush" : "ThemeTextBrush");
            return l;
        }
        Button B(string content, string tip, Action click)
        {
            var b = new Button { Content = content, ToolTip = tip, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 6, 6) };
            System.Windows.Automation.AutomationProperties.SetHelpText(b, tip);
            b.Click += (_, _) => click();
            return b;
        }

        var intro = Label("The map's own lines of dialogue. A script's message of one of these numbers says the line in the map's scenes, instead of the island's line of that number (shown, not spoken). '@' in the words starts a new paragraph.");
        DockPanel.SetDock(intro, Dock.Top);
        root.Children.Add(intro);

        var bottom = new StackPanel();
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        var fields = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fields.ColumnDefinitions.Add(new ColumnDefinition());
        void Field(int row, string label, FrameworkElement control)
        {
            fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = Label(label, muted: false); l.Margin = new Thickness(0, 4, 10, 4); l.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetRow(l, row); Grid.SetRow(control, row); Grid.SetColumn(control, 1);
            control.Margin = new Thickness(0, 2, 0, 2); control.HorizontalAlignment = control == wordsBox ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
            fields.Children.Add(l); fields.Children.Add(control);
        }
        foreach (var (attribute, name) in Ways) shownBox.Items.Add(new ComboBoxItem { Content = name, Tag = attribute });
        shownBox.SelectedIndex = 0;
        Field(0, "Number", numberBox);
        Field(1, "Shown as", shownBox);
        Field(2, "Words", wordsBox);
        bottom.Children.Add(fields);
        var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        buttons.Children.Add(B("New line", "A new line: the next free number, no words yet", NewLine));
        buttons.Children.Add(B("Put this line", "The line into the map's text under its number (replacing the line that had it)", PutLine));
        buttons.Children.Add(B("Remove this line", "Takes the line of this number out", RemoveLine));
        var save = B("Save", "Saves the lines into the map (then File > Save map writes the map's file)", () => Save());
        save.FontWeight = FontWeights.SemiBold;
        buttons.Children.Add(save);
        bottom.Children.Add(buttons);
        status.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        System.Windows.Automation.AutomationProperties.SetAutomationId(status, "MapTextStatus");
        bottom.Children.Add(status);

        list.SetResourceReference(BackgroundProperty, "ThemeFieldBrush");
        list.SetResourceReference(ForegroundProperty, "ThemeTextBrush");
        root.Children.Add(list);

        foreach (var (control, name) in new (Control, string)[] { (numberBox, "Number"), (shownBox, "Shown as"), (wordsBox, "Words"), (list, "Lines") })
            System.Windows.Automation.AutomationProperties.SetName(control, name);
        list.SelectionChanged += (_, _) =>
        {
            if (listing || list.SelectedItem is not ListBoxItem { Tag: int number } || !text.Lines.TryGetValue(number, out var line)) return;
            numberBox.Text = number.ToString();
            shownBox.SelectedIndex = Math.Max(0, Array.FindIndex(Ways, w => w.Attribute == line.Attribute));
            wordsBox.Text = line.Words;
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { Save(); e.Handled = true; } };
        Closing += (_, e) =>
        {
            if (!dirty) return;
            var answer = MessageBox.Show(this, "Save the map's text first?\n\nYes saves it, No throws the changes away.", Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel || answer == MessageBoxResult.Yes && !Save()) e.Cancel = true;
        };
        Refresh();
        if (text.Lines.Count == 0) NewLine();
        Say(text.Lines.Count == 0 ? "The map has no lines of its own yet." : $"{text.Lines.Count} lines.");
    }

    private void Say(string message) => status.Text = message;

    private void Refresh(int? select = null)
    {
        listing = true;
        list.Items.Clear();
        foreach (var line in text.Lines.Values)
        {
            var item = new ListBoxItem { Content = $"{line.Number}{(line.Attribute == MapText.Ordinary ? "" : $" ({Ways.FirstOrDefault(w => w.Attribute == line.Attribute).Name ?? $"shown {line.Attribute}"})")}: {line.Words}", Tag = line.Number };
            list.Items.Add(item);
            if (line.Number == select) list.SelectedItem = item;
        }
        listing = false;
        Title = (dirty ? "* " : "") + Title.TrimStart('*', ' ');
    }

    private void NewLine()
    {
        numberBox.Text = text.NextNumber.ToString();
        shownBox.SelectedIndex = 0;
        wordsBox.Text = "";
        list.SelectedItem = null;
        wordsBox.Focus();
        Say($"A new line, number {numberBox.Text}: type its words, then Put this line.");
    }

    private void PutLine()
    {
        if (!int.TryParse(numberBox.Text.Trim(), out var number) || number < 0 || number > 65535) { Say("The number is a whole number, 0 to 65535 (a script's message names it)."); return; }
        var words = MapText.OneLine(wordsBox.Text);
        if (words.Length == 0) { Say("Type the line's words first."); return; }
        var attribute = shownBox.SelectedItem is ComboBoxItem { Tag: int a } ? a : MapText.Ordinary;
        var replaced = text.Lines.ContainsKey(number);
        text.Lines[number] = new MapText.Line(number, attribute, words);
        dirty = true;
        Refresh(number);
        Say(replaced ? $"Line {number} changed (not saved yet)." : $"Line {number} put in (not saved yet).");
    }

    private void RemoveLine()
    {
        if (!int.TryParse(numberBox.Text.Trim(), out var number) || !text.Lines.Remove(number)) { Say("No line has that number."); return; }
        dirty = true;
        Refresh();
        Say($"Line {number} taken out (not saved yet).");
    }

    private bool Save()
    {
        try
        {
            new FileTransaction().Write(path, System.Text.Encoding.UTF8.GetBytes(text.Format())).Commit();
            dirty = false;
            Refresh();
            Say($"Saved {text.Lines.Count} lines into the map. File > Save map writes the map's file.");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Say($"Couldn't save: {e.Message}");
            return false;
        }
    }
}
