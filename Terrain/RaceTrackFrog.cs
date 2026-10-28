using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// The giant frog the lap drives through (RaceTrackPlan.Frog; RACEMOD.CPP frog=): Wannies Island's -- the user, 2026-10-14: "The massive
// blue circle is to be a giant frog that we drive through. The frog repeatedly opens and closes its mouth and extends its tongue as it does
// so, so we have to time the jump into the mouth. Exiting the frog we go up a slope to the top of the tower"; a car meeting a shut mouth is
// bounced back. Mouth and Back are the plan's points where the road goes into its mouth (a carried jump's landing: the jump is into the
// mouth) and comes out of its back; the road runs straight between them.
internal sealed class FrogRun
{
    public int Mouth { get; set; }
    public int Back { get; set; }
}

// A branch of a raised road (RaceTrackPlan.Branches): a way off the lap and back onto it -- Wannies Island's shortcut over its boost panel
// and jump. Its points (cells from the plan's origin, half a cell apart, its first on the lap's road at the lap's point From, its last on it
// at To), a height and a half width each; its carried jumps ([foot, lip, landing, landing's foot, camera], its own points) and boost panels
// ([from, to, from and to across], its own points).
internal sealed class BranchRun
{
    public double[][] Points { get; set; } = Array.Empty<double[]>();
    public double[] Heights { get; set; } = Array.Empty<double>();
    public double[]? Halfs { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    public int[][]? ArcJumps { get; set; }
    public double[][]? BoostPanels { get; set; }
    // its stretches on the lap's own road ([first, last], its own points): no deck of its own there, the lap's is
    public int[][]? Hidden { get; set; }
    // its stretches with no road at all ([first, last], its own points) that no carried jump flies: a teleporter's gap -- its road runs up
    // to the teleporter (RACEMOD.CPP teleport=) and on from where the teleporter puts the car out (the Temple of Bú's hidden passage)
    public int[][]? Gaps { get; set; }
}

// The frog as placed: its mouth (island cells, the road's middle there) and the road's height there, the way the lap runs into it (a unit
// vector), how long it is (cells, mouth to back), the ground's height under its middle; the lap's points of its mouth and back; once made
// (Install), its body (BODY.HQR).
internal sealed record FrogInfo(double X, double Z, double Y, double Tx, double Tz, double Length, double Ground, int Mouth, int Back, int Body = -1);

// The frog is drawn by the race-track mode, not by a scene: one body, posed every frame -- its jaw and tongue turned by the mode's own clock
// (so the mouth the player sees is the mouth the mode tests) -- and drawn into the island's background after its ground and decor, its depth
// written, so the cars are hidden by it, and seen inside its throat, as they should be. The road through it has no deck of its own (TrackRoad
// .Hidden): its floor is the frog's throat's, and the race-track mode's floor is under it as on any raised road.
//
// The body, in its own coordinates (units: a cell is 512) -- +z out of its mouth (towards the cars coming), y up, the origin the road's middle
// at the mouth:
//  - a throat, the road's tunnel through it: ThroatHalf either side, ThroatHigh high, from the mouth to its back;
//  - the skin round it, ring by ring, joined to the throat's ends by its face round the mouth and its rump round the way out;
//  - the lower jaw (bone 1): a slab hinged at the back of the mouth, its top the mouth's floor -- the road -- while the mouth is open (as
//    built); turned up about its hinge (JawShut) it shuts the mouth, a drawbridge filling it. Under it, a pale throat sac and walls, so that
//    nothing shows through when it is up;
//  - the tongue (bones 2-4, from the jaw's front), lying folded back along the mouth's floor; turned over the lip (TongueFlip) it shoots out
//    of the mouth at the cars coming, its two further pieces curled (TongueCurl);
//  - bulging eyes, nostrils, front legs to its hands and folded hind legs to its webbed feet, on a lily pad on a stalk down to the ground.
internal static class RaceTrackFrog
{
    // The mouth's cycle (ms; RACEMOD.CPP frog=): opening (Move), open (Open), shutting (Move), shut (the rest of Period); the tongue out
    // over TongueOut once the mouth is open, held there TongueHold, back over TongueIn. (The race-track mode lets a car in until
    // RACE_FROG_CLEAR before it shuts -- the time to get past the jaw -- and the jaw twitches then: too late.) (The user, 2026-10-15:
    // "make the frogs mouth open for slightly longer with the tongue remaining out longer too": open 3.1 s, not 2.5; the tongue held out
    // 0.9 s, not 0.16; the shut time as it was.)
    public const int Period = 5000, Move = 320, Open = 3100, TongueOut = 260, TongueHold = 900, TongueIn = 360;
    // The jaw's turn about its hinge shut, the tongue's over the lip and its two further pieces' curl (the engine's units: 4096 a turn).
    public const int JawShut = -680, TongueFlip = 1750, TongueCurl = 260;
    // The throat (units): either side of the road's middle and over the road; the jaw from its hinge to the mouth, its lip past the mouth,
    // its depth; the throat sac's top under it.
    public const float ThroatHalf = 2150, ThroatHigh = 2900;
    private const float Jaw = 3600, JawLip = 150, JawDeep = 900, JawInset = 200, SacTop = 960;
    // the pad it sits on (its top, its radius, the stalk's)
    private const float PadTop = -2450, PadRadius = 6800, PadThick = 200, Stalk = 650;
    // colours (lit: the light runs each up its ramp): its skin, its spots, its belly, the throat's flesh and floor, the tongue, the eyes,
    // the pad and its stalk. A blue frog (the user, 2026-10-15: "change the colour of our frog to blue instead of green"; it was the
    // greens 128 and 112): its skin the blue ramp (192), its spots, hands and feet the slate blue (176); the lily pad and its stalk green.
    private const int Skin = 193, Spot = 176, Belly = 36, Flesh = 65, FleshFloor = 64, Tongue = 72, TongueTip = 74, EyeBall = 103, Pupil = 48,
        Socket = 195, Pad = 130, PadEdge = 128, StalkColour = 113;

