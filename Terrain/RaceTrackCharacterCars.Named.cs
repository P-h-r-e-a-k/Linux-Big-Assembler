using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// Cars for the characters the game calls by name who had none yet (the dialogue and the body list name them):
//
//   Citadel Island   Luc the tavern's boss in a beer barrel; Tim his waiter on a serving tray; Mr. Paul in a tugboat; Mrs. Brune of the
//                    Inter-Islands ferries in a suitcase; Mr. Bazoo the shopkeeper in a brass cash register; Miss Bloop in a show case of
//                    her museum; Bob in a rowing boat and Felix with his rod on the fish he caught; Zed in a sandcastle; Dino-Fly himself;
//                    Rosa the cow in a milk float; the Tralu in a piece of his cave; Raph's band, Pat on the drums and Fab at the keyboards;
//                    the thief in a getaway car under the umbrella he stole
//   Desert Island    Joe the Elf in the green shell; Ker'aooc the healer in his cauldron; Tabata the wizard on a spell book; Moya the turtle
//   Otringal         the heavy metal guitarist of Rick's in a guitar; Rick at a grand piano; Stan in a time machine; the Emperor's wife in
//                    a royal coach
//
// (The islands under the gas have few names: the Queen, De La Fontaine and Mr. Kurtz have their cars already.)
internal static partial class RaceTrackCharacterCars
{
    public static readonly Car Guitarist = new("The guitarist's guitar", "the heavy metal guitarist of Rick's bar", 404, 37, BuildGuitar, Otringal);
    public static readonly Car Luc = new("Luc's barrel", "Luc, the boss of the tavern", 39, 38, BuildLuc, Citadel);
    public static readonly Car Tim = new("Tim's tray", "Tim the waiter", 40, 39, BuildTim, Citadel);
    public static readonly Car Paul = new("Mr. Paul's tugboat", "Mr. Paul", 59, 40, BuildPaul, Citadel);
    public static readonly Car Brune = new("Mrs. Brune's suitcase", "Mrs. Brune of the Inter-Islands ferries", 62, 41, BuildBrune, Citadel);
    public static readonly Car Bazoo = new("Mr. Bazoo's cash register", "Mr. Bazoo the shopkeeper", 99, 42, BuildBazoo, Citadel);
    public static readonly Car Bloop = new("Miss Bloop's show case", "Miss Bloop of the museum", 104, 43, BuildBloop, Citadel);
    public static readonly Car Bob = new("Bob's rowing boat", "Bob", 152, 44, BuildBob, Citadel);
    public static readonly Car Felix = new("Felix's fish", "Felix", 161, 45, BuildFelix, Citadel);
    public static readonly Car Zed = new("Zed's sandcastle", "Zed", 169, 46, BuildZed, Citadel);
    public static readonly Car DinoFly = new("Dino-Fly", "Dino-Fly", 167, 47, BuildDinoFly, Citadel);
    public static readonly Car Rosa = new("Rosa's milk float", "Rosa the cow", 177, 48, BuildRosa, Citadel);
    public static readonly Car Tralu = new("The Tralu's cave", "the Tralu", 33, 49, BuildTralu, Citadel);
    public static readonly Car Pat = new("Pat's drums", "Pat, of Raph's band", 44, 50, BuildPat, Citadel);
    public static readonly Car Fab = new("Fab's keyboards", "Fab, of Raph's band", 41, 51, BuildFab, Citadel);
    public static readonly Car Joe = new("Joe's shell", "Joe the Elf", 110, 52, BuildJoe, Desert);
    public static readonly Car Keraooc = new("Ker'aooc's cauldron", "Ker'aooc the healer", 188, 53, BuildKeraooc, Desert);
    public static readonly Car Tabata = new("Tabata's spell book", "Tabata the wizard", 189, 54, BuildTabata, Desert);
    public static readonly Car Moya = new("Moya", "Moya the turtle", 212, 55, BuildMoya, Desert);
    public static readonly Car Rick = new("Rick's piano", "Rick", 425, 56, BuildRick, Otringal);
    public static readonly Car Stan = new("Stan's time machine", "Stan", 439, 57, BuildStan, Otringal);
    public static readonly Car Empress = new("The Empress's coach", "the Emperor's wife", 454, 58, BuildEmpress, Otringal);
    public static readonly Car Thief = new("The thief's getaway car", "the Citadel Island thief", 117, 59, BuildThief, Citadel);

    private static Car[] Named => new[]
    {
        Guitarist, Luc, Tim, Paul, Brune, Bazoo, Bloop, Bob, Felix, Zed, DinoFly, Rosa, Tralu, Pat, Fab, Joe, Keraooc, Tabata, Moya, Rick, Stan, Empress, Thief,
    };

    private const int Sand = 34, GreenSoft = 136;

    // The grobos share their bones, and so do the rabbibunnies: where the waist is, and the arms (a grobo's are short and low: its hands
    // barely reach a wheel held close).
    private static Driver Grobo(float scale = 0.62f) => new(new Vector3(0, 470, 60), scale, (9, Array.Empty<int>()), (11, Array.Empty<int>()), Reach: 180, GripHeight: 30, GripX: 150);
    private static Driver Rabbibunny(bool right = true, bool left = true, int[]? drop = null, int[]? held = null)
        => new(new Vector3(0, 715, -90), 0.72f, right ? (4, new[] { 5, 6 }) : null, left ? (7, new[] { 8, 9 }) : null, drop, Held: held);

    // A drum or a can standing on the hull: a cylinder with a lid.
    private static void Can(CarMesh m, Vector3 foot, float radius, float height, int colour, int lid, int n = 6)
    {
        var a = Level(m, foot, radius, n); var b = Level(m, foot + new Vector3(0, height, 0), radius, n);
        m.Skin(a, b, _ => colour, foot + new Vector3(0, height / 2, 0));
        m.Cap(b, m.P(2, foot + new Vector3(0, height + 2, 0)), lid, foot);
    }

    // ---------------------------------------------------------------------------------------------------------------- Otringal

    // The guitarist who plays at Rick's, all in black with his tongue out: an electric guitar in his colours -- black, a white guard, silver
    // pegs and strings -- an amplifier on the tail, a demon's wings, and the fire of his show behind.
    public static Body BuildGuitar(Body guitarist, Body racer)
    {
        var m = new CarMesh(Guitarist.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 300, 10, 2, 0,
            (650, 75, 50, 48), (600, 95, 58, 52), (300, 100, 62, 56), (225, 210, 125, 105), (110, 305, 162, 128), (-70, 345, 176, 136), (-250, 348, 176, 136), (-420, 300, 160, 126), (-530, 190, 112, 100));
        hull.Skin(m, (band, k) => band == 1 ? Brown : band == 0 ? Black : k is 9 or 0 ? White : Black);
        hull.Tail(m, -560, Black);
        m.Plate(2, Black, new(-85, hull.Cy + 22, 640), new(85, hull.Cy + 22, 640), new(105, hull.Cy + 50, 800), new(-60, hull.Cy + 50, 775));
        m.Box(2, hull.OnTop(0, -490, 75), new Vector3(300, 150, 110), Black);
        foreach (var side in Sides)
        {
            // a demon's wing: two leaves
            Vector3 a = new(side * 300, hull.Cy + 60, -150), b = new(side * 560, hull.Cy + 330, -330), c = new(side * 470, hull.Cy + 190, -430), d = new(side * 600, hull.Cy + 170, -560), e = new(side * 330, hull.Cy + 40, -470);
            m.Plate(2, Black, a, b, c, e);
            m.Plate(2, Black, c, d, e);
        }
        m.Light = 1;
        foreach (var side in Sides)
        {
            foreach (var z in new[] { 670f, 715, 760 }) m.Ball(2, new(side * (95 + (z - 670) * 0.1f), hull.Cy + 40, z), 17, WhiteFlat);
            var at = hull.OnTop(0, -490, 75);
            m.Ball(2, at + new Vector3(side * 75, 0, -60), 52, Steel + 3); m.Ball(2, at + new Vector3(side * 75, 0, -66), 20, Dark);
            var mouth = new Vector3(side * 120, hull.Cy - 40, -575);
            var a = m.Loop(2, mouth + new Vector3(0, 0, 90), new Vector3(60, 0, 0), new Vector3(0, 60, 0), 6);
            var b = m.Loop(2, mouth, new Vector3(82, 0, 0), new Vector3(0, 82, 0), 6);
            m.Skin(a, b, _ => Steel, mouth + new Vector3(0, 0, 45));
            Flame(m, mouth + new Vector3(0, 0, 10), 60, 190);
            m.Ball(2, new(side * 130, hull.Cy + 30, 225), 40, RedFlat + 2);
        }
        foreach (var x in new[] { -42f, -14, 14, 42 })
        {
            m.Rod(2, new(x, hull.Cy + 50, 770), hull.OnTop(x, 210, 14), WhiteFlat);
            m.Rod(2, hull.OnTop(x, -300, 12), hull.OnTop(x, -440, 12), WhiteFlat);
        }
        m.Box(2, hull.OnTop(0, -445, 14), new Vector3(150, 24, 30), Steel);
        return Finish(m, hull, guitarist, racer,
            new Driver(new Vector3(0, 760, 0), 0.68f, (7, new[] { 8, 9 }), (10, new[] { 11, 12 }), new[] { 25 }),
            new Cabin(-110, 210, 175, Rim: Steel + 2, Screen: 190, ScreenHalf: 185),
            new WheelLook(Steel, 24, Black, SoftLight, Steel, 1, RedFlat + 2),
            new Axle(new Vector3(95, 268, 430), new Vector3(400, 150, 430), 150, 105), new Axle(new Vector3(285, 270, -300), new Vector3(520, 195, -300), 195, 140));
    }

