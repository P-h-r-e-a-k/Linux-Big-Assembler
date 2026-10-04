using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// More drivers, so that every island has at least three of its own for the track it is to get (Car.Island says whose each is):
//
//   Emerald Moon               Baldino in his space suit in a lunar lander; one of the base's grey Franco guards in a flying saucer
//   Celebration Island         a Sup priest in a car like his robe, with censers and a lectern; FunFrock, the Dark Monk unmasked
//   Island of the Wannies      the old Wannie in a firefly tart; the Wannie miner on the undergas bulldozer
//   Island of the Mosquibees   a Mosquibee in a honey pot; the Queen's ventilation guy in a fan boat
//   Island of the Francos      De La Fontaine in a gazogem tanker; the old scientist in his laboratory; the nurse in a pram
//   Island CX                  a Franco trooper in a tank; one of the Emperor's soldiers on a rocket launcher
//   Elevator Platform Island   a monkey monster in a crate; the one with the sword in a war cart; the Franco policeman in his patrol car
//   Island under Celebration   the ferryman in his boat; the Mosquibee dissident under his flag; a Mosquibee on a slab of cooling lava
//
// and four the user's notes ask for: the wizard pedlar on his flying carpet (Desert Island), and for Otringal the pighead with his broom on
// a road sweeper, the souvenir shop's Sup with his stall and the casino's bouncer on a roulette wheel.
internal static partial class RaceTrackCharacterCars
{
    public static readonly Car Lander = new("Baldino's lander", "Baldino in his space suit", 93, 14, BuildLander, Moon);
    public static readonly Car Saucer = new("The guard's saucer", "a Franco guard of the moon base", 95, 15, BuildSaucer, Moon);
    public static readonly Car Priest = new("The priest's car", "a Sup priest", 292, 16, BuildPriest, Celebration);
    public static readonly Car FunFrock = new("FunFrock's car", "FunFrock", 313, 17, BuildFunFrock, Celebration);
    public static readonly Car Tart = new("The firefly tart", "the old Wannie", 386, 18, BuildTart, Wannies);
    public static readonly Car Bulldozer = new("The undergas bulldozer", "the Wannie miner", 342, 19, BuildBulldozer, Wannies);
    public static readonly Car HoneyPot = new("The honey pot", "a Mosquibee", 192, 20, BuildHoneyPot, Mosquibees);
    public static readonly Car FanBoat = new("The fan boat", "the Queen's ventilation guy", 197, 21, BuildFanBoat, Mosquibees);
    public static readonly Car Tanker = new("The gazogem tanker", "De La Fontaine", 450, 22, BuildTanker, Francos);
    public static readonly Car Laboratory = new("Mr. Kurtz's laboratory", "Mr. Kurtz, the old Franco scientist", 446, 23, BuildLaboratory, Francos);
    public static readonly Car Pram = new("The nurse's pram", "the Franco nurse", 447, 24, BuildPram, Francos);
    public static readonly Car Tank = new("The trooper's tank", "a Franco trooper", 290, 25, BuildTank, IslandCX);
    public static readonly Car Launcher = new("The rocket launcher", "one of the Emperor's soldiers", 260, 26, BuildLauncher, IslandCX);
    public static readonly Car Crate = new("The monkey's crate", "a monkey monster", 388, 27, BuildCrate, Elevator);
    public static readonly Car WarCart = new("The war cart", "the monkey monster with the sword", 363, 28, BuildWarCart, Elevator);
    public static readonly Car Patrol = new("The patrol car", "the Franco policeman", 86, 29, BuildPatrol, Elevator);
    public static readonly Car Ferry = new("The ferryman's boat", "the ferryman", 334, 30, BuildFerry, UnderCelebration);
    public static readonly Car Rebel = new("The dissident's car", "the Mosquibee dissident", 270, 31, BuildRebel, UnderCelebration);
    public static readonly Car LavaSlab = new("The lava slab", "a Mosquibee of the hide-out", 193, 32, BuildLavaSlab, UnderCelebration);
    public static readonly Car Carpet = new("The flying carpet", "the wizard pedlar", 240, 33, BuildCarpet, Desert);
    public static readonly Car Sweeper = new("The road sweeper", "the pighead with the broom", 438, 34, BuildSweeper, Otringal);
    public static readonly Car Stall = new("The souvenir stall", "the souvenir shop's Sup", 430, 35, BuildStall, Otringal);
    public static readonly Car Roulette = new("The roulette wheel", "the casino's bouncer", 419, 36, BuildRoulette, Otringal);

    private static Car[] More => new[]
    {
        Lander, Saucer, Priest, FunFrock, Tart, Bulldozer, HoneyPot, FanBoat, Tanker, Laboratory, Pram, Tank, Launcher, Crate, WarCart, Patrol, Ferry, Rebel, LavaSlab,
        Carpet, Sweeper, Stall, Roulette,
    };

    private const int Olive = 114, Teal = 146, Ochre = 99, BlueSoft = 196, BlueFlat = 203, GreenFlat = 138;

    // A driver out of its body: where its waist is (in its own body), how much smaller it sits, its arms (the one at +x first; none: it keeps
    // its hands to itself) and the bones left out with all that hangs from them (what it holds, a boat it stands in).
    // (Keep: only these bones, for a body that is not cut at a waist -- a cow's neck and head, a Dino-Fly's. Held: bones of the arms that are
    // things in the hands, turned with them but not taken for the hand.)
    private sealed record Driver(Vector3 Origin, float Scale, (int, int[])? Right = null, (int, int[])? Left = null, int[]? Drop = null, float Reach = 215, float GripHeight = 95, float GripX = 64,
        int[]? Keep = null, int[]? Held = null);
    // The cockpit: where along the car, how big, how high its rim stands and in what colour; a windscreen at Screen (none: NaN).
    private sealed record Cabin(float Z, float Rx = 215, float Rz = 185, float Up = 26, int Rim = Steel, float Screen = float.NaN, float ScreenHalf = 190, int Rail = WhiteFlat,
        int Wheel = Steel, int WheelLine = Dark, int Sides = 10);

    // What every car ends with: the cockpit, the wheels, the driver sat in it with its hands on the wheel.
    private static Body Finish(CarMesh m, Hull hull, Body character, Body racer, Driver d, Cabin c, WheelLook look, Axle front, Axle rear, CarDriver.Painter? paint = null)
    {
        m.Light = 1;
        var rimTop = hull.Top(0, c.Z) + c.Up;
        Cockpit(m, hull, c.Z, c.Rx, c.Rz, rimTop, c.Sides, c.Rim);
        if (!float.IsNaN(c.Screen)) Windscreen(m, hull, c.Screen, c.ScreenHalf, 100, c.Rail);
        Wheels(m, look, front, rear);
        m.Light = 1;

        var driver = new CarDriver(character);
        var seat = new Vector3(0, rimTop - 6, c.Z);
        Vector3 Place(Vector3 p) => (p - d.Origin) * d.Scale + seat;
        Vector3 Unplace(Vector3 p) => (p - seat) / d.Scale + d.Origin;
        var grip = new Vector3(d.GripX, rimTop - 6 + d.GripHeight, c.Z + d.Reach);
        // (an arm given without its forearm's bones: all that hangs from the upper arm, less what is dropped)
        int[] Fore((int, int[]) arm) => arm.Item2.Length > 0 ? arm.Item2 : driver.Below(arm.Item1).Where(b => d.Drop is null || !d.Drop.Contains(b)).ToArray();
        int[]? Hand((int, int[]) arm) => d.Held is null ? null : Fore(arm).Where(b => !d.Held.Contains(b)).ToArray();
        Vector3? right = d.Right is { } r && Fore(r).Length > 0 ? Place(driver.Reach(r.Item1, Fore(r), Unplace(grip), 1, Hand(r)).Hand) : null;
        Vector3? left = d.Left is { } l && Fore(l).Length > 0 ? Place(driver.Reach(l.Item1, Fore(l), Unplace(grip with { X = -grip.X }), -1, Hand(l)).Hand) : null;
        m.Pivot(13, m.P(2, seat));
        var point = driver.Seat(m, d.Keep is { } kept ? new HashSet<int>(kept) : driver.AllBut(d.Drop), Place, rimTop, d.Scale, paint, cut: true);
        Arms(m, d.Right is { } ra ? point(character.Bones[ra.Item1].Pivot) : m.P(13, seat + new Vector3(60, 120, 0)),
                d.Left is { } la ? point(character.Bones[la.Item1].Pivot) : m.P(13, seat + new Vector3(-60, 120, 0)));
        if (right is not null || left is not null)
        {
            var rh = right ?? left!.Value with { X = -left.Value.X }; var lh = left ?? right!.Value with { X = -right.Value.X };
            SteeringWheel(m, hull, rh, lh, c.Wheel, c.WheelLine);
        }
        return m.ToBody(racer.Header);
    }

    // A hull round as a plate from above: a lens of that radius, its rings across the car at those places along it.
    private static Hull Lens(CarMesh m, float cy, int n, float radius, float top, float bottom, float[] zs, float power = 2, float phase = 0)
        => new(m, cy, n, power, phase, zs.Select(z =>
        {
            var inside = MathF.Max(0.02f, 1 - z * z / (radius * radius));
            return (z, MathF.Sqrt(inside) * radius, top * MathF.Pow(inside, 0.7f), bottom * MathF.Pow(inside, 0.7f));
        }).ToArray());

