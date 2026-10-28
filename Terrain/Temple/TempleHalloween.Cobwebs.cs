using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain.Temple;

// The Temple of Bú's cobwebs (the user, 2026-10-17: "Can we also improve the temple track add in some cobwebs, perhaps a giant cobweb we
// have to drive through"):
//   - cobwebs about the lap, decor drawn only (the plan's decor kinds 10-15): hanging from the tops of the walls across their corners beside
//     the road (CobwebCorner, CobwebCornerBig), across the alcoves and the doorways off it (CobwebSheet), between two columns (CobwebDrape),
//     flat on the walls beside it as LBA1's own are (CobwebWall), and spiders hanging on their threads (Spider) -- a drape's span (cells),
//     and which of three of a corner's or a wall's, the decor's sixth number. Each is an orb web as LBA1's own cobwebs are drawn -- its
//     radials fanned out from its hub to its frame, a spiral round the hub between them -- each strand a thin flat ribbon of pale silk,
//     nothing to bump into;
//   - the giant web across the long corridor's east doorway, which the cars drive through (the plan's giantWebs; RACEMOD.CPP web=,
//     web_part=): drawn by the race-track mode, not the island -- its frame and what a car leaves of it (always), its middle (while it is
//     whole), the tatters left hanging round the hole a car tears in it, the scraps the car flings along with it, and its spider, which
//     scuttles up out of the way of a car coming. A still copy of it whole, its spider at its hub, stands among the island's decor for the
//     editor's view, hidden while racing (GiantWebVar).
// The silk is the palette's pale greys (58-61) as they are -- LBA1's cobwebs' greys, a little paler to stand out on the temple's dark
// walls; the spiders are a dark purple, their legs banded in rust, their eyes the palette's glowing red, a pale skull on their backs.
internal static partial class TempleHalloween
{
    private static readonly Paint SilkRadial = new(new[] { 60 }, Glow: true);
    private static readonly Paint SilkSpiral = new(new[] { 59 }, Glow: true);
    private static readonly Paint SilkFrame = new(new[] { 58 }, Glow: true);
    private static readonly Paint SilkBright = new(new[] { 61 }, Glow: true);
    private static readonly Paint SpiderSkin = new(new[] { 208, 209, 210, 211, 212 });
    private static readonly Paint SpiderBand = new(new[] { 84, 85, 86, 87 });
    private static readonly Paint SpiderSkull = new(new[] { 233, 235, 236, 237, 238 });
    private static readonly Paint SpiderFang = new(new[] { 72, 73, 74 });

    // A thread of a web in its own plane (x across, y up): its points, its width, its paint.
    private sealed record WebThread(Vector2[] Points, float Width, Paint Paint);

    // How far out along `dir` from `from` the frame -- a closed outline, either way round -- is (float.MaxValue when it misses).
    private static float OutTo(Vector2 from, Vector2 dir, Vector2[] frame)
    {
        var best = float.MaxValue;
        for (var i = 0; i < frame.Length; i++)
        {
            Vector2 p = frame[i], q = frame[(i + 1) % frame.Length], e = q - p;
            var den = dir.X * e.Y - dir.Y * e.X;
            if (MathF.Abs(den) < 1e-6f) continue;
            var w = p - from;
            var t = (w.X * e.Y - w.Y * e.X) / den;
            var u = (w.X * dir.Y - w.Y * dir.X) / den;
            if (t > 1 && u >= -1e-4f && u <= 1 + 1e-4f) best = MathF.Min(best, t);
        }
        return best;
    }

