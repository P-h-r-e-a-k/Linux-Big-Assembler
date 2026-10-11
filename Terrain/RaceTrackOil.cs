using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// The oil slick a car drops (the power-ups' oil, RACEMOD.CPP): a puddle flat on the road -- an irregular black blob two cells across, an
// oily blue and violet sheen on it -- as body 1 of the small brown mushroom's entity (112, whose body 0 is the mushroom: a fixed object of
// one bone, as this is), so a slick is a copy of the mushroom's actor with this body (RaceTrackScenes: a few in each scene, out of sight
// until oil is dropped).
internal static class RaceTrackOil
{
    public const int Entity = 112, Generic = 1, MushroomBody = 171;
    public const double Radius = 520;
    // the colours, drawn as they are (no light): near-black, and the sheen's dark blue and violet
    private const int Black = 48, Sheen = 193, Violet = 226;

    public static Body Build()
    {
        var points = new List<Vector3>();
        var faces = new List<Face>();
        int P(Vector3 v) { points.Add(v); return points.Count - 1; }
        // a blob: a ring of points round a middle, the radius wobbling round it; its triangles facing up
        void Blob(Vector3 middle, double radius, int n, double phase, int colour)
        {
            var centre = P(middle);
            var ring = new int[n];
            for (var k = 0; k < n; k++)
            {
                var a = 2 * Math.PI * k / n;
                var r = radius * (1 + 0.18 * Math.Sin(3 * a + phase) + 0.1 * Math.Cos(5 * a + 2 * phase));
                ring[k] = P(middle + new Vector3((float)(Math.Sin(a) * r), 0, (float)(Math.Cos(a) * r)));
            }
            // (a ring point's x is sin, z cos: going round, the triangle centre -> k -> k+1 is the one facing up -- the other way round, it
            // faced down and the engine left it out, seen from above)
            for (var k = 0; k < n; k++) faces.Add(new Face(new[] { centre, ring[k], ring[(k + 1) % n] }, colour, Material: 0));
        }
        Blob(new Vector3(0, 4, 0), Radius, 16, 0.4, Black);
        Blob(new Vector3(90, 7, -70), Radius * 0.42, 9, 1.7, Sheen);
        Blob(new Vector3(-150, 9, 120), Radius * 0.22, 7, 2.9, Violet);
        Blob(new Vector3(110, 10, -40), Radius * 0.12, 6, 0.9, Sheen + 3);
        var body = new Body { Game = 2, Lit = false };
        body.Bones.Add(new Bone(0, points.Count, 0, -1, new byte[8]));
        body.Vertices.AddRange(points);
        body.SetWorld(points.ToArray());
        body.Faces.AddRange(faces);
        body.Validate();
        return body;
    }

    // The oil in the race's item box (RACEMOD.CPP, oil_icon=), as the game shows the inventory's objects turning: an oil drum -- blue, two
    // grey hoops, a grey lid with its bung -- and a black drop of oil over it. Lit, as the inventory's models are; in their size (some 1,500
    // units either way of the middle, which the box turns round).
    public const int IconSource = 15;      // OBJFIX.HQR's Gazogem: the header the icon takes
    private const int DrumBlue = 192, Grey = 48, HoopGrey = 176;

