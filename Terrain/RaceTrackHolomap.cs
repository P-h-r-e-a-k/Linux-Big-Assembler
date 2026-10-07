using System.Buffers.Binary;
using System.IO;

namespace LBAAssembler.Terrain;

// The race track on the holomap. When the holomap zooms in on an island it shows a pre-rendered 640 x 480 picture of it (HOLOMAP.HQR entry
// 18 + 2 * island, in the game's own palette, RESS.HQR entry 0) and draws the arrows and Twinsen over it through the camera the next
// entry gives (HOLOPLAN.CPP InitHoloPlan: the island's target point, two angles and a distance; SetProjection(320, 240, 1024, 700, 700)).
// So the built road is drawn into the picture through that same camera: asphalt taking its shade from the picture's own light, red and
// white curbs, and the start line -- hidden where the island's ground stands between it and the camera (a depth buffer of the built
// ground), and the bridge deck drawn after the roads it passes over. Citadel Island has two pictures, in the storm and once it is over.
internal static class RaceTrackHolomap
{
    public const string File = "HOLOMAP.HQR";
    public const int FirstMap = 18;             // HOLO.H HQR_BEGIN_MAP
    public const int Width = 640, Height = 480;

    // The picture (and camera) entries of an island: its own, and for Citadel Island the fine-weather one (HOLOPLAN: island 12's slot);
    // Celebration Island with the statue has a picture of its own too (island 13's slot), which the race-track mode shows.
    // (Polar Island, island 12, has its picture after the retail ones: Polar.PolarHolomap)
    public static int[] Pictures(RaceTrackIsland island) =>
        island.IslandByte == LBAAssembler.Terrain.Polar.PolarIsland.IslandByte ? new[] { LBAAssembler.Terrain.Polar.PolarHolomap.PictureEntry }
        : island.IslandByte == 0 ? new[] { FirstMap, FirstMap + 2 * 12 }
        : island.Statue ? new[] { FirstMap + 2 * 13 }
        : new[] { FirstMap + 2 * island.IslandByte };

    // The game's palette's ramps (RESS.HQR entry 0): the greys (48-63) for asphalt and the white curb, the reds (64-79) for the red curb.
    private const int GreyRamp = 48, RedRamp = 64;

    internal sealed class Camera
    {
        private readonly double[,] m = new double[3, 3];
        private readonly double camX, camY, camZ, clip;

        public Camera(byte[] record)
        {
            int V(int i) => BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(i * 4));
            double tx = V(0) * 32768.0 + V(2), tz = V(1) * 32768.0 + V(3);
            int alpha = V(4) % 4096, beta = V(5) % 4096, distance = V(6);
            double S(int a) => Math.Sin((a & 4095) * 2 * Math.PI / 4096);
            double C(int a) => Math.Cos((a & 4095) * 2 * Math.PI / 4096);
            double sa = S(alpha), ca = C(alpha), sb = S(beta), cb = C(beta), sg = 0, cg = 1;
            // LIB386 InitMatrixStdF, gamma 0
            m[0, 0] = cg * cb; m[0, 1] = -sg; m[0, 2] = cg * sb;
            m[1, 0] = sg * ca * cb + sb * sa; m[1, 1] = ca * cg; m[1, 2] = sb * sg * ca - cb * sa;
            m[2, 0] = sa * sg * cb - ca * sb; m[2, 1] = sa * cg; m[2, 2] = sa * sg * sb + ca * cb;
            // SetFollowCamera: the target rotated, then pushed back by the distance
            var (rx, ry, rz) = Rotate(tx, 0, tz);
            camX = rx; camY = ry; camZ = rz + distance; clip = camZ - 1024;
        }

        private (double X, double Y, double Z) Rotate(double x, double y, double z) =>
            (m[0, 0] * x + m[0, 1] * y + m[0, 2] * z, m[1, 0] * x + m[1, 1] * y + m[1, 2] * z, m[2, 0] * x + m[2, 1] * y + m[2, 2] * z);

