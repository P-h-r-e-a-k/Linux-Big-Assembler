using System.Numerics;
using LbaBodyStudio;
using LBAAssembler.Terrain.Palace;

namespace LBAAssembler.Terrain.Snow;

// The snow island's things, each a body of flat-coloured faces lit by the cubes' sun (SnowMesh) in the island's OBL, placed as island
// objects: drawn-only decors (their box's top under its bottom: the engine draws them by their footprint, a copy in each cube their box
// reaches, once a frame) and, for what Twinsen can't walk through, collision boxes as decors of the empty body 0 -- the engine collides
// only with the decors of the cube the hero is in, so each box goes in every cube it reaches. What they are, from LBA1's Hamalayi:
//   - puffy snow-laden trees (scenes 72, 92, 96): a brown trunk splayed at a mound of snow, a crown of snowballs heaped to a point;
//   - snowmen: with a top hat, a bucket, the Rabbibunnies' ears (scene 92's), and a little one;
//   - the ski slope's chair lift: steel lattice pylons in LBA1's rusty brown with a crossarm and its wheels, the cables and their chairs, a
//     hut at each end with the bullwheel; the slalom gates down the slope; a fence round the foot's hut (scene 96's);
//   - the Rabbibunnies' village: stone domes (scene 15), a carrot patch, dead trees;
//   - mauve rock pillars (15, 81, 92) by the pond and on the massif's cliffs, ice spikes along the pond's rock wall (92);
//   - the harbour: a pier, a pink concrete bunker with a snow-capped roof, crates, teal barrels, lamp tripods, a gantry crane (9, 72);
//   - the canyon's mine rail up its floor and a cart at its foot (62-69), the prison's pink concrete gate at its head with Funfrock's bust
//     over it (71);
//   - ice floes on the sea round the shore (9, 72); a flag on the peak.
internal static class SnowObjects
{
    public const int EmptyBody = 0, NoBoxTop = BrickBuilding.NoBoxTop;
    public const int MaxPerCube = IslandDecors.MaxPerCubeOwnEngine;
    // (a thing seen from further -- a pylon, the cables -- has a copy in each cube within this of its box: the engine draws the 3 x 3 cubes
    // round the camera's)
    public const double FarReach = IslandFile.CubeSize;

    // colours (8-bit RGB, before the light): LBA1's Hamalayi's, from its bricks (HamalayiStudy)
    public static readonly Rgb Snow = new(205, 234, 240), SnowShade = new(176, 214, 222), Trunk = new(139, 87, 47), TrunkDark = new(92, 62, 34),
        Steel = new(96, 72, 64), SteelDark = new(64, 52, 50), Concrete = new(196, 176, 188), ConcreteDark = new(150, 136, 144), Rock = new(112, 92, 102),
        RockDark = new(86, 72, 80), Coal = new(34, 32, 38), Carrot = new(239, 123, 83), Leaf = new(86, 150, 62), Wood = new(158, 110, 68),
        WoodDark = new(120, 82, 48), Teal = new(51, 103, 115), Ice = new(186, 240, 250), IceDeep = new(120, 205, 215), Red = new(200, 44, 40),
        Blue = new(48, 84, 200), Green = new(58, 150, 72), Hat = new(40, 38, 46), Bucket = new(112, 124, 136), Lamp = new(250, 220, 120),
        Cable = new(44, 44, 50), Stone = new(150, 132, 128), StoneDark = new(118, 104, 104), Pink = new(232, 160, 170);

    public sealed record Built(List<byte[]> Bodies, int Decors, int Boxes, List<string> Log);

    internal sealed class Placer(IslandFile island, SnowShading shading, double[,] heights)
    {
        public readonly List<byte[]> Bodies = new() { RaceTrackPipes.Write(new List<Vector3> { Vector3.Zero }, new List<Face>(), lit: false) };
        public int Decors, Boxes, Full;
        public readonly Dictionary<string, int> Counts = new();
        public readonly Dictionary<int, (int Body, SnowMesh Mesh)> Snowmen = new();
        public SnowShading Shading => shading;

        public SnowMesh Mesh() => new(shading);

        public int Add(SnowMesh mesh, string what) { Bodies.Add(mesh.Write(what)); return Bodies.Count - 1; }

        // the ground's height under a point (cells): the vertices' heights between them
        public double Ground(double u, double v)
        {
            u = Math.Clamp(u, 0, SnowDesign.Width - 0.001); v = Math.Clamp(v, 0, SnowDesign.Depth - 0.001);
            int x = (int)u, z = (int)v; double fx = u - x, fz = v - z;
            return (heights[x, z] * (1 - fx) + heights[x + 1, z] * fx) * (1 - fz) + (heights[x, z + 1] * (1 - fx) + heights[x + 1, z + 1] * fx) * fz;
        }
        // ... the lowest of it under a round thing's foot (the thing set into a slope, not standing out of it)
        public double Foot(double u, double v, double radiusCells)
        {
            var low = Ground(u, v);
            for (var k = 0; k < 8; k++) low = Math.Min(low, Ground(u + radiusCells * Math.Cos(k * Math.PI / 4), v + radiusCells * Math.Sin(k * Math.PI / 4)));
            return low;
        }

        // A thing: `body` drawn at cell (u, v), `y` high (world units), turned `beta` (1/4096 turns); its box as its points reach (round
        // things turned: as far as their furthest point, every way); `reach`: copies in the cubes within that too.
        public void Place(int body, SnowMesh mesh, double u, double v, double y, int beta = 0, double reach = 0, string? what = null)
        {
            var (lo, hi) = mesh.Bounds();
            double wx = SnowDesign.WorldX(u), wz = SnowDesign.WorldZ(v);
            double x0 = lo.X, x1 = hi.X, z0 = lo.Z, z1 = hi.Z;
            if (beta != 0)
            {
                var r = mesh.Points.Max(p => Math.Sqrt(p.X * p.X + p.Z * p.Z));
                x0 = z0 = -r; x1 = z1 = r;
            }
            var (placed, full) = BrickBuilding.AddToCubes(island, body, beta & 0xFFF, wx, (int)Math.Round(y), wz, wx + x0, wx + x1, (int)Math.Floor(y + lo.Y), NoBoxTop,
                wz + z0, wz + z1, MaxPerCube, reach);
            Decors += placed; Full += full;
            if (what is not null) Counts[what] = Counts.TryGetValue(what, out var n) ? n + 1 : 1;
        }

        // What Twinsen can't walk through: a box (world units, absolute), in every cube it reaches.
        public void Solid(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            var (placed, full) = BrickBuilding.AddToCubes(island, EmptyBody, 0, (x0 + x1) / 2, (int)Math.Round(y0), (z0 + z1) / 2, x0, x1, (int)Math.Floor(y0), (int)Math.Ceiling(y1),
                z0, z1, MaxPerCube);
            if (placed > 0) Boxes++;
            Full += full;
        }

        // ... a box round a point (cells), `half` across and `height` high from `y`
        public void Solid(double u, double v, double y, double half, double height) =>
            Solid(SnowDesign.WorldX(u) - half, y, SnowDesign.WorldZ(v) - half, SnowDesign.WorldX(u) + half, y + height, SnowDesign.WorldZ(v) + half);
    }

