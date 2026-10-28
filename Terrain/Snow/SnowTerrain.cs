using System.IO;
using LBAAssembler.Lba1;
using LBAAssembler.Terrain.Polar;

namespace LBAAssembler.Terrain.Snow;

// The snow island's ground (SnowDesign's heights) as an LBA2 island file: its cubes' settings Island CX's (the island whose look the map has:
// its lavender-grey sky over a grey sea, its palette), every patch with sea; each vertex as high as the design says; each cell textured with
// LBA1's own Hamalayi bricks -- their top faces for the snow, the gravel of the quays and paths, the Rabbibunnies' earth, the ice (the
// clear water's bricks, frosted) and the ski slope (the snow combed), their sides for the ice cliffs -- in the island's palette; the sea
// undrawn (the engine's sea shows), water to Twinsen; cliffs walls; the light baked from the cubes' sun.
internal static class SnowTerrain
{
    // the island whose look the map takes (MapNew.GameIslands): its sky, sea, palette and music
    public static string LookIsland = "DESERT";
    public static int LookIslandByte => Maps.MapNew.GameIslands.First(g => g.File == LookIsland).Byte;
    public static int LookPalette => LookIsland == "CITABAU" ? 42 : 27 + LookIslandByte;

    // LBA1's bricks (LBA_BRK.HQR) of Hamalayi's outside scenes, by what they are (the most used of each: HamalayiStudy)
    public static readonly int[] SnowBricks = { 2711, 2712, 2713, 2714, 2719, 2720, 2721, 2722 };
    public static readonly int[] CliffBricks = { 3033, 3034, 3040, 3041, 3042, 3048, 3049, 3050 };
    public static readonly int[] GravelBricks = { 3004, 3005, 3006, 3007 };
    public static readonly int[] EarthBricks = { 3976, 3977, 3978, 3979, 3980, 3981, 3982 };
    public static readonly int[] WaterBricks = { 2777, 2781, 2783, 2785, 2786 };
    public static readonly int[] RockBricks = { 4079, 4089, 4093 };

    // a cell whose corners are this far apart is a cliff (LBA1's tiers are 8 layers; its one- and two-layer steps stay slopes)
    public const int CliffRise = 900;
    // steeper than this is a wall to Twinsen (the triangles' collision bit), and over the summit plateau rock
    public const double SteepestWalk = 40, RockSlope = 30;
    // the light on flat ground: the palette's own colours (XPL ShadeNormalLevel 12), slopes a part of the bake's difference round it
    public const int NormalLight = 12;
    public const int SkyOverPeak = 6000;
    // the snow's tiles taken this far towards this white
    public static double SnowWhiter = 0.5;
    private static readonly (double R, double G, double B) White = (238, 246, 250);
    public const double SlopeLight = 0.8;

    public sealed record Result(IslandFile Island, byte[] Palette, Dictionary<SnowDesign.Kind, int> Cells, List<string> Log);

