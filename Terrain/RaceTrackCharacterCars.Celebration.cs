using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// Celebration Island's lava lake drivers (the user's, 2026-10-03): a kangaroo, the souvenir seller Twinsen has to beat for what he knows
// (the Franco who came back from Island CX, his head still bandaged, who sells little Dark Monks by the temple) and a policeman (the
// island's Franco guards, green helmet and spear).
internal static partial class RaceTrackCharacterCars
{
    public static readonly Car Kangaroo = new("The kangaroo's safari car", "a kangaroo, a tourist with a camera", 308, 60, BuildKangaroo, Celebration);
    public static readonly Car Seller = new("The souvenir cart", "the souvenir seller, back from Island CX", 307, 61, BuildSeller, Celebration);
    public static readonly Car Guard = new("The guard's car", "a policeman of Celebration Island", 80, 62, BuildGuard, Celebration);

    private static Car[] CelebrationCars => new[] { Kangaroo, Seller, Guard };

    // The kangaroo, a tourist: a khaki safari car with a roll bar, a big camera on the bonnet, a spring under its tail for the hops, and the
    // kangaroo's own long tail hanging off the back.
    public static Body BuildKangaroo(Body kangaroo, Body racer)
    {
        var m = new CarMesh(Kangaroo.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 300, 12, 4, 0.5f, (610, 200, 110, 105), (540, 255, 140, 122), (300, 270, 152, 130), (-320, 270, 156, 130), (-500, 258, 140, 124), (-570, 210, 112, 110));
        hull.Skin(m, (band, k) => k is 11 or 0 or 10 ? Wood + 2 : band % 3 == 1 ? Brown : Sand);
        hull.Nose(m, 622, Sand);
        hull.Tail(m, -582, Sand);
        m.Light = 1;
        // the roll bar over the seat
        foreach (var side in Sides) m.Strut(2, hull.OnTop(side * 170, -230), hull.OnTop(side * 170, -230, 330), 24, Dark + 3);
        m.Strut(2, hull.OnTop(-170, -230, 330), hull.OnTop(170, -230, 330), 24, Dark + 3);
        // the camera on the bonnet: a box, a lens, a flash
        {
            var at = hull.OnTop(0, 380, 60);
            m.Box(2, at, new Vector3(150, 100, 90), Black, Dark + 2);
            m.Ball(2, at + new Vector3(0, 0, 70), 44, Steel + 4); m.Ball(2, at + new Vector3(0, 0, 92), 26, Cyan);
            m.Ball(2, at + new Vector3(55, 62, 0), 18, WhiteFlat);
        }
        // the spring under the tail, and the tail itself curving down off the back
        {
            var spring = new Vector3(0, hull.Cy - 120, -600);
            for (var k = 0; k < 4; k++) m.Ball(2, spring + new Vector3(0, -35 * k, 0), 46, Steel + 2);
            var tail = new[] { new Vector3(0, hull.Cy + 60, -590), new Vector3(0, hull.Cy + 20, -720), new Vector3(0, hull.Cy - 40, -840), new Vector3(0, hull.Cy - 90, -940) };
            for (var k = 0; k + 1 < tail.Length; k++) m.Strut(2, tail[k], tail[k + 1], 46 - 10 * k, Brown + 2);
            m.Ball(2, tail[^1], 26, Brown + 1);
        }
        foreach (var side in Sides) m.Ball(2, new(side * 140, hull.Cy + 40, 615), 42, Yellow);
        return Finish(m, hull, kangaroo, racer,
            new Driver(new Vector3(0, 420, 0), 0.7f, (20, new[] { 21, 22 }), (17, new[] { 18, 19 }), new[] { 7, 23, 25 }),
            new Cabin(-40, 215, 185, Rim: Wood, Screen: 245),
            new WheelLook(Steel, 24, Grey, 1, Sand, SoftShare, Brown),
            new Axle(new Vector3(250, 262, 400), new Vector3(435, 155, 400), 155, 105), new Axle(new Vector3(255, 262, -370), new Vector3(465, 175, -370), 175, 125));
    }

