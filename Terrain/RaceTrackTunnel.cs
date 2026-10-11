using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// A tunnel over a stretch of the raised road (RaceTrackPlan.Tunnels): Otringal's over the bridge to its square island (the user, 2026-10-08:
// "the red is intended to be a tunnel to drive through"). From and To are the plan's points, Roof its roof's height over the deck.
internal sealed class TunnelRun
{
    public int From { get; set; }
    public int To { get; set; }
    public double Roof { get; set; } = 2800;
    // Pieces: false for a stretch that is under a roof of the island's own -- only the camera kept under it (Otringal's bridge, a tube the
    // road is cut through: RaceTrackDriveThrough).
    public bool Pieces { get; set; } = true;
    // Sunk: the road in a cutting under the ground, the tunnel its roof -- the ground cut down only between its walls, straight down beside
    // them (RaceTrackBuilder.CutUnderRaised leaves the stretch alone), and the roof's top never over Top (the ground's level: a road that
    // climbs under a level roof) and reaching Eave cells past the walls over the cut's edges -- Otringal's climb from the bridge under the
    // gatehouse up into the town (2026-10-08).
    public bool Sunk { get; set; }
    public double? Top { get; set; }
    public double Eave { get; set; }
}

// A place in the lava that shoots balls of fire onto a stretch of the lap (RaceTrackPlan.LavaBalls, RACEMOD.CPP lavaball=): Source in the
// plan's cells (and, a third number, its height: one of Volcano Island's bowls of lava, its brim -- else the ground's there), From and To
// the plan's points, about one every Every ms.
internal sealed class LavaBallRun
{
    public double[] Source { get; set; } = System.Array.Empty<double>();
    public int From { get; set; }
    public int To { get; set; }
    public int Every { get; set; } = 5000;
}

// The tunnel's pieces, every Piece cells along the road, a decor each: a wall just outside each rail, from under the deck up to the roof,
// a light strip along its inside, and the roof over the road from wall to wall -- each a box with its faces outwards (the walls' inner
// faces and the roof's underside are what the car sees from inside). Their boxes are ones nothing can touch (the raised road's rail keeps
// the car between the walls; the roof is over it). The race-track mode keeps the camera's eye under the roof from a little before the
// tunnel's mouth to its end (RACEMOD.CPP tunnel=: report.Tunnels, the raised road's points of its ends).
internal static class RaceTrackTunnel
{
    public const double Piece = 4, WallOut = 0.3, Wall = 280, Slab = 320, Under = 500, StripHigh = 900, Strip = 60;
    private const int NoBoxTop = -32000;
    // colours (unlit: each face its own): the walls' grey, the roof's lavender grey (the town's), the light strips' gold
    public const int WallLight = 58, WallDark = 53, WallTop = 56, RoofLight = 214, RoofDark = 210, RoofTop = 212, StripColour = 101;