    public static Result Build(Lba1Game lba1, string gameDirectory)
    {
        var log = new List<string>();
        var source = IslandFile.Load(Path.Combine(gameDirectory, LookIsland + ".ILE"));
        var cubes = SnowDesign.Cubes().ToList();
        var island = IslandFile.Parse(IslandScaler.NewFile(source, cubes), "SNOWISLE.ILE");
        // (the cubes' settings: the look island's cube with the most land, the sea in every patch, no animated tiles)
        var settings = source.Cubes.Values.Where(c => c.HasPolygons).OrderByDescending(c => c.Heights.Count(h => h > 0)).First();
        foreach (var (cx, cz) in cubes)
        {
            var cube = island.CubeAt(cx, cz)!;
            cube.Info = (int[])settings.Info.Clone();
            cube.Info[IslandCube.InfoNbDecors] = 0;
            cube.BitField = 0xFFFF;
            for (var i = 6; i < IslandCube.InfoSize; i++) cube.Info[i] = -1;
        }
        log.Add($"SNOWISLE.ILE: {cubes.Count} cubes ({SnowDesign.CubeX0}..{SnowDesign.CubeX0 + SnowDesign.CubesX - 1} x {SnowDesign.CubeZ0}..{SnowDesign.CubeZ0 + SnowDesign.CubesZ - 1}, " +
                $"not {SnowDesign.MissingCube}), {LookIsland}'s cube settings (sky height {settings.Info[IslandCube.InfoSkyY]}, fog from {settings.Info[IslandCube.InfoStartZFog]} to {settings.Info[IslandCube.InfoClipZFar]})");

        // the heights
        var heights = new double[SnowDesign.Width + 1, SnowDesign.Depth + 1];
        for (var v = 0; v <= SnowDesign.Depth; v++) for (var u = 0; u <= SnowDesign.Width; u++) heights[u, v] = SnowDesign.Height(u, v);
        foreach (var (cx, cz) in cubes)
        {
            var cube = island.CubeAt(cx, cz)!;
            int u0 = (cx - SnowDesign.CubeX0) * 64, v0 = (cz - SnowDesign.CubeZ0) * 64;
            for (var z = 0; z <= 64; z++)
                for (var x = 0; x <= 64; x++)
                    cube.Heights[z * IslandCube.Vertices + x] = (short)Math.Clamp(Math.Round(heights[u0 + x, v0 + z]), -32000, 32000);
        }
        var highest = heights.Cast<double>().Max();
        // (the sky over the peak: the look island's is flat, its sky lower than the peak)
        var sky = Math.Max(settings.Info[IslandCube.InfoSkyY], (int)highest + SkyOverPeak);
        foreach (var (cx, cz) in cubes) island.CubeAt(cx, cz)!.Info[IslandCube.InfoSkyY] = sky;
        log.Add($"the ground: highest {highest:0} (the peak), the tiers {SnowDesign.T0}/{SnowDesign.T1}/{SnowDesign.T2}/{SnowDesign.T3}/{SnowDesign.T4}; the sky at {sky}");

        var all = new List<(int Gx, int Gz)>();
        foreach (var (cx, cz) in cubes) for (var z = 0; z < 64; z++) for (var x = 0; x < 64; x++) all.Add((cx * 64 + x, cz * 64 + z));
        IslandGround.OptimiseDiagonals(island, new Cells(all));

        // what each cell is, and its tile
        var palette = IslandMapRenderer.LoadPaletteEntry(gameDirectory, LookPalette);
        var tiles = new Tiles(lba1, palette);
        var kinds = new Dictionary<(int Gx, int Gz), (SnowDesign.Kind Kind, object Key, int Up)>();
        var counts = new Dictionary<SnowDesign.Kind, int>();
        foreach (var (gx, gz) in all)
        {
            int u = gx - SnowDesign.CubeX0 * 64, v = gz - SnowDesign.CubeZ0 * 64;
            var c = new[] { heights[u, v], heights[u, v + 1], heights[u + 1, v + 1], heights[u + 1, v] };
            var rise = c.Max() - c.Min();
            var middle = c.Average();
            SnowDesign.Kind kind;
            if (c.Max() <= 0) kind = SnowDesign.Kind.Sea;
            else if (rise >= CliffRise || c.Min() <= 0) kind = SnowDesign.Kind.Cliff;
            else
            {
                kind = SnowDesign.KindAt(u + 0.5, v + 0.5, middle);
                var slope = Math.Atan(rise / (IslandFile.CellSize * Math.Sqrt(2))) * 180 / Math.PI;
                if (kind == SnowDesign.Kind.Snow && middle > SnowDesign.T4 + 600 && slope > RockSlope) kind = SnowDesign.Kind.Rock;
            }
            counts[kind] = counts.TryGetValue(kind, out var n) ? n + 1 : 1;
            if (kind == SnowDesign.Kind.Sea) continue;
            // (the way up the cell: 0 +x, 1 -x, 2 +z, 3 -z -- a cliff's tile stands along its high edge)
            double dx = (c[2] + c[3] - c[0] - c[1]) / 2, dz = (c[1] + c[2] - c[0] - c[3]) / 2;
            var up = Math.Abs(dx) >= Math.Abs(dz) ? (dx >= 0 ? 0 : 1) : (dz >= 0 ? 2 : 3);
            var pick = (int)(Noise.Of(gx, gz, 5) * 1000);
            object key = kind switch
            {
                SnowDesign.Kind.Cliff => ("cliff", pick % 4),
                SnowDesign.Kind.Gravel => ("gravel", pick % GravelBricks.Length),
                SnowDesign.Kind.Earth => ("earth", pick % EarthBricks.Length),
                SnowDesign.Kind.Pond => ("ice", pick % WaterBricks.Length),
                SnowDesign.Kind.Piste => ("piste", pick % 4),
                SnowDesign.Kind.Rock => ("rock", pick % RockBricks.Length),
                _ => ("snow", pick % SnowBricks.Length),
            };
            kinds[(gx, gz)] = (kind, key, up);
        }
        // (where gravel or earth meets the snow, a tile of both: the cells' square edges softened)
        var edges = 0;
        foreach (var (at, (kind, key, up)) in kinds.ToList())
        {
            if (kind is not (SnowDesign.Kind.Gravel or SnowDesign.Kind.Earth)) continue;
            var bySnow = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(d => kinds.TryGetValue((at.Gx + d.Item1, at.Gz + d.Item2), out var n) && n.Kind is SnowDesign.Kind.Snow or SnowDesign.Kind.Piste);
            if (!bySnow) continue;
            var (what, variant) = ((string, int))key;
            kinds[at] = (kind, (what + "edge", variant % 4), up);
            edges++;
        }
        log.Add("the cells: " + string.Join(", ", counts.OrderByDescending(k => k.Value).Select(k => $"{k.Value} {k.Key}")) + $" ({edges} of gravel and earth by the snow, both)");

        // the tiles on the ground's pages
        var pages = new PolarTextures.Pages(island.GroundTexture, PolarTextures.Colours.AsTheyAre(palette), IslandFile.MaxGroundPages);
        foreach (var key in kinds.Values.Select(k => k.Key).Distinct().OrderBy(k => k.ToString()))
            if (!pages.Add(key, tiles.For(key), PolarTextures.TileSize, PolarTextures.TileSize))
                throw new InvalidOperationException($"The ground's textures take more than {IslandFile.MaxGroundPages} pages.");
        island.GroundPages.Clear();
        island.GroundPages.AddRange(pages.List.Skip(1));
        log.Add($"the ground's textures: {pages.Place.Count} tiles of LBA1's Hamalayi bricks on {pages.Count} page(s)");

        // the cells: the sea first (undrawn, water), then each drawn one its tile
        foreach (var (gx, gz) in all)
        {
            var cube = island.CubeAt(gx / 64, gz / 64)!;
            for (var half = 0; half < 2; half++)
                cube.SetPolygon(gx % 64, gz % 64, half, new IslandPolygon(cube.Polygon(gx % 64, gz % 64, half)).With(bank: 6, texFlag: 0, polyFlag: 0, sampleStep: 0, codeJeu: 1, textureIndex: 0).Raw);
        }
        var mostDefinitions = 0;
        foreach (var ((gx, gz), (kind, key, up)) in kinds)
        {
            var cube = island.CubeAt(gx / 64, gz / 64)!;
            int lx = gx % 64, lz = gz % 64;
            var (page, tx, ty) = pages.Place[key];
            var diagonal = new IslandPolygon(cube.Polygon(lx, lz, 0)).Diagonal;
            for (var half = 0; half < 2; half++)
            {
                var definition = kind == SnowDesign.Kind.Cliff
                    ? SlopeDefinition(tx, ty, PolarTextures.TileSize, PolarTextures.TileSize, diagonal, half, up)
                    : IslandGround.TileDefinition(tx, ty, PolarTextures.TileSize, PolarTextures.TileSize, diagonal, half);
                var index = IslandGround.TextureIndexFor(cube, definition);
                if (index >= island.MaxGroundDefinitions) throw new InvalidOperationException($"Cube {cube.Id} needs more than {island.MaxGroundDefinitions} texture definitions.");
                mostDefinitions = Math.Max(mostDefinitions, index + 1);
                var p = new IslandPolygon(cube.Polygon(lx, lz, half));
                cube.SetPolygon(lx, lz, half, island.WithGroundTexture(p.With(bank: Land.Bank, texFlag: Land.TexFlag, polyFlag: Land.PolyFlag, sampleStep: Land.SampleStep, codeJeu: 0), page, index).Raw);
            }
        }
        log.Add($"at most {mostDefinitions} texture definitions in a cube");
        var walls = IslandGround.SetSteepCollision(island, new Cells(all), SteepestWalk);
        log.Add($"{walls} triangles steeper than {SteepestWalk} degrees are walls (the ice cliffs)");

        // the light: the bake's (the cubes' sun, the mountains' shadows), moved so flat ground has the palette's own colours
        var bake = BakeOptions.For(island.CubeAt(cubes[0].X, cubes[0].Z)!);
        bake.TerrainShadows = true;
        var vertices = new List<(int, int)>();
        foreach (var (cx, cz) in cubes) for (var z = 0; z <= 64; z++) for (var x = 0; x <= 64; x++) vertices.Add((cx * 64 + x, cz * 64 + z));
        IslandBake.Bake(island, new Cells(vertices), bake);
        var flat = cubes.SelectMany(c => island.CubeAt(c.X, c.Z)!.Intensity).GroupBy(b => b & 15).OrderByDescending(g => g.Count()).First().Key;
        foreach (var (cx, cz) in cubes)
        {
            var cube = island.CubeAt(cx, cz)!;
            for (var i = 0; i < cube.Intensity.Length; i++)
            {
                var light = Math.Clamp(NormalLight + (int)Math.Round(((cube.Intensity[i] & 15) - flat) * SlopeLight), 0, 15);
                cube.Intensity[i] = (byte)((cube.Intensity[i] & 0xF0) | light);
            }
        }
        log.Add($"the light: flat ground (the bake's {flat}) at {NormalLight}, slopes {SlopeLight} of the bake's difference round it");
        return new Result(island, palette, counts, log);
    }

