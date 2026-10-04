using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// Baldino's race car: a second opponent for the race track, Jerome Baldino (the Desert island's inventor, BODY.HQR entry 139) at the wheel of a
// car made after his rocket ship. Not a copy of the ship: a stubby ribbed barrel of a hull (the ship's planked brown fuselage, with navy
// go-faster stripes down its sides), an open cockpit so his face shows, a rocket nozzle with its flame at the back, the ship's swept grey wings
// with red lights at their tips, and an inventor's gadgets: odd wheels (big at the back, small at the front, red hub caps), a coil-spring aerial
// with a red ball, a clockwork key, a propeller on the nose and two headlights. About the size of the game's own cars (2.6 cells long, 2.3 wide).
//
// It is a body of the retail racer's entity (157), next to that entity's own car (body 0 -> BODY.HQR 227), so the racer's animations drive it:
// it has the same 18 bones, in the same order and for the same parts -- 0 and 1 the root and the bounce, 2 the hull, 3-5 and 6-8 the front
// struts, hubs and wheels, 9-10 and 11-12 the rear axles and wheels, 13 the driver. The racer's animations turn bones 14-17 (its driver's arms)
// a long way to reach its wheel; Baldino's arms are drawn holding his wheel already, in bone 13, and 14-17 are empty.
internal static class RaceTrackBaldinoCar
{
    public const int BaldinoBody = 139;      // BODY.HQR: Jerome Baldino (entity 93)
    public const int RacerBody = 227;        // BODY.HQR: the retail racer's car, whose header this one starts from
    public const int RacerEntity = 157;
    public const int Generic = 1;            // the racer entity's body number for this car (its own car is 0)

    // Palette ramp starts (the engine adds the light, up to about nine steps), and the unlit colours drawn as they are.
    private const int Plank = 224, PlankDark = 16, Stripe = 192, Metal = 48, MetalLight = 52, Brass = 100, Glass = 164, Tyre = 48;
    private const int HoleDark = 49, NozzleDark = 49, Flame = 242, FlameCore = 245, RedLight = 70, Headlight = 245, BrassLine = 101, SteeringWheel = 64;

    private const float SeatZ = -60, Waist = 520, BaldinoScale = 0.8f;
    // where his right hand holds the wheel (the left one mirrored): out in front of his face (his head reaches forward to about z 260) and below
    // his chin, behind the windscreen
    private static readonly Vector3 Grip = new(75, 615, 285);
    private static readonly Vector3 TrunkDirection = new(-0.35f, 0.45f, 0.82f);

