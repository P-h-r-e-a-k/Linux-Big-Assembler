using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// A car for the best-known character of each of the game's islands (the Queen's, the Emperor's and Zoe's are in the other file, Baldino's in
// RaceTrackBaldinoCar, and the Desert island's racer has the game's own):
//
//   Citadel Island              the weather wizard in a thundercloud; Raph the lighthouse keeper in a lighthouse boat
//   Desert Island               the Dean of the School of Magic in a wizard's hat
//   Emerald Moon                an Esmer in his space suit in a moon rover
//   Otringal                    Johnny Rocket, the first explorer to set foot on Twinsun, in his rocket
//   Celebration Island          the Dark Monk in a car of temple stone
//   Island of the Wannies       a Wannie in a mine cart of gems
//   Otringal again              the old Franco in his Leontine, the boat under a balloon (he waits with it in Otringal's harbour)
//   Island CX                   the Franco survivor on a raft
//
// (The Island of the Mosquibees has its Queen. RaceTrackCharacterCars.More.cs brings every island to three drivers or more.)
internal static partial class RaceTrackCharacterCars
{
    public static readonly Car WeatherWizard = new("The weather wizard's car", "the weather wizard", 111, 5, BuildWeatherWizard, Citadel);
    public static readonly Car Raph = new("Raph's car", "Raph the lighthouse keeper", 32, 6, BuildRaph, Citadel);
    public static readonly Car Dean = new("The Dean's car", "the Dean of the School of Magic", 123, 7, BuildDean, Desert);
    public static readonly Car Spaceman = new("The moon rover", "an Esmer in his space suit", 255, 8, BuildSpaceman, Moon);
    public static readonly Car Johnny = new("Johnny Rocket's rocket", "Johnny Rocket", 154, 9, BuildRocket, Otringal);
    public static readonly Car DarkMonk = new("The Dark Monk's car", "the Dark Monk", 311, 10, BuildDarkMonk, Celebration);
    public static readonly Car Wannie = new("The Wannie's mine cart", "a Wannie", 194, 11, BuildWannie, Wannies);
    public static readonly Car OldFranco = new("The Leontine", "the old Franco", 276, 12, BuildOldFranco, Otringal);
    public static readonly Car Survivor = new("The survivor's raft", "the Franco survivor of Island CX", 90, 13, BuildSurvivor, IslandCX);

    // More ramp starts (lit), soft-lit starts (with SoftShare) and colours drawn as they are.
    private const int Brown = 18, BrownDark = 16, Wood = 20, Cloud = 181, Stone = 210, Iron = 226, Cream = 37, Green = 130, Steel = 52;
    private const float SoftShare = 0.26f;
    private const int White = 60, RedSoft = 70, GoldSoft = 106, GreySoft = 56;
    private const int Yellow = 245, Amber = 244, Cyan = 172, WhiteFlat = 62, CreamFlat = 45, RainBlue = 201, LavaLine = 242;

    // The usual three ways a rabbibunny's, a Franco's or a Sup's body is cut at the waist and sat at the wheel.
    private static (Vector3 Right, Vector3 Left) Sit(CarMesh m, Body body, Hull hull, float seatZ, float rimTop, int[] keep, (int, int[]) right, (int, int[]) left,
        Vector3 origin, float scale, CarDriver.Painter? paint = null, float reach = 215, float gripHeight = 95, float gripX = 64)
        => SeatDriver(m, body, new HashSet<int>(keep), right, left, origin, scale, new Vector3(0, rimTop - 6, seatZ), rimTop,
                      new Vector3(gripX, rimTop - 6 + gripHeight, seatZ + reach), paint);

    // ---------------------------------------------------------------------------------------------------------------- Citadel Island

