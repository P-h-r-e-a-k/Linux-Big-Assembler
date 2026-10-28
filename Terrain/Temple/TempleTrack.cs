using System.Buffers.Binary;
using System.IO;
using LBAAssembler.Scenes;

namespace LBAAssembler.Terrain.Temple;

// The Temple of Bú's Halloween race (the user, 2026-10-16: "After it's been ported generate a Halloween inspired race track for it. This will
// be a separate release for a Halloween special track before our full racing mod is released. Let's make it as creepy, scary, and Halloween
// inspired as we can do, let's turn our mushroom pickups into mini pumpkins. Add lots of typical Halloween decor, pumpkins, witches, ghosts
// etc"), on the temple island (TempleIsland: TEMPLE.ILE and TEMPLE.OBL), raced as Otringal -- island 4 -- drawn from the temple's file in its
// own palette under its own sky (the race-track mode's island_file=, island_palette=, island_sky=). The race track's build makes all of it
// (RaceTrackIsland.Temple):
//   - the island, afresh from the LBA1 folder's temple scenes, the doorways the road goes through cut in its walls (the plan's TempleCuts);
//   - its scenes: an LBA2 outside scene is one cube of its island, so the cubes the road runs through are its scenes (FirstScene on, row by
//     row: SceneCubes), each a copy of the header of Otringal's scene 91 (its light and ambience) with its own cube, nobody in it but
//     Twinsen, the engine's Zoe placeholder and the Desert island's buggy (the engine hands the car on to the next scene's at a cube change),
//     and a cube-change zone along each edge it shares with another of them, as the Emperor's palace's (PalaceTrack).
// The route: docs/racetrack/temple_track_plan.json (tools/RaceTrackPlan/temple_design.py).
internal static class TempleTrack
{
    // past the retail game's scenes and the other tracks' (the palace's 265-299, Island CX's 300)
    public const int FirstScene = 301;
    public const string PlanResource = "RaceTrackPlan.Temple.json";
    // (and, after the route's, the cubes of the secret party's way and its hall, walked on foot -- TempleHalloween.PartyCubes: after them so
    // that no scene of the route's moves)
    private static readonly (int Cx, int Cz)[] RouteCubes = CubesOf(PlanResource);
    public static readonly (int Cx, int Cz)[] SceneCubes = WithParty(RouteCubes);
    private static (int Cx, int Cz)[] WithParty((int Cx, int Cz)[] route) => route.Length == 0 ? route : route.Concat(TempleHalloween.PartyCubes.Where(c => !route.Contains(c))).ToArray();
    public static int LastScene => FirstScene + Math.Max(1, SceneCubes.Length) - 1;
    public const int IslandByte = TempleIsland.IslandByte;
    // Otringal's scene outside the palace: every scene's header (its light) -- and LBA2's own Temple of Bù's first scene (scene 10) their
    // music and their ambient sounds: the temple's, in the second game
    private const int SourceScene = 91, SoundScene = 10;
    private const int Near = 512, Far = 32768 - 1024, Edge = 512, Side = 32768, ZoneTop = 32000;

