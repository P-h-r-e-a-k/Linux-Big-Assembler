using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain.Palace;

// An interior's bricks made a 3D building on an island -- shared by the Palace island (PalaceIsland) and the control tower's lower level on
// Island CX (ControlTowerIsland). The bricks are pictures, not shapes: every filled cell is a box in the average colour of its brick, only the
// faces that show are kept, merged into rectangles of one colour; the faces go into bodies the engine takes, each a decor whose box nothing
// touches (it is drawn, not walked on); the collision is the filled cells merged into boxes, each a decor with an empty body.
internal static class BrickBuilding
{
    public const int MaxFaces = 540, MaxPoints = 540;   // a body's polygons and points at the most (the engine's 550: Body.Limit)
    public const int MaxRun = 12;                       // a merged rectangle's cells along a side at the most ...
    public const float MaxSpan = 56000;                 // ... and a body's extent (its points within a signed 16-bit of its origin)
    public const int NoBoxTop = -32000;                 // a decor box's top that nothing touches
    // the faces' shades, as the exporter's: top, bottom, east and west, south and north
    public static readonly (int Dx, int Dy, int Dz, float Shade)[] Directions =
        { (0, 1, 0, 1.00f), (0, -1, 0, 0.50f), (1, 0, 0, 0.84f), (-1, 0, 0, 0.84f), (0, 0, 1, 0.70f), (0, 0, -1, 0.70f) };

    // A face: its four corners (in the world), its colour (the island palette's), the way it faces.
    public readonly record struct Face(Vector3[] Quad, int Colour, Vector3 Normal);

    // The faces that show, merged into rectangles of one colour, group by group: `cells` the filled cells and their brick and group, `colour`
    // a brick's colour seen from a side of that shade, `corner` a cell corner's place in the world (corner (x, y, z) is cell (x, y, z)'s
    // lowest).
    public static Dictionary<int, List<Face>> Faces(Dictionary<(int X, int Y, int Z), (int Brick, int Group)> cells, Func<int, float, int> colour,
        Func<int, int, int, Vector3> corner)
    {
        // (each face: its group, which way it faces, the plane it is in, and its two coordinates in that plane)
        var planes = new Dictionary<(int Group, int Dir, int Plane), Dictionary<(int U, int V), int>>();
        foreach (var ((x, y, z), (brick, group)) in cells)
            for (var d = 0; d < Directions.Length; d++)
            {
                var (dx, dy, dz, shade) = Directions[d];
                if (cells.ContainsKey((x + dx, y + dy, z + dz))) continue;
                var key = (group, d, dy != 0 ? y : dx != 0 ? x : z);
                if (!planes.TryGetValue(key, out var plane)) planes[key] = plane = new();
                plane[dy != 0 ? (x, z) : dx != 0 ? (z, y) : (x, y)] = colour(brick, shade);
            }
        var faces = new Dictionary<int, List<Face>>();
        foreach (var ((group, d, at), plane) in planes)
        {
            var (dx, dy, dz, _) = Directions[d];
            var list = faces.TryGetValue(group, out var l) ? l : faces[group] = new();
            foreach (var (u0, v0, u1, v1, c) in Rectangles(plane))
            {
                // (the rectangle's corners in the world: the plane at the cells' face on that side)
                Vector3 P(int u, int v) => dy != 0 ? corner(u, at + (dy > 0 ? 1 : 0), v)
                                       : dx != 0 ? corner(at + (dx > 0 ? 1 : 0), v, u)
                                                 : corner(u, v, at + (dz > 0 ? 1 : 0));
                list.Add(new Face(new[] { P(u0, v0), P(u1 + 1, v0), P(u1 + 1, v1 + 1), P(u0, v1 + 1) }, c, new Vector3(dx, dy, dz)));
            }
        }
        return faces;
    }

