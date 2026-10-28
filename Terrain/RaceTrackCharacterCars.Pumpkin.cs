using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Text.Json;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// Twinsen's pumpkin F1, the Temple of Bú's own car (the user, 2026-10-17: "Let's create a pumpkin inspired F1 car for our Temple track for
// Twinsen to drive"): a Formula 1 single-seater grown as a pumpkin --
//
//   the body     a long low nose rising to the cockpit, and round it the sidepods and the engine cover as one big pumpkin lying along the
//                car, its lobes ribbed down its length, in the fire oranges the Temple's own pumpkins are (241 on: the same in every
//                island's palette), taking a part of the light so they shade from red-orange to amber
//   the faces    the big pumpkin's front a jack-o'-lantern's carved face, glowing: the sidepods' inlets its eyes, the car's nose its nose,
//                a toothy grin under it; on its back a small one, its grin the rain light, the exhaust's glowing ember its nose
//   the stalk    the airbox behind Twinsen's head: the pumpkin's ridged olive stalk, bent forward, its cut end the intake
//   the leaves   the wings green, their endplates pumpkin leaves, veined; a vine's tendril curling over the rear wing
//   the wheels   four exposed wheels on thin black wishbones, black slicks (the rear ones with an orange band round the wall), green
//                wheels with orange caps
//   Twinsen      low in the cockpit as an F1 driver sits, his shoulders, head and ponytail out of it
//
// It is a body of the racer's entity (157: its driving animation bounces it and its wheels, as it does the other cars), made with the
// cars' kit (CarMesh), but not one of the characters' cars (All): it goes into a game folder after every track's bodies (InstallPumpkin, at
// the end of a build: no body before it moves), with its half-size copy, as race car PumpkinNumber of RACECARS.JSON. Twinsen drives it on
// the Temple of Bú when the car setup leaves him his own car (TrackCar; RaceCarSetup.OwnBuggy), and anywhere as any of the other cars (the
// Race car window's list, the race game's car page). It is only drawn in the buggy's place (RACEMOD.CPP drive_as=): it handles as his
// buggy does.
internal static partial class RaceTrackCharacterCars
{
    // race car 220 (RACECARS.JSON: after the cast's 64-217 and the two laser cars folders built on 2026-10-08 list), the racer entity's
    // body 64 (its half-size copy 164)
    public const int PumpkinNumber = 220, PumpkinGeneric = 64, TwinsenBody = 1;
    public const string PumpkinName = "Pumpkin F1";
    public static readonly Car Pumpkin = new(PumpkinName, "Twinsen", TwinsenBody, PumpkinGeneric, BuildPumpkin, "The Temple of Bú");

    // The car a track has for Twinsen of its own (RaceCarSetup.OwnBuggy: the track's own car), by its race track entry's name: its race car
    // number, or null -- his buggy.
    public static int? TrackCar(string island) => string.Equals(island, RaceTrackIsland.Temple.Name, StringComparison.OrdinalIgnoreCase) ? PumpkinNumber
        : string.Equals(island, RaceTrackIsland.Polar.Name, StringComparison.OrdinalIgnoreCase) ? DiggerNumber : null;
    // Whether a track's own car takes the place of `driveAs`: his own buggy -- and on Polar Island his laser buggies too, the digger his car
    // on the island whatever buggy he has (the user, 2026-10-27: "Twinsen's buggy is supposed to be swapped out for the tractor")
    public static bool TrackCarFor(string island, int driveAs) => driveAs == RaceCarSetup.OwnBuggy ||
        string.Equals(island, RaceTrackIsland.Polar.Name, StringComparison.OrdinalIgnoreCase) && driveAs is RaceCarSetup.LaserGreen or RaceCarSetup.LaserRed;

    // The pumpkin's skin and the glow (the fire colours, 240..247, the same in every island's palette: the Temple's are as they are, other
    // islands' tables take 241-244 to their own oranges and golds), the grooves' lines, the stalk's olive greens and the leaves' greens, the
    // carbon black. A lit colour takes a share of the light (CarMesh.Light): the skin a quarter of its nine steps, two or three colours on.
    private const int PkSkin = 241, PkGroove = 85, PkGlow = 244, PkGlowHot = 245, PkCap = 242, PkStalk = 120, PkStalkDark = 117, PkLeaf = 137, PkLeafLit = 133,
        PkWing = 135, PkVein = 139, PkVine = 134, PkCarbon = 48, PkRim = 132;
    private const float PkSkinShare = 0.28f, PkGreenShare = 0.55f, PkStalkShare = 0.42f, PkRimShare = 0.5f;

