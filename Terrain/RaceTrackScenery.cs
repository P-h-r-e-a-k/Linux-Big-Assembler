using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// An island's decor objects as the triangles they are drawn with, in the island's world units -- their collision boxes say little (an
// airship's box is a block eight cells square, a tower's frame is a box as wide as the lamp on its corner). A raised road's piers stand
// on what is really under them and keep out of what is really beside them (RaceTrackBuilder.PlacePiers).
internal sealed class RaceTrackScenery
{
    private readonly record struct Tri(Vector3 A, Vector3 B, Vector3 C, int Body);
    private const float Square = 1024;
    private readonly List<Tri> tris = new();
    private readonly Dictionary<(int, int), List<int>> squares = new();

    // The island's decors as they stand in it now, with the bodies of the OBL at `oblPath`; null when that can't be read.
    public static RaceTrackScenery? Load(IslandFile island, string oblPath)
    {
        HqrArchive bodies;
        try { bodies = HqrArchive.Open(oblPath); } catch (Exception e) when (e is IOException or InvalidDataException) { return null; }
        var scenery = new RaceTrackScenery();
        var read = new Dictionary<int, Body?>();
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
            foreach (var d in cube.Decors)
            {
                var index = d.Body & 0xFFFF;
                if (!read.TryGetValue(index, out var body))
                {
                    try { body = Body.Read(bodies.Read(index), 2, allowStatic: true); }
                    catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or IndexOutOfRangeException or EndOfStreamException) { body = null; }
                    read[index] = body;
                }
                if (body is null) continue;
                var angle = (float)((d.Beta & 0xFFFF) * 2 * Math.PI / 4096);
                var place = Matrix4x4.CreateRotationY(angle) * Matrix4x4.CreateTranslation(cx * (float)IslandFile.CubeSize + d.X, d.Y, cz * (float)IslandFile.CubeSize + d.Z);
                var points = body.Vertices.Select(v => Vector3.Transform(v, place)).ToList();
                foreach (var f in body.Faces)
                    for (var t = 1; t + 1 < f.Points.Length; t++)
                        scenery.Add(new Tri(points[f.Points[0]], points[f.Points[t]], points[f.Points[t + 1]], index));
            }
        return scenery;
    }

    private void Add(Tri t)
    {
        var id = tris.Count;
        tris.Add(t);
        int x0 = (int)MathF.Floor(MathF.Min(t.A.X, MathF.Min(t.B.X, t.C.X)) / Square), x1 = (int)MathF.Floor(MathF.Max(t.A.X, MathF.Max(t.B.X, t.C.X)) / Square);
        int z0 = (int)MathF.Floor(MathF.Min(t.A.Z, MathF.Min(t.B.Z, t.C.Z)) / Square), z1 = (int)MathF.Floor(MathF.Max(t.A.Z, MathF.Max(t.B.Z, t.C.Z)) / Square);
        for (var z = z0; z <= z1; z++)
            for (var x = x0; x <= x1; x++)
            {
                if (!squares.TryGetValue((x, z), out var list)) squares[(x, z)] = list = new();
                list.Add(id);
            }
    }

    // The highest of the objects' surfaces straight under a place, below `below`: its height and the body it belongs to.
    public (double Y, int Body)? Under(double x, double z, double below)
    {
        if (!squares.TryGetValue(((int)Math.Floor(x / Square), (int)Math.Floor(z / Square)), out var list)) return null;
        (double Y, int Body)? best = null;
        foreach (var id in list)
        {
            var t = tris[id];
            double ax = t.A.X, az = t.A.Z, bx = t.B.X, bz = t.B.Z, cx = t.C.X, cz = t.C.Z;
            var den = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
            if (Math.Abs(den) < 1e-6) continue;                     // (seen edge on: a wall)
            var w0 = ((bz - cz) * (x - cx) + (cx - bx) * (z - cz)) / den;
            var w1 = ((cz - az) * (x - cx) + (ax - cx) * (z - cz)) / den;
            var w2 = 1 - w0 - w1;
            if (w0 < -1e-4 || w1 < -1e-4 || w2 < -1e-4) continue;
            var y = w0 * t.A.Y + w1 * t.B.Y + w2 * t.C.Y;
            if (y >= below || (best is { } b && y <= b.Y)) continue;
            best = (y, t.Body);
        }
        return best;
    }

    // Whether any object's surface passes through a box (the separating-axis test of a triangle against a box).
    public bool Touches(double x0, double y0, double z0, double x1, double y1, double z1)
    {
        if (x1 <= x0 || y1 <= y0 || z1 <= z0) return false;
        var centre = new Vector3((float)((x0 + x1) / 2), (float)((y0 + y1) / 2), (float)((z0 + z1) / 2));
        var half = new Vector3((float)((x1 - x0) / 2), (float)((y1 - y0) / 2), (float)((z1 - z0) / 2));
        var seen = new HashSet<int>();
        for (var sz = (int)Math.Floor(z0 / Square); sz <= (int)Math.Floor(z1 / Square); sz++)
            for (var sx = (int)Math.Floor(x0 / Square); sx <= (int)Math.Floor(x1 / Square); sx++)
            {
                if (!squares.TryGetValue((sx, sz), out var list)) continue;
                foreach (var id in list)
                    if (seen.Add(id) && Overlaps(tris[id], centre, half)) return true;
            }
        return false;
    }

    private static bool Overlaps(Tri t, Vector3 centre, Vector3 half)
    {
        Vector3 a = t.A - centre, b = t.B - centre, c = t.C - centre;
        // the box's own axes
        if (MathF.Min(a.X, MathF.Min(b.X, c.X)) > half.X || MathF.Max(a.X, MathF.Max(b.X, c.X)) < -half.X) return false;
        if (MathF.Min(a.Y, MathF.Min(b.Y, c.Y)) > half.Y || MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) < -half.Y) return false;
        if (MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) > half.Z || MathF.Max(a.Z, MathF.Max(b.Z, c.Z)) < -half.Z) return false;
        Vector3 e0 = b - a, e1 = c - b, e2 = a - c;
        // the triangle's plane
        var normal = Vector3.Cross(e0, e1);
        if (MathF.Abs(Vector3.Dot(normal, a)) > half.X * MathF.Abs(normal.X) + half.Y * MathF.Abs(normal.Y) + half.Z * MathF.Abs(normal.Z)) return false;
        // the nine cross products of the box's axes and the triangle's edges
        foreach (var e in new[] { e0, e1, e2 })
            foreach (var axis in new[] { new Vector3(0, -e.Z, e.Y), new Vector3(e.Z, 0, -e.X), new Vector3(-e.Y, e.X, 0) })
            {
                float p0 = Vector3.Dot(a, axis), p1 = Vector3.Dot(b, axis), p2 = Vector3.Dot(c, axis);
                var reach = half.X * MathF.Abs(axis.X) + half.Y * MathF.Abs(axis.Y) + half.Z * MathF.Abs(axis.Z);
                if (MathF.Min(p0, MathF.Min(p1, p2)) > reach || MathF.Max(p0, MathF.Max(p1, p2)) < -reach) return false;
            }
        return true;
    }
}
