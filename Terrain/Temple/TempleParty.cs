using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain.Temple;

// The Temple of Bú's secret Halloween party (the user, 2026-10-19, with a picture of the room: "let's create a secret halloween party area,
// complete with traditional halloween characters and accompanyments here that the user can find if they get out of the car and explore").
//
// Where: LBA1's scene 41's great hall -- the temple's north-east building, a hall 37 x 21 of LBA1's cells (island cells 548..658 x
// 259..321) on floor layer 12 (PartyFloorY), walls a layer and a half high dividing it, a stair down out of its south side, a raised dais at
// its east end with a stair; far off the lap (its nearest road is the long corridor 150 cells south, 3,000 lower) and out of sight of it.
// LBA1 reaches it from its lower east hall; the race's way there is on foot (RACEMOD.CPP on_foot=: Space gets Twinsen out of his stopped
// car): from the long corridor north up the side passage that opens off it (LBA1 scene 41's west wing, floor 8 -- a jack-o'-lantern at its
// mouth the only hint from the road), up its stair onto the gallery at 12, and through a doorway the race cuts in the gallery's north wall
// (PartyCuts) over a short bridge it lays across the dark (PartyWalls) into the hall at its south side -- open to the dark, as LBA1 draws
// no wall on the side its camera looks from: boxes nothing draws along it keep a guest from walking off.
//
// What: the hall's decor (PlaceParty, drawn decor of the island, solid where a guest would walk into it) -- a banquet of two tables with a
// punch bowl of glowing green punch (an eye floating in it), a three-tier cake with candles, cookies, candy corn, goblets of red wine and
// candelabras; the witch stirring her cauldron of bubbling green brew over a fire, her black cat by her; a glowing dance floor; the
// vampire at the door greeting his guests, Frankenstein's monster dancing stiffly, the mummy at the banquet, a howling werewolf, two
// zombies after the punch and out of the graves, the grim reaper with his scythe among the gravestones; an open coffin standing against
// the wall; black cats, jack-o'-lanterns, a pile of pumpkins, candles, balloons, orange, purple and green bunting across the hall, cobwebs
// and spiders -- and its guests the race-track mode draws and moves (PartyGuests, RACEMOD.CPP spook= flags 32: only while Twinsen is
// about the party): the game's own rabbibunny skeletons (the risers' lighter body) dancing a conga round the dance floor and two more
// dancing on the spot, the band on the dais -- the franco drummer and two guitarists (the old rabbibunny's, the desert sphero's), each
// playing its own animation -- the race's ghosts drifting over the dance floor, the flying rats circling high over the hall, a witch on
// her broomstick round it, a spinning disco ball, and bubbles rising out of the cauldron.
internal static partial class TempleHalloween
{
    // ---- where (TempleLayout's cells: x, layer, z; island cells = 224 + 3x, 256 + 3z)
    public const int PartyLayer = 12;
    // the hall's floor (its cells' top: TempleFrame.BaseY + (12 - minY + 1) * 768, minY -8), the dais's (layer 16), the side passage's (8)
    public const double PartyFloorY = 17152, PartyDaisY = 20224, PartyPassageY = 14080;
    // The scenes Twinsen walks through to it and in it (TempleTrack: scenes of their own after the route's, so no scene number moves), each
    // a cube of the island: the side passage (cubes 8-9 x 5-6), the gallery and the bridge (8-9 x 5), the hall (8-10 x 4).
    public static readonly (int Cx, int Cz)[] PartyCubes = { (8, 4), (9, 4), (10, 4), (8, 5), (9, 5), (8, 6), (9, 6) };
    // The doorway (layout x 115-116: island 569..574): through the gallery's north wall (z 28, layers 13-16, its lintel kept) and the hall's
    // south wall (z 21, layers 13-17).
    private const int DoorX0 = 115, DoorX1 = 116, GalleryWallZ = 28, HallWallZ = 21, BridgeZ0 = 22, BridgeZ1 = 27, GalleryZ = 30;
    public static IEnumerable<int[]> PartyCuts()
    {
        for (var x = DoorX0; x <= DoorX1; x++)
        {
            for (var y = PartyLayer + 1; y <= PartyLayer + 4; y++) yield return new[] { x, y, GalleryWallZ };
            for (var y = PartyLayer + 1; y <= PartyLayer + 5; y++) yield return new[] { x, y, HallWallZ };
        }
    }
    // The bridge (RaceTrackPlan.TempleWalls' rows: x, z, from layer, to layer, the column whose bricks they take): its floor at 12 the
    // gallery's floor's bricks, its parapets either side two layers high the gallery's north wall's.
    public static IEnumerable<int[]> PartyWalls()
    {
        for (var z = BridgeZ0; z <= BridgeZ1; z++)
        {
            for (var x = DoorX0; x <= DoorX1; x++) yield return new[] { x, z, PartyLayer, PartyLayer, x, GalleryZ };
            yield return new[] { DoorX0 - 1, z, PartyLayer, PartyLayer + 2, DoorX0 - 1, GalleryWallZ };
            yield return new[] { DoorX1 + 1, z, PartyLayer, PartyLayer + 2, DoorX1 + 1, GalleryWallZ };
        }
    }

    // ---- colours (the temple's palette: TemplePalette; the fire colours 241..250 glow, the same in every island's palette)
    private static readonly Paint PGold = new(new[] { 100, 101, 103, 104, 105, 106, 107 });
    private static readonly Paint PRed = new(new[] { 65, 66, 67, 68, 69, 70, 71 });
    private static readonly Paint PPale = new(new[] { 57, 58, 59, 60, 61, 62 });
    private static readonly Paint PMauve = new(new[] { 211, 229, 214, 215, 216 });
    private static readonly Paint PZombie = new(new[] { 118, 119, 120, 121, 122, 123 });
    private static readonly Paint PBandage = new(new[] { 36, 38, 40, 41, 42, 43, 44 });
    private static readonly Paint PFur = new(new[] { 18, 20, 22, 25, 27, 29 });
    private static readonly Paint PFurLight = new(new[] { 25, 27, 29, 31, 33 });
    private static readonly Paint PBlack = new(new[] { 224, 225, 209, 226 });
    private static readonly Paint PCream = new(new[] { 43, 44, 45, 46, 47 });
    private static readonly Paint PPink = new(new[] { 75, 76, 77, 78, 79 });
    private static readonly Paint PChoc = new(new[] { 17, 18, 19, 20, 21, 22 });
    private static readonly Paint PWhite = new(new[] { 60, 61, 62, 63 });
    private static readonly Paint PSilver = new(new[] { 56, 57, 58, 59, 60, 61, 62 });
    private static readonly Paint PJacket = new(new[] { 112, 113, 114, 116, 117 });
    private static readonly Paint PDenim = new(new[] { 193, 194, 195, 196, 197, 198 });
    private static readonly Paint PRags = new(new[] { 96, 97, 98, 99, 100 });
    private static readonly Paint POrange = new(new[] { 86, 87, 88, 89, 90, 91 });
    private static readonly Paint PGreen = new(new[] { 134, 136, 137, 138, 139, 140 });
    private static readonly Paint PYellow = new(new[] { 104, 105, 106, 107, 108 });
    private static readonly Paint GOrange = new(new[] { 241 }, Glow: true), GAmber = new(new[] { 243 }, Glow: true), GYellow = new(new[] { 245 }, Glow: true),
        GViolet = new(new[] { 246 }, Glow: true), GGreen = new(new[] { 247 }, Glow: true), GCyan = new(new[] { 248 }, Glow: true),
        GRed = new(new[] { 249 }, Glow: true), GMagenta = new(new[] { 250 }, Glow: true);

    // ---- the models' parts
    // a part of another model into this one, turned `turn` radians about y (+z towards +x) and moved to `at`
    private static void PAdd(Model into, Model part, Vector3 at, float turn = 0, float scale = 1)
    {
        float c = MathF.Cos(turn), s = MathF.Sin(turn);
        var map = part.Points.Select(p => into.P(new Vector3((p.X * c + p.Z * s) * scale, p.Y * scale, (-p.X * s + p.Z * c) * scale) + at)).ToArray();
        foreach (var (ids, paint, both) in part.Faces) into.Faces.Add((ids.Select(i => map[i]).ToArray(), paint, both));
    }
    // an egg of a head round `c`, `r` across, `tall` times as high, its face +z
    private static void PHead(Model m, Vector3 c, float r, Paint skin, float tall = 1.15f, int sides = 10) =>
        m.Lathe(new (double, double)[] { (0, -r * tall), (r * 0.72, -r * 0.8 * tall), (r, -r * 0.1), (r * 0.95, r * 0.35 * tall), (r * 0.7, r * 0.8 * tall), (0, r * tall) },
            sides, _ => skin, false, false, at: c);
    // two eyes on a head's front: little triangles a touch out from it
    private static void PEyes(Model m, Vector3 c, float r, Paint eye, float up = 0.18f, float apart = 0.36f, float size = 0.2f)
    {
        foreach (var s in new[] { -1f, 1f })
        {
            var e = c + new Vector3(s * r * apart, r * up, r * 0.97f + 6);
            m.Facing(new[] { e + new Vector3(-r * size, -r * size * 0.3f, 0), e + new Vector3(r * size, -r * size * 0.3f, 0), e + new Vector3(0, r * size * 0.7f, 0) }, eye, e + new Vector3(0, 0, 2000));
        }
    }
    // a torso (or a skirt, a robe): rings round y from the bottom up, its middle at `at`, `flat` times as deep as wide
    private static void PTorso(Model m, Vector3 at, (double R, double Y)[] rings, Paint paint, float flat = 0.7f, int sides = 8, bool closed = true) =>
        m.Lathe(rings, sides, _ => paint, closed, closed, shape: a => 1 - (1 - flat) * Math.Abs(Math.Sin(a)), at: at);
    // a hand: a little box at the end of an arm
    private static void PHand(Model m, Vector3 at, float s, Paint skin) => m.Box(at - new Vector3(s, s, s), at + new Vector3(s, s, s), skin);
    // a leg from the hip down, its foot pointing +z
    private static void PLeg(Model m, Vector3 hip, Vector3 knee, Vector3 ankle, float t, Paint trousers, Paint shoe)
    {
        m.Bar(hip, knee, t, trousers);
        m.Bar(knee, ankle, t * 0.9f, trousers);
        m.Box(ankle + new Vector3(-t * 0.6f, -ankle.Y, -t * 0.6f), ankle + new Vector3(t * 0.6f, t * 0.4f, t * 1.4f), shoe);
    }
    // an arm: shoulder, elbow, hand
    private static void PArm(Model m, Vector3 shoulder, Vector3 elbow, Vector3 hand, float t, Paint sleeve, Paint skin)
    {
        m.Bar(shoulder, elbow, t, sleeve);
        m.Bar(elbow, hand, t * 0.85f, sleeve);
        PHand(m, hand, t * 0.55f, skin);
    }

