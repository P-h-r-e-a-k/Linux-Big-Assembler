using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// A race car's body being built (RaceTrackBaldinoCar, RaceTrackCharacterCars): points in the car's own coordinates per bone (handles), faces
// turned so they face out (the engine draws a polygon only from the side its points go round anticlockwise, as the retail bodies do), lines
// and spheres; ToBody makes the bones' points relative to their pivots.
//
// The bones are the retail racer's car's (BODY.HQR 227), so the racer's animations drive any car built here: 0 and 1 the root and the bounce,
// 2 the hull, 3-5 and 6-8 the front struts, hubs and wheels, 9-10 and 11-12 the rear axles and wheels, 13 the driver, 14-17 the racer's
// driver's arms (turned a long way by the animations to reach its wheel: a car here keeps its driver whole in 13 and leaves them a point each).
internal sealed class CarMesh(string name)
{
    public const int Bones = 18;
    // see-through (the engine's polygon type 2): what is behind it, moved into its colour's ramp
    public const int SeeThrough = 2;
    private static readonly int[] Parents = { -1, 0, 1, 2, 3, 4, 2, 6, 7, 2, 9, 2, 11, 2, 13, 14, 13, 16 };
    private readonly List<Vector3>[] points = Enumerable.Range(0, Bones).Select(_ => new List<Vector3>()).ToArray();
    private readonly List<float>[] lights = Enumerable.Range(0, Bones).Select(_ => new List<float>()).ToArray();
    private readonly List<(int Bone, int Index)> handles = new();
    private readonly int[] pivots = Enumerable.Repeat(-1, Bones).ToArray();
    // Material: -1 lit (Gouraud), 0 unlit, SeeThrough
    private readonly List<(int[] Points, int Colour, int Material, FaceTexture? Texture)> faces = new();
    private readonly List<(int A, int B, int Colour)> lines = new();
    private readonly List<(int Point, int Radius, int Colour)> spheres = new();

    // How much of the game's light the points made from here on take (Body.LightScale): 1 runs a colour up its ramp by the light's nine steps
    // or so; less keeps a polygon to the few colours above its own (a pink from the end of the reds, a black from the start of the greys).
    public float Light { get; set; } = 1;

    public int P(int bone, Vector3 v) { points[bone].Add(v); lights[bone].Add(Light); handles.Add((bone, points[bone].Count - 1)); return handles.Count - 1; }
    public Vector3 At(int handle) { var (b, i) = handles[handle]; return points[b][i]; }
    public void Pivot(int bone, int handle) => pivots[bone] = handle;
    public void Line(int a, int b, int colour) => lines.Add((a, b, colour));
    public void Sphere(int point, int radius, int colour) => spheres.Add((point, radius, colour));
    public void Raw(int[] hs, int colour, bool unlit) => faces.Add((hs, colour, unlit ? 0 : -1, null));
    public void Raw(int[] hs, int colour, int material, FaceTexture? texture = null) => faces.Add((hs, colour, material, texture));

    // The texture table the textured polygons' handles count in (a driver's, from its own body: the pictures are the game's one page).
    public uint[] Textures { get; set; } = [];

    private Vector3 Normal(int[] hs)
    {
        var n = Vector3.Zero;
        for (var i = 0; i < hs.Length; i++)
        {
            var a = At(hs[i]); var b = At(hs[(i + 1) % hs.Length]);
            n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
        }
        return n;
    }
    private Vector3 Centre(int[] hs) => hs.Aggregate(Vector3.Zero, (s, h) => s + At(h)) / hs.Length;
    // facing away from `inside`
    public void Out(int[] hs, int colour, Vector3 inside, bool unlit = false)
    {
        if (Vector3.Dot(Normal(hs), Centre(hs) - inside) < 0) hs = hs.Reverse().ToArray();
        faces.Add((hs, colour, unlit ? 0 : -1, null));
    }
    // facing `towards`
    public void In(int[] hs, int colour, Vector3 towards, bool unlit = false)
    {
        if (Vector3.Dot(Normal(hs), towards - Centre(hs)) < 0) hs = hs.Reverse().ToArray();
        faces.Add((hs, colour, unlit ? 0 : -1, null));
    }
    public void Up(int[] hs, int colour, bool unlit = false) => In(hs, colour, Centre(hs) + Vector3.UnitY, unlit);
    // a thin plate seen from both sides
    public void Both(int[] hs, int colour, int material = -1) { faces.Add((hs, colour, material, null)); faces.Add((hs.Reverse().ToArray(), colour, material, null)); }

