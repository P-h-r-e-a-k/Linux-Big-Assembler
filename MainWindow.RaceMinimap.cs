using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LBAAssembler;

// The minimap while a race plays (the user, 2026-10-08: "Update our mini-map for race mode so it fully fits into its viewport and shows all
// the cars moving around in real time"): the lap and the island round it scaled to fit the minimap's box -- no scrolling -- the road
// drawn over it, and every car where the engine has it, a few times a second (the control socket's racecars, RACEMOD.CPP RaceMod_Cars):
// Twinsen's yellow and bigger, the opponents' each a colour of its own. Back to the editor's own minimap when the play ends.
public partial class MainWindow
{
    private const int RaceMinimapPollMs = 150;
    private const double RaceMinimapMargin = 6;              // cells round the lap
    private static readonly Color[] RaceMinimapColours =
    {
        Color.FromRgb(230, 40, 40), Color.FromRgb(40, 200, 230), Color.FromRgb(230, 60, 220), Color.FromRgb(250, 140, 20),
        Color.FromRgb(90, 220, 60), Color.FromRgb(245, 245, 245),
    };

    private DispatcherTimer? raceMinimapTimer;
    private bool raceMinimapOn, raceMinimapBusy;
    private double raceMinimapScale = 1;
    private Rect raceMinimapArea;                            // the part of the minimap image shown (its pixels)
    private readonly Canvas raceMinimapLayer = new();          // (hit-testable: the cars' dots show their names; a click still reaches the minimap)
    private readonly Dictionary<int, Ellipse> raceMinimapCars = new();
    private Terrain.RaceTrackService.TrackInfo? raceMinimapTrack;

