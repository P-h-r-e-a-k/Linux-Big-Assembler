using System.Buffers.Binary;
using System.IO;
using LBAAssembler.Lba1;

namespace LBAAssembler.Terrain.Polar;

// Polar Island's ground as an LBA2 island (POLAR.ILE, made from MOON.ILE, the old copy of the Emerald Moon that no version of the game
// loads: its file's layout and its cubes' settings, the rest made new). LBA1 draws a scene as bricks in a grid of cells -- 512 world units
// across, 256 a layer, as LBA2's ground cells are -- and an LBA2 island is a height map with textured triangles, plus objects (PolarObjects).
//
// What is ground: each cell's brick says what it is (PolarLayout.Cell.Code): dirt (6x), the small dirt patches (06), the grey patches on it
// (77), water (F1), the crystal (Ax) and the rock of the cliffs' edges and the mountain (00, brown or teal); grey 00 bricks (the concrete
// walls), metal (22: the huts, barrels, pipes), wood (33) and what nothing walks on (F0: posts, fences, gates, the plateau's pillars) are
// objects. A column's ground is its highest ground cell; its top is the height of that cell's top, the water's surface the sea's level
// (LBA2's sea: Y 0). Each corner of the height map is as high as the highest of its four cells, so a ledge keeps its edge and the cell below
// it slopes up to it (LBA2 ground has no walls).
//
// The texture of a cell is its ground brick's top face, unskewed from the sprite's diamond into a square tile of the island's ground atlas,
// in the colours of the island's palette (the nearest of its terrain colours). Water and the sea round the island are cells left undrawn:
// LBA2's own sea shows through them, and they are water to Twinsen (game code 1).
internal static class PolarTerrain
{
    public const string IleFile = "POLAR.ILE", OblFile = "POLAR.OBL", SourceIle = "MOON.ILE", SourceObl = "MOON.OBL";
    // the island's place in the cube map, twice LBA1's size (PolarLayout.Scale): a cell of the layout at (Scale * x + OffsetX, Scale * z +
    // OffsetZ) in the island's cells, so each LBA1 scene's grid (64 x 64 cells, at whole grids from 107's but for 106, 109 and the dock) is
    // two cubes by two or a little over -- 21 cubes have land, the fewest any placement gives -- and the island in cubes (5..9, 4..10), 107's
    // grid in cubes (7..8, 6..7)
    public const int CubeX0 = 5, CubeZ0 = 4, CubesX = 5, CubesZ = 7;
    public const int Scale = PolarLayout.Scale, OffsetX = 7 * 64, OffsetZ = 6 * 64;
    // a cell of the layout's first cell of the island (it has Scale x Scale), and an island cell's of the layout
    public static int IslandX(int x) => x * Scale + OffsetX;
    public static int IslandZ(int z) => z * Scale + OffsetZ;
    public static int LayoutX(int gx) => (int)Math.Floor((gx - OffsetX) / (double)Scale);
    public static int LayoutZ(int gz) => (int)Math.Floor((gz - OffsetZ) / (double)Scale);
    // a layer of the layout's height in world units, on the island
    public static int LayerY(double layer) => (int)Math.Round(layer * 256 * Scale);
    // the size of a tile in the ground atlas (32 x 32 pixels, as the retail islands': LBA1 draws a cell's edge some 27 pixels long)
    public const int Tile = PolarTextures.TileSize;
    // a piece of land no bigger than this, the sea all round it, is a rock in the water: built as an object (PolarObjects), its sides
    // straight up out of the water -- ground would slope down into the sea all round it
    public const int RockCells = 40;
    // the light: two levels over the palette's normal one (XPL ShadeNormalLevel, 12: its colours as they are) on flat ground -- LBA1's
    // bricks have their light drawn in, and at 12 the ground still looked darker than LBA1's -- and the bake's slopes a little lighter or
    // darker round it
    public const int NormalLight = 14;
    public const double SlopeLight = 0.6;

