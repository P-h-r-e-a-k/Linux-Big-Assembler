using LBAAssembler.Terrain;

namespace ScriptRoundTrip;

// islandtwin <A.ILE> <B.ILE>: how alike two island files are -- the same cubes, the same heights, the same polygons (ground types and
// texture choices), the same decors, and the same ground texture page -- to tell whether a track built on one can be built the same on
// the other (Citadel Island's two files: CITADEL in the storm, CITABAU once the storm is over).
internal static class IslandTwinCommand
{
    public static int Run(string[] args)
    {
        var a = IslandFile.Load(args[1]); var b = IslandFile.Load(args[2]);
        Console.WriteLine($"cubes: {a.Cubes.Count} and {b.Cubes.Count}; same set: {a.Cubes.Keys.SequenceEqual(b.Cubes.Keys)}");
        int heightDiff = 0, polyDiff = 0, decorDiff = 0, texDiff = 0, intensityDiff = 0; long heights = 0;
        foreach (var (key, ca) in a.Cubes)
        {
            if (!b.Cubes.TryGetValue(key, out var cb)) continue;
            if (ca.Heights is { } ha && cb.Heights is { } hb) for (var i = 0; i < Math.Min(ha.Length, hb.Length); i++) { heights++; if (ha[i] != hb[i]) heightDiff++; }
            for (var z = 0; z < IslandCube.Cells; z++) for (var x = 0; x < IslandCube.Cells; x++) for (var h = 0; h < 2; h++)
                if (ca.HasPolygons && cb.HasPolygons && ca.Polygon(x, z, h) != cb.Polygon(x, z, h)) polyDiff++;
            if (ca.Decors.Count != cb.Decors.Count || !ca.Decors.Select(d => (d.Body, d.X, d.Y, d.Z)).SequenceEqual(cb.Decors.Select(d => (d.Body, d.X, d.Y, d.Z)))) decorDiff++;
            if (!ca.TextureDefs.SequenceEqual(cb.TextureDefs)) texDiff++;
            if (ca.HasIntensity && cb.HasIntensity && !ca.Intensity.SequenceEqual(cb.Intensity)) intensityDiff++;
        }
        Console.WriteLine($"heights differing: {heightDiff} of {heights}; polygon halves differing: {polyDiff}; cubes whose decors differ: {decorDiff}; cubes whose texture definitions differ: {texDiff}; cubes whose light differs: {intensityDiff}");
        var page = a.GroundTexture.Zip(b.GroundTexture).Count(p => p.First != p.Second);
        Console.WriteLine($"ground texture page pixels differing: {page} of {a.GroundTexture.Length}");
        return 0;
    }
}
