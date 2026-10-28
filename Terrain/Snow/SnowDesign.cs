namespace LBAAssembler.Terrain.Snow;

// The snow island's design (the user, 2026-10-22: "Design a brand new island the same size as desert island for LBA2 with a snow theme. Let's
// take inspiration from LBA1's Hamalayi Mountains, let's introduce some snowmen, and perhaps a small skiing area"): where everything is, and
// the ground's height and kind at any point.
//
// The Desert island's size: its 19 cubes, 4 x 5 with the north-west corner's missing (cubes 7..10 x 7..11; 7,7 none). Places are in the
// island's cells from its first cube's corner: u east (0..255), v south (0..319) -- cell (448 + u, 448 + v) of the island's map.
//
// Hamalayi's look, from LBA1's outside scenes (9, 15, 62-72, 81, 91, 92, 96): flat terraces of pale cyan snow cut by vertical ice cliffs in
// tiers some 8 layers high, a ragged shore ledge a couple of layers over a turquoise sea, a long canyon climbing north between cliff walls
// with a mine-rail track up its floor to the prison's pink concrete gate, a ski resort's mountain with steel lattice lift pylons and a little
// hut, puffy snow-laden trees, mauve rock pillars, a frozen pond sunk into a plateau with ice spikes along its rock wall, the Rabbibunnies'
// village of stone domes on brown earth, and snowmen. So here, from the sea up:
//   T0  the shore ledge and the harbour's quay (2 layers);
//   T1  the lowland (8): the village of domes in the south-west, the frozen lake in the west, the way to the ski slope's foot in the east;
//   T2  the ridges and the massif's first tier (16);
//   T3  the north plateau with the prison gate, and the massif's second tier (24);
//   T4  the summit plateau (32), the peak rising from it to some 46.
// The canyon runs north from the lowland to the north plateau, its floor climbing two layers at a time; the ski slope comes down the
// massif's south-east face from the summit plateau to the lowland, smooth across the tiers it cuts, the chair lift beside it.
internal static class SnowDesign
{
    public const int CubeX0 = 7, CubeZ0 = 7, CubesX = 4, CubesZ = 5;
    public static readonly (int X, int Z) MissingCube = (7, 7);
    public const int Width = CubesX * 64, Depth = CubesZ * 64;
    public static int Gx(double u) => (int)Math.Floor(CubeX0 * 64 + u);
    public static int Gz(double v) => (int)Math.Floor(CubeZ0 * 64 + v);
    public static double WorldX(double u) => (CubeX0 * 64 + u) * IslandFile.CellSize;
    public static double WorldZ(double v) => (CubeZ0 * 64 + v) * IslandFile.CellSize;
    public static IEnumerable<(int X, int Z)> Cubes()
    {
        for (var cz = CubeZ0; cz < CubeZ0 + CubesZ; cz++)
            for (var cx = CubeX0; cx < CubeX0 + CubesX; cx++)
                if ((cx, cz) != MissingCube) yield return (cx, cz);
    }

    // the tiers (world units: a layer 256)
    public const int T0 = 512, T1 = 2048, T2 = 4096, T3 = 6144, T4 = 8192;
    public const int SeaFloor = -400, PondIce = 1536;

    // ---- the shapes (cells)
    // the coast, clockwise from the north-west, clear of the missing cube and the island's edges
    public static readonly (double U, double V)[] Coast =
    {
        (82, 12), (130, 10), (180, 9), (224, 16), (245, 44), (247, 100), (241, 150), (246, 198), (240, 246), (226, 284), (196, 300),
        (152, 302), (128, 300), (88, 304), (58, 300), (30, 286), (13, 252), (10, 200), (14, 150), (11, 112), (20, 84), (52, 76), (74, 62), (76, 34),
    };
    // the harbour's quay: shore ledge wider there, the pier going south from it
    public static readonly (double U, double V)[] Quay = { (60, 274), (150, 274), (150, 310), (60, 310) };
    public const double PierU = 104, PierFromV = 299, PierToV = 316, PierHalf = 2.2;
    public const int PierDeck = 640;
    // where Twinsen starts: the pier's foot, on the quay
    public static readonly (double U, double V) Start = (104, 294);