    // Rick, of "Rick's Cafe": a grand piano, black, its keys across the front, the lid propped open, the champagne on ice.
    public static Body BuildRick(Body rick, Body racer)
    {
        var m = new CarMesh(Rick.Name);
        Roots(m);
        m.Light = SoftLight;
        var hull = new Hull(m, 300, 12, 6, 0.5f, (600, 270, 120, 115), (560, 290, 135, 125), (150, 290, 140, 128), (-250, 270, 140, 128), (-480, 200, 135, 122), (-560, 120, 120, 110));
        hull.Skin(m, (_, _) => Black);
        hull.Nose(m, 606, Black);
        hull.Tail(m, -572, Black);
        // the lid, up on its prop
        {
            int a = m.P(2, hull.OnTop(-265, 100, 2)), b = m.P(2, hull.OnTop(-240, -470, 2)), c = m.P(2, new(210, hull.Cy + 470, -470)), d = m.P(2, new(265, hull.Cy + 470, 100));
            m.Both(new[] { a, b, c, d }, Black + 1);
            m.Light = 1;
            m.Line(m.P(2, hull.OnTop(230, -150)), m.P(2, new(225, hull.Cy + 455, -150)), GoldFlat);
        }
        // the keys: a white board across the front, the black ones on it
        m.Light = SoftShare;
        m.Plate(2, White, hull.OnTop(-250, 585, 6), hull.OnTop(250, 585, 6), hull.OnTop(250, 480, 8), hull.OnTop(-250, 480, 8));
        m.Light = 1;
        for (var i = 0; i < 9; i++) { var x = -220 + i * 55f; if (i % 3 == 2) continue; m.Rod(2, hull.OnTop(x, 580, 10), hull.OnTop(x, 520, 12), Dark); m.Rod(2, hull.OnTop(x + 8, 580, 10), hull.OnTop(x + 8, 520, 12), Dark); }
        // the champagne: a bucket, the bottle's neck out of it
        Can(m, hull.OnTop(-150, 330, -4), 60, 105, Steel, Steel + 2);
        m.Strut(2, hull.OnTop(-150, 330, 95), hull.OnTop(-120, 330, 215), 30, Green);
        m.Ball(2, hull.OnTop(-117, 330, 228), 17, GoldFlat);
        foreach (var side in Sides) m.Ball(2, new(side * 200, hull.Cy + 30, 600), 38, GoldFlat);
        return Finish(m, hull, rick, racer,
            new Driver(new Vector3(0, 610, 40), 0.75f, (7, new[] { 8, 9 }), (10, new[] { 11, 12 }), GripX: 95),
            new Cabin(100, 215, 175, Rim: Gold, Wheel: Gold, WheelLine: GoldFlat),
            new WheelLook(Gold, 24, Black, SoftLight, Gold, 1, GoldFlat),
            new Axle(new Vector3(255, 262, 400), new Vector3(440, 150, 400), 150, 100), new Axle(new Vector3(230, 262, -330), new Vector3(430, 165, -330), 165, 110));
    }

    // Stan, come over from Time Commando: a time machine in the yellow of his suit -- a clock on each flank, two rings turning round its
    // tail, an aerial.
    public static Body BuildStan(Body stan, Body racer)
    {
        var m = new CarMesh(Stan.Name);
        Roots(m);
        var hull = new Hull(m, 300, 10, 2.6f, 0, (600, 130, 95, 90), (470, 250, 150, 130), (100, 290, 170, 140), (-350, 270, 160, 135), (-520, 170, 110, 105));
        hull.Skin(m, (_, k) => k is 2 or 7 ? Teal : Ochre);
        hull.Nose(m, 650, Teal);
        hull.Tail(m, -560, Teal);
        foreach (var side in Sides)
        {
            var face = new Vector3(side * (hull.Side(hull.Cy + 10, 330) + 8), hull.Cy + 10, 330);
            m.Ball(2, face, 92, WhiteFlat); m.Ball(2, face + new Vector3(side * 6, 0, 0), 14, Dark);
            m.Rod(2, face + new Vector3(side * 8, 0, 0), face + new Vector3(side * 8, 70, 0), Dark); m.Rod(2, face + new Vector3(side * 8, 0, 0), face + new Vector3(side * 8, 25, 50), Dark);
            m.Ball(2, new(side * 120, hull.Cy + 25, 600), 36, Cyan);
        }
        foreach (var (z, tilt) in new[] { (-380f, 0.35f), (-470f, -0.35f) })
        {
            var ring = m.Loop(2, new Vector3(0, hull.Cy + 20, z), new Vector3(330, 0, 330 * tilt), new Vector3(0, 300, 0), 8);
            for (var k = 0; k < 8; k++) m.Line(ring[k], ring[(k + 1) % 8], k % 2 == 0 ? GoldFlat : Cyan);
        }
        m.Rod(2, hull.OnTop(0, -300), hull.OnTop(0, -300, 330), WhiteFlat);
        m.Ball(2, hull.OnTop(0, -300, 345), 22, Cyan);
        return Finish(m, hull, stan, racer,
            new Driver(new Vector3(0, 1000, 20), 0.62f, (19, new[] { 20, 21 }), (16, new[] { 17, 18 }), Reach: 200, GripHeight: 80),
            new Cabin(-20, 205, 175, Rim: Teal + 2, Sides: 8),
            new WheelLook(Steel, 24, Grey, 1, Ochre, 1, Cyan, Lean: true),
            new Axle(new Vector3(235, 262, 380), new Vector3(430, 150, 380), 150, 100), new Axle(new Vector3(235, 262, -300), new Vector3(450, 165, -300), 165, 110));
    }

    // The Emperor's wife: a coach in the red of her gown, gold down its edges, a crown over the back of her seat, a lantern on each side,
    // gold wheels.
    public static Body BuildEmpress(Body empress, Body racer)
    {
        var m = new CarMesh(Empress.Name);
        Roots(m);
        var hull = new Hull(m, 310, 12, 4, 0.5f, (560, 210, 120, 115), (490, 265, 150, 130), (250, 280, 165, 135), (-300, 285, 185, 138), (-470, 265, 165, 132), (-540, 210, 125, 115));
        hull.Skin(m, (_, k) => k is 11 ? Red + 2 : Red);
        hull.Nose(m, 572, Gold);
        hull.Tail(m, -552, Red);
        foreach (var side in Sides)
        {
            foreach (var k in new[] { 1, 2 }) for (var i = 0; i + 1 < hull.Rings.Count; i++) { var kk = side > 0 ? k : 11 - k; m.Line(hull.Rings[i][kk], hull.Rings[i + 1][kk], GoldFlat); }
            var post = hull.OnTop(side * 235, 330, 0);
            m.Rod(2, post, post + new Vector3(0, 190, 0), GoldFlat);
            m.Ball(2, post + new Vector3(0, 215, 0), 34, Amber); m.Ball(2, post + new Vector3(0, 255, 0), 14, GoldFlat);
            m.Ball(2, new(side * 150, hull.Cy + 45, 560), 40, Yellow);
        }
        // the seat's back, red, and the crown on it: a gold band, its points, a jewel in front
        {
            int a = m.P(2, hull.OnTop(-200, -270, 0)), b = m.P(2, hull.OnTop(200, -270, 0)), c = m.P(2, new(170, 850, -330)), d = m.P(2, new(-170, 850, -330));
            m.Both(new[] { a, b, c, d }, Red + 2);
            var centre = new Vector3(0, 880, -330);
            var band = Level(m, centre, 150, 8); var top = Level(m, centre + new Vector3(0, 70, 0), 165, 8);
            for (var k = 0; k < 8; k++)
            {
                m.Both(new[] { band[k], band[(k + 1) % 8], top[(k + 1) % 8], top[k] }, Gold);
                var mid = (m.At(top[k]) + m.At(top[(k + 1) % 8])) / 2;
                m.Both(new[] { top[k], top[(k + 1) % 8], m.P(2, mid + new Vector3(0, 95, 0) + Vector3.Normalize(mid - (centre with { Y = mid.Y })) * 20) }, Gold);
            }
            m.Ball(2, centre + new Vector3(0, 35, 158), 26, RedFlat + 2);
        }
        return Finish(m, hull, empress, racer,
            new Driver(new Vector3(0, 800, 40), 0.68f, (7, new[] { 8, 9 }), (10, new[] { 11, 12 })),
            new Cabin(-70, 215, 180, Rim: Gold, Wheel: Gold, WheelLine: GoldFlat, Screen: 250, ScreenHalf: 180, Rail: GoldFlat),
            new WheelLook(Gold, 22, Gold - 2, 1, Red, 1, GoldFlat, Sides: 8),
            new Axle(new Vector3(245, 268, 380), new Vector3(430, 165, 380), 165, 70), new Axle(new Vector3(250, 268, -330), new Vector3(460, 195, -330), 195, 80));
    }

