using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// Three more race cars, each made after a character of the game and driven by it (as Baldino's rocket car, RaceTrackBaldinoCar):
//
//   the Queen of the Mosquibees   her own shape on wheels: a fat abdomen in her blue and orange bands ending in a sting, her wings swept
//                                 back over it, her thin blue legs down to the wheels, and her head -- crown, red eyes, antennae, trumpet --
//                                 looking out of a collar made like her crown
//   the Emperor                   a long black staff car: the two rows of gold buttons of his coat down the bonnet, a gold radiator with his
//                                 hat's red cockade, gold epaulettes over the front wheels, a red pennant on each wing, a throne's red seat
//                                 back, and his two-cornered hat as the wing at the back
//   Zoe                           a pink convertible, its tail flaring like her gown with a bow tied on it, a heart on the bonnet, long
//                                 pink wings over the front wheels like her gloves, lamps with lashes; Zoe in the pink gown and gloves
//
// Each is a body of the retail racer's entity (157) as Baldino's car is (bodies 2, 3 and 4), with the racer's car's 18 bones (CarMesh), so
// the racer's animations drive them. The drivers come from the characters' own bodies in BODY.HQR (CarDriver).
//
// The pinks and the black are colours the game's light would spoil -- a lit polygon runs some nine steps up its ramp, and the pinks are the
// last four of the reds (past them the next ramp starts, dark), the black the first four of the greys (past them it is grey). Their points
// take a part of the light instead (CarMesh.Light, Body.LightScale: a shorter normal), so they shade over those few colours only.
internal static partial class RaceTrackCharacterCars
{
    // Island: where its driver belongs (the track it is to race on, once every island has one).
    public sealed record Car(string Name, string Driver, int Character, int Generic, Func<Body, Body, Body> Build, string Island = "");

    public const int QueenBody = 196, EmperorBody = 453, ZoeBody = 26;      // BODY.HQR
    public const string Citadel = "Citadel Island", Desert = "Desert Island", Moon = "Emerald Moon", Otringal = "Otringal", Celebration = "Celebration Island",
        Wannies = "Island of the Wannies", Mosquibees = "Island of the Mosquibees", Francos = "Island of the Francos", IslandCX = "Island CX",
        Elevator = "Elevator Platform Island", UnderCelebration = "Island under Celebration";
    public static readonly Car Queen = new("The Queen's car", "the Queen of the Mosquibees", QueenBody, 2, BuildQueen, Mosquibees);
    public static readonly Car Emperor = new("The Emperor's car", "the Emperor", EmperorBody, 3, BuildEmperor, Otringal);
    public static readonly Car Zoe = new("Zoe's car", "Zoe", ZoeBody, 4, BuildZoe, Citadel);
    // (a property: the cars of the other file are not made yet when this file's fields are)
    public static Car[] All => new[] { Queen, Emperor, Zoe, WeatherWizard, Raph, Dean, Spaceman, Johnny, DarkMonk, Wannie, OldFranco, Survivor }.Concat(More).Concat(Named).Concat(CelebrationCars).ToArray();

    // Palette ramp starts (the engine adds the light), and the colours drawn as they are (spheres, lines, unlit polygons).
    private const int Blue = 192, Orange = 82, Gold = 102, Grey = 48, Red = 66;
    private const int HoleDark = 49, GoldFlat = 107, RedFlat = 70, Lamp = 245, Dark = 48;
    // see-through panes and wings: what is behind them moved into this ramp
    private const int Pane = 160, Wing = 208;
    // the soft-lit colours: a start and the share of the light that keeps it inside its few colours
    // (the light adds up to 9.4 steps: 0.3 of it three colours above the start, 0.26 two and a half -- the lightest pink is two below
    // the end of its ramp)
    private const float SoftLight = 0.3f, PinkShare = 0.26f;
    private const int Pink = 76, PinkLight = 77, PinkDeep = 73, Black = 48, GoldBright = 106, Heart = 72;

    // ---------------------------------------------------------------------------------------------------------------- shared parts

    // The two points of the bones under the hull (the racer's root and bounce); the pivots of bones 1 and 2.
    private static void Roots(CarMesh m)
    {
        m.P(0, new(0, 0, 0)); var root1 = m.P(0, new(0, 66, 0));
        var bounce = m.P(1, new(0, 199, 0));
        m.Pivot(1, root1); m.Pivot(2, bounce);
    }

    // A hull (bone 2): rings across the car from nose (+z) to tail, each a superellipse round (0, Cy, z) -- half width, height above Cy and
    // depth below it -- skinned panel by panel.
    private sealed class Hull
    {
        private readonly (float Z, float Rx, float Top, float Bottom)[] profile;
        public readonly float Cy, Power;
        public readonly List<int[]> Rings;

