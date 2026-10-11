using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// Pipes along a stretch of the road (RaceTrackPlan.Pipes): the Island of the Francos' refinery, the user's "lots of pipe and steam, and
// perhaps some oil dripping in places" (2026-10-07). From its first point to its last, every Every cells, a gantry over the road -- a
// pipe standing on the ground each side of it, up past the road, joined over it by a pipe as high as a car and more, and a pipe either
// side along the road to the next -- the race-track mode puffing steam from the tops of its pipes (steam=) and, with Drip, oil dripping
// from the pipe over the road onto it, where it lies as a slick a car skids on (drip=).
internal sealed class PipeRun
{
    public int From { get; set; }
    public int To { get; set; }
    public double Every { get; set; } = 10;
    public bool Drip { get; set; } = true;
    // (every so many gantries drip: 2 every other one, 1 every one)
    public int DripsEvery { get; set; } = 2;
}

// Steam jets along a stretch of the road (RaceTrackPlan.SteamJets): out of the gantries' uprights there (RaceTrackPipes.Place: a nozzle on
// each, High over the deck), blowing level across the road, Reach cells into it from its rail (the race-track mode blows it out of the
// left upright on the player's odd laps, the right on his even ones: RACEMOD.CPP jet=), On ms in every On + Off, each a little after the
// one before (Wave ms): a burst runs down the road ahead of a car. (Every: how far apart where the stretch has no gantries -- out of the
// rail there, near the deck, as the round before. The user, 2026-10-07: "The steam looks like it's coming out of the track, let's have
// it coming out of the vertical pipes on the track fully horizontally". Before that, up out of the deck.)
internal sealed class SteamJetRun
{
    public int From { get; set; }
    public int To { get; set; }
    public double Every { get; set; } = 6;
    public int On { get; set; } = 1100;
    public int Off { get; set; } = 2400;
    public int Wave { get; set; } = 450;
    public double Reach { get; set; } = 5.5;
    public int High { get; set; } = 700;
}

// A pipeline over the lap (RaceTrackPlan.Pipeline): a big pipe through Points ([x, z, height]: island cells from the plan's origin, world
// units up) on supports standing on the ground clear of the road, dripping oil where it passes over the road, every DripEvery cells
// along it there, each drip on its own time (the race-track mode's drip=: a slick where it lands) -- the Island of the Francos' pipe from
// the Gazogem factory to the air-boat (the user, 2026-10-07: "a massive pipe above the track coming out of the gazogem factory to the
// air ship ... random oil drips from the pipe at fixed intervals where it's over the track").
internal sealed class PipeLine
{
    public double[][] Points { get; set; } = Array.Empty<double[]>();
    public double Radius { get; set; } = 700;
    public double SupportEvery { get; set; } = 12;
    public double DripEvery { get; set; } = 2.5;
    public int DripMs { get; set; } = 4500;
}

internal static class RaceTrackPipes
{
    // the pipes' sizes (world units): the uprights' and the cross pipe's radius, the side pipes', how far out the uprights stand past the
    // road's rails (cells), the cross pipe's height over the deck, the uprights' over it, the side pipes' over it
    public const double Upright = 220, Cross = 200, Side = 130, Out = 1.3, CrossHigh = 2200, Top = 2900, SideHigh = 350;
    // a steam jet's nozzle on an upright: how far it stands out of the pipe towards the road, its radius, its mouth's (world units)
    public const double Nozzle = 200, NozzleRadius = 95, NozzleMouth = 140;
    // colours: the shared ramps' starts (greys, reds), lit
    public const int Grey = 48, Red = 64;
    public const int SteamEvery = 900, DripEvery = 5000;
    // how far to one side of the road's middle the oil drips, as a share of the way to its rail
    public const double DripAcross = 0.45;