    // the tiers' regions, later ones over earlier ones
    public static readonly (int Tier, (double U, double V)[] Shape)[] Regions =
    {
        // the massif's first tier, east of the canyon
        (T2, new (double, double)[] { (124, 58), (250, 0), (250, 196), (186, 202), (150, 214), (124, 206) }),
        // the north plateau (the prison gate) and the massif's second tier
        (T3, new (double, double)[] { (70, 0), (160, 0), (160, 58), (70, 58) }),
        (T3, new (double, double)[] { (138, 0), (250, 0), (250, 152), (196, 160), (150, 150), (138, 130) }),
        // the summit plateau
        (T4, new (double, double)[] { (160, 0), (250, 0), (250, 100), (206, 104), (164, 96) }),
        // the canyon's walls, always a tier or two over its climbing floor: the west ridge between it and the lake, the east wall between it
        // and the massif -- each a tier higher in its north half
        (T3, new (double, double)[] { (70, 120), (95, 120), (95, 212), (82, 222), (70, 212) }),
        (T4, new (double, double)[] { (70, 58), (95, 58), (95, 120), (70, 120) }),
        (T3, new (double, double)[] { (122, 128), (140, 132), (140, 206), (122, 206) }),
        (T4, new (double, double)[] { (122, 58), (140, 58), (140, 132), (122, 128) }),
    };

    // the canyon: its floor between the ridge and the massif, climbing from the lowland (v 252) to the north plateau (v 58)
    public const double CanyonU0 = 97, CanyonU1 = 122, CanyonSouth = 250, CanyonNorth = 58;
    public const int CanyonSteps = 8;           // two layers each: T1 + 8 x 512 = T3
    // the canyon's floor height at v (flat stretches, a short ramp up to the next)
    public static double CanyonFloor(double v)
    {
        if (v >= CanyonSouth) return T1;
        if (v <= CanyonNorth) return T3;
        var t = (CanyonSouth - v) / (CanyonSouth - CanyonNorth) * CanyonSteps;
        var step = Math.Floor(t); var f = t - step;
        const double ramp = 0.3;              // the last part of each stretch climbs to the next
        var up = f < 1 - ramp ? 0 : (f - (1 - ramp)) / ramp;
        return T1 + 512 * (step + Smooth(up));
    }

    // the frozen pond, sunk two layers into the lowland, a little island of snow in it
    public static readonly (double U, double V)[] Pond =
    {
        (28, 140), (50, 132), (66, 140), (70, 158), (64, 176), (46, 184), (30, 178), (22, 160),
    };
    public const double PondBank = 2.2;
    public static readonly (double U, double V)[] PondIsle = { (42, 154), (50, 151), (54, 158), (48, 164), (41, 161) };

    // the ski slope: down the massif's south-east face from the summit plateau to the lowland
    public static readonly (double U, double V) PisteTop = (186, 96), PisteFoot = (206, 222);
    public const double PisteHalf = 11, PisteEdge = 4;
    public const int PisteTopY = T4, PisteFootY = T1;
    // the lift beside it, its stations at the top and the foot
    public const double LiftOffset = -18;     // (to the left facing down the slope: its east side)
    public static (double U, double V) LiftTop => Across(PisteTop, PisteFoot, LiftOffset, 4);
    public static (double U, double V) LiftFoot => Across(PisteFoot, PisteTop, -LiftOffset, 2);

    // the peak: on the summit plateau's north-east, its ridges rocky
    public static readonly (double U, double V) Peak = (214, 42);
    public const double PeakRadius = 30, PeakRise = 3700;

    // ramps between the tiers (cells: from, to, half-width): each climbs straight from one end's ground to the other's
    public static readonly ((double U, double V) A, (double U, double V) B, double Half)[] Ramps =
    {
        ((100, 278), (100, 262), 3.5),       // the quay up to the lowland
        ((176, 280), (176, 264), 3.5),       // the quay's east end up to the lowland
        ((152, 124), (172, 90), 3.0),        // the massif's second tier up to the summit plateau, west of the slope
        ((160, 228), (160, 207), 3.0),       // the lowland up onto the massif's first tier
        ((160, 196), (170, 146), 3.0),       // ... and on up to its second
    };

    // the ways people walk (gravel, LBA1's paths and quays): polylines (cells) and their half-width
    public static readonly ((double U, double V)[] Line, double Half)[] Paths =
    {
        (new (double, double)[] { (104, 296), (100, 278), (100, 262), (96, 256), (70, 250), (50, 246) }, 2.0),
        (new (double, double)[] { (100, 262), (109, 252), (109, 64), (112, 48) }, 2.2),
        (new (double, double)[] { (104, 262), (140, 262), (176, 258), (200, 238), (212, 228) }, 2.0),
        (new (double, double)[] { (50, 246), (44, 226), (52, 200), (58, 190) }, 1.6),
        (new (double, double)[] { (176, 280), (176, 264) }, 2.0),
    };

    // the Rabbibunnies' village: its brown earth (LBA1's scene 15), its domes and its carrot patch
    public static readonly (double U, double V)[] Village = { (20, 222), (78, 218), (88, 240), (80, 280), (40, 288), (18, 262) };
    public static readonly (double U, double V, double R)[] Domes =
    {
        (34, 236, 2.8), (54, 230, 3.2), (28, 258, 2.6), (62, 262, 3.0), (44, 274, 2.4), (72, 240, 2.2),
    };
    public static readonly (double U, double V) CarrotPatch = (70, 252);
    public static readonly (double U, double V) Plaza = (48, 250);

