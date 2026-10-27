namespace LBAAssembler.Terrain.Temple;

// The race track's raised road over the temple, as its plan has it (RaceTrackPlan: Points every half cell, Heights its surface, RaisedHalfs
// rail to rail, RaisedStrips the slip lane beside it): for what the temple's models must keep out of. The cuts (TempleIsland.Carve, the
// plan's TempleCuts) take the cells that reach into the road's room, but a model may reach past its cells' tops: a rock's lobes stand as
// tall as LBA1's picture of the row draws them over their share of it, up to 0.4 layers over the block (2026-10-18, the user: "something
// visually clipping into the track" -- a lobe of a row of rocks the crypt's road passes 60 over, 247 over the deck's surface at its kerb).
internal sealed class TempleDeck
{
    // what the deck passes over keeps this far under its surface (world units: the cuts' own rule, temple_design.py), and the room over the
    // surface the road keeps clear (a car's height and the chase camera's: TempleActors.DeckOver)
    public const double Clear = 40, Room = 2600;
    // past the rail: the rail's own width, and a little for the deck's sides' rounding (cells)
    private const double Edge = 0.15;
    // the footprints are looked at every quarter cell
    private const double Step = 0.25;

    private readonly record struct Seg(double Ax, double Az, double Bx, double Bz, double Ya, double Yb, double Half, double Lo, double Hi);
    private readonly Dictionary<(int X, int Z), List<Seg>> grid = new();

    private TempleDeck(RaceTrackPlan plan)
    {
        var pts = plan.Points; var hs = plan.Heights!;
        var n = pts.Length;
        double Half(int i) => plan.RaisedHalfs is { } halfs && i < halfs.Length ? halfs[i] : plan.RaisedHalf ?? 4.5;
        (double Lo, double Hi)? Strip(int i) => plan.RaisedStrips is { } strips && i < strips.Length && strips[i] is { Length: 2 } s && s[1] > s[0] ? (s[0], s[1]) : null;
        for (var i = 0; i < n; i++)
        {
            // (the lap closes on its first point)
            var j = i + 1 < n ? i + 1 : 0;
            if (j == i) continue;
            double ax = plan.OriginCellX + pts[i][0], az = plan.OriginCellZ + pts[i][1], bx = plan.OriginCellX + pts[j][0], bz = plan.OriginCellZ + pts[j][1];
            var len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            if (len < 1e-6 || len > 2) continue;
            var (sa, sb) = (Strip(i), Strip(j));
            var strip = sa is { } p ? sb is { } q ? (Math.Min(p.Lo, q.Lo), Math.Max(p.Hi, q.Hi)) : p : sb ?? (0, 0);
            Add(new Seg(ax, az, bx, bz, hs[i], hs[j], Math.Max(Half(i), Half(j)) + Edge, strip.Item1 - Edge, strip.Item2 + Edge));
        }
        // (and the branches' roads -- the hidden passage's shortcut: its spur and its way out; not their gaps, where there is no road)
        foreach (var b in plan.Branches ?? Array.Empty<BranchRun>())
            for (var i = 0; i + 1 < b.Points.Length && i + 1 < b.Heights.Length; i++)
            {
                double BHalf(int k) => b.Halfs is { } halfs && k < halfs.Length ? halfs[k] : plan.RaisedHalf ?? 4.5;
                if (BHalf(i) <= 0 || BHalf(i + 1) <= 0) continue;
                double ax = plan.OriginCellX + b.Points[i][0], az = plan.OriginCellZ + b.Points[i][1], bx = plan.OriginCellX + b.Points[i + 1][0], bz = plan.OriginCellZ + b.Points[i + 1][1];
                var len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                if (len < 1e-6 || len > 2) continue;
                Add(new Seg(ax, az, bx, bz, b.Heights[i], b.Heights[i + 1], Math.Max(BHalf(i), BHalf(i + 1)) + Edge, 0, 0));
            }
    }

    private void Add(Seg seg)
    {
        var reach = Math.Max(seg.Half, Math.Max(Math.Abs(seg.Lo), Math.Abs(seg.Hi))) + 0.5;
        for (var x = (int)Math.Floor(Math.Min(seg.Ax, seg.Bx) - reach); x <= (int)Math.Floor(Math.Max(seg.Ax, seg.Bx) + reach); x++)
            for (var z = (int)Math.Floor(Math.Min(seg.Az, seg.Bz) - reach); z <= (int)Math.Floor(Math.Max(seg.Az, seg.Bz) + reach); z++)
            {
                if (!grid.TryGetValue((x, z), out var list)) grid[(x, z)] = list = new List<Seg>();
                list.Add(seg);
            }
    }

    // The plan's deck, or null: a plan without heights point by point has no raised road to keep out of.
    public static TempleDeck? Of(RaceTrackPlan? plan) => plan?.Heights is { } hs && hs.Length == plan.Points.Length && hs.Length > 1 ? new TempleDeck(plan) : null;

    // The highest a thing over an island footprint (cells) from `bottom` to `top` (world units) may reach without coming into the road's
    // room: Clear under the lowest of the deck's surfaces over the footprint that the thing reaches near (from Clear under it to Room over
    // it), or null where it reaches near none.
    public double? Cap(double x0, double x1, double z0, double z1, double bottom, double top)
    {
        double? cap = null;
        int nx = Math.Max(1, (int)Math.Ceiling((x1 - x0) / Step)), nz = Math.Max(1, (int)Math.Ceiling((z1 - z0) / Step));
        for (var i = 0; i <= nx; i++)
            for (var k = 0; k <= nz; k++)
            {
                double x = x0 + (x1 - x0) * i / nx, z = z0 + (z1 - z0) * k / nz;
                if (!grid.TryGetValue(((int)Math.Floor(x), (int)Math.Floor(z)), out var list)) continue;
                foreach (var s in list)
                {
                    double dx = s.Bx - s.Ax, dz = s.Bz - s.Az, len = Math.Sqrt(dx * dx + dz * dz);
                    dx /= len; dz /= len;
                    double qx = x - s.Ax, qz = z - s.Az, along = qx * dx + qz * dz;
                    if (along < -Step || along > len + Step) continue;
                    var across = -qx * dz + qz * dx;
                    if (Math.Abs(across) > s.Half && (across < s.Lo || across > s.Hi)) continue;
                    var surface = s.Ya + (s.Yb - s.Ya) * Math.Clamp(along / len, 0, 1);
                    if (top <= surface - Clear || bottom >= surface + Room) continue;
                    var y = surface - Clear;
                    if (cap is null || y < cap) cap = y;
                }
            }
        return cap;
    }
}