    // ---------------------------------------------------------------------------------------------------------------- Citadel Island

    // Luc, the boss of the tavern: one of his barrels on its side -- staves and hoops, the tap in front, two mugs of beer on top.
    public static Body BuildLuc(Body luc, Body racer)
    {
        var m = new CarMesh(Luc.Name);
        Roots(m);
        var hull = new Hull(m, 330, 10, 2, 0, (560, 235, 200, 180), (470, 300, 250, 200), (220, 340, 275, 210), (-160, 340, 275, 210), (-410, 300, 250, 200), (-500, 235, 200, 180));
        hull.Skin(m, (band, k) => band is 0 or 4 ? Steel - 2 : k % 2 == 0 ? Wood : Wood + 2);
        hull.Nose(m, 575, Wood + 1);
        hull.Tail(m, -515, Wood + 1);
        m.Strut(2, new Vector3(0, hull.Cy - 70, 570), new Vector3(0, hull.Cy - 70, 660), 34, Gold);
        m.Strut(2, new Vector3(0, hull.Cy - 60, 650), new Vector3(0, hull.Cy - 120, 650), 24, Gold);
        m.Ball(2, new(0, hull.Cy - 20, 650), 22, RedFlat + 2);
        m.Ball(2, new(0, hull.Cy - 150, 650), 14, Amber);
        foreach (var (x, z) in new[] { (-120f, -360f), (110f, -400f) })
        {
            var foot = hull.OnTop(x, z, -4);
            Can(m, foot, 62, 125, Gold, Gold);
            m.Ball(2, foot + new Vector3(0, 135, 0), 60, WhiteFlat); m.Ball(2, foot + new Vector3(25, 165, 10), 34, WhiteFlat);
            m.Rod(2, foot + new Vector3(62, 100, 0), foot + new Vector3(105, 70, 0), GoldFlat); m.Rod(2, foot + new Vector3(105, 70, 0), foot + new Vector3(62, 30, 0), GoldFlat);
        }
        foreach (var side in Sides) m.Ball(2, new(side * 190, hull.Cy + 110, 565), 38, Yellow);
        return Finish(m, hull, luc, racer, Grobo(),
            new Cabin(20, 240, 205, Rim: Wood + 3, Wheel: Wood, WheelLine: 22),
            new WheelLook(Wood, 26, Grey, 1, Wood + 2, 1, GoldFlat),
            new Axle(new Vector3(250, 270, 370), new Vector3(450, 155, 370), 155, 105), new Axle(new Vector3(250, 270, -310), new Vector3(470, 175, -310), 175, 120));
    }

    // Tim the waiter, his tray still in his hand: a bigger tray -- silver, a dish under its cover, a bottle and glasses -- on casters.
    public static Body BuildTim(Body tim, Body racer)
    {
        var m = new CarMesh(Tim.Name);
        Roots(m);
        var hull = Lens(m, 250, 10, 470, 62, 70, new[] { 445f, 370, 245, 100, -100, -245, -370, -445 });
        hull.Skin(m, (_, k) => k is 2 or 7 ? Steel + 2 : Steel);
        hull.Nose(m, 472, Steel + 2);
        hull.Tail(m, -472, Steel + 2);
        // the cover over its dish, a knob on top
        {
            var foot = hull.OnTop(0, -270, -4);
            var a = Level(m, foot, 170, 8); var b = Level(m, foot + new Vector3(0, 95, 0), 140, 8); var c = Level(m, foot + new Vector3(0, 160, 0), 80, 8);
            m.Skin(a, b, _ => Steel + 2, foot); m.Skin(b, c, _ => Steel + 2, foot);
            m.Cap(c, m.P(2, foot + new Vector3(0, 185, 0)), Steel + 2, foot);
            m.Ball(2, foot + new Vector3(0, 205, 0), 24, GoldFlat);
        }
        m.Strut(2, hull.OnTop(-230, 150, -4), hull.OnTop(-230, 150, 170), 62, Green);
        m.Strut(2, hull.OnTop(-230, 150, 170), hull.OnTop(-230, 150, 270), 26, Green);
        foreach (var (x, z, c) in new[] { (230f, 200f, Cyan), (280f, 60f, Cyan), (-290f, -30f, Amber) })
        {
            m.Rod(2, hull.OnTop(x, z), hull.OnTop(x, z, 70), WhiteFlat);
            m.Ball(2, hull.OnTop(x, z, 100), 40, c);
        }
        var wheel = new Axle(new Vector3(215, 215, 290), new Vector3(430, 115, 290), 115, 75);
        return Finish(m, hull, tim, racer, Rabbibunny(right: false),
            new Cabin(40, 195, 170, Up: 40, Rim: Steel + 2, Wheel: Steel, WheelLine: WhiteFlat),
            new WheelLook(Steel, 22, Grey, 1, Steel + 2, 1, WhiteFlat), wheel, wheel with { Mount = new Vector3(215, 215, -290), Hub = new Vector3(430, 115, -290) });
    }

    // Mr. Paul, the old sailor: a tugboat in his jersey's blue and white, red below -- the wheelhouse behind him, its funnel smoking,
    // old tyres down its sides, the anchor at the bow.
    public static Body BuildPaul(Body paul, Body racer)
    {
        var m = new CarMesh(Paul.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 300, 12, 3, 0.5f, (650, 110, 95, 100), (565, 230, 140, 125), (385, 285, 160, 135), (-300, 290, 165, 138), (-500, 245, 145, 128), (-585, 160, 105, 105));
        hull.Skin(m, (_, k) => k is 11 or 0 or 10 ? White : k is 3 or 4 or 5 or 6 or 7 ? RedSoft : BlueSoft);
        hull.Nose(m, 690, BlueSoft, 12);
        hull.Tail(m, -610, BlueSoft);
        var house = hull.OnTop(0, -320, 110);
        m.Box(2, house, new Vector3(300, 230, 230), White, RedSoft);
        var funnel = hull.OnTop(0, -320, 225);
        {
            var a = Level(m, funnel, 62, 6); var b = Level(m, funnel + new Vector3(0, 150, 0), 62, 6); var c = Level(m, funnel + new Vector3(0, 200, 0), 62, 6);
            m.Skin(a, b, _ => RedSoft, funnel + new Vector3(0, 70, 0)); m.Skin(b, c, _ => Black, funnel + new Vector3(0, 170, 0));
        }
        m.Light = 1;
        foreach (var (dy, dz, r) in new[] { (250f, -20f, 44), (330f, -80f, 56), (430f, -170f, 70) }) m.Ball(2, funnel + new Vector3(0, dy, dz), r, WhiteFlat - 3);
        foreach (var side in Sides)
        {
            m.Ball(2, house + new Vector3(side * 153, 20, 0), 46, Cyan);
            foreach (var z in new[] { 250f, -60 })
            {
                var at = new Vector3(side * (hull.Side(hull.Cy + 20, z) + 8), hull.Cy + 20, z);
                m.Ball(2, at, 62, Dark + 1); m.Ball(2, at + new Vector3(side * 6, 0, 0), 28, Dark + 5);
            }
            m.Ball(2, new(side * 140, hull.Cy + 50, 590), 38, Yellow);
        }
        // the anchor on the bow: shank, stock, arms
        {
            Vector3 On(float x, float up, float z) => hull.OnTop(x, z, up);
            m.Rod(2, On(0, 14, 560), On(0, 10, 420), Dark); m.Rod(2, On(-50, 12, 530), On(50, 12, 530), Dark);
            m.Rod(2, On(0, 10, 420), On(-70, 12, 470), Dark); m.Rod(2, On(0, 10, 420), On(70, 12, 470), Dark);
            m.Ball(2, On(0, 18, 575), 16, Dark + 3);
        }
        return Finish(m, hull, paul, racer, Grobo(),
            new Cabin(80, 235, 200, Rim: Wood + 2, Wheel: Wood, WheelLine: 22),
            new WheelLook(Steel, 24, Grey, 1, White, SoftShare, RedFlat + 2),
            new Axle(new Vector3(250, 262, 410), new Vector3(440, 155, 410), 155, 105), new Axle(new Vector3(265, 266, -330), new Vector3(495, 180, -330), 180, 125));
    }