    public static Body BuildIcon()
    {
        var points = new List<Vector3>();
        var lights = new List<float>();
        var faces = new List<Face>();
        var light = 1f;
        int P(Vector3 v) { points.Add(v); lights.Add(light); return points.Count - 1; }
        // (a polygon facing away from `inside`: the engine draws one only from the side its points go round anticlockwise)
        void Out(int[] hs, int colour, Vector3 inside)
        {
            var n = Vector3.Zero;
            for (var i = 0; i < hs.Length; i++)
            {
                var a = points[hs[i]]; var b = points[hs[(i + 1) % hs.Length]];
                n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
            }
            var centre = hs.Aggregate(Vector3.Zero, (s, h) => s + points[h]) / hs.Length;
            if (Vector3.Dot(n, centre - inside) < 0) hs = hs.Reverse().ToArray();
            faces.Add(new Face(hs, colour, Material: -1));
        }
        int[] Ring(double y, double r, int n, double x = 0, double z = 0)
            => Enumerable.Range(0, n).Select(k => P(new Vector3((float)(x + r * Math.Sin(2 * Math.PI * k / n)), (float)y, (float)(z + r * Math.Cos(2 * Math.PI * k / n))))).ToArray();
        // a turned shape: rings at the profile's heights, its panels facing out from the axis; each band its colour
        void Lathe((double Y, double R)[] profile, Func<int, int> colour, int n, double x = 0, double z = 0)
        {
            var rings = profile.Select(p => p.R <= 0 ? new[] { P(new Vector3((float)x, (float)p.Y, (float)z)) } : Ring(p.Y, p.R, n, x, z)).ToArray();
            for (var j = 0; j + 1 < rings.Length; j++)
            {
                var axis = new Vector3((float)x, (float)((profile[j].Y + profile[j + 1].Y) / 2), (float)z);
                for (var k = 0; k < n; k++)
                {
                    int A(int[] ring, int i) => ring.Length == 1 ? ring[0] : ring[i % n];
                    var quad = new[] { A(rings[j], k), A(rings[j], k + 1), A(rings[j + 1], k + 1), A(rings[j + 1], k) }.Distinct().ToArray();
                    if (quad.Length >= 3) Out(quad, colour(j), axis);
                }
            }
        }
        // the drum: its side (two hoops round it), its lid and its floor -- points of their own, so the edges stay sharp
        const double R = 950, Bottom = -1500, Top = 600;
        const int Sides = 14;
        Lathe(new[] { (Bottom, R), (-950, R), (-890, R + 45), (-830, R), (-10, R), (50, R + 45), (110, R), (Top, R) },
            j => j is 1 or 2 or 4 or 5 ? HoopGrey : DrumBlue, Sides);
        var lid = Ring(Top, R, Sides);
        var middle = P(new Vector3(0, (float)Top, 0));
        for (var k = 0; k < Sides; k++) Out(new[] { middle, lid[k], lid[(k + 1) % Sides] }, Grey, new Vector3(0, (float)Top - 100, 0));
        var floor = Ring(Bottom, R, Sides);
        var under = P(new Vector3(0, (float)Bottom, 0));
        for (var k = 0; k < Sides; k++) Out(new[] { under, floor[k], floor[(k + 1) % Sides] }, Grey, new Vector3(0, (float)Bottom + 100, 0));
        // the bung, off the middle of the lid
        Lathe(new[] { (Top, 170.0), (Top + 120, 170.0), (Top + 150, 0.0) }, _ => Grey, 8, 480, 0);
        // the drop over it: dark, a little of the light on it
        light = 0.45f;
        Lathe(new[] { (1950.0, 0.0), (1720, 110), (1470, 230), (1230, 320), (1050, 360), (900, 320), (800, 200), (760, 0) }, _ => Grey, 12);
        var body = new Body { Game = 2, Lit = true };
        body.Bones.Add(new Bone(0, points.Count, 0, -1, new byte[8]));
        body.Vertices.AddRange(points);
        body.SetWorld(points.ToArray());
        body.LightScale = lights.ToArray();
        body.Faces.AddRange(faces);
        body.Validate();
        return body;
    }

    // Into the game folder: the drum appended to OBJFIX.HQR (the Gazogem's header). Its index, and a line for the log.
    public static (int Index, string Log) InstallIcon(string gameDirectory)
    {
        var path = Path.Combine(gameDirectory, "OBJFIX.HQR");
        var source = Body.Read(HqrArchive.Open(path).Read(IconSource), 2, allowStatic: true);
        var icon = BuildIcon();
        icon.Header = source.Header;
        icon.Static = source.Static;
        var index = HqrArchive.CountEntries(path);
        File.WriteAllBytes(path, HqrWriter.AppendEntry(File.ReadAllBytes(path), HqrWriter.StoredEntry(icon.Write())));
        return (index, $"the oil's drum for the item box: OBJFIX.HQR entry {index}");
    }

    // The golden Gazogem for the item box: the game's Gazogem bottle (OBJFIX.HQR entry GazogemModel, the inventory's) with every face moved
    // onto the palette's gold ramp, GoldLift steps up it (the can's own are at its dark end: dull brown at the same step), its shading kept
    // -- the roulette wheel's green zero's prize (RACEMOD.CPP
    // PU_GOLD, gold_icon=; the user, 2026-10-10: "a golden gazogem bottle"). Appended to OBJFIX.HQR; its entry, and a line for the log.
    private const int GazogemModel = 15, GoldRamp = 6, GoldLift = 4;
    private static int Gold(int colour) => GoldRamp * 16 + Math.Min(15, (colour & 15) + GoldLift);