    // The car, built round the Baldino of the game's own BODY.HQR.
    public static Body Build(Body baldino, Body racer)
    {
        var m = new CarMesh("Baldino's car");
        var root0 = m.P(0, new(0, 0, 0)); var root1 = m.P(0, new(0, 66, 0));
        var bounce = m.P(1, new(0, 199, 0));

        // ---- the hull (bone 2): rings round the z axis, nose (+z) to tail
        var profile = new (float Z, float Rx, float Ry)[]
        {
            (600, 120, 100), (520, 230, 165), (400, 300, 200), (250, 330, 212), (110, 340, 215),
            (-50, 340, 212), (-210, 325, 200), (-330, 280, 180), (-430, 200, 140), (-490, 150, 115),
        };
        const float Cy = 335; const int N = 10;
        var rings = profile.Select(r => m.Ring(2, new Vector3(0, Cy, r.Z), r.Rx, r.Ry, N, alongZ: true)).ToList();
        for (var i = 0; i + 1 < rings.Count; i++)
            for (var k = 0; k < N; k++)
            {
                var colour = k is 2 or 7 ? Stripe : (i % 2 == 0 ? Plank : PlankDark);
                m.Out(new[] { rings[i][k], rings[i][(k + 1) % N], rings[i + 1][(k + 1) % N], rings[i + 1][k] }, colour, new Vector3(0, Cy, (profile[i].Z + profile[i + 1].Z) / 2));
            }
        var tip = m.P(2, new(0, Cy + 10, 660));
        for (var k = 0; k < N; k++) m.Out(new[] { rings[0][k], rings[0][(k + 1) % N], tip }, Plank, new Vector3(0, Cy, 560));
        float HullTop(float x, float z)
        {
            var i = 0; while (i + 2 < profile.Length && profile[i + 1].Z > z) i++;
            var t = Math.Clamp((profile[i].Z - z) / (profile[i].Z - profile[i + 1].Z), 0, 1);
            var rx = profile[i].Rx + (profile[i + 1].Rx - profile[i].Rx) * t; var ry = profile[i].Ry + (profile[i + 1].Ry - profile[i].Ry) * t;
            var u = Math.Clamp(x / rx, -1, 1);
            return Cy + ry * MathF.Sqrt(1 - u * u);
        }

        // the cockpit: an oval hole in the top with a brass rim standing round it
        const float HoleRx = 235, HoleRz = 200, RimTop = 560; var holeZ = SeatZ + 40;
        var rimTop = new int[N]; var rimFoot = new int[N];
        for (var k = 0; k < N; k++)
        {
            var a = k * MathF.Tau / N;
            float x = HoleRx * MathF.Sin(a), z = holeZ + HoleRz * MathF.Cos(a);
            rimTop[k] = m.P(2, new(x, RimTop, z));
            rimFoot[k] = m.P(2, new(x, HullTop(x, z) - 12, z));
        }
        var holeCentre = m.P(2, new(0, RimTop, holeZ));
        for (var k = 0; k < N; k++)
        {
            m.Out(new[] { rimFoot[k], rimFoot[(k + 1) % N], rimTop[(k + 1) % N], rimTop[k] }, Brass, new Vector3(0, 500, holeZ));
            m.Up(new[] { rimTop[k], rimTop[(k + 1) % N], holeCentre }, HoleDark, unlit: true);
        }

        // the windscreen: three low teal panels in front of his wheel, leaning back, standing on the hull
        {
            const float z0 = 365;
            var bottom = new[] { new Vector3(-170, 0, z0 - 45), new Vector3(-70, 0, z0), new Vector3(70, 0, z0), new Vector3(170, 0, z0 - 45) }
                .Select(b => b with { Y = HullTop(b.X, b.Z) - 6 }).ToArray();
            var hs = bottom.Select(b => (Bottom: m.P(2, b), Top: m.P(2, b + new Vector3(0, 70, -30)))).ToArray();
            for (var i = 0; i < 3; i++) m.Both(new[] { hs[i].Bottom, hs[i + 1].Bottom, hs[i + 1].Top, hs[i].Top }, Glass);
        }

        // the rocket nozzle: the tail ring flares out to a grey bell, dark inside, with the flame coming out of it
        {
            var bell = m.Ring(2, new Vector3(0, Cy, -600), 175, 175, N, alongZ: true);
            var throat = m.Ring(2, new Vector3(0, Cy, -545), 110, 110, N, alongZ: true);
            var last = rings[^1];
            for (var k = 0; k < N; k++)
            {
                m.Out(new[] { last[k], last[(k + 1) % N], bell[(k + 1) % N], bell[k] }, Metal, new Vector3(0, Cy, -545));
                // (the inner wall faces the axis: seen from behind, looking into the bell)
                m.In(new[] { bell[k], bell[(k + 1) % N], throat[(k + 1) % N], throat[k] }, NozzleDark, new Vector3(0, Cy, -575), unlit: true);
            }
            var flameBase = m.Ring(2, new Vector3(0, Cy, -560), 95, 95, N, alongZ: true);
            var flameTip = m.P(2, new(0, Cy, -790));
            for (var k = 0; k < N; k++) m.Out(new[] { flameBase[k], flameBase[(k + 1) % N], flameTip }, Flame, new Vector3(0, Cy, -620), unlit: true);
            m.Sphere(m.P(2, new(0, Cy, -625)), 75, FlameCore);
        }

        // the ship's swept wings at the back, a tail fin, and little canards on the nose; red lights at the wing and fin tips
        foreach (var side in new[] { 1f, -1f })
        {
            m.Both(new[] { m.P(2, new(side * 315, Cy, -220)), m.P(2, new(side * 640, Cy + 85, -400)), m.P(2, new(side * 655, Cy + 85, -520)), m.P(2, new(side * 205, Cy, -425)) }, MetalLight);
            m.Sphere(m.P(2, new(side * 655, Cy + 95, -470)), 42, RedLight);
            m.Both(new[] { m.P(2, new(side * 300, Cy - 5, 395)), m.P(2, new(side * 440, Cy + 30, 420)), m.P(2, new(side * 430, Cy + 30, 480)), m.P(2, new(side * 250, Cy - 5, 480)) }, MetalLight);
            m.Sphere(m.P(2, new(side * 150, 470, 455)), 48, Headlight);
        }
        m.Both(new[] { m.P(2, new(0, 505, -345)), m.P(2, new(0, 745, -470)), m.P(2, new(0, 765, -560)), m.P(2, new(0, 468, -470)) }, MetalLight);
        m.Sphere(m.P(2, new(0, 778, -560)), 36, RedLight);

        // the gadgets: a coil-spring aerial with a red ball, a clockwork key, a propeller on the nose
        {
            var coil = new List<int>();
            for (var i = 0; i <= 9; i++) coil.Add(m.P(2, new(-160 + (i % 2 == 0 ? -35 : 35), 500 + i * 52, -300 - i * 6)));
            for (var i = 0; i + 1 < coil.Count; i++) m.Line(coil[i], coil[i + 1], BrassLine);
            m.Sphere(m.P(2, new(-160, 1000, -358)), 48, RedLight);

            // the clockwork key sticks out of the right side behind the cockpit, its bow two lobes, up and down
            var shaft = m.Strut(2, new Vector3(325, Cy + 20, -140), new Vector3(430, Cy + 20, -140), 22, Brass);
            _ = shaft;
            foreach (var up in new[] { 1f, -1f })
                m.Both(new[] { m.P(2, new(430, Cy + 20, -140)), m.P(2, new(430, Cy + 20 + up * 70, -195)), m.P(2, new(430, Cy + 20 + up * 150, -140)), m.P(2, new(430, Cy + 20 + up * 70, -85)) }, Brass);

            var hub = m.P(2, new(0, Cy + 10, 690));
            m.Sphere(hub, 30, Brass);
            m.Both(new[] { m.P(2, new(-18, Cy + 10, 690)), m.P(2, new(40, Cy + 180, 686)), m.P(2, new(80, Cy + 165, 686)), m.P(2, new(18, Cy + 10, 690)) }, MetalLight);
            m.Both(new[] { m.P(2, new(18, Cy + 10, 690)), m.P(2, new(-40, Cy - 160, 694)), m.P(2, new(-80, Cy - 145, 694)), m.P(2, new(-18, Cy + 10, 690)) }, MetalLight);
        }

        // ---- the wheels: struts from the hull (bones 3 and 6, 9 and 11) to the hubs and wheels. Front: small, steering; back: big.
        void Wheel(int strutBone, int hubBone, int wheelBone, Vector3 mount, Vector3 hubCentre, float radius, float width, float side)
        {
            var mountPoint = m.P(2, mount);
            var inner = hubCentre - new Vector3(side * width / 2, 0, 0);
            var strutEnd = m.Strut(strutBone, mount, inner, 26, Brass);
            m.Pivot(strutBone, mountPoint);
            if (hubBone >= 0)
            {
                var hubPoint = m.P(hubBone, hubCentre);
                m.Pivot(hubBone, strutEnd);
                m.Pivot(wheelBone, hubPoint);
            }
            else m.Pivot(wheelBone, strutEnd);
            // six-sided: tread and the outer face (the inner one is hardly ever seen), a red cap on the hub
            const int S = 6;
            var outer = new int[S]; var innerRing = new int[S];
            for (var k = 0; k < S; k++)
            {
                var a = k * MathF.Tau / S + MathF.PI / S;
                var offset = new Vector3(0, radius * MathF.Cos(a), radius * MathF.Sin(a));
                outer[k] = m.P(wheelBone, hubCentre + new Vector3(side * width / 2, 0, 0) + offset);
                innerRing[k] = m.P(wheelBone, inner + offset);
            }
            var cap = m.P(wheelBone, hubCentre + new Vector3(side * (width / 2 + 8), 0, 0));
            for (var k = 0; k < S; k++)
            {
                m.Out(new[] { outer[k], outer[(k + 1) % S], innerRing[(k + 1) % S], innerRing[k] }, Tyre, hubCentre);
                m.Out(new[] { outer[k], outer[(k + 1) % S], cap }, Tyre, hubCentre - new Vector3(side * width, 0, 0));
            }
            m.Sphere(cap, (int)(radius * 0.38f), RedLight);
        }
        foreach (var (side, strut, hub, wheel, rear) in new[] { (1f, 3, 4, 5, false), (-1f, 6, 7, 8, false), (-1f, 9, -1, 10, true), (1f, 11, -1, 12, true) })
        {
            if (!rear) Wheel(strut, hub, wheel, new Vector3(side * 285, 300, 400), new Vector3(side * 440, 150, 400), 150, 110, side);
            else Wheel(strut, hub, wheel, new Vector3(side * 262, 300, -330), new Vector3(side * 510, 205, -330), 205, 150, side);
        }

        // ---- Baldino (bone 13), seated, holding his wheel
        var seat = m.P(2, new(0, Waist, SeatZ));
        m.Pivot(13, seat);
        var hands = AddBaldino(m, baldino);
        // bones 14-17 (the racer's arms): one point each, where Baldino's shoulders are
        m.Pivot(14, hands.RightShoulder); var r14 = m.P(14, m.At(hands.RightShoulder));
        m.Pivot(15, r14); m.P(15, m.At(hands.RightShoulder));
        m.Pivot(16, hands.LeftShoulder); var l16 = m.P(16, m.At(hands.LeftShoulder));
        m.Pivot(17, l16); m.P(17, m.At(hands.LeftShoulder));

        // his steering wheel through his hands, on a column down to the dashboard
        {
            var centre = (hands.Right + hands.Left) / 2; var radius = Vector3.Distance(hands.Right, hands.Left) / 2;
            var across = Vector3.Normalize(hands.Right - hands.Left); var up = Vector3.Normalize(Vector3.Cross(new Vector3(0, 0.45f, 1), across));
            if (up.Y < 0) up = -up;
            Vector3 At(float r, int i) { var a = i * MathF.Tau / 8; return centre + across * r * MathF.Cos(a) + up * r * MathF.Sin(a); }
            var outer = Enumerable.Range(0, 8).Select(i => m.P(2, At(radius + 16, i))).ToArray();
            var inner = Enumerable.Range(0, 8).Select(i => m.P(2, At(radius - 16, i))).ToArray();
            for (var i = 0; i < 8; i++) m.Both(new[] { outer[i], outer[(i + 1) % 8], inner[(i + 1) % 8], inner[i] }, SteeringWheel);
            var hubPoint = m.P(2, centre);
            m.Line(inner[0], inner[4], BrassLine);
            var dash = m.P(2, new(centre.X, HullTop(0, centre.Z + 120) + 4, centre.Z + 120));
            m.Line(hubPoint, dash, BrassLine);
        }

        m.Pivot(1, root1); m.Pivot(2, bounce);
        _ = root0;
        return m.ToBody(racer.Header);
    }