    private static int[] Level(CarMesh m, Vector3 centre, float r, int n, float rz = -1) => m.Loop(2, centre, new Vector3(r, 0, 0), new Vector3(0, 0, rz < 0 ? r : rz), n);
    private static readonly float[] Sides = { 1f, -1f };

    // A dish on a mast, looking up and forward.
    private static void Dish(CarMesh m, Vector3 foot, float height, float radius, int colour)
    {
        var head = foot + new Vector3(0, height, 0);
        m.Rod(2, foot, head, WhiteFlat);
        var axis = Vector3.Normalize(new Vector3(0, 0.55f, 1));
        var v = Vector3.Normalize(Vector3.Cross(axis, Vector3.UnitX));
        var rim = m.Loop(2, head + axis * radius * 0.35f, Vector3.UnitX * radius, v * radius, 6);
        var back = m.P(2, head);
        for (var k = 0; k < 6; k++) m.Both(new[] { rim[k], rim[(k + 1) % 6], back }, colour);
        m.Ball(2, head + axis * radius * 0.9f, 14, RedFlat + 2);
    }

    // ---------------------------------------------------------------------------------------------------------------- Emerald Moon

    // Baldino, as the Esmers hold him on the moon: in his space suit, in a lander -- a capsule in gold foil, a flag, the engine's bell behind.
    public static Body BuildLander(Body baldino, Body racer)
    {
        var m = new CarMesh(Lander.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 310, 8, 3, 0.5f, (440, 170, 110, 105), (365, 285, 175, 130), (0, 300, 185, 135), (-330, 285, 175, 130), (-405, 170, 110, 105));
        hull.Skin(m, (_, k) => k is 7 or 0 or 6 ? White : k is 3 ? GreySoft : GoldSoft);
        hull.Nose(m, 455, GreySoft);
        hull.Tail(m, -415, GreySoft);
        m.Light = 1;
        {
            var a = m.Loop(2, new Vector3(0, hull.Cy, -400), new Vector3(85, 0, 0), new Vector3(0, 85, 0), 6);
            var b = m.Loop(2, new Vector3(0, hull.Cy, -500), new Vector3(150, 0, 0), new Vector3(0, 150, 0), 6);
            m.Skin(a, b, _ => Steel, new Vector3(0, hull.Cy, -450));
            Flame(m, new Vector3(0, hull.Cy, -490), 105, 230);
        }
        foreach (var side in Sides)
        {
            m.Ball(2, new(side * (hull.Side(hull.Cy + 40, 200) + 4), hull.Cy + 40, 200), 52, Cyan);       // portholes
            m.Ball(2, new(side * 150, hull.Cy + 30, 435), 40, Yellow);
        }
        {
            var foot = hull.OnTop(-210, -250); var head = foot + new Vector3(0, 330, 0);
            m.Rod(2, foot, head, WhiteFlat);
            m.Plate(2, Red, head, head + new Vector3(0, 0, -150), head + new Vector3(0, -95, -150), head + new Vector3(0, -95, 0));
        }
        return Finish(m, hull, baldino, racer,
            new Driver(new Vector3(0, 560, 0), 0.6f, (10, new[] { 11, 12 }), (17, new[] { 18, 19 }), new[] { 24 }, Reach: 185, GripHeight: 35, GripX: 110),
            new Cabin(-40, 250, 215, Rim: Steel),
            new WheelLook(Gold, 22, Grey, 1, Steel, 1, WhiteFlat, Lean: true),
            new Axle(new Vector3(235, 262, 300), new Vector3(430, 150, 300), 150, 100), new Axle(new Vector3(235, 262, -270), new Vector3(430, 150, -270), 150, 100));
    }

    // A guard of the moon base in one of the Esmers' flying saucers: a grey disc with a red rim and lights round it, its feet down.
    public static Body BuildSaucer(Body guard, Body racer)
    {
        var m = new CarMesh(Saucer.Name);
        Roots(m);
        var hull = Lens(m, 300, 10, 480, 150, 95, new[] { 455f, 380, 250, 100, -100, -250, -380, -455 });
        hull.Skin(m, (_, k) => k is 2 or 7 ? Red : k is 3 or 4 or 5 or 6 ? Steel - 2 : Steel);
        hull.Nose(m, 482, Red);
        hull.Tail(m, -482, Red);
        for (var i = 0; i < 12; i++)
        {
            var a = (i + 0.5f) * MathF.Tau / 12;
            m.Ball(2, new(492 * MathF.Sin(a), hull.Cy + 4, 492 * MathF.Cos(a)), 26, i % 2 == 0 ? Yellow : Cyan);
        }
        m.Rod(2, hull.OnTop(0, -330), hull.OnTop(0, -330, 260), WhiteFlat);
        m.Ball(2, hull.OnTop(0, -330, 270), 22, RedFlat + 2);
        return Finish(m, hull, guard, racer,
            new Driver(new Vector3(0, 520, 0), 0.66f, (6, new[] { 7, 8 }), (9, new[] { 10, 11 })),
            new Cabin(0, 205, 190, Rim: Steel + 2, Wheel: Steel, WheelLine: WhiteFlat),
            new WheelLook(Steel, 22, Grey, 1, Steel + 2, 1, RedFlat + 2),
            new Axle(new Vector3(200, 245, 290), new Vector3(450, 140, 290), 140, 95), new Axle(new Vector3(200, 245, -290), new Vector3(450, 140, -290), 140, 95));
    }

    // ---------------------------------------------------------------------------------------------------------------- Celebration Island

    // A priest of the Dark Monk's temple: a car like his robe, black with the red sash round it, a lectern with the book open on the bonnet,
    // censers swinging at the back, a candle on each front corner.
    public static Body BuildPriest(Body priest, Body racer)
    {
        var m = new CarMesh(Priest.Name);
        Roots(m);
        m.Light = SoftLight;
        var hull = new Hull(m, 300, 12, 4, 0.5f, (610, 195, 108, 105), (545, 250, 135, 120), (250, 265, 150, 130), (150, 265, 150, 130), (-330, 265, 150, 130), (-510, 250, 135, 120), (-580, 195, 108, 105));
        hull.Skin(m, (band, _) => band == 2 ? RedSoft : Black);
        hull.Nose(m, 622, Black);
        hull.Tail(m, -592, Black);
        // the lectern, the book open on it
        {
            var at = hull.OnTop(0, 430, 95);
            m.Light = SoftShare;
            m.Plate(2, White, at, at + new Vector3(-105, 40, -30), at + new Vector3(-105, 40, 70), at + new Vector3(0, 0, 100));
            m.Plate(2, White, at, at + new Vector3(105, 40, -30), at + new Vector3(105, 40, 70), at + new Vector3(0, 0, 100));
            m.Light = 1;
            m.Strut(2, hull.OnTop(0, 480, -8), at + new Vector3(0, -4, 50), 26, Gold);
        }
        foreach (var side in Sides)
        {
            // a candle, a censer on its chain with its smoke
            var candle = hull.OnTop(side * 200, 540, 0);
            m.Strut(2, candle, candle + new Vector3(0, 120, 0), 22, Cream);
            m.Ball(2, candle + new Vector3(0, 145, 0), 20, Amber);
            var post = hull.OnTop(side * 215, -480, 0); var arm = post + new Vector3(side * 70, 330, -40);
            m.Rod(2, post, arm, GoldFlat);
            m.Rod(2, arm, arm + new Vector3(side * 25, -170, -20), GoldFlat);
            m.Ball(2, arm + new Vector3(side * 25, -185, -20), 36, GoldFlat);
            m.Ball(2, arm + new Vector3(side * 35, -110, -50), 22, WhiteFlat - 3);
            m.Ball(2, arm + new Vector3(side * 20, -40, -85), 16, WhiteFlat - 2);
            m.Ball(2, new(side * 150, hull.Cy + 45, 600), 46, Yellow);
        }
        return Finish(m, hull, priest, racer,
            new Driver(new Vector3(0, 605, 0), 0.8f, (7, new[] { 8, 9 }), (10, new[] { 11, 12 }), new[] { 21, 27 }),
            new Cabin(-90, 222, 190, Rim: Red, Wheel: Gold, WheelLine: GoldFlat),
            new WheelLook(Gold, 24, Black, SoftLight, Red, 1, GoldFlat),
            new Axle(new Vector3(245, 266, 400), new Vector3(430, 160, 400), 160, 110), new Axle(new Vector3(250, 266, -380), new Vector3(465, 180, -380), 180, 130));
    }