    // shorthands: a plate through new points (seen from both sides; Flat: unlit, Pane: see-through), a ball, a line, a box (its top
    // another colour if given)
    public int[] Plate(int bone, int colour, params Vector3[] at) { var hs = at.Select(v => P(bone, v)).ToArray(); Both(hs, colour); return hs; }
    public int[] Flat(int bone, int colour, params Vector3[] at) { var hs = at.Select(v => P(bone, v)).ToArray(); Both(hs, colour, 0); return hs; }
    public int[] Pane(int bone, int colour, params Vector3[] at) { var hs = at.Select(v => P(bone, v)).ToArray(); Both(hs, colour, SeeThrough); return hs; }
    public int Ball(int bone, Vector3 at, int radius, int colour) { var h = P(bone, at); Sphere(h, radius, colour); return h; }
    public void Rod(int bone, Vector3 a, Vector3 b, int colour) => Line(P(bone, a), P(bone, b), colour);
    public int[] Box(int bone, Vector3 centre, Vector3 size, int colour, int top = -1)
    {
        var h = size / 2;
        var hs = new[] { new Vector3(-1, -1, -1), new(1, -1, -1), new(1, -1, 1), new(-1, -1, 1), new(-1, 1, -1), new(1, 1, -1), new(1, 1, 1), new(-1, 1, 1) }
            .Select(c => P(bone, centre + c * h)).ToArray();
        foreach (var (a, b, c, d, isTop) in new[] { (0, 1, 2, 3, false), (4, 5, 6, 7, true), (0, 1, 5, 4, false), (1, 2, 6, 5, false), (2, 3, 7, 6, false), (3, 0, 4, 7, false) })
            Out(new[] { hs[a], hs[b], hs[c], hs[d] }, isTop && top >= 0 ? top : colour, centre);
        return hs;
    }

    // points round an ellipse: across x and y at a z (alongZ), or across y and z at an x
    public int[] Ring(int bone, Vector3 centre, float rx, float ry, int n, bool alongZ)
        => Enumerable.Range(0, n).Select(k =>
        {
            var a = k * MathF.Tau / n;
            return P(bone, alongZ ? centre + new Vector3(rx * MathF.Sin(a), ry * MathF.Cos(a), 0) : centre + new Vector3(0, ry * MathF.Cos(a), rx * MathF.Sin(a)));
        }).ToArray();

    // points round a squarer ring across x and y at a z: a superellipse (power 2 an ellipse, more a box with round corners), its top half
    // and its bottom half with heights of their own (a flat floor under a high, round back)
    // (`phase` turns the ring by that many steps: half a step puts a panel, not a point, on top and on each side)
    public int[] Ring(int bone, Vector3 centre, float rx, float top, float bottom, int n, float power, float phase = 0)
        => Enumerable.Range(0, n).Select(k =>
        {
            var a = (k + phase) * MathF.Tau / n;
            float s = MathF.Sin(a), c = MathF.Cos(a);
            float x = MathF.Sign(s) * MathF.Pow(MathF.Abs(s), 2 / power), y = MathF.Sign(c) * MathF.Pow(MathF.Abs(c), 2 / power);
            return P(bone, centre + new Vector3(rx * x, (y >= 0 ? top : bottom) * y, 0));
        }).ToArray();

    // points round any ellipse: centre + u cos + v sin
    public int[] Loop(int bone, Vector3 centre, Vector3 u, Vector3 v, int n, float phase = 0)
        => Enumerable.Range(0, n).Select(k => { var a = (k + phase) * MathF.Tau / n; return P(bone, centre + u * MathF.Cos(a) + v * MathF.Sin(a)); }).ToArray();

    // the skin between two rings of as many points, facing away from `inside`, each panel in the colour `colour` gives it
    public void Skin(int[] a, int[] b, Func<int, int> colour, Vector3 inside)
    {
        for (var k = 0; k < a.Length; k++) Out(new[] { a[k], a[(k + 1) % a.Length], b[(k + 1) % a.Length], b[k] }, colour(k), inside);
    }

