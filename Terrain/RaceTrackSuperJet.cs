using System.Buffers.Binary;
using System.IO;

namespace LBAAssembler.Terrain;

// What Twinsen's car turns into while the super jet-pack drives it (RACEMOD.CPP, superjet_model=): the game's own super jet-pack -- the
// inventory's model, OBJFIX.HQR 48, the protopack's second look -- at about the race car's size. The engine can't scale a body as it draws
// it, so the build appends a copy of the model at Scale (RaceTrackSmallCars.Scaled: its points, spheres and box) to OBJFIX.HQR; the
// race-track mode draws it in the car's place, lying flat along the car's way (LaidFlat), while the jet-pack lasts.
internal static class RaceTrackSuperJet
{
    public const int Source = 48;
    // (the model is 3,000 wide and 2,800 tall, the racer's car 1,270 wide and 900 tall)
    public const double Scale = 0.45;

    // Into the game folder: the copy appended to OBJFIX.HQR. Its index, and a line for the log.
    public static (int Index, string Log) Install(string gameDirectory)
    {
        var path = Path.Combine(gameDirectory, "OBJFIX.HQR");
        var model = LaidFlat(RaceTrackSmallCars.Scaled(HqrArchive.Open(path).Read(Source), Scale));
        var index = HqrArchive.CountEntries(path);
        File.WriteAllBytes(path, HqrWriter.AppendEntry(File.ReadAllBytes(path), HqrWriter.StoredEntry(model)));
        return (index, $"the super jet-pack the car turns into: OBJFIX.HQR entry {index} (entry {Source} at {Scale:0.##} of its size, laid flat)");
    }

    // The model laid flat, its top forward (the way a car's heading points, +z) and its exhausts trailing: every point and normal turned a
    // quarter turn about x -- (x, y, z) to (x, -z, y) -- and its box with them. Leaning it forward a quarter turn as the engine draws it
    // instead (CarPose) is the decomposition's singular case: the heading folded into the other angles, and at some headings the jet-pack
    // flew backwards.
    public static byte[] LaidFlat(byte[] body)
    {
        var b = (byte[])body.Clone();
        int I(int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
        void Turn(int at)
        {
            var y = BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at + 2)); var z = BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(at + 4));
            BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(at + 2), (short)-z);
            BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(at + 4), y);
        }
        int ymin = I(16), ymax = I(20), zmin = I(24), zmax = I(28);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(16), -zmax); BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(20), -zmin);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(24), ymin); BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(28), ymax);
        for (int i = 0, p = I(44); i < I(40); i++, p += 8) Turn(p);
        for (int i = 0, p = I(52); i < I(48); i++, p += 8) Turn(p);
        return b;
    }
}