    public static Built Build(IslandFile island, SnowShading shading, double[,] heights)
    {
        var at = new Placer(island, shading, heights);
        var log = new List<string>();
        // the places taken: things keep clear of each other and of the ways (cells)
        var taken = new List<(double U, double V, double R)>();
        bool Clear(double u, double v, double r) => taken.All(t => SnowDesign.Dist(u, v, (t.U, t.V)) > t.R + r);

        Harbour(at, taken);
        Village(at, taken);
        Pond(at, taken);
        Canyon(at, taken);
        Ski(at, taken, log);
        Summit(at, taken);
        Floes(at);
        Trees(at, taken, Clear, log);
        Pillars(at, taken, Clear);

        log.Add($"the things: {at.Bodies.Count} bodies, {at.Decors} decors drawn, {at.Boxes} collision boxes" + (at.Full > 0 ? $" ({at.Full} copies left out: cubes full)" : "") +
                "; " + string.Join(", ", at.Counts.OrderBy(k => k.Key).Select(k => $"{k.Value} {k.Key}")));
        return new Built(at.Bodies, at.Decors, at.Boxes, log);
    }

    // ---- the bodies

    // A puffy snow-laden tree, `s` times the usual size (some 3,000 high): a trunk splayed at a mound of snow, a crown of snowballs.
    public static SnowMesh Tree(Placer at, double s, int seed)
    {
        var m = at.Mesh();
        float S(double x) => (float)(x * s);
        m.Cylinder(Vector3.Zero, new Vector3(0, S(1150), 0), 170 * s, 105 * s, 6, Trunk, capA: false);
        for (var k = 0; k < 3; k++)
        {
            var a = k * 2 * Math.PI / 3 + seed;
            m.Beam(new Vector3(0, S(320), 0), new Vector3(S(330 * Math.Cos(a)), 0, S(330 * Math.Sin(a))), 110 * s, 90 * s, TrunkDark);
        }
        m.Ellipsoid(Vector3.Zero, 400 * s, 170 * s, 400 * s, 4, 8, Snow, below: false);
        m.Ellipsoid(new Vector3(0, S(1950), 0), 520 * s, 880 * s, 520 * s, 5, 8, SnowShade);
        var levels = new (double Y, int Count, double Out, double R)[] { (1350, 6, 430, 420), (1900, 5, 340, 370), (2400, 3, 210, 310), (2820, 1, 0, 260) };
        var lump = 0;
        foreach (var (y, count, outward, r) in levels)
            for (var k = 0; k < count; k++)
            {
                var a = k * 2 * Math.PI / count + seed * 0.7 + y;
                var c = new Vector3(S(outward * Math.Cos(a)), S(y), S(outward * Math.Sin(a)));
                var shade = Snow.Mix(SnowShade, Noise.Of(seed, lump++, 77) * 0.5);
                m.Ellipsoid(c, r * s, r * s * 0.9, r * s, 4, 6, shade, (i, j) => 0.92 + 0.12 * Noise.Of(i * 7 + j, lump, seed));
            }
        return m;
    }

    // A snowman facing +z: three snowballs, coal eyes, buttons and smile, a carrot nose, stick arms; `kind` 0 a top hat and a red scarf, 1 a
    // bucket and a green scarf, 2 the Rabbibunnies' ears and a blue scarf, 3 a little one with no hat.
    public static SnowMesh Snowman(Placer at, int kind)
    {
        var m = at.Mesh();
        var s = kind == 3 ? 0.62 : 1.0;
        float S(double x) => (float)(x * s);
        Func<int, int, double> lumpy = (i, j) => 0.95 + 0.08 * Noise.Of(i, j, 13 + kind);
        m.Ellipsoid(new Vector3(0, S(470), 0), 560 * s, 500 * s, 560 * s, 6, 10, Snow, lumpy);
        m.Ellipsoid(new Vector3(0, S(1170), 0), 400 * s, 370 * s, 400 * s, 6, 10, Snow, lumpy);
        m.Ellipsoid(new Vector3(0, S(1690), 0), 290 * s, 280 * s, 290 * s, 5, 8, Snow);
        // the face (+z): eyes, a carrot nose, a smile of coal
        foreach (var x in new[] { -100, 100 }) m.Box(new Vector3(S(x - 32), S(1745), S(232)), new Vector3(S(x + 32), S(1810), S(282)), Coal, bottom: true);
        m.Cylinder(new Vector3(0, S(1690), S(270)), new Vector3(S(15), S(1680), S(560)), 62 * s, 0, 5, Carrot);
        for (var k = -2; k <= 2; k++)
        {
            var a = k * 0.32;
            m.Box(new Vector3(S(150 * Math.Sin(a) - 22), S(1585 - 40 * Math.Cos(a)), S(240)), new Vector3(S(150 * Math.Sin(a) + 22), S(1625 - 40 * Math.Cos(a)), S(275)), Coal, bottom: true);
        }
        foreach (var y in new[] { 1020, 1170, 1320 }) m.Box(new Vector3(S(-36), S(y - 36), S(345)), new Vector3(S(36), S(y + 36), S(395)), Coal, bottom: true);
        // stick arms, a twig at each hand
        foreach (var side in new[] { -1, 1 })
        {
            var hand = new Vector3(S(side * 920), S(1620), S(80));
            m.Beam(new Vector3(S(side * 330), S(1280), 0), hand, 55 * s, 55 * s, TrunkDark);
            m.Beam(hand, hand + new Vector3(S(side * 170), S(160), S(20)), 40 * s, 40 * s, TrunkDark);
            m.Beam(hand, hand + new Vector3(S(side * 200), S(-40), S(-40)), 40 * s, 40 * s, TrunkDark);
        }
        if (kind == 3) return m;
        var scarf = kind switch { 0 => Red, 1 => Green, _ => Blue };
        m.Cylinder(new Vector3(0, S(1400), 0), new Vector3(0, S(1500), 0), 330, 310, 10, scarf);
        m.Beam(new Vector3(S(150), S(1440), S(270)), new Vector3(S(240), S(1060), S(330)), 130, 40, scarf);
        if (kind == 0)
        {
            m.Cylinder(new Vector3(0, S(1930), 0), new Vector3(0, S(1970), 0), 310, 310, 10, Hat);
            m.Cylinder(new Vector3(0, S(1970), 0), new Vector3(0, S(2330), 0), 205, 215, 10, Hat);
            m.Cylinder(new Vector3(0, S(2000), 0), new Vector3(0, S(2060), 0), 212, 214, 10, Red, capA: false, capB: false);
        }
        else if (kind == 1) m.Cylinder(new Vector3(0, S(1900), S(-20)), new Vector3(S(30), S(2230), S(-60)), 245, 195, 10, Bucket);
        else
            foreach (var side in new[] { -1, 1 })
            {
                m.Ellipsoid(new Vector3(S(side * 130), S(2150), 0), 85, 330, 110, 5, 6, Snow);
                m.Box(new Vector3(S(side * 130 - 35), S(2000), S(95)), new Vector3(S(side * 130 + 35), S(2330), S(108)), Pink, bottom: true);
            }
        return m;
    }

