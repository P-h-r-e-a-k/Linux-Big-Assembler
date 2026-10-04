using System.Numerics;

namespace LBAAssembler.Terrain;

// A vertical loop's ring (RaceTrackPlan.Loops): a band of road bent into a circle standing on the road, in the plane of the lap's way
// there, like a rollercoaster's loop -- its road surface on the inside, red and white rails along its edges, green outside it, and two
// green legs carrying it at its sides. Over the turn it winds across the road by `shift` (in on the left of the lap's middle, out on
// the right), so a car coming out of it drives on past its foot. The engine's race-track mode carries the car round it (RACEMOD.CPP
// RaceMod_Loop): the ring itself carries nothing, and its decor's box touches nothing (RaceTrackBuilder.PlaceLoops). A ring with a gap
// at its top (`gap`, radians): the car is carried over it upside down.
internal static class RaceTrackLoopBody
{
    // the palette ramps every island palette shares with the game's: greys 48-63, reds 64-79, greens 128-143
    public const int Surface = 53, RailRed = 70, RailWhite = 62, RailInner = 57, Green = 138, GreenDark = 135, GreenSide = 136, LegLight = 137, LegDark = 134;
    // the band's thickness behind its road, its rails' height and width (world units)
    public const double Thickness = 160, Rail = 150, RailWidth = 90, LegHalf = 110;
    public const int Segments = 48;

    // The ring in world axes, from its foot on the road (the decor's origin): `along`, the lap's way there (a level unit vector),
    // `radius` from the ring's middle to its road surface, `half` from the band's middle to its rails. In Parts bodies, a quarter of the
    // ring each (its road in strips, it is more points than a body may have -- the engine's 550), all of them at the same origin.
    public const int Parts = 4;

    public static List<byte[]> Ring(Vector3 along, double radius, double shift, double half, double gap) =>
        Enumerable.Range(0, Parts).Select(q => RingPart(along, radius, shift, half, gap, 2 * Math.PI * q / Parts,
            // (each quarter a little into the next: two bodies only meeting leave a crack of a pixel between them)
            2 * Math.PI * (q + 1) / Parts + (q + 1 < Parts ? RaceTrackRaisedBody.PieceOverlap / radius : 0), q)).ToList();