    // a ring closed to one point (a nose, a tail, a cap)
    public void Cap(int[] ring, int tip, int colour, Vector3 inside, bool unlit = false)
    {
        for (var k = 0; k < ring.Length; k++) Out(new[] { ring[k], ring[(k + 1) % ring.Length], tip }, colour, inside, unlit);
    }

    // a square bar from a to b (on `bone`), thickness t; returns a point at b's end (for the next bone's pivot)
    public int Strut(int bone, Vector3 a, Vector3 b, float t, int colour)
    {
        var d = Vector3.Normalize(b - a);
        var u = Vector3.Normalize(Vector3.Cross(d, Math.Abs(d.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY)) * t / 2; var v = Vector3.Normalize(Vector3.Cross(d, u)) * t / 2;
        var corners = new[] { u + v, u - v, -u - v, -u + v };
        var ea = corners.Select(c => P(bone, a + c)).ToArray(); var eb = corners.Select(c => P(bone, b + c)).ToArray();
        for (var k = 0; k < 4; k++) Out(new[] { ea[k], ea[(k + 1) % 4], eb[(k + 1) % 4], eb[k] }, colour, (a + b) / 2);
        return eb[0];
    }

    public Body ToBody(byte[] header)
    {
        // bones 4 and 7 have no points of their own but the hub; every bone needs at least its pivot's worth of order
        var offsets = new int[Bones];
        for (int b = 0, next = 0; b < Bones; b++) { offsets[b] = next; next += points[b].Count; }
        int Global(int h) => offsets[handles[h].Bone] + handles[h].Index;
        var body = new Body { Game = 2, Lit = true, Header = (byte[])header.Clone() };
        var world = new List<Vector3>();
        for (var b = 0; b < Bones; b++)
        {
            if (points[b].Count == 0) throw new InvalidOperationException($"{name}: bone {b} has no points.");
            if (b > 0 && pivots[b] < 0) throw new InvalidOperationException($"{name}: bone {b} has no pivot.");
            var pivot = b == 0 ? 0 : Global(pivots[b]);
            if (b > 0 && handles[pivots[b]].Bone != Parents[b]) throw new InvalidOperationException($"{name}: bone {b}'s pivot isn't a point of bone {Parents[b]}.");
            body.Bones.Add(new Bone(offsets[b], points[b].Count, pivot, Parents[b], new byte[8]));
            world.AddRange(points[b]);
        }
        body.Vertices.AddRange(world);
        body.SetWorld(world.ToArray());
        if (lights.Any(l => l.Any(v => v != 1))) body.LightScale = lights.SelectMany(l => l).ToArray();
        body.Textures = Textures;
        foreach (var (hs, colour, material, texture) in faces)
        {
            var ids = hs.Select(Global).ToArray();
            if (ids.Length == 4 || ids.Length == 3) body.Faces.Add(new Face(ids, colour, Material: material, Texture: texture));
            else throw new InvalidOperationException($"{name}: a polygon that isn't a triangle or a quad.");
        }
        foreach (var (a, b, colour) in lines) body.Lines.Add(new BodyLine(Global(a), Global(b), colour));
        foreach (var (p, r, colour) in spheres) body.Spheres.Add(new BodySphere(Global(p), r, colour));
        body.Validate();
        return body;
    }
}

// A car's driver, taken from the character's own body in BODY.HQR: the bones that show above the car's rim (the legs and hips are in the car),
// posed -- each arm turned to hold the wheel, whatever else the car asks for -- then made a little smaller, sat in the cockpit and added to
// the car's bone 13.
internal sealed class CarDriver
{
    private readonly Body body;
    private readonly Vector3[] world, posed;

    public CarDriver(Body body) { this.body = body; world = body.World(); posed = (Vector3[])world.Clone(); }

