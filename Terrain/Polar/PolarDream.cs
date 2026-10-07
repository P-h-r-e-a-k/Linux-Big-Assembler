using System.IO;
using System.Text;
using LBAAssembler.LbaScript;
using LBAAssembler.Scenes;

namespace LBAAssembler.Terrain.Polar;

// Polar Island's dream race (the user's, 2026-10-06): the race track picks up from the end of the first game. Twinsen is dreaming: he is
// back on Polar Island and FunFrock has escaped, racing him to Sendell to get to her first. A sprint from the dock to the top of the
// rocky peak (RaceTrackIsland.Polar, docs/racetrack/polar_track_plan.json). Won, Twinsen is shaken awake by Zoe at home, in his bed in
// the second game's first scene (scene 0); lost, the race is run again. The race-track mode does it (RACEMOD.CPP sprint=, wake=):
//   - the intro, said by Twinsen as the grid forms, and the loss's line: texts 1 and 2 of the island's own text file (island 12's,
//     PolarScenes.WriteTexts), next to its name;
//   - Twinsen wakes up lying in his bed (2026-10-06, later): scene 0's own opening, changed for the dream's end (ApplyOpening) -- won,
//     the race sets DreamVar, and in scene 0 Twinsen lies asleep on his bed (Twinsen's own animation 56, scene 101's: the Wannies' bed
//     he sleeps in after their firefly tart), Zoe beside it says the wake line at once (a text at the end of Citadel Island's file,
//     where scene 0's texts are: no recorded voice, shown not spoken), steps out of his way, he sits up and gets out of bed (57 and 58,
//     as in scene 101) and turns to her, she comes to him and kisses him, and her own opening line follows (the race track story's
//     about the rain, where the story is built).
// The texts are in every language the game has, in its own order (English, French, German, Spanish, Italian, Portuguese) and code page.
internal static class PolarDream
{
    public const int IntroText = 1, LoseText = 2;
    // the game variable a win sets (the race-track mode's win=): 1, Twinsen wakes up in his bed; 2 once Zoe has woken him, 3 as he gets
    // up, 4 as she comes to kiss him (200-204 are the race track story's, nothing in the game uses 205; 206 is the engine's Citadel weather)
    public const int DreamVar = 205;
    // Twinsen wakes up in the game's first scene, at its own start (the foot of his bed), and Zoe says the wake line
    public const int WakeScene = 0, WakeActor = 4;
    // the car's top speed for the race (km/h): the race car setup's gears scaled to it (its normal top is 80; 140 until the island was made
    // twice its size, 2026-10-06)
    public const int TopKmh = 120;
    // the race ends at the lip of the jump into the rocky peak, and Twinsen wakes up this long after it, in mid-flight: he never lands
    // (the engine's wake_flight=)
    public const int WakeFlightMs = 450;
    private const int CitadelTexts = 3;

    private static readonly string[] Intro =
    {
        "Polar Island? FunFrock has escaped, and he's racing to Sendell! If he gets to her first, the whole planet is lost. I have to beat him to the rocky peak!",
        "L'île Polaire ? FunFrock s'est échappé et il file vers Sendell ! S'il l'atteint le premier, toute la planète est perdue. Je dois arriver avant lui au pic rocheux !",
        "Die Polarinsel? FunFrock ist entkommen und rast zu Sendell! Wenn er sie zuerst erreicht, ist der ganze Planet verloren. Ich muss vor ihm am Felsgipfel sein!",
        "¿La Isla Polar? ¡FunFrock ha escapado y corre hacia Sendell! Si llega a ella primero, todo el planeta estará perdido. ¡Tengo que llegar antes que él al pico rocoso!",
        "L'Isola Polare? FunFrock è fuggito e corre verso Sendell! Se arriva da lei per primo, l'intero pianeta è perduto. Devo arrivare al picco roccioso prima di lui!",
        "A Ilha Polar? O FunFrock fugiu e corre para Sendell! Se chegar primeiro, o planeta inteiro está perdido. Tenho de chegar ao pico rochoso antes dele!",
    };

