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
        // (the lighthouse, scene 46, where the storm ends: the game's own Raph by its door, his fiancee and Twinsen and Zoe on its top, whom
        // the Weather Wizard's spell needs -- RaceTrackStory -- and the door's people and track points on the storm file's ground: the town
        // circuit, in the fine weather's file, cut the hill there by 2,000)
        StoryEntities = new[] { RaceTrackStory.LighthouseRaph, RaceTrackStory.Fiancee, RaceTrackStory.CoupleOnTop },
        OwnGroundAt = new[] { (46, 481.5, 661.8, 10.0), (46, 463.0, 665.0, 3.0) },
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
    // The game's own people the story needs, by entity, kept in the island's scenes when the build takes the others off the road (the
    // souvenir seller on Celebration Island: RaceTrackStory.ApplyCelebration).
    public int[]? StoryEntities { get; init; }
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
    // base's long arm on its roof (the straight, its pit lane beside it on one deck), up onto the crater's rim and round it through three
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

    // Volcano Island (SOUSCELB.ILE, the game's island 11 under Celebration Island, scenes 130-132; cube (7,8) isn't there), redesigned on
    // 2026-10-10 round its new summit: the ground north of the main lava channel raised to 30,000, a lake of lava on top pouring down its
    // south face in four tiers (RaceTrackLavaFall). The start and the pit lane (its boost panel) on the sand plateau's north straight; south
    // under the summit's west cliff, between a crystal gate and down the canyon road along the falls; a drop jump off its end onto the east
    // ridge, south along it under a crystal arch over the crystal field in the lava; a jump down to the south shore, along it and up the
    // volcano's flank past its bowls of lava; round the plateau's crystal garden, under another arch (RaceTrackCrystals). The game's lava
    // balls out of the falls' foot, the lava, the bowls (LavaBalls). The plan: tools/RaceTrackPlan/volcano_design.py.
    public static readonly RaceTrackIsland Volcano = new("Volcano Island", "SOUSCELB.ILE", "SOUSCELB.OBL", 11, 130, 132, 38, "RaceTrackPlan.Volcano.json", null)
    {
        Title = "Volcano Island: over the lava", Roster = RaceDriver.Volcano,
    };

    public static readonly RaceTrackIsland[] All = { Desert, Citadel, CitadelStorm, Mosquibe, Celebration, CelebrationLava, Elevator, Sendell, Moon, Emerald, Polar, Knartas, Otringal, Volcano };

    public static RaceTrackIsland ByName(string name) => All.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Desert;

    // The files a build of this island changes, besides the ones every build does (SCENE.HQR, and BODY.HQR / ANIM.HQR / RESS.HQR for the
    // cars and the jump).
    public string[] IslandFiles => TwinIleFile is { } ti && TwinOblFile is { } to ? new[] { IleFile, OblFile, ti, to } : new[] { IleFile, OblFile };
    // Those the game has, whose originals a build keeps (none of an island the build makes).
    public string[] KeptFiles => Created ? Array.Empty<string>() : IslandFiles;
}