    private sealed record Hands(Vector3 Right, Vector3 Left, int RightShoulder, int LeftShoulder);

    // Baldino from his own body: his torso, head, trunk, ears (with their rings) and arms, his legs and hips left out (they are in the car).
    // His arms are turned to hold a wheel in front of him and his trunk raised forward (hanging, it would go through the dashboard); then he is
    // made a little smaller (0.8) and sat in the hole, his waist at the rim, facing the way the car goes (+z, as he does in his body).
    private static Hands AddBaldino(CarMesh m, Body baldino)
    {
        var driver = new CarDriver(baldino);
        var keep = new HashSet<int> { 3, 4, 9, 10, 11, 12, 14, 15, 16, 17, 18, 19, 20 };

        // smaller, and sat in the car: his waist (y 433 in his body) at the car's waist height, over the seat
        var origin = new Vector3(0, 433, 0);
        Vector3 Place(Vector3 p) => (p - origin) * BaldinoScale + new Vector3(0, Waist, SeatZ);
        Vector3 Unplace(Vector3 p) => (p - new Vector3(0, Waist, SeatZ)) / BaldinoScale + origin;

        // the arms: each hand on the wheel (Grip, in the car)
        var right = driver.Reach(9, new[] { 10 }, Unplace(new Vector3(Grip.X, Grip.Y, Grip.Z)), 1);
        var left = driver.Reach(11, new[] { 12 }, Unplace(new Vector3(-Grip.X, Grip.Y, Grip.Z)), -1);

        // the trunk raised forward and to his left, as if he were trumpeting (and so as not to hide his face), its tip curled up a little more
        var trunkBase = driver.PivotOf(14);
        var trunkTip = driver.Far(16, trunkBase);
        var raised = CarDriver.Between(trunkTip - trunkBase, TrunkDirection);
        driver.Turn(new[] { 14, 15, 16 }, trunkBase, raised);
        var curlAt = Vector3.Transform(driver.PivotOf(16) - trunkBase, raised) + trunkBase;
        driver.Turn(new[] { 16 }, curlAt, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.45f));

