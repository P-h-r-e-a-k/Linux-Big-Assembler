using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LBAAssembler.Maps;

namespace LBAAssembler;

// File > Map settings…: the open map's name and description and, an island's, its weather (snow falling on it) and its cliffs -- walls,
// as the game's islands' are but in a jump, or ledges to walk or ride off, as LBA1's are (2026-10-23, recreating LBA1's Hamalayi: its
// snowboarder rides off them). OK sets them on the map; File > Save map writes them into its file.
internal sealed class MapSettingsWindow : Window
{
    private readonly LbaMap map;
    private readonly TextBox nameBox = new() { Width = 320, Padding = new Thickness(3) };
    private readonly TextBox descriptionBox = new() { Width = 320, Height = 60, Padding = new Thickness(3), TextWrapping = TextWrapping.Wrap, AcceptsReturn = false };
    private readonly ComboBox weatherBox = new() { Width = 320 };
    private readonly ComboBox ledgesBox = new() { Width = 320 };

    public MapSettingsWindow(LbaMap map)
    {
        this.map = map;
        Title = "Map settings";
        SizeToContent = SizeToContent.WidthAndHeight; CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.SetResourceReference(BackgroundProperty, "ThemeFieldBrush");
        var root = new StackPanel { Margin = new Thickness(14) };
        Content = root;
        TextBlock Label(string t) { var l = new TextBlock { Text = t, Margin = new Thickness(0, 8, 0, 2) }; l.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush"); return l; }
        root.Children.Add(Label("Name"));
        root.Children.Add(nameBox);
        root.Children.Add(Label("Description"));
        root.Children.Add(descriptionBox);
        var island = map.Kind == MapKind.Lba2Island;
        weatherBox.Items.Add(new ComboBoxItem { Content = "Clear", Tag = "" });
        weatherBox.Items.Add(new ComboBoxItem { Content = "Snow falling", Tag = "snow" });
        ledgesBox.Items.Add(new ComboBoxItem { Content = "Walls, as the game's islands' (but in a jump)", Tag = "" });
        ledgesBox.Items.Add(new ComboBoxItem { Content = "Ledges to walk or ride off, as LBA1's (falling)", Tag = "drop" });
        if (island)
        {
            root.Children.Add(Label("Weather"));
            root.Children.Add(weatherBox);
            root.Children.Add(Label("Cliffs (ground too steep to walk on)"));
            root.Children.Add(ledgesBox);
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var ok = new Button { Content = "OK", IsDefault = true, Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(12, 4, 12, 4) };
        ok.Click += (_, _) => Apply();
        cancel.Click += (_, _) => this.DialogResult = false;     // (WPF closed a dialog by its IsCancel button by itself; Avalonia only clicks it)
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        foreach (var (c, n) in new (Control, string)[] { (nameBox, "Name"), (descriptionBox, "Description"), (weatherBox, "Weather"), (ledgesBox, "Cliffs") })
            AutomationProperties.SetName(c, n);

        nameBox.Text = map.Name;
        descriptionBox.Text = map.Description;
        weatherBox.SelectedIndex = map.Weather is { Length: > 0 } ? 1 : 0;
        ledgesBox.SelectedIndex = map.Ledges == "drop" ? 1 : 0;
    }

    private void Apply()
    {
        // (an empty Avalonia text box has a null Text, where WPF's was "")
        var name = (nameBox.Text ?? "").Trim();
        if (name.Length == 0) { MessageBox.Show(this, "The map needs a name.", Title, MessageBoxButton.OK, MessageBoxImage.Information); return; }
        map.Name = name;
        map.Description = (descriptionBox.Text ?? "").Trim();
        if (map.Kind == MapKind.Lba2Island)
        {
            map.Weather = weatherBox.SelectedItem is ComboBoxItem { Tag: string { Length: > 0 } w } ? w : null;
            map.Ledges = ledgesBox.SelectedItem is ComboBoxItem { Tag: string { Length: > 0 } l } ? l : null;
        }
        this.DialogResult = true;
    }
}
