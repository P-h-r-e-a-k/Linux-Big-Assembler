namespace LBAAssembler.Terrain;

// Who races on a track: each driver a car of the racer's entity (its body: 0 the retail track's racer, 1 Baldino's rocket car, 2 and on
// the cars made after the game's characters, RaceTrackCharacterCars) or the motorbike Rabbibunny (Bike), on a racing line of its own
// (RaceTrackBuilder.PlanRacePath: the side of the road it leans to, its top speed and pull, its cornering). A Ghost has a line and no car:
// its lap at its skill is the time to beat (the car file's beat=; Raph's on Citadel Island's storm track).
//
// Every track has one Main opponent, the one Twinsen has to beat: he drives at the car setup's skill -- beatable, a little challenge --
// and the race's result is whether Twinsen finished ahead of him. The others' skills are drawn at random each race (RaceCarEngineFile).
//
// The rosters are the user's (CharactersForTrack.txt, 2026-10-03): an island with none races the retail track's racer, Baldino and the biker.
//
// A driver may drive one of the cast's cars (Cast: its number in RACECARS.JSON, RaceTrackCharacterCars.CastFirst on), a body of an entity
// of its own -- a copy of the racer's, its animations the same -- rather than the racer entity's: Body is then its body in that entity, and
// the build puts the cars in the scenes as that entity (RaceTrackScenes, from the folder's RACECARS.JSON). (2026-10-17: the Temple of Bú's
// Halloween monsters.)
internal sealed record RaceDriver(string Name, int Body, RaceTrackBuilder.RacingLine Line, bool Main = false, bool Bike = false, bool Ghost = false, int Cast = -1)
{
    public const int RacerBody = 0;

    // A car that flies (2026-10-18: the Temple of Bú's witch on her broomstick, no wheels): how far it bobs up and down over its line (units;
    // 0 none), once every HoverMs -- drawn only (RACEMOD.CPP opponent_hover=): its line, its box and what meets it are any car's.
    public int Hover { get; init; }
    public const int HoverMs = RaceTrackCharacterCars.WitchBobMs;

    // the racer, Baldino and the biker: every track's before the rosters, and still any track's with none (the racer the one to beat)
    public static readonly RaceDriver Racer = new("The racer", RacerBody, RaceTrackBuilder.RacerLine, Main: true);
    public static readonly RaceDriver Baldino = new("Baldino", RaceTrackBaldinoCar.Generic, RaceTrackBuilder.BaldinoLine);
    public static readonly RaceDriver Biker = new(RaceCarEngineFile.BikerName, -1, RaceTrackBuilder.BikerLine, Bike: true);

    private static RaceDriver Of(RaceTrackCharacterCars.Car car, string name, double side, double top, double grip, bool main = false) =>
        new(name, car.Generic, new RaceTrackBuilder.RacingLine(side, 4, top, grip), main);

    // ... and in a car of the cast's, by its number (its body in its own entity: a hundred to an entity, InstallCast)
    private static RaceDriver OfCast(int number, string name, double side, double top, double grip, bool main = false) =>
        new(name, (number - RaceTrackCharacterCars.CastFirst) % RaceTrackCharacterCars.CastPerEntity, new RaceTrackBuilder.RacingLine(side, 4, top, grip), main, Cast: number);

    // Citadel Island, the storm track: Raph, the lighthouse keeper, too busy racing to let the weather wizard up the lighthouse -- "beat my
    // time" (StoryNotes.txt). His time is his lap at the setup's skill.
    public static readonly List<RaceDriver> CitadelStorm = new()
    {
        Of(RaceTrackCharacterCars.Raph, "Raph", -1.5, 1.0, 1.0, main: true) with { Ghost = true },
    };

