using System.Numerics;
using System.Text.RegularExpressions;
using LBAAssembler;
using LBAAssembler.Lba1;
using LBAAssembler.LbaScript;
using LBAAssembler.Scenes;
using LBAAssembler.Terrain;
using LBAAssembler.Terrain.Polar;
using LBAAssembler.Terrain.Temple;
using LbaBodyStudio;

namespace ScriptRoundTrip;

// AUDIT PROBE (2026-10-18, not product code): the Temple of Bú's port checked cell by cell.
//   templeaudit raw <LBA1> <out dir>: every grid cell of scenes 8/40/41 (brick-0 cells, window/overlap drops, shapes), actors, zones,
//     ambience, scripts, the join offsets the zones give
//   templeaudit cells <LBA1> <game dir> <plan.json> <out dir> [run.log] [every] [W] [H]: the built island decoded (TEMPLE.ILE.bak = the temple
//     alone, TEMPLE.ILE = with the road, TEMPLE.OBL) -- each face to its LBA1 cell, the solid boxes; LBA1's own per-scene picture as an id
//     buffer (what LBA1 shows of each cell) against what the port draws; exposure; solids vs drawn; the road's corridor; and (with a camtrace
//     run.log) rays from the chase camera through the port's polygons, counting the pixels that see through a cell LBA1 has as a solid block.
internal static class TempleAudit
{
    public static int Run(string[] args) => args[1] switch
    {
        "raw" => Raw(args[2], args[3]),
        "cells" => Cells(args),
        "lba1angle" => Lba1Angle(args),
        "overlay" => Overlay(args),
        "crop" => Crop(args),
        "sprites" => Sprites(args),
        "solids" => Solids(args),
        _ => throw new ArgumentException("templeaudit raw|cells|lba1angle|overlay|solids"),
    };

    // solids <ile A> <ile B>: the solid boxes (decors of the empty body 0 with a box) of two island files, as sets -- those only in one
    private static int Solids(string[] args)
    {
        HashSet<(int, int, int, int, int, int)> Of(string path)
        {
            var island = IslandFile.Load(path);
            var set = new HashSet<(int, int, int, int, int, int)>();
            for (var cz = 0; cz < 16; cz++)
                for (var cx = 0; cx < 16; cx++)
                    if (island.CubeAt(cx, cz) is { } cube)
                        foreach (var d in cube.Decors)
                            if ((d.Body & 0xFFFF) == 0 && d.YMax != -32000)
                                set.Add((cx * 32768 + d.XMin, d.YMin, cz * 32768 + d.ZMin, cx * 32768 + d.XMax, d.YMax, cz * 32768 + d.ZMax));
            return set;
        }
        var a = Of(args[2]); var b = Of(args[3]);
        Console.WriteLine($"{args[2]}: {a.Count} solid boxes; {args[3]}: {b.Count}; only in the first {a.Except(b).Count()}, only in the second {b.Except(a).Count()}");
        foreach (var s in a.Except(b).Take(20)) Console.WriteLine($"  only first: {s}");
        foreach (var s in b.Except(a).Take(20)) Console.WriteLine($"  only second: {s}");
        return 0;
    }

    private static readonly int[] Scenes = { 8, 40, 41 };

    // the temple's layout; AUDIT_NOFILL=1: without the cells that close the 8|40 join's slot (TempleLayout.Filled), as builds before them had it
    private static TempleLayout Layout(Lba1Game game)
    {
        var layout = TempleLayout.Build(game);
        if (Environment.GetEnvironmentVariable("AUDIT_NOFILL") == "1") { foreach (var k in layout.Filled.Keys) layout.Cells.Remove(k); layout.Filled.Clear(); }
        return layout;
    }

    // ------------------------------------------------------------------------------------------------------------------------------ raw
    private static int Raw(string lba1, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var game = new Lba1Game(lba1);
        var area = game.Areas.First(a => a.Tiles.Any(t => t.Scene == 8));
        using var w = new StreamWriter(Path.Combine(outDir, "raw.txt"));
        foreach (var t in area.Tiles)
            w.WriteLine($"tile scene {t.Scene}: offset cells ({t.OffsetX / 512.0}, layers {t.OffsetY / 256.0}, {t.OffsetZ / 512.0}) window {t.Window} without {t.Without}");
        using var csv = new StreamWriter(Path.Combine(outDir, "raw_cells.csv"));
        csv.WriteLine("scene,x,y,z,lx,ly,lz,block,pos,shape,code,brick,status");
        var taken = new Dictionary<(int, int, int), int>();
        foreach (var s in Scenes)
        {
            var tile = area.Tiles.First(t => t.Scene == s);
            int ox = tile.OffsetX / 512, oy = tile.OffsetY / 256, oz = tile.OffsetZ / 512;
            var cells = Lba1GridCodec.Decode(game.ReadGrid(s));
            var lib = game.ReadBlocks(s);
            var count = BitConverter.ToInt32(lib, 0) / 4;
            var stats = new Dictionary<string, int>();
            var shapes = new Dictionary<int, int>();
            for (var z = 0; z < 64; z++)
            for (var x = 0; x < 64; x++)
            for (var y = 0; y < 25; y++)
            {
                var i = ((z * 64 + x) * 25 + y) * 2;
                int block = cells[i], pos = cells[i + 1];
                if (block == 0) continue;
                string status; int shape = -1, code = -1, brick = -1;
                if (block > count) status = "invalid-block";
                else
                {
                    var at = BitConverter.ToInt32(lib, (block - 1) * 4);
                    if (pos >= lib[at] * lib[at + 1] * lib[at + 2]) status = "invalid-pos";
                    else
                    {
                        var e = at + 3 + pos * 4;
                        shape = lib[e]; code = lib[e + 1]; brick = BitConverter.ToUInt16(lib, e + 2) - 1;
                        if (brick < 0) status = shape == 0 ? "brick0-empty" : "brick0-solid";
                        else if (!tile.Holds(x, z)) status = "window";
                        else if (taken.TryGetValue((x + ox, y + oy, z + oz), out var other)) status = $"overlap-{other}";
                        else { taken[(x + ox, y + oy, z + oz)] = s; status = "ok"; }
                        if (brick >= 0) shapes[shape] = shapes.GetValueOrDefault(shape) + 1;
                    }
                }
                stats[status] = stats.GetValueOrDefault(status) + 1;
                csv.WriteLine($"{s},{x},{y},{z},{x + ox},{y + oy},{z + oz},{block},{pos},{shape},{code},{brick},{status}");
            }
            w.WriteLine($"scene {s}: " + string.Join(", ", stats.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}")) +
                        "; shapes of drawn bricks: " + string.Join(", ", shapes.OrderBy(k => k.Key).Select(k => $"{k.Key}:{k.Value}")));
        }

        // the scenes: header, actors, zones, scripts
        var store = new SceneStore(SceneGame.Lba1, lba1);
        foreach (var s in Scenes)
        {
            var m = store.Load(s);
            var tile = area.Tiles.First(t => t.Scene == s);
            w.WriteLine();
            w.WriteLine($"==== scene {s}: island {m.Island} light alpha {m.AlphaLight} beta {m.BetaLight} music {m.Music} ambience every {m.SecondMin}+{m.SecondEcart} s: " +
                        string.Join("; ", m.Ambient.Select(a => $"sample {a.Sample} repeat {a.Repeat} round {a.Round} freq {a.Frequency} vol {a.Volume}")) +
                        $" cubeMode {m.CubeMode} shadow {m.ShadowLevel}");
            for (var a = 0; a < m.Actors.Count; a++)
            {
                var ac = m.Actors[a];
                w.WriteLine($"actor {a}: flags 0x{ac.Flags:X4} sprite? {ac.IsSprite} entity {ac.Entity} body {ac.Body} anim {ac.Anim} spr {ac.Sprite} pos ({ac.X},{ac.Y},{ac.Z}) " +
                            $"cell ({(ac.X + 256) / 512},{ac.Y / 256.0:0.##},{(ac.Z + 256) / 512}) layout ({(ac.X + 256) / 512 + tile.OffsetX / 512},{ac.Y / 256.0 + tile.OffsetY / 256:0.##},{(ac.Z + 256) / 512 + tile.OffsetZ / 512}) " +
                            $"beta {ac.Beta} move {ac.Move} option 0x{ac.OptionFlags:X} info [{string.Join(",", ac.Info)}] bonus {ac.NbBonus} life {ac.LifePoints} armor {ac.Armor} track {ac.Track.Length}B life {ac.Life.Length}B");
            }
            for (var i = 0; i < m.Zones.Count; i++)
            {
                var z = m.Zones[i];
                w.WriteLine($"zone {i}: type {z.Type} x {z.X0}..{z.X1} y {z.Y0}..{z.Y1} z {z.Z0}..{z.Z1} info [{string.Join(",", z.Info)}] snap {z.Snap} " +
                            $"cells x {(z.X0 + 256) / 512.0:0.#}..{(z.X1 + 256) / 512.0:0.#} layers {z.Y0 / 256.0:0.#}..{z.Y1 / 256.0:0.#} z {(z.Z0 + 256) / 512.0:0.#}..{(z.Z1 + 256) / 512.0:0.#}");
                if (z.Type == 0 && Scenes.Contains(z.Info[0]))
                {
                    var target = area.Tiles.First(t => t.Scene == z.Info[0]);
                    // (arrival = target place + (hero - zone min): the target's origin relative to this scene's, by the zone)
                    double dx = (z.X0 - z.Info[1]) / 512.0, dy = (z.Y0 - z.Info[2]) / 256.0, dz = (z.Z0 - z.Info[3]) / 512.0;
                    double ax = (target.OffsetX - tile.OffsetX) / 512.0, ay = (target.OffsetY - tile.OffsetY) / 256.0, az = (target.OffsetZ - tile.OffsetZ) / 512.0;
                    w.WriteLine($"   -> scene {z.Info[0]}: the zone puts its origin at ({dx:0.##}, {dy:0.##}, {dz:0.##}) cells/layers from {s}'s; the joined map has ({ax}, {ay}, {az}): off by ({ax - dx:0.##}, {ay - dy:0.##}, {az - dz:0.##})");
                }
            }
            var record = store.LoadRecord(s);
            var scripts = SceneScripts.Load(record, s, null, lba1: true);
            using var sw = new StreamWriter(Path.Combine(outDir, $"scripts_{s}.txt"));
            for (var a = 0; a < m.Actors.Count; a++)
            {
                sw.WriteLine($"// ===== actor {a} life");
                sw.WriteLine(scripts.GetText(a, ScriptKind.Life));
                sw.WriteLine($"// ===== actor {a} track");
                sw.WriteLine(scripts.GetText(a, ScriptKind.Track));
            }
        }
        Console.WriteLine($"{outDir}: raw.txt, raw_cells.csv, scripts_*.txt");
        return 0;
    }

