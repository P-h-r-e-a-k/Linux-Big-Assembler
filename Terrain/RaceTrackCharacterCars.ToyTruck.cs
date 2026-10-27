using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Text.Json;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// The toy dump truck (the user, 2026-10-17, with a photo of a toddler's toy truck and one of a man: "Create a special car as pictured with
// the person in the second picture, either driving with their head out of the window smoking, or riding in the back of the truck"): the
// chunky toy at race-car size --
//
//   the body     glossy yellow: the cab tall and round-roofed with a bluff front, and behind it (since 2026-10-18: "the back of the truck
//                shouldn't be square and should be a flat bed") a low open flat bed -- its deck at the balls' tops, a thick rounded rail
//                round its sides and tailgate (the toy's maker's name on the side left off)
//   the windows  big and teal with round corners (2026-10-18: "round the windows more"), let into the cab's skin: the windscreen across
//                the front's slope, wrapping round its shoulders, a window each side leaning back as the windscreen does -- the driver's
//                open, dark inside, its glass wound down (a teal line along its bottom)
//   the lamps    two round red headlights low on the front, two small ones on the tail
//   the wheels   four oversized glossy purple balls on white hubs, standing out beside the body (as purple as the islands' palettes
//                go: see TtPurple)
//   the driver   made here, not taken from a body of the game's: a fond caricature in the cars' low-poly style -- thick wavy
//                salt-and-pepper hair, rectangular glasses, a dark moustache over a cheeky grin, a white T-shirt with a dark print --
//                leaning his head out of the driver's window, his elbow out over the door, a cigarette in the corner of his mouth, its tip
//                glowing, its smoke streaming back in the wind
//
// A body of the racer's entity (157: its driving animation bounces it, as it does the other cars) made with the cars' kit (CarMesh), but
// neither one of the characters' cars (All) nor the race game's: like the pumpkin F1 it goes into a game folder at the very end of every
// build (InstallToyTruck, after InstallPumpkin: no body before it moves), with its half-size copy, as race car ToyTruckNumber of
// RACECARS.JSON. Twinsen drives it on any track by picking it in the Race car window's list (RaceCarSetup.DriveAs); it is drawn in his
// buggy's place (RACEMOD.CPP drive_as=), so it handles as his buggy does. No track has it as its own car. On the islands whose palette has
// a violet free (2026-10-18: the Temple of Bú's) he drives its copy with violet ball wheels instead (InstallToyTruckViolet, at the very end
// of the build; RaceCarEngineFile.DriveAsIslandLines).
internal static partial class RaceTrackCharacterCars
{
    // race car 221 (RACECARS.JSON: after the pumpkin F1's 220), the racer entity's body 65 (its half-size copy 165)
    public const int ToyTruckNumber = 221, ToyTruckGeneric = 65;
    public const string ToyTruckName = "Toy dump truck", ToyTruckDriver = "the toy truck's driver";

    // The truck's colours: the body in the golds' top (96 on: the yellowest ramp every island's palette has), its flat bed's deck a step
    // darker, and the windows in the teals, all taking a part of the light so they stay bright; the balls in the purples, a lavender glint;
    // red lamps, white hubs.
    // (No island's palette has a strong purple: the purples, 208 on, are greyish -- a fifth saturated at most -- on all but Wannies Island,
    // the Mosquibees' and Volcano Island, where they are plum, and on Citadel's storm palette they turn slate blue past 212; the Temple's
    // palette has 212 and 218 taken by the temple's own greys. So the balls keep to 213-215, the ramp's most purple stretch on every
    // island, with little of the light: 213 is #443c48 and 215 #585060 on the daylight islands.)
    private const int TtYellow = 105, TtDeck = 104, TtWindow = 153, TtGlass = 156, TtInside = 178, TtPurple = 213, TtShine = 221, TtLamp = 73, TtHub = WhiteFlat;
    private const float TtBodyShare = 0.42f, TtBallShare = 0.3f;
    // (Then the user, 2026-10-18, chose "purple on some islands only": the truck has a copy whose balls are a strong violet, drawn on the
    // islands whose palette has one free -- InstallToyTruckViolet, VioletIslands --, the balls unlit in TtViolet. The Temple of Bú's palette (RESS 40: Otringal's with the temple's colours in it, which go only into its slots
    // 16-239) keeps the island palette's placeholder violet, #7c00dc, in slots 1-14 and draws them as they are; nothing the temple draws
    // uses them -- not its ground, decor, object pages or sky, no sprite, none of the bodies its scenes and race use; only the textures of
    // a few bodies raced elsewhere, in 8 and 12 -- so 11, the middle of 8-14. The retail islands' tables draw the slot as a grey or another
    // colour of theirs: there the mauve copy.)
    private const int TtViolet = 11;
    // The driver's: skin (the ramp Twinsen's is in), hair and moustache greys, the grin's dark red and its teeth, the ears', nose's and hand's
    // skin drawn flat, the T-shirt's white (a part of the light) and its print, the glasses' black frames, the cigarette, its ember and smoke.
    private const int TtSkin = 34, TtHair = 49, TtMoustache = 48, TtMouth = 67, TtTeeth = 62, TtSkinFlat = 38, TtShirt = 60, TtPrint = 49, TtPrintLine = 58,
        TtFrame = 48, TtBrow = 49, TtCigarette = 62, TtEmber = 242;
    private const float TtHeadShare = 0.7f;
    // the side he sits on: -x, the side the game draws as the car's right -- a right-hand drive (seen from the chase camera behind the car,
    // +x is on the left; BodyPipeline's pictures, from the front, show it on the right)
    private const float TtDriverSide = -1;

