using System.Numerics;
using LBAAssembler;
using LBAAssembler.Lba1;
using LBAAssembler.Terrain;
using LBAAssembler.Terrain.Temple;
using LbaBodyStudio;

namespace ScriptRoundTrip;

// MEASURING PROBE (2026-10-18, not product code): what of the Temple's drawn polygons reaches into the race road's deck.
//   templeclip <game dir> <raceraised.txt> <out dir> [LBA1 dir] [plan.json]
// The race build's TEMPLE.ILE decoded (TEMPLE.ILE.bak: the temple alone -- the temple's own decors and LBA1's actors; the rest: the race's
// road, gantry and decor), every polygon sampled every 32 units, each sample against the raised road (raceraised.txt: x z y half [bank lo
// hi]: the deck's surface between consecutive points, its width rail to rail, the slip lane's strip). A sample is IN the deck's room when it
// is over the deck's width (or the strip) and from BELOW (default 120) under its surface to 1,000 over it -- and, with CLIP_INSIDE (units, 0
// by default), at least that far inside the deck's edges or the strip's: walls standing at a road's edge, where it has no rail and nothing is
// cut back from it (the Temple of Bú's ground stretch, 2026-10-19), or round a floor beside it, are not in it.
// -> clip.csv: one row per decor reaching in (its body, place, kind -- box/round/model by the LBA1 cell its samples are in, actor, race --,
//    how far over the surface its highest sample is, how many samples over / just under, where: island cell, road point, across)
internal static class TempleClipProbe
{
    private sealed record Seg(int K, Vector2 A, Vector2 B, float Ya, float Yb, float Half, float Lo, float Hi);

    public static int Run(string[] args)
    {
        string gameDir = args[1], raisedPath = args[2], outDir = args[3];
        var lba1 = args.Length > 4 ? args[4] : null;
        var planPath = args.Length > 5 ? args[5] : null;
        var below = double.Parse(Environment.GetEnvironmentVariable("CLIP_BELOW") ?? "120");
        var over = double.Parse(Environment.GetEnvironmentVariable("CLIP_OVER") ?? "1000");
        var step = float.Parse(Environment.GetEnvironmentVariable("CLIP_STEP") ?? "32");
        var actorsFrom = int.Parse(Environment.GetEnvironmentVariable("CLIP_ACTORS") ?? "1915");
        var inside = float.Parse(Environment.GetEnvironmentVariable("CLIP_INSIDE") ?? "0");
        Directory.CreateDirectory(outDir);

        // ---- the road
        var rows = File.ReadAllLines(raisedPath).Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()).ToList();
        var segs = new List<Seg>();
        for (var k = 0; k + 1 < rows.Count; k++)
        {
            int[] a = rows[k], b = rows[k + 1];
            if (a[3] <= 0 || b[3] <= 0) continue;
            var pa = new Vector2(a[0], a[1]); var pb = new Vector2(b[0], b[1]);
            if ((pb - pa).Length() > 1024 || (pb - pa).Length() < 1) continue;
            float lo = a.Length >= 7 ? Math.Min(a[5], b.Length >= 7 ? b[5] : a[5]) : 0, hi = a.Length >= 7 ? Math.Max(a[6], b.Length >= 7 ? b[6] : a[6]) : 0;
            segs.Add(new Seg(k, pa, pb, a[2], b[2], Math.Max(a[3], b[3]), lo, hi));
        }
        var grid = new Dictionary<(int, int), List<Seg>>();
        foreach (var s in segs)
        {
            var reach = Math.Max(s.Half, Math.Max(Math.Abs(s.Lo), Math.Abs(s.Hi))) + 64;
            int x0 = (int)Math.Floor((Math.Min(s.A.X, s.B.X) - reach) / 512), x1 = (int)Math.Floor((Math.Max(s.A.X, s.B.X) + reach) / 512);
            int z0 = (int)Math.Floor((Math.Min(s.A.Y, s.B.Y) - reach) / 512), z1 = (int)Math.Floor((Math.Max(s.A.Y, s.B.Y) + reach) / 512);
            for (var x = x0; x <= x1; x++)
                for (var z = z0; z <= z1; z++)
                {
                    if (!grid.TryGetValue((x, z), out var l)) grid[(x, z)] = l = new List<Seg>();
                    l.Add(s);
                }
        }
        Console.WriteLine($"road: {rows.Count} points, {segs.Count} deck segments");