    // A land cell's flags, as the fine-weather Citadel's grass has them (bank 1, textured and Gouraud-lit, footsteps 2).
    private static readonly IslandPolygon Land = new IslandPolygon(0).With(bank: 1, texFlag: 3, polyFlag: 0, sampleStep: 2);

    // A tile on a slope: its top (v = 0) along the cell's high edge, `up` the way up (0 +x, 1 -x, 2 +z, 3 -z) -- Polar Island's cliffs'.
    private static ushort[] SlopeDefinition(int x, int y, int width, int height, bool diagonal, int half, int up)
    {
        var straight = IslandGround.TileDefinition(0, 0, 1, 1, diagonal, half);
        var result = new ushort[6];
        for (var i = 0; i < 3; i++)
        {
            int ox = straight[i * 2] > 128 ? 1 : 0, oz = straight[i * 2 + 1] > 128 ? 1 : 0;
            var (u, v) = up switch { 0 => (oz, 1 - ox), 1 => (1 - oz, ox), 2 => (ox, 1 - oz), _ => (1 - ox, oz) };
            result[i * 2] = (ushort)(u == 0 ? x * 256 + 27 : (x + width) * 256 - 14);
            result[i * 2 + 1] = (ushort)(v == 0 ? y * 256 + 13 : (y + height) * 256 - 14);
        }
        return result;
    }