    // Where the frog is: the lap's points from its mouth to its back left without a deck (Hidden), and its place in the report.
    public static FrogInfo? Place(IslandFile island, TrackRoad r, int mouth, int back, RaceTrackReport report)
    {
        var n = r.Count;
        double tx = r.X[back] - r.X[mouth], tz = r.Z[back] - r.Z[mouth];
        var length = Math.Sqrt(tx * tx + tz * tz);
        if (length < 4) { report.Notes.Add("WARNING: the frog's mouth and back are too close together -- no frog"); return null; }
        tx /= length; tz /= length;
        r.Hidden ??= new bool[n];
        for (var k = mouth; ; k = (k + 1) % n) { r.Hidden[k] = true; if (k == back) break; }
        double mx = r.X[mouth] + tx * length / 2, mz = r.Z[mouth] + tz * length / 2;
        var ground = IslandOps.Altitude(island, mx * 512, mz * 512) ?? 0;
        var frog = new FrogInfo(r.X[mouth], r.Z[mouth], r.H[mouth], tx, tz, length, ground, mouth, back);
        report.Frogs.Add(frog);
        report.Notes.Add($"a giant frog: its mouth at cell ({frog.X:0.0}, {frog.Z:0.0}), {frog.Y:0} high, {length:0.0} cells long to its back at ({r.X[back]:0.0}, {r.Z[back]:0.0}), " +
                         $"on a lily pad on a stalk down to the ground ({ground:0}); no deck through it (its throat is the road's floor)");
        return frog;
    }

    // Into the game folder: each frog's body appended to BODY.HQR. Lines for the log.
    public static List<string> Install(string gameDirectory, RaceTrackReport report)
    {
        var log = new List<string>();
        if (report.Frogs.Count == 0) return log;
        var path = Path.Combine(gameDirectory, "BODY.HQR");
        var header = Body.Read(HqrArchive.Open(path).Read(RaceTrackBaldinoCar.RacerBody), 2).Header;
        var bodies = File.ReadAllBytes(path);
        var index = HqrArchive.CountEntries(path);
        for (var i = 0; i < report.Frogs.Count; i++)
        {
            var f = report.Frogs[i];
            var body = Make(header, f.Length * 512, f.Y - f.Ground);
            var bytes = body.Write();
            // (drawn close to the camera: its polygons cut at the near plane only a little in front of it -- the camera is in its throat)
            BinaryPrimitives.WriteInt32LittleEndian(bytes, BinaryPrimitives.ReadInt32LittleEndian(bytes) | Palace.BrickBuilding.CloseFlag);
            bodies = HqrWriter.AppendEntry(bodies, HqrWriter.StoredEntry(bytes));
            report.Frogs[i] = f with { Body = index };
            log.Add($"the giant frog: BODY.HQR entry {index} ({body.Vertices.Count} points, {body.Faces.Count} polygons, {body.Spheres.Count} spheres)");
            index++;
        }
        File.WriteAllBytes(path, bodies);
        return log;
    }