    // An orb web: `radials` threads from its hub out to its frame (each turned a little, `random`), a spiral round the hub from `inner` out,
    // `spacing` apart (a little wider each turn) -- each turn a thread from a radial to the next where both reach past it, no closer to the
    // frame than half a turn (`sag`: sagging between them towards the hub, a big web's) -- a tight mat at the hub, and the frame's own
    // threads (`frameEdges`: its outline's edges that are threads, not walls). Widths: the radials', the spiral's, the frame's.
    private static List<WebThread> OrbWeb(Vector2 hub, Vector2[] frame, bool[] frameEdges, int radials, float inner, float spacing, Random random,
        float radialWidth, float spiralWidth, float frameWidth, float jitter = 0.07f, bool sag = false)
    {
        var threads = new List<WebThread>();
        var turn0 = (float)(random.NextDouble() * Math.Tau / radials);
        var dirs = Enumerable.Range(0, radials).Select(k => turn0 + (k + (float)(random.NextDouble() - 0.5) * 2 * jitter) * MathF.Tau / radials)
            .Select(a => new Vector2(MathF.Cos(a), MathF.Sin(a))).ToArray();
        var lengths = dirs.Select(d => OutTo(hub, d, frame)).ToArray();
        for (var k = 0; k < radials; k++)
            if (lengths[k] < float.MaxValue) threads.Add(new WebThread(new[] { hub, hub + dirs[k] * lengths[k] }, radialWidth, SilkRadial));
        var longest = lengths.Where(l => l < float.MaxValue).DefaultIfEmpty(0).Max();
        var rings = new List<float>();
        for (float r = inner, step = spacing; r < longest; r += step, step *= 1.04f) rings.Add(r);
        foreach (var ring in rings)
        {
            // (each crossing a little off the ring; a turn of the spiral runs on round while its radials reach past it)
            var at = Enumerable.Range(0, radials).Select(k => ring + (float)(random.NextDouble() - 0.5) * spacing * 0.25f).ToArray();
            bool Reaches(int k) => lengths[k] < float.MaxValue && at[k] <= lengths[k] - spacing * 0.5f;
            var start = Enumerable.Range(0, radials).FirstOrDefault(k => !Reaches(k), -1);
            var run = new List<Vector2>();
            void Flush() { if (run.Count >= 2) threads.Add(new WebThread(run.ToArray(), spiralWidth, SilkSpiral)); run = new List<Vector2>(); }
            // (round from a radial it doesn't reach -- else all the way round, back to the first)
            var first = start < 0 ? 0 : start;
            for (var step = 0; step <= radials; step++)
            {
                var k = (first + step) % radials;
                if (!Reaches(k)) { Flush(); continue; }
                var p = hub + dirs[k] * at[k];
                if (run.Count > 0 && sag) run.Add(hub + ((run[^1] + p) / 2 - hub) * 0.94f);
                run.Add(p);
                if (start < 0 && step == radials) Flush();
            }
            Flush();
        }
        // the hub's mat: two tight rings
        foreach (var r in new[] { inner * 0.35f, inner * 0.7f })
            threads.Add(new WebThread(Enumerable.Range(0, radials + 1).Select(k => hub + dirs[k % radials] * r).ToArray(), spiralWidth, SilkSpiral));
        // the frame's threads, each run of edges that are threads one
        var edge = new List<Vector2>();
        for (var i = 0; i <= frame.Length; i++)
        {
            var e = i % frame.Length;
            if (i < frame.Length && frameEdges[e]) { if (edge.Count == 0) edge.Add(frame[e]); edge.Add(frame[(e + 1) % frame.Length]); continue; }
            if (edge.Count >= 2) threads.Add(new WebThread(edge.ToArray(), frameWidth, SilkFrame));
            edge = new List<Vector2>();
        }
        return threads;
    }

    // A sagging thread from a to b, its middle `sag` lower: its points (n pieces).
    private static Vector2[] Sag(Vector2 a, Vector2 b, float sag, int n) =>
        Enumerable.Range(0, n + 1).Select(i => { var t = i / (float)n; return Vector2.Lerp(a, b, t) - new Vector2(0, sag * 4 * t * (1 - t)); }).ToArray();

    // A thread hanging from `top` down `length`, a little crooked, with bits of spiral left on it.
    private static IEnumerable<WebThread> Dangle(Vector2 top, float length, float width, Random random)
    {
        var pieces = Math.Max(2, (int)(length / 350));
        var points = new List<Vector2> { top };
        for (var i = 1; i <= pieces; i++) points.Add(top + new Vector2((float)(random.NextDouble() - 0.5) * 120, -length * i / pieces));
        yield return new WebThread(points.ToArray(), width, SilkRadial);
        for (var i = 1; i < pieces; i++)
            if (random.NextDouble() < 0.55)
            {
                var bit = new Vector2((random.NextDouble() < 0.5 ? -1 : 1) * (float)(120 + random.NextDouble() * 220), (float)(random.NextDouble() - 0.5) * 120);
                yield return new WebThread(new[] { points[i], points[i] + bit }, width * 0.7f, SilkSpiral);
            }
    }

    // The threads into a model: each point (x, y) of the web's plane at origin + across x + up y, the ribbons facing `normal`.
    private static void Weave(Model m, IEnumerable<WebThread> threads, Vector3 origin, Vector3 across, Vector3 normal, bool both, float piece = 0)
    {
        foreach (var t in threads)
            m.Strand(t.Points.Select(p => origin + across * p.X + Vector3.UnitY * p.Y).ToArray(), t.Width, t.Paint, normal, both, piece);
    }

