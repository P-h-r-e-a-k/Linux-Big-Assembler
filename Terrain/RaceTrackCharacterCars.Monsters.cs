using System.IO;
using System.Numerics;
using System.Text.Json;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// Halloween's monsters' own cars, the Temple of Bú's opponents (the user, 2026-10-18: "Let's also create different opponents for Twinsen, a
// vampire driving a coffin, A witch flying on a broom (so no wheels), A zombie driving a hurst") --
//
//   the vampire's coffin     a six-sided coffin of dark wood on four wheels with brass hubs, its lid's foot end closed over the front as
//                            the bonnet (a brass cross on it, two candle lamps), its head end open and lined with red velvet: the vampire
//                            sitting up in it at the wheel -- pale, black hair slicked back to a widow's peak, red eyes, fangs, a tall
//                            black collar lined red behind his head, his black cape lined red streaming back over the coffin; brass
//                            handles down its sides, and at its tail six brass organ pipes standing up for exhausts
//   the witch's broomstick   no wheels: a gnarled broomstick, its golden bristles bound with red twine, flying a little over the road
//                            (RaceDriver.Hover: the race-track mode bobs it gently, RACEMOD.CPP opponent_hover=), its shadow under it --
//                            the witch astride it, leaning into the wind: green as the temple's own witches, her ginger hair streaming,
//                            a tall black hat with an orange band and a gold buckle, a purple robe and cape, orange-and-black striped
//                            stockings, pointed boots; her black cat riding on the bristles, its yellow eyes; green sparks in her wake
//   the zombie's hearse      a long black 1930s hearse on whitewall wheels under flowing wings and running boards: a long bonnet with a
//                            chrome grille and lamps, a steep windscreen, the tall rear compartment with curtained windows and chrome
//                            landau bars, a wreath on its back door, chrome along it -- the zombie leaning out of the driver's window,
//                            grey-green, sunken eyes glowing, his teeth bared, one ear gone and his skull showing, his rotting arm reaching
//                            out ahead of the car
//
// Bodies of the racer's entity (157: its driving animation bounces them and turns the wheels, as it does the other cars; the broomstick has
// none to turn) made with the cars' kit (CarMesh) as the pumpkin F1 is -- their drivers made here too, in the cars' low-poly style (no body
// of the game's is a vampire, a witch on a broom or a zombie) -- each with a car's box (MonsterBox: the broomstick's made one), so they
// meet the other cars, the flag marshal and the grid as every car does. Not characters' cars (All): they go into a game folder at the very end of every build
// (InstallMonsters, after everything else: no body before them moves), with their half-size copies (the lightning spell), as race cars
// VampireNumber, WitchNumber and ZombieNumber of RACECARS.JSON -- raced on the Temple of Bú (RaceDriver.Temple), and anyone's to drive.
// Their colours are those every island's palette keeps alike (the Temple's, RESS 40, changes a few slots only: none used here alone).
internal static partial class RaceTrackCharacterCars
{
    // race cars 222-224 (RACECARS.JSON: after the toy dump truck's 221), the racer entity's bodies 66-68 (their half-size copies 166-168)
    public const int VampireNumber = 222, WitchNumber = 223, ZombieNumber = 224;
    public const int VampireGeneric = 66, WitchGeneric = 67, ZombieGeneric = 68;
    public const string TempleOfBu = "The Temple of Bú";
    // (the drivers are made here: no BODY.HQR character -- the Twinsen they are given is not used)
    public static readonly Car Coffin = new("The vampire's coffin", "the vampire", TwinsenBody, VampireGeneric, (_, racer) => BuildCoffin(racer), TempleOfBu);
    public static readonly Car Broomstick = new("The witch's broomstick", "the witch", TwinsenBody, WitchGeneric, (_, racer) => BuildBroomstick(racer), TempleOfBu);
    public static readonly Car Hearse = new("The zombie's hearse", "the zombie", TwinsenBody, ZombieGeneric, (_, racer) => BuildHearse(racer), TempleOfBu);
    public static Car[] Monsters => new[] { Coffin, Broomstick, Hearse };
    private static int MonsterNumber(Car car) => car == Coffin ? VampireNumber : car == Broomstick ? WitchNumber : ZombieNumber;

    // The broomstick's bob over its line (RaceDriver.Hover, RACEMOD.CPP opponent_hover=): up and down this far (units), once in this long (ms).
    public const int WitchBob = 45, WitchBobMs = 1600;

    // A body's box (BODY.HQR header: x, y and z, each from and to) is what the engine meets other actors with -- Twinsen's car runs into an
    // opponent's by it -- and Body.Write makes it the body's own reach. The coffin's and the hearse's are a car's (about the old Halloween
    // opponents' and the racer's: 0.9-1.3 cells long, 0.8-0.9 across); the broomstick's own would be a sliver (the stick and the witch,
    // 460 across) reaching back to her sparks, so it gets a car's: as wide as the coffin's, from the bristles' end to the handle's tip --
    // no car drives through her, none is stopped a cell behind her.
    private static readonly int[] WitchBox = { -461, 461, 0, 1203, -780, 670 };
    private static int[]? MonsterBox(Car car) => car == Broomstick ? WitchBox : null;

    // ---------------------------------------------------------------------------------------------------------------- shared parts

    // A ring of `n` points round the way `d` goes, `r` across (`r2` the other way), its first point the way `towards` is from it.
    private static int[] HmRound(CarMesh m, int bone, Vector3 centre, Vector3 d, float r, int n, Vector3 towards, float r2 = -1)
    {
        d = Vector3.Normalize(d);
        var u = Vector3.Normalize(towards - d * Vector3.Dot(towards, d));
        return m.Loop(bone, centre, u * r, Vector3.Cross(d, u) * (r2 < 0 ? r : r2), n);
    }

    // The axis least along a direction (a limb's rings' first points: the same side of each ring all along it).
    private static Vector3 HmAcross(Vector3 d)
    {
        d = Vector3.Normalize(d);
        float x = MathF.Abs(d.X), y = MathF.Abs(d.Y), z = MathF.Abs(d.Z);
        return x <= y && x <= z ? Vector3.UnitX : y <= z ? Vector3.UnitY : Vector3.UnitZ;
    }

    // A limb through the points `at`, `r` across at each, `n` sides, each stretch in `colour(stretch)`; returns its rings.
    private static List<int[]> HmLimb(CarMesh m, int bone, Vector3[] at, float[] r, int n, Func<int, int> colour)
    {
        var towards = HmAcross(at[^1] - at[0]);
        var rings = new List<int[]>();
        for (var i = 0; i < at.Length; i++)
        {
            var d = i == 0 ? at[1] - at[0] : i == at.Length - 1 ? at[i] - at[i - 1] : Vector3.Normalize(at[i] - at[i - 1]) + Vector3.Normalize(at[i + 1] - at[i]);
            rings.Add(HmRound(m, bone, at[i], d, r[i], n, towards));
        }
        for (var i = 0; i + 1 < at.Length; i++) { var s = i; m.Skin(rings[i], rings[i + 1], _ => colour(s), (at[i] + at[i + 1]) / 2); }
        return rings;
    }

    // A body's rings up it: each (centre, half width, half depth), `n` points from the front's right round (phase half a step: panel n - 1
    // is the front's middle, n / 2 - 1 the back's, n / 4 - 1 the +x side's); each band's panels in `colour(band, panel)`.
    private static int[][] HmStack(CarMesh m, int bone, (Vector3 C, float Rx, float Rz)[] rings, int n, Func<int, int, int> colour)
    {
        var points = rings.Select(r => m.Loop(bone, r.C, new Vector3(0, 0, r.Rz), new Vector3(r.Rx, 0, 0), n, 0.5f)).ToArray();
        for (var i = 0; i + 1 < points.Length; i++) { var band = i; m.Skin(points[i], points[i + 1], k => colour(band, k), (rings[i].C + rings[i + 1].C) / 2); }
        return points;
    }

