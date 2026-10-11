using System.Buffers.Binary;
using System.IO;

namespace LBAAssembler.Terrain;

// A hut the road drives through (RaceTrackPlan.DriveThrough): the building whose pieces share the origin of a decor of Body, made Scale
// times its size and cut open where the road runs through it -- the Island of the Francos' furry dome huts, the user's "enlarge the houses
// and have Twinsen driving through some of them" (2026-10-07).
internal sealed class DriveThroughHut
{
    public int Body { get; set; }
    public double Scale { get; set; } = 1.0;
    // The passage's height over the deck, when not the usual Clearance, and its half width (cells, without Margin), when not the road's:
    // Otringal's bridge is a tube whose roof is low over the road through it -- a lower passage keeps its roof (2026-10-08).
    public double? Clearance { get; set; }
    public double? Half { get; set; }
}

// The huts made drive-through, before the road's clearing: each of the building's pieces is
//  - made Scale times its size about the building's origin (its points and its boxes: the ground it stands on is its own floor),
//  - cut open: its polygons whose middle is over the road (within its rails and Margin) and under the deck plus Clearance are dropped --
//    a passage through it as high as a car needs -- and the rest made two-sided (each polygon a copy wound the other way: from inside, a
//    dome's shell is its walls and its ceiling, not the world beyond it),
//  - split into the parts beside the road (left, right) and over it (the roof): each a body and a decor of its own, so that each has its
//    own collision box -- the walls' beside the passage, the roof's above it (the engine stops a car at any decor's box).
// Everything is done on the bodies' bytes (BodyStudio's model rewrites the polygons' types: the huts' lit and textured ones would come
// out flat). Returns the new bodies (the clearing keeps them), and in `moved` each hut's origin and scale (RaceTrackScenes moves the
// doors of its scenes with it).
internal static class RaceTrackDriveThrough
{
    public const double Clearance = 1900, Margin = 0.75;

