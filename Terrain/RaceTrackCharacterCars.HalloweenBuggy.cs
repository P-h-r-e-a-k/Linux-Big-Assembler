using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// Twinsen's Halloween buggy (the user, 2026-10-20: "instead use Twinsen's default buggy with an orange coat of paint and some bats on the
// sides"): the race game's first car, while the pumpkin F1 waits to be won -- LBA2's own buggy, the one Twinsen drives, as the game has it
// (the buggy behaviour's entity, RaceTrackSmallCars.HeroEntity: its body while he drives and the one with its gun out as he fires), its
// yellow paint made the fire orange the Temple's pumpkins are (241: the same in every island's palette, RaceTrackCharacterCars.Pumpkin),
// taking a little of the light so it shades from red-orange to amber, and on each side a black bat with its wings spread, laid on the
// body's side over the front wheel -- and one on its back, where the race's camera, behind the car, sees it.
//
// It stays the game's buggy, not one of the racer entity's cars drawn in its place (drive_as=): the buggy's own code turns its wheels, its
// steering wheel and Twinsen's head as he drives, which a car drawn by an animation wouldn't. So its bodies are more of the buggy entity's
// own -- HalloweenGeneric (its collision box copied from the buggy's: it drives as his buggy does), HalloweenFiring and the half-size
// HalloweenSmall for an opponent's lightning -- and the race file names them (RACEMOD.CPP twinsen_body=): the engine puts them on Twinsen in
// place of the buggy's own while he drives. The race game's car line for it (HalloweenBuggyCar) is "twinsen_body=..." with the body the car
// page shows (RACEGAME.CPP).
internal static partial class RaceTrackCharacterCars
{
    public const string HalloweenBuggyName = "Twinsen's Halloween buggy";
    // the buggy entity's generic bodies: the driving one, the firing one (GEN_BODY_TUNIQUE_TIR's), the half-size one
    public const int HalloweenGeneric = 60, HalloweenFiring = 61, HalloweenSmall = 160;
    private const int BuggyDriving = 1, BuggyFiring = 10;   // the buggy entity's GEN_BODY_TUNIQUE and GEN_BODY_TUNIQUE_TIR
    private const int HbHull = 2;                           // the buggy's hull (its bone 2: the paint, the bats)
    private const int HbYellow = 96, HbOrange = 241, HbBat = 48;
    private const float HbOrangeShare = 0.22f;              // (as the pumpkin's skin: 241 two or three colours on in the light)

    // A bat, wings spread, across a unit wingspan (u from -1 to 1, v up): its body and head (a fan round its middle) and its right wing (the
    // left the same, mirrored), as pieces each a convex polygon -- the leading edge's points over the scalloped trailing edge's, the fingers'
    // tips hanging between them.
    // (a body's polygons and lines are 550 at most, the firing buggy's 486 already: a bat is 20 polygons)
    private static readonly Vector2[] BatBody =
    {
        new(0, -0.24f), new(0.12f, -0.12f), new(0.11f, 0.10f), new(0.09f, 0.30f), new(0, 0.20f), new(-0.09f, 0.30f), new(-0.11f, 0.10f),
        new(-0.12f, -0.12f),
    };
    private static readonly Vector2[][] BatWing =
    {
        new Vector2[] { new(0.11f, 0.10f), new(0.36f, 0.26f), new(0.30f, -0.07f), new(0.12f, -0.12f) },
        new Vector2[] { new(0.36f, 0.26f), new(0.63f, 0.31f), new(0.58f, -0.05f), new(0.30f, -0.07f) },
        new Vector2[] { new(0.30f, -0.07f), new(0.58f, -0.05f), new(0.45f, -0.21f) },
        new Vector2[] { new(0.63f, 0.31f), new(0.86f, 0.24f), new(0.85f, -0.01f), new(0.58f, -0.05f) },
        new Vector2[] { new(0.58f, -0.05f), new(0.85f, -0.01f), new(0.72f, -0.17f) },
        new Vector2[] { new(0.86f, 0.24f), new(1.0f, 0.06f), new(0.85f, -0.01f) },
    };
    // The bats: looked for along an axis (0: x, from the car's right (+1) or left (-1) side; 2: z, from its back (-1)), where (the other
    // two axes' middle: z and y on a side, x and y on the back), how wide and how tall. On each side the front wing's side over the front
    // wheel (the buggy is open between the wheels, Twinsen's tunic seen there); on the back the panel between its red corners.
    private static readonly (int Axis, float Side, float U, float V, float HalfSpan, float Tall)[] Bats =
    {
        (0, 1, 340, 358, 165, 262), (0, -1, 340, 358, 165, 262), (2, -1, 0, 412, 150, 196),
    };
    private const float BatOff = 12;                        // how far off the paint