    // Mrs. Brune, who sells the ferry tickets: a traveller's suitcase -- tan leather, two dark straps, brass corners and locks, a handle,
    // and the labels of everywhere it has been.
    public static Body BuildBrune(Body brune, Body racer)
    {
        var m = new CarMesh(Brune.Name);
        Roots(m);
        var hull = new Hull(m, 300, 12, 9, 0.5f,
            (565, 268, 150, 140), (545, 285, 165, 150), (300, 285, 165, 150), (225, 285, 165, 150), (-225, 285, 165, 150), (-300, 285, 165, 150), (-545, 285, 165, 150), (-565, 268, 150, 140));
        hull.Skin(m, (band, _) => band is 0 or 6 ? Gold : band is 2 or 4 ? BrownDark : Wood + 2);
        hull.Nose(m, 570, Wood + 2);
        hull.Tail(m, -570, Wood + 2);
        // the handle, on its right side; the locks either side of it
        {
            const float X = 292; var y = hull.Cy + 40;
            m.Strut(2, new Vector3(X, y, 130), new Vector3(X + 95, y, 110), 26, BrownDark);
            m.Strut(2, new Vector3(X + 95, y, 110), new Vector3(X + 95, y, -110), 26, BrownDark);
            m.Strut(2, new Vector3(X + 95, y, -110), new Vector3(X, y, -130), 26, BrownDark);
            foreach (var z in new[] { 262f, -262 }) m.Ball(2, new(X + 4, y, z), 30, GoldFlat);
            m.Rod(2, new(X + 95, y - 10, 0), new(X + 120, y - 120, -30), CreamFlat);
            m.Flat(2, WhiteFlat, new(X + 110, y - 120, -70), new(X + 130, y - 120, 10), new(X + 130, y - 200, 10), new(X + 110, y - 200, -70));
        }
        foreach (var (x, z, r, c) in new[] { (-160f, -420f, 56, RedFlat + 2), (20, -400, 50, BlueFlat), (160, -440, 46, Yellow), (-60, -300, 40, GreenFlat), (150, 420, 48, Cyan), (-150, 440, 52, Amber) })
            m.Ball(2, hull.OnTop(x, z, 3), r, c);
        foreach (var side in Sides) m.Ball(2, new(side * 190, hull.Cy + 40, 572), 38, Yellow);
        return Finish(m, hull, brune, racer, Rabbibunny(),
            new Cabin(60, 215, 185, Rim: BrownDark, Wheel: Wood, WheelLine: GoldFlat),
            new WheelLook(Gold, 24, Grey, 1, Wood + 2, 1, GoldFlat),
            new Axle(new Vector3(250, 245, 380), new Vector3(440, 145, 380), 145, 95), new Axle(new Vector3(250, 245, -380), new Vector3(440, 145, -380), 145, 95));
    }

    // Mr. Bazoo the shopkeeper: a brass cash register -- the keys in rows on its front, the price tabs up at the back, the crank at its
    // side, coins in the tray.
    public static Body BuildBazoo(Body bazoo, Body racer)
    {
        var m = new CarMesh(Bazoo.Name);
        Roots(m);
        var hull = new Hull(m, 300, 12, 6, 0.5f, (580, 245, 115, 115), (520, 285, 145, 132), (-150, 285, 150, 135), (-210, 285, 290, 135), (-470, 285, 290, 135), (-540, 250, 255, 125));
        hull.Skin(m, (band, k) => band == 2 ? Gold : k is 3 or 4 or 5 or 6 or 7 ? Gold - 4 : Gold - 2);
        hull.Nose(m, 590, Gold - 2);
        hull.Tail(m, -548, Gold - 2);
        for (var row = 0; row < 3; row++)
            for (var col = 0; col < 5; col++)
                m.Ball(2, hull.OnTop(-170 + col * 85, 500 - row * 75, 10), 27, (row + col) % 4 == 0 ? RedFlat + 2 : WhiteFlat);
        // the price tabs; the crank; the coins
        m.Light = SoftShare;
        foreach (var (x, up) in new[] { (-150f, 120f), (-50f, 170f), (50f, 110f), (150f, 150f) })
        {
            var foot = hull.OnTop(x, -300, 0);
            m.Plate(2, White, foot + new Vector3(-38, 0, 0), foot + new Vector3(38, 0, 0), foot + new Vector3(38, up, 0), foot + new Vector3(-38, up, 0));
        }
        m.Light = 1;
        {
            var hub = new Vector3(292, hull.Cy + 140, -340);
            m.Strut(2, hub, hub + new Vector3(70, 0, 0), 30, Steel);
            m.Strut(2, hub + new Vector3(70, 0, 0), hub + new Vector3(70, 150, 60), 24, Steel);
            m.Ball(2, hub + new Vector3(78, 165, 66), 36, RedFlat + 2);
        }
        foreach (var (x, z) in new[] { (-120f, 250f), (-60f, 215f), (40f, 245f), (110f, 210f), (-10f, 270f) }) m.Ball(2, hull.OnTop(x, z, 10), 30, GoldFlat);
        foreach (var side in Sides) m.Ball(2, new(side * 170, hull.Cy + 35, 585), 38, Yellow);
        return Finish(m, hull, bazoo, racer, Grobo(),
            new Cabin(20, 235, 150, Rim: Gold - 4, Wheel: Steel, WheelLine: Dark),
            new WheelLook(Steel, 24, Grey, 1, Gold - 2, 1, RedFlat + 2),
            new Axle(new Vector3(250, 262, 390), new Vector3(440, 150, 390), 150, 100), new Axle(new Vector3(250, 262, -350), new Vector3(455, 170, -350), 170, 115));
    }

    // Miss Bloop, of the private museum: one of its show cases -- a marble plinth, the glass over a gold vase on its red cushion, the
    // red rope on its gold posts down each side.
    public static Body BuildBloop(Body bloop, Body racer)
    {
        var m = new CarMesh(Bloop.Name);
        Roots(m);
        var hull = new Hull(m, 300, 12, 7, 0.5f, (585, 250, 130, 125), (545, 285, 150, 135), (0, 285, 150, 135), (-505, 285, 150, 135), (-545, 250, 130, 125));
        hull.Skin(m, (_, k) => k is 11 or 0 or 10 ? Cream : Cream - 3);
        hull.Nose(m, 592, Cream - 3);
        hull.Tail(m, -552, Cream - 3);
        {
            var foot = hull.OnTop(0, -330, -2);
            var floor = new[] { new Vector3(-150, 0, -130), new Vector3(150, 0, -130), new Vector3(150, 0, 130), new Vector3(-150, 0, 130) }.Select(c => m.P(2, foot + c)).ToArray();
            var roof = new[] { new Vector3(-150, 330, -130), new Vector3(150, 330, -130), new Vector3(150, 330, 130), new Vector3(-150, 330, 130) }.Select(c => m.P(2, foot + c)).ToArray();
            for (var k = 0; k < 4; k++)
            {
                m.Both(new[] { floor[k], floor[(k + 1) % 4], roof[(k + 1) % 4], roof[k] }, Pane, CarMesh.SeeThrough);
                m.Line(floor[k], roof[k], GoldFlat); m.Line(roof[k], roof[(k + 1) % 4], GoldFlat);
            }
            m.Both(roof, Pane, CarMesh.SeeThrough);
            m.Box(2, foot + new Vector3(0, 30, 0), new Vector3(190, 60, 170), Red);
            m.Ball(2, foot + new Vector3(0, 130, 0), 66, GoldFlat); m.Ball(2, foot + new Vector3(0, 215, 0), 30, GoldFlat); m.Ball(2, foot + new Vector3(0, 255, 0), 40, GoldFlat);
        }
        foreach (var side in Sides)
        {
            var posts = new[] { 480f, 250f, 30f }.Select(z => hull.OnTop(side * 255, z, 0)).ToArray();
            foreach (var post in posts) { m.Rod(2, post, post + new Vector3(0, 150, 0), GoldFlat); m.Ball(2, post + new Vector3(0, 165, 0), 22, GoldFlat); }
            for (var i = 0; i + 1 < posts.Length; i++)
            {
                var sag = m.P(2, (posts[i] + posts[i + 1]) / 2 + new Vector3(0, 95, 0));
                m.Line(m.P(2, posts[i] + new Vector3(0, 140, 0)), sag, RedFlat + 2); m.Line(sag, m.P(2, posts[i + 1] + new Vector3(0, 140, 0)), RedFlat + 2);
            }
            m.Ball(2, new(side * 170, hull.Cy + 40, 590), 38, Yellow);
        }
        return Finish(m, hull, bloop, racer, Grobo(),
            new Cabin(140, 230, 190, Rim: Cream, Wheel: Gold, WheelLine: GoldFlat),
            new WheelLook(Gold, 24, Grey, 1, Cream, 1, GoldFlat),
            new Axle(new Vector3(250, 255, 400), new Vector3(440, 150, 400), 150, 100), new Axle(new Vector3(250, 255, -360), new Vector3(450, 165, -360), 165, 110));
    }

