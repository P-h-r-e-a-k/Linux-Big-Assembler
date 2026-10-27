using System.Numerics;

namespace LBAAssembler.Terrain.Temple;

// The rocks.
internal static partial class TempleModels
{
    // a lobe's share of its row's picture (as of a row along x, 24 pixels to a cell: 12 pixels, about LBA1's lobes), and its round top's
    // height for its half width (cells: 2.2 times it, a little more than half an ellipse as tall as wide -- LBA1's lobes are taller than
    // round)
    private const double LobeWidth = 0.5, LobeDome = 2.2;
    // a lobe's width along its row for its share of it, at least (by turns that look random, up to a fifth more)
    private const double LobeWide = 1.15;

    // LBA1's rocks: a row of upright lobes, rounded on top, like a stand of cypresses, along a wall -- the lobes overlapping, those in front
    // lower, their outlines drawn in the rock's shading; or, where a row hangs from a floor's edge over the void, rounded at their feet too.
    // The lobes are laid out the way LBA1's picture shows the row: evenly across the screen from its outline's one end to the other, as many
    // as the row is long over LobeWidth, each on the line of the block's ground that LBA1's view sees in its share of the picture (2026-10-18:
    // laid along the block's middle line, the row's two ends, the block's corners on the screen, were never covered -- a quarter of a rock
    // column's picture void). Each stands in the middle of that line's run through the block, by turns behind and in front (toward LBA1's
    // camera, a little lower), each a sixth to a third wider than its share, so that neighbours overlap and no slit shows between them. Its
    // top is the outline over its share; its foot on the block's floor, or, where the picture's lowest under it is above the floor (an egg's
    // round foot, a row hanging over the void), rounded to a point there. A lobe: an ellipse across (along the row as wide, as deep as the
    // block leaves room for), its top rounded (LobeDome: a ring half way round) to a point; 6 sides round -- 18 to 24 faces -- two tiles (its
    // body, its top), each texel the picture's. Squeezed into the bricks' cells (an end lobe flattened against the block's end, which draws
    // the end's corner as LBA1's picture does). Of a block LBA1 placed only some layers of, the lobes between them (Model.Without).
    private static void MakeLobes(Model m)
    {
        var p = m.Picture;
        // (the row's way and across it, toward LBA1's camera)
        Vector3 dir = m.Dx > m.Dz ? Vector3.UnitX : m.Dz > m.Dx ? Vector3.UnitZ : Vector3.Normalize(new Vector3(1, 0, -1));
        var across = Vector3.Normalize(new Vector3(-dir.Z, 0, dir.X));
        if (Vector3.Dot(across, new Vector3(1, 0, 1)) < 0) across = -across;
        // (pixels across the screen for a cell along the row; the bricks' cells' box, which the lobes stay in)
        var alongPx = Math.Abs(24 * (dir.X - dir.Z));
        float bx0 = m.Bricked.Min(c => c.I), bx1 = m.Bricked.Max(c => c.I) + 1, bz0 = m.Bricked.Min(c => c.K), bz1 = m.Bricked.Max(c => c.K) + 1;

        // ---- the picture's outline, column by column (its dithering's gaps of two pixels closed): each column's highest and lowest row
        bool R(int x, int y) => p.At(x, y) >= 0;
        bool D(int x, int y) => R(x, y) || (R(x - 1, y) || R(x - 2, y)) && (R(x + 1, y) || R(x + 2, y)) || (R(x, y - 1) || R(x, y - 2)) && (R(x, y + 1) || R(x, y + 2));
        var top = new int[p.Width]; var foot = new int[p.Width];
        for (var x = 0; x < p.Width; x++)
        {
            top[x] = foot[x] = -1;
            var rows = Enumerable.Range(0, p.Height).Where(y => D(x, y)).ToList();
            // (a column of a stray pixel or two isn't the row)
            if (rows.Count < 3) continue;
            top[x] = rows[0]; foot[x] = rows[^1];
        }
        int first = Array.FindIndex(top, v => v >= 0), last = Array.FindLastIndex(top, v => v >= 0);
        if (first < 0) { m.Reading = "nothing drawn"; return; }

        // (a lobe's share of the picture as wide whichever way the row goes: a rock column across LBA1's view has three)
        var count = Math.Max(1, (int)Math.Round((last - first + 1) / (LobeWidth * 24)));
        var share = (last - first + 1.0) / count;
        var shareAlong = share / alongPx;
        int lobes = 0, hanging = 0;
        for (var k = 0; k < count; k++)
        {
            // ---- its share of the picture's columns, and the line of the block's ground that LBA1's view sees there: x - z = X, its depth
            // D = x + z from where it enters the block to where it leaves
            double from = first + k * share, to = from + share, cx = (from + to) / 2;
            var X = (cx - p.OriginX - 24) / 24;
            double dLo = Math.Max(bx0 * 2 - X, bz0 * 2 + X), dHi = Math.Min(bx1 * 2 - X, bz1 * 2 + X);
            if (dLo > dHi) continue;
            // (a number for the lobe that looks random, the same every build: 0..1)
            double Jitter(uint salt) => (((uint)k * 2654435761u + salt * 40503u) >> 16 & 255) / 255.0;
            // (by turns behind and in front, in the middle of the line's run)
            var inFront = k % 2 == 1;
            var dC = (dLo + dHi) / 2 + (inFront ? 1 : -1) * Math.Min(0.24, (dHi - dLo) * 0.15);
            float xc = (float)((X + dC) / 2), zc = (float)((dC - X) / 2);
            var centre = new Vector3(xc, 0, zc);
            // (how far across the bricks' cells reach from it either way)
            double Room(Vector3 w)
            {
                var r = 10.0;
                if (Math.Abs(w.X) > 1e-6) r = Math.Min(r, (w.X > 0 ? bx1 - xc : xc - bx0) / Math.Abs(w.X));
                if (Math.Abs(w.Z) > 1e-6) r = Math.Min(r, (w.Z > 0 ? bz1 - zc : zc - bz0) / Math.Abs(w.Z));
                return r;
            }
            var room = Math.Min(Room(across), Room(-across));

            // ---- how tall (the outline over the middle three fifths of its share), where its foot is (the lowest of its share)
            var columns = Enumerable.Range((int)Math.Floor(from), (int)Math.Ceiling(to) - (int)Math.Floor(from)).Where(x => x >= 0 && x < p.Width && top[x] >= 0).ToList();
            if (columns.Count == 0) continue;
            var middle = columns.Where(x => Math.Abs(x + 0.5 - cx) <= share * 0.3).DefaultIfEmpty(columns.MinBy(x => Math.Abs(x + 0.5 - cx))).ToList();
            var axisRow = p.OriginY + 15 + 12 * (xc + zc);
            var height = (axisRow - middle.Min(x => top[x])) / 15 - 0.04;
            var footRow = middle.Max(x => foot[x]) + 1;
            // (wider than its share -- neighbours overlap -- each a little more or less; in front a little lower)
            var a = shareAlong / 2 * (LobeWide + 0.2 * Jitter(2));
            var b = Math.Max(0.05, Math.Min(a, room - 0.02));
            // (its foot: on the block's floor when its flat bottom's nearest point there is about the picture's lowest, else rounded -- its
            // point the picture's lowest)
            var reach = 12 * Math.Sqrt(Math.Pow(a * (dir.X + dir.Z), 2) + Math.Pow(b * (across.X + across.Z), 2));
            var flatAt = (axisRow + reach - footRow) / 15;
            var hangs = flatAt > 0.3;
            var round = hangs || flatAt > 0.12;
            var bottom = round ? Math.Max(0, (axisRow - footRow) / 15) : 0;
            if (inFront) height = bottom + (height - bottom) * (0.94 + 0.06 * Jitter(1));
            if (height - bottom < 0.4) continue;
            // (its group: the block's column along the row its middle stands in -- the race track's doorways take the columns they cut)
            var group = m.Dx == m.Dz ? 0 : Math.Clamp((int)Math.Floor(m.Dx > m.Dz ? xc : zc), 0, Math.Max(m.Dx, m.Dz) - 1);

            // ---- its rings: (height, half width) from its foot up -- a hanging one's foot rounded to a point -- its top's rounded
            var dome = Math.Min(LobeDome * a * 2, (height - bottom) * (round ? 0.45 : 0.6));
            var baseH = height - dome;
            var rings = new List<(double H, double A)>();
            if (round)
            {
                var under = Math.Min(LobeDome * a * 2 * 0.6, (height - bottom) * 0.35);
                rings.Add((bottom, 0));
                rings.Add((bottom + under, a * 0.9));
                if (hangs) hanging++;
            }
            else rings.Add((bottom, a * 0.9));
            if (baseH > rings[^1].H + 1e-3) rings.Add((baseH, a));
            else baseH = rings[^1].H;
            rings.Add((baseH + (height - baseH) * Math.Sin(50 * Math.PI / 180), a * Math.Cos(50 * Math.PI / 180)));
            rings.Add((height, 0));
            var lobe = new Lobe(centre, dir, across, a, b, rings, baseH, (bx0, bx1, bz0, bz1), group);
            // ---- two tiles round it (u round from the row's way, v down): its body, from its foot to its top's start, and its top
            int Tile(double h0, double h1) => m.AddTile(32, h1 - h0 > 1 ? 32 : 16, (u, v) => lobe.On(u * 2 * Math.PI, h1 - v * (h1 - h0)), m.Paint, solid: true);
            lobe.Body = Tile(rings[0].H, baseH);
            lobe.Head = Tile(baseH, height);
            m.Lobes.Add(lobe);
            LobeFaces(m, lobe, double.NegativeInfinity, double.PositiveInfinity);
            lobes++;
        }
        m.Reading = $"{lobes} lobes along {(m.Dx > m.Dz ? "x" : m.Dz > m.Dx ? "z" : "the view's across")}, {shareAlong:0.00} cells apart" + (hanging > 0 ? $", {hanging} hanging" : "");
    }

