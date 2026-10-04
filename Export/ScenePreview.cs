using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace LBAAssembler.Export;

// A 3D view of an ExportScene: drag to turn it, wheel to zoom, right or middle drag to slide it, double-click to look at all of it again. It shows the scene
// as the export writes it (right-handed, Y up), flat-shaded, in the palette colours and textures (the islands' baked light is not shown).
internal sealed class ScenePreview : Grid
{
    private readonly Viewport3D viewport = new() { ClipToBounds = true };
    private readonly ModelVisual3D content = new();
    private readonly PerspectiveCamera camera = new() { FieldOfView = 40 };
    private readonly TextBlock overlay = new() { Foreground = Brushes.LightGray, Margin = new Thickness(10), IsHitTestVisible = false, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock hint = new() { Foreground = Brushes.Gray, Margin = new Thickness(10), IsHitTestVisible = false, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Text = "Drag to turn, wheel to zoom, right drag to slide, double-click to fit" };

    private double yaw = 0.6, pitch = 0.45, distance = 10, radius = 1;
    private Point3D target;
    private Point? drag; private bool sliding;
    private int generation;

    public ScenePreview()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x16, 0x1E, 0x2A));
        var lights = new Model3DGroup();
        lights.Children.Add(new AmbientLight(Color.FromRgb(0x6A, 0x6A, 0x6A)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(0xC0, 0xC0, 0xC0), new Vector3D(-0.5, -1, -0.7)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(0x50, 0x50, 0x58), new Vector3D(0.6, 0.3, 0.8)));
        content.Content = lights;
        viewport.Children.Add(content);
        viewport.Camera = camera;
        Children.Add(viewport); Children.Add(overlay); Children.Add(hint);
        MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { Fit(); return; } drag = e.GetPosition(this); sliding = false; CaptureMouse(); };
        MouseRightButtonDown += (_, e) => { drag = e.GetPosition(this); sliding = true; CaptureMouse(); };
        MouseUp += (_, _) => { drag = null; ReleaseMouseCapture(); };
        MouseMove += OnMove;
        MouseWheel += (_, e) => { distance = Math.Clamp(distance * (e.Delta > 0 ? 0.87 : 1 / 0.87), radius * 0.05, radius * 60); UpdateCamera(); };
        SizeChanged += (_, _) => UpdateCamera();
    }

    public void ShowMessage(string text) { generation++; content.Children.Clear(); overlay.Text = text; }

    // Builds the model off the UI thread; a later call makes an earlier one that is still running quietly give up.
    public async Task ShowAsync(Func<ExportScene?> build, string label)
    {
        var mine = ++generation;
        overlay.Text = "Building the preview…";
        Model3DGroup? model = null;
        (Point3D Centre, double Radius, int Triangles)? info = null;
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
            if (mine == generation) { content.Children.Clear(); overlay.Text = "Can't preview this: " + error.Message; }
            return;
        }
        if (mine != generation) return;
        content.Children.Clear();
        if (model is null || info is not { } i) { overlay.Text = "Nothing to draw for this."; return; }
        content.Children.Add(new ModelVisual3D { Content = model });
        target = i.Centre; radius = Math.Max(1e-3, i.Radius);
        overlay.Text = $"{label}\n{i.Triangles:N0} triangles";
        Fit();
    }

    private void Fit()
    {
        // FieldOfView is the horizontal angle: in a view wider than tall the vertical half-angle is narrower, and that is what has to hold the model
        var aspect = ActualWidth > 1 && ActualHeight > 1 ? Math.Min(1, ActualHeight / ActualWidth) : 0.7;
        distance = radius / (Math.Tan(camera.FieldOfView * Math.PI / 360) * aspect) * 1.1; yaw = 0.6; pitch = 0.45;
        UpdateCamera();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (drag is not { } from) return;
        var now = e.GetPosition(this);
        double dx = now.X - from.X, dy = now.Y - from.Y;
        drag = now;
        if (sliding)
        {
            var scale = distance * 0.0016;
            var right = new Vector3D(Math.Cos(yaw), 0, -Math.Sin(yaw));
            target += right * (-dx * scale) + new Vector3D(0, dy * scale, 0);
        }
        else { yaw -= dx * 0.01; pitch = Math.Clamp(pitch + dy * 0.01, -1.5, 1.5); }
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        var offset = new Vector3D(Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch), Math.Cos(yaw) * Math.Cos(pitch)) * distance;
        camera.Position = target + offset;
        camera.LookDirection = -offset;
        camera.UpDirection = new Vector3D(0, 1, 0);
        camera.NearPlaneDistance = Math.Max(1e-4, distance * 0.01);
        camera.FarPlaneDistance = Math.Max(10, distance + radius * 20);
    }

    // The scene as a frozen 3D model: flat shaded (three own vertices per triangle), one geometry per mesh and material.
    private static (Model3DGroup, (Point3D, double, int)?) BuildModel(ExportScene game)
    {
        var scene = game.ToRightHanded(1f);
        var materials = new Material?[scene.Materials.Count];
        Material MaterialOf(int index)
        {
            if (materials[index] is { } known) return known;
            var m = scene.Materials[index];
            Brush brush;
            if (m.Texture is { } t)
            {
                var bgra = new byte[t.Width * t.Height * 4];
                for (var i = 0; i < t.Width * t.Height; i++) { bgra[i * 4] = t.Rgba[i * 4 + 2]; bgra[i * 4 + 1] = t.Rgba[i * 4 + 1]; bgra[i * 4 + 2] = t.Rgba[i * 4]; bgra[i * 4 + 3] = 255; }
                var bitmap = BitmapSource.Create(t.Width, t.Height, 96, 96, PixelFormats.Bgra32, null, bgra, t.Width * 4);
                bitmap.Freeze();
                var image = new ImageBrush(bitmap) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 1, 1), ViewportUnits = BrushMappingMode.Absolute, ViewboxUnits = BrushMappingMode.RelativeToBoundingBox };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
                image.Freeze();
                brush = image;
            }
            else { brush = new SolidColorBrush(Color.FromRgb(m.R, m.G, m.B)); brush.Freeze(); }
            var material = new DiffuseMaterial(brush);
            material.Freeze();
            return (materials[index] = material)!;
        }

        var group = new Model3DGroup();
        var geometries = new Dictionary<(ExportMesh, int), MeshGeometry3D>();
        foreach (var node in scene.Nodes)
            foreach (var primitive in node.Mesh.Primitives)
            {
                if (!geometries.TryGetValue((node.Mesh, primitive.Material), out var geometry))
                {
                    var positions = new Point3DCollection(primitive.Indices.Count);
                    var uvs = new PointCollection(primitive.Indices.Count);
                    var indices = new Int32Collection(primitive.Indices.Count);
                    for (var i = 0; i < primitive.Indices.Count; i++)
                    {
                        var p = node.Mesh.Positions[primitive.Indices[i]]; var uv = node.Mesh.Uvs[primitive.Indices[i]];
                        positions.Add(new Point3D(p.X, p.Y, p.Z)); uvs.Add(new Point(uv.X, uv.Y)); indices.Add(i);
                    }
                    geometry = new MeshGeometry3D { Positions = positions, TextureCoordinates = uvs, TriangleIndices = indices };
                    geometry.Freeze();
                    geometries[(node.Mesh, primitive.Material)] = geometry;
                }
                var material = MaterialOf(primitive.Material);
                var model = new GeometryModel3D(geometry, material) { BackMaterial = material };
                if (node.Transform != Matrix4x4.Identity)
                {
                    var m = node.Transform;
                    model.Transform = new MatrixTransform3D(new Matrix3D(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44));
                }
                model.Freeze();
                group.Children.Add(model);
            }
        group.Freeze();

        (Point3D, double, int)? info = null;
        if (scene.Bounds() is { } b)
        {
            var centre = (b.Min + b.Max) / 2;
            info = (new Point3D(centre.X, centre.Y, centre.Z), (b.Max - b.Min).Length() / 2, game.TriangleCount);
        }
        return (group, info);
    }
}