    private static readonly string[] Lose =
    {
        "No! FunFrock got to Sendell first... It can't end like this. Again!",
        "Non ! FunFrock a atteint Sendell le premier... Ça ne peut pas finir comme ça. Encore !",
        "Nein! FunFrock hat Sendell zuerst erreicht... So darf es nicht enden. Noch einmal!",
        "¡No! FunFrock ha llegado antes a Sendell... No puede terminar así. ¡Otra vez!",
        "No! FunFrock è arrivato da Sendell per primo... Non può finire così. Ancora!",
        "Não! O FunFrock chegou primeiro a Sendell... Não pode acabar assim. Outra vez!",
    };

    private static readonly string[] Wake =
    {
        "Twinsen! Twinsen, wake up! You were tossing and turning all night. Were you dreaming about FunFrock again?",
        "Twinsen ! Twinsen, réveille-toi ! Tu t'es agité toute la nuit. Tu rêvais encore de FunFrock ?",
        "Twinsen! Twinsen, wach auf! Du hast dich die ganze Nacht hin und her gewälzt. Hast du wieder von FunFrock geträumt?",
        "¡Twinsen! ¡Twinsen, despierta! Te has pasado la noche dando vueltas. ¿Estabas soñando otra vez con FunFrock?",
        "Twinsen! Twinsen, svegliati! Ti sei agitato tutta la notte. Stavi di nuovo sognando FunFrock?",
        "Twinsen! Twinsen, acorda! Andaste às voltas a noite toda. Estavas outra vez a sonhar com o FunFrock?",
    };

    // The island's own texts after its name, in a language (a language this doesn't know: the English).
    public static IEnumerable<(int Id, string Text)> IslandTexts(int language)
    {
        var words = language < Intro.Length ? language : 0;
        yield return (IntroText, Intro[words]);
        yield return (LoseText, Lose[words]);
    }

    // The race's texts into a game folder: the island's (its text file written again with them) and the wake line at the end of Citadel
    // Island's texts; and scene 0's opening for the dream's end (ApplyOpening). Returns lines for the log, the wake line's text, and
    // whether the opening says it (else the race-track mode does, before Zoe's first line).
    public static (List<string> Log, int WakeText, bool InOpening) Apply(string gameDirectory)
    {
        var log = new List<string> { PolarScenes.WriteTexts(gameDirectory) };
        var textPath = Path.Combine(gameDirectory, "TEXT.HQR");
        var text = HqrArchive.Open(textPath);
        var languages = Math.Min(Lba2TextBank.Languages(textPath), PolarScenes.Languages);
        var id = Enumerable.Range(0, languages).Max(l => Lba2TextBank.Load(text, l, CitadelTexts).Texts.Select(t => t.Id).DefaultIfEmpty(0).Max()) + 1;
        var hqr = File.ReadAllBytes(textPath);
        for (var lang = 0; lang < languages; lang++)
        {
            var lines = Lba2TextBank.Load(text, lang, CitadelTexts);
            var attribute = lines.Find(0)?.Attribute ?? Lba2TextBank.NormalAttribute;
            lines.Texts.Add(new Lba2TextBank.Text { Id = id, Attribute = attribute, Bytes = Dos.GetBytes(Wake[lang < Wake.Length ? lang : 0]) });
            hqr = lines.WriteInto(hqr);
        }
        File.WriteAllBytes(textPath, hqr);
        log.Add($"the dream's end: Zoe's wake line is text {id} of Citadel Island's, in all {languages} languages; Twinsen wakes up in scene {WakeScene}");
        var opening = ApplyOpening(gameDirectory, id);
        log.Add(opening.Log);
        return (log, id, opening.Ok);
    }