    // Citadel Island, the town circuit (the aliens' "even better race track", the day after the storm): Raph, Zoe, Mr. Paul (whose prize
    // is a ferry ticket: the one to beat), the Tralu and the thief
    public static readonly List<RaceDriver> CitadelTown = new()
    {
        Of(RaceTrackCharacterCars.Raph, "Raph", -1.5, 1.0, 1.0),
        Of(RaceTrackCharacterCars.Zoe, "Zoe", 1.5, 0.99, 1.03),
        Of(RaceTrackCharacterCars.Paul, "Mr. Paul", 2.5, 0.98, 0.97, main: true),
        Of(RaceTrackCharacterCars.Tralu, "The Tralu", 0, 1.03, 0.93),
        Of(RaceTrackCharacterCars.Thief, "The thief", -2.5, 1.02, 0.99),
    };

    // The Desert island: Moya, the Dino-Fly, the Dean of the School of Magic, the retail track's racer (its champion: the one to beat) and
    // Baldino
    public static readonly List<RaceDriver> Desert = new()
    {
        Of(RaceTrackCharacterCars.Moya, "Moya", 2.5, 0.95, 1.08),
        Of(RaceTrackCharacterCars.DinoFly, "The Dino-Fly", 0, 1.04, 0.94),
        Of(RaceTrackCharacterCars.Dean, "The Dean", -1.5, 0.99, 1.0),
        Racer,
        Baldino,
    };

    // The Emerald Moon: Baldino in his space suit, in his lander (the one to beat), and HAL, the moon base's computer, driving itself
    public static readonly List<RaceDriver> Emerald = new()
    {
        Of(RaceTrackCharacterCars.Lander, "Baldino", 0, 1.02, 0.95, main: true),
        Of(RaceTrackCharacterCars.Hal, "HAL", 2.0, 1.0, 1.01),
    };

    // Mosquibees Island: the Queen (the one to beat), and the monkey monster with the sword in his war cart
    public static readonly List<RaceDriver> Mosquibe = new()
    {
        Of(RaceTrackCharacterCars.Queen, "The Queen", -2.5, 1.01, 1.0, main: true),
        Of(RaceTrackCharacterCars.WarCart, "The monkey monster", 2.5, 0.98, 1.03),
    };

    // Celebration Island's lava lake: a kangaroo, the souvenir seller (who tells what he saw on Island CX only to a driver who beats him)
    // and a policeman, one of the island's guards
    public static readonly List<RaceDriver> CelebrationLava = new()
    {
        Of(RaceTrackCharacterCars.Kangaroo, "The kangaroo", -2.5, 1.03, 0.95),
        Of(RaceTrackCharacterCars.Seller, "The souvenir seller", 0, 1.0, 1.0, main: true),
        Of(RaceTrackCharacterCars.Guard, "The policeman", 2.5, 1.01, 0.98),
    };

    // Polar Island's dream race (2026-10-06): Twinsen dreams he is racing FunFrock to Sendell, from the dock to the top of the rocky peak --
    // FunFrock in his car, the one to beat, a shade quicker on the straights than Twinsen's car would be and a shade slower in the bends
    public static readonly List<RaceDriver> Polar = new()
    {
        Of(RaceTrackCharacterCars.FunFrock, "FunFrock", 1.5, 1.01, 0.97, main: true),
    };

    // The Island of the Francos (2026-10-07): its own -- De La Fontaine in the refinery's gazogem tanker (the one to beat), Mr. Kurtz in his
    // laboratory and the Franco nurse with her pram
    public static readonly List<RaceDriver> Knartas = new()
    {
        Of(RaceTrackCharacterCars.Tanker, "De La Fontaine", 0, 1.0, 1.0, main: true),
        Of(RaceTrackCharacterCars.Laboratory, "Mr. Kurtz", -2.0, 0.99, 1.02),
        Of(RaceTrackCharacterCars.Pram, "The nurse", 2.0, 1.02, 0.97),
    };

