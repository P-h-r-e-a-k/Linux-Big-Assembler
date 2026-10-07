using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// HAL, the moon base's computer (scene 23, by Baldino's cell; the user's, 2026-10-04): when it is broken, the base's mechanics -- its grey
// Franco guards with their tools -- cry out "HAL!!" (TEXT.HQR file 6, 546; "ZX81!!" without the translator, 545). Its cabinet and stand
// are the room's bricks and its screen a sprite (228 and 229 working, 230 cracked), so there is no body of it to use: this is made after
// the picture, and it drives itself.
//
//   the stand    the light lilac-grey base it stands on as the car's hull: rivets down its sides, the round red grille at the front on the
//                left and the dark pipe with its teal horn on the right
//   the cabinet  dark slate, its top edges rounded, the round green screen in its bezel looking ahead -- and in the screen's middle a red
//                eye, the other HAL's (bone 13, where a driver sits: the racer's animations turn it, and a ball turns in place)
//   the bottles  one at each front corner, dark, their coils glowing cyan, white caps; the red pipes arched over the top at the back, the
//                gauge on the right with its red cable down to the stand, the red knobs under the screen
internal static partial class RaceTrackCharacterCars
{
    public static readonly Car Hal = new("HAL", "HAL himself, the moon base's computer its mechanics worship", 98, 63, BuildHal, Moon);

    private static Car[] HalCars => new[] { Hal };

    // the cabinet's slate, the screen's teal-green and the stand's lilac grey (lit, each with the share of the light that keeps it in its
    // ramp), the coils' cyan (as it is)
    private const int Slate = 178, Bezel = 182, ScreenGreen = 150, Lilac = 218, Coil = 173;