    // Places the gantries; `runs` with their points the road's -- and the steam jets of `jets` out of those of them in their stretches
    // (a jet stretch with none there: out of the rail, PlaceJets). Returns the bodies made.
    public static List<int> Place(IslandFile island, TrackRoad r, IReadOnlyList<(int From, int To, PipeRun Run)> runs,
        IReadOnlyList<(int From, int To, SteamJetRun Run)> jets, RaceTrackOptions o, RaceTrackReport report)
    {
        var made = new List<int>();
        if (o.NewBodyBase < 0) { report.Notes.Add("WARNING: no place for the pipes' bodies was prepared -- no pipes"); return made; }
        int gantries = 0, skipped = 0, blowing = 0;
        var why = new List<string>();
        var nRoad = r.Count;
        // the jet stretch a point is in (its index in `jets`, -1 none); how many of its gantries came before (the bursts' wave)
        int JetRunAt(int i)
        {
            for (var q = 0; q < jets.Count; q++)
            {
                var (f, t, _) = jets[q];
                if (((i - f) % nRoad + nRoad) % nRoad <= ((t - f) % nRoad + nRoad) % nRoad) return q;
            }
            return -1;
        }
        var jetsSoFar = new int[jets.Count];
        foreach (var (from, to, run) in runs)
        {
            var n = r.Count;
            var span = ((to - from) % n + n) % n;
            var step = Math.Max(2, (int)Math.Round(run.Every / o.Spacing));
            var placed = 0;
            for (var k = step / 2; k <= span - step / 2; k += step)
            {
                var i = (from + k) % n;
                double x = r.X[i], z = r.Z[i], y = r.H[i];
                var a = (i + n - 1) % n; var b = (i + 1) % n;
                var along = new Vector3((float)(r.X[b] - r.X[a]), 0, (float)(r.Z[b] - r.Z[a]));
                if (along.Length() < 1e-6) continue;
                along = Vector3.Normalize(along);
                var across = new Vector3(-along.Z, 0, along.X);
                var half = (r.Raised is { } up && up[i] ? r.RaisedHalfs is { } halfs && i < halfs.Length ? halfs[i] : o.RaisedHalfWidth : r.CurbHalf) + Out;
                // the uprights' feet on the ground under them; none where another part of the lap -- its deck, its rails and a little more --
                // is in an upright's way up, or passes over the road in the cross pipe's (a road under the deck between the uprights is clear)
                var feet = new double[2];
                for (var s = 0; s < 2; s++)
                    feet[s] = IslandOps.Altitude(island, (x + across.X * (s == 0 ? -1 : 1) * half) * 512, (z + across.Z * (s == 0 ? -1 : 1) * half) * 512) ?? 0;
                var blocked = false;
                for (var j = 0; j < n && !blocked; j++)
                {
                    var sep = Math.Min(Math.Abs(j - i), n - Math.Abs(j - i));
                    if (sep < 30 || r.H[j] > y + Top + 1400) continue;
                    var other = (r.Raised is { } on && on[j] ? r.RaisedHalfs is { } wide && j < wide.Length ? wide[j] : o.RaisedHalfWidth : r.CurbHalf) + 0.6;
                    double dx = r.X[j] - x, dz = r.Z[j] - z;
                    var a0 = dx * across.X + dz * across.Z;
                    for (var s = 0; s < 2 && !blocked; s++)
                    {
                        var sign = s == 0 ? -1 : 1;
                        double ex = dx - across.X * sign * half, ez = dz - across.Z * sign * half;
                        if (Math.Sqrt(ex * ex + ez * ez) < other && r.H[j] > feet[s] - 400) blocked = true;
                    }
                    var off = Math.Abs(dx * along.X + dz * along.Z);
                    if (Math.Abs(a0) <= half && off < other && r.H[j] > y - 300) blocked = true;
                }
                if (blocked || feet.Any(f => f > y + 600)) { skipped++; why.Add($"({x:0.#}, {z:0.#}): {(blocked ? "the lap" : "the ground")}"); continue; }
                var len = (float)(run.Every * 512);
                var jetRun = JetRunAt(i);
                var body = Gantry(along, across, half * 512, feet[0] - y, feet[1] - y, len, jetRun >= 0 ? jets[jetRun].Run.High : -1);
                if (IslandDecors.Locate(island, x * 512, z * 512) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCube) { skipped++; why.Add($"({x:0.#}, {z:0.#}): its cube full"); continue; }
                var (cube, lx, lz) = at;
                var d = IslandDecors.Blank(o.NewBodyBase + report.NewBodies.Count, lx, (int)Math.Round(y), lz, 0);
                report.NewBodies.Add(body);
                // (its box the cross pipe's only, over the road: the uprights and the side pipes stand outside the rails, where no car goes)
                var reach = half * 512 + Upright;
                int bx0 = (int)Math.Round(lx - Math.Abs(across.X) * reach - Cross * 2), bx1 = (int)Math.Round(lx + Math.Abs(across.X) * reach + Cross * 2);
                int bz0 = (int)Math.Round(lz - Math.Abs(across.Z) * reach - Cross * 2), bz1 = (int)Math.Round(lz + Math.Abs(across.Z) * reach + Cross * 2);
                d.XMin = bx0; d.XMax = bx1; d.ZMin = bz0; d.ZMax = bz1;
                d.YMin = (int)Math.Round(y + CrossHigh - Cross); d.YMax = (int)Math.Round(y + Top);
                cube.Decors.Add(d);
                made.Add(d.Body);
                gantries++;
                // steam from the uprights' tops; oil dripping from the cross pipe onto the road's middle
                for (var s = 0; s < 2; s++)
                {
                    var sign = s == 0 ? -1 : 1;
                    report.Steam.Add(new[] { (int)Math.Round((x + across.X * sign * half) * 512), (int)Math.Round(y + Top + 80), (int)Math.Round((z + across.Z * sign * half) * 512), SteamEvery });
                }
                // (in places: every other gantry of the stretch drips -- or every one, DripsEvery -- onto one side of the road and then the
                // other: a car can keep clear)
                if (run.Drip && placed++ % Math.Max(1, run.DripsEvery) == 0)
                {
                    var side = (report.Drips.Count % 2 == 0 ? 1 : -1) * (half - Out) * DripAcross;
                    report.Drips.Add(new[] { (int)Math.Round((x + across.X * side) * 512), (int)Math.Round(y + CrossHigh - Cross - 40),
                        (int)Math.Round((z + across.Z * side) * 512), (int)Math.Round(y), DripEvery });
                }
                // the steam jet out of its uprights' nozzles: from the nozzle's mouth across the road, into it Reach from its rail
                if (jetRun >= 0)
                {
                    var jr = jets[jetRun].Run;
                    var road = half - Out;
                    var mouth = half * 512 - Upright - Nozzle;
                    var blows = mouth - (road - Math.Min(jr.Reach, 2 * road - 0.5)) * 512;
                    // (each a Wave before the one before it: the burst runs back up the road at a car. Each a Wave after it, the burst ran
                    // ahead of the car at its own speed -- a gantry's 6 cells in 450 ms, a Gazogem-fuelled car's in 420 -- and a car that came
                    // between two bursts was never met by one)
                    var nth = jetsSoFar[jetRun]++;
                    report.Jets.Add(new[] { (int)Math.Round(x * 512), (int)Math.Round(y), (int)Math.Round(z * 512), (int)Math.Round(mouth), (int)Math.Round(blows),
                        jr.On, jr.Off, nth * jr.Wave % (jr.On + jr.Off),
                        (int)Math.Round(along.X * 1000), (int)Math.Round(along.Z * 1000), jr.High });
                    blowing++;
                }
            }
        }
        // (a jet stretch with no gantry in it: its jets out of the rail, as before there were gantries to blow them)
        var bare = Enumerable.Range(0, jets.Count).Where(q => jetsSoFar[q] == 0).Select(q => jets[q]).ToList();
        if (bare.Count > 0) PlaceJets(r, bare, o, report);
        if (blowing > 0) report.Notes.Add($"steam jets: {blowing} out of the gantries' uprights, blowing level across the road -- " +
                                          "the left upright on the player's odd laps, the right on his even ones");
        report.Notes.Add($"pipes: {gantries} gantries over the road ({skipped} left out: another part of the lap in an upright's way, or the ground over the road), " +
                         $"{report.Steam.Count} steam vents, {report.Drips.Count} oil drips{(why.Count > 0 ? " -- left out: " + string.Join(", ", why) : "")}");
        return made;
    }