    // Volcano Island (2026-10-08): the island under Celebration's own -- the Mosquibee dissident in his car (the one to beat), the ferryman
    // in his boat and a Mosquibee of the hide-out on a slab of lava
    public static readonly List<RaceDriver> Volcano = new()
    {
        Of(RaceTrackCharacterCars.Rebel, "The dissident", 0, 1.0, 1.0, main: true),
        Of(RaceTrackCharacterCars.Ferry, "The ferryman", -2.0, 0.99, 1.02),
        Of(RaceTrackCharacterCars.LavaSlab, "The hide-out's Mosquibee", 2.0, 1.02, 0.97),
    };

    // Wannies Island (2026-10-14): the Wannie miner in his undergas bulldozer (the one to beat), a Wannie in a mine cart and the old Wannie
    // on her firefly tart
    public static readonly List<RaceDriver> Wannies = new()
    {
        Of(RaceTrackCharacterCars.Bulldozer, "The Wannie miner", 0, 1.0, 1.0, main: true),
        Of(RaceTrackCharacterCars.Wannie, "The mine cart", -2.0, 0.99, 1.02),
        Of(RaceTrackCharacterCars.Tart, "The old Wannie", 2.0, 1.02, 0.97),
    };

    // Island CX (2026-10-15): its own people's cars -- one of the Emperor's soldiers in his rocket launcher (the one to beat), a Franco
    // trooper's tank and the Franco survivor on his raft
    public static readonly List<RaceDriver> CxTower = new()
    {
        Of(RaceTrackCharacterCars.Launcher, "The Emperor's soldier", 0, 1.0, 1.0, main: true),
        Of(RaceTrackCharacterCars.Tank, "The Franco trooper", -1.8, 0.99, 1.02),
        Of(RaceTrackCharacterCars.Survivor, "The survivor", 1.8, 1.02, 0.97),
    };

    // The Temple of Bú's Halloween race: Halloween's own monsters in cars made for them (2026-10-18, the user: "Let's also create different
    // opponents for Twinsen, a vampire driving a coffin, A witch flying on a broom (so no wheels), A zombie driving a hurst") -- the vampire
    // in his coffin (the one to beat), the witch on her broomstick (flying: it bobs over its line, Hover) and the zombie in his hearse
    // (RaceTrackCharacterCars.Monsters), on the lines, the places on the grid and with the characters the three before them had: the
    // skeleton in his ghost car, the ghost in hers and the two-headed monster in his monster truck, the cast's cars (2026-10-17; the Dark
    // Monk, Ker'aooc and Tabata before them), which stay in the catalogue, anyone's to drive.
    public static readonly List<RaceDriver> Temple = new()
    {
        Of(RaceTrackCharacterCars.Coffin, "The vampire", 0, 1.0, 1.0, main: true),
        Of(RaceTrackCharacterCars.Broomstick, "The witch", -1.2, 0.99, 1.02) with { Hover = RaceTrackCharacterCars.WitchBob },
        Of(RaceTrackCharacterCars.Hearse, "The zombie", 1.2, 1.02, 0.97),
    };

    // The Emperor's palace (2026-10-12): racing out of the palace ahead of the Dark Monk to his teleporter -- Stan in his time machine (the
    // one to beat), the pighead with the broom in his road sweeper (CharactersForTrack.txt) and the Emperor himself, fleeing his own palace,
    // a little to either side of the road's middle: the rooms' doors are two bricks wide. (The user's list has the palace's two-headed
    // monster too: its car is one of the cast's, bodies of entities of their own, which an opponent couldn't be then -- it can since
    // 2026-10-17, RaceDriver.Cast, and raced on the Temple of Bú until 2026-10-18; the palace's line-up is as it was.)
    public static readonly List<RaceDriver> Palace = new()
    {
        Of(RaceTrackCharacterCars.Stan, "Stan", 0, 1.0, 1.0, main: true),
        Of(RaceTrackCharacterCars.Sweeper, "The road sweeper", -1.2, 0.99, 1.02),
        Of(RaceTrackCharacterCars.Emperor, "The Emperor", 1.2, 1.02, 0.97),
    };
}
