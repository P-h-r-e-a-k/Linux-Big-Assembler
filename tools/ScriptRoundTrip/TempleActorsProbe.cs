using LBAAssembler;
using LBAAssembler.Lba1;
using LBAAssembler.Lba1.Runtime;
using LBAAssembler.Terrain.Polar;
using LBAAssembler.Terrain.Temple;

namespace ScriptRoundTrip;

// PROBE (2026-10-18): the Temple of Bú's LBA1 actors, for their port as decor (Terrain/Temple/TempleActors.cs).
//   templeactors info <LBA1>: each actor of scenes 8/40/41 -- its sprite (size, hot spot, offset, volume) or its entity's body and animation,
//     and its state in LBA1's own simulation a few frames after the scene starts
internal static class TempleActorsProbe
{
    public static int Run(string[] args) => args[1] switch
    {
        "info" => Info(args[2], args.Length > 3 ? int.Parse(args[3]) : 3),
        "layer" => Layer(args[2], int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6]), args[7]),
        "models" => Models(args[2]),
        "body" => BodyDump(args[2], int.Parse(args[3])),
        "corridor" => Corridor(args[2], int.Parse(args[3]), int.Parse(args[4])),
        _ => throw new ArgumentException("templeactors info <LBA1> [frames] | layer <LBA1> <W> <H> <OX> <OY> <out prefix> | models <LBA1>"),
    };

    //   templeactors models <LBA1>: each actor's model as TempleActors makes it -- its box, faces, the share of its picture its box holds,
    //     its place on the island and how near the race's road it comes
    private static int Models(string lba1)
    {
        var game = new Lba1Game(lba1);
        var layout = TempleLayout.Build(game);
        var frame = new TempleFrame(layout.MinY);
        var log = new List<string>();
        var actors = TempleActors.Read(lba1, game, layout, log);
        foreach (var l in log) Console.WriteLine(l);
        var data = new Lba1RuntimeData(lba1);
        var map = Enumerable.Range(0, 256).ToArray();
        var plan = LBAAssembler.Terrain.RaceTrackPlan.Built(LBAAssembler.Terrain.RaceTrackIsland.Temple);
        foreach (var a in actors)
        {
            TempleActors.Model? m; string extra = "";
            if (a.IsSprite)
            {
                var s = TempleActors.ReadSprite(lba1, data, a.Sprite)!;
                var (box, plate, held) = TempleActors.BoxOf(a, s);
                extra = $"box x {box.X0}..{box.X1} y {box.Y0}..{box.Y1} z {box.Z0}..{box.Z1}{(plate ? " plate" : "")} holds {100 * held:0.0}% clip {(a.Clip is { } c ? string.Join(",", c) : "-")}";
                m = TempleActors.SpriteModel(a, s, map);
            }
            else
            {
                var b = LbaBodyStudio.Body.Read(game.ReadBody(a.Body)!, 1);
                m = TempleActors.BodyModel(a, b, map);
                extra = $"body {a.Body} beta {a.Beta}";
            }
            var placed = layout.Placements.First(p => p.Scene == a.Scene);
            var (wx, wy, wz) = frame.FromLba1(placed, a.X, a.Y, a.Z);
            var at = new System.Numerics.Vector3((float)wx, (float)wy, (float)wz) + TempleActors.PictureShift * TempleFrame.Scale;
            var faces = m?.Faces.Count ?? 0;
            string road = "-";
            if (m is not null)
            {
                var (lo, hi) = m.Box();
                road = TempleActors.NearRoad(plan, at + lo * TempleFrame.Scale, at + hi * TempleFrame.Scale) ?? "clear";
                // (and how near, without the margin: the nearest road point in xz)
                var c = (at + (lo + hi) / 2 * TempleFrame.Scale) / 512;
                var best = plan.Points.Select((p, i) => (D: Math.Sqrt((p[0] - c.X) * (p[0] - c.X) + (p[1] - c.Z) * (p[1] - c.Z)), I: i)).MinBy(t => t.D);
                road += $" | nearest road point {best.I} at {best.D:0.0} cells, its deck {plan.Heights![best.I]:0} vs the actor {at.Y + lo.Y * 3:0}..{at.Y + hi.Y * 3:0}";
                // (the box's nearest to the road's middle line, and that point's deck half-width)
                var wlo = at + lo * 3; var whi = at + hi * 3;
                var near = plan.Points.Select((p, i) => (D: Math.Sqrt(Math.Pow(Math.Max(0, Math.Max(wlo.X / 512 - p[0], p[0] - whi.X / 512)), 2) + Math.Pow(Math.Max(0, Math.Max(wlo.Z / 512 - p[1], p[1] - whi.Z / 512)), 2)), I: i)).MinBy(t => t.D);
                road += $" | box {wlo.X / 512:0.0}..{whi.X / 512:0.0} x {wlo.Z / 512:0.0}..{whi.Z / 512:0.0}: nearest the middle line {near.D:0.00} cells (point {near.I}, deck half {plan.RaisedHalfs![near.I]:0.0}, deck {plan.Heights[near.I]:0})";
            }
            Console.WriteLine($"{a.Name,-6} {a.Kind,-14} cell ({at.X / 512:0.0}, {at.Z / 512:0.0}) y {at.Y:0} faces {faces,4} {extra} | race: {road}");
        }
        return 0;
    }

    //   templeactors body <LBA1> <BODY.HQR entry>: an LBA1 body's points, faces (material, colour, corners) and lines
    private static int BodyDump(string lba1, int index)
    {
        var b = LbaBodyStudio.Body.Read(new Lba1Game(lba1).ReadBody(index)!, 1);
        for (var i = 0; i < b.Vertices.Count; i++) Console.WriteLine($"p{i} {b.Vertices[i]}");
        foreach (var f in b.Faces) Console.WriteLine($"face mat {f.Material} colour {f.Colour} points {string.Join(",", f.Points)} normal {f.FaceNormal} pn {(f.PointNormals is null ? "-" : string.Join(",", f.PointNormals))}");
        foreach (var l in b.Lines) Console.WriteLine($"line {l.A}-{l.B} colour {l.Colour}");
        foreach (var s in b.Spheres) Console.WriteLine($"sphere {s.Point} r {s.Radius} colour {s.Colour}");
        return 0;
    }

    //   templeactors corridor <game dir> <first body> <last body>: the built race island's decors of those OBL bodies (the actors'), each body
    //     point put in the world -- how many lie in the road's corridor as the audit has it (its deck's width and slip lane, 64 to 2600 over
    //     the deck), and the nearest any comes to it
    private static int Corridor(string gameDir, int first, int last)
    {
        var island = LBAAssembler.Terrain.IslandFile.Load(Path.Combine(gameDir, "TEMPLE.ILE"));
        var obl = HqrArchive.Open(Path.Combine(gameDir, "TEMPLE.OBL"));
        var plan = LBAAssembler.Terrain.RaceTrackPlan.Built(LBAAssembler.Terrain.RaceTrackIsland.Temple);
        var pts = plan.Points; var hs = plan.Heights!;
        var bodies = new Dictionary<int, System.Numerics.Vector3[]>();
        int decors = 0, inside = 0, total = 0;
        var worst = (Over: double.MaxValue, What: "");
        var seen = new HashSet<(int, int, int, int)>();
        foreach (var (cx, cz, cube) in LBAAssembler.Terrain.IslandOps.CubeCells(island))
            foreach (var d in cube.Decors)
            {
                var b = d.Body & 0xFFFF;
                if (b < first || b > last) continue;
                double wx = cx * 32768.0 + d.X, wz = cz * 32768.0 + d.Z;
                if (!seen.Add((b, (int)wx, d.Y, (int)wz))) continue;
                decors++;
                if (!bodies.TryGetValue(b, out var v)) bodies[b] = v = LbaBodyStudio.Body.Read(obl.Read(b), 2, allowStatic: true).Vertices.ToArray();
                foreach (var p in v)
                {
                    total++;
                    double px = (wx + p.X) / 512, py = d.Y + p.Y, pz = (wz + p.Z) / 512;
                    for (var i = 0; i < pts.Length; i++)
                    {
                        double dx0 = px - pts[i][0], dz0 = pz - pts[i][1];
                        if (dx0 * dx0 + dz0 * dz0 > 200) continue;
                        int j = Math.Min(i + 1, pts.Length - 1), k = Math.Max(0, j - 1);
                        double ddx = pts[j][0] - pts[k][0], ddz = pts[j][1] - pts[k][1], len = Math.Sqrt(ddx * ddx + ddz * ddz);
                        if (len < 1e-9) continue;
                        ddx /= len; ddz /= len;
                        double along = dx0 * ddx + dz0 * ddz, across = -dx0 * ddz + dz0 * ddx;
                        if (Math.Abs(along) > 0.25) continue;
                        var half = plan.RaisedHalfs is { } halfs && i < halfs.Length ? halfs[i] : plan.RaisedHalf ?? 4.5;
                        double lo = -half, hi = half;
                        if (plan.RaisedStrips is { } strips && i < strips.Length && strips[i] is { Length: 2 } st) { lo = Math.Min(lo, st[0]); hi = Math.Max(hi, st[1]); }
                        if (across < lo || across > hi) continue;
                        var over = py - hs[i];
                        if (over >= 64 && over <= 2600) { inside++; break; }
                        if (over > 0 && over < worst.Over) worst = (over, $"body {b} point over road point {i} by {over:0}");
                    }
                }
            }
        Console.WriteLine($"{decors} actor decors, {total} body points: {inside} in the road's corridor (64..2600 over its deck, its width and slip lane); " +
                          $"the lowest point over the deck elsewhere: {(worst.What == "" ? "none" : worst.What)}");
        return 0;
    }

    //   templeactors layer <LBA1> <W> <H> <OX> <OY> <out prefix>: LBA1's actors as its engine draws them over the temple's picture (the audit's
    //     auditregion picture: W x H, the anchor of the layout's cell (0, 0, 0) at (OX, OY)) -- <out>.idx (int16 LBA1 colour, -1 none) and
    //     <out>.depth (float32, x + z + 0.8 y of the joined LBA1 world: larger nearer)
    private static int Layer(string lba1, int W, int H, int OX, int OY, string outPrefix)
    {
        var game = new Lba1Game(lba1);
        var layout = TempleLayout.Build(game);
        var log = new List<string>();
        var actors = TempleActors.Read(lba1, game, layout, log);
        foreach (var l in log) Console.WriteLine(l);
        var data = new Lba1RuntimeData(lba1);
        var idx = new short[W * H]; Array.Fill(idx, (short)-1);
        var depth = new float[W * H]; Array.Fill(depth, float.NegativeInfinity);
        void Put(int x, int y, int c, float d)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || c < 0) return;
            var k = y * W + x;
            if (d < depth[k]) return;
            depth[k] = d; idx[k] = (short)c;
        }
        foreach (var a in actors)
        {
            var placed = layout.Placements.First(p => p.Scene == a.Scene);
            double Xj = a.X + 512.0 * placed.X, Yj = a.Y + 256.0 * placed.Y, Zj = a.Z + 512.0 * placed.Z;
            // (the engine's projection, from the anchor of the layout's cell (0, 0, 0): OBJECT.C ProjettePoint against GRILLE_A Map2Screen)
            (double X, double Y) Proj(double x, double y, double z) => (OX + 23 + Math.Floor((x - z) * 24 / 512), OY + 25 - Math.Floor((y * 30 - (x + z) * 12) / 512));
            float Depth(double x, double y, double z) => (float)(x + z + 0.8 * y);
            if (a.IsSprite)
            {
                var s = TempleActors.ReadSprite(lba1, data, a.Sprite)!;
                var (px, py) = Proj(Xj, Yj, Zj);
                var (ox, oy) = Proj(512.0 * placed.X, 256.0 * placed.Y, 512.0 * placed.Z);
                var d = Depth(Xj, Yj, Zj) + 300;
                for (var j = 0; j < s.Picture.Lines; j++)
                    for (var i = 0; i < s.Picture.Width; i++)
                    {
                        var c = s.Picture.Pixels[j * s.Picture.Width + i];
                        if (c < 0) continue;
                        int sx = (int)px + s.OffX + i, sy = (int)py + s.OffY + j;
                        if (a.Clip is { } clip && (sx - ox < clip[0] || sx - ox > clip[2] || sy - oy < clip[1] || sy - oy > clip[3])) continue;
                        Put(sx, sy, c, d);
                    }
                continue;
            }
            var body = LbaBodyStudio.Body.Read(game.ReadBody(a.Body)!, 1);
            var bones = (a.Pose ?? Array.Empty<Lba1Animation.BoneFrame>()).Select(f => (f.Type, (double)f.X, (double)f.Y, (double)f.Z)).ToList();
            var posed = Lba1Pose.World(body, bones, out var matrices);
            double beta = a.Beta * Math.PI * 2 / 1024, sb = Math.Sin(beta), cb = Math.Cos(beta);
            var pts = posed.Select(p => (X: Xj + p.X * cb + p.Z * sb, Y: Yj + p.Y, Z: Zj - p.X * sb + p.Z * cb)).ToArray();
            var light = Lights(body, matrices, a.Beta, a.AlphaLight, a.BetaLight);
            foreach (var f in body.Faces)
            {
                var n = f.Points.Length;
                if (n < 3) continue;
                // (front faces only: their corners' Newell normal towards LBA1's camera, (1, 0.8, 1))
                double nx = 0, ny = 0, nz = 0;
                for (var i = 0; i < n; i++)
                {
                    var p = pts[f.Points[i]]; var q = pts[f.Points[(i + 1) % n]];
                    nx += (p.Y - q.Y) * (p.Z + q.Z); ny += (p.Z - q.Z) * (p.X + q.X); nz += (p.X - q.X) * (p.Y + q.Y);
                }
                if (nx + 0.8 * ny + nz <= 0) continue;
                var shade = new float[n];
                for (var i = 0; i < n; i++)
                    shade[i] = f.Material >= 9 && f.PointNormals is { } pn && pn[i] >= 0 && pn[i] < light.Length ? light[pn[i]]
                        : f.Material is 7 or 8 && f.FaceNormal >= 0 && f.FaceNormal < light.Length ? light[f.FaceNormal] : 0;
                var sc = f.Points.Select(i => { var (x, y) = Proj(pts[i].X, pts[i].Y, pts[i].Z); var t = pts[i]; return (x, y, d: Depth(t.X, t.Y, t.Z)); }).ToArray();
                for (var k = 1; k + 1 < n; k++)
                    Fill(sc[0], sc[k], sc[k + 1], shade[0], shade[k], shade[k + 1], f.Colour, Put);
            }
            foreach (var l in body.Lines)
            {
                var (x0, y0) = Proj(pts[l.A].X, pts[l.A].Y, pts[l.A].Z); var (x1, y1) = Proj(pts[l.B].X, pts[l.B].Y, pts[l.B].Z);
                var steps = (int)Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) + 1;
                for (var t = 0; t <= steps; t++)
                {
                    var u = t / (double)steps;
                    var p = (X: pts[l.A].X + (pts[l.B].X - pts[l.A].X) * u, Y: pts[l.A].Y + (pts[l.B].Y - pts[l.A].Y) * u, Z: pts[l.A].Z + (pts[l.B].Z - pts[l.A].Z) * u);
                    Put((int)Math.Round(x0 + (x1 - x0) * u), (int)Math.Round(y0 + (y1 - y0) * u), l.Colour, Depth(p.X, p.Y, p.Z) + 1);
                }
            }
        }
        File.WriteAllBytes(outPrefix + ".idx", System.Runtime.InteropServices.MemoryMarshal.AsBytes(idx.AsSpan()).ToArray());
        File.WriteAllBytes(outPrefix + ".depth", System.Runtime.InteropServices.MemoryMarshal.AsBytes(depth.AsSpan()).ToArray());
        Console.WriteLine($"{outPrefix}.idx/.depth: {idx.Count(i => i >= 0)} actor pixels");
        return 0;
    }

    private static void Fill((double X, double Y, float D) a, (double X, double Y, float D) b, (double X, double Y, float D) c, float sa, float sb, float sc, int colour, Action<int, int, int, float> put)
    {
        double Edge((double X, double Y, float D) p, (double X, double Y, float D) q, double x, double y) => (x - p.X) * (q.Y - p.Y) - (y - p.Y) * (q.X - p.X);
        var area = Edge(a, b, c.X, c.Y);
        if (Math.Abs(area) < 1e-6) return;
        int x0 = (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), x1 = (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X)));
        int y0 = (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))), y1 = (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y)));
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                double wa = Edge(b, c, x + 0.5, y + 0.5) / area, wb = Edge(c, a, x + 0.5, y + 0.5) / area, wc = 1 - wa - wb;
                if (wa < -1e-4 || wb < -1e-4 || wc < -1e-4) continue;
                var s = wa * sa + wb * sb + wc * sc;
                put(x, y, colour + (int)Math.Round(s), (float)(wa * a.D + wb * b.D + wc * c.D));
            }
    }

    private static float[] Lights(LbaBodyStudio.Body body, double[][] matrices, int actorBeta, int alphaLight, int betaLight)
    {
        static double Rad(double a) => a * Math.PI * 2 / 1024;
        double sa = Math.Sin(Rad(alphaLight)), ca = Math.Cos(Rad(alphaLight)), sb = Math.Sin(Rad(betaLight)), cb = Math.Cos(Rad(betaLight));
        double lx = sb, ly = -sa * cb, lz = ca * cb, sy = Math.Sin(Rad(actorBeta)), cy = Math.Cos(Rad(actorBeta));
        return body.Normals.Select((n, i) =>
        {
            var m = matrices[Math.Clamp(i < body.NormalBone.Length ? body.NormalBone[i] : 0, 0, matrices.Length - 1)];
            double x = m[0] * n.X + m[1] * n.Y + m[2] * n.Z, y = m[3] * n.X + m[4] * n.Y + m[5] * n.Z, z = m[6] * n.X + m[7] * n.Y + m[8] * n.Z;
            double wx = cy * x + sy * z, wz = -sy * x + cy * z, dot = wx * lx + y * ly + wz * lz;
            return dot <= 0 || n.Range == 0 ? 0f : (float)Math.Floor(dot * 59 / n.Range);
        }).ToArray();
    }

    private static int Info(string lba1, int frames)
    {
        var game = new Lba1Game(lba1);
        var data = new Lba1RuntimeData(lba1);
        var sprites = HqrArchive.Open(Path.Combine(lba1, "SPRITES.HQR"));
        foreach (var s in TempleLayout.Scenes)
        {
            var scene = game.LoadScene(s);
            var r = new Lba1Runtime(data);
            r.ChangeCube(s);
            var start = r.Objects.Take(r.NbObjets).Select(o => (o.Body, o.Anim, o.Frame, o.Beta, o.PosX, o.PosY, o.PosZ, o.Flags)).ToArray();
            r.Run(frames);
            Console.WriteLine($"==== scene {s}: light {scene.AlphaLight} {scene.BetaLight}, {scene.Actors.Count} actors");
            for (var a = 1; a < scene.Actors.Count; a++)
            {
                var ac = scene.Actors[a];
                var o = r.Objects[a];
                string what;
                if (ac.IsSprite)
                {
                    var entry = sprites.Read(ac.Sprite);
                    var sp = PolarTextures.Sprite.Decode(entry[(int)BitConverter.ToUInt32(entry, 0)..]);
                    var off = data.SpriteOffset(ac.Sprite);
                    var v = data.SpriteVolume(ac.Sprite);
                    what = $"sprite {ac.Sprite} {sp.Width}x{sp.Lines} hot ({sp.HotX},{sp.HotY}) offset ({off.X},{off.Y}) volume {v}";
                }
                else
                {
                    var body = game.BodyIndex(ac.Entity, ac.Body);
                    var anim = game.AnimIndex(ac.Entity, ac.Anim);
                    var b = body is { } bi && game.ReadBody(bi) is { } bytes ? LbaBodyStudio.Body.Read(bytes, 1) : null;
                    what = $"entity {ac.Entity} ({game.EntityName(ac.Entity)}) genBody {ac.Body} -> BODY {body} genAnim {ac.Anim} -> ANIM {anim}" +
                           (b is null ? "" : $" [{b.Vertices.Count} pts {b.Bones.Count} bones {b.Faces.Count} polys {b.Lines.Count} lines {b.Spheres.Count} spheres; mats {string.Join(",", b.Faces.GroupBy(f => f.Material).Select(g => $"{g.Key}:{g.Count()}"))}]");
                }
                var st = start[a];
                Console.WriteLine($"actor {a,2}: flags 0x{ac.StaticFlags:X4} pos ({ac.X},{ac.Y},{ac.Z}) beta {ac.Angle} {what}");
                Console.WriteLine($"          start: body {st.Body} anim {st.Anim} frame {st.Frame} beta {st.Beta} flags 0x{st.Flags:X4} | after {frames}: body {o.Body} anim {o.Anim} frame {o.Frame} " +
                                  $"beta {o.Beta} pos ({o.PosX},{o.PosY},{o.PosZ}) flags 0x{o.Flags:X4} work 0x{o.WorkFlags:X} info [{o.Info},{o.Info1},{o.Info2},{o.Info3}] vol ({o.XMin},{o.XMax},{o.YMin},{o.YMax},{o.ZMin},{o.ZMax})");
            }
        }
        return 0;
    }
}
