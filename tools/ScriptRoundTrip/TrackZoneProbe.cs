using System.Text.Json;
using LBAAssembler.Scenes;

namespace ScriptRoundTrip;

// trackzones <game folder> <first scene> <last scene> [margin cells]: every zone of an island's outside scenes that the built race track's
// line (RACETRACK.JSON Path, world units) passes through or within `margin` cells of -- the zone's type, number, size and height, and how
// many line points it covers -- to find what stops a car on the road (a cube-change zone that doesn't reach it, a scenario zone a script
// watches, a hit zone...).
internal static class TrackZoneProbe
{
    public static int Run(string[] args)
    {
        var game = args[1];
        int first = int.Parse(args[2]), last = int.Parse(args[3]);
        var margin = args.Length > 4 ? double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 4.5;
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(game, "RACETRACK.JSON")));
        var line = doc.RootElement.GetProperty("Path").EnumerateArray()
            .Select(p => (X: p[0].GetDouble() / 512, Z: p[1].GetDouble() / 512, Y: p[2].GetDouble())).ToList();
        var interiors = new LBAAssembler.Lba2Interiors(game);
        string[] names = { "cube", "camera", "scenario", "grm", "object", "text", "ladder", "escalator", "hit", "rail", "type10", "type11" };
        for (var scene = first; scene <= last; scene++)
        {
            var model = interiors.LoadScene(scene);
            if (model is null || model.CubeMode != 1) continue;
            double ox = model.CubeX * 64.0, oz = model.CubeY * 64.0;
            for (var i = 0; i < model.Zones.Count; i++)
            {
                var z = model.Zones[i];
                double x0 = ox + Math.Min(z.X0, z.X1) / 512.0, x1 = ox + Math.Max(z.X0, z.X1) / 512.0;
                double z0 = oz + Math.Min(z.Z0, z.Z1) / 512.0, z1 = oz + Math.Max(z.Z0, z.Z1) / 512.0;
                var hits = line.Where(p => p.X >= x0 - margin && p.X <= x1 + margin && p.Z >= z0 - margin && p.Z <= z1 + margin).ToList();
                if (hits.Count == 0) continue;
                var inside = hits.Count(p => p.X >= x0 && p.X <= x1 && p.Z >= z0 && p.Z <= z1);
                var yLo = Math.Min(z.Y0, z.Y1); var yHi = Math.Max(z.Y0, z.Y1);
                var roadY = hits.Average(p => p.Y);
                var type = z.Type >= 0 && z.Type < names.Length ? names[z.Type] : z.Type.ToString();
                Console.WriteLine($"scene {scene} (cube {model.CubeX},{model.CubeY}) zone {i}: {type} num {z.Num}, cells ({x0:0.0},{z0:0.0})-({x1:0.0},{z1:0.0}) = {x1 - x0:0.0} x {z1 - z0:0.0}, " +
                                  $"height {yLo}..{yHi} (road ~{roadY:0}), line points inside {inside}, near {hits.Count}, info [{string.Join(",", z.Info)}]");
            }
        }
        return 0;
    }
}