    // Bob, in his oilskin: a green rowing boat, an oar out on each side, his catch in a bucket behind him.
    public static Body BuildBob(Body bob, Body racer)
    {
        var m = new CarMesh(Bob.Name);
        Roots(m);
        var hull = new Hull(m, 300, 10, 2.4f, 0, (650, 75, 80, 72), (545, 200, 112, 105), (310, 272, 136, 130), (-250, 272, 138, 130), (-480, 222, 126, 116), (-575, 150, 100, 95));
        hull.Skin(m, (band, _) => band % 2 == 0 ? Green : Green + 2);
        hull.Nose(m, 700, Green, 25);
        hull.Tail(m, -600, Green);
        foreach (var side in Sides)
        {
            for (var i = 0; i + 1 < hull.Rings.Count; i++) { var k = side > 0 ? 1 : 9; m.Line(hull.Rings[i][k], hull.Rings[i + 1][k], WhiteFlat); }
            var lockAt = hull.OnTop(side * 255, -40, 20); var blade = new Vector3(side * 640, 150, -400);
            m.Strut(2, lockAt + new Vector3(side * -90, 60, 50), blade, 24, Wood);
            m.Plate(2, Cream, blade + new Vector3(0, 45, 20), blade + new Vector3(side * 120, -5, -130), blade + new Vector3(side * 110, -70, -150), blade + new Vector3(0, -45, -20));
            m.Ball(2, lockAt + new Vector3(0, 12, 0), 20, GoldFlat);
            m.Ball(2, new(side * 140, hull.Cy + 40, 610), 36, Yellow);
        }
        Can(m, hull.OnTop(-60, -420, -4), 95, 150, Steel, HoleDark);
        foreach (var (x, up, c) in new[] { (-95f, 175f, Cyan), (-30f, 190f, Cyan + 2), (-70f, 215f, Cyan) }) m.Ball(2, hull.OnTop(x, -420, up), 40, c);
        m.Rod(2, hull.OnTop(150, -470), hull.OnTop(150, -470, 260), 22);
        m.Ball(2, hull.OnTop(150, -470, 285), 34, Amber);
        return Finish(m, hull, bob, racer, Grobo(),
            new Cabin(60, 225, 195, Rim: Wood + 2, Wheel: Wood, WheelLine: 22),
            new WheelLook(Wood, 24, Grey, 1, Green, 1, WhiteFlat),
            new Axle(new Vector3(225, 258, 400), new Vector3(420, 150, 400), 150, 100), new Axle(new Vector3(235, 258, -340), new Vector3(450, 170, -340), 170, 115));
    }

    // Felix, his rod up in his hand: the fish he caught with it, and what a fish -- blue back, silver flanks, fins, a tail, an eye
    // on each side.
    public static Body BuildFelix(Body felix, Body racer)
    {
        var m = new CarMesh(Felix.Name);
        Roots(m);
        var hull = new Hull(m, 320, 10, 2, 0, (620, 95, 85, 80), (520, 215, 175, 155), (300, 292, 242, 192), (0, 302, 252, 196), (-280, 232, 202, 162), (-460, 125, 122, 102), (-545, 62, 72, 62));
        hull.Skin(m, (_, k) => k is 0 or 9 or 1 or 8 ? Blue + 2 : k is 2 or 7 ? Steel : Steel + 2);
        var lips = m.P(2, new(0, hull.Cy - 10, 560));
        for (var k = 0; k < 10; k++) m.In(new[] { hull.Rings[0][k], hull.Rings[0][(k + 1) % 10], lips }, HoleDark, new Vector3(0, hull.Cy, 800), unlit: true);
        {
            var root = new Vector3(0, hull.Cy, -540);
            m.Plate(2, Blue + 2, root + new Vector3(0, 60, 0), root + new Vector3(0, 320, -260), root + new Vector3(0, 40, -170), root + new Vector3(0, -50, 0));
            m.Plate(2, Blue + 2, root + new Vector3(0, 40, -170), root + new Vector3(0, -250, -250), root + new Vector3(0, -50, 0));
            m.Skin(hull.Rings[^1], m.Loop(2, root + new Vector3(0, 0, -20), new Vector3(0, 40, 0), new Vector3(14, 0, 0), 10), _ => Blue + 2, root);
            m.Plate(2, Blue + 2, hull.OnTop(0, -150, -6), hull.OnTop(0, -260, 190), hull.OnTop(0, -400, 120), hull.OnTop(0, -400, -6));
        }
        foreach (var side in Sides)
        {
            var eye = new Vector3(side * (hull.Side(hull.Cy + 70, 470) + 6), hull.Cy + 70, 470);
            m.Ball(2, eye, 52, WhiteFlat); m.Ball(2, eye + new Vector3(side * 8, 0, 10), 22, Dark);
            var fin = new Vector3(side * hull.Side(hull.Cy - 60, 250), hull.Cy - 60, 250);
            m.Plate(2, Blue + 2, fin, fin + new Vector3(side * 190, -60, -130), fin + new Vector3(side * 150, -90, -230), fin + new Vector3(0, -20, -140));
        }
        return Finish(m, hull, felix, racer, Rabbibunny(held: new[] { 9 }),
            new Cabin(60, 200, 175, Rim: Blue + 3, Wheel: Steel, WheelLine: WhiteFlat),
            new WheelLook(Steel, 22, Grey, 1, Blue + 2, 1, WhiteFlat),
            new Axle(new Vector3(215, 262, 400), new Vector3(420, 150, 400), 150, 100), new Axle(new Vector3(200, 262, -300), new Vector3(440, 165, -300), 165, 110));
    }

    // Zed, off the beach: a sandcastle -- a tower with its flag on each back corner, battlements between them, his bucket and spade
    // and a beach ball on the front.
    public static Body BuildZed(Body zed, Body racer)
    {
        var m = new CarMesh(Zed.Name);
        Roots(m);
        var hull = new Hull(m, 300, 12, 6, 0.5f, (585, 245, 120, 120), (530, 285, 145, 135), (0, 290, 150, 138), (-470, 285, 145, 135), (-540, 245, 120, 120));
        hull.Skin(m, (band, _) => band % 2 == 0 ? Sand : Sand + 1);
        hull.Nose(m, 595, Sand);
        hull.Tail(m, -550, Sand);
        foreach (var side in Sides)
        {
            var foot = hull.OnTop(side * 205, -400, -6);
            Can(m, foot, 95, 250, Sand + 1, Sand - 2);
            m.Cap(Level(m, foot + new Vector3(0, 250, 0), 105, 6), m.P(2, foot + new Vector3(0, 380, 0)), Orange + 2, foot);
            m.Rod(2, foot + new Vector3(0, 375, 0), foot + new Vector3(0, 520, 0), Dark + 2);
            m.Flat(2, side > 0 ? RedFlat + 2 : BlueFlat, foot + new Vector3(0, 520, 0), foot + new Vector3(0, 480, -110), foot + new Vector3(0, 440, 0));
            m.Ball(2, new(side * 170, hull.Cy + 40, 590), 38, Yellow);
        }
        foreach (var x in new[] { -80f, 0, 80 }) m.Box(2, hull.OnTop(x, -440, 45), new Vector3(55, 90, 80), Sand + 1);
        // the bucket and the spade; the ball
        Can(m, hull.OnTop(-150, 430, -4), 70, 120, Red, Sand);
        m.Rod(2, hull.OnTop(-60, 480, 6), hull.OnTop(-30, 330, 110), GoldFlat);
        m.Flat(2, BlueFlat, hull.OnTop(-85, 500, 6), hull.OnTop(-35, 500, 6), hull.OnTop(-45, 560, 5), hull.OnTop(-75, 560, 5));
        {
            var ball = hull.OnTop(150, 420, 85);
            m.Ball(2, ball, 88, WhiteFlat); m.Ball(2, ball + new Vector3(-30, 25, 8), 52, RedFlat + 2); m.Ball(2, ball + new Vector3(34, -20, 8), 46, BlueFlat); m.Ball(2, ball + new Vector3(10, 45, 12), 26, Yellow);
        }
        return Finish(m, hull, zed, racer, Grobo(),
            new Cabin(40, 235, 200, Rim: Sand - 2, Wheel: Wood, WheelLine: 22),
            new WheelLook(Wood, 24, Grey, 1, Sand, 1, RedFlat + 2),
            new Axle(new Vector3(250, 258, 390), new Vector3(440, 150, 390), 150, 100), new Axle(new Vector3(250, 258, -330), new Vector3(455, 170, -330), 170, 115));
    }

