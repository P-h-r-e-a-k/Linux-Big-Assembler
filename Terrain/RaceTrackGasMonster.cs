using System.IO;
using System.Numerics;
using LbaBodyStudio;

namespace LBAAssembler.Terrain;

// Gas monsters along a stretch of the lap over Zeelich's gas (RaceTrackPlan.GasMonsters; RACEMOD.CPP gasmonster=): Otringal's, at its docks
// and round the islets where Baldino's plane lies crashed among the rocks -- the user, 2026-10-09: "let's have gas monsters coming out of
// the gas and attempting to bite cars, any bitten cars should be stunned for a couple of seconds as though they've been hit then be able to
// continue"; then, of the serpents first made for them: "The gas monsters we've added are fake ... let's just reuse the retail version";
// then: "Some of the gas monsters strike too low and appear through the track, let's have them permanently visible with their necks above
// the track and randomly striking down. Let's have each gas monster always attack the same point in the track, so once their positions are
// learnt they're avoidable"; then, of their heads coming down flat across the road: "The gas monsters aren't really biting players they're
// just slamming their heads onto the track ... Let's make the gas monsters necks longer, their body size doesn't really matter, so they're
// above the track then when they're ready they strike down onto the track into their strike zone". The game's own gas monster (entity 222,
// body 320; it rises out of the gas in Celebration Island's and the Wannies' scenes), twice its size, its neck stretched as long as its
// place wants (GasMonsterNeck.LongNeck: its head high over its strike zone) -- a body of its own for each monster -- standing out of the gas
// beside the road for good, its head poised high over its strike zone on the road's near half, mouth open, swaying; now and then it rears
// back and strikes down, its neck sinking into its hole and bending, its mouth onto the strike zone, and its jaw snaps shut there. Each
// monster's moves are its own animations, fitted to the road beside it (InstallPoses). One actor each.
internal sealed class GasMonsterRun
{
    public int From { get; set; }
    public int To { get; set; }
    // one every so many cells along the stretch, where there is gas beside the road; how far out past the rail it rises (cells)
    public double Every { get; set; } = 9;
    public double Out { get; set; } = 3;
    public int Most { get; set; } = 5;
}

// A monster as placed: where it rises out of the gas, and where on the road it bites -- its strike zone (island cells, the bite's height
// the road's); how far towards its bite the road's rail is (cells); once made (InstallPoses), its body and first animation (the entity's).
internal sealed record GasMonsterSpot(double X, double Z, double BiteX, double BiteZ, double BiteY, double Edge, int Body = -1, int FirstAnim = -1);

internal static class RaceTrackGasMonster
{
    // the game's gas monster: its entity, its body (shut: its jaw opens it), how much bigger it is made; each monster's body (the entity's,
    // from FirstMonsterBody on) and animations (from FirstPoseAnim on, 4 apart): swaying poised, rearing and striking, and biting and back;
    // their times (ms, RACEMOD.CPP's RACE_MONSTER_*): into the strike's first pose, rearing, the strike; the jaw snapping shut, held, back
    // to poised; and a sway either way; how far it sways (radians)
    public const int Entity = 222, RetailBody = 320;
    private const double Scale = 2.0;
    private const int FirstMonsterBody = 130, FirstPoseAnim = 610;
    public const int PoiseMs = 150, RearMs = 650, LungeMs = 220, SnapMs = 120, HoldMs = 330, BackMs = 700, SwayMs = 700;
    private const double Sway = 0.12;
    // how far across from the road's middle towards the monster it bites, as a share of the road's half width
    private const double BiteAcross = 0.45;
    private const double GasLevel = 60;
    // the least distance between two monsters (cells)
    private const double Apart = 9;