    // Columns: the ground's (the rocks in the water left out); Rocks: the columns of the rocks in the water (the layout's x, z). OffsetX and
    // OffsetZ: the island's cell of the layout's (x, z) is (Scale * x + OffsetX, Scale * z + OffsetZ).
    public sealed record Result(IslandFile Island, int OffsetX, int OffsetZ, Dictionary<(int X, int Z), Column> Columns, HashSet<(int X, int Z)> Rocks,
        PolarTextures.Colours Colours, List<string> Log);

    // Which of two cells meeting at a corner of the height map keeps its height there (the other slopes): the car tracks first, then the
    // rest of the ground people walk on (dirt, the paths), then water, then rock -- the brown rock of the cliffs' rims and foot, the
    // crystal. So a cliff's slope is its rim, not the path at its foot; the water's edge is the rock's, not the water's; a step beside a
    // track is the other side's.
    private static int Priority(Column? c, ISet<int> tracks) => c is not { } k || k.Water ? 2 : IsRock(k.Cell) ? 1 : tracks.Contains(k.Cell.Brick) ? 4 : 3;
    private static bool IsRock(PolarLayout.Cell c) => c.Code == 0x00 || (c.Code & 0xF0) == 0xA0;

    // What a cell's brick is, by its code and colour.
    public static bool IsGround(Lba1Game game, PolarLayout.Cell c)
    {
        switch (c.Code)
        {
            case 0xF1: case 0x66: case 0x06: case 0x77: case 0x88: return true;
            case 0xF0: case 0x22: case 0x33: case 0x11: return false;
        }
        if ((c.Code & 0xF0) == 0xA0 || (c.Code & 0xF0) == 0x60) return true;
        if (c.Code == 0x00)
        {
            // brown rock and teal crystal are ground; grey (the concrete walls, the crates) is not
            var (r, g, b) = PolarLayout.BrickColour(PolarLayoutGame(game), c.Brick);
            var grey = Math.Abs(r - g) < 14 && Math.Abs(g - b) < 14;
            return !grey;
        }
        return false;
    }
    private static Lba1Game PolarLayoutGame(Lba1Game game) => game;

    // A column of the island: its ground cell's layer (-1: none, sea) and that cell.
    public readonly record struct Column(int Top, PolarLayout.Cell Cell)
    {
        public bool Water => Top >= 0 && Cell.Water;
    }

    // (the rocky peak's columns and 111's plateau on it are objects: PolarObjects)
    public static Dictionary<(int X, int Z), Column> Columns(Lba1Game game, PolarLayout layout)
    {
        var columns = new Dictionary<(int, int), Column>();
        foreach (var ((x, y, z), cell) in layout.Cells)
        {
            if (!IsGround(game, cell) || cell.Scene == PolarLayout.PlateauScene || layout.Peak.Contains((x, z))) continue;
            if (!columns.TryGetValue((x, z), out var c) || y > c.Top) columns[(x, z)] = new Column(y, cell);
        }
        return columns;
    }

    // The surface height of a column (world units on the island, the sea at 0), and as LBA1 has it (the layout's units: a layer 256).
    public static int SurfaceOf(Column c) => c.Water ? 0 : Math.Max(0, LayerY(c.Top));
    private static int LayoutSurface(Column c) => c.Water ? 0 : Math.Max(0, c.Top * 256);