    // A lobe of a row of rocks: its axis's foot, the row's way and across it, its half widths along and across, its rings (height, half
    // width along) from its foot up, where its top's dome starts, the bricks' cells' box it stays in, its group, and its tiles.
    public sealed class Lobe(Vector3 centre, Vector3 dir, Vector3 across, double a, double b, List<(double H, double A)> rings, double baseH,
        (float X0, float X1, float Z0, float Z1) box, int group)
    {
        public readonly List<(double H, double A)> Rings = rings;
        public readonly double BaseH = baseH;
        public readonly int Group = group;
        public int Body, Head;
        public double WidthAt(double h) => WidthAt(Rings, h);
        public static double WidthAt(List<(double H, double A)> rings, double h)
        {
            if (h <= rings[0].H) return rings[0].A;
            for (var i = 1; i < rings.Count; i++)
                if (h <= rings[i].H) return rings[i - 1].A + (rings[i].A - rings[i - 1].A) * (h - rings[i - 1].H) / Math.Max(1e-9, rings[i].H - rings[i - 1].H);
            return 0;
        }
        // a point of it: round from the row's way, at a height, as far out as the rings say (`w`, else its own width there)
        public Vector3 On(double angle, double h, double? w = null)
        {
            var r = w ?? WidthAt(h);
            var q = centre + new Vector3(0, (float)h, 0) + dir * (float)(r * Math.Cos(angle)) + across * (float)(r / a * b * Math.Sin(angle));
            return new Vector3(Math.Clamp(q.X, box.X0 + 0.01f, box.X1 - 0.01f), q.Y, Math.Clamp(q.Z, box.Z0 + 0.01f, box.Z1 - 0.01f));
        }
        public Vector3 Normal(double angle, double tilt) => Vector3.Normalize(dir * (float)Math.Cos(angle) + across * (float)Math.Sin(angle) + new Vector3(0, (float)tilt, 0));
        // the box it stands in (the block's cells, layers): its widest ring round its axis, in the bricks' cells, from its foot to its top
        public (Vector3 Lo, Vector3 Hi) Bounds()
        {
            var r = Rings.Max(g => g.A);
            float ex = (float)(Math.Abs(dir.X) * r + Math.Abs(across.X) * r / a * b), ez = (float)(Math.Abs(dir.Z) * r + Math.Abs(across.Z) * r / a * b);
            return (new Vector3(Math.Max(box.X0, centre.X - ex), (float)Rings[0].H, Math.Max(box.Z0, centre.Z - ez)),
                    new Vector3(Math.Min(box.X1, centre.X + ex), (float)Rings[^1].H, Math.Min(box.Z1, centre.Z + ez)));
        }
    }