    // A section across a part of the car: where along it, the height of its middle, its half width, how far it reaches above and below
    // its middle.
    private readonly record struct Section(float Z, float Cy, float Rx, float Top, float Bottom);

    // A point round a section: a superellipse of `power`, at an angle from the top round towards +x, `scale` of the way out.
    private static Vector3 SectionPoint(Section s, float a, float power, float scale = 1)
    {
        float sin = MathF.Sin(a), cos = MathF.Cos(a);
        float x = MathF.Sign(sin) * MathF.Pow(MathF.Abs(sin), 2 / power), y = MathF.Sign(cos) * MathF.Pow(MathF.Abs(cos), 2 / power);
        return new Vector3(s.Rx * x * scale, s.Cy + (y >= 0 ? s.Top : s.Bottom) * y * scale, s.Z);
    }

    // A part's section at any z (between the two it lies between; the sections run from the front back).
    private static Section Across(Section[] part, float z)
    {
        var i = 0;
        while (i + 2 < part.Length && part[i + 1].Z > z) i++;
        var t = Math.Clamp((part[i].Z - z) / (part[i].Z - part[i + 1].Z), 0, 1);
        Section a = part[i], b = part[i + 1];
        return new Section(z, a.Cy + (b.Cy - a.Cy) * t, a.Rx + (b.Rx - a.Rx) * t, a.Top + (b.Top - a.Top) * t, a.Bottom + (b.Bottom - a.Bottom) * t);
    }

    // The top of a part over (x, z).
    private static float TopOf(Section[] part, float power, float x, float z)
    {
        var s = Across(part, z);
        var u = Math.Clamp(Math.Abs(x) / s.Rx, 0, 1);
        return s.Cy + s.Top * MathF.Pow(1 - MathF.Pow(u, power), 1 / power);
    }

    // A carved grin, glowing: `width` across and `height` high round `centre`, on a face whose own directions are `across` and `up`, seen
    // from `facing`; its upper edge toothed (`steps` pieces: a tooth every other one).
    private static void Grin(CarMesh m, Vector3 centre, Vector3 across, Vector3 up, float width, float height, int steps, Vector3 facing)
    {
        Vector3 At(float x, float v) => centre + across * x + up * v;
        var look = centre + facing * 1000;
        // (the lower edge a smile; the upper one a smile too, less bent, down to it at the corners; a tooth hangs into every other piece)
        float Low(float t) => -height / 2 * (1 - t * t) + height * 0.2f;
        var low = new int[steps + 1]; var high = new int[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var t = -1 + 2f * i / steps;
            low[i] = m.P(2, At(t * width / 2, Low(t)));
            var open = height * (1 - t * t * t * t) * (i % 2 == 1 ? 0.45f : 0.75f);
            high[i] = i == 0 || i == steps ? low[i] : m.P(2, At(t * width / 2, Low(t) + open));
        }
        for (var i = 0; i < steps; i++)
        {
            var hs = i == 0 ? new[] { low[0], low[1], high[1] } : i == steps - 1 ? new[] { low[i], low[i + 1], high[i] } : new[] { low[i], low[i + 1], high[i + 1], high[i] };
            m.In(hs, PkGlow, look, unlit: true);
        }
    }

    // A carved eye, glowing: a triangle `width` across its foot and `height` high round `centre`, its point leaning in towards the car's
    // middle, on a face seen from `facing`.
    private static void Eye(CarMesh m, Vector3 centre, float width, float height, Vector3 facing)
    {
        var lean = -MathF.Sign(centre.X) * width * 0.12f;
        m.In(new[] { m.P(2, centre + new Vector3(-width / 2, -height / 2, 0)), m.P(2, centre + new Vector3(width / 2, -height / 2, 0)), m.P(2, centre + new Vector3(lean, height / 2, 0)) },
            PkGlow, centre + facing * 1000, unlit: true);
    }