    public static Result Build(Lba1Game game, PolarLayout layout, string gameDirectory)
    {
        var log = new List<string>();
        var columns = Columns(game, layout);
        int minX = columns.Keys.Min(k => k.X), maxX = columns.Keys.Max(k => k.X), minZ = columns.Keys.Min(k => k.Z), maxZ = columns.Keys.Max(k => k.Z);
        const int offsetX = OffsetX, offsetZ = OffsetZ;
        if (IslandX(minX) < CubeX0 * 64 || IslandX(maxX + 1) > (CubeX0 + CubesX) * 64 || IslandZ(minZ) < CubeZ0 * 64 || IslandZ(maxZ + 1) > (CubeZ0 + CubesZ) * 64)
            throw new InvalidOperationException($"The island's ground (layout x {minX}..{maxX}, z {minZ}..{maxZ}) is past its cubes.");
        log.Add($"{columns.Count} columns of ground, layout x {minX}..{maxX} z {minZ}..{maxZ}; island cells = layout x {Scale} + ({offsetX}, {offsetZ})");

        // the island file: MOON.ILE's, its map down to the new cubes. The cubes' settings are a sea cube's of the fine-weather Citadel
        // (CITABAU (6, 8)): the moon's have no sea (CubeBitField: the sea is drawn in none of a cube's 16 patches), no sky height and
        // no fog. Every patch has sea; no ground tile is animated (the anim-poly offsets point into the cube's own atlas).
        var source = IslandFile.Load(Path.Combine(gameDirectory, SourceIle));
        var seaIsland = IslandFile.Load(Path.Combine(gameDirectory, "CITABAU.ILE"));
        var template = seaIsland.CubeAt(6, 8) ?? throw new InvalidDataException("CITABAU.ILE has no cube (6, 8)");
        var info = (int[])template.Info.Clone();
        info[IslandCube.InfoAlphaLight] = (info[IslandCube.InfoAlphaLight] & 0xFFFF) | unchecked((int)0xFFFF0000);
        for (var i = 6; i < 10; i++) info[i] = -1;
        var island = IslandFile.Parse(NewFile(source, info, LandTemplate));
        log.Add($"{IleFile}: {CubesX * CubesZ} cubes (cubes {CubeX0}..{CubeX0 + CubesX - 1} x {CubeZ0}..{CubeZ0 + CubesZ - 1}), settings from CITABAU.ILE's sea cube (6, 8), sea in every patch");

        // the rocks in the water: pieces of land (cells touching, corners too) no bigger than RockCells -- objects, not ground
        var rocks = new HashSet<(int X, int Z)>();
        var seen = new HashSet<(int, int)>();
        foreach (var start in columns.Where(c => !c.Value.Water).Select(c => c.Key))
        {
            if (!seen.Add(start)) continue;
            var piece = new List<(int X, int Z)>(); var queue = new Queue<(int X, int Z)>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue(); piece.Add(c);
                for (var dz = -1; dz <= 1; dz++)
                    for (var dx = -1; dx <= 1; dx++)
                        if (columns.TryGetValue((c.X + dx, c.Z + dz), out var n) && !n.Water && seen.Add((c.X + dx, c.Z + dz))) queue.Enqueue((c.X + dx, c.Z + dz));
            }
            if (piece.Count <= RockCells) rocks.UnionWith(piece);
        }
        foreach (var r in rocks) columns.Remove(r);
        log.Add($"{rocks.Count} columns of rock in the water: objects, the sea under them");

        // the car tracks' bricks (dirt whose top is tyre tread: IsTrackBrick)
        var tracks = columns.Values.Where(c => (c.Cell.Code & 0xF0) == 0x60 || c.Cell.Code == 0x06).Select(c => c.Cell.Brick).Distinct()
            .Where(b => IsTrackBrick(PolarTextures.Sprite.Decode(game.ReadBrick(b)), game.Palette)).ToHashSet();

