using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LBAAssembler;

// Build > Nuke's animation, over the scene view. It is a picture of the view, not the scene itself: Cover puts the view as it was on
// top of it, the scene is emptied and drawn again underneath, and Detonate then compares the two pictures -- what changed is what was
// there and is gone -- and blows it apart: the changed parts of the old picture break into shards that fly from a wave of explosions
// across the scene, a flash and a shockwave go off at its middle, and the smoke clears on the empty scene. A click or Esc skips it.
// Several scenes at once go off as a chain reaction: a charge for each (where it is in the view, and when it goes off), each part of the
// picture blowing up with the charge nearest to it, each charge with a shockwave of its own, the flash after the last.
internal sealed class NukeOverlay : FrameworkElement
{
    public readonly record struct Charge(Point At, double Delay);

    private const double Gravity = 1500;           // DIPs a second, squared
    private const double FlashAfter = 0.25;        // seconds after the last explosion
    private const int MaxShards = 2600;

    private readonly BitmapSource before;
    private BitmapSource? after;
    private ImageBrush? beforeBrush;
    private readonly Random random = new(7);
    private readonly Stopwatch clock = new();
    private readonly List<Shard> shards = new();
    private readonly List<Blast> blasts = new();
    private readonly List<Puff> smoke = new();
    private readonly List<Spark> sparks = new();
    private Point epicentre;
    private double flashAt, endAt, lastFrame;
    private readonly List<(Point At, double Time, double Radius)> rings = new();        // the chain's shockwaves, one a charge
    private TaskCompletionSource<bool>? done;

    private sealed class Shard
    {
        public required StreamGeometry Shape;
        public Point Centre;
        public Vector Velocity;
        public double Spin, Launch, Life, Angle;
        public Vector Moved;
    }
    private sealed record Blast(Point At, double Radius, double Time);
    private sealed class Puff { public Point At; public Vector Drift; public double Radius, Grow, Time, Life; }
    private sealed class Spark { public Point At; public Vector Velocity; public double Time, Life; }

    private NukeOverlay(BitmapSource before)
    {
        this.before = before;
        ClipToBounds = true;
        Focusable = true;
        Cursor = Cursors.Wait;
    }

    // Covers `view` with `before` (a picture of it) until Detonate: beside it in its parent panel, in its own grid cell and on top, so
    // a picture of the view taken meanwhile (Snapshot) is of the view alone.
    public static NukeOverlay Cover(FrameworkElement view, BitmapSource before)
    {
        if (view.Parent is not Panel parent) throw new InvalidOperationException("The view has no panel to put the nuke's picture in.");
        var overlay = new NukeOverlay(before);
        Grid.SetRow(overlay, Grid.GetRow(view)); Grid.SetColumn(overlay, Grid.GetColumn(view));
        Grid.SetRowSpan(overlay, Grid.GetRowSpan(view)); Grid.SetColumnSpan(overlay, Grid.GetColumnSpan(view));
        Panel.SetZIndex(overlay, 1000);
        parent.Children.Add(overlay);
        return overlay;
    }

    // Takes the cover away without blowing anything up (the nuke failed).
    public void Remove()
    {
        CompositionTarget.Rendering -= OnFrame;
        (Parent as Panel)?.Children.Remove(this);
        done?.TrySetResult(false);
    }