        // the deck's room at a point: (height over the surface, road point, across), the one nearest the surface
        (double Over, int K, double Across, bool Strip)? Deck(Vector3 p)
        {
            if (!grid.TryGetValue(((int)Math.Floor(p.X / 512), (int)Math.Floor(p.Z / 512)), out var l)) return null;
            (double Over, int K, double Across, bool Strip)? best = null;
            foreach (var s in l)
            {
                var d = s.B - s.A; var len = d.Length(); var u = d / len;
                var q = new Vector2(p.X, p.Z) - s.A;
                var t = Vector2.Dot(q, u);
                if (t < -1 || t > len + 1) continue;
                var across = -q.X * u.Y + q.Y * u.X;
                var onDeck = Math.Abs(across) <= s.Half - inside;
                var onStrip = !onDeck && s.Hi > s.Lo && across >= s.Lo + inside && across <= s.Hi - inside;
                if (!onDeck && !onStrip) continue;
                var y = s.Ya + (s.Yb - s.Ya) * Math.Clamp(t / len, 0, 1);
                var o = p.Y - y;
                if (o < -below || o > over) continue;
                if (best is null || Math.Abs(o) < Math.Abs(best.Value.Over)) best = (o, s.K, across, onStrip);
            }
            return best;
        }

        // ---- the island's decors
        var obl = HqrArchive.Open(Path.Combine(gameDir, "TEMPLE.OBL"));
        var bak = Path.Combine(gameDir, "TEMPLE.ILE.bak");
        var templeKeys = File.Exists(bak) ? Keys(IslandFile.Load(bak)) : new HashSet<(int, int, int, int)>();
        var island = IslandFile.Load(Path.Combine(gameDir, "TEMPLE.ILE"));

        // the LBA1 cells (kind of what a sample is in)
        Lba1Game? game = lba1 is null ? null : new Lba1Game(lba1);
        TempleLayout? layout = game is null ? null : TempleLayout.Build(game);
        Dictionary<int, TempleShapes.Part>? shapes = game is null ? null : TempleShapes.Find(game);
        Dictionary<int, TempleModels.Part>? models = game is null ? null : TempleModels.Find(game);
        HashSet<(int, int, int)>? cut = null;
        if (planPath is not null) cut = (RaceTrackPlan.Load(planPath).TempleCuts ?? Array.Empty<int[]>()).Where(c => c.Length == 3).Select(c => (c[0], c[1], c[2])).ToHashSet();
        string CellKind(Vector3 w)
        {
            if (layout is null) return "";
            var l = new Vector3((w.X / 512f - TempleFrame.OffsetX) / TempleFrame.Scale, (w.Y - TempleFrame.BaseY) / 768f - 8, (w.Z / 512f - TempleFrame.OffsetZ) / TempleFrame.Scale);
            var c = ((int)Math.Floor(l.X), (int)Math.Floor(l.Y), (int)Math.Floor(l.Z));
            var own = $"in {c.Item1}/{c.Item2}/{c.Item3}{(layout.Cells.TryGetValue(c, out var oc) ? $" brick {oc.Brick}" : " (empty)")}{(cut?.Contains(c) == true ? " CUT" : "")}: ";
            // (the cell the sample is in, or the nearest one round it that holds something)
            foreach (var (dx, dy, dz) in new[] { (0, 0, 0), (0, -1, 0), (1, 0, 0), (-1, 0, 0), (0, 0, 1), (0, 0, -1), (0, 1, 0) })
            {
                var cc = (c.Item1 + dx, c.Item2 + dy, c.Item3 + dz);
                if (!layout.Cells.TryGetValue(cc, out var cell)) continue;
                var kind = shapes!.TryGetValue(cell.Brick, out var p) ? "round:" + p.Shape.Kind.Name : models!.TryGetValue(cell.Brick, out var m) ? "model:" + m.Model.Kind.Name : "box";
                return own + $"{kind} brick {cell.Brick} scene {cell.Scene} cell {cc.Item1}/{cc.Item2}/{cc.Item3}{(dx != 0 || dy != 0 || dz != 0 ? " (beside)" : "")}{(cut?.Contains(cc) == true ? " CUT" : "")}";
            }
            return $"none at {c.Item1}/{c.Item2}/{c.Item3}";
        }

