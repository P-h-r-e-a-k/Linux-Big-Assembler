using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using LBAAssembler.Terrain;

namespace LBAAssembler;

// The race track generator's 3D view (the user, 2026-10-27: "enhance map generation to allow us to easily draw track in the air, perhaps
// we should allow the map image to be rotatable?"): the island as a surface with its map on it -- and what is drawn on the ground -- seen
// from any side; what is drawn in the air standing at its height, its shadow on the ground under it; a see-through plane at the pens'
// height, which the pens draw on (at height 0, the ground itself). The right mouse button turns the view, Shift with it moves it, the
// wheel brings it nearer. One unit is a cell of the island; heights as the game's (512 to a cell).
internal sealed class Track3DView : Border
{
    private readonly Viewport3D viewport = new();
    private readonly PerspectiveCamera camera = new() { FieldOfView = 45, NearPlaneDistance = 1, FarPlaneDistance = 20000 };
    private readonly GeometryModel3D terrain = new();
    private readonly GeometryModel3D plane = new();
    private readonly Model3DGroup air = new();
    private readonly IslandFile island;
    private readonly int originX, originZ, cellsW, cellsH, scale;
    // the map with what is drawn on the ground on it (BGRA, cellsW * scale wide); what is drawn in the air (map pixels, kind, height)
    private readonly Func<byte[]> texture;
    private readonly Func<IEnumerable<(int X, int Y, SketchKind Kind, float Height)>> inAir;
    // drawing: a stroke from one map pixel to the next (pixels of the map); Begin before a stroke (for undo)
    public Action<Point, Point>? Draw { get; set; }
    public Action? Begin { get; set; }
    public Action<double?>? GroundUnderMouse { get; set; }
    private double yaw = 0, pitch = 55, distance;
    private Point3D target;
    private double planeHeight;
    private Point? rotating, lastDraw;
    private DateTime lastRefresh;

    public Track3DView(IslandFile island, int originX, int originZ, int cellsW, int cellsH, int scale, Func<byte[]> texture,
        Func<IEnumerable<(int X, int Y, SketchKind Kind, float Height)>> inAir)
    {
        this.island = island; this.originX = originX; this.originZ = originZ; this.cellsW = cellsW; this.cellsH = cellsH; this.scale = scale;
        this.texture = texture; this.inAir = inAir;
        Background = Brushes.Black;
        ClipToBounds = true;
        viewport.Camera = camera;
        var lights = new Model3DGroup();
        lights.Children.Add(new AmbientLight(Color.FromRgb(150, 150, 150)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(140, 140, 140), new Vector3D(-0.4, -1, -0.3)));
        viewport.Children.Add(new ModelVisual3D { Content = lights });
        viewport.Children.Add(new ModelVisual3D { Content = terrain });
        viewport.Children.Add(new ModelVisual3D { Content = air });
        viewport.Children.Add(new ModelVisual3D { Content = plane });
        Child = viewport;
        BuildTerrain();
        target = new Point3D(cellsW / 2.0, 0, cellsH / 2.0);
        distance = Math.Max(cellsW, cellsH) * 1.3;
        Camera();
        Refresh();
        SetPlane(0);
        MouseDown += Down; MouseMove += Moved; MouseUp += Up; MouseWheel += Wheel;
        Cursor = Cursors.Pen;
    }

    private double Ground(double x, double z) => Math.Max(0, IslandOps.Altitude(island, (originX + x) * 512, (originZ + z) * 512) ?? 0) / 512;

    // the island's surface, a vertex every few cells (the map on it: Refresh)
    private void BuildTerrain()
    {
        var step = Math.Max(2, Math.Max(cellsW, cellsH) / 160);
        int nx = cellsW / step + 1, nz = cellsH / step + 1;
        var mesh = new MeshGeometry3D();
        for (var j = 0; j < nz; j++)
            for (var i = 0; i < nx; i++)
            {
                double x = Math.Min(cellsW, i * step), z = Math.Min(cellsH, j * step);
                mesh.Positions.Add(new Point3D(x, Ground(x, z), z));
                mesh.TextureCoordinates.Add(new Point(x / cellsW, z / cellsH));
            }
        for (var j = 0; j + 1 < nz; j++)
            for (var i = 0; i + 1 < nx; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(c); mesh.TriangleIndices.Add(b);
                mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(c); mesh.TriangleIndices.Add(d);
            }
        terrain.Geometry = mesh;
    }

