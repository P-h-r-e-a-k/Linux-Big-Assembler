namespace LBAAssembler.Terrain.Palace;

// The palace's upstairs (the user, 2026-10-15: "Let's add an upstairs to the inside of the palace. Let's keep the same basic colour theming,
// but maybe add a couple of bedrooms, a throne room, and any other rooms you can think of that would fit with a palace theme"). A second
// storey over the sixteen rooms, on the rooms' own grid -- each room of the ground floor a slot upstairs, 13 x 13 bricks inside its walls,
// the walls between them the same four-brick bands -- UpLayers high; the long hall's walls go up with it (a hall two storeys high, its
// round window where it was); the hill's top over the upstairs' roof now.
//   - Two of the ground floor's rooms open all the way up -- stairwells -- each with a ramp winding round a column in its middle (the race
//     track's raised road: palace_design.py): up from room 155, down into room 159.
//   - Upstairs, the rooms (Plan): the library over 151, the music room over 152, the princess's bedroom over 153, the Emperor's bedchamber
//     over 154, his dressing room and bath over 158, the throne room over 156, 157, 160 and 161 (one room, the walls between them gone),
//     the treasury over 162, the armoury over 166, the banquet hall over 164 and 165, the chapel over 163.
//   - The race's way through them (its doors, Doors: Main): out of the up-stairwell south into the library, east through the music room
//     and the two bedrooms, north through the dressing room into the throne room, past the throne and west into the down-stairwell. The
//     others (Main false): from the dressing room north through the treasury -- into the throne room -- and the armoury, west into the
//     banquet hall -- south into the throne room -- and the chapel past it.
//   - So that no way round the ground floor leaves the upstairs out, the ground floor's doors from the rooms the race comes down into (159,
//     163 and the long hall) to the rest are walled up (Closed).
// Everything here in the palace's own cells (bricks) and layers, the rooms by the scenes of the ground floor's rooms under them.
internal static class PalaceUpstairs
{
    public const int UpLayers = 11;                     // the upstairs' height (layers of 640: 7,040)
    public const int DoorLayers = 6;                    // a door's height upstairs
    public const int Inset = 4;                         // a slot's walls: bricks in from its extent (the ground floor's rooms are 17 bricks
                                                        // across their walls -- the four-brick bands they share with their neighbours both
                                                        // within it -- 9 inside, 36 cells)
    public const int Inside = 9;                        // a slot's inside, bricks
    public const int StairUp = 155, StairDown = 159;    // the stairwells (the ground floor's rooms, open to the roof)
    public const double StairRadius = 9;                // the ramp's middle line round the stairwell's middle (island cells): it and its
                                                        // rails 27 cells across in the 36 ...
    public const double ColumnRadius = 3.5;             // ... and the column in its middle
    // (each stairwell's middle: island cells in from its inside's north-west corner -- the ramp's foot on the ground floor's door's line,
    // 4.5 cells east of its middle, and its top on the upstairs door's, StairRadius west of it -- a cell and a half off its west wall)
    public const double StairX = 15.5, StairZ = 17.5;

    public enum Kind { Library, Music, Bedroom, Bedchamber, Dressing, Throne, Treasury, Armoury, Banquet, Chapel }

    // the rooms upstairs: their name, kind and the ground floor's rooms under them (more than one: one room, the walls between them gone)
    public static readonly (string Name, Kind Kind, int[] Under)[] Plan =
    {
        ("the library", Kind.Library, new[] { 151 }),
        ("the music room", Kind.Music, new[] { 152 }),
        ("the princess's bedroom", Kind.Bedroom, new[] { 153 }),
        ("the Emperor's bedchamber", Kind.Bedchamber, new[] { 154 }),
        ("the dressing room", Kind.Dressing, new[] { 158 }),
        ("the throne room", Kind.Throne, new[] { 156, 157, 160, 161 }),
        ("the treasury", Kind.Treasury, new[] { 162 }),
        ("the armoury", Kind.Armoury, new[] { 166 }),
        ("the banquet hall", Kind.Banquet, new[] { 164, 165 }),
        ("the chapel", Kind.Chapel, new[] { 163 }),
    };

