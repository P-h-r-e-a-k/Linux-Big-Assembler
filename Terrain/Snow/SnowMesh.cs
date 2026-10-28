using System.IO;
using System.Numerics;
using LbaBodyStudio;
using LBAAssembler.Terrain.Polar;

namespace LBAAssembler.Terrain.Snow;

// A colour as it is meant (8-bit RGB), before the light and the palette.
internal readonly record struct Rgb(double R, double G, double B)
{
    public Rgb Times(double k) => new(R * k, G * k, B * k);
    public Rgb Mix(Rgb o, double t) => new(R + (o.R - R) * t, G + (o.G - G) * t, B + (o.B - B) * t);
}

// The colours of the island's palette a body's faces are drawn in: each face its colour lit by the cubes' sun (the ground's bake's: the
// objects shade as the snow round them does) -- a flat colour a face, unlit by the engine, as the race tracks' decks are.
internal sealed class SnowShading
{
    private readonly byte[] palette;
    private readonly Vector3 sun;
    public const double Ambient = 0.58, Direct = 0.5;

    public SnowShading(byte[] palette, double azimuth, double elevation)
    {
        this.palette = palette;
        var (x, y, z) = IslandLight.Direction(azimuth, elevation);
        sun = Vector3.Normalize(new Vector3((float)x, (float)y, (float)z));
    }

    public int Of(Rgb c, Vector3 normal)
    {
        var lit = Ambient + Direct * Math.Max(0, Vector3.Dot(normal, sun)) + 0.06 * Math.Max(0, normal.Y);
        return Nearest(Math.Min(255, c.R * lit), Math.Min(255, c.G * lit), Math.Min(255, c.B * lit));
    }

    public int Nearest(double r, double g, double b) => SnowColour.Nearest(palette, r, g, b);
}

// The nearest of an island palette's lit colours (16..239: its ramps, which the light and the fog shade) to a colour: by brightness and,
// four times as much, by its hue's two axes (YUV) -- a plain RGB distance took the snow's pale shadowed cyans to the lavender ramp's greys,
// pink patches on the trees.
internal static class SnowColour
{
    private static readonly Dictionary<byte[], Dictionary<(int, int, int), int>> caches = new(ReferenceEqualityComparer.Instance);

    public static int Nearest(byte[] palette, double r, double g, double b)
    {
        if (!caches.TryGetValue(palette, out var cache)) caches[palette] = cache = new();
        var key = ((int)Math.Round(r), (int)Math.Round(g), (int)Math.Round(b));
        if (cache.TryGetValue(key, out var known)) return known;
        var (y, u, v) = Yuv(key.Item1, key.Item2, key.Item3);
        var best = 16; var bestD = double.MaxValue;
        for (var p = 16; p <= 239; p++)
        {
            var (pr, pg, pb) = PolarTextures.Colours.Rgb(palette, p);
            var (py, pu, pv) = Yuv(pr, pg, pb);
            var d = (y - py) * (y - py) + 4 * ((u - pu) * (u - pu) + (v - pv) * (v - pv));
            if (d < bestD) { bestD = d; best = p; }
        }
        return cache[key] = best;
    }

    private static (double Y, double U, double V) Yuv(double r, double g, double b) =>
        (0.299 * r + 0.587 * g + 0.114 * b, -0.147 * r - 0.289 * g + 0.436 * b, 0.615 * r - 0.515 * g - 0.100 * b);
}

// A body being made: its points (world units from its origin, y up) and faces, each wound so that its plain cross product points out (the
// engine draws a face only from in front), a triangle or a quad (a body holds no other).
internal sealed class SnowMesh(SnowShading shading)
{
    public readonly List<Vector3> Points = new();
    public readonly List<Face> Faces = new();
    private readonly Dictionary<Vector3, int> shared = new();
    public SnowShading Shading => shading;