    // The game variable whose being set hides the frog's still copy among the island's decor (DecorVar): a decor is hidden by the variable
    // its turn's high word names, as the game's own are; the race-track mode holds it set while it races (RaceTrackIsland.HeldVars).
    public const int DecorVar = 212;

    // The frog as one of the island's decors too, still, its mouth open -- the editor's view, and the 3D export, show the island's decor,
    // not what the race-track mode draws (the user, 2026-10-15: "The frog isn't visible in the editor, is there a reason for this?"): its
    // body appended to the island's own (report.NewBodies), drawn only (nothing touches it), hidden while racing (DecorVar).
    public static void AddDecor(IslandFile island, FrogInfo f, int newBodyBase, RaceTrackReport report)
    {
        if (newBodyBase < 0) return;
        var posed = Make(new byte[96], f.Length * 512, f.Y - f.Ground);
        var world = posed.World();
        var still = new Body
        {
            Game = 2, Static = true, Lit = true, Header = new byte[96], Vertices = world.ToList(),
            Bones = new List<Bone> { new(0, world.Length, 0, -1, new byte[8]) }, Faces = posed.Faces, Spheres = posed.Spheres, Lines = posed.Lines,
        };
        var bytes = still.Write();
        BinaryPrimitives.WriteInt32LittleEndian(bytes, BinaryPrimitives.ReadInt32LittleEndian(bytes) | Palace.BrickBuilding.CloseFlag);
        double wx = f.X * 512, wz = f.Z * 512;
        if (IslandDecors.Locate(island, wx, wz) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCubeRaceTrack)
        {
            report.Notes.Add("WARNING: no room for the frog among the island's decor (it is drawn while racing all the same)");
            return;
        }
        var turn = (int)Math.Round(Math.Atan2(-f.Tx, -f.Tz) * 4096 / (2 * Math.PI)) & 4095;
        var d = IslandDecors.Blank(newBodyBase + report.NewBodies.Count, at.X, (int)Math.Round(f.Y), at.Z, (DecorVar << 16) | turn);
        // (its box: its footprint turned as the engine turns it -- x' = x cos + z sin, z' = -x sin + z cos -- from its foot up; its top
        // under its bottom: drawn only, nothing touches it)
        double c = Math.Cos(turn * 2 * Math.PI / 4096), s = Math.Sin(turn * 2 * Math.PI / 4096);
        var xs = world.Select(v => v.X * c + v.Z * s).ToList(); var zs = world.Select(v => -v.X * s + v.Z * c).ToList();
        d.XMin = at.X + (int)Math.Floor(xs.Min()); d.XMax = at.X + (int)Math.Ceiling(xs.Max());
        d.ZMin = at.Z + (int)Math.Floor(zs.Min()); d.ZMax = at.Z + (int)Math.Ceiling(zs.Max());
        d.YMin = d.Y + (int)Math.Floor(world.Min(v => v.Y)); d.YMax = -32000;
        at.Cube.Decors.Add(d);
        report.NewBodies.Add(bytes);
        report.Placed.Add($"the giant frog among the island's decor, still, its mouth open (hidden while racing: game variable {DecorVar})");
    }

    // The frog's line for the race-track mode (RACEMOD.CPP frog=): its mouth (island units, the road's height), the way into it
    // (thousandths), its body, its length; the mouth's cycle and its parts' turns; the throat's half width.
    public static int[] Line(FrogInfo f) => new[]
    {
        (int)Math.Round(f.X * 512), (int)Math.Round(f.Y), (int)Math.Round(f.Z * 512), (int)Math.Round(f.Tx * 1000), (int)Math.Round(f.Tz * 1000), f.Body,
        (int)Math.Round(f.Length * 512), Period, Move, Open, TongueOut, TongueHold, TongueIn, JawShut, TongueFlip, TongueCurl, (int)ThroatHalf,
    };

