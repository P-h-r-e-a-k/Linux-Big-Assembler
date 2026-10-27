namespace LBAAssembler.Terrain.Palace;

// The hill the palace stands on (the user, 2026-10-12, with a picture of Otringal's palace from above: "Our new palace has a weird ground
// area that we should get rid of. The original palace is sat on a raised cliff area ... We need to make the outside look more like the
// original ... It's okay for the inside to be underground"). On Otringal's island the palace's halls stand on a flat hilltop (17,000 in cube
// (8, 7)) ringed by a rocky rim a little higher (up to 2,900 over it), its sides cliffs falling to the cloud sea; all of it the mossy rock of
// OTRINGAL.ILE's ground atlas. Here the same, round the palace's rooms, which are under it:
//   - the plateau: the rooms' footprint and a margin round their walls, Margin cells give or take Wobble;
//   - its top: a terrace flat at the roofs' top Terrace cells round the rooms (the roofs, rock-topped, and the palace's foot), and beyond it
//     rocks, rising -- the more towards the plateau's edge, its rim -- up to RockRise over it (but along the Corridors, flat: the ways a
//     road crosses the rocks). (The user, 2026-10-13: "Make the island small enough so that it only just fits the inside of the palace,
//     then have the turret's on the roof cover the whole of our inside of the palace": the island is the palace's L and a few cells, all of
//     it flat, the turrets -- PalaceOutside -- over the whole of it. It was a plateau 30 cells, then 66, past the outline's hull -- the L's
//     corner hilltop too -- with a terrace 10, 24, then 54 cells wide in it and rocks beyond);
//   - its sides: cliffs falling to the sea floor over Face cells give or take FaceWobble, ragged;
//   - under the rooms the ground is low (it is their floor's foot), out of sight;
//   - at the long hall's end (the round window, Twinsen's way out) the plateau stops at the hall's wall: the window looks out from the
//     cliff over the cloud sea;
//   - the harbour (when given): a quay at the cliff's foot and, out in the bay, a rock pillar whose top is level with the hilltop.
// The ground is painted with Otringal's rock (4 x 4 tiles of 32 pixels, as its hill is), cells under the sea left undrawn (the sea shows),
// and lit as the island's light falls on it.
internal static class PalaceCliff
{
    public const int Margin = 6;                // the plateau round the rooms' walls (cells) ...
    public const int Wobble = 2;                // ... give or take this much
    public const int Terrace = 12;              // the flat terrace round the rooms' walls (cells: all of the plateau -- no rocks) ...
    public const double RockRise = 6000;        // ... and beyond it the rocks, up to this much over it
    public const int Rim = 10;                  // the plateau's outer cells: its rim, the rocks highest
    public const int Face = 9;                  // the cliffs: cells from the plateau's edge down to the sea floor ...
    public const int FaceWobble = 4;            // ... give or take this much
    public const int SeaFloor = -700;
    public const int FarSea = 8;                // cells of sea floor past the cliffs' foot, inside the island's cubes
    // the rock of Otringal's hill: its ground atlas's mossy rock, 4 x 4 tiles of 32 pixels from (128, 128)
    private static readonly (int X, int Y) Rock = (128, 128);
    private const int RockTile = 32, RockTiles = 4;
    // the quay's stone: the grey paving of Otringal's hilltop (2 x 4 tiles of 32 pixels from (0, 64))
    private static readonly (int X, int Y) Paving = (0, 64);

    // Where the rooms are (island cells, inclusive), the plateau's height (the roofs' top), the ground under the rooms, where the plateau
    // stops at a wall (the hall's end: its cells' x range and the wall's outer face at z, the cliff on the side z decreases), the ways a road
    // crosses the rocks (island cells: a line of points and its half width), and the harbour.
    public sealed record Site(List<(int X0, int Z0, int X1, int Z1)> Rooms, double Top, double Under, (int X0, int X1, int Z)? CliffAt,
        List<Corridor> Corridors, Harbour? Harbour);

    // A way across the hilltop's rocks, flat at its top: its middle line (island cells) and half its width (cells).
    public sealed record Corridor(List<(double X, double Z)> Points, double Half);

    // The harbour: a quay at the cliff's foot (island cells: a rectangle, its height) and a pillar out in the bay (its middle, radius, top).
    public sealed record Harbour((int X0, int Z0, int X1, int Z1) Quay, double QuayY, (double X, double Z, double R, double Top) Pillar);

