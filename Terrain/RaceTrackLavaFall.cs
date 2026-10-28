using System.Numerics;

namespace LBAAssembler.Terrain;

// A lava fall beside the road (RaceTrackPlan.LavaFalls): a crag of rock made in the island's ground, a stream of lava across its top from
// a little crater at its back to the lip of its face, the lava pouring straight down the face into a pool at its foot. The idea is scene
// 100's (Wannies Island, the mine's second room: a fall of lava beside the mine's rails, spitting balls of lava out of its foot) -- the
// user, 2026-10-10: "there's a lava water fall next to a section of track that shoots lava balls, let's steal this idea and have it a
// feature of our track for Volcano Island". The face is one cell deep, its triangles near upright, so the ground's lava tile is drawn
// stretched down it in streaks. The ground copied is the island's own: its rock (Rock) and its lava (Lava, with its game code: a car
// that falls in is rescued as from any lava). The race-track mode sprays the game's lava off the fall's lip and out of its foot
// (RACEMOD.CPP lavafall=, report.LavaFalls), and the plan's lava balls with the pool as their source shoot out of it onto the road.
// Made big, the same: Volcano Island's summit since its redesign (2026-10-10, the user: "the lava waterfall in a new location as pictured,
// perhaps as tall as the level allows that shoots lava balls") -- the ground north of its main lava channel raised to 30,000 (Top: the
// island's ground holds 32,767), the stream its whole width a lake on top, the fall down its face in Tiers (a ledge of lava, a cell deep, and
// a drop, TierDepth cells each), the island's own objects inside it taken out.
internal sealed class LavaFallRun
{
    // the foot of the face, its middle (the plan's cells), and the way the face looks (towards the road: one of the four axes)
    public double[] At { get; set; } = Array.Empty<double>();
    public double[] Facing { get; set; } = { 0, 1 };
    // the fall's width (cells), the crag's top over the ground at the face's foot, its depth back from the face, its width either side of
    // the fall (cells), the pool's reach out from the face (cells)
    public double Width { get; set; } = 3;
    public double Height { get; set; } = 4500;
    public double Depth { get; set; } = 8;
    public double Wing { get; set; } = 6;
    public double Pool { get; set; } = 2;
    // the crag's top as a height, not over the ground (null: Height over it); the fall's tiers, each this many cells deep (1: one drop)
    public double? Top { get; set; }
    public int Tiers { get; set; } = 1;
    public double TierDepth { get; set; } = 2;
    // a cell of the island's rock and one of its lava (the plan's cells) to copy the ground of
    public double[] Rock { get; set; } = Array.Empty<double>();
    public double[] Lava { get; set; } = Array.Empty<double>();
    // its lava carried on from the crag's back over the road to To and poured off there (null: none)
    public AqueductRun? Aqueduct { get; set; }
}

// An aqueduct of rock carrying a fall's lava away from its crag's back (RaceTrackLavaFall.Aqueduct): Volcano Island's, west over the west
// side of the plateau and off the bluff into the sea -- the user, 2026-10-11: "Perhaps we can cross by the lava waterfall twice, once
// passing in front of it, and once under the lava flow". To where it ends (the plan's cells), its lava's surface (a height), its width
// (units). Over the road its underside is level, Clear under the lava; beyond it, it goes down into the ground (the crag's rock at one
// end, a pillar on the bluff's edge at the other). The lava pours off its end down the ground below, painted lava to the island's edge.
internal sealed class AqueductRun
{
    public double[] To { get; set; } = Array.Empty<double>();
    public double Surface { get; set; } = 16000;
    public double Width { get; set; } = 1700;
}