    // Dino-Fly himself, on wheels: his green body and pale belly, the spines down his back, his tail, his wings spread, and his own
    // neck and head looking out in front.
    public static Body BuildDinoFly(Body dino, Body racer)
    {
        var m = new CarMesh(DinoFly.Name);
        Roots(m);
        var hull = new Hull(m, 320, 10, 2, 0, (520, 150, 140, 120), (390, 250, 200, 160), (110, 300, 232, 180), (-200, 282, 216, 172), (-420, 190, 150, 130), (-540, 100, 85, 80));
        hull.Skin(m, (_, k) => k is 4 or 5 ? Cream : Teal);
        hull.Nose(m, 560, Teal);
        {
            var a = m.Loop(2, new Vector3(0, hull.Cy + 40, -680), new Vector3(0, 50, 0), new Vector3(58, 0, 0), 10);
            m.Skin(hull.Rings[^1], a, _ => Teal, new Vector3(0, hull.Cy + 20, -610));
            m.Cap(a, m.P(2, new(0, hull.Cy + 150, -860)), Teal, new Vector3(0, hull.Cy + 40, -680));
        }
        foreach (var z in new[] { -50f, -190, -320, -440 })
            m.Plate(2, Orange + 2, hull.OnTop(0, z + 55, -6), hull.OnTop(0, z - 10, 95), hull.OnTop(0, z - 60, -6));
        foreach (var side in Sides)
        {
            // a wing: two leaves from the shoulder, a finger down each
            var shoulder = hull.OnTop(side * 170, 60, -10);
            var elbow = new Vector3(side * 470, 720, -120); var tip = new Vector3(side * 690, 560, -420); var rear = new Vector3(side * 450, 470, -560); var body = hull.OnTop(side * 190, -330, -10);
            int s = m.P(2, shoulder), e = m.P(2, elbow), t = m.P(2, tip), r = m.P(2, rear), b = m.P(2, body);
            m.Both(new[] { s, e, t, r }, Teal + 2);
            m.Both(new[] { s, r, b }, Teal + 2);
            m.Line(s, e, Dark + 2); m.Line(e, t, Dark + 2); m.Line(e, r, Dark + 2);
        }
        return Finish(m, hull, dino, racer,
            new Driver(new Vector3(0, 900, 300), 0.42f, Keep: new[] { 3, 4, 5, 6, 7 }),
            new Cabin(250, 140, 130, Up: 8, Rim: Teal, Sides: 8),
            new WheelLook(Teal, 26, Grey, 1, Cream, 1, Orange + 8),
            new Axle(new Vector3(200, 270, 330), new Vector3(420, 150, 330), 150, 105), new Axle(new Vector3(235, 270, -250), new Vector3(470, 175, -250), 175, 120));
    }

    // Rosa the cow, her head and horns out over the front: a milk float in her black and white, three churns on the back, her bell
    // under its nose.
    public static Body BuildRosa(Body rosa, Body racer)
    {
        var m = new CarMesh(Rosa.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 300, 12, 5, 0.5f, (560, 225, 118, 112), (490, 280, 150, 130), (230, 285, 155, 132), (-30, 285, 155, 132), (-290, 285, 155, 132), (-480, 280, 150, 130), (-550, 230, 118, 112));
        hull.Skin(m, (band, k) => (band * 5 + k * 3) % 7 < 3 ? Black : White);
        hull.Nose(m, 572, White);
        hull.Tail(m, -562, White);
        m.Light = 1;
        foreach (var (x, z) in new[] { (-150f, -390f), (20f, -330f), (165f, -410f) })
        {
            var foot = hull.OnTop(x, z, -4);
            Can(m, foot, 78, 190, Steel, Steel + 2);
            Can(m, foot + new Vector3(0, 190, 0), 46, 60, Steel + 2, Steel);
        }
        m.Rod(2, new(0, hull.Cy - 60, 570), new(0, hull.Cy - 130, 585), Wood);
        m.Ball(2, new(0, hull.Cy - 165, 588), 44, GoldFlat);
        foreach (var side in Sides) m.Ball(2, new(side * 165, hull.Cy + 40, 565), 38, Yellow);
        return Finish(m, hull, rosa, racer,
            new Driver(new Vector3(0, 640, 300), 0.6f, Keep: new[] { 3, 4, 5, 6, 7, 8, 9 }),
            new Cabin(120, 215, 170, Rim: Steel),
            new WheelLook(Steel, 24, Grey, 1, White, SoftShare, RedFlat + 2),
            new Axle(new Vector3(250, 262, 380), new Vector3(435, 150, 380), 150, 100), new Axle(new Vector3(250, 262, -340), new Vector3(455, 170, -340), 170, 115));
    }

    // The Tralu, out of his cave and bringing it with him: a slab of its rock, stalagmites round it, and what is left of those he ate.
    public static Body BuildTralu(Body tralu, Body racer)
    {
        var m = new CarMesh(Tralu.Name);
        Roots(m);
        var hull = new Hull(m, 300, 10, 2.6f, 0, (560, 200, 120, 112), (440, 290, 165, 135), (150, 320, 180, 142), (-200, 315, 178, 142), (-430, 270, 160, 132), (-540, 180, 118, 110));
        hull.Skin(m, (band, k) => (band + k) % 3 == 0 ? Stone - 2 : Stone);
        hull.Nose(m, 585, Stone - 2);
        hull.Tail(m, -565, Stone - 2);
        foreach (var (x, z, h, r) in new[] { (-250f, 330f, 210f, 50f), (255, 250, 260, 56), (-270, -150, 170, 46), (250, -250, 300, 60), (-160, -440, 250, 56), (120, -470, 180, 46) })
        {
            var foot = hull.OnTop(x, z, -8);
            m.Cap(Level(m, foot, r, 5), m.P(2, foot + new Vector3(x * 0.06f, h, 0)), Stone + 2, foot - new Vector3(0, 40, 0));
        }
        // a skull and crossed bones on the front; ribs at the back
        {
            var skull = hull.OnTop(0, 430, 40);
            m.Ball(2, skull, 52, WhiteFlat); m.Ball(2, skull + new Vector3(0, -34, 26), 30, WhiteFlat);
            foreach (var side in Sides) m.Ball(2, skull + new Vector3(side * 18, 8, 44), 11, Dark);
            m.Rod(2, hull.OnTop(-120, 330, 10), hull.OnTop(120, 480, 10), WhiteFlat); m.Rod(2, hull.OnTop(120, 330, 10), hull.OnTop(-120, 480, 10), WhiteFlat);
            foreach (var z in new[] { -300f, -350, -400 })
            {
                int a = m.P(2, hull.OnTop(-110, z, 5)), b = m.P(2, hull.OnTop(0, z, 130)), c = m.P(2, hull.OnTop(110, z, 5));
                m.Line(a, b, WhiteFlat); m.Line(b, c, WhiteFlat);
            }
        }
        foreach (var side in Sides) m.Ball(2, new(side * 150, hull.Cy + 40, 575), 36, Amber);
        return Finish(m, hull, tralu, racer,
            new Driver(new Vector3(0, 1100, 50), 0.4f, (12, new[] { 13, 14 }), (15, new[] { 16, 17 }), Reach: 200, GripHeight: 70, GripX: 110),
            new Cabin(20, 225, 195, Rim: Stone - 2, Wheel: Stone + 2, WheelLine: WhiteFlat),
            new WheelLook(Stone, 28, Stone - 2, 1, Stone + 2, 1, WhiteFlat),
            new Axle(new Vector3(255, 262, 380), new Vector3(450, 160, 380), 160, 115), new Axle(new Vector3(255, 262, -330), new Vector3(475, 180, -330), 180, 130));
    }

    // Pat, who plays with Raph: the band's drums -- the big drum on its side for a body, two toms on it, a cymbal on each back corner,
    // the sticks crossed on the front.
    public static Body BuildPat(Body pat, Body racer)
    {
        var m = new CarMesh(Pat.Name);
        Roots(m);
        var hull = new Hull(m, 330, 10, 2, 0, (520, 300, 260, 205), (440, 320, 275, 210), (-380, 320, 275, 210), (-460, 300, 260, 205));
        hull.Skin(m, (band, _) => band == 1 ? Red : Steel + 2);
        hull.Nose(m, 530, Cream);
        hull.Tail(m, -470, Cream);
        foreach (var (x, z, c) in new[] { (-130f, 300f, Blue + 2), (130f, 300f, Blue + 2) })
        {
            var foot = hull.OnTop(x, z, -6);
            Can(m, foot, 100, 110, c, Cream);
        }
        foreach (var side in Sides)
        {
            var foot = hull.OnTop(side * 230, -330, 0); var top = foot + new Vector3(side * 40, 330, 0);
            m.Rod(2, foot, top, WhiteFlat);
            var rim = Level(m, top, 130, 8); var centre = m.P(2, top + new Vector3(0, 22, 0));
            for (var k = 0; k < 8; k++) m.Both(new[] { rim[k], rim[(k + 1) % 8], centre }, Gold);
            m.Ball(2, new(side * 200, hull.Cy + 40, 525), 38, Yellow);
        }
        m.Rod(2, hull.OnTop(-110, 470, 10), hull.OnTop(90, 380, 10), CreamFlat); m.Rod(2, hull.OnTop(110, 470, 10), hull.OnTop(-90, 380, 10), CreamFlat);
        return Finish(m, hull, pat, racer,
            new Driver(new Vector3(0, 850, 0), 0.85f, (14, new[] { 15, 16 }), (11, new[] { 12, 13 })),
            new Cabin(-40, 210, 185, Rim: Steel + 2),
            new WheelLook(Steel, 24, Grey, 1, Red, 1, WhiteFlat),
            new Axle(new Vector3(255, 270, 350), new Vector3(455, 155, 350), 155, 105), new Axle(new Vector3(255, 270, -300), new Vector3(470, 170, -300), 170, 115));
    }