    // ---- the guests that stand: each its foot at 0, facing +z
    // The vampire: black evening dress, his cape lined red flung back, a tall collar, slicked hair to a widow's peak, red eyes, fangs, a
    // goblet of red wine raised in greeting.
    private static Model PartyVampire()
    {
        var m = new Model();
        foreach (var s in new[] { -1f, 1f }) PLeg(m, new(s * 110, 840, 0), new(s * 112, 430, 20), new(s * 110, 70, 0), 150, PBlack, PBlack);
        PTorso(m, Vector3.Zero, new[] { (210.0, 800.0), (240.0, 1050.0), (265.0, 1330.0), (170.0, 1450.0) }, PBlack);
        // (his shirt front and bow tie)
        m.Facing(new[] { new Vector3(-90, 1040, 175), new Vector3(90, 1040, 175), new Vector3(110, 1400, 150), new Vector3(-110, 1400, 150) }, PWhite, new(0, 1200, 2000));
        m.Facing(new[] { new Vector3(-90, 1360, 162), new Vector3(0, 1330, 168), new Vector3(-90, 1300, 162) }, PRed, new(0, 1330, 2000));
        m.Facing(new[] { new Vector3(90, 1360, 162), new Vector3(90, 1300, 162), new Vector3(0, 1330, 168) }, PRed, new(0, 1330, 2000));
        // the cape: three panels from his shoulders to the floor behind him, black outside, red inside
        var shoulders = new[] { new Vector3(-270, 1420, -40), new Vector3(-150, 1450, -170), new Vector3(150, 1450, -170), new Vector3(270, 1420, -40) };
        var hem = new[] { new Vector3(-620, 40, 120), new Vector3(-330, 30, -420), new Vector3(330, 30, -420), new Vector3(620, 40, 120) };
        for (var k = 0; k < 3; k++)
        {
            var q = new[] { shoulders[k], shoulders[k + 1], hem[k + 1], hem[k] };
            var mid = (q[0] + q[1] + q[2] + q[3]) / 4;
            m.Facing(q, PBlack, mid + new Vector3(mid.X, 0, mid.Z - 200) * 3);
            var inner = q.Select(p => p + Vector3.Normalize(new Vector3(-p.X, 0, -p.Z + 100)) * 12).ToArray();
            m.Facing(inner, PRed, new Vector3(0, mid.Y, 300));
        }
        // the collar, standing up behind his head
        foreach (var s in new[] { -1f, 1f })
        {
            var c = new[] { new Vector3(s * 60, 1440, -130), new Vector3(s * 230, 1430, -60), new Vector3(s * 260, 1760, -120) };
            m.Facing(c, PRed, new(0, 1550, 200));
            m.Facing(c.Select(p => p + new Vector3(0, 0, -14)).ToArray(), PBlack, new(s * 400, 1550, -900));
        }
        // the head: pale, the hair black and slicked back to a point over his brow
        var head = new Vector3(0, 1590, 10);
        PHead(m, head, 135, PPale);
        m.Lathe(new (double, double)[] { (0, 160), (110, 135), (145, 60), (150, -20) }, 10, _ => PBlack, false, false, at: head + new Vector3(0, 0, -12));
        m.Facing(new[] { head + new Vector3(-60, 105, 112), head + new Vector3(60, 105, 112), head + new Vector3(0, 40, 128) }, PBlack, head + new Vector3(0, 0, 2000));
        PEyes(m, head, 135, RedEye, 0.18f, 0.35f, 0.18f);
        foreach (var s in new[] { -1f, 1f })
            m.Facing(new[] { head + new Vector3(s * 30, -70, 133), head + new Vector3(s * 52, -70, 131), head + new Vector3(s * 40, -115, 130) }, PWhite, head + new Vector3(0, -80, 2000));
        // the arms: his right raises the goblet, his left holds the cape out
        PArm(m, new(-250, 1390, -10), new(-330, 1150, 180), new(-230, 1260, 360), 110, PBlack, PPale);
        m.Lathe(new (double, double)[] { (55, 0), (18, 20), (14, 120), (75, 150), (90, 260), (0, 262) }, 8, i => i >= 4 ? PRed : PGold, true, false, at: new Vector3(-230, 1290, 360));
        PArm(m, new(250, 1390, -10), new(420, 1100, 60), new(560, 900, 160), 110, PBlack, PPale);
        return m;
    }

    // The witch standing at her cauldron (in front of her, +z), stirring it with a long stick: green, her ginger hair under a tall black
    // hat with an orange band, a purple robe.
    private static Model PartyWitch()
    {
        var m = new Model();
        PTorso(m, Vector3.Zero, new[] { (430.0, 0.0), (370.0, 300.0), (260.0, 880.0), (215.0, 1180.0), (150.0, 1330.0) }, PMauve, 0.8f, 10);
        var head = new Vector3(0, 1450, 30);
        PHead(m, head, 125, Skin);
        // (the nose, long and hooked; the chin)
        m.Facing(new[] { head + new Vector3(-30, 10, 118), head + new Vector3(30, 10, 118), head + new Vector3(0, -60, 300) }, Skin, head + new Vector3(0, 300, 1500));
        m.Facing(new[] { head + new Vector3(-28, 10, 116), head + new Vector3(0, -60, 300), head + new Vector3(0, 60, 125) }, Skin, head + new Vector3(-1500, 0, 300));
        m.Facing(new[] { head + new Vector3(28, 10, 116), head + new Vector3(0, 60, 125), head + new Vector3(0, -60, 300) }, Skin, head + new Vector3(1500, 0, 300));
        PEyes(m, head, 125, GYellow, 0.25f, 0.38f, 0.17f);
        // (her hair down her back, ginger)
        for (var k = -3; k <= 3; k++)
            m.Bar(head + new Vector3(k * 34, 70, -60), head + new Vector3(k * 52, -330, -150), 46, POrange);
        // the hat
        m.Lathe(new (double, double)[] { (380, 105), (170, 120) }, 12, _ => Hat, true, false, at: head);
        m.Lathe(new (double, double)[] { (172, 125), (178, 185) }, 12, _ => GOrange, false, false, at: head);
        m.Lathe(new (double, double)[] { (170, 185), (120, 360), (60, 520), (0, 600) }, 10, _ => Hat, false, false, at: head + new Vector3(0, 0, -20));
        // the stick into the cauldron, both hands on it
        var top = new Vector3(0, 1320, 300); var foot = new Vector3(0, 560, 900);
        m.Bar(top, foot, 55, Wood);
        PArm(m, new(-200, 1250, 0), new(-260, 1060, 200), top + Vector3.Normalize(foot - top) * 150 + new Vector3(-40, 0, 0), 100, PMauve, Skin);
        PArm(m, new(200, 1250, 0), new(260, 1020, 230), top + Vector3.Normalize(foot - top) * 360 + new Vector3(40, 0, 0), 100, PMauve, Skin);
        return m;
    }

    // A fire under the cauldron: logs crossed, flames.
    private static Model PartyFire()
    {
        var m = new Model();
        for (var k = 0; k < 4; k++)
        {
            var a = k * MathF.PI / 4;
            m.Bar(new Vector3(MathF.Cos(a) * -520, 60, MathF.Sin(a) * -520), new Vector3(MathF.Cos(a) * 520, 60, MathF.Sin(a) * 520), 110, Wood);
        }
        for (var k = 0; k < 7; k++)
        {
            var a = k * 2 * MathF.PI / 7;
            var at = new Vector3(MathF.Cos(a) * 300, 90, MathF.Sin(a) * 300);
            var h = 260 + 90 * (k % 3);
            m.Lathe(new (double, double)[] { (110, 0), (60, h * 0.6), (0, h) }, 4, _ => k % 2 == 0 ? Flame : GOrange, false, false, at: at);
        }
        return m;
    }

