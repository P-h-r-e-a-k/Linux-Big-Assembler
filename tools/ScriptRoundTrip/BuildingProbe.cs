using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// buildingprobe <pristine folder> <scratch game folder>: Citadel Island's two tracks built into the scratch folder by the build itself
// (RaceTrackService.BuildFiles: its Citadel files are put back from the pristine folder first), and, for each of its two files, every
// building the road takes away -- the island's decor pieces grouped by the origin they share, tall and a few cells across -- with how near
// that file's road comes to it, whether its surface (asphalt and curbs) runs into it or only its verge and the clearing's reach, and the
// road's height there. Never a real game folder: the scratch folder's files are rebuilt.
internal static class BuildingProbe
{
    public static int Run(string[] args)
    {
        var pristine = args[1]; var game = args[2];
        foreach (var f in new[] { "CITADEL.ILE", "CITADEL.OBL", "CITABAU.ILE", "CITABAU.OBL" })
        {
            RaceTrackService.CopyWritable(Path.Combine(pristine, f), Path.Combine(game, f));
            if (File.Exists(Path.Combine(game, f + RaceTrackService.BackupSuffix))) File.Delete(Path.Combine(game, f + RaceTrackService.BackupSuffix));
        }
        var tracks = new List<RaceTrackService.TrackBuild> { new(RaceTrackPlan.Built(RaceTrackIsland.Citadel), RaceTrackOptions.For(RaceTrackIsland.Citadel)) };
        RaceTrackService.Built? built = null;
        var plan = RaceTrackPlan.Built(RaceTrackIsland.Citadel);
        var options = RaceTrackOptions.For(RaceTrackIsland.Citadel);
        built = RaceTrackService.BuildFiles(game, Path.Combine(pristine, "CITADEL.ILE"), plan, options);
        Show("CITADEL.ILE", pristine, game, built.Report, plan.KeepBodies);
        if (built.Twin is { } twin) Show("CITABAU.ILE", pristine, game, twin.Report, RaceTrackPlan.BuiltTwin(RaceTrackIsland.Citadel)?.KeepBodies);
        foreach (var note in built.Report.Notes.Concat(built.Twin?.Report.Notes ?? new()).Where(n => n.Contains("kept") || n.Contains("moved off") || n.Contains("bridge") || n.Contains("WARNING")))
            Console.WriteLine("  note: " + note);
        return 0;
    }

