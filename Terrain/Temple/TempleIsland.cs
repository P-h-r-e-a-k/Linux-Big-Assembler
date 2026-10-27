using System.IO;
using LBAAssembler.Lba1;
using LBAAssembler.Terrain.Palace;
using LBAAssembler.Terrain.Polar;
using LbaBodyStudio;

namespace LBAAssembler.Terrain.Temple;

// Where the temple's layout lies on the island: a layout cell is Scale x Scale of the island's cells, a layer Scale layers -- everything
// Scale times as big each way, so that from LBA1's angle it looks as LBA1 draws it (three times: the temple's corridors, five to seven
// cells wide, are 15 to 21 for the race's cars, the Emperor's palace's doorways; and its 34 layers, 26,112 high, stay under the 32,767 the
// engine's heights hold). The layout's (0, 0) is island cell (OffsetX, OffsetZ), its lowest layer's foot BaseY over the island's zero.
internal sealed class TempleFrame
{
    public const int Scale = 3;
    // the island's cubes (cubes 2..12 x 3..10: the temple and a cube round it), and the layout's place in them
    public const int CubeX0 = 2, CubeZ0 = 3, CubesX = 11, CubesZ = 8;
    public const int OffsetX = 224, OffsetZ = 256;
    public const int BaseY = 1024;
    public readonly int MinY;
    public TempleFrame(int minY) => MinY = minY;

    // a layout cell's first island cell; the world units of a layout point (cells, layers)
    public int IslandX(int x) => OffsetX + x * Scale;
    public int IslandZ(int z) => OffsetZ + z * Scale;
    public double WorldX(double x) => (OffsetX + x * Scale) * 512.0;
    public double WorldZ(double z) => (OffsetZ + z * Scale) * 512.0;
    public double WorldY(double layer) => BaseY + (layer - MinY) * 256.0 * Scale;
    public double Layer(double worldY) => (worldY - BaseY) / (256.0 * Scale) + MinY;
    // the layout's x (or z) of an island cell, and the first layout column whose cells start in the cube of x's
    public static double LayoutX(double islandCell) => (islandCell - OffsetX) / Scale;
    public static double LayoutZ(double islandCell) => (islandCell - OffsetZ) / Scale;
    public int FirstColumnOfCubeX(int x) => (int)Math.Ceiling((IslandX(x) / 64 * 64 - OffsetX) / (double)Scale);
    public int FirstColumnOfCubeZ(int z) => (int)Math.Ceiling((IslandZ(z) / 64 * 64 - OffsetZ) / (double)Scale);
    // an LBA1 place in a scene of the temple (its world units: a cell 512, a layer 256 -- its cell (X, Z) from X * 512 - 256, LBA1's OBJECT.C)
    // as island world units
    public (double X, double Y, double Z) FromLba1(TempleLayout.Placed scene, int x, int y, int z)
        => (WorldX(scene.X + (x + 256) / 512.0), WorldY(scene.Y + y / 256.0), WorldZ(scene.Z + (z + 256) / 512.0));
    public IEnumerable<(int X, int Z)> Cubes()
    {
        for (var cz = CubeZ0; cz < CubeZ0 + CubesZ; cz++)
            for (var cx = CubeX0; cx < CubeX0 + CubesX; cx++) yield return (cx, cz);
    }
}

// LBA1's Temple of Bú as an LBA2 island of its own (2026-10-16, the user's: "Port the temple of Bu from LBA1 into LBA2 as a separate island.
// Make sure it is visually the same with the same colour palette, which means when viewed from the same angle as the fixed LBA1 angle it
// should match"), into a game folder:
//   - TEMPLE.ILE: its cubes (TempleFrame), their ground undrawn -- the island's sea under the temple, black as LBA1's void (its sky's) -- and
//     the temple's objects (TempleObjects): every cell of its three scenes a box of the brick's own faces, and the boxes it is solid with;
//   - TEMPLE.OBL: their bodies, an empty one for the boxes first;
//   - RESS.HQR: its palette (TemplePalette: Otringal's, LBA1's colours for the temple written in) in entry 40 and its sky in 24 -- RESS_XPL0 +
//     13 and RESS_SKYSEA0 + 13, two slots the retail file leaves empty, which no retail island's number reaches. The race-track mode draws
//     the island as Otringal's (island 4: its scenes are 4's) from the island's file with that palette and sky (the car file's island_file=,
//     island_palette=, island_sky=); the editor's view knows it by its name (RENDERER_API.CPP).
internal static class TempleIsland
{
    public const string IleFile = "TEMPLE.ILE", OblFile = "TEMPLE.OBL";
    // the layout, for the race track's design (tools/RaceTrackPlan/temple_design.py): the frame, and each LBA1 column's cells
    public const string LayoutFile = "TEMPLE.JSON";
    // (the file's layout made from Otringal's -- its atlases are replaced -- and its cubes' settings from Otringal's palace cube's)
    public const string SourceIle = "OTRINGAL.ILE";
    private static readonly (int X, int Z) SettingsCube = (8, 7);
    public const int IslandByte = 4;
    public const int PaletteEntry = 40, SkyEntry = 24;
    // the fog: black, from this far in front of the camera to the far plane, where nothing more is drawn -- inside the 3 x 3 cubes round the
    // camera whose objects the engine draws (a cube's width)
    public const int StartZFog = 12000, ClipZFar = 32000;
    public const int EmptyBody = 0;