    // FunFrock, out of the Dark Monk's statue: a blood-red car with a sabre down each flank, and behind him the ring of one of his
    // teleporters, glowing.
    public static Body BuildFunFrock(Body funfrock, Body racer)
    {
        var m = new CarMesh(FunFrock.Name);
        Roots(m);
        var hull = new Hull(m, 300, 12, 5, 0.5f, (640, 185, 108, 105), (565, 258, 145, 125), (200, 280, 160, 130), (-350, 280, 160, 130), (-540, 258, 145, 125), (-612, 185, 108, 105));
        hull.Skin(m, (_, k) => k is 11 ? Red - 2 : Red);
        hull.Nose(m, 655, Red - 2);
        hull.Tail(m, -625, Red - 2);
        foreach (var side in Sides)
        {
            // the sabre: its blade along the flank, the point curving up past the nose, a gold hilt at the back
            var x = side * 300;
            m.Plate(2, Steel + 2, new(x, hull.Cy + 30, -330), new(x, hull.Cy - 45, 300), new(x + side * 30, hull.Cy + 75, 720), new(x, hull.Cy + 70, 250));
            m.Strut(2, new Vector3(x, hull.Cy, -330), new Vector3(x, hull.Cy + 5, -470), 34, Gold);
            m.Ball(2, new(x, hull.Cy + 5, -480), 30, GoldFlat);
            m.Ball(2, new(side * 140, hull.Cy + 40, 640), 44, Yellow);
        }
        // the teleporter: a gold ring standing across the tail, a see-through glow in it
        {
            var centre = hull.OnTop(0, -500, 215);
            var ring = m.Loop(2, centre, new Vector3(215, 0, 0), new Vector3(0, 215, 0), 8);
            var hole = m.Loop(2, centre, new Vector3(165, 0, 0), new Vector3(0, 165, 0), 8);
            var middle = m.P(2, centre);
            for (var k = 0; k < 8; k++)
            {
                m.Both(new[] { ring[k], ring[(k + 1) % 8], hole[(k + 1) % 8], hole[k] }, Gold);
                m.Both(new[] { hole[k], hole[(k + 1) % 8], middle }, Pane, CarMesh.SeeThrough);
            }
        }
        return Finish(m, hull, funfrock, racer,
            new Driver(new Vector3(0, 640, 40), 0.64f, (5, new[] { 6 }), (7, new[] { 8, 14 }), new[] { 28 }, Reach: 250, GripX: 95),
            new Cabin(-110, 235, 195, Rim: Gold, Wheel: Gold, WheelLine: GoldFlat),
            new WheelLook(Steel, 26, Grey, 1, Red, 1, GoldFlat),
            new Axle(new Vector3(255, 268, 400), new Vector3(445, 165, 400), 165, 115), new Axle(new Vector3(255, 268, -330), new Vector3(475, 185, -330), 185, 135),
            (f, _) => f.Material >= 12 ? null : (f.Colour, CarDriver.Own, f.Material == 0 ? 0 : -1));
    }

    // ---------------------------------------------------------------------------------------------------------------- Island of the Wannies

    // The old Wannie in one of the family's firefly tarts: a round tart, its crust crimped all round, the fireflies still flying over it.
    public static Body BuildTart(Body wannie, Body racer)
    {
        var m = new CarMesh(Tart.Name);
        Roots(m);
        var hull = Lens(m, 270, 10, 470, 105, 90, new[] { 445f, 370, 245, 100, -100, -245, -370, -445 });
        hull.Skin(m, (_, k) => k is 0 or 9 ? Gold : k is 1 or 8 ? Gold - 2 : Cream);
        hull.Nose(m, 472, Cream);
        hull.Tail(m, -472, Cream);
        for (var i = 0; i < 14; i++)
        {
            var a = (i + 0.5f) * MathF.Tau / 14;
            m.Ball(2, new(455 * MathF.Sin(a), hull.Cy + 38, 455 * MathF.Cos(a)), 46, 42);
        }
        foreach (var (x, z, h) in new[] { (-220f, 250f, 170f), (180, 300, 240), (-60, 360, 120), (260, -120, 200), (-270, -160, 150), (90, -330, 230), (-120, -300, 110) })
        {
            var foot = hull.OnTop(x, z); var fly = foot + new Vector3(15, h, -10);
            m.Rod(2, foot, fly, Dark + 4);
            m.Ball(2, fly, 20, Yellow);
        }
        return Finish(m, hull, wannie, racer,
            new Driver(new Vector3(0, 775, -40), 0.62f, (7, new[] { 8, 9, 10 }), (12, new[] { 13 }), new[] { 15 }, Reach: 250, GripHeight: 60, GripX: 120),
            new Cabin(20, 235, 195, Rim: Cream, Wheel: Wood, WheelLine: 22),
            new WheelLook(Wood, 24, Brown, 1, Cream, 1, GoldFlat),
            new Axle(new Vector3(210, 225, 300), new Vector3(430, 125, 300), 125, 85), new Axle(new Vector3(210, 225, -300), new Vector3(430, 125, -300), 125, 85));
    }

    // The Wannie miner on the mine's bulldozer: yellow, its blade out in front, an exhaust stack puffing, a bar over his head.
    public static Body BuildBulldozer(Body miner, Body racer)
    {
        var m = new CarMesh(Bulldozer.Name);
        Roots(m);
        var hull = new Hull(m, 320, 12, 7, 0.5f, (520, 262, 150, 138), (480, 285, 172, 150), (0, 285, 172, 150), (-420, 285, 172, 150), (-465, 262, 150, 138));
        hull.Skin(m, (_, k) => k is 3 or 4 or 5 or 6 or 7 ? Grey + 2 : Ochre);
        hull.Nose(m, 528, Ochre);
        hull.Tail(m, -473, Grey + 2);
        // the blade, on two arms
        {
            var low = new[] { new Vector3(-390, 95, 640), new Vector3(390, 95, 640) }; var mid = new[] { new Vector3(-390, 250, 690), new Vector3(390, 250, 690) }; var high = new[] { new Vector3(-390, 420, 650), new Vector3(390, 420, 650) };
            int l0 = m.P(2, low[0]), l1 = m.P(2, low[1]), m0 = m.P(2, mid[0]), m1 = m.P(2, mid[1]), h0 = m.P(2, high[0]), h1 = m.P(2, high[1]);
            m.Both(new[] { l0, l1, m1, m0 }, Steel);
            m.Both(new[] { m0, m1, h1, h0 }, Steel + 2);
            foreach (var side in Sides) m.Strut(2, new Vector3(side * 270, hull.Cy - 60, 480), new Vector3(side * 330, 250, 685), 34, Ochre);
        }
        // the stack and its smoke; the bar over the driver; a lamp on it
        {
            var foot = hull.OnTop(200, 330, -6);
            m.Strut(2, foot, foot + new Vector3(0, 250, 0), 46, Steel - 2);
            foreach (var (dy, dz, r, c) in new[] { (290f, -20f, 30, WhiteFlat - 4), (350, -70, 40, WhiteFlat - 3), (430, -140, 52, WhiteFlat - 2) }) m.Ball(2, foot + new Vector3(0, dy, dz), r, c);
            int a = m.P(2, hull.OnTop(-260, -330)), b = m.P(2, hull.OnTop(-260, -330, 520)), c2 = m.P(2, hull.OnTop(260, -330, 520)), e = m.P(2, hull.OnTop(260, -330));
            m.Line(a, b, Dark + 2); m.Line(b, c2, Dark + 2); m.Line(c2, e, Dark + 2);
            m.Ball(2, hull.OnTop(0, -330, 540), 30, Amber);
        }
        var wheel = new Axle(new Vector3(255, 240, 330), new Vector3(400, 150, 330), 150, 120);
        return Finish(m, hull, miner, racer,
            new Driver(new Vector3(0, 775, -40), 0.62f, (7, new[] { 8, 9 }), (12, new[] { 13, 14 }), new[] { 25 }, Reach: 250, GripHeight: 60, GripX: 120),
            new Cabin(-70, 235, 195, Rim: Grey + 2),
            new WheelLook(Steel, 30, Grey, 1, Ochre, 1, WhiteFlat - 2, Lean: true), wheel, wheel with { Mount = new Vector3(255, 240, -300), Hub = new Vector3(400, 150, -300) });
    }

    // ---------------------------------------------------------------------------------------------------------------- Island of the Mosquibees

    // A Mosquibee in a honey pot on its side: an earthen pot, honey at its mouth and running over the lip, the dipper standing behind.
    public static Body BuildHoneyPot(Body bee, Body racer)
    {
        var m = new CarMesh(HoneyPot.Name);
        Roots(m);
        var hull = new Hull(m, 320, 10, 2, 0,
            (570, 150, 125, 118), (510, 195, 155, 142), (440, 165, 132, 124), (300, 280, 205, 172), (100, 340, 236, 186), (-150, 350, 240, 188), (-380, 300, 210, 175), (-520, 190, 135, 125));
        hull.Skin(m, (band, _) => band is 0 or 1 ? Cream : band == 4 ? Orange + 2 : Orange);
        hull.Nose(m, 545, Gold);
        hull.Tail(m, -545, Orange);
        foreach (var (x, y, r) in new[] { (-70f, -118f, 40), (25, -150, 34), (95, -105, 28), (-10, -215, 20) }) m.Ball(2, new(x, hull.Cy + y, 575), r, Amber);
        {
            var foot = hull.OnTop(0, -400, -6);
            m.Strut(2, foot, foot + new Vector3(0, 300, -40), 26, Wood);
            foreach (var (dy, r) in new[] { (320f, 46), (370, 42), (415, 34) }) m.Ball(2, foot + new Vector3(0, dy, -42 - (dy - 320) * 0.12f), r, Amber);
        }
        foreach (var side in Sides) m.Ball(2, new(side * 215, hull.Cy + 95, 330), 40, Yellow);
        return Finish(m, hull, bee, racer,
            new Driver(new Vector3(0, 600, 0), 0.8f),
            new Cabin(-110, 190, 170, Rim: Cream),
            new WheelLook(Wood, 22, Grey, 1, Gold, 1, GoldFlat),
            new Axle(new Vector3(215, 270, 380), new Vector3(420, 150, 380), 150, 105), new Axle(new Vector3(280, 280, -300), new Vector3(520, 190, -300), 190, 135));
    }