    private static void Show(string ile, string pristine, string game, RaceTrackReport report, int[]? keep)
    {
        var before = IslandOps.CubeCells(IslandFile.Load(Path.Combine(pristine, ile))).SelectMany(c => c.Item3.Decors.Select(d => (c.Item1, c.Item2, D: d))).ToList();
        var after = IslandOps.CubeCells(IslandFile.Load(Path.Combine(game, ile))).SelectMany(c => c.Item3.Decors.Select(d => (c.Item1, c.Item2, d.X, d.Z, d.Body & 0xFFFF))).ToHashSet();
        var gone = before.Where(t => !after.Contains((t.Item1, t.Item2, t.D.X, t.D.Z, t.D.Body & 0xFFFF))).ToList();
        Console.WriteLine($"== {ile}: {gone.Count} decor pieces gone");
        // (the roads, for pictures: road, point, x, z, height, deck/raised)
        using (var w = new StreamWriter(Path.Combine(game, "roads_" + Path.GetFileNameWithoutExtension(ile) + ".csv")))
        {
            w.WriteLine("road,i,x,z,h,flag");
            foreach (var r in report.Roads)
                for (var i = 0; i < r.Count; i++)
                    w.WriteLine(FormattableString.Invariant($"{r.Name},{i},{r.X[i]:0.00},{r.Z[i]:0.00},{r.H[i]:0},{(r.Raised is { } up && up[i] ? "raised" : r.Deck.Length > i && r.Deck[i] ? "deck" : r.Void.Length > i && r.Void[i] ? "void" : "")}"));
        }
        // the kept buildings: how near the road's middle (and so its curbs) comes, and whether they moved
        var builtIsland = IslandFile.Load(Path.Combine(game, ile));
        var pristineIsland = IslandFile.Load(Path.Combine(pristine, ile));
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(builtIsland))
            foreach (var d in cube.Decors.Where(d => keep?.Contains(d.Body & 0xFFFF) == true))
            {
                double x0 = (cx * 32768.0 + d.XMin) / 512, x1 = (cx * 32768.0 + d.XMax) / 512, z0 = (cz * 32768.0 + d.ZMin) / 512, z1 = (cz * 32768.0 + d.ZMax) / 512;
                var was = before.FirstOrDefault(t => t.Item1 == cx && t.Item2 == cz && t.D.X == d.X && t.D.Z == d.Z && (t.D.Body & 0xFFFF) == (d.Body & 0xFFFF)).D;
                var best = (D: 1e9, H: 0.0, Curb: 0.0, Flag: "");
                foreach (var r in report.Roads)
                    for (var i = 0; i < r.Count; i++)
                    {
                        if (r.Void.Length > i && r.Void[i]) continue;
                        var dx = Math.Max(Math.Max(x0 - r.X[i], 0), r.X[i] - x1); var dz = Math.Max(Math.Max(z0 - r.Z[i], 0), r.Z[i] - z1);
                        var dd = Math.Sqrt(dx * dx + dz * dz);
                        if (dd < best.D) best = (dd, r.H[i], r.CurbHalf, r.Raised is { } up && up[i] ? " raised" : r.Deck.Length > i && r.Deck[i] ? " deck" : "");
                    }
                double groundMove = 0;
                for (var gz = (int)Math.Ceiling(z0); gz <= (int)Math.Floor(z1); gz++)
                for (var gx = (int)Math.Ceiling(x0); gx <= (int)Math.Floor(x1); gx++)
                    if (IslandOps.Altitude(builtIsland, gx * 512.0, gz * 512.0) is { } a && IslandOps.Altitude(pristineIsland, gx * 512.0, gz * 512.0) is { } b && Math.Abs(a - b) > Math.Abs(groundMove)) groundMove = a - b;
                Console.WriteLine(FormattableString.Invariant($"  kept body {d.Body & 0xFFFF} x {x0:0.0}..{x1:0.0} z {z0:0.0}..{z1:0.0} y {d.YMin}..{d.YMax}: road middle {best.D:0.0} away (curbs {best.D - best.Curb:+0.0;-0.0} clear) at {best.H:0}{best.Flag}; ")
                    + FormattableString.Invariant($"moved {(was is null ? 0 : d.YMin - was.YMin)} up; ground under it moved up to {groundMove:0}"));
            }
        foreach (var g in gone.GroupBy(t => (t.Item1, t.Item2, t.D.X, t.D.Z)).OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2))
        {
            double x0 = g.Min(t => (t.Item1 * 32768.0 + t.D.XMin) / 512), x1 = g.Max(t => (t.Item1 * 32768.0 + t.D.XMax) / 512);
            double z0 = g.Min(t => (t.Item2 * 32768.0 + t.D.ZMin) / 512), z1 = g.Max(t => (t.Item2 * 32768.0 + t.D.ZMax) / 512);
            int y0 = g.Min(t => t.D.YMin), y1 = g.Max(t => t.D.YMax);
            if (y1 - y0 < 1500 || (x1 - x0) * (z1 - z0) < 6) continue;   // (buildings: tall and a few cells across)
            var best = (D: 1e9, H: 0.0, Curb: 0.0, Raised: false, Deck: false, Name: "");
            foreach (var r in report.Roads)
                for (var i = 0; i < r.Count; i++)
                {
                    if (r.Void.Length > i && r.Void[i]) continue;
                    var dx = Math.Max(Math.Max(x0 - r.X[i], 0), r.X[i] - x1); var dz = Math.Max(Math.Max(z0 - r.Z[i], 0), r.Z[i] - z1);
                    var d = Math.Sqrt(dx * dx + dz * dz);
                    if (d < best.D) best = (d, r.H[i], r.CurbHalf, r.Raised is { } up && up[i], r.Deck.Length > i && r.Deck[i], r.Name);
                }
            var into = best.D <= best.Curb ? "SURFACE" : best.D <= best.Curb + 2 ? "verge" : "reach";
            Console.WriteLine(FormattableString.Invariant($"cube ({g.Key.Item1},{g.Key.Item2}) origin ({g.Key.Item3},{g.Key.Item4}) bodies {string.Join(",", g.Select(t => t.D.Body & 0xFFFF).Distinct())}: ")
                + FormattableString.Invariant($"x {x0:0.0}..{x1:0.0} z {z0:0.0}..{z1:0.0} y {y0}..{y1}; {best.Name} middle {best.D:0.0} cells away ({into}), road at {best.H:0}")
                + (best.Raised ? " raised" : best.Deck ? " deck" : ""));
        }
    }
}