    // ---- the cobwebs about the lap (decor)
    // A web hanging from a thread `w` either side of its middle (y 0): down `side` at its ends and `deep` in its middle, its hub just under
    // its top thread; a few threads hanging from its bottom, an old web.
    private static List<WebThread> HangingWeb(float w, float side, float deep, int seed)
    {
        var random = new Random(seed);
        var bottom = Sag(new Vector2(w, -side), new Vector2(-w, -side), deep - side, 6);
        var frame = new[] { new Vector2(-w, 0), new Vector2(w, 0) }.Concat(bottom).ToArray();
        var edges = Enumerable.Range(0, frame.Length).Select(i => i != 1 && i != frame.Length - 1).ToArray();
        var threads = OrbWeb(new Vector2(w * 0.08f, -deep * 0.17f), frame, edges, 11, deep * 0.11f, deep * 0.08f, random, 26, 18, 30);
        foreach (var x in new[] { -0.45f, 0.15f, 0.55f })
            threads.AddRange(Dangle(bottom.MinBy(p => MathF.Abs(p.X - x * w)), deep * (0.25f + (float)random.NextDouble() * 0.3f), 18, random));
        return threads;
    }

    // A cobweb hanging from the tops of two walls across their corner: the corner at the model's origin, the walls' faces x = 0 (along +z)
    // and z = 0 (along +x), the room between them; the web across the corner from `reach` along one wall to as far along the other, from
    // the walls' tops (y 0) down.
    private static Model CornerWeb(float reach, int seed)
    {
        var m = new Model();
        Weave(m, HangingWeb(reach / MathF.Sqrt(2), reach * 0.75f, reach * 1.05f, seed), new Vector3(reach / 2, 0, reach / 2), Vector3.Normalize(new Vector3(1, 0, -1)),
            Vector3.Normalize(new Vector3(1, 0, 1)), both: true);
        return m;
    }

    // A cobweb on a wall, as LBA1 draws its own: flat in front of its face (z 0, the wall behind it, -z), hanging from its top (y 0), `half`
    // either side of its middle -- seen from the front only.
    private static Model WallWeb(float half, int seed)
    {
        var m = new Model();
        Weave(m, HangingWeb(half, half * 0.6f, half * 0.95f, seed), Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, both: false);
        return m;
    }

    // A cobweb across an alcove or a doorway: a web `wide` across and `tall` high in the plane z = 0, from y 0 (the floor) up, anchored to
    // the walls either side and the floor, its top thread between the walls.
    private static Model SheetWeb(float wide, float tall, int seed)
    {
        var random = new Random(seed);
        var w = wide / 2;
        var top = Sag(new Vector2(w, tall), new Vector2(-w, tall), tall * 0.06f, 4);
        var frame = new[] { new Vector2(-w, 0), new Vector2(w, 0) }.Concat(top).ToArray();
        var edges = Enumerable.Range(0, frame.Length).Select(i => i > 1 && i < frame.Length - 1).ToArray();
        var m = new Model();
        Weave(m, OrbWeb(new Vector2(w * 0.06f, tall * 0.58f), frame, edges, 14, wide * 0.08f, wide * 0.07f, random, 24, 17, 28), Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, both: true);
        return m;
    }

    // A cobweb between two columns `span` apart (their middles; the model's origin halfway, at its top thread's ends, the columns at
    // x = -span/2 and +span/2): a web sagging from a thread between them, threads hanging under it -- an old one, torn.
    private const float ColumnRadius = 700;
    private static Model DrapeWeb(float span, int seed)
    {
        var random = new Random(seed);
        var w = span / 2 - ColumnRadius;
        float side = MathF.Min(1500, w * 0.6f), deep = MathF.Min(2300, w * 0.95f);
        var top = Sag(new Vector2(w, 0), new Vector2(-w, 0), w * 0.12f, 6);
        var bottom = Sag(new Vector2(-w, -side), new Vector2(w, -side), deep - side, 6);
        var frame = top.Concat(bottom).ToArray();
        var edges = Enumerable.Range(0, frame.Length).Select(i => i != top.Length - 1 && i != frame.Length - 1).ToArray();
        var threads = OrbWeb(new Vector2(-w * 0.05f, -deep * 0.38f), frame, edges, 16, w * 0.07f, w * 0.06f, random, 26, 18, 30);
        foreach (var x in new[] { -0.6f, -0.2f, 0.3f, 0.7f })
            threads.AddRange(Dangle(bottom.MinBy(p => MathF.Abs(p.X - x * w)), 500 + (float)random.NextDouble() * 900, 18, random));
        var m = new Model();
        Weave(m, threads, Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, both: true);
        return m;
    }