    // A ring across the body at z, from the points of its +x half ((x, y), from the top's edge round to the bottom's) and the -x half
    // mirrored: from the top's edge down the +x side, across the bottom and up the -x side -- so with n points a half its panels (each
    // between point k and k + 1) are 0 to n - 2 down the +x side, n - 1 the bottom, n to 2n - 2 up the -x side and 2n - 1 the top. (The
    // cab's, five a half: 0-3 the +x side's shoulder, window, door and sill, 4 the bottom, 5-8 the -x side's sill, door, window and
    // shoulder, 9 the roof. The bed's, seven: 0 the +x rail's inner side, 1-3 its rounded top, 4 and 5 the +x side, 6 the bottom, 7-12
    // the -x side and rail, 13 the deck.)
    private static int[] TtRing(CarMesh m, float z, (float X, float Y)[] half)
        => half.Select(p => new Vector3(p.X, p.Y, z)).Concat(half.Reverse().Select(p => new Vector3(-p.X, p.Y, z))).Select(v => m.P(2, v)).ToArray();

    // A window with round corners let into the skin: the patch of panels between two rows of the skin's points (`bottom` and `top`, as many
    // each, running the same way) made into a frame (`frame`) round the window (`glass`, unlit with `unlit`). The window's outline is the
    // polygon `corners` -- (s, t) as shares of the patch: s across it from its first points, t up it from the bottom row; anticlockwise --
    // each corner rounded `radius` (car units) by `arc` points; the frame is the band between the patch's edge and the outline (a polygon
    // from each edge point or outline point to the next, round the outline's middle, two of them a quad where they make one), the window
    // strips across the outline from its first edge. Every polygon faces away from `inside`. Returns the outline's points, from the first
    // corner's arc round.
    private static int[] TtRoundWindow(CarMesh m, int[] bottom, int[] top, (float S, float T)[] corners, float radius, int arc, int frame, int glass, bool unlit, Vector3 inside)
    {
        var n = bottom.Length - 1;
        var b = bottom.Select(m.At).ToArray(); var t = top.Select(m.At).ToArray();
        // (s measured along the patch's middle, t across it)
        var along = new float[n + 1];
        for (var i = 1; i <= n; i++) along[i] = along[i - 1] + Vector3.Distance((b[i] + t[i]) / 2, (b[i - 1] + t[i - 1]) / 2);
        float width = along[n], height = (float)Enumerable.Range(0, n + 1).Average(i => Vector3.Distance(b[i], t[i]));
        Vector3 On(Vector2 p)
        {
            var i = Math.Clamp(Array.FindLastIndex(along, a => a <= p.X), 0, n - 1);
            var f = (p.X - along[i]) / (along[i + 1] - along[i]);
            return Vector3.Lerp(Vector3.Lerp(b[i], b[i + 1], f), Vector3.Lerp(t[i], t[i + 1], f), p.Y / height);
        }
        // the outline: each corner's arc, from where it leaves the edge before it to where it meets the edge after it
        var at = corners.Select(c => new Vector2(c.S * width, c.T * height)).ToArray();
        var outline = new List<Vector2>();
        for (var c = 0; c < at.Length; c++)
        {
            var d1 = Vector2.Normalize(at[(c + at.Length - 1) % at.Length] - at[c]); var d2 = Vector2.Normalize(at[(c + 1) % at.Length] - at[c]);
            var half = MathF.Acos(Math.Clamp(Vector2.Dot(d1, d2), -1, 1)) / 2;
            var centre = at[c] + Vector2.Normalize(d1 + d2) * radius / MathF.Sin(half);
            Vector2 from = at[c] + d1 * radius / MathF.Tan(half) - centre, to = at[c] + d2 * radius / MathF.Tan(half) - centre;
            var a0 = MathF.Atan2(from.Y, from.X);
            var sweep = MathF.Atan2(to.Y, to.X) - a0;
            while (sweep > MathF.PI) sweep -= MathF.Tau;
            while (sweep < -MathF.PI) sweep += MathF.Tau;
            for (var k = 0; k < arc; k++) { var a = a0 + sweep * k / (arc - 1); outline.Add(centre + radius * new Vector2(MathF.Cos(a), MathF.Sin(a))); }
        }
        var inner = outline.Select(p => m.P(2, On(p))).ToArray();
        int count = inner.Length;

        // the frame: the patch's edge (along the bottom row, back along the top: anticlockwise too) and the outline, walked round together
        var edge = Enumerable.Range(0, n + 1).Select(i => (Point: bottom[i], At: new Vector2(along[i], 0)))
            .Concat(Enumerable.Range(0, n + 1).Reverse().Select(i => (Point: top[i], At: new Vector2(along[i], height)))).ToArray();
        var middle = outline.Aggregate(Vector2.Zero, (s, p) => s + p) / count;
        var start = MathF.Atan2(outline[0].Y - middle.Y, outline[0].X - middle.X);
        float Angle(Vector2 p) { var a = MathF.Atan2(p.Y - middle.Y, p.X - middle.X) - start; while (a < 0) a += MathF.Tau; while (a >= MathF.Tau) a -= MathF.Tau; return a; }
        var first = Enumerable.Range(0, edge.Length).MinBy(i => Angle(edge[i].At));
        (int Point, Vector2 At) E(int k) => edge[(first + k) % edge.Length];
        float EdgeAngle(int k) => Angle(E(k).At) + (k == edge.Length ? MathF.Tau : 0);
        float InnerAngle(int l) => l == count ? MathF.Tau : l == 0 ? 0 : Angle(outline[l]);
        // (each step one triangle: true when it moves along the edge, false along the outline; k and l where it starts)
        var steps = new List<(bool Edge, int K, int L)>();
        for (int k = 0, l = 0; k < edge.Length || l < count;)
        {
            if (l == count || (k < edge.Length && EdgeAngle(k + 1) <= InnerAngle(l + 1))) steps.Add((true, k++, l));
            else steps.Add((false, k, l++));
        }
        static bool Convex(params Vector2[] q)
        {
            var sign = 0;
            for (var i = 0; i < q.Length; i++)
            {
                Vector2 e1 = q[(i + 1) % q.Length] - q[i], e2 = q[(i + 2) % q.Length] - q[(i + 1) % q.Length];
                var cross = e1.X * e2.Y - e1.Y * e2.X;
                if (MathF.Abs(cross) < 1e-3f) return false;
                if (sign == 0) sign = MathF.Sign(cross); else if (MathF.Sign(cross) != sign) return false;
            }
            return true;
        }
        for (var i = 0; i < steps.Count; i++)
        {
            var (alongEdge, k, l) = steps[i];
            if (i + 1 < steps.Count && steps[i + 1].Edge != alongEdge
                && Convex(E(k).At, E(k + 1).At, outline[(l + 1) % count], outline[l % count]))
            {
                m.Out(new[] { E(k).Point, E(k + 1).Point, inner[(l + 1) % count], inner[l % count] }, frame, inside);
                i++;
            }
            else if (alongEdge) m.Out(new[] { E(k).Point, E(k + 1).Point, inner[l % count] }, frame, inside);
            else m.Out(new[] { E(k).Point, inner[(l + 1) % count], inner[l] }, frame, inside);
        }

        // the window: strips across, from the outline's first edge (between the first corner's arc and the second's) to the opposite one
        for (var k = 0; k + 2 <= count / 2; k++)
            m.Out(new[] { inner[arc + k], inner[(arc + k + 1) % count], inner[(arc - 2 - k + count) % count], inner[(arc - 1 - k + count) % count] }, glass, inside, unlit);
        return inner;
    }