    // A picture of an element as it is on screen (the view, before and after), at the screen's own pixels.
    public static BitmapSource Snapshot(FrameworkElement element)
    {
        var dpi = VisualTreeHelper.GetDpi(element);
        int w = Math.Max(1, (int)Math.Round(element.ActualWidth * dpi.DpiScaleX)), h = Math.Max(1, (int)Math.Round(element.ActualHeight * dpi.DpiScaleY));
        var bitmap = new RenderTargetBitmap(w, h, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        // (drawn through a brush so the picture is the element where it stands, wherever its parent puts it)
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                                                            null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    // Blows up what is in `before` and not in `after` (the view drawn again, the same size; null: the cover just lifts), then removes
    // itself; `charges` (two or more): a chain reaction. Completes when the animation ends or is skipped.
    public Task Detonate(BitmapSource? afterPicture, IReadOnlyList<Charge>? charges = null)
    {
        done = new TaskCompletionSource<bool>();
        after = afterPicture;
        if (after is null || ActualWidth < 4 || ActualHeight < 4) { Finish(); return done.Task; }
        Prepare(charges is { Count: > 1 } ? charges : null);
        if (blasts.Count == 0) { Finish(); return done.Task; }
        MouseDown += (_, _) => Finish();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Finish(); };
        Keyboard.Focus(this);
        clock.Start();
        CompositionTarget.Rendering += OnFrame;
        return done.Task;
    }

    // ---- what blows up ------------------------------------------------------------------------------------------------------------

    private void Prepare(IReadOnlyList<Charge>? charges)
    {
        double w = ActualWidth, h = ActualHeight;
        int pw = before.PixelWidth, ph = before.PixelHeight;
        var a = Pixels(before, pw, ph);
        var b = Pixels(after!, pw, ph);
        double sx = w / pw, sy = h / ph;                                   // pixels to DIPs

        // which fine tiles changed (a tile whose pixels moved by more than a little on the whole, or a good part of them by a lot)
        const int fine = 8;
        int fw = (pw + fine - 1) / fine, fh = (ph + fine - 1) / fine;
        var changed = new bool[fw * fh];
        var count = 0;
        for (var ty = 0; ty < fh; ty++)
        for (var tx = 0; tx < fw; tx++)
        {
            long sum = 0; int n = 0, strong = 0;
            for (var y = ty * fine; y < Math.Min(ph, ty * fine + fine); y++)
            for (var x = tx * fine; x < Math.Min(pw, tx * fine + fine); x++)
            {
                int p = a[y * pw + x], q = b[y * pw + x];
                var d = Math.Abs((p & 255) - (q & 255)) + Math.Abs((p >> 8 & 255) - (q >> 8 & 255)) + Math.Abs((p >> 16 & 255) - (q >> 16 & 255));
                sum += d; n++; if (d > 60) strong++;
            }
            if (n > 0 && (sum / n > 18 || strong * 4 > n)) { changed[ty * fw + tx] = true; count++; }
        }
        if (count == 0) return;

        // the shards: the changed part cut into tiles (as small as the shard budget allows), each split into two triangles
        var size = Math.Max(fine * 2, (int)Math.Ceiling(Math.Sqrt(count * (double)fine * fine * 2 / MaxShards) / fine) * fine);
        var per = size / fine;
        var centres = new List<Point>();
        for (var ty = 0; ty * size < ph; ty++)
        for (var tx = 0; tx * size < pw; tx++)
        {
            var hit = 0;
            for (var y = ty * per; y < Math.Min(fh, ty * per + per); y++)
            for (var x = tx * per; x < Math.Min(fw, tx * per + per); x++)
                if (changed[y * fw + x]) hit++;
            if (hit * 3 < per * per) continue;
            double x0 = tx * size * sx, y0 = ty * size * sy, x1 = Math.Min(pw, (tx + 1) * size) * sx, y1 = Math.Min(ph, (ty + 1) * size) * sy;
            // (corners nudged a little so the pieces aren't all square)
            Point Jitter(double x, double y) => new(x + (random.NextDouble() - 0.5) * size * sx * 0.3, y + (random.NextDouble() - 0.5) * size * sy * 0.3);
            Point p00 = new(x0, y0), p10 = new(x1, y0), p01 = new(x0, y1), p11 = new(x1, y1), mid = Jitter((x0 + x1) / 2, (y0 + y1) / 2);
            foreach (var (p, q) in new[] { (p00, p10), (p10, p11), (p11, p01), (p01, p00) })
                AddShard(p, q, mid);
            centres.Add(new Point((x0 + x1) / 2, (y0 + y1) / 2));
        }
        if (shards.Count == 0) return;

        // the explosions: one where each patch of the scene stood (a coarse grid of the shards), in a wave out from the middle -- or, in a
        // chain reaction, out from the middle of each charge's part, when that charge goes off
        epicentre = new Point(centres.Average(c => c.X), centres.Average(c => c.Y));
        var cell = Math.Max(w, h) / 7;
        var patches = centres.GroupBy(c => ((int)(c.X / cell), (int)(c.Y / cell)))
            .Select(g => (At: new Point(g.Average(c => c.X), g.Average(c => c.Y)), Count: g.Count())).ToList();
        var view = new Rect(0, 0, w, h);
        if (charges is not null && view.Contains(charges[0].At)) epicentre = charges[0].At;
        var mine = patches.Select(p => charges is null ? -1 : Enumerable.Range(0, charges.Count).MinBy(k => (charges[k].At - p.At).LengthSquared)).ToList();
        for (var i = 0; i < patches.Count; i++)
        {
            var (at, n) = patches[i];
            var (from, delay, wave) = mine[i] < 0 ? (epicentre, 0.0, 1.1) : (charges![mine[i]].At, charges[mine[i]].Delay, 0.9);
            var far = Enumerable.Range(0, patches.Count).Where(j => mine[j] == mine[i]).Max(j => (patches[j].At - from).Length) + 1;
            var jittered = new Point(at.X + (random.NextDouble() - 0.5) * cell * 0.4, at.Y + (random.NextDouble() - 0.5) * cell * 0.4);
            var radius = Math.Clamp(Math.Sqrt(n) * size * sx * 1.1, cell * 0.35, cell * 0.9);
            blasts.Add(new Blast(jittered, radius, delay + 0.15 + wave * (at - from).Length / far + random.NextDouble() * 0.15));
        }
        // each charge that blows something up here: its shockwave, as it goes off
        if (charges is not null)
            foreach (var k in mine.Distinct())
            {
                var reach = Enumerable.Range(0, patches.Count).Where(j => mine[j] == k).Max(j => (patches[j].At - charges[k].At).Length);
                rings.Add((charges[k].At, charges[k].Delay + 0.12, Math.Clamp(reach * 1.4, cell * 0.8, Math.Max(w, h) * 0.6)));
            }
        var last = blasts.Max(x => x.Time);
        flashAt = last + FlashAfter;

        // each shard flies from its nearest explosion, when it goes off
        var speed = Math.Max(w, h) * 0.9;
        foreach (var s in shards)
        {
            var blast = blasts.MinBy(x => (x.At - s.Centre).LengthSquared)!;
            var away = s.Centre - blast.At;
            if (away.Length < 1) away = new Vector(random.NextDouble() - 0.5, -1);
            away.Normalize();
            var push = speed * (0.35 + random.NextDouble() * 0.65) * Math.Clamp(1.4 - (s.Centre - blast.At).Length / (blast.Radius * 3), 0.4, 1.4);
            s.Velocity = away * push + new Vector((random.NextDouble() - 0.5) * speed * 0.2, -speed * (0.2 + random.NextDouble() * 0.35));
            s.Spin = (random.NextDouble() - 0.5) * 1440;
            s.Launch = blast.Time + random.NextDouble() * 0.08;
            s.Life = 1.0 + random.NextDouble() * 0.9;
        }
        // smoke and sparks for each explosion
        foreach (var blast in blasts)
        {
            for (var k = 0; k < 4; k++)
                smoke.Add(new Puff
                {
                    At = blast.At + new Vector((random.NextDouble() - 0.5) * blast.Radius, (random.NextDouble() - 0.5) * blast.Radius * 0.6),
                    Drift = new Vector((random.NextDouble() - 0.5) * 30, -40 - random.NextDouble() * 50),
                    Radius = blast.Radius * (0.4 + random.NextDouble() * 0.3), Grow = blast.Radius * (0.6 + random.NextDouble() * 0.5),
                    Time = blast.Time + 0.15 + random.NextDouble() * 0.3, Life = 2.0 + random.NextDouble() * 1.2,
                });
            for (var k = 0; k < 14; k++)
            {
                var angle = random.NextDouble() * Math.PI * 2;
                sparks.Add(new Spark
                {
                    At = blast.At, Time = blast.Time,
                    Velocity = new Vector(Math.Cos(angle), Math.Sin(angle) - 0.6) * speed * (0.5 + random.NextDouble()),
                    Life = 0.5 + random.NextDouble() * 0.6,
                });
            }
        }
        endAt = Math.Max(flashAt + 1.6, Math.Max(shards.Max(s => s.Launch + s.Life), smoke.Max(p => p.Time + p.Life)));
        beforeBrush = new ImageBrush(before) { ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, w, h), Stretch = Stretch.Fill };
        beforeBrush.Freeze();
    }