    public static (int Index, string Log) InstallGoldIcon(string gameDirectory)
    {
        var path = Path.Combine(gameDirectory, "OBJFIX.HQR");
        var gazogem = Body.Read(HqrArchive.Open(path).Read(GazogemModel), 2, allowStatic: true);
        for (var i = 0; i < gazogem.Faces.Count; i++)
            gazogem.Faces[i] = gazogem.Faces[i] with { Colour = Gold(gazogem.Faces[i].Colour) };
        for (var i = 0; i < gazogem.Lines.Count; i++)
            gazogem.Lines[i] = gazogem.Lines[i] with { Colour = Gold(gazogem.Lines[i].Colour) };
        for (var i = 0; i < gazogem.Spheres.Count; i++)
            gazogem.Spheres[i] = gazogem.Spheres[i] with { Colour = Gold(gazogem.Spheres[i].Colour) };
        var index = HqrArchive.CountEntries(path);
        File.WriteAllBytes(path, HqrWriter.AppendEntry(File.ReadAllBytes(path), HqrWriter.StoredEntry(gazogem.Write())));
        return (index, $"the golden Gazogem for the item box: OBJFIX.HQR entry {index}");
    }

    // The game's ball of lava (OBJFIX.HQR entry LavaBallModel: the one the lava falls and bursts of Wannies Island's mine spit, its lava
    // texture alive) LavaBallScale times its size -- 130 across, a dot from a race car's camera -- for the race-track mode's lava balls
    // (RACEMOD.CPP lava_ball_model=; the user, 2026-10-10: "the lava balls that are currently coming out of the lava are different to the
    // retail ones, so let's switch these"). Appended to OBJFIX.HQR; its entry, and a line for the log.
    private const int LavaBallModel = 69;
    public const double LavaBallScale = 4;

    public static (int Index, string Log) InstallLavaBall(string gameDirectory)
    {
        var path = Path.Combine(gameDirectory, "OBJFIX.HQR");
        var clipped = 0;
        var big = IslandScaler.ScaledBody(HqrArchive.Open(path).Read(LavaBallModel), LavaBallScale, ref clipped);
        var index = HqrArchive.CountEntries(path);
        File.WriteAllBytes(path, HqrWriter.AppendEntry(File.ReadAllBytes(path), HqrWriter.StoredEntry(big)));
        return (index, $"the game's lava ball {LavaBallScale} times its size: OBJFIX.HQR entry {index}");
    }

    // The power-ups' mushroom made bigger: the game's small brown mushroom, 246 across and 307 tall -- a quarter of a car's length --
    // MushroomScale times its size, as the mushroom's entity's body BigMushroom; the lap's mushrooms and the fakes are it
    // (RaceTrackScenes). (The user, 2026-10-08: "The mushroom pick up is far too small to see" -- three times its size for a while, but
    // that was of the item box, whose fake mushroom RACEMOD.CPP now draws nearer: "make them smaller again, but a little bigger than they
    // were but not as much".)
    public const int BigMushroom = 2;
    public const double MushroomScale = 1.5;

    public static string InstallBigMushroom(string gameDirectory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var clipped = 0;
        var big = IslandScaler.ScaledBody(HqrArchive.Open(bodyPath).Read(MushroomBody), MushroomScale, ref clipped);
        var index = HqrArchive.CountEntries(bodyPath);
        File.WriteAllBytes(bodyPath, HqrWriter.AppendEntry(File.ReadAllBytes(bodyPath), HqrWriter.StoredEntry(big)));
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var table = RaceTrackBaldinoCar.WithBody(HqrArchive.Open(ressPath).Read(44), Entity, BigMushroom, index);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        return $"the power-ups' mushroom {MushroomScale} times its size: BODY.HQR entry {index}, the mushroom's entity ({Entity}) body {BigMushroom}";
    }

    // Into the game folder: the slick appended to BODY.HQR as the mushroom's entity's body 1 (the mushroom's own header: a fixed object).
    // Returns a line for the log.
    public static string Install(string gameDirectory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var slick = Build();
        slick.Header = Body.Read(HqrArchive.Open(bodyPath).Read(MushroomBody), 2, allowStatic: true).Header;
        slick.Static = true;
        var index = HqrArchive.CountEntries(bodyPath);
        File.WriteAllBytes(bodyPath, HqrWriter.AppendEntry(File.ReadAllBytes(bodyPath), HqrWriter.StoredEntry(slick.Write())));
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var table = RaceTrackBaldinoCar.WithBody(HqrArchive.Open(ressPath).Read(44), Entity, Generic, index);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        return $"the oil slick: BODY.HQR entry {index}, the mushroom's entity ({Entity}) body {Generic}";
    }
}
