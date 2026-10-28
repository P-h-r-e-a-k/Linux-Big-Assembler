using System.Numerics;

namespace LBAAssembler.Terrain.Temple;

// The statues.
internal static partial class TempleModels
{
    // the figures' voxels (cells, layers), the directions round a slice, and a slice this far off the straight line between the rings
    // either side of it (cells) is kept -- at most FigureRings rings a layer, and none further apart than a layer (a tile's height)
    private const double FigureCell = 1 / 12.0, FigureLayer = 1 / 8.0, FigureTolerance = 0.08, FigureRings = 2.5;
    private const int FigureSides = 12;

    // LBA1's statues -- the seated figures, the flame masks, the rabbit: a figure is the same either side of the way it faces, so its other
    // side's view is LBA1's picture mirrored. What both views leave of the block (a voxel whose point falls on the picture's figure from
    // LBA1's angle, and whose mirror image -- across the block's middle, along x or along z: the one leaving more, the way the figure faces
    // -- does too) is its shape: slice by slice (every eighth of a layer) a ring of FigureSides points round the slice's middle, each as far
    // out that way as the slice reaches; the rings kept where the shape turns (FigureTolerance). Each side's texels are the picture's where
    // the view that sees that side best (LBA1's, or its mirror image's: the picture at the mirrored point) sees them.
    private static void MakeFigure(Model m)
    {
        var p = m.Picture;
        // ---- the picture's figure, its dithering's gaps of two pixels closed
        var mask = new bool[p.Width * p.Height];
        bool R(int x, int y) => p.At(x, y) >= 0;
        for (var y = 0; y < p.Height; y++)
            for (var x = 0; x < p.Width; x++)
                mask[y * p.Width + x] = R(x, y) || (R(x - 1, y) || R(x - 2, y)) && (R(x + 1, y) || R(x + 2, y)) || (R(x, y - 1) || R(x, y - 2)) && (R(x, y + 1) || R(x, y + 2));
        bool Seen(Vector3 q)
        {
            var (sx, sy) = p.Screen(q);
            int x = (int)Math.Floor(sx), y = (int)Math.Floor(sy);
            return x >= 0 && y >= 0 && x < p.Width && y < p.Height && mask[y * p.Width + x];
        }
        Vector3 FlipX(Vector3 q) => new(m.Dx - q.X, q.Y, q.Z);
        Vector3 FlipZ(Vector3 q) => new(q.X, q.Y, m.Dz - q.Z);

        // ---- the voxels both views leave, the mirror the one leaving more
        int nx = (int)Math.Round(m.Dx / FigureCell), ny = (int)Math.Round(m.Dy / FigureLayer), nz = (int)Math.Round(m.Dz / FigureCell);
        Vector3 Voxel(int i, int j, int k) => new((float)((i + 0.5) * FigureCell), (float)((j + 0.5) * FigureLayer), (float)((k + 0.5) * FigureCell));
        bool[,,] Carve(Func<Vector3, Vector3> flip)
        {
            var v = new bool[nx, ny, nz];
            for (var i = 0; i < nx; i++) for (var j = 0; j < ny; j++) for (var k = 0; k < nz; k++) { var q = Voxel(i, j, k); v[i, j, k] = Seen(q) && Seen(flip(q)); }
            return v;
        }
        static int Count(bool[,,] v) { var n = 0; foreach (var b in v) if (b) n++; return n; }
        var byX = Carve(FlipX); var byZ = Carve(FlipZ);
        var mirrorX = Count(byX) >= Count(byZ);
        var solid = mirrorX ? byX : byZ;
        Func<Vector3, Vector3> flip = mirrorX ? FlipX : FlipZ;
        // (the mirror image's view, as LBA1's (1, 0, 1) mirrored)
        var view1 = Vector3.Normalize(new Vector3(1, 0, 1));
        var view2 = mirrorX ? Vector3.Normalize(new Vector3(-1, 0, 1)) : Vector3.Normalize(new Vector3(1, 0, -1));
        bool Inside(double x, double z, int j)
        {
            int i = (int)Math.Floor(x / FigureCell), k = (int)Math.Floor(z / FigureCell);
            return i >= 0 && k >= 0 && i < nx && k < nz && solid[i, j, k];
        }

        // ---- the slices: each the middle of its voxels and how far out it reaches each way round
        var slices = new List<(double Y, double Cx, double Cz, double[] R)>();
        for (var j = 0; j < ny; j++)
        {
            double sx = 0, sz = 0; var n = 0;
            for (var i = 0; i < nx; i++) for (var k = 0; k < nz; k++) if (solid[i, j, k]) { sx += (i + 0.5) * FigureCell; sz += (k + 0.5) * FigureCell; n++; }
            if (n < 2) continue;
            double cx = sx / n, cz = sz / n;
            var r = new double[FigureSides];
            for (var d = 0; d < FigureSides; d++)
            {
                var angle = 2 * Math.PI * d / FigureSides;
                double c = Math.Cos(angle), s = Math.Sin(angle), last = 0;
                for (var t = 0.0; ; t += FigureCell / 4)
                {
                    double x = cx + t * c, z = cz + t * s;
                    if (x < 0 || z < 0 || x > m.Dx || z > m.Dz) break;
                    if (Inside(x, z, j)) last = t;
                }
                r[d] = Math.Max(FigureCell / 2, last + FigureCell / 2);
            }
            slices.Add(((j + 0.5) * FigureLayer, cx, cz, r));
        }
        if (slices.Count < 2) { m.Reading = "no figure"; return; }
        // (smoothed against the voxels' steps: each reach 1-2-1 with the directions either side, then with the slices either side)
        for (var pass = 0; pass < 2; pass++)
            slices = slices.Select((s, i) =>
            {
                var r = new double[FigureSides];
                for (var d = 0; d < FigureSides; d++)
                    r[d] = pass == 0
                        ? (s.R[(d + FigureSides - 1) % FigureSides] + 2 * s.R[d] + s.R[(d + 1) % FigureSides]) / 4
                        : (slices[Math.Max(0, i - 1)].R[d] + 2 * s.R[d] + slices[Math.Min(slices.Count - 1, i + 1)].R[d]) / 4;
                return (s.Y, s.Cx, s.Cz, r);
            }).ToList();
        // (the bottom and the top slices out to the voxels' ends)
        slices[0] = (slices[0].Y - FigureLayer / 2, slices[0].Cx, slices[0].Cz, slices[0].R);
        slices[^1] = (slices[^1].Y + FigureLayer / 2, slices[^1].Cx, slices[^1].Cz, slices[^1].R);

        // ---- the rings kept: the slice furthest off the straight lines between those kept so far, one at a time, until none is further
        // than FigureTolerance or the figure has FigureRings a layer (and none are more than a layer apart)
        var keep = new SortedSet<int> { 0, slices.Count - 1 };
        double Off(int a, int i, int b)
        {
            var t = (slices[i].Y - slices[a].Y) / Math.Max(1e-9, slices[b].Y - slices[a].Y);
            double Lerp(double u, double v) => u + (v - u) * t;
            var d = Math.Max(Math.Abs(slices[i].Cx - Lerp(slices[a].Cx, slices[b].Cx)), Math.Abs(slices[i].Cz - Lerp(slices[a].Cz, slices[b].Cz)));
            for (var k = 0; k < FigureSides; k++) d = Math.Max(d, Math.Abs(slices[i].R[k] - Lerp(slices[a].R[k], slices[b].R[k])));
            return slices[b].Y - slices[a].Y > 1 ? d + 1 : d;
        }
        while (true)
        {
            var kept = keep.ToList();
            var (worst, at) = (0.0, -1);
            for (var s = 0; s + 1 < kept.Count; s++)
                for (var i = kept[s] + 1; i < kept[s + 1]; i++)
                    if (Off(kept[s], i, kept[s + 1]) is var d && d > worst) (worst, at) = (d, i);
            if (at < 0 || worst <= FigureTolerance || worst < 1 && keep.Count >= FigureRings * m.Dy + 1) break;
            keep.Add(at);
        }
        var rings = keep.Select(i => slices[i]).ToList();

        // ---- the faces: each band between two rings a tile each quarter round (u round, v down the band)
        Vector3 On(double angle, (double Y, double Cx, double Cz, double[] R) ring)
        {
            var f = angle / (2 * Math.PI) * FigureSides;
            var d0 = (int)Math.Floor(f) % FigureSides; var d1 = (d0 + 1) % FigureSides;
            var r = ring.R[d0] + (ring.R[d1] - ring.R[d0]) * (f - Math.Floor(f));
            return new Vector3((float)Math.Clamp(ring.Cx + r * Math.Cos(angle), 0.01, m.Dx - 0.01), (float)ring.Y, (float)Math.Clamp(ring.Cz + r * Math.Sin(angle), 0.01, m.Dz - 0.01));
        }
        (double Y, double Cx, double Cz, double[] R) Between((double Y, double Cx, double Cz, double[] R) a, (double Y, double Cx, double Cz, double[] R) b, double t)
            => (a.Y + (b.Y - a.Y) * t, a.Cx + (b.Cx - a.Cx) * t, a.Cz + (b.Cz - a.Cz) * t, a.R.Select((v, k) => v + (b.R[k] - v) * t).ToArray());
        // (a point's colour: the picture's where the view that sees its side best sees it. Its back, which neither sees -- the way both
        // views look from (the figure's front) turned round -- has its sides' colours: a point there the colour of the point of its ring
        // near the side it is nearer, a third as far round from it)
        var front = Vector3.Normalize(view1 + view2);
        var side = new Vector3(front.Z, 0, -front.X);
        int PaintAt(double angle, (double Y, double Cx, double Cz, double[] R) ring)
        {
            var normal = Radial(angle);
            if (Vector3.Dot(normal, front) < -0.2)
            {
                var toSide = Vector3.Dot(normal, side) >= 0 ? side : -side;
                var past = Math.Acos(Math.Clamp(Vector3.Dot(normal, toSide), -1, 1));
                var turned = Vector3.Normalize(toSide * (float)Math.Cos(past / 3) + front * (float)Math.Sin(past / 3));
                angle = Math.Atan2(turned.Z, turned.X);
                if (angle < 0) angle += 2 * Math.PI;
                normal = turned;
            }
            var q = On(angle, ring);
            return Vector3.Dot(normal, view2) > Vector3.Dot(normal, view1) ? p.Sample(flip(q)) : p.Sample(q);
        }
        static Vector3 Radial(double angle) => new((float)Math.Cos(angle), 0, (float)Math.Sin(angle));
        const int perQuarter = FigureSides / 4;
        for (var r = 0; r + 1 < rings.Count; r++)
        {
            var (lo, hi) = (rings[r], rings[r + 1]);
            for (var q = 0; q < 4; q++)
            {
                var q0 = q;
                var tile = m.AddTile(32, 16, (u, v) => PaintAt((q0 + u) * Math.PI / 2, Between(hi, lo, v)), solid: true);
                for (var n = 0; n < perQuarter; n++)
                {
                    double a0 = (q * perQuarter + n) * 2 * Math.PI / FigureSides, a1 = (q * perQuarter + n + 1) * 2 * Math.PI / FigureSides;
                    double u0 = (double)n / perQuarter, u1 = (double)(n + 1) / perQuarter;
                    var normal = Vector3.Normalize(Radial((a0 + a1) / 2) + new Vector3(0, (float)((lo.R.Average() - hi.R.Average()) / Math.Max(1e-6, hi.Y - lo.Y)), 0));
                    Face(m, tile, normal, new[] { (On(a0, hi), u0, 0.0), (On(a1, hi), u1, 0.0), (On(a1, lo), u1, 1.0), (On(a0, lo), u0, 1.0) });
                }
            }
        }
        // ---- its top (and its bottom where it stops over the block's floor): a fan from the middle, the slice from above
        void Cap((double Y, double Cx, double Cz, double[] R) ring, bool up)
        {
            var normal = up ? Vector3.UnitY : -Vector3.UnitY;
            var pts = Enumerable.Range(0, FigureSides).Select(d => On(2 * Math.PI * d / FigureSides, ring)).ToList();
            float x0 = pts.Min(v => v.X), x1 = pts.Max(v => v.X), z0 = pts.Min(v => v.Z), z1 = pts.Max(v => v.Z);
            var tile = m.AddTile(32, 32, (u, v) => new Vector3(x0 + (float)u * (x1 - x0), (float)ring.Y, z0 + (float)v * (z1 - z0)), q => up ? p.Sample(q) : p.Sample(q + new Vector3(0, 0.05f, 0)), solid: true);
            var centre = new Vector3((float)ring.Cx, (float)ring.Y, (float)ring.Cz);
            (double, double) Uv(Vector3 v) => ((v.X - x0) / Math.Max(1e-6, x1 - x0), (v.Z - z0) / Math.Max(1e-6, z1 - z0));
            for (var d = 0; d < FigureSides; d++)
            {
                var (a, b) = (pts[d], pts[(d + 1) % FigureSides]);
                var (ua, va) = Uv(a); var (ub, vb) = Uv(b); var (uc, vc) = Uv(centre);
                Face(m, tile, normal, new[] { (centre, uc, vc), (a, ua, va), (b, ub, vb) });
            }
        }
        Cap(rings[^1], true);
        if (rings[0].Y > 0.1) Cap(rings[0], false);
        m.Reading = $"mirrored along {(mirrorX ? "x" : "z")} ({Count(solid)} voxels, {Count(mirrorX ? byZ : byX)} the other way), {rings.Count} rings of {FigureSides}";
    }
}