    private void AddShard(Point a, Point b, Point c)
    {
        var shape = new StreamGeometry();
        using (var g = shape.Open()) { g.BeginFigure(a, true, true); g.LineTo(b, true, false); g.LineTo(c, true, false); }
        shape.Freeze();
        shards.Add(new Shard { Shape = shape, Centre = new Point((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3) });
    }

    private static int[] Pixels(BitmapSource source, int w, int h)
    {
        BitmapSource s = source.Format == PixelFormats.Bgra32 || source.Format == PixelFormats.Pbgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        if (s.PixelWidth != w || s.PixelHeight != h)
            s = new TransformedBitmap(s, new ScaleTransform(w / (double)s.PixelWidth, h / (double)s.PixelHeight));
        var pixels = new int[w * h];
        s.CopyPixels(pixels, w * 4, 0);
        return pixels;
    }

    // ---- the animation ------------------------------------------------------------------------------------------------------------

    private void OnFrame(object? sender, EventArgs e)
    {
        var t = clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(t - lastFrame, 0, 0.05);
        lastFrame = t;
        foreach (var s in shards)
        {
            if (t < s.Launch) continue;
            s.Moved += s.Velocity * dt;
            s.Velocity += new Vector(0, Gravity * dt);
            s.Angle += s.Spin * dt;
        }
        foreach (var p in sparks) if (t >= p.Time) { p.At += p.Velocity * dt; p.Velocity += new Vector(0, Gravity * dt); }
        foreach (var p in smoke) if (t >= p.Time) p.At += p.Drift * dt;
        if (t >= endAt) { Finish(); return; }
        InvalidateVisual();
    }

    private void Finish()
    {
        if (done is null || done.Task.IsCompleted) { (Parent as Panel)?.Children.Remove(this); return; }
        CompositionTarget.Rendering -= OnFrame;
        clock.Stop();
        (Parent as Panel)?.Children.Remove(this);
        done.TrySetResult(true);
    }

    private static readonly Brush FireBrush = Frozen(new RadialGradientBrush(new GradientStopCollection
    {
        new(Color.FromArgb(255, 255, 255, 230), 0.0), new(Color.FromArgb(240, 255, 230, 120), 0.25), new(Color.FromArgb(210, 255, 150, 40), 0.5),
        new(Color.FromArgb(140, 220, 60, 20), 0.75), new(Color.FromArgb(0, 120, 20, 10), 1.0),
    }));
    private static readonly Brush SmokeBrush = Frozen(new RadialGradientBrush(new GradientStopCollection
    {
        new(Color.FromArgb(170, 60, 55, 52), 0.0), new(Color.FromArgb(120, 70, 66, 62), 0.6), new(Color.FromArgb(0, 80, 76, 72), 1.0),
    }));
    private static readonly Brush SparkBrush = Frozen(new SolidColorBrush(Color.FromRgb(255, 225, 140)));
    private static readonly Brush FlashBrush = Frozen(new SolidColorBrush(Color.FromRgb(255, 250, 235)));
    private static readonly Brush GlowBrush = Frozen(new SolidColorBrush(Color.FromArgb(150, 255, 120, 30)));
    private static T Frozen<T>(T brush) where T : Freezable { brush.Freeze(); return brush; }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        var t = clock.IsRunning || clock.Elapsed > TimeSpan.Zero ? clock.Elapsed.TotalSeconds : -1;
        if (t < 0 || beforeBrush is null)
        {
            // (covering: the view as it was)
            dc.DrawImage(before, new Rect(0, 0, w, h));
            return;
        }
        // the shaking: every explosion shakes the view for a moment, the flash most
        var shake = 0.0;
        foreach (var b in blasts) if (t >= b.Time) shake += 7 * Math.Exp(-(t - b.Time) * 7);
        foreach (var r in rings) if (t >= r.Time) shake += 6 * Math.Exp(-(t - r.Time) * 6);
        if (t >= flashAt) shake += 16 * Math.Exp(-(t - flashAt) * 4);
        shake = Math.Min(shake, 18);
        dc.PushTransform(new TranslateTransform((random.NextDouble() - 0.5) * 2 * shake, (random.NextDouble() - 0.5) * 2 * shake));
        dc.DrawImage(after, new Rect(0, 0, w, h));

        // the pieces of what was there: at rest until their explosion, then flying, turning and falling, glowing as they go
        foreach (var s in shards)
        {
            var flying = t - s.Launch;
            if (flying > s.Life) continue;
            if (flying <= 0) { dc.DrawGeometry(beforeBrush, null, s.Shape); continue; }
            var m = Matrix.Identity;
            m.RotateAt(s.Angle, s.Centre.X, s.Centre.Y);
            m.Translate(s.Moved.X, s.Moved.Y);
            dc.PushTransform(new MatrixTransform(m));
            var fade = flying > s.Life - 0.4 ? (s.Life - flying) / 0.4 : 1;
            dc.PushOpacity(fade);
            dc.DrawGeometry(beforeBrush, null, s.Shape);
            if (flying < 0.5) { dc.PushOpacity(1 - flying / 0.5); dc.DrawGeometry(GlowBrush, null, s.Shape); dc.Pop(); }
            dc.Pop(); dc.Pop();
        }
        // smoke, under the fire
        foreach (var p in smoke)
        {
            var age = t - p.Time;
            if (age < 0 || age > p.Life) continue;
            var f = age / p.Life;
            dc.PushOpacity(Math.Min(1, age * 4) * (1 - f));
            var r = p.Radius + p.Grow * Math.Sqrt(f);
            dc.DrawEllipse(SmokeBrush, null, p.At, r, r * 0.85);
            dc.Pop();
        }
        // the fireballs: swelling fast, then burning out
        foreach (var b in blasts)
        {
            var age = t - b.Time;
            if (age < 0 || age > 0.8) continue;
            var grow = 1 - Math.Pow(1 - Math.Min(1, age / 0.3), 3);
            var r = b.Radius * (0.3 + 0.9 * grow) * (1 + 0.06 * Math.Sin(age * 60));
            dc.PushOpacity(age < 0.45 ? 1 : 1 - (age - 0.45) / 0.35);
            dc.DrawEllipse(FireBrush, null, b.At, r, r);
            dc.Pop();
        }
        foreach (var p in sparks)
        {
            var age = t - p.Time;
            if (age < 0 || age > p.Life) continue;
            dc.PushOpacity(1 - age / p.Life);
            dc.DrawEllipse(SparkBrush, null, p.At, 2.2, 2.2);
            dc.Pop();
        }
        // the chain's shockwaves, a charge at a time
        foreach (var (at, time, reach) in rings)
        {
            var age = (t - time) / 0.7;
            if (age < 0 || age >= 1) continue;
            var r = reach * (1 - Math.Pow(1 - age, 2));
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(200 * (1 - age)), 255, 236, 190)), 9 * (1 - age) + 1.5), at, r, r * 0.7);
        }
        // the flash and the shockwave from the middle, as the last of it goes up
        var since = t - flashAt;
        if (since >= 0)
        {
            var ring = Math.Min(1, since / 0.9);
            if (ring < 1)
            {
                var radius = (Math.Sqrt(w * w + h * h) * 0.75) * (1 - Math.Pow(1 - ring, 2));
                dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(220 * (1 - ring)), 255, 240, 200)), 14 * (1 - ring) + 2), epicentre, radius, radius * 0.7);
            }
            var flash = since < 0.08 ? since / 0.08 : Math.Max(0, 1 - (since - 0.08) / 0.6);
            if (flash > 0) { dc.PushOpacity(0.85 * flash); dc.DrawRectangle(FlashBrush, null, new Rect(-20, -20, w + 40, h + 40)); dc.Pop(); }
        }
        dc.Pop();
    }
}