    // Scene 0's places for the dream's end (its track points from 8 on): Twinsen on his bed (cells 9-11 x 1-4, its head to the north, its
    // top at 3,072: four layers over the floor), facing as he lies on the Wannies' bed in scene 101 (turn 0), his head on the pillow
    // and all of him on the mattress (found by trying places: the animation's root is not its middle). Zoe starts where the game's
    // opening has her, beside the bed's head on its west side (3931, 877); she steps back from it after the wake line (Aside, clear of
    // where he gets out) and comes to him once he is up (Kiss: in front of him, a little under a cell south, he turned to her) -- on that
    // side because her own opening walk afterwards starts south-west, away from him: from his west side it passed him, and walking into
    // him is the game's own kiss, again and again while he stood there.
    public static readonly (int X, int Y, int Z) Bed = (5900, 3072, 2000);
    // (where getting out of bed leaves him: on the floor beside it, at the bed's west side -- the animation lowers him there, his place
    // has to follow -- and turned to where Zoe comes to him)
    private static readonly (int X, int Y, int Z) Up = (4352, 2048, 2518);
    private const int UpBeta = 0;
    public const int BedBeta = 0;
    // Her points are where she heads for: a track's goto_point counts her there 500 short of it (GERETRAK.CPP), so each lies 500 past
    // where she stops -- back to (3250, 1550), south along x 3250 and east along the open floor south of the bed (where the game's
    // opening walks Twinsen in), well clear of him (walking into him she stuck, his box in her way), and to (4300, 3170), in front of
    // him: their boxes not touching (at 600 they do).
    private static readonly (int X, int Y, int Z) Aside = (2894, 2048, 1902);
    private static readonly (int X, int Y, int Z)[] KissWay = { (3250, 2048, 3800), (4398, 2048, 3388), (4756, 2048, 2965) };
    // (the wake line a moment after the scene has faded in from white: RACEMOD.CPP holds it white 0.4 s; the fade stops the clock)
    private const int WakeAfterSeconds = 2;
    // Zoe's animation when Twinsen walks into her at home (the game's own scene 0 script, its label 10): her kiss
    private const int ZoeKiss = 84;
    // Twinsen's animations in scene 101: asleep in the bed, sitting up, getting out of it (the hero's generic animations)
    private const int Asleep = 56, SitUp = 57, GetUp = 58;
    private const int Zoe = 4, OpeningVar = 40;

