using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// The pieces of a raised road (RaceTrackPlan.Raised): a road that stands in the air on piers, following the plan's heights at any grade --
// Celebration Island's lap winds up round the statue and comes back down over itself, which the engine's ground, one height map, cannot do.
// Each piece is a decor body made for its own place: a few cells of road surface between cross-sections of the lap (so bends and grades
// are in the mesh itself and the decor needs no turn), and the piers that carry it. Unlike the flat bridge deck (RaceTrackDeckBody), whose
// collision box is its floor, these carry nothing themselves: a decor's box is level and axis-aligned, and a sloping road made of them
// would be a staircase the engine drops a car down step by step. The engine's race-track mode has the road as a floor of its own
// (RACEMOD.CPP, from RACETRACK.JSON's Raised).
internal static class RaceTrackRaisedBody
{
    // the deck's colours: the same greys, red and white as the flat deck's (RaceTrackDeckBody; the island palettes share them)
    public const int Asphalt = RaceTrackDeckBody.TopColour, Red = RaceTrackDeckBody.RailColour, White = RaceTrackDeckBody.WhiteColour;
    public const int RailTop = 58, RailSide = 56, Side = 51, Under = 50, PierLight = 56, PierDark = 53, PierCap = 55;
    // the arrows: the orange of the ground's own (the island palettes' fifth ramp)
    public const int Arrow = 89;
    // the slab's thickness and the rail's height (world units)
    public const double Thickness = 200, Rail = 220;

    internal sealed class Mesh
    {
        public readonly List<Vector3> Points = new();
        public readonly List<Face> Faces = new();
        public int P(Vector3 v) { Points.Add(v); return Points.Count - 1; }

        // a quad whose right-hand normal points the way `outward` does (the engine culls faces seen from behind)
        public void Quad(int a, int b, int c, int d, int colour, Vector3 outward)
        {
            var n = Vector3.Cross(Points[b] - Points[a], Points[c] - Points[a]);
            Faces.Add(Vector3.Dot(n, outward) >= 0 ? new Face(new[] { a, b, c, d }, colour) : new Face(new[] { d, c, b, a }, colour));
        }

        public void Tri(int a, int b, int c, int colour, Vector3 outward)
        {
            var n = Vector3.Cross(Points[b] - Points[a], Points[c] - Points[a]);
            Faces.Add(Vector3.Dot(n, outward) >= 0 ? new Face(new[] { a, b, c }, colour) : new Face(new[] { c, b, a }, colour));
        }

        public byte[] Write() => new Body
        {
            Game = 2, Static = true, Lit = false, Header = new byte[96],
            Vertices = Points,
            Bones = new List<Bone> { new(0, Points.Count, 0, -1, new byte[8]) },
            Faces = Faces,
        }.Write();
    }

    // One piece of road: `sections` are cross-sections along the lap, each its middle (from the body's origin, in world axes, y up) and the
    // unit vector across the road; `firstBlock` numbers the curb's red and white blocks so they run on from piece to piece. Widths in world
    // units from the middle: the asphalt's edge, the curb's outer edge (where the rail begins), the road's edge. `arrow`: a piece of
    // four cells with an arrow on its asphalt, pointing the way the sections run -- its head two cells long, cut into the asphalt (the
    // cells before and after it are cut to meet its corners, so no face's edge ends part-way along another's: the engine leaves a
    // seam of missing pixels there).
    // `line`: the cell (from that section to the next) that is the start line, its asphalt white and its curbs red. A section's Across
    // need not be level: a banked road's has the height it gains per unit across as its y.
    // `wider`: how much wider than `edge` the road is at each section (world units, either side: its asphalt widens, its curbs and rails
    // move out -- the Emerald Moon's road widens for its jump, its loops and its pit lane). `stripe`: a white stripe along the asphalt this
    // far across (world units, the way Across points) on the cells `striped` says.
    // The asphalt (and the underside) are drawn in strips: the engine leaves out a polygon any point of which is behind the camera's near
    // plane, and the Emerald Moon's road, 17 cells across at its widest, was one quad from rail to rail -- the road under a close camera
    // showed a hole the width of the road (2026-10-03). `strips`: how many across, the same for every piece of a road (its widest asphalt
    // over StripWidth, an even number and ArrowStrips at the least), so two pieces meet on the same points -- where they don't, the engine
    // leaves a seam of missing pixels. For the same reason an arrow is the strips' own quads coloured, halved along their diagonals at its
    // edges (a triangle four cells long, ArrowStrips wide at its base), not cut into them; drawn over them it flickered with them.
    public const double StripeHalf = 50, StripWidth = 512, PieceOverlap = 24, ArrowHalfWidth = 896;
    public const int ArrowStrips = 8;
    public static int StripsFor(double widestAsphalt)
    {
        var n = Math.Max(ArrowStrips + 2, (int)Math.Ceiling(2 * widestAsphalt / StripWidth));
        return n % 2 == 0 ? n : n + 1;
    }

