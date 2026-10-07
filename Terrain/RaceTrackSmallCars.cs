using System.Buffers.Binary;
using System.IO;

namespace LBAAssembler.Terrain;

// The race cars shrunk to half their size, for the power-ups' lightning spell (RACEMOD.CPP: the other cars shrunk for 20 seconds). The
// engine can't scale a body as it draws it, so every body of the racer's entity (157: the retail racer, Baldino's rocket car, the cars
// made after the characters) gets a copy at half its size as another of its bodies, First + its own, which the race-track mode switches the
// car to (opponent_small=). The copy is the body's own bytes with every point, sphere and the bounding box halved: the bones' places are
// their pivot points, so the whole car comes out half the size, its polygons, normals, colours and textures as they were.
internal static class RaceTrackSmallCars
{
    public const int First = 100;
    // Twinsen's own car (2026-10-04): the buggy behaviour's entity (C_BUGGY, 12) -- its body while he drives, GEN_BODY_TUNIQUE (1) -- gets
    // the same, its generic body HeroSmall; the race-track mode switches him to it when an opponent's lightning has him shrunk
    // (twinsen_small=).
    public const int HeroEntity = 12, HeroBody = 1, HeroSmall = First + HeroBody;

    public static int SmallOf(int body) => First + body;

    // Returns a line for the log.
    public static string Install(string gameDirectory)
    {
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var table = HqrArchive.Open(ressPath).Read(44);
        var bodies = HqrArchive.Open(bodyPath);
        var own = BodiesOf(table, RaceTrackBaldinoCar.RacerEntity).Where(b => b.Generic < First).ToList();
        var file = File.ReadAllBytes(bodyPath);
        var next = HqrArchive.CountEntries(bodyPath);
        foreach (var (generic, index) in own)
        {
            file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(Halved(bodies.Read(index))));
            table = RaceTrackBaldinoCar.WithBody(table, RaceTrackBaldinoCar.RacerEntity, SmallOf(generic), next++);
        }
        var hero = BodiesOf(table, HeroEntity).Where(b => b.Generic == HeroBody).Select(b => (int?)b.Index).FirstOrDefault();
        if (hero is { } heroIndex)
        {
            file = HqrWriter.AppendEntry(file, HqrWriter.StoredEntry(Halved(bodies.Read(heroIndex))));
            table = RaceTrackBaldinoCar.WithBody(table, HeroEntity, HeroSmall, next++);
        }
        File.WriteAllBytes(bodyPath, file);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        return $"the race cars at half their size (the lightning spell): {own.Count} bodies of the racer's entity ({RaceTrackBaldinoCar.RacerEntity}), its bodies {First}-{First + own.Max(o => o.Generic)}" +
               (hero is not null ? $"; Twinsen's buggy too, entity {HeroEntity}'s body {HeroSmall}" : "; Twinsen's buggy's body not found");
    }

    // An entity's bodies in the entity table (RESS.HQR 44): its generic body numbers and their BODY.HQR entries.
    private static List<(int Generic, int Index)> BodiesOf(byte[] table, int entity)
    {
        var list = new List<(int, int)>();
        var p = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(entity * 4));
        var end = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan((entity + 1) * 4));
        while (p < end && table[p] != 255)
        {
            var command = table[p];
            if (command == 1) list.Add((table[p + 1], BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(p + 3))));
            p += command == 3 ? 3 + table[p + 3] : 2 + table[p + 2];
        }
        return list;
    }

    // An LBA2 body at half its size: its bounding box (header, 8-31), its points (count at 40, offset at 44: x, y, z and a bone, 8 bytes
    // each) and its spheres' radii (count at 80, offset at 84: radius at 6 of 8 bytes).
    public static byte[] Halved(byte[] body)
    {
        var b = (byte[])body.Clone();
        int I(int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
        void Half16(int at) => BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(at), (short)(BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at)) / 2));
        for (var at = 8; at < 32; at += 4) BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), I(at) / 2);
        int points = I(40), p = I(44);
        for (var i = 0; i < points; i++, p += 8) { Half16(p); Half16(p + 2); Half16(p + 4); }
        int spheres = I(80), q = I(84);
        for (var i = 0; i < spheres; i++, q += 8) BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(q + 6), (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(q + 6)) / 2));
        return b;
    }

    // The same at any size (the super jet-pack the car turns into: RaceTrackSuperJet).
    public static byte[] Scaled(byte[] body, double factor)
    {
        var b = (byte[])body.Clone();
        int I(int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
        void Scale16(int at) => BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(at), (short)Math.Round(BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at)) * factor));
        for (var at = 8; at < 32; at += 4) BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), (int)Math.Round(I(at) * factor));
        int points = I(40), p = I(44);
        for (var i = 0; i < points; i++, p += 8) { Scale16(p); Scale16(p + 2); Scale16(p + 4); }
        int spheres = I(80), q = I(84);
        for (var i = 0; i < spheres; i++, q += 8) BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(q + 6), (ushort)Math.Round(BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(q + 6)) * factor));
        return b;
    }
}
