using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using LbaBodyStudio;
using LBAAssembler.Lba1;

namespace LBAAssembler.Terrain.Palace;

// The dungeon's torturers (the user, 2026-10-17: "Let's make our palace basement more like a medieval dungeon. Let's add some grobo
// torturers with bull whips etc."): Grobos -- the game's grey Grobo near its end (BODY.HQR 459, entity 336: black clothes, a red sash, a
// spiked club in his left hand) -- in an executioner's black hood, a bull whip in his hand where the club was. Actors (the race's guards,
// RaceTrackGuards.Torturer; RACEMOD.CPP guard= kind 3): each at his post off the cars' way, cracking his whip now and then -- more often,
// turned to it, while a car goes by -- touching nothing (the whip's crack is the picture's only: a car is never struck, stopped or slowed).
//   - The body (Make): the game's own, its club's polygons gone and its points the whip's handle (a grip in the hand, a brass pommel); the
//     lash seven bones of its own (Lash, Lashes), one after another from the handle's end, each a length of a four-sided leather tube
//     narrowing to the tip, its far end turning the next -- so an animation bends it, swings it and cracks it; the hood a bone of its own on
//     the head (a cowl round its top and back, a brim over the brow lined in red, a short peak behind; the ears out at its sides, the trunk
//     out at the front). Then Scale times as big. The polygons, normals and lights the game's body has are kept as they are, byte for
//     byte: the new ones go after them (LBA2 bodies: a bone's points one run, the bones' in order; a flat-lit polygon's light is found past
//     all the points, so theirs move on by the points added).
//   - The animations (Anims): standing (the game's own standing, its four keyframes, the whip hand by his side, the lash from it to the
//     floor and along it in front of him, stirring); the crack: the arm up and back, the lash thrown out behind him; over the top, the lash
//     following in a loop; down and forward, the lash straight out in front -- the crack -- and its end flicking down; the lash on the
//     floor in front; back to standing. Each pose given as where the arm and the lash point (Aim), each bone turned from its parent's
//     frame as the engine turns it (Lba1Pose: M(Alpha) M(Gamma) M(Beta)).
internal static class PalaceTorturers
{
    public const int Entity = 336, Retail = 459;
    // his body and animations, the entity's own numbers for them (its own: bodies 0 and 189; animations 0-8, 22, 148 and 277)
    public const int Body = 40, Idle = 640, Crack = 641;
    public const double Scale = 1.8;
    // (ms) the crack: into its first pose, the arm up and back, over the top, down to the crack, the follow-through, back to standing
    public static readonly int[] CrackMs = { 120, 380, 180, 110, 160, 420 };

    // the game's body: its whip arm (upper arm, forearm, hand -- the hand the club's bone, now the handle's), the other arm, the torso, the
    // head; the bones added: the lash's seven, then the hood's
    private const int Upper = 11, Fore = 12, Hand = 13, OtherUpper = 9, OtherFore = 10, Torso = 3, Head = 4;
    public const int Lash = 21, Lashes = 7, Hood = Lash + Lashes;
    // (model units) the lash's lengths, its thickness (half across) at the handle's end and each length's end; the handle's length, the
    // grip's and the pommel's thickness, how far the pommel is behind the wrist; the popper's length past the lash's last ring
    private static readonly float[] LashLength = { 300, 290, 280, 270, 250, 230, 200 };
    private static readonly float[] LashHalf = { 18, 16, 14, 12, 10, 9, 8, 6 };
    private const float HandleLong = 300, Grip = 26, Pommel = 36, PommelBack = 45, Popper = 70;
    // the colours (ramp starts of the island's palette, and how much of the light each takes): the lash's leather, the grip's dark wood,
    // the pommel's brass, the hood's black, and its brim's lining a dark red (unlit)
    private const int Leather = 16, Wood = 96, Brass = 100, Black = 48, Lining = 66;
    private const float LeatherLight = 0.85f, WoodLight = 0.7f, BrassLight = 0.55f, BlackLight = 0.28f;