    // (Layout: the temple as LBA1 has it -- the layout file's; the objects are built without the cells cut for a race track's road)
    public sealed record Built(TempleLayout Layout, TempleFrame Frame, IslandFile Island, List<byte[]> Bodies, TemplePalette.Result Palette, byte[] Sky, List<string> Log);

    public static bool IsInstalled(string gameDirectory) => File.Exists(Path.Combine(gameDirectory, IleFile));

    // `cuts`: cells left out (TempleLayout's x, layer, z) -- the doorways a race track's road goes through (RaceTrackPlan.TempleCuts); and
    // `floors` and `skirts`, the floors they take laid again and the walls of their trenches (RaceTrackPlan.TempleFloors, TempleSkirts: Carve);
    // `walls`, cells the race fills (RaceTrackPlan.TempleWalls: Carve -- LBA1's actors in them left out, TempleActors);
    // `road`: that race track's plan, whose raised road the models keep out of (TempleDeck: the lobes of the rows of rocks it passes over).
    public static Built Build(string lba1Directory, string gameDirectory, string? sourceDirectory = null, IEnumerable<int[]>? cuts = null,
        IEnumerable<int[]>? floors = null, IEnumerable<int[]>? skirts = null, RaceTrackPlan? road = null, IEnumerable<int[]>? walls = null)
    {
        if (!Lba1Game.IsInstalled(lba1Directory)) throw new InvalidDataException($"No LBA1 game in {lba1Directory}.");
        sourceDirectory ??= gameDirectory;
        var log = new List<string>();
        var game = new Lba1Game(lba1Directory);
        var layout = TempleLayout.Build(game);
        log.AddRange(layout.Log);
        var frame = new TempleFrame(layout.MinY);
        if (frame.IslandX(layout.MinX) < TempleFrame.CubeX0 * 64 || frame.IslandX(layout.MaxX + 1) > (TempleFrame.CubeX0 + TempleFrame.CubesX) * 64
            || frame.IslandZ(layout.MinZ) < TempleFrame.CubeZ0 * 64 || frame.IslandZ(layout.MaxZ + 1) > (TempleFrame.CubeZ0 + TempleFrame.CubesZ) * 64)
            throw new InvalidOperationException("The temple is past the island's cubes.");
        if (frame.WorldY(layout.MaxY + 1) > 32000) throw new InvalidOperationException($"The temple's top ({frame.WorldY(layout.MaxY + 1):0}) is higher than the engine's heights hold.");

        // ---- the island: its cubes, every cell undrawn sea (the engine's sea under them), Otringal's palace cube's settings with the sea in
        // every patch, the black fog, and no animated ground
        var source = IslandFile.Load(Path.Combine(sourceDirectory, SourceIle));
        var cubes = frame.Cubes().ToList();
        var island = IslandFile.Parse(IslandScaler.NewFile(source, cubes), Path.Combine(gameDirectory, IleFile));
        var settings = source.CubeAt(SettingsCube.X, SettingsCube.Z)?.Info ?? throw new InvalidDataException($"{SourceIle} has no cube {SettingsCube}.");
        var sea = new IslandPolygon(0).With(bank: 6, texFlag: 0, polyFlag: 0, sampleStep: 0, codeJeu: 1, textureIndex: 0).Raw;
        foreach (var (cx, cz) in cubes)
        {
            var cube = island.CubeAt(cx, cz)!;
            var info = (int[])settings.Clone();
            info[IslandCube.InfoAlphaLight] = (info[IslandCube.InfoAlphaLight] & 0xFFFF) | unchecked((int)0xFFFF0000);
            info[IslandCube.InfoStartZFog] = StartZFog; info[IslandCube.InfoClipZFar] = ClipZFar;
            for (var i = 6; i < IslandCube.InfoSize; i++) info[i] = -1;
            cube.Info = info;
            for (var i = 0; i < cube.Polygons.Length; i++) cube.Polygons[i] = sea;
            Array.Fill(cube.Intensity, (byte)12);
        }
        log.Add($"{IleFile}: {cubes.Count} cubes ({TempleFrame.CubeX0}..{TempleFrame.CubeX0 + TempleFrame.CubesX - 1} x {TempleFrame.CubeZ0}..{TempleFrame.CubeZ0 + TempleFrame.CubesZ - 1}), " +
                $"the temple {TempleFrame.Scale} times LBA1's size from cell ({TempleFrame.OffsetX}, {TempleFrame.OffsetZ}), its foot at {TempleFrame.BaseY}, its top at {frame.WorldY(layout.MaxY + 1):0}; " +
                $"black fog from {StartZFog} to {ClipZFar}");

        // ---- the palette, the sky
        var ress = HqrArchive.Open(Path.Combine(gameDirectory, "RESS.HQR"));
        var palette = TemplePalette.Build(ress.Read(TemplePalette.Source), game.Palette, TempleObjects.ColourCounts(game, layout));
        log.AddRange(palette.Log);
        var sky = TemplePalette.Sky(palette.Palette);

        // ---- the objects
        var bodies = new List<byte[]> { RaceTrackPipes.Write(new List<System.Numerics.Vector3> { System.Numerics.Vector3.Zero }, new List<Face>(), lit: false) };
        var built = layout;
        // (the round things -- barrels, columns, amphorae, goblets, fire bowls -- turned, not boxes: TempleShapes; the shaped things -- rocks,
        // grilles, spiked rails, iron fans, statues, stairs, a bed of spikes -- models: TempleModels)
        var shapes = TempleShapes.Find(game, log);
        var models = TempleModels.Find(game, log);
        if (cuts is not null)
        {
            var carved = Carve(layout, shapes, models, cuts, floors, skirts, walls);
            built = carved.Built;
            log.AddRange(carved.Log);
        }
        TempleModels.DeckCap? deckCap = null;
        if (cuts is not null && TempleDeck.Of(road) is { } deck)
            deckCap = (lo, hi) => deck.Cap(TempleFrame.OffsetX + lo.X * TempleFrame.Scale, TempleFrame.OffsetX + hi.X * TempleFrame.Scale,
                TempleFrame.OffsetZ + lo.Z * TempleFrame.Scale, TempleFrame.OffsetZ + hi.Z * TempleFrame.Scale, frame.WorldY(lo.Y), frame.WorldY(hi.Y)) is { } y ? frame.Layer(y) : null;
        var objects = TempleObjects.Build(game, built, island, frame, palette.Map, bodies.Count, EmptyBody, shapes, models, uncut: layout, deck: deckCap);
        bodies.AddRange(objects.Bodies);
        log.AddRange(objects.Log);
        // (LBA1's actors as its scenes start them -- doors, lifts, floor plates, the statues, the plants, the skeletons...: TempleActors; in a
        // race track's build those on or over its road left out)
        var actors = TempleActors.Place(lba1Directory, game, layout, island, frame, palette.Map, bodies.Count, race: cuts is not null, walls: walls);
        bodies.AddRange(actors.Bodies);
        log.AddRange(actors.Log);
        log.Add($"{OblFile}: {bodies.Count} bodies (an empty one for the boxes, then the temple's)");
        return new Built(layout, frame, island, bodies, palette, sky, log);
    }