    // A spider: its back to +z (the way it is seen), its head up (+y), its legs spread round it on a web at z 0 -- `size` 1 about two
    // metres from its fangs to the end of its body (the giant web's a quarter bigger: bigger than a car), its legs one way or the other
    // (`step` 0, 1: the legs it walks on, in turn; -1 at rest). Its middle at the model's origin. Dark purple, its legs banded rust, eight
    // glowing red eyes, its fangs, a pale skull on its back. (`hanging`: head down on a thread up out of its back end, as a small one hangs.)
    private static Model SpiderModel(float size, int step = -1, bool hanging = false)
    {
        var m = new Model();
        Vector3 S(float x, float y, float z) => new Vector3(x, y, z) * size;
        (double, double)[] Scaled((double R, double Y)[] profile) => profile.Select(p => (p.R * size, p.Y * size)).ToArray();
        // its body: the abdomen under the head, a little out from the web; the head (cephalothorax)
        m.Lathe(Scaled(new (double, double)[] { (0, -980), (300, -900), (450, -650), (470, -380), (380, -140), (160, 20), (0, 60) }), 10, _ => SpiderSkin,
            bottom: false, top: false, shape: a => 0.85 + 0.15 * Math.Abs(Math.Sin(a)), at: S(0, 0, 170));
        m.Lathe(Scaled(new (double, double)[] { (0, 60), (190, 120), (290, 300), (260, 500), (150, 640), (0, 680) }), 10, _ => SpiderSkin,
            bottom: false, top: false, at: S(0, 0, 140));
        // (the skull on its back: its cranium, its jaw, the sockets and the nose dark on it, a little proud of the abdomen)
        var skull = S(0, -520, 650);
        var behind = skull - S(0, 0, 60);
        m.Face(Enumerable.Range(0, 10).Select(k => skull + S(190 * MathF.Cos(k * MathF.Tau / 10), 60 + 170 * MathF.Sin(k * MathF.Tau / 10), 0)).ToArray(), SpiderSkull, behind);
        m.Face(new[] { skull + S(-110, -60, 0), skull + S(110, -60, 0), skull + S(80, -230, 0), skull + S(-80, -230, 0) }, SpiderSkull, behind);
        foreach (var x in new[] { -75f, 75f })
            m.Face(Enumerable.Range(0, 6).Select(k => skull + S(x + 48 * MathF.Cos(k * MathF.Tau / 6), 70 + 52 * MathF.Sin(k * MathF.Tau / 6), 6)).ToArray(), Hollow, behind);
        m.Face(new[] { skull + S(-25, -10, 6), skull + S(25, -10, 6), skull + S(0, 30, 6) }, Hollow, behind);
        // its eyes: eight, on the head's front and top, glowing
        foreach (var (x, y, r) in new[] { (-70f, 560f, 42f), (70f, 560f, 42f), (-150f, 520f, 26f), (150f, 520f, 26f), (-40f, 625f, 24f), (40f, 625f, 24f), (-120f, 600f, 20f), (120f, 600f, 20f) })
        {
            var c = S(x, y, 140 + 285 * MathF.Sqrt(MathF.Max(0.05f, 1 - (y - 330) * (y - 330) / (360f * 360))) + 10);
            m.Face(Enumerable.Range(0, 5).Select(k => c + S(r * MathF.Cos(k * MathF.Tau / 5), r * MathF.Sin(k * MathF.Tau / 5), 0)).ToArray(), RedEye, c - S(0, 0, 40));
        }
        // its fangs, over its eyes, curved forward
        foreach (var x in new[] { -60f, 60f })
        {
            m.Bar(S(x, 640, 230), S(x * 1.2f, 760, 330), 70 * size, SpiderFang);
            m.Bar(S(x * 1.2f, 760, 330), S(x * 0.8f, 830, 420), 45 * size, SpiderFang);
        }
        // its legs: four each side from the head, each a thigh out to a knee raised towards the eye, a shin down to its foot on the web --
        // the front pair reaching up, the back pair down; the legs it steps with (`step`) further along the way it walks (up)
        var legs = new (float Y, float Out, float Up)[] { (520, 1100, 1350), (420, 1350, 450), (300, 1350, -450), (200, 1050, -1300) };
        for (var side = -1; side <= 1; side += 2)
            for (var i = 0; i < 4; i++)
            {
                var (y, reach, up) = legs[i];
                // (in turn: the left front, the right second, the left third, the right back -- and the other four)
                var lift = step < 0 ? 0 : ((i + (side < 0 ? 0 : 1)) % 2 == step % 2 ? 1 : -1) * 170;
                var hip = S(side * 230, y, 140);
                var foot = S(side * reach, y + up + lift, 0);
                var knee = (hip + foot) / 2 + S(0, MathF.Abs(up) * 0.15f + 180, 420);
                var shin = Vector3.Lerp(knee, foot, 0.55f);
                m.Bar(hip, knee, 95 * size, SpiderSkin);
                m.Bar(knee, shin, 80 * size, SpiderBand);
                m.Bar(shin, foot, 60 * size, SpiderSkin);
            }
        if (hanging)
        {
            m.Move(0, p => new Vector3(-p.X, -p.Y, p.Z));
            m.Strand(new[] { S(0, 960, 170), S(0, 960, 170) + new Vector3(0, 1500, 0) }, 18, SilkBright, Vector3.UnitZ, both: true);
        }
        return m;
    }

