using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Text.Json;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// LBA1's digger, the tractor Twinsen drives on Polar Island (the user, 2026-10-27: "Let's pull the digger vehicle from LBA1 into LBA2, this
// vehicle is featured on Polar Island and Twinsen rides in it, let's give it the same stats as Twinsen's buggy, but make the acceleration
// slightly worse"; "This car should then be used in Polar Island in story mode instead of Twinsen's car"): LBA1's BODY.HQR entry 130
// ("Bulldozer", the Tractor entity 80's only body) taken whole, as it stands, onto the racer's hull bone (2) -- the racer's animations bounce
// it as they do every car; it has no wheels to turn (its tracks don't) -- its colours each moved to the LBA2 palette's ramp nearest its own
// (bank to bank: a lit polygon's colour is its ramp's start), and Twinsen sat in its cab (bone 13). It goes in after every track's bodies,
// as the pumpkin F1 does (InstallDigger), as race car DiggerNumber of RACECARS.JSON.
internal static partial class RaceTrackCharacterCars
{
    // race car 225 (after the pumpkin's 220, the toy truck's 221 and the monsters' 222-224), the racer entity's body 69 (its half-size copy 169)
    public const int DiggerNumber = 225, DiggerGeneric = 69, Lba1DiggerBody = 130;
    public const string DiggerName = "The digger";
    public static readonly Car Digger = new(DiggerName, "Twinsen", TwinsenBody, DiggerGeneric, (t, r) => throw new InvalidOperationException("The digger needs LBA1."), "Polar Island");

    // LBA1's palette onto LBA2's: each LBA1 colour the same step of the LBA2 ramp (16 colours) nearest its own ramp, its steps' colours
    // taken together -- so a lit polygon runs the light up a ramp of the same hue; ramps 1-14 only (0 the greys the game keeps, 15 the
    // fire colours each island's palette makes its own).
    public static int[] Lba1ToLba2Colours(byte[] lba1Palette, byte[] lba2Palette)
    {
        var map = new int[256];
        for (var bank = 0; bank < 16; bank++)
        {
            int best = 1; double bestD = double.MaxValue;
            for (var b2 = 1; b2 < 15; b2++)
            {
                double d = 0;
                for (var k = 0; k < 16; k++)
                {
                    int i1 = (bank * 16 + k) * 3, i2 = (b2 * 16 + k) * 3;
                    double dr = lba1Palette[i1] - lba2Palette[i2], dg = lba1Palette[i1 + 1] - lba2Palette[i2 + 1], db = lba1Palette[i1 + 2] - lba2Palette[i2 + 2];
                    d += 3 * dr * dr + 4 * dg * dg + 2 * db * db;
                }
                if (d < bestD) { bestD = d; best = b2; }
            }
            for (var k = 0; k < 16; k++) map[bank * 16 + k] = best * 16 + k;
        }
        return map;
    }

