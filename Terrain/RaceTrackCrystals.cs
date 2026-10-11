using System.Buffers.Binary;
using System.IO;
using System.Numerics;

namespace LBAAssembler.Terrain;

// Crystals by the road (RaceTrackPlan.Crystals): the island's own red crystal clusters made big -- Volcano Island's (its OBL bodies 1 and 2,
// on its rocks; the user, 2026-10-10: "The island heavily features red crystal scenary, perhaps we could make this a feature too"): a gate
// either side of the way into the canyon, a field standing in the lava lowland, a garden inside the plateau's loop. Each a new island body: the cluster Scale times its size, leaning Lean degrees the way it is turned (Turn: its body's z axis
// turned that far from the world's, as the engine turns a decor), standing in the ground (Sink under it) where At says. One that would come
// within the road's rails between 1,000 under its deck (or a jump's flight) and 2,000 over it is turned another way (an eighth of a turn at a
// time, the nearest first: a cluster reaches far out on one side), then made smaller, till it doesn't (or left out). Their boxes touch
// nothing.
internal sealed class CrystalRun
{
    public double[] At { get; set; } = Array.Empty<double>();
    public int Body { get; set; } = 1;
    public double Scale { get; set; } = 3;
    public double Turn { get; set; }
    public double Lean { get; set; }
    public double Sink { get; set; } = 150;
}

// A crystal arch over the road (RaceTrackPlan.CrystalArches): where the lap passes nearest At, a half ring of crystals standing across it --
// its legs Spread cells outside the rails, down into the ground either side -- crystal spikes all round it pointing out. Its own body
// (RaceTrackCrystals.Arch), the clusters' dark red (the island palette's ramp 4, points ramp 5), shaded by the way each face looks. (The clusters themselves
// leant in over the road reached nowhere near across it: they are as wide as they are tall, and the east ridge has no room outside its
// east rail before the island's edge for one to lean from.)
internal sealed class CrystalArchRun
{
    public double[] At { get; set; } = Array.Empty<double>();
    public double Spread { get; set; } = 3;
}

internal static class RaceTrackCrystals
{
    private const int NoBoxTop = -32000;
    private const double Clear = 2000, Under = 1000, RailPad = 0.4, Shrink = 0.85, Least = 1.2;
    private static readonly int[] Turns = { 0, 45, -45, 90, -90, 135, -135, 180 };