    // Places the steam jets; `runs` with their points the road's.
    public static void PlaceJets(TrackRoad r, IReadOnlyList<(int From, int To, SteamJetRun Run)> runs, RaceTrackOptions o, RaceTrackReport report)
    {
        var n = r.Count;
        var count = 0;
        foreach (var (from, to, run) in runs)
        {
            var span = ((to - from) % n + n) % n;
            var step = Math.Max(2, (int)Math.Round(run.Every / o.Spacing));
            var k = 0;
            for (var at = step / 2; at <= span; at += step, k++)
            {
                var i = (from + at) % n;
                int a = (i + n - 1) % n, b = (i + 1) % n;
                double tx = r.X[b] - r.X[a], tz = r.Z[b] - r.Z[a];
                var len = Math.Sqrt(tx * tx + tz * tz);
                if (len < 1e-6) continue;
                tx /= len; tz /= len;
                var half = HalfAt(r, i, o);
                // [x y z (the road's middle), its half width, the reach across, on, off, phase, its way (a thousand long)]
                report.Jets.Add(new[] { (int)Math.Round(r.X[i] * 512), (int)Math.Round(r.H[i]), (int)Math.Round(r.Z[i] * 512), (int)Math.Round(half * 512),
                    (int)Math.Round(Math.Min(run.Reach, 2 * half - 0.5) * 512), run.On, run.Off, ((-k * run.Wave) % (run.On + run.Off) + run.On + run.Off) % (run.On + run.Off),
                    (int)Math.Round(tx * 1000), (int)Math.Round(tz * 1000) });
                count++;
            }
        }
        report.Notes.Add($"steam jets: {count} blowing across the road from its rail -- the left on the player's odd laps, the right on his even ones (the Gazogem factory's steam, hitting a car in a burst)");
    }