    // A head's ring, in its own coordinates (the chin's middle at 0, up y, the face towards +z, its right +x): its height, half width, how
    // far it reaches forward and back of its middle, and where its middle is (z).
    private readonly record struct HmRing(float Y, float Rx, float Front, float Back, float Zc);
    // (the rings' points round from the face's right: panel k between point k and k + 1 -- 0 the right cheek, 1 the right side, 2 the right
    // of the back, 3 the back, 4 its left, 5 the left side, 6 the left cheek, 7 the face's middle)
    private static readonly float[] HmAngles = { 28f, 75, 120, 160, 200, 240, 285, 332 };
    private const int HmFace = 7;
    private static Vector3 HmOn(HmRing r, float degrees)
    {
        var a = degrees * MathF.PI / 180;
        return new Vector3(r.Rx * MathF.Sin(a), r.Y, r.Zc + (MathF.Cos(a) >= 0 ? r.Front : r.Back) * MathF.Cos(a));
    }

    // A head from its rings, placed by `place` (bone 13): each band's panels in `colour(band, panel)` (-1: left out, the caller's), its chin
    // and crown closed. Returns its points, ring by ring.
    private static int[][] HmHead(CarMesh m, Func<Vector3, Vector3> place, HmRing[] rings, Func<int, int, int> colour, int chin, int crown, Vector3 top)
    {
        var points = rings.Select(r => HmAngles.Select(a => m.P(13, place(HmOn(r, a)))).ToArray()).ToArray();
        var middle = place(new Vector3(0, (rings[0].Y + rings[^1].Y) / 2, 0));
        for (var b = 0; b + 1 < rings.Length; b++)
            for (var k = 0; k < 8; k++)
            {
                var c = colour(b, k);
                if (c >= 0) m.Out(new[] { points[b][k], points[b][(k + 1) % 8], points[b + 1][(k + 1) % 8], points[b + 1][k] }, c, middle);
            }
        m.Cap(points[0], m.P(13, place(new Vector3(0, rings[0].Y - 8, rings[0].Zc + rings[0].Front * 0.4f))), chin, middle);
        m.Cap(points[^1], m.P(13, place(top)), crown, middle);
        return points;
    }

    // Bones 3-12 (the wheels' struts, hubs and wheels) for a car without wheels: a point each, at `at` on the hull, each turning at its
    // parent's (the racer's animations turn them; nothing hangs from them).
    private static void NoWheels(CarMesh m, Vector3 at)
    {
        var hull = m.P(2, at);
        foreach (var (strut, hub, wheel) in new[] { (3, 4, 5), (6, 7, 8), (9, -1, 10), (11, -1, 12) })
        {
            var s = m.P(strut, at); m.Pivot(strut, hull);
            if (hub >= 0) { var h = m.P(hub, at); m.Pivot(hub, s); m.P(wheel, at); m.Pivot(wheel, h); }
            else { m.P(wheel, at); m.Pivot(wheel, s); }
        }
    }

    // The four wheels as Wheels makes them, with a white band round each tyre's wall (whitewalls) in `band`.
    private static void BandedWheels(CarMesh m, WheelLook look, Axle front, Axle rear, int band)
    {
        Wheels(m, look, front, rear);
        foreach (var (side, wheelBone, isRear) in new[] { (1f, 5, false), (-1f, 8, false), (-1f, 10, true), (1f, 12, true) })
        {
            var axle = isRear ? rear : front;
            var face = axle.Hub with { X = side * axle.Hub.X } + new Vector3(side * (axle.Width / 2 + 3), 0, 0);
            const int N = 6;
            var ring = Enumerable.Range(0, N).Select(k => { var a = (k + 0.5f) * MathF.Tau / N; return m.P(wheelBone, face + new Vector3(0, axle.Radius * 0.8f * MathF.Cos(a), axle.Radius * 0.8f * MathF.Sin(a))); }).ToArray();
            for (var k = 0; k < N; k++) m.Line(ring[k], ring[(k + 1) % N], band);
        }
    }

