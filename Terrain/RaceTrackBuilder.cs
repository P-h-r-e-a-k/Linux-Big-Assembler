using System.IO;
using System.Text.Json;

namespace LBAAssembler.Terrain;

// Builds a race track on an island from a centre line: levels the ground under it (with banking in the corners and an
// embankment either side), paints the retail Desert-island track's own pieces (asphalt, red/white curb line, orange arrows,
// red/gold hatching, the white start line) and clears the decor objects in the way. Works on the island-wide vertex grid, so a
// track can cross cube borders. The numbers (widths in cells of 512 units, heights in world units) are the ones the retail track
// uses: a 9-cell wide road with a one-cell curb line at each edge.
internal sealed class RaceTrackPlan
{
    // The plan's points are cells counted from this island cell.
    public int OriginCellX { get; set; }
    public int OriginCellZ { get; set; }
    // The closed centre line of the lap.
    public double[][] Points { get; set; } = Array.Empty<double[]>();
    // The two ends of the pit lane (it runs beside the lap between them).
    public double[]? PitA { get; set; }
    public double[]? PitB { get; set; }
    // A planned height for each point (world units): the road follows these instead of the ground, which it is cut into and built up
    // from as they say -- a lap that winds up a mountain whose sides are far steeper than a car can drive. They are grade-limited like the
    // ground's to MaxGrade (the plan's own when it gives one: a mountain lap is steeper than the Desert island's 9 %). Without them the
    // road follows the ground, smoothed and grade-limited.
    public double[]? Heights { get; set; }
    public double? MaxGrade { get; set; }
    // A bridge the plan draws (with Heights): the first and last point of its flat deck, which carries the road over whatever lies
    // beneath -- the sea, another part of the lap passing under it. Its heights must be level there.
    public int[]? Deck { get; set; }
    // A jump over a gap in the ground (with Heights) -- a bay, a chasm: the point of the take-off lip and of the landing lip. The ground
    // between is left as it is; the heights must be level from a ramp's length before the one to a landing ramp's length after the other.
    public int[]? GapJump { get; set; }
    // Several jumps over gaps (with Heights), each [take-off lip, landing lip] as GapJump's, up to RaceTrackJumpAnim.MaxJumps with it.
    // Celebration Island's lava lake has two, whose leaps cross: where the lap crosses itself inside a jump's gap -- each causeway's gap
    // takes in where the other would cross it -- nothing is built, and the cars fly over it.
    public int[][]? GapJumps { get; set; }
    // The plan's own ramps for its jumps (RaceTrackOptions.JumpRampLength, JumpLandingLength), and the shortest flight it may have, as a
    // share of the retail one (RaceTrackOptions.JumpMinScale): a small island's short leaps.
    public double? JumpRampLength { get; set; }
    public double? JumpLandingLength { get; set; }
    public double? JumpMinScale { get; set; }
    // The plan's gap jumps: GapJump's, then GapJumps'.
    public List<int[]> AllGapJumps => new[] { GapJump }.Concat(GapJumps ?? Array.Empty<int[]>()).OfType<int[]>().Where(j => j.Length == 2).ToList();
    // Cells over which each end of the pit lane moves out to its full offset beside the lap (24 when not given).
    public double? PitTaper { get; set; }
    // The road's own widths (cells from its middle), for an island too small for the usual road: the asphalt's edge, the curbs' outer
    // edge, the verge's, and how far beyond that the levelled ground blends back into the island's.
    public double? AsphaltHalf { get; set; }
    public double? CurbHalf { get; set; }
    public double? VergeHalf { get; set; }
    public double? Blend { get; set; }
    // A raised road (with Heights): the first and last point of a stretch that stands in the air on piers -- Celebration Island's lap
    // winds up round the statue and comes back down over itself, and the engine's ground is one height map. The ground under it is left
    // as it is; the road is a row of decor pieces following the plan's heights at any grade (RaisedHalf cells from its middle to its
    // rail), and the engine's race-track mode drives on it (RACEMOD.CPP: the raised road is a floor of its own).
    // A sprint (Open) may have several raised stretches, each its first and last point in turn: [from, to, from, to ...] -- Polar Island's
    // has its carried jump over the main straight and its jump onto the rocky peak, with the ground road between them.
    public int[]? Raised { get; set; }
    public double? RaisedHalf { get; set; }
    // How far behind each other the grid's spots are (RaceTrackScenes.GridSpot), when not the usual 3.5: the lava lake's lap comes down a
    // ramp onto its dock just behind the grid, which stands two abreast there (1.75).
    public double? GridStep { get; set; }
    // A footing of ground under a start gantry's post that stands over a hole (RaceTrackBuilder.PostFootings): the lava lake's dock is
    // narrower than its gantry.
    public bool GantryFootings { get; set; }
    // The ground cut down where it comes near the raised road's deck (RaceTrackBuilder.CutUnderRaised: the lava lake's lap, which runs
    // along cliffs and the crater's rim; the statue track's was left as it was built).
    public bool RaisedCut { get; set; }
    // The ground under the buildings KeepBodies keeps (and KeepGroundMargin cells round them) left as it is, unless the road's own surface
    // runs over it: a road's verge and embankment, blended into the ground, lifted Citadel Island's pharmacy 950 units with the ground
    // under its origin (decors follow it, IslandOps.DecorFollow) and cut the hill under its lighthouse's door (2026-10-06).
    public bool KeepGroundUnder { get; set; }
    // The lap's middle moved off the buildings KeepBodies keeps, as it is kept off the island's edge (KeepClear): its curbs half a cell
    // clear of each one's footprint where the island's edge leaves room -- the town circuit past Mr. Paul's house and the ticket office.
    public bool KeepClear { get; set; }
    // A building KeepBodies keeps that the road's surface still runs into from one side made shallower on that side, its front -- the far
    // side -- as it was (RaceTrackBuilder.TrimKept): the user's way with Citadel Island's buildings, "not quite as deep so they don't
    // interfere with the track" -- the pharmacy against the town circuit's bridge ramp.
    public bool TrimKept { get; set; }
    // Kept pieces cut down to a wall (RaceTrackBuilder.ThinKept): each squeezed toward one side of its own box (Toward: west, east, north or
    // south) until it is Cells deep, so its far face -- the building's own wall -- stands against that side. A building is several pieces
    // that share their inner sides, open there: one piece kept between two roads (Citadel Island's shop, the piece with its door, under the
    // town circuit's bridge and between its two streets) has its neighbours kept as its walls (2026-10-06).
    public ThinKeptPiece[]? ThinKept { get; set; }
    // Huts the road drives through (RaceTrackDriveThrough): made bigger and cut open where it runs -- the Island of the Francos' village.
    public DriveThroughHut[]? DriveThrough { get; set; }
    // Pipes over the road (RaceTrackPipes): gantries along stretches of the lap, steam from their tops and oil dripping onto the road -- the
    // Island of the Francos' refinery. From and To are the plan's points.
    public PipeRun[]? Pipes { get; set; }
    // Steam jets across the road (RaceTrackPipes.Place): along stretches of the lap, out of the gantries' uprights there (out of the rail
    // where a stretch has none: PlaceJets), one side of the road and then the other -- the Gazogem factory's steam, blowing in bursts that
    // hit a car in them. From and To are the plan's points.
    public SteamJetRun[]? SteamJets { get; set; }
    // A pipeline over the lap (RaceTrackPipes.PlacePipeline): the Island of the Francos' from the Gazogem factory to the air-boat.
    public PipeLine? Pipeline { get; set; }
    // The raised road's colours stretch by stretch (DeckTheme by name: "dock", "refinery", "village"), From and To the plan's points;
    // elsewhere the standard greys, red and white.
    public ThemeRun[]? Themes { get; set; }
    // The road bridge's deck at least this high (RaceTrackOptions.RoadBridgeLeast): over a building the plan keeps under it -- the town
    // circuit's deck over Citadel Island's shop, whose top is 3500.
    public double? DeckLeast { get; set; }
    // The start line's point, for a lap with no pit lane (with one, the line is in the pit lane's middle).
    public int? Start { get; set; }
    // A sprint, not a lap (Polar Island's dream race): the route runs from its first point to its last and doesn't come back round, so
    // the road has two ends; the race starts at Start and ends at the finish line, at the point Finish. No checkpoints: the racing
    // lines run from the start line to the finish.
    public bool Open { get; set; }
    public int? Finish { get; set; }
    // The lowest the road is laid over the sea, when not the usual 700.
    public double? SeaClearance { get; set; }
    // Where the opponents' cars wait while the player qualifies, for a lap with no pit lane: [x, z, heading x, heading z] each.
    public double[][]? PitSpots { get; set; }
    // Decor bodies that stay where they are under the raised road (a statue the road winds round): where one's collision box reaches
    // into the road's space the box is cut down under the road instead of the object being cleared.
    public int[]? KeepBodies { get; set; }
    // Decors whose top is this high or higher stay, whatever their body, as KeepBodies' do (their collision boxes cut down under a raised
    // road that passes over them): Polar Island's rocky peak and its plateau are decors, and its jump lands on the peak's top -- the
    // build took the peak's columns away under the road, down to the sea, and left the road on its piers in mid-air (2026-10-06).
    public int? KeepAbove { get; set; }
    // A raised road's banking (with Raised), one number for each point: how much the road's surface rises across it, per unit of the way
    // to the left of the way the lap runs (so a bend to the left, its inside the lower, has it negative). The Elevator Platform's lap is
    // a rollercoaster: it leans into its bends.
    public double[]? Bank { get; set; }
    // For the race-track mode, on a raised road: how much the road's grade changes what a car can do there -- its top speed is that of
    // its gear times (1 - Gravity x grade), between a half and twice it, so it crawls up a lift and runs away down a drop. None: a
    // car goes as fast uphill as down.
    public double? Gravity { get; set; }
    // For the race-track mode, on a raised road: the camera that follows the car rides the road behind it -- [cells behind the car,
    // units over the road, cells ahead of the car that it looks at] -- instead of hanging on an arm behind the car, which the road's
    // other levels, a drop's crest and a tight bend all get in the way of.
    public double[]? RailCamera { get; set; }
    // Decor bodies a raised road's piers may stand on (a platform's decks); with this given, the piers are placed by the island's real
    // shapes (RaceTrackScenery) -- on the sea, the ground or one of these, clear of everything else, beside the road where they cannot
    // stand under it. Without it they stand on the ground under the road's middle or are left out.
    public int[]? PierBodies { get; set; }
    // Land mines: [cell x, cell z] each (from the origin), off the road where a car could go round part of the lap -- Mosquibees Island's
    // way round its jump. Each is a copy of the retail Desert island's mine (RaceTrackScenes.AddMine).
    public double[][]? Mines { get; set; }
    // Vertical loops (with Heights): each [the point at the ring's foot, the ring's radius to its road surface (cells), how far across the
    // car comes down from where it went in (cells), the gap at its top (degrees, 0: none)]. A ring stands on the road there, in the plane
    // of the lap's way (RaceTrackLoopBody), and the engine's race-track mode carries the car round it (RACEMOD.CPP RaceMod_Loop): round a
    // whole ring as a real car would go, falling off if too slow; round a ring with a gap at its top at the speed it came in with, over
    // the gap upside down. The road must run straight and level for the radius and more either side of the foot. A fifth number: the
    // ring's band, from its middle to its rails (cells; RaceTrackBuilder.LoopBandHalf when not given) -- the Emerald Moon's are as wide as
    // its road, drifting across a deck twice as wide.
    public double[][]? Loops { get; set; }
    // A raised road's width point by point (with Raised): cells from its middle to its rail, instead of RaisedHalf everywhere -- the
    // Emerald Moon's lap widens to the reactor's dish for its jump, to twice its width at its loops and beside its straight for the pit lane.
    public double[]? RaisedHalfs { get; set; }
    // Jumps the engine carries the car over (with Raised), each [the take-off ramp's foot, its lip, the landing lip, the landing hill's
    // foot]: the plan's heights from foot to foot are the car's way -- a ramp curving up, the flight, a hill curving down -- and the road
    // has no deck between the lips. The race-track mode (RACEMOD.CPP arcjump=) takes the car at the ramp's foot and carries it along that
    // way, from one cube into the next if it crosses one: the Emerald Moon's leap over the whole reactor. A fifth number: where
    // the camera is while the car is carried -- 1 or -1 beside the jump, on the side the builder's Across points to or the other (the
    // default, 1), 0 behind the car as on the road (Polar Island's jump over its main straight).
    public int[][]? ArcJumps { get; set; }
    // A white stripe along a raised road (with Raised): [first point, last point, how far across from the road's middle (cells, the way
    // the builder's Across points)] -- between the Emerald Moon's straight and the pit lane beside it on the same deck.
    public double[]? PitStripe { get; set; }
    // A fence along that stripe (with PitStripe): Citadel Island's white fence (CITADEL.OBL's bodies 53 and 54), solid, between the race
    // lanes and the pit lane. The racing lines keep off the pit lane's side of it.
    public bool PitFence { get; set; }
    // The grid's spots moved across the road by this much (cells, the way the builder's Across points): onto the Emerald Moon's race lanes,
    // beside its pit lane on the same deck (RaceTrackScenes.GridSpot).
    public double? GridShift { get; set; }
    public bool Planned => Heights is { Length: > 0 } h && h.Length == Points.Length;

    // The plan's own road widths and sea clearance, onto the options a build uses.
    public void ApplyTo(RaceTrackOptions o)
    {
        if (AsphaltHalf is { } a) o.AsphaltHalfWidth = a;
        if (CurbHalf is { } c) o.CurbHalfWidth = c;
        if (VergeHalf is { } v) o.VergeHalfWidth = v;
        if (Blend is { } b) o.BlendWidth = b;
        if (SeaClearance is { } s) o.BridgeClearance = s;
        if (RaisedHalf is { } r) o.RaisedHalfWidth = r;
        if (JumpRampLength is { } jr) o.JumpRampLength = jr;
        if (JumpLandingLength is { } jl) o.JumpLandingLength = jl;
        if (JumpMinScale is { } js) o.JumpMinScale = js;
        if (GridStep is { } gs) o.GridStep = gs;
        if (GridShift is { } gsh) o.GridShift = gsh;
        o.PitFence = PitFence && PitStripe is { Length: 3 };
        o.GantryFootings = GantryFootings;
        o.PierBodies = PierBodies?.ToHashSet();
    }

    public static RaceTrackPlan Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static RaceTrackPlan Read(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        try { return JsonSerializer.Deserialize<RaceTrackPlan>(stream, options) ?? throw new InvalidDataException("The plan is empty."); }
        catch (JsonException error) { throw new InvalidDataException($"The plan is not a race track plan: {error.Message}", error); }
    }

    // The track an island's route ships as inside the program (docs/racetrack/*_track_plan.json).
    public static RaceTrackPlan Built(RaceTrackIsland island) => Resource(island.PlanResource, island.Name);

    // The track of the island's other-weather file, when it has one of its own (Citadel Island's town circuit, in CITABAU); null when
    // that file is built with the same track as the island's own.
    public static RaceTrackPlan? BuiltTwin(RaceTrackIsland island) =>
        island.TwinPlanResource is { } name ? Resource(name, $"{island.Name} in its other weather") : null;

    private static RaceTrackPlan Resource(string name, string what)
    {
        using var stream = typeof(RaceTrackPlan).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException($"This build of the program has no built-in track plan for {what}.");
        return Read(stream);
    }
}

internal enum CrossingStyle { Level, Viaduct, Jump, Bridge }

internal sealed class ThinKeptPiece
{
    public int Body { get; set; }
    public string? Toward { get; set; }
    public double Cells { get; set; } = 0.25;
}

// Where a jump takes off and lands (island cell coordinates), what the scene needs to run it. Index: which of the lap's jumps (its zone,
// its flight and its labels in the hero's track script are its own); S0, S1: where its flight starts and ends along the lap (cells, the
// lap's own distance from its first point: the flight is over that stretch of it, and no other).
// A vertical loop as built (RaceTrackPlan.Loops): its foot (island cells), the road's height there, the lap's way, the ring's radius and
// how far across the car comes out of it (cells), its top's gap (degrees), and the lap's point at the foot.
// Band: the ring's band from its middle to its rails (cells).
internal sealed record LoopInfo(double X, double Z, double Y, double DirX, double DirZ, double Radius, double Shift, double Gap, int Point, double Band = 0);

internal sealed record JumpInfo(double StartX, double StartZ, double LandX, double LandZ, double DirX, double DirZ, int Beta, double Height,
    int CubeX, int CubeZ, List<(int X0, int Z0, int X1, int Z1)> Boxes, int Zone, int Anim, double FlightScale, double FlightCells,
    int Index = 0, double S0 = 0, double S1 = 0, double Drop = 0);

internal sealed class RaceTrackOptions
{
    public double AsphaltHalfWidth { get; set; } = 3.5;
    public double CurbHalfWidth { get; set; } = 4.5;
    public double VergeHalfWidth { get; set; } = 6.0;
    // Cells over which the levelled ground blends back into the natural ground beyond the verge.
    public double BlendWidth { get; set; } = 7.0;
    // Steepest climb along the road, height per horizontal distance.
    public double MaxGrade { get; set; } = 0.09;
    // Cross slope in a bend: height units per cell of the way across, per (1 / turn radius in cells).
    public double BankGain { get; set; } = 700;
    public double MaxBank { get; set; } = 70;
    // How far the ground height along the road is smoothed (cells).
    public double ProfileSmoothing { get; set; } = 6;
    // How far the grade-limited profile is smoothed again (cells), so crests and dips are rounded rather than kinks.
    public double VerticalSmoothing { get; set; } = 4;
    // Where the road is lowest above the sea when it crosses water.
    public double BridgeClearance { get; set; } = 700;
    public double Spacing { get; set; } = 0.5;
    // What stands where the lap crosses itself: nothing (level crossing), a viaduct of retail arches over it, or a jump (the retail
    // Desert island's own "car jump": a scripted flight of the buggy, see RaceTrackScenes).
    public CrossingStyle Crossing { get; set; } = CrossingStyle.Bridge;
    // The hero's animation number that plays the jump's flight (RaceTrackJumpAnim: a longer copy of the retail flight, ANIM.HQR 51, which
    // Twinsen plays as 67). How much longer is decided by the layout (PlanJump): the flight carries the car from the take-off strip over
    // the gaps and the other road to JumpLandInto cells down the far ramp. Each island's own (RaceTrackJumpAnim.GenericFor, set by
    // RaceTrackService.Prepare), so the tracks of several islands built together each fly their own.
    public int JumpAnim { get; set; } = RaceTrackJumpAnim.Generic;
    public double JumpLandInto { get; set; } = 3.5;
    // The scenario zone number of the take-off strip and the labels the hero's track script gets.
    public int JumpZone { get; set; } = 40;
    // A jump clears the other road more easily the steeper it crosses it, so near the crossing the straighter road is turned until the two
    // meet at an angle, and bent back to the drawn course over the next cells. The steepest angle from JumpCrossingAngle down to
    // JumpMinCrossingAngle (degrees) is used whose turned road comes no closer than JumpClearance cells to any other part of the lap
    // (away from the crossing itself): turned too far, the stretch beyond the jump ran into the next part of the lap and read as a
    // second crossing.
    public double JumpCrossingAngle { get; set; } = 64;
    public double JumpMinCrossingAngle { get; set; } = 36;
    public double JumpClearance { get; set; } = 12.5;
    // The ramps. The up ramp climbs this high above the road over this many cells, to a lip; between the lip and the other road's curbs
    // lies a gap of sand this many cells long (measured along the road), and as much again between the other road and the down ramp,
    // which runs back down to the road over this many cells. The down ramp's top is lower than the lip: the flight ends lower than it
    // starts, and it lands a few cells down the ramp.
    public double JumpRampHeight { get; set; } = 800;
    public double JumpRampLength { get; set; } = 8;
    public double JumpGap { get; set; } = 3;
    public double JumpLandingLength { get; set; } = 12;
    // The shortest flight a jump has, as a share of the retail one (17.6 cells): a plan's short leaps fly less (RaceTrackPlan.JumpMinScale).
    public double JumpMinScale { get; set; } = 1;
    // How far behind each other the grid's spots are (RaceTrackScenes.GridSpot; RaceTrackPlan.GridStep).
    public double GridStep { get; set; } = RaceTrackScenes.DefaultGridStep;
    // A footing under a gantry's post that stands over a hole (RaceTrackPlan.GantryFootings).
    public bool GantryFootings { get; set; }
    // The grid moved across the road (RaceTrackPlan.GridShift, cells).
    public double GridShift { get; set; }
    // A fence along a raised road's stripe (RaceTrackPlan.PitFence), and its bodies -- a section and its end post, copied from Citadel
    // Island's own (RaceTrackService.Prepare).
    public bool PitFence { get; set; }
    public byte[]? FenceSection { get; set; }
    public byte[]? FencePost { get; set; }
    // A physical, walkable bridge deck (like Citadel Island's rope bridge at "the Cliffs of the Woodbridge"): the straighter road
    // is carried over the other, on a flat deck built of decor objects (RaceTrackDeckBody), while the ground underneath keeps
    // the other road's own grade. How far above the lower road's own height the deck's walking surface sits. The engine's solid
    // collision (WorldColBrickDecors) tests the car's whole ZV box against the deck's, so the deck's UNDERSIDE must clear the
    // car's roof: the retail Desert arch keeps its deck 2260 units over the road it spans; measured in the engine, Twinsen walking the lower road
    // is still stopped at 2300 (the lower road climbs under the deck's far end) and walks straight through at 2800.
    public double RoadBridgeClearance { get; set; } = 2800;
    // (and never lower than this: RaceTrackPlan.DeckLeast)
    public double RoadBridgeLeast { get; set; }
    // How deep the deck's collision box reaches below its own walking surface. Thin, so what passes underneath only has to
    // clear RoadBridgeClearance - this.
    public double RoadBridgeDeckThickness { get; set; } = 200;
    // The crossing is re-shaped to meet at this angle (degrees) before the bridge is planned -- the concept picture's own
    // crossing is a clean X of about 42 degrees, and a shallow crossing would need an absurdly long deck.
    public double BridgeCrossingAngle { get; set; } = 42;
    // Extra clearance (cells) the deck's flat core must reach past the lower road's own verge, each side.
    public double RoadBridgeMargin { get; set; } = 2;
    // How many cells of deck each placed piece covers along the road (the deck is as wide as the upper road itself).
    public double RoadBridgeTileLength { get; set; } = 4;
    // How many cells of ramp lead up to (and down from) the deck's own flat core, each side.
    public double RoadBridgeRampLength { get; set; } = 50;
    // How many cells of ground at the deck's own height lie between each end of the deck and the top of its ramp. A decor's
    // collision box is axis-aligned, so a turned tile's box overhangs its mesh by up to (cos + sin - 1) / 2 of a tile (0.8 cells
    // here, 2 cells at the edge tiles' outer corners), and a car carried by it reaches ~0.6 cells further: with the ground there
    // level with the deck, driving onto the overhang is not a step (it was an 84-unit bump ~3 cells before the deck), and a car
    // turning off the deck's last cells comes down on level ground, not over the ramp's rock shoulder (it fell 1000-3300).
    public double RoadBridgeLanding { get; set; } = 4;
    // Low red and white railings along both edges of the deck (solid: the car cannot drive off the side and drop 2800).
    public bool DeckRailings { get; set; } = true;
    // The scene side (RaceTrackScenes): actors other than Twinsen, the buggy and the hidden Zoe slot removed; the buggy present from the start of any game (the
    // car quest no longer hides it); Twinsen and the buggy set at the start line; zones that would act on a car on the road (doors, hit, ladder ...) removed.
    public bool RemoveActors { get; set; } = true;
    public bool BuggyAlways { get; set; } = true;
    public bool StartAtLine { get; set; } = true;
    // A copy of the retail track's racer in every outside scene, for the race-track mode to race against (RaceTrackScenes).
    public bool AddOpponent { get; set; } = true;
    // A second opponent: Baldino in his rocket car (RaceTrackBaldinoCar), a body added to BODY.HQR for the racer's entity, with a line of his own.
    public bool AddBaldino { get; set; } = true;
    // The motorbike Rabbibunny (Citadel Island's bike taxi, entity 100) races too, on a line of his own.
    public bool AddBiker { get; set; } = true;
    // The track's own line-up (RaceTrackIsland.Roster) in place of the three above; null: those three, as they say.
    public List<RaceDriver>? Drivers { get; set; }
    // Who races, in the engine's order: the line-up, or the racer, Baldino and the biker as the three above say (Baldino rides on the
    // racer's entity: only with it).
    public List<RaceDriver> Racers() => Drivers?.ToList()
        ?? new[] { RaceDriver.Racer, RaceDriver.Baldino, RaceDriver.Biker }.Where(d => d.Bike ? AddBiker : AddOpponent && (d == RaceDriver.Racer || AddBaldino)).ToList();
    public bool RemoveRoadZones { get; set; } = true;
    // Camera zones (type 1: while the hero is inside the box the view jumps to a fixed camera) that reach the road or within
    // CameraMargin cells of it are removed, so the view keeps following the car all the way round.
    public bool RemoveTrackCameras { get; set; } = true;
    public double CameraMargin { get; set; } = 2;
    public bool RemoveSolidDecors { get; set; } = true;
    // The track drawn into the island's holomap picture(s) (RaceTrackHolomap).
    public bool DrawOnHolomap { get; set; } = true;
    // Citadel Island's opening sends Twinsen to the start line (RaceTrackStory): Zoe's line and a holomap arrow.
    public bool Story { get; set; } = true;
    // The island OBL index of the flat deck body (RaceTrackDeckBody), appended by RaceTrackService before the build runs. -1 if
    // no bridge deck body is available (the Bridge crossing style then falls back to a level crossing).
    public int DeckBodyIndex { get; set; } = -1;
    // A raised road (RaceTrackPlan.Raised): half its width, rail to rail, and the island OBL index its pieces' bodies start at (the
    // build makes a body for each piece -- RaceTrackReport.NewBodies -- and RaceTrackService appends them after the build).
    public double RaisedHalfWidth { get; set; } = 3.75;
    public int NewBodyBase { get; set; } = -1;
    // The bodies its piers may stand on (RaceTrackPlan.PierBodies), and the island's OBL as the build finds it, for their shapes.
    public HashSet<int>? PierBodies { get; set; }
    public string? SceneryObl { get; set; }
    // Decor bodies that are plants, posts, fences and small props: cleared where they stand on the road. Everything else is
    // a building or a rock.
    public HashSet<int> RemovableBodies { get; set; } = new()
    {
        0, 1, 2, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 23, 29, 30, 31, 32, 34, 35, 36, 37, 39, 41, 44, 45, 47, 48, 50, 51, 55, 56, 57,
        58, 59, 60, 61, 62, 63, 72, 73, 74, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 93, 98, 99, 101, 103, 104, 105,
    };
    // Decor bodies of the retail race track that are left alone (its gantry and arch). Only on its own island: body numbers are an island's
    // own, and on Citadel Island 64-69 are the walls, pillars and a frame of ordinary buildings.
    public HashSet<int> ProtectedBodies { get; set; } = new() { 64, 65, 66, 67, 68, 69, 70, 71 };
    // The retail race track's cube (island cube coordinates): what is left of it -- its painted road, curbs, arrows and hatching,
    // and its decor pieces (OldTrackBodies) -- is cleared before the new lap is laid, which now runs through the same ground.
    // null leaves the retail track as it was.
    public (int X, int Z)? OldTrackCube { get; set; } = (7, 10);
    // Which island the track is built on, and how its road is painted (RaceTrackTextures.Import fills the theme in before the build).
    public RaceTrackIsland Island { get; set; } = RaceTrackIsland.Desert;
    // The retail race track's decor bodies the build places -- the start gantry (64-66) and the viaduct's arch (68-70) -- are the Desert
    // island's; on another island those numbers are its own, quite different objects (on Citadel Island 64 and 65 are a building's stone
    // wall and pillar, which stood at the start line as "the remains of a building"). There RaceTrackService copies the bodies into the
    // island's own OBL and this maps each retail number to its copy. Empty: the numbers as they are (the Desert island).
    public Dictionary<int, int> RetailBodies { get; set; } = new();
    public int RetailBody(int body) => RetailBodies.TryGetValue(body, out var copy) ? copy : body;
    // (the same options for the island's fine-weather twin, which differs in its deck body and its theme)
    public RaceTrackOptions Copy() => (RaceTrackOptions)MemberwiseClone();

    // An island's track as it is always built: its crossing (RaceTrackIsland.Crossing) and, on the Desert island, the retail track's
    // leftovers cleared (the race track window and the command line's builds both start from this).
    public static RaceTrackOptions For(RaceTrackIsland island) => new()
    {
        Island = island, OldTrackCube = island.OldTrackCube, Crossing = island.Crossing,
        AddOpponent = !island.NoOpponents, AddBaldino = !island.NoOpponents, AddBiker = !island.NoOpponents, DrawOnHolomap = !island.NoHolomap,
        Drivers = island.NoOpponents ? new() : island.Roster,
    };
    public RaceTrackTheme Theme { get; set; } = RaceTrackTheme.Retail;
    // The retail track's own decor pieces: start gantry (64-66), billboard (67), arch and its abutments (68-70), wedge (71). The
    // garage's lamp (36) and the sphero's crystal (1) stay.
    public HashSet<int> OldTrackBodies { get; set; } = new() { 64, 65, 66, 67, 68, 69, 70, 71 };
    // Cells (island cell coordinates, inclusive) the new track must not touch: the retail track.
    public List<(int X0, int Z0, int X1, int Z1)> Keep { get; set; } = new();
}

internal sealed class RaceTrackReport
{
    // the huts the road drives through: each its cube, its origin (cube units) and how much bigger it was made (RaceTrackDriveThrough)
    public List<(int CubeX, int CubeZ, int X, int Z, double Scale)> DriveThroughs { get; } = new();
    // the pipes' steam vents, [x, y, z, every (ms)], and oil drips, [x, y, z, the road's y under it, every (ms)] (world units from the
    // island's corner; RACEMOD.CPP steam= and drip=)
    public List<int[]> Steam { get; } = new();
    public List<int[]> Drips { get; } = new();
    // ... and the steam jets across the road, [x, y, z (its middle), half width, reach, blowing (ms), not (ms), phase (ms), way x, way z]
    // (RACEMOD.CPP jet=)
    public List<int[]> Jets { get; } = new();
    public int Vertices, Cells, DecorsRemoved, SolidDecorsRemoved, BridgeCells;
    public double Length;
    public List<string> Notes { get; } = new();
    public List<(int Start, int End)> BridgeSpans { get; } = new();
    public List<(int CubeX, int CubeZ, int Body, string Kind)> Removed { get; } = new();
    public List<double[]> Arrows { get; } = new();
    // Where the lap crosses itself (island cell coordinates, the height there, the angle between the two roads in degrees).
    public List<(double X, double Z, double Y, double Angle)> Crossings { get; } = new();
    public List<(double X, double Z, double Y, double DirX, double DirZ)> StartLine { get; } = new();
    // Where the opponents' cars wait in the pit lane while the player qualifies (island cells, the height and the way the lane runs there).
    public List<(double X, double Z, double Y, double DirX, double DirZ)> Pits { get; } = new();
    // The plan's land mines (island cells).
    public List<(double X, double Z)> Mines { get; } = new();
    // ... their heights are the plan's own (spots on a deck of decor), not the ground's.
    public bool PitHeights { get; set; }
    // The roads as built (the lap first, then the pit lane): their shape, heights, widths and deck, for drawing them elsewhere (the holomap).
    public List<TrackRoad> Roads { get; } = new();
    // The way the start line's road runs as the line is painted: the road's heading turned onto the cell grid when it is close to it
    // (the gantry over the line and the line laps are counted at follow it), else the heading itself.
    public (double DirX, double DirZ)? StartLineSquare { get; set; }
    // How far the outer curbs reach either side of the start line's middle (cells, to its left and right across the way the lap runs):
    // the lap's own, or on the pit lane's side the pit lane's, where it runs beside the start line. The gantry stands outside them.
    public (double Left, double Right)? StartCurbs { get; set; }
    // Where a lap is counted: the start line's ends (island cells), across the road and the pit lane beside it, and the way the lap runs.
    public (double X0, double Z0, double X1, double Z1, double DirX, double DirZ)? LapLine { get; set; }
    // Checkpoints round the lap, in order (PlaceCheckpoints): a line across the road (island cells) and the way the lap crosses it.
    public List<(double X0, double Z0, double X1, double Z1, double DirX, double DirZ)> Checkpoints { get; } = new();
    // A sprint's (RaceTrackPlan.Open): the route has two ends, and the race is over at the finish line (the plan's Finish point: across
    // the road there, as the lap line is), at its height.
    public bool Open { get; set; }
    public (double X0, double Z0, double X1, double Z1, double DirX, double DirZ)? FinishLine { get; set; }
    public double? FinishLineHeight { get; set; }
    // The road's height at each checkpoint and at the lap line: a lap that passes over itself crosses a line's place at several heights.
    public List<double> CheckpointHeights { get; } = new();
    public double? LapLineHeight { get; set; }
    // A raised road, for the engine: its middle, point by point in lap order, [x, z, y, half width] in world units from the island's corner.
    public List<int[]> Raised { get; } = new();
    // ... whether it is the whole lap (its last point is then its first again), and the plan's Gravity.
    public bool RaisedLoop { get; set; }
    public double? Gravity { get; set; }
    public double[]? RailCamera { get; set; }
    // The raised road's surface at an island cell position, for something at about height `near` (the road passes over itself: the
    // level nearest that height), or null where the road isn't.
    public Func<double, double, double, double?>? RaisedFloor { get; set; }
    // Decor bodies the build made (a raised road's pieces and piers), to append to the island's OBL from RaceTrackOptions.NewBodyBase on.
    public List<byte[]> NewBodies { get; } = new();
    // The opponent's line (PlanRacePath) from the start line round the lap: island cells, the height, the speed in world units a second (with
    // the race car setup's reference car) and the line's bend radius there (world units; the engine plans the speeds from it and the car).
    public List<(double X, double Z, double Y, double Speed, double Radius)> RacePath { get; } = new();
    // Baldino's line: the same, keeping more to the other side of the road.
    // ... and each driver's (RaceTrackOptions.Racers, in that order; the first is RacePath)
    public List<List<(double X, double Z, double Y, double Speed, double Radius)>> DriverPaths { get; } = new();
    // The lap's centre line as built (island cells), for checks.
    public double[] LapX { get; set; } = Array.Empty<double>();
    public double[] LapZ { get; set; } = Array.Empty<double>();
    public List<(double X0, double Z0, double X1, double Z1)> BridgeCoords { get; } = new();
    public List<string> Placed { get; } = new();
    // The lap's jumps (a plan's gap jumps, in the plan's order; the crossing's jump).
    public List<JumpInfo> Jumps { get; } = new();
    // The plan's vertical loops (RaceTrackPlan.Loops).
    public List<LoopInfo> Loops { get; } = new();
    // The plan's carried jumps (RaceTrackPlan.ArcJumps): the lap's points (as built) of each one's ramp foot, lip, landing lip and hill
    // foot, the lap's distance at the two feet (from its first point), and -- once the raised road is placed -- the same four as indices
    // into Raised (the engine's arcjump=).
    public List<(int Foot, int Lip, int Land, int LandFoot, double S0, double S1)> ArcJumps { get; } = new();
    // ... and where the camera is while the car is carried over each (the plan's fifth number; null: the engine's own, beside it)
    public List<int?> ArcCameras { get; } = new();
    public List<int[]> ArcRaised { get; } = new();
    // The lap's heights as built, and whether each point is on the raised road and in a carried jump's gap (the cube edges' crossing zones
    // stand at the raised road's height).
    public double[] LapY { get; set; } = Array.Empty<double>();
    public bool[]? LapRaised { get; set; }
    public bool[]? LapArc { get; set; }
    // A drop's camera (RACEMOD.CPP jumpcam=): its flight's animation and the place the camera stands while the car flies it (cells, units up).
    public List<(int Anim, int CubeX, int CubeZ, double X, double Y, double Z)> JumpCameras { get; } = new();
    public JumpInfo? Jump => Jumps.Count > 0 ? Jumps[0] : null;
    public RoadBridgeInfo? RoadBridge { get; set; }
    // How far (cells) an island cell position is from the nearest road's centre line, 1e9 when far away.
    public Func<double, double, double> DistanceToRoad { get; set; } = (_, _) => 1e9;
    // The ground's height at an island cell position before and after the build (the scenes re-seat what stood on reshaped ground).
    public Func<double, double, double>? GroundBefore { get; set; }
    public Func<double, double, double>? GroundAfter { get; set; }
    // Whether an island cell position was drawn ground before the build (not the sea).
    public Func<double, double, bool>? WasGround { get; set; }
}

internal sealed class TrackRoad
{
    public string Name = "";
    public bool Closed;
    public double[] X = Array.Empty<double>(), Z = Array.Empty<double>();     // island cell coordinates
    public double[] S = Array.Empty<double>(), Tx = Array.Empty<double>(), Tz = Array.Empty<double>(), Kappa = Array.Empty<double>();
    public double[] H = Array.Empty<double>();
    public bool[] Bridge = Array.Empty<bool>();
    public bool[] Deck = Array.Empty<bool>();
    // The road-over-road bridge's heads: the ground level with the deck (under its last cells and on the landings beyond),
    // shaped wider than the road so a turned deck tile's overhanging collision box always lies over ground at its height.
    public bool[] Landing = Array.Empty<bool>();
    // ... and of those, the ones under the deck's own last cells (hidden by the tiles from above; the ground there rises steeply
    // from the other road's level, so it is painted as rock, not as road)
    public bool[] UnderDeck = Array.Empty<bool>();
    // The jump: its ramps and the gap between them (Jump), the gap alone, where the car is in the air and the road gives way to sand
    // (Gap), and the last cell of each ramp's top, painted as a warning stripe (Lip).
    public bool[] Jump = Array.Empty<bool>(), Gap = Array.Empty<bool>(), Lip = Array.Empty<bool>();
    // A gap jump's gap (RaceTrackPlan.GapJump): nothing there is the road's -- the ground is left as it is and not painted.
    public bool[] Void = Array.Empty<bool>();
    // The plan's own heights, point by point (RaceTrackPlan.Heights), or null.
    public double[]? Planned;
    // The raised road's colours point by point (RaceTrackPlan.Themes), or null: the standard ones.
    public DeckTheme?[]? Themes;
    // A raised road's points (RaceTrackPlan.Raised), or null: they keep the plan's heights at any grade and are Deck, so the ground under
    // them is neither shaped nor painted.
    public bool[]? Raised;
    // A raised road's banking, point by point (RaceTrackPlan.Bank), or null: level across.
    public double[]? RoadBank;
    // A raised road's width, point by point (RaceTrackPlan.RaisedHalfs, cells from its middle to its rail), or null: RaisedHalfWidth.
    public double[]? RaisedHalfs;
    // The points in a carried jump's gap (RaceTrackPlan.ArcJumps), or null: the car is carried through the air there, nothing in the way
    // of the road is cleared and no crossing zone stands at the cube's edge.
    public bool[]? Arc;
    // A white stripe along a raised road (RaceTrackPlan.PitStripe): its first and last point and how far across (cells), or null.
    public (int From, int To, double Offset)? Stripe;
    public double AsphaltHalf, CurbHalf, VergeHalf, Blend;
    public int Count => X.Length;
    public double Length;

    public TrackRoad Clone()
    {
        var copy = (TrackRoad)MemberwiseClone();
        foreach (var f in typeof(TrackRoad).GetFields())
            if (f.GetValue(this) is Array array) f.SetValue(copy, array.Clone());
        return copy;
    }
}

internal readonly record struct RoadHit(int Road, double Dist, double Lat, double S, double H, double Bank, bool Bridge, double Kappa, bool Deck, bool Landing = false, bool UnderDeck = false,
    bool Jump = false, bool Gap = false, bool Lip = false, bool Void = false, bool Raised = false, double Cross = 0, double Half = 0, bool Arc = false);

// A road-over-road bridge: the straighter road's own flat deck core (island cell coordinates), oriented along its own heading.
// RailBehind, RailAhead: whether the railings run on along the landing behind the deck's start and past its end (the way DirX, DirZ
// points) -- not where the road turns off the bridge's line there (the lava lake's bridge runs straight into a corner).
internal readonly record struct RoadBridgeInfo(double X, double Z, double Height, double DirX, double DirZ, double Width, double Length, int UnderRoad,
    bool RailBehind = true, bool RailAhead = true);

internal static class RaceTrackBuilder
{
    private const int Grid = IslandFile.GridSize;
    private static int PitMiddle;

    public static RaceTrackReport Build(IslandFile island, RaceTrackPlan plan, RaceTrackOptions options)
    {
        var report = new RaceTrackReport();
        var field = new Field(island);
        var roads = new List<TrackRoad>();
        // A plan with its own heights (Mosquibees Island's mountain lap) is built as it is drawn: its bridge and its jump are where the plan
        // puts them, so no crossing is re-shaped, and its heights are limited to the plan's own steepest grade.
        var planned = plan.Planned;
        if (planned) plan.ApplyTo(options);
        if (planned && plan.MaxGrade is { } planGrade && planGrade > options.MaxGrade) { options = options.Copy(); options.MaxGrade = planGrade; }

        var main = MakeRoad("lap", plan, options, closed: !plan.Open);
        report.Open = plan.Open;
        roads.Add(main);
        double EdgeDistance(double x, double z) => DistanceToMissingCube(island, x, z);
        KeepOnIsland(main, EdgeDistance, options, report);
        if (plan.DeckLeast is { } least && least > options.RoadBridgeLeast) { options = options.Copy(); options.RoadBridgeLeast = least; }
        var keptBoxes = plan.KeepBodies is { Length: > 0 } kb ? KeptBoxes(island, kb) : new();
        if (plan.KeepClear && keptBoxes.Count > 0) KeepClear(main, keptBoxes, EdgeDistance, options, report);
        if (planned && plan.Raised is { Length: >= 2 } raisedStretches && (raisedStretches.Length == 2 || plan.Open && raisedStretches.Length % 2 == 0))
        {
            main.Raised = new bool[main.Count];
            for (var s = 0; s + 1 < raisedStretches.Length; s += 2)
            {
                int a = PlanPoint(plan, main, raisedStretches[s]), b = PlanPoint(plan, main, raisedStretches[s + 1]);
                for (var k = a; ; k = At(main, k + 1)) { main.Raised[k] = true; if (k == b || !main.Closed && k == main.Count - 1) break; }
            }
            // (no banking on the ground such a lap has: its ends meet the raised road, which is level across, and a bend of the raised
            // road just beyond an end -- Celebration Island's hairpin onto the dock -- tilted the dock's first cells 70 a cell)
            options = options.Copy(); options.BankGain = 0;
        }
        if (planned) { }
        else if (options.Crossing == CrossingStyle.Jump)
        {
            var drawn = main.Clone();
            var angle = JumpAngle(main, options, report);
            SteepenCrossing(main, options, report, angle, JumpStraight(options, angle));
            SquareOtherRoad(drawn, main, options, report);
        }
        else if (options.Crossing == CrossingStyle.Bridge)
        {
            // straighten far enough that the whole deck, its landings AND the ramp mouths lie on the straightened line. One deck over
            // several crossings (BridgeSpan) straightens the road it carries through all of them, along that road's own heading there;
            // one crossing turns the straighter road to BridgeCrossingAngle as it always has.
            var drawnCrossings = FindCrossings(main, options);
            var span = drawnCrossings.Count > 1 ? BridgeSpan(main, drawnCrossings, options) : null;
            if (span is { Count: > 1 })
            {
                var (s0, half, _) = SpanOf(main, span, options);
                var pivot = main.Count > 0 ? Enumerable.Range(0, main.Count).MinBy(k => Math.Abs(Along(main, main.S[k] - s0))) : 0;
                var reach = half + options.RoadBridgeLanding;
                int PointAt(double ds) => Enumerable.Range(0, main.Count).MinBy(k => Math.Abs(Along(main, main.S[k] - (s0 + ds))));
                var a = PointAt(-reach); var b = PointAt(reach);
                double nx = main.X[b] - main.X[a], nz = main.Z[b] - main.Z[a]; var nl = Math.Sqrt(nx * nx + nz * nz); nx /= nl; nz /= nl;
                // (a straight line cannot bend round the island's edge: where it would run within IslandEdgeMargin of a cube the island
                // hasn't got -- the engine's open sea -- the whole line moves sideways, staying straight, until it doesn't)
                var straightHalf = reach + 1 + SpanSlack;
                var shift = StraightShift(main.X[pivot], main.Z[pivot], nx, nz, straightHalf, EdgeDistance, options);
                if (Math.Abs(shift) > 0.01) report.Notes.Add($"the bridge's straight moved {Math.Abs(shift):0.0} cells {(shift > 0 ? "left" : "right")} to keep off the island's edge");
                SteepenCrossing(main, options, report, 0, straightHalf, at: (pivot, nx, nz, -nz * shift, nx * shift));
            }
            else
                SteepenCrossing(main, options, report, options.BridgeCrossingAngle, Math.Max(15, DeckHalfCells(options, options.BridgeCrossingAngle) + options.RoadBridgeLanding + 1));
        }
        report.Length = main.Length; // as built: re-shaping the crossing shortens the lap a little
        if (planned) PlannedProfile(main, field, options, report); else Profile(main, field, options, report);
        var crossings = FindCrossings(main, options);
        if (crossings.Count > 1 && !planned)
        {
            // several crossings close together on the same road are one bridge (a loop that dips under the same road twice: Citadel Island's
            // town circuit does it); any others really are a fault in the plan
            var spanned = options.Crossing == CrossingStyle.Bridge ? BridgeSpan(main, crossings, options).Count : 1;
            if (spanned > 1) report.Notes.Add($"the road crosses over the rest of the lap {spanned} times within one deck's length: one bridge spans them all");
            if (crossings.Count > spanned)
                report.Notes.Add($"WARNING: the route crosses itself {crossings.Count} times, not {(spanned > 1 ? spanned + " times" : "once")} -- only {(spanned > 1 ? "the first " + spanned + " are" : "the first is")} bridged; check the plan, the others will paint as plain overlapping road.");
        }
        if (options.Crossing != CrossingStyle.Bridge && !planned) EqualiseCrossings(main, crossings, options);
        foreach (var c in crossings) report.Crossings.Add((c.X, c.Z, main.H[c.I], c.Angle));
        if (crossings.Count > 1 && Environment.GetEnvironmentVariable("RT_XDEBUG") == "1")
            foreach (var c in crossings)
            {
                var (ov, un) = OverUnder(main, c, options);
                report.Notes.Add($"  crossing detail: I {c.I} (s {main.S[c.I]:0.0}) J {c.J} (s {main.S[c.J]:0.0}), over {ov} (s {main.S[ov]:0.0}), under {un}, angle {c.Angle:0}");
            }
        report.Notes.Add("tightest turns: " + string.Join(", ", Tightest(main, options, 3).Select(t => $"radius {t.Radius:0.0} cells at cell ({t.X:0.0}, {t.Z:0.0})")));
        if (planned)
        {
            // (where the lap crosses itself inside a gap jump's gap, nothing carries one road over the other: the cars fly over it)
            var gaps = plan.AllGapJumps.Select(j => (Lip: PlanPoint(plan, main, j[0]), Landing: PlanPoint(plan, main, j[1]))).ToList();
            bool InGap(int k) => gaps.Any(g => Within(main, g.Lip, g.Landing, k));
            var carried = crossings.Where(c => !InGap(c.I) && !InGap(c.J)).ToList();
            if (crossings.Count > carried.Count) report.Notes.Add($"the lap crosses itself {crossings.Count - carried.Count} times in a jump's gap: the cars fly over it there");
            if (plan.Deck is [var d0, var d1]) report.RoadBridge = PlanDeck(main, PlanPoint(plan, main, d0), PlanPoint(plan, main, d1), carried, options, report);
            else if (carried.Count > 0 && main.Raised is null) report.Notes.Add($"WARNING: the lap crosses itself {carried.Count} times and the plan draws no bridge: the roads meet there");
            else if (carried.Count > 0) report.Notes.Add($"the lap passes over itself {carried.Count} times, on its raised road");
            if (gaps.Count > RaceTrackJumpAnim.MaxJumps) report.Notes.Add($"WARNING: the plan has {gaps.Count} jumps; only the first {RaceTrackJumpAnim.MaxJumps} are built");
            for (var k = 0; k < Math.Min(gaps.Count, RaceTrackJumpAnim.MaxJumps); k++)
                if (PlanGapJump(main, gaps[k].Lip, gaps[k].Landing, options, report, k) is { } gapJump) report.Jumps.Add(gapJump);
        }
        else
        {
            if (options.Crossing == CrossingStyle.Jump && crossings.Count > 0 && PlanJump(main, crossings[0], options, report) is { } jump) report.Jumps.Add(jump);
            if (options.Crossing == CrossingStyle.Bridge && crossings.Count > 0) report.RoadBridge = PlanRoadBridge(main, BridgeSpan(main, crossings, options), options, report);
        }
        foreach (var (a, b) in report.BridgeSpans) report.BridgeCoords.Add((main.X[a], main.Z[a], main.X[b], main.Z[b]));
        // (the raised road's colours, stretch by stretch)
        if (planned && plan.Themes is { Length: > 0 } themes)
        {
            main.Themes = new DeckTheme?[main.Count];
            foreach (var t in themes)
            {
                if (DeckTheme.ByName(t.Theme) is not { } theme) { report.Notes.Add($"WARNING: no deck theme \"{t.Theme}\""); continue; }
                int a = PlanPoint(plan, main, t.From), b = PlanPoint(plan, main, t.To);
                for (var k = a; ; k = (k + 1) % main.Count) { main.Themes[k] = theme; if (k == b) break; }
            }
            report.Notes.Add($"the raised road's colours: {string.Join(", ", themes.Select(t => $"{t.Theme} from the plan's point {t.From} to {t.To}"))}");
        }
        if (planned)
            foreach (var l in plan.Loops ?? Array.Empty<double[]>())
            {
                if (l.Length < 4) continue;
                var k = PlanPoint(plan, main, (int)l[0]);
                report.Loops.Add(new LoopInfo(main.X[k], main.Z[k], main.H[k], main.Tx[k], main.Tz[k], l[1], l[2], l[3], k, l.Length > 4 ? l[4] : LoopBandHalf));
            }
        if (planned && main.Raised is not null) PlanArcJumps(plan, main, report);
        if (planned && main.Raised is not null && plan.PitStripe is [var stripeFrom, var stripeTo, var stripeAt])
            main.Stripe = (PlanPoint(plan, main, (int)stripeFrom), PlanPoint(plan, main, (int)stripeTo), stripeAt);

        if (plan.PitA is { } pa && plan.PitB is { } pb) roads.Add(MakePit(main, plan, pa, pb, options, report));

        // (a raised road as wide as there is room for, where the plan doesn't say how wide: overtaking room on the Elevator Platform's lap)
        if (planned && main.Raised is not null && plan.RaisedHalfs is null) WidenRaised(island, main, plan, options, report);
        var index = new RoadIndex(roads);
        report.DistanceToRoad = (x, z) => { var h = index.Near(x, z, 14, 1); return h.Count == 0 ? 1e9 : h[0].Dist; };
        var startIndex = roads.Count > 1 ? PitMiddle : planned && plan.Start is { } startPoint ? PlanPoint(plan, main, startPoint) : -1;
        ClearOldTrack(island, options, report);
        DropDecors(island, options, report);
        var adrift = AdriftDecors(island);
        var follow = new IslandOps.DecorFollow(island);
        var keepBodies = plan.KeepBodies;
        // (the huts the road drives through, made after the ground's followers are counted: their parts keep their heights -- the road's
        // middle, where the ground is cut, is their origin -- and kept by the clearing)
        var through = new HashSet<int>();
        if (plan.DriveThrough is { Length: > 0 } huts) { through.UnionWith(RaceTrackDriveThrough.Make(island, main, huts, options, report)); keepBodies = (keepBodies ?? Array.Empty<int>()).Concat(through).ToArray(); }
        ClearDecors(island, index, options, report, roads, keepBodies, plan.KeepAbove, through);
        // (a kept building made shallower has bodies of its own, kept as the ones they copy; and so has a piece cut down to a wall)
        if (plan.ThinKept is { Length: > 0 } thin) { keepBodies = (keepBodies ?? Array.Empty<int>()).Concat(ThinKept(island, thin, options, report)).ToArray(); keptBoxes = KeptBoxes(island, keepBodies); }
        if (plan.TrimKept && keepBodies is { Length: > 0 }) { keepBodies = keepBodies.Concat(TrimKept(island, roads, keepBodies, options, report)).ToArray(); keptBoxes = KeptBoxes(island, keepBodies); }
        var natural = new Field(island);
        ModifyGround(island, field, index, roads, options, report, plan.KeepGroundUnder ? KeptGround(island, keptBoxes) : null);
        if (main.Raised is not null && plan.RaisedCut) CutUnderRaised(island, field, main, options, report, plan.KeepGroundUnder ? KeptGround(island, keptBoxes) : null);
        var painted = PaintRoad(island, field, index, roads, options, report, startIndex, planned);
        report.LapLine = LapLine(roads, report);
        report.LapX = (double[])main.X.Clone(); report.LapZ = (double[])main.Z.Clone();
        report.LapY = (double[])main.H.Clone(); report.LapRaised = (bool[]?)main.Raised?.Clone(); report.LapArc = (bool[]?)main.Arc?.Clone();
        if (!plan.Open) PlaceCheckpoints(roads, report, options, planned);
        else if (plan.Finish is { } finish) FinishLine(main, PlanPoint(plan, main, finish), options, report);
        PlacePits(roads, report, options, planned);
        foreach (var mine in plan.Mines ?? Array.Empty<double[]>())
            if (mine.Length >= 2) report.Mines.Add((mine[0] + plan.OriginCellX, mine[1] + plan.OriginCellZ));
        if (report.Mines.Count > 0) report.Notes.Add($"{report.Mines.Count} land mines off the road (the plan's)");
        if (report.Pits.Count == 0 && plan.PitSpots is { Length: > 0 } spots)
        {
            foreach (var p in spots.Where(p => p.Length >= 4))
            {
                double px = p[0] + plan.OriginCellX, pz = p[1] + plan.OriginCellZ;
                report.Pits.Add((px, pz, p.Length > 4 ? p[4] : IslandOps.Altitude(island, px * 512, pz * 512) ?? 0, p[2], p[3]));
            }
            report.PitHeights = spots.All(p => p.Length > 4);
            report.Notes.Add($"the pits: {report.Pits.Count} waiting spots beside the road (the plan's own: this lap has no pit lane)");
        }
        report.Roads.AddRange(roads);
        report.Gravity = planned && main.Raised is not null ? plan.Gravity : null;
        report.RailCamera = planned && main.Raised is not null && plan.RailCamera is { Length: 3 } ? plan.RailCamera : null;
        if (main.Raised is not null) report.RaisedFloor = (x, z, near) => RaisedFloor(main, options, x, z, near);
        // each driver's racing line (with none, the retail racer's: the test pilot and the rescue go by it)
        var drivers = options.Racers();
        foreach (var d in drivers.Count > 0 ? drivers : new List<RaceDriver> { RaceDriver.Racer })
        {
            var line = new List<(double X, double Z, double Y, double Speed, double Radius)>();
            PlanRacePath(main, report, options, d.Line, line, $"{d.Name}'s line");
            if (report.Loops.Count > 0) LaneIntoLoops(report, line);
            report.DriverPaths.Add(line);
        }
        report.RacePath.AddRange(report.DriverPaths[0]);
        ClearStaleCol(island, natural, field, index, painted, report);
        if (planned) WallSteepBanks(island, natural, field, index, painted, options, report);
        follow.Apply();
        // (and what the ground carried up into the road's way: the decors stood clear of it when they were cleared, and ground filled
        // under a raised road's end lifts what stood there -- 2026-10-06: two of LBA1's barrels at the foot of Polar Island's 108, lifted
        // 7,000 onto the start of the raised road round the rocky peak)
        ClearDecors(island, index, options, report, roads, keepBodies, plan.KeepAbove, through);
        ClearAdrift(island, adrift, report, through);
        // (the pipes after the clearing: they stand over the road, out of its way)
        // (the steam jets out of the gantries' uprights where there are some: with them)
        var jetRuns = planned && plan.SteamJets is { Length: > 0 } jets
            ? jets.Select(j => (PlanPoint(plan, main, j.From), PlanPoint(plan, main, j.To), j)).ToList() : new List<(int, int, SteamJetRun)>();
        if (planned && plan.Pipes is { Length: > 0 } pipes)
            RaceTrackPipes.Place(island, main, pipes.Select(p => (PlanPoint(plan, main, p.From), PlanPoint(plan, main, p.To), p)).ToList(), jetRuns, options, report);
        else if (jetRuns.Count > 0)
            RaceTrackPipes.PlaceJets(main, jetRuns, options, report);
        if (planned && plan.Pipeline is { } pipeline)
            RaceTrackPipes.PlacePipeline(island, main, pipeline, plan.OriginCellX, plan.OriginCellZ, options, report);
        report.GroundBefore = (x, z) => natural.Height(x, z);
        report.GroundAfter = (x, z) => IslandOps.Altitude(island, x * 512, z * 512) ?? field.Height(x, z);
        report.WasGround = (x, z) => natural.Drawn(x, z);
        PlaceStructures(island, main, crossings, startIndex, options, report, planned);
        Relight(island, index, options);
        if (planned) SeaEverywhere(island, report);
        return report;
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // the centre line

    private static TrackRoad MakeRoad(string name, RaceTrackPlan plan, RaceTrackOptions o, bool closed)
    {
        var raw = plan.Points.Select(p => (X: p[0] + plan.OriginCellX, Z: p[1] + plan.OriginCellZ)).ToList();
        var banked = plan.Planned && plan.Raised is not null && plan.Bank is { } bank && bank.Length == plan.Points.Length && bank.Any(b => b != 0);
        var widths = plan.Planned && plan.Raised is not null && plan.RaisedHalfs is { } rh && rh.Length == plan.Points.Length ? plan.RaisedHalfs : null;
        return Resample(name, raw, o, closed, plan.Planned ? plan.Heights : null, banked ? plan.Bank : null, widths);
    }

    // (with `heights`, one per raw point, the road's Planned heights are taken along with it; and its banking and raised widths)
    private static TrackRoad Resample(string name, List<(double X, double Z)> raw, RaceTrackOptions o, bool closed, IReadOnlyList<double>? heights = null,
        IReadOnlyList<double>? banks = null, IReadOnlyList<double>? halfs = null)
    {
        var pts = closed ? raw.Append(raw[0]).ToList() : raw;
        var hs = heights is null ? null : closed ? heights.Append(heights[0]).ToList() : heights.ToList();
        var bs = banks is null ? null : closed ? banks.Append(banks[0]).ToList() : banks.ToList();
        var ws = halfs is null ? null : closed ? halfs.Append(halfs[0]).ToList() : halfs.ToList();
        var cum = new List<double> { 0 };
        for (var i = 1; i < pts.Count; i++) cum.Add(cum[^1] + Math.Sqrt(Sq(pts[i].X - pts[i - 1].X) + Sq(pts[i].Z - pts[i - 1].Z)));
        var total = cum[^1];
        var n = Math.Max(8, (int)Math.Round(total / o.Spacing));
        var count = closed ? n : n + 1;
        var road = new TrackRoad { Name = name, Closed = closed, X = new double[count], Z = new double[count], Length = total, Planned = hs is null ? null : new double[count],
            RoadBank = bs is null ? null : new double[count], RaisedHalfs = ws is null ? null : new double[count] };
        var j = 0;
        for (var i = 0; i < count; i++)
        {
            var d = closed ? total * i / n : total * i / n;
            while (j < pts.Count - 2 && cum[j + 1] < d) j++;
            var seg = cum[j + 1] - cum[j];
            var t = seg <= 1e-9 ? 0 : (d - cum[j]) / seg;
            road.X[i] = pts[j].X + (pts[j + 1].X - pts[j].X) * t;
            road.Z[i] = pts[j].Z + (pts[j + 1].Z - pts[j].Z) * t;
            if (hs is not null) road.Planned![i] = hs[j] + (hs[j + 1] - hs[j]) * t;
            if (bs is not null) road.RoadBank![i] = bs[j] + (bs[j + 1] - bs[j]) * t;
            if (ws is not null) road.RaisedHalfs![i] = ws[j] + (ws[j + 1] - ws[j]) * t;
        }
        road.AsphaltHalf = o.AsphaltHalfWidth; road.CurbHalf = o.CurbHalfWidth; road.VergeHalf = o.VergeHalfWidth; road.Blend = o.BlendWidth;
        Geometry(road, o);
        return road;
    }

    private static void Geometry(TrackRoad r, RaceTrackOptions o)
    {
        var n = r.Count;
        r.S = new double[n]; r.Tx = new double[n]; r.Tz = new double[n]; r.Kappa = new double[n];
        for (var i = 1; i < n; i++) r.S[i] = r.S[i - 1] + Math.Sqrt(Sq(r.X[i] - r.X[i - 1]) + Sq(r.Z[i] - r.Z[i - 1]));
        var total = r.Closed ? r.S[n - 1] + Math.Sqrt(Sq(r.X[0] - r.X[n - 1]) + Sq(r.Z[0] - r.Z[n - 1])) : r.S[n - 1];
        r.Length = total;
        var theta = new double[n];
        var w = Math.Max(1, (int)Math.Round(3 / o.Spacing));
        for (var i = 0; i < n; i++)
        {
            var a = At(r, i - w); var b = At(r, i + w);
            var dx = r.X[b] - r.X[a]; var dz = r.Z[b] - r.Z[a];
            if (!r.Closed && (i - w < 0 || i + w >= n)) { a = Math.Max(0, i - w); b = Math.Min(n - 1, i + w); dx = r.X[b] - r.X[a]; dz = r.Z[b] - r.Z[a]; }
            var len = Math.Sqrt(dx * dx + dz * dz) + 1e-12;
            r.Tx[i] = dx / len; r.Tz[i] = dz / len; theta[i] = Math.Atan2(dz, dx);
        }
        var k = new double[n];
        for (var i = 0; i < n; i++)
        {
            var a = At(r, i - w); var b = At(r, i + w);
            var d = theta[b] - theta[a];
            while (d > Math.PI) d -= 2 * Math.PI;
            while (d < -Math.PI) d += 2 * Math.PI;
            var ds = Math.Max(1e-6, Dist(r, a, b));
            k[i] = d / ds;
        }
        r.Kappa = Smooth(k, 4 / o.Spacing, r.Closed);
    }

    // The lap's `count` tightest turns, each at least 20 cells from the others: the radius of the circle through points 3 cells either
    // side of a point.
    private static List<(double Radius, double X, double Z)> Tightest(TrackRoad r, RaceTrackOptions o, int count)
    {
        var k = Math.Max(1, (int)Math.Round(3 / o.Spacing));
        var all = new List<(double Radius, double X, double Z, double S)>();
        for (var i = 0; i < r.Count; i++)
        {
            int a = At(r, i - k), c = At(r, i + k);
            if (!r.Closed && (i - k < 0 || i + k >= r.Count)) continue;
            double abx = r.X[i] - r.X[a], abz = r.Z[i] - r.Z[a], acx = r.X[c] - r.X[a], acz = r.Z[c] - r.Z[a];
            var area2 = Math.Abs(abx * acz - abz * acx);
            if (area2 < 1e-9) continue;
            all.Add((Math.Sqrt(abx * abx + abz * abz) * Math.Sqrt(acx * acx + acz * acz) * Math.Sqrt(Sq(r.X[c] - r.X[i]) + Sq(r.Z[c] - r.Z[i])) / (2 * area2), r.X[i], r.Z[i], r.S[i]));
        }
        var picked = new List<(double Radius, double X, double Z, double S)>();
        foreach (var t in all.OrderBy(t => t.Radius))
        {
            if (picked.Any(p => Math.Min(Math.Abs(p.S - t.S), r.Length - Math.Abs(p.S - t.S)) < 20)) continue;
            picked.Add(t);
            if (picked.Count == count) break;
        }
        return picked.Select(t => (t.Radius, t.X, t.Z)).ToList();
    }

    private static int At(TrackRoad r, int i) => r.Closed ? ((i % r.Count) + r.Count) % r.Count : Math.Clamp(i, 0, r.Count - 1);

    // Whether point k is on the stretch of the lap from point a forward to point b.
    private static bool Within(TrackRoad r, int a, int b, int k)
    {
        if (!r.Closed) return k >= Math.Min(a, b) && k <= Math.Max(a, b);
        var span = ((b - a) % r.Count + r.Count) % r.Count;
        return ((k - a) % r.Count + r.Count) % r.Count <= span;
    }

    // Whether point k is on the stretch of the lap a jump's flight is over (JumpInfo.S0..S1), give or take `margin` cells.
    private static bool OnFlight(TrackRoad r, JumpInfo j, int k, double margin = 0)
    {
        double Fwd(double from, double to) { var d = (to - from) % r.Length; return d < 0 ? d + r.Length : d; }
        var span = Fwd(j.S0, j.S1) + 2 * margin;
        return Fwd(j.S0 - margin, r.S[k]) <= span;
    }

    // Along-the-road distance between two point indices (the short way round a closed road).
    private static double Dist(TrackRoad r, int a, int b)
    {
        var d = Math.Abs(r.S[b] - r.S[a]);
        return r.Closed ? Math.Min(d, r.Length - d) : d;
    }

    private static double[] Smooth(double[] v, double sigma, bool closed)
    {
        var n = v.Length; var radius = (int)Math.Ceiling(sigma * 3);
        var kernel = new double[radius * 2 + 1]; double sum = 0;
        for (var i = -radius; i <= radius; i++) { kernel[i + radius] = Math.Exp(-0.5 * i * i / (sigma * sigma)); sum += kernel[i + radius]; }
        var result = new double[n];
        for (var i = 0; i < n; i++)
        {
            double acc = 0, wsum = 0;
            for (var k = -radius; k <= radius; k++)
            {
                var j = i + k;
                if (closed) j = ((j % n) + n) % n; else if (j < 0 || j >= n) continue;
                acc += v[j] * kernel[k + radius]; wsum += kernel[k + radius];
            }
            result[i] = acc / wsum;
        }
        return result;
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // the height profile along the road

    private static void Profile(TrackRoad r, Field field, RaceTrackOptions o, RaceTrackReport report)
    {
        var n = r.Count;
        var h = new double[n]; var sea = new bool[n];
        for (var i = 0; i < n; i++)
        {
            sea[i] = !field.Drawn(r.X[i], r.Z[i]);
            h[i] = field.Height(r.X[i], r.Z[i]);
        }
        // water crossings: merge short gaps, then add the approach on either side
        var bridge = new bool[n];
        var i0 = 0;
        while (i0 < n)
        {
            if (!sea[i0]) { i0++; continue; }
            var i1 = i0; var gap = 0;
            for (var k = i0; k < n; k++)
            {
                if (sea[k]) { i1 = k; gap = 0; } else if (++gap > 8 / o.Spacing) break;
            }
            var approach = (int)(5 / o.Spacing);
            var a = i0 - approach; var b = i1 + approach;
            for (var k = a; k <= b; k++) bridge[At(r, k)] = true;
            report.BridgeSpans.Add((At(r, a), At(r, b)));
            i0 = i1 + 1;
        }
        // the ground under a bridge is the sea: replace it by a straight line between the two shores
        for (var i = 0; i < n; i++)
        {
            if (!bridge[i]) continue;
            var a = i; while (bridge[At(r, a)] && a > i - n) a--;
            var b = i; while (bridge[At(r, b)] && b < i + n) b++;
            var ha = h[At(r, a)]; var hb = h[At(r, b)];
            var t = (double)(i - a) / Math.Max(1, b - a);
            h[i] = ha + (hb - ha) * t;
        }
        var smooth = Smooth(h, o.ProfileSmoothing / o.Spacing, r.Closed);
        for (var i = 0; i < n; i++) if (bridge[i]) smooth[i] = Math.Max(smooth[i], o.BridgeClearance);
        r.H = smooth; r.Bridge = bridge; r.Deck = new bool[n]; r.Landing = new bool[n]; r.UnderDeck = new bool[n]; r.Jump = new bool[n]; r.Gap = new bool[n]; r.Lip = new bool[n];
        LimitGrade(r, o);
        // round the crests and dips the limit leaves (a grade that changes all at once pitches the car all at once); smoothing a
        // profile never makes it steeper, so the grade limit still holds
        r.H = Smooth(r.H, o.VerticalSmoothing / o.Spacing, r.Closed);
        // water bridges keep their clearance: raised where they fell short, and the approaches filled up to meet them at the limit
        for (var i = 0; i < n; i++) if (bridge[i] && r.H[i] < o.BridgeClearance) r.H[i] = o.BridgeClearance;
        r.H = Envelope(r, o.MaxGrade, below: false);
    }

    // The profile with no stretch steeper than MaxGrade that is closest to the road's own: the average of its two grade-limited
    // envelopes -- the highest profile that nowhere rises above it (cut only) and the lowest that nowhere drops below it (fill
    // only). Both climb at most MaxGrade, so their average does too, and it sits halfway between the cut and the fill everywhere.
    // Exact in one go: the pairwise relaxation this replaces stopped after 60 passes, which on a hilly stretch (the retail
    // track's bed) left climbs of 23% against a 9% limit -- converging would have taken tens of thousands.
    private static void LimitGrade(TrackRoad r, RaceTrackOptions o)
    {
        var cut = Envelope(r, o.MaxGrade, below: true);
        var fill = Envelope(r, o.MaxGrade, below: false);
        for (var i = 0; i < r.Count; i++) r.H[i] = (cut[i] + fill[i]) / 2;
    }

    // below: the highest profile at or under H whose grade nowhere exceeds `grade` (H[i] = min over j of H[j] + grade * distance);
    // otherwise the lowest at or over it (max over j of H[j] - grade * distance). Two sweeps each way (two laps round a closed road,
    // so the limit carries across the start).
    private static double[] Envelope(TrackRoad r, double grade, bool below)
    {
        var n = r.Count; var e = (double[])r.H.Clone();
        var rise = new double[n];   // the most the height may change from point i to point i + 1
        for (var i = 0; i < n; i++)
        {
            var j = r.Closed ? (i + 1) % n : Math.Min(i + 1, n - 1);
            rise[i] = Math.Sqrt(Sq(r.X[j] - r.X[i]) + Sq(r.Z[j] - r.Z[i])) * 512 * grade;
            // (a raised road is as steep as its plan draws it)
            if (r.Raised is { } raised && (raised[i] || raised[j])) rise[i] = 1e12;
        }
        void Relax(int i, int from, double limit) => e[i] = below ? Math.Min(e[i], e[from] + limit) : Math.Max(e[i], e[from] - limit);
        var sweep = r.Closed ? 2 * n : n;
        for (var k = 1; k < sweep; k++) Relax(k % n, (k - 1) % n, rise[(k - 1) % n]);
        for (var k = sweep - 2; k >= 0; k--) Relax(k % n, (k + 1) % n, rise[k % n]);
        return e;
    }

    // A planned profile (RaceTrackPlan.Heights): the plan's own heights, raised to BridgeClearance over the sea and grade-limited to
    // MaxGrade (a plan that keeps to its own grade comes out as it is). Where the road stands well clear of the ground -- built up over
    // the sea or a valley, or cut deep into a mountainside -- it is walled like the water bridge (Bridge: a level top CurbHalf +
    // RampShoulder cells each side, blocking rock at its edges, no banking) instead of the sand verge and 7-cell earth skirt, which spilled
    // a slope of fill across whatever lies below it: the sea, or the road on the next terrace of the mountain. Short breaks between walled
    // stretches are walled too, and each reaches a few cells further, so the walls don't come and go along the road.
    private const double PlannedWallHeight = 1200, PlannedWallJoin = 12, PlannedWallGrow = 4;

    private static void PlannedProfile(TrackRoad r, Field field, RaceTrackOptions o, RaceTrackReport report)
    {
        var n = r.Count;
        var h = (double[])r.Planned!.Clone();
        var walled = new bool[n];
        var sea = 0;
        var up = r.Raised ?? new bool[n];
        for (var i = 0; i < n; i++)
        {
            if (up[i]) continue;
            var over = !field.Drawn(r.X[i], r.Z[i]);
            if (over) { h[i] = Math.Max(h[i], o.BridgeClearance); sea++; }
            walled[i] = over || Math.Abs(h[i] - field.Height(r.X[i], r.Z[i])) > PlannedWallHeight;
        }
        walled = Grow(Close(walled, (int)Math.Round(PlannedWallJoin / o.Spacing), r.Closed), (int)Math.Round(PlannedWallGrow / o.Spacing), r.Closed);
        r.H = h; r.Bridge = walled; r.Deck = new bool[n]; r.Landing = new bool[n]; r.UnderDeck = new bool[n];
        r.Jump = new bool[n]; r.Gap = new bool[n]; r.Lip = new bool[n]; r.Void = new bool[n];
        // the raised road: not the ground's (Deck), and no wall beside it
        for (var i = 0; i < n; i++) if (up[i]) { r.Deck[i] = true; r.Bridge[i] = false; }
        var before = (double[])h.Clone();
        LimitGrade(r, o);
        for (var i = 0; i < n; i++) if (up[i]) r.H[i] = before[i];
        var moved = Enumerable.Range(0, n).Max(i => Math.Abs(r.H[i] - before[i]));
        double Grade(int i) { var j = At(r, i + 1); return Math.Abs(r.H[j] - r.H[i]) / Math.Max(1e-6, Math.Sqrt(Sq(r.X[j] - r.X[i]) + Sq(r.Z[j] - r.Z[i])) * 512); }
        var steepest = Enumerable.Range(0, n).Where(i => !up[i] && !up[At(r, i + 1)]).Select(Grade).DefaultIfEmpty(0).Max();
        if (r.Raised is not null)
            report.Notes.Add($"the raised road: {up.Count(u => u) * o.Spacing:0} cells on piers, from {Enumerable.Range(0, n).Where(i => up[i]).Min(i => r.H[i]):0} to {Enumerable.Range(0, n).Where(i => up[i]).Max(i => r.H[i]):0}, " +
                             $"steepest {Enumerable.Range(0, n).Where(i => up[i] && up[At(r, i + 1)]).Max(Grade) * 100:0} % (its own plan's: the ground's grade limit is not its)");
        report.Notes.Add($"planned heights {r.H.Min():0} to {r.H.Max():0}, steepest grade {steepest * 100:0.0} % (limit {o.MaxGrade * 100:0.#} %, the limit moved them by up to {moved:0}); " +
                         $"{walled.Count(w => w) * o.Spacing:0} cells of the lap walled (built up or cut down more than {PlannedWallHeight:0} from the ground, or over the sea: {sea * o.Spacing:0} cells)");
    }

    // A mask with its short false runs (up to `gap` points, between two true ones) filled, and one grown by `grow` points each way.
    private static bool[] Close(bool[] mask, int gap, bool closed)
    {
        var n = mask.Length; var result = (bool[])mask.Clone();
        if (!mask.Any(m => m)) return result;
        for (var i = 0; i < n; i++)
        {
            if (!mask[i] || mask[(i + 1) % n] || (!closed && i == n - 1)) continue;
            var run = 0;
            while (run <= gap && (closed || i + 1 + run < n) && !mask[(i + 1 + run) % n]) run++;
            if (run <= gap && (closed || i + 1 + run < n)) for (var k = 1; k <= run; k++) result[(i + k) % n] = true;
        }
        return result;
    }

    private static bool[] Grow(bool[] mask, int grow, bool closed)
    {
        var n = mask.Length; var result = (bool[])mask.Clone();
        for (var i = 0; i < n; i++)
        {
            if (!mask[i]) continue;
            for (var k = -grow; k <= grow; k++)
            {
                var j = i + k;
                if (closed) j = ((j % n) + n) % n; else if (j < 0 || j >= n) continue;
                result[j] = true;
            }
        }
        return result;
    }

    // The lap's point for a plan point: the nearest of the lap's points to it, looked for around where it falls along the lap (the lap is
    // the plan resampled, so it is near the same share of the way round -- and a point near another part of the lap, under a bridge or
    // on the next terrace, is not taken for that part's).
    private static int PlanPoint(RaceTrackPlan plan, TrackRoad r, int p)
    {
        p = Math.Clamp(p, 0, plan.Points.Length - 1);
        var x = plan.Points[p][0] + plan.OriginCellX; var z = plan.Points[p][1] + plan.OriginCellZ;
        var guess = (int)Math.Round((double)p / plan.Points.Length * r.Count);
        var window = Math.Max(20, r.Count / 20);
        var best = At(r, guess); var bd = double.MaxValue;
        for (var k = -window; k <= window; k++) { var i = At(r, guess + k); var d = Sq(r.X[i] - x) + Sq(r.Z[i] - z); if (d < bd) { bd = d; best = i; } }
        return best;
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // the pit lane: a short road beside the lap, tapering onto it at both ends

    private static TrackRoad MakePit(TrackRoad main, RaceTrackPlan plan, double[] a, double[] b, RaceTrackOptions o, RaceTrackReport report)
    {
        var ax = a[0] + plan.OriginCellX; var az = a[1] + plan.OriginCellZ; var bx = b[0] + plan.OriginCellX; var bz = b[1] + plan.OriginCellZ;
        int Near(double x, double z) { var best = 0; var bd = double.MaxValue; for (var i = 0; i < main.Count; i++) { var d = Sq(main.X[i] - x) + Sq(main.Z[i] - z); if (d < bd) { bd = d; best = i; } } return best; }
        var ia = Near(ax, az); var ib = Near(bx, bz);
        // the way from a to b that follows the lap's own direction and is the shorter of the two
        var fwd = ((ib - ia) % main.Count + main.Count) % main.Count;
        var step = fwd <= main.Count / 2 ? 1 : -1;
        var count = step == 1 ? fwd : main.Count - fwd;
        var mid = At(main, ia + step * count / 2);
        // which side the pit lane's picture sits on
        var mx = (ax + bx) / 2; var mz = (az + bz) / 2;
        var side = Math.Sign(main.Tx[mid] * (mz - main.Z[mid]) - main.Tz[mid] * (mx - main.X[mid]));
        if (side == 0) side = 1;
        const double offset = 10.5, halfAsphalt = 2.5, halfCurb = 3.5;
        var taper = plan.PitTaper ?? 24;
        var pts = new List<(double X, double Z)>();
        var hs = new List<double>();
        for (var k = 0; k <= count; k++)
        {
            var i = At(main, ia + step * k);
            var fromEnd = Math.Min(k, count - k) * o.Spacing;
            var t = Math.Clamp(fromEnd / taper, 0, 1); var e = t * t * (3 - 2 * t);
            var nx = -main.Tz[i] * side; var nz = main.Tx[i] * side;      // toward the pit side
            pts.Add((main.X[i] + nx * offset * e, main.Z[i] + nz * offset * e));
            hs.Add(main.H[i]);
        }
        var pit = Resample("pit", pts, o, closed: false);
        pit.AsphaltHalf = halfAsphalt; pit.CurbHalf = halfCurb; pit.VergeHalf = halfCurb + 1.0; pit.Blend = 4;
        // heights: the lap's own height at the nearest point of the stretch the pit lane runs beside -- where the two merge they
        // are then one surface (taking the point a fixed fraction along instead put the merge several cells off, a 200-unit step
        // on the bridge's ramp)
        pit.H = new double[pit.Count]; pit.Bridge = new bool[pit.Count]; pit.Deck = new bool[pit.Count]; pit.Landing = new bool[pit.Count]; pit.UnderDeck = new bool[pit.Count]; pit.Jump = new bool[pit.Count]; pit.Gap = new bool[pit.Count]; pit.Lip = new bool[pit.Count];
        for (var i = 0; i < pit.Count; i++)
        {
            var best = 0; var bd = double.MaxValue;
            for (var k = 0; k <= count; k++)
            {
                var j = At(main, ia + step * k);
                var dd = Sq(main.X[j] - pit.X[i]) + Sq(main.Z[j] - pit.Z[i]);
                if (dd < bd) { bd = dd; best = k; }
            }
            pit.H[i] = hs[best];
        }
        PitMiddle = mid;
        report.Notes.Add($"pit lane: {pit.Length:0} cells long, {(side > 0 ? "left" : "right")} of the lap (points {ia} to {At(main, ia + step * count)}, from cell ({main.X[ia]:0.0}, {main.Z[ia]:0.0}) to ({main.X[At(main, ia + step * count)]:0.0}, {main.Z[At(main, ia + step * count)]:0.0}))");
        return pit;
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // finding the road near a place

    private sealed class RoadIndex
    {
        private const int Bucket = 4;
        private readonly Dictionary<(int, int), List<(int Road, int Index)>> grid = new();
        private readonly List<TrackRoad> roads;
        public (int X0, int Z0, int X1, int Z1) Bounds { get; }

        public RoadIndex(List<TrackRoad> roads)
        {
            this.roads = roads;
            double minX = 1e9, minZ = 1e9, maxX = -1e9, maxZ = -1e9;
            for (var ri = 0; ri < roads.Count; ri++)
            {
                var r = roads[ri];
                for (var i = 0; i < r.Count; i++)
                {
                    var key = ((int)Math.Floor(r.X[i] / Bucket), (int)Math.Floor(r.Z[i] / Bucket));
                    if (!grid.TryGetValue(key, out var list)) grid[key] = list = new();
                    list.Add((ri, i));
                    minX = Math.Min(minX, r.X[i]); maxX = Math.Max(maxX, r.X[i]); minZ = Math.Min(minZ, r.Z[i]); maxZ = Math.Max(maxZ, r.Z[i]);
                }
            }
            var pad = roads.Max(r => r.VergeHalf + r.Blend) + 2;
            Bounds = ((int)Math.Floor(minX - pad), (int)Math.Floor(minZ - pad), (int)Math.Ceiling(maxX + pad), (int)Math.Ceiling(maxZ + pad));
        }

        // The nearest road point of each different piece of road within `radius` (up to `max`), closest first.
        public List<RoadHit> Near(double x, double z, double radius, int max = 3)
        {
            var found = new List<(double D2, int Road, int Index)>();
            var b0 = (int)Math.Floor((x - radius) / Bucket); var b1 = (int)Math.Floor((x + radius) / Bucket);
            var c0 = (int)Math.Floor((z - radius) / Bucket); var c1 = (int)Math.Floor((z + radius) / Bucket);
            var r2 = radius * radius;
            for (var bz = c0; bz <= c1; bz++)
            for (var bx = b0; bx <= b1; bx++)
                if (grid.TryGetValue((bx, bz), out var list))
                    foreach (var (ri, i) in list)
                    {
                        var d2 = Sq(roads[ri].X[i] - x) + Sq(roads[ri].Z[i] - z);
                        if (d2 <= r2) found.Add((d2, ri, i));
                    }
            found.Sort((a, b) => a.D2.CompareTo(b.D2));
            var picked = new List<(int Road, int Index)>();
            var hits = new List<RoadHit>();
            foreach (var (_, ri, i) in found)
            {
                var road = roads[ri];
                if (picked.Any(p => p.Road == ri && Separation(road, p.Index, i) < 18)) continue;
                picked.Add((ri, i));
                hits.Add(Project(road, ri, i, x, z));
                if (hits.Count >= max) break;
            }
            return hits;
        }

        private static double Separation(TrackRoad r, int a, int b)
        {
            var d = Math.Abs(r.S[a] - r.S[b]);
            return r.Closed ? Math.Min(d, r.Length - d) : d;
        }

        private static RoadHit Project(TrackRoad r, int ri, int i, double x, double z)
        {
            // exact distance to the two segments around the point
            var best = new RoadHit(ri, double.MaxValue, 0, 0, 0, 0, false, 0, false);
            for (var side = -1; side <= 0; side++)
            {
                int a, b;
                if (r.Closed) { a = At(r, i + side); b = At(r, i + side + 1); }
                else { a = i + side; b = i + side + 1; if (a < 0 || b >= r.Count) continue; }
                var sx = r.X[b] - r.X[a]; var sz = r.Z[b] - r.Z[a];
                var len2 = sx * sx + sz * sz;
                var t = len2 < 1e-12 ? 0 : Math.Clamp(((x - r.X[a]) * sx + (z - r.Z[a]) * sz) / len2, 0, 1);
                var px = r.X[a] + sx * t; var pz = r.Z[a] + sz * t;
                var d = Math.Sqrt(Sq(x - px) + Sq(z - pz));
                if (d >= best.Dist) continue;
                var tx = r.Tx[a] * (1 - t) + r.Tx[b] * t; var tz = r.Tz[a] * (1 - t) + r.Tz[b] * t;
                var tl = Math.Sqrt(tx * tx + tz * tz) + 1e-12; tx /= tl; tz /= tl;
                var lat = tx * (z - pz) - tz * (x - px);
                var k = r.Kappa[a] * (1 - t) + r.Kappa[b] * t;
                var s = r.S[a] + Math.Sqrt(len2) * t;
                var h = r.H[a] * (1 - t) + r.H[b] * t;
                best = new RoadHit(ri, d, lat, s, h, 0, r.Bridge[a] && r.Bridge[b], k, r.Deck[a] && r.Deck[b], r.Landing[a] || r.Landing[b], r.UnderDeck[a] || r.UnderDeck[b],
                    r.Jump[a] || r.Jump[b], r.Gap[a] && r.Gap[b], t < 0.5 ? r.Lip[a] : r.Lip[b], r.Void.Length > 0 && r.Void[a] && r.Void[b],
                    r.Raised is { } up && up[a] && up[b], r.RoadBank is { } rb ? rb[a] * (1 - t) + rb[b] * t : 0,
                    r.RaisedHalfs is { } rh ? rh[a] * (1 - t) + rh[b] * t : 0, r.Arc is { } arc && arc[a] && arc[b]);
            }
            return best;
        }
    }

    // How far a point is from the road, for its shaping and painting: the true distance to the nearest segment, not the sideways
    // offset alone. Where the projection stops at a segment's end the sideways offset reads ~0 however far away the point lies:
    // past the pit lane's open ends (it claimed a whole 13-cell circle round each end at its own height, a cliff across the lap
    // where the circle stopped), and in tight bends, where the index's second hit on the same road -- a point 18 cells further
    // round the bend -- got full weight and pulled the surface 500 units out of shape.
    private static double Across(TrackRoad r, RoadHit hit) => Math.Max(Math.Abs(hit.Lat), hit.Dist);

    // An open road (the pit lane) merges into the lap at both ends, so near its ends it hands the ground over to the lap: its weight
    // fades from nothing at an end to full `PitFade` cells in.
    private const double PitFade = 8;
    private static double EndFade(TrackRoad r, RoadHit hit)
    {
        if (r.Closed) return 1;
        var t = Math.Clamp(Math.Min(hit.S, r.Length - hit.S) / PitFade, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double BankOf(TrackRoad r, RoadHit hit, RaceTrackOptions o)
        => hit.Bridge ? 0 : Math.Clamp(-hit.Kappa * o.BankGain, -o.MaxBank, o.MaxBank);

    // ---------------------------------------------------------------------------------------------------------------------
    // where the lap crosses itself

    private readonly record struct Crossing(int I, int J, double X, double Z, double Angle, double BisX, double BisZ);

    private static List<Crossing> FindCrossings(TrackRoad r, RaceTrackOptions o)
    {
        var found = new List<(int I, int J, double D)>();
        for (var i = 0; i < r.Count; i++)
        for (var j = i + 1; j < r.Count; j++)
        {
            var sep = Math.Abs(r.S[j] - r.S[i]); sep = Math.Min(sep, r.Length - sep);
            if (sep < 40) continue;
            var d = Math.Sqrt(Sq(r.X[i] - r.X[j]) + Sq(r.Z[i] - r.Z[j]));
            if (d < 1.5) found.Add((i, j, d));
        }
        var result = new List<Crossing>();
        foreach (var (i, j, _) in found.OrderBy(f => f.D))
        {
            var dot = r.Tx[i] * r.Tx[j] + r.Tz[i] * r.Tz[j];
            var angle = Math.Acos(Math.Clamp(Math.Abs(dot), 0, 1)) * 180 / Math.PI;
            // (a raised road's levels one over the other, running much the same way -- a helix, a road leaving it -- are not a crossing)
            if (r.Raised is not null && angle < 60) continue;
            if (result.Any(c => (Math.Abs(c.I - i) < 80 || Math.Abs(c.I - j) < 80) && (Math.Abs(c.J - j) < 80 || Math.Abs(c.J - i) < 80))) continue;
            var sign = dot >= 0 ? 1 : -1;
            var bx = r.Tx[i] + sign * r.Tx[j]; var bz = r.Tz[i] + sign * r.Tz[j];
            var bl = Math.Sqrt(bx * bx + bz * bz) + 1e-9;
            result.Add(new Crossing(i, j, (r.X[i] + r.X[j]) / 2, (r.Z[i] + r.Z[j]) / 2, angle, bx / bl, bz / bl));
        }
        return result;
    }

    // Both roads meet at the same height where they cross (the ground is one surface): pull the two profiles together over the
    // stretch where they share cells, then re-limit the grade.
    private static void EqualiseCrossings(TrackRoad r, List<Crossing> crossings, RaceTrackOptions o)
    {
        if (crossings.Count == 0) return;
        var pairs = new List<(int I, int J)>();
        for (var i = 0; i < r.Count; i++)
        for (var j = i + 1; j < r.Count; j++)
        {
            var sep = Math.Abs(r.S[j] - r.S[i]); sep = Math.Min(sep, r.Length - sep);
            if (sep < 40) continue;
            if (Sq(r.X[i] - r.X[j]) + Sq(r.Z[i] - r.Z[j]) < 7 * 7) pairs.Add((i, j));
        }
        for (var round = 0; round < 120; round++)
        {
            foreach (var (i, j) in pairs)
            {
                var m = (r.H[i] + r.H[j]) / 2;
                r.H[i] += (m - r.H[i]) * 0.5; r.H[j] += (m - r.H[j]) * 0.5;
            }
            var sm = Smooth(r.H, 2.0 / o.Spacing, r.Closed);
            for (var i = 0; i < r.Count; i++) r.H[i] = r.Bridge[i] ? r.H[i] : sm[i];
        }
        LimitGrade(r, o);
        for (var round = 0; round < 40; round++)
            foreach (var (i, j) in pairs) { var m = (r.H[i] + r.H[j]) / 2; r.H[i] = m; r.H[j] = m; }
    }

    // Turns the straighter of the two roads that cross so they meet at `angleDeg`, with a straight run of about 15 cells either side of
    // the crossing and a smooth blend back to the drawn course over the next 40. The concept picture's crossing is a clean 42-degree X,
    // but the extracted centre line collapsed it to a near-parallel 17 degrees, so both the jump and the bridge re-shape it here.
    // `at`: straighten the road through that point along that heading instead of turning a crossing to angleDeg (a bridge over several
    // crossings, where no one angle is to be set).
    private static void SteepenCrossing(TrackRoad r, RaceTrackOptions o, RaceTrackReport report, double angleDeg, double straightCells = 15, bool turnOther = false, double blendCells = 40,
        (int Pivot, double Nx, double Nz, double ShiftX, double ShiftZ)? at = null)
    {
        var found = FindCrossings(r, o);
        if (found.Count == 0) return;
        var c = found[0];
        int ia; double ax = 0, az = 0, nx, nz;
        if (at is { } given)
        {
            ia = given.Pivot; nx = given.Nx; nz = given.Nz;
            // (keep the lap running the way it does: the heading along the road's own direction there)
            if (nx * r.Tx[ia] + nz * r.Tz[ia] < 0) { nx = -nx; nz = -nz; }
            ax = r.Tx[ia]; az = r.Tz[ia];
        }
        else
        {
            double Bend(int i) { double sum = 0; var w = (int)(12 / o.Spacing); for (var k = -w; k <= w; k++) sum += Math.Abs(r.Kappa[At(r, i + k)]); return sum; }
            int ib;
            (ia, ib) = Bend(c.I) <= Bend(c.J) ? (c.I, c.J) : (c.J, c.I);
            if (turnOther) (ia, ib) = (ib, ia);
            double bx = 0, bz = 0; var span = (int)(6 / o.Spacing);
            for (var k = -span; k <= span; k++) { var a = At(r, ia + k); var b = At(r, ib + k); ax += r.Tx[a]; az += r.Tz[a]; bx += r.Tx[b]; bz += r.Tz[b]; }
            var al = Math.Sqrt(ax * ax + az * az); ax /= al; az /= al;
            var bl = Math.Sqrt(bx * bx + bz * bz); bx /= bl; bz /= bl;
            // the road B's direction oriented like A's, and how far A must turn away from it
            if (ax * bx + az * bz < 0) { bx = -bx; bz = -bz; }
            var current = Math.Atan2(ax * bz - az * bx, ax * bx + az * bz);           // signed angle from A to B
            var target = angleDeg * Math.PI / 180;
            // new direction of A: B's direction turned by +-target, on the side A already lies on
            var sign = current >= 0 ? -1.0 : 1.0;
            var na = Math.Atan2(bz, bx) + sign * target;
            nx = Math.Cos(na); nz = Math.Sin(na);
        }
        var px = r.X[ia] + (at?.ShiftX ?? 0); var pz = r.Z[ia] + (at?.ShiftZ ?? 0); var s0 = r.S[ia];
        var straight = straightCells; var blend = blendCells;
        var n = r.Count;
        double Ds(int k) { var ds = r.S[k] - s0; if (r.Closed) { if (ds > r.Length / 2) ds -= r.Length; else if (ds < -r.Length / 2) ds += r.Length; } return ds; }
        // the drawn road's points where the re-shaped stretch rejoins it, one blend length past each end of the straight
        int Join(double ds)
        {
            var best = ia; var bd = double.MaxValue; var reach = (int)((straight + blend) / o.Spacing) + 20;
            for (var k = -reach; k <= reach; k++) { var j = At(r, ia + k); var d = Math.Abs(Ds(j) - ds); if (d < bd) { bd = d; best = j; } }
            return best;
        }
        var kIn = Join(-(straight + blend)); var kOut = Join(straight + blend);
        // The new stretch: straight through the crossing, joined to the drawn road at both ends by a cubic Hermite curve that
        // matches the road's position and heading there; then the whole lap is resampled evenly. (Moving the old points towards
        // the straight line point by point bunched them up where the drawn road curved away: a 103-degree turn in 4 cells on
        // the bridge's south-east ramp, tighter than the car can drive.)
        var raw = new List<(double X, double Z)>();
        for (var k = kOut; k != kIn; k = (k + 1) % n) raw.Add((r.X[k], r.Z[k]));
        void Hermite(double x0, double z0, double tx0, double tz0, double x1, double z1, double tx1, double tz1)
        {
            var len = Math.Sqrt(Sq(x1 - x0) + Sq(z1 - z0));
            var steps = Math.Max(8, (int)Math.Ceiling(len / 0.25));
            for (var i = 0; i < steps; i++)
            {
                var t = (double)i / steps; var t2 = t * t; var t3 = t2 * t;
                double h00 = 2 * t3 - 3 * t2 + 1, h10 = t3 - 2 * t2 + t, h01 = -2 * t3 + 3 * t2, h11 = t3 - t2;
                raw.Add((h00 * x0 + h10 * tx0 * len + h01 * x1 + h11 * tx1 * len, h00 * z0 + h10 * tz0 * len + h01 * z1 + h11 * tz1 * len));
            }
        }
        double sx0 = px - nx * straight, sz0 = pz - nz * straight, sx1 = px + nx * straight, sz1 = pz + nz * straight;
        Hermite(r.X[kIn], r.Z[kIn], r.Tx[kIn], r.Tz[kIn], sx0, sz0, nx, nz);
        Hermite(sx0, sz0, nx, nz, sx1, sz1, nx, nz);
        Hermite(sx1, sz1, nx, nz, r.X[kOut], r.Z[kOut], r.Tx[kOut], r.Tz[kOut]);
        // the lap still starts where it did (the pit lane, arrows and start line are found from it)
        var first = raw.Select((p, i) => (I: i, D: Sq(p.X - r.X[0]) + Sq(p.Z - r.Z[0]))).MinBy(t => t.D).I;
        raw = raw.Skip(first).Concat(raw.Take(first)).ToList();
        var reshaped = Resample(r.Name, raw, o, closed: true);
        r.X = reshaped.X; r.Z = reshaped.Z;
        Geometry(r, o);
        var after = FindCrossings(r, o);
        if (at is not null)
            report.Notes.Add($"the road the bridge carries straightened over {straight * 2:0} cells through its {found.Count} crossings (now at {string.Join(", ", after.Select(a => $"{a.Angle:0}"))} degrees)");
        else
            report.Notes.Add($"crossing steepened from {c.Angle:0} to {(after.Count > 0 ? after[0].Angle : 0):0} degrees (the straighter road turned by {(Math.Atan2(nz, nx) - Math.Atan2(az, ax)) * 180 / Math.PI:0} degrees at the crossing)");
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // a physical, walkable road-over-road bridge (Citadel Island's own "Cliffs of the Woodbridge" does this with a plank decor;
    // see RaceTrackDeckBody). The straighter road climbs, on its own natural grade-limited profile, onto a flat deck carried by a
    // row of deck decors; the other road keeps its own profile untouched underneath. No terrain height is shared between them at
    // the crossing (unlike EqualiseCrossings, which is skipped for this style), so this needs no steepening of the crossing angle.

    // Half the flat deck's length along the upper road. The ramp must start where nothing of the lower road reaches any more:
    // the lower road's own shaping spreads VergeHalf + BlendWidth cells to each side of it, the ramp's rock-sided shoulders
    // another CurbHalf + ~2, plus RoadBridgeMargin -- measured along the upper road that's that distance / sin(crossing angle).
    // (With the deck any shorter the lower road's embankment pulled the first ramp cells ~1000 units under the deck: a car coming
    // up the ramp would have hit the deck's edge.)
    // Rounded up to whole deck tiles, so the deck laid by PlaceDeck ends exactly where the ground's own flat landing starts.
    private static double DeckHalfCells(RaceTrackOptions o, double angleDeg)
    {
        var clear = o.VergeHalfWidth + o.BlendWidth + o.CurbHalfWidth + 2 + o.RoadBridgeMargin;   // (the landings add another 4 cells of room)
        var half = Math.Clamp(clear / Math.Sin(Math.Max(angleDeg, 12) * Math.PI / 180), 8, 45);
        return Math.Ceiling(2 * half / o.RoadBridgeTileLength - 1e-9) * o.RoadBridgeTileLength / 2;
    }

    // How many cells of each deck end have ground shaped under them (to meet the landing without a crack), and how far below
    // the deck's top that ground sits (out of sight under the tiles, and not fighting their top faces for the same depth).
    private const double DeckEndOverlap = 1.5, UnderDeckDepth = 40;

    // Which road of a crossing carries the other: the straighter one over the bendier.
    private static (int Over, int Under) OverUnder(TrackRoad r, Crossing c, RaceTrackOptions o)
    {
        double Bend(int i) { double sum = 0; var w = (int)(12 / o.Spacing); for (var k = -w; k <= w; k++) sum += Math.Abs(r.Kappa[At(r, i + k)]); return sum; }
        return Bend(c.I) <= Bend(c.J) ? (c.I, c.J) : (c.J, c.I);
    }

    // The crossings one deck spans, each with the side of the road that goes over it. A stretch of road can cross the rest of the lap more
    // than once (Citadel Island's town circuit runs over a loop that passes under it twice); one deck then carries that stretch over them
    // all, as the plan draws it. Which of the first crossing's two sides is the carried one is decided by how many of the other crossings
    // lie on the same stretch -- not by which road is straighter, which can pick a different road at each crossing and carry neither.
    private static List<(Crossing C, int Over, int Under)> BridgeSpan(TrackRoad r, List<Crossing> crossings, RaceTrackOptions o)
    {
        var first = crossings[0];
        var reach = DeckHalfCells(o, Math.Max(first.Angle, 12)) * 2;
        List<(Crossing C, int Over, int Under)>? best = null;
        foreach (var (over, under) in new[] { (first.I, first.J), (first.J, first.I) })
        {
            var list = new List<(Crossing, int, int)> { (first, over, under) };
            foreach (var c in crossings.Skip(1))
            {
                double dI = Math.Abs(Along(r, r.S[c.I] - r.S[over])), dJ = Math.Abs(Along(r, r.S[c.J] - r.S[over]));
                if (Math.Min(dI, dJ) > reach) continue;
                list.Add(dI <= dJ ? (c, c.I, c.J) : (c, c.J, c.I));
            }
            if (best is null || list.Count > best.Count) best = list;
        }
        // one crossing to span: the straighter road goes over, as ever
        if (best!.Count == 1) { var (ov, un) = OverUnder(r, first, o); return new() { (first, ov, un) }; }
        return best;
    }

    // A distance along a closed road, taken the short way round.
    private static double Along(TrackRoad r, double ds)
    {
        if (!r.Closed) return ds;
        if (ds > r.Length / 2) ds -= r.Length;
        else if (ds < -r.Length / 2) ds += r.Length;
        return ds;
    }

    // ---- the island's edge ----
    // An island is a set of cubes; beyond one it hasn't got, the engine has no ground at all (a car that gets there is in NUM_CUBE_PHANTOM,
    // the open sea). A road is kept this far from such a cube, centre to edge: its verge and half a cell more.
    private static double IslandEdgeMargin(RaceTrackOptions o) => o.VergeHalfWidth + 0.5;

    // How far an island cell position is from the nearest cube the island hasn't got (or from the edge of the 16 x 16 map). Large when
    // there is none nearby.
    private static double DistanceToMissingCube(IslandFile island, double x, double z)
    {
        var best = 1e9;
        int cx = (int)Math.Floor(x / 64), cz = (int)Math.Floor(z / 64);
        for (var dz = -1; dz <= 1; dz++)
        for (var dx = -1; dx <= 1; dx++)
        {
            int nx = cx + dx, nz = cz + dz;
            var missing = nx < 0 || nz < 0 || nx >= 16 || nz >= 16 || island.CubeAt(nx, nz) is null;
            if (!missing) continue;
            // the distance from the point to that cube's square
            var qx = Math.Clamp(x, nx * 64.0, nx * 64.0 + 64); var qz = Math.Clamp(z, nz * 64.0, nz * 64.0 + 64);
            best = Math.Min(best, Math.Sqrt(Sq(x - qx) + Sq(z - qz)));
        }
        return best;
    }

    // Moves the lap's middle away from cubes the island hasn't got, where the drawn route runs so close that its road would reach one
    // (Citadel Island's circuit passes 2 cells from such a corner north of the bridge): each point too near is pushed straight away
    // from the edge by what it lacks, the pushes are smoothed along the road so it bends gently rather than kinks, and the lap is
    // resampled. Several rounds, as a push can bring a neighbour nearer another edge.
    private static void KeepOnIsland(TrackRoad r, Func<double, double, double> edge, RaceTrackOptions o, RaceTrackReport report)
    {
        var margin = IslandEdgeMargin(o);
        var n = r.Count;
        var worstBefore = Enumerable.Range(0, n).Min(i => edge(r.X[i], r.Z[i]));
        if (worstBefore >= margin) return;
        for (var round = 0; round < 6; round++)
        {
            var px = new double[n]; var pz = new double[n]; var any = false;
            for (var i = 0; i < n; i++)
            {
                var d = edge(r.X[i], r.Z[i]);
                if (d >= margin) continue;
                // away from the edge: along the distance's own gradient
                const double h = 0.25;
                var gx = edge(r.X[i] + h, r.Z[i]) - edge(r.X[i] - h, r.Z[i]); var gz = edge(r.X[i], r.Z[i] + h) - edge(r.X[i], r.Z[i] - h);
                var gl = Math.Sqrt(gx * gx + gz * gz);
                if (gl < 1e-9) continue;
                px[i] = gx / gl * (margin - d + 0.25); pz[i] = gz / gl * (margin - d + 0.25);
                any = true;
            }
            if (!any) break;
            // the pushes spread along the road (a bell a dozen cells wide), keeping the largest at each point so none is watered down
            var w = (int)Math.Round(12 / o.Spacing);
            var sx = new double[n]; var sz = new double[n];
            for (var i = 0; i < n; i++)
            {
                if (px[i] == 0 && pz[i] == 0) continue;
                for (var k = -w; k <= w; k++)
                {
                    var j = At(r, i + k); var f = 0.5 * (1 + Math.Cos(Math.PI * k / (w + 1)));
                    if (Math.Abs(px[i] * f) > Math.Abs(sx[j])) sx[j] = px[i] * f;
                    if (Math.Abs(pz[i] * f) > Math.Abs(sz[j])) sz[j] = pz[i] * f;
                }
            }
            var pts = new List<(double X, double Z)>();
            for (var i = 0; i < n; i++) pts.Add((r.X[i] + sx[i], r.Z[i] + sz[i]));
            var moved = Resample(r.Name, pts, o, closed: r.Closed, r.Planned);
            r.X = moved.X; r.Z = moved.Z; r.Planned = moved.Planned; n = r.Count;
            Geometry(r, o);
        }
        var worstAfter = Enumerable.Range(0, r.Count).Min(i => edge(r.X[i], r.Z[i]));
        report.Notes.Add($"the lap kept off the island's edge: it came within {worstBefore:0.0} cells of a cube the island hasn't got (the engine's open sea), now {worstAfter:0.0} (the road needs {margin:0.0})" +
                         (worstAfter < margin - 0.25 ? " -- WARNING: still too near" : ""));
    }

    // How far to move a straight (through (x, z) along (nx, nz), `half` cells each way) sideways -- to its left for a positive answer --
    // so all of it is at least IslandEdgeMargin from a missing cube: the smallest such move, or 0 when it is clear already.
    private static double StraightShift(double x, double z, double nx, double nz, double half, Func<double, double, double> edge, RaceTrackOptions o)
    {
        var margin = IslandEdgeMargin(o);
        bool Clear(double s)
        {
            for (var t = -half; t <= half; t += 0.5)
                if (edge(x + nx * t - nz * s, z + nz * t + nx * s) < margin) return false;
            return true;
        }
        for (var s = 0.0; s <= 16; s += 0.25)
        {
            if (Clear(s)) return s;
            if (s > 0 && Clear(-s)) return -s;
        }
        return 0;
    }

    // Where one deck over a group of crossings sits: its middle along the road it carries (s), half its flat length, and the height of
    // the highest road it passes over. (For one crossing: that crossing, DeckHalfCells, and the road under it.)
    private static (double S0, double Half, double UnderHeight) SpanOf(TrackRoad r, List<(Crossing C, int Over, int Under)> group, RaceTrackOptions o)
    {
        // (the heights are there once the road has its profile; before that, while it is being straightened, only the place matters)
        double Height(int i) => i < r.H.Length ? r.H[i] : 0;
        var s0 = r.S[group[0].Over];
        var half = DeckHalfCells(o, Math.Max(group[0].C.Angle, 12));
        var under = Height(group[0].Under);
        if (group.Count < 2) return (s0, half, under);
        var spans = group.Select(g => (At: Along(r, r.S[g.Over] - s0), Half: DeckHalfCells(o, Math.Max(g.C.Angle, 12)), Under: Height(g.Under))).ToList();
        var mid = (spans.Min(g => g.At - g.Half) + spans.Max(g => g.At + g.Half)) / 2;
        return (s0 + mid, spans.Max(g => Math.Abs(g.At - mid) + g.Half), spans.Max(g => g.Under));
    }

    // How much further than the deck and its landings a multi-crossing span is straightened: the crossings move a little when the road
    // is straightened, and the deck is then centred on where they are.
    private const double SpanSlack = 6;

    private static RoadBridgeInfo? PlanRoadBridge(TrackRoad r, List<(Crossing C, int Over, int Under)> group, RaceTrackOptions o, RaceTrackReport report)
    {
        var c = group[0].C;
        var (over, under) = (group[0].Over, group[0].Under);
        var angleDeg = Math.Max(c.Angle, 12);                                                 // never let a near-parallel crossing blow the deck length up
        // the crossing was straightened over coreHalf+3 cells each side (see Build), so the straight deck matches the road under it
        var coreHalfCells = DeckHalfCells(o, angleDeg);
        var rampCells = o.RoadBridgeRampLength;
        var s0 = r.S[over];
        var deckHeight = r.H[under] + o.RoadBridgeClearance;
        if (group.Count > 1)
        {
            // one deck over them all: centred between them, long enough for each one's own span, and high enough over the highest road it
            // carries the lap above
            var (spanS, spanHalf, spanUnder) = SpanOf(r, group, o);
            s0 = spanS; coreHalfCells = spanHalf; deckHeight = spanUnder + o.RoadBridgeClearance;
        }
        deckHeight = Math.Max(deckHeight, o.RoadBridgeLeast);

        // flat at the deck's height over the deck and its two landings, then the ramps
        var flatHalf = coreHalfCells + o.RoadBridgeLanding;
        double Weight(double ds)
        {
            var ad = Math.Abs(ds);
            if (ad <= flatHalf) return 1;
            if (ad >= flatHalf + rampCells) return 0;
            var t = (ad - flatHalf) / rampCells; return 1 - t * t * (3 - 2 * t);
        }
        var n = r.Count;
        // The natural profile is already grade-limited; the ramp is laid over it directly and NOT run through LimitGrade again,
        // which would spread a 2800-unit climb over ~60 cells each way and sag the deck joint. With the smoothstep blend the
        // steepest point of the ramp is 1.5 x clearance / ramp length (84 units per cell, about 9 degrees, at the defaults).
        for (var k = 0; k < n; k++)
        {
            var ds = Along(r, r.S[k] - s0);
            var w = Weight(ds);
            if (w <= 0) continue;
            r.H[k] = r.H[k] * (1 - w) + deckHeight * w;
            var ad = Math.Abs(ds);
            if (ad <= coreHalfCells - DeckEndOverlap) { r.Deck[k] = true; continue; }
            // the ramps get the water bridge's own narrow, rock-sided shoulders (ModifyGround: level to CurbHalf + RampShoulder) instead of the
            // ordinary 13-cell earth skirt, which would otherwise spill a bulge across the road passing under the deck
            r.Bridge[k] = true;
            if (ad > flatHalf) continue;
            // the bridge heads: the landings level with the deck, and the ground under the deck's last cells just below its top
            // (hidden by the tiles, so there is no crack between the landing and the deck)
            r.Landing[k] = true;
            if (ad <= coreHalfCells) { r.H[k] = deckHeight - UnderDeckDepth; r.UnderDeck[k] = true; }
        }
        var widthUnits = r.VergeHalf * 2 * 512;
        var lengthUnits = coreHalfCells * 2 * 512;
        // (the deck is centred on the group, which may be a little along the road from the first crossing)
        var centre = r.Count > 0 ? Nearest(r, r.X[over], r.Z[over]) : over;
        for (var k = 0; k < r.Count; k++) if (Math.Abs(Along(r, r.S[k] - s0)) < o.Spacing) { centre = k; break; }
        report.Notes.Add($"road bridge: the straighter road climbs onto a deck over the other -- {coreHalfCells * 2:0} cells of flat deck ({widthUnits / 512:0.#} cells wide) plus {o.RoadBridgeLanding:0.#} cells of level ground and {rampCells:0} cells of ramp each side, deck height {deckHeight:0} ({o.RoadBridgeClearance:0} above the road it crosses, at {angleDeg:0} degrees)");
        // how well the straight deck fits the road it carries: the road's middle, anywhere along the deck, is this far off its axis (more than
        // a cell or so and the deck juts out over the verge while the road runs off its side)
        double worst = 0;
        for (var k = 0; k < r.Count; k++)
        {
            var ds = Along(r, r.S[k] - s0);
            if (Math.Abs(ds) > coreHalfCells) continue;
            worst = Math.Max(worst, Math.Abs(-(r.X[k] - r.X[centre]) * r.Tz[centre] + (r.Z[k] - r.Z[centre]) * r.Tx[centre]));
        }
        report.Notes.Add($"road bridge: the road runs within {worst:0.0} cells of the deck's middle all along it" + (worst > 1.5 ? " -- WARNING: the deck does not follow the road" : ""));
        return new RoadBridgeInfo(r.X[centre], r.Z[centre], deckHeight, r.Tx[centre], r.Tz[centre], widthUnits, lengthUnits, under);
    }

    // The plan's own bridge (RaceTrackPlan.Deck): a flat deck from lap point a to lap point b, the way the lap runs, over whatever lies
    // beneath -- the sea, a part of the lap passing under it. The plan keeps the road straight and level there. As PlanRoadBridge's: the
    // deck is a whole number of tiles (lengthened evenly at both ends to make it so), the tiles cover it but for DeckEndOverlap cells at
    // each end, where the ground rises to just under their top, and RoadBridgeLanding cells of ground level with it lie beyond each end;
    // the rest of the road keeps the plan's heights. Each part of the lap it crosses is checked for the clearance a car needs under it.
    private static RoadBridgeInfo? PlanDeck(TrackRoad r, int a, int b, List<Crossing> crossings, RaceTrackOptions o, RaceTrackReport report)
    {
        var n = r.Count;
        var drawn = Along(r, r.S[b] - r.S[a]); if (drawn < 0) drawn += r.Length;
        var tile = o.RoadBridgeTileLength;
        var len = Math.Ceiling(drawn / tile - 1e-9) * tile;
        a = At(r, a - (int)Math.Round((len - drawn) / 2 / o.Spacing));
        double Pos(int k) { var t = r.S[k] - r.S[a]; if (t < 0) t += r.Length; return t; }        // 0 .. Length, from a the way the lap runs
        var mid = At(r, a + (int)Math.Round(len / 2 / o.Spacing));
        var deckHeight = r.H[mid];
        var under = -1;
        foreach (var c in crossings)
        {
            var (over, below) = Pos(c.I) <= len ? (c.I, c.J) : Pos(c.J) <= len ? (c.J, c.I) : (-1, -1);
            if (over < 0) { report.Notes.Add($"WARNING: the lap crosses itself at cell ({c.X:0.0}, {c.Z:0.0}), away from the plan's bridge: the roads meet there"); continue; }
            under = below;
            var clear = deckHeight - r.H[below];
            report.Notes.Add($"the plan's bridge passes {clear:0} over the lap at cell ({c.X:0.0}, {c.Z:0.0}), crossing it at {c.Angle:0} degrees" +
                             (clear < o.RoadBridgeClearance ? $" -- WARNING: a car needs {o.RoadBridgeClearance:0}" : ""));
        }
        var level = 0.0;
        for (var k = 0; k < n; k++)
        {
            var t = Pos(k);
            if (t <= len)
            {
                level = Math.Max(level, Math.Abs(r.H[k] - deckHeight));
                r.H[k] = deckHeight;
                if (Math.Min(t, len - t) >= DeckEndOverlap) { r.Deck[k] = true; continue; }
                r.Bridge[k] = true; r.Landing[k] = true; r.UnderDeck[k] = true; r.H[k] = deckHeight - UnderDeckDepth;
            }
            else if (t - len <= o.RoadBridgeLanding || r.Length - t <= o.RoadBridgeLanding)
            {
                level = Math.Max(level, Math.Abs(r.H[k] - deckHeight));
                r.H[k] = deckHeight; r.Bridge[k] = true; r.Landing[k] = true;
            }
        }
        var b2 = At(r, a + (int)Math.Round(len / o.Spacing));
        double dx = r.X[b2] - r.X[a], dz = r.Z[b2] - r.Z[a]; var dl = Math.Sqrt(dx * dx + dz * dz) + 1e-9; dx /= dl; dz /= dl;
        double cx = (r.X[a] + r.X[b2]) / 2, cz = (r.Z[a] + r.Z[b2]) / 2;
        double worst = 0;
        for (var k = 0; k < n; k++) if (Pos(k) <= len) worst = Math.Max(worst, Math.Abs(-(r.X[k] - cx) * dz + (r.Z[k] - cz) * dx));
        report.Notes.Add($"the plan's bridge: {len:0} cells of flat deck ({len / tile:0} tiles) at {deckHeight:0} from ({r.X[a]:0.0}, {r.Z[a]:0.0}) to ({r.X[b2]:0.0}, {r.Z[b2]:0.0}), " +
                         $"{o.RoadBridgeLanding:0.#} cells of level ground each end; the plan's heights were up to {level:0} off level along it, " +
                         $"and the road runs within {worst:0.0} cells of the deck's middle" + (worst > 1.5 ? " -- WARNING: the deck does not follow the road" : ""));
        // (a landing the road doesn't run straight on along: no railing there, or a car turning off the bridge drives into it)
        double behind = 0, ahead = 0;
        for (var k = 0; k < n; k++)
        {
            var t = Pos(k); var off = Math.Abs(-(r.X[k] - cx) * dz + (r.Z[k] - cz) * dx);
            if (t > len && t - len <= o.RoadBridgeLanding) ahead = Math.Max(ahead, off);
            else if (t > len && r.Length - t <= o.RoadBridgeLanding) behind = Math.Max(behind, off);
        }
        if (behind > 1 || ahead > 1) report.Notes.Add($"the bridge's railings stop at the deck {(behind > 1 && ahead > 1 ? "at both ends" : behind > 1 ? "where the road comes onto it" : "where the road leaves it")}: the road turns there");
        return new RoadBridgeInfo(cx, cz, deckHeight, dx, dz, r.VergeHalf * 2 * 512, len * 512, under, behind <= 1, ahead <= 1);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // the jump where the lap crosses itself

    // How far the road is made straight either side of the crossing for a jump at `angleDeg`: the ramps, their gaps and a little more.
    private static double JumpStraight(RaceTrackOptions o, double angleDeg) => Math.Max(15, JumpLip(o, angleDeg) + Math.Max(o.JumpRampLength, o.JumpLandingLength) + 3);

    // The angle the jump crosses at: the steepest (from JumpCrossingAngle down, 2 degrees at a time) whose turned road keeps JumpClearance
    // cells from every other part of the lap, else the one that keeps the most.
    private static double JumpAngle(TrackRoad r, RaceTrackOptions o, RaceTrackReport report)
    {
        var bestAngle = o.JumpCrossingAngle; var bestClear = double.MinValue;
        for (var angle = o.JumpCrossingAngle; angle >= o.JumpMinCrossingAngle - 1e-9; angle -= 2)
        {
            var trial = r.Clone();
            SteepenCrossing(trial, o, new RaceTrackReport(), angle, JumpStraight(o, angle));
            var clear = JumpClearance(r, trial, o);
            if (clear >= o.JumpClearance)
            {
                if (angle < o.JumpCrossingAngle)
                    report.Notes.Add($"jump: crossing at {angle:0} degrees, the steepest that keeps the road {o.JumpClearance:0.#} cells from the rest of the lap ({clear:0.0}; steeper, the stretch beyond the jump ran into another part of the lap)");
                return angle;
            }
            if (clear > bestClear) { bestClear = clear; bestAngle = angle; }
        }
        report.Notes.Add($"WARNING: at no crossing angle from {o.JumpCrossingAngle:0} to {o.JumpMinCrossingAngle:0} degrees does the jump's road keep {o.JumpClearance:0.#} cells from the rest of the lap; {bestAngle:0} degrees keeps {bestClear:0.0}");
        return bestAngle;
    }

    // The other road, squared up to the jump near the crossing. The jump's road can only be turned so far (JumpAngle), and at that angle the
    // other road's two arms run so close beside the jump's ramps that its asphalt reached onto the ramps' sides (up to 3 cells: the ramps
    // lost their curbs there). So the other road is turned too, over a short stretch only: straight across the jump for SquareStraight
    // cells either side, then back to its own course over a short blend. Turned the way the jump's road was, over the usual long blend,
    // it ran into other parts of the lap. Tried from 90 degrees down, with the shortest blends last; the first that keeps the lap
    // JumpClearance cells from itself, crosses it once, and bends no tighter than SquareMinRadius cells is used.
    private const double SquareStraight = 8, SquareMinRadius = 6;

    private static void SquareOtherRoad(TrackRoad drawn, TrackRoad r, RaceTrackOptions o, RaceTrackReport report)
    {
        var before = FindCrossings(r, o);
        if (before.Count == 0) return;
        var current = before[0].Angle;
        foreach (var target in new double[] { 90, 85, 80, 75, 70, 65, 60 })
        {
            if (target <= current + 5) break;
            foreach (var blend in new double[] { 32, 28, 24 })
            {
                var trial = r.Clone();
                SteepenCrossing(trial, o, new RaceTrackReport(), target, SquareStraight, turnOther: true, blendCells: blend);
                if (FindCrossings(trial, o).Count != 1) continue;
                if (JumpClearance(drawn, trial, o) < o.JumpClearance) continue;
                // the tightest bend of what this turn moved
                var tightest = double.MaxValue;
                for (var k = 0; k < trial.Count; k++)
                {
                    var moved = true;
                    for (var m = 0; m < r.Count && moved; m++) if (Sq(r.X[m] - trial.X[k]) + Sq(r.Z[m] - trial.Z[k]) < 0.25) moved = false;
                    if (moved && Math.Abs(trial.Kappa[k]) > 1e-9) tightest = Math.Min(tightest, 1 / Math.Abs(trial.Kappa[k]));
                }
                if (tightest < SquareMinRadius) continue;
                SteepenCrossing(r, o, report, target, SquareStraight, turnOther: true, blendCells: blend);
                report.Notes.Add($"jump: the other road squared up to the jump ({target:0} degrees, straight {SquareStraight:0} cells either side, back on its course over {blend:0} cells; its bends there no tighter than {tightest:0.0} cells)");
                return;
            }
        }
        report.Notes.Add($"WARNING: the other road could not be squared up to the jump without coming within {o.JumpClearance:0.#} cells of the rest of the lap; its arms run close beside the ramps");
    }

    // How close the re-shaped stretch of the road (the points of `r` that are no longer on the drawn road `drawn`) comes to another part of
    // the lap, cells between centre lines, against points at least 30 cells away along the lap. The two roads' own meeting -- the other
    // road within 40 cells of the crossing, along it -- is left out: that is the one crossing there should be.
    private static double JumpClearance(TrackRoad drawn, TrackRoad r, RaceTrackOptions o)
    {
        var found = FindCrossings(r, o);
        if (found.Count == 0) return 0;
        double Along(double a, double b) { var d = Math.Abs(a - b); return Math.Min(d, r.Length - d); }
        var sI = r.S[found[0].I]; var sJ = r.S[found[0].J];
        bool Moved(int k)
        {
            for (var m = 0; m < drawn.Count; m++) if (Sq(drawn.X[m] - r.X[k]) + Sq(drawn.Z[m] - r.Z[k]) < 0.5 * 0.5) return false;
            return true;
        }
        var best = double.MaxValue;
        for (var i = 0; i < r.Count; i++)
        {
            if (!Moved(i)) continue;
            for (var j = 0; j < r.Count; j++)
            {
                if (Along(r.S[j], r.S[i]) < 30 || Along(r.S[j], sI) < 40 || Along(r.S[j], sJ) < 40) continue;
                var d = Sq(r.X[i] - r.X[j]) + Sq(r.Z[i] - r.Z[j]);
                if (d < best) best = d;
            }
        }
        return best == double.MaxValue ? double.MaxValue : Math.Sqrt(best);
    }

    // How far each ramp's end -- the lip, and the down ramp's top -- is from the crossing, cells along the road: the other road's curbs,
    // measured along this one, and the gap.
    private static double JumpLip(RaceTrackOptions o, double angleDeg) => o.CurbHalfWidth / Math.Sin(Math.Clamp(angleDeg, 30, 90) * Math.PI / 180) + o.JumpGap;

    // The take-off strip: it starts this many cells before the lip and is this deep. The flight starts where the car enters it (the
    // hero's script looks every frame; a car at full speed moves a fifth of a cell a frame). The flight's first cell is on the ground,
    // so it has to start well short of the lip: the lip runs across the cell grid at an angle, and the blocking rock of its face steps
    // onto the ramp's top at one side, which stopped a flight started 1.5 cells short of it three cells off the centre line.
    private const double JumpZoneBefore = 2.5, JumpZoneDepth = 1.25;

    // A ramp's shape, 0 at its foot to 1 at its top: a straight climb with a rounded foot, so the car doesn't pitch up all at once.
    private static double Rise(double t)
    {
        const double foot = 0.3;
        t = Math.Clamp(t, 0, 1);
        return (t < foot ? t * t / (2 * foot) : t - foot / 2) / (1 - foot / 2);
    }

    // One of the two roads that meet (the straighter) climbs a ramp before the crossing, flies over the other road and lands on a down
    // ramp beyond it; between each ramp and the other road lies a gap of sand, so the road visibly doesn't cross itself. The flight is the
    // retail buggy jump's mechanism with a longer flight (RaceTrackJumpAnim): a strip of scenario zones across the ramp (the hero's script
    // starts the flight when the buggy is in it and heads roughly the right way), and the flight's steps carry the car along the road's
    // heading from where it entered the strip. The whole jump sits on a level stretch at the crossing's height, so the flight, which
    // knows nothing of the ground, ends where the down ramp is.
    private static JumpInfo? PlanJump(TrackRoad r, Crossing c, RaceTrackOptions o, RaceTrackReport report)
    {
        double Bend(int i)
        {
            double sum = 0; var w = (int)(12 / o.Spacing);
            for (var k = -w; k <= w; k++) sum += Math.Abs(r.Kappa[At(r, i + k)]);
            return sum;
        }
        var ia = Bend(c.I) <= Bend(c.J) ? c.I : c.J;
        var ib = ia == c.I ? c.J : c.I;
        double hx = 0, hz = 0; var span = (int)(6 / o.Spacing);
        for (var k = -span; k <= span; k++) { var i = At(r, ia + k); hx += r.Tx[i]; hz += r.Tz[i]; }
        var hl = Math.Sqrt(hx * hx + hz * hz) + 1e-9; hx /= hl; hz /= hl;
        var n = r.Count; var s0 = r.S[ia];
        double Ds(int k) { var ds = r.S[k] - s0; if (r.Closed) { if (ds > r.Length / 2) ds -= r.Length; else if (ds < -r.Length / 2) ds += r.Length; } return ds; }

        // Where the jump's line leaves the other road's curbs, cells from the crossing either way. The other road bends through the
        // crossing, so its curbs are not as far on one side as on the other; each lip is a gap past its own side's curb.
        double Edge(int way)
        {
            for (var t = 0.0; t < 40; t += 0.1)
            {
                var px = r.X[ia] + way * hx * t; var pz = r.Z[ia] + way * hz * t;
                var d = double.MaxValue;
                for (var k = -120; k < 120; k++)
                {
                    int a = At(r, ib + k), b = At(r, ib + k + 1);
                    var sx = r.X[b] - r.X[a]; var sz = r.Z[b] - r.Z[a]; var len2 = sx * sx + sz * sz;
                    var u = len2 < 1e-12 ? 0 : Math.Clamp(((px - r.X[a]) * sx + (pz - r.Z[a]) * sz) / len2, 0, 1);
                    d = Math.Min(d, Sq(px - r.X[a] - sx * u) + Sq(pz - r.Z[a] - sz * u));
                }
                if (Math.Sqrt(d) > r.CurbHalf) return t;
            }
            return 40;
        }
        var lipUp = Edge(-1) + o.JumpGap; var lipDown = Edge(1) + o.JumpGap;
        return LayJump(r, ia, hx, hz, lipUp, lipDown, openGap: false, o, report);
    }

    // A jump the plan draws over a gap in the ground (RaceTrackPlan.GapJump: a bay between two arms of a plateau): the take-off lip at
    // one point, the landing lip at the other, the road straight and level between and around them (the plan keeps it so). The ground in
    // the gap is left as it is -- the car flies over it -- and the flight is the crossing jump's.
    private static JumpInfo? PlanGapJump(TrackRoad r, int lip, int landing, RaceTrackOptions o, RaceTrackReport report, int index = 0)
    {
        var len = Along(r, r.S[landing] - r.S[lip]); if (len < 0) len += r.Length;
        var ia = At(r, lip + (int)Math.Round(len / 2 / o.Spacing));
        double hx = r.X[landing] - r.X[lip], hz = r.Z[landing] - r.Z[lip];
        var hl = Math.Sqrt(hx * hx + hz * hz) + 1e-9; hx /= hl; hz /= hl;
        var lipUp = Math.Abs(Along(r, r.S[ia] - r.S[lip])); var lipDown = Math.Abs(Along(r, r.S[landing] - r.S[ia]));
        // (the plan's road should run straight over the gap: the flight flies straight on)
        var off = 0.0;
        for (var k = 0; k <= (int)Math.Round(len / o.Spacing); k++)
        {
            var i = At(r, lip + k);
            off = Math.Max(off, Math.Abs(-(r.X[i] - r.X[lip]) * hz + (r.Z[i] - r.Z[lip]) * hx));
        }
        // (a landing lower than the take-off by more than a ramp's height: a drop -- the lava lake's, off the mesa onto the dock)
        double? landLevel = r.H[lip] - r.H[landing] > DropFrom ? r.H[landing] : null;
        report.Notes.Add($"gap jump: {len:0.0} cells from the take-off lip at ({r.X[lip]:0.0}, {r.Z[lip]:0.0}) to the landing lip at ({r.X[landing]:0.0}, {r.Z[landing]:0.0}), " +
                         (landLevel is { } low ? $"from {r.H[lip]:0} down to {low:0} (a drop); " : $"level at {r.H[ia]:0}; ") +
                         $"the road runs within {off:0.0} cells of the flight's line over it" + (off > 1 ? " -- WARNING: the plan's road bends over the gap" : ""));
        return LayJump(r, ia, hx, hz, lipUp, lipDown, openGap: true, o, report, index, landLevel is null ? null : (r.H[lip], landLevel.Value));
    }

    // A gap jump whose landing lip is this much lower than its take-off lip, or more, is a drop: its flight is one of its own, diving down
    // (RaceTrackJumpAnim.Drop), and its landing ramp a low one.
    private const double DropFrom = 600, DropRamp = 200;

    // The ramps, the gap and the flight of a jump through point `ia` along (hx, hz): the take-off lip lipUp cells before it, the landing
    // lip lipDown cells after. `openGap`: a gap jump -- the gap is left as the ground is (Void), and the road's own profile is already level
    // there, so it is not blended.
    // `levels`: a drop's take-off and landing levels (the plan's heights at its lips; the jump is level at its middle's height otherwise).
    private static JumpInfo? LayJump(TrackRoad r, int ia, double hx, double hz, double lipUp, double lipDown, bool openGap, RaceTrackOptions o, RaceTrackReport report, int index = 0,
        (double Up, double Down)? levels = null)
    {
        var n = r.Count; var s0 = r.S[ia];
        double Ds(int k) { var ds = r.S[k] - s0; if (r.Closed) { if (ds > r.Length / 2) ds -= r.Length; else if (ds < -r.Length / 2) ds += r.Length; } return ds; }

        // along the road from the crossing: the up ramp's foot, its lip, the gap, the other road, the gap, the down ramp's top and foot
        var upFoot = -lipUp - o.JumpRampLength; var downFoot = lipDown + o.JumpLandingLength;
        var start = -lipUp - JumpZoneBefore;
        // the flight: long enough to reach JumpLandInto cells down the far ramp (never shorter than the retail one, or the plan's shortest)
        var scale = RaceTrackJumpAnim.ForwardFor(JumpZoneBefore + lipUp + lipDown + o.JumpLandInto, o.JumpMinScale);
        var flight = RaceTrackJumpAnim.Distance(scale);
        var land = start + flight;
        var up = o.JumpRampHeight;
        var takeoff = up * Rise((start - upFoot) / o.JumpRampLength);
        // the down ramp is as high as puts its surface, where the flight ends, just under the flight's end
        var landT = (land - lipDown) / o.JumpLandingLength;
        var down = Math.Clamp((takeoff + RaceTrackJumpAnim.EndDrop(scale) - 30) / Math.Max(0.05, Rise(1 - landT)), 150, up);
        // (a drop: a low landing ramp, and the flight ends just over it, however far below the take-off that is)
        var drop = 0.0;
        if (levels is { } lv)
        {
            down = DropRamp;
            drop = lv.Up + takeoff - (lv.Down + down * Rise(Math.Clamp(1 - landT, 0, 1)) + 30);
        }

        // a level stretch at the crossing's height (EqualiseCrossings made the two roads meet there) over the whole jump, blended into the
        // road's own profile over the next cells, then the ramps on it (a drop: its take-off side at its own level, its landing side at its)
        var level = levels?.Up ?? r.H[ia];
        var landLevel = levels?.Down ?? level;
        const double blendCells = 15;
        for (var k = 0; k < n; k++)
        {
            var ds = Ds(k);
            var outside = ds < upFoot - 2 ? upFoot - 2 - ds : ds > downFoot + 2 ? ds - downFoot - 2 : 0;
            if (outside >= (openGap ? 1e-9 : blendCells)) continue;
            var t = outside / blendCells;
            var w = 1 - t * t * (3 - 2 * t);
            r.H[k] = r.H[k] * (1 - w) + (ds > 0 ? landLevel : level) * w;
            // (the warning stripe on each ramp's last cell of flat top: the cell at the very end holds the drop, and is rock)
            if (ds >= upFoot && ds <= -lipUp)
            {
                r.H[k] = level + up * Rise((ds - upFoot) / o.JumpRampLength);
                r.Bridge[k] = true; r.Jump[k] = true; r.Lip[k] = ds >= -lipUp - 2 && ds < -lipUp - 0.75;
            }
            else if (ds > -lipUp && ds < lipDown) { r.Jump[k] = true; r.Gap[k] = true; if (openGap) r.Void[k] = true; }
            else if (ds >= lipDown && ds <= downFoot)
            {
                r.H[k] = landLevel + down * Rise(1 - (ds - lipDown) / o.JumpLandingLength);
                r.Bridge[k] = true; r.Jump[k] = true; r.Lip[k] = ds > lipDown + 0.75 && ds <= lipDown + 2;
            }
        }

        // (the road is straight here: SteepenCrossing made it so over the whole jump)
        var sx = r.X[ia] + hx * start; var sz = r.Z[ia] + hz * start;       // where the flight starts
        var lx = r.X[ia] + hx * land; var lz = r.Z[ia] + hz * land;         // where it ends
        var beta = (int)Math.Round(Math.Atan2(hx, hz) / (2 * Math.PI) * 4096); beta = ((beta % 4096) + 4096) % 4096;
        // the strip across the ramp: cells whose centre is 0..JumpZoneDepth cells past the start and within the curbs
        var cells = new HashSet<(int, int)>();
        for (var gz = (int)Math.Floor(sz) - 8; gz <= (int)Math.Ceiling(sz) + 8; gz++)
        for (var gx = (int)Math.Floor(sx) - 8; gx <= (int)Math.Ceiling(sx) + 8; gx++)
        {
            var qx = gx + 0.5 - sx; var qz = gz + 0.5 - sz;
            var f = qx * hx + qz * hz; var u = -qx * hz + qz * hx;
            if (f >= 0 && f <= JumpZoneDepth && Math.Abs(u) <= r.CurbHalf) cells.Add((gx, gz));
        }
        // rows of cells become boxes; equal runs in neighbouring rows join
        var rows = cells.GroupBy(q => q.Item2).OrderBy(g => g.Key).ToList();
        var boxes = new List<(int X0, int Z0, int X1, int Z1)>();
        var open = new List<(int X0, int Z0, int X1, int Z1)>();
        foreach (var row in rows)
        {
            var xs = row.Select(q => q.Item1).OrderBy(x => x).ToList();
            var runs = new List<(int, int)>(); var first = xs[0]; var prev = xs[0];
            foreach (var x in xs.Skip(1)) { if (x != prev + 1) { runs.Add((first, prev + 1)); first = x; } prev = x; }
            runs.Add((first, prev + 1));
            var next = new List<(int X0, int Z0, int X1, int Z1)>();
            foreach (var (x0, x1) in runs)
            {
                var at = open.FindIndex(b => b.X0 == x0 && b.X1 == x1 && b.Z1 == row.Key);
                if (at >= 0) { next.Add((x0, open[at].Z0, x1, row.Key + 1)); open.RemoveAt(at); }
                else next.Add((x0, row.Key, x1, row.Key + 1));
            }
            boxes.AddRange(open); open = next;
        }
        boxes.AddRange(open);
        var height = level + takeoff;
        var cube = ((int)Math.Floor(sx / 64), (int)Math.Floor(sz / 64));
        if ((int)Math.Floor(lx / 64) != cube.Item1 || (int)Math.Floor(lz / 64) != cube.Item2)
            report.Notes.Add("WARNING: the jump lands in another cube than it starts in; the game changes scene at the cube's edge and the flight would break");
        report.Notes.Add((openGap
                             ? $"jump: a ramp {up:0} high over {o.JumpRampLength:0} cells to the lip, a gap of {lipUp + lipDown:0.0} cells the car flies over{(drop > 0 ? $", diving {drop:0} down" : "")}, and a down ramp {down:0} high over {o.JumpLandingLength:0} cells; "
                             : $"jump: a ramp {up:0} high over {o.JumpRampLength:0} cells to a lip {lipUp:0.0} cells before the crossing, a gap of sand, the other road, " +
                               $"a gap and a down ramp {down:0} high over {o.JumpLandingLength:0} cells from {lipDown:0.0} cells past it (the gaps {o.JumpGap:0.#} cells past the other road's curbs); ") +
                         $"the flight ({flight:0.0} cells, the retail one x{scale:0.00}) starts at cell ({sx:0.0}, {sz:0.0}), " +
                         $"{JumpZoneBefore:0.#} cells before the lip, and lands {land - lipDown:0.0} cells down the far ramp at ({lx:0.0}, {lz:0.0}); heading turn {beta}, {boxes.Count} zone boxes");
        double OnLap(double ds) { var v = (s0 + ds) % r.Length; return v < 0 ? v + r.Length : v; }
        return new JumpInfo(sx, sz, lx, lz, hx, hz, beta, height, cube.Item1, cube.Item2, boxes, o.JumpZone + index, o.JumpAnim + RaceTrackJumpAnim.JumpOffset * index,
            scale, flight, index, OnLap(start), OnLap(land), drop);
    }

    // The plan's carried jumps (RaceTrackPlan.ArcJumps): no deck between the lips (Gap; the ground there is left as it is, Void), nothing
    // that stands under the car's way cleared (Arc: the engine carries the car through the air, over the Emerald Moon's reactor), and
    // where each is along the lap, for the checkpoints to keep off it. Its heights are the plan's own, foot to foot: the engine's way.
    private static void PlanArcJumps(RaceTrackPlan plan, TrackRoad r, RaceTrackReport report)
    {
        foreach (var j in plan.ArcJumps ?? Array.Empty<int[]>())
        {
            if (j.Length < 4) continue;
            int foot = PlanPoint(plan, r, j[0]), lip = PlanPoint(plan, r, j[1]), land = PlanPoint(plan, r, j[2]), landFoot = PlanPoint(plan, r, j[3]);
            r.Arc ??= new bool[r.Count];
            var top = r.H[lip];
            for (var k = At(r, lip + 1); k != land; k = At(r, k + 1)) { r.Gap[k] = true; r.Void[k] = true; r.Arc[k] = true; top = Math.Max(top, r.H[k]); }
            report.ArcJumps.Add((foot, lip, land, landFoot, r.S[foot], r.S[landFoot]));
            report.ArcCameras.Add(j.Length > 4 ? j[4] : null);
            double Cells(int a, int b) { var d = r.S[b] - r.S[a]; return d < 0 ? d + r.Length : d; }
            report.Notes.Add($"carried jump: a ramp from cell ({r.X[foot]:0.0}, {r.Z[foot]:0.0}) up {r.H[lip] - r.H[foot]:0} over {Cells(foot, lip):0.0} cells to its lip, a flight of " +
                             $"{Cells(lip, land):0.0} cells topping out at {top:0}, a hill down {r.H[land] - r.H[landFoot]:0} over {Cells(land, landFoot):0.0} cells to cell ({r.X[landFoot]:0.0}, {r.Z[landFoot]:0.0}) " +
                             $"(the race-track mode carries the car from foot to foot)");
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // what Play's race-track mode needs besides the ground: checkpoints round the lap, and the opponent's line

    // Checkpoints: lines across the road the car has to cross in order before a lap counts (the engine's race-track mode, RACEMOD.CPP);
    // without them a car could reverse over the start line and cross it again. Spread evenly round the lap, clear of what would make a
    // line unfair or unseen: the stretch the pit lane runs beside (a car in the pits skips it), the crossing (the other road passes under
    // the bridge or the jump there), and the edges of the cubes (the engine only sees a car's moves within one cube). Each reaches past
    // the verge on both sides, but never half way to another part of the lap.
    // (2026-10-03) One line in the middle of every corner -- what a car cutting a corner skips -- reaching CheckpointPastEdge past the road's
    // edge (its curbs, or a raised road's rail) on both sides: a car half off the road, running wide or overtaking on the edge, still
    // crosses it; one cutting across the inside goes round its end. A corner is a stretch bending one way tighter than CornerRadius cells,
    // through CornerTurn at the least (two such stretches a few cells apart are one corner); one turning further than half a circle has a
    // line in the middle of each half-circle of it. (Before: one about every 60 cells, where the road ran straightest.)
    private const int MaxCheckpoints = 64;      // (the engine's RACE_MAX_CHECKPOINTS)
    private const double CheckpointPastEdge = 2, CornerRadius = 40, CornerTurn = 0.5, CornerJoin = 8;

    // Where the opponents' cars wait while the player drives his qualifying lap: in the pit lane beside the start line, PitStep cells
    // apart, the first PitFirst cells before the line, each facing the way a car leaves the pits. On the grid they stand across the start
    // line, which a qualifying lap has to cross. The spots are found by where they lie along the lap, not by counting points: the pit lane
    // is its own road, and its points may run either way round the lap.
    private const double PitFirst = 2, PitStep = 4;
    private const int PitWaitSpots = 5;

    // (a planned lap's short pit lane -- Mosquibees Island's plateau has room for 24 cells of it -- is still moving out from the lap where
    // the third spot before the line would be: a car waiting there stood on the lap. There a spot must be PitClear cells to the side, and
    // the ones that aren't are taken after the line instead.)
    private const double PitClear = 6;

    private static void PlacePits(List<TrackRoad> roads, RaceTrackReport report, RaceTrackOptions o, bool planned = false)
    {
        if (roads.Count < 2 || report.StartLine.Count == 0) return;
        var pit = roads[1];
        var s = report.StartLine[0];
        double Along(int i) => (pit.X[i] - s.X) * s.DirX + (pit.Z[i] - s.Z) * s.DirZ;
        double Across(int i) => -(pit.X[i] - s.X) * s.DirZ + (pit.Z[i] - s.Z) * s.DirX;
        var targets = Enumerable.Range(0, PitWaitSpots).Select(k => -(PitFirst + k * PitStep)).ToList();
        if (planned) targets.AddRange(Enumerable.Range(0, PitWaitSpots).Select(k => PitFirst + k * PitStep));
        foreach (var target in targets)
        {
            if (report.Pits.Count == PitWaitSpots) break;
            var best = -1; var bd = double.MaxValue;
            for (var i = 2; i < pit.Count - 2; i++) { var d = Math.Abs(Along(i) - target); if (d < bd) { bd = d; best = i; } }
            if (planned && (best < 0 || bd > PitStep || Math.Abs(Across(best)) < PitClear)) continue;
            if (best < 0 || bd > PitStep) break;
            // facing the way a car leaves the pits: the lane's own direction there, turned to agree with the lap's
            var sign = pit.Tx[best] * s.DirX + pit.Tz[best] * s.DirZ < 0 ? -1 : 1;
            report.Pits.Add((pit.X[best], pit.Z[best], pit.H[best], pit.Tx[best] * sign, pit.Tz[best] * sign));
        }
        if (report.Pits.Count == 0) return;
        var side = Math.Abs(Across(Nearest(pit, report.Pits[0].X, report.Pits[0].Z)));
        var after = report.Pits.Count(p => (p.X - s.X) * s.DirX + (p.Z - s.Z) * s.DirZ > 0);
        report.Notes.Add($"the pits: {report.Pits.Count} waiting spots in the pit lane, from {PitFirst:0.#} cells before the start line, {PitStep:0.#} apart, " +
                         $"{side:0.#} cells to the side of the lap" + (after > 0 ? $" ({after} of them after the line: the lane is too short to hold them all before it)" : ""));
    }

    private static int Nearest(TrackRoad road, double x, double z)
    {
        var best = 0; var bd = double.MaxValue;
        for (var i = 0; i < road.Count; i++) { var d = Sq(road.X[i] - x) + Sq(road.Z[i] - z); if (d < bd) { bd = d; best = i; } }
        return best;
    }

    // Two parts of a raised road this far apart in height are different levels: a line across one is not crossed on the other (the
    // engine counts a line within 1200 of its height, RACEMOD.CPP RACE_LINE_REACH).
    private const double RaisedLevelApart = 2400;

    // A sprint's finish line (RaceTrackPlan.Finish): across the road at point k, a cell past its rail or verge either side, the way the
    // route runs there, at the road's height. No checkpoints: the route is driven once, start to finish.
    private static void FinishLine(TrackRoad r, int k, RaceTrackOptions o, RaceTrackReport report)
    {
        double x = r.X[k], z = r.Z[k], dx = r.Tx[k], dz = r.Tz[k];
        var half = (r.Raised is { } up && up[k] ? r.RaisedHalfs?[k] ?? o.RaisedHalfWidth : r.VergeHalf) + 1;
        double nx = -dz, nz = dx;
        report.FinishLine = (x - nx * half, z - nz * half, x + nx * half, z + nz * half, dx, dz);
        report.FinishLineHeight = r.H[k];
        report.Notes.Add($"a sprint: the finish line at cell ({x:0.0}, {z:0.0}), height {r.H[k]:0}, {r.S[k] - (report.StartLine.Count > 0 ? r.S[Nearest(r, report.StartLine[0].X, report.StartLine[0].Z)] : 0):0} cells from the start line; no checkpoints");
    }

    private static void PlaceCheckpoints(List<TrackRoad> roads, RaceTrackReport report, RaceTrackOptions o, bool planned = false)
    {
        if (report.StartLine.Count == 0) return;
        var r = roads[0]; var n = r.Count;
        var i0 = Nearest(r, report.StartLine[0].X, report.StartLine[0].Z); var s0 = r.S[i0];
        double Ahead(int k) { var d = r.S[k] - s0; return d < 0 ? d + r.Length : d; }
        var avoid = new List<(double From, double To)>();
        if (roads.Count > 1)
        {
            var pit = roads[1];
            var a = Ahead(Nearest(r, pit.X[0], pit.Z[0])); var b = Ahead(Nearest(r, pit.X[pit.Count - 1], pit.Z[pit.Count - 1]));
            var (from, to) = a > r.Length / 2 ? (a, b) : (b, a);          // the pit lane runs through the start line: from before it to after it
            avoid.Add((from - 6, r.Length)); avoid.Add((0, to + 6));
        }
        else avoid.Add((r.Length - 10, r.Length));
        // (every line has its height, so near a bridge's crossing the road over it and the road under it each have their own lines; only a
        // level crossing, where both roads are at one height, keeps them away)
        var crossingClear = !planned && o.Crossing == CrossingStyle.Level ? 45 : 8;
        // (a jump: a line there would be crossed in the air -- out of its height's reach -- or not at all by a car that missed the jump; 20
        // cells either side of the flight, or 4 on a short lap with several jumps, where 20 would leave no room for any line. Where its
        // flight is along the lap: the point nearest the flight's middle may be the other road's, where the lap crosses itself under it.)
        var jumps = report.Jumps;
        var jumpClear = jumps.Sum(j => j.FlightCells + 40) > r.Length / 2 ? 4 : 20;
        foreach (var jump in jumps)
        {
            var j = ((jump.S0 - s0) % r.Length + r.Length) % r.Length + jump.FlightCells / 2;
            double from = j - jump.FlightCells / 2 - jumpClear, to = j + jump.FlightCells / 2 + jumpClear;
            avoid.Add((from, to));
            if (from < 0) avoid.Add((from + r.Length, r.Length));
            if (to > r.Length) avoid.Add((0, to - r.Length));
        }
        // (a carried jump: the car is in the air from its ramp's foot to its landing hill's)
        foreach (var arc in report.ArcJumps)
        {
            double a = Ahead(arc.Foot) - 6, b = Ahead(arc.Foot) + ((arc.S1 - arc.S0) % r.Length + r.Length) % r.Length + 6;
            avoid.Add((a, b));
            if (a < 0) avoid.Add((a + r.Length, r.Length));
            if (b > r.Length) avoid.Add((0, b - r.Length));
        }
        // (a loop: the car is in the air round its ring, a radius either side of its foot, and lines' heights don't reach it there)
        foreach (var l in report.Loops)
        {
            var a = Ahead(l.Point);
            avoid.Add((a - l.Radius - 6, a + l.Radius + 6));
            if (a - l.Radius - 6 < 0) avoid.Add((a - l.Radius - 6 + r.Length, r.Length));
            if (a + l.Radius + 6 > r.Length) avoid.Add((0, a + l.Radius + 6 - r.Length));
        }
        foreach (var c in FindCrossings(r, o))
        {
            // (where both roads leap over the crossing -- the lava lake's -- the jumps keep the lines off it)
            if (jumps.Any(j => OnFlight(r, j, c.I)) && jumps.Any(j => OnFlight(r, j, c.J))) continue;
            foreach (var k in new[] { c.I, c.J }) avoid.Add((Ahead(k) - crossingClear, Ahead(k) + crossingClear));
        }
        bool Clear(int k) => !avoid.Any(v => Ahead(k) >= v.From && Ahead(k) <= v.To);
        bool InsideCube(double x, double z) { var fx = x - Math.Floor(x / 64) * 64; var fz = z - Math.Floor(z / 64) * 64; return fx >= 2 && fx <= 62 && fz >= 2 && fz <= 62; }
        // the corners: the road's bend, smoothed over 3 cells either way; runs of it tighter than CornerRadius one way, from a straight point
        var w = Math.Max(1, (int)Math.Round(3 / o.Spacing));
        var bend = new double[n];
        for (var k = 0; k < n; k++) { double sum = 0; for (var m = -w; m <= w; m++) sum += r.Kappa[At(r, k + m)]; bend[k] = sum / (2 * w + 1); }
        var startAt = Enumerable.Range(0, n).FirstOrDefault(k => Math.Abs(bend[k]) < 1 / CornerRadius);
        var runs = new List<(int From, int Count, int Sign)>();
        for (var j = 0; j < n; j++)
        {
            var k = (startAt + j) % n;
            var sign = Math.Abs(bend[k]) >= 1 / CornerRadius ? Math.Sign(bend[k]) : 0;
            if (sign == 0) continue;
            if (runs.Count > 0 && runs[^1].Sign == sign && j - (runs[^1].From + runs[^1].Count) <= CornerJoin / o.Spacing)
                runs[^1] = (runs[^1].From, j - runs[^1].From + 1, sign);
            else runs.Add((j, 1, sign));
        }
        // each corner's middle -- where it has turned half of its whole turn -- or the middles of its half-circles
        var targets = new List<(int Point, int From, int Count)>();
        foreach (var (from, count, _) in runs)
        {
            var turn = Enumerable.Range(from, count).Sum(j => Math.Abs(r.Kappa[(startAt + j) % n]) * o.Spacing);
            if (turn < CornerTurn) continue;
            var pieces = Math.Max(1, (int)Math.Round(turn / Math.PI));
            for (var q = 0; q < pieces; q++)
            {
                double want = turn * (q + 0.5) / pieces, sum = 0;
                var j = from;
                for (; j < from + count - 1; j++) { sum += Math.Abs(r.Kappa[(startAt + j) % n]) * o.Spacing; if (sum >= want) break; }
                targets.Add(((startAt + j) % n, from, count));
            }
        }
        // the road's edge at a point: its curbs, or a raised road's rail
        double Edge(int k) => r.Raised is { } up && up[k] ? r.RaisedHalfs is { } halfs && k < halfs.Length ? halfs[k] : o.RaisedHalfWidth : r.CurbHalf;
        var placed = new List<int>();
        foreach (var (point, from, count) in targets.OrderBy(t => Ahead(t.Point)))
        {
            if (report.Checkpoints.Count >= MaxCheckpoints) break;
            // the corner's middle, or the nearest place in the corner a line will do (clear of the pits, the jumps, the crossings and the
            // cubes' edges, and reaching past the road's edge on both sides)
            var order = Enumerable.Range(from, count).Select(j => (startAt + j) % n).Where(Clear).OrderBy(k => Math.Abs(r.S[k] - r.S[point]));
            foreach (var k in order)
            {
                var x = r.X[k]; var z = r.Z[k]; var nx = -r.Tz[k]; var nz = r.Tx[k];
                var cx = Math.Floor(x / 64); var cz = Math.Floor(z / 64);
                if (!InsideCube(x, z)) continue;
                if (placed.Any(p => Math.Abs(Ahead(p) - Ahead(k)) < 6)) break;
                var full = Edge(k) + CheckpointPastEdge;
                // (what stops a side short: 0 nothing, 1 the cube's edge, 2 another part of the lap at its height -- the road coming back
                // round a hairpin, another running alongside -- by that part's verge)
                int Stop(double t)
                {
                    var px = x + nx * t; var pz = z + nz * t;
                    if (Math.Floor(px / 64) != cx || Math.Floor(pz / 64) != cz) return 1;
                    var own = r.VergeHalf + 3;
                    for (var m = 0; m < n; m += 2)
                    {
                        var sep = Math.Abs(r.S[m] - r.S[k]); if (r.Closed) sep = Math.Min(sep, r.Length - sep);
                        // (another level -- a bridge over the line's place, a raised road's helix -- doesn't count: the line has its height)
                        if (sep <= own || Math.Abs(r.H[m] - r.H[k]) > RaisedLevelApart) continue;
                        if (Sq(r.X[m] - px) + Sq(r.Z[m] - pz) < Sq(r.VergeHalf)) return 2;
                    }
                    return 0;
                }
                // (a side reaches far enough when it is past the road's edge -- or, stopped by another part of the lap, at least to the edge)
                (double Reach, bool Enough) Side(int way)
                {
                    var reach = 0.0; var stop = 0;
                    for (var t = 0.5; t <= full + 1e-9; t += 0.5)
                    {
                        stop = Stop(way * t);
                        if (stop != 0) break;
                        reach = t;
                    }
                    return (reach, reach >= full - 0.5 || stop == 2 && reach >= Edge(k));
                }
                var (left, leftEnough) = Side(-1); var (right, rightEnough) = Side(1);
                if (!leftEnough || !rightEnough) continue;
                report.Checkpoints.Add((x - nx * left, z - nz * left, x + nx * right, z + nz * right, r.Tx[k], r.Tz[k]));
                report.CheckpointHeights.Add(r.H[k]);
                placed.Add(k);
                break;
            }
        }
        report.Notes.Add($"{report.Checkpoints.Count} checkpoints round the lap, in the middle of {targets.Count} corners (a lap counts once the car has crossed them all, in order)");
    }

    // An opponent's racing line (the race-track mode drives a car along it): a point a cell apart round the lap from the start line, placed
    // across the road where the car is quickest -- wide into a bend, clipping its inside, wide out of it -- with each point kept within
    // `Reach` cells of the middle of the road (the car's middle; its wheels then just reach the curb): first smoothed (each point moved to
    // the middle of its neighbours over windows from 30 cells down to 2, then back within the road), then a local search for the lap time
    // itself. On the inside of a bend a point stays far enough from the bend's centre that the line keeps a radius of `LineTightest` cells
    // at least (the lap's tightest bends are 2.6 cells round the middle of the road, less than `Reach`: the line would fold there). Each
    // opponent leans a little to its own side (`Side`, a weak pull), so the two lines don't lie on each other. With the ground's height
    // there (the deck on the bridge, the flight's arc over the jump), the line's bend radius at each point (the circle through the points
    // three cells either side), and speeds for a reference car, the race car setup's: as fast as its steering takes each bend (the buggy
    // turns at a fixed rate, so a bend of radius R is taken at up to that rate times R) and its top gear, braking and pulling away as hard
    // as it can. The engine plans the speeds again from the player's own car (RACEMOD.CPP PlanSpeeds).
    // Side: the side of the road it leans to (cells); Grid: how many cells behind the start line it started before there was a grid (an
    // old engine's opponent_grid); Top and Grip: its character, its top speed and pull, and its cornering, as shares of the player's car's
    // (the engine file passes them on, RaceCarEngineFile).
    internal sealed record RacingLine(double Side, double Grid, double Top, double Grip);
    internal static readonly RacingLine RacerLine = new(-2.5, 4, 1.0, 1.0);
    // Baldino's rocket car: a little quicker on the straights, a good deal slower in the bends
    internal static readonly RacingLine BaldinoLine = new(2.5, RaceTrackScenes.BaldinoGridBack, 1.02, 0.92);
    // the motorbike: a little slower on the straights, quicker through the bends, down the middle of the road
    internal static readonly RacingLine BikerLine = new(0, RaceTrackScenes.BikerGridBack, 0.97, 1.06);
    private const double Reach = 3.2, SideLean = 0.02, LineTightest = 1.2;
    private static readonly int[] SmoothWindows = { 30, 15, 8, 4, 2 };
    private const int SmoothRounds = 100, SearchSpacing = 4;
    private static readonly double[] SearchSteps = { 1, 0.5, 0.25, 0.1 };
    // the reference car (RaceCarSetup's "Race car"): top gear 34 km/h, steering 110 % (1126 of 4096 a second), acceleration 4000 and brakes
    // 15600 units a second squared, gears topping out at 11, 16, 22, 28 and 34 km/h
    private static readonly double[] ReferenceGears = { 1564, 2276, 3129, 3982, 4836 };
    private const double ReferenceTurn = 1126 * 2 * Math.PI / 4096, ReferenceAccel = 4000, ReferenceBrake = 15600, ReferenceTop = 4836;
    // the engine's gravity on a rollercoaster of a lap (RACEMOD.H RACE_GRAVITY, RACE_OVERSPEED_DRAG, RACE_CLIMB)
    private const double EngineGravity = 5000, EngineOverspeedDrag = 0.5, EngineBankSteer = 2.2, EngineClimb = 0.6;
    // how far inside a raised road's edge its rail keeps a car's middle, cells (RACEMOD.CPP RAISED_INSET, 640 units)
    private const double RailInset = 1.25, RailMargin = 0.6;

    // (how far a racing line keeps from a stripe beside the pit lane: a car's half width and some)
    private const double StripeClear = 1.6;

    private static void PlanRacePath(TrackRoad r, RaceTrackReport report, RaceTrackOptions o, RacingLine line,
        List<(double X, double Z, double Y, double Speed, double Radius)> result, string name)
    {
        if (report.StartLine.Count == 0) return;
        var i0 = Nearest(r, report.StartLine[0].X, report.StartLine[0].Z);
        // (a sprint's line runs from the start line to the route's end, and its ends are its own: W holds an index there instead of
        // going round)
        var open = !r.Closed;
        var count = open ? (r.Count - 1 - i0) / 2 + 1 : r.Count / 2;
        int W(int m) => open ? Math.Clamp(m, 0, count - 1) : ((m % count) + count) % count;
        var cx = new double[count]; var cz = new double[count]; var nx = new double[count]; var nz = new double[count]; var at = new int[count];
        for (var m = 0; m < count; m++)
        {
            var k = (i0 + 2 * m) % r.Count;
            at[m] = k; cx[m] = r.X[k]; cz[m] = r.Z[k]; nx[m] = -r.Tz[k]; nz[m] = r.Tx[k];
        }
        // how far across the road each point may go: `Reach` either way, less on the inside of a bend (the middle of the road's own turn,
        // measured over two cells either side, the sharpest of the five: its centre is on the side the road turns to)
        var lo = new double[count]; var hi = new double[count];
        var reach = Math.Min(Reach, r.AsphaltHalf - 0.3);
        // (on a rollercoaster of a lap the road's rail slows a car it holds, and it holds a car's middle RailInset from the road's edge:
        // the line keeps RailMargin inside that)
        if (report.Gravity is > 0) reach = Math.Min(reach, o.RaisedHalfWidth - RailInset - RailMargin);
        double Turn(int m)
        {
            int a = W(m - 1), b = W(m + 1);
            double ax = cx[m] - cx[a], az = cz[m] - cz[a], bx = cx[b] - cx[m], bz = cz[b] - cz[m];
            var angle = Math.Atan2(ax * bz - az * bx, ax * bx + az * bz);
            return angle / Math.Max(1e-6, (Math.Sqrt(ax * ax + az * az) + Math.Sqrt(bx * bx + bz * bz)) / 2);
        }
        var turns = Enumerable.Range(0, count).Select(Turn).ToArray();
        for (var m = 0; m < count; m++)
        {
            var sharpest = Enumerable.Range(-2, 5).Select(d => turns[W(m + d)]).OrderByDescending(Math.Abs).First();
            var inside = Math.Abs(sharpest) < 1e-6 ? reach : Math.Clamp(1 / Math.Abs(sharpest) - LineTightest, 0, reach);
            // (the normal (-Tz, Tx) is the way the road runs turned a quarter turn the way a positive turn goes, so a positive turn has its
            // centre on the normal's side)
            lo[m] = sharpest > 0 ? -reach : -inside;
            hi[m] = sharpest > 0 ? inside : reach;
            // (beside a stripe -- the Emerald Moon's pit lane -- no further than a car's width from it, on the road's side of it)
            if (r.Stripe is { } st && (st.From <= st.To ? at[m] >= st.From - 12 && at[m] <= st.To + 12 : at[m] >= st.From - 12 || at[m] <= st.To + 12))
            {
                if (st.Offset > 0) hi[m] = Math.Min(hi[m], st.Offset - StripeClear); else lo[m] = Math.Max(lo[m], st.Offset + StripeClear);
                if (lo[m] > hi[m]) lo[m] = hi[m];
            }
        }
        // the offsets across the road, from the middle of the road. (None is held: the race-track mode lines the cars up on grid spots in
        // the qualifying's order and moves each from its spot onto its line over its first cells.)
        var off = new double[count]; var held = new bool[count];
        // the points over a jump's flight: no bend there (the car is in the air, on the flight's arc). Those of the stretch of the lap the
        // flight is over, from 2 cells before it to 2 after -- not the other road where the lap crosses itself under a jump, which a car
        // drives on.
        var flight = new bool[count]; var flown = new JumpInfo?[count];
        for (var m = 0; m < count; m++)
            if (report.Jumps.FirstOrDefault(j => OnFlight(r, j, at[m], 2)) is { } over) { flight[m] = true; flown[m] = over; }

        // 1. smoothed, wide windows first: each point moved to the middle of the points `w` either side of it, then back within the road;
        //    a long bend's line is moved as a whole, which point-by-point curvature sweeps take tens of thousands of rounds to do
        var px = new double[count]; var pz = new double[count];
        var sumX = new double[3 * count + 1]; var sumZ = new double[3 * count + 1];
        foreach (var w in SmoothWindows)
            for (var round = 0; round < SmoothRounds; round++)
            {
                for (var m = 0; m < count; m++) { px[m] = cx[m] + nx[m] * off[m]; pz[m] = cz[m] + nz[m] * off[m]; }
                for (var i = 0; i < 3 * count; i++) { sumX[i + 1] = sumX[i] + px[W(i - count)]; sumZ[i + 1] = sumZ[i] + pz[W(i - count)]; }
                for (var m = 0; m < count; m++)
                {
                    if (held[m]) continue;
                    int from = count + m - w, to = count + m + w + 1;
                    var ax = (sumX[to] - sumX[from]) / (2 * w + 1); var az = (sumZ[to] - sumZ[from]) / (2 * w + 1);
                    var across = (ax - cx[m]) * nx[m] + (az - cz[m]) * nz[m];
                    off[m] = Math.Clamp((across + SideLean * line.Side) / (1 + SideLean), lo[m], hi[m]);
                }
            }

        // (gravity, RaceTrackPlan.Gravity: a raised road's slopes pull at the car, as the engine has it -- RACEMOD.CPP PlanSpeeds, BUGGY.CPP
        // RaceSpeedOnSlope: the slope's pull at each point, and the share of its top speed the engine holds up a climb)
        var gravity = report.Gravity is { } g and > 0 ? g : 0;
        var slopePull = new double[count]; var share = new double[count];
        for (var m = 0; m < count; m++)
        {
            int a = at[m], b = at[W(m + 1)];
            var run = Math.Sqrt(Sq(r.X[b] - r.X[a]) + Sq(r.Z[b] - r.Z[a])) * 512;
            var slope = gravity > 0 && run > 1 ? (r.H[b] - r.H[a]) / run : 0;
            slopePull[m] = -gravity * EngineGravity * slope / Math.Sqrt(1 + slope * slope);
            share[m] = slope > 0 ? Math.Max(0.5, 1 - gravity * EngineClimb * slope) : 1;
        }
        // ... and a banked bend turns it: the car steers quicker by EngineBankSteer per unit of banking (BUGGY.CPP), so the bend counts
        // as one that much wider -- in the radii the engine plans the opponents' speeds from, too
        var bankTurn = new double[count];
        for (var m = 0; m < count; m++) bankTurn[m] = gravity > 0 && r.RoadBank is { } rb ? 1 + EngineBankSteer * Math.Abs(rb[at[m]]) : 1;

        // the lap time of a line (offsets across the road) for this opponent's car, and its speeds and bend radii
        double LapTime(double[] offsets, double[]? speeds = null, double[]? bends = null)
        {
            var qx = new double[count]; var qz = new double[count];
            for (var m = 0; m < count; m++) { qx[m] = cx[m] + nx[m] * offsets[m]; qz[m] = cz[m] + nz[m] * offsets[m]; }
            var v = speeds ?? new double[count]; var ds = new double[count];
            for (var m = 0; m < count; m++)
            {
                int a = W(m - 3), c = W(m + 3), next = W(m + 1);
                double abx = qx[m] - qx[a], abz = qz[m] - qz[a], bcx = qx[c] - qx[m], bcz = qz[c] - qz[m], cax = qx[a] - qx[c], caz = qz[a] - qz[c];
                var cross = Math.Abs(abx * bcz - abz * bcx);
                var bend = flight[m] || cross < 1e-9 ? 1e6 : Math.Min(1e6, Math.Sqrt((abx * abx + abz * abz) * (bcx * bcx + bcz * bcz) * (cax * cax + caz * caz)) / (2 * cross) * 512);
                if (bankTurn[m] > 1) bend = Math.Min(1e6, bend * bankTurn[m]);
                if (bends is not null) bends[m] = bend;
                // (on a rollercoaster of a lap the car steers quicker the faster it goes over its top speed: a bend it can take at that
                // speed it can take at any)
                var steered = ReferenceTurn * line.Grip * bend;
                v[m] = Math.Max(400, gravity > 0 ? steered < ReferenceTop * line.Top ? steered : ReferenceTop * line.Top * 4 : Math.Min(ReferenceTop * line.Top, steered));
                ds[m] = Math.Sqrt(Sq(qx[next] - qx[m]) + Sq(qz[next] - qz[m])) * 512;
            }
            // braking before bends and pulling away after them, two rounds each way (the lap is a loop)
            // (a sprint's from its start to its end, once each way)
            var last = open ? count - 1 : count;
            for (var round = 0; round < 2 && gravity <= 0; round++)
            {
                for (var i = last - 1; i >= 0; i--) v[i] = Math.Min(v[i], Math.Sqrt(Sq(v[W(i + 1)]) + 2 * ReferenceBrake * ds[i]));
                for (var i = 0; i < last; i++) v[W(i + 1)] = Math.Min(v[W(i + 1)], Math.Sqrt(Sq(v[i]) + 2 * ReferenceAccel * line.Top * ReferencePull(v[i]) * ds[i]));
            }
            // ... on a rollercoaster of a lap, with the slopes: the engine up to its share of the top speed, the slope's pull, the drag on
            // speed over the top gear's; and the slope counted when braking
            var top = ReferenceTop * line.Top;
            for (var round = 0; round < 3 && gravity > 0; round++)
            {
                for (var i = 0; i < last; i++)
                {
                    var a = slopePull[i];
                    if (v[i] < top * share[i]) a += ReferenceAccel * line.Top * ReferencePull(v[i] / share[i]);
                    if (v[i] > top) a -= EngineOverspeedDrag * (v[i] - top);
                    var reach2 = Sq(v[i]) + 2 * a * ds[i];
                    if (a < 0 && v[i] >= top * share[i] && reach2 < Sq(top * share[i])) reach2 = Sq(top * share[i]);
                    v[W(i + 1)] = Math.Min(v[W(i + 1)], reach2 > 400.0 * 400.0 ? Math.Sqrt(reach2) : 400);
                }
                for (var i = last - 1; i >= 0; i--)
                    v[i] = Math.Min(v[i], Math.Sqrt(Sq(v[W(i + 1)]) + 2 * Math.Max(ReferenceBrake / 4, ReferenceBrake - slopePull[i]) * ds[i]));
            }
            var time = 0.0;
            for (var m = 0; m < count; m++) time += ds[m] / v[m];
            return time;
        }

        // 2. then a local search for the lap time itself: the line held at points every few cells (straight between them), each moved in
        //    and out by a step, a move kept when the lap is quicker, with smaller steps as it settles
        var controls = Enumerable.Range(0, (count + SearchSpacing - 1) / SearchSpacing).Select(j => j * SearchSpacing).ToArray();
        double[] Expand(double[] c)
        {
            var o = new double[count];
            for (var j = 0; j < controls.Length; j++)
            {
                int m0 = controls[j], m1 = j + 1 < controls.Length ? controls[j + 1] : count;
                var c1 = j + 1 < controls.Length ? c[j + 1] : open ? c[j] : c[0];
                for (var m = m0; m < m1; m++) o[m] = c[j] + (c1 - c[j]) * (m - m0) / (m1 - m0);
            }
            for (var m = 0; m < count; m++) o[m] = held[m] ? line.Side : Math.Clamp(o[m], lo[m], hi[m]);
            return o;
        }
        var smoothed = LapTime(off);
        var ctrl = controls.Select(m => off[m]).ToArray();
        var best = LapTime(Expand(ctrl));
        foreach (var step in SearchSteps)
            for (var round = 0; round < 3; round++)
            {
                var improved = false;
                for (var j = 0; j < controls.Length; j++)
                {
                    if (held[controls[j]]) continue;
                    foreach (var d in new[] { step, -step })
                    {
                        var saved = ctrl[j];
                        ctrl[j] = saved + d;
                        var t = LapTime(Expand(ctrl));
                        if (t < best - 1e-4) { best = t; improved = true; break; }
                        ctrl[j] = saved;
                    }
                }
                if (!improved) break;
            }
        var searched = Expand(ctrl);
        if (best < smoothed) off = searched;

        var speeds = new double[count]; var radius = new double[count];
        var lap = LapTime(off, speeds, radius);
        for (var m = 0; m < count; m++)
        {
            var k = at[m];
            double x = cx[m] + nx[m] * off[m], z = cz[m] + nz[m] * off[m];
            var bank = r.Bridge[k] || r.Deck[k] ? 0 : Math.Clamp(-r.Kappa[k] * o.BankGain, -o.MaxBank, o.MaxBank);
            // (a raised road's own banking is per unit across, the ground's per cell)
            if (r.Raised is { } up && up[k] && r.RoadBank is { } rb) bank = rb[k] * 512;
            var y = r.H[k] + bank * off[m];
            if (flown[m] is { } jump)
            {
                // over the jump: the flight's own arc
                var along = (x - jump.StartX) * jump.DirX + (z - jump.StartZ) * jump.DirZ;
                if (along >= 0 && along <= jump.FlightCells)
                    y = jump.Height + (jump.Drop > 0 ? RaceTrackJumpAnim.DropAt(along / jump.FlightCells, jump.Drop) : RaceTrackJumpAnim.Climb(jump.FlightScale, along));
            }
            result.Add((x, z, y, speeds[m], radius[m]));
        }
        report.Notes.Add($"{name}: {count} points, up to {off.Max(Math.Abs):0.0} cells from the middle of the road, bends down to {radius.Min() / 512:0.0} cells; " +
                         $"with the race car driven perfectly {speeds.Min() * 3.6 / 512:0}-{speeds.Max() * 3.6 / 512:0} km/h, a lap in {lap:0.0} s " +
                         $"(the middle of the road {LapTime(new double[count]):0.0} s)");
    }

    private static double ReferencePull(double v)
    {
        var g = 0;
        while (g < ReferenceGears.Length - 1 && ReferenceGears[g] <= v) g++;
        return Math.Clamp(3800 / ReferenceGears[g], 0.25, 4);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // a raised road widened

    // (2026-10-04) A raised road too narrow to overtake on -- the Elevator Platform's, 6.5 cells rail to rail -- is widened to WidenedHalf
    // from its middle to its rail wherever there is room, point by point (its RaisedHalfs, as the Emerald Moon's plan gives its own): no
    // further than its bend lets its inside edge go (BendShare of the radius), clear by WidenedClear of every other part of the lap at a
    // height near its own (another level of a road that winds over itself, a road alongside -- half of the gap each) and of every decor
    // object its space would newly reach (no building the plan's road missed is cleared for a wider one), and over the ground. It keeps
    // the plan's width where it meets the ground road (its ends, WidenKeep cells), at the start line (its gantry) and over a jump's gap,
    // and changes width by WidenRate a cell along at the most, so it widens and narrows evenly.
    private const double WidenedHalf = 4.75, WidenedClear = 0.75, WidenRate = 0.2, BendShare = 0.8, WidenKeep = 8, WidenStartKeep = 10, WidenJumpKeep = 3, WidenLevel = 1800;

    private static void WidenRaised(IslandFile island, TrackRoad r, RaceTrackPlan plan, RaceTrackOptions o, RaceTrackReport report)
    {
        var n = r.Count; var up = r.Raised!; var plain = o.RaisedHalfWidth;
        if (plain >= WidenedHalf) return;
        double Apart(int a, int b) { var d = Math.Abs(r.S[a] - r.S[b]); return r.Closed ? Math.Min(d, r.Length - d) : d; }
        var limit = new double[n];
        var why = new string[n];
        for (var i = 0; i < n; i++) limit[i] = up[i] ? WidenedHalf : plain;
        void Limit(int i, double to, string reason) { if (to < limit[i] - 1e-9) { limit[i] = to; why[i] = reason; } }
        // the plan's width at its ends, its start line and its jumps
        var keepAt = new List<(double S, double Reach)>();
        for (var i = 0; i < n; i++) if (!up[i]) keepAt.Add((r.S[i], WidenKeep));
        if (plan.Start is { } start) keepAt.Add((r.S[PlanPoint(plan, r, start)], WidenStartKeep));
        foreach (var j in report.Jumps) keepAt.Add((j.S0 + j.FlightCells / 2, j.FlightCells / 2 + WidenJumpKeep));
        for (var i = 0; i < n; i++)
            foreach (var (s, reach) in keepAt)
            {
                var d = Math.Abs(r.S[i] - s); if (r.Closed) d = Math.Min(d, r.Length - d);
                if (d <= reach) { Limit(i, plain, "kept"); break; }
            }
        // the bend: the inside edge no further in than BendShare of its radius
        for (var i = 0; i < n; i++)
            if (Math.Abs(r.Kappa[i]) > 1e-6) Limit(i, Math.Max(plain, BendShare / Math.Abs(r.Kappa[i])), "bend");
        // the rest of the lap at a height near its own: each side gets half of the gap between them
        for (var i = 0; i < n; i++)
        {
            if (limit[i] <= plain) continue;
            for (var j = 0; j < n; j += 2)
            {
                if (Apart(i, j) <= 2 * WidenedHalf + 4 || Math.Abs(r.H[i] - r.H[j]) > WidenLevel) continue;
                var d = Math.Sqrt(Sq(r.X[i] - r.X[j]) + Sq(r.Z[i] - r.Z[j]));
                var room = up[j] ? (d - WidenedClear) / 2 : d - r.VergeHalf - WidenedClear;
                if (room < limit[i]) Limit(i, Math.Max(plain, room), up[j] ? "level" : "road");
            }
        }
        // decor objects its space would newly reach (those the plan's road reaches are cleared anyway), and the ground over its slab
        var boxes = new List<(double X0, double Z0, double X1, double Z1, double Y0, double Y1, int Body)>();
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
            foreach (var d in cube.Decors)
                boxes.Add(((cx * (double)IslandFile.CubeSize + d.XMin) / 512, (cz * (double)IslandFile.CubeSize + d.ZMin) / 512,
                           (cx * (double)IslandFile.CubeSize + d.XMax) / 512, (cz * (double)IslandFile.CubeSize + d.ZMax) / 512, d.YMin, d.YMax, d.Body & 0xFFFF));
        var byBody = new Dictionary<int, int>();
        for (var i = 0; i < n; i++)
        {
            if (limit[i] <= plain) continue;
            var lean = Math.Abs(r.RoadBank?[i] ?? 0) * limit[i] * 512;
            double y0 = r.H[i] - lean - RaisedBelow, y1 = r.H[i] + lean + RaisedAbove;
            foreach (var b in boxes)
            {
                if (b.Y1 < y0 || b.Y0 > y1) continue;
                var dx = Math.Max(0, Math.Max(b.X0 - r.X[i], r.X[i] - b.X1)); var dz = Math.Max(0, Math.Max(b.Z0 - r.Z[i], r.Z[i] - b.Z1));
                var dist = Math.Sqrt(dx * dx + dz * dz);
                if (dist <= plain + WidenedClear || dist > limit[i] + WidenedClear) continue;
                Limit(i, Math.Max(plain, dist - WidenedClear - 0.25), "decor");
                byBody[b.Body] = byBody.GetValueOrDefault(b.Body) + 1;
            }
            if (!plan.RaisedCut)
                for (var side = -1; side <= 1; side += 2)
                    for (var w = plain + 0.5; w <= limit[i]; w += 0.5)
                    {
                        var gx = r.X[i] - r.Tz[i] * side * w; var gz = r.Z[i] + r.Tx[i] * side * w;
                        if (IslandOps.Altitude(island, gx * 512, gz * 512) is { } ground && ground > r.H[i] - lean - RaisedBelow) { Limit(i, Math.Max(plain, w - 0.5), "ground"); break; }
                    }
        }
        // evenly: no faster than WidenRate a cell along
        var half = new double[n];
        for (var i = 0; i < n; i++)
        {
            var h = limit[i];
            for (var j = 0; j < n; j++) h = Math.Min(h, limit[j] + WidenRate * Apart(i, j));
            half[i] = Math.Max(plain, h);
        }
        r.RaisedHalfs = half;
        if (Environment.GetEnvironmentVariable("RT_WIDEN_DEBUG") == "1")
            report.Notes.Add("  widening held by decor bodies: " + string.Join(", ", byBody.OrderByDescending(kv => kv.Value).Take(12).Select(kv => $"{kv.Key} x{kv.Value}")));
        var raised = Enumerable.Range(0, n).Where(i => up[i]).ToList();
        var wide = raised.Count(i => half[i] >= WidenedHalf - 0.01);
        report.Notes.Add($"the raised road widened from {plain:0.##} to {WidenedHalf:0.##} cells either side of its middle along {100.0 * wide / Math.Max(1, raised.Count):0}% of it" +
                         $" ({raised.Average(i => half[i]):0.##} on average, {raised.Min(i => half[i]):0.##} at the least); held narrower by: " +
                         string.Join(", ", raised.Where(i => why[i] is not null && limit[i] < WidenedHalf - 0.01).GroupBy(i => why[i]).OrderByDescending(g => g.Count())
                             .Select(g => $"{g.Key} {100.0 * g.Count() / raised.Count:0}%")));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // decor objects in the way

    // A raised road's own space: from under its slab to over a car's roof.
    private const double RaisedBelow = 320, RaisedAbove = 1400;

    // The island's own decors its record says go whatever the road does (RaceTrackIsland.DropDecors): the pieces placed at each origin.
    private static void DropDecors(IslandFile island, RaceTrackOptions o, RaceTrackReport report)
    {
        foreach (var (cx, cz, x, z) in o.Island.DropDecors ?? Array.Empty<(int, int, int, int)>())
        {
            if (island.CubeAt(cx, cz) is not { } cube) continue;
            foreach (var d in cube.Decors.Where(d => d.X == x && d.Z == z).ToList())
            {
                cube.Decors.Remove(d);
                report.Removed.Add((cx, cz, d.Body & 0xFFFF, "dropped"));
                report.DecorsRemoved++;
                report.Notes.Add($"decor body {d.Body & 0xFFFF} at ({x}, {z}) in cube ({cx},{cz}) taken away (the island's record says so)");
            }
        }
    }

    // `asIs`: bodies kept with their boxes as they are -- a drive-through hut's parts (RaceTrackDriveThrough), its walls beside the road and
    // its roof over it, whose boxes the build made itself
    private static void ClearDecors(IslandFile island, RoadIndex index, RaceTrackOptions o, RaceTrackReport report, List<TrackRoad>? roads = null, int[]? keepBodies = null,
        int? keepAbove = null, HashSet<int>? asIs = null)
    {
        var taken = new List<(int Cx, int Cz, IslandDecor D)>();
        bool Protected(int body) => o.Island.OldTrackCube is not null && o.ProtectedBodies.Contains(body);
        var keepSet = keepBodies?.ToHashSet() ?? new HashSet<int>();
        bool Kept(IslandDecor d) => keepSet.Contains(d.Body & 0xFFFF) || keepAbove is { } high && d.YMax >= high;
        var raised = roads is { Count: > 0 } && roads[0].Raised is not null;
        // (a raised road wider than a ground road's verge -- the Emerald Moon's, as wide as the reactor's dish -- is looked for further out)
        var look = raised ? Math.Max(8, (roads![0].RaisedHalfs is { Length: > 0 } wide ? wide.Max() : o.RaisedHalfWidth) + 1.5) : 8;
        var cut = 0;
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
        {
            var remove = new List<IslandDecor>();
            foreach (var d in cube.Decors)
            {
                var body = d.Body & 0xFFFF;
                if (Protected(body)) continue;
                var x0 = (int)Math.Floor((cx * (double)IslandFile.CubeSize + d.XMin) / 512); var x1 = (int)Math.Floor((cx * (double)IslandFile.CubeSize + d.XMax) / 512);
                var z0 = (int)Math.Floor((cz * (double)IslandFile.CubeSize + d.ZMin) / 512); var z1 = (int)Math.Floor((cz * (double)IslandFile.CubeSize + d.ZMax) / 512);
                var hit = false;
                // (the lowest raised road whose space the decor's box reaches into)
                var roof = double.MaxValue;
                for (var z = z0; z <= z1 && (!hit || raised); z++)
                for (var x = x0; x <= x1 && (!hit || raised); x++)
                    foreach (var h in index.Near(x + 0.5, z + 0.5, look, raised ? 8 : 2))
                    {
                        // a raised road passes over what stands on the ground: only what reaches into its own space is in its way
                        if (h.Raised)
                        {
                            // (a carried jump's flight: over whatever stands under it, the Emerald Moon's reactor)
                            if (h.Arc) continue;
                            var half = h.Half > 0 ? h.Half : o.RaisedHalfWidth;
                            var lean = Math.Abs(h.Cross) * half * 512;
                            if (h.Dist > half + 0.75 || d.YMax < h.H - lean - RaisedBelow || d.YMin > h.H + lean + RaisedAbove) continue;
                            roof = Math.Min(roof, h.H - lean);
                            hit = true;
                        }
                        else if (h.Dist <= (h.Road == 0 ? 6.5 : 4.5)) hit = true;
                    }
                if (!hit) continue;
                if (asIs?.Contains(body) == true) continue;
                if (Kept(d))
                {
                    // it stays: its collision box is cut down to end under the road (the box is a box, a statue isn't: the plan keeps
                    // the road clear of the statue itself)
                    if (roof < double.MaxValue) { d.YMax = Math.Max(d.YMin + 1, (int)Math.Floor(roof - RaisedBelow - 60)); cut++; }
                    else report.Notes.Add($"decor body {body} in cube ({cx},{cz}) stands on the road and is kept (the plan says so)");
                    continue;
                }
                var removable = o.RemovableBodies.Contains(body);
                if (!removable && !o.RemoveSolidDecors) { report.Notes.Add($"solid decor left on the road: body {body} in cube ({cx},{cz})"); continue; }
                remove.Add(d);
                report.Removed.Add((cx, cz, body, removable ? "prop" : "solid"));
                if (removable) report.DecorsRemoved++; else report.SolidDecorsRemoved++;
            }
            // pieces placed at one origin are one object (a palm is a trunk and a crown, the gantry a beam and two posts): the rest of
            // an object goes with the piece that was on the road, or a bare trunk is left standing by the road
            foreach (var d in cube.Decors.Where(e => !remove.Contains(e) && remove.Any(r => r.X == e.X && r.Y == e.Y && r.Z == e.Z)).ToList())
            {
                var body = d.Body & 0xFFFF;
                if (Protected(body) || Kept(d)) continue;
                remove.Add(d);
                report.Removed.Add((cx, cz, body, "part"));
                report.DecorsRemoved++;
            }
            foreach (var d in remove) { cube.Decors.Remove(d); taken.Add((cx, cz, d)); }
        }
        if (cut > 0) report.Notes.Add($"{cut} kept decor objects' collision boxes cut down to end under the raised road that passes over them");
        ClearRuins(island, index, taken, o, report, Kept);
    }

    // The rest of a structure the road ran through. A building is several decor pieces placed apart (walls, pillars, a roof beam), so
    // clearing the pieces on the road leaves the others standing: a wall on its own by the kerb, a beam in mid-air. Pieces whose boxes
    // touch are one structure; where one lost pieces to the road and all that is left of it are small parts -- no whole building among
    // them -- those parts go too, as far as RuinReach cells from the road. A whole building that only touched a prop on the road keeps
    // everything. (Pieces are compared in island units: a piece that reaches into a second cube is kept in both, and goes from both.)
    //
    // Pieces that stand a little apart are one structure too, as long as they sit at much the same height: Mosquibees Island's plank
    // walkway over a gully is a row of deck slabs half a cell apart with a handrail along each side a thousand units higher, and with
    // touching boxes alone every slab was its own structure -- so the road went through the middle of it and left both halves standing.
    // The gap is kept small: reaching further merged whole hillsides of separate objects into one structure, which then kept a big
    // piece and nothing of it was cleared at all.
    private const double RuinTouch = 0.25, RuinReach = 14, RuinBigPiece = 12;
    private const double RuinGap = 0.6, RuinRise = 1200;

    // A decor held up by nothing: its underside (YMin -- for many decors Y is 0, the body's own heights are YMin..YMax) more than AdriftBy
    // above the highest ground anywhere under its footprint. The island has some of its own -- a roof on its walls, a sign on its post, a
    // plank over a gully, each held up by other decors -- and those stay; what goes is one that rested on the ground before the build and is
    // held up by nothing after it (ClearAdrift). A decor follows the ground under its origin (IslandOps.DecorFollow), and on Citadel Island's
    // fine-weather file a rock whose corner stood where the road's embankment rose was lifted 2,057 units, 1,132 clear of everything under
    // the rest of it, and a sign whose ground the road cut away was left 2,392 up: from the road they looked like loose ground in the air.
    private const int AdriftBy = 400;

    private static bool Adrift(IslandFile island, int cx, int cz, IslandDecor d)
    {
        double ox = cx * (double)IslandFile.CubeSize, oz = cz * (double)IslandFile.CubeSize;
        double? top = null;
        for (var z = d.ZMin; z <= d.ZMax; z += 128)
        for (var x = d.XMin; x <= d.XMax; x += 128)
            if (IslandOps.Altitude(island, ox + x, oz + z) is { } g && (top is null || g > top)) top = g;
        return top is { } t && d.YMin - t > AdriftBy;
    }

    private static HashSet<IslandDecor> AdriftDecors(IslandFile island) =>
        IslandOps.CubeCells(island).SelectMany(c => c.Item3.Decors.Where(d => Adrift(island, c.Item1, c.Item2, d))).ToHashSet();

    // (`asIs`: bodies the build put in the air on purpose -- a drive-through hut's roof over the road -- left where they are)
    private static void ClearAdrift(IslandFile island, HashSet<IslandDecor> before, RaceTrackReport report, HashSet<int>? asIs = null)
    {
        var gone = 0;
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
            foreach (var d in cube.Decors.Where(d => !before.Contains(d) && asIs?.Contains(d.Body & 0xFFFF) != true && Adrift(island, cx, cz, d)).ToList())
            {
                cube.Decors.Remove(d);
                report.Removed.Add((cx, cz, d.Body & 0xFFFF, "adrift"));
                report.DecorsRemoved++;
                gone++;
            }
        if (gone > 0) report.Notes.Add($"{gone} decor objects that rested on the ground and were left held up by nothing once it was reshaped (lifted with it by a corner, or with their ground cut away from under them) removed");
    }

    private static void ClearRuins(IslandFile island, RoadIndex index, List<(int Cx, int Cz, IslandDecor D)> taken, RaceTrackOptions o, RaceTrackReport report,
        Func<IslandDecor, bool>? kept = null)
    {
        if (taken.Count == 0) return;
        (double X0, double Z0, double X1, double Z1, double Y0, double Y1) Box(int cx, int cz, IslandDecor d) =>
            ((cx * (double)IslandFile.CubeSize + d.XMin) / 512, (cz * (double)IslandFile.CubeSize + d.ZMin) / 512,
             (cx * (double)IslandFile.CubeSize + d.XMax) / 512, (cz * (double)IslandFile.CubeSize + d.ZMax) / 512, d.YMin, d.YMax);
        (int Body, long X, long Z) Id(int cx, int cz, IslandDecor d) => (d.Body & 0xFFFF, cx * (long)IslandFile.CubeSize + d.X, cz * (long)IslandFile.CubeSize + d.Z);
        var pieces = new List<(bool Gone, (int, long, long) Id, (double X0, double Z0, double X1, double Z1, double Y0, double Y1) B)>();
        var seen = new HashSet<(int, long, long)>();
        foreach (var (cx, cz, d) in taken) if (seen.Add(Id(cx, cz, d))) pieces.Add((true, Id(cx, cz, d), Box(cx, cz, d)));
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
            foreach (var d in cube.Decors)
                if (seen.Add(Id(cx, cz, d))) pieces.Add((false, Id(cx, cz, d), Box(cx, cz, d)));
        // structures: pieces whose boxes touch (in plan and in height), joined
        var parent = Enumerable.Range(0, pieces.Count).ToArray();
        int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
        bool Touch(int i, int j)
        {
            var a = pieces[i].B; var b = pieces[j].B;
            // touching, or standing a little apart at much the same height (a row of planks, a rail over a deck)
            var gap = a.X0 <= b.X1 + RuinTouch && b.X0 <= a.X1 + RuinTouch && a.Z0 <= b.Z1 + RuinTouch && b.Z0 <= a.Z1 + RuinTouch ? 64.0
                : a.X0 <= b.X1 + RuinGap && b.X0 <= a.X1 + RuinGap && a.Z0 <= b.Z1 + RuinGap && b.Z0 <= a.Z1 + RuinGap ? RuinRise : -1;
            return gap >= 0 && a.Y0 <= b.Y1 + gap && b.Y0 <= a.Y1 + gap;
        }
        for (var i = 0; i < pieces.Count; i++)
        for (var j = i + 1; j < pieces.Count; j++)
            if (Touch(i, j)) parent[Find(i)] = Find(j);
        var ruins = new HashSet<(int, long, long)>();
        foreach (var group in Enumerable.Range(0, pieces.Count).GroupBy(Find))
        {
            var members = group.ToList();
            if (!members.Any(m => pieces[m].Gone)) continue;
            var left = members.Where(m => !pieces[m].Gone).ToList();
            if (left.Count == 0) continue;
            if (left.Any(m => (pieces[m].B.X1 - pieces[m].B.X0) * (pieces[m].B.Z1 - pieces[m].B.Z0) > RuinBigPiece)) continue;
            foreach (var m in left)
            {
                var b = pieces[m].B;
                var near = index.Near((b.X0 + b.X1) / 2, (b.Z0 + b.Z1) / 2, RuinReach + 4, 1);
                if (near.Count > 0 && near[0].Dist <= RuinReach) ruins.Add(pieces[m].Id);
            }
        }
        if (ruins.Count == 0) return;
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
        {
            var gone = cube.Decors.Where(d => ruins.Contains(Id(cx, cz, d)) && kept?.Invoke(d) != true).ToList();
            foreach (var d in gone) { cube.Decors.Remove(d); report.Removed.Add((cx, cz, d.Body & 0xFFFF, "ruin")); report.DecorsRemoved++; }
        }
        report.Notes.Add($"{ruins.Count} pieces left of structures the road ran through (a wall, a pillar, a beam whose supports were on the road) removed too");
    }

    // The retail race track (see OldTrackCube): its decor pieces removed, and every triangle painted as road (the asphalt tile,
    // the white curb texel, the red/gold hatch tile, flat red curb and orange arrow banks) turned into the plain sand of its own
    // infield. Runs before the new lap is painted, so wherever the new road runs over the old one its own paint wins. The
    // ground's shape is left as it is: the old road bed simply becomes a sand strip between its rock banks.
    private static void ClearOldTrack(IslandFile island, RaceTrackOptions o, RaceTrackReport report)
    {
        if (o.OldTrackCube is not { } oc || island.CubeAt(oc.X, oc.Z) is not { } cube) return;
        var pieces = cube.Decors.Where(d => o.OldTrackBodies.Contains(d.Body & 0xFFFF)).ToList();
        foreach (var d in pieces) { cube.Decors.Remove(d); report.Removed.Add((oc.X, oc.Z, d.Body & 0xFFFF, "old track")); }
        var halves = 0;
        if (cube.HasPolygons)
        {
            var sand = new IslandPolygon(0).With(bank: 2, texFlag: 0, polyFlag: 3, sampleStep: 12, codeJeu: 0);
            for (var z = 0; z < IslandCube.Cells; z++)
            for (var x = 0; x < IslandCube.Cells; x++)
            {
                var diagonal = new IslandPolygon(cube.Polygon(x, z, 0)).Diagonal;
                for (var half = 0; half < 2; half++)
                {
                    if (!IsTrackPaint(cube, new IslandPolygon(cube.Polygon(x, z, half)))) continue;
                    cube.SetPolygon(x, z, half, sand.With(diagonal: diagonal).Raw);
                    halves++;
                }
            }
        }
        report.Notes.Add($"the retail race track in cube ({oc.X},{oc.Z}) cleared: {pieces.Count} decor pieces removed, {halves} painted triangles turned back to sand");
    }

    // Whether a ground triangle carries one of the race track's own paints.
    private static bool IsTrackPaint(IslandCube cube, IslandPolygon p)
    {
        if (p.TexFlag == 0) return p.PolyFlag != 0 && p.Bank is 4 or 5;   // red curb, orange arrow (flat)
        var i = p.TextureIndex;
        if (i * 6 + 6 > cube.TextureDefs.Length) return false;
        int u0 = int.MaxValue, u1 = int.MinValue, v0 = int.MaxValue, v1 = int.MinValue;
        for (var k = 0; k < 3; k++)
        {
            var u = cube.TextureDefs[i * 6 + k * 2] >> 8; var v = cube.TextureDefs[i * 6 + k * 2 + 1] >> 8;
            u0 = Math.Min(u0, u); u1 = Math.Max(u1, u); v0 = Math.Min(v0, v); v1 = Math.Max(v1, v);
        }
        bool Inside(int x0, int y0, int x1, int y1) => u0 >= x0 && u1 <= x1 && v0 >= y0 && v1 <= y1;
        return Inside(96, 0, 128, 32) || Inside(179, 154, 181, 156) || Inside(192, 48, 208, 64);   // asphalt, white curb / start line, hatch
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // the ground

    // How much wider than the road's verge each bridge head's level top is (cells each side): a turned deck tile's collision box
    // reaches up to ~0.8 cells past its mesh, and an edge tile's reaches 8 cells out from the centre line.
    private const double LandingExtra = 2;

    // How far past the curb a ramp's (or the water bridge's) level top reaches, cells. Its first cell is plain rock, the rest (and
    // the bridge heads' wider tops past their first cell) the blocking rock wall: a car that drives into Col ground is held back
    // like a wall, but one put onto it -- the engine moves a car up to 0.7 cells sideways when it crosses into the next cube --
    // skates, and a car at the curb skated off the side. The wall is 1.5 cells wide: painted cell by cell, a narrower band had gaps.
    private const double RampShoulder = 2.5;

    // Whether a cell has a cell with no ground drawn (the sea) or no cube at all within `reach` cells.
    private static bool NearSea(Field field, int gx, int gz, int reach)
    {
        for (var dz = -reach; dz <= reach; dz++)
        for (var dx = -reach; dx <= reach; dx++)
            if (!field.Drawn(gx + dx + 0.5, gz + dz + 0.5)) return true;
        return false;
    }

    // How far a cell's highest corner is above its lowest.
    private static double CellRise(Field field, int gx, int gz)
    {
        double lo = double.MaxValue, hi = double.MinValue;
        for (var dz = 0; dz <= 1; dz++)
        for (var dx = 0; dx <= 1; dx++) { var h = field.VertexHeight(gx + dx, gz + dz); lo = Math.Min(lo, h); hi = Math.Max(hi, h); }
        return hi - lo;
    }

    // Whether a vertex touches a cell with no ground drawn (the sea) or no cube at all (off the island).
    private static bool BesideSea(Field field, int gx, int gz)
    {
        for (var dz = -1; dz <= 0; dz++)
        for (var dx = -1; dx <= 0; dx++)
            if (!field.Drawn(gx + dx + 0.5, gz + dz + 0.5)) return true;
        return false;
    }

    // The footprints of the island's decors whose body is one of `bodies` (the buildings a plan keeps), in island cells.
    private static List<(double X0, double Z0, double X1, double Z1)> KeptBoxes(IslandFile island, int[] bodies)
    {
        var set = bodies.ToHashSet();
        var boxes = new List<(double, double, double, double)>();
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
            foreach (var d in cube.Decors.Where(d => set.Contains(d.Body & 0xFFFF)))
                boxes.Add(((cx * (double)IslandFile.CubeSize + d.XMin) / 512, (cz * (double)IslandFile.CubeSize + d.ZMin) / 512,
                           (cx * (double)IslandFile.CubeSize + d.XMax) / 512, (cz * (double)IslandFile.CubeSize + d.ZMax) / 512));
        return boxes;
    }

    // The kept buildings (RaceTrackPlan.TrimKept) the road's surface -- out to its curbs -- still reaches into from one side, where
    // the road is a ground road there (not a deck or a raised road over it, nor a jump's gap) and not inside the building (it runs through,
    // and no shortening helps): each building -- its pieces placed at one origin -- made shallower on that side by what the road takes,
    // up to TrimMost of its depth, its far side (its front) where it was. Each piece's body is copied with its points squeezed toward that
    // far side along the axis -- its own axis, as the decor's turn lays it in the world -- the copy appended to the island's OBL
    // (RaceTrackReport.NewBodies), and the decor's box is squeezed with it.
    private const double TrimMost = 0.4;

    // Returns the bodies it made.
    private static List<int> TrimKept(IslandFile island, List<TrackRoad> roads, int[] keep, RaceTrackOptions o, RaceTrackReport report)
    {
        var madeBodies = new List<int>();
        if (o.SceneryObl is not { } obl || o.NewBodyBase < 0) { report.Notes.Add("WARNING: the island's OBL wasn't counted -- no kept building made shallower"); return madeBodies; }
        var hqr = HqrArchive.Open(obl);
        var set = keep.ToHashSet();
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
            foreach (var group in cube.Decors.Where(d => set.Contains(d.Body & 0xFFFF) && IslandFile.ObjectPageOf(d) == 0).GroupBy(d => (d.X, d.Z)).ToList())
            {
                double ox = cx * (double)IslandFile.CubeSize, oz = cz * (double)IslandFile.CubeSize;
                double x0 = group.Min(d => (ox + d.XMin) / 512), x1 = group.Max(d => (ox + d.XMax) / 512);
                double z0 = group.Min(d => (oz + d.ZMin) / 512), z1 = group.Max(d => (oz + d.ZMax) / 512);
                // the cut each side (west, east, north, south) needs so that no road point's surface -- a disc of its curbs and half a cell --
                // reaches the building any more (infinite where cutting that side can't do it)
                var into = new double[4];
                var any = false;
                foreach (var r in roads)
                    for (var i = 0; i < r.Count; i++)
                    {
                        if (r.Deck.Length > i && r.Deck[i] || r.Void.Length > i && r.Void[i] || r.Raised is { } up && up[i]) continue;
                        // (the road's own surface: a building its curbs only touch -- a door on that side -- is left as it is)
                        double x = r.X[i], z = r.Z[i], reach = r.CurbHalf + 0.05;
                        double dx = Math.Max(Math.Max(x0 - x, 0), x - x1), dz = Math.Max(Math.Max(z0 - z, 0), z - z1);
                        if (dx * dx + dz * dz >= reach * reach) continue;
                        any = true;
                        var spanX = Math.Sqrt(reach * reach - dz * dz); var spanZ = Math.Sqrt(reach * reach - dx * dx);
                        into[0] = Math.Max(into[0], x + spanX >= x1 ? 1e9 : x + spanX - x0);
                        into[1] = Math.Max(into[1], x - spanX <= x0 ? 1e9 : x1 - (x - spanX));
                        into[2] = Math.Max(into[2], z + spanZ >= z1 ? 1e9 : z + spanZ - z0);
                        into[3] = Math.Max(into[3], z - spanZ <= z0 ? 1e9 : z1 - (z - spanZ));
                    }
                if (!any) continue;
                {
                    var side = Enumerable.Range(0, 4).MinBy(k => into[k] / (k < 2 ? x1 - x0 : z1 - z0));
                    var cut = into[side];
                    // (a touch -- under half a cell, the curbs' edge brushing a corner, or a door that faces the road -- is let be)
                    if (cut < 0.5) continue;
                    var alongX = side < 2;
                    var depth = alongX ? x1 - x0 : z1 - z0;
                    if (cut + 0.25 > TrimMost * depth) { report.Notes.Add($"a kept building in cube ({cx},{cz}) at ({group.Key.X}, {group.Key.Z}): the road runs {(cut > 1e8 ? "through" : $"{cut:0.0} cells into")} it, more than it can be made shallower by"); continue; }
                    // the far side stays: world units from the origin along the axis, and the squeeze
                    var anchor = alongX ? (side == 0 ? x1 * 512 - ox : x0 * 512 - ox) - group.Key.X : (side == 2 ? z1 * 512 - oz : z0 * 512 - oz) - group.Key.Z;
                    var k = (depth - cut - 0.25) / depth;
                    var made = new List<string>();
                    foreach (var d in group.ToList())
                    {
                        var body = Squeezed(hqr.Read(d.Body & 0xFFFF), d, alongX, anchor, k);
                        if (body is null) { made.Add($"body {d.Body & 0xFFFF} left (not a body of one bone)"); continue; }
                        var was = d.Body & 0xFFFF;
                        d.Body = o.NewBodyBase + report.NewBodies.Count;
                        madeBodies.Add(d.Body);
                        report.NewBodies.Add(body);
                        if (alongX)
                        {
                            d.XMin = (int)Math.Round(d.X + anchor + (d.XMin - d.X - anchor) * k); d.XMax = (int)Math.Round(d.X + anchor + (d.XMax - d.X - anchor) * k);
                            if (d.XMin > d.XMax) (d.XMin, d.XMax) = (d.XMax, d.XMin);
                        }
                        else
                        {
                            d.ZMin = (int)Math.Round(d.Z + anchor + (d.ZMin - d.Z - anchor) * k); d.ZMax = (int)Math.Round(d.Z + anchor + (d.ZMax - d.Z - anchor) * k);
                            if (d.ZMin > d.ZMax) (d.ZMin, d.ZMax) = (d.ZMax, d.ZMin);
                        }
                        made.Add($"body {was} as {d.Body}");
                    }
                    report.Notes.Add($"a kept building in cube ({cx},{cz}) made {cut + 0.25:0.0} cells shallower on its {new[] { "west", "east", "north", "south" }[side]} side, where the road runs into it ({string.Join(", ", made)})");
                }
            }
        return madeBodies;
    }

    // The plan's kept pieces cut down to walls (RaceTrackPlan.ThinKept): every decor of each body squeezed toward the side named until it is
    // that many cells deep, as TrimKept squeezes a building -- a copy of its body with its points moved, at the end of the island's OBL --
    // so the face on its far side stands where that side was.
    private static List<int> ThinKept(IslandFile island, ThinKeptPiece[] pieces, RaceTrackOptions o, RaceTrackReport report)
    {
        var madeBodies = new List<int>();
        if (o.SceneryObl is not { } obl || o.NewBodyBase < 0) { report.Notes.Add("WARNING: the island's OBL wasn't counted -- no kept piece cut down to a wall"); return madeBodies; }
        var hqr = HqrArchive.Open(obl);
        var sides = new[] { "west", "east", "north", "south" };
        foreach (var piece in pieces)
        {
            var side = Array.IndexOf(sides, piece.Toward?.ToLowerInvariant());
            if (side < 0 || piece.Cells <= 0) { report.Notes.Add($"WARNING: kept piece {piece.Body} to cut down toward \"{piece.Toward}\": not a side (west, east, north, south) or no depth"); continue; }
            var alongX = side < 2;
            foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
                foreach (var d in cube.Decors.Where(d => (d.Body & 0xFFFF) == piece.Body && IslandFile.ObjectPageOf(d) == 0).ToList())
                {
                    // (world units from the decor's origin: the side that stays, and the squeeze)
                    double lo = alongX ? d.XMin - d.X : d.ZMin - d.Z, hi = alongX ? d.XMax - d.X : d.ZMax - d.Z;
                    var depth = (hi - lo) / 512;
                    if (depth <= piece.Cells) continue;
                    var anchor = side % 2 == 1 ? hi : lo;
                    var k = piece.Cells / depth;
                    var body = Squeezed(hqr.Read(piece.Body), d, alongX, anchor, k);
                    if (body is null) { report.Notes.Add($"kept piece {piece.Body} in cube ({cx},{cz}): not a body of one bone, left as it is"); continue; }
                    d.Body = o.NewBodyBase + report.NewBodies.Count;
                    madeBodies.Add(d.Body);
                    report.NewBodies.Add(body);
                    if (alongX)
                    {
                        d.XMin = (int)Math.Round(d.X + anchor + (d.XMin - d.X - anchor) * k); d.XMax = (int)Math.Round(d.X + anchor + (d.XMax - d.X - anchor) * k);
                        if (d.XMin > d.XMax) (d.XMin, d.XMax) = (d.XMax, d.XMin);
                    }
                    else
                    {
                        d.ZMin = (int)Math.Round(d.Z + anchor + (d.ZMin - d.Z - anchor) * k); d.ZMax = (int)Math.Round(d.Z + anchor + (d.ZMax - d.Z - anchor) * k);
                        if (d.ZMin > d.ZMax) (d.ZMin, d.ZMax) = (d.ZMax, d.ZMin);
                    }
                    report.Notes.Add($"kept piece {piece.Body} in cube ({cx},{cz}) cut down to a wall {piece.Cells:0.##} cells deep against its {sides[side]} side (body {d.Body})");
                }
        }
        return madeBodies;
    }

    // A decor's body with its points squeezed toward `anchor` (world units from the decor's origin, along world X or Z) by `k`: the world
    // axis is one of the body's own as the decor's turn lays it (a building's turn is a quarter turn's multiple), found by matching the
    // body's box, turned, to the decor's. Null for a body of more than one bone (its points are its bones' own) or a turn that matches none.
    private static byte[]? Squeezed(byte[] body, IslandDecor d, bool alongX, double anchor, double k)
    {
        var b = (byte[])body.Clone();
        int I(int at) => System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
        if (I(32) > 1) return null;
        int lx0 = I(8), lx1 = I(12), lz0 = I(24), lz1 = I(28);
        // the four quarter turns: world x and z from local x and z
        (int Ax, int Sx, int Az, int Sz)[] turns = { (0, 1, 2, 1), (2, 1, 0, -1), (0, -1, 2, -1), (2, -1, 0, 1) };
        double Error((int Ax, int Sx, int Az, int Sz) t)
        {
            (double Lo, double Hi) W(int axis, int sign) { var (lo, hi) = axis == 0 ? (lx0, lx1) : (lz0, lz1); return sign > 0 ? (lo, hi) : (-hi, -lo); }
            var (wx0, wx1) = W(t.Ax, t.Sx); var (wz0, wz1) = W(t.Az, t.Sz);
            return Math.Abs(wx0 - (d.XMin - d.X)) + Math.Abs(wx1 - (d.XMax - d.X)) + Math.Abs(wz0 - (d.ZMin - d.Z)) + Math.Abs(wz1 - (d.ZMax - d.Z));
        }
        var best = turns.MinBy(Error);
        if (Error(best) > 256) return null;
        var (axis, sign) = alongX ? (best.Ax, best.Sx) : (best.Az, best.Sz);
        var local = anchor * sign;                 // the anchor in the body's own axis
        var off = axis == 0 ? 0 : 4;               // a point's x at 0, z at 4
        short Squeeze(short v) => (short)Math.Round(local + (v - local) * k);
        int points = I(40), p = I(44);
        for (var i = 0; i < points; i++, p += 8)
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(p + off), Squeeze(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(p + off))));
        // (its own box: the axis's two ends)
        int lo = axis == 0 ? 8 : 24;
        var e0 = (int)Math.Round(local + (I(lo) - local) * k); var e1 = (int)Math.Round(local + (I(lo + 4) - local) * k);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(lo), Math.Min(e0, e1));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(lo + 4), Math.Max(e0, e1));
        return b;
    }

    // How far round a kept building the ground stays as it is (RaceTrackPlan.KeepGroundUnder): its doorstep and the path to it.
    private const double KeepGroundMargin = 2;

    // The ground vertices under the kept buildings and KeepGroundMargin round them.
    private static HashSet<(int, int)> KeptGround(IslandFile island, List<(double X0, double Z0, double X1, double Z1)> boxes)
    {
        var set = new HashSet<(int, int)>();
        foreach (var (x0, z0, x1, z1) in boxes)
            for (var gz = (int)Math.Ceiling(z0 - KeepGroundMargin); gz <= (int)Math.Floor(z1 + KeepGroundMargin); gz++)
            for (var gx = (int)Math.Ceiling(x0 - KeepGroundMargin); gx <= (int)Math.Floor(x1 + KeepGroundMargin); gx++)
                set.Add((gx, gz));
        return set;
    }

    // Moves the lap's middle off the buildings the plan keeps (RaceTrackPlan.KeepClear), as KeepOnIsland moves it off the island's edge: each
    // point whose curbs would reach within half a cell of a kept building's footprint is pushed straight away from it by what it lacks --
    // but never nearer the island's edge than the road needs there -- the pushes spread along the road, and the lap resampled. Several
    // rounds, as a push can bring a neighbour nearer another building.
    // (the most a kept building moves the road in one round: one it runs through, or nearly, is given up rather than the road thrown about)
    private const double KeepClearMost = 3.5;

    private static void KeepClear(TrackRoad r, List<(double X0, double Z0, double X1, double Z1)> boxes, Func<double, double, double> edge, RaceTrackOptions o, RaceTrackReport report)
    {
        var need = o.CurbHalfWidth + 0.5;
        var margin = IslandEdgeMargin(o);
        // (the nearest kept building the road runs beside -- not one it runs through, over it on a bridge's deck, or one it can't miss)
        (double D, double Ux, double Uz) Away(double x, double z)
        {
            var best = (D: 1e9, Ux: 0.0, Uz: 0.0);
            foreach (var (x0, z0, x1, z1) in boxes)
            {
                var dx = x < x0 ? x - x0 : x > x1 ? x - x1 : 0; var dz = z < z0 ? z - z0 : z > z1 ? z - z1 : 0;
                var d = Math.Sqrt(dx * dx + dz * dz);
                if (d > 0 && d < best.D) best = (d, dx / d, dz / d);
            }
            return best;
        }
        double Nearest(double x, double z) => boxes.Min(b =>
        {
            var dx = Math.Max(Math.Max(b.X0 - x, 0), x - b.X1); var dz = Math.Max(Math.Max(b.Z0 - z, 0), z - b.Z1);
            return Math.Sqrt(dx * dx + dz * dz);
        });
        var n = r.Count;
        var worstBefore = Enumerable.Range(0, n).Min(i => Away(r.X[i], r.Z[i]).D);
        if (worstBefore >= need) return;
        for (var round = 0; round < 8; round++)
        {
            var px = new double[n]; var pz = new double[n]; var any = false;
            for (var i = 0; i < n; i++)
            {
                var (d, ux, uz) = Away(r.X[i], r.Z[i]);
                if (d >= need) continue;
                var lack = Math.Min(need - d + 0.1, KeepClearMost);
                // (as far as the island's edge lets it go, and not into another kept building)
                bool Blocked(double l)
                {
                    double nx = r.X[i] + ux * l, nz = r.Z[i] + uz * l;
                    var e = edge(nx, nz);
                    if (e < margin && e < edge(r.X[i], r.Z[i])) return true;
                    var o = Nearest(nx, nz);
                    return o < need && o < d + l - 0.05;
                }
                while (lack > 0.05 && Blocked(lack)) lack -= 0.25;
                if (lack <= 0.05) continue;
                px[i] = ux * lack; pz[i] = uz * lack; any = true;
            }
            if (!any) break;
            var w = (int)Math.Round(10 / o.Spacing);
            var sx = new double[n]; var sz = new double[n];
            for (var i = 0; i < n; i++)
            {
                if (px[i] == 0 && pz[i] == 0) continue;
                for (var k = -w; k <= w; k++)
                {
                    var j = At(r, i + k); var f = 0.5 * (1 + Math.Cos(Math.PI * k / (w + 1)));
                    if (Math.Abs(px[i] * f) > Math.Abs(sx[j])) sx[j] = px[i] * f;
                    if (Math.Abs(pz[i] * f) > Math.Abs(sz[j])) sz[j] = pz[i] * f;
                }
            }
            var pts = new List<(double X, double Z)>();
            for (var i = 0; i < n; i++) pts.Add((r.X[i] + sx[i], r.Z[i] + sz[i]));
            var moved = Resample(r.Name, pts, o, closed: r.Closed, r.Planned);
            r.X = moved.X; r.Z = moved.Z; r.Planned = moved.Planned; n = r.Count;
            Geometry(r, o);
        }
        var worstAfter = Enumerable.Range(0, r.Count).Min(i => Away(r.X[i], r.Z[i]).D);
        report.Notes.Add($"the lap moved off the buildings the plan keeps: its middle came within {worstBefore:0.0} cells of one, now {worstAfter:0.0} (its curbs need {need:0.0})" +
                         (worstAfter < need - 0.25 ? " -- the island's edge leaves no more room: where it is nearer, the curbs reach the building" : ""));
    }

    private static void ModifyGround(IslandFile island, Field field, RoadIndex index, List<TrackRoad> roads, RaceTrackOptions o, RaceTrackReport report,
        HashSet<(int, int)>? keptGround = null)
    {
        var b = index.Bounds;
        var maxReach = roads.Max(r => r.VergeHalf + r.Blend);
        var updates = new List<(int Gx, int Gz, double Height, double Weight)>();
        for (var gz = Math.Max(0, b.Z0); gz <= Math.Min(Grid, b.Z1); gz++)
        for (var gx = Math.Max(0, b.X0); gx <= Math.Min(Grid, b.X1); gx++)
        {
            if (!island.HasVertex(gx, gz)) continue;
            var hits = index.Near(gx, gz, maxReach);
            if (hits.Count == 0) continue;
            // the sea's edge: a vertex beside undrawn ground (or at the island's own edge) that is only in the embankment, not
            // under the road (or a bridge head), keeps its natural height, so a fill ends in the island's own cliff instead of a
            // slab in mid-air
            if (!hits.Any(h => !h.Deck && !h.Void && Across(roads[h.Road], h) <= roads[h.Road].VergeHalf + (h.Landing ? LandingExtra : 0)) && BesideSea(field, gx, gz)) continue;
            // (a building the plan keeps: its ground as it is, but where the road's own surface runs over it)
            if (keptGround is not null && keptGround.Contains((gx, gz)) && !hits.Any(h => !h.Deck && !h.Void && Across(roads[h.Road], h) <= roads[h.Road].CurbHalf)) continue;
            var layers = new List<(double Dist, double Weight, double Target, int Order)>();
            foreach (var hit in hits)
            {
                if (hit.Deck) continue;   // the deck (decor) carries the road here; the ground underneath is the other road's, untouched
                if (hit.Void) continue;   // a gap jump's gap: the car flies over the ground as it is
                var r = roads[hit.Road];
                var verge = hit.Landing ? r.VergeHalf + LandingExtra : hit.Bridge ? r.CurbHalf + RampShoulder : r.VergeHalf;
                var blend = hit.Bridge ? 1.2 : r.Blend;
                var d = Across(r, hit);
                double w;
                if (d <= verge) w = 1;
                else if (d >= verge + blend) continue;
                else { var t = (d - verge) / blend; w = 1 - t * t * (3 - 2 * t); }
                w *= EndFade(r, hit);
                if (w <= 0) continue;
                var bank = BankOf(r, hit, o);
                // a jump ramp's flat top keeps its own height over another road's verge and embankment; a road's own surface (asphalt and
                // curbs) keeps its height over anything
                var rampTop = hit.Jump && hit.Bridge && d <= r.CurbHalf + RampShoulder;
                var order = rampTop ? 1 : d <= r.CurbHalf ? 2 : 0;
                layers.Add((d, w, hit.H + bank * Math.Clamp(hit.Lat, -r.CurbHalf - 1, r.CurbHalf + 1), order));
            }
            if (layers.Count == 0) continue;
            // the nearest road on top: each road's shape is laid over the ground from the farthest to the nearest, so a road's own
            // surface and verge always win over another piece's embankment. (Averaging them instead let a hairpin's other leg,
            // 8-13 cells away, pull the road surface 100-500 units out of level, and kept the bridge heads from being level.) Above
            // that order: a jump ramp's top over other roads' verges, and road surfaces over everything. Near the crossing the other
            // road runs 7-12 cells from the jump's ramps, so its verge reached the ramps' sides; the nearer of the two won, which cut
            // the ramp's edge down to the verge's height and left its curb cells as a steep face of rock.
            var value = field.VertexHeight(gx, gz);
            var keep = 1.0;
            foreach (var (_, w, target, _) in layers.OrderBy(l => l.Order).ThenByDescending(l => l.Dist)) { value = value * (1 - w) + target * w; keep *= 1 - w; }
            updates.Add((gx, gz, value, 1 - keep));
        }
        foreach (var (gx, gz, height, _) in updates) island.SetHeight(gx, gz, (int)Math.Round(height));
        report.Vertices = updates.Count;
        field.Refresh(island);
    }

    // The ground under a raised road is the island's own, but where it comes up near the deck -- a coast road at a cliff's foot, a road
    // along a rim -- the car meets rock beside the road, and the engine's floor is the deck only where it stands well over the ground
    // (RACEMOD.CPP RaisedAt: 300). The lava lake's lap (2026-10-01) ran along its north rim with the rock beside the road above the deck,
    // and the test pilot, cutting a corner, stopped dead against it. There the ground is cut down: RaisedClearance under the deck out to
    // RaisedCutReach cells past its edge, then a bank back up to the ground as it is -- a cutting the road runs along. Not near the ends
    // of the raised road where it rises from the ground road (RaisedCutFromEnd cells): there the deck starts on the ground. There only
    // ground standing over the deck is brought down, to just under it (RaisedFlushUnder) -- 2026-10-06: Polar Island's ramp up to the
    // peak rises from a dip beside a ledge of LBA1's terrain, and the ledge stood through the ramp's left half; neither the cutting (too
    // near the end) nor the ends' flush (the deck well over the dip under its middle) reached it. Not where the ground road is nearer:
    // the ground there is the road's own.
    private const double RaisedClearance = 450, RaisedCutReach = 1, RaisedCutBank = 1.5, RaisedCutFromEnd = 8;
    // And the ends themselves (later on 2026-10-01): the deck there is only a little over the ground, and the ground under it was the
    // island's own -- at the lava lake's dock the deck's first cells hovered over the slope down to the sea (a slit under its end), and
    // at its ramp's foot the hill's toe stood up through the deck (the engine takes the higher of deck and ground: cars rode up it and
    // over the rail). Under each end, as far as the deck stays within RaisedFlush of the ground under its middle, the ground is made the
    // deck's own surface (RaisedFlushUnder beneath it) out to its rails, and eased back to the ground as it is over RaisedFlushBlend
    // cells beyond them; the cutting leaves that ground alone.
    private const double RaisedFlush = 600, RaisedFlushUnder = 50, RaisedFlushBlend = 1.5;    // (15 under: the ground showed through the asphalt at a distance)

    // `keep`: the ground under the plan's kept buildings (KeepGroundUnder), left as it is -- but where it stands up through the deck, brought
    // down to just under it (Citadel Island's storm track, 2026-10-07: the cutting raised the baggage claim's floor 231 at the north end).
    private static void CutUnderRaised(IslandFile island, Field field, TrackRoad r, RaceTrackOptions o, RaceTrackReport report, HashSet<(int, int)>? keep = null)
    {
        var up = r.Raised!; var n = r.Count;
        var inner = o.RaisedHalfWidth + RaisedCutReach;
        const double reach = 6;                                    // cells past the deck's middle the bank may reach
        var flushed = FlushRaisedEnds(island, r, o, report, keep);
        // how far along the raised road each point is from where it meets the ground road
        var fromEnd = new double[n];
        for (var i = 0; i < n; i++)
        {
            fromEnd[i] = double.MaxValue;
            if (!up[i]) continue;
            for (var way = -1; way <= 1; way += 2)
                for (var k = 1; k < n; k++)
                {
                    var j = At(r, i + way * k);
                    if (up[j]) continue;
                    if (!r.Gap[j]) fromEnd[i] = Math.Min(fromEnd[i], k * o.Spacing);
                    break;
                }
        }
        var limit = new Dictionary<(int, int), double>();
        // (near the ends the nearest stretch of deck's, as the ends' flush: a ramp rises fast, and the lowest deck in reach was its foot)
        var endLimit = new Dictionary<(int, int), (double Allowed, double D)>();
        for (var a = 0; a < n; a++)
        {
            var b = At(r, a + 1);
            if (!up[a] || !up[b] || Math.Abs(r.H[a] - r.H[b]) > DropFrom) continue;
            var nearEnd = Math.Min(fromEnd[a], fromEnd[b]) < RaisedCutFromEnd;
            if (nearEnd && (r.Gap[a] || r.Gap[b])) continue;
            double sx = r.X[b] - r.X[a], sz = r.Z[b] - r.Z[a], len2 = sx * sx + sz * sz;
            if (len2 < 1e-12) continue;
            for (var gz = (int)Math.Floor(Math.Min(r.Z[a], r.Z[b]) - reach); gz <= (int)Math.Ceiling(Math.Max(r.Z[a], r.Z[b]) + reach); gz++)
            for (var gx = (int)Math.Floor(Math.Min(r.X[a], r.X[b]) - reach); gx <= (int)Math.Ceiling(Math.Max(r.X[a], r.X[b]) + reach); gx++)
            {
                var t = Math.Clamp(((gx - r.X[a]) * sx + (gz - r.Z[a]) * sz) / len2, 0, 1);
                var d = Math.Sqrt(Sq(gx - r.X[a] - sx * t) + Sq(gz - r.Z[a] - sz * t));
                if (d > reach) continue;
                var deck = r.H[a] + (r.H[b] - r.H[a]) * t;
                if (r.RoadBank is { } rb) deck -= Math.Abs(rb[a]) * o.RaisedHalfWidth * 512;
                var allowed = deck - (nearEnd ? RaisedFlushUnder : RaisedClearance) + Math.Max(0, d - inner) * 512 * RaisedCutBank;
                if (nearEnd)
                {
                    if (!endLimit.TryGetValue((gx, gz), out var near) || d < near.D) endLimit[(gx, gz)] = (allowed, d);
                }
                else limit[(gx, gz)] = limit.TryGetValue((gx, gz), out var was) ? Math.Min(was, allowed) : allowed;
            }
        }
        // (never near the road on the ground: the ground there is the road's own)
        var ground = Enumerable.Range(0, n).Where(i => !up[i] && !r.Gap[i]).ToList();
        var clearOfRoad = r.VergeHalf + r.Blend + 1;
        int cut = 0; double deepest = 0;
        foreach (var ((gx, gz), allowed) in limit)
        {
            if (flushed.Contains((gx, gz))) continue;
            if (island.HeightAt(gx, gz) is not { } h || h <= allowed) continue;
            if (ground.Any(i => Sq(r.X[i] - gx) + Sq(r.Z[i] - gz) < Sq(clearOfRoad))) continue;
            var kept = keep?.Contains((gx, gz)) == true;
            if (kept && h <= allowed + RaisedClearance - RaisedFlushUnder) continue;
            var to = Math.Max(0, (int)Math.Floor(kept ? allowed + RaisedClearance - RaisedFlushUnder : allowed));
            deepest = Math.Max(deepest, h - to);
            island.SetHeight(gx, gz, to); cut++;
        }
        // (near the ends: the ground over the deck, where the deck's points are nearer than the ground road's)
        var deckPoints = Enumerable.Range(0, n).Where(i => up[i] && !r.Gap[i]).ToList();
        double Nearest(List<int> points, int gx, int gz) => points.Count == 0 ? double.MaxValue : points.Min(i => Sq(r.X[i] - gx) + Sq(r.Z[i] - gz));
        int endCut = 0; double endDeepest = 0;
        foreach (var ((gx, gz), (allowed, _)) in endLimit)
        {
            if (flushed.Contains((gx, gz))) continue;
            if (island.HeightAt(gx, gz) is not { } h || h <= allowed) continue;
            if (Nearest(ground, gx, gz) <= Nearest(deckPoints, gx, gz)) continue;
            if (keep?.Contains((gx, gz)) == true && h <= allowed) continue;
            var to = Math.Max(0, (int)Math.Floor(allowed));
            endDeepest = Math.Max(endDeepest, h - to);
            island.SetHeight(gx, gz, to); endCut++;
        }
        if (endCut > 0)
        {
            report.Vertices += endCut;
            report.Notes.Add($"the raised road's ends: the ground standing over the deck brought down under it at {endCut} vertices, {endDeepest:0} at the most");
        }
        if (cut == 0 && endCut == 0 && flushed.Count == 0) return;
        field.Refresh(island);
        if (cut == 0) return;
        report.Vertices += cut;
        report.Notes.Add($"the raised road: the ground cut down under and beside it at {cut} vertices, {deepest:0} at the most, to stand {RaisedClearance:0} under the deck");
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // painting

    // Wall: rock that blocks (Col); Rock: the same rock look, passable
    private enum Kind { None, Asphalt, RedCurb, WhiteCurb, Sand, Hatch, Wall, Rock, Arrow, Start }

    private static HashSet<(int, int)> PaintRoad(IslandFile island, Field field, RoadIndex index, List<TrackRoad> roads, RaceTrackOptions o, RaceTrackReport report, int startIndex, bool planned = false)
    {
        var b = index.Bounds;
        var kinds = new Dictionary<(int, int), Kind>();
        // (the cells on one road alone, plain road -- no deck, jump, bridge or landing -- for the smooth kerbs: KerbCells)
        var plain = new Dictionary<(int, int), RoadHit>();
        var maxReach = roads.Max(r => r.VergeHalf) + LandingExtra + 0.8;
        for (var gz = Math.Max(0, b.Z0); gz < Math.Min(Grid, b.Z1); gz++)
        for (var gx = Math.Max(0, b.X0); gx < Math.Min(Grid, b.X1); gx++)
        {
            if (island.CubeAt(gx / IslandCube.Cells, gz / IslandCube.Cells) is not { HasPolygons: true }) continue;
            var hits = index.Near(gx + 0.5, gz + 0.5, maxReach, 2);
            if (hits.Count == 0) continue;
            // the road whose surface the cell is really part of: a pit lane cell belongs to the pit lane when it is inside its asphalt
            RoadHit? chosen = null; var ck = Kind.None;
            foreach (var hit in hits)
            {
                var r = roads[hit.Road]; var d = Across(r, hit);
                Kind k;
                if (hit.Deck) continue;   // the deck's own polygons draw the road here, not the ground
                if (hit.Void) continue;   // (a gap jump's gap keeps its own ground)
                if (hit.Gap) { if (d <= r.VergeHalf) k = Kind.Sand; else continue; }
                else if (hit.Lip && d <= r.AsphaltHalf) k = Kind.Hatch;
                else if (hit.UnderDeck) { if (d <= r.VergeHalf + LandingExtra) k = Kind.Rock; else continue; }
                else if (d <= r.AsphaltHalf) k = Kind.Asphalt;
                else if (d <= r.CurbHalf) k = (int)Math.Floor(hit.S / 1.6) % 2 == 0 ? Kind.RedCurb : Kind.WhiteCurb;
                else if (hit.Landing) { if (d <= r.VergeHalf + LandingExtra) k = d <= r.CurbHalf + 1 ? Kind.Rock : Kind.Wall; else continue; }
                else if (hit.Bridge) { if (d <= r.CurbHalf + RampShoulder) k = d <= r.CurbHalf + 1 ? Kind.Rock : Kind.Wall; else continue; }
                else if (d <= r.VergeHalf) k = Kind.Sand;
                else continue;
                // (a jump ramp's rock shoulders over another road's sand verge)
                var rank = k == Kind.Asphalt ? 3 : k is Kind.RedCurb or Kind.WhiteCurb ? 2 : k is Kind.Rock or Kind.Wall && hit.Jump && hit.Bridge ? 1.5 : 1;
                var crank = ck == Kind.Asphalt ? 3 : ck is Kind.RedCurb or Kind.WhiteCurb ? 2 : ck == Kind.None ? 0 : chosen is { Jump: true, Bridge: true } && ck is Kind.Rock or Kind.Wall ? 1.5 : 1;
                if (rank > crank) { chosen = hit; ck = k; }
            }
            if (chosen is null || ck == Kind.None) continue;
            // the jump's ends, where a ramp drops to the gap, are a face of blocking rock
            if (chosen.Value.Jump && CellRise(field, gx, gz) > 250) ck = Kind.Wall;
            // (a planned lap's verge that holds a step -- the edge of a terrace, where the next part of the lap lies far below or above -- is
            // blocking rock too: the engine lifts a car onto any higher ground it drives into, so a sand step was a ramp up the cliff)
            // (A jump's warning stripe is hatched but it is road -- the car drives over it to the lip -- so neither of these makes it a wall:
            // Citadel Island's storm track, reversed, has its take-off ramp built over the harbour's basin, and the stripe's cells within
            // two cells of that water became blocking rock across the ramp. A car taking off hit it mid-flight, the engine started Twinsen
            // skating back down the ramp -- an animation nothing interrupts -- and the flight was dropped until the skid ended.)
            var stripe = chosen.Value.Lip && ck == Kind.Hatch;
            // (and a rock shoulder that holds one: under each end of a bridge's deck the ground is raised to the deck and drops straight
            // to the ground under its middle, and on Mosquibees Island a car on the road beneath drove up that 5,000-high face onto the
            // landing. A ramp's shoulders rise far less than SteepVerge a cell.)
            if (planned && !stripe && ck is Kind.Sand or Kind.Hatch or Kind.Rock && CellRise(field, gx, gz) > SteepVerge) ck = Kind.Wall;
            // banked bends: the outside verge is hatched
            if (ck == Kind.Sand && Math.Abs(chosen.Value.Kappa) > 0.05 && chosen.Value.Lat * chosen.Value.Kappa < 0) ck = Kind.Hatch;
            // a verge within 2 cells of the sea (or the island's edge) is a blocking rock wall: where the road runs along a cliff top
            // (the west rim of the old track's cube, 8 cells from the edge) a car running wide stopped at the world's edge with its
            // nose over the drop
            if (!stripe && ck is Kind.Sand or Kind.Hatch && NearSea(field, gx, gz, 2)) ck = Kind.Wall;
            kinds[(gx, gz)] = ck;
            if (ck is Kind.Asphalt or Kind.RedCurb or Kind.WhiteCurb or Kind.Sand or Kind.Hatch && Plain(chosen.Value)
                && hits.Count(h => !h.Deck && Across(roads[h.Road], h) <= roads[h.Road].CurbHalf + 1.5) == 1)
                plain[(gx, gz)] = chosen.Value;
        }
        // the start line first, so the arrows (which only go where every cell is plain asphalt) keep clear of it
        if (startIndex >= 0) MarkStartLine(roads[0], startIndex, kinds, report);
        var arrows = new Dictionary<(int, int), ArrowCell>();
        MarkArrows(island, roads[0], kinds, arrows, o, report);
        // the kerbs drawn smooth: a texture of their own in the island's page, mapped onto the triangles along them (KerbCells)
        var kerbCells = new Dictionary<(int, int), KerbCell>();
        if (plain.Count > 0)
        {
            if (RaceTrackTextures.KerbTexture(island, o.Theme) is { } kerbAt)
            {
                // (on an island with more ground pages -- POLAR.ILE -- a cube has room for 1024 texture definitions: the road's coordinates
                // snapped, so a straight kerb's triangles share theirs)
                kerbCells = KerbCells(island, index, roads, kinds, arrows, plain, kerbAt, o.Theme, island.GroundPages.Count > 0 ? KerbSnap : null);
                var triangles = kerbCells.Values.Sum(c => c.Uvs.Count(u => u is not null));
                report.Notes.Add($"the kerbs drawn smooth: {triangles} triangles along them carry a kerb texture of their own (at ({kerbAt.X},{kerbAt.Y}) in the ground's page, " +
                                 $"mapped by the road's own coordinates), {kerbCells.Count} cells cut the way the road runs");
            }
            else report.Notes.Add("the kerbs are painted cell by cell: the island's ground texture has no free block for the kerb texture");
        }
        var painter = new Painter(island, o.Theme);
        foreach (var ((gx, gz), kind) in kinds)
        {
            if (kerbCells.TryGetValue((gx, gz), out var kerb)) painter.PaintKerb(gx, gz, kerb);
            else painter.Paint(gx, gz, kind, kind == Kind.Asphalt && arrows.TryGetValue((gx, gz), out var arrow) ? arrow : null);
            report.Cells++;
            if (kind is Kind.Wall or Kind.Rock) report.BridgeCells++;
        }
        return kinds.Keys.ToHashSet();
    }

    // Plain road: not a deck's, a jump's or a landing's (a walled road -- Bridge: over water or a valley, or cut into a cliff -- is, its
    // verge rock).
    private static bool Plain(RoadHit h) => !h.Deck && !h.Void && !h.Gap && !h.Lip && !h.UnderDeck && !h.Landing && !h.Jump && !h.Raised && !h.Arc;

    // A cell along a kerb, drawn triangle by triangle: cut the way the road runs (Diagonal), and each of its two triangles either plain
    // paint (Kinds: the asphalt, or the verge -- sand, or a banked bend's hatching) or, reaching the kerb, its kerb texture's corners
    // (Uvs) over the verge's flat colour (Under: the texture's colour 0 lets it show).
    private sealed record KerbCell(bool Diagonal, Kind[] Kinds, ushort[]?[] Uvs, (int Bank, int Pos) Under);

    private static readonly (int X, int Z)[] CellCorners = { (0, 0), (0, 1), (1, 1), (1, 0) };
    private static readonly int[][] CellHalves = { new[] { 0, 1, 2 }, new[] { 2, 3, 0 }, new[] { 3, 0, 1 }, new[] { 1, 2, 3 } };

    // The kerbs drawn smooth (2026-10-05: painted cell by cell, a kerb a cell wide stepped along every bend). Each cell near a kerb on
    // one plain road gets the road's coordinates at its four corners -- how far across it (from its middle) and how far along it -- and
    // is cut along whichever diagonal keeps both its triangles within a cell across the road (a cell cut against the road's way reaches
    // 1.4 cells across). A triangle short of the kerb is asphalt, one past it the verge; one that reaches it is drawn as the verge's flat
    // colour with the kerb texture over it (RaceTrackTextures.KerbTexture: asphalt, the kerb's red and white blocks, and nothing past
    // it), mapped corner by corner from the road's coordinates -- so the kerb's edges and the blocks' ends run where the road says, to a
    // tenth of a cell, not along the cells.
    // `snap`: the road's coordinates at the corners rounded to it (cells), and the kerb's blocks KerbSnapBlock cells long -- its red and
    // white a whole number of cells -- so a kerb's triangles share texture definitions wherever the road runs straight, every
    // 2 * KerbSnapBlock cells (2026-10-06: Polar Island, twice LBA1's size, the curve through the plan's points wavering by a hundredth of a
    // cell and the blocks 1.6 cells long, had a definition a triangle -- 1,815 for 1,871 in its busiest cube -- and an island with more
    // ground pages has room for 1,024)
    private const double KerbSnap = 1.0 / 16, KerbSnapBlock = 2;

    private static Dictionary<(int, int), KerbCell> KerbCells(IslandFile island, RoadIndex index, List<TrackRoad> roads, Dictionary<(int, int), Kind> kinds,
        Dictionary<(int, int), ArrowCell> arrows, Dictionary<(int, int), RoadHit> plain, (int X, int Y) texture, RaceTrackTheme theme, double? snap = null)
    {
        const int T = RaceTrackTextures.KerbTexels;
        var cells = new Dictionary<(int, int), KerbCell>();
        var rockFlat = Common(island, theme, theme.Rock, theme.Sand);
        double Sep(TrackRoad r, double a, double b)
        {
            var d = a - b;
            if (r.Closed) { d = (d % r.Length + r.Length) % r.Length; if (d > r.Length / 2) d -= r.Length; }
            return d;
        }
        foreach (var ((gx, gz), hit) in plain)
        {
            if (!kinds.TryGetValue((gx, gz), out var kind) || kind is Kind.Start or Kind.Arrow or Kind.Wall || arrows.ContainsKey((gx, gz))) continue;
            var r = roads[hit.Road];
            double inner = r.AsphaltHalf, outer = r.CurbHalf;
            if (Math.Abs(outer - inner - 1) > 0.05) continue;
            var across = Across(r, hit);
            if (across < inner - 1.25 || across > outer + 1.25) continue;
            // the corners in the road's coordinates (the same stretch of it as the cell's middle, on the same side)
            var d = new double[4]; var s = new double[4]; var ok = true;
            for (var k = 0; k < 4 && ok; k++)
            {
                var (ox, oz) = CellCorners[k];
                RoadHit? best = null; var bestSep = 6.0;
                foreach (var n in index.Near(gx + ox, gz + oz, outer + 3, 4))
                {
                    if (n.Road != hit.Road) continue;
                    var sep = Math.Abs(Sep(r, n.S, hit.S));
                    if (sep < bestSep) { bestSep = sep; best = n; }
                }
                if (best is not { } c || (c.Lat >= 0) != (hit.Lat >= 0)) { ok = false; break; }
                d[k] = Across(r, c);
                s[k] = hit.S + Sep(r, c.S, hit.S);
                if (snap is { } q) { d[k] = Math.Round(d[k] / q) * q; s[k] = Math.Round(s[k] / q) * q; }
            }
            if (!ok) continue;
            double Spread(int[] t) => t.Max(c => d[c]) - t.Min(c => d[c]);
            double Reach(bool diagonal) => Math.Max(Spread(CellHalves[diagonal ? 2 : 0]), Spread(CellHalves[diagonal ? 3 : 1]));
            var cut = Reach(true) < Reach(false);
            // (a walled road's verge is rock -- its wall, further out, keeps its own cells)
            var verge = hit.Bridge ? Kind.Rock : kind == Kind.Hatch || Math.Abs(hit.Kappa) > 0.05 && hit.Lat * hit.Kappa < 0 ? Kind.Hatch : Kind.Sand;
            var halves = new Kind[2]; var uvs = new ushort[]?[2];
            for (var half = 0; half < 2 && ok; half++)
            {
                var t = CellHalves[(cut ? 2 : 0) + half];
                double lo = t.Min(c => d[c]), hi = t.Max(c => d[c]);
                if (hi <= inner) { halves[half] = Kind.Asphalt; continue; }
                if (lo >= outer) { halves[half] = verge; continue; }
                // (reaching further across than the texture: the cell keeps its own paint)
                if (lo < inner - 1.02 || hi > outer + 1.02) { ok = false; break; }
                halves[half] = Kind.RedCurb;
                var s0 = t.Min(c => s[c]);
                // (the texture's blocks are KerbBlock cells long at T texels a cell; snapped, KerbSnapBlock)
                var block = snap is null ? RaceTrackTextures.KerbBlock : KerbSnapBlock;
                var along = T * RaceTrackTextures.KerbBlock / block;
                var period = 2 * block;
                var phase = (s0 % period + period) % period;
                var uv = new ushort[6];
                for (var i = 0; i < 3; i++)
                {
                    var c = t[i];
                    double x = (s[c] - s0 + phase) * along, y = (d[c] - (inner - 1)) * T;
                    uv[i * 2] = (ushort)Math.Clamp((int)Math.Round((texture.X + x) * 256), texture.X * 256 + 24, (texture.X + RaceTrackTextures.KerbWide) * 256 - 24);
                    uv[i * 2 + 1] = (ushort)Math.Clamp((int)Math.Round((texture.Y + y) * 256), texture.Y * 256 + 24, (texture.Y + 3 * T) * 256 - 24);
                }
                uvs[half] = uv;
            }
            if (!ok) continue;
            // (beside a banked bend's hatching the kerb's edge is sand, the hatching from the next triangle out: the hatching's own colour
            // there read as the kerb's red running on round its white blocks)
            cells[(gx, gz)] = new KerbCell(cut, halves, uvs, verge == Kind.Rock ? rockFlat : theme.Sand);
        }
        return cells;
    }

    // A flat colour for a textured verge (a walled road's rock), where the kerb texture shows the verge's colour beside it: the ramp whose colour at the ground's usual light (its 9th: a flat colour shows its ramp from the 11th, less the light,
    // as the red kerb shows 73) is nearest the tile's average -- the tile's most common colour's ramp without the palette. (Mosquibees
    // Island's rock by its most common colour was a dark brown band beside the lighter rock.)
    private static (int Bank, int Pos) Common(IslandFile island, RaceTrackTheme theme, (int X, int Y, int W, int H) h, (int Bank, int Pos) none)
    {
        if (h.W == 0) return none;
        var counts = new int[256];
        for (var y = h.Y; y < h.Y + h.H; y++)
        for (var x = h.X; x < h.X + h.W; x++) counts[island.GroundTexture[y * 256 + x]]++;
        if (theme.Palette is not { } pal)
        {
            var common = Array.IndexOf(counts, counts.Max());
            return (common / 16, common % 16);
        }
        double r = 0, g = 0, b = 0, n = 0;
        for (var i = 1; i < 256; i++) { r += counts[i] * pal[i * 3]; g += counts[i] * pal[i * 3 + 1]; b += counts[i] * pal[i * 3 + 2]; n += counts[i]; }
        if (n == 0) return none;
        r /= n; g /= n; b /= n;
        var best = 0; var bd = double.MaxValue;
        for (var bank = 1; bank < 15; bank++)
        {
            var j = bank * 16 + 9;
            var d = Sq(pal[j * 3] - r) + Sq(pal[j * 3 + 1] - g) + Sq(pal[j * 3 + 2] - b);
            if (d < bd) { bd = d; best = bank; }
        }
        return (best, 11);
    }

    // Triangles outside the painted road that still carry the retail rock's blocking bit (Col) although the shaping has left them
    // nearly flat: invisible walls just past the verge (a car running wide at a corner stopped dead on a grey rock patch in the
    // sand). Where the shaping moved a triangle's ground (a corner by 50 units or more) and it is now walkable, the bit is
    // cleared, and a rock-textured triangle left nearly flat is painted as the sand around it.
    private static void ClearStaleCol(IslandFile island, Field natural, Field field, RoadIndex index, HashSet<(int, int)> painted, RaceTrackReport report)
    {
        var b = index.Bounds;
        var sand = new IslandPolygon(0).With(bank: 2, texFlag: 0, polyFlag: 3, sampleStep: 12, codeJeu: 0);
        IslandPolygon? At(int gx, int gz, int half) =>
            island.CubeAt(gx / IslandCube.Cells, gz / IslandCube.Cells) is { HasPolygons: true } c ? new IslandPolygon(c.Polygon(gx % IslandCube.Cells, gz % IslandCube.Cells, half)) : null;
        // (neighbours are judged as they were before this pass, so the order of the cells doesn't matter)
        var changes = new List<(IslandCube Cube, int X, int Z, int Half, uint Raw, bool Sand)>();
        for (var gz = Math.Max(1, b.Z0); gz < Math.Min(Grid - 1, b.Z1); gz++)
        for (var gx = Math.Max(1, b.X0); gx < Math.Min(Grid - 1, b.X1); gx++)
        {
            if (painted.Contains((gx, gz))) continue;
            if (island.CubeAt(gx / IslandCube.Cells, gz / IslandCube.Cells) is not { HasPolygons: true } cube) continue;
            var x = gx % IslandCube.Cells; var z = gz % IslandCube.Cells;
            var diagonal = new IslandPolygon(cube.Polygon(x, z, 0)).Diagonal;
            for (var half = 0; half < 2; half++)
            {
                var p = new IslandPolygon(cube.Polygon(x, z, half));
                if (!p.Col) continue;
                var corners = TriangleCorners(diagonal, half);
                var moved = corners.Max(c => Math.Abs(field.VertexHeight(gx + c.X, gz + c.Z) - natural.VertexHeight(gx + c.X, gz + c.Z)));
                var slope = Slope(field, gx, gz, corners);
                if (moved < 50 || slope >= 0.5) continue;
                // a rock patch left on nearly flat ground in the middle of sand or road reads as the wall it was, so it becomes sand;
                // next to a real outcrop or hillside it stays rock (just passable), or the sand would bite a sawtooth into the rock
                var soft = 0;
                for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                    for (var h = 0; h < 2; h++)
                        if ((dx != 0 || dz != 0) && (painted.Contains((gx + dx, gz + dz)) || At(gx + dx, gz + dz, h) is { TexFlag: 0, PolyFlag: not 0, Bank: 2 })) soft++;
                var toSand = slope < 0.35 && IsRock(cube, p) && soft >= 10;
                changes.Add((cube, x, z, half, (toSand ? sand.With(diagonal: diagonal) : p.With(col: false)).Raw, toSand));
            }
        }
        foreach (var c in changes) c.Cube.SetPolygon(c.X, c.Z, c.Half, c.Raw);
        var cleared = changes.Count; var repainted = changes.Count(c => c.Sand);
        if (cleared > 0) report.Notes.Add($"{cleared} blocking rock triangles that the shaping left nearly flat made passable ({repainted} repainted as sand)");
    }

    // A planned lap (RaceTrackPlan.Heights) is cut into mountainsides and built up over valleys and the sea, and one part of it runs on a
    // terrace above another: the banks the shaping leaves beside it are steep, and the engine lifts a car straight onto higher ground it
    // moves into, however much higher, unless the ground's triangle blocks (EXTFUNC.CPP ReajustPosExt) -- an ordinary bank was a ramp up
    // the cliff to the next terrace. So every cell of the shaping that it left steeper than SteepBank (rise over run) is painted as the
    // blocking rock. A verge cell holding a step of more than SteepVerge is the same (PaintRoad).
    private const double SteepBank = 1.0, SteepVerge = 300;

    private static void WallSteepBanks(IslandFile island, Field natural, Field field, RoadIndex index, HashSet<(int, int)> painted, RaceTrackOptions o, RaceTrackReport report)
    {
        var b = index.Bounds;
        var painter = new Painter(island, o.Theme);
        var walls = new List<(int, int)>();
        for (var gz = Math.Max(0, b.Z0); gz < Math.Min(Grid, b.Z1); gz++)
        for (var gx = Math.Max(0, b.X0); gx < Math.Min(Grid, b.X1); gx++)
        {
            if (painted.Contains((gx, gz))) continue;
            if (island.CubeAt(gx / IslandCube.Cells, gz / IslandCube.Cells) is not { HasPolygons: true } cube) continue;
            var diagonal = new IslandPolygon(cube.Polygon(gx % IslandCube.Cells, gz % IslandCube.Cells, 0)).Diagonal;
            var steep = false;
            for (var half = 0; half < 2 && !steep; half++)
            {
                var corners = TriangleCorners(diagonal, half);
                var moved = corners.Max(c => Math.Abs(field.VertexHeight(gx + c.X, gz + c.Z) - natural.VertexHeight(gx + c.X, gz + c.Z)));
                steep = moved >= 50 && Slope(field, gx, gz, corners) >= SteepBank;
            }
            if (steep) walls.Add((gx, gz));
        }
        foreach (var (gx, gz) in walls) { painter.Paint(gx, gz, Kind.Wall); painted.Add((gx, gz)); }
        if (walls.Count > 0) report.Notes.Add($"{walls.Count} cells of steep bank the shaping left beside the road painted as blocking rock (steeper than {SteepBank:0.#} rise over run)");
    }

    // The engine draws the sea under a cube in 4 x 4 squares, only those its info word marks (CubeBitField, LOADISLE.CPP; DrawOneSea):
    // the retail cubes mark the squares with open water, and leave out the ones under a mountain, which nothing ever sees. A planned lap
    // runs on terraces one above another, and the camera following the car sees the ground of the next terrace up from underneath, where
    // the engine draws no triangle (back faces are culled): with no sea square beneath either, that showed as a black hole in the view
    // beside the mountain lap's loops. Every square is marked, so the view through there is the sea instead, as it is round the island.
    // Where the ground is drawn it hides the sea as ever.
    private static void SeaEverywhere(IslandFile island, RaceTrackReport report)
    {
        var marked = 0;
        foreach (var cube in island.Cubes.Values)
        {
            if (cube.BitField == 0xFFFF) continue;
            marked += 16 - System.Numerics.BitOperations.PopCount((uint)cube.BitField);
            cube.BitField = 0xFFFF;
        }
        if (marked > 0) report.Notes.Add($"the sea drawn under the whole of every cube ({marked} more of the engine's sea squares): no black holes where the camera sees a terrace from below");
    }

    // The corners (x, z offsets in the cell) of triangle `half` of a cell cut along `diagonal` (IslandGround's own HalfCorners).
    private static (int X, int Z)[] TriangleCorners(bool diagonal, int half) => (diagonal, half) switch
    {
        (false, 0) => new[] { (0, 0), (0, 1), (1, 1) },
        (false, _) => new[] { (1, 1), (1, 0), (0, 0) },
        (true, 0) => new[] { (1, 0), (0, 0), (0, 1) },
        _ => new[] { (0, 1), (1, 1), (1, 0) },
    };

    // Rise over run of a ground triangle.
    private static double Slope(Field field, int gx, int gz, (int X, int Z)[] c)
    {
        double P(int k, int axis) => axis == 0 ? c[k].X * 512.0 : axis == 2 ? c[k].Z * 512.0 : field.VertexHeight(gx + c[k].X, gz + c[k].Z);
        double ux = P(1, 0) - P(0, 0), uy = P(1, 1) - P(0, 1), uz = P(1, 2) - P(0, 2);
        double vx = P(2, 0) - P(0, 0), vy = P(2, 1) - P(0, 1), vz = P(2, 2) - P(0, 2);
        double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
        return Math.Abs(ny) < 1e-9 ? double.MaxValue : Math.Sqrt(nx * nx + nz * nz) / Math.Abs(ny);
    }

    // Whether a triangle is textured with one of the atlas's rock tiles (rows 128-191).
    private static bool IsRock(IslandCube cube, IslandPolygon p)
    {
        if (p.TexFlag == 0) return false;
        var i = p.TextureIndex;
        if (i * 6 + 6 > cube.TextureDefs.Length) return false;
        for (var k = 0; k < 3; k++) { var v = cube.TextureDefs[i * 6 + k * 2 + 1] >> 8; if (v < 128 || v > 192) return false; }
        return true;
    }

    // Orange arrows, drawn at the resolution of the ground's own triangles (half cells) like the retail track's. A retail arrow
    // (flat bank 5, 17 triangles) is 4.95 cells long -- a right-angled head 2.1 long and 4.2 wide on a shaft 1.4 wide -- and always
    // points along a diagonal: then every edge of it lies on a cell side or a cell diagonal and the triangles draw it exactly. Ours
    // do the same: each arrow is turned onto the nearest of the eight directions the triangles draw exactly and placed where the
    // road runs that way, its tip on a cell corner. Outlines are in road-aligned cells, F forward (the tip at F = 0, the arrow
    // behind it) and U across.
    // Along a row or column: head 2 long and 4 wide on a shaft 2 wide, 6 long -- the retail proportions, and with the tip snapped to
    // the nearest cell corner the head keeps a cell clear of the curbs (a 6-wide head touched them).
    private static readonly (double F, double U)[] ArrowAxis =
    {
        (0, 0), (-2, 2), (-2, 1), (-6, 1), (-6, -1), (-2, -1), (-2, -2),
    };
    // Along a diagonal (steps of half a cell diagonal): the retail head (2.1 long, 4.2 wide) and shaft (1.4 wide), 6.4 long.
    private static readonly (double F, double U)[] ArrowDiagonal = Steps(new (double, double)[]
    {
        (0, 0), (-3, 3), (-3, 1), (-9, 1), (-9, -1), (-3, -1), (-3, -3),
    }, Math.Sqrt(0.5));
    // At any other heading (only when no spot of the straight runs close enough to one of the eight): 6.5 long, a head 3 long and
    // 5 wide on a shaft 1.7 wide. Its edges step by half cells.
    private static readonly (double F, double U)[] ArrowFree =
    {
        (0, 0), (-3, 2.5), (-3, 0.85), (-6.5, 0.85), (-6.5, -0.85), (-3, -0.85), (-3, -2.5),
    };
    // How far the arrow may be turned off the road's own heading to land on one of the eight: first 5 degrees, then 8.
    private static readonly double[] ArrowSnapDegrees = { 5, 8 };
    // The stretch searched for the arrow's spot (cells after the bend's exit) and the usual spot within it.
    private const double ArrowSearchFrom = 4, ArrowSearchTo = 36, ArrowUsual = 18;
    // Over what length of road its heading is measured (the chord, so an arrow where the road still turns sits in its lane).
    private const double ArrowChord = 7;

    private static (double F, double U)[] Steps((double F, double U)[] outline, double step) => outline.Select(p => (p.F * step, p.U * step)).ToArray();
    private static double OutlineLength((double F, double U)[] outline) => -outline.Min(p => p.F);

    // One cell of an arrow: which way the cell is cut and which of its two triangles are orange.
    private readonly record struct ArrowCell(bool Diagonal, bool Half0, bool Half1);

    // Orange arrows on the straight after each bend (and every 150 cells of a long straight), pointing the way of the lap.
    private static void MarkArrows(IslandFile island, TrackRoad r, Dictionary<(int, int), Kind> kinds, Dictionary<(int, int), ArrowCell> arrows, RaceTrackOptions o, RaceTrackReport report)
    {
        var n = r.Count;
        // find corner exits: where |kappa| drops below the threshold after a bend of at least 20 cells, plus every 140 cells on long straights
        var marks = new List<int>();
        var lastMark = -1000.0;
        for (var i = 0; i < n; i++)
        {
            if (r.Bridge[i] || r.Jump[i]) continue;
            // (a raised road's arrows are in its own pieces: RaceTrackRaisedBody)
            if (r.Raised is { } raised && raised[i]) continue;
            var straight = Math.Abs(r.Kappa[i]) < 0.02;
            var prevBend = false;
            for (var k = 1; k <= (int)(14 / o.Spacing); k++) if (Math.Abs(r.Kappa[At(r, i - k)]) > 0.06) { prevBend = true; break; }
            if (!straight) continue;
            var s = r.S[i];
            var since = s - lastMark; if (since < 0) since += r.Length;
            if ((prevBend && since > 40) || since > 150)
            {
                marks.Add(i); lastMark = s;
            }
        }
        foreach (var i in marks)
            if (!PlaceArrow(island, r, i, kinds, arrows, o, report))
                report.Notes.Add($"arrow {ArrowUsual:0} cells after cell ({r.X[i]:0.0}, {r.Z[i]:0.0}) left out: no spot near it lies wholly on plain asphalt");
    }

    // One arrow for the straight that starts at point i: at the spot nearest the usual one whose heading is within
    // ArrowSnapDegrees of one of the eight exact directions, else at the usual spot at the road's own heading. The whole arrow
    // or none of it: a spot where it would touch anything but plain asphalt (a curb, the start line, a bridge deck ...) is passed over.
    private static bool PlaceArrow(IslandFile island, TrackRoad r, int i, Dictionary<(int, int), Kind> kinds, Dictionary<(int, int), ArrowCell> arrows, RaceTrackOptions o, RaceTrackReport report)
    {
        var usual = (int)(ArrowUsual / o.Spacing);
        var from = (int)(ArrowSearchFrom / o.Spacing); var to = (int)(ArrowSearchTo / o.Spacing);
        var candidates = Enumerable.Range(from, to - from + 1).OrderBy(k => Math.Abs(k - usual)).ToList();
        // (not under the road bridge's deck either, where it can't be seen)
        bool Fits(Dictionary<(int, int), ArrowCell> cells) => cells.Count > 0 && cells.Keys.All(c => kinds.TryGetValue(c, out var kind) && kind == Kind.Asphalt && !UnderBridgeDeck(report, c.Item1, c.Item2));
        void Keep(Dictionary<(int, int), ArrowCell> cells, int j, double tx, double tz)
        {
            foreach (var (cell, arrow) in cells) arrows[cell] = arrow;
            report.Arrows.Add(new[] { r.X[j], r.Z[j], tx, tz });
        }
        foreach (var tolerance in ArrowSnapDegrees)
        foreach (var k in candidates)
        {
            var j = At(r, i + k);
            if (r.Bridge[j] || r.Deck[j] || r.Jump[j]) continue;
            var (cx, cz) = Chord(r, j, ArrowChord, o);
            var angle = Math.Atan2(cz, cx);
            var octant = (int)Math.Round(angle / (Math.PI / 4));
            if (Math.Abs(angle - octant * Math.PI / 4) * 180 / Math.PI > tolerance) continue;
            // (dx, dz) one of (1,0), (1,1), (0,1) ... ; the unit heading is (dx, dz) * scale
            var dx = Math.Round(Math.Cos(octant * Math.PI / 4)); var dz = Math.Round(Math.Sin(octant * Math.PI / 4));
            var diagonal = dx != 0 && dz != 0;
            var outline = diagonal ? ArrowDiagonal : ArrowAxis;
            var scale = diagonal ? Math.Sqrt(0.5) : 1;
            var half = OutlineLength(outline) / 2;
            // the tip on the cell corner nearest to where it falls with the arrow centred on the road point
            var tipX = Math.Round(r.X[j] + dx * scale * half); var tipZ = Math.Round(r.Z[j] + dz * scale * half);
            var cells = RasteriseArrow(island, outline, tipX, tipZ, dx * scale, dz * scale);
            if (!Fits(cells)) continue;
            Keep(cells, j, dx * scale, dz * scale);
            return true;
        }
        {
            var j = At(r, i + usual);
            var (tx, tz) = Chord(r, j, OutlineLength(ArrowFree), o);
            var half = OutlineLength(ArrowFree) / 2;
            var cells = RasteriseArrow(island, ArrowFree, r.X[j] + tx * half, r.Z[j] + tz * half, tx, tz);
            if (!Fits(cells)) return false;
            Keep(cells, j, tx, tz);
            return true;
        }
    }

    // Whether a cell lies under the road bridge's deck (with a cell to spare).
    private static bool UnderBridgeDeck(RaceTrackReport report, int gx, int gz)
    {
        if (report.RoadBridge is not { } rb) return false;
        var dx = gx + 0.5 - rb.X; var dz = gz + 0.5 - rb.Z;
        var along = dx * rb.DirX + dz * rb.DirZ; var across = -dx * rb.DirZ + dz * rb.DirX;
        return Math.Abs(along) <= rb.Length / 1024 + 1 && Math.Abs(across) <= rb.Width / 1024 + 1;
    }

    // The road's unit heading at point j, measured over `length` cells of it.
    private static (double X, double Z) Chord(TrackRoad r, int j, double length, RaceTrackOptions o)
    {
        var reach = Math.Max(1, (int)Math.Round(length / 2 / o.Spacing));
        var a = At(r, j - reach); var b = At(r, j + reach);
        var dx = r.X[b] - r.X[a]; var dz = r.Z[b] - r.Z[a]; var len = Math.Sqrt(dx * dx + dz * dz);
        return len > 1e-6 ? (dx / len, dz / len) : (r.Tx[j], r.Tz[j]);
    }

    // Rasterises a polygon given in road-aligned cells (F along the heading (tx, tz), U across it), its origin placed at the island
    // cell position (ox, oz), at the resolution of the ground's triangles. The engine cuts a cell one of two ways (MAPTOOLS.CPP
    // GiveTriangle, read from the cell's first triangle): Sens 0 along corners 0-2 with triangle 0 where x < z, Sens 1 along
    // corners 1-3 with triangle 0 where x + z < 1. Each cell is sampled on a grid, each triangle is orange when most of its
    // samples are inside, and the cut is chosen whose triangles miss the fewest samples; a cell wholly inside, or one where both cuts
    // do equally well, keeps its own cut. (An outline whose edges all lie on cell sides and diagonals comes out exact.) The cut also
    // decides how the engine interpolates the cell's height, which is harmless here: the road is levelled flat across, so both cuts
    // give the same surface to within a few units. Returns only cells with an orange triangle.
    private static Dictionary<(int, int), ArrowCell> RasteriseArrow(IslandFile island, (double F, double U)[] outline, double ox, double oz, double tx, double tz)
    {
        const int Samples = 16;   // per cell side
        var result = new Dictionary<(int, int), ArrowCell>();
        var px = new double[outline.Length]; var pz = new double[outline.Length];
        for (var k = 0; k < outline.Length; k++)
        {
            px[k] = ox + outline[k].F * tx - outline[k].U * tz;
            pz[k] = oz + outline[k].F * tz + outline[k].U * tx;
        }
        var inside = new int[4]; var count = new int[4];   // per triangle, indexed Sens * 2 + half
        for (var gz = (int)Math.Floor(pz.Min()); gz <= (int)Math.Floor(pz.Max()); gz++)
        for (var gx = (int)Math.Floor(px.Min()); gx <= (int)Math.Floor(px.Max()); gx++)
        {
            Array.Clear(inside); Array.Clear(count);
            var total = 0;
            for (var j = 0; j < Samples; j++)
            for (var i = 0; i < Samples; i++)
            {
                var u = (i + 0.5) / Samples; var v = (j + 0.5) / Samples;
                var hit = PointInPolygon(px, pz, gx + u, gz + v) ? 1 : 0;
                var t0 = u < v ? 0 : 1; var t1 = 2 + (u + v < 1 ? 0 : 1);
                count[t0]++; inside[t0] += hit;
                count[t1]++; inside[t1] += hit;
                total += hit;
            }
            if (total == 0) continue;
            if (island.CubeAt(gx / IslandCube.Cells, gz / IslandCube.Cells) is not { HasPolygons: true } cube) continue;
            var own = new IslandPolygon(cube.Polygon(gx % IslandCube.Cells, gz % IslandCube.Cells, 0)).Diagonal;
            if (total == Samples * Samples) { result[(gx, gz)] = new ArrowCell(own, true, true); continue; }
            int Miss(int t) => Math.Min(inside[t], count[t] - inside[t]);
            bool Fill(int t) => inside[t] * 2 > count[t];
            var miss0 = Miss(0) + Miss(1); var miss1 = Miss(2) + Miss(3);
            var sens = miss0 == miss1 ? (own ? 1 : 0) : miss1 < miss0 ? 1 : 0;
            var cell = new ArrowCell(sens == 1, Fill(sens * 2), Fill(sens * 2 + 1));
            if (cell.Half0 || cell.Half1) result[(gx, gz)] = cell;
        }
        return result;
    }

    // Even-odd test of a point against a polygon.
    private static bool PointInPolygon(double[] px, double[] pz, double x, double z)
    {
        var inside = false;
        for (int a = 0, b = px.Length - 1; a < px.Length; b = a++)
            if ((pz[a] > z) != (pz[b] > z) && x < (px[b] - px[a]) * (z - pz[a]) / (pz[b] - pz[a]) + px[a]) inside = !inside;
        return inside;
    }

    private sealed class Painter
    {
        private readonly IslandFile island;
        private readonly RaceTrackTheme theme;
        public Painter(IslandFile island, RaceTrackTheme theme) { this.island = island; this.theme = theme; }

        private static ushort[] Tile((int X, int Y, int W, int H) t, bool diagonal, int half) => IslandGround.TileDefinition(t.X, t.Y, t.W, t.H, diagonal, half);

        // sample words of the retail track's cells: (bank, texFlag, polyFlag, step)
        private static IslandPolygon Flat(int bank, int step) => new IslandPolygon(0).With(bank: bank, texFlag: 0, polyFlag: 3, sampleStep: step, codeJeu: 0);
        private static IslandPolygon Textured(int step) => new IslandPolygon(0).With(bank: 2, texFlag: 1, polyFlag: 0, sampleStep: step, codeJeu: 0);

        // `arrow`: the cell carries part of an arrow -- its cut is set to the arrow's and its orange triangles painted as Kind.Arrow,
        // the others as `kind` (the asphalt tile's corners follow the new cut).
        public void Paint(int gx, int gz, Kind kind, ArrowCell? arrow = null)
        {
            var cube = island.CubeAt(gx / IslandCube.Cells, gz / IslandCube.Cells)!;
            var x = gx % IslandCube.Cells; var z = gz % IslandCube.Cells;
            var diagonal = arrow?.Diagonal ?? new IslandPolygon(cube.Polygon(x, z, 0)).Diagonal;
            for (var half = 0; half < 2; half++)
            {
                var p = Polygon(cube, arrow is { } a && (half == 0 ? a.Half0 : a.Half1) ? Kind.Arrow : kind, diagonal, half);
                cube.SetPolygon(x, z, half, Paged(p).Raw);
            }
            NoWater(gx, gz);
        }

        // A cell along a kerb (KerbCells): cut the road's way, each triangle its own paint -- or the verge's flat colour with the kerb
        // texture over it.
        public void PaintKerb(int gx, int gz, KerbCell kerb)
        {
            var cube = island.CubeAt(gx / IslandCube.Cells, gz / IslandCube.Cells)!;
            var x = gx % IslandCube.Cells; var z = gz % IslandCube.Cells;
            for (var half = 0; half < 2; half++)
            {
                var p = kerb.Uvs[half] is { } uv
                    ? Flat(kerb.Under.Bank, kerb.Under.Pos).With(texFlag: 1, textureIndex: IslandGround.TextureIndexFor(cube, uv), diagonal: kerb.Diagonal, col: false)
                    : Polygon(cube, kerb.Kinds[half], kerb.Diagonal, half);
                cube.SetPolygon(x, z, half, Paged(p).Raw);
            }
            NoWater(gx, gz);
        }

        // (the road's textures are on the ground's first page: on an island with more pages its definition is the page's and the triangle's
        // Wide bit -- IslandFile.WithGroundTexture; a flat triangle has neither)
        private IslandPolygon Paged(IslandPolygon p) =>
            island.GroundPages.Count == 0 ? p : p.TexFlag == 0 ? p.With(textureIndex: 0, wide: false) : island.WithGroundTexture(p, 0, p.TextureIndex);

        private IslandPolygon Polygon(IslandCube cube, Kind kind, bool diagonal, int half)
        {
                IslandPolygon p;
                bool col = false;
                switch (kind)
                {
                    case Kind.Asphalt: p = Textured(5).With(textureIndex: IslandGround.TextureIndexFor(cube, Tile(theme.Asphalt, diagonal, half))); break;
                    case Kind.Start:
                    case Kind.WhiteCurb: p = Textured(4).With(textureIndex: IslandGround.TextureIndexFor(cube, IslandGround.TileDefinition(theme.WhiteCurb.X, theme.WhiteCurb.Y, 1, 1, diagonal, half))); break;
                    case Kind.RedCurb: p = Flat(theme.RedCurb.Bank, theme.RedCurb.Pos); break;
                    case Kind.Arrow: p = Flat(theme.Arrow.Bank, theme.Arrow.Pos); break;
                    case Kind.Hatch:
                        p = theme.FlatHatch is { } flat ? Flat(flat.Bank, flat.Pos) : Textured(1).With(textureIndex: IslandGround.TextureIndexFor(cube, Tile(theme.Hatch, diagonal, half)));
                        break;
                    case Kind.Wall:
                    case Kind.Rock: p = Textured(5).With(texFlag: 3, textureIndex: IslandGround.TextureIndexFor(cube, Tile(theme.Rock, diagonal, half))); col = kind == Kind.Wall; break;
                    default: p = Flat(theme.Sand.Bank, theme.Sand.Pos); break;
                }
                return p.With(diagonal: diagonal, col: col);
        }

        // no water code, no water depth under the road
        private void NoWater(int gx, int gz)
        {
            for (var dz = 0; dz <= 1; dz++)
            for (var dx = 0; dx <= 1; dx++)
                foreach (var (c, vx, vz) in island.Owners(gx + dx, gz + dz))
                    if (c.HasIntensity) c.Intensity[vz * IslandCube.Vertices + vx] &= 0x0F;
        }
    }

    // The line a lap is counted at: across the lap at the start line, from its verge on one side to past the pit lane on the other (a car
    // coming out of the pits crosses it too).
    private static (double, double, double, double, double, double)? LapLine(List<TrackRoad> roads, RaceTrackReport report)
    {
        if (report.StartLine.Count == 0) return null;
        var (x, z, _, dx, dz) = report.StartLine[0];
        if (report.StartLineSquare is { } square) (dx, dz) = square;
        var nx = -dz; var nz = dx;
        double left = roads[0].VergeHalf, right = roads[0].VergeHalf;
        double leftCurb = roads[0].CurbHalf, rightCurb = roads[0].CurbHalf;
        if (roads.Count > 1)
        {
            var pit = roads[1];
            var best = Enumerable.Range(0, pit.Count).MinBy(i => Sq(pit.X[i] - x) + Sq(pit.Z[i] - z));
            var across = (pit.X[best] - x) * nx + (pit.Z[best] - z) * nz;
            if (across > 0) right = Math.Max(right, across + pit.VergeHalf); else left = Math.Max(left, -across + pit.VergeHalf);
            if (across > 0) rightCurb = Math.Max(rightCurb, across + pit.CurbHalf); else leftCurb = Math.Max(leftCurb, -across + pit.CurbHalf);
        }
        // (no further across than another part of the lap: just out of the lava lake's corner the line's end reached the road the grid
        // stands on, and a car leaving the grid crossed it there first)
        var main = roads[0];
        var at = Enumerable.Range(0, main.Count).MinBy(i => Sq(main.X[i] - x) + Sq(main.Z[i] - z));
        bool Alone(double t)
        {
            double px = x + nx * t, pz = z + nz * t;
            for (var m = 0; m < main.Count; m++)
            {
                var sep = Math.Abs(main.S[m] - main.S[at]); if (main.Closed) sep = Math.Min(sep, main.Length - sep);
                // (a raised road's other levels over the line don't count: the engine counts the line only near its own height)
                if (main.Raised is not null && Math.Abs(main.H[m] - main.H[at]) > RaisedLevelApart) continue;
                if (sep > 20 && Sq(main.X[m] - px) + Sq(main.Z[m] - pz) < Sq(main.CurbHalf + 1)) return false;
            }
            return true;
        }
        while (left > leftCurb && !Alone(-left)) left = Math.Max(leftCurb, left - 0.25);
        while (right > rightCurb && !Alone(right)) right = Math.Max(rightCurb, right - 0.25);
        report.StartCurbs = (leftCurb, rightCurb);
        report.LapLineHeight = report.StartLine[0].Y;
        return (x - nx * left, z - nz * left, x + nx * right, z + nz * right, dx, dz);
    }

    // The start line: a one-cell white row across the road at point i. Where the road runs within 25 degrees of the cell grid's rows or
    // columns the row is one straight column (or row) of cells through the cell the point is in; a row of the cells within half a cell
    // of the true line stepped sideways where the road runs a few degrees off the grid. Elsewhere, the cells along the true line.
    private static void MarkStartLine(TrackRoad r, int i, Dictionary<(int, int), Kind> kinds, RaceTrackReport report)
    {
        var cx = r.X[i]; var cz = r.Z[i]; var tx = r.Tx[i]; var tz = r.Tz[i];
        var heading = Math.Atan2(tz, tx);
        var quarter = Math.Round(heading / (Math.PI / 2));
        if (Math.Abs(heading - quarter * Math.PI / 2) <= 25 * Math.PI / 180)
        {
            var sx = Math.Round(Math.Cos(quarter * Math.PI / 2)); var sz = Math.Round(Math.Sin(quarter * Math.PI / 2));   // the road, on the grid
            var gx0 = (int)Math.Floor(cx); var gz0 = (int)Math.Floor(cz);
            for (var k = -8; k <= 8; k++)
            {
                // along the line: the road's grid direction turned a quarter
                var gx = gx0 - (int)sz * k; var gz = gz0 + (int)sx * k;
                var px = gx + 0.5 - cx; var pz = gz + 0.5 - cz;
                if (Math.Abs(-px * tz + pz * tx) > r.CurbHalf || !kinds.TryGetValue((gx, gz), out var was)) continue;
                kinds[(gx, gz)] = Kind.Start;
                // the curb blocks either side of the line red, so a white one doesn't run into the white line and bend its end
                if (was is Kind.RedCurb or Kind.WhiteCurb)
                    foreach (var side in new[] { 1, -1 })
                    {
                        var key = (gx + (int)sx * side, gz + (int)sz * side);
                        if (kinds.TryGetValue(key, out var next) && next == Kind.WhiteCurb) kinds[key] = Kind.RedCurb;
                    }
            }
            // the line's middle is the middle of that column across the road
            var mx = gx0 + 0.5; var mz = gz0 + 0.5;
            var along = (mx - cx) * sx + (mz - cz) * sz;
            report.StartLine.Add((cx + sx * along, cz + sz * along, r.H[i], tx, tz));
            report.StartLineSquare = (sx, sz);
            return;
        }
        for (var gz = (int)Math.Floor(cz - 6); gz <= (int)Math.Ceiling(cz + 6); gz++)
        for (var gx = (int)Math.Floor(cx - 6); gx <= (int)Math.Ceiling(cx + 6); gx++)
        {
            var px = gx + 0.5 - cx; var pz = gz + 0.5 - cz;
            var f = px * tx + pz * tz; var u = -px * tz + pz * tx;
            if (Math.Abs(f) <= 0.6 && Math.Abs(u) <= r.CurbHalf && kinds.ContainsKey((gx, gz))) kinds[(gx, gz)] = Kind.Start;
        }
        report.StartLine.Add((cx, cz, r.H[i], tx, tz));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // the gantry over the start line and the bridge-like gantry where the lap crosses itself

    // Retail gantry (the Desert track's start line): body 64 the beam, 65 and 66 its posts, all placed at one origin. Boxes are the
    // retail ZVs relative to that origin: (xMin, yMin, zMin, xMax, yMax, zMax).
    private static readonly (int Body, int[] Box)[] Gantry =
    {
        (64, new[] { -5519, 3141, -397, -141, 3990, -114 }),
        (65, new[] { -424, 28, -397, -141, 3141, -114 }),
        (66, new[] { -5519, -512, -397, -5236, 3141, -114 }),
    };
    private const double GantryCentreOffset = 2830;      // the origin is this far from the middle of the gantry, along its beam
    private const double GantryBeam = 5378;              // the beam's length (body 64's box), post to post
    private const double GantryMargin = 0.4;             // cells between an outer curb and the post outside it

    // The gantry across the start line. One beam spans a road and its curbs; where the pit lane runs beside the start line (every track's
    // start is in the pit lane's middle) as many beams end to end as reach over both, centred on them, with the posts only at the two outer
    // ends -- a single gantry over the lap put its post on the strip between the lap and the pit lane, in the middle of the straight as a
    // driver sees it (Citadel Island's storm track). `curbs`: how far the outer curbs reach either side of the line (RaceTrackReport.StartCurbs).
    private static void PlaceGantry(IslandFile island, double cx, double cz, double height, double bisX, double bisZ, string what, RaceTrackReport report, RaceTrackOptions o,
        (double Left, double Right)? curbs = null)
    {
        // the beam runs across the road, i.e. at right angles to the way the road runs (local +X along e, to the line's right)
        var ex = -bisZ; var ez = bisX;
        var theta = Math.Atan2(-ez, ex);
        var beta = (int)Math.Round(theta / (2 * Math.PI) * 4096); beta = ((beta % 4096) + 4096) % 4096;
        var cos = Math.Cos(theta); var sin = Math.Sin(theta);
        var y = (int)Math.Round(height);
        var beam = GantryBeam / 512;
        var (left, right) = curbs ?? (0, 0);
        var beams = curbs is null ? 1 : Math.Max(1, (int)Math.Ceiling((left + right + 2 * GantryMargin) / beam - 1e-9));
        var middle = beams == 1 && curbs is not null && left + right + 2 * GantryMargin <= beam ? 0 : (right - left) / 2;
        if (beams > 1)
        {
            // Beams end to end come out longer than the road and the pit lane need; the spare goes where both posts have ground under
            // them -- beyond the pit lane first, then either side evenly, then beyond the lap. Evenly, Mosquibees Island's lap-side post
            // stood past the plateau's edge, over the sea. (The ground under a post: no lower than a post's length under the road.)
            var total = beams * beam;
            bool Grounded(double across)
            {
                var g = IslandOps.Altitude(island, (cx + ex * across) * 512, (cz + ez * across) * 512);
                return g is { } h && h >= height - 1000;
            }
            var pitRight = right >= left;
            var choices = new[]
            {
                pitRight ? -(left + GantryMargin) + total / 2 : right + GantryMargin - total / 2,      // the spare beyond the pit lane
                (right - left) / 2,                                                                    // evenly
                pitRight ? right + GantryMargin - total / 2 : -(left + GantryMargin) + total / 2,      // beyond the lap
            };
            middle = choices.FirstOrDefault(m => Grounded(m - total / 2) && Grounded(m + total / 2), (right - left) / 2);
        }
        var placed = 0;
        var posts = new List<(double X0, double Z0, double X1, double Z1)>();
        for (var k = 0; k < beams; k++)
        {
            var along = middle + (k + 0.5 - beams / 2.0) * beam;             // this beam's middle, across the line
            var originX = (cx + ex * (along + GantryCentreOffset / 512)) * 512; var originZ = (cz + ez * (along + GantryCentreOffset / 512)) * 512;
            if (IslandDecors.Locate(island, originX, originZ) is not { } at) continue;
            double cubeX = Math.Floor(originX / IslandFile.CubeSize) * IslandFile.CubeSize, cubeZ = Math.Floor(originZ / IslandFile.CubeSize) * IslandFile.CubeSize;
            foreach (var (body, box) in Gantry)
            {
                // (body 66 is the post at the beam's left end, 65 at its right: only the outermost ones stand)
                if (body == 66 && k != 0 || body == 65 && k != beams - 1) continue;
                var d = IslandDecors.Blank(o.RetailBody(body), at.X, y, at.Z, beta);
                double minX = 1e18, maxX = -1e18, minZ = 1e18, maxZ = -1e18;
                foreach (var (bx, bz) in new[] { (box[0], box[2]), (box[3], box[2]), (box[0], box[5]), (box[3], box[5]) })
                {
                    var wx = bx * cos + bz * sin; var wz = -bx * sin + bz * cos;
                    minX = Math.Min(minX, wx); maxX = Math.Max(maxX, wx); minZ = Math.Min(minZ, wz); maxZ = Math.Max(maxZ, wz);
                }
                d.XMin = at.X + (int)Math.Floor(minX); d.XMax = at.X + (int)Math.Ceiling(maxX);
                d.ZMin = at.Z + (int)Math.Floor(minZ); d.ZMax = at.Z + (int)Math.Ceiling(maxZ);
                d.YMin = y + box[1]; d.YMax = y + box[4];
                if (at.Cube.Decors.Count < IslandDecors.MaxPerCube) { at.Cube.Decors.Add(d); placed++; }
                if (body != 64) posts.Add(((cubeX + d.XMin) / 512, (cubeZ + d.ZMin) / 512, (cubeX + d.XMax) / 512, (cubeZ + d.ZMax) / 512));
            }
        }
        if (placed == 0) { report.Placed.Add($"{what}: off the island"); return; }
        if (o.GantryFootings) PostFootings(island, o, posts, y, what, report);
        var span = beams == 1 ? "" : $", {beams} beams end to end over the lap and the pit lane beside it, posts {middle - beams * beam / 2:0.0} and {middle + beams * beam / 2:+0.0} cells across";
        report.Placed.Add($"{what}: gantry at cell ({cx:0.0}, {cz:0.0}), height {y}, turn {beta}{span}");
    }

    // The ground under the raised road's ends (CutUnderRaised): returns the vertices it set.
    private static HashSet<(int, int)> FlushRaisedEnds(IslandFile island, TrackRoad r, RaceTrackOptions o, RaceTrackReport report, HashSet<(int, int)>? keep = null)
    {
        var up = r.Raised!; var n = r.Count;
        var half = o.RaisedHalfWidth;
        // the points of each end's stretch: from where the road comes off the ground road, while the deck is still near the ground
        var end = new bool[n];
        for (var i = 0; i < n; i++)
        {
            if (!up[i] || r.Gap[i]) continue;
            for (var way = -1; way <= 1; way += 2)
            {
                var j = At(r, i + way);
                if (up[j] || r.Gap[j]) continue;
                for (var k = 0; k < n; k++)
                {
                    var p = At(r, i - way * k);
                    if (!up[p] || r.Gap[p]) break;
                    // (on until the deck is well over the ground -- or under it: there the road runs in a cutting, the cutting's)
                    var over = r.H[p] - (IslandOps.Altitude(island, r.X[p] * 512, r.Z[p] * 512) ?? 0);
                    if (over > RaisedFlush || over < -RaisedClearance) break;
                    end[p] = true;
                }
            }
        }
        // the nearest such stretch's surface for every vertex in reach: (the ground it should be, how much of it, how far it is)
        var want = new Dictionary<(int, int), (double H, double W, double D)>();
        for (var a = 0; a < n; a++)
        {
            var b = At(r, a + 1);
            if (!(end[a] || end[b]) || !up[a] || !up[b] || r.Gap[a] || r.Gap[b]) continue;
            double sx = r.X[b] - r.X[a], sz = r.Z[b] - r.Z[a], len2 = sx * sx + sz * sz;
            if (len2 < 1e-12) continue;
            var reach = half + RaisedFlushBlend;
            for (var gz = (int)Math.Floor(Math.Min(r.Z[a], r.Z[b]) - reach); gz <= (int)Math.Ceiling(Math.Max(r.Z[a], r.Z[b]) + reach); gz++)
            for (var gx = (int)Math.Floor(Math.Min(r.X[a], r.X[b]) - reach); gx <= (int)Math.Ceiling(Math.Max(r.X[a], r.X[b]) + reach); gx++)
            {
                var t = Math.Clamp(((gx - r.X[a]) * sx + (gz - r.Z[a]) * sz) / len2, 0, 1);
                var d = Math.Sqrt(Sq(gx - r.X[a] - sx * t) + Sq(gz - r.Z[a] - sz * t));
                if (d > reach) continue;
                var u = Math.Clamp((d - half) / RaisedFlushBlend, 0, 1);
                var w = 1 - u * u * (3 - 2 * u);
                var surface = r.H[a] + (r.H[b] - r.H[a]) * t - RaisedFlushUnder;
                if (!want.TryGetValue((gx, gz), out var was) || d < was.D) want[(gx, gz)] = (surface, w, d);
            }
        }
        var set = new HashSet<(int, int)>();
        double raised = 0, lowered = 0;
        foreach (var ((gx, gz), (surface, w, d)) in want)
        {
            if (w <= 0 || island.HeightAt(gx, gz) is not { } h) continue;
            // (a kept building's ground: only where it stands up through the deck)
            if (keep?.Contains((gx, gz)) == true && !(d <= half && h > surface)) continue;
            // (under the deck its surface; beside it, ground below it filled up towards it, ground above it brought down to it out to the
            // cutting's reach past the rail -- the engine's floor reaches a little past it -- and banked up from there)
            var to = d <= half || h < surface ? h + (surface - h) * w : Math.Min(h, surface + Math.Max(0, d - half - RaisedCutReach) * 512 * RaisedCutBank);
            if (Math.Abs(to - h) < 1) continue;
            raised = Math.Max(raised, to - h); lowered = Math.Max(lowered, h - to);
            island.SetHeight(gx, gz, (int)Math.Round(to)); set.Add((gx, gz));
        }
        if (set.Count > 0)
            report.Notes.Add($"the raised road's ends: the ground under {end.Count(e => e) * o.Spacing:0} cells of them made the deck's own surface ({set.Count} vertices, " +
                             $"raised {raised:0} and lowered {lowered:0} at the most)");
        return set;
    }

    // A gantry's post standing over a hole gets a footing (2026-10-01: the lava lake's dock is narrower than its start line's gantry, and
    // both posts stood in the sea beside it; a plan's choice, RaceTrackPlan.GantryFootings): the ground round it raised to the gantry's
    // foot out to FootingTop cells from the post, eased down to the ground as it is over FootingBlend more -- never on the road or its
    // verge -- and the cells of it that weren't drawn (the sea) drawn as rock.
    private const double FootingTop = 0.9, FootingBlend = 1.6, FootingHole = 100;

    private static void PostFootings(IslandFile island, RaceTrackOptions o, List<(double X0, double Z0, double X1, double Z1)> posts, int y, string what, RaceTrackReport report)
    {
        var painter = new Painter(island, o.Theme);
        int raised = 0, drawn = 0, footed = 0;
        foreach (var (x0, z0, x1, z1) in posts)
        {
            var under = new[] { (x0, z0), (x1, z0), (x0, z1), (x1, z1), ((x0 + x1) / 2, (z0 + z1) / 2) }.Min(p => IslandOps.Altitude(island, p.Item1 * 512, p.Item2 * 512) ?? 0);
            if (under >= y - FootingHole) continue;
            footed++;
            var reach = FootingTop + FootingBlend;
            var changed = new List<(int, int)>();
            for (var gz = (int)Math.Floor(z0 - reach); gz <= (int)Math.Ceiling(z1 + reach); gz++)
            for (var gx = (int)Math.Floor(x0 - reach); gx <= (int)Math.Ceiling(x1 + reach); gx++)
            {
                if (island.HeightAt(gx, gz) is not { } h) continue;
                var d = Math.Sqrt(Sq(Math.Max(0, Math.Max(x0 - gx, gx - x1))) + Sq(Math.Max(0, Math.Max(z0 - gz, gz - z1))));
                if (d > reach) continue;
                if (report.DistanceToRoad is { } road && road(gx, gz) < o.VergeHalfWidth) continue;
                var t = Math.Clamp((d - FootingTop) / FootingBlend, 0, 1);
                var want = h + (y - h) * (1 - t * t * (3 - 2 * t));
                if (want <= h + 1) continue;                                    // (only ever raised)
                island.SetHeight(gx, gz, (int)Math.Round(want)); raised++; changed.Add((gx, gz));
            }
            var cells = changed.SelectMany(v => new[] { (v.Item1 - 1, v.Item2 - 1), (v.Item1, v.Item2 - 1), (v.Item1 - 1, v.Item2), v }).Distinct();
            foreach (var (gx, gz) in cells)
            {
                if (island.CubeAt(gx / IslandCube.Cells, gz / IslandCube.Cells) is not { HasPolygons: true } cube || gx < 0 || gz < 0) continue;
                var x = gx % IslandCube.Cells; var z = gz % IslandCube.Cells;
                var any = false;
                for (var half = 0; half < 2; half++) { var p = new IslandPolygon(cube.Polygon(x, z, half)); if (p.TexFlag != 0 || p.PolyFlag != 0) any = true; }
                if (any) continue;
                painter.Paint(gx, gz, Kind.Rock); drawn++;
            }
        }
        if (footed > 0) report.Notes.Add($"{what}: {footed} of the gantry's posts stood over a hole -- a footing of ground under each ({raised} vertices raised, {drawn} cells of the sea drawn as rock)");
    }

    private static void PlaceStructures(IslandFile island, TrackRoad main, List<Crossing> crossings, int startIndex, RaceTrackOptions options, RaceTrackReport report, bool planned = false)
    {
        // (a lap that is all raised road has a gantry of its own, on the road: PlaceRaised)
        if (startIndex >= 0 && report.StartLine.Count > 0 && !(main.Closed && main.Raised is { } all && all.All(u => u)))
        {
            var d = report.StartLine[0];
            var (dx, dz) = report.StartLineSquare ?? (d.DirX, d.DirZ);
            PlaceGantry(island, d.X, d.Z, d.Y, dx, dz, "start line", report, options, report.StartCurbs);
        }
        if (options.Crossing == CrossingStyle.Viaduct && !planned)
            foreach (var c in crossings) PlaceBridge(island, c.X, c.Z, main.H[c.I], c.BisX, c.BisZ, report, options);
        if ((options.Crossing == CrossingStyle.Bridge || planned) && report.RoadBridge is { } rb) PlaceDeck(island, rb, options, report);
        if (main.Raised is not null) PlaceRaised(island, main, options, report, startIndex);
        if (report.Loops.Count > 0) PlaceLoops(island, options, report);
        if (report.Jumps.Any(j => j.Drop > 0)) PlaceJumpCameras(island, report);
    }

    // A drop's camera (RACEMOD.CPP jumpcam=): the camera that follows the car would go down the hillside behind it and into the hill, and
    // the landing would not be seen. Instead the camera stands beside the flight: of the places to either side of it, DropCamFar cells out
    // from a little past its middle, between the landing's height and a little over the take-off's, the one that sees the most of the
    // car's places along the flight and the road past the landing -- its line to each clear of the ground and of the raised road's decks
    // (through a slab, or past a rail) -- then the one whose lines pass highest over the ground, a little of that given up for each cell
    // farther out.
    private static readonly double[] DropCamFar = { 12, 15, 18 }, DropCamUp = { 0.6, 0.85, 1.05, 1.2 };

    private static void PlaceJumpCameras(IslandFile island, RaceTrackReport report)
    {
        double Ground(double x, double z) => IslandOps.Altitude(island, x * 512, z * 512) ?? 0;
        // the deck's pieces: segments between points that both have a width (a jump's gap has none)
        var points = report.Raised.Select(p => (X: p[0] / 512.0, Z: p[1] / 512.0, Y: (double)p[2], Half: p[3] / 512.0)).ToList();
        var decks = Enumerable.Range(0, Math.Max(0, points.Count - 1)).Where(i => points[i].Half > 0 && points[i + 1].Half > 0).Select(i => (A: points[i], B: points[i + 1])).ToList();
        bool Hidden(double px, double py, double pz)
        {
            foreach (var (a, b) in decks)
            {
                double sx = b.X - a.X, sz = b.Z - a.Z, l2 = sx * sx + sz * sz;
                if (l2 < 1e-9) continue;
                var t = ((px - a.X) * sx + (pz - a.Z) * sz) / l2;
                if (t < 0 || t > 1) continue;
                var lat = Math.Sqrt(Sq(px - a.X - sx * t) + Sq(pz - a.Z - sz * t));
                if (lat > a.Half + 0.3) continue;
                var y = a.Y + (b.Y - a.Y) * t;
                if (py > y - 300 && py < y - 20) return true;                          // (through the slab)
                if (lat > a.Half - 0.4 && py > y - 300 && py < y + 150) return true;    // (past a rail)
            }
            return false;
        }
        foreach (var j in report.Jumps.Where(j => j.Drop > 0))
        {
            double fx = j.LandX - j.StartX, fz = j.LandZ - j.StartZ, len = Math.Sqrt(fx * fx + fz * fz);
            if (len < 1) continue;
            fx /= len; fz /= len;
            var landY = j.Height - j.Drop;
            var sights = new List<(double X, double Y, double Z)>();
            // (the car's body, a little over the road it is on)
            const double body = 250;
            for (var u = 0.0; u <= 1.0001; u += 0.1) sights.Add((j.StartX + fx * len * u, j.Height + RaceTrackJumpAnim.DropAt(u, j.Drop) + body, j.StartZ + fz * len * u));
            for (var a = 1.0; a <= 4; a += 1) sights.Add((j.LandX + fx * a, landY + body, j.LandZ + fz * a));
            (double X, double Y, double Z, int Seen, double Clear, double Score)? best = null;
            foreach (var side in new[] { -1, 1 })
            foreach (var far in DropCamFar)
            foreach (var up in DropCamUp)
            {
                double mx = j.StartX + fx * len * 0.55, mz = j.StartZ + fz * len * 0.55;
                double ex = mx - fz * far * side, ez = mz + fx * far * side, ey = landY + (j.Height - landY) * up;
                if (ey < Ground(ex, ez) + 600) continue;
                int seen = 0; var clear = double.MaxValue;
                foreach (var (sx, sy, sz) in sights)
                {
                    var lowest = double.MaxValue; var open = true;
                    for (var t = 0.05; t < 0.97 && open; t += 0.03)
                    {
                        double px = ex + (sx - ex) * t, py = ey + (sy - ey) * t, pz = ez + (sz - ez) * t;
                        lowest = Math.Min(lowest, py - Ground(px, pz));
                        open = lowest > 0 && !Hidden(px, py, pz);
                    }
                    if (open) { seen++; clear = Math.Min(clear, lowest); }
                }
                var score = seen * 1000 + Math.Min(clear, 900) - far * 40;
                if (best is null || score > best.Value.Score) best = (ex, ey, ez, seen, clear, score);
            }
            if (best is not { Seen: > 0 } b) { report.Notes.Add("WARNING: no place for the drop's camera: the camera follows the car down"); continue; }
            report.JumpCameras.Add((j.Anim, j.CubeX, j.CubeZ, b.X, b.Y, b.Z));
            report.Notes.Add($"the drop's camera: at cell ({b.X:0.0}, {b.Z:0.0}), {b.Y:0} up; it sees {b.Seen} of {sights.Count} of the car's places along the flight and past the landing, " +
                             $"its lines to them {b.Clear:0} over the ground at the least");
        }
    }

    // Each loop's ring (RaceTrackLoopBody): a decor of its own at its foot, its box -- the ring's footprint on the road, its top far
    // under it -- touching nothing (the engine carries the car round the ring; the box's corners decide whether the ring is drawn).
    private static void PlaceLoops(IslandFile island, RaceTrackOptions o, RaceTrackReport report)
    {
        if (o.NewBodyBase < 0) { report.Notes.Add("WARNING: no place for the loops' rings was prepared (the island's OBL wasn't counted) -- no rings."); return; }
        foreach (var l in report.Loops)
        {
            var wx = l.X * 512; var wz = l.Z * 512;
            if (IslandDecors.Locate(island, wx, wz) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCube) { report.Notes.Add($"WARNING: the loop at cell ({l.X:0.0}, {l.Z:0.0}): no room for its ring"); continue; }
            var along = new System.Numerics.Vector3((float)l.DirX, 0, (float)l.DirZ);
            var radius = l.Radius * 512; var shift = l.Shift * 512; var half = l.Band * 512;
            var bodies = RaceTrackLoopBody.Ring(along, radius, shift, half, l.Gap * Math.PI / 180);
            if (at.Cube.Decors.Count + bodies.Count > IslandDecors.MaxPerCube) { report.Notes.Add($"WARNING: the loop at cell ({l.X:0.0}, {l.Z:0.0}): no room for its ring"); continue; }
            var y = (int)Math.Round(l.Y);
            var reach = (int)Math.Ceiling(radius + RaceTrackLoopBody.Thickness + 2 * RaceTrackLoopBody.LegHalf);
            var wide = (int)Math.Ceiling(shift / 2 + half + RaceTrackLoopBody.RailWidth);
            int ex = (int)Math.Ceiling(Math.Abs(l.DirX) * reach + Math.Abs(l.DirZ) * wide), ez = (int)Math.Ceiling(Math.Abs(l.DirZ) * reach + Math.Abs(l.DirX) * wide);
            // (the ring in quarters, a decor each at its foot, each with the ring's whole footprint as its box)
            foreach (var body in bodies)
            {
                var d = IslandDecors.Blank(o.NewBodyBase + report.NewBodies.Count, at.X, y, at.Z, 0);
                d.XMin = at.X - ex; d.XMax = at.X + ex; d.ZMin = at.Z - ez; d.ZMax = at.Z + ez; d.YMin = y - 200; d.YMax = NoBoxTop;
                at.Cube.Decors.Add(d);
                report.NewBodies.Add(body);
            }
            report.Placed.Add($"loop at cell ({l.X:0.0}, {l.Z:0.0}): a ring of {l.Radius:0.0} cells, {l.Shift:0.0} across, its band {2 * half / 512:0.0} wide{(l.Gap > 0 ? $", a gap of {l.Gap:0} degrees at its top" : "")}, height {y}");
        }
    }

    // a loop ring's band: from its middle to its rails (cells), when the plan doesn't say
    private const double LoopBandHalf = 1.3;

    // The racing lines past a loop (the opponents' and the test pilot's: the race-track mode takes them round it as it does the player's
    // car) run into its ring on its own lane -- left of the road's middle by half the loop's shift -- and away from it on the other.
    private static void LaneIntoLoops(RaceTrackReport report, List<(double X, double Z, double Y, double Speed, double Radius)> line)
    {
        foreach (var l in report.Loops)
        {
            double nx = -l.DirZ, nz = l.DirX;
            for (var i = 0; i < line.Count; i++)
            {
                var p = line[i];
                double a = (p.X - l.X) * l.DirX + (p.Z - l.Z) * l.DirZ, s = (p.X - l.X) * nx + (p.Z - l.Z) * nz;
                if (Math.Abs(a) > 16 || Math.Abs(s) > 6) continue;
                var lane = a < 0 ? -l.Shift / 2 : l.Shift / 2;
                var w = a < 0 ? Smooth((a + 16) / 8) : Smooth((16 - a) / 8);
                var t = s + (lane - s) * w;
                line[i] = (l.X + l.DirX * a + nx * t, l.Z + l.DirZ * a + nz * t, p.Y, p.Speed, p.Radius);
            }
        }
        static double Smooth(double u) { u = Math.Clamp(u, 0, 1); return u * u * (3 - 2 * u); }
    }

    // The raised road (RaceTrackPlan.Raised): its pieces, a decor body each (RaceTrackRaisedBody.Tile) RaisedPiece cells long, and a pier
    // every RaisedPier cells where the road stands RaisedPierFrom or more over the ground -- each pier a body of its own height, moved a
    // few cells along the road where it would stand on another part of the lap or in a decor object, and left out where there is no clear
    // place. The pieces' decors get a box nothing can touch: a decor's box is its collision, level and axis-aligned, and a box round a
    // sloping, turned piece would be a staircase of invisible floors and walls; the engine has the road as a floor of its own instead
    // (report.Raised, the road's middle point by point, for RACETRACK.JSON), so nothing may collide with the pieces themselves. Even a
    // box a few units across under the road's middle was a floor: on the bridge's steep fall the engine held the car's box, which
    // reaches over it, at that little box's top for as long as the car passed it, the road already hundreds of units lower, and then
    // dropped the car. So a piece's box has its top far under its own bottom (the engine's tests all need the top at or over something;
    // its bottom corners, at the piece's real place, are what decides whether the piece is drawn). A pier's box is its column, ending
    // well under the road all the way along the car's length.
    private const double RaisedPiece = 4, RaisedPier = 10, RaisedPierFrom = 900, PierHalf = 230, PierBeam = 260;
    private const int NoBoxTop = -32000, PierClear = 5, RaisedArrowEvery = 3;
    private const double RaisedGantryClear = 2300, RaisedGantryBeam = 640;

    private static void PlaceRaised(IslandFile island, TrackRoad r, RaceTrackOptions o, RaceTrackReport report, int startIndex = -1)
    {
        var up = r.Raised!; var n = r.Count;
        // (a lap that is all raised road starts at its first point and closes on it)
        var loop = r.Closed && up.All(u => u);
        var first = loop ? 0 : -1;
        for (var i = 0; i < n && first < 0; i++) if (up[i] && !up[At(r, i - 1)]) first = i;
        if (first < 0) return;
        var span = new List<int>();
        // (a sprint's road ends where its route does: At holds the last point there. Its raised stretches -- it may have several -- are
        // one list for the engine, the ground road between them points with no width: no floor of the raised road's there)
        if (!r.Closed) { var last = Enumerable.Range(0, n).Last(i => up[i]); for (var k = first; k <= last; k++) span.Add(k); }
        else for (var k = first; up[k] && span.Count < n; k = At(r, k + 1)) span.Add(k);
        if (loop) span.Add(first);
        report.RaisedLoop = loop;
        var half = o.RaisedHalfWidth * 512;
        double Half(int k) => (r.RaisedHalfs?[k] ?? o.RaisedHalfWidth) * 512;
        var banks = r.RoadBank;
        // (a jump's gap in it -- between the lips, r.Gap -- has no width: the engine's floor isn't there)
        foreach (var k in span)
        {
            var width = r.Gap[k] || !up[k] ? 0 : (int)Math.Round(Half(k));
            report.Raised.Add(banks is null
                ? new[] { (int)Math.Round(r.X[k] * 512), (int)Math.Round(r.Z[k] * 512), (int)Math.Round(r.H[k]), width }
                : new[] { (int)Math.Round(r.X[k] * 512), (int)Math.Round(r.Z[k] * 512), (int)Math.Round(r.H[k]), width, (int)Math.Round(banks[k] * RaisedBankUnits) });
        }
        // (the carried jumps' feet and lips as places in that list: the engine's way over each)
        foreach (var (arc, which) in report.ArcJumps.Select((a, i) => (a, i)))
        {
            var ids = new[] { arc.Foot, arc.Lip, arc.Land, arc.LandFoot }.Select(k => span.IndexOf(k)).ToArray();
            // (the camera's side, when the plan gives it: the engine's arcjump= fifth number)
            if (ids.All(i => i >= 0) && ids[0] < ids[1] && ids[1] < ids[2] && ids[2] < ids[3])
                report.ArcRaised.Add(which < report.ArcCameras.Count && report.ArcCameras[which] is int side ? ids.Append(side).ToArray() : ids);
            else report.Notes.Add($"WARNING: the carried jump from cell ({r.X[arc.Foot]:0.0}, {r.Z[arc.Foot]:0.0}) is not along the raised road in one piece: the engine won't carry the car over it");
        }
        if (o.NewBodyBase < 0) { report.Notes.Add("WARNING: no place for the raised road's bodies was prepared (the island's OBL wasn't counted) -- the raised road has no pieces."); return; }

        System.Numerics.Vector3 World(int k) => new((float)(r.X[k] * 512), (float)r.H[k], (float)(r.Z[k] * 512));
        // (across the road, to the left of the way the lap runs: level, or leaning with the road's banking)
        System.Numerics.Vector3 Across(int k) => new((float)-r.Tz[k], (float)(banks?[k] ?? 0), (float)r.Tx[k]);
        bool Add(byte[] body, double wx, double wy, double wz, int x0, int y0, int z0, int x1, int y1, int z1, bool untouchable = false)
        {
            if (IslandDecors.Locate(island, wx, wz) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCube) return false;
            var d = IslandDecors.Blank(o.NewBodyBase + report.NewBodies.Count, at.X, (int)Math.Round(wy), at.Z, 0);
            d.XMin = at.X + x0; d.XMax = at.X + x1; d.YMin = d.Y + y0; d.YMax = untouchable ? NoBoxTop : d.Y + y1; d.ZMin = at.Z + z0; d.ZMax = at.Z + z1;
            at.Cube.Decors.Add(d);
            report.NewBodies.Add(body);
            return true;
        }

        bool Striped(int k) => r.Stripe is { } st && (st.From <= st.To ? k >= st.From && k <= st.To : k >= st.From || k <= st.To);

        // the pieces: cross-sections a cell apart, along each stretch of the road between its jumps' gaps (each ends at a lip), every one
        // with as many strips across its asphalt as the widest needs (RaceTrackRaisedBody.Tile: they meet on the same points)
        var strips = RaceTrackRaisedBody.StripsFor(span.Max(k => (r.AsphaltHalf - o.RaisedHalfWidth) * 512 + Half(k)));
        var per = Math.Max(1, (int)Math.Round(1 / o.Spacing));                    // lap points to a cell
        var cells = Math.Max(1, (int)Math.Round(RaisedPiece));
        int pieces = 0, left = 0;
        var runs = new List<List<int>> { new() };
        foreach (var k in span) { if (r.Gap[k] || !up[k]) { if (runs[^1].Count > 0) runs.Add(new()); } else runs[^1].Add(k); }
        foreach (var run in runs.Where(u => u.Count >= 2))
        for (var t = 0; t + per < run.Count + per - 1 && t < run.Count - 1; t += cells * per)
        {
            var ids = new List<int>();
            for (var j = 0; j <= cells && t + j * per < run.Count; j++) ids.Add(run[t + j * per]);
            if (ids[^1] != run[^1] && t + cells * per >= run.Count - 1) ids.Add(run[^1]);      // (the last piece reaches the stretch's end)
            if (ids.Count < 2) break;
            var origin = World(ids[ids.Count / 2]);
            origin = new((float)Math.Round(origin.X), (float)Math.Round(origin.Y), (float)Math.Round(origin.Z));
            var sections = ids.Select(k => (World(k) - origin, Across(k))).ToList();
            // (its last cross-section a little into the next piece's first cell: two pieces' bodies projected each on its own leave a
            // crack of a pixel between them where they only meet -- the background through a dotted line across the road)
            var (endMid, endAcross) = sections[^1];
            var onward = System.Numerics.Vector3.Normalize(endMid - sections[^2].Item1);
            sections[^1] = (endMid + onward * (float)RaceTrackRaisedBody.PieceOverlap, endAcross);
            // (the start line, where it is on a lap that is all raised road: the cell after its point painted white)
            var line = -1;
            for (var j = 0; j + 1 < ids.Count && startIndex >= 0 && loop; j++) if (ids[j] == startIndex) line = j;
            // (the plan's stripe along the road, cell by cell: between the Emerald Moon's straight and its pit lane)
            var striped = ids.Take(ids.Count - 1).Select((k, j) => Striped(k) && Striped(ids[j + 1])).ToArray();
            // (an arrow on every third piece, the way the lap runs; none across the stripe)
            var arrow = pieces % RaisedArrowEvery == 1 && ids.Count == cells + 1 && line < 0 && !striped.Any(b => b);
            var body = RaceTrackRaisedBody.Tile(sections, t / per, r.AsphaltHalf * 512, r.CurbHalf * 512, half, arrow, line,
                r.RaisedHalfs is null ? null : ids.Select(k => Half(k) - half).ToArray(),
                r.Stripe is { } st && !o.PitFence ? st.Offset * 512 : double.NaN, striped, strips, r.Themes?[ids[ids.Count / 2]]);   // (under a fence, no stripe)
            // (its box over its footprint, touching nothing: the engine leaves out a decor whose middle is behind the camera unless a corner
            // of its box is in front of it -- 3DEXT/DECORS.CPP -- and a piece whose middle had just passed under the camera was a hole)
            var corners = ids.SelectMany(k => new[] { -1, 1 }.Select(side => World(k) + new System.Numerics.Vector3((float)-r.Tz[k], 0, (float)r.Tx[k]) * (float)(side * (Half(k) + 64)) - origin)).ToList();
            int x0 = (int)Math.Floor(corners.Min(c => c.X)), x1 = (int)Math.Ceiling(corners.Max(c => c.X)), z0 = (int)Math.Floor(corners.Min(c => c.Z)), z1 = (int)Math.Ceiling(corners.Max(c => c.Z));
            if (Add(body, origin.X, origin.Y, origin.Z, x0, -(int)RaceTrackRaisedBody.Thickness - 80, z0, x1, 0, z1, untouchable: true)) pieces++; else left++;
        }

        // the gantry over a start line that is on the raised road: one body, its posts on the rails (nothing collides with it)
        if (loop && startIndex >= 0)
        {
            var at = World(startIndex);
            var gantry = RaceTrackRaisedBody.Gantry(new System.Numerics.Vector3((float)-r.Tz[startIndex], 0, (float)r.Tx[startIndex]), Half(startIndex), RaisedGantryClear, RaisedGantryBeam);
            if (Add(gantry, Math.Round(at.X), Math.Round(at.Y), Math.Round(at.Z), -16, -(int)RaceTrackRaisedBody.Thickness - 80, -16, 16, 0, 16, untouchable: true))
                report.Placed.Add($"start line: a gantry on the raised road at cell ({r.X[startIndex]:0.0}, {r.Z[startIndex]:0.0}), height {r.H[startIndex]:0}, {RaisedGantryClear + RaisedGantryBeam:0} to its top");
            else left++;
        }

        if (o.PitFence && r.Stripe is { } fenced && o.FenceSection is { } section && o.FencePost is { } post) left += PlaceFence(island, r, o, report, fenced, section, post);

        var (piers, none) = o.PierBodies is { } stand && o.SceneryObl is { } obl && RaceTrackScenery.Load(island, obl) is { } scenery
            ? PlacePiersOnScenery(island, r, o, report, span, loop, scenery, stand, Across, Add, ref left)
            : PlacePiers(island, r, o, report, span, Across, Add, ref left);
        if (left > 0) report.Notes.Add($"WARNING: {left} pieces of the raised road were left out: the cube already holds {IslandDecors.MaxPerCube} decors");
        report.Placed.Add($"raised road: {(span.Count(k => up[k]) - (loop ? 1 : 0)) * o.Spacing:0} cells in {pieces} pieces of {RaisedPiece:0} cells and {piers} piers" +
                          (none > 0 ? $" ({none} more left out: no clear ground under the road there)" : "") +
                          $", {report.NewBodies.Count} new decor bodies from {o.NewBodyBase} on");
    }

    // A raised road's banking in the engine's file: this many to one (RACEMOD.CPP RAISED_BANK_UNITS).
    public const double RaisedBankUnits = 10000;

    // The fence along a raised road's stripe (RaceTrackPlan.PitFence): Citadel Island's white fence, a section every FenceStep along the
    // stripe from its first point to its last, turned along the road, and its end post after the last; solid, its box the section's own
    // (the road beside the Emerald Moon's pit lane runs straight along the island's grid, so a box fits it). Returns how many were left out.
    private const double FenceStep = 1000;

    private static int PlaceFence(IslandFile island, TrackRoad r, RaceTrackOptions o, RaceTrackReport report, (int From, int To, double Offset) stripe,
        byte[] section, byte[] post)
    {
        // the stripe's line, point by point (world units), and the fence's two bodies' shapes
        var line = new List<(double X, double Z, double Y)>();
        for (var k = stripe.From; ; k = At(r, k + 1))
        {
            line.Add(((r.X[k] - r.Tz[k] * stripe.Offset) * 512, (r.Z[k] + r.Tx[k] * stripe.Offset) * 512, r.H[k]));
            if (k == stripe.To || line.Count > r.Count) break;
        }
        static (float X0, float X1, float Y1, float Z0, float Z1) Bounds(byte[] body)
        {
            var b = LbaBodyStudio.Body.Read(body, 2, allowStatic: true);
            return (b.Vertices.Min(v => v.X), b.Vertices.Max(v => v.X), b.Vertices.Max(v => v.Y), b.Vertices.Min(v => v.Z), b.Vertices.Max(v => v.Z));
        }
        var sb = Bounds(section); var pb = Bounds(post);
        int sectionBody = o.NewBodyBase + report.NewBodies.Count; report.NewBodies.Add(section);
        int postBody = o.NewBodyBase + report.NewBodies.Count; report.NewBodies.Add(post);
        var cum = new List<double> { 0 };
        for (var i = 1; i < line.Count; i++) cum.Add(cum[^1] + Math.Sqrt(Sq(line[i].X - line[i - 1].X) + Sq(line[i].Z - line[i - 1].Z)));
        (double X, double Z, double Y, double DX, double DZ) Along(double s)
        {
            var i = 0;
            while (i < line.Count - 2 && cum[i + 1] < s) i++;
            var len = Math.Max(1e-6, cum[i + 1] - cum[i]); var t = Math.Clamp((s - cum[i]) / len, 0, 1);
            return (line[i].X + (line[i + 1].X - line[i].X) * t, line[i].Z + (line[i + 1].Z - line[i].Z) * t, line[i].Y + (line[i + 1].Y - line[i].Y) * t,
                (line[i + 1].X - line[i].X) / len, (line[i + 1].Z - line[i].Z) / len);
        }
        int placed = 0, left = 0;
        bool Put(int body, (float X0, float X1, float Y1, float Z0, float Z1) b, double wx, double wy, double wz, int turn)
        {
            if (IslandDecors.Locate(island, wx, wz) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCube) return false;
            var d = IslandDecors.Blank(body, at.X, (int)Math.Round(wy), at.Z, turn);
            // (the box: the body's own, turned as the engine turns it -- x' = x cos + z sin, z' = -x sin + z cos)
            var a = turn * 2 * Math.PI / 4096; double c = Math.Cos(a), sn = Math.Sin(a);
            var xs = new[] { b.X0, b.X1 }.SelectMany(x => new[] { b.Z0, b.Z1 }.Select(z => (X: x * c + z * sn, Z: -x * sn + z * c))).ToList();
            d.XMin = at.X + (int)Math.Floor(xs.Min(p => p.X)); d.XMax = at.X + (int)Math.Ceiling(xs.Max(p => p.X));
            d.ZMin = at.Z + (int)Math.Floor(xs.Min(p => p.Z)); d.ZMax = at.Z + (int)Math.Ceiling(xs.Max(p => p.Z));
            d.YMin = d.Y; d.YMax = d.Y + (int)Math.Ceiling(b.Y1);
            at.Cube.Decors.Add(d);
            return true;
        }
        // (the section's length along its own x: from its post to where the next one's begins)
        double s0 = -sb.X0;
        for (var s = s0; s + sb.X1 <= cum[^1] + 1; s += FenceStep)
        {
            var p = Along(s);
            // (turned so its own x runs the way the road does: the engine turns a body's x towards (cos b, -sin b))
            var turn = ((int)Math.Round(Math.Atan2(-p.DZ, p.DX) / (2 * Math.PI) * 4096) % 4096 + 4096) % 4096;
            if (Put(sectionBody, sb, p.X, p.Y, p.Z, turn)) placed++; else left++;
            s0 = s;
        }
        var end = Along(s0 + FenceStep);
        var endTurn = (((int)Math.Round(Math.Atan2(-end.DZ, end.DX) / (2 * Math.PI) * 4096) + 2048) % 4096 + 4096) % 4096;
        if (Put(postBody, pb, end.X, end.Y, end.Z, endTurn)) placed++; else left++;
        report.Placed.Add($"pit lane fence: {placed} pieces of Citadel Island's white fence along the stripe, {cum[^1] / 512:0.0} cells, {(left > 0 ? $"{left} left out (the cube's decors are full)" : "solid")}");
        return left;
    }

    private delegate bool AddDecor(byte[] body, double wx, double wy, double wz, int x0, int y0, int z0, int x1, int y1, int z1, bool untouchable = false);

    // The piers under the road's middle, on the ground: one every RaisedPier cells, moved a few cells along the road where it would stand
    // on another part of the lap, where an opponent waits or in a decor object's box.
    private static (int Piers, int None) PlacePiers(IslandFile island, TrackRoad r, RaceTrackOptions o, RaceTrackReport report, List<int> span,
        Func<int, System.Numerics.Vector3> across, AddDecor add, ref int left)
    {
        var up = r.Raised!; var n = r.Count;
        (double X0, double Z0, double X1, double Z1, double Y1)[] boxes = IslandOps.CubeCells(island).SelectMany(c => c.Item3.Decors.Select(d =>
            ((c.Item1 * (double)IslandFile.CubeSize + d.XMin) / 512, (c.Item2 * (double)IslandFile.CubeSize + d.ZMin) / 512,
             (c.Item1 * (double)IslandFile.CubeSize + d.XMax) / 512, (c.Item2 * (double)IslandFile.CubeSize + d.ZMax) / 512, (double)d.YMax))).ToArray();
        bool Clear(int k, double ground)
        {
            // not on another part of the lap (any road under this point, raised or on the ground) ...
            for (var m = 0; m < n; m++)
            {
                var sep = Math.Abs(r.S[m] - r.S[k]); sep = Math.Min(sep, r.Length - sep);
                if (sep < 12) continue;
                var reach = (up[m] ? r.RaisedHalfs?[m] ?? o.RaisedHalfWidth : r.VergeHalf) + 1.2;
                if (Sq(r.X[m] - r.X[k]) + Sq(r.Z[m] - r.Z[k]) < reach * reach && r.H[m] < r.H[k] - 300) return false;
            }
            // ... nor where an opponent's car waits
            foreach (var p in report.Pits) if (Sq(p.X - r.X[k]) + Sq(p.Z - r.Z[k]) < Sq(2.5)) return false;
            // ... nor in a decor object that stands there
            foreach (var b in boxes)
                if (r.X[k] > b.X0 - 0.7 && r.X[k] < b.X1 + 0.7 && r.Z[k] > b.Z0 - 0.7 && r.Z[k] < b.Z1 + 0.7 && b.Y1 > ground + 50) return false;
            return true;
        }
        int piers = 0, none = 0;
        var every = (int)Math.Round(RaisedPier / o.Spacing);
        for (var t = every / 2; t < span.Count; t += every)
        {
            if (!up[span[t]]) continue;                                                      // (a sprint's ground road between its stretches)
            var placed = false;
            foreach (var shift in new[] { 0, 2, -2, 4, -4, 6, -6, 8, -8 })
            {
                var at = t + shift;
                if (at < 1 || at >= span.Count - 1) continue;
                var k = span[at];
                if (r.Gap[k] || r.Gap[span[at - 1]] || r.Gap[span[at + 1]]) continue;      // (not in a jump's gap)
                if (!up[k] || !up[span[at - 1]] || !up[span[at + 1]]) continue;
                var ground = IslandOps.Altitude(island, r.X[k] * 512, r.Z[k] * 512) ?? 0;
                var road = Math.Min(r.H[k], Math.Min(r.H[span[at - 1]], r.H[span[at + 1]]));
                var height = road - RaceTrackRaisedBody.Thickness - ground;
                // (the road's lowest within a car's length of the pier: the column's box ends under that)
                var lowest = Enumerable.Range(-PierClear, 2 * PierClear + 1).Where(j => at + j >= 0 && at + j < span.Count).Min(j => r.H[span[at + j]]);
                if (height < RaisedPierFrom) { placed = true; break; }        // low enough to need none
                if (!Clear(k, ground)) continue;
                var body = RaceTrackRaisedBody.Pier(height, PierHalf, across(k), (r.RaisedHalfs?[k] ?? o.RaisedHalfWidth) * 512 - 150, PierBeam, PierHalf, r.Themes?[k]);
                var top = (int)Math.Round(Math.Min(height - PierBeam - 60, lowest - ground - 420));
                if (add(body, r.X[k] * 512, ground, r.Z[k] * 512, -(int)PierHalf, 0, -(int)PierHalf, (int)PierHalf, Math.Max(1, top), (int)PierHalf)) piers++; else left++;
                placed = true;
                break;
            }
            if (!placed) none++;
        }
        return (piers, none);
    }

    // The piers of a road that stands among things (RaceTrackPlan.PierBodies: the Elevator Platform's lap, round a tower, over its decks
    // and over itself): each stands on what is really under it -- the sea, the ground, or a deck the plan lets it stand on -- and clear of
    // everything else's real shape, the lap's other levels included. Under the road's middle where it can; else beside the road, outside
    // its rail, with its beam reaching under the road from there -- on a helix that puts a level's columns beside the levels below it.
    // One every RaisedPier cells, moved up to four cells along the road to find a place.
    private const double PierBeside = 0.6;       // cells from the rail to the middle of a column beside the road
    private static (int Piers, int None) PlacePiersOnScenery(IslandFile island, TrackRoad r, RaceTrackOptions o, RaceTrackReport report, List<int> span, bool loop,
        RaceTrackScenery scenery, HashSet<int> stand, Func<int, System.Numerics.Vector3> across, AddDecor add, ref int left)
    {
        var n = r.Count;
        var count = loop ? span.Count - 1 : span.Count;
        int Point(int at) => loop ? span[((at % count) + count) % count] : span[Math.Clamp(at, 0, count - 1)];
        // what a column at a place (world units) can stand on, up to `top`: the height, or null when something is in its way
        double? Footing(double wx, double wz, double top)
        {
            double? foot = null;
            foreach (var (dx, dz) in new[] { (0.0, 0.0), (-PierHalf, -PierHalf), (PierHalf, -PierHalf), (PierHalf, PierHalf), (-PierHalf, PierHalf) })
            {
                var ground = IslandOps.Altitude(island, wx + dx, wz + dz) ?? 0;
                var y = ground;
                if (scenery.Under(wx + dx, wz + dz, top) is { } under && under.Y > ground + 60)
                {
                    if (!stand.Contains(under.Body)) return null;
                    y = under.Y;
                }
                if (foot is { } f && Math.Abs(f - y) > 60) return null;        // (half on a deck, half off it)
                foot ??= y;
            }
            return foot;
        }
        int piers = 0, none = 0;
        var every = (int)Math.Round(RaisedPier / o.Spacing);
        for (var t = every / 2; t < count; t += every)
        {
            var placed = false;
            foreach (var shift in new[] { 0, 2, -2, 4, -4, 6, -6, 8, -8 })
            {
                var at = t + shift;
                if (!loop && (at < 1 || at >= count - 1)) continue;
                var k = Point(at);
                var a = across(k);
                var bank = a.Y;
                var half = (r.RaisedHalfs?[k] ?? o.RaisedHalfWidth) * 512;
                // (beside the road: the outside of a bend first)
                var outer = r.Kappa[k] > 0 ? -1 : 1;
                foreach (var side in new[] { 0, outer, -outer })
                {
                    var lateral = side * (half + PierBeside * 512);               // from the road's middle, to the left positive
                    double wx = r.X[k] * 512 + a.X * lateral, wz = r.Z[k] * 512 + a.Z * lateral;
                    // the road's underside over the column (the banked surface carried out to it), and its lowest a car's length either way
                    var road = Enumerable.Range(-1, 3).Min(j => r.H[Point(at + j)]) + bank * lateral;
                    var top = road - RaceTrackRaisedBody.Thickness;
                    if (Footing(wx, wz, top) is not { } foot) continue;
                    var height = top - foot;
                    if (height < RaisedPierFrom) { if (side == 0) placed = true; break; }        // low enough to need none
                    // not through another part of the lap: no part of the road lower than this one within its width of the column
                    var blocked = false;
                    for (var m = 0; m < n && !blocked; m++)
                    {
                        var sep = Math.Abs(r.S[m] - r.S[k]); sep = Math.Min(sep, r.Length - sep);
                        if (sep < 12 || r.H[m] > r.H[k] - 300) continue;
                        blocked = Sq(r.X[m] * 512 - wx) + Sq(r.Z[m] * 512 - wz) < Sq((r.RaisedHalfs?[m] ?? o.RaisedHalfWidth) * 512 + PierHalf + 60);
                    }
                    // ... nor where an opponent's car waits, nor through anything that stands there
                    foreach (var p in report.Pits) if (Sq(p.X * 512 - wx) + Sq(p.Z * 512 - wz) < Sq(2.5 * 512)) blocked = true;
                    if (blocked || scenery.Touches(wx - PierHalf - 40, foot + 80, wz - PierHalf - 40, wx + PierHalf + 40, top - 40, wz + PierHalf + 40)) continue;
                    // the beam: under the road from rail to rail, and out to the column when that stands beside it
                    double lo = Math.Min(-(half - 150) - lateral, -PierHalf), hi = Math.Max(half - 150 - lateral, PierHalf);
                    var body = RaceTrackRaisedBody.Pier(height, PierHalf, a, lo, hi, PierBeam, PierHalf, r.Themes?[k]);
                    var lowest = Enumerable.Range(-PierClear, 2 * PierClear + 1).Min(j => r.H[Point(at + j)]) - Math.Abs(bank) * half;
                    var box = (int)Math.Round(Math.Min(height - PierBeam - 60, lowest - foot - 420));
                    if (add(body, wx, foot, wz, -(int)PierHalf, 0, -(int)PierHalf, (int)PierHalf, Math.Max(1, box), (int)PierHalf)) piers++; else left++;
                    placed = true;
                    break;
                }
                if (placed) break;
            }
            if (!placed) none++;
        }
        return (piers, none);
    }

    // The raised road's surface at a place (island cells) for something at about height `near`: of the road's levels there, the one
    // nearest that height; null where the road isn't, or has no level within RaisedLevelApart / 2 of that height (Celebration Island's
    // bridge passes high over the apron its opponents wait on).
    private static double? RaisedFloor(TrackRoad r, RaceTrackOptions o, double x, double z, double near)
    {
        var up = r.Raised!;
        double? best = null; var score = double.MaxValue;
        for (var i = 0; i < r.Count; i++)
        {
            var j = At(r, i + 1);
            if (!up[i] || !up[j] || j == i) continue;
            double sx = r.X[j] - r.X[i], sz = r.Z[j] - r.Z[i]; var len2 = sx * sx + sz * sz;
            if (len2 < 1e-12) continue;
            var t = Math.Clamp(((x - r.X[i]) * sx + (z - r.Z[i]) * sz) / len2, 0, 1);
            double px = r.X[i] + sx * t, pz = r.Z[i] + sz * t;
            var d2 = Sq(x - px) + Sq(z - pz);
            if (d2 > Sq(r.RaisedHalfs is { } rh ? rh[i] + (rh[j] - rh[i]) * t : o.RaisedHalfWidth)) continue;
            var lat = (sx * (z - pz) - sz * (x - px)) / Math.Sqrt(len2);
            var bank = r.RoadBank is { } b ? b[i] + (b[j] - b[i]) * t : 0;
            var y = r.H[i] + (r.H[j] - r.H[i]) * t + bank * lat * 512;
            var s = Math.Abs(y - near) + Math.Sqrt(d2);
            if (s < score) { score = s; best = y; }
        }
        return best is { } b2 && Math.Abs(b2 - near) <= RaisedLevelApart / 2 ? best : null;
    }

    // The flat deck of a road-over-road bridge: a row of RaceTrackDeckBody tiles, each as wide as the upper road and a few
    // cells long, laid end to end along the bridge's own heading so the whole flat core (RoadBridgeInfo.Length) is covered.
    // RaceTrackDeckBody's mesh has local X = width (across the road) and local Z = length (along it), top surface at local Y 0.
    // The same rotation convention as PlaceGantry (whose body is authored the same way: local X the piece's long/across axis):
    // ex,ez is the world direction local +X should point along (here, across the bridge's own heading), theta = atan2(-ez, ex)
    // is the Beta the engine rotates the mesh by, and a local (lx, lz) offset lands at world (lx*cos + lz*sin, -lx*sin + lz*cos).
    // A railing piece's collision square: its centre this far outside the deck's edge, and its half size (world units). With the
    // car's own box (about 600 either way) the car's centre then stops about 5.1 cells from the centre line: wider than the
    // ramp's rock walls allow, so there is no funnel where the road meets the deck, and wide enough that the engine's nudge at a
    // cube border (a car crossing into the next cube is moved to half a brick inside it, up to 0.73 cells sideways on the
    // diagonal deck) doesn't push a car on the curb into a square. The same on the landings, where a car still carried by a
    // tile's overhang doesn't feel the rock paint either.
    private const double RailBoxOutset = 700, RailBoxHalf = 180;

    private static void PlaceDeck(IslandFile island, RoadBridgeInfo rb, RaceTrackOptions o, RaceTrackReport report)
    {
        if (o.DeckBodyIndex < 0) { report.Notes.Add("WARNING: no deck body was prepared (DESERT.OBL wasn't extended) -- the bridge has no physical deck."); return; }
        var ex = -rb.DirZ; var ez = rb.DirX;                              // across the bridge (local +X)
        var theta = Math.Atan2(-ez, ex);
        int Beta(double t) { var b = (int)Math.Round(t / (2 * Math.PI) * 4096); return ((b % 4096) + 4096) % 4096; }
        var beta = Beta(theta);
        var cos = Math.Cos(theta); var sin = Math.Sin(theta);
        // a grid of square tiles (see RaceTrackDeckBody.AppendTo for why): whole columns across the road, whole rows along it
        var tile = o.RoadBridgeTileLength * 512;
        var cols = RaceTrackDeckBody.Columns(o);   // the edge tiles' curb strip is placed for this many columns
        var rows = Math.Max(1, (int)Math.Ceiling(rb.Length / tile - 1e-6));
        var width = cols * tile; var length = rows * tile;
        var y = (int)Math.Round(rb.Height);
        int tiles = 0, rails = 0, copies = 0, full = 0;
        (double X, double Z) ToWorld(double lx, double lz) => (lx * cos + lz * sin, -lx * sin + lz * cos);
        // one piece: its centre (lx, lz) and half sizes (hx, hz) in the deck's own frame; its collision box is the axis-aligned
        // box round its turned corners, from yMin to yMax -- or, with `box`, a square of that half size round a point of the deck's frame
        bool Put(int body, int b, double lx, double lz, double hx, double hz, int yMin, int yMax, (double Lx, double Lz, double Half)? box = null)
        {
            var (ox, oz) = ToWorld(lx, lz);
            var wx = rb.X * 512 + ox; var wz = rb.Z * 512 + oz;
            double minX = 1e18, maxX = -1e18, minZ = 1e18, maxZ = -1e18;
            if (box is { } sq)
            {
                var (bx, bz) = ToWorld(sq.Lx, sq.Lz);
                minX = bx - ox - sq.Half; maxX = bx - ox + sq.Half; minZ = bz - oz - sq.Half; maxZ = bz - oz + sq.Half;
            }
            else
                foreach (var (cx, cz) in new[] { (-hx, -hz), (hx, -hz), (-hx, hz), (hx, hz) })
                {
                    var (rx, rz) = ToWorld(cx, cz);
                    minX = Math.Min(minX, rx); maxX = Math.Max(maxX, rx); minZ = Math.Min(minZ, rz); maxZ = Math.Max(maxZ, rz);
                }
            var (n, f) = AddToCubes(island, body, b, wx, y, wz, wx + minX, wx + maxX, yMin, yMax, wz + minZ, wz + maxZ);
            copies += n; full += f;
            return n > 0;
        }
        for (var row = 0; row < rows; row++)
        for (var col = 0; col < cols; col++)
        {
            // edge columns carry the red rail on their outer side: the +X column as is, the -X column turned half round
            var body = o.DeckBodyIndex; var b = beta;
            if (col == cols - 1) body = o.DeckBodyIndex + 1;
            else if (col == 0) { body = o.DeckBodyIndex + 1; b = (beta + 2048) % 4096; }
            if (Put(body, b, -width / 2 + tile * (col + 0.5), -length / 2 + tile * (row + 0.5), tile / 2, tile / 2, y - (int)o.RoadBridgeDeckThickness, y)) tiles++;
        }
        if (o.DeckRailings)
        {
            // Along both outer edges of the deck and its two landings. The deck runs diagonally and a decor's collision box is
            // axis-aligned: a box round a whole 2-cell piece reached a cell inside the rail, and the row of them made a sawtooth
            // that stopped a car dead 3 cells from the centre line (and head-on where the road meets the deck). The engine only
            // tests the box (and uses it for nothing else but a far-distance cull), so each piece's box is a small square just
            // outside the deck's edge instead: a car anywhere on the road never touches one; a car that runs onto the concrete
            // strip is stopped before it can drive off (the gaps between the squares are narrower than the car). The boxes
            // reach well above the step a car may climb (DEMI_BRICK_Y, 128), so they are walls, not floors.
            // (local +Z runs against the bridge's heading: the landing past its end is at -Z)
            var landing = o.RoadBridgeLanding * 512;
            double from = -length / 2 - (rb.RailAhead ? landing : 0), to = length / 2 + (rb.RailBehind ? landing : 0);
            var span = to - from;
            var pieces = Math.Max(1, (int)Math.Round(span / RaceTrackDeckBody.RailLength));
            var lateral = width / 2 - RaceTrackDeckBody.RailThickness / 2;
            foreach (var side in new[] { -1, 1 })
                for (var i = 0; i < pieces; i++)
                {
                    var lz = from + (i + 0.5) * span / pieces;
                    if (Put(o.DeckBodyIndex + 2, side > 0 ? beta : (beta + 2048) % 4096, side * lateral, lz, 0, 0,
                            y - 100, y + (int)RaceTrackDeckBody.RailHeight + 100, (side * (width / 2 + RailBoxOutset), lz, RailBoxHalf))) rails++;
                }
        }
        if (full > 0) report.Notes.Add($"WARNING: {full} deck pieces were left out of a cube that already holds {IslandDecors.MaxPerCube} decors");
        report.Placed.Add($"road bridge: {tiles} deck tiles ({cols} x {rows} of {o.RoadBridgeTileLength:0} cells, {width / 512:0.#} x {length / 512:0.#} cells) and {rails} railing pieces ({copies} decors in all, counting the copies in each cube a piece reaches) at cell ({rb.X:0.0}, {rb.Z:0.0}), deck height {y}, turn {beta}, over road {rb.UnderRoad}");
    }

    // Adds one decor to every cube its collision box reaches into, each copy in that cube's own frame (its position may then lie
    // outside 0..32767). The engine only knows the decors of the cube the hero is in (LOADISLE.CPP LoadCube: ListDecors is that
    // cube's list alone), for drawing and for collision alike, so a deck tile kept only in the cube that holds its centre was
    // neither seen nor solid from the next one: a car driving the deck over a cube border fell through it (Y 3436 to 388 in one
    // frame). Returns how many copies went in, and how many cubes were too full to take one.
    private static (int Placed, int Full) AddToCubes(IslandFile island, int body, int beta, double wx, int y, double wz,
        double minX, double maxX, int yMin, int yMax, double minZ, double maxZ)
    {
        var size = (double)IslandFile.CubeSize;
        int placed = 0, full = 0;
        for (var cz = (int)Math.Floor(minZ / size); cz <= (int)Math.Floor(maxZ / size); cz++)
        for (var cx = (int)Math.Floor(minX / size); cx <= (int)Math.Floor(maxX / size); cx++)
        {
            if (island.CubeAt(cx, cz) is not { } cube) continue;
            if (cube.Decors.Count >= IslandDecors.MaxPerCube) { full++; continue; }
            var ox = cx * size; var oz = cz * size;
            var d = IslandDecors.Blank(body, (int)Math.Round(wx - ox), y, (int)Math.Round(wz - oz), beta);
            d.XMin = (int)Math.Floor(minX - ox); d.XMax = (int)Math.Ceiling(maxX - ox);
            d.ZMin = (int)Math.Floor(minZ - oz); d.ZMax = (int)Math.Ceiling(maxZ - oz);
            d.YMin = yMin; d.YMax = yMax;
            cube.Decors.Add(d);
            placed++;
        }
        return (placed, full);
    }

    // The retail overpass of the Desert track (bodies 68 the arched deck, 69 and 70 the abutments), boxes relative to the piece's own origin.
    private static readonly (int Body, int[] Box)[] ArchPieces =
    {
        (68, new[] { -3499, 2260, -853, -616, 3288, 340 }),
        (69, new[] { -4115, 683, -1043, -3499, 2692, 530 }),
        (70, new[] { -616, 85, -1043, 0, 2692, 530 }),
    };

    // Three arched decks side by side between the two abutments, across the two roads where the lap crosses itself. The ground is one
    // surface, so the roads meet at grade and the bridge stands over the junction: the car drives under it.
    private static void PlaceBridge(IslandFile island, double cx, double cz, double height, double bisX, double bisZ, RaceTrackReport report, RaceTrackOptions o)
    {
        var ex = -bisZ; var ez = bisX;
        var theta = Math.Atan2(-ez, ex);
        var beta = (int)Math.Round(theta / (2 * Math.PI) * 4096); beta = ((beta % 4096) + 4096) % 4096;
        var cos = Math.Cos(theta); var sin = Math.Sin(theta);
        const double deckWidth = 2883, abutment = 616, decks = 3;
        var half = decks * deckWidth / 2;
        var pieces = new List<(int Body, int[] Box, double OriginX)>();
        for (var k = 0; k < decks; k++) pieces.Add((68, ArchPieces[0].Box, -half + deckWidth * (k + 0.5) + 2057.5));
        pieces.Add((69, ArchPieces[1].Box, -half - abutment + 4115));
        pieces.Add((70, ArchPieces[2].Box, half + abutment));
        var y = (int)Math.Round(height);
        var placed = 0;
        foreach (var (body, box, originLocal) in pieces)
        {
            var wx = (cx * 512 + ex * originLocal); var wz = (cz * 512 + ez * originLocal);
            if (IslandDecors.Locate(island, wx, wz) is not { } at || at.Cube.Decors.Count >= IslandDecors.MaxPerCube) continue;
            var d = IslandDecors.Blank(o.RetailBody(body), at.X, y, at.Z, beta);
            double minX = 1e18, maxX = -1e18, minZ = 1e18, maxZ = -1e18;
            foreach (var (bx, bz) in new[] { (box[0], box[2]), (box[3], box[2]), (box[0], box[5]), (box[3], box[5]) })
            {
                var rx = bx * cos + bz * sin; var rz = -bx * sin + bz * cos;
                minX = Math.Min(minX, rx); maxX = Math.Max(maxX, rx); minZ = Math.Min(minZ, rz); maxZ = Math.Max(maxZ, rz);
            }
            d.XMin = at.X + (int)Math.Floor(minX); d.XMax = at.X + (int)Math.Ceiling(maxX);
            d.ZMin = at.Z + (int)Math.Floor(minZ); d.ZMax = at.Z + (int)Math.Ceiling(maxZ);
            d.YMin = y + box[1]; d.YMax = y + box[4];
            at.Cube.Decors.Add(d);
            placed++;
        }
        report.Placed.Add($"crossing bridge: {placed} pieces (3 arched decks, 2 abutments) over cell ({cx:0.0}, {cz:0.0}), road height {y}, turn {beta}");
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // light

    private static void Relight(IslandFile island, RoadIndex index, RaceTrackOptions o)
    {
        var field = new IslandHeightField(island);
        var b = index.Bounds;
        foreach (var (cx, cz, cube) in IslandOps.CubeCells(island))
        {
            var options = BakeOptions.For(cube);
            _ = options;
        }
        var reference = island.Cubes.Values.First();
        var bake = BakeOptions.For(reference);
        for (var gz = Math.Max(0, b.Z0); gz <= Math.Min(Grid, b.Z1); gz++)
        for (var gx = Math.Max(0, b.X0); gx <= Math.Min(Grid, b.X1); gx++)
        {
            if (!island.HasVertex(gx, gz)) continue;
            var hits = index.Near(gx, gz, 14, 1);
            if (hits.Count == 0) continue;
            // (a raised road changes no ground: what lies under it keeps the island's own light, its statue's shadow and all)
            if (hits[0].Raised) continue;
            var target = IslandBake.Compute(field, new List<(double, double, double, double, double, double)>(), gx, gz, bake);
            // the steep ground under and beside the bridge's deck ends faces away from the light and baked almost black -- a dark
            // hole below the deck seen from the road; it is kept at least mid-bright
            if (hits[0].Deck || hits[0].Landing) target = Math.Max(target, 7);
            var current = island.LightAt(gx, gz) ?? 15;
            island.SetLight(gx, gz, (int)Math.Round(current + (target - current) * 0.9));
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // the island's heights and what is drawn, as flat arrays

    private sealed class Field
    {
        private readonly double[] heights = new double[(Grid + 1) * (Grid + 1)];
        private readonly bool[] has = new bool[(Grid + 1) * (Grid + 1)];
        private readonly bool[] drawn = new bool[Grid * Grid];

        public Field(IslandFile island)
        {
            Refresh(island);
            for (var cz = 0; cz < IslandFile.MapSize; cz++)
            for (var cx = 0; cx < IslandFile.MapSize; cx++)
            {
                if (island.CubeAt(cx, cz) is not { HasPolygons: true } cube) continue;
                for (var z = 0; z < 64; z++)
                for (var x = 0; x < 64; x++)
                {
                    var any = false;
                    for (var h = 0; h < 2; h++) { var p = new IslandPolygon(cube.Polygon(x, z, h)); if (p.TexFlag != 0 || p.PolyFlag != 0) any = true; }
                    drawn[(cz * 64 + z) * Grid + cx * 64 + x] = any;
                }
            }
        }

        public void Refresh(IslandFile island)
        {
            for (var gz = 0; gz <= Grid; gz++)
            for (var gx = 0; gx <= Grid; gx++)
            {
                var h = island.HeightAt(gx, gz);
                has[gz * (Grid + 1) + gx] = h.HasValue;
                heights[gz * (Grid + 1) + gx] = h ?? 0;
            }
        }

        public double VertexHeight(int gx, int gz) => heights[gz * (Grid + 1) + gx];

        public bool Drawn(double x, double z)
        {
            var gx = (int)Math.Floor(x); var gz = (int)Math.Floor(z);
            return (uint)gx < Grid && (uint)gz < Grid && drawn[gz * Grid + gx];
        }

        public double Height(double x, double z)
        {
            var gx = Math.Clamp((int)Math.Floor(x), 0, Grid - 1); var gz = Math.Clamp((int)Math.Floor(z), 0, Grid - 1);
            var fx = Math.Clamp(x - gx, 0, 1); var fz = Math.Clamp(z - gz, 0, 1);
            double H(int ix, int iz) => heights[iz * (Grid + 1) + ix];
            return H(gx, gz) * (1 - fx) * (1 - fz) + H(gx + 1, gz) * fx * (1 - fz) + H(gx, gz + 1) * (1 - fx) * fz + H(gx + 1, gz + 1) * fx * fz;
        }
    }

    private static double Sq(double v) => v * v;
}
