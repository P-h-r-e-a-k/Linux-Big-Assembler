using System.Buffers.Binary;
using System.IO;
using LBAAssembler.LbaScript;
using LBAAssembler.Scenes;

namespace LBAAssembler.Terrain.Palace;

// The Emperor's palace race (the user, 2026-10-12: "through the palace rooms, coming out the window and around the roof tops? Point-to-point
// i.e. no laps, finishing with massive jump to harbor teleport? - Twinsen, quick, Dark monk's in the palace, he's killing everybody. Leading
// into final showdown at Celebration"), on the Palace island (PalaceIsland: PALACE.ILE and PALACE.OBL), raced as Otringal -- island 4 --
// drawn from the palace's file (the race-track mode's island_file=). The race track's build makes all of it (RaceTrackIsland.Palace):
//   - the island, afresh from the folder's palace scenes and its original Otringal files (PalaceIsland.Build);
//   - its scenes: an LBA2 outside scene is one cube of its island, so the cubes the road runs through are its scenes (FirstScene on, row by
//     row: SceneCubes), each a copy of the header of Otringal's scene outside the palace (91: its light, ambience and music) with its own
//     cube, nobody in it but Twinsen, the engine's Zoe placeholder and the Desert island's buggy (the engine hands the car on to the next
//     scene's at a cube change), and a cube-change zone along each edge it shares with another of them. Past 254 they have no holomap
//     arrow (the engine's CubeArrowFlags): none is needed, nothing in the game leads there;
//   - the race's lines (the race-track mode's sprint: intro=, lose=, wake=): Twinsen's cry as the grid forms and a loss's line, at the end of
//     Otringal's texts; and the line he says arriving on Celebration Island through the Dark Monk's teleporter -- the race won, the
//     picture whitening as the car flies into it (wake_flight=) and Celebration's scene fading in from white -- at the end of Celebration
//     Island's.
// The route: docs/racetrack/palace_track_plan.json (tools/RaceTrackPlan/palace_design.py).
internal static class PalaceTrack
{
    // past the retail game's 0..221 and the race tracks' 222..264 (the palace's: 265 on, one a cube of SceneCubes)
    public const int FirstScene = 265;
    // The cubes the road runs through (and the jump's flight), row by row: each a scene. From the route built into the program: each
    // point's cube, and those its road's width and a cell more reach -- a car on the road's edge (the rooftops' road swinging in among
    // the domes, 2026-10-12, ran into a cube with no scene, and the car stopped at its edge).
    public const string PlanResource = "RaceTrackPlan.Palace.json";
    public static readonly (int Cx, int Cz)[] SceneCubes = CubesOf(PlanResource);
    public static int LastScene => FirstScene + SceneCubes.Length - 1;

    private static (int Cx, int Cz)[] CubesOf(string resource)
    {
        // (none without the plan -- the build then says it has none -- rather than every island's race track failing with this class)
        using var stream = typeof(PalaceTrack).Assembly.GetManifestResourceStream(resource);
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
        // (and every cube where the cars drive where they like: the rooms, any way through them -- RaceTrackPlan.OpenAreas)
        foreach (var a in (plan.OpenAreas ?? Array.Empty<double[]>()).Where(a => a.Length >= 4))
            for (var cz = (int)Math.Floor((plan.OriginCellZ + a[1]) / 64); cz <= (int)Math.Floor((plan.OriginCellZ + a[3]) / 64); cz++)
                for (var cx = (int)Math.Floor((plan.OriginCellX + a[0]) / 64); cx <= (int)Math.Floor((plan.OriginCellX + a[2]) / 64); cx++)
                    cubes.Add((cx, cz));
        return cubes.OrderBy(c => c.Cz).ThenBy(c => c.Cx).ToArray();
    }
    public static int SceneOf(int cx, int cz) => Array.IndexOf(SceneCubes, (cx, cz)) is var i and >= 0 ? FirstScene + i : -1;
    // Otringal's scene outside the palace: every scene's header (its light, ambience, music)
    private const int SourceScene = 91;
    private const int Near = 512, Far = 32768 - 1024, Edge = 512, Side = 32768, ZoneTop = 32000;
    public const int IslandByte = 4;

