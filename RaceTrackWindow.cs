using System.IO;
using System.Windows;
using System.Windows.Controls;
using LBAAssembler.Terrain;
using Microsoft.Win32;

namespace LBAAssembler;

// Tools > LBA2: race track: builds a proposed track (levelled, banked road with the retail track's own textures, pit lane, start gantry,
// and a bridge or a jump where the lap crosses itself) into the LBA2 game folder, or puts the folder back as it was. The tracks of any of
// the islands, built together (each one ticked): the Desert island's (with a jump, and the retail track's leftovers cleared), Citadel
// Island's two (its storm track in CITADEL, with its own jump, and its town circuit in CITABAU, with a bridge: one tick, as they share the
// island's scenes), Mosquibees Island's mountain lap (whose plan draws its own bridge and jump), Celebration Island's lap round the statue
// (a raised road on piers, all of it its plan's) and its lava lake's (round the whole island from the same dock, raised over the crater with three
// jumps, and a ramp back down to the dock), or the Elevator Platform's rollercoaster (all of it raised road, banked, with the slopes
// pulling at the car). What carries each lap over itself is the island's own (RaceTrackIsland.Crossing), and every track is drawn on its
// island's holomap picture. Play races the track of the island the editor has open (RaceTrackService.RaceFor).
internal sealed class RaceTrackWindow : Window
{
    private readonly string gameRoot;
    private readonly Action changed;
    // one box for each island: those ticked are built, together (Citadel Island's box builds both its tracks)
    private readonly List<CheckBox> islandChecks = new();
    private readonly RadioButton builtInPlan = new() { Content = "The track built into the program", IsChecked = true };
    private readonly RadioButton filePlan = new() { Content = "A plan file:" };
    private readonly TextBox planPath = new() { Padding = new Thickness(3), IsEnabled = false };
    private readonly Button browseButton = new() { Content = "Browse…", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(6, 0, 0, 0), IsEnabled = false };
    private readonly CheckBox removeActors = new() { Content = "Remove the actors of the island's outside scenes, except Twinsen, the buggy and those the travel cutscenes need", IsChecked = true, ToolTip = "The engine's hidden Zoe placeholder in slot 1 stays (the buggy needs that slot), and so do the ferry, the Dino-Fly and the actors Twinsen's own script waits on in the cutscenes of arriving, leaving and the game's ending." };
    private readonly CheckBox buggyAlways = new() { Content = "The buggy is there from the start of any game (skip the car quest)", IsChecked = true, ToolTip = "The island's scenes delete the buggy until game variable 74 reaches 3; this makes that test always pass" };
    private readonly CheckBox startAtLine = new() { Content = "Twinsen and the buggy start on the grid", IsChecked = true };
    private readonly CheckBox roadZones = new() { Content = "Remove zones that would act on a car on the road (doors, hit, ladder, escalator, grid, rail)", IsChecked = true };
    private readonly CheckBox trackCameras = new() { Content = "Remove the fixed camera angles along the track (the view keeps following the car)", IsChecked = true, ToolTip = "Camera zones (type 1) that reach the road or come within a few cells of it" };
    private readonly CheckBox story = new() { Content = "Story: the rain, the Weather Wizard, Raph's time to beat, the aliens' new track and Mr. Paul's ferry ticket (Citadel Island)", IsChecked = true, ToolTip = "Zoe sends Twinsen to the Weather Wizard; Raph won't let him up the lighthouse until Twinsen beats his time on the storm track, where Mr. Paul wants racing gloves (in the attic, where the darts lay); then the game's own spell and the aliens, who build the town circuit for the next day: Twinsen sleeps in his bed, races Raph, Zoe, Mr. Paul, the Tralu and the thief, and a win is a ferry ticket. Played as a game: Play with the race car setup's new game." };
    private readonly TextBox log = new() { IsReadOnly = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 11, Height = 190, TextWrapping = TextWrapping.NoWrap };
    private readonly Button buildButton = new() { Content = "Build the track", Padding = new Thickness(18, 5, 18, 5), IsDefault = true };
    private readonly Button restoreButton = new() { Content = "Put the original files back", Padding = new Thickness(14, 5, 14, 5) };
    private readonly Button closeButton = new() { Content = "Close", Padding = new Thickness(14, 5, 14, 5), IsCancel = true };
    private readonly Button carButton = new() { Content = "Race car setup…", Padding = new Thickness(14, 5, 14, 5), ToolTip = "The buggy's gears, acceleration, brakes and steering when you play a folder with a race track built" };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    public RaceTrackWindow(string gameRoot, Action changed)
    {
        this.gameRoot = gameRoot;
        this.changed = changed;
        Title = "LBA2 race track";
        Width = 760; SizeToContent = SizeToContent.Height; MinWidth = 640;
        // (no taller than the screen: the window scrolls instead)
        MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        SetResourceReference(BackgroundProperty, "ThemeWindowBrush");
        SetResourceReference(ForegroundProperty, "ThemeTextBrush");
        // the islands the folder has tracks on, ticked (building again keeps them), else the Desert island's; Citadel Island once, for both
        // its tracks (its entry that races the town circuit when neither of its files is open: the one that carries the opponents and the story)
        var builtOn = RaceTrackService.BuiltIslands(gameRoot).Select(i => i.IleFile).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (island, label, tip) in Listed)
        {
            var box = new CheckBox { Content = label, Tag = island, ToolTip = tip, IsChecked = builtOn.Count == 0 ? island == RaceTrackIsland.Desert : builtOn.Contains(island.IleFile), Margin = new Thickness(0, 2, 0, 2) };
            box.Checked += (_, _) => IslandChanged();
            box.Unchecked += (_, _) => IslandChanged();
            islandChecks.Add(box);
        }
        BuildLayout();
        builtInPlan.Checked += (_, _) => PlanChoiceChanged();
        filePlan.Checked += (_, _) => PlanChoiceChanged();
        browseButton.Click += (_, _) => Browse();
        buildButton.Click += async (_, _) => await BuildAsync();
        restoreButton.Click += (_, _) => Restore();
        carButton.Click += (_, _) => new RaceCarWindow(forPlay: false, gameRoot) { Owner = this }.ShowDialog();
        IslandChanged();
        UpdateStatus();
    }

    // The islands the window lists, two to a row, each with what its track is: the box's label and its tooltip.
    private static readonly (RaceTrackIsland Island, string Label, string Tip)[] Listed =
    {
        (RaceTrackIsland.Desert, "Desert island", "A jump where the lap crosses itself; what is left of the original race track (its road paint, start gantry, arch and billboard in cube 7,10) is cleared"),
        (RaceTrackIsland.Mosquibe, "Mosquibees Island", "Round the mountain: its plan draws its own bridge and jump"),
        (RaceTrackIsland.Citadel, "Citadel Island: the storm track and the town circuit", "Both of the island's tracks: the storm track in CITADEL.ILE (in the rain, with its own jump) and the town circuit in CITABAU.ILE (once the storm is over, with a bridge where it crosses itself). Play races the one of the file the editor has open."),
        (RaceTrackIsland.Celebration, "Celebration Island: round the statue", "A raised road on piers up round the statue: its plan draws it all"),
        (RaceTrackIsland.CelebrationLava, "Celebration Island: the lava lake", "Before the statue rises: from the statue track's dock round the whole island -- along the north shore, up the east coast to the crater's rim, across the lava lake on a causeway, behind the temple -- with three jumps over gaps in the raised road, a wide last turn and a ramp back down to the dock. It races in a scene of its own (223, a copy of the island's scene 95) and Play races it with CELEBRAT.ILE open"),
        (RaceTrackIsland.Elevator, "The Elevator Platform: the rollercoaster", "All of it a raised road, banked, with the slopes pulling at the car: its plan draws it all"),
        (RaceTrackIsland.Moon, "The old moon (MOON.ILE): two vertical loops", "The Emerald Moon's older copy, which the game never loads: a lap round the moon base on the crater's floor through two vertical loops -- one with a gap at its top the car has to leap upside down, and one it drives round, both steered upside down too and both fallen off if taken too slowly. It races in scenes of its own (225-228, copies of the Emerald Moon's 74-77), the opponents round the loops with you"),
        (RaceTrackIsland.Emerald, "The Emerald Moon: over the reactor", "From the user's sketch, all of it a raised road: along the moon base's roof (the straight, a pit lane beside it), up onto the crater's rim and round it through three vertical loops twice the road's width, and one jump over the whole reactor -- a curved ramp, a flight over its dish 20,000 up and a curved hill down, the road as wide as the dish there and banked hard through the turns either side. It races in scenes of its own (229-232, copies of the moon's 74-77), the opponents waiting in the pit lane while you qualify"),
        (RaceTrackIsland.Sendell, "Sendell's Well: the cut island", "Island 1, which the game never shipped: the build makes it (SENDELL.ILE, a round island with a well in its middle, made from Citadel Island's files) and a scene for it (224), and a lap round the well, up from the beach onto the plateau and back down. Putting the folder back deletes the island's files"),
    };

    // The islands ticked, in the order the list has them: the order they are built in, the first the one Play races when the editor has none
    // of their islands open.
    private List<RaceTrackIsland> Islands() => islandChecks.Where(c => c.IsChecked == true).Select(c => (RaceTrackIsland)c.Tag).ToList();

    // What only one island has: the story is Citadel Island's; and a plan file is one island's track.
    private void IslandChanged()
    {
        var islands = Islands();
        // the story is Citadel Island's own opening, and the souvenir seller's race on Celebration Island's lava lake
        var citadel = islands.Any(i => i.IleFile == RaceTrackIsland.Citadel.IleFile || i == RaceTrackIsland.CelebrationLava);
        if (story.IsEnabled != citadel) story.IsChecked = citadel;
        story.IsEnabled = citadel;
        var one = islands.Count == 1;
        filePlan.IsEnabled = one;
        filePlan.ToolTip = one ? null : "A plan file is one island's track: tick that island alone";
        if (!one && filePlan.IsChecked == true) builtInPlan.IsChecked = true;
        var tracks = islands.Sum(i => i.Tracks);
        buildButton.Content = tracks > 1 ? $"Build the {tracks} tracks" : "Build the track";
        buildButton.IsEnabled = islands.Count > 0;
        UpdateStatus();
    }

    private void BuildLayout()
    {
        foreach (var t in new TextBlock[] { status }) t.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        foreach (var box in new TextBox[] { planPath, log })
        {
            box.SetResourceReference(BackgroundProperty, "ThemeFieldBrush");
            box.SetResourceReference(ForegroundProperty, "ThemeTextBrush");
            box.SetResourceReference(BorderBrushProperty, "ThemeBorderBrush");
        }
        foreach (var c in new Control[] { builtInPlan, filePlan, removeActors, buggyAlways, startAtLine, roadZones, trackCameras, story }) c.SetResourceReference(ForegroundProperty, "ThemeTextBrush");

        var root = new StackPanel { Margin = new Thickness(16) };
        var intro = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = "Builds proposed race tracks on the islands ticked below, one or several at once: the ground under the road is levelled and banked, the road is painted with the retail " +
                   "track's asphalt, red and white curbs, arrows and red/gold hatching, with a pit lane and a start gantry, and drawn on the island's holomap picture. " +
                   "Where a lap crosses itself, the Desert island's jumps (and what is left of the original race track there is cleared) and Citadel Island's " +
                   "town circuit has a bridge; the other tracks' plans draw their own. On an island other than the Desert one the road's tiles are copied " +
                   "into its own spare texture space and matched to its palette, and its scenes are given a buggy.\n\n" +
                   "It changes the island's ground and decor bodies and SCENE.HQR in the LBA2 game folder (and, for a jump, ANIM.HQR and RESS.HQR: its flight; BODY.HQR and RESS.HQR for the cars made after the game's characters: Baldino's, and fifty-eight more, three or more for each island). The first time, the originals are kept beside them (as *" + RaceTrackService.BackupSuffix +
                   "); every build starts from those copies, and the button below puts them back. When you play a folder with a race track built, the game " +
                   "runs in its race-track mode: the car setup below (gears on X and Z, brakes, steering), the car staying level on the bridge, a checkpoint in the middle of each corner, " +
                   "the track's own drivers in the cars made after them (Moya, the Dino-Fly, the Dean, the racer and Baldino on the Desert island; Raph, Zoe, Mr. Paul, the Tralu and " +
                   "the thief on Citadel Island's town circuit, and Raph's time to beat on its storm track; the Queen and the monkey monster on Mosquibees Island; Baldino in his lander on the " +
                   "Emerald Moon; the retail track's racer, Baldino and the biker elsewhere), and the gear, speed, lap times and position on screen. " +
                   "Celebration Island's lap winds up round the statue on a raised road and comes back down a bridge: only that race-track mode can drive it " +
                   "(it is the statue's island there, whatever the story has reached, and the camera follows the car). Its lava lake's lap is the island " +
                   "before the statue rises, in a scene of its own: round the whole island on a raised road from the same dock, with three jumps and a ramp back down to the dock. " +
                   "The Elevator Platform's is a rollercoaster: a helix up round the elevator's tower, a drop, hills and banked bends, all of it in the air, " +
                   "where the slopes slow the car and speed it up and the camera rides the road behind it -- also that mode's alone. " +
                   "Sendell's Well is the island the game never shipped: the build makes it (SENDELL.ILE and SENDELL.OBL, its sky, palette and holomap " +
                   "picture, and scene 224), and putting the folder back deletes it. " +
                   "The old moon (MOON.ILE, the Emerald Moon's older copy, which the game never loads) has two vertical loops, that mode's too: driven " +
                   "round and steered upside down, one whole -- too slow and the car falls off -- and one with a cut-out at its top, leapt upside down. " +
                   "The Emerald Moon's lap (EMERAUDE.ILE, in scenes of its own, 229-232) is all in the air too: along the moon base's roof, round the " +
                   "crater's rim through three loops, and over the whole reactor in one jump that mode carries the car over, into the next cube. " +
                   "Use Tools > Test edits first to try it on a scratch copy of the game folder.",
        };
        intro.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        root.Children.Add(intro);
        root.Children.Add(new TextBlock { Text = $"Game folder: {gameRoot}", Margin = new Thickness(0, 8, 0, 8), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Foreground = (System.Windows.Media.Brush)FindResource("ThemeTextBrush") });

        root.Children.Add(Section("The islands"));
        var islandsHint = new TextBlock { Text = "The islands ticked get their tracks, built together. Play races the one of the island the editor has open (for Citadel Island: CITADEL.ILE the storm track, CITABAU.ILE the town circuit).", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        islandsHint.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        root.Children.Add(islandsHint);
        var islandGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (var c in islandChecks) { c.SetResourceReference(ForegroundProperty, "ThemeTextBrush"); islandGrid.Children.Add(c); }
        root.Children.Add(islandGrid);

        root.Children.Add(Section("The track"));
        root.Children.Add(builtInPlan);
        var fileRow = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        DockPanel.SetDock(browseButton, Dock.Right);
        fileRow.Children.Add(filePlan); fileRow.Children.Add(browseButton); fileRow.Children.Add(planPath);
        filePlan.Margin = new Thickness(0, 0, 8, 0);
        root.Children.Add(fileRow);

        root.Children.Add(Section("The scenes"));
        foreach (var c in new CheckBox[] { removeActors, buggyAlways, startAtLine, roadZones, trackCameras, story }) { c.Margin = new Thickness(0, 2, 0, 2); root.Children.Add(c); }

        root.Children.Add(status);
        log.Margin = new Thickness(0, 10, 0, 0);
        root.Children.Add(log);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        restoreButton.Margin = new Thickness(0, 0, 10, 0); buildButton.Margin = new Thickness(0, 0, 10, 0); carButton.Margin = new Thickness(0, 0, 10, 0);
        buttons.Children.Add(carButton); buttons.Children.Add(restoreButton); buttons.Children.Add(buildButton); buttons.Children.Add(closeButton);
        root.Children.Add(buttons);
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = root };
    }

    private TextBlock Section(string text)
    {
        var t = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) };
        t.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        return t;
    }

    private void PlanChoiceChanged()
    {
        var file = filePlan.IsChecked == true;
        planPath.IsEnabled = file; browseButton.IsEnabled = file;
    }

    private void Browse()
    {
        var dialog = new OpenFileDialog { Title = "Race track plan", Filter = "Race track plan (*.json)|*.json|All files|*.*" };
        if (dialog.ShowDialog(this) == true) planPath.Text = dialog.FileName;
    }

    private void UpdateStatus()
    {
        var has = RaceTrackService.HasBackups(gameRoot);
        restoreButton.IsEnabled = has;
        var built = has ? RaceTrackService.BuiltIslands(gameRoot) : new();
        var count = built.Sum(i => i.Tracks);
        status.Text = !has ? "No race track is built in this folder yet."
            : count > 1 ? $"This folder has {count} race tracks built: {string.Join(", ", built.Select(i => i.Built))}. The originals are kept."
            : $"A race track is built in this folder: {built.FirstOrDefault()?.Built ?? "the Desert island's"}. The originals are kept.";
    }

    // The options an island's track is built with (each track its own: the build fills them in): its crossing, the Desert island's retail
    // track cleared and the holomap picture drawn on as always (RaceTrackOptions.For), and the scene choices.
    private RaceTrackOptions Options(RaceTrackIsland island)
    {
        var options = RaceTrackOptions.For(island);
        options.RemoveActors = removeActors.IsChecked == true;
        options.BuggyAlways = buggyAlways.IsChecked == true;
        options.StartAtLine = startAtLine.IsChecked == true;
        options.RemoveRoadZones = roadZones.IsChecked == true;
        options.RemoveTrackCameras = trackCameras.IsChecked == true;
        options.DrawOnHolomap = !island.NoHolomap;
        options.Story = story.IsChecked == true && island.IleFile == RaceTrackIsland.Citadel.IleFile;
        return options;
    }

    private async Task BuildAsync()
    {
        var islands = Islands();
        if (islands.Count == 0) return;
        var tracks = new List<RaceTrackService.TrackBuild>();
        try
        {
            foreach (var island in islands)
            {
                var plan = RaceTrackPlan.Built(island);
                // (a plan file is the track of the one island ticked: for Citadel Island, its town circuit's -- the fine-weather file's -- the storm track built in)
                RaceTrackPlan? twinPlan = null;
                if (filePlan.IsChecked == true && islands.Count == 1)
                {
                    if (island.RacesTwin) twinPlan = RaceTrackPlan.Load(planPath.Text.Trim());
                    else plan = RaceTrackPlan.Load(planPath.Text.Trim());
                }
                tracks.Add(new RaceTrackService.TrackBuild(plan, Options(island), twinPlan));
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, $"The plan can't be read: {error.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var files = islands.SelectMany(RaceTrackService.FilesFor).Distinct();
        // (a track built before on an island left unticked goes: every build starts from the originals)
        var gone = RaceTrackService.BuiltIslands(gameRoot).Where(b => !islands.Any(i => i.IleFile == b.IleFile)).Select(b => b.IleFile == RaceTrackIsland.Citadel.IleFile ? "Citadel Island" : b.Name).Distinct().ToList();
        var answer = MessageBox.Show(this,
            $"Build {string.Join(", ", islands.Select(i => Listed.FirstOrDefault(l => l.Island == i).Label ?? i.Shown))} into\n{gameRoot}\n\n{string.Join(", ", files)} will change. " +
            (RaceTrackService.HasBackups(gameRoot) ? "The originals kept by the first build are used again." : "The originals are kept as *" + RaceTrackService.BackupSuffix + ".") +
            (gone.Count > 0 ? $"\n\nThe track{(gone.Count > 1 ? "s" : "")} built before on {string.Join(" and ", gone)} will be taken out: tick {(gone.Count > 1 ? "them" : "it")} to keep {(gone.Count > 1 ? "them" : "it")}." : ""),
            Title, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;
        buildButton.IsEnabled = false; restoreButton.IsEnabled = false;
        log.Text = islands.Sum(i => i.Tracks) > 1 ? $"Building {islands.Sum(i => i.Tracks)} tracks…" : "Building…";
        var root = gameRoot;
        var result = await Task.Run(() => RaceTrackService.Build(root, tracks));
        buildButton.IsEnabled = true;
        log.Text = string.Join("\n", result.Log);
        status.Text = result.Summary;
        UpdateStatus();
        if (result.Ok) changed();
    }

    private void Restore()
    {
        var answer = MessageBox.Show(this, "Put the original files back (the island's ground and decor bodies, SCENE.HQR and the others the build changed)? Anything else changed in them since the track was built is lost.", Title, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;
        try
        {
            var text = RaceTrackService.Restore(gameRoot);
            log.Text = text; status.Text = text;
            UpdateStatus();
            changed();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Couldn't put the files back: {error.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