    // The doors upstairs: from the room over `A` to the one over `B` (neighbours), its span along the wall (bricks of A's inside, from its
    // west or north), and whether it is on the race's way. The rows' and columns' doors at bricks 3..6 -- the ground floor's door lines,
    // two cells past a room's middle -- the stairwells' on their ramps' lines; the treasury's into the throne room two bricks south, clear of
    // the throne's dais.
    public static readonly (int A, int B, int U0, bool Main)[] Doors =
    {
        (StairUp, 151, 0, true),        // out of the up-stairwell (its ramp's top, west of its column) south into the library
        (151, 152, 3, true), (152, 153, 3, true), (153, 154, 3, true),
        (154, 158, 3, true),            // north into the dressing room ...
        (158, 157, 3, true),            // ... west into the throne room
        (160, StairDown, 0, true),      // out of the throne room west into the down-stairwell (its ramp's top, north of its column)
        (158, 162, 3, false), (162, 161, 5, false), (162, 166, 3, false), (166, 165, 3, false), (164, 160, 3, false), (164, 163, 3, false),
    };
    public const int DoorBricks = 4;

    // The ground floor's doors walled up: between the rooms the race comes down into and the rest.
    public static readonly (int A, int B)[] Closed = { (155, 159), (159, 160), (163, 164) };

    // the floor's paint upstairs: the race's way red, door to door, square-cornered, as wide as the doors -- and the ground floor's colours
    // round it: gold tiles along the red, the rest the tiles' greens; rugs in the rooms (Rugs) in their own colours
    public static readonly (float R, float G, float B) RugPurple = (92, 52, 110), RugBlue = (46, 66, 120), RugGold = (150, 110, 50);

    // The rugs: a room's kind, its corners (bricks of its inside: from its west and north edges, the far ones not in) and colour.
    private static readonly (Kind Room, int U0, int V0, int U1, int V1, (float R, float G, float B) Colour)[] Rugs =
    {
        (Kind.Bedroom, 0, 0, 3, 2, RugPurple), (Kind.Bedchamber, 0, 0, 3, 2, RugBlue), (Kind.Throne, 14, 5, 17, 15, RugPurple),
        (Kind.Library, 5, 0, 9, 2, RugGold), (Kind.Chapel, 1, 3, 9, 7, RugPurple), (Kind.Banquet, 5, 0, 21, 2, RugBlue),
    };