    public static byte[] Tile(IReadOnlyList<(Vector3 Mid, Vector3 Across)> sections, int firstBlock, double asphalt, double curb, double edge, bool arrow = false, int line = -1,
        IReadOnlyList<double>? wider = null, double stripe = double.NaN, IReadOnlyList<bool>? striped = null, int strips = 0)
    {
        var m = new Mesh();
        double Wider(int j) => wider?[j] ?? 0;
        arrow = arrow && sections.Count == 5;
        if (strips < 1) strips = StripsFor(Enumerable.Range(0, sections.Count).Max(j => asphalt + Wider(j)));
        // (the stripe: its strip of the asphalt coloured -- the one its middle is in)
        // across each section, from one edge to the other: the offset and the height over the road's surface
        var profile = new (double U, double Y)[]
        {
            (-edge, -Thickness), (-edge, Rail), (-curb, Rail), (-curb, 0), (-asphalt, 0),
            (asphalt, 0), (curb, 0), (curb, Rail), (edge, Rail), (edge, -Thickness),
        };
        var at = new int[sections.Count, profile.Length];
        int On(int j, double u, double y = 0) => m.P(sections[j].Mid + sections[j].Across * (float)u + new Vector3(0, (float)y, 0));
        for (var j = 0; j < sections.Count; j++)
            for (var k = 0; k < profile.Length; k++)
                at[j, k] = On(j, profile[k].U + Math.Sign(profile[k].U) * Wider(j), profile[k].Y);
        // the asphalt's strips, their edges evenly across it at every section; and the underside's, two to each of them
        var edges = new int[sections.Count, strips + 1];
        var below = Math.Max(1, (strips + 1) / 2);
        var under = new int[sections.Count, below + 1];
        for (var j = 0; j < sections.Count; j++)
        {
            edges[j, 0] = at[j, 4]; edges[j, strips] = at[j, 5];
            for (var i = 1; i < strips; i++) edges[j, i] = On(j, (-1 + 2.0 * i / strips) * (asphalt + Wider(j)));
            under[j, 0] = at[j, 9]; under[j, below] = at[j, 0];
            for (var i = 1; i < below; i++) under[j, i] = On(j, (1 - 2.0 * i / below) * (edge + Wider(j)), -Thickness);
        }
        var up = Vector3.UnitY;
        for (var j = 0; j + 1 < sections.Count; j++)
        {
            var across = Vector3.Normalize(sections[j].Across + sections[j + 1].Across);
            var block = j == line || (firstBlock + j) % 2 == 0 ? Red : White;
            void Strip(int k, int colour, Vector3 outward) => m.Quad(at[j, k], at[j, k + 1], at[j + 1, k + 1], at[j + 1, k], colour, outward);
            Strip(0, Side, -across);          // the outer side, slab and rail
            Strip(1, RailTop, up);
            Strip(2, RailSide, across);       // the rail's inner face
            Strip(3, block, up);              // the curb
            for (var i = 0; i < strips; i++)
            {
                int qa = edges[j, i], qb = edges[j, i + 1], qc = edges[j + 1, i + 1], qd = edges[j + 1, i];
                // (an arrow over the strips from the section's middle, w0 - j either side of it at section j, to its point: w0 strips about
                // ArrowHalfWidth, ArrowStrips / 2 at the most -- on a wide deck its strips are wide, and its arrow shorter)
                var c = strips / 2;
                var w0 = Math.Clamp((int)Math.Round(ArrowHalfWidth / (2 * (asphalt + Wider(0)) / strips)), 1, ArrowStrips / 2);
                var w = w0 - j;
                if (arrow && w > 0 && i == c - w) { m.Tri(qa, qb, qc, Arrow, up); m.Tri(qa, qc, qd, Asphalt, up); continue; }
                if (arrow && w > 0 && i == c + w - 1) { m.Tri(qa, qb, qd, Arrow, up); m.Tri(qb, qc, qd, Asphalt, up); continue; }
                var white = j == line || !double.IsNaN(stripe) && striped is { } st && j < st.Count && st[j]
                            && i == Math.Clamp((int)Math.Floor((stripe / (asphalt + Wider(j)) + 1) / 2 * strips), 0, strips - 1);
                var colour = white ? White : arrow && w > 0 && i > c - w && i < c + w - 1 ? Arrow : Asphalt;
                m.Quad(qa, qb, qc, qd, colour, up);
            }
            Strip(5, block, up);
            Strip(6, RailSide, -across);
            Strip(7, RailTop, up);
            Strip(8, Side, across);
            for (var i = 0; i < below; i++) m.Quad(under[j, i], under[j, i + 1], under[j + 1, i + 1], under[j + 1, i], Under, -up);   // the underside
        }
        return m.Write();
    }