    // What a race track's road leaves of the temple (Build, and ScriptRoundTrip templecells): the layout less
    //   - the cells its doorways cut (`cuts`: the plan's TempleCuts);
    //   - what those leave of the round things they cut through, hanging over the road or standing on nothing; and of the shaped things,
    //     theirs and what stood on the round things' -- a statue whole or not at all, a grille or a row of rocks but the columns along it
    //     they cut (Remains: TempleShapes and TempleModels' CutRemains);
    //   - the pieces the cuts split off from the rest and left over the road's room -- a cell of a wall's top, a stub of lintel (Loose: no
    //     more than LooseMost cells, each of them higher than a floor may stand in its column);
    // with the floors they took laid again (`floors`: the plan's TempleFloors, each column the road comes within reach of, [x, z, the
    // highest a cell may stand in it without reaching into the road's room, the layer to lay a floor at, the layer of the floor round it],
    // the last two left out where no floor is round it): where
    // a column the cuts (or what went with them) took cells of has nothing from that layer up to that height -- a doorway through a wall
    // LBA1 stood on no floor, a statue or a row of rocks taken down to its foot, a ramp's trench down through a floor -- a cell there (Fills),
    // the brick the floor round it has most (a plain box: no round or shaped thing's); and where a trench's floor is laid lower than a floor
    // kept beside it -- a step of the crypt's gallery, the cornice of a wall whose foot the cuts took, a ledge -- the dark under that floor
    // filled down to it (`skirts`: the plan's TempleSkirts, [x, z, from layer, to layer]: Skirts), with the bricks of the wall the cuts took
    // beside it at each layer, else of the kept cell over it. And `walls` (the plan's TempleWalls, [x, z, from layer, to layer, the column
    // whose bricks they take x, z]): cells filled where LBA1 has none -- the hidden passage's door walled up, the Temple of Bú's secret
    // shortcut -- each with the brick that column has at its layer.
    public sealed record Carved(TempleLayout Built, HashSet<(int X, int Y, int Z)> Cut, HashSet<(int X, int Y, int Z)> Remains,
        HashSet<(int X, int Y, int Z)> Loose, Dictionary<(int X, int Y, int Z), PolarLayout.Cell> Fills,
        Dictionary<(int X, int Y, int Z), PolarLayout.Cell> Skirts, List<string> Log);
    public const int LooseMost = 12;