    // The furniture, room by room: each piece's kind, the middle of its foot (bricks of the room's inside, from its west and north edges:
    // a room 9 bricks across inside, the throne room 22 and the banquet hall 22 by 9), the way it looks, its depth and width (bricks), its
    // height (layers) -- in the strips the race's way leaves (Doors: the rows' and columns' doors at bricks 3..6, the way between them
    // square-cornered; gold tiles a brick either side of it).
    private static readonly Dictionary<Kind, (PalaceFurniture.Up Kind, double U, double V, int Nx, int Nz, double Depth, double Width, double High)[]> Furniture = new()
    {
        // (the way: from the north door at bricks 0..3 south to bricks 3..6, east along them -- the north-east block and the south strip free)
        [Kind.Library] = new (PalaceFurniture.Up, double, double, int, int, double, double, double)[]
        {
            (PalaceFurniture.Up.Bookshelf, 6.0, 0.4, 0, 1, 0.8, 1.8, 9), (PalaceFurniture.Up.Bookshelf, 7.95, 0.4, 0, 1, 0.8, 1.8, 9),
            (PalaceFurniture.Up.Bookshelf, 1.1, 8.6, 0, -1, 0.8, 1.8, 9), (PalaceFurniture.Up.Bookshelf, 3.05, 8.6, 0, -1, 0.8, 1.8, 9),
            (PalaceFurniture.Up.Bookshelf, 5.0, 8.6, 0, -1, 0.8, 1.8, 9), (PalaceFurniture.Up.Bookshelf, 6.95, 8.6, 0, -1, 0.8, 1.8, 9),
            (PalaceFurniture.Up.Globe, 7.0, 1.7, 0, 1, 0.7, 0.7, 4), (PalaceFurniture.Up.Candelabra, 8.6, 8.4, -1, 0, 0.5, 1.0, 4),
        },
        // (the way: west to east along bricks 3..6 -- the north strip and the south strip free)
        [Kind.Music] = new (PalaceFurniture.Up, double, double, int, int, double, double, double)[]
        {
            (PalaceFurniture.Up.Piano, 4.5, 1.0, 0, 1, 2.0, 1.5, 4), (PalaceFurniture.Up.Harp, 7.6, 1.0, 0, 1, 0.9, 0.9, 5),
            (PalaceFurniture.Up.MusicStand, 1.4, 1.5, 0, 1, 0.5, 0.7, 3),
            (PalaceFurniture.Up.Chair, 1.5, 8.4, 0, -1, 0.5, 0.5, 2), (PalaceFurniture.Up.Chair, 3.5, 8.4, 0, -1, 0.5, 0.5, 2),
            (PalaceFurniture.Up.Chair, 5.5, 8.4, 0, -1, 0.5, 0.5, 2), (PalaceFurniture.Up.Chair, 7.5, 8.4, 0, -1, 0.5, 0.5, 2),
            (PalaceFurniture.Up.Candelabra, 0.5, 0.5, 1, 0, 0.5, 1.0, 4), (PalaceFurniture.Up.Candelabra, 8.5, 0.5, -1, 0, 0.5, 1.0, 4),
        },
        // (the way: west to east along bricks 3..6)
        [Kind.Bedroom] = new (PalaceFurniture.Up, double, double, int, int, double, double, double)[]
        {
            (PalaceFurniture.Up.Bed, 1.2, 1.2, 1, 0, 2.4, 2.0, 7), (PalaceFurniture.Up.Wardrobe, 4.5, 0.45, 0, 1, 0.8, 1.7, 8),
            (PalaceFurniture.Up.Wardrobe, 6.3, 0.45, 0, 1, 0.8, 1.7, 8), (PalaceFurniture.Up.Plant, 8.2, 1.0, -1, 0, 1.0, 1.0, 4),
            (PalaceFurniture.Up.DressingTable, 4.5, 8.55, 0, -1, 0.8, 2.0, 6), (PalaceFurniture.Up.Plant, 0.8, 8.2, 1, 0, 1.0, 1.0, 4),
            (PalaceFurniture.Up.Plant, 8.2, 8.2, -1, 0, 1.0, 1.0, 4),
        },
        // (the way: from the west door along bricks 3..6 to the column at bricks 3..6, north up it -- the north-west block, the east strip and
        // the south strip free)
        [Kind.Bedchamber] = new (PalaceFurniture.Up, double, double, int, int, double, double, double)[]
        {
            (PalaceFurniture.Up.GrandBed, 1.15, 1.2, 1, 0, 2.3, 2.2, 7), (PalaceFurniture.Up.Wardrobe, 8.55, 1.2, -1, 0, 0.8, 1.7, 8),
            (PalaceFurniture.Up.Wardrobe, 8.55, 5.0, -1, 0, 0.8, 1.7, 8), (PalaceFurniture.Up.Fireplace, 4.0, 8.55, 0, -1, 0.9, 2.4, 6),
            (PalaceFurniture.Up.Chest, 8.4, 8.0, -1, 0, 0.8, 1.6, 2), (PalaceFurniture.Up.Plant, 0.8, 8.2, 1, 0, 1.0, 1.0, 4),
        },
        // (the way: from the south door up bricks 3..6 to the row at bricks 3..6, west along it -- the north-west block, the east strip and the
        // south-west block free)
        [Kind.Dressing] = new (PalaceFurniture.Up, double, double, int, int, double, double, double)[]
        {
            // (the others' way goes on north from the row, up bricks 3..6 to the treasury: the north strip's middle clear too)
            (PalaceFurniture.Up.Bathtub, 1.0, 1.4, 1, 0, 1.4, 2.6, 2), (PalaceFurniture.Up.Wardrobe, 8.55, 1.2, -1, 0, 0.8, 1.7, 8),
            (PalaceFurniture.Up.Wardrobe, 8.55, 4.6, -1, 0, 0.8, 1.7, 8), (PalaceFurniture.Up.Wardrobe, 8.55, 7.8, -1, 0, 0.8, 1.7, 8),
            (PalaceFurniture.Up.DressingTable, 1.2, 8.55, 0, -1, 0.8, 2.0, 6),
        },
        // (22 x 22: the way in from the east door along bricks 16..19, north up bricks 3..6, west out along bricks 0..3 -- the throne on the
        // north wall east of it, looking down the room; its columns in two rows between the others' way in from the treasury and the race's)
        [Kind.Throne] = new (PalaceFurniture.Up, double, double, int, int, double, double, double)[]
        {
            (PalaceFurniture.Up.Throne, 15.5, 2.3, 0, 1, 4.6, 7.0, 7),
            (PalaceFurniture.Up.Candelabra, 11.0, 1.0, 0, 1, 0.5, 1.0, 4), (PalaceFurniture.Up.Candelabra, 20.0, 1.0, 0, 1, 0.5, 1.0, 4),
            (PalaceFurniture.Up.Column, 11.0, 10.0, 1, 0, 1, 1, PalaceUpstairs.UpLayers), (PalaceFurniture.Up.Column, 20.0, 10.0, 1, 0, 1, 1, PalaceUpstairs.UpLayers),
            (PalaceFurniture.Up.Column, 11.0, 14.0, 1, 0, 1, 1, PalaceUpstairs.UpLayers), (PalaceFurniture.Up.Column, 20.0, 14.0, 1, 0, 1, 1, PalaceUpstairs.UpLayers),
            (PalaceFurniture.Up.Chandelier, 15.5, 9.0, 1, 0, 1, 1, PalaceUpstairs.UpLayers), (PalaceFurniture.Up.Chandelier, 15.5, 16.0, 1, 0, 1, 1, PalaceUpstairs.UpLayers),
            (PalaceFurniture.Up.Bench, 12.0, 21.5, 0, -1, 0.8, 2.4, 2), (PalaceFurniture.Up.Bench, 18.0, 21.5, 0, -1, 0.8, 2.4, 2),
            (PalaceFurniture.Up.Bench, 0.6, 11.0, 1, 0, 0.8, 2.4, 2),
            (PalaceFurniture.Up.Plant, 21.0, 21.0, -1, 0, 1.0, 1.0, 4), (PalaceFurniture.Up.Plant, 1.0, 21.0, 1, 0, 1.0, 1.0, 4), (PalaceFurniture.Up.Plant, 1.0, 6.5, 1, 0, 1.0, 1.0, 4),
        },
        // (the others' way: from the south door up bricks 3..6 and on north, out of the west door at bricks 3..6 -- the east strip and the
        // west corners free)
        [Kind.Treasury] = new (PalaceFurniture.Up, double, double, int, int, double, double, double)[]
        {
            (PalaceFurniture.Up.GoldPile, 1.2, 1.2, 1, 0, 1.8, 1.8, 2), (PalaceFurniture.Up.GoldPile, 1.0, 8.2, 1, 0, 1.4, 1.4, 2),
            (PalaceFurniture.Up.Pedestal, 8.3, 1.2, -1, 0, 0.7, 0.7, 3), (PalaceFurniture.Up.Pedestal, 8.3, 4.5, -1, 0, 0.7, 0.7, 3),
            (PalaceFurniture.Up.Pedestal, 8.3, 7.8, -1, 0, 0.7, 0.7, 3),
        },
        // (the others' way: from the south door up bricks 3..6 to the row at bricks 3..6, west along it -- the north strip, the east strip and the
        // south-west block free)
        [Kind.Armoury] = new (PalaceFurniture.Up, double, double, int, int, double, double, double)[]
        {
            (PalaceFurniture.Up.Armour, 1.0, 0.6, 0, 1, 0.8, 0.8, 7), (PalaceFurniture.Up.Armour, 3.0, 0.6, 0, 1, 0.8, 0.8, 7), (PalaceFurniture.Up.Armour, 5.0, 0.6, 0, 1, 0.8, 0.8, 7),
            (PalaceFurniture.Up.Armour, 7.0, 0.6, 0, 1, 0.8, 0.8, 7), (PalaceFurniture.Up.WeaponRack, 8.7, 4.5, -1, 0, 0.5, 2.0, 6),
            (PalaceFurniture.Up.WeaponRack, 8.7, 7.4, -1, 0, 0.5, 2.0, 6), (PalaceFurniture.Up.Armour, 1.0, 8.4, 0, -1, 0.8, 0.8, 7),
        },
        // (22 x 9: the others' way east to west along bricks 3..6, the throne room's door south at bricks 3..6 -- the north strip and the
        // south strip east of that door free)
        [Kind.Banquet] = new (PalaceFurniture.Up, double, double, int, int, double, double, double)[]
        {
            (PalaceFurniture.Up.BanquetTable, 13.0, 1.2, 0, 1, 1.2, 14.0, 2),
            (PalaceFurniture.Up.Chandelier, 9.0, 1.2, 0, 1, 1, 1, PalaceUpstairs.UpLayers), (PalaceFurniture.Up.Chandelier, 17.0, 1.2, 0, 1, 1, 1, PalaceUpstairs.UpLayers),
            (PalaceFurniture.Up.Fireplace, 15.0, 8.55, 0, -1, 0.9, 2.4, 6), (PalaceFurniture.Up.Plant, 21.0, 8.2, -1, 0, 1.0, 1.0, 4),
            (PalaceFurniture.Up.Plant, 8.6, 8.2, 1, 0, 1.0, 1.0, 4), (PalaceFurniture.Up.Plant, 1.0, 1.0, 1, 0, 1.0, 1.0, 4),
        },
        // (the others' way in from the east door along bricks 3..6: the aisle up to the altar)
        [Kind.Chapel] = new (PalaceFurniture.Up, double, double, int, int, double, double, double)[]
        {
            (PalaceFurniture.Up.Altar, 0.7, 4.5, 1, 0, 1.1, 2.6, 2), (PalaceFurniture.Up.Candle, 0.6, 2.6, 1, 0, 0.4, 0.4, 3), (PalaceFurniture.Up.Candle, 0.6, 6.4, 1, 0, 0.4, 0.4, 3),
            (PalaceFurniture.Up.Candelabra, 0.5, 0.6, 1, 0, 0.5, 1.0, 4), (PalaceFurniture.Up.Candelabra, 0.5, 8.4, 1, 0, 0.5, 1.0, 4),
            (PalaceFurniture.Up.Pew, 3.0, 1.2, -1, 0, 0.8, 2.2, 3), (PalaceFurniture.Up.Pew, 5.0, 1.2, -1, 0, 0.8, 2.2, 3), (PalaceFurniture.Up.Pew, 7.0, 1.2, -1, 0, 0.8, 2.2, 3),
            (PalaceFurniture.Up.Pew, 3.0, 8.2, -1, 0, 0.8, 1.4, 3), (PalaceFurniture.Up.Pew, 5.0, 8.2, -1, 0, 0.8, 1.4, 3), (PalaceFurniture.Up.Pew, 7.0, 8.2, -1, 0, 0.8, 1.4, 3),
        },
    };