        // LongProjectPoint3D: screen x, y and the depth (distance in front of the camera); null behind it
        public (double X, double Y, double Depth)? Project(double x, double y, double z)
        {
            var (rx, ry, rz) = Rotate(x, y, z);
            if (rz > clip) return null;
            var f = 700.0 / (camZ - rz);
            return (320 + (rx - camX) * f, 240 - (ry - camY) * f, camZ - rz);
        }
    }

    // Draws the report's roads into the island's pictures in HOLOMAP.HQR (in the game folder), for the island file just built -- all of its
    // pictures, or those of `pictures` (Citadel Island with a track in each weather: each file's on its own weather's picture). Returns a
    // line for the log.
    public static string Draw(string gameDirectory, RaceTrackIsland island, IslandFile built, RaceTrackReport report, int[]? pictures = null)
    {
        var path = Path.Combine(gameDirectory, File);
        var hqr = System.IO.File.ReadAllBytes(path);
        var archive = HqrArchive.Open(path);
        var drawn = new List<string>();
        foreach (var entry in pictures ?? Pictures(island))
        {
            var picture = archive.Read(entry);
            var record = archive.Read(entry + 1);
            if (picture.Length < Width * Height || record.Length < 36) continue;
            var camera = new Camera(record);
            var depth = GroundDepth(built, camera);
            var pixels = picture[..(Width * Height)];
            var painted = 0;
            // the roads on the ground first, the deck (above the roads it crosses) last
            foreach (var deck in new[] { false, true })
                foreach (var road in report.Roads)
                    painted += DrawRoad(road, deck, camera, depth, pixels);
            painted += DrawStartLine(report, camera, depth, pixels);
            var result = (byte[])picture.Clone();
            pixels.CopyTo(result, 0);
            hqr = HqrWriter.ReplaceEntry(hqr, entry, HqrWriter.StoredEntry(result));
            drawn.Add($"entry {entry} ({painted} pixels)");
        }
        System.IO.File.WriteAllBytes(path, hqr);
        return $"the track drawn on {island.Name}'s holomap picture{(drawn.Count > 1 ? "s" : "")}: {string.Join(", ", drawn)}";
    }

    // The distance to the nearest ground at each pixel: every ground triangle of the island, projected and filled.
    private static float[] GroundDepth(IslandFile island, Camera camera)
    {
        var depth = new float[Width * Height];
        Array.Fill(depth, float.MaxValue);
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
        {
            var ox = cx * 32768.0; var oz = cz * 32768.0;
            var p = new (double X, double Y, double Depth)?[IslandCube.Vertices * IslandCube.Vertices];
            for (var vz = 0; vz < IslandCube.Vertices; vz++)
            for (var vx = 0; vx < IslandCube.Vertices; vx++)
                p[vz * IslandCube.Vertices + vx] = camera.Project(ox + vx * 512, cube.Height(vx, vz), oz + vz * 512);
            for (var vz = 0; vz < IslandCube.Cells; vz++)
            for (var vx = 0; vx < IslandCube.Cells; vx++)
            {
                var a = p[vz * IslandCube.Vertices + vx]; var b = p[vz * IslandCube.Vertices + vx + 1];
                var c = p[(vz + 1) * IslandCube.Vertices + vx]; var d = p[(vz + 1) * IslandCube.Vertices + vx + 1];
                if (a is null || b is null || c is null || d is null) continue;
                Fill(a.Value, b.Value, d.Value, (i, z) => { if (z < depth[i]) depth[i] = (float)z; });
                Fill(a.Value, d.Value, c.Value, (i, z) => { if (z < depth[i]) depth[i] = (float)z; });
            }
        }
        return depth;
    }

    // How far a road may be behind the ground drawn at its pixel and still show (it lies on that ground; the picture's own, older ground
    // is a little off where the build reshaped it).
    private const double DepthSlack = 900;

    private static int DrawRoad(TrackRoad r, bool deckOnly, Camera camera, float[] depth, byte[] pixels)
    {
        var painted = 0;
        var n = r.Count;
        var last = r.Closed ? n : n - 1;
        for (var i = 0; i < last; i++)
        {
            var j = (i + 1) % n;
            var isDeck = i < r.Deck.Length && (r.Deck[i] || r.Deck[j]);
            if (isDeck != deckOnly) continue;
            if (i < r.Gap.Length && (r.Gap[i] || r.Gap[j])) continue;            // the jump's gap: sand, the car in the air
            // (a cell's width either side of the middle: the asphalt, then the curb, red and white every 1.6 cells like the ground's)
            double nxi = -r.Tz[i], nzi = r.Tx[i], nxj = -r.Tz[j], nzj = r.Tx[j];
            var red = (int)Math.Floor(r.S[i] / 1.6) % 2 == 0;
            painted += Band(r, i, j, nxi, nzi, nxj, nzj, -r.AsphaltHalf, r.AsphaltHalf, Kind.Asphalt, camera, depth, pixels, isDeck);
            painted += Band(r, i, j, nxi, nzi, nxj, nzj, r.AsphaltHalf, r.CurbHalf, red ? Kind.Red : Kind.White, camera, depth, pixels, isDeck);
            painted += Band(r, i, j, nxi, nzi, nxj, nzj, -r.CurbHalf, -r.AsphaltHalf, red ? Kind.Red : Kind.White, camera, depth, pixels, isDeck);
        }
        return painted;
    }

    private enum Kind { Asphalt, Red, White }

    private static int Band(TrackRoad r, int i, int j, double nxi, double nzi, double nxj, double nzj, double from, double to, Kind kind,
        Camera camera, float[] depth, byte[] pixels, bool deck)
    {
        (double X, double Y, double Depth)? P(int k, double nx, double nz, double off) =>
            camera.Project((r.X[k] + nx * off) * 512, r.H[k], (r.Z[k] + nz * off) * 512);
        var a = P(i, nxi, nzi, from); var b = P(i, nxi, nzi, to); var c = P(j, nxj, nzj, to); var d = P(j, nxj, nzj, from);
        if (a is null || b is null || c is null || d is null) return 0;
        var painted = 0;
        void Put(int idx, double z)
        {
            // a deck stands above what it crosses: it is drawn wherever it is nearer than the roads under it
            if (!deck && z > depth[idx] + DepthSlack) return;
            pixels[idx] = Colour(kind, pixels[idx]);
            painted++;
        }
        Fill(a.Value, b.Value, c.Value, Put);
        Fill(a.Value, c.Value, d.Value, Put);
        return painted;
    }

    // The road's colours, lit like the picture: the pixel's own brightness picks the shade in the ramp.
    private static byte[]? palette;
    private static byte Colour(Kind kind, byte under)
    {
        var light = Luminance(under);
        return kind switch
        {
            Kind.Asphalt => (byte)(GreyRamp + 2 + Math.Round(light * 7)),      // mid to dark grey
            Kind.Red => (byte)(RedRamp + 5 + Math.Round(light * 8)),
            _ => (byte)(GreyRamp + 12 + Math.Round(light * 3)),
        };
    }

    public static void UsePalette(byte[] ress0) => palette = ress0.Length >= 768 ? ress0[..768] : null;

    private static double Luminance(byte index)
    {
        if (palette is null) return 0.6;
        var p = palette;
        var v = (0.3 * p[index * 3] + 0.59 * p[index * 3 + 1] + 0.11 * p[index * 3 + 2]) / (p.Take(768).Max() <= 63 ? 63.0 : 255.0);
        return Math.Clamp(v * 1.4, 0, 1);
    }

    private static int DrawStartLine(RaceTrackReport report, Camera camera, float[] depth, byte[] pixels)
    {
        if (report.StartLine.Count == 0) return 0;
        var (x, z, y, dx, dz) = report.StartLine[0];
        var nx = -dz; var nz = dx;
        var road = report.Roads.FirstOrDefault();
        var half = road?.AsphaltHalf ?? 3.5;
        var painted = 0;
        // a band half a cell each side of the line, across the asphalt
        var a = camera.Project((x + nx * -half - dx * 0.5) * 512, y, (z + nz * -half - dz * 0.5) * 512);
        var b = camera.Project((x + nx * half - dx * 0.5) * 512, y, (z + nz * half - dz * 0.5) * 512);
        var c = camera.Project((x + nx * half + dx * 0.5) * 512, y, (z + nz * half + dz * 0.5) * 512);
        var d = camera.Project((x + nx * -half + dx * 0.5) * 512, y, (z + nz * -half + dz * 0.5) * 512);
        if (a is null || b is null || c is null || d is null) return 0;
        void Put(int idx, double depthHere) { if (depthHere <= depth[idx] + DepthSlack) { pixels[idx] = Colour(Kind.White, pixels[idx]); painted++; } }
        Fill(a.Value, b.Value, c.Value, Put);
        Fill(a.Value, c.Value, d.Value, Put);
        return painted;
    }

    // Fills a projected triangle, calling `put` with each covered pixel's index and depth (interpolated).
    private static void Fill((double X, double Y, double Depth) a, (double X, double Y, double Depth) b, (double X, double Y, double Depth) c, Action<int, double> put)
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
            put(y * Width + x, w0 * a.Depth + w1 * b.Depth + w2 * c.Depth);
        }
    }
}