        public Hull(CarMesh m, float cy, int n, float power, float phase, params (float Z, float Rx, float Top, float Bottom)[] profile)
        {
            this.profile = profile; Cy = cy; Power = power;
            Rings = profile.Select(r => m.Ring(2, new Vector3(0, cy, r.Z), r.Rx, r.Top, r.Bottom, n, power, phase)).ToList();
        }

        // colour(band, panel): bands count from the nose, panels round from the top towards +x
        public void Skin(CarMesh m, Func<int, int, int> colour)
        {
            for (var i = 0; i + 1 < Rings.Count; i++)
            {
                var band = i;
                m.Skin(Rings[i], Rings[i + 1], k => colour(band, k), new Vector3(0, Cy, (profile[i].Z + profile[i + 1].Z) / 2));
            }
        }
        public void Nose(CarMesh m, float z, int colour, float lift = 0) => m.Cap(Rings[0], m.P(2, new(0, Cy + lift, z)), colour, new Vector3(0, Cy, profile[0].Z - 40));
        public void Tail(CarMesh m, float z, int colour, float lift = 0) => m.Cap(Rings[^1], m.P(2, new(0, Cy + lift, z)), colour, new Vector3(0, Cy, profile[^1].Z + 40));

        private (float Rx, float Top, float Bottom) At(float z)
        {
            var i = 0; while (i + 2 < profile.Length && profile[i + 1].Z > z) i++;
            var t = Math.Clamp((profile[i].Z - z) / (profile[i].Z - profile[i + 1].Z), 0, 1);
            return (profile[i].Rx + (profile[i + 1].Rx - profile[i].Rx) * t, profile[i].Top + (profile[i + 1].Top - profile[i].Top) * t,
                    profile[i].Bottom + (profile[i + 1].Bottom - profile[i].Bottom) * t);
        }
        // the top of the hull over (x, z)
        public float Top(float x, float z)
        {
            var (rx, top, _) = At(z);
            var u = Math.Clamp(Math.Abs(x) / rx, 0, 1);
            return Cy + top * MathF.Pow(1 - MathF.Pow(u, Power), 1 / Power);
        }
        public Vector3 OnTop(float x, float z, float above = 0) => new(x, Top(x, z) + above, z);
        // the hull's half width at a height
        public float Side(float y, float z)
        {
            var (rx, top, bottom) = At(z);
            var v = Math.Clamp(Math.Abs(y - Cy) / (y >= Cy ? top : bottom), 0, 1);
            return rx * MathF.Pow(1 - MathF.Pow(v, Power), 1 / Power);
        }
    }

    // The cockpit: an oval hole in the top of the hull with a rim standing round it, dark inside. Returns the rim's top points.
    private static int[] Cockpit(CarMesh m, Hull hull, float z, float rx, float rz, float rimTop, int n, int rim, int floor = HoleDark)
    {
        var top = new int[n]; var foot = new int[n];
        for (var k = 0; k < n; k++)
        {
            var a = k * MathF.Tau / n;
            float x = rx * MathF.Sin(a), pz = z + rz * MathF.Cos(a);
            top[k] = m.P(2, new(x, rimTop, pz));
            foot[k] = m.P(2, new(x, hull.Top(x, pz) - 12, pz));
        }
        var centre = m.P(2, new(0, rimTop, z));
        for (var k = 0; k < n; k++)
        {
            m.Out(new[] { foot[k], foot[(k + 1) % n], top[(k + 1) % n], top[k] }, rim, new Vector3(0, rimTop - 60, z));
            m.Up(new[] { top[k], top[(k + 1) % n], centre }, floor, unlit: true);
        }
        return top;
    }

    // What the four wheels are made of: the struts, the tyres (tread and wall), the wheel inside the tyre and the cap on its hub.
    // (Lean: five-sided, the wall and the wheel sharing their points -- for a car whose driver takes most of the body's points)
    private sealed record WheelLook(int Strut, float StrutThickness, int Tyre, float TyreLight, int Rim, float RimLight, int Cap, int Sides = 6, bool Lean = false);
    // An axle's right-hand wheel (the left one mirrored): where its strut leaves the hull, the wheel's middle, its radius and width.
    private sealed record Axle(Vector3 Mount, Vector3 Hub, float Radius, float Width);