    // The digger as an LBA2 race car: `lba1` LBA1's body 130, its palette and LBA2's; Twinsen's LBA2 body; the racer's car (its header).
    public static Body BuildDigger(Body lba1, byte[] lba1Palette, byte[] lba2Palette, Body twinsen, Body racer, bool driver = true)
    {
        var colours = Lba1ToLba2Colours(lba1Palette, lba2Palette);
        var m = new CarMesh(DiggerName);
        Roots(m);
        // ---- LBA1's digger as it stands, on the hull: its blade, its front, at +z as a car's (LBA1 and LBA2 face the same way)
        var world = lba1.World();
        var handle = world.Select(v => m.P(2, v)).ToArray();
        foreach (var f in lba1.Faces)
        {
            // (LBA1's types: 7-10 lit, flat or Gouraud; 5 and 6 see-through; the rest unlit)
            var material = f.Material >= 7 ? -1 : f.Material is 5 or 6 ? CarMesh.SeeThrough : 0;
            var hs = f.Points.Select(i => handle[i]).ToArray();
            if (hs.Length is 3 or 4) m.Raw(hs, colours[f.Colour & 255], material);
            else
                // (a polygon of more corners: a fan of triangles)
                for (var k = 1; k + 1 < hs.Length; k++) m.Raw(new[] { hs[0], hs[k], hs[k + 1] }, colours[f.Colour & 255], material);
        }
        foreach (var l in lba1.Lines) m.Line(handle[l.A], handle[l.B], colours[l.Colour & 255]);
        foreach (var s in lba1.Spheres) m.Sphere(handle[s.Point], s.Radius, colours[s.Colour & 255]);
        // ---- the wheels' bones: a point each, where the racer's wheels would be (nothing of the digger's turns)
        var parents = new[] { 0, 0, 0, 2, 3, 4, 2, 6, 7, 2, 9, 2, 11 };
        for (var b = 3; b <= 12; b++)
        {
            var at = new Vector3(b is >= 3 and <= 8 ? 300 : -300, 150, b is >= 3 and <= 8 ? 400 : -400);
            m.Pivot(b, m.P(parents[b], at));
            m.P(b, at);
        }
        // ---- Twinsen in the cab (bone 13), as the pumpkin's driver sits: his arms and legs left out, the arms' bones a point each
        var seat = DiggerSeat(world);
        m.Pivot(13, m.P(2, seat));
        if (driver)
        {
            m.Light = 1;
            const float Scale = 0.75f;
            var origin = new Vector3(0, 600, 33);
            Vector3 Place(Vector3 p) => (p - origin) * Scale + seat;
            var d = new CarDriver(twinsen);
            var point = d.Seat(m, d.AllBut(new[] { 4, 6, 8, 11 }), Place, seat.Y + 120, Scale, cut: true);
            Arms(m, point(twinsen.Bones[4].Pivot), point(twinsen.Bones[6].Pivot));
        }
        else
        {
            m.P(13, seat);
            for (var b = 14; b <= 17; b++) { m.Pivot(b, m.P(b == 16 ? 13 : b - 1, seat)); m.P(b, seat); }
        }
        var body = m.ToBody(racer.Header);
        var all = body.World();
        var box = new[] { all.Min(v => v.X), all.Max(v => v.X), all.Min(v => v.Y), all.Max(v => v.Y), all.Min(v => v.Z), all.Max(v => v.Z) };
        for (var i = 0; i < 6; i++) BinaryPrimitives.WriteInt32LittleEndian(body.Header.AsSpan(8 + i * 4), (int)MathF.Round(box[i]));
        return body;
    }

    // Where Twinsen sits: the cab's floor, behind its sloping windscreen (the cab is 800-1427 up, x within 300, its roof z -75..225);
    // DiggerSeatAt over it for trials (ScriptRoundTrip digger ... x,y,z)
    public static Vector3? DiggerSeatAt = null;
    private static Vector3 DiggerSeat(Vector3[] world) => DiggerSeatAt ?? new Vector3(0, 760, 60);
}