        // the heights, on the layout's grid first: each corner the height of the cells round it that keep theirs (Priority), the highest
        // of them (LBA1's units: a layer 256)
        Column? ColumnAt(int x, int z) => columns.TryGetValue((x, z), out var c) ? c : null;
        var cornerHeights = new Dictionary<(int, int), int>();
        int Corner(int vx, int vz)
        {
            if (cornerHeights.TryGetValue((vx, vz), out var known)) return known;
            var round = new[] { ColumnAt(vx - 1, vz - 1), ColumnAt(vx, vz - 1), ColumnAt(vx - 1, vz), ColumnAt(vx, vz) };
            var first = round.Max(c => Priority(c, tracks));
            return cornerHeights[(vx, vz)] = round.Where(c => Priority(c, tracks) == first).Max(c => c is { } k ? LayoutSurface(k) : 0);
        }
        // ... and the island's: its vertices among the layout's corners, between them as the layout's cell slopes (bilinear), all of it
        // Scale times as high -- so its slopes are LBA1's
        foreach (var (cx, cz, cube) in Cubes(island))
            for (var vz = 0; vz <= 64; vz++)
            for (var vx = 0; vx <= 64; vx++)
            {
                double u = (cx * 64 + vx - offsetX) / (double)Scale, w = (cz * 64 + vz - offsetZ) / (double)Scale;
                int x0 = (int)Math.Floor(u), z0 = (int)Math.Floor(w);
                double fx = u - x0, fz = w - z0;
                var h = (Corner(x0, z0) * (1 - fx) + Corner(x0 + 1, z0) * fx) * (1 - fz) + (Corner(x0, z0 + 1) * (1 - fx) + Corner(x0 + 1, z0 + 1) * fx) * fz;
                cube.Heights[vz * IslandCube.Vertices + vx] = (short)Math.Round(h * Scale);
            }

        // what each cell of the layout is: land (its brick's top); a cliff (land whose corners are two layers and more apart: the side of the
        // column the slope belongs to -- its own, a rim dropping to the path below, or the higher one it climbs to -- two bricks of it); a
        // bank (water that land's edge slopes down into: the land's side); water shut in by land on every corner (LBA1's water, flat at the
        // land's height); or sea (undrawn). Each of the island's Scale x Scale cells of it is what it is.
        var all = new List<(int Gx, int Gz)>();
        foreach (var (cx, cz, _) in Cubes(island))
            for (var z = 0; z < 64; z++) for (var x = 0; x < 64; x++) all.Add((cx * 64 + x, cz * 64 + z));
        IslandGround.OptimiseDiagonals(island, new Cells(all));
        var layoutCells = new List<(int X, int Z)>();
        for (var z = LayoutZ(CubeZ0 * 64); z <= LayoutZ((CubeZ0 + CubesZ) * 64 - 1); z++)
            for (var x = LayoutX(CubeX0 * 64); x <= LayoutX((CubeX0 + CubesX) * 64 - 1); x++) layoutCells.Add((x, z));
        var layoutKinds = new Dictionary<(int X, int Z), (Kind Kind, object Key, int Up)>();
        var waterBrick = columns.Values.Where(c => c.Water).GroupBy(c => c.Cell.Brick).OrderByDescending(g => g.Count()).Select(g => g.Key).DefaultIfEmpty(-1).First();
        // (a column's side: its top brick and, two layers, the one under it)
        object Side((int X, int Z) at, Column c, int layers, int up)
        {
            var below = layers > 1 && layout.Cells.TryGetValue((at.X, c.Top - 1, at.Z), out var b) && IsGround(game, b) ? b.Brick : -1;
            return (c.Cell.Brick, layers > 1 ? below : -2, up < 2 ? 0 : 1);
        }
        foreach (var at in layoutCells)
        {
            var corners = new[] { Corner(at.X, at.Z), Corner(at.X, at.Z + 1), Corner(at.X + 1, at.Z + 1), Corner(at.X + 1, at.Z) };
            var hasOwn = columns.TryGetValue(at, out var own);
            var land = hasOwn && !own.Water;
            var rise = corners.Max() - corners.Min();
            // the way up the cell (0 +x, 1 -x, 2 +z, 3 -z): its corners' slope; the highest of its eight neighbours
            double dx = (corners[2] + corners[3] - corners[0] - corners[1]) / 2.0, dz = (corners[1] + corners[2] - corners[0] - corners[3]) / 2.0;
            var up = Math.Abs(dx) >= Math.Abs(dz) ? (dx >= 0 ? 0 : 1) : (dz >= 0 ? 2 : 3);
            (int X, int Z) highAt = default; Column? high = null;
            for (var nz = -1; nz <= 1; nz++)
                for (var nx = -1; nx <= 1; nx++)
                    if ((nx, nz) != (0, 0) && columns.TryGetValue((at.X + nx, at.Z + nz), out var n) && !n.Water && (high is null || SurfaceOf(n) > SurfaceOf(high.Value)))
                    { high = n; highAt = (at.X + nx, at.Z + nz); }
            var layers = rise >= 512 ? 2 : 1;
            if (land)
            {
                if (rise < 512) layoutKinds[at] = (Kind.Land, own.Cell.Brick, 0);
                // (dropping from its own height: its own side; climbing to a neighbour's: that one's)
                else if (LayoutSurface(own) >= corners.Max() || high is not { } h) layoutKinds[at] = (Kind.Cliff, Side(at, own, layers, up), up);
                else layoutKinds[at] = (Kind.Cliff, Side(highAt, h, layers, up), up);
            }
            else if (corners.Max() > 0 && high is { } h)
            {
                var brick = hasOwn && own.Water ? own.Cell.Brick : waterBrick;
                if (corners.Min() > 0 && brick >= 0) layoutKinds[at] = (Kind.ShutWater, brick, 0);
                else layoutKinds[at] = (Kind.Bank, Side(highAt, h, layers, up), up);
            }
        }
        var kinds = new Dictionary<(int Gx, int Gz), (Kind Kind, object Key, int Up)>();
        foreach (var ((x, z), kind) in layoutKinds)
            for (var j = 0; j < Scale; j++)
                for (var i = 0; i < Scale; i++) kinds[(IslandX(x) + i, IslandZ(z) + j)] = kind;