    // The Queen's ventilation guy, his fan with him: a fan boat -- a flat hull, the great fan in its cage at the back, two rudders behind it.
    public static Body BuildFanBoat(Body bee, Body racer)
    {
        var m = new CarMesh(FanBoat.Name);
        Roots(m);
        var hull = new Hull(m, 265, 12, 5, 0.5f, (610, 170, 62, 75), (520, 268, 84, 90), (100, 282, 90, 95), (-330, 282, 90, 95), (-440, 262, 84, 90));
        hull.Skin(m, (_, k) => k is 11 or 0 or 10 ? Cream : Teal);
        hull.Nose(m, 640, Teal, 20);
        hull.Tail(m, -452, Teal);
        {
            var hub = hull.OnTop(0, -400, 330);
            var cage = m.Loop(2, hub, new Vector3(310, 0, 0), new Vector3(0, 310, 0), 8);
            for (var k = 0; k < 8; k++) m.Line(cage[k], cage[(k + 1) % 8], GoldFlat);
            m.Line(cage[0], cage[4], GoldFlat); m.Line(cage[2], cage[6], GoldFlat);
            m.Ball(2, hub + new Vector3(0, 0, 14), 40, GoldFlat);
            for (var i = 0; i < 4; i++)
            {
                var a = i * MathF.Tau / 4 + 0.5f;
                Vector3 Out(float r, float turn) => hub + new Vector3(MathF.Cos(a + turn), MathF.Sin(a + turn), 0) * r;
                m.Plate(2, Steel + 2, Out(30, 0), Out(270, -0.22f) + new Vector3(0, 0, 18), Out(270, 0.22f) - new Vector3(0, 0, 18));
            }
            foreach (var side in Sides)
            {
                m.Strut(2, hull.OnTop(side * 180, -390, -6), hub + new Vector3(side * 40, -40, 0), 24, Steel);
                m.Plate(2, Red, hull.OnTop(side * 140, -450, 40) + new Vector3(0, 0, -30), hull.OnTop(side * 140, -450, 560) + new Vector3(0, 0, -30), hull.OnTop(side * 140, -450, 560) + new Vector3(0, 0, -200),
                    hull.OnTop(side * 140, -450, 40) + new Vector3(0, 0, -200));
                m.Ball(2, new(side * 170, hull.Cy + 15, 600), 38, Yellow);
            }
        }
        return Finish(m, hull, bee, racer,
            new Driver(new Vector3(0, 600, 0), 0.8f),
            new Cabin(40, 190, 170, Up: 60, Rim: Teal + 2),
            new WheelLook(Steel, 22, Grey, 1, Cream, 1, GoldFlat),
            new Axle(new Vector3(245, 245, 400), new Vector3(430, 145, 400), 145, 100), new Axle(new Vector3(255, 245, -250), new Vector3(470, 160, -250), 160, 110));
    }

    // ---------------------------------------------------------------------------------------------------------------- Island of the Francos

    // De La Fontaine, of the factory's family: a gazogem tanker -- the green tank with its yellow band and filler dome, a warning sign on
    // each side, the hose coiled at the back.
    public static Body BuildTanker(Body franco, Body racer)
    {
        var m = new CarMesh(Tanker.Name);
        Roots(m);
        var hull = new Hull(m, 330, 10, 2, 0, (590, 190, 170, 150), (525, 300, 235, 180), (200, 305, 240, 184), (-60, 305, 240, 184), (-430, 300, 235, 180), (-500, 190, 170, 150));
        hull.Skin(m, (band, _) => band is 0 or 4 ? Steel : band == 2 ? Ochre : Green);
        hull.Nose(m, 615, Steel);
        hull.Tail(m, -525, Steel);
        // the filler dome
        {
            var foot = hull.OnTop(0, -260, -10);
            var a = Level(m, foot, 105, 6); var b = Level(m, foot + new Vector3(0, 70, 0), 90, 6);
            m.Skin(a, b, _ => Steel, foot + new Vector3(0, 30, 0));
            m.Cap(b, m.P(2, foot + new Vector3(0, 105, 0)), Steel + 2, foot);
        }
        foreach (var side in Sides)
        {
            // the sign: a yellow diamond, a flame in it
            var x = side * (hull.Side(hull.Cy + 20, -250) + 8); var c = new Vector3(x, hull.Cy + 20, -250);
            m.Flat(2, Amber, c + new Vector3(0, 95, 0), c + new Vector3(0, 0, 95), c + new Vector3(0, -95, 0), c + new Vector3(0, 0, -95));
            m.Ball(2, c + new Vector3(side * 6, -10, 0), 34, RedFlat + 2);
            m.Ball(2, new(side * 150, hull.Cy + 40, 590), 46, Yellow);
        }
        {
            var reel = new Vector3(0, hull.Cy + 40, -545);
            m.Ball(2, reel, 80, Dark + 1); m.Ball(2, reel + new Vector3(0, 0, -8), 42, Steel + 3);
            m.Rod(2, reel + new Vector3(70, -30, 0), new Vector3(230, 140, -520), Dark + 1);
            m.Ball(2, new(230, 130, -520), 22, Steel + 4);
        }
        return Finish(m, hull, franco, racer,
            new Driver(new Vector3(0, 470, 0), 0.8f, (6, new[] { 7, 8 }), (9, new[] { 10, 11 })),
            new Cabin(250, 205, 175, Rim: Steel, Screen: 500, ScreenHalf: 170),
            new WheelLook(Steel, 26, Grey, 1, Green, 1, Amber),
            new Axle(new Vector3(245, 275, 430), new Vector3(440, 160, 430), 160, 110), new Axle(new Vector3(260, 280, -300), new Vector3(500, 190, -300), 190, 140));
    }

    // Mr. Kurtz ("Retired Colonel - Specializing in non-scientific sciences", says his door), the old Franco scientist: his laboratory on wheels -- a flask of something green bubbling at the back, a coil throwing sparks beside
    // it, dials on the bonnet.
    public static Body BuildLaboratory(Body franco, Body racer)
    {
        var m = new CarMesh(Laboratory.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 300, 12, 4, 0.5f, (600, 205, 110, 105), (535, 255, 140, 122), (250, 270, 155, 130), (-350, 270, 155, 130), (-500, 255, 140, 122), (-570, 205, 110, 105));
        hull.Skin(m, (_, k) => k is 2 or 8 ? BlueSoft : k is 3 or 4 or 5 or 6 or 7 ? GreySoft : White);
        hull.Nose(m, 612, GreySoft);
        hull.Tail(m, -582, GreySoft);
        m.Light = 1;
        // the flask: see-through, what is in it, its bubbles
        {
            var foot = hull.OnTop(-140, -400, 0);
            var belly = Level(m, foot + new Vector3(0, 110, 0), 120, 6); var neck = Level(m, foot + new Vector3(0, 290, 0), 38, 6); var lip = Level(m, foot + new Vector3(0, 370, 0), 46, 6);
            var bottom = m.P(2, foot);
            for (var k = 0; k < 6; k++)
            {
                m.Both(new[] { belly[k], belly[(k + 1) % 6], bottom }, Pane, CarMesh.SeeThrough);
                m.Both(new[] { belly[k], belly[(k + 1) % 6], neck[(k + 1) % 6], neck[k] }, Pane, CarMesh.SeeThrough);
                m.Line(neck[k], lip[k], WhiteFlat);
            }
            m.Ball(2, foot + new Vector3(0, 80, 0), 82, GreenFlat);
            foreach (var (dy, dx, r) in new[] { (430f, 10f, 22), (500, -25, 16), (560, 15, 11) }) m.Ball(2, foot + new Vector3(dx, dy, 0), r, GreenFlat + 2);
        }
        // the coil: a post of rings, a ball on top, sparks
        {
            var foot = hull.OnTop(160, -420, 0); var top = foot + new Vector3(0, 330, 0);
            m.Strut(2, foot, top, 40, Steel - 1);
            foreach (var dy in new[] { 80f, 160, 240 }) m.Ball(2, foot + new Vector3(0, dy, 0), 42, Orange + 6);
            m.Ball(2, top + new Vector3(0, 30, 0), 56, Steel + 6);
            foreach (var (a, b, c) in new[] { (new Vector3(50, 40, 0), new Vector3(120, 10, 30), new Vector3(170, 70, 10)), (new Vector3(-40, 60, 20), new Vector3(-90, 130, 40), new Vector3(-150, 110, 80)), (new Vector3(10, 80, -30), new Vector3(40, 160, -60), new Vector3(-10, 220, -50)) })
            {
                int p0 = m.P(2, top + a), p1 = m.P(2, top + b), p2 = m.P(2, top + c);
                m.Line(p0, p1, Yellow); m.Line(p1, p2, Yellow);
            }
        }
        foreach (var side in Sides)
        {
            var dial = hull.OnTop(side * 95, 400, 8);
            m.Ball(2, dial, 40, WhiteFlat); m.Ball(2, dial + new Vector3(0, 6, 0), 12, RedFlat + 2);
            m.Ball(2, new(side * 155, hull.Cy + 42, 590), 46, Yellow);
        }
        return Finish(m, hull, franco, racer,
            new Driver(new Vector3(0, 520, 0), 0.78f, (6, new[] { 7, 8 }), (9, new[] { 10, 11 }), new[] { 27, 28 }),
            new Cabin(20, 215, 185, Rim: Blue + 2, Screen: 300),
            new WheelLook(Steel, 24, Grey, 1, White, SoftShare, BlueFlat),
            new Axle(new Vector3(250, 266, 400), new Vector3(435, 155, 400), 155, 105), new Axle(new Vector3(255, 266, -330), new Vector3(470, 175, -330), 175, 125));
    }