    // A steel lattice pylon (LBA1's, scene 96's), `height` to its crossarm, the cable along +z (turned to the lift's way), the crossarm's
    // wheels `half` either side.
    public static SnowMesh Pylon(Placer at, double height, double half, double turn)
    {
        var m = at.Mesh();
        m.Turn(turn);
        float H = (float)height;
        Vector3 Leg(int i, double f) { var b = 380 - 230 * f; return new Vector3((float)((i & 1) == 0 ? -b : b), (float)(f * height), (float)((i & 2) == 0 ? -b : b)); }
        int[] around = { 0, 1, 3, 2, 0 };
        for (var i = 0; i < 4; i++) m.Beam(Leg(i, 0), Leg(i, 1), 90, 90, Steel);
        const int levels = 5;
        for (var l = 1; l <= levels; l++)
        {
            double f0 = (l - 1.0) / levels, f1 = (double)l / levels;
            for (var k = 0; k < 4; k++)
            {
                int a = around[k], b = around[k + 1];
                m.Beam(Leg(a, f1), Leg(b, f1), 55, 55, SteelDark);
                m.Beam(Leg(a, f0), Leg(b, f1), 40, 40, Steel);
            }
        }
        m.Beam(new Vector3(-(float)(half + 250), H + 120, 0), new Vector3((float)(half + 250), H + 120, 0), 180, 220, Steel);
        foreach (var side in new[] { -1, 1 })
        {
            var x = (float)(side * half);
            m.Cylinder(new Vector3(x, H - 40, -260), new Vector3(x, H - 40, 260), 0, 0, 3, SteelDark);
            m.Cylinder(new Vector3(x - 50, H - 40, -230), new Vector3(x + 50, H - 40, -230), 150, 150, 8, Coal);
            m.Cylinder(new Vector3(x - 50, H - 40, 230), new Vector3(x + 50, H - 40, 230), 150, 150, 8, Coal);
            m.Beam(new Vector3(x, H + 60, 0), new Vector3(x, H - 40, 0), 70, 70, SteelDark);
        }
        return m;
    }

    // A hut as LBA1's ski resort's (scene 96): pale walls, a dark arched doorway (+z), the roof's snow overhanging; `w` x `d` x `h`.
    public static void Hut(SnowMesh m, double w, double d, double h, Rgb wall)
    {
        float W = (float)(w / 2), D = (float)(d / 2), Hh = (float)h;
        m.Box(new Vector3(-W, 0, -D), new Vector3(W, Hh, D), wall);
        m.Box(new Vector3(-W - 120, Hh, -D - 120), new Vector3(W + 120, Hh + 260, D + 120), Snow);
        m.Box(new Vector3(-280, 0, D), new Vector3(280, (float)(h * 0.62), D + 30), Coal);
        m.Cylinder(new Vector3(0, (float)(h * 0.62), D), new Vector3(0, (float)(h * 0.62), D + 30), 280, 280, 8, Coal, capA: false);
        m.Box(new Vector3(-W - 10, (float)(h * 0.45), -280), new Vector3(-W, (float)(h * 0.7), 280), new Rgb(70, 110, 130));
    }

    // The bullwheel the cable turns round, over a station: a wheel lying flat on its post, `half` across to the cable.
    public static void Bullwheel(SnowMesh m, double half, double height)
    {
        m.Beam(new Vector3(0, 0, 0), new Vector3(0, (float)height, 0), 260, 260, Steel);
        m.Cylinder(new Vector3(0, (float)height, 0), new Vector3(0, (float)height + 140, 0), half, half, 16, SteelDark);
        m.Beam(new Vector3(-(float)half, (float)height + 70, 0), new Vector3((float)half, (float)height + 70, 0), 120, 120, Steel);
    }

    // A stone dome of the Rabbibunnies' (scene 15's), radius `r`, its doorway facing `turn`.
    public static SnowMesh Dome(Placer at, double r, double turn, int seed)
    {
        var m = at.Mesh();
        m.Turn(turn);
        m.Ellipsoid(Vector3.Zero, r, r * 0.86, r, 10, 16, Stone, (i, j) => 0.97 + 0.05 * Noise.Of(i, j, seed), below: false,
            colourAt: (i, j) => (i + j) % 2 == 0 ? Stone : StoneDark.Mix(Stone, Noise.Of(i, j, seed + 1) * 0.6));
        // its doorway, a porch of stone over it, snow on the top
        float R = (float)r;
        m.Box(new Vector3(-0.24f * R, 0, 0.72f * R), new Vector3(0.24f * R, 0.5f * R, 0.98f * R), StoneDark);
        m.Box(new Vector3(-0.16f * R, 0, 0.98f * R), new Vector3(0.16f * R, 0.4f * R, 1.0f * R), Coal);
        m.Ellipsoid(new Vector3(0, 0.84f * R, 0), 0.45 * r, 0.1 * r, 0.45 * r, 4, 10, Snow, below: false);
        return m;
    }

    // A mauve rock pillar (LBA1's), stacked lumps `height` high, snow on its top.
    public static SnowMesh Pillar(Placer at, double height, int seed)
    {
        var m = at.Mesh();
        double y = 0; var k = 0;
        while (y < height - 100)
        {
            var h = Math.Min(height - y, 700 + 500 * Noise.Of(seed, k, 3));
            double hw = 520 - 160 * y / height + 160 * Noise.Of(seed, k, 4), hd = 480 - 140 * y / height + 160 * Noise.Of(seed, k, 5);
            double ox = 120 * (Noise.Of(seed, k, 6) - 0.5), oz = 120 * (Noise.Of(seed, k, 7) - 0.5);
            var colour = Rock.Mix(RockDark, Noise.Of(seed, k, 8));
            var top = y + h >= height - 100 ? Snow : colour;
            m.Box(new Vector3((float)(ox - hw), (float)y, (float)(oz - hd)), new Vector3((float)(ox + hw), (float)(y + h), (float)(oz + hd)), colour, top);
            y += h; k++;
        }
        return m;
    }

    // An ice spike (scene 92's), `height` high.
    public static SnowMesh Spike(Placer at, double height, int seed)
    {
        var m = at.Mesh();
        m.Cylinder(Vector3.Zero, new Vector3((float)(80 * (Noise.Of(seed, 1, 9) - 0.5)), (float)height, 0), 260 + 90 * Noise.Of(seed, 2, 9), 0, 6, Ice);
        m.Cylinder(new Vector3(200, 0, 120), new Vector3(240, (float)(height * 0.35), 160), 120, 0, 5, IceDeep);
        return m;
    }

    // ---- the places