    // A pumpkin leaf, standing up (an endplate): its middle, its own directions (along it, up it), how far it reaches along and up, seen
    // from both sides (or, with `outward`, from that side only); its vein from the middle up to its top lobe's tip.
    private static void Leaf(CarMesh m, Vector3 middle, Vector3 along, Vector3 up, float halfLength, float halfHeight, Vector3? outward = null)
    {
        // (the outline round from the notch at its foot: the lower lobes, the sinuses, the top lobe; a share of the way out at each angle)
        var outline = new (float Angle, float Out)[] { (270, 0.3f), (335, 0.95f), (40, 0.62f), (95, 1f), (145, 0.62f), (205, 0.95f) };
        // (a leaf seen from both sides isn't lit -- its two faces' normals cancel out -- one seen from one side is: a darker green, a part of
        // the light)
        var before = m.Light;
        if (outward is not null) m.Light = PkGreenShare;
        var centre = m.P(2, middle);
        var rim = outline.Select(o =>
        {
            var a = o.Angle * MathF.PI / 180;
            return m.P(2, middle + along * (MathF.Cos(a) * halfLength * o.Out) + up * (MathF.Sin(a) * halfHeight * o.Out));
        }).ToArray();
        m.Light = before;
        for (var k = 0; k < rim.Length; k++)
        {
            var tri = new[] { rim[k], rim[(k + 1) % rim.Length], centre };
            if (outward is { } o) m.Out(tri, PkLeafLit, middle - o * 10);
            else m.Both(tri, PkLeaf);
        }
        m.Line(centre, rim[3], PkVein);
    }

    // A wheel (an axle's, on its side): its wishbones from the body -- the hull's points of the upper one and of the lower one, fore and
    // aft -- to the upright inside the wheel (the strut's bone, which turns where the lower one leaves the body), its hub (a front wheel's,
    // which steers about the upright) and the wheel itself: a slick tyre, a green wheel with an orange cap, and (`band`) an orange band
    // round the tyre's wall.
    private static void PumpkinWheel(CarMesh m, (float Side, int Strut, int Hub, int Wheel) bones, Vector3 hub, float radius, float width, int sides,
        Vector3[] arms, bool band)
    {
        var (side, strutBone, hubBone, wheelBone) = bones;
        Vector3 S(Vector3 v) => v with { X = side * v.X };
        hub = S(hub);
        var inner = hub - new Vector3(side * width / 2, 0, 0);
        // the upright, a little inside the wheel: where the wishbones meet it (a rear one's axle too: its wheel turns there); the wishbones
        // thin black plates, seen from above
        m.Light = SoftLight;
        var a = arms.Select(p => m.P(2, S(p))).ToArray();
        var upper = m.P(strutBone, inner + new Vector3(side * 12, radius * 0.5f, 0));
        var lower = m.P(strutBone, inner + new Vector3(side * 12, -radius * 0.55f, 0));
        m.Pivot(strutBone, a[2]);
        m.Up(new[] { a[0], a[1], upper }, PkCarbon);
        m.Up(new[] { a[2], a[3], lower }, PkCarbon);
        m.Light = 1;
        if (hubBone >= 0)
        {
            var hubPoint = m.P(hubBone, hub);
            m.Pivot(hubBone, upper);
            m.Pivot(wheelBone, hubPoint);
        }
        else m.Pivot(wheelBone, m.P(strutBone, inner + new Vector3(side * 12, 0, 0)));

        // the tyre: its tread and its outer wall; the wheel inside the wall, out to its cap
        var face = hub + new Vector3(side * width / 2, 0, 0);
        Vector3 Offset(int k, float r) { var an = k * MathF.Tau / sides + MathF.PI / sides; return new Vector3(0, r * MathF.Cos(an), r * MathF.Sin(an)); }
        m.Light = SoftLight;
        var outer = Enumerable.Range(0, sides).Select(k => m.P(wheelBone, face + Offset(k, radius))).ToArray();
        var innerRing = Enumerable.Range(0, sides).Select(k => m.P(wheelBone, inner + Offset(k, radius))).ToArray();
        m.Light = PkRimShare;
        var rim = Enumerable.Range(0, sides).Select(k => m.P(wheelBone, face + new Vector3(side * 6, 0, 0) + Offset(k, radius * 0.6f))).ToArray();
        var cap = m.P(wheelBone, face + new Vector3(side * 16, 0, 0));
        m.Light = 1;
        for (var k = 0; k < sides; k++)
        {
            var next = (k + 1) % sides;
            m.Out(new[] { outer[k], outer[next], innerRing[next], innerRing[k] }, PkCarbon, hub);
            m.Out(new[] { outer[k], outer[next], rim[next], rim[k] }, PkCarbon, hub - new Vector3(side * width, 0, 0));
            m.Out(new[] { rim[k], rim[next], cap }, PkRim, hub - new Vector3(side * width, 0, 0));
        }
        m.Sphere(cap, (int)(radius * 0.24f), PkCap);
        if (!band) return;
        var ring = Enumerable.Range(0, sides).Select(k => m.P(wheelBone, face + new Vector3(side * 4, 0, 0) + Offset(k, radius * 0.82f))).ToArray();
        for (var k = 0; k < sides; k++) m.Line(ring[k], ring[(k + 1) % sides], PkCap);
    }