    // The weather wizard's car: the thundercloud he keeps over the island, with its lightning and its rain -- and the sun coming out behind.
    public static Body BuildWeatherWizard(Body wizard, Body racer)
    {
        var m = new CarMesh(WeatherWizard.Name);
        Roots(m);
        var hull = new Hull(m, 330, 10, 2, 0,
            (600, 140, 110, 100), (500, 250, 190, 150), (330, 320, 235, 165), (120, 345, 250, 170), (-100, 350, 250, 170), (-300, 325, 235, 165), (-470, 250, 185, 150), (-570, 140, 105, 100));
        hull.Skin(m, (_, _) => Cloud);
        hull.Nose(m, 650, Cloud);
        hull.Tail(m, -615, Cloud);

        // the cloud's puffs along its shoulders and over its tail, light against the dark belly
        foreach (var side in new[] { 1f, -1f })
        {
            foreach (var (z, r) in new[] { (470f, 80), (300, 100), (110, 95), (-290, 105), (-450, 85) })
                m.Ball(2, new(side * (hull.Side(hull.Cy + 120, z) + 10), hull.Cy + 135, z), r, WhiteFlat - 1);
            // a lightning bolt down each flank
            var x = side * (hull.Side(hull.Cy, 40) + 14);
            m.Flat(2, Amber, new(x, hull.Cy + 120, 120), new(x, hull.Cy + 120, 20), new(x, hull.Cy - 20, -20), new(x, hull.Cy - 20, 70));
            m.Flat(2, Amber, new(x, hull.Cy, 10), new(x, hull.Cy, 100), new(x, hull.Cy - 190, 30));
            // and the rain under it
            foreach (var z in new[] { 200f, 0, -200 })
                m.Rod(2, new(side * 150, hull.Cy - 150, z), new(side * 150 - 25, 70, z - 50), RainBlue);
        }
        foreach (var (x, z, r) in new[] { (0f, -400f, 110), (-120, -520, 80), (130, -510, 85) }) m.Ball(2, hull.OnTop(x, z, 30), r, WhiteFlat);
        // the sun behind it, on a gold mast
        {
            var sun = new Vector3(0, 850, -500);
            m.Rod(2, hull.OnTop(0, -500), sun, GoldFlat);
            m.Ball(2, sun, 75, Yellow);
            for (var i = 0; i < 8; i++)
            {
                var a = i * MathF.Tau / 8;
                m.Rod(2, sun + new Vector3(MathF.Cos(a), MathF.Sin(a), 0) * 95, sun + new Vector3(MathF.Cos(a), MathF.Sin(a), 0) * 150, Amber);
            }
        }

        const float SeatZ = -60;
        var rimTop = hull.Top(0, SeatZ) + 26;
        Cockpit(m, hull, SeatZ, 215, 185, rimTop, 10, Cloud + 1);
        Windscreen(m, hull, 265, 195, 100, WhiteFlat);
        Wheels(m, new WheelLook(Steel, 22, Grey, 1, Cloud + 1, 1, Yellow),
            new Axle(new Vector3(250, 270, 400), new Vector3(440, 155, 400), 155, 105), new Axle(new Vector3(270, 275, -360), new Vector3(505, 185, -360), 185, 130));
        var hands = Sit(m, wizard, hull, SeatZ, rimTop, new[] { 3, 4, 5, 6, 7, 8, 9, 16, 17, 18, 19, 20 }, (4, new[] { 5, 6 }), (7, new[] { 8, 9 }), new Vector3(0, 715, -90), 0.72f);
        SteeringWheel(m, hull, hands.Right, hands.Left, Gold, GoldFlat);
        return m.ToBody(racer.Header);
    }

    // Raph the lighthouse keeper's car: a boat in the lighthouse's red and white, the lighthouse itself standing on its stern.
    public static Body BuildRaph(Body raph, Body racer)
    {
        var m = new CarMesh(Raph.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 300, 12, 3, 0.5f,
            (650, 110, 90, 95), (570, 225, 138, 125), (390, 285, 160, 135), (160, 300, 170, 140), (-140, 300, 170, 140), (-390, 280, 165, 138), (-550, 230, 140, 125), (-625, 150, 100, 100));
        hull.Skin(m, (band, _) => band % 2 == 0 ? RedSoft : White);
        hull.Nose(m, 690, RedSoft, 10);
        hull.Tail(m, -650, RedSoft);

        // the lighthouse: a tapering tower in bands, a gallery, the lantern's glass round its lamp, a red roof
        {
            const float Z = -400; const int N = 6;
            var levels = new (float Y, float R)[] { (hull.Top(0, Z) - 10, 105), (610, 95), (740, 86), (870, 78) };
            var rings = levels.Select(l => m.Loop(2, new Vector3(0, l.Y, Z), new Vector3(l.R, 0, 0), new Vector3(0, 0, l.R), N)).ToArray();
            for (var i = 0; i + 1 < rings.Length; i++) { var band = i; m.Skin(rings[i], rings[i + 1], _ => band == 1 ? RedSoft : White, new Vector3(0, (levels[i].Y + levels[i + 1].Y) / 2, Z)); }
            m.Light = 1;
            var gallery = m.Loop(2, new Vector3(0, 875, Z), new Vector3(112, 0, 0), new Vector3(0, 0, 112), N);
            for (var k = 0; k < N; k++) m.Line(gallery[k], gallery[(k + 1) % N], Dark);
            var glassFoot = m.Loop(2, new Vector3(0, 880, Z), new Vector3(70, 0, 0), new Vector3(0, 0, 70), N);
            var glassHead = m.Loop(2, new Vector3(0, 990, Z), new Vector3(70, 0, 0), new Vector3(0, 0, 70), N);
            for (var k = 0; k < N; k++) m.Both(new[] { glassFoot[k], glassFoot[(k + 1) % N], glassHead[(k + 1) % N], glassHead[k] }, Pane, CarMesh.SeeThrough);
            m.Ball(2, new(0, 935, Z), 46, Yellow);
            var eaves = m.Loop(2, new Vector3(0, 990, Z), new Vector3(92, 0, 0), new Vector3(0, 0, 92), N);
            var top = m.P(2, new(0, 1090, Z));
            m.Cap(eaves, top, Red, new Vector3(0, 960, Z));
            m.Sphere(top, 18, GoldFlat);
        }
        m.Light = 1;
        foreach (var side in new[] { 1f, -1f })
        {
            // a life buoy on each side, lamps, a rope along the gunwale
            var at = new Vector3(side * (hull.Side(hull.Cy + 40, 120) + 6), hull.Cy + 40, 120);
            m.Ball(2, at, 78, RedFlat + 2); m.Ball(2, at + new Vector3(side * 8, 0, 0), 50, WhiteFlat); m.Ball(2, at + new Vector3(side * 16, 0, 0), 24, RedFlat + 2);
            m.Ball(2, new(side * 150, hull.Cy + 45, 615), 48, Yellow);
            for (var i = 1; i + 2 < hull.Rings.Count; i++) { var k = side > 0 ? 1 : 10; m.Line(hull.Rings[i][k], hull.Rings[i + 1][k], 22); }
        }

        const float SeatZ = 20;
        var rimTop = hull.Top(0, SeatZ) + 26;
        Cockpit(m, hull, SeatZ, 215, 185, rimTop, 10, Wood);
        Windscreen(m, hull, 330, 195, 100, WhiteFlat);
        Wheels(m, new WheelLook(Steel, 22, Grey, 1, White, SoftShare, RedFlat + 2),
            new Axle(new Vector3(250, 262, 410), new Vector3(435, 155, 410), 155, 105), new Axle(new Vector3(265, 266, -330), new Vector3(495, 180, -330), 180, 125));
        var hands = Sit(m, raph, hull, SeatZ, rimTop, new[] { 3, 4, 5, 6, 7, 8, 9, 16, 17, 18, 19, 20 }, (4, new[] { 5, 6 }), (7, new[] { 8, 9 }), new Vector3(0, 715, -90), 0.72f);
        SteeringWheel(m, hull, hands.Right, hands.Left, Wood, 22);
        return m.ToBody(racer.Header);
    }