    private static void Harbour(Placer at, List<(double U, double V, double R)> taken)
    {
        // the pier: planks across it on stringers, piles into the sea, in pieces of four cells
        var deck = SnowDesign.PierDeck;
        var width = SnowDesign.PierHalf * IslandFile.CellSize;
        for (var v0 = SnowDesign.PierFromV; v0 < SnowDesign.PierToV; v0 += 4)
        {
            var v1 = Math.Min(SnowDesign.PierToV, v0 + 4);
            var m = at.Mesh();
            var len = (v1 - v0) * IslandFile.CellSize;
            for (double z = 0; z < len; z += 200)
            {
                var c = ((int)(z / 200) % 3) switch { 0 => Wood, 1 => WoodDark.Mix(Wood, 0.5), _ => Wood.Mix(Snow, 0.15) };
                m.Box(new Vector3((float)-width, deck - 90, (float)z + 8), new Vector3((float)width, deck, (float)Math.Min(len, z + 192)), c);
            }
            foreach (var x in new[] { -width + 200, width - 200 }) m.Box(new Vector3((float)(x - 90), deck - 260, 0), new Vector3((float)(x + 90), deck - 90, (float)len), WoodDark);
            for (double z = 256; z < len; z += 1024)
                foreach (var x in new[] { -width + 160, width - 160 })
                    m.Cylinder(new Vector3((float)x, -900, (float)z), new Vector3((float)x, deck - 90, (float)z), 120, 120, 6, WoodDark);
            if (v1 == SnowDesign.PierToV)
                foreach (var x in new[] { -width + 260, width - 260 })
                    m.Cylinder(new Vector3((float)x, deck, (float)(len - 300)), new Vector3((float)x, deck + 380, (float)(len - 300)), 110, 90, 8, Steel);
            var body = at.Add(m, "pier");
            at.Place(body, m, SnowDesign.PierU, v0, 0, what: "pier piece");
            at.Solid(SnowDesign.WorldX(SnowDesign.PierU) - width, SnowDesign.SeaFloor, SnowDesign.WorldZ(v0), SnowDesign.WorldX(SnowDesign.PierU) + width, deck, SnowDesign.WorldZ(v1));
        }
        taken.Add((SnowDesign.PierU, (SnowDesign.PierFromV + SnowDesign.PierToV) / 2, 4));

        // the pink concrete bunker by the quay, its door to the sea
        {
            var m = at.Mesh();
            m.Box(new Vector3(-1500, 0, -1250), new Vector3(1500, 2300, 1250), Concrete);
            m.Box(new Vector3(-1620, 2300, -1370), new Vector3(1620, 2520, 1370), Snow);
            m.Box(new Vector3(-1620, 2300, 1250), new Vector3(1620, 2420, 1400), ConcreteDark);
            m.Box(new Vector3(-450, 0, 1250), new Vector3(450, 1700, 1290), SteelDark);
            foreach (var x in new[] { -1100, 1100 }) m.Box(new Vector3(x - 250, 1300, 1250), new Vector3(x + 250, 1650, 1280), new Rgb(70, 110, 130));
            m.Box(new Vector3(1500, 0, -900), new Vector3(1530, 1500, -300), SteelDark);
            var (u, v) = (80.0, 287.0);
            var y = at.Foot(u, v, 3);
            at.Place(at.Add(m, "bunker"), m, u, v, y, what: "bunker");
            at.Solid(SnowDesign.WorldX(u) - 1500, y, SnowDesign.WorldZ(v) - 1250, SnowDesign.WorldX(u) + 1500, y + 2520, SnowDesign.WorldZ(v) + 1250);
            taken.Add((u, v, 5));
        }
        // crates and barrels, the lamp tripods, the gantry crane over the quay's east end
        var crate = at.Mesh();
        crate.Box(new Vector3(-300, 0, -300), new Vector3(300, 600, 300), Wood);
        crate.Box(new Vector3(-310, 250, -310), new Vector3(310, 350, 310), WoodDark);
        var crateBody = at.Add(crate, "crate");
        var barrel = at.Mesh();
        barrel.Cylinder(Vector3.Zero, new Vector3(0, 760, 0), 250, 250, 8, Teal, cap: Teal.Times(1.2));
        barrel.Cylinder(new Vector3(0, 240, 0), new Vector3(0, 300, 0), 262, 262, 8, SteelDark, capA: false, capB: false);
        var barrelBody = at.Add(barrel, "barrel");
        foreach (var (u, v, kind, stack) in new[] { (118.0, 290.0, 0, 0), (119.3, 290.0, 0, 0), (118.6, 291.3, 0, 0), (118.6, 290.6, 0, 1), (124.0, 289.0, 1, 0), (125.1, 289.4, 1, 0), (124.4, 290.4, 1, 0), (88.0, 292.0, 0, 0), (89.2, 292.4, 1, 0) })
        {
            var y = at.Foot(u, v, 0.6) + stack * 600;
            if (kind == 0) { at.Place(crateBody, crate, u, v, y, what: "crate"); at.Solid(u, v, y, 300, 600); }
            else { at.Place(barrelBody, barrel, u, v, y, what: "barrel"); at.Solid(u, v, y, 250, 760); }
        }
        taken.Add((121, 290, 4));
        var lamp = at.Mesh();
        for (var k = 0; k < 3; k++) { var a = k * 2 * Math.PI / 3; lamp.Beam(new Vector3((float)(380 * Math.Cos(a)), 0, (float)(380 * Math.Sin(a))), new Vector3(0, 1900, 0), 60, 60, SteelDark); }
        lamp.Box(new Vector3(-130, 1900, -130), new Vector3(130, 2160, 130), Lamp);
        lamp.Box(new Vector3(-160, 2160, -160), new Vector3(160, 2220, 160), SteelDark);
        var lampBody = at.Add(lamp, "lamp");
        foreach (var (u, v) in new[] { (97.0, 297.0), (111.0, 297.0), (60.0, 250.0), (34.0, 248.0) })
        {
            at.Place(lampBody, lamp, u, v, at.Foot(u, v, 0.8), what: "lamp");
            at.Solid(u, v, at.Foot(u, v, 0.8), 150, 2000);
        }
        {
            var m = at.Mesh();
            foreach (var x in new[] { -2600, 2600 })
            {
                m.Beam(new Vector3(x, 0, -900), new Vector3(x, 5200, 0), 200, 200, Steel);
                m.Beam(new Vector3(x, 0, 900), new Vector3(x, 5200, 0), 200, 200, Steel);
                m.Beam(new Vector3(x, 1800, -620), new Vector3(x, 1800, 620), 120, 120, SteelDark);
            }
            m.Box(new Vector3(-3000, 5200, -260), new Vector3(3000, 5650, 260), Steel, Snow);
            m.Beam(new Vector3(800, 5200, 0), new Vector3(800, 2600, 0), 30, 30, Cable);
            m.Box(new Vector3(650, 2350, -150), new Vector3(950, 2600, 150), SteelDark);
            var (u, v) = (137.0, 291.0);
            var y = at.Foot(u, v, 6);
            at.Place(at.Add(m, "crane"), m, u, v, y, reach: FarReach / 2, what: "crane");
            foreach (var x in new[] { -2600, 2600 })
                at.Solid(SnowDesign.WorldX(u) + x - 200, y, SnowDesign.WorldZ(v) - 900, SnowDesign.WorldX(u) + x + 200, y + 5200, SnowDesign.WorldZ(v) + 900);
            taken.Add((u, v, 7));
        }
    }

