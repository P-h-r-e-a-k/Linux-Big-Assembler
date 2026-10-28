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
// A raised road's colours (RaceTrackPlan.Themes): the deck, the curbs' two, the rails', the slab's side and underside, the piers' and the
// arrows'. A lap's stretches can each have their own -- the Island of the Francos' three parts (the user, 2026-10-07: "make the track 3
// distinctly themed sections"): the dock's planks with blue and white curbs on wooden piles, the refinery's steel with yellow and black
// hazard curbs and red rails, the village's earth with green and white curbs and olive rails. Palette indices of the islands' shared
// ramps: browns 16-31, greys 48-63, reds 64-79, golds 96-111, greens 112-143, blues 192-207.
internal sealed record DeckTheme(string Name, int Asphalt, int Red, int White, int RailTop, int RailSide, int Side, int Under, int PierLight, int PierDark, int PierCap, int Arrow)
{
    // The start gantry's colours on such a road -- its posts and its beam's two squares -- or null: the race's own red posts and black and
    // white squares.
    public (int Post, int Light, int Dark)? Gantry { get; init; }

    public static readonly DeckTheme Standard = new("standard", RaceTrackRaisedBody.Asphalt, RaceTrackRaisedBody.Red, RaceTrackRaisedBody.White, RaceTrackRaisedBody.RailTop,
        RaceTrackRaisedBody.RailSide, RaceTrackRaisedBody.Side, RaceTrackRaisedBody.Under, RaceTrackRaisedBody.PierLight, RaceTrackRaisedBody.PierDark, RaceTrackRaisedBody.PierCap,
        RaceTrackRaisedBody.Arrow);
    public static readonly DeckTheme Dock = new("dock", 23, 201, 63, 28, 25, 21, 19, 24, 20, 27, 62);
    public static readonly DeckTheme Refinery = new("refinery", 52, 108, 49, 70, 67, 50, 49, 57, 54, 70, 245);
    public static readonly DeckTheme Village = new("village", 104, 134, 63, 121, 118, 98, 97, 100, 97, 120, 89);
    // the Emperor's palace (Otringal's palette): a red carpet edged in gold through its rooms and up to the round window; out of it and over
    // the rooftops its pale stone, edged and railed in the turquoise of its domes' drums
    public static readonly DeckTheme Carpet = new("carpet", 72, 105, 239, 105, 102, 83, 83, 236, 234, 105, 239);
    public static readonly DeckTheme Palace = new("palace", 236, 188, 239, 188, 197, 234, 55, 236, 234, 188, 105);
    // the Temple of Bú's Halloween race (its own palette: Otringal's with LBA1's colours written in, TemplePalette): a road near black, its
    // curbs pumpkin orange and black, its rails' tops glowing orange over sides of dark plum, black slabs on dark stone piers
    public static readonly DeckTheme Halloween = new("halloween", 209, 88, 224, 89, 211, 210, 208, 213, 210, 86, 89) { Gantry = (86, 242, 224) };
    public static readonly DeckTheme[] All = { Standard, Dock, Refinery, Village, Carpet, Palace, Halloween };
    public static DeckTheme? ByName(string? name) => All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}