    // Fab, who plays with Raph too: the keyboards -- black, the keys along both sides, a stack of speakers on the back and a mirror ball
    // over them.
    public static Body BuildFab(Body fab, Body racer)
    {
        var m = new CarMesh(Fab.Name);
        Roots(m);
        m.Light = SoftLight;
        var hull = new Hull(m, 300, 12, 6, 0.5f, (600, 240, 112, 110), (540, 285, 140, 128), (0, 290, 145, 130), (-480, 285, 140, 128), (-545, 240, 112, 110));
        hull.Skin(m, (_, _) => Black);
        hull.Nose(m, 610, Black);
        hull.Tail(m, -556, Black);
        var stack = hull.OnTop(0, -360, 150);
        m.Box(2, stack, new Vector3(420, 300, 220), Black + 1);
        m.Light = SoftShare;
        foreach (var side in Sides)
        {
            // the keys: a white strip along the shoulder, the black ones across it
            var x = side * 235;
            m.Plate(2, White, hull.OnTop(x - 45, 520, 8), hull.OnTop(x + 45, 520, 5), hull.OnTop(x + 45, -120, 5), hull.OnTop(x - 45, -120, 8));
        }
        m.Light = 1;
        foreach (var side in Sides)
        {
            for (var i = 0; i < 12; i++) { if (i % 4 == 3) continue; var z = 490 - i * 50f; m.Rod(2, hull.OnTop(side * 205, z, 12), hull.OnTop(side * 255, z, 10), Dark); }
            foreach (var up in new[] { -70f, 70 })
            {
                m.Ball(2, stack + new Vector3(side * 105, up, -112), 58, Steel + 3); m.Ball(2, stack + new Vector3(side * 105, up, -118), 22, Dark);
            }
            m.Ball(2, new(side * 170, hull.Cy + 35, 600), 38, side > 0 ? RedFlat + 2 : BlueFlat);
        }
        {
            var ball = stack + new Vector3(0, 330, 0);
            m.Rod(2, stack + new Vector3(0, 150, 0), ball, WhiteFlat);
            m.Ball(2, ball, 82, WhiteFlat - 1);
            foreach (var (dx, dy, c) in new[] { (-35f, 30f, Cyan), (30f, 40f, Yellow), (10f, -35f, RedFlat + 8), (-40f, -25f, WhiteFlat), (45f, -5f, BlueFlat) }) m.Ball(2, ball + new Vector3(dx, dy, 20), 20, c);
        }
        return Finish(m, hull, fab, racer,
            new Driver(new Vector3(0, 420, 0), 0.8f, (3, new[] { 4, 5 }), (6, new[] { 7, 8 }), Reach: 200, GripHeight: 80, GripX: 120),
            new Cabin(120, 215, 185, Rim: Steel + 2),
            new WheelLook(Steel, 24, Black, SoftLight, Steel + 2, 1, Cyan),
            new Axle(new Vector3(250, 262, 400), new Vector3(440, 150, 400), 150, 100), new Axle(new Vector3(250, 262, -350), new Vector3(455, 170, -350), 170, 115));
    }

    // ---------------------------------------------------------------------------------------------------------------- Desert Island

    // Joe the Elf, whose magic keeps going wrong: the green shell it shut him in, on wheels, the sparks of it still flying.
    public static Body BuildJoe(Body joe, Body racer)
    {
        var m = new CarMesh(Joe.Name);
        Roots(m);
        var hull = Lens(m, 270, 10, 450, 250, 95, new[] { 425f, 350, 230, 90, -90, -230, -350, -425 });
        hull.Skin(m, (band, k) => k is 2 or 7 ? Cream : k is 3 or 4 or 5 or 6 ? Green - 2 : (band + k) % 2 == 0 ? Green : Green + 2);
        hull.Nose(m, 452, Cream);
        hull.Tail(m, -452, Cream);
        foreach (var (x, y, z, r, c) in new[] { (-260f, 560f, 180f, 24, Yellow), (250, 620, -80, 18, WhiteFlat), (-200, 700, -250, 20, Yellow), (180, 760, 150, 14, Cyan), (-60, 830, -330, 16, Yellow), (300, 480, -300, 20, WhiteFlat) })
        {
            m.Ball(2, new(x, y, z), r, c);
            m.Rod(2, new(x - r * 2, y, z), new(x + r * 2, y, z), c); m.Rod(2, new(x, y - r * 2, z), new(x, y + r * 2, z), c);
        }
        foreach (var side in Sides) m.Ball(2, new(side * 170, hull.Cy + 50, 410), 38, Yellow);
        return Finish(m, hull, joe, racer,
            new Driver(new Vector3(0, 215, 0), 1.15f, (6, Array.Empty<int>()), (4, Array.Empty<int>()), Reach: 170, GripHeight: 120, GripX: 70),
            new Cabin(0, 170, 160, Rim: Cream, Wheel: Gold, WheelLine: GoldFlat),
            new WheelLook(Green, 24, Grey, 1, Cream, 1, GoldFlat),
            new Axle(new Vector3(200, 250, 270), new Vector3(430, 140, 270), 140, 95), new Axle(new Vector3(200, 250, -270), new Vector3(430, 140, -270), 140, 95));
    }

    // Ker'aooc the healer: his cauldron -- black iron, the green cure bubbling in it, the ladle standing in it, his flasks round the rim.
    public static Body BuildKeraooc(Body healer, Body racer)
    {
        var m = new CarMesh(Keraooc.Name);
        Roots(m);
        m.Light = SoftLight;
        var hull = Lens(m, 330, 10, 450, 70, 215, new[] { 425f, 350, 230, 90, -90, -230, -350, -425 });
        hull.Skin(m, (_, k) => k is 0 or 9 or 1 or 8 ? GreenSoft : Black);
        hull.Nose(m, 452, Black);
        hull.Tail(m, -452, Black);
        m.Light = 1;
        foreach (var (x, z, up, r) in new[] { (-200f, 250f, 40f, 46), (150, 300, 70, 34), (240, 80, 30, 40), (-260, -60, 80, 28), (120, -280, 45, 50), (-120, -320, 95, 30), (0, -180, 140, 22), (-60, 330, 130, 18) })
            m.Ball(2, hull.OnTop(x, z, up), r, up > 90 ? WhiteFlat : GreenFlat + 2);
        m.Strut(2, hull.OnTop(230, -220, -10), hull.OnTop(330, -300, 420), 28, Wood);
        m.Ball(2, hull.OnTop(335, -305, 440), 30, Wood + 8);
        for (var i = 0; i < 6; i++)
        {
            var a = (i + 0.5f) * MathF.Tau / 6;
            var at = new Vector3(455 * MathF.Sin(a), hull.Cy + 30, 455 * MathF.Cos(a));
            m.Rod(2, at, at - new Vector3(0, 90, 0), CreamFlat);
            m.Ball(2, at - new Vector3(0, 125, 0), 38, i % 3 == 0 ? RedFlat + 8 : i % 3 == 1 ? Cyan : Amber);
        }
        return Finish(m, hull, healer, racer,
            new Driver(new Vector3(0, 420, 0), 0.75f, (3, new[] { 4, 5 }), (6, new[] { 7, 8 }), Reach: 200, GripHeight: 80, GripX: 120),
            new Cabin(0, 205, 190, Rim: Steel - 2),
            new WheelLook(Steel - 2, 26, Black, SoftLight, Steel, 1, GreenFlat),
            new Axle(new Vector3(200, 230, 280), new Vector3(450, 140, 280), 140, 95), new Axle(new Vector3(200, 230, -280), new Vector3(450, 140, -280), 140, 95));
    }