        var bodies = new Dictionary<int, Body?>();
        var seen = new HashSet<(int, int, int, int)>();
        var hits = new List<string>();
        int decors = 0, polys = 0, samples = 0, inOver = 0, inUnder = 0;
        var underHist = new int[(int)(below / 10) + 1];
        for (var cz = 0; cz < 16; cz++)
            for (var cx = 0; cx < 16; cx++)
            {
                if (island.CubeAt(cx, cz) is not { } cube) continue;
                foreach (var d in cube.Decors)
                {
                    var b = d.Body & 0xFFFF;
                    if (b == 0) continue;
                    int wx = cx * 32768 + d.X, wz = cz * 32768 + d.Z;
                    var key = (b, wx, d.Y, wz);
                    if (!seen.Add(key)) continue;
                    decors++;
                    var temple = templeKeys.Contains(key);
                    var source = temple ? (b >= actorsFrom ? "actor" : "temple") : "race";
                    if (!bodies.TryGetValue(b, out var body))
                    {
                        try { body = Body.Read(obl.Read(b), 2, allowStatic: true); } catch { body = null; }
                        bodies[b] = body;
                    }
                    if (body is null) continue;
                    var world = body.World();
                    var beta = (d.Beta & 0xFFFF) * Math.PI * 2 / 4096;
                    float cos = (float)Math.Cos(beta), sin = (float)Math.Sin(beta);
                    Vector3 Place(Vector3 v) => (d.Beta & 0xFFFF) == 0 ? new Vector3(wx + v.X, d.Y + v.Y, wz + v.Z)
                        : new Vector3(wx + v.X * cos + v.Z * sin, d.Y + v.Y, wz - v.X * sin + v.Z * cos);
                    double maxOver = double.MinValue; int nOver = 0, nUnder = 0; Vector3 at = default; (double Over, int K, double Across, bool Strip) atDeck = default;
                    int atColour = -1; bool atTex = false; Vector3 atNormal = default;
                    foreach (var f in body.Faces)
                    {
                        if (f.Points.Length < 3) continue;
                        polys++;
                        var p = f.Points.Select(i => Place(world[i])).ToArray();
                        var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                        if (n.LengthSquared() > 1e-6) n = Vector3.Normalize(n);
                        for (var t = 1; t + 1 < p.Length; t++)
                        {
                            Vector3 a = p[0], bb = p[t], c = p[t + 1];
                            var e = Math.Max((bb - a).Length(), Math.Max((c - a).Length(), (c - bb).Length()));
                            var m = Math.Max(1, (int)Math.Ceiling(e / step));
                            for (var i = 0; i <= m; i++)
                                for (var j = 0; i + j <= m; j++)
                                {
                                    // (a little inside the triangle: an edge shared with the deck's own is not an intrusion)
                                    float u = (i + 0.02f) / (m + 0.06f), v = (j + 0.02f) / (m + 0.06f);
                                    var q = a + (bb - a) * u + (c - a) * v;
                                    samples++;
                                    if (Deck(q) is not { } dk) continue;
                                    if (source == "race") continue;
                                    if (dk.Over > 0) { nOver++; inOver++; } else { nUnder++; inUnder++; underHist[(int)(-dk.Over / 10)]++; }
                                    if (dk.Over > maxOver) { maxOver = dk.Over; at = q; atDeck = dk; atColour = f.Colour; atTex = f.Texture is not null; atNormal = n; }
                                }
                        }
                    }
                    if (nOver + nUnder == 0) continue;
                    var kind = source == "temple" ? CellKind(at) : "";
                    hits.Add($"{source},{b},{wx},{d.Y},{wz},{maxOver:0},{nOver},{nUnder},{at.X / 512:0.0},{at.Y:0},{at.Z / 512:0.0},{atDeck.K},{atDeck.Across:0},{(atDeck.Strip ? 1 : 0)}," +
                             $"{(atTex ? "tex" : atColour.ToString())},{atNormal.X:0.00}/{atNormal.Y:0.00}/{atNormal.Z:0.00},{body.Faces.Count},\"{kind}\"");
                }
            }
        File.WriteAllLines(Path.Combine(outDir, "clip.csv"), new[] { "source,body,x,y,z,maxOver,samplesOver,samplesUnder,atCellX,atY,atCellZ,roadPoint,across,strip,colour,normal,faces,kind" }
            .Concat(hits.OrderByDescending(h => double.Parse(h.Split(',')[5]))));
        Console.WriteLine($"{decors} drawn decors, {polys} polygons, {samples} samples; in the deck's room ({below} under to {over} over the surface): " +
                          $"{hits.Count} decors, {inOver} samples over the surface, {inUnder} under it");
        Console.WriteLine($"  decors reaching over the surface: {hits.Count(h => double.Parse(h.Split(',')[5]) > 0)} " +
                          $"(temple {hits.Count(h => h.StartsWith("temple") && double.Parse(h.Split(',')[5]) > 0)}, actor {hits.Count(h => h.StartsWith("actor") && double.Parse(h.Split(',')[5]) > 0)})");
        Console.WriteLine("  samples under the surface by depth (10s): " + string.Join(" ", underHist.Select((c, i) => $"{i * 10}:{c}")));
        return 0;
    }

    private static HashSet<(int, int, int, int)> Keys(IslandFile island)
    {
        var keys = new HashSet<(int, int, int, int)>();
        for (var cz = 0; cz < 16; cz++)
            for (var cx = 0; cx < 16; cx++)
                if (island.CubeAt(cx, cz) is { } cube)
                    foreach (var d in cube.Decors)
                        if ((d.Body & 0xFFFF) != 0) keys.Add((d.Body & 0xFFFF, cx * 32768 + d.X, d.Y, cz * 32768 + d.Z));
        return keys;
    }
}
