namespace LBAAssembler.Terrain;

// An island's holomap picture drawn from its ground, for an island the game has no picture of (HOLOMAP.HQR entry 18 + 2 * island: the
// 640 x 480 picture the holomap zooms in on, in the game's own palette, RESS.HQR entry 0; the next entry is the camera it was drawn
// through, which the holomap then draws the arrows and Twinsen with -- RaceTrackHolomap.Camera). The retail pictures are pre-rendered
// with the decors; this draws the ground alone: every textured triangle of every cube the map shows, its atlas texture shaded by the
// cube's baked light, nearest to the game's palette, over a background (the sea of another picture) where there is no ground.
internal static class HolomapPicture
{
    private const int Width = RaceTrackHolomap.Width, Height = RaceTrackHolomap.Height;
    private static readonly int[][] HalfCorners = { new[] { 0, 1, 2 }, new[] { 2, 3, 0 }, new[] { 3, 0, 1 }, new[] { 1, 2, 3 } };
    private static readonly (int X, int Z)[] CornerOffsets = { (0, 0), (0, 1), (1, 1), (1, 0) };

    // `islandPalette`: the island's own (768 bytes, 0..255 each); `gamePalette`: RESS.HQR entry 0's; `camera`: the 9-int record;
    // `background`: 640 x 480 in the game's palette, drawn over. Sea cells (game code 1, height 0) are left to the background.
    // `solids`: boxes to draw as well, in world units, each a flat colour (8-bit RGB) on its top and darker on its sides -- what the
    // ground alone doesn't show (Polar Island's rocky peak, built of objects).
    public static byte[] Draw(IslandFile island, byte[] islandPalette, byte[] gamePalette, byte[] camera, byte[] background,
        IEnumerable<(double X0, double Z0, double X1, double Z1, double Y0, double Y1, (double R, double G, double B) Colour)>? solids = null)
    {
        var cam = new RaceTrackHolomap.Camera(camera);
        var pixels = (byte[])background.Clone();
        var depth = new float[Width * Height];
        Array.Fill(depth, float.MaxValue);
        var nearest = new Dictionary<int, byte>();
        byte ToGame(double r, double g, double b)
        {
            int ri = Math.Clamp((int)r, 0, 255), gi = Math.Clamp((int)g, 0, 255), bi = Math.Clamp((int)b, 0, 255);
            var key = (ri >> 2) << 12 | (gi >> 2) << 6 | (bi >> 2);
            if (nearest.TryGetValue(key, out var hit)) return hit;
            var best = 0; var bd = int.MaxValue;
            for (var i = 1; i < 256; i++)
            {
                int dr = gamePalette[i * 3] - ri, dg = gamePalette[i * 3 + 1] - gi, db = gamePalette[i * 3 + 2] - bi;
                var d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
                if (d < bd) { bd = d; best = i; }
            }
            return nearest[key] = (byte)best;
        }

        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
        {
            var ox = cx * 32768.0; var oz = cz * 32768.0;
            var projected = new (double X, double Y, double Depth)?[IslandCube.Vertices * IslandCube.Vertices];
            for (var vz = 0; vz < IslandCube.Vertices; vz++)
            for (var vx = 0; vx < IslandCube.Vertices; vx++)
                projected[vz * IslandCube.Vertices + vx] = cam.Project(ox + vx * 512, cube.Height(vx, vz), oz + vz * 512);
            for (var z = 0; z < IslandCube.Cells; z++)
            for (var x = 0; x < IslandCube.Cells; x++)
            for (var half = 0; half < 2; half++)
            {
                var polygon = new IslandPolygon(cube.Polygon(x, z, half));
                if (polygon.CodeJeu == 1 && cube.Height(x, z) == 0 && cube.Height(x + 1, z + 1) == 0) continue;
                if (polygon.TexFlag == 0) continue;
                var (page, index) = island.GroundTextureOf(polygon);
                if (index * 6 + 6 > cube.TextureDefs.Length) continue;
                var texture = island.GroundPage(page);
                var diagonal = new IslandPolygon(cube.Polygon(x, z, 0)).Diagonal;
                var corners = HalfCorners[(diagonal ? 2 : 0) + half];
                var p = new (double X, double Y, double Depth)[3];
                var u = new double[3]; var v = new double[3]; var light = new double[3];
                var skip = false;
                for (var k = 0; k < 3; k++)
                {
                    var (dx, dz) = CornerOffsets[corners[k]];
                    if (projected[(z + dz) * IslandCube.Vertices + x + dx] is not { } q) { skip = true; break; }
                    p[k] = q;
                    u[k] = cube.TextureDefs[index * 6 + k * 2] / 256.0;
                    v[k] = cube.TextureDefs[index * 6 + k * 2 + 1] / 256.0;
                    light[k] = cube.Light(x + dx, z + dz) / 15.0;
                }
                if (skip) continue;
                Fill(p[0], p[1], p[2], (i, d, w0, w1, w2) =>
                {
                    if (d >= depth[i]) return;
                    depth[i] = (float)d;
                    var tu = (int)Math.Clamp(w0 * u[0] + w1 * u[1] + w2 * u[2], 0, 255);
                    var tv = (int)Math.Clamp(w0 * v[0] + w1 * v[1] + w2 * v[2], 0, 255);
                    var c = texture[tv * 256 + tu];
                    // (a texture over a flat colour -- the engine's incrust, the race track's kerbs: its colour 0 is see-through)
                    if (c == 0 && polygon.PolyFlag != 0) c = (byte)((polygon.Bank << 4) + 11);
                    // (the engine's light ramps run a colour from dark to bright: brightness 0..15 taken as 0.3..1.25 of the atlas colour)
                    var shade = 0.3 + 0.95 * (w0 * light[0] + w1 * light[1] + w2 * light[2]);
                    pixels[i] = ToGame(islandPalette[c * 3] * shade, islandPalette[c * 3 + 1] * shade, islandPalette[c * 3 + 2] * shade);
                });
            }
        }
        foreach (var (x0, z0, x1, z1, y0, y1, (r, g, b)) in solids ?? Enumerable.Empty<(double, double, double, double, double, double, (double, double, double))>())
        {
            // its top and four sides, each two triangles
            var faces = new (double X, double Y, double Z)[][]
            {
                new[] { (x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1) },
                new[] { (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0) },
                new[] { (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1) },
                new[] { (x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0) },
                new[] { (x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0) },
            };
            for (var f = 0; f < faces.Length; f++)
            {
                var shade = f == 0 ? 1.0 : f <= 2 ? 0.7 : 0.55;
                var colour = ToGame(r * shade, g * shade, b * shade);
                var q = faces[f].Select(v => cam.Project(v.X, v.Y, v.Z)).ToArray();
                if (q.Any(v => v is null)) continue;
                foreach (var (a, b2, c) in new[] { (0, 1, 2), (0, 2, 3) })
                    Fill(q[a]!.Value, q[b2]!.Value, q[c]!.Value, (i, d, _, _, _) =>
                    {
                        if (d >= depth[i]) return;
                        depth[i] = (float)d; pixels[i] = colour;
                    });
            }
        }
        return pixels;
    }