    // the race's end: Twinsen arrives on Celebration Island (scene 95, the island with the Dark Monk's statue) and says the wake line
    public const int WakeScene = 95, WakeActor = 0;
    // ... the statue risen: a win sets the game's variable the engine draws Celebration Island from CELEBRA2 by (FLAG_CELEBRATION) -- the
    // user, 2026-10-13: "the black dot is where the race ends in a large jump where we'll then zone transition to CELEBRA2.ILE"
    public const int StatueVar = 79;
    // the race ends at the leap's lip, and the dream fades this long after it, in mid-flight
    public const int WakeFlightMs = 2400;
    // TEXT.HQR's files: an island's is the engine's file 3 + its number (MESSAGE.CPP TextEntry): Otringal's 7, Celebration Island's 8
    private const int OtringalTexts = 3 + 4, CelebrationTexts = 3 + 5;

    private static readonly string[] Intro =
    {
        "The Dark Monk is in the palace, and he's killing everybody! I have to get out: through the rooms, up the stairs, past the throne and back down, out of the round window and into his teleporter!",
        "Le Moine Noir est dans le palais, et il tue tout le monde ! Je dois sortir : par les salles, en haut de l'escalier, devant le trône et redescendre, par la fenêtre ronde jusque dans son téléporteur !",
        "Der Schwarze Mönch ist im Palast und bringt alle um! Ich muss hier raus: durch die Säle, die Treppe hinauf, am Thron vorbei und wieder hinunter, aus dem runden Fenster in seinen Teleporter!",
        "¡El Monje Negro está en el palacio y está matando a todos! Tengo que salir: por las salas, escaleras arriba, delante del trono y otra vez abajo, por la ventana redonda hasta su teletransportador!",
        "Il Monaco Nero è nel palazzo e sta uccidendo tutti! Devo uscire: attraverso le sale, su per le scale, davanti al trono e di nuovo giù, dalla finestra rotonda fino al suo teletrasporto!",
        "O Monge Negro está no palácio e está a matar toda a gente! Tenho de sair: pelas salas, escadas acima, diante do trono e de novo para baixo, pela janela redonda até ao teletransportador dele!",
    };
    private static readonly string[] Lose =
    {
        "Too slow... the Dark Monk's teleporter is gone. The palace again -- faster!",
        "Trop lent... le téléporteur du Moine Noir a disparu. Encore le palais -- plus vite !",
        "Zu langsam... der Teleporter des Schwarzen Mönchs ist weg. Noch einmal durch den Palast -- schneller!",
        "Demasiado lento... el teletransportador del Monje Negro ha desaparecido. ¡Otra vez el palacio, más rápido!",
        "Troppo lento... il teletrasporto del Monaco Nero è sparito. Di nuovo il palazzo -- più veloce!",
        "Demasiado lento... o teletransportador do Monge Negro desapareceu. Outra vez o palácio -- mais depressa!",
    };
    private static readonly string[] Wake =
    {
        "Celebration Island... his teleporter brought me to his statue. This is where it ends, Dark Monk!",
        "L'île de la Célébration... son téléporteur m'a mené à sa statue. C'est ici que tout se termine, Moine Noir !",
        "Die Insel der Feierlichkeiten... sein Teleporter hat mich zu seiner Statue gebracht. Hier endet es, Schwarzer Mönch!",
        "La isla de la Celebración... su teletransportador me ha traído a su estatua. ¡Aquí termina todo, Monje Negro!",
        "L'isola della Celebrazione... il suo teletrasporto mi ha portato alla sua statua. È qui che finisce, Monaco Nero!",
        "A ilha da Celebração... o teletransportador dele trouxe-me até à estátua. É aqui que tudo acaba, Monge Negro!",
    };

    // The island and its scenes, made afresh in a game folder whose files are the originals (the race track's build: Otringal's own files
    // from their backups when it has them -- another track may have changed the folder's).
    public static List<string> Install(string gameDirectory, string backupSuffix)
    {
        var log = new List<string>();
        var source = gameDirectory;
        if (File.Exists(Path.Combine(gameDirectory, PalaceIsland.SourceIle + backupSuffix)))
        {
            source = Path.Combine(Path.GetTempPath(), "LBAAssembler-palace-" + Environment.ProcessId);
            Directory.CreateDirectory(source);
            foreach (var f in new[] { PalaceIsland.SourceIle, PalaceIsland.SourceObl })
            {
                var from = Path.Combine(gameDirectory, f + backupSuffix);
                File.Copy(File.Exists(from) ? from : Path.Combine(gameDirectory, f), Path.Combine(source, f), true);
            }
        }
        try { log.Add(PalaceIsland.Build(gameDirectory, gameDirectory, source).Log); }
        finally { if (source != gameDirectory) try { Directory.Delete(source, true); } catch (IOException) { } }
        log.AddRange(AddScenes(gameDirectory));
        return log;
    }