    // The body: `length` from its mouth to its back (units), its floor `high` over the ground (its stalk's length).
    public static Body Make(byte[] header, double length, double high)
    {
        var m = new CarMesh("the giant frog", new[] { -1, 0, 1, 2, 3 });
        var L = (float)length;
        float W = ThroatHalf, H = ThroatHigh;
        // the throat's cross-section, round from the floor's left corner up its left wall, over its roof, down its right wall and back
        // along its floor (three pieces: the road's middle one)
        var profile = new Vector2[]
        {
            new(-W, 0), new(-W, 0.30f * H), new(-W, 0.58f * H), new(-0.86f * W, 0.80f * H), new(-0.58f * W, 0.95f * H), new(-0.2f * W, H),
            new(0.2f * W, H), new(0.58f * W, 0.95f * H), new(0.86f * W, 0.80f * H), new(W, 0.58f * H), new(W, 0.30f * H), new(W, 0),
            new(W / 3, 0), new(-W / 3, 0),
        };
        var P = profile.Length;
        bool Floor(int k) => k >= 11;                         // the edge from point k to k + 1 is the floor's
        // the throat's sections: the mouth, halfway to the jaw's hinge, the hinge, then along to the back a little over a metre apart
        var stations = new List<float> { 0, -Jaw / 2, -Jaw };
        var rest = Math.Max(1, (int)Math.Ceiling((L - Jaw) / 1300));
        for (var s = 1; s <= rest; s++) stations.Add(-Jaw - (L - Jaw) * s / rest);
        var throat = stations.Select(z => profile.Select(p => m.P(0, new Vector3(p.X, p.Y, z))).ToArray()).ToList();
        for (var s = 0; s + 1 < throat.Count; s++)
        {
            var mid = new Vector3(0, H / 2, (stations[s] + stations[s + 1]) / 2);
            for (var k = 0; k < P; k++)
            {
                // (the floor over the jaw is the jaw's own)
                if (Floor(k) && stations[s + 1] >= -Jaw - 1) continue;
                var a = throat[s]; var b = throat[s + 1];
                m.In(new[] { a[k], a[(k + 1) % P], b[(k + 1) % P], b[k] }, Floor(k) ? FleshFloor : (s + k) % 2 == 0 ? Flesh : Flesh + 1, mid);
            }
        }
        // the skin: rings round the throat -- z, the ring's middle's height, its half width, how far up and down from its middle -- each point
        // out from the throat's middle the way its throat point is, on a rounded box (power 2.5)
        var rings = new (float Z, float Mid, float Half, float Up, float Down)[]
        {
            (250, 1500, 3700, 2600, 2700),                 // its face round the mouth: wide
            (-1500, 1800, 4700, 3400, 3100),               // its cheeks, under its eyes: its widest
            (-Jaw, 1700, 4600, 3500, 3600),
            (-0.58f * L, 1600, 4300, 3600, 4000),          // its back, sloping down ...
            (-0.78f * L, 1500, 3900, 3000, 3700),
            (-0.94f * L, 1450, 3300, 2400, 3000),
            (-L - 300, 1450, 2700, 1900, 2300),            // ... to its rump round the way out
        };
        const float Power = 2.5f;
        var skin = rings.Select(ring => profile.Select(p =>
        {
            var a = MathF.Atan2(p.Y - H / 2, p.X);
            float c = MathF.Cos(a), s = MathF.Sin(a);
            var x = ring.Half * MathF.Sign(c) * MathF.Pow(MathF.Abs(c), 2 / Power);
            var y = ring.Mid + (s >= 0 ? ring.Up : ring.Down) * MathF.Sign(s) * MathF.Pow(MathF.Abs(s), 2 / Power);
            return m.P(0, new Vector3(x, y, ring.Z));
        }).ToArray()).ToList();
        for (var s = 0; s + 1 < skin.Count; s++)
        {
            var inside = new Vector3(0, (rings[s].Mid + rings[s + 1].Mid) / 2, (rings[s].Z + rings[s + 1].Z) / 2);
            for (var k = 0; k < P; k++)
            {
                var a = skin[s]; var b = skin[s + 1];
                var low = m.At(a[k]).Y < rings[s].Mid - 0.45f * rings[s].Down && m.At(a[(k + 1) % P]).Y < rings[s].Mid - 0.45f * rings[s].Down;
                var top = k >= 3 && k <= 8;
                m.Out(new[] { a[k], a[(k + 1) % P], b[(k + 1) % P], b[k] }, low ? Belly : top && (s + k) % 3 == 0 ? Spot : Skin, inside);
            }
        }
        // its face, from the mouth's edge out to the first ring (the chin under the mouth pale), and its rump round the way out
        for (var k = 0; k < P; k++)
        {
            var t = throat[0]; var r0 = skin[0];
            m.Out(new[] { t[k], t[(k + 1) % P], r0[(k + 1) % P], r0[k] }, Floor(k) ? Belly : Skin, new Vector3(0, H / 2, -1500));
            var tb = throat[^1]; var rb = skin[^1];
            m.Out(new[] { tb[k], tb[(k + 1) % P], rb[(k + 1) % P], rb[k] }, Skin, new Vector3(0, H / 2, -L + 1500));
        }
        // under the jaw: the throat sac's top and its walls, from the mouth to the hinge (seen only while the jaw is up)
        {
            var sac = new[] { 0f, -Jaw / 2, -Jaw }.Select(z => (L: m.P(0, new Vector3(-W, -SacTop, z)), R: m.P(0, new Vector3(W, -SacTop, z)))).ToArray();
            for (var s = 0; s < 2; s++)
            {
                m.Up(new[] { sac[s].L, sac[s].R, sac[s + 1].R, sac[s + 1].L }, FleshFloor);
                var inside = new Vector3(0, -SacTop / 2, (stations[s] + stations[s + 1]) / 2);
                m.In(new[] { throat[s][0], throat[s + 1][0], sac[s + 1].L, sac[s].L }, Flesh, inside);
                m.In(new[] { throat[s][11], throat[s + 1][11], sac[s + 1].R, sac[s].R }, Flesh, inside);
            }
            m.In(new[] { throat[2][0], throat[2][11], sac[2].R, sac[2].L }, Flesh, new Vector3(0, -SacTop / 2, 0));
        }
        // its eyes, bulging on top of its head (a socket, the gold eyeball, the black pupil), and its nostrils
        foreach (var side in new[] { -1f, 1f })
        {
            m.Ball(0, new Vector3(side * 2600, 4900, -1700), 1550, Socket);
            m.Ball(0, new Vector3(side * 2750, 5300, -1250), 1200, EyeBall);
            m.Ball(0, new Vector3(side * 2850, 5450, -120), 520, Pupil);
            m.Ball(0, new Vector3(side * 800, 3950, 400), 200, Pupil);
        }
        // its legs: the front ones down to its hands on the pad, the hind ones folded beside it, their webbed feet forward on the pad
        foreach (var side in new[] { -1f, 1f })
        {
            m.Strut(0, new Vector3(side * 3900, 0, -2000), new Vector3(side * 4300, PadTop + 300, 1000), 1100, Skin);
            m.Box(0, new Vector3(side * 4400, PadTop + 130, 1500), new Vector3(2000, 260, 1800), Spot);
            m.Strut(0, new Vector3(side * 3800, 300, -0.80f * L), new Vector3(side * 5600, -900, -0.42f * L), 2300, Skin);
            m.Strut(0, new Vector3(side * 5600, -900, -0.42f * L), new Vector3(side * 5300, PadTop + 500, -0.88f * L), 1400, Skin);
            m.Box(0, new Vector3(side * 5400, PadTop + 130, -0.62f * L), new Vector3(2200, 260, 3600), Spot);
        }
        // the lily pad it sits on, and the stalk under it down to the ground
        {
            var mid = new Vector3(0, PadTop, -0.5f * L);
            const int N = 18;
            var centre = m.P(0, mid);
            var inner = Enumerable.Range(0, N).Select(i => m.P(0, mid + new Vector3(PadRadius / 2 * MathF.Sin(i * MathF.Tau / N), 0, PadRadius / 2 * MathF.Cos(i * MathF.Tau / N)))).ToArray();
            var rim = Enumerable.Range(0, N).Select(i => m.P(0, mid + new Vector3(PadRadius * MathF.Sin(i * MathF.Tau / N), 0, PadRadius * MathF.Cos(i * MathF.Tau / N)))).ToArray();
            var under = Enumerable.Range(0, N).Select(i => m.P(0, mid + new Vector3(PadRadius * MathF.Sin(i * MathF.Tau / N), -PadThick, PadRadius * MathF.Cos(i * MathF.Tau / N)))).ToArray();
            var bottom = m.P(0, mid - new Vector3(0, PadThick, 0));
            for (var i = 0; i < N; i++)
            {
                var j = (i + 1) % N;
                m.Up(new[] { centre, inner[i], inner[j] }, Pad);
                m.Up(new[] { inner[i], rim[i], rim[j], inner[j] }, i % 2 == 0 ? Pad : Pad + 1);
                m.Out(new[] { rim[i], rim[j], under[j], under[i] }, PadEdge, mid - new Vector3(0, PadThick / 2, 0));
                m.In(new[] { bottom, under[i], under[j] }, PadEdge, mid - new Vector3(0, PadThick + 1000, 0));
            }
            var foot = (float)Math.Max(-32000, -high);
            const int S = 6;
            var top = Enumerable.Range(0, S).Select(i => m.P(0, new Vector3(Stalk * MathF.Sin(i * MathF.Tau / S), PadTop - PadThick, mid.Z + Stalk * MathF.Cos(i * MathF.Tau / S)))).ToArray();
            var low = Enumerable.Range(0, S).Select(i => m.P(0, new Vector3(Stalk * 1.3f * MathF.Sin(i * MathF.Tau / S), foot, mid.Z + Stalk * 1.3f * MathF.Cos(i * MathF.Tau / S)))).ToArray();
            for (var i = 0; i < S; i++)
                m.Out(new[] { top[i], top[(i + 1) % S], low[(i + 1) % S], low[i] }, StalkColour, new Vector3(0, (PadTop + foot) / 2, mid.Z));
        }
        // the lower jaw (bone 1), hinged at the back of the mouth: its top the mouth's floor, in three along it; its lip, its underside, its
        // sides and its back
        m.Pivot(1, m.P(0, new Vector3(0, 0, -Jaw)));
        {
            var w = W - JawInset;
            var zs = new[] { -Jaw, -Jaw * 2 / 3, -Jaw / 3, JawLip };
            var top = zs.Select(z => (L: m.P(1, new Vector3(-w, 0, z)), R: m.P(1, new Vector3(w, 0, z)))).ToArray();
            var low = zs.Select(z => (L: m.P(1, new Vector3(-w, -JawDeep, z)), R: m.P(1, new Vector3(w, -JawDeep, z)))).ToArray();
            var inside = new Vector3(0, -JawDeep / 2, (JawLip - Jaw) / 2);
            for (var s = 0; s < 3; s++)
            {
                m.Out(new[] { top[s].L, top[s].R, top[s + 1].R, top[s + 1].L }, Flesh, inside);
                m.Out(new[] { low[s].L, low[s].R, low[s + 1].R, low[s + 1].L }, Skin + 2, inside);
                m.Out(new[] { top[s].L, top[s + 1].L, low[s + 1].L, low[s].L }, Skin, inside);
                m.Out(new[] { top[s].R, top[s + 1].R, low[s + 1].R, low[s].R }, Skin, inside);
            }
            m.Out(new[] { top[3].L, top[3].R, low[3].R, low[3].L }, Skin, inside);
            m.Out(new[] { top[0].L, top[0].R, low[0].R, low[0].L }, Flesh, inside);
        }
        // the tongue (bones 2-4), from the jaw's lip folded back along the mouth's floor: three pieces, narrowing, a ball at its tip
        {
            var zs = new[] { JawLip - 100, -1400f, -2850f, -4300f };
            var halfs = new[] { 850f, 780f, 680f, 520f };
            const float Low = 15, High = 340;
            m.Pivot(2, m.P(1, new Vector3(0, Low, zs[0])));
            for (var b = 0; b < 3; b++)
            {
                var bone = 2 + b;
                if (b > 0) m.Pivot(bone, m.P(bone - 1, new Vector3(0, Low, zs[b])));
                var near = new[] { m.P(bone, new Vector3(-halfs[b], Low, zs[b])), m.P(bone, new Vector3(halfs[b], Low, zs[b])),
                                   m.P(bone, new Vector3(halfs[b], High, zs[b])), m.P(bone, new Vector3(-halfs[b], High, zs[b])) };
                var far = new[] { m.P(bone, new Vector3(-halfs[b + 1], Low, zs[b + 1])), m.P(bone, new Vector3(halfs[b + 1], Low, zs[b + 1])),
                                  m.P(bone, new Vector3(halfs[b + 1], High, zs[b + 1])), m.P(bone, new Vector3(-halfs[b + 1], High, zs[b + 1])) };
                var inside = new Vector3(0, (Low + High) / 2, (zs[b] + zs[b + 1]) / 2);
                for (var k = 0; k < 4; k++) m.Out(new[] { near[k], near[(k + 1) % 4], far[(k + 1) % 4], far[k] }, k == 2 ? Tongue + 1 : Tongue, inside);
                if (b == 0) m.Out(near, Tongue, inside);
                if (b == 2) { m.Out(far, TongueTip, inside); m.Ball(bone, new Vector3(0, (Low + High) / 2, zs[3] - 250), 620, TongueTip); }
            }
        }
        return m.ToBody(header);
    }
}