    // Frankenstein's monster: tall and square, green, flat-headed, bolts in his neck, a scar, a short dark coat, big boots, his arms out.
    private static Model PartyFrank()
    {
        var m = new Model();
        foreach (var s in new[] { -1f, 1f })
        {
            PLeg(m, new(s * 150, 980, 0), new(s * 160, 520, 30), new(s * 160, 160, 0), 210, PDenim, PBlack);
            m.Box(new Vector3(s * 160 - 160, 0, -160), new Vector3(s * 160 + 160, 170, 300), PBlack);
        }
        m.Box(new Vector3(-330, 930, -180), new Vector3(330, 1660, 180), PJacket);
        m.Box(new Vector3(-120, 1300, 175), new Vector3(120, 1640, 190), PRags);
        // the arms out in front of him
        PArm(m, new(-400, 1580, 0), new(-380, 1520, 340), new(-330, 1480, 700), 170, PJacket, Skin);
        PArm(m, new(400, 1580, 0), new(390, 1500, 340), new(340, 1430, 690), 170, PJacket, Skin);
        m.Box(new Vector3(-90, 1650, -90), new Vector3(90, 1730, 90), Skin);
        var lo = new Vector3(-180, 1720, -170); var hi = new Vector3(180, 2060, 170);
        m.Box(lo, hi, Skin);
        m.Box(new Vector3(-190, 2050, -185), new Vector3(190, 2130, 180), PBlack);
        // (his fringe, ragged over his brow)
        for (var k = 0; k < 6; k++)
        {
            var x = -180 + k * 60;
            m.Facing(new[] { new Vector3(x, 2052, 182), new Vector3(x + 60, 2052, 182), new Vector3(x + 30, 1975 - 15 * (k % 2), 182) }, PBlack, new(0, 2000, 2000));
        }
        foreach (var s in new[] { -1f, 1f })
        {
            m.Bar(new Vector3(s * 175, 1770, 0), new Vector3(s * 290, 1770, 0), 60, PSilver);
            var e = new Vector3(s * 70, 1930, 176);
            m.Facing(new[] { e + new Vector3(-35, -12, 0), e + new Vector3(35, -12, 0), e + new Vector3(0, 22, 0) }, GYellow, e + new Vector3(0, 0, 2000));
        }
        // (his scar and his mouth)
        m.Box(new Vector3(-110, 2010, 170), new Vector3(110, 2022, 178), PBlack);
        for (var k = 0; k < 5; k++) m.Box(new Vector3(-90 + k * 45, 1998, 170), new Vector3(-84 + k * 45, 2034, 179), PBlack);
        m.Box(new Vector3(-80, 1790, 170), new Vector3(80, 1805, 178), PBlack);
        return m;
    }

    // The mummy: wrapped in bandages from head to foot, ends of them hanging loose, his arms out, his eyes green in the dark.
    private static Model PartyMummy()
    {
        var m = new Model();
        foreach (var s in new[] { -1f, 1f }) PLeg(m, new(s * 120, 820, 0), new(s * 130, 430, 40), new(s * 120, 80, 0), 170, PBandage, PBandage);
        // (the body's bands: ring after ring, a little out of true)
        var rings = new List<(double, double)>();
        for (var k = 0; k <= 9; k++) rings.Add((k % 2 == 0 ? 225 : 210 + 5 * (k % 3), 780 + k * 70));
        rings.Add((150, 1490));
        PTorso(m, Vector3.Zero, rings.ToArray(), PBandage, 0.72f, 8);
        PArm(m, new(-260, 1380, 0), new(-280, 1310, 330), new(-240, 1250, 650), 120, PBandage, PBandage);
        PArm(m, new(260, 1380, 0), new(270, 1350, 320), new(250, 1330, 640), 120, PBandage, PBandage);
        var head = new Vector3(0, 1610, 0);
        m.Lathe(new (double, double)[] { (0, -150), (95, -130), (130, -40), (135, 40), (110, 110), (60, 150), (0, 165) }, 10, _ => PBandage, false, false, at: head);
        // (a dark band over his eyes, and the eyes in it)
        m.Lathe(new (double, double)[] { (137, -5), (137, 55) }, 10, _ => PChoc, false, false, at: head);
        PEyes(m, head + new Vector3(0, -2, 6), 135, GGreen, 0.18f, 0.36f, 0.17f);
        // (loose ends: strips hanging from his arms and his side)
        m.Face(new[] { new Vector3(-240, 1240, 560), new Vector3(-200, 1240, 560), new Vector3(-150, 860, 520), new Vector3(-190, 850, 520) }, PBandage, new(0, 1000, 0), both: true);
        m.Face(new[] { new Vector3(245, 1320, 420), new Vector3(285, 1320, 420), new Vector3(330, 980, 380), new Vector3(290, 970, 380) }, PBandage, new(0, 1000, 0), both: true);
        m.Face(new[] { new Vector3(200, 900, 120), new Vector3(230, 900, 80), new Vector3(300, 420, 160), new Vector3(265, 420, 190) }, PBandage, new(0, 700, 0), both: true);
        return m;
    }

    // The werewolf: hunched, shaggy brown, his snout up howling, pointed ears, yellow eyes, claws; torn trousers.
    private static Model PartyWolf()
    {
        var m = new Model();
        foreach (var s in new[] { -1f, 1f })
        {
            // (legs bent the wolf's way: thigh forward, shin back, up on his toes)
            m.Bar(new Vector3(s * 150, 820, -40), new Vector3(s * 170, 500, 160), 190, PDenim);
            m.Bar(new Vector3(s * 170, 500, 160), new Vector3(s * 160, 160, -90), 140, PFur);
            m.Bar(new Vector3(s * 160, 160, -90), new Vector3(s * 160, 40, 120), 120, PFur);
            for (var k = -1; k <= 1; k++)
                m.Facing(new[] { new Vector3(s * 160 + k * 40 - 15, 0, 170), new Vector3(s * 160 + k * 40 + 15, 0, 170), new Vector3(s * 160 + k * 40, 40, 250) }, PWhite, new(0, 300, 2000));
        }
        // the body, leaning forward
        PTorso(m, new Vector3(0, 0, 60), new[] { (240.0, 780.0), (300.0, 1050.0), (320.0, 1300.0), (230.0, 1480.0) }, PFur, 0.75f, 8);
        m.Facing(new[] { new Vector3(-150, 1050, 300), new Vector3(150, 1050, 300), new Vector3(170, 1380, 270), new Vector3(-170, 1380, 270) }, PFurLight, new(0, 1200, 2000));
        // (shaggy tufts down his back)
        for (var k = 0; k < 5; k++)
            m.Face(new[] { new Vector3(-120 + k * 60, 1460 - k * 40, -150), new Vector3(-80 + k * 60, 1460 - k * 40, -150), new Vector3(-100 + k * 60, 1280 - k * 60, -330) }, PFur, new(0, 1300, 0), both: true);
        // the arms hanging, claws out
        foreach (var s in new[] { -1f, 1f })
        {
            PArm(m, new(s * 300, 1420, 60), new(s * 380, 1080, 160), new(s * 340, 780, 300), 140, PFur, PFur);
            for (var k = -1; k <= 1; k++)
                m.Bar(new Vector3(s * 340 + k * 30, 760, 320), new Vector3(s * 345 + k * 38, 640, 380), 22, PWhite);
        }
        // the head thrown back, howling: the snout up and forward, the jaw open
        var head = new Vector3(0, 1620, 140);
        m.Lathe(new (double, double)[] { (0, -150), (120, -120), (160, 0), (130, 110), (0, 160) }, 8, _ => PFur, false, false, at: head);
        var nose = head + new Vector3(0, 300, 280);
        m.Bar(head + new Vector3(0, 40, 60), nose, 130, PFur);
        m.Box(nose - new Vector3(40, 30, 30), nose + new Vector3(40, 40, 40), PBlack);
        m.Bar(head + new Vector3(0, -50, 80), head + new Vector3(0, 120, 330), 90, PFurLight);
        m.Facing(new[] { head + new Vector3(-35, 20, 150), head + new Vector3(35, 20, 150), nose + new Vector3(0, -80, -40) }, PRed, nose + new Vector3(0, -800, 600));
        foreach (var s in new[] { -1f, 1f })
        {
            m.Face(new[] { head + new Vector3(s * 60, 120, -40), head + new Vector3(s * 140, 100, -20), head + new Vector3(s * 120, 310, -120) }, PFur, head, both: true);
            var e = head + new Vector3(s * 62, 70, 135);
            m.Facing(new[] { e + new Vector3(-28, -10, 0), e + new Vector3(28, -10, 0), e + new Vector3(0, 22, 0) }, GYellow, e + new Vector3(0, 400, 2000));
        }
        return m;
    }