    // Into the game folder: his body (BODY.HQR's end) and animations (ANIM.HQR's), the entity's body Body and animations Idle and Crack
    // (RESS.HQR's entity table). Lines for the build's log.
    public static List<string> Install(string gameDirectory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var animPath = Path.Combine(gameDirectory, "ANIM.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var table = HqrArchive.Open(ressPath).Read(44);
        var entity = Lba2EntityTable.Parse(table).Entities.FirstOrDefault(e => e.Id == Entity);
        if (entity is null || RaceTrackGuards.BodyOf(table, Entity, 0) != Retail || !entity.Anims.Any(a => a.Generic == 0))
            return new() { $"WARNING: the dungeon's torturers: entity {Entity} isn't the grey Grobo (BODY.HQR {Retail}) -- none made" };
        if (entity.Bodies.Any(b => b.Generic == Body) || entity.Anims.Any(a => a.Generic is Idle or Crack))
            return new() { $"WARNING: the dungeon's torturers: entity {Entity} already has body {Body} or animation {Idle} or {Crack} -- none made" };
        var bytes = Make(HqrArchive.Open(bodyPath).Read(Retail), out var standing);
        var (idle, crack) = Anims(standing, HqrArchive.Open(animPath).Read(entity.Anims.First(a => a.Generic == 0).Anim));
        var bodyIndex = HqrArchive.CountEntries(bodyPath);
        File.WriteAllBytes(bodyPath, HqrWriter.AppendEntry(File.ReadAllBytes(bodyPath), HqrWriter.StoredEntry(bytes)));
        var animIndex = HqrArchive.CountEntries(animPath);
        var anims = HqrWriter.AppendEntry(File.ReadAllBytes(animPath), HqrWriter.StoredEntry(idle.Write()));
        File.WriteAllBytes(animPath, HqrWriter.AppendEntry(anims, HqrWriter.StoredEntry(crack.Write())));
        table = RaceTrackBaldinoCar.WithBody(table, Entity, Body, bodyIndex);
        table = RaceTrackJumpAnim.WithAnim(table, Entity, Idle, animIndex);
        table = RaceTrackJumpAnim.WithAnim(table, Entity, Crack, animIndex + 1);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        return new()
        {
            $"the dungeon's torturers: the grey Grobo (BODY.HQR {Retail}) hooded, a bull whip in his hand, {Scale} times as big -- BODY.HQR {bodyIndex} " +
            $"({standing.Vertices.Count} points, {standing.Bones.Count} bones), entity {Entity}'s body {Body}; standing and the crack ANIM.HQR {animIndex} and " +
            $"{animIndex + 1}, its animations {Idle} and {Crack}",
        };
    }

    // ---- the body -----------------------------------------------------------------------------------------------------------------

    // An LBA2 body's parts (T_BODY_HEADER's): its header, bones (parent, pivot point, points, face normals), points and their normals
    // (x, y, z, bone), face normals, polygons (blocks: a type and its records), lines, spheres, textures as they are. (Temple.TempleMarshal's
    // flags are grafted onto the School of Magic's assistant the same way.)
    internal sealed class Parts
    {
        public byte[] Header = Array.Empty<byte>();
        public List<(int Parent, int Pivot, int Count, int FaceNormals)> Bones = new();
        public List<(short X, short Y, short Z, int Bone)> Points = new(), Normals = new();
        public byte[] FaceNormals = Array.Empty<byte>(), Lines = Array.Empty<byte>(), Spheres = Array.Empty<byte>(), Textures = Array.Empty<byte>();
        public List<(int Type, List<byte[]> Records)> Blocks = new();
        public int LineCount, SphereCount, TextureCount, FaceNormalCount;

        private static int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
        private static int U(byte[] b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));
        private static short S(byte[] b, int at) => BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at));
        // a record's size: a plain polygon's 12 bytes, a textured one's 24 (32 a quad), an environment-mapped one's 16 (AFF_OBJ.CPP)
        public static int Stride(int type) => (type & 0x4000) != 0 ? 16 : (type & 0xFF) > 7 ? ((type & 0x8000) != 0 ? 32 : 24) : 12;

        public static Parts Read(byte[] b)
        {
            var p = new Parts { Header = b[..96] };
            for (int i = 0, at = I(b, 36); i < I(b, 32); i++, at += 8) p.Bones.Add((S(b, at), U(b, at + 2), U(b, at + 4), U(b, at + 6)));
            for (int i = 0, at = I(b, 44); i < I(b, 40); i++, at += 8) p.Points.Add((S(b, at), S(b, at + 2), S(b, at + 4), U(b, at + 6)));
            for (int i = 0, at = I(b, 52); i < I(b, 48); i++, at += 8) p.Normals.Add((S(b, at), S(b, at + 2), S(b, at + 4), U(b, at + 6)));
            p.FaceNormalCount = I(b, 56);
            p.FaceNormals = b.AsSpan(I(b, 60), p.FaceNormalCount * 8).ToArray();
            for (int at = I(b, 68), end = I(b, 76); at < end;)
            {
                int type = U(b, at), n = U(b, at + 2), stride = Stride(type);
                at += 8;
                var records = new List<byte[]>();
                for (var k = 0; k < n; k++, at += stride) records.Add(b.AsSpan(at, stride).ToArray());
                p.Blocks.Add((type, records));
            }
            p.LineCount = I(b, 72); p.Lines = b.AsSpan(I(b, 76), p.LineCount * 8).ToArray();
            p.SphereCount = I(b, 80); p.Spheres = b.AsSpan(I(b, 84), p.SphereCount * 8).ToArray();
            p.TextureCount = I(b, 88); p.Textures = b.AsSpan(I(b, 92), p.TextureCount * 4).ToArray();
            return p;
        }

        public byte[] Write()
        {
            using var s = new MemoryStream();
            using var w = new BinaryWriter(s);
            w.Write(Header);
            var offsets = new int[16];
            offsets[0] = Bones.Count; offsets[1] = (int)s.Position;
            foreach (var (parent, pivot, count, faces) in Bones) { w.Write((short)parent); w.Write((ushort)pivot); w.Write((ushort)count); w.Write((ushort)faces); }
            offsets[2] = Points.Count; offsets[3] = (int)s.Position;
            foreach (var (x, y, z, bone) in Points) { w.Write(x); w.Write(y); w.Write(z); w.Write((ushort)bone); }
            offsets[4] = Normals.Count; offsets[5] = (int)s.Position;
            foreach (var (x, y, z, bone) in Normals) { w.Write(x); w.Write(y); w.Write(z); w.Write((ushort)bone); }
            offsets[6] = FaceNormalCount; offsets[7] = (int)s.Position;
            w.Write(FaceNormals);
            offsets[8] = Blocks.Sum(k => k.Records.Count); offsets[9] = (int)s.Position;
            foreach (var (type, records) in Blocks.Where(k => k.Records.Count > 0))
            {
                w.Write((ushort)type); w.Write((ushort)records.Count); w.Write(8 + records.Count * Stride(type));
                foreach (var r in records) w.Write(r);
            }
            offsets[10] = LineCount; offsets[11] = (int)s.Position; w.Write(Lines);
            offsets[12] = SphereCount; offsets[13] = (int)s.Position; w.Write(Spheres);
            offsets[14] = TextureCount; offsets[15] = (int)s.Position; w.Write(Textures);
            var bytes = s.ToArray();
            for (var k = 0; k < 16; k++) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32 + k * 4), offsets[k]);
            return bytes;
        }

        // The points' places as the body stands (each bone's points from its pivot's).
        public Vector3[] World()
        {
            var world = new Vector3[Points.Count];
            var start = 0;
            for (var b = 0; b < Bones.Count; b++)
            {
                var origin = b == 0 ? Vector3.Zero : world[Bones[b].Pivot];
                for (var i = start; i < start + Bones[b].Count; i++) world[i] = new Vector3(Points[i].X, Points[i].Y, Points[i].Z) + origin;
                start += Bones[b].Count;
            }
            return world;
        }

        public int StartOf(int bone) => Bones.Take(bone).Sum(b => b.Count);
    }

    // A new polygon: its points (wound so that it faces out: Body.VertexNormals) and colour, drawn Gouraud lit (type 4) or flat and unlit
    // (type 0: not counted in its points' normals).
    private sealed record Poly(int[] Points, int Colour, bool Lit = true);

    // The torturer's body from the game's (`retail`: BODY.HQR Retail), its bytes, Scale times as big; `standing`: the body as it stands, for
    // the animations (not scaled).
    public static byte[] Make(byte[] retail, out Body standing)
    {
        var p = Parts.Read(retail);
        if (p.Bones.Count != Lash || p.Bones[Hand].Count < 13) throw new InvalidDataException($"BODY.HQR {Retail} isn't the grey Grobo with the club.");
        var world = p.World().ToList();
        var oldPoints = p.Points.Count;
        int handStart = p.StartOf(Hand), handCount = p.Bones[Hand].Count, handPivot = p.Bones[Hand].Pivot;
        var club = Enumerable.Range(handStart, handCount).ToHashSet();
        // (its club's polygons gone -- every one with a point of the hand's)
        var gone = 0;
        foreach (var (type, records) in p.Blocks)
        {
            var corners = (type & 0x8000) != 0 ? 4 : 3;
            gone += records.RemoveAll(r => Enumerable.Range(0, corners).Any(k => club.Contains(BinaryPrimitives.ReadUInt16LittleEndian(r.AsSpan(k * 2)))));
        }
        if (gone == 0) throw new InvalidDataException("The grey Grobo's club wasn't found in his hand.");

        var polys = new List<Poly>();
        var light = new Dictionary<int, float>();                   // (a point's share of the light: the hand's and the new ones)
        var added = new List<(Vector3 At, int Bone)>();              // (the points added, after the body's own: places as it stands)
        int Add(Vector3 at, int bone, float share) { added.Add((at, bone)); light[oldPoints + added.Count - 1] = share; return oldPoints + added.Count - 1; }
        Vector3[] Ring(Vector3 centre, float half) => Enumerable.Range(0, 4).Select(k => centre + new Vector3(MathF.Cos(k * MathF.PI / 2) * half, MathF.Sin(k * MathF.PI / 2) * half, 0)).ToArray();

        // the handle: the hand's points, along +z from the wrist (the club pointed so) -- a ring at the pommel's back, the pommel's ring, the
        // grip's two rings, and its end's middle (the lash's first pivot); the hand's other points at the wrist
        var wrist = world[handPivot];
        var used = 0;
        int[] HandRing(float z, float half, float share)
        {
            var ids = new int[4];
            foreach (var (q, k) in Ring(wrist + new Vector3(0, 0, z), half).Select((q, k) => (q, k)))
            {
                ids[k] = handStart + used++;
                world[ids[k]] = q;
                light[ids[k]] = share;
            }
            return ids;
        }
        var back = HandRing(-PommelBack, Pommel * 0.6f, BrassLight);
        var knob = HandRing(-PommelBack * 0.4f, Pommel, BrassLight);
        var gripA = HandRing(10, Grip, WoodLight);
        var gripB = HandRing(HandleLong, LashHalf[0], WoodLight);
        var handleEnd = handStart + used;
        world[handStart + used++] = wrist + new Vector3(0, 0, HandleLong);
        while (used < handCount) world[handStart + used++] = wrist;
        polys.Add(new Poly(new[] { back[3], back[2], back[1], back[0] }, Brass));
        Tube(polys, back, knob, Brass);
        Tube(polys, knob, gripA, Brass);
        Tube(polys, gripA, gripB, Wood);

        // the lash: each bone a length of it, its far end's ring and middle -- the next one's pivot -- the last one's tip
        var newBones = new List<(int Parent, int Pivot, int First, int Count)>();
        var (ringBefore, pivot) = (gripB, handleEnd);
        var along = wrist.Z + HandleLong;
        for (var k = 0; k < Lashes; k++)
        {
            along += LashLength[k];
            var centre = new Vector3(wrist.X, wrist.Y, along);
            var first = oldPoints + added.Count;
            var ring = Ring(centre, LashHalf[k + 1]).Select(q => Add(q, Lash + k, LeatherLight)).ToArray();
            var middle = Add(k + 1 < Lashes ? centre : centre + new Vector3(0, 0, Popper), Lash + k, LeatherLight);
            newBones.Add((k == 0 ? Hand : Lash + k - 1, pivot, first, ring.Length + 1));
            Tube(polys, ringBefore, ring, Leather);
            if (k + 1 == Lashes) for (var s = 0; s < 4; s++) polys.Add(new Poly(new[] { ring[s], ring[(s + 1) % 4], middle }, Leather));
            (ringBefore, pivot) = (ring, middle);
        }

        // the hood: a cowl round the head's top and back -- rings across it, front to back, each an arch from the left jaw over the top to
        // the right -- its brim over the brow, lined in red; its back closed behind a short peak; the ears through its sides
        var hoodFirst = oldPoints + added.Count;
        (float Z, (float X, float Y)[] Arch)[] arches =
        {
            (262, new[] { (-212f, 690f), (-226f, 880f), (-170f, 1022f), (0f, 1068f), (170f, 1022f), (226f, 880f), (212f, 690f) }),
            (190, new[] { (-226f, 660f), (-240f, 890f), (-180f, 1062f), (0f, 1118f), (180f, 1062f), (240f, 890f), (226f, 660f) }),
            (40, new[] { (-236f, 640f), (-248f, 892f), (-186f, 1092f), (0f, 1148f), (186f, 1092f), (248f, 892f), (236f, 640f) }),
            (-130, new[] { (-216f, 630f), (-226f, 868f), (-152f, 1062f), (0f, 1112f), (152f, 1062f), (226f, 868f), (216f, 630f) }),
            (-226, new[] { (-150f, 630f), (-156f, 840f), (-100f, 1002f), (0f, 1040f), (100f, 1002f), (156f, 840f), (150f, 630f) }),
        };
        var rings = arches.Select(r => r.Arch.Select(a => Add(new Vector3(a.X, a.Y, r.Z), Hood, BlackLight)).ToArray()).ToList();
        // (outside, seen from out of the hood: a ring's arch runs from its left to its right; the rings go front to back)
        for (var r = 0; r + 1 < rings.Count; r++)
            for (var a = 0; a + 1 < rings[r].Length; a++)
                polys.Add(new Poly(new[] { rings[r][a], rings[r][a + 1], rings[r + 1][a + 1], rings[r + 1][a] }, Black));
        // (the brim's inside, seen from under it: the same band wound the other way)
        for (var a = 0; a + 1 < rings[0].Length; a++)
            polys.Add(new Poly(new[] { rings[1][a], rings[1][a + 1], rings[0][a + 1], rings[0][a] }, Lining, Lit: false));
        var peak = Add(new Vector3(0, 980, -360), Hood, BlackLight);
        var bottom = Add(new Vector3(0, 640, -250), Hood, BlackLight);
        var last = rings[^1];
        for (var a = 0; a + 1 < last.Length; a++) polys.Add(new Poly(new[] { last[a], last[a + 1], peak }, Black));
        polys.Add(new Poly(new[] { last[^1], last[0], bottom }, Black));
        polys.Add(new Poly(new[] { last[^1], bottom, peak }, Black));
        polys.Add(new Poly(new[] { bottom, last[0], peak }, Black));
        newBones.Add((Head, p.Bones[Head].Pivot, hoodFirst, oldPoints + added.Count - hoodFirst));

        // ---- written: the hand's points moved, the new points after the body's own -- each from its bone's pivot
        world.AddRange(added.Select(a => a.At));
        for (var k = 0; k < handCount; k++)
        {
            var q = world[handStart + k] - wrist;
            p.Points[handStart + k] = ((short)Math.Round(q.X), (short)Math.Round(q.Y), (short)Math.Round(q.Z), Hand);
        }
        for (var b = 0; b < newBones.Count; b++)
        {
            var (parent, piv, first, count) = newBones[b];
            p.Bones.Add((parent, piv, count, 0));
            for (var i = first; i < first + count; i++)
            {
                var q = world[i] - world[piv];
                p.Points.Add(((short)Math.Round(q.X), (short)Math.Round(q.Y), (short)Math.Round(q.Z), Lash + b));
            }
        }
        // (the flat-lit polygons' lights are found past every point: on by the points added)
        foreach (var (_, records) in p.Blocks)
            foreach (var r in records)
            {
                var normal = BinaryPrimitives.ReadUInt16LittleEndian(r.AsSpan(10));
                if (normal >= oldPoints) BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(10), (ushort)(normal + added.Count));
            }
        // (the normals of the hand's points and the new ones: from the lit polygons round them, as long as their share of the light)
        var sum = new Dictionary<int, Vector3>();
        foreach (var poly in polys.Where(q => q.Lit))
        {
            var n = Newell(poly.Points.Select(i => world[i]).ToArray());
            foreach (var i in poly.Points) sum[i] = sum.GetValueOrDefault(i) + n;
        }
        (short, short, short, int) NormalOf(int i, int bone)
        {
            var n = sum.TryGetValue(i, out var v) && v.LengthSquared() > 1e-9f ? Vector3.Normalize(v) * 10240 * light.GetValueOrDefault(i, 1f) : Vector3.Zero;
            return ((short)Math.Round(n.X), (short)Math.Round(n.Y), (short)Math.Round(n.Z), bone);
        }
        for (var k = 0; k < handCount; k++) p.Normals[handStart + k] = NormalOf(handStart + k, Hand);
        for (var b = 0; b < newBones.Count; b++)
            for (var i = newBones[b].First; i < newBones[b].First + newBones[b].Count; i++) p.Normals.Add(NormalOf(i, Lash + b));
        // (the new polygons: a block each of lit and unlit triangles and quads)
        foreach (var group in polys.GroupBy(q => (Quad: q.Points.Length == 4, q.Lit)))
        {
            var type = (group.Key.Quad ? 0x8000 : 0) | (group.Key.Lit ? 4 : 0);
            p.Blocks.Add((type, group.Select(q =>
            {
                var r = new byte[12];
                for (var k = 0; k < q.Points.Length; k++) BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(k * 2), (ushort)q.Points[k]);
                BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(8), (ushort)q.Colour);
                return r;
            }).ToList()));
        }
        // (its box, as it stands)
        int[] box = { (int)Math.Floor(world.Min(v => v.X)), (int)Math.Ceiling(world.Max(v => v.X)), (int)Math.Floor(world.Min(v => v.Y)), (int)Math.Ceiling(world.Max(v => v.Y)),
                      (int)Math.Floor(world.Min(v => v.Z)), (int)Math.Ceiling(world.Max(v => v.Z)) };
        for (var k = 0; k < 6; k++) BinaryPrimitives.WriteInt32LittleEndian(p.Header.AsSpan(8 + k * 4), box[k]);
        var bytes = p.Write();
        standing = LbaBodyStudio.Body.Read(bytes, 2, allowStatic: true);
        return RaceTrackSmallCars.Scaled(bytes, Scale);
    }

    // ---- the animations -----------------------------------------------------------------------------------------------------------

    // A pose: where the whip arm's bones point (the upper arm, the forearm, the handle -- the body's own frame: +z his front, +y up; his
    // whip arm on his -x side), the way the lash runs from the handle's end (a line through these places, from the handle's end -- the
    // first is that end itself; the lash's lengths laid along it), the other arm's (null: as the game's standing has it), and the torso
    // turned (radians, its Alpha -- leaning forward -- and Beta -- the whip side forward -- on the game's standing).
    internal sealed record Pose(Vector3 Upper, Vector3 Fore, Vector3 Handle, Func<Vector3, Vector3[]> LashWay, Vector3? OtherUpper = null, Vector3? OtherFore = null,
        float Lean = 0, float Twist = 0);

    // (model units over the floor: the lash lying on it)
    private const float OnFloor = 22;
    // standing: the whip hand by his side a little forward, the handle down and forward, the lash down to the floor and along it in front of
    // him, stirring (`k`: the game's standing's keyframe), the other arm hanging
    private static Pose Standing(int k) => new(new Vector3(-0.42f, -0.88f, 0.22f), new Vector3(-0.05f, -0.55f, 0.83f), new Vector3(0.05f, -0.6f, 0.8f),
        p => new[] { p, p + new Vector3(0, -110, 110), new Vector3(p.X - 40 + 30 * Sway(k), OnFloor, p.Z + 600), new Vector3(p.X + 60 - 70 * Sway(k), OnFloor, p.Z + 1250),
                     new Vector3(p.X - 30 + 110 * Sway(k + 1), OnFloor, p.Z + 1800) },
        new Vector3(0.45f, -0.88f, 0.12f), new Vector3(0.08f, -0.82f, 0.55f));
    private static float Sway(int k) => (k % 4) switch { 0 => 0, 1 => 1, 2 => 0, _ => -1 };

    // the crack's poses after standing: the arm up and back, the lash out behind; over the top, the lash following in a loop; the crack;
    // the follow-through, the lash on the floor in front
    private static readonly Pose[] Cracking =
    {
        new(new Vector3(-0.25f, 0.8f, -0.55f), new Vector3(-0.05f, 0.7f, -0.7f), new Vector3(0, 0.35f, -0.94f),
            p => new[] { p, p + new Vector3(0, 60, -500), p + new Vector3(0, 0, -1100), p + new Vector3(30, -250, -1700) },
            new Vector3(0.35f, -0.3f, 0.88f), new Vector3(0.1f, 0.1f, 1), Lean: -0.12f, Twist: -0.35f),
        new(new Vector3(-0.2f, 0.97f, 0.15f), new Vector3(-0.05f, 0.55f, 0.83f), new Vector3(0, 0.2f, 0.98f),
            p => new[] { p, p + new Vector3(0, 250, -250), p + new Vector3(0, 420, -700), p + new Vector3(0, 300, -1150), p + new Vector3(0, 0, -1350), p + new Vector3(0, -200, -1150) },
            new Vector3(0.4f, -0.5f, 0.75f), new Vector3(0.1f, -0.2f, 0.98f), Lean: 0.02f, Twist: -0.1f),
        new(new Vector3(-0.15f, 0.25f, 0.96f), new Vector3(0, -0.15f, 0.99f), new Vector3(0, -0.12f, 0.99f),
            p => new[] { p, p + new Vector3(0, -60, 1100), p + new Vector3(0, -130, 1500), p + new Vector3(0, -330, 1780) },
            new Vector3(0.45f, -0.6f, -0.55f), new Vector3(0.1f, -0.7f, -0.6f), Lean: 0.22f, Twist: 0.25f),
        new(new Vector3(-0.2f, -0.35f, 0.92f), new Vector3(0, -0.65f, 0.76f), new Vector3(0, -0.75f, 0.66f),
            p => new[] { p, new Vector3(p.X, OnFloor, p.Z + 160), new Vector3(p.X + 60, OnFloor, p.Z + 900), new Vector3(p.X - 50, OnFloor, p.Z + 1800) },
            new Vector3(0.45f, -0.7f, -0.3f), new Vector3(0.1f, -0.85f, -0.3f), Lean: 0.25f, Twist: 0.3f),
    };

    // The animations: standing (looped) and the crack (once), for the body as it stands (`body`: Make's; its first bones the game's), from
    // the game's standing (`retailIdle`: the entity's animation 0, ANIM.HQR). Its translations Scale times as big, as the body.
    public static (Anim Idle, Anim Crack) Anims(Body body, byte[] retailIdle)
    {
        var retail = ReadFrames(retailIdle, body.Bones.Count);
        var idle = new Anim { Game = 2, LoopFrame = 0 };
        for (var k = 0; k < retail.Count; k++) idle.Frames.Add(Frame(body, retail[k].Time, retail[k].Bones, Standing(k)));
        var crack = new Anim { Game = 2, LoopFrame = CrackMs.Length - 1 };
        var poses = new[] { Standing(0) }.Concat(Cracking).Append(Standing(0)).ToArray();
        for (var k = 0; k < poses.Length; k++) crack.Frames.Add(Frame(body, CrackMs[k], retail[0].Bones, poses[k]));
        return (idle, crack);
    }

    // An animation's keyframes, each bone's (type, and its three values: radians, or a translation's units), as many bones as `bones` (the
    // ones it hasn't still).
    internal static List<(int Time, (int Type, double A, double B, double C)[] Bones)> ReadFrames(byte[] d, int bones)
    {
        int frames = BinaryPrimitives.ReadUInt16LittleEndian(d), have = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(2));
        var list = new List<(int, (int, double, double, double)[])>();
        for (var f = 0; f < frames; f++)
        {
            var at = 8 + f * (8 + have * 8);
            var values = new (int, double, double, double)[bones];
            for (var b = 0; b < Math.Min(have, bones); b++)
            {
                var q = at + 8 + b * 8;
                var type = BinaryPrimitives.ReadInt16LittleEndian(d.AsSpan(q)) & 1;
                double V(int o) { var v = BinaryPrimitives.ReadInt16LittleEndian(d.AsSpan(q + o)); return type == 1 ? v : (((v & 0xFFF) ^ 0x800) - 0x800) * Math.PI * 2 / 4096; }
                values[b] = (type, V(2), V(4), V(6));
            }
            list.Add((BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(at)), values));
        }
        return list;
    }

    // A keyframe: the game's standing's values (`basis`), the torso turned, then each of the pose's bones aimed in turn -- from the shoulder
    // down to the lash's end -- each turned from its parent as it stands now.
    internal static AnimFrame Frame(Body body, int time, (int Type, double A, double B, double C)[] basis, Pose pose)
    {
        var v = Aim(body, basis, pose);
        return new AnimFrame(time, 0, 0, 0, v.Select((b, i) => i == 0 ? AnimBone.Neutral
            : b.Type == 1 ? new AnimBone(1, (float)(b.A * Scale), (float)(b.B * Scale), (float)(b.C * Scale))
            : new AnimBone(0, (float)(b.A / (2 * Math.PI)), (float)(b.B / (2 * Math.PI)), (float)(b.C / (2 * Math.PI)))).ToArray());
    }

    // The bones' values for a pose (radians; translations in the body's units, not scaled).
    internal static (int Type, double A, double B, double C)[] Aim(Body body, (int Type, double A, double B, double C)[] basis, Pose pose)
    {
        var v = ((int Type, double A, double B, double C)[])basis.Clone();
        v[Torso] = (0, v[Torso].A + pose.Lean, v[Torso].B + pose.Twist, v[Torso].C);
        var rest = body.World();
        Vector3 Pivot(int b) => rest[body.Bones[b].Pivot];
        Vector3 Ends(int b) { var pts = Enumerable.Range(body.Bones[b].Start, body.Bones[b].Count).Select(i => rest[i]).ToList(); return pts.Aggregate(Vector3.Zero, (s, q) => s + q) / pts.Count; }
        void Point(int bone, Vector3 from, Vector3 to)
        {
            var (_, matrices) = Posed(body, v);
            var parent = matrices[body.Bones[bone].Parent];
            v[bone] = Turn(parent, Vector3.Normalize(from), Vector3.Normalize(to));
        }
        if (pose.OtherUpper is { } ou) Point(OtherUpper, Pivot(OtherFore) - Pivot(OtherUpper), ou);
        if (pose.OtherFore is { } of) Point(OtherFore, Ends(OtherFore) - Pivot(OtherFore), of);
        Point(Upper, Pivot(Fore) - Pivot(Upper), pose.Upper);
        Point(Fore, Pivot(Hand) - Pivot(Fore), pose.Fore);
        Point(Hand, Vector3.UnitZ, pose.Handle);
        // (the lash laid along its way: each length's end where the way has gone that far from the last one's)
        var (points, _) = Posed(body, v);
        var start = points[body.Bones[Lash].Pivot];
        var way = pose.LashWay(start);
        var at = start;
        var leg = 1;
        for (var k = 0; k < Lashes; k++)
        {
            var next = Along(way, ref leg, at, LashLength[k]);
            Point(Lash + k, Vector3.UnitZ, next - at);
            at = next;
        }
        return v;
    }

    // The place `length` on from `at` along a way (a line through places; `leg`: the stretch of it `at` is on, moved on as it goes): the
    // first place on it that far from `at` (straight on past its end).
    private static Vector3 Along(Vector3[] way, ref int leg, Vector3 at, float length)
    {
        for (; leg < way.Length; leg++)
        {
            Vector3 a = way[leg - 1], b = way[leg], d = b - a;
            // (where on this stretch -- from a to b -- the distance from `at` is `length`: the furthest such place)
            float qa = d.LengthSquared(), qb = 2 * Vector3.Dot(d, a - at), qc = (a - at).LengthSquared() - length * length;
            var disc = qb * qb - 4 * qa * qc;
            if (qa < 1e-6f || disc < 0) continue;
            var t = (-qb + MathF.Sqrt(disc)) / (2 * qa);
            if (t >= 0 && t <= 1) return a + d * t;
        }
        var end = way[^1] - way[^2];
        return at + (end.LengthSquared() > 1e-6f ? Vector3.Normalize(end) : Vector3.UnitZ) * length;
    }

    // The body posed by the values (radians): its points and each bone's frame (Lba1Pose's: 3 x 3, row by row).
    internal static (Vector3[] Points, double[][] Matrices) Posed(Body body, (int Type, double A, double B, double C)[] v)
    {
        const double Units = 1024 / (2 * Math.PI);
        var points = Lba1Pose.World(body, v.Select(b => b.Type == 1 ? (1, b.A, b.B, b.C) : (0, b.A * Units, b.B * Units, b.C * Units)).ToArray(), out var matrices);
        return (points, matrices);
    }

    // A bone's own turn (in its parent's frame `m`) that points its rest direction `from` along `to`: the parent's frame turned the
    // shortest way from where the bone would point unturned to `to` -- as the engine's angles (M(Alpha) M(Gamma) M(Beta)).
    internal static (int, double, double, double) Turn(double[] m, Vector3 from, Vector3 to)
    {
        Vector3 Apply(double[] r, Vector3 p) => new((float)(r[0] * p.X + r[1] * p.Y + r[2] * p.Z), (float)(r[3] * p.X + r[4] * p.Y + r[5] * p.Z), (float)(r[6] * p.X + r[7] * p.Y + r[8] * p.Z));
        var u = Vector3.Normalize(Apply(m, from));
        // (the shortest turn from u to `to`, as a matrix: Rodrigues')
        var axis = Vector3.Cross(u, to);
        double s = axis.Length(), c = Vector3.Dot(u, to);
        var q = new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        if (s > 1e-6)
        {
            axis /= (float)s;
            double x = axis.X, y = axis.Y, z = axis.Z, t = 1 - c;
            q = new[] { c + x * x * t, x * y * t - z * s, x * z * t + y * s, y * x * t + z * s, c + y * y * t, y * z * t - x * s, z * x * t - y * s, z * y * t + x * s, c + z * z * t };
        }
        else if (c < 0) q = new double[] { -1, 0, 0, 0, 1, 0, 0, 0, -1 };   // (half round, about y)
        // (the bone's own: the parent's frame undone -- its transpose -- the turn, the parent's frame)
        double[] Mul(double[] a, double[] b)
        {
            var r = new double[9];
            for (var i = 0; i < 3; i++) for (var j = 0; j < 3; j++) r[i * 3 + j] = a[i * 3] * b[j] + a[i * 3 + 1] * b[3 + j] + a[i * 3 + 2] * b[6 + j];
            return r;
        }
        var mt = new[] { m[0], m[3], m[6], m[1], m[4], m[7], m[2], m[5], m[8] };
        var l = Mul(Mul(mt, q), m);
        // (L = M(Alpha) M(Gamma) M(Beta): L01 = -sin Gamma, L00 = cos Gamma cos Beta, L02 = cos Gamma sin Beta, L11 = cos Alpha cos Gamma,
        // L21 = sin Alpha cos Gamma -- IMATSTDF.CPP)
        var gamma = Math.Asin(Math.Clamp(-l[1], -1, 1));
        var beta = Math.Atan2(l[2], l[0]);
        var alpha = Math.Atan2(l[7], l[4]);
        return (0, alpha, beta, gamma);
    }

    // the four sides between two rings of four along +z (from `a` to `b`), facing out
    private static void Tube(List<Poly> polys, int[] a, int[] b, int colour)
    {
        for (var s = 0; s < 4; s++) polys.Add(new Poly(new[] { a[s], a[(s + 1) % 4], b[(s + 1) % 4], b[s] }, colour));
    }

    internal static Vector3 Newell(Vector3[] p)
    {
        var n = Vector3.Zero;
        for (var i = 0; i < p.Length; i++) { var (a, b) = (p[i], p[(i + 1) % p.Length]); n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y)); }
        return n;
    }
}
