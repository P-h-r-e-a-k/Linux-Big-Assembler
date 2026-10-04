using System.Buffers.Binary;
using System.IO;
using System.Text;
using LBAAssembler.LbaScript;
using LBAAssembler.Scenes;

namespace LBAAssembler.Terrain;

// The race track mod's story, on Citadel Island (the user's StoryNotes.txt, 2026-10-03), told through the game's own storm plot:
//   1. The game opens with Zoe walking up to Twinsen in their house (scene 0, actor 4). She is sick of the rain: find the Weather Wizard
//      and get him to fix it -- her line, and the game's own arrow to his tent (holomap position 21, which his script clears).
//   2. The wizard (scene 21, actor 2) would clear it from the top of the lighthouse, but Raph, its keeper, is too busy at the race track
//      and won't let him in -- in place of the game's "I can't find the keeper", and an arrow to the storm track's start line (222). The
//      game's plot variable 51 goes to 1 as it always did.
//   3. At the start line (the storm track's start scene) stand Raph and Mr. Paul, copies of the game's own (Raph from the Tralu's cave,
//      scene 2; Mr. Paul from his house, scene 7). Raph: beat my time and I'll come to the lighthouse (51 goes to 2, as talking to him
//      in the cave did). Mr. Paul runs the track: no racing gloves, no race -- the race-track mode's gate keeps the start line shut until
//      Twinsen has them (game variable 7: the gloves are found in the attic as before). Raph is in the cave no more (nor is Zoe, who came
//      to fetch Twinsen there after the Tralu was beaten): the race is how he is won over.
//   4. Raph's time is his lap (RaceDriver.CitadelStorm: a driver with no car). A lap under it sets BeatVar (the race-track mode's beat=);
//      Raph says so and goes back to the lighthouse -- the plot's 51 and 56 go to 3, as when he was freed from the Tralu, and the game's
//      own lighthouse scene follows: the wizard waits there, Twinsen joins him, the spell, the storm is over (chapter 2).
//   5. The game then has the aliens land by the tavern (scene 42): Twinsen arrives there in a cutscene that waits for one of them to
//      speak (game variable 70), but the island's actors who played it were taken off the road by the track's build. In their place one
//      alien (a copy of the game's own) stands near where the cutscene puts Twinsen, off the road, and thanks him -- as a thank you
//      they'll build an even better race track, ready tomorrow -- which ends the cutscene. DayVar goes to Tired.
//   6. Tired, everyone Twinsen talks to on the island tells him to go home and have a nap (the race-track mode's tired=). Action at his
//      bed (a zone of its own by it, scene 0) and he sleeps: DayVar Rested, and an arrow to the town circuit's start line (223), which the
//      gate kept shut until now.
//   7. The town circuit races Raph, Zoe, Mr. Paul, the Tralu and the thief (RaceDriver.CitadelTown), three laps. A win (WonVar) is Mr.
//      Paul's prize, a ferry ticket (inventory slot 13), handed over once Twinsen is out of his car.
// The texts are new ones at the end of Citadel's text file, in every language the game has (TEXT.HQR, code page 850 like the game's own).
// At the end, so they have no recorded voice (MESSAGE.CPP Speak: a text past the last sample is shown, not spoken) and every text before
// them keeps its own.
//   - The arrows are holomap positions 222 and 223 (HOLOMAP.HQR entry 12, record 50 + n): positions are scene numbers -- a scene's record
//     is where it lies on the island, used for its arrow and for "Twinsen is here" -- and the game has 222 scenes, so 222-254 are no
//     scene's and no script uses them. Their labels are new texts in the holomap's text file (file 2).
//   - Racing gloves, in the darts' place in Twinsen's attic (scene 1: the darts lie on a shelf, actor 8, and pressing Action there -- zone 5
//     -- gives them). The inventory is 40 fixed slots, all the game's (a save stores exactly 40, and item n's count is game variable n, 40
//     being the Dino-Fly quest), so the gloves take a slot the mod has no use for: 7, the part Baldino gives for Zoe to mend the car -- the
//     mod has the buggy ready from the start. Not the darts' own slot: the darts are sold in the shop and found elsewhere too, and stay.
//     The slot's model (OBJFIX.HQR 7) and its texts (file 2: 7 the found message, 107 the name, 207 the description) become the gloves'; the
//     attic's shelf shows the gloves (a body of its own on the darts' entity) and gives them. Those three texts are spoken by the narrator
//     (EN_GAM.VOX), so each keeps its place in the file under an id nothing asks for -- every text after it keeps its voice -- and the gloves'
//     text goes at the end, where it has none.
internal static class RaceTrackStory
{
    // the holomap: the storm track's start line, the town circuit's, and the game's own arrow to the Weather Wizard's tent
    public const int StartArrow = 222, TownArrow = 223, WizardArrow = 21;
    // the game variables: the gloves (an inventory slot), the ferry ticket's, the story's day (Tired after the aliens' thanks, Rested once
    // Twinsen has slept: the town circuit is open), the town circuit won (1; 2 once the ticket is handed over) and Raph's time beaten
    // (1; 2 once Raph has said so). 200-202 are game variables nothing in the game uses.
    public const int GlovesSlot = 7, FerryTicket = 13;
    public const int DayVar = 200, Tired = 1, Rested = 2, WonVar = 201, BeatVar = 202;
    // Celebration Island's lava lake: the souvenir seller beaten (1; 2 once he has told what he saw) and his race run (won or not), 203-204
    public const int SellerBeaten = 203, SellerRaced = 204;
    public const int RaceLaps = 3;
    // the game's storm plot (51: 1 the wizard spoken to, 2 Raph spoken to, 3 Raph freed, 4 the storm over), the lighthouse keeper's
    // (56: 3 back at the lighthouse), the aliens' landing (70) and the behaviour Twinsen drives in (12)
    private const int StormPlot = 51, KeeperPlot = 56, AliensLanded = 70, Driving = 12;
    private const int OpeningScene = 0, Zoe = 4, BedZone = 5;
    private const int WizardTent = 21, Wizard = 2, TraluCave = 2, CaveRaph = 2, CaveZoe = 9, PaulHouse = 7, PaulActor = 2;
    private const int AliensScene = 42, Alien = 6, AlienGreeting = 388, ArrivalPoint = 20;
    // the gloves: the attic, its shelf actor, and the darts' display entity and its body
    private const int Attic = 1, Shelf = 8, DartsEntity = 18, DartsBody = 31, CarScene = 49;
    private const int CitadelTexts = 3, HolomapTexts = 2;
    private const byte LabelAttribute = 17;          // the holomap labels' own (text 501 "Downtown Pharmacy.")
    private const int ArrowRecordSize = 32;
    private const uint InvisibleNoShadow = 0x1200;

