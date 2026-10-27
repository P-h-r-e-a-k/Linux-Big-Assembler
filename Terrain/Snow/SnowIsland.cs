using System.IO;
using LBAAssembler.Lba1;
using LBAAssembler.Maps;
using LBAAssembler.Scenes;

namespace LBAAssembler.Terrain.Snow;

// The snow island, built as a map of its own (Maps/LbaMap: SNOWISLE.lbamap, an island's ILE, OBL and an outside scene a cube) -- a mod
// apart from the race tracks (the user, 2026-10-22: "This will be a separate mod to our racing mod, but for now let's build it into
// LBAAssembler for testing"). It is made from SnowDesign by SnowTerrain (the ground, LBA1's Hamalayi bricks its textures: the LBA1 folder
// is needed to build it, not to play it) and SnowObjects (its things), its look Island CX's (sky, palette, music, holomap: the island
// number its scenes carry). The engine plays it from a game folder's MAPS folder; Tools > LBA2: Snow island builds it and opens it, as File >
// Open map would, in a workspace -- File > Install map into game puts it into the game.
internal static class SnowIsland
{
    public const string MapName = "Snow island", FileName = "SNOWISLE";

    public sealed record Built(LbaMap Map, int StartScene, List<string> Log);

    public static Built Build(string lba1Directory, string gameDirectory)
    {
        if (!Lba1Game.IsInstalled(lba1Directory)) throw new InvalidDataException($"No LBA1 game in {lba1Directory}: the snow island's ground is textured with LBA1's Hamalayi bricks.");
        if (!File.Exists(Path.Combine(gameDirectory, SnowTerrain.LookIsland + ".ILE"))) throw new InvalidDataException($"The game folder has no {SnowTerrain.LookIsland}.ILE (the island whose look it takes).");
        var lba1 = new Lba1Game(lba1Directory);
        var terrain = SnowTerrain.Build(lba1, gameDirectory);
        var log = new List<string>(terrain.Log);
        var island = terrain.Island;

        // the things, lit by the cubes' sun
        var heights = new double[SnowDesign.Width + 1, SnowDesign.Depth + 1];
        foreach (var (cx, cz) in SnowDesign.Cubes())
        {
            var cube = island.CubeAt(cx, cz)!;
            int u0 = (cx - SnowDesign.CubeX0) * 64, v0 = (cz - SnowDesign.CubeZ0) * 64;
            for (var z = 0; z <= 64; z++) for (var x = 0; x <= 64; x++) heights[u0 + x, v0 + z] = cube.Height(x, z);
        }
        var sun = BakeOptions.For(island.CubeAt(SnowDesign.CubeX0 + 1, SnowDesign.CubeZ0)!);
        var objects = SnowObjects.Build(island, new SnowShading(terrain.Palette, sun.Azimuth, sun.Elevation), heights);
        log.AddRange(objects.Log);

        var ile = island.ToBytes();
        var obl = Polar.PolarIsland.Hqr(objects.Bodies);
        var map = MapNew.IslandMap(gameDirectory, MapName, FileName, ile, obl, SnowTerrain.LookIslandByte);
        map.Weather = "snow";
        map.Description = "A snowy island in the spirit of LBA1's Hamalayi Mountains: ice cliffs and terraces, a canyon up to the prison gate, " +
                          "a frozen pond, the Rabbibunnies' village, a ski slope with its chair lift, and snowmen.";

        // Twinsen in each scene: the start (the pier's foot) in its cube; elsewhere the walkable cell nearest the cube's middle
        var start = -1;
        for (var k = 0; k < map.Scenes.Count; k++)
        {
            var record = map.Scenes[k];
            var scene = SceneSerializer.Parse(SceneGame.Lba2, record.Record);
            var (cx, cz) = (record.CubeX, record.CubeZ);
            double u, v;
            var (su, sv) = SnowDesign.Start;
            if (SnowDesign.Gx(su) / 64 == cx && SnowDesign.Gz(sv) / 64 == cz) { (u, v) = (su, sv); start = k; }
            else (u, v) = Walkable(heights, cx, cz);
            scene.Hero.X = (int)Math.Round((SnowDesign.Gx(u) - cx * 64 + (u - Math.Floor(u))) * IslandFile.CellSize);
            scene.Hero.Z = (int)Math.Round((SnowDesign.Gz(v) - cz * 64 + (v - Math.Floor(v))) * IslandFile.CellSize);
            scene.Hero.Y = (int)Math.Round(heights[(int)Math.Round(u), (int)Math.Round(v)]) + 64;
            scene.Hero.Beta = 2048;
            map.Scenes[k] = record with { Record = SceneSerializer.Write(scene) };
        }
        log.Add($"{map.Scenes.Count} scenes, one a cube; Twinsen starts on the quay at the pier's foot (the map's scene {start + 1})");
        return new Built(map, start, log);
    }

    // The cell of a cube nearest its middle where Twinsen can stand: land, level, not under a thing (the design's: clear of the ways'
    // things is near enough -- the paths and the snow).
    private static (double U, double V) Walkable(double[,] h, int cx, int cz)
    {
        int u0 = (cx - SnowDesign.CubeX0) * 64, v0 = (cz - SnowDesign.CubeZ0) * 64;
        var best = (U: u0 + 32.5, V: v0 + 32.5); var bestD = double.MaxValue;
        for (var v = v0 + 2; v < v0 + 62; v++)
            for (var u = u0 + 2; u < u0 + 62; u++)
            {
                var c = new[] { h[u, v], h[u + 1, v], h[u, v + 1], h[u + 1, v + 1] };
                if (c.Min() <= 64 || c.Max() - c.Min() > 120) continue;
                var kind = SnowDesign.KindAt(u + 0.5, v + 0.5, c.Average());
                if (kind is not (SnowDesign.Kind.Snow or SnowDesign.Kind.Gravel or SnowDesign.Kind.Piste)) continue;
                var d = (u + 0.5 - u0 - 32) * (u + 0.5 - u0 - 32) + (v + 0.5 - v0 - 32) * (v + 0.5 - v0 - 32);
                // (gravel first: a way, where no tree stands)
                if (kind == SnowDesign.Kind.Gravel) d *= 0.25;
                if (d < bestD) { bestD = d; best = (u + 0.5, v + 0.5); }
            }
        return best;
    }
}
