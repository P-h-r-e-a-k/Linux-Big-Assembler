using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using LbaBodyStudio;
using LBAAssembler.Lba1;

namespace LBAAssembler.Terrain.Palace;

// The palace rooms' furniture (the user, 2026-10-12: "Let's also see if for each inside room of the palace we can recreate the decor that
// each room naturally has"). A room's furniture is in its grid: blocks of the palace's brick library -- a red marble column, a potted plant,
// a lamp on the wall, a bench, a fireplace, a candelabra, a portrait -- each a few cells of bricks, pictures; the Palace island made each
// cell a box of its brick's average colour, so a plant was a purple block and a column a red post. Here each is made again in 3D where its
// bricks were, from what the bricks picture:
//   - Find: the furniture blocks' placements in the rooms' grids (a block's cells say which block they are and where in it, so the cells
//     of one placement come together again; a column stood on another of itself -- the grids make a tall one so -- is one); their cells
//     stay in the collision (the furniture is in the way, as in the game), and the standing pieces' cells leave the drawn faces, the model
//     drawn instead;
//   - the pieces on the walls -- portraits, the windows, the round window at the long hall's end (the one Twinsen leaves the palace by:
//     room 80's way out is in front of it, the big chest under it) -- are the wall itself in the grid: their cells stay a wall, in the
//     wallpaper, and the picture goes on its face: the portraits and the round window sampled from their bricks' pictures (each spot of the
//     face looked up where the isometric picture draws it), the windows a gold frame round panes of glass; the windows inside and out;
//   - Chests: the rooms' chests are actors, sprites (the closed chest of every other room, the big one under room 80's round window), as
//     3D chests.
// The doors are sprite actors too, but the game slides them open and shut (their track scripts); left out, the doorways stay open.
// A model is made in a room's own units (512 a cell across, 256 a layer up) about the middle of its block's foot, turned to face out of the
// wall it stands at, and goes to the island as the rooms do: Across times as wide, Up times as high; its faces flat, shaded by the way they
// face as the rooms' are, in Otringal's palette.
internal static class PalaceFurniture
{
    public enum Kind { Column, Plant, PlinthPlant, Lamp, Bench, Candelabra, Candle, Fireplace, Portrait, RoundWindow, Window }

    // the palace library's blocks that picture furniture, and their size (a block of another size is not taken for one)
    private static readonly Dictionary<int, (Kind Kind, int Dx, int Dy, int Dz)> Blocks = new()
    {
        [32] = (Kind.Column, 1, 8, 1), [31] = (Kind.Plant, 2, 4, 2), [51] = (Kind.PlinthPlant, 2, 5, 2), [29] = (Kind.Lamp, 1, 2, 1), [30] = (Kind.Lamp, 1, 2, 1),
        [61] = (Kind.Bench, 3, 2, 2), [62] = (Kind.Bench, 2, 2, 3), [63] = (Kind.Candelabra, 1, 4, 2), [64] = (Kind.Candelabra, 2, 4, 1),
        [102] = (Kind.Candle, 1, 3, 1), [68] = (Kind.Fireplace, 1, 5, 4), [69] = (Kind.Fireplace, 4, 5, 1), [105] = (Kind.Portrait, 1, 8, 4),
        [101] = (Kind.RoundWindow, 4, 8, 1), [57] = (Kind.Window, 1, 10, 4), [58] = (Kind.Window, 4, 10, 1),
    };
    // the chests' sprites (SPRITES.HQR): closed and open, the long side along z (342, 343, 346, 347) or x (344, 345, 348); 394 the big one
    private static readonly int[] ChestsAlongZ = { 342, 343, 346, 347 }, ChestsAlongX = { 344, 345, 348 };
    public const int BigChest = 394;

    // A placement of a furniture block in a room: its block, room (tile), first cell and size (cells of the island's palace) and the cells it
    // fills there; a portrait's or the round window's block picture (its bricks drawn alone, as the joined map draws them).
    public sealed record Piece(Kind Kind, int Block, int Tile, (int X, int Y, int Z) Origin, (int X, int Y, int Z) Size, List<(int X, int Y, int Z)> Cells,
        Lba1SceneImage? Image = null)
    {
        // a standing piece: its cells drawn by its model, not as boxes (the wall pieces' cells stay the wall)
        public bool Standing => Kind is not (Kind.Portrait or Kind.RoundWindow or Kind.Window);
    }

    // A chest: its room (tile), the middle of its foot (cells of the island's palace, fractions of one), its size (room units) and its long
    // side along x or not.
    public sealed record Chest(int Tile, int Sprite, double X, double Y, double Z, double Long, double Short, double High, bool AlongX);

    // The furniture blocks placed in the palace's rooms (the scenes of `area`, each at its offset in cells) whose cells the rooms kept
    // (`cells`: a room's cell another one's wall claimed is not its own).
    public static List<Piece> Find(string gameDirectory, Lba1Area area, (int X, int Y, int Z)[] offsets, IReadOnlyDictionary<(int X, int Y, int Z), (int Brick, int Tile)> cells)
    {
        var backend = new Grids.Lba2GridBackend(gameDirectory);
        var pieces = new List<Piece>();
        for (var ti = 0; ti < area.Tiles.Count; ti++)
        {
            var tile = area.Tiles[ti];
            if (backend.GridOfScene(tile.Scene) is not { } grid) continue;
            var library = backend.LoadLibrary(grid);
            var decoded = Lba1GridRenderer.DecodeGrid(backend.LoadGrid(grid));
            var count = (int)(BinaryPrimitives.ReadUInt32LittleEndian(library) / 4);
            var found = new Dictionary<(int Block, int X, int Y, int Z), List<(int, int, int)>>();
            for (var z = 0; z < Lba1GridRenderer.SizeZ; z++)
            for (var x = 0; x < Lba1GridRenderer.SizeX; x++)
            for (var y = 0; y < Lba1GridRenderer.SizeY; y++)
            {
                var i = ((z * Lba1GridRenderer.SizeX + x) * Lba1GridRenderer.SizeY + y) * 2;
                int block = decoded[i], pos = decoded[i + 1];
                if (block == 0 || block > count || !Blocks.TryGetValue(block, out var known) || !tile.Holds(x, z)) continue;
                var at = (int)BinaryPrimitives.ReadUInt32LittleEndian(library.AsSpan((block - 1) * 4));
                if (at + 3 > library.Length || (library[at], library[at + 1], library[at + 2]) != (known.Dx, known.Dy, known.Dz)) continue;
                // (the cell at (i, j, k) of a block is its entry i + dx (j + dy k): GridPaint)
                int bi = pos % known.Dx, bj = pos / known.Dx % known.Dy, bk = pos / known.Dx / known.Dy;
                var cell = (x + offsets[ti].X, y + offsets[ti].Y, z + offsets[ti].Z);
                if (!cells.TryGetValue(cell, out var c) || c.Tile != ti) continue;
                var key = (block, x - bi, y - bj, z - bk);
                if (!found.TryGetValue(key, out var list)) found[key] = list = new();
                list.Add(cell);
            }
            // (placements of a block on the same foot, one over another and touching -- a column twice as tall -- as one, from the lowest's foot
            // to the highest's top)
            var placed = found.Select(f => (f.Key.Block, f.Key.X, Y0: f.Key.Y, f.Key.Z, Y1: f.Key.Y + Blocks[f.Key.Block].Dy, Cells: f.Value))
                .OrderBy(f => f.Block).ThenBy(f => f.X).ThenBy(f => f.Z).ThenBy(f => f.Y0).ToList();
            for (var k = 0; k < placed.Count; k++)
            {
                var (block, x, y0, z, y1, list) = placed[k];
                while (k + 1 < placed.Count && placed[k + 1] is var n && (n.Block, n.X, n.Z) == (block, x, z) && n.Y0 <= y1)
                {
                    y1 = Math.Max(y1, n.Y1); list = list.Concat(n.Cells).ToList(); k++;
                }
                var (kind, dx, _, dz) = Blocks[block];
                var image = kind is Kind.Portrait or Kind.RoundWindow ? Picture(backend, library, block) : null;
                pieces.Add(new Piece(kind, block, ti, (x + offsets[ti].X, y0 + offsets[ti].Y, z + offsets[ti].Z), (dx, y1 - y0, dz), list, image));
            }
        }
        return pieces;
    }

    // A block's picture: its bricks drawn alone at their cells of the block, as the joined map draws a grid.
    private static Lba1SceneImage? Picture(Grids.Lba2GridBackend backend, byte[] library, int block)
    {
        var at = (int)BinaryPrimitives.ReadUInt32LittleEndian(library.AsSpan((block - 1) * 4));
        int dx = library[at], dy = library[at + 1], dz = library[at + 2];
        var placements = new List<Lba1Placement>();
        for (var p = 0; p < dx * dy * dz; p++)
        {
            var number = BinaryPrimitives.ReadUInt16LittleEndian(library.AsSpan(at + 3 + p * 4 + 2));
            if (number != 0) placements.Add(new Lba1Placement(p % dx, p / dx % dy, p / dx / dy, number - 1));
        }
        return placements.Count == 0 ? null : Lba1GridRenderer.Render(new[] { new Lba1Tile(placements, 0, 0, 0) }, backend.Brick, backend.Palette);
    }

    // The rooms' chests: their scenes' sprite actors that picture one.
    public static List<Chest> Chests(Lba2Interiors interiors, Lba1Area area, (int X, int Y, int Z)[] offsets)
    {
        var chests = new List<Chest>();
        for (var ti = 0; ti < area.Tiles.Count; ti++)
        {
            if (interiors.LoadScene(area.Tiles[ti].Scene) is not { } scene) continue;
            foreach (var a in scene.Actors.Skip(1).Where(a => a.IsSprite))
            {
                // (a sprite's place is the middle of its foot; its size measured on its picture: the small chests 78 pixels wide and 65 high,
                // the big one 120 by 98 -- 24 pixels a cell along each of the long and short sides, 15 a layer up)
                bool alongZ = ChestsAlongZ.Contains(a.Sprite), alongX = ChestsAlongX.Contains(a.Sprite), big = a.Sprite == BigChest;
                if (!alongZ && !alongX && !big) continue;
                var (lng, sht, high) = big ? (1700.0, 860.0, 650.0) : (1130.0, 530.0, 440.0);
                chests.Add(new Chest(ti, a.Sprite, offsets[ti].X + a.X / 512.0, offsets[ti].Y + a.Y / 256.0, offsets[ti].Z + a.Z / 512.0, lng, sht, high, alongX || big));
            }
        }
        return chests;
    }

    // ---- the models ------------------------------------------------------------------------------------------------------------------

    // the colours the bricks and sprites picture (the interiors' palette; matched in Otringal's when drawn)
    private static readonly (float R, float G, float B) Gold = (190, 145, 70), DarkGold = (132, 100, 50), RedMarble = (156, 60, 36),
        Purple = (125, 104, 155), DarkPurple = (98, 82, 126), PotGold = (196, 146, 70), Soil = (60, 36, 12), Globe = (190, 245, 210), GlobeTop = (240, 250, 225),
        Cushion = (170, 62, 44), Fringe = (140, 40, 34), Wood = (60, 36, 12), Pewter = (96, 96, 100), Wax = (215, 215, 205), Flame = (250, 200, 80),
        GreenMarble = (70, 98, 70), WhiteMarble = (225, 228, 228), Soot = (14, 12, 12), Iron = (44, 44, 48), Glass = (66, 92, 100), GlassLow = (92, 84, 112),
        ChestBody = (96, 92, 110), ChestLid = (118, 112, 132), ChestBand = (44, 30, 20), PlinthGreen = (64, 88, 64);

    // Everything the rooms' furniture adds: each room's faces (BrickBuilding faces, in the island's world) and the chests' collision boxes.
    public sealed record Made(Dictionary<int, List<BrickBuilding.Face>> Faces, List<(double X0, double Y0, double Z0, double X1, double Y1, double Z1)> Boxes, List<string> Log);

    // The pieces' and chests' models. `place`: a point of the palace in cells (fractions of one) and layers to the island's world;
    // `colour`: a colour seen from a way (a unit normal in the world) as a colour of the island's palette; `faces`: which way a piece
    // looks (a unit along x or z: away from the wall it stands at, into its room); `wall`: the wallpaper's colour at a layer.
    // (`portals`: fireplaces made teleporters -- hollow, a glow at the back -- PalaceIsland's PortalFrom and PortalTo)
    public static Made Build(List<Piece> pieces, List<Chest> chests, Func<double, double, double, Vector3> place, Func<(float R, float G, float B), Vector3, int> colour,
        Func<Piece, (int Nx, int Nz)> faces, Func<int, (float R, float G, float B)> wall, IReadOnlySet<Piece>? portals = null)
    {
        var made = new Dictionary<int, List<BrickBuilding.Face>>();
        var boxes = new List<(double, double, double, double, double, double)>();
        var counts = new Dictionary<(string One, string Many), int>();
        void Count(string one, string many) => counts[(one, many)] = counts.GetValueOrDefault((one, many)) + 1;
        foreach (var piece in pieces)
        {
            var list = made.TryGetValue(piece.Tile, out var l) ? l : made[piece.Tile] = new();
            var (ox, oy, oz) = piece.Origin; var (dx, dy, dz) = piece.Size;
            var (nx, nz) = faces(piece);
            // (the model's frame: its front along +x, about the middle of the block's foot; turned to the way the piece looks)
            var kit = new Kit(list, place, colour, ox + dx / 2.0, oy, oz + dz / 2.0, nx, nz);
            // (its depth -- along the way it looks -- and width, in room units)
            double depth = (nx != 0 ? dx : dz) * 512.0, width = (nx != 0 ? dz : dx) * 512.0, high = dy * 256.0;
            switch (piece.Kind)
            {
                case Kind.Column: Column(kit, high); Count("column", "columns"); break;
                case Kind.Plant: Plant(kit, 0); Count("potted plant", "potted plants"); break;
                case Kind.PlinthPlant:
                    kit.Box(new(-480, 0, -480), new(480, 230, 480), PlinthGreen, PlinthGreen);
                    kit.Box(new(-500, 230, -500), new(500, 256, 500), DarkGold, Gold);
                    Plant(kit, 256); Count("plant on a plinth", "plants on plinths"); break;
                case Kind.Lamp: Lamp(kit); Count("wall lamp", "wall lamps"); break;
                case Kind.Bench:
                    // (its long side along the block's longer one: across the way it "looks", a bench has no front)
                    var benchKit = new Kit(list, place, colour, ox + dx / 2.0, oy, oz + dz / 2.0, dx > dz ? 0 : 1, dx > dz ? 1 : 0);
                    Bench(benchKit, Math.Max(dx, dz) * 512.0, Math.Min(dx, dz) * 512.0); Count("bench", "benches"); break;
                case Kind.Candelabra:
                    var armsKit = new Kit(list, place, colour, ox + dx / 2.0, oy, oz + dz / 2.0, dx > dz ? 0 : 1, dx > dz ? 1 : 0);
                    Candelabra(armsKit); Count("candelabra", "candelabras"); break;
                case Kind.Candle: Candle(kit); Count("candlestick", "candlesticks"); break;
                case Kind.Fireplace:
                    if (portals?.Contains(piece) == true) { Portal(kit, depth, width, high); Count("fireplace (a secret teleporter)", "fireplaces (secret teleporters)"); }
                    else { Fireplace(kit, depth, width, high); Count("fireplace", "fireplaces"); }
                    break;
                case Kind.Portrait:
                    if (piece.Image is not { } portrait) break;
                    Picture(list, portrait, piece, nx, nz, place, colour, wall);
                    Count("portrait", "portraits");
                    break;
                case Kind.RoundWindow:
                    if (piece.Image is not { } round) break;
                    // (a window: its picture on both faces of the wall -- outside, the window alone, not the frame and leaves round it)
                    Picture(list, round, piece, nx, nz, place, colour, wall);
                    Picture(list, round, piece, -nx, -nz, place, colour, wall, trim: 0.17);
                    Count("round window (inside and out)", "round windows (inside and out)");
                    break;
                case Kind.Window:
                    Window(kit, depth, width, high, inside: true);
                    // (and on the wall's other face, looking out: the same window seen from outside)
                    Window(new Kit(list, place, colour, ox + dx / 2.0, oy, oz + dz / 2.0, -nx, -nz), depth, width, high, inside: false);
                    Count("window (inside and out)", "windows (inside and out)"); break;
            }
        }
        foreach (var chest in chests)
        {
            var list = made.TryGetValue(chest.Tile, out var l) ? l : made[chest.Tile] = new();
            var kit = new Kit(list, place, colour, chest.X, chest.Y, chest.Z, chest.AlongX ? 0 : 1, chest.AlongX ? 1 : 0);
            ChestModel(kit, chest.Long, chest.Short, chest.High);
            // (in the way, as the actor is)
            double hx = (chest.AlongX ? chest.Long : chest.Short) / 1024.0, hz = (chest.AlongX ? chest.Short : chest.Long) / 1024.0;
            var lo = place(chest.X - hx, chest.Y, chest.Z - hz); var hi = place(chest.X + hx, chest.Y + chest.High / 256.0, chest.Z + hz);
            boxes.Add((lo.X, lo.Y, lo.Z, hi.X, hi.Y, hi.Z));
            Count(chest.Sprite == BigChest ? "big chest" : "chest", chest.Sprite == BigChest ? "big chests" : "chests");
        }
        var log = new List<string> { $"the rooms' furniture: {string.Join(", ", counts.OrderByDescending(c => c.Value).Select(c => $"{c.Value} {(c.Value == 1 ? c.Key.One : c.Key.Many)}"))}" };
        return new Made(made, boxes, log);
    }

