using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// leftovers <before.ILE> <after.ILE> [touch cells]: decors the race track build left standing whose boxes touch a decor it removed -- the
// rest of a building whose other pieces were on the road. Lists each with the removed piece it touches.
internal static class LeftoverProbe
{
    public static int Run(string[] args)
    {
        var before = IslandFile.Load(args[1]); var after = IslandFile.Load(args[2]);
        var touch = args.Length > 3 ? double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 0.5;
        (double X0, double Z0, double X1, double Z1, double Y0, double Y1) Box(int cx, int cz, IslandDecor d) =>
            ((cx * 64.0 * 512 + d.XMin) / 512, (cz * 64.0 * 512 + d.ZMin) / 512, (cx * 64.0 * 512 + d.XMax) / 512, (cz * 64.0 * 512 + d.ZMax) / 512, d.YMin, d.YMax);
        var removed = new List<(int Cx, int Cz, IslandDecor D, (double X0, double Z0, double X1, double Z1, double Y0, double Y1) B)>();
        var kept = new List<(int Cx, int Cz, IslandDecor D, (double X0, double Z0, double X1, double Z1, double Y0, double Y1) B)>();
        foreach (var (cx, cz, cb) in IslandOps.CubeCells(before))
        {
            var ca = after.CubeAt(cx, cz);
            foreach (var d in cb.Decors)
            {
                // (the build moves a decor up or down with the ground it stands on, so only its place on the map identifies it)
                var still = ca is not null && ca.Decors.Any(e => e.Body == d.Body && e.X == d.X && e.Z == d.Z);
                (still ? kept : removed).Add((cx, cz, d, Box(cx, cz, d)));
            }
        }
        Console.WriteLine($"{removed.Count} decors removed, {kept.Count} kept");
        if (Environment.GetEnvironmentVariable("RUINLIST") == "1")
            foreach (var r in removed) Console.WriteLine($"  removed body {r.D.Body & 0xFFFF} cube ({r.Cx},{r.Cz}) cells ({r.B.X0:0.0},{r.B.Z0:0.0})-({r.B.X1:0.0},{r.B.Z1:0.0}) height {r.B.Y0}..{r.B.Y1}");
        var seen = new HashSet<(int, int, int, int)>();
        foreach (var k in kept)
        {
            var near = removed.Where(r => k.B.X0 <= r.B.X1 + touch && r.B.X0 <= k.B.X1 + touch && k.B.Z0 <= r.B.Z1 + touch && r.B.Z0 <= k.B.Z1 + touch).ToList();
            if (near.Count == 0) continue;
            var id = (k.D.Body & 0xFFFF, (int)(k.B.X0 * 10), (int)(k.B.Z0 * 10), (int)k.B.Y0);
            if (!seen.Add(id)) continue;           // (a decor spanning two cubes is stored in both)
            Console.WriteLine($"kept body {k.D.Body & 0xFFFF} cube ({k.Cx},{k.Cz}) cells ({k.B.X0:0.0},{k.B.Z0:0.0})-({k.B.X1:0.0},{k.B.Z1:0.0}) = {k.B.X1 - k.B.X0:0.0} x {k.B.Z1 - k.B.Z0:0.0}, height {k.B.Y0}..{k.B.Y1}; touches removed body {string.Join(",", near.Select(r => r.D.Body & 0xFFFF).Distinct())}");
        }
        return 0;
    }
}