    // The story's lines (Citadel Island's texts), in English, French, German, Spanish, Italian, Portuguese: TEXT.HQR's own order. The game's
    // own names: the Weather Wizard is the Mage Météo, the Wettermagier, the Mago Meteo, the Mago Metereologo, the Mago do Tempo.
    private enum Line { Zoe, Wizard, WizardAfter, Raph, RaphBeaten, PaulGloves, PaulReady, Aliens, Tired, Sleep, PaulTicket }
    private static readonly string[][] Lines =
    {
        new[]
        {
            "Twinsen, I am sick of all this rain! Go and find the Weather Wizard and get him to fix it!",
            "Twinsen, j'en ai assez de toute cette pluie ! Va trouver le Mage Météo et demande-lui d'y remédier !",
            "Twinsen, ich habe diesen ganzen Regen satt! Geh zum Wettermagier und sorg dafür, dass er etwas dagegen tut!",
            "¡Twinsen, estoy harta de tanta lluvia! ¡Ve a buscar al Mago Meteo y haz que lo arregle!",
            "Twinsen, sono stufa di tutta questa pioggia! Va' a cercare il Mago Metereologo e fa' che la faccia smettere!",
            "Twinsen, estou farta de toda esta chuva! Vai procurar o Mago do Tempo e faz com que ele resolva isto!",
        },
        new[]
        {
            "I was going to clear the rain, but I need to do it from the top of the lighthouse, and that rascal Raph is too busy at the race track and refuses to let me.",
            "J'allais chasser la pluie, mais je dois le faire du haut du phare, et ce coquin de Raph est trop occupé sur le circuit et refuse de me laisser monter.",
            "Ich wollte den Regen vertreiben, aber das muss ich von der Spitze des Leuchtturms aus tun, und dieser Schlingel Raph ist auf der Rennstrecke zu beschäftigt und lässt mich nicht hinauf.",
            "Iba a despejar la lluvia, pero tengo que hacerlo desde lo alto del faro, y ese granuja de Raph está muy ocupado en el circuito y no me deja subir.",
            "Stavo per far smettere la pioggia, ma devo farlo dalla cima del faro, e quel furfante di Raph è troppo occupato alla pista e non mi lascia salire.",
            "Eu ia acabar com a chuva, mas tenho de o fazer do alto do farol, e aquele maroto do Raph está muito ocupado na pista e não me deixa subir.",
        },
        new[]
        {
            "Raph is back at the lighthouse? Then let's go, Twinsen! I'll meet you there.",
            "Raph est retourné au phare ? Alors allons-y, Twinsen ! Je te retrouve là-bas.",
            "Raph ist wieder am Leuchtturm? Dann los, Twinsen! Wir treffen uns dort.",
            "¿Raph ha vuelto al faro? ¡Entonces vamos, Twinsen! Nos vemos allí.",
            "Raph è tornato al faro? Allora andiamo, Twinsen! Ci vediamo là.",
            "O Raph voltou ao farol? Então vamos, Twinsen! Encontramo-nos lá.",
        },
        new[]
        {
            "I'm having too much fun, it can wait. I'll tell you what: if you can beat my time on the track, then I'll come with you to the lighthouse.",
            "Je m'amuse trop, ça peut attendre. Je te propose un marché : si tu bats mon temps sur le circuit, je viens avec toi au phare.",
            "Ich habe gerade viel zu viel Spaß, das kann warten. Ich mache dir einen Vorschlag: Wenn du meine Zeit auf der Strecke schlägst, komme ich mit dir zum Leuchtturm.",
            "Me lo estoy pasando demasiado bien, eso puede esperar. Te propongo una cosa: si consigues batir mi tiempo en el circuito, iré contigo al faro.",
            "Mi sto divertendo troppo, può aspettare. Facciamo così: se riesci a battere il mio tempo sulla pista, verrò con te al faro.",
            "Estou a divertir-me demasiado, isso pode esperar. Faço-te uma proposta: se conseguires bater o meu tempo na pista, vou contigo ao farol.",
        },
        new[]
        {
            "You beat my time, Twinsen! A deal is a deal: I'm off to the lighthouse. Bring the Weather Wizard!",
            "Tu as battu mon temps, Twinsen ! Marché conclu : je file au phare. Amène le Mage Météo !",
            "Du hast meine Zeit geschlagen, Twinsen! Abgemacht ist abgemacht: Ich gehe zum Leuchtturm. Bring den Wettermagier mit!",
            "¡Has batido mi tiempo, Twinsen! Lo prometido es deuda: me voy al faro. ¡Trae al Mago Meteo!",
            "Hai battuto il mio tempo, Twinsen! Un patto è un patto: vado al faro. Porta il Mago Metereologo!",
            "Bateste o meu tempo, Twinsen! O prometido é devido: vou para o farol. Traz o Mago do Tempo!",
        },
        new[]
        {
            "Sorry Twinsen, for safety reasons you'll need some racing gloves to take part.",
            "Désolé Twinsen, pour des raisons de sécurité il te faut des gants de course pour participer.",
            "Tut mir leid, Twinsen, aus Sicherheitsgründen brauchst du Rennhandschuhe, um mitzumachen.",
            "Lo siento, Twinsen, por razones de seguridad necesitas unos guantes de carreras para participar.",
            "Mi dispiace, Twinsen, per motivi di sicurezza ti servono dei guanti da corsa per partecipare.",
            "Desculpa, Twinsen, por razões de segurança precisas de luvas de corrida para participar.",
        },
        new[]
        {
            "Racing gloves, very good. The track is yours, Twinsen: see if you can beat Raph's time!",
            "Des gants de course, très bien. Le circuit est à toi, Twinsen : essaie de battre le temps de Raph !",
            "Rennhandschuhe, sehr gut. Die Strecke gehört dir, Twinsen: Versuch, Raphs Zeit zu schlagen!",
            "Guantes de carreras, muy bien. El circuito es tuyo, Twinsen: ¡a ver si bates el tiempo de Raph!",
            "Guanti da corsa, benissimo. La pista è tua, Twinsen: vedi se riesci a battere il tempo di Raph!",
            "Luvas de corrida, muito bem. A pista é tua, Twinsen: vê se consegues bater o tempo do Raph!",
        },
        new[]
        {
            "People from the planet Twinsun, we come to you in a spirit of peace. @ Thank you for clearing the rain which was preventing us from landing. As a thank you, we'll build you an even better race track. It should be ready tomorrow.",
            "Habitants de la planète Twinsun, nous sommes venus animés par un esprit de paix. @ Merci d'avoir chassé la pluie qui nous empêchait d'atterrir. Pour vous remercier, nous allons vous construire un circuit encore meilleur. Il devrait être prêt demain.",
            "Bewohner des Planeten Twinsun, wir sind in friedlicher Absicht hierher gekommen. @ Wir danken Euch, dass Ihr den Regen vertrieben habt, der uns an der Landung hinderte. Zum Dank bauen wir Euch eine noch bessere Rennstrecke. Sie sollte morgen fertig sein.",
            "Habitantes del planeta Twinsun, hemos venido en son de paz. @ Gracias por despejar la lluvia que nos impedía aterrizar. Para agradecéroslo, os construiremos un circuito todavía mejor. Debería estar listo mañana.",
            "Abitanti del pianeta Twinsun, siamo venuti animati da intenzioni pacifiche. @ Vi ringraziamo di aver allontanato la pioggia che ci impediva di atterrare. Per ringraziarvi, vi costruiremo una pista ancora migliore. Dovrebbe essere pronta domani.",
            "Habitantes do planeta Twinsun, viemos tomados por um espírito de paz. @ Obrigado por terem afastado a chuva que nos impedia de pousar. Como agradecimento, vamos construir-vos uma pista ainda melhor. Deverá estar pronta amanhã.",
        },
        new[]
        {
            "You look tired, Twinsen. You should go home and have a nap.",
            "Tu as l'air fatigué, Twinsen. Tu devrais rentrer chez toi faire une sieste.",
            "Du siehst müde aus, Twinsen. Du solltest nach Hause gehen und ein Nickerchen machen.",
            "Pareces cansado, Twinsen. Deberías irte a casa y echarte una siesta.",
            "Sembri stanco, Twinsen. Dovresti andare a casa e farti un pisolino.",
            "Pareces cansado, Twinsen. Devias ir para casa e dormir uma sesta.",
        },
        new[]
        {
            "What a day! A good night's sleep... @ Morning already! The aliens' new race track should be ready by now.",
            "Quelle journée ! Une bonne nuit de sommeil... @ Déjà le matin ! Le nouveau circuit des extraterrestres devrait être prêt.",
            "Was für ein Tag! Eine Mütze voll Schlaf... @ Schon Morgen! Die neue Rennstrecke der Außerirdischen müsste jetzt fertig sein.",
            "¡Qué día! Una buena noche de sueño... @ ¡Ya es de día! El nuevo circuito de los extraterrestres ya debería estar listo.",
            "Che giornata! Una bella dormita... @ È già mattina! La nuova pista degli alieni dovrebbe essere pronta.",
            "Que dia! Uma boa noite de sono... @ Já é de manhã! A nova pista dos extraterrestres já deve estar pronta.",
        },
        new[]
        {
            "Well raced, Twinsen! You've won the prize: a ferry ticket.",
            "Belle course, Twinsen ! Tu as gagné le prix : un ticket de ferry.",
            "Gut gefahren, Twinsen! Du hast den Preis gewonnen: eine Fahrkarte für die Fähre.",
            "¡Buena carrera, Twinsen! Has ganado el premio: un ticket de ferry.",
            "Bella corsa, Twinsen! Hai vinto il premio: un biglietto per il traghetto.",
            "Boa corrida, Twinsen! Ganhaste o prémio: um bilhete de barca.",
        },
    };
    // the holomap labels: the storm track's start line, the town circuit's
    private static readonly string[][] Labels =
    {
        new[] { "Race track start line.", "Ligne de départ du circuit.", "Startlinie der Rennstrecke.", "Línea de salida del circuito.", "Linea di partenza della pista.", "Linha de largada da pista." },
        new[] { "The aliens' new race track.", "Le nouveau circuit des extraterrestres.", "Die neue Rennstrecke der Außerirdischen.", "El nuevo circuito de los extraterrestres.", "La nuova pista degli alieni.", "A nova pista dos extraterrestres." },
    };
    // the gloves' texts: found, name, description
    private static readonly string[][] GlovesTexts =
    {
        new[] { "You have found a pair of racing gloves.", "Racing gloves", "Your racing gloves: a firm grip on the buggy's steering wheel, lap after lap." },
        new[] { "Tu as trouvé une paire de gants de course.", "Gants de course", "Tes gants de course : une bonne prise sur le volant du buggy, tour après tour." },
        new[] { "Du hast ein Paar Rennhandschuhe gefunden.", "Rennhandschuhe", "Deine Rennhandschuhe: fester Griff am Lenkrad des Buggys, Runde für Runde." },
        new[] { "Has encontrado un par de guantes de carreras.", "Guantes de carreras", "Tus guantes de carreras: buen agarre en el volante del buggy, vuelta tras vuelta." },
        new[] { "Hai trovato un paio di guanti da corsa.", "Guanti da corsa", "I tuoi guanti da corsa: una presa salda sul volante del buggy, giro dopo giro." },
        new[] { "Você encontrou um par de luvas de corrida.", "Luvas de corrida", "Suas luvas de corrida: firmeza no volante do buggy, volta após volta." },
    };
    // (where each of those texts' old words stay: ids no script or engine asks for)
    private const int Retired = 60000;