    // The nurse of the Francos' nursery in a pram: a white tub with a blue band, its hood up at the back, the push handle behind it, a
    // red cross on each side, tall thin wheels.
    public static Body BuildPram(Body nurse, Body racer)
    {
        var m = new CarMesh(Pram.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 340, 10, 2.5f, 0, (530, 175, 120, 118), (440, 268, 165, 150), (160, 300, 182, 160), (-190, 300, 185, 160), (-390, 270, 175, 150), (-475, 180, 125, 120));
        hull.Skin(m, (_, k) => k is 2 or 7 ? BlueSoft : White);
        hull.Nose(m, 560, White);
        hull.Tail(m, -505, White);
        m.Light = 1;
        // the hood: three leaves round its hinge
        {
            const float Z0 = -170, R = 360; var y0 = hull.Top(0, Z0) - 10;
            var angles = new[] { 1.15f, 1.75f, 2.4f, 3.1f };
            var arcs = angles.Select(a => (L: m.P(2, new(-265, y0 + R * MathF.Sin(a), Z0 + R * MathF.Cos(a))), T: m.P(2, new(0, y0 + R * 1.12f * MathF.Sin(a), Z0 + R * 1.12f * MathF.Cos(a))), Rg: m.P(2, new(265, y0 + R * MathF.Sin(a), Z0 + R * MathF.Cos(a))))).ToArray();
            for (var i = 0; i + 1 < arcs.Length; i++)
            {
                m.Both(new[] { arcs[i].L, arcs[i].T, arcs[i + 1].T, arcs[i + 1].L }, Blue + 2);
                m.Both(new[] { arcs[i].T, arcs[i].Rg, arcs[i + 1].Rg, arcs[i + 1].T }, Blue + 2);
            }
        }
        foreach (var side in Sides)
        {
            // the handle; the cross
            int foot = m.P(2, new(side * 200, hull.Cy + 60, -470)), hand = m.P(2, new(side * 200, hull.Cy + 330, -690));
            m.Line(foot, hand, WhiteFlat);
            if (side > 0) m.Line(hand, m.P(2, new(-200, hull.Cy + 330, -690)), WhiteFlat);
            var x = side * (hull.Side(hull.Cy + 30, 250) + 8); var c = new Vector3(x, hull.Cy + 30, 250);
            m.Flat(2, RedFlat + 2, c + new Vector3(0, 80, -26), c + new Vector3(0, 80, 26), c + new Vector3(0, -80, 26), c + new Vector3(0, -80, -26));
            m.Flat(2, RedFlat + 2, c + new Vector3(0, 26, -80), c + new Vector3(0, 26, 80), c + new Vector3(0, -26, 80), c + new Vector3(0, -26, -80));
            m.Ball(2, new(side * 140, hull.Cy + 40, 540), 40, Yellow);
        }
        var wheel = new Axle(new Vector3(230, 250, 330), new Vector3(420, 185, 330), 185, 55);
        return Finish(m, hull, nurse, racer,
            new Driver(new Vector3(0, 490, 0), 0.8f, (10, new[] { 11, 12 }), (13, new[] { 14, 15 })),
            new Cabin(150, 205, 175, Rim: Blue + 2, Wheel: Steel, WheelLine: WhiteFlat),
            new WheelLook(Steel, 20, White, SoftShare, Steel, 1, BlueFlat, Sides: 8), wheel, wheel with { Mount = new Vector3(230, 250, -300), Hub = new Vector3(420, 185, -300) });
    }

    // ---------------------------------------------------------------------------------------------------------------- Island CX

    // A trooper of the garrison: a tank in two greens, a gun down each flank, a whip aerial.
    public static Body BuildTank(Body trooper, Body racer)
    {
        var m = new CarMesh(Tank.Name);
        Roots(m);
        var hull = new Hull(m, 300, 12, 6, 0.5f, (610, 225, 105, 108), (530, 290, 150, 130), (180, 295, 155, 132), (-180, 295, 155, 132), (-460, 290, 150, 130), (-545, 235, 118, 115));
        hull.Skin(m, (band, k) => (band + k / 2) % 2 == 0 ? Olive : Olive + 2);
        hull.Nose(m, 625, Olive);
        hull.Tail(m, -558, Olive + 2);
        foreach (var side in Sides)
        {
            var from = new Vector3(side * 325, hull.Cy + 70, -120); var to = new Vector3(side * 325, hull.Cy + 70, 600);
            m.Box(2, new Vector3(side * 325, hull.Cy + 70, -200), new Vector3(90, 110, 260), Olive + 2);
            m.Strut(2, from, to, 42, Steel - 2);
            m.Ball(2, to + new Vector3(0, 0, 6), 24, Dark);
            m.Ball(2, new(side * 150, hull.Cy + 35, 610), 40, Yellow);
        }
        m.Rod(2, hull.OnTop(-220, -480), hull.OnTop(-220, -480, 430), Dark + 2);
        m.Ball(2, hull.OnTop(-220, -480, 440), 18, RedFlat + 2);
        m.Box(2, hull.OnTop(90, -430, 60), new Vector3(220, 120, 160), Olive + 1);
        return Finish(m, hull, trooper, racer,
            new Driver(new Vector3(0, 520, 0), 0.78f, (8, new[] { 9, 10 }), (11, new[] { 12, 13 })),
            new Cabin(-10, 215, 185, Rim: Olive + 3),
            new WheelLook(Steel - 2, 28, Grey, 1, Olive + 1, 1, Dark + 3),
            new Axle(new Vector3(255, 262, 390), new Vector3(450, 165, 390), 165, 125), new Axle(new Vector3(255, 262, -360), new Vector3(450, 165, -360), 165, 125));
    }

    // One of the Emperor's soldiers on the island's rocket launcher: black, four red-nosed rockets on their rack at the back, a dish.
    public static Body BuildLauncher(Body soldier, Body racer)
    {
        var m = new CarMesh(Launcher.Name);
        Roots(m);
        m.Light = SoftLight;
        var hull = new Hull(m, 300, 12, 5, 0.5f, (620, 200, 108, 105), (550, 262, 142, 124), (150, 275, 152, 130), (-420, 275, 152, 130), (-560, 255, 138, 122));
        hull.Skin(m, (_, _) => Black);
        hull.Nose(m, 632, Black);
        hull.Tail(m, -572, Black);
        m.Light = 1;
        foreach (var x in new[] { -195f, -65, 65, 195 })
        {
            var tail = hull.OnTop(x, -510, 70); var nose = tail + new Vector3(0, 250, 330);
            var along = Vector3.Normalize(nose - tail);
            m.Strut(2, tail, nose, 62, Steel + 2);
            var u = Vector3.UnitX * 36; var v = Vector3.Normalize(Vector3.Cross(along, Vector3.UnitX)) * 36;
            m.Cap(m.Loop(2, nose, u, v, 4), m.P(2, nose + along * 105), Red, tail);
            m.Plate(2, Red, tail, tail + new Vector3(0, 80, -40), tail - along * 10 + new Vector3(0, 10, -75));
        }
        m.Box(2, hull.OnTop(0, -450, 40), new Vector3(470, 60, 200), Steel - 2);
        Dish(m, hull.OnTop(-205, -120, 0), 250, 110, Steel);
        foreach (var side in Sides)
        {
            for (var i = 0; i + 1 < hull.Rings.Count; i++) { var k = side > 0 ? 2 : 9; m.Line(hull.Rings[i][k], hull.Rings[i + 1][k], RedFlat + 2); }
            m.Ball(2, new(side * 150, hull.Cy + 40, 615), 44, Yellow);
        }
        return Finish(m, hull, soldier, racer,
            new Driver(new Vector3(0, 600, 0), 0.78f, (7, new[] { 8, 9 }), (10, new[] { 11, 12 }), new[] { 24 }),
            new Cabin(190, 215, 180, Rim: Red),
            new WheelLook(Steel, 26, Black, SoftLight, Steel, 1, RedFlat + 2),
            new Axle(new Vector3(250, 264, 420), new Vector3(440, 160, 420), 160, 115), new Axle(new Vector3(255, 264, -370), new Vector3(470, 180, -370), 180, 130));
    }

    // ---------------------------------------------------------------------------------------------------------------- Elevator Platform Island