        // the points his kept polygons, lines and spheres use; polygons wholly below the rim (inside the car) are left out
        const float Rim = 560;
        var point = driver.Seat(m, keep, Place, Rim, BaldinoScale);
        return new Hands(Place(right.Hand), Place(left.Hand), point(baldino.Bones[9].Pivot), point(baldino.Bones[11].Pivot));
    }

    // Adds the car to a game folder: the body at the end of BODY.HQR and, in the entity table (RESS.HQR entry 44), a body record giving it to the
    // racer's entity as its body 1. Returns the BODY.HQR index and a line for the build's log.
    public static (int Index, string Log) Install(string gameDirectory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var bodies = HqrArchive.Open(bodyPath);
        var car = Build(Body.Read(bodies.Read(BaldinoBody), 2), Body.Read(bodies.Read(RacerBody), 2));
        var index = HqrArchive.CountEntries(bodyPath);
        File.WriteAllBytes(bodyPath, HqrWriter.AppendEntry(File.ReadAllBytes(bodyPath), HqrWriter.StoredEntry(car.Write())));

        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var table = WithBody(HqrArchive.Open(ressPath).Read(44), RacerEntity, Generic, index);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        return (index, $"Baldino's car: BODY.HQR entry {index} ({car.Faces.Count} polygons, {car.Lines.Count} lines, {car.Spheres.Count} spheres, {car.Vertices.Count} points), " +
                       $"the racer's entity ({RacerEntity}) body {Generic}, driven with the racer's animations");
    }

    // The entity table with a body record -- 1, generic number (1 byte), size 4, BODY.HQR index (S16), 0 (no collision box of its own) -- added
    // to one entity just before its end mark (255), or its index changed when the entity already has that body number. The entities after it
    // move, so their offsets do too (as RaceTrackJumpAnim.WithAnim).
    public static byte[] WithBody(byte[] table, int entity, int generic, int bodyIndex)
    {
        var count = BinaryPrimitives.ReadInt32LittleEndian(table) / 4 - 1;
        if (entity < 0 || entity >= count) throw new InvalidDataException($"RESS.HQR has no entity {entity}.");
        var start = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(entity * 4));
        var end = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan((entity + 1) * 4));
        var p = start;
        while (p < end && table[p] != 255)
        {
            var command = table[p];
            if (command == 1 && table[p + 1] == generic)
            {
                var same = (byte[])table.Clone();
                BinaryPrimitives.WriteInt16LittleEndian(same.AsSpan(p + 3), (short)bodyIndex);
                return same;
            }
            p += command == 3 ? 3 + table[p + 3] : 2 + table[p + 2];
        }
        if (p >= end) throw new InvalidDataException($"Entity {entity}'s records have no end mark.");
        var record = new byte[] { 1, (byte)generic, 4, (byte)bodyIndex, (byte)(bodyIndex >> 8), 0 };
        var result = new byte[table.Length + record.Length];
        table.AsSpan(0, p).CopyTo(result);
        record.CopyTo(result.AsSpan(p));
        table.AsSpan(p).CopyTo(result.AsSpan(p + record.Length));
        for (var i = entity + 1; i <= count; i++)
        {
            var at = i * 4;
            if (at + 4 > result.Length) break;
            var offset = BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(at));
            if (offset >= p) BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(at), offset + record.Length);
        }
        return result;
    }
}
