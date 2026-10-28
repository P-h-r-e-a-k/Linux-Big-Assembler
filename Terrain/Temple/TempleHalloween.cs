using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain.Temple;

// The Temple of Bú's Halloween (the user, 2026-10-16: "Let's make it as creepy, scary, and Halloween inspired as we can do, let's turn our
// mushroom pickups into mini pumpkins. Add lots of typical Halloween decor, pumpkins, witches, ghosts etc"), in the plan's Halloween:
//   - Decor: what stands about the temple's floors beside the road, and hangs in the dark round its spirals -- jack-o'-lanterns, a giant one
//     at the corners, gravestones and crosses with candles, a witch's cauldron bubbling green -- each a decor of the island's, drawn only
//     (nothing to bump into);
//   - Spooks: what flies -- witches on their broomsticks round the spirals and over the halls, throwing fire balls at the cars below them;
//     ghosts drifting to and fro over the road; flying rats (bats until 2026-10-18) wheeling high over it: bodies of BODY.HQR that the
//     race-track mode flies and draws itself (RACEMOD.CPP spook=);
//   - the power-ups' mushrooms made mini pumpkins: the mushroom's entity's body Pumpkin (the item box shows the lap's own).
// The models are flat polygons in the island's palette (the Temple of Bú's: Otringal's, LBA1's colours written in), each face shaded by the
// way it faces from a light above and in front (Paint: its colours from dark to light), the carved faces, the flames and the brew in the
// palette's fire colours (240..247: the same in every island's palette) as they are -- they glow in the dark.
internal sealed class HalloweenRun
{
    // [kind, x, z, y, turn] each: island cells, the floor's height, a turn (0..4095) -- the kinds: Kinds
    public double[][]? Decor { get; set; }
    public SpookRun[]? Spooks { get; set; }
    // the power-ups' mushrooms are mini pumpkins
    public bool Pumpkins { get; set; } = true;
    // (2026-10-17) the places a witch dives at Twinsen's car from (RACEMOD.CPP dive=), each [x, z, the road's height there] (island cells);
    // what jumps up out of the floor beside the road as his car comes by (scare=), each [0 a skeleton | 1 a ghost, x, z, the floor's height,
    // the way it faces]; and the ghost power-up (haunt=)
    public double[][]? Dives { get; set; }
    public double[][]? Scares { get; set; }
    public bool Haunt { get; set; }
    // (2026-10-17) the giant webs the lap drives through (RACEMOD.CPP web=: TempleHalloween.Cobwebs), each [x, z, the road's height there,
    // the lap's way through it x and z, how far its posts stand either side of the road's middle (cells)]
    public double[][]? GiantWebs { get; set; }
}

// A spook (RACEMOD.CPP spook=): what it is (witch, ghost, rat -- a bat before 2026-10-18 --, riser), its flags (1 throws fire balls, 2
// round and round its way, 8 standing at its way's first point, facing its second), the height it flies at, and its way (island cells).
internal sealed class SpookRun
{
    public string Kind { get; set; } = "ghost";
    public int Flags { get; set; }
    public double Y { get; set; }
    public double[][] Way { get; set; } = Array.Empty<double[]>();
}

internal static partial class TempleHalloween
{
    // (and since 2026-10-17 LBA1's skeleton in four poses: TempleHalloween.Skeletons; and cobwebs and spiders: TempleHalloween.Cobwebs)
    public enum Kind
    {
        Lantern, GiantLantern, Grave, Cross, Candles, Cauldron, SkeletonLying, SkeletonSlumped, SkeletonHanging, SkeletonChained,
        CobwebCorner, CobwebCornerBig, CobwebSheet, CobwebDrape, Spider, CobwebWall,
        // (2026-10-18: LBA2's rabbibunny skeleton, TempleHalloween.Creatures)
        RabbiLying, RabbiSitting, RabbiStanding, RabbiCrouching,
    }
    private static bool IsCobweb(Kind kind) => kind is >= Kind.CobwebCorner and <= Kind.CobwebWall;
    // (a bat before 2026-10-18: the flying rat since -- a plan that still has one gets the rat; a riser is the rabbibunny skeleton by the road)
    public static readonly string[] SpookKinds = { "witch", "ghost", "rat", "riser" };
    // the mushroom's entity's body the power-ups' pumpkins are (RaceTrackOil: 0 the game's mushroom, 1 the oil slick, 2 the big mushroom)
    public const int Pumpkin = 3;