    // The grim reaper: a black hooded robe, a skull in the hood's shadow with red eyes, bony hands, his scythe.
    private static Model PartyReaper()
    {
        var m = new Model();
        PTorso(m, Vector3.Zero, new[] { (460.0, 0.0), (380.0, 220.0), (300.0, 900.0), (270.0, 1380.0), (210.0, 1580.0) }, PBlack, 0.8f, 10);
        // the hood: a cowl round the head, open in front
        var head = new Vector3(0, 1760, 0);
        m.Lathe(new (double, double)[] { (0, 230), (150, 200), (205, 80), (215, -60), (190, -190) }, 10, _ => PBlack, false, false, at: head + new Vector3(0, 0, -30),
            shape: a => 1, yShape: null);
        m.Facing(Enumerable.Range(0, 8).Select(k => head + new Vector3(140 * MathF.Cos(k * MathF.PI / 4), 165 * MathF.Sin(k * MathF.PI / 4), 175)).ToArray(), Hollow, head + new Vector3(0, 0, 2000));
        // (the skull in it)
        var skull = head + new Vector3(0, -10, 60);
        m.Lathe(new (double, double)[] { (0, -120), (80, -100), (110, -10), (100, 70), (0, 120) }, 8, _ => Bone, false, false, at: skull);
        PEyes(m, skull, 110, GRed, 0.12f, 0.38f, 0.22f);
        m.Facing(new[] { skull + new Vector3(-50, -70, 106), skull + new Vector3(50, -70, 106), skull + new Vector3(50, -55, 108), skull + new Vector3(-50, -55, 108) }, PBlack, skull + new Vector3(0, 0, 2000));
        // the arms in their sleeves, bony hands on the scythe's handle
        var snathLo = new Vector3(380, 40, 160); var snathHi = new Vector3(330, 2420, 120);
        m.Bar(snathLo, snathHi, 70, Wood);
        PArm(m, new(-280, 1480, 0), new(-150, 1150, 280), snathLo + (snathHi - snathLo) * 0.5f + new Vector3(-40, 0, 0), 150, PBlack, Bone);
        PArm(m, new(280, 1480, 0), new(400, 1350, 200), snathLo + (snathHi - snathLo) * 0.68f + new Vector3(-40, 0, 0), 150, PBlack, Bone);
        // the blade, curving forward from the top of the handle
        var blade = new List<Vector3>();
        for (var k = 0; k <= 6; k++)
        {
            var t = k / 6f;
            blade.Add(snathHi + new Vector3(-30 * t, -60 * t * t, 120 + 900 * t) + new Vector3(0, -260 * MathF.Sin(t * MathF.PI * 0.9f), 0));
        }
        for (var k = 0; k < 6; k++)
        {
            var a = blade[k]; var b = blade[k + 1];
            var w = 130 * (1 - k / 6f) + 20;
            m.Face(new[] { a, b, b + new Vector3(0, w * 0.8f, 0), a + new Vector3(0, w, 0) }, PSilver, a + new Vector3(500, 0, 0), both: true);
        }
        return m;
    }

    // A zombie: grey-green, in rags, shuffling with his arms out (lurching: one higher), his head to one side, his eyes glowing.
    private static Model PartyZombie(bool woman)
    {
        var m = new Model();
        foreach (var s in new[] { -1f, 1f }) PLeg(m, new(s * 110, 800, 0), new(s * (125 + 25 * s), 420, 60 * s), new(s * 120, 80, -40 * s), 140, woman ? PZombie : PDenim, PChoc);
        if (woman) PTorso(m, Vector3.Zero, new[] { (380.0, 380.0), (300.0, 650.0), (230.0, 900.0), (200.0, 1120.0), (230.0, 1300.0), (150.0, 1420.0) }, PMauve, 0.75f, 9);
        else PTorso(m, Vector3.Zero, new[] { (210.0, 760.0), (225.0, 1000.0), (245.0, 1300.0), (160.0, 1420.0) }, PRags, 0.7f, 8);
        // (the rags torn at the hem)
        for (var k = 0; k < 6; k++)
        {
            var a = k * MathF.PI / 3;
            var r = woman ? 380f : 215f; var y = woman ? 380f : 760f;
            m.Face(new[] { new Vector3(r * MathF.Cos(a), y, r * 0.7f * MathF.Sin(a)), new Vector3(r * MathF.Cos(a + 0.4f), y, r * 0.7f * MathF.Sin(a + 0.4f)),
                new Vector3(r * 1.02f * MathF.Cos(a + 0.2f), y - 140, r * 0.72f * MathF.Sin(a + 0.2f)) }, woman ? PMauve : PRags, new(0, y, 0), both: true);
        }
        PArm(m, new(-240, 1350, 0), new(-260, 1330, 330), new(-230, 1330, 660), 105, woman ? PZombie : PRags, PZombie);
        PArm(m, new(240, 1350, 0), new(260, 1240, 300), new(250, 1150, 600), 105, woman ? PZombie : PRags, PZombie);
        var head = new Vector3(30, 1550, 40);
        PHead(m, head, 125, PZombie, 1.1f);
        PEyes(m, head, 125, GYellow, 0.2f, 0.38f, 0.18f);
        m.Facing(new[] { head + new Vector3(-50, -60, 118), head + new Vector3(50, -60, 118), head + new Vector3(30, -90, 116), head + new Vector3(-40, -95, 116) }, PBlack, head + new Vector3(0, 0, 2000));
        if (woman)
            for (var k = -3; k <= 3; k++) m.Bar(head + new Vector3(k * 36, 90, -40), head + new Vector3(k * 60, -380, -110), 44, PSilver);
        else m.Lathe(new (double, double)[] { (0, 150), (100, 120), (130, 50) }, 8, _ => PChoc, false, false, at: head + new Vector3(0, 0, -15));
        return m;
    }

    // A black cat: sitting (its tail round its feet), or arched (its back up, its tail high).
    private static Model PartyCat(bool arched)
    {
        var m = new Model();
        Vector3 head;
        if (!arched)
        {
            m.Lathe(new (double, double)[] { (0, 0), (150, 20), (170, 150), (120, 330), (70, 420) }, 8, _ => PBlack, false, false, at: new Vector3(0, 0, -30));
            head = new Vector3(0, 470, 30);
            var tail = new[] { new Vector3(0, 30, -170), new Vector3(150, 25, -140), new Vector3(210, 25, 20), new Vector3(150, 25, 160) };
            for (var k = 0; k + 1 < tail.Length; k++) m.Bar(tail[k], tail[k + 1], 45, PBlack);
            foreach (var s in new[] { -1f, 1f }) m.Bar(new Vector3(s * 60, 260, 70), new Vector3(s * 60, 0, 110), 50, PBlack);
        }
        else
        {
            var back = new[] { new Vector3(0, 220, -260), new Vector3(0, 380, -100), new Vector3(0, 400, 80), new Vector3(0, 300, 230) };
            for (var k = 0; k + 1 < back.Length; k++) m.Bar(back[k], back[k + 1], 150, PBlack);
            foreach (var (x, z) in new[] { (-60f, -230f), (60f, -230f), (-60f, 200f), (60f, 200f) }) m.Bar(new Vector3(x, 240, z), new Vector3(x * 1.2f, 0, z * 1.15f), 45, PBlack);
            var tail = new[] { new Vector3(0, 230, -280), new Vector3(0, 470, -330), new Vector3(0, 640, -260), new Vector3(0, 700, -170) };
            for (var k = 0; k + 1 < tail.Length; k++) m.Bar(tail[k], tail[k + 1], 45, PBlack);
            head = new Vector3(0, 380, 320);
        }
        PHead(m, head, 95, PBlack, 0.9f, 8);
        foreach (var s in new[] { -1f, 1f })
            m.Face(new[] { head + new Vector3(s * 25, 60, -10), head + new Vector3(s * 90, 50, -10), head + new Vector3(s * 70, 150, -20) }, PBlack, head, both: true);
        PEyes(m, head, 95, GYellow, 0.15f, 0.42f, 0.22f);
        return m;
    }

    // An open coffin standing against a wall: dark wood, lined with red velvet, brass handles.
    private static Model PartyCoffin()
    {
        var m = new Model();
        var outline = new (double X, double Y)[] { (-260, 0), (260, 0), (430, 1450), (300, 1950), (-300, 1950), (-430, 1450) };
        m.Slab(outline, 160, PChoc, PChoc, z0: -150);
        // (its sides, standing out of the back to the front)
        for (var k = 0; k < outline.Length; k++)
        {
            var a = outline[k]; var b = outline[(k + 1) % outline.Length];
            m.Face(new[] { new Vector3((float)a.X, (float)a.Y, -70), new Vector3((float)b.X, (float)b.Y, -70), new Vector3((float)b.X, (float)b.Y, 230), new Vector3((float)a.X, (float)a.Y, 230) },
                PChoc, new Vector3(0, 975, 80), both: true);
        }
        var lining = outline.Select(p => new Vector3((float)p.X * 0.86f, 70 + (float)p.Y * 0.93f, -66)).ToArray();
        m.Facing(lining, PRed, new Vector3(0, 975, 2000));
        foreach (var s in new[] { -1f, 1f })
            foreach (var y in new[] { 500f, 1100f })
                m.Box(new Vector3(s * (y < 800 ? 300 : 395) - 30, y - 30, 40), new Vector3(s * (y < 800 ? 300 : 395) + 30, y + 30, 180), PGold);
        return m;
    }

    // ---- the banquet
    // A long table `len` along x: a black cloth hanging down its sides, an orange hem, its legs under it.
    public const float PartyTableTop = 680;
    private static Model PartyTable(float len)
    {
        var m = new Model();
        float hl = len / 2, hw = 550, h = PartyTableTop;
        m.Box(new Vector3(-hl, h - 50, -hw), new Vector3(hl, h, hw), PBlack);
        foreach (var s in new[] { -1f, 1f })
        {
            m.Box(new Vector3(-hl, h - 400, s > 0 ? hw - 20 : -hw), new Vector3(hl, h - 50, s > 0 ? hw : -hw + 20), PBlack);
            m.Box(new Vector3(-hl - 2, h - 470, s > 0 ? hw - 18 : -hw - 2), new Vector3(hl + 2, h - 400, s > 0 ? hw + 2 : -hw + 18), GOrange);
            m.Box(new Vector3(s > 0 ? hl - 20 : -hl, h - 400, -hw), new Vector3(s > 0 ? hl : -hl + 20, h - 50, hw), PBlack);
            m.Box(new Vector3(s > 0 ? hl - 18 : -hl - 2, h - 470, -hw - 2), new Vector3(s > 0 ? hl + 2 : -hl + 18, h - 400, hw + 2), GOrange);
            foreach (var t in new[] { -1f, 1f })
                m.Box(new Vector3(s * (hl - 160) - 45, 0, t * (hw - 160) - 45), new Vector3(s * (hl - 160) + 45, h - 470, t * (hw - 160) + 45), Wood);
        }
        return m;
    }