    // ---------------------------------------------------------------------------------------------------------------- Desert Island

    // The Dean of the School of Magic's car: a wizard's hat on its side -- the point curling up at the front, the brim round the back -- blue
    // with gold stars and a moon, a crystal ball on the bonnet, and sparks of magic behind.
    public static Body BuildDean(Body dean, Body racer)
    {
        var m = new CarMesh(Dean.Name);
        Roots(m);
        var hull = new Hull(m, 315, 10, 2, 0,
            (520, 120, 100, 92), (420, 165, 130, 112), (250, 215, 162, 132), (50, 262, 190, 150), (-150, 300, 210, 162), (-330, 325, 222, 168), (-470, 340, 228, 170), (-540, 340, 228, 170));
        hull.Skin(m, (band, _) => band == 6 ? Gold : Blue);
        // the point of the hat: on from the nose, thinner and turning up
        {
            var a = m.Loop(2, new Vector3(0, hull.Cy + 30, 610), new Vector3(0, 68, 22), new Vector3(78, 0, 0), 10);
            var b = m.Loop(2, new Vector3(0, hull.Cy + 100, 690), new Vector3(0, 34, 28), new Vector3(44, 0, 0), 10);
            m.Skin(hull.Rings[0], a, _ => Blue, new Vector3(0, hull.Cy + 10, 565));
            m.Skin(a, b, _ => Blue, new Vector3(0, hull.Cy + 60, 650));
            m.Cap(b, m.P(2, new(0, hull.Cy + 215, 715)), Blue, new Vector3(0, hull.Cy + 100, 690));
            m.Ball(2, new(0, hull.Cy + 225, 715), 26, Yellow);
        }
        // the brim: a wide ring round the back, the hat's gold band in front of it
        {
            var brim = m.Ring(2, new Vector3(0, hull.Cy, -580), 500, 360, 235, 10, 2);
            for (var k = 0; k < 10; k++) m.Both(new[] { hull.Rings[^1][k], hull.Rings[^1][(k + 1) % 10], brim[(k + 1) % 10], brim[k] }, Blue);
            hull.Tail(m, -560, Blue + 1);
        }
        foreach (var side in new[] { 1f, -1f })
        {
            // stars on its sides, a moon (a gold disc, a blue one over most of it)
            foreach (var (z, y, r) in new[] { (380f, 50f, 20), (230, 110, 17), (110, 10, 24), (-190, 150, 16), (-260, 20, 22), (-400, 130, 18) })
                m.Ball(2, new(side * (hull.Side(hull.Cy + y, z) + 4), hull.Cy + y, z), r, Yellow);
            var moon = new Vector3(side * (hull.Side(hull.Cy + 70, -60) + 6), hull.Cy + 70, -60);
            m.Ball(2, moon, 46, Yellow);
            m.Ball(2, moon + new Vector3(side * 8, 12, 20), 38, Blue + 7);
        }
        // the crystal ball on the bonnet, on its gold foot; sparks behind
        m.Strut(2, hull.OnTop(0, 360, -8), hull.OnTop(0, 360, 40), 30, Gold);
        m.Ball(2, hull.OnTop(0, 360, 92), 58, Cyan);
        m.Ball(2, hull.OnTop(-14, 372, 108), 16, WhiteFlat);
        foreach (var (x, y, z, r, c) in new[] { (60f, 40f, -660f, 26, Yellow), (-90, -30, -700, 18, WhiteFlat), (20, 110, -740, 14, Yellow), (-40, 10, -790, 10, WhiteFlat) })
            m.Ball(2, new(x, hull.Cy + y, z), r, c);

        const float SeatZ = -90;
        var rimTop = hull.Top(0, SeatZ) + 26;
        Cockpit(m, hull, SeatZ, 210, 180, rimTop, 10, Gold);
        Windscreen(m, hull, 210, 170, 100, GoldFlat);
        Wheels(m, new WheelLook(Gold, 22, Grey, 1, Blue + 2, 1, Yellow),
            new Axle(new Vector3(150, 265, 380), new Vector3(400, 150, 380), 150, 105), new Axle(new Vector3(275, 270, -300), new Vector3(520, 190, -300), 190, 135));
        var hands = Sit(m, dean, hull, SeatZ, rimTop, new[] { 3, 4, 5, 6, 7, 8, 9, 16, 17, 18, 19, 20 }, (4, new[] { 5, 6 }), (7, new[] { 8, 9 }), new Vector3(0, 715, -90), 0.72f);
        SteeringWheel(m, hull, hands.Right, hands.Left, Gold, GoldFlat);
        return m.ToBody(racer.Header);
    }