    // A calm sea in the colours of another island's picture (its region x0..x1, y0..y1 all sea): smooth noise over the picture, each
    // pixel given the sea colour at the same share of that region's pixels (its colours taken dark to light, as often as the region has
    // them), so it has the retail sea's colouring without the waves a patch of it would repeat.
    public static byte[] SeaBackground(byte[] picture, byte[] gamePalette, int x0, int y0, int x1, int y1)
    {
        var sample = new List<byte>();
        for (var y = y0; y <= y1; y++) for (var x = x0; x <= x1; x++) sample.Add(picture[y * Width + x]);
        double Luma(byte c) => 0.3 * gamePalette[c * 3] + 0.59 * gamePalette[c * 3 + 1] + 0.11 * gamePalette[c * 3 + 2];
        var ordered = sample.OrderBy(Luma).ToArray();
        var random = new Random(1);
        // value noise: a coarse and a fine lattice of random values, smoothly interpolated
        double[,] Lattice(int n) { var l = new double[n + 2, n + 2]; for (var i = 0; i < n + 2; i++) for (var j = 0; j < n + 2; j++) l[i, j] = random.NextDouble(); return l; }
        double Sample(double[,] l, int n, double u, double v)
        {
            double fx = u * n, fy = v * n; int ix = (int)fx, iy = (int)fy; double tx = fx - ix, ty = fy - iy;
            tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
            return l[ix, iy] * (1 - tx) * (1 - ty) + l[ix + 1, iy] * tx * (1 - ty) + l[ix, iy + 1] * (1 - tx) * ty + l[ix + 1, iy + 1] * tx * ty;
        }
        var coarse = Lattice(12); var fine = Lattice(48); var grain = Lattice(160);
        var values = new double[Width * Height];
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            values[y * Width + x] = 0.55 * Sample(coarse, 12, x / (double)Width, y / (double)Height) + 0.3 * Sample(fine, 48, x / (double)Width, y / (double)Height)
                                    + 0.15 * Sample(grain, 160, x / (double)Width, y / (double)Height);
        // (each pixel's rank among all of them picks the colour at the same rank in the region)
        var ranks = Enumerable.Range(0, values.Length).OrderBy(i => values[i]).ToArray();
        var result = new byte[Width * Height];
        for (var r = 0; r < ranks.Length; r++) result[ranks[r]] = ordered[(int)((long)r * ordered.Length / ranks.Length)];
        return result;
    }

    // Fills a projected triangle: each covered pixel's index, depth and the corners' weights.
    private static void Fill((double X, double Y, double Depth) a, (double X, double Y, double Depth) b, (double X, double Y, double Depth) c,
        Action<int, double, double, double, double> put)
    {
        var minX = (int)Math.Max(0, Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
        var maxX = (int)Math.Min(Width - 1, Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
        var minY = (int)Math.Max(0, Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
        var maxY = (int)Math.Min(Height - 1, Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
        if (minX > maxX || minY > maxY) return;
        var area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (Math.Abs(area) < 1e-9) return;
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            double px = x + 0.5, py = y + 0.5;
            var w0 = ((b.X - px) * (c.Y - py) - (b.Y - py) * (c.X - px)) / area;
            var w1 = ((c.X - px) * (a.Y - py) - (c.Y - py) * (a.X - px)) / area;
            var w2 = 1 - w0 - w1;
            if (w0 < 0 || w1 < 0 || w2 < 0) continue;
            put(y * Width + x, w0 * a.Depth + w1 * b.Depth + w2 * c.Depth, w0, w1, w2);
        }
    }
}