    public static List<int> Place(IslandFile island, TrackRoad r, IReadOnlyList<(int From, int To, TunnelRun Run)> tunnels, RaceTrackOptions o, RaceTrackReport report)
    {
        var made = new List<int>();
        if (o.NewBodyBase < 0) { report.Notes.Add("WARNING: no place for the tunnels' bodies was prepared -- no tunnel"); return made; }
        foreach (var (from, to, run) in tunnels)
        {
            var n = r.Count;
            var span = ((to - from) % n + n) % n;
            // (the roof's height over the deck, its least: the camera's eye is kept under it)
            double RoofAt(int i) => run.Top is { } top && r.H[i] + run.Roof + Slab > top ? top - Slab : r.H[i] + run.Roof;
            var least = run.Roof;
            for (var k = 0; k <= span; k++) { var i = (from + k) % n; least = Math.Min(least, RoofAt(i) - r.H[i]); }
            if (!run.Pieces)
            {
                report.Tunnels.Add((from, to, least));
                report.Notes.Add($"tunnel: {span * o.Spacing:0} cells from cell ({r.X[from]:0.0}, {r.Z[from]:0.0}) to ({r.X[to]:0.0}, {r.Z[to]:0.0}) under the island's own roof, {least:0} over the deck");
                continue;
            }
            var cut = run.Sunk ? CutSunk(island, r, from, span, o) : (0, 0.0);
            var step = Math.Max(2, (int)Math.Round(Piece / o.Spacing));
            int pieces = 0, skipped = 0;
            for (var k0 = 0; k0 < span; k0 += step)
            {
                var k1 = Math.Min(span, k0 + step);
                int a = (from + k0) % n, b = (from + k1) % n, mid = (from + (k0 + k1) / 2) % n;
                var origin = new Vector3((float)(r.X[mid] * 512), (float)r.H[mid], (float)(r.Z[mid] * 512));
                var mesh = new List<Vector3>(); var faces = new List<Face>();
                var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue);
                for (var k = k0; k < k1; k++)
                {
                    int i = (from + k) % n, j = (from + k + 1) % n;
                    var p = new Vector3((float)(r.X[i] * 512), (float)r.H[i], (float)(r.Z[i] * 512)) - origin;
                    var q = new Vector3((float)(r.X[j] * 512), (float)r.H[j], (float)(r.Z[j] * 512)) - origin;
                    var along = new Vector3(q.X - p.X, 0, q.Z - p.Z);
                    if (along.Length() < 1e-3) continue;
                    var dir = Vector3.Normalize(along);
                    var across = new Vector3(-dir.Z, 0, dir.X);
                    var centre = (p + q) / 2;
                    var halfAlong = dir * (along.Length() / 2 + 12);   // (a little into the next: no crack)
                    var half = (float)((HalfAt(r, i, o) + WallOut) * 512);
                    var floor = centre.Y;
                    var roof = (float)((RoofAt(i) + RoofAt(j)) / 2 - (r.H[i] + r.H[j]) / 2 + floor);
                    foreach (var side in new[] { -1f, 1f })
                    {
                        // the wall: from under the deck to the roof's top, Wall thick, outside the rail
                        var wc = new Vector3(centre.X, 0, centre.Z) + across * side * (half + (float)Wall / 2);
                        RaceTrackPipes.Box(mesh, faces, wc, halfAlong, across * ((float)Wall / 2), floor - (float)Under, roof + (float)Slab, WallLight, WallDark, WallTop);
                        // its light strip, on its inside
                        var sc = new Vector3(centre.X, 0, centre.Z) + across * side * (half - (float)Strip / 2);
                        RaceTrackPipes.Box(mesh, faces, sc, halfAlong, across * ((float)Strip / 2), floor + (float)StripHigh, floor + (float)StripHigh + 90, StripColour, StripColour, StripColour);
                    }
                    // the roof: from wall to wall
                    var rc = new Vector3(centre.X, 0, centre.Z);
                    var eave = (float)(run.Eave * 512);
                    RaceTrackPipes.Box(mesh, faces, rc, halfAlong, across * (half + (float)Wall + eave), roof, roof + (float)Slab, RoofLight, RoofDark, RoofTop);
                    foreach (var v in new[] { p, q })
                        foreach (var side in new[] { -1f, 1f })
                        {
                            var e = v + across * side * (half + (float)Wall + eave);
                            lo = Vector3.Min(lo, e - new Vector3(0, (float)Under, 0)); hi = Vector3.Max(hi, new Vector3(e.X, roof + (float)Slab, e.Z));
                        }
                }
                if (faces.Count == 0) continue;
                if (IslandDecors.Locate(island, origin.X, origin.Z) is not { } where || where.Cube.Decors.Count >= IslandDecors.MaxPerCube) { skipped++; continue; }
                var (cube, lx, lz) = where;
                var d = IslandDecors.Blank(o.NewBodyBase + report.NewBodies.Count, lx, (int)Math.Round(origin.Y), lz, 0);
                report.NewBodies.Add(RaceTrackPipes.Write(mesh, faces, lit: false));
                d.XMin = (int)Math.Floor(lx + lo.X); d.XMax = (int)Math.Ceiling(lx + hi.X);
                // (a box nothing can touch, its top far under its bottom -- as the raised road's pieces have: a box round the walls and the roof
                // takes in the road between them, and the engine stopped the car dead at the tunnel's mouth; its bottom corners, where the
                // piece is, are what decides whether it is drawn)
                d.YMin = (int)Math.Floor(origin.Y + lo.Y); d.YMax = NoBoxTop;
                d.ZMin = (int)Math.Floor(lz + lo.Z); d.ZMax = (int)Math.Ceiling(lz + hi.Z);
                cube.Decors.Add(d);
                made.Add(d.Body);
                pieces++;
            }
            report.Tunnels.Add((from, to, least));
            report.Notes.Add($"tunnel: {span * o.Spacing:0} cells from cell ({r.X[from]:0.0}, {r.Z[from]:0.0}) to ({r.X[to]:0.0}, {r.Z[to]:0.0}), its roof {run.Roof:0} over the deck" +
                             (run.Top is { } t ? $" (its top no higher than {t:0}: {least:0} over the deck at the least)" : "") + $", {pieces} pieces{(skipped > 0 ? $" ({skipped} left out: a cube full)" : "")}" +
                             (run.Sunk ? $"; sunk: the ground cut down between its walls at {cut.Item1} vertices, {cut.Item2:0} at the most" : ""));
        }
        return made;
    }

    // A sunk tunnel's cutting: the ground between its walls (out to their outer faces) brought down to RaisedUnder under the deck, and
    // nothing beyond them -- the ground there stays the town's, its edge straight down beside the walls, under the roof's eaves.
    public const double RaisedUnder = 450;
    private static (int, double) CutSunk(IslandFile island, TrackRoad r, int from, int span, RaceTrackOptions o)
    {
        var n = r.Count;
        int count = 0; double deepest = 0;
        var lowest = new Dictionary<(int, int), double>();
        for (var k = 0; k < span; k++)
        {
            int a = (from + k) % n, b = (from + k + 1) % n;
            double sx = r.X[b] - r.X[a], sz = r.Z[b] - r.Z[a], len2 = sx * sx + sz * sz;
            if (len2 < 1e-12) continue;
            var reach = HalfAt(r, a, o) + WallOut + Wall / 512 + 0.05;
            for (var gz = (int)Math.Floor(Math.Min(r.Z[a], r.Z[b]) - reach); gz <= (int)Math.Ceiling(Math.Max(r.Z[a], r.Z[b]) + reach); gz++)
            for (var gx = (int)Math.Floor(Math.Min(r.X[a], r.X[b]) - reach); gx <= (int)Math.Ceiling(Math.Max(r.X[a], r.X[b]) + reach); gx++)
            {
                var t = ((gx - r.X[a]) * sx + (gz - r.Z[a]) * sz) / len2;
                if (t < -0.01 && k > 0 || t > 1.01 && k < span - 1) continue;
                t = Math.Clamp(t, 0, 1);
                var d = Math.Sqrt((gx - r.X[a] - sx * t) * (gx - r.X[a] - sx * t) + (gz - r.Z[a] - sz * t) * (gz - r.Z[a] - sz * t));
                if (d > reach) continue;
                var to = r.H[a] + (r.H[b] - r.H[a]) * t - RaisedUnder;
                lowest[(gx, gz)] = lowest.TryGetValue((gx, gz), out var was) ? Math.Min(was, to) : to;
            }
        }
        foreach (var ((gx, gz), to) in lowest)
        {
            if (island.HeightAt(gx, gz) is not { } h || h <= to) continue;
            deepest = Math.Max(deepest, h - to);
            island.SetHeight(gx, gz, Math.Max(0, (int)Math.Floor(to)));
            count++;
        }
        return (count, deepest);
    }

    private static double HalfAt(TrackRoad r, int i, RaceTrackOptions o) =>
        r.Raised is { } up && up[i] ? r.RaisedHalfs is { } halfs && i < halfs.Length ? halfs[i] : o.RaisedHalfWidth : r.CurbHalf;
}