    public static void Place(IslandFile island, TrackRoad r, IEnumerable<CrystalRun> runs, int originX, int originZ, RaceTrackOptions o, RaceTrackReport report)
    {
        if (o.SceneryObl is not { } oblPath || !File.Exists(oblPath) || o.NewBodyBase < 0) { report.Notes.Add("WARNING: crystals: no island objects to make them of"); return; }
        var obl = HqrArchive.Open(oblPath);
        int placed = 0, shrunk = 0, left = 0;
        foreach (var c in runs.Where(c => c.At is { Length: >= 2 }))
        {
            double wx = c.At[0] + originX, wz = c.At[1] + originZ;
            if (IslandDecors.Locate(island, wx * 512, wz * 512) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCube) { left++; continue; }
            var source = obl.Read(c.Body);
            var baseY = (IslandOps.Altitude(island, wx * 512, wz * 512) ?? 0) - c.Sink;
            int Beta(double turn) => ((int)Math.Round(turn * 4096 / 360) % 4096 + 4096) % 4096;
            var beta = Beta(c.Turn);
            var turned = c.Turn;
            var scale = c.Scale;
            byte[]? body = null;
            while (scale >= Least && body is null)
            {
                var made = Transformed(source, scale, c.Lean * Math.PI / 180);
                if (made is null) break;
                var pts0 = Points(made);
                foreach (var t in Turns)
                    if (Clears(r, o, pts0, wx, wz, baseY, Beta(c.Turn + t))) { body = made; beta = Beta(c.Turn + t); turned = c.Turn + t; break; }
                if (body is null) scale *= Shrink;
            }
            if (body is null) { left++; report.Notes.Add($"WARNING: the crystal at ({wx:0.#}, {wz:0.#}) left out: too near the road at any size"); continue; }
            if (scale < c.Scale - 1e-9) shrunk++;
            // (its box over its footprint, touching nothing)
            var pts = Points(body).Select(p => Turned(p, beta)).ToList();
            var d = IslandDecors.Blank(o.NewBodyBase + report.NewBodies.Count, at.X, (int)Math.Round(baseY), at.Z, beta);
            d.XMin = at.X + (int)Math.Floor(pts.Min(p => p.X)); d.XMax = at.X + (int)Math.Ceiling(pts.Max(p => p.X));
            d.ZMin = at.Z + (int)Math.Floor(pts.Min(p => p.Z)); d.ZMax = at.Z + (int)Math.Ceiling(pts.Max(p => p.Z));
            d.YMin = (int)Math.Round(baseY); d.YMax = NoBoxTop;
            at.Cube.Decors.Add(d);
            report.NewBodies.Add(body);
            placed++;
            report.Notes.Add($"  a crystal (body {c.Body}) at ({wx:0.#}, {wz:0.#}), {scale:0.##} times its size{(scale < c.Scale - 1e-9 ? $" (of {c.Scale:0.##})" : "")}, " +
                             $"turned {turned:0}{(turned != c.Turn ? $" (of {c.Turn:0})" : "")}, leaning {c.Lean:0}, its foot at {baseY:0}, {pts.Max(p => p.Y):0} tall");
        }
        report.Notes.Add($"{placed} crystals by the road{(shrunk > 0 ? $", {shrunk} made smaller to clear it" : "")}{(left > 0 ? $", {left} left out" : "")}");
    }

    public static void PlaceArches(IslandFile island, TrackRoad r, IEnumerable<CrystalArchRun> runs, int originX, int originZ, RaceTrackOptions o, RaceTrackReport report)
    {
        if (o.NewBodyBase < 0) { report.Notes.Add("WARNING: crystal arches: no place for new bodies"); return; }
        foreach (var run in runs.Where(a => a.At is { Length: >= 2 }))
        {
            double x = run.At[0] + originX, z = run.At[1] + originZ;
            var k = Enumerable.Range(0, r.Count).Where(i => !r.Gap[i]).OrderBy(i => (r.X[i] - x) * (r.X[i] - x) + (r.Z[i] - z) * (r.Z[i] - z)).First();
            var half = (r.RaisedHalfs?[k] ?? o.RaisedHalfWidth) + run.Spread;
            double tx = r.Tx[k], tz = r.Tz[k], deck = r.H[k];
            var beta = ((int)Math.Round(Math.Atan2(tx, tz) * 4096 / (2 * Math.PI)) % 4096 + 4096) % 4096;
            // (each leg down into the ground under it: the body's +x turned by the decor's beta is (cos, -sin) of it -- the road's -Across)
            double Leg(int side)
            {
                double lx = r.X[k] + tz * half * side, lz = r.Z[k] - tx * half * side;
                return deck - (IslandOps.Altitude(island, lx * 512, lz * 512) ?? deck - 400) + 300;
            }
            if (IslandDecors.Locate(island, r.X[k] * 512, r.Z[k] * 512) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCube)
            { report.Notes.Add($"WARNING: the crystal arch at ({x:0.#}, {z:0.#}): no room"); continue; }
            var (body, reach) = Arch(half * 512, Leg(-1), Leg(1), k * 7919);
            var d = IslandDecors.Blank(o.NewBodyBase + report.NewBodies.Count, at.X, (int)Math.Round(deck), at.Z, beta);
            var ext = (int)Math.Ceiling(reach);
            d.XMin = at.X - ext; d.XMax = at.X + ext; d.ZMin = at.Z - ext; d.ZMax = at.Z + ext; d.YMin = (int)Math.Round(deck); d.YMax = NoBoxTop;
            at.Cube.Decors.Add(d);
            report.NewBodies.Add(body);
            report.Notes.Add($"a crystal arch over the road at ({r.X[k]:0.0}, {r.Z[k]:0.0}), its legs {half:0.0} cells either side ({Leg(-1):0} and {Leg(1):0} down), its band {half * 512 - 330:0} over the deck at its top");
        }
    }

    // The arch: a half ring of radius `radius` (units) round the body's origin on the deck, across the road (the body's x axis; z along the
    // road), its band a chain of crystal prisms, a crystal spike out from it every so often, its legs straight down `legLeft` and
    // `legRight` from its ends (its -x end and its +x end). Returns the body and how far it reaches from its origin.
    public static (byte[] Body, double Reach) Arch(double radius, double legLeft, double legRight, int seed)
    {
        var m = new RaceTrackRaisedBody.Mesh();
        var rnd = new Random(seed);
        var light = Vector3.Normalize(new Vector3(0.35f, 0.85f, 0.4f));
        // (the island's ramp 4 for the crystals' dark red, its ramp 5 brighter for their points -- through the island's colour tables, as the
        // game draws its objects, these read as the clusters' own; ramp 2, the palette's purple, came out cream)
        int Shade(Vector3 n, int bright, int ramp = 4) => ramp * 16 + Math.Clamp(3 + bright + (int)Math.Round(9 * Math.Max(0, Vector3.Dot(Vector3.Normalize(n), light))), 2, 15);
        double reach = radius;
        // a crystal: a hexagonal prism from `a` to `b`, `r` round, a point of `tip` beyond b -- its foot open (in the band, the ground or the
        // leg it grows from: a body has 550 points and 550 faces at most, Body.Limit; the arch has 35 crystals, 13 points and 12 faces each)
        void Crystal(Vector3 a, Vector3 b, float r, float tip, int bright)
        {
            var axis = Vector3.Normalize(b - a);
            var side = Vector3.Normalize(Vector3.Cross(axis, Math.Abs(axis.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
            var up = Vector3.Cross(side, axis);
            var ring0 = new int[6]; var ring1 = new int[6]; var mids = new Vector3[6];
            for (var i = 0; i < 6; i++)
            {
                var ang = i * MathF.PI / 3;
                var off = side * MathF.Cos(ang) * r + up * MathF.Sin(ang) * r;
                ring0[i] = m.P(a + off); ring1[i] = m.P(b + off); mids[i] = off;
            }
            var point = m.P(b + axis * tip);
            for (var i = 0; i < 6; i++)
            {
                var j = (i + 1) % 6;
                var outward = mids[i] + mids[j];
                m.Quad(ring0[i], ring0[j], ring1[j], ring1[i], Shade(outward, bright), outward);
                m.Tri(ring1[i], ring1[j], point, Shade(outward + axis * r, bright, 5), outward + axis * r);
            }
            reach = Math.Max(reach, Math.Max(new Vector2(b.X, b.Z).Length(), new Vector2(a.X, a.Z).Length()) + tip);
        }
        Vector3 On(double ang, double rr, double along = 0) => new((float)(rr * Math.Cos(ang)), (float)(rr * Math.Sin(ang)), (float)along);
        // the band: prisms end to end round the half ring
        const int Links = 10;
        for (var i = 0; i < Links; i++)
        {
            double a0 = Math.PI * i / Links, a1 = Math.PI * (i + 1) / Links;
            Crystal(On(a0 - 0.03, radius), On(a1 + 0.03, radius), 330, 0.1f, 0);
        }
        // the spikes: out from the band, leaning along the road a little either way, longer at the crown
        for (var i = 0; i <= 12; i++)
        {
            var ang = Math.PI * i / 12 + (rnd.NextDouble() - 0.5) * 0.12;
            var len = 900 + 1300 * Math.Sin(ang) + rnd.NextDouble() * 500;
            var lean = (rnd.NextDouble() - 0.5) * 0.7;
            var a = On(ang, radius - 150, 0);
            var dir = Vector3.Normalize(new Vector3((float)Math.Cos(ang), (float)Math.Sin(ang), (float)lean));
            Crystal(a, a + dir * (float)len, (float)(220 + rnd.NextDouble() * 120), 380, i % 3 == 0 ? 1 : 0);
            if (i % 3 == 1)
            {
                var dir2 = Vector3.Normalize(new Vector3((float)Math.Cos(ang + 0.15), (float)Math.Sin(ang + 0.15), (float)-lean * 1.5f));
                Crystal(a, a + dir2 * (float)(len * 0.6), 170, 260, -1);
            }
        }
        // the legs: a column of crystals from each end down into the ground, a cluster at its foot
        foreach (var (sx, leg) in new[] { (-1.0, legLeft), (1.0, legRight) })
        {
            var top = new Vector3((float)(sx * radius), 0, 0);
            var foot = new Vector3((float)(sx * radius), (float)-leg, 0);
            Crystal(foot, top, 420, 0.1f, -1);
            for (var c = 0; c < 3; c++)
            {
                var ang = c * 2 * Math.PI / 3 + 0.4;
                var dir = Vector3.Normalize(new Vector3((float)(sx * 0.5 + 0.35 * Math.Cos(ang)), 1, (float)(0.45 * Math.Sin(ang))));
                var baseAt = foot + new Vector3(0, (float)Math.Min(leg, 600), 0);
                Crystal(baseAt, baseAt + dir * (float)(900 + rnd.NextDouble() * 700), 230, 320, 0);
            }
        }
        return (m.Write(), reach);
    }

    // Whether a body's points, turned and standing at (wx, wz) (cells) on baseY, keep off the road: none within its rails (and RailPad) under
    // Clear over its deck there.
    private static bool Clears(TrackRoad r, RaceTrackOptions o, List<(double X, double Y, double Z)> points, double wx, double wz, double baseY, int beta)
    {
        // (only the road's points near: within the body's reach)
        var reach = points.Max(p => Math.Sqrt(p.X * p.X + p.Z * p.Z)) / 512 + o.RaisedHalfWidth + 4;
        var near = Enumerable.Range(0, r.Count).Where(k => Math.Abs(r.X[k] - wx) < reach && Math.Abs(r.Z[k] - wz) < reach).ToList();
        if (near.Count == 0) return true;
        foreach (var p in points)
        {
            var t = Turned(p, beta);
            double px = wx + t.X / 512, pz = wz + t.Z / 512, py = baseY + t.Y;
            foreach (var k in near)
            {
                var half = (r.RaisedHalfs?[k] ?? o.RaisedHalfWidth) + RailPad;
                var dx = px - r.X[k]; var dz = pz - r.Z[k];
                if (dx * dx + dz * dz > (half + 0.5) * (half + 0.5)) continue;
                // (across the road at that point, not along it: within its width)
                var across = Math.Abs(-dx * r.Tz[k] + dz * r.Tx[k]);
                if (across <= half && py < r.H[k] + Clear && py > r.H[k] - Under) return false;
            }
        }
        return true;
    }

    // A body point turned as the engine turns a decor (beta: its z axis towards the world's (sin, cos)).
    private static (double X, double Y, double Z) Turned((double X, double Y, double Z) p, int beta)
    {
        var a = beta * 2 * Math.PI / 4096;
        return (p.X * Math.Cos(a) + p.Z * Math.Sin(a), p.Y, -p.X * Math.Sin(a) + p.Z * Math.Cos(a));
    }

    private static List<(double X, double Y, double Z)> Points(byte[] b)
    {
        int I(int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
        short S(int at) => BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at));
        var list = new List<(double, double, double)>();
        int n = I(40), p = I(44);
        for (var i = 0; i < n; i++, p += 8) list.Add((S(p), S(p + 2), S(p + 4)));
        return list;
    }

    // An island body `k` times its size, leaning `lean` radians towards its +z (its points and its normals turned about its x axis): a
    // one-bone body, its points in its own frame. Its box made again round its points. Null: not one this can do.
    private static byte[]? Transformed(byte[] body, double k, double lean)
    {
        var b = (byte[])body.Clone();
        int I(int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
        short S(int at) => BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at));
        void W(int at, double v) => BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(at), (short)Math.Clamp(Math.Round(v), short.MinValue, short.MaxValue));
        if (b.Length < 96 || I(32) != 1) return null;
        double c = Math.Cos(lean), s = Math.Sin(lean);
        int points = I(40), p = I(44);
        if (p < 0 || p + points * 8 > b.Length) return null;
        double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue, z0 = double.MaxValue, z1 = double.MinValue;
        for (var i = 0; i < points; i++, p += 8)
        {
            double x = S(p), y = S(p + 2), z = S(p + 4);
            double ny = y * c - z * s, nz = z * c + y * s;
            x *= k; ny *= k; nz *= k;
            W(p, x); W(p + 2, ny); W(p + 4, nz);
            x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, ny); y1 = Math.Max(y1, ny); z0 = Math.Min(z0, nz); z1 = Math.Max(z1, nz);
        }
        // (its points' normals and its faces' -- T_BODY_HEADER's NbNormales and NbNormFaces -- turned with it)
        foreach (var (count, offset) in new[] { (I(48), I(52)), (I(56), I(60)) })
            if (count > 0 && offset > 0 && offset + count * 8 <= b.Length)
                for (int i = 0, q = offset; i < count; i++, q += 8)
                {
                    double y = S(q + 2), z = S(q + 4);
                    W(q + 2, y * c - z * s); W(q + 4, z * c + y * s);
                }
        int spheres = I(80), sp = I(84);
        if (spheres > 0 && sp > 0 && sp + spheres * 8 <= b.Length)
            for (var i = 0; i < spheres; i++, sp += 8)
                BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(sp + 6), (ushort)Math.Min(65535, Math.Round(BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(sp + 6)) * k)));
        var box = new[] { x0, x1, y0, y1, z0, z1 };
        for (var i = 0; i < 6; i++) BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(8 + i * 4), (int)Math.Round(box[i]));
        return b;
    }
}
