using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LBAAssembler.Terrain;

namespace LBAAssembler;

// The race track generator's 3D view (the user, 2026-10-27: "enhance map generation to allow us to easily draw track in the air, perhaps
// we should allow the map image to be rotatable?"): the island as a surface with its map on it -- and what is drawn on the ground -- seen
// from any side; what is drawn in the air standing at its height, its shadow on the ground under it; a see-through plane at the pens'
// height, which the pens draw on (at height 0, the ground itself). The right mouse button turns the view, Shift with it moves it, the
// wheel brings it nearer. One unit is a cell of the island; heights as the game's (512 to a cell).
//
// Upstream draws this with WPF's Media3D (a Viewport3D of GeometryModel3Ds under a PerspectiveCamera, an AmbientLight and a
// DirectionalLight). Avalonia has no 3D of any kind, so the same picture is rasterized on the CPU -- z-buffered, perspective-correct,
// nearest-neighbour textures, the translucent parts blended -- into a WriteableBitmap shown in an Image, exactly as this port already
// does for the export preview (Export/ScenePreview). The camera, the lights, the materials and the meshes are the ones upstream's
// Viewport3D used, so the view matches; the mouse's ray is cast at the same mesh WPF's VisualTreeHelper.HitTest was given.
internal sealed class Track3DView : Border
{
    // WPF's PerspectiveCamera.FieldOfView: the HORIZONTAL angle. NearPlaneDistance is upstream's (FarPlaneDistance, 20000, is further
    // than this view ever gets: the camera is pulled back to four times the island's size at most).
    private const double FieldOfView = 45, NearPlane = 1;
    // The AmbientLight and the DirectionalLight upstream puts in the viewport, as fractions of full brightness, and -- a directional
    // light travels along its Direction, so what a surface is lit by is the direction back towards it -- where its light comes from.
    private const double Ambient = 150 / 255.0, Directional = 140 / 255.0;
    private static readonly Vec3 Towards = new Vec3(0.4, 1, 0.3).Normalized();

    // (the picture is drawn at the view's own size, so Fill never actually scales it; nearest-neighbour keeps a half-pixel rounding sharp)
    private readonly Image surface = new() { Stretch = Stretch.Fill };
    private WriteableBitmap? frame;
    private bool drawQueued;