    // The punch bowl on its stand: silver, brimming with glowing green punch, an eye floating in it, a ladle.
    private static Model PartyPunch()
    {
        var m = new Model();
        m.Lathe(new (double, double)[] { (190, 0), (60, 60), (50, 200), (130, 260), (330, 420), (380, 560), (0, 560) }, 14, _ => PSilver, true, false);
        m.Lathe(new (double, double)[] { (360, 530), (0, 540) }, 14, _ => Brew, false, false);
        var eye = new Vector3(90, 560, 40);
        m.Lathe(new (double, double)[] { (0, -60), (55, -40), (62, 0), (50, 35), (0, 50) }, 8, _ => PWhite, false, false, at: eye);
        m.Facing(new[] { eye + new Vector3(-25, 40, 18), eye + new Vector3(25, 40, 18), eye + new Vector3(0, 52, -25) }, PRed, eye + new Vector3(0, 1000, 0));
        m.Bar(new Vector3(-200, 545, -100), new Vector3(-420, 880, -180), 35, PSilver);
        // (glasses of it beside the bowl)
        foreach (var (x, z) in new[] { (520f, 140f), (480f, -220f), (-520f, 200f) })
        {
            m.Lathe(new (double, double)[] { (60, 0), (55, 160), (0, 160) }, 6, _ => PSilver, true, false, at: new Vector3(x, 0, z));
            m.Lathe(new (double, double)[] { (54, 130), (0, 132) }, 6, _ => Brew, false, false, at: new Vector3(x, 0, z));
        }
        return m;
    }

    // A cake of three tiers -- chocolate, pink, cream -- orange icing round each, candles on top, a pumpkin on its crown.
    private static Model PartyCake()
    {
        var m = new Model();
        var tiers = new[] { (420.0, 0.0, 220.0, PChoc), (310.0, 220.0, 200.0, PPink), (200.0, 420.0, 180.0, PCream) };
        foreach (var (r, y, h, paint) in tiers)
        {
            m.Lathe(new (double, double)[] { (r, y), (r, y + h), (0, y + h) }, 14, _ => paint, false, false);
            m.Lathe(new (double, double)[] { (r + 12, y + h - 40), (r + 12, y + h + 5) }, 14, _ => GOrange, false, false);
        }
        m.Lathe(new (double, double)[] { (520, -5), (520, 15), (0, 15) }, 14, _ => PSilver, true, false);
        var pumpkin = Lantern(110, 150, 10, 5);
        PAdd(m, pumpkin, new Vector3(0, 600, 0));
        for (var k = 0; k < 6; k++)
        {
            var a = k * MathF.PI / 3 + 0.3f;
            var at = new Vector3(150 * MathF.Cos(a), 600, 150 * MathF.Sin(a));
            m.Bar(at, at + new Vector3(0, 160, 0), 28, k % 2 == 0 ? GMagenta : GCyan);
            m.Facing(new[] { at + new Vector3(-18, 165, 0), at + new Vector3(18, 165, 0), at + new Vector3(0, 250, 0) }, Flame, at + new Vector3(0, 200, 2000));
            m.Facing(new[] { at + new Vector3(0, 165, -18), at + new Vector3(0, 165, 18), at + new Vector3(0, 250, 0) }, Flame, at + new Vector3(2000, 200, 0));
        }
        return m;
    }

    // Plates of treats: cookies (pumpkin-orange, ghost-white), candy corn, goblets of red wine, a skull.
    private static Model PartyTreats(int which)
    {
        var m = new Model();
        switch (which)
        {
            case 0:     // cookies on a plate
                m.Lathe(new (double, double)[] { (300, 0), (330, 30), (0, 30) }, 12, _ => PWhite, true, false);
                for (var k = 0; k < 7; k++)
                {
                    var a = k * 2 * MathF.PI / 7;
                    var at = k == 6 ? new Vector3(0, 30, 0) : new Vector3(170 * MathF.Cos(a), 35 + 8 * (k % 2), 170 * MathF.Sin(a));
                    m.Lathe(new (double, double)[] { (80, 0), (80, 25), (0, 25) }, 8, _ => k % 3 == 0 ? PWhite : POrange, false, false, at: at);
                }
                break;
            case 1:     // a bowl of candy corn
                m.Lathe(new (double, double)[] { (140, 0), (300, 140), (320, 200), (0, 170) }, 12, _ => PMauve, true, false);
                for (var k = 0; k < 9; k++)
                {
                    var a = k * 2 * MathF.PI / 9;
                    var at = new Vector3(190 * MathF.Cos(a) * (k % 2 == 0 ? 1 : 0.5f), 190, 190 * MathF.Sin(a) * (k % 2 == 0 ? 1 : 0.5f));
                    m.Lathe(new (double, double)[] { (55, 0), (40, 35), (22, 70), (0, 105) }, 5, i => i == 0 ? GYellow : i == 1 ? GOrange : PWhite, true, false, at: at);
                }
                break;
            case 2:     // goblets
                foreach (var (x, z) in new[] { (-180f, 0f), (60f, 160f), (170f, -120f) })
                    m.Lathe(new (double, double)[] { (70, 0), (16, 25), (14, 140), (80, 175), (95, 290), (0, 292) }, 8, i => i >= 4 ? PRed : PGold, true, false, at: new Vector3(x, 0, z));
                break;
            default:    // a skull with a candle on it
                m.Lathe(new (double, double)[] { (0, 0), (120, 20), (160, 140), (150, 260), (90, 330), (0, 345) }, 10, _ => Bone, false, false);
                PEyes(m, new Vector3(0, 180, 0), 150, PBlack, 0.05f, 0.38f, 0.32f);
                m.Lathe(new (double, double)[] { (45, 330), (45, 520), (35, 530) }, 6, _ => Wax, false, true);
                m.Facing(new[] { new Vector3(-22, 535, 0), new Vector3(22, 535, 0), new Vector3(0, 660, 0) }, Flame, new Vector3(0, 600, 2000));
                m.Facing(new[] { new Vector3(0, 535, -22), new Vector3(0, 535, 22), new Vector3(0, 660, 0) }, Flame, new Vector3(2000, 600, 0));
                break;
        }
        return m;
    }

    // A candelabra: a gold stand with five arms, a candle burning on each.
    private static Model PartyCandelabra()
    {
        var m = new Model();
        m.Lathe(new (double, double)[] { (200, 0), (170, 40), (50, 90), (40, 600), (70, 640), (0, 650) }, 8, _ => PGold, true, false);
        for (var k = -2; k <= 2; k++)
        {
            var tip = new Vector3(k * 160, 900 - Math.Abs(k) * 60, 0);
            if (k != 0)
            {
                m.Bar(new Vector3(0, 640, 0), new Vector3(k * 90, 560, 0), 40, PGold);
                m.Bar(new Vector3(k * 90, 560, 0), tip - new Vector3(0, 60, 0), 40, PGold);
            }
            else m.Bar(new Vector3(0, 640, 0), tip - new Vector3(0, 60, 0), 40, PGold);
            m.Lathe(new (double, double)[] { (60, -60), (60, -40), (0, -40) }, 6, _ => PGold, false, false, at: tip);
            m.Lathe(new (double, double)[] { (32, -40), (32, 230), (24, 240) }, 6, _ => Wax, false, true, at: tip);
            m.Facing(new[] { tip + new Vector3(-20, 245, 0), tip + new Vector3(20, 245, 0), tip + new Vector3(0, 380, 0) }, Flame, tip + new Vector3(0, 300, 2000));
            m.Facing(new[] { tip + new Vector3(0, 245, -20), tip + new Vector3(0, 245, 20), tip + new Vector3(0, 380, 0) }, Flame, tip + new Vector3(2000, 300, 0));
        }
        return m;
    }

    // ---- the hall's dressing
    // A pile of pumpkins: five jack-o'-lanterns of different sizes, heaped.
    private static Model PartyPumpkins()
    {
        var m = new Model();
        foreach (var (x, y, z, r, turn) in new[] { (-380f, 0f, 60f, 330f, 0.3f), (330f, 0f, 120f, 300f, -0.4f), (0f, 0f, -330f, 360f, 0.1f), (-60f, 380f, -60f, 260f, 0.2f), (420f, 0f, -380f, 220f, -0.9f) })
            PAdd(m, Lantern(r, r * 1.35f, 10, 5), new Vector3(x, y, z), turn);
        return m;
    }

    // A bunch of balloons on strings tied to a weight on the floor: `colours` glowing or bright (orange, purple, green...).
    private static Model PartyBalloons(int seed)
    {
        var m = new Model();
        var random = new Random(seed);
        var paints = new[] { POrange, PMauve, PGreen, GMagenta, PYellow, GViolet, PRed };
        m.Lathe(new (double, double)[] { (120, 0), (120, 90), (60, 120), (0, 120) }, 6, _ => PBlack, true, false);
        for (var k = 0; k < 5; k++)
        {
            var a = k * 2 * MathF.PI / 5 + (float)random.NextDouble() * 0.5f;
            var at = new Vector3(MathF.Cos(a) * (230 + random.Next(120)), 1700 + random.Next(600) + (k == 0 ? 300 : 0), MathF.Sin(a) * (230 + random.Next(120)));
            if (k == 0) at = new Vector3(0, at.Y, 0);
            m.Lathe(new (double, double)[] { (0, -260), (90, -230), (190, -110), (210, 30), (170, 170), (90, 250), (0, 280) }, 8, _ => paints[(seed + k) % paints.Length], false, false, at: at);
            m.Bar(new Vector3(0, 110, 0), at - new Vector3(0, 270, 0), 12, PWhite);
        }
        return m;
    }