    private static void Village(Placer at, List<(double U, double V, double R)> taken)
    {
        var plaza = SnowDesign.Plaza;
        foreach (var (u, v, r) in SnowDesign.Domes)
        {
            var turn = Math.Atan2(plaza.U - u, plaza.V - v);
            var radius = r * IslandFile.CellSize;
            var m = Dome(at, radius, turn, (int)(u * 7 + v));
            var y = at.Foot(u, v, r * 0.8);
            at.Place(at.Add(m, "dome"), m, u, v, y, what: "dome");
            var half = radius * 0.72;
            at.Solid(u, v, y, half, radius * 0.75);
            taken.Add((u, v, r + 1.5));
        }
        // the carrot patch: carrots in rows, their leaves up
        {
            var m = at.Mesh();
            for (var i = 0; i < 4; i++)
                for (var j = 0; j < 3; j++)
                {
                    var c = new Vector3(i * 700 - 1050, 0, j * 700 - 700);
                    m.Cylinder(c + new Vector3(0, -120, 0), c + new Vector3(0, 300, 0), 0, 230, 6, Carrot);
                    for (var k = 0; k < 3; k++)
                    {
                        var a = k * 2 * Math.PI / 3 + i + j;
                        m.TwoSided(Leaf, c + new Vector3(0, 300, 0), c + new Vector3((float)(160 * Math.Cos(a)), 760, (float)(160 * Math.Sin(a))), c + new Vector3((float)(60 * Math.Cos(a + 0.6)), 300, (float)(60 * Math.Sin(a + 0.6))));
                    }
                }
            var (u, v) = SnowDesign.CarrotPatch;
            at.Place(at.Add(m, "carrots"), m, u, v, at.Ground(u, v), what: "carrot patch");
            taken.Add((u, v, 3));
        }
        // dead trees round the village
        var dead = new[] { DeadTree(at, 0), DeadTree(at, 1) };
        var deadBodies = dead.Select(d => at.Add(d, "dead tree")).ToArray();
        foreach (var (u, v, k) in new[] { (26.0, 222.0, 0), (84.0, 230.0, 1), (22.0, 276.0, 1), (72.0, 282.0, 0), (88.0, 258.0, 0), (40.0, 216.0, 1) })
        {
            var y = at.Foot(u, v, 0.5);
            at.Place(deadBodies[k], dead[k], u, v, y, (int)(Noise.Of((int)u, (int)v, 3) * 4096), what: "dead tree");
            at.Solid(u, v, y, 200, 1800);
            taken.Add((u, v, 2));
        }
        // the village's snowman, the Rabbibunnies' kind
        PlaceSnowman(at, taken, 2, plaza.U, plaza.V, SnowDesign.Start);
    }

    public static SnowMesh DeadTree(Placer at, int kind)
    {
        var m = at.Mesh();
        m.Cylinder(Vector3.Zero, new Vector3(30, 2200, 0), 190, 110, 6, Trunk, capA: false);
        var branches = kind == 0
            ? new[] { (1200.0, 0.4, 700.0), (1600.0, 2.6, 600.0), (2000.0, 4.4, 500.0), (1900.0, 1.2, 420.0) }
            : new[] { (900.0, 1.0, 650.0), (1400.0, 3.4, 700.0), (1850.0, 5.2, 520.0) };
        foreach (var (y, a, len) in branches)
        {
            var from = new Vector3(0, (float)y, 0);
            var to = from + new Vector3((float)(len * Math.Cos(a)), (float)(len * 0.7), (float)(len * Math.Sin(a)));
            m.Beam(from, to, 90, 90, TrunkDark);
            m.Beam(to, to + new Vector3((float)(200 * Math.Cos(a + 0.8)), 220, (float)(200 * Math.Sin(a + 0.8))), 55, 55, TrunkDark);
        }
        return m;
    }

    // A snowman at (u, v) facing the point `look` (a body for each kind, made the first time).
    private static void PlaceSnowman(Placer at, List<(double U, double V, double R)> taken, int kind, double u, double v, (double U, double V) look)
    {
        if (!at.Snowmen.TryGetValue(kind, out var made))
        {
            var mesh = Snowman(at, kind);
            made = at.Snowmen[kind] = (at.Add(mesh, "snowman"), mesh);
        }
        // (the turn: the engine's beta, a 4096th of a turn, its body's +z towards `look`)
        var beta = (int)Math.Round(Math.Atan2(look.U - u, look.V - v) / (2 * Math.PI) * 4096) & 0xFFF;
        var y = at.Foot(u, v, kind == 3 ? 0.6 : 1.0);
        at.Place(made.Body, made.Mesh, u, v, y, beta == 0 ? 1 : beta, what: "snowman");
        at.Solid(u, v, y, kind == 3 ? 330 : 520, kind == 3 ? 1200 : 1950);
        taken.Add((u, v, 2.5));
    }

    private static void Pond(Placer at, List<(double U, double V, double R)> taken)
    {
        // ice spikes along the pond's north-west rim, the rock pillars behind them
        var spikes = new[] { Spike(at, 3600, 1), Spike(at, 2400, 2), Spike(at, 4600, 3) };
        var spikeBodies = spikes.Select(s => at.Add(s, "ice spike")).ToArray();
        var k = 0;
        foreach (var (u, v) in new[] { (26.0, 140.0), (31.0, 134.5), (38.0, 131.0), (46.0, 129.0), (55.0, 129.5), (20.0, 148.0), (18.5, 158.0), (21.0, 168.0) })
        {
            var y = at.Foot(u, v, 0.5);
            at.Place(spikeBodies[k % 3], spikes[k % 3], u, v, y, (int)(Noise.Of(k, 9, 2) * 4096) | 1, what: "ice spike");
            at.Solid(u, v, y, 220, 2000);
            taken.Add((u, v, 1.5));
            k++;
        }
        var pillars = new[] { Pillar(at, 5200, 11), Pillar(at, 3800, 12), Pillar(at, 6400, 13) };
        var pillarBodies = pillars.Select(p => at.Add(p, "rock pillar")).ToArray();
        k = 0;
        foreach (var (u, v) in new[] { (22.0, 131.0), (29.0, 126.0), (36.0, 122.0), (45.0, 121.0), (54.0, 122.0), (14.0, 142.0), (12.5, 153.0) })
        {
            var y = at.Foot(u, v, 1.2);
            at.Place(pillarBodies[k % 3], pillars[k % 3], u, v, y, what: "rock pillar");
            at.Solid(u, v, y, 560, k % 3 == 1 ? 3800 : 5200);
            taken.Add((u, v, 2.5));
            k++;
        }
        // a snowman family on the shore, and the Rabbibunnies' kind on the pond's isle
        var shore = (60.0, 190.0);
        PlaceSnowman(at, taken, 0, 67.0, 182.0, shore);
        PlaceSnowman(at, taken, 1, 70.5, 185.0, shore);
        PlaceSnowman(at, taken, 3, 66.0, 186.5, shore);
        PlaceSnowman(at, taken, 2, 47.0, 157.0, (60.0, 175.0));
    }