internal static class RaceTrackLavaFall
{
    // the stream's bed under the crag's top, the pool under the ground, the crag's top's roughness (units); how far its sides and back
    // fall to the ground (cells); the lava's light (0..15: it glows -- a face turned from the sun was baked dark)
    private const double Bed = 350, PoolDrop = 150, Rough = 600, SideFall = 1.6, BackFall = 2.2, Wobbly = 1.6;
    private const int Glow = 13;
    private const int MaxPlaces = 16;              // RACEMOD.CPP RACE_MAX_FALLS

    public static void Make(IslandFile island, LavaFallRun run, int originX, int originZ, TrackRoad road, RaceTrackOptions o, RaceTrackReport report)
    {
        if (run.At is not { Length: >= 2 } || run.Rock is not { Length: >= 2 } || run.Lava is not { Length: >= 2 }) { report.Notes.Add("WARNING: a lava fall with no place, rock or lava"); return; }
        // (the face square to an axis: a face slanting across the cells is a staircase)
        int ux, uz;
        if (Math.Abs(run.Facing[0]) >= Math.Abs(run.Facing[1])) { ux = Math.Sign(run.Facing[0]); uz = 0; } else { ux = 0; uz = Math.Sign(run.Facing[1]); }
        if (ux == 0 && uz == 0) uz = 1;
        int ax = -uz, az = ux;                                     // across the face
        var fx = (int)Math.Round(run.At[0] + originX); var fz = (int)Math.Round(run.At[1] + originZ);
        var rock = Samples(island, (int)Math.Round(run.Rock[0] + originX), (int)Math.Round(run.Rock[1] + originZ));
        var lava = Samples(island, (int)Math.Round(run.Lava[0] + originX), (int)Math.Round(run.Lava[1] + originZ));
        if (rock.Count == 0 || lava.Count == 0 || island.HeightAt(fx, fz) is not { } foot) { report.Notes.Add($"WARNING: the lava fall at ({fx}, {fz}): no ground there, or no rock or lava to copy"); return; }
        var halfW = run.Width / 2; var outer = halfW + run.Wing;
        var top = run.Top ?? foot + run.Height;
        var lake = top - Bed;
        var tiers = Math.Max(1, run.Tiers);
        // (a vertex `f` cells back from the face, on the fall: its tier's ledge -- the front one lowest -- or the lake behind them)
        double Ledge(int f) { var t = (int)Math.Floor(-f / Math.Max(1, run.TierDepth)); return t >= tiers - 1 ? lake : foot + (lake - foot) * (t + 1) / tiers; }
        double Hash(int x, int z) { var h = (uint)(x * 73856093 ^ z * 19349663); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (h & 0xFFFF) / 32767.5 - 1; }
        // (a wobble along a line, -1..1, changing over a few cells: the crag's sides and back in and out, not a box's)
        double Wobble(double t, int line) { var i = (int)Math.Floor(t / 3); var u = t / 3 - i; u = u * u * (3 - 2 * u); return Hash(i, line) * (1 - u) + Hash(i + 1, line) * u; }

        // the heights: the crag (its top rough but for the stream's bed, its sides and back falling steeply to the ground), its face (the
        // vertices along the lip at the top, the next row out the pool's), the pool
        var reach = (int)Math.Ceiling(outer + SideFall + 2); var back = (int)Math.Ceiling(run.Depth + BackFall + 2); var ahead = (int)Math.Ceiling(run.Pool + 2);
        var original = new Dictionary<(int, int), int>();
        var lavaVertex = new HashSet<(int, int)>();
        var raised = new HashSet<(int, int)>();
        var changed = 0;
        for (var f = -back; f <= ahead; f++)
        for (var a = -reach; a <= reach; a++)
        {
            int gx = fx + ux * f + ax * a, gz = fz + uz * f + az * a;
            if (island.HeightAt(gx, gz) is not { } g) continue;
            original[(gx, gz)] = g;
            double? want = null;
            if (f <= 0)
            {
                // inside the crag's footprint: how far in from its sides and its back (cells; each wobbling in and out along it, by up to
                // Wobbly -- the back not in behind the crater), eased up to its top
                var inBack = f + run.Depth;
                var wob = Wobbly * Wobble(a + 40, 13);
                if (Math.Abs(a) <= halfW + 2.5) wob = Math.Max(0, wob);
                var inSide = outer + Wobbly * Wobble(f + 40, a < 0 ? 7 : 11) - Math.Abs(a);
                var k = Math.Min(Math.Min(inSide / SideFall, (inBack + wob) / BackFall), 1);
                if (k > 0)
                {
                    var stream = Math.Abs(a) <= halfW - 0.5 && inBack >= 2 - 1e-9;
                    var crater = Math.Abs(a) <= halfW + 0.5 && inBack >= 2 - 1e-9 && inBack <= 4 + 1e-9;
                    var high = stream || crater ? (stream ? Ledge(f) : lake) : top + Rough * Hash(gx, gz) - (k < 1 ? Rough * 0.5 : 0);
                    var e = k * k * (3 - 2 * k);
                    want = g + (high - g) * e;
                    if ((stream || crater) && k >= 1) lavaVertex.Add((gx, gz));
                    if (want > g + 30) raised.Add((gx, gz));
                }
            }
            else if (f <= run.Pool && Math.Abs(a) <= halfW + 1) { want = Math.Min(g, foot) - PoolDrop; lavaVertex.Add((gx, gz)); }
            if (want is not { } w) continue;
            island.SetHeight(gx, gz, (int)Math.Round(Math.Clamp(w, -32000, 32000)));
            changed++;
        }

        // the ground painted: lava over the stream, the crater, the face's middle and the pool (with its rim), rock over the rest of the crag
        int lavaCells = 0, rockCells = 0;
        for (var f = -back; f < ahead; f++)
        for (var a = -reach; a < reach; a++)
        {
            // (the cell from vertex (f, a) to (f + 1, a + 1), its middle half a cell on each way)
            double cf = f + 0.5, ca = a + 0.5;
            int gx = Math.Min(fx + ux * f + ax * a, fx + ux * (f + 1) + ax * (a + 1)), gz = Math.Min(fz + uz * f + az * a, fz + uz * (f + 1) + az * (a + 1));
            var inBack = cf + run.Depth;
            var isLava = cf < 0 && Math.Abs(ca) <= halfW && inBack >= 2                                  // the stream
                         || cf < 0 && Math.Abs(ca) <= halfW + 1 && inBack >= 2 && inBack <= 4                // the crater at its back
                         || cf > 0 && cf < 1 && Math.Abs(ca) <= halfW                                       // the fall
                         || cf > 1 && cf < run.Pool + 1 && Math.Abs(ca) <= halfW + 1.5;                     // the pool
            // (rock: a cell with a corner the crag raised)
            var isRock = !isLava && cf < 1 && new[] { (f, a), (f + 1, a), (f, a + 1), (f + 1, a + 1) }.Any(c => raised.Contains((fx + ux * c.Item1 + ax * c.Item2, fz + uz * c.Item1 + az * c.Item2)));
            if (isLava) { PaintCell(island, gx, gz, lava[(int)((Hash(gx, gz) + 1) / 2 * lava.Count) % lava.Count]); lavaCells++; }
            else if (isRock) { PaintCell(island, gx, gz, rock[(int)((Hash(gz, gx) + 1) / 2 * rock.Count) % rock.Count]); rockCells++; }
        }

        // the island's objects standing inside the crag taken out (they would be drawn over it: the ground is drawn first)
        var removed = 0;
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
            removed += cube.Decors.RemoveAll(d =>
            {
                var gx = (int)Math.Round(cx * IslandCube.Cells + d.X / 512.0); var gz = (int)Math.Round(cz * IslandCube.Cells + d.Z / 512.0);
                return raised.Contains((gx, gz)) && island.HeightAt(gx, gz) is { } h && h > d.Y + 400;
            });

        // the light, baked again over what changed (the lava's at least Glow)
        var field = new IslandHeightField(island);
        var cube0 = island.CubeAt(fx / IslandCube.Cells, fz / IslandCube.Cells);
        var bake = cube0 is null ? new BakeOptions() : BakeOptions.For(cube0);
        foreach (var (gx, gz) in original.Keys)
        {
            var lit = (int)Math.Round(Math.Clamp(IslandBake.Compute(field, new List<(double, double, double, double, double, double)>(), gx, gz, bake), 0, 15));
            if (lavaVertex.Contains((gx, gz))) lit = Math.Max(lit, Glow);
            island.SetLight(gx, gz, lit);
        }

        // the race-track mode's: across the fall, its lip (a little back from the face, on the stream) and its foot below it (in the pool)
        var beta = ((int)Math.Round(Math.Atan2(ux, uz) * 4096 / (2 * Math.PI)) % 4096 + 4096) % 4096;
        // (at most RACE_MAX_FALLS of them, spread across: the engine's)
        var step = Math.Max(1, Math.Ceiling((2 * Math.Floor(halfW) + 1) / MaxPlaces));
        for (var a = -Math.Floor(halfW); a <= Math.Floor(halfW) + 1e-9; a += step)
        {
            double lx = fx + ux * -0.2 + ax * a, lz = fz + uz * -0.2 + az * a, px = fx + ux * 1.4 + ax * a, pz = fz + uz * 1.4 + az * a;
            report.LavaFalls.Add(new[] { (int)Math.Round(lx * 512), (int)Math.Round(lake), (int)Math.Round(lz * 512),
                                         (int)Math.Round(px * 512), (int)Math.Round(foot - PoolDrop), (int)Math.Round(pz * 512), beta });
        }
        if (run.Aqueduct is { To.Length: >= 2 } aq)
            Aqueduct(island, aq, fx - ux * (run.Depth - 0.8), fz - uz * (run.Depth - 0.8), aq.To[0] + originX, aq.To[1] + originZ, lava, road, o, report);
        var s = lava[0][0].Polygon;
        report.Notes.Add($"a lava fall at ({fx}, {fz}) facing ({ux}, {uz}): its crag's top {top:0} (the ground {foot}), {tiers} tier(s), {changed} heights, {rockCells} cells of rock, " +
                         $"{lavaCells} of lava (code {s.CodeJeu}, texture flag {s.TexFlag}, polygon flag {s.PolyFlag}), {removed} island object(s) inside it taken out");
    }