    // ------------------------------------------------------------------------------------------------------------------------------ cells
    private sealed record Poly(Vector3[] P, Vector3 N, int Type, int Body, bool Temple)
    {
        // its texture: the object page, the tile's origin on it, the points' (u, v) in 1/256 texels from it
        public int Page = -1, Tx, Ty;
        public int[]? Uv;
    }

    private const int MinY = -8;
    private static Vector3 Layout(Vector3 w) => new((w.X / 512f - TempleFrame.OffsetX) / TempleFrame.Scale, (w.Y - TempleFrame.BaseY) / 768f + MinY, (w.Z / 512f - TempleFrame.OffsetZ) / TempleFrame.Scale);
    private static Vector3 World(double x, double y, double z) => new((float)((TempleFrame.OffsetX + x * TempleFrame.Scale) * 512.0), (float)(TempleFrame.BaseY + (y - MinY) * 768.0), (float)((TempleFrame.OffsetZ + z * TempleFrame.Scale) * 512.0));
    private static (int, int, int) CellOf(Vector3 l) => ((int)Math.Floor(l.X), (int)Math.Floor(l.Y), (int)Math.Floor(l.Z));

    // decors of an island file: polygons (world), solid boxes (world), and the (body, place) keys of its drawn decors
    private static (List<Poly> Polys, List<(Vector3 Lo, Vector3 Hi)> Solids, HashSet<(int, int, int, int)> Keys, int Decors) Decode(string ile, HqrArchive obl, HashSet<(int, int, int, int)>? skip, bool temple)
    {
        var island = IslandFile.Load(ile);
        var polys = new List<Poly>();
        var solids = new HashSet<(int, int, int, int, int, int)>();
        var keys = new HashSet<(int, int, int, int)>();
        var bodies = new Dictionary<int, Body?>();
        var decors = 0;
        for (var cz = 0; cz < 16; cz++)
        for (var cx = 0; cx < 16; cx++)
        {
            var cube = island.CubeAt(cx, cz);
            if (cube is null) continue;
            foreach (var d in cube.Decors)
            {
                decors++;
                var b = d.Body & 0xFFFF;
                int wx = cx * 32768 + d.X, wz = cz * 32768 + d.Z;
                if (b == 0)
                {
                    if (d.YMax == -32000) continue;
                    solids.Add((cx * 32768 + d.XMin, d.YMin, cz * 32768 + d.ZMin, cx * 32768 + d.XMax, d.YMax, cz * 32768 + d.ZMax));
                    continue;
                }
                var key = (b, wx, d.Y, wz);
                if (!keys.Add(key)) continue;
                if (skip is not null && skip.Contains(key)) continue;
                if (!bodies.TryGetValue(b, out var body))
                {
                    try { body = Body.Read(obl.Read(b), 2, allowStatic: true); } catch { body = null; }
                    bodies[b] = body;
                }
                if (body is null) continue;
                var beta = (d.Beta & 0xFFFF) * Math.PI * 2 / 4096;
                float cos = (float)Math.Cos(beta), sin = (float)Math.Sin(beta);
                Vector3 Place(Vector3 v) => (d.Beta & 0xFFFF) == 0 ? new Vector3(wx + v.X, d.Y + v.Y, wz + v.Z)
                    : new Vector3(wx + v.X * cos + v.Z * sin, d.Y + v.Y, wz - v.X * sin + v.Z * cos);
                foreach (var f in body.Faces)
                {
                    var p = f.Points.Select(i => Place(body.Vertices[i])).ToArray();
                    var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                    if (n.LengthSquared() < 1e-6 && p.Length > 3) n = Vector3.Cross(p[2] - p[0], p[3] - p[0]);
                    if (n.LengthSquared() < 1e-6) continue;
                    var poly = new Poly(p, Vector3.Normalize(n), f.Material, b, temple);
                    if (f.Texture is { } tex && tex.Handle < body.Textures.Length)
                    {
                        var off = (int)(body.Textures[tex.Handle] & 0xFFFF);
                        poly.Page = IslandFile.ObjectPageOf(d); poly.Tx = off & 255; poly.Ty = off >> 8; poly.Uv = tex.UV;
                    }
                    polys.Add(poly);
                }
            }
        }
        return (polys, solids.Select(s => (new Vector3(s.Item1, s.Item2, s.Item3), new Vector3(s.Item4, s.Item5, s.Item6))).ToList(), keys, decors);
    }

    private static int Dir(Vector3 n)
    {
        if (n.Y > 0.99f) return 0;
        if (n.X > 0.99f) return 1;
        if (n.X < -0.99f) return 2;
        if (n.Z > 0.99f) return 3;
        if (n.Z < -0.99f) return 4;
        if (n.Y < -0.99f) return 5;
        return 6;
    }
    private static readonly string[] DirNames = { "top", "+x", "-x", "+z", "-z", "bottom", "other" };
    private static readonly (int Dx, int Dy, int Dz)[] Dirs = { (0, 1, 0), (1, 0, 0), (-1, 0, 0), (0, 0, 1), (0, 0, -1), (0, -1, 0) };
    private static PolarTextures.Face FaceOf(int dir) => dir switch { 0 or 5 => PolarTextures.Face.Top, 1 or 2 => PolarTextures.Face.SideX, _ => PolarTextures.Face.SideZ };