    // The wheels: struts from the hull (bones 3 and 6, 9 and 11) to the hubs (4, 7: the front ones steer) and wheels (5, 8, 10, 12).
    private static void Wheels(CarMesh m, WheelLook look, Axle front, Axle rear)
    {
        foreach (var (side, strutBone, hubBone, wheelBone, isRear) in new[] { (1f, 3, 4, 5, false), (-1f, 6, 7, 8, false), (-1f, 9, -1, 10, true), (1f, 11, -1, 12, true) })
        {
            var axle = isRear ? rear : front;
            var mount = axle.Mount with { X = side * axle.Mount.X }; var centre = axle.Hub with { X = side * axle.Hub.X };
            float radius = axle.Radius, width = axle.Width;
            m.Light = 1;
            var mountPoint = m.P(2, mount);
            var inner = centre - new Vector3(side * width / 2, 0, 0);
            var strutEnd = m.Strut(strutBone, mount, inner, look.StrutThickness, look.Strut);
            m.Pivot(strutBone, mountPoint);
            if (hubBone >= 0)
            {
                var hubPoint = m.P(hubBone, centre);
                m.Pivot(hubBone, strutEnd);
                m.Pivot(wheelBone, hubPoint);
            }
            else m.Pivot(wheelBone, strutEnd);

            // the tyre: its tread and its outer wall (the inner one is hardly ever seen); inside the wall the wheel, out to its cap
            var s = look.Lean ? 5 : look.Sides;
            var outer = new int[s]; var innerRing = new int[s]; var wall = new int[s]; var rim = new int[s];
            var face = centre + new Vector3(side * width / 2, 0, 0);
            Vector3 Offset(int k, float r) { var a = k * MathF.Tau / s + MathF.PI / s; return new Vector3(0, r * MathF.Cos(a), r * MathF.Sin(a)); }
            m.Light = look.TyreLight;
            for (var k = 0; k < s; k++)
            {
                outer[k] = m.P(wheelBone, face + Offset(k, radius));
                innerRing[k] = m.P(wheelBone, inner + Offset(k, radius));
                if (!look.Lean) wall[k] = m.P(wheelBone, face + new Vector3(side * 6, 0, 0) + Offset(k, radius * 0.62f));
            }
            m.Light = look.Lean ? look.TyreLight : look.RimLight;
            for (var k = 0; k < s; k++) rim[k] = m.P(wheelBone, face + new Vector3(side * 6, 0, 0) + Offset(k, radius * 0.62f));
            if (look.Lean) wall = rim;
            var cap = m.P(wheelBone, face + new Vector3(side * 16, 0, 0));
            m.Light = 1;
            for (var k = 0; k < s; k++)
            {
                var next = (k + 1) % s;
                m.Out(new[] { outer[k], outer[next], innerRing[next], innerRing[k] }, look.Tyre, centre);
                m.Out(new[] { outer[k], outer[next], wall[next], wall[k] }, look.Tyre, centre - new Vector3(side * width, 0, 0));
                m.Out(new[] { rim[k], rim[next], cap }, look.Rim, centre - new Vector3(side * width, 0, 0));
            }
            m.Sphere(cap, (int)(radius * 0.26f), look.Cap);
        }
    }

    // A plate arched over a wheel (on the hull, so clear of the wheel as it steers): from one angle to another, measured from straight
    // above the hub towards the front, `gap` above the tyre.
    private static void Mudguard(CarMesh m, Axle axle, float side, float from, float to, int steps, float gap, float extra, int colour)
    {
        var hub = axle.Hub with { X = side * axle.Hub.X };
        float r = axle.Radius + gap, half = axle.Width / 2 + extra;
        var near = new int[steps + 1]; var far = new int[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var a = from + (to - from) * i / steps;
            var at = hub + new Vector3(0, r * MathF.Cos(a), r * MathF.Sin(a));
            near[i] = m.P(2, at - new Vector3(side * half, 0, 0)); far[i] = m.P(2, at + new Vector3(side * half, 0, 0));
        }
        for (var i = 0; i < steps; i++) m.Both(new[] { near[i], near[i + 1], far[i + 1], far[i] }, colour);
    }

    // Bones 14-17 (the racer's driver's arms): one point each, at two points of the driver (bone 13).
    private static void Arms(CarMesh m, int right, int left)
    {
        m.Pivot(14, right); var r14 = m.P(14, m.At(right));
        m.Pivot(15, r14); m.P(15, m.At(right));
        m.Pivot(16, left); var l16 = m.P(16, m.At(left));
        m.Pivot(17, l16); m.P(17, m.At(left));
    }