    // The souvenir seller: a stall on wheels -- wooden sides, a red and white striped awning on four posts (the red and white of his bandages),
    // and on its counter the little Dark Monks he sells, with a price tag.
    public static Body BuildSeller(Body seller, Body racer)
    {
        var m = new CarMesh(Seller.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 300, 12, 6, 0.5f, (600, 230, 130, 120), (560, 268, 150, 132), (300, 280, 158, 135), (-300, 280, 158, 135), (-540, 270, 150, 130), (-585, 230, 125, 118));
        hull.Skin(m, (band, k) => k is 11 or 0 or 10 ? Wood + 3 : band % 2 == 0 ? Wood : Wood + 2);
        hull.Nose(m, 612, Wood + 2);
        hull.Tail(m, -597, Wood + 2);
        m.Light = 1;
        // the awning over the back half: four posts, a striped roof in two slopes
        {
            const float Front = -150, Back = -540, Up = 430;
            foreach (var side in Sides)
                foreach (var z in new[] { Front, Back }) m.Strut(2, hull.OnTop(side * 190, z), hull.OnTop(side * 190, z, Up), 18, Wood);
            // (the stripes share their points: the ridge and the two eaves, a point at each stripe's edge)
            const int Stripes = 6;
            var ridge = new int[Stripes + 1]; var left = new int[Stripes + 1]; var right = new int[Stripes + 1];
            for (var k = 0; k <= Stripes; k++)
            {
                var z = Front - (Front - Back) * k / Stripes;
                ridge[k] = m.P(2, hull.OnTop(0, z, Up + 90)); left[k] = m.P(2, hull.OnTop(-230, z, Up - 10)); right[k] = m.P(2, hull.OnTop(230, z, Up - 10));
            }
            for (var k = 0; k < Stripes; k++)
            {
                var colour = k % 2 == 0 ? RedSoft : White;
                m.Both(new[] { ridge[k], ridge[k + 1], left[k + 1], left[k] }, colour);
                m.Both(new[] { ridge[k], ridge[k + 1], right[k + 1], right[k] }, colour);
            }
        }
        // the souvenirs on the counter under it: little Dark Monks (a dark cone body, a round head), and a price tag
        foreach (var (x, z) in new[] { (-100f, -280f), (100f, -280f), (0f, -430f) })
        {
            var foot = hull.OnTop(x, z, 4);
            m.Strut(2, foot, foot + new Vector3(0, 90, 0), 30, Dark + 2);
            m.Ball(2, foot + new Vector3(0, 110, 0), 24, Grey + 4);
        }
        {
            var tag = hull.OnTop(0, 520, 0);
            m.Strut(2, tag, tag + new Vector3(0, 160, 0), 10, Wood);
            m.Box(2, tag + new Vector3(0, 200, 0), new Vector3(150, 70, 10), WhiteFlat, RedFlat + 2);
        }
        foreach (var side in Sides) m.Ball(2, new(side * 140, hull.Cy + 40, 605), 40, Amber);
        return Finish(m, hull, seller, racer,
            // (his case and its straps left out: the car can't carry more bones than the engine's 30)
            new Driver(new Vector3(0, 520, 0), 0.78f, (6, new[] { 7, 8 }), (9, new[] { 10, 11 }), new[] { 20, 21, 26, 27, 28 }),
            new Cabin(160, 210, 180, Rim: Wood + 3, Screen: 400),
            new WheelLook(Wood, 24, Grey, 1, Wood + 2, SoftShare, RedFlat + 2),
            new Axle(new Vector3(250, 262, 410), new Vector3(440, 155, 410), 155, 105), new Axle(new Vector3(255, 262, -380), new Vector3(465, 175, -380), 175, 125));
    }

    // A policeman of Celebration Island, one of its Franco guards: a car in his helmet's green with a white band, a red light on its roof
    // bar and his spear along its side, its red blade forward.
    public static Body BuildGuard(Body guard, Body racer)
    {
        var m = new CarMesh(Guard.Name);
        Roots(m);
        m.Light = SoftShare;
        var hull = new Hull(m, 300, 12, 4, 0.5f, (620, 205, 105, 105), (550, 258, 138, 122), (280, 272, 152, 130), (-330, 272, 158, 130), (-500, 262, 140, 124), (-580, 215, 112, 110));
        hull.Skin(m, (_, k) => k is 11 or 0 or 10 ? White : k is 3 or 4 or 5 or 6 or 7 ? GreenSoft : Green);
        hull.Nose(m, 632, Green);
        hull.Tail(m, -592, Green);
        m.Light = 1;
        {
            int a = m.P(2, hull.OnTop(-235, -280)), b = m.P(2, hull.OnTop(-235, -280, 470)), c = m.P(2, hull.OnTop(235, -280, 470)), d = m.P(2, hull.OnTop(235, -280));
            m.Line(a, b, WhiteFlat); m.Line(c, d, WhiteFlat);
            m.Box(2, hull.OnTop(0, -280, 480), new Vector3(480, 36, 60), Steel + 2);
            m.Ball(2, hull.OnTop(0, -280, 525), 52, RedFlat + 2);
        }
        // the spear, along the left side: shaft and blade
        {
            var tail = new Vector3(-175, hull.Cy + 60, -560); var head = new Vector3(-175, hull.Cy + 120, 620);
            m.Strut(2, tail, head, 16, Wood);
            m.Flat(2, RedFlat + 2, head + new Vector3(0, 30, 0), head + new Vector3(0, -30, 0), head + new Vector3(0, 0, 150));
        }
        foreach (var side in Sides)
        {
            m.Ball(2, new(side * 150, hull.Cy + 40, 615), 46, Yellow);
            m.Rod(2, new(side * 190, hull.Cy - 50, 600), new(side * 190, hull.Cy - 50, 690), Dark + 3);
        }
        m.Rod(2, new(-190, hull.Cy - 50, 690), new(190, hull.Cy - 50, 690), Dark + 3);
        return Finish(m, hull, guard, racer,
            new Driver(new Vector3(0, 520, 0), 0.78f, (6, new[] { 7, 8 }), (9, new[] { 10, 11 }), new[] { 28 }),
            new Cabin(-40, 215, 185, Rim: Green + 2, Screen: 245),
            new WheelLook(Steel, 24, Grey, 1, White, SoftShare, GreenFlat),
            new Axle(new Vector3(250, 266, 400), new Vector3(435, 155, 400), 155, 105), new Axle(new Vector3(255, 266, -370), new Vector3(465, 175, -370), 175, 125));
    }
}
