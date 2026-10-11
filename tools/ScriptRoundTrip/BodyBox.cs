namespace ScriptRoundTrip;

// bodybox <BODY.HQR> <entry>... [--bones]: each body's extent in its own frame (its points posed at rest: min and max x, y, z), its bones and
// points; with --bones, each bone's parent, pivot (at rest), points and their extent.
internal static class BodyBox
{
    public static int Run(string[] args)
    {
        var hqr = LBAAssembler.HqrArchive.Open(args[1]);
        var bones = args.Contains("--bones");
        foreach (var arg in args.Skip(2).Where(a => a != "--bones"))
        {
            var body = LbaBodyStudio.Body.Read(hqr.Read(int.Parse(arg)), 2, allowStatic: true);
            var w = body.World();
            Console.WriteLine(FormattableString.Invariant($"body {arg}: {body.Bones.Count} bones, {w.Length} points, x {w.Min(p => p.X):0}..{w.Max(p => p.X):0} y {w.Min(p => p.Y):0}..{w.Max(p => p.Y):0} z {w.Min(p => p.Z):0}..{w.Max(p => p.Z):0}"));
            if (!bones) continue;
            for (var i = 0; i < body.Bones.Count; i++)
            {
                var b = body.Bones[i];
                var pivot = b.Parent < 0 ? System.Numerics.Vector3.Zero : w[b.Pivot];
                var own = Enumerable.Range(b.Start, b.Count).Select(p => w[p]).ToArray();
                // (the colours of the faces with a point of this bone's: colour x count)
                var colours = body.Faces.Where(f => f.Points.Any(p => p >= b.Start && p < b.Start + b.Count)).GroupBy(f => f.Colour).OrderByDescending(g => g.Count())
                    .Select(g => $"{g.Key}x{g.Count()}");
                Console.WriteLine(FormattableString.Invariant($"  bone {i}: parent {b.Parent}, pivot ({pivot.X:0}, {pivot.Y:0}, {pivot.Z:0}), {b.Count} points") +
                    (own.Length > 0 ? FormattableString.Invariant($" y {own.Min(p => p.Y):0}..{own.Max(p => p.Y):0} z {own.Min(p => p.Z):0}..{own.Max(p => p.Z):0}") : "") +
                    $" faces {string.Join(" ", colours)}");
            }
        }
        return 0;
    }
}