    private static double HalfAt(TrackRoad r, int i, RaceTrackOptions o) =>
        r.Raised is { } up && up[i] ? r.RaisedHalfs is { } halfs && i < halfs.Length ? halfs[i] : o.RaisedHalfWidth : r.CurbHalf;

    // The pipeline: its pipe in pieces of PipePiece cells at most (a decor each, in the cube its middle is in; its box the pipe's), its
    // supports, and its drips over the road. Returns the bodies made.
    public const double PipePiece = 6;
    public const int PipeColour = 144, FlangeColour = 64, SupportLight = 57, SupportDark = 54, SupportTop = 55;
    public static List<int> PlacePipeline(IslandFile island, TrackRoad r, PipeLine line, double originX, double originZ, RaceTrackOptions o, RaceTrackReport report)
    {
        var made = new List<int>();
        if (o.NewBodyBase < 0 || line.Points.Length < 2) return made;
        var pts = line.Points.Select(p => new Vector3((float)((p[0] + originX) * 512), (float)p[2], (float)((p[1] + originZ) * 512))).ToList();
        var radius = (float)line.Radius;
        int pieces = 0, supports = 0, skipped = 0;
        bool Add(byte[] body, Vector3 at, Vector3 lo, Vector3 hi)
        {
            if (IslandDecors.Locate(island, at.X, at.Z) is not { } where || where.Cube.Decors.Count >= IslandDecors.MaxPerCube) return false;
            var (cube, lx, lz) = where;
            var d = IslandDecors.Blank(o.NewBodyBase + report.NewBodies.Count, lx, (int)Math.Round(at.Y), lz, 0);
            report.NewBodies.Add(body);
            d.XMin = (int)Math.Floor(lx + lo.X); d.XMax = (int)Math.Ceiling(lx + hi.X);
            d.YMin = (int)Math.Floor(at.Y + lo.Y); d.YMax = (int)Math.Ceiling(at.Y + hi.Y);
            d.ZMin = (int)Math.Floor(lz + lo.Z); d.ZMax = (int)Math.Ceiling(lz + hi.Z);
            cube.Decors.Add(d);
            made.Add(d.Body);
            return true;
        }
        // the pipe
        for (var k = 0; k + 1 < pts.Count; k++)
        {
            var a = pts[k]; var b = pts[k + 1];
            var cells = Math.Sqrt(Math.Pow((b.X - a.X) / 512, 2) + Math.Pow((b.Z - a.Z) / 512, 2) + Math.Pow((b.Y - a.Y) / 512, 2));
            var n = Math.Max(1, (int)Math.Ceiling(cells / PipePiece));
            for (var c = 0; c < n; c++)
            {
                var p0 = Vector3.Lerp(a, b, (float)c / n); var p1 = Vector3.Lerp(a, b, (float)(c + 1) / n);
                var mid = (p0 + p1) / 2;
                var mesh = new List<Vector3>(); var faces = new List<Face>();
                var axis = Vector3.Normalize(p1 - p0);
                // (each piece a little into the next: no crack between them; its flange at its start, a wider one at a bend)
                Cylinder(mesh, faces, p0 - mid - axis * 40, p1 - mid + axis * 40, radius, PipeColour, 12);
                var flange = c == 0 && k > 0 ? 1.3f : 1.18f;
                Cylinder(mesh, faces, p0 - mid - axis * 110, p0 - mid + axis * 110, radius * flange, FlangeColour, 12);
                var body = Write(mesh, faces, lit: true);
                var lo = Vector3.Min(p0, p1) - mid - new Vector3(radius); var hi = Vector3.Max(p0, p1) - mid + new Vector3(radius);
                if (Add(body, mid, lo, hi)) pieces++; else skipped++;
            }
        }
        // the supports: every SupportEvery cells along it, from the ground up into the pipe, none where the road passes through its way up
        var length = 0.0;
        for (var k = 0; k + 1 < pts.Count; k++) length += Horizontal(pts[k], pts[k + 1]);
        for (var s = line.SupportEvery / 2; s < length - 1; s += line.SupportEvery)
        {
            var p = AlongPipe(pts, s);
            var ground = IslandOps.Altitude(island, p.X, p.Z) ?? 0;
            var bottom = p.Y - radius;
            if (bottom - ground < 1500) continue;
            var blocked = false;
            for (var j = 0; j < r.Count && !blocked; j++)
            {
                var dd = Math.Sqrt(Math.Pow(r.X[j] * 512 - p.X, 2) + Math.Pow(r.Z[j] * 512 - p.Z, 2)) / 512;
                if (dd < HalfAt(r, j, o) + 1.6 && r.H[j] > ground - 400 && r.H[j] < p.Y + 1500) blocked = true;
            }
            if (blocked) { skipped++; continue; }
            var mesh = new List<Vector3>(); var faces = new List<Face>();
            var w = radius * 0.8f;
            Box(mesh, faces, Vector3.Zero, new Vector3(w, 0, 0), new Vector3(0, 0, w), 0, (float)(bottom - ground + radius * 0.4), SupportLight, SupportDark, SupportTop);
            var dir = Vector3.Normalize(new Vector3(AlongPipe(pts, s + 0.5).X - AlongPipe(pts, s - 0.5).X, 0, AlongPipe(pts, s + 0.5).Z - AlongPipe(pts, s - 0.5).Z));
            var across = new Vector3(-dir.Z, 0, dir.X);
            Box(mesh, faces, Vector3.Zero, across * (radius * 1.3f), dir * (w * 0.9f), (float)(bottom - ground - 260), (float)(bottom - ground + 60), SupportLight, SupportDark, SupportTop);
            var at = new Vector3(p.X, (float)ground, p.Z);
            if (Add(Write(mesh, faces, lit: false), at, new Vector3(-w, 0, -w), new Vector3(w, (float)(bottom - ground), w))) supports++; else skipped++;
        }
        // the drips: where it is over the road (a part of the lap within the road's width under it, well below it), every DripEvery cells
        var stretches = new List<(double From, double To)>();
        double? open = null;
        for (var s = 0.0; s <= length; s += 0.25)
        {
            var p = AlongPipe(pts, s);
            var over = NearestUnder(r, o, p, radius) is not null;
            if (over && open is null) open = s;
            if (!over && open is { } from) { stretches.Add((from, s - 0.25)); open = null; }
        }
        if (open is { } last) stretches.Add((last, length));
        var drips = 0;
        foreach (var (from, to) in stretches)
            for (var s = from + 0.6; s <= to - 0.3; s += line.DripEvery)
            {
                var p = AlongPipe(pts, s);
                if (NearestUnder(r, o, p, radius) is not { } deck) continue;
                report.Drips.Add(new[] { (int)Math.Round(p.X), (int)Math.Round(p.Y - radius - 30), (int)Math.Round(p.Z), (int)Math.Round(deck), line.DripMs });
                drips++;
            }
        report.Notes.Add($"the pipeline: {pieces} pieces of pipe, {supports} supports ({skipped} left out: the road in a support's way, or a cube full), " +
                         $"{drips} drips over the road in {stretches.Count} places it passes over it");
        return made;
    }