    // The steering wheel through the driver's hands, on a column down to the hull in front of it.
    private static void SteeringWheel(CarMesh m, Hull hull, Vector3 right, Vector3 left, int colour, int line)
    {
        var centre = (right + left) / 2; var radius = Vector3.Distance(right, left) / 2;
        var across = Vector3.Normalize(right - left); var up = Vector3.Normalize(Vector3.Cross(new Vector3(0, 0.45f, 1), across));
        if (up.Y < 0) up = -up;
        Vector3 At(float r, int i) { var a = i * MathF.Tau / 8; return centre + across * r * MathF.Cos(a) + up * r * MathF.Sin(a); }
        var outer = Enumerable.Range(0, 8).Select(i => m.P(2, At(radius + 14, i))).ToArray();
        var inner = Enumerable.Range(0, 8).Select(i => m.P(2, At(radius - 14, i))).ToArray();
        for (var i = 0; i < 8; i++) m.Both(new[] { outer[i], outer[(i + 1) % 8], inner[(i + 1) % 8], inner[i] }, colour);
        var hubPoint = m.P(2, centre);
        m.Line(inner[0], inner[4], line);
        m.Line(hubPoint, m.P(2, hull.OnTop(centre.X, centre.Z + 110, 4)), line);
    }

    // A seated driver: the character's kept bones, each hand brought to the wheel, smaller, its waist (`origin`, in its own body) at `seat`.
    private static (Vector3 Right, Vector3 Left) SeatDriver(CarMesh m, Body character, HashSet<int> keep, (int Upper, int[] Fore) rightArm, (int Upper, int[] Fore) leftArm,
        Vector3 origin, float scale, Vector3 seat, float rim, Vector3 grip, CarDriver.Painter? paint)
    {
        var driver = new CarDriver(character);
        Vector3 Place(Vector3 p) => (p - origin) * scale + seat;
        Vector3 Unplace(Vector3 p) => (p - seat) / scale + origin;
        var right = driver.Reach(rightArm.Upper, rightArm.Fore, Unplace(grip), 1);
        var left = driver.Reach(leftArm.Upper, leftArm.Fore, Unplace(grip with { X = -grip.X }), -1);
        m.Light = 1;
        m.Pivot(13, m.P(2, seat));
        var point = driver.Seat(m, keep, Place, rim, scale, paint);
        Arms(m, point(character.Bones[rightArm.Upper].Pivot), point(character.Bones[leftArm.Upper].Pivot));
        return (Place(right.Hand), Place(left.Hand));
    }

    // A windscreen round the front of a cockpit: three see-through panes standing on the hull, a rail along their top.
    private static void Windscreen(CarMesh m, Hull hull, float z0, float halfWidth, float height, int rail)
    {
        var bottom = new[] { new Vector3(-halfWidth, 0, z0 - 60), new Vector3(-halfWidth * 0.4f, 0, z0), new Vector3(halfWidth * 0.4f, 0, z0), new Vector3(halfWidth, 0, z0 - 60) };
        var hs = bottom.Select(b => (Bottom: m.P(2, hull.OnTop(b.X, b.Z, -5)), Top: m.P(2, hull.OnTop(b.X * 0.9f, b.Z, 0) + new Vector3(0, height, -height * 0.4f)))).ToArray();
        for (var i = 0; i < 3; i++)
        {
            m.Both(new[] { hs[i].Bottom, hs[i + 1].Bottom, hs[i + 1].Top, hs[i].Top }, Pane, CarMesh.SeeThrough);
            m.Line(hs[i].Top, hs[i + 1].Top, rail);
        }
        m.Line(hs[0].Bottom, hs[0].Top, rail); m.Line(hs[3].Bottom, hs[3].Top, rail);
    }

    // A flame out of the back of something: an unlit cone with a bright ball in its mouth.
    private static void Flame(CarMesh m, Vector3 mouth, float radius, float length)
    {
        var ring = m.Loop(2, mouth, new Vector3(radius, 0, 0), new Vector3(0, radius, 0), 6);
        m.Cap(ring, m.P(2, mouth - new Vector3(0, 0, length)), 242, mouth + new Vector3(0, 0, 40), unlit: true);
        m.Ball(2, mouth - new Vector3(0, 0, length * 0.25f), (int)(radius * 0.7f), 245);
    }

    // ---------------------------------------------------------------------------------------------------------------- the Queen

