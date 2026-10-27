using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Characters;

// A character's body made in the Character maker (2026-10-23, recreating LBA1's Hamalayi by hand: Twinsen on his snowboard, the slalom
// flags, the snowboard to pick up): a game body to start from, or nothing (a root with one point), with parts added -- each part a new bone
// of its own (a box, a flat sheet or a flat triangle, in one of the palette's colours) hung from a point of a bone already there, so the
// body's own bones, points and polygons stay as they were and its animations still fit it (a new bone's slot past theirs keeps still).
internal static class CharacterBody
{
    public enum Shape { Box, Sheet, Triangle }

    public static readonly string[] ShapeNames = { "Box", "Flat sheet (both sides)", "Flat triangle (both sides)" };

    // An empty body: one bone, one point at the feet (the point new parts hang from).
    public static Body Empty() => new()
    {
        Game = 2, Lit = true, Header = new byte[96],
        Vertices = new List<Vector3> { Vector3.Zero },
        Bones = new List<Bone> { new(0, 1, 0, -1, new byte[8]) },
    };

    // A game body to start from, ready to be written again as a body of the character's: lit (its point normals made from its shape when
    // written), its polygons drawn as the game draws them -- textured ones textured, flat unlit (type 0) and see-through (type 2) ones as
    // they are, every other one Gouraud-lit (the game's flat-lit type 1 needs face normals the writer doesn't make: Gouraud is near it).
    public static Body FromGame(byte[] data)
    {
        var body = Body.Read(data, 2);
        body.Lit = true;
        body.Faces = body.Faces.Select(f => f.Texture is not null || f.Material is 0 or 2 ? f : f with { Material = -1 }).ToList();
        if (BitConverter.ToInt32(body.Header, 0) is var info && (info & 256) == 0) BitConverter.GetBytes(info | 256).CopyTo(body.Header, 0);
        return body;
    }

    // Adds a part: a new bone hung from `pivot` (a point of the body already there -- its parent bone's), its points `offset` from that
    // point (the part's middle), `size` across (x), up (y) and deep (z), turned `yawDegrees` about the up axis. Returns the new bone.
    public static int AddPart(Body body, Shape shape, int pivot, Vector3 offset, Vector3 size, double yawDegrees, int colour)
    {
        if (pivot < 0 || pivot >= body.Vertices.Count) throw new InvalidOperationException($"The body has no point {pivot} (it has {body.Vertices.Count}).");
        if (body.Bones.Count >= 30) throw new InvalidOperationException("A body has 30 bones at the most (the engine's).");
        var parent = body.Bones.FindIndex(b => pivot >= b.Start && pivot < b.Start + b.Count);
        float hx = size.X / 2, hy = size.Y / 2, hz = size.Z / 2;
        var c = Math.Cos(yawDegrees * Math.PI / 180); var s = Math.Sin(yawDegrees * Math.PI / 180);
        Vector3 P(float x, float y, float z) => new((float)(x * c + z * s) + offset.X, y + offset.Y, (float)(z * c - x * s) + offset.Z);
        var points = shape switch
        {
            Shape.Box => new[] { P(-hx, -hy, -hz), P(hx, -hy, -hz), P(hx, hy, -hz), P(-hx, hy, -hz), P(-hx, -hy, hz), P(hx, -hy, hz), P(hx, hy, hz), P(-hx, hy, hz) },
            // (a sheet: upright, across x and up y -- a flag, a sign. Each of its sides has points of its own: one point shared by both
            // sides would have its light's normal made of the two sides' -- nothing -- and the game would draw both dark)
            Shape.Sheet => new[] { P(-hx, -hy, 0), P(hx, -hy, 0), P(hx, hy, 0), P(-hx, hy, 0), P(-hx, -hy, 0), P(hx, -hy, 0), P(hx, hy, 0), P(-hx, hy, 0) },
            _ => new[] { P(-hx, -hy, 0), P(hx, 0, 0), P(-hx, hy, 0), P(-hx, -hy, 0), P(hx, 0, 0), P(-hx, hy, 0) },
        };
        var start = body.Vertices.Count;
        if (start + points.Length > body.Limit) throw new InvalidOperationException($"A body has {body.Limit} points at the most.");
        body.Vertices.AddRange(points);
        body.Bones.Add(new Bone(start, points.Length, pivot, Math.Max(0, parent), new byte[8]));
        int[] F(params int[] k) => k.Select(i => start + i).ToArray();
        var faces = shape switch
        {
            // (a box's sides wound so the Newell normal points out, as the game's own bodies' do: OutwardWinding makes sure; a sheet's two
            // sides, one each way)
            Shape.Box => new[] { F(0, 3, 2, 1), F(4, 5, 6, 7), F(0, 1, 5, 4), F(3, 7, 6, 2), F(0, 4, 7, 3), F(1, 2, 6, 5) },
            Shape.Sheet => new[] { F(0, 1, 2, 3), F(7, 6, 5, 4) },
            _ => new[] { F(0, 1, 2), F(5, 4, 3) },
        };
        foreach (var f in faces) body.Faces.Add(new Face(f, colour));
        if (shape == Shape.Box) OutwardWinding(body, start, points.Length, faces.Length);
        return body.Bones.Count - 1;
    }