    // what the points given are moved by before they go in (a thing built facing +z, turned to face its way; a piece placed in a body
    // made of several) -- the faces lit as they then face
    public Matrix4x4 Transform = Matrix4x4.Identity;
    public void Turn(double radians, Vector3 at = default) => Transform = Matrix4x4.CreateRotationY((float)radians) * Matrix4x4.CreateTranslation(at);

    private int P(Vector3 v)
    {
        if (shared.TryGetValue(v, out var i)) return i;
        Points.Add(v);
        return shared[v] = Points.Count - 1;
    }

    // A face of three or four points, turned to face `outward`.
    public void Poly(Rgb colour, Vector3 outward, params Vector3[] p)
    {
        if (!Transform.IsIdentity)
        {
            p = p.Select(q => Vector3.Transform(q, Transform)).ToArray();
            outward = Vector3.TransformNormal(outward, Transform);
        }
        var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
        if (n.LengthSquared() < 1e-6f) return;
        if (Vector3.Dot(n, outward) < 0) { Array.Reverse(p); n = -n; }
        Faces.Add(new Face(p.Select(P).ToArray(), shading.Of(colour, Vector3.Normalize(n)), Material: 0));
    }

    // ... seen from both sides (a flag, a leaf)
    public void TwoSided(Rgb colour, params Vector3[] p)
    {
        var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
        Poly(colour, n, p);
        Poly(colour, -n, p.Reverse().ToArray());
    }

    // An axis-aligned box; `top` its top's colour (a snow cap), the bottom left out unless `bottom`.
    public void Box(Vector3 lo, Vector3 hi, Rgb side, Rgb? top = null, bool bottom = false)
    {
        Vector3 C(int x, int y, int z) => new(x == 0 ? lo.X : hi.X, y == 0 ? lo.Y : hi.Y, z == 0 ? lo.Z : hi.Z);
        Poly(top ?? side, Vector3.UnitY, C(0, 1, 0), C(1, 1, 0), C(1, 1, 1), C(0, 1, 1));
        if (bottom) Poly(side, -Vector3.UnitY, C(0, 0, 0), C(1, 0, 0), C(1, 0, 1), C(0, 0, 1));
        Poly(side, Vector3.UnitX, C(1, 0, 0), C(1, 1, 0), C(1, 1, 1), C(1, 0, 1));
        Poly(side, -Vector3.UnitX, C(0, 0, 0), C(0, 1, 0), C(0, 1, 1), C(0, 0, 1));
        Poly(side, Vector3.UnitZ, C(0, 0, 1), C(1, 0, 1), C(1, 1, 1), C(0, 1, 1));
        Poly(side, -Vector3.UnitZ, C(0, 0, 0), C(1, 0, 0), C(1, 1, 0), C(0, 1, 0));
    }