    public static List<int> Make(IslandFile island, TrackRoad road, DriveThroughHut[] huts, RaceTrackOptions o, RaceTrackReport report)
    {
        var made = new List<int>();
        if (o.SceneryObl is not { } obl || o.NewBodyBase < 0) { report.Notes.Add("WARNING: the island's OBL wasn't counted -- no hut made drive-through"); return made; }
        var hqr = HqrArchive.Open(obl);
        var roadHalf = (road.Raised is not null ? o.RaisedHalfWidth : o.CurbHalfWidth) + Margin;
        foreach (var hut in huts)
        {
            var half = hut.Half is { } h ? h + Margin : roadHalf;
            var clearance = hut.Clearance ?? Clearance;
            // the building: the decors sharing the origin of the first decor of that body
            IslandCube? cube = null; int cx = 0, cz = 0; IslandDecor? first = null;
            foreach (var (x, z, c) in IslandOps.CubeCells(island))
            {
                first = c.Decors.FirstOrDefault(d => (d.Body & 0xFFFF) == hut.Body && IslandFile.ObjectPageOf(d) == 0);
                if (first is not null) { cube = c; cx = x; cz = z; break; }
            }
            if (cube is null || first is null) { report.Notes.Add($"WARNING: drive-through hut {hut.Body}: no decor of that body"); continue; }
            var group = cube.Decors.Where(d => d.X == first.X && d.Z == first.Z && IslandFile.ObjectPageOf(d) == 0).ToList();
            double ox = cx * 64 + first.X / 512.0, oz = cz * 64 + first.Z / 512.0;
            // the road through it: its points within reach of the origin
            var near = Enumerable.Range(0, road.Count).Where(i => Math.Abs(road.X[i] - ox) < 40 && Math.Abs(road.Z[i] - oz) < 40).ToList();
            if (near.Count < 2) { report.Notes.Add($"WARNING: drive-through hut {hut.Body}: the road doesn't pass near it"); continue; }
            var parts = new int[3]; var dropped = 0; var kept = 0; var pieces = 0;
            foreach (var d in group)
            {
                var source = hqr.Read(d.Body & 0xFFFF);
                if (I(source, 32) > 1) { report.Notes.Add($"drive-through hut {hut.Body}: piece {d.Body & 0xFFFF} has bones; left as it is"); continue; }
                var turn = QuarterTurn(source, d);
                if (turn is not { } t) { report.Notes.Add($"drive-through hut {hut.Body}: piece {d.Body & 0xFFFF}'s turn not found; left as it is"); continue; }
                var b = Scaled(source, hut.Scale);
                var pts = Points(b);
                (double Dx, double Dz) World(short[] p) => (t.Sx * p[t.Ax], t.Sz * p[t.Az]);
                // each polygon's part: -1 dropped, 0 left, 1 right, 2 the roof
                var faces = Faces(b);
                var part = new int[faces.Count];
                for (var f = 0; f < faces.Count; f++)
                {
                    double sx = 0, sy = 0, sz = 0;
                    foreach (var p in faces[f].Points) { var (dx, dz) = World(pts[p]); sx += dx; sz += dz; sy += pts[p][1]; }
                    var n = faces[f].Points.Length;
                    double wx = cx * 64 + (d.X + sx / n) / 512.0, wz = cz * 64 + (d.Z + sz / n) / 512.0, wy = d.Y + sy / n;
                    var (dist, deck, side) = Nearest(road, near, wx, wz);
                    part[f] = dist < half ? (wy < deck + clearance ? -1 : 2) : side > 0 ? 0 : 1;
                    if (part[f] < 0) dropped++; else kept++;
                }
                pieces++;
                for (var k = 0; k < 3; k++)
                {
                    var keep = Enumerable.Range(0, faces.Count).Where(f => part[f] == k).ToHashSet();
                    if (keep.Count == 0) continue;
                    var body = Filtered(b, faces, keep, twoSided: true);
                    // its box: the points its polygons use, in the island (cube-local), the walls' kept out of the passage
                    var used = keep.SelectMany(f => faces[f].Points).Distinct().ToList();
                    double x0 = double.MaxValue, x1 = double.MinValue, z0 = double.MaxValue, z1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
                    double lx0 = double.MaxValue, lx1 = double.MinValue, ly0 = double.MaxValue, ly1 = double.MinValue, lz0 = double.MaxValue, lz1 = double.MinValue;
                    foreach (var p in used)
                    {
                        var (dx, dz) = World(pts[p]);
                        x0 = Math.Min(x0, d.X + dx); x1 = Math.Max(x1, d.X + dx); z0 = Math.Min(z0, d.Z + dz); z1 = Math.Max(z1, d.Z + dz);
                        y0 = Math.Min(y0, d.Y + pts[p][1]); y1 = Math.Max(y1, d.Y + pts[p][1]);
                        lx0 = Math.Min(lx0, pts[p][0]); lx1 = Math.Max(lx1, pts[p][0]); ly0 = Math.Min(ly0, pts[p][1]); ly1 = Math.Max(ly1, pts[p][1]);
                        lz0 = Math.Min(lz0, pts[p][2]); lz1 = Math.Max(lz1, pts[p][2]);
                    }
                    if (k < 2) ClampOutOfPassage(road, near, cx, cz, half, k, ref x0, ref x1, ref z0, ref z1);
                    if (k == 2)
                    {
                        // (over the road all its length: Clearance over the deck's highest point under it -- the road can slope through
                        // the hut: twice its size, the Island of the Francos' cluster of huts has the road climbing out of it 1,400)
                        var top = Nearest(road, near, ox, oz).Deck;
                        foreach (var i in near)
                        {
                            double px = (road.X[i] - cx * 64) * 512, pz = (road.Z[i] - cz * 64) * 512;
                            if (px >= x0 - half * 512 && px <= x1 + half * 512 && pz >= z0 - half * 512 && pz <= z1 + half * 512) top = Math.Max(top, road.H[i]);
                        }
                        y0 = Math.Max(y0, top + clearance);
                    }
                    SetBox(body, lx0, lx1, ly0, ly1, lz0, lz1);
                    var nd = d.Clone();
                    nd.Body = o.NewBodyBase + report.NewBodies.Count;
                    report.NewBodies.Add(body);
                    nd.XMin = (int)Math.Floor(x0); nd.XMax = (int)Math.Ceiling(x1);
                    nd.ZMin = (int)Math.Floor(z0); nd.ZMax = (int)Math.Ceiling(z1);
                    nd.YMin = (int)Math.Floor(y0); nd.YMax = (int)Math.Ceiling(y1);
                    if (nd.XMax <= nd.XMin || nd.ZMax <= nd.ZMin) { nd.XMin = nd.XMax = (int)Math.Round((x0 + x1) / 2); nd.ZMin = nd.ZMax = (int)Math.Round((z0 + z1) / 2); }
                    cube.Decors.Add(nd);
                    made.Add(nd.Body);
                    parts[k]++;
                }
                cube.Decors.Remove(d);
            }
            report.DriveThroughs.Add((cx, cz, first.X, first.Z, hut.Scale));
            report.Notes.Add($"a hut driven through (body {hut.Body}, cube ({cx},{cz})): {pieces} pieces made {hut.Scale:0.##} times their size, {dropped} polygons cut out " +
                             $"for the road ({kept} left, two-sided), in {parts[0]} wall pieces left of it, {parts[1]} right and {parts[2]} over it");
        }
        return made;
    }