    public static Body BuildHal(Body mechanic, Body racer)
    {
        var m = new CarMesh(Hal.Name);
        Roots(m);

        // the stand
        m.Light = 0.5f;
        var stand = new Hull(m, 290, 12, 5, 0.5f, (520, 290, 55, 70), (490, 330, 82, 95), (-450, 330, 82, 95), (-480, 290, 55, 70));
        stand.Skin(m, (band, k) => k is 5 or 6 ? Lilac - 3 : band == 1 ? Lilac : Lilac - 1);
        stand.Nose(m, 525, Lilac - 1);
        stand.Tail(m, -485, Lilac - 1);
        var deck = stand.Top(0, 0);
        m.Light = 1;
        foreach (var side in Sides)
            for (var i = 0; i < 6; i++) m.Ball(2, new(side * 336, 300, 380 - i * 150), 13, Dark + 1);
        // the round red grille, front left
        {
            var centre = new Vector3(-165, 292, 527);
            var rim = m.Loop(2, centre, new Vector3(58, 0, 0), new Vector3(0, 58, 0), 8);
            var hub = m.P(2, centre + new Vector3(0, 0, 8));
            m.Light = 0.5f;
            for (var k = 0; k < 8; k++) m.Out(new[] { rim[k], rim[(k + 1) % 8], hub }, Red, centre - new Vector3(0, 0, 40));
            m.Light = 1;
            m.Line(rim[0], rim[4], Dark); m.Line(rim[2], rim[6], Dark);
        }
        // the dark pipe out of the front on the right, and its teal horn
        {
            m.Light = 0.3f;
            var back = m.Loop(2, new Vector3(190, 262, 475), new Vector3(52, 0, 0), new Vector3(0, 52, 0), 6);
            var front = m.Loop(2, new Vector3(190, 262, 575), new Vector3(52, 0, 0), new Vector3(0, 52, 0), 6);
            m.Skin(back, front, _ => Slate - 2, new Vector3(190, 262, 525));
            m.Light = 0.6f;
            var mouth = m.Loop(2, new Vector3(190, 250, 670), new Vector3(88, 0, 0), new Vector3(0, 88, 0), 6);
            m.Skin(front, mouth, _ => ScreenGreen - 2, new Vector3(190, 262, 535));
            m.Light = 1;
            m.Cap(mouth, m.P(2, new Vector3(190, 252, 625)), Dark + 1, new Vector3(190, 252, 535), unlit: true);
        }

        // the cabinet: its cross-section, the top edges rounded, from its back to its front
        const float Back = -290, Front = 200, Top = 905;
        m.Light = 0.45f;
        var section = new (float X, float Y)[] { (-250, deck - 10), (250, deck - 10), (250, 790), (222, 862), (142, Top), (-142, Top), (-222, 862), (-250, 790) };
        var fore = section.Select(s => m.P(2, new(s.X, s.Y, Front))).ToArray();
        var aft = section.Select(s => m.P(2, new(s.X, s.Y, Back))).ToArray();
        var middle = new Vector3(0, (deck + Top) / 2, (Front + Back) / 2);
        m.Skin(fore, aft, _ => Slate, middle);
        m.Cap(fore, m.P(2, new(0, 640, Front)), Slate, middle);
        m.Cap(aft, m.P(2, new(0, 640, Back)), Slate, middle);

        // the screen: its bezel, and the screen bulging out of it, a grid over it
        var eye = new Vector3(0, 625, Front + 44);
        {
            m.Light = 0.5f;
            var outer = m.Loop(2, new Vector3(0, 625, Front + 6), new Vector3(205, 0, 0), new Vector3(0, 205, 0), 12);
            var inner = m.Loop(2, new Vector3(0, 625, Front + 16), new Vector3(172, 0, 0), new Vector3(0, 172, 0), 12);
            m.Skin(outer, inner, _ => Bezel, new Vector3(0, 625, Front - 60));
            m.Light = 0.6f;
            var dome = m.Loop(2, new Vector3(0, 625, Front + 34), new Vector3(112, 0, 0), new Vector3(0, 112, 0), 12);
            m.Skin(inner, dome, _ => ScreenGreen, new Vector3(0, 625, Front - 120));
            m.Cap(dome, m.P(2, new Vector3(0, 625, Front + 42)), ScreenGreen, new Vector3(0, 625, Front - 120));
            m.Light = 1;
            foreach (var at in new[] { -80f, 80f })
            {
                m.Rod(2, new(-140, 625 + at, Front + 40), new(140, 625 + at, Front + 40), ScreenGreen - 4);
                m.Rod(2, new(at, 485, Front + 40), new(at, 765, Front + 40), ScreenGreen - 4);
            }
        }
        // the red knobs under it
        foreach (var side in Sides) m.Ball(2, new(side * 135, deck + 55, Front + 10), 20, RedFlat);

        // the bottles at the front corners: a dark foot, the coils (glowing bands between dark ones), a neck, a white cap
        foreach (var side in Sides)
        {
            var x = side * 292; const float Z = Front - 20, R = 50;
            m.Light = 0.3f;
            var rings = new[] { deck - 5, deck + 95, deck + 140, deck + 185, deck + 230, deck + 275, deck + 320, deck + 365 }
                .Select(y => Level(m, new Vector3(x, y, Z), R, 6)).ToList();
            var neck = Level(m, new Vector3(x, deck + 430, Z), R * 0.6f, 6);
            for (var i = 0; i + 1 < rings.Count; i++)
            {
                var band = i;
                m.Light = 1;
                m.Skin(rings[i], rings[i + 1], _ => band % 2 == 1 ? Coil : Slate - 1, new Vector3(x, deck + 100 + 45 * i, Z));
            }
            m.Skin(rings[^1], neck, _ => Slate - 1, new Vector3(x, deck + 380, Z));
            m.Ball(2, new(x, deck + 468, Z), 44, WhiteFlat);
        }

        // the red pipes arched over the top at the back, and the grey one out behind
        m.Light = 0.5f;
        foreach (var side in Sides)
        {
            var x = side * 120;
            var a = new Vector3(x, Top - 8, -10); var b = new Vector3(x, Top + 95, -90); var c = new Vector3(x, Top + 95, -190); var d = new Vector3(x, Top - 8, -265);
            m.Strut(2, a, b, 30, Red); m.Strut(2, b, c, 30, Red); m.Strut(2, c, d, 30, Red);
        }
        m.Light = 0.4f;
        m.Strut(2, new(200, Top - 40, -240), new(200, Top - 40, Back - 130), 34, Steel);
        // the gauge on the right, its slot, its red cable down to the stand
        {
            var foot = new Vector3(286, 560, -170);
            m.Light = SoftShare;
            Can(m, foot, 34, 150, White, WhiteFlat);
            m.Light = 1;
            m.Rod(2, foot + new Vector3(30, 40, -10), foot + new Vector3(30, 120, -10), Dark);
            m.Light = 0.5f;
            m.Strut(2, foot + new Vector3(0, 5, 0), new(305, 470, -90), 16, Red);
            m.Strut(2, new(305, 470, -90), new(300, deck + 4, -20), 16, Red);
        }

        Wheels(m, new WheelLook(Slate - 1, 24, Black, SoftLight, Bezel, 0.5f, Coil),
            new Axle(new Vector3(230, 250, 330), new Vector3(405, 135, 330), 135, 90), new Axle(new Vector3(230, 250, -320), new Vector3(405, 135, -320), 135, 90));

        // HAL's eye: a black lens on the screen, its red light in front of it (bone 13)
        m.Light = 1;
        var lensRim = m.Loop(2, eye, new Vector3(46, 0, 0), new Vector3(0, 46, 0), 8);
        m.Cap(lensRim, m.P(2, eye + new Vector3(0, 0, 4)), Dark, eye - new Vector3(0, 0, 60), unlit: true);
        var pivot = m.P(2, eye + new Vector3(0, 0, 14));
        m.Pivot(13, pivot);
        var light = m.Ball(13, eye + new Vector3(0, 0, 14), 26, RedFlat);
        var glint = m.Ball(13, eye + new Vector3(8, 8, 30), 6, Yellow);
        Arms(m, light, glint);
        _ = mechanic;
        return m.ToBody(racer.Header);
    }
}
