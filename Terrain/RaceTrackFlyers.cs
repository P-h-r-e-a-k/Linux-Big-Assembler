namespace LBAAssembler.Terrain;

// Flying gas monsters over a stretch of the lap (RaceTrackPlan.Flyers; RACEMOD.CPP flyer=): Island CX's, in the hall under its gas -- the
// user, 2026-10-15: "Naturally the inside has two types of gas monster, one that flies around and shoots red fire balls, and the other one
// that you've already implemented". The game's own (entity 329: it rises out of the gas in Island CX's room under the emperor's, scene 181,
// and spits red fire balls at Twinsen), flown by the race-track mode to and fro along a way beside the road, over the hall's floor, stopping
// now and then to spit a fire ball at a car below it. One actor each.
internal sealed class FlyerRun
{
    public int From { get; set; }
    public int To { get; set; }
    // how far beside the road's middle it flies (cells; plus: the road's left, as it runs; minus: its right), how high over the floor,
    // and the floor's height
    public double Out { get; set; } = 5.5;
    public double Height { get; set; } = 2600;
    public double Floor { get; set; }
}

// A monster as placed: its way (island cells), flown to and fro, and its height.
internal sealed record FlyerSpot(IReadOnlyList<(double X, double Z)> Way, double Y);

internal static class RaceTrackFlyers
{
    public const int Entity = 329;
    // cells between its way's points
    private const double Step = 2;
    // what stands in its way: anything over the floor reaching up to within this of its height (the hall's pillars and walls, not the road's
    // decks under it)
    private const double Below = 1500, Above = 1500;

    public static FlyerSpot? Place(IslandFile island, TrackRoad r, int from, int to, FlyerRun run, RaceTrackOptions o,
                                   IReadOnlyDictionary<int, double>? tops = null)
    {
        var n = r.Count;
        var span = ((to - from) % n + n) % n + 1;
        var per = Math.Max(1, (int)Math.Round(Step / o.Spacing));
        var y = run.Floor + run.Height;
        var boxes = IslandOps.CubeCells(island).SelectMany(c => c.Item3.Decors.Select(d =>
            (X0: (c.Item1 * (double)IslandFile.CubeSize + d.XMin) / 512, Z0: (c.Item2 * (double)IslandFile.CubeSize + d.ZMin) / 512,
             X1: (c.Item1 * (double)IslandFile.CubeSize + d.XMax) / 512, Z1: (c.Item2 * (double)IslandFile.CubeSize + d.ZMax) / 512, Y0: (double)d.YMin, Y1: (double)d.YMax)))
            .Where(b => b.Y1 > b.Y0 && b.Y1 > y - Below && b.Y0 < y + Above).ToList();
        // (nor the road itself where it passes about as high -- a ramp down into the hall or up out of it: the decks' boxes are ones nothing
        // touches, with no top, so the boxes above don't see them, and two of Island CX's monsters flew through its ramps, 2026-10-16)
        double HalfAt(int i) => r.Raised is { } up && up[i] ? r.RaisedHalfs is { } halfs && i < halfs.Length ? halfs[i] : o.RaisedHalfWidth : r.CurbHalf;
        bool RoadClear(double x, double z)
        {
            for (var k = 0; k < n; k++)
            {
                // (what is at the road there, from under its deck up to a car's roof or a tunnel's: `tops`, over the deck; the monster's
                // own reach, from a little under its height to over its back)
                var top = r.H[k] + (tops != null && tops.TryGetValue(k, out var t) ? t : RoadCar);
                if (r.H[k] - RoadUnderDeck > y + FlyerOver || top < y - FlyerUnder) continue;
                var room = HalfAt(k) + RoadRoom;
                if ((r.X[k] - x) * (r.X[k] - x) + (r.Z[k] - z) * (r.Z[k] - z) < room * room) return false;
            }
            return true;
        }
        bool Clear(double x, double z) => !boxes.Any(b => x > b.X0 - 1 && x < b.X1 + 1 && z > b.Z0 - 1 && z < b.Z1 + 1) && RoadClear(x, z);
        var points = new List<(double X, double Z)>();
        for (var j = 0; j < span; j += per)
        {
            var k = (from + j) % n;
            points.Add((r.X[k] - r.Tz[k] * run.Out, r.Z[k] + r.Tx[k] * run.Out));
        }
        // (its way the longest run of clear points, each clear of the next half way there too: a point left out in the middle had it fly
        // straight across what stood there)
        List<(double X, double Z)> way = new(), at = new();
        for (var q = 0; q < points.Count; q++)
        {
            var ok = Clear(points[q].X, points[q].Z) && (at.Count == 0 || Clear((at[^1].X + points[q].X) / 2, (at[^1].Z + points[q].Z) / 2));
            if (!ok) { if (at.Count > way.Count) way = at; at = new(); if (!Clear(points[q].X, points[q].Z)) continue; }
            at.Add(points[q]);
        }
        if (at.Count > way.Count) way = at;
        return way.Count >= 2 ? new FlyerSpot(way, y) : null;
    }

    // the road in its way: its middle within its half width and RoadRoom cells (the monster, entity 329's body 451, is 760 wide, flying
    // along its way: half that and a margin), and what is at it -- RoadUnderDeck under the deck (its
    // underside, a tunnel's walls) up to RoadCar over it (a car), or a tunnel's roof -- within the monster's reach, FlyerUnder under its
    // height (it bobs) to FlyerOver over it (its back)
    private const double RoadRoom = 1.5, RoadUnderDeck = 600, RoadCar = 1200, FlyerUnder = 300, FlyerOver = 1600;
}