    // The decor cobwebs' models: a kind and its sixth number (CobwebDrape: its span, cells; a corner's, a wall's: which of three).
    public const float CornerReach = 1700, CornerBigReach = 2700, WallHalf = 1500;
    private static Model CobwebModel(Kind kind, double size) => kind switch
    {
        Kind.CobwebCorner => CornerWeb(CornerReach, 17 + 11 * (int)size),
        Kind.CobwebCornerBig => CornerWeb(CornerBigReach, 29 + 11 * (int)size),
        Kind.CobwebSheet => SheetWeb(1450, 2250, 41),
        Kind.CobwebDrape => DrapeWeb((float)(size > 0 ? size : 6) * 512, 53 + (int)size),
        Kind.CobwebWall => WallWeb(WallHalf, 67 + 13 * (int)size),
        _ => SpiderModel(0.2f, hanging: true),
    };
    // (how far each reaches round its place, and how far under and over it)
    private static double CobwebReach(Kind kind, double size) => kind switch
    {
        Kind.CobwebCorner => CornerReach + 100, Kind.CobwebCornerBig => CornerBigReach + 100, Kind.CobwebSheet => 800,
        Kind.CobwebDrape => (size > 0 ? size : 6) * 256 + 300, Kind.CobwebWall => WallHalf + 100, _ => 500,
    };
    private static (double Under, double Over) CobwebHeight(Kind kind, double size) => kind switch
    {
        Kind.CobwebCorner => (CornerReach * 1.6, 100), Kind.CobwebCornerBig => (CornerBigReach * 1.6, 100), Kind.CobwebSheet => (100, 2500),
        Kind.CobwebDrape => (Math.Min(2300, ((size > 0 ? size : 6) * 256 - ColumnRadius) * 0.95) + 1500, 300), Kind.CobwebWall => (WallHalf * 1.55, 100),
        _ => (300, 1800),
    };

    // ---- the giant web (RACEMOD.CPP web=, web_part=)
    // Its size round its foot -- the road's middle under it, the way through it +z, across it +x (the lap's left): its frame's posts `half`
    // either side (the columns' faces), its top GiantTall over the road, its hub GiantHub over it; the hole a car tears through it, half an
    // ellipse on the road GiantHoleAcross either side and GiantHoleUp high -- the road's width and the camera's height behind the car --
    // under its hub. Its strands face the cars coming (-z: they never see it from behind).
    public const float GiantTall = 5600, GiantHub = 3350, GiantHoleAcross = 2950, GiantHoleUp = 2850;
    // (the game variable whose being set hides the still copy among the island's decor: the frog's, held set while racing)
    public const int GiantWebVar = RaceTrackFrog.DecorVar;
    // a part's kind (web_part=): drawn always, drawn while the web is whole, a tatter hanging from where a thread is torn (drawn once it is
    // torn, swinging), a scrap flung after the car (for a moment after it is torn)
    public const int PartFrame = 0, PartMiddle = 1, PartTatter = 2, PartScrap = 3;

    // A giant web's part as a body: its kind, its model, and its place in the web (across, up: its hinge, a tatter's), its turn.
    public sealed record WebPart(int Kind, byte[] Body, float Across, float Up, int Turn = 0);

    private static List<WebThread> GiantThreads(float half)
    {
        var random = new Random(20261017);
        var w = half + 60;
        // the frame: the posts (the columns' faces), the road under it, its top thread sagging between the columns' tops
        var top = Sag(new Vector2(w, GiantTall), new Vector2(-w, GiantTall), 380, 8);
        var frame = new[] { new Vector2(-w, 0), new Vector2(w, 0) }.Concat(top).ToArray();
        var edges = Enumerable.Range(0, frame.Length).Select(i => i > 1 && i < frame.Length - 1).ToArray();
        var threads = OrbWeb(new Vector2(110, GiantHub), frame, edges, 26, 300, 235, random, 46, 34, 64, jitter: 0.05f, sag: true);
        // (and its guy threads: from the posts up to its top, and down to the road)
        foreach (var (a, b) in new[] { (new Vector2(-w, 4200), new Vector2(-w * 0.55f, GiantTall - 150)), (new Vector2(w, 4300), new Vector2(w * 0.5f, GiantTall - 140)),
                                       (new Vector2(-w, 900), new Vector2(-w * 0.62f, 0)), (new Vector2(w, 1000), new Vector2(w * 0.6f, 0)) })
            threads.Add(new WebThread(new[] { a, b }, 52, SilkFrame));
        return threads;
    }