    // The upstairs, made: its floor's layer (the ground floor's roof's) and its top layer, its rooms (each its tile -- a group of faces, a
    // room of the palace's -- and inside, bricks), the stairwells' middles (island cells), the floor's paint, the furniture, the cells the
    // furniture stands in (no picture behind it), the doors (island cells: x0, z0, x1, z1) and what it did.
    public sealed record Room(string Name, Kind Kind, int Tile, int[] Under, (int X0, int Z0, int X1, int Z1) Inside);
    public sealed record Stair(int Scene, double X, double Z, bool Up, (int X0, int Z0, int X1, int Z1) Inside);
    public sealed record DoorWay(int A, int B, double X0, double Z0, double X1, double Z1, bool Main);
    public sealed record Made(int FloorLayer, int TopLayer, List<Room> Rooms, List<Stair> Stairs, Dictionary<(int X, int Z), (float R, float G, float B)> Paint,
        List<PalaceFurniture.UpPiece> Pieces, HashSet<(int X, int Y, int Z)> Standing, List<DoorWay> DoorWays, int Closed, List<string> Log);

    // Adds the upstairs to the palace's cells (`cells`, and `shown`: the cells drawn as boxes -- the same dictionary or a copy without the
    // furniture's), its air to `air`: over the sixteen rooms (each ground tile's extent, bricks; `scenes` each tile's scene), `floorLayer`
    // the layer its floor fills (the ground floor's roof's); the long hall (`hallTile`) up with it. Its rooms' tiles from `firstTile` on.
    // (`cellX`, `cellZ`: a brick's edge to island cells; `paint`: the ground floor's colours -- red, the golds, the greens)
    public static Made Add(IReadOnlyList<int> scenes, (int X0, int Z0, int X1, int Z1)[] extents, int hallTile, int minY, int floorLayer,
        Dictionary<(int X, int Y, int Z), (int Brick, int Tile)> cells, Dictionary<(int X, int Y, int Z), (int Brick, int Tile)> shown, HashSet<(int X, int Y, int Z)> air,
        int firstTile, Func<double, double> cellX, Func<double, double> cellZ,
        ((float R, float G, float B) Red, (float R, float G, float B) GoldLight, (float R, float G, float B) GoldDark, (float R, float G, float B) GreenLight, (float R, float G, float B) GreenDark) paint)
    {
        var log = new List<string>();
        int Yf = floorLayer, Y1 = floorLayer + UpLayers;
        var tileOf = Enumerable.Range(0, scenes.Count).ToDictionary(ti => scenes[ti], ti => ti);
        (int X0, int Z0, int X1, int Z1) Inner(int scene) { var e = extents[tileOf[scene]]; return (e.X0 + Inset, e.Z0 + Inset, e.X1 - Inset, e.Z1 - Inset); }
        var sixteen = Plan.SelectMany(p => p.Under).Concat(new[] { StairUp, StairDown }).ToList();
        foreach (var s in sixteen)
        {
            var i = Inner(s);
            if (i.X1 - i.X0 + 1 != Inside || i.Z1 - i.Z0 + 1 != Inside) log.Add($"  WARNING: room {s}'s inside is {i.X1 - i.X0 + 1} x {i.Z1 - i.Z0 + 1} bricks, not {Inside} x {Inside}");
        }
        bool Contains((int X0, int Z0, int X1, int Z1) r, int x, int z) => x >= r.X0 && x <= r.X1 && z >= r.Z0 && z <= r.Z1;
        void Put((int X, int Y, int Z) c, int tile)
        {
            if (cells.TryAdd(c, (-1, tile)) && !ReferenceEquals(shown, cells)) shown[c] = (-1, tile);
            air.Remove(c);
        }

        // ---- the ground floor's doors walled up between the rooms the race comes down into and the rest: every empty cell of the band two
        // rooms' walls share, up to the roof
        var closed = 0;
        foreach (var (a, b) in Closed)
        {
            var (ea, eb) = (extents[tileOf[a]], extents[tileOf[b]]);
            int bx0 = Math.Max(ea.X0, eb.X0), bx1 = Math.Min(ea.X1, eb.X1), bz0 = Math.Max(ea.Z0, eb.Z0), bz1 = Math.Min(ea.Z1, eb.Z1);
            for (var x = bx0; x <= bx1; x++)
                for (var z = bz0; z <= bz1; z++)
                    for (var y = minY + 1; y < Yf; y++)
                        if (!cells.ContainsKey((x, y, z))) { Put((x, y, z), tileOf[a]); closed++; }
        }
        log.Add($"the ground floor's doors walled up: {string.Join(", ", Closed.Select(c => $"{c.A}-{c.B}"))} ({closed} cells) -- the rooms the race comes down into, " +
                $"{StairDown}, 163 and the long hall, only by the stairs");

        // ---- the rooms upstairs: a tile each, inside the box round the insides of the rooms under it
        var rooms = new List<Room>();
        var roomAt = new Dictionary<(int X, int Z), int>();
        var upTile = new Dictionary<int, int>();          // (a ground floor's room: the tile of the room over it)
        for (var k = 0; k < Plan.Length; k++)
        {
            var (name, kind, under) = Plan[k];
            var ins = under.Select(Inner).ToList();
            var inside = (ins.Min(i => i.X0), ins.Min(i => i.Z0), ins.Max(i => i.X1), ins.Max(i => i.Z1));
            rooms.Add(new Room(name, kind, firstTile + k, under, inside));
            foreach (var u in under) upTile[u] = firstTile + k;
            for (var x = inside.Item1; x <= inside.Item3; x++) for (var z = inside.Item2; z <= inside.Item4; z++) roomAt[(x, z)] = k;
        }
        var stairs = new List<Stair>();
        foreach (var (scene, up) in new[] { (StairUp, true), (StairDown, false) })
        {
            var i = Inner(scene);
            stairs.Add(new Stair(scene, cellX(i.X0) + StairX, cellZ(i.Z0) + StairZ, up, i));
            upTile[scene] = tileOf[scene];
        }
        var extentOf = sixteen.ToDictionary(s => s, s => extents[tileOf[s]]);
        int ux0 = extentOf.Values.Min(e => e.X0), ux1 = extentOf.Values.Max(e => e.X1), uz0 = extentOf.Values.Min(e => e.Z0), uz1 = extentOf.Values.Max(e => e.Z1);
        int TileAt(int x, int z)
        {
            if (roomAt.TryGetValue((x, z), out var k)) return rooms[k].Tile;
            foreach (var s in stairs) if (Contains(s.Inside, x, z)) return tileOf[s.Scene];
            foreach (var (s, e) in extentOf) if (Contains(e, x, z)) return upTile[s];
            return tileOf[sixteen[0]];
        }

        // ---- the doors upstairs: their openings (bricks) -- across the band between the two rooms' insides, along the wall from brick U0
        // of A's inside, DoorBricks wide
        var doorCells = new HashSet<(int X, int Z)>();
        var doorRects = new List<(int A, int B, (int X0, int Z0, int X1, int Z1) R, bool Main)>();
        foreach (var (a, b, u0, isMain) in Doors)
        {
            var (ia, ib) = (Inner(a), Inner(b));
            (int, int, int, int) r = ia.Z0 == ib.Z0
                ? (Math.Min(ia.X1, ib.X1) + 1, ia.Z0 + u0, Math.Max(ia.X0, ib.X0) - 1, ia.Z0 + u0 + DoorBricks - 1)     // side by side: a wall along z
                : (ia.X0 + u0, Math.Min(ia.Z1, ib.Z1) + 1, ia.X0 + u0 + DoorBricks - 1, Math.Max(ia.Z0, ib.Z0) - 1);    // one over the other
            doorRects.Add((a, b, r, isMain));
            for (var x = r.Item1; x <= r.Item3; x++) for (var z = r.Item2; z <= r.Item4; z++) doorCells.Add((x, z));
        }

        // ---- the cells: the floor -- the layer of the ground floor's roofs -- over all of it but the stairwells; over it the walls, but the
        // rooms' and the stairwells' insides and the doors, to the top
        var stairInside = new HashSet<(int X, int Z)>();
        foreach (var s in stairs) for (var x = s.Inside.X0; x <= s.Inside.X1; x++) for (var z = s.Inside.Z0; z <= s.Inside.Z1; z++) stairInside.Add((x, z));
        var walls = 0;
        for (var x = ux0; x <= ux1; x++)
            for (var z = uz0; z <= uz1; z++)
            {
                if (!extentOf.Values.Any(e => Contains(e, x, z))) continue;
                bool stair = stairInside.Contains((x, z)), room = roomAt.ContainsKey((x, z)), door = doorCells.Contains((x, z));
                var tile = TileAt(x, z);
                if (stair) air.Add((x, Yf, z)); else Put((x, Yf, z), tile);
                for (var y = Yf + 1; y <= Y1; y++)
                {
                    if (room || stair || (door && y <= Yf + DoorLayers)) air.Add((x, y, z));
                    else { Put((x, y, z), tile); walls++; }
                }
            }
        // ---- the long hall up with it: its walls' columns on up to the roof, its air too
        var eh = extents[hallTile];
        var hallUp = 0;
        for (var x = eh.X0; x <= eh.X1; x++)
            for (var z = eh.Z0; z <= eh.Z1; z++)
            {
                if (extentOf.Values.Any(e => Contains(e, x, z))) continue;
                bool wall = cells.ContainsKey((x, Yf - 1, z)) || cells.ContainsKey((x, Yf - 2, z)), open = air.Contains((x, Yf - 1, z));
                for (var y = Yf; y <= Y1; y++)
                {
                    if (wall) { Put((x, y, z), hallTile); hallUp++; }
                    else if (open) air.Add((x, y, z));
                }
            }
        log.Add($"the upstairs: {rooms.Count} rooms ({string.Join(", ", rooms.Select(r => $"{r.Name} over {string.Join("+", r.Under)}"))}), the stairwells {StairUp} " +
                $"(up) and {StairDown} (down) open to the roof; its floor layer {Yf} ({UpLayers} layers to its top), {walls} cells of walls, {doorRects.Count} doors " +
                $"({doorRects.Count(d => d.Main)} on the race's way); the long hall two storeys high ({hallUp} cells of wall more)");

        // ---- the floor's paint: the race's way red (the main doors in turn: from one door's line to the next's in each room, square-cornered;
        // the throne room's two doors on different rows, a column between them), gold along it, rugs, the greens
        var red = new HashSet<(int X, int Z)>();
        void Rect(int x0, int z0, int x1, int z1) { for (var x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++) for (var z = Math.Min(z0, z1); z <= Math.Max(z0, z1); z++) red.Add((x, z)); }
        var main = doorRects.Where(d => d.Main).ToList();
        foreach (var d in main) Rect(d.R.X0, d.R.Z0, d.R.X1, d.R.Z1);
        for (var k = 0; k + 1 < main.Count; k++)
        {
            var (din, dout) = (main[k], main[k + 1]);
            var room = rooms.FirstOrDefault(r => r.Under.Contains(din.B) && r.Under.Contains(dout.A));
            if (room is null) continue;
            var ins = room.Inside;
            // (a door's way across the room: along x through a wall along z -- its span z -- or the other way)
            bool AlongX((int X0, int Z0, int X1, int Z1) r) => r.X1 < ins.X0 || r.X0 > ins.X1;
            var (ri, ro) = (din.R, dout.R);
            if (AlongX(ri) != AlongX(ro))
            {
                // (an L: in along the first door's line as far as the second's, out along the second's from the first's)
                if (!AlongX(ri))
                {
                    var fromNorth = ri.Z1 < ins.Z0;
                    Rect(ri.X0, fromNorth ? ins.Z0 : ro.Z0, ri.X1, fromNorth ? ro.Z1 : ins.Z1);
                    var toEast = ro.X0 > ins.X1;
                    Rect(toEast ? ri.X0 : ins.X0, ro.Z0, toEast ? ins.X1 : ri.X1, ro.Z1);
                }
                else
                {
                    var fromWest = ri.X1 < ins.X0;
                    Rect(fromWest ? ins.X0 : ro.X0, ri.Z0, fromWest ? ro.X1 : ins.X1, ri.Z1);
                    var toNorth = ro.Z1 < ins.Z0;
                    Rect(ro.X0, toNorth ? ins.Z0 : ri.Z0, ro.X1, toNorth ? ri.Z1 : ins.Z1);
                }
            }
            else if (AlongX(ri) && (ri.Z0, ri.Z1) != (ro.Z0, ro.Z1))
            {
                // (the throne room: in along the first door's row to the column of its west room's doors, along that to the second's row, out)
                var west = room.Under.Select(Inner).Min(i => i.X0);
                int c0 = west + 3, c1 = west + 6;
                Rect(c0, ri.Z0, ins.X1, ri.Z1);
                Rect(c0, Math.Min(ri.Z0, ro.Z0), c1, Math.Max(ri.Z1, ro.Z1));
                Rect(ins.X0, ro.Z0, c1, ro.Z1);
            }
            else if (AlongX(ri)) Rect(ins.X0, ri.Z0, ins.X1, ri.Z1);
            else Rect(ri.X0, ins.Z0, ri.X1, ins.Z1);
        }
        var floor = new HashSet<(int X, int Z)>();
        foreach (var ((x, y, z), _) in cells) if (y == Yf && air.Contains((x, Yf + 1, z))) floor.Add((x, z));
        var colours = new Dictionary<(int X, int Z), (float R, float G, float B)>();
        foreach (var c in floor)
        {
            if (red.Contains(c)) { colours[c] = paint.Red; continue; }
            var edge = false;
            for (var dx = -1; dx <= 1 && !edge; dx++) for (var dz = -1; dz <= 1 && !edge; dz++) edge = red.Contains((c.X + dx, c.Z + dz));
            var light = ((c.X + c.Z) & 1) == 0;
            colours[c] = edge ? (light ? paint.GoldLight : paint.GoldDark) : light ? paint.GreenLight : paint.GreenDark;
        }
        foreach (var (kind, u0, v0, u1, v1, colour) in Rugs)
            foreach (var room in rooms.Where(r => r.Kind == kind))
                for (var x = room.Inside.X0 + u0; x < room.Inside.X0 + u1; x++)
                    for (var z = room.Inside.Z0 + v0; z < room.Inside.Z0 + v1; z++)
                        if (floor.Contains((x, z)) && !red.Contains((x, z)) && colours[(x, z)] != paint.GoldLight && colours[(x, z)] != paint.GoldDark) colours[(x, z)] = colour;

        // ---- the furniture: each room's, at its place in the room; the stairwells' columns, from the ground floor's floor up to the roof
        var pieces = new List<PalaceFurniture.UpPiece>();
        var standing = new HashSet<(int X, int Y, int Z)>();
        var inTheWay = new List<string>();
        foreach (var room in rooms)
        {
            if (!Furniture.TryGetValue(room.Kind, out var list)) continue;
            foreach (var (kind, u, v, nx, nz, depth, width, high) in list)
            {
                double x = room.Inside.X0 + u, z = room.Inside.Z0 + v;
                double hx = (nx != 0 ? depth : width) / 2, hz = (nx != 0 ? width : depth) / 2;
                var solid = kind is not (PalaceFurniture.Up.Chandelier or PalaceFurniture.Up.Candle);
                pieces.Add(new PalaceFurniture.UpPiece(kind, room.Tile, x, Yf + 1, z, nx, nz, depth, width, kind == PalaceFurniture.Up.Chandelier ? UpLayers : high, solid));
                if (kind == PalaceFurniture.Up.Chandelier) continue;
                for (var cx = (int)Math.Floor(x - hx); cx <= (int)Math.Floor(x + hx - 1e-6); cx++)
                    for (var cz = (int)Math.Floor(z - hz); cz <= (int)Math.Floor(z + hz - 1e-6); cz++)
                    {
                        if (red.Contains((cx, cz))) inTheWay.Add($"{kind} in {room.Name} at ({cx}, {cz})");
                        for (var y = Yf + 1; y <= Yf + Math.Max(1, (int)Math.Ceiling(high)); y++) standing.Add((cx, y, cz));
                    }
            }
        }
        foreach (var s in stairs)
            pieces.Add(new PalaceFurniture.UpPiece(PalaceFurniture.Up.StairColumn, s.Up ? tileOf[StairUp] : tileOf[StairDown], s.Inside.X0 + StairX / 4, minY + 1, s.Inside.Z0 + StairZ / 4,
                1, 0, ColumnRadius * 2 / 4, ColumnRadius * 2 / 4, Y1 + 1 - (minY + 1)));
        if (inTheWay.Count > 0) log.Add($"  WARNING: furniture on the race's way: {string.Join("; ", inTheWay.Distinct().Take(8))}");

        var doorWays = doorRects.Select(d => new DoorWay(d.A, d.B, cellX(d.R.X0), cellZ(d.R.Z0), cellX(d.R.X1 + 1), cellZ(d.R.Z1 + 1), d.Main)).ToList();
        return new Made(Yf, Y1, rooms, stairs, colours, pieces, standing, doorWays, closed, log);
    }
}