    // Into the game folder: each monster (report.GasMonsters, which get their body and first animation) -- the game's gas monster twice
    // its size, its neck as long as the road beside it wants (its mouth reaching a tenth again past the poised place over the strike zone;
    // longer, a step at a time, where the poses don't come out right), its poses against that road (GasMonsterPoser), the body appended to
    // BODY.HQR and three animations to ANIM.HQR: swaying poised; rearing and striking; biting and back to poised. Lines for the log.
    public static List<string> InstallPoses(string gameDirectory, RaceTrackReport report)
    {
        var log = new List<string>();
        if (report.GasMonsters.Count == 0) return log;
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var clipped = 0;
        var twice = IslandScaler.ScaledBody(HqrArchive.Open(bodyPath).Read(RetailBody), Scale, ref clipped);
        var plain = new GasMonsterNeck(Body.Read(twice, 2, allowStatic: true));
        var bodies = File.ReadAllBytes(bodyPath);
        var bodyIndex = HqrArchive.CountEntries(bodyPath);
        var animPath = Path.Combine(gameDirectory, "ANIM.HQR");
        var anims = File.ReadAllBytes(animPath);
        var animIndex = HqrArchive.CountEntries(animPath);
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var table = HqrArchive.Open(ressPath).Read(44);
        var gen = FirstFreeAnim(table, Entity, FirstPoseAnim);
        var bodyGen = FirstFreeBody(table, Entity, FirstMonsterBody);
        for (var i = 0; i < report.GasMonsters.Count; i++)
        {
            var g = report.GasMonsters[i];
            double d = Math.Sqrt((g.BiteX - g.X) * (g.BiteX - g.X) + (g.BiteZ - g.Z) * (g.BiteZ - g.Z)) * 512, e = g.Edge * 512;
            // (the neck: long enough for the mouth to reach a tenth again past its poised place, from the foot at the gas)
            var reach = Math.Sqrt((d - 0.15 * (d - e)) * (d - 0.15 * (d - e)) + (g.BiteY + GasMonsterPoser.PoisedHigh) * (g.BiteY + GasMonsterPoser.PoisedHigh));
            var stretch = Math.Max(1, (1.1 * reach - plain.HeadReach) / plain.Length);
            byte[] body = twice;
            GasMonsterNeck neck = plain;
            GasMonsterPoser.Poses poses = null!;
            var fits = false;
            for (var attempt = 0; attempt < 4 && !fits; attempt++, stretch *= 1.15)
            {
                body = GasMonsterNeck.LongNeck(twice, stretch);
                neck = new GasMonsterNeck(Body.Read(body, 2, allowStatic: true));
                poses = GasMonsterPoser.Solve(neck, d, e, g.BiteY);
                fits = poses.StruckMiss < 200 && poses.PoisedOver >= GasMonsterPoser.PoisedHigh - 500 && poses.LeastOverRail >= 0 && poses.LeastOverDeck >= 0;
            }
            if (!fits) stretch /= 1.15;
            var (poised, rear, struck, bit) = (poses.Poised, poses.Rear, poses.Struck, poses.Bit);
            var idle = new Anim { Game = 2, LoopFrame = 0 };
            idle.Frames.AddRange(new[] { neck.Frame(poised, SwayMs), neck.Frame(poised with { Nod = poised.Nod + 0.1, Jaw = poised.Jaw - 0.25 }, SwayMs, Sway),
                                         neck.Frame(poised, SwayMs), neck.Frame(poised with { Nod = poised.Nod - 0.06, Jaw = poised.Jaw + 0.15 }, SwayMs, -Sway) });
            var strike = new Anim { Game = 2, LoopFrame = 2 };
            strike.Frames.AddRange(new[] { neck.Frame(poised, PoiseMs), neck.Frame(rear, RearMs), neck.Frame(struck, LungeMs) });
            var biting = new Anim { Game = 2, LoopFrame = 3 };
            biting.Frames.AddRange(new[] { neck.Frame(struck, 20), neck.Frame(bit, SnapMs), neck.Frame(bit, HoldMs), neck.Frame(poised, BackMs) });
            bodies = HqrWriter.AppendEntry(bodies, HqrWriter.StoredEntry(body));
            table = RaceTrackBaldinoCar.WithBody(table, Entity, bodyGen, bodyIndex++);
            foreach (var (anim, n) in new[] { (idle, 0), (strike, 1), (biting, 2) })
            {
                anims = HqrWriter.AppendEntry(anims, HqrWriter.StoredEntry(anim.Write()));
                table = RaceTrackJumpAnim.WithAnim(table, Entity, gen + n, animIndex++);
            }
            // (for a look from the side: each pose's points from the foot, and the road beside it -- RT_MONSTER_POSES=<folder>)
            if (Environment.GetEnvironmentVariable("RT_MONSTER_POSES") is { Length: > 0 } dump)
            {
                Directory.CreateDirectory(dump);
                using var w = new StreamWriter(Path.Combine(dump, $"monster{i}.csv"));
                w.WriteLine($"# foot 0 road {g.BiteY:0} rail {e:0} bite {d:0}");
                w.WriteLine("pose,z,y");
                foreach (var (name, shape) in new[] { ("poised", poised), ("rear", rear), ("struck", struck), ("bit", bit) })
                    foreach (var q in neck.Pose(shape)) w.WriteLine($"{name},{q.Z:0},{q.Y:0}");
            }
            report.GasMonsters[i] = g with { Body = bodyGen, FirstAnim = gen };
            log.Add($"{(fits ? "" : "WARNING: ")}gas monster at ({g.X:0.0}, {g.Z:0.0}): its neck {stretch:0.00} times as long ({neck.Length:0}; " +
                    $"the road {g.BiteY:0}, its strike zone {d / 512:0.0} cells out, the rail {g.Edge:0.0}); struck its open mouth {poses.StruckMiss:0} from the strike zone " +
                    $"(its neck sunk {-struck.Drop:0}), poised {poses.PoisedOver:0} over the road, at the least {poses.LeastOverRail:0} over the rail and " +
                    $"{poses.LeastOverDeck:0} over the deck; body {bodyGen}, animations {gen}-{gen + 2}");
            gen += 4;
            bodyGen++;
        }
        File.WriteAllBytes(bodyPath, bodies);
        File.WriteAllBytes(animPath, anims);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        if (clipped > 0) log.Add($"WARNING: the gas monster twice its size: {clipped} values clipped");
        return log;
    }

