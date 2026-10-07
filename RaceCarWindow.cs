using System.Windows;
using System.Windows.Controls;

namespace LBAAssembler;

// The race car's setup (RaceCarSetup): the buggy's gears, acceleration, brakes, steering and the on-screen display when Play runs an LBA2 folder with a
// race track built. Shown before such a play (forPlay: Play / Cancel), or from the race track dialog (Save / Cancel). Saved in the settings.
internal sealed class RaceCarWindow : Window
{
    private readonly RaceCarSetup setup = EditorSettings.Current.RaceCar.Clone();
    private readonly ComboBox preset = new() { MinWidth = 320 };
    private readonly ComboBox gears = new() { Width = 70 };
    // the car Twinsen drives: his buggy, or one of the opponents' cars the folder has (RaceCarSetup.DriveAs)
    private readonly ComboBox driveAs = new() { MinWidth = 320 };
    private readonly StackPanel gearRows = new();
    private readonly List<(FrameworkElement Row, Slider Slider)> gearSliders = new();
    private readonly CheckBox automatic = new() { Content = "Automatic gearbox (the gears change by themselves)" };
    private readonly CheckBox display = new() { Content = "Show the gear, the speed and the lap times on screen" };
    private readonly CheckBox askBeforePlay = new() { Content = "Show this before each race-track play" };
    private readonly CheckBox opponent = new() { Content = "Race the opponents" };
    // the track's drivers, each raced unless left out (RaceCarSetup.LeftOut)
    private readonly WrapPanel drivers = new() { Margin = new Thickness(18, 2, 0, 0) };
    private readonly CheckBox newGame = new() { Content = "Play the story from a new game, every track raced where and when the game is", ToolTip = "Twinsen starts in his house. Citadel Island's storm track is raced in the rain, its town circuit once the storm is over and Twinsen has slept; each other island's track on its island." };
    private readonly CheckBox fightBack = new() { Content = "They push harder when they fall behind you" };
    private readonly CheckBox penguinsWalk = new() { Content = "Penguins walk the track until a car comes near (off: they go off a second after they are dropped)", Margin = new Thickness(18, 2, 0, 0) };
    private readonly CheckBox powerUps = new() { Content = "Power-ups in the mushrooms along the track", ToolTip = "Rows of small brown mushrooms across the road, each with a power-up inside: Gazogem fuel, the protection spell, the lightning spell, a nitro penguin (it goes off at whatever it runs into), the super jet-pack (the car turns into the jet-pack and the game drives it at twice the speed, through or past any car in its way) or oil (whoever drives over it skids). A car takes one from a row; the cars at the back get the super jet-pack and lightning, the ones in front penguins and oil. They grow back. Yours go into the item box at the top left (two slots, a roulette picks each): Shift uses the selected one, Q selects the other." };
    private readonly CheckBox qualifying = new() { Content = "Drive a qualifying lap first: the times set the grid" };
    private readonly CheckBox showCheckpoints = new() { Content = "Show the checkpoints as red lines across the road (for testing)", ToolTip = "One checkpoint in the middle of every corner, reaching a little past the road's edges: a lap counts once you have crossed them all, so cutting a corner doesn't pay, and running wide or overtaking on the edge still counts." };
    private readonly CheckBox fineWeather = new() { Content = "Stop the rain on Citadel Island (the weather after the lighthouse, when the aliens land)", ToolTip = "No rain or thunder, the brighter island with its own light and sky. Only the weather changes: the story stays where it is." };
    // Citadel Island built with a track in each of its files: the weather is the raced track's (the town circuit's is the fine weather's,
    // the storm track's the rain's) -- the one of the island file open in the editor (`track`, RaceTrackService.RaceFor), or else the one
    // the race track window built it to race -- null for any other track, where the setup says
    private readonly bool? trackWeather;
    private readonly CheckBox startAtLine = new() { Content = "Start beside the car on the start/finish straight, with the editor's markings hidden" };
    private readonly List<Action> refresh = new();
    private readonly TabControl tabs = new() { Margin = new Thickness(0, 0, 0, 0) };
    // the tab last shown, for the next time the window opens (this session)
    private static int lastTab;
    private bool updating;

