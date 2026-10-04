using LBAAssembler;
using LBAAssembler.Terrain;
using LbaBodyStudio;

namespace ScriptRoundTrip;

// gloves <game folder> <out.hqr> [scale] [flat]: the race track mod's racing gloves, written as a one-entry HQR (to render or inspect), with the
// header of OBJFIX.HQR entry 7 (the inventory model they replace).
internal static class GlovesCommand
{
    public static int Run(string[] args)
    {
        var scale = args.Length > 3 ? float.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : RaceTrackGloves.InventoryScale;
        var gloves = RaceTrackGloves.Build(scale, upright: args.Length <= 4 || args[4] != "flat");
        gloves.Static = true;
        gloves.Header = Body.Read(HqrArchive.Open(Path.Combine(args[1], "OBJFIX.HQR")).Read(7), 2, allowStatic: true).Header;
        var hqr = HqrWriter.AppendEntry(new byte[] { 4, 0, 0, 0 }, HqrWriter.StoredEntry(gloves.Write()));
        File.WriteAllBytes(args[2], hqr);
        Console.WriteLine($"{args[2]}: {gloves.Faces.Count} polygons, {gloves.Vertices.Count} points");
        return 0;
    }
}
