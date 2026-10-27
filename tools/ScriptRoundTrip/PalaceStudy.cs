using System.Numerics;
using LBAAssembler;
using LBAAssembler.LbaScript;
using LBAAssembler.Scenes;
using LBAAssembler.Terrain;
using LbaBodyStudio;

namespace ScriptRoundTrip;

// Studies for the Palace island's outside and furniture (Terrain/Palace) -- read only:
//   palaceactors <game folder> [scene ...]: each palace room's actors (entity, BODY.HQR body and its size, sprite, flags, place, scripts)
//   palacecells <game folder> [--walls]: each room's cells as the Palace island places them and its roof layer; the outer walls' columns
//   palaceblocks <game folder> <out folder>: the library blocks the palace grids are made of, how often placed where, a picture of each
//   palacefloor <game folder> <out.png>: the rooms' floor bricks from above, in their average colours, and the floor's colours
//   palaceportraits <game folder> <out.png> [scale]: the pictures of the game's characters as the island's object pages have them
//   palacepictures <ile> <obl>: where a built island's pictures hang -- who, the middle (cells), the way it looks: "name;x;z;nx;nz"
//   palacezones <game folder> [--inner]: the palace rooms' ways out and other scenes' ways in
//   gridcolumn <game folder> <scene> <x> <z> [<x1> <z1>]: what a grid has in a column, layer by layer (block.place)
//   blockface <game folder> <scene> <block> <out.png> [dx,dy]: a block's picture and its +x and +z faces looked up through it
//   islandmesh <ile> <obl> <ress palette entry> <out.bin> [x0 z0 x1 z1] [--no-terrain] [--no-decors] [--boxes] [--caps y] [--texels]: the
//     island's ground, decor bodies (and collision boxes, or their cross-sections at height y) as coloured triangles (world units; 13 float32
//     each: three corners, r g b, kind 0 decor 1 ground 2 box 3 cut box); --texels: textured decor faces in pieces, their textures' colours
//   meshshot <mesh.bin> <out.png> [...]: such a mesh drawn with a depth buffer (below)
//   oblcheck <obl> [first]: the bodies past the engine's 550 points or primitives
//   decorcount <ile> [obl]: each cube's decors (the engine's 200), and with the OBL whether their bodies are there
//   palacefurniture <game folder>: the furniture PalaceFurniture finds, room by room
internal static class PalaceStudy
{
    public static int Actors(string[] args)
    {
        var game = args[1];
        var only = args.Skip(2).Select(int.Parse).ToHashSet();
        var interiors = new Lba2Interiors(game);
        var area = Lba2Areas.Find(interiors.LoadScene).First(a => a.Tiles.Any(t => t.Scene == 151));
        var archive = HqrArchive.Open(Path.Combine(game, "SCENE.HQR"));
        foreach (var tile in area.Tiles)
        {
            if (only.Count > 0 && !only.Contains(tile.Scene)) continue;
            var sep = Lba2Areas.Separations.Where(s => s.Scene == tile.Scene).Select(s => (s.Dx, s.Dy, s.Dz)).FirstOrDefault();
            var scene = interiors.LoadScene(tile.Scene)!;
            var cells = interiors.Placements(tile);
            Console.WriteLine($"== scene {tile.Scene}: offset cells ({tile.OffsetX / 512 - sep.Dx}, {tile.OffsetY / 256 - sep.Dy}, {tile.OffsetZ / 512 - sep.Dz}), " +
                              $"cells x {cells.Min(c => c.X)}..{cells.Max(c => c.X)} y {cells.Min(c => c.Y)}..{cells.Max(c => c.Y)} z {cells.Min(c => c.Z)}..{cells.Max(c => c.Z)}, " +
                              $"window {tile.Window}, without {tile.Without}, {scene.Actors.Count - 1} actors");
            var rec = SceneRecord.Parse(archive.Read(tile.Scene + 1));
            var syms = new SceneSymbols(rec);
            for (var i = 1; i < scene.Actors.Count; i++)
            {
                var a = scene.Actors[i];
                string what;
                if (a.IsSprite) what = $"sprite {a.Sprite}";
                else if (interiors.BodyIndex(a.Entity, a.Body) is { } bi && interiors.ReadBody(bi) is { } data)
                {
                    try
                    {
                        var b = Body.Read(data, 2, allowStatic: true);
                        var w = b.World();
                        Vector3 lo = w.Aggregate(new Vector3(float.MaxValue), Vector3.Min), hi = w.Aggregate(new Vector3(float.MinValue), Vector3.Max);
                        what = $"entity {a.Entity} body {a.Body} = BODY {bi}: {b.Vertices.Count} pts {b.Faces.Count} faces ({b.Faces.Count(f => f.Texture != null)} textured) {b.Lines.Count} lines {b.Spheres.Count} spheres {b.Bones.Count} bones, " +
                               $"x {lo.X:0}..{hi.X:0} y {lo.Y:0}..{hi.Y:0} z {lo.Z:0}..{hi.Z:0}";
                    }
                    catch (Exception e) { what = $"entity {a.Entity} body {a.Body} = BODY {bi}: {e.Message}"; }
                }
                else what = $"entity {a.Entity} body {a.Body} (none)";
                Console.WriteLine($"  actor {i}: {what}; at ({a.X},{a.Y},{a.Z}) beta {a.Beta} flags 0x{a.Flags:X} move {a.Move} anim {a.Anim} life {a.Life.Length} track {a.Track.Length}");
                if (args.Contains("--scripts") || true)
                {
                    var ra = rec.Actors[i];
                    var life = LifeText.Decompile(rec.Life(ra).ToArray(), i, syms);
                    var track = TrackText.Decompile(rec.Track(ra).ToArray());
                    foreach (var line in life.Split('\n').Where(l => l.Trim().Length > 0).Take(14)) Console.WriteLine("      L " + line.TrimEnd());
                    foreach (var line in track.Split('\n').Where(l => l.Trim().Length > 0).Take(8)) Console.WriteLine("      T " + line.TrimEnd());
                }
            }
        }
        return 0;
    }