    // ---- the upstairs' furniture (PalaceUpstairs: the user, 2026-10-15, "maybe add a couple of bedrooms, a throne room, and any other
    // rooms you can think of that would fit with a palace theme") -- no blocks of the game's to follow: each piece made here, in the rooms'
    // own colours (the gold, the reds, the marbles, the wood of the ground floor's furniture).
    public enum Up
    {
        Bed, GrandBed, Wardrobe, Bookshelf, ReadingTable, Chair, Globe, Piano, Harp, MusicStand, Bathtub, DressingTable, Throne, Column,
        Candelabra, Chandelier, GoldPile, Pedestal, Chest, Armour, WeaponRack, Altar, Pew, Candle, Plant, Fireplace, BanquetTable, Bench, StairColumn,
        // (the dungeon under the palace -- PalaceIsland's: a secret way down through room 159's fireplace; its captives hung from its ceiling;
        // then made a medieval one: its stone pillars and arches, braziers, the rack, an iron maiden, a pillory, a whipping post, a wheel,
        // chains and hooks, straw in the cages)
        Cage, WhipRack, Shackles, Torch, Cot, Barrel, Hanging, Suspended,
        Pilaster, Rib, Brazier, Rack, IronMaiden, Pillory, WhippingPost, Wheel, Chain, Straw,
    }

    // A piece upstairs: its kind, room (tile), the middle of its foot (palace cells, fractions of one; the floor's layer), the way it looks
    // (its front: away from the wall it stands at), its depth along that way and width across it (cells) and height (layers: its box, if
    // `Solid`; a chandelier hangs from `High` up).
    public sealed record UpPiece(Up Kind, int Tile, double X, double Y, double Z, int Nx, int Nz, double Depth, double Width, double High, bool Solid = true);

    private static readonly (float R, float G, float B) Linen = (226, 220, 200), Royal = (150, 30, 36), RoyalDark = (110, 22, 30), DarkWood = (74, 44, 24),
        PianoBlack = (28, 24, 26), Ivory = (232, 226, 210), Steel = (150, 156, 164), SteelDark = (100, 104, 112), Water = (70, 120, 170), Enamel = (236, 236, 232),
        Ruby = (200, 30, 40), Emerald = (40, 160, 80), Sapphire = (40, 70, 190), Velvet = (96, 40, 120);
    private static readonly (float R, float G, float B)[] Books = { (140, 40, 30), (40, 70, 120), (50, 100, 50), (170, 130, 60), (90, 50, 90), (120, 80, 40) };

    // The upstairs pieces' models (each room's faces) and their boxes. `place` and `colour` as Build's; `captives`: the dungeon's (Captive),
    // in turns, `palette` an index of the island's palette as its colour.
    public static Made Upstairs(List<UpPiece> pieces, Func<double, double, double, Vector3> place, Func<(float R, float G, float B), Vector3, int> colour,
        Body? armour = null, IReadOnlyList<Body>? captives = null, Func<int, (float R, float G, float B)>? palette = null)
    {
        var made = new Dictionary<int, List<BrickBuilding.Face>>();
        var boxes = new List<(double, double, double, double, double, double)>();
        var counts = new Dictionary<Up, int>();
        foreach (var p in pieces)
        {
            var list = made.TryGetValue(p.Tile, out var l) ? l : made[p.Tile] = new();
            var kit = new Kit(list, place, colour, p.X, p.Y, p.Z, p.Nx, p.Nz);
            double d = p.Depth * 512, w = p.Width * 512, h = p.High * 256;
            switch (p.Kind)
            {
                case Up.Bed: Bed(kit, d, w, Velvet, false); break;
                case Up.GrandBed: Bed(kit, d, w, Royal, true); break;
                case Up.Wardrobe: Wardrobe(kit, d, w, h); break;
                case Up.Bookshelf: Bookshelf(kit, d, w, h); break;
                case Up.ReadingTable: Table(kit, d, w, 300, cloth: false); break;
                case Up.BanquetTable: Table(kit, d, w, 300, cloth: true); break;
                case Up.Chair: Chair(kit); break;
                case Up.Globe: GlobeModel(kit); break;
                case Up.Piano: Piano(kit, d, w); break;
                case Up.Harp: Harp(kit); break;
                case Up.MusicStand: MusicStand(kit); break;
                case Up.Bathtub: Bathtub(kit, d, w); break;
                case Up.DressingTable: DressingTable(kit, d, w); break;
                case Up.Throne: Throne(kit, d, w); break;
                case Up.Column: Column(kit, h); break;
                case Up.StairColumn: StairColumn(kit, h, Math.Min(d, w) / 2); break;
                case Up.Candelabra: Candelabra(kit); break;
                case Up.Chandelier: Chandelier(kit, h); break;
                case Up.GoldPile: GoldPile(kit, w); break;
                case Up.Pedestal: Pedestal(kit, counts.GetValueOrDefault(Up.Pedestal)); break;
                case Up.Chest: ChestModel(kit, w, d, 440); break;
                case Up.Armour: Armour(kit, armour); break;
                case Up.WeaponRack: WeaponRack(kit, w); break;
                case Up.Altar: Altar(kit, d, w); break;
                case Up.Pew: Pew(kit, d, w); break;
                case Up.Bench: Bench(kit, w, d); break;
                case Up.Candle: Candle(kit); break;
                case Up.Plant: Plant(kit, 0); break;
                case Up.Fireplace: Fireplace(kit, d, w, h); break;
                case Up.Cage: Cage(kit, d, w, h); break;
                case Up.WhipRack: WhipRack(kit, w); break;
                case Up.Shackles: Shackles(kit); break;
                case Up.Torch: Torch(kit); break;
                case Up.Cot: Cot(kit, d, w); break;
                case Up.Barrel: Barrel(kit); break;
                case Up.Hanging or Up.Suspended:
                    // (the bodies in turns, each hung both ways)
                    var held = counts.GetValueOrDefault(p.Kind) + (p.Kind == Up.Suspended ? 1 : 0);
                    if (captives is { Count: > 0 } && palette is not null) Captive(kit, captives[held % captives.Count], p.Kind == Up.Suspended, h, palette);
                    break;
                case Up.Pilaster: Pilaster(kit, d, w, h); break;
                case Up.Rib: Rib(kit, d, w, h); break;
                case Up.Brazier: Brazier(kit, counts.GetValueOrDefault(Up.Brazier)); break;
                case Up.Rack: Rack(kit, d, w); break;
                case Up.IronMaiden: IronMaiden(kit); break;
                case Up.Pillory: Pillory(kit); break;
                case Up.WhippingPost: WhippingPost(kit); break;
                case Up.Wheel: Wheel(kit, h); break;
                case Up.Chain: Chain(kit, h, counts.GetValueOrDefault(Up.Chain)); break;
                case Up.Straw: StrawBed(kit, d, w, counts.GetValueOrDefault(Up.Straw)); break;
            }
            counts[p.Kind] = counts.GetValueOrDefault(p.Kind) + 1;
            if (!p.Solid) continue;
            // (its box: its foot, turned as it looks, up to its height -- a stairwell's column's much narrower than it is: a car on the ramp by
            // its inner rail, 5.5 cells from the column's middle, its box 2.8 either way along x and z -- a corner of it as near as 1.5 cells
            // where the ramp runs on a slant -- must not touch it: a cell either way)
            var shrink = p.Kind == Up.StairColumn ? 1.0 / 3.5 : 1.0;
            double hx = (p.Nx != 0 ? p.Depth : p.Width) / 2 * shrink, hz = (p.Nx != 0 ? p.Width : p.Depth) / 2 * shrink;
            var lo = place(p.X - hx, p.Y, p.Z - hz); var hi = place(p.X + hx, p.Y + p.High, p.Z + hz);
            boxes.Add((lo.X, lo.Y, lo.Z, hi.X, hi.Y, hi.Z));
        }
        var log = new List<string> { $"the upstairs' furniture: {string.Join(", ", counts.OrderByDescending(c => c.Value).Select(c => $"{c.Value} {c.Key}"))}" };
        return new Made(made, boxes, log);
    }

    private static void Bed(Kit k, double length, double width, (float R, float G, float B) cover, bool grand)
    {
        // a four-poster: its head against the wall (-x), a wooden frame, the mattress, the cover over its foot two thirds, two pillows, a
        // headboard with a gold crest, the posts and the canopy over it, a gold valance round the canopy's three open sides
        float L = (float)length / 2, W = (float)width / 2, post = grand ? 80 : 65, top = grand ? 1750 : 1550;
        k.Box(new(-L, 0, -W), new(L, 150, W), DarkWood, Wood, bottom: false);
        k.Box(new(-L + 30, 150, -W + 30), new(L - 30, 270, W - 30), Linen, Linen);
        k.Box(new(-L + (float)length * 0.3f, 268, -W + 22), new(L - 22, 300, W - 22), cover, cover);
        foreach (var s in new[] { -1, 1 })
            k.Box(new(-L + 50, 270, s > 0 ? 25 : -W + 60), new(-L + 50 + (float)length * 0.2f, 345, s > 0 ? W - 60 : -25), Linen, Linen);
        k.Box(new(-L, 0, -W), new(-L + 70, 760, W), DarkWood, DarkGold);
        k.Box(new(-L, 760, -W * 0.35f), new(-L + 70, 860, W * 0.35f), DarkGold, Gold);
        foreach (var sx in new[] { -1, 1 })
            foreach (var sz in new[] { -1, 1 })
                k.Box(new(sx < 0 ? -L : L - post, 0, sz < 0 ? -W : W - post), new(sx < 0 ? -L + post : L, top, sz < 0 ? -W + post : W), DarkWood, Gold, lid: false);
        k.Box(new(-L - 10, top, -W - 10), new(L + 10, top + 60, W + 10), cover, cover, bottom: true);
        k.Box(new(L - 10, top - 150, -W - 10), new(L + 10, top, W + 10), Gold, Gold, lid: false);
        foreach (var s in new[] { -1, 1 })
            k.Box(new(-L - 10, top - 150, s < 0 ? -W - 10 : W - 10), new(L + 10, top, s < 0 ? -W + 10 : W + 10), Gold, Gold, lid: false);
        if (grand)
            foreach (var s in new[] { -1, 1 })
                // (drapes tied back at the foot's posts)
                k.Quad(new(L - 40, top, s * (W - 40)), new(L - 40, top, s * (W - 260)), new(L - 40, 250, s * (W - 80)), new(L - 40, 250, s * (W - 10)), cover, Vector3.UnitX);
    }

    private static void Wardrobe(Kit k, double depth, double width, double high)
    {
        // dark wood against the wall, its two doors' raised panels on the front (+x), gold handles, a cornice
        float d = (float)depth / 2, w = (float)width / 2, h = (float)high;
        k.Box(new(-d, 0, -w), new(d, h - 60, w), DarkWood, DarkWood);
        k.Box(new(-d - 10, h - 60, -w - 25), new(d + 25, h, w + 25), DarkGold, Gold, bottom: true);
        foreach (var s in new[] { -1, 1 })
        {
            k.Box(new(d, 120, s < 0 ? -w + 50 : 20), new(d + 18, h - 180, s < 0 ? -20 : w - 50), Wood, Wood);
            k.Box(new(d + 18, h * 0.45f, s * 70 - 18), new(d + 45, h * 0.45f + 110, s * 70 + 18), DarkGold, Gold);
        }
    }

    private static void Bookshelf(Kit k, double depth, double width, double high)
    {
        // a tall case against the wall, its shelves full: rows of spines on its front (+x), in turns of colours, each shelf a board
        float d = (float)depth / 2, w = (float)width / 2, h = (float)high;
        k.Box(new(-d, 0, -w), new(d, h, w), DarkWood, DarkGold);
        const int Rows = 5;
        float row = (h - 120) / Rows;
        var books = 0;
        for (var r = 0; r < Rows; r++)
        {
            float y0 = 70 + r * row;
            k.Box(new(d, y0 - 25, -w + 20), new(d + 20, y0, w - 20), Wood, Wood, lid: true);
            // (the spines: some leaning taller than others)
            for (float z = -w + 40; z < w - 60; z += 70)
            {
                var c = Books[(books++ * 7 + r * 3) % Books.Length];
                var tall = row * (0.62f + 0.18f * ((books * 5) % 3) / 2f);
                k.Quad(new(d + 6, y0, z), new(d + 6, y0, z + 62), new(d + 6, y0 + tall, z + 62), new(d + 6, y0 + tall, z), c, Vector3.UnitX);
            }
        }
    }