// A stretch of the lap in a DeckTheme (RaceTrackPlan.Themes): the plan's points From to To.
internal sealed class ThemeRun
{
    public int From { get; set; }
    public int To { get; set; }
    public string Theme { get; set; } = "";
}

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
    public const double StripeHalf = 50, StripWidth = 512, PieceOverlap = 24, ArrowHalfWidth = 1024, ArrowShaftHalf = 512;
    public const int ArrowHeadCells = 3;
    public const int ArrowStrips = 8;
    public static int StripsFor(double widestAsphalt)
    {
        var n = Math.Max(ArrowStrips + 2, (int)Math.Ceiling(2 * widestAsphalt / StripWidth));
        return n % 2 == 0 ? n : n + 1;
    }

    // `boost`: the cells (from each section to the next) a boost panel covers -- how far across it reaches (world units, the way Across
    // points) and how many cells into the panel the cell is -- its strips there a white arrow on orange in every cell, pointing the way the
    // sections run (RACEMOD.CPP boostpanel=).
    public static byte[] Tile(IReadOnlyList<(Vector3 Mid, Vector3 Across)> sections, int firstBlock, double asphalt, double curb, double edge, bool arrow = false, int line = -1,
        IReadOnlyList<double>? wider = null, double stripe = double.NaN, IReadOnlyList<bool>? striped = null, int strips = 0, DeckTheme? theme = null,
        IReadOnlyList<(double Lo, double Hi, int Q)?>? boost = null, IReadOnlyList<(bool Minus, bool Plus)>? open = null,
        IReadOnlyList<(bool Minus, bool Plus)>? flush = null)
    {
        // (open: a section with no rail on that side -- down to the curb -- where another road's deck runs on from it: a branch of the
        // raised road leaving the lap or coming back onto it, Wannies Island's shortcut)
        // (flush: a section with no rail on that side, its curb flat out to the road's edge, where the road lies level with the floor beside
        // it -- the cars drive off it there: the Temple of Bú's ground stretch, the user, 2026-10-19: "drop the track guard rails where it's
        // level with the ground")
        var m = new Mesh();
        var (Asphalt, Red, White, RailTop, RailSide, Side, Under, Arrow) = theme is { } th
            ? (th.Asphalt, th.Red, th.White, th.RailTop, th.RailSide, th.Side, th.Under, th.Arrow)
            : (RaceTrackRaisedBody.Asphalt, RaceTrackRaisedBody.Red, RaceTrackRaisedBody.White, RaceTrackRaisedBody.RailTop, RaceTrackRaisedBody.RailSide, RaceTrackRaisedBody.Side,
               RaceTrackRaisedBody.Under, RaceTrackRaisedBody.Arrow);
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
            {
                var flat = open is not null && j < open.Count && (k is 1 or 2 ? open[j].Minus : k is 7 or 8 && open[j].Plus)
                           || flush is not null && j < flush.Count && (k is 1 or 2 ? flush[j].Minus : k is 7 or 8 && flush[j].Plus);
                at[j, k] = On(j, profile[k].U + Math.Sign(profile[k].U) * Wider(j), flat ? 0 : profile[k].Y);
            }
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
            // (a side open from section to section: asphalt to its edge, no curb and no flattened rail -- they lay as a red and white line
            // across the one deck where a branch leaves the lap; the user, 2026-10-15: "where our track splits we've got red/white line in
            // the middle that we can remove")
            bool OpenCell(bool plus) => open is not null && j + 1 < open.Count && (plus ? open[j].Plus && open[j + 1].Plus : open[j].Minus && open[j + 1].Minus);
            var openMinus = OpenCell(false); var openPlus = OpenCell(true);
            // (a side flush from section to section: the slab's edge, the curb out to it where the rail was, no rail's face)
            bool FlushCell(bool plus) => flush is not null && j + 1 < flush.Count && (plus ? flush[j].Plus && flush[j + 1].Plus : flush[j].Minus && flush[j + 1].Minus);
            var flushMinus = !openMinus && FlushCell(false); var flushPlus = !openPlus && FlushCell(true);
            Strip(0, openMinus ? Asphalt : Side, -across);          // the outer side, slab and rail
            Strip(1, openMinus ? Asphalt : flushMinus ? block : RailTop, up);
            if (!flushMinus) Strip(2, openMinus ? Asphalt : RailSide, across);       // the rail's inner face
            Strip(3, openMinus ? Asphalt : block, up);              // the curb
            // (a boost panel's strips: those whose middles are within its reach across)
            int b0 = -1, b1 = -1;
            if (boost is not null && j < boost.Count && boost[j] is { } bp)
                for (var i = 0; i < strips; i++)
                {
                    var u = (-1 + (2.0 * i + 1) / strips) * (asphalt + Wider(j));
                    if (u >= bp.Lo && u <= bp.Hi) { if (b0 < 0) b0 = i; b1 = i; }
                }
            for (var i = 0; i < strips; i++)
            {
                int qa = edges[j, i], qb = edges[j, i + 1], qc = edges[j + 1, i + 1], qd = edges[j + 1, i];
                if (b0 >= 0 && i >= b0 && i <= b1 && boost![j] is { } panel)
                {
                    // (a white arrow in every cell, pointing the way the sections run: as wide as the panel at the cell's back, as its middle
                    // strips at its front -- its sides the outer strips' diagonals, their outer halves orange; a panel one strip wide, white
                    // and orange cells in turn)
                    if (b0 == b1) m.Quad(qa, qb, qc, qd, panel.Q % 2 == 0 ? White : Arrow, up);
                    else if (i == b0) { m.Tri(qa, qb, qc, White, up); m.Tri(qa, qc, qd, Arrow, up); }
                    else if (i == b1) { m.Tri(qa, qb, qd, White, up); m.Tri(qb, qc, qd, Arrow, up); }
                    else m.Quad(qa, qb, qc, qd, White, up);
                    continue;
                }
                // (an arrow as the ground's are -- a shaft and a head, the user, 2026-10-27: "raised road sections have tended to have small
                // triangles, and Desert track featured arrows instead, let's be consistent across all tracks": its first cell the shaft,
                // ArrowShaftHalf either side of the middle; then the head over the strips from w0 either side of the middle at its base,
                // a strip less each cell, to its point -- w0 strips about ArrowHalfWidth, ArrowHeadCells at the most)
                var c = strips / 2;
                var sw = 2 * (asphalt + Wider(0)) / strips;
                var w0 = Math.Clamp((int)Math.Round(ArrowHalfWidth / sw), 1, ArrowHeadCells);
                var shaft = Math.Clamp((int)Math.Round(ArrowShaftHalf / sw), 1, Math.Max(1, w0 - 1));
                if (arrow && j == 0)
                {
                    m.Quad(qa, qb, qc, qd, i >= c - shaft && i < c + shaft ? Arrow : j == line ? White : Asphalt, up);
                    continue;
                }
                var w = w0 - (j - 1);
                if (arrow && w > 0 && i == c - w) { m.Tri(qa, qb, qc, Arrow, up); m.Tri(qa, qc, qd, Asphalt, up); continue; }
                if (arrow && w > 0 && i == c + w - 1) { m.Tri(qa, qb, qd, Arrow, up); m.Tri(qb, qc, qd, Asphalt, up); continue; }
                var white = j == line || !double.IsNaN(stripe) && striped is { } st && j < st.Count && st[j]
                            && i == Math.Clamp((int)Math.Floor((stripe / (asphalt + Wider(j)) + 1) / 2 * strips), 0, strips - 1);
                var colour = white ? White : arrow && w > 0 && i > c - w && i < c + w - 1 ? Arrow : Asphalt;
                m.Quad(qa, qb, qc, qd, colour, up);
            }
            Strip(5, openPlus ? Asphalt : block, up);
            if (!flushPlus) Strip(6, openPlus ? Asphalt : RailSide, -across);
            Strip(7, openPlus ? Asphalt : flushPlus ? block : RailTop, up);
            Strip(8, openPlus ? Asphalt : Side, across);
            for (var i = 0; i < below; i++) m.Quad(under[j, i], under[j, i + 1], under[j + 1, i + 1], under[j + 1, i], Under, -up);   // the underside
        }
        return m.Write();
    }

    // A start gantry for a line on the raised road itself: two red posts on the rails and a chequered beam between their tops, `clear`
    // over the road and `beam` high -- lower than the retail gantry (3990 to its top), which does not fit under the next level of a road
    // that winds up over its own start. `across`: the unit vector across the road; the body's origin is the road's middle at the line.
    public const int Black = 48;
    public static byte[] Gantry(Vector3 across, double half, double clear, double beam, DeckTheme? theme = null)
    {
        var (postColour, light, dark) = theme?.Gantry ?? (Red, White, Black);
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
            Box(across * (side * (float)half), across * post, along * post, -(float)Thickness, (float)clear, postColour, 70);
        // the beam: its two faces in squares, black and white, two rows of them
        // (16 points a column, the posts and the ends 48 more: at most 30 columns keeps the body within the engine's 550 -- a gantry built to
        // a road with a pit lane beside it, 20 cells across, would have had 36, its squares longer instead)
        var columns = Math.Clamp((int)Math.Round(2 * reach / (beam / 2)), 2, 30);
        float y0 = (float)clear, y1 = (float)(clear + beam), mid = (float)(clear + beam / 2);
        foreach (var face in new[] { -1f, 1f })
            for (var k = 0; k < columns; k++)
            {
                float u0 = -reach + 2 * reach * k / columns, u1 = -reach + 2 * reach * (k + 1) / columns;
                Vector3 At(float u, float y) => across * u + along * (face * thick) + new Vector3(0, y, 0);
                for (var row = 0; row < 2; row++)
                {
                    float a = row == 0 ? y0 : mid, b = row == 0 ? mid : y1;
                    m.Quad(m.P(At(u0, a)), m.P(At(u1, a)), m.P(At(u1, b)), m.P(At(u0, b)), (k + row) % 2 == 0 ? dark : light, along * face);
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
    public static byte[] Pier(double height, double half, Vector3 across, double beam, double thick, double beamHalf, DeckTheme? theme = null)
        => Pier(height, half, across, -beam, beam, thick, beamHalf, theme);

    // ... with its beam from `lo` to `hi` along `across` from the column (a column beside the road, its beam reaching under it), and
    // leaning as `across` does (a banked road's): `height` is the beam's top over the column.
    public static byte[] Pier(double height, double half, Vector3 across, double lo, double hi, double thick, double beamHalf, DeckTheme? theme = null)
    {
        var m = new Mesh();
        var (PierLight, PierDark, PierCap) = theme is { } th ? (th.PierLight, th.PierDark, th.PierCap) : (RaceTrackRaisedBody.PierLight, RaceTrackRaisedBody.PierDark, RaceTrackRaisedBody.PierCap);
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

    // A block: `halfX` by `halfZ` (units) round its middle, from 0 up to `height`, its sides in two colours and its top in a third -- the
    // walls built up under a raised road where it passes over a building cut down for it, a lift's pad and post (RaceTrackBuilder DeckWalls,
    // DeckLifts)
    public static byte[] Block(double halfX, double halfZ, double height, int light = PierLight, int dark = PierDark, int top = PierCap)
    {
        var m = new Mesh();
        float x = (float)halfX, z = (float)halfZ, h = (float)height;
        var c = new[] { new Vector3(-x, 0, -z), new Vector3(x, 0, -z), new Vector3(x, 0, z), new Vector3(-x, 0, z) };
        var lo = c.Select(p => m.P(p)).ToArray(); var hi = c.Select(p => m.P(p + new Vector3(0, h, 0))).ToArray();
        for (var k = 0; k < 4; k++)
        {
            var n = (k + 1) % 4;
            m.Quad(lo[k], lo[n], hi[n], hi[k], k % 2 == 0 ? light : dark, (c[k] + c[n]) / 2);
        }
        m.Quad(hi[0], hi[1], hi[2], hi[3], top, Vector3.UnitY);
        m.Quad(lo[0], lo[1], lo[2], lo[3], dark, -Vector3.UnitY);
        return m.Write();
    }
}
