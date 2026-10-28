using System.IO;
using System.Numerics;
using LBAAssembler;
using LBAAssembler.Terrain;
using LBAAssembler.Terrain.Palace;
using LBAAssembler.Terrain.Temple;
using LbaBodyStudio;

namespace ScriptRoundTrip;

// MEASURING PROBE (2026-10-18): an entity's body (LBA2's entity table) drawn offline in every keyframe of each of its animations, in the
// Temple of Bú's palette -- the body's bones, points, polygons and colours (each colour's ramp in the palette), each keyframe's box.
//   templecreatures <game dir> <out dir> <entity> [yaw degrees] [palette entry]
// -> <out>/e<entity>_a<generic>_k<keyframe>.png
internal static class TempleCreaturesProbe
{
    public static int Run(string[] args)
    {
        var game = args[1];
        var outDir = args[2];
        var yaw = args.Length > 4 ? double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 25;
        var entry = args.Length > 5 ? int.Parse(args[5]) : IslandMapRenderer.TemplePaletteEntryFor(game);
        Directory.CreateDirectory(outDir);
        if (args[3] == "decor")
        {
            // the rabbibunny skeleton's decor bodies as the island has them (TempleHalloween.Creatures)
            var pal = IslandMapRenderer.LoadPaletteEntry(game, entry);
            foreach (var pose in Enum.GetValues<TempleHalloween.RabbiPose>())
            {
                var bytes = TempleHalloween.RabbiDecorBody(game, pose);
                if (bytes is null) { Console.WriteLine($"{pose}: none"); continue; }
                var b = Body.Read(bytes, 2, allowStatic: true);
                var pts = b.World();
                var lo = new Vector3(pts.Min(p => p.X), pts.Min(p => p.Y), pts.Min(p => p.Z));
                var hi = new Vector3(pts.Max(p => p.X), pts.Max(p => p.Y), pts.Max(p => p.Z));
                Console.WriteLine($"{pose}: {b.Vertices.Count} points, {b.Faces.Count} polygons, box ({lo.X:0},{lo.Y:0},{lo.Z:0})..({hi.X:0},{hi.Y:0},{hi.Z:0}), colours {string.Join(",", b.Faces.Select(f => f.Colour).Distinct().OrderBy(c => c))}");
                Shot(b, pts, pal, Path.Combine(outDir, $"decor_{pose}.png"), yaw, Math.Max(hi.Y - lo.Y, Math.Max(hi.X - lo.X, hi.Z - lo.Z)), (lo + hi) / 2);
            }
            return 0;
        }
        if (args[3] == "rat")
        {
            // the flying rat as the spooks have it (bigger, lit), in each keyframe of its flapping
            var pal = IslandMapRenderer.LoadPaletteEntry(game, entry);
            var bytes = TempleHalloween.RatBody(game) ?? throw new InvalidDataException("no rat");
            var b = Body.Read(bytes, 2);
            var flap = TempleHalloween.CreatureAnim(game, TempleHalloween.FlyingRatBody, TempleHalloween.RatFly);
            Console.WriteLine($"rat: {bytes.Length} bytes, {b.Bones.Count} bones, {b.Vertices.Count} points, {b.Faces.Count} polygons (materials {string.Join(",", b.Faces.Select(f => f.Material).Distinct())}), flap ANIM.HQR {flap}");
            var frames = PalaceTorturers.ReadFrames(HqrArchive.Open(Path.Combine(game, "ANIM.HQR")).Read(flap), b.Bones.Count);
            for (var k = 0; k < frames.Count; k += 2)
            {
                var (points, _) = PalaceTorturers.Posed(b, frames[k].Bones);
                Shot(b, points, pal, Path.Combine(outDir, $"rat_k{k}.png"), yaw, 1800, Vector3.Zero);
            }
            return 0;
        }
        var table = Lba2EntityTable.Load(game) ?? throw new InvalidDataException("no entity table");
        // (b<n>: BODY.HQR n, the entity that has it as its body)
        var byBody = args[3].StartsWith('b');
        var id = byBody ? table.KindForBody(int.Parse(args[3][1..]))?.Entity ?? throw new InvalidDataException("no entity has that body") : int.Parse(args[3]);
        var entity = table.Entities[id];
        Console.WriteLine($"entities with it: {string.Join(", ", byBody ? table.EntitiesWithBody(int.Parse(args[3][1..])).Select(e => e.Id) : new[] { id })}");
        var bodyIndex = byBody ? int.Parse(args[3][1..]) : entity.Bodies.First(b => b.Generic == 0).Body;
        var body = Body.Read(HqrArchive.Open(Path.Combine(game, "BODY.HQR")).Read(bodyIndex), 2, allowStatic: true);
        var palette = IslandMapRenderer.LoadPaletteEntry(game, entry);
        Console.WriteLine($"entity {id}: BODY.HQR {bodyIndex}: {body.Bones.Count} bones, {body.Vertices.Count} points, {body.Faces.Count} polygons, {body.Lines.Count} lines, " +
                          $"{body.Spheres.Count} spheres; palette RESS {entry}");
        for (var b = 0; b < body.Bones.Count; b++)
            Console.WriteLine($"  bone {b}: parent {body.Bones[b].Parent}, pivot {body.Bones[b].Pivot}, points {body.Bones[b].Start}+{body.Bones[b].Count}");
        foreach (var g in body.Faces.GroupBy(f => (f.Colour, f.Material)).OrderBy(g => g.Key.Colour))
        {
            var ramp = g.Key.Colour & ~15;
            Console.WriteLine($"  colour {g.Key.Colour} material {g.Key.Material}: {g.Count()} polygons; ramp {ramp}..{ramp + 15}: " +
                              string.Join(" ", Enumerable.Range(ramp, 16).Select(c => $"{palette[c * 3]:x2}{palette[c * 3 + 1]:x2}{palette[c * 3 + 2]:x2}")));
        }
        foreach (var g in body.Lines.GroupBy(l => l.Colour)) Console.WriteLine($"  line colour {g.Key}: {g.Count()}");
        foreach (var g in body.Spheres.GroupBy(s => s.Colour)) Console.WriteLine($"  sphere colour {g.Key}: {g.Count()} (radii {string.Join(",", g.Select(s => s.Radius))})");
        var anims = HqrArchive.Open(Path.Combine(game, "ANIM.HQR"));
        foreach (var (generic, anim) in entity.Anims)
        {
            var data = anims.Read(anim);
            var frames = PalaceTorturers.ReadFrames(data, body.Bones.Count);
            for (var k = 0; k < frames.Count; k++)
            {
                var (points, _) = PalaceTorturers.Posed(body, frames[k].Bones);
                var lo = new Vector3(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z));
                var hi = new Vector3(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z));
                Console.WriteLine($"  anim {generic} (ANIM.HQR {anim}) keyframe {k}: {frames[k].Time} ticks, box ({lo.X:0},{lo.Y:0},{lo.Z:0})..({hi.X:0},{hi.Y:0},{hi.Z:0}); " +
                                  string.Join(" ", frames[k].Bones.Skip(1).Select((v, i) => v.Type == 1 ? $"{i + 1}:t({v.A:0},{v.B:0},{v.C:0})" : $"{i + 1}:({Deg(v.A)},{Deg(v.B)},{Deg(v.C)})")));
                Shot(body, points, palette, Path.Combine(outDir, $"e{id}_a{generic}_k{k}.png"), yaw, Math.Max(hi.Y - lo.Y, Math.Max(hi.X - lo.X, hi.Z - lo.Z)), (lo + hi) / 2);
            }
        }
        return 0;
    }

    private static string Deg(double radians) => $"{radians * 180 / Math.PI:0}";

    private static void Shot(Body body, Vector3[] points, byte[] palette, string png, double yaw, double size, Vector3 middle)
    {
        var bin = Path.ChangeExtension(png, ".bin");
        using (var w = new BinaryWriter(File.Create(bin)))
        {
            void Tri(Vector3 a, Vector3 b, Vector3 c, int colour, int kind)
            {
                foreach (var q in new[] { a, b, c }) { w.Write(q.X); w.Write(q.Y); w.Write(q.Z); }
                w.Write((float)palette[colour * 3]); w.Write((float)palette[colour * 3 + 1]); w.Write((float)palette[colour * 3 + 2]); w.Write((float)kind);
            }
            foreach (var f in body.Faces)
            {
                var lit = f.Material != 0;
                var colour = Math.Clamp(f.Colour + (lit ? 7 : 0), 0, 255);
                for (var t = 1; t + 1 < f.Points.Length; t++) Tri(points[f.Points[0]], points[f.Points[t]], points[f.Points[t + 1]], colour, lit ? 0 : 3);
            }
            foreach (var l in body.Lines)
            {
                Vector3 a = points[l.A], b = points[l.B], side = Vector3.Normalize(Vector3.Cross(b - a, Vector3.UnitY) + new Vector3(1e-4f, 0, 0)) * 6;
                Tri(a - side, b - side, b + side, l.Colour, 3); Tri(a - side, b + side, a + side, l.Colour, 3);
            }
            foreach (var s in body.Spheres)
            {
                var c = points[s.Point];
                var r = (float)s.Radius;
                Vector3[] axis = { Vector3.UnitX * r, Vector3.UnitY * r, Vector3.UnitZ * r };
                foreach (var sx in new[] { -1, 1 }) foreach (var sy in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 })
                {
                    Vector3 a = c + axis[0] * sx, b = c + axis[1] * sy, d = c + axis[2] * sz;
                    if (sx * sy * sz > 0) Tri(a, b, d, s.Colour + 7, 0); else Tri(a, d, b, s.Colour + 7, 0);
                }
            }
        }
        PalaceStudy.Shot(new[] { "meshshot", bin, png, "--size", "480", "480", "--target", $"{middle.X:0}", $"{middle.Y:0}", $"{middle.Z:0}", "--yaw", $"{yaw:0}", "--pitch", "15",
            "--dist", $"{size * 2.4:0}", "--fov", "40" });
        File.Delete(bin);
    }
}