    // A script edit that doesn't find the game's own script as it expects: the edit is left out, and the log says so.
    private sealed class NotTheGames(string message) : Exception(message);

    private static string Once(string text, string what, string with)
    {
        var at = text.IndexOf(what, StringComparison.Ordinal);
        if (at < 0) throw new NotTheGames($"no \"{what}\"");
        return text[..at] + with + text[(at + what.Length)..];
    }

    // The gloves' models and the attic: the shelf shows and gives the gloves instead of the darts; and the car part's one use (letting Zoe
    // mend the car, scene 49) no longer takes them away.
    private static List<string> Gloves(string gameDirectory, SceneStore store)
    {
        var log = new List<string> { RaceTrackGloves.InstallInventory(gameDirectory, GlovesSlot) };
        var (generic, bodyLog) = RaceTrackGloves.InstallInRoom(gameDirectory, DartsEntity, DartsBody);
        log.Add(bodyLog);

        var attic = store.Load(Attic);
        if (attic.Actors.Count <= Shelf || attic.Actors[Shelf].Entity != DartsEntity) { log.Add($"no gloves: scene {Attic}'s shelf isn't the darts'"); return log; }
        attic.Actors[Shelf].Body = generic;
        var scripts = SceneScripts.Load(SceneSerializer.Write(attic), Attic);
        var hero = scripts.GetText(0, ScriptKind.Life);
        var shelf = scripts.GetText(Shelf, ScriptKind.Life);
        if (!hero.Contains($"kill_obj({Shelf});") || !hero.Contains("found_object(2);") || !shelf.Contains("if (3 == var_game(2))"))
        {
            log.Add($"no gloves: scene {Attic}'s scripts aren't the game's own");
            return log;
        }
        // (the one found_object(2) in the attic is the shelf's; its other finds are the holomap's and the magic ball's)
        scripts.SetText(0, ScriptKind.Life, hero.Replace("found_object(2);", $"found_object({GlovesSlot});\n                    set_var_game({GlovesSlot}, 1);"));
        scripts.SetText(Shelf, ScriptKind.Life, shelf.Replace("if (3 == var_game(2))", $"if (0 < var_game({GlovesSlot}))"));
        var built = scripts.Build();
        if (!built.Ok) { log.Add("no gloves: the attic's scripts would not compile: " + string.Join("; ", built.Errors)); return log; }
        var changes = new List<SceneChange> { new(Attic, SceneSerializer.Parse(SceneGame.Lba2, built.Record!), null) };

        var car = store.Load(CarScene);
        var carScripts = SceneScripts.Load(SceneSerializer.Write(car), CarScene);
        var carHero = carScripts.GetText(0, ScriptKind.Life);
        var use = $"1 == use_inventory({GlovesSlot})";
        if (carHero.Contains(use))
        {
            // (use_inventory answers 0 or 1: asking for 2 never comes true, and the script keeps its shape)
            carScripts.SetText(0, ScriptKind.Life, carHero.Replace(use, $"2 == use_inventory({GlovesSlot})"));
            var carBuilt = carScripts.Build();
            if (carBuilt.Ok) changes.Add(new(CarScene, SceneSerializer.Parse(SceneGame.Lba2, carBuilt.Record!), null));
            else log.Add("scene 49: the car part's use could not be switched off: " + string.Join("; ", carBuilt.Errors));
        }
        store.SaveMany(changes, allowErrors: true);
        log.Add($"scene {Attic}: the attic's shelf shows the racing gloves (entity {DartsEntity} body {generic}) and Action there gives them (item {GlovesSlot}); the darts are still in the shop");
        return log;
    }

