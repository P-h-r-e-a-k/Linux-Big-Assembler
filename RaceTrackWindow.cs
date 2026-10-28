using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LBAAssembler.Terrain;

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
    // (the island a plan file is the track of, of those ticked -- a plan doesn't say which it is: the others are built with their own tracks;
    // and the island's built-in track saved as a plan file, to start one from. Until 2026-10-27 a plan file wanted its island ticked alone,
    // and with every island ticked the option was greyed with nothing to say why)
    private readonly ComboBox planIsland = new() { MinWidth = 260, Margin = new Thickness(6, 0, 0, 0), IsEnabled = false };
    private readonly Button sampleButton = new() { Content = "Sample…", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(6, 0, 0, 0),
        ToolTip = "Save a sample plan file -- the Desert island's lap, with a comment on every part saying what it is -- to see what a plan is, change and build from" };
    private readonly Button saveBuiltIn = new() { Content = "Save its built-in track as a plan file…", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(6, 0, 0, 0), IsEnabled = false,
        ToolTip = "The track built into the program for the island chosen, as a plan file to change and build from" };
    private readonly CheckBox removeActors = new() { Content = "Remove the actors of the island's outside scenes, except Twinsen, the buggy and those the travel cutscenes need", IsChecked = true, ToolTip = "The engine's hidden Zoe placeholder in slot 1 stays (the buggy needs that slot), and so do the ferry, the Dino-Fly and the actors Twinsen's own script waits on in the cutscenes of arriving, leaving and the game's ending." };
    private readonly CheckBox buggyAlways = new() { Content = "The buggy is there from the start of any game (skip the car quest)", IsChecked = true, ToolTip = "The island's scenes delete the buggy until game variable 74 reaches 3; this makes that test always pass" };
    private readonly CheckBox startAtLine = new() { Content = "Twinsen and the buggy start on the grid", IsChecked = true };
    private readonly CheckBox roadZones = new() { Content = "Remove zones that would act on a car on the road (doors, hit, ladder, escalator, grid, rail)", IsChecked = true };
    private readonly CheckBox trackCameras = new() { Content = "Remove the fixed camera angles along the track (the view keeps following the car)", IsChecked = true, ToolTip = "Camera zones (type 1) that reach the road or come within a few cells of it" };
    private readonly CheckBox story = new() { Content = "Story: the rain, the Weather Wizard, Raph's time to beat, the aliens' new track and Mr. Paul's ferry ticket (Citadel Island)", IsChecked = true, ToolTip = "Zoe sends Twinsen to the Weather Wizard; Raph won't let him up the lighthouse until Twinsen beats his time on the storm track, where Mr. Paul wants racing gloves (in the attic, where the darts lay); then the game's own spell and the aliens, who build the town circuit for the next day: Twinsen sleeps in his bed, races Raph, Zoe, Mr. Paul, the Tralu and the thief, and a win is a ferry ticket. Played as a game: Play with the race car setup's new game." };
    // (Avalonia's text box has no scroll-bar properties of its own: BuildLayout sets them on the ScrollViewer inside its template)
    private readonly TextBox log = new() { IsReadOnly = true, FontFamily = UiFonts.Mono, FontSize = 11, Height = 190, TextWrapping = TextWrapping.NoWrap };
    private readonly Button buildButton = new() { Content = "Build the track", Padding = new Thickness(18, 5, 18, 5), IsDefault = true };
    private readonly Button restoreButton = new() { Content = "Put the original files back", Padding = new Thickness(14, 5, 14, 5) };
    private readonly Button closeButton = new() { Content = "Close", Padding = new Thickness(14, 5, 14, 5), IsCancel = true };
    private readonly Button carButton = new() { Content = "Race car setup…", Padding = new Thickness(14, 5, 14, 5), ToolTip = "The buggy's gears, acceleration, brakes and steering when you play a folder with a race track built" };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    // the build's progress (the user, 2026-10-11: "add a visible process bar for building tracks in the track builder window"): the bar and
    // what the build is doing, how far and for how long; shown from the first build on
    private readonly ProgressBar progressBar = new() { Height = 14, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock progressText = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private bool building;

    public RaceTrackWindow(string gameRoot, Action changed)
    {
        this.gameRoot = gameRoot;
        this.changed = changed;
        Title = "LBA2 race track";
        Width = 760; SizeToContent = SizeToContent.Height; MinWidth = 640;
        // (no taller than the screen: the window scrolls instead). Avalonia gives a screen's working area in physical
        // pixels, so it is divided by that screen's scaling to reach the device-independent units MaxHeight is in.
        if ((Screens?.Primary ?? Screens?.All.FirstOrDefault()) is { } screen) MaxHeight = screen.WorkingArea.Height / screen.Scaling;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = true;
        this.SetResourceReference(BackgroundProperty, "ThemeWindowBrush");
        this.SetResourceReference(ForegroundProperty, "ThemeTextBrush");
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
        saveBuiltIn.Click += (_, _) => SaveBuiltIn();
        sampleButton.Click += (_, _) => SaveSample();
        planIsland.SelectionChanged += (_, _) => PlanChoiceChanged();
        buildButton.Click += async (_, _) => await BuildAsync();
        restoreButton.Click += (_, _) => Restore();
        carButton.Click += (_, _) => new RaceCarWindow(forPlay: false, gameRoot).WithOwner(this).ShowDialog();
        IslandChanged();
        UpdateStatus();
        // (the build goes on without the window: it isn't closed under it)
        Closing += (_, e) => { if (building) e.Cancel = true; };
    }

    // The islands the window lists, two to a row, each with what its track is: the box's label and its tooltip.
    internal static readonly (RaceTrackIsland Island, string Label, string Tip)[] Listed =
    {
        (RaceTrackIsland.Desert, "Desert island", "A jump where the lap crosses itself; what is left of the original race track (its road paint, start gantry, arch and billboard in cube 7,10) is cleared"),
        (RaceTrackIsland.Mosquibe, "Mosquibees Island", "Round the mountain: its plan draws its own bridge and jump"),
        (RaceTrackIsland.Citadel, "Citadel Island: the storm track and the town circuit", "Both of the island's tracks: the storm track in CITADEL.ILE (in the rain, with its own jump) and the town circuit in CITABAU.ILE (once the storm is over, with a bridge where it crosses itself). Play races the one of the file the editor has open."),
        (RaceTrackIsland.Celebration, "Celebration Island: round the statue", "A raised road on piers up round the statue: its plan draws it all"),
        (RaceTrackIsland.CelebrationLava, "Celebration Island: the lava lake", "Before the statue rises: from the statue track's dock round the whole island -- along the north shore, up the east coast to the crater's rim, across the lava lake on a causeway, behind the temple -- with three jumps over gaps in the raised road, a wide last turn and a ramp back down to the dock. It races in a scene of its own (223, a copy of the island's scene 95) and Play races it with CELEBRAT.ILE open"),
        (RaceTrackIsland.Elevator, "The Elevator Platform: the rollercoaster", "All of it a raised road, banked, with the slopes pulling at the car: its plan draws it all"),
        (RaceTrackIsland.Moon, "The old moon (MOON.ILE): two vertical loops", "The Emerald Moon's older copy, which the game never loads: a lap round the moon base on the crater's floor through two vertical loops -- one with a gap at its top the car has to leap upside down, and one it drives round, both steered upside down too and both fallen off if taken too slowly. It races in scenes of its own (225-228, copies of the Emerald Moon's 74-77), the opponents round the loops with you"),
        (RaceTrackIsland.Emerald, "The Emerald Moon: over the reactor", "From the user's sketch, all of it a raised road: along the moon base's roof (the straight, and the pit lane beside it: a strip of road of its own, clear space between them), up onto the crater's rim and round it through three vertical loops twice the road's width, and one jump over the whole reactor -- a curved ramp, a flight over its dish 20,000 up and a curved hill down, the road as wide as the dish there and banked hard through the turns either side. It races in scenes of its own (229-232, copies of the moon's 74-77), the opponents waiting in the pit lane while you qualify"),
        (RaceTrackIsland.Polar, "Polar Island: the dream race to Sendell", "LBA1's Polar Island (island 12, scenes 233-244), from the user's drawing: picking up from the end of the first game, Twinsen dreams he is racing FunFrock to Sendell -- a sprint from the dock up the island, round a hairpin, back down its east side, over a jump across the main straight, up a serpentine under the rocky peak and a jump onto its top, his car's top speed 140 km/h. Win and Zoe shakes him awake at home, in the second game's first scene; lose and the race starts again. A new game played as a game starts in the dream. When the folder hasn't got Polar Island the build adds it first, from the LBA1 folder in the settings; putting the track back leaves the island"),
        (RaceTrackIsland.Knartas, "The Island of the Francos: dock, refinery and village", "Most of it a raised road at three levels that passes over itself: down the dock's east arm, a loop on its north arm, a jump over the inlet's mouth and the rocket, up over the refinery and a loop over its tanks, past its machine houses among pipes and steam, a leap down over the cracking tower into the village and through its huts, a loop on the south shore, a leap up over the refinery's fence, two loops on the top road over the rocks (the second with a gap at its top) and down the channel onto the start. De La Fontaine races in the refinery's gazogem tanker, with Mr. Kurtz and the Franco nurse"),
        (RaceTrackIsland.Otringal, "Otringal: the game's tunnel, the town and the palace", "From the user's sketch, a road close over the ground: round the square island, where its two spaceships come down onto their pads by the road, hover, land and rise again; down the game's slope and through the bridge's tube (the game's tunnel), climbing under the gatehouse up into the town; up through it onto the straight along the palace's east side (the pit lane beside it), west along the north edge, a jump over the inlet, an S round the tanks, through the west pier's yard and over the sea to the south-west islets (a scene the build adds for them), along the south coast and a jump over the sea back onto the square island"),
        (RaceTrackIsland.Volcano, "Volcano Island: over the lava", "From the user's sketch, the island under Celebration, in layers: the start and the pit lane (a boost panel in it) on the sand plateau's north straight; a climb twice round a circle over the plateau, past a lava fall each time; a double spiral down, a circle over the main lava channel with a lava geyser shooting up its middle; a slope down over the channel, a jump onto the east edge past a crystal arch, a jump to the south shore, back up the volcano's flank and along the plateau's south edge and west side, under the fall's lava carried over the road. Lava geysers shower the road with balls of fire and red crystal shards fall out of the volcano -- a car caught where one lands is stopped. The dissident races in his car, with the ferryman and a Mosquibee of the hide-out"),
        (RaceTrackIsland.Wannies, "Wannies Island: through the frog and down the tower", "From the user's drawing: the start straight over the lava below the Wannies' elevator, its pit lane beside it down the pier; round the east island past the mine's machinery and the west island's south loop (boost pads round it), climbing all the way, high over the islands -- or, straight on at the east island's corner, over a boost pad and up a ramp, a high leap over the loop: a shortcut -- to the jump into a giant frog's mouth -- it opens and shuts, its tongue shooting out, and a shut one throws the car back; through the frog and out of its back, up over the lava to the elevator tower's top, and twice round the tower down a helix to the start. The Wannie miner races in his undergas bulldozer, with a Wannie in a mine cart and the old Wannie on her firefly tart"),
        (RaceTrackIsland.Sendell, "Sendell's Well: the cut island", "Island 1, which the game never shipped: the build makes it (SENDELL.ILE, a round island with a well in its middle, made from Citadel Island's files) and a scene for it (224), and a lap round the well, up from the beach onto the plateau and back down. Putting the folder back deletes the island's files"),
        (RaceTrackIsland.Palace, "The Emperor's palace: the rooms, upstairs and down, the window", "A sprint, no laps: the Dark Monk is in the palace, killing everybody. On the Palace island (Otringal's palace in its hill, two storeys high, PALACE.ILE: the build makes it afresh, with its scenes, 265 on): from the south-east room west along the red carpet, up the ramp round the stairwell's column, through the library, the music room, the princess's bedroom and the Emperor's bedchamber, his dressing room and the throne room, down the other stairwell and up the long hall to the round window Twinsen leaves by -- the finish -- and the leap out of it into the Dark Monk's teleporter. Win and Twinsen arrives on Celebration Island; lose and the race starts again. Stan races, with the road sweeper and the Emperor. Play races it with PALACE.ILE open; putting the folder back leaves the Palace island, without its track"),
        (RaceTrackIsland.Temple, "The Temple of Bú: the Halloween race", "LBA1's Temple of Bú (its three scenes, made by the build from the LBA1 folder in the settings, in LBA1's own colours): a lap on a raised road a little over its floors, through doorways cut where it passes the walls -- west along the long corridor, through the first floor and its lower hall, down a spiral through the dark to the crypt, through the crypt's rooms (the start on the straight through its west room) and up a spiral to the corridor. Pumpkins for mushrooms, Halloween all round. Raced by the Dark Monk, Ker'aooc in her cauldron and Tabata on his spell book"),
        (RaceTrackIsland.CxTower, "Island CX: round the island, down through a door, twice round the halls", "On Island CX with the control tower's halls under its gas (CXTOWER.ILE, made by the build: the halls one ring round the control room, roofed): round the island under its four corner towers, whose cannons fire bursts at whichever car outside they like; down the south-west trench and through its door, its grate gone, down under the gas and twice round the ring past its gas monsters, up and out through the north-east trench's door. The pit lane beside the start straight. Raced by the Emperor's soldier in his rocket launcher, a Franco trooper's tank and the survivor's raft"),
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
        // (a plan file is one island's: which, of those ticked)
        var chosen = planIsland.SelectedItem is ComboBoxItem { Tag: RaceTrackIsland was } ? was : null;
        planIsland.Items.Clear();
        foreach (var i in islands) planIsland.Items.Add(new ComboBoxItem { Content = Listed.First(l => l.Island == i).Label, Tag = i });
        planIsland.SelectedIndex = chosen is not null && islands.IndexOf(chosen) is var k and >= 0 ? k : islands.Count > 0 ? 0 : -1;
        filePlan.IsEnabled = islands.Count > 0;
        if (islands.Count == 0 && filePlan.IsChecked == true) builtInPlan.IsChecked = true;
        PlanChoiceChanged();
        var tracks = islands.Sum(i => i.Tracks);
        buildButton.Content = tracks > 1 ? $"Build the {tracks} tracks" : "Build the track";
        buildButton.IsEnabled = islands.Count > 0;
        UpdateStatus();
    }

    private void BuildLayout()
    {
        ScrollViewer.SetVerticalScrollBarVisibility(log, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(log, ScrollBarVisibility.Auto);
        foreach (var t in new TextBlock[] { status, progressText }) t.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        progressBar.SetResourceReference(ForegroundProperty, "ThemeAccentBrush");
        progressBar.SetResourceReference(BackgroundProperty, "ThemeFieldBrush");
        progressBar.SetResourceReference(BorderBrushProperty, "ThemeBorderBrush");
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
                   "Polar Island's (LBA1's island, added first when the folder hasn't got it) is a dream: a sprint against FunFrock from the dock to the top of the " +
                   "rocky peak, at up to 140 km/h; winning it wakes Twinsen up at home, and a new game starts in it. " +
                   "Use Tools > Test edits first to try it on a scratch copy of the game folder.",
        };
        intro.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        root.Children.Add(intro);
        var folderLine = new TextBlock { Text = $"Game folder: {gameRoot}", Margin = new Thickness(0, 8, 0, 8), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };
        folderLine.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");    // (a resource reference rather than WPF's one-shot FindResource, as everywhere else in the port)
        root.Children.Add(folderLine);

        root.Children.Add(Section("The islands"));
        var islandsHint = new TextBlock { Text = "The islands ticked get their tracks, built together. Play races the one of the island the editor has open (for Citadel Island: CITADEL.ILE the storm track, CITABAU.ILE the town circuit).", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        islandsHint.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        root.Children.Add(islandsHint);
        var islandGrid = new UniformGrid { Columns = 2 };
        foreach (var c in islandChecks) { c.SetResourceReference(ForegroundProperty, "ThemeTextBrush"); islandGrid.Children.Add(c); }
        root.Children.Add(islandGrid);

        root.Children.Add(Section("The track"));
        root.Children.Add(builtInPlan);
        var fileRow = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        DockPanel.SetDock(browseButton, Dock.Right); DockPanel.SetDock(sampleButton, Dock.Right);
        fileRow.Children.Add(filePlan); fileRow.Children.Add(sampleButton); fileRow.Children.Add(browseButton); fileRow.Children.Add(planPath);
        filePlan.Margin = new Thickness(0, 0, 8, 0);
        root.Children.Add(fileRow);
        var forRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 4, 0, 0) };
        var forText = new TextBlock { Text = "the track of", VerticalAlignment = VerticalAlignment.Center };
        forText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        forRow.Children.Add(forText); forRow.Children.Add(planIsland); forRow.Children.Add(saveBuiltIn);
        root.Children.Add(forRow);
        Avalonia.Controls.ToolTip.SetShowOnDisabled(filePlan, true);    // (WPF's ToolTipService.SetShowOnDisabled: Avalonia hangs that attached property off ToolTip itself)
        filePlan.ToolTip = "Tick an island first: a plan file is the track of one of the islands ticked (the others are built with their own)";

        root.Children.Add(Section("The scenes"));
        foreach (var c in new CheckBox[] { removeActors, buggyAlways, startAtLine, roadZones, trackCameras, story }) { c.Margin = new Thickness(0, 2, 0, 2); root.Children.Add(c); }

        root.Children.Add(status);
        root.Children.Add(progressBar);
        root.Children.Add(progressText);
        log.Margin = new Thickness(0, 10, 0, 0);
        root.Children.Add(log);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        restoreButton.Margin = new Thickness(0, 0, 10, 0); buildButton.Margin = new Thickness(0, 0, 10, 0); carButton.Margin = new Thickness(0, 0, 10, 0);
        closeButton.Click += (_, _) => Close();                  // (WPF closed a dialog by its IsCancel button by itself; Avalonia only clicks it)
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
        var file = filePlan.IsChecked == true && filePlan.IsEnabled;
        planPath.IsEnabled = file; browseButton.IsEnabled = file; planIsland.IsEnabled = file; saveBuiltIn.IsEnabled = file && planIsland.SelectedItem is not null;
    }

    // The sample plan file (docs/racetrack/sample_track_plan.json: the Desert island's lap in 63 points, each part commented) saved where the
    // user says, its path filled in -- and, the Desert island ticked, it the island the file is the track of
    private void SaveSample()
    {
        var dialog = new SaveFileDialog { Title = "Save the sample plan file", Filter = "Race track plan (*.json)|*.json", FileName = "sample_track_plan.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using (var stream = typeof(RaceTrackPlan).Assembly.GetManifestResourceStream("RaceTrackPlan.Sample.json") ?? throw new InvalidDataException("This build of the program has no sample plan."))
            using (var file = File.Create(dialog.FileName)) stream.CopyTo(file);
            planPath.Text = dialog.FileName;
            if (filePlan.IsEnabled) filePlan.IsChecked = true;
            foreach (ComboBoxItem item in planIsland.Items) if (item.Tag == RaceTrackIsland.Desert) planIsland.SelectedItem = item;
            status.Text = $"The sample plan is saved as {dialog.FileName}: open it in a text editor to see what each part of a plan is. With the Desert island ticked and it chosen as the track of, Build builds it.";
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"The sample can't be saved: {error.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // A plan file made elsewhere (the track generator) to build: its island ticked (the others as they are), the file chosen for it
    public void UsePlan(RaceTrackIsland island, string path)
    {
        foreach (var box in islandChecks) if (box.Tag == island) box.IsChecked = true;
        planPath.Text = path;
        filePlan.IsChecked = true;
        foreach (ComboBoxItem item in planIsland.Items) if (item.Tag == island) planIsland.SelectedItem = item;
        status.Text = $"The track drawn in the generator, {System.IO.Path.GetFileName(path)}, is chosen as {island.Name}'s: Build builds it (with the other islands ticked).";
    }

    private RaceTrackIsland? PlanIsland() => planIsland.SelectedItem is ComboBoxItem { Tag: RaceTrackIsland island } ? island : null;

    // The island's built-in track (its other-weather file's for Citadel Island: the town circuit, the track a plan file is) as a plan file
    private void SaveBuiltIn()
    {
        if (PlanIsland() is not { } island) return;
        var resource = island.RacesTwin && island.TwinPlanResource is { } twin ? twin : island.PlanResource;
        var dialog = new SaveFileDialog { Title = "Save the built-in track as a plan file", Filter = "Race track plan (*.json)|*.json", FileName = Path.GetFileNameWithoutExtension(island.IleFile).ToLowerInvariant() + "_track_plan.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using var stream = typeof(RaceTrackPlan).Assembly.GetManifestResourceStream(resource) ?? throw new InvalidDataException($"This build of the program has no built-in track plan for {island.Name}.");
            using var file = File.Create(dialog.FileName);
            stream.CopyTo(file);
            planPath.Text = dialog.FileName;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"The plan can't be saved: {error.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
                // (a plan file is the track of the island chosen for it: for Citadel Island, its town circuit's -- the fine-weather file's -- the storm
                // track built in)
                RaceTrackPlan? twinPlan = null;
                if (filePlan.IsChecked == true && ReferenceEquals(island, PlanIsland()))
                {
                    // (Avalonia leaves a text box's Text null until something is typed into it, where WPF started it at "": an empty
                    // path still has to reach Load, which reports it as a plan that can't be read)
                    var path = (planPath.Text ?? "").Trim();
                    if (island.RacesTwin) twinPlan = RaceTrackPlan.Load(path);
                    else plan = RaceTrackPlan.Load(path);
                }
                tracks.Add(new RaceTrackService.TrackBuild(plan, Options(island), twinPlan));
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, $"The plan can't be read: {error.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        // (Polar Island's track is built on the island, which the build makes from LBA1's scenes when the folder hasn't got it: without an
        // LBA1 folder to make it from, nothing would be built -- said now, before anything is asked or changed)
        if (islands.Any(i => i.Dream) && RaceTrackService.PolarProblem(gameRoot) is { } polar)
        {
            MessageBox.Show(this, polar, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        // (and the Temple of Bú's, which the build makes from LBA1's scenes every time)
        if (islands.Any(i => i.FromLba1) && RaceTrackService.Lba1Problem("The Temple of Bú's track is built on LBA1's temple, which the build makes from LBA1's own scenes") is { } lba1)
        {
            MessageBox.Show(this, lba1, Title, MessageBoxButton.OK, MessageBoxImage.Error);
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
        buildButton.IsEnabled = false; restoreButton.IsEnabled = false; carButton.IsEnabled = false; closeButton.IsEnabled = false;
        building = true;
        log.Text = islands.Sum(i => i.Tracks) > 1 ? $"Building {islands.Sum(i => i.Tracks)} tracks…" : "Building…";
        // the bar, where it can be seen, and the time going by while a step takes long (the road's ground and scenery on a big island)
        var started = DateTime.Now;
        var doing = "Starting";
        var done = 0.0;
        progressBar.Value = 0; progressBar.Visibility = Visibility.Visible; progressText.Visibility = Visibility.Visible;
        void Show() => progressText.Text = $"{done * 100:0}%  {doing}…  ({Elapsed(DateTime.Now - started)})";
        Show();
        // (the bar and its line have only just been shown: Avalonia scrolls to the bounds the control has when BringIntoView is called,
        // where WPF recorded the request and acted on it after the next layout pass -- so that pass is run first)
        UpdateLayout();
        progressText.BringIntoView();
        var clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        clock.Tick += (_, _) => Show();
        clock.Start();
        var progress = new Progress<RaceTrackService.BuildProgress>(p => { done = p.Done; doing = p.Doing; progressBar.Value = Math.Clamp(p.Done, 0, 1); Show(); });
        var root = gameRoot;
        RaceTrackService.BuildResult result;
        try { result = await Task.Run(() => RaceTrackService.Build(root, tracks, progress)); }
        finally
        {
            clock.Stop();
            building = false;
            buildButton.IsEnabled = true; carButton.IsEnabled = true; closeButton.IsEnabled = true;
        }
        var took = Elapsed(DateTime.Now - started);
        progressBar.Value = result.Ok ? 1 : 0;
        progressText.Text = result.Ok ? $"Built in {took}." : $"The build stopped after {took}: nothing was changed.";
        log.Text = result.Ok ? string.Join("\n", result.Log) : result.Summary;
        status.Text = result.Summary;
        UpdateStatus();
        if (result.Ok) changed();
        else MessageBox.Show(this, result.Summary, Title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string Elapsed(TimeSpan t) => t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes}:{t.Seconds:00}" : $"{t.TotalSeconds:0} s";

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