    // ---- the height at a point (cells, corners included); its tier and kind
    public static double Height(double u, double v)
    {
        if (!IsLand(u, v, out var inside)) return SeaFloor;
        // the shore ledge: a band along the coast, ragged; wider at the quay
        var h = TierAt(u, v);
        var (ru, rv) = Warp(u, v);
        // (the terraces not quite flat: drifts a few hundred units high, as LBA1's snow lies in steps and mounds)
        if (h >= T1) h += 260 * (Noise.Fbm(u / 13, v / 13, 3, 71) - 0.5);
        // the peak
        var dp = Dist(u, v, Peak);
        if (h >= T4 && dp < PeakRadius)
        {
            var t = 1 - dp / PeakRadius;
            var ridges = Noise.Ridged(u / 10, v / 10, 3, 53);
            h += PeakRise * Math.Pow(t, 1.3) * (0.75 + 0.5 * ridges);
        }
        // the frozen pond: its banks sloping two cells down to the ice (walkable: the land round it is reached across them), its isle's up
        if (h >= T1 && Inside(Pond, ru, rv))
        {
            var down = Smooth(SignedDistance(Pond, ru, rv) / PondBank) * (1 - Smooth((SignedDistance(PondIsle, ru, rv) + PondBank) / PondBank));
            h += (PondIce - h) * down;
        }
        // the ramps
        foreach (var (a, b, half) in Ramps)
        {
            var (t, d) = Along(a, b, u, v);
            if (t < 0 || t > 1 || d > half) continue;
            double ha = TierAt(a.U, a.V), hb = TierAt(b.U, b.V);
            h = ha + (hb - ha) * t;
        }
        // the ski slope, smooth across the tiers it cuts (and blended into them at its edges)
        var (ps, pd) = Along(PisteTop, PisteFoot, u, v);
        if (ps > -0.03 && ps < 1.03 && pd < PisteHalf + PisteEdge)
        {
            var s = Math.Clamp(ps, 0, 1);
            var profile = PisteFootY + (PisteTopY - PisteFootY) * Math.Pow(1 - s, 1.5);
            var w = pd <= PisteHalf ? 1 : 1 - Smooth((pd - PisteHalf) / PisteEdge);
            if (ps < 0) w *= 1 - Smooth(-ps / 0.03);
            if (ps > 1) w *= 1 - Smooth((ps - 1) / 0.03);
            h = h + (profile - h) * w;
        }
        return h;
    }

    // a region's tier at a point -- the shore ledge, the regions, the canyon -- without the drifts, the peak, the pond, the ramps or the
    // slope (and so a ramp's ends: it climbs from the ground there to the ground at its other end)
    private static double TierAt(double u, double v)
    {
        if (!IsLand(u, v, out var inside)) return SeaFloor;
        var ledge = 6 + 5 * Noise.Fbm(u / 9, v / 9, 2, 17);
        double h = inside < ledge || Inside(Quay, u, v) ? T0 : T1;
        if (h <= T0) return h;
        var (ru, rv) = Warp(u, v);
        foreach (var (tier, shape) in Regions) if (Inside(shape, ru, rv)) h = tier;
        if (ru > CanyonU0 && ru < CanyonU1 && rv > CanyonNorth - 1 && rv < CanyonSouth + 6) h = Math.Min(h, CanyonFloor(v));
        return h;
    }

    // Where a point is tested against the regions' outlines: a little off, so their cliffs meander (a slow sway of a few cells, and a
    // ragged edge on it, LBA1's scalloped cliff tops).
    public static (double U, double V) Warp(double u, double v) =>
        (u + 8 * (Noise.Fbm(u / 26, v / 26, 2, 31) - 0.5) + 2.4 * (Noise.Fbm(u / 5, v / 5, 2, 33) - 0.5),
         v + 8 * (Noise.Fbm(u / 26 + 40, v / 26, 2, 37) - 0.5) + 2.4 * (Noise.Fbm(u / 5 + 40, v / 5, 2, 39) - 0.5));

    // land: inside the coast, ragged; `inside` how far in (cells)
    public static bool IsLand(double u, double v, out double inside)
    {
        inside = SignedDistance(Coast, u, v) + 3.5 * (Noise.Fbm(u / 11, v / 11, 3, 7) - 0.5) * 2;
        // (the missing cube and the island's edges: sea, whatever)
        if (u < 64 + 6 && v < 64 + 6) inside = Math.Min(inside, Math.Max(u - 70, v - 70));
        inside = Math.Min(inside, Math.Min(Math.Min(u - 4, v - 4), Math.Min(Width - 4 - u, Depth - 4 - v)));
        return inside > 0;
    }

    public enum Kind { Sea, Snow, Cliff, Gravel, Earth, Pond, Piste, Rock, Ledge }