    // A ball on a wheel's bone: its poles on the axle (x), `rings` rings of `meridians` points between them, faced out (`unlit`: drawn in
    // `colour` itself, whatever the light).
    private static void TtBall(CarMesh m, int bone, Vector3 centre, float radius, int meridians, int rings, int colour, bool unlit)
    {
        var lat = Enumerable.Range(1, rings).Select(j =>
        {
            var th = j * MathF.PI / (rings + 1);
            return Enumerable.Range(0, meridians).Select(k =>
            {
                var ph = (k + 0.5f) * MathF.Tau / meridians;
                return m.P(bone, centre + new Vector3(radius * MathF.Cos(th), radius * MathF.Sin(th) * MathF.Cos(ph), radius * MathF.Sin(th) * MathF.Sin(ph)));
            }).ToArray();
        }).ToArray();
        m.Cap(lat[0], m.P(bone, centre + new Vector3(radius, 0, 0)), colour, centre, unlit);
        for (var j = 0; j + 1 < lat.Length; j++)
            for (var k = 0; k < meridians; k++)
                m.Out(new[] { lat[j][k], lat[j][(k + 1) % meridians], lat[j + 1][(k + 1) % meridians], lat[j + 1][k] }, colour, centre, unlit);
        m.Cap(lat[^1], m.P(bone, centre - new Vector3(radius, 0, 0)), colour, centre, unlit);
    }

    // A wheel: the strut (from inside the body, where it turns; the white hub on the ball's inner side), the hub (a front wheel's, which
    // steers about the ball's middle) and the ball itself -- the violet copy's (TtViolet) unlit violet, else lit mauve --; a lavender glint
    // high on its outer side (on a bone that doesn't roll it round).
    private static void TtWheel(CarMesh m, (float Side, int Strut, int Hub, int Wheel) bones, Vector3 centre, float radius, bool violet)
    {
        var (side, strutBone, hubBone, wheelBone) = bones;
        centre = centre with { X = side * centre.X };
        m.Light = 1;
        m.Pivot(strutBone, m.P(2, new Vector3(side * 230, centre.Y + 40, centre.Z)));
        var hub = m.P(strutBone, centre - new Vector3(side * radius * 0.94f, 0, 0));
        m.Sphere(hub, (int)(radius * 0.25f), TtHub);
        var middle = m.P(strutBone, centre);
        var still = strutBone;
        if (hubBone >= 0)
        {
            var hubMiddle = m.P(hubBone, centre);
            m.Pivot(hubBone, middle);
            m.Pivot(wheelBone, hubMiddle);
            still = hubBone;
        }
        else m.Pivot(wheelBone, middle);
        m.Light = TtBallShare;
        TtBall(m, wheelBone, centre, radius, 10, 4, violet ? TtViolet : TtPurple, unlit: violet);
        m.Light = 1;
        m.Ball(still, centre + Vector3.Normalize(new Vector3(side * 0.7f, 0.65f, 0.1f)) * radius * 1.02f, (int)(radius * 0.12f), TtShine);
    }

