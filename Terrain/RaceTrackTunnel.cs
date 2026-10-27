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
    // UnderGround: the roof's top kept this far under the island's ground over the tunnel, where the road is under the ground -- a tall
    // tunnel down under a plateau whose edge it passes under (Island CX's ramps from its doors, 2026-10-16)
    public double? UnderGround { get; set; }
    // CloseAbove: at an end of the tunnel that no other tunnel goes on from, a wall across it from this height (island units) up to its
    // roof -- a tunnel that comes out under a roof of the island's own, through which it passes: the rock over that roof isn't drawn
    // (Island CX's ramps, out into its hall under the hall's roof)
    public double? CloseAbove { get; set; }
    // Ceiling: for the camera alone (RACEMOD.CPP tunnel='s fourth number), a height its eye is kept under -- a stretch under a level roof of
    // the island's own whose height over the road changes (Island CX's ramps in its hall, under the hall's roof)
    public double? Ceiling { get; set; }
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
    // (a lava geyser's, RaceTrackPlan.LavaGeysers: the balls of each shower)
    public int Balls { get; set; }
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
        var n = r.Count;
        // each tunnel's roof at its points (a sunk one's after its cutting, as its pieces are built on it) and its least over the deck --
        // in the plan's order: a cutting changes the ground the roofs after it are kept under
        var runs = new List<(int From, int To, TunnelRun Run, int Span, double Least, double[] Roofs, (int, double) Cut)>();
        foreach (var (from, to, run) in tunnels)
        {
            var span = ((to - from) % n + n) % n;
            var least = run.Roof;
            for (var k = 0; k <= span; k++) { var i = (from + k) % n; least = Math.Min(least, RoofAt(island, r, i, run, o) - r.H[i]); }
            var cut = run.Pieces && run.Sunk ? CutSunk(island, r, from, span, o) : (0, 0.0);
            var roofs = new double[span + 1];
            for (var k = 0; k <= span; k++) roofs[k] = RoofAt(island, r, (from + k) % n, run, o);
            runs.Add((from, to, run, span, least, roofs, cut));
        }
        var strips = StripHeights(r, runs.Where(t => t.Run.Pieces).Select(t => (t.From, t.Span, t.Roofs)).ToList(), o);
        foreach (var (from, to, run, span, least, roofs, cut) in runs)
        {
            if (!run.Pieces)
            {
                report.Tunnels.Add((from, to, least, run.Ceiling));
                report.Notes.Add($"tunnel: {span * o.Spacing:0} cells from cell ({r.X[from]:0.0}, {r.Z[from]:0.0}) to ({r.X[to]:0.0}, {r.Z[to]:0.0}) under the island's own roof, {least:0} over the deck");
                continue;
            }
            // (whether another tunnel of pieces goes on from either end)
            var startFree = !tunnels.Any(t => t.Run.Pieces && t.To == from && t.Run != run);
            var endFree = !tunnels.Any(t => t.Run.Pieces && t.From == to && t.Run != run);
            var step = Math.Max(2, (int)Math.Round(Piece / o.Spacing));
            int pieces = 0, skipped = 0;
            for (var k0 = 0; k0 < span; k0 += step)
            {
                var k1 = Math.Min(span, k0 + step);
                var mid = (from + (k0 + k1) / 2) % n;
                // (in whole units: the pieces either side of a joint put its points in the same place -- no crack)
                var origin = new Vector3(MathF.Round((float)(r.X[mid] * 512)), MathF.Round((float)r.H[mid]), MathF.Round((float)(r.Z[mid] * 512)));
                var mesh = new List<Vector3>(); var faces = new List<Face>();
                Surfaces(mesh, faces, island, r, from, k0, k1, run, roofs, strips, origin, o);
                // (an end of the tunnel: what closes it)
                foreach (var (k, isEnd) in new[] { (k0, false), (k1, true) })
                {
                    if (isEnd ? k != span : k != 0) continue;
                    int i = (from + k) % n, j = (from + (isEnd ? k - 1 : k + 1)) % n;
                    var at = PointAt(r, i, origin);
                    var dir = Vector3.Normalize(new Vector3((float)r.Tx[i], 0, (float)r.Tz[i]));
                    var across = new Vector3(-dir.Z, 0, dir.X);
                    var sign = isEnd ? 1f : -1f;
                    var half = (float)((HalfAt(r, i, o) + WallOut) * 512);
                    var roof = (float)(roofs[k] - origin.Y);
                    var eave = (float)(run.Eave * 512);
                    // (a sunk tunnel's end where the road goes on under the island, `under`: a wall over the next tunnel's mouth, from its roof
                    // -- this one's Roof over the deck -- up to this one's top: the ground's face over the mouth is left undrawn
                    // (RaceTrackBuilder.OpenTunnelMouths) and the gap between the two roofs showed the sky -- Island CX's doors, 2026-10-15)
                    if (run.Sunk && r.Under?[i] == true)
                    {
                        var hc = new Vector3(at.X, 0, at.Z) - dir * sign * ((float)Wall / 2);
                        var bottom = at.Y + (float)run.Roof;
                        // (up to the ground behind it where that is higher than this tunnel's top: Island CX's plateau stands 320 over
                        // door A's portal, and its edge showed over the portal's roof, floating)
                        var headTop = roof + (float)Slab;
                        var reachCells = HalfAt(r, i, o) + WallOut + Wall / 512 + run.Eave + 1;
                        for (var gz = (int)Math.Floor(r.Z[i] - reachCells); gz <= (int)Math.Ceiling(r.Z[i] + reachCells); gz++)
                            for (var gx = (int)Math.Floor(r.X[i] - reachCells); gx <= (int)Math.Ceiling(r.X[i] + reachCells); gx++)
                                if ((gx - r.X[i]) * (gx - r.X[i]) + (gz - r.Z[i]) * (gz - r.Z[i]) <= reachCells * reachCells && island.HeightAt(gx, gz) is { } gh)
                                    headTop = Math.Max(headTop, gh - origin.Y + 20);
                        if (headTop > bottom + 50)
                            RaceTrackPipes.Box(mesh, faces, hc, dir * ((float)Wall / 2 + 12), across * (half + (float)Wall + eave), bottom, headTop, WallLight, WallLight, WallLight);
                    }
                    // (an end no other tunnel goes on from, CloseAbove: a wall across it from that height up to the roof's top)
                    if (run.CloseAbove is { } above && (isEnd ? endFree : startFree))
                    {
                        var bottom = (float)(above - origin.Y);
                        if (roof + (float)Slab >= bottom + 50)
                        {
                            var cc = new Vector3(at.X, 0, at.Z) - dir * sign * ((float)Wall / 2);
                            RaceTrackPipes.Box(mesh, faces, cc, dir * ((float)Wall / 2 + 12), across * (half + (float)Wall + eave), bottom, roof + (float)Slab, WallLight, WallLight, WallLight);
                        }
                    }
                }
                if (faces.Count == 0) continue;
                if (IslandDecors.Locate(island, origin.X, origin.Z) is not { } where || where.Cube.Decors.Count >= IslandDecors.MaxPerCubeRaceTrack) { skipped++; continue; }
                var (cube, lx, lz) = where;
                var lo = mesh.Aggregate(new Vector3(float.MaxValue), Vector3.Min); var hi = mesh.Aggregate(new Vector3(float.MinValue), Vector3.Max);
                var d = IslandDecors.Blank(o.NewBodyBase + report.NewBodies.Count, lx, (int)Math.Round(origin.Y), lz, 0);
                // (drawn close to the camera, BrickBuilding.CloseFlag: the camera rides inside a tunnel, and the near plane cut away the
                // wall beside it and the roof over it -- the ground outside showed through, Island CX's tall ramps, 2026-10-16)
                var body = RaceTrackPipes.Write(mesh, faces, lit: false);
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(body, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(body) | LBAAssembler.Terrain.Palace.BrickBuilding.CloseFlag);
                report.NewBodies.Add(body);
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
            report.Tunnels.Add((from, to, least, run.Ceiling));
            report.Notes.Add($"tunnel: {span * o.Spacing:0} cells from cell ({r.X[from]:0.0}, {r.Z[from]:0.0}) to ({r.X[to]:0.0}, {r.Z[to]:0.0}), its roof {run.Roof:0} over the deck" +
                             (run.Top is { } t ? $" (its top no higher than {t:0}: {least:0} over the deck at the least)" : "") + $", {pieces} pieces{(skipped > 0 ? $" ({skipped} left out: a cube full)" : "")}" +
                             (run.Sunk ? $"; sunk: the ground cut down between its walls at {cut.Item1} vertices, {cut.Item2:0} at the most" : ""));
        }
        return made;
    }

    // The roof's height (its underside, island units) at point i of the tunnel `run`.
    private static double RoofAt(IslandFile island, TrackRoad r, int i, TunnelRun run, RaceTrackOptions o)
    {
        var roof = run.Top is { } top && r.H[i] + run.Roof + Slab > top ? top - Slab : r.H[i] + run.Roof;
        if (run.UnderGround is { } under && GroundOver(island, r, i, o) is { } g && g > r.H[i]) roof = Math.Min(roof, g - under - Slab);
        return roof;
    }

    private static Vector3 PointAt(TrackRoad r, int i, Vector3 origin) => new Vector3((float)(r.X[i] * 512), (float)r.H[i], (float)(r.Z[i] * 512)) - origin;

    // The light strip's middle over the deck at each point of the tunnels of pieces: one height along each run of tunnels that follow one
    // another -- the middle of their height over the deck as it mostly is (its median) -- so the line runs along the walls' middle parallel
    // to the road; lowered where a tunnel is too low for it, to no nearer the roof than 200, and eased over StripEase cells either way so
    // it never steps (Island CX's doors: a portal 1,200 high, then a ramp 2,200 high -- the line at 1,100, and at 955 in the portals). (The
    // user, 2026-10-16: "smooth them out so they're all one colour with a single straight line down the middle instead of the current jagged
    // gold line".)
    public const double StripEase = 6, StripHalf = 45, StripOut = 40, StripClear = 200;
    private static Dictionary<int, double> StripHeights(TrackRoad r, IReadOnlyList<(int From, int Span, double[] Roofs)> runs, RaceTrackOptions o)
    {
        var n = r.Count;
        var clear = new Dictionary<int, double>();
        foreach (var (from, span, roofs) in runs)
            for (var k = 0; k <= span; k++)
            {
                var i = (from + k) % n;
                var c = roofs[k] - r.H[i];
                clear[i] = clear.TryGetValue(i, out var was) ? Math.Min(was, c) : c;
            }
        double Lowest(int i) => StripHalf + StripClear;
        double Highest(int i) => Math.Max(Lowest(i), clear[i] - StripHalf - StripClear);
        // the runs of tunnels: points one after another
        var raw = new Dictionary<int, double>();
        var chainOf = new Dictionary<int, int>();
        var chains = 0;
        foreach (var i0 in clear.Keys.OrderBy(i => i))
        {
            if (chainOf.ContainsKey(i0)) continue;
            var start = i0;
            for (var back = ((start - 1) % n + n) % n; clear.ContainsKey(back) && back != i0; back = ((back - 1) % n + n) % n) start = back;
            var chain = new List<int>();
            for (var k = start; clear.ContainsKey(k) && !chainOf.ContainsKey(k); k = (k + 1) % n) { chain.Add(k); chainOf[k] = chains; }
            chains++;
            var halves = chain.Select(i => clear[i] / 2).OrderBy(v => v).ToList();
            var level = halves[halves.Count / 2];
            foreach (var i in chain) raw[i] = Math.Min(level, Highest(i));
        }
        var reach = Math.Max(1, (int)Math.Round(StripEase / o.Spacing));
        var strips = new Dictionary<int, double>();
        foreach (var i in clear.Keys)
        {
            double sum = 0, weight = 0;
            for (var d = -reach; d <= reach; d++)
                if (raw.TryGetValue(((i + d) % n + n) % n, out var v) && chainOf[((i + d) % n + n) % n] == chainOf[i]) { var w = reach + 1 - Math.Abs(d); sum += v * w; weight += w; }
            strips[i] = Math.Max(Lowest(i), Math.Min(sum / weight, Highest(i)));
        }
        return strips;
    }

    // A piece's walls, light strips and roof, points k0..k1 of the tunnel from `from`: each a surface through the points' own places --
    // the walls straight up from under the deck to the roof's top beside the rails, one colour; a strip along each wall's inside at the
    // tunnel's middle (StripHeights); the roof over the road from wall to wall -- so they follow the road's slope and bends without a step
    // or a joint. (Until 2026-10-16 each segment was a level box of its own: down a ramp the strips went down in steps, and each box's
    // ends showed, light and dark, along the walls.) The roof's surfaces, and the strips' tops and bottoms, are in triangles: across a
    // sloping bend their four corners aren't in one plane.
    private static void Surfaces(List<Vector3> mesh, List<Face> faces, IslandFile island, TrackRoad r, int from, int k0, int k1, TunnelRun run,
                                 double[] roofs, Dictionary<int, double> strips, Vector3 origin, RaceTrackOptions o)
    {
        var n = r.Count;
        var count = k1 - k0 + 1;
        var at = new Vector3[count]; var across = new Vector3[count]; var half = new float[count]; var roof = new float[count]; var strip = new float[count];
        for (var c = 0; c < count; c++)
        {
            var i = (from + k0 + c) % n;
            at[c] = PointAt(r, i, origin);
            across[c] = Vector3.Normalize(new Vector3((float)-r.Tz[i], 0, (float)r.Tx[i]));
            half[c] = (float)((HalfAt(r, i, o) + WallOut) * 512);
            roof[c] = (float)(roofs[k0 + c] - origin.Y);
            strip[c] = at[c].Y + (float)(strips.TryGetValue(i, out var s) ? s : StripHigh);
        }
        int[] Line(Func<int, Vector3> place) { var l = new int[count]; for (var c = 0; c < count; c++) { mesh.Add(place(c)); l[c] = mesh.Count - 1; } return l; }
        void Add(int[] pts, int colour, Vector3 outward)
        {
            var nrm = Vector3.Cross(mesh[pts[1]] - mesh[pts[0]], mesh[pts[2]] - mesh[pts[0]]);
            if (nrm.LengthSquared() < 1e-6f) return;
            faces.Add(Vector3.Dot(nrm, outward) >= 0 ? new Face(pts, colour) : new Face(pts.Reverse().ToArray(), colour));
        }
        // a surface between two lines along the points, facing `outward` at each segment's start; flat ones in quads, the others in triangles
        void Between(int[] a, int[] b, int colour, Func<int, Vector3> outward, bool flat)
        {
            for (var c = 0; c + 1 < count; c++)
            {
                if (flat) { Add(new[] { a[c], a[c + 1], b[c + 1], b[c] }, colour, outward(c)); continue; }
                Add(new[] { a[c], a[c + 1], b[c + 1] }, colour, outward(c));
                Add(new[] { a[c], b[c + 1], b[c] }, colour, outward(c));
            }
        }
        var up = Vector3.UnitY;
        var eave = (float)(run.Eave * 512);
        foreach (var side in new[] { -1f, 1f })
        {
            Vector3 Off(int c, float by, float y) => new Vector3(at[c].X, 0, at[c].Z) + across[c] * side * by + new Vector3(0, y, 0);
            Vector3 Inward(int c) => -across[c] * side;
            Vector3 Outward(int c) => across[c] * side;
            // the wall: its inside and its outside, from under the deck to the roof's top
            var ib = Line(c => Off(c, half[c], at[c].Y - (float)Under)); var it = Line(c => Off(c, half[c], roof[c] + (float)Slab));
            var ob = Line(c => Off(c, half[c] + (float)Wall, at[c].Y - (float)Under)); var ot = Line(c => Off(c, half[c] + (float)Wall, roof[c] + (float)Slab));
            Between(ib, it, WallLight, Inward, flat: true);
            Between(ob, ot, WallLight, Outward, flat: true);
            // its light strip, StripOut out from its inside
            var fb = Line(c => Off(c, half[c] - (float)StripOut, strip[c] - (float)StripHalf)); var ft = Line(c => Off(c, half[c] - (float)StripOut, strip[c] + (float)StripHalf));
            var bb = Line(c => Off(c, half[c], strip[c] - (float)StripHalf)); var bt = Line(c => Off(c, half[c], strip[c] + (float)StripHalf));
            Between(fb, ft, StripColour, Inward, flat: true);
            Between(bt, ft, StripColour, _ => up, flat: false);
            Between(bb, fb, StripColour, _ => -up, flat: false);
            // the wall's ends, where the tunnel ends
            foreach (var (c, isEnd) in new[] { (0, k0 == 0), (count - 1, k1 == ((run.To - from) % n + n) % n) })
            {
                if (!isEnd || count < 2) continue;
                var along = Vector3.Normalize(new Vector3(at[c == 0 ? 1 : c].X - at[c == 0 ? 0 : c - 1].X, 0, at[c == 0 ? 1 : c].Z - at[c == 0 ? 0 : c - 1].Z)) * (c == 0 ? -1f : 1f);
                Add(new[] { ib[c], it[c], ot[c], ob[c] }, WallLight, along);
            }
        }
        // the roof: its underside, its top and its edges, from wall to wall and Eave past them
        Vector3 R(int c, float by, float y) => new Vector3(at[c].X, 0, at[c].Z) + across[c] * by + new Vector3(0, y, 0);
        float W(int c) => half[c] + (float)Wall + eave;
        var ul = Line(c => R(c, -W(c), roof[c])); var ur = Line(c => R(c, W(c), roof[c]));
        var tl = Line(c => R(c, -W(c), roof[c] + (float)Slab)); var tr = Line(c => R(c, W(c), roof[c] + (float)Slab));
        Between(ul, ur, RoofLight, _ => -up, flat: false);
        Between(tl, tr, RoofTop, _ => up, flat: false);
        Between(ul, tl, RoofDark, c => -across[c], flat: true);
        Between(ur, tr, RoofDark, c => across[c], flat: true);
        // (and its ends, where the tunnel ends)
        foreach (var (c, isEnd) in new[] { (0, k0 == 0), (count - 1, k1 == ((run.To - from) % n + n) % n) })
        {
            if (!isEnd || count < 2) continue;
            var along = Vector3.Normalize(new Vector3(at[c == 0 ? 1 : c].X - at[c == 0 ? 0 : c - 1].X, 0, at[c == 0 ? 1 : c].Z - at[c == 0 ? 0 : c - 1].Z)) * (c == 0 ? -1f : 1f);
            Add(new[] { ul[c], ur[c], tr[c], tl[c] }, RoofDark, along);
        }
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
                // (nor past an end where the road goes on under the island, `under`: the ground there is over it -- Island CX's door A's
                // portal cut the plateau behind it 4 cells deep, and the road went into flat ground before the cliff, 2026-10-15)
                if (t < -0.01 && r.Under?[a] == true || t > 1.01 && r.Under?[b] == true) continue;
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

    // The island's ground over a tunnel at point i, its least across the tunnel from wall to wall (null: none there).
    private static double? GroundOver(IslandFile island, TrackRoad r, int i, RaceTrackOptions o)
    {
        var reach = HalfAt(r, i, o) + WallOut + Wall / 512;
        double? least = null;
        for (var s = -1.0; s <= 1.0001; s += 0.25)
            if (IslandOps.Altitude(island, (r.X[i] - r.Tz[i] * reach * s) * 512, (r.Z[i] + r.Tx[i] * reach * s) * 512) is { } g)
                least = least is { } l ? Math.Min(l, g) : g;
        return least;
    }
}