    // A point of a quad (corners a, b along one edge, d, c along the other: a-b-c-d round it) at shares s along a-b and t from a-b to d-c,
    // `out` units off it towards `outward`.
    private static Vector3 HmOnQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float s, float t, float off = 0, Vector3 outward = default)
        => Vector3.Lerp(Vector3.Lerp(a, b, s), Vector3.Lerp(d, c, s), t) + (outward == default ? Vector3.Zero : Vector3.Normalize(outward) * off);

    // ================================================================================================================ the vampire's coffin

    // The coffin's wood (the dark browns, half the light), the lid a step lighter, its mouldings' lines; the red velvet inside; brass; the
    // candle lamps and the tail lights.
    private const int CfWood = 21, CfLid = 22, CfMoulding = 27, CfDash = 18, CfVelvet = 67, CfFloor = 66, CfBrass = 105, CfBrassFlat = 107, CfCandle = 245, CfTail = 249;
    private const float CfWoodShare = 0.5f, CfVelvetShare = 0.3f, CfBrassShare = 0.5f;
    // The vampire's: pale skin, black hair and coat (a little of the light), his shirt's white, the cape's and collar's black and red (unlit),
    // his eyes, his fangs, his lips, the cravat and the medallion.
    private const int VpSkin = 59, VpHair = 48, VpCoat = 48, VpShirt = 60, VpCapeOut = 49, VpCapeIn = 68, VpHem = 72, VpEye = 249, VpFang = 63, VpLips = 66,
        VpCravat = 70, VpMedal = 107, VpHand = 61;
    private const float VpSkinShare = 0.4f, VpCoatShare = 0.3f;

    // The coffin's half width at the top of its walls at z (its six-sided plan: narrow at its foot -- the nose --, widest at the shoulders,
    // narrowing again to its head end, the tail); its walls lean out a little.
    private const float CfFoot = 640, CfShoulder = -170, CfHead = -640, CfBottom = 180, CfTop = 455, CfLidEnd = 60;
    private static float CfHalf(float z) => z >= CfShoulder ? 150 + 150 * (CfFoot - z) / (CfFoot - CfShoulder) : 300 - 85 * (CfShoulder - z) / (CfShoulder - CfHead);

    public static Body BuildCoffin(Body racer)
    {
        var m = new CarMesh(Coffin.Name);
        Roots(m);
        var stations = new[] { CfFoot, CfLidEnd, CfShoulder, CfHead };
        Vector3 Inside(float z) => new(0, 300, z);

        // ---- the walls: outside the wood, all round; inside the open part's red velvet (its own points, a little inside: lit on its own)
        m.Light = CfWoodShare;
        var top = Sides.Select(s => stations.Select(z => m.P(2, new Vector3(s * CfHalf(z), CfTop, z))).ToArray()).ToArray();
        var bottom = Sides.Select(s => stations.Select(z => m.P(2, new Vector3(s * CfHalf(z) * 0.88f, CfBottom, z))).ToArray()).ToArray();
        for (var side = 0; side < 2; side++)
            for (var i = 0; i + 1 < stations.Length; i++)
                m.Out(new[] { bottom[side][i], bottom[side][i + 1], top[side][i + 1], top[side][i] }, CfWood, Inside((stations[i] + stations[i + 1]) / 2));
        m.Out(new[] { bottom[0][0], bottom[1][0], top[1][0], top[0][0] }, CfWood, Inside(0));
        m.Out(new[] { bottom[0][^1], bottom[1][^1], top[1][^1], top[0][^1] }, CfWood, Inside(0));
        for (var i = 0; i + 1 < stations.Length; i++)
            m.Out(new[] { bottom[0][i], bottom[0][i + 1], bottom[1][i + 1], bottom[1][i] }, CfWood, new Vector3(0, 400, (stations[i] + stations[i + 1]) / 2));
        // (the mouldings: lines round the top of the walls and along their feet)
        for (var side = 0; side < 2; side++)
            for (var i = 0; i + 1 < stations.Length; i++) { m.Line(top[side][i], top[side][i + 1], CfMoulding); m.Line(bottom[side][i], bottom[side][i + 1], CfMoulding); }
        m.Line(top[0][^1], top[1][^1], CfMoulding);
        m.Line(bottom[0][0], bottom[1][0], CfMoulding); m.Line(bottom[0][^1], bottom[1][^1], CfMoulding);

        // the velvet: the open part's walls inside, its floor, the tail's inside; a dark wooden board under the lid's end
        m.Light = CfVelvetShare;
        const float Floor = 330;
        var open = stations.Skip(1).ToArray();
        var vTop = Sides.Select(s => open.Select(z => m.P(2, new Vector3(s * (CfHalf(z) - 8), CfTop - 4, z))).ToArray()).ToArray();
        var vFoot = Sides.Select(s => open.Select(z => m.P(2, new Vector3(s * (CfHalf(z) * 0.94f - 8), Floor, z))).ToArray()).ToArray();
        for (var side = 0; side < 2; side++)
            for (var i = 0; i + 1 < open.Length; i++)
                m.In(new[] { vFoot[side][i], vFoot[side][i + 1], vTop[side][i + 1], vTop[side][i] }, CfVelvet, new Vector3(0, 400, (open[i] + open[i + 1]) / 2));
        m.In(new[] { vFoot[0][^1], vFoot[1][^1], vTop[1][^1], vTop[0][^1] }, CfVelvet, new Vector3(0, 400, -300));
        for (var i = 0; i + 1 < open.Length; i++) m.Up(new[] { vFoot[0][i], vFoot[0][i + 1], vFoot[1][i + 1], vFoot[1][i] }, CfFloor);
        m.Light = CfWoodShare;
        m.In(new[] { vFoot[0][0], vFoot[1][0], vTop[1][0], vTop[0][0] }, CfDash, new Vector3(0, 400, -200));

        // ---- the lid's foot end, closed over the front (the bonnet): a slab with bevelled edges, a brass cross on it, the candle lamps
        {
            m.Light = CfWoodShare;
            const float Under = CfTop + 3, Over = 500;
            int[] Outline(float y, float grow, float zFront, float zBack) => new[]
            {
                m.P(2, new Vector3(CfHalf(zFront) + grow, y, zFront)), m.P(2, new Vector3(-CfHalf(zFront) - grow, y, zFront)),
                m.P(2, new Vector3(-CfHalf(zBack) - grow, y, zBack)), m.P(2, new Vector3(CfHalf(zBack) + grow, y, zBack)),
            };
            var low = Outline(Under, 16, CfFoot + 16, CfLidEnd);
            var high = Outline(Over, 2, CfFoot - 4, CfLidEnd + 14);
            var centre = new Vector3(0, 470, 350);
            m.Up(high, CfLid);
            for (var k = 0; k < 4; k++) m.Out(new[] { low[k], low[(k + 1) % 4], high[(k + 1) % 4], high[k] }, CfLid, centre);
            for (var k = 0; k < 4; k++) m.Line(high[k], high[(k + 1) % 4], CfMoulding);
            m.Light = CfBrassShare;
            m.Box(2, new Vector3(0, Over + 7, 340), new Vector3(40, 14, 420), CfBrass);
            m.Box(2, new Vector3(0, Over + 8, 440), new Vector3(230, 14, 40), CfBrass);
            m.Light = 1;
            foreach (var side in Sides)
            {
                // (a candle in a brass holder at each front corner of the lid, its flame the lamp)
                var at = new Vector3(side * 118, Over, 560);
                m.Rod(2, at, at + new Vector3(0, 70, 0), 62);
                m.Ball(2, at + new Vector3(0, 98, 0), 34, CfCandle);
                m.Ball(2, at + new Vector3(0, 6, 0), 26, CfBrassFlat);
            }
        }

        // ---- brass handles down the sides, standing off the walls
        m.Light = CfBrassShare;
        foreach (var side in Sides)
            foreach (var z in new[] { 150f, -110 })
            {
                float h0 = CfHalf(z + 60) + 26, h1 = CfHalf(z - 60) + 26;
                m.Strut(2, new Vector3(side * h0, 320, z + 60), new Vector3(side * h1, 320, z - 60), 18, CfBrass);
                m.Rod(2, new Vector3(side * (h0 - 26), 320, z + 60), new Vector3(side * h0, 320, z + 60), CfBrassFlat);
                m.Rod(2, new Vector3(side * (h1 - 26), 320, z - 60), new Vector3(side * h1, 320, z - 60), CfBrassFlat);
            }

        // ---- the organ pipes at the tail: three a side on a brass bar across the coffin's head end, the middle ones the tallest; their
        // mouths dark; and the tail lights under them
        {
            m.Strut(2, new Vector3(-205, 330, CfHead - 30), new Vector3(205, 330, CfHead - 30), 30, CfBrass);
            foreach (var side in Sides)
                foreach (var (x, height) in new[] { (60f, 740f), (115f, 690f), (170f, 640f) })
                {
                    var foot = new Vector3(side * x, 330, CfHead - 32);
                    var pipe = HmLimb(m, 2, new[] { foot, foot with { Y = height } }, new[] { 24f, 24 }, 5, _ => CfBrass);
                    m.Cap(pipe[1], m.P(2, foot with { Y = height - 30 }), HoleDark, foot with { Y = height - 200 }, unlit: true);
                }
            m.Light = 1;
            foreach (var side in Sides) m.Ball(2, new Vector3(side * 190, 400, CfHead - 8), 30, CfTail);
        }

        // ---- the wheels: black, brass hubs, iron struts
        Wheels(m, new WheelLook(Dark, 24, Black, SoftLight, CfBrass, CfBrassShare, CfBrassFlat),
            new Axle(new Vector3(CfHalf(430) - 12, 290, 430), new Vector3(330, 150, 430), 150, 100),
            new Axle(new Vector3(CfHalf(-430) - 12, 300, -430), new Vector3(385, 175, -430), 175, 120));

        // ---- the vampire (bone 13), sitting up in his coffin at the wheel
        const float SeatZ = -230;
        m.Light = 1;
        m.Pivot(13, m.P(2, new Vector3(0, 380, SeatZ)));
        // his body: a black coat, the shirt's white down its front, his shoulders wide
        m.Light = VpCoatShare;
        var torso = HmStack(m, 13, new[]
        {
            (new Vector3(0, 360, SeatZ), 100f, 70f), (new Vector3(0, 470, SeatZ + 4), 116f, 76f), (new Vector3(0, 560, SeatZ + 6), 140f, 72f),
            (new Vector3(0, 606, SeatZ + 8), 104f, 56f), (new Vector3(0, 626, SeatZ + 10), 46f, 38f),
        }, 8, (band, k) => k == 7 && band >= 1 ? VpShirt : VpCoat);
        m.Light = 1;
        m.Ball(13, new Vector3(0, 612, SeatZ + 62), 20, VpCravat);
        m.Ball(13, new Vector3(0, 560, SeatZ + 80), 16, VpMedal);
        // his arms, black sleeves to the wheel, pale hands on it
        m.Light = VpCoatShare;
        var shoulders = new List<int>();
        foreach (var side in Sides)
        {
            var arm = HmLimb(m, 13, new[] { new Vector3(side * 128, 585, SeatZ + 4), new Vector3(side * 150, 480, SeatZ + 90), new Vector3(side * 84, 516, SeatZ + 182) },
                new[] { 38f, 33, 26 }, 5, _ => VpCoat);
            shoulders.Add(arm[0][0]);
            m.Light = 1;
            m.Ball(13, new Vector3(side * 78, 520, SeatZ + 196), 26, VpHand);
            m.Light = VpCoatShare;
        }
        // the steering wheel in his hands, on its column down to the board under the lid
        m.Light = CfBrassShare;
        {
            var centre = new Vector3(0, 520, SeatZ + 200);
            var across = Vector3.UnitX; var up = Vector3.Normalize(new Vector3(0, 1, -0.45f));
            var outer = Enumerable.Range(0, 8).Select(k => m.P(2, centre + across * 84 * MathF.Cos(k * MathF.Tau / 8) + up * 84 * MathF.Sin(k * MathF.Tau / 8))).ToArray();
            var inner = Enumerable.Range(0, 8).Select(k => m.P(2, centre + across * 66 * MathF.Cos(k * MathF.Tau / 8) + up * 66 * MathF.Sin(k * MathF.Tau / 8))).ToArray();
            for (var k = 0; k < 8; k++) m.Both(new[] { outer[k], outer[(k + 1) % 8], inner[(k + 1) % 8], inner[k] }, CfBrass);
            m.Line(m.P(2, centre), m.P(2, new Vector3(0, 440, CfLidEnd - 4)), Dark);
        }
        // his neck and head: pale, his hair slicked back to a widow's peak, pointed ears, red eyes, fangs
        m.Light = VpSkinShare;
        const float HeadScale = 1.25f;
        var chin = new Vector3(0, 640, SeatZ + 26);
        Vector3 Head(Vector3 p) => chin + p * HeadScale;
        HmLimb(m, 13, new[] { new Vector3(0, 610, SeatZ + 8), chin + new Vector3(0, 30, -20) }, new[] { 36f, 32 }, 5, _ => VpSkin);
        var rings = new HmRing[]
        {
            new(0, 24, 26, 16, 20), new(30, 48, 56, 44, 10), new(62, 58, 66, 60, 4), new(96, 63, 70, 70, 0), new(130, 64, 64, 76, -4),
            new(160, 56, 46, 72, -10), new(182, 38, 24, 52, -16),
        };
        HmHead(m, Head, rings, (band, k) => band switch
        {
            2 => k is 2 or 3 or 4 ? VpHair : VpSkin,
            3 => k is 1 or 2 or 3 or 4 or 5 ? VpHair : VpSkin,
            4 => k is 0 or 6 ? VpSkin : VpHair,
            5 => VpHair,
            _ => VpSkin,
        }, VpSkin, VpHair, new Vector3(0, 190, -14));
        m.Light = 1;
        foreach (var side in Sides)
        {
            m.Ball(13, Head(new Vector3(side * 25, 104, 66)), 10, VpEye);
            m.Line(m.P(13, Head(new Vector3(side * 8, 116, 70))), m.P(13, Head(new Vector3(side * 46, 126, 60))), VpHair);
            // (a pointed ear: a triangle back from the side of the head, its point up)
            m.Light = VpSkinShare;
            var ear = new[] { m.P(13, Head(new Vector3(side * 62, 112, 2))), m.P(13, Head(new Vector3(side * 63, 80, 6))), m.P(13, Head(new Vector3(side * 90, 140, -24))) };
            m.Out(ear, VpSkin, Head(new Vector3(-side * 40, 110, 0)));
            m.Out(new[] { ear[0], ear[2], ear[1] }, VpSkin, Head(new Vector3(side * 140, 110, 0)));
            m.Light = 1;
            // (a fang over his lip)
            m.In(new[] { m.P(13, Head(new Vector3(side * 6, 58, 66))), m.P(13, Head(new Vector3(side * 18, 58, 64))), m.P(13, Head(new Vector3(side * 12, 38, 64))) }, VpFang, Head(new Vector3(0, 50, 300)), unlit: true);
        }
        m.Line(m.P(13, Head(new Vector3(-22, 60, 64))), m.P(13, Head(new Vector3(22, 60, 64))), VpLips);
        m.In(new[] { m.P(13, Head(new Vector3(-9, 84, 70))), m.P(13, Head(new Vector3(9, 84, 70))), m.P(13, Head(new Vector3(0, 108, 78))) }, VpSkin + 1, Head(new Vector3(0, 90, 300)), unlit: true);

        // his collar standing up behind his head, black outside, red inside, its two points high at the sides
        {
            var neck = new Vector3(0, 600, SeatZ + 4);
            var angles = new[] { 95f, 135, 180, 225, 265 };
            int[] Arc(float y, float r, float tipLift) => angles.Select((a, i) =>
            {
                var t = a * MathF.PI / 180;
                var lift = i is 0 or 4 ? tipLift : 0;
                return m.P(13, neck + new Vector3(r * MathF.Sin(t), y + lift, r * MathF.Cos(t) - 10));
            }).ToArray();
            var foot = Arc(0, 100, 0);
            var rim = Arc(250, 165, 60);
            var towards = neck + new Vector3(0, 120, 60);
            for (var k = 0; k + 1 < angles.Length; k++)
            {
                var quad = new[] { foot[k], foot[k + 1], rim[k + 1], rim[k] };
                m.In(quad, VpCapeIn, towards, unlit: true);
                m.Out(quad, VpCapeOut, towards, unlit: true);
                m.Line(rim[k], rim[k + 1], VpHem);
            }
        }
        // his cape, streaming back over the coffin from his shoulders: black outside, red inside, a red hem
        {
            var shoulderLine = new[] { -130f, -65, 0, 65, 130 };
            var hemX = new[] { -270f, -135, 0, 135, 270 };
            var hemZ = new[] { -540f, -590, -560, -590, -540 };
            var hemY = new[] { 470f, 500, 520, 500, 470 };
            var roots = shoulderLine.Select(x => m.P(13, new Vector3(x, 600 - MathF.Abs(x) * 0.12f, SeatZ - 52))).ToArray();
            var hem = hemX.Select((x, i) => m.P(13, new Vector3(x, hemY[i], hemZ[i]))).ToArray();
            var under = new Vector3(0, 380, -400);
            for (var k = 0; k + 1 < roots.Length; k++)
            {
                var quad = new[] { roots[k], roots[k + 1], hem[k + 1], hem[k] };
                m.In(quad, VpCapeIn, under, unlit: true);
                m.Out(quad, VpCapeOut, under, unlit: true);
                m.Line(hem[k], hem[k + 1], VpHem);
            }
        }
        Arms(m, shoulders[0], shoulders[1]);
        return m.ToBody(racer.Header);
    }

    // ================================================================================================================ the witch's broomstick

    // The broomstick's wood, its bristles' golds and the twine; the witch's green skin, ginger hair, purple robe and cape (a part of the
    // light), black hat and boots, the hat's orange band and gold buckle, her stockings' orange and black stripes, her eyes; the cat, the
    // sparks, the shadow under her.
    private const int WtWood = 19, WtStraw = 103, WtStrawEnd = 104, WtStrawLine = 101, WtTwine = 70, WtSkin = 137, WtHair = 86, WtRobe = 213, WtCape = 210,
        WtHat = 48, WtBand = 89, WtBuckle = 107, WtStripe = 89, WtBoot = 48, WtEye = 245, WtMouth = 49, WtCat = 48, WtCatEye = 245, WtSpark = 247,
        WtSparkGold = 245, WtShadow = 48;
    private const float WtWoodShare = 0.5f, WtStrawShare = 0.5f, WtSkinShare = 0.45f, WtHairShare = 0.4f, WtRobeShare = 0.4f, WtDarkShare = 0.3f;
    // How high the broomstick flies over the road: its handle's line at the witch's seat (the race-track mode bobs it, WitchBob).
    private const float WtHandle = 430;

    public static Body BuildBroomstick(Body racer)
    {
        var m = new CarMesh(Broomstick.Name);
        Roots(m);
        NoWheels(m, new Vector3(0, WtHandle, -60));

        // ---- its shadow on the road under it (no car's shadow is drawn in the race): a dark oval, flat on the road -- over it however low
        // the bob takes the broomstick (WitchBob: the body goes up and down with it)
        {
            const float Low = WitchBob + 10;
            var ring = Enumerable.Range(0, 8).Select(k => m.P(2, new Vector3(230 * MathF.Sin(k * MathF.Tau / 8), Low, -90 + 470 * MathF.Cos(k * MathF.Tau / 8)))).ToArray();
            var mid = m.P(2, new Vector3(0, Low, -90));
            for (var k = 0; k < 8; k++) m.Up(new[] { ring[k], ring[(k + 1) % 8], mid }, WtShadow, unlit: true);
        }

        // ---- the handle: gnarled, rising a little to its tip
        m.Light = WtWoodShare;
        var stick = new[] { new Vector3(0, 492, 640), new Vector3(8, 472, 380), new Vector3(-6, 450, 120), new Vector3(4, WtHandle, -120), new Vector3(0, 422, -310) };
        var handle = HmLimb(m, 2, stick, new[] { 18f, 24, 25, 25, 26 }, 5, _ => WtWood);
        m.Cap(handle[0], m.P(2, stick[0] + new Vector3(0, 6, 30)), WtWood, stick[1]);

        // ---- the bristles: a bundle flaring back from the twine, its ends a fan; darker strands along it
        m.Light = WtStrawShare;
        var bundle = new (Vector3 C, float R)[] { (new Vector3(0, 422, -300), 50), (new Vector3(0, 424, -370), 72), (new Vector3(0, 432, -560), 135), (new Vector3(0, 440, -760), 175) };
        // (wider than high: the first radius is up, the second across)
        var rings = bundle.Select(b => HmRound(m, 2, b.C, -Vector3.UnitZ, b.R * 0.8f, 8, Vector3.UnitY, b.R)).ToArray();
        for (var i = 0; i + 1 < rings.Length; i++) m.Skin(rings[i], rings[i + 1], k => WtStraw, (bundle[i].C + bundle[i + 1].C) / 2);
        m.Cap(rings[^1], m.P(2, new Vector3(0, 440, -700)), WtStrawEnd, new Vector3(0, 432, -500));
        m.Cap(rings[0], m.P(2, new Vector3(0, 422, -280)), WtStraw, new Vector3(0, 424, -420));
        m.Light = 1;
        for (var k = 0; k < 8; k += 2) { m.Line(rings[1][k], rings[2][k], WtStrawLine); m.Line(rings[2][k + 1], rings[3][k + 1], WtStrawLine); }
        // (the twine round its neck, twice)
        foreach (var z in new[] { -318f, -346 })
        {
            var r = 50 + 22 * (-300 - z) / 70 + 3;
            var twine = HmRound(m, 2, new Vector3(0, 423, z), -Vector3.UnitZ, r * 0.8f, 8, Vector3.UnitY, r);
            for (var k = 0; k < 8; k++) m.Line(twine[k], twine[(k + 1) % 8], WtTwine);
        }

        // ---- her black cat, riding on the bristles behind her: its body and head, ears, yellow eyes, its tail curling up
        {
            m.Ball(2, new Vector3(0, 530, -470), 58, WtCat);
            m.Ball(2, new Vector3(0, 600, -418), 42, WtCat);
            m.Light = WtDarkShare;
            foreach (var side in Sides)
                m.Plate(2, WtCat, new Vector3(side * 14, 622, -420), new Vector3(side * 40, 618, -424), new Vector3(side * 32, 670, -428));
            m.Light = 1;
            foreach (var side in Sides) m.Ball(2, new Vector3(side * 16, 606, -378), 9, WtCatEye);
            var tail = new[] { new Vector3(0, 500, -520), new Vector3(0, 560, -600), new Vector3(10, 650, -610), new Vector3(30, 690, -570) }.Select(p => m.P(2, p)).ToArray();
            for (var k = 0; k + 1 < tail.Length; k++) m.Line(tail[k], tail[k + 1], WtCat);
        }

        // ---- green sparks in her wake
        m.Ball(2, new Vector3(40, 470, -860), 24, WtSpark);
        m.Ball(2, new Vector3(-50, 510, -960), 17, WtSparkGold);
        m.Ball(2, new Vector3(15, 450, -1050), 12, WtSpark);

        // ---- the witch (bone 13), astride the stick, leaning into the wind
        const float SeatZ = -60;
        m.Pivot(13, m.P(2, new Vector3(0, WtHandle + 20, SeatZ)));
        // her robe: a skirt over the stick and her thighs, up to her shoulders, leaning forward
        m.Light = WtRobeShare;
        var robe = HmStack(m, 13, new[]
        {
            (new Vector3(0, 395, SeatZ - 40), 165f, 150f), (new Vector3(0, 470, SeatZ - 30), 125f, 100f), (new Vector3(0, 580, SeatZ + 10), 108f, 78f),
            (new Vector3(0, 680, SeatZ + 60), 118f, 68f), (new Vector3(0, 735, SeatZ + 84), 92f, 54f), (new Vector3(0, 758, SeatZ + 94), 40f, 34f),
        }, 8, (_, _) => WtRobe);
        // (the skirt's hem closed under it: a dark underside)
        m.Cap(robe[0], m.P(13, new Vector3(0, 420, SeatZ - 40)), WtCape, new Vector3(0, 600, SeatZ));
        // her legs: thighs in the robe forward along the stick, then her striped stockings down to her knees and back, black pointed boots
        foreach (var side in Sides)
        {
            m.Light = WtRobeShare;
            HmLimb(m, 13, new[] { new Vector3(side * 85, 440, SeatZ - 20), new Vector3(side * 120, 410, SeatZ + 110) }, new[] { 58f, 48 }, 5, _ => WtRobe);
            m.Light = WtDarkShare;
            var knee = new Vector3(side * 124, 400, SeatZ + 130);
            var ankle = new Vector3(side * 132, 238, SeatZ + 40);
            var leg = new[] { knee, Vector3.Lerp(knee, ankle, 0.25f), Vector3.Lerp(knee, ankle, 0.5f), Vector3.Lerp(knee, ankle, 0.75f), ankle };
            HmLimb(m, 13, leg, new[] { 40f, 36, 33, 30, 28 }, 5, s => s % 2 == 0 ? WtStripe : Dark);
            // (the boot: up from the ankle, then a pointed toe curling up)
            var boot = HmLimb(m, 13, new[] { ankle + new Vector3(0, 30, -6), ankle + new Vector3(0, -40, 6), ankle + new Vector3(0, -50, 90) }, new[] { 34f, 34, 22 }, 5, _ => WtBoot);
            m.Cap(boot[^1], m.P(13, ankle + new Vector3(0, -22, 150)), WtBoot, ankle + new Vector3(0, -40, 20));
        }
        // her arms forward to the stick, green hands on it
        var shoulders = new List<int>();
        foreach (var side in Sides)
        {
            m.Light = WtRobeShare;
            var arm = HmLimb(m, 13, new[] { new Vector3(side * 100, 720, SeatZ + 84), new Vector3(side * 122, 620, SeatZ + 200), new Vector3(side * 42, 512, SeatZ + 300) },
                new[] { 36f, 34, 40 }, 5, _ => WtRobe);
            shoulders.Add(arm[0][0]);
            m.Light = 1;
            m.Ball(13, new Vector3(side * 30, 486, SeatZ + 318), 30, WtSkin + 2);
        }
        // her cape streaming back from her shoulders, ragged at its hem
        {
            var roots = new[] { -95f, 0, 95 }.Select(x => m.P(13, new Vector3(x, 740 - MathF.Abs(x) * 0.2f, SeatZ + 60))).ToArray();
            var hem = new[] { (-210f, 600f, -380f), (-90f, 640, -330), (0f, 625, -430), (90f, 640, -330), (210f, 600, -380) }.Select(p => m.P(13, new Vector3(p.Item1, p.Item2, p.Item3))).ToArray();
            var under = new Vector3(0, 450, -150);
            foreach (var quad in new[] { new[] { roots[0], roots[1], hem[2], hem[1] }, new[] { roots[1], roots[2], hem[3], hem[2] } })
            {
                m.In(quad, WtCape + 1, under, unlit: true); m.Out(quad, WtCape + 2, under, unlit: true);
            }
            foreach (var tri in new[] { new[] { roots[0], hem[1], hem[0] }, new[] { roots[2], hem[4], hem[3] } })
            {
                m.In(tri, WtCape + 1, under, unlit: true); m.Out(tri, WtCape + 2, under, unlit: true);
            }
        }
        // her head: green, a long hooked nose, a pointed chin, yellow eyes, a wart; her ginger hair down her back, streaming
        m.Light = WtSkinShare;
        const float HeadScale = 1.15f;
        var chin = new Vector3(0, 760, SeatZ + 120);
        var tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.22f);
        Vector3 Head(Vector3 p) => chin + Vector3.Transform(p * HeadScale, tilt);
        var headRings = new HmRing[]
        {
            new(0, 16, 30, 12, 30), new(28, 44, 50, 42, 10), new(60, 56, 62, 58, 2), new(96, 60, 64, 66, 0), new(128, 60, 58, 70, -4),
            new(154, 54, 46, 66, -8), new(170, 42, 30, 54, -10),
        };
        HmHead(m, Head, headRings, (band, k) => band switch
        {
            2 => k is 2 or 3 or 4 ? WtHair : WtSkin,
            >= 3 => k is 1 or 2 or 3 or 4 or 5 ? WtHair : band >= 5 ? WtHair : WtSkin,
            _ => WtSkin,
        }, WtSkin, WtHair, new Vector3(0, 176, -10));
        // (the nose: a long hook from between her eyes)
        {
            int a = m.P(13, Head(new Vector3(-12, 92, 62))), b = m.P(13, Head(new Vector3(12, 92, 62))), c = m.P(13, Head(new Vector3(0, 108, 64)));
            int tip = m.P(13, Head(new Vector3(0, 62, 148)));
            var nose = Head(new Vector3(0, 90, 40));
            m.Out(new[] { a, b, tip }, WtSkin, nose); m.Out(new[] { b, c, tip }, WtSkin, nose); m.Out(new[] { c, a, tip }, WtSkin, nose);
        }
        m.Light = 1;
        foreach (var side in Sides) m.Ball(13, Head(new Vector3(side * 24, 104, 62)), 9, WtEye);
        m.Ball(13, Head(new Vector3(22, 54, 60)), 8, WtSkin - 2);
        m.Line(m.P(13, Head(new Vector3(-22, 42, 56))), m.P(13, Head(new Vector3(20, 46, 58))), WtMouth);
        // (her hair: three wide locks from under her hat, streaming out behind her in the wind)
        m.Light = WtHairShare;
        foreach (var side in Sides)
            m.Plate(13, WtHair, Head(new Vector3(side * 62, 120, -10)), Head(new Vector3(side * 26, 150, -58)),
                new Vector3(side * 70, 790, SeatZ - 250), new Vector3(side * 170, 700, SeatZ - 170));
        m.Plate(13, WtHair, Head(new Vector3(-30, 110, -66)), Head(new Vector3(30, 110, -66)), new Vector3(36, 730, SeatZ - 300), new Vector3(-36, 730, SeatZ - 300));
        // her hat: a wide brim, a tall cone bent back at its tip, an orange band and a gold buckle
        {
            m.Light = WtDarkShare;
            var crown = Head(new Vector3(0, 158, -6));
            var axis = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, tilt));
            var brimOut = HmRound(m, 13, crown, axis, 215, 10, Vector3.UnitZ, 200);
            var brimIn = HmRound(m, 13, crown + axis * 6, axis, 100, 10, Vector3.UnitZ, 92);
            for (var k = 0; k < 10; k++)
            {
                var quad = new[] { brimOut[k], brimOut[(k + 1) % 10], brimIn[(k + 1) % 10], brimIn[k] };
                m.In(quad, WtHat, crown + axis * 300); m.In(quad, WtHat, crown - axis * 300);
            }
            var cone = new[] { crown + axis * 6, crown + axis * 120 - Vector3.UnitZ * 20, crown + axis * 230 - Vector3.UnitZ * 70 };
            var hat = HmLimb(m, 13, cone, new[] { 96f, 62, 30 }, 6, _ => WtHat);
            m.Cap(hat[^1], m.P(13, crown + axis * 270 - Vector3.UnitZ * 170), WtHat, cone[1]);
            m.Light = 1;
            var band = HmRound(m, 13, crown + axis * 28, axis, 92, 8, Vector3.UnitZ, 86);
            for (var k = 0; k < 8; k++) m.Line(band[k], band[(k + 1) % 8], WtBand);
            m.Ball(13, crown + axis * 30 + Vector3.Transform(new Vector3(0, 0, 96), tilt), 18, WtBuckle);
        }
        Arms(m, shoulders[0], shoulders[1]);
        return m.ToBody(racer.Header);
    }

    // ================================================================================================================ the zombie's hearse

    // The hearse's black (a part of the light: dark greys), its chrome, the windows (dark), the curtains' wine red, the wreath's greens and
    // its flowers, the lamps; the zombie's grey-green skin, his ragged shirt, his hair, his sunken eyes' glow, his teeth, his bared skull.
    private const int HrBlack = 48, HrChrome = 58, HrChromeLine = 61, HrWindow = 49, HrGlass = 160, HrCurtain = 66, HrTassel = 106, HrWreath = 132, HrRose = 72,
        HrLily = 62, HrLamp = 245, HrTail = 249, HrMarker = 244, HrTyre = 48, HrWhitewall = 62;
    private const float HrBlackShare = 0.45f, HrChromeShare = 0.45f, HrCurtainShare = 0.3f, HrWreathShare = 0.45f;
    private const int ZbSkin = 117, ZbShirt = 226, ZbHair = 49, ZbSocket = 49, ZbEye = 247, ZbTeeth = 62, ZbMouth = 64, ZbBone = 60, ZbStitch = 49;
    private const float ZbSkinShare = 0.45f, ZbShirtShare = 0.4f;
    // the side he drives on (as the toy truck's driver: -x, the car's right -- seen from the chase camera behind it, on the right)
    private const float ZbSide = -1;

    public static Body BuildHearse(Body racer)
    {
        var m = new CarMesh(Hearse.Name);
        Roots(m);

        // ---- the body: rings from the nose back (TtRing: each ring's +x half from the roof's edge round to the bottom -- the edge, the
        // roof's corner, the belt, the sill, the bottom), the bonnet low, the windscreen steep, the long roof over the cab and the
        // compartment behind it
        var profile = new (float Z, (float X, float Y)[] Half)[]
        {
            (722, new[] { (105f, 372f), (176f, 366f), (204f, 350f), (206f, 222f), (176f, 196f) }),
            (660, new[] { (150f, 414f), (214f, 408f), (234f, 394f), (236f, 212f), (204f, 188f) }),
            (300, new[] { (158f, 428f), (220f, 422f), (240f, 412f), (238f, 212f), (204f, 188f) }),
            (165, new[] { (150f, 738f), (210f, 718f), (240f, 416f), (238f, 212f), (204f, 188f) }),
            (-650, new[] { (150f, 748f), (210f, 728f), (240f, 416f), (238f, 212f), (204f, 188f) }),
            (-724, new[] { (136f, 694f), (198f, 678f), (230f, 408f), (230f, 216f), (194f, 192f) }),
        };
        const int Windscreen = 2;
        m.Light = HrBlackShare;
        var rings = profile.Select(r => TtRing(m, r.Z, r.Half)).ToArray();
        Vector3 Inside(int i) => new(0, 400, (profile[i].Z + profile[i + 1].Z) / 2);
        for (var i = 0; i + 1 < rings.Length; i++)
            for (var k = 0; k < 10; k++)
            {
                // (the windscreen: the top and its corners between the bonnet's end and the roof's front, see-through)
                if (i == Windscreen && k is 9 or 0 or 8) continue;
                m.Out(new[] { rings[i][k], rings[i][(k + 1) % 10], rings[i + 1][(k + 1) % 10], rings[i + 1][k] }, HrBlack, Inside(i));
            }
        // the front under the bonnet's end and the back (its door): closed
        m.Cap(rings[0], m.P(2, new Vector3(0, 290, 730)), HrBlack, new Vector3(0, 300, 640));
        m.Cap(rings[^1], m.P(2, new Vector3(0, 450, -730)), HrBlack, new Vector3(0, 450, -640));
        m.Light = 1;
        foreach (var k in new[] { 9, 0, 8 })
            m.Both(new[] { rings[Windscreen][k], rings[Windscreen][(k + 1) % 10], rings[Windscreen + 1][(k + 1) % 10], rings[Windscreen + 1][k] }, HrGlass, CarMesh.SeeThrough);
        // the chrome: along the belt and round the roof's edge, the windscreen's frame
        for (var i = 0; i + 1 < rings.Length; i++)
        {
            m.Line(rings[i][2], rings[i + 1][2], HrChromeLine); m.Line(rings[i][7], rings[i + 1][7], HrChromeLine);
            if (i >= Windscreen + 1) { m.Line(rings[i][1], rings[i + 1][1], HrChromeLine); m.Line(rings[i][8], rings[i + 1][8], HrChromeLine); }
        }
        m.Line(rings[Windscreen][1], rings[Windscreen][8], HrChromeLine);
        m.Line(rings[Windscreen + 1][0], rings[Windscreen + 1][9], HrChromeLine);

        // ---- the windows, on the sides' upper panels (the roof's corner to the belt) from the windscreen's top back: the cab's, the driver's
        // open (dark inside); the compartment's two, curtained -- dark glass, the wine-red curtains drawn back to the sides, gold tie lines
        var cab = new List<int>();
        foreach (var side in Sides)
        {
            // (the panel's corners: front-top, back-top, back-belt, front-belt)
            int corner = side > 0 ? 1 : 8, belt = side > 0 ? 2 : 7;
            Vector3 ft = m.At(rings[3][corner]), bt = m.At(rings[4][corner]), bb = m.At(rings[4][belt]), fb = m.At(rings[3][belt]);
            var outward = Vector3.Normalize(new Vector3(side, 0.15f, 0));
            Vector3 On(float z, float t) { var s = (profile[3].Z - z) / (profile[3].Z - profile[4].Z); return HmOnQuad(fb, bb, bt, ft, s, t, 4, outward); }
            int[] Pane(float z0, float z1, float t0, float t1) => new[] { m.P(2, On(z0, t0)), m.P(2, On(z1, t0)), m.P(2, On(z1, t1)), m.P(2, On(z0, t1)) };
            var outside = new Vector3(side * 1000, 560, 0);
            // the cab's window
            var w = Pane(150, -30, 0.1f, 0.9f);
            m.In(w, HrWindow, outside + new Vector3(0, 0, 60), unlit: true);
            for (var k = 0; k < 4; k++) m.Line(w[k], w[(k + 1) % 4], HrChromeLine);
            if (side == ZbSide) cab.AddRange(w);
            // the compartment's, curtained
            foreach (var (z0, z1) in new[] { (-90f, -330f), (-380f, -610f) })
            {
                var p = Pane(z0, z1, 0.1f, 0.9f);
                m.In(p, HrWindow, outside, unlit: true);
                for (var k = 0; k < 4; k++) m.Line(p[k], p[(k + 1) % 4], HrChromeLine);
                // (each curtain drawn back to its side and tied: a triangle from the top across, down to the bottom corner, the tie a gold line
                // across it)
                m.Light = HrCurtainShare;
                var span = z0 - z1;
                foreach (var (from, to) in new[] { (z0, z0 - span * 0.34f), (z1, z1 + span * 0.34f) })
                {
                    var drape = new[] { m.P(2, On(from, 0.88f) + outward * 6), m.P(2, On(to, 0.88f) + outward * 6), m.P(2, On(from, 0.12f) + outward * 6) };
                    m.In(drape, HrCurtain, outside);
                    m.Line(m.P(2, On(from, 0.45f) + outward * 10), m.P(2, On(from + (to - from) * 0.3f, 0.5f) + outward * 10), HrTassel);
                }
                m.Light = 1;
            }
            // the landau bars: an S of chrome on the compartment's rear quarter
            var s1 = new[] { (-640f, 0.86f), (-668f, 0.7f), (-630f, 0.5f), (-652f, 0.34f) }.Select(p => m.P(2, On(p.Item1, p.Item2) + outward * 8)).ToArray();
            for (var k = 0; k + 1 < s1.Length; k++) m.Line(s1[k], s1[k + 1], HrChromeLine);
        }

        // ---- the wings over the wheels and the running boards between them: black, flowing, edged with chrome
        var front = new Axle(new Vector3(204, 260, 450), new Vector3(318, 165, 450), 165, 92);
        var rear = new Axle(new Vector3(204, 268, -420), new Vector3(318, 175, -420), 175, 100);
        m.Light = HrBlackShare;
        foreach (var side in Sides)
        {
            Mudguard(m, front, side, -1.55f, 1.95f, 4, 24, 16, HrBlack);
            Mudguard(m, rear, side, -1.7f, 1.45f, 4, 24, 16, HrBlack);
            // (the running board: from the front wing's back to the rear wing's front, the body's sill to the wings' outside, its edge chrome)
            var board = m.Plate(2, HrBlack, new Vector3(side * 236, 206, 250), new Vector3(side * 372, 206, 250), new Vector3(side * 372, 206, -232), new Vector3(side * 236, 206, -232));
            m.Line(board[1], board[2], HrChromeLine);
        }

        // ---- the front: a chrome grille, the bumper, the lamps on the wings, a mascot; the back: the bumper, the tail lamps, the wreath on the
        // door; marker lamps on the roof
        m.Light = HrChromeShare;
        m.Box(2, new Vector3(0, 214, 738), new Vector3(470, 36, 28), HrChrome);
        m.Box(2, new Vector3(0, 222, -742), new Vector3(450, 36, 26), HrChrome);
        m.Light = 1;
        {
            var g = m.Flat(2, HrChrome + 2, new Vector3(-104, 236, 728), new Vector3(104, 236, 728), new Vector3(96, 356, 726), new Vector3(-96, 356, 726));
            for (var k = 1; k < 4; k++) { var x = -100 + 200 * k / 4f; m.Rod(2, new Vector3(x, 240, 732), new Vector3(x * 0.96f, 352, 730), Dark); }
            m.Line(g[2], g[3], HrChromeLine);
        }
        m.Ball(2, new Vector3(0, 392, 712), 18, HrChromeLine + 1);
        foreach (var side in Sides)
        {
            m.Ball(2, new Vector3(side * 300, 372, 590), 44, HrLamp);
            m.Ball(2, new Vector3(side * 196, 360, -732), 30, HrTail);
            m.Ball(2, new Vector3(side * 170, 752, 150), 18, HrMarker);
        }
        {
            var centre = new Vector3(0, 470, -731);
            m.Light = HrWreathShare;
            var outer = HmRound(m, 2, centre, -Vector3.UnitZ, 112, 8, Vector3.UnitY);
            var inner = HmRound(m, 2, centre + new Vector3(0, 0, -8), -Vector3.UnitZ, 70, 8, Vector3.UnitY);
            for (var k = 0; k < 8; k++) m.In(new[] { outer[k], outer[(k + 1) % 8], inner[(k + 1) % 8], inner[k] }, HrWreath, centre - Vector3.UnitZ * 400);
            m.Light = 1;
            for (var k = 0; k < 8; k++)
            {
                var a = (k + 0.5f) * MathF.Tau / 8;
                m.Ball(2, centre + new Vector3(92 * MathF.Sin(a), 92 * MathF.Cos(a), -14), k % 2 == 0 ? 22 : 17, k % 2 == 0 ? HrRose : HrLily);
            }
        }

        // ---- the wheels: black tyres with white walls, chrome hubs
        BandedWheels(m, new WheelLook(Dark, 24, HrTyre, SoftLight, HrChrome, HrChromeShare, HrChromeLine, Lean: true), front, rear, HrWhitewall);

        // ---- the zombie (bone 13), leaning out of the driver's window, his arm reaching out ahead of the car
        m.Pivot(13, cab[0]);
        Vector3 S(Vector3 v) => v with { X = ZbSide * v.X };
        // his shoulders across the cab's window, the outer one out of it: a ragged shirt
        m.Light = ZbShirtShare;
        var yoke = HmLimb(m, 13, new[] { S(new Vector3(150, 560, 60)), S(new Vector3(278, 578, 66)) }, new[] { 58f, 54 }, 5, _ => ZbShirt);
        m.Cap(yoke[^1], m.P(13, S(new Vector3(300, 580, 66))), ZbShirt, S(new Vector3(200, 570, 60)));
        // (rags hanging from the sleeve)
        foreach (var dz in new[] { -30f, 20 })
            m.Plate(13, ZbShirt + 1, S(new Vector3(300, 548, 66 + dz)), S(new Vector3(300, 548, 96 + dz)), S(new Vector3(306, 470, 76 + dz)));
        // his arm, stretched out ahead along the car: the torn sleeve, the bare forearm, the hand, its fingers reaching
        m.Light = ZbShirtShare;
        var shoulder = S(new Vector3(300, 572, 70));
        var elbow = S(new Vector3(330, 560, 250));
        var wrist = S(new Vector3(336, 572, 420));
        HmLimb(m, 13, new[] { shoulder, Vector3.Lerp(shoulder, elbow, 0.6f) }, new[] { 42f, 38 }, 5, _ => ZbShirt);
        m.Light = ZbSkinShare;
        HmLimb(m, 13, new[] { Vector3.Lerp(shoulder, elbow, 0.6f), elbow, wrist }, new[] { 28f, 26, 22 }, 5, _ => ZbSkin);
        m.Light = 1;
        var hand = wrist + new Vector3(0, 0, 30);
        m.Ball(13, hand, 30, ZbSkin + 2);
        foreach (var (dx, dy) in new[] { (-18f, 8f), (0f, 14f), (18f, 6f) })
            m.Rod(13, hand + S(new Vector3(dx, dy, 18)), hand + S(new Vector3(dx * 1.3f, dy - 26, 96)), ZbSkin + 1);
        // his neck and his head, out of the window, turned out a little and lolling
        m.Light = ZbSkinShare;
        const float HeadScale = 1.2f;
        var chin = S(new Vector3(300, 640, 120));
        // (turned out from the car a little -- a turn about y takes +z towards +x -- and lolling outward)
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, ZbSide * 0.25f) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -ZbSide * 0.2f);
        Vector3 Head(Vector3 p) => chin + Vector3.Transform(p with { X = ZbSide * p.X } * HeadScale, turn);
        HmLimb(m, 13, new[] { S(new Vector3(250, 590, 70)), Head(new Vector3(0, 30, -20)) }, new[] { 34f, 30 }, 5, _ => ZbSkin);
        var headRings = new HmRing[]
        {
            new(0, 30, 30, 18, 24), new(30, 54, 60, 48, 10), new(60, 62, 70, 62, 4), new(96, 66, 72, 72, 0), new(130, 66, 68, 78, -4),
            new(160, 58, 50, 74, -10), new(182, 40, 26, 54, -16),
        };
        HmHead(m, Head, headRings, (_, _) => ZbSkin, ZbSkin, ZbSkin, new Vector3(0, 192, -14));
        m.Light = 1;
        // (his eyes sunk into dark sockets, glowing; his nose rotted to a dark hole; his teeth bared over a dark mouth; stitches over one
        // cheek; the other ear gone and his skull showing over it; a few tufts of hair)
        foreach (var side in Sides)
        {
            m.Ball(13, Head(new Vector3(side * 26, 100, 70)), 19, ZbSocket);
            m.Ball(13, Head(new Vector3(side * 26, 100, 78)), 7, ZbEye);
        }
        m.In(new[] { m.P(13, Head(new Vector3(-10, 72, 74))), m.P(13, Head(new Vector3(10, 72, 74))), m.P(13, Head(new Vector3(0, 90, 78))) }, ZbSocket, Head(new Vector3(0, 80, 300)), unlit: true);
        {
            int a = m.P(13, Head(new Vector3(-30, 38, 62))), b = m.P(13, Head(new Vector3(30, 38, 62))), c = m.P(13, Head(new Vector3(28, 56, 66))), d = m.P(13, Head(new Vector3(-28, 56, 66)));
            m.In(new[] { a, b, c, d }, ZbMouth, Head(new Vector3(0, 46, 300)), unlit: true);
            // (four teeth hanging from the top of his mouth, crooked: a row of points along it, a tip under each gap)
            var row = Enumerable.Range(0, 5).Select(k => m.P(13, Head(new Vector3(-26 + 13 * k, 56, 67)))).ToArray();
            for (var k = 0; k < 4; k++)
                m.In(new[] { row[k], row[k + 1], m.P(13, Head(new Vector3(-19.5f + 13 * k + (k % 2 == 0 ? 2 : -2), k % 2 == 0 ? 42 : 46, 66))) }, ZbTeeth, Head(new Vector3(0, 50, 300)), unlit: true);
        }
        m.Line(m.P(13, Head(new Vector3(-56, 70, 30))), m.P(13, Head(new Vector3(-36, 40, 54))), ZbStitch);
        foreach (var t in new[] { 0.33f, 0.67f })
        {
            var p = Vector3.Lerp(new Vector3(-56, 70, 30), new Vector3(-36, 40, 54), t);
            m.Line(m.P(13, Head(p + new Vector3(-8, -6, 0))), m.P(13, Head(p + new Vector3(8, 6, 0))), ZbStitch);
        }
        m.Ball(13, Head(new Vector3(-68, 100, 4)), 18, ZbSkin + 1);
        m.Ball(13, Head(new Vector3(50, 158, 14)), 20, ZbBone);
        m.Ball(13, Head(new Vector3(68, 104, 0)), 10, ZbSocket);
        foreach (var (at, r) in new[] { (new Vector3(-22, 196, -16), 12), (new Vector3(-54, 170, -46), 11), (new Vector3(8, 186, -60), 10), (new Vector3(-40, 150, -82), 9) })
            m.Ball(13, Head(at), (int)(r * HeadScale), ZbHair);
        Arms(m, yoke[0][0], yoke[^1][0]);
        return m.ToBody(racer.Header);
    }

    // ================================================================================================================ installing

    // Adds Halloween's monsters' cars to a game folder whose race tracks are built (at the very end of the build: RaceTrackService.Build,
    // after everything else -- the flag marshal's and the toy truck's violet copy's bodies too -- so no body before them moves): each body
    // and its half-size copy at the end of BODY.HQR, the racer's entity's bodies VampireGeneric.. and their half-size ones, and their lines
    // in RACECARS.JSON. Returns lines for the build's log.
    public static List<string> InstallMonsters(string gameDirectory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var racer = Body.Read(HqrArchive.Open(bodyPath).Read(RaceTrackBaldinoCar.RacerBody), 2);
        var log = new List<string>();
        var file = File.ReadAllBytes(bodyPath);
        var table = HqrArchive.Open(ressPath).Read(44);
        var next = HqrArchive.CountEntries(bodyPath);
        var catalogue = Catalogue(gameDirectory);
        foreach (var car in Monsters)
        {
            var number = MonsterNumber(car);
            Body body;
            try { body = car.Build(racer, racer); }
            catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException) { log.Add($"{car.Name}: not made -- {e.Message}"); continue; }
            var bytes = body.Write();
            if (MonsterBox(car) is { } box)
                for (var i = 0; i < 6; i++) System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8 + i * 4), box[i]);
            int own = next, small = next + 1;
            file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(bytes));
            file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(RaceTrackSmallCars.Halved(bytes)));
            next += 2;
            table = RaceTrackBaldinoCar.WithBody(table, RaceTrackBaldinoCar.RacerEntity, car.Generic, own);
            table = RaceTrackBaldinoCar.WithBody(table, RaceTrackBaldinoCar.RacerEntity, RaceTrackSmallCars.SmallOf(car.Generic), small);
            catalogue.RemoveAll(e => e.Number == number);
            catalogue.Add(new CatalogueEntry(number, car.Name, car.Driver, car.Island, RaceTrackBaldinoCar.RacerEntity, car.Generic, own, small));
            log.Add($"{car.Name} (race car {number}, {car.Driver}'s on {car.Island}): BODY.HQR entry {own} ({body.Faces.Count} polygons, {body.Lines.Count} lines, " +
                    $"{body.Spheres.Count} spheres, {body.Vertices.Count} points), its half-size copy {small}; the racer's entity ({RaceTrackBaldinoCar.RacerEntity}) bodies " +
                    $"{car.Generic} and {RaceTrackSmallCars.SmallOf(car.Generic)}");
        }
        File.WriteAllBytes(bodyPath, file);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        File.WriteAllText(Path.Combine(gameDirectory, CatalogueFile), JsonSerializer.Serialize(catalogue, new JsonSerializerOptions { WriteIndented = true }));
        return log;
    }
}
