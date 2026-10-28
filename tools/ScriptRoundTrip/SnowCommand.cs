using LBAAssembler.Terrain.Snow;

namespace ScriptRoundTrip;

// The snow island (Terrain/Snow): its design looked at, built, installed.
internal static class SnowCommand
{
    // snowisland <LBA1 folder> <LBA2 folder> <out.lbamap> [<game folder to install into: its MAPS>]: the island built, saved as a map, and put
    // in a game folder's MAPS folder (a sandbox's), its scenes' numbers said
    public static int Island(string[] args)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        // (SNOW_LOOK: another island's look, SNOW_WHITER: the snow nearer white -- trying them)
        if (Environment.GetEnvironmentVariable("SNOW_LOOK") is { Length: > 0 } look) SnowTerrain.LookIsland = look.ToUpperInvariant();
        if (Environment.GetEnvironmentVariable("SNOW_WHITER") is { Length: > 0 } whiter) SnowTerrain.SnowWhiter = double.Parse(whiter, System.Globalization.CultureInfo.InvariantCulture);
        var built = SnowIsland.Build(args[1], args[2]);
        foreach (var line in built.Log) Console.WriteLine(line);
        built.Map.Save(args[3]);
        Console.WriteLine($"{args[3]}: {new FileInfo(args[3]).Length / 1024} KB, built in {watch.Elapsed.TotalSeconds:0.0} s");
        if (args.Length > 4)
        {
            var (path, scenes) = LBAAssembler.Maps.MapInstall.InstallInMapsFolder(built.Map, args[4], SnowIsland.FileName);
            Console.WriteLine($"{path}: scenes {string.Join(", ", scenes)}; Twinsen starts in scene {scenes[built.StartScene]}");
        }
        return 0;
    }

    // snowwalk <LBA1 folder> <LBA2 folder> [out.png]: where Twinsen can walk from the start -- over the built island's cells that are
    // neither walls (the triangles' collision bit: steeper than 40 degrees) nor water, the pier's cells too -- and whether the island's
    // places are among them; a picture of it (reached: white, land not reached: red, walls: dark)
    public static int Walk(string[] args)
    {
        var built = SnowIsland.Build(args[1], args[2]);
        var island = LBAAssembler.Terrain.IslandFile.Parse(built.Map.Ile!, "SNOWISLE.ILE");
        bool Free(int u, int v)
        {
            if (u < 0 || v < 0 || u >= SnowDesign.Width || v >= SnowDesign.Depth) return false;
            if (Math.Abs(u + 0.5 - SnowDesign.PierU) < SnowDesign.PierHalf && v >= SnowDesign.PierFromV && v < SnowDesign.PierToV) return true;
            int gx = SnowDesign.Gx(u), gz = SnowDesign.Gz(v);
            if (island.CubeAt(gx / 64, gz / 64) is not { } cube) return false;
            for (var half = 0; half < 2; half++)
            {
                var p = new LBAAssembler.Terrain.IslandPolygon(cube.Polygon(gx % 64, gz % 64, half));
                if (p.Col || p.CodeJeu == 1) return false;
            }
            return true;
        }
        var reached = new bool[SnowDesign.Width, SnowDesign.Depth];
        var queue = new Queue<(int, int)>();
        var (su, sv) = ((int)SnowDesign.Start.U, (int)SnowDesign.Start.V);
        reached[su, sv] = true; queue.Enqueue((su, sv));
        while (queue.Count > 0)
        {
            var (u, v) = queue.Dequeue();
            foreach (var (du, dv) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nu = u + du, nv = v + dv;
                if (nu < 0 || nv < 0 || nu >= SnowDesign.Width || nv >= SnowDesign.Depth || reached[nu, nv] || !Free(nu, nv)) continue;
                reached[nu, nv] = true; queue.Enqueue((nu, nv));
            }
        }
        var places = new (string Name, double U, double V)[]
        {
            ("the village's plaza", SnowDesign.Plaza.U, SnowDesign.Plaza.V), ("the pond's shore", 60, 190), ("the pond's ice", 40, 168),
            ("the canyon's foot", 109, 240), ("the canyon's head", 109, 62), ("the prison gate", 109, 45), ("the north plateau's east", 150, 30),
            ("the ski slope's foot", SnowDesign.PisteFoot.U, SnowDesign.PisteFoot.V - 4), ("the ski slope's top", SnowDesign.PisteTop.U, SnowDesign.PisteTop.V + 4),
            ("the summit plateau", 200, 80), ("the massif's first tier", 175, 196), ("the massif's second tier", 170, 140), ("the lift's foot", SnowDesign.LiftFoot.U - 4, SnowDesign.LiftFoot.V),
        };
        foreach (var (name, u, v) in places)
        {
            // (the place or a cell within 2 of it)
            var ok = false;
            for (var dv = -2; dv <= 2 && !ok; dv++) for (var du = -2; du <= 2 && !ok; du++)
            {
                int x = (int)u + du, z = (int)v + dv;
                ok = x >= 0 && z >= 0 && x < SnowDesign.Width && z < SnowDesign.Depth && reached[x, z];
            }
            Console.WriteLine($"{(ok ? "reached    " : "NOT REACHED")} {name} ({u:0},{v:0})");
        }
        int land = 0, got = 0;
        for (var v = 0; v < SnowDesign.Depth; v++) for (var u = 0; u < SnowDesign.Width; u++) if (Free(u, v)) { land++; if (reached[u, v]) got++; }
        Console.WriteLine($"{got} of {land} walkable cells reached ({100.0 * got / land:0.0} %)");
        if (args.Length > 3)
        {
            const int k = 3;
            var px = new byte[SnowDesign.Width * k * SnowDesign.Depth * k * 4];
            for (var y = 0; y < SnowDesign.Depth * k; y++)
                for (var x = 0; x < SnowDesign.Width * k; x++)
                {
                    int u = x / k, v = y / k;
                    (byte r, byte g, byte b) c = reached[u, v] ? ((byte)235, (byte)240, (byte)245) : Free(u, v) ? ((byte)220, (byte)40, (byte)40) : SnowDesign.IsLand(u, v, out _) ? ((byte)60, (byte)70, (byte)80) : ((byte)30, (byte)90, (byte)140);
                    var i = (y * SnowDesign.Width * k + x) * 4;
                    px[i] = c.b; px[i + 1] = c.g; px[i + 2] = c.r; px[i + 3] = 255;
                }
            PngWriter.Write(args[3], px, SnowDesign.Width * k, SnowDesign.Depth * k);
        }
        return 0;
    }

    // snowdesign <out.png> [scale]: the design from above -- the height shaded (lit from the north-west), each kind of ground its colour,
    // the sea blue, the cubes' edges marked
    public static int Design(string[] args)
    {
        var scale = args.Length > 2 ? int.Parse(args[2]) : 3;
        int w = SnowDesign.Width * scale, h = SnowDesign.Depth * scale;
        var heights = new double[SnowDesign.Width + 1, SnowDesign.Depth + 1];
        for (var v = 0; v <= SnowDesign.Depth; v++) for (var u = 0; u <= SnowDesign.Width; u++) heights[u, v] = SnowDesign.Height(u, v);
        var px = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                int u = x / scale, v = y / scale;
                var c = (heights[u, v] + heights[u + 1, v] + heights[u, v + 1] + heights[u + 1, v + 1]) / 4;
                var kind = SnowDesign.KindAt(u + 0.5, v + 0.5, c);
                var rise = new[] { heights[u, v], heights[u + 1, v], heights[u, v + 1], heights[u + 1, v + 1] };
                var cliff = rise.Max() - rise.Min() >= 1024;
                (double r, double g, double b) col = kind switch
                {
                    SnowDesign.Kind.Sea => (40, 150, 170),
                    SnowDesign.Kind.Pond => (90, 200, 210),
                    SnowDesign.Kind.Gravel => (128, 126, 126),
                    SnowDesign.Kind.Earth => (79, 63, 55),
                    SnowDesign.Kind.Piste => (240, 248, 255),
                    SnowDesign.Kind.Rock => (85, 72, 78),
                    _ => (180, 222, 228),
                };
                if (cliff && kind != SnowDesign.Kind.Sea) col = (120, 160, 170);
                // (lit from the north-west, and darker low down)
                var dx = heights[u + 1, v] + heights[u + 1, v + 1] - heights[u, v] - heights[u, v + 1];
                var dz = heights[u, v + 1] + heights[u + 1, v + 1] - heights[u, v] - heights[u + 1, v];
                var shade = Math.Clamp(1 - (dx + dz) / 6000.0, 0.45, 1.3) * (0.75 + 0.25 * Math.Clamp(c / 10000.0, 0, 1));
                if (kind == SnowDesign.Kind.Sea) shade = 1;
                if ((u % 64 == 0 && x % scale == 0) || (v % 64 == 0 && y % scale == 0)) { col = (255, 0, 0); shade = 1; }
                var i = (y * w + x) * 4;
                px[i] = (byte)Math.Clamp(col.b * shade, 0, 255); px[i + 1] = (byte)Math.Clamp(col.g * shade, 0, 255); px[i + 2] = (byte)Math.Clamp(col.r * shade, 0, 255); px[i + 3] = 255;
            }
        PngWriter.Write(args[1], px, w, h);
        var land = 0; var max = double.MinValue;
        for (var v = 0; v <= SnowDesign.Depth; v++) for (var u = 0; u <= SnowDesign.Width; u++) { if (heights[u, v] > 0) land++; max = Math.Max(max, heights[u, v]); }
        Console.WriteLine($"{args[1]}: {land} vertices of land, highest {max:0}");
        return 0;
    }
}