    // The Queen of the Mosquibees' car: herself, lying on wheels.
    public static Body BuildQueen(Body queen, Body racer)
    {
        var m = new CarMesh(Queen.Name);
        Roots(m);

        // her abdomen: round and fat, banded blue and orange from the front, drawn out to the sting
        var hull = new Hull(m, 345, 10, 2, 0,
            (560, 150, 118, 110), (470, 250, 180, 165), (350, 320, 225, 200), (200, 360, 250, 218), (40, 375, 262, 225),
            (-120, 365, 255, 220), (-270, 325, 228, 200), (-400, 260, 185, 165), (-500, 180, 130, 118), (-570, 95, 70, 65), (-615, 48, 36, 36));
        hull.Skin(m, (band, _) => band == 9 ? Grey : band % 2 == 0 ? Blue : Orange);
        hull.Nose(m, 630, Blue);
        hull.Tail(m, -800, Grey, -12);

        // a collar made like her crown, standing round the hole her head looks out of
        const float HeadZ = 120;
        var rimTop = hull.Top(0, HeadZ) + 28;
        const int N = 10;
        var rim = Cockpit(m, hull, HeadZ, 170, 150, rimTop, N, Gold);
        for (var k = 0; k < N; k++)
        {
            var a = m.At(rim[k]); var b = m.At(rim[(k + 1) % N]);
            var mid = (a + b) / 2; var outward = Vector3.Normalize(new Vector3(mid.X, 0, mid.Z - HeadZ));
            m.Both(new[] { rim[k], rim[(k + 1) % N], m.P(2, mid + outward * 22 + new Vector3(0, 78, 0)) }, Gold);
        }

        // her head (bone 13), larger than life, on a blue neck: crown, eyes, antennae and trumpet as they are in her own body
        {
            const float Scale = 1.25f;
            var origin = new Vector3(0, 1270, 114); var seat = new Vector3(0, rimTop + 62, HeadZ);
            var driver = new CarDriver(queen);
            m.Pivot(13, m.P(2, new(0, rimTop, HeadZ)));
            var foot = m.Loop(13, new Vector3(0, rimTop - 14, HeadZ), new Vector3(118, 0, 0), new Vector3(0, 0, 105), 6);
            var neck = m.Loop(13, new Vector3(0, seat.Y + 26, HeadZ + 10), new Vector3(62, 0, 0), new Vector3(0, 0, 62), 6);
            m.Skin(foot, neck, _ => Blue, new Vector3(0, rimTop + 20, HeadZ));
            driver.Seat(m, new HashSet<int> { 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 29 }, p => (p - origin) * Scale + seat, 0, Scale);
            Arms(m, neck[1], neck[4]);
        }

        // her wings: two a side, see-through, veined, swept up and back from behind her head
        foreach (var side in new[] { 1f, -1f })
        {
            foreach (var (root, lead, tip) in new[]
            {
                (new Vector3(60, 0, -60), new Vector3(330, 890, -300), new Vector3(430, 980, -640)),
                (new Vector3(95, 0, -90), new Vector3(520, 690, -380), new Vector3(610, 700, -700)),
            })
            {
                // a flat kite: the root on her back, the leading edge, the tip, and the trailing corner in their plane
                Vector3 On(Vector3 p) => new(side * p.X, p.Y > 0 ? p.Y : hull.Top(p.X, p.Z) - 4, p.Z);
                Vector3 pa = On(root), pb = On(lead), pc = On(tip), pd = pa + (pc - pa) * 0.8f - (pb - pa) * 0.45f;
                int a = m.P(2, pa), b = m.P(2, pb), c = m.P(2, pc), d = m.P(2, pd);
                m.Both(new[] { a, b, c, d }, Wing, CarMesh.SeeThrough);
                m.Line(a, b, 86); m.Line(b, c, 86); m.Line(c, d, 86); m.Line(a, c, 86);
                m.Line(a, m.P(2, (pb + pc) / 2), 86); m.Line(a, m.P(2, (pc + pd) / 2), 86);
            }
            // her red eyes again, as the lamps
            m.Sphere(m.P(2, new(side * 125, hull.Cy + 70, 515)), 52, RedFlat);
        }

        // her thin blue legs, down to wheels with honey-gold hubs
        Wheels(m, new WheelLook(Blue + 2, 22, Grey, 1, Gold, 1, GoldFlat),
            new Axle(new Vector3(250, 290, 385), new Vector3(430, 150, 385), 150, 105),
            new Axle(new Vector3(285, 300, -300), new Vector3(520, 200, -300), 200, 150));
        return m.ToBody(racer.Header);
    }

    // ---------------------------------------------------------------------------------------------------------------- the Emperor