    private static (int Cx, int Cz)[] CubesOf(string resource)
    {
        using var stream = typeof(TempleTrack).Assembly.GetManifestResourceStream(resource);
        if (stream is null) return Array.Empty<(int, int)>();
        var plan = RaceTrackPlan.Read(stream);
        var cubes = new HashSet<(int Cx, int Cz)>();
        for (var i = 0; i < plan.Points.Length; i++)
        {
            var reach = (plan.RaisedHalfs is { } halfs && i < halfs.Length ? halfs[i] : plan.RaisedHalf ?? 4.5) + 1;
            double x = plan.OriginCellX + plan.Points[i][0], z = plan.OriginCellZ + plan.Points[i][1];
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                    cubes.Add(((int)Math.Floor((x + dx * reach) / 64), (int)Math.Floor((z + dz * reach) / 64)));
        }
        // (and its branches' roads -- the hidden passage's shortcut: its spur and its way out -- not their gaps, where there is no road)
        foreach (var b in plan.Branches ?? Array.Empty<BranchRun>())
            for (var i = 0; i < b.Points.Length; i++)
            {
                var half = b.Halfs is { } halfs && i < halfs.Length ? halfs[i] : plan.RaisedHalf ?? 4.5;
                if (half <= 0) continue;
                double x = plan.OriginCellX + b.Points[i][0], z = plan.OriginCellZ + b.Points[i][1];
                for (var dz = -1; dz <= 1; dz++)
                    for (var dx = -1; dx <= 1; dx++)
                        cubes.Add(((int)Math.Floor((x + dx * (half + 1)) / 64), (int)Math.Floor((z + dz * (half + 1)) / 64)));
            }
        return cubes.OrderBy(c => c.Cz).ThenBy(c => c.Cx).ToArray();
    }
    public static int SceneOf(int cx, int cz) => Array.IndexOf(SceneCubes, (cx, cz)) is var i and >= 0 ? FirstScene + i : -1;

    // The island and its scenes, made afresh in a game folder whose files are the originals (the race track's build), from the LBA1 folder.
    public static List<string> Install(string gameDirectory, string lba1Directory)
    {
        var plan = RaceTrackPlan.Built(RaceTrackIsland.Temple);
        // (the secret party's doorway and bridge with the road's doorways and walls: TempleHalloween.PartyCuts, PartyWalls)
        var built = TempleIsland.Build(lba1Directory, gameDirectory, cuts: (plan.TempleCuts ?? Array.Empty<int[]>()).Concat(TempleHalloween.PartyCuts()).ToArray(),
            floors: plan.TempleFloors, skirts: plan.TempleSkirts, road: plan, walls: (plan.TempleWalls ?? Array.Empty<int[]>()).Concat(TempleHalloween.PartyWalls()).ToArray());
        var log = new List<string>(built.Log);
        log.AddRange(TempleIsland.Install(gameDirectory, built));
        log.AddRange(AddScenes(gameDirectory));
        // (the race's music: LBA1's temple music from the player's LBA1, when it has it -- the scenes' own, LBA2's Temple of Bù's, else)
        log.AddRange(TempleMusic.Install(gameDirectory, lba1Directory));
        return log;
    }