    public int BoneOf(int point) => body.Bones.FindIndex(b => point >= b.Start && point < b.Start + b.Count);
    public int Bones => body.Bones.Count;
    // the bones hanging from one, nearest first
    public int[] Below(int bone)
    {
        var list = new List<int>();
        for (var b = bone + 1; b < body.Bones.Count; b++) if (body.Bones[b].Parent == bone || list.Contains(body.Bones[b].Parent)) list.Add(b);
        return list.ToArray();
    }
    // every bone but these and all that hangs from them
    public HashSet<int> AllBut(IEnumerable<int>? drop)
    {
        var gone = new HashSet<int>(drop ?? Array.Empty<int>());
        for (var b = 0; b < body.Bones.Count; b++) if (gone.Contains(body.Bones[b].Parent)) gone.Add(b);
        return Enumerable.Range(0, body.Bones.Count).Where(b => !gone.Contains(b)).ToHashSet();
    }
    // where a bone turns, as the body stands (before any posing)
    public Vector3 PivotOf(int bone) => world[body.Bones[bone].Pivot];
    public void Turn(IEnumerable<int> bones, Vector3 pivot, Quaternion q)
    {
        foreach (var b in bones)
            for (var i = body.Bones[b].Start; i < body.Bones[b].Start + body.Bones[b].Count; i++) posed[i] = Vector3.Transform(posed[i] - pivot, q) + pivot;
    }
    // a bone's point furthest from a place, as posed (a forearm's from its elbow: the hand)
    public Vector3 Far(int bone, Vector3 from) => Far(new[] { bone }, from);
    public Vector3 Far(IEnumerable<int> bones, Vector3 from)
        => bones.SelectMany(b => Enumerable.Range(body.Bones[b].Start, body.Bones[b].Count)).Select(i => posed[i]).OrderByDescending(p => Vector3.DistanceSquared(p, from)).First();

    // The body's arms, found from its bones: on each side the bone that turns at shoulder height, well out from the middle, with the most
    // hanging from it -- and what hangs from it (the forearm first, then the hand and whatever it holds). Null for a side without one.
    public ((int Upper, int[] Fore)? Right, (int Upper, int[] Fore)? Left) FindArms(ISet<int>? not = null)
    {
        float height = world.Max(v => v.Y) - world.Min(v => v.Y), floor = world.Min(v => v.Y);
        var children = Enumerable.Range(0, body.Bones.Count).ToDictionary(b => b, b => Enumerable.Range(0, body.Bones.Count).Where(c => body.Bones[c].Parent == b).ToList());
        List<int> Below(int b) { var list = new List<int>(); foreach (var c in children[b]) { if (not is not null && not.Contains(c)) continue; list.Add(c); list.AddRange(Below(c)); } return list; }
        IEnumerable<Vector3> Points(int b) => Enumerable.Range(body.Bones[b].Start, body.Bones[b].Count).Select(i => world[i]);
        (int Upper, int[] Fore)? Side(float side)
        {
            (int Upper, int[] Fore, float Drop)? best = null;
            for (var b = 1; b < body.Bones.Count; b++)
            {
                if (not is not null && not.Contains(b)) continue;
                var pivot = PivotOf(b);
                if (pivot.X * side < 40 || pivot.Y - floor < height * 0.5f) continue;
                var below = Below(b).ToArray();
                var points = Points(b).Concat(below.SelectMany(Points)).ToList();
                if (points.Count == 0 || points.Count > world.Length * 0.4f) continue;
                var drop = pivot.Y - points.Min(v => v.Y);
                if (drop < height * 0.12f) continue;
                // (the shoulder, not the elbow: a bone whose parent is already a candidate on this side hangs from the arm)
                if (best is { } found && (found.Upper == body.Bones[b].Parent || found.Fore.Contains(body.Bones[b].Parent))) continue;
                if (best is null || drop > best.Value.Drop) best = (b, below, drop);
            }
            return best is { } arm ? (arm.Upper, arm.Fore) : null;
        }
        return (Side(1), Side(-1));
    }

