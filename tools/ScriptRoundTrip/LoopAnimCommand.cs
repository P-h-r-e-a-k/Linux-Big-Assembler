using System.Buffers.Binary;
using LBAAssembler;
using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// loopanim <game folder> <generic> <radius cells> <drift cells> <keyframes> <ms each> <sign>: (2026-10-01, can the car drive a loop?) a
// vertical loop for Twinsen in his buggy, added to the folder's ANIM.HQR and given to his buggy entity (RESS.HQR 44, entity 12) as generic
// animation <generic> -- in a race-track build, a jump's number (RaceTrackJumpAnim.GenericFor), so its take-off flies it instead.
// The engine turns each keyframe's step by the object's whole orientation, pitch too (LIB386/ANIM/INTERDEP.CPP: the master rotation,
// slot 0's Alpha, is added to the object's own, and the step rotated by all three angles), so a steady pitch and a step along the car's
// own forward carry it round a circle in the vertical plane, upside down at the top. <drift>: how far it ends up ahead of where it began
// (a loop over a gap, landing beyond it); each keyframe's step is that keyframe's piece of the circle plus its share of the drift, turned
// into the car's own frame at the keyframe's middle pitch. <sign>: 1 or -1, which way Alpha turns the nose up.
internal static class LoopAnimCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 8) { Console.WriteLine("loopanim <game folder> <generic> <radius cells> <drift cells> <keyframes> <ms each> <sign>"); return 1; }
        var game = args[1]; var generic = int.Parse(args[2]);
        double radius = double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) * 512, drift = double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) * 512;
        int frames = int.Parse(args[5]), ms = int.Parse(args[6]), sign = int.Parse(args[7]);
        var animPath = Path.Combine(game, "ANIM.HQR");
        var retail = HqrArchive.Open(animPath).Read(RaceTrackJumpAnim.RetailEntry);
        int bones = BinaryPrimitives.ReadUInt16LittleEndian(retail.AsSpan(2));
        var frameSize = 8 + bones * 8;
        // the pose (slots 1..) of the retail flight's second keyframe, every keyframe
        var pose = retail.AsSpan(8 + frameSize + 16, (bones - 1) * 8).ToArray();
        var anim = new byte[8 + frames * frameSize];
        BinaryPrimitives.WriteUInt16LittleEndian(anim, (ushort)frames);
        BinaryPrimitives.WriteUInt16LittleEndian(anim.AsSpan(2), (ushort)bones);
        BinaryPrimitives.WriteUInt16LittleEndian(anim.AsSpan(4), (ushort)(frames - 1));
        var dAlpha = 4096.0 / frames;
        for (var f = 0; f < frames; f++)
        {
            double t0 = 2 * Math.PI * f / frames, t1 = 2 * Math.PI * (f + 1) / frames, tm = (t0 + t1) / 2;
            // the world step in the loop's plane: forward and up
            double fw = radius * (Math.Sin(t1) - Math.Sin(t0)) + drift / frames, up = radius * (Math.Cos(t0) - Math.Cos(t1));
            // into the car's own frame, pitched tm nose up
            double localFw = fw * Math.Cos(tm) + up * Math.Sin(tm), localUp = -fw * Math.Sin(tm) + up * Math.Cos(tm);
            var p = 8 + f * frameSize;
            BinaryPrimitives.WriteUInt16LittleEndian(anim.AsSpan(p), (ushort)ms);
            BinaryPrimitives.WriteInt16LittleEndian(anim.AsSpan(p + 2), 0);
            BinaryPrimitives.WriteInt16LittleEndian(anim.AsSpan(p + 4), (short)Math.Round(localUp));
            BinaryPrimitives.WriteInt16LittleEndian(anim.AsSpan(p + 6), (short)Math.Round(localFw));
            // slot 0: the master bits (1 the rotation is the whole object's, 2 no gravity) and the pitch
            BinaryPrimitives.WriteInt16LittleEndian(anim.AsSpan(p + 8), 3);
            BinaryPrimitives.WriteInt16LittleEndian(anim.AsSpan(p + 10), (short)Math.Round(sign * dAlpha));
            pose.CopyTo(anim.AsSpan(p + 16));
        }
        var index = HqrArchive.CountEntries(animPath);
        File.WriteAllBytes(animPath, HqrWriter.AppendEntry(File.ReadAllBytes(animPath), HqrWriter.StoredEntry(anim)));
        var ressPath = Path.Combine(game, "RESS.HQR");
        var table = RaceTrackJumpAnim.WithAnim(HqrArchive.Open(ressPath).Read(44), RaceTrackJumpAnim.BuggyEntity, generic, index);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        Console.WriteLine($"loop: ANIM.HQR entry {index}, {frames} keyframes of {ms} ms, radius {radius / 512:0.0} cells, drift {drift / 512:0.0}, generic {generic}");
        return 0;
    }
}