    // (a body of the giant web's is begun again once it has this many points or faces: a thread more -- its longest a turn of the spiral
    // in pieces, 130 points -- stays under what a body holds)
    private const int BodyRoom = 400;

    private static bool InHole(Vector2 p) => p.Y < GiantHoleUp * MathF.Sqrt(MathF.Max(0, 1 - p.X * p.X / (GiantHoleAcross * GiantHoleAcross)));

    // Where a piece from a to b crosses the hole's edge (its share of the way from a): the half ellipse's (the road under it is no edge).
    private static List<float> HoleCrossings(Vector2 a, Vector2 b)
    {
        var list = new List<float>();
        double ax = a.X / GiantHoleAcross, ay = a.Y / GiantHoleUp, dx = (b.X - a.X) / GiantHoleAcross, dy = (b.Y - a.Y) / GiantHoleUp;
        double qa = dx * dx + dy * dy, qb = 2 * (ax * dx + ay * dy), qc = ax * ax + ay * ay - 1, disc = qb * qb - 4 * qa * qc;
        if (qa < 1e-12 || disc < 0) return list;
        foreach (var t in new[] { (-qb - Math.Sqrt(disc)) / (2 * qa), (-qb + Math.Sqrt(disc)) / (2 * qa) })
            if (t > 1e-4 && t < 1 - 1e-4 && a.Y + (b.Y - a.Y) * t >= 0) list.Add((float)t);
        return list;
    }

