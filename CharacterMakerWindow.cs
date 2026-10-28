using System.IO;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LBAAssembler.Characters;
using LbaBodyStudio;

namespace LBAAssembler;

// The Character maker (Build > Actors and zones > Character maker…; 2026-10-23, recreating LBA1's Hamalayi by hand: "recreate an actor
// along with animations purely by driving the interface as a user would do without anything external"): a character of the open map's
// own -- an entity with its bodies and animations -- made from one of the game's bodies or from nothing, parts (boxes, sheets,
// triangles) hung from its points, and its animations keyframe by keyframe, each bone's turn typed in, seen posed and playing. Saved into
// the map's workspace (MapCharacters: BODY.HQR, ANIM.HQR, the entity table, <stem>_CHARACTERS.TXT) for the scene editor's actors, and so
// into the map with Save map.
internal sealed class CharacterMakerWindow : Window
{
    private readonly string directory, sidecar;
    // (the game's palette: Generator.Palette gives 256 packed 0xAARRGGBB colours here, where upstream's was a System.Drawing.Color[])
    private readonly uint[] palette;
    private readonly List<(int Generic, string Name, Body Body)> bodies = new();
    private readonly List<CharacterAnim> anims = new();
    private int entity = -1, current = -1, currentAnim = -1, currentKey = -1;
    private bool listing, dirty;
    private double playedMs;