    // The map (with what is drawn on the ground) on the surface again, and what is drawn in the air made again
    public void Refresh()
    {
        var px = texture();
        var bmp = BitmapSource.Create(cellsW * scale, cellsH * scale, 96, 96, PixelFormats.Bgra32, null, px, cellsW * scale * 4);
        bmp.Freeze();
        var brush = new ImageBrush(bmp) { ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 1, 1) };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor);
        terrain.Material = new DiffuseMaterial(brush);
        terrain.BackMaterial = terrain.Material;
        // (in the air: a square a cell across at its height for each cell drawn, in the pen's colour; its shadow on the ground under it)
        air.Children.Clear();
        var byKind = new Dictionary<SketchKind, MeshGeometry3D>();
        var shadow = new MeshGeometry3D();
        var seen = new HashSet<(int, int)>();
        foreach (var (x, y, kind, height) in inAir())
        {
            int cx = x / scale, cz = y / scale;
            if (!seen.Add((cx, cz))) continue;
            if (!byKind.TryGetValue(kind, out var mesh)) byKind[kind] = mesh = new MeshGeometry3D();
            Square(mesh, cx, cz, height / 512.0 + 0.05);
            Square(shadow, cx, cz, Ground(cx + 0.5, cz + 0.5) + 0.08);
        }
        air.Children.Add(new GeometryModel3D(shadow, new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)))));
        foreach (var (kind, mesh) in byKind)
        {
            var (r, g, b) = TrackSketch.Colour(kind);
            var m = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(r, g, b)));
            air.Children.Add(new GeometryModel3D(mesh, m) { BackMaterial = m });
        }
        lastRefresh = DateTime.Now;
    }

    private static void Square(MeshGeometry3D mesh, double x, double z, double y)
    {
        var i = mesh.Positions.Count;
        mesh.Positions.Add(new Point3D(x, y, z)); mesh.Positions.Add(new Point3D(x + 1, y, z));
        mesh.Positions.Add(new Point3D(x, y, z + 1)); mesh.Positions.Add(new Point3D(x + 1, y, z + 1));
        mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 2); mesh.TriangleIndices.Add(i + 1);
        mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i + 2); mesh.TriangleIndices.Add(i + 3);
    }

    // The height plane at `height` (world units; 0: none -- the pens draw on the ground)
    public void SetPlane(double height)
    {
        planeHeight = height;
        var y = height / 512.0;
        var mesh = new MeshGeometry3D();
        const double margin = 40;
        mesh.Positions.Add(new Point3D(-margin, y, -margin)); mesh.Positions.Add(new Point3D(cellsW + margin, y, -margin));
        mesh.Positions.Add(new Point3D(-margin, y, cellsH + margin)); mesh.Positions.Add(new Point3D(cellsW + margin, y, cellsH + margin));
        mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(1);
        mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(3);
        plane.Geometry = mesh;
        var m = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb((byte)(height > 0 ? 70 : 1), 80, 170, 255)));
        plane.Material = m; plane.BackMaterial = m;
    }

    private void Camera()
    {
        double yr = yaw * Math.PI / 180, pr = pitch * Math.PI / 180;
        var offset = new Vector3D(Math.Sin(yr) * Math.Cos(pr), Math.Sin(pr), Math.Cos(yr) * Math.Cos(pr)) * distance;
        camera.Position = target + offset;
        camera.LookDirection = -offset;
        camera.UpDirection = new Vector3D(0, 1, 0);
    }

    // Where the mouse is on the height plane (or the ground, at height 0): the cell, or null
    private Point3D? Under(Point at)
    {
        Point3D? found = null;
        var wantPlane = planeHeight > 0;
        VisualTreeHelper.HitTest(viewport, null, r =>
        {
            if (r is RayMeshGeometry3DHitTestResult hit && ReferenceEquals(hit.ModelHit, wantPlane ? plane : terrain)) { found = hit.PointHit; return HitTestResultBehavior.Stop; }
            return HitTestResultBehavior.Continue;
        }, new PointHitTestParameters(at));
        return found;
    }

    private void Down(object sender, MouseButtonEventArgs e)
    {
        Focus();
        if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle) { rotating = e.GetPosition(this); CaptureMouse(); return; }
        if (e.ChangedButton != MouseButton.Left || Under(e.GetPosition(this)) is not { } p) return;
        Begin?.Invoke();
        var at = new Point(p.X * scale, p.Z * scale);
        Draw?.Invoke(at, at);
        lastDraw = at;
        CaptureMouse();
    }

    private void Moved(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        var under = Under(pos);
        GroundUnderMouse?.Invoke(under is { } u ? Ground(u.X, u.Z) * 512 : null);
        if (rotating is { } from && (e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed))
        {
            var d = pos - from;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || e.MiddleButton == MouseButtonState.Pressed)
            {
                // (moved: along the ground, as far as the view is wide)
                var yr = yaw * Math.PI / 180; var k = distance / Math.Max(1, ActualWidth);
                target += new Vector3D(-Math.Cos(yr) * d.X * k - Math.Sin(yr) * d.Y * k, 0, Math.Sin(yr) * d.X * k - Math.Cos(yr) * d.Y * k);
            }
            else { yaw -= d.X * 0.4; pitch = Math.Clamp(pitch + d.Y * 0.3, 8, 89); }
            rotating = pos;
            Camera();
            return;
        }
        if (lastDraw is { } last && e.LeftButton == MouseButtonState.Pressed && under is { } p)
        {
            var at = new Point(p.X * scale, p.Z * scale);
            Draw?.Invoke(last, at);
            lastDraw = at;
            if ((DateTime.Now - lastRefresh).TotalMilliseconds > 120) Refresh();
        }
    }

    private void Up(object sender, MouseButtonEventArgs e)
    {
        if (lastDraw is not null) Refresh();
        rotating = null; lastDraw = null;
        ReleaseMouseCapture();
    }

    private void Wheel(object sender, MouseWheelEventArgs e)
    {
        distance = Math.Clamp(distance * (e.Delta > 0 ? 0.88 : 1 / 0.88), 20, Math.Max(cellsW, cellsH) * 4);
        Camera();
        e.Handled = true;
    }
}