    // The Emperor's car: a long black staff car, dressed as he is.
    public static Body BuildEmperor(Body emperor, Body racer)
    {
        var m = new CarMesh(Emperor.Name);
        Roots(m);

        // the body: square-shouldered, a long bonnet, black all over
        m.Light = SoftLight;
        var hull = new Hull(m, 300, 12, 4, 0.5f,
            (650, 205, 105, 110), (585, 245, 128, 125), (300, 265, 152, 135), (170, 285, 195, 140), (-300, 285, 195, 140), (-430, 270, 160, 135), (-620, 235, 125, 120));
        hull.Skin(m, (_, _) => Black);
        hull.Nose(m, 662, GoldBright);                                         // the radiator, gold
        hull.Tail(m, -640, Black);
        m.Light = 1;
        m.Sphere(m.P(2, new(0, hull.Cy + 8, 668)), 46, RedFlat);              // his hat's cockade on it
        m.Sphere(m.P(2, new(0, hull.Cy + 8, 676)), 17, GoldFlat);

        // a gold line down each side, and his coat's two rows of gold buttons down the bonnet
        foreach (var k in new[] { 2, 9 })
            for (var i = 0; i + 1 < hull.Rings.Count; i++) m.Line(hull.Rings[i][k], hull.Rings[i + 1][k], GoldFlat);
        foreach (var side in new[] { 1f, -1f })
        {
            foreach (var z in new[] { 245f, 335, 425, 515 }) m.Sphere(m.P(2, hull.OnTop(side * 88, z, 6)), 25, GoldFlat);
            m.Sphere(m.P(2, new(side * 150, hull.Cy + 52, 640)), 56, Lamp);                                     // lamps
            m.Sphere(m.P(2, new(side * 165, hull.Cy + 40, -628)), 32, RedFlat);                                  // tail lights
            // a red pennant on each front wing
            int foot = m.P(2, hull.OnTop(side * 205, 560)), head = m.P(2, hull.OnTop(side * 205, 560, 250));
            m.Line(foot, head, GoldFlat);
            m.Both(new[] { head, m.P(2, m.At(head) + new Vector3(0, -85, 0)), m.P(2, m.At(head) + new Vector3(0, -45, -150)) }, Red);
        }

        var front = new Axle(new Vector3(250, 268, 400), new Vector3(440, 165, 400), 165, 115);
        var rear = new Axle(new Vector3(255, 268, -420), new Vector3(475, 185, -420), 185, 135);
        Wheels(m, new WheelLook(Gold, 26, Black, SoftLight, Gold, 1, GoldFlat), front, rear);
        // epaulettes: gold guards over the front wheels
        foreach (var side in new[] { 1f, -1f }) Mudguard(m, front, side, -1.0f, 1.0f, 3, 38, 12, Gold);

        // the cockpit, rimmed in red leather; behind it a throne's seat back, red with gold knobs
        const float SeatZ = -90, Waist = 500;
        var rimTop = hull.Top(0, SeatZ) + 28;
        Cockpit(m, hull, SeatZ, 228, 195, rimTop, 10, Red);
        {
            int a = m.P(2, new(-185, rimTop - 6, -268)), b = m.P(2, new(185, rimTop - 6, -268)), c = m.P(2, new(150, 830, -312)), d = m.P(2, new(-150, 830, -312));
            m.Both(new[] { a, b, c, d }, Red);
            m.Line(c, d, GoldFlat);
            m.Sphere(c, 24, GoldFlat); m.Sphere(d, 24, GoldFlat);
        }
        // the windscreen: an upright see-through pane in a gold frame
        {
            int a = m.P(2, hull.OnTop(-190, 285, -4)), b = m.P(2, hull.OnTop(190, 285, -4)), c = m.P(2, new(172, 640, 250)), d = m.P(2, new(-172, 640, 250));
            m.Both(new[] { a, b, c, d }, Pane, CarMesh.SeeThrough);
            m.Line(a, d, GoldFlat); m.Line(d, c, GoldFlat); m.Line(c, b, GoldFlat);
        }

        // his hat as the wing at the back: a black crescent on two posts, gold along its top, the cockade in the middle
        {
            const float Z = -560;
            foreach (var side in new[] { 1f, -1f }) m.Strut(2, hull.OnTop(side * 150, Z + 10, -10), new Vector3(side * 150, 585, Z), 20, Gold);
            // (the hat's two brims: folded together along the top, apart below, closed at the pointed ends)
            var xs = new[] { -410f, -230, 0, 230, 410 }; var tops = new[] { 592f, 700, 770, 700, 592 }; var bottoms = new[] { 572f, 584, 590, 584, 572 }; var apart = new[] { 0f, 34, 46, 34, 0 };
            m.Light = SoftLight;
            var top = xs.Select((x, i) => m.P(2, new(x, tops[i], Z - 12 * MathF.Abs(x) / 410))).ToArray();
            var fore = xs.Select((x, i) => m.P(2, new(x, bottoms[i], Z - 12 * MathF.Abs(x) / 410 + apart[i]))).ToArray();
            var aft = xs.Select((x, i) => i is 0 or 4 ? fore[i] : m.P(2, new(x, bottoms[i], Z - 12 * MathF.Abs(x) / 410 - apart[i]))).ToArray();
            m.Light = 1;
            for (var i = 0; i + 1 < xs.Length; i++)
            {
                var inside = new Vector3((xs[i] + xs[i + 1]) / 2, 585, Z);
                foreach (var brim in new[] { fore, aft })
                {
                    if (i == 0) m.Out(new[] { brim[0], brim[1], top[1] }, Black, inside);
                    else if (i == 3) m.Out(new[] { brim[3], brim[4], top[3] }, Black, inside);
                    else m.Out(new[] { brim[i], brim[i + 1], top[i + 1], top[i] }, Black, inside);
                }
                if (i is 1 or 2) m.Line(top[i], top[i + 1], GoldFlat);
            }
            m.Line(fore[0], top[1], GoldFlat); m.Line(top[3], fore[4], GoldFlat);
            m.Sphere(m.P(2, new(0, 680, Z + 40)), 36, RedFlat);
            m.Sphere(m.P(2, new(0, 680, Z + 48)), 13, GoldFlat);
        }

        // the Emperor (bone 13): hat, coat and scarf, from his waist up, his hands on the wheel
        var hands = SeatDriver(m, emperor, new HashSet<int> { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 19, 20, 21, 22 }, (7, new[] { 8, 9 }), (10, new[] { 11, 12 }),
            new Vector3(0, 480, 0), 0.8f, new Vector3(0, Waist, SeatZ), rimTop, new Vector3(70, 615, SeatZ + 235),
            // his coat and hat as black as the car (a lit grey in his own body)
            (f, _) => f.Colour == 224 && f.Material != 0 ? (Black + 1, SoftLight, -1) : (f.Colour, CarDriver.Own, f.Material == 0 ? 0 : -1));
        SteeringWheel(m, hull, hands.Right, hands.Left, Gold, GoldFlat);
        return m.ToBody(racer.Header);
    }