    public static List<string> Shape(IslandFile island, IEnumerable<(int X, int Z)> cubes, Site site)
    {
        var log = new List<string>();
        var cubeList = cubes.ToList();
        bool InRooms(int cx, int cz) => site.Rooms.Any(r => cx >= r.X0 && cx <= r.X1 && cz >= r.Z0 && cz <= r.Z1);
        // (a vertex whose four cells are all the rooms': inside their outer walls -- a vertex on the walls' outer face is the hilltop's; the
        // rooms share their walls, so a cell of any of them)
        bool UnderRooms(int gx, int gz) => InRooms(gx - 1, gz - 1) && InRooms(gx, gz - 1) && InRooms(gx - 1, gz) && InRooms(gx, gz);

        // (how much a corridor flattens a vertex: 1 within its width, none 5 cells past it)
        double Flattened(int gx, int gz)
        {
            var flat = 0.0;
            foreach (var c in site.Corridors)
                for (var i = 0; i + 1 < c.Points.Count; i++)
                    flat = Math.Max(flat, 1 - SmoothStep(c.Half, c.Half + 5, ToSegment(gx, gz, c.Points[i], c.Points[i + 1])));
            return flat;
        }
        // (the rocks: crags -- sharp ridges, as rock breaks -- on swells, 0 to about 1)
        static double Rough(int gx, int gz)
        {
            var ridges = Math.Pow(1 - Math.Abs(Noise(gx / 7.0, gz / 7.0, 2)), 3);
            var fine = Math.Pow(1 - Math.Abs(Noise(gx / 2.6, gz / 2.6, 8)), 2);
            var swell = 0.5 + 0.5 * Noise(gx / 27.0, gz / 27.0, 3);
            return Math.Clamp((0.7 * ridges + 0.3 * fine) * (0.3 + 0.7 * swell) * 1.35, 0, 1.1);
        }

        // (how far a vertex is from the rooms, in cells: 0 on their walls' outer face -- the island only just round them, the corner of their
        // L sea)
        double ToRooms(int gx, int gz) => site.Rooms.Min(r =>
        {
            double dx = Math.Max(Math.Max(r.X0 - gx, gx - (r.X1 + 1)), 0), dz = Math.Max(Math.Max(r.Z0 - gz, gz - (r.Z1 + 1)), 0);
            return Math.Sqrt(dx * dx + dz * dz);
        });

        double HeightAt(int gx, int gz)
        {
            if (UnderRooms(gx, gz)) return site.Under;
            var d = ToRooms(gx, gz);
            // the plateau's reach here: Margin give or take Wobble -- bays and spurs -- and none beside the hall's end
            var margin = Margin + Wobble * (0.65 * Noise(gx / 31.0, gz / 31.0, 1) + 0.35 * Noise(gx / 11.0, gz / 11.0, 6));
            if (site.CliffAt is { } c && gz <= c.Z)
            {
                // (the hall's end: its wall stands on the cliff's edge, the ground at its foot the rooms' floor's, the cliff under it -- the
                // round window looks out over the cloud sea)
                if (gx >= c.X0 && gx <= c.X1 + 1)
                {
                    var t0 = Math.Clamp((c.Z - gz) / (double)Face, 0, 1);
                    return site.Under + (SeaFloor - site.Under) * Math.Pow(t0, 0.65) + 500 * Noise(gx / 2.7, gz / 2.7, 4) * Math.Sin(Math.PI * t0);
                }
                var beside = Math.Max(c.X0 - gx, gx - c.X1 - 1);
                margin *= SmoothStep(4, 24, beside);
            }
            // the top: the terrace round the rooms' walls, then the rocks, the higher towards the rim (but along a corridor, flat)
            var rise = RockRise * Rough(gx, gz) * SmoothStep(Terrace, Terrace + 10, d) * (0.55 + 0.45 * SmoothStep(margin - Rim, margin, d))
                       * (1 - Flattened(gx, gz));
            if (d <= margin) return site.Top + rise;
            // the cliff: from the rocks at the edge down to the sea floor, ragged, its face the wider or narrower here and there
            var face = Face + FaceWobble * Noise(gx / 19.0, gz / 19.0, 7);
            var t = Math.Clamp((d - margin) / face, 0, 1);
            var fall = Math.Pow(t, 0.6);
            var ragged = 950 * Noise(gx / 2.5, gz / 2.5, 4) * Math.Sin(Math.PI * t);
            return site.Top + rise + (SeaFloor - site.Top - rise) * fall + ragged;
        }

        // ---- the heights, vertex by vertex (the harbour on top: the quay where it is higher than the cliff's foot, the pillar)
        var heights = new Dictionary<(int, int), double>();
        double Final(int gx, int gz)
        {
            if (heights.TryGetValue((gx, gz), out var known)) return known;
            var h = HeightAt(gx, gz);
            if (site.Harbour is { } hb)
            {
                var (qx0, qz0, qx1, qz1) = hb.Quay;
                // (the quay: flat at its height, its edge a short drop into the sea)
                var outQ = Math.Max(Math.Max(qx0 - gx, gx - qx1), Math.Max(qz0 - gz, gz - qz1));
                if (outQ <= 0) h = Math.Max(h, hb.QuayY);
                else if (outQ < 2) h = Math.Max(h, hb.QuayY + (SeaFloor - hb.QuayY) * outQ / 2);
                // (the pillar: flat on top, its sides falling sheer, flaring at the foot)
                var (px, pz, pr, ptop) = hb.Pillar;
                var pd = Math.Sqrt((gx - px) * (gx - px) + (gz - pz) * (gz - pz));
                if (pd <= pr) h = Math.Max(h, ptop);
                else if (pd < pr + 6) h = Math.Max(h, ptop + (SeaFloor - ptop) * Math.Pow((pd - pr) / 6, 0.35) + 500 * Noise(gx / 1.7, gz / 1.7, 5) * Math.Sin(Math.PI * (pd - pr) / 6));
            }
            return heights[(gx, gz)] = h;
        }

        foreach (var (cx, cz) in cubeList)
        {
            if (island.CubeAt(cx, cz) is not { } cube) continue;
            // (the light, the sky and the sea: Otringal's, the sky high over the domes, the sea everywhere the ground isn't over it)
            cube.BitField = 0xFFFF;
            cube.Info[IslandCube.InfoSkyY] = Math.Max(cube.Info[IslandCube.InfoSkyY], SkyY);
            // (the fog further off than Otringal's 10,500 to 41,200: the hill and its domes seen across the bay, the domes -- 26,000 over the
            // roofs -- from the race's road as it nears them, and a room's far wall through its door from the next, rooms 28,000 across; the
            // engine's depth buffer is scaled to the far end, so it can be further than 16 bits)
            cube.Info[IslandCube.InfoStartZFog] = StartFog; cube.Info[IslandCube.InfoClipZFar] = FarClip;
            for (var vz = 0; vz < IslandCube.Vertices; vz++)
                for (var vx = 0; vx < IslandCube.Vertices; vx++)
                {
                    var h = Final(cx * 64 + vx, cz * 64 + vz);
                    cube.Heights[vz * IslandCube.Vertices + vx] = (short)Math.Clamp(Math.Round(h), short.MinValue, short.MaxValue);
                }
        }

        // ---- the paint: the rock everywhere over the sea (the quay's top paved), nothing under it
        var painted = 0; var sea = 0;
        foreach (var (cx, cz) in cubeList)
        {
            if (island.CubeAt(cx, cz) is not { HasPolygons: true } cube) continue;
            for (var lz = 0; lz < IslandCube.Cells; lz++)
                for (var lx = 0; lx < IslandCube.Cells; lx++)
                {
                    int gx = cx * 64 + lx, gz = cz * 64 + lz;
                    var hs = new[] { cube.Height(lx, lz), cube.Height(lx + 1, lz), cube.Height(lx, lz + 1), cube.Height(lx + 1, lz + 1) };
                    var underSea = hs.All(h => h <= 0);
                    var quay = site.Harbour is { } hb && gx >= hb.Quay.X0 && gx <= hb.Quay.X1 && gz >= hb.Quay.Z0 && gz <= hb.Quay.Z1 && hs.All(h => Math.Abs(h - hb.QuayY) < 1);
                    // (the quay paved; and the ground under the rooms, out of sight -- but for a crack between a floor and a model -- a stone floor)
                    var paved = quay || InRooms(gx, gz);
                    var (tx, ty) = paved ? (Paving.X + RockTile * (gx & 1), Paving.Y + RockTile * (gz & 3)) : (Rock.X + RockTile * (gx % RockTiles), Rock.Y + RockTile * (gz % RockTiles));
                    // (the cut that suits the ground: along the diagonal whose ends are nearer in height)
                    var diagonal = Math.Abs(hs[1] - hs[2]) < Math.Abs(hs[0] - hs[3]);
                    for (var half = 0; half < 2; half++)
                    {
                        var polygon = new IslandPolygon(cube.Polygon(lx, lz, half));
                        if (underSea)
                        {
                            cube.SetPolygon(lx, lz, half, polygon.With(texFlag: 0, polyFlag: 0, codeJeu: 0, diagonal: diagonal).Raw);
                            continue;
                        }
                        var index = IslandGround.TextureIndexFor(cube, IslandGround.TileDefinition(tx, ty, RockTile - 1, RockTile - 1, diagonal, half));
                        cube.SetPolygon(lx, lz, half, island.WithGroundTexture(polygon.With(texFlag: 2, polyFlag: 0, codeJeu: 0, diagonal: diagonal), 0, index).Raw);
                    }
                    if (underSea) sea++; else painted++;
                }
        }

        // ---- the light: the island's sun on the slopes, the cliffs' shadows
        var region = new Cells(cubeList);
        var light = island.CubeAt(cubeList[0].X, cubeList[0].Z) is { } first ? BakeOptions.For(first) : new BakeOptions();
        light.TerrainShadows = true;
        IslandBake.Bake(island, region, light);
        log.Add($"the hill: a plateau at {site.Top:0} round the rooms (their outline and {Margin} cells give or take {Wobble}), a terrace over the L's corner and {Terrace} cells round them, " +
                $"rocks beyond up to {RockRise:0} over it ({site.Corridors.Count} ways across them flat), cliffs of {Face} cells give or take {FaceWobble} to the sea floor at {SeaFloor}; " +
                $"{painted} cells of Otringal's rock, {sea} under the sea" +
                (site.Harbour is { } h2 ? $"; a harbour quay at {h2.QuayY:0}, a pillar out in the bay ({h2.Pillar.R:0.#} cells across its top, at {h2.Pillar.Top:0})" : ""));
        return log;
    }