    // ---------------------------------------------------------------------------------------------------------------- Emerald Moon

    // The moon rover: a white box wrapped in gold foil, solar panels out either side, a dish and a whip aerial, all its wheels alike.
    public static Body BuildSpaceman(Body sup, Body racer)
    {
        var m = new CarMesh(Spaceman.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 300, 12, 5, 0.5f,
            (570, 225, 105, 100), (510, 280, 128, 112), (200, 290, 140, 118), (-250, 290, 140, 118), (-460, 280, 132, 112), (-530, 225, 105, 100));
        hull.Skin(m, (_, k) => k is 2 or 8 ? GoldSoft : k is 3 or 4 or 5 or 6 or 7 ? GreySoft : White);
        hull.Nose(m, 585, GreySoft);
        hull.Tail(m, -545, GreySoft);
        m.Light = 1;

        foreach (var side in new[] { 1f, -1f })
        {
            // a solar panel: blue, ruled
            var p = m.Plate(2, Blue + 2, new(side * 295, 420, -120), new(side * 590, 500, -120), new(side * 590, 500, -420), new(side * 295, 420, -420));
            m.Line(m.P(2, new(side * 442, 461, -120)), m.P(2, new(side * 442, 461, -420)), 205);
            m.Line(m.P(2, new(side * 295, 421, -270)), m.P(2, new(side * 590, 501, -270)), 205);
            _ = p;
            m.Ball(2, new(side * 170, hull.Cy + 40, 575), 46, Yellow);
        }
        // the dish on its mast, looking up and forward; a whip aerial with a red ball
        {
            var foot = hull.OnTop(-150, -400); var head = foot + new Vector3(0, 300, 0);
            m.Rod(2, foot, head, WhiteFlat);
            var axis = Vector3.Normalize(new Vector3(0, 0.55f, 1));
            var u = Vector3.UnitX; var v = Vector3.Normalize(Vector3.Cross(axis, u));
            var rim = m.Loop(2, head + axis * 50, u * 135, v * 135, 8);
            var back = m.P(2, head);
            for (var k = 0; k < 8; k++) m.Both(new[] { rim[k], rim[(k + 1) % 8], back }, Steel);
            m.Rod(2, head, head + axis * 120, Dark);
            m.Ball(2, head + axis * 125, 16, RedFlat + 2);
            m.Rod(2, hull.OnTop(190, -440), hull.OnTop(190, -440, 420), WhiteFlat);
            m.Ball(2, hull.OnTop(190, -440, 430), 22, RedFlat + 2);
        }

        const float SeatZ = -40;
        var rimTop = hull.Top(0, SeatZ) + 24;
        Cockpit(m, hull, SeatZ, 235, 200, rimTop, 10, Steel);
        var wheel = new Axle(new Vector3(255, 262, 390), new Vector3(470, 175, 390), 175, 125);
        Wheels(m, new WheelLook(Steel, 24, Grey + 1, 1, Gold, 1, WhiteFlat), wheel, wheel with { Mount = new Vector3(255, 262, -380), Hub = new Vector3(470, 175, -380) });
        var hands = Sit(m, sup, hull, SeatZ, rimTop, new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, (10, new[] { 11, 12 }), (7, new[] { 8, 9 }), new Vector3(0, 700, 0), 0.66f, reach: 230, gripX: 80);
        SteeringWheel(m, hull, hands.Right, hands.Left, Steel, Dark);
        return m.ToBody(racer.Header);
    }

    // ---------------------------------------------------------------------------------------------------------------- Otringal