    private static List<string> AddScenes(string gameDirectory)
    {
        var log = new List<string>();
        var scenePath = Path.Combine(gameDirectory, "SCENE.HQR");
        var source = SceneSerializer.Parse(SceneGame.Lba2, HqrArchive.Open(scenePath).Read(SourceScene + 1));
        var sound = SceneSerializer.Parse(SceneGame.Lba2, HqrArchive.Open(scenePath).Read(SoundScene + 1));
        source.Music = sound.Music;
        source.Ambient = sound.Ambient.Select(a => a.Clone()).ToArray();
        source.SecondMin = sound.SecondMin; source.SecondEcart = sound.SecondEcart;
        var hqr = HqrFile.Parse(File.ReadAllBytes(scenePath));
        var buggy = HqrArchive.Open(scenePath) is var archive && SceneSerializer.Parse(SceneGame.Lba2, archive.Read(RaceTrackScenes.BuggyScene + 1)) is var desert
            ? desert.Actors.Skip(1).FirstOrDefault(a => a.Entity == RaceTrackScenes.BuggyEntity)?.Clone() : null;
        if (buggy is not null)
        {
            RaceTrackScenes.BuggyAlwaysThere(buggy);
            if (buggy.Life.Length > 9 && buggy.Life[8] == 0x46 && buggy.Life[9] == 0) buggy.Life[9] = 1;
        }
        var cubes = SceneCubes.ToHashSet();
        var largest = BinaryPrimitives.ReadInt32LittleEndian(hqr.Read(0));
        var zones = 0;
        var island = IslandFile.Load(Path.Combine(gameDirectory, TempleIsland.IleFile));
        foreach (var (cx, cz) in SceneCubes)
        {
            var model = source.Clone();
            model.Island = IslandByte; model.CubeX = cx; model.CubeY = cz;
            var keepZoe = model.Actors.Count > 1 && model.Actors[1].Entity == 14 && model.Actors[1].X == 0 && model.Actors[1].Z == 0;
            model.Actors.RemoveRange(keepZoe ? 2 : 1, model.Actors.Count - (keepZoe ? 2 : 1));
            model.Zones.Clear(); model.TrackPoints.Clear();
            foreach (var a in model.Actors) { a.Life = new byte[] { 0 }; a.Track = new byte[] { 0 }; }
            // (Twinsen: in the cube's middle, over the temple's highest there or the sea -- the race track's build puts him on the grid in the
            // start's scene)
            var hero = model.Hero;
            hero.X = Side / 2; hero.Z = Side / 2; hero.Y = Math.Max(0, (int)(IslandOps.Altitude(island, cx * Side + Side / 2.0, cz * Side + Side / 2.0) ?? 0));
            hero.Beta = 0;
            if (buggy is not null)
            {
                var copy = buggy.Clone();
                copy.X = hero.X + 1024; copy.Y = hero.Y; copy.Z = hero.Z; copy.Beta = 0;
                SceneOps.AddActor(model, copy);
            }
            void Zone(int dx, int dz, int x0, int z0, int x1, int z1, int info0, int info2)
            {
                if (!cubes.Contains((cx + dx, cz + dz))) return;
                var zone = new SceneZoneModel { Type = 0, Num = SceneOf(cx + dx, cz + dz), X0 = x0, Y0 = 0, Z0 = z0, X1 = x1, Y1 = ZoneTop, Z1 = z1, Info = new int[8] };
                zone.Info[0] = info0; zone.Info[2] = info2; zone.Info[7] = 1;   // ZONE_ON
                model.Zones.Add(zone);
                zones++;
            }
            Zone(1, 0, Side - Edge, 0, Side, Side, Near, 0);
            Zone(-1, 0, 0, 0, Edge, Side, Far, 0);
            Zone(0, 1, 0, Side - Edge, Side, Side, 0, Near);
            Zone(0, -1, 0, 0, Side, Edge, 0, Far);
            var record = SceneSerializer.Write(model);
            var entry = SceneOf(cx, cz) + 1;
            while (hqr.Count < entry) hqr.Slots.Add(new HqrFile.Slot());
            if (hqr.Count == entry) hqr.Add(record); else hqr.SetStored(entry, record);
            largest = Math.Max(largest, record.Length);
        }
        var size = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(size, largest);
        hqr.SetStored(0, size);
        File.WriteAllBytes(scenePath, hqr.ToBytes());
        // (their names, for the editor's scene list: the folder's SCENE.HQD, which it reads over the game's own descriptions)
        string? names = null;
        foreach (var (cx, cz) in SceneCubes)
            names = HqdWriter.Describe(gameDirectory, "SCENE.HQR", SceneGame.Lba2, SceneOf(cx, cz) + 1, $"The Temple of Bú's Halloween race: cube ({cx}, {cz})", names);
        if (names is not null) File.WriteAllText(Path.Combine(gameDirectory, HqdWriter.SidecarName("SCENE.HQR")), names, System.Text.Encoding.Latin1);
        log.Add($"SCENE.HQR: scenes {FirstScene}-{LastScene}, the temple island's cubes the road runs through ({zones} cube-change zones), Otringal's scene {SourceScene}'s light, " +
                $"LBA2's Temple of Bù's music ({sound.Music}: the race's when LBA1's isn't there, TempleMusic) and sounds (scene {SoundScene})");
        return log;
    }
}