    // Twinsen's pumpkin F1.
    public static Body BuildPumpkin(Body twinsen, Body racer)
    {
        var m = new CarMesh(Pumpkin.Name);
        Roots(m);
        var right = new Vector3(1, 0, 0);

        // ---- the nose and the monocoque: one long tube, low and narrow at the front, rising to the cockpit; two grooves down its top
        const float NosePower = 2.4f;
        var nose = new Section[]
        {
            new(850, 200, 30, 24, 24), new(710, 212, 72, 50, 52), new(540, 244, 98, 70, 76), new(360, 288, 118, 88, 98),
            new(150, 326, 138, 100, 114), new(-250, 330, 140, 98, 118),
        };
        m.Light = PkSkinShare;
        var tube = nose.Select(s => Enumerable.Range(0, 8).Select(k => m.P(2, SectionPoint(s, (k + 0.5f) * MathF.Tau / 8, NosePower))).ToArray()).ToList();
        for (var i = 0; i + 1 < tube.Count; i++)
            m.Skin(tube[i], tube[i + 1], _ => PkSkin, new Vector3(0, (nose[i].Cy + nose[i + 1].Cy) / 2, (nose[i].Z + nose[i + 1].Z) / 2));
        m.Cap(tube[0], m.P(2, new(0, 198, 890)), PkSkin, new Vector3(0, 200, 820));
        foreach (var k in new[] { 0, 7 })
            for (var i = 0; i + 1 < tube.Count; i++) m.Line(tube[i][k], tube[i + 1][k], PkGroove);

        // ---- the sidepods and the engine cover: one big pumpkin lying along the car, its lobes running its length (each lobe's points
        // its own: lit round, darker in the grooves), its front face with the sidepods' inlets, its back the gearbox's
        const int Lobes = 6; const float Groove = 0.8f, BodyPower = 2.2f;
        var pumpkin = new Section[]
        {
            new(195, 215, 300, 105, 130), new(60, 220, 358, 125, 145), new(-180, 218, 330, 118, 145), new(-400, 212, 210, 100, 132),
            new(-545, 212, 120, 80, 115),
        };
        m.Light = PkSkinShare;
        // (each ring: per lobe its groove, its ridge and the next groove -- a groove along the top, under the monocoque and the spine, the
        // ridges out at the sides)
        var lobes = pumpkin.Select(s => Enumerable.Range(0, Lobes).Select(j => new[] { 0f, 0.5f, 1f }
            .Select(t => m.P(2, SectionPoint(s, (j + t) * MathF.Tau / Lobes, BodyPower, t == 0.5f ? 1 : Groove))).ToArray()).ToArray()).ToArray();
        for (var i = 0; i + 1 < lobes.Length; i++)
        {
            var inside = new Vector3(0, (pumpkin[i].Cy + pumpkin[i + 1].Cy) / 2, (pumpkin[i].Z + pumpkin[i + 1].Z) / 2);
            for (var j = 0; j < Lobes; j++)
                for (var h = 0; h < 2; h++)
                    m.Out(new[] { lobes[i][j][h], lobes[i][j][h + 1], lobes[i + 1][j][h + 1], lobes[i + 1][j][h] }, PkSkin, inside);
        }
        void Close(int[][] ring, Vector3 middle, Vector3 inside)
        {
            var c = m.P(2, middle);
            foreach (var lobe in ring) { m.Out(new[] { lobe[0], lobe[1], c }, PkSkin, inside); m.Out(new[] { lobe[1], lobe[2], c }, PkSkin, inside); }
        }
        Close(lobes[0], new Vector3(0, 215, 212), new Vector3(0, 215, 120));
        Close(lobes[^1], new Vector3(0, 212, -560), new Vector3(0, 212, -480));
        // (its grooves, down the upper half and the lower: darker lines)
        foreach (var j in new[] { 1, 2, 4, 5 })
            for (var i = 0; i + 1 < lobes.Length; i++) m.Line(lobes[i][j][0], lobes[i + 1][j][0], PkGroove);
        m.Light = 1;

        // ---- the carved faces, glowing: on the pumpkin's front its eyes the sidepods' inlets, the car's nose its nose, a toothy grin under
        // it; on its back two small eyes and a grin for the rain light
        foreach (var side in Sides) Eye(m, new Vector3(side * 222, 226, 209), 116, 66, Vector3.UnitZ);
        Grin(m, new Vector3(0, 150, 211), right, Vector3.UnitY, 300, 76, 6, Vector3.UnitZ);
        foreach (var side in Sides) Eye(m, new Vector3(side * 50, 214, -566), 54, 34, -Vector3.UnitZ);
        Grin(m, new Vector3(0, 152, -566), right, Vector3.UnitY, 150, 50, 4, -Vector3.UnitZ);

        // ---- the engine cover's spine behind the cockpit, up to the stalk and down to the gearbox
        const float SpinePower = 2.2f;
        var spine = new Section[] { new(-170, 352, 100, 92, 40), new(-320, 352, 96, 150, 40), new(-470, 300, 70, 62, 40) };
        m.Light = PkSkinShare;
        var spineRings = spine.Select(s => Enumerable.Range(0, 6).Select(k => m.P(2, SectionPoint(s, (k + 0.5f) * MathF.Tau / 6, SpinePower))).ToArray()).ToList();
        for (var i = 0; i + 1 < spineRings.Count; i++)
            m.Skin(spineRings[i], spineRings[i + 1], _ => PkSkin, new Vector3(0, (spine[i].Cy + spine[i + 1].Cy) / 2, (spine[i].Z + spine[i + 1].Z) / 2));
        m.Cap(spineRings[^1], m.P(2, new(0, 285, -575)), PkSkin, new Vector3(0, 300, -440));
        for (var i = 0; i + 1 < spineRings.Count; i++) { m.Line(spineRings[i][0], spineRings[i + 1][0], PkGroove); m.Line(spineRings[i][5], spineRings[i + 1][5], PkGroove); }
        m.Light = 1;
        m.Ball(2, new Vector3(0, 330, -578), 30, PkGlowHot);

        // ---- the cockpit: its rim, carbon black, and the dark inside
        const float CockpitZ = -70, CockpitRx = 112, CockpitRz = 150;
        var rimTop = TopOf(nose, NosePower, 0, CockpitZ) + 22;
        {
            m.Light = SoftLight;
            const int N = 6;
            var top = new int[N]; var foot = new int[N];
            for (var k = 0; k < N; k++)
            {
                var an = k * MathF.Tau / N;
                float x = CockpitRx * MathF.Sin(an), z = CockpitZ + CockpitRz * MathF.Cos(an);
                top[k] = m.P(2, new(x, rimTop, z));
                foot[k] = m.P(2, new(x, TopOf(nose, NosePower, x, z) - 10, z));
            }
            var floor = m.P(2, new(0, rimTop, CockpitZ));
            for (var k = 0; k < N; k++)
            {
                m.Out(new[] { foot[k], foot[(k + 1) % N], top[(k + 1) % N], top[k] }, PkCarbon, new Vector3(0, rimTop - 60, CockpitZ));
                m.Up(new[] { top[k], top[(k + 1) % N], floor }, HoleDark, unlit: true);
            }
            m.Light = 1;
        }

        // ---- the airbox: the pumpkin's stalk, ridged, behind Twinsen's head and bent forward, its cut end the intake
        {
            m.Light = PkStalkShare;
            // (its rings up it, each across the way it goes there: flared at its foot, bending forward to its cut end)
            var stalk = new (Vector3 Centre, Vector3 Axis, float R)[]
            {
                (new(0, 466, -340), Vector3.UnitY, 74), (new(0, 532, -330), Vector3.Normalize(new Vector3(0, 1, 0.12f)), 50),
                (new(0, 590, -312), Vector3.Normalize(new Vector3(0, 1, 0.45f)), 45), (new(0, 630, -282), Vector3.Normalize(new Vector3(0, 0.7f, 1)), 43),
            };
            var rings = stalk.Select(r => m.Loop(2, r.Centre, right * r.R, Vector3.Normalize(Vector3.Cross(right, r.Axis)) * r.R, 6)).ToArray();
            for (var i = 0; i + 1 < rings.Length; i++)
                m.Skin(rings[i], rings[i + 1], k => k % 2 == 0 ? PkStalk : PkStalkDark, (stalk[i].Centre + stalk[i + 1].Centre) / 2);
            m.Light = 1;
            m.Cap(rings[^1], m.P(2, stalk[^1].Centre - stalk[^1].Axis * 30), HoleDark, stalk[^2].Centre, unlit: true);
        }

        // ---- the rear wing: two green planes between pumpkin leaves, on a stalk from the gearbox; a tendril curling over it
        {
            m.Light = 1;
            m.Plate(2, PkWing, new(-292, 548, -598), new(292, 548, -598), new(292, 592, -700), new(-292, 592, -700));
            m.Plate(2, PkWing, new(-292, 606, -690), new(292, 606, -690), new(292, 694, -738), new(-292, 694, -738));
            m.Light = PkGreenShare;
            m.Strut(2, new Vector3(0, 290, -540), new Vector3(0, 552, -640), 30, PkStalk);
            m.Light = 1;
            foreach (var side in Sides) Leaf(m, new Vector3(side * 300, 575, -668), -Vector3.UnitZ, Vector3.UnitY, 125, 175);
            var curl = new[] { new Vector3(0, 612, -690), new Vector3(40, 720, -700), new Vector3(105, 712, -735), new Vector3(78, 668, -716) };
            var curlPoints = curl.Select(p => m.P(2, p)).ToArray();
            for (var k = 0; k + 1 < curlPoints.Length; k++) m.Line(curlPoints[k], curlPoints[k + 1], PkVine);
        }

        // ---- the front wing: two green planes under the nose, between pumpkin leaves, hung from the nose
        {
            m.Plate(2, PkWing, new(-468, 76, 845), new(468, 76, 845), new(468, 98, 732), new(-468, 98, 732));
            m.Plate(2, PkWing, new(-468, 112, 738), new(468, 112, 738), new(468, 160, 690), new(-468, 160, 690));
            foreach (var side in Sides)
            {
                Leaf(m, new Vector3(side * 474, 132, 772), Vector3.UnitZ, Vector3.UnitY, 105, 82, new Vector3(side, 0, 0));
                m.Rod(2, new Vector3(side * 26, 182, 780), new Vector3(side * 70, 96, 790), PkCarbon);
            }
        }

        // ---- the wheels: the front ones steering, the rear ones bigger, banded
        var frontArms = new[] { new Vector3(118, 300, 480), new Vector3(128, 304, 360), new Vector3(110, 200, 500), new Vector3(122, 196, 340) };
        var rearArms = new[] { new Vector3(215, 285, -320), new Vector3(170, 290, -470), new Vector3(220, 135, -320), new Vector3(175, 135, -470) };
        PumpkinWheel(m, (1, 3, 4, 5), new Vector3(452, 150, 430), 150, 128, 6, frontArms, false);
        PumpkinWheel(m, (-1, 6, 7, 8), new Vector3(452, 150, 430), 150, 128, 6, frontArms, false);
        PumpkinWheel(m, (-1, 9, -1, 10), new Vector3(450, 176, -400), 176, 168, 8, rearArms, true);
        PumpkinWheel(m, (1, 11, -1, 12), new Vector3(450, 176, -400), 176, 168, 8, rearArms, true);

        // ---- Twinsen (bone 13): low in the cockpit, his arms down in it at the wheel, his shoulders, head and ponytail out of it
        {
            m.Light = 1;
            const float Scale = 0.6f;
            var origin = new Vector3(0, 600, 33);
            var seat = new Vector3(0, rimTop - 150, CockpitZ - 12);
            Vector3 Place(Vector3 p) => (p - origin) * Scale + seat;
            m.Pivot(13, m.P(2, seat));
            var driver = new CarDriver(twinsen);
            // (his arms -- 4 and 6 and all that hangs from them -- and his legs left out)
            var point = driver.Seat(m, driver.AllBut(new[] { 4, 6, 8, 11 }), Place, rimTop, Scale, cut: true);
            Arms(m, point(twinsen.Bones[4].Pivot), point(twinsen.Bones[6].Pivot));
        }

        var body = m.ToBody(racer.Header);
        // (its box its own: longer than the racer's car it is made on)
        var world = body.World();
        var box = new[] { world.Min(v => v.X), world.Max(v => v.X), world.Min(v => v.Y), world.Max(v => v.Y), world.Min(v => v.Z), world.Max(v => v.Z) };
        for (var i = 0; i < 6; i++) BinaryPrimitives.WriteInt32LittleEndian(body.Header.AsSpan(8 + i * 4), (int)MathF.Round(box[i]));
        return body;
    }