    // the road's deck under a place of the pipe (a part of the lap within its width less a cell, at least 1,600 below the pipe), or null
    private static double? NearestUnder(TrackRoad r, RaceTrackOptions o, Vector3 p, float radius)
    {
        double best = double.MaxValue; double? deck = null;
        for (var j = 0; j < r.Count; j++)
        {
            var dd = Math.Sqrt(Math.Pow(r.X[j] * 512 - p.X, 2) + Math.Pow(r.Z[j] * 512 - p.Z, 2)) / 512;
            if (dd < HalfAt(r, j, o) - 0.9 && r.H[j] < p.Y - radius - 1600 && dd < best) { best = dd; deck = r.H[j]; }
        }
        return deck;
    }

    private static double Horizontal(Vector3 a, Vector3 b) => Math.Sqrt(Math.Pow((b.X - a.X) / 512, 2) + Math.Pow((b.Z - a.Z) / 512, 2));

    // the pipe's middle `s` cells along it (measured on the level)
    private static Vector3 AlongPipe(List<Vector3> pts, double s)
    {
        s = Math.Max(0, s);
        for (var k = 0; k + 1 < pts.Count; k++)
        {
            var l = Horizontal(pts[k], pts[k + 1]);
            if (s <= l || k + 2 == pts.Count) return Vector3.Lerp(pts[k], pts[k + 1], (float)Math.Clamp(l > 1e-9 ? s / l : 0, 0, 1));
            s -= l;
        }
        return pts[^1];
    }