internal static partial class RaceTrackCharacterCars
{
    // Adds the digger to a game folder whose race tracks are built (at the end of the build, as the pumpkin F1: RaceTrackService.Build):
    // its body and its half-size copy at the end of BODY.HQR, the racer's entity's bodies DiggerGeneric and its half-size one, and its line
    // in RACECARS.JSON. `lba1Directory`: LBA1's game folder (its BODY.HQR and RESS.HQR); none there, no digger. Lines for the build's log.
    public static List<string> InstallDigger(string gameDirectory, string? lba1Directory)
    {
        if (string.IsNullOrEmpty(lba1Directory) || !File.Exists(Path.Combine(lba1Directory, "BODY.HQR")) || !File.Exists(Path.Combine(lba1Directory, "RESS.HQR")))
            return new() { $"{DiggerName}: not made -- no LBA1 folder to take it from" };
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var bodies = HqrArchive.Open(bodyPath);
        Body car;
        byte[] empty;
        try
        {
            var lba1 = Body.Read(HqrArchive.Open(Path.Combine(lba1Directory, "BODY.HQR")).Read(Lba1DiggerBody), 1);
            car = BuildDigger(lba1, HqrArchive.Open(Path.Combine(lba1Directory, "RESS.HQR")).Read(0), HqrArchive.Open(ressPath).Read(0),
                              Body.Read(bodies.Read(TwinsenBody), 2), Body.Read(bodies.Read(RaceTrackBaldinoCar.RacerBody), 2));
            empty = EmptyDigger(lba1Directory, bodies, ressPath);
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException or IOException) { return new() { $"{DiggerName}: not made -- {e.Message}" }; }
        var bytes = car.Write();
        var file = File.ReadAllBytes(bodyPath);
        var own = HqrArchive.CountEntries(bodyPath);
        file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(bytes));
        file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(RaceTrackSmallCars.Halved(bytes)));
        file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(empty));
        var small = own + 1;
        var parked = own + 2;
        File.WriteAllBytes(bodyPath, file);
        var table = HqrArchive.Open(ressPath).Read(44);
        table = RaceTrackBaldinoCar.WithBody(table, RaceTrackBaldinoCar.RacerEntity, DiggerGeneric, own);
        table = RaceTrackBaldinoCar.WithBody(table, RaceTrackBaldinoCar.RacerEntity, RaceTrackSmallCars.SmallOf(DiggerGeneric), small);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        var catalogue = Catalogue(gameDirectory).Where(e => e.Number != DiggerNumber).ToList();
        catalogue.Add(new CatalogueEntry(DiggerNumber, DiggerName, Digger.Driver, Digger.Island, RaceTrackBaldinoCar.RacerEntity, DiggerGeneric, own, small));
        File.WriteAllText(Path.Combine(gameDirectory, CatalogueFile), JsonSerializer.Serialize(catalogue, new JsonSerializerOptions { WriteIndented = true }));
        // (and Polar Island's car outside a race: RACEMOD.CPP ISLANDCARS.TXT)
        if (RacerDrive(gameDirectory) is { } drive) SetIslandCar(gameDirectory, PolarIslandNumber, own, drive, DiggerStoryAccel, parked);
        return new()
        {
            $"{DiggerName} (race car {DiggerNumber}, LBA1's Polar Island tractor, body {Lba1DiggerBody}): BODY.HQR entry {own} ({car.Faces.Count} polygons, " +
            $"{car.Vertices.Count} points), its half-size copy {small}, parked with nobody in it {parked}; the racer's entity ({RaceTrackBaldinoCar.RacerEntity}) bodies {DiggerGeneric} and {RaceTrackSmallCars.SmallOf(DiggerGeneric)}",
        };
    }
}

internal static partial class RaceTrackCharacterCars
{
    public const string IslandCarsFile = "ISLANDCARS.TXT";
    public const int PolarIslandNumber = 12;
    // the digger's acceleration outside a race, a share of the buggy's: "slightly worse"
    public const int DiggerStoryAccel = 85;

    // The island's own car outside a race (RACEMOD.CPP ISLANDCARS.TXT: "island_car=<island> <body> <animation> <acceleration %> [<parked
    // body>]" -- the parked one with nobody in it, drawn while it stands, in a race too): the line for `island` put in (or taken out,
    // body < 0), the others kept.
    public static void SetIslandCar(string gameDirectory, int island, int body, int anim, int accel, int parked = -1)
    {
        var path = Path.Combine(gameDirectory, IslandCarsFile);
        var lines = File.Exists(path) ? File.ReadAllLines(path).Where(l => !l.StartsWith($"island_car={island} ")).ToList() : new List<string>();
        if (lines.Count == 0) lines.Add("# LBA Assembler: each island's own car outside a race -- island, BODY.HQR body, ANIM.HQR animation, acceleration (% of the buggy's)");
        if (body >= 0) lines.Add($"island_car={island} {body} {anim} {accel}" + (parked >= 0 ? $" {parked}" : ""));
        if (lines.All(l => l.StartsWith('#'))) { if (File.Exists(path)) File.Delete(path); return; }
        File.WriteAllLines(path, lines);
    }