    // what a cell is by its middle: its ground (not the cliffs, which the build tells by the corners)
    public static Kind KindAt(double u, double v, double height)
    {
        if (height <= 0) return Kind.Sea;
        if (Inside(Pond, u, v) && !Inside(PondIsle, u, v) && Math.Abs(height - PondIce) < 64) return Kind.Pond;
        var (ps, pd) = Along(PisteTop, PisteFoot, u, v);
        if (ps >= 0 && ps <= 1 && pd < PisteHalf - 0.5) return Kind.Piste;
        foreach (var (line, half) in Paths)
            if (DistToLine(line, u, v) < half + 0.6 * (Noise.Fbm(u / 3, v / 3, 1, 91) - 0.5)) return Kind.Gravel;
        if (Inside(Village, u, v) && Noise.Fbm(u / 6, v / 6, 3, 61) > 0.56) return Kind.Earth;
        return Kind.Snow;
    }

    // ---- geometry
    public static double Smooth(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
    public static double Dist(double u, double v, (double U, double V) p) => Math.Sqrt((u - p.U) * (u - p.U) + (v - p.V) * (v - p.V));

    // how far along a -> b (0..1) the point's foot is, and how far off the line it is
    public static (double T, double D) Along((double U, double V) a, (double U, double V) b, double u, double v)
    {
        double dx = b.U - a.U, dz = b.V - a.V, len2 = dx * dx + dz * dz;
        var t = ((u - a.U) * dx + (v - a.V) * dz) / len2;
        double fx = a.U + t * dx, fz = a.V + t * dz;
        return (t, Math.Sqrt((u - fx) * (u - fx) + (v - fz) * (v - fz)));
    }

    // a point `across` cells to the right of a -> b (facing b), `back` cells before a along it
    public static (double U, double V) Across((double U, double V) a, (double U, double V) b, double across, double back)
    {
        double dx = b.U - a.U, dz = b.V - a.V, len = Math.Sqrt(dx * dx + dz * dz);
        dx /= len; dz /= len;
        return (a.U - dz * across - dx * back, a.V + dx * across - dz * back);
    }

    public static double DistToLine((double U, double V)[] line, double u, double v)
    {
        var best = double.MaxValue;
        for (var i = 0; i + 1 < line.Length; i++)
        {
            var (t, d) = Along(line[i], line[i + 1], u, v);
            best = Math.Min(best, t < 0 ? Dist(u, v, line[i]) : t > 1 ? Dist(u, v, line[i + 1]) : d);
        }
        return best;
    }

    public static bool Inside((double U, double V)[] poly, double u, double v)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].V > v) != (poly[j].V > v) && u < (poly[j].U - poly[i].U) * (v - poly[i].V) / (poly[j].V - poly[i].V) + poly[i].U) inside = !inside;
        return inside;
    }

    // inside: positive, the distance to the outline
    public static double SignedDistance((double U, double V)[] poly, double u, double v)
    {
        var best = double.MaxValue;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var (t, d) = Along(poly[j], poly[i], u, v);
            best = Math.Min(best, t < 0 ? Dist(u, v, poly[j]) : t > 1 ? Dist(u, v, poly[i]) : d);
        }
        return Inside(poly, u, v) ? best : -best;
    }
}

// Value noise, the same every build (no Random: a hash of the lattice point and a seed), 0..1.
internal static class Noise
{
    private static double Hash(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (double)0xFFFFFF;
        }
    }

    public static double Value(double x, double y, int seed)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        var a = Hash(x0, y0, seed) + (Hash(x0 + 1, y0, seed) - Hash(x0, y0, seed)) * fx;
        var b = Hash(x0, y0 + 1, seed) + (Hash(x0 + 1, y0 + 1, seed) - Hash(x0, y0 + 1, seed)) * fx;
        return a + (b - a) * fy;
    }

    public static double Fbm(double x, double y, int octaves, int seed)
    {
        double sum = 0, amp = 1, norm = 0;
        for (var o = 0; o < octaves; o++)
        {
            sum += amp * Value(x, y, seed + o * 101);
            norm += amp; amp *= 0.5; x *= 2; y *= 2;
        }
        return sum / norm;
    }

    // ridges: 1 along the noise's middle line, 0 away from it
    public static double Ridged(double x, double y, int octaves, int seed)
    {
        double sum = 0, amp = 1, norm = 0;
        for (var o = 0; o < octaves; o++)
        {
            sum += amp * (1 - Math.Abs(2 * Value(x, y, seed + o * 101) - 1));
            norm += amp; amp *= 0.5; x *= 2; y *= 2;
        }
        return sum / norm;
    }

    // a number 0..1 for a thing (a tree's turn, a variant), the same every build
    public static double Of(int a, int b, int seed) => Hash(a, b, seed);
}