    // The ground's tiles (32 x 32, the island palette's indices) from LBA1's bricks: their top faces, as LBA1 draws them; the ice the
    // clear water's, frosted (a third of the way to the snow's pale cyan); the ski slope the snow combed (a darker line every four texels
    // down the slope's fall, as a piste basher leaves it); a cliff two bricks' ice-cliff sides one over the other.
    private sealed class Tiles(Lba1Game lba1, byte[] palette)
    {
        public int[] For(object key)
        {
            var (what, n) = ((string, int))key;
            (double R, double G, double B)[] snow = Rgb(SnowBricks[(n * 3 + 1) % SnowBricks.Length], c => Whiter(c, SnowWhiter));
            return Quantise(what switch
            {
                "snow" => Rgb(SnowBricks[n], c => Whiter(c, SnowWhiter)),
                "gravel" => Rgb(GravelBricks[n], c => c),
                "earth" => Rgb(EarthBricks[n], Earthier),
                "graveledge" => Mixed(Rgb(GravelBricks[n], c => c), snow, 0.5, n),
                "earthedge" => Mixed(Rgb(EarthBricks[n], Earthier), snow, 0.45, n + 10),
                "rock" => Rgb(RockBricks[n], c => c),
                "ice" => Rgb(WaterBricks[n], c => (c.R + (196 - c.R) / 3, c.G + (232 - c.G) / 3, c.B + (238 - c.B) / 3)),
                "piste" => Piste(n),
                _ => Cliff(n),
            });
        }