    // The digger with nobody in it, written: parked, him on foot beside it (ISLANDCARS.TXT's parked body)
    private static byte[] EmptyDigger(string lba1Directory, HqrArchive bodies, string ressPath)
    {
        var lba1 = Body.Read(HqrArchive.Open(Path.Combine(lba1Directory, "BODY.HQR")).Read(Lba1DiggerBody), 1);
        return BuildDigger(lba1, HqrArchive.Open(Path.Combine(lba1Directory, "RESS.HQR")).Read(0), HqrArchive.Open(ressPath).Read(0),
                           Body.Read(bodies.Read(TwinsenBody), 2), Body.Read(bodies.Read(RaceTrackBaldinoCar.RacerBody), 2), driver: false).Write();
    }

    // The racer's driving animation (the racer entity's generic 1) in the folder's ANIM.HQR, or null
    internal static int? RacerDrive(string gameDirectory) =>
        Lba2EntityTable.Load(gameDirectory)?.Entities.FirstOrDefault(e => e.Id == RaceTrackScenes.RacerEntity)?.Anims.Where(a => a.Generic == 1).Select(a => (int?)a.Anim).FirstOrDefault();

    // Polar Island in a story game (Tools > Polar Island: PolarIsland.Add): the digger in BODY.HQR -- once: an ISLANDCARS.TXT line already
    // naming an entry that is the digger as this makes it keeps it -- and Polar Island's line naming it.
    public static List<string> InstallPolarDigger(string gameDirectory, string lba1Directory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var anim = RacerDrive(gameDirectory);
        if (anim is null) return new() { $"{DiggerName}: not put on Polar Island -- the folder has no racer's driving animation" };
        byte[] bytes, empty;
        try
        {
            var bodies = HqrArchive.Open(bodyPath);
            empty = EmptyDigger(lba1Directory, bodies, ressPath);
            var lba1 = Body.Read(HqrArchive.Open(Path.Combine(lba1Directory, "BODY.HQR")).Read(Lba1DiggerBody), 1);
            bytes = BuildDigger(lba1, HqrArchive.Open(Path.Combine(lba1Directory, "RESS.HQR")).Read(0), HqrArchive.Open(ressPath).Read(0),
                                Body.Read(bodies.Read(TwinsenBody), 2), Body.Read(bodies.Read(RaceTrackBaldinoCar.RacerBody), 2)).Write();
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException or IOException) { return new() { $"{DiggerName}: not made -- {e.Message}" }; }
        // (already there, as the file says)
        var listed = File.Exists(Path.Combine(gameDirectory, IslandCarsFile))
            ? File.ReadAllLines(Path.Combine(gameDirectory, IslandCarsFile)).Where(l => l.StartsWith($"island_car={PolarIslandNumber} ")).Select(l => int.TryParse(l.Split(' ')[1], out var b) ? b : -1).FirstOrDefault(-1)
            : -1;
        var count = HqrArchive.CountEntries(bodyPath);
        if (listed >= 0 && listed < count)
        {
            try
            {
                var store = HqrArchive.Open(bodyPath);
                if (store.Read(listed).AsSpan().SequenceEqual(bytes) && listed + 1 < count && store.Read(listed + 1).AsSpan().SequenceEqual(empty))
                {
                    SetIslandCar(gameDirectory, PolarIslandNumber, listed, anim.Value, DiggerStoryAccel, listed + 1);
                    return new() { $"{DiggerName}: Polar Island's car already (BODY.HQR entry {listed}, parked {listed + 1})" };
                }
            }
            catch (InvalidDataException) { }
        }
        var file = HqrWriter.AppendEntry(File.ReadAllBytes(bodyPath), HqrWriter.StoredEntry(bytes));
        File.WriteAllBytes(bodyPath, HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(empty)));
        SetIslandCar(gameDirectory, PolarIslandNumber, count, anim.Value, DiggerStoryAccel, count + 1);
        return new() { $"{DiggerName}: Twinsen's car on Polar Island outside a race (BODY.HQR entry {count}, {IslandCarsFile}: {DiggerStoryAccel}% of the buggy's acceleration)" };
    }
}