    private Mesh3D terrain = new();
    private Mesh3D plane = new();
    // what is drawn in the air, in the order upstream's Model3DGroup holds it: the shadow on the ground first, then a mesh per pen
    private readonly List<(Mesh3D Mesh, Color Colour)> air = new();
    // the map on the surface, as upstream's ImageBrush over the 0..1 picture coordinates (BGRA, textureW wide)
    private byte[] terrainTexture = Array.Empty<byte>();
    private int textureW, textureH;
    private Color planeColour;

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
    private Vec3 target;
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
        // (WPF's BitmapScalingMode.NearestNeighbor: Avalonia spells the same thing BitmapInterpolationMode.None)
        RenderOptions.SetBitmapInterpolationMode(surface, BitmapInterpolationMode.None);
        Child = surface;
        BuildTerrain();
        target = new Vec3(cellsW / 2.0, 0, cellsH / 2.0);
        distance = Math.Max(cellsW, cellsH) * 1.3;
        Camera();
        Refresh();
        SetPlane(0);
        PointerPressed += Down; PointerMoved += Moved; PointerReleased += Up; PointerWheelChanged += Wheel;
        SizeChanged += (_, _) => Invalidate3D();
        // (WPF's Cursors.Pen: Avalonia's cursors are the platform's own and none of them is a pen, so the drawing crosshair the
        // generator's 2D board already uses stands in for it)
        Cursor = Cursors.Cross;
    }

    private double Ground(double x, double z) => Math.Max(0, IslandOps.Altitude(island, (originX + x) * 512, (originZ + z) * 512) ?? 0) / 512;

    // the island's surface, a vertex every few cells (the map on it: Refresh)
    private void BuildTerrain()
    {
        var step = Math.Max(2, Math.Max(cellsW, cellsH) / 160);
        int nx = cellsW / step + 1, nz = cellsH / step + 1;
        var mesh = new Mesh3D();
        for (var j = 0; j < nz; j++)
            for (var i = 0; i < nx; i++)
            {
                double x = Math.Min(cellsW, i * step), z = Math.Min(cellsH, j * step);
                mesh.Positions.Add(new Vec3(x, Ground(x, z), z));
                mesh.TextureCoordinates.Add(new Point(x / cellsW, z / cellsH));
            }
        for (var j = 0; j + 1 < nz; j++)
            for (var i = 0; i + 1 < nx; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(c); mesh.TriangleIndices.Add(b);
                mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(c); mesh.TriangleIndices.Add(d);
            }
        mesh.Smooth();
        terrain = mesh;
    }

    // The map (with what is drawn on the ground) on the surface again, and what is drawn in the air made again
    public void Refresh()
    {
        var px = texture();
        // (upstream makes a BitmapSource of it for an ImageBrush over the unit square; the rasterizer samples the pixels themselves)
        terrainTexture = px; textureW = cellsW * scale; textureH = cellsH * scale;
        // (in the air: a square a cell across at its height for each cell drawn, in the pen's colour; its shadow on the ground under it)
        air.Clear();
        var byKind = new Dictionary<SketchKind, Mesh3D>();
        var shadow = new Mesh3D();
        var seen = new HashSet<(int, int)>();
        foreach (var (x, y, kind, height) in inAir())
        {
            int cx = x / scale, cz = y / scale;
            if (!seen.Add((cx, cz))) continue;
            if (!byKind.TryGetValue(kind, out var mesh)) byKind[kind] = mesh = new Mesh3D();
            Square(mesh, cx, cz, height / 512.0 + 0.05);
            Square(shadow, cx, cz, Ground(cx + 0.5, cz + 0.5) + 0.08);
        }
        shadow.Smooth();
        air.Add((shadow, Color.FromArgb(110, 0, 0, 0)));
        foreach (var (kind, mesh) in byKind)
        {
            var (r, g, b) = TrackSketch.Colour(kind);
            mesh.Smooth();
            air.Add((mesh, Color.FromRgb(r, g, b)));
        }
        lastRefresh = DateTime.Now;
        Invalidate3D();
    }

    private static void Square(Mesh3D mesh, double x, double z, double y)
    {
        var i = mesh.Positions.Count;
        mesh.Positions.Add(new Vec3(x, y, z)); mesh.Positions.Add(new Vec3(x + 1, y, z));
        mesh.Positions.Add(new Vec3(x, y, z + 1)); mesh.Positions.Add(new Vec3(x + 1, y, z + 1));
        mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 2); mesh.TriangleIndices.Add(i + 1);
        mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i + 2); mesh.TriangleIndices.Add(i + 3);
    }

    // The height plane at `height` (world units; 0: none -- the pens draw on the ground)
    public void SetPlane(double height)
    {
        planeHeight = height;
        var y = height / 512.0;
        var mesh = new Mesh3D();
        const double margin = 40;
        mesh.Positions.Add(new Vec3(-margin, y, -margin)); mesh.Positions.Add(new Vec3(cellsW + margin, y, -margin));
        mesh.Positions.Add(new Vec3(-margin, y, cellsH + margin)); mesh.Positions.Add(new Vec3(cellsW + margin, y, cellsH + margin));
        mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(1);
        mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(3);
        mesh.Smooth();
        plane = mesh;
        planeColour = Color.FromArgb((byte)(height > 0 ? 70 : 1), 80, 170, 255);
        Invalidate3D();
    }

    // Upstream moves its PerspectiveCamera here (Position, LookDirection, UpDirection) and WPF redraws the viewport. There is no camera
    // object to move: yaw, pitch, distance and target are read straight by the rasterizer, so this only asks for the picture again.
    private void Camera() => Invalidate3D();

    // ---- the rasterizer ------------------------------------------------------------------------------------------------------------------

    // Draws the picture again, at most one redraw a dispatcher frame. WPF's Viewport3D redrew itself on the GPU whenever the camera
    // or the models changed; here each frame costs real CPU time, so a drag queues one redraw rather than one per pointer move.
    private void Invalidate3D()
    {
        if (drawQueued) return;
        drawQueued = true;
        Dispatcher.UIThread.Post(() => { drawQueued = false; DrawFrame(); }, DispatcherPriority.Render);
    }

    private void DrawFrame()
    {
        var width = (int)Math.Round(Bounds.Width);
        var height = (int)Math.Round(Bounds.Height);
        if (width < 2 || height < 2) { surface.Source = null; return; }
        var pixels = Rasterize(width, height);
        if (frame is null || frame.PixelSize.Width != width || frame.PixelSize.Height != height) frame = BitmapFactory.Writeable(width, height);
        frame.WritePixels(new PixelRect(0, 0, width, height), pixels, width * 4, 0);
        surface.Source = frame;
        // (the picture is the same object every frame, so the Image has to be told its pixels changed)
        surface.InvalidateVisual();
    }

    // Where the camera stands and which way it looks, from yaw, pitch and distance -- upstream's PerspectiveCamera.Position,
    // LookDirection and UpDirection (0, 1, 0), as its view matrix works them out.
    private (Vec3 Eye, Vec3 Right, Vec3 Up, Vec3 Forward, double Focal) CameraBasis(int width)
    {
        double yr = yaw * Math.PI / 180, pr = pitch * Math.PI / 180;
        var offset = new Vec3(Math.Sin(yr) * Math.Cos(pr), Math.Sin(pr), Math.Cos(yr) * Math.Cos(pr)) * distance;
        var forward = (offset * -1).Normalized();
        var right = Vec3.Cross(forward, new Vec3(0, 1, 0)).Normalized();
        return (target + offset, right, Vec3.Cross(right, forward).Normalized(), forward, width / (2.0 * Math.Tan(FieldOfView * Math.PI / 360)));
    }

    // The scene from where the camera is now, as one BGRA buffer: the viewport's own order -- the surface, then what is drawn in the
    // air (its shadow first), then the see-through height plane over them.
    private byte[] Rasterize(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        // (the Border's own black, which the Viewport3D showed through where nothing is drawn)
        for (var i = 0; i < width * height; i++) pixels[i * 4 + 3] = 255;
        var depth = new double[width * height];
        Array.Fill(depth, double.PositiveInfinity);
        var (eye, right, up, forward, focal) = CameraBasis(width);
        DrawMesh(terrain, pixels, depth, width, height, eye, right, up, forward, focal, terrainTexture, textureW, textureH, Colors.White);
        foreach (var (mesh, colour) in air) DrawMesh(mesh, pixels, depth, width, height, eye, right, up, forward, focal, null, 0, 0, colour);
        DrawMesh(plane, pixels, depth, width, height, eye, right, up, forward, focal, null, 0, 0, planeColour);
        return pixels;
    }

    // One mesh with one material: a flat colour (blended by its alpha, as upstream's see-through brushes are) or the map, sampled
    // nearest-neighbour. Lit by the viewport's two lights at each corner and shaded across the triangle, which is what WPF did with the
    // normals it works out for a mesh that carries none; a triangle with a corner at or behind the near plane is left out whole, as the
    // island view's rasterizer does (WPF clipped it instead).
    private static void DrawMesh(Mesh3D mesh, byte[] pixels, double[] depth, int width, int height,
        Vec3 eye, Vec3 right, Vec3 up, Vec3 forward, double focal, byte[]? texture, int textureWidth, int textureHeight, Color colour)
    {
        if (mesh.TriangleIndices.Count == 0) return;
        var map = texture ?? Array.Empty<byte>();
        var textured = textureWidth > 0 && textureHeight > 0 && map.Length >= textureWidth * textureHeight * 4;
        var alpha = colour.A / 255.0;
        Span<double> sx = stackalloc double[3], sy = stackalloc double[3], invZ = stackalloc double[3];
        Span<double> uOverZ = stackalloc double[3], vOverZ = stackalloc double[3], lit = stackalloc double[3];
        for (var t = 0; t + 2 < mesh.TriangleIndices.Count; t += 3)
        {
            int ia = mesh.TriangleIndices[t], ib = mesh.TriangleIndices[t + 1], ic = mesh.TriangleIndices[t + 2];
            Vec3 pa = mesh.Positions[ia], pb = mesh.Positions[ib], pc = mesh.Positions[ic];
            // the far side is painted with the same material (upstream's BackMaterial), so the normals are turned towards the camera
            var facing = Vec3.Dot(Vec3.Cross(pb - pa, pc - pa), eye - pa) < 0 ? -1.0 : 1.0;
            var ok = true;
            for (var corner = 0; corner < 3; corner++)
            {
                var index = corner == 0 ? ia : corner == 1 ? ib : ic;
                var relative = mesh.Positions[index] - eye;
                var viewZ = Vec3.Dot(relative, forward);
                if (viewZ <= NearPlane) { ok = false; break; }
                sx[corner] = width / 2.0 + focal * Vec3.Dot(relative, right) / viewZ;
                sy[corner] = height / 2.0 - focal * Vec3.Dot(relative, up) / viewZ;
                invZ[corner] = 1 / viewZ;
                var uv = textured && index < mesh.TextureCoordinates.Count ? mesh.TextureCoordinates[index] : default;
                uOverZ[corner] = uv.X / viewZ; vOverZ[corner] = uv.Y / viewZ;
                var normal = index < mesh.Normals.Length ? mesh.Normals[index] * facing : new Vec3(0, 1, 0);
                lit[corner] = Ambient + Directional * Math.Max(0, Vec3.Dot(normal, Towards));
            }
            if (!ok) continue;

            var area = (sx[1] - sx[0]) * (sy[2] - sy[0]) - (sy[1] - sy[0]) * (sx[2] - sx[0]);
            if (Math.Abs(area) < 0.01) continue;
            var minX = Math.Max(0, (int)Math.Floor(Math.Min(sx[0], Math.Min(sx[1], sx[2]))));
            var maxX = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(sx[0], Math.Max(sx[1], sx[2]))));
            var minY = Math.Max(0, (int)Math.Floor(Math.Min(sy[0], Math.Min(sy[1], sy[2]))));
            var maxY = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(sy[0], Math.Max(sy[1], sy[2]))));
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var w0 = ((sx[1] - x) * (sy[2] - y) - (sy[1] - y) * (sx[2] - x)) / area;
                var w1 = ((sx[2] - x) * (sy[0] - y) - (sy[2] - y) * (sx[0] - x)) / area;
                var w2 = 1 - w0 - w1;
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                var oneOverZ = invZ[0] * w0 + invZ[1] * w1 + invZ[2] * w2;
                if (oneOverZ <= 0) continue;
                var z = 1 / oneOverZ;
                var at = y * width + x;
                if (z >= depth[at]) continue;
                depth[at] = z;
                double r, g, b;
                if (textured)
                {
                    // perspective-correct picture coordinates, sampled nearest-neighbour over the whole map (upstream's ImageBrush
                    // lays the map over the surface's 0..1 coordinates corner to corner)
                    var u = (uOverZ[0] * w0 + uOverZ[1] * w1 + uOverZ[2] * w2) * z;
                    var v = (vOverZ[0] * w0 + vOverZ[1] * w1 + vOverZ[2] * w2) * z;
                    var px = Math.Clamp((int)(u * textureWidth), 0, textureWidth - 1);
                    var py = Math.Clamp((int)(v * textureHeight), 0, textureHeight - 1);
                    var texel = (py * textureWidth + px) * 4;
                    b = map[texel]; g = map[texel + 1]; r = map[texel + 2];
                }
                else { r = colour.R; g = colour.G; b = colour.B; }
                var light = lit[0] * w0 + lit[1] * w1 + lit[2] * w2;
                double litR = Math.Clamp(r * light, 0, 255), litG = Math.Clamp(g * light, 0, 255), litB = Math.Clamp(b * light, 0, 255);
                if (alpha >= 1)
                {
                    pixels[at * 4] = (byte)litB; pixels[at * 4 + 1] = (byte)litG; pixels[at * 4 + 2] = (byte)litR;
                }
                else
                {
                    pixels[at * 4] = (byte)(litB * alpha + pixels[at * 4] * (1 - alpha));
                    pixels[at * 4 + 1] = (byte)(litG * alpha + pixels[at * 4 + 1] * (1 - alpha));
                    pixels[at * 4 + 2] = (byte)(litR * alpha + pixels[at * 4 + 2] * (1 - alpha));
                }
                pixels[at * 4 + 3] = 255;
            }
        }
    }

    // ---- the mouse -----------------------------------------------------------------------------------------------------------------------

    // Where the mouse is on the height plane (or the ground, at height 0): the cell, or null
    private Vec3? Under(Point at)
    {
        var wantPlane = planeHeight > 0;
        var width = (int)Math.Round(Bounds.Width);
        var height = (int)Math.Round(Bounds.Height);
        if (width < 2 || height < 2) return null;
        // (WPF sent a ray down the viewport with VisualTreeHelper.HitTest and took the nearest hit on that model; the ray is built here
        // from the same camera the picture is drawn with, and met with the same mesh)
        var (eye, right, up, forward, focal) = CameraBasis(width);
        var ray = (right * (at.X - width / 2.0) + up * (height / 2.0 - at.Y) + forward * focal).Normalized();
        return Hit(wantPlane ? plane : terrain, eye, ray);
    }

    // The nearest point of `mesh` the ray meets, or null (WPF's RayMeshGeometry3DHitTestResult.PointHit).
    private static Vec3? Hit(Mesh3D mesh, Vec3 eye, Vec3 ray)
    {
        var best = double.PositiveInfinity;
        Vec3? found = null;
        for (var t = 0; t + 2 < mesh.TriangleIndices.Count; t += 3)
        {
            Vec3 a = mesh.Positions[mesh.TriangleIndices[t]], b = mesh.Positions[mesh.TriangleIndices[t + 1]], c = mesh.Positions[mesh.TriangleIndices[t + 2]];
            Vec3 edge1 = b - a, edge2 = c - a;
            var p = Vec3.Cross(ray, edge2);
            var determinant = Vec3.Dot(edge1, p);
            if (Math.Abs(determinant) < 1e-9) continue;              // (the ray runs along the triangle)
            var inverse = 1 / determinant;
            var s = eye - a;
            var u = Vec3.Dot(s, p) * inverse;
            if (u < 0 || u > 1) continue;
            var q = Vec3.Cross(s, edge1);
            var v = Vec3.Dot(ray, q) * inverse;
            if (v < 0 || u + v > 1) continue;
            var away = Vec3.Dot(edge2, q) * inverse;
            if (away <= NearPlane || away >= best) continue;
            best = away; found = eye + ray * away;
        }
        return found;
    }

    private void Down(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle) { rotating = e.GetPosition(this); this.CaptureMouse(); return; }
        if (e.ChangedButton != MouseButton.Left || Under(e.GetPosition(this)) is not { } p) return;
        Begin?.Invoke();
        var at = new Point(p.X * scale, p.Z * scale);
        Draw?.Invoke(at, at);
        lastDraw = at;
        this.CaptureMouse();
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        var under = Under(pos);
        GroundUnderMouse?.Invoke(under is { } u ? Ground(u.X, u.Z) * 512 : null);
        if (rotating is { } from && (e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed))
        {
            var d = pos - from;
            if (Keyboard.Modifiers.HasFlag(KeyModifiers.Shift) || e.MiddleButton == MouseButtonState.Pressed)
            {
                // (moved: along the ground, as far as the view is wide)
                var yr = yaw * Math.PI / 180; var k = distance / Math.Max(1, this.ActualWidth);
                target += new Vec3(-Math.Cos(yr) * d.X * k - Math.Sin(yr) * d.Y * k, 0, Math.Sin(yr) * d.X * k - Math.Cos(yr) * d.Y * k);
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

    private void Up(object? sender, PointerReleasedEventArgs e)
    {
        if (lastDraw is not null) Refresh();
        rotating = null; lastDraw = null;
        this.ReleaseMouseCapture();
    }

    private void Wheel(object? sender, PointerWheelEventArgs e)
    {
        distance = Math.Clamp(distance * (e.WheelDelta > 0 ? 0.88 : 1 / 0.88), 20, Math.Max(cellsW, cellsH) * 4);
        Camera();
        e.Handled = true;
    }

    // WPF's MeshGeometry3D in the two shapes this view used it: corner points with the picture coordinates of the map on them (the
    // surface), or corner points alone (the squares and the height plane).
    private sealed class Mesh3D
    {
        public readonly List<Vec3> Positions = new();
        public readonly List<Point> TextureCoordinates = new();
        public readonly List<int> TriangleIndices = new();
        public Vec3[] Normals = Array.Empty<Vec3>();

        // What WPF worked out for a mesh that carries no Normals of its own: each corner's normal the sum of the normals of the
        // triangles it belongs to, normalized -- so a surface shares its corners and is shaded smoothly across them.
        public void Smooth()
        {
            var normals = new Vec3[Positions.Count];
            for (var t = 0; t + 2 < TriangleIndices.Count; t += 3)
            {
                int a = TriangleIndices[t], b = TriangleIndices[t + 1], c = TriangleIndices[t + 2];
                var n = Vec3.Cross(Positions[b] - Positions[a], Positions[c] - Positions[a]);
                normals[a] += n; normals[b] += n; normals[c] += n;
            }
            for (var i = 0; i < normals.Length; i++) normals[i] = normals[i].Normalized();
            Normals = normals;
        }
    }
}