    public static Carved Carve(TempleLayout layout, Dictionary<int, TempleShapes.Part> shapes, Dictionary<int, TempleModels.Part> models,
        IEnumerable<int[]> cuts, IEnumerable<int[]>? floors, IEnumerable<int[]>? skirts = null, IEnumerable<int[]>? walls = null)
    {
        var log = new List<string>();
        var cut = cuts.Where(c => c.Length == 3).Select(c => (X: c[0], Y: c[1], Z: c[2])).ToHashSet();
        var built = layout.Without(cut);
        log.Add($"{layout.Cells.Count - built.Cells.Count} cells cut where the race track's road goes through the walls");
        var remains = TempleShapes.CutRemains(shapes, layout.Cells, cut);
        if (remains.Count > 0)
        {
            built = built.Without(remains);
            log.Add($"{remains.Count} cells of round things left hanging by the cuts taken out with them");
        }
        var modelRemains = TempleModels.CutRemains(models, layout.Cells, cut.Concat(remains).ToHashSet());
        if (modelRemains.Count > 0)
        {
            built = built.Without(modelRemains);
            log.Add($"{modelRemains.Count} cells of shaped things the cuts took part of taken out with them");
        }
        remains.UnionWith(modelRemains);
        var floorOf = new Dictionary<(int X, int Z), (int Highest, int? Lay, int? Floor)>();
        foreach (var f in floors ?? Array.Empty<int[]>())
            if (f.Length >= 3) floorOf[(f[0], f[1])] = (f[2], f.Length >= 5 ? f[3] : null, f.Length >= 5 ? f[4] : null);

        // ---- the pieces split off over the road: the built cells' parts (six neighbours), the small ones next to a cell taken, each of whose
        // cells is over the road's room
        var gone = cut.Where(layout.Cells.ContainsKey).Concat(remains).ToHashSet();
        var loose = new HashSet<(int X, int Y, int Z)>();
        var seen = new HashSet<(int X, int Y, int Z)>();
        var queue = new Queue<(int X, int Y, int Z)>();
        foreach (var start in built.Cells.Keys)
        {
            if (!seen.Add(start)) continue;
            var part = new List<(int X, int Y, int Z)>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                part.Add(c);
                foreach (var n in Six(c))
                    if (built.Cells.ContainsKey(n) && seen.Add(n)) queue.Enqueue(n);
            }
            if (part.Count > LooseMost || !part.Any(c => Six(c).Any(gone.Contains))) continue;
            if (part.All(c => floorOf.TryGetValue((c.X, c.Z), out var f) && c.Y > f.Highest)) loose.UnionWith(part);
        }
        if (loose.Count > 0)
        {
            built = built.Without(loose);
            log.Add($"{loose.Count} cells the cuts split off over the road taken out with them");
        }