    // palacecells <game folder>: each room's cells as the Palace island places them, its roof layer (PalaceIsland's rule), and per layer how
    // many cells are filled; with --blocks the grid's library blocks of each room (block number, extent, cells)
    public static int Cells(string[] args)
    {
        var interiors = new Lba2Interiors(args[1]);
        var area = Lba2Areas.Find(interiors.LoadScene).First(a => a.Tiles.Any(t => t.Scene == 151));
        var cells = new Dictionary<(int X, int Y, int Z), (int Brick, int Tile)>();
        for (var ti = 0; ti < area.Tiles.Count; ti++)
        {
            var tile = area.Tiles[ti];
            var sep = Lba2Areas.Separations.Where(s => s.Scene == tile.Scene).Select(s => (s.Dx, s.Dy, s.Dz)).FirstOrDefault();
            int ox = tile.OffsetX / 512 - sep.Dx, oy = tile.OffsetY / 256 - sep.Dy, oz = tile.OffsetZ / 512 - sep.Dz;
            foreach (var p in interiors.Placements(tile)) cells.TryAdd((p.X + ox, p.Y + oy, p.Z + oz), (p.Brick, ti));
        }
        if (args.Contains("--walls"))
        {
            // (the outer walls' columns, side by side: how many layers each has, '.' none -- the gaps in the building's outside)
            int minX = cells.Keys.Min(c => c.X), maxX = cells.Keys.Max(c => c.X), minZ = cells.Keys.Min(c => c.Z), maxZ = cells.Keys.Max(c => c.Z);
            string Column(int x, int z) { var n = cells.Keys.Count(c => c.X == x && c.Z == z && c.Y >= 1 && c.Y <= 12); return n == 0 ? "." : n >= 12 ? "#" : (n % 10).ToString(); }
            for (var x = minX; x <= minX + 3; x++) Console.WriteLine($"x {x}: " + string.Concat(Enumerable.Range(minZ, maxZ - minZ + 1).Select(z => Column(x, z))));
            for (var z = 44; z <= 47; z++) Console.WriteLine($"z {z}: " + string.Concat(Enumerable.Range(minX, maxX - minX + 1).Select(x => Column(x, z))));
            for (var z = maxZ - 3; z <= maxZ; z++) Console.WriteLine($"z {z}: " + string.Concat(Enumerable.Range(minX, maxX - minX + 1).Select(x => Column(x, z))));
            for (var x = maxX - 3; x <= maxX; x++) Console.WriteLine($"x {x}: " + string.Concat(Enumerable.Range(minZ, maxZ - minZ + 1).Select(z => Column(x, z))));
            for (var x = 16; x <= 19; x++) Console.WriteLine($"x {x}: " + string.Concat(Enumerable.Range(minZ, maxZ - minZ + 1).Select(z => Column(x, z))));
        }
        for (var ti = 0; ti < area.Tiles.Count; ti++)
        {
            var mine = cells.Where(c => c.Value.Tile == ti).Select(c => c.Key).ToList();
            int rx0 = mine.Min(c => c.X), rx1 = mine.Max(c => c.X), rz0 = mine.Min(c => c.Z), rz1 = mine.Max(c => c.Z);
            var edge = mine.Where(c => c.X == rx0 || c.X == rx1 || c.Z == rz0 || c.Z == rz1).GroupBy(c => (c.X, c.Z)).Select(g => g.Max(c => c.Y)).OrderBy(h => h).ToList();
            var top = edge.Count > 0 ? edge[(int)(edge.Count * 0.8)] + 1 : mine.Max(c => c.Y) + 1;
            var layers = string.Join(" ", Enumerable.Range(0, 15).Select(y => mine.Count(c => c.Y == y)));
            Console.WriteLine($"room {area.Tiles[ti].Scene}: x {rx0}..{rx1} z {rz0}..{rz1}, {mine.Count} cells, roof at layer {top} (max {mine.Max(c => c.Y)}); per layer: {layers}");
        }
        return 0;
    }

    // palaceportraits <game folder> <out.png> [scale]: the palace's pictures of the game's characters (PalacePortraits) as the island's
    // object pages have them, in Otringal's palette, `scale` (3) times as big
    public static int Portraits(string[] args)
    {
        var palette = IslandMapRenderer.LoadPaletteEntry(args[1], LBAAssembler.Terrain.Palace.PalaceIsland.PaletteEntry);
        var cache = new Dictionary<(float, float, float), int>();
        var painted = LBAAssembler.Terrain.Palace.PalacePortraits.PaintAll(args[1], c => LBAAssembler.Terrain.Palace.BrickBuilding.Nearest(palette, c, cache));
        foreach (var line in painted.Log) Console.WriteLine(line);
        var scale = args.Length > 3 ? int.Parse(args[3]) : 3;
        int w = painted.Pages.Count * 256 * scale + (painted.Pages.Count - 1) * 8, h = 256 * scale;
        var bgra = new byte[w * h * 4];
        for (var p = 0; p < painted.Pages.Count; p++)
            for (var y = 0; y < h; y++)
                for (var x = 0; x < 256 * scale; x++)
                {
                    var (r, g, b) = LBAAssembler.Terrain.Palace.BrickBuilding.Colour(palette, painted.Pages[p][y / scale * 256 + x / scale]);
                    var at = (y * w + p * (256 * scale + 8) + x) * 4;
                    bgra[at] = b; bgra[at + 1] = g; bgra[at + 2] = r; bgra[at + 3] = 255;
                }
        PngWriter.Write(args[2], bgra, w, h);
        Console.WriteLine($"{args[2]}: {painted.Pages.Count} pages");
        return 0;
    }

    // palacepictures <ile> <obl>: the pictures a built Palace island has hung (PalacePortraits.Hang: a body of one textured polygon each,
    // its texture entry 0x7F7F0000 | the picture's corner on its page) -- the sitter from the corner and the page (four to a page, the
    // lowest page the first's), each once (not its copies in the cubes round it): name, middle x and z in cells, the way it looks
    public static int Pictures(string[] args)
    {
        var island = IslandFile.Load(args[1]);
        var obl = HqrArchive.Open(args[2]);
        var bodies = new Dictionary<int, Body?>();
        var found = new Dictionary<(int X, int Y, int Z), (int Page, int Offset, Vector3 Normal)>();
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
            foreach (var d in cube.Decors)
            {
                var index = d.Body & ((1 << IslandFile.DecorPageShift) - 1);
                if (!bodies.TryGetValue(index, out var body))
                {
                    try { body = Body.Read(obl.Read(index), 2, allowStatic: true); } catch (Exception) { body = null; }
                    bodies[index] = body;
                }
                if (body is null || body.Faces.Count != 1 || body.Faces[0].Texture is null || body.Textures.Length != 1 || (body.Textures[0] & 0xFFFF0000) != 0x7F7F0000) continue;
                var f = body.Faces[0];
                var turn = Matrix4x4.CreateRotationY((float)((d.Beta & 0xFFFF) * 2 * Math.PI / 4096));
                var normal = Vector3.Normalize(Vector3.TransformNormal(Vector3.Cross(body.Vertices[f.Points[1]] - body.Vertices[f.Points[0]], body.Vertices[f.Points[2]] - body.Vertices[f.Points[0]]), turn));
                found.TryAdd((cx * 32768 + d.X, d.Y, cz * 32768 + d.Z), (IslandFile.ObjectPageOf(d), (int)(body.Textures[0] & 0xFFFF), normal));
            }
        if (found.Count == 0) { Console.WriteLine("no pictures"); return 1; }
        var first = found.Values.Min(v => v.Page);
        var sitters = LBAAssembler.Terrain.Palace.PalacePortraits.Sitters;
        foreach (var ((x, _, z), (page, offset, normal)) in found.OrderBy(p => p.Key.X).ThenBy(p => p.Key.Z))
        {
            var k = (page - first) * 4 + offset / 256 / 120 * 2 + offset % 256 / 96;
            Console.WriteLine($"{(k < sitters.Length ? sitters[k].Name : "?" + k)};{x / 512.0:0.##};{z / 512.0:0.##};{Math.Round(normal.X)};{Math.Round(normal.Z)}");
        }
        return 0;
    }

