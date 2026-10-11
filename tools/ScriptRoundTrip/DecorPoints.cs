using System.Globalization;
using System.Numerics;
using LBAAssembler;
using LBAAssembler.Terrain;
using LbaBodyStudio;

namespace ScriptRoundTrip;

// decorpoints <ISLAND> <out.csv> [body ...]: every point of the island's decor bodies where it stands in the island (cube-local units:
// "decor,body,x,y,z,cx,cz", cx/cz the island map cell of the cube -- an island of several cubes numbers each cube's decors from 0),
// so that a big object's real shape -- a statue's, at each height -- can be planned round instead of its box.
// LBA2_DIR is the game folder.
internal static class DecorPointsCommand
{
    public static int Run(string[] args)
    {
        var dir = Environment.GetEnvironmentVariable("LBA2_DIR") ?? @"E:\GOG Games\Little Big Adventure 2 - Level viewer";
        var name = args[1].ToUpperInvariant();
        var island = IslandFile.Load(Path.Combine(dir, name + ".ILE"));
        var bodies = HqrArchive.Open(Path.Combine(dir, name + ".OBL"));
        var only = args.Skip(3).Select(int.Parse).ToHashSet();
        using var w = new StreamWriter(args[2]);
        w.WriteLine("decor,body,x,y,z,cx,cz");
        using var faces = Path.ChangeExtension(args[2], ".faces.csv") is { } fp ? new StreamWriter(fp) : null;
        faces?.WriteLine("decor,body,x1,y1,z1,x2,y2,z2,x3,y3,z3,cx,cz");
        var read = new Dictionary<int, Body?>();
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
        {
            for (var k = 0; k < cube.Decors.Count; k++)
            {
                var d = cube.Decors[k];
                var index = d.Body & 0xFFFF;
                if (only.Count > 0 && !only.Contains(index)) continue;
                if (!read.TryGetValue(index, out var body))
                {
                    try { body = Body.Read(bodies.Read(index), 2, allowStatic: true); }
                    catch (Exception error) { Console.WriteLine($"body {index}: {error.Message}"); body = null; }
                    read[index] = body;
                }
                if (body is null) continue;
                var angle = (float)((d.Beta & 0xFFFF) * 2 * Math.PI / 4096);
                var turn = Matrix4x4.CreateRotationY(angle) * Matrix4x4.CreateTranslation(d.X, d.Y, d.Z);
                var used = body.Faces.SelectMany(f => f.Points).Distinct().ToList();
                Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
                foreach (var p in used)
                {
                    var v = Vector3.Transform(body.Vertices[p], turn);
                    lo = Vector3.Min(lo, v); hi = Vector3.Max(hi, v);
                    w.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{k},{index},{v.X:0},{v.Y:0},{v.Z:0},{cx},{cz}"));
                }
                // (every face as a fan of triangles, for clearance checks against the real shape)
                if (faces is not null)
                    foreach (var f in body.Faces)
                        for (var t = 1; t + 1 < f.Points.Length; t++)
                        {
                            var tri = new[] { f.Points[0], f.Points[t], f.Points[t + 1] }.Select(q => Vector3.Transform(body.Vertices[q], turn)).ToArray();
                            faces.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{k},{index},{tri[0].X:0},{tri[0].Y:0},{tri[0].Z:0},{tri[1].X:0},{tri[1].Y:0},{tri[1].Z:0},{tri[2].X:0},{tri[2].Y:0},{tri[2].Z:0},{cx},{cz}"));
                        }
                Console.WriteLine($"cube ({cx},{cz}) decor {k}: body {index} at ({d.X},{d.Y},{d.Z}) turn {d.Beta & 0xFFFF}: {used.Count} points, {body.Faces.Count} faces, x {lo.X:0}..{hi.X:0} y {lo.Y:0}..{hi.Y:0} z {lo.Z:0}..{hi.Z:0}; box x {d.XMin}..{d.XMax} y {d.YMin}..{d.YMax} z {d.ZMin}..{d.ZMax}");
            }
        }
        return 0;
    }
}

// sceneactors <game folder> <scene>: an LBA2 scene's hero start and every actor (entity, body, place, flags, script sizes).
internal static class SceneActorsCommand
{
    public static int Run(string[] args)
    {
        var scene = new LBAAssembler.Scenes.SceneStore(LBAAssembler.Scenes.SceneGame.Lba2, args[1]).Load(int.Parse(args[2]));
        Console.WriteLine($"scene {args[2]}: island {scene.Island} cube ({scene.CubeX},{scene.CubeY}) hero ({scene.Hero.X},{scene.Hero.Y},{scene.Hero.Z})");
        for (var i = 0; i < scene.Actors.Count; i++)
        {
            var a = scene.Actors[i];
            Console.WriteLine($"  actor {i + 1}: entity {a.Entity} body {a.Body} anim {a.Anim} at ({a.X},{a.Y},{a.Z}) beta {a.Beta} flags 0x{a.Flags:X} move {a.Move} life {a.Life.Length} track {a.Track.Length}");
        }
        // (points: the scene's track points too -- where a script's pos_point puts an actor)
        if (args.Contains("points"))
            for (var i = 0; i < scene.TrackPoints.Count; i++) Console.WriteLine($"  point {i}: ({scene.TrackPoints[i].X},{scene.TrackPoints[i].Y},{scene.TrackPoints[i].Z})");
        return 0;
    }
}