    private static void Canyon(Placer at, List<(double U, double V, double R)> taken)
    {
        // the mine rail up the canyon's floor: sleepers a cell apart, two rails over them, as the ground lies -- in pieces of 8 cells
        const double u = 109.5;
        const double gauge = 380;
        double from = SnowDesign.CanyonNorth + 4, to = SnowDesign.CanyonSouth - 2;
        for (var v0 = from; v0 < to; v0 += 8)
        {
            var v1 = Math.Min(to, v0 + 8);
            var baseY = at.Ground(u, v0);
            var m = at.Mesh();
            Vector3? prev = null;
            for (var v = v0; v <= v1 + 1e-6; v += 1)
            {
                var y = (float)(at.Ground(u, v) - baseY + 20);
                var z = (float)((v - v0) * IslandFile.CellSize);
                m.Box(new Vector3(-560, y, z - 110), new Vector3(560, y + 70, z + 110), WoodDark);
                var here = new Vector3(0, y + 70, z);
                if (prev is { } p)
                    foreach (var x in new[] { -gauge, gauge })
                        m.Beam(new Vector3((float)x, p.Y + 35, p.Z), new Vector3((float)x, here.Y + 35, here.Z), 70, 70, Steel);
                prev = here;
            }
            at.Place(at.Add(m, "rail"), m, u, v0, baseY, what: "rail piece");
        }
        // the cart at the foot
        {
            var m = at.Mesh();
            m.Box(new Vector3(-520, 260, -800), new Vector3(520, 1000, 800), Steel);
            m.Box(new Vector3(-440, 1000, -720), new Vector3(440, 1060, 720), Rock);
            foreach (var x in new[] { -gauge, gauge })
                foreach (var z in new[] { -520, 520 })
                    m.Cylinder(new Vector3((float)x - 40, 200, z), new Vector3((float)x + 40, 200, z), 190, 190, 8, Coal);
            var v = to - 6;
            var y = at.Ground(u, v) + 90;
            at.Place(at.Add(m, "cart"), m, u, v, y, what: "cart");
            at.Solid(u, v, y, 600, 1100);
        }
        taken.Add((u, (from + to) / 2, 0));
        for (var v = from; v < to; v += 4) taken.Add((u, v, 2.2));

        // the prison's gate at the canyon's head: pink concrete pillars, a lintel, Funfrock's bust on it
        {
            const double gu = 109.5, gv = 52;
            var y = at.Foot(gu, gv, 2);
            var m = at.Mesh();
            const float px = 4600, ph = 5400;
            foreach (var x in new[] { -px, px })
            {
                m.Box(new Vector3(x - 700, 0, -700), new Vector3(x + 700, ph, 700), Concrete, Snow);
                m.Box(new Vector3(x - 800, ph - 300, -800), new Vector3(x + 800, ph - 150, 800), ConcreteDark);
            }
            m.Box(new Vector3(-px - 700, ph, -600), new Vector3(px + 700, ph + 1000, 600), Concrete, Snow);
            m.Box(new Vector3(-900, ph + 1000, -500), new Vector3(900, ph + 1500, 500), ConcreteDark, Snow);
            m.Ellipsoid(new Vector3(0, ph + 1900, 0), 820, 420, 460, 5, 10, new Rgb(196, 168, 92));
            m.Ellipsoid(new Vector3(0, ph + 2650, 0), 430, 520, 450, 6, 10, new Rgb(214, 186, 104));
            m.Cylinder(new Vector3(0, ph + 2620, 380), new Vector3(0, ph + 2560, 640), 110, 0, 5, new Rgb(214, 186, 104));
            var gateBody = at.Add(m, "prison gate");
            at.Place(gateBody, m, gu, gv, y, reach: FarReach / 2, what: "prison gate");
            foreach (var x in new[] { -px, px })
                at.Solid(SnowDesign.WorldX(gu) + x - 700, y, SnowDesign.WorldZ(gv) - 700, SnowDesign.WorldX(gu) + x + 700, y + ph + 1000, SnowDesign.WorldZ(gv) + 700);
            taken.Add((gu, gv, 12));
        }
    }

    private static void Ski(Placer at, List<(double U, double V, double R)> taken, List<string> log)
    {
        var top = SnowDesign.LiftTop; var foot = SnowDesign.LiftFoot;
        double du = foot.U - top.U, dv = foot.V - top.V, length = Math.Sqrt(du * du + dv * dv);
        var turn = Math.Atan2(du, dv);
        const double half = 1100, pylonHeight = 4300, stationWheel = 3000;
        // the pylons, about every 16 cells between the stations
        var pylons = Math.Max(2, (int)Math.Round(length / 16));
        var tops = new List<Vector3>();
        tops.Add(new Vector3((float)SnowDesign.WorldX(top.U), (float)(at.Ground(top.U, top.V) + stationWheel + 70), (float)SnowDesign.WorldZ(top.V)));
        for (var k = 1; k < pylons; k++)
        {
            double f = (double)k / pylons, u = top.U + du * f, v = top.V + dv * f;
            var y = at.Foot(u, v, 0.8);
            var m = Pylon(at, pylonHeight, half, turn);
            at.Place(at.Add(m, "pylon"), m, u, v, y, reach: FarReach, what: "pylon");
            at.Solid(u, v, y, 420, pylonHeight);
            tops.Add(new Vector3((float)SnowDesign.WorldX(u), (float)(y + pylonHeight - 40), (float)SnowDesign.WorldZ(v)));
            taken.Add((u, v, 2.5));
        }
        tops.Add(new Vector3((float)SnowDesign.WorldX(foot.U), (float)(at.Ground(foot.U, foot.V) + stationWheel + 70), (float)SnowDesign.WorldZ(foot.V)));
        // the cables between them, chairs on both (going up on one side, down the other), a piece a span
        var across = new Vector3((float)Math.Cos(turn), 0, (float)-Math.Sin(turn));
        var chair = 0;
        for (var k = 0; k + 1 < tops.Count; k++)
        {
            var a = tops[k]; var b = tops[k + 1];
            var mid = (a + b) / 2;
            var m = at.Mesh();
            foreach (var side in new[] { -1f, 1f })
            {
                var off = across * (float)(side * half);
                m.Beam(a - mid + off, b - mid + off, 34, 34, Cable);
                var span = (b - a).Length();
                for (var d = 1300f; d < span - 900; d += 2600)
                {
                    var p = a - mid + off + (b - a) * (d / span);
                    var seat = p - new Vector3(0, 1150, 0);
                    var colour = chair++ % 2 == 0 ? Red : Blue;
                    m.Beam(p, seat + new Vector3(0, 80, 0), 40, 40, SteelDark);
                    var dir = Vector3.Normalize(new Vector3(b.X - a.X, 0, b.Z - a.Z));
                    m.Beam(seat - across * 330, seat + across * 330, 120, 420, colour);
                    m.Beam(seat - across * 330 - dir * (side * 210) + new Vector3(0, 300, 0), seat + across * 330 - dir * (side * 210) + new Vector3(0, 300, 0), 100, 380, colour);
                }
            }
            var body = at.Add(m, "cable");
            double mu = mid.X / IslandFile.CellSize - SnowDesign.CubeX0 * 64, mv = mid.Z / IslandFile.CellSize - SnowDesign.CubeZ0 * 64;
            at.Place(body, m, mu, mv, mid.Y, reach: FarReach, what: "cable span");
        }
        // the stations: a hut and the bullwheel, each end
        foreach (var (station, isTop) in new[] { (top, true), (foot, false) })
        {
            var m = at.Mesh();
            m.Turn(turn + (isTop ? Math.PI : 0));
            Bullwheel(m, half, stationWheel);
            m.Transform = Matrix4x4.CreateRotationY((float)(turn + (isTop ? Math.PI : 0))) * Matrix4x4.CreateTranslation(Vector3.Transform(new Vector3((float)(isTop ? half + 2400 : -half - 2400), 0, 600),
                Matrix4x4.CreateRotationY((float)turn)));
            Hut(m, 2600, 2200, 2100, new Rgb(214, 228, 232));
            var y = at.Foot(station.U, station.V, 3);
            at.Place(at.Add(m, isTop ? "top station" : "foot station"), m, station.U, station.V, y, reach: FarReach, what: "lift station");
            at.Solid(station.U, station.V, y, 300, stationWheel);
            var hut = Vector3.Transform(new Vector3((float)(isTop ? half + 2400 : -half - 2400), 0, 600), Matrix4x4.CreateRotationY((float)turn));
            at.Solid(SnowDesign.WorldX(station.U) + hut.X - 1400, y, SnowDesign.WorldZ(station.V) + hut.Z - 1400, SnowDesign.WorldX(station.U) + hut.X + 1400, y + 2400,
                SnowDesign.WorldZ(station.V) + hut.Z + 1400);
            taken.Add((station.U, station.V, 7));
        }
        log.Add($"the chair lift: {length:0} cells from ({top.U:0},{top.V:0}) to ({foot.U:0},{foot.V:0}), {pylons - 1} pylons, {chair} chairs");

        // the slalom gates down the slope: a red and a blue in turn, either side of the line, each two poles and a panel
        var pt = SnowDesign.PisteTop; var pf = SnowDesign.PisteFoot;
        var downTurn = Math.Atan2(pf.U - pt.U, pf.V - pt.V);
        var gates = new (int Body, SnowMesh Mesh)[2];
        for (var g = 0; g < 2; g++)
        {
            var m = at.Mesh();
            m.Turn(downTurn);
            var colour = g == 0 ? Red : Blue;
            foreach (var x in new[] { -800f, 800f })
            {
                m.Cylinder(Vector3.Zero + new Vector3(x, 0, 0), new Vector3(x, 1500, 0), 32, 32, 5, colour);
                m.Cylinder(new Vector3(x, 1500, 0), new Vector3(x, 1600, 0), 34, 34, 5, Snow);
            }
            m.TwoSided(colour, new Vector3(-800, 1450, 0), new Vector3(800, 1450, 0), new Vector3(800, 900, 0), new Vector3(-800, 900, 0));
            m.TwoSided(Snow, new Vector3(-300, 1350, 2), new Vector3(300, 1350, 2), new Vector3(300, 1000, 2), new Vector3(-300, 1000, 2));
            gates[g] = (at.Add(m, "gate"), m);
        }
        var n = 0;
        for (var s = 0.12; s < 0.92; s += 0.065, n++)
        {
            var (cu, cv) = (pt.U + (pf.U - pt.U) * s, pt.V + (pf.V - pt.V) * s);
            var (u, v) = SnowDesign.Across(pt, pf, (n % 2 == 0 ? 1 : -1) * 4.5, 0);
            u += cu - pt.U; v += cv - pt.V;
            at.Place(gates[n % 2].Body, gates[n % 2].Mesh, u, v, at.Ground(u, v), what: "slalom gate");
        }
        // a fence round the foot's hut (scene 96's), the skis left against it, snowmen at the top and the foot
        {
            var (u, v) = SnowDesign.Across(foot, top, -7, -1);
            var y0 = at.Foot(u, v, 3);
            var fence = FenceAlong(at, turn + Math.PI / 2);
            at.Place(at.Add(fence, "fence"), fence, u, v, y0, what: "fence");
            var dir = new Vector3((float)Math.Sin(turn + Math.PI / 2), 0, (float)Math.Cos(turn + Math.PI / 2));
            for (var k = -3; k <= 3; k++)
            {
                var p = dir * (k * 900);
                at.Solid(SnowDesign.WorldX(u) + p.X - 70, y0, SnowDesign.WorldZ(v) + p.Z - 70, SnowDesign.WorldX(u) + p.X + 70, y0 + 950, SnowDesign.WorldZ(v) + p.Z + 70);
            }
            taken.Add((u, v, 4));
        }
        PlaceSnowman(at, taken, 0, top.U - 9, top.V + 3, pt);
        PlaceSnowman(at, taken, 1, foot.U - 8, foot.V + 4, SnowDesign.Start);
        PlaceSnowman(at, taken, 3, foot.U - 6.5, foot.V + 6, SnowDesign.Start);
        // (the slope itself: no tree, no pillar on it)
        for (var s = 0.0; s <= 1.0; s += 0.05)
            taken.Add((pt.U + (pf.U - pt.U) * s, pt.V + (pf.V - pt.V) * s, SnowDesign.PisteHalf + 1));
        for (var s = 0.0; s <= 1.0; s += 0.05)
            taken.Add((top.U + du * s, top.V + dv * s, 3));
    }