    // The buggy's body painted for Halloween: its hull's yellow faces orange (on points of their own, which take HbOrangeShare of the light:
    // the red trim and the green beside them keep theirs), a bat on each side and one on its back. Every other polygon, its bones and its polygon types as the
    // game's body has them.
    public static Body BuildHalloweenBuggy(Body buggy)
    {
        var world = buggy.World();
        int BoneOf(int p) => buggy.Bones.FindIndex(b => p >= b.Start && p < b.Start + b.Count);
        var bones = buggy.Bones.Count;
        // each bone's points, the game's first (each with its own share of the light), then those made here
        var points = Enumerable.Range(0, bones).Select(b => Enumerable.Range(buggy.Bones[b].Start, buggy.Bones[b].Count)
            .Select(i => (World: world[i], Light: buggy.LightScale is { } ls && i < ls.Length ? ls[i] : 1f)).ToList()).ToArray();
        // (a point's place: its bone and its index in the bone's list)
        var refs = new List<(int Bone, int Index)>();
        for (var b = 0; b < bones; b++) for (var k = 0; k < points[b].Count; k++) refs.Add((b, k));
        int Add(int bone, Vector3 at, float light) { points[bone].Add((at, light)); refs.Add((bone, points[bone].Count - 1)); return refs.Count - 1; }

        // the paint: the hull's yellow faces orange, on copies of their points
        var orange = new Dictionary<int, int>();
        var faces = new List<Face>();
        var painted = 0;
        foreach (var f in buggy.Faces)
        {
            var kept = f with { Lba2Type = f.Lba2Type >= 0 ? f.Lba2Type : f.Material };
            if (f.Colour != HbYellow || f.Texture is not null || !f.Points.All(p => BoneOf(p) == HbHull)) { faces.Add(kept); continue; }
            var ids = f.Points.Select(p => orange.TryGetValue(p, out var o) ? o : orange[p] = Add(HbHull, world[p], HbOrangeShare)).ToArray();
            faces.Add(kept with { Points = ids, Colour = HbOrange });
            painted++;
        }
        if (painted == 0) throw new InvalidDataException("the buggy's body has no yellow paint on its hull: not the game's buggy");

        // the bats: each point of one laid on the hull (its outermost polygon there, looked for from outside along the bat's axis), a little
        // off it
        var hull = buggy.Faces.Where(f => f.Points.All(p => BoneOf(p) == HbHull)).SelectMany(f => f.Points.Length == 4
            ? new[] { (world[f.Points[0]], world[f.Points[1]], world[f.Points[2]]), (world[f.Points[0]], world[f.Points[2]], world[f.Points[3]]) }
            : new[] { (world[f.Points[0]], world[f.Points[1]], world[f.Points[2]]) }).ToList();
        foreach (var (axis, side, u0, v0, halfSpan, tall) in Bats)
        {
            var made = new Dictionary<Vector2, int>();
            int On(Vector2 uv)
            {
                if (made.TryGetValue(uv, out var h)) return h;
                float u = u0 + uv.X * halfSpan, y = v0 + uv.Y * tall / 2;
                // (on a side u is z, the hull's x looked for; on the back u is x, its z)
                var at = axis == 0 ? new Vector3(0, y, u) : new Vector3(u, y, 0);
                var found = SurfaceOf(hull, axis, side, at) ?? throw new InvalidDataException($"the buggy has no paint at ({at.X:0}, {at.Y:0}, {at.Z:0}) for its bat");
                var p = axis == 0 ? at with { X = found + side * BatOff } : at with { Z = found + side * BatOff };
                return made[uv] = Add(HbHull, p, 0);
            }
            var outward = axis == 0 ? new Vector3(side, 0, 0) : new Vector3(0, 0, side);
            void Piece(Vector2[] outline, int colour)
            {
                var ids = outline.Select(On).ToArray();
                // (facing out: the engine draws a polygon only from the side its points go round anticlockwise)
                var n = Newell(ids.Select(i => points[refs[i].Bone][refs[i].Index].World).ToArray());
                if (Vector3.Dot(n, outward) < 0) ids = ids.Reverse().ToArray();
                faces.Add(new Face(ids, colour, Material: 0));
            }
            // its body and head, a fan round its middle; its wings
            var middle = new Vector2(0, 0.02f);
            for (var k = 0; k < BatBody.Length; k++) Piece(new[] { middle, BatBody[k], BatBody[(k + 1) % BatBody.Length] }, HbBat);
            foreach (var wing in BatWing)
            {
                Piece(wing, HbBat);
                Piece(wing.Select(v => new Vector2(-v.X, v.Y)).ToArray(), HbBat);
            }
        }

        // the body: the bones' points in order, every polygon, line and sphere on them
        var start = new int[bones];
        for (int b = 0, next = 0; b < bones; b++) { start[b] = next; next += points[b].Count; }
        int New(int handle) => start[refs[handle].Bone] + refs[handle].Index;
        // (the game's own points keep their numbers' order within their bone: handle i < the old count is the old point i)
        var body = new Body { Game = 2, Lit = true, Header = (byte[])buggy.Header.Clone(), Textures = buggy.Textures };
        for (var b = 0; b < bones; b++)
        {
            var old = buggy.Bones[b];
            body.Bones.Add(new Bone(start[b], points[b].Count, b == 0 ? old.Pivot : New(old.Pivot), old.Parent, old.Record));
        }
        var newWorld = points.SelectMany(l => l.Select(p => p.World)).ToArray();
        body.Vertices.AddRange(new Vector3[newWorld.Length]);
        body.SetWorld(newWorld);
        body.LightScale = points.SelectMany(l => l.Select(p => p.Light)).ToArray();
        body.Faces.AddRange(faces.Select(f => f with { Points = f.Points.Select(New).ToArray() }));
        body.Lines.AddRange(buggy.Lines.Select(l => l with { A = New(l.A), B = New(l.B) }));
        body.Spheres.AddRange(buggy.Spheres.Select(s => s with { Point = New(s.Point) }));
        body.Validate();
        return body;
    }