    // Johnny Rocket's car -- "Zeelich's brightest star, the first explorer to set foot on Twinsun": the rocket he did it in, silver with a
    // green nose and fins and a red band, a star on each side, its engine lit.
    public static Body BuildRocket(Body johnny, Body racer)
    {
        var m = new CarMesh(Johnny.Name);
        Roots(m);
        var hull = new Hull(m, 320, 10, 2, 0,
            (590, 105, 95, 92), (450, 195, 170, 155), (230, 250, 212, 178), (-120, 262, 220, 182), (-380, 230, 198, 170), (-510, 170, 150, 138));
        hull.Skin(m, (band, _) => band == 0 ? Green : band == 2 ? Red : Steel);
        hull.Nose(m, 800, Green);
        // the engine's bell and its flame, three fins round it
        {
            var bell = m.Loop(2, new Vector3(0, hull.Cy, -600), new Vector3(0, 205, 0), new Vector3(205, 0, 0), 10);
            m.Skin(hull.Rings[^1], bell, _ => Steel - 2, new Vector3(0, hull.Cy, -555));
            var throat = m.P(2, new(0, hull.Cy, -520));
            for (var k = 0; k < 10; k++) m.In(new[] { bell[k], bell[(k + 1) % 10], throat }, HoleDark, new Vector3(0, hull.Cy, -700), unlit: true);
            Flame(m, new Vector3(0, hull.Cy, -575), 130, 260);
            foreach (var (dx, dy) in new[] { (1f, 0.25f), (-1f, 0.25f), (0f, 1f) })
            {
                var o = new Vector3(dx, dy, 0); var root = new Vector3(dx * 215, hull.Cy + dy * 200, 0);
                m.Plate(2, Green, root + new Vector3(0, 0, -250), root + o * 280 + new Vector3(0, 0, -520), root + o * 290 + new Vector3(0, 0, -680), root + new Vector3(0, 0, -500) - o * 40);
            }
        }
        foreach (var side in new[] { 1f, -1f })
        {
            // a porthole, and his star
            m.Ball(2, new(side * (hull.Side(hull.Cy + 60, 330) + 4), hull.Cy + 60, 330), 50, Cyan);
            var star = new Vector3(side * (hull.Side(hull.Cy + 30, -250) + 8), hull.Cy + 30, -250);
            m.Ball(2, star, 40, Yellow);
            for (var i = 0; i < 5; i++)
            {
                var a = i * MathF.Tau / 5 + 0.3f;
                m.Flat(2, Yellow, star + new Vector3(0, MathF.Cos(a - 0.45f), MathF.Sin(a - 0.45f)) * 36, star + new Vector3(0, MathF.Cos(a + 0.45f), MathF.Sin(a + 0.45f)) * 36, star + new Vector3(0, MathF.Cos(a), MathF.Sin(a)) * 105);
            }
        }

        const float SeatZ = -90;
        var rimTop = hull.Top(0, SeatZ) + 26;
        Cockpit(m, hull, SeatZ, 200, 175, rimTop, 10, Green + 2);
        Windscreen(m, hull, 210, 170, 95, WhiteFlat);
        Wheels(m, new WheelLook(Steel, 24, Grey, 1, Green, 1, RedFlat + 2),
            new Axle(new Vector3(200, 268, 400), new Vector3(410, 150, 400), 150, 105), new Axle(new Vector3(215, 270, -300), new Vector3(470, 185, -300), 185, 130));
        var hands = Sit(m, johnny, hull, SeatZ, rimTop, new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 19, 20, 21, 22, 23, 24, 25 }, (7, new[] { 8, 9 }), (10, new[] { 11, 12 }), new Vector3(0, 605, 0), 0.8f);
        SteeringWheel(m, hull, hands.Right, hands.Left, Steel, WhiteFlat);
        return m.ToBody(racer.Header);
    }

    // ---------------------------------------------------------------------------------------------------------------- Celebration Island

    // The Dark Monk's car: a block of his temple's stone, red eyes in front, lava in its seams, a fire bowl on each corner of its tail and his
    // key standing between them.
    public static Body BuildDarkMonk(Body monk, Body racer)
    {
        var m = new CarMesh(DarkMonk.Name);
        Roots(m);
        var hull = new Hull(m, 300, 12, 6, 0.5f,
            (630, 205, 118, 108), (565, 268, 150, 125), (250, 285, 166, 130), (-350, 285, 166, 130), (-525, 268, 150, 125), (-605, 205, 118, 108));
        hull.Skin(m, (band, k) => k is 11 ? Stone + 2 : Stone);
        hull.Nose(m, 640, Stone);
        hull.Tail(m, -615, Stone);
        foreach (var side in new[] { 1f, -1f })
        {
            m.Ball(2, new(side * 95, hull.Cy + 30, 640), 40, RedFlat + 2);                 // the eyes, a spark in each
            m.Ball(2, new(side * 95, hull.Cy + 30, 650), 14, Amber);
            // lava in the seam along each flank
            for (var i = 0; i + 1 < hull.Rings.Count; i++) { var k = side > 0 ? 3 : 8; m.Line(hull.Rings[i][k], hull.Rings[i + 1][k], LavaLine); }
            // a horn on each front corner, curving in
            var foot = hull.OnTop(side * 215, 520, -10);
            var knee = m.Strut(2, foot, foot + new Vector3(side * 50, 150, 30), 44, Stone + 2);
            _ = knee;
            m.Cap(m.Loop(2, foot + new Vector3(side * 50, 150, 30), new Vector3(22, 0, 0), new Vector3(0, 0, 22), 4), m.P(2, foot + new Vector3(side * -10, 300, 70)), Stone + 3, foot + new Vector3(side * 50, 130, 30));
            // a fire bowl on each corner of the tail
            var bowl = hull.OnTop(side * 205, -520, 0);
            var lip = m.Loop(2, bowl + new Vector3(0, 70, 0), new Vector3(70, 0, 0), new Vector3(0, 0, 70), 6);
            m.Cap(lip, m.P(2, bowl - new Vector3(0, 12, 0)), Steel - 2, bowl + new Vector3(0, 90, 0));
            var flame = m.Loop(2, bowl + new Vector3(0, 72, 0), new Vector3(55, 0, 0), new Vector3(0, 0, 55), 6);
            m.Cap(flame, m.P(2, bowl + new Vector3(side * 15, 270, -40)), 242, bowl, unlit: true);
            m.Ball(2, bowl + new Vector3(0, 105, 0), 38, Yellow);
        }
        // his key between the fires: the shaft up from the tail, the bow a ring of gold, two teeth
        {
            var foot = hull.OnTop(0, -540, -10); var head = foot + new Vector3(0, 330, 0);
            m.Strut(2, foot, head, 30, Gold);
            var bow = m.Loop(2, head + new Vector3(0, 95, 0), new Vector3(105, 0, 0), new Vector3(0, 95, 0), 8);
            var hole = m.Loop(2, head + new Vector3(0, 95, 0), new Vector3(55, 0, 0), new Vector3(0, 50, 0), 8);
            for (var k = 0; k < 8; k++) m.Both(new[] { bow[k], bow[(k + 1) % 8], hole[(k + 1) % 8], hole[k] }, Gold);
            m.Plate(2, Gold, foot + new Vector3(0, 70, 0), foot + new Vector3(95, 70, 0), foot + new Vector3(95, 120, 0), foot + new Vector3(0, 120, 0));
            m.Plate(2, Gold, foot + new Vector3(0, 150, 0), foot + new Vector3(70, 150, 0), foot + new Vector3(70, 195, 0), foot + new Vector3(0, 195, 0));
        }

        const float SeatZ = -120;
        var rimTop = hull.Top(0, SeatZ) + 24;
        Cockpit(m, hull, SeatZ, 235, 195, rimTop, 10, Stone + 3);
        Wheels(m, new WheelLook(Stone + 1, 28, Stone - 2, 1, Stone + 3, 1, RedFlat + 2),
            new Axle(new Vector3(255, 268, 400), new Vector3(445, 165, 400), 165, 120), new Axle(new Vector3(255, 268, -360), new Vector3(475, 185, -360), 185, 135));
        var hands = Sit(m, monk, hull, SeatZ, rimTop, new[] { 3, 4, 5, 6, 7, 8, 14, 15, 17, 18, 19, 20, 21 }, (5, new[] { 6 }), (7, new[] { 8, 14 }), new Vector3(0, 640, 40), 0.64f,
            // (the see-through pictures of his hood are left out: drawn solid here, they would be black patches)
            (f, _) => f.Material >= 12 ? null : (f.Colour, CarDriver.Own, f.Material == 0 ? 0 : -1), reach: 250, gripX: 95);
        SteeringWheel(m, hull, hands.Right, hands.Left, Stone + 3, LavaLine);
        return m.ToBody(racer.Header);
    }

    // ---------------------------------------------------------------------------------------------------------------- Island of the Wannies

    // The Wannie's car: a mine cart off the rails -- iron plates, four small iron wheels, buffers -- heaped with gems behind him, his lantern
    // on a pole and his pick across the back.
    public static Body BuildWannie(Body wannie, Body racer)
    {
        var m = new CarMesh(Wannie.Name);
        Roots(m);
        var hull = new Hull(m, 330, 12, 8, 0.5f,
            (585, 235, 150, 135), (545, 280, 180, 150), (280, 285, 185, 152), (0, 285, 185, 152), (-280, 285, 185, 152), (-505, 280, 180, 150), (-545, 235, 150, 135));
        hull.Skin(m, (band, k) => band is 0 or 5 ? Iron - 1 : band % 2 == 1 ? Iron : Iron + 2);
        hull.Nose(m, 595, Iron - 1);
        hull.Tail(m, -555, Iron - 1);
        foreach (var side in new[] { 1f, -1f })
        {
            // rivets down the corners, a buffer in front, a lamp
            foreach (var z in new[] { 500f, 140, -140, -470 })
                foreach (var y in new[] { 110f, -90 }) m.Ball(2, new(side * (hull.Side(hull.Cy + y, z) + 3), hull.Cy + y, z), 15, WhiteFlat - 2);
            m.Strut(2, new Vector3(side * 150, hull.Cy - 40, 585), new Vector3(side * 150, hull.Cy - 40, 650), 30, Steel);
            m.Ball(2, new(side * 150, hull.Cy - 40, 660), 44, Steel + 4);
        }
        // the gems: a heap in the back half, yellow and blue and pink
        foreach (var (x, up, z, r, c) in new[]
                 {
                     (-110f, 10f, -170f, 70, Amber), (90, 20, -200, 78, Cyan), (0, 70, -300, 85, Yellow), (-150, 30, -380, 72, Cyan), (140, 30, -400, 75, Amber),
                     (20, 110, -420, 62, 78), (-40, 120, -220, 50, WhiteFlat), (70, 95, -320, 45, Cyan + 2),
                 })
            m.Ball(2, hull.OnTop(x, z, up), r, c);
        // his lantern on a pole, his pick across the tail
        m.Rod(2, hull.OnTop(230, 430), hull.OnTop(230, 430, 330), 22);
        m.Ball(2, hull.OnTop(230, 430, 360), 46, Amber);
        m.Ball(2, hull.OnTop(230, 430, 360) + new Vector3(0, 0, 6), 22, Yellow);
        {
            var y = hull.Top(0, -500) + 40;
            m.Strut(2, new Vector3(-330, y, -500), new Vector3(300, y, -500), 28, Brown);
            m.Plate(2, Steel, new(300, y + 25, -500), new(345, y + 25, -500), new(330, y + 190, -560), new(300, y + 120, -520));
            m.Plate(2, Steel, new(300, y - 25, -500), new(345, y - 25, -500), new(330, y - 150, -440), new(300, y - 90, -480));
        }

        const float SeatZ = 170;
        var rimTop = hull.Top(0, SeatZ) + 22;
        Cockpit(m, hull, SeatZ, 235, 195, rimTop, 10, Iron + 3);
        var wheel = new Axle(new Vector3(235, 225, 380), new Vector3(355, 125, 380), 125, 80);
        Wheels(m, new WheelLook(Steel, 30, Steel - 1, 1, Iron + 1, 1, WhiteFlat - 2), wheel, wheel with { Mount = new Vector3(235, 225, -380), Hub = new Vector3(355, 125, -380) });
        var hands = Sit(m, wannie, hull, SeatZ, rimTop, new[] { 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 16 }, (7, new[] { 8, 9, 10, 11 }), (12, new[] { 13, 14, 16 }), new Vector3(0, 775, -40), 0.62f,
            reach: 250, gripHeight: 60, gripX: 120);
        SteeringWheel(m, hull, hands.Right, hands.Left, Steel, Dark);
        return m.ToBody(racer.Header);
    }

    // ---------------------------------------------------------------------------------------------------------------- Island of the Francos

    // The old Franco's car: his Leontine on wheels -- the wooden boat, the balloon over its stern on four ropes, the rudder and the propeller.
    public static Body BuildOldFranco(Body franco, Body racer)
    {
        var m = new CarMesh(OldFranco.Name);
        Roots(m);
        var hull = new Hull(m, 300, 10, 2.4f, 0,
            (660, 55, 60, 50), (570, 190, 122, 110), (390, 270, 150, 138), (110, 300, 160, 148), (-190, 295, 160, 148), (-420, 250, 150, 138), (-545, 165, 118, 108));
        hull.Skin(m, (band, _) => band % 2 == 0 ? Wood : Wood + 2);
        hull.Nose(m, 700, Wood, 35);
        hull.Tail(m, -575, Wood);

        // the balloon: over the stern, cream with a red band
        const float By = 800, Bz = -300;
        var balloon = new (float Z, float R)[] { (330, 50), (240, 150), (90, 215), (-110, 225), (-280, 170), (-370, 60) };
        {
            var rings = balloon.Select(b => m.Loop(2, new Vector3(0, By, Bz + b.Z), new Vector3(b.R, 0, 0), new Vector3(0, b.R * 0.85f, 0), 8)).ToArray();
            for (var i = 0; i + 1 < rings.Length; i++) { var band = i; m.Skin(rings[i], rings[i + 1], _ => band == 2 ? Red : Cream, new Vector3(0, By, Bz + (balloon[i].Z + balloon[i + 1].Z) / 2)); }
            m.Cap(rings[0], m.P(2, new(0, By, Bz + 365)), Cream, new Vector3(0, By, Bz + 300));
            m.Cap(rings[^1], m.P(2, new(0, By, Bz - 400)), Cream, new Vector3(0, By, Bz - 330));
            foreach (var side in new[] { 1f, -1f })
            {
                // its ropes down to the gunwale
                m.Rod(2, new(side * 150, By - 150, Bz + 100), hull.OnTop(side * 240, -150, -5), CreamFlat);
                m.Rod(2, new(side * 150, By - 150, Bz - 150), hull.OnTop(side * 200, -450, -5), CreamFlat);
                m.Ball(2, new(side * 150, hull.Cy + 35, 600), 40, Yellow);
            }
            // the rudder behind the balloon, the propeller behind the boat
            m.Plate(2, Red, new(0, By + 40, Bz - 330), new(0, By + 210, Bz - 470), new(0, By + 190, Bz - 560), new(0, By - 30, Bz - 430));
            var hub = new Vector3(0, hull.Cy + 20, -600);
            m.Ball(2, hub, 28, GoldFlat);
            m.Plate(2, Steel + 2, hub + new Vector3(-16, 0, 0), hub + new Vector3(40, 170, -4), hub + new Vector3(80, 155, -4), hub + new Vector3(16, 0, 0));
            m.Plate(2, Steel + 2, hub + new Vector3(16, 0, 0), hub + new Vector3(-40, -170, 4), hub + new Vector3(-80, -155, 4), hub + new Vector3(-16, 0, 0));
        }

        const float SeatZ = 190;
        var rimTop = hull.Top(0, SeatZ) + 24;
        Cockpit(m, hull, SeatZ, 210, 175, rimTop, 10, Wood + 3);
        Windscreen(m, hull, 465, 170, 85, CreamFlat);
        Wheels(m, new WheelLook(Wood, 24, Brown, 1, Wood + 2, 1, GoldFlat),
            new Axle(new Vector3(230, 262, 420), new Vector3(420, 150, 420), 150, 95), new Axle(new Vector3(245, 264, -330), new Vector3(470, 180, -330), 180, 115));
        var hands = Sit(m, franco, hull, SeatZ, rimTop, new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 20, 21, 22, 23, 24, 25, 26, 27 }, (6, new[] { 7, 8 }), (9, new[] { 10, 11 }), new Vector3(0, 500, 0), 0.82f,
            gripX: 80);
        SteeringWheel(m, hull, hands.Right, hands.Left, Wood + 2, 22);
        return m.ToBody(racer.Header);
    }

    // ---------------------------------------------------------------------------------------------------------------- Island CX

    // The survivor's car: the raft that got him off Island CX -- planks, a patched square sail on a mast, an oil drum and a crate for cargo.
    public static Body BuildSurvivor(Body survivor, Body racer)
    {
        var m = new CarMesh(Survivor.Name);
        Roots(m);
        var hull = new Hull(m, 260, 12, 6, 0.5f,
            (610, 265, 62, 70), (370, 270, 66, 72), (130, 270, 66, 72), (-100, 270, 66, 72), (-330, 270, 66, 72), (-570, 265, 62, 70));
        hull.Skin(m, (band, _) => band % 2 == 0 ? Brown : Wood);
        hull.Nose(m, 615, BrownDark);
        hull.Tail(m, -575, BrownDark);

        // the mast and its sail, square across the raft, patched, a red cross daubed on it for help
        {
            const float Z = -230;
            var top = hull.Top(0, Z);
            m.Strut(2, new Vector3(0, top - 10, Z), new Vector3(0, 960, Z), 30, Brown);
            m.Rod(2, new(-270, 900, Z - 6), new(270, 900, Z - 6), 22);
            m.Plate(2, Cream, new(-250, 895, Z - 12), new(250, 895, Z - 12), new(225, 470, Z - 40), new(-225, 470, Z - 40));
            m.Flat(2, RedFlat + 2, new(-40, 790, Z - 22), new(40, 790, Z - 22), new(40, 560, Z - 36), new(-40, 560, Z - 36));
            m.Flat(2, RedFlat + 2, new(-130, 715, Z - 27), new(130, 715, Z - 27), new(130, 640, Z - 32), new(-130, 640, Z - 32));
            foreach (var side in new[] { 1f, -1f }) m.Rod(2, new(side * 225, 470, Z - 40), hull.OnTop(side * 240, Z - 150), CreamFlat);
        }
        // cargo: an oil drum standing at the back, a crate in front
        {
            var foot = hull.OnTop(140, -450, -4);
            var a = m.Loop(2, foot, new Vector3(95, 0, 0), new Vector3(0, 0, 95), 6); var b = m.Loop(2, foot + new Vector3(0, 230, 0), new Vector3(95, 0, 0), new Vector3(0, 0, 95), 6);
            m.Skin(a, b, _ => Blue + 1, foot + new Vector3(0, 115, 0));
            m.Cap(b, m.P(2, foot + new Vector3(0, 232, 0)), Steel, foot + new Vector3(0, 100, 0));
            m.Box(2, hull.OnTop(-120, 420, 85), new Vector3(200, 170, 190), Wood + 1, Cream - 3);
            foreach (var side in new[] { 1f, -1f }) m.Ball(2, new(side * 190, hull.Cy + 5, 615), 40, Yellow);
        }

        const float SeatZ = 60;
        var rimTop = hull.Top(0, SeatZ) + 70;
        Cockpit(m, hull, SeatZ, 190, 165, rimTop, 8, Wood + 2);
        Wheels(m, new WheelLook(Steel - 2, 24, Grey, 1, Brown, 1, CreamFlat),
            new Axle(new Vector3(250, 245, 420), new Vector3(430, 150, 420), 150, 100), new Axle(new Vector3(250, 245, -400), new Vector3(450, 170, -400), 170, 115));
        var hands = Sit(m, survivor, hull, SeatZ, rimTop, new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 22, 23, 24, 25, 26, 27, 28, 29 }, (9, new[] { 10, 11 }), (12, new[] { 13, 14 }), new Vector3(0, 500, 0), 0.8f,
            gripX: 80);
        SteeringWheel(m, hull, hands.Right, hands.Left, Brown, 22);
        return m.ToBody(racer.Header);
    }
}
