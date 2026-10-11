using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// A giant roulette wheel the lap runs through (RaceTrackPlan.Roulette): Otringal's, floating over where its casino stood -- the user,
// 2026-10-09: "let's have the jump land into a giant rotating roulette wheel complete with colours, numbers, and a giant white ball that
// rolls around for cars to avoid. Cars land in the roulette wheel from the jump, and drive around in a circle and out a hole in the bottom";
// then "let's have it twice as wide and over the casino location ... it can be just magically floating in the air without visible supports
// ... Let's make the sides of the roulette wheel steeper so instead of a guide rail cars are slowly drawn down with gravity". A bowl with no
// legs, its floor rising ever more steeply from a hole in its middle to its wall: HoleY at the hole's edge, Slope a cell outwards from there
// and Steepen a cell a cell more. The cars drive it freely -- it is the race-track mode's own floor, without rails -- drawn down its slopes
// towards the hole, and drop through the hole onto the road under it (RACEMOD.CPP roulette=). Its head -- Pockets pockets, the first green,
// then red and black in turn (two reds side by side where the circle closes), no numbers (the user, 2026-10-10: "Let's lose the numbers on the roulette wheel, half the segments, but double
// the width of the remaining ones. Red is win, black is lose, and green is super jackpot"; the European wheel's 37 numbered ones before)
// -- turns, and white balls roll round the bowl. All of it decors, seen from wherever the wheel is, the race-track mode turning the head
// and moving the balls; nothing here is touched by anything. The race-track mode tells the pocket the car drops through over by its angle
// on the head (RACEMOD.CPP PocketPrize: the same order).
internal sealed class RouletteRun
{
    // its middle (the plan's cells), its wall's inner face, its outside, and the hole (cells from the middle)
    public double[] Centre { get; set; } = Array.Empty<double>();
    public double Rim { get; set; } = 16.2;
    public double Outer { get; set; } = 16.5;
    public double Hole { get; set; } = 4;
    // its floor at the hole's edge, its rise a cell outwards there and how much steeper a cell further out, its wall over its floor
    public double HoleY { get; set; } = 10600;
    public double Slope { get; set; } = 100;
    public double Steepen { get; set; } = 10;
    public double Wall { get; set; } = 400;
    // the lap's way round it (the plan's points: the opponents' line, no floor of its own), and where the drop through its hole lands
    public int From { get; set; }
    public int To { get; set; }
    public int BallFrom { get; set; }
    public int BallTo { get; set; }
    public int Landing { get; set; }
    // how fast it turns (degrees a second)
    public double Spin { get; set; } = 45;
    // its legs round it (degrees from east towards south); none: it floats
    public double[]? Legs { get; set; }
    // its balls (the race-track mode rolls and bounces them: RACEMOD.CPP roulette_balls=)
    public int Balls { get; set; } = 3;
}

// The wheel as built: its middle (island world units), the cube its decors are in, the bodies (the island's) of its turning head and of
// its ball, how fast it turns and the ball's radius; its floor (HoleY, Slope, Steepen), the hole's radius, the wall's and its outside's
// (cells), its underside's depth under its floor; the lap's point where the drop through the hole lands.
internal sealed record RouletteSpot(double X, double Z, int CubeX, int CubeZ, int[] Turning, int Ball, double Spin, int BallRadius,
    double HoleY, double Slope, double Steepen, double Hole, double Rim, double Outer, double Under, int Landing, int Balls = 1, int Pockets = 37);

internal static class RaceTrackRoulette
{
    public const int BallRadius = 720;
    // the bowl's floor's thickness; the pockets' inner edge past the hole (cells)
    private const double Thick = 300, PocketsPast = 0.8;
    private const int Around = 48, Bodies = 7;
    private const double LegIn = 1.7, LegHalf = 260;
    // the island palettes' shared ramps: the pockets' red, black and green, gold, the woods, white
    private const int Red = 71, Black = 49, Green = 134, Gold = 105, GoldDark = 101, Wood = 25, WoodLight = 27, WoodDark = 20, Skirt = 23, White = 62, HoleDark = 48;
    // its pockets: the first green, then red and black in turn -- 9 red, 8 black (half the European wheel's 36 and its zero: 19 for a
    // while; the user, 2026-10-10: "drop a segment from our roulette wheel and make all other slots slightly wider to compensate")
    public const int Pockets = 18;
    private static int PocketColour(int k) => k == 0 ? Green : k % 2 == 1 ? Red : Black;
    private const int NoBoxTop = -32000;