        // ---- the floors laid again
        var lost = gone.Concat(loose).Select(c => (c.X, c.Z)).ToHashSet();
        var fills = new Dictionary<(int X, int Y, int Z), PolarLayout.Cell>();
        bool Plain(PolarLayout.Cell c) => c.Shape == 1 && !shapes.ContainsKey(c.Brick) && !models.ContainsKey(c.Brick);
        foreach (var ((x, z), (highest, layAt, floorAt)) in floorOf.OrderBy(f => f.Key.Z).ThenBy(f => f.Key.X))
        {
            if (layAt is not { } lay || floorAt is not { } floor || !lost.Contains((x, z))) continue;
            var held = false;
            for (var y = lay; y <= highest && !held; y++) held = built.Cells.ContainsKey((x, y, z));
            if (held) continue;
            // (the floor round it, a ring further out at a time: a row of rocks or a grille may stand all round it)
            var around = new List<PolarLayout.Cell>();
            for (var r = 1; r <= 3 && around.Count == 0; r++)
                foreach (var layer in new[] { floor, lay })
                {
                    for (var dz = -r; dz <= r; dz++)
                        for (var dx = -r; dx <= r; dx++)
                            if (Math.Max(Math.Abs(dx), Math.Abs(dz)) == r && built.Cells.TryGetValue((x + dx, layer, z + dz), out var n) && Plain(n)) around.Add(n);
                    if (around.Count > 0) break;
                }
            if (around.Count == 0) continue;
            fills[(x, lay, z)] = around.GroupBy(c => c).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Brick).ThenBy(g => g.Key.Scene).First().Key;
        }
        foreach (var (at, cell) in fills) built.Cells[at] = cell;
        if (fills.Count > 0) log.Add($"{fills.Count} floor cells laid again where the cuts left pits beside or under the road");

        // ---- the trenches' walls
        var skirted = new Dictionary<(int X, int Y, int Z), PolarLayout.Cell>();
        foreach (var s in (skirts ?? Array.Empty<int[]>()).Where(s => s.Length == 4))
        {
            int x = s[0], z = s[1];
            for (var y = s[2]; y <= s[3]; y++)
            {
                if (built.Cells.ContainsKey((x, y, z))) continue;
                PolarLayout.Cell? brick = null;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (brick is null && layout.Cells.TryGetValue((x + dx, y, z + dz), out var was) && !built.Cells.ContainsKey((x + dx, y, z + dz)) && Plain(was)) brick = was;
                for (var k = s[3] + 1; k <= s[3] + 8 && brick is null; k++)
                    if (built.Cells.TryGetValue((x, k, z), out var over) && Plain(over)) brick = over;
                if (brick is { } b) skirted[(x, y, z)] = b;
            }
        }
        foreach (var (at, cell) in skirted) built.Cells[at] = cell;
        if (skirted.Count > 0) log.Add($"{skirted.Count} cells of wall under the floors beside the trenches the cuts left");

        // ---- the race's own walls
        var walled = 0;
        foreach (var w in (walls ?? Array.Empty<int[]>()).Where(w => w.Length >= 6))
            for (var y = w[2]; y <= w[3]; y++)
                if (!built.Cells.ContainsKey((w[0], y, w[1])) && layout.Cells.TryGetValue((w[4], y, w[5]), out var brick))
                {
                    built.Cells[(w[0], y, w[1])] = brick;
                    fills[(w[0], y, w[1])] = brick;
                    walled++;
                }
        if (walled > 0) log.Add($"{walled} cells of wall the race fills (the hidden passage's door walled up)");
        return new Carved(built, cut, remains, loose, fills, skirted, log);
    }

    private static IEnumerable<(int X, int Y, int Z)> Six((int X, int Y, int Z) c)
    {
        yield return (c.X + 1, c.Y, c.Z); yield return (c.X - 1, c.Y, c.Z);
        yield return (c.X, c.Y + 1, c.Z); yield return (c.X, c.Y - 1, c.Z);
        yield return (c.X, c.Y, c.Z + 1); yield return (c.X, c.Y, c.Z - 1);
    }

    // Writes a built island into a game folder: its files, and its palette and sky into RESS.HQR. Returns lines for the log.
    public static List<string> Install(string gameDirectory, Built built)
    {
        var log = new List<string>();
        Writable(Path.Combine(gameDirectory, "RESS.HQR"));
        // (a build's own file replaced: no .bak of it left beside it)
        var ilePath = Path.Combine(gameDirectory, IleFile);
        if (File.Exists(ilePath)) { Writable(ilePath); File.Delete(ilePath); }
        built.Island.Save(ilePath);
        if (File.Exists(ilePath + ".bak")) File.Delete(ilePath + ".bak");
        File.WriteAllBytes(Path.Combine(gameDirectory, OblFile), PolarIsland.Hqr(built.Bodies));
        File.WriteAllText(Path.Combine(gameDirectory, LayoutFile), LayoutJson(built));
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var bytes = File.ReadAllBytes(ressPath);
        bytes = Set(bytes, PaletteEntry, built.Palette.Xpl);
        bytes = Set(bytes, SkyEntry, built.Sky);
        File.WriteAllBytes(ressPath, bytes);
        log.Add($"{IleFile} and {OblFile} written; RESS.HQR: the temple's palette (entry {PaletteEntry}) and its night sky (entry {SkyEntry})");
        return log;
    }

    // The layout as the race track's design reads it: the frame (a layout cell's island cell is offset + scale * its own; a layer's foot
    // baseY + (layer - minY) * layer), the scenes' places, and each column's cells -- a bit for each layer from minY up -- with its top
    // brick's LBA1 code.
    private static string LayoutJson(Built built)
    {
        var columns = new Dictionary<(int X, int Z), (long Bits, int Top, int Code)>();
        foreach (var ((x, y, z), cell) in built.Layout.Cells)
        {
            columns.TryGetValue((x, z), out var c);
            var top = c.Bits == 0 || y > c.Top;
            columns[(x, z)] = (c.Bits | 1L << (y - built.Layout.MinY), top ? y : c.Top, top ? cell.Code : c.Code);
        }
        var layout = new Dictionary<string, object>
        {
            ["scale"] = TempleFrame.Scale, ["offsetX"] = TempleFrame.OffsetX, ["offsetZ"] = TempleFrame.OffsetZ, ["baseY"] = TempleFrame.BaseY,
            ["minY"] = built.Layout.MinY, ["maxY"] = built.Layout.MaxY, ["layer"] = 256 * TempleFrame.Scale,
            ["width"] = built.Layout.MaxX + 1, ["depth"] = built.Layout.MaxZ + 1,
            ["cubes"] = new[] { TempleFrame.CubeX0, TempleFrame.CubeZ0, TempleFrame.CubesX, TempleFrame.CubesZ },
            ["scenes"] = built.Layout.Placements.Select(p => new[] { p.Scene, p.X, p.Y, p.Z }).ToList(),
            ["columns"] = columns.OrderBy(c => c.Key.Z).ThenBy(c => c.Key.X).Select(c => new object[] { c.Key.X, c.Key.Z, c.Value.Bits, c.Value.Top, c.Value.Code }).ToList(),
        };
        return System.Text.Json.JsonSerializer.Serialize(layout);
    }

    public static List<string> Add(string lba1Directory, string gameDirectory)
    {
        var built = Build(lba1Directory, gameDirectory);
        return built.Log.Concat(Install(gameDirectory, built)).ToList();
    }

    // Takes the island out of a game folder: its files, and RESS.HQR's two entries emptied (the retail file has none there).
    public static bool Remove(string gameDirectory)
    {
        var gone = false;
        foreach (var f in new[] { IleFile, OblFile, IleFile + ".bak", LayoutFile })
        {
            var path = Path.Combine(gameDirectory, f);
            if (File.Exists(path)) { Writable(path); File.Delete(path); gone = true; }
        }
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        if (File.Exists(ressPath))
        {
            var ress = HqrFile.Parse(File.ReadAllBytes(ressPath));
            var changed = false;
            foreach (var entry in new[] { PaletteEntry, SkyEntry })
                if (entry < ress.Count && !ress.IsEmpty(entry)) { ress.Clear(entry); changed = true; }
            if (changed) { Writable(ressPath); File.WriteAllBytes(ressPath, ress.ToBytes()); gone = true; }
        }
        return gone;
    }

    private static void Writable(string path)
    {
        var info = new FileInfo(path);
        if (info.Exists && info.IsReadOnly) info.IsReadOnly = false;
    }

    // An HQR entry set: an empty retail slot is given data, one with data replaced.
    private static byte[] Set(byte[] hqr, int slot, byte[] data)
    {
        try { return HqrWriter.FillEntry(hqr, slot, HqrWriter.StoredEntry(data)); }
        catch (InvalidDataException) { return HqrWriter.ReplaceEntry(hqr, slot, HqrWriter.StoredEntry(data)); }
    }
}