    // a cylinder from a to b, its sides and its caps facing out
    private static void Cylinder(List<Vector3> pts, List<Face> faces, Vector3 a, Vector3 b, float radius, int colour, int sides = 8)
    {
        var axis = Vector3.Normalize(b - a);
        var u = Vector3.Normalize(Math.Abs(axis.Y) > 0.9 ? Vector3.Cross(axis, Vector3.UnitX) : Vector3.Cross(axis, Vector3.UnitY));
        var v = Vector3.Cross(axis, u);
        var ring0 = new int[sides]; var ring1 = new int[sides];
        for (var k = 0; k < sides; k++)
        {
            var t = 2 * Math.PI * k / sides;
            var off = (u * (float)Math.Cos(t) + v * (float)Math.Sin(t)) * radius;
            ring0[k] = pts.Count; pts.Add(a + off);
            ring1[k] = pts.Count; pts.Add(b + off);
        }
        for (var k = 0; k < sides; k++)
        {
            var k2 = (k + 1) % sides;
            int p0 = ring0[k], p1 = ring0[k2], p2 = ring1[k2], p3 = ring1[k];
            var mid = (pts[p0] + pts[p1] + pts[p2] + pts[p3]) / 4;
            var outward = mid - (a + axis * Vector3.Dot(mid - a, axis));
            var nrm = Vector3.Cross(pts[p1] - pts[p0], pts[p2] - pts[p0]);
            faces.Add(Vector3.Dot(nrm, outward) >= 0 ? new Face(new[] { p0, p1, p2, p3 }, colour) : new Face(new[] { p3, p2, p1, p0 }, colour));
        }
        foreach (var (ring, end, sign) in new[] { (ring0, a, -1f), (ring1, b, 1f) })
        {
            var c = pts.Count; pts.Add(end);
            for (var k = 0; k < sides; k++)
            {
                int p0 = ring[k], p1 = ring[(k + 1) % sides];
                var nrm = Vector3.Cross(pts[p0] - pts[c], pts[p1] - pts[c]);
                faces.Add(Vector3.Dot(nrm, axis * sign) >= 0 ? new Face(new[] { c, p0, p1 }, colour) : new Face(new[] { p1, p0, c }, colour));
            }
        }
    }

