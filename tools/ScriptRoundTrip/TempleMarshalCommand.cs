using System.IO;
using System.Numerics;
using LBAAssembler;
using LBAAssembler.Terrain;
using LBAAssembler.Terrain.Palace;
using LBAAssembler.Terrain.Temple;

namespace ScriptRoundTrip;

// templemarshal <game folder> <out folder> [yaw degrees] [palette entry]: the Temple of Bú's flag marshal (Temple.TempleMarshal) made from
// the folder's BODY.HQR 124 and entity 77's standing -- read back and checked (bones, points, polygons, the animations' sizes; her plinth's
// body) -- and drawn
// offline (meshshot) in every keyframe of each of his animations, from in front a little to one side: <out>/<animation>_<keyframe>.png;
// and the game's own body as its standing has it (<out>/retail.png). The island's palette: the Temple of Bú's (or Otringal's) by default.
internal static class TempleMarshalCommand
{
    public static int Run(string[] args)
    {
        var game = args[1];
        var outDir = args[2];
        var yaw = args.Length > 3 ? double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 25;
        var entry = args.Length > 4 ? int.Parse(args[4]) : IslandMapRenderer.TemplePaletteEntryFor(game);
        Directory.CreateDirectory(outDir);
        var table = HqrArchive.Open(Path.Combine(game, "RESS.HQR")).Read(44);
        var entity = Lba2EntityTable.Parse(table).Entities.First(e => e.Id == TempleMarshal.Entity);
        var idle = HqrArchive.Open(Path.Combine(game, "ANIM.HQR")).Read(entity.Anims.First(a => a.Generic == 0).Anim);
        var retailBytes = HqrArchive.Open(Path.Combine(game, "BODY.HQR")).Read(TempleMarshal.Retail);
        var bytes = TempleMarshal.Make(retailBytes, out var standing);
        var anims = TempleMarshal.Anims(standing, idle);
        // (read back as the game will: the scaled body)
        var built = LbaBodyStudio.Body.Read(bytes, 2, allowStatic: true);
        Console.WriteLine($"body: {bytes.Length} bytes, {built.Bones.Count} bones, {built.Vertices.Count} points, {built.Faces.Count} polygons, {built.Lines.Count} lines, " +
                          $"{built.Spheres.Count} spheres ({built.Faces.Count + built.Lines.Count + built.Spheres.Count} of 550); the game's: {LbaBodyStudio.Body.Read(retailBytes, 2, allowStatic: true).Faces.Count} polygons");
        // (her plinth, a body of its own: read back too)
        var plinthBytes = TempleMarshal.MakePlinth(retailBytes);
        var plinth = LbaBodyStudio.Body.Read(plinthBytes, 2, allowStatic: true);
        Console.WriteLine($"plinth: {plinthBytes.Length} bytes, {plinth.Bones.Count} bone, {plinth.Vertices.Count} points, {plinth.Faces.Count} polygons");
        var names = new[] { "ready", "raise", "drop", "wave" };
        for (var k = 0; k < anims.Length; k++)
        {
            var data = anims[k].Write();
            Console.WriteLine($"{names[k]}: {anims[k].Frames.Count} keyframes, loop {anims[k].LoopFrame}, {data.Length} bytes, {BitConverter.ToUInt16(data, 2)} bones a keyframe");
        }
        var palette = IslandMapRenderer.LoadPaletteEntry(game, entry);
        // the game's own, as its standing has it
        var retail = LbaBodyStudio.Body.Read(retailBytes, 2, allowStatic: true);
        var basis = PalaceTorturers.ReadFrames(idle, retail.Bones.Count)[0].Bones;
        Shot(retail, PalaceTorturers.Posed(retail, basis).Points, palette, Path.Combine(outDir, "retail.png"), yaw, 1);
        for (var k = 0; k < anims.Length; k++)
            for (var f = 0; f < anims[k].Frames.Count; f++)
            {
                // (the keyframe's values back as radians and the body's own units)
                var values = anims[k].Frames[f].Bones.Select(b => b.Type == 1
                    ? (1, b.X / TempleMarshal.Scale, b.Y / TempleMarshal.Scale, b.Z / TempleMarshal.Scale)
                    : (0, b.X * 2 * Math.PI, b.Y * 2 * Math.PI, b.Z * 2 * Math.PI)).ToArray();
                var posed = PalaceTorturers.Posed(standing, values).Points;
                // (each pole's way as posed: its tip's ring from its back ring)
                string Way(int bone)
                {
                    var b = standing.Bones[bone];
                    var back = Enumerable.Range(b.Start, 4).Select(i => posed[i]).Aggregate(Vector3.Zero, (s, q) => s + q) / 4;
                    var tip = Enumerable.Range(b.Start + 4, 4).Select(i => posed[i]).Aggregate(Vector3.Zero, (s, q) => s + q) / 4;
                    var d = Vector3.Normalize(tip - back);
                    return $"({d.X:0.00}, {d.Y:0.00}, {d.Z:0.00})";
                }
                Console.WriteLine($"  {names[k]} {f}: the green pole {Way(TempleMarshal.GreenPoleBone)}, the chequered pole {Way(TempleMarshal.CheckPoleBone)}");
                Shot(standing, posed, palette, Path.Combine(outDir, $"{names[k]}_{f}.png"), yaw, TempleMarshal.Scale);
            }
        Console.WriteLine($"pictures in {outDir}");
        return 0;
    }