    // The hull at `at` seen along an axis (0: x, 2: z) from `side` (+1 from beyond its + end, -1 its -), the other two of `at`'s coordinates
    // where to look: that axis's coordinate of the outermost polygon there, on that side of the middle, or null.
    private static float? SurfaceOf(List<(Vector3 A, Vector3 B, Vector3 C)> hull, int axis, float side, Vector3 at)
    {
        // (the two coordinates across the axis: y and the other one)
        static float Q(Vector3 v, int axis) => axis == 0 ? v.Z : v.X;
        static float W(Vector3 v, int axis) => axis == 0 ? v.X : v.Z;
        float y = at.Y, q = Q(at, axis);
        float? best = null;
        foreach (var (a, b, c) in hull)
        {
            // (the triangle seen along the axis: (y, q) inside it, and its depth there)
            var d = (b.Y - a.Y) * (Q(c, axis) - Q(a, axis)) - (c.Y - a.Y) * (Q(b, axis) - Q(a, axis));
            if (MathF.Abs(d) < 1e-3f) continue;
            var s = ((y - a.Y) * (Q(c, axis) - Q(a, axis)) - (c.Y - a.Y) * (q - Q(a, axis))) / d;
            var t = ((b.Y - a.Y) * (q - Q(a, axis)) - (y - a.Y) * (Q(b, axis) - Q(a, axis))) / d;
            if (s < -1e-4f || t < -1e-4f || s + t > 1 + 1e-4f) continue;
            var w = W(a, axis) + s * (W(b, axis) - W(a, axis)) + t * (W(c, axis) - W(a, axis));
            if (w * side <= 0) continue;
            if (best is null || w * side > best.Value * side) best = w;
        }
        return best;
    }