    // A start gantry for a line on the raised road itself: two red posts on the rails and a chequered beam between their tops, `clear`
    // over the road and `beam` high -- lower than the retail gantry (3990 to its top), which does not fit under the next level of a road
    // that winds up over its own start. `across`: the unit vector across the road; the body's origin is the road's middle at the line.
    public const int Black = 48;
    public static byte[] Gantry(Vector3 across, double half, double clear, double beam)
    {
        var m = new Mesh();
        var along = new Vector3(-across.Z, 0, across.X);
        const float post = 130, thick = 110;
        void Box(Vector3 centre, Vector3 u, Vector3 v, float y0, float y1, int light, int dark)
        {
            var c = new[] { centre - u - v, centre + u - v, centre + u + v, centre - u + v };
            var lo = c.Select(p => m.P(p + new Vector3(0, y0, 0))).ToArray(); var hi = c.Select(p => m.P(p + new Vector3(0, y1, 0))).ToArray();
            for (var k = 0; k < 4; k++)
            {
                var n = (k + 1) % 4;
                m.Quad(lo[k], lo[n], hi[n], hi[k], k % 2 == 0 ? light : dark, (c[k] + c[n]) / 2 - centre);
            }
            m.Quad(hi[0], hi[1], hi[2], hi[3], light, Vector3.UnitY);
            m.Quad(lo[0], lo[1], lo[2], lo[3], dark, -Vector3.UnitY);
        }
        var reach = (float)half + post;
        foreach (var side in new[] { -1f, 1f })
            Box(across * (side * (float)half), across * post, along * post, -(float)Thickness, (float)clear, Red, 70);
        // the beam: its two faces in squares, black and white, two rows of them
        var columns = Math.Max(2, (int)Math.Round(2 * reach / (beam / 2)));
        float y0 = (float)clear, y1 = (float)(clear + beam), mid = (float)(clear + beam / 2);
        foreach (var face in new[] { -1f, 1f })
            for (var k = 0; k < columns; k++)
            {
                float u0 = -reach + 2 * reach * k / columns, u1 = -reach + 2 * reach * (k + 1) / columns;
                Vector3 At(float u, float y) => across * u + along * (face * thick) + new Vector3(0, y, 0);
                for (var row = 0; row < 2; row++)
                {
                    float a = row == 0 ? y0 : mid, b = row == 0 ? mid : y1;
                    m.Quad(m.P(At(u0, a)), m.P(At(u1, a)), m.P(At(u1, b)), m.P(At(u0, b)), (k + row) % 2 == 0 ? Black : White, along * face);
                }
            }
        // ... and its top, underside and ends
        Vector3 Corner(float u, float w, float y) => across * u + along * w + new Vector3(0, y, 0);
        m.Quad(m.P(Corner(-reach, -thick, y1)), m.P(Corner(reach, -thick, y1)), m.P(Corner(reach, thick, y1)), m.P(Corner(-reach, thick, y1)), RailTop, Vector3.UnitY);
        m.Quad(m.P(Corner(-reach, -thick, y0)), m.P(Corner(reach, -thick, y0)), m.P(Corner(reach, thick, y0)), m.P(Corner(-reach, thick, y0)), Under, -Vector3.UnitY);
        foreach (var side in new[] { -1f, 1f })
            m.Quad(m.P(Corner(side * reach, -thick, y0)), m.P(Corner(side * reach, thick, y0)), m.P(Corner(side * reach, thick, y1)), m.P(Corner(side * reach, -thick, y1)), Side, across * side);
        return m.Write();
    }