    // A posed body drawn by meshshot: its polygons (a lit one a few steps into its ramp, shaded by the picture's light; an unlit one its own
    // colour), its lines as thin strips and its spheres as small diamonds, Scale times as big, from in front turned `yaw`.
    private static void Shot(LbaBodyStudio.Body body, Vector3[] points, byte[] palette, string png, double yaw, double scale)
    {
        var bin = Path.ChangeExtension(png, ".bin");
        using (var w = new BinaryWriter(File.Create(bin)))
        {
            Vector3 P(int i) => points[i] * (float)scale;
            void Tri(Vector3 a, Vector3 b, Vector3 c, int colour, int kind)
            {
                foreach (var q in new[] { a, b, c }) { w.Write(q.X); w.Write(q.Y); w.Write(q.Z); }
                w.Write((float)palette[colour * 3]); w.Write((float)palette[colour * 3 + 1]); w.Write((float)palette[colour * 3 + 2]); w.Write((float)kind);
            }
            foreach (var f in body.Faces)
            {
                var lit = f.Material != 0;
                var colour = Math.Clamp(f.Colour + (lit ? 7 : 0), 0, 255);
                for (var t = 1; t + 1 < f.Points.Length; t++) Tri(P(f.Points[0]), P(f.Points[t]), P(f.Points[t + 1]), colour, lit ? 0 : 3);
            }
            foreach (var l in body.Lines)
            {
                Vector3 a = P(l.A), b = P(l.B), side = Vector3.Normalize(Vector3.Cross(b - a, Vector3.UnitY) + new Vector3(1e-4f, 0, 0)) * 6;
                Tri(a - side, b - side, b + side, l.Colour, 3); Tri(a - side, b + side, a + side, l.Colour, 3);
                Tri(a + side, b + side, b - side, l.Colour, 3); Tri(a + side, b - side, a - side, l.Colour, 3);
            }
            foreach (var s in body.Spheres)
            {
                var c = P(s.Point);
                var r = (float)(s.Radius * scale);
                Vector3[] axis = { Vector3.UnitX * r, Vector3.UnitY * r, Vector3.UnitZ * r };
                foreach (var sx in new[] { -1, 1 }) foreach (var sy in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 })
                {
                    Vector3 a = c + axis[0] * sx, b = c + axis[1] * sy, d = c + axis[2] * sz;
                    if (sx * sy * sz > 0) Tri(a, b, d, s.Colour + 7, 0); else Tri(a, d, b, s.Colour + 7, 0);
                }
            }
        }
        var height = 2400 * scale;
        PalaceStudy.Shot(new[] { "meshshot", bin, png, "--size", "640", "760", "--target", "0", $"{height * 0.42:0}", "0", "--yaw", $"{yaw:0}", "--pitch", "6",
            "--dist", $"{height * 2.3:0}", "--fov", "40", "--cull" });
        File.Delete(bin);
    }
}