    // A lobe's faces, 6 sides round, between its rings -- of the part of it between two heights (layers): where LBA1 placed only some of a
    // block's layers (Model.Without), the lobe is cut flat at the lowest (or highest) of them and closed there, as LBA1's picture of the
    // bricks it placed ends at their cells' outline -- its tiles as they are.
    private static void LobeFaces(Model m, Lobe lobe, double lo, double hi)
    {
        var rings = lobe.Rings;
        double bottom = rings[0].H, top = rings[^1].H;
        bool cutLow = false, cutHigh = false;
        if (lo > bottom + 1e-6 || hi < top - 1e-6)
        {
            double from = Math.Max(lo, bottom), to = Math.Min(hi, top);
            if (to - from < 0.3) return;
            cutLow = from > bottom + 1e-6; cutHigh = to < top - 1e-6;
            var cut = new List<(double H, double A)>();
            if (cutLow) cut.Add((from, lobe.WidthAt(from)));
            cut.AddRange(rings.Where(r => r.H > from + 1e-6 && r.H < to - 1e-6 || !cutLow && Math.Abs(r.H - bottom) < 1e-6 || !cutHigh && Math.Abs(r.H - top) < 1e-6));
            if (cutHigh) cut.Add((to, lobe.WidthAt(to)));
            cut = cut.OrderBy(r => r.H).ToList();
            // (its dome's start a ring of it, for the tiles' parting)
            if (lobe.BaseH > cut[0].H + 1e-6 && lobe.BaseH < cut[^1].H - 1e-6 && cut.All(r => Math.Abs(r.H - lobe.BaseH) > 1e-6))
            {
                cut.Add((lobe.BaseH, Lobe.WidthAt(cut, lobe.BaseH)));
                cut = cut.OrderBy(r => r.H).ToList();
            }
            rings = cut;
        }
        const int sides = 6;
        for (var r = 0; r + 1 < rings.Count; r++)
        {
            var (low, high) = (rings[r], rings[r + 1]);
            if (high.H - low.H < 1e-6) continue;
            var (tile, t0, t1) = high.H <= lobe.BaseH + 1e-9 ? (lobe.Body, lobe.Rings[0].H, lobe.BaseH) : (lobe.Head, lobe.BaseH, lobe.Rings[^1].H);
            double V(double h) => Math.Clamp((t1 - h) / Math.Max(1e-9, t1 - t0), 0, 1);
            for (var n = 0; n < sides; n++)
            {
                double a0 = 2 * Math.PI * n / sides, a1 = 2 * Math.PI * (n + 1) / sides, u0 = (double)n / sides, u1 = (double)(n + 1) / sides;
                // (out from the axis, tilted up or down as the lobe narrows or widens: cells and layers)
                var normal = lobe.Normal((a0 + a1) / 2, (low.A - high.A) / Math.Max(1e-6, high.H - low.H));
                var corners = new List<(Vector3, double, double)>
                {
                    (lobe.On(a0, high.H, high.A), u0, V(high.H)), (lobe.On(a1, high.H, high.A), u1, V(high.H)), (lobe.On(a1, low.H, low.A), u1, V(low.H)), (lobe.On(a0, low.H, low.A), u0, V(low.H)),
                };
                if (high.A < 1e-3) corners.RemoveAt(1);
                else if (low.A < 1e-3) corners.RemoveAt(3);
                Face(m, tile, normal, corners, group: lobe.Group);
            }
        }
        // (where it is cut, closed: a fan from its axis, its ring's colours)
        void Cap((double H, double A) ring, bool up)
        {
            if (ring.A < 1e-3) return;
            var (tile, t0, t1) = ring.H <= lobe.BaseH + 1e-9 ? (lobe.Body, lobe.Rings[0].H, lobe.BaseH) : (lobe.Head, lobe.BaseH, lobe.Rings[^1].H);
            var v = Math.Clamp((t1 - ring.H) / Math.Max(1e-9, t1 - t0), 0, 1);
            for (var n = 0; n < sides; n++)
            {
                double a0 = 2 * Math.PI * n / sides, a1 = 2 * Math.PI * (n + 1) / sides;
                Face(m, tile, up ? Vector3.UnitY : -Vector3.UnitY, new[] { (lobe.On(0, ring.H, 0), (n + 0.5) / sides, v), (lobe.On(a0, ring.H, ring.A), (double)n / sides, v), (lobe.On(a1, ring.H, ring.A), (n + 1.0) / sides, v) },
                    group: lobe.Group);
            }
        }
        if (cutLow) Cap(rings[0], false);
        if (cutHigh) Cap(rings[^1], true);
    }
}
