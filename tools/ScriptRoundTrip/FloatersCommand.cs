using LBAAssembler;
using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// floaters <ILE> [gap] [strict]: the decors hanging in the air -- nothing under them: the ground at their footprint's middle and corners more than
// `gap` (300) below their bottom, and no other decor whose footprint overlaps theirs reaching up to within `gap` of it
internal static class FloatersCommand
{
    // floaters <ILE> strip <out ILE> <cx> <cz> [x0 x1 z0 z1]: a copy without that cube's decors (those whose box's middle is in the island
    // cells x0..x1, z0..z1) -- to find which a picture shows
    public static int Strip(string[] args)
    {
        var island = IslandFile.Load(args[1]);
        int cx = int.Parse(args[3]), cz = int.Parse(args[4]);
        // (or "body n": that body's decors)
        if (args.Length > 6 && args[5] == "body")
        {
            var only = int.Parse(args[6]);
            var m = island.CubeAt(cx, cz)!.Decors.RemoveAll(d => (d.Body & 0xFFFF) == only);
            island.Save(args[2]);
            Console.WriteLine($"{m} decors taken out");
            return 0;
        }
        var range = args.Length > 8 ? args[5..9].Select(double.Parse).ToArray() : new[] { -1e9, 1e9, -1e9, 1e9 };
        var cube = island.CubeAt(cx, cz)!;
        var n = cube.Decors.RemoveAll(d => (cx * 32768.0 + (d.XMin + d.XMax) / 2.0) / 512 is var x && x >= range[0] && x <= range[1] &&
                                           (cz * 32768.0 + (d.ZMin + d.ZMax) / 2.0) / 512 is var z && z >= range[2] && z <= range[3]);
        island.Save(args[2]);
        Console.WriteLine($"{n} decors taken out");
        return 0;
    }

    public static int Run(string[] args)
    {
        if (args.Length > 2 && args[2] == "strip") return Strip(args.Take(2).Concat(args.Skip(3)).ToArray());
        var island = IslandFile.Load(args[1]);
        var gap = args.Length > 2 ? int.Parse(args[2]) : 300;
        // (strict: held only from below -- resting on what is held -- not by what is beside it)
        var strict = args.Contains("strict");
        // (near x z r: every decor within r cells of island cell (x, z), held or not)
        var near = Array.IndexOf(args, "near") is var ni and > 0 ? (X: double.Parse(args[ni + 1]), Z: double.Parse(args[ni + 2]), R: double.Parse(args[ni + 3])) : default;
        var all = IslandOps.CubeCells(island).SelectMany(c => c.Item3.Decors.Select(d => (Cx: c.Item1, Cz: c.Item2, D: d))).ToList();
        (double X0, double Z0, double X1, double Z1) Box((int Cx, int Cz, IslandDecor D) t) =>
            (t.Cx * 32768.0 + t.D.XMin, t.Cz * 32768.0 + t.D.ZMin, t.Cx * 32768.0 + t.D.XMax, t.Cz * 32768.0 + t.D.ZMax);
        // Held up: standing on the ground, a raised road's deck (its box's top the "no box" mark), the rocky peak's layers (over 14,000
        // up), or touching -- resting on, or fixed beside within a quarter of a cell -- something held up; spread till nothing changes
        var boxes = all.Select(Box).ToList();
        var held = new bool[all.Count];
        var grounds = new double[all.Count];
        for (var i = 0; i < all.Count; i++)
        {
            var (b, d) = (boxes[i], all[i].D);
            var points = new[] { ((b.X0 + b.X1) / 2, (b.Z0 + b.Z1) / 2), (b.X0 + 64, b.Z0 + 64), (b.X1 - 64, b.Z0 + 64), (b.X0 + 64, b.Z1 - 64), (b.X1 - 64, b.Z1 - 64) };
            grounds[i] = points.Select(p => IslandOps.Altitude(island, p.Item1, p.Item2) ?? -99999).Max();
            held[i] = grounds[i] >= d.YMin - gap || d.YMax < d.YMin || d.YMin > 14000;
        }
        bool Touch(int i, int j)
        {
            var (a, b) = (boxes[i], boxes[j]);
            var (da, db) = (all[i].D, all[j].D);
            if (strict && !(db.YMax >= da.YMin - gap && db.YMin < da.YMin && a.X0 < b.X1 && b.X0 < a.X1 && a.Z0 < b.Z1 && b.Z0 < a.Z1)) return false;
            return da.YMin <= db.YMax + gap && db.YMin <= da.YMax + gap &&
                   a.X0 <= b.X1 + 128 && b.X0 <= a.X1 + 128 && a.Z0 <= b.Z1 + 128 && b.Z0 <= a.Z1 + 128;
        }
        for (var changed = true; changed;)
        {
            changed = false;
            for (var i = 0; i < all.Count; i++)
                if (!held[i])
                    for (var j = 0; j < all.Count; j++)
                        if (held[j] && j != i && Touch(i, j)) { held[i] = changed = true; break; }
        }
        var found = 0;
        for (var i = 0; i < all.Count; i++)
        {
            if (near.R > 0 ? Math.Max(Math.Abs((boxes[i].X0 + boxes[i].X1) / 1024 - near.X), Math.Abs((boxes[i].Z0 + boxes[i].Z1) / 1024 - near.Z)) > near.R : held[i]) continue;
            var (t, b) = (all[i], boxes[i]);
            found++;
            Console.WriteLine(FormattableString.Invariant($"{i} cube ({t.Cx},{t.Cz}) body {t.D.Body & 0xFFFF}: cells x {b.X0 / 512:0.0}..{b.X1 / 512:0.0} z {b.Z0 / 512:0.0}..{b.Z1 / 512:0.0} y {t.D.YMin}..{t.D.YMax}, the ground {grounds[i]:0} -- {t.D.YMin - grounds[i]:0} in the air"));
        }
        Console.WriteLine($"{found} of {all.Count} decors with nothing under them");
        return 0;
    }
}