    // A beam from a to b, `w` across and `h` the other way (a strut, a rail, a stick).
    public void Beam(Vector3 a, Vector3 b, double w, double h, Rgb colour)
    {
        var d = Vector3.Normalize(b - a);
        var side = Math.Abs(d.Y) > 0.95f ? Vector3.UnitX : Vector3.Normalize(Vector3.Cross(d, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(side, d));
        var sw = side * (float)(w / 2); var uh = up * (float)(h / 2);
        var c = new Vector3[8];
        for (var i = 0; i < 8; i++)
            c[i] = ((i & 4) != 0 ? b : a) + ((i & 1) != 0 ? sw : -sw) + ((i & 2) != 0 ? uh : -uh);
        var mid = (a + b) / 2;
        void F(int p, int q, int r, int s) { var centre = (c[p] + c[q] + c[r] + c[s]) / 4; Poly(colour, centre - mid, c[p], c[q], c[r], c[s]); }
        F(0, 1, 3, 2); F(4, 5, 7, 6); F(0, 1, 5, 4); F(2, 3, 7, 6); F(0, 2, 6, 4); F(1, 3, 7, 5);
    }

    // A cylinder (or a cone, a frustum, a disc) from a to b, radius r0 at a and r1 at b; caps where the radius isn't 0.
    public void Cylinder(Vector3 a, Vector3 b, double r0, double r1, int sides, Rgb colour, Rgb? cap = null, bool capA = true, bool capB = true, double phase = 0)
    {
        var d = Vector3.Normalize(b - a);
        var u = Math.Abs(d.Y) > 0.95f ? Vector3.UnitX : Vector3.Normalize(Vector3.Cross(d, Vector3.UnitY));
        var v = Vector3.Cross(d, u);
        Vector3 Ring(Vector3 o, double r, int k)
        {
            var t = 2 * Math.PI * (k + phase) / sides;
            return o + u * (float)(r * Math.Cos(t)) + v * (float)(r * Math.Sin(t));
        }
        for (var k = 0; k < sides; k++)
        {
            var mid = (Ring(a, (r0 + r1) / 2, k) + Ring(a, (r0 + r1) / 2, k + 1)) / 2 - a;
            var outward = mid - d * Vector3.Dot(mid, d) + d * (float)((r0 - r1) * 0.5);
            if (r1 <= 0) Poly(colour, outward, Ring(a, r0, k), Ring(a, r0, k + 1), b);
            else if (r0 <= 0) Poly(colour, outward, a, Ring(b, r1, k + 1), Ring(b, r1, k));
            else Poly(colour, outward, Ring(a, r0, k), Ring(a, r0, k + 1), Ring(b, r1, k + 1), Ring(b, r1, k));
            if (capA && r0 > 0 && sides > 2) Poly(cap ?? colour, -d, a, Ring(a, r0, k), Ring(a, r0, k + 1));
            if (capB && r1 > 0 && sides > 2) Poly(cap ?? colour, d, b, Ring(b, r1, k), Ring(b, r1, k + 1));
        }
    }

    // An ellipsoid round c (rings from pole to pole, segments round), each point pushed out by `bump` (a lumpy snowball, a stone dome);
    // `below` false leaves out its lower half under c (a dome).
    public void Ellipsoid(Vector3 c, double rx, double ry, double rz, int rings, int segments, Rgb colour, Func<int, int, double>? bump = null,
        bool below = true, Func<int, int, Rgb>? colourAt = null)
    {
        Vector3 At(int i, int j)
        {
            var theta = Math.PI * i / rings; var phi = 2 * Math.PI * j / segments;
            var k = bump?.Invoke(i, j % segments) ?? 1;
            if (i == 0 || i == rings) k = bump?.Invoke(i, 0) ?? 1;
            return c + new Vector3((float)(rx * k * Math.Sin(theta) * Math.Cos(phi)), (float)(ry * k * Math.Cos(theta)), (float)(rz * k * Math.Sin(theta) * Math.Sin(phi)));
        }
        var last = below ? rings : rings / 2;
        for (var i = 0; i < last; i++)
            for (var j = 0; j < segments; j++)
            {
                var col = colourAt?.Invoke(i, j) ?? colour;
                var p = new[] { At(i, j), At(i, j + 1), At(i + 1, j + 1), At(i + 1, j) };
                var centre = (p[0] + p[1] + p[2] + p[3]) / 4;
                if (i == 0) Poly(col, centre - c, p[0], p[2], p[3]);
                else if (i == rings - 1) Poly(col, centre - c, p[0], p[1], p[2]);
                else Poly(col, centre - c, p);
            }
    }

    // The body, unlit (its faces' colours as they are), its points within what a body holds.
    public byte[] Write(string what)
    {
        if (Points.Any(p => Math.Abs(p.X) > 32767 || Math.Abs(p.Y) > 32767 || Math.Abs(p.Z) > 32767))
            throw new InvalidDataException($"{what}: bigger than a body holds");
        return RaceTrackPipes.Write(Points, Faces, lit: false);
    }

    public (Vector3 Lo, Vector3 Hi) Bounds()
    {
        var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue);
        foreach (var p in Points) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
        return (lo, hi);
    }
}