    // The play's race connected (StartLba2Control): the minimap goes over to the race, if it shows the race's island.
    private void StartRaceMinimap()
    {
        StopRaceMinimap();
        if (raceToPlay is null || MinimapImage.Source is null || currentIsland is null || interiorSceneActive) return;
        var track = Terrain.RaceTrackService.Raced(raceToPlay);
        var island = Terrain.RaceTrackIsland.ByName(track.Island);
        var shown = System.IO.Path.GetFileName(activeFile);
        if (!string.Equals(shown, island.IleFile, StringComparison.OrdinalIgnoreCase) && !string.Equals(shown, island.TwinIleFile, StringComparison.OrdinalIgnoreCase))
            return;
        raceMinimapTrack = track;
        raceMinimapOn = true;
        if (!MinimapContent.Children.Contains(raceMinimapLayer)) MinimapContent.Children.Add(raceMinimapLayer);
        MinimapMarkerCanvas.Children.Clear();
        MinimapActorCanvas.Visibility = Visibility.Collapsed;
        MinimapScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        MinimapScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        FitRaceMinimap();
        raceMinimapTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RaceMinimapPollMs) };
        raceMinimapTimer.Tick += async (_, _) => await PollRaceCars();
        raceMinimapTimer.Start();
    }

    private void StopRaceMinimap()
    {
        raceMinimapTimer?.Stop();
        raceMinimapTimer = null;
        if (!raceMinimapOn) return;
        raceMinimapOn = false;
        raceMinimapTrack = null;
        raceMinimapCars.Clear();
        raceMinimapLayer.Children.Clear();
        MinimapContent.Children.Remove(raceMinimapLayer);
        MinimapContent.LayoutTransform = Transform.Identity;
        MinimapActorCanvas.Visibility = Visibility.Visible;
        MinimapScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        MinimapScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        UpdateMinimapMarker();
        CenterMinimapOnMarker();
    }

    // The lap's extent (and RaceMinimapMargin round it) fitted to the minimap's box: scaled, and scrolled to it; the road drawn.
    private void FitRaceMinimap()
    {
        if (!raceMinimapOn || raceMinimapTrack is not { } track || MinimapImage.Source is not BitmapSource image) return;
        var road = RaceMinimapRoad(track);
        if (road.Count == 0) { StopRaceMinimap(); return; }
        double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
        foreach (var (x, z, _) in road.SelectMany(r => r)) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); z0 = Math.Min(z0, z); z1 = Math.Max(z1, z); }
        var margin = RaceMinimapMargin * MinimapPixelsPerCell;
        double width = image.PixelWidth, height = image.PixelHeight;
        x0 = Math.Max(0, x0 - margin); z0 = Math.Max(0, z0 - margin); x1 = Math.Min(width, x1 + margin); z1 = Math.Min(height, z1 + margin);
        // (square: the box is)
        var side = Math.Max(x1 - x0, z1 - z0);
        var cx = (x0 + x1) / 2; var cz = (z0 + z1) / 2;
        x0 = Math.Clamp(cx - side / 2, 0, Math.Max(0, width - side)); z0 = Math.Clamp(cz - side / 2, 0, Math.Max(0, height - side));
        raceMinimapArea = new Rect(x0, z0, Math.Min(side, width), Math.Min(side, height));
        var box = Math.Min(MinimapScrollViewer.Width, MinimapScrollViewer.Height);
        if (double.IsNaN(box) || box <= 0) box = 222;
        raceMinimapScale = box / Math.Max(1, side);
        MinimapContent.LayoutTransform = new ScaleTransform(raceMinimapScale, raceMinimapScale);
        MinimapContent.UpdateLayout();
        MinimapScrollViewer.ScrollToHorizontalOffset(x0 * raceMinimapScale);
        MinimapScrollViewer.ScrollToVerticalOffset(z0 * raceMinimapScale);
        // the road: its rails' width where the lap is a raised road (gaps -- the jumps -- left open), else the racing line
        raceMinimapLayer.Children.Clear();
        raceMinimapCars.Clear();
        // (indoors -- a track with open areas, the Emperor's palace's rooms -- the island's picture is its roof: a dark panel over it, the
        // rooms' floors on that, the road through them -- the user, 2026-10-16: "Clean up mini-map so it no longer shows outside")
        if (track.OpenAreas is { Count: > 0 } areas)
        {
            var panel = new Rectangle { Width = raceMinimapArea.Width, Height = raceMinimapArea.Height, Fill = new SolidColorBrush(Color.FromRgb(24, 24, 30)) };
            Canvas.SetLeft(panel, raceMinimapArea.X); Canvas.SetTop(panel, raceMinimapArea.Y);
            raceMinimapLayer.Children.Add(panel);
            var floor = new SolidColorBrush(Color.FromArgb(70, 200, 190, 160)); floor.Freeze();
            foreach (var a in areas.Where(a => a.Length >= 4))
            {
                var (ax, az) = (a[0] / MinimapWorldUnitsPerPixel - minimapCropOffsetXPixels, a[1] / MinimapWorldUnitsPerPixel - minimapCropOffsetYPixels);
                var (bx, bz) = (a[2] / MinimapWorldUnitsPerPixel - minimapCropOffsetXPixels, a[3] / MinimapWorldUnitsPerPixel - minimapCropOffsetYPixels);
                var room = new Rectangle { Width = Math.Abs(bx - ax), Height = Math.Abs(bz - az), Fill = floor };
                Canvas.SetLeft(room, Math.Min(ax, bx)); Canvas.SetTop(room, Math.Min(az, bz));
                raceMinimapLayer.Children.Add(room);
            }
        }
        var edge = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)); edge.Freeze();
        var asphalt = new SolidColorBrush(Color.FromArgb(235, 60, 60, 66)); asphalt.Freeze();
        foreach (var stretch in road)
        {
            if (stretch.Count < 2) continue;
            var points = new PointCollection(stretch.Select(p => new Point(p.X, p.Z)));
            var wide = Math.Max(2.5 / raceMinimapScale, stretch.Average(p => p.Width));
            raceMinimapLayer.Children.Add(new Polyline { Points = points, Stroke = edge, StrokeThickness = wide + 2 / raceMinimapScale, StrokeLineJoin = PenLineJoin.Round });
            raceMinimapLayer.Children.Add(new Polyline { Points = points, Stroke = asphalt, StrokeThickness = wide, StrokeLineJoin = PenLineJoin.Round });
        }
    }

    // The road in minimap pixels, stretch by stretch: the raised road's middle and width (RACETRACK.JSON Raised: world units [x, z, y, half],
    // a jump's gap a half of 0), else the racing line (Path: [x, z, ...]) four cells wide. Indoors (open areas: the rooms are no raised
    // road, a half of 0 as a gap's) the racing line, and a sprint's only as far as its finish line -- the Emperor's palace's goes on out
    // of the window in a leap over the sea, and the minimap showed that, the palace a speck in it.
    private List<List<(double X, double Z, double Width)>> RaceMinimapRoad(Terrain.RaceTrackService.TrackInfo track)
    {
        var stretches = new List<List<(double X, double Z, double Width)>>();
        var current = new List<(double X, double Z, double Width)>();
        (double, double) Px(int x, int z) => (x / MinimapWorldUnitsPerPixel - minimapCropOffsetXPixels, z / MinimapWorldUnitsPerPixel - minimapCropOffsetYPixels);
        if (track.OpenAreas is { Count: > 0 } && track.Path is { Count: > 1 } line)
        {
            var upTo = line.Count - 1;
            if (track.Dream?.Finish is { } finish)
            {
                double fx = finish.CubeX * 32768.0 + (finish.X0 + finish.X1) / 2.0, fz = finish.CubeZ * 32768.0 + (finish.Z0 + finish.Z1) / 2.0;
                upTo = Enumerable.Range(0, line.Count).OrderBy(i => (line[i][0] - fx) * (line[i][0] - fx) + (line[i][1] - fz) * (line[i][1] - fz)).First();
            }
            foreach (var p in line.Take(upTo + 1))
            {
                var (x, z) = Px(p[0], p[1]);
                current.Add((x, z, 6.0 * 512 / MinimapWorldUnitsPerPixel));
            }
            stretches.Add(current);
            return stretches;
        }
        if (track.Raised is { Count: > 1 } raised)
        {
            foreach (var p in raised)
            {
                if (p.Length < 4 || p[3] <= 0) { if (current.Count > 0) { stretches.Add(current); current = new(); } continue; }
                var (x, z) = Px(p[0], p[1]);
                current.Add((x, z, 2.0 * p[3] / MinimapWorldUnitsPerPixel));
            }
        }
        else if (track.Path is { Count: > 1 } path)
            foreach (var p in path.Append(path[0]))
            {
                var (x, z) = Px(p[0], p[1]);
                current.Add((x, z, 8.0 * 512 / MinimapWorldUnitsPerPixel));
            }
        if (current.Count > 0) stretches.Add(current);
        return stretches;
    }

    // The cars where the engine has them now.
    private async Task PollRaceCars()
    {
        if (!raceMinimapOn || raceMinimapBusy || lba2Control is not { } client) return;
        raceMinimapBusy = true;
        try
        {
            var response = await client.SendAsync("racecars");
            if (!raceMinimapOn) return;
            var seen = new HashSet<int>();
            foreach (var line in response.Split('\n'))
            {
                var parts = line.Trim().Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5 || parts[0] != "car" || !int.TryParse(parts[1], out var id) || !int.TryParse(parts[2], out var wx) || !int.TryParse(parts[3], out var wz)) continue;
                seen.Add(id);
                if (!raceMinimapCars.TryGetValue(id, out var dot))
                {
                    var hero = id < 0;
                    var size = (hero ? 9 : 7) / raceMinimapScale;
                    var fill = new SolidColorBrush(hero ? Colors.Yellow : RaceMinimapColours[((id % RaceMinimapColours.Length) + RaceMinimapColours.Length) % RaceMinimapColours.Length]);
                    fill.Freeze();
                    dot = new Ellipse { Width = size, Height = size, Fill = fill, Stroke = Brushes.Black, StrokeThickness = 1.2 / raceMinimapScale, ToolTip = parts.Length > 5 ? parts[5] : null };
                    if (hero) Panel.SetZIndex(dot, 10);
                    raceMinimapCars[id] = dot;
                    raceMinimapLayer.Children.Add(dot);
                }
                var x = wx / MinimapWorldUnitsPerPixel - minimapCropOffsetXPixels;
                var z = wz / MinimapWorldUnitsPerPixel - minimapCropOffsetYPixels;
                Canvas.SetLeft(dot, x - dot.Width / 2);
                Canvas.SetTop(dot, z - dot.Height / 2);
                dot.Visibility = Visibility.Visible;
            }
            foreach (var (id, dot) in raceMinimapCars) if (!seen.Contains(id)) dot.Visibility = Visibility.Collapsed;
        }
        catch (IOException) { }
        finally { raceMinimapBusy = false; }
    }
}
