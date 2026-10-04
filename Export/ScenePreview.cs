using System.IO;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace LBAAssembler.Export;

// A 3D view of an ExportScene: drag to turn it, wheel to zoom, right or middle drag to slide it, double-click to look at all of it again. It shows the scene
// as the export writes it (right-handed, Y up), flat-shaded, in the palette colours and textures (the islands' baked light is not shown).
//
// Upstream draws this with WPF's Media3D (a Viewport3D of GeometryModel3Ds under a PerspectiveCamera and three lights). Avalonia has no
// 3D of any kind, so the same picture is rasterized on the CPU -- z-buffered, perspective-correct, nearest-neighbour textures -- into a
// WriteableBitmap shown in an Image, exactly as this port already does for the island view (SoftwareTerrainRenderer). The camera, the
// lights and the material colours are the ones upstream's Viewport3D used, so the view matches.
internal sealed class ScenePreview : Grid
{
    // WPF's PerspectiveCamera.FieldOfView: the HORIZONTAL angle.
    private const double FieldOfView = 40;
    // The AmbientLight and the two DirectionalLights upstream puts in the viewport, as fractions of full brightness.
    private static readonly Vec3 Ambient = new(0x6A / 255.0, 0x6A / 255.0, 0x6A / 255.0);
    private static readonly (Vec3 Colour, Vec3 Towards)[] Lights =
    {
        // (a directional light travels along its Direction, so what a surface is lit by is the direction back towards it)
        (new Vec3(0xC0 / 255.0, 0xC0 / 255.0, 0xC0 / 255.0), new Vec3(0.5, 1, 0.7).Normalized()),
        (new Vec3(0x50 / 255.0, 0x50 / 255.0, 0x58 / 255.0), new Vec3(-0.6, -0.3, -0.8).Normalized()),
    };