    // the aqueduct's look: its lava's surface over the floor of its channel's walls, the walls' thickness, its underside under the lava over
    // the road (units); how far beyond the road it reaches down to the ground (cells); its colours, palette indices as the game draws its
    // objects through the island's colour tables (an in-game swatch of all 256, 2026-10-11: each ramp's light end goes cream or yellow --
    // the lava first drawn there came out pale): ramp 4's dark browns for its rock, ramp 6's red-orange for its lava, ramp 5's red
    private const double WallUp = 250, Wall = 300, Clear = 700, Down = 2.5, PourDrop = 2600, Spill = 450;
    private const int RockRamp = 4, LavaTop = 6 * 16 + 7, LavaSpill = 6 * 16 + 6, LavaDeep = 6 * 16 + 5, LavaEdge = 5 * 16 + 9;

    private static void Aqueduct(IslandFile island, AqueductRun aq, double sx, double sz, double ex, double ez, List<IslandGround.Sample[]> lava, TrackRoad r,
        RaceTrackOptions o, RaceTrackReport report)
    {
        if (o.NewBodyBase < 0) { report.Notes.Add("WARNING: an aqueduct: no place for new bodies"); return; }
        double dx = ex - sx, dz = ez - sz, len = Math.Sqrt(dx * dx + dz * dz);
        if (len < 2) return;
        dx /= len; dz /= len;
        // (the body's +x turned by the decor's beta is (cos, -sin) of it: along the aqueduct)
        var beta = ((int)Math.Round(Math.Atan2(-dz, dx) * 4096 / (2 * Math.PI)) % 4096 + 4096) % 4096;
        if (IslandDecors.Locate(island, sx * 512, sz * 512) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCubeRaceTrack)
        { report.Notes.Add($"WARNING: the aqueduct from ({sx:0.#}, {sz:0.#}): no room"); return; }
        // its underside along it: level over the road (within its rails and a cell, under the lava's level), down into the ground beyond
        var n = Math.Max(4, (int)Math.Ceiling(len * 512 / 600));
        var under = new double[n + 1];
        bool OverRoad(double x, double z)
        {
            for (var k = 0; k < r.Count; k++)
            {
                if (r.H[k] > aq.Surface) continue;
                var half = (r.RaisedHalfs?[k] ?? o.RaisedHalfWidth) + 1.2;
                if ((r.X[k] - x) * (r.X[k] - x) + (r.Z[k] - z) * (r.Z[k] - z) < half * half) return true;
            }
            return false;
        }
        var over = Enumerable.Range(0, n + 1).Select(i => OverRoad(sx + dx * len * i / n, sz + dz * len * i / n)).ToArray();
        var lowestDeck = double.MaxValue;
        for (var k = 0; k < r.Count; k++)
            for (var i = 0; i <= n; i++)
                if (over[i] && Math.Abs(r.X[k] - (sx + dx * len * i / n)) < 6 && Math.Abs(r.Z[k] - (sz + dz * len * i / n)) < 6) lowestDeck = Math.Min(lowestDeck, r.H[k]);
        for (var i = 0; i <= n; i++)
        {
            // (cells from the nearest part over the road)
            var far = double.MaxValue;
            for (var j = 0; j <= n; j++) if (over[j]) far = Math.Min(far, Math.Abs(i - j) * len / n);
            var g = IslandOps.Altitude(island, (sx + dx * len * i / n) * 512, (sz + dz * len * i / n) * 512) ?? 0;
            var deep = aq.Surface - (g - 300);
            var f = far == double.MaxValue ? 1 : Math.Clamp(far / Down, 0, 1);
            under[i] = -(Clear + (deep - Clear) * f * f * (3 - 2 * f));
        }
        var m = new RaceTrackRaisedBody.Mesh();
        var light = Vector3.Normalize(new Vector3(0.35f, 0.85f, 0.4f));
        int Shade(Vector3 nrm, int ramp, int lo) => ramp * 16 + Math.Clamp(lo + (int)Math.Round(5 * Math.Max(0, Vector3.Dot(Vector3.Normalize(nrm), light))), 0, 15);
        float h = (float)(aq.Width / 2), inner = (float)(aq.Width / 2 - Wall);
        var sections = new List<int[]>();
        for (var i = 0; i <= n; i++)
        {
            var x = (float)(len * 512 * i / n); var u = (float)under[i];
            // (its outer walls run with lava spilling over them, down to Spill under the lava, over the road: seen from under it, it is the
            // lava's -- a rock bridge, its lava out of sight on top, until 2026-10-11)
            var spill = over[i] ? (float)-Spill : (float)WallUp;
            sections.Add(new[] { m.P(new(x, u, -h)), m.P(new(x, (float)WallUp, -h)), m.P(new(x, (float)WallUp, -inner)), m.P(new(x, 0, -inner)),
                                 m.P(new(x, 0, inner)), m.P(new(x, (float)WallUp, inner)), m.P(new(x, (float)WallUp, h)), m.P(new(x, u, h)),
                                 m.P(new(x, spill, -h)), m.P(new(x, spill, h)) });
        }
        var up = Vector3.UnitY; var side = Vector3.UnitZ; var along = Vector3.UnitX;
        for (var i = 0; i < n; i++)
        {
            int[] a = sections[i], b = sections[i + 1];
            void Q(int p, int q, int colour, Vector3 outward) => m.Quad(a[p], a[q], b[q], b[p], colour, outward);
            Q(0, 8, Shade(-side, RockRamp, 2), -side);                 // the outer walls: rock under the lava spilling down them
            Q(9, 7, Shade(side, RockRamp, 2), side);
            Q(8, 1, LavaSpill, -side);
            Q(6, 9, LavaSpill, side);
            Q(1, 2, Shade(up, RockRamp, 4), up);                       // their tops
            Q(5, 6, Shade(up, RockRamp, 4), up);
            Q(2, 3, LavaEdge, side);                                   // the channel's sides, red
            Q(4, 5, LavaEdge, -side);
            Q(3, 4, LavaTop, up);                                      // the lava
            Q(7, 0, Shade(-up, RockRamp, 0), -up);                     // the underside
        }
        // its ends: the crag's (inside its rock) and the pour's, with the lava's sheet falling off it
        foreach (var (sec, outward) in new[] { (sections[0], -along), (sections[n], along) })
            for (var t = 1; t + 1 < 8; t++) m.Tri(sec[0], sec[t], sec[t + 1], Shade(outward, RockRamp, 2), outward);
        // drips of lava hanging under it over the road, a row down each side of its underside
        var rnd = new Random(n * 31);
        for (var i = 1; i < n; i++)
        {
            if (!over[i] || i % 2 == 1) continue;
            foreach (var zs in new[] { -0.55f, 0.55f })
            {
                var x = (float)(len * 512 * i / n + (rnd.NextDouble() - 0.5) * 200); var z = zs * h; var u = (float)under[i];
                var drip = (float)(350 + rnd.NextDouble() * 450);
                int t0 = m.P(new(x - 110, u, z - 110)), t1 = m.P(new(x + 110, u, z - 110)), t2 = m.P(new(x, u, z + 130)), tip = m.P(new(x, u - drip, z));
                m.Tri(t0, t1, tip, LavaTop, new Vector3(0, -0.3f, -1));
                m.Tri(t1, t2, tip, LavaSpill, new Vector3(1, -0.3f, 0.5f));
                m.Tri(t2, t0, tip, LavaDeep, new Vector3(-1, -0.3f, 0.5f));
            }
        }
        {
            var end = (float)(len * 512);
            int p0 = m.P(new(end, 0, -inner)), p1 = m.P(new(end, 0, inner)), p2 = m.P(new(end + 350, (float)-PourDrop, inner + 120)), p3 = m.P(new(end + 350, (float)-PourDrop, -inner - 120));
            m.Quad(p0, p1, p2, p3, LavaTop, along + up);
            m.Quad(p3, p2, p1, p0, LavaSpill, -along);
        }
        var body = m.Write();
        var d = IslandDecors.Blank(o.NewBodyBase + report.NewBodies.Count, at.X, (int)Math.Round(aq.Surface), at.Z, beta);
        double bx0 = Math.Min(0, dx * len * 512), bx1 = Math.Max(0, dx * len * 512), bz0 = Math.Min(0, dz * len * 512), bz1 = Math.Max(0, dz * len * 512);
        d.XMin = at.X + (int)Math.Floor(bx0 - aq.Width); d.XMax = at.X + (int)Math.Ceiling(bx1 + aq.Width);
        d.ZMin = at.Z + (int)Math.Floor(bz0 - aq.Width); d.ZMax = at.Z + (int)Math.Ceiling(bz1 + aq.Width);
        d.YMin = (int)Math.Round(aq.Surface + under.Min()); d.YMax = -32000;
        at.Cube.Decors.Add(d);
        report.NewBodies.Add(body);

        // the lava down the ground below its end, to the island's edge: the cells along its way from its end on, two either side
        int painted = 0;
        for (var t = 0.0; t <= 8; t += 0.5)
        {
            double px = ex + dx * t, pz = ez + dz * t;
            for (var w = -2; w <= 2; w++)
            {
                int gx = (int)Math.Floor(px - dz * w), gz = (int)Math.Floor(pz + dx * w);
                if (island.HeightAt(gx, gz) is null || island.HeightAt(gx + 1, gz + 1) is null) continue;
                PaintCell(island, gx, gz, lava[(gx * 7 + gz * 13 & 0x7FFF) % lava.Count]);
                foreach (var (vx, vz) in new[] { (gx, gz), (gx + 1, gz), (gx, gz + 1), (gx + 1, gz + 1) })
                    if (island.LightAt(vx, vz) is { } l && l < Glow) island.SetLight(vx, vz, Glow);
                painted++;
            }
        }
        // the race-track mode's spit and sparks at its end: off its lip, down to the foot of the ground below
        var pourBeta = ((int)Math.Round(Math.Atan2(dx, dz) * 4096 / (2 * Math.PI)) % 4096 + 4096) % 4096;
        foreach (var w in new[] { -0.4, 0.4 })
        {
            double lx = ex - dz * w, lz = ez + dx * w, fx2 = ex + dx * 3 - dz * w, fz2 = ez + dz * 3 + dx * w;
            var foot = IslandOps.Altitude(island, fx2 * 512, fz2 * 512) ?? 0;
            report.LavaFalls.Add(new[] { (int)Math.Round(lx * 512), (int)Math.Round(aq.Surface), (int)Math.Round(lz * 512), (int)Math.Round(fx2 * 512), (int)Math.Round(foot + 100),
                                         (int)Math.Round(fz2 * 512), pourBeta });
        }
        report.Notes.Add($"an aqueduct of lava from ({sx:0.#}, {sz:0.#}) to ({ex:0.#}, {ez:0.#}), {len:0.0} cells, its lava at {aq.Surface:0}" +
                         (lowestDeck < double.MaxValue ? $", {aq.Surface - Clear - lowestDeck:0} over the road under it" : ", over no road") + $"; {painted} cells of lava below its end");
    }