    // The toy dump truck; `violet`: its copy with violet ball wheels (TtViolet), everything else the same.
    public static Body BuildToyTruck(Body racer, bool violet = false)
    {
        var m = new CarMesh(ToyTruckName);
        Roots(m);

        // ---- the cab: rings from the front back -- the front's rim, the windscreen's top, the side windows' back, the roof's back, the
        // cab's back (against the bed)
        var profile = new (float Z, (float X, float Y)[] Half)[]
        {
            (700, new[] { (115f, 470f), (210f, 455f), (250f, 395f), (258f, 300f), (205f, 178f) }),
            (545, new[] { (150f, 768f), (268f, 738f), (304f, 530f), (311f, 330f), (250f, 168f) }),
            (280, new[] { (150f, 775f), (270f, 742f), (306f, 530f), (311f, 330f), (250f, 168f) }),
            (180, new[] { (145f, 745f), (264f, 718f), (306f, 545f), (311f, 330f), (250f, 168f) }),
            (140, new[] { (115f, 705f), (236f, 688f), (306f, 545f), (310f, 330f), (246f, 170f) }),
        };
        const int Windscreen = 0, Cab = 1;
        m.Light = TtBodyShare;
        var rings = profile.Select(r => TtRing(m, r.Z, r.Half)).ToArray();
        Vector3 Inside(int i) => new(0, 470, (profile[i].Z + profile[i + 1].Z) / 2);
        for (var i = 0; i + 1 < rings.Length; i++)
            for (var k = 0; k < 10; k++)
            {
                // (the windows' panels -- the windscreen's 8, 9 and 0 between the front's rim and the roof, the side windows' 1 and 7 -- are
                // made with their frames, below)
                if ((i == Windscreen && k is 8 or 9 or 0) || (i == Cab && k is 1 or 7)) continue;
                m.Out(new[] { rings[i][k], rings[i][(k + 1) % 10], rings[i + 1][(k + 1) % 10], rings[i + 1][k] }, TtYellow, Inside(i));
            }
        // the front and the back closed: the front's face (its lamps on it, below) and the cab's back, flat, the bed against it
        m.Cap(rings[0], m.P(2, new(0, 320, 716)), TtYellow, new Vector3(0, 320, 640));
        m.Cap(rings[^1], m.P(2, new(0, 440, profile[^1].Z)), TtYellow, new Vector3(0, 440, profile[^1].Z + 80));
        // the windows, big and round-cornered as the toy's: the windscreen across the front's slope, wrapping round its shoulders, and a
        // window each side, its front edge leaning back as the windscreen does -- the driver's open, dark inside, the other teal
        TtRoundWindow(m, new[] { rings[0][8], rings[0][9], rings[0][0], rings[0][1] }, new[] { rings[1][8], rings[1][9], rings[1][0], rings[1][1] },
            new[] { (0.09f, 0.08f), (0.91f, 0.08f), (0.945f, 0.9f), (0.055f, 0.9f) }, 72, 4, TtYellow, TtWindow, false, Inside(Windscreen));
        // (three points round each of their corners: they are smaller)
        const int SideArc = 3;
        var sideWindow = new[] { (0.07f, 0.08f), (0.93f, 0.08f), (0.93f, 0.91f), (0.36f, 0.91f) };
        int[] driverWindow = null!;
        foreach (var side in Sides)
        {
            int sill = side > 0 ? 2 : 7, top = side > 0 ? 1 : 8;
            var open = side == TtDriverSide;
            var outline = TtRoundWindow(m, new[] { rings[Cab][sill], rings[Cab + 1][sill] }, new[] { rings[Cab][top], rings[Cab + 1][top] }, sideWindow, 44, SideArc,
                TtYellow, open ? TtInside : TtWindow, open, Inside(Cab));
            if (open) driverWindow = outline;
        }

        // ---- the flat bed behind the cab: a low open deck, a little darker, with a thick rounded rail round it -- rings from the cab's
        // back to the tail: the deck's front and back, the tailgate's inside, its top and the tail's rounding (the bed's front against the
        // cab's back, all of it inside the cab's outline)
        (float, float)[] Rail(float deck) => new[] { (256f, deck), (262f, 462f), (274f, 484f), (294f, 486f), (304f, 466f), (306f, 330f), (246f, 172f) };
        var bed = new (float Z, (float X, float Y)[] Half)[]
        {
            (profile[^1].Z, Rail(396)), (-385, Rail(396)), (-412, Rail(462)),
            (-440, new[] { (256f, 484f), (262f, 486f), (274f, 486f), (294f, 486f), (304f, 466f), (306f, 330f), (246f, 172f) }),
            (-464, new[] { (240f, 466f), (248f, 468f), (260f, 468f), (278f, 466f), (288f, 452f), (290f, 330f), (232f, 182f) }),
        };
        var bedRings = bed.Select(r => TtRing(m, r.Z, r.Half)).ToArray();
        var around = bedRings[0].Length;
        for (var i = 0; i + 1 < bedRings.Length; i++)
            for (var k = 0; k < around; k++)
            {
                // (the ring isn't round -- the deck is in a hollow -- so each panel faces the way its edge of the ring does: in x and y the
                // ring goes clockwise, down the +x side and back along the deck, so out is the edge's direction turned a quarter the other way)
                var quad = new[] { bedRings[i][k], bedRings[i][(k + 1) % around], bedRings[i + 1][(k + 1) % around], bedRings[i + 1][k] };
                var d = m.At(quad[1]) - m.At(quad[0]) + m.At(quad[2]) - m.At(quad[3]);
                var centre = quad.Aggregate(Vector3.Zero, (s, h) => s + m.At(h)) / 4;
                m.In(quad, i == 0 && k == around - 1 ? TtDeck : TtYellow, centre + new Vector3(-d.Y, d.X, 0));
            }
        m.Cap(bedRings[^1], m.P(2, new(0, 320, bed[^1].Z - 6)), TtYellow, new Vector3(0, 320, bed[^1].Z + 80));

        m.Light = 1;
        // the driver's window's glass, wound down: its top edge along the opening's bottom
        {
            var outward = new Vector3(TtDriverSide * 4, 2, 0);
            m.Line(m.P(2, m.At(driverWindow[SideArc - 1]) + outward), m.P(2, m.At(driverWindow[SideArc]) + outward), TtGlass);
        }
        // the headlights: round and red, low on the front
        foreach (var side in Sides) m.Ball(2, new Vector3(side * 160, 262, 713), 50, TtLamp);
        // (and small ones on the tail, for the cars behind)
        foreach (var side in Sides) m.Ball(2, new Vector3(side * 180, 330, bed[^1].Z - 8), 30, TtLamp);

        // ---- the wheels: four big balls
        var front = new Vector3(480, 200, 400); var rear = new Vector3(480, 200, -270);
        const float Radius = 200;
        TtWheel(m, (1, 3, 4, 5), front, Radius, violet);
        TtWheel(m, (-1, 6, 7, 8), front, Radius, violet);
        TtWheel(m, (-1, 9, -1, 10), rear, Radius, violet);
        TtWheel(m, (1, 11, -1, 12), rear, Radius, violet);

        // ---- the driver (bone 13, turning at the window's sill)
        m.Pivot(13, m.P(2, new Vector3(TtDriverSide * 304, 530, 410)));
        var (shoulder, neck) = TtDriver(m);
        Arms(m, shoulder, neck);

        var body = m.ToBody(racer.Header);
        // (its box its own: the balls and the driver's head stand out of the racer's car's)
        var world = body.World();
        var box = new[] { world.Min(v => v.X), world.Max(v => v.X), world.Min(v => v.Y), world.Max(v => v.Y), world.Min(v => v.Z), world.Max(v => v.Z) };
        for (var i = 0; i < 6; i++) BinaryPrimitives.WriteInt32LittleEndian(body.Header.AsSpan(8 + i * 4), (int)MathF.Round(box[i]));
        return body;
    }