    // A box's faces turned so their normals point away from its middle.
    private static void OutwardWinding(Body body, int start, int count, int faces)
    {
        var middle = Vector3.Zero;
        for (var i = start; i < start + count; i++) middle += body.Vertices[i];
        middle /= count;
        for (var k = body.Faces.Count - faces; k < body.Faces.Count; k++)
        {
            var f = body.Faces[k];
            var p = f.Points.Select(i => body.Vertices[i]).ToArray();
            var n = Vector3.Zero;
            for (var i = 0; i < p.Length; i++)
            {
                var a = p[i]; var b = p[(i + 1) % p.Length];
                n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
            }
            var centre = p.Aggregate(Vector3.Zero, (x, y) => x + y) / p.Length;
            if (Vector3.Dot(n, centre - middle) < 0) body.Faces[k] = f with { Points = f.Points.Reverse().ToArray() };
        }
    }

    // Takes the last part away (the last bone, its points and its polygons), when nothing hangs from it.
    public static void RemoveLastPart(Body body)
    {
        if (body.Bones.Count <= 1) throw new InvalidOperationException("The body has no part to take away (its first bone stays).");
        var last = body.Bones.Count - 1;
        var bone = body.Bones[last];
        if (body.Bones.Any(b => b.Parent == last && b != bone)) throw new InvalidOperationException("Something hangs from the last part.");
        body.Faces.RemoveAll(f => f.Points.Any(p => p >= bone.Start));
        body.Lines.RemoveAll(l => l.A >= bone.Start || l.B >= bone.Start);
        body.Spheres.RemoveAll(s => s.Point >= bone.Start);
        body.Vertices.RemoveRange(bone.Start, bone.Count);
        body.Bones.RemoveAt(last);
        if (body.LightScale is { } ls && ls.Length > body.Vertices.Count) body.LightScale = ls[..body.Vertices.Count];
    }

    // Every polygon of a bone in one colour.
    public static int Recolour(Body body, int bone, int colour)
    {
        if (bone < 0 || bone >= body.Bones.Count) throw new InvalidOperationException($"The body has no bone {bone}.");
        var b = body.Bones[bone];
        var n = 0;
        for (var k = 0; k < body.Faces.Count; k++)
        {
            var f = body.Faces[k];
            if (f.Texture is not null || f.Points.Any(p => p < b.Start || p >= b.Start + b.Count)) continue;
            body.Faces[k] = f with { Colour = colour };
            n++;
        }
        return n;
    }

    // The body's extent (the neutral pose): its collision box in the entity.
    public static (short X0, short Y0, short Z0, short X1, short Y1, short Z1) Extent(Body body)
    {
        var w = body.World();
        static short S(float v) => (short)Math.Clamp((int)Math.Round(v), short.MinValue, short.MaxValue);
        return (S(w.Min(v => v.X)), S(w.Min(v => v.Y)), S(w.Min(v => v.Z)), S(w.Max(v => v.X)), S(w.Max(v => v.Y)), S(w.Max(v => v.Z)));
    }
}