    private static void Table(Kit k, double length, double width, double high, bool cloth)
    {
        // a wooden top on four legs; a white cloth over a banquet's, hanging over its edges
        float l = (float)length / 2, w = (float)width / 2, h = (float)high;
        k.Box(new(-l, h - 40, -w), new(l, h, w), Wood, cloth ? Linen : Wood, bottom: true);
        foreach (var sx in new[] { -1, 1 })
            foreach (var sz in new[] { -1, 1 })
                k.Box(new(sx * (l - 60) - 30, 0, sz * (w - 60) - 30), new(sx * (l - 60) + 30, h - 40, sz * (w - 60) + 30), DarkWood, DarkWood, lid: false);
        if (!cloth) return;
        k.Box(new(-l - 30, h - 120, -w - 30), new(l + 30, h + 6, w + 30), Linen, Linen, bottom: false);
        // (a gold runner down its middle, and plates and goblets along it)
        k.Quad(new(-l + 60, h + 8, -w * 0.2f), new(l - 60, h + 8, -w * 0.2f), new(l - 60, h + 8, w * 0.2f), new(-l + 60, h + 8, w * 0.2f), Gold, Vector3.UnitY);
        for (float x = -l + 180; x < l - 120; x += 360)
            foreach (var s in new[] { -1, 1 })
            {
                k.Lathe(new[] { (0.0, h + 7.0), (70.0, h + 9.0), (80.0, h + 20.0) }, 8, _ => Ivory, top: false, bottom: false, cx: x, cz: s * w * 0.62);
                k.Lathe(new[] { (25.0, h + 6.0), (8.0, h + 30.0), (8.0, h + 80.0), (32.0, h + 130.0) }, 6, _ => Gold, top: true, bottom: false, cx: x + 110, cz: s * w * 0.45);
            }
    }

    private static void Chair(Kit k)
    {
        // a seat facing +x, its back behind it (-x), red cushion, dark wood, a gold top rail
        k.Box(new(-120, 170, -120), new(120, 210, 120), DarkWood, Cushion, bottom: true);
        k.Box(new(-130, 210, -115), new(-95, 560, 115), DarkWood, DarkWood);
        k.Box(new(-128, 560, -125), new(-92, 600, 125), DarkGold, Gold);
        k.Box(new(-95, 260, -95), new(-90, 520, 95), Cushion, Cushion, lid: false);
        foreach (var sx in new[] { -1, 1 })
            foreach (var sz in new[] { -1, 1 })
                k.Box(new(sx * 95 - 22, 0, sz * 95 - 22), new(sx * 95 + 22, 170, sz * 95 + 22), DarkWood, DarkWood, lid: false);
    }

    private static void GlobeModel(Kit k)
    {
        // a globe on a wooden stand: its seas and lands in bands, a gold ring round it
        k.Lathe(new[] { (190.0, 0.0), (190.0, 40.0), (60.0, 80.0), (40.0, 420.0), (90.0, 460.0) }, 8, _ => DarkWood, top: false, bottom: true);
        k.Lathe(new[] { (0.0, 470.0), (130.0, 510.0), (200.0, 600.0), (215.0, 700.0), (200.0, 800.0), (130.0, 890.0), (0.0, 930.0) }, 10,
            band => band % 2 == 0 ? Water : Emerald, top: false, bottom: false);
        k.Lathe(new[] { (235.0, 690.0), (235.0, 710.0) }, 12, _ => Gold, top: false, bottom: false);
    }

    private static void Piano(Kit k, double length, double width)
    {
        // a grand piano: its keyboard at the front (+x), its body tapering back to the curved tail (-x), black, on three legs; its lid propped
        // open on the bass side; a bench before the keys
        float L = (float)length / 2, W = (float)width / 2;
        (float X, float Z)[] outline = { (L, -W), (L, W), (-L * 0.15f, W), (-L * 0.7f, W * 0.8f), (-L, W * 0.35f), (-L * 0.95f, -W * 0.1f), (-L * 0.55f, -W * 0.45f), (L * 0.05f, -W) };
        const float y0 = 330, y1 = 470;
        var mid = new Vector3(0, 0, 0);
        for (var i = 0; i < outline.Length; i++)
        {
            var (ax, az) = outline[i]; var (bx, bz) = outline[(i + 1) % outline.Length];
            var n = new Vector3(bz - az, 0, -(bx - ax));
            k.Quad(new(ax, y0, az), new(bx, y0, bz), new(bx, y1, bz), new(ax, y1, az), PianoBlack, n.LengthSquared() > 0 ? Vector3.Normalize(n) : Vector3.UnitX);
            k.Tri(new(mid.X, y1, mid.Z), new(ax, y1, az), new(bx, y1, bz), PianoBlack, Vector3.UnitY);
            k.Tri(new(mid.X, y0, mid.Z), new(ax, y0, az), new(bx, y0, bz), PianoBlack, -Vector3.UnitY);
        }
        // (the lid: hinged on the bass side, its far edge propped up)
        k.Quad(new(L * 0.9f, y1 + 5, W), new(-L * 0.6f, y1 + 5, W), new(-L * 0.6f, y1 + 520, W * 0.1f), new(L * 0.9f, y1 + 300, -W * 0.2f), PianoBlack, new Vector3(0, 0.5f, -0.86f));
        k.Box(new(-L * 0.6f, y1, W * 0.05f), new(-L * 0.6f + 20, y1 + 500, W * 0.05f + 20), DarkGold, DarkGold, lid: false);
        // (the keys: a white strip, black keys over it in twos and threes)
        k.Box(new(L, 380, -W + 40), new(L + 130, 420, W - 40), PianoBlack, Ivory);
        for (int i = 0, n = (int)((2 * W - 120) / 46); i < n; i++)
            if (i % 7 is not (2 or 6))
                k.Box(new(L + 50, 420, -W + 60 + i * 46), new(L + 130, 445, -W + 60 + i * 46 + 26), PianoBlack, PianoBlack, lid: true);
        foreach (var (x, z) in new[] { (L - 80, -W + 80), (L - 80, W - 80), (-L * 0.75f, W * 0.4f) })
            k.Box(new(x - 40, 0, z - 40), new(x + 40, y0, z + 40), PianoBlack, PianoBlack, lid: false);
        // (the bench)
        k.Box(new(L + 330, 0, -260), new(L + 470, 230, 260), PianoBlack, Cushion);
    }

    private static void Harp(Kit k)
    {
        // a gold harp on its foot: the pillar, the neck curving over, the soundbox sloping back down; its strings
        k.Box(new(-160, 0, -220), new(160, 70, 220), DarkGold, Gold);
        k.Box(new(-40, 70, -210), new(40, 1250, -150), Gold, Gold);
        var a = new Vector3(0, 1250, -180); var b = new Vector3(0, 1120, 230); var c = new Vector3(0, 90, 120);
        void Bar(Vector3 p, Vector3 q, float t, (float, float, float) col)
        {
            var side = new Vector3(t, 0, 0);
            var up = Vector3.Normalize(Vector3.Cross(q - p, side)) * t;
            k.Quad(p - side, q - side, q + side, p + side, col, Vector3.Normalize(up));
            k.Quad(p - side + up * 0, p + side, q + side, q - side, col, -Vector3.Normalize(up));
            k.Quad(p - side, q - side, q - side + up, p - side + up, col, -Vector3.UnitX);
            k.Quad(p + side, q + side, q + side + up, p + side + up, col, Vector3.UnitX);
        }
        Bar(a, b, 40, Gold);
        Bar(c, b, 60, DarkGold);
        for (var i = 1; i < 8; i++)
        {
            var t = i / 8f;
            var top = a + (b - a) * t; var bottom = new Vector3(0, 90 + (1120 - 90) * t, -150 + (120 + 150) * t * 0.0f + 270 * t);
            k.Quad(new(3, bottom.Y, bottom.Z - 4), new(3, top.Y, top.Z - 4), new(3, top.Y, top.Z + 4), new(3, bottom.Y, bottom.Z + 4), Linen, Vector3.UnitX);
        }
    }

    private static void MusicStand(Kit k)
    {
        k.Lathe(new[] { (120.0, 0.0), (120.0, 20.0), (20.0, 40.0), (15.0, 560.0) }, 6, _ => Pewter, top: false, bottom: true);
        k.Quad(new(-30, 520, -180), new(-30, 520, 180), new(-110, 820, 180), new(-110, 820, -180), Pewter, Vector3.Normalize(new Vector3(1, 0.27f, 0)));
        k.Quad(new(-25, 525, -170), new(-25, 525, 170), new(-105, 815, 170), new(-105, 815, -170), Linen, Vector3.Normalize(new Vector3(1, 0.27f, 0)));
    }

    private static void Bathtub(Kit k, double depth, double width)
    {
        // a white bath on four gold feet, its long side along z: its rim, the water in it, gold taps at the wall's end
        float d = (float)depth / 2, w = (float)width / 2, rim = 430;
        k.Box(new(-d, 90, -w), new(d, rim - 40, w), Enamel, Enamel, lid: false, bottom: true);
        k.Box(new(-d - 20, rim - 40, -w - 20), new(d + 20, rim, -w + 70), Enamel, Enamel);
        k.Box(new(-d - 20, rim - 40, w - 70), new(d + 20, rim, w + 20), Enamel, Enamel);
        k.Box(new(-d - 20, rim - 40, -w + 70), new(-d + 70, rim, w - 70), Enamel, Enamel);
        k.Box(new(d - 70, rim - 40, -w + 70), new(d + 20, rim, w - 70), Enamel, Enamel);
        k.Quad(new(-d + 70, rim - 90, -w + 70), new(d - 70, rim - 90, -w + 70), new(d - 70, rim - 90, w - 70), new(-d + 70, rim - 90, w - 70), Water, Vector3.UnitY);
        foreach (var sx in new[] { -1, 1 })
            foreach (var sz in new[] { -1, 1 })
                k.Lathe(new[] { (55.0, 0.0), (35.0, 50.0), (45.0, 95.0) }, 6, _ => Gold, top: false, bottom: false, cx: sx * (d - 90), cz: sz * (w - 110));
        k.Box(new(-50, rim, -w - 20), new(50, rim + 160, -w + 30), DarkGold, Gold);
    }

    private static void DressingTable(Kit k, double depth, double width)
    {
        // a small table against the wall, a tall mirror over it in a gold frame, a stool before it
        float d = (float)depth / 2, w = (float)width / 2;
        Table(k, depth, width, 330, cloth: false);
        k.Box(new(-d, 330, -w * 0.6f), new(-d + 50, 1300, w * 0.6f), DarkGold, Gold);
        k.Quad(new(-d + 52, 380, -w * 0.5f), new(-d + 52, 380, w * 0.5f), new(-d + 52, 1250, w * 0.5f), new(-d + 52, 1250, -w * 0.5f), Glass, Vector3.UnitX);
        k.Lathe(new[] { (0.0, 0.0), (90.0, 10.0), (60.0, 200.0), (130.0, 230.0), (130.0, 260.0), (0.0, 265.0) }, 8, b => b >= 3 ? Cushion : DarkWood, top: false, bottom: false, cx: d + 260);
    }

    private static void Throne(Kit k, double depth, double width)
    {
        // the dais: three steps of red carpet edged in gold, deeper at the back; on its top the throne, its back to the wall (-x): a gold
        // frame, a red seat and back, armrests, a crest of gold over it and gold balls on its posts
        float D = (float)depth / 2, W = (float)width / 2;
        for (var s = 0; s < 3; s++)
        {
            float x0 = -D, x1 = D - s * (float)depth * 0.18f, z = W - s * (float)width * 0.1f, y0 = s * 90, y1 = (s + 1) * 90;
            k.Box(new(x0, y0, -z), new(x1, y1, z), Gold, Royal);
            k.Quad(new(x1 + 3, y0 + 20, -z), new(x1 + 3, y0 + 20, z), new(x1 + 3, y1 - 20, z), new(x1 + 3, y1 - 20, -z), RoyalDark, Vector3.UnitX);
        }
        float b = 270, bx = -D + 60;
        k.Box(new(bx, b, -330), new(bx + 620, b + 210, 330), Gold, Royal);
        k.Box(new(bx, b + 210, -330), new(bx + 140, b + 1500, 330), DarkGold, Gold);
        k.Box(new(bx + 140, b + 300, -260), new(bx + 150, b + 1400, 260), Royal, Royal, lid: false);
        foreach (var s in new[] { -1, 1 })
        {
            k.Box(new(bx + 140, b + 210, s < 0 ? -330 : 270), new(bx + 620, b + 470, s < 0 ? -270 : 330), DarkGold, Gold);
            k.Lathe(new[] { (0.0, b + 1500.0), (70.0, b + 1540.0), (75.0, b + 1600.0), (0.0, b + 1660.0) }, 8, _ => Gold, top: false, bottom: false, cx: bx + 70, cz: s * 290);
        }
        k.Quad(new(bx + 141, b + 1500, -230), new(bx + 141, b + 1500, 230), new(bx + 141, b + 1820, 0), new(bx + 141, b + 1820, 0), Gold, Vector3.UnitX);
        k.Quad(new(bx - 1, b + 1500, 230), new(bx - 1, b + 1500, -230), new(bx - 1, b + 1820, 0), new(bx - 1, b + 1820, 0), Gold, -Vector3.UnitX);
    }

    private static void Chandelier(Kit k, double high)
    {
        // hung from the ceiling (`high` up): a gold chain, a gold dish and ring, eight candles round it
        float y = (float)high - 900;
        k.Box(new(-15, y + 260, -15), new(15, (float)high, 15), DarkGold, DarkGold, lid: false);
        k.Lathe(new[] { (0.0, y + 40.0), (110.0, y + 70.0), (380.0, y + 150.0), (420.0, y + 200.0), (380.0, y + 210.0), (0.0, y + 230.0) }, 12,
            band => band % 2 == 0 ? Gold : DarkGold, top: false, bottom: false);
        for (var i = 0; i < 8; i++)
        {
            var a = i * Math.PI / 4;
            float x = (float)(Math.Cos(a) * 340), z = (float)(Math.Sin(a) * 340);
            k.Box(new(x - 20, y + 210, z - 20), new(x + 20, y + 360, z + 20), Wax, Wax);
            k.Leaf(new Vector3(x, y + 365, z), new Vector3(x, y + 440, z), new Vector3(18, 0, 0), Flame);
        }
    }

    private static void GoldPile(Kit k, double width)
    {
        // a heap of gold: stepped, coins on top, a goblet in it
        float w = (float)width / 2;
        k.Box(new(-w, 0, -w), new(w, 110, w), DarkGold, Gold);
        k.Box(new(-w * 0.7f, 110, -w * 0.65f), new(w * 0.65f, 220, w * 0.7f), DarkGold, Gold);
        k.Box(new(-w * 0.4f, 220, -w * 0.35f), new(w * 0.35f, 310, w * 0.4f), DarkGold, Gold);
        k.Lathe(new[] { (40.0, 310.0), (14.0, 350.0), (14.0, 420.0), (60.0, 520.0) }, 6, _ => Gold, top: true, bottom: false, cx: w * 0.1, cz: -w * 0.1);
    }

    private static void Pedestal(Kit k, int which)
    {
        // a white marble pedestal, a red cushion on it, a gem: ruby, emerald, sapphire by turns
        k.Lathe(new[] { (170.0, 0.0), (170.0, 60.0), (110.0, 90.0), (90.0, 480.0), (160.0, 520.0), (160.0, 560.0) }, 8, _ => WhiteMarble, top: true, bottom: false);
        k.Box(new(-110, 560, -110), new(110, 610, 110), Royal, Royal);
        var gem = new[] { Ruby, Emerald, Sapphire }[which % 3];
        k.Lathe(new[] { (0.0, 610.0), (110.0, 690.0), (80.0, 740.0), (0.0, 760.0) }, 8, _ => gem, top: false, bottom: false);
    }