    private static Encoding Dos
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(850);
        }
    }

    // A holomap arrow (`position`) on a start line, on Citadel Island, labelled with text `label` of the holomap's file, off until a script
    // switches it on.
    private static byte[] Arrow(byte[] arrows, int position, (double X, double Z, double Y, double DirX, double DirZ) line, int label)
    {
        var at = (50 + position) * ArrowRecordSize;
        if (arrows.Length < at + ArrowRecordSize) throw new InvalidDataException("The holomap's arrow table is shorter than the game's own.");
        var citadel = 50 + 49;          // (scene 49's record: Citadel Island's own place on the globe)
        void Put(int field, int value) => BinaryPrimitives.WriteInt32LittleEndian(arrows.AsSpan(at + field * 4), value);
        Put(0, (int)Math.Round(line.X * 512)); Put(1, (int)Math.Round(line.Y)); Put(2, (int)Math.Round(line.Z * 512));
        for (var f = 3; f < 6; f++) Put(f, BinaryPrimitives.ReadInt32LittleEndian(arrows.AsSpan(citadel * ArrowRecordSize + f * 4)));
        Put(6, label);
        arrows[at + 28] = 0xFF;         // no inventory object
        arrows[at + 29] = 0;            // off, never asked
        arrows[at + 30] = 0;            // planet Twinsun
        arrows[at + 31] = 0;            // Citadel Island
        return arrows;
    }

    // How far a place is outside every road of a track -- the lap and its pit lane -- past its verge (cells; less than 0 on one).
    private static double OffRoads(RaceTrackReport report, double x, double z)
    {
        if (report.Roads.Count == 0) return report.DistanceToRoad(x, z) - 5;
        var off = double.MaxValue;
        foreach (var r in report.Roads)
        {
            var nearest = double.MaxValue;
            for (var i = 0; i < r.X.Length; i++) nearest = Math.Min(nearest, (r.X[i] - x) * (r.X[i] - x) + (r.Z[i] - z) * (r.Z[i] - z));
            off = Math.Min(off, Math.Sqrt(nearest) - r.VergeHalf);
        }
        return off;
    }

    // Where a person stands by a start line: `back` cells before it along the lap, off the roads to one side (the first of `sides` with
    // ground there near the road's height, `clear` cells past every road's verge), facing the line. Null when neither side has room.
    private static (double X, double Z, double Y, int Beta)? Beside(RaceTrackReport report, (double X, double Z, double Y, double DirX, double DirZ) s, double back, double clear, int[] sides)
    {
        foreach (var side in sides)
            for (var d = 3.0; d <= 24; d += 0.5)
            {
                double x = s.X - s.DirX * back - s.DirZ * side * d, z = s.Z - s.DirZ * back + s.DirX * side * d;
                if (OffRoads(report, x, z) < clear) continue;
                if (report.GroundAfter?.Invoke(x, z) is not { } g || g < 200 || Math.Abs(g - s.Y) > 500) continue;
                var beta = (int)Math.Round(Math.Atan2(s.X - x, s.Z - z) / (2 * Math.PI) * 4096);
                return (x, z, g, ((beta % 4096) + 4096) % 4096);
            }
        return null;
    }

    // ---- Celebration Island: the souvenir seller -------------------------------------------------------------------------------------
    // The user's (2026-10-03, later): Twinsen has to beat the souvenir seller to get the information he needs. In the game the seller (scene
    // 95, actor 5: the Franco who came back from Island CX, BODY.HQR 307) tells it when Twinsen walks up to him: what he saw there (171),
    // and to "How can I get there?" Rick's gang, at the bar by Otringal's harbour (172, with its holomap arrow, 136; game variable 124 goes
    // to 1). Now, until Twinsen has beaten him, he answers with a challenge instead and takes him to the lava lake's race (its scene, 223:
    // the lava lake is the island before the statue rises, as it is when Twinsen meets him). There he races as the one to beat, three laps.
    // Out of his car after the race, the seller tells him what he saw if he won, or laughs at him if not, and he is back by the dock (scene
    // 95's start); a race lost, the challenge stands.
    private enum Seller { Challenge, Beaten, Rematch }
    private static readonly string[][] SellerLines =
    {
        new[]
        {
            "What I saw over there? That'll cost you more than a statuette, mister. Race me round the lava lake, and if you beat me I'll tell you everything. Come on, the cars are waiting!",
            "Ce que j'ai vu là-bas ? Ça vous coûtera plus qu'une statuette, monsieur. Faites-moi la course autour du lac de lave, et si vous me battez je vous dirai tout. Venez, les voitures attendent !",
            "Was ich dort gesehen habe? Das kostet dich mehr als eine Statuette, mein Freund. Fahr mit mir ein Rennen um den Lavasee, und wenn du mich schlägst, erzähle ich dir alles. Komm, die Wagen warten!",
            "¿Lo que vi allí? Eso te costará más que una estatuilla, amigo. Échame una carrera alrededor del lago de lava, y si me ganas te lo contaré todo. ¡Vamos, los coches esperan!",
            "Quello che ho visto laggiù? Ti costerà più di una statuetta, amico. Sfidami in una corsa intorno al lago di lava, e se mi batti ti racconterò tutto. Vieni, le auto aspettano!",
            "O que eu vi lá? Isso vai custar-te mais do que uma estatueta, amigo. Corre contra mim à volta do lago de lava, e se me venceres conto-te tudo. Anda, os carros estão à espera!",
        },
        new[]
        {
            "You beat me fair and square, mister! A deal's a deal: here's what I saw on Island CX.",
            "Vous m'avez battu à la régulière, monsieur ! Marché conclu : voici ce que j'ai vu sur l'île CX.",
            "Du hast mich ehrlich geschlagen, mein Freund! Abgemacht ist abgemacht: Das habe ich auf der Insel CX gesehen.",
            "¡Me has ganado limpiamente, amigo! Lo prometido es deuda: esto es lo que vi en la isla CX.",
            "Mi hai battuto lealmente, amico! Un patto è un patto: ecco cosa ho visto sull'isola CX.",
            "Venceste-me com justiça, amigo! O prometido é devido: eis o que vi na ilha CX.",
        },
        new[]
        {
            "Ha! Not fast enough, mister. Come and find me at my stall when you want a rematch.",
            "Ha ! Pas assez rapide, monsieur. Revenez me voir à mon étal si vous voulez une revanche.",
            "Ha! Nicht schnell genug, mein Freund. Komm zu meinem Stand, wenn du eine Revanche willst.",
            "¡Ja! No eres lo bastante rápido, amigo. Ven a buscarme a mi puesto si quieres la revancha.",
            "Ah! Non abbastanza veloce, amico. Vieni a trovarmi alla mia bancarella se vuoi la rivincita.",
            "Ah! Não foste rápido que chegue, amigo. Vem ter comigo à minha banca se quiseres a desforra.",
        },
    };
    // (the seller is the game's actor 5 of scene 95, entity 213; a track's build that took the others off the road renumbered him)
    public const int SellerEntity = 213;
    private const int CelebrationScene = 95, CelebrationTexts = 3 + 5;
    private const int Told = 124, RickArrow = 136;

    // `scenes`: what the lava lake's scenes were given (its start scene, 223, is its race's). Returns lines for the log.
    public static List<string> ApplyCelebration(string gameDirectory, RaceTrackScenes.Result scenes)
    {
        var log = new List<string>();
        if (scenes.StartScene < 0) { log.Add("no story: the lava lake's track has no start scene"); return log; }
        // the lines, at the end of Celebration Island's texts
        var textPath = Path.Combine(gameDirectory, "TEXT.HQR");
        var text = HqrArchive.Open(textPath);
        var languages = Lba2TextBank.Languages(textPath);
        var first = Enumerable.Range(0, languages).Max(l => Lba2TextBank.Load(text, l, CelebrationTexts).Texts.Select(t => t.Id).DefaultIfEmpty(0).Max()) + 1;
        int Id(Seller line) => first + (int)line;
        var hqr = File.ReadAllBytes(textPath);
        for (var lang = 0; lang < languages; lang++)
        {
            var words = lang < SellerLines[0].Length ? lang : 0;
            var lines = Lba2TextBank.Load(text, lang, CelebrationTexts);
            var attribute = lines.Find(170)?.Attribute ?? Lba2TextBank.NormalAttribute;
            for (var k = 0; k < SellerLines.Length; k++)
                lines.Texts.Add(new Lba2TextBank.Text { Id = first + k, Attribute = attribute, Bytes = Dos.GetBytes(SellerLines[k][words]) });
            hqr = lines.WriteInto(hqr);
        }
        File.WriteAllBytes(textPath, hqr);
        log.Add($"the souvenir seller's {SellerLines.Length} lines are texts {first}-{first + SellerLines.Length - 1} of Celebration Island's, in all {languages} languages");

        var store = new SceneStore(SceneGame.Lba2, gameDirectory);
        var changes = new List<SceneChange>();
        // the seller: a challenge before he is beaten, what he knows after
        try
        {
            var model = store.Load(CelebrationScene);
            var sellerActor = model.Actors.FindIndex(a => a.Entity == SellerEntity);
            if (sellerActor < 1) throw new NotTheGames($"no souvenir seller (entity {SellerEntity})");
            var scripts = SceneScripts.Load(SceneSerializer.Write(model), CelebrationScene);
            var seller = scripts.GetText(sellerActor, ScriptKind.Life);
            int from = seller.IndexOf($"set_var_game({Told}, 1);", StringComparison.Ordinal), to = seller.IndexOf($"set_holo_pos({RickArrow});", StringComparison.Ordinal);
            if (from < 0 || to < from) throw new NotTheGames("the seller's lines");
            to += $"set_holo_pos({RickArrow});".Length;
            seller = seller[..from] + $@"if (0 == var_game({SellerBeaten}))
            {{
                message(170);
                message_obj(0, 6);
                message({Id(Seller.Challenge)});
                set_var_cube(0, 0);
                change_cube({scenes.StartScene});
            }}
            else
            {{
                set_var_game({Told}, 1);
                message(170);
                message_obj(0, 6);
                message(171);
                message_obj(0, 7);
                message(172);
                set_holo_pos({RickArrow});
            }}" + seller[to..];
            scripts.SetText(sellerActor, ScriptKind.Life, seller);
            var built = scripts.Build();
            if (!built.Ok) throw new NotTheGames(string.Join("; ", built.Errors));
            changes.Add(new(CelebrationScene, SceneSerializer.Parse(SceneGame.Lba2, built.Record!), null));
            log.Add($"scene {CelebrationScene}: the souvenir seller challenges Twinsen to the lava lake's race (scene {scenes.StartScene}) until he is beaten (variable {SellerBeaten})");
        }
        catch (Exception e) when (e is NotTheGames or ScriptCompileException or InvalidDataException or ArgumentException)
        {
            log.Add($"scene {CelebrationScene}: the souvenir seller left as he was: {e.Message}");
            return log;
        }
        // the race's scene: once Twinsen is out of his car after the race, what the seller says, and back to the dock
        try
        {
            var model = store.Load(scenes.StartScene);
            var colour = store.Load(CelebrationScene).Actors.FirstOrDefault(a => a.Entity == SellerEntity)?.CoulObj ?? 4;
            var voice = SceneOps.BlankActor(SceneGame.Lba2, IslandFile.CubeSize / 2, 0, IslandFile.CubeSize / 2, entity: 16);
            voice.Life = new byte[] { 0 }; voice.Track = new byte[] { 0 };
            voice.Flags = InvisibleNoShadow; voice.Body = -1; voice.Armor = 51; voice.LifePoints = -1; voice.CoulObj = colour;
            var index = SceneOps.AddActor(model, voice);
            var scripts = SceneScripts.Load(SceneSerializer.Write(model), scenes.StartScene);
            scripts.SetText(index, ScriptKind.Life, $@"void comportement_0()
{{
    set_comportement(comportement_1);
}}

void comportement_1()
{{
    if (1 == var_game({SellerRaced}) && {Driving} != comportement_hero())
    {{
        set_var_game({SellerRaced}, 0);
        if (1 == var_game({SellerBeaten}))
        {{
            message({Id(Seller.Beaten)});
            message(171);
            message_obj(0, 7);
            message(172);
            set_holo_pos({RickArrow});
            set_var_game({Told}, 1);
            set_var_game({SellerBeaten}, 2);
        }}
        else
        {{
            message({Id(Seller.Rematch)});
        }}
        change_cube({CelebrationScene});
    }}
}}
");
            scripts.SetText(index, ScriptKind.Track, "label(0);\nstop();\n");
            var built = scripts.Build();
            if (!built.Ok) throw new NotTheGames(string.Join("; ", built.Errors));
            changes.Add(new(scenes.StartScene, SceneSerializer.Parse(SceneGame.Lba2, built.Record!), null));
            log.Add($"scene {scenes.StartScene}: after the race the seller tells Twinsen what he saw if he won (variable {SellerBeaten}), or offers a rematch, and Twinsen is back at scene {CelebrationScene}");
        }
        catch (Exception e) when (e is NotTheGames or ScriptCompileException or InvalidDataException or ArgumentException)
        {
            log.Add($"scene {scenes.StartScene}: the race's end left out: {e.Message}");
        }
        store.SaveMany(changes, allowErrors: true);
        return log;
    }

    // Returns lines for the log and what the race-track mode needs (the tired line's text). `storm`: the storm track's build (CITADEL),
    // `town`: the town circuit's (CITABAU); `scenes`, `townScenes`: what the scenes were given for each. Their start lines must have been placed.
    public static (List<string> Log, RaceTrackService.StoryInfo? Info) Apply(string gameDirectory, RaceTrackReport storm, RaceTrackReport town,
        RaceTrackScenes.Result scenes, RaceTrackScenes.Result townScenes)
    {
        var log = new List<string>();
        if (storm.StartLine.Count == 0 || town.StartLine.Count == 0) { log.Add("no story: a track has no start line"); return (log, null); }

        // the texts
        var textPath = Path.Combine(gameDirectory, "TEXT.HQR");
        var text = HqrArchive.Open(textPath);
        var languages = Lba2TextBank.Languages(textPath);
        int NewId(int file) => Enumerable.Range(0, languages).Max(l => Lba2TextBank.Load(text, l, file).Texts.Select(t => t.Id).DefaultIfEmpty(0).Max()) + 1;
        var first = NewId(CitadelTexts); var labelFirst = NewId(HolomapTexts);
        int Id(Line line) => first + (int)line;
        var hqr = File.ReadAllBytes(textPath);
        for (var lang = 0; lang < languages; lang++)
        {
            var words = lang < Lines[0].Length ? lang : 0;       // (a language this doesn't know gets the English)
            var lines = Lba2TextBank.Load(text, lang, CitadelTexts);
            var attribute = lines.Find(0)?.Attribute ?? Lba2TextBank.NormalAttribute;
            for (var k = 0; k < Lines.Length; k++)
                lines.Texts.Add(new Lba2TextBank.Text { Id = first + k, Attribute = attribute, Bytes = Dos.GetBytes(Lines[k][words]) });
            hqr = lines.WriteInto(hqr);
            var labels = Lba2TextBank.Load(text, lang, HolomapTexts);
            for (var k = 0; k < Labels.Length; k++)
                labels.Texts.Add(new Lba2TextBank.Text { Id = labelFirst + k, Attribute = LabelAttribute, Bytes = Dos.GetBytes(Labels[k][words]) });
            // (file 2 is also the inventory's: the gloves' found message, name and description, each moved out of its voiced place)
            var item = new[] { GlovesSlot, 100 + GlovesSlot, 200 + GlovesSlot };
            for (var k = 0; k < item.Length; k++)
            {
                var own = labels.Find(item[k]);
                var itemAttribute = own?.Attribute ?? Lba2TextBank.NormalAttribute;
                if (own is not null) own.Id = Retired + item[k];
                labels.Texts.Add(new Lba2TextBank.Text { Id = item[k], Attribute = itemAttribute, Bytes = Dos.GetBytes(GlovesTexts[words][k]) });
            }
            hqr = labels.WriteInto(hqr);
        }
        File.WriteAllBytes(textPath, hqr);
        log.Add($"the story's {Lines.Length} lines are texts {first}-{first + Lines.Length - 1} of Citadel Island's, the start lines' holomap labels texts {labelFirst}-{labelFirst + 1}, in all {languages} languages (shown, not spoken)");

        // the arrows: the storm track's start line, the town circuit's
        var holoPath = Path.Combine(gameDirectory, RaceTrackHolomap.File);
        var arrows = HqrArchive.Open(holoPath).Read(12);
        arrows = Arrow(arrows, StartArrow, storm.StartLine[0], labelFirst);
        arrows = Arrow(arrows, TownArrow, town.StartLine[0], labelFirst + 1);
        File.WriteAllBytes(holoPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(holoPath), 12, HqrWriter.StoredEntry(arrows)));
        log.Add($"holomap arrows {StartArrow} (the storm track's start line, cell {storm.StartLine[0].X:0.0}, {storm.StartLine[0].Z:0.0}) and {TownArrow} (the town circuit's, {town.StartLine[0].X:0.0}, {town.StartLine[0].Z:0.0})");

        var store = new SceneStore(SceneGame.Lba2, gameDirectory);
        log.AddRange(Gloves(gameDirectory, store));
        // (the people copied are the game's own, from the scenes as they were before any track's build)
        var originals = File.Exists(store.ScenePath + RaceTrackService.BackupSuffix) ? new SceneStore(SceneGame.Lba2, gameDirectory, "SCENE.HQR" + RaceTrackService.BackupSuffix) : store;

        // every scene edited, once each: loaded, then each step's edits of its actors and scripts in turn, saved together at the end
        var models = new Dictionary<int, SceneModel>();
        SceneModel Model(int scene) => models.TryGetValue(scene, out var m) ? m : models[scene] = store.Load(scene);
        // (`edit` changes the scene's actors and zones, then gives what it does to the scripts, which are read with those changes)
        void Edit(int scene, string what, Func<SceneModel, Action<SceneScripts>> edit)
        {
            try
            {
                var model = SceneSerializer.Parse(SceneGame.Lba2, SceneSerializer.Write(Model(scene)));
                var scriptEdit = edit(model);
                var scripts = SceneScripts.Load(SceneSerializer.Write(model), scene);
                scriptEdit(scripts);
                var built = scripts.Build();
                if (!built.Ok) { log.Add($"scene {scene}: {what}: the scripts would not compile: {string.Join("; ", built.Errors)}"); return; }
                models[scene] = SceneSerializer.Parse(SceneGame.Lba2, built.Record!);
                log.Add($"scene {scene}: {what}");
            }
            catch (Exception e) when (e is NotTheGames or ScriptCompileException or InvalidDataException or ArgumentException)
            {
                log.Add($"scene {scene}: {what} left out: {(e is NotTheGames ? "the script isn't the game's own: " : "")}{e.Message}");
            }
        }
        // a person (or anyone) added to a scene, with a life script of its own (set once the scripts are read)
        Action<SceneScripts> Add(SceneModel model, SceneActorModel actor, string life)
        {
            actor.Life = new byte[] { 0 }; actor.Track = new byte[] { 0 };
            var index = SceneOps.AddActor(model, actor);
            return scripts =>
            {
                scripts.SetText(index, ScriptKind.Life, life);
                scripts.SetText(index, ScriptKind.Track, "label(0);\nstop();\n");
            };
        }

        // 1. Zoe sends Twinsen to the Weather Wizard; and his bed, where he sleeps once the aliens have thanked him
        Edit(OpeningScene, "Zoe sends Twinsen to the Weather Wizard (his tent's arrow); Action at Twinsen's bed sleeps the night once he is tired", model =>
        {
            // the bed: a zone of its own round it (the hero starts the game at its foot), and Action there
            model.Zones.Add(new SceneZoneModel { Type = 2, Num = BedZone, Info = new[] { 0, 0, 0, 0, 0, 0, 0, 1 }, X0 = 7168, Y0 = 2048, Z0 = 3072, X1 = 10751, Y1 = 3327, Z1 = 5631 });
            return scripts =>
        {
            var zoe = scripts.GetText(Zoe, ScriptKind.Life);
            if (!zoe.Contains("message(0);") || !zoe.Contains("set_holo_pos(22);")) throw new NotTheGames("Zoe's opening");
            // (only the opening's arrow: her later reminder -- "did you find something to cure the Dino-Fly?" -- keeps pointing at the pharmacy)
            var said = zoe.IndexOf("message(0);", StringComparison.Ordinal);
            var arrow = zoe.IndexOf("set_holo_pos(22);", said, StringComparison.Ordinal);
            zoe = zoe[..arrow] + $"set_holo_pos({WizardArrow});" + zoe[(arrow + "set_holo_pos(22);".Length)..];
            scripts.SetText(Zoe, ScriptKind.Life, zoe.Replace("message(0);", $"message({Id(Line.Zoe)});"));
            var hero = scripts.GetText(0, ScriptKind.Life);
            hero = Once(hero, "case 4:", $@"case {BedZone}:
                    if ({Tired} == var_game({DayVar}))
                    {{
                        message({Id(Line.Sleep)});
                        set_var_game({DayVar}, {Rested});
                        set_holo_pos({TownArrow});
                    }}
                    break;
                case 4:");
            scripts.SetText(0, ScriptKind.Life, hero);
        };
        });

        // 2. Raph isn't held in the Tralu's cave (nor does Zoe come to fetch Twinsen there): he is at the race track
        Edit(TraluCave, "Raph and Zoe are no longer in the Tralu's cave (Raph is at the race track)", _ => scripts =>
        {
            scripts.SetText(CaveRaph, ScriptKind.Life, Once(scripts.GetText(CaveRaph, ScriptKind.Life), $"if (1 < var_game({KeeperPlot}))", $"if (0 <= var_game({KeeperPlot}))"));
            scripts.SetText(CaveZoe, ScriptKind.Life, Once(scripts.GetText(CaveZoe, ScriptKind.Life), $"if (2 < var_game({StormPlot}))", $"if (0 <= var_game({StormPlot}))"));
        });

        // 3. the Weather Wizard: Raph won't let him up the lighthouse; the arrow to the start line
        Edit(WizardTent, "the Weather Wizard tells of Raph at the race track (the start line's arrow), and once Raph is back at the lighthouse to meet him there", _ => scripts =>
        {
            var wizard = scripts.GetText(Wizard, ScriptKind.Life);
            wizard = Once(wizard, "message(26);", $@"if (3 > var_game({StormPlot}))
                {{
                    message({Id(Line.Wizard)});
                    set_holo_pos({StartArrow});
                }}
                else
                {{
                    message({Id(Line.WizardAfter)});
                }}");
            // (the game's next line was Twinsen's: he had seen the keeper held in the cave)
            wizard = Once(wizard, "message_obj(0, 306);", $"set_var_game({StormPlot}, 2);");
            scripts.SetText(Wizard, ScriptKind.Life, wizard);
        });

        // 4. Raph and Mr. Paul at the storm track's start line, in the storm
        var raph = originals.Load(TraluCave).Actors.ElementAtOrDefault(CaveRaph)?.Clone();
        var paul = originals.Load(PaulHouse).Actors.ElementAtOrDefault(PaulActor)?.Clone();
        var start = storm.StartLine[0];
        if (scenes.StartScene >= 0 && raph is not null && paul is not null)
            Edit(scenes.StartScene, "Raph and Mr. Paul stand by the storm track's start line", model =>
            {
                var sides = new[] { 1, -1 };
                if (Beside(storm, start, 1, 1, sides) is not { } p || Beside(storm, start, 3, 1, sides) is not { } r) throw new NotTheGames("no room beside the start line");
                void Put(SceneActorModel actor, (double X, double Z, double Y, int Beta) at)
                {
                    actor.X = (int)Math.Round((at.X - model.CubeX * 64) * 512); actor.Z = (int)Math.Round((at.Z - model.CubeY * 64) * 512);
                    actor.Y = (int)Math.Round(at.Y); actor.Beta = at.Beta;
                }
                Put(raph, r); Put(paul, p);
                var raphScript = Add(model, raph, $@"void comportement_0()
{{
    if (1 < chapter() || 2 < var_game({KeeperPlot}))
    {{
        suicide();
    }}
    else
    {{
        set_comportement(comportement_1);
    }}
}}

void comportement_1()
{{
    if (1 == var_game({BeatVar}))
    {{
        message({Id(Line.RaphBeaten)});
        set_var_game({StormPlot}, 3);
        set_var_game({KeeperPlot}, 3);
        set_var_game({BeatVar}, 2);
        suicide();
    }}
    swif (1 == action())
    {{
        if (1500 > distance(0) && {Driving} != comportement_hero())
        {{
            message({Id(Line.Raph)});
            if (2 > var_game({StormPlot}))
            {{
                set_var_game({StormPlot}, 2);
            }}
        }}
    }}
}}
");
                var paulScript = Add(model, paul, $@"void comportement_0()
{{
    if (1 < chapter())
    {{
        suicide();
    }}
    else
    {{
        set_comportement(comportement_1);
    }}
}}

void comportement_1()
{{
    swif (1 == action())
    {{
        if (1500 > distance(0) && {Driving} != comportement_hero())
        {{
            if (0 == var_game({GlovesSlot}))
            {{
                message({Id(Line.PaulGloves)});
            }}
            else
            {{
                message({Id(Line.PaulReady)});
            }}
        }}
    }}
}}
");
                return scripts => { raphScript(scripts); paulScript(scripts); };
            });
        else log.Add("Raph and Mr. Paul left out: no start scene, or the game's own Raph (scene 2) or Mr. Paul (scene 7) not found");

        // 5. the aliens' thanks, where Twinsen arrives after the spell: the game's landing cutscene there waits for an alien to speak
        var alien = originals.Load(AliensScene).Actors.ElementAtOrDefault(Alien)?.Clone();
        if (alien is not null)
            Edit(AliensScene, "an alien thanks Twinsen where he arrives after the spell, which ends the game's landing cutscene, and Twinsen is tired", model =>
            {
                // off the road (the fine weather's: the town circuit's) beside where the cutscene puts Twinsen, on its ground
                if (ArrivalPoint >= model.TrackPoints.Count) throw new NotTheGames($"no track point {ArrivalPoint}");
                var arrival = model.TrackPoints[ArrivalPoint];
                var from = (model.CubeX * 64 + arrival.X / 512.0, model.CubeY * 64 + arrival.Z / 512.0, (double)arrival.Y, 0.0, 1.0);
                if (Beside(town, from, -1, 1, new[] { 1, -1 }) is not { } a) throw new NotTheGames("no room by Twinsen's arrival");
                alien.X = (int)Math.Round((a.X - model.CubeX * 64) * 512); alien.Z = (int)Math.Round((a.Z - model.CubeY * 64) * 512);
                alien.Y = (int)Math.Round(a.Y); alien.Beta = a.Beta;
                var alienScript = Add(model, alien, $@"void comportement_0()
{{
    if (2 != chapter())
    {{
        suicide();
    }}
    else
    {{
        set_comportement(comportement_1);
    }}
}}

void comportement_1()
{{
    if (0 == var_game({AliensLanded}))
    {{
        set_var_game({AliensLanded}, 1);
        message({Id(Line.Aliens)});
        set_var_game({DayVar}, {Tired});
    }}
    swif (1 == action())
    {{
        if (1500 > distance(0))
        {{
            message({AlienGreeting});
        }}
    }}
}}
");
                return scripts =>
                {
                    // (the cutscene: Twinsen at point 20, waiting for the landing's variable)
                    if (!scripts.GetText(0, ScriptKind.Life).Contains($"if (1 < chapter() && 0 == var_game({AliensLanded}))")) throw new NotTheGames("the landing cutscene");
                    alienScript(scripts);
                };
            });

        // 6. the town circuit won: Mr. Paul's prize, once Twinsen is out of his car -- in whichever of the island's outside scenes that is
        var paulColour = paul?.CoulObj ?? 4;
        for (var scene = RaceTrackIsland.Citadel.FirstScene; scene <= RaceTrackIsland.Citadel.LastScene; scene++)
        {
            if (!store.SceneExists(scene)) continue;
            Edit(scene, "Mr. Paul hands over his prize, a ferry ticket, after a win on the town circuit", model =>
            {
                var prize = SceneOps.BlankActor(SceneGame.Lba2, IslandFile.CubeSize / 2, 0, IslandFile.CubeSize / 2, entity: 16);
                prize.Life = new byte[] { 0 }; prize.Track = new byte[] { 0 };
                prize.Flags = InvisibleNoShadow; prize.Body = -1; prize.Armor = 51; prize.LifePoints = -1; prize.CoulObj = paulColour;
                return Add(model, prize, $@"void comportement_0()
{{
    set_comportement(comportement_1);
}}

void comportement_1()
{{
    if (1 == var_game({WonVar}) && {Driving} != comportement_hero())
    {{
        message({Id(Line.PaulTicket)});
        found_object({FerryTicket});
        set_var_game({FerryTicket}, 1);
        set_var_game({WonVar}, 2);
    }}
}}
");
            });
        }

        store.SaveMany(models.Select(m => new SceneChange(m.Key, m.Value, null)).ToList(), allowErrors: true);
        log.Add($"the race-track mode: Mr. Paul's gloves gate the storm track (variable {GlovesSlot}), Raph's time beaten sets {BeatVar}; the town circuit opens once {DayVar} is {Rested}, " +
                $"{RaceLaps} laps, a win sets {WonVar}; while {DayVar} is {Tired} everyone says text {Id(Line.Tired)}");
        return (log, new RaceTrackService.StoryInfo(Id(Line.Tired), TownArrow));
    }
}