    // The driver (bone 13), in the car's coordinates (his side's x as TtDriverSide has it): his shoulders and chest in the window, his head
    // out of it, his arm out over the door, the cigarette and its smoke. Returns two of his points for the racer's arms' bones (Arms).
    private static (int Shoulder, int Neck) TtDriver(CarMesh m)
    {
        Vector3 S(Vector3 v) => v with { X = TtDriverSide * v.X };
        // a ring of `n` points round the way `d` goes, `r` across, its first point towards +z (so rings of a bent arm line up)
        int[] Round(Vector3 centre, Vector3 d, float r, int n)
        {
            d = Vector3.Normalize(d);
            var u = Vector3.Normalize(Vector3.UnitZ - d * Vector3.Dot(Vector3.UnitZ, d));
            return m.Loop(13, centre, u * r, Vector3.Cross(d, u) * r, n);
        }
        // a tube from a to b, `ra` across at a and `rb` at b; returns both rings
        (int[] A, int[] B) Tube(Vector3 a, Vector3 b, float ra, float rb, int n, Func<int, int> colour)
        {
            var ringA = Round(a, b - a, ra, n); var ringB = Round(b, b - a, rb, n);
            m.Skin(ringA, ringB, colour, (a + b) / 2);
            return (ringA, ringB);
        }

        // ---- his shoulders and the top of his chest: a white yoke across the window, its outer end his shoulder out of it; on its front
        // his T-shirt's dark print, a pale scrawl across it
        m.Light = SoftShare;
        var inner = S(new Vector3(215, 602, 392)); var outer = S(new Vector3(352, 612, 382));
        var yokeUp = Vector3.UnitY * 46; var yokeFore = Vector3.UnitZ * 66;
        int[] Hexagon(Vector3 c) => Enumerable.Range(0, 6).Select(k => m.P(13, c + yokeUp * MathF.Cos(k * MathF.Tau / 6) + yokeFore * MathF.Sin(k * MathF.Tau / 6))).ToArray();
        var yokeIn = Hexagon(inner); var yokeOut = Hexagon(outer);
        // (panel k between point k and k + 1: 0 the top's front, 1 the chest -- the print --, 2 the front's bottom, 3-5 underneath and the back)
        m.Skin(yokeIn, yokeOut, k => k == 1 ? TtPrint : TtShirt, (inner + outer) / 2);
        m.Out(new[] { yokeOut[0], yokeOut[1], yokeOut[2], yokeOut[3] }, TtShirt, inner);
        m.Out(new[] { yokeOut[3], yokeOut[4], yokeOut[5], yokeOut[0] }, TtShirt, inner);
        // (the scrawl on the part of the chest out of the window)
        m.Line(m.P(13, Vector3.Lerp(m.At(yokeIn[1]), m.At(yokeOut[1]), 0.66f)), m.P(13, Vector3.Lerp(m.At(yokeIn[2]), m.At(yokeOut[2]), 0.92f)), TtPrintLine);
        m.Line(m.P(13, Vector3.Lerp(m.At(yokeIn[1]), m.At(yokeOut[1]), 0.9f)), m.P(13, Vector3.Lerp(m.At(yokeIn[2]), m.At(yokeOut[2]), 0.7f)), TtPrintLine);

        // ---- his arm out over the door: the sleeve from the shoulder, the bare arm out to the elbow, the forearm hanging down the door, his
        // hand
        var shoulderAt = outer + S(new Vector3(-8, -8, -10));
        var elbow = S(new Vector3(445, 590, 340)); var wrist = S(new Vector3(436, 462, 312)); var hand = S(new Vector3(431, 428, 304));
        var sleeveEnd = Vector3.Lerp(shoulderAt, elbow, 0.5f);
        m.Light = SoftShare;
        Tube(shoulderAt, sleeveEnd, 42, 38, 5, _ => TtShirt);
        m.Light = TtHeadShare;
        var upper = Tube(sleeveEnd, elbow, 27, 25, 5, _ => TtSkin);
        m.Skin(upper.B, Round(wrist, wrist - elbow, 20, 5), _ => TtSkin, (elbow + wrist) / 2);
        m.Light = 1;
        m.Ball(13, hand, 28, TtSkinFlat);

        // ---- his head, out of the window, turned a little out and tilted
        // (a little bigger than life: he is the car's character)
        const float HeadScale = 1.2f;
        var chin = S(new Vector3(400, 680, 452));
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, TtDriverSide * 15 * MathF.PI / 180) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -TtDriverSide * 8 * MathF.PI / 180);
        Vector3 Head(Vector3 local) => chin + Vector3.Transform(local with { X = TtDriverSide * local.X } * HeadScale, turn);
        var (neckTop, mouthCorner) = TtHead(m, Head, HeadScale);

        // ---- his neck, from the yoke up into his head
        var neckFoot = S(new Vector3(318, 648, 400));
        m.Light = TtHeadShare;
        var neckRings = Tube(neckFoot, neckTop, 40, 34, 5, _ => TtSkin);

        // ---- the cigarette in the corner of his mouth, its ember, and the smoke streaming back
        m.Light = 1;
        var tip = Head(new Vector3(62, 40, 110));
        m.Line(m.P(13, mouthCorner), m.P(13, tip), TtCigarette);
        m.Ball(13, tip, 10, TtEmber);
        foreach (var (offset, radius, colour) in new[] { (new Vector3(10, 20, -26), 11, 58), (new Vector3(24, 40, -128), 16, 59), (new Vector3(36, 58, -250), 22, 60), (new Vector3(46, 72, -390), 29, 61) })
            m.Ball(13, tip + S(offset), radius, colour);
        return (yokeOut[0], neckRings.A[0]);
    }

    // His head, from rings up it -- chin, jaw, the grin's top, the moustache's top, the eyes, the brow, the hairline -- and a crown, in its
    // own coordinates (the chin's middle at 0, up y, the face towards +z, his right +x) placed by `place`: the face's features in its panels
    // (the grin, the moustache), the hair over the back, the sides and the crown, wavy tufts round it; his nose, ears and eyes, the glasses'
    // frames and his eyebrows. Returns where his neck goes into it and his mouth's corner (the cigarette's), placed.
    private static (Vector3 Neck, Vector3 Mouth) TtHead(CarMesh m, Func<Vector3, Vector3> place, float scale)
    {
        int P(Vector3 local) => m.P(13, place(local));
        // (the rings' points round from the face's right: panel k between point k and k + 1 -- 0 the right cheek, 1 the right side, 2 the right
        // of the back, 3 the back, 4 its left, 5 the left side, 6 the left cheek, 7 the face's middle)
        var angles = new[] { 28f, 75, 120, 160, 200, 240, 285, 332 };
        var rings = new (float Y, float Rx, float Front, float Back, float Zc)[]
        {
            (0, 30, 28, 18, 26), (36, 58, 64, 50, 12), (58, 66, 74, 64, 8), (80, 71, 80, 74, 4), (118, 75, 80, 84, 0), (152, 75, 76, 88, -4), (185, 64, 58, 78, -8),
        };
        Vector3 On(int r, float degrees)
        {
            var (y, rx, front, back, zc) = rings[r];
            var a = degrees * MathF.PI / 180;
            return new Vector3(rx * MathF.Sin(a), y, zc + (MathF.Cos(a) >= 0 ? front : back) * MathF.Cos(a));
        }
        m.Light = TtHeadShare;
        var points = Enumerable.Range(0, rings.Length).Select(r => angles.Select(a => P(On(r, a))).ToArray()).ToArray();
        const int Face = 7;
        int Colour(int band, int k) => band switch
        {
            1 => k == Face ? -1 : TtSkin,
            2 => k == Face ? TtMoustache : k is 2 or 3 or 4 ? TtHair : TtSkin,
            3 => k is 2 or 3 or 4 ? TtHair : TtSkin,
            4 => k is 1 or 2 or 3 or 4 or 5 ? TtHair : TtSkin,
            5 => k == Face ? TtSkin : TtHair,
            _ => TtSkin,
        };
        var middle = new Vector3(0, 100, 0);
        for (var b = 0; b + 1 < rings.Length; b++)
            for (var k = 0; k < 8; k++)
            {
                var quad = new[] { points[b][k], points[b][(k + 1) % 8], points[b + 1][(k + 1) % 8], points[b + 1][k] };
                var colour = Colour(b, k);
                if (colour >= 0) { m.Out(quad, colour, place(middle)); continue; }
                // (the grin: his teeth along its top, open and dark red under them -- unlit)
                int l = P(Vector3.Lerp(On(1, angles[Face]), On(2, angles[Face]), 0.45f)), r = P(Vector3.Lerp(On(1, angles[0]), On(2, angles[0]), 0.45f));
                m.Out(new[] { l, r, points[b + 1][0], points[b + 1][Face] }, TtTeeth, place(middle), unlit: true);
                m.Out(new[] { points[b][Face], points[b][0], r, l }, TtMouth, place(middle), unlit: true);
            }
        m.Cap(points[0], P(new Vector3(0, -6, 30)), TtSkin, place(middle));
        m.Cap(points[^1], P(new Vector3(0, 212, -12)), TtHair, place(middle));
        m.Light = 1;

        // his nose, ears and eyes; the glasses' frames -- rectangles, a bridge, the arms back to his ears -- and his eyebrows over them
        m.Ball(13, place(new Vector3(0, 90, 94)), (int)(20 * scale), TtSkinFlat);
        foreach (var side in Sides)
        {
            m.Ball(13, place(new Vector3(side * 78, 100, 4)), (int)(17 * scale), TtSkinFlat);
            m.Ball(13, place(new Vector3(side * 27, 120, 80)), (int)(8 * scale), TtFrame);
        }
        int[] Lens(float side) => new[] { P(new(side * 8, 134, 85)), P(new(side * 50, 134, 66)), P(new(side * 50, 106, 66)), P(new(side * 8, 106, 85)) };
        var right = Lens(1); var left = Lens(-1);
        foreach (var lens in new[] { right, left }) for (var k = 0; k < 4; k++) m.Line(lens[k], lens[(k + 1) % 4], TtFrame);
        m.Line(right[0], left[0], TtFrame);
        m.Line(right[1], points[4][1], TtFrame); m.Line(left[1], points[4][6], TtFrame);
        foreach (var side in Sides) m.Line(P(new(side * 9, 143, 85)), P(new(side * 52, 149, 64)), TtBrow);

        // his hair's waves: tufts over the crown, the forelock and the sides, salt and pepper
        foreach (var (at, radius, colour) in new[]
        {
            (new Vector3(4, 206, 34), 32, 52), (new Vector3(-40, 200, 8), 30, 55), (new Vector3(42, 198, 4), 30, 57), (new Vector3(0, 212, -36), 34, 53),
            (new Vector3(-62, 172, -30), 27, 56), (new Vector3(64, 170, -32), 27, 52), (new Vector3(0, 186, -72), 30, 54), (new Vector3(30, 172, 58), 22, 57),
        })
            m.Ball(13, place(at), (int)(radius * scale), colour);
        return (place(new Vector3(0, 30, -16)), place(Vector3.Lerp(On(1, 28), On(2, 28), 0.5f) + new Vector3(0, 0, -4)));
    }

    // Adds the toy dump truck to a game folder whose race tracks are built (at the very end of the build: RaceTrackService.Build): its body and
    // its half-size copy at the end of BODY.HQR, the racer's entity's bodies ToyTruckGeneric and its half-size one, and its line in
    // RACECARS.JSON. Returns lines for the build's log.
    public static List<string> InstallToyTruck(string gameDirectory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var racer = Body.Read(HqrArchive.Open(bodyPath).Read(RaceTrackBaldinoCar.RacerBody), 2);
        Body car;
        try { car = BuildToyTruck(racer); }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException) { return new() { $"{ToyTruckName}: not made -- {e.Message}" }; }
        var bytes = car.Write();
        var file = File.ReadAllBytes(bodyPath);
        var own = HqrArchive.CountEntries(bodyPath);
        file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(bytes));
        file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(RaceTrackSmallCars.Halved(bytes)));
        var small = own + 1;
        File.WriteAllBytes(bodyPath, file);
        var table = HqrArchive.Open(ressPath).Read(44);
        table = RaceTrackBaldinoCar.WithBody(table, RaceTrackBaldinoCar.RacerEntity, ToyTruckGeneric, own);
        table = RaceTrackBaldinoCar.WithBody(table, RaceTrackBaldinoCar.RacerEntity, RaceTrackSmallCars.SmallOf(ToyTruckGeneric), small);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        // (its line after the others': the folder's cars, as InstallCast and InstallPumpkin wrote them)
        var catalogue = Catalogue(gameDirectory).Where(e => e.Number != ToyTruckNumber).ToList();
        catalogue.Add(new CatalogueEntry(ToyTruckNumber, ToyTruckName, ToyTruckDriver, "", RaceTrackBaldinoCar.RacerEntity, ToyTruckGeneric, own, small));
        File.WriteAllText(Path.Combine(gameDirectory, CatalogueFile), JsonSerializer.Serialize(catalogue, new JsonSerializerOptions { WriteIndented = true }));
        return new()
        {
            $"{ToyTruckName} (race car {ToyTruckNumber}, anyone's to drive): BODY.HQR entry {own} ({car.Faces.Count} polygons, {car.Lines.Count} lines, {car.Spheres.Count} spheres, " +
            $"{car.Vertices.Count} points), its half-size copy {small}; the racer's entity ({RaceTrackBaldinoCar.RacerEntity}) bodies {ToyTruckGeneric} and {RaceTrackSmallCars.SmallOf(ToyTruckGeneric)}",
        };
    }

    // Adds the toy truck's violet-wheeled copy (BuildToyTruck's violet) and its half-size copy to a game folder InstallToyTruck has put the
    // truck in: at the very end of BODY.HQR -- after everything a build installs (RaceTrackService.Build: after the Temple of Bú's flag
    // marshal), so no other body moves -- and named in the truck's RACECARS.JSON line (Violet). Not in the entity table: the engine's
    // drive_as= names BODY.HQR bodies, and Play's car file names this copy's on the islands VioletIslands finds
    // (RaceCarEngineFile.DriveAsIslandLines); the Race car window still lists the one truck. Returns lines for the build's log.
    public static List<string> InstallToyTruckViolet(string gameDirectory)
    {
        var catalogue = Catalogue(gameDirectory);
        var index = catalogue.FindIndex(e => e.Number == ToyTruckNumber);
        if (index < 0) return new() { $"{ToyTruckName}'s violet copy: not made -- the folder has no {ToyTruckName}" };
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var racer = Body.Read(HqrArchive.Open(bodyPath).Read(RaceTrackBaldinoCar.RacerBody), 2);
        Body car;
        try { car = BuildToyTruck(racer, violet: true); }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException) { return new() { $"{ToyTruckName}'s violet copy: not made -- {e.Message}" }; }
        var bytes = car.Write();
        var file = File.ReadAllBytes(bodyPath);
        var own = HqrArchive.CountEntries(bodyPath);
        file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(bytes));
        file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(RaceTrackSmallCars.Halved(bytes)));
        File.WriteAllBytes(bodyPath, file);
        catalogue[index] = catalogue[index] with { Violet = new[] { own, own + 1 } };
        File.WriteAllText(Path.Combine(gameDirectory, CatalogueFile), JsonSerializer.Serialize(catalogue, new JsonSerializerOptions { WriteIndented = true }));
        var islands = VioletIslands(gameDirectory);
        return new()
        {
            $"{ToyTruckName}'s violet-wheeled copy: BODY.HQR entry {own}, its half-size copy {own + 1}; drawn on " +
            (islands.Count == 0 ? "no island (no race track's palette here draws the violet)" : string.Join(", ", islands.OrderBy(n => n))) + ", the mauve one everywhere else",
        };
    }

    // Whether a palette (an island's RESS.HQR XPL entry) draws TtViolet as a strong violet: the colour its shading table's ShadeNormalLevel
    // row turns the slot into -- the colour a polygon in it is drawn in -- of a hue 255-295 degrees, at least 60 % saturated and 40 % bright.
    internal static bool DrawsViolet(byte[] xpl)
    {
        if (xpl.Length < 28) return false;
        int at = BinaryPrimitives.ReadInt32LittleEndian(xpl.AsSpan(4)), table = BinaryPrimitives.ReadInt32LittleEndian(xpl.AsSpan(12)),
            level = BinaryPrimitives.ReadInt32LittleEndian(xpl.AsSpan(24));
        var row = table + level * 256 + TtViolet;
        if (at < 0 || at + 768 > xpl.Length || row < 0 || row >= xpl.Length) return false;
        var c = xpl[row];
        float r = xpl[at + c * 3] / 255f, g = xpl[at + c * 3 + 1] / 255f, b = xpl[at + c * 3 + 2] / 255f;
        float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        if (max < 0.4f || max - min < 0.6f * max) return false;
        var hue = max == r ? 60 * ((g - b) / (max - min)) : max == g ? 60 * (2 + (b - r) / (max - min)) : 60 * (4 + (r - g) / (max - min));
        if (hue < 0) hue += 360;
        return hue is >= 255 and <= 295;
    }

    // The race tracks' islands, by name, whose palette -- the one their race draws with: the island's own look (island_palette=, the Temple
    // of Bú's) or its PaletteEntry -- draws the violet (DrawsViolet), as the game folder's RESS.HQR has them: decided from the palettes, so
    // an island given a palette with the violet free gets the violet copy. 2026-10-18: the Temple of Bú's alone.
    internal static HashSet<string> VioletIslands(string gameDirectory)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(gameDirectory, "RESS.HQR");
        if (!File.Exists(path)) return names;
        HqrArchive ress; int count;
        try { ress = HqrArchive.Open(path); count = HqrArchive.CountEntries(path); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { DebugLog.Log($"RaceTrackCharacterCars.VioletIslands: {e.Message}"); return names; }
        var known = new Dictionary<int, bool>();
        foreach (var island in RaceTrackIsland.All)
        {
            var entry = island.Look?.Palette ?? island.PaletteEntry;
            if (!known.TryGetValue(entry, out var violet))
            {
                // (a palette the folder hasn't -- an island built after it, Polar Island's own -- draws none)
                try { violet = entry >= 0 && entry < count && DrawsViolet(ress.Read(entry)); }
                catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or IndexOutOfRangeException) { violet = false; }
                known[entry] = violet;
            }
            if (violet) names.Add(island.Name);
        }
        return names;
    }
}
