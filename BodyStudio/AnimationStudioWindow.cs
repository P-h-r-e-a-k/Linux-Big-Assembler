using System.IO;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LBAAssembler;
using LBAAssembler.Lba1;
// the app's WPF-style blocking dialog shim (Compat/FileDialogs.cs), not Avalonia's obsolete one of the same name
using SaveFileDialog=LBAAssembler.SaveFileDialog;

namespace LbaBodyStudio;

// The "full animation suite" companion to BodyStudioWindow.cs's own Body Studio: picks a roster character
// (MarioCustom.cs and its own siblings) and an AnimGenerator clip (walk/run/idle/jump), exposes the
// generator's own tunable parameters as live sliders, plays the result back on a real posed preview
// (Lba1Pose.World -- an existing forward-kinematics helper originally written for LBA1 playback,
// reused as-is here since its own rotation math is purely geometric and doesn't care which game the
// FINAL exported file targets, only Anim.Write()'s own angle-unit scale does), and exports the
// generated clip to a real ANIM.HQR-format archive. Not a full keyframe timeline editor (dragging
// individual bone poses by hand) -- that's flagged as a possible future step in project-mario-roster
// memory, not attempted here given the time this whole roster/animation effort already took; this is
// "adjust the procedural generator's own knobs and preview/export the result," a genuinely complete
// and useful tool on its own, not a placeholder. Like Body Studio it stopped being a WinForms Form for
// the Interface Audit (see BodyStudioWindow.cs's own comment); control types and the theme mechanism
// changed, the behaviour did not.
public sealed class AnimationStudioWindow : Window
{
    readonly ComboBox character = Combo("Mario", "Luigi", "Peach", "Toad", "Bowser", "Yoshi");
    readonly ComboBox game = Combo("LBA2", "LBA1");
    readonly ComboBox animType = Combo("Walk", "Run", "Idle", "Jump");
    readonly NumberBox swingDeg = Number(0, 90, 22), kneeDeg = Number(0, 90, 35), armDeg = Number(0, 90, 18), leanDeg = Number(-20, 20, 0);
    readonly NumberBox frameCount = Number(2, 24, 8), frameTicks = Number(1, 40, 9);
    readonly ModelView preview = new();
    readonly TextBlock status = new() { Text = "Choose a character and clip, then Regenerate.", TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    readonly Button regenerate = new() { Content = "Regenerate" }, export = new() { Content = "Export…", IsEnabled = false };
    readonly CheckBox playing = new() { Content = "Play", IsChecked = true };
    // 33 ms: the same ~30 frames a second the WinForms timer ran at.
    readonly DispatcherTimer playTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };

    Anim? currentAnim;
    Body? currentBody;
    uint[]? currentPalette;
    int currentFrame;
    double elapsedTicks;

