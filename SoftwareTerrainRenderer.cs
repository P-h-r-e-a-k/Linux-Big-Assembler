using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace LBAAssembler;

internal static class SoftwareTerrainRenderer
{
    public static WriteableBitmap Render(IslandDocument island, int width, int height, double yaw, double pitch, double distance, double targetX, double targetZ)
    {
        width = Math.Max(320, width);
        height = Math.Max(200, height);
        var pixels = new byte[width * height * 4];
        var depth = Enumerable.Repeat(float.PositiveInfinity, width * height).ToArray();
        for (var index = 0; index < width * height; index++)
        {
            pixels[index * 4] = 152;
            pixels[index * 4 + 1] = 185;
            pixels[index * 4 + 2] = 202;
            pixels[index * 4 + 3] = 255;
        }

        var yawRadians = yaw * Math.PI / 180.0;
        var pitchRadians = pitch * Math.PI / 180.0;
        var target = new Vec3(targetX, 0, targetZ);
        var horizontal = distance * Math.Cos(pitchRadians);
        var camera = new Vec3(targetX + horizontal * Math.Cos(yawRadians), distance * Math.Sin(pitchRadians), targetZ + horizontal * Math.Sin(yawRadians));
        var forward = target - camera;
        forward = forward.Normalized();
        var right = Vec3.Cross(forward, new Vec3(0, 1, 0)).Normalized();
        var up = Vec3.Cross(right, forward).Normalized();
        var focal = width / (2.0 * Math.Tan(55.0 * Math.PI / 360.0));

        for (var cubeY = 0; cubeY < 16; cubeY++)
        for (var cubeX = 0; cubeX < 16; cubeX++)
        {
            var cubeId = island.CubeAt(cubeX, cubeY) & 0x7F;
            if (cubeId == 0) continue;
            for (var z = 0; z < 64; z++)
            for (var x = 0; x < 64; x++)
            {
                var first = island.PolygonAt(cubeId, x, z);
                var second = island.PolygonAt(cubeId, x, z, 1);
                var firstCorners = ((first >> 16) & 1) == 0 ? new[] { 0, 1, 2 } : new[] { 3, 0, 1 };
                var secondCorners = ((second >> 16) & 1) == 0 ? new[] { 2, 3, 0 } : new[] { 1, 2, 3 };
                RasterTriangle(island, pixels, depth, width, height, camera, right, up, forward, focal, cubeId, cubeX, cubeY, x, z, first, firstCorners);
                RasterTriangle(island, pixels, depth, width, height, camera, right, up, forward, focal, cubeId, cubeX, cubeY, x, z, second, secondCorners);
            }
        }

        var bitmap = BitmapFactory.Writeable(width, height);
        bitmap.WritePixels(new PixelRect(0, 0, width, height), pixels, width * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }

    // Projects a world-space point (e.g. an actor position) using the exact
    // same camera this renderer builds internally for Render(), so overlays
    // (actor markers, selection highlights) drawn on a WPF Canvas above the
    // rendered bitmap line up with it pixel-for-pixel. Returns false for a
    // point behind the camera (viewZ <= 1, matching RasterTriangle's own
    // near-plane check) rather than a garbage screen position.
    public static bool TryProjectWorldPoint(int width, int height, double yaw, double pitch, double distance, double targetX, double targetZ, Vec3 world, out double screenX, out double screenY)
    {
        width = Math.Max(320, width);
        height = Math.Max(200, height);
        var yawRadians = yaw * Math.PI / 180.0;
        var pitchRadians = pitch * Math.PI / 180.0;
        var horizontal = distance * Math.Cos(pitchRadians);
        var camera = new Vec3(targetX + horizontal * Math.Cos(yawRadians), distance * Math.Sin(pitchRadians), targetZ + horizontal * Math.Sin(yawRadians));
        var target = new Vec3(targetX, 0, targetZ);
        var forward = target - camera;
        forward = forward.Normalized();
        var right = Vec3.Cross(forward, new Vec3(0, 1, 0)).Normalized();
        var up = Vec3.Cross(right, forward).Normalized();
        var focal = width / (2.0 * Math.Tan(55.0 * Math.PI / 360.0));

        var relative = world - camera;
        var viewX = Vec3.Dot(relative, right);
        var viewY = Vec3.Dot(relative, up);
        var viewZ = Vec3.Dot(relative, forward);
        if (viewZ <= 1) { screenX = screenY = 0; return false; }
        screenX = width / 2.0 + focal * viewX / viewZ;
        screenY = height / 2.0 - focal * viewY / viewZ;
        return true;
    }

    private static void RasterTriangle(IslandDocument island, byte[] pixels, float[] depth, int width, int height, Vec3 camera, Vec3 right, Vec3 up, Vec3 forward, double focal, int cubeId, int cubeX, int cubeY, int cellX, int cellZ, uint polygon, int[] corners)
    {
        var texture = island.TextureAt(cubeId, (int)((polygon >> 19) & 0x1FFF));
        var textured = ((polygon >> 4) & 3) != 0 && texture is not null;
        var local = new[] { new Point(0, 0), new Point(0, 1), new Point(1, 1), new Point(1, 0) };
        var projected = new ProjectedPoint[3];
        for (var index = 0; index < 3; index++)
        {
            var corner = corners[index];
            var x = cellX + (int)local[corner].X;
            var z = cellZ + (int)local[corner].Y;
            var world = new Vec3(cubeX * 32768 + x * 512, island.HeightAt(cubeId, x, z), cubeY * 32768 + z * 512);
            var relative = world - camera;
            var viewX = Vec3.Dot(relative, right);
            var viewY = Vec3.Dot(relative, up);
            var viewZ = Vec3.Dot(relative, forward);
            if (viewZ <= 1) return;
            projected[index] = new ProjectedPoint((float)widthFor(focal, viewX, viewZ), (float)heightFor(focal, viewY, viewZ), viewZ, island.IntensityAt(cubeId, x, z), textured ? TextureCoordinate(texture!, index * 2) : 0, textured ? TextureCoordinate(texture!, index * 2 + 1) : 0);
        }
        var minX = Math.Max(0, (int)Math.Floor(Math.Min(projected[0].X, Math.Min(projected[1].X, projected[2].X))));
        var maxX = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(projected[0].X, Math.Max(projected[1].X, projected[2].X))));
        var minY = Math.Max(0, (int)Math.Floor(Math.Min(projected[0].Y, Math.Min(projected[1].Y, projected[2].Y))));
        var maxY = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(projected[0].Y, Math.Max(projected[1].Y, projected[2].Y))));
        var area = Edge(projected[0], projected[1], projected[2].X, projected[2].Y);
        if (Math.Abs(area) < 0.01) return;
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var w0 = Edge(projected[1], projected[2], x, y) / area;
            var w1 = Edge(projected[2], projected[0], x, y) / area;
            var w2 = 1 - w0 - w1;
            if (w0 < 0 || w1 < 0 || w2 < 0) continue;
            var z = (float)(projected[0].Z * w0 + projected[1].Z * w1 + projected[2].Z * w2);
            var offset = y * width + x;
            if (z >= depth[offset]) continue;
            depth[offset] = z;
            var u = projected[0].U * w0 + projected[1].U * w1 + projected[2].U * w2;
            var v = projected[0].V * w0 + projected[1].V * w1 + projected[2].V * w2;
            var light = (int)Math.Clamp(Math.Round(projected[0].Light * w0 + projected[1].Light * w1 + projected[2].Light * w2), 0, 15);
            var color = textured ? island.ColorAt(u, v, light) : FlatColor((int)(polygon & 15), light);
            pixels[offset * 4] = color.B; pixels[offset * 4 + 1] = color.G; pixels[offset * 4 + 2] = color.R; pixels[offset * 4 + 3] = 255;
        }

        double widthFor(double f, double x, double z) => width / 2.0 + f * x / z;
        double heightFor(double f, double y, double z) => height / 2.0 - f * y / z;
    }

    private static double TextureCoordinate(ushort[] texture, int index) => texture[index] / 256.0;
    private static Color FlatColor(int bank, int light) => Color.FromRgb((byte)Math.Clamp(70 + bank * 10 + light * 5, 0, 255), (byte)Math.Clamp(95 + bank * 7 + light * 6, 0, 255), (byte)Math.Clamp(55 + bank * 4 + light * 3, 0, 255));
    private static double Edge(ProjectedPoint a, ProjectedPoint b, double x, double y) => (x - a.X) * (b.Y - a.Y) - (y - a.Y) * (b.X - a.X);
    private readonly record struct ProjectedPoint(float X, float Y, double Z, byte Light, double U, double V);
}

// WPF's Media3D Point3D/Vector3D in the two shapes this renderer used them (double precision, cross product, dot product).
internal readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public Vec3 Normalized() { var l = Length; return l <= 0 ? this : new(X / l, Y / l, Z / l); }
    public static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}