    private static int Cells(string[] args)
    {
        string lba1 = args[2], gameDir = args[3], planPath = args[4], outDir = args[5];
        var runLog = args.Length > 6 ? args[6] : null;
        var every = args.Length > 7 ? int.Parse(args[7]) : 20;
        int rw = args.Length > 8 ? int.Parse(args[8]) : 160, rh = args.Length > 9 ? int.Parse(args[9]) : 120;
        Directory.CreateDirectory(outDir);
        var log = new StreamWriter(Path.Combine(outDir, "summary.txt"));
        void Say(string s) { Console.WriteLine(s); log.WriteLine(s); log.Flush(); }

        var game = new Lba1Game(lba1);
        var layout = Layout(game);
        var plan = RaceTrackPlan.Load(planPath);
        var cut = (plan.TempleCuts ?? Array.Empty<int[]>()).Where(c => c.Length == 3).Select(c => (c[0], c[1], c[2])).ToHashSet();
        var shapes = TempleShapes.Find(game);
        var remains = TempleShapes.CutRemains(shapes, layout.Cells, cut);
        var models = TempleModels.Find(game);
        var modelRemains = TempleModels.CutRemains(models, layout.Cells, cut.Concat(remains).ToHashSet());
        var gone = cut.Concat(remains).Concat(modelRemains).ToHashSet();
        // (TempleLayout.Without: a cell that closes the 8|40 join's slot goes with the cell it copies)
        var built = layout.Without(gone).Cells;
        gone = layout.Cells.Keys.Where(k => !built.ContainsKey(k)).ToHashSet();
        Say($"layout {layout.Cells.Count} cells; cut {cut.Count(layout.Cells.ContainsKey)} (+{cut.Count(c => !layout.Cells.ContainsKey(c))} cut cells that hold nothing), round remains {remains.Count}, model remains {modelRemains.Count}; built {built.Count}");

        // per brick: box, coverage
        var sprites = new Dictionary<int, PolarTextures.Sprite>();
        PolarTextures.Sprite Spr(int b) => sprites.TryGetValue(b, out var s) ? s : sprites[b] = PolarTextures.Sprite.Decode(game.ReadBrick(b));
        var info = new Dictionary<int, (PolarTextures.Box Box, bool Whole, double Top, double X, double Z, double Max)>();
        (PolarTextures.Box Box, bool Whole, double Top, double X, double Z, double Max) Info(int b)
        {
            if (info.TryGetValue(b, out var i)) return i;
            var s = Spr(b);
            var whole = Enum.GetValues<PolarTextures.Face>().Any(f => s.Coverage(f) >= TempleObjects.Solid);
            var box = whole ? PolarTextures.Box.Full : s.Footprint();
            double t = s.Coverage(PolarTextures.Face.Top, box), x = s.Coverage(PolarTextures.Face.SideX, box), z = s.Coverage(PolarTextures.Face.SideZ, box);
            return info[b] = (box, whole, t, x, z, Math.Max(t, Math.Max(x, z)));
        }
        string Kind(int brick) => shapes.TryGetValue(brick, out var p) ? "round:" + p.Shape.Kind.Name : models.TryGetValue(brick, out var m) ? "model:" + m.Model.Kind.Name : "box";

        // ---- the built island decoded
        var obl = HqrArchive.Open(Path.Combine(gameDir, "TEMPLE.OBL"));
        var bak = Path.Combine(gameDir, "TEMPLE.ILE.bak");
        var templeIle = File.Exists(bak) ? bak : Path.Combine(gameDir, "TEMPLE.ILE");
        var (tPolys, tSolids, tKeys, tDecors) = Decode(templeIle, obl, null, true);
        var (rPolys, rSolids, _, rDecors) = Decode(Path.Combine(gameDir, "TEMPLE.ILE"), obl, tKeys, false);
        var raceSolids = rSolids.Where(r => !tSolids.Contains(r)).ToList();
        Say($"decoded {templeIle}: {tDecors} decors, {tKeys.Count} drawn bodies, {tPolys.Count} polygons, {tSolids.Count} distinct solid boxes; final TEMPLE.ILE: {rDecors} decors, {rPolys.Count} more polygons (road, decor), {raceSolids.Count} more solid boxes");

        // faces to cells
        var faces = new Dictionary<(int, int, int), (int N, int Mask, int SeeThrough)>();
        var outside = 0;
        foreach (var p in tPolys)
        {
            var c = p.P.Aggregate(Vector3.Zero, (a, b) => a + b) / p.P.Length - p.N * 2;
            var cell = CellOf(Layout(c));
            if (!built.ContainsKey(cell)) outside++;
            faces.TryGetValue(cell, out var f);
            faces[cell] = (f.N + 1, f.Mask | 1 << Dir(p.N), f.SeeThrough + (p.Type == 12 ? 1 : 0));
        }
        Say($"faces in {faces.Count} cells ({faces.Keys.Count(built.ContainsKey)} of them built cells); {outside} faces fall in no built cell");
        // (heights: every axis-aligned face's plane on the lattice)
        var offLattice = 0;
        foreach (var p in tPolys.Where(p => Dir(p.N) is 0 or 5))
        {
            var ly = (p.P[0].Y - TempleFrame.BaseY) / 768.0;
            if (Math.Abs(ly - Math.Round(ly * 4) / 4) > 0.01 && models.Count >= 0) offLattice++;
        }
        Say($"horizontal faces off the quarter-layer lattice (world y not BaseY + k * 192): {offLattice} of {tPolys.Count(p => Dir(p.N) is 0 or 5)} (models and round things have their own heights)");

        // solid boxes indexed by layout cell
        var solidAt = new Dictionary<(int, int, int), List<int>>();
        for (var i = 0; i < tSolids.Count; i++)
        {
            var (lo, hi) = tSolids[i];
            var a = Layout(lo); var b = Layout(hi);
            for (var x = (int)Math.Floor(a.X + 1e-3); x <= (int)Math.Floor(b.X - 1e-3); x++)
            for (var y = (int)Math.Floor(a.Y + 1e-3); y <= (int)Math.Floor(b.Y - 1e-3); y++)
            for (var z = (int)Math.Floor(a.Z + 1e-3); z <= (int)Math.Floor(b.Z - 1e-3); z++)
            {
                if (!solidAt.TryGetValue((x, y, z), out var l)) solidAt[(x, y, z)] = l = new();
                l.Add(i);
            }
        }
        bool SolidAtPoint(Vector3 world)
        {
            var c = CellOf(Layout(world));
            return solidAt.TryGetValue(c, out var l) && l.Any(i => tSolids[i].Lo.X <= world.X && world.X <= tSolids[i].Hi.X && tSolids[i].Lo.Y <= world.Y && world.Y <= tSolids[i].Hi.Y
                                                                   && tSolids[i].Lo.Z <= world.Z && world.Z <= tSolids[i].Hi.Z);
        }
        bool CellSolid((int X, int Y, int Z) c, PolarTextures.Box box) => SolidAtPoint(World(c.X + (box.U0 + box.U1) / 2, c.Y + 0.5, c.Z + (box.V0 + box.V1) / 2));

        // ---- per cell
        using (var csv = new StreamWriter(Path.Combine(outDir, "cells_audit.csv")))
        {
            csv.WriteLine("x,y,z,scene,block,brick,shape,kind,full,whole,covTop,covX,covZ,wisp,faces,seeThrough,dirs,exposed,missing,solid");
            foreach (var ((x, y, z), cell) in built.OrderBy(c => c.Key.Z).ThenBy(c => c.Key.X).ThenBy(c => c.Key.Y))
            {
                var i = Info(cell.Brick);
                faces.TryGetValue((x, y, z), out var f);
                var exposed = 0;
                for (var d = 0; d < 6; d++) if (!built.ContainsKey((x + Dirs[d].Dx, y + Dirs[d].Dy, z + Dirs[d].Dz))) exposed |= 1 << d;
                var missing = exposed & ~f.Mask;
                var solid = CellSolid((x, y, z), i.Box);
                csv.WriteLine($"{x},{y},{z},{cell.Scene},{cell.Block},{cell.Brick},{cell.Shape},{Kind(cell.Brick)},{(i.Box.IsFull ? 1 : 0)},{(i.Whole ? 1 : 0)},{i.Top:0.###},{i.X:0.###},{i.Z:0.###}," +
                              $"{(i.Max < TempleObjects.Wisp ? 1 : 0)},{f.N},{f.SeeThrough},{f.Mask},{exposed},{missing},{(solid ? 1 : 0)}");
            }
        }

        // ---- the solids: over cells not built (a doorway's cut, an empty cell), and how many built cells each kind is
        var solidInGone = gone.Where(c => layout.Cells.ContainsKey(c) && CellSolid(c, Info(layout.Cells[c].Brick).Box)).ToList();
        Say($"cut / removed cells whose middle is inside a solid box (an invisible wall in a doorway): {solidInGone.Count}" + (solidInGone.Count > 0 ? " e.g. " + string.Join(" ", solidInGone.Take(10)) : ""));
        var solidEmpty = solidAt.Keys.Where(c => !built.ContainsKey(c)).ToList();
        // (a box reaching a cell not built: is its middle inside the box?)
        var solidEmptyMid = solidEmpty.Where(c => SolidAtPoint(World(c.Item1 + 0.5, c.Item2 + 0.5, c.Item3 + 0.5))).ToList();
        Say($"layout cells with no built brick whose middle is inside a temple solid box: {solidEmptyMid.Count}" + (solidEmptyMid.Count > 0 ? " e.g. " + string.Join(" ", solidEmptyMid.Take(12)) : ""));
        File.WriteAllLines(Path.Combine(outDir, "solid_in_empty.csv"), new[] { "x,y,z,gone" }.Concat(solidEmptyMid.Select(c => $"{c.Item1},{c.Item2},{c.Item3},{(gone.Contains(c) ? 1 : 0)}")));

        // ---- LBA1's own picture of each scene alone: what each cell shows (top / +x / +z pixels), against the port's faces
        var area = game.Areas.First(a => a.Tiles.Any(t => t.Scene == 8));
        using (var vis = new StreamWriter(Path.Combine(outDir, "lba1_visible.csv")))
        {
            vis.WriteLine("scene,x,y,z,lx,ly,lz,brick,pxTop,pxX,pxZ,status,kind,portMask,portFaces,missingDirs");
            foreach (var s in Scenes)
            {
                var tile = area.Tiles.First(t => t.Scene == s);
                int ox = tile.OffsetX / 512, oy = tile.OffsetY / 256, oz = tile.OffsetZ / 512;
                var placed = PolarLayout.Bricks(game, s).OrderBy(b => b.Z).ThenBy(b => b.X).ThenBy(b => b.Y).ToList();
                const int W = 64 * 48 + 200, H = 64 * 24 + 25 * 15 + 200, OX = 64 * 24 + 100, OY = 25 * 15 + 100;
                var id = new int[W * H]; Array.Fill(id, -1);
                var fc = new byte[W * H];
                for (var k = 0; k < placed.Count; k++)
                {
                    var b = placed[k]; var sp = Spr(b.Brick);
                    int ax = 24 * (b.X - b.Z) + OX, ay = 12 * (b.X + b.Z) - 15 * b.Y + OY;
                    for (var py = 0; py < sp.Lines; py++)
                    for (var px = 0; px < sp.Width; px++)
                    {
                        if (sp.Pixels[py * sp.Width + px] < 0) continue;
                        int sx = px + sp.HotX, sy = py + sp.HotY;
                        int X = ax + sx, Y = ay + sy;
                        if (X < 0 || Y < 0 || X >= W || Y >= H) continue;
                        id[Y * W + X] = k;
                        var top = Math.Abs(sx + 0.5 - 24) / 24.0 + Math.Abs(sy + 0.5 - 12) / 12.0 <= 1;
                        fc[Y * W + X] = (byte)(top ? 0 : sx + 0.5 >= 24 ? 1 : 2);
                    }
                }
                var px3 = new int[placed.Count, 3];
                for (var q = 0; q < id.Length; q++) if (id[q] >= 0) px3[id[q], fc[q]]++;
                var counts = new Dictionary<string, int>();
                for (var k = 0; k < placed.Count; k++)
                {
                    int t = px3[k, 0], xx = px3[k, 1], zz = px3[k, 2];
                    if (t + xx + zz < 8) continue;
                    var b = placed[k];
                    var lc = (b.X + ox, b.Y + oy, b.Z + oz);
                    string status; var kind = Kind(b.Brick); var mask = 0; var nf = 0; var miss = "";
                    if (!tile.Holds(b.X, b.Z)) status = "window";
                    else if (!layout.Cells.TryGetValue(lc, out var lcCell) || lcCell.Scene != s) status = "overlap-dropped";
                    else if (gone.Contains(lc)) status = cut.Contains(lc) ? "cut" : "cut-remains";
                    else
                    {
                        faces.TryGetValue(lc, out var f); mask = f.Mask; nf = f.N;
                        if (kind != "box" || !Info(b.Brick).Box.IsFull) status = nf > 0 ? "drawn" : "NOTHING";
                        else
                        {
                            var need = new List<int>();
                            if (t >= 8) need.Add(0);
                            if (xx >= 8) need.Add(1);
                            if (zz >= 8) need.Add(3);
                            var m = need.Where(d => (mask & 1 << d) == 0).ToList();
                            miss = string.Join("|", m.Select(d => DirNames[d]));
                            status = nf == 0 ? "NOTHING" : m.Count > 0 ? "MISSING-FACE" : "drawn";
                        }
                    }
                    counts[status] = counts.GetValueOrDefault(status) + 1;
                    vis.WriteLine($"{s},{b.X},{b.Y},{b.Z},{lc.Item1},{lc.Item2},{lc.Item3},{b.Brick},{t},{xx},{zz},{status},{kind},{mask},{nf},{miss}");
                }
                Say($"scene {s}, LBA1's own picture: {placed.Count} bricks, " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => $"{c.Key} {c.Value}")) + " (cells showing 8+ pixels)");
            }
        }

        // ---- the road's corridor: the deck's width, from just over it to a car's height and to the camera's
        var corridor = new Dictionary<(int, int, int), (double Along, double Lateral, double Over, int Point)>();
        var pts = plan.Points; var hs = plan.Heights!; var halfs = plan.RaisedHalfs;
        for (var i = 0; i < pts.Length; i++)
        {
            var j = Math.Min(i + 1, pts.Length - 1); var k0 = Math.Max(0, j - 1);
            double dx = pts[j][0] - pts[k0][0], dz = pts[j][1] - pts[k0][1], len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-9) continue;
            double nx = -dz / len, nz = dx / len;
            var half = halfs is not null && i < halfs.Length ? halfs[i] : plan.RaisedHalf ?? 4.5;
            var lo = -half; var hiL = half;
            if (plan.RaisedStrips is { } strips && i < strips.Length && strips[i] is { Length: 2 } st) { lo = Math.Min(lo, st[0]); hiL = Math.Max(hiL, st[1]); }
            for (var off = lo; off <= hiL + 1e-9; off += 0.5)
            for (var over = 64.0; over <= 2600; over += 128)
            {
                var wpt = new Vector3((float)((pts[i][0] + nx * off) * 512), (float)(hs[i] + over), (float)((pts[i][1] + nz * off) * 512));
                var c = CellOf(Layout(wpt));
                if (!built.ContainsKey(c)) continue;
                if (!corridor.TryGetValue(c, out var prev) || over < prev.Over) corridor[c] = (i * 0.5, off, over, i);
            }
        }
        using (var cc = new StreamWriter(Path.Combine(outDir, "corridor.csv")))
        {
            cc.WriteLine("x,y,z,scene,block,brick,kind,wisp,faces,solid,over,lateral,point,routeX,routeZ,deck");
            foreach (var (c, v) in corridor.OrderBy(c => c.Value.Point))
            {
                var cell = built[c]; var i = Info(cell.Brick);
                faces.TryGetValue(c, out var f);
                cc.WriteLine($"{c.Item1},{c.Item2},{c.Item3},{cell.Scene},{cell.Block},{cell.Brick},{Kind(cell.Brick)},{(i.Max < TempleObjects.Wisp ? 1 : 0)},{f.N},{(CellSolid(c, i.Box) ? 1 : 0)},{v.Over},{v.Lateral:0.##},{v.Point},{pts[v.Point][0]:0.#},{pts[v.Point][1]:0.#},{hs[v.Point]:0}");
            }
        }
        Say($"built cells inside the road's corridor (deck width + slip lane, 64..2600 over the deck): {corridor.Count}; within a car's height (<= 960 over): {corridor.Count(c => c.Value.Over <= 960)}");

        log.Flush();
        if (runLog is not null) Rays(runLog, every, rw, rh, outDir, tPolys, rPolys, built, Info, Kind, faces, gone, layout, Say);
        log.Close();
        return 0;
    }

    // ------------------------------------------------------------------------------------------------------------------------------ rays
    private static void Rays(string runLog, int every, int W, int H, string outDir, List<Poly> tPolys, List<Poly> rPolys,
        Dictionary<(int X, int Y, int Z), PolarLayout.Cell> built,
        Func<int, (PolarTextures.Box Box, bool Whole, double Top, double X, double Z, double Max)> info, Func<int, string> kind,
        Dictionary<(int, int, int), (int N, int Mask, int SeeThrough)> faces, HashSet<(int, int, int)> gone, TempleLayout layout, Action<string> say)
    {
        // the grid the rays walk: layout cells over the island's cubes
        const int GX0 = -34, GX1 = 206, GY0 = -14, GY1 = 34, GZ0 = -24, GZ1 = 152;
        int NX = GX1 - GX0, NY = GY1 - GY0, NZ = GZ1 - GZ0;
        var all = tPolys.Concat(rPolys).ToList();
        // triangles
        var tris = new List<(Vector3 A, Vector3 B, Vector3 C, Vector3 N, int Poly)>();
        for (var i = 0; i < all.Count; i++)
        {
            var p = all[i];
            for (var k = 1; k + 1 < p.P.Length; k++) tris.Add((p.P[0], p.P[k], p.P[k + 1], p.N, i));
        }
        var lists = new List<int>?[NX * NY * NZ];
        int Idx(int x, int y, int z) => ((x - GX0) * NY + (y - GY0)) * NZ + (z - GZ0);
        for (var t = 0; t < tris.Count; t++)
        {
            var (a, b, c, _, _) = tris[t];
            var lo = Layout(Vector3.Min(a, Vector3.Min(b, c))); var hi = Layout(Vector3.Max(a, Vector3.Max(b, c)));
            for (var x = Math.Max(GX0, (int)Math.Floor(lo.X - 0.01)); x <= Math.Min(GX1 - 1, (int)Math.Floor(hi.X + 0.01)); x++)
            for (var y = Math.Max(GY0, (int)Math.Floor(lo.Y - 0.01)); y <= Math.Min(GY1 - 1, (int)Math.Floor(hi.Y + 0.01)); y++)
            for (var z = Math.Max(GZ0, (int)Math.Floor(lo.Z - 0.01)); z <= Math.Min(GZ1 - 1, (int)Math.Floor(hi.Z + 0.01)); z++)
                (lists[Idx(x, y, z)] ??= new()).Add(t);
        }
        // the cells LBA1 has as solid blocks (whole bricks, full boxes, not round things, models or wisps)
        var block = new bool[NX * NY * NZ];
        foreach (var (c, cell) in built)
        {
            if (c.X < GX0 || c.X >= GX1 || c.Y < GY0 || c.Y >= GY1 || c.Z < GZ0 || c.Z >= GZ1) continue;
            var i = info(cell.Brick);
            if (kind(cell.Brick) == "box" && i.Whole && i.Box.IsFull && i.Max >= TempleObjects.Wisp) block[Idx(c.X, c.Y, c.Z)] = true;
        }

        // the camera's frames
        var rx = new Regex(@"tick (\d+) .* eye (-?\d+),(-?\d+),(-?\d+) look (-?\d+),(-?\d+),(-?\d+) hero (-?\d+),(-?\d+),(-?\d+) cube (\d+),(\d+)");
        var frames = new List<(int Tick, Vector3 Eye, Vector3 Look)>();
        foreach (var line in File.ReadLines(runLog, System.Text.Encoding.Latin1))
        {
            if (!line.StartsWith("[INFO] [cam] tick")) continue;
            var m = rx.Match(line);
            if (!m.Success) continue;
            var t = int.Parse(m.Groups[1].Value);
            float cx = int.Parse(m.Groups[11].Value) * 32768f, cz = int.Parse(m.Groups[12].Value) * 32768f;
            frames.Add((t, new Vector3(int.Parse(m.Groups[2].Value) + cx, int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value) + cz),
                new Vector3(int.Parse(m.Groups[5].Value) + cx, int.Parse(m.Groups[6].Value), int.Parse(m.Groups[7].Value) + cz)));
        }
        var maxTick = int.Parse(Environment.GetEnvironmentVariable("AUDIT_MAXTICK") ?? "2900");
        var minTick = int.Parse(Environment.GetEnvironmentVariable("AUDIT_MINTICK") ?? "120");
        var use = frames.Where(f => f.Tick >= minTick && f.Tick <= maxTick && f.Tick % every == 0).GroupBy(f => f.Tick).Select(g => g.First()).ToList();
        say($"rays: {tris.Count} triangles ({tPolys.Count} temple polygons, {rPolys.Count} road/decor), {use.Count} frames of {frames.Count}, {W}x{H}");

        const float Far = 32000f, FogStart = 12000f;
        // (the engine's exterior focal is 600 at the screenshot's width, 640 by default: EXTFUNC.CPP SetProjection ChampX/ChampZ)
        var f0 = 600f * W / float.Parse(Environment.GetEnvironmentVariable("AUDIT_REFW") ?? "640");
        // per hole cell: frames seen, pixels, by entry direction; per frame: hole share
        var holeCells = new System.Collections.Concurrent.ConcurrentDictionary<(int, int, int, int), (int Px, int Frames, float MinDist)>();
        var frameRows = new List<string>();
        var shots = Environment.GetEnvironmentVariable("AUDIT_SHOTS")?.Split(',').Select(int.Parse).ToHashSet() ?? new HashSet<int>();
        foreach (var (tick, eye, look) in use)
        {
            var fwd = Vector3.Normalize(look - eye);
            var right = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitY));
            var up = Vector3.Cross(right, fwd);
            var res = new (int Kind, float Dist, int Hx, int Hy, int Hz, int Dir)[W * H];
            var eyeCell = CellOf(Layout(eye));
            var eyeInBlock = eyeCell.Item1 >= GX0 && eyeCell.Item1 < GX1 && eyeCell.Item2 >= GY0 && eyeCell.Item2 < GY1 && eyeCell.Item3 >= GZ0 && eyeCell.Item3 < GZ1 && block[Idx(eyeCell.Item1, eyeCell.Item2, eyeCell.Item3)];
            Parallel.For(0, H, py =>
            {
                for (var px = 0; px < W; px++)
                {
                    var u = (px + 0.5f - W / 2f) / f0; var v = -(py + 0.5f - H / 2f) / f0;
                    var d = Vector3.Normalize(fwd + right * u + up * v);
                    res[py * W + px] = Cast(eye, d);
                }
            });
            (int Kind, float Dist, int Hx, int Hy, int Hz, int Dir) Cast(Vector3 o, Vector3 d)
            {
                // DDA in layout cells (cell 1536 x 768 x 1536 world units)
                var lo = Layout(o);
                var dl = new Vector3(d.X / 1536f, d.Y / 768f, d.Z / 1536f); // layout units per world unit along the ray
                int x = (int)Math.Floor(lo.X), y = (int)Math.Floor(lo.Y), z = (int)Math.Floor(lo.Z);
                int sx = Math.Sign(dl.X), sy = Math.Sign(dl.Y), sz = Math.Sign(dl.Z);
                float tdx = sx != 0 ? 1f / Math.Abs(dl.X) : float.MaxValue, tdy = sy != 0 ? 1f / Math.Abs(dl.Y) : float.MaxValue, tdz = sz != 0 ? 1f / Math.Abs(dl.Z) : float.MaxValue;
                float tmx = sx > 0 ? (x + 1 - lo.X) * tdx : sx < 0 ? (lo.X - x) * tdx : float.MaxValue;
                float tmy = sy > 0 ? (y + 1 - lo.Y) * tdy : sy < 0 ? (lo.Y - y) * tdy : float.MaxValue;
                float tmz = sz > 0 ? (z + 1 - lo.Z) * tdz : sz < 0 ? (lo.Z - z) * tdz : float.MaxValue;
                float tIn = 0; var entry = -1;
                var hole = (Kind: 0, Dist: 0f, Hx: 0, Hy: 0, Hz: 0, Dir: -1);
                var first = true;
                while (tIn < Far)
                {
                    var tOut = Math.Min(tmx, Math.Min(tmy, tmz));
                    if (x >= GX0 && x < GX1 && y >= GY0 && y < GY1 && z >= GZ0 && z < GZ1)
                    {
                        var list = lists[Idx(x, y, z)];
                        var best = float.MaxValue;
                        if (list is not null)
                            foreach (var ti in list)
                            {
                                var (a, b, c, n, _) = tris[ti];
                                if (Vector3.Dot(n, d) >= 0) continue;   // back face
                                var e1 = b - a; var e2 = c - a; var pv = Vector3.Cross(d, e2); var det = Vector3.Dot(e1, pv);
                                if (Math.Abs(det) < 1e-9f) continue;
                                var inv = 1f / det; var tv = o - a; var uu = Vector3.Dot(tv, pv) * inv;
                                if (uu < -1e-4f || uu > 1.0001f) continue;
                                var qv = Vector3.Cross(tv, e1); var vv = Vector3.Dot(d, qv) * inv;
                                if (vv < -1e-4f || uu + vv > 1.0001f) continue;
                                var tt = Vector3.Dot(e2, qv) * inv;
                                if (tt > 1f && tt < best && tt >= tIn - 2f && tt <= tOut + 2f) best = tt;
                            }
                        if (best < float.MaxValue)
                            return hole.Kind != 0 ? (2, best, hole.Hx, hole.Hy, hole.Hz, hole.Dir) : (1, best, x, y, z, -1);
                        // (through a cell LBA1 has as a solid block, a stretch of it, with nothing hit: a hole -- unless the eye is in it)
                        if (hole.Kind == 0 && block[Idx(x, y, z)] && tOut - tIn > 96f && !(first && eyeInBlock))
                            hole = (3, tIn, x, y, z, entry);
                    }
                    first = false;
                    tIn = tOut;
                    if (tmx <= tmy && tmx <= tmz) { x += sx; tmx += tdx; entry = sx > 0 ? 2 : 1; }      // entered through its -x side (moving +x) ...
                    else if (tmy <= tmz) { y += sy; tmy += tdy; entry = sy > 0 ? 5 : 0; }
                    else { z += sz; tmz += tdz; entry = sz > 0 ? 4 : 3; }
                }
                return hole.Kind != 0 ? (3, Far, hole.Hx, hole.Hy, hole.Hz, hole.Dir) : (0, Far, 0, 0, 0, -1);
            }
            int holes = 0, holesNear = 0, sky = 0;
            var seenThisFrame = new Dictionary<(int, int, int, int), (int Px, float Dist)>();
            foreach (var r in res)
            {
                if (r.Kind == 0) sky++;
                if (r.Kind is 2 or 3)
                {
                    holes++;
                    var key = (r.Hx, r.Hy, r.Hz, r.Dir);
                    var dist = Vector3.Distance(eye, World(r.Hx + 0.5, r.Hy + 0.5, r.Hz + 0.5));
                    if (dist < FogStart) holesNear++;
                    seenThisFrame.TryGetValue(key, out var sf);
                    seenThisFrame[key] = (sf.Px + 1, dist);
                }
            }
            foreach (var (k, v) in seenThisFrame)
                holeCells.AddOrUpdate(k, (v.Px, 1, v.Dist), (_, o) => (o.Px + v.Px, o.Frames + 1, Math.Min(o.MinDist, v.Dist)));
            frameRows.Add($"{tick},{eye.X:0},{eye.Y:0},{eye.Z:0},{(eyeInBlock ? 1 : 0)},{100.0 * holes / res.Length:0.###},{100.0 * holesNear / res.Length:0.###},{100.0 * sky / res.Length:0.##}");
            if (shots.Contains(tick))
            {
                // a false-colour picture: grey by distance where hit; red where a hole (through a solid LBA1 block); black where nothing
                var bgra = new byte[W * H * 4];
                for (var q = 0; q < res.Length; q++)
                {
                    var r = res[q]; byte R = 0, G = 0, B = 0;
                    if (r.Kind == 1) { var g = (byte)Math.Clamp(255 - r.Dist / 32000f * 230, 25, 255); R = G = B = g; }
                    else if (r.Kind is 2 or 3) { R = 255; G = (byte)(r.Kind == 2 ? 80 : 0); B = (byte)(r.Kind == 2 ? 80 : 0); }
                    bgra[q * 4] = B; bgra[q * 4 + 1] = G; bgra[q * 4 + 2] = R; bgra[q * 4 + 3] = 255;
                }
                PngWriter.Write(Path.Combine(outDir, $"rays_t{tick}.png"), bgra, W, H);
            }
        }
        File.WriteAllLines(Path.Combine(outDir, "rays_frames.csv"), new[] { "tick,eyeX,eyeY,eyeZ,eyeInBlock,holePct,holeNearPct,nothingPct" }.Concat(frameRows));
        using (var hc = new StreamWriter(Path.Combine(outDir, "rays_holes.csv")))
        {
            hc.WriteLine("x,y,z,entry,px,frames,minDist,scene,block,brick,kind,covTop,covX,covZ,portMask,exposed,neighbourIn");
            foreach (var ((x, y, z, d), v) in holeCells.OrderByDescending(h => h.Value.Px))
            {
                var cell = built[(x, y, z)]; var i = info(cell.Brick);
                faces.TryGetValue((x, y, z), out var f);
                var exposed = 0;
                for (var k = 0; k < 6; k++) if (!built.ContainsKey((x + Dirs[k].Dx, y + Dirs[k].Dy, z + Dirs[k].Dz))) exposed |= 1 << k;
                var nb = d >= 0 && built.TryGetValue((x + Dirs[d].Dx, y + Dirs[d].Dy, z + Dirs[d].Dz), out var n) ? $"{n.Brick}:{kind(n.Brick)}" : "";
                hc.WriteLine($"{x},{y},{z},{(d >= 0 ? DirNames[d] : "?")},{v.Px},{v.Frames},{v.MinDist:0},{cell.Scene},{cell.Block},{cell.Brick},{kind(cell.Brick)},{i.Top:0.##},{i.X:0.##},{i.Z:0.##},{f.Mask},{exposed},{nb}");
            }
        }
        var rows = frameRows.Select(r => r.Split(',')).ToList();
        say($"rays: frames with any hole pixel {rows.Count(r => double.Parse(r[5]) > 0)} of {rows.Count}; mean hole share {rows.Average(r => double.Parse(r[5])):0.###} % (within fog start {rows.Average(r => double.Parse(r[6])):0.###} %), " +
            $"max {rows.Max(r => double.Parse(r[5])):0.##} %; eye inside an LBA1 solid block in {rows.Count(r => r[4] == "1")} frames; hole cells {holeCells.Count}");
    }

    // ------------------------------------------------------------------------------------------------------------------------------ lba1angle
    // Each scene as LBA1 draws it (alone, its bricks far to near), and the port's polygons of that scene's part of the island drawn from the
    // same angle (orthographic, LBA1's projection, z-buffered, textured from the island's object pages, see-through texels left out, back
    // faces culled) -- pixel by pixel: the same colour (within 30 RGB), another colour, or nothing (a hole); pixels of cells the road's cuts
    // took are left out of both.
    private static int Lba1Angle(string[] args)
    {
        string lba1 = args[2], gameDir = args[3], planPath = args[4], outDir = args[5];
        Directory.CreateDirectory(outDir);
        var game = new Lba1Game(lba1);
        var layout = Layout(game);
        var plan = RaceTrackPlan.Load(planPath);
        var cut = (plan.TempleCuts ?? Array.Empty<int[]>()).Where(c => c.Length == 3).Select(c => (c[0], c[1], c[2])).ToHashSet();
        var shapes = TempleShapes.Find(game);
        var remains = TempleShapes.CutRemains(shapes, layout.Cells, cut);
        var models = TempleModels.Find(game);
        var modelRemains = TempleModels.CutRemains(models, layout.Cells, cut.Concat(remains).ToHashSet());
        var gone = cut.Concat(remains).Concat(modelRemains).ToHashSet();
        string Kind(int brick) => shapes.TryGetValue(brick, out var p) ? "round:" + p.Shape.Kind.Name : models.TryGetValue(brick, out var m) ? "model:" + m.Model.Kind.Name : "box";
        var obl = HqrArchive.Open(Path.Combine(gameDir, "TEMPLE.OBL"));
        var bak = Path.Combine(gameDir, "TEMPLE.ILE.bak");
        var ilePath = File.Exists(bak) ? bak : Path.Combine(gameDir, "TEMPLE.ILE");
        var island = IslandFile.Load(ilePath);
        var (polys, _, _, _) = Decode(ilePath, obl, null, true);
        var ipal = IslandMapRenderer.LoadPaletteEntry(gameDir, TempleIsland.PaletteEntry);
        var lpal = game.Palette;
        var area = game.Areas.First(a => a.Tiles.Any(t => t.Scene == 8));
        var sprites = new Dictionary<int, PolarTextures.Sprite>();
        PolarTextures.Sprite Spr(int b) => sprites.TryGetValue(b, out var s) ? s : sprites[b] = PolarTextures.Sprite.Decode(game.ReadBrick(b));
        var view = new Vector3(1536f, 1228.8f, 1536f);
        using var csv = new StreamWriter(Path.Combine(outDir, "lba1angle_cells.csv"));
        csv.WriteLine("scene,x,y,z,lx,ly,lz,brick,kind,px,match,differ,hole");
        foreach (var s in Scenes)
        {
            var tile = area.Tiles.First(t => t.Scene == s);
            int ox = tile.OffsetX / 512, oy = tile.OffsetY / 256, oz = tile.OffsetZ / 512;
            var placed = PolarLayout.Bricks(game, s).OrderBy(b => b.Z).ThenBy(b => b.X).ThenBy(b => b.Y).ToList();
            const int W = 64 * 48 + 200, H = 64 * 24 + 25 * 15 + 260, OX = 64 * 24 + 100, OY = 25 * 15 + 130;
            var id = new int[W * H]; Array.Fill(id, -1);
            var col = new int[W * H];
            for (var k = 0; k < placed.Count; k++)
            {
                var b = placed[k]; var sp = Spr(b.Brick);
                int ax = 24 * (b.X - b.Z) + OX, ay = 12 * (b.X + b.Z) - 15 * b.Y + OY;
                for (var py = 0; py < sp.Lines; py++)
                for (var px = 0; px < sp.Width; px++)
                {
                    var c = sp.Pixels[py * sp.Width + px];
                    if (c < 0) continue;
                    int X = ax + px + sp.HotX, Y = ay + py + sp.HotY;
                    if (X < 0 || Y < 0 || X >= W || Y >= H) continue;
                    id[Y * W + X] = k; col[Y * W + X] = c;
                }
            }
            // the port
            var zb = new float[W * H]; Array.Fill(zb, float.MinValue);
            var pc = new int[W * H]; Array.Fill(pc, -1);
            foreach (var poly in polys)
            {
                // (its middle a hair inside the cell it is a face of: a face on a scene's last row lies on the next one's first)
                var cen = Layout(poly.P.Aggregate(Vector3.Zero, (a, b) => a + b) / poly.P.Length - poly.N * 2);
                if (cen.X < ox || cen.X >= ox + 64 || cen.Z < oz || cen.Z >= oz + 64) continue;
                if (Vector3.Dot(poly.N, view) <= 0) continue;
                var page = poly.Page >= 0 ? island.ObjectPage(poly.Page) : null;
                var sp = poly.P.Select(w =>
                {
                    var q = Layout(w); float X = q.X - ox, Y = q.Y - oy, Z = q.Z - oz;
                    return (Sx: OX + 24 * (X - Z) + 24, Sy: OY + 12 * (X + Z) - 15 * Y + 15, D: X + Z + 1.6f * Y);
                }).ToArray();
                for (var k = 1; k + 1 < sp.Length; k++)
                {
                    int[] vi = { 0, k, k + 1 };
                    var (a, b, c) = (sp[0], sp[k], sp[k + 1]);
                    var area2 = (b.Sx - a.Sx) * (c.Sy - a.Sy) - (b.Sy - a.Sy) * (c.Sx - a.Sx);
                    if (Math.Abs(area2) < 1e-6) continue;
                    int x0 = Math.Max(0, (int)Math.Floor(Math.Min(a.Sx, Math.Min(b.Sx, c.Sx)))), x1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(a.Sx, Math.Max(b.Sx, c.Sx))));
                    int y0 = Math.Max(0, (int)Math.Floor(Math.Min(a.Sy, Math.Min(b.Sy, c.Sy)))), y1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(a.Sy, Math.Max(b.Sy, c.Sy))));
                    for (var y = y0; y <= y1; y++)
                    for (var x = x0; x <= x1; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        var w0 = ((b.Sx - px) * (c.Sy - py) - (b.Sy - py) * (c.Sx - px)) / area2;
                        var w1 = ((c.Sx - px) * (a.Sy - py) - (c.Sy - py) * (a.Sx - px)) / area2;
                        var w2 = 1 - w0 - w1;
                        if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                        var depth = w0 * a.D + w1 * b.D + w2 * c.D;
                        var q = y * W + x;
                        if (depth <= zb[q]) continue;
                        var texel = 0;
                        if (page is not null && poly.Uv is { } uv)
                        {
                            float u = w0 * uv[vi[0] * 2] + w1 * uv[vi[1] * 2] + w2 * uv[vi[2] * 2];
                            float v = w0 * uv[vi[0] * 2 + 1] + w1 * uv[vi[1] * 2 + 1] + w2 * uv[vi[2] * 2 + 1];
                            texel = page[((poly.Ty + (int)Math.Floor(v / 256)) & 255) * 256 + ((poly.Tx + (int)Math.Floor(u / 256)) & 255)];
                        }
                        if (poly.Type == 12 && texel == 0) continue;
                        zb[q] = depth; pc[q] = texel;
                    }
                }
            }
            // compare
            var stats = new Dictionary<int, (int Px, int Match, int Differ, int Hole)>();
            long match = 0, differ = 0, hole = 0, extra = 0, cutPx = 0;
            var img = new byte[W * H * 4 * 3];
            int bx0 = W, by0 = H, bx1 = 0, by1 = 0;
            void Put(int panel, int q, (int R, int G, int B) c)
            {
                int x = q % W, y = q / W, o = (y * W * 3 + panel * W + x) * 4;
                img[o] = (byte)c.B; img[o + 1] = (byte)c.G; img[o + 2] = (byte)c.R; img[o + 3] = 255;
            }
            for (var q = 0; q < id.Length; q++)
            {
                if (id[q] < 0 && pc[q] < 0) continue;
                int x = q % W, y = q / W;
                bx0 = Math.Min(bx0, x); bx1 = Math.Max(bx1, x); by0 = Math.Min(by0, y); by1 = Math.Max(by1, y);
                if (id[q] >= 0) Put(0, q, PolarTextures.Colours.Rgb(lpal, col[q]));
                if (pc[q] >= 0) Put(1, q, PolarTextures.Colours.Rgb(ipal, pc[q]));
                if (id[q] >= 0)
                {
                    var b = placed[id[q]];
                    if (gone.Contains((b.X + ox, b.Y + oy, b.Z + oz))) { cutPx++; Put(2, q, (60, 60, 60)); continue; }
                    stats.TryGetValue(id[q], out var st); st.Px++;
                    if (pc[q] < 0) { hole++; st.Hole++; Put(2, q, (255, 0, 0)); }
                    else
                    {
                        // (the nearest colour the port draws within a pixel of it: the 32-texel tiles resample LBA1's 48-pixel bricks)
                        var (r1, g1, b1) = PolarTextures.Colours.Rgb(lpal, col[q]);
                        var dist = double.MaxValue;
                        for (var dy = -1; dy <= 1; dy++)
                            for (var dx = -1; dx <= 1; dx++)
                            {
                                var nq = q + dy * W + dx;
                                if (nq < 0 || nq >= pc.Length || pc[nq] < 0) continue;
                                var (r2, g2, b2) = PolarTextures.Colours.Rgb(ipal, pc[nq]);
                                dist = Math.Min(dist, Math.Sqrt((r1 - r2) * (r1 - r2) + (g1 - g2) * (g1 - g2) + (b1 - b2) * (b1 - b2)));
                            }
                        if (dist <= 30) { match++; st.Match++; Put(2, q, (0, 90, 0)); } else { differ++; st.Differ++; Put(2, q, (255, 220, 0)); }
                    }
                    stats[id[q]] = st;
                }
                else { extra++; Put(2, q, (60, 120, 255)); }
            }
            foreach (var (k, st) in stats.OrderBy(k => k.Key))
            {
                var b = placed[k];
                csv.WriteLine($"{s},{b.X},{b.Y},{b.Z},{b.X + ox},{b.Y + oy},{b.Z + oz},{b.Brick},{Kind(b.Brick)},{st.Px},{st.Match},{st.Differ},{st.Hole}");
            }
            var total = match + differ + hole;
            Console.WriteLine($"scene {s} from LBA1's angle: {total} LBA1 pixels compared ({cutPx} of cut cells left out): same colour {100.0 * match / total:0.00} %, another colour {100.0 * differ / total:0.00} %, " +
                              $"nothing drawn {100.0 * hole / total:0.00} %; the port draws {extra} pixels where LBA1 draws none");
            // the picture, cropped: LBA1 | port | (green same, yellow other colour, red nothing, blue port only, grey cut)
            int cw = bx1 - bx0 + 1, ch = by1 - by0 + 1;
            Console.WriteLine($"  lba1angle_{s}.png: 3 panels of {cw} x {ch}; a scene cell (x, y, z)'s brick anchor is at ({24 + OX - bx0} + 24 (x - z), {15 + OY - by0} + 12 (x + z) - 15 y) in each panel");
            var crop = new byte[cw * 3 * ch * 4];
            for (var y = 0; y < ch; y++)
                for (var panel = 0; panel < 3; panel++)
                    Array.Copy(img, ((y + by0) * W * 3 + panel * W + bx0) * 4, crop, (y * cw * 3 + panel * cw) * 4, cw * 4);
            PngWriter.Write(Path.Combine(outDir, $"lba1angle_{s}.png"), crop, cw * 3, ch);
        }
        return 0;
    }

    // ------------------------------------------------------------------------------------------------------------------------------ overlay
    // overlay <shots dir> <rays dir> <out dir> <tick...>: the engine's screenshot and beside it the same with the ray cast's hole pixels red
    private static int Overlay(string[] args)
    {
        string shots = args[2], rays = args[3], outDir = args[4];
        Directory.CreateDirectory(outDir);
        foreach (var t in args.Skip(5))
        {
            var (s, sw, sh, sb) = ReadPng(Path.Combine(shots, $"t{t}.png"));
            var (r, rw, rh, rb) = ReadPng(Path.Combine(rays, $"rays_t{t}.png"));
            var outW = sw * 2 + 8;
            var img = new byte[outW * sh * 4];
            Array.Fill(img, (byte)255);
            for (var y = 0; y < sh; y++)
                for (var x = 0; x < sw; x++)
                {
                    var si = (y * sw + x) * sb;
                    byte R = s[si], G = s[si + 1], B = s[si + 2];
                    var o1 = (y * outW + x) * 4; img[o1] = B; img[o1 + 1] = G; img[o1 + 2] = R; img[o1 + 3] = 255;
                    var ri = ((y * rh / sh) * rw + x * rw / sw) * rb;
                    var red = r[ri] == 255 && r[ri + 1] <= 80 && r[ri + 2] <= 80;
                    var o2 = (y * outW + sw + 8 + x) * 4;
                    img[o2] = red ? (byte)(B / 3) : B; img[o2 + 1] = red ? (byte)(G / 3) : G; img[o2 + 2] = red ? (byte)((R + 510) / 3) : R; img[o2 + 3] = 255;
                }
            PngWriter.Write(Path.Combine(outDir, $"overlay_t{t}.png"), img, outW, sh);
            Console.WriteLine(Path.Combine(outDir, $"overlay_t{t}.png"));
        }
        return 0;
    }

    // sprites <LBA1> <out dir> <n...>: SPRITES.HQR entries each to its own picture (x2), with its size and hot spot
    private static int Sprites(string[] args)
    {
        var dir = args[2]; Directory.CreateDirectory(args[3]);
        var palette = HqrArchive.Open(Path.Combine(dir, "RESS.HQR")).Read(0);
        var sprites = HqrArchive.Open(Path.Combine(dir, "SPRITES.HQR"));
        foreach (var n in args.Skip(4).Select(int.Parse))
        {
            var entry = sprites.Read(n);
            var start = (int)BitConverter.ToUInt32(entry, 0);
            var data = entry[start..];
            int w = data[0], h = data[1];
            var bgra = new byte[w * h * 4];
            Lba1GridRenderer.Blit(bgra, w, h, data, 0, 0, palette);
            var big = new byte[w * 2 * h * 2 * 4];
            for (var y = 0; y < h * 2; y++) for (var x = 0; x < w * 2; x++)
            {
                var s = ((y / 2) * w + x / 2) * 4; var o = (y * w * 2 + x) * 4;
                if (bgra[s + 3] == 0) { big[o] = 40; big[o + 1] = 40; big[o + 2] = 40; big[o + 3] = 255; continue; }
                Array.Copy(bgra, s, big, o, 4);
            }
            PngWriter.Write(Path.Combine(args[3], $"sprite_{n}.png"), big, w * 2, h * 2);
            Console.WriteLine($"sprite {n}: {w} x {h}, hot spot ({(sbyte)data[2]}, {(sbyte)data[3]}), entry {entry.Length} bytes (offset table {start})");
        }
        return 0;
    }

    // crop <png> <out> <x> <y> <w> <h> [scale] [panels panelWidth]: a part of a picture (with panels: the same part of each of the side-by-side panels)
    private static int Crop(string[] args)
    {
        var (d, w, h, bpp) = ReadPng(args[2]);
        int x0 = int.Parse(args[4]), y0 = int.Parse(args[5]), cw = int.Parse(args[6]), ch = int.Parse(args[7]);
        var k = args.Length > 8 ? int.Parse(args[8]) : 1;
        int panels = args.Length > 9 ? int.Parse(args[9]) : 1, pw = args.Length > 10 ? int.Parse(args[10]) : 0;
        var ow = (cw * k + 4) * panels;
        var img = new byte[ow * ch * k * 4];
        Array.Fill(img, (byte)255);
        for (var p = 0; p < panels; p++)
            for (var y = 0; y < ch * k; y++)
                for (var x = 0; x < cw * k; x++)
                {
                    int sx = x0 + p * pw + x / k, sy = y0 + y / k;
                    if (sx < 0 || sy < 0 || sx >= w || sy >= h) continue;
                    var s = (sy * w + sx) * bpp; var o = (y * ow + p * (cw * k + 4) + x) * 4;
                    img[o] = d[s + 2]; img[o + 1] = d[s + 1]; img[o + 2] = d[s]; img[o + 3] = 255;
                }
        PngWriter.Write(args[3], img, ow, ch * k);
        return 0;
    }

    // RGB(A) of a PNG (8-bit RGB / RGBA): bytes, width, height, bytes a pixel (R, G, B first)
    private static (byte[] Data, int W, int H, int Bpp) ReadPng(string path)
    {
        var png = File.ReadAllBytes(path);
        int pos = 8, w = 0, h = 0, colour = 0;
        using var idat = new MemoryStream();
        while (pos + 8 <= png.Length)
        {
            var len = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
            var type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            if (type == "IHDR") { w = (png[pos + 8] << 24) | (png[pos + 9] << 16) | (png[pos + 10] << 8) | png[pos + 11]; h = (png[pos + 12] << 24) | (png[pos + 13] << 16) | (png[pos + 14] << 8) | png[pos + 15]; colour = png[pos + 17]; }
            else if (type == "IDAT") idat.Write(png, pos + 8, len);
            pos += 12 + len;
        }
        var bpp = colour == 6 ? 4 : 3;
        idat.Position = 0;
        using var z = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress);
        var raw = new MemoryStream(); z.CopyTo(raw);
        var data = raw.ToArray(); var stride = w * bpp; var rows = new byte[h * stride];
        for (var y = 0; y < h; y++)
        {
            var filter = data[y * (stride + 1)];
            for (var x = 0; x < stride; x++)
            {
                int cur = data[y * (stride + 1) + 1 + x];
                int a = x >= bpp ? rows[y * stride + x - bpp] : 0, b = y > 0 ? rows[(y - 1) * stride + x] : 0, c = x >= bpp && y > 0 ? rows[(y - 1) * stride + x - bpp] : 0;
                int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                var v = filter switch { 0 => cur, 1 => cur + a, 2 => cur + b, 3 => cur + ((a + b) >> 1), _ => cur + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c) };
                rows[y * stride + x] = (byte)v;
            }
        }
        return (rows, w, h, bpp);
    }
}