    // The giant web's parts, the race-track mode's bodies (made as the spooks' are: `racerHeader`, the racer's car's).
    public static List<WebPart> GiantWebParts(float half, byte[] racerHeader)
    {
        var parts = new List<WebPart>();
        // what a car leaves (outside the hole) and what it tears out (inside): each thread cut where it crosses the hole's edge, a run of
        // its pieces on one side a thread of that side's; where a thread is cut, a tatter may hang
        var keep = new List<WebThread>(); var tornOut = new List<WebThread>(); var cut = new List<Vector2>();
        foreach (var t in GiantThreads(half))
        {
            var run = new List<Vector2> { t.Points[0] };
            var inside = InHole(t.Points[0]);
            void Flush() { if (run.Count >= 2) (inside ? tornOut : keep).Add(t with { Points = run.ToArray() }); }
            for (var i = 0; i + 1 < t.Points.Length; i++)
            {
                Vector2 a = t.Points[i], b = t.Points[i + 1];
                foreach (var c in HoleCrossings(a, b).OrderBy(c => c))
                {
                    var p = Vector2.Lerp(a, b, c);
                    run.Add(p);
                    Flush();
                    run = new List<Vector2> { p };
                    inside = !inside;
                    if (p.Y > 500) cut.Add(p);
                }
                run.Add(b);
            }
            Flush();
        }
        // the parts' bodies, the strands facing -z, in pieces no longer than 700: in models no bigger than a body holds (550 points, 550
        // polygons)
        void Pack(List<WebThread> threads, int kind)
        {
            var m = new Model();
            foreach (var t in threads)
            {
                if (m.Points.Count > BodyRoom || m.FaceCount > BodyRoom) { parts.Add(new WebPart(kind, m.WriteMoving(racerHeader), 0, 0)); m = new Model(); }
                Weave(m, new[] { t }, Vector3.Zero, Vector3.UnitX, -Vector3.UnitZ, both: false, piece: 700);
            }
            if (m.Faces.Count > 0) parts.Add(new WebPart(kind, m.WriteMoving(racerHeader), 0, 0));
        }
        Pack(keep, PartFrame);
        Pack(tornOut, PartMiddle);
        // the tatters: threads hanging from the torn ends round the hole's edge -- of the ends, the nearest to each of TatterCount places
        // spread evenly round it -- of three lengths (none reaching the road), turned a little each
        var tatters = new[] { 650f, 1050f, 1500f }.Select((length, i) =>
        {
            var m = new Model();
            Weave(m, Dangle(Vector2.Zero, length, 30, new Random(31 + i)), Vector3.Zero, Vector3.UnitX, -Vector3.UnitZ, both: true, piece: 500);
            return (Length: length, Body: m.WriteMoving(racerHeader));
        }).ToArray();
        float RimAngle(Vector2 p) => MathF.Atan2(p.Y / GiantHoleUp, p.X / GiantHoleAcross);
        var used = new HashSet<int>();
        const int TatterCount = 12;
        for (var k = 0; k < TatterCount; k++)
        {
            var want = MathF.PI * (k + 0.5f) / TatterCount;
            var best = Enumerable.Range(0, cut.Count).Where(j => !used.Contains(j) && MathF.Abs(RimAngle(cut[j]) - want) < MathF.PI / TatterCount * 0.6f)
                .OrderBy(j => MathF.Abs(RimAngle(cut[j]) - want)).DefaultIfEmpty(-1).First();
            if (best < 0) continue;
            used.Add(best);
            var at = cut[best];
            var fits = tatters.Where(t => t.Length < at.Y - 300).ToArray();
            if (fits.Length == 0) continue;
            var tatter = fits[(k * 7 + 3) % fits.Length];
            parts.Add(new WebPart(PartTatter, tatter.Body, at.X, at.Y, (k * 397) % 300 - 150));
        }
        // the scraps the car flings along with it: small tangles of strands (two of them), from places across the hole
        var scraps = Enumerable.Range(0, 2).Select(i =>
        {
            var r = new Random(71 + i);
            var m = new Model();
            for (var j = 0; j < 6; j++)
            {
                var a = new Vector3((float)(r.NextDouble() - 0.5) * 260, (float)(r.NextDouble() - 0.5) * 260, (float)(r.NextDouble() - 0.5) * 160);
                var d = Vector3.Normalize(new Vector3((float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f, (float)r.NextDouble() * 0.6f - 0.3f));
                var n = Vector3.Cross(d, Vector3.UnitY);
                m.Strand(new[] { a, a + d * (float)(160 + r.NextDouble() * 220) }, 22, j % 2 == 0 ? SilkRadial : SilkSpiral,
                    n.LengthSquared() < 1e-4f ? Vector3.UnitZ : Vector3.Normalize(n), both: true);
            }
            return m.WriteMoving(racerHeader);
        }).ToArray();
        foreach (var (x, y, i) in new[] { (-1900f, 700f, 0), (-900f, 1600f, 1), (300f, 2200f, 0), (1000f, 900f, 1), (2000f, 1500f, 0), (-200f, 600f, 1) })
            parts.Add(new WebPart(PartScrap, scraps[i], x, y, (int)(x * 3) & 4095));
        return parts;
    }

    // The giant web's spider, its legs one way and the other (the race-track mode draws them in turn as it scuttles).
    public static (byte[] A, byte[] B) GiantSpider(byte[] racerHeader) => (SpiderModel(GiantSpiderSize, 0).WriteMoving(racerHeader), SpiderModel(GiantSpiderSize, 1).WriteMoving(racerHeader));
    private const float GiantSpiderSize = 1.25f;

    // ---- into the island: each giant web's place (its bodies go into BODY.HQR when the build finishes: InstallGiantWebs), and a still copy
    // of it whole among the island's decor for the editor's view, hidden while racing (GiantWebVar)
    public static void PlaceGiantWebs(IslandFile island, RaceTrackPlan plan, RaceTrackOptions o, RaceTrackReport report)
    {
        foreach (var g in plan.Halloween?.GiantWebs ?? Array.Empty<double[]>())
        {
            if (g.Length < 6) continue;
            var len = Math.Sqrt(g[3] * g[3] + g[4] * g[4]);
            if (len < 1e-9) continue;
            var web = new RaceTrackReport.GiantWeb(g[0] + plan.OriginCellX, g[1] + plan.OriginCellZ, g[2], g[3] / len, g[4] / len, g[5]);
            report.GiantWebs.Add(web);
            var still = "";
            if (o.NewBodyBase >= 0)
            {
                // (each body a decor at the web's foot, turned so that its +z is the lap's way)
                var turn = (int)Math.Round(Math.Atan2(web.WayX, web.WayZ) * 4096 / (2 * Math.PI)) & 4095;
                double wx = web.X * 512, wz = web.Z * 512;
                var y = (int)Math.Round(web.Y);
                int placed = 0, bodies = 0;
                foreach (var (body, reach) in GiantWebStill((float)(web.Half * 512)))
                {
                    var index = o.NewBodyBase + report.NewBodies.Count;
                    report.NewBodies.Add(body);
                    bodies++;
                    var (here, _) = Palace.BrickBuilding.AddToCubes(island, index, (GiantWebVar << 16) | turn, wx, y, wz, wx - reach, wx + reach, y - 100,
                        Palace.BrickBuilding.NoBoxTop, wz - reach, wz + reach, IslandDecors.MaxPerCubeRaceTrack);
                    placed += here > 0 ? 1 : 0;
                }
                still = $"; a still copy among the island's decor ({placed} of its {bodies} bodies placed; hidden while racing: game variable {GiantWebVar})";
            }
            report.Notes.Add($"a giant web across the road at cell ({web.X:0.0}, {web.Z:0.0}), {web.Y:0} high, the lap's way through it ({web.WayX:0.00}, {web.WayZ:0.00}), " +
                             $"{2 * web.Half:0.0} cells across and {GiantTall:0} high, the race-track mode's (web=){still}");
        }
    }

    // ---- into BODY.HQR: each giant web's parts (one body each; the tatters' and scraps' several times over) and its spider's two, after the
    // rest of Halloween's. Lines for the log.
    public static List<string> InstallGiantWebs(string gameDirectory, RaceTrackReport report)
    {
        var log = new List<string>();
        if (report.GiantWebs.Count == 0) return log;
        var path = Path.Combine(gameDirectory, "BODY.HQR");
        var racerHeader = Body.Read(HqrArchive.Open(path).Read(RaceTrackBaldinoCar.RacerBody), 2).Header;
        var bodies = File.ReadAllBytes(path);
        var index = HqrArchive.CountEntries(path);
        for (var i = 0; i < report.GiantWebs.Count; i++)
        {
            var web = report.GiantWebs[i];
            var parts = new List<(int Kind, int Body, int Across, int Up, int Turn)>();
            var made = new Dictionary<byte[], int>(ReferenceEqualityComparer.Instance);
            var first = index;
            foreach (var p in GiantWebParts((float)(web.Half * 512), racerHeader))
            {
                if (!made.TryGetValue(p.Body, out var body))
                {
                    bodies = HqrWriter.AppendEntry(bodies, HqrWriter.StoredEntry(p.Body));
                    body = made[p.Body] = index++;
                }
                parts.Add((p.Kind, body, (int)Math.Round(p.Across), (int)Math.Round(p.Up), p.Turn));
            }
            var (a, b) = GiantSpider(racerHeader);
            bodies = HqrWriter.AppendEntry(bodies, HqrWriter.StoredEntry(a));
            bodies = HqrWriter.AppendEntry(bodies, HqrWriter.StoredEntry(b));
            report.GiantWebs[i] = web with { SpiderA = index, SpiderB = index + 1, Parts = parts };
            index += 2;
            log.Add($"the giant web: BODY.HQR {first}-{index - 1} -- {parts.Count(p => p.Kind == PartFrame)} bodies of its frame and what a car leaves of it, " +
                    $"{parts.Count(p => p.Kind == PartMiddle)} of its middle, {parts.Count(p => p.Kind == PartTatter)} tatters, {parts.Count(p => p.Kind == PartScrap)} scraps, " +
                    $"its spider {index - 2} and {index - 1}");
        }
        File.WriteAllBytes(path, bodies);
        return log;
    }

    // A giant web as the car file has it (RACEMOD.CPP web=): its foot (island units, the road's height), the lap's way through it
    // (thousandths), its posts' half width, its height, its hub's, the hole's half width and height, its spider's two bodies; and each part
    // (web_part=): its kind, its body, its place across and up the web, its turn.
    public static int[] Line(RaceTrackReport.GiantWeb w) => new[]
    {
        (int)Math.Round(w.X * 512), (int)Math.Round(w.Y), (int)Math.Round(w.Z * 512), (int)Math.Round(w.WayX * 1000), (int)Math.Round(w.WayZ * 1000),
        (int)Math.Round(w.Half * 512), (int)GiantTall, (int)GiantHub, (int)GiantHoleAcross, (int)GiantHoleUp, w.SpiderA, w.SpiderB,
    };
    public static IEnumerable<int[]> PartLines(RaceTrackReport.GiantWeb w) =>
        (w.Parts ?? new()).Select(p => new[] { p.Kind, p.Body, p.Across, p.Up, p.Turn & 4095 });

    // ... and as the island's still decor (the editor's view): the whole web, seen from both sides, and the spider at its hub, its back to
    // the cars coming -- each body and how far it reaches round its foot.
    public static List<(byte[] Body, double Reach)> GiantWebStill(float half)
    {
        var list = new List<(byte[], double)>();
        var m = new Model();
        foreach (var t in GiantThreads(half))
        {
            if (m.Points.Count > BodyRoom || m.FaceCount > BodyRoom) { list.Add((m.Write(), half + 200)); m = new Model(); }
            Weave(m, new[] { t }, Vector3.Zero, Vector3.UnitX, -Vector3.UnitZ, both: true, piece: 700);
        }
        if (m.Faces.Count > 0) list.Add((m.Write(), half + 200));
        var spider = SpiderModel(GiantSpiderSize);
        spider.Move(0, p => new Vector3(-p.X, p.Y + GiantHub - 250, -p.Z - 60));
        list.Add((spider.Write(), 2500));
        return list;
    }
}