    private static Vector3 Newell(Vector3[] p)
    {
        var n = Vector3.Zero;
        for (var i = 0; i < p.Length; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Length];
            n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
        }
        return n;
    }

    // Adds the Halloween buggy to a game folder whose race tracks are built (at the end of the build, after the pumpkin F1: no body before
    // it moves): its driving and firing bodies and the driving one's half-size copy at the end of BODY.HQR, the buggy entity's bodies
    // HalloweenGeneric (with the buggy's own collision box), HalloweenFiring and HalloweenSmall. Returns lines for the build's log.
    public static List<string> InstallHalloweenBuggy(string gameDirectory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var table = HqrArchive.Open(ressPath).Read(44);
        var bodies = HqrArchive.Open(bodyPath);
        var driving = BodyRecord(table, RaceTrackSmallCars.HeroEntity, BuggyDriving);
        var firing = BodyRecord(table, RaceTrackSmallCars.HeroEntity, BuggyFiring);
        if (driving is null) return new() { $"{HalloweenBuggyName}: not made -- the buggy's entity ({RaceTrackSmallCars.HeroEntity}) has no body {BuggyDriving}" };
        byte[] car, gun;
        try
        {
            car = BuildHalloweenBuggy(Body.Read(bodies.Read(driving.Value.Body), 2)).Write();
            gun = firing is { } f ? BuildHalloweenBuggy(Body.Read(bodies.Read(f.Body), 2)).Write() : car;
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException) { return new() { $"{HalloweenBuggyName}: not made -- {e.Message}" }; }
        var file = File.ReadAllBytes(bodyPath);
        var own = HqrArchive.CountEntries(bodyPath);
        file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(car));
        file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(gun));
        file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(RaceTrackSmallCars.Halved(car)));
        File.WriteAllBytes(bodyPath, file);
        // (the driving and firing bodies' records the buggy's own, but for the body: its collision box with them)
        table = WithBodyRecord(table, RaceTrackSmallCars.HeroEntity, driving.Value.Record, HalloweenGeneric, own);
        table = WithBodyRecord(table, RaceTrackSmallCars.HeroEntity, (firing ?? driving).Value.Record, HalloweenFiring, own + 1);
        table = RaceTrackBaldinoCar.WithBody(table, RaceTrackSmallCars.HeroEntity, HalloweenSmall, own + 2);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        var made = Body.Read(car, 2);
        return new()
        {
            $"{HalloweenBuggyName} (the buggy painted orange, a bat on each side and on its back): BODY.HQR entries {own} (driving: {made.Faces.Count} polygons, {made.Vertices.Count} points), " +
            $"{own + 1} (firing) and {own + 2} (half size); the buggy's entity ({RaceTrackSmallCars.HeroEntity}) bodies {HalloweenGeneric}, {HalloweenFiring} and {HalloweenSmall}",
        };
    }

    // The race game's car line for the Halloween buggy in a folder that has it: the race file's line (RACEMOD.CPP twinsen_body=: the driving,
    // firing and half-size bodies of the buggy's entity), the BODY.HQR body the car page shows and the ANIM.HQR animation it is posed by --
    // the buggy's standing still (its bones' places come from an animation: unposed, the engine draws Twinsen and the wheels out of
    // place); null when the folder hasn't got it.
    public static (string Line, int Body, int Anim)? HalloweenBuggyCar(string gameDirectory)
    {
        var path = Path.Combine(gameDirectory, "RESS.HQR");
        if (!File.Exists(path)) return null;
        var table = HqrArchive.Open(path).Read(44);
        if (BodyRecord(table, RaceTrackSmallCars.HeroEntity, HalloweenGeneric) is not { } own) return null;
        return ($"twinsen_body={HalloweenGeneric} {HalloweenFiring} {HalloweenSmall}", own.Body, AnimOf(table, RaceTrackSmallCars.HeroEntity, BuggyStill));
    }
    private const int BuggyStill = 0;                       // the buggy entity's GEN_ANIM_RIEN

    // An entity's animation for a generic one (RESS.HQR 44: 3, generic (U16), size, ANIM.HQR index (S16), ...), -1 none.
    private static int AnimOf(byte[] table, int entity, int generic)
    {
        var p = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(entity * 4));
        var end = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan((entity + 1) * 4));
        while (p < end && table[p] != 255)
        {
            var command = table[p];
            if (command == 3 && BinaryPrimitives.ReadUInt16LittleEndian(table.AsSpan(p + 1)) == generic) return BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(p + 4));
            p += command == 3 ? 3 + table[p + 3] : 2 + table[p + 2];
        }
        return -1;
    }

    // An entity's body record for a generic body (RESS.HQR 44: 1, generic, size, BODY.HQR index (S16), ...): its bytes and its body; null
    // when it has none.
    private static (byte[] Record, int Body)? BodyRecord(byte[] table, int entity, int generic)
    {
        var p = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(entity * 4));
        var end = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan((entity + 1) * 4));
        while (p < end && table[p] != 255)
        {
            var command = table[p];
            var size = command == 3 ? 3 + table[p + 3] : 2 + table[p + 2];
            if (command == 1 && table[p + 1] == generic) return (table.AsSpan(p, size).ToArray(), BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(p + 3)));
            p += size;
        }
        return null;
    }

    // The table with a body record for `generic` of an entity: a copy of `record` (another of its bodies' -- its collision box with it) for
    // BODY.HQR body `bodyIndex`, in place of the generic's own record or before the entity's end mark.
    private static byte[] WithBodyRecord(byte[] table, int entity, byte[] record, int generic, int bodyIndex)
    {
        var copy = (byte[])record.Clone();
        copy[1] = (byte)generic;
        BinaryPrimitives.WriteInt16LittleEndian(copy.AsSpan(3), (short)bodyIndex);
        var count = BinaryPrimitives.ReadInt32LittleEndian(table) / 4 - 1;
        var start = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(entity * 4));
        var end = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan((entity + 1) * 4));
        var p = start;
        int at = -1, gone = 0;
        while (p < end && table[p] != 255)
        {
            var command = table[p];
            var size = command == 3 ? 3 + table[p + 3] : 2 + table[p + 2];
            if (command == 1 && table[p + 1] == generic) { at = p; gone = size; break; }
            p += size;
        }
        if (at < 0)
        {
            if (p >= end) throw new InvalidDataException($"Entity {entity}'s records have no end mark.");
            at = p;
        }
        var grow = copy.Length - gone;
        var result = new byte[table.Length + grow];
        table.AsSpan(0, at).CopyTo(result);
        copy.CopyTo(result.AsSpan(at));
        table.AsSpan(at + gone).CopyTo(result.AsSpan(at + copy.Length));
        for (var i = entity + 1; i <= count; i++)
        {
            var o = i * 4;
            if (o + 4 > result.Length) break;
            var offset = BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(o));
            if (offset > at) BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(o), offset + grow);
        }
        return result;
    }
}
