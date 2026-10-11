namespace LBAAssembler.Terrain;

// A machine gun on a knoll inside a corner (RaceTrackPlan.Gunners, RACEMOD.CPP gunner=): Otringal's Franco gun by the corner past the
// start (the user, 2026-10-09: "a left over machine gun ... let's move it to the high ground that's just above the track at that corner,
// and add in a franco to fire in bursts so that the bullets only land on the inside of the corner so they can be avoided by driving the
// corner on the outside" -- "The corner with the higher ground on the inside close to where the gun originally was"). Knoll: the
// knoll's middle in the plan's cells; From and To: the corner, the plan's points; Rise: the knoll's top over the corner's highest deck.
internal sealed class GunnerRun
{
    public double[] Knoll { get; set; } = System.Array.Empty<double>();
    public int From { get; set; }
    public int To { get; set; }
    public double Rise { get; set; } = 500;
    // the knoll: its flat top's radius and where its sides meet the ground (cells from its middle)
    public double TopRadius { get; set; } = 2.2;
    public double FootRadius { get; set; } = 4.2;
}

// The knoll built and where the gun fires: the knoll's middle (cells) and top (world units), the point it faces (cells: the middle of
// where it fires), and the places its bullets land -- the inner half of the corner, each a point (world units) and a radius round it.
internal sealed record GunnerSpot(double X, double Z, double Top, double FaceX, double FaceZ, List<(double X, double Z, double Y, double R)> Targets);

internal static class RaceTrackGunner
{
    // (the bullets land between these shares of the road's half width from its middle towards the knoll: the inner half, its kerb kept)
    private const double InnerFrom = 0.25, InnerTo = 0.9;
    private const int MaxTargets = 24;

    // The knoll raised (the ground within FootRadius of its middle brought up towards Top, never down, and never under the road) and the
    // corner's inner half as the gun's targets: the road's points From..To, each moved across towards the knoll.
    public static GunnerSpot Place(IslandFile island, TrackRoad r, int from, int to, GunnerRun run, double kx, double kz, RaceTrackOptions o,
        RaceTrackReport report)
    {
        var n = r.Count;
        var span = ((to - from) % n + n) % n + 1;
        double highest = double.MinValue;
        for (var k = 0; k < span; k++) highest = Math.Max(highest, r.H[(from + k) % n]);
        var top = highest + run.Rise;
        // (the road's reach: no ground raised within its rail and a cell past it)
        bool UnderRoad(double gx, double gz)
        {
            for (var i = 0; i < n; i++)
            {
                var reach = HalfAt(r, i, o) + 1;
                if ((r.X[i] - gx) * (r.X[i] - gx) + (r.Z[i] - gz) * (r.Z[i] - gz) < reach * reach) return true;
            }
            return false;
        }
        var raised = 0;
        var foot = run.FootRadius;
        for (var gz = (int)Math.Floor(kz - foot); gz <= (int)Math.Ceiling(kz + foot); gz++)
        for (var gx = (int)Math.Floor(kx - foot); gx <= (int)Math.Ceiling(kx + foot); gx++)
        {
            var d = Math.Sqrt((gx - kx) * (gx - kx) + (gz - kz) * (gz - kz));
            if (d >= foot || island.HeightAt(gx, gz) is not { } h || UnderRoad(gx, gz)) continue;
            // (flat on top, then down its sides to the ground, steepest half way)
            var share = d <= run.TopRadius ? 1 : (foot - d) / (foot - run.TopRadius);
            share = share * share * (3 - 2 * share);
            var to_ = h + (top - h) * share;
            if (to_ <= h) continue;
            island.SetHeight(gx, gz, (int)Math.Round(to_));
            raised++;
        }
        // the targets: the corner's points, each moved towards the knoll into the road's inner half
        var targets = new List<(double X, double Z, double Y, double R)>();
        var step = Math.Max(1, span / MaxTargets);
        double fx = 0, fz = 0;
        for (var k = 0; k < span; k += step)
        {
            int i = (from + k) % n, j = (i + 1) % n;
            double tx = r.X[j] - r.X[i], tz = r.Z[j] - r.Z[i], len = Math.Sqrt(tx * tx + tz * tz);
            if (len < 1e-9) continue;
            double nx = -tz / len, nz = tx / len;
            if (nx * (kx - r.X[i]) + nz * (kz - r.Z[i]) < 0) { nx = -nx; nz = -nz; }
            var half = HalfAt(r, i, o);
            var across = half * (InnerFrom + InnerTo) / 2;
            double x = r.X[i] + nx * across, z = r.Z[i] + nz * across;
            targets.Add((x * 512, z * 512, r.H[i], half * (InnerTo - InnerFrom) / 2 * 512));
            fx += x; fz += z;
        }
        if (targets.Count > 0) { fx /= targets.Count; fz /= targets.Count; }
        report.Notes.Add($"a machine gun on a knoll at ({kx:0.#}, {kz:0.#}), its top at {top:0} ({raised} points of the ground raised), firing onto the inner half of the corner (the lap's points {from}-{to}, {targets.Count} places)");
        return new GunnerSpot(kx, kz, top, fx, fz, targets);
    }

    private static double HalfAt(TrackRoad r, int i, RaceTrackOptions o) =>
        r.Raised is { } up && up[i] ? r.RaisedHalfs is { } halfs && i < halfs.Length ? halfs[i] : o.RaisedHalfWidth : r.CurbHalf;
}