    // A rope fence across, seven posts, a pair of skis leaning on it.
    private static SnowMesh FenceAlong(Placer at, double turn)
    {
        var m = at.Mesh();
        m.Turn(turn);
        var post = new List<Vector3>();
        for (var k = 0; k <= 6; k++) post.Add(new Vector3(0, 0, k * 900 - 2700));
        foreach (var p in post) m.Beam(p, p + new Vector3(0, 950, 0), 110, 110, Trunk);
        for (var k = 0; k + 1 < post.Count; k++)
            foreach (var y in new[] { 820f, 450f }) m.Beam(post[k] + new Vector3(0, y, 0), post[k + 1] + new Vector3(0, y - 60, 0), 40, 40, TrunkDark);
        foreach (var z in new[] { -350f, -150f })
            m.Beam(new Vector3(140, 30, z), new Vector3(50, 1650, z + 60), 25, 90, z < -200 ? Red : Blue);
        return m;
    }

    private static void Summit(Placer at, List<(double U, double V, double R)> taken)
    {
        // the peak's top: the highest point near the design's peak, a flag on it
        var best = (U: SnowDesign.Peak.U, V: SnowDesign.Peak.V, H: double.MinValue);
        for (var v = SnowDesign.Peak.V - 12; v <= SnowDesign.Peak.V + 12; v += 0.5)
            for (var u = SnowDesign.Peak.U - 12; u <= SnowDesign.Peak.U + 12; u += 0.5)
                if (at.Ground(u, v) > best.H) best = (u, v, at.Ground(u, v));
        var m = at.Mesh();
        m.Cylinder(Vector3.Zero, new Vector3(0, 2600, 0), 50, 40, 5, Steel);
        m.TwoSided(Red, new Vector3(0, 2550, 0), new Vector3(1100, 2350, 0), new Vector3(1100, 1850, 0), new Vector3(0, 1950, 0));
        m.Ellipsoid(Vector3.Zero, 500, 240, 500, 3, 6, Rock, below: false);
        at.Place(at.Add(m, "summit flag"), m, best.U, best.V, at.Foot(best.U, best.V, 0.6), reach: FarReach, what: "summit flag");
        taken.Add((best.U, best.V, 3));
    }

    private static void Floes(Placer at)
    {
        // ice floes on the sea round the shore: a few shapes, turned every way
        var shapes = new SnowMesh[4];
        for (var s = 0; s < shapes.Length; s++)
        {
            var m = at.Mesh();
            var n = 7 + s;
            var ring = new List<Vector3>();
            for (var k = 0; k < n; k++)
            {
                var a = 2 * Math.PI * k / n; var r = 700 + 700 * Noise.Of(s, k, 21) + 300 * s;
                ring.Add(new Vector3((float)(r * Math.Cos(a)), 0, (float)(r * Math.Sin(a))));
            }
            for (var k = 0; k < n; k++)
            {
                var p = ring[k]; var q = ring[(k + 1) % n];
                m.Poly(Snow, Vector3.UnitY, new Vector3(0, 110, 0), p + new Vector3(0, 110, 0), q + new Vector3(0, 110, 0));
                m.Poly(IceDeep, (p + q) / 2, p + new Vector3(0, 110, 0), q + new Vector3(0, 110, 0), q * 0.9f + new Vector3(0, -260, 0), p * 0.9f + new Vector3(0, -260, 0));
            }
            shapes[s] = m;
        }
        var bodies = shapes.Select(s => at.Add(s, "floe")).ToArray();
        var count = 0;
        for (var v = 6.0; v < SnowDesign.Depth - 6; v += 9)
            for (var u = 6.0; u < SnowDesign.Width - 6; u += 9)
            {
                double ju = u + 6 * Noise.Of((int)u, (int)v, 41), jv = v + 6 * Noise.Of((int)u, (int)v, 43);
                if (SnowDesign.IsLand(ju, jv, out var inside) || inside < -10 || inside > -3) continue;
                if (ju < 64 && jv < 64 && (ju < 60 || jv < 60)) continue;
                if (Noise.Of((int)u, (int)v, 47) > 0.32) continue;
                if (Math.Abs(ju - SnowDesign.PierU) < 8 && jv > SnowDesign.PierFromV - 4) continue;
                var k = count++ % bodies.Length;
                at.Place(bodies[k], shapes[k], ju, jv, 0, (int)(Noise.Of((int)u, (int)v, 49) * 4096) | 1, what: "floe");
            }
    }