    // The first of 4 animation numbers in a row the entity doesn't use yet, from `from` on, in steps of 4.
    private static int FirstFreeAnim(byte[] table, int entity, int from)
    {
        var used = Records(table, entity, 3);
        var gen = from;
        while (Enumerable.Range(gen, 4).Any(used.Contains)) gen += 4;
        return gen;
    }

    // The first body number from `from` on that the entity, and the ones after it for the monsters, don't use yet (its own: 0, 158, 159).
    private static int FirstFreeBody(byte[] table, int entity, int from)
    {
        var used = Records(table, entity, 1);
        var gen = from;
        while (Enumerable.Range(gen, 16).Any(used.Contains)) gen += 16;
        return gen;
    }

    // The numbers of an entity's records of one kind (1 bodies, 3 animations).
    private static HashSet<int> Records(byte[] table, int entity, int kind)
    {
        int start = BitConverter.ToInt32(table, entity * 4), end = BitConverter.ToInt32(table, (entity + 1) * 4);
        var used = new HashSet<int>();
        for (var p = start; p < end && table[p] != 255;)
            if (table[p] == 3) { if (kind == 3) used.Add(table[p + 1] | table[p + 2] << 8); p += 3 + table[p + 3]; }
            else { if (kind == table[p]) used.Add(table[p + 1]); p += 2 + table[p + 2]; }
        return used;
    }

    // Where along the stretch From..To (the lap's points) monsters rise: every Every cells, out past the rail over the gas on one side or the
    // other (each side in turn where both have it), clear of the lap elsewhere and of what stands there; at most Most.
    public static List<GasMonsterSpot> Place(IslandFile island, TrackRoad r, int from, int to, GasMonsterRun run, RaceTrackOptions o, RaceTrackReport report)
    {
        var placed = report.GasMonsters;
        var n = r.Count;
        var span = ((to - from) % n + n) % n + 1;
        var per = Math.Max(1, (int)Math.Round(run.Every / o.Spacing));
        var spots = new List<GasMonsterSpot>();
        var boxes = IslandOps.CubeCells(island).SelectMany(c => c.Item3.Decors.Select(d =>
            ((c.Item1 * (double)IslandFile.CubeSize + d.XMin) / 512, (c.Item2 * (double)IslandFile.CubeSize + d.ZMin) / 512,
             (c.Item1 * (double)IslandFile.CubeSize + d.XMax) / 512, (c.Item2 * (double)IslandFile.CubeSize + d.ZMax) / 512))).ToList();
        bool Gas(double x, double z)
        {
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                    if ((IslandOps.Altitude(island, (x + dx * 0.8) * 512, (z + dz * 0.8) * 512) ?? 0) > GasLevel) return false;
            return !boxes.Any(b => x > b.Item1 - 1 && x < b.Item3 + 1 && z > b.Item2 - 1 && z < b.Item4 + 1);
        }
        bool ClearOfLap(double x, double z, int k)
        {
            for (var i = 0; i < n; i++)
            {
                var apart = Math.Abs(i - k); apart = Math.Min(apart, n - apart);
                if (apart * o.Spacing < 6) continue;
                var half = r.RaisedHalfs?[i] ?? o.RaisedHalfWidth;
                if ((r.X[i] - x) * (r.X[i] - x) + (r.Z[i] - z) * (r.Z[i] - z) < (half + 2.5) * (half + 2.5)) return false;
            }
            return true;
        }
        var prefer = 1;
        for (var j = per / 2; j < span && spots.Count < run.Most; j += per)
        {
            var k = (from + j) % n;
            if (r.Gap[k]) continue;
            double tx = r.Tx[k], tz = r.Tz[k], ax = -tz, az = tx;
            var half = r.RaisedHalfs?[k] ?? o.RaisedHalfWidth;
            foreach (var side in new[] { prefer, -prefer })
            {
                double sx = r.X[k] + ax * side * (half + run.Out), sz = r.Z[k] + az * side * (half + run.Out);
                if (!Gas(sx, sz) || !ClearOfLap(sx, sz, k)) continue;
                // (and not near another: two side by side bit a car one after the other)
                if (spots.Concat(placed).Any(o => (o.X - sx) * (o.X - sx) + (o.Z - sz) * (o.Z - sz) < Apart * Apart)) continue;
                spots.Add(new GasMonsterSpot(sx, sz, r.X[k] + ax * side * half * BiteAcross, r.Z[k] + az * side * half * BiteAcross, r.H[k], run.Out));
                prefer = -side;
                break;
            }
        }
        report.Notes.Add($"gas monsters along the lap's points {from}-{to}: {spots.Count} ({string.Join(", ", spots.Select(s => $"({s.X:0.0}, {s.Z:0.0})"))})");
        return spots;
    }
}