    private sealed class Mesh
    {
        public readonly List<Vector3> Points = new();
        public readonly List<Face> Faces = new();
        public int P(Vector3 v) { Points.Add(v); return Points.Count - 1; }
        public void Quad(int a, int b, int c, int d, int colour, Vector3 outward)
        {
            var n = Vector3.Cross(Points[b] - Points[a], Points[c] - Points[a]);
            Faces.Add(Vector3.Dot(n, outward) >= 0 ? new Face(new[] { a, b, c, d }, colour) : new Face(new[] { d, c, b, a }, colour));
        }
        public byte[] Write() => RaceTrackPipes.Write(Points, Faces, lit: false);
    }

    // Builds the wheel at (cx, cz) (island cells): the ground that would reach its underside lowered, what stands up into it taken away
    // (not what `kept` keeps), and its decors added. Null when its cube is full.
    public static RouletteSpot? Place(IslandFile island, TrackRoad r, RouletteRun run, double cx, double cz, int landing, RaceTrackOptions o,
        RaceTrackReport report, Func<int, bool> kept)
    {
        var rim = run.Rim; var outer = Math.Max(run.Outer, rim + 0.2);
        double Floor(double cells) { var d = Math.Max(0, cells - run.Hole); return run.Slope * d + run.Steepen * d * d; }   // over HoleY
        double Under(double cells) => Floor(Math.Min(cells, rim)) - Thick;
        double UnderAt(double x, double z) => run.HoleY + Under(Math.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)));
        // the ground under it: no higher than a little under its underside
        var lowered = 0;
        for (var gz = (int)Math.Floor(cz - outer - 1); gz <= (int)Math.Ceiling(cz + outer + 1); gz++)
        for (var gx = (int)Math.Floor(cx - outer - 1); gx <= (int)Math.Ceiling(cx + outer + 1); gx++)
        {
            if (Math.Sqrt((gx - cx) * (gx - cx) + (gz - cz) * (gz - cz)) > outer + 0.7) continue;
            var limit = UnderAt(gx, gz) - 400;
            if (island.HeightAt(gx, gz) is not { } h || h <= limit) continue;
            island.SetHeight(gx, gz, (int)Math.Round(limit));
            lowered++;
        }
        // what reaches up into it (not what the plan keeps)
        var removed = 0;
        foreach (var (qx, qz, cube) in IslandOps.CubeCells(island))
            foreach (var d in cube.Decors.ToList())
            {
                var body = d.Body & 0xFFFF;
                if (kept(body)) continue;
                double x0 = (qx * (double)IslandFile.CubeSize + Math.Min(d.XMin, d.X)) / 512, x1 = (qx * (double)IslandFile.CubeSize + Math.Max(d.XMax, d.X)) / 512;
                double z0 = (qz * (double)IslandFile.CubeSize + Math.Min(d.ZMin, d.Z)) / 512, z1 = (qz * (double)IslandFile.CubeSize + Math.Max(d.ZMax, d.Z)) / 512;
                double nx = Math.Clamp(cx, x0, x1), nz = Math.Clamp(cz, z0, z1);
                if ((nx - cx) * (nx - cx) + (nz - cz) * (nz - cz) > (outer + 0.3) * (outer + 0.3)) continue;
                if (Math.Max(d.YMax, d.Y + 1) <= UnderAt(nx, nz) - 150) continue;
                cube.Decors.Remove(d);
                report.Removed.Add((qx, qz, body, "roulette wheel"));
                removed++;
            }
        if (IslandDecors.Locate(island, cx * 512, cz * 512) is not { } home) return null;
        var homeX = (int)Math.Floor(cx * 512 / IslandFile.CubeSize); var homeZ = (int)Math.Floor(cz * 512 / IslandFile.CubeSize);
        if (home.Cube.Decors.Count + Bodies + 8 > IslandDecors.MaxPerCube) { report.Notes.Add("WARNING: the roulette wheel: no room for it in its cube"); return null; }

        var made = new List<int>();
        int Add(byte[] body, double wx, double wy, double wz, double ext, double bottom)
        {
            if (IslandDecors.Locate(island, wx, wz) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCube) return -1;
            var number = o.NewBodyBase + report.NewBodies.Count;
            var d = IslandDecors.Blank(number, at.X, (int)Math.Round(wy), at.Z, 0);
            d.XMin = at.X - (int)ext; d.XMax = at.X + (int)ext; d.ZMin = at.Z - (int)ext; d.ZMax = at.Z + (int)ext;
            d.YMin = (int)Math.Floor(bottom); d.YMax = NoBoxTop;
            at.Cube.Decors.Add(d);
            report.NewBodies.Add(body);
            made.Add(number);
            return number;
        }
        double wcx = cx * 512, wcz = cz * 512, y0 = run.HoleY, ext = outer * 512 + 64, bottom = y0 + Under(run.Hole);
        static Vector3 At(double cells, double angle, double y) => new((float)(cells * 512 * Math.Cos(angle)), (float)y, (float)(cells * 512 * Math.Sin(angle)));
        static Vector3 Radial(double angle) => new((float)Math.Cos(angle), 0, (float)Math.Sin(angle));
        int[] Ring(Mesh m, double cells, double y) => Enumerable.Range(0, Around).Select(k => m.P(At(cells, 2 * Math.PI * k / Around, y))).ToArray();
        var turning = new List<int>();

        // the bowl's wall (gold on top) and its wooden side down to its underside
        var wall = new Mesh();
        {
            var top = Floor(rim) + run.Wall;
            int[] foot = Ring(wall, rim, Floor(rim) - 30), inTop = Ring(wall, rim, top), outTop = Ring(wall, outer, top), outFoot = Ring(wall, outer, Under(rim));
            for (var k = 0; k < Around; k++)
            {
                int j = (k + 1) % Around; var radial = Radial(2 * Math.PI * (k + 0.5) / Around);
                wall.Quad(foot[k], foot[j], inTop[j], inTop[k], k % 2 == 0 ? WoodLight : Wood, -radial);
                wall.Quad(inTop[k], inTop[j], outTop[j], outTop[k], Gold, Vector3.UnitY);
                wall.Quad(outTop[k], outTop[j], outFoot[j], outFoot[k], Skirt, radial);
            }
        }
        var wallBody = Add(wall.Write(), wcx, y0, wcz, ext, bottom);
        if (wallBody >= 0) turning.Add(wallBody);
        // its underside, following its floor's curve, and the hole's own wall
        var under = new Mesh();
        {
            var radii = new List<double> { outer };
            for (var c = rim; c > run.Hole + 0.01; c -= (rim - run.Hole) / 7) radii.Add(c);
            radii.Add(run.Hole);
            var rings = radii.Select(c => Ring(under, c, Under(c))).ToList();
            var holeTop = Ring(under, run.Hole, Floor(run.Hole));
            for (var i = 0; i + 1 < rings.Count; i++)
                for (var k = 0; k < Around; k++)
                {
                    int j = (k + 1) % Around; var radial = Radial(2 * Math.PI * (k + 0.5) / Around);
                    under.Quad(rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k], i % 2 == 0 ? WoodDark : Skirt, -Vector3.UnitY + radial * 0.3f);
                }
            for (var k = 0; k < Around; k++)
            {
                int j = (k + 1) % Around; var radial = Radial(2 * Math.PI * (k + 0.5) / Around);
                under.Quad(rings[^1][k], rings[^1][j], holeTop[j], holeTop[k], HoleDark, -radial);
            }
        }
        var underBody = Add(under.Write(), wcx, y0, wcz, ext, bottom);
        if (underBody >= 0) turning.Add(underBody);

        // the floor inside the pockets, round the hole: wood in segments of two tones, a gold ring round the hole's edge
        var pockets = run.Hole + PocketsPast;
        var inner = new Mesh();
        {
            int[] a = Ring(inner, run.Hole, Floor(run.Hole) + 4), b = Ring(inner, run.Hole + 0.18, Floor(run.Hole + 0.18) + 4),
                  c = Ring(inner, run.Hole + 0.18, Floor(run.Hole + 0.18) + 2), e = Ring(inner, pockets, Floor(pockets) + 2);
            for (var k = 0; k < Around; k++)
            {
                var j = (k + 1) % Around;
                inner.Quad(a[k], a[j], b[j], b[k], Gold, Vector3.UnitY);
                inner.Quad(c[k], c[j], e[j], e[k], k / 4 % 2 == 0 ? Wood : WoodLight, Vector3.UnitY);
            }
        }
        var innerBody = Add(inner.Write(), wcx, y0, wcz, ext, bottom);
        if (innerBody >= 0) turning.Add(innerBody);

        // the head: the pockets, each its colour from the floor round the hole to the wall in bands along the floor's curve, gold frets
        // between them and a gold ring round them -- in Bodies pieces (the engine's limit on a body's points)
        var bands = new List<double>();
        for (var c = pockets; c < rim - 0.04; c += (rim - 0.03 - pockets) / 8) bands.Add(c);
        bands.Add(rim - 0.03);
        var pocket = 2 * Math.PI / Pockets;
        for (var piece = 0; piece < Bodies; piece++)
        {
            var head = new Mesh();
            int k0 = piece * Pockets / Bodies, k1 = (piece + 1) * Pockets / Bodies;
            for (var k = k0; k < k1; k++)
            {
                double a0 = k * pocket, a1 = (k + 1) * pocket;
                var colour = PocketColour(k);
                var inEdge = bands.Select(c => head.P(At(c, a0, Floor(c) + 6))).ToArray();
                var outEdge = bands.Select(c => head.P(At(c, a1, Floor(c) + 6))).ToArray();
                for (var i = 0; i + 1 < bands.Count; i++) head.Quad(inEdge[i], outEdge[i], outEdge[i + 1], inEdge[i + 1], colour, Vector3.UnitY);
                void Band(double r0, double r1, double lift, int c) =>
                    head.Quad(head.P(At(r0, a0, Floor(r0) + lift)), head.P(At(r0, a1, Floor(r0) + lift)), head.P(At(r1, a1, Floor(r1) + lift)), head.P(At(r1, a0, Floor(r1) + lift)), c, Vector3.UnitY);
                Band(pockets, pockets + 0.15, 12, Gold);
                // (the fret along the pocket's first edge: a strip 50 wide following the floor)
                var side = new Vector3((float)-Math.Sin(a0), 0, (float)Math.Cos(a0)) * 25;
                var fretA = bands.Select(c => head.P(At(c, a0, Floor(c) + 16) - side)).ToArray();
                var fretB = bands.Select(c => head.P(At(c, a0, Floor(c) + 16) + side)).ToArray();
                for (var i = 0; i + 1 < bands.Count; i++) head.Quad(fretA[i], fretB[i], fretB[i + 1], fretA[i + 1], GoldDark, Vector3.UnitY);
            }
            var number = Add(head.Write(), wcx, y0, wcz, ext, bottom);
            if (number >= 0) turning.Add(number);
        }

        // the balls: white spheres (the race-track mode rolls and bounces them round the bowl), each a body of its own, one after another
        // (the race-track mode tells them apart by it), where they start
        var ball = new Body { Game = 2, Static = true, Lit = false, Header = new byte[96], Vertices = new List<Vector3> { Vector3.Zero },
            Bones = new List<Bone> { new(0, 1, 0, -1, new byte[8]) } };
        ball.Spheres.Add(new BodySphere(0, BallRadius, White));
        var ballCells = (run.Hole + rim) / 2;
        var balls = Math.Max(1, run.Balls);
        var ballBody = -1;
        for (var k = 0; k < balls; k++)
        {
            var a = 2 * Math.PI * k / balls;
            var b = Add(ball.Write(), wcx + ballCells * 512 * Math.Cos(a), y0 + Floor(ballCells) + BallRadius, wcz + ballCells * 512 * Math.Sin(a), BallRadius + 64, y0 + Floor(ballCells));
            if (k == 0) ballBody = b;
            else if (b != ballBody + k) { balls = k; break; }
        }

        // the legs, if the plan gives the wheel any: square columns from the ground to its underside
        var legs = 0;
        foreach (var deg in run.Legs ?? Array.Empty<double>())
        {
            var a = deg * Math.PI / 180;
            var at = rim - LegIn;
            double lx = wcx + at * 512 * Math.Cos(a), lz = wcz + at * 512 * Math.Sin(a);
            var ground = IslandOps.Altitude(island, lx, lz) ?? 0;
            var top = y0 + Under(at) - ground;
            var leg = new Mesh();
            var u = new Vector3((float)Math.Cos(a), 0, (float)Math.Sin(a)); var v = new Vector3(-u.Z, 0, u.X);
            RaceTrackPipes.Box(leg.Points, leg.Faces, Vector3.Zero, u * (float)LegHalf, v * (float)LegHalf, -60, (float)top, Gold, GoldDark, GoldDark);
            if (Add(leg.Write(), lx, ground, lz, LegHalf * 2, ground - 60) >= 0) legs++;
        }

        report.Notes.Add($"a roulette wheel at cell ({cx:0.0}, {cz:0.0}): its wall {rim:0.##} cells out, its floor {run.HoleY:0} at the hole ({run.Hole:0.#} cells) to " +
                         $"{run.HoleY + Floor(rim):0} at the wall; {turning.Count} turning pieces, its ball, {(legs > 0 ? $"{legs} legs" : "floating")}, {made.Count} decors in all; " +
                         $"{lowered} points of the ground lowered under it, {removed} decors taken away");
        return new RouletteSpot(wcx, wcz, homeX, homeZ, turning.ToArray(), ballBody, run.Spin, BallRadius, run.HoleY, run.Slope, run.Steepen, run.Hole, rim, outer, Thick, landing, balls, Pockets);
    }
}