        // the ground's textures, on as many pages as they take: the land's and the shut-in water's bricks' top faces, and the sides the
        // cliffs and banks show (a layer of a column's side, or two)
        var palette = TerrainPalette(gameDirectory, out var paletteEntry);
        var colours = new PolarTextures.Colours(game.Palette, palette);
        var sprites = new Dictionary<int, PolarTextures.Sprite>();
        PolarTextures.Sprite Sprite(int brick) => sprites.TryGetValue(brick, out var sp) ? sp : sprites[brick] = PolarTextures.Sprite.Decode(game.ReadBrick(brick));
        var wanted = new Dictionary<object, (int[] Tile, int W, int H)>();
        foreach (var (_, key, _) in kinds.Values)
        {
            if (wanted.ContainsKey(key)) continue;
            if (key is int brick) { wanted[key] = (Sprite(brick).Tile(PolarTextures.Face.Top), Tile, Tile); continue; }
            var (b0, b1, axis) = ((int, int, int))key;
            var face = axis == 0 ? PolarTextures.Face.SideX : PolarTextures.Face.SideZ;
            var (w, h) = PolarTextures.Size(face);
            var top = Sprite(b0).Tile(face);
            if (b1 == -2) { wanted[key] = (top, w, h); continue; }
            var under = b1 >= 0 ? Sprite(b1).Tile(face) : top;
            wanted[key] = (top.Concat(under).ToArray(), w, 2 * h);
        }
        var pages = new PolarTextures.Pages(island.GroundTexture, colours, IslandFile.MaxGroundPages);
        pages.Reserve(PolarTextures.Pages.SlotsPerRow - 1);
        foreach (var (key, (tile, w, h)) in wanted.OrderByDescending(k => k.Value.H))
            if (!pages.Add(key, tile, w, h)) throw new InvalidOperationException($"The ground's textures take more than {IslandFile.MaxGroundPages} pages.");
        island.GroundPages.Clear();
        island.GroundPages.AddRange(pages.List.Skip(1));
        log.Add($"the ground's textures: {wanted.Count} faces of bricks ({wanted.Keys.Count(k => k is int)} tops, {wanted.Keys.Count(k => k is not int)} sides) at {Tile} pixels to a cell, on {pages.Count} pages");
        log.Add($"the palette: RESS.HQR entry {paletteEntry}'s (the nearest of its terrain colours)");