    // a box round `centre`, `u` and `v` its half sides on the level, from y0 to y1
    internal static void Box(List<Vector3> pts, List<Face> faces, Vector3 centre, Vector3 u, Vector3 v, float y0, float y1, int light, int dark, int top)
    {
        var c = new[] { centre - u - v, centre + u - v, centre + u + v, centre - u + v };
        var lo = c.Select(p => { pts.Add(p + new Vector3(0, y0, 0)); return pts.Count - 1; }).ToArray();
        var hi = c.Select(p => { pts.Add(p + new Vector3(0, y1, 0)); return pts.Count - 1; }).ToArray();
        void Quad(int a, int b, int cc, int d, int colour, Vector3 outward)
        {
            var n = Vector3.Cross(pts[b] - pts[a], pts[cc] - pts[a]);
            faces.Add(Vector3.Dot(n, outward) >= 0 ? new Face(new[] { a, b, cc, d }, colour) : new Face(new[] { d, cc, b, a }, colour));
        }
        for (var k = 0; k < 4; k++)
        {
            var n = (k + 1) % 4;
            Quad(lo[k], lo[n], hi[n], hi[k], k % 2 == 0 ? light : dark, (c[k] + c[n]) / 2 - centre);
        }
        Quad(hi[0], hi[1], hi[2], hi[3], top, Vector3.UnitY);
        Quad(lo[0], lo[1], lo[2], lo[3], dark, -Vector3.UnitY);
    }

    internal static byte[] Write(List<Vector3> pts, List<Face> faces, bool lit) => new Body
    {
        Game = 2, Static = true, Lit = lit, Header = new byte[96],
        Vertices = pts,
        Bones = new List<Bone> { new(0, pts.Count, 0, -1, new byte[8]) },
        Faces = faces,
    }.Write();