    // Bunting: a cord from `from` to `to` (world units, the model's origin half way), sagging `sag` in the middle, pennants hanging from it
    // every PennantStep in turn orange, purple and green (they glow), both their sides.
    private const float PennantStep = 520;
    private static Model PartyBunting(Vector3 from, Vector3 to, float sag, int seed)
    {
        var m = new Model();
        var mid = (from + to) / 2;
        from -= mid; to -= mid;
        var len = (to - from).Length();
        var n = Math.Max(2, (int)(len / PennantStep));
        Vector3 At(float t) => from + (to - from) * t - new Vector3(0, sag * 4 * t * (1 - t), 0);
        var colours = new[] { GOrange, GViolet, GGreen, GMagenta };
        var side = Vector3.Normalize(to - from);
        for (var k = 0; k < n; k++)
        {
            float t0 = k / (float)n, t1 = (k + 1) / (float)n;
            m.Bar(At(t0), At(t1), 18, PBlack);
            var a = At(t0 + 0.12f / n); var b = At(t1 - 0.12f / n);
            var tip = (a + b) / 2 - new Vector3(0, 420, 0);
            m.Face(new[] { a, b, tip }, colours[(k + seed) % colours.Length], (a + b + tip) / 3 + Vector3.Cross(side, Vector3.UnitY) * 50, both: true);
        }
        return m;
    }

    // The dance floor: tiles `tile` across, `nx` by `nz`, glowing in turn (a light under each), dark between, a gold edge.
    private static Model PartyDanceFloor(int nx, int nz, float tile)
    {
        var m = new Model();
        var colours = new[] { GMagenta, GCyan, GOrange, GGreen, GViolet, GYellow };
        float x0 = -nx * tile / 2, z0 = -nz * tile / 2;
        m.Box(new Vector3(x0 - 60, 0, z0 - 60), new Vector3(-x0 + 60, 14, -z0 + 60), PGold);
        for (var i = 0; i < nx; i++)
            for (var k = 0; k < nz; k++)
            {
                float x = x0 + i * tile, z = z0 + k * tile, g = tile * 0.06f;
                var paint = (i + k) % 2 == 0 ? colours[(i * 7 + k * 3) % colours.Length] : PBlack;
                m.Facing(new[] { new Vector3(x + g, 18, z + g), new Vector3(x + tile - g, 18, z + g), new Vector3(x + tile - g, 18, z + tile - g), new Vector3(x + g, 18, z + tile - g) },
                    paint, new Vector3(x + tile / 2, 2000, z + tile / 2));
            }
        return m;
    }

    // A broom leaning against a wall (its foot at 0, leaning towards -z).
    private static Model PartyBroom()
    {
        var m = new Model();
        var foot = new Vector3(0, 200, 120); var top = new Vector3(0, 1700, -200);
        m.Bar(foot, top, 55, Wood);
        var first = m.Points.Count;
        m.Lathe(new (double, double)[] { (50, 0), (150, 200), (230, 420), (0, 440) }, 8, _ => Straw, false, false);
        m.Move(first, q => new Vector3(q.X, 420 - q.Y, q.Z + 120 + q.Y * 0.06f));
        return m;
    }

    // ---- the moving guests' bodies (BODY.HQR: drawn by the race-track mode, spook=)
    // The disco ball: silver facets, a few lit in colours, on its chain.
    private static Model PartyDiscoBall()
    {
        var m = new Model();
        const int sides = 12, rings = 8; const float R = 340;
        var lit = new[] { GCyan, GMagenta, GYellow, GGreen };
        for (var i = 0; i < rings; i++)
            for (var k = 0; k < sides; k++)
            {
                Vector3 P(int ring, int side)
                {
                    var t = ring * MathF.PI / rings; var a = side * 2 * MathF.PI / sides;
                    return new Vector3(R * MathF.Sin(t) * MathF.Cos(a), R * MathF.Cos(t), R * MathF.Sin(t) * MathF.Sin(a));
                }
                var q = i == 0 ? new[] { P(0, 0), P(1, k + 1), P(1, k) } : i == rings - 1 ? new[] { P(i, k), P(i, k + 1), P(rings, 0) } : new[] { P(i, k), P(i, k + 1), P(i + 1, k + 1), P(i + 1, k) };
                var paint = (i * 5 + k * 3) % 7 == 0 ? lit[(i + k) % lit.Length] : PSilver;
                m.Face(q, paint, Vector3.Zero);
            }
        m.Bar(new Vector3(0, R - 20, 0), new Vector3(0, R + 900, 0), 30, Iron);
        return m;
    }

    // A bubble of the brew: a little glowing green ball.
    private static Model PartyBubble()
    {
        var m = new Model();
        m.Lathe(new (double, double)[] { (0, -70), (55, -50), (75, 0), (55, 50), (0, 70) }, 6, _ => Brew, false, false);
        return m;
    }

    // ---- the decor's places: what, where (island cells), its floor's height, its turn (0 faces +z / south, 1024 east, 2048 north, 3072 west),
    // and, solid, its box (half its width across x and z as it stands turned 0, its top) -- Twinsen walks into it, not through it
    private sealed record Piece(string What, double X, double Z, double Y, int Turn, double HalfX = 0, double HalfZ = 0, double Top = 0, double Reach = 900);
    // the dance floor's middle (island cells): six tiles of two cells across, seven along
    private const double DanceX = 591.75, DanceZ = 281.75;

