using System.IO;

namespace LBAAssembler.Terrain;

// A giant hoop the cars drive through (RaceTrackPlan.Hoops): Otringal's, from the little arena south of its town where dogs do tricks and
// jump through a hoop -- the user, 2026-10-09: "let's scale up the hoop so it's a massive hoop for the cars to drive through". The hoop's own
// pieces (island bodies: its base with the bottom of the ring, its sides, its top, all at one origin, the ring standing across the body's z
// axis) made Scale times as big, standing where the lap passes nearest the hoop's place, the ring square across the road and its bottom just
// under the deck -- the lap through it. Their boxes touch nothing.
internal sealed class HoopRun
{
    // where the hoop stood (the plan's cells), its pieces (the island's bodies), how much bigger, the top of its bottom's ring over its foot;
    // how far along the lap from there it may move to stand on a straight (cells), and how much straight it wants either side of its ring
    public double[] At { get; set; } = Array.Empty<double>();
    public int[] Bodies { get; set; } = Array.Empty<int>();
    public double Scale { get; set; } = 4.5;
    public double Bottom { get; set; } = 812;
    public double Thick { get; set; } = 250;
    public double Within { get; set; } = 12;
    public double Straight { get; set; } = 6;
}

internal static class RaceTrackHoop
{
    private const int NoBoxTop = -32000;

    public static void Place(IslandFile island, TrackRoad r, HoopRun run, double x, double z, RaceTrackOptions o, RaceTrackReport report)
    {
        if (o.SceneryObl is not { } oblPath || !File.Exists(oblPath) || o.NewBodyBase < 0 || run.Bodies.Length == 0) { report.Notes.Add("WARNING: a hoop: no island objects to make it of"); return; }
        // (the lap's straightest point within Within of where it stood -- on a bend the chase camera, swinging wide, flew through its side --
        // and the deck's lowest across the ring's thickness there)
        var thick = run.Thick * run.Scale / 512;
        var reach = Math.Max(2, (int)Math.Ceiling((thick + run.Straight) / o.Spacing));
        double Turn(int i)
        {
            int a = (i - reach + r.Count) % r.Count, b = (i + reach) % r.Count;
            return Math.Acos(Math.Clamp(r.Tx[a] * r.Tx[b] + r.Tz[a] * r.Tz[b], -1, 1));
        }
        double Far(int i) => Math.Sqrt((r.X[i] - x) * (r.X[i] - x) + (r.Z[i] - z) * (r.Z[i] - z));
        var nearest = Enumerable.Range(0, r.Count).OrderBy(Far).First();
        var stretch = (int)Math.Ceiling(run.Within / o.Spacing);
        var k = Enumerable.Range(-stretch, 2 * stretch + 1).Select(d => (nearest + d + r.Count) % r.Count).Where(i => Far(i) <= run.Within && !r.Gap[i])
            .DefaultIfEmpty(nearest).OrderBy(i => Turn(i) * 180 / Math.PI + Far(i) * 0.5).First();
        double tx = r.Tx[k], tz = r.Tz[k];
        var lowest = r.H[k];
        for (var i = 0; i < r.Count; i++)
        {
            var along = (r.X[i] - r.X[k]) * tx + (r.Z[i] - r.Z[k]) * tz;
            var across = -(r.X[i] - r.X[k]) * tz + (r.Z[i] - r.Z[k]) * tx;
            if (Math.Abs(along) <= thick + 0.5 && Math.Abs(across) < 1) lowest = Math.Min(lowest, r.H[i]);
        }
        var foot = lowest - run.Bottom * run.Scale - 120;
        var beta = ((int)Math.Round(Math.Atan2(tx, tz) * 4096 / (2 * Math.PI)) % 4096 + 4096) % 4096;
        if (IslandDecors.Locate(island, r.X[k] * 512, r.Z[k] * 512) is not { } at || at.Cube.Decors.Count + run.Bodies.Length > IslandDecors.MaxPerCube)
        { report.Notes.Add($"WARNING: the hoop at ({x:0.#}, {z:0.#}): no room for it"); return; }
        var obl = HqrArchive.Open(oblPath);
        var clipped = 0;
        var ext = (int)Math.Round(1000 * run.Scale);
        foreach (var body in run.Bodies)
        {
            var d = IslandDecors.Blank(o.NewBodyBase + report.NewBodies.Count, at.X, (int)Math.Round(foot), at.Z, beta);
            d.XMin = at.X - ext; d.XMax = at.X + ext; d.ZMin = at.Z - ext; d.ZMax = at.Z + ext; d.YMin = (int)Math.Round(foot); d.YMax = NoBoxTop;
            at.Cube.Decors.Add(d);
            report.NewBodies.Add(IslandScaler.ScaledBody(obl.Read(body), run.Scale, ref clipped));
        }
        report.Notes.Add($"a hoop {run.Scale:0.#} times its size (bodies {string.Join(", ", run.Bodies)}) across the lap at cell ({r.X[k]:0.0}, {r.Z[k]:0.0}) ({Far(k):0.#} cells from where it stood, the lap turning {Turn(k) * 180 / Math.PI:0} degrees through it), " +
                         $"its foot at {foot:0}, the deck {lowest:0} through it{(clipped > 0 ? $" ({clipped} values clipped)" : "")}");
    }
}