    private readonly ComboBox characterBox = new() { Width = 260 };
    private readonly TextBox nameBox = F("New character", 220);
    private readonly TextBox gameBodyBox = F("0"), bodyGenericBox = F("0"), bodyNameBox = F("Body", 160);
    private readonly ListBox bodyList = L(70), boneList = L(150), pointList = L(110);
    private readonly ComboBox shapeBox = new() { Width = 200 };
    private readonly TextBox pivotBox = F("0"), offXBox = F("0"), offYBox = F("0"), offZBox = F("0"), sizeXBox = F("100"), sizeYBox = F("100"), sizeZBox = F("100"), turnBox = F("0"), colourBox = F("80"), recolourBoneBox = F("1");
    private readonly Avalonia.Controls.Shapes.Rectangle swatch = new() { Width = 22, Height = 22, Stroke = Brushes.Gray };
    private readonly ListBox animList = L(110), keyList = L(110), poseList = L(170);
    private readonly TextBox animGenericBox = F("0"), animNameBox = F("Standing", 160), copyAnimBox = F("");
    private readonly TextBox msBox = F("200"), stepXBox = F("0"), stepYBox = F("0"), stepZBox = F("0");
    // (a keyframe in the air: the animation's master record says no gravity -- the game's jumps, which carry a character over ground it
    // may not walk onto, a cliff's face, and let it fall where it comes down)
    private readonly CheckBox airBox = new() { Content = "Leaves the ground (a jump: no gravity, over cliffs)" };
    private readonly TextBox poseBoneBox = F("1"), poseXBox = F("0"), poseYBox = F("0"), poseZBox = F("0");
    private readonly ModelView preview = new();
    private readonly Slider yawSlider = new() { Minimum = -180, Maximum = 180, Value = 30, Width = 240 };
    private readonly CheckBox playBox = new() { Content = "Play the animation", Margin = new Thickness(12, 0, 0, 0) };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(40) };

    private static TextBox F(string text, double width = 64) => new() { Text = text, Width = width, Padding = new Thickness(2), Margin = new Thickness(0, 0, 6, 0) };
    private static ListBox L(double height) => new() { Height = height, FontFamily = UiFonts.Mono, FontSize = 11 };

    public CharacterMakerWindow(string directory, string sidecar)
    {
        this.directory = directory; this.sidecar = sidecar;
        palette = Generator.Palette(directory);
        Title = "Character maker";
        Width = 1250; Height = 900; MinWidth = 1000; MinHeight = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.SetResourceReference(BackgroundProperty, "ThemeFieldBrush");
        var root = new DockPanel { Margin = new Thickness(10) };
        Content = root;

        TextBlock T(string t, bool head = false)
        {
            var l = new TextBlock { Text = t, Margin = new Thickness(0, head ? 8 : 2, 6, 2), FontWeight = head ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            l.SetResourceReference(TextBlock.ForegroundProperty, head ? "ThemeTextBrush" : "ThemeTextMutedBrush");
            return l;
        }
        WrapPanel Row(params Control[] items) { var r = new WrapPanel { Margin = new Thickness(0, 1, 0, 3) }; foreach (var i in items) r.Children.Add(i); return r; }
        Button B(string text, string tip, Action click)
        {
            var b = new Button { Content = text, ToolTip = tip, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 4) };
            Avalonia.Automation.AutomationProperties.SetHelpText(b, tip);
            b.Click += (_, _) => Guard(click);
            return b;
        }

        // ---- the character: which, its name, saved into the map
        var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(top, Dock.Top);
        top.Children.Add(T("Character", head: true));
        top.Children.Add(characterBox);
        top.Children.Add(B("Open it", "Opens the map's character chosen to change it (or starts a new one)", OpenCharacter));
        top.Children.Add(T("Name"));
        top.Children.Add(nameBox);
        var save = B("Save into the map", "Writes the character's bodies, animations and entity into the map (then File > Save map writes the map's file)", SaveCharacter);
        save.FontWeight = FontWeights.SemiBold;
        top.Children.Add(save);
        root.Children.Add(top);
        status.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextBrush");
        Avalonia.Automation.AutomationProperties.SetAutomationId(status, "CharacterMakerStatus");
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        // ---- the left: the body and the animations
        var tabs = new TabControl { Width = 470 };
        DockPanel.SetDock(tabs, Dock.Left);
        root.Children.Add(tabs);

        var bodyPanel = new StackPanel { Margin = new Thickness(6) };
        bodyPanel.Children.Add(Row(T("Start from the game's body (BODY.HQR)"), gameBodyBox, B("Start from it", "The character's body becomes a copy of that game body (its bones, points and polygons) to add parts to", StartFromGame)));
        bodyPanel.Children.Add(Row(B("Start empty", "The body becomes one point at the feet, to build every part of it", StartEmpty)));
        bodyPanel.Children.Add(T("The character's bodies (a script's body(n) picks one by its number)", head: true));
        bodyPanel.Children.Add(bodyList);
        bodyPanel.Children.Add(Row(T("Number"), bodyGenericBox, T("Name"), bodyNameBox, B("Set", "Gives the body chosen this number and name", SetBodyName)));
        bodyPanel.Children.Add(Row(B("New body (a copy of this one)", "Another body of the character: a copy of the one chosen, numbered as typed", CopyBody), B("Remove this body", "Takes the body chosen out of the character", RemoveBody)));
        bodyPanel.Children.Add(T("Its bones (each part a bone of its own) -- pick one to see its points", head: true));
        bodyPanel.Children.Add(boneList);
        bodyPanel.Children.Add(pointList);
        bodyPanel.Children.Add(T("Add a part, hung from a point of the body: its middle that far from the point, that size", head: true));
        foreach (var n in CharacterBody.ShapeNames) shapeBox.Items.Add(n);
        shapeBox.SelectedIndex = 0;
        bodyPanel.Children.Add(Row(T("Shape"), shapeBox, T("From point"), pivotBox));
        bodyPanel.Children.Add(Row(T("Offset X"), offXBox, T("Y"), offYBox, T("Z"), offZBox));
        bodyPanel.Children.Add(Row(T("Size X"), sizeXBox, T("Y"), sizeYBox, T("Z"), sizeZBox));
        bodyPanel.Children.Add(Row(T("Turn °"), turnBox, T("Colour"), colourBox, swatch));
        bodyPanel.Children.Add(Row(B("Add the part", "Adds the part as a new bone hung from the point", AddPart), B("Take the last part away", "Takes the last part (bone) away", RemovePart)));
        bodyPanel.Children.Add(Row(T("Recolour bone"), recolourBoneBox, B("Recolour it", "Every polygon of that bone in the colour above", Recolour)));
        tabs.Items.Add(new TabItem { Header = "Body", Content = new ScrollViewer { Content = bodyPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });

        var animPanel = new StackPanel { Margin = new Thickness(6) };
        animPanel.Children.Add(T("The character's animations (a script's anim(n) plays one by its number)", head: true));
        animPanel.Children.Add(animList);
        animPanel.Children.Add(Row(T("Number"), animGenericBox, T("Name"), animNameBox));
        animPanel.Children.Add(Row(T("Copy of the game's animation (ANIM.HQR, blank: standing still)"), copyAnimBox));
        animPanel.Children.Add(Row(B("New animation", "A new animation numbered and named as typed: a copy of the game animation typed, or one keyframe standing still", NewAnim),
            B("Set number and name", "Gives the animation chosen the number and name typed", SetAnimName), B("Remove it", "Takes the animation chosen out", RemoveAnim)));
        animPanel.Children.Add(T("Its keyframes", head: true));
        animPanel.Children.Add(keyList);
        animPanel.Children.Add(Row(T("Milliseconds to reach it (LBA2's Twinsen walks a keyframe in 200)"), msBox));
        airBox.SetResourceReference(ForegroundProperty, "ThemeTextBrush");
        animPanel.Children.Add(Row(airBox));
        animPanel.Children.Add(Row(T("Move X"), stepXBox, T("Y"), stepYBox, T("Z"), stepZBox, B("Set", "The keyframe's time, how far the character moves over it and whether it is in the air", SetKeyTiming)));
        animPanel.Children.Add(Row(B("Add a keyframe (a copy of this one)", "A new keyframe after the one chosen, the same pose to change", AddKey), B("Remove it", "Takes the keyframe chosen out", RemoveKey),
            B("Loop back to it", "After the last keyframe the animation goes on from this one", SetLoop)));
        animPanel.Children.Add(T("The keyframe's pose: each bone's turn from its rest (degrees)", head: true));
        animPanel.Children.Add(poseList);
        animPanel.Children.Add(Row(T("Bone"), poseBoneBox, T("Turn X"), poseXBox, T("Y"), poseYBox, T("Z"), poseZBox));
        animPanel.Children.Add(Row(B("Set the bone", "The bone's turn in the keyframe chosen", SetBone), B("Same in every keyframe", "The bone's turn in every keyframe of the animation", SetBoneEverywhere)));
        tabs.Items.Add(new TabItem { Header = "Animations", Content = new ScrollViewer { Content = animPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });

        // ---- the right: the preview
        var right = new DockPanel { Margin = new Thickness(10, 0, 0, 0) };
        var controls = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(controls, Dock.Bottom);
        controls.Children.Add(T("Turn the view"));
        controls.Children.Add(yawSlider);
        playBox.SetResourceReference(ForegroundProperty, "ThemeTextBrush");
        controls.Children.Add(playBox);
        right.Children.Add(controls);
        right.Children.Add(preview);
        root.Children.Add(right);

        foreach (var (c, n) in new (Control, string)[]
                 {
                     (characterBox, "Character"), (nameBox, "Character name"), (gameBodyBox, "Game body"), (bodyGenericBox, "Body number"), (bodyNameBox, "Body name"), (bodyList, "Bodies"),
                     (boneList, "Bones"), (pointList, "Points"), (shapeBox, "Shape"), (pivotBox, "From point"), (offXBox, "Offset X"), (offYBox, "Offset Y"), (offZBox, "Offset Z"),
                     (sizeXBox, "Size X"), (sizeYBox, "Size Y"), (sizeZBox, "Size Z"), (turnBox, "Turn"), (colourBox, "Colour"), (recolourBoneBox, "Recolour bone"),
                     (animList, "Animations"), (animGenericBox, "Animation number"), (animNameBox, "Animation name"), (copyAnimBox, "Copy of game animation"), (keyList, "Keyframes"),
                     (msBox, "Milliseconds"), (stepXBox, "Move X"), (stepYBox, "Move Y"), (stepZBox, "Move Z"), (poseList, "Pose"), (poseBoneBox, "Pose bone"), (poseXBox, "Turn X"),
                     (poseYBox, "Turn Y"), (poseZBox, "Turn Z"), (airBox, "Leaves the ground"), (yawSlider, "Turn the view"), (playBox, "Play the animation"),
                 })
            Avalonia.Automation.AutomationProperties.SetName(c, n);

        colourBox.TextChanged += (_, _) => ShowSwatch();
        bodyList.SelectionChanged += (_, _) => { if (!listing && bodyList.SelectedIndex >= 0) { current = bodyList.SelectedIndex; ShowBody(); } };
        boneList.SelectionChanged += (_, _) => { if (!listing) ShowPoints(); };
        animList.SelectionChanged += (_, _) => { if (!listing && animList.SelectedIndex >= 0) { currentAnim = animList.SelectedIndex; currentKey = 0; ShowAnim(); } };
        keyList.SelectionChanged += (_, _) => { if (!listing && keyList.SelectedIndex >= 0) { currentKey = keyList.SelectedIndex; ShowKey(); } };
        poseList.SelectionChanged += (_, _) =>
        {
            if (listing || poseList.SelectedIndex < 0 || Key() is not { } k) return;
            var b = poseList.SelectedIndex + 1;
            poseBoneBox.Text = b.ToString();
            var v = k.Bones[b].Value;
            poseXBox.Text = Num(v.X); poseYBox.Text = Num(v.Y); poseZBox.Text = Num(v.Z);
        };
        yawSlider.ValueChanged += (_, _) => { preview.Yaw = (float)(yawSlider.Value * Math.PI / 180); preview.Invalidate(); };
        playBox.Checked += (_, _) => { playedMs = 0; timer.Start(); };
        playBox.Unchecked += (_, _) => { timer.Stop(); Pose(); };
        timer.Tick += (_, _) => { playedMs += timer.Interval.TotalMilliseconds; if (playedMs > 1e7) playedMs = 0; Pose(); };
        Closing += (_, e) =>
        {
            if (!dirty) return;
            var answer = MessageBox.Show(this, "Save the character into the map first?\n\nYes saves it, No throws the changes away.", Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) e.Cancel = true;
            else if (answer == MessageBoxResult.Yes) { Guard(SaveCharacter); if (dirty) e.Cancel = true; }
        };
        Closed += (_, _) => timer.Stop();

        ListCharacters();
        ShowSwatch();
        preview.Yaw = (float)(yawSlider.Value * Math.PI / 180);
        Say("Start from a game body or start empty, add parts, then make its animations; Save into the map.");
    }

    // ---- small helpers

    private void Say(string text) => status.Text = text;

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or FormatException or IOException or ArgumentException or UnauthorizedAccessException)
        {
            Say(e.Message);
        }
    }

    private static string Num(float v) => Math.Round(v, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static float Value(TextBox box, string what)
    {
        // (an empty Avalonia text box has a null Text, where WPF's was "": every read of one here goes through ?? "")
        if (!float.TryParse((box.Text ?? "").Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) throw new FormatException($"{what}: type a number.");
        return v;
    }

    private static int Whole(TextBox box, string what, int min = int.MinValue, int max = int.MaxValue)
    {
        if (!int.TryParse((box.Text ?? "").Trim(), out var v) || v < min || v > max) throw new FormatException($"{what}: type a whole number{(min > int.MinValue ? $" from {min} to {max}" : "")}.");
        return v;
    }

    private Body Current => current >= 0 && current < bodies.Count ? bodies[current].Body : throw new InvalidOperationException("Start the body first (from a game body, or empty).");

    private CharacterAnim? Anim() => currentAnim >= 0 && currentAnim < anims.Count ? anims[currentAnim] : null;
    private CharacterAnim.Key? Key() => Anim() is { } a && currentKey >= 0 && currentKey < a.Keys.Count ? a.Keys[currentKey] : null;

    private void ShowSwatch()
    {
        // (the colour lit as the game lights a body: a polygon's colour is the start of its ramp of sixteen, the light takes it some seven
        // steps up -- the swatch shows that, the ramp's start to its lit middle)
        if (int.TryParse((colourBox.Text ?? "").Trim(), out var c) && c >= 0 && c < 256)
        {
            var lit = palette[Math.Min(c | 15, c + 7)];
            var dark = palette[c];
            // (WPF's LinearGradientBrush(from, to, angle) with angle 0 drew the ramp left to right; Avalonia has no such constructor, so
            // that same line is given as the brush's own start and end points)
            swatch.Fill = new LinearGradientBrush
            {
                StartPoint = RelativePoint.TopLeft,
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromUInt32(dark), 0), new GradientStop(Color.FromUInt32(lit), 1) },
            };
        }
        else swatch.Fill = Brushes.Transparent;
    }

    private void Changed() { dirty = true; if (!Title!.StartsWith('*')) Title = "* " + Title; }

    // ---- characters

    private void ListCharacters(int? select = null)
    {
        characterBox.Items.Clear();
        characterBox.Items.Add(new ComboBoxItem { Content = "(a new character)", Tag = -1 });
        foreach (var c in MapCharacters.Load(sidecar))
            characterBox.Items.Add(new ComboBoxItem { Content = $"{c.Entity}: {c.Name}", Tag = c.Entity });
        characterBox.SelectedIndex = 0;
        foreach (ComboBoxItem item in characterBox.Items) if (item.Tag is int e && e == select) characterBox.SelectedItem = item;
    }

    private void OpenCharacter()
    {
        var chosen = characterBox.SelectedItem is ComboBoxItem { Tag: int e } ? e : -1;
        bodies.Clear(); anims.Clear(); current = currentAnim = currentKey = -1;
        entity = chosen;
        if (chosen < 0)
        {
            nameBox.Text = "New character";
            ShowBody(); ListAnims();
            Say("A new character: start its body from a game body or empty.");
            return;
        }
        var c = MapCharacters.Load(sidecar).First(x => x.Entity == chosen);
        nameBox.Text = c.Name;
        var bodyHqr = HqrArchive.Open(Path.Combine(directory, "BODY.HQR"));
        var animHqr = HqrArchive.Open(Path.Combine(directory, "ANIM.HQR"));
        foreach (var b in c.Bodies) bodies.Add((b.Generic, b.Name, CharacterBody.FromGame(bodyHqr.Read(b.Entry))));
        current = bodies.Count > 0 ? 0 : -1;
        var count = bodies.Count > 0 ? bodies[0].Body.Bones.Count : 1;
        foreach (var a in c.Anims) anims.Add(CharacterAnim.Read(animHqr.Read(a.Entry), count, a.Generic, a.Name));
        currentAnim = anims.Count > 0 ? 0 : -1; currentKey = 0;
        dirty = false; Title = "Character maker";
        ShowBody(); ListAnims();
        Say($"Opened {c.Name}: {bodies.Count} bodies, {anims.Count} animations.");
    }

    private void SaveCharacter()
    {
        if (bodies.Count == 0) throw new InvalidOperationException("The character has no body yet.");
        var bones = bodies[0].Body.Bones.Count;
        if (bodies.Any(b => b.Body.Bones.Count != bones)) throw new InvalidOperationException("Every body of a character has the same bones (its animations move them): make the others copies of the first.");
        if (anims.Count == 0) anims.Add(CharacterAnim.Still(bones, 0, "Standing"));
        var made = bodies.Select(b => new MapCharacters.MadeBody(b.Generic, b.Name, b.Body.Write(), CharacterBody.Extent(b.Body))).ToList();
        var madeAnims = anims.Select(a => { a.FitBones(bones); return new MapCharacters.MadeAnim(a.Generic, a.Name, a.Write(bones)); }).ToList();
        var name = (nameBox.Text ?? "").Trim().Length > 0 ? (nameBox.Text ?? "").Trim() : "Character";
        var put = MapCharacters.Put(directory, sidecar, entity, name, made, madeAnims);
        entity = put.Entity;
        dirty = false; Title = "Character maker";
        ListCharacters(entity);
        if (currentAnim < 0 && anims.Count > 0) { currentAnim = 0; currentKey = 0; }
        ListAnims();
        Saved?.Invoke();
        Say($"{name} saved into the map: entity {entity}, bodies {string.Join(", ", put.Bodies.Select(b => $"{b.Generic} (BODY.HQR {b.Entry})"))}, " +
            $"animations {string.Join(", ", put.Anims.Select(a => a.Generic))}. Build > Actors and zones: Add > New 3D actor shows it among the bodies.");
    }

    public event Action? Saved;

    // ---- bodies

    private void StartFromGame()
    {
        var n = Whole(gameBodyBox, "Game body", 0);
        var path = Path.Combine(directory, "BODY.HQR");
        var hqr = HqrArchive.Open(path);
        if (n >= hqr.Slots - 1) throw new InvalidOperationException($"BODY.HQR has {hqr.Slots - 1} bodies (0 to {hqr.Slots - 2}).");
        var body = CharacterBody.FromGame(hqr.Read(n));
        SetFirstBody(body, $"Game body {n}");
        Say($"The body is a copy of the game's body {n}: {body.Bones.Count} bones, {body.Vertices.Count} points, {body.Faces.Count} polygons.");
    }

    private void StartEmpty()
    {
        SetFirstBody(CharacterBody.Empty(), "Body");
        Say("The body is one point at the feet (point 0): add its parts, hung from it.");
    }

    private void SetFirstBody(Body body, string name)
    {
        if (current >= 0 && current < bodies.Count) bodies[current] = (bodies[current].Generic, bodies[current].Name, body);
        else { bodies.Add((0, name, body)); current = bodies.Count - 1; }
        FitAnims();
        Changed(); ShowBody();
    }

    private void SetBodyName()
    {
        var g = Whole(bodyGenericBox, "Body number", 0, 255);
        if (bodies.Where((_, i) => i != current).Any(b => b.Generic == g)) throw new InvalidOperationException($"Another body is number {g}.");
        bodies[current] = (g, (bodyNameBox.Text ?? "").Trim(), Current);
        Changed(); ShowBody();
    }

    private void CopyBody()
    {
        var body = CharacterBody.FromGame(Current.Write());
        var g = Whole(bodyGenericBox, "Body number", 0, 255);
        if (bodies.Any(b => b.Generic == g)) g = Enumerable.Range(0, 256).First(x => bodies.All(b => b.Generic != x));
        bodies.Add((g, (bodyNameBox.Text ?? "").Trim() is { Length: > 0 } n ? n : $"Body {g}", body));
        current = bodies.Count - 1;
        Changed(); ShowBody();
        Say($"Body {g}: a copy of the other, to recolour or change (the same bones: the character's animations move it too).");
    }

    private void RemoveBody()
    {
        if (current < 0) return;
        bodies.RemoveAt(current);
        current = bodies.Count > 0 ? 0 : -1;
        Changed(); ShowBody();
    }

    private void AddPart()
    {
        var body = Current;
        var shape = (CharacterBody.Shape)Math.Max(0, shapeBox.SelectedIndex);
        var bone = CharacterBody.AddPart(body, shape, Whole(pivotBox, "From point", 0),
            new Vector3(Value(offXBox, "Offset X"), Value(offYBox, "Offset Y"), Value(offZBox, "Offset Z")),
            new Vector3(Value(sizeXBox, "Size X"), Value(sizeYBox, "Size Y"), Value(sizeZBox, "Size Z")), Value(turnBox, "Turn"), Whole(colourBox, "Colour", 0, 255));
        FitAnims();
        Changed(); ShowBody(selectBone: bone);
        Say($"Part added: bone {bone}, its points {body.Bones[bone].Start} to {body.Bones[bone].Start + body.Bones[bone].Count - 1}.");
    }

    private void RemovePart()
    {
        CharacterBody.RemoveLastPart(Current);
        FitAnims();
        Changed(); ShowBody();
        Say("The last part is taken away.");
    }

    private void Recolour()
    {
        var n = CharacterBody.Recolour(Current, Whole(recolourBoneBox, "Recolour bone", 0), Whole(colourBox, "Colour", 0, 255));
        Changed(); ShowBody();
        Say($"{n} polygons recoloured.");
    }

    // (the animations keep a slot for each bone: a part added keeps still in them until posed)
    private void FitAnims()
    {
        if (bodies.Count == 0) return;
        var bones = bodies[0].Body.Bones.Count;
        foreach (var a in anims) a.FitBones(bones);
    }

    private void ShowBody(int? selectBone = null)
    {
        listing = true;
        bodyList.Items.Clear();
        foreach (var b in bodies) bodyList.Items.Add($"{b.Generic}: {b.Name} -- {b.Body.Bones.Count} bones, {b.Body.Vertices.Count} points, {b.Body.Faces.Count} polygons");
        bodyList.SelectedIndex = current;
        boneList.Items.Clear();
        if (current >= 0)
        {
            var body = Current;
            bodyGenericBox.Text = bodies[current].Generic.ToString(); bodyNameBox.Text = bodies[current].Name;
            for (var i = 0; i < body.Bones.Count; i++)
            {
                var b = body.Bones[i];
                var colours = body.Faces.Where(f => f.Points.All(p => p >= b.Start && p < b.Start + b.Count)).Select(f => f.Colour).Distinct().OrderBy(c => c).ToList();
                boneList.Items.Add($"bone {i,2}: from {(b.Parent < 0 ? "-" : $"bone {b.Parent} point {b.Pivot}")}, points {b.Start}-{b.Start + b.Count - 1}" +
                                   (colours.Count > 0 ? $", colours {string.Join(",", colours.Take(5))}" : ""));
            }
            boneList.SelectedIndex = selectBone ?? -1;
        }
        listing = false;
        ShowPoints();
        Pose();
    }

    private void ShowPoints()
    {
        pointList.Items.Clear();
        if (current < 0 || boneList.SelectedIndex < 0) return;
        var body = Current;
        var b = body.Bones[boneList.SelectedIndex];
        var world = body.World();
        for (var p = b.Start; p < b.Start + b.Count; p++) pointList.Items.Add($"point {p,3}: at {world[p].X:0}, {world[p].Y:0}, {world[p].Z:0}");
    }

    // ---- animations

    private void NewAnim()
    {
        if (bodies.Count == 0) throw new InvalidOperationException("Start the body first: an animation moves its bones.");
        var g = Whole(animGenericBox, "Animation number", 0, 65535);
        if (anims.Any(a => a.Generic == g)) throw new InvalidOperationException($"The character has an animation numbered {g} already.");
        var bones = bodies[0].Body.Bones.Count;
        var name = (animNameBox.Text ?? "").Trim();
        CharacterAnim anim;
        if ((copyAnimBox.Text ?? "").Trim().Length > 0)
        {
            var n = Whole(copyAnimBox, "Copy of game animation", 0);
            var hqr = HqrArchive.Open(Path.Combine(directory, "ANIM.HQR"));
            if (n >= hqr.Slots - 1) throw new InvalidOperationException($"ANIM.HQR has {hqr.Slots - 1} animations (0 to {hqr.Slots - 2}).");
            anim = CharacterAnim.Read(hqr.Read(n), bones, g, name);
        }
        else anim = CharacterAnim.Still(bones, g, name);
        anims.Add(anim);
        currentAnim = anims.Count - 1; currentKey = 0;
        Changed(); ListAnims();
        Say($"Animation {g} ({name}): {anim.Keys.Count} keyframes.");
    }

    private void SetAnimName()
    {
        var a = Anim() ?? throw new InvalidOperationException("Choose an animation first.");
        var g = Whole(animGenericBox, "Animation number", 0, 65535);
        if (anims.Any(x => x != a && x.Generic == g)) throw new InvalidOperationException($"Another animation is number {g}.");
        a.Generic = g; a.Name = (animNameBox.Text ?? "").Trim();
        Changed(); ListAnims();
    }

    private void RemoveAnim()
    {
        if (Anim() is null) return;
        anims.RemoveAt(currentAnim);
        currentAnim = anims.Count > 0 ? 0 : -1; currentKey = 0;
        Changed(); ListAnims();
    }

    private void AddKey()
    {
        var a = Anim() ?? throw new InvalidOperationException("Choose an animation first.");
        var k = Key() ?? a.Keys[^1];
        a.Keys.Insert(currentKey + 1, k.Copy());
        currentKey++;
        if (a.LoopFrame >= currentKey && a.LoopFrame > 0) a.LoopFrame++;
        Changed(); ShowAnim();
    }

    private void RemoveKey()
    {
        var a = Anim() ?? throw new InvalidOperationException("Choose an animation first.");
        if (a.Keys.Count <= 1) throw new InvalidOperationException("An animation has one keyframe at least.");
        a.Keys.RemoveAt(currentKey);
        if (a.LoopFrame >= a.Keys.Count || a.LoopFrame > currentKey) a.LoopFrame = Math.Max(0, a.LoopFrame - 1);
        currentKey = Math.Min(currentKey, a.Keys.Count - 1);
        Changed(); ShowAnim();
    }

    private void SetLoop()
    {
        var a = Anim() ?? throw new InvalidOperationException("Choose an animation first.");
        a.LoopFrame = Math.Max(0, currentKey);
        Changed(); ShowAnim();
    }

    private void SetKeyTiming()
    {
        var k = Key() ?? throw new InvalidOperationException("Choose a keyframe first.");
        k.Milliseconds = Whole(msBox, "Milliseconds", 1, 60000);
        k.Step = new Vector3(Value(stepXBox, "Move X"), Value(stepYBox, "Move Y"), Value(stepZBox, "Move Z"));
        // (the master record's gravity bit: 2)
        k.Bones[0] = ((k.Bones[0].Type & ~2) | (airBox.IsChecked == true ? 2 : 0), k.Bones[0].Value);
        Changed(); ShowAnim();
    }

    private void SetBone()
    {
        var k = Key() ?? throw new InvalidOperationException("Choose a keyframe first.");
        SetBoneIn(k);
        Changed(); ShowAnim(keepPose: true);
    }

    private void SetBoneEverywhere()
    {
        var a = Anim() ?? throw new InvalidOperationException("Choose an animation first.");
        foreach (var k in a.Keys) SetBoneIn(k);
        Changed(); ShowAnim(keepPose: true);
    }

    private void SetBoneIn(CharacterAnim.Key k)
    {
        var b = Whole(poseBoneBox, "Bone", 1, k.Bones.Count - 1);
        var type = k.Bones[b].Type;
        k.Bones[b] = (type, new Vector3(Value(poseXBox, "Turn X"), Value(poseYBox, "Turn Y"), Value(poseZBox, "Turn Z")));
    }

    private void ListAnims()
    {
        listing = true;
        animList.Items.Clear();
        foreach (var a in anims) animList.Items.Add($"{a.Generic,5}: {a.Name} -- {a.Keys.Count} keyframes, {a.TotalMilliseconds} ms, loops to {a.LoopFrame}");
        animList.SelectedIndex = currentAnim;
        listing = false;
        ShowAnim();
    }

    private void ShowAnim(bool keepPose = false)
    {
        listing = true;
        var poseIndex = poseList.SelectedIndex;
        keyList.Items.Clear(); poseList.Items.Clear();
        if (Anim() is { } a)
        {
            animGenericBox.Text = a.Generic.ToString(); animNameBox.Text = a.Name;
            for (var i = 0; i < a.Keys.Count; i++)
            {
                var k = a.Keys[i];
                keyList.Items.Add($"keyframe {i}: {k.Milliseconds} ms, moves {k.Step.X:0},{k.Step.Y:0},{k.Step.Z:0}{((k.Bones[0].Type & 2) != 0 ? "  (in the air)" : "")}{(i == a.LoopFrame ? "  (loops back here)" : "")}");
            }
            if (currentKey < 0 || currentKey >= a.Keys.Count) currentKey = 0;
            keyList.SelectedIndex = currentKey;
            if (animList.SelectedIndex != currentAnim) animList.SelectedIndex = currentAnim;
            animList.Items[currentAnim] = $"{a.Generic,5}: {a.Name} -- {a.Keys.Count} keyframes, {a.TotalMilliseconds} ms, loops to {a.LoopFrame}";
            animList.SelectedIndex = currentAnim;
        }
        listing = false;
        ShowKey(keepPose ? poseIndex : -1);
    }

    private void ShowKey(int selectPose = -1)
    {
        listing = true;
        poseList.Items.Clear();
        if (Key() is { } k)
        {
            msBox.Text = k.Milliseconds.ToString(); airBox.IsChecked = (k.Bones[0].Type & 2) != 0; stepXBox.Text = Num(k.Step.X); stepYBox.Text = Num(k.Step.Y); stepZBox.Text = Num(k.Step.Z);
            for (var b = 1; b < k.Bones.Count; b++)
            {
                var (type, v) = k.Bones[b];
                poseList.Items.Add((type & 1) != 0 ? $"bone {b,2}: shifted {v.X:0}, {v.Y:0}, {v.Z:0}" : $"bone {b,2}: turned {Num(v.X)}, {Num(v.Y)}, {Num(v.Z)}");
            }
            poseList.SelectedIndex = selectPose;
        }
        listing = false;
        Pose();
    }

    // ---- the preview

    private void Pose()
    {
        if (current < 0 || current >= bodies.Count) { preview.Model = null; preview.Invalidate(); return; }
        var body = bodies[current].Body;
        preview.Model = new Generated(body, palette, 0, "", "");
        if (Anim() is { } a)
        {
            a.FitBones(body.Bones.Count);
            var bones = playBox.IsChecked == true ? a.PoseAt(playedMs) : a.PoseOf(Math.Clamp(currentKey, 0, a.Keys.Count - 1));
            try { preview.Pose = LBAAssembler.Lba1.Lba1Pose.World(body, bones); }
            catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException) { preview.Pose = null; }
        }
        else preview.Pose = null;
        preview.Invalidate();
    }
}
