namespace LBAAssembler.Terrain;

// Which island a race track is built on, and what differs between islands: the files it changes, the scenes it edits, and the ground
// textures and colours the road is painted with (every island has its own texture page and palette, so the Desert track's own tile
// coordinates draw something else on another island -- RaceTrackTextures copies the road's tiles into the island's spare texture space
// and gives back where they landed).
internal sealed record RaceTrackIsland(
    string Name,                    // as the dialog and the log call it
    string IleFile,                 // the island's ground
    string OblFile,                 // its decor bodies (the bridge deck's are added here)
    int IslandByte,                 // what a scene's own island byte says (SceneModel.Island)
    int FirstScene, int LastScene,  // its outside scenes (the demo copies, 190 and up, are left alone)
    int PaletteEntry,               // RESS.HQR: the palette its ground is drawn with
    string PlanResource,            // the route built into the program
    (int X, int Z)? OldTrackCube)   // a retail race track to clear first (the Desert island's own)
{
    // The Desert island: the retail race track's own island, and the first track built. Its lap jumps over itself.
    public static readonly RaceTrackIsland Desert = new("Desert island", "DESERT.ILE", "DESERT.OBL", 2, 55, 73, 29,
        "RaceTrackPlan.Desert.json", (7, 10))
    {
        Crossing = CrossingStyle.Jump, Roster = RaceDriver.Desert,
        // (the island's people and animals away from the road stay -- the camel: the user, 2026-10-27, "Let's make sure with Desert island
        // we're not removing any actors or zones we don't need the same as we checked with citadel")
        KeepOffRoadActors = true,
        // (the School of Magic's gates open from the start -- the game unlocks them later, game variable 65, which also shows the open gates'
        // decor: the user, 2026-10-27, "The gates to the school of magic are initially closed on Desert island and get unlocked later in the
        // game, let's just start the game with them open")
        HeldVars = new[] { (65, 1) },
        // (the School of Magic's canyon: its rim back up where the levelling cut it down beside the road -- in only through the gate, as
        // the game has it: the user, 2026-10-27, "Let's raise the terrain slightly around the school of magic to force the user to access
        // it via the gate, but let's make sure we don't change the near track terrain height")
        OwnRimsAt = new[] { (535, 590, 571, 630) },
        // (its water crossing a deck over the water, the ferry's channel open under it: RaceTrackBuilder.WaterDeck)
        WaterDeck = true,
    };

    // Citadel Island: two tracks, one for each of its files, no retail track to clear, and no buggy in its scenes (the build puts one at each
    // start line). The storm's file, CITADEL, carries a short lap round the town -- along the west rampart and off its end over a jump,
    // round the harbour and back up through a double hairpin -- with no opponents yet. Once the storm is over (chapter 2, after the
    // lighthouse; in Play, the car setup's fine weather) the engine draws the island from CITABAU instead -- the same ground with its own
    // light, palette and decor bodies -- and that carries the town circuit, with its opponents and the story. The two share the island's
    // scenes (RaceTrackScenes applies both), so the island is built with both whichever one is chosen: the two entries, the town circuit
    // and the storm track, differ only in which one Play races (RacesTwin) -- and with it the weather it plays in -- when the editor has
    // neither file open (the race track window lists the island once, as this entry). The town circuit crosses itself on a bridge.
    public static readonly RaceTrackIsland Citadel = new("Citadel Island", "CITADEL.ILE", "CITADEL.OBL", 0, 42, 50, 27,
        "RaceTrackPlan.CitadelStorm.json", null)
    {
        TwinIleFile = "CITABAU.ILE", TwinOblFile = "CITABAU.OBL", TwinPlanResource = "RaceTrackPlan.Citadel.json", RacesTwin = true,
        Title = "Citadel Island: the town circuit (once the storm is over)", Roster = RaceDriver.CitadelStorm, TwinRoster = RaceDriver.CitadelTown,
        // (the storm track's car, scene 42, when it is fine -- its start line is under CITABAU's ground -- in the yard north of Twinsen's
        // house, flat at 250 in both files; the town circuit's, scene 49, in the storm -- its start line is CITADEL's sea -- under the
        // carport, where the island's own car stands: that one, a decor that can't be driven, goes from both files)
        ParkOwn = (540.5, 637.5, 1024, 1), ParkTwin = (544.125, 654.75, 1024, 0),
        DropDecors = new[] { (8, 10, 16448, 7552) }, OwnWeather = 0,
        // (the lighthouse, scene 46, where the storm ends: the game's own Raph by its door and Twinsen and Zoe on its top, whom the Weather
        // Wizard's spell needs -- RaceTrackStory -- and the door's people and track points on the storm file's ground: the town circuit, in
        // the fine weather's file, cut the hill there by 2,000. Raph's fiancee went with the others, 2026-10-14 -- the user: "an NPC on the
        // track behind the lighthouse that we need to remove": she wanders between five places behind it, on the town circuit, and runs
        // after Twinsen until the keeper is back -- after the car; no script of the scene's refers to her)
        StoryEntities = new[] { RaceTrackStory.LighthouseRaph, RaceTrackStory.CoupleOnTop },
        OwnGroundAt = new[] { (46, 481.5, 661.8, 10.0) },
        // (the museum (scene 48, its building's top taken for the storm track's deck): walls built up from what is left of it to the deck, and
        // a lift on the deck where its upper window was, down into the museum where the window took Twinsen -- scene 15, at the window's own
        // place: the user, 2026-10-27)
        DeckWalls = new[] { (518.7, 568.5, 523.1, 572.6, 2618) },
        // (no support for the storm track's deck in the walk into the port beside the big wall, its other side open park: the user,
        // 2026-10-27, "This post blocking access to the port ... it's a track support, but let's just remove the post")
        // ... and in the alley between the curved wall and the lattice-walled shop south of it, at cell (518.9, 614.0) (the user, 2026-10-27,
        // "the post on citadel.ile is still ... there")
        NoPiersAt = new[] { (519.0, 582.0, 3.0), (518.9, 614.0, 4.0) },
        // (Twinsen's car in the story where he left it, first under the carport at his house -- not on a start line: the user, 2026-10-27)
        CarHome = (544.125, 654.75, 1024),
        // (its steep banks beside the road its own ground, walked on by Twinsen, walls to the cars only: RaceTrackBuilder.WallSteepBanks)
        SoftBanks = true,
        DeckLifts = new[] { (520.8, 568.1, 15, 12288, 3000, 13312) },
        // (and in the town circuit's file, where the road took the museum: its front against the road's bank, the retail front (body 38)
        // moved there, and a lift on the road above -- the user, 2026-10-27, "Let's put the museum on the wall where Twinsen is currently
        // facing": scene 48 at (10966, 31375), facing west)
        TwinMuseum = (38, 520.5, 570.5, 525.5, 572.4, 521.5, 573.3),
        // (the island's people and things away from both roads stay: the cow, the sewer lifts by the start line and over the school -- without
        // them the lift's zone took Twinsen down into the sewers and the ride back up took him straight down again -- the user, 2026-10-27)
        KeepOffRoadActors = true,
    };

    public static readonly RaceTrackIsland CitadelStorm = Citadel with
    {
        Name = "Citadel Island, storm track", RacesTwin = false, Title = "Citadel Island: the storm track (in the rain)",
    };

    // Mosquibees Island: a mountain lap from a drawing -- two loops winding up round the Mosquibees' mountain from the shore, a bridge
    // from its top over the first loop and the channel to the plateau's ridge, a jump over the plateau's west bay, the start line on its
    // north edge, and the long way down its east side. The plan carries its own heights, bridge and jump (RaceTrackPlan.Heights). Its
    // outside scenes are 102, 103 and 105; 104 between them is the Queen's throne, an inside scene.
    public static readonly RaceTrackIsland Mosquibe = new("Mosquibees Island", "MOSQUIBE.ILE", "MOSQUIBE.OBL", 7, 102, 105, 34,
        "RaceTrackPlan.Mosquibe.json", null)
    {
        Roster = RaceDriver.Mosquibe,
    };

    // Celebration Island with the statue (CELEBRA2: the file the engine draws once the statue has risen, game variable 79; before that
    // it is CELEBRAT, the same island with its lava lake empty): from the dock where the taxi lands, round the island and up round the
    // statue in a spiral to its shoulders, a ring round its head, and a bridge straight back down to the dock. All of it but the dock is
    // a raised road on piers (RaceTrackPlan.Raised), which only the engine's race-track mode can drive; that mode draws the island with
    // the statue whatever the game variable says (Statue). One outside scene, 95, and one cube.
    public static readonly RaceTrackIsland Celebration = new("Celebration Island", "CELEBRA2.ILE", "CELEBRA2.OBL", 5, 95, 95, 32,
        "RaceTrackPlan.Celebration.json", null)
    {
        Statue = true, Title = "Celebration Island: round the statue", StoryEntities = new[] { RaceTrackStory.SellerEntity },
    };

    // Celebration Island before the statue rises (CELEBRAT: the mesa's top a plateau round a lake of lava level with it, the temple on its
    // west edge, lava channels pouring off its north side): a figure of eight on the plateau, a loop over the north (a causeway over the
    // north channel's mouth) and one over the south, joined by two causeways that cross in the middle of the lake -- and both jump there, their
    // leaps crossing over one hole of lava (RaceTrackPlan.GapJumps). The island's one outside scene, 95, carries the statue's track, so
    // this one races in a scene of its own: 223, a copy of 95 the build adds (CopiesScene), which the race-track mode draws without the
    // statue (the car file's statue=0). Its jumps' flights are numbered as an other file's (OtherFile), clear of the statue's track's.
    public static readonly RaceTrackIsland CelebrationLava = new("Celebration Island, lava lake", "CELEBRAT.ILE", "CELEBRAT.OBL", 5, 223, 223, 32,
        "RaceTrackPlan.CelebrationLava.json", null)
    {
        OtherFile = true, CopiesScene = 95, Title = "Celebration Island: the lava lake", Roster = RaceDriver.CelebrationLava,
    };

    // The Elevator Platform (ASCENCE: the foot of the elevator from Otringal, a few decks on legs over the sea with the elevator's tower
    // in the middle and an airship moored beside it -- 28 cells by 24, all of it decor, the ground under it nothing but sea). Too small
    // for a road on it, so the lap is built upwards, a rollercoaster: all of it a raised road (RaceTrackPlan.Raised, the whole lap),
    // banked in its bends (Bank), from a station in front of the elevator's door up a helix round the tower, down a drop, over hills and
    // the airship and back. Only the engine's race-track mode can drive it, and there the grade changes the car's speed (Gravity).
    // One outside scene, 120, and one cube.
    public static readonly RaceTrackIsland Elevator = new("Elevator Platform", "ASCENCE.ILE", "ASCENCE.OBL", 10, 120, 120, 37,
        "RaceTrackPlan.Elevator.json", null)
    {
        Title = "The Elevator Platform: the rollercoaster",
    };

    // Sendell's Well (island 1, cut from the game: no SENDELL.ILE ships and no scene is on it): the build makes the island first --
    // one round cube, a beach, grassy slopes up to a plateau and a paved rim round the well -- and a scene for it, 224 (SendellWell).
    // The lap runs round the well, up from the beach onto the plateau and back down.
    public static readonly RaceTrackIsland Sendell = new("Sendell's Well", SendellWell.IleFile, SendellWell.OblFile, SendellWell.IslandByte,
        SendellWell.Scene, SendellWell.Scene, SendellWell.PaletteEntry, "RaceTrackPlan.Sendell.json", null)
    {
        Created = true, Title = "Sendell's Well: the cut island, made by the build",
    };

    // What carries the lap over itself where its plan doesn't draw that itself (RaceTrackPlan.Planned: Mosquibees Island's, Celebration
    // Island's, the Elevator Platform's and Citadel Island's storm track draw their own): the Desert island's jumps, Citadel Island's town
    // circuit (its twin's plan) has a bridge.
    public CrossingStyle Crossing { get; init; } = CrossingStyle.Bridge;

    // Who races there (RaceDriver: the user's line-up for the track), and on the twin's track (Citadel Island's town circuit). Null: the
    // retail track's racer, Baldino and the biker.
    public List<RaceDriver>? Roster { get; init; }
    // The race's levels, easy, medium and hard (RaceDifficulty: the Temple of Bú's, 2026-10-17); null: none -- the race as it is, whatever
    // the car setup's difficulty.
    public RaceLevel[]? Levels { get; init; }
    // The game's own people the story needs, by entity, kept in the island's scenes when the build takes the others off the road (the
    // souvenir seller on Celebration Island: RaceTrackStory.ApplyCelebration).
    public int[]? StoryEntities { get; init; }
    // The entities whose actors every build keeps, story or not (and moves off the road): the island's own story outside a race
    public int[]? KeptEntities { get; init; }
    // The build keeps every actor that is off the road -- it stands clear of it, and its scripts put it nowhere on it (RaceTrackScenes
    // OffRoad) -- taking only those on it away; without, every actor goes but the story's and the cutscenes'
    public bool KeepOffRoadActors { get; init; }
    // Places (island cells, x0 z0 x1 z1) where the island's own ground comes back up to its height after the road's levelling, clear of the
    // road: rims that kept people to a gate (RaceTrackBuilder)
    public (int X0, int Z0, int X1, int Z1)[]? OwnRimsAt { get; init; }
    // The lap's longest water crossing a raised deck over the water, humped for a boat to pass under (RaceTrackBuilder.WaterDeck)
    public bool WaterDeck { get; init; }
    // Walls built up under the raised road where it passes over a building cut down for it: island cells x0 z0 x1 z1 and the height they
    // stand on (the building's top), up to the deck's underside (RaceTrackBuilder)
    public (double X0, double Z0, double X1, double Z1, int BaseY)[]? DeckWalls { get; init; }
    // Places a raised road's pier never stands (island cells, a reach round each): a walkway the build's own rules (a lane between
    // buildings, a door) don't see (RaceTrackBuilder.PlacePiers)
    public (double X, double Z, double Reach)[]? NoPiersAt { get; init; }
    // Lifts on the raised road down into a building: island cell, and the scene and place there they take Twinsen to (a door zone on the deck)
    public (double X, double Z, int Scene, int ToX, int ToY, int ToZ)[]? DeckLifts { get; init; }
    // A building's front put back in the other weather's file (RaceTrackBuilder.PlaceTwinMuseum): the retail decor of Body nearest FromX, FromZ
    // moved to ToX, ToZ (island cells; its door zone and way out moved with it, RaceTrackScenes), and a lift on the road at LiftX, LiftZ
    // into the building as DeckLifts' first
    public (int Body, double FromX, double FromZ, double ToX, double ToZ, double LiftX, double LiftZ)? TwinMuseum { get; init; }
    // The entity of the one the island's story has waiting at the start, whatever the build's story setting: brought from the scene the
    // island has him in to stand just behind Twinsen on the grid (Polar Island's grobo with the digger's key, 2026-10-27)
    public int? StartPerson { get; init; }
    // The one to beat of the island's story's start, standing beside his car on pole before he gets in (RACEMOD.CPP chase_car=): the
    // game's actor brought from its scene, and its entity
    public (int Scene, int Actor, int Entity)? ChaseDriver { get; init; }
    // Where Twinsen's car first stands in the story (island cell, turn): its scene's car made there the first time, and every start scene's
    // car where he left it -- on the start line only when the race file starts the game there (RACEMOD.CPP RACE_GRID_CAR_VAR)
    public (double X, double Z, int Beta)? CarHome { get; init; }
    // The steep banks the shaping leaves beside the road kept as the island's own ground, walls to the cars only (RaceTrackBuilder)
    public bool SoftBanks { get; init; }
    // For an island with a track in each weather: places whose scene's people and track points belong to the own file's story (the storm's,
    // gone once it is over), each the scene, the island cell and a reach in cells -- the build seats them on the own file's reshaped ground,
    // not on the ground of whichever file's road is nearer (RaceTrackScenes.Reseat).
    public (int Scene, double X, double Z, double Reach)[]? OwnGroundAt { get; init; }
    public List<RaceDriver>? TwinRoster { get; init; }

    // The island's other file for other weather (EXTFUNC.CPP loads it instead once the storm is over), built with the same track -- or,
    // with TwinPlanResource, with a track of its own (the route built into the program for it).
    public string? TwinIleFile { get; init; }
    public string? TwinOblFile { get; init; }
    public string? TwinPlanResource { get; init; }
    // For an island with a track in each file: whether Play races the twin's (Citadel Island's town circuit, in fine weather) or the
    // island's own file's (its storm track, in the rain).
    public bool RacesTwin { get; init; }
    // As the race track window lists it (the name, unless the island has two entries).
    public string? Title { get; init; }
    // The track is in the file the engine draws only while game variable 79 is set (Celebration Island's statue): the race-track mode
    // is told to draw that one (the car file's statue=1).
    public bool Statue { get; init; }
    // The file is the island's other one, raced on its own (Celebration Island's CELEBRAT, beside the statue's CELEBRA2): the race-track
    // mode is told to draw it (statue=0), and its jumps' flights are numbered as an other file's (RaceTrackJumpAnim.GenericFor).
    public bool OtherFile { get; init; }
    // The scene its track races in is not in the game: the build adds it, a copy of this one (CelebrationLava's 223, of 95), as
    // FirstScene (RaceTrackScenes.AddScene).
    public int? CopiesScene { get; init; }
    public string Shown => Title ?? Name;
    // How many tracks a build of the island makes: Citadel Island's two files each carry one (its storm track and its town circuit).
    public int Tracks => TwinPlanResource is not null ? 2 : 1;
    // As a list of the tracks built names it.
    public string Built => TwinPlanResource is not null ? $"{Name.Split(',')[0]} (the storm track and the town circuit)" : Name;

    // The Emerald Moon's older copy (MOON.ILE and MOON.OBL, 14 February 1997): the same moon as EMERAUDE.ILE, island 3, its heights all
    // but the same and its texturing different, which no version of the game loads -- free ground for a test track. The race-track mode
    // draws island 3 from it (the car file's island_file=: RaceFile), in scenes of its own, 225-228, copies of the Emerald Moon's four
    // outside scenes 74-77 (CopiesScenes). Its lap runs round the moon base on the crater's floor, through two vertical loops (the plan's
    // Loops: RACEMOD.CPP has the cars driven round them, upside down over the top), one with a gap at its top that a car has to leap. The
    // opponents go round them too (since 2026-10-03). Nothing is drawn on the holomap (island 3's picture is the Emerald Moon's).
    public static readonly RaceTrackIsland Moon = new("The old moon", "MOON.ILE", "MOON.OBL", 3, 225, 228, 30, "RaceTrackPlan.Moon.json", null)
    {
        CopiesScenes = (74, 4), RaceFile = "moon", NoHolomap = true,
        Title = "The old moon (MOON.ILE): two vertical loops",
    };

    // The Emerald Moon itself (EMERAUDE.ILE, island 3), from the user's sketch: all of it a raised road -- along the middle of the moon
    // base's long arm on its roof (the straight, its pit lane beside it: a strip of road of its own, clear space between them, since
    // 2026-10-12 -- RaceTrackPlan.RaisedStrips), up onto the crater's rim and round it through three
    // vertical loops (the plan's Loops: the deck there twice the road's width, the car going in on one half and coming out on the other),
    // and one jump over the whole reactor on the north rim (ArcJumps: the race-track mode carries the car up a curved ramp, over the
    // reactor's dish and down a curved hill, from one cube into the next), the road as wide as the dish there and banked hard through the
    // turns either side. It races in scenes of its own, 229-232, copies of the moon's four outside scenes 74-77 (CopiesScenes): the
    // story's scenes put Twinsen in his space suit, out of his car. The opponents race it too (since 2026-10-03: round the loops and over
    // the reactor), waiting in the pit lane while the player qualifies. Nothing is drawn on the holomap.
    public static readonly RaceTrackIsland Emerald = new("The Emerald Moon", "EMERAUDE.ILE", "EMERAUDE.OBL", 3, 229, 232, 30, "RaceTrackPlan.Emerald.json", null)
    {
        CopiesScenes = (74, 4), NoHolomap = true, Roster = RaceDriver.Emerald,
        Title = "The Emerald Moon: over the reactor, round the base",
    };

    // Polar Island (LBA1's at twice its size, made into LBA2's island 12: Terrain/Polar, POLAR.ILE and POLAR.OBL, scenes 233-253) and the
    // dream that picks up from the end of the first game: Twinsen dreams he is racing FunFrock to Sendell. From the dock down the arm and up
    // the island, round a hairpin at its top, back down its east side, over a jump across the main straight, up the terraces under the
    // rocky peak along LBA1's car tracks, round the peak on a raised road and a carried jump into it -- a sprint, not a lap (the plan's
    // Open: a start line and a finish line, the jump's lip), in Twinsen's car at 120 km/h at the most (Dream.TopKmh: the setup's gears,
    // scaled). A win wakes him up in mid-flight (Dream.WakeFlight): Zoe shakes him awake at home, in the second game's first scene. A loss
    // runs the race again. The island is added by the build when the folder hasn't got it (Polar.PolarIsland, from the LBA1 folder in the
    // settings), and stays when the track is put back.
    public static readonly RaceTrackIsland Polar = new("Polar Island", LBAAssembler.Terrain.Polar.PolarTerrain.IleFile, LBAAssembler.Terrain.Polar.PolarTerrain.OblFile,
        LBAAssembler.Terrain.Polar.PolarIsland.IslandByte, LBAAssembler.Terrain.Polar.PolarScenes.FirstScene,
        LBAAssembler.Terrain.Polar.PolarScenes.FirstScene + LBAAssembler.Terrain.Polar.PolarScenes.Count - 1, LBAAssembler.Terrain.Polar.PolarIsland.PaletteEntry, "RaceTrackPlan.Polar.json", null)
    {
        Roster = RaceDriver.Polar, Dream = true, Title = "Polar Island: the dream race to Sendell (LBA1's island, added when missing)",
        // (the grobo with the digger's key, on the dock: Twinsen's car outside a race asks for it -- Polar.PolarGrobo)
        StartPerson = LBAAssembler.Terrain.Polar.PolarGrobo.Entity,
        // (FunFrock himself beside his car on pole, talking to Twinsen before he gets in: the game's own, scene 95's actor 14)
        ChaseDriver = (95, 14, 217),
    };

    // A sprint dreamt at the start of the game (Polar Island's): won, Twinsen wakes up at home (RaceTrackService.DreamInfo).
    public bool Dream { get; init; }

    // An island with a track in each weather (Citadel Island): where a start scene's car parks when the other weather's file is shown --
    // the start line's ground in one file can be the sea, or under the ground, in the other (2026-10-06: the town circuit's car stood in
    // the sea in the storm, where the dream's race wakes Twinsen up, and couldn't be driven). Island cells (in the start scene's own
    // cube) and a turn, for the own file's start and the twin's, and the value of game variable 206 the engine sets for the weather the
    // car parks in (RACEMOD.CPP RaceMod_CitadelWeather: 1 fine, 0 the storm).
    public (double X, double Z, int Beta, int When)? ParkOwn { get; init; }
    // The value of game variable 206 while the island's own file is shown (the twin's is the other: 0 and 1) -- Citadel Island's storm, 0.
    // Each file's jump acts only in its own weather (RaceTrackScenes.AddJump).
    public int? OwnWeather { get; init; }
    public (double X, double Z, int Beta, int When)? ParkTwin { get; init; }

    // Decors of the island's own that every build takes away, wherever the road is: each the cube and the origin (cube units) of the
    // pieces placed there (RaceTrackBuilder.DropDecors) -- Citadel Island's car under Twinsen's carport, a model that can't be driven,
    // where the build parks the real one (2026-10-06: the user found two cars by the house, the island's and the build's).
    public (int CubeX, int CubeZ, int X, int Z)[]? DropDecors { get; init; }

    // The island isn't in the game: the build makes its files and its scene (Sendell's Well). They have no originals to keep, so the
    // build's backups leave them out (KeptFiles); it makes them afresh every time, and putting the folder back deletes them.
    public bool Created { get; init; }
    // Scenes the track races in that the game hasn't got: copies of `Count` original scenes from `First` on, numbered from FirstScene
    // (RaceTrackScenes.AddScene; CopiesScene is one).
    public (int First, int Count)? CopiesScenes { get; init; }
    // The file the race-track mode draws the island from instead of its own (the car file's island_file=): an island file no version of
    // the game loads (MOON).
    public string? RaceFile { get; init; }
    // No opponents' cars in its scenes; nothing drawn on the holomap.
    public bool NoOpponents { get; init; }
    public bool NoHolomap { get; init; }
    // An island made bigger for its track (IslandScaler): Scale times each way, everything on it with it, its new cubes' scenes numbered
    // from MoreScenes (past 254: no holomap arrow -- the engine's CubeArrowFlags).
    public int Scale { get; init; } = 1;
    // Every car on Gazogem fuel all race, and no mushroom gives it (the race-track mode's fuel_always=): the Island of the Francos', where
    // the fuel is made (the user, 2026-10-07).
    public bool FuelAlways { get; init; }
    // Game variables the race-track mode holds at a value while it runs (the car file's vargame=): travel cutscenes kept from playing --
    // Otringal's square island's Dino-Fly landing, which holds Twinsen at its pad while variable 83 is under 2 (2026-10-08).
    public (int Var, int Value)[] HeldVars { get; init; } = Array.Empty<(int, int)>();
    // Shuttles the race-track mode flies (the car file's shuttle=): a scene's actor (its index and entity in the game's scene) and the pad
    // on the road it comes down onto (cube-local x and z) -- Otringal's spaceship on the square island hovers over the road, lands on it a
    // while and rises straight up again, over and over (the user, 2026-10-08).
    public (int Scene, int Actor, int Entity, int X, int Z)[] Shuttles { get; init; } = Array.Empty<(int, int, int, int, int)>();
    public int[] MoreScenes { get; init; } = Array.Empty<int>();
    // Cubes of the island's ground the game has no scene for, that the lap drives over: a scene the build makes for each (IslandScaler
    // .AddCubeScenes), numbered Scene, from a copy of Template (its actors inert stand-ins, cube changes to and from its neighbours) --
    // Otringal's south-west islets, cube (6,9) (2026-10-08).
    public (int CubeX, int CubeZ, int Scene, int Template)[] NewCubes { get; init; } = Array.Empty<(int, int, int, int)>();
    // The island's outside scenes: FirstScene..LastScene, MoreScenes when it is made bigger, and its new cubes'.
    public IEnumerable<int> Scenes => Enumerable.Range(FirstScene, LastScene - FirstScene + 1).Concat(Scale > 1 ? MoreScenes : Array.Empty<int>()).Concat(NewCubes.Select(c => c.Scene));
    public bool HasScene(int scene) => scene >= FirstScene && scene <= LastScene || Scale > 1 && MoreScenes.Contains(scene) || NewCubes.Any(c => c.Scene == scene);
    // The scenes the build adds, as (original, copy) pairs.
    public IEnumerable<(int From, int To)> AddedScenes =>
        CopiesScenes is { } many ? Enumerable.Range(0, many.Count).Select(k => (many.First + k, FirstScene + k))
        : CopiesScene is { } one ? new[] { (one, FirstScene) } : Enumerable.Empty<(int, int)>();

    // The Island of the Francos (KNARTAS.ILE, the game's island 8, scenes 107-109): its dock, its gazogem refinery and its village of furry
    // dome huts, made twice its size each way for its track (Scale: IslandScaler -- 12 cubes, scenes 255-263 besides) and raced on a raised
    // road that passes over itself, in three looks (the user, 2026-10-07: "drop all the vertical loops ... more height ... more loops, but
    // not vertical ones ... 4 times its original size ... 3 distinctly themed sections"): the dock's -- the start up the dock's east arm, a
    // loop out over the sea that climbs over its own way in, a jump over the inlet's mouth and the island's rocket, the south arm; the
    // refinery's -- a loop over its south fence up onto the high road at 14,000, past its tanks and machine houses under pipes that puff
    // steam and drip oil, the factory's steam blowing out of the road, and a leap over the cracking tower down into the village; the
    // village's -- through its huts half way up their domes, a loop over the hills, the south shore, a leap past the refinery's fence and
    // the climb to the top road over the rocks; then down the channel between the dock and the refinery onto the start.
    // The plan: tools/RaceTrackPlan/knartas_design.py.
    public static readonly RaceTrackIsland Knartas = new("Island of the Francos", "KNARTAS.ILE", "KNARTAS.OBL", 8, 107, 109, 35, "RaceTrackPlan.Knartas.json", null)
    {
        Roster = RaceDriver.Knartas,
        Title = "The Island of the Francos: over the dock, the refinery and the village",
        // (twice its size each way, its 3 cubes 12: scenes 107-109 and 255-263 -- the user, 2026-10-07)
        Scale = 2,
        MoreScenes = Enumerable.Range(255, 9).ToArray(),
        FuelAlways = true,
    };

    // Otringal (OTRINGAL.ILE, the game's island 4, scenes 87-92), from the user's sketch (2026-10-08): one raised road round the island,
    // close over its ground (version 2, the same day) -- round the square island south-east of the town past its spaceships' pads, down the
    // game's own slope into the building at its foot and through the bridge's tube (the game's tunnel) and on under the gatehouse, climbing
    // into the town, up through it onto the straight along the palace's east side (the start line, the pit lane on its left between the
    // race lanes and the palace), west along the island's north edge, south down its west side and a jump over the inlet, an S round the
    // town's tanks, down through the west pier's yard over the sea to the south-west islets (cube (6,9): the game has its ground but no scene
    // there -- NewCubes, scene 264), along the south coast and a jump over the sea back onto the square island. The plan:
    // tools/RaceTrackPlan/otringal_design.py.
    public static readonly RaceTrackIsland Otringal = new("Otringal", "OTRINGAL.ILE", "OTRINGAL.OBL", 4, 87, 92, 31, "RaceTrackPlan.Otringal.json", null)
    {
        Title = "Otringal: through the game's tunnel, through the town and round the palace",
        NewCubes = new[] { (6, 9, 264, 87) },
        // (Twinsen has arrived on Otringal: the square island's Dino-Fly landing doesn't play when the car flies into it off the jump)
        HeldVars = new[] { (83, 3) },
        // (its spaceships: scene 92's actor 2, on its pad on the road along the square island's north side -- its script's own pos_point(1)
        // -- and actor 3, parked on the other pad, beside the road along its east side: the ground-level road runs into it, so it flies too)
        Shuttles = new[] { (92, 2, 47, 19200, 9472), (92, 3, 47, 19104, 17632) },
    };

    // Volcano Island (SOUSCELB.ILE, the game's island 11 under Celebration Island, scenes 130-132; cube (7,8) isn't there), from the user's
    // sketch (2026-10-08), in layers since 2026-10-11: the start and the pit lane (its boost panel) on the sand plateau's north straight;
    // a climb twice round a circle over the plateau, each time past a lava fall down a crag's face (RaceTrackLavaFall), its second turn over
    // its first; a double spiral down, a circle over the main lava channel with a lava geyser up its open middle, and a slope over the
    // channel (both loops circles since 2026-10-11); between the channels, a jump onto the east edge (a
    // crystal arch, the crystal field in the lava: RaceTrackCrystals), a jump down to the south shore -- both landing as high as they leave --
    // along it and up the volcano's flank; along the plateau's south edge and its west side, under the crag's lava carried over the road on an
    // aqueduct. The game's lava balls out of lava geysers' showers, the aqueduct, the fall's pool; red crystal shards out of the volcano
    // (LavaBalls, LavaGeysers, CrystalShards). The plan: tools/RaceTrackPlan/volcano_design.py.
    public static readonly RaceTrackIsland Volcano = new("Volcano Island", "SOUSCELB.ILE", "SOUSCELB.OBL", 11, 130, 132, 38, "RaceTrackPlan.Volcano.json", null)
    {
        Title = "Volcano Island: over the lava", Roster = RaceDriver.Volcano,
    };

    // Wannies Island (PLATFORM.ILE, island 6: outside scenes 96-99), from the user's drawing (2026-10-14): from the start straight east of
    // the elevator's pier (its pit lane down the pier: a branch of the raised road) round the east island and the west one (or a shortcut
    // off the east island's corner over a boost pad and a high leap over the west one's loop: another branch), climbing all the way, high
    // over the islands, to a jump into a giant frog's mouth (timed: it opens and shuts; a shut one throws the car back -- RaceTrackFrog),
    // through it and out of its back, up over the lava to the elevator tower's top and twice round it down a helix to the start. The plan:
    // tools/RaceTrackPlan/wannies_design.py.
    public static readonly RaceTrackIsland Wannies = new("Wannies Island", "PLATFORM.ILE", "PLATFORM.OBL", 6, 96, 99, 33, "RaceTrackPlan.Wannies.json", null)
    {
        Title = "Wannies Island: through the frog and down the tower", Roster = RaceDriver.Wannies,
        // (Twinsen has come down in the elevator: scene 98's hero script plays the elevator's cutscene -- and puts him at its point 7, on
        // foot -- while it's 0, and above 1 the cinema of his way back up)
        // (and the frog's still copy among the decor -- the editor's -- hidden: the race-track mode draws the frog, RaceTrackFrog.AddDecor)
        HeldVars = new[] { (107, 1), (RaceTrackFrog.DecorVar, 1) },
        // (the pier's east post, body 33: the pit lane runs over the pier there, low since the road came down -- 2026-10-15)
        DropDecors = new[] { (8, 7, 19712, 17664) },
    };

    // The Emperor's palace (the user, 2026-10-12: "through the palace rooms, coming out the window and around the roof tops? Point-to-point
    // i.e. no laps, finishing with massive jump to harbor teleport? - Twinsen, quick, Dark monk's in the palace, he's killing everybody.
    // Leading into final showdown at Celebration"), on the Palace island (Terrain/Palace: PALACE.ILE and PALACE.OBL, made by the build --
    // Created: PalaceTrack.Install -- with its scenes from 265, a cube of the route each), raced as Otringal's island 4 drawn from the palace's
    // file (RaceFile). A sprint through the palace, two storeys high since 2026-10-15 (its outside dropped -- docs/racetrack/
    // palace_outside_archive -- and an upstairs added: PalaceUpstairs): from the sixteen rooms' south-east room west along the bottom row,
    // up the ramp round the stairwell's column, through the library, the music room, the bedrooms and the dressing room into the throne
    // room, down the other stairwell's ramp, through the long hall and up to its round window: the finish, the leap out of it -- won, the
    // Dark Monk's teleporter takes the car in mid-air and Twinsen arrives on Celebration Island (PalaceTrack's lines and the race-track
    // mode's wake=). The plan: tools/RaceTrackPlan/palace_design.py.
    public static readonly RaceTrackIsland Palace = new("The Emperor's palace", LBAAssembler.Terrain.Palace.PalaceIsland.IleFile, LBAAssembler.Terrain.Palace.PalaceIsland.OblFile,
        LBAAssembler.Terrain.Palace.PalaceTrack.IslandByte, LBAAssembler.Terrain.Palace.PalaceTrack.FirstScene, LBAAssembler.Terrain.Palace.PalaceTrack.LastScene,
        LBAAssembler.Terrain.Palace.PalaceIsland.PaletteEntry, "RaceTrackPlan.Palace.json", null)
    {
        Created = true, RaceFile = "palace", NoHolomap = true, Roster = RaceDriver.Palace, Sprint = true, OwnScenes = true, RailHeld = true,
        Title = "The Emperor's palace: through its rooms, upstairs past the throne, out of the round window to the Dark Monk's teleporter",
    };

    // Island CX (the user, 2026-10-15: "a loop around the tower, the track enters the underground section through a slope down through one of
    // the doors ... loops around the underground section a few times then back out a door on the other side ... a pit lane ... the cannons
    // that normally fire at Twinsen outside randomly firing at any outside car"; then: "In the underground section there's some gas monsters
    // that attack the player let's build on this theme"), on CXTOWER.ILE -- Island CX with the control tower's lower level under its gas,
    // made by the build as one ring of a hall round the control room (Created: ControlTower.CxTowerTrack.Install) -- raced as island 9 drawn
    // from it (RaceFile) in a copy of Island CX's outside scene, 300 (CopiesScene). Round the island under its corner towers' cannons, which
    // fire at whichever car outside they like (HuntingGuns); down the south-west trench and through its door, down under the gas and twice
    // round the ring past its gas monsters, up and out of the north-east trench's door. The plan: tools/RaceTrackPlan/cxtower_design.py.
    public static readonly RaceTrackIsland CxTower = new("Island CX", LBAAssembler.Terrain.ControlTower.ControlTowerIsland.IleFile,
        LBAAssembler.Terrain.ControlTower.ControlTowerIsland.OblFile, LBAAssembler.Terrain.ControlTower.CxTowerTrack.IslandByte,
        LBAAssembler.Terrain.ControlTower.CxTowerTrack.Scene, LBAAssembler.Terrain.ControlTower.CxTowerTrack.Scene,
        LBAAssembler.Terrain.ControlTower.ControlTowerIsland.PaletteEntry, LBAAssembler.Terrain.ControlTower.CxTowerTrack.PlanResource, null)
    {
        Created = true, RaceFile = "cxtower", CopiesScene = LBAAssembler.Terrain.ControlTower.CxTowerTrack.SourceScene, NoHolomap = true, RailHeld = true,
        Roster = RaceDriver.CxTower,
        HuntingGuns = (LBAAssembler.Terrain.ControlTower.CxTowerTrack.CannonEntity, LBAAssembler.Terrain.ControlTower.CxTowerTrack.CannonReach),
        Title = "Island CX: round the island under its cannons, down through a door and twice round the halls under the gas",
    };

    // The Temple of Bú (the user, 2026-10-16: "Port the temple of Bu from LBA1 into LBA2 as a separate island ... generate a Halloween inspired
    // race track for it. This will be a separate release for a Halloween special track"), LBA1's three temple scenes ported as an island of
    // their own (Terrain/Temple: TEMPLE.ILE and TEMPLE.OBL, made by the build from the LBA1 folder in the settings -- Created; LBA1's colours
    // in a palette of its own, RESS 40, under a night sky, RESS 24: Look), raced as Otringal's island 4 drawn from the temple's file (RaceFile),
    // in scenes of its own from 301, a cube of the route each (TempleTrack). A lap round the temple on a raised road a little over its floors,
    // through doorways cut where it passes the walls: west along the long corridor, through the first floor's south side and its lower hall,
    // out over its open south edge and down a spiral through the dark to the crypt, through the crypt's rooms -- the start on the straight
    // through its west room -- and up a spiral to the corridor. The plan: tools/RaceTrackPlan/temple_design.py.
    public static readonly RaceTrackIsland Temple = new("The Temple of Bú", LBAAssembler.Terrain.Temple.TempleIsland.IleFile, LBAAssembler.Terrain.Temple.TempleIsland.OblFile,
        LBAAssembler.Terrain.Temple.TempleTrack.IslandByte, LBAAssembler.Terrain.Temple.TempleTrack.FirstScene, LBAAssembler.Terrain.Temple.TempleTrack.LastScene,
        LBAAssembler.Terrain.Temple.TempleIsland.PaletteEntry, LBAAssembler.Terrain.Temple.TempleTrack.PlanResource, null)
    {
        Created = true, RaceFile = "temple", Look = (LBAAssembler.Terrain.Temple.TempleIsland.PaletteEntry, LBAAssembler.Terrain.Temple.TempleIsland.SkyEntry),
        NoHolomap = true, RailHeld = true, OwnScenes = true, FromLba1 = true, Roster = RaceDriver.Temple, Levels = RaceDifficulty.Temple, RaceLaps = 5,
        FlagMarshal = true,
        Title = "The Temple of Bú: the Halloween race through LBA1's temple, down to its crypt and back (LBA1's scenes, made by the build)",
        // (the giant web's still copy among the decor -- the editor's -- hidden: the race-track mode draws the web, TempleHalloween.PlaceGiantWebs)
        HeldVars = new[] { (LBAAssembler.Terrain.Temple.TempleHalloween.GiantWebVar, 1) },
    };

    // An island drawn in a palette and under a sky of its own (RESS.HQR entries: the car file's island_palette=, island_sky=) -- the Temple of
    // Bú's, LBA1's colours.
    public (int Palette, int Sky)? Look { get; init; }
    // The build makes the island from LBA1's own scenes (the LBA1 folder in the settings): the Temple of Bú.
    public bool FromLba1 { get; init; }
    // A race over laps of its own (the race-track mode's race_laps=: the race is over at the last lap's line, the display counts them) --
    // the Temple of Bú's five (the user, 2026-10-17: "Let's make the temple track a 5 lap race"). Not for a story's race, which has its own.
    public int? RaceLaps { get; init; }
    // A flag marshal by the start line, dropping a flag at GO and waving the chequered flag at the finish (Temple.TempleMarshal; the
    // race-track mode's marshal=): the Temple of Bú's, the School of Magic's assistant.
    public bool FlagMarshal { get; init; }

    // Guns of the island's own scene the race-track mode fires at the cars (gunner= with gunner_hunt=): their entity, kept in the scene with
    // their scripts emptied, and how far they reach (cells) -- Island CX's cannons.
    public (int Entity, double Reach)? HuntingGuns { get; init; }

    // A sprint whose end the build makes (the Emperor's palace's: its lines and the teleport to Celebration Island, PalaceTrack) -- not a
    // dream (Dream: Polar Island's, which new games start in).
    public bool Sprint { get; init; }
    // Scenes of its own the build makes, FirstScene to LastScene (the palace's): a story set tells its track from the island's own by them.
    public bool OwnScenes { get; init; }
    // The rail holds the car on the raised road everywhere, even where it lies over flat ground (the race-track mode's rail_held=): the
    // Emperor's palace's road along the hilltop, 60 over it, let cars drive off onto the rocks.
    public bool RailHeld { get; init; }

    public static readonly RaceTrackIsland[] All = { Desert, Citadel, CitadelStorm, Mosquibe, Celebration, CelebrationLava, Elevator, Sendell, Moon, Emerald, Polar, Knartas, Otringal, Volcano, Palace, Wannies, CxTower, Temple };

    public static RaceTrackIsland ByName(string name) => All.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Desert;

    // The files a build of this island changes, besides the ones every build does (SCENE.HQR, and BODY.HQR / ANIM.HQR / RESS.HQR for the
    // cars and the jump).
    public string[] IslandFiles => TwinIleFile is { } ti && TwinOblFile is { } to ? new[] { IleFile, OblFile, ti, to } : new[] { IleFile, OblFile };
    // Those the game has, whose originals a build keeps (none of an island the build makes).
    public string[] KeptFiles => Created ? Array.Empty<string>() : IslandFiles;
}