    // ---- colours: shades of the island's palette, dark to light (Paint), and the fire colours, which glow
    private sealed record Paint(int[] Shades, bool Glow = false);
    private static readonly Paint PumpkinSkin = new(new[] { 85, 86, 241, 242, 243 });
    private static readonly Paint Carved = new(new[] { 244 }, Glow: true);
    private static readonly Paint Stem = new(new[] { 113, 115, 116, 117, 118 });
    // (52 is a temple colour now, a teal: the grey between 51 and 53 in the temple's palette is 212 -- the 2026-10-18 audit's teal step)
    private static readonly Paint Stone = new(new[] { 49, 50, 51, 212, 53, 54, 55 });
    private static readonly Paint StoneDark = new(new[] { 48, 49, 50, 51 });
    private static readonly Paint Moss = new(new[] { 112, 114, 116 });
    private static readonly Paint Wax = new(new[] { 57, 58, 59, 60, 61, 62 });
    private static readonly Paint Flame = new(new[] { 245 }, Glow: true);
    private static readonly Paint Iron = new(new[] { 224, 225, 226, 227, 209 });
    private static readonly Paint Brew = new(new[] { 247 }, Glow: true);
    private static readonly Paint Sheet = new(new[] { 219, 220, 221, 222, 223 });
    private static readonly Paint Hollow = new(new[] { 224 }, Glow: true);
    private static readonly Paint Robe = new(new[] { 208, 209, 210, 211 });
    private static readonly Paint Hat = new(new[] { 224, 225, 226, 209 });
    private static readonly Paint Skin = new(new[] { 135, 136, 137, 138, 139 });
    private static readonly Paint Wood = new(new[] { 18, 19, 20, 21, 22 });
    private static readonly Paint Straw = new(new[] { 102, 103, 104, 105, 106 });
    private static readonly Paint Wing = new(new[] { 224, 225, 226 });
    private static readonly Paint RedEye = new(new[] { 249 }, Glow: true);
    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-0.35f, 0.85f, 0.4f));

    // ---- a model: flat faces in world units, each its paint; written as a body of one bone, unlit
    private sealed class Model
    {
        public readonly List<Vector3> Points = new();
        public readonly List<(int[] Ids, Paint Paint, bool Both)> Faces = new();
        // (a point once: faces meeting there share it)
        private readonly Dictionary<(int, int, int), int> index = new();
        public int P(Vector3 v)
        {
            var key = ((int)MathF.Round(v.X), (int)MathF.Round(v.Y), (int)MathF.Round(v.Z));
            if (index.TryGetValue(key, out var i)) return i;
            Points.Add(v); return index[key] = Points.Count - 1;
        }
        // The points from `first` on moved (a part built round one axis laid along another): the index of points by place made again.
        public void Move(int first, Func<Vector3, Vector3> move)
        {
            for (var i = first; i < Points.Count; i++) Points[i] = move(Points[i]);
            index.Clear();
            for (var i = 0; i < Points.Count; i++) index.TryAdd(((int)MathF.Round(Points[i].X), (int)MathF.Round(Points[i].Y), (int)MathF.Round(Points[i].Z)), i);
        }

        // a face, its corners as given -- turned so that it faces away from `inside` -- or seen from both sides
        public void Face(Vector3[] corners, Paint paint, Vector3 inside, bool both = false)
        {
            // (a body holds triangles and quads: more corners a fan of triangles)
            if (corners.Length > 4)
            {
                for (var k = 1; k + 1 < corners.Length; k++) Face(new[] { corners[0], corners[k], corners[k + 1] }, paint, inside, both);
                return;
            }
            var ids = corners.Select(P).ToArray();
            var n = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]);
            var centre = corners.Aggregate(Vector3.Zero, (s, c) => s + c) / corners.Length;
            if (Vector3.Dot(n, centre - inside) < 0) ids = ids.Reverse().ToArray();
            Faces.Add((ids, paint, both));
        }
        public void Facing(Vector3[] corners, Paint paint, Vector3 towards, bool both = false)
        {
            var centre = corners.Aggregate(Vector3.Zero, (s, c) => s + c) / corners.Length;
            Face(corners, paint, centre - towards, both);
        }

        // a face as a game's body has it: its corners wound outwards (their Newell normal out of the surface, Body.VertexNormals), kept so
        public void Outward(Vector3[] corners, Paint paint)
        {
            var n = Vector3.Zero;
            for (var i = 0; i < corners.Length; i++)
            {
                var a = corners[i]; var b = corners[(i + 1) % corners.Length];
                n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
            }
            if (n.LengthSquared() < 1e-6f) return;
            var centre = corners.Aggregate(Vector3.Zero, (s, c) => s + c) / corners.Length;
            Face(corners, paint, centre - Vector3.Normalize(n) * 10);
        }

        // a chain from a to b: links `link` long and `t` thick (every other one thinner), each a little into the next
        public void Chain(Vector3 a, Vector3 b, float link, float t)
        {
            var d = b - a; var len = d.Length();
            if (len < 1) return;
            d /= len;
            var k = 0;
            for (var s = 0f; s < len; s += link * 0.85f, k++)
                Bar(a + d * s, a + d * MathF.Min(len, s + link), k % 2 == 0 ? t : t * 0.6f, Iron);
        }

        // A turned shape about the y axis through (cx, cz): rings of `sides` points at each (radius, height) of the profile from the bottom
        // up, the radius at each point round a ring times shape(angle); band i in paint(i); closed at the bottom and the top when asked.
        public void Lathe((double R, double Y)[] profile, int sides, Func<int, Paint> paint, bool bottom, bool top, Func<double, double>? shape = null,
            Vector3? at = null, Func<double, double, double>? yShape = null)
        {
            var c = at ?? Vector3.Zero;
            Vector3 Pt(int ring, int k)
            {
                var a = k * 2 * Math.PI / sides;
                var r = profile[ring].R * (shape?.Invoke(a) ?? 1);
                var y = profile[ring].Y + (yShape?.Invoke(a, ring) ?? 0);
                return c + new Vector3((float)(r * Math.Cos(a)), (float)y, (float)(r * Math.Sin(a)));
            }
            for (var i = 0; i + 1 < profile.Length; i++)
            {
                var inside = c + new Vector3(0, (float)((profile[i].Y + profile[i + 1].Y) / 2), 0);
                for (var k = 0; k < sides; k++)
                {
                    var q = new[] { Pt(i, k), Pt(i, (k + 1) % sides), Pt(i + 1, (k + 1) % sides), Pt(i + 1, k) };
                    if (profile[i].R < 1e-6) q = new[] { q[0], q[2], q[3] };
                    else if (profile[i + 1].R < 1e-6) q = new[] { q[0], q[1], q[2] };
                    Face(q, paint(i), inside);
                }
            }
            if (bottom && profile[0].R > 1e-6)
                for (var k = 0; k < sides; k++) Face(new[] { c + new Vector3(0, (float)profile[0].Y, 0), Pt(0, k), Pt(0, (k + 1) % sides) }, paint(0), c + new Vector3(0, (float)profile[0].Y + 10, 0));
            var last = profile.Length - 1;
            if (top && profile[last].R > 1e-6)
                for (var k = 0; k < sides; k++) Face(new[] { c + new Vector3(0, (float)profile[last].Y, 0), Pt(last, k), Pt(last, (k + 1) % sides) }, paint(last - 1), c + new Vector3(0, (float)profile[last].Y - 10, 0));
        }

        // (2026-10-17) A strand of silk along `path`, `width` wide: a thin flat ribbon facing `normal` (and the other way too when `both`),
        // its pieces sharing their corners where they meet (mitred), each no longer than `piece` (0: as the path's) -- a big web's: a
        // polygon the camera passes close to is cut at the near plane, and the filler loses the depth of a long one running away from it.
        public void Strand(IReadOnlyList<Vector3> path, float width, Paint paint, Vector3 normal, bool both, float piece = 0)
        {
            var pts = new List<Vector3> { path[0] };
            for (var i = 1; i < path.Count; i++)
            {
                var d = path[i] - pts[^1];
                if (d.Length() < 1) continue;
                var pieces = piece > 0 ? Math.Max(1, (int)Math.Ceiling(d.Length() / piece)) : 1;
                var from = pts[^1];
                for (var k = 1; k <= pieces; k++) pts.Add(from + d * (k / (float)pieces));
            }
            if (pts.Count < 2) return;
            // (each piece's side: across it in the ribbon's plane; at a joint the two sides' middle, longer as the joint is sharper)
            var sides = new Vector3[pts.Count - 1];
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var s = Vector3.Cross(Vector3.Normalize(pts[i + 1] - pts[i]), normal);
                sides[i] = s.LengthSquared() < 1e-6f ? Vector3.UnitX : Vector3.Normalize(s);
            }
            var offsets = new Vector3[pts.Count];
            for (var i = 0; i < pts.Count; i++)
            {
                if (i == 0 || i == pts.Count - 1) { offsets[i] = sides[Math.Min(i, sides.Length - 1)] * width / 2; continue; }
                var m = sides[i - 1] + sides[i];
                if (m.LengthSquared() < 1e-6f) { offsets[i] = sides[i] * width / 2; continue; }
                m = Vector3.Normalize(m);
                offsets[i] = m * width / 2 / MathF.Max(0.4f, Vector3.Dot(m, sides[i]));
            }
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                Vector3 p = pts[i], q = pts[i + 1];
                Face(new[] { p - offsets[i], q - offsets[i + 1], q + offsets[i + 1], p + offsets[i] }, paint, (p + q) / 2 - normal * 10, both);
            }
        }

        // a box between two corners
        public void Box(Vector3 lo, Vector3 hi, Paint paint)
        {
            var c = (lo + hi) / 2;
            Vector3 V(int i) => new((i & 1) != 0 ? hi.X : lo.X, (i & 2) != 0 ? hi.Y : lo.Y, (i & 4) != 0 ? hi.Z : lo.Z);
            foreach (var q in new[] { new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 }, new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 }, new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 } })
                Face(q.Select(V).ToArray(), paint, c);
        }

        // a bar from a to b, t thick (four sides)
        public void Bar(Vector3 a, Vector3 b, float t, Paint paint)
        {
            var d = Vector3.Normalize(b - a);
            var u = Vector3.Normalize(Vector3.Cross(d, Math.Abs(d.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY)) * t / 2;
            var v = Vector3.Normalize(Vector3.Cross(d, u)) * t / 2;
            var cs = new[] { u + v, u - v, -u - v, -u + v };
            var mid = (a + b) / 2;
            for (var k = 0; k < 4; k++) Face(new[] { a + cs[k], a + cs[(k + 1) % 4], b + cs[(k + 1) % 4], b + cs[k] }, paint, mid);
            Face(cs.Select(c => a + c).ToArray(), paint, mid); Face(cs.Select(c => b + c).ToArray(), paint, mid);
        }

        // A flat outline (x, y) pushed out `depth` along z, centred on z = 0: its two faces and its rim.
        public void Slab((double X, double Y)[] outline, float depth, Paint face, Paint rim, float z0 = 0)
        {
            var front = outline.Select(p => new Vector3((float)p.X, (float)p.Y, z0 + depth / 2)).ToArray();
            var back = outline.Select(p => new Vector3((float)p.X, (float)p.Y, z0 - depth / 2)).ToArray();
            var c = new Vector3((float)outline.Average(p => p.X), (float)outline.Average(p => p.Y), z0);
            // (the outline as a fan from its middle: it is convex, or near enough)
            for (var k = 0; k < outline.Length; k++)
            {
                var k2 = (k + 1) % outline.Length;
                Face(new[] { new Vector3(c.X, c.Y, z0 + depth / 2), front[k], front[k2] }, face, c);
                Face(new[] { new Vector3(c.X, c.Y, z0 - depth / 2), back[k], back[k2] }, face, c);
                Face(new[] { front[k], front[k2], back[k2], back[k] }, rim, c);
            }
        }

        // As a body the race-track mode draws as an object of its own (the spooks'), made as the giant frog's is (CarMesh, an actor's body
        // with `header`, the racer's): its faces unlit in their shades.
        public byte[] WriteMoving(byte[] header)
        {
            var mesh = new CarMesh("spook", new[] { -1 });
            var ids = Points.Select(p => mesh.P(0, p)).ToArray();
            foreach (var (face, colour) in Shaded())
                mesh.Raw(face.Select(i => ids[i]).ToArray(), colour, unlit: true);
            var body = mesh.ToBody(header);
            var bytes = body.Write();
            BinaryPrimitives.WriteInt32LittleEndian(bytes, BinaryPrimitives.ReadInt32LittleEndian(bytes) | Palace.BrickBuilding.CloseFlag);
            return bytes;
        }

        // the faces with each one's colour: its paint's shade for the way it faces (both sides of a two-sided one)
        private List<(int[] Ids, int Colour)> Shaded()
        {
            var list = new List<(int[] Ids, int Colour)>();
            foreach (var (ids, paint, both) in Faces)
            {
                var p = ids.Select(i => Points[i]).ToArray();
                var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                var shade = paint.Shades[0];
                if (!paint.Glow && n.LengthSquared() > 0)
                {
                    var lit = 0.5f + 0.5f * Vector3.Dot(Vector3.Normalize(n), Light);
                    shade = paint.Shades[Math.Clamp((int)Math.Round(lit * (paint.Shades.Length - 1)), 0, paint.Shades.Length - 1)];
                }
                list.Add((ids, shade));
                if (both) list.Add((ids.Reverse().ToArray(), paint.Glow ? shade : paint.Shades[Math.Max(0, paint.Shades.Length / 3)]));
            }
            return list;
        }

        public int FaceCount => Faces.Sum(f => f.Both ? 2 : 1);

        public byte[] Write(bool moving = false)
        {
            var faces = new List<Face>();
            foreach (var (ids, paint, both) in Faces)
            {
                var p = ids.Select(i => Points[i]).ToArray();
                var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                var shade = paint.Shades[0];
                if (!paint.Glow && n.LengthSquared() > 0)
                {
                    var lit = 0.5f + 0.5f * Vector3.Dot(Vector3.Normalize(n), Light);
                    shade = paint.Shades[Math.Clamp((int)Math.Round(lit * (paint.Shades.Length - 1)), 0, paint.Shades.Length - 1)];
                }
                faces.Add(new Face(ids, shade, Material: 0));
                if (both) faces.Add(new Face(ids.Reverse().ToArray(), paint.Glow ? shade : paint.Shades[Math.Max(0, paint.Shades.Length / 3)], Material: 0));
            }
            if (Points.Count > 540 || faces.Count > 540) throw new InvalidOperationException($"A Halloween model of {Points.Count} points and {faces.Count} faces is more than a body holds.");
            // (the header's first byte: the body format's version, 0x10 as every retail body has it -- the engine's object drawing reads the
            // polygons by it, and a 0 crashed it, 2026-10-16)
            var header = new byte[96];
            header[0] = 0x10;
            var bytes = new Body
            {
                Game = 2, Static = !moving, Lit = false, Header = header,
                Vertices = Points,
                Bones = new List<Bone> { new(0, Points.Count, 0, -1, new byte[8]) },
                Faces = faces,
            }.Write();
            // (drawn close to the camera: its polygons cut at the near plane only a little in front of it)
            BinaryPrimitives.WriteInt32LittleEndian(bytes, BinaryPrimitives.ReadInt32LittleEndian(bytes) | Palace.BrickBuilding.CloseFlag);
            return bytes;
        }
    }

    // ---- the models
    // A jack-o'-lantern R across its middle and H tall, its foot at 0, its face looking +z: a pumpkin of eight lobes, its stem, its carved
    // eyes, nose and grin glowing.
    private static Model Lantern(double R, double H, int sides = 24, int rings = 9)
    {
        var m = new Model();
        var profile = Enumerable.Range(0, rings + 1).Select(i =>
        {
            var t = i / (double)rings;
            return (R * Math.Pow(Math.Sin(Math.PI * (0.06 + 0.88 * t)), 0.75), H * t);
        }).ToArray();
        double Lobe(double a) => 0.84 + 0.16 * Math.Pow(Math.Cos(4 * a), 2);
        m.Lathe(profile, sides, _ => PumpkinSkin, bottom: true, top: true, shape: Lobe);
        // the stem
        m.Bar(new Vector3(0, (float)(H * 0.93), 0), new Vector3((float)(R * 0.08), (float)(H * 1.13), (float)(-R * 0.05)), (float)(R * 0.16), Stem);
        // the face: on the pumpkin's skin round its front (the angle pi/2: +z), a little out from it
        Vector3 On(double a, double t)
        {
            var r = R * Math.Pow(Math.Sin(Math.PI * (0.06 + 0.88 * t)), 0.75) * Lobe(a) * 1.03;
            return new Vector3((float)(r * Math.Cos(a)), (float)(H * t), (float)(r * Math.Sin(a)));
        }
        const double F = Math.PI / 2;
        var front = new Vector3(0, (float)(H / 2), (float)(R * 2));
        foreach (var side in new[] { -1, 1 })
            m.Facing(new[] { On(F + side * 0.48, 0.56), On(F + side * 0.16, 0.56), On(F + side * 0.32, 0.74) }, Carved, front);
        m.Facing(new[] { On(F - 0.08, 0.44), On(F + 0.08, 0.44), On(F, 0.53) }, Carved, front);
        // (the grin: a band from cheek to cheek, its top edge toothed)
        var grin = new List<(double A, double T)>();
        for (var k = 0; k <= 8; k++) { var a = F - 0.62 + 1.24 * k / 8; grin.Add((a, 0.25 + 0.06 * Math.Pow((k - 4) / 4.0, 2))); }
        for (var k = 0; k < 8; k++)
        {
            var (a0, t0) = grin[k]; var (a1, t1) = grin[k + 1];
            var top0 = t0 + (k % 2 == 0 ? 0.13 : 0.08); var top1 = t1 + (k % 2 == 0 ? 0.08 : 0.13);
            m.Facing(new[] { On(a0, t0), On(a1, t1), On(a1, top1), On(a0, top0) }, Carved, front);
        }
        return m;
    }

    // A gravestone: a slab with a round top, a cross carved into its face, moss at its foot.
    private static Model Grave()
    {
        var m = new Model();
        const double W = 460, H = 900, Top = 1320;
        var outline = new List<(double X, double Y)> { (-W, 0), (W, 0), (W, H) };
        for (var k = 1; k < 8; k++) { var a = Math.PI * k / 8; outline.Add((W * Math.Cos(a), H + (Top - H) * Math.Sin(a))); }
        outline.Add((-W, H));
        m.Slab(outline.ToArray(), 220, Stone, StoneDark);
        // the carved cross: two bars a little proud of its face, darker
        m.Box(new Vector3(-50, 560, 110), new Vector3(50, 1120, 135), StoneDark);
        m.Box(new Vector3(-210, 880, 110), new Vector3(210, 970, 135), StoneDark);
        m.Box(new Vector3((float)-W - 60, 0, -170), new Vector3((float)W + 60, 70, 190), Moss);
        return m;
    }

    // A cross for a grave, leaning a little.
    private static Model Cross()
    {
        var m = new Model();
        m.Box(new Vector3(-110, 0, -110), new Vector3(110, 1700, 110), Stone);
        m.Box(new Vector3(-480, 1080, -100), new Vector3(480, 1290, 100), Stone);
        m.Box(new Vector3(-300, 0, -260), new Vector3(300, 90, 260), Moss);
        return m;
    }

    // Three candles of different heights, their flames.
    private static Model Candles()
    {
        var m = new Model();
        foreach (var (x, z, h) in new[] { (0.0, 0.0, 420.0), (170.0, 90.0, 300.0), (-140.0, 120.0, 520.0) })
        {
            var at = new Vector3((float)x, 0, (float)z);
            m.Lathe(new[] { (62.0, 0.0), (62.0, h), (50.0, h + 18) }, 6, _ => Wax, bottom: false, top: true, at: at);
            // the flame: a double point over the wick
            var a = at + new Vector3(0, (float)h + 20, 0); var tip = at + new Vector3(0, (float)h + 190, 0);
            for (var k = 0; k < 3; k++)
            {
                var u = (float)(k * 2 * Math.PI / 3); var v = (float)((k + 1) * 2 * Math.PI / 3);
                var p = at + new Vector3(32 * MathF.Cos(u), (float)h + 70, 32 * MathF.Sin(u)); var q = at + new Vector3(32 * MathF.Cos(v), (float)h + 70, 32 * MathF.Sin(v));
                m.Face(new[] { a, q, p }, Flame, at + new Vector3(0, (float)h + 70, 0));
                m.Face(new[] { p, q, tip }, Flame, at + new Vector3(0, (float)h + 70, 0));
            }
        }
        return m;
    }

    // A witch's cauldron on three legs, brimming with glowing green brew and bubbles.
    private static Model Cauldron()
    {
        var m = new Model();
        const double R = 620;
        m.Lathe(new[] { (0.0, 160.0), (R * 0.70, 190.0), (R, 420.0), (R * 0.96, 700.0), (R * 0.82, 820.0), (R * 0.88, 860.0) }, 16, _ => Iron, bottom: false, top: false);
        // (the brew, a little under the rim, and three bubbles on it)
        m.Lathe(new[] { (R * 0.80, 790.0), (0.0, 800.0) }, 16, _ => Brew, bottom: false, top: false);
        foreach (var (x, z, r) in new[] { (-150.0, 120.0, 90.0), (180.0, -60.0, 70.0), (40.0, 260.0, 55.0) })
            m.Lathe(new[] { (0.0, 795.0), (r, 795.0 + r * 0.6), (r * 0.7, 795.0 + r * 1.3), (0.0, 795.0 + r * 1.5) }, 6, _ => Brew, bottom: false, top: false,
                at: new Vector3((float)x, 0, (float)z));
        for (var k = 0; k < 3; k++)
        {
            var a = (float)(k * 2 * Math.PI / 3 + 0.5);
            const float r = (float)R;
            m.Bar(new Vector3(r * 0.55f * MathF.Cos(a), 260, r * 0.55f * MathF.Sin(a)), new Vector3(r * 0.75f * MathF.Cos(a), 0, r * 0.75f * MathF.Sin(a)), 90, Iron);
        }
        return m;
    }

    // A ghost, a sheet with nothing under it: its head round, its hem wide and wavy (its foot -- the hem's middle -- at 0), its eyes and its
    // mouth black hollows, looking +z.
    private static Model Ghost()
    {
        var m = new Model();
        const int sides = 16;
        var profile = new (double R, double Y)[] { (560, 0), (520, 260), (430, 640), (380, 980), (360, 1240), (300, 1460), (190, 1610), (0, 1680) };
        m.Lathe(profile, sides, _ => Sheet, bottom: false, top: false, yShape: (a, ring) => ring == 0 ? 130 * Math.Sin(5 * a) : 0);
        // (the hem's inside, so that it isn't see-through from below)
        for (var k = 0; k < sides; k++)
        {
            double a0 = k * 2 * Math.PI / sides, a1 = (k + 1) * 2 * Math.PI / sides;
            var p0 = new Vector3((float)(560 * Math.Cos(a0)), (float)(130 * Math.Sin(5 * a0)), (float)(560 * Math.Sin(a0)));
            var p1 = new Vector3((float)(560 * Math.Cos(a1)), (float)(130 * Math.Sin(5 * a1)), (float)(560 * Math.Sin(a1)));
            var q0 = new Vector3((float)(520 * Math.Cos(a0)), 260, (float)(520 * Math.Sin(a0)));
            var q1 = new Vector3((float)(520 * Math.Cos(a1)), 260, (float)(520 * Math.Sin(a1)));
            m.Face(new[] { p0, p1, q1, q0 }, Sheet, new Vector3(p0.X, 0, p0.Z) * 3);
        }
        // the eyes and the mouth: dark ovals on its face
        Vector3 Face(double a, double y) { var r = 365 - (y - 1240) * 0.15 + 12; return new Vector3((float)(r * Math.Cos(a)), (float)y, (float)(r * Math.Sin(a))); }
        var front = new Vector3(0, 1200, 3000);
        foreach (var side in new[] { -1, 1 })
        {
            var a = Math.PI / 2 + side * 0.32;
            m.Facing(Enumerable.Range(0, 6).Select(k => Face(a + 0.13 * Math.Cos(k * Math.PI / 3), 1300 + 85 * Math.Sin(k * Math.PI / 3))).ToArray(), Hollow, front);
        }
        m.Facing(Enumerable.Range(0, 8).Select(k => Face(Math.PI / 2 + 0.12 * Math.Cos(k * Math.PI / 4), 1040 + 110 * Math.Sin(k * Math.PI / 4))).ToArray(), Hollow, front);
        return m;
    }

    // A witch on her broomstick, flying +z: the stick under her, its bristles behind; her dark robe and cape, her green face and long nose,
    // her pointed hat; her eyes glowing.
    private static Model Witch()
    {
        var m = new Model();
        m.Bar(new Vector3(0, 0, 1200), new Vector3(0, 0, -900), 80, Wood);
        // (the bristles: a cone built round y and laid down to point back along the stick -- (x, y, z) to (x, z, -y), a turn)
        var first = m.Points.Count;
        m.Lathe(new[] { (60.0, 0.0), (180.0, 260.0), (300.0, 600.0), (0.0, 620.0) }, 8, _ => Straw, bottom: false, top: false);
        m.Move(first, q => new Vector3(q.X, q.Z, -900 - q.Y));
        // the robe: a cone from her seat up to her shoulders, a little forward
        m.Lathe(new[] { (300.0, 0.0), (250.0, 220.0), (170.0, 560.0), (110.0, 700.0) }, 10, _ => Robe, bottom: true, top: true, at: new Vector3(0, 40, -60));
        // the head, the nose, the eyes
        m.Lathe(new[] { (0.0, 720.0), (115.0, 760.0), (130.0, 850.0), (95.0, 940.0), (0.0, 960.0) }, 8, _ => Skin, bottom: false, top: false, at: new Vector3(0, 0, 10));
        m.Facing(new[] { new Vector3(-40, 820, 120), new Vector3(40, 820, 120), new Vector3(0, 790, 300) }, Skin, new Vector3(0, 900, 1000));
        m.Facing(new[] { new Vector3(-35, 790, 118), new Vector3(0, 790, 300), new Vector3(0, 860, 125) }, Skin, new Vector3(-1000, 820, 200));
        m.Facing(new[] { new Vector3(35, 790, 118), new Vector3(0, 860, 125), new Vector3(0, 790, 300) }, Skin, new Vector3(1000, 820, 200));
        foreach (var x in new[] { -55f, 55f })
            m.Facing(new[] { new Vector3(x - 25, 870, 128), new Vector3(x + 25, 870, 128), new Vector3(x, 900, 126) }, RedEye, new Vector3(x, 880, 1000));
        // the hat: a brim and a cone, its tip bent back
        m.Lathe(new[] { (330.0, 930.0), (170.0, 945.0) }, 12, _ => Hat, bottom: true, top: false, at: new Vector3(0, 0, 10));
        m.Lathe(new[] { (170.0, 945.0), (120.0, 1150.0), (60.0, 1330.0), (0.0, 1420.0) }, 10, _ => Hat, bottom: false, top: false, at: new Vector3(0, 0, -20));
        // the cape, streaming behind her
        m.Face(new[] { new Vector3(-180, 650, -80), new Vector3(180, 650, -80), new Vector3(260, 120, -760), new Vector3(-260, 120, -760) }, Robe, new Vector3(0, 900, 200), both: true);
        // her arms to the stick in front of her, her legs down behind
        foreach (var x in new[] { -1f, 1f })
        {
            m.Bar(new Vector3(x * 140, 600, 0), new Vector3(x * 60, 40, 420), 90, Robe);
            m.Bar(new Vector3(x * 120, 40, -40), new Vector3(x * 150, -330, -280), 100, Robe);
            m.Box(new Vector3(x * 150 - 70, -390, -380), new Vector3(x * 150 + 70, -300, -170), Hat);
        }
        return m;
    }

    // A bat, wings spread, flying +z (since 2026-10-18 only where the game folder has no flying rat: TempleHalloween.Creatures).
    private static Model Bat()
    {
        var m = new Model();
        m.Lathe(new[] { (0.0, -200.0), (110.0, -120.0), (130.0, 60.0), (90.0, 180.0), (0.0, 230.0) }, 8, _ => Wing, bottom: false, top: false);
        // (laid along z, its head forward: the lathe is round y -- (x, y, z) to (x, -z, y), a turn)
        m.Move(0, p => new Vector3(p.X, -p.Z, p.Y));
        foreach (var x in new[] { -1f, 1f })
        {
            // the ears
            m.Face(new[] { new Vector3(x * 40, 90, 170), new Vector3(x * 110, 90, 150), new Vector3(x * 90, 250, 170) }, Wing, new Vector3(0, 0, 0), both: true);
            // the eyes
            m.Facing(new[] { new Vector3(x * 30, 60, 215), new Vector3(x * 70, 60, 205), new Vector3(x * 50, 95, 210) }, RedEye, new Vector3(0, 60, 1000));
            // the wing: scalloped, from the body out
            var tips = new[] { (120.0, 60.0), (520.0, 260.0), (980.0, 120.0), (1180.0, -60.0), (900.0, -180.0), (650.0, -60.0), (420.0, -200.0), (150.0, -120.0) };
            var root = new Vector3(x * 60, 0, 0);
            for (var k = 0; k + 1 < tips.Length; k++)
            {
                var a = new Vector3(x * (float)tips[k].Item1, 30, (float)tips[k].Item2);
                var b = new Vector3(x * (float)tips[k + 1].Item1, 30, (float)tips[k + 1].Item2);
                m.Face(new[] { root, a, b }, Wing, new Vector3(0, -1000, 0), both: true);
            }
        }
        return m;
    }

    // (null: a skeleton's, with no LBA1 folder to take it from)
    private static Model? DecorModel(Kind kind) => kind switch
    {
        Kind.Lantern => Lantern(640, 900),
        Kind.GiantLantern => Lantern(1500, 2000),
        Kind.Grave => Grave(),
        Kind.Cross => Cross(),
        Kind.Candles => Candles(),
        Kind.SkeletonLying => SkeletonModel(SkeletonPose.Lying),
        Kind.SkeletonSlumped => SkeletonModel(SkeletonPose.Slumped),
        Kind.SkeletonHanging => SkeletonModel(SkeletonPose.Hanging),
        Kind.SkeletonChained => SkeletonModel(SkeletonPose.Chained),
        Kind.RabbiLying => RabbiModel(RabbiPose.Lying),
        Kind.RabbiSitting => RabbiModel(RabbiPose.Sitting),
        Kind.RabbiStanding => RabbiModel(RabbiPose.Standing),
        Kind.RabbiCrouching => RabbiModel(RabbiPose.Crouching),
        _ => Cauldron(),
    };

    // (how far each kind reaches from its middle, for its decor's box: the engine draws a decor whose box's corners are in sight)
    private static double Reach(Kind kind) => kind switch
    {
        Kind.GiantLantern => 1700, Kind.Cauldron => 800, Kind.Cross or Kind.Grave => 700,
        Kind.SkeletonLying => SkeletonReach(SkeletonPose.Lying), Kind.SkeletonSlumped => SkeletonReach(SkeletonPose.Slumped),
        Kind.SkeletonHanging => SkeletonReach(SkeletonPose.Hanging), Kind.SkeletonChained => SkeletonReach(SkeletonPose.Chained),
        Kind.RabbiLying => RabbiReach(RabbiPose.Lying), Kind.RabbiSitting => RabbiReach(RabbiPose.Sitting),
        Kind.RabbiStanding => RabbiReach(RabbiPose.Standing), Kind.RabbiCrouching => RabbiReach(RabbiPose.Crouching),
        _ => 700,
    };
    private static double Tall(Kind kind) => kind switch { Kind.GiantLantern => 2400, Kind.Cross => 1800, Kind.Grave => 1400, Kind.Cauldron => 1100, _ => 1100 };

    // ---- into the island: the plan's decor, drawn only, a body for each kind (RaceTrackReport.NewBodies, numbered from NewBodyBase) -- a
    // cobweb's for each of its sixth numbers (a drape's span, which of three)
    public static void PlaceDecor(IslandFile island, RaceTrackPlan plan, RaceTrackOptions o, RaceTrackReport report)
    {
        if (plan.Halloween?.Decor is not { Length: > 0 } decor) return;
        if (o.NewBodyBase < 0) { report.Notes.Add("WARNING: no place for the Halloween decor's bodies was prepared (the island's OBL wasn't counted)"); return; }
        // (the rabbibunny skeletons' body and animations: the build's game folder's, where the island's OBL is)
        creaturesFrom = o.SceneryObl is { } obl ? Path.GetDirectoryName(obl) : null;
        var bodies = new Dictionary<(Kind, double), int>();
        int placed = 0, left = 0, missing = 0;
        foreach (var d in decor.Where(d => d.Length >= 4))
        {
            var kind = (Kind)Math.Clamp((int)d[0], 0, (int)Kind.RabbiCrouching);
            var size = IsCobweb(kind) && d.Length > 5 ? d[5] : 0;
            if (!bodies.TryGetValue((kind, size), out var body))
            {
                // (a skeleton's model is LBA1's own: none without the LBA1 folder -- those left out)
                var model = IsCobweb(kind) ? CobwebModel(kind, size) : DecorModel(kind);
                body = model is null ? -1 : o.NewBodyBase + report.NewBodies.Count;
                if (model is not null) report.NewBodies.Add(model.Write());
                bodies[(kind, size)] = body;
            }
            if (body < 0) { missing++; continue; }
            double wx = (d[1] + plan.OriginCellX) * 512, wz = (d[2] + plan.OriginCellZ) * 512;
            var y = (int)Math.Round(d[3]);
            var beta = d.Length > 4 ? (int)Math.Round(d[4]) & 4095 : 0;
            var reach = IsCobweb(kind) ? CobwebReach(kind, size) : Reach(kind);
            // (a cobweb hanging from a wall's top: its box from under it)
            var under = IsCobweb(kind) ? (int)Math.Round(CobwebHeight(kind, size).Under) : 100;
            var (placedHere, full) = Palace.BrickBuilding.AddToCubes(island, body, beta, wx, y, wz, wx - reach, wx + reach, y - under, Palace.BrickBuilding.NoBoxTop, wz - reach, wz + reach,
                IslandDecors.MaxPerCubeRaceTrack);
            if (placedHere > 0) placed++; else left++;
            _ = full;
        }
        report.Notes.Add($"the Halloween decor: {placed} pieces ({string.Join(", ", decor.GroupBy(d => (Kind)(int)d[0]).Select(g => $"{g.Count()} {g.Key}"))}), {bodies.Count(b => b.Value >= 0)} bodies" +
                         (left > 0 ? $"; {left} left out (their cubes full)" : "") + (missing > 0 ? $"; {missing} skeletons left out (no LBA1 folder to take LBA1's skeleton from, or no rabbibunny skeleton in the game folder)" : ""));
        _ = Tall(Kind.Lantern);
    }

    // ---- into BODY.HQR: each spook kind's body (the race-track mode's spook=), the dives' witch (dive=), the scares' skeleton and ghost
    // (scare=), the ghost power-up's ghost and its icon (haunt=); and the pumpkin, the mushroom's entity's body Pumpkin.
    public static List<string> Install(string gameDirectory, RaceTrackReport report)
    {
        var log = new List<string>();
        var path = Path.Combine(gameDirectory, "BODY.HQR");
        if (report.Spooks.Count > 0 || report.Dives.Count > 0 || report.Scares.Count > 0 || report.Haunt)
        {
            var racerHeader = Body.Read(HqrArchive.Open(path).Read(RaceTrackBaldinoCar.RacerBody), 2).Header;
            var bodies = File.ReadAllBytes(path);
            var index = HqrArchive.CountEntries(path);
            var made = new Dictionary<string, int>();
            // (each kind's body once: -1 a skeleton with no LBA1 folder to take it from; the flying rat where the bat's was -- 2026-10-18 -- so no
            // body after it moves: the game's rat, made bigger and shaded, or the bat it took the place of when the game folder has no rat)
            var ratIsBat = false;
            int Moving(string kind)
            {
                if (kind == "bat") kind = "rat";
                if (made.TryGetValue(kind, out var body)) return body;
                if (kind == "rat" && RatBody(gameDirectory) is { } rat)
                {
                    bodies = HqrWriter.AppendEntry(bodies, HqrWriter.StoredEntry(rat));
                    return made[kind] = index++;
                }
                ratIsBat |= kind == "rat";
                var model = kind switch { "witch" => Witch(), "rat" => Bat(), "skeleton" => SkeletonModel(SkeletonPose.Reaching), _ => Ghost() };
                if (model is null) return made[kind] = -1;
                bodies = HqrWriter.AppendEntry(bodies, HqrWriter.StoredEntry(model.WriteMoving(racerHeader)));
                return made[kind] = index++;
            }
            // (the flying rat flaps its wings: its own animation; a riser is the game's rabbibunny skeleton fallen in a heap of bones by the
            // road, getting up as Twinsen's car comes and swiping at it: TempleHalloween.Creatures)
            int ratFlap = CreatureAnim(gameDirectory, FlyingRatBody, RatFly);
            int riseHeap = CreatureAnim(gameDirectory, RabbiSkeletonBody, RabbiCollapse), riseSwipe = CreatureAnim(gameDirectory, RabbiSkeletonBody, RabbiSwipe);
            // (its body made lighter at the end of the build, its entity's body RiserBody: InstallRisers)
            int rabbi = RabbiEntity(gameDirectory);
            for (var i = 0; i < report.Spooks.Count; i++)
            {
                var s = report.Spooks[i];
                if (s.Kind == "riser")
                    report.Spooks[i] = riseHeap >= 0 && riseSwipe >= 0 && rabbi >= 0 ? s with { Body = RiserBody, Entity = rabbi, Anim = riseHeap, Action = riseSwipe } : s with { Body = -1 };
                else
                {
                    var body = Moving(s.Kind);
                    report.Spooks[i] = s with { Body = body, Anim = s.Kind is "rat" or "bat" && body >= 0 && ratFlap >= 0 && !ratIsBat ? ratFlap : -1 };
                }
            }
            for (var i = 0; i < report.Dives.Count; i++) report.Dives[i] = report.Dives[i] with { Body = Moving("witch") };
            for (var i = 0; i < report.Scares.Count; i++) report.Scares[i] = report.Scares[i] with { Body = Moving(report.Scares[i].Kind) };
            if (report.Haunt)
            {
                // (the ghost circling over a haunted car is the spooks' ghost; the item box's icon the same ghost, a fixed body)
                var ghost = Moving("ghost");
                bodies = HqrWriter.AppendEntry(bodies, HqrWriter.StoredEntry(Ghost().Write()));
                report.HauntBodies = (ghost, index++);
            }
            File.WriteAllBytes(path, bodies);
            if (report.Spooks.Count > 0)
                log.Add($"the spooks: {report.Spooks.Count} ({string.Join(", ", report.Spooks.GroupBy(s => s.Kind).Select(g => $"{g.Count()} {g.Key}{(g.Count() > 1 ? (g.Key == "witch" ? "es" : "s") : "")}"))}), " +
                        $"their bodies BODY.HQR {string.Join(", ", made.Where(m => m.Key is "witch" or "ghost" or "rat").Select(m => $"{m.Value} ({m.Key}{(m.Key == "rat" && !ratIsBat ? $", flapping ANIM.HQR {ratFlap}" : "")})"))}" +
                        (report.Spooks.Any(s => s.Kind == "riser") ? $"; the risers entity {rabbi}'s body {RiserBody} (the game's rabbibunny skeleton, made lighter at the end of the build), fallen in a heap ANIM.HQR {riseHeap}, its swipe {riseSwipe}" : ""));
            if (report.Dives.Count > 0 || report.Scares.Count > 0)
                log.Add($"the scares: {report.Dives.Count} places a witch dives at the car from, {report.Scares.Count(s => s.Body >= 0)} things jumping up beside the road " +
                        $"({string.Join(", ", report.Scares.GroupBy(s => s.Kind).Select(g => $"{g.Count()} {g.Key}{(g.Count() > 1 ? "s" : "")}"))})" +
                        (made.TryGetValue("skeleton", out var sk) ? sk >= 0 ? $", the skeleton BODY.HQR {sk}" : ", the skeletons left out (no LBA1 folder)" : ""));
            if (report.HauntBodies is { } hb) log.Add($"the ghost power-up: its ghost BODY.HQR {hb.Ghost}, its icon {hb.Icon}");
        }
        if (report.Pumpkins)
        {
            // (30 % bigger than the first ones, 240 by 320, on the widened road: the user, 2026-10-20, "enlarge the item boxes slightly")
            var model = Lantern(312, 416, sides: 12, rings: 6);
            var mushroom = Body.Read(HqrArchive.Open(path).Read(RaceTrackOil.MushroomBody), 2, allowStatic: true);
            var pumpkin = Body.Read(model.Write(), 2, allowStatic: true);
            pumpkin.Header = mushroom.Header; pumpkin.Static = true;
            var index = HqrArchive.CountEntries(path);
            File.WriteAllBytes(path, HqrWriter.AppendEntry(File.ReadAllBytes(path), HqrWriter.StoredEntry(pumpkin.Write())));
            var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
            var table = RaceTrackBaldinoCar.WithBody(HqrArchive.Open(ressPath).Read(44), RaceTrackOil.Entity, Pumpkin, index);
            File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
            log.Add($"the power-ups' mini pumpkins: BODY.HQR entry {index}, the mushroom's entity ({RaceTrackOil.Entity}) body {Pumpkin}");
        }
        return log;
    }

    // A spook as the car file has it (RACEMOD.CPP spook=): its body, its flags, its height, (flags 4) its animation of ANIM.HQR, (flags 8:
    // standing at its way's first point, facing its second) the one it plays as Twinsen's car comes, (flags 16: <body> is the entity's
    // body number, not BODY.HQR's) the entity, its way's points (island units).
    public static int[] Line(RaceTrackReport.Spook s)
    {
        var standing = (s.Flags & 8) != 0 && s.Anim >= 0 && s.Action >= 0;
        var flags = (s.Anim >= 0 ? s.Flags | 4 : s.Flags & ~4) & ~16;
        if (!standing) flags &= ~8;
        var more = new List<int>();
        if (s.Anim >= 0) more.Add(s.Anim);
        if (standing) more.Add(s.Action);
        if (s.Entity >= 0) { flags |= 16; more.Add(s.Entity); }
        return new[] { s.Body, flags, (int)Math.Round(s.Y) }.Concat(more).Concat(s.Way.SelectMany(p => new[] { (int)Math.Round(p.X * 512), (int)Math.Round(p.Z * 512) })).ToArray();
    }

    // A dive's place and a scare as the car file has them (dive=, scare=): the body, the place (island units), the road's or the floor's
    // height, (a scare's) the way it faces.
    public static int[] Line(RaceTrackReport.Dive d) => new[] { d.Body, (int)Math.Round(d.X * 512), (int)Math.Round(d.Z * 512), (int)Math.Round(d.Y) };
    public static int[] Line(RaceTrackReport.Scare s) => new[] { s.Body, (int)Math.Round(s.X * 512), (int)Math.Round(s.Z * 512), (int)Math.Round(s.Y), s.Turn & 4095 };
}