    // (the picture is drawn at the view's own size, so Fill never actually scales it; nearest-neighbour keeps a half-pixel rounding sharp)
    private readonly Image surface = new() { Stretch = Stretch.Fill };
    private readonly TextBlock overlay = new() { Foreground = Brushes.LightGray, Margin = new Thickness(10), IsHitTestVisible = false, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock hint = new() { Foreground = Brushes.Gray, Margin = new Thickness(10), IsHitTestVisible = false, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Text = "Drag to turn, wheel to zoom, right drag to slide, double-click to fit" };

    private double yaw = 0.6, pitch = 0.45, distance = 10, radius = 1;
    private Vec3 target;
    private Point? drag; private bool sliding;
    private int generation;
    // What is being looked at (the triangles of the scene, in the export's own right-handed coordinates), and the picture it was last drawn into.
    private PreviewModel? drawn;
    private WriteableBitmap? frame;
    private bool drawQueued;

    public ScenePreview()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1E, 0x2A));
        RenderOptions.SetBitmapInterpolationMode(surface, BitmapInterpolationMode.None);
        Children.Add(surface); Children.Add(overlay); Children.Add(hint);
        PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsRightButtonPressed || point.Properties.IsMiddleButtonPressed) { drag = point.Position; sliding = true; e.Pointer.Capture(this); }
            else if (point.Properties.IsLeftButtonPressed)
            {
                if (e.ClickCount == 2) { Fit(); return; }
                drag = point.Position; sliding = false; e.Pointer.Capture(this);
            }
        };
        PointerReleased += (_, e) => { drag = null; e.Pointer.Capture(null); };
        PointerMoved += OnMove;
        PointerWheelChanged += (_, e) => { distance = Math.Clamp(distance * (e.Delta.Y > 0 ? 0.87 : 1 / 0.87), radius * 0.05, radius * 60); UpdateCamera(); };
        SizeChanged += (_, _) => UpdateCamera();
    }

    public void ShowMessage(string text) { generation++; drawn = null; overlay.Text = text; UpdateCamera(); }

    // Builds the model off the UI thread; a later call makes an earlier one that is still running quietly give up.
    public async Task ShowAsync(Func<ExportScene?> build, string label)
    {
        var mine = ++generation;
        overlay.Text = "Building the preview…";
        PreviewModel? model = null;
        (Vec3 Centre, double Radius, int Triangles)? info = null;
        try
        {
            (model, info) = await Task.Run(() =>
            {
                var scene = build();
                return scene is null || scene.TriangleCount == 0 ? (null, null) : BuildModel(scene);
            });
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException or InvalidOperationException or OverflowException)
        {
            if (mine == generation) { drawn = null; overlay.Text = "Can't preview this: " + error.Message; UpdateCamera(); }
            return;
        }
        if (mine != generation) return;
        drawn = null;
        if (model is null || info is not { } i) { overlay.Text = "Nothing to draw for this."; UpdateCamera(); return; }
        drawn = model;
        target = i.Centre; radius = Math.Max(1e-3, i.Radius);
        overlay.Text = $"{label}\n{i.Triangles:N0} triangles";
        Fit();
    }

    private void Fit()
    {
        // FieldOfView is the horizontal angle: in a view wider than tall the vertical half-angle is narrower, and that is what has to hold the model
        var aspect = Bounds.Width > 1 && Bounds.Height > 1 ? Math.Min(1, Bounds.Height / Bounds.Width) : 0.7;
        distance = radius / (Math.Tan(FieldOfView * Math.PI / 360) * aspect) * 1.1; yaw = 0.6; pitch = 0.45;
        UpdateCamera();
    }

    private void OnMove(object? sender, PointerEventArgs e)
    {
        if (drag is not { } from) return;
        var now = e.GetPosition(this);
        double dx = now.X - from.X, dy = now.Y - from.Y;
        drag = now;
        if (sliding)
        {
            var scale = distance * 0.0016;
            var right = new Vec3(Math.Cos(yaw), 0, -Math.Sin(yaw));
            target += right * (-dx * scale) + new Vec3(0, dy * scale, 0);
        }
        else { yaw -= dx * 0.01; pitch = Math.Clamp(pitch + dy * 0.01, -1.5, 1.5); }
        UpdateCamera();
    }

    // The camera moved (or the view was resized, or what it looks at changed): draw it again. WPF's Viewport3D redrew itself on the GPU;
    // here each frame costs real CPU time, so a drag queues at most one redraw per dispatcher frame rather than one per pointer move.
    private void UpdateCamera()
    {
        if (drawQueued) return;
        drawQueued = true;
        Dispatcher.UIThread.Post(() => { drawQueued = false; Draw(); }, DispatcherPriority.Render);
    }

    private void Draw()
    {
        var width = (int)Math.Round(Bounds.Width);
        var height = (int)Math.Round(Bounds.Height);
        if (width < 2 || height < 2) { surface.Source = null; return; }
        var pixels = Rasterize(drawn, width, height);
        if (frame is null || frame.PixelSize.Width != width || frame.PixelSize.Height != height) frame = BitmapFactory.Writeable(width, height);
        frame.WritePixels(new PixelRect(0, 0, width, height), pixels, width * 4, 0);
        surface.Source = frame;
        // (the picture is the same object every frame, so the Image has to be told its pixels changed)
        surface.InvalidateVisual();
    }

    // ---- the rasterizer ------------------------------------------------------------------------------------------------------------------

    // One triangle, in the export's right-handed Y-up world, with the picture coordinates of its corners and the material that paints it.
    private readonly record struct Face(Vec3 A, Vec3 B, Vec3 C, Vector2 UvA, Vector2 UvB, Vector2 UvC, int Material);

    // What a material paints with: a flat colour, or a picture (BGR, top row first) sampled nearest-neighbour and tiled, which is what
    // upstream's ImageBrush (TileMode.Tile over the unit square, BitmapScalingMode.NearestNeighbor) did.
    private sealed record Paint(byte R, byte G, byte B, int Width, int Height, byte[]? Picture);

    private sealed class PreviewModel
    {
        public required Face[] Faces { get; init; }
        public required Paint[] Paints { get; init; }
    }

    // The scene as a flat list of world-space triangles (the node transforms are baked in, as WPF's MatrixTransform3D did at draw time).
    private static (PreviewModel, (Vec3 Centre, double Radius, int Triangles)?) BuildModel(ExportScene game)
    {
        var scene = game.ToRightHanded(1f);
        var paints = new Paint[scene.Materials.Count];
        for (var m = 0; m < paints.Length; m++)
        {
            var material = scene.Materials[m];
            if (material.Texture is { } t)
            {
                var picture = new byte[t.Width * t.Height * 3];
                for (var i = 0; i < t.Width * t.Height; i++) { picture[i * 3] = t.Rgba[i * 4 + 2]; picture[i * 3 + 1] = t.Rgba[i * 4 + 1]; picture[i * 3 + 2] = t.Rgba[i * 4]; }
                paints[m] = new Paint(material.R, material.G, material.B, t.Width, t.Height, picture);
            }
            else paints[m] = new Paint(material.R, material.G, material.B, 0, 0, null);
        }

        var faces = new List<Face>();
        foreach (var node in scene.Nodes)
        {
            var mesh = node.Mesh;
            var moved = node.Transform != Matrix4x4.Identity;
            var corners = new Vec3[mesh.Positions.Count];
            for (var i = 0; i < corners.Length; i++)
            {
                var p = moved ? Vector3.Transform(mesh.Positions[i], node.Transform) : mesh.Positions[i];
                corners[i] = new Vec3(p.X, p.Y, p.Z);
            }
            foreach (var primitive in node.Mesh.Primitives)
                for (var i = 0; i + 2 < primitive.Indices.Count; i += 3)
                {
                    int a = primitive.Indices[i], b = primitive.Indices[i + 1], c = primitive.Indices[i + 2];
                    faces.Add(new Face(corners[a], corners[b], corners[c], mesh.Uvs[a], mesh.Uvs[b], mesh.Uvs[c], primitive.Material));
                }
        }

        (Vec3, double, int)? info = null;
        if (scene.Bounds() is { } box)
        {
            var centre = (box.Min + box.Max) / 2;
            info = (new Vec3(centre.X, centre.Y, centre.Z), (box.Max - box.Min).Length() / 2, game.TriangleCount);
        }
        return (new PreviewModel { Faces = faces.ToArray(), Paints = paints }, info);
    }

    // The model from where the camera is now, as one BGRA buffer. Flat shaded (one normal a triangle, turned to face the camera, which is
    // what upstream's BackMaterial did for the far side), z-buffered, with perspective-correct picture coordinates.
    private byte[] Rasterize(PreviewModel? model, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++) { pixels[i * 4] = 0x2A; pixels[i * 4 + 1] = 0x1E; pixels[i * 4 + 2] = 0x16; pixels[i * 4 + 3] = 255; }
        if (model is null || model.Faces.Length == 0 || model.Paints.Length == 0) return pixels;

        var offset = new Vec3(Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch), Math.Cos(yaw) * Math.Cos(pitch)) * distance;
        var eye = target + offset;
        var forward = (offset * -1).Normalized();
        var right = Vec3.Cross(forward, new Vec3(0, 1, 0)).Normalized();
        var up = Vec3.Cross(right, forward).Normalized();
        var focal = width / (2.0 * Math.Tan(FieldOfView * Math.PI / 360));
        // (a triangle with a corner at or behind the near plane is left out whole, as the island view's rasterizer does: WPF clipped it instead)
        var near = Math.Max(1e-4, distance * 0.01);

        var depth = new double[width * height];
        Array.Fill(depth, double.PositiveInfinity);
        Span<double> sx = stackalloc double[3], sy = stackalloc double[3], invZ = stackalloc double[3], uOverZ = stackalloc double[3], vOverZ = stackalloc double[3];

        foreach (var face in model.Faces)
        {
            var paint = model.Paints[Math.Clamp(face.Material, 0, model.Paints.Length - 1)];
            var ok = true;
            for (var corner = 0; corner < 3 && ok; corner++)
            {
                var world = corner == 0 ? face.A : corner == 1 ? face.B : face.C;
                var uv = corner == 0 ? face.UvA : corner == 1 ? face.UvB : face.UvC;
                var relative = world - eye;
                var viewZ = Vec3.Dot(relative, forward);
                if (viewZ <= near) { ok = false; break; }
                sx[corner] = width / 2.0 + focal * Vec3.Dot(relative, right) / viewZ;
                sy[corner] = height / 2.0 - focal * Vec3.Dot(relative, up) / viewZ;
                invZ[corner] = 1 / viewZ;
                uOverZ[corner] = uv.X / viewZ; vOverZ[corner] = uv.Y / viewZ;
            }
            if (!ok) continue;

            var area = (sx[1] - sx[0]) * (sy[2] - sy[0]) - (sy[1] - sy[0]) * (sx[2] - sx[0]);
            if (Math.Abs(area) < 0.01) continue;

            // flat shading: the face's own normal, turned towards the camera (upstream painted the far side with the same material)
            var normal = Vec3.Cross(face.B - face.A, face.C - face.A).Normalized();
            if (Vec3.Dot(normal, eye - face.A) < 0) normal *= -1;
            double litR = Ambient.X, litG = Ambient.Y, litB = Ambient.Z;
            foreach (var (colour, towards) in Lights)
            {
                var lambert = Math.Max(0, Vec3.Dot(normal, towards));
                if (lambert <= 0) continue;
                litR += colour.X * lambert; litG += colour.Y * lambert; litB += colour.Z * lambert;
            }

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
                if (paint.Picture is { } picture)
                {
                    // perspective-correct picture coordinates, wrapped (the brush tiled) and sampled nearest-neighbour
                    var u = (uOverZ[0] * w0 + uOverZ[1] * w1 + uOverZ[2] * w2) * z;
                    var v = (vOverZ[0] * w0 + vOverZ[1] * w1 + vOverZ[2] * w2) * z;
                    var px = Math.Clamp((int)((u - Math.Floor(u)) * paint.Width), 0, paint.Width - 1);
                    var py = Math.Clamp((int)((v - Math.Floor(v)) * paint.Height), 0, paint.Height - 1);
                    var texel = (py * paint.Width + px) * 3;
                    b = picture[texel]; g = picture[texel + 1]; r = picture[texel + 2];
                }
                else { r = paint.R; g = paint.G; b = paint.B; }
                pixels[at * 4] = (byte)Math.Clamp(b * litB, 0, 255);
                pixels[at * 4 + 1] = (byte)Math.Clamp(g * litG, 0, 255);
                pixels[at * 4 + 2] = (byte)Math.Clamp(r * litR, 0, 255);
                pixels[at * 4 + 3] = 255;
            }
        }
        return pixels;
    }
}