    // Scene 0's opening, for the dream's end: when DreamVar is 1 (and the opening hasn't run: game variable 40 is 0), Twinsen starts asleep
    // on his bed in place of walking in; Zoe beside it says the wake line (`wake`) at once, DreamVar goes to 2 and she steps back, 3 and he
    // sits up, gets out of bed and turns to her (his track's label 2, where the game's opening has him stop), 4 and she comes round to him
    // and kisses him -- and her own opening goes on from there, as it always does. Returns whether the scene was changed, and a line for the log.
    public static (bool Ok, string Log) ApplyOpening(string gameDirectory, int wake)
    {
        try
        {
            var store = new SceneStore(SceneGame.Lba2, gameDirectory);
            var model = store.Load(WakeScene);
            var first = model.TrackPoints.Count;
            // (POLAR_BED="x y z beta": another place on the bed, for trying it out)
            var (bedX, bedY, bedZ, bedBeta) = (Bed.X, Bed.Y, Bed.Z, BedBeta);
            if (Environment.GetEnvironmentVariable("POLAR_BED")?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray() is [var tx, var ty, var tz, var tb])
                (bedX, bedY, bedZ, bedBeta) = (tx, ty, tz, tb);
            model.TrackPoints.Add(new SceneTrackPoint(bedX, bedY, bedZ));
            model.TrackPoints.Add(new SceneTrackPoint(Up.X, Up.Y, Up.Z));
            model.TrackPoints.Add(new SceneTrackPoint(Aside.X, Aside.Y, Aside.Z));
            foreach (var p in KissWay) model.TrackPoints.Add(new SceneTrackPoint(p.X, p.Y, p.Z));
            int bed = first, up = first + 1, aside = first + 2, kiss = first + 3;
            var scripts = SceneScripts.Load(SceneSerializer.Write(model), WakeScene);

            var heroLife = scripts.GetText(0, ScriptKind.Life);
            heroLife = Once(heroLife, $"if (0 == var_game({OpeningVar}))", $@"if (1 == var_game({DreamVar}) && 0 == var_game({OpeningVar}))
    {{
        set_var_game(94, 1);
        set_var_game(253, 1);
        cinema_mode(1);
        pos_point({bed});
        beta({bedBeta});
        shadow_obj(0, 0);
        set_dir(NO_MOVE);
        anim({Asleep});
        set_track(label_150);
        set_comportement(comportement_3);
    }}
    else if (0 == var_game({OpeningVar}))");
            scripts.SetText(0, ScriptKind.Life, heroLife);
            scripts.SetText(0, ScriptKind.Track, scripts.GetText(0, ScriptKind.Track).TrimEnd() + $@"

label(150);
anim({Asleep});
stop();

label(151);
anim({SitUp});
wait_anim();
anim({GetUp});
wait_anim();
pos_point({up});
anim(0);
angle({UpBeta});

label(2);
stop();
");

            var zoeLife = scripts.GetText(Zoe, ScriptKind.Life);
            zoeLife = Once(zoeLife, "set_track(label_0);", $@"if (1 == var_game({DreamVar}))
                {{
                    set_track(label_160);
                }}
                else
                {{
                    set_track(label_0);
                }}");
            zoeLife = Once(zoeLife, "void comportement_1()\n{\n", $@"void comportement_1()
{{
    if (1 == var_game({DreamVar}) && 161 == l_track())
    {{
        message({wake});
        set_var_game({DreamVar}, 2);
        set_track(label_162);
    }}
    if (2 == var_game({DreamVar}) && 163 == l_track())
    {{
        set_var_game({DreamVar}, 3);
        shadow_obj(0, 1);
        set_track_obj(0, label_151);
    }}
    if (3 == var_game({DreamVar}) && 2 == l_track_obj(0))
    {{
        set_var_game({DreamVar}, 4);
        set_track(label_164);
    }}
");
            scripts.SetText(Zoe, ScriptKind.Life, zoeLife);
            scripts.SetText(Zoe, ScriptKind.Track, scripts.GetText(Zoe, ScriptKind.Track).TrimEnd() + $@"

label(160);
anim(0);
face_twinsen(-1);
wait_nb_second({WakeAfterSeconds});

label(161);
stop();

label(162);
anim(1);
goto_point({aside});
anim(0);
face_twinsen(-1);

label(163);
stop();

label(164);
anim(1);
goto_point({kiss});
goto_point({kiss + 1});
goto_point({kiss + 2});
anim(0);
face_twinsen(-1);
anim({ZoeKiss});
wait_anim();
anim(0);

label(100);
stop();
");
            var built = scripts.Build();
            if (!built.Ok) return (false, $"scene {WakeScene}: the dream's waking up left out, its scripts would not compile: {string.Join("; ", built.Errors)}");
            store.SaveMany(new List<SceneChange> { new(WakeScene, SceneSerializer.Parse(SceneGame.Lba2, built.Record!), null) }, allowErrors: true);
            return (true, $"scene {WakeScene}: won, Twinsen wakes up asleep in his bed (track point {bed}, animation {Asleep}), Zoe beside it wakes him (text {wake}), " +
                          $"steps back (point {aside}), he sits up and gets out of bed ({SitUp}, {GetUp}; point {up}), she comes round to him (points {kiss}-{kiss + 2}) and kisses him " +
                          $"(her animation {ZoeKiss}) before her own opening line");
        }
        catch (Exception e) when (e is InvalidOperationException or ScriptCompileException or InvalidDataException or ArgumentException or IOException)
        {
            return (false, $"scene {WakeScene}: the dream's waking up left out ({e.Message}): Zoe says the wake line before her own, the race-track mode's");
        }
    }

    private static string Once(string text, string what, string with)
    {
        var at = text.IndexOf(what, StringComparison.Ordinal);
        if (at < 0) throw new InvalidOperationException($"scene {WakeScene}'s scripts aren't the game's own (no \"{what.Split('\n')[0]}\")");
        return text[..at] + with + text[(at + what.Length)..];
    }

    internal static Encoding Dos
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(850);
        }
    }
}