    // A pier: a square column `half` wide each way from the ground (the body's origin, y 0) up to `height`, and on it a beam `beam` long
    // each way across the road and `thick` high, along `across`. The column's sides are in two greys, so it reads as a solid from any side.
    public static byte[] Pier(double height, double half, Vector3 across, double beam, double thick, double beamHalf)
        => Pier(height, half, across, -beam, beam, thick, beamHalf);

    // ... with its beam from `lo` to `hi` along `across` from the column (a column beside the road, its beam reaching under it), and
    // leaning as `across` does (a banked road's): `height` is the beam's top over the column.
    public static byte[] Pier(double height, double half, Vector3 across, double lo, double hi, double thick, double beamHalf)
    {
        var m = new Mesh();
        var h = (float)Math.Max(thick + 50, height); var t = (float)thick; var a = (float)half;
        // (the column is upright whatever the beam's lean)
        var level = across.Y == 0 ? across : Vector3.Normalize(new Vector3(across.X, 0, across.Z));
        var along = new Vector3(-level.Z, 0, level.X);
        void Box(Vector3 centre, Vector3 u, Vector3 v, float y0, float y1, int light, int dark, int top)
        {
            var c = new[] { centre - u - v, centre + u - v, centre + u + v, centre - u + v };
            var lo = c.Select(p => m.P(p + new Vector3(0, y0, 0))).ToArray(); var hi = c.Select(p => m.P(p + new Vector3(0, y1, 0))).ToArray();
            for (var k = 0; k < 4; k++)
            {
                var n = (k + 1) % 4;
                var outward = (c[k] + c[n]) / 2 - centre;
                m.Quad(lo[k], lo[n], hi[n], hi[k], k % 2 == 0 ? light : dark, outward);
            }
            m.Quad(hi[0], hi[1], hi[2], hi[3], top, Vector3.UnitY);
            m.Quad(lo[0], lo[1], lo[2], lo[3], dark, -Vector3.UnitY);
        }
        Box(Vector3.Zero, level * a, along * a, 0, h - t, PierLight, PierDark, PierDark);
        Box(across * (float)((lo + hi) / 2), across * (float)((hi - lo) / 2), along * (float)beamHalf, h - t, h, PierCap, PierDark, PierCap);
        return m.Write();
    }
}