    // A monkey monster of the platform: one of the crates they heave about, more of them roped on behind, his bananas on the bonnet.
    public static Body BuildCrate(Body monkey, Body racer)
    {
        var m = new CarMesh(Crate.Name);
        Roots(m);
        var hull = new Hull(m, 320, 12, 9, 0.5f, (560, 262, 168, 150), (540, 282, 185, 160), (180, 282, 185, 160), (-180, 282, 185, 160), (-520, 282, 185, 160), (-540, 262, 168, 150));
        hull.Skin(m, (band, _) => band % 2 == 0 ? Wood + 2 : Wood);
        hull.Nose(m, 566, Wood + 2);
        hull.Tail(m, -546, Wood + 2);
        foreach (var side in Sides)
        {
            // the braces across each side
            foreach (var (z0, z1) in new[] { (540f, 180f), (180, -180), (-180, -520) })
            {
                var x = side * 292;
                m.Rod(2, new(x, hull.Cy + 150, z0), new(x, hull.Cy - 130, z1), BrownDark); m.Rod(2, new(x, hull.Cy - 130, z0), new(x, hull.Cy + 150, z1), BrownDark);
            }
            m.Ball(2, new(side * 190, hull.Cy + 60, 568), 40, Yellow);
        }
        m.Box(2, hull.OnTop(-110, -400, 95), new Vector3(230, 190, 210), Wood + 1, Cream - 3);
        m.Box(2, hull.OnTop(140, -420, 70), new Vector3(180, 140, 170), Wood + 3, Cream - 3);
        m.Box(2, hull.OnTop(-90, -390, 250), new Vector3(150, 120, 140), Wood + 2, Cream - 2);
        foreach (var (x, z, r) in new[] { (-60f, 420f, 34), (-20, 440, 36), (20, 425, 34), (55, 445, 30), (0, 470, 26) }) m.Ball(2, hull.OnTop(x, z, 22), r, Amber);
        return Finish(m, hull, monkey, racer,
            new Driver(new Vector3(0, 700, -300), 0.5f, (19, new[] { 20, 21 }), (22, new[] { 23, 24 }), Reach: 230, GripHeight: 50, GripX: 120),
            new Cabin(-60, 245, 200, Rim: Wood + 3, Wheel: Wood, WheelLine: 22),
            new WheelLook(Wood, 28, Grey, 1, Wood + 2, 1, CreamFlat),
            new Axle(new Vector3(250, 245, 380), new Vector3(440, 155, 380), 155, 110), new Axle(new Vector3(250, 245, -370), new Vector3(440, 155, -370), 155, 110));
    }

    // The monkey monster with the sword, which he keeps in his hand: an iron war cart -- a ram in front, spikes and shields down its
    // sides, his banner at the back.
    public static Body BuildWarCart(Body monkey, Body racer)
    {
        var m = new CarMesh(WarCart.Name);
        Roots(m);
        var hull = new Hull(m, 300, 10, 4, 0.5f, (560, 215, 120, 112), (500, 270, 150, 128), (-380, 275, 152, 130), (-500, 230, 125, 115));
        hull.Skin(m, (_, k) => k is 9 ? Iron + 2 : Iron);
        hull.Nose(m, 780, Steel + 2, -30);
        hull.Tail(m, -512, Iron);
        foreach (var side in Sides)
        {
            foreach (var z in new[] { 380f, 60, -260 })
            {
                var at = new Vector3(side * (hull.Side(hull.Cy + 20, z) - 4), hull.Cy + 20, z);
                m.Cap(m.Loop(2, at, new Vector3(0, 36, 0), new Vector3(0, 0, 36), 4), m.P(2, at + new Vector3(side * 150, 10, 0)), Steel + 2, at - new Vector3(side * 40, 0, 0));
            }
            foreach (var z in new[] { 220f, -100 })
            {
                var at = new Vector3(side * (hull.Side(hull.Cy + 40, z) + 6), hull.Cy + 40, z);
                m.Ball(2, at, 68, RedFlat + 2); m.Ball(2, at + new Vector3(side * 8, 0, 0), 24, GoldFlat);
            }
        }
        {
            var foot = hull.OnTop(0, -440); var head = foot + new Vector3(0, 560, 0);
            m.Rod(2, foot, head, Dark + 2);
            m.Plate(2, Red, head, head + new Vector3(0, -40, -260), head + new Vector3(0, -150, -120), head + new Vector3(0, -260, -250));
            m.Plate(2, Red, head, head + new Vector3(0, -150, -120), head + new Vector3(0, -260, -250), head + new Vector3(0, -250, 0));
        }
        return Finish(m, hull, monkey, racer,
            new Driver(new Vector3(0, 900, -80), 0.62f, (9, new[] { 10, 11 }), null, Reach: 215, GripHeight: 70, GripX: 95),
            new Cabin(-40, 230, 195, Rim: Iron + 3),
            new WheelLook(Steel, 28, Grey, 1, Iron + 1, 1, RedFlat + 2, Lean: true),
            new Axle(new Vector3(240, 262, 360), new Vector3(430, 160, 360), 160, 115), new Axle(new Vector3(245, 262, -330), new Vector3(460, 180, -330), 180, 130));
    }

    // The Franco policeman who guards the way to the platform: his patrol car, white over blue, the red and blue lights on their bar.
    public static Body BuildPatrol(Body policeman, Body racer)
    {
        var m = new CarMesh(Patrol.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 300, 12, 4, 0.5f, (620, 205, 105, 105), (550, 258, 138, 122), (280, 272, 152, 130), (-330, 272, 158, 130), (-500, 262, 140, 124), (-580, 215, 112, 110));
        hull.Skin(m, (_, k) => k is 11 or 0 or 10 ? White : k is 3 or 4 or 5 or 6 or 7 ? GreySoft : BlueSoft);
        hull.Nose(m, 632, BlueSoft);
        hull.Tail(m, -592, BlueSoft);
        m.Light = 1;
        {
            int a = m.P(2, hull.OnTop(-235, -280)), b = m.P(2, hull.OnTop(-235, -280, 470)), c = m.P(2, hull.OnTop(235, -280, 470)), d = m.P(2, hull.OnTop(235, -280));
            m.Line(a, b, WhiteFlat); m.Line(c, d, WhiteFlat);
            m.Box(2, hull.OnTop(0, -280, 480), new Vector3(480, 36, 60), Steel + 2);
            m.Ball(2, hull.OnTop(-120, -280, 520), 46, RedFlat + 2); m.Ball(2, hull.OnTop(120, -280, 520), 46, BlueFlat);
        }
        foreach (var side in Sides)
        {
            for (var i = 0; i + 1 < hull.Rings.Count; i++) { var k = side > 0 ? 2 : 9; m.Line(hull.Rings[i][k], hull.Rings[i + 1][k], RedFlat + 2); }
            m.Ball(2, new(side * 150, hull.Cy + 40, 615), 46, Yellow);
            m.Rod(2, new(side * 190, hull.Cy - 50, 600), new(side * 190, hull.Cy - 50, 690), Dark + 3);
        }
        m.Rod(2, new(-190, hull.Cy - 50, 690), new(190, hull.Cy - 50, 690), Dark + 3);
        return Finish(m, hull, policeman, racer,
            new Driver(new Vector3(0, 520, 0), 0.78f, (6, new[] { 7, 8 }), null),
            new Cabin(-40, 215, 185, Rim: Blue + 2, Screen: 245),
            new WheelLook(Steel, 24, Grey, 1, White, SoftShare, BlueFlat),
            new Axle(new Vector3(250, 266, 400), new Vector3(435, 155, 400), 155, 105), new Axle(new Vector3(255, 266, -370), new Vector3(465, 175, -370), 175, 125));
    }

    // ---------------------------------------------------------------------------------------------------------------- Island under Celebration

    // The ferryman who takes Twinsen over the lava: his boat -- long, dark, its prow curling up over a lantern -- his pole lashed down its side.
    public static Body BuildFerry(Body ferryman, Body racer)
    {
        var m = new CarMesh(Ferry.Name);
        Roots(m);
        var hull = new Hull(m, 290, 10, 2.4f, 0, (640, 95, 85, 75), (560, 175, 110, 100), (390, 245, 132, 125), (100, 280, 142, 135), (-250, 275, 142, 135), (-480, 210, 125, 115), (-590, 110, 92, 82));
        hull.Skin(m, (band, _) => band % 2 == 0 ? BrownDark + 1 : Brown);
        {
            var a = m.Loop(2, new Vector3(0, hull.Cy + 50, 730), new Vector3(0, 50, 18), new Vector3(60, 0, 0), 10);
            var b = m.Loop(2, new Vector3(0, hull.Cy + 170, 790), new Vector3(0, 22, 30), new Vector3(30, 0, 0), 10);
            m.Skin(hull.Rings[0], a, _ => Brown, new Vector3(0, hull.Cy + 20, 690));
            m.Skin(a, b, _ => Brown, new Vector3(0, hull.Cy + 110, 760));
            m.Cap(b, m.P(2, new(0, hull.Cy + 300, 760)), Brown, new Vector3(0, hull.Cy + 170, 790));
            m.Rod(2, new(0, hull.Cy + 295, 760), new(0, hull.Cy + 215, 830), Dark + 3);
            m.Ball(2, new(0, hull.Cy + 180, 835), 36, Amber); m.Ball(2, new(0, hull.Cy + 180, 842), 16, Yellow);
        }
        hull.Tail(m, -660, Brown, 120);
        m.Rod(2, new(300, hull.Cy + 150, -560), new(255, hull.Cy + 95, 700), 22);
        m.Ball(2, new(300, hull.Cy + 150, -565), 16, RedFlat + 2);
        return Finish(m, hull, ferryman, racer,
            new Driver(new Vector3(0, 870, -120), 0.68f, (7, new[] { 8 }), (12, new[] { 13 }), new[] { 14, 17, 18, 24 }, Reach: 240, GripHeight: 60, GripX: 110),
            new Cabin(-70, 205, 180, Rim: Brown, Wheel: Wood, WheelLine: 22),
            new WheelLook(Wood, 24, Grey, 1, Brown, 1, Amber),
            new Axle(new Vector3(220, 255, 400), new Vector3(420, 150, 400), 150, 100), new Axle(new Vector3(240, 258, -330), new Vector3(460, 175, -330), 175, 120));
    }