    // A box's six faces, its top in `top` and its sides and bottom in `side` (a roof slab, a lid).
    public static void AddBox(List<Face> list, Vector3 lo, Vector3 hi, int top, int side)
    {
        float xa = lo.X, ya = lo.Y, za = lo.Z, xb = hi.X, yb = hi.Y, zb = hi.Z;
        void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 e, int colour, Vector3 n) => list.Add(new Face(new[] { a, b, c, e }, colour, n));
        Q(new(xa, yb, za), new(xb, yb, za), new(xb, yb, zb), new(xa, yb, zb), top, Vector3.UnitY);
        Q(new(xa, ya, za), new(xb, ya, za), new(xb, ya, zb), new(xa, ya, zb), side, -Vector3.UnitY);
        Q(new(xa, ya, za), new(xb, ya, za), new(xb, yb, za), new(xa, yb, za), side, -Vector3.UnitZ);
        Q(new(xa, ya, zb), new(xb, ya, zb), new(xb, yb, zb), new(xa, yb, zb), side, Vector3.UnitZ);
        Q(new(xa, ya, za), new(xa, ya, zb), new(xa, yb, zb), new(xa, yb, za), side, -Vector3.UnitX);
        Q(new(xb, ya, za), new(xb, ya, zb), new(xb, yb, zb), new(xb, yb, za), side, Vector3.UnitX);
    }

    // Each group's faces as bodies (a few where they are many), appended to `newBodies` (numbered from `firstBody` on), each a decor nothing
    // touches. Returns the bodies, the faces, and the decors left out (a cube full).
    public static (int Bodies, int Faces, int Left) AddBodies(IslandFile island, Dictionary<int, List<Face>> groups, List<byte[]> newBodies, int firstBody,
        Func<int, string>? what = null)
    {
        int faceCount = 0, bodyCount = 0, left = 0;
        foreach (var (group, faces) in groups)
            foreach (var chunk in Chunks(faces))
            {
                var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue);
                foreach (var f in chunk) foreach (var p in f.Quad) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
                // (the origin at the middle of the chunk: every point within what a body holds)
                var origin = new Vector3((lo.X + hi.X) / 2, (lo.Y + hi.Y) / 2, (lo.Z + hi.Z) / 2);
                var pts = new List<Vector3>(); var bodyFaces = new List<LbaBodyStudio.Face>();
                var shared = new Dictionary<Vector3, int>();
                foreach (var (quad, colour, normal) in chunk)
                {
                    var ids = quad.Select(p => { if (!shared.TryGetValue(p, out var i)) { pts.Add(p - origin); shared[p] = i = pts.Count - 1; } return i; }).ToArray();
                    // (wound so the plain cross product points the way it faces: outwards)
                    var n = Vector3.Cross(quad[1] - quad[0], quad[2] - quad[0]);
                    if (Vector3.Dot(n, normal) < 0) Array.Reverse(ids);
                    bodyFaces.Add(new LbaBodyStudio.Face(ids, colour, Material: 0));
                }
                var body = firstBody + newBodies.Count;
                if (pts.Any(p => Math.Abs(p.X) > 32767 || Math.Abs(p.Y) > 32767 || Math.Abs(p.Z) > 32767))
                    throw new InvalidDataException($"{what?.Invoke(group) ?? $"group {group}"}: a body of {chunk.Count} faces from {lo} to {hi} is bigger than a body holds");
                newBodies.Add(RaceTrackPipes.Write(pts, bodyFaces, lit: false));
                faceCount += chunk.Count; bodyCount++;
                if (IslandDecors.Locate(island, origin.X, origin.Z) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCube) { left++; continue; }
                var d = IslandDecors.Blank(body, at.X, (int)Math.Round(origin.Y), at.Z);
                d.XMin = (int)Math.Floor(at.X + lo.X - origin.X); d.XMax = (int)Math.Ceiling(at.X + hi.X - origin.X);
                d.ZMin = (int)Math.Floor(at.Z + lo.Z - origin.Z); d.ZMax = (int)Math.Ceiling(at.Z + hi.Z - origin.Z);
                d.YMin = (int)Math.Floor(lo.Y); d.YMax = NoBoxTop;     // (its Y the middle of its points: the body is drawn round it)
                at.Cube.Decors.Add(d);
            }
        return (bodyCount, faceCount, left);
    }

    // The collision boxes (world units), each a decor with the empty body `empty`. Returns the boxes placed and those left out (a cube full).
    public static (int Boxes, int Left) AddBoxes(IslandFile island, int empty, IEnumerable<(double X0, double Y0, double Z0, double X1, double Y1, double Z1)> boxes)
    {
        int count = 0, left = 0;
        foreach (var (bx0, by0, bz0, bx1, by1, bz1) in boxes)
        {
            double cx = (bx0 + bx1) / 2, cz = (bz0 + bz1) / 2;
            if (IslandDecors.Locate(island, cx, cz) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCube) { left++; continue; }
            var d = IslandDecors.Blank(empty, at.X, (int)Math.Round(by0), at.Z);
            d.XMin = (int)Math.Floor(at.X + bx0 - cx); d.XMax = (int)Math.Ceiling(at.X + bx1 - cx);
            d.ZMin = (int)Math.Floor(at.Z + bz0 - cz); d.ZMax = (int)Math.Ceiling(at.Z + bz1 - cz);
            d.YMin = (int)Math.Floor(by0); d.YMax = (int)Math.Ceiling(by1);
            at.Cube.Decors.Add(d);
            count++;
        }
        return (count, left);
    }

    // The nearest colour of a palette (6-bit or 8-bit) to `c`, weighted as the eye sees, entries 1-254; remembered in `cache`.
    public static int Nearest(byte[] palette, (float R, float G, float B) c, Dictionary<(float, float, float), int> cache)
    {
        if (cache.TryGetValue(c, out var known)) return known;
        var best = 1; var bd = double.MaxValue;
        for (var i = 1; i < 255; i++)
        {
            var p = Colour(palette, i);
            double d = (p.R - c.R) * (p.R - c.R) * 0.3 + (p.G - c.G) * (p.G - c.G) * 0.59 + (p.B - c.B) * (p.B - c.B) * 0.11;
            if (d < bd) { bd = d; best = i; }
        }
        return cache[c] = best;
    }

    private static readonly Dictionary<byte[], bool> SixBit = new(ReferenceEqualityComparer.Instance);

    // A colour of a 256-entry palette (6-bit, as the islands' are, or 8-bit), as the exporter reads them (Export/Palettes).
    public static (byte R, byte G, byte B) Colour(byte[] palette, int index)
    {
        var i = index * 3;
        if (i + 2 >= palette.Length) return (200, 200, 200);
        bool six;
        lock (SixBit) if (!SixBit.TryGetValue(palette, out six)) SixBit[palette] = six = palette.Take(Math.Min(768, palette.Length)).All(v => v < 64);
        byte S(byte v) => (byte)Math.Min(255, six ? v * 4 : v);
        return (S(palette[i]), S(palette[i + 1]), S(palette[i + 2]));
    }

    // The mean colour of a brick's drawn pixels (LBA_BKG's run-length pictures: width, lines, hot spot, then per line its runs), as the
    // exporter's blocky maps colour their boxes (Export/Meshers GridMesher).
    public static (float R, float G, float B) BrickAverage(byte[]? data, byte[] palette)
    {
        if (data is null || data.Length < 4) return (150, 150, 150);
        double r = 0, g = 0, b = 0; var n = 0;
        var src = 4;
        int lines = data[1];
        for (var line = 0; line < lines && src < data.Length; line++)
        {
            int runs = data[src++];
            for (var run = 0; run < runs && src < data.Length; run++)
            {
                var control = data[src++];
                var count = (control & 0x3F) + 1;
                switch (control >> 6)
                {
                    case 0: break;
                    case 1:
                        for (var k = 0; k < count && src < data.Length; k++) { var c = Colour(palette, data[src++]); r += c.R; g += c.G; b += c.B; n++; }
                        break;
                    default:
                        if (src >= data.Length) break;
                        var colour = Colour(palette, data[src++]);
                        r += colour.R * count; g += colour.G * count; b += colour.B * count; n += count;
                        break;
                }
            }
        }
        return n == 0 ? (150, 150, 150) : ((float)(r / n), (float)(g / n), (float)(b / n));
    }

    // A group's faces in bodies the engine takes: as many as fit under MaxFaces polygons and MaxPoints points (the corners they share counted
    // once), in the order they come.
    public static IEnumerable<List<Face>> Chunks(List<Face> faces)
    {
        var chunk = new List<Face>();
        var points = new HashSet<Vector3>();
        var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue);
        // (in order across the group: a body's faces near one another)
        foreach (var f in faces.OrderBy(f => Math.Floor(f.Quad[0].Z / 20000)).ThenBy(f => f.Quad[0].X).ThenBy(f => f.Quad[0].Z))
        {
            var fresh = f.Quad.Count(p => !points.Contains(p));
            var flo = f.Quad.Aggregate(lo, Vector3.Min); var fhi = f.Quad.Aggregate(hi, Vector3.Max);
            var span = fhi - flo;
            if (chunk.Count > 0 && (chunk.Count + 1 > MaxFaces || points.Count + fresh > MaxPoints || span.X > MaxSpan || span.Z > MaxSpan || span.Y > MaxSpan))
            {
                yield return chunk;
                chunk = new(); points.Clear();
                lo = new Vector3(float.MaxValue); hi = new Vector3(float.MinValue);
                flo = f.Quad.Aggregate(lo, Vector3.Min); fhi = f.Quad.Aggregate(hi, Vector3.Max);
            }
            chunk.Add(f);
            lo = flo; hi = fhi;
            foreach (var p in f.Quad) points.Add(p);
        }
        if (chunk.Count > 0) yield return chunk;
    }

    // A plane's faces merged into rectangles of one colour (u0, v0, u1, v1 inclusive): grown along u, then along v as far as the whole row
    // matches.
    public static IEnumerable<(int U0, int V0, int U1, int V1, int Colour)> Rectangles(Dictionary<(int U, int V), int> plane)
    {
        var done = new HashSet<(int, int)>();
        foreach (var (u, v) in plane.Keys.OrderBy(k => k.V).ThenBy(k => k.U))
        {
            if (done.Contains((u, v))) continue;
            var colour = plane[(u, v)];
            var u1 = u;
            while (u1 - u + 1 < MaxRun && plane.TryGetValue((u1 + 1, v), out var c) && c == colour && !done.Contains((u1 + 1, v))) u1++;
            var v1 = v;
            while (v1 - v + 1 < MaxRun)
            {
                var row = v1 + 1; var ok = true;
                for (var k = u; k <= u1 && ok; k++) ok = plane.TryGetValue((k, row), out var c) && c == colour && !done.Contains((k, row));
                if (!ok) break;
                v1 = row;
            }
            for (var j = v; j <= v1; j++) for (var k = u; k <= u1; k++) done.Add((k, j));
            yield return (u, v, u1, v1, colour);
        }
    }

    // The filled cells merged into boxes (x0, y0, z0, x1, y1, z1 inclusive): grown along x, then z, then y while every cell is filled.
    public static List<(int X0, int Y0, int Z0, int X1, int Y1, int Z1)> Boxes(HashSet<(int X, int Y, int Z)> filled)
    {
        var done = new HashSet<(int, int, int)>();
        var result = new List<(int, int, int, int, int, int)>();
        bool Free((int, int, int) c) => filled.Contains(c) && !done.Contains(c);
        foreach (var (x, y, z) in filled.OrderBy(c => c.Y).ThenBy(c => c.Z).ThenBy(c => c.X))
        {
            if (done.Contains((x, y, z))) continue;
            var x1 = x;
            while (Free((x1 + 1, y, z))) x1++;
            var z1 = z;
            while (Enumerable.Range(x, x1 - x + 1).All(k => Free((k, y, z1 + 1)))) z1++;
            var y1 = y;
            while (Enumerable.Range(x, x1 - x + 1).All(k => Enumerable.Range(z, z1 - z + 1).All(j => Free((k, y1 + 1, j))))) y1++;
            for (var a = x; a <= x1; a++) for (var b = y; b <= y1; b++) for (var c = z; c <= z1; c++) done.Add((a, b, c));
            result.Add((x, y, z, x1, y1, z1));
        }
        return result;
    }

    // A cube's cells, as a region to paint.
    public sealed class CubeCells : IslandRegion
    {
        private readonly int cx, cz;
        public CubeCells(int cx, int cz) { this.cx = cx; this.cz = cz; }
        public override IEnumerable<(int Gx, int Gz, double Weight)> Vertices(IslandFile island)
        {
            for (var z = 0; z < 64; z++) for (var x = 0; x < 64; x++) yield return (cx * 64 + x, cz * 64 + z, 1.0);
        }
        public override (double Gx, double Gz) Center => (cx * 64 + 32, cz * 64 + 32);
    }
}