    // A suit of armour in the shape of one of the Emperor's Sup soldiers (`model`: the game's own body, BODY.HQR SupSoldier, as it stands),
    // in steel on a plinth, facing out, a spear beside it (the user, 2026-10-16: "Improve suits of armour, perhaps we should base these on
    // the Sup actor"; they were boxes: legs, body, arms, a helmet). Without the body (no BODY.HQR), the boxes.
    public const int SupSoldier = 261;
    private const double ArmourHigh = 1260;     // the suit's height over the plinth (model units: 256 a layer)
    private static void Armour(Kit k, Body? model)
    {
        if (model is null || model.Vertices.Count == 0) { ArmourBoxes(k); return; }
        k.Box(new(-170, 0, -170), new(170, 90, 170), DarkWood, DarkWood);
        // (the body as modelled, its rest pose: it looks along +z -- turned to look along +x, the way the piece looks)
        var world = model.World();
        float minY = world.Min(p => p.Y), maxY = world.Max(p => p.Y);
        float cx = (world.Min(p => p.X) + world.Max(p => p.X)) / 2, cz = (world.Min(p => p.Z) + world.Max(p => p.Z)) / 2;
        var s = (float)(ArmourHigh / Math.Max(1, maxY - minY));
        Vector3 M(Vector3 p) => new((p.Z - cz) * s, 90 + (p.Y - minY) * s, -(p.X - cx) * s);
        var at = world.Select(M).ToArray();
        // (its colours steel: lighter where the body's were light -- a colour's place in its ramp of sixteen -- and shaded as it faces)
        (float R, float G, float B) Metal(int colour)
        {
            var t = (colour & 15) / 15f;
            return (SteelDark.R + (Steel.R - SteelDark.R) * t, SteelDark.G + (Steel.G - SteelDark.G) * t, SteelDark.B + (Steel.B - SteelDark.B) * t);
        }
        foreach (var f in model.Faces)
        {
            if (f.Points.Length < 3) continue;
            var pts = f.Points.Select(i => at[i]).ToArray();
            var n = Vector3.Zero;
            for (var i = 0; i < pts.Length; i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Length]; n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y)); }
            if (n.LengthSquared() < 1e-6f) continue;
            n = Vector3.Normalize(n);
            for (var i = 1; i + 1 < pts.Length; i++) k.Tri(pts[0], pts[i], pts[i + 1], Metal(f.Colour), n);
        }
        foreach (var sphere in model.Spheres)
        {
            var c = at[sphere.Point];
            var r = sphere.Radius * s;
            k.Lathe(new[] { (0.0, (double)c.Y - r), (r * 0.7, c.Y - r * 0.7), ((double)r, (double)c.Y), (r * 0.7, c.Y + r * 0.7), (0.0, (double)c.Y + r) }, 8, _ => Metal(sphere.Colour),
                top: false, bottom: false, cx: c.X, cz: c.Z);
        }
        // (its spear, held up beside it)
        k.Box(new(40, 0, 230), new(80, 1650, 270), Wood, Wood);
        k.Leaf(new Vector3(60, 1650, 250), new Vector3(60, 1830, 250), new Vector3(0, 0, 45), Steel);
    }

    private static void ArmourBoxes(Kit k)
    {
        // a suit of armour on a plinth, facing out (+x): legs, body, arms, helmet with a red plume, a spear held up beside it
        k.Box(new(-170, 0, -170), new(170, 90, 170), DarkWood, DarkWood);
        foreach (var s in new[] { -1, 1 })
        {
            k.Box(new(-55, 90, s < 0 ? -115 : 30), new(55, 620, s < 0 ? -30 : 115), SteelDark, Steel);
            k.Box(new(-60, 650, s < 0 ? -195 : 135), new(50, 1000, s < 0 ? -135 : 195), SteelDark, Steel);
        }
        k.Box(new(-80, 620, -135), new(80, 1020, 135), Steel, Steel);
        k.Box(new(-85, 1020, -80), new(85, 1230, 80), Steel, SteelDark);
        k.Box(new(84, 1100, -60), new(88, 1140, 60), PianoBlack, PianoBlack, lid: false);
        k.Box(new(-20, 1230, -25), new(30, 1340, 25), Royal, Royal);
        k.Box(new(40, 0, 210), new(80, 1650, 250), Wood, Wood);
        k.Leaf(new Vector3(60, 1650, 230), new Vector3(60, 1830, 230), new Vector3(0, 0, 45), Steel);
    }

    // ---- the dungeon's: an iron cage, whips on a rack, shackles, a torch, a prisoner's cot, a barrel (black iron, old wood, straw)
    private static readonly (float R, float G, float B) CageIron = (58, 56, 60), IronLight = (92, 90, 96), OldWood = (88, 62, 38), Straw = (176, 150, 84),
        Blanket = (96, 88, 80), Leather = (70, 40, 26), TorchFlame = (255, 170, 40), FlameCore = (255, 236, 150);

    private static void Cage(Kit k, double depth, double width, double high)
    {
        // a cage of iron bars on a plinth of stone, a frame round its foot and its top, a roof of bars (its sides the piece's boxes: the
        // prisoner inside stands free)
        float d = (float)depth / 2, w = (float)width / 2, h = (float)high;
        const float Bar = 14, Gap = 110, Rim = 30;
        k.Box(new(-d, 0, -w), new(d, 40, w), CageIron, IronLight);
        // (its top an open frame, bars across it)
        foreach (var s in new[] { -1f, 1f })
        {
            k.Box(new(-d, h - 40, s * w - Rim), new(d, h, s * w + Rim), CageIron, IronLight, bottom: true);
            k.Box(new(s * d - Rim, h - 40, -w), new(s * d + Rim, h, w), CageIron, IronLight, bottom: true);
        }
        for (var x = -d + Gap; x < d - 1; x += Gap) k.Box(new(x - Bar, h - 30, -w), new(x + Bar, h - 10, w), CageIron, CageIron, bottom: true);
        foreach (var s in new[] { -1f, 1f })
        {
            for (var z = -w; z <= w + 1; z += Gap) k.Box(new(s * d - Bar, 40, z - Bar), new(s * d + Bar, h - 40, z + Bar), CageIron, CageIron, lid: false);
            for (var x = -d + Gap; x < d - 1; x += Gap) k.Box(new(x - Bar, 40, s * w - Bar), new(x + Bar, h - 40, s * w + Bar), CageIron, CageIron, lid: false);
            // (the bars across the middle, and a lock on the front)
            k.Box(new(-d, h * 0.45f, s * w - Bar), new(d, h * 0.45f + 30, s * w + Bar), IronLight, IronLight);
            k.Box(new(s * d - Bar, h * 0.45f, -w), new(s * d + Bar, h * 0.45f + 30, w), IronLight, IronLight);
        }
        k.Box(new(d - 4, h * 0.5f - 60, -40), new(d + 30, h * 0.5f + 20, 40), IronLight, IronLight);
        k.Box(new(-d, 0, -w), new(d, 6, w), IronLight, IronLight);
    }

    private static void WhipRack(Kit k, double width)
    {
        // a board on the wall, pegs in a row, a coiled whip hanging from each: its handle, its lash in loops down the wall
        float w = (float)width / 2;
        k.Box(new(-30, 520, -w), new(0, 600, w), OldWood, OldWood);
        var n = Math.Max(2, (int)(width / 160));
        for (var i = 0; i < n; i++)
        {
            var z = -w + (i + 0.5f) * (2 * w / n);
            k.Box(new(0, 560, z - 10), new(40, 580, z + 10), CageIron, CageIron);
            k.Box(new(10, 420, z - 12), new(34, 575, z + 12), Leather, Leather);
            // (the lash: a loop of short pieces, then its end hanging)
            for (var a = 0; a < 10; a++)
            {
                var t0 = a / 10.0 * Math.PI * 2; var t1 = (a + 1) / 10.0 * Math.PI * 2;
                Vector3 P(double t) => new(26, (float)(330 + 70 * Math.Cos(t)), (float)(z + 60 * Math.Sin(t)));
                k.Box(Vector3.Min(P(t0), P(t1)) - new Vector3(6, 6, 6), Vector3.Max(P(t0), P(t1)) + new Vector3(6, 6, 6), Leather, Leather);
            }
            k.Box(new(20, 140, z - 6), new(32, 260, z + 6), Leather, Leather);
        }
    }

    private static void Shackles(Kit k)
    {
        // two iron rings on the wall, chains down from them to the cuffs
        foreach (var s in new[] { -1f, 1f })
        {
            k.Box(new(0, 640, s * 120 - 30), new(30, 700, s * 120 + 30), CageIron, IronLight);
            for (var y = 640f; y > 380; y -= 50) k.Box(new(6, y - 40, s * 120 - 12), new(24, y, s * 120 + 12), IronLight, IronLight);
            k.Box(new(0, 330, s * 120 - 34), new(36, 380, s * 120 + 34), CageIron, IronLight);
        }
    }

    private static void Torch(Kit k)
    {
        // a sconce on the wall (or a pillar's face): an iron plate, an arm out from it, a cup on its end and a wooden torch in it, its flame
        // two leaves across each other round a hot core (the dungeon made medieval, 2026-10-17: it was a bracket and a stick)
        k.Box(W(0, 1300, -130), W(60, 1800, 130), CageIron, IronLight);
        k.Box(W(60, 1520, -35), W(330, 1580, 35), CageIron, IronLight, bottom: true);
        k.Lathe(new[] { (W(70), U(1500)), (W(120), U(1620)), (W(130), U(1700)) }, 8, _ => CageIron, top: false, bottom: true, cx: W(380));
        k.Lathe(new[] { (W(45), U(1600)), (W(50), U(1980)) }, 6, _ => OldWood, top: true, bottom: false, cx: W(380));
        foreach (var side in new[] { W(0, 0, 130), W(130, 0, 0) })
            k.Leaf(W(380, 1960, 0), W(380, 2420, 0), side, TorchFlame);
        k.Leaf(W(380, 1980, 0), W(380, 2250, 0), W(0, 0, 65), FlameCore);
        k.Leaf(W(380, 1980, 0), W(380, 2250, 0), W(65, 0, 0), FlameCore);
    }

    // ---- the dungeon made medieval (the user, 2026-10-17: "Let's make our palace basement more like a medieval dungeon"): stone pillars
    // along its walls and arches across it between them, the rack, an iron maiden, a pillory, a whipping post, a wheel on the wall, braziers
    // of glowing coals with branding irons in them, chains and hooks from the arches, straw in the cages. Their sizes in the world's units:
    // W and U make them a model's (a quarter across, two fifths up: PalaceIsland.Across, Up).
    private static double W(double world) => world / PalaceIsland.Across;
    private static double U(double world) => world / PalaceIsland.Up;
    private static Vector3 W(double x, double y, double z) => new((float)(x / PalaceIsland.Across), (float)(y / PalaceIsland.Up), (float)(z / PalaceIsland.Across));
    private static readonly (float R, float G, float B) PillarStone = (118, 112, 104), PillarDark = (96, 92, 86), PillarLight = (132, 126, 118), ArchUnder = (176, 168, 156),
        Ember = (236, 96, 28), EmberDark = (150, 40, 18), EmberHot = (255, 206, 90), Ash = (70, 66, 62), Rope2 = (150, 118, 74), IronFace = (132, 128, 124),
        StrawDark = (140, 116, 62), Hole = (20, 18, 16);

    // A pillar against the wall (behind it, -x: the piece's foot's middle half its depth out from the wall), up to `high` (model units): its
    // plinth, its shaft, its capital -- an arch springs from it (Rib).
    private static void Pilaster(Kit k, double depth, double width, double high)
    {
        float d = (float)depth / 2, w = (float)width / 2, h = (float)high;
        k.Box(new(-d, 0, -w - (float)W(40)), new(d + (float)W(50), (float)U(320), w + (float)W(40)), PillarDark, PillarDark);
        k.Box(new(-d, (float)U(320), -w), new(d, h - (float)U(180), w), PillarStone, PillarStone, lid: false);
        k.Box(new(-d, h - (float)U(180), -w - (float)W(50)), new(d + (float)W(70), h, w + (float)W(50)), PillarLight, PillarLight, bottom: true);
    }

    // An arch across the dungeon (along x, `width` across it: from wall to wall -- the pillars at its ends), `depth` thick (along z): from
    // the pillars' tops (ArchSpring) rising to its crown under the ceiling (`high`), in voussoirs of two stones; filled up to the ceiling.
    public const double ArchSpring = 2300;
    private const double ArchCrownUnder = 140;
    private static void Rib(Kit k, double depth, double width, double high)
    {
        const int Stones = 16;
        float d = (float)depth / 2, w = (float)width / 2, top = (float)high;
        float spring = (float)U(ArchSpring), crown = top - (float)U(ArchCrownUnder);
        float Under(float x) { var t = x / w; return spring + (crown - spring) * (1 - t * t); }
        for (var i = 0; i < Stones; i++)
        {
            float x0 = -w + 2 * w * i / Stones, x1 = -w + 2 * w * (i + 1) / Stones, y0 = Under(x0), y1 = Under(x1);
            var stone = i % 2 == 0 ? PillarStone : PillarDark;
            var down = Vector3.Normalize(new Vector3(y1 - y0, -(x1 - x0), 0));
            // (its underside, shaded as a face looking down is -- half -- in a lighter stone: the torches' light from below)
            k.Quad(new(x0, y0, -d), new(x1, y1, -d), new(x1, y1, d), new(x0, y0, d), i % 2 == 0 ? ArchUnder : PillarLight, down);
            k.Quad(new(x0, y0, -d), new(x1, y1, -d), new(x1, top, -d), new(x0, top, -d), stone, -Vector3.UnitZ);
            k.Quad(new(x0, y0, d), new(x1, y1, d), new(x1, top, d), new(x0, top, d), stone, Vector3.UnitZ);
        }
    }

    // A brazier: an iron bowl on three legs, heaped with glowing coals, flames over them, two branding irons in them (`which`: turned so no
    // two are alike).
    private static void Brazier(Kit k, int which)
    {
        for (var leg = 0; leg < 3; leg++)
        {
            var a = leg * Math.PI * 2 / 3 + 0.4;
            double x = Math.Cos(a) * 330, z = Math.Sin(a) * 330;
            k.Box(W(x - 40, 0, z - 40), W(x + 40, 760, z + 40), CageIron, CageIron, lid: false);
            k.Box(W(x - 70, 0, z - 70), W(x + 70, 50, z + 70), CageIron, IronLight);
        }
        k.Lathe(new[] { (W(120), U(700)), (W(330), U(740)), (W(440), U(880)), (W(470), U(1000)), (W(440), U(1010)) }, 10,
            band => band >= 3 ? IronLight : CageIron, top: false, bottom: true);
        k.Lathe(new[] { (W(440), U(990)), (W(300), U(1040)), (W(0), U(1070)) }, 10, band => band == 0 ? EmberDark : Ember, top: false, bottom: false);
        // (the coals: lumps, the hottest yellow; three flames over them)
        for (var c = 0; c < 9; c++)
        {
            var a = c * 2.399 + which;
            double r = 60 + (c * 37 % 5) * 60, x = Math.Cos(a) * r, z = Math.Sin(a) * r, s = 50 + c % 3 * 20;
            k.Box(W(x - s, 1020, z - s), W(x + s, 1080 + c % 2 * 40, z + s), c % 3 == 0 ? EmberHot : c % 3 == 1 ? Ember : Ash, c % 3 == 0 ? EmberHot : Ember);
        }
        for (var f = 0; f < 3; f++)
        {
            var a = f * 2.1 + which;
            var at = W(Math.Cos(a) * 140, 1060, Math.Sin(a) * 140);
            k.Leaf(at, at + W(0, 380 + f * 90, 0), W(Math.Cos(a + 1.6) * 90, 0, Math.Sin(a + 1.6) * 90), f == 1 ? FlameCore : TorchFlame);
        }
        // (the irons: rods from the coals out over the rim, a wooden grip on each)
        for (var i = 0; i < 2; i++)
        {
            var a = which * 1.3 + i * 2.6;
            double cx = Math.Cos(a), cz = Math.Sin(a);
            k.Rod(W(cx * 60, 1040, cz * 60), W(cx * 720, 1300, cz * 720), (float)W(22), CageIron);
            k.Rod(W(cx * 720, 1300, cz * 720), W(cx * 960, 1395, cz * 960), (float)W(38), OldWood);
        }
    }

    // The rack: a wooden table along z (`width` long, `depth` across), a roller at each end with spokes to turn it by, ropes from the
    // rollers along the bed -- nobody on it.
    private static void Rack(Kit k, double depth, double width)
    {
        float d = (float)depth / 2, l = (float)width / 2, top = (float)U(850), leg = (float)W(80);
        foreach (var sx in new[] { -1, 1 })
            foreach (var sz in new[] { -1, 1 })
                k.Box(new(sx * (d - leg) - leg, 0, sz * (l - leg) - leg), new(sx * (d - leg) + leg, top, sz * (l - leg) + leg), DarkWood, DarkWood, lid: false);
        foreach (var sx in new[] { -1, 1 })
            k.Box(new(sx * d - (float)W(60), top - (float)U(140), -l), new(sx * d + (float)W(60), top, l), DarkWood, Wood, bottom: true);
        k.Box(new(-d + (float)W(60), top - (float)U(60), -l + (float)W(320)), new(d - (float)W(60), top - (float)U(20), l - (float)W(320)), OldWood, OldWood, bottom: true);
        // (the rollers, across the table near its ends, standing proud of it; spokes through one; the ropes to the middle)
        foreach (var sz in new[] { -1, 1 })
        {
            var z = sz * (l - (float)W(170));
            k.Rod(new(-d - (float)W(120), top + (float)U(60), z), new(d + (float)W(120), top + (float)U(60), z), (float)W(95), Wood);
            foreach (var sx in new[] { -1f, 1f })
                k.Rod(new(sx * (d - (float)W(160)), top + (float)U(60), z), new(sx * (d - (float)W(160)), top + (float)U(40), -sz * (float)W(900) + z * 0.55f), (float)W(20), Rope2);
        }
        for (var s = 0; s < 4; s++)
        {
            var a = s * Math.PI / 4 + 0.3;
            var c = new Vector3(d + (float)W(60), top + (float)U(60), -l + (float)W(170));
            k.Rod(c - new Vector3(0, (float)(Math.Sin(a) * U(380)), (float)(Math.Cos(a) * W(380))), c + new Vector3(0, (float)(Math.Sin(a) * U(380)), (float)(Math.Cos(a) * W(380))), (float)W(28), OldWood);
        }
    }

    // An iron maiden, shut, looking out (+x): an octagonal case on a plinth, broad at the shoulders and narrower to a head, iron bands round
    // it, its two doors' seam down the front, a stern iron face on its head.
    private static void IronMaiden(Kit k)
    {
        k.Box(W(-460, 0, -460), W(460, 100, 460), PillarDark, PillarStone);
        k.Lathe(new[] { (W(400), U(100)), (W(450), U(1450)), (W(300), U(2050)), (W(160), U(2300)), (W(0), U(2380)) }, 8, band => band == 2 ? IronLight : CageIron, top: false, bottom: false);
        foreach (var y in new[] { 450.0, 1000, 1450 })
        {
            var r = y < 1450 ? 400 + 50 * (y - 100) / 1350 : 450;
            k.Lathe(new[] { (W(r + 18), U(y - 45)), (W(r + 18), U(y + 45)) }, 8, _ => IronLight, top: false, bottom: false);
        }
        // (the front: the face of the octagon looking out -- its seam, the face over it)
        float Front(double r) => (float)(W(r) * Math.Cos(Math.PI / 8)) + 1.5f;
        k.Quad(new(Front(400), (float)U(100), (float)W(-12)), new(Front(400), (float)U(100), (float)W(12)), new(Front(440), (float)U(1720), (float)W(12)), new(Front(440), (float)U(1720), (float)W(-12)), Hole, Vector3.UnitX);
        float fx = Front(330) + 2;
        k.Quad(new(fx, (float)U(1760), (float)W(-150)), new(fx, (float)U(1760), (float)W(150)), new(fx, (float)U(2080), (float)W(110)), new(fx, (float)U(2080), (float)W(-110)), IronFace, Vector3.UnitX);
        foreach (var s in new[] { -1, 1 })
            k.Quad(new(fx + 1, (float)U(1960), (float)W(s * 40)), new(fx + 1, (float)U(1960), (float)W(s * 110)), new(fx + 1, (float)U(2000), (float)W(s * 110)), new(fx + 1, (float)U(2000), (float)W(s * 40)), Hole, Vector3.UnitX);
        k.Quad(new(fx + 1, (float)U(1820), (float)W(-70)), new(fx + 1, (float)U(1820), (float)W(70)), new(fx + 1, (float)U(1845), (float)W(70)), new(fx + 1, (float)U(1845), (float)W(-70)), Hole, Vector3.UnitX);
    }

    // A pillory: a post, a board across it at a man's neck with holes for the head and the hands (seen from both sides), an iron hinge,
    // the post's foot braced.
    private static void Pillory(Kit k)
    {
        k.Box(W(-90, 0, -90), W(90, 2150, 90), DarkWood, DarkWood);
        k.Box(W(-520, 0, -70), W(520, 120, 70), DarkWood, DarkWood);
        k.Box(W(-70, 0, -520), W(70, 120, 520), DarkWood, DarkWood);
        k.Box(W(-80, 1430, -680), W(80, 1800, 680), OldWood, Wood, bottom: true);
        k.Box(W(-84, 1600, -680), W(84, 1625, 680), CageIron, CageIron, lid: false);
        foreach (var s in new[] { -1, 1 })
            foreach (var (z, r) in new[] { (0.0, 120.0), (-420.0, 70.0), (420.0, 70.0) })
            {
                var c = W(s * 82, 1612, z);
                for (var i = 0; i < 8; i += 2)
                {
                    Vector3 P(int j) => c + W(0, Math.Sin(j * Math.PI / 4) * r, Math.Cos(j * Math.PI / 4) * r);
                    k.Quad(c, P(i), P(i + 1), P(i + 2), Hole, new Vector3(s, 0, 0));
                }
            }
        k.Box(W(80, 1540, -760), W(120, 1700, -680), CageIron, IronLight);
    }

    // A whipping post: a stone foot, a thick wooden post, two iron rings on it, a rope coiled at its foot.
    private static void WhippingPost(Kit k)
    {
        k.Box(W(-380, 0, -380), W(380, 220, 380), PillarDark, PillarStone);
        k.Lathe(new[] { (W(190), U(220)), (W(170), U(2650)), (W(210), U(2700)), (W(0), U(2760)) }, 8, band => band == 0 ? DarkWood : Wood, top: false, bottom: false);
        foreach (var y in new[] { 1750.0, 900 })
            k.Lathe(new[] { (W(200), U(y - 35)), (W(205), U(y + 35)) }, 8, _ => IronLight, top: false, bottom: false);
        foreach (var y in new[] { 1750.0, 900 })
            k.Box(W(190, y - 110, -20), W(250, y + 40, 20), CageIron, IronLight);
        for (var turn = 0; turn < 3; turn++)
            k.Lathe(new[] { (W(330 - turn * 40), U(220 + turn * 50)), (W(330 - turn * 40), U(260 + turn * 50)) }, 8, _ => Rope2, top: true, bottom: false, cx: W(-120), cz: W(140));
    }

    // A wheel on the wall (behind it, -x): a cartwheel of wood on an iron bracket, its rim in sixteen pieces, eight spokes, its hub; its
    // middle `high` up (model units).
    private static void Wheel(Kit k, double high)
    {
        var y0 = high * PalaceIsland.Up;   // (its middle's height, the world's)
        const double R = 950, Out = 230;
        Vector3 At(double a, double r) => W(Out, y0 + Math.Sin(a) * r, Math.Cos(a) * r);
        for (var i = 0; i < 16; i++)
            k.Rod(At(i * Math.PI / 8, R), At((i + 1) * Math.PI / 8, R), (float)W(70), i % 2 == 0 ? Wood : OldWood);
        for (var i = 0; i < 8; i++)
            k.Rod(At(i * Math.PI / 4, 120), At(i * Math.PI / 4, R - 60), (float)W(35), OldWood);
        k.Box(W(Out - 90, y0 - 140, -140), W(Out + 90, y0 + 140, 140), DarkWood, DarkWood);
        k.Box(W(0, y0 - 60, -60), W(Out - 90, y0 + 60, 60), CageIron, IronLight);
        foreach (var i in new[] { 1, 5, 9, 13 })
            k.Box(At(i * Math.PI / 8, R) - W(20, 50, 50), At(i * Math.PI / 8, R) + W(110, 50, 50), CageIron, IronLight);
    }

    // A chain hanging from the ceiling (`high` up, model units), link by link -- each a flat iron plate across the last, its eye dark -- a
    // hook on its end (`which`: how long).
    private static void Chain(Kit k, double high, int which)
    {
        var top = high * PalaceIsland.Up;
        var links = 6 + which * 5 % 4;
        const double Link = 170;
        for (var i = 0; i < links; i++)
        {
            double y1 = top - i * Link, y0 = y1 - Link - 30;
            var across = i % 2 == 0;
            k.Box(across ? W(-75, y0, -14) : W(-14, y0, -75), across ? W(75, y1, 14) : W(14, y1, 75), CageIron, IronLight, bottom: true);
            var n = across ? Vector3.UnitZ : Vector3.UnitX;
            foreach (var s in new[] { -1, 1 })
            {
                Vector3 P(double u, double v) => across ? W(u, v, s * 16) : W(s * 16, v, u);
                k.Quad(P(-35, y0 + 45), P(35, y0 + 45), P(35, y1 - 45), P(-35, y1 - 45), Hole, n * s);
            }
        }
        // (the hook: down, round and up)
        var foot = top - links * Link - 30;
        k.Box(W(-18, foot - 260, -18), W(18, foot, 18), IronLight, IronLight, bottom: true);
        k.Box(W(-18, foot - 300, -18), W(18, foot - 260, 180), IronLight, IronLight, bottom: true);
        k.Box(W(-18, foot - 300, 150), W(18, foot - 140, 190), IronLight, IronLight);
    }

    // Straw on a cage's floor (`depth` by `width`): a few low heaps of it in two straws, tufts sticking out of them every way (`which`:
    // strewn so that no two cages' are alike).
    private static void StrawBed(Kit k, double depth, double width, int which)
    {
        float d = (float)depth / 2, w = (float)width / 2;
        for (var i = 0; i < 4; i++)
        {
            float cx = (float)((((i * 7 + which * 3) % 5) / 4.0 - 0.5) * d), cz = (float)((((i * 3 + which * 5) % 7) / 6.0 - 0.5) * w);
            float hx = d * (0.32f + 0.08f * (i % 3)), hz = w * (0.28f + 0.07f * ((i + which) % 3));
            k.Box(new(cx - hx, 0, cz - hz), new(cx + hx, (float)U(28 + 9 * i), cz + hz), StrawDark, i % 2 == 0 ? Straw : StrawDark);
        }
        for (var t = 0; t < 14; t++)
        {
            var a = t * 2.399 + which;
            float x = (float)(Math.Cos(a * 1.7) * d * 0.7), z = (float)(Math.Sin(a * 1.3) * w * 0.7);
            var root = new Vector3(x, (float)U(25), z);
            var tip = root + W(Math.Cos(a) * 260, 90 + t % 4 * 40, Math.Sin(a) * 260);
            k.Leaf(root, tip, W(-Math.Sin(a) * 26, 0, Math.Cos(a) * 26), t % 2 == 0 ? Straw : StrawDark);
        }
    }

    private static void Cot(Kit k, double depth, double width)
    {
        // a prisoner's cot: a wooden frame on four legs, straw on it, a rough blanket over half of it
        float d = (float)depth / 2, w = (float)width / 2;
        foreach (var (sx, sz) in new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) })
            k.Box(new(sx * d - (sx > 0 ? 40 : 0), 0, sz * w - (sz > 0 ? 40 : 0)), new(sx * d + (sx < 0 ? 40 : 0), 160, sz * w + (sz < 0 ? 40 : 0)), OldWood, OldWood);
        k.Box(new(-d, 160, -w), new(d, 200, w), OldWood, OldWood);
        k.Box(new(-d + 20, 200, -w + 20), new(d - 20, 260, w - 20), Straw, Straw);
        k.Box(new(-d + 10, 210, -w + 10), new(d - 10, 272, 0), Blanket, Blanket);
    }

    private static void Barrel(Kit k)
    {
        // an old barrel, iron hoops round it
        k.Lathe(new[] { (0.0, 0.0), (120.0, 0.0), (150.0, 160.0), (150.0, 260.0), (120.0, 420.0), (0.0, 420.0) }, 10, i => i == 1 || i == 3 ? CageIron : OldWood, top: true, bottom: false);
    }

    // ---- the dungeon's captives (the user, 2026-10-17: "The palace dungeon is a bit tame, let's add some female rabbibunny rope bunnies
    // suspended from the ceiling"): female rabbibunnies -- the game's own bodies, in their own clothes and colours -- hung in ropes from the
    // dungeon's ceiling, their feet well clear of its floor. Hanging: by her wrists, her arms up and apart, a rope from each wrist up to a bar
    // under the ceiling, her legs together and tied under the knees and at the ankles, her toes down, her head bowed. Suspended: lying face
    // down under a beam, her arms bound to her sides, held up by ropes round her chest, her waist, her shins and her ankles, her head hanging.
    // Each posed by turning her bones about their pivots (CarDriver: the arms found from the bones, a two-bone reach for each hand), then
    // CaptiveScale times the game's size, as big across as up in the world (a model's units are Across times the world's across but only Up
    // times up). A rope round her is a band hugging where a plane across her cuts her (the eight-sided outline round the cut). Drawn only:
    // they hang between the cages, off the way down the dungeon's middle, nothing to run into.
    public static readonly int[] Rabbibunnies = { 62, 68 };   // BODY.HQR: Mrs. Brune, the Inter-Islands' rabbibunny; a rabbibunny in blue and white
    private const float CaptiveScale = 1.5f;
    // (radians) her arms out from upright, hanging; out from her sides and back, lying; her feet pointed; her head bowed, and hanging lower
    // lying (raised, it showed the gap under her jaw from the side: the game's bodies have no throat, the head over the shoulders)
    private const float Spread = 0.42f, AtSides = 0.12f, ToesDown = 1.0f, Bowed = 0.26f, Hung = 0.5f;
    private const float FootGap = 10;           // (her own units) the inside of each foot from her middle, her legs together
    private const int LitSteps = 7;             // a lit polygon's colour this many steps up its ramp (about the game's light on a body)
    // (world units) the bar's (or beam's) underside under the ceiling, its half thickness, past the ropes at its ends; the ropes from it to
    // her hands (to her back, lying); the ropes' half thickness, the bands round her: how wide, how far off her
    private const float BarDrop = 260, BarHalf = 45, BeamDrop = 300, BeamHalf = 55, BarOver = 160, HangingRopes = 110, LyingRopes = 420;
    private const float RopeHalf = 20, BandWide = 46, BandOff = 8;
    private static readonly (float R, float G, float B) Rope = (160, 128, 82), RopeTop = (190, 158, 104);

    // A captive (`lying`: Suspended, else Hanging) under a ceiling `ceiling` up (model units), in `palette`'s colours.
    private static void Captive(Kit k, Body model, bool lying, double ceiling, Func<int, (float R, float G, float B)> palette)
    {
        if (Bound(model, lying) is not { } bound) return;
        // (her own coordinates to the world's, about the piece's middle, up from the floor: standing she looks along +z -- turned to look out,
        // +x; lying face down, her head along +z, her right side out)
        var g = CaptiveScale;
        Vector3 Turned(Vector3 p) => (lying ? new Vector3(p.X, -p.Z, p.Y) : new Vector3(p.Z, p.Y, -p.X)) * g;
        var turned = bound.Points.Select(Turned).ToArray();
        float top = (float)(ceiling * PalaceIsland.Up), under = top - (lying ? BeamDrop : BarDrop), reach = under - (lying ? LyingRopes : HangingRopes);
        float x0 = turned.Min(p => p.X), x1 = turned.Max(p => p.X), z0 = turned.Min(p => p.Z), z1 = turned.Max(p => p.Z);
        var shift = new Vector3(-(x0 + x1) / 2, reach - bound.Top.Max(i => turned[i].Y), -(z0 + z1) / 2);
        Vector3 Q(Vector3 p) => Turned(p) + shift;
        Vector3 Room(Vector3 w) => new(w.X / PalaceIsland.Across, w.Y / (float)PalaceIsland.Up, w.Z / PalaceIsland.Across);
        void WorldBox(Vector3 a, Vector3 b, (float, float, float) side, (float, float, float) lid) => k.Box(Room(Vector3.Min(a, b)), Room(Vector3.Max(a, b)), side, lid, bottom: true);

        // her polygons in their own colours: a lit one's some steps up its ramp, a textured one's its picture's at its middle (her lines and
        // spheres -- these bodies have none -- left out)
        var at = turned.Select(p => Room(p + shift)).ToArray();
        (float R, float G, float B)? Paint(Face f)
        {
            int c = f.Colour, type = f.Material;
            if (f.Texture is { } t && model.TexturePage is { } page && t.Handle < model.Textures.Length)
            {
                double u = 0, v = 0;
                for (var i = 0; i < f.Points.Length; i++) { u += t.UV[i * 2]; v += t.UV[i * 2 + 1]; }
                var info = model.Textures[t.Handle];
                var mask = (int)(info >> 16);
                int tx = ((int)(u / f.Points.Length) >> 8) & (mask & 0xFF), ty = ((int)(v / f.Points.Length) >> 8) & ((mask >> 8) & 0xFF);
                c = page[((int)(info & 0xFFFF) + ty * 256 + tx) & 0xFFFF];
                if (c == 0 && type is (>= 12 and <= 15) or (>= 20 and <= 23)) return null;
            }
            // (lit as the game lights its type -- PalacePortraits.Paint: not 0, 2, 3, 8 or 12)
            if ((type & 3) == 1 || ((type & 3) >= 2 && type is not (2 or 3)) || type is >= 4 and <= 7)
            {
                var share = model.LightScale is { } ls ? f.Points.Average(i => i < ls.Length ? ls[i] : 1f) : 1f;
                c = Math.Min((c & ~15) + 15, c + (int)Math.Round(LitSteps * share));
            }
            return palette(c);
        }
        foreach (var f in model.Faces)
        {
            if (f.Points.Length < 3 || Paint(f) is not { } rgb) continue;
            var pts = f.Points.Select(i => at[i]).ToArray();
            var n = Vector3.Zero;
            for (var i = 0; i < pts.Length; i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Length]; n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y)); }
            if (n.LengthSquared() < 1e-6f) continue;
            n = Vector3.Normalize(n);
            for (var i = 1; i + 1 < pts.Length; i++) k.Tri(pts[0], pts[i], pts[i + 1], rgb, n);
        }

        // the ropes round her: each a band round where the plane across her cuts those bones -- the eight-sided outline whose sides touch the
        // cut, a little out from it -- facing out; and up from the ones that hold her to the bar (or beam)
        var ups = new List<Vector3>();
        foreach (var (y, bones, up) in bound.Bands)
        {
            var cut = Section(model, bound.Points, y, bones);
            if (cut.Count == 0) continue;
            float cx = (cut.Min(c => c.X) + cut.Max(c => c.X)) / 2, cz = (cut.Min(c => c.Y) + cut.Max(c => c.Y)) / 2, w = BandWide / g / 2;
            var sides = Enumerable.Range(0, 8).Select(i => new Vector2(MathF.Cos(i * MathF.PI / 4), MathF.Sin(i * MathF.PI / 4))).ToArray();
            var reach8 = sides.Select(s => cut.Max(c => Vector2.Dot(c - new Vector2(cx, cz), s)) + BandOff / g).ToArray();
            // (each corner where two neighbouring sides meet)
            var corners = Enumerable.Range(0, 8).Select(i =>
            {
                Vector2 a = sides[i], b = sides[(i + 1) % 8];
                float ra = reach8[i], rb = reach8[(i + 1) % 8], det = a.X * b.Y - a.Y * b.X;
                return new Vector2(cx + (ra * b.Y - rb * a.Y) / det, cz + (a.X * rb - b.X * ra) / det);
            }).ToArray();
            var middle = Room(Q(new Vector3(cx, y, cz)));
            var high = float.MinValue;
            for (var i = 0; i < 8; i++)
            {
                Vector2 a = corners[(i + 7) % 8], b = corners[i];
                var quad = new[] { new Vector3(a.X, y - w, a.Y), new Vector3(b.X, y - w, b.Y), new Vector3(b.X, y + w, b.Y), new Vector3(a.X, y + w, a.Y) }.Select(Q).ToArray();
                high = Math.Max(high, quad.Max(p => p.Y));
                var room = quad.Select(Room).ToArray();
                var n = Vector3.Cross(room[1] - room[0], room[3] - room[0]);
                if (n.LengthSquared() < 1e-6f) continue;
                n = Vector3.Normalize(n);
                if (Vector3.Dot(n, (room[0] + room[2]) / 2 - middle) < 0) n = -n;
                k.Quad(room[0], room[1], room[2], room[3], Rope, n);
            }
            if (up) ups.Add(Q(new Vector3(cx, y, cz)) with { Y = high });
        }
        if (ups.Count == 0) return;
        // (the bar across over her hands -- the beam along over her, lying -- hung from the ceiling at its ends, an iron plate at each)
        foreach (var p in ups) WorldBox(new(p.X - RopeHalf, p.Y, p.Z - RopeHalf), new(p.X + RopeHalf, under, p.Z + RopeHalf), Rope, RopeTop);
        float mx = ups.Average(p => p.X), half = lying ? BeamHalf : BarHalf;
        float za = (lying ? z0 + shift.Z : ups.Min(p => p.Z)) - BarOver, zb = (lying ? z1 + shift.Z : ups.Max(p => p.Z)) + BarOver;
        WorldBox(new(mx - half, under, za), new(mx + half, under + 2 * half, zb), OldWood, OldWood);
        foreach (var z in new[] { za + 2 * half, zb - 2 * half })
        {
            WorldBox(new(mx - RopeHalf, under + 2 * half, z - RopeHalf), new(mx + RopeHalf, top - 18, z + RopeHalf), Rope, RopeTop);
            WorldBox(new(mx - 55, top - 18, z - 55), new(mx + 55, top, z + 55), CageIron, IronLight);
        }
    }

    // A captive posed (her own coordinates, standing -- lying, before she is laid face down): her points, the ones her height under the
    // ropes is taken from (the hands she hangs by; lying, the whole of her) and the ropes round her: each across her at a height (y), round
    // some of her bones, a rope from it up to the bar or not. Null for a body whose arms and legs aren't found.
    private sealed record Captured(Vector3[] Points, int[] Top, List<(float Y, HashSet<int> Bones, bool Up)> Bands);

    private static Captured? Bound(Body model, bool lying)
    {
        var d = new CarDriver(model);
        if (d.FindArms() is not ({ } right, { } left)) return null;
        var rest = model.World();
        float floor = rest.Min(p => p.Y), height = rest.Max(p => p.Y) - floor;
        var pose = d.Posed;
        int[] Limb(int b) => d.Below(b).Prepend(b).ToArray();
        IEnumerable<int> PointsOf(IEnumerable<int> bones) => bones.SelectMany(b => Enumerable.Range(model.Bones[b].Start, model.Bones[b].Count));
        Vector3 Pivot(int b) => pose[model.Bones[b].Pivot];
        var arms = Limb(right.Upper).Concat(Limb(left.Upper)).ToHashSet();
        // (a leg: the bone turning highest in the lower half of her, off her middle on its side, its points reaching the floor)
        int Leg(float side)
        {
            var best = -1;
            for (var b = 1; b < model.Bones.Count; b++)
            {
                var at = d.PivotOf(b);
                if (arms.Contains(b) || at.X * side < 20 || at.Y - floor > height * 0.55f) continue;
                if (PointsOf(Limb(b)).Select(i => rest[i].Y).DefaultIfEmpty(float.MaxValue).Min() - floor > height * 0.1f) continue;
                if (best < 0 || at.Y > d.PivotOf(best).Y) best = b;
            }
            return best;
        }
        int rightLeg = Leg(1), leftLeg = Leg(-1);
        if (rightLeg < 0 || leftLeg < 0) return null;
        var legs = Limb(rightLeg).Concat(Limb(leftLeg)).ToHashSet();
        // (her head: the biggest bone turning high on her middle, with what hangs from it -- her ears)
        var head = Enumerable.Range(1, model.Bones.Count - 1)
            .Where(b => !arms.Contains(b) && !legs.Contains(b) && d.PivotOf(b).Y - floor > height * 0.6f && Math.Abs(d.PivotOf(b).X) < 40)
            .DefaultIfEmpty(-1).MaxBy(b => b < 0 ? -1 : model.Bones[b].Count);
        var middle = Enumerable.Range(0, model.Bones.Count).Where(b => !arms.Contains(b) && !legs.Contains(b) && (head < 0 || !Limb(head).Contains(b))).ToHashSet();

        // her legs together -- each turned at the hip till the inside of its foot is by her middle -- her toes pointed down
        var shins = new List<int[]>();
        foreach (var (leg, side) in new[] { (rightLeg, 1f), (leftLeg, -1f) })
        {
            var limb = Limb(leg);
            var foot = limb.MinBy(b => d.PivotOf(b).Y);
            var feet = PointsOf(new[] { foot }).Select(i => pose[i]).ToList();
            if (feet.Count > 0)
            {
                var centre = feet.Aggregate(Vector3.Zero, (s, p) => s + p) / feet.Count;
                var inside = feet.Min(p => p.X * side);
                d.Turn(limb, Pivot(leg), CarDriver.Between(centre - Pivot(leg), centre - new Vector3(side * (inside - FootGap), 0, 0) - Pivot(leg)));
            }
            d.Turn(Limb(foot), Pivot(foot), Quaternion.CreateFromAxisAngle(Vector3.UnitX, ToesDown));
            shins.Add(limb.Length > 2 ? Limb(limb[1]) : limb);
        }
        // her arms straight: hanging, up and apart over her head; lying, down her sides, a little behind them
        foreach (var (arm, side) in new[] { (right, 1f), (left, -1f) })
        {
            var shoulder = d.PivotOf(arm.Upper);
            var elbow = d.PivotOf(arm.Fore[0]);
            var length = Vector3.Distance(shoulder, elbow) + Vector3.Distance(elbow, d.Far(arm.Fore, elbow));
            var way = lying ? Vector3.Normalize(new Vector3(side * AtSides, -1, -AtSides)) : new Vector3(side * MathF.Sin(Spread), MathF.Cos(Spread), 0);
            d.Reach(arm.Upper, arm.Fore, shoulder + way * length * 0.98f, side);
        }
        var waistY = (Pivot(rightLeg).Y + Pivot(leftLeg).Y) / 2 + height * 0.06f;
        if (head >= 0) d.Turn(Limb(head), Pivot(head), Quaternion.CreateFromAxisAngle(Vector3.UnitX, lying ? Hung : Bowed));

        // the ropes: round her shins under her knees and round her ankles; and round each wrist (hanging) or her chest and her waist, her
        // arms with them (lying)
        var bands = new List<(float Y, HashSet<int> Bones, bool Up)>();
        var below = shins.SelectMany(s => s).ToHashSet();
        float knee = shins.Average(s => Pivot(s[0]).Y), ankle = shins.Average(s => Pivot(s[^1]).Y);
        bands.Add((knee - height * 0.03f, below, lying));
        bands.Add((ankle + height * 0.035f, below, lying));
        int[] top;
        if (lying)
        {
            var chest = (d.PivotOf(right.Upper).Y + d.PivotOf(left.Upper).Y) / 2 - height * 0.1f;
            bands.Add((chest, middle.Concat(arms).ToHashSet(), true));
            bands.Add((waistY, middle.Concat(arms).ToHashSet(), true));
            top = Enumerable.Range(0, pose.Length).ToArray();
        }
        else
        {
            var hands = new List<int>();
            foreach (var arm in new[] { right, left })
            {
                var hand = arm.Fore.Length > 1 ? arm.Fore[1] : arm.Fore[0];
                bands.Add((Pivot(hand).Y + height * 0.012f, new HashSet<int> { arm.Fore[0], hand }, true));
                hands.AddRange(PointsOf(Limb(hand)));
            }
            top = hands.ToArray();
        }
        return new Captured((Vector3[])pose.Clone(), top, bands);
    }

    // Where a plane across a posed body at a height (y) cuts the polygons of some of its bones (those wholly theirs): the points (x, z) where
    // their sides cross it; none where it misses them.
    private static List<Vector2> Section(Body model, Vector3[] points, float y, HashSet<int> bones)
    {
        var mine = bones.SelectMany(b => Enumerable.Range(model.Bones[b].Start, model.Bones[b].Count)).ToHashSet();
        var cut = new List<Vector2>();
        foreach (var f in model.Faces)
        {
            if (!f.Points.All(mine.Contains)) continue;
            for (var i = 0; i < f.Points.Length; i++)
            {
                Vector3 a = points[f.Points[i]], b = points[f.Points[(i + 1) % f.Points.Length]];
                if (a.Y == b.Y || (a.Y - y) * (b.Y - y) > 0) continue;
                var c = a + (b - a) * ((y - a.Y) / (b.Y - a.Y));
                cut.Add(new Vector2(c.X, c.Z));
            }
        }
        return cut;
    }

    private static void WeaponRack(Kit k, double width)
    {
        // a wooden rack against the wall: two posts, two rails, spears and swords standing in it
        float w = (float)width / 2;
        foreach (var s in new[] { -1, 1 })
            k.Box(new(-60, 0, s * w - 35), new(10, 1300, s * w + 35), DarkWood, DarkWood);
        k.Box(new(-40, 300, -w), new(30, 360, w), DarkWood, DarkWood);
        k.Box(new(-40, 1000, -w), new(30, 1060, w), DarkWood, DarkWood);
        for (float z = -w + 120; z < w - 80; z += 160)
        {
            var spear = ((int)((z + w) / 160)) % 2 == 0;
            k.Box(new(30, 0, z - 15), new(60, spear ? 1550 : 1150, z + 15), spear ? Wood : Steel, Steel);
            if (spear) k.Leaf(new Vector3(45, 1550, z), new Vector3(45, 1720, z), new Vector3(0, 0, 40), Steel);
            else k.Box(new(20, 1000, z - 70), new(70, 1030, z + 70), DarkGold, Gold);
        }
    }

    private static void Altar(Kit k, double depth, double width)
    {
        // a white marble altar, a red cloth over it hanging at the front with a gold edge, candlesticks and a gold orb on it
        float d = (float)depth / 2, w = (float)width / 2;
        k.Box(new(-d, 0, -w), new(d, 420, w), WhiteMarble, WhiteMarble);
        k.Box(new(-d - 10, 420, -w - 10), new(d + 10, 440, w + 10), Royal, Royal, bottom: true);
        k.Quad(new(d + 12, 160, -w * 0.6f), new(d + 12, 160, w * 0.6f), new(d + 12, 430, w * 0.6f), new(d + 12, 430, -w * 0.6f), Royal, Vector3.UnitX);
        k.Quad(new(d + 14, 160, -w * 0.6f), new(d + 14, 160, w * 0.6f), new(d + 14, 200, w * 0.6f), new(d + 14, 200, -w * 0.6f), Gold, Vector3.UnitX);
        k.Lathe(new[] { (0.0, 440.0), (60.0, 450.0), (40.0, 480.0), (90.0, 540.0), (100.0, 600.0), (70.0, 660.0), (0.0, 680.0) }, 8, _ => Gold, top: false, bottom: false);
    }

    private static void StairColumn(Kit k, double high, double radius)
    {
        // the stairwell's column, the ramp winding round it: red marble in gold bands a storey apart, a gold foot and capital
        var r = radius * 0.92;
        k.Lathe(new[] { (radius, 0.0), (radius, 90.0), (r, 130.0) }, 12, _ => Gold, top: false, bottom: false);
        var bands = new List<(double R, double Y)> { (r, 130.0) };
        for (var y = 900.0; y < high - 400; y += 900) { bands.Add((r, y)); bands.Add((r, y + 60)); }
        bands.Add((r, high - 260));
        k.Lathe(bands.ToArray(), 12, i => i % 2 == 1 ? Gold : RedMarble, top: false, bottom: false);
        k.Lathe(new[] { (r, high - 260), (radius * 1.08, high - 160), (radius * 1.08, high) }, 12, _ => Gold, top: false, bottom: false);
    }

    private static void Pew(Kit k, double depth, double width)
    {
        // a long wooden seat facing +x, its back behind it, its length along z
        float d = (float)depth / 2, w = (float)width / 2;
        k.Box(new(-d, 200, -w), new(d, 250, w), DarkWood, Wood, bottom: true);
        k.Box(new(-d, 250, -w), new(-d + 50, 620, w), DarkWood, DarkWood);
        foreach (var s in new[] { -1, 1 })
            k.Box(new(-d, 0, s * w - 30), new(d, 330, s * w + 30), DarkWood, DarkGold);
        k.Box(new(-d + 30, 0, -w + 30), new(d - 30, 200, -w + 60), DarkWood, DarkWood, lid: false);
    }

    private static void Column(Kit k, double high)
    {
        // a red marble shaft on a gold base, a gold capital and its square top
        k.Box(new(-220, 0, -220), new(220, 60, 220), DarkGold, Gold, bottom: false);
        k.Lathe(new[] { (190.0, 60.0), (190.0, 110.0), (150.0, 130.0) }, 8, _ => Gold, top: false, bottom: false);
        k.Lathe(new[] { (150.0, 130.0), (150.0, high - 150) }, 8, _ => RedMarble, top: false, bottom: false);
        k.Lathe(new[] { (150.0, high - 150), (195.0, high - 110), (195.0, high - 70) }, 8, _ => Gold, top: false, bottom: false);
        k.Box(new(-230, (float)high - 70, -230), new(230, (float)high, 230), DarkGold, Gold, bottom: true);
    }

    private static void Plant(Kit k, double y0)
    {
        // a gold pot, its earth, and purple leaves fanning up and out of it
        k.Lathe(new[] { (150.0, y0), (250.0, y0 + 60), (320.0, y0 + 190), (310.0, y0 + 320), (255.0, y0 + 395), (295.0, y0 + 430), (285.0, y0 + 470) }, 8,
            band => band is 1 or 4 ? DarkGold : PotGold, top: false, bottom: true);
        k.Lathe(new[] { (285.0, y0 + 470), (250.0, y0 + 450), (0.0, y0 + 450) }, 8, _ => Soil, top: false, bottom: false);
        var root = new Vector3(0, (float)(y0 + 450), 0);
        for (var i = 0; i < 9; i++)
        {
            var a = i * Math.PI * 2 / 9 + (i % 2) * 0.3;
            var reach = i % 3 == 0 ? 470 : 400;
            var rise = i % 3 == 1 ? 560 : i % 3 == 2 ? 380 : 460;
            var tip = root + new Vector3((float)(Math.Cos(a) * reach), rise, (float)(Math.Sin(a) * reach));
            var side = Vector3.Normalize(new Vector3((float)-Math.Sin(a), 0, (float)Math.Cos(a))) * 120;
            k.Leaf(root + new Vector3(0, 30, 0), tip, side, i % 2 == 0 ? Purple : DarkPurple);
        }
        // (a tuft standing up in the middle)
        k.Leaf(root, root + new Vector3(40, 620, 30), new Vector3(110, 0, -60), Purple);
        k.Leaf(root, root + new Vector3(-30, 600, -40), new Vector3(-60, 0, 110), DarkPurple);
    }

    private static void Lamp(Kit k)
    {
        // a gold bracket out of the wall (behind it, -x), a glass globe under it, a gold leaf over it on the wall
        k.Box(new(-256, 300, -30), new(-40, 340, 30), DarkGold, Gold, bottom: true);
        k.Lathe(new[] { (0.0, 290.0), (35.0, 300.0), (35.0, 330.0), (0.0, 340.0) }, 6, _ => Gold, top: false, bottom: false, cx: -10);
        k.Lathe(new[] { (0.0, 120.0), (80.0, 145.0), (112.0, 200.0), (95.0, 255.0), (45.0, 290.0), (0.0, 295.0) }, 8,
            band => band >= 3 ? GlobeTop : Globe, top: false, bottom: false, cx: -10);
        k.Quad(new(-254, 330, -10), new(-254, 470, -110), new(-254, 600, 0), new(-254, 470, 110), Gold, Vector3.UnitX);
    }

    private static void Bench(Kit k, double length, double depth)
    {
        // a red cushion on a fringed red skirt with a gold band, on four dark legs -- its length along z
        double l = length / 2 - 60, d = depth / 2 - 60;
        k.Box(new((float)-d, 300, (float)-l), new((float)d, 470, (float)l), Cushion, (196, 72, 54), bottom: false);
        k.Box(new((float)-d - 10, 280, (float)-l - 10), new((float)d + 10, 305, (float)l + 10), DarkGold, Gold, bottom: true);
        k.Box(new((float)-d + 30, 150, (float)-l + 30), new((float)d - 30, 280, (float)l - 30), Fringe, Fringe, lid: false, bottom: true);
        foreach (var sx in new[] { -1, 1 })
            foreach (var sz in new[] { -1, 1 })
                k.Box(new((float)(sx * (d - 70) - 40), 0, (float)(sz * (l - 70) - 40)), new((float)(sx * (d - 70) + 40), 150, (float)(sz * (l - 70) + 40)), Wood, Wood, lid: false);
    }

    private static void Candelabra(Kit k)
    {
        // a pewter foot and stem, an arm across with three candles -- the arm along z
        k.Lathe(new[] { (140.0, 0.0), (140.0, 30.0), (70.0, 60.0), (35.0, 120.0) }, 8, _ => Pewter, top: false, bottom: true);
        k.Box(new(-25, 120, -25), new(25, 600, 25), Pewter, Pewter, lid: false);
        k.Box(new(-22, 580, -260), new(22, 625, 260), Pewter, Pewter, bottom: true);
        foreach (var (z, rise) in new[] { (-240.0, 0.0), (0.0, 60.0), (240.0, 0.0) })
        {
            k.Box(new(-35, (float)(625 + rise), (float)(z - 35)), new(35, (float)(660 + rise), (float)(z + 35)), Pewter, Pewter, bottom: true);
            k.Box(new(-22, (float)(660 + rise), (float)(z - 22)), new(22, (float)(900 + rise), (float)(z + 22)), Wax, Wax);
            k.Leaf(new Vector3(0, (float)(905 + rise), (float)z), new Vector3(0, (float)(990 + rise), (float)z), new Vector3(0, 0, 20), Flame);
            k.Leaf(new Vector3(0, (float)(905 + rise), (float)z), new Vector3(0, (float)(990 + rise), (float)z), new Vector3(20, 0, 0), Flame);
        }
    }

    private static void Candle(Kit k)
    {
        k.Lathe(new[] { (100.0, 0.0), (100.0, 25.0), (40.0, 55.0) }, 8, _ => Pewter, top: true, bottom: true);
        k.Box(new(-20, 55, -20), new(20, 450, 20), Pewter, Pewter, lid: false);
        k.Box(new(-45, 450, -45), new(45, 490, 45), Pewter, Pewter, bottom: true);
        k.Box(new(-24, 490, -24), new(24, 700, 24), Wax, Wax);
        k.Leaf(new Vector3(0, 705, 0), new Vector3(0, 785, 0), new Vector3(0, 0, 20), Flame);
        k.Leaf(new Vector3(0, 705, 0), new Vector3(0, 785, 0), new Vector3(20, 0, 0), Flame);
    }

    private static void Fireplace(Kit k, double depth, double width, double high)
    {
        // a green marble chimney breast against the wall (behind it, -x), a gold mantel shelf on it, the hearth in front: a white marble
        // surround round the black fireplace, an iron grate in it
        double d = depth / 2, w = width / 2, mantel = high - 130;
        k.Box(new((float)-d, 0, (float)-w), new((float)d, (float)mantel, (float)w), GreenMarble, GreenMarble, lid: false);
        k.Box(new((float)-d, (float)mantel, (float)-w - 20), new((float)d + 50, (float)high, (float)w + 20), DarkGold, Gold, bottom: true);
        var front = (float)d;
        k.Quad(new(front + 4, 0, -700), new(front + 4, 0, 700), new(front + 4, 920, 700), new(front + 4, 920, -700), WhiteMarble, Vector3.UnitX);
        k.Quad(new(front + 8, 0, -470), new(front + 8, 0, 470), new(front + 8, 640, 470), new(front + 8, 640, -470), Soot, Vector3.UnitX);
        k.Quad(new(front + 8, 640, -470), new(front + 8, 640, 470), new(front + 8, 720, 360), new(front + 8, 720, -360), Soot, Vector3.UnitX);
        for (var bar = 0; bar < 3; bar++)
            k.Box(new(front - 60, (float)(70 + bar * 70), -380), new(front + 30, (float)(95 + bar * 70), 380), Iron, Iron);
        // (gold strips down the surround's sides)
        foreach (var s in new[] { -1, 1 })
            k.Quad(new(front + 12, 0, s * 700 - 60), new(front + 12, 0, s * 700 + 60), new(front + 12, 920, s * 700 + 60), new(front + 12, 920, s * 700 - 60), Gold, Vector3.UnitX);
    }

    // A fireplace made a teleporter (the user, 2026-10-13: "Our fireplace ... let's turn it into a teleporter so driving into it brings us
    // out of a second fire place"; then "Make the teleport linked fireplaces visually identical to the other fireplaces, they're a secret
    // warp point"): the others' fireplace (Fireplace) -- its marble breast, white surround, black opening and iron grate, gold shelf and
    // strips -- but hollow behind its opening, the whole block deep, black inside: a car drives in (PalaceIsland takes the middle two cells
    // of the four along the wall, PortalHalf either side of the middle and PortalHigh up, out of the collision).
    public const double PortalHalf = 512, PortalHigh = 768;    // (room units: the hollow's cells, two across and three layers up)
    private const double OpenHalf = 470, OpenHigh = 640, OpenTop = 720, SurroundHalf = 700, SurroundHigh = 920;   // (as Fireplace draws them)
    private static void Portal(Kit k, double depth, double width, double high)
    {
        float d = (float)(depth / 2), w = (float)(width / 2), mantel = (float)(high - 130), oh = (float)OpenHalf, oy = (float)OpenHigh;
        // (the breast: its front either side of the opening and over it, its two ends; its top under the shelf, its back against the wall)
        k.Quad(new(d, 0, -w), new(d, 0, -oh), new(d, mantel, -oh), new(d, mantel, -w), GreenMarble, Vector3.UnitX);
        k.Quad(new(d, 0, oh), new(d, 0, w), new(d, mantel, w), new(d, mantel, oh), GreenMarble, Vector3.UnitX);
        k.Quad(new(d, oy, -oh), new(d, oy, oh), new(d, mantel, oh), new(d, mantel, -oh), GreenMarble, Vector3.UnitX);
        k.Quad(new(-d, 0, -w), new(d, 0, -w), new(d, mantel, -w), new(-d, mantel, -w), GreenMarble, -Vector3.UnitZ);
        k.Quad(new(-d, 0, w), new(d, 0, w), new(d, mantel, w), new(-d, mantel, w), GreenMarble, Vector3.UnitZ);
        k.Box(new(-d, mantel, -w - 20), new(d + 50, (float)high, w + 20), DarkGold, Gold, bottom: true);
        // (the hollow: black at its back, its sides and its top)
        k.Quad(new(-d + 2, 0, -oh), new(-d + 2, 0, oh), new(-d + 2, oy, oh), new(-d + 2, oy, -oh), Soot, Vector3.UnitX);
        k.Quad(new(-d, 0, -oh), new(d, 0, -oh), new(d, oy, -oh), new(-d, oy, -oh), Soot, Vector3.UnitZ);
        k.Quad(new(-d, 0, oh), new(d, 0, oh), new(d, oy, oh), new(-d, oy, oh), Soot, -Vector3.UnitZ);
        k.Quad(new(-d, oy, -oh), new(d, oy, -oh), new(d, oy, oh), new(-d, oy, oh), Soot, -Vector3.UnitY);
        // (the front as Fireplace's: the white surround round the opening, the black over the opening's top, the grate, the gold strips)
        var front = d + 4;
        float sh = (float)SurroundHalf, sy = (float)SurroundHigh;
        k.Quad(new(front, 0, -sh), new(front, 0, -oh), new(front, sy, -oh), new(front, sy, -sh), WhiteMarble, Vector3.UnitX);
        k.Quad(new(front, 0, oh), new(front, 0, sh), new(front, sy, sh), new(front, sy, oh), WhiteMarble, Vector3.UnitX);
        k.Quad(new(front, oy, -oh), new(front, oy, oh), new(front, sy, oh), new(front, sy, -oh), WhiteMarble, Vector3.UnitX);
        k.Quad(new(front + 8, oy, -oh), new(front + 8, oy, oh), new(front + 8, (float)OpenTop, 360), new(front + 8, (float)OpenTop, -360), Soot, Vector3.UnitX);
        for (var bar = 0; bar < 3; bar++)
            k.Box(new(front - 60, (float)(70 + bar * 70), -380), new(front + 30, (float)(95 + bar * 70), 380), Iron, Iron);
        foreach (var s in new[] { -1, 1 })
            k.Quad(new(front + 12, 0, s * sh - 60), new(front + 12, 0, s * sh + 60), new(front + 12, sy, s * sh + 60), new(front + 12, sy, s * sh - 60), Gold, Vector3.UnitX);
    }

    private static void Window(Kit k, double depth, double width, double high, bool inside)
    {
        // on the wall's face (in front of it, +x): a gold frame round two tall panes, a bar across a third of the way down, gold leaves on the
        // lower panes (as the bricks picture it)
        var f = (float)(depth / 2 + 6);
        double w = width / 2, u0 = -w + 0.08 * width, u1 = w - 0.08 * width, v0 = 0.05 * high, v1 = 0.95 * high, t = 0.045 * width;
        double cross = v1 - (v1 - v0) * 0.33, mid = 0.0;
        void Q(double ua, double va, double ub, double vb, (float, float, float) c, float off = 0) =>
            k.Quad(new(f + off, (float)va, (float)ua), new(f + off, (float)va, (float)ub), new(f + off, (float)vb, (float)ub), new(f + off, (float)vb, (float)ua), c, Vector3.UnitX);
        // the glass, darker low down, and the frame over it
        Q(u0, v0, u1, cross, GlassLow);
        Q(u0, cross, u1, v1, Glass);
        Q(u0, v0, u0 + t, v1, Gold, 4); Q(u1 - t, v0, u1, v1, Gold, 4);
        Q(u0, v0, u1, v0 + t, Gold, 4); Q(u0, v1 - t, u1, v1, Gold, 4);
        Q(mid - t / 2, v0, mid + t / 2, v1, DarkGold, 4);
        Q(u0, cross - t / 2, u1, cross + t / 2, DarkGold, 4);
        if (!inside) return;
        foreach (var s in new[] { -1, 1 })
        {
            double cu = s * (u1 - u0) / 4, cv = v0 + (cross - v0) * 0.35, r = (u1 - u0) * 0.09;
            k.Quad(new(f + 6, (float)(cv - r * 1.6), (float)cu), new(f + 6, (float)cv, (float)(cu + r)), new(f + 6, (float)(cv + r * 1.6), (float)cu), new(f + 6, (float)cv, (float)(cu - r)), Gold, Vector3.UnitX);
        }
    }

    private static void ChestModel(Kit k, double length, double depth, double high)
    {
        // a mauve-grey chest, its long side along z: the box, a dark band where the lid shuts, the lid, gold leaves on its long sides and lid,
        // a gold clasp on the front (+x)
        double l = length / 2, d = depth / 2, band = high * 0.6;
        k.Box(new((float)-d, 0, (float)-l), new((float)d, (float)band, (float)l), ChestBody, ChestBody, lid: false);
        k.Box(new((float)-d + 10, (float)band, (float)-l + 10), new((float)d - 10, (float)(band + 30), (float)l - 10), ChestBand, ChestBand, lid: false, bottom: false);
        k.Box(new((float)-d - 8, (float)(band + 30), (float)-l - 8), new((float)d + 8, (float)high, (float)l + 8), ChestBody, ChestLid, bottom: true);
        foreach (var s in new[] { -1, 1 })
        {
            var x = (float)(s * (d + 4));
            foreach (var z in new[] { -l * 0.45, l * 0.45 })
                k.Quad(new(x, (float)(band * 0.25), (float)z), new(x, (float)(band * 0.5), (float)(z + 90)), new(x, (float)(band * 0.75), (float)z), new(x, (float)(band * 0.5), (float)(z - 90)), Gold, new Vector3(s, 0, 0));
        }
        var top = (float)high + 4;
        k.Quad(new(0, top, (float)(-l * 0.3)), new((float)(d * 0.5), top, 0), new(0, top, (float)(l * 0.3)), new((float)(-d * 0.5), top, 0), Gold, Vector3.UnitY);
        k.Box(new((float)d, (float)(band - 90), -45), new((float)d + 30, (float)(band + 40), 45), DarkGold, Gold);
    }

    // A portrait's or the round window's picture on a face of its wall (the side it looks out of, `nx`, `nz`): its block's picture looked up
    // where the isometric picture draws each spot of the face, a grid of them (Samples a cell across, a layer up), the ones in the
    // wallpaper's colour left out, the rest matched in Otringal's palette and merged into rectangles of one colour, just off the wall. (The
    // bricks picture a block's +x and +z sides: a picture on its other side is that one, as if seen through the wall.) `trim`: the share of
    // the face left out at each of its sides.
    private const int SamplesAcross = 6, SamplesUp = 5;
    private static void Picture(List<BrickBuilding.Face> list, Lba1SceneImage image, Piece piece, int nx, int nz, Func<double, double, double, Vector3> place,
        Func<(float R, float G, float B), Vector3, int> colour, Func<int, (float R, float G, float B)> wall, double trim = 0)
    {
        var (ox, oy, oz) = piece.Origin; var (dx, dy, dz) = piece.Size;
        var alongZ = nx != 0;
        var across = alongZ ? dz : dx;
        int nu = across * SamplesAcross, nv = dy * SamplesUp;
        var normal = Vector3.Normalize(nx != 0 ? new Vector3(nx, 0, 0) : new Vector3(0, 0, nz));
        var grid = new Dictionary<(int U, int V), int>();
        for (var v = (int)(nv * trim); v < nv - (int)(nv * trim); v++)
            for (var u = (int)(nu * trim); u < nu - (int)(nu * trim); u++)
            {
                // (a spot of the block's +x or +z face, in the block's own units: where the picture has it -- the joined map draws a cell's
                // brick with its top left corner where Project puts the cell's lowest corner, so a point is drawn 24 pixels right of and 15
                // below where Project puts it)
                double a = (u + 0.5) / SamplesAcross * 512, h = (v + 0.5) / SamplesUp * 256;
                var p = alongZ ? image.Project(dx * 512 - 1, h, a) : image.Project(a, h, dz * 512 - 1);
                int px = (int)Math.Floor(p.X + 24), py = (int)Math.Floor(p.Y + 15);
                if (px < 0 || py < 0 || px >= image.Width || py >= image.Height || image.Bgra[(py * image.Width + px) * 4 + 3] == 0) continue;
                // (the pixels round it, averaged: the bricks' dithering would come out as specks)
                double r = 0, g = 0, b = 0; var n = 0;
                for (var qy = Math.Max(0, py - 1); qy <= Math.Min(image.Height - 1, py + 1); qy++)
                    for (var qx = Math.Max(0, px - 1); qx <= Math.Min(image.Width - 1, px + 1); qx++)
                    {
                        var o = (qy * image.Width + qx) * 4;
                        if (image.Bgra[o + 3] == 0) continue;
                        r += image.Bgra[o + 2]; g += image.Bgra[o + 1]; b += image.Bgra[o]; n++;
                    }
                (float R, float G, float B) rgb = ((float)(r / n), (float)(g / n), (float)(b / n));
                // (the wall round it: near the wallpaper's colour at that height -- left to the wall's own face)
                var w = wall(oy + v / SamplesUp);
                var dist = (rgb.R - w.R) * (rgb.R - w.R) + (rgb.G - w.G) * (rgb.G - w.G) + (rgb.B - w.B) * (rgb.B - w.B);
                if (dist < 40 * 40) continue;
                var c = colour(rgb, normal);
                grid[(u, v)] = c;
            }
        foreach (var (u0, v0, u1, v1, c) in BrickBuilding.Rectangles(grid))
        {
            // (corners: the face's plane just in front of the wall, cells and layers of the palace)
            double ca = (double)u0 / SamplesAcross, cb = (double)(u1 + 1) / SamplesAcross, la = oy + (double)v0 / SamplesUp, lb = oy + (double)(v1 + 1) / SamplesUp;
            Vector3 P(double along, double layer) => alongZ
                ? place(ox + (nx > 0 ? dx + 0.01 : -0.01), layer, oz + along)
                : place(ox + along, layer, oz + (nz > 0 ? dz + 0.01 : -0.01));
            list.Add(new BrickBuilding.Face(new[] { P(ca, la), P(cb, la), P(cb, lb), P(ca, lb) }, c, normal));
        }
    }

    // A model's maker: points in a room's units about the middle of a piece's foot (+x its front, y up from its foot), turned so that +x is
    // the way it looks, and placed in the island's world; each face given the way it looks outwards (BrickBuilding.AddBodies winds it so).
    private sealed class Kit
    {
        private readonly List<BrickBuilding.Face> list;
        private readonly Func<double, double, double, Vector3> place;
        private readonly Func<(float R, float G, float B), Vector3, int> colour;
        private readonly double cx, cy, cz;
        private readonly int fx, fz;

        public Kit(List<BrickBuilding.Face> list, Func<double, double, double, Vector3> place, Func<(float R, float G, float B), Vector3, int> colour,
            double cx, double cy, double cz, int fx, int fz)
        {
            this.list = list; this.place = place; this.colour = colour; this.cx = cx; this.cy = cy; this.cz = cz;
            (this.fx, this.fz) = fx == 0 && fz == 0 ? (1, 0) : (fx, fz);
        }

        // (a model's (x, z) turned so that +x goes the way it looks: +x stays, +z: x -> z, z -> -x; and so on)
        private (double X, double Z) Turn(double x, double z) => (x * fx - z * fz, x * fz + z * fx);

        private Vector3 World(Vector3 p)
        {
            var (x, z) = Turn(p.X, p.Z);
            return place(cx + x / 512, cy + p.Y / 256, cz + z / 512);
        }

        private Vector3 WorldNormal(Vector3 n)
        {
            // (the island stretches a room Across times across and Up times up: a normal shrinks by the same)
            var (x, z) = Turn(n.X, n.Z);
            var w = new Vector3((float)(x / PalaceIsland.Across), (float)(n.Y / PalaceIsland.Up), (float)(z / PalaceIsland.Across));
            return w.LengthSquared() > 0 ? Vector3.Normalize(w) : Vector3.UnitY;
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, (float R, float G, float B) rgb, Vector3 normal)
        {
            var n = WorldNormal(normal);
            list.Add(new BrickBuilding.Face(new[] { World(a), World(b), World(c), World(d) }, colour(rgb, n), n));
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, (float R, float G, float B) rgb, Vector3 normal)
        {
            var n = WorldNormal(normal);
            list.Add(new BrickBuilding.Face(new[] { World(a), World(b), World(c) }, colour(rgb, n), n));
        }

        // A box's faces: its sides and bottom in `side`, its top in `top` (its bottom only when asked, its top -- `lid` -- unless told not:
        // the faces nobody sees left off).
        public void Box(Vector3 lo, Vector3 hi, (float R, float G, float B) side, (float R, float G, float B) top, bool bottom = false, bool lid = true)
        {
            float xa = lo.X, ya = lo.Y, za = lo.Z, xb = hi.X, yb = hi.Y, zb = hi.Z;
            if (lid) Quad(new(xa, yb, za), new(xb, yb, za), new(xb, yb, zb), new(xa, yb, zb), top, Vector3.UnitY);
            if (bottom) Quad(new(xa, ya, za), new(xb, ya, za), new(xb, ya, zb), new(xa, ya, zb), side, -Vector3.UnitY);
            Quad(new(xa, ya, za), new(xb, ya, za), new(xb, yb, za), new(xa, yb, za), side, -Vector3.UnitZ);
            Quad(new(xa, ya, zb), new(xb, ya, zb), new(xb, yb, zb), new(xa, yb, zb), side, Vector3.UnitZ);
            Quad(new(xa, ya, za), new(xa, ya, zb), new(xa, yb, zb), new(xa, yb, za), side, -Vector3.UnitX);
            Quad(new(xb, ya, za), new(xb, ya, zb), new(xb, yb, zb), new(xb, yb, za), side, Vector3.UnitX);
        }

        // A turned shape: rings of `sides` points at each (radius, height) of the profile (from the foot up), about (cx, cz); band i (between
        // rings i and i + 1) in band(i); caps closing the top and the bottom rings when asked.
        public void Lathe((double R, double Y)[] profile, int sides, Func<int, (float R, float G, float B)> band, bool top, bool bottom, double cx = 0, double cz = 0)
        {
            Vector3 P(int ring, int s)
            {
                var a = 2 * Math.PI * (s + 0.5) / sides;
                return new((float)(cx + Math.Cos(a) * profile[ring].R), (float)profile[ring].Y, (float)(cz + Math.Sin(a) * profile[ring].R));
            }
            for (var i = 0; i + 1 < profile.Length; i++)
            {
                var (r0, y0) = profile[i]; var (r1, y1) = profile[i + 1];
                if (r0 <= 0 && r1 <= 0) continue;
                for (var s = 0; s < sides; s++)
                {
                    var a = 2 * Math.PI * (s + 1.0) / sides;
                    // (outwards: across the profile's slope, round the axis)
                    var n = new Vector3((float)(Math.Cos(a) * (y1 - y0)), (float)(r0 - r1), (float)(Math.Sin(a) * (y1 - y0)));
                    if (n.LengthSquared() < 1e-6f) continue;
                    n = Vector3.Normalize(n);
                    var s1 = (s + 1) % sides;
                    if (r0 <= 0) Tri(P(i, s), P(i + 1, s), P(i + 1, s1), band(i), n);
                    else if (r1 <= 0) Tri(P(i, s), P(i, s1), P(i + 1, s), band(i), n);
                    else Quad(P(i, s), P(i, s1), P(i + 1, s1), P(i + 1, s), band(i), n);
                }
            }
            void Cap(int ring, Vector3 n)
            {
                if (profile[ring].R <= 0) return;
                var middle = new Vector3((float)cx, (float)profile[ring].Y, (float)cz);
                for (var s = 0; s < sides; s += 2)
                    Quad(middle, P(ring, s), P(ring, (s + 1) % sides), P(ring, (s + 2) % sides), band(Math.Max(0, ring - (n.Y > 0 ? 1 : 0))), n);
            }
            if (top) Cap(profile.Length - 1, Vector3.UnitY);
            if (bottom) Cap(0, -Vector3.UnitY);
        }

        // A rod (a bar, a spoke, a rope): four sides from `a` to `b`, `half` its half thickness, each facing out.
        public void Rod(Vector3 a, Vector3 b, float half, (float R, float G, float B) rgb)
        {
            var along = b - a;
            if (along.LengthSquared() < 1e-6f) return;
            along = Vector3.Normalize(along);
            var side = Vector3.Normalize(Vector3.Cross(along, MathF.Abs(along.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX)) * half;
            var up = Vector3.Normalize(Vector3.Cross(side, along)) * half;
            Vector3[] c = { side + up, up - side, -side - up, side - up };
            for (var i = 0; i < 4; i++)
            {
                Vector3 p = c[i], q = c[(i + 1) % 4];
                Quad(a + p, a + q, b + q, b + p, rgb, Vector3.Normalize(p + q));
            }
        }

        // A leaf (a flame, a frond): a flat diamond from its root to its tip, `side` its half width at its widest, seen from both sides.
        public void Leaf(Vector3 root, Vector3 tip, Vector3 side, (float R, float G, float B) rgb)
        {
            var wide = root + (tip - root) * 0.4f;
            var n = Vector3.Cross(tip - root, side);
            if (n.LengthSquared() < 1e-6f) return;
            n = Vector3.Normalize(n);
            Quad(root, wide + side, tip, wide - side, rgb, n);
            Quad(root, wide - side, tip, wide + side, rgb, -n);
        }
    }
}