    // ---------------------------------------------------------------------------------------------------------------- Zoe

    // Zoe's car: a pink convertible in her gown.
    public static Body BuildZoe(Body zoe, Body racer)
    {
        var m = new CarMesh(Zoe.Name);
        Roots(m);

        // the body: round-shouldered, flaring at the tail like the gown's skirt; the top light pink, the sides pink, deep pink below
        m.Light = PinkShare;
        var hull = new Hull(m, 295, 12, 2.8f, 0.5f,
            (660, 150, 78, 85), (590, 235, 118, 120), (430, 285, 146, 135), (230, 300, 165, 140), (20, 300, 176, 140),
            (-180, 305, 180, 140), (-360, 340, 185, 145), (-500, 385, 170, 150), (-590, 350, 125, 135), (-640, 250, 80, 100));
        hull.Skin(m, (_, k) => k is 11 or 0 or 10 ? PinkLight : k is 3 or 4 or 5 or 6 or 7 ? PinkDeep : Pink);
        hull.Nose(m, 692, Pink, -6);
        hull.Tail(m, -668, PinkDeep);
        m.Light = 1;

        foreach (var side in new[] { 1f, -1f })
        {
            // lamps with lashes, tail lights
            var lamp = new Vector3(side * 172, hull.Cy + 40, 605);
            m.Sphere(m.P(2, lamp), 50, Lamp);
            foreach (var (dx, dz) in new[] { (-0.5f, 0f), (0.2f, 0f), (0.9f, -0.15f) })
                m.Line(m.P(2, lamp + new Vector3(side * dx * 40, 46, dz * 40)), m.P(2, lamp + new Vector3(side * (dx * 40 + 22 + dx * 26), 96, dz * 40 - 8)), Dark);
            m.Sphere(m.P(2, new(side * 190, hull.Cy + 35, -628)), 30, RedFlat);
            // a cream line along the body where the pinks meet
            for (var i = 0; i + 1 < hull.Rings.Count; i++) { var k = side > 0 ? 3 : 8; m.Line(hull.Rings[i][k], hull.Rings[i + 1][k], 45); }
        }

        // a heart on the bonnet
        {
            m.Sphere(m.P(2, hull.OnTop(-27, 520, 12)), 33, Heart); m.Sphere(m.P(2, hull.OnTop(27, 520, 12)), 33, Heart);
            m.Up(new[] { m.P(2, hull.OnTop(-56, 508, 5)), m.P(2, hull.OnTop(56, 508, 5)), m.P(2, hull.OnTop(0, 592, 5)) }, Heart, unlit: true);
        }

        var front = new Axle(new Vector3(250, 262, 400), new Vector3(435, 155, 400), 155, 105);
        var rear = new Axle(new Vector3(275, 268, -370), new Vector3(505, 180, -370), 180, 125);
        Wheels(m, new WheelLook(Gold, 22, Grey, 1, PinkLight, PinkShare, GoldFlat), front, rear);
        // her long gloves: pink wings over the front wheels, drawn out behind them
        m.Light = PinkShare;
        foreach (var side in new[] { 1f, -1f }) Mudguard(m, front, side, -1.75f, 1.0f, 4, 34, 10, PinkLight);
        m.Light = 1;

        // the cockpit, its rim the lighter band round the top of the gown
        const float SeatZ = -70, Waist = 500;
        var rimTop = hull.Top(0, SeatZ) + 26;
        m.Light = 0.1f;
        Cockpit(m, hull, SeatZ, 222, 190, rimTop, 10, 78);
        m.Light = 1;
        // the windscreen: three see-through panes round the front of the cockpit, a cream rail along their top
        Windscreen(m, hull, 265, 205, 105, 45);

        // a bow tied on the tail: two loops, a knot, two ends hanging down the back
        {
            var knot = hull.OnTop(0, -575, 22);
            m.Sphere(m.P(2, knot), 34, RedFlat + 2);
            foreach (var side in new[] { 1f, -1f })
            {
                m.Both(new[] { m.P(2, knot), m.P(2, knot + new Vector3(side * 170, 95, -20)), m.P(2, knot + new Vector3(side * 190, -30, -5)) }, RedFlat);
                m.Both(new[] { m.P(2, knot), m.P(2, knot + new Vector3(side * 95, -150, -75)), m.P(2, knot + new Vector3(side * 35, -165, -85)) }, RedFlat);
            }
        }

        // Zoe (bone 13), from her waist up, her hands on the wheel: the gown pink, the long gloves (forearms and hands) pink too
        var gloves = new HashSet<int> { 5, 7, 21, 22 };
        var hands = SeatDriver(m, zoe, new HashSet<int> { 3, 4, 5, 6, 7, 12, 13, 14, 15, 16, 17, 18, 21, 22 }, (4, new[] { 5, 21 }), (6, new[] { 7, 22 }),
            new Vector3(0, 700, -20), 0.8f, new Vector3(0, Waist, SeatZ), rimTop, new Vector3(64, 598, SeatZ + 215),
            (f, bones) => f.Material == 0 ? (f.Colour, CarDriver.Own, 0)
                : f.Colour == 64 ? (PinkDeep + 2, SoftLight, -1)
                : f.Colour == 32 && bones.All(gloves.Contains) ? (Pink, PinkShare, -1)
                : (f.Colour, CarDriver.Own, -1));
        SteeringWheel(m, hull, hands.Right, hands.Left, Gold, GoldFlat);
        return m.ToBody(racer.Header);
    }