    public AnimationStudioWindow()
    {
        Title = "LBA Assembler — Animation Studio"; Width = 1200; Height = 930; MinWidth = 900; MinHeight = 600; FontSize = 13; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        this.SetResourceReference(Control.BackgroundProperty, "ThemeFieldBrush");
        var root = new DockPanel(); Content = root;
        var statusBar = new Border() { Height = 48, Padding = new Thickness(16, 8, 16, 8), Child = status }; statusBar.SetResourceReference(Border.BackgroundProperty, "ThemeWindowBrush"); DockPanel.SetDock(statusBar, Dock.Bottom); root.Children.Add(statusBar);
        // 390/360, matching Body Studio's own BodyStudioWindow.cs panel sizing -- 320/280 clipped several of
        // this form's own longer field labels/combo text at the panel edge (confirmed live 2026-09-23).
        var main = new Grid(); main.SetResourceReference(Panel.BackgroundProperty, "ThemeBorderBrush"); root.Children.Add(main);
        main.ColumnDefinitions.Add(new ColumnDefinition(390, GridUnitType.Pixel) { MinWidth = 360 }); main.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel)); main.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        var splitter = new GridSplitter(); splitter.SetResourceReference(Control.BackgroundProperty, "ThemeBorderBrush"); Grid.SetColumn(splitter, 1); main.Children.Add(splitter);
        var left = new DockPanel(); left.SetResourceReference(Panel.BackgroundProperty, "ThemeWindowBrush"); Grid.SetColumn(left, 0); main.Children.Add(left);
        var fields = new StackPanel() { Margin = new Thickness(18) };
        void Add(Control c) { c.Margin = new Thickness(0, 0, 0, 10); fields.Children.Add(c); }
        // Description text (wrapped to the column) reads softer than headings and field names: that is the only
        // difference between the two, so the wrap width doubles as which theme brush the text takes.
        TextBlock Themed(string text, double maxWidth = 0) { var t = new TextBlock() { Text = text, TextWrapping = TextWrapping.Wrap }; if (maxWidth > 0) { t.MaxWidth = maxWidth; t.HorizontalAlignment = HorizontalAlignment.Left; } t.SetResourceReference(TextBlock.ForegroundProperty, maxWidth > 0 ? "ThemeTextMutedBrush" : "ThemeTextBrush"); return t; }
        void Label(string text) { var t = Themed(text); t.FontWeight = FontWeight.Bold; Add(t); }
        void Note(string text) => Add(Themed(text, 255));
        void Field(string text, Control c) { var t = Themed(text); t.Margin = new Thickness(0, 2, 0, 3); Add(t); Add(c); }

        Label("ANIMATION STUDIO");
        Note("Generates walk/run/idle/jump clips procedurally (AnimGenerator.cs) for any roster character -- the same 19-bone rig every custom body shares. Adjust the joint-angle knobs below and Regenerate to preview.");
        Field("Character", character); Field("Game (colours + angle scale)", game); Field("Clip", animType);
        Label("Walk / run knobs");
        Field("Leg swing (degrees)", swingDeg); Field("Knee bend (degrees)", kneeDeg); Field("Arm swing (degrees)", armDeg); Field("Forward lean (degrees, run only)", leanDeg);
        Field("Keyframes per cycle", frameCount); Field("Ticks between keyframes (lower = faster)", frameTicks);
        Note("Idle and jump use their own fixed shape (AnimGenerator.Idle/Jump) -- these knobs only affect Walk/Run.");
        Add(regenerate); Add(playing);
        var buttons = new StackPanel() { Orientation = Orientation.Horizontal, Spacing = 6, Height = 50, Margin = new Thickness(12, 8, 0, 0) };
        buttons.Children.Add(export); DockPanel.SetDock(buttons, Dock.Bottom); left.Children.Add(buttons);
        left.Children.Add(new ScrollViewer() { Content = fields, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Grid.SetColumn(preview, 2); main.Children.Add(preview);

        regenerate.Click += (_, _) => Regenerate();
        export.Click += (_, _) => Export();
        playing.IsCheckedChanged += (_, _) => { if (playing.IsChecked == true) playTimer.Start(); else playTimer.Stop(); };
        playTimer.Tick += (_, _) => Advance();
        foreach (var c in new Control[] { character, game, animType, swingDeg, kneeDeg, armDeg, leanDeg, frameCount, frameTicks })
        {
            if (c is ComboBox cb) cb.SelectionChanged += (_, _) => export.IsEnabled = false;
            if (c is NumberBox n) n.ValueChanged += () => export.IsEnabled = false;
        }
        ApplyTheme();
        playTimer.Start();
        Closed += (_, _) => playTimer.Stop();
    }

    // Run once, right after the whole tree is built (called from the constructor) -- every assignment below is
    // SetResourceReference (Compat/ResourceCompat.cs: a DynamicResource binding), not a resolved Brush, so it keeps
    // tracking the active theme forever after with no need to ever re-run this on a later theme switch.
    void ApplyTheme()
    {
        foreach (var c in BodyStudioWindow.Descendants(this))
        {
            switch (c)
            {
                case NumberBox nb: nb.ApplyTheme("ThemeFieldBrush", "ThemeTextBrush", "ThemeButtonBorderBrush"); break;
                case Button b when b != export:
                    b.SetResourceReference(Control.BackgroundProperty, "ThemeRaisedBrush");
                    b.SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush");
                    b.SetResourceReference(Control.BorderBrushProperty, "ThemeButtonBorderBrush");
                    break;
                case CheckBox chk: chk.SetResourceReference(Control.ForegroundProperty, "ThemeTextBrush"); break;
            }
        }
        export.SetResourceReference(Control.BackgroundProperty, "ThemeAccentBrush");
        export.SetResourceReference(Control.ForegroundProperty, "ThemeAccentTextBrush");
        export.FontWeight = FontWeight.Bold;
        status.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
    }
    static ComboBox Combo(params string[] items) { var c = new ComboBox() { HorizontalAlignment = HorizontalAlignment.Stretch }; foreach (var item in items) c.Items.Add(item); c.SelectedIndex = 0; return c; }
    static NumberBox Number(int min, int max, int value) => new(min, max, value);

    void Regenerate()
    {
        try
        {
            var gameIndex = game.SelectedIndex == 0 ? 2 : 1; // combo is "LBA2","LBA1"
            var folder = gameIndex == 1 ? LBAAssembler.EditorSettings.Current.Lba1Directory : LBAAssembler.EditorSettings.Current.GameDirectory;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) { Error("Set the LBA" + gameIndex + " game folder in Settings first."); return; }
            var donor = Body.Read(new Hqr(Generator.BodyArchive(folder)).Read(0), gameIndex);
            Body body = (character.SelectedItem as string, gameIndex) switch
            {
                ("Mario", 2) => MarioCustom.Build(donor),
                ("Mario", 1) => MarioCustom.Build(donor, game: 1, red: 97, skin: 48, dark: 64, blue: 66),
                ("Luigi", 2) => LuigiCustom.Build(donor),
                ("Luigi", 1) => LuigiCustom.Build(donor, game: 1, green: 118, skin: 48, dark: 64, blue: 66),
                ("Peach", 2) => PeachCustom.Build(donor),
                ("Peach", 1) => PeachCustom.Build(donor, game: 1, pink: 224, skin: 48, gold: 144, blonde: 150),
                ("Toad", 2) => ToadCustom.Build(donor),
                ("Toad", 1) => ToadCustom.Build(donor, game: 1, red: 97, blue: 66, dark: 64, white: 214, cream: 48),
                ("Bowser", 2) => BowserCustom.Build(donor),
                ("Bowser", 1) => BowserCustom.Build(donor, game: 1, green: 118, cream: 48, shellColour: 97, dark: 64),
                ("Yoshi", 2) => YoshiCustom.Build(donor),
                ("Yoshi", 1) => YoshiCustom.Build(donor, game: 1, green: 118, cream: 48, saddle: 97, white: 214),
                _ => MarioCustom.Build(donor),
            };
            var nbBones = body.Bones.Count;
            Anim anim = (animType.SelectedItem as string) switch
            {
                "Walk" => AnimGenerator.Gait(gameIndex, nbBones, frameCount.Value, frameTicks.Value, swingDeg.Value, kneeDeg.Value, armDeg.Value, 0),
                "Run" => AnimGenerator.Gait(gameIndex, nbBones, frameCount.Value, frameTicks.Value, swingDeg.Value, kneeDeg.Value, armDeg.Value, leanDeg.Value),
                "Idle" => AnimGenerator.Idle(gameIndex, nbBones),
                "Jump" => AnimGenerator.Jump(gameIndex, nbBones),
                _ => AnimGenerator.Walk(gameIndex, nbBones),
            };
            currentBody = body; currentAnim = anim; currentPalette = Generator.Palette(folder);
            currentFrame = 0; elapsedTicks = 0;
            preview.Model = new Generated(body, currentPalette, 0, "", "");
            export.IsEnabled = true;
            status.Text = $"{character.SelectedItem} ({(gameIndex == 1 ? "LBA1" : "LBA2")}) -- {animType.SelectedItem}, {anim.Frames.Count} keyframes, {body.Bones.Count} bones. Playing live.";
        }
        catch (Exception e) { Error(e.Message); }
    }

    // Advances playback and computes an interpolated pose between the current and next keyframe --
    // same linear-interpolation shape Lba1Animation.Player already uses for a parsed-from-bytes clip,
    // just working directly on the in-memory Anim being tuned here instead of round-tripping through
    // Write()/Parse() first (regenerating on every knob tweak already exercises the real writer, via
    // Export -- this preview path is deliberately the cheaper, no-serialization one).
    void Advance()
    {
        if (currentAnim == null || currentBody == null || currentAnim.Frames.Count < 2) { RenderCurrentPose(); return; }
        var frames = currentAnim.Frames;
        var target = (currentFrame + 1 < frames.Count) ? currentFrame + 1 : currentAnim.LoopFrame;
        elapsedTicks += playTimer.Interval.TotalMilliseconds / 20.0; // ~50Hz engine tick, matches AnimGenerator's own frameTicks units
        var length = System.Math.Max(1, frames[target].FrameTime);
        if (elapsedTicks >= length) { elapsedTicks -= length; currentFrame = target; }
        RenderCurrentPose();
    }

    void RenderCurrentPose()
    {
        if (currentAnim == null || currentBody == null) return;
        var frames = currentAnim.Frames;
        var from = frames[currentFrame];
        var target = (currentFrame + 1 < frames.Count) ? currentFrame + 1 : currentAnim.LoopFrame;
        var to = frames[target];
        var t = currentFrame == target ? 0 : System.Math.Clamp(elapsedTicks / System.Math.Max(1, to.FrameTime), 0, 1);
        var bones = new (int Type, double X, double Y, double Z)[from.Bones.Length];
        for (var i = 0; i < bones.Length; i++)
        {
            var a = from.Bones[i]; var b = to.Bones[i];
            // AnimBone stores turns (0..1 = 360 degrees); Lba1Pose.World's own Rotate() expects the
            // LBA1 1024-units-per-turn scale regardless of which game this clip will actually target
            // -- its rotation math is purely geometric, so this is just a fixed internal unit for the
            // preview, unrelated to Anim.Write()'s own game-aware angle scaling for the exported file.
            bones[i] = (b.Type, a.X * 1024 + (b.X - a.X) * 1024 * t, a.Y * 1024 + (b.Y - a.Y) * 1024 * t, a.Z * 1024 + (b.Z - a.Z) * 1024 * t);
        }
        preview.Pose = Lba1Pose.World(currentBody, bones);
        preview.Invalidate();
    }

    void Export()
    {
        if (currentAnim == null) return;
        var d = new SaveFileDialog() { Filter = "Animation archive|*.hqr;*.HQR", FileName = $"{character.SelectedItem}{animType.SelectedItem}.hqr" };
        if (d.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllBytes(d.FileName, Hqr.Build([currentAnim.Write()]));
            status.Text = $"Exported to {d.FileName} (entry 0, {currentAnim.Frames.Count} keyframes, {(currentAnim.Game == 1 ? "LBA1" : "LBA2")} angle scale).";
        }
        catch (Exception e) { Error(e.Message); }
    }

    void Error(string message) { status.Text = message; MessageBox.Show(this, message, "Animation Studio", MessageBoxButton.OK, MessageBoxImage.Error); }
}