    // Tabata, who teaches at the School of Magic: a spell book lying open -- its cream pages ruled with writing, its red cover under
    // them, the ribbon out at the back, a quill in its inkwell, a few letters floating off the page.
    public static Body BuildTabata(Body tabata, Body racer)
    {
        var m = new CarMesh(Tabata.Name);
        Roots(m);
        var hull = new Hull(m, 270, 12, 7, 0.5f, (590, 300, 85, 95), (560, 315, 95, 100), (0, 315, 100, 100), (-540, 315, 95, 100), (-570, 300, 85, 95));
        hull.Skin(m, (_, k) => k is 11 or 0 or 10 or 1 or 9 ? Cream : Red);
        hull.Nose(m, 594, Cream - 3);
        hull.Tail(m, -574, Cream - 3);
        foreach (var side in Sides)
        {
            foreach (var z in new[] { 480f, 400, 320, -250, -330, -410, -490 })
                m.Rod(2, hull.OnTop(side * 60, z, 4), hull.OnTop(side * (z is 320 or -490 ? 170 : 260), z, 4), Dark + 2);
            m.Ball(2, new(side * 190, hull.Cy + 20, 590), 36, Yellow);
        }
        for (var i = 0; i + 1 < hull.Rings.Count; i++) m.Line(m.P(2, hull.OnTop(0, 590 - i * 290, 6)), m.P(2, hull.OnTop(0, 590 - (i + 1) * 290, 6)), RedFlat);
        m.Plate(2, Red, hull.OnTop(-25, -570, 4), hull.OnTop(25, -570, 4), new Vector3(30, hull.Cy - 40, -700), new Vector3(-30, hull.Cy - 40, -700));
        m.Plate(2, Red, new Vector3(30, hull.Cy - 40, -700), new Vector3(0, hull.Cy - 5, -690), new Vector3(-30, hull.Cy - 40, -700));
        {
            var pot = hull.OnTop(210, -130, -4);
            Can(m, pot, 52, 70, BlueFlat - 9, Dark);
            m.Rod(2, pot + new Vector3(0, 60, 0), pot + new Vector3(40, 330, -90), WhiteFlat);
            m.Plate(2, Cream, pot + new Vector3(12, 140, -30), pot + new Vector3(90, 250, -60), pot + new Vector3(40, 330, -90), pot + new Vector3(-10, 250, -75));
        }
        foreach (var (x, y, z, r) in new[] { (-220f, 520f, 300f, 22), (-150, 640, 150, 17), (-260, 720, -40, 13), (180, 600, 380, 16) }) m.Ball(2, new(x, y, z), r, Yellow);
        return Finish(m, hull, tabata, racer,
            new Driver(new Vector3(0, 790, 31), 0.85f, (14, new[] { 15, 16 }), (11, new[] { 12, 13 })),
            new Cabin(60, 200, 175, Up: 36, Rim: Red, Wheel: Gold, WheelLine: GoldFlat),
            new WheelLook(Gold, 22, Grey, 1, Red, 1, GoldFlat),
            new Axle(new Vector3(255, 235, 400), new Vector3(450, 140, 400), 140, 95), new Axle(new Vector3(255, 235, -380), new Vector3(450, 140, -380), 140, 95));
    }

    // Moya the turtle, who carries Twinsen over the water: herself -- the shell in its plates of two greens, a flipper at each corner,
    // her tail, and her head up on its neck in front.
    public static Body BuildMoya(Body moya, Body racer)
    {
        var m = new CarMesh(Moya.Name);
        Roots(m);
        var hull = Lens(m, 270, 10, 470, 260, 85, new[] { 445f, 370, 245, 100, -100, -245, -370, -445 });
        hull.Skin(m, (band, k) => k is 2 or 7 ? Cream : k is 3 or 4 or 5 or 6 ? Cream - 3 : (band + k) % 2 == 0 ? Green : Green + 3);
        hull.Nose(m, 472, Cream);
        hull.Tail(m, -472, Cream);
        m.Plate(2, Green + 1, new(-50, hull.Cy - 10, -450), new(50, hull.Cy - 10, -450), new(0, hull.Cy - 50, -640));
        foreach (var side in Sides)
            foreach (var (z, back) in new[] { (300f, 1f), (-300f, -1f) })
            {
                var root = new Vector3(side * hull.Side(hull.Cy - 20, z), hull.Cy - 20, z);
                m.Plate(2, Green + 1, root + new Vector3(0, 0, 70), root + new Vector3(side * 270, -90, 40 + back * 130), root + new Vector3(side * 300, -120, back * 20), root + new Vector3(0, -10, -70));
            }
        Wheels(m, new WheelLook(Green + 1, 24, Grey, 1, Cream, 1, GoldFlat),
            new Axle(new Vector3(200, 240, 270), new Vector3(430, 135, 270), 135, 90), new Axle(new Vector3(200, 240, -270), new Vector3(430, 135, -270), 135, 90));
        // her neck and head (bone 13: what the racer's driver does, she nods)
        m.Light = 1;
        var seat = new Vector3(0, hull.Cy + 40, 400);
        m.Pivot(13, m.P(2, seat));
        {
            var foot = m.Loop(13, seat, new Vector3(0, 70, 30), new Vector3(75, 0, 0), 8);
            var neck = m.Loop(13, seat + new Vector3(0, 200, 170), new Vector3(0, 55, -35), new Vector3(58, 0, 0), 8);
            var jaw = m.Loop(13, seat + new Vector3(0, 330, 270), new Vector3(0, 80, -20), new Vector3(85, 0, 0), 8);
            var snout = m.Loop(13, seat + new Vector3(0, 320, 400), new Vector3(0, 50, 0), new Vector3(52, 0, 0), 8);
            m.Skin(foot, neck, _ => Green + 1, seat + new Vector3(0, 100, 85));
            m.Skin(neck, jaw, _ => Green + 1, seat + new Vector3(0, 265, 220));
            m.Skin(jaw, snout, _ => Green + 1, seat + new Vector3(0, 325, 335));
            m.Cap(snout, m.P(13, seat + new Vector3(0, 315, 440)), Green + 1, seat + new Vector3(0, 320, 380));
            foreach (var side in Sides) { m.Ball(13, seat + new Vector3(side * 70, 375, 300), 30, WhiteFlat); m.Ball(13, seat + new Vector3(side * 80, 378, 312), 13, Dark); }
            m.Rod(13, seat + new Vector3(-45, 295, 425), seat + new Vector3(45, 295, 425), Dark + 2);
            Arms(m, neck[2], neck[6]);
        }
        _ = moya;
        return m.ToBody(racer.Header);
    }

    // The Citadel Island thief, who makes off with the pharmacy customer's umbrella: a getaway car in a burglar's black and white hoops,
    // a mask across its nose -- and the umbrella, open, planted in the back of it and leaning in the wind: its gores in the two creams of
    // the game's own, its grey shaft and crook, its ribs underneath. His sack of loot beside it, coins spilling.
    public static Body BuildThief(Body thief, Body racer)
    {
        var m = new CarMesh(Thief.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 300, 12, 5, 0.5f,
            (600, 215, 110, 108), (540, 268, 140, 125), (400, 280, 150, 130), (250, 282, 152, 130), (100, 282, 152, 130), (-50, 282, 152, 130), (-200, 282, 152, 130), (-350, 280, 150, 130), (-500, 268, 140, 125),
            (-565, 215, 110, 108));
        hull.Skin(m, (band, _) => band % 2 == 0 ? Black : White);
        hull.Nose(m, 612, Black);
        hull.Tail(m, -577, Black);
        m.Light = 1;
        foreach (var side in Sides) m.Ball(2, new(side * 120, hull.Cy + 25, 606), 44, Yellow);      // the eyes in the mask

        // the umbrella
        {
            var foot = hull.OnTop(0, -330, -6); var apex = foot + new Vector3(0, 640, -150);
            var axis = Vector3.Normalize(apex - foot); var u = Vector3.UnitX; var v = Vector3.Normalize(Vector3.Cross(axis, u));
            const int Gores = 8;
            var shoulder = m.Loop(2, apex - axis * 70, u * 215, v * 215, Gores);
            var rim = m.Loop(2, apex - axis * 230, u * 390, v * 390, Gores);
            var top = m.P(2, apex);
            for (var k = 0; k < Gores; k++)
            {
                var colour = k % 2 == 0 ? 32 : 35;
                m.Both(new[] { shoulder[k], shoulder[(k + 1) % Gores], top }, colour);
                m.Both(new[] { rim[k], rim[(k + 1) % Gores], shoulder[(k + 1) % Gores], shoulder[k] }, colour);
            }
            // the shaft and the ferrule, the runner and the ribs from it to the rim, the crook lying on the deck
            m.Strut(2, foot, apex - axis * 10, 26, Stone - 2);
            m.Strut(2, apex, apex + axis * 70, 16, Stone);
            var runner = m.P(2, apex - axis * 300);
            for (var k = 0; k < Gores; k++) m.Line(runner, rim[k], Dark + 3);
            var bend = foot + new Vector3(0, 12, 110);
            m.Strut(2, foot + new Vector3(0, 12, 0), bend, 30, Stone - 2);
            m.Strut(2, bend, bend + new Vector3(90, 0, 70), 30, Stone - 2);
            m.Strut(2, bend + new Vector3(90, 0, 70), bend + new Vector3(160, 0, 10), 30, Stone - 2);
        }
        // the sack, its neck tied, and what is falling out of it
        {
            var sack = hull.OnTop(-150, -140, 85);
            m.Ball(2, sack, 112, 40); m.Ball(2, sack + new Vector3(10, 95, 5), 50, 40); m.Ball(2, sack + new Vector3(16, 150, 8), 30, 42);
            m.Ball(2, sack + new Vector3(8, 118, 40), 16, RedFlat + 2);
            foreach (var (x, z, r) in new[] { (-40f, 40f, 26), (-250f, -30f, 22), (-90f, -300f, 24), (-230f, -260f, 20), (20f, -40f, 18) }) m.Ball(2, hull.OnTop(x, z, 8), r, GoldFlat);
        }
        return Finish(m, hull, thief, racer, Rabbibunny(),
            new Cabin(140, 215, 185, Rim: Steel - 2, Screen: 420, ScreenHalf: 185),
            new WheelLook(Steel, 24, Black, SoftLight, White, SoftShare, Yellow),
            new Axle(new Vector3(250, 262, 430), new Vector3(440, 155, 430), 155, 105), new Axle(new Vector3(255, 262, -360), new Vector3(465, 175, -360), 175, 125));
    }
}