    // The island's files and scenes out of a folder (the scenes go with the folder's SCENE.HQR, which the build puts back from its own copy).
    public static bool Remove(string gameDirectory)
    {
        var gone = false;
        foreach (var f in new[] { PalaceIsland.IleFile, PalaceIsland.OblFile, PalaceIsland.IleFile + ".bak", PalaceIsland.LayoutFile })
        {
            var path = Path.Combine(gameDirectory, f);
            if (File.Exists(path)) { File.Delete(path); gone = true; }
        }
        return gone;
    }

    private static List<string> AddScenes(string gameDirectory)
    {
        var log = new List<string>();
        var scenePath = Path.Combine(gameDirectory, "SCENE.HQR");
        var source = SceneSerializer.Parse(SceneGame.Lba2, HqrArchive.Open(scenePath).Read(SourceScene + 1));
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
        var island = IslandFile.Load(Path.Combine(gameDirectory, PalaceIsland.IleFile));
        foreach (var (cx, cz) in SceneCubes)
        {
            var model = source.Clone();
            model.Island = IslandByte; model.CubeX = cx; model.CubeY = cz;
            var keepZoe = model.Actors.Count > 1 && model.Actors[1].Entity == 14 && model.Actors[1].X == 0 && model.Actors[1].Z == 0;
            model.Actors.RemoveRange(keepZoe ? 2 : 1, model.Actors.Count - (keepZoe ? 2 : 1));
            model.Zones.Clear(); model.TrackPoints.Clear();
            foreach (var a in model.Actors) { a.Life = new byte[] { 0 }; a.Track = new byte[] { 0 }; }
            // (Twinsen: on the highest ground in the cube's middle -- the race track's build puts him on the grid in the start's scene)
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
            names = HqdWriter.Describe(gameDirectory, "SCENE.HQR", SceneGame.Lba2, SceneOf(cx, cz) + 1, $"The Emperor's palace race: cube ({cx}, {cz})", names);
        if (names is not null) File.WriteAllText(Path.Combine(gameDirectory, HqdWriter.SidecarName("SCENE.HQR")), names, System.Text.Encoding.Latin1);
        log.Add($"SCENE.HQR: scenes {FirstScene}-{LastScene}, the palace island's cubes the road runs through ({zones} cube-change zones), Otringal's scene {SourceScene}'s light and music");
        return log;
    }

    // The race's lines into a game folder: the intro and the loss's line at the end of Otringal's texts, the arrival line at the end of
    // Celebration Island's, in every language the game has. Returns the log line and the three texts' ids.
    public static (string Log, int Intro, int Lose, int Wake) Texts(string gameDirectory)
    {
        var textPath = Path.Combine(gameDirectory, "TEXT.HQR");
        var languages = Math.Min(Lba2TextBank.Languages(textPath), Intro.Length);
        int NextId(int file)
        {
            var text = HqrArchive.Open(textPath);
            return Enumerable.Range(0, languages).Max(l => Lba2TextBank.Load(text, l, file).Texts.Select(t => t.Id).DefaultIfEmpty(0).Max()) + 1;
        }
        void Append(int file, params (int Id, string[] Lines)[] texts)
        {
            var archive = HqrArchive.Open(textPath);
            var hqr = File.ReadAllBytes(textPath);
            for (var lang = 0; lang < languages; lang++)
            {
                var lines = Lba2TextBank.Load(archive, lang, file);
                var attribute = lines.Find(0)?.Attribute ?? Lba2TextBank.NormalAttribute;
                foreach (var (id, words) in texts)
                    lines.Texts.Add(new Lba2TextBank.Text { Id = id, Attribute = attribute, Bytes = Polar.PolarDream.Dos.GetBytes(words[lang < words.Length ? lang : 0]) });
                hqr = lines.WriteInto(hqr);
            }
            File.WriteAllBytes(textPath, hqr);
        }
        var intro = NextId(OtringalTexts);
        Append(OtringalTexts, (intro, Intro), (intro + 1, Lose));
        var wake = NextId(CelebrationTexts);
        Append(CelebrationTexts, (wake, Wake));
        return ($"the race's lines: texts {intro} and {intro + 1} of Otringal's, {wake} of Celebration Island's, in all {languages} languages", intro, intro + 1, wake);
    }
}