// heroarmor <game folder> <first scene> <last scene>: Twinsen's armour in each scene (a blast whose force is no more than it does nothing:
// OBJECT.CPP HitObj), and any scene where it is 20 or more (the race-track mode's penguin blast, RACE_BLAST_FORCE).
internal static class HeroArmor
{
    public static int Run(string[] args)
    {
        var store = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, args[1]);
        var counts = new SortedDictionary<int, int>();
        for (var s = int.Parse(args[2]); s <= int.Parse(args[3]); s++)
        {
            if (!store.SceneExists(s)) continue;
            LBAAssembler.Scenes.SceneModel m;
            try { m = store.Load(s); } catch { continue; }
            var a = m.Actors[0].Armor;
            counts[a] = counts.GetValueOrDefault(a) + 1;
            if (a >= 20) Console.WriteLine($"scene {s} (island {m.Island}, mode {m.CubeMode}): Twinsen's armour {a}");
        }
        Console.WriteLine("armour: scenes -- " + string.Join(", ", counts.Select(kv => $"{kv.Key}: {kv.Value}")));
        return 0;
    }
}

// bodyfaces <OBL or HQR> <body> [x0 x1 y0 y1 z0 z1]: one body's polygons in the order they are stored (the engine's polygon blocks, a
// type each): type, points (the body's own coordinates), colour, texture handle and UVs -- only those with a point inside the box, if
// given. (Citadel Island's shop door: which polygons share its window, and in what order they come.)
internal static class BodyFaces
{
    public static int Run(string[] args)
    {
        var bytes = LBAAssembler.HqrArchive.Open(args[1]).Read(int.Parse(args[2]));
        var body = LbaBodyStudio.Body.Read(bytes, 2, allowStatic: true);
        double[]? box = args.Length >= 9 ? args.Skip(3).Take(6).Select(double.Parse).ToArray() : null;
        Console.WriteLine($"body {args[2]}: {body.Vertices.Count} points, {body.Faces.Count} polygons, textures [{string.Join(", ", body.Textures.Select(t => $"0x{t:X8}"))}]");
        var k = 0;
        foreach (var f in body.Faces)
        {
            var pts = f.Points.Select(i => body.Vertices[i]).ToArray();
            if (box is null || pts.Any(p => p.X >= box[0] && p.X <= box[1] && p.Y >= box[2] && p.Y <= box[3] && p.Z >= box[4] && p.Z <= box[5]))
                Console.WriteLine($"  #{k} type {f.Material} colour {f.Colour}: {string.Join(" ", pts.Select(p => $"({p.X:0},{p.Y:0},{p.Z:0})"))}" +
                    (f.Texture is { } t ? $" tex {t.Handle} uv [{string.Join(",", t.UV.Select(v => (v / 256.0).ToString("0.#")))}]" : ""));
            k++;
        }
        return 0;
    }
}

// decorlist <ILE> [x0 x1 z0 z1]: every decor piece of an island (or those whose box reaches into the cells given), grouped by the origin
// the pieces of a building share: cube, bodies, box in cells, heights, origin and turn.
internal static class DecorList
{
    public static int Run(string[] args)
    {
        var island = IslandFile.Load(args[1]);
        double[]? r = args.Length >= 6 ? args.Skip(2).Take(4).Select(double.Parse).ToArray() : null;
        var all = IslandOps.CubeCells(island).SelectMany(c => c.Item3.Decors.Select(d => (Cx: c.Item1, Cz: c.Item2, D: d))).ToList();
        var groups = all.GroupBy(t => (t.Cx, t.Cz, t.D.X, t.D.Z)).ToList();
        foreach (var g in groups.OrderBy(g => g.Key.Cz).ThenBy(g => g.Key.Cx).ThenBy(g => g.Key.Z).ThenBy(g => g.Key.X))
        {
            double x0 = g.Min(t => (t.Cx * 32768.0 + t.D.XMin) / 512), x1 = g.Max(t => (t.Cx * 32768.0 + t.D.XMax) / 512);
            double z0 = g.Min(t => (t.Cz * 32768.0 + t.D.ZMin) / 512), z1 = g.Max(t => (t.Cz * 32768.0 + t.D.ZMax) / 512);
            if (r is not null && (x1 < r[0] || x0 > r[1] || z1 < r[2] || z0 > r[3])) continue;
            int y0 = g.Min(t => t.D.YMin), y1 = g.Max(t => t.D.YMax);
            var d = g.First().D;
            Console.WriteLine(FormattableString.Invariant($"cube ({g.Key.Cx},{g.Key.Cz}) origin ({d.X},{d.Y},{d.Z}) turn {d.Beta & 4095} bodies {string.Join(",", g.Select(t => t.D.Body & 0xFFFF))}: cells x {x0:0.0}..{x1:0.0} z {z0:0.0}..{z1:0.0} y {y0}..{y1}"));
        }
        return 0;
    }
}

// bodyroundtrip <OBL> <body>...: each body read into the body model and written back -- same bytes, or what differs (points, polygons,
// textures, size): whether the model can carry an island's decor body through an edit.
internal static class BodyRoundTrip
{
    public static int Run(string[] args)
    {
        var hqr = LBAAssembler.HqrArchive.Open(args[1]);
        foreach (var a in args.Skip(2))
        {
            var bytes = hqr.Read(int.Parse(a));
            var body = LbaBodyStudio.Body.Read(bytes, 2, allowStatic: true);
            var back = body.Write();
            var again = LbaBodyStudio.Body.Read(back, 2, allowStatic: true);
            var same = bytes.AsSpan().SequenceEqual(back);
            Console.WriteLine($"body {a}: {bytes.Length} bytes, {body.Vertices.Count} points, {body.Faces.Count} polygons, lit {body.Lit}, static {body.Static}, textures {body.Textures.Length}; " +
                $"written {back.Length} bytes, {(same ? "IDENTICAL" : "different")}; read back {again.Vertices.Count} points, {again.Faces.Count} polygons, " +
                $"types {string.Join(",", body.Faces.GroupBy(f => f.Material).Select(g => $"{g.Key}x{g.Count()}"))} -> {string.Join(",", again.Faces.GroupBy(f => f.Material).Select(g => $"{g.Key}x{g.Count()}"))}");
            if (!same)
            {
                int first = -1;
                for (var i = 0; i < Math.Min(bytes.Length, back.Length); i++) if (bytes[i] != back[i]) { first = i; break; }
                Console.WriteLine($"  first difference at byte {first}; header words orig {string.Join(" ", Enumerable.Range(0, 24).Select(k => BitConverter.ToInt32(bytes, k * 4)))}");
                Console.WriteLine($"  header words back {string.Join(" ", Enumerable.Range(0, 24).Select(k => BitConverter.ToInt32(back, k * 4)))}");
            }
        }
        return 0;
    }
}
