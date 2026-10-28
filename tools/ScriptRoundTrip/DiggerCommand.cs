using System.Numerics;
using LBAAssembler.Terrain;
using LBAAssembler;
using LbaBodyStudio;

namespace ScriptRoundTrip;

// digger <LBA1 folder> <LBA2 folder> <out.hqr> [nodriver] [seat x,y,z]: LBA1's digger made an LBA2 race car (RaceTrackCharacterCars.BuildDigger)
// -- LBA1's body 130, LBA2's Twinsen (1) and racer (227) -- written alone into <out.hqr> (BodyPipeline carviews draws it).
internal static class DiggerCommand
{
    public static int Run(string[] args)
    {
        string lba1Dir = args[1], lba2Dir = args[2], output = args[3];
        // (lba1look: any LBA1 body as it stands, its colours on LBA2's palette, for a look at it: digger <lba1> <lba2> <out.hqr> look <body>)
        if (args.Length > 5 && args[4] == "look")
        {
            var body = Body.Read(HqrArchive.Open(Path.Combine(lba1Dir, "BODY.HQR")).Read(int.Parse(args[5])), 1);
            var m2 = RaceTrackCharacterCars.Lba1ToLba2Colours(HqrArchive.Open(Path.Combine(lba1Dir, "RESS.HQR")).Read(0), HqrArchive.Open(Path.Combine(lba2Dir, "RESS.HQR")).Read(0));
            var copy = new Body { Game = 2, Lit = true, Header = new byte[96] };
            copy.Vertices.AddRange(body.World());
            copy.Bones.Add(new Bone(0, body.Vertices.Count, 0, -1, new byte[8]));
            copy.SetWorld(copy.Vertices.ToArray());
            foreach (var f in body.Faces) if (f.Points.Length is 3 or 4) copy.Faces.Add(new Face(f.Points, m2[f.Colour & 255], Material: -1));
            foreach (var l in body.Lines) copy.Lines.Add(new BodyLine(l.A, l.B, m2[l.Colour & 255]));
            foreach (var sp in body.Spheres) copy.Spheres.Add(new BodySphere(sp.Point, sp.Radius, m2[sp.Colour & 255]));
            File.WriteAllBytes(output, HqrWriter.AppendEntry(new byte[] { 4, 0, 0, 0 }, HqrWriter.StoredEntry(copy.Write())));
            Console.WriteLine($"LBA1 body {args[5]} -> {output}");
            return 0;
        }
        var noDriver = args.Skip(4).Contains("nodriver");
        var seat = args.Skip(4).FirstOrDefault(a => a.Contains(','));
        if (seat != null) { var v = seat.Split(',').Select(float.Parse).ToArray(); RaceTrackCharacterCars.DiggerSeatAt = new Vector3(v[0], v[1], v[2]); }
        var lba1 = Body.Read(HqrArchive.Open(Path.Combine(lba1Dir, "BODY.HQR")).Read(RaceTrackCharacterCars.Lba1DiggerBody), 1);
        var lba2 = HqrArchive.Open(Path.Combine(lba2Dir, "BODY.HQR"));
        byte[] pal1 = HqrArchive.Open(Path.Combine(lba1Dir, "RESS.HQR")).Read(0), pal2 = HqrArchive.Open(Path.Combine(lba2Dir, "RESS.HQR")).Read(0);
        if (args.Skip(4).Contains("points"))
            foreach (var g in lba1.World().Select((v, i) => (v, i)).GroupBy(t => (int)(t.v.Y / 200)).OrderBy(g => g.Key))
                Console.WriteLine($"  y {g.Key * 200}-{g.Key * 200 + 199}: x {g.Min(t => t.v.X):0}..{g.Max(t => t.v.X):0} z {g.Min(t => t.v.Z):0}..{g.Max(t => t.v.Z):0} ({g.Count()} points)");
        var map = RaceTrackCharacterCars.Lba1ToLba2Colours(pal1, pal2);
        foreach (var g in lba1.Faces.GroupBy(f => (f.Colour & 255, f.Material)).OrderByDescending(g => g.Count()))
        {
            int c = g.Key.Item1, c2 = map[c];
            Console.WriteLine($"  LBA1 colour {c} (material {g.Key.Material}) x{g.Count()}: rgb {pal1[c * 3]},{pal1[c * 3 + 1]},{pal1[c * 3 + 2]} -> LBA2 {c2}: rgb {pal2[c2 * 3]},{pal2[c2 * 3 + 1]},{pal2[c2 * 3 + 2]}");
        }
        var car = RaceTrackCharacterCars.BuildDigger(lba1, pal1, pal2, Body.Read(lba2.Read(1), 2), Body.Read(lba2.Read(227), 2), !noDriver);
        File.WriteAllBytes(output, HqrWriter.AppendEntry(new byte[] { 4, 0, 0, 0 }, HqrWriter.StoredEntry(car.Write())));
        var w = car.World();
        Console.WriteLine($"digger: {car.Faces.Count} polygons, {car.Vertices.Count} points, x {w.Min(p => p.X):0}..{w.Max(p => p.X):0} y {w.Min(p => p.Y):0}..{w.Max(p => p.Y):0} z {w.Min(p => p.Z):0}..{w.Max(p => p.Z):0} -> {output}");
        return 0;
    }
}