    private static void Trees(Placer at, List<(double U, double V, double R)> taken, Func<double, double, double, bool> clear, List<string> log)
    {
        var sizes = new[] { 0.8, 1.0, 1.25 };
        var made = sizes.Select((s, i) => Tree(at, s, i * 3 + 1)).ToArray();
        var bodies = made.Select(m => at.Add(m, "tree")).ToArray();
        var placed = 0;
        void Put(double u, double v, int k)
        {
            var y = at.Foot(u, v, 0.6);
            at.Place(bodies[k], made[k], u, v, y, (int)(Noise.Of((int)(u * 3), (int)(v * 3), 55) * 4096) | 1, what: "tree");
            at.Solid(u, v, y, 230 * sizes[k], 1800 * sizes[k]);
            taken.Add((u, v, 1.8 * sizes[k]));
            placed++;
        }
        // (somewhere a tree can stand: snow, near level, between the lowland and the massif's second tier, clear of the ways)
        bool Fits(double u, double v)
        {
            if (!SnowDesign.IsLand(u, v, out var inside) || inside < 4) return false;
            var h = at.Ground(u, v);
            if (h < SnowDesign.T1 - 300 || h > SnowDesign.T3 + 600) return false;
            var lo = double.MaxValue; var hi = double.MinValue;
            for (var k = 0; k < 8; k++)
            {
                var g = at.Ground(u + 1.2 * Math.Cos(k * Math.PI / 4), v + 1.2 * Math.Sin(k * Math.PI / 4));
                lo = Math.Min(lo, g); hi = Math.Max(hi, g);
            }
            if (hi - lo > 420) return false;
            if (SnowDesign.KindAt(u, v, h) != SnowDesign.Kind.Snow) return false;
            if (SnowDesign.Paths.Any(p => SnowDesign.DistToLine(p.Line, u, v) < p.Half + 2.5)) return false;
            if (SnowDesign.Ramps.Any(r => SnowDesign.DistToLine(new[] { r.A, r.B }, u, v) < r.Half + 2.5)) return false;
            if (SnowDesign.Inside(SnowDesign.Village, u, v) || SnowDesign.Inside(SnowDesign.Quay, u, v)) return false;
            if (SnowDesign.SignedDistance(SnowDesign.Pond, u, v) > -3) return false;
            if (u > SnowDesign.CanyonU0 - 1 && u < SnowDesign.CanyonU1 + 1 && v > SnowDesign.CanyonNorth && v < SnowDesign.CanyonSouth) return false;
            return true;
        }
        // the slope's edges lined, then woods where the forest's noise says, a lone tree here and there
        var (pt, pf) = (SnowDesign.PisteTop, SnowDesign.PisteFoot);
        for (var s = 0.1; s < 0.97; s += 0.045)
            foreach (var side in new[] { -1, 1 })
            {
                var (u, v) = SnowDesign.Across(pt, pf, side * (SnowDesign.PisteHalf + 3 + 1.5 * Noise.Of((int)(s * 100), side, 61)), 0);
                u += (pf.U - pt.U) * s; v += (pf.V - pt.V) * s;
                if (clear(u, v, 1.6) && Fits(u, v)) Put(u, v, (int)(Noise.Of((int)(s * 100), side, 63) * 3) % 3);
            }
        for (var v = 4.0; v < SnowDesign.Depth - 4; v += 4.2)
            for (var u = 4.0; u < SnowDesign.Width - 4; u += 4.2)
            {
                double ju = u + 3 * (Noise.Of((int)u, (int)v, 65) - 0.5), jv = v + 3 * (Noise.Of((int)u, (int)v, 67) - 0.5);
                var forest = Noise.Fbm(ju / 28, jv / 28, 3, 69);
                var chance = forest > 0.56 ? 0.75 : forest > 0.5 ? 0.3 : 0.035;
                if (Noise.Of((int)(ju * 2), (int)(jv * 2), 71) > chance) continue;
                if (!clear(ju, jv, 1.6) || !Fits(ju, jv)) continue;
                Put(ju, jv, (int)(Noise.Of((int)ju, (int)jv, 73) * 3) % 3);
            }
        log.Add($"{placed} trees");
    }

    private static void Pillars(Placer at, List<(double U, double V, double R)> taken, Func<double, double, double, bool> clear)
    {
        // mauve rock pillars on the massif's tiers, near their cliffs' edges
        var shapes = new[] { Pillar(at, 3400, 31), Pillar(at, 4800, 32), Pillar(at, 2600, 33), Pillar(at, 6000, 34) };
        var bodies = shapes.Select(s => at.Add(s, "rock pillar")).ToArray();
        var count = 0;
        for (var v = 6.0; v < 200; v += 7)
            for (var u = 70.0; u < SnowDesign.Width - 6; u += 7)
            {
                double ju = u + 4 * (Noise.Of((int)u, (int)v, 81) - 0.5), jv = v + 4 * (Noise.Of((int)u, (int)v, 83) - 0.5);
                var h = at.Ground(ju, jv);
                if (h < SnowDesign.T2 - 200 || !clear(ju, jv, 2.5)) continue;
                if (Noise.Of((int)ju, (int)jv, 85) > 0.22) continue;
                // (by a cliff: ground a tier lower within three cells)
                var drop = Enumerable.Range(0, 8).Min(k => at.Ground(ju + 3 * Math.Cos(k * Math.PI / 4), jv + 3 * Math.Sin(k * Math.PI / 4)));
                if (h - drop < 1500) continue;
                var level = Enumerable.Range(0, 8).Max(k => Math.Abs(at.Ground(ju + 1.2 * Math.Cos(k * Math.PI / 4), jv + 1.2 * Math.Sin(k * Math.PI / 4)) - h));
                if (level > 300) continue;
                if (SnowDesign.Paths.Any(p => SnowDesign.DistToLine(p.Line, ju, jv) < p.Half + 3)) continue;
                if (SnowDesign.Ramps.Any(r => SnowDesign.DistToLine(new[] { r.A, r.B }, ju, jv) < r.Half + 3)) continue;
                var k = count++ % bodies.Length;
                var y = at.Foot(ju, jv, 1.2);
                at.Place(bodies[k], shapes[k], ju, jv, y, what: "rock pillar");
                at.Solid(ju, jv, y, 560, 2600);
                taken.Add((ju, jv, 2.5));
            }
    }
}