    private static List<Piece> PartyPieces()
    {
        double F = PartyFloorY, T = PartyFloorY + PartyTableTop, D = PartyDaisY, P = PartyPassageY;
        var list = new List<Piece>
        {
            // the way there: a jack-o'-lantern at the side passage's mouth off the long corridor (the hint), one by its stair, candles on the
            // gallery and the bridge, two lanterns either side of the doorway on each side
            new("lantern", 551.5, 446.2, P, 0, Reach: 800), new("lantern", 567.5, 370.5, P, 3072, Reach: 800), new("candles", 550.2, 357.0, F, 0, Reach: 500),
            new("lantern", 566.6, 343.8, F, 0, Reach: 800), new("lantern", 577.4, 343.8, F, 0, Reach: 800),
            new("candles", 569.7, 324.5, F, 0, Reach: 500), new("candles", 574.3, 330.5, F, 2048, Reach: 500), new("candles", 569.7, 336.5, F, 1024, Reach: 500),
            new("lantern", 566.8, 316.0, F, 0, Reach: 800), new("lantern", 577.6, 316.0, F, 0, Reach: 800),
            // the vampire at the door, greeting his guests
            new("vampire", 572.0, 308.5, F, 0, 330, 330, 1700, 900),
            // the witch at her cauldron, its fire, her cat, her broom
            new("cauldron", 551.4, 287.6, F + 180, 0, 650, 650, 1100, 900), new("fire", 551.4, 287.6, F, 0, Reach: 700),
            new("witch", 551.4, 285.4, F, 0, 380, 380, 1800, 900), new("cat", 553.4, 292.5, F, 3072, Reach: 500), new("broom", 548.9, 279.5, F, 1024, Reach: 900),
            new("cornerweb", 548.3, 277.2, F + 3300, 0, Reach: 2000),
            // the dance floor, Frankenstein's monster dancing by it
            // (the floor between the wall down the middle of the hall's west half, x 581-583, and the pillar at x 599)
            new("dancefloor", DanceX, DanceZ, F, 0, Reach: 5000), new("frank", 603.6, 287.8, F, 3072, 360, 300, 2150, 900),
            new("lantern", 584.6, 274.4, F, 1024, Reach: 800), new("lantern", 598.6, 274.4, F, 3072, Reach: 800),
            new("giantlantern", 606.6, 279.2, F, 3584, 1300, 1300, 2000, 1700),
            // the banquet: two tables, the punch, the cake, the treats, candelabras; the mummy at its end, a zombie after the punch
            new("table", 605.5, 310.6, F, 0, 2900, 560, 690, 3000), new("table", 616.9, 310.6, F, 0, 2900, 560, 690, 3000),
            new("candelabra", 600.6, 310.6, T, 0, Reach: 600), new("punch", 603.4, 310.5, T, 0, Reach: 800), new("cookies", 607.0, 310.9, T, 0, Reach: 500),
            new("goblets", 609.0, 310.2, T, 0, Reach: 500), new("cake", 611.6, 310.6, T, 0, Reach: 700), new("candycorn", 614.6, 310.3, T, 0, Reach: 500),
            new("skull", 616.9, 310.8, T, 0, Reach: 500), new("goblets", 618.9, 310.9, T, 1400, Reach: 500), new("smalllantern", 620.4, 310.4, T, 0, Reach: 500),
            new("candelabra", 621.9, 310.6, T, 0, Reach: 600),
            new("mummy", 626.4, 311.2, F, 3072, 300, 300, 1800, 900), new("zombie", 596.8, 307.4, F, 1024, 300, 300, 1700, 900),
            new("balloons1", 598.4, 314.2, F, 0, Reach: 900), new("balloons2", 624.6, 306.6, F, 0, Reach: 900),
            // the graveyard (the north-east corner): gravestones, crosses, candles, the grim reaper, a zombie out of a grave
            new("grave", 619.6, 275.2, F, 0, 520, 200, 1350, 800), new("grave", 624.2, 276.4, F, 200, 520, 200, 1350, 800), new("grave", 628.4, 275.0, F, -150, 520, 200, 1350, 800),
            new("grave", 620.6, 282.6, F, 120, 520, 200, 1350, 800), new("cross", 625.4, 281.6, F, 0, 300, 150, 1800, 800), new("grave", 628.6, 287.0, F, -200, 520, 200, 1350, 800),
            new("cross", 619.4, 289.4, F, 300, 300, 150, 1800, 800), new("candles", 622.8, 279.4, F, 0, Reach: 500), new("candles", 627.2, 291.6, F, 1500, Reach: 500),
            new("reaper", 623.4, 295.8, F, 3072, 380, 380, 2000, 1300), new("zombie2", 627.6, 296.8, F, 3072, 320, 320, 1700, 900),
            new("cat_arched", 619.6, 298.6, F, 1024, Reach: 600),
            // the north side: the coffin against the wall, the werewolf howling, a pile of pumpkins, black cats, cobwebs, spiders
            new("coffin", 575.4, 259.9, F, 0, 450, 200, 1950, 1100), new("wolf", 590.0, 262.6, F, 0, 340, 340, 1900, 900),
            new("pumpkins", 563.6, 261.6, F, 0, 800, 800, 800, 1000), new("cat", 583.2, 261.4, F, 0, Reach: 500), new("cat_arched", 597.4, 262.2, F, 3072, Reach: 600),
            new("lantern", 568.6, 260.6, F, 600, Reach: 800), new("lantern", 611.0, 260.4, F, 0, Reach: 800), new("candles", 580.0, 260.4, F, 0, Reach: 500),
            new("cornerweb", 548.4, 259.4, F + 3600, 1024, Reach: 2000), new("cornerweb", 640.6, 259.4, F + 3600, 2048, Reach: 2000),
            new("spider", 562.0, 266.0, F + 2600, 0, Reach: 700), new("spider", 634.0, 300.0, F + 2400, 0, Reach: 700), new("spider", 605.0, 297.0, F + 2800, 0, Reach: 700),
            // the east side and the dais: the band's (the race draws them) lanterns at its lip, balloons, a pumpkin
            new("lantern", 645.0, 261.0, D, 3072, Reach: 800), new("lantern", 645.0, 281.0, D, 3072, Reach: 800), new("balloons3", 651.6, 259.9, D, 0, Reach: 900),
            new("balloons4", 651.6, 283.2, D, 0, Reach: 900), new("lantern", 638.0, 312.0, F, 3072, Reach: 800), new("cat", 638.6, 296.0, F, 3072, Reach: 500),
            new("balloons5", 637.8, 276.0, F, 0, Reach: 900),
            // the alcove's own fire bowls (LBA1's) are left; pumpkins in its steps
            new("lantern", 561.6, 309.8, F, 0, Reach: 800),
            // the west half's middle, between its walls: LBA1's skeletons -- one slumped against the wall, one lying where it fell --, a pile
            // of pumpkins, candles, an arched cat
            new("skeleton_slumped", 563.5, 277.6, F, 0, Reach: 1100), new("skeleton_lying", 571.0, 284.6, F, 900, Reach: 1200),
            new("pumpkins", 576.4, 286.0, F, 300, 800, 800, 800, 1000), new("candles", 568.4, 287.6, F, 0, Reach: 500),
            new("cat_arched", 565.2, 283.4, F, 1024, Reach: 600), new("lantern", 579.0, 277.6, F, 3072, Reach: 800),
        };
        // the bunting, across the hall over the dance floor, over the banquet, along the dais
        list.Add(new("bunting:560,277,614,277," + (F + 3400) + ",700", 587, 277, F + 3400, 0, Reach: 14000));
        list.Add(new("bunting:560,288,614,288," + (F + 3400) + ",700", 587, 288, F + 3400, 0, Reach: 14000));
        list.Add(new("bunting:577,268,606,295," + (F + 3700) + ",500", 591.5, 281.5, F + 3700, 0, Reach: 10000));
        list.Add(new("bunting:577,295,606,268," + (F + 3700) + ",500", 591.5, 281.5, F + 3700, 0, Reach: 10000));
        list.Add(new("bunting:598,306,628,306," + (F + 2600) + ",400", 613, 306, F + 2600, 0, Reach: 8000));
        list.Add(new("bunting:645,258,645,285," + (D + 2600) + ",500", 645, 271.5, D + 2600, 0, Reach: 7500));
        return list;
    }