    // ---------------------------------------------------------------------------------------------------------------- installing

    // Adds the cars to a game folder: each body at the end of BODY.HQR and, in the entity table (RESS.HQR entry 44), a body record giving it
    // to the racer's entity as its body 2, 3 or 4 (as RaceTrackBaldinoCar.Install). Returns the BODY.HQR indices and lines for the build's log.
    // (`only`: some of the cars, for a look at them while they are being made)
    public static (Dictionary<string, int> Index, List<string> Log) Install(string gameDirectory, IReadOnlyCollection<Car>? only = null)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var bodies = HqrArchive.Open(bodyPath);
        var racer = Body.Read(bodies.Read(RaceTrackBaldinoCar.RacerBody), 2);
        var cars = (only ?? All).Select(c => (Car: c, Body: c.Build(Body.Read(bodies.Read(c.Character), 2), racer))).ToList();

        var index = new Dictionary<string, int>(); var log = new List<string>();
        var bodyFile = File.ReadAllBytes(bodyPath);
        var table = HqrArchive.Open(ressPath).Read(44);
        var next = HqrArchive.CountEntries(bodyPath);
        foreach (var (car, body) in cars)
        {
            bodyFile = HqrWriter.AppendEntry(bodyFile, HqrWriter.StoredEntry(body.Write()));
            table = RaceTrackBaldinoCar.WithBody(table, RaceTrackBaldinoCar.RacerEntity, car.Generic, next);
            index[car.Name] = next;
            log.Add($"{car.Name}: BODY.HQR entry {next} ({body.Faces.Count} polygons, {body.Lines.Count} lines, {body.Spheres.Count} spheres, {body.Vertices.Count} points), " +
                    $"the racer's entity ({RaceTrackBaldinoCar.RacerEntity}) body {car.Generic}, {car.Driver} driving ({car.Island})");
            next++;
        }
        File.WriteAllBytes(bodyPath, bodyFile);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        return (index, log);
    }
}