        // (LBA1's snow is a pale cyan; the island palette's nearest to it a stronger one: the snow is taken part of the way to white first)
        private static (double R, double G, double B) Whiter((double R, double G, double B) c, double t) =>
            (Math.Min(255, c.R + (White.R - c.R) * t), Math.Min(255, c.G + (White.G - c.G) * t), Math.Min(255, c.B + (White.B - c.B) * t));
        // (the Rabbibunnies' earth a little lighter: LBA1's is near black on the island's snow)
        private static (double R, double G, double B) Earthier((double R, double G, double B) c) => (c.R * 1.15 + 14, c.G * 1.15 + 10, c.B * 1.15 + 8);

        private (double R, double G, double B)[] Rgb(int brick, Func<(double R, double G, double B), (double R, double G, double B)> change)
        {
            var tile = PolarTextures.Sprite.Decode(lba1.ReadBrick(brick)).Tile(PolarTextures.Face.Top);
            return tile.Select(c => change(PolarTextures.Colours.Rgb(lba1.Palette, Math.Max(0, c)))).ToArray();
        }

        // two tiles, a texel of one or the other as a lumpy noise says (`share` of the first)
        private static (double R, double G, double B)[] Mixed((double R, double G, double B)[] a, (double R, double G, double B)[] b, double share, int seed)
        {
            var size = PolarTextures.TileSize;
            var result = new (double R, double G, double B)[a.Length];
            for (var k = 0; k < a.Length; k++)
                result[k] = Noise.Fbm((k % size) / 6.0, (k / size) / 6.0, 2, 97 + seed) < share ? a[k] : b[k];
            return result;
        }

        // the ski slope: the snow combed, a darker line every four texels down the slope's fall, as a piste basher leaves it
        private (double R, double G, double B)[] Piste(int n)
        {
            var size = PolarTextures.TileSize;
            var snow = Rgb(SnowBricks[n * 2], c => Whiter(c, SnowWhiter + 0.05));
            return snow.Select((c, k) => k % size % 4 == 0 ? (c.R * 0.93, c.G * 0.95, c.B * 0.97) : (Math.Min(255, c.R * 1.02), Math.Min(255, c.G * 1.02), Math.Min(255, c.B * 1.01))).ToArray();
        }

        // a cliff: two bricks' ice-cliff sides one over the other
        private (double R, double G, double B)[] Cliff(int n)
        {
            var top = PolarTextures.Sprite.Decode(lba1.ReadBrick(CliffBricks[2 * n])).Tile(PolarTextures.Face.SideX);
            var under = PolarTextures.Sprite.Decode(lba1.ReadBrick(CliffBricks[2 * n + 1])).Tile(PolarTextures.Face.SideZ);
            return top.Concat(under).Select(c => ((double, double, double))PolarTextures.Colours.Rgb(lba1.Palette, Math.Max(0, c))).ToArray();
        }

        private int[] Quantise((double R, double G, double B)[] tile) => tile.Select(c => SnowColour.Nearest(palette, c.R, c.G, c.B)).ToArray();
    }

    private sealed class Cells(IEnumerable<(int Gx, int Gz)> cells) : IslandRegion
    {
        private readonly List<(int Gx, int Gz)> list = cells.ToList();
        public override IEnumerable<(int Gx, int Gz, double Weight)> Vertices(IslandFile island) => list.Select(c => (c.Gx, c.Gz, 1.0));
        public override (double Gx, double Gz) Center => list.Count == 0 ? (0, 0) : (list.Average(c => c.Gx), list.Average(c => c.Gz));
    }
}