    // The Mosquibee dissident of the hide-out: a dark car under his big red flag, a loud-hailer on its nose.
    public static Body BuildRebel(Body bee, Body racer)
    {
        var m = new CarMesh(Rebel.Name);
        Roots(m);
        var hull = new Hull(m, 310, 10, 2, 0, (520, 190, 130, 118), (420, 270, 180, 145), (180, 315, 210, 160), (-120, 320, 212, 160), (-360, 285, 190, 150), (-480, 190, 130, 118));
        hull.Skin(m, (band, _) => band == 2 ? Red : BrownDark + 2);
        hull.Tail(m, -505, BrownDark + 2);
        {
            var horn = m.Loop(2, new Vector3(0, hull.Cy + 10, 690), new Vector3(0, 125, 0), new Vector3(125, 0, 0), 10);
            m.Skin(hull.Rings[0], horn, _ => Steel, new Vector3(0, hull.Cy, 600));
            var throat = m.P(2, new(0, hull.Cy + 5, 540));
            for (var k = 0; k < 10; k++) m.In(new[] { horn[k], horn[(k + 1) % 10], throat }, HoleDark, new Vector3(0, hull.Cy + 10, 760), unlit: true);
        }
        {
            var foot = hull.OnTop(0, -400); var head = foot + new Vector3(0, 640, 0);
            m.Strut(2, foot, head, 24, Wood);
            var a = head + new Vector3(0, -10, -10); var b = a + new Vector3(0, 25, -190); var c = a + new Vector3(0, -15, -380); var d = c + new Vector3(0, -250, 0); var e = b + new Vector3(0, -250, 0); var f = a + new Vector3(0, -250, 0);
            int pa = m.P(2, a), pb = m.P(2, b), pc = m.P(2, c), pd = m.P(2, d), pe = m.P(2, e), pf = m.P(2, f);
            m.Both(new[] { pa, pb, pe, pf }, Red); m.Both(new[] { pb, pc, pd, pe }, Red);
            m.Ball(2, (a + d) / 2 + new Vector3(8, 0, 0), 52, Yellow); m.Ball(2, (a + d) / 2 + new Vector3(-8, 0, 0), 52, Yellow);
        }
        foreach (var side in Sides) m.Ball(2, new(side * 215, hull.Cy + 80, 420), 40, Yellow);
        return Finish(m, hull, bee, racer,
            new Driver(new Vector3(0, 600, 0), 0.8f),
            new Cabin(-90, 200, 175, Rim: Red),
            new WheelLook(Steel, 22, Grey, 1, Red, 1, Yellow),
            new Axle(new Vector3(225, 268, 370), new Vector3(425, 150, 370), 150, 105), new Axle(new Vector3(265, 272, -290), new Vector3(505, 185, -290), 185, 130));
    }

    // A Mosquibee of the hide-out on a slab of the lava that flows round it: black crust, the glow showing at its edges and through its
    // cracks, fire spitting out behind.
    public static Body BuildLavaSlab(Body bee, Body racer)
    {
        var m = new CarMesh(LavaSlab.Name);
        Roots(m);
        var hull = new Hull(m, 265, 12, 5, 0.5f, (590, 235, 72, 80), (490, 300, 95, 92), (160, 310, 100, 95), (-170, 305, 100, 95), (-430, 300, 95, 92), (-530, 235, 72, 80));
        hull.Skin(m, (_, k) => k is 2 or 3 or 7 or 8 ? Orange + 2 : k is 4 or 5 or 6 ? Orange : Grey);
        hull.Nose(m, 602, Orange + 2);
        hull.Tail(m, -542, Orange + 2);
        foreach (var path in new[]
                 {
                     new[] { new Vector3(-250, 0, 520), new Vector3(-120, 0, 380), new Vector3(-190, 0, 250), new Vector3(-60, 0, 180) },
                     new[] { new Vector3(260, 0, 440), new Vector3(150, 0, 330), new Vector3(210, 0, 210) },
                     new[] { new Vector3(-270, 0, -260), new Vector3(-150, 0, -330), new Vector3(-200, 0, -450), new Vector3(-60, 0, -500) },
                     new[] { new Vector3(250, 0, -250), new Vector3(120, 0, -340), new Vector3(190, 0, -470) },
                 })
        {
            var hs = path.Select(p => m.P(2, hull.OnTop(p.X, p.Z, 3))).ToArray();
            for (var i = 0; i + 1 < hs.Length; i++) m.Line(hs[i], hs[i + 1], i % 2 == 0 ? LavaLine : Amber);
        }
        foreach (var side in Sides)
        {
            Flame(m, new Vector3(side * 170, hull.Cy - 10, -535), 60, 200);
            m.Ball(2, new(side * 165, hull.Cy + 10, 600), 42, Amber);
        }
        foreach (var (x, y, z, r, c) in new[] { (0f, 60f, -650f, 34, Amber), (-70, 150, -760, 24, LavaLine), (60, 230, -820, 16, Yellow) }) m.Ball(2, new(x, hull.Cy + y, z), r, c);
        return Finish(m, hull, bee, racer,
            new Driver(new Vector3(0, 600, 0), 0.8f),
            new Cabin(-20, 190, 170, Up: 60, Rim: Grey + 1),
            new WheelLook(Steel - 2, 24, Grey, 1, Orange, 1, Amber),
            new Axle(new Vector3(250, 245, 390), new Vector3(440, 150, 390), 150, 105), new Axle(new Vector3(250, 245, -340), new Vector3(450, 165, -340), 165, 115));
    }

    // ---------------------------------------------------------------------------------------------------------------- from the user's notes

    // The wizard pedlar on his flying carpet: red and green bands, gold down its edges and a tassel on each corner, the front curling up,
    // his wares -- a lamp, jars, a rolled rug -- piled behind him.
    public static Body BuildCarpet(Body pedlar, Body racer)
    {
        var m = new CarMesh(Carpet.Name);
        Roots(m);
        var hull = new Hull(m, 235, 12, 8, 0.5f, (600, 300, 26, 26), (360, 300, 26, 26), (120, 300, 26, 26), (-120, 300, 26, 26), (-360, 300, 26, 26), (-600, 300, 26, 26));
        hull.Skin(m, (band, _) => band % 2 == 0 ? Red : Teal);
        {
            var a = m.Ring(2, new Vector3(0, hull.Cy + 45, 690), 300, 26, 26, 12, 8, 0.5f);
            var b = m.Ring(2, new Vector3(0, hull.Cy + 130, 700), 300, 24, 24, 12, 8, 0.5f);
            m.Skin(hull.Rings[0], a, _ => Teal, new Vector3(0, hull.Cy + 20, 640));
            m.Skin(a, b, _ => Red, new Vector3(0, hull.Cy + 90, 690));
            m.Cap(b, m.P(2, new(0, hull.Cy + 145, 702)), Gold, new Vector3(0, hull.Cy + 90, 695));
        }
        hull.Tail(m, -606, Gold);
        foreach (var side in Sides)
        {
            foreach (var k in new[] { 2, 3 }) for (var i = 0; i + 1 < hull.Rings.Count; i++) { var kk = side > 0 ? k : 11 - k; m.Line(hull.Rings[i][kk], hull.Rings[i + 1][kk], GoldFlat); }
            foreach (var z in new[] { 700f, -610 })
            {
                var corner = new Vector3(side * 305, hull.Cy + (z > 0 ? 130 : 0), z);
                m.Rod(2, corner, corner + new Vector3(side * 30, -70, z > 0 ? 20 : -40), GoldFlat);
                m.Ball(2, corner + new Vector3(side * 32, -85, z > 0 ? 22 : -44), 22, GoldFlat);
            }
        }
        // the wares
        {
            var lamp = hull.OnTop(-120, -330, 55);
            m.Ball(2, lamp, 62, GoldFlat); m.Ball(2, lamp + new Vector3(0, 60, 0), 24, GoldFlat);
            m.Rod(2, lamp + new Vector3(40, 10, 0), lamp + new Vector3(140, 60, 30), GoldFlat);
            m.Ball(2, hull.OnTop(110, -300, 60), 64, Cyan); m.Ball(2, hull.OnTop(110, -300, 135), 30, Cyan + 2);
            m.Ball(2, hull.OnTop(30, -450, 48), 52, Orange + 7); m.Ball(2, hull.OnTop(200, -460, 40), 44, GreenFlat);
            m.Strut(2, hull.OnTop(-250, -520, 40), hull.OnTop(200, -540, 40), 70, Blue + 2);
        }
        var wheel = new Axle(new Vector3(230, 225, 380), new Vector3(400, 115, 380), 115, 80);
        return Finish(m, hull, pedlar, racer,
            new Driver(new Vector3(0, 1130, -49), 0.75f, (4, new[] { 5, 6 }), (7, new[] { 8, 9 }), new[] { 20 }),
            new Cabin(-20, 200, 175, Up: 45, Rim: Gold, Wheel: Gold, WheelLine: GoldFlat),
            new WheelLook(Gold, 22, Grey, 1, Gold, 1, RedFlat + 2), wheel, wheel with { Mount = new Vector3(230, 225, -380), Hub = new Vector3(400, 115, -380) });
    }

