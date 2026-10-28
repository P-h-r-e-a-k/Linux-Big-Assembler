# The proposed race tracks, built on the Desert island, Citadel Island, Mosquibees Island, Celebration Island, the Elevator Platform, Sendell's Well, the moons and Polar Island (2026-09-27, reworked up to 2026-10-06)

Nine tracks are built by the same code: the Desert island's (this document's main subject), Citadel Island's town circuit (see "Citadel Island's town circuit") and its storm track (see "Citadel Island in the storm: a track of its own") -- one in each of the island's two files -- Mosquibees Island's mountain lap (see "Mosquibees Island's mountain lap") Celebration Island's lap up round the statue on a raised road (see "Celebration Island: round the statue") and its lava lake's (see "Celebration Island: the lava lake"), the Elevator Platform's rollercoaster (see "The Elevator Platform: a rollercoaster") a lap round Sendell's Well, the cut island the build makes (see "Walls, mines, checkpoints, Sendell's Well and a loop"), two vertical loops on the old moon, MOON.ILE (see "The old moon: two vertical loops"), the Emerald Moon's lap over its reactor (see "The Emerald Moon: over the reactor") and Polar Island's dream race, a sprint rather than a lap (see "Polar Island: the dream race to Sendell"). What differs between islands is `Terrain/RaceTrackIsland.cs`. Any of them can be built into one game folder together, and Play races the one of the island the editor has open (see "Several tracks in one game folder").

The picture the track came from is [racetrack/concept_track.png](racetrack/concept_track.png). It is built on a **copy** of the game, so nothing in the real game folders is touched.

| | |
|---|---|
| Where the copy is | `E:\dump\LBA2RaceTrackBuild\Game` (a plain copy of the "Level viewer" install without the CD images, `DOSBOX`, `DRIVERS` and the intro video except `VIDEO\VIDEO.HQR`, which the engine needs) |
| The originals of the files that change | `E:\dump\LBA2RaceTrackBuild\Pristine` (`DESERT.ILE`, `DESERT.OBL`, `SCENE.HQR`, `ANIM.HQR`, `RESS.HQR`); a build always starts from them, so it can be repeated |
| Files the build changes | `DESERT.ILE` (heights, ground triangles, decor objects), `DESERT.OBL` (three new decor bodies for the road bridge: a plain deck tile, an edge tile with the curb, and a railing), `SCENE.HQR` (scenes 55-73); for the jump also `ANIM.HQR` and `RESS.HQR` (its longer flight, see "The jump"); for Baldino's car and the fifty-eight cars after other characters `BODY.HQR` and `RESS.HQR` (see "Baldino's rocket car" and "Cars after the game's characters"). It writes `RACETRACK.JSON` beside them: the crossing style, the start line and the checkpoints, the opponent's line and actors, and the scene the race starts in, for Play's race-track mode |
| The game engine | LBA Assembler's Play runs the engine in its race-track mode on a folder with a race track built, and only then (see "The engine's race-track mode"): the car stays level on the bridge deck, has gears and the car setup, a lap counts only through every checkpoint, two opponents race round the lap (the retail track's racer and Baldino in his rocket car), and the gear, speed, lap times and race position are on screen. In the retail engine, or the community engine without that mode, the track and the jump work, but the car's body tilts over on the deck |
| To look at it | LBAAssembler > File > Settings > LBA2 folder = the copy, then Desert island (Explore), or Play: with the car setup's race start on (the default) Play starts the race in scene 67 beside the car, and with it off it plays whichever of scenes 55-73 is open |
| To rebuild | `ScriptRoundTrip buildtrack docs\racetrack\track_plan.json E:\dump\LBA2RaceTrackBuild\Pristine E:\dump\LBA2RaceTrackBuild\Game out.png 4` |

Pictures, all from the built copy:

- [Top-down map](racetrack/build/built_map.png). This renderer draws the ground only, so the bridge deck is not in it.
- [An earlier route over your picture](racetrack/build/built_over_concept.png): cyan is the lap before the retail track's area was added and the crossing re-shaped.
- In the game:
  - [the start line](racetrack/build/h_start.png)
  - [the water bridge](racetrack/build/h_bridge.png)
  - [walking the lower road towards the bridge, with the deck overhead](racetrack/build/h_under_bridge.png)
  - [on the deck](racetrack/build/h_deck_test.png)
  - [the deck's end: curbs, railings and the landing beyond](racetrack/build/h_deck_end.png)
  - [the pit lane entry](racetrack/build/h_pit_entry.png) and [its exit](racetrack/build/h_pit_exit.png)
  - [the race-track mode's display](racetrack/build/h_race_display.png) (gear, speed, lap times) and [the car level on the deck with it, tipping without it](racetrack/build/deck_race_mode.png)
- The jump crossing style: [the car in the air over the gap](racetrack/build/h_jump.png), [the gap and the down ramp from the other road](racetrack/build/h_jump_gap.png), [from above](racetrack/build/jump_top.png).
- The level crossing style (an old picture): [the level crossing](racetrack/build/h_cross.png).
- [The race car setup](racetrack/build/car_setup.png).
- The race: [Play in the app](racetrack/build/race_start_app.png) (Twinsen beside his car on the straight, the racer and Baldino on the grid, the editor's markings hidden), [the opponent setting off](racetrack/build/race_opponent.png), [the checkpoint messages](racetrack/build/checkpoints.png).
- Baldino's rocket car: [in the game, four ways round](racetrack/build/baldino_car_game.png), [in Body Studio's renderer](racetrack/build/baldino_car_preview.png), [racing](racetrack/build/baldino_race.png).
- The cars after the game's characters: [all fifty-nine](racetrack/build/character_cars_preview.png), [in the game](racetrack/build/character_cars_game.png).

## What was built

- **The lap** is 1351 cells long (about 691,500 world units). The plan is 1372 cells; straightening the road at the crossing shortens it. The road is 9 cells wide with one cell of red/white curb each side, the same widths as the retail Desert track. The centre line comes from the picture (see "Where it goes"), with one stretch re-routed through the retail track's own ground (see "The retail track's area").
- **The ground** under the road follows the natural height, smoothed and limited to a 9 % climb (exactly; see "The ground"). It is smoothed again so crests and dips are rounded, then levelled across the road with an embankment of about 7 cells each side that blends into the ground around it. 29,902 vertices were changed.
- **Banking**: each bend's outside edge is raised. The cross slope is 700 / turn radius in cells, at most 70 height units per cell (about 8 degrees).
- **Textures**, the ones catalogued in [LBA2_DESERT_RACE_TRACK.md](LBA2_DESERT_RACE_TRACK.md):
  - the asphalt tile (96,0);
  - the red curb (flat bank 4) alternating with the white curb texel (180,155);
  - 12 orange arrows (flat bank 5) after bends and on long straights, drawn triangle by triangle like the retail ones (see "The arrows");
  - the red/gold hatch tile (192,48) on the outer verge of bends, and sand on the rest of the verge;
  - the white start line row.
- **The pit lane** is 136 cells long, beside the bottom straight of the map (the yellow line of the picture). It has 5 cells of asphalt with its own curbs, runs 10.5 cells from the lap's centre line, and tapers onto the lap at both ends.
- **The start line** is a one-cell white row across the road at the middle of the pit lane. The retail gantry (bodies 64 + 65 + 66, checkered beam and red posts) stands over it, turned to the road.
- **The water bridge** takes the lap across a corner of the harbour on a raised causeway: 29 cells long, 19 of them over water, at least 700 units above the sea. Its steep rock sides carry the blocking bit (Col), so a car stays on it.
- **Where the lap crosses itself**, in one place that matches the picture's single "Bridge" label (see "Only one crossing"), there is a **road bridge** built the way Citadel Island's own plank bridges are built (see "The road bridge"). This is the default style, `CrossingStyle.Bridge`.
  - The crossing is first re-shaped to about the picture's own angle: 41 degrees, where the fitted centre line had flattened it to 17.
  - The straighter road climbs a 50-cell ramp, then crosses 4 cells of level landing onto a flat deck 2800 units above the other road. The deck is 68 cells long and 12 wide, laid as a grid of 51 decor tiles with the road's red and white curb along both edges and low red and white railings outside them.
  - The other road keeps its own grade and passes underneath.
  - Two other styles are still offered in the menu. A **jump**: the straighter road climbs a ramp to a lip, the car flies over a gap of sand, the other road and another gap, and lands on a down ramp (see "The jump"); it turns the crossing to the steepest angle that keeps the rest of the lap clear (42 degrees on this track) first, then squares the other road up to the jump. A **viaduct** is three arched decks and two abutments (retail bodies 68-70) over a level junction.
- **Decor**: 119 plants, posts, fences and small props on the road were removed. The set of body numbers is `RemovableBodies` in the code. Pieces placed at one origin go together, so a palm's trunk goes with its crown. The route was planned around every house. What was left of the retail Desert track (cube 7,10) is gone: its gantry, billboard, arch and wedge are removed, and its painted road is turned back to sand where the new lap doesn't run over it.
- **Scenes** (55-73, every outside scene of the island):
  - 163 actors are removed: all except Twinsen, the buggy, slot 1 (see below) and the 10 that the ferry and Dino-Fly cutscenes need (see "The stand-in, and the actors that stay"). An inert stand-in takes the removed actors' script references.
  - The fixed camera angles along the track are removed: 16 camera zones (see "Fixed cameras").
  - Track points and actors that stood on reshaped ground move with it.
  - The light is re-baked along the road.
  - A copy of the retail track's racer and one of Baldino's rocket car, the opponents' cars, are added to each of them (see "The engine's race-track mode" and "Baldino's rocket car").
- **Twinsen and the buggy** start in scene 67 next to the start line, facing the way the lap runs, with the racer's car on the grid beside the buggy and Baldino's on the second row.

## Things worth knowing

- **Only one crossing.** The lap's centre line crosses itself exactly once. This was checked three ways:
  - an exhaustive segment-intersection test in island-cell space;
  - the same test after the warp into the picture's space, used for the overlay picture;
  - the painted ground of a fresh build, where only one place shows two road arms meeting.

  An early bad build did cross itself three times, and each crossing got its own structure. `RaceTrackBuilder.Build` now logs a loud `WARNING` if `FindCrossings` ever returns more than one crossing, and treats only the first as intended.
- **The engine's ground is one height map** (`CalculAltitudeObjet`). A road cannot pass over another at the same place using terrain alone, which is why the road bridge is a decor object.
- **The buggy is one object for the whole game** (BUGGY.CPP). It exists in the cube that `INIT_BUGGY` last put it in. The island's scenes run `INIT_BUGGY(0)`, which only shows it where it already is, and every scene deletes it (`SUICIDE`) until the car quest (game variable 74) reaches 3. The copy's scripts were changed in three ways:
  - The quest test always passes (`IF VAR_GAME(74) >= 0`).
  - Scene 67 uses `INIT_BUGGY(2)`, which puts the buggy on the start line whenever that scene starts on foot.
  - While Twinsen drives (`comportement_hero() == 12`), scene 67 uses `INIT_BUGGY(0)` instead. Forcing the buggy onto the start line when Twinsen drives back into the scene on the next lap parked a second, solid buggy there for the car to crash into.

  The engine also rebuilds its background copy when Twinsen gets in (`TakeBuggy`); without that, the parked car stayed on screen as a ghost.
- **Actor slot 1 is kept.** Every scene has a bodiless "Zoe" placeholder in slot 1 (entity 14, at 0,0,0). With it deleted, the buggy moved into slot 1 and came up with no life; with it kept, the buggy's state matches the retail one exactly.
- **Not touched**:
  - the demo copies of these scenes (198-206), the interiors, the texts, and everything outside these three files;
  - the zones on the road (doors, hit, ladder, escalator, grid, rail): all were checked and none lie on it;
  - the garage beside the old track and its door into scene 54.
- **Where the track differs from the picture.** The picture is a hand-painted view of the island, not a map, so the lap was fitted to the real island. On average it is 3 cells from the drawing (90 % of it within 7 cells, 17 cells at worst), to go round the town's houses, the fortress and the big rocks.

## Known limitations

- **The game's ending hangs in scene 60** on a build with the actors removed: the ending's script waits on actors that are gone. Only the actors the travel cutscenes wait on directly are kept. "Put the original files back" undoes the build.
- **The ferry passes through the causeway** in the harbour's arrival and departure cutscenes. This is only visual; the cutscenes play to the end.
- **The ramps' sides.** A car steered hard off a ramp can still leave it: the blocking bit makes it slide along the side rather than stop dead (see "The road bridge"). Railings along the ramps would close this.
- **The tightest turns** are radius 2.6 cells (the north-west hairpin at cell 483, 534, straight from the picture, whose two legs are 9 cells apart), 3.8 cells (625, 643) and 4.4 cells (574, 556). Widening the hairpin means moving one leg, which changes the drawn shape.
- **The pit lane's second end** joins the lap on the road bridge's south-east ramp, where the road climbs 13-18 %.
- **On foot, Twinsen can pass between the railing squares** at the deck's edge. The gaps are narrower than the car, not than Twinsen.
- **Some of the island's own objects overhang the cut** where the road is carved into a hillside: a rock whose far edge loses the ground under it. Only what the build leaves hanging over most of its own footprint is cleared (see "Mosquibees Island's mountain lap"); a corner over the air is left as it is.
- **The deck tile nearest the camera** is sometimes clipped by the near plane in the engine's view. (The ground crossing the near plane used to be filled with black spikes -- TERRAIN.CPP `FillBlackPolyZBuf`, the original engine's handling; since 2026-09-29 it is drawn clipped, see "Mosquibees Island's mountain lap".)
- **The jump runs one way.** Driving the lap backwards, the car climbs the down ramp and drops off its top into the gap; from the gap it can drive on along the other road.
- **The gears, the car setup, the checkpoints, the opponent, the display and the level car on the deck** are the community engine's race-track mode, which only LBA Assembler's Play turns on. In the retail game the track, the bridge and the jump work, with the original car, and the car's body tilts over on the deck; the opponent's car stands still on the grid.
- **Celebration Island's and the Elevator Platform's raised roads are the race-track mode's alone**: without it the road is only decor, and nothing can drive on it (see "Celebration Island: round the statue" and "The Elevator Platform: a rollercoaster").
- **The opponents aren't driven by the game's physics.** Each follows a line planned with the track, at planned speeds. They have no collision, so they pass through the player's car and each other (the two lines meet in tight bends), and they don't react to the player. They wait while Twinsen is out of the car, and are only seen in the island's outside scenes.

## Where it goes (how the picture became coordinates)

1. The picture is the island seen from the other side (turned 180 degrees, tilted and stretched). It was registered to the island's own top-down map by 15 landmarks (the islets, the oasis, the harbour, the town, the retail track...) with a thin-plate spline: [tools/RaceTrackPlan/landmarks.json](../tools/RaceTrackPlan/landmarks.json), checked by [pic3.py](../tools/RaceTrackPlan/pic3.py).
2. The road was cut out of the picture by colour (lime, plus the red "Bridge" label), skeletonised, and followed along the skeleton between hand-placed waypoints ([pic5.py](../tools/RaceTrackPlan/pic5.py), [pic6.py](../tools/RaceTrackPlan/pic6.py), [pic7.py](../tools/RaceTrackPlan/pic7.py)). The yellow pit lane gave its two ends.
3. The centre line was fitted to the real ground ([plan5.py](../tools/RaceTrackPlan/plan5.py)). This is the cheapest path within 28 cells of the drawing, where houses, rocks, cliffs, the sea and the retail track cost a lot. A relaxation then pushes different parts of the track apart, to 14 cells where it can, except at the one crossing. In the final lap a few places come closer: 11 cells near the north-west hairpin, and 8 where the two roads converge on the crossing. The result is [racetrack/track_plan.json](racetrack/track_plan.json), in island cells counted from cell 448,448.
4. `Terrain/RaceTrackBuilder.cs` (ground, painting, decor) and `Terrain/RaceTrackScenes.cs` (scenes) build it. `tools/ScriptRoundTrip/RaceTrackCommand.cs` is the command line (`buildtrack`, `herostart`).

## Checking it without the editor

`tools/RaceTrackPlan/hshot.ps1 -cube 67 -tp "x y z"` starts the engine without a window, muted, on the sandbox copy. It teleports Twinsen and saves a screenshot and a state dump (the actors' positions and life). `tools/RaceTrackPlan/drive.ps1` drives the buggy the same way. Both take `-car <file>` to run the engine's race-track mode with a car setup file, as Play does on a folder with a race track built; without it they run the plain engine. The pictures above and the bridge faults were found with these. `gears.ps1` puts Twinsen in the buggy on the start line and prints its speed while it shifts gear at set ticks (`-keys '200:27,280:27'`, 27 being X's scancode and 29 Z's). `jumpdrive.py` drives a jump build over the jump and prints the car's path; build with `RT_CROSSING=Jump` and keep the build's output as `jumplog.txt` in the game folder. `ScriptRoundTrip racetrack island 2` lists the island's scenes, and `racetrack 9 10 --scripts` lists a cube's actors and scripts. `buildtrack` takes `RT_CROSSING=Bridge|Jump|Viaduct|Level`, and `RT_ARROWS=1` lists the arrows.

## The road bridge

The Citadel Island scene "at the Cliffs of the Woodbridge" (cube 9,7) has two short plank bridges over a canyon: a flat deck decor (CITADEL.OBL body 14, 1500 x 200 x 1300 units) with corner posts and rail braces at each end. The mechanism is `ReajustPosDecors` (EXTFUNC.CPP). When a character's or car's xz position is inside a decor's ZV box, the object is held at the box's `YMax`, whatever the terrain underneath is doing. So a decor is a second, independent floor, and that carries one road over another with no scripted jump.

**The bodies.** `Terrain/RaceTrackDeckBody.cs` builds three bodies with `BodyStudio.Body` and appends them to the copy's `DESERT.OBL` (`AppendTo`, which counts the entries with `HqrArchive.CountEntries`):

- a plain deck tile (index 106);
- an edge tile (107), whose top carries the road's curb and a concrete strip outside it;
- a railing (108).

Every face lists its points so its normal points out of the body, as the retail bodies do; the engine skips faces seen from behind. The colours are:

- the road's grey (palette 53) on top;
- red (75) and white (63) for the curb;
- a lighter grey (57) for the concrete;
- a darker grey (51) for the sides.

The white is the ground curb's own white: palette colours 0-15 are remapped on objects, so 15 draws lilac.

**Where the deck goes** (`RaceTrackBuilder.PlanRoadBridge`):

- `SteepenCrossing` turns the crossing to `BridgeCrossingAngle` (42 degrees asked for, 41 reached). It makes the straighter road dead straight over the deck, its landings and the ramp mouths, and joins that straight to the drawn road with cubic Hermite curves. Before this, the join had a kink of radius 2.8 cells south-east of the bridge.
- The straighter road keeps its own grade-limited height profile everywhere except a flat window around the crossing. That window is pinned to the other road's height there plus `RoadBridgeClearance` (2800) and blended in with a smoothstep over `RoadBridgeRampLength` (50) cells each side.
- Cells under the deck's core are marked (`TrackRoad.Deck`). `ModifyGround` leaves the ground there to the lower road, `PaintRoad` paints nothing there, and `PlaceDeck` lays the tiles instead.

Three numbers were found by testing in the engine:

- **Clearance 2800.** The engine's solid collision (`WorldColBrickDecors` / `TestZVDecorsZV`, EXTFUNC.CPP) tests the walker's whole box against the deck's box. So the deck's underside must clear a walker's head everywhere under it, and the lower road climbs a few hundred units under the deck's far end. At 900 and at 2300, Twinsen walking the lower road was stopped dead under the deck; at 2800 he walks straight through.
- **Deck length 68.** The ramp may only start where nothing of the lower road's shaping reaches any more: `VergeHalf + BlendWidth` to its side, plus the ramp's own shoulders and `RoadBridgeMargin`. Measured along the upper road, that is that distance / sin(angle) (`DeckHalfCells`). With a shorter deck, the lower road's embankment pulled the first ramp cells about 1000 units under the deck.
- **Square tiles.** A decor's collision box is axis-aligned. The deck is a grid of 4 x 4-cell squares (3 across, 17 along), whose boxes overhang the mesh by well under a cell. An earlier version laid long slabs diagonally, and each slab's box reached cells past the visible deck.

More was found by driving the buggy over it in the engine:

- **Every cube a piece reaches gets its own copy.** The engine only knows the decors of the cube the hero is in (`LoadCube`: `ListDecors` is that cube's list alone), for drawing and for collision. The deck crosses two cube borders. A tile kept only in the cube that holds its centre was neither drawn nor solid from the next cube, and a car driving over the border fell 3000 units in one frame. `PlaceDeck` adds every tile and railing to each cube its box overlaps, each copy in that cube's own frame: 138 decors in all.
- **The bridge heads.** The deck is a whole number of tiles long. The ground under its last 1.5 cells is shaped 40 below the deck's top, hidden by the tiles, so there is no crack at the joint. Past each end, the ground runs level with the deck for 4 cells (`RoadBridgeLanding`) before the ramp starts. These landings are 2 cells wider than the road's verge each side (`LandingExtra`), so an edge tile's overhanging box always lies over ground at its own height. The car steps about 10 units onto the deck and 1 off it.
- **The shoulders.** Beside the ramps and landings, a strip of rock one cell wide past the curb is passable, and a band of blocking rock (Col) runs beyond it, to 2.5 cells past the curb on the ramps (`RampShoulder`) and to the verge plus 2 on the landings. A car driven into Col stops as at a wall, but a car put down on Col skates out of control. When the car changes cube, the engine's snap can move it up to 0.73 cells sideways, so the passable strip keeps a car running along the curb off the Col.
- **Railings.** The railings are low red and white bars, 300 high, one piece per 2 cells, along both outer edges of the deck. A diagonal piece's own axis-aligned box would reach inside the rail, and a first version stopped the car dead on the road. So each piece's collision box is a small square (360 units wide) centred 700 units outside the deck's edge; the engine only tests the box. The outset also absorbs the 0.73-cell cube-change snap. A car on the road never touches one, and a car that runs onto the concrete strip is stopped before it can drive off: the gaps between the squares are narrower than the car, and the squares stand well above the step it may climb (`DEMI_BRICK_Y`, 128).

**Verified in the engine** (headless, muted, `drive.ps1` and `hshot.ps1`):

- Walking the lower road goes straight under the deck and out the far side.
- Driving up either ramp stays on the deck all the way across.
- In a sweep of 210 drives over the bridge heads and ramps, including hard steering, 1 car fell at a deck end and 5 left the side of a ramp. The rest crossed, or stopped against the blocking rock. In an earlier version, 85 of 144 fell at the north-west end.
- Drives along the cube borders, up to 4.2 cells off the centre line, all crossed.

### The car on the deck (engine patch)

The car tipped over on the bridge because of how its body is drawn, not where it is. `DoAnimBuggy` (BUGGY.CPP) tilts the car's body (pitch and roll) and turns its wheels from heights sampled 400 units ahead, behind and to each side with `CalculAltitudeObjet`, which reads the height map only. On the deck those samples read the lower road 2800 below, so the body nose-dived at the joint (77 degrees) and rolled up to 51 degrees along the deck, while the car itself stayed on the deck at the right height. No data change can fix that while a road runs under the deck.

The fix is in the engine that LBA Assembler's Play runs (`native/lba2-classic-community`). A helper, `BuggyFloorY`, gives those samples a decor's top by two rules:

- A decor top under the sample point counts if the engine itself would put the car on it: a top between the ground and the car's height plus the step it may climb.
- While the car is carried by a decor, a sample point with no decor under it (hanging over the deck's edge or end) that would read more than a step below the car reads the car's own height. The engine holds the car level on the deck for as long as its box touches it, so the body does not dip towards the ground far below.

With no decor near, nothing changes. Measured on the whole deck, pitch is at most 11 (1 degree) and roll 1, against 875 and 579 before. Positions are frame for frame the same as before, and plain ground is unchanged. Both native targets (`lba2cc`, `lba2_renderer`) are rebuilt with it.

This is part of the engine's race-track mode: without it (any game folder with no race track built) `BuggyFloorY` is the terrain as ever. Checked by driving onto the deck both ways with the same car: [level with the mode, tipping without it](racetrack/build/deck_race_mode.png).

## The retail track's area

The lap runs through the ground the retail Desert track used (cube 7,10, scene 57), and what was left of the retail track is removed.

- **The route.** Three routes were designed and checked independently: a gentle loop through the old infield, one tracing the retail layout, and one found with a cost-based router along the old road bed. The chosen one takes the retail track's own layout driven the other way round: the bottom lobe, the esses with the hairpin widened to a radius of about 9 cells, the bottom-left lobe, the sea-side rim, the top straight, and the old start straight as the exit. It reuses 82 % of the old road bed, its tightest turn in the cube is 9.2 cells, and it keeps 17 cells from every other part of the lap. The splices where it joins the rest of the lap were smoothed.
- **What is removed** (`RaceTrackBuilder.ClearOldTrack`, option `OldTrackCube`, a tick box in the menu):
  - the retail track's 8 decor pieces (bodies 64-71: start gantry, billboard, arch and abutments, wedge);
  - every triangle of its paint (asphalt, curbs, arrows, hatching, start line), 2967 in all, turned into the plain sand of its own infield before the new lap is painted over the same ground.

  The ground's shape is left alone, so where the new lap doesn't use the old road bed, it becomes sand between its rock banks.

## The ground

**The grade limit.** The first grade limiter moved pairs of neighbouring heights towards each other, 60 passes at most. On a hilly stretch such as the retail track's bed that is nowhere near enough, and it left 23 % climbs against the 9 % limit. `LimitGrade` now computes the answer directly: the average of the two envelopes of the profile limited to the grade. One is the highest profile that nowhere rises above it (cut only); the other is the lowest that nowhere drops below it (fill only). Both climb at most 9 %, so their average does too, and it splits the difference between cutting and filling everywhere. A 4-cell smoothing afterwards (`VerticalSmoothing`) rounds the crests and dips, which otherwise pitch the car all at once; smoothing never makes a profile steeper. The water bridge keeps its clearance by a fill-only pass.

**Distance to the road.** A point's distance from a road is its true distance to the nearest segment. It used to be the sideways offset alone, which reads about 0 wherever the projection stops at a segment's end. That caused two faults:

- Past the pit lane's open ends, it claimed a 13-cell circle round each end at the pit lane's height, which made a cliff across the lap.
- In tight bends, the search's second hit on the same road (a point 18 cells further round the bend) got full weight and pulled the surface up to 540 units out of shape.

**Where two roads overlap**, each road's shape is laid over the ground from the farthest to the nearest, so the nearest road wins. The pit lane takes its heights from the nearest point of the lap beside it, so where the two merge they are already at one height, and it hands the ground back to the lap over its last 8 cells.

**Two more rules** keep the shaping out of trouble:

- **The sea's edge.** A vertex beside the sea or at the island's own edge keeps its natural height unless it lies within a road's verge. Before this rule, the fill beside the new section raised the island's west rim in cube (7,10) from sea level to 4800, a slab ending in mid-air. Verge cells within 2 cells of the sea get the blocking bit, a sea wall: a car running wide there stops instead of dropping into the water.
- **Old rock walls.** The retail rock banks carry the blocking bit (Col) on their triangles, and where the shaping left one nearly flat just past the verge, it became an invisible wall in the sand. Every triangle the shaping moved (by 50 units or more at a corner) whose new slope is walkable (under 0.5) loses the bit: about 2000 triangles. A nearly flat rock one (under 0.35) whose neighbours are mostly sand or road (at least 10 of its 16 neighbouring triangles) is painted as sand: 119. Next to a real outcrop it stays rock, only passable, so the sand does not bite a sawtooth into the rock.

## The arrows

![arrows](racetrack/build/arrows_compare.png)

The first arrows were blobs: whole cells painted wherever a cell's centre fell inside a small arrow shape at the road's own angle. The retail arrows are drawn with the ground's own triangles, since each cell is two triangles cut along one diagonal or the other. A retail arrow is 17 orange triangles: a head 2.1 cells long and 4.2 wide on a shaft 1.4 wide, always pointing along a diagonal, so every edge of the arrow is a cell side or a cell diagonal and the triangles draw it exactly.

The track's arrows do the same. Each arrow is turned to the nearest of the eight directions the triangles can draw exactly (at most 5-8 degrees off the road) and placed where the road runs that way, with its tip on a cell corner. Each cell's cut is chosen to fit the arrow's edges (`RasteriseArrow`). The arrows come in two shapes:

- Along a row or column, an arrow is 6 cells long, with a head 2 long and 4 wide on a shaft 2 wide: 24 triangles.
- Along a diagonal, it has the retail head and shaft, 6.4 cells long.

An arrow that would touch anything but plain asphalt is moved along its straight, or left out with a note in the log. Two were left out, both near the road bridge.

**The start line** is one straight column of white cells across the road (`MarkStartLine`): where the road runs within 25 degrees of the cell grid's rows or columns, the line is the column through the cell the start point is in, and the gantry over it and the line laps are counted at are turned onto the grid with it. It used to be the cells within half a cell of the true line, which stepped sideways a cell where the road runs 5 degrees off the grid. The curb blocks either side of it are red, so a white one doesn't run into it. [From above](racetrack/build/start_line_top.png).

**Checked in the engine** (2026-09-28): every arrow of a bridge build (12) and of a jump build (15-16) was photographed standing on it in the game, and all are clean. The engine reads a cell's cut from its first triangle only (TERRAIN.CPP), as the builder and the map assume. A jump build in the "Level viewer" install had broken arrows in the game: its diagonal arrows came out as two arrows pointing opposite ways over each other, and it was just as broken drawn top-down with the engine's rule. It came from an app instance started before the final arrow code, while that code was still being changed: the same build from the current app and from the command line is byte-identical, and clean. Rebuilding from a freshly started app fixes it.

## Fixed cameras

Camera zones (type 1) switch the view to a fixed camera while Twinsen is inside the box. "Forced" ones re-aim it every frame and switch off the view's recentring, which is why the car could drive out of the picture.

With `RemoveTrackCameras` (a tick box), a camera zone is removed from scenes 55-73 if it is switched on when the scene loads and its box reaches within 8.5 cells of the road's centre line. That removes 16 zones:

| Scene | Zones removed |
|---|---|
| 61, the School of Magic plaza | 9 |
| 67, the start area | 2 |
| 57, over the old track's start straight | 2 |
| 60, 62 and 65 | 1 each |

Zones that start switched off are kept: only cutscene scripts switch them on (the ferry's arrival, calling the car), and they need them. Scripts and signs that switch a camera on look it up by number, and do nothing when it is gone. Camera shots of doors and buildings further from the road stay.

## The stand-in, and the actors that stay

Removing an actor leaves references to it in the scripts that stay. Mostly these are in Twinsen's own life script: "if Twinsen is near the shopkeeper and presses Action, talk, then send the shopkeeper's track to @45".

At first those references were pointed at Twinsen himself. In scene 67, every press of Action (which is also how he gets into the car) found him at distance 0 from "the shopkeeper", made him speak, and jumped his own track script to a foreign offset.

They now go to a stand-in actor added to each scene that needs one:

- It is invisible, with no body and no shadow, 20000 below the ground. The engine's `distance()` reads "far away" (32000) when two heights differ by 1500 or more.
- Its life and track scripts are a single `END`, and every jump the kept scripts make into its scripts goes to that `END`.
- A camera told to follow it follows Twinsen instead.

Some actors can't go. Twinsen's own script plays the cutscenes of arriving on and leaving the island: by ferry in the harbour (scenes 59, 60, 65) and by Dino-Fly (scenes 55, 73). It waits on those actors' tracks or points the camera at them. Without them, a normal game got stuck for good on the way to or from the island, and with the stand-in the screen went blank.

So the actors Twinsen's script waits on directly (`l_track_obj(n) == k`) or follows with the camera (`cam_follow(n)`) stay: 10 in five scenes (55: 3; 59: 2; 60: 13, 28, 30, 31; 65: 3, 4, 13; 73: 3). In a new game they are hidden until their cutscenes, except the boat moored in the harbour and a Grobo on its quay (scene 65, 10 cells from the road) and the turtle-calling bell (scene 55, 14 cells from the road). The arrival cutscenes were checked against the original files: Twinsen, the Dino-Fly, the ferry and the cameras end up exactly where they do in the original game.

## A longer jump (proposal)

![jump proposal](racetrack/build/jump_proposal.png)

Still a proposal. Its way of making the flight longer, a scaled copy of the retail flight given to Twinsen as an animation of its own, is now built for the jump crossing style (sized to the layout: x1.43 at this track's crossing; see "The jump"); the harbour leap would need a x1.3 copy of its own.

The proposal is the **harbour leap**, in place of the water bridge (scene 65, cube 9,8). The lap crosses 19 cells of open harbour water there, and a jump 1.3 times the retail one clears it:

| | Retail jump | Proposed jump |
|---|---|---|
| Length | 17.6 cells | 22.8 cells |
| Peak | +1921 | +2401 |
| Time | 1.78 s | 2.05 s |

On the centre line it leaves 1.7 cells of quay before the water and 0.8 cells of the far quay's top to spare. The retail length would come down in the sea.

It works exactly like the retail car jump (next section): a take-off strip, a controller actor, and a flight animation. The flight is a new `ANIM.HQR` entry made from entry 51, with the forward steps x1.3, the climb x1.25 and the timing x1.15. Twinsen gets it as a new animation number, so the retail jump in scene 62 stays as it is.

What it needs:

- **A take-off strip narrowed to the middle 3 cells**, with rock walls either side, or a wider far quay. The quays cross the flight line at a slant, so the car only lands on the far quay if it takes off within about 1.5 cells of the centre line.
- **The approach raised by 251.** The flight ends 251 below its start; with the approach raised, that lands on the far quay.
- **A way across for a miss.** The causeway goes, so a miss (on foot, or reversing) ends in the sea. A footbridge beside the flight line, or keeping the causeway and flying alongside it, is part of the proposal.

The alternative, B in the picture, is a dry "canyon" jump on the nearly straight stretch from cell (587, 653) to (610, 653) in the start cube, landing 275 lower, with a gap dug across the road under the flight. It is safer and less spectacular.

[tools/RaceTrackPlan/jumpsite.py](../tools/RaceTrackPlan/jumpsite.py) searched the rest of the lap for stretches straight enough and long enough for a 23-cell flight inside one cube. It leaves out the water bridge itself, the road bridge, the start and the pit ends. It found B, and one other stretch whose landing is 900 above its take-off, which the flight can't reach.

## How the retail jump works (scene 62 "near car jump"; still available as a crossing style)

There is no jumping in the engine's physics. The buggy follows the ground height map, and an object that falls (`FALLING`) only moves straight down. The retail jump is a **scripted flight**:

1. The **hero's own life script** (scene 62, actor 0) checks three things every frame: `IF COMPORTEMENT_HERO == 12` (driving the buggy), `IF ZONE == 1` (Twinsen is in scenario zone 1, the take-off), and `BETA` within about 45 degrees of the zone's direction. `ZONE == 2` with the opposite direction handles the return jump.
2. Then it does `SET_DIR(MOVE_BUGGY)` (movement 12: the object is moved by its animation and its track, not the keys) and `SET_TRACK(label 0)`, and switches to a waiting behaviour.
3. The hero's **track script** label 0 is `ANIM(67); WAIT_ANIM; ANIM(0)`, then `LABEL(1); STOP`. Generic animation 67 of Twinsen is `ANIM.HQR` entry 51: 18 keyframes over 1780 ms, with the "master" bit (no gravity) set on keyframes 1-17. Its root translation adds up to **8990 units forward** (17.6 cells), climbs **1921**, and ends 201 below the start.
4. When the track reaches label 1, the waiting behaviour does `SET_DIR(MOVE_BUGGY_MANUAL)` (13), and the player drives again.

Measured in the engine (headless, `tools/RaceTrackPlan/drive.ps1`): the flight runs along the buggy's heading from the point of entering the zone, 17.4-17.6 cells long, with Y up to about +1920. It lands wherever that is; if the ground there is higher, the car is lifted to it (the retail landing is on a plateau 265 higher, and comes out 7383 units long).

In the retail scene the two plateaus are 4400 to 5200 high with a canyon between them (heights down to 200). Zone 1 is a 4 x 3 cell box on the west plateau's edge, and zone 2 is the same on the east one. The buggy's own script (actor 5) sets game variable 168 while the buggy is in zones 2-5, so the tour garage (scene 57) takes the buggy back when you leave it there.

## The jump (2026-09-28)

![the jump from above](racetrack/build/jump_top.png)

The jump crossing style, like the retail car jump, is a scripted flight (previous section); what is new is the road around it. Before, the road ran on through the crossing as a level junction and the car flew over it, so from the ground the track still crossed itself. Now the jumping road leaves the ground (numbers from the current build, the other road squared up to 90 degrees):

| Along the road, from the crossing | What is there |
|---|---|
| -15.4 to -7.4 cells | **the up ramp**: 800 above the road at its top, 8 cells long, a straight climb with a rounded foot, asphalt and curbs with rock sides; its last cell of flat top is painted with the red/gold hatching |
| -7.4 to -4.4 | **a gap**: sand, level with the other road |
| -4.4 to +4.7 | **the other road** (its curbs, 9 cells across; the two sides are measured separately, since the road bends a little) |
| +4.7 to +7.7 | **a gap** |
| +7.7 to +19.7 | **the down ramp**: 372 above the road at its top (hatched), down to the road over 12 cells |

The very end of each ramp is a face of blocking rock. Each gap is 3 cells (`JumpGap`) past where the jumping road's line actually leaves the other road's curbs, measured on the built road: the other road bends through the crossing, and a gap worked out from the crossing angle alone came out a cell wide on one side. The whole jump sits on a level stretch at the crossing's height, which EqualiseCrossings gave both roads, blended into the road's own profile over the next 15 cells. That matters because the flight knows nothing of the ground: it ends a fixed distance on and a fixed height down from where it starts, so the down ramp must be where the flight ends. `RaceTrackBuilder.PlanJump` does the layout; the ramps get the bridges' narrow rock-sided shoulders (`TrackRoad.Bridge`), the gap is sand (`TrackRoad.Gap`), and no arrow is put on any of it (`TrackRoad.Jump`).

**The crossing angle.** A jump clears the other road more easily the steeper it crosses it, but the crossing is re-shaped: the straighter road is turned and made straight over the whole jump, then bent back to the drawn course. At 64 degrees the stretch beyond the jump ran into the next part of the lap, 4.3 cells between centre lines (two 9-cell roads overlapping), which read as a second crossing. So `JumpAngle` tries 64 degrees down to 36, 2 at a time, and takes the steepest whose re-shaped stretch (the points that moved off the drawn road) keeps `JumpClearance` (12.5 cells) from every other part of the lap. The other road's own arm near the crossing is left out, being the one crossing there should be. On the Desert track that is 42 degrees (41 as built), keeping 13.2 cells, as the bridge build does there. A lap where no angle manages it gets the angle that keeps the most, and a `WARNING` in the log.

**The other road squared up.** At 41 degrees the other road met the up ramp at a slant, and its surface ran into the ramp's right side: 3.1 cells of overlap, where its ground and paint won over the ramp's curb, so the ramp had no red/white curb on that side. `SquareOtherRoad` now turns the other road to meet the jump at 90 degrees over a short stretch: straight for 8 cells either side of the crossing, then back on its own course over 32 cells. It tries 90 degrees down to 60 with blends of 32, 28 and 24 cells, and takes the first that still crosses the lap once, keeps `JumpClearance` from the rest of the lap, and bends no tighter than 6 cells (10.8 here). Its surface now stays 3 cells clear of both ramps, which come out the same either side. Two smaller changes back this up: the ground under a jump ramp's top is shaped after the verges and embankments and before the road surfaces (`ModifyGround`), and a ramp's rock sides win over another road's sand verge in the paint.

![the up ramp's curbs, both sides](racetrack/build/jump_ramp_curbs.png)

**A longer flight.** The retail flight (17.6 cells) would land in the gap. `Terrain/RaceTrackJumpAnim.cs` adds a copy of ANIM.HQR entry 51 as a new entry with its steps scaled, as long as the layout needs: from the take-off strip, over both gaps and the other road, to `JumpLandInto` (3.5) cells down the far ramp. Squared up, that is x1.21 forward, x1.18 up and x1.11 the time: 21.2 cells in about 2 s. It is added after the island is built, since the layout decides its length. It gives the new entry to Twinsen's buggy entity (RESS.HQR entry 44, entity 12: the engine loads entity n for behaviour n, and driving is behaviour 12) as generic animation 200, the one record added to that entity (since 2026-09-30 the island's own number, 202 on the Desert island: see "Several tracks in one game folder"); the retail jump in scene 62 keeps entry 51. The hero's track script plays `anim(200)`.

**Where the flight starts.** The take-off strip (scenario zones numbered 40) starts 2.5 cells before the lip and is 1.25 cells deep, as wide as the curbs. The flight starts where the car enters it, whatever the speed: at full speed the car moves a fifth of a cell a frame. So the flight starts at a known place, 5.5 cells up the ramp, and ends 3.6 cells down the far ramp, 30 units above its surface: the down ramp's height (372) is worked out from that. The strip started 1.5 cells before the lip at first. The jumping road runs diagonally across the ground's grid, so the lip's rock face is a staircase of blocking cells, and a car 3 cells off the centre line hit a step of it before it reached the strip, and stopped.

**Verified in the engine** (headless, `tools/RaceTrackPlan/jumpdrive.py`):
- Driving at full speed from 20 cells before the take-off, on the centre line, the car climbs the ramp and takes off at the strip. It flies over the gaps and the other road, lands on the down ramp, and drives on down it.
- Three and four cells either side of the centre line, the same.
- Both ramps have their red/white curbs along both edges up to the lip (the picture above, and cell by cell along both edges).
- [In the air](racetrack/build/h_jump.png); [from the other road](racetrack/build/h_jump_gap.png) (an earlier build), with the gap and the down ramp's hatched top.

### How the scene runs it

`Terrain/RaceTrackScenes.cs` (`AddJump`) adds a small controller actor instead of editing each scene's long hero script:

- **The controller** (entity 16, invisible, no body) goes in the scene the take-off lies in (scene 66). It makes the retail script's three checks (`zone_obj(0)`, `beta_obj(0)` within 56 degrees of the road, `comportement_hero`), then does `set_dir_obj(0, 12)` and `set_track_obj(0, label_90)`. It gives the keys back when label 91 is reached.
- **The hero's track script** gets `label(90); beta(<heading>); anim(200); wait_anim(); anim(0); label(91); stop();`. The `beta` sets the road's heading first, so the flight goes along the road whatever the car's heading was within the window.
- **The take-off strip** is added at the end of the zone list, so it wins where zones overlap.
- **The crossing**: `RaceTrackBuilder.SteepenCrossing` turns the road to the chosen angle and makes it straight over the whole jump (about 25 cells either side), `SquareOtherRoad` squares the other road up to it, and `PlanJump` lays it out.

## The engine's race-track mode (2026-09-28)

The engine changes a race track needs are in one place, `native/lba2-classic-community/SOURCES/RACEMOD.CPP`, and are on only when the environment names a car setup file (`LBA2_RACETRACK_FILE`). LBA Assembler's Play sets it only when the LBA2 folder it plays has a race track built (`RaceTrackService.HasBackups`). Every other game plays exactly as before: the code paths are the original ones.

With it on:
- **The car stays level on the bridge deck** (BUGGY.CPP `BuggyFloorY`, "The car on the deck"), and the parked car doesn't stay on screen as a ghost when Twinsen gets in (`TakeBuggy`).
- **The car setup**, from the file, replaces the buggy's fixed rates (BUGGY.CPP `RaceSpeed`):
  - a **gearbox**: X shifts up and Z down, on the key's press. Each gear has its own top speed; it can also be automatic (up at a gear's top speed, down below 70 % of the gear below's);
  - the **acceleration**, scaled to the gear as a gearbox does it. The pull goes with the gear ratio and the top speed against it, so a gear whose top speed is the original car's pulls like the original car, and a lower one harder (at most 4 times);
  - above a gear's top speed, just after a shift down, the car slows as it does off the throttle;
  - the **braking**, the **rolling to a stop**, the **top speed backwards** and the **steering**;
  - the engine's sound revs up through each gear.
- **A display** in the top right corner, in the game's font, while Twinsen drives: the gear, the speed, the lap and its time, the checkpoints crossed, the last and best laps, the race position and a message line. Speeds are in km/h with a cell taken as a metre, so the original car's top speed is 27 km/h.
- **Laps are counted** at the start line (from RACETRACK.JSON: its ends in its cube's coordinates, across the lap and the pit lane, and the way a lap crosses it). Crossing it the right way starts lap 1. The timer is the game's own clock (`TimerRefHR`).
- **Checkpoints.** A lap used to count at every forward crossing of the line, so a car could reverse over it and cross it again. Now there are 8 checkpoints round the lap: lines across the road (and its verges), each within one cube, put by `RaceTrackBuilder.PlaceCheckpoints` evenly along the lap, away from the pit lane, the crossing and the cube edges. A lap counts when the car has crossed them all in order and then the start line; the display shows the last lap's time and the best. Crossing the start line short of them shows "Missed checkpoint N: no lap", and the lap's time runs on. Crossing the last one backwards takes it off again and shows "Wrong way".
- **An opponent.** The retail Desert track's racer (scene 57's actor 4: entity 157, body 227, the car with its driver) is copied into every outside scene of the island. The copies are hidden below the ground, except the one on the grid beside the buggy in scene 67. The engine drives it along a racing line the builder plans with the track, at speeds planned from the player's own car (see "The opponents' driving"). Over the jump it follows the flight's own arc. It sets off when the player first drives off, and waits while Twinsen is out of the car. Each frame it is placed, facing along its line and running its driving animation, in whichever scene is showing (`RaceMod_Frame`, at the start of the frame's drawing). The display shows the race position: how far round each car is. While it moves, the whole scene is redrawn each frame. Without that, the car, drawn into the engine's background copy while it waited on the grid, stayed there as a picture once it set off.
- **Play starts the race.** With "Start beside the car on the start/finish straight" in the car setup (the default), Play on a folder with a race track built starts scene 67 whatever scene is open (`RACETRACK.JSON`'s `StartScene`). Twinsen stands beside his buggy on pole, with the opponents on the grid behind him (see "The grid, qualifying and the count-down"), and the zones and routes the editor draws over the game are hidden; changing those checkboxes while playing shows them again. The build removes the outside scenes' other actors already. The scene's hero start keeps no facing, and Twinsen comes in facing the lap's way, so he stands 1.8 cells behind the car, where Action gets him straight in.

- **Two opponents.** Baldino races too, in his rocket car, on a line of his own (see "Baldino's rocket car"). The engine drives up to four, each with its own line, grid spot, skill, character and cars; the display's position counts them all ("Position 2/3").

The file is `key=value` lines: `gears`, `gear1`..`gear8` (top speeds, world units a second), `accel`, `brake`, `coast` (speed gained or lost per millisecond; the original car's are 4, 12 and 7), `reverse`, `steer` (4096ths of a turn a second; the original 1024), `automatic`, `hud`, `startline=<cube x> <cube z> <x0> <z0> <x1> <z1> <dir x> <dir z>`, a `checkpoint=` line in the same form for each checkpoint in order, and for the opponent `opponent_path=<file>` (lines of `x z y speed radius`, island world units, 32768 a cube: with the bend radius the speeds are planned from the car, without it the file's are driven), `opponent_grid=<the line's point it starts at>`, `opponent_pace=<percent>` (its skill), `opponent_top=` and `opponent_grip=<percent>` (its character), `opponent_catchup=<percent>` `opponent_name=`, and an `opponent_actor=<scene> <actor>` line for each scene's copy; the second opponent's lines are `opponent2_path` and so on, up to `opponent4`. The grid is `grid=<cube x> <cube z> <x> <y> <z> <turn>` lines, pole first, `qualifying=1` (or 0), `qualifying_seed=<n>` (optional: the opponents' qualifying times' randomness, else the clock) and `hide_actor=<scene> <actor>` for the cars of opponents the player left out. Anything missing keeps the original car's value; with no `checkpoint=` lines every crossing of the line counts, and with no opponent lines there is no opponent. X is otherwise the dodge key, which does nothing while driving.

**The race car setup** (`RaceCarWindow.cs`, `RaceCarSetup.cs`, kept in the settings): presets (the original buggy, a 5-gear race car, a fast 6-gear one), the number of gears and each one's top speed in km/h, the acceleration, braking, rolling to a stop and steering as percentages of the original car, the opponents (each on or off, with its skill, 50-120 %, and whether they push harder when they fall behind), and the race start. It appears when Play starts on a folder with a race track (it can be switched off there; it doesn't appear when the game is only restarted), and from the race track dialog's "Race car setup…" button. Play writes it for the engine as `racecar.txt` in its user folder, with the opponents' lines beside it as `racepath.txt` and `racepath2.txt` (`Terrain/RaceCarEngineFile.cs`). Since 2026-10-17, "Use each track's best setup for its top speed, acceleration and steering" (Car tab, off unless ticked) drives each track with the setup the stress test found fastest there (`RaceCarSetup.TrackSetups`, from [the stress test](LBA2_RACE_TRACK_STRESS_TEST.md)) in place of those three sliders, which keep their values, greyed, for a track with none and for when it is off; the window says which setup applies.

![car setup](racetrack/build/car_setup.png)

**Verified**:
- Headless, with no car file, the car tops out at 3800 as ever.
- With a 3-gear car (1500/2500/3500), first gear holds at 1500, and each X press lifts the top speed to the next gear's.
- The display shows the gear and the speed. The first crossing of the start line starts lap 1; the car taken back and across again shows lap 2, and the last and best times.
- In the app: Play on the built folder showed the car setup, then the game with the display; the car file had the start line from RACETRACK.JSON. Play on the same folder restored showed neither.
- Checkpoints (headless, a test file with two checkpoints in the start cube): crossing one counts it (1/2); reversing over it shows "Wrong way" and takes it off (0/2); recrossing the start line then shows "Missed checkpoint 1: no lap" and lap 1's time runs on. With both crossed in order, the next crossing counts lap 2, with the last and best times. The built lap's 8 checkpoints are crossed once each, in order and the lap's way, by the opponent's line (both styles).
- The opponent (headless, jump and bridge builds): on the grid beside the buggy, it sets off with the player and runs ahead, and nothing is left behind on the grid. On the bridge build its line runs over the deck at the deck's height and under it on the lower road.
- In the app (2026-09-28): with scene 61 open, Play showed the car setup, then started scene 67 with Twinsen beside his car, the opponent on the grid and no zones or routes drawn. The car file it wrote matched the one tested headless.

![checkpoints](racetrack/build/checkpoints.png)

![race start in the app](racetrack/build/race_start_app.png)

## The grid, qualifying and the count-down (2026-09-28)

![qualifying, the grid and the count-down](racetrack/build/qualifying_grid.png)

**The grid** (`RaceTrackScenes.GridSpot`) has five spots in scene 67, staggered either side of the middle of the road:
- Pole is 3 cells behind the start line; each spot is 3.5 cells behind the one before it and 1.75 cells to its side.
- Two cars side by side are 3.5 cells apart across the road (the cars are 2.6 cells wide, so 0.9 cells between them), and 7 cells apart on the same side. Before, the racer's car stood touching the player's.
- The build puts Twinsen's buggy on pole, with Twinsen 1.8 cells behind it (where Action gets him in; the next car is on the other side, clear of him), the racer on the second spot and Baldino on the third.
- The spots are written to RACETRACK.JSON (`Grid`) and passed to the engine.

**The pits** (`RaceTrackBuilder.PlacePits`) are where the opponents' cars wait while Twinsen qualifies: five spots in the pit lane, the first 2 cells before the start line and each 4 cells further back, 10.5 cells to the side of the lap (the road's curb is 4.5 from its middle, so they stand well clear of it) and facing the way a car leaves the pits. On the grid they stood across the start line, which a qualifying lap has to cross. The build parks the cars there, so they are out of the way in the retail engine too; the spots go to RACETRACK.JSON (`Pits`) and the car file (`pit=`). The pit lane is its own road whose points may run either way round the lap, so a spot is found by where it lies along the lap, not by counting points along the lane.

**Qualifying** (`RACEMOD.CPP`, the car setup's "Drive a qualifying lap first", on by default):
1. While Twinsen qualifies, the opponents wait in the pits. The display says "Qualifying: cross the start line", then "Qualifying lap" with its time and the checkpoints.
2. His first complete lap (every checkpoint, in order) is his qualifying time.
3. Each opponent's time is its lap as the engine planned it at its skill, plus or minus up to 3 s at random.
4. Everyone is sorted by time and put on the grid, pole first: the opponents move from the pits onto their spots. Twinsen's car is moved to its spot (only ever in scene 67, where his lap has just ended), standing, and the camera follows it. The display lists the grid ("1 Baldino 2:25.97 ...") for 4 s.

Without qualifying, the grid is formed as soon as Twinsen is in his car, with him on pole and the opponents behind in order of their times.

**The count-down** follows: a big 3, 2 and 1 in red, a second each, then a green GO!, drawn in blocks in the middle of the screen (`BigText`). Twinsen's car can't move until GO (`RaceMod_Held`, BUGGY.CPP `RaceSpeed`). At GO the laps start afresh from the start line, and the opponents pull away from a standstill:
- An opponent's speed rises no faster than the car's pull through its gears allows, up to its line's speed.
- It moves from its grid spot onto its racing line over its first 12 cells. The lines are no longer held at the grid.

**Verified** (headless, the jump build):
- **Without qualifying:** the grid formed with Twinsen on pole. The throttle was held from 2.8 s, but his car stayed on its spot until GO at 6.1 s. The count-down showed and all three cars set off at GO.
- **Qualifying:** a test car file puts two checkpoints just past the start line, the second crossed backwards, so a lap can be completed without steering. Twinsen's 155.39 s lap put him third behind Baldino's 145.97 s and the racer's 148.05 s. His car was moved to the third spot, the grid was listed, then 3, 2, 1, GO.
- **The pits:** during qualifying the start/finish straight is clear, and a state dump puts both opponents exactly on the first two pit spots (0.9 and 4.9 cells before the line, 12 cells to its side). When the grid forms they move onto grid spots 2 and 3, to the centimetre. In the app, Play shows Twinsen alone on the straight with the two cars waiting in the pit lane.
- **In the app:** the menu build is byte-identical to the command-line build. Play started on the staggered grid, and the car file had the grid, qualifying and the opponents' names.

![the count-down](racetrack/build/countdown.png)

## The opponents' driving (2026-09-28)

![the racing lines](racetrack/build/racing_lines.png)

The racer's line (red above) and Baldino's (blue), at the north-west hairpin, the two bends at (625, 643) and (653, 480), the jump and the start.

**The racing line** (`RaceTrackBuilder.PlanRacePath`) has a point a cell apart round the lap. Each point may sit up to 3.2 cells either side of the middle of the road (the car's middle: its wheels then just reach the curb). On the inside of a bend it stays far enough from the bend's centre to keep a radius of 1.2 cells at least: the lap's tightest bends are 2.6 cells round the middle of the road, and a line cutting further in folded over itself there. The line is found in two steps:

1. **Smoothing.** Each point is moved to the middle of its neighbours and then back within the road, over windows from 30 cells down to 2, 100 rounds each. A long bend's line moves as a whole. Point-by-point curvature sweeps, tried first, only ironed out small wiggles in 6000 rounds; they left the line on the middle of the road.
2. **Local search.** A search on the lap time itself: points every 4 cells are moved in and out by 1, 0.5, 0.25 and 0.1 cells, and a move is kept when the lap is quicker.

Each opponent leans a little to its own side, so the two lines don't lie on each other on the straights. Its grid spot and the start line are held at its side, so it starts where the scene puts its car. On the Desert track, with the race car driven perfectly, the line takes 134.4 s against 145.0 s on the middle of the road; the old line took 163.2 s.

**The speeds** are planned by the engine when the race starts (`RACEMOD.CPP` `PlanSpeeds`), from the player's own car as the car setup makes it:
- **Bends:** the buggy turns at a fixed rate whatever its speed (its steering, `SpeedRot` a second, BUGGY.CPP), so a bend of radius R is taken at up to that rate times R. The path file carries each point's bend radius (the circle through the points three cells either side).
- **Straights:** no faster than the car's top gear.
- **Braking and accelerating:** braking before bends as hard as the car's brakes, pulling away after them as hard as its gears (the pull of the gear an automatic gearbox would be in), two rounds each way round the loop.
- **Skill:** the speeds are driven at the opponent's skill, the car setup's slider. At 100 % it drives the player's car perfectly on its line, faster than anyone can. The defaults are 92 % for the racer and 91 % for Baldino: laps of 146.1 s and 145.0 s. A player driving the middle of the road perfectly takes 145 s.
- **Character:** Baldino drives with 102 % of the car's top speed and pull but 92 % of its cornering. His perfect lap is 132.0 s, the racer's 134.4 s.
- **Catch-up:** an opponent that has fallen behind the player pushes harder, up to 8 % more at 60 cells behind. It never slows down when ahead. This is a car setup checkbox, on by default.

Because the speeds come from the player's car, a faster car setup (the 6-gear preset, say) makes the opponents faster too.

**Verified:**
- **Headless:** the engine planned the perfect laps above. With Twinsen sitting in his car at the start, the opponents lapped in 146.09 s and 145.01 s, lap after lap. The engine logs each opponent's lap (`[racemod] opponent 1: lap 1 in 146.09 s`).
- **The lines:** they stay within the road everywhere; in the picture above, they use the curbs round every bend. On the bridge build they run over the deck at its height and under it on the lower road.
- **In the app:** the menu build is byte-identical to the command-line build. Play wrote the skills, characters and catch-up into the car file, and the engine planned from them.

## Baldino's rocket car (2026-09-28)

![Baldino's car in the game](racetrack/build/baldino_car_game.png)

A second opponent: Jerome Baldino, the Desert island's inventor, in a car made after his rocket ship (`Terrain/RaceTrackBaldinoCar.cs`). It isn't a copy of the ship but an inventor's version of it, about the size of the game's own cars (2.6 cells long, 2.3 wide):

- **The hull** is a stubby ribbed barrel, the ship's planked fuselage in alternating brown and taupe planks, with navy go-faster stripes down both sides.
- **The cockpit is open**, so his face shows: an oval hole with a brass rim, a low teal windscreen, and a red steering wheel he holds out in front of him (his elephant's head reaches forward, so the wheel is below his chin).
- **At the back**, a grey rocket nozzle with an orange flame (drawn unlit, so it glows), the ship's swept grey wings with red lights at their tips (its red discs), and a tail fin with another.
- **The gadgets**: odd wheels (big at the back, small at the front) with red hub caps, a coil-spring aerial with a red ball, a clockwork key in the side, a propeller on the nose and two yellow headlights.
- **Baldino himself** comes from his own body (BODY.HQR entry 139, "Jerome Baldino"): his torso, head with sunglasses, ears and arms. His hips and legs are left out (they are in the car), he is made a little smaller (0.8) and his arms are posed on the wheel (a two-bone reach that keeps his arm lengths). His trunk is raised forward and to one side, as if trumpeting: hanging, it went through the dashboard, and straight up it hid his face.

**How it moves.** The car is a second body of the retail racer's entity (157: its own car is body 0, BODY.HQR 227; this one is body 1), so the racer's animations drive it. It has the same 18 bones for the same parts:
- 0-1: the root and the bounce;
- 2: the hull;
- 3-8: the front struts, hubs and wheels;
- 9-12: the rear axles and wheels;
- 13: the driver.

The racer's driving animation bounces the car, tilts it a degree, wobbles the wheels and leans the driver. It also swings the racer's arms (bones 14-17) a long way onto his wheel; Baldino's arms are drawn on his wheel already, in bone 13, and 14-17 are empty.

**What the build does** (`RaceTrackService.Finish`):
- It adds the body to the end of BODY.HQR (entry 469 of the original file: 415 polygons, 11 lines, 16 spheres, 443 points, inside the engine's 550).
- It adds a body record, `1, 1, 4, <entry>, 0`, to entity 157 in RESS.HQR entry 44 (`RaceTrackBaldinoCar.WithBody`, as the jump's `WithAnim` does for its animation).
- It puts a copy of the racer's actor with body 1 in every outside scene, hidden below the ground, except the one on the grid in scene 67 (the third spot until the qualifying decides).
- It writes his line to RACETRACK.JSON (`Rivals`).

BODY.HQR is kept as `BODY.HQR.before-racetrack` with the others. The body writer (`Body.Write`) now keeps a face whose `Material` is 0 flat and unlit in a lit body (the flame, the cockpit's dark hole). Every retail body still reads back after a write (`BodyPipeline bodyroundtrip`).

**His line** (`RaceTrackBuilder.PlanRacePath` with `BaldinoLine`) keeps more to the other side of the road from the racer's. His rocket car (his character) drives with 102 % of the player's car's top speed and pull but 92 % of its cornering. Over the jump it follows the flight like the racer's. The car setup has "Race Baldino too" and his own skill.

**Verified**:
- In the engine (headless): the car on the grid, and seen from the front, three-quarters, the side and the back (the scene's copy turned with `ScriptRoundTrip actorbeta`, played without the race-track mode, which turns the cars along their lines every frame). With the race on, both opponents set off with the player and the display counts three racers.
- His line crosses the 8 checkpoints once each, in order and the lap's way, on the jump and the bridge builds. On the bridge build it runs over the deck at its height and under it on the lower road.
- The menu build (jump) is byte-identical to the command-line build, BODY.HQR included. Play from the app started the race with both cars on the grid, and the car file it wrote had both opponents.

![Baldino's car, Body Studio's renderer](racetrack/build/baldino_car_preview.png)

![racing Baldino](racetrack/build/baldino_race.png)

`ScriptRoundTrip baldinocar <game> <scratch>` builds the car into copies of a folder's BODY.HQR and RESS.HQR; `BodyPipeline object 2 <scratch>\BODY.HQR 469 <turn> <png>` draws it. `BodyPipeline bodyinfo <file.hqr> <entry>` lists a body's bones, `animdump <file.hqr> <entry>` an animation's keyframes, `palettesheet 2 <png>` the body palette.

## Citadel Island's town circuit (2026-09-29)

![the plan and the track built from it](racetrack/build/citadel_plan_vs_built.png)

A second track, from the user's own drawing over a top-down picture of Citadel Island: a 1,046-cell town circuit through the harbour, the citadel's streets and the cliffs, with a pit lane down the west side and a bridge where the lap crosses itself. Everything the Desert track learned is reused: the same road, curbs, arrows and hatching, the same grid, qualifying, count-down, opponents and racing lines.

**The route came off the picture.** The picture is the island's own map turned a quarter turn clockwise and squashed, so the drawing was fitted to a fresh render of `CITADEL.ILE` rather than measured by hand:
- the land of both was masked (the sea, and the drawn lines) and the best scale and offset found by cross-correlation: 0.805 across and 0.595 down, an island cell being 4 pixels of the render. The outline it gives follows the coast in the drawing exactly.
- The lime route was skeletonised and walked with a "snake": a cursor that steps a few pixels along and snaps back to the line. A plain walk of the skeleton turned back on itself where the route crosses, and closed a loop covering only part of the track; the snake carries straight on through a crossing. It closed on the whole 1,071-cell lap.
- The pink bridge bar is drawn over the lime and cuts it, so the route mask is the lime and the pink together.
- The orange pit lane's two ends and the bridge's middle came from their own masks.
`docs/racetrack/citadel_track_plan.json` is the result (the plan format the Desert track uses: points in island cells, and the pit lane's ends). `ScriptRoundTrip planprobe CITADEL <plan>` checked every point is on land before anything was built.

**What differs between islands** is now a record, `Terrain/RaceTrackIsland.cs`: the ground and decor files, the island byte, its outside scenes, its palette, the plan built into the program, and whether there is a retail race track to clear. The dialog has an island to choose, and the command line takes `RT_ISLAND`.

- **The road's look** (`Terrain/RaceTrackTextures.cs`). The Desert track's asphalt, hatching, rock and white curb are tiles of the *Desert island's* own 256 x 256 texture page, and its red curb and arrows are colours of its own palette; on Citadel those coordinates draw whatever happens to be there. The build now copies those four tiles into the target island's spare texture space -- the 8 x 8 blocks no cube's polygon reads, 14 % of Citadel's page -- and remaps every pixel to the nearest colour of that island's palette. The flat colours are matched the same way, keeping each colour's position in its ramp so the engine's light still lands on it (red curb 69, arrows 101 on Citadel).
- **The buggy.** Citadel has none: the buggy is one object the game puts on the Desert island. The build copies the buggy actor out of Desert scene 67 into the start scene, with the same script, and takes the car quest out of it so it is there from the start of any game (a copy added after that patch had already run kept the quest test and removed itself, which is why Twinsen first stood on the grid with no car).
- **One bridge over two crossings.** The drawing has the lap cross itself twice a few cells apart, with one pink bridge over both: a loop that dips under the same stretch of road twice. The bridge planner used to take the first crossing and give the deck to whichever road was straighter there, which picked a different road at each crossing and carried neither. It now takes the crossings within a deck's length of each other as one span, chooses the side of the first crossing that the others also lie on, centres the deck between them, makes it long enough for all of them and high enough over the highest road it passes above (73 cells of deck here, against 68 for the Desert track's one crossing).
- **Read-only files.** A game folder taken off a disc (or from a reference set kept read-only) has read-only files, and File.Copy carries that to the copy, so the build failed on its own backup; every copy the build makes is now cleared.

**The track**: 1,046 cells, 19,033 ground points levelled, 12,481 cells painted, 96 props and 85 solid decors taken off the road, 10 arrows, a 98-cell pit lane, 8 checkpoints, a grid of 5 and the pits beside the start line. With the race car driven perfectly a lap is 102.6 s (the racer's line) against 111.8 s round the middle of the road -- a shorter, twistier circuit than the Desert track's 134 s.

**Verified**:
- The menu build is byte-identical to the command-line build (all seven files), and only Citadel's own files are kept and changed: the Desert island's are untouched.
- The Desert track, built again with all of this in place, is byte-identical to its last build (`DESERT.ILE`, `SCENE.HQR`, `RACETRACK.JSON`), for the jump and the bridge alike.
- In the engine (headless): the start scene (49, near the Dino-Fly) puts Twinsen beside his buggy on the grid, Action gets him in, the display says "Qualifying: cross the start line", he crosses it and the lap times run with the checkpoints. The opponents wait in the pit lane.
- In the app: Play starts scene 49 with the car and the opponents in the pits.
- Citadel's own rain and gloom make the track look dark: a screenshot of the untouched scene 49 is darker still (mean brightness 43 against 59), so that is the island, not the build.

### Second round: the invisible walls, the leftovers, the bridge and the rain (2026-09-29)

The first Citadel build could not be driven round. Five separate faults, four of them Desert assumptions that only showed on a second island:

**The invisible walls were the cube edges.** An island's outside is one scene per cube, and the engine holds the hero at a cube's edge (`EXTFUNC.CPP`: the position is clamped and `FlagHeroOut` set) unless a cube-change zone (type 0) to the next cube's scene covers that spot, at his height, on that edge (`GereZoneChangeCube`; the zone's arrival value, 512 or 32768 - 1024, names the edge). The build removes door zones that lie on the road, and it told a door from an edge crossing by the scene it leads to -- hard-coded as the Desert's outside scenes, 55-73. On Citadel every edge crossing leads to 42-50, so all of them were removed: 29 zones, a wall at every cube edge. Now the island's own range decides (`RaceTrackIsland.FirstScene/LastScene`), and only the 9 real doors go (into the sewer, Tralu, the ticket office...).

That alone was not enough: the retail edge zones only span the edges where the retail paths cross them (scene 42's south edge into 49 has none for 14 cells, where a cliff was). So the build now finds every place the lap crosses a cube edge (`RaceTrackScenes.EdgeCrossings`) and, both ways, adds a crossing zone of its own wherever the island's don't cover the whole road's width at the road's height (`CoverEdges`: the last cell before the edge, 16 cells of it, the position and height carried over like the retail ones). Citadel's lap crosses 14 edges and needed 9 zones; the Desert's crosses 26 and needed 14 -- it too had stretches of edge its racing line crossed fine but the road's full width didn't.

**The remains of a building were the start gantry and some protected bodies.** Body numbers are an island's own. The gantry is placed as bodies 64-66 (the Desert track's beam and posts) and the viaduct's arch as 68-70; on Citadel 64 and 65 are a building's stone wall and pillar, so the "gantry" stood at the start line as a wall and a pillar. And the retail track's bodies (64-71) were protected from removal -- on every island, so Citadel's walls and pillars on the road stayed. Now the protection only applies on the island that has the retail track, and for any other island the build copies the Desert's gantry and arch bodies into its own OBL (`RaceTrackService.CopyRetailBodies`, as they are: the palettes put the same colours at those indices) and places the copies (`RaceTrackOptions.RetailBodies`).

A third kind of leftover: a building is several decor pieces placed apart, and clearing the pieces on the road left the rest standing -- a wall on its own by the kerb, a roof beam in mid-air whose supports had gone. `ClearRuins` treats pieces whose boxes touch as one structure, and where one lost pieces to the road and what is left is all small parts (no whole building among them), those go too, within 14 cells of the road. It took 6 pieces on Citadel and 3 on the Desert (two lone fence posts and a small low piece).

**The bridge didn't meet the road.** The deck is straight, so the road it carries is straightened first (`SteepenCrossing`). With one deck over two crossings, the straightening still picked its road by the old one-crossing rule (the straighter one) -- the other road -- while the deck went on the stretch the two crossings share: the deck sat on a road that had never been straightened. Now a multi-crossing span straightens the road it carries, through all its crossings, along that road's own heading (`SpanOf` gives the span's middle and length to both steps). The build reports how far the road's middle strays from the deck's: 0.0 cells along the whole deck, where anything over 1.5 is a warning.

**The road ran off the island.** Driving over the bridge, the car went through scene 94, the engine's open sea: the deck's far end reached the corner of cube (7,8), which Citadel hasn't got. The drawn route itself passes 2 cells from that corner, and the road is 6 cells wide each side. `KeepOnIsland` now moves the lap's middle smoothly away from any missing cube until its verge clears it by half a cell (on Citadel from 2.2 to 6.8 cells; the Desert's lap never comes near one), and the bridge's straight is moved sideways, staying straight, until all of it clears too (6 cells here, so the deck stands 6 cells east of the drawing's pink bar).

**The rain.** The storm is the engine's `TEMPETE_ACTIVE` (COMMON.H): chapter below 2, which is how the story stands until the lighthouse keeper is freed and the aliens land. Once it is over the engine does more than stop the rain -- it loads Citadel Island from a different file, `CITABAU.ILE` ("citadelle beau"), with its own light, palette (RESS 42 against 27) and decor bodies (`CITABAU.OBL`), and a clear sky. So:
- the track is built into both files (`RaceTrackIsland.TwinIleFile/TwinOblFile`, `RaceTrackService.BuildTwin`): the same ground, so the same road, with the twin's own texture import, decor clearing, gantry and deck bodies. Without it the track would vanish in a real game once the lighthouse quest is done.
- the race car setup has "Stop the rain on Citadel Island" (on by default), which writes `weather=fine` to the car file; the engine's storm macros then read `RaceMod_FineWeather()` as well as the chapter. Only the weather changes, not the story: the scripts that test the chapter or the lighthouse quest (variable 56 turns Twinsen back at scene 49's edge in one of its states) are left as they are.

![the start in the storm and in fine weather](racetrack/build/citadel_weather.png)

**Verified**:
- Every place the lap crosses a cube edge, both ways, walked in the engine (a script puts Twinsen a few cells before the edge on the racing line and walks him over): all 28 Citadel crossings and all 48 Desert ones end in the right scene. (Loading scene 48 directly starts Twinsen inside its scenario zone 7, which plays its scripted arrival from the sewer grid and holds him, so its departures were walked after arriving from scene 47, as a car does.)
- The bridge in the engine: the car started on the straight before the deck and driven straight across held the deck's height (3501) the whole length, within 0.3 cells of its middle, and went on into scene 48 without passing through the open sea.
- `weather=fine`: the engine loads the fine-weather island (mean brightness at the start 99 against 64), no rain, the sky and sea clear, the track, gantry and grid all there.
- The fine-weather file built from the same plan has the same lap, start line, pits and checkpoints; the heights of the two files are identical.
- The menu build is byte-identical to the command-line build (all nine files), and keeps backups of CITABAU's two files as well as CITADEL's.
- In the app: Tools > LBA2: race track opens on the island the folder was built with; Play shows the car setup with the rain option and starts scene 49 in fine weather with the real gantry.

![Play in the app, fine weather](racetrack/build/citadel_play.png)

**Known limitations**:
- One island at a time. `RACETRACK.JSON` describes the last one built, and Play races that one; building Citadel's leaves the Desert island's ground as it was but stops the game racing it. (Citadel Island itself now carries two tracks, one in each weather: see "Citadel Island in the storm".)
- The lap's tightest turn is 1.5 cells round the middle of the road (the Desert track's is 2.6), at the hairpin above the harbour.

## The story, the holomap, the gloves and the biker (2026-09-29)

![the holomap pictures with the tracks drawn in](racetrack/build/holomap_pictures.png)

**The holomap pictures** (`Terrain/RaceTrackHolomap.cs`). When the holomap zooms in on an island it shows a pre-rendered 640 x 480 picture of it -- HOLOMAP.HQR entry 18 + 2 x island, in the game's palette (RESS.HQR entry 0) -- and draws the arrows and Twinsen over it through a camera the next entry gives: the island's target point, two angles and a distance (HOLOPLAN.CPP InitHoloPlan, `SetProjection(320, 240, 1024, 700, 700)`, `SetFollowCamera`). That camera is reproduced exactly (LIB386's `InitMatrixStdF`, `LongWorldRotatePoint` -- a rotation only, the camera being the rotated target pushed back by the distance -- and `LongProjectPoint3D`); projecting the island's own heights lands on the island in each picture, islets included. The built road is drawn through it: asphalt taking its shade from the picture's own light, red and white curbs every 1.6 cells as on the ground, the start line, all hidden where the built ground stands between it and the camera (a depth buffer of every ground triangle), and the bridge deck drawn after the roads it passes over. Citadel Island has two pictures, the storm's (18) and, once it is over, its own slot 12's (42); the Desert island's is 22. The retail Desert picture's own little race track sits under the new one but for a few pixels of its gantry.

**Zoe's line** (`Terrain/RaceTrackStory.cs`, Citadel Island only; replaced on 2026-10-03 by the user's story -- see "The story and each track's drivers"). The game opens with Zoe walking up to Twinsen in their house (scene 0, actor 4) to say text 0 of Citadel's texts -- "rush to the downtown pharmacy" -- and switch on the pharmacy's holomap arrow (`set_holo_pos(22)`). Her walk stays; she says a new text instead: "Twinsen, the new race track is finally built, head to the start line behind the house to qualify. Don't forget your racing gloves.", in all six languages the game has (English, French, German, Spanish, Italian, Portuguese; code page 850, as the game's own texts). TEXT.HQR's language files are 15 per language, each an id list and a table of offsets and texts (an attribute byte, the characters, a 0: MESSAGE.CPP); the CD version speaks a text by its place in the list, so a new text goes at the end, past the last recorded voice, and is shown, not spoken (`Speak`: `num >= MaxVoice`). Her later reminder ("did you find something to cure the Dino-Fly?") keeps the pharmacy's arrow.

**The arrow.** Holomap positions are scene numbers: record 50 + n of HOLOMAP.HQR entry 12 is where scene n lies on its island, used for its arrow and for "Twinsen is here" when he is inside it. The game has 222 scenes and every number 0-99 is one, so the start line's arrow is position 222 -- no scene's, no script's, and the island view's arrow loop runs to 254. Its label is a new text in the holomap's text file (2): "Race track start line.". The race-track mode clears it once Twinsen is at the wheel (`holo_arrow=` in the car file). Fixed in the engine on the way: the arrows' spin was only set up for positions below 100, and the game's own 104-188 started still. The engine's `dumpstate` now lists the arrows switched on (`holo_active`): a new game shows `[22]` after Zoe's line on the untouched data and `[222]` on the mod's.

**The racing gloves.** The darts lie on a shelf in Twinsen's attic (scene 1, actor 8, entity 18 -- BODY.HQR 31); Action in zone 5 there gives them (`found_object(2)`). The inventory is 40 fixed slots, and all 40 are the game's: a save stores exactly 40, item n's count is game variable n, and variable 40 is already the Dino-Fly quest. The darts' own slot won't do either -- the shop sells them (scene 14, 4 kashes, `found_object(2); set_var_game(2, 3)`), and scene 36 and the Desert island's scene 67 give them too. So the gloves take slot 7, the part Baldino gives Twinsen for Zoe to mend the car: the mod has the buggy ready from the start, so the part has nothing left to do.
- Their model (`Terrain/RaceTrackGloves.cs`): a pair of driving gloves built from boxes on one bone -- lit racing red, white cuff bands and a white stripe down the back of each hand, dark palms and fingertips in flat colours (a lit grey ramp came out mid-grey). Standing, fingers up, backs forward, at the size of the game's own glove (OBJFIX 11) as OBJFIX.HQR 7, the model the inventory and the "you have found" screen turn; and lying flat at the darts' size as a new body of the darts' entity (body 1), so the pair lies on the attic's shelf.
- Their texts: slot 7's found message, name and description (file 2: 7, 107, 207) are the narrator's (EN_GAM.VOX has them), so each old text keeps its place in the file under an id nothing asks for (60000 + id) -- every text after it keeps its voice -- and the gloves' texts go at the end, unvoiced: "You have found a pair of racing gloves.", "Racing gloves", "Your racing gloves: a firm grip on the buggy's steering wheel, lap after lap." (six languages).
- The attic: the shelf shows the gloves and goes once they are taken (`if (0 < var_game(7))`); Action there gives them (`found_object(7); set_var_game(7, 1)`). The part's one use -- giving it to Zoe near the car, scene 49 -- would have taken them away, so it asks `2 == use_inventory(7)`, which never answers.

**The biker.** Citadel Island's motorbike taxi -- a Rabbibunny on his bike, entity 100 (body 0 = BODY.HQR 148; animation 0 standing astride it, 317 riding: ANIM.HQR 764 and 766) -- stood on the track: his actors were kept because Twinsen's script rides with him in a travel cutscene, and the one in scene 42 was on the road under the bridge. He races now: the taxi's copies leave the outside scenes, and a copy of him joins every one as the third opponent, on a line of his own down the middle of the road (a bike: 97 % of the car's top speed, 106 % of its cornering). The car setup races him (on by default, skill 90 %); the engine drives each opponent with its own animations (`opponentN_anim=0 317`; the cars keep 0 and 1). He races on the Desert island's track too.

![the gloves and the biker in the engine](racetrack/build/story_engine.png)

**Verified**:
- In the engine, a new game in scene 0: Zoe says text 996 (`[life] obj=4 MESSAGE dial=996`) and arrow 222 is on; the untouched game switches on 22. Her line shows in the game's dialogue box.
- The attic: the shelf actor is the gloves' body; after Action, item 7's count is 1, the darts' (2) untouched, the shelf gone; the found screen says "You have found a pair of racing gloves."; the inventory shows the gloves in the part's box.
- The race: the grid "1 Twinsen, 2 Baldino, 3 The racer, 4 The biker", GO, and the biker riding the lap on his bike with its riding animation, "Position n/4" on the display.
- The holomap: the island view in the engine shows the track on Citadel Island's picture.
- The menu build is byte-identical to the command-line build (all nine files, HOLOMAP.HQR, TEXT.HQR and OBJFIX.HQR now among the ones kept and put back). The suites pass.

## Mosquibees Island's mountain lap (2026-09-29)

![the drawing, the plan (its height from blue, low, to red, high) and the island as built](racetrack/build/mosquibe_plan_vs_built.png)

The drawing: a lap round the Mosquibees' mountain in two loops, up to a bridge (pink) over to the plateau, a jump (blue) along the plateau's west side, and back down. It had no pit lane; the build puts one beside the start line (below).

**The island.** MOSQUIBE.ILE is three cubes (7,8), (8,7) and (8,8); (7,7) is missing -- the engine's open sea. The Mosquibees' mountain (7,8) is a stepped cone: flanks steeper than 100 %, a 9,500 shelf, an 11,100 summit with the three landing pads. The plateau (8,7) is a mesa at 12,750 with cliffs all round and three bays cut into it (west, north, east). South of it a ridge (9,500-11,000) runs down to the low ground by the arrival (700-1,600); the sea channel between the mountain and the ridge is 13-17 cells wide. Outside scenes 102 (the plateau), 103 (the mountain) and 105 (the arrival); 104, numbered between them, is the Queen's throne inside the mountain.

**From the drawing to a plan** (`tools/RaceTrackPlan/fitcam.py`, `perspective_trace.py`, `mosquibe_design.py`). The drawing is the island seen at a slant, in perspective -- the high plateau drawn far bigger than the mountain -- so a flat fit of the picture does not work. A pinhole camera is fitted instead (position, turn, tilt, focal length) that turns the island's 3D ground into the drawing, scored by how well the land it would see covers the drawing's land (IoU 0.92). The drawn line is traced and each point put on the ground the camera sees behind that pixel. That trace is a guide, not the plan: the mountain's flanks are far too steep for a road to follow the ground, so the lap is designed by hand along it -- a closed spline through control points, with its own heights: straight grades keyed at points, a vertical curve (a parabola tangent to both grades) at every change, the start straight, the bridge deck and the jump exactly level.

**The lap**, 500 cells, 47.6 s for the race car driven by the test pilot (below):
- **The start line and the pit lane** on the plateau's north edge, the only long, level, open straight. The pit lane runs inside it (south); it is short (24 cells beside the lap), so it moves out from the lap over 7 cells instead of 24 (`pitTaper`), and the waiting spots are taken after the line too where the ones before it are still too near the lap (two before, two after, 7.4-9.2 cells to the side).
- The north-east hairpin (4.8 cells), an S-bend across the plateau, and **the descent** down its east side: 14 %, over the low ground on a walled causeway 5,000-9,000 high.
- **The bottom straight** along the south shore, over the channel's mouth, still coming down, to **the first loop's start at the mountain's south-west foot: 925, the lowest point of the lap, just above the sea.**
- **Two loops winding up round the mountain**, clockwise: the first round its flank, the second round the summit, 11.5 cells inside it and 5,000-8,000 above it -- terraces one above the other; at most 12.6 %.
- **The bridge**: from the mountain's shoulder a flat deck, 28 cells (7 tiles) at 11,100, over the first loop -- 5,265 above it, crossing at 65 degrees -- and over the channel to the ridge.
- Up the ridge (12 %) to **the jump over the plateau's west bay**: a ramp to a lip at 13,100 on the south-west arm, a 17-cell gap over the bay (the flight tops out 15,500 above the lava sea), a landing 3.4 cells down the ramp on the north-west arm; then the north-west corner to the line.

**The plan** (`docs/racetrack/mosquibe_track_plan.json`) carries more than the others: `heights` (one per point: the road follows them, cut into the mountain and built up over the sea, grade-limited to the plan's own `maxGrade`, 15.8 %), `deck` (the first and last point of the flat deck), `gapJump` (the take-off lip and the landing lip) and `pitTaper`. A plan with heights is built as drawn: no crossing is re-shaped and the crossing style doesn't apply (the dialog greys it out) -- except that the style still decides whether the deck is given its bodies in the island's OBL, so such a plan sets it itself (`RaceTrackService.FollowPlan`: a bridge when it draws a `deck`). The greyed-out box first kept whatever the folder's previous build had used, and a folder last built with the jump got a Mosquibees lap with no deck under its bridge (2026-09-29).

**What the builder does with a planned lap** (`Terrain/RaceTrackBuilder.cs`; none of it touches a plan without heights -- the Desert island's and Citadel Island's builds are byte for byte what they were):
- **Walled road** (`PlannedProfile`): wherever the road stands more than 1,200 off the ground -- built up over the sea or the low ground, or cut deep into the mountain -- it gets the water bridge's narrow level top with blocking rock at its edges instead of the 7-cell earth skirt, which spilled fill across whatever lies below: the sea, or the road on the next terrace. 367 cells of the lap.
- **Steep banks are blocking rock** (`WallSteepBanks`, and verge cells holding a step): the engine lifts a car straight onto higher ground it drives into, however much higher, unless the triangle blocks (EXTFUNC.CPP `ReajustPosExt`), so an ordinary bank beside a terrace was a ramp up the cliff to the next one. 918 cells.
- **The plan's deck** (`PlanDeck`): the flat tiles over the deck's points, lengthened to whole tiles, the ground just under their last cells and level with them for 4 cells beyond, the clearance over every part of the lap it crosses reported.
- **The gap jump** (`PlanGapJump`, the crossing jump's ramps and flight in `LayJump`): the road's own level, the ramps on it, and between the lips nothing -- the gap's ground is left as it is and not painted (`Void`). A checkpoint is kept away from it.
- **The sea under every cube**: the engine draws the sea in 4 x 4 squares, only those a cube's info word marks (`CubeBitField`); all are marked.

**The engine** (`native/lba2-classic-community`):
- **Ground across the near plane is drawn clipped** (TERRAIN.CPP `DrawFeuillePolyClipped`). A ground cell some of whose corners are behind the camera's near plane (3,000 units) was painted black from its visible edge down to the bottom of the screen -- right for flat ground passing under a camera close over it, which is off the bottom of the screen anyway, but on this lap the camera following the car passes a few cells from terrace walls, and whole sides of the view went black. Each of its two triangles is now clipped against the plane in the camera's space (`ClipperZ`, as the sea's `Draw_Poly`) and drawn with its own colour and texture. The retail islands look as they did.
  ![before and after](racetrack/build/mosquibe_near_plane.png)
- **A test pilot** for headless runs: the console's `autodrive 1 [look ahead]` steers and pedals the player's car along the first opponent's line (RACEMOD.CPP `RaceMod_AutoInput`), so a whole qualifying lap and race can be driven with no one at the keys. The race-track mode now logs each checkpoint, missed checkpoint and lap.
- The console's `cube` leaves any cutscene the old scene was in (a harness's jump straight after boot landed in the opening's `cinema_mode(1)`, which then held for the whole run: letterbox bars, and the Auto camera never ran); `screenshot [path]` takes a file name; `camtrace` shows the camera's eye.

**The scenes**: a cube change to any scene that isn't one of the island's own outside scenes is a door (the rule counted everything numbered from the first to the last outside scene, and 104 is the Queen's throne).

![along the lap in the engine: the loops, the summit, the deck, the ridge, the jump's take-off and flight, the north-west corner, the grid](racetrack/build/mosquibe_engine.png)
![the jump over the west bay](racetrack/build/mosquibe_jump.png)

**Verified**, on a copy of the game:
- The test pilot drives the whole lap: the qualifying lap through all five checkpoints in 47.61 s, the grid (Twinsen, Baldino, the racer, the biker), GO, and race laps of 47.63 s and 47.50 s, the three opponents lapping in 48-52 s.
- Every cube edge the lap crosses, both ways (8 of 8, the deck's included, at deck height).
- The jump: the flight starts on the take-off strip, clears the bay and lands on the far ramp; the deck carries the car level at 11,100 over the first loop.
- The frames round the lap with both cameras: no black left in the view.
- The menu build is byte-identical to the command-line build; "Put the original files back" restores every file exactly.
- The holomap picture (HOLOMAP.HQR entry 32) has the track drawn in; the picture itself is the island as it was, so where the build raised or cut the ground a great deal the road is drawn over the old shapes.
  ![the holomap picture before and after](racetrack/build/mosquibe_holomap.png)

**Two leftovers, and what caused them (2026-09-29)**. Driving the built lap turned up a Mosquibee's nest sitting on the asphalt and a plank walkway hanging in the sky. Both were general faults, fixed for every island; `ScriptRoundTrip trackleftovers <game folder> <ISLAND> <lap.csv>` is the probe that found them (the lap's centre line comes from a build run with `RT_DUMP=<file>`; `RT_BEFORE=<the untouched ILE>` tells what the build itself left hanging rather than what the island always had, and it lists the actors of the outside scenes the same way).

![the island, the walkway carried into the sky, and after the fix](racetrack/build/mosquibe_leftovers.png)

- **Objects were carried about by the ground under a corner of them** (`Terrain/IslandOps.cs`, `DecorFollow`). A decor keeps its height above the ground when the build reshapes it, read under the decor's own origin -- but an origin is not where the object rests: for many it is a corner of the box, and its `Y` is 0, the body itself being at `YMin..YMax`. Mosquibees Island's walkway is a row of planks on posts across a gully, so its origin stands over the gully floor; when the road's embankment filled that floor, the whole walkway rose 4,000 units into the air. Now a decor follows the ground only when it rests on it -- its underside at or below the highest ground anywhere under its footprint -- and one hanging clear of the ground is left where it is. (Following the *highest* ground under the footprint instead was tried and is wrong the other way: it lifted a small rock beside the new embankment onto the embankment's own height.)
- **A structure whose pieces stand apart was not recognised** (`RaceTrackBuilder.ClearRuins`). What the road ran through is cleared, and so are the orphaned pieces of the same structure -- but pieces counted as one structure only when their boxes touched within a quarter of a cell and lay within 64 units of each other in height. The walkway's planks are half a cell apart with a handrail a thousand units above them, so every plank was its own structure and nothing was cleared: the road went through the middle of it and left both halves standing over the drop. Pieces a little apart (`RuinGap` 0.6 cells) at much the same height (`RuinRise` 1,200) are now one structure too. The gap is deliberately small: reaching 1.5 cells merged whole hillsides into one structure, which then held a big piece and so nothing of it was cleared at all.
- **A kept actor standing on the road is moved aside** (`RaceTrackScenes.ShiftOffRoad`). The actors of the outside scenes are removed except the ones Twinsen's own script waits on in a cutscene; one of those was a Mosquibee's nest the second loop was laid straight through. Such an actor is now moved to the nearest spot clear of the road, in its own cube, on the new ground -- keeping the height above the ground it had, for one that doesn't fall. Not the ones on water: the harbour ferry waits there and sails a route of its own, as `Reseat` already allowed for. Citadel Island's circuit moves nine of them (the town's own scene 48), the Desert island none.

  ![the nest on the road, and moved aside](racetrack/build/mosquibe_nest.png)

## Citadel Island in the storm: a track of its own (2026-09-29)

![the drawing (turned north up), the lap designed on the island (its height from blue, low, to red, high) and the storm file as built](racetrack/build/citadel_storm_plan_vs_built.png)

Citadel Island is two files: `CITADEL.ILE` while the storm lasts (the whole opening of the game, up to the lighthouse) and `CITABAU.ILE` once it is over (see "The rain" above). The town circuit was built into both. Now it stays in `CITABAU` alone -- byte for byte what it was -- and `CITADEL` gets a simpler lap of its own, from the user's drawing (`citadelTrack.png`: a loop round the town, the pink dots a jump, the orange line a pit lane). No opponents race it yet: Twinsen drives it on his own, with the lap timer and the checkpoints.

**The drawing** is the island's map turned a quarter turn clockwise and squashed, like the town circuit's; the land masks were matched (IoU 0.84: 5.4 pixels a cell across, 3.8 down) and the lime line skeletonised and walked (`tools/RaceTrackPlan/citadel_storm_design.py` has the method). What it runs over decided the design more than the line did:
- the west side follows the town's **west rampart**, a raised street 9 cells wide at 2,500 (the town itself is at 250), and the drawing's two pink dots sit exactly at the rampart's south end, where it drops to the harbour. So the lap was first built **south along the rampart, jumping off its end** (it now runs the other way: see "Reversed" below): level at 2,000 from 10 cells before the take-off lip to 14 after the landing lip (the gap jump's rule), an 11-cell gap over the drop, the flight (17.6 cells, the retail one) wholly inside cube (8,9) -- the drawn dots are 5 to 7 cells further north, where the flight would have crossed into cube (8,8) mid-air. The landing is a causeway over the harbour's little basin, and the road comes down to the harbour's level round the south-west corner (10.8 % at the steepest).
- the south and east sides are the harbour's level; the **pit lane** is on the inside of the east straight as drawn, and the **start line** (the pit lane's middle, as on every track) is on the east straight, in scene 42.
- the drawing's **wiggle** at the north-east corner had three roads 7 cells apart -- less than one road's width (9 cells of asphalt and curbs). It is a double hairpin with its legs 11 cells apart, and it climbs back up to the north rampart (5.6 %). Its bends, 4.4 cells round the middle, are the tightest on the lap.

The lap is 297 cells; the racer's line round it would be 29.1 s driven perfectly, and the test pilot drives it in 28.2 s.

**One island, two tracks.** An island's twin file can now carry a track of its own (`RaceTrackIsland.TwinPlanResource`: Citadel's plan is `citadel_storm_track_plan.json`, its twin's `citadel_track_plan.json`). What that changes:
- `RaceTrackService.BuildFiles` (the one build both the menu and `buildtrack` run) builds the island's own file from its plan with no opponents, and the twin from its own plan with the options as chosen -- the opponents, the story and the crossing style go with the twin's track, the town circuit. The dialog's crossing style stays available for it (the storm track draws its own jump).
- The scenes are the same for both files, so `RaceTrackScenes.Apply` takes both tracks: each gets its buggy and Twinsen at its own start line in its own start scene (42 in the storm, 49 once it is over; a buggy is waiting at each), its jump and its cube-edge crossings; the zones on either road go; the actors' heights follow the ground of the nearer road.
- The holomap's storm picture (entry 18) shows the storm track, the fine weather's (42) the town circuit. The jump's flight was one animation for the whole game (the storm track's); since 2026-09-30 each island's track, and the town circuit, has its own (see "Several tracks in one game folder").
- `RACETRACK.JSON` describes the storm track and has the town circuit as `Twin`. Play races one of them, the town circuit's cars kept out of sight when it is the storm track (`RaceTrackService.Raced`, `RaceCarEngineFile`) -- chosen at first by the car setup's weather, now by the race track window (see "Reversed" below).

**Two faults found on the way, both general:**
- **An edge zone that only touched the road's height.** The build adds a cube-change zone where the island's own don't cover the road, and counted one as covering when its height range merely reached the road's middle. The zone from 48 into 42 starts exactly at the street's 250; the car on the new road there stood at 245 and was held at the edge. A zone now has to reach 512 below and 1,024 above the road (`CoverBelow`/`CoverAbove`), or another is added: 2 more on the storm track, 8 on the Desert's lap, 2 on Mosquibees Island's. The Desert's and Mosquibees Island's edge walks are what they were (42/46 -- the four corner crossings walked into the next-but-one cube -- and 8/8).
- **Play's save was made in the wrong weather.** Play enters the scene in a short headless run and saves there, then loads that save in the real run. The headless run had no race car file, so it was always the storm -- harmless while both files held the same ground, but now the town circuit's start is, in the storm file, in the sea by Twinsen's house: the save held him drowning, and the fine-weather game went on to his house. Both headless runs now get the same car file as the game (`Lba2Play.PrepareSceneSave`).

![in the engine, in the rain: the start, the double hairpin, and the jump off the rampart's end](racetrack/build/citadel_storm_engine.png)

**Verified**:
- `CITABAU.ILE` and `CITABAU.OBL` are byte-identical to the town circuit's build before this; the Desert island's and Mosquibees Island's builds are byte-identical but for the edge zones above (their ground, bodies and `RACETRACK.JSON` unchanged). The menu build is byte-identical to the command-line build (all twelve files).
- In the engine, in the rain: the test pilot (`autodrive`, on the storm line) laps it in 28.2 s through all 7 checkpoints, jump included; every cube-edge crossing of the storm lap walked both ways, 4/4 (`edgetest.py`), and the town circuit's in fine weather (`EDGE_TWIN=1`, with a `weather=fine` car file), 24/28 as before (the four departures from scene 48 loaded cold, held by its scripted arrival).
- In the app: Play with the rain starts scene 42 on the storm track's start line, beside the buggy; with it stopped, scene 49 on the town circuit's grid with the opponents in the pits.
- The suites pass.

**Known limitations**:
- The story is the town circuit's: Zoe sends Twinsen to its start line, and the holomap's arrow points there. A game that runs its opening in the storm (as the real game does, without the car setup's fine weather) finds the storm track in town and, at the arrow, only the buggy on the storm file's own ground.
- The storm track's jump zone and controller stay in scene 42 in fine weather too. Since the storm track was reversed both run north there, and it is the height that keeps them apart: the zone reaches 500 above the ramp (3,006), the town circuit's deck is at 3,500 (see "Reversed").
- No opponents on the storm track yet (the build gives it no grid, so the race-track mode times laps from the first crossing of the line).

### Reversed, its own entry in the window, and the loose ground (2026-09-29)

![the reversed lap in the engine, in the rain: the start (facing south now), the harbour side, and the jump from the harbour onto the rampart's end](racetrack/build/citadel_storm_reversed.png)

**The other way round.** The user asked for the storm track reversed: clockwise on the map now, north up the rampart's street, so the jump takes off on the harbour side and lands on the rampart's end, the start line on the east straight faces south, and the double hairpin comes down from the north rampart (`citadel_storm_design.py`'s `REVERSE`: the lap's points and heights in the other order, the gap jump's lip and landing swapped). The level stretch the jump needs now reaches 14 cells past the landing at the rampart's end, so its key moved 3 cells north (J0), and the pit lane's north end 2 cells south so that the start line (z 585.5) has the buggy (582.7) and Twinsen (581.0) behind it, clear of cube (8,8)'s edge at 576. The flight runs 596.5 to 579.0, inside cube (8,9); the painted arrows, the checkpoints, the start and the jump's zone and heading (turn 2048) all come from the lap's direction. The test pilot laps it in 28.35 s through all 7 checkpoints, jump included; the cube edges walk 4/4.

**Its own entry.** The race track window lists Citadel Island twice: "the town circuit (once the storm is over)" and "the storm track (in the rain)" (`RaceTrackIsland.CitadelStorm`, `RacesTwin`, `Title`). Both build the same files -- both tracks, since they share the island's scenes -- and differ in what `RACETRACK.JSON` names as built, which decides what Play races and in which weather (`RaceTrackService.Raced`, `FineWeather`): the storm track in the rain, starting in scene 42; the town circuit without it, in scene 49. The race car setup's rain box shows that weather greyed out for such a folder ("Citadel Island in the rain: its storm track is raced"); for any other folder it is the choice it was. A plan file is the chosen entry's track (the town circuit's goes to `CITABAU`). The menu build of the storm entry is byte-identical to the command line's (all twelve files); the two entries' game files are identical.

**The jump's zone and the town circuit's bridge.** Reversed, the storm track's jump runs north -- the same way as the town circuit on its bridge, which passes over it at 3,500 -- so the heading no longer tells them apart, and the zone reached 900 above the ramp (to 3,514). It now reaches 500 above (3,006 here; a car on a ramp is within a couple of hundred of it), for every island's jump; Mosquibees Island's still fires (its lap, 47.61 s, is what it was).

**Loose ground in the air.** On `CITABAU` the user saw a lump of rock floating by the town circuit. A decor follows the ground under its **origin** when the build reshapes it (`IslandOps.DecorFollow`, and since the Mosquibee leftovers only when it rested on the ground) -- and a 2 x 3-cell rock up by the Weather Wizard's tent, cube (7,7), had its origin's corner where the road's embankment rose: it went up 2,057 with it and hung 1,132 clear of everything under the rest of it. In cube (9,7), scene 50, a post and its sign had their ground cut away from under them and were left 517 and 2,392 up. `ScriptRoundTrip trackleftovers` missed all three: it measured an object from its origin's height (`Y`), which for many is 0, the body itself being at `YMin..YMax`; `RT_ADRIFT=1` now lists every decor whose underside is more than `RT_FLOAT` above the highest ground anywhere under its footprint, and with `RT_BEFORE`, whether it was so before. The build now removes any decor that rested on the ground before the ground was reshaped and is held up by nothing after it (`RaceTrackBuilder.ClearAdrift`, `AdriftBy` 400) -- not those already up there, which the island has plenty of: roofs on their walls, signs on their posts, planks over gullies. It took those 3 from `CITABAU`, and none from `CITADEL`, the Desert island or Mosquibees Island (their files are unchanged). What is left in the air on both Citadel files is the start gantries' beams.

### The jump that stalled, the start line that wasn't there, and the gantry (2026-09-29)

The user drove the reversed storm track and found three things: Twinsen stuck at the take-off, for a few seconds or for good, before he sometimes jumped; a post of the start gantry in the middle of the straight; and a lap timer that never started ("Cross the start line" all the way round).

**The jump.** The flight is Twinsen's animation (200) played from his track script once the controller puts him in movement 12, moved by his animation alone. A trace of every frame (the engine's `objtrace`, which now logs the generic animation and its `FlagAnim` too; `tools/RaceTrackPlan/jumptrace.py` drives it) caught it at 60 frames a second: one frame after take-off the flight was replaced by animation 34, `GEN_ANIM_SKATEG` -- Twinsen skating -- with `ANIM_ALL_THEN`, which nothing interrupts, and the car slid back down the ramp. When a falling object lands on a triangle whose blocking bit (Col) is set, the engine starts that slide (`OBJECT.CPP` `ReceptionObj`, `InitSkating`); the track script's `anim(200)` is refused while it plays, the flight's end label comes early, the controller hands the car back and the zone fires again -- stuck, then a jump seconds later, depending on where each frame lands. The blocking triangles were a stripe across the ramp: the ramp's warning stripe (hatching, the cell before the lip) had been turned into blocking rock by two painting rules meant for verges -- one within 2 cells of the sea, one for a planned lap's verge steps. Reversed, the take-off ramp stands over the harbour's basin, so the stripe was near water. A warning stripe is road, and neither rule applies to it now (`PaintRoad`). With it gone the flight is whole at every frame rate tried (10, 16, 20, 33 ms), with the default car and with the user's much faster one (80 km/h, 300 % acceleration); Mosquibees Island's jump likewise (its lap 47.60 s).

**The start line, and a native bug.** The user's `RACETRACK.JSON` had a start line of no length and no direction (`startline=8 9 23697 5019 23697 5019 0 0`), which no crossing counts. The same build on the command line was right, and so was the app's first build in a session; the next one, after the editor had redrawn its 3D view, was wrong. In it `Math.Round` itself gave wrong answers: -1.904 quarter turns rounded to -1, and `sin(pi/2)` to 0. The engine's triangle filler (`LIB386/pol_work/POLY.CPP`, `Draw_Triangle`), ported from the assembler, sets the thread's rounding mode to truncation for its maths -- the assembler's `fldcw [Status_Int]` -- and never set it back: the port kept only the comment for the closing `fldcw [Status_Float]`. The rounding mode belongs to the thread. The editor draws with the renderer on its worker threads, so a worker that had drawn a triangle went on truncating, and the race track build, run on one of them, snapped its start line to nothing (its sibling `POLYCLIP.CPP` saved and restored it properly). `Draw_Triangle` now restores it on both ways out, as the assembler did. That also changes the engine: its own maths after a frame's drawing no longer runs truncated. After the fix, five builds in a row in one session all come out right.

**The gantry.** One beam spans a road and its curbs, and the start line is always in the pit lane's middle, so a single gantry over the lap put its far post on the strip between the lap and the pit lane -- in the middle of the straight as a driver sees it. It is now as many beams end to end as reach over both (`PlaceGantry`, `RaceTrackReport.StartCurbs`: how far the outer curbs reach either side of the line), with posts only at the two outer ends; beams end to end come out longer than needed, and the spare goes where both posts have ground -- beyond the pit lane first (evenly, Mosquibees Island's lap-side post stood past the plateau's edge, over the sea). All four tracks get it: two beams, posts 4.9 cells out from the lap's middle and 16.1 on the pit lane's side.

**Also found**: after Play loads its save, Twinsen stands behind the buggy but facing across the road (the scene's start keeps his place, not his heading), so he has to turn to it before Action gets him in.

## Cars after the game's characters (2026-09-30)

![the cars, Body Studio's renderer](racetrack/build/character_cars_preview.png)

Fifty-eight more cars in the manner of Baldino's: each made after a character of the game and driven by it, so that every one of the game's eleven islands has at least three drivers of its own for the track it is to get, and the characters the game calls by name have one too (Twinsen has his buggy). They came in five rounds:
1. the three the user asked for: the Queen of the Mosquibees, the Emperor and Zoe;
2. one or two for the best-known character of each island;
3. enough to give each island three, and four from the user's notes (the wizard pedlar, the pighead with the broom, the souvenir shop's Sup, the casino's bouncer);
4. the named characters who had none yet -- and a correction: the guitar car is the heavy metal guitarist's, who plays at Rick's, and Johnny Rocket ("Zeelich's brightest star, the first explorer to set foot on Twinsun", says the Zeelich Gazette) has the rocket he did it in;
5. the Citadel Island thief, with the umbrella he steals as part of his car.

They are in the game's files after a build, as bodies 2 to 59 of the racer's entity (157; its own car is body 0, Baldino's 1). **No actor drives them yet**: the race-track mode still races the racer, Baldino and the biker. Each car says whose island it is (`RaceTrackCharacterCars.Car.Island`), for giving the tracks their casts once every island has one.

Who belongs where was read from the game's scenes: `ScriptRoundTrip islandcast` lists, for each island, the bodies its scenes' actors wear (so the old Franco with the Leontine is Otringal's, where he waits in the harbour; the Elevator Platform has only its monkey monsters, and the Franco policeman who guards the way to it makes its third). Who has a name was read from the game's texts (`ScriptRoundTrip lba2text find`) and the body list: the weather wizard is Bersimon on his diploma, the old Franco scientist is Mr. Kurtz on his door, Raph's two friends in the tavern are Pat and Fab (which of the two bodies is which is a guess: Pat the girl, Fab the sphero). The islands under the gas have few names.

| Island | Body | Driver (BODY.HQR) | The car |
|---|---|---|---|
| Citadel Island | 4 | Zoe (26) | A pink convertible flaring at the tail like her gown, a bow tied on it, a heart on the bonnet, long pink wings over the front wheels like her gloves, lamps with lashes. Zoe wears the pink gown and gloves of the user's picture (her body in the game is in red). [views](racetrack/build/cars/zoe.png) |
|  | 5 | the weather wizard (Bersimon) (111) | The thundercloud he keeps over the island: white puffs along its shoulders, a lightning bolt down each flank, rain under it, and the sun coming out on a mast behind. [views](racetrack/build/cars/weather_wizard.png) |
|  | 6 | Raph the lighthouse keeper (32) | A boat in the lighthouse's red and white, the lighthouse itself standing on its stern (banded tower, gallery, a lamp behind glass, red roof), a life buoy on each side. [views](racetrack/build/cars/raph.png) |
|  | 38 | Luc, the boss of the tavern (39) | One of his barrels on its side: staves and hoops, the tap in front, two mugs of beer on top. [views](racetrack/build/cars/luc.png) |
|  | 39 | Tim the waiter (40) | A silver tray on casters: a dish under its cover, a bottle and glasses (and his own tray still in his hand). [views](racetrack/build/cars/tim.png) |
|  | 40 | Mr. Paul (59) | A tugboat in his jersey's blue and white, red below: the wheelhouse behind him, its funnel smoking, old tyres down its sides, the anchor at the bow. [views](racetrack/build/cars/paul.png) |
|  | 41 | Mrs. Brune of the Inter-Islands ferries (62) | A traveller's suitcase: tan leather, two dark straps, brass corners and locks, a handle with its tag, the labels of everywhere it has been. [views](racetrack/build/cars/brune.png) |
|  | 42 | Mr. Bazoo the shopkeeper (99) | A brass cash register: the keys in rows on its front, the price tabs up at the back, the crank at its side, coins in the tray. [views](racetrack/build/cars/bazoo.png) |
|  | 43 | Miss Bloop of the museum (104) | One of her show cases: a marble plinth, the glass over a gold vase on its red cushion, the red rope on gold posts down each side. [views](racetrack/build/cars/bloop.png) |
|  | 44 | Bob (152) | A green rowing boat, an oar out on each side, his catch in a bucket behind him, a lantern. [views](racetrack/build/cars/bob.png) |
|  | 45 | Felix (161) | The fish he caught: blue back, silver flanks, fins, a tail, an eye on each side -- his rod up in his hand. [views](racetrack/build/cars/felix.png) |
|  | 46 | Zed (169) | A sandcastle: a tower with its flag on each back corner, battlements between them, a bucket and spade and a beach ball on the front. [views](racetrack/build/cars/zed.png) |
|  | 47 | Dino-Fly (167) | Himself on wheels: his green body and pale belly, the spines down his back, his tail, his wings spread, and his own neck and head in front. [views](racetrack/build/cars/dino_fly.png) |
|  | 48 | Rosa the cow (177) | A milk float in her black and white, three churns on the back, her bell under its nose; her head and horns out over the front. [views](racetrack/build/cars/rosa.png) |
|  | 49 | the Tralu (33) | A slab of his cave: stalagmites round it, a skull and crossed bones on the front, ribs at the back. [views](racetrack/build/cars/tralu.png) |
|  | 50 | Pat, of Raph's band (44) | The drums: the big drum on its side for a body, two toms, a cymbal on each back corner, the sticks crossed on the front. [views](racetrack/build/cars/pat.png) |
|  | 51 | Fab, of Raph's band (41) | The keyboards: black, the keys along both sides, a stack of speakers on the back and a mirror ball over them. [views](racetrack/build/cars/fab.png) |
|  | 59 | the thief (117) | A getaway car in a burglar's black and white hoops, two yellow eyes in the black of its nose -- and the umbrella he steals from the pharmacy's customer, open, planted in the back of it: its eight gores in the two creams of the game's own, the grey shaft and crook, the ribs underneath. His sack of loot beside it, coins spilling over the deck. [views](racetrack/build/cars/thief.png) |
| Desert Island | 1 | Jerome Baldino (139) | His rocket car (see "Baldino's rocket car"). The island's own racer in the game's own car is body 0. |
|  | 7 | the Dean of the School of Magic (123) | A wizard's hat on its side: the point curling up at the front, the brim round the back behind a gold band, blue with stars and a crescent moon, a crystal ball on the bonnet, sparks behind. [views](racetrack/build/cars/dean.png) |
|  | 33 | the wizard pedlar (Chedil Amiradoo) (240) | His flying carpet: red and green bands, gold down its edges, a tassel on each corner, the front curling up, his wares (a lamp, jars, a rolled rug) piled behind him. [views](racetrack/build/cars/carpet.png) |
|  | 52 | Joe the Elf (110) | The green shell his magic shut him in, on wheels, its sparks still flying. [views](racetrack/build/cars/joe.png) |
|  | 53 | Ker'aooc the healer (188) | His cauldron: black iron, the green cure bubbling in it, the ladle standing in it, his flasks hanging round the rim. [views](racetrack/build/cars/keraooc.png) |
|  | 54 | Tabata the wizard (189) | A spell book lying open: cream pages ruled with writing, the red cover under them, the ribbon out at the back, a quill in its inkwell, letters floating off the page. [views](racetrack/build/cars/tabata.png) |
|  | 55 | Moya the turtle (212) | Herself: the shell in plates of two greens, a flipper at each corner, her tail, her head up on its neck in front (nobody drives: her head is where a driver would be). [views](racetrack/build/cars/moya.png) |
| Emerald Moon | 8 | an Esmer in his space suit (255) | A moon rover: a white box wrapped in gold foil, solar panels either side, a dish and a whip aerial, four wheels alike. [views](racetrack/build/cars/moon_rover.png) |
|  | 14 | Baldino in his space suit (93) | A lunar lander: a capsule in gold foil with portholes, a flag, the engine's bell and its flame behind. [views](racetrack/build/cars/lander.png) |
|  | 15 | a Franco guard of the moon base (95) | One of the Esmers' flying saucers: a grey disc with a red rim and lights all round it. [views](racetrack/build/cars/saucer.png) |
|  | 63 | HAL, the moon base's computer (no body of its own: the room's bricks and screen sprites 228-230) | Itself, driving itself: the dark slate cabinet with the round green screen, the screen's grid and a red eye in it; a coil bottle with a white cap at each front corner; the red pipes arched over the top. It stands on the lilac-grey stand, which has rivets, the red grille and the teal horn. [views](racetrack/build/cars/hal.png) |
| Otringal | 3 | the Emperor (453) | A long black staff car: his coat's two rows of gold buttons down the bonnet, a gold radiator with his hat's red cockade, gold epaulettes over the front wheels, a red pennant on each wing, a throne's red seat back, and his two-cornered hat as the wing at the back. [views](racetrack/build/cars/emperor.png) |
|  | 58 | the Emperor's wife (454) | A coach in the red of her gown: gold down its edges, a crown over the back of her seat, a lantern on each side, gold wheels. [views](racetrack/build/cars/empress.png) |
|  | 9 | Johnny Rocket (154) | "Zeelich's brightest star, the first explorer to set foot on Twinsun": his rocket, silver with a green nose and fins and a red band, a star on each side, its engine lit. [views](racetrack/build/cars/johnny_rocket.png) |
|  | 37 | the heavy metal guitarist of Rick's bar (404) | An electric guitar in his colours: black with a white guard, silver pegs and strings down the neck that is its bonnet, an amplifier on the tail, a demon's wings, the fire of his show behind. [views](racetrack/build/cars/guitar.png) |
|  | 56 | Rick (425) | A grand piano: black, its keys across the front, the lid propped open, the champagne on ice. [views](racetrack/build/cars/rick.png) |
|  | 57 | Stan (from Time Commando) (439) | A time machine in the yellow of his suit: a clock on each flank, two rings round its tail, an aerial. [views](racetrack/build/cars/stan.png) |
|  | 12 | the old Franco (276) | His Leontine on wheels (he waits with it in Otringal's harbour): the wooden boat, the balloon over its stern on four ropes, the rudder and the propeller. [views](racetrack/build/cars/old_franco.png) |
|  | 34 | the pighead with the broom (438) | A road sweeper in the yellow of his overalls: the brush turning in front, his own broom standing at the back, a bin and a warning light. [views](racetrack/build/cars/sweeper.png) |
|  | 35 | the souvenir shop's Sup (430) | His stall: a wooden barrow, a red and white awning over little Twinsuns, the sign with the heart on the bonnet (and on his shirt). [views](racetrack/build/cars/stall.png) |
|  | 36 | the casino's bouncer (419) | A roulette wheel, red and black with gold on its rim and the ball on it, a pair of dice on the tail and a one-armed bandit's lever at his side. [views](racetrack/build/cars/roulette.png) |
| Celebration Island | 10 | the Dark Monk (311) | A block of his temple's stone: red eyes in front, lava in its seams, a horn on each front corner, a fire bowl on each corner of the tail and his gold key standing between them. [views](racetrack/build/cars/dark_monk.png) |
|  | 16 | a Sup priest (292) | A car like his robe, black with the red sash round it: a lectern with the book open on the bonnet, a candle on each front corner, censers swinging at the back. [views](racetrack/build/cars/priest.png) |
|  | 17 | FunFrock (313) | The Dark Monk unmasked: a blood-red car with a sabre down each flank and, behind him, the gold ring of one of his teleporters, glowing. [views](racetrack/build/cars/funfrock.png) |
| Island of the Wannies | 11 | a Wannie (194) | A mine cart off its rails: riveted iron plates, four small iron wheels, buffers, a heap of gems behind him, his lantern on a pole and his pick across the back. [views](racetrack/build/cars/wannie.png) |
|  | 18 | the old Wannie (386) | A slice of the family's firefly tart, as the game's own slice is (the inventory's): a wedge, its point forward, the red filling over its dark edge, a pink layer and the pastry, the crust standing up along its wide end at the back, three cream domes on top (the whole round tart until 2026-10-06). [views](racetrack/build/cars/tart.png) |
|  | 19 | the Wannie miner (342) | The mine's bulldozer: yellow, its blade out in front, an exhaust stack puffing, a bar over his head. [views](racetrack/build/cars/bulldozer.png) |
| Island of the Mosquibees | 2 | the Queen of the Mosquibees (196) | Herself on wheels: a fat abdomen in her blue and orange bands ending in a sting, see-through veined wings swept back over it, thin blue legs down to honey-gold hubs, and her own head (crown, red eyes, antennae, trumpet) looking out of a collar made like her crown. [views](racetrack/build/cars/queen.png) |
|  | 20 | a Mosquibee (192) | A honey pot on its side: an earthen pot, honey at its mouth and running over the lip, the dipper standing behind. [views](racetrack/build/cars/honey_pot.png) |
|  | 21 | the Queen's ventilation guy (197) | A fan boat: a flat hull, the great fan in its cage at the back, two rudders behind it (and his own fan in front of him). [views](racetrack/build/cars/fan_boat.png) |
| Island of the Francos | 22 | De La Fontaine (the burgermaster's brother) (450) | A gazogem tanker: the green tank with its yellow band and filler dome, a warning sign on each side, the hose reeled at the back. [views](racetrack/build/cars/tanker.png) |
|  | 23 | Mr. Kurtz, the old scientist (446) | His laboratory ("Retired Colonel - Specializing in non-scientific sciences", says his door): a see-through flask of something green bubbling at the back, a coil throwing sparks beside it, dials on the bonnet. [views](racetrack/build/cars/laboratory.png) |
|  | 24 | the Franco nurse (447) | A pram: a white tub with a blue band, its hood up at the back, the push handle, a red cross on each side, tall thin wheels. [views](racetrack/build/cars/pram.png) |
| Island CX | 13 | the Franco survivor (90) | The raft that got him off: planks, a patched square sail with a red cross daubed on it, an oil drum and a crate. [views](racetrack/build/cars/survivor.png) |
|  | 25 | a Franco trooper (290) | A tank in two greens, a gun down each flank, a whip aerial. [views](racetrack/build/cars/tank.png) |
|  | 26 | one of the Emperor's soldiers (260) | The island's rocket launcher: black, four red-nosed rockets on their rack at the back, a dish. [views](racetrack/build/cars/launcher.png) |
| Elevator Platform Island | 27 | a monkey monster (388) | One of the crates they heave about, braced and nailed, more of them roped on behind, his bananas on the bonnet. [views](racetrack/build/cars/crate.png) |
|  | 28 | the monkey monster with the sword (363) | An iron war cart: a ram in front, spikes and shields down its sides, his banner at the back (his sword stays in his hand). [views](racetrack/build/cars/war_cart.png) |
|  | 29 | the Franco policeman (86) | His patrol car, white over blue, the red and blue lights on their bar, a bumper bar in front. [views](racetrack/build/cars/patrol.png) |
| Island under Celebration | 30 | the ferryman (334) | His boat: long, dark, its prow curling up over a lantern, his pole lashed down its side. [views](racetrack/build/cars/ferry.png) |
|  | 31 | the Mosquibee dissident (270) | A dark car under his big red flag, a loud-hailer for a nose. [views](racetrack/build/cars/rebel.png) |
|  | 32 | a Mosquibee of the hide-out (193) | A slab of cooling lava: black crust, the glow showing at its edges and through its cracks, fire spitting out behind. [views](racetrack/build/cars/lava_slab.png) |

![the cars in the game](racetrack/build/character_cars_game.png)

**The code.** `Terrain/RaceTrackCarMesh.cs` holds what every such car shares: `CarMesh`, a body being built on the racer's car's 18 bones (points per bone, faces turned to face out, plates, balls, lines, boxes, loops and the skin between them), and `CarDriver`, which takes a character from its own body in BODY.HQR -- the bones above its waist, each arm turned by a two-bone reach to put its hand on the wheel, then scaled and sat in the cockpit as the car's bone 13. Baldino's car was moved onto them. `Terrain/RaceTrackCharacterCars.cs` (the first three), `RaceTrackCharacterCars.Islands.cs` (one for each island), `RaceTrackCharacterCars.More.cs` (three for each) and `RaceTrackCharacterCars.Named.cs` (the named characters) build the cars from a small kit -- `Hull` (rings of a superellipse from nose to tail; `Lens` for one round as a plate), `Cockpit`, `Wheels`, `Mudguard`, `Windscreen`, `Flame`, `Dish`, `SeatDriver`, `SteeringWheel`, and `Finish` (the cockpit, the wheels and the driver from a `Driver` and a `Cabin`: a new car is some forty lines) -- and what makes each one itself. `ScriptRoundTrip driverinfo <game> <body>...` gives what seating a character needs: its size, its bones, and the arms found from them (`CarDriver.FindArms`). `RaceTrackCharacterCars.Install`, called by every build (`RaceTrackService.Finish`), appends the fifty-eight bodies to BODY.HQR after Baldino's and gives each to entity 157 in RESS.HQR entry 44. Each is inside the engine's 550 points and 550 polygons, lines and spheres (the Emperor's the fullest: 531 points).

**The light a point takes.** A lit polygon is drawn in its colour plus the light, in steps up its ramp of sixteen. How many steps is the length of each point's normal -- and the game's own bodies use that point by point: 16384 on most of Twinsen and Zoe (the whole ramp, fifteen steps), 8192 on the Emperor's coat (seven), less on what should stay dark; 455 of BODY.HQR's 470 entries have points whose normals are not 10240 long (`BodyPipeline lightscales`). The body writer wrote 10240 everywhere (nine steps). It now keeps a share per point (`Body.LightScale`, read back from the normals; the preview renderer uses it), and that is what makes two of these cars possible:
- **Zoe's pinks** are the last four of the reds (76-79). Lit the usual way, a polygon starting there runs past the end of the ramp into the next one, which starts nearly black. With a quarter of the light (0.26) its points take two steps at most: 76 to 78.
- **The Emperor's black** is the first four of the greys (48-51); with nine steps a lit grey comes out mid-grey (his coat in the game is a lit grey-brown for that reason). With 0.3 it stays black, with a sheen.

The drivers keep the shares of their own bodies, so they are lit in the car as they are on foot (Baldino too: his car's BODY.HQR entry changed for that).

Two more things the writer now does: a polygon whose material is 2 is written as the engine's see-through type (what is behind it, moved into the polygon's ramp: the Queen's wings, the windscreens, the lighthouse's lantern), and a driver's textured polygons keep their textures (Raph's striped jersey, the space suit).

**Found on the way, in the engine.**
- *Outside, the last colour of every ramp is never shown.* Everything drawn outside goes through the island's fog table, and its row for no fog is not quite the identity: each ramp's sixteenth colour becomes the fifteenth (79 shows as 78, 63 as 62). The lightest pink on a track is 78.
- *An actor drawn against the depth buffer has its shadow drawn over it.* The opponents' actors had the flag for that (`OBJ_ZBUFFER`: the bridge and the hills hide them as they should) and a shadow, and the shadow came out as a dark patch across the side of the car nearer the camera -- on the racer's car and Baldino's since they first raced, only nobody could tell on a dark car. It showed on Zoe's: the top of a pink car drawn five colours darker. The game itself never gives such an actor a shadow (Twinsen's buggy: `BUGGY.CPP`), and the opponents now have none either (`RaceTrackScenes.OpponentFlags`).

**Verified**: every car in the engine, four ways round (`carshow` stands any of the racer's bodies in a scene; the pictures above are the Desert track's start straight), the colours on screen counted against the palette (Zoe's car: nothing past 78 and nothing of the next ramp; the Emperor's hull: 48 to 51); Zoe's, the Queen's and the Emperor's cars drawn with Citadel Island's and the Island of the Mosquibees' own palettes; every one of the game's bodies still reads back after a write (`BodyPipeline bodyroundtrip`); the Desert, Citadel and Mosquibees builds, and a race started in the engine.

`ScriptRoundTrip charactercars <game> <scratch>` builds the cars into copies of a folder's BODY.HQR and RESS.HQR; `BodyPipeline carviews <file.hqr> <entry> <game folder> <png> [island palette entry]` draws a body from six sides; `ScriptRoundTrip carshow <game> <scene> <x> <y|ground> <z> <turn> <dx> <dz> <body>...` stands cars in a test folder's scene; `BodyPipeline lightscales <file.hqr>` lists the bodies whose points do not all take the same light.

## Celebration Island: round the statue (2026-09-30)

![the drawing turned north up, and the island as built, from above and from the west](racetrack/build/celebration_plan_vs_built.png)

The drawing (`CelebrationTrackStatue.png`, the island's map turned a quarter turn): from where the taxi puts Twinsen down, up and round the statue in a spiral to its top (lime), and a long bridge back down to the start (pink).

**The island.** Celebration Island is one cube, (7,7), one outside scene, 95, and two files: `CELEBRAT.ILE` before the statue is there and `CELEBRA2.ILE` with it, which the engine loads once the story's flag (game variable 79) is set. The track is built into `CELEBRA2`; `CELEBRAT` is left alone. The taxi lands on a paved strip on the north-west corner, 6 cells wide and 26 long at 420. The statue is decor, not ground: seven bodies on one origin, 17,000 high from its base (3,891) to the crescent's tips (20,796), facing west -- 20 cells wide north to south at the shoulders (16,300), thin east to west, its head a box of 5.7 x 6.6 cells. The temple stands at its feet on the west side (top 11,140).

**Why the road stands in the air.** The engine's ground is one height map: a spiral of ground winding up to the statue's head would be a tower burying the statue, and the lap has to pass over and under itself twice over. The flat road bridge of the other tracks does not do either -- a decor object carries a car on its collision box, which is level and square to the map, so a slope made of them is a staircase, and a car that steps down from one box to the next *falls* and stops. So this lap's road is a **raised road**: decor bodies made for their own place, which only look like a road, and a floor of its own in the engine's race-track mode (below). The island and the statue stay as they are.

**The lap** (`tools/RaceTrackPlan/celebration_design.py`), 429 cells, counter-clockwise on the map, 41.8 s for the race car driven by the test pilot:
- **The start straight** down the dock, the line at its far end and the grid behind it, where the taxi lands. No pit lane (the dock is the only level ground there is): while Twinsen qualifies the opponents wait on the apron beside the dock (`pitSpots`).
- A short cutting through the hill south of the dock, and from there **the road leaves the ground**: two loops round the island, the first out over the sea along the coast, the second 6.5 to 8 cells inside it, corners of radius 8, **one even grade of 10.7 % for 296 cells**, up to 16,560 -- just over the statue's shoulders.
- **A level ring round the head**, radius 10.3 cells, 67 cells round: the inner rail is 6.5 cells from the head's middle.
- **The bridge**: straight, as drawn, 33.4 cells from the ring to the dock's corner. The ring is 16,000 above the dock and that corner is 33 cells away, so it is steep -- a parabola in at the top (5.5 cells) and out at the bottom (4), **109 % at the steepest** -- and it dives under the second loop's north leg and the first loop's north-west corner, 2,849 clear at the least. The car takes it at full speed and stays on the road. A gentler bridge would have to sweep round the island once more.
- A raised quay at the bridge's foot (a hairpin of radius 4, 150 over the dock -- two surfaces a few units apart flicker through each other) and a 3-cell ramp down onto the dock.

The road is narrower than the other tracks': 5.5 cells of asphalt and 7 with the curbs (9 and 11 elsewhere), 7.5 with the rails -- three levels of it have to fit side by side in one cube.

**The plan** (`docs/racetrack/celebration_track_plan.json`) adds: `raised` (the first and last point of the raised road; the span may wrap past the lap's first point), `raisedHalf`, the road's widths (`asphaltHalf`, `curbHalf`, `vergeHalf`, `blend`), `start` (the start line's point, for a lap with no pit lane), `pitSpots` (where the opponents wait: cell x, cell z, heading), `keepBodies` (decor the road may pass over: the statue and the temple) and `seaClearance`.

**What the builder does** (`Terrain/RaceTrackBuilder.cs` `PlaceRaised`, `Terrain/RaceTrackRaisedBody.cs`; none of it touches a plan without `raised` -- the Desert's, Citadel's and Mosquibees Island's builds are byte for byte what they were):
- **The road's pieces**: one decor body for every 4 cells of raised road, 99 of them, each made for its place from the lap's own cross-sections -- so the bends, the grade and the bridge's curve are in the mesh and no piece is turned. A slab 200 thick, asphalt, the red and white curb blocks running on from piece to piece, a rail 220 high each side, and an arrow cut into the asphalt of every third piece.
- **The piers**: a column and a cross beam every 10 cells where the road is 900 or more over the ground, 38 of them, moved along the road by up to 4 cells to stand clear of the lap's other levels, the opponents' waiting spots and the island's own objects.
- **The boxes carry nothing.** A piece's collision box is put out of reach (its top at -32,000) and a pier's ends below any road near it: the engine lifts an object onto any box top between the floor and the object within its footprint (`ReajustPosDecors`), and on the bridge each piece's little box was a ledge the car hung on and then fell from.
- **What is under the road stays**: only decor reaching into the road's own space goes (4 solid objects, all body 3, and 7 props), and the collision boxes of the statue and the temple are cut down to end under the road that passes over them (2).
- **The ground** is levelled and painted only where the lap is on it, the dock and the cutting: 400 vertices, 370 cells. No banking on a raised lap (the hairpin's curvature tilted the dock's first cells).
- **The textures.** This island's ground texture has no free space for the road's tiles, so the road is painted with the island's own: asphalt from its darkest even patch, white from its whitest pixel, its cliff for the walls, the arrows' flat colour for the hatching (`RaceTrackTextures.Borrowed`).
- The 137 new bodies are added to `CELEBRA2.OBL` (from 30 on, after the copied gantry and arch); `RACETRACK.JSON` carries the road's line (`Raised`: x, z, height and half width at every point) and a height for the start line and each checkpoint.

**The engine** (`native/lba2-classic-community`, the race-track mode only; the car file's `raised=<file>` and `statue=1`):
- **A floor of its own** (RACEMOD.CPP `RaisedAt`, through a hook in 3DEXT/MAPTOOLS.CPP `CalculAltitudeObjet`): for an object at a given height the floor is the highest level of the road not more than 700 above it, else the island's ground -- so a car on the first loop is not lifted to the second, and one under the bridge stays on the dock. It carries every object: Twinsen on foot, the opponents, the shadows.
- **The rails** (`RaceMod_Rail`): Twinsen and his car are kept inside the road's edge while on it; the road's triangles never block (`GiveTerrainCol`), at any grade.
- **The lines count at their height** (the ninth number on `startline=` and `checkpoint=`): the bridge passes over the dock and under the loops, and a line is crossed only by a car on its own level.
- **The statue's island whatever the story says**: the race-track mode loads `CELEBRA2` and its holomap picture (HOLOMAP.HQR entry 44) when the track built is the statue's (EXTFUNC.CPP, HOLOPLAN.CPP).
- **Twinsen faces his car** when a game starts beside it (`FaceTheCar`; on every track -- a scene's start keeps his place, not his heading, and the car takes him only when he faces it).

**The camera.** The classic camera stands still until Twinsen leaves the screen, and on this lap the car goes out of sight behind the road's other levels without leaving it, so a raised road turns the camera that follows the car on. Getting that camera to show the car on the bridge took four fixes (FOLLOWCAM.CPP):
- Its lift over the ground in front of the eye was worked out and then thrown away before anything was drawn: every full redraw goes through `AffGrilleExt`, which sets the camera again from its angles. It is applied again after that (`FollowCamReapplyLift`). This was so on every island -- an eye behind a hill stayed behind it.
- The lifted eye was aimed with `SetTargetCamera` (LIB386/3D/CAMERA.CPP), which takes the target's height from the camera's z and its z from the camera's height. The follow camera now aims its own (`FollowCamLiftEye`); the library's function is left as it is.
- The road counts as ground in front of the eye (`SightFloorHook`), and behind a car coming down the bridge it rises faster than any camera can see over at the arm's usual length, so the arm shortens as the road steepens behind the car (`FollowCamArmHook`, `RaisedArm`): a close view from above, under the levels overhead.
- The higher the lift, the less of the forward lean the aim keeps, so the car stays in the frame.

**The scene** (95): 27 actors removed and 4 left, 6 fixed camera zones and 4 zones on the road removed, the buggy copied from the Desert's scene 67 (the island has none of its own), the racer, Baldino and the biker added. The characters' cars are not cast yet.

![one lap in the engine: the grid on the dock, the first loop over the sea, the second, the ring round the head, down the bridge, the quay, the line](racetrack/build/celebration_engine.png)
![the holomap picture before and after](racetrack/build/celebration_holomap.png)
![the race track window](racetrack/build/celebration_dialog.png)

**Verified**, on a copy of the game:
- The test pilot drives it all: the qualifying lap through all 8 checkpoints in 41.82 s, the grid (Twinsen, the racer, Baldino, the biker), GO, race laps of 41.82 s, the opponents lapping in 42.9-45.1 s on the raised road.
- `tools/RaceTrackPlan/raisedtest.py`, 15 of 15: the car put on the loops and the bridge and driven ahead, steered hard into either rail, reversed, started from the rail, driven back *up* the bridge, and Twinsen on foot walking along and across the road -- never more than half a unit off the road's height (150 for Twinsen walking down the bridge, a step behind the floor), never past the rail.
- Mosquibees Island's lap is what it was (47.60 s); the Desert's, Citadel's and Mosquibees Island's builds are byte-identical to before.
- The menu build is byte-identical to the command-line build; Play from the app starts scene 95 on the grid, on the statue's island. The suites pass.

**Known limitations**:
- **Only the race-track mode can drive it.** In the retail game, or the engine without that mode, the road is there to look at and carries nothing: a car leaves the dock's cutting onto the ground under the road.
- **No track on the island without the statue** (`CELEBRAT`), and with a track built Play always shows the statue's island. (Since 2026-10-01 there is: "Celebration Island: the lava lake" below, in a scene of its own.)
- **Piers are left out where there is no room**: over the lap's other levels and close to the statue the road hangs unsupported for up to a few pieces.
- **A piece of road crossing the camera's near plane loses polygons** (a body's polygon with a point behind the plane is not drawn), so the road right under a close camera can show a hole for a frame.
- The holomap picture draws the road at its height over the island as it was.

`ScriptRoundTrip decorpoints <ISLAND> <out.csv> [body ...]` writes the points and triangles of an island's decor bodies in island coordinates (how the statue was measured), and `sceneactors <game> <scene>` lists a scene's actors.

## The Elevator Platform: a rollercoaster (2026-09-30)

![the lap from above, its height from blue (low) to red (high), its profile, and the platform as built](racetrack/build/elevator_plan_vs_built.png)

The request: a track for the Elevator Platform, "a very small area, so we may need to build vertically, perhaps with a rollercoaster inspired design".

**The island.** `ASCENCE.ILE` (island byte 10) is one cube, (7,7), and one outside scene, 120 -- the foot of the elevator down from Otringal. Its ground is nothing but sea (heights 0 and 50): the platform is all decor, 28 cells by 24 -- a main deck at 4,028 on legs, two lower decks and a landing at 3,270, the elevator's tower in the middle of the main deck (x 23-32, z 31.4-40.4, up to 10,802, with a lamp on a bracket at two of its corners), a crane house on legs east of it (from x 39.3, its cabin 4,775-7,275) and an airship moored beyond that (its fins up to 7,381). Twinsen comes out of the elevator's door on the south side of the tower.

**The lap** (`tools/RaceTrackPlan/elevator_design.py`), 354 cells, all of it a raised road on piers:
- **The station**: level with the main deck (4,028), its rail along the deck's south edge in front of the elevator's door. The start line is there, the grid behind it; the start gantry is one of the road's own (below).
- **The lift**: a helix round the tower, two turns and a quarter at 15.6 %, its levels 4,800 apart (the camera has to fit between them, and the second level has to pass over the tower's lamp at 8,060), up to **14,844** -- 4,000 over the tower's top. The ring is as tight to the tower as it goes: its sides half a cell off it (a rounded corner has to clear a square one), its outer rail inside the crane house's legs.
- **The first drop**: north off the top, a short level stretch, a crest and **125 %** down, into a banked diving turn onto the north edge, where a camelback takes it up again (to 6,200) and a turn-round brings it back east.
- **A hill that passes under the drop** -- the lap's one crossing, at 90 degrees -- and a climb to **the airship**: over its fins at 7,800, down between them to 6,500 over the hulls, up to 7,300 over the tails, then a dive round the south-east corner.
- **Three hops** along the south edge, and a turn up into the station.
- **Banking**: every bend leans in, up to 0.40 (the height across the road per unit across it; 0.25 on the helix, whose inside edge passes close to the tower); the station and the first bend out of it stay level.

The road is the narrowest yet: 4.8 cells of asphalt, 6 with the curbs, 6.5 rail to rail (Celebration Island's is 7.5, the others' 9 of asphalt).

**The plan** (`docs/racetrack/elevator_track_plan.json`) adds `bank` (one number a point), `gravity`, `railCamera` (below), `pierBodies` (the decks a pier may stand on), heights on the `pitSpots` (a deck of decor, which the ground knows nothing of), and a `raised` span that is the whole lap.

**What the builder does** (`Terrain/RaceTrackBuilder.cs`, `RaceTrackRaisedBody.cs`, `RaceTrackScenery.cs`; the other islands' builds are byte for byte what they were):
- **A lap that is all raised road** is a loop: the road's file closes on its first point, the pieces run on round it, and the start line is painted on the piece it lies on -- a white cell, red curbs either side.
- **Its own start gantry**, one body on the road: red posts on the rails and a chequered beam, 2,940 to its top. The retail gantry (3,990) does not fit under the helix's next level: the car stuck against its box a lap up.
- **Banked pieces**: each cross-section leans with the road, rails and all.
- **Piers on what is really there** (`RaceTrackScenery`: the island's decor as the triangles they are drawn with, not their boxes). A pier stands on the sea or on one of the plan's decks, never on a crate or the airship, and its column may not pass through anything or through another part of the lap. Where it cannot stand under the road's middle it stands beside the road, just outside the rail, its beam reaching under the road from there -- on the helix that puts every level's columns beside the levels below it, like a real coaster's. 33 piers; over the airship there is no place, so the road spans it.
- **What is in the way**: crates and barrels on the decks (14) are taken away; the platform's own structures -- the decks, the tower, the crane house, the airship, the fences -- stay, their collision boxes cut down under the road that passes over them.
- **The scene** (120): the grid on the road's own level at the station (`RaceTrackReport.RaisedFloor`), Twinsen and the buggy there, the opponents waiting on the landing east of the station (3,270), 3 actors removed, a door zone and 8 fixed camera zones removed; the holomap picture (entry 38) drawn.
- A checkpoint may lie across the road under or over another level of it (the engine counts a line within 1,200 of its height).

**The engine** (`native/lba2-classic-community`, all of it the race-track mode's):
- **The banked floor** (`RaisedAt`): the road's height at a place is its surface's there, from a fifth number on each point of the road's file (the banking in ten-thousandths). The car's pitch and roll follow it.
- **Gravity** (`gravity=`, BUGGY.CPP `RaceSpeedOnSlope`): on the raised road the slope pulls at the car, 5,000 units a second squared times its sine -- a cell being about a metre, that is gravity. Up a climb the engine holds a share of the gear's top speed only (1 - 0.6 x the slope); down a drop the car runs past its top gear's speed, and loses the excess to drag over a second or two, so what it gathers down the first drop carries it over the camelback. Over its top speed the car steers as much quicker as it goes faster, and a banked bend turns it quicker still (1 + 2.2 x the banking), so a bend it can take at its top speed it can take at any. The rail, while it holds the car, takes the excess speed off (2,000 a second). The opponents' speeds are planned with the same physics (`PlanSpeeds`), and so is the builder's report of them. With the test pilot's line the car reaches 50 km/h at the foot of the drop; a perfect lap is 32 s.
- **The rail camera** (`railcam=`, `RailCamera` through FOLLOWCAM.CPP's new `FollowCamEyeHook`): while Twinsen drives, the eye rides the road itself -- over its middle 9 cells behind the car, 3,400 up, looking between the car and the road 4 cells ahead; nearer and lower round a tight bend (so it looks along the car's way), high enough to see the car over a crest, and under the level of the road above it. An arm behind the car did badly here: the helix's levels hid the car, the drop's crest hid it, and the arm swung out over nothing in the bends. The view moves onto the rail camera over a few frames when Twinsen gets in.
- **On foot, under a level of the road that runs with his** (the helix over the station), the camera that follows him keeps its eye under that level (`RaisedArm`); a level that only crosses overhead is let pass.
- **A scene that starts on the raised road starts on it** (OBJECT.CPP): the scene's start and a restart put Twinsen on the ground under him -- here the sea, where he drowned -- and now on the road.
- **The rail holds after actors' collisions too** (EXTFUNC.CPP): an opponent's car sideswiping Twinsen's pushed it over the rail into the sea. It showed only in races where Baldino started on pole; the player's car is now held on the road after the push as before it.
- **The opponents start at the grid's own level** (`OnSpot`): their line's nearest point to a grid spot was taken in plan alone, and the helix's levels are over the grid -- a car put on one started the race a third of a lap ahead, lapping in 27 s.
- The road counts as a level over a place only away from the stretch the place is on (`RaisedBetween`): on a 125 % slope the road a few cells up it is "over" a place on it too, and the camera was put under it. The test pilot looks further ahead the faster the car goes.

![the start: Twinsen and his car in the station, the elevator's tower on the left, the gantry, the landing with the opponents' cars](racetrack/build/elevator_start.png)
![one lap in the engine with the rail camera: the helix round the tower, the top, the first drop and the diving turn, the camelback, the airship, the hops, the station](racetrack/build/elevator_engine.png)
![the holomap picture before and after](racetrack/build/elevator_holomap.png)
![the race track window](racetrack/build/elevator_dialog.png)

**Verified**, on a copy of the game:
- The test pilot drives it all: the qualifying lap through all 8 checkpoints in 34.02 s, the grid, GO, race laps of 35.3-35.8 s against the three opponents' 34.6-36.5 s. The pilot's trace of every frame: never off the road, never off its height by more than a unit (`tools/RaceTrackPlan/pilottrace.py`).
- `raisedtest.py`, 18 of 18: the car on the helix, the upper helix and the drop, steered hard into either rail, reversed, driven back up the drop, started from the rail, on the most banked bend steered either way, and Twinsen on foot walking along and across the road, the banked one included.
- The races with Baldino on pole, which threw Twinsen off the road before the rail held after collisions, race through (qualifying seeds 2 and 3).
- The Desert island's, Citadel Island's, Mosquibees Island's and Celebration Island's builds are byte-identical to the last commit's (every file); Celebration Island's lap is 41.82 s and its raised-road test 15 of 15, Mosquibees Island's lap 47.60 s, as before.
- The menu build is byte-identical to the command-line build (all ten files); Play from the app starts scene 120 in the station, Twinsen beside his car. The suites pass.

**Known limitations**:
- **Only the race-track mode can drive it**, as Celebration Island's.
- **The helix's bends are tight** (5 cells round the middle of the road): the test pilot touches the rail in about a quarter of its frames there. Under its top speed the rail costs a car nothing; over it, the excess.
- **No airtime**: the car keeps to the road over the crests however fast it goes.
- **The road spans the airship unsupported** (17 cells), and one more pier finds no place.
- The Desert track's gantry and arch are still copied into the island's OBL (unused).
- The rail camera is a close chase view on the helix, where the level above leaves it little room.

### Through the road behind Baldino (2026-09-30)

The user raced it against Baldino and now and then went through the road and fell to the level below -- never alone or against the biker. Both were the engine moving Twinsen after the rail had had its say, when an opponent's car was against his (their collision boxes overlapping):
- **Pushed over the edge.** An actor's box pushes the hero out of it, sideways, and the engine does that in more than one place in his move: the rail was applied after the first, not the others. Racing a deliberately slow Baldino so that the test pilot rams him 2,300 frames over, Twinsen's car was pushed to 1,731 from the road's middle -- past the road's edge (1,664), where the next level down of the helix is the floor.
- **Lowered into it.** An object that nothing of the ground holds up is lowered half a brick (128) to see whether it stands on another object, and left there when one is found: against Baldino's car Twinsen rode 128 into the road, 305 frames of that run.

The rail and a new floor (`RaceMod_Floor`: not under the surface of the road he stood on) now hold after everything else in the hero's move (EXTFUNC.CPP `DoAnimExtGround`). Baldino did it the most because his car's box is the widest of the three (1,396 across, the racer's 1,266, the bike's 894) and his line the closest to the player's. The same ramming run now: never under the road, never past the rail (1,025 from the middle, the rail holding at 1,024); races with all three opponents on three grids, both tracks' raised-road tests (18 and 15), Celebration Island's lap (41.82 s) and Mosquibees Island's (47.60 s) are as they were. `pilottrace.py` now checks every frame for both. To ram an opponent: a car file with only that opponent (its `opponent2_` lines renamed `opponent_`, the others' actors in `hide_actor=`), `opponent_pace=45` and `opponent_catchup=0`, and the test pilot, which follows the first opponent's line, run long enough to lap it.

`tools/RaceTrackPlan/builtviews.py <game> <ISLAND> <scratch> <first new body> [views]` draws a built island from its sides, from above and at a slant; `pilottrace.py <engine log> <raised road file>` tabulates the speed along the lap from a headless run of the test pilot with `objtrace 0`.

### A car for every character (2026-10-05)

The user's: "Generate a vehicle for every unique character in LBA2 then produce an image that displays all of them along with a description and number".

![every race car, numbered](racetrack/cars/all_cars.png)

(Pages of 48: [1](racetrack/cars/all_cars_1.png), [2](racetrack/cars/all_cars_2.png), [3](racetrack/cars/all_cars_3.png), [4](racetrack/cars/all_cars_4.png), [5](racetrack/cars/all_cars_5.png).)

**The cast.** 154 more characters, numbered 64-217 after the 64 cars before them (0 the racer's, 1 Baldino's, 2-63 made by hand): one body for each character of BODY.HQR -- a character's variants with something in its hand or in another pose are one -- less Twinsen (his buggy), the characters that already had a car, and what isn't a character (props, vehicles, doors, silhouettes; "Dot", 29, is a static model). `RaceTrackCharacterCars.Cast` (Terrain/RaceTrackCharacterCars.Cast.cs) lists them with their island (from the scenes each body stands in) and kind.

**Made by steps, not by hand.** Each car is the style its kind drives -- Sups roadsters, Francos jeeps (police and guards with a light bar), grobos trucks, rabbibunnies and children karts, quetches bubble cars, spheros ball cars, Wannies mine carts, Mosquibees and flyers gliders, glooms and bathers swamp boats, pigheads and monsters monster trucks, robots robot cars, animals basket cars, ghosts and gas clouds see-through ghost cars -- in the colours its driver's body covers most (by area; each a ramp start the light keeps in its ramp), with the driver:
- *with arms* (CarDriver.FindArms): cut at its waist (the hips its legs hang from), sized to show about as much as a Sup does in the cars made by hand (narrower than the cockpit), its hands on the wheel;
- *grobos*: the hand-made grobo cars' seat (arms 9 and 11 -- FindArms takes their ears -- waist and size for the grobo's height);
- *without* (animals, creatures, robots on legs -- FindArms takes the legs -- clouds): sat whole on the cockpit's floor;
- *too big for the engine together* (550 points, 550 polygons, lines and spheres): leaner cars in steps (fewer sides, five-sided wheels, no trimmings), then the driver's smallest polygons left out.

The sheet showed what the first steps got wrong -- creatures cut away under the rim or the cockpit's floor (the snake, the flying rat, the dogs), grobos holding the wheel by the ears, robots seated by the legs, spheros buried in their ball -- and the rules above are what put them right.

**In the game's files.** Bodies of two new entities (345 and 346 in the sandbox), copies of the racer's animations, a hundred cars each -- an entity's body numbers are one byte and each car has its half-size copy beside it (+100). The build installs them once per build (`InstallCast`, after the cars made by hand and their small copies; +0.8 s; BODY.HQR 7.6 MB). **RACECARS.JSON** in the game folder lists every car by number: name, driver, island, entity, body number, BODY.HQR body and small copy. "Put the original files back" removes it.

**Driving them.** Race car setup > Your car lists all 218 (by number, name and driver) from RACECARS.JSON; `drive_as=` takes the car's BODY.HQR bodies from it. They aren't raced by anyone yet: every one is a body of an entity, so a line-up can use it. (Since 2026-10-17 one does: a `RaceDriver` with `Cast` -- its number -- is put in the scenes as its own entity and body, read from the folder's RACECARS.JSON, which the build installs before the scenes (`RaceTrackScenes.Apply`); the Temple of Bú races the skeleton's, the ghost's and the two-headed monster's: docs/LBA2_TEMPLE_OF_BU.md.)

**Tools:** `ScriptRoundTrip castcars <built folder> <scratch> [numbers]` (the cast installed into a copy of BODY.HQR and RESS.HQR, each car's counts); `BodyPipeline castsheet <built folder> <out.png> [columns] [per page]` (the sheet and its pages, from RACECARS.JSON).

**Verified:** all 154 made (none over the engine's limits); the sandbox's seven islands rebuilt with them; the Desert track driven as the baggage grobo's truck (76) and the camel's monster truck (124); the race car window lists 219 choices (the buggy and 218 cars).

## A jump flown at the car's speed (2026-09-30)

![the storm track's jump, the fast car and the default car at the same moments after the take-off](racetrack/build/jump_speed.png)

The user asked whether a jump, a scripted flight, could go faster when the car comes to it faster. It does now. The flight is Twinsen's animation 200 (see "The jump"): its keyframes' steps carry him along its arc in the animation's own time, so until now every car flew every jump in the same time, about 2 s -- a car at 80 km/h slowed at the take-off to the flight's own speed (36 km/h; 32 as flown, see below), and the default car (34 km/h) went on at about its own.

**How.** The engine's race-track mode now runs the flight's clock faster or slower (`RACEMOD.CPP` `RaceMod_JumpClock`, around the hero's `ObjectSetInterDep` in `OBJECT.CPP` `GereObjAnim`): as many times as fast as the car's speed at the take-off is over the flight's own speed, which it works out from the animation (its keyframes' steps along the ground over their times: 36 km/h on the storm track, 41 on Mosquibees Island's longer flight). Each frame the keyframe's start and end times are brought nearer by the extra time, so the animation finds itself that much further on. The arc is the same, so the landing is too: the car comes down on the far ramp whatever its speed, and keeps its speed through the air (the buggy's speed is left alone while the animation carries it), so it lands going as fast as it took off.

The engine drops the time an animation runs past a keyframe's end (the next keyframe starts from the frame's time). At the flight's own speed that cost little, but flown twice as fast at 60 frames a second it would lose a third of the rate; the time run past is carried into the next keyframe instead.

**The limits.** The rate is kept between 0.6 and 4: a car that crawls onto the take-off still jumps (in 3 s at most on the storm track), not in slow motion. The car file's `jump_speed=<lo> <hi>` sets them, and `jump_speed=0` turns it off (the flight in its own time, as before). The app doesn't write the key; the engine's defaults apply.

**Verified** (headless, the test pilot, `tools/RaceTrackPlan/jumptrace.py`, which now reports each flight's time, length and speed and the car's speed down the far ramp, and reads any track's log):

| | Take-off | Flight | In the air | Down the far ramp | Lap |
|---|---|---|---|---|---|
| Storm track, the user's fast car, before | 80 km/h | 32 km/h | 2.00 s | 78 km/h | 13.97 s |
| Storm track, the user's fast car, now | 80 km/h | 78 km/h (x2.24) | 0.82 s | 77 km/h | 12.78 s |
| Storm track, the default car, now | 34 km/h | 33 km/h (x0.95) | 1.90 s (2.00 before) | 32 km/h | 28.29 s (28.38) |
| Storm track, a slow car (12 km/h) | 12 km/h | 21 km/h (x0.6, the limit) | 2.99 s | 12 km/h | |
| Mosquibees Island, the default car | 34 km/h | 34 km/h (x0.84) | 2.50 s | 34 km/h | 47.81 s (47.60) |
| Mosquibees Island, the fast car | 80 km/h | 79 km/h (x1.97) | 1.07 s | 81 km/h | 20.54 s |

The same peak (2,022 over the take-off on the storm track, 2,579 on Mosquibees Island) and the same landing in every run; the fast car's flight is the same at 10 and 16 ms a frame. The default car's flight on Mosquibees Island takes 0.4 s longer than before: that flight was drawn out for a longer gap and flew faster than the car (41 km/h), and now keeps to the car's 34. The tracks without a jump are unchanged (the Elevator Platform's pilot lap 34.02 s).

Found on the way, and not changed: with the simulation stepped at 33 ms a frame (`--fixed-timestep 0 --fixed-dt 33`; the engine steps at 16 ms whatever the frame rate unless its `FixedTimestep` setting is changed), the fast car stalls at the cube edge just past the storm track's far ramp, with or without this change -- 375 units a step overshoots the edge.

## Several tracks in one game folder (2026-09-30)

![five tracks built at once, and Play with three of their islands open](racetrack/build/multi_tracks.png)

The user asked for more than one track to be built at the same time, and for Play to start the one of the island the editor has open, to speed up testing. Until now a folder had one track: a build started from the originals (`*.before-racetrack`) of that island's files and the shared ones, so building a second island took the first one's scenes, cars and flight away (and, since only the files of the island being built were put back, left the first island's ground and decor bodies half there).

**The window.** The island box is now a list of ticks, two to a row, one for each island: Citadel Island's one tick builds both its tracks, the storm track in `CITADEL.ILE` and the town circuit in `CITABAU.ILE` (it first had two entries, the town circuit and the storm track, which built the same files and so unticked each other -- which read as though only one could be built; see "Fixed choices" below). It opens with the folder's tracks ticked, and builds exactly the ticked ones: a track built before on an island left unticked goes, and the question before the build says so. The story goes to Citadel Island; a plan file is one island's (Citadel Island's: its town circuit's), so it can be chosen only with one island ticked. The window scrolls when it would be taller than the screen.

**The build** (`RaceTrackService.Build` with a list of `TrackBuild`): every file any earlier build changed is put back from its original first, then the tracks are built one after another, each with `BuildFiles` as before. Each island's ground, decor bodies and outside scenes are its own; what they share is added to, not replaced:
- **The jump's flight.** There was one, generic animation 200, for the game. Each island's jump now has its own, 200 plus the island's number (a scene's island byte: the storm track 200, the Desert island 202, Mosquibees Island 207), and 12 more for Citadel Island's town circuit, so a town circuit with a jump no longer shares the storm track's flight either (the old "one flight, made for CITADEL's" warning). The engine's race-track mode flies 200 to 223 at the car's speed (`RACEMOD.CPP` `RACE_JUMP_ANIM_FIRST..LAST`; since 2026-10-01 200 to 295, 24 more for each jump after a track's first: "the lava lake" below).
- **The cars.** Baldino's car and the 58 character cars go into `BODY.HQR` and `RESS.HQR` once for all the tracks of a build (`BuildSession`); the scenes name them by their body number in the racer's entity, not the file's entry, so every island finds them.
- **The actors copied from other islands' scenes** -- the retail racer (Desert scene 57), the biker (Citadel Island's outside scenes) and the buggy (Desert scene 67) -- are taken from the original `SCENE.HQR`: a track built before in the same build has changed those scenes (`RaceTrackScenes.Apply`, `SceneStore` on another file). So is the Desert island's texture page the road tiles are copied from (`RaceTrackTextures.Import`).
- **RACETRACK.JSON** has the first track as its own record, as before, and the others under `Others`, each a record of its own. A folder built by an older version (one track, no `Others`) reads as before.

**Play** (`MainWindow.Play.cs`, `RaceTrackService.RaceFor`) races the track of the island file on screen -- `CITADEL.ILE` the storm track, `CITABAU.ILE` the town circuit, `CELEBRAT.ILE` or `CELEBRA2.ILE` Celebration Island's -- else, for an inside scene, the track of its island (Citadel Island's as its entry was built), else the first track built. It writes the engine's car file for that track only (its start line, checkpoints, opponents, raised road, weather, statue) and starts on its grid. The Play button says which: "▶ Race: Mosquibees Island", with the start scene in its tooltip. The scene editor's own Play races the track of its scene's island (`RaceForScene`), and the race car window shows the weather of the track about to be raced.

**Verified:**
- All five islands' tracks built together on the command line (`buildtogether`) in 20 s, and from the window (five ticked, and three): each island's ground and decor files, and its scenes, byte for byte the same as its own track built alone; no other scene of `SCENE.HQR` touched.
- Each island built alone, before and after: byte-identical for the Desert island, Citadel Island (both files), Celebration Island and the Elevator Platform; Mosquibees Island differs only where its jump's animation number is (207 in the hero's track script and the buggy entity's record).
- In the engine, from the joint build: Mosquibees Island's lap 47.81 s with its jump flown (animation 207), the storm track 28.29 s with its jump (200), Celebration Island 41.82 s, the Elevator Platform 34.02 s -- each as its own build gives.
- In the app on the joint build: the Play button named the right track for each of `MOSQUIBE`, `ASCENCE`, `CITADEL`, `CITABAU`, `CELEBRAT` and `DESERT`, falling back to the first track on an island without one; Play started Mosquibees Island's grid (scene 102), the Elevator Platform's (120) and the town circuit's in fine weather with its three opponents (49), one after another without building again.

### Fixed choices: one tick for Citadel Island, the Desert island's jump, the town circuit's bridge (2026-09-30)

The user found that Citadel Island's two tracks couldn't be ticked together (ticking one unticked the other), and asked for the choices that are always made the same way to go: the Desert island's lap always jumps where it crosses itself, the town circuit always has a bridge, the Desert island's leftovers of the retail track are always cleared and every track is always drawn on its island's holomap picture.
- Both Citadel tracks were in fact always built together (the island's two files share its scenes); the window now lists the island once, "Citadel Island: the storm track and the town circuit" (the town circuit's entry underneath: the one Play races when neither of the island's files is open, the one with the opponents and the story). A folder built with the old storm-track entry opens with it ticked.
- The crossing style is gone from the window: it is the island's own (`RaceTrackIsland.Crossing`: the Desert island's `Jump`, Citadel Island's `Bridge` for its town circuit; the other tracks' plans draw their own crossings), and `RaceTrackOptions.For(island)` gives it -- with the retail track's clearing on the Desert island -- to the window's builds and the command line's alike (`RT_CROSSING` still overrides it there, for tests).
- "Clear what's left of the original race track" and "Draw the track on the island's holomap picture" are gone too: always done.
- After a build (or putting the files back) the editor now reloads whichever island file it has open if the build changed it, and forgets every scene's cached scripts (not only the Desert island's, as when there was one track), keeping any with unsaved edits.

Checked: each other island built alone byte-identical to before; the Desert island's default build is byte-identical to the previous jump build (`RT_CROSSING=Jump`); built from the window (the Desert island and Citadel Island ticked) the same. In the engine the test pilot drives the Desert island's jump lap whole: its qualifying lap 132.51 s through all eight checkpoints, the jump flown with its own animation (202) at the car's speed (x0.86) and landed at 34 km/h.

Found on the way: on the Desert island's bridge layout (no longer built from the window) the test pilot stopped for good near the south edge of cube (9, 10) after the second checkpoint; the jump layout re-shapes the lap round the crossing, and it doesn't happen there.

The command line: `buildtogether <pristine> <game> <island>...` builds several tracks as the window does (`RT_LOG=1` prints the log), and `racecarfile` takes `RT_RACE=<island file>` for the car file Play writes with that island open.

## Celebration Island: the lava lake (2026-10-01)

*Replaced later the same day by a lap round the whole island: see "The lava lake, round the whole island" below.*

![the lap over the island, and the island's holomap picture as built](racetrack/build/celebrat_plan.png)

The user asked for a track on `CELEBRAT.ILE` -- Celebration Island before the statue rises -- "perhaps a track with a few jumps", that feels different from the statue's lap but is still fun to drive. The statue's lap is long (429 cells, 42 s), all of it a raised road winding up round the statue; this one is short and on the ground, and its jumps go over lava.

**The island.** One cube, (7,7): a mesa whose top is a plateau, 5,700-6,500 high and about 30 cells across, round a **lake of lava level with it** (5,700-5,800), with the temple on its west edge and lava channels pouring off its north side down to the sea. The sides drop 1,000-2,000 a cell, so the lap stays on the top: there is room for about 100 cells of road.

**The lap** (`tools/RaceTrackPlan/celebrat_design.py`), 92 cells, 10-11 s for the race car driven by the test pilot: **a figure of eight**. A loop over the north -- the plateau's north-west block, a causeway over the mouth of the north channel (where it leaves the lake, still nearly level with it), the tongue of rock east of it -- and a loop over the south, the ruins' terrace; between them **two causeways across the lake that cross in its middle at 87 degrees, and both jump there**: each has a ramp up to a lip 4.5 cells before the crossing and a ramp down from 4.5 cells after it, so the two leaps cross over one crater of lava, its four ramp mouths round it. Each flight is 15.1 cells (the retail flight x0.86, the first one shorter than the retail one: `jumpMinScale`), with ramps of 6 cells. The road is the statue lap's width (5.5 cells of asphalt). The start line is on the north side just before the channel, the grid behind it round the north-west corner, and the other cars wait for the qualifying lap in the ruins south of the temple. Every corner is on the plateau; the temple is 1.5 cells clear of the curbs.

**The road keeps within 1,150 of the ground under its middle.** Further, the builder walls a planned road (blocking rock at its edges, `PlannedProfile`), and the walls spread 4 cells either way: on the first version the north side crossed the channel on a bridge 2,000 over its lava, and a small knoll and a mound needed cuts of 1,260, and the car hit the walls on the inside of the corners next to them and stopped dead. Now the north side crosses the channel where it is still nearly level with the lake (no bridge), and the road rises over the knoll and the mound instead of cutting them down (`WALL_FREE`): no cell of the lap is walled.

**The plan** (`docs/racetrack/celebrat_track_plan.json`) adds `gapJumps` (several gap jumps, each [take-off lip, landing lip] as `gapJump`'s), `jumpRampLength`, `jumpLandingLength` and `jumpMinScale`.

**What the builder does** (`Terrain/RaceTrackBuilder.cs`, `RaceTrackScenes.cs`, `RaceTrackService.cs`):
- **Several jumps a lap** (up to four), each with its own zone number (40, 41...), its own controller actor and its own pair of labels in the hero's track script (90/91, 92/93...) and **its own flight**: 200 plus the island's number, 12 more for an island's other file, and **24 more for each jump after a track's first** -- this one's are 217 and 241. The engine flies 200-295 at the car's speed.
- **A crossing inside a jump's gap needs nothing over it**: the cars fly over it (no "the roads meet there" warning), and where both roads jump the checkpoints keep off it through the jumps' own margins (4 cells either side of a flight on a lap this short, where 20 left room for none).
- **The opponents' line over a flight is the flight's arc only on the stretch of lap the flight is over** (`JumpInfo.S0`, `S1`). It used to be every point near the flight's line, and on the Desert island's lap that took in the other road where it passes *under* the jump: the opponents driving there were given the arc's height, about 2,000 up. Fixed for the Desert too.
- **The grid follows the lap round a bend** behind the start line (`RaceTrackScenes.Behind`), where it used to stand in a straight line back from it. On this lap the line is just out of a corner, and the back of the grid stood off the plateau, 3,000 down; on Mosquibees Island's lap the last spot was 4.3 cells off the road, on the town circuit's 2.3, on the Elevator Platform's 1.9 (at the rail) -- all now on the road. A straight grid is as before.
- **The start line stops short of another part of the lap** that would cross its ends (a car on the other road crossed it there first); a raised road's other levels don't count.
- **A bridge's railings stop at the deck** at an end where the road turns off the bridge's line, instead of running 4 cells on into the corner.
- **Its own scene: 223**, a copy of the original scene 95 that the build adds to `SCENE.HQR` (222 left empty: it is the holomap position of the story's arrow), with scene 95's place on the holomap copied to 223's (`AddScene`, `RaceTrackIsland.CopiesScene`). The island's one outside scene carries the statue's track, so both tracks can be built into one folder. The engine loads `CELEBRAT` for it in the race-track mode whatever the story has reached (the car file's `statue=0`; `statue=1` still means the statue's file, and without it the game decides: `RaceMod_StatueShown`), and the track is drawn on CELEBRAT's holomap picture (entry 28).
- The texture page has no spare space, so as on the statue's island the road is painted with the island's own pixels: dark rock for the asphalt, its palest pixel -- a pale lilac -- for the white curbs.

**Back on the road** (the engine, every track's race-track mode): a car that goes into the lava or the sea used to burn or drown Twinsen, and the game started again from his last save -- on this lap, after a leap that fell short. Now, while he races, the car is put back on the road instead: on the first opponent's line, two cells behind the last place it was on the ground beside it, facing the way the lap runs, stopped, with "Back on the road" on the display (`RaceMod_Rescue`, from `GereCodesJeu`; the lap's count doesn't see the move). The car file's `rescue=0` turns it off; a track with no opponents has no line, and there the game's own death stands.

**The opponents' qualifying times** were their lap at their skill, 3 s either way at random -- more than a perfect lap's worth of luck on a 10-second lap. The spread is now a tenth of their lap, if that is less (`RACE_QUALIFY_SHARE`; laps over 30 s are as before).

![in the engine](racetrack/build/celebrat_engine.png)
![the race track window, and Play with CELEBRAT open](racetrack/build/celebrat_app.png)

**Verified**, on copies of the game:
- The test pilot: the qualifying lap through all 6 checkpoints in 10.19 s, the grid, GO, race laps of 10.2-11.3 s, both leaps every lap (20 flights in a row: 14-15 cells along the ground, peaking 1,800 over the take-off), the three opponents lapping in 10.5-10.9 s.
- Dropped into the crater: back on the road two cells behind, then the leap and the lap finished.
- Both Celebration Island tracks built together, on the command line and from the window: each car file names its own scene and island file, the statue's lap 41.82 s and the lava lake's 10.19 s from the one folder. In the app, `CELEBRAT.ILE` open: "Race: Celebration Island, lava lake", scene 223; `CELEBRA2.ILE`: the statue's lap, scene 95. Play started 223 on its grid.
- The other islands built alone against the last commit's code: every island and decor file byte-identical; their records differ only by the fixes above (the Desert's opponents' line under its jump; the grids of Mosquibees Island, the town circuit and the Elevator Platform, and the Desert's by a quarter of a cell at most). The suites pass.

**Known limitations**: a short lap -- the plateau holds about 100 cells of road. Two cars in the two leaps at the same moment can meet over the crater (the rescue puts one back if it falls). The white curbs are pale lilac.

## Walls, mines, checkpoints, Sendell's Well and a loop (2026-10-01)

**Mosquibees Island: a wall you could drive up.** Under the bridge, the end of the bridge's deck stands on a face of ground about 5,000 high that the shaping makes nearly vertical. The planned laps' painting left it as the island's own rock, which the engine doesn't block, so a car could climb it. Now, on a planned lap, sand, hatching and rock whose cell rises more than `SteepVerge` (300) become the blocking wall (`PaintRoad`). A scan of the built island for steep triangles that don't block finds none the build made (the 49 left are the original island's own, away from the road).

**Mosquibees Island: land mines round the jump.** To the right of the jump the ground let a car go round it. The plan now has `mines` (cells from its origin): six, in two rows across that way round. Each is a copy of the retail Desert island's mine (scene 66, actor 7: entity 16, body 30) with a track point under it and a life script of its own: when the car comes within 900 it goes off (`impact_point(P, 15)`) and is gone (`RaceTrackScenes.AddMine`). In the engine's race-track mode a hit on the race car (`RaceMod_CarHit`, from `HitObj`) **stops the car dead and holds it for 600 ms**, and costs Twinsen no life: in the blast the car takes a dozen hits, and one pass through the field took 70 of his 100 life points. Taking that way round now costs about 3 s.

![the way round the jump, mined](racetrack/build/mosquibe_mines.png)

**Checkpoints, generously zoned.** The town circuit had 8 checkpoints on a lap of 1,044 cells, and none in the 120-cell hairpin loop under the bridge, so a car could skip both long sections there. Now:
- **One line about every 60 cells**, 8 at the least and 24 at the most (`CheckpointSpacing`, `MaxCheckpoints`): the town circuit has 17, the Desert 23, the storm track 8, the others 8 (the lava lake 4: its two leaps leave little room). The town's lines 14 and 15 stand at the hairpin ends of the two long sections under the bridge, so cutting across between them misses a line.
- **Each line reaches 6 cells past the verge** on each side (`CheckpointReach`), as far as its cube and the rest of the lap allow, and stands where the road is straightest near its place (a cell of bend counts as 10 of distance).
- **Each side reaches past the verge, or as far as another part of the lap allows.** A side cut off by another part of the lap must still reach past the curb; a side cut off by the cube's edge must reach past the verge. A line that can't is passed over, and where no place near its own works, one up to twice as far away is taken. Each line comes after the one before it. The lava lake's first loop had a line that stopped at the curb on its inner side, and the test pilot cutting the loop drove round its end.
- **The engine counts a line 1.5 cells past either end** for a car crossing it the lap's way (`RACE_CHECKPOINT_SLACK`, 768); crossed backwards it counts only along its own length, because past its ends the road comes back the other way.
- **Every line has its height** (the engine counts it only for a car within 1,200 of it), on every lap, not only a raised road's: a line under a bridge isn't crossed from the deck.

![the town circuit's 17 checkpoints](racetrack/build/town_checkpoints.png)

**Sendell's Well in the race track window.** The island made yesterday (`docs/LBA2_SENDELL_WELL.md`) is now one of the islands the window lists (`RaceTrackIsland.Sendell`). The build makes it every time, from the originals (`Terrain/SendellWell.cs`, which the `sendell build` test command now uses too): `SENDELL.ILE` and `SENDELL.OBL`, island 1's sky and palette in `RESS.HQR`, its holomap picture and camera, and **scene 224** (222 is the story's holomap arrow and 223 the lava lake's scene), placed on the holomap at island 1's place on the globe. These files have no originals, so the backups leave them out (`RaceTrackIsland.Created`, `KeptFiles`). A build without the island deletes them, and so does putting the folder back. The main window now refreshes its island and scene lists after a build or a restore, so `SENDELL.ILE` appears and disappears without restarting.

The lap (`tools/RaceTrackPlan/sendell_design.py`, `docs/racetrack/sendell_track_plan.json`) runs round the well, clockwise: along the plateau's north edge, down the east slope, a straight on the south beach (the start, heading west), and back up the west slope through an S. It is 103 cells long and climbs and drops 1,200 at up to 9 %, on a road a little narrower than the retail one. The other cars wait for the qualifying lap on the plateau round the rim. The start was first on a bend, where the racing line cut into the gantry's inner leg and the test pilot stuck there; it is now on the straight.

![the lap over the island](racetrack/build/sendell_plan.png)
![in the engine: the start, the qualifying lap round the well, the grid](racetrack/build/sendell_engine.png)
![Play in the app](racetrack/build/sendell_app.png)

**Can Twinsen drive a loop?** As a scripted flight, yes. A jump's flight is a generic animation of the car (`RaceTrackJumpAnim`). Its slot 0 can carry a **master rotation**: bit 0 of its type adds the keyframe's Alpha (pitch) to the car's own (`LIB386/ANIM/INTERDEP.CPP`), and the step is turned by the car's whole orientation, pitch too. Bit 1 switches gravity off. So keyframes that pitch the car steadily while stepping it forward along its own nose carry it round a vertical circle. `ScriptRoundTrip loopanim <game> <generic> <radius> <drift> <keyframes> <ms> <sign>` builds one from the retail flight's pose and gives it to the buggy as a jump's flight. On a copy of the lava lake (flight 241: radius 3 cells, 15 cells forward, 32 keyframes of 90 ms, sign -1), the car took off, went over the top upside down (pitch 2,048 of 4,096, 2,800 above the take-off), came down 15 cells on and raced on. With sign +1 the pitch turns the wrong way: the car points down and the engine's ground check ends the flight. A loop section of a track would also need:
- **The ring itself**: a road body bent into a vertical circle (built like the raised road's pieces), with no collision box of its own, since the car inside it would hit it.
- **Landing level**: the flight ends 30/4,096 short of level and the car stays pitched that much. The engine should set Alpha back to 0 when a master-rotation flight ends or is cut short.
- **The flight's speed**: the jump clock judged this flight's own speed as 23 km/h from its steps and played it 1.32 times faster. A loop's own speed should be measured along its circle.
- **The opponents**: they follow their lines without pitch, so they would go round the loop level. The engine would have to pitch an opponent's car from its line's slope.

![the loop: over the top upside down, vertical, coming down, landed](racetrack/build/loop_test.png)

**Verified**, on copies of the game: all seven earlier tracks built into one folder with the new builder, then the test pilot on each: no checkpoint missed anywhere. Lava lake 4 lines, laps 10.2-11.4 s; storm track 8 lines, 28.3 s; Mosquibees 47.8 s; Celebration 41.8 s; Elevator 34-35 s; town circuit 17 lines, 99.0-99.1 s; the Desert stalls at checkpoint 7 as before these changes. Sendell's Well: 8.9 s laps, no checkpoint missed. From the app on a sandbox folder: built Sendell's Well alone, picked `SENDELL.ILE`, played its race (muted), put the folder back (the island's files went and the window moved to another island), and built it again. selftest, commenttests and areatests pass; scenetests show the same 49 failures (of 3.4 million checks) as the last commit's code.

## The old moon: two vertical loops (2026-10-01)

The user asked for two vertical loops on `MOON.ILE` (which no version of the game loads): one with an upside-down scripted jump at its top, and one that is just a natural vertical loop.

**The island.** `MOON.ILE`/`MOON.OBL` (14 February 1997) are an older copy of the Emerald Moon, island 3 (`EMERAUDE.ILE`): four cubes, (7,7)-(8,8), the same heights all but a little, different texturing, and the Zeelich moon base on a crater floor about 300 high. The race-track mode draws island 3 from it instead (`island_file=3 moon` in the car file: `RaceMod_IslandFile`, used by `InitGrilleExt`), with island 3's sky and palette (RESS 30). The track races in scenes of its own, **225-228**, copies of the Emerald Moon's four outside scenes 74-77 (`RaceTrackIsland.CopiesScenes`, `RaceTrackScenes.AddScene`). Their cube changes to one another point at the copies, and Twinsen's own life script in them is emptied: the Emerald Moon's scenes put him in his space suit (behaviour 10 or 11) whenever he comes in, which took him out of the car at the first cube edge. The original scenes are left alone. There are no opponents, because their cars don't go round loops, and nothing is drawn on the holomap, because island 3's picture is the Emerald Moon's. A track with no opponents has no grid, so Play no longer calls it outdated (`IsOutdated`). At the start the race-track mode now turns Twinsen to face the car itself and brings him within the action key's reach (`FaceTheCar`); before, it used the grid's pole spot.

**The lap** (`tools/RaceTrackPlan/moon_design.py`, `docs/racetrack/moon_track_plan.json`): an oval of 174 cells on the crater floor round the base, anticlockwise on the map, with the start line on its east side. Both loops are on the south side, its "loop alley": first the jump loop, then the whole loop after a 30-cell run-up. Each ring is within one cube (the engine carries the car round in that cube's coordinates), on road kept level for its radius plus 6 cells either side of its foot. The builder clears the base's buildings where the road runs through them.

![the lap over the crater](racetrack/build/moon_plan.png)

**The plan's `loops`**: each is [the point at the ring's foot, its radius (cells, to its road surface), the shift across (cells), the gap at its top (degrees, 0 for none)]. Here both are 4 cells, 3 across; the jump loop has a gap of 70 degrees. The builder (`RaceTrackBuilder`: `LoopInfo`, `PlaceLoops`, `RaceTrackLoopBody.Ring`):
- **The ring**: a band of road bent into a vertical circle in the plane of the lap's way, wound 3 cells across over the turn so the car comes out beside where it went in and drives on past the ring's foot. Grey road on the inside, red and white rails, green outside, two green legs at its widest. It is a decor body of its own, in world axes from its foot, and its box touches nothing (top far under it), because the engine carries the car round. The jump loop's ring stops short of its top for 70 degrees, its ends capped.
- **No checkpoint lines** within the ring's radius plus 6 cells of a loop: the car is in the air there.
- **The racing line**, which only the test pilot drives on this lap, runs into each ring on its own lane (left of the middle by half the shift) and away from it on the other.
- **RACETRACK.JSON's `Loops`**, written to the car file as `loop=<cube x> <cube z> <x> <y> <z> <dir x> <dir z> <radius> <shift> <gap>`.

**The engine** (`RACEMOD.CPP RaceMod_Loop`, called by `DoAnimExt` for the hero after his car's move). A car crossing a ring's foot the lap's way (anywhere across the road; where it went in across fades into the ring's lane over the first quarter) is carried round the ring instead of over the ground. Nothing else moves it while it is in the loop: no ground, no collisions, no safe place recorded.
- **The whole loop is physics.** The speed changes with the climb and the drop. The car stays on while v²/R + g·cos θ is positive (`RACE_LOOP_G` 1,600 units/s²), which over the top takes about 28 km/h with nothing pushing it, 21 km/h with the throttle held. The throttle pushes it on with `RACE_LOOP_PUSH` (600) as far as the ring presses back on the wheels, and the brakes slow it as far too. Too slow, it falls off: thrown along the ring and still turning, it lands, stands level, stops, and is held for 0.9 s ("Fell off the loop"). Below the quarter, where nothing throws it off, it rolls back down and out backwards.
- **The jump loop is scripted.** The car is carried round at the speed it came in with (2,500 units/s at the least) and never falls. Over the gap it leaves the ring upside down and passes 300 units inside the ring's line at the gap's middle (`RACE_LOOP_DIP`), landing on the far side.
- **The camera**: while the car is in a loop, the camera that follows it looks from the ring's side, 3.2 radii out on the side the car goes in on, at the ring's middle drawn a little towards the car. From behind, the ring's own back section, coming down behind its foot, was between the camera and the car. The race-track mode now switches the follow camera on for a lap with loops, as it does for a raised road.
- **The car stands level again** whenever neither a loop nor a flight's master rotation pitches it. The earlier loop test (a jump flight with a master rotation, `ScriptRoundTrip loopanim`) left the car 30/4,096 short of level.

![the rings on the road](racetrack/build/moon_rings.png)
![from the side: the jump loop (top row: up, over the gap upside down, down) and the whole loop (bottom row: in, up, upside down at the top)](racetrack/build/moon_loops.png)
![Play in the app](racetrack/build/moon_app.png)

**Verified**, on copies of the game: the test pilot at full speed, laps of 22.1 s. Every lap, the jump loop: in at 4,836 units/s, over the gap, round. Every lap, the whole loop: in at 4,836, round at about 5,050, its speed dropping to 29 km/h over the top. With the car's top speed cut to 2,700 (19 km/h): the jump loop is carried round, and on the whole loop the car falls off at 120 degrees, lands, and rolls back out of each slow try after it. From the app, on a sandbox folder: built, `MOON.ILE` picked ("Race: The old moon", scene 228), Play starts on the start line with Twinsen at the car. All seven earlier tracks raced again with the new engine: the same laps, no checkpoint missed.

**Known limitations**: no opponents on this lap. A car that falls off the whole loop lands near its foot and has to back up for a run-up. The rings are seen side-on only while the car is in them.

**Correction (later on 2026-10-01): the car rolled instead of pitching.** The engine turns a body -- and an animation's step -- by `M = M(Alpha) M(Gamma) M(Beta)` (`LIB386/3D/IMATSTDF.CPP`): the heading first, then Gamma about the world's Z axis and Alpha about its X axis. So Alpha alone pitches only a car heading along Z; both moon loops run along +X, where Alpha rolled the car round its own long axis (in the pictures above, the rear wheels stacked one over the other at the ring's side). `RACEMOD.CPP CarPitch` now sets all three angles for a pitch `phi` in the car's own frame at heading `beta`: Alpha = atan2(cos b sin p, cos p), Beta = atan2(sin b cos p, cos b), Gamma = asin(-sin b sin p). When neither a loop nor a flight's master rotation turns the car, Gamma is put back to 0 along with Alpha. Nothing in the built files changes; the engine does it.

![before and after: the car round the whole loop](racetrack/build/moon_loop_pitch.png)

## The lava lake, round the whole island (2026-10-01)

*Reshaped later the same day from the user's sketch: see "The lava lake, third version" below. The drop, the drop camera and the cutting stay in the builder and engine for any track.*

![the lap on the built island](racetrack/build/lava2_map.png)

The first lava lake lap (above) was far too small: about 70 cells on the crater's plateau. The user asked for as much of the island's footprint as possible, perhaps more jumps, the start and finish where the statue track's are, and height used in a way unlike the statue track's helix. The new lap (`tools/RaceTrackPlan/celebrat_design.py`, `docs/racetrack/celebrat_track_plan.json`) is 247 cells, clockwise, raised road everywhere except the dock:

- **The dock** (420): the start line at (7.2, 13.5) heading north, the pits beside it, the statue track's area.
- **The north shore** (900, along the sea at the foot of the crater's cliffs): jump 1, a 7-cell gap over a lava channel.
- **The east coast**: a long climb from 900 to 6,300 (19 % at the steepest) round the north-east corner and down the coast to the south rim.
- **The south rim** at 6,300, then north across the middle of the lava lake on a causeway: jump 2, an 8-cell gap over the lava.
- **The north rim**, west, then south behind the temple: jump 3, a 7-cell gap, then a hairpin at the south-west corner.
- **The drop** (jump 4): north from the hairpin, the road ends at a lip on the mesa's west edge at 6,300 and the car flies 18.6 cells down the hillside onto the dock, 6,112 below.

![a qualifying lap](racetrack/build/lava2_tour.png)
![jumps 1-3](racetrack/build/lava2_jumps.png)
![the drop](racetrack/build/lava2_drop.png)

**Gaps in a raised road.** A gap jump on a raised road leaves a hole in the deck between its lips. `report.Raised` points in the gap have no width (Half 0); the engine skips any segment that touches one (`RaisedAt`, `RaisedBetween`), and no longer rounds off a segment's end where the road stops -- at its last point or a lip (`RaisedOpenEnd`; a segment's round end held the car up half a road's width past the drop's lip). The pieces and piers are laid along each stretch between gaps.

**The drop.** A gap jump whose landing lip is more than 600 below its take-off lip (`DropFrom`) is a drop: its landing ramp is 200 high, and its flight is drawn instead of scaled from the retail one (`RaceTrackJumpAnim.Drop`, `InstallDrop`). Its arc is `DropAt(u, drop) = 4·1200·u(1-u) - drop·s((u-0.2)/0.8)` (s the smoothstep): a hop as steep as the retail flight's first climb, clear of the take-off lip, then a smooth dive. The car's pitch follows the arc, eased in from level and back to it at both ends (53° nose down at the most), set in each keyframe as the change of all three angles (`Pitched`, the same decomposition as `CarPitch`) with the step in the car's own pitched frame. The opponents' lines fly the same arc. The engine's estimate of a flight's own speed (`FlightSpeed`, which sets the rate the flight is flown at) now turns each step by the whole of M, as the engine moves the car.

**The camera for the drop.** The camera that follows the car went down the hillside behind it and into the hill, and the landing was never seen. The builder now picks a place beside a drop's flight (`PlaceJumpCameras`: either side, 12-18 cells out, from the landing's height to a little over the take-off's) that sees the most of the car's places along the flight and past the landing -- each line clear of the ground and of the raised road's slabs and rails -- and writes it to the car file (`jumpcam=<anim> <cube x> <cube z> <x> <y> <z>`). While the hero flies that animation, and 1.5 s after he lands, the follow camera's eye moves there and looks at the car (`JumpCamera`). For the lava lake: 12 cells out over the sea to the west, 7,814 up, seeing 14 of its 15 places.

**Cutting under the deck.** The lap runs along cliffs and the crater's rim, where the rock beside the road came above the deck: the test pilot, cutting a corner on the north rim, stopped dead against it. With the plan's `raisedCut` (only this lap's), the builder lowers the ground to 450 under the deck out to a cell past its edge, then banks back up to the ground as it is (`CutUnderRaised`): 279 vertices, 2,330 at the most. Never within 8 cells of where the deck rises from the ground road, nor near the ground road itself. (Tried on every raised road first, it cut a 300-deep hole in the statue track's dock.)

**Test tools.** `--exec-at` takes 128 commands a run (16 before), so one run can screenshot a whole lap; `teleport <x> <y> <z> [beta [alpha [gamma]]]` sets the hero's angles too.

**Verified**, on copies of the game: the test pilot round the lap, qualifying 26.1-26.3 s, race laps 26.1-29.7 s with the three opponents (their qualifying 24.7-28.8 s), no checkpoint missed, no rescue. The drop: up to 7,440 off the lip, down to 431 on the dock's landing ramp and away at speed. All seven tracks of the joint build raced again with the new engine and builder: every lap completed, no checkpoint missed, no rescue (the Desert island's stall at checkpoint 7 is the known one). The old moon's loops re-shot: the car now pitches round both.

**Known limitations**: the first moment of the drop is hidden from its camera by the take-off ramp, and the camera swings out to its place over 12 frames rather than cutting. The cuttings under the deck are plain rock.

## The lava lake, third version: the user's sketch (2026-10-01)

![the user's sketch over the second version, and the new lap turned the same way](racetrack/build/lava3_sketch.png)

The user liked the second version's basics, but its last corner was too tight and the drop would be better as a ramp. Their sketch, drawn over the editor's minimap (which turns with the 3D view: up was east there), kept the start, made the third and fourth corners turn back more on themselves into the lake's jump, shifted the inner part of the lap over, and opened up the last turn; the jumps keep their lengths. The design (`tools/RaceTrackPlan/celebrat_design.py`) now draws the lap as circles it turns round joined by the lines that touch them, so a turn can go round more than half a circle:

- **The hook**: from the south rim the lap turns 120 degrees back on itself onto a causeway north-east across the lava lake; jump 2 (8 cells) is in its middle, heading 30 degrees east of north.
- **The S**: off the causeway round to the west onto the north rim (z 16.7), and round its corner south beside the temple.
- **Jump 3** (7 cells) stays just west of the temple: its roofs are 11,140 high, nearly 5,000 over the road, so the sketched line across the temple can't be flown. The road there is the plateau's west edge.
- **The last turn**: a kink left, then round to the right 204 degrees on a radius of 5.8 (the hairpin was 3), over the south-west corner.
- **The ramp** instead of the drop: from the end of jump 3's landing round the last turn and north along the island's west shore (x 4.6: the island's edge must be 4.3 cells off, the verge and half a cell -- not the 6.5 the design script had assumed) onto the dock, 6,300 down to 420 over 55 cells, its grade eased in and out and steepest near its foot (28 %). The south-west hill is the mesa's west flank and stands across the whole dock's width; every way onto the dock from the south crosses it, so the ramp runs through a cutting there (`raisedCut`: 427 vertices, 3,300 at the most at the cutting's uphill edge): a rock face beside the road.
- **The grid** stands two abreast (`gridStep` 1.75, a new plan option; 3.5 elsewhere), so it ends at z 23.5 and the ramp comes down just behind it.

![the new lap](racetrack/build/lava3_map.png)
![a qualifying lap](racetrack/build/lava3_tour.png)
![the last turn and the ramp](racetrack/build/lava3_ramp.png)

**Verified**, on copies of the game: the test pilot, qualifying 27.0 s, a race lap 29.5 s with the three opponents (their qualifying 26.7-32.0 s), no checkpoint missed, no rescue; down the ramp at up to 43 km/h. Built with the other six tracks into one folder: theirs keep the 3.5 grid. Built and raced from the app on a sandbox folder.

### The start/finish area: ground under the deck's ends, and footings for the gantry (2026-10-01)

![the user's picture, and the fixes](racetrack/build/lava3_start_fixes.png)

The user found the ground showing through the road at the foot of the ramp (cars rode up over the rail there), a slit under the start of the north shore's raised road, and the start gantry's post standing in the sea.

- **The raised road's ends** (`FlushRaisedEnds`, part of `raisedCut`): the engine takes the higher of the deck and the ground under it, so where the hill's toe stood up through the deck's edge a car rode up onto it and over the rail. Under each end of the raised road, as far as the deck stays within 600 of the ground under its middle (and stops where it runs into the hill: that is the cutting's), the ground is now the deck's own surface, 50 under it (15 under showed through the asphalt at a distance), out to its rails; beside it, ground below is filled up towards it and ground above brought down to it out to a cell past the rail (the engine's floor reaches a fifth of a cell past it), then banked up. The cutting leaves that ground alone. Checked over the whole lap, across the deck and that fifth of a cell past each rail: no ground above the deck anywhere.
- **Gantry footings** (`gantryFootings`, a plan option, only this lap's): the dock is narrower than the start line's gantry, and both posts stood in the sea. Under a post over a hole the ground is raised to the gantry's foot out to 0.9 cells from it and eased down over 1.6 more -- never on the road or its verge -- and the sea's cells there are drawn as rock (29 vertices, 27 cells). Tried on every track first, it would also have put a rock mound by the Desert island's pit lane and drawn rock under the statue track's west post (the same hole as here): left to the plans that ask for it.

**Verified**: the test pilot, three race runs in this folder and one in the folder with all seven tracks, qualifying 27.0 s, race laps 29.5-30.2 s, no checkpoint missed; one rescue in one run (an opponent's bump on the north shore, far from these changes; the other three runs had none).

### Over the cliff at the foot of the ramp (2026-10-02)

The user found a place on the left of the ramp down to the start/finish line where the car went out of the track and died. The ramp's last 11 cells run along the island's west cliff (x 2: the ground falls from the deck's height to the sea), and there the deck lies on the ground (the flush ends above): the engine let go of the rail wherever the road was less than 300 over the ground under the car (`RaceMod_Rail`), so that a car can drive off the road where it comes down onto the ground. A car against the left rail there slid off the deck, over the cliff, into the sea, and was put back on the road ("back on the road: line point 251").

- **The engine** (`RACEMOD.CPP`, `RaceMod_Rail`): where the road lies on the ground, the rail now still holds on a side where the ground just past the floor's edge (`RAISED_BESIDE` 384 out) is 300 or more below the road; where it is about as high (the dock beside the ramp's right, any road ending on flat ground) the car drives off as before. Past the road's own first or last point it is always free (`RaisedPastEnd`: otherwise the segment before the open last one, clamped to its end, held a car at the ramp's end).
- **The test pilot** has a sideways offset (`autodrive 1 5 <cells>`: to the left of the line, x east and z south), to drive a lap against a rail. Against the left rail down the ramp (2.2 and 2.5 cells): the old engine slides off at x 2.8 and is rescued or stuck on the cliff; the new one stays on the deck (x 3.6 and over), rolls off the end onto the dock and finishes the lap (29.15 s). On the ordinary line nothing changed: lava 27.04 / 30.18-30.19 s, statue track 41.82 / 41.86 s, storm, mosquibees, elevator and town identical (desert's checkpoint-7 stall and sendell's crash are older).
- **Not changed, found on the way**: taken from the far right of the road (1.9 and 2.6 cells right of the line, either engine), the causeway jump over the lake (jump 2, line point 128) can fall short into the lava. (Fixed the same evening: below.)

### Jumps 2 and 3 from the right side (2026-10-02, later)

The user found the car going through the right side of jump 3 and dropping to the track below; jump 2 dropped a car on its right into the lava (found with the pilot). Two different faults:

- **Jump 3: the ramp-foot fix's own fault.** `RaisedPastEnd` (the place past the road's own last point, where the rail lets go) looked at any place within seven cells of the ramp's foot (6.33, 24.44) beyond it, at any height -- and the right side of jump 3's run-up (x 11-12, z 19.5-24.4) is within that, 6,000 higher. A car on that rail was let off the deck and fell onto the ramp's foot below. (Reproduced by placing the car on the rail there, `teleport`, and holding right: the release of that afternoon dropped it to y 428 at (10.6, 23.3); the engines before and after held it at x 11.80.) Now a place counts as past an end only beyond the end's point, within the road's width there, and level with it (`RAISED_LEVEL`).
- **Jump 2: the rail during the flight (older).** The flight starts in the trigger zone, two or three cells before the lip, and the car is still over the take-off deck. Past the last segment before the gap, `RaisedAt` measures the car against the segment before it, clamped at its end point; a car on the rail line is a little outside its limit there, and the rail put it back beside that point -- every frame. The flight went nowhere and the car dropped into the lava when it ended. On the middle of the road the car never touches the limit, which is why the pilot's ordinary laps never showed it. The rail now stays out of a jump's flight (`s_flying`), and every open end of the road -- each jump's lip as well as the road's own ends -- counts as an end for `RaisedPastEnd` (a car on the rail rolling off a lip whose jump didn't start then falls, as it should).
- **The triggers were checked too**: every place across the road the rail allows (1.8 cells either side of the middle) runs through at least a cell of each jump's trigger zone in the last four before its lip.

**Verified** (the lava lap, `rt_lava`): whole laps with the pilot 2.2 and 2.5 cells left and 1.9, 2.2, 2.6 and 3.0 cells right of its line: no rescue (before, 1.9 and 2.6 right fell at jump 2); the ramp's foot against the left rail still holds; the jump-3 rail test holds at x 11.80. Every track's ordinary pilot laps as before.

## The Emerald Moon: over the reactor (2026-10-02)

The user drew a lap for `EMERAUDE.ILE` (the Emerald Moon, island 3) over the editor's minimap:
- a jump on either side of the reactor (the large pink dots), the road there as wide as the top of the reactor, and wider coming into it;
- large, banked curves (red) into the jump and after it;
- a straight along the middle of the moon base's building, with a pit lane on its right;
- the vertical loops tested on the old moon (blue), each twice the road's width, the road coming out of a loop offset sideways from where it went in;
- two arrows for the way round.

Asked how the jumps and the straight should work, the user chose one jump right over the reactor, the pink dots being its take-off and landing, and a road on the building's roof.

**The island.**
- **The terrain.** Four cubes, (7,7)-(8,8), cells 448-576 both ways. A crater floor about 325 high is ringed by a rim 4,000-7,150 high.
- **The moon base** stands on the crater floor: a cross of tube-roofed buildings. The long arm runs north-south along x 57-64, its roof 4,538 high at the middle. The short arm runs east-west along z 56-64.
- **The reactor** is on the north rim (body 6): a plateau 6,695 high, then a cylinder 26 cells across up to 12,000, then a dish 17.3 cells across up to 18,716. Its middle is on the border between the cubes, at x 64, z 13.
- **The sketch** was fitted to the island by landmarks with a projective fit, because the minimap is tilted. Up on the minimap was east, right was south.

![the sketch and the lap](racetrack/build/emerald_plan.png)

**The lap** (`tools/RaceTrackPlan/emerald_design.py`, `docs/racetrack/emerald_track_plan.json`): 540 cells, all of it a raised road. Rounded corners join straight runs (every 0.5 cells), the way the sketch's arrows go:
- **The straight** runs south along the long arm's roof, 5,000 high. The race lanes' middle is on the roof's middle (x 60.5). One deck carries both the race lanes and a pit lane 3.5 cells wide on their right (west), with a white stripe between them (`pitStripe`). The start line and its gantry are on the straight.
- **The inner U**: a hairpin at the south end, then back north over the short arm, still at 5,000. Then east along z 42, climbing to the outer ring's 7,700 (grades eased in and out, 15 % at the most).
- **The rim**: south down the east rim through the first loop (whole), west along the south side through the second (a 70-degree gap at its top), and north up the west rim through the third (whole). The outer ring is at 7,700 because the west rim reaches 7,150.
- **The red curves** are a wide banked turn onto the reactor's line (z 13, radius 10) and, after the jump, a banked U-turn (radius 11), banked up to 0.32. Through both, the road widens to 8.5 cells from its middle to its rail, the width of the reactor's dish, and narrows again after the U-turn.
- **The way back**: west along z 35, past the reactor's south side, coming down to 5,000 and onto the straight.

**The loops**: each has a radius of 4. The deck is 6.1 cells from its middle to its rail for 10 cells either side of the foot, twice the road's width, and narrows back over 8 more. The ring's band is as wide as the road (the plan's loops gain a fifth number, the band's half width: 3.05). The band drifts 6.1 cells across, a whole road width, so the car goes in on the left half and comes out on the right. Each ring is within one cube.

**The jump: one leap over the whole reactor** (the plan's `arcJumps`: [ramp foot, lip, landing lip, hill foot]).
- **The ramp** curves up from the deck, 6 cells long, to 60 degrees at its lip: a circle's arc, from x 33 to the lip at x 39, 9,474 high.
- **The flight** is a parabola from the lip to the landing lip at x 90, with the lip's slope at both ends. It tops out at 20,780, 2,000 over the dish, clearing the reactor's top across the whole deck's width by 900 at least. The design script picks the gentlest ramp angle that clears it.
- **The landing hill** curves back down, 6 cells, to the deck at x 96.

The plan's heights from the ramp's foot to the hill's foot are that path. The road has no deck between the lips.

![the jump from the side](racetrack/build/emerald_jump_side.png)

No flight animation could fly this jump. An animation moves the hero in his cube's own units, and this flight crosses from cube (7,7) into (8,7) high over the reactor. So the race-track mode carries the car itself (`RACEMOD.CPP RaceArc`, called first by `RaceMod_Loop`):
- **Taking the car on.** A car that crosses the line across the road at the ramp's foot, going the lap's way and level with the deck, is taken onto the jump. It is carried along the plan's path at its speed along the ground (4,500 units/s at least), at the same distance across the road as it came in, and pitched with the path (`CarPitch`).
- **The cube change.** Where the path crosses into another cube, the engine changes the scene itself: `NewCube` is set from the car file's `cube_scene=` lines, along with the place in the new cube's units, `FlagChgCube` 1 and no re-seating. In the new scene the car is carried on. The builder makes no crossing zone at a carried jump's edge.
- **The landing.** The car is set down on the landing hill's foot, level, going as fast as it was carried, and its lap check forgets its last place.
- **The camera** flies beside the car, 18 cells out to the south, a little behind it and 1,800 over it, looking at the car with the reactor passing under it. It stays there half a second after the landing.
- **The rail** stays out of the carried flight.
- **The car file** gets `arcjump=<foot> <lip> <land> <land foot> [side]` (places in the raised road's file) and `cube_scene=<cube x> <cube z> <scene>`.

At full speed the jump takes about 6.6 s from the ramp's foot to the hill's foot.

![over the reactor](racetrack/build/emerald_jump.png)

**What else the builder does for it:**
- **Widths point by point** (the plan's `raisedHalfs`). They go to the engine's raised road file, the deck pieces (`RaceTrackRaisedBody.Tile` widens the asphalt and moves the curbs and rails out), the start gantry, the piers, the raised floor and decor clearing.
- **The engine looks further** for the road's points: by the widest half width in squares of four cells (`s_rbExtra`). The 8.5-cell road was wider than the one square either way that it searched.
- **The cube edges' crossing zones** stand at the raised road's own height, not the ground's. The straight crosses z 64 at 5,000, over a crater floor at 325.
- **Decor under the jump is left alone.** Under a carried jump's gap, nothing is cleared or cut down: the reactor stays as it is. The other decor bodies are kept (`keepBodies`). The egg-shaped building beside the west rim had its box cut down under the road (the road keeps clear of the building itself).
- **Checkpoints** keep 6 cells clear of a carried jump.

The track races in scenes of its own, **229-232**, copies of the moon's outside scenes 74-77 (Twinsen's own life script emptied, as on the old moon), with no opponents and nothing drawn on the holomap. Nine piers were left out where there was no clear ground under the road. On the straight, the deck lies 260 over the long arm's roof.

![loops](racetrack/build/emerald_loops.png)
![the straight and the red curves](racetrack/build/emerald_road.png)
![Play in the app](racetrack/build/emerald_app.png)

**Verified**:
- **Headless pilot** (`rt_emer`): a lap of 64.05 s, all 9 checkpoints, no rescue. All three loops round (in at 4,836 units/s). The jump: carried at 4,836, into scene 231 at the top (20,777 high), down on the hill's foot. The car's place, dumped every 25 ticks against the road, stays on the deck all the way round, banking included, apart from the loops.
- **Other tracks unchanged**: the eight other tracks built together are byte-identical to the last commit's build (38 files). The elevator and lava laps raced with the new engine qualify in the same times as before.
- **From the app**, on a sandbox folder: the Emerald Moon ticked and built in the race track window. With `EMERAUDE.ILE` open, the Play button reads "Race: The Emerald Moon". Play starts in scene 229 on the grid under the start gantry, muted.

**Limits**:
- The pit lane was only a lane: there were no opponents to wait in it. (Since 2026-10-03 they race it and wait there: below.)
- A car driven backwards up the landing hill would fall off its lip onto the reactor's plateau (not tried).

## Driving the loops, and opponents round them (2026-10-03)

The user asked for two changes. Opponent cars should be able to do the loops. The car should be driven upside down, instead of the top of a loop being scripted. Until now the engine carried the car along the middle of the ring's band, and the player had only the throttle and the brakes. On a ring with a gap, the car was carried round at the speed it came in with and over the gap on a fixed path, whatever that speed was. Neither moon track had opponents.

**The ride** (`RACEMOD.CPP LoopStep`, the same for the player's car and for every opponent's): the car is driven round the ring as it is on a road bent into a circle.
- **On the ring.** The speed changes with the climb and the drop. The throttle pushes the car on, as far as the ring presses back on its wheels; the brakes slow it as far. The steering turns the car across the band: up to 20 degrees, at the car's own steering rate. The rails keep its middle a car's half width inside the band. While steering, the car yaws on the ring's surface: `CarPose` adds a yaw about the car's own up axis to the heading and the pitch. A car that came in wide of its lane is pushed onto the band over the first part of the ring.
- **Off the ring.** A car that is too slow over the top leaves the ring and flies inside it, as a falling body. It comes down on the ring again further round and rides on with its speed along the ring; a landing into the ring at more than 2,400 units/s keeps only a third of that speed ("Hard landing"). If it falls through the middle onto the road at the ring's foot, it has fallen off: the car is held for 0.9 s ("Fell off the loop").
- **A ring with a gap** is now a real leap. At the gap's edge the car flies on as it was going, upside down, and lands on the far side. The far edge catches a car up to 500 over it ("Over the gap"). A car that is too fast sails over the far edge and falls outside the ring ("Overshot the loop"); it lands on the road or the deck, held for 0.9 s. A car that is a little too slow loses the ring just before the gap and comes down hard further round the far side, but still gets round.
- **Under a quarter turn**, a car that is too slow rolls back down and out backwards, as before.
- **The camera** stays beside the ring, as before.

**The speeds.** For each ring the engine works out, at load, the speeds at its foot that a car with the throttle held gets cleanly round at (`LoopWindow`): every entry speed is tried with the same step at the engine's 16 ms frame. Cleanly means on the ring all the way to a gap, and no hard landing.

| Ring (radius 4 cells) | Clean entry speeds | Falls off below | Overshoots above |
|---|---|---|---|
| Whole | 22 km/h and over | about 19 km/h | n/a |
| 70-degree gap at its top | 21–24 km/h (3,000–3,375 units/s) | about 19.5 km/h | 24 km/h |

A ring with a gap is taken at about third gear's speed. Coming up to one, 15–30 cells before its foot, the display says how fast to take it ("Jump loop: 22-23 km/h", rounded inwards). (Since 2026-10-04 a car that is too fast is slowed to it, and the display gives only the least: see "Oil slicks on the road; the open loop slows a car that is too fast".)

![a race through the jump loop](racetrack/build/loops_opponents.png)

**Steering, tested.** On the Emerald Moon's first loop the test pilot came in on the right rail of the band. Holding left for 40 ticks over the top took the car from that rail (+922 across the band's middle) to the band's middle, its heading yawed 20 degrees while steering. Holding right against the rail changed nothing.

**The road before a ring** (`RaceMod_Loop`): its right half closes over the RACE_LOOP_FUNNEL (8) cells before the ring, down to the entry lane. The ring's way down stands there, and a car on the right half used to drive past under it and skip the loop.

**The opponents** are driven round the same way (`RaceMod_Frame`):
- **Entry.** Each opponent's line has each loop's foot on it (`OpponentLoops`). When an opponent crosses the foot, its line waits there while it goes round. It takes the ring at a speed inside the clean ones (`LoopEntry`), with the throttle held, and its car is placed and pitched by the ride.
- **No falls.** An opponent never falls off: too slow over the top, it is held on.
- **Planned speeds.** Their speeds are planned to brake to the middle of a gap ring's speeds (`LoopCaps`). The time round the rings is added to their planned laps, which set their qualifying times.
- **The reactor jump.** Over the Emerald Moon's reactor jump, an opponent follows its line's own arc (the plan's heights), pitched with its slope. It disappears from the player's scene as it crosses into the next cube.

**Opponents in other cubes.** The engine keeps every object of a scene within its cube (`DoAnimExt`). An opponent's car standing in the next cube was therefore pinned to the cube's edge, an invisible wall. The old moon's grid is just over the border from the lap's crossing, and the player's car was stopped there, jittering over a checkpoint line. An opponent outside the player's cube is now kept out of sight and out of the way. This was also the Desert island test pilot's old stall at checkpoint 7: its qualifying lap now completes (132.6 s).

**The tracks.**
- **The old moon:** opponents now race it. They wait beside the start line's straight, inside the oval on the crater floor (the plan's new `pitSpots`). On the grid behind the line they stood on the lap's last bend, in the player's way.
- **The Emerald Moon:** opponents now race it too, waiting in the pit lane beside the start while the player qualifies (the plan's `pitSpots`). The start line moved to z 62 so that its grid stands on the level part of the straight.
- **The loop record** (`RACETRACK.JSON` `Loops`, the car file's `loop=`) gains an eleventh number, the band's half width, for the steering.
- **The test pilot** holds the throttle round a ring. Coming up to a ring with a gap, it keeps close to the middle of its speeds (coasting a little over them, braking well over them).

![the Emerald Moon in the app: the opponents in the pit lane](racetrack/build/loops_emerald_pits.png)

**Verified**:
- **The old moon** (`rt_moon`), the pilot and three opponents:
  - qualifying 25.34 s, the grid, the count-down;
  - race laps 25.53 and 25.62 s, the opponents' 27.1–28.3 s;
  - every lap, all four cars round both rings, the opponents taking the jump loop at 22 km/h.
  - The pilot leapt the gap cleanly ("Over the gap") or a little slow (off at 145 degrees, down further round); it never fell.
- **The Emerald Moon** (`rt_emer`):
  - qualifying 67.77 s; the opponents waited in the pit lane, then the grid;
  - race laps 67.13 and 67.76 s, the opponents' 68.9–71.5 s;
  - all three loops every lap by every car;
  - the opponents over the reactor: up from 7,700 to 20,725, out of the player's scene at the cubes' border.
- **The other tracks:** the seven others build byte-identical (38 files). With the new engine their solo qualifying laps are identical (lava 27.04, storm 28.30, Mosquibees 47.80, statue 41.86, elevator 34.02, town 99.02 s); Sendell's Well's crash is older.
- **From the app**, on a sandbox folder: both moons built in the race track window. With `EMERAUDE.ILE` open, Play starts on the grid with the opponents in the pit lane, muted.

### The Emerald Moon, second round: the cut-out loop, holes, borders, the pit lane, a wider road (2026-10-03, later)

The user found five things on the Emerald Moon:
- Twinsen couldn't drive the cut-out loop: he was sent flying into the air.
- Parts of the track showed as holes, though they could be driven over.
- The track's border was much wider than on other tracks.
- The pit lane should be a separate strip or have a fence, perhaps Citadel Island's white one.
- The track was too narrow in places.

**The cut-out loop.** With a 70-degree gap at its top, only entry speeds of 21-24 km/h got the car over cleanly. Anything faster sailed past the far edge ("Overshot the loop") -- and a player comes in at full speed. The engine's own step, run over the gap's size (radius 4 cells, throttle held), gives:

| Gap | Clean entry speeds | Above that |
|---|---|---|
| 70 degrees | 21–24 km/h | overshoots |
| 40 degrees | 22–37 km/h | overshoots |
| 35 degrees | 22–43 km/h | still gets round |
| 30 degrees | 22–46 km/h | still gets round |

Both moons' cut-out loops are now 35 degrees. They are taken at full speed (34 km/h), still a real leap upside down over the gap: off the ring at 162 degrees, over the gap, round at 5,386 units/s. The display's "how fast" hint, the opponents' braking for such a loop and the test pilot's now apply only where the car's top speed would overshoot it.

![the cut-out at full speed](racetrack/build/emerald_cutout_full_speed.png)

**The border.** The plan never set the asphalt's and the curbs' widths. The road was drawn with the ground tracks' own (asphalt 3.5 cells, curbs 4.5) inside a rail at 3.05. So the curb and the rail top folded back outside the rail: a grey band a cell and a half wide along both edges, and faces turned the wrong way. The plan now gives them like the other raised roads: the rail at the road's half width, the curb a quarter cell inside it, the asphalt a cell inside it.

**A wider road.**
- **Base road:** the rails are now at 4.5 cells from the middle (they were at 3.05), where the ground tracks' curbs are. A car's middle can go 3.25 cells either side of the road's middle (it was 1.8).
- **Loops:** the decks are twice that, 9 cells, and their rings' bands are as wide as the road.
- **Layout room:** three moves keep the wider road clear of itself.
  - The leg past the reactor's south side moves to z 33 and starts down after the rim's crest, not on it.
  - The inner U moves to z 43, and the U-turn after the jump narrows over its first half.
  - The east and west loops move along their straights: the west one's wider deck had reached the egg-shaped building.

**The pit lane.**
- **The fence:** Citadel Island's white fence (CITADEL.OBL's body 53, a section a thousand units long, and 54, its end post) is copied into EMERAUDE.OBL as it is; the island palettes share its colours. It stands solid along the line between the race lanes and the pit lane, 18 sections and an end post over 34 cells (the plan's `pitFence`). The pit lane is 4.5 cells wide behind it, open at both ends where the deck widens and narrows.
- **Tested:** the test pilot steered 4 cells towards the pit lane is stopped with its middle a car's half width from the fence, and slides along it to the straight's end.
- **The racing lines** keep 1.6 cells on the race lanes' side of it.
- **The grid** is moved onto the race lanes (the plan's `gridShift`, half the pit lane's width).
- **The opponents** wait in the pit lane behind the fence while the player qualifies.

![the road, the fence, the rings](racetrack/build/emerald_v2_road.png)
![Play in the app](racetrack/build/emerald_fence_app.png)

**The holes.** There were three causes:
- **The engine leaves out a polygon any point of which is behind the camera's near plane.** The road was one quad from rail to rail per cell, 17 cells across at its widest. Now the asphalt and the underside are strips, as many across as the widest asphalt needs, the same number for every piece of a road (`RaceTrackRaisedBody.StripsFor`). The rings' road and outsides are strips too. A ring is too many points for one body then (the engine takes 550 a body), so each ring is four quarter rings, a decor each at its foot.
- **The engine leaves out a whole decor whose middle is behind the camera** (`3DEXT/DECORS.CPP`): a piece of road whose middle had just passed under a close camera went whole. Now a decor whose box touches nothing (its top under its bottom, as only the race tracks' pieces have) is still drawn while any corner of its box is in front of the camera. The road pieces' boxes now cover their footprint, still touching nothing.
- **Pieces only meeting left a crack of a pixel between them**, the background through a dotted line across the road, because each body is projected on its own. Now each piece runs 24 units into the next, and each ring quarter into the next.

**Arrows.** The strips' points, the same on both sides of every joint, leave no room for an arrow cut into the asphalt; an arrow drawn over it flickered with it. An arrow is now the strips' own quads coloured, halved along their diagonals at its edges: a triangle 4 cells long, its base about the old arrow's width (2 strips either side on a wide deck).

![a crack between pieces, and none](racetrack/build/emerald_seams.png)

**Verified**:
- **The Emerald Moon** (`rt_emer`), the pilot and three opponents:
  - qualifying 61.52 s;
  - race laps 61.52 and 61.54 s, the opponents' 65.2–69.7 s;
  - the cut-out loop at full speed every lap, and every loop by every car.
- **The old moon:** qualifying 22.47 s, race laps 22.44 s.
- **The other tracks:** built again, the seven others' files are byte-identical but for the three raised roads' island and decor files (Celebration Island's two, the Elevator Platform's), whose road pieces are now strips. Their solo qualifying laps are unchanged (27.04, 41.86, 34.02 s), and the statue track was looked at too.
- **From the app**, on a sandbox folder: both moons built in the race track window. Play on the Emerald Moon shows the grid, the fence and the opponents in the pit lane, muted.
- **The body tool** (`tools/BodyPipeline`) builds again: it now leaves out `Scenes/SceneNuke.cs`, which needs the editor's own island code. It has a new `oblsheet <island .OBL> <palette> <out.png>` to look at an island's decor bodies, which is how Citadel's fence was found.

## The story and each track's drivers (2026-10-03)

The user's two notes, in the repository's folder (not committed):
- **StoryNotes.txt:** Zoe is sick of the rain and sends Twinsen to the Weather Wizard. The wizard would clear it from the top of the lighthouse, but Raph is too busy at the track and won't let him. Raph: beat my time and I'll come to the lighthouse. Mr. Paul runs the track and wants racing gloves. The aliens thank Twinsen for the rain: they'll build an even better race track, ready tomorrow. Twinsen has to go home and sleep (everyone tells him he looks tired) and uses his bed.
- **CharactersForTrack.txt:** who races each track.

Their follow-up: the story isn't fully worked out yet, so implement what can be.

### The drivers

`Terrain/RaceTrackDrivers.cs` (`RaceDriver`). A driver is one of the racer entity's car bodies (0 the retail racer, 1 Baldino's rocket car, 2-63 the character cars) or the motorbike. It has a racing line of its own, planned with its character (side of the road, top speed, cornering), and its skill a few points either side of the car setup's. An island's `Roster` replaces the racer, Baldino and the biker; an island with none keeps them.

| Track | Drivers |
|---|---|
| Citadel Island, storm track | Raph: his lap at the setup's skill less 6 is the time to beat (no car on the track) |
| Citadel Island, town circuit | Raph, Zoe, Mr. Paul, the Tralu, the thief |
| Desert island | Moya, the Dino-Fly, the Dean, the retail racer, Baldino |
| The Emerald Moon | Baldino in his lander (space suit), HAL |
| Mosquibees Island | the Queen, the monkey monster with the sword (war cart) |
| Otringal palace | (no track yet: Stan, the pighead with the broom, the two-headed monster) |
| the others | the retail racer, Baldino, the biker, as before |

- **The engine** races up to six opponents (`RACE_MAX_OPPONENTS`, keys `opponent2_` to `opponent6_`), and every grid has six spots.
- **The race car setup** lists the drivers of the track Play races, each with its own box, and one skill slider for all of them. This replaces the racer's, Baldino's and the biker's own boxes and sliders (`RaceCarSetup.LeftOut`).
- **RACETRACK.JSON** keeps the drivers as `Drivers`. A folder built before this still races its racer, Baldino and biker.

### The story

`Terrain/RaceTrackStory.cs` tells it through the game's own storm plot, which already has the lighthouse, the spell and the aliens:

| Game variable | Meaning |
|---|---|
| 51 | storm plot: 1 the wizard spoken to, 2 Raph spoken to, 3 Raph freed, 4 the storm over |
| 56 | the keeper: 3 back at the lighthouse |
| 70 | the aliens' landing done |
| 253 | chapter: 2 once the storm is over |
| 200-202 | new (variables no script uses): the day (1 tired, 2 slept), the town circuit won (1, 2 once paid), Raph's time beaten (1, 2 once Raph has said so) |

1. **Zoe** (scene 0, actor 4). She walks up to Twinsen as ever and says: "Twinsen, I am sick of all this rain! Go and find the Weather Wizard and get him to fix it!" She switches on the game's own arrow to his tent (holomap 21, which his script clears).
2. **The Weather Wizard** (scene 21). In place of "I can't find the keeper", he says the notes' line about Raph, and an arrow (222) points to the storm track's start line. Once Raph is back at the lighthouse he says to meet him there. Twinsen's own next line (he had seen the keeper held in a cave) is gone.
3. **At the start line** (scene 42, in the storm): Raph and Mr. Paul, copies of the game's own (Raph from the Tralu's cave, scene 2; Mr. Paul from his house, scene 7). They stand off the road and the pit lane, and both are gone once the storm is over.
   - Raph says the notes' line (variable 51 goes to 2, as talking to him in the cave did).
   - Mr. Paul: "Sorry Twinsen, for safety reasons you'll need some racing gloves to take part", or, with the gloves, to beat Raph's time.
   - The start line itself stays shut without gloves (`gate=`): crossing it says "Mr. Paul: racing gloves first!". The gloves are in the attic as before.
   - Raph is no longer held in the Tralu's cave, and Zoe no longer comes to fetch Twinsen there.

   ![Raph and Mr. Paul by the storm track's gantry](racetrack/build/story_raph_paul.png)
4. **Raph's time.** The display shows it ("Raph's time 0:33.90"), and a lap under it beats it (`beat=`). Raph says "You beat my time, Twinsen! A deal is a deal: I'm off to the lighthouse. Bring the Weather Wizard!" and goes. Variables 51 and 56 go to 3, as when he was freed in the game, so the game's own lighthouse scene follows: the wizard at the door, "We are ready, Master", the spell, chapter 2.
5. **The aliens.** The game then takes Twinsen to the tavern square (scene 42) in a cutscene that waits for an alien to speak. The island's actors who played it were taken off the road by the track's build (the cutscene would have waited forever). One alien, a copy of the game's own, now stands off the town circuit's road by Twinsen and says: "People from the planet Twinsun, we come to you in a spirit of peace. Thank you for clearing the rain which was preventing us from landing. As a thank you, we'll build you an even better race track. It should be ready tomorrow." That ends the cutscene, and variable 200 goes to 1.

   ![after the spell: fine weather, the town circuit](racetrack/build/story_after_spell.png)
6. **Tired.** While variable 200 is 1, anyone Twinsen talks to on Citadel Island (anyone but Twinsen himself) says "You look tired, Twinsen. You should go home and have a nap." instead of their own line. The engine swaps the text (`tired=`; GERELIFE.CPP's four message opcodes ask `RaceMod_Dial`).
7. **The bed** (scene 0). It has a zone of its own (scenaric zone 5). Action there while tired: "What a day! A good night's sleep... Morning already! The aliens' new race track should be ready by now." Variable 200 goes to 2, and an arrow (223) points to the town circuit's start line.
8. **The town circuit.** It is shut until then ("The new track opens tomorrow"). Afterwards it races Raph, Zoe, Mr. Paul, the Tralu and the thief over three laps; the display shows "Lap 1/3" and, at the end, "Finished 1st of 6".
   - A win sets variable 201.
   - Once Twinsen is out of his car anywhere on the island, Mr. Paul says "Well raced, Twinsen! You've won the prize: a ferry ticket." and gives it (inventory slot 13: the game's own ticket, for its ferry to the Desert island).

   ![the grid](racetrack/build/story_town_grid.png)
   ![three laps](racetrack/build/story_town_race.png)

**The texts.** Eleven new lines at the end of Citadel Island's texts and two holomap labels, in all six languages, using the game's own names: the Mage Météo, the Wettermagier, the Mago Meteo, the Mago Metereologo, the Mago do Tempo. They are shown, not spoken (see the 2026-09-29 section). Zoe's race-track line of 2026-09-29 is replaced. The gloves stay as they were.

![the wizard's new line in the engine](racetrack/build/story_wizard_line.png)

### Playing it

- **A track set.** The race-track mode used to load one track. A game played as a game now loads a set:
  - The car file Play writes holds the car and `track=<island> <when> <first scene> <last scene> <file>` lines, one per track. Each track's own file sits beside it.
  - When the game enters an island's outside scene, the engine loads the track raced there now (`RaceMod_Island`, EXTFUNC.CPP `InitGrilleExt`, before the island's file is chosen).
  - On Citadel Island that is the storm track while it rains (chapter below 2) and the town circuit once the storm is over; the weather is the game's own.
  - Celebration Island's two tracks go by the statue, and the moons' by their scenes. An island with no track keeps the car alone.
  - Everything one track set up is undone before the next loads (`ResetTrack`).
- **The new keys** (RACEMOD.CPP's description): `track=`, `gate=<var> <least> <text>`, `beat=<n> <var> <value>...`, `race_laps=`, `win=<var> <value>...`, `tired=<var> <value> <text>`.
- **The race car setup** has a new box: "Play the story from a new game".
  - Play then starts a new game, Twinsen in his house, every track raced where and when the game is. The engine's command harness starts it past the menu.
  - A Play that doesn't start on the start line is played the same way (the set, with the story's gates).
  - A race started on its line is that track alone, as before, with no gates. Its time to beat is still shown.

![a new game from the app](racetrack/build/story_new_game_app.png)

### Verified

All runs below were headless and muted, on a sandbox game folder built with the Desert island, Citadel Island, Mosquibees Island and the Emerald Moon (`E:\dump\TEMP\story`). The test pilot drove where there was driving.

**The story, step by step:**
- **A new game:** scene 0, chapter 1, Zoe's line. The engine loads the storm track for Citadel Island ("island 0 (storm): its track").
- **The storm track without gloves:** "start line crossed, but the race is closed: Mr. Paul: racing gloves first!".
- **With the gloves:** lap 1 in 28.44 s against Raph's 33.90 s. Variable 202 went to 1; then Raph's script set 51 = 3, 56 = 3 and 202 = 2.
- **The lighthouse:** with 51 and 56 at 3, Action by the wizard at the door ran the game's spell. That gave chapter 2 and scene 42, and the engine switched to the town circuit ("island 0 (fine weather)"). The alien's thanks set 70 = 1 and 200 = 1; Twinsen is back with his body and free to walk.
- **Tired:** the alien's second line came out as the tired line ("text 388 said as the tired line 1004").
- **The bed:** variable 200 went to 2, and arrow 223 is on.
- **The town circuit on day 1:** "the race is closed: The new track opens tomorrow".

**The town circuit on day 2,** with the setup's skill at 70 so the pilot would win:
- qualifying 99.17 s, a grid of six;
- laps 98.48, 98.42 and 99.06 s, the opponents' 133-140 s;
- "race over after 3 laps: 1st of 6" set variable 201.
- Out of the car, Mr. Paul's ticket: 201 went to 2 and 13 to 1.
- At the normal skill the opponents' qualifying laps were 107.6-115.0 s.

**The other tracks,** raced alone as Play's start on the line does:
- **Desert island:** qualifying 132.04 s (unchanged), the race with Moya, the Dino-Fly, the Dean, the racer and Baldino.
- **Mosquibees Island:** 47.87 s, with the Queen and the monkey monster.
- **The Emerald Moon:** 61.29 s, with Baldino's lander.
- **The old moon** (no line-up: the racer, Baldino and the biker): 22.47 s, unchanged.

**From the app** (sandbox settings):
- Desert and Citadel built from the race track window.
- The race car setup shows the town circuit's five drivers and the new box.
- Play started a new game in the editor's window, Zoe saying her new line.

**Not done** (the notes leave these open):
- Otringal's palace has no track, so its drivers aren't raced yet, and the two-headed monster (BODY.HQR 259) has no car.
- What the ferry ticket leads to beyond the game's own ferry is for the story to say.
- Mr. Paul is "in charge of the track": in the game Raph is the lighthouse keeper (Mr. Paul's house is scene 7), and the story reads right that way.
- Zoe ("Zoe?" in the notes) races on the town circuit, and can be left out in the setup.

### Celebration Island's drivers, the one to beat, and power-ups (2026-10-03, later)

The user asked for three things:
- On the lava lake (CELEBRAT), the drivers a kangaroo, the souvenir seller Twinsen talks to, and a policeman. Twinsen has to beat the seller to get the information he needs to progress.
- On each track, random speeds for the opponents, except the main one Twinsen has to beat, who should be beatable but a slight challenge.
- The power-ups of powerupNotes.txt, hidden in the game's small brown mushrooms until a car drives over them.

**The lava lake's drivers.** They are three new cars of the racer's entity (`Terrain/RaceTrackCharacterCars.Celebration.cs`):

| Body | Driver | Car |
|---|---|---|
| 60 | a kangaroo (BODY.HQR 308, a tourist with a camera) | a khaki safari car with a roll bar, the camera on the bonnet, a spring under its tail and the kangaroo's own tail off the back |
| 61 | the souvenir seller (BODY.HQR 307: the Franco who came back from Island CX, his head still bandaged) | a stall on wheels, a red and white striped awning over little Dark Monks for sale, and a price tag |
| 62 | a policeman (BODY.HQR 80: one of the island's Franco guards, green helmet and spear) | a green and white car with a red light, the spear's blade on its nose |

The seller's case and its straps are left out of the seated driver: a car takes the engine's 30 bones at most.

![the three cars](racetrack/build/celebration_drivers_cars.png)
![on the lava lake's grid](racetrack/build/celebration_lava_pit.png)

**The one to beat** (`RaceDriver.Main`):

| Track | The one to beat |
|---|---|
| Citadel storm track | Raph (his time) |
| Citadel town circuit | Mr. Paul |
| Desert island | the retail racer |
| The Emerald Moon | Baldino |
| Mosquibees Island | the Queen |
| Celebration lava lake | the souvenir seller |
| the others | the racer |

- **His skill** is a setting of its own: the race car setup's "The one to beat's skill", 86 % by default. At 92 % the test pilot, a better driver than most, lost the lava lake to the seller by half a second.
- **The others' skills** are drawn at random each time the track loads, 14 under to 12 over his (`opponent_pace=<lo> <hi>`).
- **The display** says "Ahead of …" or "Behind …" him through the race.
- **A race's result** (the town circuit's ferry ticket, the seller's information) is finishing ahead of him (`main=`), not first. The end says "You beat the souvenir seller!" or "The souvenir seller beat you".

**The souvenir seller's information.** In the game he is scene 95's actor 5 (entity 213). Asked what he saw on Island CX and how to get there, he tells of the fortress and of Rick's gang at the bar by Otringal's harbour (texts 171 and 172, Rick's holomap arrow 136, variable 124).
1. Until Twinsen has beaten him, after "What did you see over there?" he says: "That'll cost you more than a statuette, mister. Race me round the lava lake, and if you beat me I'll tell you everything." He takes Twinsen to the lava lake's race scene (223), on the grid by the buggy.
2. Three laps follow (story only), against him, the kangaroo and the policeman.
3. Out of his car after the race, Twinsen hears one of two things:
   - won: "You beat me fair and square, mister! A deal's a deal: here's what I saw on Island CX." The game's own information follows, with its arrow and variable.
   - lost: "Ha! Not fast enough, mister. Come and find me at my stall when you want a rematch."
4. Either way he is back in scene 95, by the dock.

New variables: 203 (seller beaten; 2 once told), 204 (his race run). The three lines are in all six languages.

- **The seller stays on the island.** The statue track's build took everyone off scene 95, the seller too. Now the people the story needs stay where the game has them (`RaceTrackIsland.StoryEntities`). He isn't moved out of the statue track's way either: he only exists before the statue rises, and moved, he stood in the lava.
- **A race over and come back to** is a race afresh (`RaceMod_Island`): a lost challenge can be raced again.

**Power-ups** (RACEMOD.CPP, `powerups=`).

*The mushrooms.* The game's small brown mushroom (entity 112, BODY.HQR 171; Citadel Island's by the weather wizard's tent, scene 45) is copied onto every track's road every 40 cells from 30 after the start line (`RaceTrackScenes.MushroomSpots`).
- Each sits a little left of, right of or on the middle of the road, and never on or near a jump, a loop or a carried jump.
- Any car that comes within 1,000 units of one takes it, and it grows back 15 s later.
- Its box is made a point, and the reach is longer than a car's own box: the engine stops a car at any object's box (CheckObjCol), and the first test pilot sat jammed against one.

*The seven power-ups,* from powerupNotes.txt, chosen at random by weight:

| Power-up | Weight | What it does |
|---|---|---|
| Gazogem fuel | 24 | a boost: the car's top speed and pull × 1.5 for 4 s |
| Protection spell | 12 | 30 s in which no mine, penguin or lightning hurts the car |
| Lightning spell | 12 | the other cars shrunk and slowed (× 0.7) for 20 s |
| Clover | 8 | life and magic full |
| Health | 16 | a quarter of the life back |
| Nitro penguin | 16 | dropped behind the car; 2 s later it goes off (the land mines' blast) and stops every car within 4 cells for 1.5 s |
| Super jet-pack | 12 | the game drives for 10 s, faster (× 1.35): the test pilot's steering, the player's keys left out |
| Oil (since the same evening) | 14 | dropped behind the car: a slick on the road until a car drives over it, and that car skids for 2 s |

- **Opponents take them too:** a boost, a penguin dropped, or Twinsen shrunk (unless protected).
- **The shrunk cars:** the engine can't scale a body as it draws it, so every car of the racer's entity gets a copy at half its size as its body 100 + its own (`Terrain/RaceTrackSmallCars.cs`). The copy is the body's own bytes with its points, spheres and bounding box halved. The engine switches a shrunk opponent to it (`opponent_small=`).
- **The penguin seen** is the shop's nitro penguin (scene 14, entity 46): a copy of it waits out of sight in each scene and stands where the newest one was dropped.
- **The display** shows what is on and for how long ("Protected 27").
- **The setting:** the race car setup's "Power-ups in the mushrooms along the track", on by default. A test key, `powerup_only=<n>`, puts one power-up in every mushroom.

**Oil, from the user the same evening** ("we may need to create an object for this").

*The slick* (`Terrain/RaceTrackOil.cs`) is a new body:
- a black puddle two cells across, irregular round its edge, with an oily blue and violet sheen on it, drawn flat (no light);
- made the mushroom's entity's body 1 (entity 112, whose body 0 is the mushroom: a fixed object of one bone, as the slick is), so a slick is a copy of the mushroom's actor with another body;
- three copies in each scene, out of sight until oil is dropped. The race-track mode keeps up to six slicks on the lap and shows a scene's with its copies, 20 units over the road.
- *A bug on the way:* its triangles were first wound facing down, and the engine left them out, seen from above.

*The skid:*
- **Twinsen's car:** for 2 s his keys are left out (as the jet-pack does), the car turns round three times as quickly as it steers, and slows only a third as fast, so it slides on, spinning (`RaceMod_SkidSteer`, `RaceMod_SkidCoast` in BUGGY.CPP's RaceSpeed).
- **An opponent's car:** it turns round one and a half times as it slows to under half its pace.
- **Who:** an opponent that takes oil drops it behind itself. The protection spell keeps Twinsen's car off a slick.

*Verified* (Mosquibees Island, every mushroom oil):
- Twinsen dropped a slick, and reversed back over it: he skidded, his heading swinging through a full turn in under a second.
- In the race, the Queen and the monkey monster skidded on his slicks, three and one times in a lap.

![an oil slick on the road](racetrack/build/powerup_oil.png)

**Oil, penguins and a key to drop them** (the user, the same evening):
- **Slicks last until a car drives over one,** and that car takes it with it. With no time limit, the oldest of six goes when a seventh is dropped.
- **Twinsen holds the oil or a penguin** until he drops it with Shift (either). The display says "Oil: Shift drops it".
  - The left Shift is the game's inventory key. While he holds something, and until the key is let go after a drop, the race-track mode keeps it from opening the inventory (`RaceMod_BlocksInventory`, PERSO.CPP's input).
  - The test pilot drops what it holds a second on (`autodrop_ms=`, 0: never).
  - Opponents still drop theirs at once.
- **A penguin dropped walks the track** (`penguin_mode=walk`, the default):
  - It walks at about a cell a second along the first opponent's racing line, this way or that, wandering up to a cell and a half to either side, turning now and then. With no line, it wanders round where it was dropped.
  - From a second after it is dropped, it goes off when any car comes within two cells.
  - Three penguin copies wait in each scene, so several can walk in one at once.
- **The setting** "Penguins walk the track until a car comes near", on by default. Off: a penguin goes off a second after it is dropped (`penguin_mode=fuse`).
- **Verified** (Mosquibees Island):
  - Holding oil, two presses of Shift (scancode 225 held from the console) dropped it twice, and the race went on with no inventory opened.
  - Walking penguins moved about 450 units a second and turned round now and then; after the start both opponents walked into some and were blown up and stopped.
  - On the fuse, each penguin went off within the second after it was dropped.

**Later the same day, two changes from the user:**
- **The lightning spell fills Twinsen's magic.** The game's own lightning spell takes all of his magic, so picking it up now fills it too, as a clover does (magic level × 20). It also shows the game's own lightning flash (INCRUST_ECLAIR, 0.7 s). With no magic level yet (the start of a game) there is nothing to fill.
- **No one dies in a race** (`RaceMod_NoHarm`: the race-track mode is on and Twinsen is driving).
  - *Penguins.* A car a penguin blows up stops with the hit reaction a land mine gives Twinsen's car: for his, the game's own `HitObj` (the hit animation, the stars, the car stopped, no life lost, as `RaceMod_CarHit` already made it for the mines). An opponent's car plays its own hit animation (the racer entity has one: its generic 6) under the stars, and its driving animation waits until it is over.
  - *Falls.* A fall long enough to kill Twinsen (16 bricks: OBJECT.CPP's "trop haut mort directe") or to hurt him (8) now stops his car a moment, as a mine does, with the landing animation and no life lost.
  - *Lightning strikes.* A strike stops his car with the stars instead of killing him (FoudroieObj).
  - *Already safe:* the sea and the lava put the car back on the road (`RaceMod_Rescue`), and the opponents are never hurt (NO_CHOC).
- **Verified** (Mosquibees Island, every mushroom a penguin):
  - Both opponents were blown up and stopped with their hit animation.
  - Braked and reversed back over his own penguin, Twinsen's car played generic animation 22 (the second of the game's two hit animations) and stopped, his life still 200.
  - The lightning pickup logs the magic filled.

![a mushroom on the lava lake's road](racetrack/build/powerup_mushroom.png)
![a nitro penguin dropped](racetrack/build/powerup_penguin.png)

**Verified** (headless, muted, `E:\dump\TEMP\story`, the test pilot driving):

*The souvenir seller:*
- Talking to him in scene 95 took Twinsen to scene 223, where the engine loaded the lava lake.
- **Lost** (one-to-beat skill 92): "3rd of 4, behind the main opponent". Out of the car, the rematch line; back in scene 95 with variable 203 at 0.
- **Won** (skill 86): qualifying 27.23 s; laps 27.22, 26.80 and 26.80 s against the seller's 28.1-29.6 s; "1st of 4, ahead of the main opponent". Out of the car: variable 203 = 2, 124 = 1, Rick's arrow on, back in scene 95.

*Each power-up* (one at a time with `powerup_only`):
- **Clover:** life 200 to 255.
- **Lightning:** the opponents' cars switched to their half-size bodies.
- **Jet-pack:** with the pilot switched off just after the pickup, the car drove on through three cubes.
- **Penguin:** dropped behind the car, it went off 2 s later.
- **In the random mix:** Gazogem fuel's lap 26.80 s against 27.22 s; the kangaroo took penguins, jet-packs, protection and health.

*The other tracks*, with mushrooms on and no car jammed:
- Desert: qualifying 132.23 s (132.04 before).
- The Emerald Moon: 61.29 s.
- Mosquibees Island: laps complete.

*From the app:* the race car setup shows the one to beat's skill (86 %), the random range and the power-ups box, and Play starts.

### The item box, rows of mushrooms, a lighter display (2026-10-03, night)

The user's asks:
- The jet-pack froze Twinsen in place when he raced no opponents.
- Pickups go in an item box in the top left corner, with the retail game's spinning item display, and Twinsen can hold a second item, with a key to choose between them.
- The display was too much: drop it, drop the checkpoints totally, put the speed and gear bottom right and the lap time bottom left.
- Rows of two or three mushrooms side by side, depending on the road's width, and a pseudo-random pick shown as an animation, the more powerful items for the cars further behind.
- The game's own protection spell animation while Twinsen is protected.

**The jet-pack freeze.**
- *The cause:* the jet-pack drives by the first opponent's racing line, and the player's keys are left out while it lasts (`RaceMod_Takeover`). With no opponent raced the car file had no line at all, so the jet-pack gave no keys back and the car stood still until it ran out.
- *The guide line:* when no opponent is raced, the car file now carries the one to beat's line all the same (`guide_path=`, `guide_top=`, `guide_grip=`; `RaceCarEngineFile.Guide`, written as `raceguide.txt`). Everything that follows a line uses it: the jet-pack, the walking penguins, the laps, and the "back on the road" rescue, which also had nothing to go by with no opponents.
- *With no line anywhere* (an old car file), the jet-pack is only a boost and the keys stay the player's.

**The item box** (`RaceMod_Draw`, `ItemBox`):
- *Two slots, top left,* each the retail found-object display (OBJECT.CPP's INCRUST_OBJ): black in the game's own frame, the item's OBJFIX model turning about once a second. They are 1/8 of the screen's height across (60 pixels at least, the retail size). The selected slot is ringed in the palette's gold (244). (Since 2026-10-17 one box, and no selection: see *One item box* at the end.)
- *The models* are the inventory's own:

  | Power-up | Shown as |
  |---|---|
  | Gazogem fuel | OBJFIX 15, the Gazogem can |
  | Protection spell | 39, the spell's ring of beads |
  | Lightning spell | 19, the lightning ring |
  | Clover | 60, the clover |
  | Nitro penguin | 14, the meca-penguin |
  | Super jet-pack | 48, the protopack's second look |
  | Health | the heart sprite of the game's bonuses (a sprite, not a model), beating |
  | Oil | a new model: a blue oil drum with two grey hoops, its lid and bung, a black drop over it (`RaceTrackOil.BuildIcon`), appended to OBJFIX.HQR by the build (`oil_icon=`) |

- *Lit as the inventory lights them:* the light is set with the camera level (`SetAngleCamera(0,0,0)`), not with the scene's camera as the found-object display does. The box is drawn without the outside's fog filler and depth buffer, with the colour table of things near (`SetCLUT(PalLevel)`; outside, the fog is the colour table of each object's distance, and the box would have the last object's). The protection spell's beads and the clover are dark in the retail inventory too.
- *The roulette:* an item taken goes into the first free slot and the items go by in it, slowing down over 1.5 s to the one drawn, which can't be used until it stops.
- *Keys:* Shift uses the selected item (the other slot's if that one is empty), and Q selects the other slot. Every item is held now, not only oil and penguins, so while Twinsen has one, Shift doesn't open the inventory. (Since 2026-10-16 Ctrl uses it instead, and Shift is the inventory's again: see *The race game* at the end. Since 2026-10-17 there is one box and no Q: see *One item box*.) With both slots full the mushrooms still give way to the car, empty. The test pilot uses each item `autodrop_ms` after its roulette stops (default 1 s, 0 never).

**Who draws what** (`RandomPowerUp`, `BackShare`):
- Each power-up has a strength:
  - weak: the clover, health, oil
  - middling: Gazogem fuel, the protection spell, the nitro penguin
  - strong: the lightning spell, the super jet-pack
- Each weight is multiplied by a factor running from the leader to the last car:

  | Strength | The leader | The last car |
  |---|---|---|
  | weak | × 1.6 | × 0.4 |
  | middling | × 1.0 | × 1.2 |
  | strong | × 0.05 | × 2.4 |

- Opponents draw by their own place.
- Outside a race (qualifying, alone, before GO) the plain weights.
- A car takes one item from a row: the others it passes in the next 0.7 s give way, empty.

**The rows** (`RaceTrackScenes.MushroomRow`): every 40 cells as before, now a row across the road. (Since 2026-10-17 a row every 8 s of driving instead: "Item spacing" in LBA2_RACE_TRACK_STRESS_TEST.md.)
- Three mushrooms where the road is 6 cells wide or more (the default asphalt, 7 wide, has three), two where it is 3.8 or more, else one; each row keeps a cell from the road's edges.
- They are 2.4 cells apart, more than the 2 cells a car's middle takes one from, so a car down the middle of one takes only that one.
- On a raised road the width is its own, rail to rail.
- A scene keeps room for its penguins and slicks under the engine's 100 actors, and a row that doesn't fit is shorter.
- Engine limit: 512 mushrooms a lap (was 128).
- Desert island has 93 now, the Citadel town circuit 75, Mosquibees Island 33.

**The protection spell's animation** is the game's own: the power-up casts `ToggleSortProtection`, the 16 orbiting sprites and the hum.
- The spell takes magic as it lasts, and ends when the magic runs out. While it is the power-up's, EXTRA.CPP takes none (`RaceMod_ProtectionFree`), and the sprites blink for its last 3 s (`RaceMod_ProtectionEnding`).
- It goes off after its 30 s, or when a new track loads.
- A spell Twinsen cast himself is left alone.

**The display:** bottom right the gear and the speed; bottom left the lap and its time. On Raph's storm track that line adds his time to beat. Before the start it shows the start line's text or the story's gate, and after the race the place. Everything else is gone: position, ahead of or behind the one to beat, last/best laps, the grid list, power-up timers, messages. The count-down stays.

**No checkpoints.**
- The build still finds them, but the car file no longer has them, and the engine reads none.
- A lap counts when the car crosses the start line after being halfway round the line it follows: a third to two thirds along the line, the first opponent's or the guide. So backing over the line and driving on again is no lap.
- With no line at all, any crossing the right way counts.
- At the line itself, the line's nearest point can be either end of it. Once the car has been halfway round, a point near the start reads as the lap's end (`PlayerProgress`).

![the item box, a row of mushrooms, the new display](racetrack/build/item_box_hud.png)
![the roulette going by: Gazogem, the jet-pack, the lightning ring](racetrack/build/item_box_roulette.png)
![held: protection, clover, health, penguin, oil, Gazogem, jet-pack](racetrack/build/item_box_items.png)
![two held, Q, Shift](racetrack/build/item_box_slots.png)
![the protection spell round the car](racetrack/build/powerup_protection_spell.png)

**Verified** (headless, muted, `E:\dump\TEMP\story`, rebuilt with `buildtogether`):
- *No opponents, the jet-pack in every mushroom:*
  - With the guide line, the pilot drove on through every jet-pack.
  - With no line at all and no pilot (up held, Shift pressed from the console), "Super jet-pack! (no line: a boost, the keys his)" and the car went on at 46 km/h under the player's own keys.
  - Before, the car stood still until the jet-pack ran out.
- *Two slots:* lightning in slot 1 and oil in slot 2; Q selected slot 2, Shift dropped the oil, and the selection went back to the lightning.
- *Each item's picture* is as in the screenshots above.
- *The protection spell:* its 16 sprites orbit the car for the 30 s.
- *Laps without checkpoints:*
  - The lava lake: qualifying 27.20 s, race lap 1 27.62 s.
  - Mosquibees Island: qualifying 46.72 s, race lap 50.11 s.
  - The Emerald Moon (loops, the carried jump): 60.87 s (61.29 s before).
  - Celebration Island's raised road: 40.67 s.
- *The draw by place:* in the Mosquibees race the leader drew Gazogem, oil, penguins, clovers, protection and health; the opponents took 15 mushrooms.

### Opponents in sight over cube edges, no health or clovers, penguins that go off at what they hit (2026-10-03, night)

The user's report from a test lap on Desert island:
- The opponents kept going invisible and coming back. They should always be visible.
- Health and clovers are meaningless as pickups in a race.
- Twinsen drove into a penguin standing on the track and it didn't explode. Penguins should blow up when they hit something.

**The opponents vanishing.**
- *The cause:* the engine keeps every object of a scene inside its cube (EXTFUNC.CPP DoAnimExt, half a cell from each edge). An opponent's car placed in the next cube stood on the edge itself, a wall Twinsen's car stopped at. That was the old moon's grid and Desert's checkpoint-7 stall.
- *The earlier fix* (2026-10-03, "Driving the loops") hid such a car instead. On a lap that crosses cube edges as often as Desert's, the cars just ahead vanished at every edge until Twinsen's car crossed it too.
- *Now:* the cars the race-track mode places this frame are let out of the cube (`RaceMod_FreeOfCube`). They stand where they are on the lap, over the edge too, in sight and in their true place, so they make no wall.
- They have no shadow and no ground or wall checks of their own (flags 0x1A1000: NO_SHADOW, NO_CHOC, NO_PRE_CLIP, OBJ_ZBUFFER), so nothing about them reads past the cube's map.

**Health and clovers** are no longer power-ups. No one dies in a race, so they did nothing.
- The six left, with their weights and strengths:

  | Power-up | Weight | Strength |
  |---|---|---|
  | Gazogem fuel | 24 | weak |
  | protection spell | 12 | middling |
  | lightning spell | 12 | strong |
  | nitro penguin | 16 | middling |
  | super jet-pack | 12 | strong |
  | oil | 14 | weak |

- Gazogem fuel moved from middling to weak, so the leader still has something besides oil.
- `powerup_only=` numbers changed: 0 Gazogem, 1 protection, 2 lightning, 3 penguin, 4 jet-pack, 5 oil.

**Penguins that go off at what they hit** (`PenguinHits`).
- *The cause:* a walking penguin could go off only a second after it was dropped, for every car. The second was meant for the car that dropped it, to get away. But a car right behind the one that dropped it reached it inside that second and drove through it.
- *Now that second is the dropper's alone* (`T_RACE_PENGUIN.Dropper`). Any other car's middle within 1,100 units sets it off at once.
- It also goes off at:
  - another penguin within 500 units (both go off);
  - a mushroom standing on the road within 500 (the mushroom is taken, and grows back);
  - a wall: the ground where it stands more than 512 units higher than the penguin. With no line, a penguin now walks on the ground up and down slopes, so only a real step counts.
- On the fuse (`penguin_mode=fuse`) it goes off at what it runs into too, or when its second is out.
- The log says what it ran into.

![the field in the next cube, in sight](racetrack/build/opponents_next_cube.png)

**Verified** (headless, muted):
- *Desert, the opponents faster (skill 115), no qualifying:* six times a car stood in the next cube within 20 cells of Twinsen's, and the engine drew every one (its WAS_DRAWN flag; the field ahead is in the screenshots).
- *Desert, qualifying with the opponents waiting in the pit lane:* 132.14 s (132.04 s before), with no wall at any cube edge.
- *Mosquibees Island, every mushroom a penguin, the opponents faster:*
  - Penguins the Queen dropped blew up the monkey monster right behind her, four times.
  - One she dropped ran into Twinsen's car: "Twinsen's car blown up by a penguin: stopped".

### Checkpoints at the corners, two jet-packs, oil and penguins to the front (2026-10-03, night)

The user's asks:
- *Checkpoints.* "Remove checkpoints" had meant the display's text, not the checkpoints themselves. Bring them back, one in the middle of every corner, for now drawn as a solid red line (to be made unseen later). Each should reach a little past the road's edge: a car half off the road still counts. The aim is to stop cutting the track on purpose without penalising a car on the edge, overtaking.
- *The super jet-pack* doubles the car's speed while the game drives, and goes to the cars at the back of the pack. A plain jet-pack does the same at one and a half times.
- *Oil and penguins* are more common the nearer a car is to the front.

Then four reports from the user's play:
- On Ascence, Twinsen's car sank through the deck, only part of him showing (the protection spell on).
- A protected car hit an oil slick as if it were a wall.
- The automatic gearbox stuck in first gear for no clear reason.

**The checkpoints** (`RaceTrackBuilder.PlaceCheckpoints`, RACEMOD.CPP).
- *Where they go:* a corner is a stretch of the lap bending one way tighter than a 40-cell radius, its bend smoothed over 3 cells, through 0.5 rad (29°) at the least. Two such stretches 8 cells apart or less are one corner. Its line goes where the corner has turned half its turn. A corner of more than a half circle gets a line in the middle of each half circle.
- *Where they don't:* clear of the pit lane's stretch, the jumps, the carried jumps, the loops, the crossings and the cubes' edges, as before. In the corner, the line goes as near its middle as is clear.
- *How far they reach:* 2 cells past the road's edge on both sides (its curbs, 4.5 cells from the middle by default, or a raised road's rail). Where another part of the lap comes near, a line stops short, but never inside the road's edge. The engine counts exactly that line (no slack past its ends).
- *How many:*

  | Track | Checkpoints |
  |---|---|
  | Desert island | 22, of 26 corners |
  | Citadel Island's two tracks | 5 and 15 |
  | Mosquibees Island | 8 of 9 |
  | The Emerald Moon | 12 |
  | Celebration Island | 9 |
  | the lava lake | 5 |
  | the Elevator Platform | 8 |

- *The rule:* a lap counts once they have all been crossed, in order. A track with none counts a lap halfway round, as since the item box.
- *The red lines* (`checkpoint_lines=1`; the race car setup's "Show the checkpoints as red lines across the road (for testing)", on): the engine follows each line of this cube along the ground (or a raised road's deck), and draws it 1/128 of the screen's height thick over the scene.
- *The display* still says nothing about them. A lap with a checkpoint missed doesn't count, and the log says which.

**The jet-packs** (`PU_SUPERJET`, `PU_JETPACK`):

| | Speed while the game drives | Item box shows | Who gets it |
|---|---|---|---|
| Super jet-pack | × 2 | OBJFIX 48 | the back of the pack |
| Jet-pack | × 1.5 | OBJFIX 12, the protopack | the back half |

- *The car's top speed and pull* scale by the factor, and so does the speed the game drives the line at. On the level the car also steers as much quicker over its top gear's speed (BUGGY.CPP, as it already did on a rollercoaster's slopes), so it takes the line's bends at twice the speed.
- *The look-ahead:* the test pilot's look-ahead grows only over the jet-pack's own top speed. Grown with the car's speed, it cut inside the corners' checkpoints and into a wall on Mosquibees Island.
- *Measured:* 9,672 and 7,254 units a second against the top gear's 4,836.
- *Opponents* that take one go at their pace times its factor for 10 s.

**Who draws what** (`s_puLead`, `s_puLast`: each weight's factor for the leader and for the last car; between them by place):

| Power-up | Weight | Leader | Last |
|---|---|---|---|
| Gazogem | 20 | 1.0 | 1.0 |
| protection | 12 | 0.8 | 1.2 |
| lightning | 10 | 0.05 | 2.0 |
| penguin | 16 | 1.8 | 0.4 |
| super jet-pack | 8 | 0 | 2.5 |
| jet-pack | 12 | 0.3 | 1.6 |
| oil | 16 | 2.0 | 0.4 |

- *The leader* draws oil or a penguin about two times in three.
- *The last car* draws a jet-pack or lightning more than half the time (56 %).
- `powerup_only=` takes several now, one at random: 0 Gazogem, 1 protection, 2 lightning, 3 penguin, 4 super jet-pack, 5 jet-pack, 6 oil.

**The oil slick a protected car hit.**
- *The cause:* the engine stops a car at any object's box (OBJECT.CPP CheckObjCol), and the race's props had boxes made only points across. A mushroom has to vanish before a car's box reaches it. But a slick a protected car drives over stays on the road for the next car, and was a post in its way.
- *Now* this scene's mushrooms, penguins and slicks are left out of CheckObjCol (`RaceMod_NoCollide`, a per-scene table).
- *Verified* on Mosquibees Island, protected the whole race with every mushroom oil: Twinsen drove over the Queen's slicks 14 times, never stopped, with a normal lap (47.71 s); the monkey monster skidded on them 11 times.

**Twinsen sunk into Ascence's deck.** The props' boxes were not the cause (left out of a car's way all the same); the item box was. See the next section.

**First gear.**
- *The cause:* the automatic gearbox changes up at the gear's top speed. Shrunk by an opponent's lightning (× 0.7 for 20 s), a car slowed to first gear never reached first gear's top, so it stayed in first. Since the item box, nothing on screen says the car is shrunk.
- *Now* the gears change by the speed the car would have without a boost, a jet-pack or the shrinking (`speed / RaceMod_SpeedFactor()`, as a climb already did).

![two corners' checkpoints on Mosquibees Island](racetrack/build/checkpoint_lines.png)

**Verified** (headless, muted, the folder rebuilt with the Elevator Platform's track too):
- *Desert, alone, every mushroom a jet-pack:*
  - The pilot crossed all 22 checkpoints in order, the lap counting at 78.24 s (132 s without).
  - Before the look-ahead fix it missed the third; on Mosquibees Island it drove into a wall.
- *Mosquibees Island:* all 8 checkpoints, twice, in a race.
- *Shrunk from the start* (`shrunk_ms=`, a test key, as is `protect_ms=`): the automatic gearbox went up through all five gears.
- *Ascence, protected, every mushroom oil, opponents ahead:*
  - With the props in the car's way (switched back for the test), Twinsen drove over 3 slicks, and once his car stood 124 units under the deck against a slick's box.
  - Out of the way: 20 slicks, never under the deck, laps 34.26 and 34.02 s.

### Twinsen sinking into Ascence's deck: the item box's view (2026-10-04)

The user, after the fix above: "Twinsen still seems to be getting pushed into the ground both with and without other racers, it seems to be when Twinsen gets to the first lot of mushrooms."

**Finding it.**
- *The user's own log* (`release/lba2-play/adeline.log`): the first mushroom's item, then the automatic gearbox down from fifth to first, the car slowing to a stop.
- *Headless, it never showed.* A height log every quarter second had the car on the deck to the unit, through:
  - the user's car setup (fifth gear 11,378);
  - Play's own start (a save made in the scene, then loaded);
  - the protection spell;
  - driving through the outer mushrooms;
  - longer frame steps.
- *What headless never draws:* a frame that draws only the objects over the last drawn ground (AFF_OBJETS_FLIP). At its fixed 50 frames a second, every frame while driving was a full redraw. At 200 frames a second (`--fixed-dt 5`) two thirds of them were objects only, and there the car sank to Twinsen's shoulders, or was hidden, from the first item on.

**The cause.** The item box drew each item as the game's found-object display does, then reset the view with PtrInit3DView. That resets the plain follow camera without the race-track mode's own eye and lift (FOLLOWCAM.CPP FollowCamReapplyLift: Ascence's rail camera). A full redraw sets the camera again before it draws anything. An objects-only frame draws the cars with the camera it finds, so from another eye than the deck under them, with their depth tested against the deck's. A real frame rate higher than the redraws makes most frames objects only.

![before: an objects-only frame at 200 frames a second, the car sunk to Twinsen's shoulders](racetrack/build/ascence_sunk_before.png)

**The fix.** The item box saves the library's whole 3D view before drawing an item and puts it back exactly after (`SaveView`, `RestoreView`):
- the projection (centre, near clip, factors, ratios, the projection functions);
- the camera (angles, position, rotated position, clip, MatriceWorld);
- the light (angles, normal, camera-space vector).

At 200 frames a second, the car stayed on the deck in every frame.

**The red lines on Ascence** leapt up and down: a line's points took another level of the road, or the ground far under the deck. Now a raised road's line follows the deck at its own level (`RaisedAt`, within 600 of the line's height), and past the deck's edge carries on at the edge's height.

### Wider raised roads, the super jet-pack as the car, through the cars in its way (2026-10-04, later)

The user's asks:
- Some raised tracks, like the Elevator Platform's (ASCENCE.ILE), are too tight to overtake on: widen them, where they can be if not everywhere.
- Remove the jet-pack power-up, keeping the super jet-pack.
- While the super jet-pack is on, the car turns into a model of it, tilted forwards and about the car's size.
- A car with it got stuck behind an opponent on the same line: it should push any car in its way aside, or pass through it.

**Wider raised roads** (`RaceTrackBuilder.WidenRaised`).
- *Which:* a raised road whose plan doesn't give its own widths (`RaisedHalfs`). That is the Elevator Platform, Celebration Island's statue track and the lava lake; the Emerald Moon's plan sets its own.
- *How wide:* `WidenedHalf`, 4.75 cells from the middle to the rail, point by point (into the road's RaisedHalfs, which the deck pieces, the rails, the piers, the racing lines and the engine's floor already follow).
- *How far it can go* at each point:
  - no further than its bend lets the inside edge go (0.8 of the radius);
  - clear by 0.75 cells of every other part of the lap within 1,800 units of its height (half the gap each), and of a ground road beside it by its verge;
  - clear of every decor object its space would newly reach, so no building the plan's road missed is cleared for the wider one;
  - over the ground (unless the plan cuts the ground away under the deck).
- *Kept at the plan's width:* 8 cells into each end where it meets the ground road, 10 cells either side of the start line (its gantry) and over a jump's gap with 3 cells either side.
- *Smoothing:* the width changes by 0.2 cells a cell along at the most, so it widens and narrows evenly.
- *The build says how much, and what held it back:*

  | Track | Width before | Average now | At the full 4.75 | Held back mostly by |
  |---|---|---|---|---|
  | Elevator Platform | 3.25 | 4.11 | 38% | the elevator tower (OBL body 0, a box 7 cells across that the road spirals round) and the platform's buildings, 30%; its own other levels, 6% |
  | Celebration's statue track | 3.75 | 4.47 | 58% | other levels, 12% |
  | Lava lake | 3.05 | 3.61 | 13% | its many ends and jumps, 30% |

- `RT_WIDEN_DEBUG=1` lists the decor bodies that held it back.

**The super jet-pack only.** The plain jet-pack (× 1.5) is out. The super jet-pack (× 2) takes its place in the draw: weight 12, never to the leader, 2.5 times its weight to the last car.

**The car turned into the super jet-pack** (RACEMOD.CPP `DrawSuperJet`, `superjet_model=`; `RaceTrackSuperJet`).
- *The model:* the build appends the inventory's super jet-pack (OBJFIX.HQR 48) at 0.45 of its size to OBJFIX.HQR. It is 3,000 units wide and 2,800 tall; the racer's car is 1,270 and 900 (`RaceTrackSmallCars.Scaled`).
- *Twinsen's car* is hidden while it lasts (INVISIBLE, set every frame: the game sets the car's flags again as it drives).
- *The jet-pack drawn in its place:*
  - 560 units over the road, bobbing a little, leaning forward 0.6 rad (34°) along the car's heading, its exhausts trailing;
  - depth-tested against the ground and the decor, in the colours of things near;
  - its place on the screen marked to be drawn over next frame.
- *Test keys:* `superjet_lean=`, `superjet_turn=`. The lean's sign was found by drawing it at 1.2 rad: the first way round, it leant backwards.

![the car turned into the super jet-pack, the racer pushed aside](racetrack/build/superjet_car.png)

**Through the cars in its way.**
- *Passing through:* while Twinsen's super jet-pack is on, every opponent's car is left out of his car's collisions (`RaceMod_NoCollide`), and an opponent with one passes through Twinsen's.
- *Pushing aside:* a car whose middle comes within 1,400 units of the jet-pack is pushed 2 cells aside, away from the jet-pack's way (right ahead: to its right). It goes out over 0.3 s and back onto its line over 1.2 s, at half its speed.

**Verified** (headless, muted):
- *Desert, super jet-packs only:* the car hidden and the jet-pack drawn in its place, the size of the car.
- *Ascence, every mushroom a super jet-pack, the opponents faster (skill 125):*
  - 14 cars pushed aside in four laps, Twinsen never stuck behind one.
  - His laps 22.8, 18.6 and 16.5 s, every checkpoint crossed.
- *The widened tracks, the test pilot's qualifying laps:*
  - Elevator Platform: 30.9 s (34.0 before, its line using the room).
  - Celebration's statue track: 40.2 s (40.7).
  - The lava lake: 26.9 s (27.2).
  - In each the race's laps complete too.

![the Elevator Platform's road widened](racetrack/build/ascence_widened.png)

### The super jet-pack flat out and untouchable, its end slowed and flashing; one turn on oil; Twinsen shrunk too (2026-10-04, evening)

The user's asks:
- Tilt the super jet-pack further, so it is fully horizontal.
- While the super jet-pack is on, nothing should affect it (it still skidded on oil).
- Before it ends, slow the car gradually back to its normal top speed, with a slow flashing to warn that it is about to end.
- Spinning a car round three times on oil is too much of a penalty: once round, then a complete stop.
- (Added) A small car for Twinsen, so an opponent's lightning shrinks him as it does the other cars.

**The jet-pack lying flat** (`RaceTrackSuperJet.LaidFlat`).
- *Why the build turns it:* the engine leaning the model a quarter turn as it draws it (`CarPose`) is the decomposition's singular case. The heading folds into the other angles, and at some headings the jet-pack flew backwards.
- *What the build does:* it turns every point and normal of the 0.45-scale copy a quarter turn about x, (x, y, z) to (x, −z, y), and its box with them. The tanks' tops point the way the car goes and the yellow nozzles trail; the HUD's upright icon shows the nozzles at the bottom.
- *In the engine:* `RACE_SUPERJET_LEAN` is now 0 (`superjet_lean=` adds to it, for tests). It is drawn 560 units over the road, the underside about 100 over.

![the super jet-pack lying flat, nozzles trailing](racetrack/build/superjet_flat.png)

**Nothing touches the super jet-pack.** While Twinsen's is on:
- he drives over oil (logged "Twinsen drives over oil, on the super jet-pack");
- penguins and opponents' lightning pass him by, and `RaceMod_CarHit` lets nothing knock him;
- using it clears a skid, a hit or a shrink he was in.

An opponent on a super jet-pack drives over oil too.

**Its end: slowed and flashing** (`JetFactor`, `RACE_JET_EASE_MS` 2,500, `RACE_JET_FLASH_MS` 250).
- *Slowing:* over its last 2.5 s the speed factor falls evenly from 2 to 1. That covers Twinsen's speed, the pilot's look-ahead speeds and an opponent's pace, so the car comes back to its own top speed rather than dropping to it.
- *Flashing:* over the same 2.5 s the jet-pack and the car take turns, 250 ms each.

![the last 2.5 s: the jet-pack and the car in turn, 80 ms a frame](racetrack/build/superjet_flash.png)

**Oil: once round, then a standstill** (`RACE_SKID_MS` 1,400; `RaceMod_Skid`, `RaceMod_MoveBeta` in BUGGY.CPP).
- *Turning:* the car turns one full turn, quickly at first and easing out: 4096 × (1 − (1 − p)²).
- *Slowing:* its speed falls as (1 − p)² to 0.
- *Sliding:* it slides on along the heading it hit the oil with, while the body turns.
- *Control:* no steering or throttle until it stops.
- *Opponents:* the same, their pace taken down the same way (the old 0.45 pace and three turns are out).

**Twinsen's small car** (`RaceTrackSmallCars.HeroSmall`, `twinsen_small=`).
- *The body:* the build halves Twinsen's buggy too (entity 12's generic body 1) into generic body 101.
- *When it is used:* while an opponent's lightning has him shrunk, unless the protection spell or the super jet-pack is on. Before, his car only slowed.
- *The shadow* shrinks with it. The engine sizes a shadow from the average of the box's width and length, so a small car's shadow still shows a little round it, as the opponents' small cars' shadows do.

![Twinsen's car on the line, then shrunk](racetrack/build/twinsen_small_car.png)

**Verified** (headless, muted):
- *Mosquibees, oil and lightning only, the opponents faster (skill 125):*
  - each of Twinsen's three skids ended at speed 0 one full turn round (heading 3183, 1133, 1159 from 3182, 1132, 1158);
  - the opponents skidded too;
  - each lightning hit switched his car to body 101.
- *Mosquibees, super jet-packs and oil only:* Twinsen drove over oil three times on the jet-pack and never skidded while it was on.
- *Desert, super jet-packs only:*
  - the factor went 2.00 → 1.85 → 1.65 → 1.44 → 1.23 → 1.02 over the last 2.5 s, the speed 9,672 → 4,952 (his top 4,836);
  - frames 80 ms apart show the jet-pack and the car in turn;
  - the jet-pack lay flat, nozzles trailing, at every heading round the lap.

### HAL on the Emerald Moon; the place in the race on the display (2026-10-04, night)

The user's asks:
- In the moon base there is a computer Twinsen can break, and then its mechanics worship it and call it HAL: turn it into a vehicle for the Emerald Moon.
- Show the current position, to see how a race is going.

**What HAL is in the game.**
- *Where:* scene 23, "Emerald Moon, next to outside Baldino's cell".
- *Actor 4, the screen:* a sprite actor, sprites 228 and 229 flickering while it works and 230 once cracked. The cabinet, the bottles and the stand are the room's bricks.
- *When it is broken* (hit, or `var_cube(2)`): the mechanics drop what they are doing. They are actors 12-15, the base's grey Franco guards with tools (BODY.HQR 98). One of them cries "HAL!!" (TEXT.HQR file 6, 546) with the translator, "ZX81!!" (545) without.
- *So there is no body to reuse:* the car is made after the room's look (`lba2sprite` renders the sprites and the palette, to match its colours).

**The car** (`RaceTrackCharacterCars.Hal`, body 63 of the racer's entity; `.Hal.cs`).
- *The stand* is the hull, in the lilac grey (ramp 208), with:
  - rivets down its sides;
  - the round red grille at the front on the left;
  - the dark pipe with its teal horn on the right.
- *The cabinet* is dark slate (ramp 176) with its top edges rounded. On its front:
  - the round green screen (ramp 144) bulging out of its bezel;
  - a grid over the screen, and a red eye in its middle (the other HAL's);
  - the red knobs under the screen.
- *Round the cabinet:*
  - a bottle at each front corner, dark, its coils glowing cyan, a white cap on top;
  - the red pipes arched over the top at the back, and the grey pipe out behind;
  - the gauge on the right with its red cable down to the stand.
- *It drives itself:* bone 13, where a driver sits, holds only the eye. The racer's animations turn that bone up to 40° (measured in ANIM.HQR 1071-1077), which would swing a screen out of its cabinet; a ball turns in place.
- *Size:* 509 points and 386 polygons, 7 lines, 22 spheres (the engine's limits are 550 and 550).
- *Line-up:* it races the Emerald Moon with Baldino, who stays the one to beat.

![HAL's car](racetrack/build/cars/hal.png)

![HAL racing, behind Twinsen on the Emerald Moon](racetrack/build/hal_racing.png)

**The place in the race** (RACEMOD.CPP `RacePlace`, drawn over the lap line, bottom left).
- *What it shows:* "Position 2nd of 3", from the moment the grid's GO starts the race until Twinsen finishes; after that, "Finished ..." as before.
- *How it is counted:* by the same measure as the item box's "how far back" (`BackShare`): the player's progress round the lap against each opponent's.
- *Not counted:* a time to beat with no car (Raph's on the storm track), so a race against the clock shows no position.

![the position as the opponents pass and are passed](racetrack/build/race_position.png)

**Verified** (headless, muted):
- *The Emerald Moon:* the grid was Twinsen, HAL, Baldino; HAL drove the lap with the others.
- *At opponent skill 130:* the display went 2nd → 1st → 2nd → 3rd of 3 as the cars passed each other.

### Oil slicks on the road; the open loop slows a car that is too fast (2026-10-04, night)

The user's asks:
- Oil slicks seem to be either invisible or half buried in the track.
- The open loop on EMERAUDE.ILE sends Twinsen flying into the air if he hits it too fast.

**Why the slicks were buried.** A slick was laid 1,300 units behind the car that dropped it, at that car's own height (and 20 over).
- *On a slope* the road back there is higher or lower. Going downhill the slick was inside the road: half of it, or all of it (unseen). Going uphill it floated.
- *Logged on Mosquibees:* road heights 150-240 units off the drop height were common (12125 against 11884).
- *Even at the right height,* a flat puddle 1,240 across on a slope of 0.13-0.2 has its uphill half under the road.

**The fix** (RACEMOD.CPP `PoseSlick`, `SlickGround`). Once a slick is in the scene in sight:
- *Height:* it takes the road's height under its middle: a raised road's in reach of where it was dropped, else the ground's.
- *Tilt:* it is tilted to the road's slope.
  - The slope comes from the heights 256 either side, along x and along z.
  - With no heading, the engine's M(Alpha) M(Gamma) M(Beta) turns its up by Gamma = asin(-nx) and Alpha = atan2(nz, ny).
  - A step over 0.6, like a deck's edge, counts as no slope.
- *Lift:* it rises by as much as the road anywhere under its rim (12 places, 620 out) stands over the tilted plane, plus 14. A crest or a banked deck's curve no longer covers an edge.
- *Collisions:* its new height is what the cars are checked against too.
- *Dropped in the air:* off a jump, it lands on the ground below.

**Verified:**
- *Desert, only oil:* the slicks behind Twinsen lie whole on the road, slopes 0.09-0.13.
- *Elevator Platform, on the banked spiral deck:* the slicks find the deck (432 below the car on the climb), with slopes up to 0.41 and lifts of 49-88.

![an oil slick lying on a sloping road behind Twinsen](racetrack/build/oil_on_slope.png)

**The open loop.**
- *The problem:* the Emerald Moon's second loop has a 35° gap at its top. It is leapt cleanly only from 22 to 43 km/h (`LoopWindow`).
- *Who it threw:* the car's own top, 34 km/h, is inside that window, but the super jet-pack (68 km/h) and a boost are not. Faster, the leap carried the car high over the far edge and it fell outside the ring.
- *The opponents* were already kept inside the window (`LoopEntry`).

**The fix** (`LoopCap`, `RACE_LOOP_SLOW`). Before a ring with a gap:
- *Braking:* over the road up to its foot, Twinsen's car is held to sqrt(cap² + 2 × 8000 × distance). That brakes it smoothly, no harder than 8,000 units/s², down to the cap at the foot.
- *The cap* is the window's top less a fifth of its width: 39 km/h on the Emerald Moon.
- *Clamped:* its speed into the ring is clamped to the cap as well.
- *The hint* now gives only the least to take it at ("Jump loop: over 22 km/h"). Slower than that the car still falls off: that is the driver's to get right.
- *Whole rings* are untouched.

**Verified** (Emerald Moon, only super jet-packs):
- Twinsen came up to loop 2 at 9,672 units/s and was slowed to about 5,500.
- He left the ring at the gap's edge, was caught "Over the gap" and came round, on both laps.
- Loops 1 and 3, whole, he took at the full 9,672.

### Smooth kerbs (2026-10-05)

The user's ask: the red and white markings on some tracks, Desert Island's and Citadel Island's, are jagged in places instead of one long smooth curve.

**Why they were jagged.** `PaintRoad` gave each 512-unit cell one paint (asphalt, red or white kerb, sand, hatching), chosen by how far the cell's middle is from the road's, and painted both its triangles alike. A kerb one cell wide therefore stepped along every bend and every road not square to the grid.

![before: the kerb and the hatching in cell-sized steps](racetrack/build/kerbs_before.png)

**What the engine allows** (3DEXT/TERRAIN.CPP).
- *Texture corners:* a ground triangle's texture corners are its own (8.8 fixed point into the island's 256 x 256 page, up to 8,192 definitions a cube). Any triangle can show any part of the page, stretched any way.
- *Flat colour plus texture:* a triangle with both draws the flat, lit colour first, then the texture over it with colour 0 see-through (`POLY_TEXTURE_INCRUST`). The verge's own sand shows through the texture's empty part, shaded exactly as the sand next to it.

**The fix** (`RaceTrackTextures.KerbTexture`, `RaceTrackBuilder.KerbCells`, `Painter.PaintKerb`).
- *The kerb texture:* a 48 x 32 block of the island's page, 10 texels a cell, in free space.
  - Along the road (x), red and white blocks 1.6 cells long, as before.
  - Across it (y), from one cell inside the asphalt's edge to one past the kerb:
    - asphalt (the asphalt tile's own pixels, at this scale);
    - the kerb: the red is the colour the flat red kerb shows at the ground's usual light (its ramp's 9th, Desert island's 73), the white the white curb's pixel;
    - nothing (colour 0).
- *The cut:* every cell near a kerb gets the road's coordinates at its four corners (across, from the road's middle, and along). It is cut along whichever diagonal keeps both its triangles within one cell across the road; cut against the road's way, a triangle reaches 1.4 cells.
- *Each triangle:*
  - short of the kerb: asphalt;
  - past it: the verge;
  - reaching it: the verge's flat colour with the kerb texture over it, mapped corner by corner from the road's coordinates.
- *Result:* the kerb's edges and the blocks' ends run where the road says, to a tenth of a cell, whatever the grid.
- *The verge's colour under the texture:*
  - sand on an open verge;
  - also sand beside a banked bend's hatching, which starts from the next triangle out. The hatching's average colour there read as the kerb's red running on round its white blocks.
  - On a walled road (over water or a valley, or cut into a cliff: `Bridge`), whose verge is rock, the ramp whose colour at the usual light is nearest the rock tile's average colour.
- *Where it is not used:*
  - cells near a second road's kerb (a pit lane alongside, a crossing);
  - jumps, landings, decks, the start line and the arrows;
  - roads whose kerb isn't one cell wide.
  - An island whose page has no free 48 x 32 block keeps the cell-by-cell kerbs: Celebration Island's two tracks.
- *The editor's map and holomap pictures* now take colour 0 of a texture over a flat colour as see-through too.

| Track | Triangles with the kerb texture | Where in the page |
|---|---|---|
| Desert island | 10,390 | (208, 32) |
| Citadel Island, storm track | 2,140 | (16, 160) |
| Citadel Island, town circuit | 7,153 | (16, 160) |
| Mosquibees Island | 2,677 | (16, 40) |
| Polar Island (2026-10-06) | 3,818 | (88, 224) |

![after: the same bend](racetrack/build/kerbs_after.png)

![Citadel Island and Mosquibees Island](racetrack/build/kerbs_citadel_mosquibees.png)

**What is still stepped:**
- the hatching's edges, and the edge between a rock verge's flat colour and the rock texture: they are their own tiles, painted cell by cell;
- a kerb right beside another road's.

**Verified:**
- *The sandbox, all seven islands:* it builds.
- *Desert's qualifying lap:* 126.7 s on fuel only, 119.8 s with every power-up. A first run with every power-up lost the lap after checkpoint 8, in a pile-up of its own penguins and oil; it went round in the rerun.
- *Mosquibees and the Citadel town circuit:* their qualifying laps complete.

### Mushrooms out of sight, a lightning strike, shrinking by place, driving any car, checkpoint maps (2026-10-05, later)

**Floating mushrooms on Celebration Island.** Scene 95 carries the statue's track (CELEBRA2), whose mushrooms stood on its raised road up to 16,560 high. The same scene is drawn with CELEBRAT (no statue, no road) before the statue rises -- in the game, and in the editor's view of CELEBRAT -- so they hung in the air. Citadel Island's scenes carry both weathers' tracks the same way. Now every mushroom is built out of sight (Y -20000, as the penguins and oil slicks are), with its road height in RACETRACK.JSON (`Mushrooms`: [scene, actor, y]) and the car file (`mushroom=<scene> <actor> <y>`); the race-track mode stands it there. Older builds (no height) work as before. Tracks need rebuilding.

**The lightning strike.** An opponent's lightning shrank Twinsen with no sign of it. Both ways now go through `Strike` (RACEMOD.CPP): the game's flash (INCRUST_ECLAIR), its thunder (SAMPLE_FOUDRE_STEP3), and a bolt from the sky onto each car struck, jagged anew every frame for 0.6 s (`DrawBolts`: white, edged in gold, with a branch). Twinsen's spell strikes the opponents in sight; theirs strikes him -- and with the protection spell on, the bolt lands but he isn't shrunk ("protected!"). On the super jet-pack nothing strikes him.

![an opponent's lightning striking Twinsen's car](racetrack/build/lightning_strike.png)

**Shrinking by place.** `ShrinkMs`: 20 s +/-25 %, longer the further ahead -- the leader 25 s, the last 15 s (four cars: 25, 21.7, 18.3, 15 s); 20 s with no race on (qualifying). Each opponent has its own `ShrunkUntil`.

**Driving as any car.** Race car setup > Your car: Twinsen's buggy, or any of the racer entity's cars the folder has (the racer's, Baldino's, the character cars). The car file gets `drive_as=<BODY.HQR body> <ANIM.HQR animation> <shrunk body>` (`RaceCarEngineFile.DriveAsLine`, from the folder's entity table: the racer entity's generic body, its driving animation 1, its shrunk body). The race-track mode hides Twinsen's car (as under the super jet-pack) and draws that car in its place (`DrawDriveAs`: a T_OBJ_3D of its own with the opponents' driving animation, facing the car's way, pitched by the slope just driven); its shrunk body while shrunk. It handles as the setup makes the car. Test: `RT_DRIVE=<body> racecarfile ...`. (Since 2026-10-17 the first choice is *Twinsen's own car*: his buggy, or the car a track has for him -- the Temple of Bú's pumpkin F1, race car 220, which every build adds last -- and "Twinsen's buggy, on every track" (-4, `RT_DRIVE=-4`) keeps the buggy there too: docs/LBA2_TEMPLE_OF_BU.md.)

![driving the Desert track as the Dean's car](racetrack/build/drive_as_dean.png)

**Checkpoint maps.** `ScriptRoundTrip trackmaps <game> <out>` then `trackmaps_paint.ps1 <out>`: a map of each built track (the island from above, a raised road's deck drawn over it, markers and arrows round the lap, the start line, the checkpoints placed), to draw checkpoints on. A lap that winds over itself gets a map per level (Celebration's statue 3, the Elevator Platform 4). `maps.txt` keeps how each map's pixels turn back into island cells.

**Verified:** the sandbox's seven islands rebuilt (mushrooms at -20000, heights in RACETRACK.JSON); the statue track raced with lightning-only mushrooms (mushrooms on the deck and taken; Twinsen struck, the bolt in the screenshots; shrink times 25/21.7/18.3/15 s by place); the Desert track driven as the Dean's car.

## Polar Island: the dream race to Sendell (2026-10-06)

The user drew a route over a picture of Polar Island (LBA1's island, made into LBA2's island 12: [LBA2_POLAR_ISLAND.md](LBA2_POLAR_ISLAND.md)).
The track picks up from the end of the first game: Twinsen dreams he is racing FunFrock to Sendell. It starts at a red dot on the dock, runs
down the island, loops round, has one jump over itself (two pink marks) and finishes with a jump onto the top of the rocky peak. Then Zoe
shakes Twinsen awake at home, in the second game's first scene.

**From the drawing to a plan.** A 3 x 4 camera (a DLT from seven landmark corners: the dock's end, the arm, 107's corners, the peak) maps the
island's columns onto the drawing. The drawn route's control points, read off the drawing, were put back on the ground through it and
traced on the island seen from above, keeping to the car tracks where the drawing follows them. `tools/RaceTrackPlan/polar_design.py`
turns them into the plan (`docs/racetrack/polar_track_plan.json`): a Catmull-Rom line every half cell, heights from the ground under it
(the highest of a small cross round each point, smoothed, grade-limited to 14 %, at least 600 over the sea), and the two jumps' ways.

**A sprint.** New plan keys: `open` (the route has two ends: the road isn't closed, `TrackRoad.Closed` false) and `finish` (the finish
line's point). An open plan gets no checkpoints. Its finish line is across the road there, like the lap line, at the road's height
(`RaceTrackReport.FinishLine`). Its racing lines run from the start line to the route's end (`PlanRacePath`: the same planner, with ends
of its own instead of going round). The scenes' edge crossings, the grid's places behind the start line and the mushroom rows don't wrap
round from the last point to the first.

**Two raised stretches.** A sprint's `raised` may give several stretches, each its first and last point. The engine's raised road
file is then one list from the first stretch's start to the last one's end, and the ground road between them is points with no width:
no floor there, as in a jump's gap. Pieces and piers are only under the stretches.

**The jumps.** Both are carried jumps (`arcJumps`). The one over the main straight (from cell 521 to 498.5 along z 456, its top at 2,728,
the straight under it at 603) crosses from cube (8, 7) to cube (7, 7). The straight is four cells from that edge, and a gap jump (the car's
own flight animation) breaks at a cube change. The one onto the peak climbs 700 up its ramp, tops out at 15,200 over the plateau's
pillars and comes down a short hill onto the pad, 46 layers up.

**The engine: `sprint=1`, `finishline=`, `intro=`, `lose=`, `wake=`, `dream=`** (`RACEMOD.CPP`):
- an opponent's line has ends: `PathAt` holds it there, and the opponent stops where its line crosses the finish line (`FinishS`). It
  then counts as finished: ahead of the player, who hasn't;
- the player crossing the finish line ends the race (`SprintOver`), and so does the one to beat (`main=`) getting there first, which
  loses it. The player's car stops there (`RaceMod_Held`);
- `RaceMod_Story`, called once a frame from the main loop before the scene is drawn (`PERSO.CPP`), where a dialog can open as a life
  script's does. It shows the intro (said by Twinsen as the grid forms, the count-down starting once it is read). 3.5 s after a win it
  takes Twinsen out of his car (`LeaveBuggy`, `ResetBuggy`: the car is no one's yet, as in a new game; and his move put back to on foot,
  which `LeaveBuggy` leaves the car's -- a change of scene puts a hero whose move is the car's back in it, `ChangeCube`'s `MemoMove`, and
  he drove off his bed at the race's speed) and starts the waking scene afresh at its own start (`FlagChgCube` 0, as the console's `cube`
  does: 2 carries his animation over the change). 3.5 s after a loss it takes the car back to the grid in the start line's scene
  (`cube_scene=`), where the grid forms again with the loss's line;
- `RaceMod_Dial`: in the waking scene, the actor `wake=` names (Zoe, actor 4 of scene 0) says the wake line before her first line. If she
  says nothing, `RaceMod_Story` says it 15 s after he woke. (Since the bed, below, scene 0's own opening says it: `wake=0 -1 4`.)
- `dream=<scene>`, a set's key: a new game starts there instead of in Twinsen's house (`RaceMod_NewGameScene`, from `InitGame`).

**The editor.** `RaceTrackIsland.Polar` (`Dream`), with its line-up `RaceDriver.Polar`: FunFrock, the one to beat. The track's record in
`RACETRACK.JSON` has `Dream`: the finish line, the top speed (140), the texts and where Twinsen wakes up. The car file scales every gear
of the setup so the top one is 140 km/h, turns qualifying off and writes the sprint's keys. A story set writes `dream=` with the start
scene. `RaceTrackService.EnsurePolar` adds the island when the originals the build starts from haven't got it (`PolarInOriginals`), and
`Problem` no longer asks for the island's files. The road's tiles go on the ground's first page: the island build keeps its last row of
32 x 32 slots free (`PolarTextures.Pages.Reserve`), and `RaceTrackTextures.FreeBlocks` counts only the first page's triangles on an island
with more pages. The holomap picture is the island's own (entry 46). The dock scene's buggy has `INIT_BUGGY 1`, and the start line's
script edit takes it as well as 0.

**The kerbs, smooth (2026-10-06, later).** The plan's kerb was 0.75 cells wide (asphalt to 3.0, kerb to 3.75), and the smooth kerbs
(above) are made for a kerb a cell wide: Polar Island's had stayed in cell-sized steps. It is a cell wide now (to 4.0), and 3,818 of its
triangles carry the kerb texture. An island with more texture pages has room for 1,024 texture definitions in a cube (a page and a
definition share the index), and the kerbs take one a triangle: the busiest cube has 994 with the road, and a build that would need
more stops with that said. What is still in steps is the sand verge's outer edge against the island's brown ground, and the rock
walls: they are painted cell by cell, as on every track.

**Waking up in bed (2026-10-06, later).** Won, Twinsen wakes up lying in his bed. Scene 101 (the Wannies' house, where the firefly tart
sends him to sleep) has his own animations for it: 56 asleep in a bed, 57 sitting up, 58 getting out. The race's `win=` sets game
variable 205 (`PolarDream.DreamVar`: nothing in the game uses it), and the build changes scene 0's opening for it (`ApplyOpening`):
- Twinsen starts asleep on his bed (a new track point, the bed's cells 9-11 x 1-4 at 3,072, four layers over the floor; turn 0, as he
  lies on the Wannies' bed, which is the same size with its head the same way; the place found by trying, his head on the pillow), no
  shadow, in cinema mode;
- Zoe walks round the bed's foot to its side (three new points) instead of turning to him, and says the wake line; 205 goes to 2;
- he sits up and gets out of bed, and is put on the floor where that leaves him (the animation lowers him; his place has to follow);
  his track stops at a label 2 of its own, which is what her own opening waits for -- her line, the Weather Wizard's arrow and the rest
  of the game's opening follow as ever.
Citadel Island's story edits the same scene (Zoe's line, the bed's zone): built together, scene 0 has both.

**Checked** (sandbox `E:\dump\TEMP\ptrack\game`, muted):
- `buildtogether` with the six other islands' tracks gives the same 56 files as the previous version, byte for byte.
- The test pilot (`autodrive`) from the grid to the peak: both jumps carry the car (the first changes cube at the top of its flight), the
  finish ends the race won in 36-40 s, Twinsen wakes up asleep in his bed in scene 0, Zoe comes to the bedside and wakes him, he gets up
  onto the floor and her own line follows (later: the kerbs smooth, the bed). With FunFrock's skill at
  300 % he gets there first, the race is lost and runs again from the grid.
- A new game with the story set starts in scene 243, on the dock beside the car.
- From the app: `POLAR.ILE` opens with the road, and Play ("Race: Polar Island") starts on the grid, or in the dream as a new game.
- Putting the track back leaves the island as it was before the track.
- Later: the six other islands built again give the same files but BODY.HQR, whose only changes are the firefly tart car and its
  half-size copy; Citadel Island and Polar Island built together give scene 0 both their edits.

**Round 2 (2026-10-06, later): the peak, the jumps, waking up.**
- *The rocky peak* had lost its middle: the race build takes away decor objects under the road, and the jump's landing pad is on the
  peak, so the tall pieces the peak is made of (columns from the ground to 11,776) went with the low ones round them, leaving the top on
  a thin stalk. A new plan key, `keepAbove` (6,000 here), keeps every decor whose top is at least that high, as `keepBodies` keeps its
  bodies; their boxes are cut to end under the raised road as kept decors' are, and the ruins pass (`ClearRuins`) leaves them too.
  `decordiff <ileA> <ileB> x0 x1 z0 z1` (`PolarStudy.cs`) lists the decors one island file has and the other hasn't in a box of cells.
- *The camera at the first jump* went to the side, as at every carried jump. An `arcJumps` entry may now have a fifth number, the
  camera's side: 1 or -1 beside the jump, 0 behind the car (the engine's `arcjump=` side 0: `ArcCamera` gives way to the follow
  camera). The first jump has 0; the jump up to the peak keeps the side view.
- *The second jump's ramp* had LBA1's terrain through its left half: the ramp rises from a dip (3,072) beside a ledge at 4,608, four
  cells wide along its north rail. The plan now has `raisedCut`, and `CutUnderRaised` deals with the ends of the raised road too: within
  8 cells of where the deck leaves the ground road, ground standing over the deck is brought down to 50 under the nearest stretch of
  deck (out to a cell past the rail, then banked up), never where the ground road is nearer. (The cutting stays away from the ends, and
  the ends' flush only runs while the deck is near the ground under its middle: here it is 900 over the dip.) The lava lake, the other
  `raisedCut` plan, is unchanged by it, byte for byte.
- *Waking up* now puts the race away: the finished race had held the car still wherever Twinsen drove next ("finished 1/1" on Citadel
  Island), a power-up still on came back with the next car (the super jet-pack), and the dream's fine weather stayed, so the house
  door led to CITABAU. `RaceMod_Story`, on a win, reloads the race file for the car alone (`ResetTrack`, `Load(.., 1)`: a story set's
  file when one is loaded), clears the fine weather, and gives the car the gears of the setup as it is (`after_gears=`: the dream's are
  scaled to 140 km/h). The car file of a dreamt race has no `weather=fine`: Twinsen wakes in the game's own weather.

Checked (sandbox, muted): the test pilot over the whole sprint (the first jump with the camera behind; the ramp up to the peak whole,
kerbs on both sides; the peak standing; the win), then out of scene 0's front door into scene 49 on CITADEL in the rain, on foot. The
seven other tracks built again are the same files, byte for byte.

**Round 3 (2026-10-06, later): the island twice its size, LBA1's tracks up 108, round the peak and a jump into it.** The user: "widen the
island and make the whole thing bigger" -- 108's car tracks go back and forth up its terraces more often than the road could follow --
"trim [the car] down to 120", "a loop around the tower followed by a jump into it", and Twinsen to hit that jump "but never land as Zoe
rocks him to wake him up". The island at twice LBA1's size is in [LBA2_POLAR_ISLAND.md](LBA2_POLAR_ISLAND.md).
- *The route* (`tools/RaceTrackPlan/polar_design.py`, run in a folder with the big island's `heights.csv` and `columns.csv`): the old
  route's control points doubled from the dock to the south strip and the jump over the main straight, then LBA1's own tracks up 108 --
  five ways up, each a terrace higher (1,200, 3,072, 6,144, 7,168, 8,192, 9,216), twelve to sixteen cells apart, hairpins of 5 to 6 cells
  round (the tightest, between two ways twelve cells apart, a loop a little wider than LBA1's square turn) -- east along 108's top, then
  a raised road round the rocky peak (its north side over the sea, its east side, a half circle 20 cells round over the lake on its south
  side), climbing 900 from 108's top, and in to a straight and a ramp at the peak's south face. 1,642 cells to the finish, 606 before.
- *Cube edges:* LBA1's grids are whole cubes at the placement with the fewest cubes of land, and its tracks up the arm run along x 512, a
  cube's edge: the road runs ten cells east of them there (along an edge the car changes scene back and forth). The final straight, 109's
  hairpin and 108's top run are kept eight cells or so off edges too (`polar_design.py`'s notes).
- *The jump into the peak:* a carried jump whose landing is the route's end, a few cells inside the peak (no deck anyone sees). The plan's
  `finish` is the ramp's lip, and `keepAbove` is 15,000 (the peak's decors; at twice the size the huts and walls stand taller than the
  old 6,000). Both jumps' cameras are behind the car (`arcJumps` fifth number 0).
- *Waking in mid-flight* (`RACEMOD.CPP`, `wake_flight=<ms>`, `PolarDream.WakeFlightMs` 450; `RaceTrackService.DreamInfo.WakeFlight`): a
  won sprint isn't held at the line (`RaceMod_Held`), the car is carried on over the jump, and 450 ms later the picture fades to white
  (`FadePalToPal` to a white palette: the engine's `WhiteFade` fades from black) and the race is put away as before. The waking scene is
  held white (`RaceMod_FadeFromWhite`, from `OBJECT.CPP`'s fade-in) for 400 ms -- its first picture is Twinsen at the scene's start, before
  its opening lays him in bed -- then fades in from white (`FadeWhiteToPalAndSamples`, `RaceMod_Story`).
- *Top speed* 120 km/h (`PolarDream.TopKmh`); the intro now says "beat him to the rocky peak".
- *Texture definitions:* the busiest cube needed 2,030 with the road. A paged island's triangle has only 10 bits for its definition, the
  page taking the index's top 3, so the triangle's unused `Dummy` bit is now the definition's eleventh (`IslandPolygon.Wide`,
  `IslandFile.GroundTextureOf` / `WithGroundTexture` / `MaxGroundDefinitions`, the painter's `Paged`, the terrain editor's paints,
  `IslandDocument.TextureKey`; the engine's `GroundTexDef`): 2,048 a cube. And on a paged island the smooth kerbs' road coordinates are
  snapped to 1/16 of a cell and their blocks are 2 cells long (`KerbSnap`, `KerbSnapBlock`), so a straight kerb's triangles share
  definitions every 4 cells: the busiest cube now needs 1,425. Other islands' kerbs are as they were, byte for byte.
- *Commands:* `defsuse <ILE> <cx> <cz>` (a cube's definitions by kind of triangle; `DEFS_CELLS` lists a box's kerb triangles),
  `buildhere <game> <island>...` (the window's Build on a folder as it is: the upgrade from the island at LBA1's size). The build names the
  cube that is short of definitions.

Checked (sandbox `E:\dump\TEMP\pbig`, muted): the big island in the engine (dock, 107, 108, the peak) and the app (POLAR.ILE's 35 cubes in
the 3D view and minimap, scene 252), its holomap picture; the test pilot over the whole sprint (both jumps carried, a win in 64-69 s,
the white fade, Twinsen asleep in bed with Zoe beside him); the window's build over a folder with the island at LBA1's size and its track
(put back, the big island added, scenes 233-253 its own and none left over); the seven other tracks built again are the same files, byte
for byte; the island round trip.

**Round 4 (2026-10-06, later): barrels on the road, waking up, the car outside the house.**
- *Two of LBA1's barrels on the road* where it leaves 108's top for the raised road round the peak. They stood 7,000 below the road,
  at the terrace's foot, when the road was cleared; then the raised road's end had its ground filled up to the deck (`FlushRaisedEnds`)
  and decors follow the ground under them (`IslandOps.DecorFollow`), so they rode up onto the road. The decors are now cleared again
  once they have followed the ground (`ClearDecors` a second time, after `follow.Apply()`); the other tracks' files are unchanged by it.
- *Waking up* (the user: Zoe took a long time walking round the bed): she starts where the game's opening has her, beside the bed's
  head, and says the wake line two seconds after the room has faded in; steps back out of his way; he sits up, gets out and turns to
  her; she comes round by the open floor south of the bed and kisses him (her animation 84, the game's own when Twinsen walks into her
  at home: she hugs him, hearts float up); then her opening line (the race track story's about the rain, where it is built). Game
  variable 205 counts the steps (1 asleep, 2 the line said, 3 getting up, 4 the kiss). Two things learnt: a track's `goto_point` counts
  an actor there 500 short of the point (GERETRAK.CPP), so the points lie 500 past where she stops; and an actor whose way runs into
  Twinsen's box walks on the spot for ever -- her route keeps a cell from him, and the kiss happens on his south side, so her own opening
  walk afterwards leads away from him (from his west side it passed him, and walking into him is the game's kiss again, over and over
  while he stood still).
- *The car outside the house* (the user: undrivable after the dream): the town circuit's start, scene 49, puts the car on its start
  line whenever the scene starts on foot, and in the storm -- the dream wakes Twinsen up in it -- that line is CITADEL.ILE's sea. The
  engine now tells the scripts which weather's file is shown (game variable 206: 1 fine, CITABAU; 0 the storm, CITADEL -- set in
  EXTFUNC.CPP `InitGrilleExt` as it chooses the file, `RaceMod_CitadelWeather`, in any game), and each of Citadel Island's two start
  cars parks, in the other weather, in the yard north of Twinsen's house (`RaceTrackIsland.ParkOwn` / `ParkTwin`: flat at 250 in both
  files): the storm track's in scene 42 when it is fine (its line is under CITABAU's ground), the town circuit's in scene 49 in the
  storm. Checked: in the storm the car stands in the yard and drives off (behaviour 12, 5,000 units east); racing the town circuit
  (`weather=fine`) it stands on its start line.
- *Commands:* `entityanims <game> <entity>...` (an entity's animations and bodies), `gametext <game> <file> <id>...`.

## Citadel Island again: the dots, one car, the way into the docks, the doors, the pharmacy, Raph's laps and the lighthouse (2026-10-06, evening)

![the way into the docks under the new deck, the pharmacy and the baggage claim back, the car under the carport, Raph lapping, Raph stopped in his car, and the spell on the lighthouse](racetrack/build/citadel_round_oct6.png)

The user drove the storm track and asked for six things, then for the storm story to be told round Raph's laps.

**Three dots following the car.** Every scene's power-up actors and spare cars wait out of sight at Y -20,000 until the race-track mode
needs them -- in Citadel Island's scenes the other weather's track's too (its mushrooms in rows of three). Drawn, each put its shadow on
the ground straight above it. The engine now leaves out any actor that far down (`OBJECT.CPP`, the actors' drawing: `y <= -10000`), body
and shadow. The storm track's own mushrooms are there in a race (the test pilot takes five a lap).

**One car by the house.** The island has a car of its own under Twinsen's carport: a decor that can't be driven (CITADEL body 85,
CITABAU 114). It goes from both files (`RaceTrackIsland.DropDecors`: the pieces at an origin, `RaceTrackBuilder.DropDecors`), and in the
storm the town circuit's start car parks in its place, turned as it was (`ParkTwin`, cell 544.1, 654.75).

**The way into the docks.** The jump climbed from the harbour on a filled embankment and buried the street at the rampart's south end,
cells z 612-617 -- the town's way into the docks (scene 43's crossing zone from 42 is there, under a little bridge at 2,250). The storm
plan now (`citadel_storm_design.py`):
- the jump is at the rampart's own height, 2,500 (it was 2,000), so the landing no longer cuts the rampart down;
- the climb from the harbour, round the south-west corner and up to the take-off lip, is a raised road on piers (`raised` 317-429,
  `raisedHalf` 4.5): the ground under it is left as it was, the street, the passage and the dock's steps with it. Twinsen walks from the
  town under the deck into the docks (scene 43). The test pilot laps in 22.95 s with the power-ups, the jump flown off the deck.

**The doors.** The build took every door within reach of a road away -- to keep the car out of the buildings. It never needed to: the
engine takes the car through a cube change only into an outside scene (`OBJECT.CPP GereZoneChangeCube`: from the buggy, only when the
destination's holomap flag says outside), and most doors also need Twinsen to walk into the building's wall (`Info5` bit 0,
`ZONE_TEST_BRICK`), which does nothing where the building is gone. So every door into a building now stays; only one without the wall
test (a sewer's grate) goes where a road's surface now covers it at its height (`RaceTrackScenes`, `Paved`). On Citadel Island 11 doors
near the roads are kept (the shop, the tavern, the sewer, Mr. Paul's house, the ticket office, Tralu's cave, the spider cave, the museum's
two, the pharmacy, the baggage claim, the school, the neighbour's house); a door that is shut in the game (the pharmacy's, until its
script opens it) is as shut as before.

**The pharmacy, the museum and the storage centre.** The pharmacy and the baggage claim (the user's storage centre) are one low building
of two bodies by the rampart, its doors facing east. The storm track's double hairpin turned 1.5 cells from them, a thousand units up.
Its west turn now lies 6 cells further east and stays at the street's height (250-583; the climb to the north rampart is on the next
leg, 11.2 % at its steepest), and the plan keeps the building's two bodies (`keepBodies` 32, 33): it stands, and the street in front of
its doors is level. The museum couldn't be done the same way: its top floor stands on the rampart's walkway, 5.7 cells from the railing
-- the rampart is the island's edge (cube (7,8) is missing) -- and the road along it, 9 cells wide, lands the jump right there; going
over it would take a deck at 5,300. The town circuit (in the fine weather's file) still takes all three away: its road runs along the
rampart's east side.

**Raph laps the storm track** (the story, `RaceTrackStory`):
- *His laps.* Raph's car laps the storm track on its own, at his skill (his line is the time to beat): the engine's `parade=<driver>
  <variable> <value>` (`RACEMOD.CPP ParadeStep`) drives the time-to-beat driver along his line in the scenes' copies of his car
  (`parade_actor=`: the town circuit's copies, out of sight in the storm) while game variable 208 is 0, nothing colliding with it; then
  it stands parked (`parade_park=`) by the start line. It asks for the whole picture to be drawn while it moves (`RaceMod_Story`, before
  the frame is drawn: a depth-buffered car is drawn into the background, and the opponents' own request is only taken up while Twinsen
  drives -- on foot, the car's first picture stayed on the road).
- *He stops* when Twinsen comes within 20 cells of the start line's end on foot (208 goes to 1), and talks from his parked car -- the car
  has him at its wheel (the game's Raph is the speaker, out of sight where it parks).
- *What he says:* without a car or driving gloves, beat my time and I'll come to the lighthouse -- *you'll need a car and some driving
  gloves to take part*; with both, the time itself: "My best lap is 32.65 seconds." The time is the race-track mode's (`beat_text=<text>`:
  `MESSAGE.CPP GetText` asks `RaceMod_Text`, which puts Raph's lap at the setup's skill where the text has `##`). Having a car is game
  variable 207, set by every outside scene's controller the first time Twinsen drives.
- *Mr. Paul stops him:* a scenaric zone over the start line, the lap's and the pit lane's (the scene's next number, 41), and Mr. Paul
  puts Twinsen out of his car in it without gloves (`set_dir_obj(0, MOVE_MANUAL)`, the game's own way out of the buggy) -- "Stop right
  there, Twinsen! Nobody drives on this track without driving gloves." The race-track mode's gate stays too. "Racing gloves" are "driving
  gloves" everywhere now, in all six languages.
- *Raph's time beaten,* Twinsen out of his car: "A deal is a deal: I'm off to the lighthouse. See you there!" -- the plot's 51 and 56 go
  to 3, as when the game's Raph was freed -- and Zoe joins Twinsen: the two of them walking together (behaviour 5, `C_DOUBLE`: the game's
  own, after the Tralu's cave), her line "Raph's on his way to the lighthouse, and the Weather Wizard is meeting us there," and they are at
  the lighthouse (`change_cube(46)`; the game's walk back from the cave, scenes 45, 47, 50, 48, 49, would play its own films and look for
  people the build took away). There the game's own scene runs: the wizard and Raph wait at the door, Zoe keeps Twinsen from wandering
  off, Action by the wizard -- "We are ready, Master." / "Follow me!" -- the spell from the lighthouse's top, Twinsen and Zoe up there
  with him, the storm is over (chapter 2), and the alien thanks Twinsen by the tavern.
- The lighthouse's people are kept (`RaceTrackIsland.StoryEntities`: Raph 19, his fiancée 113, Twinsen and Zoe on its top 116), and they
  and the door's track points stand on the storm file's ground (`OwnGroundAt`, `RaceTrackScenes.Reseat`): the town circuit, in the fine
  weather's file, cut the hill at the door from 3,250 to about 1,000, and the scene's people had been moved down with it -- in the storm
  the wizard stood inside the hill.
- New game variables: 207 (Twinsen has his car) and 208 (Raph has stopped); nothing in the game uses them (`scriptgrep2`).

Checked (sandbox `E:\dump\TEMP\pbig2`, muted, the story's set of track files): the dots gone where the town circuit's mushrooms wait in
the storm; one car by the house; the storm lap with the test pilot; the walk under the deck into scene 43; the pharmacy's building and
doors; Raph's car lapping with Twinsen away and stopping as he comes, his two lines and the time in them; Mr. Paul putting Twinsen out
without gloves, and not with them; the beaten time to the lighthouse as Twinsen and Zoe, the spell, chapter 2 and the alien's thanks;
the dream's waking up as before. *Commands:* `scriptgrep2 <game> <text>` (every LBA2 script line with it), `decordiff` prints each
decor's origin and turn.

## Citadel Island's buildings back: the storm track over the shop and the museum, the town circuit's houses (2026-10-06, night)

![the storm track's jump, as it was, at 3,900; the museum's lower block, the pharmacy on its own ground, the shop under the take-off and the school in the rain; the dock with its ferry, the pharmacy made less deep, and the lighthouse once the storm is over](racetrack/build/citadel_buildings_oct6.png)

The user: the pharmacy and the storage building float; the museum and the shop are still gone in the rain; restore what can be on the town circuit too -- Mr. Paul's house and the ferry ticket office perhaps by making them "not quite as deep" or the land further out -- the museum may stay out of the fine weather's file. And the fine weather still had a jump script on the town circuit's bridge.

**Why the pharmacy floated.** Decors follow the ground under their origin (`IslandOps.DecorFollow`). The rampart road's verge was blended down into the town over the building's west half, which lifted the ground at its origin by 950 -- and the building with it, over a slope. Plans can now keep the ground under their kept buildings (`keepGroundUnder`: the footprint and 2 cells round it, `KeepGroundMargin`, untouched by `ModifyGround` except under the road's own surface). It now stands where it did, on the ground it stood on.

**The storm track over the shop and the museum.** A first try (v2026-10-06h) flew a carried jump of 36 cells over the whole museum, its pit lane shortened to clear the tavern; the user found the jump far too long and the pit lane too narrow, and would sooner lose the museum's top floor and the tavern. So the lap is as it was -- the jump off the rampart's end, an 11-cell gap (z 594 to 583) flown by the retail flight, the pit lane its 48 cells down the east straight, the start line at z 585.5 -- but higher: take-off and landing at 3,900 (it was 2,500), on one raised road from the harbour round to the north rampart (`raised` [SE1, N3], 141 cells; down to the north rampart at 5 %). Under it the town is untouched: the shop under the take-off (its top 3,500), the way into the docks, and the museum's lower block under the landing (2,618). What goes: the museum's top floor (bodies 31, 42) and the tavern, which the pit lane runs through. The double hairpin's east turn stays 5 cells west, clear of the school. The test pilot laps it through all its checkpoints, the jump off the deck and onto the deck.

**The town circuit (the fine weather's file).** Measured with the build itself (`buildingprobe <pristine> <scratch>`: Citadel's two files built into a scratch folder, every building gone and why, and for each kept one how far the road's curbs are from it and whether it or its ground moved):
- *Mr. Paul's house and the ticket office:* the dock loop ran along their fronts, its curbs 1.3-1.8 cells into them. The loop's north side now runs 2.5 cells further out over the dock's square (the plan's points, at least z 597.8): the curbs are 0.8-0.9 cells clear and their doors face the road.
- *The lighthouse:* the lap round its islet only came within the clearing's reach -- 5.5 cells, at sea level -- and its blend cut the hill at the door 2,000 down. Kept, with its ground: it stands, and so does the hilltop by its door.
- *The pharmacy and the baggage claim:* the bridge's ramp up the rampart runs 2.3 cells into its back (the island's edge leaves the ramp no room further west). The build now makes such a building shallower on that side (`trimKept`, `RaceTrackBuilder.TrimKept`): its pieces' bodies are copied with their points squeezed toward the far side -- the front, with its doors -- along the decor's own axis (its turn found by matching the body's box to the decor's), the copies go on the end of the island's OBL, and the decors' boxes are squeezed with them. The road's surface has to run half a cell or more into a building for that; one it only brushes -- a corner, a door facing the road -- is left as it is. Both doors work (Twinsen walked into the baggage claim).
- *The sewer's hut, the neighbour's house:* kept (the neighbour's 0.7 cells further from the lap, the road's points moved along its east side).
- *Not kept:* the museum (the bridge's ramp runs over it), the shop (the dock loop runs through it at street level, twice), the school (the lap runs through it), and two of the fine weather's own (no doors).
- A general push of the lap off kept buildings, as `KeepOnIsland` pushes it off the island's edge, is there too (`keepClear`, `KeepClear`: never through a building, at most 3.5 cells a round, not into another) -- but the town circuit doesn't use it: moving its dock loop moved its bridge, and the bridge's ramps then ran over Twinsen's house.

**The jump on the town circuit's bridge.** Citadel Island's scenes carry both weathers' tracks, so the storm track's jump zone stood in scene 42 in fine weather too, and raised to the rampart's height its reach took in the town circuit's bridge. The storm track's jump is now carried by the race-track mode of its own file only; and a jump's controller on an island with a track in each weather acts only in its own (`RaceTrackIsland.OwnWeather`: game variable 206, the controller's `&& w == var_game(206)`). The town circuit's test pilot runs its qualifying lap over the bridge and into the race with no jump.

**Mr. Paul and a race started on the line.** His stop (no gloves, on the start line) belongs to the story; a race started on its line has no gloves gate, and he put Twinsen out of his car there too. The race-track mode now sets game variable 209 while a gate of the track is shut (`RACE_GATE_VAR`), and he only stops a car then.

Checked (sandbox `E:\dump\TEMP\pbig2`, muted): the storm lap with the test pilot, the jump off the deck and onto it; the buildings in both weathers (pictures above); Raph lapping, stopping, his lines; Mr. Paul in the story, and not on a race started on the line; the beaten time to the lighthouse, the spell and chapter 2; the town circuit's qualifying and race over its bridge.

## A penguin's blast that left Twinsen alone: power-ups and a loaded game (2026-10-06, late)

The user: Twinsen can be hit by a penguin and not feel the blast -- are we resetting him properly once the protection spell or the super jet-pack wears off?

**When they wear off: yes.** Checked with two new console commands that make the case without a race's chance in it -- `raceitem <0..5>` uses a power-up (0 fuel, 1 protection, 2 lightning, 3 penguin, 4 super jet-pack, 5 oil) and `raceblast [cells]` sets off a penguin's blast that many cells ahead of Twinsen's car (1.5 if left out) -- on the storm track with the test pilot driving and taking no items itself (`autodrop_ms=0`): a blast stops the car; under the protection spell it doesn't; when the spell is over (30 s) it stops the car again; under the super jet-pack it doesn't; when that is over (10 s) it stops the car again. Across cube borders too. His armour is 0 in every scene (`heroarmor <game folder> 0 242`), so a blast's force of 20 always gets through.

**After loading a game: no.** The race mode keeps each power-up's end as a time on the game clock, and loading a game sets the clock back to the save's (`SetTimerHR(savetimerrefhr)`, SAVEGAME.CPP). A protection spell or a super jet-pack taken after the save then ran on, unseen, until the clock had caught up again -- its sparkles gone with the load, but Twinsen's car left alone by every blast (the log: "the blast leaves Twinsen's car as it is: the protection spell", in a game with no spell on), and the jet-pack's hand still on the wheel. A save made under the spell came back with the game's own spell on (the save keeps the protection's extras, the game turns the spell on from them) and nothing to turn it off.

**The fix.** A loaded game has no power-up from before it (`ForgetPowerUps`, called by SAVEGAME.CPP once the save's clock is set, `RaceMod_GameLoaded`): the spell and the jet-pack over (a protection spell the save had on while Twinsen drives with them), the item box empty, every car's boost, skid, shrinking and stop over, the penguins and oil slicks gone, the mushrooms back, Twinsen visible and his own size. (v2026-10-06j also did this whenever the game clock went back by more than a second; that went in v2026-10-06k: the game pauses its clock for every dialog, menu and scene load by setting it back to where it was, `RestoreTimer` in TIMER.CPP, so a pause would have taken Twinsen's power-ups away.)

Checked (sandbox `E:\dump\TEMP\pbig2`, muted, `--fixed-dt 20`): save, protection and super jet-pack, load, blast -- the car stops; protection, save, load, blast -- the car stops, the hero's NO_CHOC off; the wear-off run above unchanged.

## The penguins again: the protection spell's bubbles gone with a jump, and Citadel's shop in the fine weather (2026-10-06, night)

The user, after v2026-10-06j: penguins still go off at the car and do nothing, with no game loaded at all -- from the Polar dream into the Citadel storm track; and the shop's door is missing its decor.

**Why the blasts did nothing.** The user's own Play log (`release\lba2-play\adeline.log`) says it: each blast that left the car alone was "the blast leaves Twinsen's car as it is: the protection spell", the spell used earlier in the same lap. The race's protection lasts 30 seconds, nearly three laps of the storm track, and the spell's bubbles round the car are what tells the player it is on -- but they were gone long before that:
- the game turns its spell off whenever a script takes Twinsen's moves (GERELIFE.CPP `SET_DIR`, `ToggleSortProtection` when the hero's move isn't manual), and the storm track's jump is such a script: its flight sets his moves at the take-off and again at the landing;
- a scene's change puts the spell back (OBJECT.CPP `ChangeCube`) and then clears every extra with the hero's animation kept (`ClearExtra`), so the spell stayed on with no bubbles.

The race mode's own 30 seconds ran on regardless, so the car came out of the jump unprotected to look at and untouchable by blasts, oil and lightning.

**The fix.** While the race's protection has time left and Twinsen drives, the spell is put back whenever it is off or its bubbles are gone (`PowerUps` in RACEMOD.CPP, logged "the protection spell put back: N ms of it left"). Checked on the storm track with the test pilot: the spell put back at the jump's take-off and landing and at the scene changes to 48 and back to 42, the bubbles there in every picture up to the 30 seconds and gone after; a blast under it leaves the car alone, one after it stops the car.

**Citadel's shop in the fine weather.** On the storm track (CITADEL) the shop is the original's, its door and porch included: the raised road passes over it (pictures taken in the same place, the same camera, before and after the build, match but for the deck and its pier). The town circuit (CITABAU) took the whole shop away -- the circuit's bridge and both its streets run through where it stood -- but its door stays (a door is never removed: the cars can't use a building's door), so Twinsen walked into the shop from the open road, beside a strip of sea (the hole in the ground the shop's floor covered). The shop is pieces sharing their inner sides: the one with the door (body 90, x 512.6-523, z 602.4-608, top 2,427) stands between the two streets and under the bridge's deck (3,500). It is now kept, with its two neighbours as its walls: a new plan option, `thinKept` (`RaceTrackBuilder.ThinKept`), squeezes a kept piece toward one side to a few tenths of a cell -- the shop's north piece (89) to its south side and its south piece (91) to its north side, a quarter of a cell each, so their outer faces, the building's own north and south walls, stand where body 90 was open; the porch's back wall (95) closes the rest of the north side. The porch and its stall stood on the north street and stay out. The streets' middles are 4 to 4.4 cells from the pieces (their curbs clear), the opponents' lines 1.3 cells or more.

Checked (sandbox `E:\dump\TEMP\pbig2`, Citadel built with `buildingprobe`): the shop from its door, from the south street and the west; the door's zone where it was.

## The super jet-pack off Polar Island's terraces, and back on the road from the sea (2026-10-06, night)

The user: on Polar Island, with the super jet-pack, Twinsen sometimes leaves the track and ends up in the water.

**Where.** The dream sprint driven by the test pilot with a super jet-pack every 11 seconds (the user's own race file, a sandbox with their built files; `E:\dump\TEMP\pjet`, `run.sh`, `trace.py`): at 31 s the car is on the road up LBA1's tower 108, 8,200 up, coming to the hairpin at the terraces' west end, where the road turns from west to north at x 326.5; a second later the engine has it in its phantom cube (94) -- the sea round the island, where no scene is -- at sea level. It never made the turn: it turned at its steady rate all the way, on a circle of about 9 cells, and ran up the bank beside the road and over the island's map edge at x 320.

**Why.** The jet-pack drives the car along the line at twice the line's own speed (the speed planned for the car's own steering), and BUGGY.CPP only quickened the steering above the top gear's speed. The dream car's top is 120 km/h; through the hairpin the jet-pack had it at about 100 -- twice the line's speed, still under the top -- so it steered as slowly as ever and turned a circle twice as wide.

**The fix.** While the jet-pack drives, the car steers as much quicker as the jet-pack makes it go (`RaceMod_JetSteer`, used by both of BUGGY.CPP's speed functions: the larger of it and the over-the-top factor): the line's own circles, at its speed. Polar's sprint under jet-packs every 11 s, at two timings and with the pilot's own items too: the car keeps to the line through the hairpin and up the terraces, wins (33-50 s) and wakes up. The same under jet-packs on the other tracks, old engine against new: the storm track, the lava lake, Mosquibees Island, the Elevator Platform and the Desert lap as many laps, as fast or a little faster, and nothing is put back on the road.

**Back on the road from the sea.** A car that goes into the sea or the lava is put back on the road (`RaceMod_Rescue`) -- but only at a line point in the scene it is in, so a car out of that scene (into the phantom cube, or a neighbour's sea) had the drowning put off and was never put back: it stayed in the water. The race mode now remembers the scene the car was last safe in, and a car out of it is taken back to it: the scene changes and, once it is in, the car is put at the safe place on the line (`RescueArrive`; the scene change's own place can't be used -- asked for from inside AffScene, the frame's end clears FlagChgCube first). Checked by pushing the car out of scene 242 into scene 241's sea and out of scene 243 off the island's map: "off the road out of scene 243 (in 94): back to it", "back on the road in scene 243: line point 1206", and the pilot drove on up the terraces.

## Citadel's shop door in the game, and the rampart over the storm track's deck (2026-10-07)

The user, with pictures: the shop's door still isn't right in LBA Assembler, though the game outside it shows it right from the same folder; and the storm track still has ground over the road.

**The shop's door.** The door's window is two polygons of their own, 50 units in front of the door (OBL body 60 in CITADEL, the shop's door piece), of type 12 -- `POLY_TEXTURE_SOLID_INC`, a texture whose colour 0 is see-through: the shop's sack, drawn over the window. Their texture handle is `0xFFFF0000`, the whole page with its UVs anywhere on it (the door's own wood and grille use the same handle, in types 16/17). The engine's `Triangle_Texture_Solid`/`Quad_Texture_Solid` (LIB386/OBJECT/AFF_OBJ.CPP) took that value for an unassigned placeholder and drew the polygon's flat colour instead -- a fallback made for the body preview, where some BODY.HQR bodies drew nothing (2026-09-24) -- so in the game the sack was two flat blue-grey panes. The fallback is now the body preview's only (`g_previewFlattenTransparent`, set around its own render); in the game the handle draws its texture, as the retail game does: the sack is in the window. (Every type 8/12 polygon with that handle was drawn flat in Play and in the editor's 3D view until now.)

**The ground over the deck.** Measured along the storm track's raised road (every ground point within the deck's half width against the deck's height along its segment, `poke.py`): 13 points over the deck, all at its two ends -- up to 700 over its north edge where it comes down beside the north rampart (cells 522-528, z 522-524: the rampart's slope and rock, the user's picture), up to 111 at its foot by the harbour. The lava lake's plan already cuts such ground (`raisedCut`, `CutUnderRaised`: the ground near the deck cut to under it, and under each end the ground made the deck's own surface out to its rails, `FlushRaisedEnds`); the storm plan now has it too. The cutting now leaves the kept buildings' ground alone (`keepGroundUnder`), except where it stands up through the deck: unbounded, it raised the baggage claim's floor 231 at the north end. Built (`buildingprobe`): no ground over the deck, every kept building and its ground as before, the fine weather's files and the storm file's objects byte-identical; the pilot laps it (12 s, and 7 s under jet-packs), every jump, nothing put back on the road.

## The Island of the Francos: over the dock, the refinery and the village (2026-10-07)

The user: a track for KNARTAS.ILE, through its three parts -- the village, the refinery and the dock; the village's houses made bigger and Twinsen driving through some of them; lots of pipes and steam in the refinery, and oil dripping in places; the port's ideas left to me. Then: loads of height, lots of loops and jumps, a longer lap than the flat ground allows.

**The lap** (`tools/RaceTrackPlan/knartas_design.py`, run in `E:\dump\TEMP\knartas` with the island's heights and decor boxes; `docs/racetrack/knartas_track_plan.json`; `RaceTrackIsland.Knartas`, scene 107): 598 cells, nearly all of it a raised road at three levels that passes over itself -- the dock's road at 3,400 (over the piers' railings and crates), the high road at 6,000 over the refinery's buildings and the top road at 5,600 over the rocks north of it -- with the village's stretch down among the huts. It starts on the dock's east arm, loops over the north arm, jumps the inlet's mouth past the rocket (a carried jump, 55°), climbs east along the south arm over the start straight and the channel, loops between the refinery's tanks, runs east among the pipes, leaps the cracking tower (22°) into the village, drives through four huts, loops along the south shore, jumps the compound's east fence (30°) and climbs to the top road over the rocks (two loops there, the second with a 35° gap at its top), then comes down the channel over the little bridge to the start. Five loops (the engine's limit was four: `RACE_MAX_LOOPS` is 8 now) and three carried jumps; the steepest grade 18.6 %. Drivers: De La Fontaine (the tanker, the one to beat), Mr. Kurtz (the laboratory) and the nurse (the pram).

**The port.** The dock's road runs over the piers high enough to keep their railings (an earlier 2,600 took them off); the airships stay moored beside it, the rocket stands under the inlet jump, and the start's grid waits on the east arm with pit spots under the straight's west edge (with no pit lane, grid spots ran back round the U-turn and sat on the road while the player qualified).

**Through the village's huts** (`RaceTrackDriveThrough`, plan `driveThrough`: a hut's body and how much bigger). Each hut's pieces (the decor group sharing its origin) are scaled up round the road and cut where it runs: every face over the road's corridor below the deck plus 1,900 is dropped, the rest split into a left side, a right side and a roof, each a new OBL body of its own with its own box (the road's passage left clear), and every face made two-sided -- from inside, the dome's inner side shows. The parts are kept as they are by the clearing (`asIs`: the kept-box cut would flatten their boxes and the adrift-decor clearing took them). Four huts: 88 ×1.4, 81 ×1.6, 84 ×1.3 and the village's middle cluster (76-79) as it is; the road runs under each dome.

**The refinery's pipes** (`RaceTrackPipes`, plan `pipes`: stretches of the lap, a gantry every few cells). Each gantry: two grey pipes standing on the compound's floor outside the rails, up 2,900 over the deck with red vent caps, a cross pipe over the road 2,200 up (its box the only one: nothing else is in a car's way), and a pipe along each side to the next gantry; a lit body (greys ramp 48, reds 64). None stands where another part of the lap passes through an upright or over the cross pipe. Five gantries over the high road (one left out in the widened loop deck on the top road beside it). The race-track mode (`steam=`, `drip=` in the race file, `Vents`):

- steam puffs from each upright's top, each on its own time (every ~0.9 s), rising and growing as it fades -- the game's own smoke puff (pof 1), at most 16 at once, only near Twinsen;
- every other gantry drips oil from its cross pipe onto one side of the road (alternate sides, 45 % of the way to the rail): a drop falls (seen near Twinsen) and where it lands a slick lies for 9 s unless a car takes it -- the oil power-up's slick and skid, the slicks shown by the scenes' own oil actors (only where one is free, never one on the road unseen, never a dropped one replaced: `RACE_MAX_SLICKS` 10). The pipes drip wherever Twinsen is, so a slick may be waiting when he comes.

Tested with the test pilot (`pilot.sh`): the qualifying lap 73.3 s, every loop and jump taken, nothing put back on the road; slicks lying under the pipes off the car's line.

## The Island of the Francos twice its size, in three looks, with loops on the level (2026-10-07, later)

The user: drop the vertical loops; more height, and more loops but not vertical ones; the whole island bigger, maybe 4 times its size, everything scaled up; more steam -- the Gazogem factory's steam that hits Twinsen -- and more oil drips; three distinctly themed sections.

**Twice its size each way, four times its area** (`Terrain/IslandScaler.cs`, `RaceTrackIsland.Scale`/`MoreScenes`; done by the build, from the originals, before anything of the track's). About the corner of the island's first cube, everything goes twice as far and twice as high:

- the ground: each cell becomes 2 x 2 cut the same way, each triangle inside one of the old cell's with that part of its texture -- the island looks as it did, twice as big; heights doubled, the light, the game codes and the water depth as they were;
- the objects: twice as far and as high, their boxes doubled, and every body of KNARTAS.OBL twice its size (box, points, spheres; none past what a body holds);
- the scenes: 107, 108 and 109 become 12 -- each keeps its number for the cube its Twinsen starts in, the other 9 are scenes 255-263. Zones, track points and actors go twice as far into the new scene of the cube they are in (a zone into each it reaches); an actor not in a scene's cube stays in its list as an inert stand-in (out of sight, no scripts), since scripts name actors by their place in it; the cube changes between the 12 cubes are made afresh; and every other scene's way onto the island (the factory's and the houses' doors out, the guard post's) leads to the same place, now in its new scene;
- the holomap: its camera's target twice as far and its distance doubled -- a perspective, so the game's own picture fits the island as it is now, the track drawn into it and Twinsen's place too -- and the island's scenes' arrows moved with it.

Scenes past 254: the engine's holomap arrows (`TabArrow`, kept in the saves) stop at `MAX_CUBE` 255, and the exterior edge crossings read a scene's arrow flags (`GereZoneChangeCube`: bit 2, an exterior scene). `CubeArrowFlags` (HOLO.H) gives a scene past it the flags of an exterior cube with no arrow; the holomap's "where is Twinsen" takes the same. Nothing else in the engine is sized by scene number. (Scenes 222-254 are all the race tracks' already: the story's arrow, the lava lake, Sendell, the moon, the Emerald Moon, Polar Island.)

**The lap** (`tools/RaceTrackPlan/knartas_design.py`, now on the scaled island: `scaleisland` in a sandbox, `islandheights`, `decorpoints` + `obstacles.py`): 1,400 cells, 2,700 to 16,700 high, steepest grade 16.9 %; three carried jumps (over the rocket at the inlet's mouth, off the refinery's north-east corner over the cracking tower down into the village -- topping out at 16,700 -- and past the refinery's east fence up onto the climb); no rings. Its three loops lie on the level: a **cloverleaf** -- where the lap would turn one way, three corners turning the other way take it round 270 degrees and over its own way in, 2,700-2,900 above it there: over the sea off the dock's north-west corner, over the refinery's south fence (up onto the high road at 14,000), and over the village's hills between its first hut and its middle.

**Three looks** (`DeckTheme`, plan `themes`: stretches of the lap by its points; `RaceTrackRaisedBody.Tile`/`Pier` take one):

| Section | Deck | Curbs | Rails | Piers | Arrows |
|---|---|---|---|---|---|
| dock | dark wood planks (23) | blue and white (201/63) | wooden (28/25) | wooden piles (24/20/27) | white |
| refinery | dark steel (52) | yellow and black hazard (108/49) | red (70/67) | steel with red caps (57/54/70) | yellow |
| village | sandy earth (104) | green and white (134/63) | olive (121/118) | brown with olive caps (100/97/120) | orange |

The dock's stretch is its piers, the sea loop, the inlet and the south arm, and the channel down from the rocks back to the start; the refinery's from the channel to the tower leap; the village's from the leap's landing round the huts, the south shore and the climb over the rocks.

**Steam, the factory's own.** The Gazogem factory's steam (its rooms' scripts: `set_hit_zone(n, 10)` with `impact_point(n, 37)`) is impact 37. The race-track mode now blows it out of the road (`jet=x y z reach on off phase`, `Jets`): each jet blows in bursts (1.3 s in every 3.5), one after another down the road ahead of a car, on one side of it and then the other; a car in one while it blows is hit as by a penguin's blast but held 0.9 s ("Scalded by steam!") -- not with the protection spell or on the super jet-pack. 14 jets along the refinery's roads (plan `steamJets`), and the pipe gantries over them -- 16 of them now, 32 vents puffing.

**More oil.** Every gantry drips now (`dripsEvery` 1: 16 drips), the scenes have 5 oil slicks each (3 until now) and the race-track mode 16 slicks in all (10).

**The village's huts**, twice their size with the island, are driven through as they are, half way up their domes; a roof piece's box is now over the deck's highest point under it (the road climbing out of the village's middle ran into the cluster's roof).

**Tested** (sandbox `E:\dump\TEMP\kbig`, `pilot.sh`, start scene 257): qualifying 143.8 s, the race's laps 156.1, 147.4 and 158.1 s, the three opponents 145.6-157.0 s; every jump carried, nothing put back on the road. The hazards hit everyone: in the race the opponents skidded 24 times and were hit by steam 31 times, the test pilot 21 and 12. (A first run had the nurse parked on her grid spot in front of the line while the player qualified: of the plan's three waiting spots one was in the next cube, and only the start scene's are written -- all three are in its cube now.) The build through the window's path (`buildtogether` with Polar Island and Citadel Island, then `buildhere` again in the same folder) makes the island bigger once, from its originals; the holomap shows the game's own picture with the lap drawn on it where it runs.

## The Island of the Francos: shorter leaps, steam across the road, the pipeline to the air-boat, oil that stays on the road (2026-10-07, night)

The user: two of the jumps far too long, the one over the air-boat only just okay -- shorten the other two substantially, and the boat's a little by bringing its landing closer; oil at some points close to the ground passing through the track onto the floor below; the steam sideways, out of the left side on one lap and the right on the next; and a massive pipe above the track from the Gazogem factory to the air ship, dripping oil at fixed places where it is over the track.

**The jumps** (`knartas_design.py` `JUMPS`): the leap over the factory 76 cells to 42 (its ramp moved on to the factory's north edge, its landing just past it), the leap past the refinery's east fence 60 to 30, the jump over the air-boat 35 to 32 (its landing 3 cells closer). A jump can now have its landing lip's height and its landing hill's foot of its own: the factory leap lands at 7,600, clear of the factory's back, and its carried landing hill takes it down to 5,600 -- steeper than a road may be, but the race-track mode carries the car down it with the flight -- so the road comes to the first hut low enough to go under its dome. The road is level to the leap's ramp.

**Steam across the road** (`jet=x y z half reach on off phase dirx dirz`, RACEMOD.CPP `Jets`): each jet stands on the road's middle and blows out of its rail across it -- out of the driver's left on the player's odd laps (the qualifying lap and the race's first and third), his right on the even ones; a burst keeps the side it began with. The steam is the factory's own impact taken apart: its hiss at the burst's start, its puffs (sprites 100-109) in a row from the rail into the road, a puff flown across the road growing as it goes, and its sparks (flow 16, its angle up taken down for the spray) blown across. A car in the band from the rail 5.5 cells across (the road is 9.5) while it blows is hit as before; the other side of the road is clear. (The first form, up out of the deck, is still read.)

**The pipeline** (plan `pipeline`, `RaceTrackPipes.PlacePipeline`): a pipe 1,400 across, teal with red flanges, out of the factory's west wall, up to 12,000 and west along the refinery, under the high road (1,000 to spare), over the channel road and the start straight 13 cells past the start line, and down into the air-boat's deck between its two hulls; 29 pieces of pipe on 9 concrete supports (none where the road passes through a support's way up). It drips oil where it passes over the road -- every 2.5 cells along it there, each drip on its own time (`drip=`): 6 drips, over the channel road and the start straight. `RACE_MAX_DRIPS` 48.

**Oil off the road.** A slick takes the deck under it -- found where it lies, at its height -- or else the ground. A slick dropped behind a car where that is just off the deck (a bend's outside, past a jump's lip) or by a car in the air found no deck and lay on the ground under the road. Now a slick that misses the deck but has it within 1,100 at its height is moved onto it, a slick's width in from its edge; one with no deck near it that would lie more than 900 under where it was dropped is gone (`PoseSlick`). And the opponents waiting on their spots while the player qualifies are no longer skidded or scalded where their lines would have them.

**Tested** (`E:\dump\TEMP\kbig`): qualifying 145.0 s, the race's laps 153.1, 162.0 and 149.7 s, the opponents 146-182 s; every jump carried, nothing put back on the road. The steam out of the left rail on the race's first lap and the right on its second (pictures); with it on one side of the road the test pilot, down the middle, was scalded 6 times in four laps (12 before). A lap with the pilot dropping oil every 2.5 s: every slick on the deck. (The case the user saw wasn't met again; the race-track mode's log says "a slick off the deck's edge moved onto it" or "... with no road under it: gone" when it happens.)

## The Island of the Francos: the factory's horizontal steam, shorter leaps still, Gazogem fuel all race; the editor's whole-island view (2026-10-07, late)

The user, with pictures: the steam jets still show vertically -- the Gazogem factory has horizontal steam jets of its own, use those; the two long jumps still too long; remove Gazogem as a pickup and have it on the cars all the time on this track only; and in the editor, moving a map shifts it against the background and can leave part of it off the screen.

**The factory's horizontal steam.** The factory's rooms play two steam impacts: 37 (a jet straight up, sprites 100-109, flow 16 -- what the race-track mode had used) and 38 (a jet out of the walls on the level, sprites 110-118, flow 76, the hiss sample 17). The jets now play 38's: its hiss at the burst's start, its jet at the rail and a third and two thirds of the way across, and its sparks -- flow 76 already sprays on the level, the way it is turned (the road's other side). The sprites are drawn blowing to the screen's right only, so a jet that blows to the left from where the camera is is drawn mirrored: `EXTRA_MIRROR` (bit 30 of an extra's flags) has OBJECT.CPP draw a mirrored copy of the raw sprite (`MirroredRawSprite`, made once and kept), placed so it mirrors about the extra's place. Their scale 22 (the factory's 35 is for its rooms' close cameras): the jet reaches over the band it hits. Out of the left rail on lap 1, the right on lap 2 (pictures).

**Shorter leaps.** The leap over the factory 42 cells to 24 -- its ramp now on the high road over the factory's roof (1,637 over it), landing just past it -- and the one past the refinery's east fence 30 to 18. The jump over the air-boat stays at 32.

**Gazogem fuel all race** (`RaceTrackIsland.FuelAlways`, the race file's `fuel_always=1`): no mushroom gives it, and every car has its boost all race -- the player's car 1.5 times its top speed and pull (the super jet-pack's own speed while it drives), the opponents 1.35 times their pace; the test pilot drives the line at the boosted speed. A lap is now about 95-100 s.

**The editor's view of a whole island.** The native view drew a square of 5 x 5 cubes round the cube under the camera's target (`AffGrilleExtWide(2)`), the sea with it: moving the view moved that square -- the sea under the island -- and an island wider than it lost its far cubes (the Island of the Francos twice its size is four cubes across: with the camera over the dock, the village and the refinery's east side weren't drawn; over the village, the dock wasn't). Now the renderer draws a rectangle of cubes whichever the camera is over (`AffGrilleExtArea`, `lba2_renderer_render_frame_area`), and the editor gives it the island's cubes and a ring of sea two cubes wide round them (`IslandCubeArea`): the whole island always, the sea fixed under it. The actors, routes and zones drawn over the view take the same rectangle. (`polarview` takes `AREA=x0,z0,x1,z1` and `FAR=` to compare.)

**The oil through the track, found.** A full race logged it at last: a slick 1,057 across from a deck's middle, inside its width, yet with no deck under it -- past the deck's end, over a jump's gap: a car that has just landed drops its oil 1,300 behind it, back over the gap, where there is no road and the sea or the ground far below. The earlier fix only moved a slick across onto a deck beside it; now a slick that misses the deck is put on the nearest place on it -- across and along, at a jump's landing lip -- and one that still has no deck under it is gone.

## The Island of the Francos: steam out of the gantries' pipes, the camera behind the car under the huts; moving the editor's view past an island's edge (2026-10-07, later still)

The user, with a picture: the steam looks like it comes out of the track -- have it come out of the vertical pipes on the track, fully horizontally; driving under parts of the houses forces the camera to look straight down until Enter puts it back behind Twinsen -- keep it behind him. And, with the editor's view now pinned to the island: it is hard to zoom in on some parts of an island -- let the whole thing be moved so that any part of it can be brought to the middle of the view.

**Steam out of the uprights.** A steam-jet stretch (`steamJets`) now blows out of the gantries in it (`RaceTrackPipes.Place` takes the jet stretches): each of their uprights has a nozzle -- a short pipe towards the road with a red mouth, `SteamJetRun.High` (700) over the deck -- and the jet record (`jet=`, an 11th number: how high over the deck it blows, `RACE_STEAM_HIGH` 380 without it) starts at the nozzle's mouth, the upright's distance from the road's middle less the pipe and the nozzle, and reaches `Reach` cells into the road from its rail. Left upright on lap 1, right on lap 2, as before. A stretch with no gantry in it still blows out of the rail. Two things kept them from being seen or felt, both found with the pilot: the pipes' tops puffed into all 24 of the steam's extras (nine gantries in reach, eighteen tops), so the jets drew nothing -- the tops now take 12 at most (`RACE_VENT_PUFFS`); and each jet blew a `Wave` (450 ms) after the one before, which is a Gazogem-fuelled car's own pace from gantry to gantry (6 cells in 420 ms): a car that came between two bursts rode between them the whole stretch. Each now blows a `Wave` *before* the one before it -- the burst runs back up the road at the car: in a qualifying lap and a race lap the pilot was scalded twice a lap, and the opponents too.

**The jet's look, fixed** (the user, with an in-game picture and a drawing of what was meant: a narrow plume out of the nozzle, level across the road). The factory's wall jet (impact 38's sprites 110-118) is drawn on a slant for the factory rooms' cameras, and it blows to the screen's *left* from its right edge (its hot spot is its right edge: -58 in a sprite 60 wide) -- the mirroring had it the wrong way round, so out of a left upright it billowed outwards, away from the road, and drawn as big as the whole reach. Now the jet is the factory's other one, its burst straight up (impact 37's sprites 100-102: 102 is a narrow-rooted plume 27 x 71) laid on its side: an extra with `EXTRA_SCALE_TURNED` (in its `Scale` -- every bit of an extra's `Flags` is taken) and its way in `Vx, Vy, Vz` (unused for an animated sprite that doesn't fly) is drawn by `DrawTurnedExtra` (OBJECT.CPP) from a turned copy of its sprite (`TurnedRawSprite`, made once and kept, its right side up either way: the plume rises a little), its root -- its hot spot -- on the nozzle's mouth, as thick as its scale makes it there and as long on the screen as the way to its end is (shorter where the jet runs towards the camera), to the screen's left or right as that way goes, worked out every frame. The race-track mode gives it a scale that makes 102 as long as the jet's reach (71,000 / reach), so the steam covers just the band that scalds; at a burst's start it grows from 100 and 101 (240 ms). Out of the left uprights on lap 1, the right on lap 2 (pictures), the car scalded as before.

**Through the track, and stuck under it.** The user, with a picture: Twinsen went through the track as lightning and steam struck. Their Play log (lap 21 of a long race, the protection spell on, so neither the lightning nor the steam touched the car) has the car landing from at least 2,048 down ("the car is hit, but protected" is also what a protected landing from a fall of 8 bricks says) on the high road between checkpoints 4 and 5 -- just after two of Twinsen's penguins had stopped Mr. Kurtz's car dead in front of him -- and no checkpoint of Twinsen's after it: the engine, left running when the editor was closed, was asked over its control socket where the car was (`status`, `dumpstate`): on the ground 10,800 under the high road, inside the bend where it turns east (cube (9,7), cell (633, 497)). What put it off the road was not found: four laps of a stress race (every mushroom a protection spell, lightning, penguin or oil, used at once) never left the road. But nothing would ever have put it back -- the rescue was only for lava and the sea. Now (`FallenBelow`, RACEMOD.CPP) a car on a raised-road track that has stood RACE_BELOW_MS (1.5 s) off the lap -- no point of the line within 4,500 of it at about its height -- with the line's nearest point within 16 cells at least 1,500 over it is put back on the road at its last safe place, as from the sea (not in a jump's flight, a loop, the super jet-pack's or a fall still going on; the line, not the raised road, so that a lap on the ground beside a raised part, the Desert island's, is the lap). Tested by teleporting the pilot's car to that place mid-lap: put back 1.5 s after it landed, and on to the next checkpoint.

**The camera under the huts.** The trace (`camtrace 1`) showed the camera's pitch going from 300 (the default, behind the car) to 434-714 and staying there. At every cube edge the lap crosses, the scene change re-centres the camera (`CameraCenter(1)`), and the classic `SearchCameraPos` lifts the eye over any decor's box it finds it in -- a drive-through hut's roof, a gantry -- and steepens `AlphaCam` to look down from there; the follow camera takes its pitch from `AlphaCam`, so it kept looking down until the player re-centred it. With the race-track mode's follow camera on, `CameraCenter` now leaves that search out (INTEXT.CPP): the follow camera keeps its own clearance over the ground and the raised road. A whole lap and a race lap: the pitch never left 300.

**Moving the editor's view past an island's edge.** The view looks at a point 10,000 up, so the ground in the middle of the view lies some 20,000 beyond that point at the usual tilt; and the view's middle had to stay over a cube with land (`IsWorldPositionOnIsland`) -- the renderer couldn't place its camera over open sea, `set_view_target` failed there -- and the pan scrollbars' range was the island's cubes. So an island's edge on the camera's side could never come to the middle. Now the middle may go a cube out past the island's cubes (`IsWorldPositionInView`, `PanRing`; the scrollbars' range too), within the two-cube ring of sea the view draws; and the renderer, given a target over open sea, takes the nearest cube with land as the frame its camera is placed in (`lba2_renderer_set_view_target`). Checked in the editor with ASCENCE.ILE (one cube): the page buttons now take the view a cube out either side, where they stopped at the island's own cube.

## Otringal: through the tunnel, over the town and round the palace (2026-10-08)

The user's sketch over the editor's minimap: "Green is the track, pink are jump points and the red is intended to be a tunnel to drive through and the orange line represents a pit lane". The minimap was turned a quarter clockwise and stretched along the island: sketch x = 1141 - 1.47 * (render y), sketch y = (render x) - 23 on a render 4 pixels a cell from cell (384, 448) -- found from the square island, the pier and the dome island. The green was thinned to its middle line (skimage skeletonize, traced end to end between the pink dots) and mapped to cells (E:\dump\TEMP\otr\sketchfit.py, centreline.py; prep.py makes the design inputs from `islandheights`, `decorpoints`, `decorlist` and `islandrender`).

**The lap** (tools/RaceTrackPlan/otringal_design.py, `RaceTrackIsland.Otringal`, island 4, scenes 87-92), 660 cells, all of it a raised road: round the square island at 5,000; west over the bridge through a tunnel; up through the town over its roofs (the climb's grade 24 % at the most) onto the straight along the palace's east side at 14,000 -- the start line, the pit lane on its left between the race lanes and the palace (the palace's box ends at x 488.5); west along the north edge; south down the west side (14,000: east of it the ground is a cliff of 12,700-13,700, and cube (6,7) isn't there to go round it) and a jump over the inlet (9 cells); an S over the town's tanks at 13,200; down past the west pier over the sea at 7,400 to the south-west islets; along the south coast and a jump over the sea onto the square island (20 cells). Qualifying lap with the test pilot 66.5 s.

**New builder features:**
- **A scene for a cube the game has none for** (`RaceTrackIsland.NewCubes`, `IslandScaler.AddCubeScenes`): Otringal's south-west islets, cube (6,9), have ground but no scene -- the lap drives over them. Scene 264 is a copy of scene 87 in that cube: Twinsen on the ground nearest its middle, its actors inert stand-ins (scripts use them by number), its track points kept, cube changes to its neighbours, and one into it from each neighbour.
- **Tunnels** (plan `tunnels`: [{from, to, roof}], `RaceTrackTunnel`): pieces every 4 cells, each a decor -- a wall just outside each rail from under the deck to the roof, a gold light strip along its inside, and the roof from wall to wall, all boxes with their faces outwards; their decor boxes untouchable (top far under bottom): one box round walls and roof took in the road, and the car stopped dead at the tunnel's mouth. The race file's `tunnel=<from> <to> <roof>` (the raised road's points).
- **Longer raised pieces** (plan `raisedPiece`, cells): the town's and the palace's cubes are near their 200 decors already, and pieces of 4 cells left 20 out; Otringal's are 10 cells.
- **Game variables held while racing** (`RaceTrackIsland.HeldVars`, the race file's `vargame=<n> <value>`): the square island's own script for Twinsen plays the Dino-Fly's landing whenever variable 83 is under 2 -- cinema mode, Twinsen moved to its pad until the Dino-Fly comes in -- and the car that flew into scene 92 off the jump stood there 11 s. Otringal holds 83 at 3.

**The tunnel camera** (RACEMOD.CPP `TunnelCamera`, first in the race camera chain after the jumps' and loops'): from 40 points before the tunnel's mouth to 16 past its end, the rail camera's eye -- on the road 4,200 behind the car, 1,500 over it (under the roof), looking at it. Shortening the follow camera's arm wasn't enough: it looks at a point well ahead of the car (DISTANCE_VISEE), and a short arm put the car out of the bottom of the view.

### Version 2: close to the ground, the game's own tunnel, the spaceships (2026-10-08)

The user: "re-do the Otringal track so it's closer to ground level. There's also a space ship that constantly lands and takes off from the track, let's leave this in as a feature but perhaps have it hover for a few seconds, land for a couple of seconds before rising straight up again. The tunnel that we've added was intended to be the in-game tunnel area ... driving down the in-game slope, through a short tunnel then up back onto our track".

**Heights from the ground** (otringal_design.py): the deck 400 over the median of the ground across it (a rock at one rail is cut away by RaisedCut), at least 900 over the sea; floors spread out at 17 % each way (the deck rises before a rise in the ground), ceilings likewise, and the deck the lower of the two -- where a ceiling wins the ground is cut, a cutting. Steeper (30 %) from the top of the square island's slope down into the tunnel, and from the bridge up into the upper town: the town is three terraces (the lower town 3,900, the upper town 7,600, the palace's plateau 11,200) with cliffs between them, and the bridge's floor is at 1,000. Heights 1,000 to 12,400 (were 5,000 to 15,100); typically 900 over the ground. Lap 669 cells; qualifying lap with the test pilot 57 s.

**The game's tunnel.** In the game the slope at the square island's north-west corner (the ground 3,200 down to 900) leads to a door in a façade (body 144), into the bridge -- an inside scene (86) -- and out through the gatehouse (112, another inside scene, 78, with stairs up the cliff) into the lower town. The road now does the same: down the slope (cut a little deeper and wider), through the façade cut open (`driveThrough` with a `clearance` of 1,250 over the deck and a `half` width of 3.6: its lintel kept as a beam over the road), a few cells of tunnel pieces through the rock, the bridge's tube (bodies 143 and 111, 10.4 cells wide, 2,666 high at its middle, walls 1,866: cut open at its ends and its floor, its walls and roof kept, the road narrowed to 3.6 through it at 1,000), and then a sunk tunnel climbing under the gatehouse at 30 % (its roof slab level with the gatehouse's base, 3,900; the deck at most 2,430 under it at its west face, the cars 800 high), out into an open cutting and up through the lower town.
- `DriveThroughHut.Clearance`/`Half`: a lower, narrower passage than the huts' (Clearance 1,900, the road's half width).
- `TunnelRun.Pieces = false`: only the camera kept under a roof of the island's own; `Sunk`: the ground cut down only between the tunnel's walls, straight down (`CutUnderRaised` leaves the stretch alone), `Top`: the roof never over this height (a level roof over a climbing road), `Eave`: the roof reaching past the walls over the cut's edges.
- `StayPut` (plan): decor bodies that don't follow the ground (`IslandOps.DecorFollow`): the cutting past the gatehouse's west face reaches the ground under its origin, and it sank 1,650 with it.
- The tunnel camera's eye was lifted until the sight line cleared the road all the way to the car's wheels (the rail camera's rule): near the car that line is a few hundred over the road, and the eye ended 4,000 up, over the tube's roof. In a tunnel the line now ends at the car's middle (700 up) and the eye is kept 250 under the roof.

**Kept this time** (`keepBodies`): the Emperor's palace (bodies 9-14, 25, 26 -- version 1 took it away: the pit lane's edge was inside its box), its porch east of it (23, 24, 30: the straight moved to x 499 to clear it), the stair towers (69), the town's big buildings (70/71, 77), the statue under the straight (99/100, the deck over it) and the gatehouse. Re-routed for the same reason: the S's first leg (z 523.5, clear of 70/71), the pier's leg through its yard west of the warehouse (51) and north of the little house at its corner, and the islets' leg along their north shore, north of the rock (105) and the trees. Still taken away where the road runs: the palace gate's east tower and its two pillars on the straight, the tower at the plateau's north-east corner, the rows of tanks in the street of the S, fences, posts, lamps, the square island's wall where the road crosses it.

**The spaceships** (`RaceTrackIsland.Shuttles`, the race file's `shuttle=<scene> <actor> <x> <y> <z>`, RACEMOD.CPP `Shuttles`): scene 92's two ships (entity 47), actor 2 on its pad on the road along the square island's north side, actor 3 on the other pad beside the east side (the ground-level road ran into it, parked: the test pilot stood against it for the rest of its run). Their scripts are stopped and the race mode flies them on one clock, half a cycle apart: out of sight 5.5 s; down out of the sky with its legs coming out (the game's bodies 68, 63, 67, 62, 66, 61, 65, 60, 64, 58) 2.6 s; hovering 2,700 over the road (the cars pass under), its thrust flickering (58/59) 3.6 s; down onto the road 0.9 s; there 2.2 s; straight up, faster and faster, its legs going in, 3.4 s. A car that runs into one while it is down is stopped as by a penguin's blast ("Hit the shuttle!"); the pad's height is the deck's there, from the raised road's file.

### Version 3: the machine gunner, the roulette wheel, the gas monsters (2026-10-09)

The user: "We still have items on the track that shouldn't be there like a left over machine gun ... let's move it to the high ground that's just above the track at that corner, and add in a franco to fire in bursts so that the bullets only land on the inside of the corner ... The jump approaching the casino the landing is too tight ... let's have the jump land into a giant rotating roulette wheel complete with colours, numbers, and a giant white ball that rolls around for cars to avoid. Cars land in the roulette wheel from the jump, and drive around in a circle and out a hole in the bottom. In the dock area and around the rocks where Baldino crash lands let's have gas monsters coming out of the gas and attempting to bite cars, any bitten cars should be stunned for a couple of seconds".

**The machine gunner** (plan `gunners`: [{knoll, from, to, rise}], `RaceTrackGunner`; RACEMOD.CPP `gunner=`, `gunner_target=`, `Gunners`). The gun the user saw on the road was scene 88's own Franco machine gun (entity 200): the builder had moved it off the road, but its life script's `pos_point` put it back on its track point every frame -- `ShiftOffRoad` now moves those points with the actor. The corner past the start gets a knoll (the ground raised under a flat top 500 over the corner's highest deck, never under the road), both guns on it and a Franco (entity 201, anim 474: sitting at it) behind the first. In a race, every 4.2 s or so a burst: the gun's own firing animation (its kick and rattle -- FICHE.CPP `RaceMod_SilentThrow` leaves out the bullets it would throw) and 10 of the game's tracers (raw sprite 50) from its muzzle to places on the corner's inner half (25-90 % of the half width from the middle), the gun and Franco turning to each; a car within 650 of where one lands is stopped as by a magic ball.

**The roulette wheel** (plan `roulette`, `RaceTrackRoulette`; RACEMOD.CPP `roulette=`, `Wheel`, `WheelDecors`). A bowl 16.5 cells across on legs over the inlet's gully north of the casino (its middle (460.6, 520), all of it short of the casino at z 528.8; the ground under it lowered to the square's 7,600, rising steeply outside it). Its floor is a cone, 9,400 at the hole's edge (2.8 cells out) rising 170 a cell to the wall (7.95 cells out, 250 high). The jump over the inlet (now from x 454.8: the builder keeps the road 6.5 cells off cube (6,7), which the island hasn't got, and its push had moved the wheel's lane too) lands on the wheel's west side heading south; the lane (the raised road's, 2 cells either side, banked as the cone is, no deck pieces or piers: `TrackRoad.Bare`) runs round at 5.8 cells -- south, east, north-north-east -- then dives in a half turn (3.65 cells round) that passes 1.5 cells from the middle, narrowing to 1 cell over its last two, which the car is carried over: a carried jump from there drops it through the hole onto the road under the wheel (8,000), which runs out east-south-east past the casino. Two lessons: a dive straight through the middle (a 2.75-cell half turn) with a 1.6-cell lane stopped the test pilot dead where it began -- the engine keeps a car's middle 640 inside the rails, and its racing line ran outside the lane (lines now keep within a raised road narrower than the island's own: `PlanRacePath` `ReachAt`); and a carried car is now eased onto the narrower of the lip and the landing (`s_arcAcross1`).
- The wheel is all decors (`IslandDecors`), so it is seen whole from anywhere: the bowl (its wall, gold rim, wooden side and underside, the hole's wall), the wooden ring round the hole, the lane's gold walls (its inner rail, its outer where it leaves the bowl's wall; none over the hole), three legs (plan `legs`, degrees: clear of the road under it), the head in five pieces -- 37 pockets in a European wheel's order, red (71), black (49) and the green zero (134), gold frets and rings, each pocket's number in white (a 3-by-5 pixel font, its runs as quads, upright seen from outside the wheel) -- and the ball, a white sphere 360 across. The engine poses them as each cube's decors are drawn (3DEXT/DECORS.CPP `DecorPoseHook`): the head's pieces turned 24° a second against the lap's way, the ball moved (its box with it) to and fro along the lane between the race file's places, 1,700 a second, weaving across it; a car it rolls into is stopped as by a magic ball.
- Cameras: in the bowl and dropping through the hole the eye rides the lane behind the car (`WheelCamera`, the rail camera's way: the follow camera went out over the hill beside the wheel and into it, round the dive); on the road under the wheel it is 4,200 behind the car, 1,000 up and kept under the bowl (an eye nearer than the near plane, CLIP_NEAR 3,000, and the car isn't drawn at all). A carried jump with no camera of its own beside it now uses the rail camera's eye where `railcam=` is set, and the rail camera doesn't ride back over a gap the car has just flown.

**The gas monsters** (plan `gasMonsters`: [{from, to, every, out, most}], `RaceTrackGasMonster`; RACEMOD.CPP `gasmonster=`, `Monsters`). Serpents: a neck 7,000 long (teal, a yellow-green belly, purple spines) and a crocodile's head with teeth and yellow eyes, jaws open and shut -- the mushroom entity's (112) bodies 3, 4 and 5, two actors of its scene each, out of sight until raised. Five: every 8 cells where there is gas (the ground under 60) 3 cells past the rail, 9 cells apart at the least, along the lap from the docks' edge over the gas to the islets, and along the islets' north shore -- Baldino's crashed plane (body 105, its wreckage 101-104) on the rocks south of it -- and on over the gas. A car coming its way wakes one in time for its strike to meet it (by the car's speed and heading; a slow car within 5 cells too): it rises out of the gas in a puff of steam (0.7 s), rears with its jaws open and sways (0.5 s), strikes its head down onto the road's near half (0.26 s: its jaws' tip 45 % of the half width from the middle), snaps them shut -- a car within 950 is bitten and held 2.2 s ("Bitten by a gas monster!"), opponents too -- and sinks back (0.9 s), resting 2.5-5 s. The far half of the road is safe; a car just bitten isn't bitten again for 2.5 s after its stun. On a qualifying lap too (the gunner fires only in a race).

**Testing**: no power-ups in the pilot's race file (`powerups=0`) for runs that repeat; `CAR=car_np.txt` (qualifying) and `car_np_race.txt` (three laps against the opponents: everyone finished, the ball, the gun and the monsters hit Twinsen and each opponent along the way).

### Version 4: the floating wheel, the game's gas monsters, the giant hoop (2026-10-09, later)

The user: "We have an actor on the track in the spaceport area. The roulette wheel is too small, and let's have it slowly rotating, it can be just magically floating in the air without visible supports, let's have it twice as wide and over the casino location ... We can remove that building if we need to. Let's make the sides of the roulette wheel steeper so instead of a guide rail cars are slowly drawn down with gravity. The gas monsters we've added are fake ... let's just reuse the retail version"; then: "Part of otringal has a small area where dogs do tricks and jump through a hoop, let's scale up the hoop so it's a massive hoop for the cars to drive through".

**The spaceport's man.** Scene 92's actor 4 (entity 50) is kept -- Twinsen's travel cutscenes wait on him -- and stood off the road, but his track script walks him to points 14 and 20, both on the road. `ShiftOffRoad` now moves every point a kept actor's script stands it on that lies on the road (`PointsOffRoad`), whether or not the actor itself was on it.

**The roulette wheel, floating** (plan `roulette`: centre, rim, outer, hole, holeY, slope, steepen, wall, landing, spin, no legs). Over where the casino stood (its building, island bodies 70 and 71, taken away -- the plan no longer keeps them), its middle (458.8, 532), its wall 16.2 cells out (twice the first's), 16.5 outside, the hole 4 cells. Its floor is 10,600 at the hole's edge and rises 100 a cell, steepening by 10 a cell every cell (`bowl(r)`): 13,308 at the wall, the wall 400 over it. There is no lane and no rail: the bowl is the race-track mode's own floor (`WheelFloorAt`, used by `RaisedAltitude`, the raised road's collision off over it), and the cars drive it as they like, banked by its slope (`RaceMod_Gravity`) and drawn in towards the hole 1,500 a second for each unit of slope (`WheelMove`, `RACE_WHEEL_PULL`; a car is held inside the wall). Wherever a car reaches the hole it drops through: a curve from where it is to the road under the wheel (the lap's point `landing`, 8,000), at least 0.9 s and 3,000 a second, the car pitching over and back. The road under it runs on west to the pier. The whole bowl turns 8° a second (`spin`) against the lap's way; the ball goes round it the other way (0.35 radians a second) and rolls down towards the hole and back up every 9 s. The pockets' numbers now read from inside the bowl, their tops towards the wall.
- The jump down the west side (a 24° ramp, its lip at z 505) flies a curve over the wall (532 clear) to land 9.5 cells out on the bowl's north-west side, heading round it anticlockwise; the opponents' line spirals in from there to 5.6 cells at its south, then a half turn passing 1.5 cells from the middle, heading west, through the hole. The test pilot lands at (452.7, 524.8), takes 2.4 s round to the hole and 1.25 s down to the road.
- Cameras: in the bowl the eye is 4,600 behind the car, 2,200 up and at least 1,500 over the floor (`WheelCamera`); through the hole it stays where it was and watches the car fall; under the wheel as before.

**The game's gas monsters** (`RaceTrackGasMonster`). Entity 222, the gas monster of Celebration Island's and the Wannies' scenes: body 320, 322 with its mouth open (321 is its hole in the gas, not used), the animations it sways (1481), strikes (1485), rears after striking (1482) and rises (1480) with (its scripts: body 158/anim 498 in the hole, body 0/anim 499 rising, body 159/anim 0 swaying, anim 142 striking, anim 211 after). Made bigger -- the bodies by `IslandScaler.ScaledBody`, the animations by `ScaledAnim` (the bones' moves that much longer, the frames' steps along the ground left out) -- in two sizes, twice its own (5,108 tall) and three times (7,662), as the entity's bodies 120/122 and 124/126 and animations 600-603 and 604-607 (a scene's actor's body is a signed byte: 200 didn't fit). One actor each, its own scripts stopped; the race-track mode stands it at its rise point, leans it towards the road till its jaws (4,900 up and 600 forward on its body at twice the size) are 500 over the bite, lifting it out of the gas as far as that takes, and switches its bodies and animations through rise, sway, strike and after. Each spot gets the smaller size unless the road where it bites is within 1,800 of its head: the docks' road is at 4,300-4,450, and a twice-size monster there was drawn but hidden behind the road's own deck from the chase camera (z-tested with `OBJ_ZBUFFER`; without it, `DrawRecover` painted the scenery back over it) -- the docks' and the last islet's are three times the size, the other three twice. Everything else (waking, timing, the bite, the stun) is as version 3.

**The giant hoop** (plan `hoops`: [{at, bodies, scale, bottom}], `RaceTrackHoop`). The dogs' hoop in the arena south of the town is OTRINGAL.OBL bodies 134 (its red base with the ring's bottom), 133 and 135 (the sides) and 132 (the top), all at one origin, the ring across the body's z axis, at (476, 589.5). The builder makes them 4.5 times as big and stands them as island decors square across the lap, the ring's bottom 120 under the deck (the base on the ground below), their boxes touching nothing. The lap bends through the arena and the chase camera, swinging wide, flew through the ring's side, so the hoop goes to the straightest point of the lap within 12 cells of its place (the least turn over 6 cells either side of the ring): (473.3, 583), the deck 5,804 through it.

**Testing**: the engine's `--exec-at` holds 128 commands at most -- shots and state dumps past that are dropped (the pilot's `SHOTS` and `DUMP` together).

### Version 5: the monsters on guard, a faster wheel (2026-10-09, later)

The user: "Some of the gas monsters strike too low and appear through the track, let's have them permanently visible with their necks above the track and randomly striking down. Let's have each gas monster always attach the same point in the track, so once their positions are learnt they're avoidable. Let's speed up the rotation of the roulette wheel a lot more, it's currently so slow it's not noticable".

**Why they went through the track.** Version 4 leaned the whole body, stiff, from its foot in the gas till its jaws reached the bite. A straight line from below the deck to a point over it crosses the rail lower than its far end, so the neck went through the road's side whenever the bite was more than a little inside the rail -- and every bite was 2.2 cells inside it.

**Necks bent bone by bone** (`GasMonsterNeck`, `GasMonsterPoser`, `RaceTrackGasMonster.InstallPoses`). The gas monster's two bodies are chains from the hole (bone 0) up to the head: the shut one (320) has ten neck bones (1-10) and its head at bone 11 (12-16 and the eyes 27-28 under it), the open one (322) six (1-6), its head at 7 and its jaw at 8 -- the skeletons differ, so an animation is made for the body it plays on. The neck is the chain below its first bone of 30 points or more. A shape leans each neck bone `Lean + (Top - Lean) * along^Gather` from upright towards the mouth's side (along: the bone's middle up the neck, 0-1) and the head `Nod` past the neck's top; the engine turns a bone by M(Alpha)M(Gamma)M(Beta) in its parent's frame (LIB386/3D/IMATSTDF.CPP; Alpha tips +y towards +z, where the mouth faces), as `Lba1Pose` does, so each bone's own value is the difference from its parent's lean. For each monster, in its own frame (the foot at its place, z towards its bite, the rail and the deck where the road is), a grid search and then a finer one find three shapes:
- struck: the head's lowest point 100 over the deck at the bite, nothing on the road's side of the rail lower than 340 over the deck at the rail (the rail is 220) or 60 over it beyond; its foot as high as that allows (at the gas or under it, the hole hidden);
- poised: its head's lowest point 1,300 over the road (1,950 at three times the size) a quarter of the way in from the rail, 450 clear of the rail and the deck;
- reared: 600 (900) back over the gas from the rail and 1,900 (2,850) over the road.
The moves between them are checked as the engine blends them (each bone's turn by itself). A monster gets the smallest size that comes out right: its struck head within 200 of the bite, its poised head 600 (900) over the road, nothing through the rail or the deck. Otringal's: the docks' one three times the size, its foot at the gas; the islets' first twice (foot 460 under the gas), the other three three times (feet 0-1,380 under). `RT_MONSTER_POSES=<folder>` writes each one's posed points for a look from the side.
- Its animations (the entity's, 610 on, 4 a monster): swaying poised (four keyframes 0.7 s apart, a sway of 0.12 radians either way shared along the neck, the head nodding), mouth open; the strike (into the poised pose 0.15 s, reared 0.65 s, struck 0.22 s, held there), mouth open; the bite (the struck pose held 0.45 s, back to poised 0.7 s), mouth shut. The game's own animations aren't used.
- The engine (`Monsters`): each monster stands at its place for good, its foot at the height the race file gives (`gasmonster=` now ends with the foot's height and the first animation), facing its bite. Poised, it strikes after 1.4 to 2.8 s, at random (the first any time in the first 2.8 s, so they don't strike together); 1.02 s into the strike its head is on the road, and a car within 950 of its place then or while its jaws are held there (0.45 s) is bitten (2.2 s); 1.17 s after the hit it is poised again. (Biting only at the hit, every 2.2-4.4 s, caught a car crossing the place in 0.2 s too seldom to matter: none of Twinsen's three laps.) A 3-lap race: everyone finished, three bites. A car isn't woken into anything any more: where a monster bites is always the same place, the near half of the road, so a driver who has learnt them keeps to the far half there or times it.

**The roulette wheel** turns 45° a second (a turn in 8 s), was 8.

### Version 6: long necks that bite, a bigger wheel, a wider start (2026-10-09, later)

The user: "The gas monsters aren't really biting players they're just slamming their heads onto the track ... Let's make the gas monsters necks longer, their body size doesn't really matter, so they're above hte track then when they're ready they strike down onto the track into their strike zone. Let's see if we can make the roulette wheel bigger still. I don't think we have enough space to make it double the size, but let's get as close to this as we can do"; then, purple lines drawn along the four places the start/finish straight narrowed: "let's expand the track at these points".

**Long necks, and a bite.** Version 5's struck pose put the head's lowest point on the road, so the head came down flat across it. Now:
- *One body, its jaw opening the mouth.* The game opens the gas monster's mouth by turning the jaw (its open body, 322, is the same mesh with the jaw a bone of its own, and its sway, anim 1481, turns that jaw 26-76°; its rise, 1480, turns the shut body's jaw, bone 14, by 56°). So every monster uses the shut body (320), with its smoother neck (eight neck bones to the open body's four), and opens its mouth by turning bone 14: 0.7 radians poised, 1.0 reared and striking, shut (-0.05) as it bites.
- *A neck of its own.* Each monster's body is the game's at twice the size with its neck bones' points stretched up from their pivots (`GasMonsterNeck.LongNeck`; every bone above moves with them), as long as its place wants: from its foot at the gas, its mouth must reach a tenth again past its poised place (2,600 over the road, over the strike zone), 1.4 to 2.25 times the neck's own length at Otringal (4,260 to 6,880). They are the entity's bodies 130 on (the scene's actor starts on the game's own, body 0, out of sight; the engine gives it its own the first time it poses it).
- *A plunge.* Bone 1, a one-point stub at the neck's foot, is moved instead of turned (a translation, as the game's rise moves its bones): the whole neck sinks into its hole as it strikes, up to 0.45 of its length (at Otringal 0-800).
- *The poses* (`GasMonsterPoser`): poised, the mouth's middle 2,600 over the road 15 % of the way back from the strike zone towards the rail, facing 75° down; reared, 3,300 over it, 300 short of the rail, facing 45° down, jaw wide; struck, the mouth's middle over the strike zone and the head's lowest point -- the open jaw -- 50 over the deck, facing 100° down; bit, the same with the jaw shut. A monster is right when its struck jaw is within 200 of that, its poised mouth 2,100 or more over the road, and nothing through the rail or the deck (the moves between included); otherwise its neck is made 15 % longer and tried again. All six of Otringal's are right first time: the struck jaw 3-10 from its place, poised 2,500 over the road, the necks 790-1,290 over the rail.
- *Its animations:* poised (four keyframes 0.7 s apart, the sway and a nod, the jaw breathing), the strike (into poised 0.15 s, reared 0.65 s, struck 0.22 s), the bite (the jaw snapping shut 0.12 s, held 0.33 s, back to poised 0.7 s). The engine (`RACE_MONSTER_BACK`, `RACE_MONSTER_HOLD`) bites a car at the strike zone when the jaw lands and while it snaps shut and holds.
- With no size rule (version 4's: a twice-size monster hidden behind a high road's deck) the stretch past the islets gets a sixth monster, at (437.3, 608.6).

**The roulette wheel half as big again.** What stops it is the jump into it: its lip at (454.8, 505) north of the wheel. Its middle moved 2 cells south, to (458.8, 534), still over the casino; its wall 24.3 cells out (was 16.2), its outside 24.8 -- 2.2 cells from the lip; the hole 5 cells. Its floor still rises from 10,600 at the hole to 13,292 at the wall (70 a cell, steepening 3.6 a cell every cell), so the jump clears it as before -- now off a 31° ramp (was 24°), 19 cells, 516 over the wall. The town's climb passes 3 cells outside it, under its rim's height. The landing 14 cells out, the line round it down to 8.4 before the half turn (passing 2 from the middle), the drop 7.5 cells, the straight out 5: the car takes about 8 s round it. The lap is 737.5 cells.

**The start/finish straight wider at its ends.** The straight is 6.25 cells to its rail on either side for the pit lane (4 elsewhere); it narrowed over 7 cells at each end (z 511-504 and 470-463), which is where the purple lines were. It now keeps its full width through both (z 511 to 463) and eases back to 4 over the next 10 cells, into the town's climb south of it and the turn west north of it (`WIDE_EASE`).

### A rescue that turned the game clock back (2026-10-09, later)

The user, mid-race: "it's currently glitching and Twinsen's car is undrivable". Their Play log (`release\lba2-play\adeline.log`): in lap 5 Twinsen went off the roulette wheel's south rim onto the town's ground in the next cube (scene 89, the last safe place in scene 87); the rescue put him back across the scenes; on the next line "opponent 2: lap 5 in -153.58 s", then a lap every few frames, up to lap 152. His car stood stopped for about 150 s until a car pushed it, and then drove again.

**Cause.** The game clock (`TimerRefHR`, the race mode's `now`) is paused by a bracket: `SaveTimer` (counted; the first notes the clock) and `RestoreTimer` (the one that brings the count back to 0 puts the clock back to that note). A game loaded from a save -- Play starts the race with `--load EDITORPLAY` -- ends the load with the bracket left open (CONTROL.CPP closes it only under `--fixed-dt`). `ChangeCube` opens its own bracket only on `FlagChgCube` but always closes one. A scene change the race mode asks for -- the rescue into another scene (`Rescue`), the race again after a loss and the dream's wake (`RaceMod_Story`) -- is asked for in or before `AffScene`, whose end clears `FlagChgCube` before the change is made: so `ChangeCube` closed the load's bracket, and the clock went back to the moment the game was loaded, 153 s into that race. Everything the race keeps as a time to come was then 153 s off: the car's stun held it stopped till the clock caught up; Baldino's oil skid (`SkidSpeed * (1 - p)^2`, p from a start now in the future) ran his speed away, a lap a frame, slowing as the clock came round. The test pilot never saw it: it starts with `--fixed-dt` and the console's `cube`, where no bracket is open and a lone `RestoreTimer` does nothing.

**Fix.** Those three scene changes open the bracket themselves (`SaveTimer` with `NewCube`), so `ChangeCube`'s `RestoreTimer` closes theirs. And the race mode says in the log when the game clock goes back more than a second ("WARNING: the game clock went back").

**Reproduced and checked** (E:\dump\TEMP\otr\repro_rewind.sh: a save made as Play makes it, loaded without `--fixed-dt`, the car teleported off the road into the next cube): before, the clock went from 5,485 to 764 ms at the rescue; after, it runs on (5,015 to 5,606). Setting `FlagChgCube = 1` with the rescue's change, as the carried jumps do, did not help: `AffScene`'s end cleared it first (OBJECT.CPP, after the autosave it triggers).

### A race alone: the gunners, the game's bullets, the monsters' doubles, the camera under the wheel (2026-10-10)

The user: "There seems to be issues on the otringal track in a single car race that doesn't happen when Twinsen has opponents, for example the gunner not firing, also he's not firing the default bullets the retail version normally fires ... the gas monsters seem to be leaving shadow versions behind that aren't correctly cleaned up"; "We also seem to have two guns and one gunner, let's either remove a gun or add a second gunner"; "When Twinsen drops out of the roulette wheel it's very hard for him to see where he's going".
- **The gunner fires in any race.** Its bursts waited on `RaceOn()` -- other cars on the lap -- so a race alone had none. They now wait on `RaceRunning()`: after the grid's GO, alone or against others.
- **The game's own bullets.** The gun's firing animation (entity 200, anim 173) throws 9 bullets as F_THROW_3D: raw sprite 50 at its own size, level, 100 a frame (a 20 ms frame; 5,000 a second), weight 5-18, force 5, in a fan of -256..256 -- which, from the knoll, would fly off level whichever way the gun faced (they stay left out, `RaceMod_SilentThrow`). The race-track mode's were that sprite made bigger (scale 18) and flown in a straight line. Each of its bullets is now thrown as the gun throws its own (`ThrowExtra`, sprite 50, speed 100, weight 12, no force -- its hit is the race-track mode's, where it lands; nothing on the way stops it), at the height that brings it down on its place: in frames, x = v cos(h) t, y = v sin(h) t - (weight/16) t^2, the lower of the two heights that land it there (`GunnerShot`); taken away where it lands, with the spark.
- **A Franco at each gun.** Both of scene 88's guns stand on the knoll; each now has a Franco (entity 201, sitting at it) and its own `gunner=` line, its muzzle at its own gun (the second 1.6 cells beside the first), their bursts half a burst apart. The test pilot, on the racing line through the corner's inner half, is hit about 3 times a pass.
- **The monsters' dark doubles.** With the camera still -- the car stopped, bitten or hit -- the engine draws only what moves over the picture kept from the last whole frame (`AFF_OBJETS_FLIP`), and the gas monster's swing left a dark copy of itself behind it (reproduced: the car stopped by the docks' monster in a race alone). Every frame of a race out of doors is now drawn whole (`RaceMod_Story`, as a parade lap's already were) -- which also keeps the roulette wheel, decors, turning while the camera is still.
- **The camera under the wheel.** After the drop through the hole the eye was 4,200 behind the car and 1,000 up, looking at a place 900 ahead of it: the car hid the road. It is now 2,000 up (still under the bowl) and looks at a place 4,000 ahead, on the road (`RACE_WHEEL_CAM_UP`, `RACE_WHEEL_CAM_AHEAD`).
- Testing a race alone: `RT_NO_OPPONENTS=1 ScriptRoundTrip racecarfile ...` (car_solo*.txt).

### The gas monsters seen from afar (2026-10-10, later)

The user: "Our gas monsters are invisible until we're right at them, which takes the user by surprise so they have no chance to dodge". The engine draws a scene's actors only while that scene is the one about (decors it draws from any cube): the islets' five, actors of scene 264, stood up out of nowhere as the car crossed into it from the docks (scene 87), though the islets were in sight long before; the docks' one was missing from under the roulette wheel (scene 89). Each monster is now an actor of every scene whose cube comes within 40 cells of it (`GasMonsterSeen`; its start in the scene's record clamped into the cube -- a record's place is 16 bits -- the race-track mode standing it where it is). Its `gasmonster=` line is one a monster still -- one state, one strike -- its own scene's actor first, then `<scene> <actor>` for each scene it is seen from (`SeenScene`/`SeenActor`, at most 8); the race-track mode poses the one of the scene about, and on a scene change gives the new one its body and the animation it is in. At Otringal: the docks' monster in scenes 87, 89 and 264, the islets' in 264, 87, 89 and 90. From under the wheel the docks' monster now stands over the town ahead; from the docks the islets' stand up across the gas.

### No pale doubles; the roulette wheel made fun (2026-10-10, later)

The user: "We're getting random shadow images of our gas monsters at points in the track, is this fixable? The roulette wheel we've created isn't really fun to drive, any suggestions on how we can improve this?" -- of four ideas offered, all four chosen: a prize by the pocket, the floor dragging the car round, several bouncing balls, a quicker way down.

**Pale doubles.** The island's decors are drawn without the z-buffer, the actors with it: a monster far behind a decor -- the wheel's bowl, a raised road's deck -- was drawn over it, near-white in the fog, since the monsters became actors of the neighbouring scenes. A monster is now drawn only with the car within 40 cells of it (`RACE_MONSTER_SEEN_FAR`), and never while the car is in the bowl or dropping through it; its strikes go on unseen.

**The roulette wheel.**
- *A prize by the pocket.* The pocket the car goes into the hole over -- its angle on the turning head, `s_pockets` in the wheel's order -- decides it, said on the screen when the car lands: red, Gazogem's boost for 2.5 s; the green zero, "Zero! Jackpot!", the boost for 5 s and an item into a free slot (with power-ups on); black, its number and nothing. The spin makes it a matter of timing. (The race-track mode's messages -- "Back on the road" too -- had never been drawn: `RaceMod_Draw` now shows them near the top, in the middle.)
- *The floor drags the car.* On the bowl Twinsen's car is carried round with the turning head at 0.4 of its speed, its heading turned with it (`RACE_WHEEL_DRAG`): with the turn, the way round is quicker; against it, slower.
- *Three bouncing balls* (`roulette_balls=`, plan `balls`; bodies one after another from the wheel's ball): each pulled down the bowl by its slope, bounced off the wall (0.85) and kicked back up off the ridge round the hole (1.15), off each other and off the cars, losing 3 % of its speed a second, at most 9,000 a second. A car one rolls into is knocked 1,800 aside over 0.45 s and keeps 60 % of its speed (an opponent pushed aside as by the super jet-pack), the ball bouncing away -- no longer stopped dead.
- *Quicker (still).* The pull down the slope is 3,500 a unit of slope (was 1,500); the jump lands 11 cells out (was 14) and the lap's line round the bowl goes down to 6.5 cells before the half turn (was 8.4) -- 82.5 cells of it (was 108). The test pilot is through the hole about 4 s after landing (was 8). The lap is 715 cells.

### The roulette wheel's prizes: 19 pockets, a slot machine, the golden Gazogem (2026-10-10, later)

The user: "Let's lose the numbers on the roulette wheel, half the segments, but double the width of the remaining ones. Red is win, black is lose, and green is super jackpot. For red we spin three wheels at the top middle of the screen like a fruit machine, and they spin stopping one at a time from left to right displaying a powerup, let's make it so roughly 75% of the time the user wins a power up prize, if they get the green super jackpot they get a golden gazogem bottle where they can use the power up for a speedboost as many times as they like for 8 seconds". Earlier the same day: "We don't need messages saying 'Hit by shuttle', 'bitten by gas monster', or 'Hit by gunner'" and "We also don't need a message for each pickup either" -- those are logged only now (the race-track mode's messages, `Message`, had never been drawn until the wheel's prizes needed them; results, start-line notices, loop hints, "Back on the road" and the wheel's prizes stay).
- *The head* (`RaceTrackRoulette.Pockets`, 19): pocket 0 green, then red and black in turn (9 of each), each 18.9 degrees (the European wheel's were 9.7), no numbers, its colour in eight bands out to the wall. The race file says how many (`roulette_pockets=`); the race-track mode tells the pocket the car drops through over by its angle on the head (`PocketPrize`: 0 green, odd red, even black -- checked against a frame of the drop). A test key forces a prize: `roulette_prize=<0|1|2>`.
- *Black:* "Black: no luck".
- *Red: the slot machine* (`SlotSpin`, `SlotMachine`, `SlotShown`). Three reels at the top middle of the screen, item boxes a quarter bigger than the item box's, each turning over the power-ups (one every 70 ms) until it stops: the first 1.2 s after the landing, the next 0.7 s after each other. Three the same win: 75 % of the time (`RACE_SLOT_WIN`) all three are one power-up drawn as a mushroom's is (`RandomPowerUp`), ringed in gold when the last stops, "Winner!", and it goes into the item box at once (`GiveItem`: a free slot, or the one not selected; since 2026-10-17 the one box, kept free while the reels turn, and with it full no spin but red's boost -- see *One item box*); otherwise the first two are often the same and the last not, "No luck". The reels stay 1.8 s after.
- *The green: the golden Gazogem* (`PU_GOLD`, never from a mushroom nor the item box's roulette, `PU_ROLLED`). "Super jackpot! A golden Gazogem!", into the item box. Its icon is the game's Gazogem can (OBJFIX.HQR entry 15) with every face on the palette's gold ramp (6), four steps up (`RaceTrackOil.InstallGoldIcon`, `gold_icon=`). From its first use it stays in its slot for 8 s (`RACE_GOLD_MS`), a gold bar under the slot running down; each use in that time gives Gazogem's boost for 1.2 s and kicks the car's speed up a quarter (to the boosted top speed at most). Then it is gone. (Since 2026-10-17, won while the item box holds something, it waits in a second box: see *One item box*.)
- Without power-ups (`powerups=0`) red gives a 2.5 s boost and the green a 5 s one, as before.

### 18 pockets, a jet-pack that gets stuck on nothing, the golden Gazogem's kick (2026-10-10, later)

The user: "Let's drop a segment from our roulette wheel and make all other slots slightly wider to compensate. Sometimes on this track our super jetpack can get stuck on things such as the fence on the start/finish line that separates the track from the pit lane, or the side of one of the space shuttles that protrudes onto the track. The jet pack needs updating so it doesn't get stuck on objects. Let's tweak the golden gazogem powerup so that every time the user presses the powerup button they get an instant 10 kmph speed injection".
- *18 pockets* (`RaceTrackRoulette.Pockets`): the green, then 9 red and 8 black in turn -- two reds side by side where the circle closes -- each 20 degrees (was 18.9).
- *The super jet-pack goes through objects* (`RaceMod_JetGhost`). It steers the car back onto the opponents' line from wherever it is, and its car collided as any car does: set off from the pit lane (reproduced: the car put there, `pitjet.sh`), it ran along the pit lane's fence at half its speed for 1.2 s trying to cross to its line, and at a steeper angle stopped dead; the shuttles stood in its way likewise. Now, while it lasts, Twinsen's car runs into no island object and no actor: the engine's decor tests -- `WorldColBrickExt`'s decor boxes, `WorldColBrickDecors`, `DoCornerReajustDecors` (EXTFUNC.CPP: `JetGhost`, the moving object Twinsen's car on the jet-pack) -- find none for it, nor `CheckObjCol` and `IsObjCol` any actor (OBJECT.CPP), and no actor is stopped by it (`RaceMod_NoCollide`). (Gating only the push-back, `DoCornerReajustDecors`, made it worse: the move's later check put the car back where it was whenever its box was in a decor's -- stuck on the fence for good.) The road's floor and its rails (the race-track mode's own), and the ground, still hold it. From the pit lane it now cuts across the fence onto its line; a lap of jet-packs (`jetlap.sh`, `raceitem 4` every 10.6 s) has no stall but in the roulette wheel, no fall.
- *The golden Gazogem's kick:* each use now adds 10 km/h at once (`RACE_GOLD_INJECT`, 1,422 a second -- the dial shows speed x 36 / 5,120), up to the super jet-pack's speed (twice the top gear's); with it, 1.2 s of Gazogem's boost as before. Over the top speed the car loses the extra gradually (BUGGY.CPP), so pressing often keeps it up: the test pilot, pressing every 0.6 s, ran at 44-47 km/h (the car's top is 34).

### The magic balls forwards and surer, a shorter protection spell (2026-10-10, later)

The user: "Make all magic balls exit the car forwards instead of being dropped behind. Let's increase the cells the yellow ball is active to 56, double what it is now. Green ball to 10 seconds, and Red ball to last until it hits the car in front, and fire balls to always hit all cars"; and "Let's decrease the length the protection spell lasts for, perhaps 12 seconds, let's also have it no longer cancel a shrink".
- *Forwards.* Every ball starts 900 in front of the car it is thrown from. What went behind were the homing balls -- the fire ball's for the cars behind the thrower, and a red one whose car fell behind it -- which went after their car along the track whichever way round the lap was nearer. They now go forwards only, round the lap to it however far behind it is, and straight at it once it is near and ahead (`Shots`). From pole at the start, the fire ball's three went the whole lap round and hit all three cars after 16 s.
- *The yellow* darts 56 cells (`RACE_DART_RANGE`, was 28; the red with no car in front darts twice that). *The green* rolls 10 s (`RACE_GREEN_MS`, was 6).
- *The red and the fire ball* have no time limit while their car is on the lap: each goes on until it hits it (a car not on the lap for the moment is waited for, ahead along the track, for at most a minute). With every mushroom a red ball (`powerup_only=8`), 10 of 10 fired at a car in front hit it.
- *The protection spell* lasts 12 s (`RACE_PROTECT_MS`, was 30) and no longer clears a shrink, nor holds it off while it lasts: a car already shrunk stays shrunk -- small and at 70 % speed -- for its time. It still keeps a new lightning strike off, and every other hit.

### The golden Gazogem with no top speed (2026-10-10, later)

The user: "Remove the top speed limit whilst we have golden gazogem, let's allow the user to inject as much speed as they can handle for the duration they have it". While it runs -- from its first use till its 8 s are up -- the car keeps whatever speed it has over the gear's top: BUGGY.CPP's overspeed drag and its coast back down to the top are both off (`RaceMod_NoTopSpeed`), and each use adds its 10 km/h with no cap. Gravity still acts (uphill the car loses speed, downhill gains it), and after the 8 s the extra is lost as any is. The test pilot holds the accelerator while it runs (it braked down to its line's speed otherwise): pressing every 0.6 s it went from 44 to 161 km/h -- the car's own top is 34 -- and kept to the road through the islets and up into the spaceport.

## Volcano Island: over the lava (2026-10-08)

The user's sketch over the editor's minimap: "Pink are jump points, green is track, orange is pitlane. For this track let's add lava balls shooting up and out of the lava at points and raining down onto the track". The minimap was turned half a turn and stretched: sketch x = 1111 - 2.05 * (render x), sketch y = 725 - 1.39 * (render y), the render 4 pixels a cell from cell (448, 448) (E:\dump\TEMP\volc\trace.py, centreline.py).

**The island.** `SOUSCELB.ILE`, the game's island 11 under Celebration Island: cubes (7,7), (8,7), (8,8) -- scenes 130, 131, 132; (7,8) isn't there. A sand plateau on the volcano's top at 12,750, rock slopes, lava channels and a lava ring round the south. Its palette is RESS entry 38 (the editor drew it with Citadel Island's, 27, until now).

**The lap** (tools/RaceTrackPlan/volcano_design.py, `RaceTrackIsland.Volcano`), 392 cells, a raised road close over the ground as Otringal's version 2 (400 over the median ground across it, at least 1,400 over the lowest lava, 17 % grades): round the plateau -- the start line and the pit lane (on its right, inside the lap) on its north straight; down off its east end and a jump south over the main lava channel (14 cells); a short stretch on the rocks between two channels and a jump east over the second (13.5 cells) onto the island's east edge; south along it and a jump south-west down to the south shore (9.5 cells); west along the shore and back up the volcano's flank onto the plateau. Heights 2,450 to 13,150. Qualifying lap with the test pilot 38.7 s. The drivers are the island's own: the Mosquibee dissident in his car (the one to beat), the ferryman in his boat and a Mosquibee of the hide-out on a slab of lava.

**Lava balls** (plan `lavaBalls`: [{source: [x, z], from, to, every}], the race file's `lavaball=<x> <y> <z> <from> <to> <every> <phase>`, RACEMOD.CPP `LavaBalls`): six places in the lava beside the lap. Each, every `every` ms give or take a quarter, while Twinsen is within 40 cells of where it will land, shoots a ball of fire -- the magic ball's fire sprite (raw sprite 11, its trail 15-17) -- up out of the lava (a splash where it leaves it) in an arc onto a place picked along its stretch of the road, within 60 % of the deck's half width. Its shadow (sprite 12) lies on the road there from the start, growing as the ball comes down; the flight takes 1.5 s and more the further it goes. Where it lands it bursts as a nitro penguin does, and a car within 1,100 is stopped -- Twinsen's unless the protection spell or the super jet-pack is on ("Hit by a lava ball!"), an opponent's with its hit animation. In a 4-lap test race the lava hit the ferryman once and Twinsen once.

### The pit lane inside, a wider road, the game's lava, a lava fall and a boost panel (2026-10-10)

The user: "Volcano island (SOUSCELB.ILE), the pit lane is on the wrong side of the start/finish straight, let's fix this. Also the lava balls that are currently coming out of the lava are different to the retail ones, so let's switch these. In scene '100: Wannies Island, the mine, 2nd room (room #58)' there's a lava water fall next to a section of track that shoots lava balls, let's steal this idea and have it a feature of our track for Volcano Island. Let's also make the entire track slightly wider if we can. Let's create a boost panel that cars can drive over for a speed boost, they'd only need to drive over it with at least two wheels for it to activate, this should immediately shoot a car up to 120kph, let's test this by putting this on the pit lane so if cars want the speed boost they have to deviate from the normal racing line".
- *The pit lane's side.* The deck of the north straight was widened to the south (inside the lap) for the pit lane, and the opponents waited there, but the stripe and Citadel's fence were put 1.75 cells *north* of the deck's middle and the grid moved 2.25 south -- Otringal's signs, whose straight runs north (the builder's Across is (-Tz, Tx): south heading east, west heading north). So the fenced-off lane was the outside 4.5 cells and the grid stood in the pit lane. Now `pitStripe` is at +2.5, `gridShift` -2.25: race lanes north (the grid on them), the pit lane south, inside the lap, as in the sketch.
- *Wider:* the raised road's half width 4.75 cells (was 4.0): 9.5 cells across, 14 on the pit straight. The test pilot's qualifying lap 35.5-38.3 s (was 38.7).
- *The game's lava balls* (`GameLava`). The ball is the game's own lava ball -- OBJFIX.HQR entry 69, the one Wannies Island's mine spits -- four times its size (`RaceTrackOil.InstallLavaBall`, `lava_ball_model=`; it is 130 across, a dot from the race camera), a 3D extra tumbling along the arc (as the laser bolts are, `LaserExtra`). It leaves the lava with the mine's lava burst, impact 25 (its sound, flow 67's sparks, ten of the little lava balls), and bursts the same way where it lands; no fire trail, no penguin's blast. The burst's little balls are only to see (their `EXTRA_END_OBJ` and `EXTRA_HIT_ANY` cleared): what stops a car is still the landing's reach, 1,100 -- an opponent's car hit by a stray one would have been hurt as an actor, and they bounce about the deck. They are left out, the sparks alone, while fewer than 24 extras are free. The ball's faces are the objects' texture page's animated fire, black until something animates it (the scene does only with animated textures of its own or the protection spell on; without power-ups the balls flew black): `LavaBalls` animates it every frame now.
- *The lava fall* (`RaceTrackLavaFall`, plan `lavaFalls`, `lavafall=`). Scene 100's is a lava fall beside the mine's rails spitting lava balls out of its foot (impact 23, its flow 90 thrown sideways: `impact_point(37, 23)` and `(36, 23)` in its script, impact 25 with the burst actor's animation). Ours is made in the island's ground on the plateau inside the lap, by its south edge: a crag 4,600 over the sand, 15 cells across and 8 deep, its sides and back wobbling in and out and falling steeply to the sand, its top rough; a stream of lava across its top from a little crater at its back to the lip of its face, the face one cell deep -- its triangles near upright, so the ground's lava tile is drawn down it in streaks -- and a pool at its foot 1.25 cells from the road's inner rail. The ground is the island's own, copied: its rock (cells by (530, 466)) and its lava (by (515, 479): code 13, animated lava), a cell's two triangles together; the light baked again, the lava's at least 13 (it glows). The race-track mode, while Twinsen is within 48 cells, throws impact 23's spit out of the fall's foot every 0.75-2.25 s, aimed the way the face looks (flow 90 and its little balls, `CreateExtraParticleFlow`), and sparks off its lip and down its face every 0.3-0.9 s; a seventh `lavaBalls` source, the pool, shoots the game's balls onto the straight beside it every 2.6 s.
- *The boost panel* (plan `boostPanels`: [first point, last point, from and to across (cells)], `boostpanel=<x> <y> <z> <way x> <way z> <half length> <half width>`, `BoostPanels`): in the pit lane, 5 cells long from x 492 (past the start line and the waiting spots) and 3 wide in its middle. On the deck (`RaceTrackRaisedBody.Tile`'s `boost`) a white arrow on orange in each cell, pointing on: as wide as the panel at the cell's back, as its middle strip at its front (the deck's strips are a cell wide there: the outer ones' diagonals are the arrow's sides). With two of BUGGY.CPP's four wheels or more on it Twinsen's car is at 120 km/h at once (17,067 a second; the car's top is 34); over its top speed it then loses the extra as it does any (its coast): 120 km/h, 3.5 times its top, comes down to its own over about 1.6 s, 35 cells on. Put in the pit lane at a standstill 3 cells before the panel, the test pilot was at 120 km/h on it and through the bend and the channel jump after it. The opponents' lines keep to the race lanes, so only a player takes it (the race-track mode would boost any car by its wheels; the opponents' don't drive by wheels). The test pilot holds the accelerator for 2.5 s after a panel.
- *A missed jump, rescued into its gap.* In a 4-lap test race Twinsen's car was stopped by a fake mushroom's blast at the shore jump's ramp; it slid over the ramp's foot at no speed, so it was never carried (`RaceArc` took a car only as it crossed the foot going forwards), drove up the ramp and off the lip onto a rock in the gap, and the rescue put it back at the line's point in the gap over the same rock, again and again, for the rest of the race: its last safe place had been taken there (the line there is the flight's, and `LineOnGround` called any point of a raised road ground). Now no point within a point of a gap in the raised road (`OverRaisedGap`) is ground for the rescue or a safe place, and a car on a ramp past its foot, going up it, is carried from where it is ("carried jump 3: on, up its ramp already (557 along)").

### Volcano Island redesigned: the lava falls, the canyon, crystals; an easier pit lane, a longer boost (2026-10-10, later)

The user, over the editor's minimap with a dashed line along the main lava channel: "Suggest a total redesign for the track, perhaps with the lava waterfall in a new location as pictured, perhaps as tall as the level allows that shoots lava balls. I'm open to any suggestions you make"; "The island heavily features red crystal scenary, perhaps we could make this a feature too". The proposal (E:\dump\TEMP\volc\proposal.png) was taken as it was -- "make the changes you suggested along with these fixes": "In the current track the pitlane is too hard to drive into" and "The current boost panel duration needs to be a lot longer"; and "Keep the mushrooms as pickups". The lap, 397 cells, clockwise:
1. *The lava falls* (`RaceTrackLavaFall`, made big: `top`, `tiers`, `tierDepth`). The ground north of the channel is the island's summit: 30,000 high at its rim (the island's ground holds heights to 32,767; a 29,750 test crag drew fine), 36 cells across, 20 deep, its sides sheer. A lake of lava fills its top and pours over its south rim down four tiers -- each a ledge of lava a cell deep and a drop of about 6,100 -- into the channel at its foot, 30 cells wide, the rock either side of the cascade standing to the top. Lava tiles stretched down the drops read as falling lava. The island's object inside it (body 0, a 4.6-cell rock) is taken out (decors are drawn over the ground). 16 places across its face feed the race-track mode's spit and sparks (`RACE_MAX_FALLS` 16).
2. From the start the north straight ends with a right turn down the plateau's east edge under the summit's west cliff, then left between the crystal gate (one crystal; north of the way the cliff leaves no room) off the plateau.
3. *The canyon road* east along the channel, 8 cells off the falls' face, descending at the grade's limit (17 %) from 12,300 to 9,800: the falls on the left all the way, five places at their foot shooting the game's lava balls onto the road beside each.
4. *The drop jump* off the canyon's end (lip 9,920) east over the lava lowland down onto the east ridge (5,800), 13.5 cells; its camera beside it (`arcJumps` fifth number 1: south of it), the falls behind the car. (The design's grade spreading and smoothing no longer cross its gap: the landing had been pulled up to 8,300.)
5. South along the east ridge under a *crystal arch*; the *crystal field* (six of the island's clusters, made 3-3.5 times bigger) in the lava below to the right, and lava geysers in the lowland shooting onto the ridge.
6. The shore jump and the shore (as before); up the flank, *the island's bowls of lava* spitting the balls (a lava source's third number is its height: a bowl's brim); round the plateau's *crystal garden* (six clusters) and under a second arch over its west side.
- *Crystals* (`RaceTrackCrystals`, plan `crystals`: at, body, scale, turn, lean): the island's clusters (OBL bodies 1 and 2, as wide as they are tall) copied bigger and leaning, as new island bodies. One that would come within the rails between 1,000 under the deck (or a jump's flight) and 2,000 over it is turned another way (an eighth of a turn at a time), then made smaller. 14 placed. A cluster leant over the road reached nowhere near across it, so *the arches* (plan `crystalArches`) are a body of their own (`Arch`): a half ring of crystal prisms over the road, its legs 3 cells outside the rails down into the ground, crystal spikes out from it -- 35 crystals, 13 points and 12 faces each (an island body has 550 of each at most). Its colours: the island palette's ramp 4 (ramp 5 for the points), which through the island's colour tables read as the clusters' dark red; ramp 2, the palette's own purple, came out cream.
- *The pit lane* (the user: "too hard to drive into"): the deck widens over 8 cells from the end of the north-west bend (over 6, ending right where the fence began: the way in was a slot two cells wide at its first post), and the fence starts 7 cells after it is full width and ends sooner (x 481-497). A car keeping right out of the bend is in the pit lane without turning (the test pilot, 4.5 cells right of its line from the bend's end, ran straight in and over the panel).
- *The boost panel lasts* (the user: "a lot longer"): after the car leaves it, its top speed is 120 km/h for 4.5 s (`RACE_PANEL_HOLD_MS`) and eases back to its own over 3.5 s (`RACE_PANEL_FADE_MS`; `RaceMod_SpeedFactor`, `PanelFactor`) -- the accelerator held keeps it there, a hit or the brakes take it off as ever. It had been lost as any speed over the gear's top in 1.6 s. Off the panel the pilot ran the canyon and the drop jump at 120-125 km/h, then 109, 97, 76, 62, 49 km/h down the east ridge. At that speed the bends after the pit lane want braking (the pilot, steering by a point ahead along its line, hit their rails).
- In a 4-lap race (the pilot): qualifying 38.1 s, laps 47-56 s for everyone, the lava hitting each opponent about once a lap.

### Version 3: the old lap in two layers, the lava fall on the bluff, the aqueduct, lava geysers (2026-10-11)

The user: "The new Volcano island race track that you've came up with is worse than the one we had previously. Let's keep the lava waterfall but move it to the top of the bluff, to the left of where the crystal garden currently is. Let's add multiple layers of track to add some height to the track. Perhaps we can cross by the lava waterfall twice, once passing in front of it, and once under the lava flow. Perhaps we could have streams of lava randomly shooting up into the air sending lava balls showering down onto the track". The lap of 2026-10-08/10 again (the summit, the canyon road and the drop jump are gone), 458 cells, with the plateau in two layers; the pit lane's easier way in, the longer boost and the crystals kept.
- *The upper layer.* The north straight (the start, the pit lane, the boost panel) ends with a right turn; the road climbs south down the plateau's east edge, west, and north past the lava fall, at 15,800-17,300 over the plateau's 12,750 (the plan lifts its floor from the climb's start at 15.5 % -- under the grade's limit, so the spreading keeps the straight level -- to `UP` 17,300), then east across its own climb 3,600 over it and north-east off the plateau onto the old bridge east, now 15,000 at its middle. The jump over the main lava channel takes the extra: lip 13,990, landing 8,286 -- the design's grade spreading lets the road drop `JUMP_DROP` (4,000) more across that gap only, so everything after it is at the old lap's heights (spread across the gap at the grade it held the whole east half 4,000 over its ground; not spread across it at all, the stretch between the channels sank into its rocks).
- *The lava fall* (`RaceTrackLavaFall`) on the plateau's west part, west of the crystal garden: a crag 12 cells wide, 11.5 deep, its top 24,000, the lava pouring down its east face in three tiers into a pool at its foot -- the upper layer's way north passes it 1.25 cells off the face, the tiers above and below the road (the user's "passing in front of it").
- *The aqueduct* (`RaceTrackLavaFall.Aqueduct`, plan `lavaFalls[].aqueduct`: to, surface, width): the crag's lava carried west on a trough of rock out of its back, over the west side (the lower layer) and off the bluff's edge into the sea -- "under the lava flow". Its own body: level over the road (its underside 2,469 over the deck, its lava at 16,300), going down into the ground beyond it (into the crag's rock, a pillar on the bluff), lava spilling down its outer walls over the road, drips of lava hanging under it, a sheet of lava falling off its end; the cliff below its end painted with the island's lava down to the sea, and the race-track mode's spit and sparks there. A lava source over the road (its third number, a height: 15,700) drips the game's lava balls onto the road under it every 2.4 s, and one in the fall's pool throws them onto the upper layer beside it.
- *Its colours*: palette indices as the game draws its objects through the island's colour tables -- an in-game swatch of all 256 (a 16 x 16 panel in the arch's place, `RT_SWATCH=1`, photographed) showed each ramp's light end going cream or yellow (the lava first drawn with ramp 6's 12-14 came out pale) and ramp 3 light tan: ramp 6's index 5-7 (red-orange) for the lava, ramp 5's 9 for its channel's sides, ramp 4's 0-9 for the rock.
- *Lava geysers* (plan `lavaGeysers`: as `lavaBalls` with `balls`; the race file's `lavageyser=<x> <y> <z> <from> <to> <every> <phase> <balls>`, RACEMOD.CPP `LavaGeysers`): six, where the old lap's single lava balls came from (the main channel under the bridge and the jump, the second channel, the lowland, the shore). Every 5-11 s or so, while Twinsen is within 45 cells, one goes off: for 1.8 s a stream of the game's lava balls shot up out of the lava, fast enough to go 2,500 past the height of the road it showers (the bridge is 11,000 over the channel), falling back with gravity, the game's lava burst at its foot every 0.3 s; and a shower of 4-5 lava balls, 0.22 s apart, each one's shadow on the road where it will land (`lavaball=`'s, armed together). `RACE_MAX_LAVA` 48. In a 4-lap race 77 eruptions, Twinsen hit 2 times, the ferryman once.
- *Crystals*: the lowland's field (six), the arch on the east edge; of the garden one is left, inside the upper layer's loop (the crag and the loop fill the rest).
- The test pilot's qualifying lap 37.7-38.6 s; race laps 44-47 s (Twinsen), 45-52 s (the opponents), no rescue.

### Version 4: a two-turn climb, a double spiral down, no jumping down, crystal shards (2026-10-11, later)

The user: "Let's change the upward loop so it's bigger and loops around an extra time, let's also remove the jump down and have a slope instead, jumps downwards feel unnatural to drive and we should try to avoid them if possible. Let's also have a double spiral back down again. Let's also add in some red crystal shards that shoot up into the air from the volcano, then fall back down onto the track, we should see their shadows on the track getting slowly bigger until they hit with an explosion that stuns any cars in the vicinity". The lap is 660 cells now (the test pilot's qualifying lap 52-62 s, race laps 63-74 s).
- *The climb* is twice round a bigger loop (x 487.5-511, z 474-487, 66 cells round) over the plateau's east part, its second turn over its first: the floor lifted from the loop's start at `LIFT_GRADE` -- worked out from the loop's length so that a turn is `HELIX_PITCH` (4,800, the Elevator Platform helix's: the camera behind the car fits between the levels) higher than the one under it -- to 24,300 at its top, east off it over its start. Each turn goes north past the lava fall: in front of it twice, at two heights. (The design's marks along a spiral are found in order round the lap -- `nearest_after` -- its turns being over one another.)
- *The double spiral down*: an ellipse 10 by 7 cells round (534, 461.5), north of the main lava channel (its north rail 1.8 cells from the island's edge, its south one off the channel), two and a half turns clockwise from its west side at the grade's limit, 23,250 down to 11,065 -- each turn 4,352 or more over the one under it -- off its east side south down a slope (a bridge) over the channel, the old jump's way, to 8,100 between the channels. Its corners every 30 degrees round, rounded 3 cells (the ellipse's ends are tight: a 3.8-cell bend at 11 by 6.5, where the pilot dropped to 15 km/h).
- *No jumping down.* The jump over the channel is gone; the second jump and the shore jump, which landed 1,250 and 300 under their lips, land as high as they leave: the ground under the landing and the road after it lifted to the lip's height as the take-off ramp makes it -- curving up from its foot at the jump's angle, higher than the road's own height there (lifted to that, a landing dropped 600 in a cell and a half after touching down: a 144 % grade) -- the road going down after them at the grade.
- *Crystal shards* (plan `crystalShards`: as `lavaGeysers`; `crystalshards=`, `crystal_shard_model=<shard> <piece>`; RACEMOD.CPP Kind 1 lava balls). The models: a red six-sided crystal pointed at both ends, 1,700 long, and a piece of it 0.3 as big, appended to OBJFIX.HQR (`RaceTrackCrystals.InstallCrystalShards`; flat colours, the island palette's ramp 5 reds and ramp 6 points). Five stretches -- the start straight, the climb's first south side and its second east side, the west side, the plateau's south edge -- each every 9-11 s or so while Twinsen is near: shards shot up out of the volcano (the crag's lake, 23,700) and falling back into it, and three aimed ones arcing 3,500 higher than a lava ball and 1.1 s slower onto the stretch, tumbling end over end. Each one's shadow lies on the road where it will land from the start, solid (a see-through one was lost on the dark asphalt), its size -- a sprite's scale divides it -- growing evenly from small (scale 110) to big (8). Landing: a nitro penguin's explosion, six pieces of the shard flying (only to see), and every car within 1,800 stunned for 1.8 s (Twinsen's unless the protection spell or the super jet-pack is on). In a 4-lap race the shards stunned Twinsen 3 times and the opponents 4.

### The invisible wall at the climb's first bend: cube edges the road's width crosses (2026-10-11, later)

The user: "I'm currently blocked from driving forward and I'm not sure why, please debug my current session ... I can reverse backwards and drive forwards to where Twinsen is now, it's like there's an invisible wall blocking us". Read from the live game with gdb (its symbols: `ListObjet`, `NumCube`, `CurrentCubeX`; the race state's `lto_priv` statics), the car was in scene 131, cube (8,7), half a cell east of the edge it shares with scene 130 (x 512, z 485.9, 14,661 up), facing west at 0 km/h.

- *Why*: the climb loop's east legs run along x 511, a cell inside cube (7,7), with the road 4.75 cells either side of its middle. A car on the outside of the first bend crossed x 512: scene 130's own zone into 131 covers its whole east edge up to 15,000, so the car went into scene 131. Scene 131's zones back into 130 covered z 466-482 (above 21,968) and 489.7-505.7 only, and the engine holds the hero at a cube's edge where no cube-change zone covers him (`EXTFUNC.CPP` DoAnimExt: `Nxw` kept `DEMI_BRICK_XZ` inside, `FlagHeroOutX` set). The build gave the scenes a zone only where the lap's *middle* crosses an edge (`RaceTrackScenes.EdgeCrossings`), and the middle never crosses there.
- *The build*: `EdgeCrossings` also follows the road's width -- the raised road by its own points and widths, to its rails and half a cell past them, the ground's lap to its curbs -- and wherever either side of the road reaches over an edge its middle doesn't cross, adds a crossing there each way, unless one at about that place and height already covers it. On Volcano Island that is 9 zones from scene 131 back into 130 along the climb's east legs at both levels, 3 more from 130 into 131 for the second level, and a few between 131 and 132 on the spiral down.
- *The engine* (for tracks built before this, and wherever a car gets over an edge no zone covers): the hero held at the edge in the race car (`RaceMod_EdgeHeld`, noted from DoAnimExt -- the flags themselves are cleared by the next actor's move) is carried over into the next cube's scene (`cube_scene=`) at its near side, the height and the other coordinate kept, as a zone would (`RaceMod_EdgeCross`, called from PERSO.CPP after the hero's zones). It has to be asked for there, in the actors' loop: asked for as the scene was drawn, the drawing's autosave cleared `FlagChgCube` first and the car arrived at the scene's start, on the grid.
- Tested at the user's own place (teleported east over the edge into 131, then driven back west): the old engine and the old build held the car at x 512.5; the new engine alone took it into 130 and on round the bend, and so did the new build's zones alone with the old engine. Five laps with the user's car file on the new engine and build: nothing held, and no crossing the engine had to make itself.

### The race track window's progress bar, and Polar Island without an LBA1 folder (2026-10-11, later)

The user: "add a visible process bar for building tracks in the track builder window", and "Display an error message is somebody tries to build polar island without having their LBA1 asset folder set as apparently this doesn't work and causes nothing to build".

- `RaceTrackService.Build` takes an `IProgress<BuildProgress>` (the part done, what it is doing). Each part has a share of the bar by how long it takes (`BuildSeconds`, measured: the thirteen tracks built together in 62 s, the Desert island's 16 of them, Citadel Island's, the Island of the Francos' and Otringal's about 8 each, most others 1-2; Polar Island added to the folder 4), and each track's steps a share of its own (`BuildSession.Step`: reading the island, the road's textures, the road with its ground and scenery -- most of it --, saving, the other weather's island, the holomap and the cars, the scenes, the story). The window shows the bar under the status line with what is being built, how far and the time so far, scrolled into view, and "Built in 19 s." when it is done; it can't be closed while a build runs (the build would go on without it). `buildtogether` prints the same with `RT_PROGRESS=1`.
- *Polar Island*: a build that has to add the island and has no LBA1 folder threw at its first step, changed nothing, and showed it only in the status line, the log left empty -- nothing seemed to happen. Now the window asks `RaceTrackService.PolarProblem` before anything else, and says what is wrong in an error box (no LBA1 folder set, or a folder without LBA1's SCENE.HQR, LBA_GRI.HQR, LBA_BLL.HQR, LBA_BRK.HQR and RESS.HQR -- choose it under File > Settings); and any build that fails says why in an error box and in the log.

### Version 5: both loops circles, and bigger (2026-10-11, later)

The user: "Let's make both loops in Volcano island bigger and circular".

- *A circle in the plan*: `circle(cx, cz, r, a0, a1)` in volcano_design.py -- the polygon round the circle (its corners at r / cos(θ/2), θ about 30 degrees), each corner rounded with the circle's own radius, so the rounded corners are arcs of the circle itself, meeting at the polygon's sides' middles: the road is an exact circle. The way in and out are tangents: a straight along a side's line runs on into it with no kink.
- *The climb*: a circle 13.5 cells round its middle (84.5 cells a turn; the rectangle was 23.5 by 13, 66 a turn), touching the north straight's line at its north point (501, 463.75) -- the straight runs on into it -- its west side at x 487.5, in front of the lava fall as before. Twice round, then on east along the same line at the top, 24,400. The lift is `HELIX_PITCH` x 1.10 a turn (12.2 %): the end of its first turn passes over the start straight's pit lane, which is level, and at x 1.04 that was 4,584 over it.
- *The double spiral*: a circle 11.5 round (72 cells a turn; the ellipse was 10 by 7, its north rail 1.8 cells from the island's edge), touching the same line at its north point (537, 463.75) -- the top level along to it -- and centred over the main lava channel. Round clockwise until its tangent points at the slope's foot (542, 505): two and a quarter turns and 13 degrees, worked out in the plan (`SPIRAL_EXIT`). It comes down from the top to 11,000 at an even grade (16 %, 5,900 a turn) -- left to the grade's limit it went down to 7,400, and the slope after it took the road through the rocks and crystals by the second jump. A lava geyser now shoots up its open middle (the channel's lava at (537, 479)) and showers its top turn; the one beside the slope moved to (553, 488).
- *Room for them*: the plateau's south road 5 cells further south (z 502, the climb's south rail at 495.5), held at the plateau's height (beside its edge, the ground under its middle is the flank's, and the deck cut 2,400 into the plateau); the pit lane, its fence, the waiting spots, the boost panel and the start line 2.5 cells west (the straight ends where the climb begins). Two more crystals in the garden inside the climb.
- The lap is 710 cells (660 before). The circles reach over the edge between scenes 130 and 131 at several heights: the build adds the crossing zones (scene 131 has 21 zones, 255 the most). Tested with the user's fast car and three opponents: five laps of 28-36 s, the opponents round both circles with it, nothing held at an edge, no falls.

## Attacks: the magic ball's colours and the fake mushroom (2026-10-08)

The user: "an attacking powerup for cars to hit other drivers with utilising Twinsen's magic ball and it's different colours with different behaviours" -- "yellow ball goes in a straight line short distance doesn't bounce - or just darts / Green straight line, sticks to the track for a few seconds bouncing around / Red homes in on the car in front / Fire ball splits into a ball for each opponent that hunts them down and hits them"; and "a fake powerup mushroom that cars can pick up that's identical to the real powerup mushrooms, the car can drop this powerup anywhere, and when another car drives into it the fake mushroom explodes".

Five new power-ups in the mushrooms (RACEMOD.CPP, after the six there were: `raceitem 6`..`10` uses one at once), for Twinsen (the item box, Shift) and for the opponents (at once, as they take it). The item box shows the magic ball in its colour and the fake as the mushroom itself. The ball's colours are OBJFIX 1, 40, 41 and 42 (the inventory's list for the magic ball, `TabObj3D`, picked by `MagicLevel - 1`). Until 2026-10-08 (v2026-10-08k) the box had OBJFIX 0-3 instead -- the holomap, the yellow ball, the dart and Sendell's ball -- taking the inventory's index into that list for the model itself (the user: "You've used the holomap, and sendell's ball as race item symbols, what do these do"). The fire ball's model is the animated fire texture, and the protection spell's its sparkles; they start black and move only while something animates them, so the box runs the texture animation (`DoTextureAnimation`, as the inventory does) and draws from the objects' texture page. Before that both were empty boxes.
- **Yellow magic ball**: darts straight ahead of the car, 11,000 units a second, 14 cells, over the road; it hits the first car it reaches.
- **Green magic ball**: rolls along the track (the guide line), twice a car's speed, from one side of the road to the other and hopping, for 6 s; it hits any car it reaches.
- **Red magic ball**: goes after the car in front (by how far round the race each is) -- along the track, forwards or back, straight at it once within 3,500 -- and hits it; the leader's darts ahead twice as far as a yellow one.
- **Fire ball**: one ball for each of the other cars, each going after its own as the red does.
- **Fake mushroom**: dropped behind the car when used, where it is -- the same model as the real mushrooms (a few copies of the mushroom in each scene, `fake=<scene> <actor>`: a track built before 2026-10-08 has none, and gives none until it is built again). A car that drives into it (not the one that dropped it, for 1.2 s) sets it off: a nitro penguin's blast.
- A hit stops the car as a penguin's blast does (Twinsen's unless the protection spell or the super jet-pack is on, "Hit by the dissident's red magic ball!"); the magic ball's own burst where it hits. Each ball is the magic ball's sprite in its colour (raw sprites 8-11) with its trail behind it, in the scene where it is.
- The weights: the yellow and the fake mostly for the cars in front, the red and the fire ball for the cars at the back (`s_puLead`, `s_puLast`).

**Firing while driving.** In the game the car stops while Twinsen throws his magic ball from it (BUGGY.CPP: the throw took the speed and the steering for aiming). In a race it drives on: the throw no longer stops it or turns it. A ball from Twinsen that hits an opponent's car stops it as the power-ups' balls do (OBJECT.CPP HitObj, `RaceMod_OpponentStruck`) -- its armour took the game's hits without a flinch.

## Twinsen's laser cars (2026-10-08)

The user: "Create extra car models for Twinsen, one that fires green lasers, and one that fires red ... we'll need to patch this so he can also fire whilst driving for both the balls and the laser".
- **The cars**: Twinsen's own buggy (since the user's follow-up: "For Twinsen's laser cars let's keep the look of his original car, it already has an animation for firing bullets just make it so this gun fires the lasers instead"). The car setup's "drive as" has "Twinsen's buggy, firing green lasers" and "... firing red lasers" under his plain buggy (`RaceCarSetup.LaserGreen`/`LaserRed`, -2 and -3). The first version's two separate models (a roadster with a laser cannon on its bonnet, cars 218 and 219 of RACECARS.JSON) are no longer built. A folder built with them has them stand for the two choices: they are left out of the list, and a setup that picked one drives his buggy firing that colour (`RaceCarEngineFile.DriveAsLine`).
- **The bolts**: the game's own (`RaceTrackLasers`, the race file's `laser_models=63 72`, written for every track whatever it was built with). OBJFIX.HQR 63 is the bolt Twinsen's laser pistol fires (his entities' animation 38: a thin green prism 560 long). 72 is the red bolt the enemies' guns fire (entities 31, 51 and 199, `F_THROW_OBJ_3D`: a red spike 800 long; 71 is its green twin). Until the user's "let's reuse the one the game naturally fires" the build appended a bigger green bolt and a red one of its own to OBJFIX.HQR.
- **Firing**: the race file's `drive_laser=1` or `2` (no `drive_as=`: the car keeps its body). The throw key works the buggy's gun as it always did: it opens the bonnet (the firing body) and plays its firing animation, while he drives (BUGGY.CPP). Each shot of that animation (FICHE.CPP `F_THROW_MAGIC_EXTRA`, the buggy's gun) fires a bolt instead of the magic ball (`RaceMod_LaserThrow`). The bolt starts 420 units ahead of the car and flies straight ahead at 17,000 units a second for 34 cells. A bolt that reaches a car (within 1,050) stops it as a magic ball does ("hit by Twinsen's red laser"). Firing doesn't slow the car.

## Bigger mushrooms, the game's own magic ball, faster balls, a quicker roulette (2026-10-08, later)

The user: "The mushroom pick up is far too small to see, let's make this bigger. For the magic ball object, it moves too slowly to catch up with the car in front, we should fix this, especially for the red and fire balls so they home in on their target. It looks like we're using a different model for the magic ball to the in-game one, let's switch to Twinsen's normal ball." Then, after a first go: "When I'd said the mushrooms were too small, I'd meant in the object viewer (as pictured), let's make them smaller again, but a little bigger than they were but not as much. The magic ball powerup when thrown seems very small, let's make this a lot bigger and faster", and "When the item box is rotating through powerups deciding which one to give the player, let's have this go through them a lot faster so it only takes 4-5 seconds to decide on a powerup".
- **The fake mushroom in the item box** fills its slot as the other items do. They are about 4,000 tall, the mushroom a few hundred, so an object whose half size is under 1,200 is drawn nearer, round its middle (the body header's box, bytes 8-31: the camera's distance 16,000 times its half size over 1,900). The other items are drawn as before.
- **The mushrooms on the lap** are the game's small brown mushroom (BODY.HQR 171: 246 across, 307 tall, a quarter of a car's length) half again its size (`RaceTrackOil.MushroomScale` 1.5; 3 for a while). The build adds that copy as body 2 of the mushroom's entity (112), beside the oil slick's body 1 (`RaceTrackOil.InstallBigMushroom`, `IslandScaler.ScaledBody`). The lap's mushrooms and the fake ones are it, so a fake still looks like a real one.
- **The balls' look**: the same sprites as Twinsen's magic ball in each colour (raw sprites 8-11), without the trail of sparkles they had, at four and a half times the game's own size (`RACE_SHOT_SCALE` 16; the game's is 70, ThrowExtra's -1).
- **The game's own ball was drawn wrongly too.** The Island of the Francos' steam jets (2026-10-07) mark an extra drawn laid on its side with a bit of its Scale (`EXTRA_SCALE_TURNED`, bit 24). The game's -1, its sprites' own size, has every bit set, so every sprite it throws at that size -- Twinsen's magic ball, in races and out -- was drawn laid on its side and stretched along its way. Only a scale of its own (positive) carries the mark now (OBJECT.CPP).
- **The speeds**: the yellow 22,000 a second for 28 cells (11,000 and 14 cells at first), the green 18,000 along the track (9,500), the red and the fire 24,000 after their cars (10,500), straight at them once within 6,000 (3,500). A car's top speed is about 4,800, the super jet-pack's twice that. A ball hits a car it passed anywhere along its way since the last frame, not only where it ends the frame (fast, it could jump one). In a test race the red and fire balls hit their cars within half a second; the log says how long each took ("the fire ball hit after 0.3 s").
- **The roulette** settles on the item in 4.5 s, the items going by fast at first (45 of them, 20 a second) and slowing to the one drawn (`RACE_ITEM_ROLL_MS`, `RACE_ITEM_ROLL_STEPS`). It was meant to take 1.5 s past 20 items, but the green ball's "this long" (6 s) was given the same name, `RACE_ROLL_MS`, further down the file, and that redefinition is what the roulette read: six seconds.

## The minimap while a race plays (2026-10-08)

The user: "Update our mini-map for race mode so it fully fits into its viewport and shows all the cars moving around in real time". While Play races a track whose island the editor has open (`MainWindow.RaceMinimap.cs`), the minimap panel shows the lap and 6 cells round it scaled to fit its box (no scrolling), the road drawn over the island -- a raised road its rails' width, a jump's gap left open; a road on the ground the racing line -- and every car as a dot: Twinsen's yellow and bigger, each opponent a colour of its own (its name as the dot's tooltip). The dots move with the cars, asked for every 150 ms over the play's control socket: the engine's console command `racecars` (`RaceMod_Cars`) answers `car <n> <x> <z> <beta> <name>` a line each, island units, n -1 for Twinsen -- an opponent's own car where the scene has it in sight, else where its line has it in the race. When the play stops the minimap goes back to the editor's own (zoomed in, scrolling, the camera's marker).

## The Emerald Moon: a whole second loop, and the pit lane apart from the straight (2026-10-12)

The user: "Emerald moon, the cut out loop needs removing and replacing with a normal loop, the pit lane is devided with a fence, and the
pit lane is too narrow. Let's remove the fence and have an extra strip of road with clear space between the start/finish line and the
pit straight with the pit lane being wider".

![The pit lane on the right, apart from the straight with the start gantry, the opponents waiting in it](racetrack/emerald/pit_lane.png)

- *The second loop* (the south side's) is whole, as the other two: its gap of 35 degrees, leapt upside down, is gone (the plan's loop
  gap 0).
- *The pit lane* is a strip of road of its own beside the straight (plan `raisedStrips`: for each point of the lap, from and to across
  it, cells; RaceTrackPlan.RaisedStrips): from the first corner's end it grows out of the straight's west edge over 5 cells, moves 2.5
  cells away from it over 5 more -- clear space between them, the base's roof under it -- runs beside it past the start line, and comes
  back the same way before the hairpin. It is 8 cells wide, rail to rail (its asphalt 6; the fenced lane's was 4.5). The fence and the
  white stripe are gone; the straight is the road's usual width again, on the roof's middle (it had been moved half the old pit lane's
  width east), and the grid on its middle.
- *The deck*: where the strip meets the straight's edge, the straight's deck reaches across to the strip's far edge, one deck; where it
  is apart, a deck of its own, with its own curbs and rails (`RaceTrackBuilder` StripPieces: RaceTrackRaisedBody.Tile on sections moved
  across). The start line and its gantry: the line reaches across the strip, so a car in the pit lane crosses it too (LapLine); the
  gantry stands over the straight.
- *The engine* (RACEMOD.CPP): the raised road's file has two more numbers on a point with a strip beside it, from and to across
  (`StripLo`, `StripHi`). The floor is the road's or the strip's (RaisedAt, OnStrip); the rail keeps a car on whichever of the two it is
  nearer, the clear space between them an edge like the road's outer one, and where they meet, one floor from the road's far edge to the
  strip's (RaceMod_Rail).
- *Cube edges again*: the straight's rail is a quarter cell short of the edge between scenes 229 and 231 (x 64). The crossing zones the
  build now adds where a road's width reaches over an edge, and the engine's own carrying over (above), took a car riding the rail into
  231 -- half a cell inside it, where a car arrives -- and its rail straight back, a scene change a frame. Now both carry a car over only
  where it can be on the other side: the build where a car's middle reaches more than half a cell past the edge inside its rail
  (`EdgeCrossings`, `Reach`), the engine where its rail lets it be where it arrives (`RailAllows`). Volcano Island's climb keeps its
  zones; the user's own place there still crosses.
- The design script's jump: the island's objects read again since 2026-10-02 put the reactor a little higher, and the ramp search picked
  61 degrees; the ramp is kept at 60 (`THETA_DEG`), as built and raced, and the script says it clears by less than 900 by that reading.
- Tested: a qualifying lap of 53.6 s and a race lap of 64.1 s with the opponents, no checkpoint missed, no car rescued; the pilot through
  the whole pit lane and back onto the straight; a car steered at the clear space held on the straight's edge all the way along; along
  the straight's other rail no change of scene.

## Folders with non-English names, and why a race didn't start (2026-10-12)

A user's video (in Russia): Play's "Race: Elevator Platform", the race car setup's Play, and no race -- Twinsen not where he should be, the
editor's log saying only "LBA2 control socket didn't come up". On a fresh copy of the game with fresh settings the same steps raced (the
prepared save's start and the fallback `cube 120` start alike); what differs on such a machine:

- *Folder names outside ASCII.* SDL's main hands the game its command line in UTF-8, and the C library's narrow file calls (`stat`, `fopen`)
  and `getenv` read paths in the process's code page. A play folder (`lba2-play` beside LBAAssembler.exe -- under `C:\Users\<Cyrillic
  name>` for many) with any letter outside ASCII was "Invalid user directory", and a game folder such was "does not hold the game data":
  the game ended before its window opened, saying so only on its console, and the editor showed "The game ended." over its own view of the
  island. Race files read with a path in them that isn't ASCII were "could not be read" (the raised road) or, the race file itself, nothing
  at all -- the race-track mode on with nothing in it. Fixed with the game's manifest (`packaging/lba2cc.manifest`, resource 1 of type 24
  in `lba2cc.rc`, replacing the toolchain's default one): the UTF-8 code page (Windows 10 1903 and later), so those calls take the paths as
  they are. Tested from Play with the editor and its play folder under `...\Иван\`, and the game folder at a Cyrillic path: raced, Twinsen
  on the grid. (No comments in the manifest: an XML comment can't hold "--", and Windows then won't start the program at all.)
- *The control socket.* The game opens 127.0.0.1:27015 for the editor's script breakpoints, the race minimap and live resizing -- nothing
  the race needs. Where something else has the port (or Windows reserves it: Hyper-V, WSL and Docker reserve ranges of ports), the game says
  `[ctlsrv] bind 127.0.0.1:27015: WSA error 10013` and races on. Another program listening there took the editor's connection and never
  answered its greeting, and the editor waited for ever; it now gives up after 3 s.
- *Saying what happened.* The game says which race file it reads (`[racemod] race file ...`) and when one can't be read (the race file
  itself, a car's line); the editor reads the game's console: a game that ends on an error shows it ("The game ended: [ERROR] ...", and a
  message when its window never appeared), and its warnings so far go into editor.log once the socket is up or given up on. editor.log also
  says whether a play races and which track (`Lba2Play: scene 120, racing Elevator Platform, from the grid`) and the race file it wrote.
- `ScriptRoundTrip` takes `RT_CULTURE=ru-RU` to run as a machine with that language does: the race file and a whole build come out the same
  as with the invariant culture (the decimal comma is not a problem).
- *The cause, as it turned out* (the same user's logs, later that day: their folders' names were plain): the editor sat in their game
  folder, an LBA2 Classic Community install, whose own `lba2cc.exe` was there too -- that project's readme says to unzip it into the
  game's folder -- and the editor took an `lba2cc.exe` beside itself before its own. The game's log said "Built Aug 23 2026" with no
  commit after it (the editor's engine says "Built <date> from commit <hash>"): an engine from before the race-track mode, so no race,
  the opponents standing where the build parks them, the raised roads solid only where their decor boxes are, no control socket and no
  `[racemod]` lines. `Lba2Engine.Find` now always plays the editor's own engine (`LBA2_ENGINE=<path>` picks another, for development),
  and editor.log says which: `Lba2Play: the engine ...\native\lba2cc.<key>.exe`. Tested with a stranger's `lba2cc.exe` beside the
  editor: Play raced the Emperor's palace on the editor's own.

## The Emperor's palace: through the rooms, out of the round window, over the rooftops (2026-10-12)

The user's brief: "through the palace rooms, coming out the window and around the roof tops? Point-to-point i.e. no laps, finishing
with massive jump to harbor teleport? - Twinsen, quick, Dark monk's in the palace, he's killing everybody. Leading into final showdown at
Celebration"; its drivers: "Stan, The pighead w/broom, *Two headed monster". On the Palace island (`docs/LBA2_PALACE_ISLAND.md`), which
the build makes afresh with its scenes (`Terrain/Palace/PalaceTrack.cs`, `RaceTrackIsland.Palace`: Created, OwnScenes, Sprint); the
route `docs/racetrack/palace_track_plan.json` from `tools/RaceTrackPlan/palace_design.py` (it reads the built island's `PALACE.JSON`:
where the rooms, the round window, the ways across the rocks and the harbour's pillar are).

![Through the rooms on the red carpet: a fireplace, windows, the next door](racetrack/palace/rooms.png)

![Over the rooftops among the domed halls, their teal-tiled feet beside the road](racetrack/palace/rooftops.png)

![The runway to the cliff's edge, the Dark Monk's teleporter on its pillar in the bay](racetrack/palace/runway.png)

![In mid-flight into the teleporter: the race won, the picture about to whiten](racetrack/palace/teleporter.png)

![And Twinsen on Celebration Island, by the Dark Monk's statue's lava lake](racetrack/palace/celebration.png)

- *The route*, one raised road from end to end (1,188 cells): the grid in the sixteen rooms' south-east room (154), south of its chest;
  west along the bottom row of rooms door by door, north up the west column and into the long hall (80) -- a red carpet with gold
  borders (deck theme `carpet`), the rooms' doors (two bricks, 8 cells) a little wider than it; north down the hall, a ramp up to the
  round window's sill and out of it over the cliff's edge; over the cloud sea, round to the east and climbing (at most 9.5%) to the
  hilltop -- the deck theme `palace`, cream and teal, on piers to the sea; south along the way across the rocks (x 470, flat, the domes
  ahead); onto the roofs, a swing west into the courtyard under the big dome's hall, back east along the north-east hall's north wall,
  south down its east side and east onto the runway; the jump off the cliff's edge.
- *Point-to-point*: the race-track mode's sprint (`sprint=`, `race_laps=1`): the finish line at the jump's lip (19,976); the jump carried
  (`arcjump=`: 43.5 cells, its top 2,600 over the lip) onto the teleporter's pillar, its top as high as the lip (no drop). Won, the
  picture whitens in mid-flight as the car reaches the ring (`wake_flight=2400`) and Twinsen arrives on Celebration Island, scene 95, by
  the Dark Monk's statue: "Celebration Island... his teleporter brought me to his statue. This is where it ends, Dark Monk!" (`wake=`).
  Twinsen's cry as the grid forms ("The Dark Monk is in the palace, and he's killing everybody! ...") and a loss's line are added to
  Otringal's texts, the arrival line to Celebration Island's, in the game's 6 languages (`PalaceTrack.Texts`).
- *The scenes*: an outside scene is one cube, so each cube the road reaches -- its width and a cell more -- is a scene, from 265 on,
  row by row (24 of them, 265-288; the start's is 288), each a copy of the header of Otringal's scene outside the palace (91: its light
  and music) with Twinsen, the engine's Zoe placeholder, the buggy and a cube-change zone on each edge shared with another. The list is
  worked out from the route built into the program (`PalaceTrack.SceneCubes`): the rooftops' road, moved in among the domes, ran into
  cubes that had none, and the car stopped at the first one's edge.
- *The drivers*: Stan, the road sweeper (the pig-headed sweeper) and the Emperor. The two-headed monster has a car only among the cast's
  (a body of another entity), and an opponent's car has to be one of the racer's own hand-made ones (`RaceDriver.Palace`).
- *The camera* rides the road behind the car (`railCamera`), low and level: the rooms' walls, fireplaces and portraits in the picture, and
  the halls beside the rooftops' road. The race-track mode's rail camera took two more numbers for it (RACEMOD.CPP `railcam=<back> <up>
  <lead> [sight] [aim]`): its sight line to the car ended at the car's wheels and had to clear the road by 500, which lifted the eye to
  some 4,000 over a level road whatever it was asked, looking 33 degrees down -- nothing over the road ahead in the picture. Ending it
  at the car's middle (500) keeps the eye where it is put (2,100 over the road, 9 cells behind: past the near plane round the tightest
  bend), and looking at a point 1,000 over the road ahead makes the view level, the car in its lower third. Other tracks are as they were.
- *The domes* are 26,000 over the roofs: near them the camera shows their halls' walls (teal-tiled feet, pale stone), their tops over
  the picture; they show whole from further off, and in the editor's island view. The island's fog was moved out for them (40,000 to
  70,000) -- inside, looking through several rooms' doors, the far ones fade to the haze. And the engine had never drawn them at all:
  it left out an object any of whose foot's corners lay past the fog's end, and a dome's foot is 86,000 across (`3DEXT/DECORS.CPP`: an
  object of this editor's that is drawn only is left out only when all four are).
- *The race's road on a route open at both ends* (`RaceTrackBuilder` PlaceRaised): a sprint's road is raised from its first point; the
  race file had had no raised road, jumps or cube scenes for it, and the car drove under its deck.
- *With Otringal's track in the same folder* (the user, the same day: "dropping twinsen into the gas and he's dying"): Play races the
  palace as the game's set of tracks (a sprint that ends somewhere else -- the race-track mode's `track=` lines, one car file a track), and
  the race-track mode took the first of them on the island the scene is on. Otringal's is on island 4 too, raced in all its scenes
  (`track=4 0 -1 -1`), and built before the palace: in scene 288 it loaded Otringal's track, drew OTRINGAL.ILE under the palace's grid,
  and Twinsen fell into its clouds (here, the game crashed). Now a track raced in scenes of its own comes first (RACEMOD.CPP
  RaceMod_Island); Otringal's own still races in its scenes.
- Tested with the test pilot (autodrive): 117.6 s with the power-ups off, 131.1 s with them (penguins and fire balls stop a car), 1st of
  4 and won both times, no car rescued; the white flash in mid-flight and Twinsen awake in scene 95. The build takes some 20 s (the
  island and its scenes made afresh each time).

### The palace, second round: wider, more rooms, a loop round the roofs, no white blocks (2026-10-12, later)

The user: "The palace track is far too narrow. We're also getting random white images on screen, as pictured. There's also multiple
places for Twinsen to clip out of the track and end up on the ground. Let's make the inside tracks wider if we can do, and perhaps loop
in more rooms. Add a section where we're outside on the roof looping around the palace roof tops. Double check the end jump works and
correctly teleports us because I had it failing".

![Through the rooms on the wider carpet, a door four bricks wide ahead](racetrack/palace/v2_rooms.png)

![The loop round the palace's roofs, its halls and domes beside the road](racetrack/palace/v2_rooftop_loop.png)

- *Wider*: the rooms' doors are four bricks (16 cells) instead of two, and the line between two facing doors is cleared of furniture
  -- the rooms' pieces, plain blocks and chests (`PalaceIsland`: "the doors made wider"). The road is 11 cells rail to rail indoors
  (5.5 from its middle), 14 outdoors. The engine collides a car with a square box about 2.8 cells from its middle whichever way it
  faces, so the design keeps 1.55 cells past each rail clear as well (`palace_design.py`, `CAR_BOX`).
- *More rooms*: thirteen of the sixteen, the road winding through them row by row (154 west to 151, 155 east to 158, 162 west to 159,
  163) into the long hall, its control points every 4 cells and its bends arcs, so the spline doesn't overshoot by the long straights.
- *The loop round the roofs*: from the hilltop the road runs south along the palace's west side, round its south-west and south-east
  corners on a corridor the island now leaves clear (`PalaceIsland.RingOut`, `PALACE.JSON`'s second corridor), up its east side and onto
  the runway.
- *Off the road and onto the ground*: the rail lets a car go where a raised road lies within 300 of the ground (a track's ends meeting
  the ground road), and indoors the carpet does: a car on its edge went off it onto the floor. The palace's car file says `rail_held=1`
  (`RaceTrackIsland.RailHeld`): the rail holds a car everywhere. And on the outside of a bend the rail clamped a car past one segment's
  end onto the line across that end, frame after frame -- it stopped dead along the outside rail; it now goes round the point the two
  segments share (`RaceMod_Rail`).
- *The white blocks*: a polygon's depth for the depth buffer is its distance times a factor that grows as the far plane is pushed out
  (`Fill_ZBuffer_Factor`, 2^32 / the far plane); for the palace's far domes the product passed 32 bits and wrapped round to a near depth,
  and far polygons -- the domes' pale stone -- were drawn over everything (`LIB386/OBJECT/AFF_OBJ.CPP`). It is worked out in 64 bits and
  clamped now.
- *The jump*: a race won flies into the teleporter, whitens in mid-flight and wakes Twinsen in scene 95; a race lost doesn't -- it is run
  again from the grid (scene 290). Checked both ways.
- Tested with the test pilot down the road's middle (209.4 s) and along each edge (185.6 s, 189.0 s): won, no car rescued, no stop at an
  edge.

## The Emerald Moon: four loops turn and turn about, the road its own width into and out of each; a pit lane out of and into the corners (2026-10-12, later)

The user, of the loops: "the left side of the vertical loops don't capture the car so you're able ot skip the loop and drive straight.
Also the camera seems questionable in places, let's analyse the camera around entire track and make sure it's as smooth and as optimal
as we can get it"; and then: "where we have the vertical loops the track is double width when it doesn't need to be, the loop its self
is double the width, but it should be a single width going into loop, and an offset single width coming out of the loop, let's add an
extra loop so we have two loops going from left to right, and two going from right to left to keep everything balanced. Let's alternate
them so right goes into left, then left goes into right. Let's also smooth out the track coming into and out of hte pit lane so the
entrance and exit are both wider and feed directly out of and into their respective corners" -- with a picture of the pit lane's ends
coloured in.

![The lap: four rings (blue boxes), the road stepping across under each; the pit lane beside the straight](racetrack/emerald/lap_v4.png)

![Into the fourth ring: the road its own width, the ring's way down beside it on its leg](racetrack/emerald/ring_way_in.png)

![The pit lane: out of the corner onto the straight and into the hairpin, each in one straight line](racetrack/emerald/pit_funnels.png)

- *The road steps sideways under each ring.* It comes into the ring in one lane, its own width, and goes on out of it in the other: the
  ring is twice the road's width, its band drifting across from the way in's lane to the way out's. The plan's loops take a sixth
  number for it (`RaceTrackPlan.Loops`): the way out's first point, the first number then the way in's last -- a half cell past the ring's
  foot, the way out starting a half cell short of it. The points between, the step, have no deck and no floor (`TrackRoad.Jog`, then
  `Gap`); the road either side runs its own way (`Geometry` doesn't look across a step); the ring's foot is halfway between and the ring
  along the way in; its legs, no deck beside the road for them now, reach down to the ground. The plan's `keepPoints` has the builder take
  the plan's points as they are: spacing them again cut the step's corners, the way in's last point off its lane, and the ring turned
  1.8 degrees with it.
- *Four rings, in on the right and in on the left in turn*: the east rim's (in on the right), the south side's two (in on the left, then
  on the right: the road between them on the inside lane), the west rim's (in on the left). After every two the road is back on its own
  line, so the stretch from the fourth ring over the reactor to the first -- the jump, the straight, the pit lane -- is as it was. In on
  the right is a negative shift (`loop=`), which the engine takes everywhere (the ring's lane, the line it takes a car in at, the funnel
  closing the road's other half before it, the camera's side) and the ring's body drifts the other way.
- *The lap* is 582 cells: the east rim's road after the first ring 9 cells further east, the south side's middle 9 cells north, and the
  west side straight up onto the west rim at x 9 (4.5 cells from the island's edge) instead of the S. The design script checks each
  ring's footprint for other parts of the lap and the island's objects (none), and that it stands in one cube.
- *The loops' capture* (the first report): a car came into a ring anywhere from its lane's far edge to a little past the other lane, and
  the double road's outer part on the side the car goes in on was past that -- it drove on past the foot. It now takes the car anywhere on
  the way in (`RACE_LOOP_ENTRY` past either edge), and not the ring it has just come round: put down at its foot, a hair short of the line,
  a car went round a second time (`RACE_LOOP_AGAIN`).
- *The opponents*: their line steps from lane to lane at the ring's foot (`RaceTrackBuilder.PlanRacePath`: held on the lane's middle 8
  cells either side, no bend counted at the step); round the ring, an opponent goes on from the way out's foot (RACEMOD.CPP
  `OpponentLoops`, `LoopOut`) -- it had gone on from the way in's lane and slid across to the other, after every ring, at its speed. The
  test pilot doesn't look across a ring's foot either (it steered for the other lane short of the ring).
- *The pit lane*: its outer edge runs straight from the middle of the corner onto the straight to its own edge, along it, and straight
  into the middle of the hairpin at the straight's end; the clear space between it and the straight is where it was. On the outside of a
  bend the engine's floor of a strip beside the road had gaps -- each segment's floor stopped at its end, and the wedge on the outside
  between one segment's end and the next one's start was neither's -- and a car on the pit lane's new mouths fell through. The strip's edge
  runs round the point they share there now, as the road's own does (`OnStrip`).
- Tested with the test pilot: a qualifying lap of 57.5 s, a race lap of 63.6 s, the opponents 69.8 s and 77.1 s, every ring taken by every
  car, no checkpoint missed, no car rescued.

### The camera round the lap

Measured with the engine's camera trace (console `camtrace 1`: each frame the run's tick, the eye, what it looks at, the hero, the cube
and which camera placed the eye -- `arm`, `loop`, `arc`, `leave` ...) and `tools/RaceTrackPlan/camtrace.py`, which turns a run's trace
into how far the eye moves a frame, how sharply, how far the view turns, where the car is in the picture, and -- given a car file's rings
(`RINGS=`) -- the frames with a ring between the eye and the car.

![Out of a ring: the camera beside the car at the road's height, the ring going by](racetrack/emerald/ring_way_out.png)

![Round a ring, watched down its axis from the side the car went in on](racetrack/emerald/ring_round.png)

- *No cuts*: a race-track camera's eye (`FollowCamEyeHook`: the loop's, the carried jump's, the rail's ...) was taken in one frame, and
  given back in one -- 16,000 to 22,000 units and 90 to 136 degrees at a time round the loops and the reactor's jump. The view now swings
  round the car onto it over 40 frames and back onto the arm's over 45, its length eased and the car kept in the picture
  (`FOLLOWCAM.CPP`, `FollowCamSwing`); the arm's own angle is held on its aim meanwhile. The worst frame over a lap now: 813 units and 4.4 degrees, easing back off a ring.
- *Round a ring* the eye stands out on the side the car goes in on (from the other, its swing round from behind the car passed behind the
  ring's way down, and the car was hidden), level with the ring's middle, looking down its axis, its aim drawn only 0.15 towards the car:
  the ring is a band, open at its sides, and the car is seen all the way round inside it. Over the ring's last three eighths it comes down
  to a quarter of the radius over the road.
- *Out of a ring* the arm's eye, behind the car and over it, would look at it through the ring's way down for some 16 cells. So the camera
  follows the car beside it at the road's height, its sight under the ring's front, until the arm's eye -- behind the car as it heads --
  and the way back there are clear of the ring, and only then eases back (RACEMOD.CPP `LoopCamera`). Frames with a ring between the eye and
  the car over a lap: 271 before, 49 now -- a few at each ring as the sight passes just over its edge rail.
- *By the island's edge* (the fourth ring) the eye comes nearer, onto the island. *Across a cube's edge* (the first ring is by one) the
  eye held from the last frame is moved into the new cube's coordinates (`FollowCamCubeJump`): the view had swung in from 32,000 away.

## No power-ups on the qualifying lap (2026-10-12)

The user: "Remove power ups while a car is driving a qualifying lap". While the qualifying lap is driven (the race-track mode's
`PHASE_QUALIFYING`) the mushrooms are off the track and give nothing, the item box isn't drawn, and a track's Gazogem fuel all race
(`fuel_always=`, the Island of the Francos') doesn't push the car; from the grid on, the race has them all (RACEMOD.CPP `Qualifying`). The
opponents' qualifying times are their planned laps, none of the power-ups in them -- the player's lap is now timed the same way. (The test
pilot's qualifying lap on the Emerald Moon took seven power-ups, a super jet-pack among them: 57.5 s; without them 63.6 s, as a race lap.)
The game's log says so as the lap starts: `[racemod] the qualifying lap: no power-ups until the race`.

## Playing full screen, and the race car's handling up to ten times (2026-10-12)

- *F11* while a scene plays (LBA2) puts the game full screen on the editor's screen, and F11 again puts it back in the editor. The game's
  window is moved into a window of its own over the whole screen and the game switched to the screen's size (`resolution WxH`, as for
  any other change of the play area -- up to 1920 by 1024, the engine's limit), and back (`EmbeddedGameHost.ToggleFullScreen`). The game
  has the keyboard, so the editor takes F11 before it does (a low-level keyboard hook while the play lasts, `PlayKeyHook`, on a thread
  of its own, only while one of the editor's windows or the game's is in front): the engine's own F11, a debug build's benchmark, stops
  the game until a key is pressed.
- *Every change of the game's size went back.* After a `resolution` switch the engine asks whether to keep it, and the editor answered
  with Enter -- but the dialog opens on Revert, so the game went back to the size it was launched at, scaled into the new area (in the
  editor as well as full screen). It answers Y now, Keep whatever has the focus (`key 28`).
- After such a switch SDL gives the game's window its own style again, a popup's: the game's window can then be the one in front, which
  is why the hook takes F11 from it too. (Made a child's again, SDL put it back at once, again and again; it is left so.)
- *The race car setup* (the user: "modify the car handling sliders so we can tweak them up to 1000% also expose top speed to be
  configured. I want to test different combinations to optimise for different tracks"): acceleration, braking, rolling to a stop and
  steering go up to 1000 % of the original buggy's (from 10 %); a top speed slider on the Car tab sets the top gear's, the other gears
  scaled with it; each gear's own slider, and reverse, go up to 270 km/h (`RaceCarSetup.MaxKmh`, ten times the original buggy's 27).

## The Emperor's palace, third round: the rooms open, arrows on the floor, a bigger roof top (2026-10-12, later)

The user: "For our palace track let's scale up the roof top so it covers more of the outside of the palace area. For the inside of the
palace let's remove the track but add some arrows to allow cars to take multiple directions, it has a single entry and exit area so all
cars will ultimately end up back on the same track".

![The sprint: the rooms (no road, the opponents' ways in light blue, the arrows white), the long hall, the round window, out over the cloud sea and round onto the bigger roof top, the jump to the pillar](racetrack/palace/v3_lap.png)

![The grid in room 154: an arrow ahead (west) and one to the right (north, through the door to room 158)](racetrack/palace/v3_rooms_arrows.png)

![North through the doors, the arrow on the floor](racetrack/palace/v3_hall_door.png)

![Round the roof top, a hall of the domes beside the road](racetrack/palace/v3_roof_top.png)

- *The rooms have no road.* The plan's `bare` runs from the route's start to the foot of the ramp up to the round window: no deck, no rail,
  no floor of the race-track mode's (`RaceTrackPlan.Bare`, `TrackRoad.Bare`). The cars drive the rooms' own floor, and go where they like
  through the doors -- all four walls of a room have one. The race-track mode holds them on the floor wherever the plan's `openAreas` are
  (the sixteen rooms, the long hall: `open_area=` in the car file, RACEMOD.CPP `OpenAreaAt`): the floor's drawn top, 10,640, is under them
  whatever the boxes say there -- the floor's collision is lower, and at a cube's edge a car had dropped to the hill's ground under the
  rooms. Every cube of the areas has a scene (`PalaceTrack.CubesOf`), so a car can cross into any of them.
- *Arrows on the floor* (`floorArrows`, `RaceTrackBuilder.PlaceFloorArrows`): a flat arrow five cells long in front of each room's west and
  north doors -- the ways on towards room 163 -- and two up the long hall; a decor each, one body for each way they point, nothing touching
  its box. Every way from the start (room 154, the south-east) to room 163 (the north-west) and its door into the hall goes west or north.
- *The opponents each take their own way* (`openRoutes`, `RaceTrackBuilder.RouteThrough`): a driver's line from the grid to where it meets
  the route in the long hall is replaced by the plan's way, a cell apart, with each point's bend for the engine's speed plan. The first
  driver keeps the route (west along the bottom row, north up the west column -- the test pilot's way); the second goes north up the east
  column and west along the top row; the third a staircase from room 153, west and north by turns. The design script checks each way, a
  car's box either side, against the rooms' walls and furniture.
- *The camera in the rooms* follows the car's own trail (RACEMOD.CPP `OpenCamera`, `TrailStep`): where there is no road the rail camera
  has nothing to ride, so the eye walks back along the way the car came, as far back and as high as the rail camera's, and looks ahead of
  the car as it heads. Out of the window it hands over to the rail camera (85 units, 0.6 degrees in a frame).
- *A bigger roof top.* The hill's flat top was a terrace 10 cells round the rooms and rocks beyond -- the corner of the rooms' L between
  the long hall and the sixteen rooms was all rocks, up to 3,800 over the roofs. It is flat now over that corner and 24 cells round the
  rooms' outline (`PalaceCliff.Terrace`, measured from the outline's hull), the rocks only at the plateau's rim
  (`docs/LBA2_PALACE_ISLAND.md`). The road over it: out of the window north over the cloud sea and round to the east (182 cells, climbing
  9 % at the most), down onto the roof top east of the long hall, south-east along the north-east coast, back west in a hairpin along the
  sixteen rooms' north side and over the long hall's roof between its towers and the domes, south down the west side, east along the
  south side, north up the sixteen rooms' roof's east side between the domes and its edge, and east along the runway: 1,050 cells round
  the roof top, where it was some 750.
- *Floors and ceilings that went near the camera.* The engine leaves out a body's polygon with any corner behind the camera's near plane
  (`LIB386/OBJECT/AFF_OBJ.CPP` `TestVisible`), and the palace's floors, ceilings and walls were rectangles merged a dozen cells long: one
  passing under or beside the camera went from the picture whole -- the hill's grey paving under the rooms showed through the floor, the
  sky through the long hall's roof (it was so in the second round too, under the carpet road's edges). The palace's faces are a cell
  across at the most now (`PalaceIsland.NearRun`; a wall's still runs up it), and the roofs' undersides squares of a cell: what goes is
  under the bottom of the picture with the near plane at 2,000 or less -- the game's 3,000 at 480 lines high, scaled down for more lines
  (at the test pilot's 640 x 480 a sliver at the bottom edge can still go). 33,000 faces in 88 bodies instead of 19,000 in 67.
- *Scene to scene and back at a cube's edge.* A change of cube puts the car a cell on: from half a brick inside the cube it leaves to half
  a brick inside the next. With an opponent's car just ahead -- nose to tail up the long hall -- that put it against the other car, whose
  box pushed it back over the edge, into the scene it had just left, a cell on again: every frame, four or five times, the car standing
  still and the camera jolting 1,000 units. A car that has just come into a scene isn't taken straight back into the one it left for half
  a second now (RACEMOD.CPP `RaceMod_CubeChangeAllowed`, in OBJECT.CPP `GereZoneChangeCube` and the race-track mode's own crossing): held
  at the edge, it goes on as the car ahead does. (Every race track's cube changes go through it.)
- Tested with the test pilot down the route's middle (183.2 s, no power-ups), with power-ups (179.4 s) and along the road's side (185.9 s):
  won every time, the opponents on their three ways, no car rescued, no cube changed back and forth, the jump into the teleporter and
  Twinsen awake in scene 95.

## The Emperor's palace, fourth round: no way out through the walls, a bigger roof top, the race ends out of the round window (2026-10-13)

The user: "The inside of our Palace track has bugs, in a few places I've clipped from the inside to the outside. The roof of our Palace
needs a lot of work. Let's make the roof top bigger, and remove the track from the outside for now. The track on the outside needs to be
raised in the air looping around the giant turrets that form the roof, which means the existing track section outside needs to go".

![The rooms flooded at a car's size from the start (green): every wall stands, nothing reaches outside](racetrack/palace/v4_sealed_rooms.png)

![Up the ramp to the round window: the finish line is in it](racetrack/palace/v4_window_ramp.png)

![Out of the window over the cloud sea: won, the teleporter's white takes the car in mid-air](racetrack/palace/v4_leap.png)

- *Out through the walls.* The rooms' collision is fine as the island builds it; the race track's build cut it. Where a decor stands in a
  raised road's space and is kept (the palace's are: `keepAbove`), its box is cut down to end under the road -- and the route through
  the rooms, a raised road with no road at all (`bare`), counted, at the floor's height: the rooms' east wall, one long box, was cut to
  end under the floor along its whole length, and the car drove through it and up onto the hilltop (two places on the long hall's west
  wall the same). Where there is no road nothing is cut now (`RoadHit.Bare`, `ClearDecors`); and a decor only drawn -- its box's top under
  its bottom -- keeps that box (cut down, it became a box things hit, which the engine culls as a game decor's: walls went from the
  picture). Checked by flooding the rooms' free floor at a car's size from the start: before, it ran out along the whole east side; now
  it stays in. And as a net (RACEMOD.CPP `RaceMod_OpenWall`): from an open area's floor a car doesn't move onto ground well over the floor
  -- the hill round the rooms -- nor out of every area but onto the raised road; held as by a wall.
- *A bigger roof top*: flat 54 cells round the palace on a plateau reaching 66 (`PalaceCliff`; `docs/LBA2_PALACE_ISLAND.md`). The island
  grew to 8 x 11 cubes and the palace moved in it; the design script reads where from `PALACE.JSON`, the start line and the grid placed
  from room 154's own door lines (still in one cube).
- *The road outside is gone* (until the road round the roof's turrets, high in the air). The race ends in the round window: the finish
  line at the window's outer face, at the top of the ramp up from the long hall, and the leap out of it -- a carried jump, 108 cells north
  over the cloud sea between the hill's two arms, 2,600 up at its top -- in which the Dark Monk's teleporter takes a winner: the picture
  whitens 2.4 s on and Twinsen wakes on Celebration Island. Where the leap would come down there is no road: no car reaches it.
- *A lost race out of a carried jump* (found here): the race again from the grid was asked for while the car was still being carried, and
  the next frame the flight put it back on its way and on into the next cube -- the car flew on, came down on the road beyond, drove off
  its end into the sea, was put back on the road four times. A loss now ends the flight first (`s_arcOn`).
- Tested with the test pilot: 59.6 s and won (no power-ups), 48.5 s and won (power-ups), along the road's side 62.1 s and lost -- the race
  run again from the grid; no car rescued, Twinsen awake in scene 95 after each win. The start scene is 290 now.

### The island only just round the palace, the turrets over all of it (2026-10-13, later)

The user: "let's also make the turrets taller and wider", then "Make the island small enough so that it only just fits the inside of the
palace, then have the turret's on the roof cover the whole of our inside of the palace". The island is the palace's L and a few cells, its
cliffs straight down to the cloud sea, and Otringal's four domed halls stand on the roof as six turrets 31,000 to 57,000 high over all of
it (`docs/LBA2_PALACE_ISLAND.md`). The race is as it was -- the rooms, the long hall, the leap out of the round window -- the palace in
another place in the smaller island (the plan from `PALACE.JSON` again). Checked: the rooms flooded from the start reach nowhere outside;
the test pilot won in 61.1 s and along the road's side in 56.1 s, and with power-ups lost (64.6 s) and was run again from the grid, no car
rescued.

### Nothing missing near the camera (2026-10-13, later)

The user: "There's terrain clipping through the palace floor and visible inside as pictured, we should fix this also" (and the roof:
"a lot of points on our new roof that isn't full and can be seen through").

![Before (left) and after: the ground under the floor in a band along the screen's foot, gone](racetrack/palace/v5_floor_cut.png)

- *Polygons cut at the camera's near plane* (`LIB386/OBJECT/AFF_OBJ.CPP`). A body's polygon with a corner behind the near plane (3,000
  in front of the camera at 480 lines; `ProjectList` marks the corner, `TestVisible` dropped the polygon) went from the picture whole, a
  hole as big as the polygon. It now goes in the body's sorted list where it would have gone whole (`QueueClipped`, a 0xFF type byte), and
  is drawn by its own type's handler -- its colour, light, texture and map points as for any other -- whose `Fill_Poly` call goes, while
  it is drawn, to `ClipFill`: the corners in the camera's space, cut at the near plane (as the terrain's triangles are, `ClipperZ` at
  NearClip + 1) and at the sides of a view 30,000 pixels either way of the middle (a screen point is 16 bits), their map points and light
  in between, projected, faced as `TestVisible` faces a polygon, and filled. A corner right on the camera's plane is moved off it while the
  handler runs (its W divides by the depth). A body partly behind the camera is drawn even when the points in front are all off the screen
  (`SomeBehind`). In the isometric view nothing changes.
- *Drawn close* (`MASK_OBJECT_CLOSE`, Info bit 11 -- no retail body has it; `CLOSE_NEAR` 400 in `AFF_OBJ.H`). Cut at the near plane the
  palace's floor ended 3,000 in front of the camera, on the screen with the camera tilted down, and the ground under it showed in a band
  along the screen's foot. A body with the flag has its polygons cut 400 in front instead -- what is nearer than the engine's near plane
  is drawn too -- and `DECORS.CPP` keeps one of LBA Assembler's drawn-only decors while a corner of its foot is 400 in front (not the near
  plane's 3,000). The palace's rooms, roof rock and turrets have it (`BrickBuilding.CloseFlag`).
- *The rooms in every cube they are seen from* (`docs/LBA2_PALACE_ISLAND.md`): the game draws the decors of the 3 x 3 cubes round the
  camera's, and a room further off, seen down a row of doors, wasn't drawn. (First put down here as what brought back the floor under the
  camera; that was the cut quads. The copies made the palace slow -- the next section.)
- *Race-track pieces up to 1,000 a cube* (`IslandDecors.MaxPerCubeRaceTrack`). The builder placed its pieces up to the retail engine's
  200 decors a cube; with the rooms in every cube the palace's start gate and 23 of its 27 floor arrows were left out (the report even said
  the gate was "off the island" -- now "its cube full"). A race track is played in this editor's engine only, which draws 1,000.
- Found on the way: the first cut covered one-colour triangles only -- a quad's header carries its quad bit (0x8000) in `TypePoly`, and
  the floor's quads never matched.
- Checked: the test pilot through the rooms, a picture every 40 frames -- the floor to the screen's foot in every one; won in 60.7 s, and
  with power-ups lost (54.5 s; opponents' skill is drawn anew each run); no car rescued. The Emerald Moon's lap, a picture every 30 frames:
  the loops and rings as before, 63.5 s. The thirteen other tracks built by the last commit's code and this one: all 42 files the same.

### The palace fast again (2026-10-13, later still)

The user: "Our palace track is now very laggy, and it's very laggy to view in our editor window too, is there anything we can do to
improve this". Measured with the engine's `perftrace` (headless, 1280 x 720, the test pilot through the rooms and the hall: `perf.sh`):
the frame took 61 ms (16 a second) in the rooms where the island before the copies had taken 25 ms; the editor's 3D view took 330 to
760 ms a frame.

- *Every copy drawn.* A cube's decors are drawn with its ground (`TERRAIN.CPP` `AffichageTerrainZBuf`), and the game draws the 3 x 3
  cubes round the camera's -- the editor's view every cube of the island (`EXTFUNC.CPP` `AffGrilleExtArea`). A body in every cube within
  the far plane was drawn once for each of them: nine times a frame in the game, some thirty in the editor, each time all its points
  turned and projected and its polygons filled at the same place. One of LBA Assembler's drawn-only decors -- its box's top under its
  bottom -- is now drawn once a frame (`DECORS.CPP` `DrawnThisFrame`: the same body at the same place in the island, turned the same;
  `LoadCube` notes which cube's list is loaded, `ClsTerrainZBuf` starts a frame). A race track's decks, copied into each cube they
  reach, were drawn twice where they cross a cube's edge: once now. A retail decor is drawn as before.
- *Out of the view, left out first.* Such a decor wholly behind the camera, off to a side or past the far plane is left out before its
  points are turned, by its body's own box (its points', worked out once a body; `DecorOutOfView`, `BoxOutOfView`). A few per cent.
- *The editor's view.* Its cube data was unpacked again every frame: the per-cube caches held 20 cubes' worth (the retail engine draws 9)
  and, by memory, 10 of the palace's decor lists of 18 KB; the editor goes round the 54 of the palace's island. They hold a whole island
  now, 16 x 16 cubes (`DEFINES.H` `MAX_*_MEM`, `LBA_EXT.H` `ISLE_CUBE_SLOTS`; some 35 MB): 22 ms of the frame to 0.2. And a cube wholly
  out of the view is neither loaded nor drawn; the 3 x 3 round the camera, drawn by the loop over the island's cubes, isn't drawn again
  before it (`DrawHorizon2ZBuf`).
- *The cut polygons* (the near plane's, above) are cut at 8,000 pixels either way of the middle, not 30,000: the filler cuts a polygon to
  the screen in 16.16 fixed point and two corners more than 32,767 apart overflowed it.
- Tried and left: the rooms' faces merged back into rectangles of up to 12 cells (19,000 faces, not 33,000): 10 % faster, but the filler
  takes a polygon's depth straight across the screen, and on a long rectangle running away from the camera it is too far in the middle --
  the ground under the floor came through in big patches. The faces stay a cell across (`PalaceIsland.NearRun`).

| | rooms (frame 400) | rooms (1200) | hall (2400) | editor's view |
|---|---|---|---|---|
| v2026-10-13c | 61 ms | 58 ms | 31 ms | 330-760 ms |
| the island before the copies, the engine then | 25 ms | 22 ms | 20 ms | -- |
| now | 19 ms | 18 ms | 13 ms | 29-54 ms |

Checked: the test pilot through the palace, a picture every 40 frames, as before (won in 58.8 s; with power-ups 49.5 s); the Emerald
Moon's lap a picture every 30 frames: 81 of 111 the same to the pixel, the rest by a few pixels but the last five, where the grid's
opponents stood in other places (drawn at random); the editor's views of the palace as before.

## The Emperor's palace, fifth round: one red carpet, the palace's people in the cars' way, pictures on the walls (2026-10-13, evening)

The user: "Let's simplify the flooring inside the palace. Let's make the main road section that's mostly red at the moment pure red just
with the white arrows. The surrounding area varies a lot, let's keep this consistent with a check pattern based on the green ... The
retail game has enemies inside the palace, 3 pigs, Stan from Time commando, and lots of soldiers with swords, let's add some of these
enemies inside as obstacles, perhaps the could chase and attack the cars, or throw random things at them"; then "let's see if we can make
the colours on the walls more consistent too as they're currently very blocky. Let's also see if we can generate some pictures to hang on
the walls based on some of the games characters".

![The road one red under its arrow, the floor's checks, Twinsen's picture; a soldier at his post, the red pighead at his work](racetrack/palace/v6_rooms.png)

![A soldier has struck the car in front: it is stopped, stars round it](racetrack/palace/v6_strike.png)

![The long hall hung with the characters' pictures](racetrack/palace/v6_hall.png)

- *The floor and the walls* (the island: `docs/LBA2_PALACE_ISLAND.md`). The carpets the doors lead to are one red under the arrows, and
  the rest of the floor is checked in the tiles' two greens. The rooms' walls are in bands: skirting, trim, wallpaper, trim, cornice.
- *The palace's people* (`Terrain/RaceTrackGuards.cs`; `RACEMOD.CPP` `Guards`). They come from the plan's `guards`, which
  `palace_design.py` places, and reach the engine as `guard=` lines in the race car file: kind, post, floor, the way it faces, leash, its
  animations, then a (scene, actor) pair for each scene it has an actor in. There are 21 of them.
  - *19 of the Emperor's soldiers*, sword out (entity 184, body 2: BODY.HQR 261), in their fighting stance (animation 434). There is one
    in every room but the start's, at a corner of its carpets' crossing, 7 cells off each way, on a leash of 6.5 cells. Four more stand
    along the long hall's carpet, 8 cells from its middle, either side by turns, on a leash of 7.
  - *A soldier's attack.* When a car comes his way within 9,000, he walks (animation 437, 1,800 a second) towards where it will pass, no
    further than his leash from his post, turned to it. He strikes (one of his four strikes, 142, 277, 438 or 439, at random) when the
    car will be in reach as the blow lands, 600 ms in. A car whose middle is within 1,350 of his is then stopped 1.1 s (stars round it:
    `CarStruckFor`) and let be for 1.5 s after. Then he walks back to his post.
  - *Running him down.* A car whose middle comes within 700 of his knocks him back (animation 6, 1.7 s) and keeps 0.6 of its speed.
  - *Two pigheads*: the red one with the hammer (entity 313, at his work: animation 231) in room 152, and the brown one with the garden
    tool (314, animation 585) in room 163, where every way meets before the long hall. Each stands 14 and 13 cells off the crossing, on a
    leash of 1.
  - *A pighead's throw.* At a car between 2,500 and 11,000 from him, he throws (animation 142) one of twelve of the game's things at
    random: a vase, a key, a coin, a ring, fruit, mushrooms, tins, a stone, a bone, a sausage, meat or a rock (SPRIRAW.HQR 5, 6, 18, 19,
    20, 28, 31, 55, 57, 59, 60, 124). He aims at where the car will be when it comes down, and throws as the game throws (`ThrowExtra`,
    4,500 a second; never a flight of more than 2.4 s), every 2.4 to 3.6 s. A car within 950 of where it lands is stopped 0.9 s.
  - *Not in the way:* Stan and the yellow pighead with the broom. They race, in the user's line-up: Stan in his time machine, and the
    road sweeper.
  - *Their actors.* Each has an actor in every scene within 40 cells of his post, because the game draws a scene's actors only while it
    is the one about. The actors' scripts are gone: the race-track mode moves and poses the copy in the scene about, and hides it further
    than 40 cells from Twinsen. In the other scenes they strike and throw all the same, at the opponents there. Before the start they
    stand at their posts.
  - *First placed*, the pigheads stood in rooms 157 and 160, which only the opponents' other ways cross. No car came within 11,000 of
    them, and they threw nothing.
- *Pictures of the game's characters.* 28 pictures of 8 characters hang on the rooms' walls (`Terrain/Palace/PalacePortraits.cs`). They
  are painted from the game folder's own bodies when the island is built, onto two pages of the island's object textures, and each is
  one textured polygon.
- *Checked* with the test pilot:
  - no power-ups: won in 61.66 s. Soldiers struck the cars (Twinsen's down to 12 km/h at one), and the pigheads' throws hit Twinsen, the
    Emperor and the road sweeper.
  - with power-ups: won in 62.90 s (before the pigheads moved). Another run wasn't over in 72 s: Stan's lightning shrank Twinsen's car
    for 15 s.
  - no car was rescued in any run.

## The Emperor's palace, sixth round: round the turrets on the roof, a fireplace that's a teleporter, giants (2026-10-13, night)

The user: "The start area needs changing so the start line lines up with the straight of the track", "The enemies are too easy to avoid,
perhaps making them bigger would help with this?", the fireplace in room 151 "a teleporter so driving into it brings us out of a second
fire place", "Add a raised track outside on the roof that loops around the roof turrets", "Enlarge the track width so it's the same width
as a door way", and "Add an entrance door to the palace in the start room". The island's part: `docs/LBA2_PALACE_ISLAND.md`.

![The route: the rooms (blue), out of the round window and climbing past the long hall's turrets, round the four turrets on the roof
(white), the leap through the teleporter on the harbour's pillar; the fireplace teleporter (magenta), the guards (orange)](racetrack/palace/v7_route.png)

![On the roof, between the turrets](racetrack/palace/v7_roof.png)

![A soldier twice the game's size](racetrack/palace/v7_giant.png)

- *The start on the straight.* The grid is in front of the palace's new doors in room 154's east wall, on the bottom row's carpet line,
  and the start line is ahead of it on the same line (the room's chest, which put the grid off the line, is against the north wall now).
  The grid's back is 12 cells from the doors, so the camera behind it is clear of their frame.
- *The road round the turrets* (`palace_design.py`, fifth version). Out of the round window a raised road 1,500 over the roof's rock
  (`RAISE`), 7 cells either side of its middle, 6 in the ways between the turrets:
  1. north and east over the cloud sea, climbing (at most 9 %), and south past the long hall's turrets;
  2. on south down the middle way between the four turrets on the sixteen rooms' roof;
  3. west through the middle way across, south down the palace's west side, east along its south side and north up its east side:
     round three sides of the south-west turret and two of the south-east one;
  4. east off the roof's edge: the leap over the harbour (a carried jump, 80 cells) through the Dark Monk's teleporter on its pillar, the
     finish line at its lip. A win's teleporter takes the car in mid-air, as the round window's leap did.
  The turrets were made to leave room (`SlotFill` 0.80); a first way, out east over the sea and round by the turrets' north side, had
  half a minute of road in the fog with nothing beside it. The race is about 2 minutes 40 at the top speed now, 105 seconds of it outside.
- *The teleporter* (`RACEMOD.CPP` `Teleport`, `teleport=`; the plan's `teleports`, from the island's layout). The player's car driven
  into room 151's fireplace comes out of room 159's at its speed, looking out of it. It is a short cut past room 155: about 5 seconds.
  It goes into the other cube's scene as a carried jump does (`NewCube`, the car where it was until the scene is in). The open areas'
  camera starts its trail again, and the lap forgets where the car was. The race's places find the car on the line again
  (`PlayerProgress`' whole-line search).
- *Giants* (`RaceTrackGuards.Install`, `guard_size=`). Each kind's body is twice the game's (`RaceTrackSmallCars.Scaled`), at the end of
  BODY.HQR and given to its entity as body 20 (`BigBody`). The race-track mode scales their reach, their pace and how near a car runs into
  them by the same, and how far they see a car coming by its square root. The carpets are four bricks across now, so the guards stand
  further off them (`CARPET_HALF` 8) and step out further (a leash of 10 cells).
- *Checked* with the test pilot:
  - no power-ups: won in 178.44 s; the giants stopped the car more than once; no car rescued.
  - the teleporter: the car driven straight into room 151's fireplace came out of room 159's facing east at 4,836 units a second, in
    another cube's scene, from 4th to 1st, and drove on.
  - with power-ups: won in 157.58 s, no car rescued.

## The Emperor's palace, seventh round: the user's own way round the roof, a spiral up to the take-off ramp, a secret warp (2026-10-13, night)

The user, with a drawing of the island from above: "I've added an image of a proposed new track route for outside the palace it's in lime
green, starting at the point the current track exits the building, at the end of the route let's add a loop of track going up into the air,
culminating with a large take off ramp, the spiral part isn't drawn"; and, with a picture of room 151: "Our exit fireplace is in the wrong
place ... the exit fireplace should be between the two portraits", then "Make the teleport linked fireplaces visually identical to the
other fireplaces, they're a secret warp point".

![The route (getting redder as it climbs) round the turrets as the user drew it, and the spiral up to the leap through the pillar's teleporter](racetrack/palace/v8_route.png)

![On the spiral: its turns over one another](racetrack/palace/v8_spiral.png)

![Down the straight off the spiral's top to the take-off ramp, the teleporter's ring on its pillar ahead](racetrack/palace/v8_ring.png)

- *The user's way* (`palace_design.py`; the drawing was the island turned a quarter round, north to its left). Out of the round window a
  hook east over the sea and back west between the long hall's two turrets (they were made smaller for it: `HallFill` 0.70), south down
  the west side of the second, east along the north-west turret's north side, south down the middle way, west through the middle way
  across, round the south-west turret (south down the west side, east along the south side, north up the middle way), east through the
  middle way's east half (5.5 cells either side there: the turrets 16 cells apart), north up the east side and west to the spiral. Each
  line is where the turrets leave the most room, measured on the island's collision.
- *Heights*: the road climbs out of the window (at most 10 %) to 380 over the long hall's roof, then to 1,500 over the roof
  (`RAISE`), and on round the south-west turret 4,500 more (`BRIDGE`): the second time through the middle crossing it passes over the
  first.
- *The spiral* over the sea north of the turrets: one and a half turns (`SPIRAL_LOOPS`) climbing 5,000 (`SPIRAL_RISE`), 3,333 a turn, so
  each turn passes 2,861 or more over the one under it. Off its top a straight east to a take-off ramp rising 1,000 over 12 cells
  (`KICK`, `RUNUP`): the leap -- a carried jump, the finish line at its lip, 31,216 high -- through the Dark Monk's teleporter. The
  teleporter's pillar was moved onto the leap's line and raised to it (`PalaceIsland.PillarNorth`, `PillarRise`). A win's teleporter
  takes the car in mid-air as before; the leap's top is 32,616, under 32,767.
- *The design script's new check*: where the road passes over itself (the middle crossing, the spiral's turns, the straight off its
  top), the least height between the two (`the road over itself`), with a warning under 2,800.
- *Into the wrong cube at a corner* (`RaceTrackScenes.EdgeCrossings`, found here). The crossing zones were made between the cube a step
  of the road (or of its edge) started in and the cube it ended in. Where that step went through a corner, the zone on each edge led
  into the cube across the corner and kept the coordinate along its edge: on the spiral, the car on its second turn went into a zone made
  for its road's edge at the corner of four cubes, arrived a whole cube west, came down on the road below and lost the race. Each zone
  now leads into the cube across its own edge, where the step meets it. The Desert island's track had it too: six of its scenes had zones
  into the cube across a corner (scene 58's east and north edges into 62, 67's into 71, and back) -- now gone, its edges covered by the
  scenes' own whole-edge zones and zones into the right cube (67 into 72). The other twelve tracks build the same.
- *The secret warp*: room 151's fireplace now leads to a fireplace made between the two pictures on the same room's south wall, the car
  coming out of it heading north into the room. Both look as every other fireplace (`docs/LBA2_PALACE_ISLAND.md`). Driven into from the
  bottom row's carpet, the car came out of the other at 4,836 units a second.
- *Checked* with the test pilot: no power-ups, won in 240.98 s, no car rescued, Twinsen awake in scene 95 after the leap.

## The Emperor's palace, eighth round: only the track red inside, the road high over the roof in the regular colours (2026-10-13, late)

The user, after a picture of the rooms from above: "We have lots of red that isn't the inside track. The red track area should be the
width of the door way, there should be no other red on the floor. It looks like we've also attempted to round the corners slightly,
let's keep the corners nice and square. The outside segment of track we have is still terrible. It needs to represent driving over the
roof top, looping between the roof turrets, more height. We also need the outside track colour to be our regular track colour with a
red/white border on the edge".

![The road over the roof from above, by height: out of the window, high over the long hall and the roof, under itself at the middle crossing, up the spiral to the take-off ramp](racetrack/palace/v9_route.png)

![High over the roof's rock between the turrets](racetrack/palace/v9_roof.png)

![Between the domed turrets](racetrack/palace/v9_turrets.png)

- *The red inside* is the race's track only, as wide as the doors with a square corner (`docs/LBA2_PALACE_ISLAND.md`, "Only the
  track red"). The route runs along its middle as before: the bottom row's door line and the west column's.
- *Higher* (`palace_design.py`; the user's way round the roof kept as it was). Before, the road came over the long hall's roof 380 over
  it and ran round the turrets 1,500 over the roof: from the car it was a road on the ground between the turrets' walls. Now it climbs
  out of the window over the sea (at most 10.7 %) to 2,300 over the long hall's roof where it comes in between its turrets
  (`OVER_HALL`), and on (at most 5.5 %) to 9,000 over the roof by the first time through the middle crossing (`HIGH`: 28,216 -- the
  turrets are 35,000 to 47,500 high). Round the south-west turret it comes down 3,000 (`DIP`), so the second time through the middle
  crossing it passes under the first; then level to the spiral, up the spiral 5,000 to 30,216 and the take-off ramp to the lip at
  31,216 as before. The road's two ways through the crossing are 3,000 apart, the spiral's turns 2,861 (the least: 2,800).
- *The regular road*: out of the round window the deck theme `standard` (dark asphalt, red and white curbs, grey rails and piers)
  instead of `palace` (the palace's pale stone, turquoise edges and rails), which was lost in the haze. The ramp up to the window keeps
  the carpet's red.
- *Checked* with the test pilot: no power-ups, won in 242.41 s; power-ups, won in 245.56 s; no car rescued, through the teleporter's
  ring at 4,836 units a second, Twinsen awake in scene 95.

## The Emperor's palace, ninth round: the user's new way over the roof -- two loops round the long hall's turrets, a weave, a jump (2026-10-13, late)

The user, with a drawing over a picture of the roof from above: "I've re-drawn the roof portion of our palace track again. The previous
version we had was too many straight lines and wasn't very interesting. This track exits the palace at the same place, loops around twice,
one loop on top of the other, a short straight into another double loop going down, then looping around and weaving in and out of the
buildings, as much as possible we should avoid straight track sections. The pink dots are a jump and the black dot is where the race ends
in a large jump where we'll then zone transition to CELEBRA2.ILE"; then "our current outside track section had very wide edges at the side
of the track, these should more closely resemble our other tracks".

![The road over the roof from above, by height: the two loops round the long hall's turrets, the weave round the four turrets, the jump (dashed, from the pink dot) and the leap off the finish (the black dot)](racetrack/palace/v10_route.png)

![Climbing round the long hall's first turret, the loop's second time round overhead](racetrack/palace/v10_loop1.png)

![Coming down round the second turret](racetrack/palace/v10_loop2.png)

![Weaving between the turrets, the road's other level beside it](racetrack/palace/v10_weave.png)

![The jump along the strip north of the north-east turret](racetrack/palace/v10_jump.png)

![Up the middle way to the finish line, the leap's lip](racetrack/palace/v10_home.png)

- *Reading the drawing.* The user's picture was the editor's 3D view from above, in perspective: the higher a thing, the further from the
  picture's middle it shows. A camera straight down was fitted to the old road in the picture (its heights known: `PALACE.JSON`, the
  plan) -- 77 % of the road's points fell on its asphalt -- and the drawn line read through it onto the island, at 22,000 high.
- *The way* (`palace_design.py`: the loops are ellipses, `HELIX1`, `HELIX2`; the rest smooth curves through points in the middle of the room
  the turrets leave, measured on the island's collision). Out of the round window round the long hall's first turret one and a half
  times, clockwise seen from above, climbing -- the second time round over the first; across to the second turret and round it the other
  way, coming down -- its west side twice, one over the other; south down the sixteen rooms' west side, round the south-west turret
  (east along the south side, north up the middle way, west through the middle way across), north by the north-west turret's west side
  (over the way down: the road there twice), east along its north side to the jump; east along the north side, south down the east side,
  west along the south side and north up the middle way -- over the way up it before -- to the finish. The long sides weave a little
  from side to side; no bend is tighter than 11 cells.
- *Heights.* The ramp up to the round window is 80 cells long now and the road goes through the window at 14,600 (it was 11,980: the
  first loop has to be over the long hall's roof a third of a turn on). The first loop climbs to 30,500, the second comes down 3,400 each
  time round to 26,339, the way round the sixteen rooms comes down to 23,400 by the north-west turret, the jump's lip is 24,400 (its top
  25,900), home climbs to 29,000 and the finish line is at the leap's lip at 30,000 (its top 31,400). Where the road passes over itself the
  two are 2,915 apart at the least.
- *What the drawing couldn't have.* Its jump went over the north-east turret's west wing, 46,000 to 56,000 high -- past what a car can
  reach (32,767): it flies along the strip north of that turret instead (44 cells, a carried jump: `arcJumps`). Its finish (the black dot)
  was by that turret: the finish line is 25 cells west of it, in the middle way, and the leap goes north off the roof over the cloud sea.
- *The finish.* A win sets game variable 79 (the car file's `win=79 1`: `PalaceTrack.StatueVar`, `RaceTrackService`'s DreamInfo
  WinVar), so the dream ends on Celebration Island with its statue: the engine draws it from CELEBRA2.ILE by that variable. The
  teleporter's pillar is back by the harbour's quay, off the race's way (`docs/LBA2_PALACE_ISLAND.md`).
- *The road's edges like the other tracks'.* The palace's road had its curbs 3.5 cells out and its deck to 5.5 to 7, a flat rail top 2 to
  3.5 cells wide either side. Now the deck is 4.5 cells either side (5.5 up the ramp and through the window), its asphalt to a cell in
  from the edge and its curbs to a quarter of a cell in (`asphaltHalf`, `curbHalf`, `raisedHalf`): the Island of the Francos', the
  Volcano's, the Emerald Moon's.
- *Two lines on cubes' edges.* The finish line first stood at z 576, a cube's edge: the car flew over it uncounted, came down off the
  leap and was rescued. The finish line and the jump's lip are a few cells off the edges now.
- *The design's checks* (`palace_design.py`): a car's box round any place across the road it can be (the road's half-width less a curb's
  width and a quarter, either side, across the way the road runs, and the car's box round that) -- a square round the road's middle as wide
  as that was too much on a slant; the jumps' flights; where the road passes over itself; the tightest bends and the longest straight.
- *Checked* with the test pilot: power-ups, won in 290.85 s; no power-ups, won in 314.60 s (stopped by three of the guards on the way);
  no car rescued, both jumps carried, game variable 79 set, Twinsen awake in scene 95. The race is long: some 5 minutes.

## A woman off the town circuit, a clean arrow, smooth kerbs, a crash, a fairer refinery (2026-10-14)

- *Off the town circuit.* The user: "Our citabau.ile track has an NPC on the track behind the lighthouse that we need to remove". She was
  the fiancée (scene 46, entity 113): her script puts her at a random one of track points 9 to 13 behind the lighthouse and walks her
  between them, and she follows Twinsen when he comes within 1,300 -- onto the road. The track kept her as one of the lighthouse's story
  people (`RaceTrackIsland` Citadel's `StoryEntities`), but no other script of the scene refers to her and the storm's end needs only
  Raph by the door and Twinsen and Zoe on the top: she is removed with the scene's other actors now (12 removed, 6 left).
- *The first arrow past the start line* (the town circuit): "looks corrupted". The road there runs 11 degrees off the nearest of the
  eight directions an arrow is drawn exactly in, and an arrow at any other heading (`ArrowFree`) had its edges step by half cells. Now
  the arrow may be turned up to 16 degrees (5, 8, 12, then 16: `ArrowSnapDegrees`) to land on one of the eight, and a straight no spot
  of which fits within 16 degrees gets none. That arrow is a clean one pointing west at (519.3, 689.3) now. No other island's arrows
  changed (each island built by the program before and after, from the same files).
- *Smooth kerbs where the lap runs close to itself.* The user, with a picture of the Desert island: "Some of the red/white lines around
  the track on desert.ile aren't smooth lines". A cell takes the smooth kerb (`KerbCells`: the kerb texture mapped by the road's own
  coordinates) only where it is plainly one road's; two kinds of cells weren't and were painted whole, red or white -- a kerb in steps:
  a jump's ramp (on the Desert island it runs on a slant: its kerbs stepped all the way up), and a cell another part of the lap came
  near at all, even with only its verge or the gap under a jump's flight. Now a ramp is plain road, and another part of the lap only
  counts where its kerb comes within reach of the cell (more than three quarters of a cell past its kerb, `KerbClear`, it doesn't).
  What is still painted cell by cell is where two kerbs really overlap: the pit lane's joins (27 cells), a hairpin's inside (11) and
  the jumps' lips (4: the lip's road is the hatched warning stripe, and the kerb texture would lay asphalt into it). The Desert island
  has 264 more smooth kerb triangles, Mosquibees Island 188, the storm track 88, the town circuit 42; Polar Island changes too.

![The Desert island's ramp before: its kerbs and the lower road's in whole cells](racetrack/desert_ramp_kerbs_before.png)

![After: cut along the slant (the two red squares at the hatched stripe's ends are the jump's lip)](racetrack/desert_ramp_kerbs_after.png)

- *A crash in a race.* The user: "I've just had race mode crash whilst testing". The engine's log (`adeline.log`'s CRASH block, the
  addresses read against the build's symbols) put it in `GiveTerrainCol` (3DEXT/MAPTOOLS.CPP), from the hero's animation step
  (`DoAnimExt`, `ReceptionObj`): on the Island of the Francos, scene 257, the car by the cube's north edge after lap 4. The ground's
  collision is read cell by cell from the island's cache with no check that the cell is in the cube -- a place a little off its edge read
  outside it, and past the cache's end, memory that isn't there. `GiveTerrainCol` and `GiveTriangle` take a place outside the cube's
  64 by 64 cells as no ground now (as off the island). Three races of three laps since, through that scene each lap: no crash.
- *The Island of the Francos' refinery: a safe way through.* The user: "Our Knartas.ile track is a little too chaotic, in places we have
  both steam and oil attacking obstacles for the player at the same time, and in lots of places at the same time, let's tweak this so
  we're always providing a safe path through so they can avoid getting hit if they drive well". Every one of the 16 gantries over the
  road blew steam from one side (5.5 cells across, three quarters of a cell past the road's middle) and dripped oil 2.1 cells to one
  side of the middle, the side changing gantry by gantry -- where the oil fell on the side the steam didn't reach, 0.4 of a cell of road
  was left; and the pipeline from the factory dripped three times across the road where it passes over it (2.5 cells apart, a slick
  taking a car 1.5 cells from its middle: under a cell left). Now (`PipeRun.OneSide`, `PipeLine.OneSide`, the plan's `oneSide`):
  - a gantry either blows steam or drips oil, never both: every other one of a steam stretch blows (9 jets, 15 before), the ones between
    drip (7 drips, 16 before);
  - the steam blows to the road's middle and no further;
  - a gantry's drip falls on the side the steam blows from that lap -- the driver's left on odd laps, his right on even ones (the
    engine's `drip=` takes the road's middle under it as two more numbers and mirrors the drip through it on even laps);
  - the pipeline drips once where it passes over the road (two places), on one half, its slick clear of the middle (`SlickClear`).

  So on each lap the steam and the gantries' oil are all on one half of the road and the other half is clear; at the pipeline, the half
  away from its drip. The test pilot, which keeps to its line, shows it: on the odd laps (steam from the left), where its line runs on
  the right it was never hit, and its only hits were where its line runs exactly down the middle (0.0 cells over); on the even lap its
  line runs on the steam's side through the second and third stretches and it was hit there, and every one of its skids on a drip was
  on an even lap. The engine's log says now how far over the middle a car was when the steam hit it, and whether the oil it skidded on
  was a pipe's drip.

## The jumps' strips across the whole ramp, missed checkpoints called out, Otringal's pier smoothed, a shorter landing, the Volcano's pit lane (2026-10-14, later)

- *A jump's strip across the whole ramp.* The user: "Let's extend the jump zone for Mosquibe.ile fully across to cover the left side. A
  car has to be almost fully off the track to jump there, but it should still work for them". The strip of scene zone that starts a
  jump's flight held the cells whose middles were within the kerbs; past the kerb the ramp has a cell of rock shoulder and its blocking
  walls (`RampShoulder`, 2.5 cells), and a car there drove off the lip into the gap. On Mosquibees Island the strip ran from 5 cells left
  of the road's middle to 4 right of it; now (`LayJump`) it holds the ramp's whole top, kerbs, shoulders and walls -- 7 cells either side
  (cells 520 to 534, the road's middle at 527). Checked with the test pilot driving the ramp 5 cells to either side of the middle: both
  flew and landed on the far ramp. Every island's ramp jumps get it (the Desert's, the storm track's, Mosquibees', the lava lake's
  three); on each, the lap's line passes through the wider strip only at the ramp itself (no other part of the lap could set it off).
- *Missed checkpoints, for testing.* The user: "add in some code that alerts me for when I miss a check point in a track complete with
  the check point number, also add this to the log file". The moment the car is past the next checkpoint without having crossed it -- a
  later one crossed first, or the car 16 cells further round the lap than it (`RACE_MISS_PAST`) -- "Missed checkpoint N" shows on the
  display for 4 seconds, and the log (`adeline.log`) has a line: `MISSED checkpoint N of M on lap L: <how> -- the car in cube (x,z) at
  (x, y, z), the checkpoint's line in cube (x,z) from (x0, z0) to (x1, z1)`. Once a checkpoint a lap; crossing the start line with one
  still missed now says "Missed checkpoint N: no lap" on the display too (it was only in the log). How far round the lap the car is
  counts the road's height (`CarAlongGuide`): round Mosquibees Island's mountain, on the ground, the lap passes over itself and the
  race's own position tracker -- which counts height only on a raised road -- put the car on the loop above, and checkpoint 6 was
  called missed before the car came to it. Checked: two laps of Mosquibees and of Otringal with none called, and a car moved past
  checkpoint 2 called at once with the right number.

![The display when a checkpoint is missed](racetrack/missed_checkpoint.png)

- *Otringal's pier, smoothed.* The user, with a green line drawn over the map: "This section of track on Otringal, the corners are a
  little tight, let's change the track so it matches the green line in this image instead to smooth them out". Out from under the
  roulette wheel the road turned south at the pier's yard, west north of the little house and south again over the sea -- three
  corners 4 cells round (the builder's 6). It is one straight now from the first corner to the last, both 12 cells round
  (`otringal_design.py`: the corner at (420.3, 553) gone, the others widened); the lap is 4 cells shorter. Two laps with the test
  pilot: 73.7 s from the standing start, then 65.5 s.

![Before: the zig-zag west of the wheel](racetrack/otringal_pier_before.png)

![After: one sweep](racetrack/otringal_pier_after.png)

- *The spaceship's landing.* The user: "The space ship that lands on Otringal before taking off again, let's reduce the time it's
  blocking the track slightly". It blocked the road while under 1,200 over its pad: the end of its touch-down, its 2.2 seconds on the
  road and the first second of its climb -- some 3.6 seconds a landing. It stands on the road 1.3 seconds now (`RACE_SHUTTLE_LANDED`):
  some 2.7 seconds.
- *Volcano Island's pit lane, easier to get into.* The user, with a picture of the minimap: "The pit lane on volcano island
  (souscelb.ile) is too tight to drive into, are we able to bring the section of track there out slightly further to the left so we can
  widen the angle coming into the pitlane". The road up the plateau's west side turned onto the north straight square, 6 cells round,
  and the deck only widened for the pit lane -- on that bend's inside -- after it: the bend's inner edge was all but a point. The bend
  is further out now, north-west (its corner 4.5 cells north and 1.5 west of where it was, 9 cells round), the road leaving it
  east-south-east, across towards the pit lane's side, and onto the straight 3.5 cells before the fence (`volcano_design.py`:
  `PIT_BEND_X`, `PIT_BEND_OUT`, `PIT_JOIN_X`); the deck widens from the bend's middle, so its inside is a broad curve straight into the
  pit lane. The start line, the grid, the fence and the waiting spots are where they were. (The widening's end had been found as the
  lap's point nearest (501, SZ) -- which the climb's second turn passes over again, higher: with the lap a few points longer it was
  taken there, and the pit lane's width ran on round the whole first turn of the climb. It is the first such point after the pit lane
  now.) Checked with the test pilot steering for the pit lane from the approach: on the old bend it ran into the bend's inside and
  stayed there; on the new one it drove into the pit lane and down it (three runs); a lap on the line, 66.95 s.

![Volcano Island's bend onto the start straight, turned as the minimap shows it: before (left) and after](racetrack/volcano_pit_bend.png)

## Checkpoints, round by round with the missed-checkpoint alert (2026-10-14, evening)

The user drove the tracks with the new alert and asked, track by track: "CITABAU.ILE check point three needs extending to the left";
"Let's move check point 3 for CITADEL.ILE so it's further round at the middle of the corner"; "CITADEL.ILE let's extend checkpoint 4
slightly further to the left"; "Desert, extend check point 7 slightly both left and right, then check point 8 extend left"; "On
Desert.ile we need to add a check point between 12 and 13, so the corner can't be skipped"; "Desert, extend check point 17 slightly
left". ("Left" is the driver's, the way the lap runs.)

- *Every line reaches further on its corner's inside* (`PlaceCheckpoints`: `CheckpointInsidePastEdge`, 6 cells past the road's edge
  where nothing stops it short, 2 on the outside as before) -- the side a car cuts a corner on. The town circuit's checkpoint 3 (a
  left-hand bend north of the shop, the square on its inside open) reaches 10.5 cells to the left of the road's middle now (6.5), and so
  does the Desert's 17 (18 now: see below) -- both were the inside. Whether a line can be placed is decided as before: no island has a
  checkpoint more or fewer for it.
- *The plan's own adjustments* (`RaceTrackPlan.CheckpointTweaks`, `CheckpointTweak`): a checkpoint picked by its number (as the race
  counts them, the builder's, before any added) or by a place near its line, moved to the lap's point nearest `at` -- reaching across
  the road there at least, however near another part of the lap is -- its ends `left` and `right` cells further out; or (`add`) a line
  of the plan's own at `at`. The storm track (`citadel_storm_design.py`): 3 at its hairpin's middle (lap point 65; the builder had
  stepped it 4 cells back, the hairpin's way out 4.8 cells from where its inside end has to be), 4 1.5 cells further left. The Desert
  island (`track_plan.json`): 7 2 cells further left (its right, the inside, 4 further already), 8 2 further left (its inside, held at
  the kerb by the hairpin's own way out, which runs along the line), and a new line at the top of the hairpin between 12 and 13 (lap
  point 1378), its inside end at the V where the hairpin's two legs' kerbs meet (9 cells) -- from one leg to the other a car crosses
  it; the Desert's checkpoints after it are numbered one more.
- `RT_CPDEBUG=1` for `buildtrack` lists each checkpoint: its lap point, how far it reaches either side of the road's middle and which
  side is the inside.
- Checked: the storm track (five laps), the town circuit and the Desert (a lap each) with every checkpoint counted in order; on the
  Desert a car moved from the hairpin's way up to its way down, past the top, was called at once: "MISSED checkpoint 13 of 23 on lap 1:
  23 cells further round the lap".

### The Island of the Francos' huts: nothing in the way along the rails

The user, with a picture of the road going in under a hut's dome: "The right side of the track on Knartas.ile in this position seems to
have something us stopping when we're hugging the right side of the track". A hut the road is driven through
(`RaceTrackDriveThrough`) is cut into walls either side and a roof, each with its own box -- and the engine stops a car at any decor's
box. The walls' boxes were to be kept out of the passage, the deck's width and `Margin` from the road's middle, but the road through a
hut was every point of the lap within 40 cells of it, and the Francos' lap passes 25 cells beside two of them: the passage was measured
from that other road's middle, and the walls' boxes stayed where they were -- hut 88's right wall's 3.9 cells from the middle, hut 84's
left wall's 3.7, with the deck 4.75 and 4.4 to 4.6 wide. Now the road through a hut is the stretch of the lap through it (`PassageReach`
cells either way from its point nearest the hut, at the height nearest the hut's floor), and the passage is as wide as the deck there
(its own half width, point by point, and `Margin`). No decor's box comes within 0.6 of a cell of a rail anywhere on the lap now. Checked
with the test pilot driving 4 cells to the right through hut 88 and to the left through hut 84: through both at speed; the same on a
build without the fix stopped dead at hut 88's wall. Otringal's tunnel (its drive-throughs) is cut as before.

## The Emperor's palace, eleventh round: the roof road higher, the jump half as long (2026-10-14, night)

The user: "The inside of the Palace track is good and should be left alone for the moment, however the outside of the palace track is
too low to the ground, let's lift the track up, so we're hugging tighter to the buildings, so we get more of a sense of driving through
the roof tops. Also the outside jump is far too long and needs to be shorter". The inside is as it was (`palace_design.py`: only the
outside's heights and the jump changed; the way the road goes is the user's drawing's, v10's).

- *How high it can go.* No higher than some 30,600 anywhere: positions are 16-bit (32,767) and the rail camera rides 2,100 over the
  road. The first loop already tops out there (30,500), and the finish's leap needs its 1,400 over the lip. The turrets' walls rise from
  the roof (19,216) to 34,000 to 38,000 (their domes further), so the road runs up their sides, not over them.
- *What holds it down.* Where the road passes over itself the decks are 2,800 apart at the least: the second loop's west side twice
  (the second time is its way out), and round the north-west turret the road passes under its own way down the west side and beside
  the second loop's south side -- that stretch stays the lowest.
- *The heights now* (v10's in brackets): the second loop in at 30,500 (30,300) and down 3,300 a turn (3,400) -- out at 26,656 (26,339);
  round the sixteen rooms to 26,000 at the middle way's west end (25,000); under the way down the west side 23,800 (23,400); then, past
  the second loop, up to the jump: its lip 26,000 (24,400); home 28,500 to 29,500 (28,000 to 29,000); the finish's lip 30,500 (30,000),
  the leap's top 31,900. The least height between two of the road's ways is 2,829.
- *The jump* along the strip north of the north-east turret is 22 cells (44): lips at x 463 and 485, its top 900 over them (1,500).
- Checked with the test pilot: won in 286.82 s, no car rescued, both jumps carried (the short one set down on its landing at 26,000),
  game variable 79 set, Twinsen awake in scene 95.

### The rooftops: sixty of them, lower, round the road (2026-10-14, night)

The user: "Let's lower the roof tops. Are we able to add in more roof tops? I know we've previously had performance issues on this
level". The four halls over the sixteen rooms and the two on the long hall stood 22,500 to 47,500 over the roof (19,216) -- the road,
at 23,800 to 30,500 and no higher, ran far down their sides. Now (`PalaceOutside`: the rooftops) Otringal's four palace halls stand
over the roof sixty times, 13 to 34 cells across, as tall as their own shape (0.8 to 1.35 times their width up: 7,000 to 12,000 over
the roof), their tops round the height of the road nearest them -- 1,500 over it and 2,000 under it in turn -- packed in an order of
their own over the sixteen rooms' roof and the long hall's, two cells apart, none within the road's half width and 2.5 cells of it
unless the road is 1,800 over its top (`RaceRoad`: the palace's track built into the program, its points out of the rooms). The
route's checks (`palace_design.py`) are all clear against them and give the same plan.

![The roof from above: the road and the sixty rooftops round it](racetrack/palace/v12_rooftops_top.png)

![From the car: the domes round the road at its height](racetrack/palace/v12_rooftops_race.png)

- *What it costs.* The test pilot over the roof, 6,000 frames: 10.3 ms a frame with the six turrets, 10.6 with sixty rooftops; the
  busiest cube 551 objects of the 1,000 a cube holds (406 with thirty); 131 bodies of the outside (each size its own body), well inside
  the island bodies' cache (1,024 bodies, 16 MB). The earlier slowness was each copy of a decor drawn once for every cube that listed
  it, fixed then (the "lag fix" of 2026-10-13): a copy is drawn once a frame now.
- Checked: won in 276.43 s, no car rescued, both jumps carried, Twinsen awake in scene 95.

## Wannies Island: through a giant frog, down the elevator tower, a shortcut over a jump (2026-10-14)

The user's drawing for `PLATFORM.ILE`: "Pink is jump, and purple is a boost pad. The massive blue circle is to be a giant frog that we
drive through. The frog repeatedly opens and closes its mouth and extends its tongue as it does so, so we have to time the jump into the
mouth. Exiting the frog we go up a slope to the top of the tower, then there's a loop coming down the tower looping around about three
times, into the start finish line and the pit lane". Asked how the pieces fit: the boost pad and its jump a shortcut beside the road
round the right ("Both: jump as a shortcut"); the road built up to the frog's jump, "which then lifts the frog higher into the air, this
will then make the slope to the top of the tower less steep"; a car meeting a shut mouth bounced back.

The plan is `tools/RaceTrackPlan/wannies_design.py` (the drawing read off and laid on the island, as the other tracks'), the island
`RaceTrackIsland.Wannies` (outside scenes 96-99), the line-up the Wannie miner in his undergas bulldozer (the one to beat), a Wannie in a
mine cart and the old Wannie on her firefly tart.

- *The lap* (812 cells, all raised road): from the pier south of the Wannies' elevator round the east island -- north of its buildings,
  out round the mine's machinery -- west along its south side, the west island's south loop, and north-west up to the frog, climbing all
  the way (2,600 to 19,500, 10.7 %); the jump into the frog's mouth at 20,400; through the frog and out of its back, up over the lava to
  the elevator tower's top (25,000, 13.8 %) and three times round it down a helix (11.5 %) onto the pier.
- *Scene 98's own script.* The hero's life script there plays the elevator's cutscene while game variable 107 is 0 (and puts Twinsen at
  its point 7, on foot): the island holds 107 at 1 (`HeldVars`, the race file's `vargame=`).
- *A checkpoint just past the start line* -- here the corner after it -- was never taken: the car crosses it in the frame it crosses the
  start line, and the lap begins after the checkpoints are counted. None is placed within 6 cells after the line now (no other track had
  one there).

![Down the tower's helix](racetrack/wannies/tower_helix.png)

### The frog

![The frog's tongue shooting out at the car coming up the ramp](racetrack/wannies/frog_tongue.png)

- *The body* (`RaceTrackFrog`, one body of 417 points, 365 polygons and 9 spheres): a throat the road's width through it, mouth to back
  (18 cells, straight along the drawing's line); its skin ring by ring round it -- a wide head, its back sloping down to the rump round
  the way out; bulging gold eyes, nostrils, front legs to its hands and folded hind legs to webbed feet, on a lily pad on a stalk down to
  the ground (14,000 under it). Bone 1 is the lower jaw, hinged at the back of the mouth: its top the mouth's floor -- the road -- while
  the mouth is open, turned up it fills the mouth like a drawbridge (a pale throat sac and walls under it). Bones 2-4 are the tongue,
  folded back along the floor; turned over the lip it shoots out of the mouth at the cars coming and curls down towards the ramp.
- *Drawn by the race-track mode*, not as a scene's actor: posed by the mode's own clock every frame -- the jaw and the tongue turned
  straight in the body's frame (`CurrentFrame`), no animation of the game's -- and drawn into the island's background after the ground
  and the decor, its depth written (`RaceMod_DrawBackdrop`, from `AffGrilleExt`). So the cars, drawn after it and tested against the
  depth, are hidden by it where they should be and seen in its throat; an actor would have been sorted against the car by its box, and
  the car inside the frog's box drawn over it or under it whole. Its polygons are cut close to the camera (the body's close flag): the
  camera is in its throat.
- *The mouth's cycle*, 4.4 s: opening 0.32 s, open 2.5 s (the tongue out and back in its first 0.8 s), shutting 0.32 s, shut. A car is
  let in until 0.6 s before it shuts -- the time to get past the jaw -- and the jaw twitches three times then: too late.
- *A shut mouth* (`frog=`, `FrogBounce`): the carried jump into it (its landing is the mouth) is broken off at the jaw's lip, and the car
  thrown back over the gap in an arc and put down on the road 8 cells before the ramp's foot, facing on, stopped -- 1.3 s.
- *The opponents* coming up to it are slowed, from 40 cells out, to come to it as it opens (`FrogCap`), their arrival kept till they are in.
- *The road through it* has no deck of its own (`TrackRoad.Hidden`: no pieces, no piers; the mode's floor as anywhere), narrower up the
  ramp and through the frog (3.5 cells either side); the camera kept low in its throat (a tunnel with no pieces).

![In its throat](racetrack/wannies/frog_throat.png)

![The mouth shut: the car thrown back](racetrack/wannies/frog_shut.png)

![The body, open with its tongue out (the frog view of `frogbody`)](racetrack/wannies/frog_body.png)

### The shortcut

Where the lap turns west at the east island's south-west corner, a branch of the raised road goes straight on north-west, over the boost
pad, bends west up a ramp (9 cells, 800 up) and jumps west over the lap's dip round the channel (11.5 cells, 350 up: no jump lands lower
than it took off), landing on the lap at its next corner and on along it. As long as the lap's stretch it skips (50 cells); what it gains
is the boost pad's speed, carried over the jump.

- *A branch of the raised road* (the plan's `branches`, `BranchRun`; the engine's `raised_lap=`, `raised_branch=`): its points after the
  lap's in the engine's raised road, behind a point with no width, its first on the lap's road where it leaves, its last where it comes
  back. The engine finds its floor and keeps the car on it as on the lap (the nearest road the car is on); its ends are not open ends
  (the lap's road is there); the lap's loop is the lap's own points; the camera that rides the road behind the car goes on along the lap
  past a branch's ends, and the distances between the road's points that the cameras and the floor's levels go by run through them.
- *Its decks*: where it lies on the lap's road (its first cells, its landing) it has none of its own; 40 over the lap's deck where the
  two overlap; neither road has a rail across the other's deck there (`OpenRails`: a deck piece's rail down to its curb), so the two
  part at a V. Its piers stand clear of the lap's road.
- No checkpoint on the stretch of the lap it goes round.
- Checked with the test pilot taken off its line at the fork: on the boost pad (120 km/h), carried over the jump at 17,066 units/s,
  down on the lap.

![The fork: the shortcut straight on, its boost pad](racetrack/wannies/shortcut_fork.png)

![Over the jump](racetrack/wannies/shortcut_jump.png)

### Second round: two turns down the tower, a straight start with its own pit lane, a shortcut that is one (2026-10-15)

The user: "Let's reduce the downward loops around the elevator by one so it's a slightly steeper spiral downwards. Let's also refine the
start/finish line ... The main path now goes straight on, with a thinner pitlane to the side"; then: "change the jump section so it acts
as a short cut. Let's add some boost pads around the other loop of track that the jump skips so if driven well it's the same as if not
quicker than taking the jump"; and: "Our current boost pad isn't fast enough so we'll need to increase the speed boost it offers".

- *Two turns down the tower* (three before): 257 cells from 25,000 to 7,000, 13.7 % (11.5 % before). The lap is 685 cells (812).
- *The start straight* goes straight on south from the helix (x 558.5) over the lava east of the pier -- no more swing west onto the
  pier and back -- the start line on it at z 497 under the gantry, the grid behind it. *The pit lane* is a branch of the raised road of
  its own, half as wide (2.25 cells either side): off the straight's west side just out of the helix, down the pier's east side (x 551)
  past the start line, the opponents' three waiting places along it, back onto the straight before it bends onto the east island. The
  start line reaches across it too (a car in the pits crosses it), and no checkpoint is on the stretch it runs beside -- that stretch
  runs round the lap's seam now, which the builder's checkpoint rule for a branch didn't allow for before.
- *The shortcut*: off the lap where it did before, over its boost pad, then up a ramp north-north-west to a take-off at (510, 533) -- the
  user's pink -- and a leap that hangs in the air 2.4 s, curving west over the S's turn, down onto the lap's road past the turn (the
  other pink): the loop round the west island's south is left out (82 cells instead of 196). For the leap to be up onto the lap (no
  jump lands lower), the lap climbs only gently round that loop (4.5 %) and 13.5 % elsewhere, the start straight at 6,500 and the
  helix's foot at 7,000 (2,600 and 3,300 before): it takes off at 15,390 and comes down at 18,187. The leap's time in the air is its own
  (the engine's arcjump='s sixth number: the car carried over the gap at the speed that takes), the camera beside it.
- *Round the loop it leaves out*: three boost pads (just past the fork, on the way west past the corner, on the S's lower leg), stronger
  than the shortcut's own: 2.1 times the car's top speed. With the user's car (110 km/h) the loop, boosted all round, takes about 3.1 s
  from the fork to where the leap comes down, the shortcut about 3.4 s; with the setup's default car (34 km/h) the shortcut is still
  about a second and a half the quicker.
- *A boost pad's speed* is now at least 1.7 times the car's own top speed (RACEMOD.CPP `PanelSpeed`), 120 km/h at the least as before
  (for a car of the default setup nothing changes); a pad with a lift of its own (boostpanel='s eighth number, hundredths) has its least
  speed as much higher -- the loop's, 148 km/h for the default car.
- Checked with the test pilot: a lap of 70.5 s (one bounce at the frog) over the loop's pads; taken off its line at the fork, on the
  shortcut's pad, the leap carried (two scene changes in the air), down on the S. The other 13 tracks build the same, byte for byte.

![The start: the gantry over the straight, the pit lane beside it](racetrack/wannies/start_pitlane.png)

### Third round: a longer, wider pit lane, the frog's mouth open longer, the car the player's up to the lip (2026-10-15, later)

The user: "The pit lane is short, narrow, and has a strut through it from the road above. Let's lengthen the pitlane, remove the strut and
make it slightly wider. The frog isn't visible in the editor, is there a reason for this? Let's make the frogs mouth open for slightly
longer with the tongue remaining out longer too. The game takes control of the car too early, if we need to script the car let's wait until
the last second to guide it through the frog".

- *The pit lane* is 61 cells (41): off the helix's last straight down the tower's east side, its straight down the pier from z 484 to
  515, back onto the lap past its bend onto the east island; 2.75 cells either side of its middle (2.25). *The strut* was one of the
  helix's piers: its lowest turn's south side passes high over the pit lane, and a pier stood clear of the lap but on the pit lane. A
  raised road's piers now stand clear of every other road of it -- the lap's of its branches', a branch's of the lap's and the other
  branches' -- where those have a deck.
- *The frog's mouth* opens for 3.1 s (2.5) and its tongue is held out 0.9 s (0.16); the time it stays shut is as it was, its cycle 5 s.
- *The car is the player's up the ramp*: the carried jumps -- into the frog and the shortcut's leap -- take the car only from half a cell
  before their lip (from the ramp's foot before, 10 and 9 cells back). A car the frog throws back is put down 18 cells before the lip,
  where it was.
- *The frog in the editor*: it was drawn only by the race-track mode, in the game; the editor shows the island's decor. The build now puts
  a still copy of it among the island's decor too (its mouth open), hidden by game variable 212 -- the game's own way of hiding a decor --
  which the race-track mode holds set on Wannies Island while it races, so in the game only the moving frog is seen.

![The frog in the editor, among the island's decor](racetrack/wannies/editor_frog.png)

### Fourth round: the road close over the ground, wide pit-lane mouths, a blue frog (2026-10-15, later still)

The user, with a picture of the start straight and its pit lane (the two ends of the pit lane filled in green): "Let's widen up the pitlane
entry and exit as shown. Also where our track splits we've got red/white line in the middle that we can remove, and also let's remove the
boost panel leading up to the jump on the right path. Let's make all the track closer to the ground which means our inclines will be
steeper exiting the frog"; then "We also need to change the colour of our frog to blue instead of green".

- *Close over the ground* (wannies_design.py, the heights): the lap climbed at 13.5 % from the start straight's 6,500 to the frog's 20,400,
  4,000 to 14,000 over the islands. Now each point has a floor -- 550 over the highest ground across the road, 1,400 over the lava, and
  450 over the top of any decor box the builder would count in the road's space (a cell of the box with its middle within the deck's half
  + 0.75: the builder takes such a decor away unless the deck is 320 over it) -- and the road is the lowest one over its floors that climbs
  and falls at 17 % at the most (as Volcano Island's and Otringal's), its crests and dips rounded over 6 cells. The floors are tested
  against the decors' boxes as the builder tests them, not their shapes: the mine's machinery on the east island leans, its shape 6,534
  high where its box is 8,380, and the first build took the whole machine away.
- *The frog* comes down to 13,564 (20,400): its lily pad, 2,650 under its floor and 13 cells round, clears the Wannies' house on the hill
  under it (bodies 12 and 13, 5,613 to 10,637) by 300. Lower, the house would stand through the pad; it stays. The road before the frog
  comes up to its ramp at 17 %; from the frog to the helix's top (25,000, over the tower's) it climbs at 34 % on average, 37 % at the
  steepest (14 % before), eased in and out. The helix comes down to 4,920 (7,000), 15 % (14 %).
- *The start straight* is at 4,630 (6,500), level to the start line; then the road climbs at 17 % to clear the east island's houses (body
  83, 5,802, beside the road).
- *The shortcut* keeps its leap up onto the lap: its lip 8,122 over the east island's plateau, the lap 11,509 where it comes down. Its boost
  pad is gone; the leap is carried in its own time (2.4 s) whatever the car's speed, so it still lands. Where it leaves the lap the lap is
  raised to what the shortcut needs (the lap goes down off the plateau, the shortcut stays over it: before that, a step where they parted).
- *The pit lane's mouths* (the user's green): the pit lane is laid out in the lap's own frame, its outer edge and inner edge set across
  the lap at each of the lap's points. Its outer edge is a straight taper from the lap's own edge out to the pit lane's straight, over
  12.5 cells at the entry (from the helix's last straight, z 473.5 to 486) and 14 at the exit (from z 511 round the lap); its inner edge
  lies 1.5 cells inside the lap's deck until the two part, so the mouth is one wide deck with the lap's, 3.5 cells either side at its
  widest. The entry's taper clears the corner of the elevator's landing platform (body 20, kept) by the builder's half-cell rounding. The
  pier's east post (body 33) stood in the pit lane's way once it was low; the island's record drops it (`DropDecors`).
- *No line where the road splits*: where a branch's deck lies on the lap's (or the lap's on a branch's), the side's rail was flattened but
  its curb and rail top still drawn -- a red and white line across the one deck. An open side is asphalt now, its curb, rail and edge
  (RaceTrackRaisedBody.Tile); where the two decks part, the two rails meet in a V.
- *A blue frog*: its skin the palette's blue ramp (192), its spots, hands and feet the slate blue (176); the lily pad and its stalk stay
  green (RaceTrackFrog's colours).

The test pilot: a lap in 75.5 s (thrown back twice by the frog, untimed as ever); round the pit lane, the lap counted in 70.1 s.

![The pit lane's entry: one wide deck off the helix's last straight](racetrack/wannies/pit_entry_wide.png)
![The pit lane's exit](racetrack/wannies/pit_exit_wide.png)
![The blue frog](racetrack/wannies/frog_blue.png)
![The climb from the frog to the tower's top](racetrack/wannies/climb_to_tower.png)

### Fifth round: a shorter leap, no car on its nose (2026-10-15, later)

The user: "Our jump on platform.ile is far too long, we need to change it so the car lands as soon as it can"; then "After using the boost
panels on platform.ile and driving into the side wall it tips the car up on its nose, we should fix this".

- *The shortcut's leap* came down on the S west past its turn (the user's other pink): 38 cells and 2.4 s in the air, climbing 3,400. It
  now lands on the lap's first point past the loop it skips that the leap can come down on (`wannies_design.py` `leap_landing`): from where
  the lap comes back east under the take-off, the first whose way the leap turns onto by 40 degrees at the most, with room before it to
  curve onto it. With the ramp aimed at the S's turn, that is just past the turn: 24 cells, curving 17 degrees, 1.5 s in the air (the arc
  and the time scaled to its length). The lap itself is unchanged. The test pilot took it: carried from the lip, down on the S 1.5 s later.
- *On its nose*: the car's pitch and roll are measured from the floor 400 in front of and behind its middle and 400 to either side
  (BUGGY.CPP). Driven into the rail -- at a boost panel's speed, further over it -- a point past the rail is over the ground far below the
  deck, and the car stood on its nose. In a race a point whose floor is more than 700 off the car's own now counts as level with it for
  the pitch, 250 for the roll (`TiltFloorY`): no road climbs or falls 700 over 400 (the steepest, the Elevator Platform's drop, 500), and
  none is banked 250 across (the steepest bank, 0.40: 160). The car driving as a character car is pitched by the way it climbs: that is
  measured over the whole way it goes now, not only its share along its heading (pushed along a rail it goes nearly sideways), and
  capped (`DrawDriveAs`).

![The shortcut's leap, down on the S just past its turn](racetrack/wannies/leap_short.png)

### Sixth round: the pit lane's mouths open both ways (2026-10-16)

The user: "the right side of the jump doesn't work. Entrance of the pitlane can be driven into but not out of. Exit of pitlane partial
barrier in places also".

- *The pit lane's mouths*: the rail holds a car RAISED_INSET (640) inside a deck's edges -- the deck of the road nearest it (`RaisedAt`:
  of a level, the nearest middle). Where the pit lane's deck leaves the lap's, tapering to 0.83 cells across, a car on the lap near the
  mouth was put over into it (the mouth's middle the nearer) and once in held there: between the lap's 640-in edge and the mouth's lay a
  strip neither deck let it into. The same strip at the exit was the barrier. The rail now lets a car be anywhere a circle of 640 round it
  is on one deck or another of that level (`RaisedHolds`, `OnSomeDeck`): the decks taken together. On the release engine the pilot was
  held 4.5 cells off the lap's middle at the exit for 100 ticks and couldn't get back onto the lap from the entrance; now both ways, at
  once.
- *The jump's right side*: not found. The shortcut's leap was taken from both rails at the pilot's speed and at 110 km/h, on the release
  engine and this one, and so was the frog's (from 1.3 cells either side); every time the car flew and came down on the road. (The fork
  into the shortcut is one of the places two decks overlap: the rail's change there applies.)

### The shortcut's jump on its right side (2026-10-17)

The user, with a picture of the car past the lip and stuck there: "Jump still failing on right hand side ... Seems to be intermittent".
Their own game, read live: the car 890 units past the shortcut's lip, 1,500 to the right of its middle, at the deck's height, speed 0;
no carried jump.

- *Why*: the shortcut's last straight crosses from cube (8,8) into cube (7,8) at x 512 cells. The game keeps a car half a cell inside
  its cube, and a cube change puts it half a cell inside the next: a cell's jump with no move between. On the road's right side the
  take-off -- the ramp's foot and lip, 256 units apart -- lies a few hundred units from that edge, so the change could set the car down
  between the foot and the lip, and its next move took it past the lip. `RaceArc` takes a car as it crosses the foot, or where a move
  ends on the ramp: neither saw it. Driven off the lip, it was held at the deck's height (the raised road's floor) and stopped by the
  rail over the gap -- in a race the stuck-car reset brought it back after 3 s of driving on; reversing, it stayed. On the left side the
  change comes cells before the ramp.
- *Repro*: Twinsen put on the shortcut's straight (`teleport` in scene 99) 9-15 cells before the lip, 950-1,152 to the right, heading
  along it or up to 8 degrees right, flat out (15,644 units/s): the release engine missed the jump in 38 of 72 runs, at 16 and 20 ms
  frames alike. At the pilot's slower 50-70 km/h it was always taken, the cube change dropping it between foot and lip.
- *Fix*: `RaceArc` also takes a car past the lip, over the gap, still at the lip's height and within its width, going forwards, and carries
  it on from where it is ("carried jump 2: on, past its lip already (289 along)"). Now 144 of 144, every one landing on the lap. No other
  road crosses any track's jump gaps at its lip's height, and Otringal's wheel lane (a lip 0 wide) is left as it was.

## The Emperor's palace, twelfth round: the outside dropped, an upstairs, the race through both floors (2026-10-15)

The user: "The outside of the palace track just isn't working, and we've tried a few revisions, let's keep a copy of it so we can rebuild
it in the future if we need to, but for now drop it totally. Let's add an upstairs to the inside of the palace. Let's keep the same basic
colour theming, but maybe add a couple of bedrooms, a throne room, and any other rooms you can think of that would fit with a palace
theme."

- *The outside, kept*: the git tag `palace-outside-v12` (commit `60c8e61`, the last with it) and `docs/racetrack/palace_outside_archive`:
  the design script and the plan as they were, and how to build them again. The island's outside code stays, off (`PalaceIsland.Build`'s
  `outside`).
- *The upstairs* is the island's (`docs/LBA2_PALACE_ISLAND.md`, "No outside, and an upstairs"): ten rooms over the sixteen, two stairwells
  open to the roof, the long hall two storeys high, the hill's top over it all.
- *The race* (`tools/RaceTrackPlan/palace_design.py`, thirteenth version), inside only, 1,230 cells:
  1. From the grid in front of the palace's doors in room 154, west along the bottom row and north into room 155, a stairwell.
  2. Up the ramp round its column: 1.5 turns, 9 cells round, at 21 % (23 % at the steepest). It is the race-track mode's raised road, the
     rooms' red carpet edged in gold, the car held on its rails.
  3. Upstairs, out south into the library, then east through the music room, the princess's bedroom and the Emperor's bedchamber.
  4. North through his dressing room, then west into the throne room past the throne on its dais. North up the room and west into the
     other stairwell, room 159's.
  5. Down its ramp: 1.75 turns at 18 % (20 % at the steepest).
  6. Out north through room 163 into the long hall and up its ramp to the round window. The finish line is in the window, and the leap
     out of it is carried; a win's teleporter takes the car in mid-air, as in the fourth round.
  - The floors are open areas, the cars driving where they like: the ground floor's and the long hall's at 10,640, the upstairs' at
    19,600. Upstairs they leave out the stairwells' open middles, where a car on a ramp under the floor would be taken for one on it.
  - Each ramp's turns pass at least 4,508 (up) and 3,732 (down) over the one under them.
- *The opponents' other ways* (`openRoutes`):
  - One goes north through room 158 and west through 157 back to the bottom row. Upstairs it goes from the dressing room north through
    the treasury into the throne room.
  - The other goes on from the treasury through the armoury, west into the banquet hall, and south through its door into the throne room.
- *Arrows on the floor*: the ground floor's other rooms point south, to the bottom row (159, 163 and the long hall are reached only down
  the stairs). Upstairs they run along the race's way and the others'.
- *Guards*:
  - Ground floor: soldiers in the rooms on the way and beside it, and up the long hall; the pigheads in 152 and 163.
  - Upstairs: soldiers in the music room, the dressing room and the throne room.
- The checks: every way clear of the furniture's and walls' boxes; the leap's flight clear.
- *The race's lines*: the intro is now "through the rooms, up the stairs, past the throne and back down, out of the round window and into
  his teleporter", in all six languages.
- The test pilot (start scene 290): won in 111.11 s, first of four, no rescues. It was up on the upstairs' floor from 32 s to 79 s, down
  the second ramp by 92 s, and woke on Celebration Island (scene 95).

![The up-stairwell: the ramp round its column](racetrack/palace/upstairs_up_stairwell.png)
![The library](racetrack/palace/upstairs_library.png)
![The princess's bedroom](racetrack/palace/upstairs_bedroom.png)
![The throne room](racetrack/palace/upstairs_throne_room.png)
![The down-stairwell](racetrack/palace/upstairs_down_stairwell.png)
![The long hall, two storeys high](racetrack/palace/upstairs_long_hall_two_storeys.png)
![The upstairs from above](racetrack/palace/upstairs_top.png)

### The down-stairwell's ramp down to the floor (2026-10-15, later)

The user: "Inside the palace the spiral down from upstairs to downstairs doesn't connect to the ground it just stops forcing cars to just
drop off, we should fix this, so it's a smooth drive down".

- The plan's bare stretch (no road: the ground floor's rooms) begins at the ramp's foot, and the builder finds a plan point's place on the
  road as the nearest road point (`PlanPoint`) -- but the ramp's turn above its foot passes within a fifth of a cell of it, and the bare
  stretch began up there: no road under the ramp's last 1.25 turns, and the car fell 5,500 to the floor halfway down. A road point is now
  given the plan's height at its place along the plan, and where the nearest point is another level's (1,500 off), the nearest at the plan
  point's own height is taken. Roads that don't pass over themselves are unchanged.
- Out of the stairwell the line ran north along the cube edge (x 384, the ramp's foot 1.5 cells past it) and the car changed cubes back
  and forth every half second; it now goes straight over to the round window's line (3 cells inside the cube west of the edge) just past
  the foot.
- The test pilot: down the ramp from 19,600 to the floor without a drop, three cube changes on to the long hall; won in 123.99 s, no
  rescues.

## The Emperor's palace, thirteenth round: Sup armour, Zeelich portraits, a cleaner floor and minimap, a dungeon (2026-10-16)

The user: "Improve suits of armour, perhaps we should base these on the Sup actor. Clean up mini-map so it nolonger shows outside. Replace
portraits with characters from Zeelich. Clean up any arrows on the floor that aren't on the track, such as the one pointing at a fire place.
Make sure all remaining arrows are in the centre of the track"; then: "Add a basement level ... This is a secret easter egg area that should
only be accessible via a fire place, let's use the one by the down spiral". (Its brief also asked for things of an adult nature; the
dungeon is a plain one -- a jail's -- and nothing more.)

- *Suits of armour* (the armoury upstairs): each is one of the Emperor's Sup soldiers -- the game's own body (BODY.HQR 261, the palace's
  guards') as it stands, turned to look out, 1,260 tall, its polygons and spheres in steel shaded by their colours' place in their ramps
  -- on a wooden plinth, a spear beside it (`PalaceFurniture.Armour`; without the body, the boxes it was).
- *Pictures*: the people of Zeelich -- a Sup guard of the palace, an old Sup lady, a Sup priest, the old Franco, a Franco policeman, a
  Wannie, a Mosquibee, and the Queen of the Mosquibees by the warp's fireplaces as before (`PalacePortraits.Sitters`).
- *Arrows on the floor*: only those on the race's own way, each moved onto its middle and turned the way it runs there
  (`palace_design.py`): 20 of the 38. Gone: the other rooms' arrows south and the detours' (the opponents' ways), and room 151's that
  pointed west past the turn north, at its fireplace.
- *The minimap*: the palace's sprint goes on out of the round window in a leap over the sea, and the minimap held all of it, the palace a
  speck. Indoors (a track with open areas) the minimap now shows the racing line only as far as a sprint's finish line, on a dark panel
  over the island's picture (its roof), the rooms' floors on it (`MainWindow.RaceMinimap.cs`).
- *The dungeon*: under the stairwell 159 and room 163 north of it (cells x 361..397, z 533..621), its floor at 6,800 -- 3,840 under the
  ground floor's -- in dark stone: flagstones, courses of stone, a sooty ceiling (the ground floor's floor's underside). The ground under it
  is brought down under its floor (the rooms' ground is just under the ground floor's floor, and a car on the dungeon's would have been
  stood on top of it).
  - *The way in*: room 159's fireplace, beside the down-spiral, is hollowed and a teleporter as room 151's (`PalaceIsland` `Hollow`):
    driven into, the car comes out at the dungeon's south end, looking north. *The way out*: a fireplace in its north wall; a car that
    drives up to it comes out on the race's way at the down-spiral's foot, 12 cells on north, looking north.
  - *In it*: five iron cages along its walls (their sides boxes, open tops of bars), two prisoners' cots, two racks of whips, two pairs of
    shackles, eight torches, barrels and a chest (`PalaceFurniture.Up` Cage, WhipRack, Shackles, Torch, Cot, Barrel). Its people are the
    race's guards: two jailers -- Sup soldiers, sword out, stepping out up to 6 cells at a car -- and two pighead prisoners, leashed in
    their cages, throwing things through the bars. Its floor is an open area of its own (an area is the one whose floor the car is near).
  - It is off the race's way: the sprint's line, its time and its winner are unchanged (won in 123.99 s).

![A suit of armour in the shape of a Sup soldier](racetrack/palace/armoury_sup_armour.png)
![The people of Zeelich on the walls](racetrack/palace/portraits_zeelich.png)
![The dungeon from above, its ceiling off](racetrack/palace/dungeon_from_above.png)
![Its cages](racetrack/palace/dungeon_cages.png)
![A jailer coming at the car](racetrack/palace/dungeon_jailer.png)
![The way out](racetrack/palace/dungeon_way_out.png)

### The dungeon, reached (2026-10-16, later)

The user: "we can't currently access the underground dungeon area as the game detects us as off the track and resets us". The race's
fall rescue (RACEMOD.CPP `FallenBelow`) puts back on the road a car that has stood 1.5 s well under the line with no line at its height
near it -- and in the dungeon the upstairs lap runs 9,355 over the car, 1,633 aside: `fallen off the road to (194048,6801,308858), 9355
below the lap 1633 away`, a second after the fireplace's teleporter. A car on the floor of an area of the race's own off the lap -- one the
line never runs through at its floor's height (`SideArea`), as the dungeon -- is now where it was let go, not fallen. The rooms the lap runs
through keep the rescue (a car fallen from upstairs into a room the closed doors cut off). Checked mid-race: down through 159's
fireplace, past the jailers, out of the north fireplace back into 163, no reset.

### The dungeon's captives (2026-10-17)

The user: "The palace dungeon is a bit tame, let's add some female rabbibunny rope bunnies suspended from the ceiling". Four female
rabbibunnies hang in ropes from the dungeon's ceiling, in the gaps between the cages (`PalaceFurniture.Captive`, `Up.Hanging` and
`Up.Suspended`):

- *Who*: the game's own bodies, in their own clothes and colours -- BODY.HQR 62 (Mrs. Brune, the Inter-Islands' rabbibunny: green top,
  grey skirt) and 68 (the rabbibunny in blue and white), each hung both ways (`PalaceFurniture.Rabbibunnies`). Their polygons' colours are
  the island palette's at their index, a lit one's seven steps up its ramp, a textured one's (68's striped top) its picture's at its middle.
- *Posed*: each bone turned about its own pivot -- the arms found from the bones and each hand put by a two-bone reach (the race cars'
  drivers' `CarDriver`), the legs (the bone turning highest in her lower half on each side) turned at the hip till the feet are
  together, the feet pointed, the head turned. 1.5 times the game's size, as big across as up in the world (a model's units are 4 times
  the world's across but only 2.5 times up).
- *Hanging* (two): by her wrists, her arms up and apart, a rope from each wrist up to a wooden bar hung under the ceiling; tied under the
  knees and at the ankles; her toes some 500 over the floor, her head bowed. Looking south, at a car coming up the dungeon.
- *Suspended* (two): lying face down across her gap, her head towards the way, her arms bound to her sides, held up by ropes round her
  chest, her waist, her shins and her ankles from a beam along over her; her head hangs (raised, the gap under the jaw showed from the
  side: the game's bodies have no throat). Her underside some 2,000 over the floor.
- *The ropes*: each a band hugging where a plane across her cuts those bones (the eight-sided outline whose sides touch the cut), the
  ropes up to the bar and the beam and their hangers boxes, an iron plate at the ceiling.
- *Where*: in the gaps between the cages, reaching no further out from the walls than the cages' fronts (2.35 of the palace's cells: the
  way down the middle), drawn only -- no boxes: the race, its line, its jailers' and the cars' collision are as they were (the palace's
  992 solid boxes identical, every other race-track file byte-identical; the pilot, power-ups off, on the build before and this one
  started together: 124.63 s, won, no rescues, both).

![Two of them from the car](racetrack/palace/dungeon_captives.png)
![All four, close](racetrack/palace/dungeon_captives_close.png)

### The dungeon made medieval, its torturers (2026-10-17, later)

The user: "Let's make our palace basement more like a medieval dungeon. Let's add some grobo torturers with bull whips etc." Its layout is
as it was -- the way up its middle, the fireplaces in and out, the cages, the captives, the jailers and the prisoners -- and round them:

- *Its stone* (`PalaceDungeon`): its walls, floor and ceiling textured, a page of the island's object textures painted at the build in the
  island's palette -- the walls courses of rough grey ashlar in dark mortar (each block's top edge lit, its foot shaded, chipped; damp and
  green at the foot, sooty under the ceiling; as light as the rooms' walls are shaded, east and west lighter than north and south), the floor
  big uneven flagstones in four rows, worn and cracked here and there (two of them, a cell each, turned about cell by cell), the ceiling dark
  timber planks. One perspective-correct textured polygon a face (TEXTUREZ, the pictures' kind), the stone running on from cell to cell: as
  many polygons as the flat faces they replace.
- *Its vault*: five stone arches across it on pillars against both walls, where both walls are free between the cages (`DungeonArches`),
  springing at 2,300 and rising to just under the ceiling -- over a car's camera all the way across; a sconce on each pillar and either side
  of the ways in and out (an iron cup, a torch, its flame; they were torches on the walls); chains with hooks from the arches by the walls.
- *Its furniture* (`PalaceFurniture.Up`), in the gaps by the walls, clear of the cages, the captives and the way down the middle: the rack
  by the west wall (a wooden table, a roller at each end with spokes, ropes along it -- nobody on it), an iron maiden in the north-east
  corner (shut, an iron face on it), a pillory on the east side by the south end, a whipping post in the north-west corner, a cartwheel on
  the north wall beside the way out, three braziers of glowing coals with branding irons in them, straw in the cages. The solid ones are
  in the collision, as the cots are.
- *Its torturers* (`PalaceTorturers`, `RaceTrackGuards.Torturer`): four Grobos -- the game's grey Grobo near its end (BODY.HQR 459, entity
  336: black clothes, a red sash) in an executioner's black hood (a cowl, its brim lined in red, the ears out at its sides), a bull whip in
  his hand where his spiked club was, 1.8 times the game's size; one by the whipping post and the wheel, one by the iron maiden, one by the
  rack, one by the pillory, each looking out at the way.
  - *The whip* is part of his body: the club's polygons gone and its points the handle's, the lash seven bones of its own after the hand,
    each a length of a tapering leather tube, so an animation swings it. The body's own polygons, normals and lights are kept byte for
    byte, the new ones after them (a flat-lit polygon's light is found past all the points: theirs move on by the points added).
  - *His animations* (the entity's 640 and 641): standing -- the game's own standing, the whip hand by his side, the lash from it to the
    floor and along it in front of him, stirring -- and the crack: the arm up and back, the lash thrown out behind him; over the top, the
    lash following in a loop; down and forward, the lash straight out in front and its end flicking down; the lash on the floor; standing
    again (1.37 s). Each pose is where the arm's and the lash's bones point, each bone's turn from its parent's frame worked out as the
    engine turns bones (M(Alpha) M(Gamma) M(Beta)).
  - *In the race* (RACEMOD.CPP `guard=` kind 3): he stays at his post and cracks his whip every 1.6 to 4.8 s, every 0.5 to 1.5 s with a car
    within 14,000 on his floor, turned to it (no further round than 56 degrees from the way his post looks: the whip out at the way). He
    strikes nothing and nothing runs into him: no car is stopped, slowed or put back. His own random numbers: the race -- the jailers',
    the prisoners' and the opponents' doings -- draws the same ones as before. The log says `torturer N cracks his whip at X's car`.
  - His body and animations are made at the end of a build (`RaceTrackService.Build`), after every track's: a build of several tracks
    leaves every other track's bodies and animations where they were.
- It is off the race's way: the sprint is as it was (the pilot, power-ups off: 122.52 s, won, no rescues -- the build before this one the
  same to the hundredth). Down in the dungeon a car drives up it and out of its north fireplace as before (four drives, no rescues).

![The dungeon from the car: flagstones, ashlar, the arches, a torturer by the pillory](racetrack/palace/dungeon_medieval.png)
![The rack's torturer, his whip across the floor; a brazier, the way to the north end](racetrack/palace/dungeon_medieval_rack.png)
![A torturer's crack, 120 ms apart](racetrack/palace/dungeon_torturer_crack.png)
![The dungeon from above, its ceiling off](racetrack/palace/dungeon_medieval_above.png)

## Island CX: round the island under its cannons, down through a door, twice round the halls under the gas (2026-10-15)

The user: "Create a track for our CXTower island. The basic concept for this track is a loop around the tower, the track enters the
underground section through a slope down through one of the doors, which means we'll need to remove the metal grate that's currently
there, loops around the underground section a few times then back out a door on the other side. Don't forget to add a pit lane. Let's
have the cannons that normally fire at Twinsen outside randomly firing at any outside car." Then: "In the underground section there's
some gas monsters that attack the player let's build on this theme for the underground section."

The island is `CXTOWER.ILE`: Island CX as it is, with the control tower's lower level under its gas (`docs/LBA2_CONTROL_TOWER_ISLAND.md`).
The track is `RaceTrackIsland.CxTower`. It is raced as island 9 drawn from that file (`island_file=9 cxtower`), in scene 300, a copy of
Island CX's outside scene 110. The build makes the island afresh (`ControlTower/CxTowerTrack.Install`, `ControlTowerIsland.RaceRoom`):

- *One ring of a hall round the control room.* The walls between the two halls and the empty corners under the island are taken down. The
  corners are floored and walled like the halls. What stood on the halls' floors is gone (the doors' landings, the consoles, the grates'
  rims), and the grates are shut. The low outer walls are built up to the tall ones' height, 19 layers.
- *One roof just under the gas*, at -512. It is open only where the road's tunnels come down through it (`CxTowerTrack.RoofHoles`, the plan's
  tunnels with pieces). Its underside is a face a cell, in 16 x 16-cell bodies drawn from the floor's height (see the camera, below).
- No secret passage under it.

The route (`tools/RaceTrackPlan/cxtower_design.py`), 457 cells:

1. The start straight runs west along the island's south side. Its pit lane goes straight on along z 59.5 where the lap's S-bend turns up
   onto the straight, and comes back onto the road at the turn into the trench. It is a branch of the raised road, with three spots.
2. North down the south-west trench, which the build cuts wider, and through door A. The door's grate (body 9) is gone. The first 3 cells
   past the door are a tunnel sunk in the plateau, its roof flush with it.
3. A tunnel under the island at 26 %, and a U-turn under the north-west tower down into the ring.
4. Once round the ring on its inner lanes (west, south, east, north), then on round the outer ones (west and south). Most of it is on the
   floor at -5,166.
5. North up a tunnel to door C (26 % for its last 22 cells; 16 % up the outer lane before it). On the way it passes 3,144 over the ring's
   south inner lane.
6. Out through the north-east trench and north past the north-east tower. East round it over the gas, down the east side, round the
   south-east tower, and through the S-bend onto the start straight.

The road's rails are 3 cells from the road's middle underground and 3.5 outside (wider where there is room since the fifth round, below).
The plan's `edgeMargin` is 4: the builder no longer pushes the lap off the cube's edges, where the ring's outer lanes and the road round the
towers run.

The race-track mode's part (`RACEMOD.CPP`):

- *Under the ground* (the plan's `under`; the raised road file's eighth number on each point; an open area's sixth number):
  - The road's floor is the deck, whatever ground is over it, and there is no collision with the ground there.
  - The camera's sight line isn't lifted over the ground there, and the oil, the penguins and the open-area walls ignore the ground over the road.
  - A car there is in no gas (`TerrainUnderHook`, `GiveTerrainCodeJeu`), so it isn't rescued from the gas overhead.
  - The builder cuts no ground for these stretches, stands no piers under them and clears no decor in their way.
- *The cannons* on the four corner towers are scene 110's actors 3-6, entity 264, kept in the scene with their scripts emptied
  (`RaceTrackIsland.HuntingGuns`). Each is a gunner, `gunner=` with `gunner_hunt=<reach>` (40 cells). For each burst it picks a car at
  random among those within its reach that are outside: over the gas, not under the island. It fires 8 shots at where that car will be
  when each one comes down, the car's speed measured between shots, a little either side. A car that turns, brakes or speeds up is
  missed; one in a burst is stunned. The test pilot's race had them fire at Twinsen and the opponents alike.
- *Gas monsters*: the game's own stand on the ring's floor beside its lanes (`GasMonsterRun.Floor`; the monster's foot is its floor, its
  poses fitted to the road over it). Each strikes at its own place on the road. (6 at first, 2 since the second round below.)
- *The camera in the ring*: three tunnels of the camera's alone (no pieces) cover the ring, their roofs the room's roof over the road. The
  camera that follows the car normally stood 6,000 over it, out over the roof and the gas, where the island's ground near the eye isn't
  drawn (the near plane), so the car was seen through the island. In a tunnel the race-track mode's tunnel camera rides behind the car
  under the roof.
- *The roof itself* was first one body the whole cube wide, then 16 merged rectangles a tile. Neither was drawn:
  - The engine draws a decor whose box's bottom corners are in sight; a roof's are over the camera. So the roof pieces' boxes now start at
    the floor (`AddBodies`' `drawnFrom`).
  - The filler runs a face's depth straight across the screen, and the ground over the roof showed through big faces running away from
    the camera. So the roof's underside is a face a cell, with the close flag, as the palace's floors are.

The opponents are Island CX's own people's cars: the Emperor's soldier in his rocket launcher (the one to beat), a Franco trooper's tank
and the survivor's raft.

The test pilot raced 3 laps: 48.8, 53.2 and 51.9 s, first of four, no rescues and no missed checkpoints. It was hit by vents and bites
along the way, and the opponents finished all three laps.

![The lap from above](racetrack/cxtower/lap_from_above.png)
![The ring under the island, its roof off: the inner lanes once round, the outer ones on, the ramps down and up](racetrack/cxtower/ring_roof_off.png)
![The start straight](racetrack/cxtower/start.png)
![Into door A](racetrack/cxtower/door_a.png)
![The gas monsters in the ring](racetrack/cxtower/ring_gas_monsters.png)
![Up the tunnel to door C](racetrack/cxtower/exit_tunnel.png)
![Round the north-east tower, the car stunned by its cannon](racetrack/cxtower/cannon_tower.png)

### The second round (2026-10-15, later)

The user: "Our CXTower track, you've added steam that fires underground, this needs removing. Naturally the inside has two types of gas
monster, one that flies around and shoots red fire balls, and the other one that you've already implemented. Let's reduce the number of gas
monsters you've already added, and add some of the flying ones that fire fireballs. Currently driving inside there's no visible door way, it
looks like we're driving into a cliff, we should fix this". Then: "The large missile launchers on CX aren't firing in a way to hit the cars
as they're firing over the track, however we seem to have new machine gun fire coming from the turrets instead. The actual missile launchers
should target the cars, and let's remove the smaller machine gun fire".

- *No gas vents*: the plan's `steamJets` are gone (`cxtower_design.py`).
- *Gas monsters*: one a run on three of the ring's sides (`most: 1`); the build places 2.
- *Flying gas monsters* (`RaceTrackFlyers`, plan `flyers`, `RACEMOD.CPP` `flyer=`): the game's other gas monster, entity 329. In Island
  CX's room under the emperor's (scene 181) it rises out of the gas and spits red fire balls (raw sprite 20, its animation 173's
  `F_THROW_3D`) at Twinsen. Four fly to and fro along the south inner lane, the east and north sides and the west outer lane, 2,600 over
  the floor beside the lane (away from the walls), bobbing. Every 3.6 to 5.4 s each stops, turns to the nearest car below it in the hall
  between 1,800 and 11,000 away and spits a fire ball (its animation's own left out, `RaceMod_SilentThrow`) at where that car will be, a
  little either side. A car the ball comes down by is stopped 0.9 s.
- *The cannons fire their missiles*: the "machine gun fire" was the hunting gunners' bullets (`gunner_hunt=` borrowed the Franco gunner's
  burst), and the missiles flying over the track were the cannon's own firing animation's (`F_THROW_OBJ_3D`, OBJFIX.HQR 92, thrown level
  along its facing). A hunting gun now fires one missile a burst and no bullets: its animation's missile is left out (`RaceMod_SilentThrow`,
  now in `F_THROW_OBJ_3D` too) and the race-track mode flies the same missile straight from the gun's mouth (2,662 along its facing) at
  where its car will be when it gets there (7,500 units a second). Where it comes down, a nitro penguin's explosion, and a car within 1,400
  is stunned. The gunners' bullets, the missiles and the fire balls are one list, `Projectiles`.
- *Door A's doorway*: the road went down into the trench's end and into flat ground before the cliff, so it looked like driving into rock.
  - The sunk tunnel (door A's portal) rounded its cutting's far end 4 cells deep into the plateau over the road going on under the island.
    `CutSunk` no longer cuts past an end where the road goes on `under`; the raised road's cutting (`CutUnderRaised`) leaves a cell nearer
    an `under` point of about its height (2,000) than the stretch it cuts. Only Island CX has `under`.
  - A heightmap can't hang over the road: the ground's face over the mouth (one cell, from the road up to the plateau) stood across it.
    `OpenTunnelMouths` (after the tunnels are placed) leaves those cells' triangles undrawn (texture and polygon flags 0).
  - The sunk tunnel's end gets a wall from the next tunnel's roof up to the ground behind it (the plateau stands 320 over the portal's roof,
    and its edge showed, floating). Seen from the trench: the portal, a wall over the opening and the tunnel going down into the island.
  Door C, coming out, is the same.

The test pilot raced 3 laps: 48.2, 49.4 and 48.8 s, first of four, no rescues. Fire balls hit Twinsen 11 times and the opponents 17 (the
pilot never dodges); the missiles, 45 of them, mostly came down by the cars outside.

![Door A from the trench: the portal, the wall over the opening, the tunnel going down](racetrack/cxtower/door_a_doorway.png)
![Flying gas monsters over the ring](racetrack/cxtower/ring_flyers.png)

### The third round: no gas in the doorways, a camera that sees the car (2026-10-16)

The user: "Entering to go downstairs on Island CX the door way is filled with gas. Also the camera needs fixing on the entry and exit it's
too hard to see".

- *The gas*: a flat plane at the sea's height (-50) over the whole cube, the ramp under it seen through it from the trench -- the car half
  under the gas. Island CX's cube marks only its outer ring of sixteen squares for the sea (0xf99f), but the engine drew it under the whole
  cube: `DrawHorizon2ZBuf` loads the cubes round the camera's, and each that doesn't load sets `CubeBitField` to all sixteen (`LoadCube`)
  -- with no neighbour loaded (a one-cube island) the camera's own cube is never loaded again, so its own mask was lost. It now keeps the
  own cube's (`TERRAIN.CPP`). And the race-track builder's `SeaEverywhere` (every square marked: no black holes under a terrace) leaves a
  square without where the road runs `under` the island or below the sea's height (Island CX's door squares: 0xf9df).
- *The camera on the ramps*: a tunnel's roof was 1,200 over the deck, which kept the eye 300 over the road behind the car (4,200 back): on
  the 26 % climb out to door C that is under the car's deck, and the car was seen from under the road. Down to door A the eye came round the
  U-turn at the ramp's foot on the road behind -- off to the car's side, the tunnel's inner wall's end between them.
  - The ramps' tunnels are now 2,200 tall where they run under the plateau (`TunnelRun.UnderGround`: the roof's top 40 under the ground
    over it), each in two -- the first two cells from the door, under the plateau's face, lower -- and they end where the ramp is out of
    the plateau: door A's at the U-turn's start, door C's at the exit arc's end. A wall closes each tunnel's free end over the ring's roof
    (`CloseAbove`): the rock between that roof and the ground isn't drawn. The ring's roof is open only over the tunnels, to their ends
    (`CxTowerTrack.RoofHoles`), and has no box over the road where it runs under it with less than 1,700 over the deck
    (`CxTowerTrack.RoofLow`): a car's box is taller than the car, and the first try stopped dead there.
  - From the tunnels into the ring (round the U-turn down to the floor, and from the outer lanes' corner up to door C's tunnel) a tunnel of
    the camera's alone with a ceiling: `tunnel=`'s fourth number, a height the eye is kept 250 under (the ring's roof). There the eye is
    behind the car along the road's way at the car itself, 1,500 over the car's road (`RACEMOD.CPP` `HallEye`), eased in and out over the
    stretch's first and last 8 points; nearer (to 3,200) while that place is in a tunnel's wall or a wall of the island's objects.
  - On a climb the tunnel camera comes nearer (3,400) and rises to 500 over the car where the roof lets it; the eye is kept under the roof
    of the tunnel it is in itself (`InTunnel`, a tunnel's own stretch before the one before's tail).
  - Tunnel bodies are drawn close to the camera (`BrickBuilding.CloseFlag`): the near plane cut away the wall beside the eye and the
    ground outside showed through. (Every track's tunnel bodies have the flag now.)
- The test pilot: down door A's ramp behind and over the car, round the U-turn from behind, up to door C over it; 3 laps 51.1 and 56.7 s
  (fire ball and missile hits stop it, as before), no rescues, nothing on the ramps that stops a car.

![Down door A's ramp](racetrack/cxtower/door_a_down_the_ramp.png)
![The U-turn at the ramp's foot](racetrack/cxtower/door_a_u_turn.png)
![Up to door C](racetrack/cxtower/door_c_climb.png)

### The fourth round: no lip into door A, smooth walls, the flying monsters clear of the ramps (2026-10-16)

The user: "there's a slight lip which upsets the car very slightly when driving onto the slope to the underground section. The walls also
need some refining, as they're a little glitchy and hard on the eye, let's smooth them out so they're all one colour with a single straight
line down the middle instead of the current jagged gold line" -- and then "One of the flying walrus monsters currently clips through the
track".

- **The lip** (RACEMOD.CPP `RaisedAltitude`, `NearUnder`): the road's deck was smooth all the way down (a new console command, `cartrace 1`,
  logs the car each frame: its place, pitch, roll, speed, the floor under it and what its pitch was taken from). The trouble was the
  ground: door A's portal is sunk in the plateau, and over the road's last half cell before the `under` stretch the ground slopes from
  the cutting up to the plateau's edge, 1,900 over the deck. A floor there was the higher of deck and ground, so a car was lifted for a
  frame onto ground 700 over the deck, and its pitch probes (BUGGY.CPP `TiltFloorY`, 400 ahead and behind) found the slope and dipped
  the car's nose (166 -> -17 -> 160). Within 4 points of an `under` stretch, ground over the deck is now no floor for a car about as high
  as the deck: down through door A the pitch goes 133 -> 166 in steps of no more than 13, on every lap of three races.
- **The walls** (RaceTrackTunnel.cs `Surfaces`, `StripHeights`): each road segment had been a level box of its own -- wall, light strip
  and roof -- so down a 26 % ramp the strips went down in steps, and the boxes' ends showed light and dark along the walls. The pieces
  are now surfaces through the road's own points: walls straight up beside the rails, one colour; the roof from wall to wall; and one
  gold strip on each wall, at one height over the deck along a run of tunnels -- the middle of their height as it mostly is (1,100 in
  CX's ramps), lowered to stay 200 under a roof too low for it (955 in the portals) and eased over 6 cells, so it never steps. Each
  piece's origin is in whole units, so the pieces either side of a joint share their points. (Otringal's two tunnels are built the same
  way and look the same.)
- **The flying monsters** (RaceTrackFlyers.cs): a monster's way kept clear of the hall's pillars and walls -- decor boxes at its height --
  but the ramps' decks and tunnel pieces have boxes with no top, which nothing touches, so the ramps weren't seen: one monster's way
  crossed door A's U-turn at the road's own height, another's ran into door C's ramp tunnel, a third flew half a cell past the U-turn's
  edge at the cars' height. Now the road counts too: within its half width and 1.5 cells (the monster -- entity 329, body 451 -- is 760
  wide and flies along its way), from 600 under the deck up to a car's height over it or a tunnel's roof. A way keeps its longest run of
  clear points (a point left out in the middle had it fly straight across the gap): 8, 13, 11 and 10 points, from 11, 13, 16 and 17.

![Through door A's portal: one colour, one line](racetrack/cxtower/round4_door_a_portal.png)
![Up the ramp to door C](racetrack/cxtower/round4_ramp_walls.png)

### The fifth round: wider where there is room (2026-10-17)

The user: "CXTower track feels too tight let's see where there's scope for widening the track slightly but let's keep the current route
just making changes where we're able too".

The route is the same: the road's middle hasn't moved, and nor has the racing line -- the opponents and the test pilot drive the lines
they drove, planned within the old widths (the plan's `lineHalfs`, `RaceTrackBuilder.PlanRacePath`). The road is wider wherever nothing
stands near it, eased between one place and the next (`cxtower_design.py`, `WIDTHS`). Half widths, cells from the road's middle to its
rail (the rail keeps a car's middle 1.25 inside it):

| stretch | before | after | what limits it |
|---|---|---|---|
| the start straight | 3.5 | 4 | the pit lane beside it: 0.75 between the decks |
| the turn into the south-west trench, the trench | 3.5 | 3.75 | the south-west tower's base at the turn's end, 0.7 clear (a car's box, square to the island, 0.45); the trench's sides are ground, cut back |
| door A and door C: the portals and the tunnels down and up | 3 | 3 | a tunnel's roof is kept under the ground over its walls, and the plateau's edge falls away beside both ramps: half a cell wider, the roofs would be 240 lower, 1,200-1,400 over the deck at the doors' faces (the car is 940 tall) |
| the U-turn and the ring's west inner lane | 3 | 3.15 | the west outer lane 7 cells over: 0.65 between the decks |
| the ring's west outer lane and north side | 3 | 3.2 | the hall's walls, 0.3 clear |
| the ring's south inner lane | 3 | 3.4 | the gas monster between it and the outer lane (below) |
| the ring's south outer lane | 3 | 3 | the same gas monster |
| the ring's east side | 3 | 3.5 | nothing near: the wall is 7 cells off |
| the north-east trench | 3.5 | 4 | ground, cut back |
| round the north-east and south-east towers, the S-bend | 3.5 | 3.5 | the towers' bases and struts, 0.15-0.6 clear, and the cube's edge |
| the east side between the towers | 3.5 | 4 | the cube's edge: the rail half a cell from it |

The roads narrow into the doors over 7 cells (from 3.75 to door A, from door C to 4).

What else the wider road moved:

- *The south side's gas monster* stands between the two lanes, its run's `out` past the inner lane's rail, and must be the outer lane's
  half and 2.5 cells from that lane's middle (`RaceTrackGasMonster`, `ClearOfLap`); the lanes are 11 apart. It stands 2 out now (from
  2.5): at 1.5 its poses put its neck through the rail. So the inner lane is 3.4 and the outer one stays 3.
- *Gas monsters on a floor stand on it*: `Gas` now also asks for the floor's box under the spot. With the north and west lanes wider, their
  monsters' far-side spots were past the hall's walls, outside the cube (a spot exactly a cell from a wall had been ruled out by rounding
  alone). The monsters are where they were.
- *Piers*: a pier stands on the ground, so a road `under` the ground is not in its way (`PlacePiers`). The ring's wider east lane, 4.5
  cells from the outside road over the gas, had taken four of its piers; and two the ring kept out before stand now, on the north side
  and in the S-bend (12 piers, none left out).
- *A checkpoint*: the builder lays a line across a corner where it reaches past the road's edge or is stopped by another part of the lap
  no nearer than the edge, probing in half cells; across the ring's south-west inner corner it now stopped short of the wider edge (the
  west outer lane beside). The plan adds a line of its own there (`checkpointTweaks`), as it was: 9 checkpoints.

The flying monsters' ways, the tunnels, the power-ups and the racing lines are as they were. Of the other 17 tracks, built together with
it, every file is the same; only Island CX's own (its island, its RACETRACK.JSON entry, scene 300 and its gas monsters' animations) differ.

The test pilot, three laps against the opponents with the power-ups (the stress test's car, 110 km/h): on the line and 1.5 and 2 cells
either side of it, and the hot car (everything +20 %), 8 races and 5 time trials -- every race finished, no rescues, falls, stuck cars,
edge holds or missed checkpoints (the same runs on the old build: one stuck car reset, the hot car 1.5 cells off the line in the ring's
south lane). Lap times are mostly the hazards: 17-37 s laps either way, a clean one about 20.5 s.

## The Temple of Bú: the Halloween race (2026-10-16)

LBA1's Temple of Bú ported as an island of its own, in LBA1's own colours, and a Halloween race on it: through its rooms on a raised road with
doorways cut through the walls, down a spiral through the dark to its crypt and back up. There are jack-o'-lanterns, gravestones, cauldrons,
witches throwing fire balls, ghosts and bats, and mini pumpkins for mushrooms. See `docs/LBA2_TEMPLE_OF_BU.md`.

## The game has the keyboard from the start (2026-10-12)

The user: "After hitting play sometimes we have to manually click the game area to get it to respond to input, let's see if we can remove
this limitation". The game's window was given the keyboard once, as it was embedded; anything of the editor's that took it after that
kept it -- WPF giving the focus back to the control that last had it when the editor's window is activated again (after the race car's
setup closes, after another program), the game's own window restyled by SDL at a change of size -- and only a click on the game gave it
back. Now the game has the keyboard from the moment it is embedded until a click on one of the editor's own controls, and again from a
click on the game; while it should have it, it is given it back on the next tick (120 ms) whenever the window in front is the editor's,
full screen's or the game's own (`EmbeddedGameHost.FocusTick`, `EditorInFront`; a click on the game comes in as the game's window's
`WM_PARENTNOTIFY`). A dialog, a panel floating on its own or another program in front keeps the keyboard. Checked from the UI: Play, the
race car setup's Play, and no click -- the game's window had the keyboard from the first look, 6 s on, to the last, 36 s on.

## The menu command

Tools > LBA2: Desert island race track... (`RaceTrackWindow.cs`, `Terrain/RaceTrackService.cs`) builds the track from the plan built into the program, or from a plan file. The scene options are choices in the dialog; the crossing style, clearing the old track and drawing on the holomap were too, until 2026-09-30 (see "Fixed choices").

The first build keeps `DESERT.ILE`, `DESERT.OBL`, `SCENE.HQR`, `ANIM.HQR` and `RESS.HQR` as `*.before-racetrack` beside the originals (a folder built before the jump used the last two keeps them from its next build on, while they are still the originals), and every build starts from those. "Put the original files back" restores them all and removes the copies and RACETRACK.JSON. "Race car setup…" opens the car setup. The editor's views are refreshed afterwards.

It was tested on a fresh copy of the game folder, with the bridge and with the jump (again on 2026-09-28 with the squared-up jump, checkpoints and opponent, and with Baldino's car: all six files): the result is byte-identical to the command-line build (all five files and RACETRACK.JSON), and restoring gives back the original files' hashes.

![dialog](racetrack/build/menu_dialog.png)

Extra commands in `ScriptRoundTrip`:

- `driveprep <game> <cellx> <cellz> <turn>`: the buggy and Twinsen ready on the road at a cell.
- `initbuggy <game> <scene>`.
- `scripttext <game> <scene> <actor> life|track`: a script as the editor's C text.
- `racetrack island <n>` and `racetrack <cx> <cz> --scripts --hero`.

## The race game (2026-10-16): Little Big Halloween Racing Adventure

The user: a standalone build outside LBA Assembler, in the retail game's look and menus, with an installer that takes both games' folders,
copies their data into its own folder and builds the race tracks there; "Story Mode" (in place of New game), "Track select" and "Specials",
the first two greyed for the Halloween special; items on Ctrl instead of Shift ("this shouldn't be used when driving anyway"); a car
selection of ten character cars, the skeleton's and other Halloween ones; and a best-times list per track, the top ten, from a time trial
-- no opponents, no pickups, a can of Gazogem for each lap.

The installer is its own project, `E:\dump\LittleBigRacingAdventure` (its README says how it installs and builds). The engine side is here:

- **Ctrl uses the item** (RACEMOD.CPP `PowerUps`, `RaceMod_FilterInput` from PERSO.CPP's frame input): the behaviour key, read from the
  frame's input before it is masked, so a key bound to it in the options and a pad's left shoulder use items too; in the car the behaviour
  menu doesn't open (its other behaviours take Twinsen out of the car). Shift opens the inventory again, as in the game. LBA Assembler's
  Play has the same keys.
- **The race game** (RACEGAME.CPP, on when `RACEGAME.TXT` is beside the game's data): the main menu is the race game's -- Resume (a race
  on), Story Mode and Track select (greyed and passed over: GAMEMENU.CPP's rows, its keys and GAMEMENU_MOUSE.CPP's hit test ask
  `RaceGame_RowDisabled`), Specials, Best times, the game's Options, Quit. Specials -> the track -> Race or Time trial -> the car page (its
  body turning in a box, measured by the pixels it draws: the character cars' header boxes are their template's) -> the special's race
  file with the car's `drive_as=` and the race game's keys, written to `RACE_NOW.TXT` in the user's folder and loaded (`RaceMod_LoadFile`),
  then a new game with no introduction. No first-launch introduction either.
- **New race-file keys** (RACEMOD.CPP): `start_scene=` (a new game's scene: the start line's), `start_in_car=1` (Twinsen put in the car as the
  scene opens: the car's own SET_MOVE, `AdjustDirObject`), `trial=1` (a can of Gazogem at GO and at the start of each later lap, the item box
  shown without mushrooms, the time from GO to the last line into the best times: `RaceGame_Finished`), `end_to_menu=<ms>` (after the race,
  back to the menu: PERSO.CPP's menu key asks `RaceMod_WantsMenu`).
- **Best times**: `BESTTIMES.TXT` in the user's folder, `track|ms|best lap ms|car|date`, the ten fastest of each track kept.
- **Difficulty** (2026-10-17): a race on a track with levels -- its race file has `difficulty_level=` lines: the Temple of Bú's -- asks
  Easy, Medium or Hard between Race and the car page (the last one chosen comes up first, kept in `RACEGAME_PREFS.TXT` in the user's
  folder; Esc on the car page goes back to it); the pick goes into `RACE_NOW.TXT` after the file's own `difficulty=`, so it counts. A time
  trial has no levels (Twinsen alone): its best times are the same at every level. What the levels do: docs/LBA2_TEMPLE_OF_BU.md.
- **Screenshots of the menus**: the console's `screenshot` is taken in a modal's own loop too (PERSO.CPP's pre-present chain: the main
  loop's capture never runs in a menu), so a headless run with `--listen` -- and no `--fixed-dt`, `--exec-at` or `--tick`, any of which arms
  the automated boot that goes past the menu -- can picture every page: `E:\dump\TEMP\templediff\menudrive.ps1`.

Tested from a headless install (`LBHRA-Setup.exe --install`) of the pristine assets into `E:\dump\TEMP\lbra\install`, driven through the
engine's `--listen` socket (`key`, `autodrive`): the menus, all ten cars on the car page, a time trial (laps of 54.7, 56.5 and 54.2 s, a
can each lap, 2:48.59 the best time), Ctrl using the can with no behaviour menu, a race against the three (1st of 4), and each back to the
menu.

### The first run: controls, power-ups, qualifying (2026-10-17)

The user: "a powerup page for LBRA that shows the user all the powerups and what they do, we can show this as a flash screen the first
time they load a race, and have a view option for it in settings if they wish to view it again, we should also the first run show the
controls, and let's also force a qualifying lap the first time they play, with the option to skip qualifying in further runs and start
from the back of the grid."

Race game only (RACEGAME.CPP; the editor's Play is unchanged):

- **The first run's pages**, right after the car page (and the qualifying answer), as the race file is loaded and before the scene: the
  player's first race or time trial gets the **Controls** page (the keys and a pad's buttons; the gears' keys when the car's are changed by
  hand), the first race with power-ups the **Power-ups** pages (the pickups, then the track's power-ups four a page, each in its item box
  turning as the race draws it, its name and what it does). Enter turns the pages ("Enter: start" on the last), left and right too, Esc
  skips the rest. Once shown they don't come back by themselves.
- **Options** (GAMEMENU.CPP `OptionsMenu`, the game's own menu with two rows after its own: `RaceGame_OptionsMenu`/`RaceGame_OptionsRow`):
  **Power-ups** and **Race controls** show the pages again (the game's own "Controls" row is the key remapping).
- **The track's power-ups** come from its race file alone (RACEMOD.CPP `RaceMod_ReadPage`): the ones `RandomPowerUp` draws there
  (`fuel_always=` drops Gazogem, no `fake=` copies the fake pickup, no `haunt=` the ghost; `powerup_only=` lists them), the golden Gazogem
  with a `roulette=`; a time trial's Gazogem alone. The pickups' look is a mushroom actor's body, read from its scene in SCENE.HQR the way
  `LoadScene` reads it (the Temple's: its pumpkin). `RaceMod_DrawItem` draws a box with `ItemBox`, the page's icons in place of the race's.
- **Qualifying**: the first race always has a qualifying lap (`qualifying=1` added to `RACE_NOW.TXT`); once one has been driven
  (`RaceGame_Qualified`, from RACEMOD's qualifying lap), a race asks after the car page: "Qualifying lap" or "Skip: start at the back of the
  grid" (`qualifying=0` + **`grid_back=1`**: `FormGrid` puts Twinsen behind every opponent, they in their qualifying times' order; the log
  says "Twinsen (no qualifying: at the back)"), the last answer chosen first; Esc is the car page again. Not asked for a time trial or a
  track without grid spots.
- **Settings**: `RACEGAME_SETTINGS.TXT` in the user's folder, beside `BESTTIMES.TXT`: `controls_seen`, `powerups_seen`, `qualified`,
  `skip_qualifying` (other keys kept as they are). A new user folder (a reinstall) starts the first run again.
- **Screenshots in menus**: the console's `screenshot` is taken in `DoGameMenu` and on these pages too (it was only the game loop's), so a
  headless `--listen` run captures them.

Tested headless and muted on a scratch install (`E:\dump\TEMP\lbrapages`: `LBRA-Setup.exe --install`, the worktree's engine copied over
the game's exe, `drive.ps1` through `--listen`): a fresh user folder's first race (controls + 3 power-up pages, then a forced qualifying lap
of 122.8 s and the grid from it), a second run (no pages, the question, skip: "4 Twinsen (no qualifying: at the back)", "Position 4th of 4"),
a third (qualifying chosen, the skip answer pre-selected, Esc back to the car page), Options > Power-ups / Race controls, a time trial first
(controls only) then a race (power-ups only, Esc skipping), and the editor's Play of the Temple (no `RACEGAME.TXT`) the same as the old
engine's with `qualifying=0` (Twinsen on pole) and `qualifying=1`.



## One item box (2026-10-17)

The user: "Let's also reduce item boxes from two to one. Let's keep the second item box for Otringal for when a user gets the golden
gazogem, and only make it visible if they already have another item, when the user uses their primary item we can remove the additional
item box again and replace the first box with the golden gazogem item, this also means that we can drop the item selection key to simplify
the controls."

All in RACEMOD.CPP (`PowerUps`, `GiveItem`, `GoldIntoBox`, `BoxFree`, `WheelPrize`, `SlotMachine`, `RaceMod_Draw`), race-track mode only:

- **One box.** Twinsen holds one item. A mushroom he comes to with the box already full is not taken: it stays standing for the next car
  (it isn't in a car's way since `RaceMod_NoCollide`, so the old "give way, empty" for the CheckObjCol jam is no longer needed). The rest
  of a row he has just taken one from still gives way, empty, as an opponent's row does (one item a row, `RACE_ROW_MS`): left standing
  too, they armed the cars behind a leading Twinsen far more than before (the Temple's opponents took 70 mushrooms in a test race against
  1 at HEAD). The log says "a mushroom left standing: Twinsen's item box is full" (at most once every 5 s).
- **Ctrl uses it**, in the editor's Play and in the race game alike (`RaceMod_FilterInput`, the behaviour key, since 2026-10-16). **Q is
  gone**: nothing reads it, and nothing is selected. The box is ringed in gold while it holds the item Ctrl uses.
- **The golden Gazogem's second box** (Otringal's roulette wheel, the green). Won with the box empty, it goes into the box. Won while the
  box holds an item, it waits in a second box beside it, drawn only while it holds something. When the box's item is used (Ctrl), the
  golden Gazogem moves into the box and the second box goes; the next press is its first boost. It stays in the box for its 8 s as
  before (a boost at each press), and if a second golden Gazogem waits behind a running one, it moves in when the first's time is up.
  With both boxes full, the green gives its 5 s boost instead.
- **Red, the slot machine.** With the box empty as the car lands, the reels spin and the box is kept free for the prize (a mushroom passed
  meanwhile stays standing: "kept for the slot machine"); a win goes into it. With the box full as the car lands there is no spin: red's
  2.5 s boost instead, as without power-ups.
- **The time trial's cans** (the race game): a can at GO and at each later lap, but none while Twinsen still holds the last one.
- **Opponents** never held items: each uses what it takes at once, one from a row (`OpponentMushrooms`, `o->TakeAt`). Unchanged; they now
  also find the mushrooms Twinsen leaves standing.
- **Saved games**: `ForgetPowerUps` empties both boxes on a load, as before; a new track (`ResetTrack`) also ends a running golden
  Gazogem and the slot machine.
- **Console tests**: `racebox <n> [won]` (power-up n into the box as a mushroom's, only into a free box with the roulette turning, or with
  `won` 1 as a prize, the golden Gazogem 11 into the second box when the box is full) and `raceprize <0|1|2>` (the wheel's prize now: 0
  black, 1 red, 2 the green).

![one box: empty, the roulette, a penguin; the golden Gazogem waiting, moved in, its gold bar; the slot machine, its prize; red with the box full](racetrack/build/item_box_one.png)

**Verified** (headless, muted, `E:\dump\TEMP\itembox`: plain copies of the stress test's 18-track game and the Temple's newest build):
- *The states* (Otringal, the car standing on the grid, `state.sh`, `slot.sh`): a penguin taken with its roulette; oil offered with the
  box full, left standing; the green with the box full -> the second box; Q pressed -> nothing; Ctrl -> the penguin dropped and the golden
  Gazogem moved in; its boosts and gold bar; used up after 8 s; the green with the box empty -> straight into the box; red with the box
  empty -> the slot machine (wins: a penguin, a super jet-pack, a fire ball, the protection spell, lightning; one miss), a mushroom
  offered meanwhile left standing; red with the box
  full -> red's boost; the green with both full -> the green's boost; saved with both boxes full, loaded -> both empty.
- *Pilot races* (3 laps, the opponents, power-ups, the pilot using each item a second after its roulette; `races.sh`, `otrstat.sh`):
  every race finished. Desert, Mosquibees, Emerald and the Temple (with its ghost) twice each: no rescue. Otringal 14 times: 3 rescues,
  all the same pile-up in scene 92 by the spaceport's pad -- cars stopped together by a penguin's blast or a ball, the opponents then
  held again and again by the landed shuttle, Twinsen's car pinned behind them at the rail, put back on the road after 3 s (`StuckCar`).
  At HEAD, 0 in 10. Twinsen takes about half as many items now (Otringal: 19-30 a race, 33-54 at HEAD), so he races nearer the pack;
  the opponents' pickups are much as before (Otringal median 36, 33 at HEAD). A way out, if it recurs: an opponent's car stopped by a hit
  not solid to Twinsen's (`RaceMod_NoCollide`, as on the super jet-pack).
- *The Temple driving* (`run.sh temple drive`): the one box during the race, ringed while it holds an item.

## A quicker roulette, its stop unmistakable (2026-10-17, later)

The user: "Let's reduce the time it takes to select a powerup. Currently when selecting a powerup the last few frames show for too long,
which can make a user think they've got a power up whilst it's still rotating."

**Why the last frames lingered.** The roulette ran 4.5 s past 45 items, its place `45 (1 - (1 - t)^2)`: an ease-out whose rate falls to
nothing at the stop. Each step lasted longer than the one before it, and the last ones most of all -- the item *before* the one drawn stood
in the box 0.67 s, the two before it 0.28 and 0.21 s -- and since the box was ringed in gold the whole time, a standing item looked like
the result. Then it changed.

**Now** (RACEMOD.CPP `ShownItem`, `ItemBox`, every track -- the race game's too):
- *0.9 s, 14 items* (`RACE_ITEM_ROLL_MS`, `RACE_ITEM_ROLL_STEPS`): the rate falls only to half its start's (`RACE_ITEM_ROLL_SLOW`), an
  item every 48 ms at first and every 96 ms at the end, the last step landing on the item drawn. The odds, the rows, the one box and the
  golden Gazogem's second box are as they were (the item is drawn as the mushroom is taken; only its showing changed).
- *Going by, nothing looks settled*: the items are drawn at 0.8 of their size and the box has no ring.
- *The stop*: the item pops up 1.45 times its size and comes down to it over 0.3 s (`RACE_ITEM_POP`, `RACE_ITEM_POP_MS`); the box's ring
  comes on white and wider for 0.24 s (`RACE_ITEM_FLASH_MS`), then gold -- gold now means "ready". An item won outright (the slot
  machine's prize, the golden Gazogem, a time trial's can) pops in the same way.
- *Ctrl* uses the item only once it has stopped, as before (a press while it turns does nothing and isn't kept; holding the key through
  the stop doesn't fire it: the next press does). The test pilot still uses each item 1 s after its stop (`autodrop_ms`).
- *The slot machine's reels* (Otringal's roulette wheel, red) have their own code (`SlotShown`): they turn at a steady 70 ms an item and
  stop dead, one after another, so nothing lingered there and their timing is unchanged. They share the drawing, so each reel is drawn
  smaller while it turns and pops up as it stops, and the winning line's ring flashes white as the third stops.

![Before: the last 1.5 s of the 4.5 s roulette; now: the whole 0.9 s roulette and its stop; the slot machine's reels](racetrack/build/item_box_quick_roulette.png)

**Verified** (headless, muted, `E:\dump\TEMP\ghostfx`: `roll.sh` -- the Desert car standing on the grid, a penguin into the box with
`racebox 3`, a picture every frame; `slot.sh` -- Otringal's slot machine, `raceprize 1`): a Ctrl press 0.2 s before the stop does nothing,
one a frame (20 ms) after it drops the penguin; the reels pop at 1.2, 1.9 and 2.6 s. Pilot races (`races.sh`, 3 laps against the
opponents, the pilot using each item 1 s after its stop), this build against the one before: no rescue in either; the opponents still
take and use their items. **The item rate goes up**: the box is free 3.6 s sooner, so the pilot takes about twice as many items, and
leading he empties the rows the opponents behind him found standing before (one item a row). Taken in the 3 laps, Twinsen's and all the
opponents': Desert 44 and 36 (19 and 64 before; 1st of 6, 2nd before), Mosquibees 18 and 4 (6 and 27; 1st of 3, 3rd before), the Temple
61 and 20 (26 and 29; 1st of 4 both) -- one race each, the races aren't repeatable to the frame. A player who keeps an item for a
moment takes fewer; if the old rate is wanted back, a pause after each item before the next mushroom gives one would do it.

## The red magic ball follows the track (2026-10-18)

The user: "Powerups: The Red magic ball to follow the track until it hits the car in front."

**Before.** The red ball went after the car in front (by how far round the race each car was), along the *guide* line (the first
opponent's) at 24,000 units a second. Its car's place on that line was looked for afresh every frame anywhere on it, and from 6,000 away it
flew straight at the car, through whatever was between. It never changed its car, and a ball thrown by the leader darted straight ahead for
112 cells, off the road at the first bend.

**Now** (RACEMOD.CPP `RedBall`, `RedAim`, `RedGap`, `LineNear`, `CarAhead`/`RaceShare`, `BranchStep`, `HeroBranch`; every track, Twinsen's
balls and the opponents' alike):
- *Its way*: the racing line of the car it goes after. That is the car's own way round, so the ball is on that car's level of a raised road,
  goes over its carried jumps as the line does and through every scene of the lap. The car's place on the line is the car's own (its S), in
  sight or scenes away. After Twinsen, who has no line, the ball takes the thrower's line, and Twinsen's place on it is looked for within 40
  points of where he was (on a raised road the height counts, so another level of the road is never taken for his). The way to the car is a
  distance along the track (a car most of a lap ahead is that far along it), and the ball never runs past the car's place.
- *Its speed*: twice the player's car's top speed, which is 220 km/h with the race game's car (110). It comes up on a car at the speed the
  car itself drives: a car 20 cells ahead is reached in about 0.7 s, 100 cells ahead in about 3.3 s. It is faster than a car on Gazogem
  fuel (1.5 times), never slower than the old 24,000, and never slower than 1.4 times its car's speed or its thrower's.
- *The last stretch*: within 10 cells it comes over to its car's side of the road. Within 3 cells, on the car's own level, it flies straight
  at the car, so it connects with a car anywhere across the road (the hit reach is the balls' 950). If the car gets 16 cells away (a rescue,
  a teleporter), the ball goes back to following the track.
- *Loops*: at a loop's foot the ball goes round the ring, inside it over its band, as a car does (the line itself steps across the road
  there, from the lane in to the lane out). It then carries on from the lane out.
- *A car it can't hit yet*: a car in a jump's flight, going round a loop or carried over a jump is not struck there. Struck, an opponent stood
  in the air on its line over the gap and Twinsen's flight broke off. The ball follows 1.5 cells behind until the car is down, then hits it.
- *Branches*: if Twinsen is on a branch of the raised road (Wannies Island's shortcut, its pit lane), a ball behind the branch's mouth turns
  into it after him and rides the branch's own road. A ball already past the mouth waits where the branch comes back onto the lap.
- *Another car to go after*: if its car finishes the race, is put back on the road (Twinsen's rescue) or drops out, the ball goes after the
  car now in front of it. With none, it carries on as a leader's ball. A ball from before a new grid (a lost sprint run again) fades.
- *No car in front* (the thrower leads): along the track 112 cells (as far as before, but round the bends), going after a car met within 16
  cells ahead on the way (a back marker the leader is coming up to lap), then it fades with the magic ball's burst.
- *Immunities*: as before (`CarStruck`). The protection spell holds and the super jet-pack absorbs the ball, which is used up either way.
- *Opponents*: the same ball. On Hard (the Temple's level, `OpponentAims`), an opponent holds a red ball until Twinsen is the car directly in
  front of it, as before, but now however far ahead and on whatever level. The 900-unit height check was for the straight balls.
- *The car in front*: found by how far round the race each car is, each opponent's progress taken as its share of its own line's length
  (`RaceShare`). The Emperor's palace's lines take their own ways through the rooms and are up to half as long again as each other, so a
  car further along a longer way had been taken for one ahead. A car that has finished is no one's car in front.
- *Seen*: Twinsen's magic ball sprite in red, as before, 450 over the road, now also drawn up to 32 cells past the edge of the scene's cube
  (the road in sight goes on there). It leaves a trail of the red ball's own sparkle (`SPRITE_BALLE_SHADOW_3`, the ball on its way back to
  Twinsen's hand in the game), a small see-through one every 45 ms.
- *The log*: "Twinsen's red ball follows the track to The skeleton (24 cells ahead)", "... (160 cells ahead, out of sight in cube 10,7)", a
  "follows the track: 26.2 cells behind Baldino (scene 231, in sight)" each second, "goes round loop 4", "turns into the raised road's
  branch 1 after Twinsen", "closes in on The skeleton (2.6 cells)", "hits The skeleton (0.6 s, 39 cells along the track, in sight, cube
  7,8)", "the car it went after has finished -- no car in front of it now, ...", "meets Baldino on its way (14.1 cells ahead)", "fades out:
  no car met on its way (1.8 s, 112 cells along the track)".
- *Console test*: `raceattack <n> [item]`: opponent n (1 the first) throws a ball power-up now (8 red, the default), wherever its car is.
- *The race game's power-up page*: "Follows the track to the car in front and stops it."

![The red ball: round a bend to the skeleton; up a ramp and round the right-hander; round a loop's ring; the leader's ball after a back marker; an opponent's ball along Wannies Island's shortcut onto Twinsen](racetrack/build/red_ball_track.png)

**Verified** (headless, muted, `E:\dump\TEMP\redball`: plain copies of the stress test's 18-track game and the Temple's newest build; `run.py`,
`scout.py`, `loopscout.py`, `batch.py`):
- *Twinsen's ball, followed from his chase camera* (`raceitem 8` at a moment `scout.py` picked, the race's randomness fixed by
  `qualifying_seed`): the Temple, the skeleton 17 cells ahead round a left-hander, hit after 0.4 s and 26 cells; 26 cells ahead up a ramp
  and round a right-hander, 0.7 s and 42 cells; 24 cells ahead on a straight, 0.6 s and 39 cells. The Emerald Moon: Baldino 21 cells
  ahead past loop 4, the ball round the ring and on, hit after 1.0 s and 59 cells; Baldino round a loop when the ball reached him, held
  1.5 cells behind and hit as he came out.
- *Scenes away* (the Desert, 19 scenes): Twinsen's ball at the Dino-Fly 160 cells ahead in cube 10,7, hit after 3.4 s and 205 cells; Moya's at
  Baldino 122 cells ahead, hit after 3.9 s and 239 cells, out of sight; the log following each ball through the cubes once a second.
- *The leader's ball* (the Emerald Moon, both opponents at 45 %): Baldino, a lap down, 14 cells ahead: "meets Baldino on its way", hit
  after 0.3 s; with no one ahead, "fades out: no car met on its way (1.8 s, 112 cells along the track)".
- *A branch* (Wannies Island): Twinsen standing on the shortcut, the old Wannie's ball 39 cells behind: "turns into the raised road's
  branch 1 after Twinsen", hit after 0.6 s and 38 cells, coming at him from behind along the shortcut's road.
- *Another car*: Baldino finishing the one-lap race before Twinsen's ball reached him: "the car it went after has finished -- no car in
  front of it now", faded 112 cells on. Twinsen put back on the road with a ball after him (teleported off the road under the Temple's
  deck): "was put back on the road -- after Twinsen now (36 cells ahead)", hit.
- *Hard* (the Temple, every mushroom a red ball): an opponent holding its ball for Twinsen threw it once he was the car in front and hit him;
  balls held 6 s without that went after their own car in front.
- *Red balls only* (`powerup_only=8`, the pilot using each a second after its roulette; all 18 tracks, one race each, `batch.py`): every race
  ran to its end. One rescue, on Sendell's Well: Twinsen stuck 3 s behind a car a ball had stopped on its one-car-wide road. HEAD had 10
  (one there too). 1,313 red balls: 474 thrown at a car in front, 460 of them hit it (the rest went after another car when theirs finished,
  or were still on their way when the run ended); 839 with no car in front (a leader's, or a car's whose race was over), 39 of those met a
  back marker. 33 went round a ring. HEAD: 1,209 thrown, 590 at a car, 568 hits.
- *Every power-up* (the usual draws; the Temple, the Emerald Moon, Wannies Island, the Desert, the town circuit, the Emperor's palace,
  Otringal, Mosquibees Island; two races each, this build and HEAD): all 16 finished, no rescue in either. The opponents took 419 items
  (413 at HEAD). Of 47 red balls, the 15 thrown at a car all hit it.

The 18 tracks stress-tested and car setups scored per track (2026-10-16): docs/LBA2_RACE_TRACK_STRESS_TEST.md (`raceplan`, tools/RaceTrackStress).

## The pack kept together (2026-10-20)

The user: "Let's tweak the car behaviour to keep them more bunched so there's more overtaking, let's also change powerups that are given
to cars to help keep cars bunched more too." Every race with opponents, every track (`RACEMOD.CPP`, the pack kept together):

- **The pack's middle** (`PackFactors`): the median of every car's place in the race, Twinsen's among them. An opponent more than 0.5 s
  ahead of it eases off, one behind it pushes, up to the level's share at 7 s (easy eases 12 % / pushes 8 %, medium -- and a track with no
  levels -- 9 % / 9 %, hard 6 % / 11 %). Behind Twinsen the push and the level's catch-up don't stack: the more of the two.
- **The slipstream**: a car 1 to 12 cells behind another (full from 2.5 to 6 cells) is 3.5 % quicker -- Twinsen's car too
  (`RaceMod_SpeedFactor`, its top speed and pull).
- **Form**: each opponent 3.5 % quicker and slower in turn, each on its own beat of 50 to 85 s.
- All of it eased in over 1.5 s (`o->Pack`, `o->Band`): no car brakes on a straight nor shoots off.
- **Skills drawn nearer the middle** of `opponent_pace=<lo> <hi>` (0.6 of the way out: 72-98 is drawn as 77-93); a fixed skill as given.
- **Items by place**: the leader draws mostly the protection spell and the balls that go on ahead of it, Gazogem fuel a third as often
  and fewer drops; the back draws the super jet-pack, lightning, the red ball and the fire ball more. The opponents at the front pass
  rows by (`PackTakes`: the leader 60 / 50 / 35 % of them by level, the second about a fifth); Twinsen takes from every row. The
  Temple's ghost when due is untouched.
- **The log**: at each of Twinsen's race laps `pack at Twinsen's lap N: <order, each car's gap to the leader in s>; spread, Twinsen to the
  nearest, passes so far` (a pass counted once a car is a cell clear), and each opponent's item with its place (`0.33 of the way back`).

**Measured** (the Temple, 5 laps, no qualifying, the test pilot at 100 % -- the perfect line -- and 88 %, seeds 101, 202, 303; means over
the three; spread = first to last at Twinsen's lap lines, s):

| Level, pilot | Spread | The opponents' spread | Twinsen to the nearest | Passes among the opponents | Passes with Twinsen | Twinsen's places |
|---|---|---|---|---|---|---|
| Easy, 88 % | 49.4 → 39.5 | 15.4 → 3.8 | 34.0 → 35.7 | 9.7 → 12.7 | 0 → 0 | 1 1 1 → 1 1 1 |
| Easy, 100 % | 63.8 → 56.4 | 14.8 → 4.0 | 49.0 → 52.4 | 5.3 → 13.7 | 0 → 0 | 1 1 1 → 1 1 1 |
| Medium, 88 % | 12.8 → 6.8 | 9.6 → 3.5 | 3.4 → 3.3 | 16.3 → 21.0 | 2.0 → 5.7 | 3 1 1 → 4 1 1 |
| Medium, 100 % | 27.2 → 16.9 | 14.5 → 8.5 | 12.8 → 8.5 | 8.7 → 11.3 | 1.3 → 0.7 | 1 1 1 → 1 1 1 |
| Hard, 88 % | 8.0 → 4.0 | 5.7 → 2.7 | 2.5 → 1.4 | 16.7 → 28.0 | 12.3 → 16.0 | 4 3 1 → 4 4 1 |
| Hard, 100 % | 8.5 → 5.4 | 6.3 → 3.7 | 2.3 → 2.0 | 15.3 → 25.3 | 9.7 → 12.0 | 1 1 4 → 4 4 1 |

Hard's losses are close: the three opponents 1 to 5 s ahead at the line. Easy keeps its runaway lead for Twinsen (no catch-up there by
design); its opponents race each other in a pack. The leader's strong items (a boost, lightning, the red and fire balls, the ghost)
went from a quarter of its draws to a sixth, its Gazogem fuel from 12 % to 6 %; the ghost still came 4 to 6 times a race.

The Desert (five opponents, no levels: medium's numbers; 3 laps, seed 101; the test pilot stuck far behind there in both builds, so the
opponents alone): they finished 428.6 to 558.8 s from GO, the first four within 52 s; now 462.2 to 492.8 s, the first four within 2.8 s.

## The handbrake (2026-10-21)

The user: "Let's modify the engine to allow us to have access to a 'handbrake' so we can use it to skid around corners. Let's
initially map this to shift." In the race-track mode only (the race game and the editor's Play), the player's car only.

- **The key**: Shift, either one -- or whatever key Options gives the inventory, which the car has no use for (the buggy's
  behaviour has no `CF_INVENTORY`), and a pad's **Y**, the inventory's button (`RaceMod_FilterInput` notes `I_INVENTORY` and keeps
  it out of `Input` in the car). Nothing else reads Shift while driving (Ctrl took the items off it on 2026-10-16).
- **The model** (RACEMOD.CPP `RaceMod_Handbrake`, `RaceMod_Slide`, `RaceMod_MoveBeta`; BUGGY.CPP `RaceSpeed`, `MoveBuggy`): held,
  the back wheels lock -- no drive, a brake 0.22 of the pedal's (the Temple's car: about 56 km/h a second), the steering 1.5 times
  as quick -- and the way the car goes (`s_slideBeta`) parts from the way it points: it follows the heading round only slowly (the
  gap shrinking by e^-1.5 a second), never more than 56 degrees behind it, and the car loses speed sliding sideways (0.6 of it a
  second times the sine of the slide's angle). Let go, the grip comes back over 0.45 s (to 9 a second) and the way it goes swings
  round to the heading; the slide is over once they are within half a degree (3.5 with the grip all back and the steering still
  on). A slide starts only above 10 km/h; not on the grid, in the air, over a jump or round a loop, skidding on oil or on the super
  jet-pack. The car moves each frame by its speed as ever -- only the direction is the slide's -- so the rail and the walls hold a
  sliding car as any other; while they hold it back (its last move short of 0.6 of the one meant) the grip comes back 5 a second
  quicker, so it scrapes along rather than pressing on. Across a cube's edge the slide goes on.
- **Tuning**: the race file's `handbrake=<on 0|1> [brake] [steer] [grip] [slip]` (defaults `1 0.22 1.5 1.5 640`; slip in 4096ths of
  a turn); `handbrake=0` turns it off for a track.
- **The camera**: the rail camera keeps behind the way the car goes while it slides (it chose its side of the car by the heading:
  swung square across the road, the car had the eye flip round in front of it).
- **Heard and seen**: a tyre squeal looped while it slides, louder the further sideways and the faster, a little higher the faster
  (`SkidSound`: `RACESOUNDS/SKID.WAV` if the game folder has one, else made by the engine -- a wavering 1,180 Hz tone over hiss, half a
  second looped); a puff of the game's smoke from a back wheel every 90 ms while it slides past 18 degrees above 25 km/h.
- **The Controls page** (RACEGAME.CPP): "Shift -- Handbrake" and "Y button -- Handbrake"; P and Esc share a row as a pad's Back and
  Start do, and the lines are as far apart as the rows leave room for (23 on the Temple's page, as before).
- **The log**: `[racemod] handbrake on at <km/h>`, every 0.2 s while it slides `handbrake t <ms> key heading way slip speed grip
  [held back] at x,y,z`, `let go`, `the slide over after <s> (<why>) -- <from> to <to> km/h, <most> degrees sideways at most`.

**Measured** (the Temple, `--fixed-dt 20`): never pressed, nothing changes -- a time trial lap 58.12 s and a seeded race lap 70.26 s
(the opponents' laps too), every cartrace and racemod line the same as the engine before. The test pilot through the U-turn hairpin
with Shift held 15 / 25 / 40 frames: slides of 33 / 53 / 56 degrees, 109 km/h down to 80 / 69 / 55, the lap 0.2 to 0.7 s slower than
the pilot's own line (it brakes there anyway) and the car out of the bend on the road each time. Swung square across the straight with
the handbrake held, the car slid sideways into the rail and was held there on the deck, no rescue.

## The ghost car (2026-10-24)

The user: "Add a function that records a "ghost" car for a track ... either so a user can watch their lap back, compete against their
best lap, or share their best lap with somebody else." They suggested logging the keys, since a time trial has no power-ups.

- **Why not the keys**: the engine moves everything by the real time each frame took (`TimerRefHR`, a variable step), so the same
  keys drive a different lap at a different frame rate. Two headless runs of the same input already drift by seconds. The ghost
  keeps where the car was instead: exact on any PC, at any frame rate.
- **Recording** (RACEMOD.CPP, the ghost section before `RaceMod_Frame`): every frame of a time trial's lap, after the frame's moves
  (`GhostRecord`, at most every 15 ms), one sample: the lap's clock, the car's place in island units, its angles as drawn, its body
  and animation, the island. With `drive_as=` it is the car drawn in Twinsen's place, pitched as `DrawDriveAs` pitches it. The lap's
  clock at each checkpoint goes with it (`GhostSplit`).
- **Kept** (`GhostLapDone`, from `RaceMod_Lap` and from `SprintOver` for a sprint's run): a lap quicker than the player's own best ghost
  is written to `<user folder>\GHOSTS\<track>.ghost`. The track is `ghost_name=` (the race game writes its track's name), else the race
  file's name. A lap quicker than the ghost raced so far is raced from the next lap on.
- **The file**: text. A head (`ghost=1`, `track=`, `lap=` ms, `car=`, `date=`, `splits=`, `points=`), then one line a sample:
  `t x y z alpha beta gamma body anim island`. A 57 s Temple lap is about 3,400 lines, 155 KB. Written to `.new`, then renamed.
- **Raced** (`ghost=2` the track's fastest file in `GHOSTS`, any name; `1` the player's own; `0` none): `DrawGhost` in `RaceMod_Draw`,
  after `DrawDriveAs`. It is interpolated between samples (not across a jump of more than 4096 units: a rescue, a teleporter). It is
  drawn tested against the scene's depth without writing it (`FILL_POLY_FOG_NZW`), into a screen copied just before. Every pixel it
  changed is mixed with the one under it (`GhostMix`: 55 % of the ghost's colour, tinted 30 % to pale blue, the palette's nearest,
  made as needed and again when the palette changes). The display's line over Best time shows the ghost's lap. For 3 s after a
  checkpoint or the line it shows the gap in words, "0.44 ahead" or "1.37 behind": the font has no `+`.
- **Played back** (`ghost_watch=<file>`, `GhostWatch`): Twinsen's car hidden, the ghost drawn solid where it is, the car put there
  every frame so the camera follows. No laps counted, no grid, no rescues, no keys but the menu's. Past the cube's edge, the car goes
  into that cube's scene as a carried jump's car does (`FrogCarry`, now with a `why`). After the lap's end it rests 2.5 s and goes
  round again, back to the start's scene if the run ended elsewhere (a sprint).
- **The race game** (RACEGAME.CPP): `StartRace` writes `ghost_name=`, `ghost_car=` and `ghost=` for a time trial. Best laps shows the
  track's fastest ghost ("Your ghost car" / "Shared ghost car" and its time): Enter plays it back (`StartRace` with `ghost_watch=`),
  G goes round the fastest / your best lap / none (`ghost=` in `RACEGAME_SETTINGS.TXT`), O opens the folder (`SDL_OpenURL`, not under
  the control socket). The page moved up and its car names are cut to its width: "Twinsen's Halloween buggy" ran past the edge.
- **Tested** (an LBRA install from this build, `E:\dump\TEMP\lbra\ghost_inst`, driven by `--listen`): the Temple with Twinsen's
  buggy. Lap 1 (57.67 s) was kept; the next laps raced it, the ghost seen ahead after a stop, the splits shown; lap 5 (57.16 s)
  replaced it. Otringal with a character car (`drive_as=`), three scenes and two carried jumps a lap: kept, raced, played back
  through its scenes. Polar Island's sprint: kept at the finish (52.64 s), played back through 20 scene changes and round again
  from the start's scene. A copy renamed with a faster time showed as "Shared ghost car" and was played back.

**Smoothing the playback, and no flashes** (the same evening -- the user: "the car position seems to move very slightly backwards and
forwards, and appears a little jerky and we also have occasional screen flashing"):

- **The clocks**: the engine simulates in 16 ms steps (`FixedTimestep`, PERSO.CPP's sub-step loop) while a 60 Hz frame is 16.7 ms, so
  a frame runs one step, and every 0.4 s two. The samples were stamped with the frame's clock (`RaceMod_Frame`), a millisecond or two
  off the step that moved the car, and every 0.4 s one went twice as far in the same time. And the replay put the car where the ghost
  was in the frame's time, while the camera follows it in the steps: the camera lagged and caught up, the car slid back and forth.
  Both now happen in the step itself (`RaceMod_EdgeCross`, after the hero's move, before the camera): `GhostRecord` stamps each
  sample with the step's clock (a lap's samples now exactly 16 ms apart), and `GhostWatch` puts the car there, where `DrawGhost` then
  draws it. A ghost raced is drawn at the last step's clock (`SimNow`: `LastSimRefHR`), as the car on the screen is.
- **The first files** (`ghost=1`): their times are rebuilt as they load (`GhostRetime`). Each sample counts as a whole number of 16 ms
  steps (its distance against its neighbours' typical step), then the times are nudged towards the old stamps over half a second
  either side. On the user's Emerald Moon ghost, the samples whose speed jumped more than 10 % went from 264 to 50 of 1,159. New files
  are `ghost=2`.
- **The cube changes**: a change of cube puts the car half a cell on, so the two sides' places don't join. Slid between them, the
  played-back car kept crossing back and forth between two scenes, and the engine's own zones and edge hold changed scenes too.
  Each change was a flash. Now a sample is never slid across a change of cube. While a ghost plays back, the only scene changes are
  its own, at its samples (zones refused: `RaceMod_CubeChangeAllowed`). Its car is never rescued off the island's edge
  (`RaceMod_OffIsland`) and never stopped by a hit (`RaceMod_CarHit`). The user's Emerald Moon replay had all three: three "back on
  the road" rescues, an edge hold and a hit. Played back now: exactly the lap's 8 scene changes a lap, nothing else.
- **What is left**: the engine's own step against the frame, one frame in 25 at 60 Hz moving the car two steps, as it does when you
  drive.

**The game's icon** (LBRA installer, `GameIcon.cs`): the installed `LBRA.exe` had the engine's icon, so its window, the taskbar and
Explorer showed it. The installer now replaces the exe's icon groups with `pumpkin.ico`'s pictures through Windows' resource update,
before it marks the exe a windowed program. SDL takes the window's icon from the exe's first icon.

**Round the loops, and the exe's icon in Explorer** (2026-10-25 -- the user: "On Emerald Moon the car appears to be going around the
outside of the loop instead of the inside"; "the exe icon for LBRA still retains the engine icon"):

- **The car Twinsen drives as** (`drive_as=`) was drawn pitched with the road's climb alone (`s_drivePitch`, no steeper than
  `RACE_DRIVE_SLOPE_MOST`), so round a loop it stayed nearly upright: roof to the ring, wheels to the sky. Twinsen's own buggy was
  posed right, because `RaceMod_Loop`'s `CarPose` turns him round the ring. `DriveAsPose` now gives the car drawn in his place his
  pose while a loop has him (`s_loopState`), and the slope's pitch otherwise. `DrawDriveAs` and the ghost's recording both use it, so
  ghosts recorded from now on go round the inside too. A ghost recorded before this keeps the upright pose in its samples.
- **The ghost's angles** are not slid between two samples where one turns over a quarter of a turn. Over a loop's top the three
  angles swap round, and slid each on its own the car tumbled for a frame.
- **Tested**: Emerald Moon with the vampire's coffin. Live, the car rides the inside of loop 1, wheels on the ring. A new ghost lap
  played back rides the inside of loop 2.
- **The icon**: the installed exe already held only the pumpkin's five pictures, 16 to 256 pixels. Explorer was showing the icon it
  had cached for that path before the update. The installer (`GameIcon.Refresh`) now tells the shell the exe changed and empties its
  icon cache (`SHChangeNotify`: `SHCNE_UPDATEITEM`, `SHCNE_ASSOCCHANGED`).

**A ghost that drowned** (2026-10-25 -- the user: "One of the ghosts I recorded yesterday isn't playing back correctly, the ghost car was
dying. I think it was the Citabau track"):

- **The cause**: Citadel Island's town's race file has no `cube_scene=` lines, and they were the only cube-to-scene table the playback
  had. Its lap changes cube 15 times. While a ghost plays back, the engine's own zones are shut (`RaceMod_CubeChangeAllowed`), so the
  car stayed at the edge of its cube. 448 ms in, the ghost goes into cube (7,10) along the shore. Twinsen, held at the edge over the
  sea, drowned: the life gauge, then the death's restore, the game clock wound back 5 s. `RaceMod_Rescue` didn't save him, because
  with no safe place on record it lets the engine have him.
- **The fixes**:
  - `CubeScene` is a cube's scene: `cube_scene=` first, else the island's, read once an island from SCENE.HQR's scene heads (island,
    cube, kind; the nearest in number where two outside scenes share a cube). `LoadSceneCubeXY` only answers for the cubes round the
    current one. `FrogCarry` uses it, so the frog's throw does too.
  - While a ghost plays back, `RaceMod_Rescue` holds the car, so Twinsen is never drowned or put back on the road.
- **Tested**: the user's own Citadel town ghost (the flying carpet, 31.81 s) in the sandbox. "Island 0: the scenes of 9 of its cubes",
  then all 15 of the lap's changes (scenes 46, 49, 42, 48, 47, 45, 47, 50, ...) and round again. No clock going back, no falling, no
  rescue; the carpet on the road all the way.

## Ghosts to beat: Easy, Medium and Hard on every track (2026-10-25)

The user: "Let's generate easy medium and hard ghosts to beat for each track. Hard tracks should make use of any shortcuts and be
optimised for best lines using the best car for the specific track."

- **The game side**:
  - The game folder's `ghosts\` holds the game's own ghosts, each with `level=Easy|Medium|Hard` in its head.
  - `RaceMod_GhostFind` modes: 1 own, 2 fastest of all (own, shared, game's), 3/4/5 the game's Easy/Medium/Hard, 6 the fastest of the
    player's and the shared ones. `ghost=` in the race file takes 0-5.
  - Racing a game's ghost: the display names it ("Hard ghost 0:46.28"), and a lap under it says "You beat the Hard ghost!" once a trial.
    A level ghost stays raced, rather than giving way to the player's own quicker lap.
  - The lap's time against the ghost stays on the display its 3 s: a checkpoint just past the line had covered it at once.
  - Best laps: Up, Down go through the player's/shared ghost and the three levels; Enter plays the one shown back. G goes round the
    fastest, your best lap, Easy, Medium, Hard, none.
- **The shortcuts** (`autoshort 1`, the console; `BuildShortLine`): a line for the autodrive alone. The lap's counting keeps the racing
  line. Spliced in:
  - each raised-road branch under 0.9 of the way round it saves;
  - each teleporter within 12 cells of the line that comes out further round the lap, turned in towards from as far back as it is off;
  - each rail opening, over the edge at its middle and on along the road below.

  On the Temple of Bú the hidden passage and the spiral drop are both taken, every lap counted: 46.28 s against 54.53 s on the racing
  line. Wannies Island's branch is taken, but its lap is slower there (the frog). Island CX's branch is a pit lane, longer, and is left.
  The palace's teleporters save nothing.
- **The cars**: `raceplan file` gives each car's star profile's perfect lap on the track loaded (top x(1+0.08 d), pull x(1+0.5 d)(1+0.08 d),
  steering x(1+0.2 d), d = (stars - 2.5) / 2.5). Hard takes the quickest unlocked car: mostly the first quetch child's kart (2.5/1/4) or
  the undergas bulldozer (1/1.5/5). The Temple: the Tralu's cave. Polar Island: the Dark Monk's car. Island CX: the firefly tart.
  The Francos: the nomad grobo's truck. The palace: Mr. Kurtz's laboratory.
- **Made by** `E:\dump\TEMP\lbralltracks\gen.ps1` on an LBRA install (the menus driven through `--listen`):
  - Easy: Twinsen's Halloween buggy at 70 % of the line's speed, its laps 1-2.
  - Medium: the same car at 85 %, lap 3.
  - Hard: the best car on the shortcut line at 100 %, then 104/100/108/102 % with the look-ahead varied, its fastest lap.
  - The pace's effect isn't linear: on Otringal 60 % gave 46 s, 78-92 % all about 32-34 s.
  - Wannies Island: a trial per level of 6 laps, best kept, labelled by time.
- **Times** (Easy / Medium / Hard, s): Temple 80.9/65.6/46.3, Polar 70.9/58.9/52.9, Citadel storm 14.8/12.6/10.2, Citadel town
  52.6/43.6/36.1, Desert 65.4/54.2/46.7, Emerald 33.9/28.2/22.9, Otringal 38.4/32.6/25.9, lava lake 17.2/14.6/11.3, Francos
  45.0/38.5/31.7, Elevator 18.1/15.2/11.3, Wannies 11.7/11.6/11.4, Mosquibees 23.7/19.8/16.5, Volcano 34.0/28.5/23.0, CX 30.9/23.0/18.3,
  palace 51.3/42.4/37.2, statue 24.0/20.0/15.6.
- **Shipped**: the LBRA installer embeds `Game\ghosts\*.ghost` as `game/ghosts/...`, so they go into the install's `ghosts\`.
- **Found on the way**: on Wannies Island the race game's menu during or after a race can leave the game in hall 123 (scene 123, island
  6) with no menu. The menu's resume reloads the current save there, and a car driven on past the finish goes in. Not fixed yet.

**Wannies Island: the race sent to the hall** (2026-10-25 -- the user: "Look into it properly please"):

- **What happened**: on Wannies Island, Esc during a race (or the race over going back to the menu) left the game in hall 123 with no
  menu, the race gone.
- **Why**:
  - Wannies Island is made on LBA2's scenes 96-99. Twinsen's own life script in its start scene, 98, begins the arrival's cinema:
    `CINEMA_MODE 123`, a cinema whose destination is the hall.
  - The race takes the scene over, so the cinema never runs its course. `CinemaMode` stayed 123 all race; no bands showed.
  - PERSO.CPP's menu key, while a cinema with a destination is on, calls `ResetCinemaMode`: "skip the cinema", `NumCube = -1;
    NewCube = CinemaMode`. That made the jump to the hall, which cubetrace showed as `-1 -> 123`. The race's own way back to the menu
    (`RaceMod_WantsMenu`) goes through the same key.
  - Found with `cubetrace`, new logs of the menu's decisions (GAMEMENU.CPP `[menu]`), Twinsen's life while racing (`[racemod]
    Twinsen's life a -> b`; it never changed: no death) and of every script cinema (GERELIFE.CPP `[life] ... cinema mode`).
- **The fix**: GERELIFE.CPP `LM_CINEMA_MODE` asks `RaceMod_NoCinema`. While a race is on -- the car on a track with a start line, or
  `start_in_car=` -- a script's cinema is left out and said.
- **Tested**: Wannies Island's trial, "cinema mode 123 left out (a race)". Esc during lap 2 opens the menu; Resume goes on with the race
  in its scene; the race over goes back to the menu over the track.
- **Not the cause** (tried first, reverted): the zones into a scene inside, and holding the race's way back to the menu while a cinema
  was on.

## Racing a ghost: its own page, levels unlocked in turn; Best laps simplified (2026-10-25)

The user: "Let's change this interface because it's terrible ... first have the user select the track, then Time Trial, then add a
screen so they can choose to race a ghost ... force them to beat easy to unlock medium, and to beat medium to unlock hard ... simplify
the Best laps screen ... top 5 times ... a watch section ... Remove "O to open the ghosts folder"".

- **Race a ghost** (RACEGAME.CPP `GhostPage`, after a Time trial is chosen, before the car page): Back, No ghost, Your best lap, Easy,
  Medium, Hard (IDs `RG_ID_GHOST_NONE` 2360 .. `_HARD` 2364), each with its lap. A missing ghost is greyed; a locked level reads "Medium
  ghost: beat Easy" and is greyed. The rows sit lower than other pages' (centre 308, header at 92) so the header clears the menu
  picture's second title line. The choice is saved as `ghost=0|1|3|4|5`; Esc on the car page comes back here.
- **Unlocks**: `ghost_beaten=<n>|<track>` lines in `RACEGAME_SETTINGS.TXT` (1 Easy beaten, 2 Medium, 3 Hard). RACEMOD's GhostLapDone
  calls `RaceGame_GhostBeaten(level)` (COMMON.H), which keeps it and returns the level it opens: "You beat the Easy ghost! Medium
  unlocked".
- **Your best lap** is the GhostFind mode 6 (the player's own or a shared one, the quicker): the race file gets `ghost=6` for it, and
  RACEMOD swaps in a new best lap for mode 6 as for 1 and 2. The row reads **Shared ghost** when the quicker one is shared, and **Your
  recorded lap** when Best laps holds a faster lap with no ghost (laps from before ghosts were kept).
- **Best laps** (`TimesPage`): three rows -- the track (Left/Right: tracks), Your best lap, Ghost: Easy/Medium/Hard (Left/Right go round
  the levels there are) -- the selected one framed and orange; the five fastest laps between the track and the Watch header. Enter
  watches; any level can be watched, locked or not. G and O are gone, and with them the ghost-order cycling.
- **Tested** (sandbox, Otringal): the page with Medium/Hard locked; Easy raced with the autodrive, "the Easy ghost beaten, Medium open",
  `ghost_beaten=1|Otringal`, Medium open on the page after; Best laps' rows, Left/Right round the levels, the Easy ghost watched; the
  Temple's page (its own unlocks); a shared ghost faster than the player's shown and raced.

## The next fixes: start camera, menu row, harder ghosts' times, a time trial's laps (2026-10-25)

- **Emerald Moon's start camera** (the user: "up in the air and at the side"): only after another run had been left through the menu
  while the race camera's hook had the view (round a loop, or easing back from it). `FollowCamOwnEyeAt` and the ease back
  (`FollowCamLeave`) are places in the cube the car was in, and `FollowCamReapplyLift` put the view back on them every frame, so the
  new race opened looking from the old run's spot, the car out of the picture, until it moved. New `FollowCamDropOwnEye`
  (FOLLOWCAM.CPP/H) forgets both; RACEMOD calls it in `RaceMod_NewGameScene` and when a race is begun again from the grid.
  `FollowCamResetToView` now clears the leave too. Repro script: `E:\dump\TEMP\lbra\alltracks\emcam.ps1 [-After 2]`.
- **The main menu's row**: `RaceGame_Menu` builds its rows each time and opened on Specials; now on the row chosen last (Track select,
  Specials, Best laps) unless a race is paused (Resume first). The sub-pages already kept theirs.
- **Harder ghosts' times**: GhostLapDone looks up the Medium and Hard ghosts' laps (`RaceMod_GhostFind` 4, 5) when a lap beats the
  raced ghost, and passes the hardest one beaten; `s_ghostBeaten` is now that level + 1, so a later lap beating a harder time is
  said too. `RaceGame_GhostBeaten` returns what opens ("Medium", "Hard", "Medium and Hard").
- **A time trial's laps**: `LapsPage` (IDs 2365..2369) after Time trial, before the ghost page: 1, 3, 5, 10, Endless.
  `trial_laps=` kept; the race file gets `race_laps=` from it (0 endless: RACEMOD counts laps with no end, "Lap 7"). Sprints and a
  ghost played back keep the track's own. An endless trial's fastest lap goes into Best laps as it is driven
  (`RaceGame_TrialBest`, COMMON.H), replacing the one it put there before (kept with laps 0).
- **Tested** in the sandbox: the start camera after leaving round a loop and while easing out of one; an endless trial against Easy
  (lap 1 beat Medium's time: "You beat Easy and Medium! Medium and Hard unlocked", `ghost_beaten=2`; Best laps' entry replaced as
  the laps got faster; "Lap 5" on the display); 1 lap chosen on the page, the trial over after it, the menu back on Track select.

## Extreme ghosts: the test pilot's racing line and handbrake slides (2026-10-25)

The user: "All the hard ghosts I've raced so far have seemed quite easy to beat, let's add an extra difficulty ghost called "Extreme"
that is almost impossible to beat. Make sure we're using the best car for the track, taking all the optimum lines, and sliding around
corners where that's quicker".

- **The level**: GhostFind mode 7 (`level=Extreme` in a game ghost's head; mode 6 was taken by own + shared). RACEGAME: the ghost page's
  seventh row (`RG_ID_GHOST_EXTREME` 2370, rows centred at 290 as the in-race main menu's, the header in white over the picture's
  title -- a shaded strip there never showed over the menu picture), opened by beating Hard (`ghost_beaten=` up to 4); Best laps'
  ghost line goes round four levels; `RaceGame_GhostBeaten` returns every level a lap opens ("Hard and Extreme"). GhostLapDone checks
  every harder level, Extreme too.
- **autoslide** `<0|1> [on degrees] [plan]`: the pilot's speeds planned with the steering `plan` times as quick (`s_autoSteerPlan`, in
  PlayerPlan and FinishAutoLine), and the handbrake (I_INVENTORY from RaceMod_AutoInput) held while the way the car goes (`s_slideBeta`
  while sliding) is more than `on` degrees off the bearing to the line ahead, let go within a third of that. A held slide turns the
  car's path RACE_HB_STEER (1.5) times as fast once the slip is full, with no drive and almost no scrub: a bend the steering limits
  is taken half as fast again. Emerald Moon, the bulldozer: 23.24 s a lap without, 21.1 with.
- **autoline** `<0|1> [margin cells]`: the pilot's line (a copy of the racing line when autoshort takes no shortcut) moved across the
  raised road to the least-bending line: each free point only along its normal, towards (-p[i-2k] + 4p[i-k] + 4p[i+k] - p[i+2k]) / 6,
  k = 16, 8, 4, 2, 1, held inside its room on the road found once up front (`RAISED_INSET` + margin from the edge, the same level).
  Points stay off the raised road, in or near a jump's span, within 10 cells of a loop, by teleporters and rail openings, at steps in
  the line. A moved point keeps the racing line's banking (its radius times the racing line's over the plain circle's). Kept only if
  its planned lap is quicker. Emerald: planned 24.75 -> 23.59 s; driven with slides, about 0.2 s a lap. `LBA2_AUTOLINE_DUMP=<file>`
  writes both lines' points.
- **Fixed on the way**: the pilot's own line (FinishAutoLine) planned its speeds before OpponentLoops placed the loops on it, so every
  ring's speed cap and the steering into it went by a loop at the line's start -- an autoshort line on a track with loops lapped 50 %
  slower. Loops are now placed first.
- **Generator** `E:\dump\TEMP\lbra\alltracks\xgen.ps1 [-Only 'a|b'] [-Root <sandbox>] [-Port n]`: the top 3 cars by raceplan (locked
  ones too), each 3 laps of an endless trial on `autoshort 1; autoline 1 0.25; autoslide 1 14 1.35; autodrive 1 5 0 100`, then the
  best car with six variations (slide plan 1.5, on 18 or 10 degrees, look 6, pace 104, no autoline), two laps each. The own ghost keeps
  the fastest lap of the lot; it becomes `<track> - Extreme.ghost`. Two run at once on `ghost_inst` / `ghost_inst2` (ports 27123/4).
  `trycfg.ps1` tries settings by hand.

## Polar Island's mini-map, sprint runs, a missed checkpoint's lap, Restart (2026-10-25)

- **Mini-map** (the user: Polar Island's "only has the raised track section on it and not the part that's ground level"): the map drew
  the raised road whenever a track had one. `MapGuideOverRaised`: when a tenth or more of the guide line's points are off the raised
  road (RaisedAt, 1024 padding) it draws the guide line instead -- Polar: 1330 of 1661 off -- and the raised road's branches beside it.
- **Sprint runs** (the user: "In time trial mode for our two point to point tracks, let's respawn the user back at the start line after
  they complete a lap so they don't have to restart"): the laps page asks **How many runs?** for a sprint (1, 3, 5, 10, endless);
  RACEGAME writes `trial_runs=`. Over the finish with runs to go, `SprintRunAgain` keeps the run (GhostLapDone, RaceGame_TrialBest,
  `s_runsBest` -- the grid clears s_bestLap), holds its time on the display, and RaceMod_Story sends the car back with `RaceFromGrid`
  (the old loss restart, made a function) RACE_RUN_AGAIN_MS later once it isn't flying: Polar's finish is at carried jump 2's lip, and
  sent back at once the jump caught the car again the next frame and carried it into the scene past it, where it stuck. HUD "Run 2/3".
  The last run ends as before (SprintOver, with the runs' best); RaceGame_Finished takes the interim Best laps entry out first.
- **A missed checkpoint in a time trial** (the user: "have the time reset if a user passes the start/finish line after missing a check
  point"): RaceMod_Lap starts the lap again from the line -- clock, checkpoints from the first, ghost recording -- "Missed checkpoint 3:
  the lap starts again". A race keeps its old rule (the lap uncounted until the checkpoint is taken).
- **Restart** (the user: "We should also add in a restart menu option"): RG_ID_RESTART 2371 under Resume in a race of the race
  game's (going on or just over), "Restart the race / the time trial" or "Watch again from the start": StartRace again with the race's
  own setup (`s_raceWatch` kept for a ghost played back). The greyed Story Mode row is left out while it shows: seven rows.
- **Tested** (sandbox 3): Polar 3 runs (52.7 / 52.9 / 52.9 s, the respawn after each, the trial over after the third); Otringal: over the
  line, teleported back onto the grid and over it again -- "Missed checkpoint 3: the lap starts again", checkpoints from 1; Esc in the
  trial, "Restart the time trial", the trial again.

## Extreme ghosts, the second day: Wannies Island and the pilot's lines (2026-10-26)

- **First pass, all tracks** (xgen.ps1): 13 of 16 beat Hard by 3-13 %. The Elevator Platform, Wannies Island and the palace didn't.
  The second pass (`-Second`: the Hard ghost's car and the Extreme ghost's, plain settings at 100-110 % besides slides, seeded with
  the Extreme ghost so only a quicker lap replaces it) took the Elevator Platform to 10.69 s (Hard 11.33).
- **Wannies Island**: Hard's 11.4 s laps were the racing line's; the pilot's line with autoshort took branch 1 (68 cells against 181)
  and lapped in 21-25 s however it was driven -- shorter on the map, far slower on the rollercoaster. Fixed for the generator with
  `-NoShort` (autoshort 0). In the engine:
  - `BuildAutoLine` leaves out, one at a time, each branch whose leaving out plans the lap quicker (`s_shortSkip`; teleporters stay in
    both). The plan didn't see Wannies' case (the racing line planned no quicker), so it changes nothing there, but holds elsewhere.
  - `FinishAutoLine` takes each point's banking from the racing line's nearest point when the line has shortcuts spliced in (point
    counts differ) -- before, a shortcut line planned every banked bend as flat.
  - Tried and taken out again: the pilot slowing to come to the giant frog's mouth while it is open, as the opponents do (learning
    how late it came). Its arrival was erratic (+-2.5 s), and the slowing cost more than the mouth did: the pace hardly matters on
    Wannies (Easy at 60 % lapped 11.71 s, Hard at 100 % 11.43) -- what is quickest is driving on and taking the mouth as it comes.
- On Wannies the slides and the line pulled tight cost time (14.8-16.7 s against the plain racing line's 12.2): the plain settings
  at higher paces are its Extreme.
- Also tried and taken out: the pilot braking as late as it can (only for a slower point ahead): no quicker on the Emerald Moon,
  and on Wannies Island the speed it carried into the carried jumps cost 8-12 s a lap.
- **Wannies Island's lift film** (the user: "the lift cut scene is displaying before every test, is this a bug?"): the start scene's
  script plays ACF ASCENSEU (the lift, ~20 s) before every race and trial there. GERELIFE LM_PLAY_ACF now asks RaceMod_NoCinema
  and leaves it out in a race, as LM_CINEMA_MODE is: "[life] actor 0 of scene 98: film ASCENSEU left out (a race)".
- **The high leap** (the user: "None of your test laps on the Wannie's race track are taking the shorter jump route, perhaps this is
  quicker?"): branch 2 runs over the line's start, and autoshort left every such branch. `autoshort 1 <branches>` now takes those
  branches and no others (s_shortForce); one over the start turns the pilot's copy of the racing line round to start where the
  branch comes back to it (RotatedLine) and splices it in at the end. Driven: the vampire's coffin 10.85 s (the racing line's best
  11.36 s, the frog's way 21+ s) -- the Extreme ghost. The branch is 71 cells against 65 the way round but skips the climb.

## The Updates page's word kept up to date (2026-10-26)

- The user: the page "never seems to change from 'Checking for a new version...', but did install correctly on exit". The page drew
  the updater's word (Update\status.txt) once, as its menu was entered, and the menu then waited for a key: the check's answer, written
  20 s later, wasn't shown till something was pressed. GAMEMENU `MenuRedrawHook`: with MenuRelayoutReturns set, DoGameMenu asks it while
  it waits and returns MENU_RELAYOUT when it says the page's text changed (the row chosen kept in the menu's first word -- the resize's
  return now keeps it too). RACEGAME `UpdateWordChanged` looks at status.txt twice a second against the time the page drew.

## Ghost sharing: a zip of a player's ghosts, and racing another person's (2026-10-26)

- The user: "update ghost sharing so a user can share a zip file with all of their ghosts in, exported with their name, then update the
  "Race a ghost" screen so they can select a specific person's ghost ... an "Other" option, that then lists all of the installed ghosts
  from other people relevant to that track".
- Ghost files may carry `driver=<name>`. RACEMOD `RaceMod_GhostOthers` lists a track's ghosts in GHOSTS but the player's own, each
  person's quickest (driver=, else the file name after the last " - "), the quickest first; `ghost=8` + `ghost_file=` races that file
  (the display: "Sam  0:27.15"). `RaceMod_GhostFolderSignature`: the folder's count and newest change.
- RACEGAME: the ghost page's eighth row, **Other people's ghosts (n)** (8 rows at centre 284, the canvas's 480 just holding them);
  OthersPage: Back, up to 5 ghosts, Import ghosts..., Share my ghosts...; the choice kept as `ghost_other=`. The page drawn again
  when the GHOSTS folder changes (MenuRedrawHook). "Your best lap" is now the player's own only (GhostFind mode 1, not 6).
- Import and Share are the setup's: RACEUPDATE `RaceUpdate_Tool` starts `Update\LBRA-Setup.exe --export-ghosts|--import-ghosts
  "<GHOSTS>"` (LBRA Installer\GhostShare.cs): Share asks the name (GHOST_NAME.TXT beside GHOSTS) and where to save
  `LBRA ghosts - <name>.zip` (each own ghost as `<track> - <name>.ghost` with driver=, and a README); Import takes zips or .ghost
  files (an OpenFileDialog, or files after the folder on the command line; LBRA_GHOSTS_QUIET=1 for tests) into GHOSTS as
  `<track> - <driver>.ghost`.
- Also: a time trial's best lap goes into Best laps the moment it is over the line (RaceGame_TrialBest for every trial, not only an
  endless one) -- a player's report: leaving a trial before its end lost the lap.

## Time trial run-up, renamed tracks, Track select's scrollbar, the pad's double step (2026-10-26)

- **A run-up in a time trial** (the user: "start further back from the start/finish line to give the users car time to accellerate
  before crossing the start line"): RACEMOD `TrialStartSpot` puts the player's car on the car's guide line 10 cells before the start
  line (the guide line's point nearest the line's segment, within 3 cells; walked back 10 cells down to 2 while it is in the start's
  cube and within 1500 of the line's height), facing along the line. A trial outside the start scene (Polar, the Palace) sends the car
  to that spot as the grid waits. Desert: 10 cells back, the countdown, GO, checkpoint 1 as before.
- **The storm's countdown**: Citadel 1's trial file had no `qualifying=0`, so its trial began as a qualifying lap (PHASE_QUALIFYING)
  and never counted down. A time trial is never a qualifying lap now (`if (s_trial) s_qualifying = 0`).
- **Renamed tracks** (Citadel 1, Citadel 2, Emerald Moon, Celebration 1, The Palace, Celebration 2): RACEGAME.TXT's `track=` line takes
  a fifth field `was=<old name>`; RACEGAME `RenamedTracks` moves the old name's best laps and ghost_beaten to the new, and the
  player's GHOSTS files (`<old>.ghost`, `<old> - X.ghost`) to the new name with their `track=` line rewritten. The setup writes the
  was= field (GameInstall.cs `Renamed`) and clears the shipped ghosts before copying the renamed ones.
- **Track select's scrollbar**: down the list's right edge, its thumb the rows shown out of all of them; a click or a drag on it
  scrolls there, as the mouse wheel does (ConsumeMouseWheelY), the track chosen kept among the rows shown. It replaces the "..." marks.
- **The pad's double step** (a player: "when I navigate the car selection menu with the D-pad or left stick, it skips over 1 car in
  every horizontal step"): the pass a pad way is pushed, MyGetInput hands the pad's own scancode (K_GAMEPAD_BASE up) on in MyKey
  for "press any key"; RACEGAME Nav/Step only looked at the pad with MyKey 0, so the way went unrecorded, Input stepped once, and the
  next pass took the still-held way for a new push and stepped again. Nav/Step follow the pad's way every pass and take a pad
  scancode in MyKey for no key; DoGameMenu's two pad readers (GAMEMENU) too.

## Recording a video from inside the engine: framedump (2026-10-26)

- The user: "Record an interesting video of game footage with clips from all tracks including showing off power ups to be uploaded
  to YouTube". Console `framedump start <video.mp4> [seconds] [fps] [wall]` (FRAMEDUMP.CPP) pipes every presented frame -- the
  engine's own, before the console's overlay; nothing of the screen is read -- to ffmpeg (LBA2_FFMPEG) as an x264 MP4, and the
  effects' mix to `<video.mp4>.wav`. `framedump at <seconds> <command>` runs a console command on that frame of the recording
  (`raceitem 9` at 3 s); `framedump stop` ends it early.
- The game's own time: the dump arms the fixed-dt clock (16 ms a tick, as `rec start` arms it mid-session -- TimerRefHR kept, or a
  race on the grid waited 27 s for its countdown), each frame is placed by that clock (repeated or left out to hold `fps`) and that
  span of the effects mixed with it (AIL Sample_CaptureMix), so picture and sound keep step however slowly ffmpeg takes frames.
  `wall` leaves the clock alone: a menu's loop never runs the tick hook that moves an armed clock.
- LBA2_AUDIO_CAPTURE=1 gives the sound device silence (the effects mixed for the dump only: nothing heard on a shared machine);
  LBA2_NO_ATTRACT=1 keeps the main menu from its idle demo (a test run's menu clock can reach the minute before a script picks).
- The video's scripts (record.ps1, shoot.ps1, menu_shot.ps1, edit.py: 1080p60, captions, LBA2's CD track under the game's sound,
  -14 LUFS) are scratch in E:\dump\TEMP\lbra\video.

## A game ghost's time beaten with any ghost raced (2026-10-26)

- A player: the Island of the Francos' Extreme ghost was "not unlocking when they beat the time". The Extreme ghost is there (30.28 s;
  Hard 31.67 s) and racing the Hard ghost unlocks it (tested: 30.43 s, "You beat the Hard ghost! Extreme unlocked"); but GhostLapDone
  only looked at the game's ghost times while one of them was raced -- a lap under 31.67 s with No ghost, Your best lap or another
  person's ghost opened nothing. Now every time trial lap is held against the track's game ghosts (RaceMod_GhostFind modes 3, 4, 5, 7):
  "You beat the Hard ghost's time! Extreme unlocked" (tested with ghost=0: 31.01 s). With no game ghost raced it speaks only of a level
  not beaten before (RACEGAME `RaceGame_GhostBeatenLevel`), not of a known one every trial.

## The test pilot overtakes: autopass (2026-10-26)

- The user, of the trailer: "lots of places ... where one car is directly behind the one in front instead of trying to over take".
  The pilot drove the opponents' own line, so it sat in their wake. Console `autopass <0|1> [cells]` (RACEMOD `PassOffset`): a car
  within six cells ahead and 1.7 cells either side of the pilot's lane takes the pilot out to the side the other leaves (up to `cells`,
  1.2 by default; the trailer used 1.3), eased 30 units a tick, and back onto its line once it is by. Desert, from 6th on the grid:
  1st in 17 s, the cars side by side through the dunes. For footage and tests only; nothing turns it on in play.

## A sprint's time trial started on the line (2026-10-26)

- A player: Polar Island's time trial "starting on the start line, rather than behind it, so the lap never starts". TrialStartSpot
  walked back along the guide line from the start line, but a sprint's line starts there and PathAt holds a place before its start at
  its first point -- the car went onto the line itself, never crossed it, and the lap never began (Polar Island and The Palace, the two
  sprints). Now a sprint only goes back as far as its line goes, else keeps the track's own start (behind the line). Checked every
  track's time trial to "halfway round lap 1": all 16 start their lap (14 with the run-up, the two sprints from their own start).

## A time trial's run-up doubled; Polar Island's own trial start (2026-10-26)

- The user: "double how far back a car starts for a time trial, and let's set Polar as far back as we can do"; then "The Temple track
  had the same issue as Polar island, so we need to start the car further back with this track too". RACE_TRIAL_RUNUP 10 -> 20 cells
  (TrialStartSpot still walks down to 2 cells where the start line's cube or a drop cuts it short); the Temple of Bú: 20 cells.
- A sprint's racing line begins on its start line, so it has no run-up to walk back along. New race-file key `trial_start=<cube x>
  <cube z> <x> <y> <z> <beta>`: a time trial's own start, used before the run-up. LBRA's installer writes it (GameInstall.cs
  `TrialStarts`) for Polar Island: 8 10 5888 1300 27136 2048 -- the dock straight's last grid row, 20.5 cells behind the line; probed
  with the engine's own screenshots, the road's rounded end is about a cell further and the sea past it.

## Mini-maps, the Mosquibees gantry, Wannies' sky, the Temple's flag marshal (2026-10-26)

- The user: "All tracks appear to be missing the pitlane. Emerald Moon has gaps where the vertical loops are, Otringal has a gap where
  the roulette wheel, let's add in a large circle to match it, Celebration 1 has a stretch of the pitlane missing where the car drives
  on the ground, Let's remove the sky layer for Wannies Island, Mosquibees track has the post of the start/finish line in the middle of
  the pitlane, this needs widening. Celebration 2, the start finish line that's on the ground isn't mapped. The magicians assistant on
  the start line for the Temple track, let's remove her collision so cars can drive through her".
- RACEMOD `MapExtras` draws, in step with the road's edge and asphalt passes: the raised road's strip (the Emerald Moon's and the Temple's
  pit lanes); the new key `map_road=<n> <x z>...` (the build's "pit" road on the ground -- RaceTrackService `MapRoads`, RaceCarEngineFile:
  Citadel 1 and 2, the Desert, Mosquibees); the raised road's wide run by the start line twice as thick (Volcano's and Otringal's pit
  decks); a loop's floorless points bridged (the Emerald Moon's four rings); a ring at the roulette wheel's wall (Otringal); and the guide
  line wherever it runs off the raised road's own floor at its height, a few points on into the road (the Celebration tracks' ground
  straights). MapLine centres a start or finish line where the guide line crosses it (Celebration 2's ground line, 9 cells wide, the
  road two cells off its middle).
- Mosquibees' gantry: the start line's curbs took the pit lane's point nearest the line's middle -- on a short pit lane still moving out,
  further along and nearer the lap -- so a post stood in the pit lane (posts 8.1 cells either side). Now the pit lane's point level with
  the line along the lap: posts 11.3 cells either side, two cells past the pit lane.
- `sky=0` (RACEMOD `RaceMod_NoSky`, EXTFUNC's two DrawSkySeaZBuf calls): no sky layer; LBRA's installer writes it for Wannies Island,
  whose road climbs to 25,000, through the sheet (GameInstall.cs `NoSky`).
- The Temple's flag marshal: RACE_MARSHAL_SOLID 0 -- MarshalPost and MarshalKeepOff no longer put cars out round her (the opponents
  still keep their grid columns past her).
- Then (the user: "We need to smooth out the mini-map for Emerald moon, it's a bit jagged in places. Where we've added the jump section
  in the line is totally offset. The pitlane isn't well smoothed in to the rest of the track"): the racing line cuts the road's corners,
  so drawing it over the raised road's floorless runs offset the dish jump and jagged the loops. Now `MapFloor`/`MapFillFind`: a run of
  floorless raised points under 60 cells long with floor both ends, on one stretch, is drawn by the road's own points (the jump, the
  loops' sideways steps); the guide line only where no raised point of any kind is within three cells at its height (true ground
  stretches); the loop bridging dropped. The raised road's strip eased onto the road's line over 5 cells at each end (a smoothstep).

## Best laps' page 17, the sum of best played back, the picture; restart in qualifying; Citadel 2's checkpoint 8; the camera in a car (2026-10-27)

- The user: "Under "Best Laps" Let's add a new page in at the end of loop so it's page 17 that's a list of each track name, along with a
  users best time, then at the bottom show the total time to complete all the laps. Also add a button in that generates an image to the
  clipboard in the format "Track name", "Time", "Car used", along with a sum of best at the bottom of the image"; "If a user selects
  their sum of best, then play back all track ghosts in order"; "When a user goes through the best laps page, let's have the ghost time
  shown to be the time of the highest ghost they've unlocked".
- RACEGAME TimesPage: after the tracks shown (with two or more) the page "All tracks" -- each track's best lap (SmallText: the font drawn
  at two-thirds), the sum of best (SumOfBest; "(n without a lap)"), Copy as an image. `AllBestPicture` draws a card -- the player's name
  from GHOST_NAME.TXT, Track name / Time / Car used, the sum -- as wide as the screen allows (up to 800), takes it, saves "Best laps.png"
  beside the settings (SavePNG reads rows a screen's width apart) and puts it on the clipboard: RACEUPDATE `RaceUpdate_ClipboardImage`,
  CF_DIB and the PNG as "PNG" (LBA2_NO_CLIPBOARD=1 for tests: the clipboard is the person's at the machine).
- Enter on the sum of best: `WatchListStart` plays the player's best lap of every track in turn. RACEMOD GhostWatch asks
  `RaceGame_WatchDone` at the lap's end; non-zero sets s_menuAt, and RaceGame_Menu starts the next track's straight away (no menu shown);
  choosing anything but Resume from the menu ends the list. Tested: Temple, Polar Island, Citadel 1 in turn.
- A track's page shows the ghost of the hardest level unlocked there (ghost_beaten: Easy, then Medium, Hard, Extreme).
- Restart during a qualifying lap ("it starts the race with Twinsen first on the grid"): the qualifying choice went into the race file
  once (s_qualifyNow, then -1); a restart wrote none and the race file's own put Twinsen on pole. `s_raceQualify` keeps it and Restart
  uses it again. Tested: Desert, restarted in the qualifying lap -- "a qualifying lap first" again.
- "Extend checkpoint 8 on Citabau further into the inside of the corner": citadel_track_plan.json `checkpointTweaks`
  [{number 8, right 4}] -- a right-hand corner, the line's right end 4.7 cells from the racing line; now 10.5 cells right of the
  road's middle (was 6.5).
- "Let's add a camera option for Story mode ... Auto when in a car, and classic when out": the Options' camera cycles Classic -> Auto ->
  Auto camera in a car -> Classic (MENU_LABEL_AUTO_CAMERA_IN_CAR, six languages; cfg AutoCameraInCar). PERSO.CPP's main loop sets
  FollowCamera from Comportement == C_BUGGY when it is on (FollowCamResetToView on the way in; "[camera] ..." in the log).

## LBA1's digger on Polar Island, and the grobo with its key (2026-10-27)

- The user: "Let's pull the digger vehicle from LBA1 into LBA2, this vehicle is featured on Polar Island and Twinsen rides in it, let's
  give it the same stats as Twinsen's buggy, but make the acceleration slightly worse"; "This car should then be used in Polar Island in
  story mode instead of Twinsen's car"; "We also need to pull across the grobo character that gives Twinsen the key to the tractor into
  Polar Island near the start line. LBA2 already has grobo's so we could base it on one of those and change the clothing".
- The digger: LBA1's BODY.HQR 130 ("Bulldozer", the Tractor entity 80's body; LBA1 and LBA2 bodies share one scale and facing --
  Twinsen measures the same in both). RaceTrackCharacterCars.BuildDigger puts it as it stands on the racer's hull bone, each colour
  moved to the LBA2 ramp nearest its own ramp (Lba1ToLba2Colours: bank to bank, so a lit polygon runs a ramp of its hue; LBA1's gold
  has no match in LBA2 and comes out ochre), the wheels' bones a point each, Twinsen in the cab behind the windscreen. Race car 225 of
  RACECARS.JSON, the racer entity's body 69 (InstallDigger, after the monsters'), LBRA's car grid at 2 / 2.5 / 2.5 (the Halloween
  buggy's 2.5 / 2.5 / 2.5, slower away).
- Outside a race: ISLANDCARS.TXT ("island_car=<island> <body> <anim> <accel %>", RaceTrackCharacterCars.SetIslandCar; the race build
  and Tools > Polar Island write it). RACEMOD IslandCarFrame / IslandCarDraw (RaceMod_Frame and RaceMod_Draw with no race on): on that
  island Twinsen and his car hidden -- him driving, or it parked (NUM_BUGGY) -- and the body drawn in their place (DrawDriveAsOn, the
  racer's animation); BUGGY.CPP's acceleration times RaceMod_StoryAccel (85 %).
- The grobo (Terrain/Polar/PolarGrobo.cs): LBA1's scene 109 actor 6 (entity 11, BODY.HQR 17, "Grobo: Green vest") says LBA1 texts 12
  and 11 and gives the key; Twinsen says 10 without it. Here LBA2's baggage-claim grobo (entity 33, body 54) with his teal top and its
  red trim moved to the green ramp (the entity's body 1), on the dock four cells and more from Twinsen's start, on the far side from the
  car; spoken to, he says texts 20 and 21 (the island's text file, LBA1's lines in its five languages and a Portuguese one) and sets game
  variable 230. Every scene's copy of the car's script (the Desert buggy's) asks for it: Action beside the car without it, Twinsen says
  text 22 (message_obj). Tested headless: no key, he stays out; the grobo, then the car, he drives.

## Polar Island: nothing hanging over the sea, and sea past its last cubes (2026-10-27)

- The user: "Polar island has a lot of bits floating in the air, let's either remove them, or work out what they should be attached to";
  "The start of Polar island where the track starts in-game connects to another scene without a water segment, are we able to resolve
  this so the illusion isn't broken?".
- What hung: LBA1's sea is a layer of water bricks (layer 1) and what stands on it -- the dock's posts, the rocks' stacks, the crane's
  feet -- stands on its top, 512 up on the island; the engine's sea is at 0. And at the island's edges LBA1's grids have nothing at all
  under the quays, decks and pillars -- its camera never showed beneath them, the void was black. PolarObjects now carries each object
  column's lowest brick on down to the sea where it is over sea (no land column, or water's): on the water, or over nothing up to layer 11
  (1,327 cells). Not over land, not on LBA1's invisible brick 222 (collision a deck rests on), not the rocky peak's heights.
- The race build left one more: a slab of the dock crane in cube (8, 8), 2,080 over the road, its structure's other pieces cleared from
  the road and it bigger than ClearRuins' small parts (16 cells to 12). ClearRuins now takes what is left of a structure the road ran
  through hanging -- its underside more than AdriftBy over the ground under it, touching nothing left of the structure -- however big
  (and what rested on it after it). On the other fifteen tracks it changes nothing (setup.log's ruin counts the same).
- The sea: POLAR.ILE's cubes were 5..9 x 4..10, the land's; past them the engine draws no sea, and the race's start, in cube (8, 10), looked
  out at the edge of the world. They are now 4..10 x 3..11 (PolarTerrain.CubeX0 / CubeZ0 / CubesX / CubesZ): a ring of sea cubes all round,
  63 cubes (the engine's MAX_CUBE is 255), no scenes in them.
- `ScriptRoundTrip floaters <ILE> [gap] [strict] [near x z r]` lists decors held up by nothing (resting on, or fixed beside within a quarter
  of a cell, something held up -- spread till nothing changes; "strict": resting on only); `floaters <ILE> strip <out> <cx> <cz> [x0 x1 z0
  z1 | body n]` writes a copy without some of a cube's decors, to find which one a picture shows. Polar's race build had 21 such decors,
  now 4 (small gaps, 450-770, over the hillsides' slopes, away from the road).

## Polar Island: the grobo through the race build; the backs and insides of buildings (2026-10-27)

- The user: "I just tried to test Polar Island in the editor, and there's no grobo visible for Twinsen to talk to for the key. Let's also
  improve the decor, for example some of our buildings have no back to them. We should check everything".
- The grobo: the race build's actor clearing (RaceTrackScenes, RemoveActors) took him out of scene 251 with every other actor. Polar
  Island's StoryEntities now name his entity (33): kept, and moved off the road if he were on it. And a folder whose originals
  (*.before-racetrack) have the island from before him -- the user's editor folder -- gets it made again: PolarInOriginals now also asks
  for this version's sea ring (cube (CubeX0, CubeZ0)) and the grobo in a scene; otherwise the build puts the folder back and adds the
  island again (EnsurePolar). Tested headless in an LBRA install: Action beside him sets game variable 230.
- Floors LBA1 never drew: under a building's roof LBA1's grids often have no brick at all (its camera never looked down there), and the
  island showed the sea there, a pool inside the walls. PolarTerrain.Columns gives such a place (no brick, ground or water all round
  within 10 cells along x and z, land on two sides at least) the lowest of that land's ground, and never over what stands there.
  Shut-in water went from 240 cells to 8.
- Insides: over such a floor, up to the roof (10 layers at most), each cell takes the nearest wall's brick along x or z (PolarObjects).
- Backs: an empty cell in a run of at most 2 along x or z, over ground, closed in at both ends -- one a wall brick standing end-on to the
  run, the other a wall brick or ground -- takes the nearer wall's brick. Not over water, not within 2 cells of LBA1's car tracks, not
  where LBA1 has any brick, even an invisible one.
- "Wall brick": whole footprint and both sides at least 80 % drawn (a fence's slats, posts and specks of a few pixels are not -- a
  4 x 7-pixel speck has a whole footprint, and filling with it drew a black slab by a tank on the cliff).
- Checked with `ScriptRoundTrip polarview` from four sides on every land cube, before and after (all 21); the three white sheds (cubes
  (7,4), (7,7), (7,9)) are closed. 604 inside cells, 93 back cells, 234 cells down to the sea.
- Still no grobo after the user rebuilt in the editor: StoryEntities are kept only with the build's story on, and the race track window
  turns the story on for Citadel Island only (LBRA's installer leaves it on, which is why its test passed). He is now in KeptEntities,
  kept by every build, story or not. `ScriptRoundTrip buildhere` now sets the story as the window does, so it builds what the editor builds
  (the island's name as RaceTrackIsland has it: "Polar Island"; an unknown name builds the Desert's).

## Polar Island's story start: the grobo behind the grid, his grey face, the digger parked and driven; mushrooms over a cube's edge (2026-10-27)

- The user: "The grobo on Polar is too far away from the start/finish line, let's have him spawn in behind the cars closer to Twinsen,
  the grobo's face colour is also wrong, it's black when it should be grey. Twinsen's buggy is supposed to be swapped out for the tractor
  that we've made for Polar Island when in story mode"; "sometimes Funfrock's car vanishes from view and then comes back, the same with
  some of the mushrooms not visible until the last second".
- Story mode is the dream: a new game starts Twinsen on foot behind his car on the grid of scene 252.
  - The grobo: RaceTrackIsland.StartPerson (entity 33) -- the race build takes every scene's copy out and brings one, from the island's
    scene of him, five cells behind the pole and 1.2 to the side away from the next car, facing Twinsen (RaceTrackScenes, StartPersonActor):
    just out of his script's reach (1500) from Twinsen's start, so the Action key at the car isn't his.
  - His face: PolarGrobo.BuildBody wrote the body unlit (Body.Lit false: no normals, every polygon flat at its ramp's dark end), so his
    grey skin was near black and his vest dark. Now written lit, each polygon its retail type (Lba2Type = Material). Every race build
    installs the body again (RaceTrackService, after the digger), so a folder whose originals have the old one gets the new.
  - The digger: Polar Island's own car for Twinsen (RaceTrackCharacterCars.TrackCar, as the Temple's pumpkin F1), with his own buggy or
    either laser buggy (TrackCarFor; the lasers stay). The race file's drive_as= is the digger's.
  - Parked: race mode drew the car he drives as only while he drove; on foot beside it the buggy showed. HeroLook now hides the parked
    buggy (s_raceParked) and DrawDriveAs draws the car in its place, before RaceMod_Draw's "not driving" return. And the digger parked
    has nobody in it: ISLANDCARS.TXT takes a fifth number, the island car's empty body (the build appends the digger without Twinsen,
    driver: false), which DrawDriveAsOn draws for a parked car that is the island's own (in a race and out of one).
  - Tested headless (a sandbox built with `buildhere`, the window's story setting): at the start the empty digger, Twinsen, the grobo
    behind; Action at the car: no key, he stays out; at the grobo: game variable 230 set; at the car: in (behaviour 12), the digger driven.
- Mushrooms: each is an actor of its cube's scene, stood on the road only while Twinsen is in that scene -- a row just over a cube's edge
  (Polar Island's 21 small scenes; scenes 236 and 240 share a row across their edge) came into sight as he crossed. The build now writes
  each mushroom's place on the island (mushroom=scene actor y x z), and DrawFarMushrooms draws those of other scenes within 40,000 units
  with the body this scene's mushrooms have (as the jet-pack and drive_as are drawn: after the scene, depth-tested). A race: up to six
  drawn from the next cubes at once.
- FunFrock's car: not seen vanishing yet. LBA2_CARVIS=1 logs the first opponent's car's state as it changes (out of sight, hidden,
  bodiless, behind the camera, off the screen) and the far mushrooms drawn; an autodriven race showed his car never hidden, on the screen
  whenever it was in view.

## The race track window's plan file, with several islands ticked (2026-10-27)

- The user: "We have the option of building a race track in LBAAssembler, but the option to select a plan file to do this with isn't
  selectable, is this properly implemented with all recent updates?"
- A plan file doesn't say which island it is, so "A plan file:" was enabled only with one island ticked -- and with all of them ticked
  (the usual build) it was greyed, its tooltip (why) never shown on a disabled control. Now it is there whenever an island is ticked: a
  "the track of" list picks which of the ticked islands the file is (Citadel Island: its town circuit, as before), the others built with
  their own tracks; "Save its built-in track as a plan file…" writes the program's own plan for that island (the embedded
  docs/racetrack/*_track_plan.json) to start a new one from, and fills the path in. The build reads a file with the same reader as the
  built-in plans (RaceTrackPlan.Read), so everything a built-in plan can say a file can.

## A sample plan file (2026-10-27)

- The user: "Let's provide a sample plan file so users know what to create".
- docs/racetrack/sample_track_plan.json: the Desert island's lap in 63 points (every 15th of the built-in plan's 945; the builder spaces
  and smooths them again), its pit lane, the usual road widths and one checkpoint tweak, with a // comment on each part saying what it
  is, what is required (originCellX, originCellZ, points) and what else a plan can say. Plans now read with comments and trailing commas
  allowed (RaceTrackPlan.Read).
- Embedded in the program (RaceTrackPlan.Sample.json); the race track window's "Sample…" button saves it where the user says, fills in
  the path, chooses "A plan file" and the Desert island as the track of (when ticked).
- Tested: `ScriptRoundTrip buildhere <folder> "Desert island" --plan docs/racetrack/sample_track_plan.json` (buildhere's new --plan, as the
  window's plan file builds) on a copy of the pristine test folder: a 1288-cell lap, its pit lane, 16 checkpoints (the plan's tweak
  applied), the grid, five opponents' lines; the top view shows the road round the island as the built-in lap.

## The race track generator: a track drawn on the map, or placed in sections (2026-10-27)

- The user: "The track plan format isn't feasible for a user to hand generate. Let's update LBAAssembler with the ability to take a
  screenshot of an island with a map drawn on it, then we can automatically generate the track accordingly. Let's have this as a new
  track generator wizard. The user selects an island, then with an image of the map they draw the track in one colour, let's go with
  green, jumps in pink, pitlane in orange, and vertical loops in blue. Let's also let the user place track sections onto an island by
  hand, and automatically snap them together so the user doesn't have to perfectly align them".
- Tools > LBA2: race track generator (TrackGeneratorWindow), three pages:
  1. The island (the race track window's list, but the Temple and the palace, whose roads the build makes; the island as it was before
     a build: its *.before-racetrack file).
  2. The drawing, on the island's map (IslandMapRenderer, 3 pixels a cell): pens -- road green, jump pink (over the road where it flies),
     loop blue (over the road where it stands), pit lane orange (beside the road) -- an eraser, pen width, undo (Ctrl+Z), Ctrl+wheel zoom.
     Track sections (straights, 45/90-degree bends, hairpins, a jump, a loop, a pit lane): pick one, click on the map; within 8 cells of an
     open end it snaps onto it, turned to go on from it (and its end onto an open start); the wheel turns it, "Move or turn sections"
     drags and turns placed ones (let go near an open end: snapped), a right click removes one. "Save the map as a picture" / "Load a
     drawn picture": draw in any paint program; what differs from the map in the pens' colours (TrackSketch.Classify, by hue, strong
     colours only) is taken, the picture scaled to the map.
  3. The track: followed (TrackSketchTracer), drawn over the map (its way, jumps magenta, loops cyan, the pit lane, the start), what was
     found and any problem; "The other way round"; "Save the plan…"; "Build…" saves it beside the editor (TrackPlans\) and opens the race
     track window with the island ticked and the plan chosen (RaceTrackWindow.UsePlan).
- TrackSketchTracer: the road (green, pink, blue) closed over small gaps, its biggest piece; a start on a straight in the stroke's middle;
  marched both ways a step at a time, each step the way that stays on the road and bends least, set back to the stroke's middle -- where
  the stroke is wider than a road across (a crossing) straight on, not to the middle; a lap when it comes back round (or both ends meet),
  else a sprint. Into cells a cell apart, smoothed. Each point's kind from what is drawn round it. Jumps: up to 22 cells of each pink
  stretch, slid along it to where the ramp, flight and landing are all in one cube (a flight over a cube's edge breaks), the road straight
  under them; loops at a blue stretch's middle, the road straight 14 cells either side (old moon's: radius 4, 3 across); heights from the
  ground, smoothed, level over each jump and loop, grade 15 %. The pit lane: the orange stroke's two farthest ends. Start: the pit lane's
  middle (the builder's), else the longest straight. Crossings noted (the build makes its own jump there).
- Placed sections with nothing drawn by hand on the road are followed as they are (their exact middle line, chained end to nearest start
  within 14 cells), not through the picture -- a small square of wide strokes traced as a sprint cutting across its inside.
- Tested: `ScriptRoundTrip sketchtest` draws an island's built-in lap on its map as a user would (green, a pink, a blue and an orange
  stroke), reads it back as a picture and traces it: the Desert lap (crossing itself once) a 1374-cell lap, a jump, a loop and the pit
  lane, which builds (a 19.5-cell gap jump, the road straight under it, the loop's ring standing); Mosquibees' 502 for its 500. A UI test
  on a sandbox (env LBA2_TRACKGEN_PICTURE / _SECTIONS / _SAVE drive it without dialogs): the picture -> the same lap; nine sections
  dropped a few pixels and degrees off, snapped -> a 255-cell lap with a jump and a loop, which builds (the ring on the road in the 3D view).
- Not yet: Otringal's drawn lap traced as a 641-cell sprint for its 711 -- a tight double hairpin inside the town drawn in wide strokes
  merges; draw such places with a thinner pen or further apart.

## The race track generator: slopes, spirals and roads passing over each other (2026-10-27)

- The user: "We should make sure the track builder allows for slopes, and upward spirals, along with other over lapping sections of
  track". The builder has done so for a while (the raised road: Volcano Island's double spiral, Wannies' helix, the Elevator Platform);
  the generator now says it. Also fixed the same day: Back to the generator's first page crashed the editor (its island list added to a
  second page; v2026-10-27i).
- Sections that climb (Part.Rise): Ramp up / down (1500 over 30 cells), Steep up / down (2000 over 20), Spiral up / down left / right (a
  turn and a quarter, 12 cells round, 4000: one turn over the other; the quarter more so its end is clear of its start -- a whole turn
  ended where it began and the chain couldn't tell what followed what). The chain (TrackGeneratorWindow.Chain) carries each point's height
  over the first.
- Any climb and the plan is a raised road from start to finish (Raised [0, n-1], RaisedHalf 4.75, RaisedCut, grade 25 %), as the Elevator
  Platform's: each point the ground averaged 20 cells round it (so two roads at one place have one base, their climb between them whole)
  plus 900, plus the climb, smoothed along the way, lifted where it would be under the ground. No pit lane on it (said). A lap whose climbs
  don't come back down is a problem; where the road passes over itself (centres within 7 cells, decks overlapping by half and more) the two
  decks must be 1600 apart, else a problem naming the place and the heights.
- First tries, kept for why: the whole lap lifted over its highest ground (5400 to 8600 everywhere); heights max(ground, start + climb)
  (the lower turn lifted by the ground under it to 1577 under the upper); the ground smoothed along the way (a spiral's turns have
  different neighbours along it); the overlap test at 9 cells (roads side by side, rails touching); 3200 spirals (the spiral's way out
  passes under its first quarter turn at two thirds of its climb).
- Tested: a lap of sections with a spiral up and a spiral down (LBA2_TRACKGEN_SECTIONS, the UI test) -> 474 cells, a raised road 4054 to
  8851, "passes over itself with room"; built on the Desert island: 119 deck pieces, 38 piers, steepest 15 %, the spiral standing over
  itself in the 3D view. LBA2_TRACKGEN_DUMP=<csv> writes each point's place, climb, height and ground.

## The race track generator: hairpins in a drawing, turning sections, snapping either way round (2026-10-27)

- The user: a lap drawn on Wannies Island "doesn't appear joined up" on page 3 (two pictures); "I can't seem to turn a section of track
  when turn is selected, it just moves the track instead"; "When attempting to snap a turned section to one end of a piece of track such
  as a hairpin left, to another hairpin piece it snaps both ends together instead of just the intended one".
- Drawing: the march looked 50 degrees either side and stopped where nothing lay ahead -- the drawing's two hairpins (the stroke's tip at
  the top left, the turn over the fortress) were its "ends", a 276-cell sprint. Now, nothing ahead and not at a crossing, it looks round to
  170 degrees for road not yet gone along (marked 1.4 times the stroke's half width round each step: narrower, the stroke's own edges read
  as fresh road and it went back round the lap the wrong way); at a crossing never round the corner (the crossing road is fresh too: the
  Desert test drawing went round its lower half three times) but straight on with a little more room. The user's drawing: a 520-cell lap.
  Desert 1375, Mosquibees 502, Wannies 461, Volcano 397 as before; Otringal, a sprint before, a 676-cell lap.
  `ScriptRoundTrip sketchimage <game> <ILE> <picture.bgra> <w> <h> <out>` traces a picture as the generator does (ffmpeg -f rawvideo
  -pix_fmt bgra makes the raw file from a screenshot).
- Turning: "Move or turn sections" moved on a drag and turned on the mouse wheel only. Now "Move sections (drag)" and "Turn sections (drag
  round; Q / E turn 15°)": a drag turns it about its start (a joined start stays joined); Q and E turn the one being placed or the one under
  the mouse.
- Snapping: a section snaps by one joint, either way round -- a piece of road has no way of its own (Section.Reversed: its parts in the
  other order, each turning and climbing the other way; Placed.Flipped) -- its start onto an open end or its end onto an open start, the
  joint within reach needing it turned least from the way it lies, then the nearest. A left hairpin dropped with its far end at another's
  end goes on from there as an S; driven the same way as the other it closed into a circle. The next section to place starts turned to go
  on from the last placed.

## The race track generator: drawing in the air -- a height for the pens, a 3D view, a height profile (2026-10-27)

- The user: "We need to also enhance map generation to allow us to easily draw track in the air, perhaps we should allow the map image to
  be rotatable?"; asked which way (a height profile, a rotatable 3D view, height pens): "Let's implement both option 1 and 2".
- Height: a slider on the drawing page (0: on the ground). The pens lay what they draw at it (TrackSketch.Heights, per pixel, NaN on the
  ground); on the map it shows lighter and striped; the ground's height under the mouse is shown, and how far over it the pen draws.
- 3D view (Track3DView, a WPF Viewport3D): the island's surface (a vertex every few cells, IslandOps.Altitude) with the map and what is drawn
  on the ground on it; what is drawn in the air as squares at its height in the pens' colours, its shadow on the ground under it; a
  see-through plane at the pens' height (none at 0). Right button turns it, Shift + right (or the middle) moves it, the wheel zooms; the left
  draws where the mouse meets the plane (the ground at 0: hit tests of the one model). Sections are placed on the 2D map.
- Height profile (page 3): the track's height (yellow) over the ground (brown) along it, jumps and loops marked; dragging sets the height
  under the mouse and eases it 30 points either side; the point is marked on the map; let go, the track is made again with it; "Heights as
  drawn" forgets it. The profile is matched to the new route by where its points are (the route's start moves with its pit lane, and
  raising it drops the pit lane), not by number.
- The plan (TrackSketchTracer.Plan): each point's drawn height the highest drawn round it. Raised road: from sections' climbs the whole
  track (as before); from heights drawn or dragged, only that stretch -- RaiseRamp (40) points of ramp each end, the ground road's own
  heights (its jumps' and loops' level stretches) elsewhere, one stretch (a lap's can't run through its start: then the whole lap, said).
  No steeper than 25 %: a hill widened, never cut (GradeLimited), the raised stretch then out over its slopes. Roads over each other: room
  checked only where one of them is raised (two on the ground are the build's crossroads).
- Tested (the UI test with LBA2_TRACKGEN_AIR / LBA2_TRACKGEN_PROFILE hooks, the Desert drawing): a stretch drawn at 6000 -> points
  1035-1146 raised, the rest on the ground, built (112 cells on piers); a profile dragged to 7000 -> 131 cells on piers, built; the plain
  drawing and the spiral lap as before; the 3D view shown and turned to and from. Fixed on the way: a raised drawing without sections hit a
  null (the lap-closing check read the sections' climbs); the profile's first edit raised the whole lap (compared with the bare ground,
  under the jump's level run-up); it was rotated to the start twice.

## Polar Island's story start: FunFrock drives off, the grobo gives the key, the chase, the end either way (2026-10-27)

- The user: "Let's start story mode on Polar Island with FunFrock speaking "You'll never catch me, whoever makes it to the Sendells first
  will get all of their magical powers" at which point Funfrock starts driving off into the distance, let's start with the digger off the
  track next to the grobo. Twinsen speaks aloud "No, Funfrock come back, what am I going to do I need to stop him". The grobo turns to
  Twinsen and says "Quick, Twinsen, take my vehicle, the fate of the world rests upon your shoulders". This causes the grobo to give
  Twinsen the key ... We should slow Funfrock a little to allow Twinsen to catch up ... we should try to have Funfrock win just ahead of
  Twinsen, so Twinsen watches Funfrock take the jump first. At which point regardless of who wins the end of the race triggers and
  Twinsen is in bed at home with Zoe waking him up".
- Engine (RACEMOD.CPP), three race-file keys, written only into the story's dream file for Polar Island (RaceCarEngineFile; the race
  game's Track select race is as it was -- tested: Twinsen starts in his car, on the grid):
  - chase=<his text> <Twinsen's> <helper entity> <helper's text> <game var>: ChaseOpening in place of the grid and the intro -- in the
    start line's scene the one to beat on pole (OnSpot), his line, "go" (no count); RACE_CHASE_PAUSE_MS of him driving off; Twinsen's
    line; the grobo (the actor of entity 33) turned to Twinsen, his line, game variable 230 set (the key). The opponents drive while
    Twinsen is on foot (`moving` in a chase).
  - chase_lead=<cells> (14): ChaseLead scales his pace by 1 + (lead wanted - lead) / (1.5 x lead wanted), 0.3 to 1.4.
  - dream_any_end=1: he goes on over the finish (the jump into the peak) to his line's end; his finish ends the dream too,
    RACE_DREAM_END_FLIGHT_MS (1.6 s) later -- the win's sets applied all the same (game variable 205: scene 0's opening, Twinsen in bed).
- Build: the texts 23-25 in the island's file (six languages, PolarDream), sorted by id; the digger parked off the road 5.5 cells to the
  side of the pole's column, beside the grobo, facing the way the lap runs (RaceTrackScenes, the StartPerson block).
- Tested headless (the story set, autodrive): FunFrock on pole says his line and drives off, Twinsen and the grobo speak, the key is
  given; Twinsen gets in, crosses the start line and chases; FunFrock, held a little ahead, finishes first ("FunFrock got there first"),
  over the jump, Twinsen watching just behind; the picture fades to white and Twinsen wakes up in scene 0, Zoe there.

## Auto camera in a car the default; key remaps kept (2026-10-27)

- The user: "Let's have the camera for Story mode, and in LBAAssembler to default to "Auto camera in a car". LBA2 also allows keys to be
  reconfigured, is it possible to stop my key configuration being over ridden by a new release of LBAAssembler".
- Camera: AutoCameraInCar's default TRUE (CONFIG_FILE.CPP); a cfg from before -- its 0 only the option's old default written out -- turned
  on once at ReadConfigFile, CameraDefaults: 1 marking it done (a camera chosen afterwards is kept; tested both ways).
- Keys: the remaps were in lba2.cfg only once the engine exited cleanly (BOOT_EXIT WriteConfigFile; resolution changes aside), and the
  editor stops Play by ending the engine's process, as a crash does -- and a release's publish closes the running editor and its engine
  (the standing rule). Every remap made in such a session was lost; the user's lba2-play\lba2.cfg held the defaults. Now leaving the
  keyboard and the gamepad configuration screens saves the cfg at once (CONFIG.CPP MenuConfig, MenuGamepadConfig). Publishing never
  touches release\lba2-play (checked), and a headless run keeps the keys (tested).

## The digger and the buggy; on foot by the raised road; Citadel's actors, doors, lifts, piers and ferry (2026-10-27)

- The parked digger showed the buggy's wheels under it, and the buggy alone while a behaviour key's name was up (the user: "I was
  behaviour switching with the F keys outside of the vehicle, this caused the digger to display as the buggy"). Two causes: the exterior
  terrain pass's actor drawing (EXTFUNC.CPP AffichageActorsZBuf, the renderer's) drew INVISIBLE actors and those out of sight at Y -20000 in
  the game too -- now only in the editor's renderer (GLOBAL.CPP RendererShowHiddenActors, set by RendererBootAssets); and the behaviour
  name's text (OBJECT.CPP INCRUST_SYS_TEXT) left the clip round itself, cutting away the car drawn after it -- RaceMod_Draw unsets it.
- On foot by the raised road (Citadel: "walking into the raised track about chest height causes him to initialy clip into the track then
  spawn on top of it ... Twinsen is unable to jump off the track at the raised sections"): the deck is Twinsen's floor only a step up on foot
  (RAISED_FOOT_STEP 300, RaceMod_FloorForFoot) and its side a wall he slides along (FootDeckWall); the rail holds the car only -- on foot
  he walks or jumps off the edge as off any ledge.
- Citadel Island keeps the actors off the roads (RaceTrackIsland.KeepOffRoadActors; "make sure we've not removed any actors that aren't
  directly on the track such as the cow"): an actor goes only where it, or a point its scripts send it to, is in the way of the cars --
  RaceTrackReport.InTheWay: within a road's width (the curbs', a raised road's own) and a cell, from under its surface to RoadHeadroom over
  it. Every island keeps whoever switches doors (SET_CHANGE_CUBE) and the actors those scripts wait on, where they keep off the road; kept
  cutscene actors are moved aside only when in the way in 3D (a lift's platform beside the road was moved 12 cells from its zone).
- Zones ("keeping all zones that don't directly interfere with the tracks on all levels"): other than the fixed cameras, a zone goes only
  where it is in the way (InTheWay at its own heights).
- Doors into inside scenes (RaceTrackService.DoorBoxes, the original scenes' cube changes to scenes off the island): their decor -- a lift,
  a cave's rock, a house -- stays unless on the asphalt itself. A door whose every original piece of decor went: gone from the scenes if
  lost in all of the island's files; one walked into its wall (ZONE_TEST_BRICK) and lost in one weather's file only is shut while that
  file is shown (door_off=<file> <scene> <box>, RACEMOD.CPP RaceMod_DoorShut in GereZoneChangeCube) -- the storm track's deck over the lift
  by its start line; the museum's door, its building's top taken by the storm track and the whole of it by the town circuit.
- Piers: none in a lane between buildings (walls within 3.5 cells both sides) nor at a door ("The entrance to the port area on Citadel
  has a track support in the way") -- the one at cell (519, 595), 4,073 high.
- The ferry (scene 43): invisible, and the ticket does nothing at the quay or to the ferryman, until the town circuit is won and the
  weather is fine ("lets Twinsen take the ferry in the rain by selecting his ferry ticket from his inventory").

## New Game, Raph's pull-up, the bike taxi, Desert's actors, the school's gates and rim (2026-10-27)

- New Game after a run reads the race file afresh (RaceMod_NewGameScene, from the second game of a session): Polar Island's dream and its
  chase opening start again ("Selecting new game after starting Polar island in play mode doesn't initialise the track correctly"). The
  console's `newgame` runs the menu's New Game for headless tests.
- Raph (parade_stop=<text>, RaceTrackStory RaphYell): the variable set when Twinsen stands on the start line on foot (its zone), he laps on
  until he comes up to him, brakes from 14 cells with the tyres' squeal, pulls up 2.6 cells to his side, yells "Get off the track", and
  rolls on to his park.
- One weather's road only: an actor in the way of one file's road and not the other's stays where it is, and actor_at=<file> <scene>
  <actor> <x> <y> <z> puts it aside while that file is shown (RaceTrackScenes Result.ActorsAt) -- Citadel's bike taxi under the shelter by
  the shop in the storm, the town circuit running through the shelter.
- Desert island keeps its actors off the road (KeepOffRoadActors) -- the camel, the garden balsam (scene 62's actor 7: removed with the
  rest, it couldn't be picked up). An actor that stands clear but walks onto the road keeps its way, the points on the road moved off it
  (PointsOffRoad). 41 actors still go, every one standing on the road; no zone goes but 16 fixed cameras along the track.
- The School of Magic's gates open from the start (HeldVars 65 = 1, which also shows the open gates' decor), and its canyon's rim comes
  back up to the island's own height where the levelling cut it, clear of the road by its curbs and 0.75 cells (OwnRimsAt, 253 points).

## Desert's water deck, Citadel's museum lift, FunFrock outside his car (2026-10-27)

- Desert's water crossing (RaceTrackIsland.WaterDeck; "a two sided draw bridge ... If a draw bridge isn't feasible let's change it into a
  small raised track section with a slight ramp up and down"): the lap's longest water span a raised road from shore to shore, humped to
  2,200 over the sea with 18 % ramps, as wide as the road (RaceTrackBuilder.WaterDeck), and the island's own sea back under it -- every cell
  that was sea before, near the deck and no ground road, its triangles and heights as they were (WaterUnderDeck, 523 cells). The ferry sails
  under it. A drawbridge would need decks the engine moves; the hump is the fallback the user named.
- Citadel's museum (scene 48; its top taken for the storm track's deck): walls built up from what is left of it, 2,618, to the deck's
  underside (DeckWalls, RaceTrackRaisedBody.Block), and a lift on the deck where its upper window was (DeckLifts: a draw-only pad and post,
  a door zone over it into scene 15 at the window's own place, 12288 3000 13312). Tested: Twinsen walks onto the pad and is in the museum.
- Polar Island's story start (chase_car=<body> <anim> <entity>): FunFrock's car stands empty on pole (RaceTrackCharacterCars
  BuildFunFrockEmpty -- the car with its driver's polygons taken out, his bones kept for the racer's animations -- installed as "FunFrock's
  car, empty"), FunFrock himself beside it (RaceTrackIsland.ChaseDriver: scene 95's actor 14, entity 217, scripts emptied); he says his
  line, walks to it (RACE_CHASE_WALK_MS) and gets in, and the car drives off. The camera looks at the opening from the other side
  (the classic camera's angle turned half round through AddBetaCam), and back once Twinsen is in his car.

## The bridge raised for the ferry, arrows alike, Raph waits on the road, the museum lift both ways, the car at home (2026-10-27)

- Desert's bridge (WaterDeck): "the top of the ferry still clips through it, let's have a road ramp up, the bridge over, then a ramp back
  down again" -- a ramp up on the ground at 15 %, the deck over the water only (its sea and 2 cells either side) at 3,400, a ramp down: 76
  cells in all, 23 of deck.
- Arrows (RaceTrackRaisedBody.Tile): "raised road sections have tended to have small triangles, and Desert track featured arrows instead,
  let's be consistent across all tracks" -- the deck's arrow is the ground's: a shaft (its first cell, ArrowShaftHalf either side) and a
  head (three cells, ArrowHalfWidth either side at its base), orange.
- Raph (parade_stop=<text> <entity> <var>): stopped beside Twinsen he stays on the road -- the game's Raph, who talks for him, moved with
  his car -- until Twinsen drives with his gloves (game variable 7); then he drives to his park, facing the way he goes, and turns as he
  parks ("during the cut scene had his car slide sideways to the edge of the track").
- The start line's car: in only with the action key -- its script's carry_obj_by clause (the car's number in the game's scene, another
  actor's once the build took some away) put Twinsen in it as he stood by it.
- The museum lift: one pad 2.5 cells across, level with the road, no post; the museum's window out (scene 15's door into 48 nearest the
  lift's arrival) brings Twinsen up onto the road beside it.
- Twinsen's car on Citadel (RaceTrackIsland.CarHome; RACEMOD.CPP game variable 211, RACE_GRID_CAR_VAR): on the start line only when the
  race file starts the game there (the race game's start_in_car=, start_scene=); otherwise made under the carport at his house the first
  time (scene 49, INIT_BUGGY 1) and shown where he left it in the start scenes (INIT_BUGGY 0).
- The cow (2026-10-27, "The cow is still missing from the citadel track"): an actor seen -- a body, not hidden -- that stands on the road, on
  an island that keeps its actors, is moved aside (ShiftOffRoad, its way's points with it) and kept; only the hidden ones on the road
  still go. Citadel: 25 actors removed (87 before), the cow moved 8 cells off the town circuit.
- A door shut in one weather's file (door_off=) shuts the way back to its place from the scene it leads to as well: the sewers' ride up
  to the start line's lift, whose platform the storm track's deck took ("causes twinsen to momentarily be up in the air").

## The cow and the bike taxi in their weather, the museum lift a real lift, banks walked on (2026-10-27)

- An actor in the way of one weather's road only stays where it is in the other, its scripts' points untouched, and is put aside
  (actor_at=) while that weather's file is shown: Citadel Island's cow ("you've moved the cow, even though the rain track goes nowhere
  near its position"), the bike taxi under the shop's shelter. On an island with two files, actors and points follow the reshaped ground
  up, never down (Reseat raiseOnly): lowered for one file they stood buried in the other's.
- The museum lift (deck_lift=, RACEMOD.CPP DeckLift): no door zone -- the action key on the pad takes Twinsen down with it through the
  road (RACE_LIFT_MS, RACE_LIFT_DROP; its decor moved by DecorPoseHook, now RaceDecorPose for it and the roulette wheel) into the museum;
  the museum's window brings him onto the pad from below, and it rises with him. His keys held while it moves, standing.
- Citadel's steep banks (SoftBanks): the island's own ground kept, walled for the cars only -- the blocking bit and game code 10
  (MAPTOOLS.CPP CJ_CAR_WALL), open to Twinsen on foot (TerrainFootOpen, set by RaceMod_FloorForFoot): he walks on them, the cars still
  can't climb them.
- Twinsen placed at a scene's start or back after a clover (OBJECT.CPP) takes the raised road as his floor only a step up, as he walks.