    // palacefloor <game folder> <out.png>: the rooms' floor -- each bottom-layer cell with nothing over it, in its brick's average colour (as
    // the Palace island colours it, before matching to Otringal's palette), 6 pixels a cell; and the floor's colours, most used first
    public static int Floor(string[] args)
    {
        var interiors = new Lba2Interiors(args[1]);
        var area = Lba2Areas.Find(interiors.LoadScene).First(a => a.Tiles.Any(t => t.Scene == 151));
        var cells = new Dictionary<(int X, int Y, int Z), (int Brick, int Tile)>();
        for (var ti = 0; ti < area.Tiles.Count; ti++)
        {
            var tile = area.Tiles[ti];
            var sep = Lba2Areas.Separations.Where(s => s.Scene == tile.Scene).Select(s => (s.Dx, s.Dy, s.Dz)).FirstOrDefault();
            int ox = tile.OffsetX / 512 - sep.Dx, oy = tile.OffsetY / 256 - sep.Dy, oz = tile.OffsetZ / 512 - sep.Dz;
            foreach (var p in interiors.Placements(tile)) cells.TryAdd((p.X + ox, p.Y + oy, p.Z + oz), (p.Brick, ti));
        }
        int minX = cells.Keys.Min(c => c.X), maxX = cells.Keys.Max(c => c.X), minY = cells.Keys.Min(c => c.Y);
        int minZ = cells.Keys.Min(c => c.Z), maxZ = cells.Keys.Max(c => c.Z);
        var averages = new Dictionary<int, (float R, float G, float B)>();
        const int S = 6;
        int w = (maxX - minX + 1) * S, h = (maxZ - minZ + 1) * S;
        var px = new byte[w * h * 4];
        for (var k = 3; k < px.Length; k += 4) px[k] = 255;
        var counts = new Dictionary<int, int>();
        foreach (var ((x, y, z), (brick, _)) in cells)
        {
            if (y != minY || brick < 0 || cells.ContainsKey((x, y + 1, z))) continue;
            if (!averages.TryGetValue(brick, out var a)) averages[brick] = a = LBAAssembler.Terrain.Palace.BrickBuilding.BrickAverage(interiors.ReadBrick(brick), interiors.Palette);
            counts[brick] = counts.GetValueOrDefault(brick) + 1;
            for (var i = 0; i < S - 1; i++)
                for (var j = 0; j < S - 1; j++)
                {
                    var at = (((z - minZ) * S + j) * w + (x - minX) * S + i) * 4;
                    px[at] = (byte)Math.Clamp(a.B, 0, 255); px[at + 1] = (byte)Math.Clamp(a.G, 0, 255); px[at + 2] = (byte)Math.Clamp(a.R, 0, 255);
                }
        }
        PngWriter.Write(args[2], px, w, h);
        Console.WriteLine($"floor cells x {minX}..{maxX} z {minZ}..{maxZ} (layer {minY}); bricks, most used first:");
        foreach (var (brick, n) in counts.OrderByDescending(c => c.Value))
        {
            var a = averages[brick];
            Console.WriteLine($"  brick {brick}: {n} cells, rgb ({a.R:0},{a.G:0},{a.B:0})");
        }
        // (the inner walls: a cell over the floor with an empty cell beside it inside a room's columns -- each layer's colours, by how many
        // faces; drawn bricks only, as the island has them)
        var extents = new List<(int X0, int Z0, int X1, int Z1)>();
        foreach (var g in cells.GroupBy(c => c.Value.Tile))
            extents.Add((g.Min(c => c.Key.X), g.Min(c => c.Key.Z), g.Max(c => c.Key.X), g.Max(c => c.Key.Z)));
        bool Inside(int x, int z) => extents.Any(e => x > e.X0 && x < e.X1 && z > e.Z0 && z < e.Z1);
        var maxY = cells.Keys.Max(c => c.Y);
        for (var y = minY + 1; y <= maxY; y++)
        {
            var layer = new Dictionary<(int R, int G, int B), int>();
            var total = 0;
            foreach (var ((x, cy, z), (brick, _)) in cells)
            {
                if (cy != y || brick < 0) continue;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    if (cells.ContainsKey((x + dx, y, z + dz)) || !Inside(x + dx, z + dz)) continue;
                    if (!averages.TryGetValue(brick, out var a)) averages[brick] = a = LBAAssembler.Terrain.Palace.BrickBuilding.BrickAverage(interiors.ReadBrick(brick), interiors.Palette);
                    var key = ((int)(a.R / 16) * 16, (int)(a.G / 16) * 16, (int)(a.B / 16) * 16);
                    layer[key] = layer.GetValueOrDefault(key) + 1; total++;
                }
            }
            if (total == 0) continue;
            Console.WriteLine($"wall layer {y}: {total} faces; " + string.Join(", ", layer.OrderByDescending(p => p.Value).Take(7).Select(p => $"({p.Key.R},{p.Key.G},{p.Key.B}) {p.Value * 100 / total}%")));
        }
        return 0;
    }

    // palaceblocks <game folder> <out folder>: the library blocks the palace grids are made of -- each block's size, how often it is placed
    // (whole or in part) in which rooms, and a picture of each (<out>/block<n>.png) -- to tell its furniture from its walls and floors
    public static int Blocks(string[] args)
    {
        var game = args[1]; var outDir = args[2];
        Directory.CreateDirectory(outDir);
        var backend = new LBAAssembler.Grids.Lba2GridBackend(game);
        var interiors = new Lba2Interiors(game);
        var area = Lba2Areas.Find(interiors.LoadScene).First(a => a.Tiles.Any(t => t.Scene == 151));
        var uses = new Dictionary<int, List<(int Scene, int X, int Y, int Z, int Cells)>>();
        var dims = new Dictionary<int, (int Dx, int Dy, int Dz)>();
        byte[]? library = null;
        foreach (var tile in area.Tiles)
        {
            var gridId = backend.GridOfScene(tile.Scene)!.Value;
            library = backend.LoadLibrary(gridId);
            var cells = LBAAssembler.Lba1.Lba1GridRenderer.DecodeGrid(backend.LoadGrid(gridId));
            var instances = new Dictionary<(int Block, int X, int Y, int Z), int>();
            for (var z = 0; z < 64; z++) for (var x = 0; x < 64; x++) for (var y = 0; y < 25; y++)
            {
                var i = ((z * 64 + x) * 25 + y) * 2;
                int block = cells[i], pos = cells[i + 1];
                if (block == 0) continue;
                var at = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(library.AsSpan((block - 1) * 4));
                int dx = library[at], dy = library[at + 1], dz = library[at + 2];
                dims[block] = (dx, dy, dz);
                int bi = pos % dx, bj = pos / dx % dy, bk = pos / dx / dy;
                var key = (block, x - bi, y - bj, z - bk);
                instances[key] = instances.GetValueOrDefault(key) + 1;
            }
            foreach (var ((block, x, y, z), n) in instances)
            {
                if (!uses.TryGetValue(block, out var list)) uses[block] = list = new();
                list.Add((tile.Scene, x, y, z, n));
            }
        }
        foreach (var (block, list) in uses.OrderBy(u => u.Key))
        {
            var (dx, dy, dz) = dims[block];
            Console.WriteLine($"block {block}: {dx} x {dy} x {dz}, {list.Count} placements ({list.Sum(l => l.Cells)} cells) in rooms {string.Join(",", list.Select(l => l.Scene).Distinct())}; " +
                              $"first at ({list[0].X},{list[0].Y},{list[0].Z}) in {list[0].Scene}");
            // (its picture: the block alone, every cell of it)
            var at = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(library!.AsSpan((block - 1) * 4));
            var placements = new List<LBAAssembler.Lba1.Lba1Placement>();
            for (var p = 0; p < dx * dy * dz; p++)
            {
                var number = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(library.AsSpan(at + 3 + p * 4 + 2));
                if (number == 0) continue;
                placements.Add(new LBAAssembler.Lba1.Lba1Placement(p % dx, p / dx % dy, p / dx / dy, number - 1));
            }
            if (placements.Count == 0) continue;
            var image = LBAAssembler.Lba1.Lba1GridRenderer.Render(new[] { new LBAAssembler.Lba1.Lba1Tile(placements, 0, 0, 0) }, backend.Brick, backend.Palette);
            PngWriter.Write(Path.Combine(outDir, $"block{block}.png"), image.Bgra, image.Width, image.Height);
        }
        return 0;
    }

    // meshshot <mesh.bin> <out.png> [--size W H] [--target x y z] [--yaw deg] [--pitch deg] [--dist d] [--fov deg] [--top] [--box x0 z0 x1 z1]
    //   [--no-ground] [--clip-y y [--cut]] [--clip-box x0 y0 z0 x1 y1 z1]: an islandmesh dump drawn with a depth buffer -- a perspective
    //   camera `dist` from `target`, turned `yaw` about the upright (0: looking north, from the south) and tilted down `pitch`; or (--top)
    //   straight down over the box. --clip-y leaves out the triangles above that height (a look into the rooms), with --cut the parts of
    //   triangles above it; --clip-box only what is inside it.
    public static int Shot(string[] args)
    {
        double[] Opt(string name, params double[] fallback)
        {
            var i = Array.IndexOf(args, name);
            return i < 0 ? fallback : args.Skip(i + 1).Take(fallback.Length).Select(double.Parse).ToArray();
        }
        var raw = File.ReadAllBytes(args[1]);
        var n = raw.Length / 52;
        var f = new float[n * 13];
        Buffer.BlockCopy(raw, 0, f, 0, n * 52);
        int W = (int)Opt("--size", 1200, 800)[0], H = (int)Opt("--size", 1200, 800)[1];
        var box = Opt("--box", double.NaN, 0, 0, 0);
        var clipY = Opt("--clip-y", double.NaN)[0];
        var clipBox = Opt("--clip-box", double.NaN, 0, 0, 0, 0, 0);
        var noGround = args.Contains("--no-ground");
        var cutAtClip = args.Contains("--cut") && !double.IsNaN(clipY);
        var light = Vector3.Normalize(new Vector3(-0.45f, 0.8f, 0.4f));
        var tris = new List<(Vector3 A, Vector3 B, Vector3 C, Vector3 Col, bool Body)>();
        for (var t = 0; t < n; t++)
        {
            var o = t * 13;
            if (noGround && f[o + 12] == 1) continue;
            Vector3 a = new(f[o], f[o + 1], f[o + 2]), b = new(f[o + 3], f[o + 4], f[o + 5]), c = new(f[o + 6], f[o + 7], f[o + 8]);
            var m = (a + b + c) / 3;
            if (!double.IsNaN(box[0]) && (m.X < box[0] || m.X > box[2] || m.Z < box[1] || m.Z > box[3])) continue;
            if (!double.IsNaN(clipY) && !cutAtClip && Math.Min(a.Y, Math.Min(b.Y, c.Y)) > clipY - 1) continue;
            if (!double.IsNaN(clipBox[0]) && (m.X < clipBox[0] || m.Y < clipBox[1] || m.Z < clipBox[2] || m.X > clipBox[3] || m.Y > clipBox[4] || m.Z > clipBox[5])) continue;
            var nn = Vector3.Cross(b - a, c - a);
            if (nn.LengthSquared() < 1e-6f) continue;
            nn = Vector3.Normalize(nn);
            // (a cut box -- islandmesh --caps -- in its own colour, unlit)
            var shade = f[o + 12] == 3 ? 1f : f[o + 12] == 1 ? 0.75f + 0.25f * Math.Abs(Vector3.Dot(nn, light)) : 0.55f + 0.45f * Math.Abs(Vector3.Dot(nn, light));
            var col = new Vector3(f[o + 9], f[o + 10], f[o + 11]) * shade;
            if (cutAtClip)
            {
                // (--cut: the triangle cut at the clip height, what is over it left out -- not the whole triangle kept or dropped)
                var corners = new[] { a, b, c };
                var under = new List<Vector3>();
                for (var i = 0; i < 3; i++)
                {
                    Vector3 p = corners[i], q = corners[(i + 1) % 3];
                    bool pin = p.Y <= clipY, qin = q.Y <= clipY;
                    if (pin) under.Add(p);
                    if (pin != qin) under.Add(p + (q - p) * (float)((clipY - p.Y) / (q.Y - p.Y)));
                }
                for (var k = 1; k + 1 < under.Count; k++) tris.Add((under[0], under[k], under[k + 1], col, f[o + 12] != 1));
                continue;
            }
            tris.Add((a, b, c, col, f[o + 12] != 1));
        }
        var depth = new float[W * H]; Array.Fill(depth, float.MaxValue);
        var bgra = new byte[W * H * 4];
        for (var i = 0; i < W * H; i++) { bgra[i * 4] = 40; bgra[i * 4 + 1] = 30; bgra[i * 4 + 2] = 25; bgra[i * 4 + 3] = 255; }
        Func<Vector3, Vector3> toCamera; Func<Vector3, (float X, float Y)> project; float near; var eyeAt = Vector3.Zero;
        if (args.Contains("--top"))
        {
            // (straight down: x to the right, z down the picture, the box -- or everything -- fitted)
            float x0 = (float)box[0], z0 = (float)box[1], x1 = (float)box[2], z1 = (float)box[3];
            if (double.IsNaN(box[0])) { x0 = tris.Min(t => Math.Min(t.A.X, Math.Min(t.B.X, t.C.X))); x1 = tris.Max(t => Math.Max(t.A.X, Math.Max(t.B.X, t.C.X))); z0 = tris.Min(t => Math.Min(t.A.Z, Math.Min(t.B.Z, t.C.Z))); z1 = tris.Max(t => Math.Max(t.A.Z, Math.Max(t.B.Z, t.C.Z))); }
            var s = Math.Min((W - 20) / (x1 - x0), (H - 20) / (z1 - z0));
            toCamera = p => new Vector3((p.X - x0) * s + 10, (p.Z - z0) * s + 10, 100000 - p.Y);
            project = p => (p.X, p.Y);
            near = float.MinValue;
        }
        else
        {
            var target = Opt("--target", 0, 0, 0); var yaw = Opt("--yaw", 0)[0] * Math.PI / 180; var pitch = Opt("--pitch", 30)[0] * Math.PI / 180;
            var dist = Opt("--dist", 50000)[0]; var fov = Opt("--fov", 60)[0] * Math.PI / 180;
            // (the camera looks along `forward`: north (-z) turned by yaw, tilted down by pitch)
            var forward = new Vector3((float)(Math.Sin(yaw) * Math.Cos(pitch)), (float)-Math.Sin(pitch), (float)(-Math.Cos(yaw) * Math.Cos(pitch)));
            var tgt = new Vector3((float)target[0], (float)target[1], (float)target[2]);
            var eye = tgt - forward * (float)dist; eyeAt = eye;
            var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            var up = Vector3.Cross(right, forward);
            var focal = (float)(W / 2 / Math.Tan(fov / 2));
            toCamera = p => { var d = p - eye; return new Vector3(Vector3.Dot(d, right), Vector3.Dot(d, up), Vector3.Dot(d, forward)); };
            project = p => (W / 2f + p.X / p.Z * focal, H / 2f - p.Y / p.Z * focal);
            near = 50;
        }
        var drawn = 0;
        // (--cull: the bodies' faces seen from behind left out, as the engine does -- their polygons are wound so that the cross product of
        // their first edges points out)
        var cull = args.Contains("--cull");
        foreach (var (a, b, c, col, isBody) in tris)
        {
            var poly = new List<Vector3> { toCamera(a), toCamera(b), toCamera(c) };
            if (cull && isBody)
            {
                // (in the world: a face looks at the camera when its normal points towards the eye -- straight down, up)
                var outward = Vector3.Cross(b - a, c - a);
                var facing = near == float.MinValue ? outward.Y : Vector3.Dot(outward, eyeAt - a);
                if (facing < 0) continue;
            }
            if (near > float.MinValue)
            {
                // (cut at the near plane)
                var cut = new List<Vector3>();
                for (var i = 0; i < poly.Count; i++)
                {
                    Vector3 p = poly[i], q = poly[(i + 1) % poly.Count];
                    bool pin = p.Z >= near, qin = q.Z >= near;
                    if (pin) cut.Add(p);
                    if (pin != qin) cut.Add(p + (q - p) * ((near - p.Z) / (q.Z - p.Z)));
                }
                poly = cut;
                if (poly.Count < 3) continue;
            }
            var scr = poly.Select(p => { var (sx, sy) = project(p); return new Vector3(sx, sy, p.Z); }).ToList();
            byte cr = (byte)Math.Clamp(col.X, 0, 255), cg = (byte)Math.Clamp(col.Y, 0, 255), cb = (byte)Math.Clamp(col.Z, 0, 255);
            for (var k = 1; k + 1 < scr.Count; k++)
            {
                Vector3 p0 = scr[0], p1 = scr[k], p2 = scr[k + 1];
                var area = (p1.X - p0.X) * (p2.Y - p0.Y) - (p2.X - p0.X) * (p1.Y - p0.Y);
                if (Math.Abs(area) < 1e-6f) continue;
                int minx = Math.Max(0, (int)Math.Floor(Math.Min(p0.X, Math.Min(p1.X, p2.X)))), maxx = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(p0.X, Math.Max(p1.X, p2.X))));
                int miny = Math.Max(0, (int)Math.Floor(Math.Min(p0.Y, Math.Min(p1.Y, p2.Y)))), maxy = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(p0.Y, Math.Max(p1.Y, p2.Y))));
                float iz0 = 1 / p0.Z, iz1 = 1 / p1.Z, iz2 = 1 / p2.Z;
                var ortho = near == float.MinValue;
                for (var y = miny; y <= maxy; y++)
                    for (var x = minx; x <= maxx; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        var w0 = ((p1.X - px) * (p2.Y - py) - (p2.X - px) * (p1.Y - py)) / area;
                        var w1 = ((p2.X - px) * (p0.Y - py) - (p0.X - px) * (p2.Y - py)) / area;
                        var w2 = 1 - w0 - w1;
                        if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                        var z = ortho ? w0 * p0.Z + w1 * p1.Z + w2 * p2.Z : 1 / (w0 * iz0 + w1 * iz1 + w2 * iz2);
                        var idx = y * W + x;
                        if (z >= depth[idx]) continue;
                        depth[idx] = z;
                        bgra[idx * 4] = cb; bgra[idx * 4 + 1] = cg; bgra[idx * 4 + 2] = cr;
                    }
                drawn++;
            }
        }
        PngWriter.Write(args[2], bgra, W, H);
        Console.WriteLine($"{args[2]}: {tris.Count} triangles, {drawn} drawn");
        return 0;
    }

    // gridcolumn <game folder> <scene> <x> <z> [<x1> <z1>]: what an interior's grid has in a column (or a rectangle of them), layer by layer:
    // block (library), place in the block, brick
    public static int GridColumn(string[] args)
    {
        var backend = new LBAAssembler.Grids.Lba2GridBackend(args[1]);
        var grid = backend.GridOfScene(int.Parse(args[2]))!.Value;
        var library = backend.LoadLibrary(grid);
        var cells = LBAAssembler.Lba1.Lba1GridRenderer.DecodeGrid(backend.LoadGrid(grid));
        int x0 = int.Parse(args[3]), z0 = int.Parse(args[4]), x1 = args.Length > 6 ? int.Parse(args[5]) : x0, z1 = args.Length > 6 ? int.Parse(args[6]) : z0;
        for (var z = z0; z <= z1; z++)
            for (var x = x0; x <= x1; x++)
            {
                var line = new List<string>();
                for (var y = 0; y < 25; y++)
                {
                    var i = ((z * 64 + x) * 25 + y) * 2;
                    if (cells[i] == 0) continue;
                    line.Add($"{y}:{cells[i]}.{cells[i + 1]}");
                }
                Console.WriteLine($"({x},{z}): {string.Join(" ", line)}");
            }
        return 0;
    }

    // blockface <game folder> <scene> <block> <out.png>: a library block's picture (as the joined map draws it) and its +x and +z faces
    // looked up through the picture's projection, 8 samples a cell and a layer -- to check what the Palace island's furniture samples
    public static int BlockFace(string[] args)
    {
        var backend = new LBAAssembler.Grids.Lba2GridBackend(args[1]);
        var grid = backend.GridOfScene(int.Parse(args[2]))!.Value;
        var library = backend.LoadLibrary(grid);
        var block = int.Parse(args[3]);
        var at = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(library.AsSpan((block - 1) * 4));
        int dx = library[at], dy = library[at + 1], dz = library[at + 2];
        var placements = new List<LBAAssembler.Lba1.Lba1Placement>();
        for (var p = 0; p < dx * dy * dz; p++)
        {
            var number = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(library.AsSpan(at + 3 + p * 4 + 2));
            if (number != 0) placements.Add(new LBAAssembler.Lba1.Lba1Placement(p % dx, p / dx % dy, p / dx / dy, number - 1));
        }
        var image = LBAAssembler.Lba1.Lba1GridRenderer.Render(new[] { new LBAAssembler.Lba1.Lba1Tile(placements, 0, 0, 0) }, backend.Brick, backend.Palette);
        const int S = 8;
        int fxw = dz * S, fzw = dx * S, fh = dy * S;
        int W = image.Width + 20 + fxw * 3 + 20 + fzw * 3, H = Math.Max(image.Height, fh * 3);
        var bgra = new byte[W * H * 4];
        for (var y = 0; y < image.Height; y++) Array.Copy(image.Bgra, y * image.Width * 4, bgra, y * W * 4, image.Width * 4);
        void Face(int left, int width, Func<double, double, System.Windows.Point> project)
        {
            for (var v = 0; v < fh; v++)
                for (var u = 0; u < width; u++)
                {
                    var p = project((u + 0.5) / S * 512, (v + 0.5) / S * 256);
                    int px = (int)Math.Floor(p.X), py = (int)Math.Floor(p.Y);
                    if (px < 0 || py < 0 || px >= image.Width || py >= image.Height) continue;
                    var o = (py * image.Width + px) * 4;
                    for (var k = 0; k < 9; k++)
                    {
                        int X = left + u * 3 + k % 3, Y = (fh - 1 - v) * 3 + k / 3;
                        Array.Copy(image.Bgra, o, bgra, (Y * W + X) * 4, 4);
                    }
                    // (and mark where it was sampled on the picture)
                    if (u % 4 == 0 && v % 4 == 0) { var q = (py * W + px) * 4; bgra[q] = 255; bgra[q + 1] = 0; bgra[q + 2] = 255; bgra[q + 3] = 255; }
                }
        }
        // (a point is drawn 24 pixels right of and 15 below where Project puts it: PalaceFurniture.Picture; another shift as "dx,dy")
        var shift = args.Length > 5 ? args[5].Split(",").Select(double.Parse).ToArray() : new[] { 24.0, 15.0 };
        System.Windows.Point At(double x, double y, double z) { var p = image.Project(x, y, z); return new System.Windows.Point(p.X + shift[0], p.Y + shift[1]); }
        Face(image.Width + 20, fxw, (a, h) => At(dx * 512 - 1, h, a));
        Face(image.Width + 20 + fxw * 3 + 20, fzw, (a, h) => At(a, h, dz * 512 - 1));
        PngWriter.Write(args[4], bgra, W, H);
        Console.WriteLine($"block {block}: {dx} x {dy} x {dz}, picture {image.Width} x {image.Height}, origin ({image.OriginX}, {image.OriginY}) -> {args[4]}");
        return 0;
    }

    // oblcheck <obl> [first]: each body from `first` on -- points, polygons, its extent -- and the ones past what the engine takes (550
    // points, 550 polygons and lines and spheres)
    public static int OblCheck(string[] args)
    {
        var archive = HqrArchive.Open(args[1]);
        var count = HqrArchive.CountEntries(args[1]);
        var first = args.Length > 2 ? int.Parse(args[2]) : 0;
        int worstPoints = 0, worstPrims = 0, bad = 0;
        for (var i = first; i < count; i++)
        {
            var body = Body.Read(archive.Read(i), 2, allowStatic: true);
            var prims = body.Faces.Count + body.Lines.Count + body.Spheres.Count;
            worstPoints = Math.Max(worstPoints, body.Vertices.Count); worstPrims = Math.Max(worstPrims, prims);
            if (body.Vertices.Count > 550 || prims > 550) { bad++; Console.WriteLine($"  body {i}: {body.Vertices.Count} points, {prims} primitives -- too many"); }
        }
        Console.WriteLine($"{count - first} bodies from {first}: at most {worstPoints} points and {worstPrims} primitives; {bad} past the engine's 550");
        return bad == 0 ? 0 : 1;
    }

    // palacefurniture <game folder>: the furniture PalaceFurniture finds, room by room
    public static int Furniture(string[] args)
    {
        var interiors = new Lba2Interiors(args[1]);
        var area = Lba2Areas.Find(interiors.LoadScene).First(a => a.Tiles.Any(t => t.Scene == 151));
        var cells = new Dictionary<(int X, int Y, int Z), (int Brick, int Tile)>();
        var offsets = new (int X, int Y, int Z)[area.Tiles.Count];
        for (var ti = 0; ti < area.Tiles.Count; ti++)
        {
            var tile = area.Tiles[ti];
            var sep = Lba2Areas.Separations.Where(s => s.Scene == tile.Scene).Select(s => (s.Dx, s.Dy, s.Dz)).FirstOrDefault();
            offsets[ti] = (tile.OffsetX / 512 - sep.Dx, tile.OffsetY / 256 - sep.Dy, tile.OffsetZ / 512 - sep.Dz);
            foreach (var p in interiors.Placements(tile)) cells.TryAdd((p.X + offsets[ti].X, p.Y + offsets[ti].Y, p.Z + offsets[ti].Z), (p.Brick, ti));
        }
        var pieces = LBAAssembler.Terrain.Palace.PalaceFurniture.Find(args[1], area, offsets, cells);
        var chests = LBAAssembler.Terrain.Palace.PalaceFurniture.Chests(interiors, area, offsets);
        for (var ti = 0; ti < area.Tiles.Count; ti++)
        {
            var mine = pieces.Where(p => p.Tile == ti).GroupBy(p => p.Kind).Select(g => $"{g.Count()} {g.Key}").ToList();
            var chestCount = chests.Count(c => c.Tile == ti);
            if (chestCount > 0) mine.Add($"{chestCount} chest{(chestCount > 1 ? "s" : "")}");
            Console.WriteLine($"room {area.Tiles[ti].Scene}: {string.Join(", ", mine)}");
        }
        return 0;
    }

    // decorcount <ile>: how many decors each cube holds (the engine draws 200 at the most: MAX_OBJ_DECORS)
    public static int DecorCount(string[] args)
    {
        var island = IslandFile.Load(args[1]);
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island).OrderByDescending(c => c.Cube.Decors.Count))
            Console.WriteLine($"cube ({cx},{cz}): {cube.Decors.Count} decors (INF says {cube.Info[IslandCube.InfoNbDecors]})");
        // (with the OBL: every decor's body there, every hot point inside its cube)
        if (args.Length > 2)
        {
            var count = HqrArchive.CountEntries(args[2]);
            var wrong = 0;
            foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
                foreach (var d in cube.Decors)
                    if ((d.Body & 0xFFFF) >= count || d.X < 0 || d.X > 32767 || d.Z < 0 || d.Z > 32767) { wrong++; Console.WriteLine($"  cube ({cx},{cz}): body {d.Body & 0xFFFF} at ({d.X},{d.Y},{d.Z})"); }
            Console.WriteLine($"{count} bodies in the OBL; {wrong} decors wrong");
        }
        return 0;
    }

    // palacezones <game folder>: the palace rooms' ways out (cube-change zones) and every other scene's ways into them
    public static int Zones(string[] args)
    {
        var interiors = new Lba2Interiors(args[1]);
        var palace = new HashSet<int> { 80 };
        for (var s = 151; s <= 166; s++) palace.Add(s);
        for (var s = 0; s < 260; s++)
        {
            var model = interiors.LoadScene(s);
            if (model is null) continue;
            foreach (var z in model.Zones.Where(z => z.Type == 0))
            {
                var fromPalace = palace.Contains(s); var toPalace = palace.Contains(z.Num);
                if (!fromPalace && !toPalace) continue;
                if (fromPalace && toPalace && !args.Contains("--inner")) continue;
                Console.WriteLine($"scene {s} (island {model.Island} cube ({model.CubeX},{model.CubeY}) mode {model.CubeMode}) -> {z.Num}: box ({z.X0},{z.Y0},{z.Z0})-({z.X1},{z.Y1},{z.Z1}) info [{string.Join(",", z.Info)}]");
            }
        }
        return 0;
    }

    public static int Mesh(string[] args)
    {
        var island = IslandFile.Load(args[1]);
        var obl = HqrArchive.Open(args[2]);
        // (the palette from RESS.HQR beside the OBL, or the game folder's: LBA2_DIR)
        var oblFolder = Path.GetDirectoryName(Path.GetFullPath(args[2]))!;
        var ressFolder = File.Exists(Path.Combine(oblFolder, "RESS.HQR")) ? oblFolder : Environment.GetEnvironmentVariable("LBA2_DIR") ?? @"E:\GOG Games\Little Big Adventure 2";
        var palette = IslandMapRenderer.LoadPaletteEntry(ressFolder, int.Parse(args[3]));
        var capsAt = Array.IndexOf(args, "--caps");
        var rest = args.Skip(5).Where((a, i) => !a.StartsWith("--") && i + 5 != capsAt + 1).Select(double.Parse).ToArray();
        var (x0, z0, x1, z1) = rest.Length >= 4 ? (rest[0], rest[1], rest[2], rest[3]) : (double.MinValue, double.MinValue, double.MaxValue, double.MaxValue);
        using var w = new BinaryWriter(File.Create(args[4]));
        var tris = 0;
        void Tri(Vector3 a, Vector3 b, Vector3 c, (byte R, byte G, byte B) col, int kind)
        {
            foreach (var p in new[] { a, b, c }) { w.Write(p.X); w.Write(p.Y); w.Write(p.Z); }
            w.Write((float)col.R); w.Write((float)col.G); w.Write((float)col.B); w.Write((float)kind);
            tris++;
        }
        (byte, byte, byte) Pal(byte[] pal, int i) { var c = LBAAssembler.Terrain.Palace.BrickBuilding.Colour(pal, i); return (c.R, c.G, c.B); }
        if (!args.Contains("--no-terrain"))
        {
            // (each cell's ground: its two triangles in the colour of their texture's middle)
            foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
                for (var z = 0; z < 64; z++)
                    for (var x = 0; x < 64; x++)
                    {
                        double wx = cx * 32768.0 + x * 512, wz = cz * 32768.0 + z * 512;
                        if (wx + 512 < x0 || wx > x1 || wz + 512 < z0 || wz > z1) continue;
                        Vector3 V(int dx, int dz) => new((float)(wx + dx * 512), cube.Heights[(z + dz) * 65 + x + dx], (float)(wz + dz * 512));
                        var p0 = new IslandPolygon(cube.Polygon(x, z, 0));
                        var corners = p0.Diagonal ? new[] { new[] { (0, 0), (1, 0), (0, 1) }, new[] { (1, 0), (1, 1), (0, 1) } } : new[] { new[] { (0, 0), (1, 1), (0, 1) }, new[] { (0, 0), (1, 0), (1, 1) } };
                        for (var half = 0; half < 2; half++)
                        {
                            var poly = new IslandPolygon(cube.Polygon(x, z, half));
                            (byte, byte, byte) col = (40, 80, 130);
                            var ti = poly.TextureIndex;
                            if (poly.TexFlag != 0 && ti * 6 + 6 <= cube.TextureDefs.Length)
                            {
                                int u = (cube.TextureDefs[ti * 6] + cube.TextureDefs[ti * 6 + 2] + cube.TextureDefs[ti * 6 + 4]) / 3 >> 8;
                                int v = (cube.TextureDefs[ti * 6 + 1] + cube.TextureDefs[ti * 6 + 3] + cube.TextureDefs[ti * 6 + 5]) / 3 >> 8;
                                var texel = island.GroundTexture[(v & 255) * 256 + (u & 255)];
                                col = texel == 0 && poly.PolyFlag != 0 ? Pal(palette, poly.Bank * 16 + 11) : Pal(palette, texel);
                            }
                            else if (poly.PolyFlag != 0) col = Pal(palette, poly.Bank * 16 + 11);
                            var c = corners[half];
                            Tri(V(c[0].Item1, c[0].Item2), V(c[1].Item1, c[1].Item2), V(c[2].Item1, c[2].Item2), col, 1);
                        }
                    }
        }
        if (args.Contains("--boxes"))
        {
            // (the solid boxes -- collision -- as boxes, coloured by height: kind 2)
            foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
                foreach (var d in cube.Decors)
                {
                    if (d.YMax < d.YMin) continue;
                    float bx0 = cx * 32768f + d.XMin, bx1 = cx * 32768f + d.XMax, bz0 = cz * 32768f + d.ZMin, bz1 = cz * 32768f + d.ZMax;
                    if (bx1 < x0 || bx0 > x1 || bz1 < z0 || bz0 > z1) continue;
                    float by0 = d.YMin, by1 = d.YMax;
                    var t = Math.Clamp((by1 - 2000) / 30000f, 0, 1);
                    (byte, byte, byte) col = ((byte)(60 + 190 * t), (byte)(200 - 120 * t), (byte)(90));
                    Vector3 P(int i) => new((i & 1) != 0 ? bx1 : bx0, (i & 2) != 0 ? by1 : by0, (i & 4) != 0 ? bz1 : bz0);
                    foreach (var q in new[] { new[] { 2, 3, 7, 6 }, new[] { 0, 1, 5, 4 }, new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 }, new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 } })
                    { Tri(P(q[0]), P(q[1]), P(q[2]), col, 2); Tri(P(q[0]), P(q[2]), P(q[3]), col, 2); }
                }
        }
        if (capsAt >= 0)
        {
            // (--caps y: the solid boxes through height y as their cross-section there, dark -- kind 3, a plan's cut walls: drawn with
            // meshshot --top --clip-y y --cut, a look down into rooms)
            var capY = float.Parse(args[capsAt + 1]);
            foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
                foreach (var d in cube.Decors)
                {
                    if (d.YMax < d.YMin || d.YMin >= capY || d.YMax <= capY) continue;
                    float bx0 = cx * 32768f + d.XMin, bx1 = cx * 32768f + d.XMax, bz0 = cz * 32768f + d.ZMin, bz1 = cz * 32768f + d.ZMax;
                    if (bx1 < x0 || bx0 > x1 || bz1 < z0 || bz0 > z1) continue;
                    (byte, byte, byte) col = (44, 40, 40);
                    Tri(new(bx0, capY, bz0), new(bx0, capY, bz1), new(bx1, capY, bz1), col, 3);
                    Tri(new(bx0, capY, bz0), new(bx1, capY, bz1), new(bx1, capY, bz0), col, 3);
                }
        }
        if (!args.Contains("--no-decors"))
        {
            var texels = args.Contains("--texels");
            var bodies = new Dictionary<int, Body?>();
            // (with --texels, a body copied into several cubes -- the palace's are, to every cube they are seen from -- drawn once)
            var drawn = new HashSet<(int Body, double X, int Y, double Z, int Beta)>();
            foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
                foreach (var d in cube.Decors)
                {
                    double wx = cx * 32768.0 + d.X, wz = cz * 32768.0 + d.Z;
                    if (cx * 32768.0 + d.XMax < x0 || cx * 32768.0 + d.XMin > x1 || cz * 32768.0 + d.ZMax < z0 || cz * 32768.0 + d.ZMin > z1) continue;
                    if (texels && !drawn.Add((d.Body, wx, d.Y, wz, d.Beta))) continue;
                    var index = d.Body & 0xFFFF;
                    if (!bodies.TryGetValue(index, out var body))
                    {
                        try { body = Body.Read(obl.Read(index), 2, allowStatic: true); } catch (Exception) { body = null; }
                        bodies[index] = body;
                    }
                    if (body is null) continue;
                    var page = island.ObjectPage(IslandFile.ObjectPageOf(d));
                    var turn = Matrix4x4.CreateRotationY((float)((d.Beta & 0xFFFF) * 2 * Math.PI / 4096)) * Matrix4x4.CreateTranslation((float)wx, d.Y, (float)wz);
                    var pts = body.Vertices.Select(v => Vector3.Transform(v, turn)).ToArray();
                    foreach (var f in body.Faces)
                    {
                        (byte, byte, byte) col;
                        // (--texels: a textured face in pieces some two pixels of its texture across, each the colour of its own -- the
                        // dungeon's stone; else in one, the colour of its middle)
                        if (texels && f.Texture is { } tt && tt.Handle < body.Textures.Length && f.Points.Length is 3 or 4)
                        {
                            var info = body.Textures[tt.Handle];
                            int offset = (int)(info & 0xFFFF), mx = (int)(info >> 16) & 0xFF, my = (int)(info >> 24) & 0xFF;
                            var c4 = Enumerable.Range(0, 4).Select(j => Math.Min(j, f.Points.Length - 1)).ToArray();
                            Vector3[] P = c4.Select(j => pts[f.Points[j]]).ToArray();
                            (double U, double V)[] T = c4.Select(j => (tt.UV[j * 2] / 256.0, tt.UV[j * 2 + 1] / 256.0)).ToArray();
                            var span = Enumerable.Range(0, 4).Max(j => Math.Max(Math.Abs(T[j].U - T[(j + 1) % 4].U), Math.Abs(T[j].V - T[(j + 1) % 4].V)));
                            var n = Math.Clamp((int)Math.Ceiling(span / 2), 1, 64);
                            Vector3 At(double s, double q) => Vector3.Lerp(Vector3.Lerp(P[0], P[1], (float)s), Vector3.Lerp(P[3], P[2], (float)s), (float)q);
                            (double, double) Uv(double s, double q) => (T[0].U + (T[1].U - T[0].U) * s + (T[3].U - T[0].U) * q + (T[2].U - T[1].U - T[3].U + T[0].U) * s * q,
                                                                       T[0].V + (T[1].V - T[0].V) * s + (T[3].V - T[0].V) * q + (T[2].V - T[1].V - T[3].V + T[0].V) * s * q);
                            for (var a = 0; a < n; a++)
                                for (var b = 0; b < n; b++)
                                {
                                    double s0 = (double)a / n, s1 = (double)(a + 1) / n, q0 = (double)b / n, q1 = (double)(b + 1) / n;
                                    var (cu, cv) = Uv((s0 + s1) / 2, (q0 + q1) / 2);
                                    var piece = Pal(palette, page[(offset + ((int)Math.Floor(cv) & my) * 256 + ((int)Math.Floor(cu) & mx)) & 0xFFFF]);
                                    Tri(At(s0, q0), At(s1, q0), At(s1, q1), piece, 0);
                                    Tri(At(s0, q0), At(s1, q1), At(s0, q1), piece, 0);
                                }
                            continue;
                        }
                        if (f.Texture is { } t && t.Handle < body.Textures.Length)
                        {
                            var info = body.Textures[t.Handle];
                            int offset = (int)(info & 0xFFFF), mx = (int)(info >> 16) & 0xFF, my = (int)(info >> 24) & 0xFF;
                            int n = f.Points.Length; int su = 0, sv = 0;
                            for (var j = 0; j < n; j++) { su += t.UV[j * 2]; sv += t.UV[j * 2 + 1]; }
                            int u = (su / n >> 8) & mx, v = (sv / n >> 8) & my;
                            col = Pal(palette, page[(offset + v * 256 + u) & 0xFFFF]);
                        }
                        else
                        {
                            // (a lit polygon's colour is the first of its ramp: the middle of it, as the light mostly leaves it)
                            var lit = f.Material is 4 or 7 or 1 or 3;
                            col = Pal(palette, lit ? Math.Min(255, f.Colour + 7) : f.Colour);
                        }
                        for (var k = 1; k + 1 < f.Points.Length; k++) Tri(pts[f.Points[0]], pts[f.Points[k]], pts[f.Points[k + 1]], col, 0);
                    }
                }
        }
        Console.WriteLine($"{args[4]}: {tris} triangles");
        return 0;
    }
}