    // Adds the pumpkin F1 to a game folder whose race tracks are built (at the end of the build: RaceTrackService.Build): its body and its
    // half-size copy at the end of BODY.HQR, the racer's entity's bodies PumpkinGeneric and its half-size one, and its line in RACECARS.JSON.
    // Returns lines for the build's log.
    public static List<string> InstallPumpkin(string gameDirectory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var bodies = HqrArchive.Open(bodyPath);
        var racer = Body.Read(bodies.Read(RaceTrackBaldinoCar.RacerBody), 2);
        Body car;
        try { car = BuildPumpkin(Body.Read(bodies.Read(TwinsenBody), 2), racer); }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException) { return new() { $"{PumpkinName}: not made -- {e.Message}" }; }
        var bytes = car.Write();
        var file = File.ReadAllBytes(bodyPath);
        var own = HqrArchive.CountEntries(bodyPath);
        file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(bytes));
        file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(RaceTrackSmallCars.Halved(bytes)));
        var small = own + 1;
        File.WriteAllBytes(bodyPath, file);
        var table = HqrArchive.Open(ressPath).Read(44);
        table = RaceTrackBaldinoCar.WithBody(table, RaceTrackBaldinoCar.RacerEntity, PumpkinGeneric, own);
        table = RaceTrackBaldinoCar.WithBody(table, RaceTrackBaldinoCar.RacerEntity, RaceTrackSmallCars.SmallOf(PumpkinGeneric), small);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        // (its line after the others': the folder's cars, as InstallCast wrote them)
        var catalogue = Catalogue(gameDirectory).Where(e => e.Number != PumpkinNumber).ToList();
        catalogue.Add(new CatalogueEntry(PumpkinNumber, PumpkinName, Pumpkin.Driver, Pumpkin.Island, RaceTrackBaldinoCar.RacerEntity, PumpkinGeneric, own, small));
        File.WriteAllText(Path.Combine(gameDirectory, CatalogueFile), JsonSerializer.Serialize(catalogue, new JsonSerializerOptions { WriteIndented = true }));
        return new()
        {
            $"{PumpkinName} (race car {PumpkinNumber}, Twinsen's on the Temple of Bú): BODY.HQR entry {own} ({car.Faces.Count} polygons, {car.Lines.Count} lines, {car.Spheres.Count} spheres, " +
            $"{car.Vertices.Count} points), its half-size copy {small}; the racer's entity ({RaceTrackBaldinoCar.RacerEntity}) bodies {PumpkinGeneric} and {RaceTrackSmallCars.SmallOf(PumpkinGeneric)}",
        };
    }
}