    public RaceCarWindow(bool forPlay, string? gameDirectory = null, Terrain.RaceTrackService.TrackInfo? track = null)
    {
        Title = "Race car setup";
        if (gameDirectory is not null && (track ?? Terrain.RaceTrackService.ReadInfo(gameDirectory)) is { Twin: not null } info)
        {
            trackWeather = Terrain.RaceTrackService.FineWeather(info, setup.FineWeather);
            fineWeather.IsEnabled = false;
            fineWeather.Content = trackWeather == true
                ? "Citadel Island without the rain: its town circuit is raced"
                : "Citadel Island in the rain: its storm track is raced";
            fineWeather.ToolTip = "This folder has a Citadel Island track in each weather: Play races the one of the island file open in the editor (CITADEL.ILE the storm track, CITABAU.ILE the town circuit), in its own weather.";
        }
        Width = 640; SizeToContent = SizeToContent.Height; MinWidth = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        SetResourceReference(BackgroundProperty, "ThemeWindowBrush");
        SetResourceReference(ForegroundProperty, "ThemeTextBrush");

        // (2026-10-06: in tabs -- it had grown taller than the screen as one column)
        var root = new StackPanel { Margin = new Thickness(16) };
        root.Children.Add(Text("How the buggy drives when you play a game folder that has a race track built; other game folders play the game as it is.",
                               new Thickness(0, 0, 0, 8)));
        var car = Page(); var gearbox = Page(); var race = Page(); var play = Page();

        // ---- the car: which one, a preset, its handling
        car.Children.Add(Section("Your car", first: true));
        car.Children.Add(Text("Drive the track in Twinsen's buggy, or in any of the opponents' cars the folder has -- to try a track in it. " +
                              "It handles as this setup makes the car.", new Thickness(0, 0, 0, 4)));
        foreach (var (generic, name) in DrivableCars(gameDirectory)) driveAs.Items.Add(new ComboBoxItem { Content = name, Tag = generic });
        driveAs.SelectionChanged += (_, _) => { if (!updating && driveAs.SelectedItem is ComboBoxItem { Tag: int generic }) setup.DriveAs = generic; };
        car.Children.Add(driveAs);

        car.Children.Add(Section("Start from"));
        foreach (var p in RaceCarSetup.Presets) preset.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p });
        preset.Items.Add(new ComboBoxItem { Content = "Custom" });
        preset.SelectionChanged += (_, _) => PresetChosen();
        car.Children.Add(preset);

        car.Children.Add(Section("Handling (the original buggy is 100 %)"));
        car.Children.Add(SliderRow("Acceleration", 25, 300, 5, " %", () => setup.AccelerationPercent, v => setup.AccelerationPercent = (int)v).Row);
        car.Children.Add(SliderRow("Braking", 25, 300, 5, " %", () => setup.BrakingPercent, v => setup.BrakingPercent = (int)v).Row);
        car.Children.Add(SliderRow("Rolling to a stop (off the throttle)", 0, 300, 5, " %", () => setup.CoastingPercent, v => setup.CoastingPercent = (int)v).Row);
        car.Children.Add(SliderRow("Steering", 50, 200, 5, " %", () => setup.SteeringPercent, v => setup.SteeringPercent = (int)v).Row);
        car.Children.Add(SliderRow("Top speed backwards", 5, 40, 1, " km/h", () => setup.ReverseKmh, v => setup.ReverseKmh = (int)v).Row);

        // ---- the gearbox
        gearbox.Children.Add(Section("Gearbox", first: true));
        gearbox.Children.Add(Text("X shifts up a gear and Z down, unless the gearbox is automatic. Each gear has its own top speed, and a low gear pulls " +
                                  "harder than a high one. Speeds are as the game's display shows them, a cell taken as a metre: the original buggy " +
                                  "tops out at 27 km/h.", new Thickness(0, 0, 0, 6)));
        for (var g = 1; g <= RaceCarSetup.MaxGears; g++) gears.Items.Add(g);
        gears.SelectionChanged += (_, _) => { if (updating || gears.SelectedItem is not int count) return; setup.Gears = count; ShowGearRows(); Edited(); };
        var gearCount = new StackPanel { Orientation = Orientation.Horizontal };
        gearCount.Children.Add(Text("Gears:", new Thickness(0, 0, 8, 0)));
        gearCount.Children.Add(gears);
        gearbox.Children.Add(gearCount);
        for (var g = 0; g < RaceCarSetup.MaxGears; g++)
        {
            var gear = g;
            var (row, slider) = SliderRow($"Gear {g + 1} top speed", 5, 80, 1, " km/h", () => setup.TopKmh(gear), v =>
            {
                while (setup.GearTopKmh.Count <= gear) setup.GearTopKmh.Add(setup.GearTopKmh.LastOrDefault(27));
                setup.GearTopKmh[gear] = (int)v;
            });
            gearSliders.Add((row, slider));
            gearRows.Children.Add(row);
        }
        gearbox.Children.Add(gearRows);
        automatic.Checked += (_, _) => { setup.Automatic = true; Edited(); };
        automatic.Unchecked += (_, _) => { setup.Automatic = false; Edited(); };
        automatic.Margin = new Thickness(0, 4, 0, 0);
        gearbox.Children.Add(automatic);

        // ---- the opponents and the race
        race.Children.Add(Section("Opponents", first: true));
        race.Children.Add(Text("The opponents -- each track's own drivers in the cars made after them, or the original track's racer, Baldino and the motorbike " +
                               "Rabbibunny -- drive this car, set up as on the other tabs, on racing lines of their own: 100 % skill drives it perfectly, faster than " +
                               "you can; a little less gives a close race.", new Thickness(0, 0, 0, 4)));
        opponent.Checked += (_, _) => setup.Opponent = true;
        opponent.Unchecked += (_, _) => setup.Opponent = false;
        race.Children.Add(opponent);
        // (the drivers of the track Play races: each one's own box)
        var raced = gameDirectory is null ? null : track ?? Terrain.RaceTrackService.ReadInfo(gameDirectory);
        foreach (var name in DriverNames(raced is null ? null : Terrain.RaceTrackService.Raced(raced)))
        {
            var box = new CheckBox { Content = name, Margin = new Thickness(0, 2, 14, 2), IsChecked = !setup.LeftOut.Contains(name) };
            box.Checked += (_, _) => setup.LeftOut.Remove(name);
            box.Unchecked += (_, _) => { if (!setup.LeftOut.Contains(name)) setup.LeftOut.Add(name); };
            box.SetResourceReference(ForegroundProperty, "ThemeTextBrush");
            drivers.Children.Add(box);
        }
        if (drivers.Children.Count > 0) race.Children.Add(drivers);
        race.Children.Add(SliderRow("The one to beat's skill", 50, 120, 1, " %", () => setup.MainSkill, v => setup.MainSkill = (int)v).Row);
        race.Children.Add(Text($"Each track has one opponent you have to beat; the others' skills are drawn at random each race, from {Terrain.RaceCarEngineFile.RandomBelow} points under this to {Terrain.RaceCarEngineFile.RandomAbove} over it.", new Thickness(0, 0, 0, 4)));
        fightBack.Checked += (_, _) => setup.OpponentsFightBack = true;
        fightBack.Unchecked += (_, _) => setup.OpponentsFightBack = false;
        fightBack.Margin = new Thickness(0, 4, 0, 0);
        race.Children.Add(fightBack);

        race.Children.Add(Section("The race"));
        race.Children.Add(Text("The race starts with a count-down from the grid. A lap counts once you have crossed every checkpoint (one in the middle of " +
                               "each corner) and the start line again.", new Thickness(0, 0, 0, 4)));
        qualifying.Checked += (_, _) => setup.Qualifying = true;
        qualifying.Unchecked += (_, _) => setup.Qualifying = false;
        race.Children.Add(qualifying);
        powerUps.Checked += (_, _) => setup.PowerUps = true;
        powerUps.Unchecked += (_, _) => setup.PowerUps = false;
        powerUps.Margin = new Thickness(0, 4, 0, 0);
        race.Children.Add(powerUps);
        penguinsWalk.Checked += (_, _) => setup.PenguinsWalk = true;
        penguinsWalk.Unchecked += (_, _) => setup.PenguinsWalk = false;
        race.Children.Add(penguinsWalk);

        // ---- how Play starts, and what is on screen
        play.Children.Add(Section("Starting", first: true));
        startAtLine.Checked += (_, _) => setup.StartAtLine = true;
        startAtLine.Unchecked += (_, _) => setup.StartAtLine = false;
        play.Children.Add(startAtLine);
        newGame.Checked += (_, _) => setup.NewGame = true;
        newGame.Unchecked += (_, _) => setup.NewGame = false;
        newGame.Margin = new Thickness(0, 4, 0, 0);
        play.Children.Add(newGame);
        fineWeather.Checked += (_, _) => { if (trackWeather is null) setup.FineWeather = true; };
        fineWeather.Unchecked += (_, _) => { if (trackWeather is null) setup.FineWeather = false; };
        fineWeather.Margin = new Thickness(0, 4, 0, 0);
        play.Children.Add(fineWeather);

        play.Children.Add(Section("On screen"));
        display.Checked += (_, _) => setup.ShowDisplay = true;
        display.Unchecked += (_, _) => setup.ShowDisplay = false;
        askBeforePlay.Checked += (_, _) => setup.AskBeforePlay = true;
        askBeforePlay.Unchecked += (_, _) => setup.AskBeforePlay = false;
        play.Children.Add(display);
        showCheckpoints.Checked += (_, _) => setup.ShowCheckpoints = true;
        showCheckpoints.Unchecked += (_, _) => setup.ShowCheckpoints = false;
        showCheckpoints.Margin = new Thickness(0, 4, 0, 0);
        play.Children.Add(showCheckpoints);
        askBeforePlay.Margin = new Thickness(0, 4, 0, 0);
        play.Children.Add(askBeforePlay);
        foreach (var c in new Control[] { automatic, display, showCheckpoints, askBeforePlay, opponent, fightBack, powerUps, penguinsWalk, qualifying, fineWeather, startAtLine, newGame }) c.SetResourceReference(ForegroundProperty, "ThemeTextBrush");

        foreach (var (header, page) in new[] { ("Car", car), ("Gearbox", gearbox), ("Opponents", race), ("Play", play) })
            tabs.Items.Add(new TabItem
            {
                Header = header,
                Content = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
            });
        tabs.SelectedIndex = Math.Clamp(lastTab, 0, tabs.Items.Count - 1);
        tabs.SelectionChanged += (_, e) => { if (ReferenceEquals(e.OriginalSource, tabs)) lastTab = tabs.SelectedIndex; };
        root.Children.Add(tabs);
        // (every tab as tall as the tallest, so the window keeps its size from tab to tab -- as tall as the screen allows, and scrolled past that)
        Loaded += (_, _) => EvenTabs();

        var ok = new Button { Content = forPlay ? "Play" : "Save", Padding = new Thickness(18, 5, 18, 5), IsDefault = true, Margin = new Thickness(0, 0, 10, 0) };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 5, 14, 5), IsCancel = true };
        ok.Click += (_, _) => Accept();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        Content = root;
        Load();
    }

    private void Load()
    {
        updating = true;
        gears.SelectedItem = Math.Clamp(setup.Gears, 1, RaceCarSetup.MaxGears);
        automatic.IsChecked = setup.Automatic;
        display.IsChecked = setup.ShowDisplay;
        showCheckpoints.IsChecked = setup.ShowCheckpoints;
        askBeforePlay.IsChecked = setup.AskBeforePlay;
        opponent.IsChecked = setup.Opponent;
        newGame.IsChecked = setup.NewGame;
        powerUps.IsChecked = setup.PowerUps;
        penguinsWalk.IsChecked = setup.PenguinsWalk;
        fightBack.IsChecked = setup.OpponentsFightBack;
        qualifying.IsChecked = setup.Qualifying;
        fineWeather.IsChecked = trackWeather ?? setup.FineWeather;
        startAtLine.IsChecked = setup.StartAtLine;
        driveAs.SelectedItem = driveAs.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag is int g && g == setup.DriveAs) ?? driveAs.Items.OfType<ComboBoxItem>().FirstOrDefault();
        foreach (var r in refresh) r();
        ShowGearRows();
        preset.SelectedIndex = MatchingPreset();
        updating = false;
    }

    // The cars Twinsen can drive (RaceCarSetup.DriveAs): his buggy, and the racer entity's cars the folder's entity table has -- the
    // retail racer's, Baldino's rocket car and the cars made after the characters (Terrain.RaceTrackCharacterCars) -- by name.
    private static List<(int Generic, string Name)> DrivableCars(string? gameDirectory)
    {
        var list = new List<(int, string)> { (-1, "Twinsen's buggy") };
        // (a folder built since 2026-10-05: every car by its number, the cast's too -- RACECARS.JSON)
        if (gameDirectory is not null && Terrain.RaceTrackCharacterCars.Catalogue(gameDirectory) is { Count: > 0 } listed)
        {
            list.AddRange(listed.Select(e => (e.Number, $"{e.Number}: {e.Name} ({e.Driver})")));
            return list;
        }
        var racer = gameDirectory is null ? null : Lba2EntityTable.Load(gameDirectory)?.Entities.FirstOrDefault(e => e.Id == Terrain.RaceTrackScenes.RacerEntity);
        if (racer is null) return list;
        var has = racer.Bodies.Select(b => b.Generic).ToHashSet();
        var names = new Dictionary<int, string> { [0] = "The racer's car (the original track's racer)", [1] = "Baldino's rocket car" };
        foreach (var c in Terrain.RaceTrackCharacterCars.All) names.TryAdd(c.Generic, $"{c.Name} ({c.Driver})");
        foreach (var (generic, name) in names.OrderBy(n => n.Key))
            if (has.Contains(generic)) list.Add((generic, $"{generic}: {name}"));
        return list;
    }

    // The drivers of a track that have a car (a time to beat has none): its line-up, or (a track built before the line-ups) the retail
    // track's racer and the others it was built with.
    private static IEnumerable<string> DriverNames(Terrain.RaceTrackService.TrackInfo? track)
    {
        if (track?.Drivers is { Count: > 0 } list) return list.Where(d => !d.Ghost).Select(d => d.Name);
        var names = new List<string>();
        if (track?.Opponent is { Count: > 0 }) names.Add(Terrain.RaceDriver.Racer.Name);
        names.AddRange((track?.Rivals ?? new()).Select(r => r.Name));
        return names;
    }

    // Which preset the setup is (the last item, Custom, when none).
    private int MatchingPreset()
    {
        for (var i = 0; i < RaceCarSetup.Presets.Count; i++)
        {
            var p = RaceCarSetup.Presets[i].Setup;
            if (p.Gears == setup.Gears && Enumerable.Range(0, setup.Gears).All(g => p.TopKmh(g) == setup.TopKmh(g)) && p.AccelerationPercent == setup.AccelerationPercent &&
                p.BrakingPercent == setup.BrakingPercent && p.CoastingPercent == setup.CoastingPercent && p.SteeringPercent == setup.SteeringPercent &&
                p.ReverseKmh == setup.ReverseKmh && p.Automatic == setup.Automatic)
                return i;
        }
        return RaceCarSetup.Presets.Count;
    }

    private void PresetChosen()
    {
        if (updating || preset.SelectedItem is not ComboBoxItem { Tag: RaceCarSetup.Preset p }) return;
        var chosen = p.Setup;
        setup.Gears = chosen.Gears; setup.GearTopKmh = new List<int>(chosen.GearTopKmh);
        setup.AccelerationPercent = chosen.AccelerationPercent; setup.BrakingPercent = chosen.BrakingPercent; setup.CoastingPercent = chosen.CoastingPercent;
        setup.SteeringPercent = chosen.SteeringPercent; setup.ReverseKmh = chosen.ReverseKmh; setup.Automatic = chosen.Automatic;
        Load();
    }

    // Any value changed by hand: the preset box says Custom unless the values happen to be a preset's.
    private void Edited()
    {
        if (updating) return;
        updating = true;
        preset.SelectedIndex = MatchingPreset();
        updating = false;
    }

    private void ShowGearRows()
    {
        for (var g = 0; g < gearSliders.Count; g++) gearSliders[g].Row.Visibility = g < setup.Gears ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Accept()
    {
        // the gears' top speeds from low to high
        for (var g = 1; g < setup.Gears; g++) if (setup.TopKmh(g) < setup.TopKmh(g - 1))
        {
            MessageBox.Show(this, $"Gear {g + 1} is slower than gear {g}. Each gear needs a higher top speed than the one below it.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        EditorSettings.Current.RaceCar = setup;
        try { EditorSettings.Current.Save(); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException) { DebugLog.Log($"RaceCarWindow: settings not saved: {error.Message}"); }
        DialogResult = true;
    }

    private (FrameworkElement Row, Slider Slider) SliderRow(string label, double min, double max, double step, string unit, Func<double> get, Action<double> set)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        var name = Text(label);
        var slider = new Slider { Minimum = min, Maximum = max, SmallChange = step, LargeChange = step * 5, TickFrequency = step, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
        var value = Text("", new Thickness(8, 0, 0, 0));
        Grid.SetColumn(slider, 1); Grid.SetColumn(value, 2);
        grid.Children.Add(name); grid.Children.Add(slider); grid.Children.Add(value);
        slider.ValueChanged += (_, e) =>
        {
            value.Text = $"{e.NewValue:0}{unit}";
            if (updating) return;
            set(e.NewValue);
            Edited();
        };
        refresh.Add(() => { slider.Value = Math.Clamp(get(), min, max); value.Text = $"{slider.Value:0}{unit}"; });
        return (grid, slider);
    }

    private TextBlock Text(string text, Thickness? margin = null)
    {
        var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = margin ?? new Thickness(0) };
        t.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        return t;
    }

    // A tab's page.
    private static StackPanel Page() => new() { Margin = new Thickness(10, 8, 10, 10) };

    // Every tab's page as tall as the tallest (each measured at the width the shown one has), up to what fits on the screen with the rest
    // of the window round it; a page taller than that scrolls.
    private void EvenTabs()
    {
        if (tabs.SelectedContent is not ScrollViewer shown) return;
        var width = shown.ActualWidth;
        double most = 0;
        // (the gearbox's page with every gear's row: choosing more gears doesn't make it scroll)
        foreach (var (row, _) in gearSliders) row.Visibility = Visibility.Visible;
        foreach (var item in tabs.Items.OfType<TabItem>())
            if (item.Content is ScrollViewer { Content: FrameworkElement page })
            {
                page.Measure(new Size(width, double.PositiveInfinity));
                most = Math.Max(most, page.DesiredSize.Height);
            }
        ShowGearRows();
        var room = SystemParameters.WorkArea.Height - (ActualHeight - shown.ActualHeight) - 24;
        foreach (var item in tabs.Items.OfType<TabItem>())
            if (item.Content is ScrollViewer viewer) viewer.Height = Math.Max(160, Math.Min(Math.Ceiling(most) + 2, room));
    }

    private TextBlock Section(string text, bool first = false)
    {
        var t = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, first ? 0 : 12, 0, 4) };
        t.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        return t;
    }
}