    private static Model? PartyModel(string what)
    {
        if (what.StartsWith("bunting:"))
        {
            var v = what[8..].Split(',').Select(s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var mid = new Vector3((float)((v[0] + v[2]) / 2 * 512), 0, (float)((v[1] + v[3]) / 2 * 512));
            return PartyBunting(new Vector3((float)(v[0] * 512), 0, (float)(v[1] * 512)) - mid, new Vector3((float)(v[2] * 512), 0, (float)(v[3] * 512)) - mid, (float)v[5], (int)v[0]);
        }
        return what switch
        {
            "lantern" => Lantern(640, 900),
            "smalllantern" => Lantern(200, 270, 12, 6),
            "giantlantern" => Lantern(1500, 2000),
            "candles" => Candles(),
            "grave" => Grave(),
            "cross" => Cross(),
            "cauldron" => Cauldron(),
            "fire" => PartyFire(),
            "witch" => PartyWitch(),
            "vampire" => PartyVampire(),
            "frank" => PartyFrank(),
            "mummy" => PartyMummy(),
            "wolf" => PartyWolf(),
            "reaper" => PartyReaper(),
            "zombie" => PartyZombie(false),
            "zombie2" => PartyZombie(true),
            "cat" => PartyCat(false),
            "cat_arched" => PartyCat(true),
            "coffin" => PartyCoffin(),
            "broom" => PartyBroom(),
            "table" => PartyTable(5760),
            "punch" => PartyPunch(),
            "cake" => PartyCake(),
            "cookies" => PartyTreats(0),
            "candycorn" => PartyTreats(1),
            "goblets" => PartyTreats(2),
            "skull" => PartyTreats(3),
            "candelabra" => PartyCandelabra(),
            "pumpkins" => PartyPumpkins(),
            "dancefloor" => PartyDanceFloor(6, 7, 1024),
            "skeleton_slumped" => SkeletonModel(SkeletonPose.Slumped),
            "skeleton_lying" => SkeletonModel(SkeletonPose.Lying),
            "cornerweb" => CobwebModel(Kind.CobwebCornerBig, 1),
            "spider" => CobwebModel(Kind.Spider, 0),
            _ when what.StartsWith("balloons") => PartyBalloons(what[^1] - '0'),
            _ => null,
        };
    }

    // ---- into the island: the party's decor, drawn (and solid where a guest would walk into it), a body for each kind -- after every other
    // body of the island's (RaceTrackReport.NewBodies, numbered from NewBodyBase: the Halloween decor's and the giant webs' come first)
    public static void PlaceParty(IslandFile island, RaceTrackPlan plan, RaceTrackOptions o, RaceTrackReport report)
    {
        if (o.Island != RaceTrackIsland.Temple || plan.Halloween is null) return;
        if (o.NewBodyBase < 0) { report.Notes.Add("WARNING: the party: no place for its bodies was prepared (the island's OBL wasn't counted)"); return; }
        var bodies = new Dictionary<string, int>();
        int placed = 0, solid = 0, left = 0, faces = 0;
        var cubes = new Dictionary<(int, int), int>();
        foreach (var p in PartyPieces())
        {
            if (!bodies.TryGetValue(p.What, out var body))
            {
                var model = PartyModel(p.What);
                body = model is null ? -1 : o.NewBodyBase + report.NewBodies.Count;
                if (model is not null)
                {
                    try { report.NewBodies.Add(model.Write()); }
                    catch (InvalidOperationException e) { throw new InvalidOperationException($"The party's {p.What}: {e.Message}"); }
                    faces += model.FaceCount;
                }
                bodies[p.What] = body;
            }
            if (body < 0) { left++; continue; }
            double wx = p.X * 512, wz = p.Z * 512;
            var y = (int)Math.Round(p.Y);
            var (n, full) = Palace.BrickBuilding.AddToCubes(island, body, p.Turn & 4095, wx, y, wz, wx - p.Reach, wx + p.Reach, y - 100, Palace.BrickBuilding.NoBoxTop,
                wz - p.Reach, wz + p.Reach, IslandDecors.MaxPerCubeRaceTrack);
            if (n > 0) placed++; else left++;
            var key = ((int)Math.Floor(wx / 32768), (int)Math.Floor(wz / 32768));
            cubes[key] = cubes.GetValueOrDefault(key) + n;
            if (p.Top <= 0) continue;
            // (its box: turned a quarter, its halves swapped)
            var quarter = ((p.Turn & 4095) + 512) / 1024 % 2 == 1;
            double hx = quarter ? p.HalfZ : p.HalfX, hz = quarter ? p.HalfX : p.HalfZ;
            if (Palace.BrickBuilding.AddToCubes(island, TempleIsland.EmptyBody, 0, wx, y, wz, wx - hx, wx + hx, y, y + (int)Math.Round(p.Top), wz - hz, wz + hz,
                    IslandDecors.MaxPerCubeRaceTrack).Placed > 0) solid++;
        }
        // the hall's open south side: LBA1 draws no wall on the side its camera looks from, and the hall's floor ends there over the dark --
        // boxes nothing draws keep a guest from walking off it (but at the bridge's doorway and the stair down)
        var rails = 0;
        foreach (var (rx0, rx1) in new[] { (545.0, 568.9), (575.1, 584.0), (592.0, 642.0) })
            rails += Palace.BrickBuilding.AddToCubes(island, TempleIsland.EmptyBody, 0, (rx0 + rx1) / 2 * 512, (int)PartyFloorY, 321.8 * 512, rx0 * 512, rx1 * 512,
                (int)PartyFloorY, (int)PartyFloorY + 1800, 321.6 * 512, 322.0 * 512, IslandDecors.MaxPerCubeRaceTrack).Placed;
        report.Notes.Add($"the party (LBA1 scene 41's great hall): {placed} pieces of decor, {solid} of them solid, {bodies.Count(b => b.Value >= 0)} bodies ({faces} faces), " +
                         $"{rails} boxes along its open south side" + (left > 0 ? $"; {left} left out (their cubes full)" : "") +
                         $"; per cube (the party's of all the cube's decor) {string.Join(", ", cubes.OrderBy(c => c.Key).Select(c => $"({c.Key.Item1},{c.Key.Item2}) {c.Value} of " + (island.CubeAt(c.Key.Item1, c.Key.Item2)?.Decors.Count ?? 0)))}");
        report.OnFoot = true;
    }

    // ---- the guests the race-track mode draws (spook= flags 32): after Install (the Halloween spooks' bodies are in BODY.HQR then -- the
    // ghosts, the witch and the flying rats are theirs), the skeletons the risers' (the rabbibunny skeleton made lighter, by its entity),
    // the band the game's own characters with their own animations, the disco ball and the bubbles bodies of their own (PartyInstall, at the
    // very end of the build: by the rabbibunny skeleton's entity, PartyDiscoBody and PartyBubbleBody)
    public const int PartyDiscoBody = 60, PartyBubbleBody = 61;
    public const int DrummerBody = 301, OldRabbiGuitarBody = 251, SpheroGuitarBody = 223;
    // flags: 2 round and round its way, 4 animated, 16 by entity, 32 a party guest, 64 on the floor, 128 swaying, 256 standing facing its
    // second point, 512 spinning
    public static List<string> PartyGuests(string gameDirectory, RaceTrackReport report)
    {
        var log = new List<string>();
        if (!report.OnFoot) return log;
        RaceTrackReport.Spook? Of(string kind) => report.Spooks.FirstOrDefault(s => s.Kind == kind && s.Body >= 0);
        var guests = new List<RaceTrackReport.Spook>();
        static List<(double X, double Z)> Way(params double[] xz) => Enumerable.Range(0, xz.Length / 2).Select(k => (xz[2 * k], xz[2 * k + 1])).ToList();
        static List<(double X, double Z)> Ring(double cx, double cz, double rx, double rz, int n, double phase = 0) =>
            Enumerable.Range(0, n).Select(k => (cx + rx * Math.Cos(phase + k * 2 * Math.PI / n), cz + rz * Math.Sin(phase + k * 2 * Math.PI / n))).ToList();
        double F = PartyFloorY, D = PartyDaisY;
        // the skeletons: a conga round the dance floor (their walk), two dancing on the spot (their swipe, swaying)
        var rabbi = RabbiEntity(gameDirectory);
        var walk = CreatureAnim(gameDirectory, RabbiSkeletonBody, 1);
        var dance = CreatureAnim(gameDirectory, RabbiSkeletonBody, 148);
        var swipe = CreatureAnim(gameDirectory, RabbiSkeletonBody, RabbiSwipe);
        if (rabbi >= 0 && walk >= 0)
            for (var k = 0; k < 5; k++)
                guests.Add(new("party skeleton", 2 | 4 | 32 | 64, F, Ring(DanceX, DanceZ, 4.6, 5.4, 16, k * 2 * Math.PI / 5), RiserBody, walk, -1, rabbi));
        if (rabbi >= 0 && (dance >= 0 || swipe >= 0))
        {
            guests.Add(new("party skeleton", 4 | 32 | 64 | 128 | 256, F, Way(DanceX, DanceZ, DanceX, DanceZ + 8), RiserBody, dance >= 0 ? dance : swipe, -1, rabbi));
            guests.Add(new("party skeleton", 4 | 32 | 64 | 128 | 256, F, Way(DanceX - 2.6, DanceZ + 2.4, DanceX, DanceZ), RiserBody, swipe >= 0 ? swipe : dance, -1, rabbi));
            guests.Add(new("party skeleton", 4 | 32 | 64 | 128 | 256, F, Way(DanceX + 2.6, DanceZ - 2.4, DanceX, DanceZ), RiserBody, swipe >= 0 ? swipe : dance, -1, rabbi));
        }
        // the band on the dais, facing the hall (west)
        foreach (var (body, generic, z) in new[] { (DrummerBody, 0, 271.0), (OldRabbiGuitarBody, 28, 264.5), (SpheroGuitarBody, 28, 277.5) })
        {
            var anim = CreatureAnim(gameDirectory, body, generic);
            if (anim < 0) anim = CreatureAnim(gameDirectory, body, 0);
            if (anim >= 0) guests.Add(new("party band", 4 | 32 | 64 | 256, D, Way(649.0, z, 640.0, z), body, anim));
        }
        // the ghosts drifting over the dance floor and the banquet; the flying rats round high over the hall; a witch on her broomstick
        if (Of("ghost") is { } ghost)
        {
            guests.Add(ghost with { Kind = "party ghost", Flags = 2 | 32, Y = F + 2300, Way = Ring(DanceX, DanceZ, 5, 5, 12), Action = -1, Entity = -1 });
            guests.Add(ghost with { Kind = "party ghost", Flags = 2 | 32, Y = F + 2900, Way = Ring(DanceX - 2, DanceZ + 2, 10, 7, 12, Math.PI), Action = -1, Entity = -1 });
            guests.Add(ghost with { Kind = "party ghost", Flags = 32, Y = F + 2000, Way = Way(600, 308, 624, 308), Action = -1, Entity = -1 });
        }
        if (Of("rat") is { } rat)
            for (var k = 0; k < 3; k++)
                guests.Add(rat with { Kind = "party rat", Flags = (rat.Anim >= 0 ? 4 : 0) | 2 | 32, Y = F + 4600 + 500 * k, Way = Ring(595, 286, 16 - 3 * k, 10 - k, 14, k * 2.1), Action = -1, Entity = -1 });
        if (Of("witch") is { } witch)
            guests.Add(witch with { Kind = "party witch", Flags = 2 | 32, Y = F + 6200, Way = Ring(594, 287, 30, 18, 18), Action = -1, Entity = -1 });
        // the disco ball over the dance floor, the bubbles out of the cauldron (their bodies at the end of the build: PartyInstall)
        if (rabbi >= 0)
        {
            guests.Add(new("party disco ball", 32 | 64 | 512, F + 3900, Way(DanceX, DanceZ), PartyDiscoBody, -1, -1, rabbi));
            foreach (var (x, z, up) in new[] { (551.0, 287.2, 1000.0), (551.9, 287.9, 1180.0), (551.3, 288.3, 900.0) })
                guests.Add(new("party bubble", 32, F + 180 + up, Way(x, z), PartyBubbleBody, -1, -1, rabbi));
        }
        report.Spooks.AddRange(guests);
        log.Add($"the party's guests (drawn by the race while Twinsen is about it): {guests.Count} ({string.Join(", ", guests.GroupBy(g => g.Kind[6..]).Select(g => $"{g.Count()} {g.Key}"))})");
        return log;
    }

    // ---- into BODY.HQR at the very end of the build (no other body moves): the disco ball and a bubble, the rabbibunny skeleton's entity's
    // bodies PartyDiscoBody and PartyBubbleBody (RESS.HQR 44), as the risers' (InstallRisers) -- the race file names them by the entity.
    public static List<string> PartyInstall(string gameDirectory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var table = HqrArchive.Open(ressPath).Read(44);
        var parsed = Lba2EntityTable.Parse(table);
        if (parsed.KindForBody(RabbiSkeletonBody) is not { } kind) return new() { "WARNING: the party: no entity has the rabbibunny skeleton -- no disco ball, no bubbles" };
        if (parsed.Entities[kind.Entity].Bodies.Any(b => b.Generic is PartyDiscoBody or PartyBubbleBody))
            return new() { $"WARNING: the party: entity {kind.Entity} already has body {PartyDiscoBody} or {PartyBubbleBody} -- none made" };
        var racerHeader = Body.Read(HqrArchive.Open(bodyPath).Read(RaceTrackBaldinoCar.RacerBody), 2).Header;
        var bodies = File.ReadAllBytes(bodyPath);
        var first = HqrArchive.CountEntries(bodyPath);
        bodies = HqrWriter.AppendEntry(bodies, HqrWriter.StoredEntry(PartyDiscoBall().WriteMoving(racerHeader)));
        bodies = HqrWriter.AppendEntry(bodies, HqrWriter.StoredEntry(PartyBubble().WriteMoving(racerHeader)));
        File.WriteAllBytes(bodyPath, bodies);
        table = RaceTrackBaldinoCar.WithBody(table, kind.Entity, PartyDiscoBody, first);
        table = RaceTrackBaldinoCar.WithBody(table, kind.Entity, PartyBubbleBody, first + 1);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        return new() { $"the party: its disco ball BODY.HQR {first} and bubble {first + 1}, entity {kind.Entity}'s bodies {PartyDiscoBody} and {PartyBubbleBody}" };
    }
}