        // the cells: every one sea first (a retail sea cell's flags -- bank 6, undrawn, water), then each drawn one its tile -- lit and
        // textured, the template's flags; land with no game code, the rest water to Twinsen. (A texture index: the page in the top bits,
        // the definition in the cube's list in the rest -- IslandFile.GroundTextureOf.)
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
            var (_, w, h) = wanted[key];
            var diagonal = new IslandPolygon(cube.Polygon(lx, lz, 0)).Diagonal;
            for (var half = 0; half < 2; half++)
            {
                var definition = kind is Kind.Cliff or Kind.Bank ? SlopeDefinition(tx, ty, w, h, diagonal, half, up) : IslandGround.TileDefinition(tx, ty, w, h, diagonal, half);
                var index = IslandGround.TextureIndexFor(cube, definition);
                if (index >= island.MaxGroundDefinitions) throw new InvalidOperationException($"Cube {cube.Id} needs more than {island.MaxGroundDefinitions} texture definitions.");
                mostDefinitions = Math.Max(mostDefinitions, index + 1);
                var p = new IslandPolygon(cube.Polygon(lx, lz, half));
                cube.SetPolygon(lx, lz, half, island.WithGroundTexture(p.With(bank: LandTemplate.Bank, texFlag: LandTemplate.TexFlag, polyFlag: LandTemplate.PolyFlag, sampleStep: LandTemplate.SampleStep,
                    codeJeu: kind is Kind.Land or Kind.Cliff ? 0 : 1), page, index).Raw);
            }
        }
        // (the car tracks: ground cells whose top is a track brick -- a fifth of its top face the tracks' dark brown and more; any that
        // isn't flat land is a track the port changed)
        var trackBricks = tracks;
        var trackCells = layoutKinds.Where(k => columns.TryGetValue(k.Key, out var c) && !c.Water && trackBricks.Contains(c.Cell.Brick)).ToList();
        var bentTracks = trackCells.Where(k => k.Value.Kind != Kind.Land || new[] { Corner(k.Key.X, k.Key.Z), Corner(k.Key.X + 1, k.Key.Z), Corner(k.Key.X, k.Key.Z + 1), Corner(k.Key.X + 1, k.Key.Z + 1) }.Distinct().Count() > 1).ToList();
        log.Add($"the car tracks: {trackBricks.Count} track bricks, {trackCells.Count} cells of the layout; {bentTracks.Count} of them not flat land ({string.Join(", ", bentTracks.GroupBy(k => k.Value.Kind).Select(g => $"{g.Count()} {g.Key}"))})" +
            (bentTracks.Count > 0 ? ": " + string.Join(" ", bentTracks.Take(40).Select(k => $"({k.Key.X},{k.Key.Z}){k.Value.Kind}")) : ""));
        TrackCells = trackCells.Select(k => k.Key).ToHashSet();
        var counts = kinds.Values.GroupBy(k => k.Kind).ToDictionary(g => g.Key, g => g.Count());
        int Of(Kind k) => counts.TryGetValue(k, out var n) ? n : 0;
        log.Add($"{Of(Kind.Land)} cells of land, {Of(Kind.Cliff)} of cliff, {Of(Kind.Bank)} of bank, {Of(Kind.ShutWater)} of water shut in by land (the island's: {Scale} x {Scale} to a cell of LBA1's); {all.Count - kinds.Count} of open water and sea (the engine's sea under them); at most {mostDefinitions} texture definitions in a cube");
        // (a slope steeper than LBA1's one-layer steps is a wall to Twinsen: the triangles' own collision flag)
        var walls = IslandGround.SetSteepCollision(island, new Cells(all), SteepestWalk);
        log.Add($"{walls} triangles steeper than {SteepestWalk} degrees are walls");

        // the light: the bake's (the cubes' sun), moved so flat ground is at the palette's normal level and slopes stay near it
        var bake = BakeOptions.For(island.Cubes.Values.First());
        var vertices = new List<(int, int)>();
        foreach (var (cx, cz, _) in Cubes(island))
            for (var z = 0; z <= 64; z++) for (var x = 0; x <= 64; x++) vertices.Add((cx * 64 + x, cz * 64 + z));
        IslandBake.Bake(island, new Cells(vertices), bake);
        var flat = Cubes(island).SelectMany(c => c.Cube.Intensity).GroupBy(v => v & 15).OrderByDescending(g => g.Count()).First().Key;
        foreach (var (_, _, cube) in Cubes(island))
            for (var i = 0; i < cube.Intensity.Length; i++)
            {
                var light = Math.Clamp(NormalLight + (int)Math.Round(((cube.Intensity[i] & 15) - flat) * SlopeLight), 0, 15);
                cube.Intensity[i] = (byte)((cube.Intensity[i] & 0xF0) | light);
            }
        log.Add($"the light: flat ground (the bake's {flat}) at the palette's normal level {NormalLight}, slopes {SlopeLight} of the bake's difference round it");
        return new Result(island, offsetX, offsetZ, columns, rocks, colours, log);
    }

    private enum Kind { Land, Cliff, Bank, ShutWater }

    // The last build's car track cells (the layout's x, z): for the objects' check.
    public static HashSet<(int X, int Z)> TrackCells { get; private set; } = new();

    // A track brick: a fifth of its top face and more the tyre tread's dark brown (r + g + b under 150, red over blue: not the crystal).
    public static bool IsTrackBrick(PolarTextures.Sprite sprite, byte[] palette)
    {
        var tile = sprite.Tile(PolarTextures.Face.Top);
        var dark = tile.Count(c => c >= 0 && PolarTextures.Colours.Rgb(palette, c) is var (r, g, b) && r + g + b < 150 && r > b);
        return dark * 5 >= tile.Length;
    }

    // the steepest ground Twinsen walks up: a slope of one layer over a cell (27 degrees) is a step of LBA1's, two (45) a wall
    public const double SteepestWalk = 40;

    // A side's tile on a slope: its top (v = 0) along the cell's high edge, `up` the way up (0 +x, 1 -x, 2 +z, 3 -z), u along the edge.
    private static ushort[] SlopeDefinition(int x, int y, int width, int height, bool diagonal, int half, int up)
    {
        var straight = IslandGround.TileDefinition(0, 0, 1, 1, diagonal, half);
        var result = new ushort[6];
        for (var i = 0; i < 3; i++)
        {
            // the corner, 0 or 1 along x and z, from the plain definition (u along x, v along z)
            int ox = straight[i * 2] > 128 ? 1 : 0, oz = straight[i * 2 + 1] > 128 ? 1 : 0;
            var (u, v) = up switch { 0 => (oz, 1 - ox), 1 => (1 - oz, ox), 2 => (ox, 1 - oz), _ => (1 - ox, oz) };
            result[i * 2] = (ushort)(u == 0 ? x * 256 + 27 : (x + width) * 256 - 14);
            result[i * 2 + 1] = (ushort)(v == 0 ? y * 256 + 13 : (y + height) * 256 - 14);
        }
        return result;
    }

    public static IEnumerable<(int Cx, int Cz, IslandCube Cube)> Cubes(IslandFile island)
    {
        for (var cz = CubeZ0; cz < CubeZ0 + CubesZ; cz++)
            for (var cx = CubeX0; cx < CubeX0 + CubesX; cx++)
                if (island.CubeAt(cx, cz) is { } cube) yield return (cx, cz, cube);
    }

    // A land cell's flags, as the fine-weather Citadel's grass has them (bank 1, textured and lit, the footstep sound 2).
    private static readonly IslandPolygon LandTemplate = new IslandPolygon(0).With(bank: 1, texFlag: 3, polyFlag: 0, sampleStep: 2);

    // A new island file: the source's map record cleared to the new cubes, its atlases, and for each cube the six records -- its settings
    // (the template cube's), no objects, flat ground of undrawn cells, one texture definition, heights 0 and light.
    private static byte[] NewFile(IslandFile source, int[] infoWords, IslandPolygon land)
    {
        var entries = new List<byte[]>();
        var map = new byte[IslandFile.MapSize * IslandFile.MapSize];
        var id = 0;
        for (var cz = CubeZ0; cz < CubeZ0 + CubesZ; cz++)
            for (var cx = CubeX0; cx < CubeX0 + CubesX; cx++)
                map[cz * IslandFile.MapSize + cx] = (byte)(++id);
        entries.Add(map);
        entries.Add((byte[])source.GroundTexture.Clone());
        entries.Add((byte[])source.ObjectTexture.Clone());
        var info = new byte[IslandCube.InfoSize * 4];
        for (var i = 0; i < IslandCube.InfoSize; i++) BinaryPrimitives.WriteInt32LittleEndian(info.AsSpan(i * 4), infoWords[i]);
        BinaryPrimitives.WriteInt32LittleEndian(info.AsSpan(IslandCube.InfoNbDecors * 4), 0);
        var polygons = new byte[IslandCube.Cells * IslandCube.Cells * 2 * 4];
        var empty = new IslandPolygon(land.Raw).With(texFlag: 0, polyFlag: 0, codeJeu: 0, textureIndex: 0).Raw;
        for (var i = 0; i < IslandCube.Cells * IslandCube.Cells * 2; i++) BinaryPrimitives.WriteUInt32LittleEndian(polygons.AsSpan(i * 4), empty);
        var textures = new byte[12];
        var heights = new byte[IslandCube.Vertices * IslandCube.Vertices * 2];
        var light = Enumerable.Repeat((byte)10, IslandCube.Vertices * IslandCube.Vertices).ToArray();
        for (var c = 0; c < id; c++)
        {
            entries.Add((byte[])info.Clone());
            entries.Add(Array.Empty<byte>());
            entries.Add((byte[])polygons.Clone());
            entries.Add((byte[])textures.Clone());
            entries.Add((byte[])heights.Clone());
            entries.Add((byte[])light.Clone());
        }
        // an HQR of stored entries (an empty one -- the decors of a cube with none -- has its slot, no data)
        var stored = entries.Select(e => e.Length == 0 ? null : HqrWriter.StoredEntry(e)).ToList();
        var tableBytes = (stored.Count + 1) * 4;
        var total = tableBytes + stored.Sum(e => e?.Length ?? 0);
        var result = new byte[total];
        var at = tableBytes;
        for (var i = 0; i < stored.Count; i++)
        {
            if (stored[i] is not { } e) continue;
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4), (uint)at);
            e.CopyTo(result.AsSpan(at));
            at += e.Length;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(stored.Count * 4), (uint)total);
        return result;
    }

    // The island's palette: of the retail islands' (RESS.HQR 27-37, 42), the one whose terrain colours come nearest Polar Island's ground.
    // Polar Island's own entry (39) is filled from it when the island is installed.
    public static readonly int[] PaletteCandidates = { 27, 29, 30, 31, 32, 33, 34, 35, 36, 37, 42 };
    public static int ChosenPalette = 42;
    public static byte[] TerrainPalette(string gameDirectory, out int entry)
    {
        entry = ChosenPalette;
        return IslandMapRenderer.LoadPaletteEntry(gameDirectory, entry);
    }

    // A region of whole cells.
    private sealed class Cells(IEnumerable<(int Gx, int Gz)> cells) : IslandRegion
    {
        private readonly List<(int Gx, int Gz)> list = cells.ToList();
        public override IEnumerable<(int Gx, int Gz, double Weight)> Vertices(IslandFile island) => list.Select(c => (c.Gx, c.Gz, 1.0));
        public override (double Gx, double Gz) Center => list.Count == 0 ? (0, 0) : (list.Average(c => c.Gx), list.Average(c => c.Gz));
    }
}