    private static byte[] RingPart(Vector3 along, double radius, double shift, double half, double gap, double from, double to, int part)
    {
        var m = new RaceTrackRaisedBody.Mesh();
        along = Vector3.Normalize(new Vector3(along.X, 0, along.Z));
        var across = new Vector3(-along.Z, 0, along.X);
        var up = Vector3.UnitY;
        float R = (float)radius;
        // across each section: (offset across, height over the road surface towards the ring's middle)
        var edge = half + RailWidth;
        var profile = new (double U, double H)[] { (-edge, -Thickness), (-edge, Rail), (-half, Rail), (-half, 0), (half, 0), (half, Rail), (edge, Rail), (edge, -Thickness) };
        Vector3 Middle(double t) => along * (float)(radius * Math.Sin(t)) + up * (float)(radius * (1 - Math.Cos(t))) + across * (float)(-shift / 2 + shift * t / (2 * Math.PI));
        Vector3 Inward(double t) => -along * (float)Math.Sin(t) + up * (float)Math.Cos(t);
        bool InGap(double t) => gap > 0 && t > Math.PI - gap / 2 + 1e-9 && t < Math.PI + gap / 2 - 1e-9;
        // the sections: every 360/Segments degrees, and the gap's edges
        var angles = Enumerable.Range(0, Segments + 1).Select(i => 2 * Math.PI * i / Segments).ToList();
        if (gap > 0) angles.AddRange(new[] { Math.PI - gap / 2, Math.PI + gap / 2 });
        angles = angles.Distinct().Where(t => t >= from - 1e-9 && t <= to + 1e-9).Append(to).Distinct().OrderBy(t => t).ToList();
        var at = new List<int[]>();
        // (the road and the ring's outside in strips no wider than a cell: the engine leaves out a polygon with a point behind the camera's
        // near plane, RaceTrackRaisedBody.StripWidth)
        var strips = Math.Max(1, (int)Math.Ceiling(2 * half / RaceTrackRaisedBody.StripWidth));
        var road = new List<int[]>(); var shell = new List<int[]>();
        foreach (var t in angles)
        {
            var c = Middle(t); var n = Inward(t);
            at.Add(profile.Select(p => m.P(c + across * (float)p.U + n * (float)p.H)).ToArray());
            road.Add(Enumerable.Range(1, strips - 1).Select(i => m.P(c + across * (float)(-half + 2 * half * i / strips))).ToArray());
            shell.Add(Enumerable.Range(1, strips - 1).Select(i => m.P(c + across * (float)(edge - 2 * edge * i / strips) - n * (float)Thickness)).ToArray());
        }
        for (var j = 0; j + 1 < angles.Count; j++)
        {
            var mid = (angles[j] + angles[j + 1]) / 2;
            if (InGap(mid)) continue;
            var inward = Inward(mid);
            var block = j % 2 == 0 ? RailRed : RailWhite;
            void Strip(int k, int colour, Vector3 outward) => m.Quad(at[j][k], at[j][k + 1], at[j + 1][k + 1], at[j + 1][k], colour, outward);
            Strip(0, GreenSide, -across);       // the left side, band and rail
            Strip(1, block, inward);            // the rail's top
            Strip(2, RailInner, across);        // its inner face
            for (var i = 0; i < strips; i++)          // the road
            {
                int A(int s) => i == 0 ? at[s][3] : road[s][i - 1];
                int B(int s) => i == strips - 1 ? at[s][4] : road[s][i];
                m.Quad(A(j), B(j), B(j + 1), A(j + 1), Surface, inward);
            }
            Strip(4, RailInner, -across);
            Strip(5, block, inward);
            Strip(6, GreenSide, across);
            for (var i = 0; i < strips; i++)          // the outside of the ring
            {
                int A(int s) => i == 0 ? at[s][7] : shell[s][i - 1];
                int B(int s) => i == strips - 1 ? at[s][0] : shell[s][i];
                m.Quad(A(j), B(j), B(j + 1), A(j + 1), Green, -inward);
            }
        }
        // the band's ends at the gap: its cross-section, three quads
        if (gap > 0)
            foreach (var (t, way) in new[] { (Math.PI - gap / 2, 1f), (Math.PI + gap / 2, -1f) })
            {
                if (!angles.Contains(t)) continue;
                var s = at[angles.IndexOf(t)];
                var tangent = along * (float)Math.Cos(t) + up * (float)Math.Sin(t);
                var outward = tangent * way;
                m.Quad(s[0], s[1], s[2], s[3], GreenDark, outward);
                m.Quad(s[3], s[4], s[7], s[0], GreenDark, outward);
                m.Quad(s[4], s[5], s[6], s[7], GreenDark, outward);
            }
        // the legs: from under the road up to the ring's outside at its widest, ahead of its foot and behind it
        foreach (var t in new[] { Math.PI / 2, 3 * Math.PI / 2 })
        {
            if (Math.Min(Parts - 1, (int)Math.Floor(t / (2 * Math.PI) * Parts + 1e-9)) != part) continue;
            var outside = -Inward(t);
            var top = Middle(t) + outside * (float)(Thickness + LegHalf);
            var foot = new Vector3(top.X, -200, top.Z);
            Column(m, foot, top.Y + (float)LegHalf, along, across);
        }
        return m.Write();
    }

    private static void Column(RaceTrackRaisedBody.Mesh m, Vector3 foot, float topY, Vector3 along, Vector3 across)
    {
        var u = along * (float)LegHalf; var v = across * (float)LegHalf;
        var corners = new[] { foot - u - v, foot + u - v, foot + u + v, foot - u + v };
        var lo = corners.Select(p => m.P(p)).ToArray();
        var hi = corners.Select(p => m.P(new Vector3(p.X, topY, p.Z))).ToArray();
        for (var k = 0; k < 4; k++)
        {
            var next = (k + 1) % 4;
            m.Quad(lo[k], lo[next], hi[next], hi[k], k % 2 == 0 ? LegLight : LegDark, (corners[k] + corners[next]) / 2 - foot);
        }
        m.Quad(hi[0], hi[1], hi[2], hi[3], LegLight, Vector3.UnitY);
    }
}