    // ---- the road near a place: how far from its middle (cells), the deck's height there, and on which side (+ left of its way)
    private static (double Dist, double Deck, int Side) Nearest(TrackRoad r, List<int> near, double x, double z)
    {
        var best = (D: double.MaxValue, H: 0.0, S: 1);
        foreach (var i in near)
        {
            var j = (i + 1) % r.Count;
            double ax = r.X[i], az = r.Z[i], bx = r.X[j], bz = r.Z[j], sx = bx - ax, sz = bz - az, l2 = sx * sx + sz * sz;
            var u = l2 > 1e-9 ? Math.Clamp(((x - ax) * sx + (z - az) * sz) / l2, 0, 1) : 0;
            double px = ax + sx * u, pz = az + sz * u, d = Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
            if (d < best.D) best = (d, r.H[i] + (r.H[j] - r.H[i]) * u, sx * (z - az) - sz * (x - ax) > 0 ? 1 : -1);
        }
        return best;
    }

    // a wall part's box kept out of the passage where the road runs along an axis through the hut (the Francos' village's all do)
    private static void ClampOutOfPassage(TrackRoad r, List<int> near, int cx, int cz, double half, int part, ref double x0, ref double x1, ref double z0, ref double z1)
    {
        double ox = cx * 64.0, oz = cz * 64.0;
        var mid = near[near.Count / 2]; var nxt = near[Math.Min(near.Count - 1, near.Count / 2 + 1)];
        double dx = r.X[nxt] - r.X[mid], dz = r.Z[nxt] - r.Z[mid], l = Math.Sqrt(dx * dx + dz * dz);
        if (l < 1e-9) return;
        dx /= l; dz /= l;
        // the road's middle across the box (cells), then cube-local units
        double cxm = ((x0 + x1) / 2) / 512 + ox, czm = ((z0 + z1) / 2) / 512 + oz;
        var (_, _, _) = (0, 0, 0);
        if (Math.Abs(dx) < 0.25)            // along z: the passage is a band of x
        {
            var roadX = near.Select(i => (D: Math.Abs(r.Z[i] - czm), X: r.X[i])).MinBy(t => t.D).X;
            var lo = (roadX - half - ox) * 512; var hi = (roadX + half - ox) * 512;
            if ((x0 + x1) / 2 < (lo + hi) / 2) x1 = Math.Min(x1, lo); else x0 = Math.Max(x0, hi);
        }
        else if (Math.Abs(dz) < 0.25)       // along x: a band of z
        {
            var roadZ = near.Select(i => (D: Math.Abs(r.X[i] - cxm), Z: r.Z[i])).MinBy(t => t.D).Z;
            var lo = (roadZ - half - oz) * 512; var hi = (roadZ + half - oz) * 512;
            if ((z0 + z1) / 2 < (lo + hi) / 2) z1 = Math.Min(z1, lo); else z0 = Math.Max(z0, hi);
        }
    }