    // An arm turned so its hand is at `target` (in the body's own coordinates): the elbow bent out to the side and a little down, the upper
    // arm and forearm keeping their lengths (two-bone reach: the elbow on the circle both lengths allow, towards the side). `fore` is the
    // forearm and what hangs on it (a hand of its own bone). `handOf`: the bones the hand is looked for in, when what hangs on the forearm
    // includes something held that reaches further than the hand (a fishing rod). Returns the shoulder and where the hand is now.
    public (Vector3 Shoulder, Vector3 Hand) Reach(int upper, int[] fore, Vector3 target, float side, int[]? handOf = null)
    {
        handOf ??= fore;
        var shoulder = PivotOf(upper); var elbow = PivotOf(fore[0]); var hand = Far(handOf, elbow);
        float l1 = Vector3.Distance(shoulder, elbow), l2 = Vector3.Distance(elbow, hand);
        var reach = target - shoulder; var d = Math.Clamp(reach.Length(), Math.Abs(l1 - l2) + 1, l1 + l2 - 1); var u = Vector3.Normalize(reach);
        var along = (l1 * l1 - l2 * l2 + d * d) / (2 * d); var height = MathF.Sqrt(MathF.Max(0, l1 * l1 - along * along));
        var hint = new Vector3(side, -0.45f, 0); hint -= u * Vector3.Dot(hint, u); hint = Vector3.Normalize(hint);
        var newElbow = shoulder + u * along + hint * height;
        Turn(fore.Prepend(upper), shoulder, Between(elbow - shoulder, newElbow - shoulder));
        Turn(fore, newElbow, Between(Far(handOf, newElbow) - newElbow, shoulder + u * d - newElbow));
        return (shoulder, Far(handOf, newElbow));
    }

    // What a polygon of the driver becomes in the car: its colour, how much light its points take (CarMesh.Light; Own: what each takes in the
    // character's own body) and its material (-1 lit, 0 unlit, CarMesh.SeeThrough); null leaves it out. A textured polygon keeps its texture
    // whatever the colour.
    public const float Own = -1;
    public delegate (int Colour, float Light, int Material)? Painter(Face face, int[] bones);

    // The posed driver's kept bones added to the car's bone 13, through `place` (the body's coordinates to the car's); polygons wholly below
    // `rim` (inside the car) are left out, and with `cut` lines and spheres below it too. Returns the car's point for any of the body's
    // points (made when the car has none yet).
    public Func<int, int> Seat(CarMesh m, HashSet<int> keep, Func<Vector3, Vector3> place, float rim, float scale, Painter? paint = null, bool cut = false)
    {
        bool Inside(int p) => cut && place(posed[p]).Y < rim - 20;
        if (body.Faces.Any(f => f.Texture is not null)) m.Textures = body.Textures;
        var map = new Dictionary<(int Point, float Light), int>();
        int Point(int i, float light)
        {
            if (light < 0) light = body.LightScale is { } own ? own[i] : 1;
            if (map.TryGetValue((i, light), out var h)) return h;
            var before = m.Light; m.Light = light;
            map[(i, light)] = h = m.P(13, place(posed[i]));
            m.Light = before;
            return h;
        }
        foreach (var f in body.Faces)
        {
            var bones = f.Points.Select(BoneOf).ToArray();
            if (!bones.All(keep.Contains)) continue;
            if (f.Points.All(p => place(posed[p]).Y < rim - 20)) continue;
            var (colour, light, material) = paint is null ? (f.Colour, Own, f.Material == 0 ? 0 : -1) : paint(f, bones) ?? (-1, 0f, 0);
            if (colour < 0) continue;
            m.Raw(f.Points.Select(p => Point(p, light)).ToArray(), colour, material, f.Texture);
        }
        foreach (var l in body.Lines) if (keep.Contains(BoneOf(l.A)) && keep.Contains(BoneOf(l.B)) && !(Inside(l.A) && Inside(l.B))) m.Line(Point(l.A, Own), Point(l.B, Own), l.Colour);
        foreach (var s in body.Spheres) if (keep.Contains(BoneOf(s.Point)) && !Inside(s.Point)) m.Sphere(Point(s.Point, Own), (int)(s.Radius * scale), s.Colour);
        return i => Point(i, Own);
    }

    // The rotation that turns direction a into direction b.
    public static Quaternion Between(Vector3 a, Vector3 b)
    {
        a = Vector3.Normalize(a); b = Vector3.Normalize(b);
        var axis = Vector3.Cross(a, b); var s = axis.Length(); var c = Vector3.Dot(a, b);
        if (s < 1e-6f) return c > 0 ? Quaternion.Identity : Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        return Quaternion.CreateFromAxisAngle(axis / s, MathF.Atan2(s, c));
    }
}