    // The ground of a cell and the three either side of it along x (both its triangles, as they are): copies to paint with. A cell's two
    // triangles go together -- their texture corners follow the way it is cut.
    private static List<IslandGround.Sample[]> Samples(IslandFile island, int gx, int gz)
    {
        var list = new List<IslandGround.Sample[]>();
        for (var d = -1; d <= 1; d++)
        {
            var a = IslandGround.Pick(island, gx + d, gz, 0); var b = IslandGround.Pick(island, gx + d, gz, 1);
            if (a is null || b is null || a.Texture is null || b.Texture is null || a.Polygon.TexFlag == 0) continue;
            list.Add(new[] { a, b });
        }
        return list;
    }

    private static void PaintCell(IslandFile island, int gx, int gz, IslandGround.Sample[] halves)
    {
        if (island.CubeAt(gx / IslandCube.Cells, gz / IslandCube.Cells) is not { HasPolygons: true } cube) return;
        int x = gx % IslandCube.Cells, z = gz % IslandCube.Cells;
        for (var h = 0; h < 2; h++)
        {
            var s = halves[h];
            var p = new IslandPolygon(cube.Polygon(x, z, h)).With(bank: s.Polygon.Bank, texFlag: s.Polygon.TexFlag, polyFlag: s.Polygon.PolyFlag, sampleStep: s.Polygon.SampleStep,
                codeJeu: s.Polygon.CodeJeu, diagonal: s.Polygon.Diagonal);
            if (s.Texture is not null) p = island.WithGroundTexture(p, island.GroundTextureOf(s.Polygon).Page, IslandGround.TextureIndexFor(cube, s.Texture));
            cube.SetPolygon(x, z, h, p.Raw);
        }
    }
}