    // One gantry, from its origin on the road's middle at the deck: `along` the road's way, `across` to its left; the uprights at `half`
    // either side (world units) from their feet (`down0`, `down1`: the ground under them, from the deck) to Top; the cross pipe between
    // them at CrossHigh; a side pipe along each, `len` long, at SideHigh outside the rails; a red band round each upright under its vent;
    // and with `jetHigh` (0 or more), a steam jet's nozzle out of each upright towards the road, that high over the deck.
    private static byte[] Gantry(Vector3 along, Vector3 across, double half, double down0, double down1, float len, int jetHigh = -1)
    {
        var pts = new List<Vector3>();
        var faces = new List<Face>();
        void Cylinder(Vector3 a, Vector3 b, float radius, int colour, int sides = 8)
        {
            var axis = Vector3.Normalize(b - a);
            var u = Vector3.Normalize(Math.Abs(axis.Y) > 0.9 ? Vector3.Cross(axis, Vector3.UnitX) : Vector3.Cross(axis, Vector3.UnitY));
            var v = Vector3.Cross(axis, u);
            var ring0 = new int[sides]; var ring1 = new int[sides];
            for (var k = 0; k < sides; k++)
            {
                var t = 2 * Math.PI * k / sides;
                var off = (u * (float)Math.Cos(t) + v * (float)Math.Sin(t)) * radius;
                ring0[k] = pts.Count; pts.Add(a + off);
                ring1[k] = pts.Count; pts.Add(b + off);
            }
            for (var k = 0; k < sides; k++)
            {
                var k2 = (k + 1) % sides;
                int p0 = ring0[k], p1 = ring0[k2], p2 = ring1[k2], p3 = ring1[k];
                var mid = (pts[p0] + pts[p1] + pts[p2] + pts[p3]) / 4;
                var axisPoint = a + axis * Vector3.Dot(mid - a, axis);
                var outward = mid - axisPoint;
                var nrm = Vector3.Cross(pts[p1] - pts[p0], pts[p2] - pts[p0]);
                faces.Add(Vector3.Dot(nrm, outward) >= 0 ? new Face(new[] { p0, p1, p2, p3 }, colour) : new Face(new[] { p3, p2, p1, p0 }, colour));
            }
            // its ends: a cap each
            foreach (var (ring, end, sign) in new[] { (ring0, a, -1f), (ring1, b, 1f) })
            {
                var c = pts.Count; pts.Add(end);
                for (var k = 0; k < sides; k++)
                {
                    int p0 = ring[k], p1 = ring[(k + 1) % sides];
                    var nrm = Vector3.Cross(pts[p0] - pts[c], pts[p1] - pts[c]);
                    faces.Add(Vector3.Dot(nrm, axis * sign) >= 0 ? new Face(new[] { c, p0, p1 }, colour) : new Face(new[] { p1, p0, c }, colour));
                }
            }
        }
        var up = Vector3.UnitY;
        var left = across * (float)half; var right = -across * (float)half;
        Cylinder(left + up * (float)down0, left + up * (float)(Top - 260), (float)Upright, Grey);
        Cylinder(right + up * (float)down1, right + up * (float)(Top - 260), (float)Upright, Grey);
        // the vents: a red band and a wider cap at each top
        Cylinder(left + up * (float)(Top - 260), left + up * (float)Top, (float)(Upright * 1.35), Red);
        Cylinder(right + up * (float)(Top - 260), right + up * (float)Top, (float)(Upright * 1.35), Red);
        // the cross pipe over the road (through the uprights) and the side pipes along it outside the rails
        Cylinder(left * 1.08f + up * (float)CrossHigh, right * 1.08f + up * (float)CrossHigh, (float)Cross, Grey);
        foreach (var side in new[] { across * (float)(half - 420), -across * (float)(half - 420) })
            Cylinder(side + up * (float)SideHigh - along * (len / 2), side + up * (float)SideHigh + along * (len / 2), (float)Side, Grey, 6);
        if (jetHigh >= 0)
            foreach (var s in new[] { 1f, -1f })
            {
                // (from the upright's middle out past its side, and a wider red mouth at its end: the steam comes out of that)
                var inward = -across * s;
                var root = across * s * (float)half + up * jetHigh;
                var tip = root + inward * (float)(Upright + Nozzle);
                Cylinder(root, tip - inward * 60, (float)NozzleRadius, Grey, 6);
                Cylinder(tip - inward * 60, tip, (float)NozzleMouth, Red, 6);
            }
        var body = new Body
        {
            Game = 2, Static = true, Lit = true, Header = new byte[96],
            Vertices = pts,
            Bones = new List<Bone> { new(0, pts.Count, 0, -1, new byte[8]) },
            Faces = faces,
        };
        return body.Write();
    }
}