    // The pighead who sweeps the Emperor's palace: a road sweeper in the yellow of his overalls, its brush turning in front, his own
    // broom standing at the back, bristles up, a bin and a warning light.
    public static Body BuildSweeper(Body pighead, Body racer)
    {
        var m = new CarMesh(Sweeper.Name);
        Roots(m);
        var hull = new Hull(m, 310, 12, 5, 0.5f, (560, 235, 118, 112), (500, 280, 150, 130), (0, 285, 155, 132), (-400, 280, 150, 130), (-480, 235, 118, 112));
        hull.Skin(m, (_, k) => k is 3 or 4 or 5 or 6 or 7 ? Grey + 2 : Ochre);
        hull.Nose(m, 572, Ochre);
        hull.Tail(m, -492, Grey + 2);
        // the brush: a roller of straw across the front, on two arms
        {
            var left = m.Loop(2, new Vector3(-330, 112, 660), new Vector3(0, 100, 0), new Vector3(0, 0, 100), 6);
            var right = m.Loop(2, new Vector3(330, 112, 660), new Vector3(0, 100, 0), new Vector3(0, 0, 100), 6);
            m.Skin(left, right, k => k % 2 == 0 ? Cream : Cream - 3, new Vector3(0, 112, 660));
            m.Cap(left, m.P(2, new(-340, 112, 660)), Steel, new Vector3(0, 112, 660)); m.Cap(right, m.P(2, new(340, 112, 660)), Steel, new Vector3(0, 112, 660));
            foreach (var side in Sides) m.Strut(2, new Vector3(side * 250, hull.Cy - 50, 520), new Vector3(side * 345, 125, 660), 30, Steel);
        }
        // the broom at the back
        {
            var foot = hull.OnTop(0, -430, -6); var neck = foot + new Vector3(0, 470, 0);
            m.Strut(2, foot, neck, 28, Wood);
            m.Plate(2, Cream, neck + new Vector3(-55, 0, 0), neck + new Vector3(55, 0, 0), neck + new Vector3(170, 230, 0), neck + new Vector3(-170, 230, 0));
            m.Rod(2, neck + new Vector3(-75, 40, 0), neck + new Vector3(75, 40, 0), RedFlat + 2);
        }
        m.Box(2, hull.OnTop(-170, -300, 75), new Vector3(150, 150, 150), Steel, Steel - 2);
        m.Rod(2, hull.OnTop(200, -300), hull.OnTop(200, -300, 170), Dark + 3);
        m.Ball(2, hull.OnTop(200, -300, 190), 34, Amber);
        foreach (var side in Sides) m.Ball(2, new(side * 165, hull.Cy + 50, 555), 42, Yellow);
        return Finish(m, hull, pighead, racer,
            new Driver(new Vector3(0, 625, -54), 0.85f, (9, new[] { 10, 11 }), (12, new[] { 13, 14 }), new[] { 24 }),
            new Cabin(30, 215, 185, Rim: Grey + 2),
            new WheelLook(Steel, 26, Grey, 1, Ochre, 1, WhiteFlat - 2, Lean: true),
            new Axle(new Vector3(250, 262, 350), new Vector3(430, 150, 350), 150, 105), new Axle(new Vector3(255, 262, -330), new Vector3(455, 170, -330), 170, 120));
    }

    // The Sup of the souvenir shop: his stall -- a wooden barrow, a striped awning over the goods (little Twinsuns, blue and green), and
    // on the bonnet the sign with the heart.
    public static Body BuildStall(Body sup, Body racer)
    {
        var m = new CarMesh(Stall.Name);
        Roots(m);
        var hull = new Hull(m, 300, 12, 6, 0.5f, (590, 225, 112, 108), (520, 278, 150, 130), (160, 285, 155, 132), (-200, 285, 155, 132), (-470, 280, 150, 130), (-545, 230, 115, 110));
        hull.Skin(m, (band, _) => band % 2 == 0 ? Wood : Wood + 2);
        hull.Nose(m, 602, Wood);
        hull.Tail(m, -557, Wood);
        // the awning: four strips, red and white, on four posts
        {
            const float Front = -110, Back = -520, Eave = 330, Ridge = 410;
            foreach (var (x, z) in new[] { (-270f, Front), (270f, Front), (-270f, Back), (270f, Back) }) m.Rod(2, hull.OnTop(x, z), hull.OnTop(x, z, Eave), 22);
            for (var i = 0; i < 4; i++)
            {
                float x0 = -290 + i * 145, x1 = x0 + 145;
                m.Light = i % 2 == 0 ? 1 : SoftShare;
                float Lift(float x) => Eave + (Ridge - Eave) * (1 - MathF.Abs(x) / 290);
                m.Plate(2, i % 2 == 0 ? Red : White, hull.OnTop(x0, Front + 20, Lift(x0)), hull.OnTop(x1, Front + 20, Lift(x1)), hull.OnTop(x1, Back - 20, Lift(x1)), hull.OnTop(x0, Back - 20, Lift(x0)));
            }
            m.Light = 1;
            foreach (var (x, z, r, c) in new[] { (-150f, -220f, 62, BlueFlat), (30, -260, 56, GreenFlat), (170, -210, 50, BlueFlat), (-60, -400, 58, GreenFlat), (120, -410, 60, BlueFlat) })
            {
                m.Ball(2, hull.OnTop(x, z, r - 6), r, c);
                m.Ball(2, hull.OnTop(x + 14, z + 10, r + 8), r / 3, c == BlueFlat ? GreenFlat : WhiteFlat);
            }
        }
        // the sign
        {
            var at = hull.OnTop(0, 430, 8);
            m.Light = SoftShare;
            m.Plate(2, White, at + new Vector3(-150, 0, 40), at + new Vector3(150, 0, 40), at + new Vector3(150, 190, -20), at + new Vector3(-150, 190, -20));
            m.Light = 1;
            var heart = at + new Vector3(0, 110, 22);
            m.Ball(2, heart + new Vector3(-28, 12, 0), 34, Heart); m.Ball(2, heart + new Vector3(28, 12, 0), 34, Heart);
            m.Flat(2, Heart, heart + new Vector3(-58, 2, 4), heart + new Vector3(58, 2, 4), heart + new Vector3(0, -78, 22));
        }
        foreach (var side in Sides) m.Ball(2, new(side * 160, hull.Cy + 45, 588), 42, Yellow);
        return Finish(m, hull, sup, racer,
            new Driver(new Vector3(0, 605, 0), 0.8f, (8, new[] { 9, 10 }), (11, new[] { 12, 13 })),
            new Cabin(130, 210, 175, Rim: Wood + 3, Wheel: Wood, WheelLine: 22),
            new WheelLook(Wood, 24, Grey, 1, Red, 1, WhiteFlat),
            new Axle(new Vector3(250, 262, 400), new Vector3(435, 160, 400), 160, 105), new Axle(new Vector3(255, 262, -350), new Vector3(465, 180, -350), 180, 110));
    }

    // The bouncer of Otringal's casino: a roulette wheel -- red and black all the way round, gold on its rim, the ball on it -- with a
    // pair of dice on the tail and a one-armed bandit's lever at his side.
    public static Body BuildRoulette(Body bouncer, Body racer)
    {
        var m = new CarMesh(Roulette.Name);
        Roots(m);
        var hull = Lens(m, 290, 10, 480, 110, 95, new[] { 455f, 380, 250, 100, -100, -250, -380, -455 });
        hull.Skin(m, (band, k) => k is 2 or 7 ? Gold : k is 3 or 4 or 5 or 6 ? Wood : (band + (k < 5 ? 0 : 1) + (k is 1 or 8 ? 1 : 0)) % 2 == 0 ? Red : Grey);
        hull.Nose(m, 482, Gold);
        hull.Tail(m, -482, Gold);
        foreach (var (x, z) in new[] { (175f, 175f), (-175f, 175f), (175f, -175f), (-175f, -175f) })
        {
            m.Strut(2, hull.OnTop(x, z, -6), hull.OnTop(x, z, 70), 26, Gold);
            m.Ball(2, hull.OnTop(x, z, 92), 34, GoldFlat);
        }
        m.Ball(2, hull.OnTop(-300, 230, 34), 40, WhiteFlat);
        m.Light = SoftShare;
        foreach (var (x, z, turn) in new[] { (-110f, -350f, 0f), (95f, -370f, 0f) })
        {
            var c = hull.OnTop(x, z, 78);
            m.Box(2, c, new Vector3(140, 140, 140), White);
            _ = turn;
        }
        m.Light = 1;
        foreach (var (x, z) in new[] { (-110f, -350f), (95f, -370f) })
        {
            var c = hull.OnTop(x, z, 78);
            foreach (var (dx, dz) in new[] { (-35f, -35f), (35f, 35f), (0f, 0f) }) m.Ball(2, c + new Vector3(dx, 74, dz), 13, Dark);
            m.Ball(2, c + new Vector3(x < 0 ? -74 : 74, 0, 0), 16, Dark);
        }
        {
            var foot = new Vector3(330, hull.Cy + 40, 60); var knob = foot + new Vector3(90, 330, -40);
            m.Strut(2, foot, knob, 26, Steel + 2);
            m.Ball(2, knob + new Vector3(8, 25, 0), 46, RedFlat + 2);
        }
        return Finish(m, hull, bouncer, racer,
            new Driver(new Vector3(0, 900, 0), 0.45f, (7, new[] { 8, 9, 10, 11 }), (12, new[] { 13, 14, 15, 16 }), Reach: 210, GripHeight: 70, GripX: 130),
            new Cabin(10, 215, 190, Rim: Gold, Wheel: Gold, WheelLine: GoldFlat),
            new WheelLook(Gold, 24, Grey, 1, Red, 1, GoldFlat),
            new Axle(new Vector3(205, 245, 290), new Vector3(450, 140, 290), 140, 95), new Axle(new Vector3(205, 245, -290), new Vector3(450, 140, -290), 140, 95));
    }
}
