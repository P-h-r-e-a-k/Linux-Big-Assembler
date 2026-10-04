using LBAAssembler.Scenes;
using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// trackleftovers <game folder> <ISLAND> <lap.csv> [first scene] [last scene]: what a built race track left near its road -- decor objects
// whose box comes within `reach` cells of the lap's centre line, decors standing well off the ground under them (left floating when the
// build reshaped it), and the same for the actors of the island's outside scenes. `lap.csv` is the centre line the build dumped
// (RT_DUMP), one "x,z" island cell a line.
internal static class TrackLeftoverProbe
{
    public static int Run(string[] args)
    {
        var game = args[1];
        var island = IslandFile.Load(Path.Combine(game, args[2].ToUpperInvariant() + ".ILE"));
        // RT_BEFORE: the island as it was, so a decor that already overhung a cliff of its own isn't reported -- only what the build left hanging
        var before = Environment.GetEnvironmentVariable("RT_BEFORE") is { Length: > 0 } bp ? IslandFile.Load(bp) : null;
        var lap = File.ReadAllLines(args[3]).Where(l => l.Contains(',')).Select(l => l.Split(','))
            .Select(p => (X: double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), Z: double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture))).ToList();
        int first = args.Length > 4 ? int.Parse(args[4]) : -1, last = args.Length > 5 ? int.Parse(args[5]) : -1;
        var reach = double.TryParse(Environment.GetEnvironmentVariable("RT_REACH"), out var r) ? r : 8.0;
        var floatBy = double.TryParse(Environment.GetEnvironmentVariable("RT_FLOAT"), out var f) ? f : 400.0;

        double ToLap(double x, double z)
        {
            var best = 1e9;
            foreach (var (lx, lz) in lap) { var d = (lx - x) * (lx - x) + (lz - z) * (lz - z); if (d < best) best = d; }
            return Math.Sqrt(best);
        }
        // the distance from the lap to a box (0 when the lap runs through it): the nearest point of the box to each lap point
        double BoxToLap(double x0, double z0, double x1, double z1)
        {
            var best = 1e9;
            foreach (var (lx, lz) in lap)
            {
                var qx = Math.Clamp(lx, x0, x1); var qz = Math.Clamp(lz, z0, z1);
                var d = (lx - qx) * (lx - qx) + (lz - qz) * (lz - qz);
                if (d < best) best = d;
            }
            return Math.Sqrt(best);
        }

        // RT_ADRIFT=1: instead, every decor whose underside (YMin: for many decors Y is 0, the body's own heights are YMin..YMax) is more than
        // RT_FLOAT above the highest ground anywhere under its footprint -- held up by nothing -- and, with RT_BEFORE, whether it was so before
        if (Environment.GetEnvironmentVariable("RT_ADRIFT") == "1")
        {
            double? Highest(IslandFile isl, double ox, double oz, IslandDecor d)
            {
                double? best = null;
                for (var z = d.ZMin; z <= d.ZMax; z += 128)
                for (var x = d.XMin; x <= d.XMax; x += 128)
                    if (IslandOps.Altitude(isl, ox + x, oz + z) is { } g && (best is null || g > best)) best = g;
                return best;
            }
            var pristine = before is null ? null : IslandOps.CubeCells(before).ToDictionary(c => (c.Item1, c.Item2), c => c.Item3);
            Console.WriteLine($"decors held up by nothing (underside more than {floatBy:0} above the highest ground under them):");
            foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
                for (var k = 0; k < cube.Decors.Count; k++)
                {
                    var d = cube.Decors[k];
                    double ox = cx * (double)IslandFile.CubeSize, oz = cz * (double)IslandFile.CubeSize;
                    if (Highest(island, ox, oz, d) is not { } top || d.YMin - top <= floatBy) continue;
                    string was = "";
                    if (pristine is not null && pristine.TryGetValue((cx, cz), out var old))
                    {
                        var same = old.Decors.FirstOrDefault(o => o.Body == d.Body && o.XMin == d.XMin && o.ZMin == d.ZMin);
                        was = same is null ? ", not in the original (moved or added)"
                            : Highest(before!, ox, oz, same) is { } otop && same.YMin - otop > floatBy ? $", was adrift before too (underside {same.YMin}, ground {otop:0})"
                            : $", NOT before: it stood at {same.YMin} on ground up to {Highest(before!, ox, oz, same):0}";
                    }
                    var near = BoxToLap((ox + d.XMin) / 512, (oz + d.ZMin) / 512, (ox + d.XMax) / 512, (oz + d.ZMax) / 512);
                    Console.WriteLine($"  body {d.Body & 0xFFFF,3} cube ({cx},{cz}) decor {k} at cell ({(ox + (d.XMin + d.XMax) / 2.0) / 512:0.0}, {(oz + (d.ZMin + d.ZMax) / 2.0) / 512:0.0}): " +
                                      $"underside {d.YMin}, top {d.YMax}, highest ground under it {top:0} ({d.YMin - top:+0}), box {(d.XMax - d.XMin) / 512.0:0.0} x {(d.ZMax - d.ZMin) / 512.0:0.0} cells, {near:0.0} cells from the lap{was}");
                }
            return 0;
        }
        Console.WriteLine($"lap of {lap.Count} points; decors within {reach:0.#} cells of it, and decors more than {floatBy:0} off the ground:");
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
        foreach (var d in cube.Decors)
        {
            double ox = cx * (double)IslandFile.CubeSize, oz = cz * (double)IslandFile.CubeSize;
            double x0 = (ox + d.XMin) / 512, x1 = (ox + d.XMax) / 512, z0 = (oz + d.ZMin) / 512, z1 = (oz + d.ZMax) / 512;
            var near = BoxToLap(x0, z0, x1, z1);
            var ground = IslandOps.Altitude(island, ox + d.X, oz + d.Z);
            var above = ground is null ? 0 : d.Y - ground.Value;
            // the lowest ground anywhere under the object: where the build cut the hillside away beside it, the object's own cell can
            // still be on the ground while the rest of it hangs over the cut
            double under = double.MaxValue, wasUnder = double.MaxValue;
            // the share of the object's footprint with air under it: ground more than a step below its base, now and before the build
            int samples = 0, air = 0, wasAir = 0;
            var wasAtOriginEarly = before is null ? null : IslandOps.Altitude(before, ox + d.X, oz + d.Z);
            var moved = before is null || wasAtOriginEarly is null || ground is null ? 0 : ground.Value - wasAtOriginEarly.Value;
            for (var sz = z0; sz <= z1 + 0.001; sz += 0.5)
            for (var sx = x0; sx <= x1 + 0.001; sx += 0.5)
            {
                samples++;
                if (IslandOps.Altitude(island, sx * 512, sz * 512) is { } g) { under = Math.Min(under, g); if (g < d.Y - 250) air++; }
                if (before is not null && IslandOps.Altitude(before, sx * 512, sz * 512) is { } h) { wasUnder = Math.Min(wasUnder, h); if (h < d.Y - moved - 250) wasAir++; }
            }
            var overhang = under == double.MaxValue ? 0 : d.Y - under;
            // How much of that overhang the build made. The decor moved with the ground under its own origin (IslandOps.DecorFollow),
            // so where it stood before is its height now less that move; the overhang it had then is that, over the lowest ground the
            // island had under its box.
            var wasAtOrigin = before is null ? null : IslandOps.Altitude(before, ox + d.X, oz + d.Z);
            var added = before is null || wasUnder == double.MaxValue || wasAtOrigin is null || ground is null
                ? 0 : overhang - (d.Y - (ground.Value - wasAtOrigin.Value) - wasUnder);
            if (near > reach && Math.Abs(above) <= floatBy && (before is null ? overhang : added) <= floatBy) continue;
            Console.WriteLine($"  body {d.Body & 0xFFFF,3} cube ({cx},{cz}) at cell ({(ox + d.X) / 512:0.0}, {(oz + d.Z) / 512:0.0}) y {d.Y}" +
                              $" (ground {ground?.ToString("0") ?? "none"}, {above:+0;-0;0}; lowest under it {(under == double.MaxValue ? "none" : under.ToString("0"))}, {overhang:+0;-0;0}{(before is null ? "" : $", {added:+0;-0;0} of that the build's")}; air under {air * 100.0 / Math.Max(1, samples):0}% of it{(before is null ? "" : $", was {wasAir * 100.0 / Math.Max(1, samples):0}%")}), box {x1 - x0:0.0} x {z1 - z0:0.0} cells, {near:0.0} cells from the lap" +
                              (near <= reach ? "  ON THE ROAD" : "") + (Math.Abs(above) > floatBy ? (above > 0 ? "  FLOATING" : "  SUNK") : (before is null ? overhang : added) > floatBy ? "  LEFT HANGING" : ""));
        }

        if (first < 0) return 0;
        var store = new SceneStore(SceneGame.Lba2, game);
        Console.WriteLine($"actors of scenes {first}..{last}:");
        for (var scene = first; scene <= last; scene++)
        {
            if (!store.SceneExists(scene)) continue;
            SceneModel m;
            try { m = store.Load(scene); } catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException) { continue; }
            if (m.CubeMode != 1) continue;
            double ox = m.CubeX * (double)IslandFile.CubeSize, oz = m.CubeY * (double)IslandFile.CubeSize;
            for (var i = 0; i < m.Actors.Count; i++)
            {
                var a = m.Actors[i];
                if (a.Y < -10000) continue;                  // parked out of sight (an opponent's car in another scene)
                var near = ToLap((ox + a.X) / 512, (oz + a.Z) / 512);
                var ground = IslandOps.Altitude(island, ox + a.X, oz + a.Z);
                var above = ground is null ? 0 : a.Y - ground.Value;
                if (near > reach && Math.Abs(above) <= floatBy) continue;
                Console.WriteLine($"  scene {scene} actor {i,2} entity {a.Entity,4} body {a.Body,3} flags 0x{a.Flags:X4}{((a.Flags & 0x0800) != 0 ? " fallable" : "")} at cell ({(ox + a.X) / 512:0.0}, {(oz + a.Z) / 512:0.0}) y {a.Y}" +
                                  $" (ground {ground?.ToString("0") ?? "none"}, {above:+0;-0;0}), {near:0.0} cells from the lap" +
                                  (near <= reach ? "  ON THE ROAD" : "") + (Math.Abs(above) > floatBy ? (above > 0 ? "  FLOATING" : "  SUNK") : ""));
            }
        }
        return 0;
    }
}