    // ---- the body's bytes
    private static int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static void SetI(byte[] b, int at, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), v);
    private static short S(byte[] b, int at) => BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at));
    private static ushort U(byte[] b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));

    // the decor's quarter turn, found by matching the body's box turned to the decor's (as RaceTrackBuilder.Squeezed does): world x is
    // Sx times the body's axis Ax (0 x, 2 z), world z Sz times its axis Az
    private static (int Ax, int Sx, int Az, int Sz)? QuarterTurn(byte[] b, IslandDecor d)
    {
        int lx0 = I(b, 8), lx1 = I(b, 12), lz0 = I(b, 24), lz1 = I(b, 28);
        (int Ax, int Sx, int Az, int Sz)[] turns = { (0, 1, 2, 1), (2, 1, 0, -1), (0, -1, 2, -1), (2, -1, 0, 1) };
        double Error((int Ax, int Sx, int Az, int Sz) t)
        {
            (double Lo, double Hi) W(int axis, int sign) { var (lo, hi) = axis == 0 ? (lx0, lx1) : (lz0, lz1); return sign > 0 ? (lo, hi) : (-hi, -lo); }
            var (wx0, wx1) = W(t.Ax, t.Sx); var (wz0, wz1) = W(t.Az, t.Sz);
            return Math.Abs(wx0 - (d.XMin - d.X)) + Math.Abs(wx1 - (d.XMax - d.X)) + Math.Abs(wz0 - (d.ZMin - d.Z)) + Math.Abs(wz1 - (d.ZMax - d.Z));
        }
        var best = turns.MinBy(Error);
        return Error(best) > 256 ? null : best;
    }

    // its points (x, y, z) in its own coordinates
    private static List<short[]> Points(byte[] b)
    {
        int n = I(b, 40), p = I(b, 44);
        var list = new List<short[]>(n);
        for (var i = 0; i < n; i++, p += 8) list.Add(new[] { S(b, p), S(b, p + 2), S(b, p + 4) });
        return list;
    }

    // a copy with its points and its box `k` times their size
    private static byte[] Scaled(byte[] source, double k)
    {
        var b = (byte[])source.Clone();
        if (Math.Abs(k - 1) < 1e-9) return b;
        int n = I(b, 40), p = I(b, 44);
        for (var i = 0; i < n; i++, p += 8)
            for (var a = 0; a < 3; a++)
                BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(p + 2 * a), (short)Math.Clamp(Math.Round(S(b, p + 2 * a) * k), short.MinValue, short.MaxValue));
        for (var w = 8; w <= 28; w += 4) SetI(b, w, (int)Math.Round(I(b, w) * k));
        return b;
    }

    private static void SetBox(byte[] b, double x0, double x1, double y0, double y1, double z0, double z1)
    {
        SetI(b, 8, (int)Math.Floor(x0)); SetI(b, 12, (int)Math.Ceiling(x1)); SetI(b, 16, (int)Math.Floor(y0));
        SetI(b, 20, (int)Math.Ceiling(y1)); SetI(b, 24, (int)Math.Floor(z0)); SetI(b, 28, (int)Math.Ceiling(z1));
    }

    // its polygons: each its block, its offset and its points
    private sealed record Face(int Block, int At, int[] Points);
    private sealed record Block(int At, ushort Type, int Count, int Stride, bool Quad, bool Env);

    private static List<Block> Blocks(byte[] b)
    {
        var list = new List<Block>();
        int p = I(b, 68), end = I(b, 76);
        while (p < end)
        {
            var type = U(b, p); int n = U(b, p + 2);
            bool quad = (type & 0x8000) != 0, env = (type & 0x4000) != 0, tex = (type & 255) > 7;
            var stride = env ? 16 : tex ? (quad ? 32 : 24) : 12;
            list.Add(new Block(p, type, n, stride, quad, env));
            p += 8 + n * stride;
        }
        return list;
    }

    private static List<Face> Faces(byte[] b)
    {
        var faces = new List<Face>();
        var blocks = Blocks(b);
        for (var k = 0; k < blocks.Count; k++)
        {
            var bl = blocks[k];
            for (var i = 0; i < bl.Count; i++)
            {
                var at = bl.At + 8 + i * bl.Stride;
                var pts = Enumerable.Range(0, bl.Quad ? 4 : 3).Select(j => (int)U(b, at + 2 * j)).ToArray();
                faces.Add(new Face(k, at, pts));
            }
        }
        return faces;
    }

    // a polygon wound the other way round: its points (and its texture's corners with them) in the reverse order
    private static byte[] Reversed(byte[] b, Block bl, int at)
    {
        var e = b.AsSpan(at, bl.Stride).ToArray();
        var np = bl.Quad ? 4 : 3;
        var p = Enumerable.Range(0, np).Select(j => U(e, 2 * j)).Reverse().ToArray();
        for (var j = 0; j < np; j++) BinaryPrimitives.WriteUInt16LittleEndian(e.AsSpan(2 * j), p[j]);
        var tex = (bl.Type & 255) > 7 && !bl.Env;
        if (tex)
        {
            var uvAt = bl.Quad ? 12 : 12;    // (tri: P1 P2 P3 Handle Colour Normal U1 V1 ...; quad: P1..P4 Colour Normal U1 V1 ...)
            var uv = Enumerable.Range(0, np).Select(j => (U(e, uvAt + 4 * j), U(e, uvAt + 4 * j + 2))).Reverse().ToArray();
            for (var j = 0; j < np; j++) { BinaryPrimitives.WriteUInt16LittleEndian(e.AsSpan(uvAt + 4 * j), uv[j].Item1); BinaryPrimitives.WriteUInt16LittleEndian(e.AsSpan(uvAt + 4 * j + 2), uv[j].Item2); }
        }
        return e;
    }

    // a copy with only the polygons `keep` (each block's in its order), each also wound the other way if `twoSided`; the lines, spheres
    // and texture table moved up after them
    private static byte[] Filtered(byte[] b, List<Face> faces, HashSet<int> keep, bool twoSided)
    {
        var blocks = Blocks(b);
        int start = I(b, 68), end = I(b, 76);
        using var ms = new MemoryStream();
        ms.Write(b, 0, start);
        var total = 0;
        for (var k = 0; k < blocks.Count; k++)
        {
            var bl = blocks[k];
            var mine = Enumerable.Range(0, faces.Count).Where(f => faces[f].Block == k && keep.Contains(f)).ToList();
            if (mine.Count == 0) continue;
            var count = mine.Count * (twoSided ? 2 : 1);
            var head = new byte[8];
            BinaryPrimitives.WriteUInt16LittleEndian(head, bl.Type);
            BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(2), (ushort)count);
            BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(4), (uint)(8 + count * bl.Stride));
            ms.Write(head);
            foreach (var f in mine) ms.Write(b, faces[f].At, bl.Stride);
            if (twoSided) foreach (var f in mine) ms.Write(Reversed(b, bl, faces[f].At));
            total += count;
        }
        var delta = (int)ms.Length - end;
        ms.Write(b, end, b.Length - end);
        var outb = ms.ToArray();
        SetI(outb, 64, total);
        foreach (var w in new[] { 76, 84, 92 }) if (I(b, w) >= end) SetI(outb, w, I(b, w) + delta);
        return outb;
    }
}