    public const int SkyY = 90000;              // the sky over the turrets (Otringal's is at 22,199, under its domes)
    public const int StartFog = 40000, FarClip = 70000;

    // ---- helpers

    // How far a point is from a segment.
    private static double ToSegment(double x, double z, (double X, double Z) a, (double X, double Z) b)
    {
        double ex = b.X - a.X, ez = b.Z - a.Z, len2 = ex * ex + ez * ez;
        var t = len2 > 0 ? Math.Clamp(((x - a.X) * ex + (z - a.Z) * ez) / len2, 0, 1) : 0;
        double dx = x - (a.X + t * ex), dz = z - (a.Z + t * ez);
        return Math.Sqrt(dx * dx + dz * dz);
    }

    private static double SmoothStep(double a, double b, double x)
    {
        var t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    // Smooth value noise in -1..1, the same for the same place and seed whatever the build (no Random: the files come out the same).
    private static double Noise(double x, double z, int seed)
    {
        int x0 = (int)Math.Floor(x), z0 = (int)Math.Floor(z);
        double fx = x - x0, fz = z - z0;
        double sx = fx * fx * (3 - 2 * fx), sz = fz * fz * (3 - 2 * fz);
        double a = Hash(x0, z0, seed), b = Hash(x0 + 1, z0, seed), c = Hash(x0, z0 + 1, seed), d = Hash(x0 + 1, z0 + 1, seed);
        return a + (b - a) * sx + (c - a) * sz + (a - b - c + d) * sx * sz;
    }

    private static double Hash(int x, int z, int seed)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + z * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return h / (double)uint.MaxValue * 2 - 1;
        }
    }

    // The cubes' cells, as a region to light.
    private sealed class Cells(List<(int X, int Z)> cubes) : IslandRegion
    {
        public override IEnumerable<(int Gx, int Gz, double Weight)> Vertices(IslandFile island)
        {
            foreach (var (cx, cz) in cubes)
                for (var z = 0; z <= 64; z++) for (var x = 0; x <= 64; x++) yield return (cx * 64 + x, cz * 64 + z, 1.0);
        }
        public override (double Gx, double Gz) Center => (cubes.Average(c => c.X) * 64 + 32, cubes.Average(c => c.Z) * 64 + 32);
    }
}
