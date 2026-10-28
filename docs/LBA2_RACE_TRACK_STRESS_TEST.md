# Race track stress test and car setups (2026-10-16)

The user: "Stress test all of our tracks and suggest car setup tweaks for each track, perhaps tracks with long straights would favour
cornering and tracks with tight corners better turning."

All 18 tracks were built together from the pristine data (`ScriptRoundTrip buildtogether`, 378 s) into `E:\dump\TEMP\stress\game` and
driven headless by the test pilot (`autodrive`). The scripts are in `tools/RaceTrackStress/`:

- `stress.py files` writes each track's race file (`racecarfile`, `RT_RACE=<island>`) with the standalone game's car appended: five gears to
  45/57/63/97/110 km/h, acceleration, braking and rolling 300 %, steering 200 %, automatic, no qualifying lap, three laps.
- `stress.py plan` scores setups with the engine's own speed planner (the console's new `raceplan`, below).
- `stress.py pilot` drives four races per track (the base car on the racing line, the base car 1.5 cells to the left of it, a hot car with
  everything +20 %, the suggested setup), with the opponents and power-ups; `report.py` reads them up to the finish, with a position
  trace (`racecars` every 5 s) to find cars standing still; `repro.py` repeats one with screenshots and state dumps.
- `stress.py trial` drives clean time trials (no opponents, no power-ups) of the base car and the suggested one.
- `items.py` races layouts of the power-ups' rows against each other (2026-10-17: "Item spacing", at the end).

## How a setup is scored: `raceplan`

The engine plans every line's speeds from the player's car (RACEMOD.CPP `PlanSpeeds`): a bend of radius R at up to the steering's turn
rate times R, no faster than the top gear, braking into bends with the brakes and pulling away through the gears. The new console
command `raceplan <top%> <accel%> <brake%> <steer%>` (and `raceplan file <setups> <out>`) re-plans the first opponent's line (or the
guide) for the race file's car scaled by those percentages, at a neutral character, and returns its perfect lap and where its time goes:
flat out (at the top gear's speed), round the bends (at the steering's limit) and between (braking and pulling away). 236 setups take
1.5 s per track. "Top speed" scales every gear, as the Race car window's top speed slider does, so a higher one also pulls less hard.

Two grids: every attribute 70-130 % with all four summing to 400, and -- since the brakes turned out worth almost nothing -- the brakes
left at 100 % and top speed, acceleration and steering (70-130 % in steps of 5) summing to 300: a setup "budget", so a gain in one costs
another. The best of the second grid is each track's suggestion.

## What each track rewards

Time shares are the base car's planned lap. The three middle columns are the lap time saved by 10 % more of one attribute alone. The
suggestion is in the Race car window's terms (base: top speed 110 km/h, acceleration 300 %, braking 300 %, steering 200 %). Planned and
driven laps are base -> suggested; driven is the mean of three clean laps (the sprints: the run). (These fastest setups were the window's
"best setups" for a day; since 2026-10-17 they are the drivable ones of "Easy to drive, not only fast", below.)

| track | flat out | bends | +10 % top | +10 % accel | +10 % steer | top speed | accel | steering | planned lap (s) | driven lap (s) |
|---|---|---|---|---|---|---|---|---|---|---|
| Desert island | 34 % | 6 % | 0.99 | 0.61 | 1.64 | 110 km/h | 210 % | 260 % | 50.4 -> 47.4 | 48.0 -> 46.0 |
| Citadel, storm lap | 23 % | 9 % | 0.16 | 0.14 | 0.45 | 110 km/h | 210 % | 260 % | 11.1 -> 10.3 | 11.0 -> 10.5 |
| Citadel, town circuit | 21 % | 6 % | 0.50 | 0.50 | 1.62 | 110 km/h | 210 % | 260 % | 39.6 -> 36.5 | 38.7 -> 36.3 |
| Mosquibees Island | 41 % | 4 % | 0.53 | 0.16 | 0.58 | 116 km/h | 210 % | 250 % | 17.5 -> 16.3 | 17.6 -> 16.3 |
| Celebration Island | 2 % | 22 % | 0.07 | 0.23 | 1.09 | 104 km/h | 225 % | 260 % | 17.8 -> 15.5 | 17.2 -> 15.2 |
| Celebration, lava lake | 0 % | 15 % | 0.02 | 0.16 | 0.70 | 104 km/h | 225 % | 260 % | 12.2 -> 10.6 | 12.8 -> 11.1 |
| Elevator Platform | 3 % | 20 % | -0.01 | 0.11 | 1.03 | 110 km/h | 210 % | 260 % | 14.2 -> 11.9 | 13.0 -> 11.3 |
| Sendell's Well | 0 % | 17 % | 0.05 | 0.04 | 0.30 | 94 km/h | 255 % | 260 % | 4.6 -> 3.7 | 4.3 -> 3.6 |
| The old moon | 0 % | 44 % | -0.01 | 0.12 | 0.41 | 94 km/h | 255 % | 260 % | 12.9 -> 12.1 | 13.1 -> 12.5 |
| Polar Island (sprint) | 71 % | 3 % | 3.40 | 0.26 | 0.81 | 143 km/h | 210 % | 200 % | 56.4 -> 49.6 | 55.8 -> 50.9 |
| Island of the Francos | 48 % | 12 % | 1.38 | 0.26 | 1.77 | 116 km/h | 210 % | 250 % | 48.4 -> 44.0 | 39.2 -> 35.8 |
| Otringal | 18 % | 16 % | 0.20 | 0.37 | 1.29 | 110 km/h | 210 % | 260 % | 28.4 -> 26.0 | 29.1 -> 26.5 |
| Volcano Island | 19 % | 23 % | 0.26 | 0.23 | 1.29 | 116 km/h | 210 % | 250 % | 24.7 -> 22.2 | 24.9 -> 22.3 |
| The Emerald Moon | 21 % | 23 % | 0.35 | 0.27 | 1.09 | 110 km/h | 210 % | 260 % | 25.3 -> 23.1 | 24.6 -> 22.5 |
| Wannies Island | 47 % | 6 % | 0.76 | 0.19 | 0.72 | 116 km/h | 210 % | 250 % | 23.4 -> 21.7 | 22.2 -> 23.9 |
| The Emperor's palace (sprint) | 66 % | 20 % | 2.09 | 0.08 | 1.07 | 126 km/h | 210 % | 230 % | 40.3 -> 35.8 | 37.7 -> 34.7 |
| Island CX | 0 % | 12 % | 0.08 | 0.31 | 1.01 | 99 km/h | 240 % | 260 % | 20.2 -> 17.8 | 21.5 -> 19.5 |
| The Temple of Bù | 71 % | 7 % | 3.00 | 0.15 | 1.17 | 132 km/h | 210 % | 220 % | 55.3 -> 49.1 | 55.2 -> 48.9 |

Braking is left out of the table: 10 % more saves at most 0.04 s anywhere. At 300 % the brakes stop the car in a few metres, so the
plan barely brakes; they matter only to a driver reacting late.

What it says:

- **Long straights want top speed** (the user's guess, with "cornering" read as top speed): Polar Island, the Temple of Bù and the
  Emperor's palace spend two thirds of the lap flat out, and 10 % more top speed is worth 2-3.4 s there -- several times what steering is.
- **Tight tracks want steering, and give up top speed**: Celebration Island, the lava lake, the Elevator Platform, Sendell's Well, the old
  moon and Island CX never reach the base car's 110 km/h for long (0-3 % of the lap flat out); steering is worth ten times top speed there,
  and their suggestions trade top speed away (94-104 km/h) for steering and a little acceleration.
- **The rest are steering first, top speed second** (Desert, both Citadel tracks, Otringal, Volcano, Emerald, Francos, Mosquibees, Wannies).
- **Acceleration is the base car's surplus**: at 300 % it is what every suggestion gives up first.
- The driven laps follow the plan to within a second except on Wannies Island, whose laps scatter (17-27 s: the frog's mouth timing,
  the branches), and Island CX and the Francos, whose hazards make single laps noisy.

**A catch for the game**: the opponents drive the player's own car as the setup makes it (RACEMOD.CPP `PlanSpeeds`, "Each drives the
player's own car"), so a better setup makes the whole field faster and the race no easier. For per-track setups to matter in a race, the
opponents would need a car of their own (the track's reference setup).

**In the Race car window (2026-10-17).** The user: "Yesterday we calculated the optimum car setup for each track, let's add this as an
option to override the test sliders we currently have for car setup." The Car tab's "Use each track's best setup for its top speed,
acceleration and steering" (off unless ticked; `RaceCarSetup.UseTrackSetups` in the settings) drives the track Play races with the
suggestion above in place of those three sliders: its top speed scales the gears as the top speed slider does, and braking, rolling,
reverse and the gearbox stay the sliders'. The sliders keep their values, greyed while they are overridden, and drive a track with no
suggestion (one built later) and every track once the box is cleared. The window says which setup applies (the track Play races; from
the race track window, each of the folder's tracks). The table is `RaceCarSetup.TrackSetups`, by race track entry (Citadel Island's
two tracks each have one). Each track's lowest planned lap in `plan3.txt` was the table's setup (until the next section replaced them);
Wannies Island and the Emperor's palace each have a tie at the third decimal place, and the table takes the first of the pair. The race file says which it
used (`# the track's best setup ...`, then the car keys), in each track's file of the story's set too, where the set's own keys (an
island with no track) stay the sliders'. Polar Island's dreamt sprint takes the suggestion's 143 km/h in place of the dream's own 120
(its waking gears stay the sliders'). Test: `RT_TRACK_SETUP=1 ScriptRoundTrip racecarfile ...`.

## Easy to drive, not only fast (2026-10-17)

The user: "We also need to refine the best setup for tracks, the best setup is not the one that gives the fastest time, the recommended
setup for the Temple track means a slight side arrow tap takes us from one side of the narrow track to the other at the recommended speed.
Reducing the speed to 80 and setting steering to 150 feels better to drive and allows us to more easily stay in the centre of the track."

**Why the fastest lap was the wrong test.** The planner's lap is a robot's: it drives the line at exactly the speed each bend allows and
steers as finely as it likes. Top speed and steering each make that lap quicker, so the fastest setups took as much of both as the budget
gave (steering 250-260 % on 15 tracks; 126-143 km/h on the palace, the Temple and Polar Island). A player steers with keys, a tap at a time,
and sees the car drift before tapping back -- and how far a tap sends the car across grows with both the speed and the turn the tap makes.
Nothing in the lap time knows how wide the road is.

**How the buggy steers** (BUGGY.CPP `MoveBuggy`, `RaceSpeed`): while a steering key is held the car turns at a fixed rate -- the race file's
`steer` (the window's percentage of 1024) in 4096ths of a turn a second, at any speed up to the top gear's (faster than that, more in
proportion) -- starting and stopping at once, and nothing turns it back: let go, it carries on along its new heading. A tap of t seconds turns
it by steer x t, and it then crosses the road at v·sin(turn) until the driver taps back. In the engine (`drive.py tap`: the test pilot
drives the Temple's long straight and lets go; `input` holds the throttle and taps left for 6 steps of 16 ms; `cartrace` logs each frame):

| setup | speed | heading turned (4096ths) | steer x 96 ms | across after the tap | v·sin(turn) |
|---|---|---|---|---|---|
| 80 km/h, steering 150 % | 79.7 km/h | 144 (12.7°), held after it | 147 | 4.91 cells/s | 5.00 cells/s |
| 132 km/h, steering 220 % | 128.5 km/h | 216 (19.0°), held after it | 216 | 9.44 cells/s | 10.46 cells/s |

**The measure: a tap's move across the road, for the road's room.** A tap of 0.1 s, and the tap back 0.25 s after it, move the car across
by 2v(1 - cos wt)/w + v·sin(wt)·0.25 (v the speed, w the turn rate, t the tap) -- nearly v x w x 0.1 x 0.35: the car's speed times its turn
rate, the sideways pull of a full-lock bend. At 132 km/h with steering 220 % that is 4.4 cells; at 80 km/h with 150 %, 1.8. The room is how
far the car's middle can move from the road's middle: on a raised road its half width less 640 (RAISED_INSET, the car's half width the
engine keeps inside the rail), on the ground the plan's kerb half width less the car's (633). The Temple's deck is 4.5 cells from middle to
rail (4.0 on its narrower stretches): 3.25 cells of room (2.75). The measure is the share of the room the tap takes, at every point of the
racing line at the speed the planner drives there with that setup (the console's `raceplan file <setups> <laps> <points>` now writes each
setup's plan point by point: RACEMOD.CPP `PlanPointsOut`), averaged over the lap's worst 2 s of driving -- one narrow gate doesn't decide
it, a narrow straight does. Where the engine carries the car over a jump nothing is steered; where a branch leaves or joins the road, the
widest piece there counts. `tools/RaceTrackStress/drive.py` does it all (`points`, `table`, `report`, `verify`, `tap`).

**Calibrated on the user's verdict.** At 80 km/h with steering 150 % the Temple's tap takes 0.66 of the room (1.8 cells of the narrow
stretches' 2.75, at full speed); with the old suggestion, 132 km/h and 220 %, 1.58 -- from the road's middle well past the rail, from one
side most of the way to the other, as the user found. The standalone game's base car (110 km/h, 200 %) takes about 1.2. The limit is the
user's: **0.66 of the room**. The tap's and the reaction's lengths cancel out in that calibration (every term goes as v x w): with 0.06 s
and 0.2 s the same 18 setups come out, with 0.15 s and 0.4 s the same but for a step of the grid on three tracks. The window matters
more: 1 s puts four more of the tight tracks at the floor below (Celebration Island, the Elevator Platform, the old moon, Polar Island),
4 s lets the narrow ones up to 20 km/h faster (the old moon 99 km/h, Otringal 90).

**The choice.** The grid: top speed and steering 70-130 % of the base car's in 2.5 % steps, acceleration 70-130 % in steps of 5, the three
summing to no more than 300 -- the stress test's budget, so what the tap takes from top speed and steering goes to acceleration. Of the
setups within the limit, the one with the fastest planned lap; top speeds rounded down to whole km/h. The Temple's is the user's own top
speed and steering, with the acceleration that laps them fastest (the planner's own pick within the limit there is 85 km/h with steering
140 %: 70.9 s against 73.9, for the same tap). How it was balanced:

- The limit is relative to each road's room, so a wider road takes a faster car: the Francos' and the Emerald Moon's 104 km/h against the
  Temple's 80. Narrow ground roads (the old moon's and Sendell's Well's kerbs at 3.5 cells) and raised ones (the lava lake's 3.05, Island
  CX's 3-4) take slower or less quick-steering cars.
- It reads the speed the car is driven at, not its top gear's: a track that never reaches its top speed is judged in its bends (Sendell's
  Well's car is set to 101 km/h and never goes over 76), and a rollercoaster's drops count at the speed they give (the Elevator Platform's
  car goes 91 km/h downhill with a top gear of 79).
- Nothing goes below 70 % of the base car's top speed or steering -- 77 km/h, 140 % -- the user's own 80 km/h and 150 % are just above
  both: slower than that would be absurd for a race car. Island CX, Otringal and the lava lake are narrower than even that floor manages
  (their tap takes 0.70-0.81 of the room) and get the floor, marked * below. If they still feel twitchy, steering under 140 % is the next
  thing to try.
- Acceleration doesn't move the car across, so every setup puts the budget's rest into it: 390 % (130 % of the base car's) on all 18 --
  at the Temple 0.2 s a lap better than 300 %.
- Steering came out low almost everywhere (140-155 %; Volcano Island, all bends, 175 %): for the same tap the planner gains more lap from
  top speed than from steering.
- The perfect lap is 18-56 % slower than the fastest setup's. The opponents drive the player's car as the setup makes it (above), so a
  race is no easier or harder for it.

Each track's setup in the Race car window's terms (top speed, acceleration, steering). The room is the car's middle's either side of the
road's middle, typical (half the lap's time) and narrowest (its narrowest tenth). The tap's move is at the car's fastest, and in brackets
the measure (the worst 2 s, as a share of the room; the limit 0.66). Laps are old setup -> new; planned is the perfect lap, driven the
mean of the test pilot's three clean laps (the sprints: the run).

| track | room: typical / narrowest (cells) | old setup | its tap | new setup | its tap | planned lap (s) | driven lap (s) |
|---|---|---|---|---|---|---|---|
| Desert island | 3.26 / 3.26 | 110 km/h, 210 %, 260 % | 4.3 cells (1.31) | 99 km/h, 390 %, 140 % | 2.1 cells (0.64) | 47.4 -> 56.0 | 46.3 -> 54.0 |
| Citadel, storm lap | 3.26 / 3.26 | 110 km/h, 210 %, 260 % | 4.3 cells (1.24) | 96 km/h, 390 %, 150 % | 2.2 cells (0.66) | 10.3 -> 12.3 | 10.5 -> 12.2 |
| Citadel, town circuit | 3.26 / 3.26 | 110 km/h, 210 %, 260 % | 4.3 cells (1.31) | 93 km/h, 390 %, 150 % | 2.1 cells (0.65) | 36.5 -> 44.6 | 36.1 -> 43.3 |
| Mosquibees Island | 3.26 / 3.26 | 116 km/h, 210 %, 250 % | 4.3 cells (1.33) | 99 km/h, 390 %, 140 % | 2.1 cells (0.64) | 16.2 -> 19.6 | 16.3 -> 19.5 |
| Celebration Island | 3.50 / 2.70 | 104 km/h, 225 %, 260 % | 4.0 cells (1.26) | 82 km/h, 390 %, 140 % | 1.7 cells (0.66) | 15.5 -> 21.4 | 15.3 -> 20.9 |
| Celebration, lava lake | 2.50 / 1.56 | 104 km/h, 225 %, 260 % | 4.0 cells (1.93) | 79 km/h, 390 %, 140 % | 1.9 cells (0.81) * | 10.6 -> 14.7 | 11.2 -> 15.1 |
| Elevator Platform | 3.50 / 2.15 | 110 km/h, 210 %, 260 % | 4.8 cells (1.57) | 79 km/h, 390 %, 145 % | 2.3 cells (0.65) | 11.9 -> 17.7 | 11.3 -> 16.5 |
| Sendell's Well | 2.26 / 2.26 | 94 km/h, 255 %, 260 % | 3.6 cells (1.49) | 101 km/h, 390 %, 155 % | 1.8 cells (0.66) | 3.7 -> 5.3 | 3.6 -> 5.1 |
| The old moon | 2.26 / 2.26 | 94 km/h, 255 %, 260 % | 3.6 cells (1.57) | 79 km/h, 390 %, 140 % | 1.7 cells (0.66) | 12.1 -> 14.3 | 12.8 -> 14.3 |
| Polar Island (sprint) | 2.76 / 2.76 | 143 km/h, 210 %, 200 % | 4.3 cells (1.58) | 77 km/h, 390 %, 140 % | 1.6 cells (0.64) | 49.6 -> 77.2 | 51.1 -> 75.2 |
| Island of the Francos | 3.50 / 3.50 | 116 km/h, 210 %, 250 % | 4.3 cells (1.25) | 104 km/h, 390 %, 145 % | 2.3 cells (0.65) | 43.9 -> 54.8 | 39.3 -> 42.6 |
| Otringal | 2.75 / 2.51 | 110 km/h, 210 %, 260 % | 4.3 cells (1.56) | 77 km/h, 390 %, 140 % | 1.6 cells (0.70) * | 26.0 -> 35.7 | 28.8 -> 35.7 |
| Volcano Island | 3.50 / 3.50 | 116 km/h, 210 %, 250 % | 4.3 cells (1.24) | 85 km/h, 390 %, 175 % | 2.2 cells (0.64) | 22.2 -> 27.8 | 22.4 -> 28.1 |
| The Emerald Moon | 3.25 / 3.25 | 110 km/h, 210 %, 260 % | 4.3 cells (1.29) | 104 km/h, 390 %, 140 % | 2.2 cells (0.66) | 23.1 -> 28.7 | 22.5 -> 29.0 |
| Wannies Island | 3.50 / 3.50 | 116 km/h, 210 %, 250 % | 4.3 cells (1.24) | 101 km/h, 390 %, 150 % | 2.3 cells (0.66) | 21.7 -> 26.1 | 20.6 -> 21.0 |
| The Emperor's palace (sprint) | 3.01 / 3.01 | 126 km/h, 210 %, 230 % | 4.3 cells (1.44) | 93 km/h, 390 %, 140 % | 2.0 cells (0.65) | 35.9 -> 49.6 | 34.9 -> 46.3 |
| Island CX | 2.15 / 1.75 | 99 km/h, 240 %, 260 % | 3.8 cells (1.96) | 77 km/h, 390 %, 140 % | 1.6 cells (0.80) * | 17.8 -> 24.2 | 18.4 -> 26.6 |
| The Temple of Bú | 3.25 / 3.25 | 132 km/h, 210 %, 220 % | 4.4 cells (1.58) | 80 km/h, 390 %, 150 % | 1.8 cells (0.66) | 49.1 -> 73.9 | 48.9 -> 73.4 |

Polar Island's dreamt sprint takes the table's 77 km/h in place of the dream's own 120 while the box is ticked (as it took 143 before).
(The Francos' and Wannies Island's driven laps are under their plans with either setup: the Francos' hazards and Wannies' branches, as in
the first table.)

**Checked.** All 18 tracks built together from the pristine data with this code (`E:\dump\TEMP\setupdrive\game`, STRESS_ROOT
`E:\dump\TEMP\setupdrive`) and each driven by the test pilot three times (`drive.py verify`, 54 runs): a clean time trial with the old
setup and one with the new, and a race with the new against the opponents, power-ups on. Every run reached its end with no rescue, fall,
missed checkpoint or hold at a cube's edge before it, and every jump landed (the Desert 3 a run, the storm lap 8-9, Mosquibees 3, the lava
lake 9); the old moon's "off the ring" lines are its first loop's gap, leapt on purpose, as many with either setup. After the finish, the
pilot driving on with power-ups still about, the stuck-car reset fired once on Mosquibees Island (against its decor 25 at cells
(488, 532), a nitro penguin just dropped) and once on Sendell's Well. With the new setups `RT_TRACK_SETUP=1 ScriptRoundTrip racecarfile`
writes each track's top speed, acceleration and steering into its race file and into the story set's track files (the set's own keys
the sliders').

## What the stress runs found

## What the stress runs found

72 races and 36 time trials: no crashes, no game-clock warnings, no missed checkpoints, every race finished (the storm lap has no
opponents, only Raph's time to beat, so it never "ends"). The trouble was all off the racing line, where the AI never drives but a player
will:

1. **Otringal: the pit wall's blunt end.** A row of thin decor boxes (body 246, 0.4 x 2 cells, 944 high) runs along the start straight
   at island cells x 497.1-497.5, z 470.8-504.0, 1.6 cells left of the deck's middle. A car on that line drives into its end face at z 504
   head-on and stays there, wheels spinning at 110 km/h (55 s until the run ended). Wants a flared or angled leading end.
2. **Celebration Island: the start gantry's left post on the kerb** (cells about 458, 476). A car 1.5 cells left of the line runs into it and
   is held (90 s, never freed). Another stop of 30 s at cells (485, 474) in one race freed itself.
3. **The old moon: the same, its start gantry's left post** (cells about 532, 526), 65 s.
4. **Citadel town circuit: a brick building's corner at the road's edge** (cells about 512, 608, between checkpoints 14 and 15). Off the line
   the car wedges into it (15-30 s, and once for the whole race with the suggested setup).
5. **The Emerald Moon: one cube edge with no crossing zone**, from cube (7,8) into cube (7,7) at x 4,800-5,700, height 7,700, every lap of
   every run. The engine's fallback carries the car over (RACEMOD.CPP `HeldAtEdge`), so nothing shows, but the build should add the zone.
6. One race of the lava lake had a rescue out of the lava, which worked; in the first, untraced desert run Twinsen's car stopped for good
   after a jump flown at 2.74x its speed and a lightning hit -- not seen again in the four traced desert runs.
7. The old moon's "off the ring" lines are its first loop's gap, leapt on purpose, not failures.

The test pilot never reverses, so a car held against a wall stays there. A player can back out, but a stuck-car rescue (held still with the
throttle down for a few seconds: back on the road, as from the lava) would make all four holds harmless.

## The fixes (the same day)

The user: "Make the fixes and add the stuck car reset".

- **The stuck-car reset** (RACEMOD.CPP `StuckCar`): Twinsen's car that goes no further than a cell in 3 s while it drives on -- the
  throttle down, or its own speed still over 1,000 units/s (a car held against something keeps its speed, 70-90 km/h on the display,
  and the test pilot, which drives by that speed, lets go of the throttle near the old moon's gap loop) -- is put back on the road behind
  (`Rescue`), as from the sea. Not while it is held on purpose (the grid, a hit, a bite: `RaceMod_Held`), skidding, flying a jump, in a
  loop, on the super jet-pack, on the roulette wheel's drop or falling. The log says where, and the decor it last ran into (if within the
  last second): `stuck at (x,y,z) for 3.0 s driving on: back on the road (the last decor run into: ...)`. The car's own box is logged once
  a run too (`the car's box: x -633..633, y 0..940, z -633..633` -- the scenery's clearances are for that).
- **Glancing blows** (EXTFUNC.CPP `GlanceAside`, RACEMOD.CPP `RaceMod_Glance`): the decors' boxes are all square to the island, so no shape
  of a wall's end or a post could turn a car aside -- a car meeting one square on stopped dead. Now the race car, blocked along the way it
  mostly moves by a box it overlaps across that way by no more than 320 units (a gantry's post, 283 wide; a pit wall's end, about 200; a
  corner the road's edge brushes), is pushed aside past it, to its nearer side. Blocked across its way it slides along the box as the
  game has it (pushed to the nearer side there, a car running past a post beside it was pushed back, frame after frame).
- **Start gantries built to the road** (RaceTrackBuilder.cs `BuiltGantry`): the retail gantry is one length, 10.5 cells post to post, and
  over the 9-cell roads its posts stood 0-1.2 cells beyond the kerbs -- under the race car's half width (1.24 cells). Where either post
  would stand nearer its kerb than 1.75 cells, the gantry is built to the road instead: RaceTrackRaisedBody's gantry (as the raised roads
  have), as tall as the retail one, the frame drawn only and each post a solid box of an empty body. Nine tracks have one now: Desert,
  both Citadel tracks, Mosquibees, Celebration, Sendell's Well, the old moon, Polar Island and the Emperor's palace. (Its chequered beam
  is capped at 30 squares: the 20-cell gantries over the pit lanes would have passed the engine's 550 points.)
- **The Emerald Moon's edge zone** (RaceTrackScenes.cs `CoverEdges`): a zone's start along the edge is also its arrival's other
  coordinate, and the engine reads 512 and 31,744 there as the marks of the other edges' zones (OBJECT.CPP `GereZoneChangeCube`). Scene
  230's north zone happened to start at x 512, so it waited for the car to leave eastwards too and never took it. A start on either
  mark is now moved a unit.

Checked by building all 18 tracks fresh with the last commit's code and with these changes: only the nine gantried islands' ILE and OBL
and SCENE.HQR changed (28 of 47 files the same). Then the 72 races again: every one finished, and no car stood still. The reset fired 12
times before a race's end, at 6 places:

- off the racing line (the test pilot 1.5 cells to its left): the town circuit's narrow street, where a building's wall is the road's
  edge (2); the start of Celebration Island's raised road (1); Polar Island (1);
- on the racing line with the faster cars: Sendell's Well with the suggested setup (1), and Wannies Island at cells (538, 488) with the
  hot car (2);
- **on the racing line with every car, still to fix: Wannies Island, lap point 384 of its raised road (cells 464, 562, height 7,113)**,
  where the car is held against the left rail (base 2, hot 2, suggested setup 1). It used to cost about 10 s
  a lap there (a 35.6 s lap against 25); the reset now gets the car out in 3 s.

## The second round (the same evening)

The user: "Make the fixes". Every reset left was followed to its cause, with forced super jet-packs too (`tools/RaceTrackStress/jet.py
<track> <tag> <side> <tick>...`: `raceitem 4` at each tick and the cars' places every 10 ticks after it).

- **Wannies Island, lap point 384, held against the rail** (RACEMOD.CPP `RaceMod_AutoInput`): round a tight bend the point the pilot
  steers for, a look-ahead on along the line, is inside the bend from the car (the chord cuts the arc). Where the line takes the bend
  near the inner rail -- 155 units short of the rail's hold here -- that point was past the rail, and the car drove into the rail nose
  first and stood there pointing at it. On a raised road the point is now kept inside the rail's hold, half a cell in (left where it is
  on a strip beside the road, or on another stretch's deck). The super jet-pack drives by the same pilot, so it is kept in too.
- **The super jet-pack over Mosquibees Island's plateau's edge**: the pilot looks further ahead the faster the car goes, and at twice the
  speed the point ahead cut the bend after the start by the plateau's edge -- over it, every jet-pack. The look-ahead is now at most 0.7
  of the line's radius there (no nearer than two cells). The test pilot's own side offset (`autodrive 1 5 <side>`) is no longer added
  to the jet-pack's steering (a player's jet-pack has none). A rescue on the jet-pack ends it (it drove the car off the road, and at
  twice the speed it drove it off again at the same bend: seven rescues in one jet-pack), and the stuck-car reset now works on it too
  (it drives into a wall as a car does, and stood there pinned until it ran out).
- **The super jet-pack through Mosquibees Island's bridge** (EXTFUNC.CPP `GhostSkips`): since 2026-10-10 the jet-pack goes through the
  island's objects (the user's "the jet pack needs updating so it doesn't get stuck on objects"), and that took the bridge's deck too --
  a deck of the island's objects, not a raised road. The car fell into the valley at the bridge's start in 5 jet-packs of 6. An object
  the car is on -- its top no more than half a brick over where the car was, the game's own "on top of it" test -- now holds it up as
  it holds any car; walls, posts and the shuttles' sides are still gone through.
- **Twinsen on foot after a rescue into another scene**: a car that left its scene (the jet-pack off the plateau into scene 105, or over
  the island's edge) was rescued by setting the scene to change from the drawing, which begins the scene afresh -- and its own opening's
  `SET_MOVE` took Twinsen out of his car. On foot the race stood still for good. The rescue's scene change is now asked for as a zone's
  is (`RaceMod_EdgeCross`: the line point's place, the car carried over), and for 3 s after it Twinsen is put back in his car if he
  comes in on foot (`BackInCar`).
- **Off the island's edge** (EXTFUNC.CPP, `RaceMod_OffIsland`): the game sends a hero who moves where there is no cube into the phantom
  cube (94) to drown, which took Twinsen out of his car too. A race car is now held at the edge and put back on the road behind.
- **Opponents stopped for good by being run into** (RACEMOD.CPP `RaceMod_OpponentStruck`, EXTRA.CPP `RaceMod_ExtraHit`): the game
  counts a car running into an actor as a hit (OBJECT.CPP's object collisions), and the race took every hit by Twinsen for his magic
  ball, which stops an opponent for 1.5 s. Leant on, an opponent was stopped again each time its last stop ran out and never moved. On
  Wannies Island the old Wannie stood on the racing line at cells (537, 488) -- the first round's "hot car" spot -- with Twinsen's car
  against the rail behind him: four resets in a row, each putting Twinsen back behind him. Only a thrown ball's hit stops an opponent
  now; run into, a car is no more than in the way (`Twinsen's car runs into ...'s car: no hit`, at most every 5 s in the log). The runs
  showed 6-26 such run-ins a race.
- **The Desert's jump cut short over its gap** (RACEMOD.CPP `RaceMod_InFlight`; EXTFUNC.CPP `InitSkating`, OBJECT.CPP `HitObj`): a jump
  is the hero's flight animation (RACE_JUMP_ANIM_*), and anything that plays another animation on him ends the flight where he is. Two
  did: the game's skating, which the engine starts on a buggy over a forbidden slope (EXTFUNC.CPP `ReajustPosExt`'s "blindage buggy")
  -- at the lip, over the gap's steep edge -- and a hit in the air (his own nitro penguin going off beside him, slow after a rescue).
  The car fell into the gap and stood against the landing ramp's stepped foot, always at (317951, 1143, 319487), the reset getting it
  out. With jet-packs forced every 10 s, 3 of 27 jumps ended so (`jump: the flight over (now animation 33 ...)`: 33 the skate, 22 a hit,
  0 the landing). While Twinsen flies a jump he now neither skates nor takes a hit (`... hits Twinsen's car: in a jump's flight`); all
  24 jumps of the next six races landed. Each flight's end is logged now, with the height above the ground there.
- The log says where each sea or lava rescue was (`into the lava or the sea at (x,y,z)[, on the super jet-pack]`); the stuck-car
  reset adds the rail's last move on a raised road (`the rail's last move: <what> at segment <n>, <lat> across (kept <lo>..<hi>)`:
  1 the move not made, 2 past the road's end, 3 within the rail, 4 on a strip, 5 within another stretch's rail, 6 driven off a grounded
  end, 7 round a corner, 8 put on the rail, 9 not at the deck's height, 10 off a grounded end) and the objects within 3 cells of the
  car. `ScriptRoundTrip altitude <ILE> <x> <z> [radius]` prints the island's ground round a place every half cell (it found the bridge's
  valley). `report.py` now splits a log line written into the middle of another (the engine's lines and the console's are written
  apart: one Otringal race's end was hidden that way).

Checked with the 72 races and 36 time trials again on the last build: every race with opponents finished (the storm lap has none),
no crash, no flight cut short, and the time trials ran without a reset (Otringal's and the Francos' slow laps are their hazards -- the
gunners, a gas monster's bite, the shuttle, the steam jets on their lap's side). The reset fired 4 times before a race's end, against 12
in the first round, all with the test pilot 1.5 cells to the left of the line: the town circuit's narrow street (1), Celebration
Island's lap start -- where the line runs along the raised road's east edge, at ground level, and 1.5 cells beside it is the hillside --
(2), and once into the lava lake's lava. None is on the racing line. The forced jet-packs: 6 on Mosquibees Island and 12 on Otringal,
Wannies, the Emerald Moon and Island CX without a rescue, and six Desert races with one every 10 s, every jump landed.

## Item spacing (2026-10-17)

The user: "Currently items are too close together and they make for a chaotic race, let's do some bench marking to move them further
apart."

**Until now** a row of mushrooms stood every 40 cells of road from 30 after the start line (RaceTrackScenes `MushroomSpots`): at the
cars' speed a row every 1.4-2.1 s on every track but the loop and jump tracks, 31 rows a lap on the Desert, 39 on Polar Island, 42 on the
Temple of Bú -- 303 rows on the 18 tracks. The test pilot took an item as fast as his two slots emptied (13 a minute, one every 4.5 s),
and the race was mostly the items going off.

### How it was measured

`tools/RaceTrackStress/items.py`, the layouts built by `ScriptRoundTrip buildtogether` with `RT_ITEM_SPACING` (`"40"` the old rule,
`"80"` cells, `"8s"` seconds; nothing set: the builder's default). Each layout is a folder of `E:\dump\TEMP\itemspacing` (`ITEMS_ROOT`)
with all 18 tracks built into `game\`, a plain copy of `E:\dump\TEMP\stress\game` (`ITEMS_SEED`). The engine and ScriptRoundTrip are
stress.py's (the main tree's builds) unless `ITEMS_ENG` / `ITEMS_SR` say otherwise -- these runs used copies of them in that folder:

```
python items.py build <variant> [8s|40|...]   # the 18 tracks built (log: build.log, each track's rows; files.md5)
python items.py files <variant>          # each track's race file: the stress test's base car, its opponents and power-ups, 3 laps
RUNS=4 PAR=7 python items.py run <variant>
python items.py report <variant>...      # per track and overall (and <variant>\items_<SCEN>.json)
python items.py table <variant>...       # the tables below
itemdiff <game A> <game B>               # ScriptRoundTrip: two builds compared scene by scene with the power-ups taken out
```

Four races a track and layout, `qualifying_seed=1..4` -- the seed is the whole race's randomness (the power-ups drawn, the opponents'
skills), so every layout races the same draws, and a track whose layout did not change raced exactly the same (the lava lake, Sendell's
Well, the storm lap and the old moon came out identical at 8 and 12 s). No qualifying lap, the pilot on the racing line from pole, three
laps (the sprints one). Two kinds of race:

- **lead** (`SCEN=lead`, the default): the opponents at their own skills. The pilot mostly leads.
- **pack** (`SCEN=pack`): every opponent at 96-104 % of the planned speeds, faster than the pilot: he races from among them and behind,
  the others' items coming at him from in front.

What a run counts, from GO to Twinsen's finish (`read` in items.py; the engine's own log lines):

- **pickups**: `Twinsen takes ...`, `<name> takes a mushroom's power-up` (an opponent uses its item at once).
- **item hits**: an item's effect taking hold on a car -- blown up by a penguin, skidding on a slick (not a pipe's drip), hit by a magic
  ball, a fire ball or a fake mushroom, shrunk by lightning, haunted. A hazard's hits (steam jets, lava, gunners, the shuttle, witches)
  are not items' and left out; a hit the protection spell or the super jet-pack shrugs off is not a hit.
- **item hits a car a minute** -- all cars' item hits over the cars and the race's minutes: **the chaos figure**.
- **places changed a minute**: the cars ranked every second along the racing line (`racecars` every 50 ticks, each car's place on the
  first opponent's line, lap by lap, never backwards), each pair that swaps counted.
- **lap spread**: the standard deviation of Twinsen's second and third laps; **laps over planned**: their mean over the engine's perfect
  lap (`opponent line ... a perfect lap`).
- **rescues**: `back on the road` (the sea, the lava, a stuck car's reset, a fall).

### Overall

| (lead, mean of the 17 tracks with opponents) | 40 cells (HEAD) | 80 cells | 5 s | **8 s** | 12 s |
|---|---|---|---|---|---|
| rows a lap (mean of all 18) | 16.8 | 8.4 | 5.4 | **3.3** | 2.3 |
| Twinsen's pickups a minute | 13.2 | 10.5 | 8.2 | **5.2** | 3.5 |
| each opponent's pickups a minute | 3.0 | 1.6 | 1.3 | **1.0** | 0.6 |
| item hits on Twinsen a lap | 1.49 | 1.02 | 0.91 | **0.68** | 0.30 |
| item hits on each opponent a lap | 1.29 | 0.88 | 0.63 | **0.43** | 0.30 |
| **item hits a car a minute (chaos)** | 2.51 | 1.71 | 1.38 | **1.04** | 0.67 |
| places changed a minute | 7.4 | 7.1 | 6.2 | **4.7** | 4.4 |
| Twinsen's place changes a race | 3.1 | 2.2 | 2.2 | **1.8** | 1.3 |
| Twinsen's lap spread (s) | 2.05 | 1.91 | 1.95 | **1.25** | 1.24 |
| Twinsen's laps over the planned lap | 1.11 | 1.07 | 1.07 | **1.06** | 1.03 |
| races finished / rescues (72 races) | 72 / 1 | 72 / 0 | 72 / 1 | **72 / 1** | 72 / 0 |

| (pack, mean of the 17 tracks with opponents) | 40 cells (HEAD) | **8 s** |
|---|---|---|
| Twinsen's pickups a minute | 10.8 | **4.4** |
| each opponent's pickups a minute | 5.0 | **1.4** |
| item hits on Twinsen a lap | 1.83 | **0.51** |
| item hits on each opponent a lap | 1.77 | **0.60** |
| **item hits a car a minute (chaos)** | 3.32 | **1.11** |
| places changed a minute | 9.5 | **7.6** |
| Twinsen's place changes a race | 5.1 | **4.3** |
| Twinsen's lap spread (s) | 2.66 | **2.02** |
| Twinsen's laps over the planned lap | 1.10 | **1.05** |
| races finished / rescues (72 races) | 72 / 2 | **72 / 2** |

Two things the races showed about the engine (RACEMOD.CPP, unchanged):

- **The leader empties the rows.** A mushroom taken is gone for 15 s (`RACE_MUSHROOM_BACK`), and an opponent takes one only in the scene
  Twinsen is in (its car and the mushrooms are that scene's actors). Leading, the pilot took 13 items a minute while each opponent got 3;
  in the pack the opponents' share rose to 5. The old layout's chaos was mostly the leader's own items (oil, penguins and fake mushrooms
  dropped behind him go to the leader: `s_puLead`).
- **On a lap shorter than 15 s the leader finds his row still empty next time round**: the storm lap (11 s), the lava lake (12 s), the
  Elevator Platform (14 s), the old moon (13 s) and Sendell's Well (4.6 s) give him an item every other lap or less, whatever the spacing.
  A regrowth tied to the lap (say the lesser of 15 s and two thirds of the planned lap) would be the engine's side of it.

### Each track

Rows a lap and the seconds between them (the builder's reckoning, below):

| track | 40 cells (HEAD) | 80 cells | 5 s | **8 s** | 12 s |
|---|---|---|---|---|---|
| Desert island | 31 (1.5) | 15 (3.2) | 10 (4.8) | **6 (8.0)** | 4 (11.9) |
| Citadel, storm lap | 5 (2.1) | 2 (5.2) | 2 (5.2) | **1 (10.3)** | 1 (10.3) |
| Citadel, town circuit | 25 (1.5) | 13 (2.8) | 7 (5.3) | **5 (7.4)** | 3 (12.3) |
| Mosquibees Island | 11 (1.6) | 6 (2.9) | 3 (5.7) | **2 (8.6)** | 1 (17.1) |
| Celebration Island | 10 (1.7) | 5 (3.3) | 3 (5.6) | **2 (8.3)** | 1 (16.7) |
| Celebration Island, lava lake | 1 (10.2) | 0 (-) | 2 (5.1) | **1 (10.2)** | 1 (10.2) |
| Elevator Platform | 8 (1.6) | 4 (3.3) | 3 (4.4) | **2 (6.6)** | 1 (13.2) |
| Sendell's Well | 2 (2.0) | 1 (4.1) | 1 (4.1) | **1 (4.1)** | 1 (4.1) |
| The old moon | 2 (4.8) | 1 (6.6) | 1 (9.6) | **1 (9.6)** | 1 (9.6) |
| Polar Island (sprint) | 39 (1.4) | 19 (3.0) | 11 (5.1) | **7 (8.0)** | 5 (11.3) |
| Island of the Francos | 32 (1.5) | 16 (3.1) | 10 (5.0) | **6 (8.2)** | 4 (12.4) |
| Otringal | 16 (1.7) | 9 (3.0) | 5 (5.4) | **3 (8.9)** | 2 (13.4) |
| Volcano Island | 17 (1.5) | 9 (2.9) | 5 (5.2) | **3 (8.6)** | 2 (12.9) |
| The Emerald Moon | 7 (3.8) | 2 (10.4) | 5 (5.4) | **3 (9.0)** | 2 (13.4) |
| Wannies Island | 16 (1.5) | 8 (3.0) | 5 (4.8) | **3 (8.0)** | 2 (12.0) |
| The Emperor's palace (sprint) | 28 (1.6) | 14 (3.1) | 9 (4.9) | **5 (8.8)** | 4 (11.0) |
| Island CX | 11 (1.6) | 6 (2.9) | 3 (5.8) | **2 (8.7)** | 1 (17.4) |
| The Temple of Bú | 42 (1.4) | 21 (2.7) | 12 (4.8) | **7 (8.2)** | 5 (11.5) |

Twinsen's pickups a minute, and the chaos figure (item hits a car a minute), lead races; the last column the pack's, HEAD -> 8 s:

| track | pickups: 40 cells (HEAD) | 80 cells | 5 s | **8 s** | 12 s | chaos: 40 cells (HEAD) | 80 cells | 5 s | **8 s** | 12 s | pack chaos |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Desert island | 17.2 | 13.4 | 10.4 | **6.7** | 4.8 | 2.08 | 1.27 | 0.99 | **0.52** | 0.25 | 2.99 -> 1.25 |
| Citadel, storm lap | 14.6 | 6.8 | 6.0 | **3.5** | 3.5 | 1.25 | 1.63 | 1.27 | **0.83** | 0.83 | 1.66 -> 0.00 |
| Citadel, town circuit | 18.5 | 16.4 | 9.6 | **7.3** | 4.6 | 1.33 | 1.55 | 0.56 | **0.58** | 0.21 | 2.65 -> 0.92 |
| Mosquibees Island | 17.1 | 14.0 | 10.3 | **6.9** | 3.1 | 1.52 | 1.41 | 0.85 | **0.67** | 0.28 | 3.67 -> 1.71 |
| Celebration Island | 7.5 | 11.1 | 9.7 | **3.4** | 3.4 | 5.60 | 2.94 | 1.71 | **1.09** | 0.84 | 5.25 -> 1.18 |
| Celebration Island, lava lake | 1.6 | 0.0 | 2.0 | **1.7** | 1.7 | 1.80 | 0.00 | 2.40 | **1.85** | 1.85 | 1.06 -> 1.29 |
| Elevator Platform | 9.0 | 7.1 | 5.1 | **3.6** | 1.9 | 4.47 | 2.92 | 2.75 | **1.72** | 0.66 | 5.08 -> 0.91 |
| Sendell's Well | 9.3 | 4.6 | 4.6 | **4.6** | 4.6 | 1.45 | 1.16 | 1.16 | **1.16** | 1.16 | 2.02 -> 0.29 |
| The old moon | 6.3 | 3.2 | 3.1 | **1.5** | 1.5 | 0.48 | 0.19 | 0.57 | **0.87** | 0.87 | 1.09 -> 0.74 |
| Polar Island (sprint) | 16.6 | 10.8 | 7.0 | **2.4** | 3.5 | 2.90 | 3.28 | 2.24 | **1.94** | 1.11 | 2.65 -> 1.23 |
| Island of the Francos | 17.8 | 15.6 | 11.6 | **5.7** | 5.7 | 2.71 | 2.26 | 1.95 | **1.72** | 0.72 | 4.66 -> 1.45 |
| Otringal | 16.9 | 13.7 | 8.9 | **5.8** | 1.8 | 2.12 | 2.03 | 0.90 | **0.34** | 0.18 | 2.91 -> 1.14 |
| Volcano Island | 16.4 | 16.2 | 12.0 | **7.0** | 4.5 | 2.32 | 1.54 | 1.20 | **0.59** | 0.64 | 3.82 -> 1.22 |
| The Emerald Moon | 14.0 | 4.8 | 9.8 | **7.2** | 4.8 | 1.63 | 0.60 | 1.47 | **0.53** | 0.53 | 2.44 -> 1.31 |
| Wannies Island | 12.5 | 10.0 | 8.3 | **8.6** | 3.3 | 3.03 | 2.10 | 1.34 | **0.82** | 0.34 | 3.79 -> 0.98 |
| The Emperor's palace (sprint) | 15.5 | 12.8 | 7.0 | **4.6** | 2.8 | 2.42 | 1.66 | 1.53 | **1.14** | 0.59 | 1.98 -> 0.81 |
| Island CX | 10.3 | 10.1 | 8.0 | **4.5** | 2.7 | 4.53 | 2.89 | 0.94 | **1.52** | 0.61 | 6.41 -> 1.38 |
| The Temple of Bú | 17.2 | 15.5 | 12.3 | **7.5** | 5.4 | 2.28 | 1.26 | 0.92 | **0.60** | 0.60 | 3.93 -> 1.12 |

(Four races a cell: single tracks swing by a factor of two from one draw to the next -- Polar Island, a 56 s sprint against one car, is
the noisiest -- so read the means.)

### The rule chosen: a row every 8 s of driving

**The aim**: about one pickup every 10-12 s for the player -- five a minute, two or three a lap on a 25 s lap, much as a kart racer
spaces its item boxes -- with the hits that make a race "chaotic" more than halved.

**8 s** does that: Twinsen picks up 5.2 items a minute leading and 4.4 in the pack (13.2 and 10.8 before), the chaos figure falls from
2.51 to 1.04 (-59 %) leading and from 3.32 to 1.11 (-67 %) in the pack, he is hit by an item 0.68 times a lap instead of 1.49 (0.51
instead of 1.83 in the pack), and his laps are steadier (spread 2.05 -> 1.25 s) and nearer the plan (1.11 -> 1.06). The racing itself
stays: in the pack the places still change 7.6 times a minute (9.5 before). **5 s** leaves 8 pickups a minute and only takes the chaos
to 1.38; **12 s** takes it on to 0.67, but at 3.5 pickups a minute -- one in 17 s, and on the 17 s laps (Mosquibees Island, Celebration
Island, Island CX) a single row -- with Otringal down to 1.8 a minute: rare. **80 cells** (twice the old spacing) is still 10.5 a minute
and 1.71, and a fixed spacing in cells floods the fast tracks (Polar, the Temple: 19 and 21 rows) and can miss a track's few clear places
altogether: the lava lake had no row at all.

**How** (RaceTrackScenes `MushroomSpots`, `RaceTrackOptions.ItemSpacing` = 8): the lap's driving time from the start line is reckoned
for the standalone game's car -- top gear 110 km/h, steering 200 % -- round each bend of the first opponent's line as fast as it steers
(`RaceTrackReport.RacePath`'s radii) and no faster than its top gear, a vertical loop's ring at 60 km/h. That time is cut into the whole
number of even stretches nearest to 8 s (at least one), and a row stands in the middle of each -- or, where the middle is on or near a
jump, a loop or a carried jump (the same clearances as before), at the clear point of its stretch nearest the middle, at least half a
stretch after the row before, and not in the start line's first 30 cells or the lap's last 20. The rows are the same as before: three
across a wide road, two (or one) on a narrow one. One rule for every track -- no track needed a tweak of its own. The reckoned laps come
within 17 % of the engine's planned ones (Desert 47.7 s against 50.4, Polar 56.3 against 56.4, the Temple 57.6 against 55.3), the old
moon's excepted (9.6 against 12.9: its jump loop is taken at 22-39 km/h). The build's log says each track's rows: `the power-ups: 18
mushrooms in 6 rows across the road ..., a row about every 7.9 s of driving (a lap 47.7 s)`. `ItemSpacingCells` keeps the old rule for
comparisons (`RT_ITEM_SPACING=40` builds exactly what the last commit built).

From 303 rows and 909 mushrooms on the 18 tracks to 60 rows and 180 mushrooms. The lava lake keeps its one row and the old moon has one
(its two loops' clearances leave room for one in the 9.6 s reckoned; at 40 cells it had two).

### Checked

- The build with the default (no `RT_ITEM_SPACING`) is byte for byte the 8 s layout that was raced; `RT_ITEM_SPACING=40` with the new
  code is byte for byte the last commit's build (all 47 files).
- Against the last commit's build only `SCENE.HQR` and `RACETRACK.JSON` differ (45 of 47 files the same). `itemdiff`: 115 scenes'
  records differ (of 317), and each of the 115 is the same as before once the power-ups' actors are taken out (the mushrooms, and the
  penguins, slicks and fake mushrooms after them, which move up by as many places). In RACETRACK.JSON each track's `Mushrooms`,
  `Penguins`, `Oil` and `Fakes` lists, nothing else. In the race files the `mushroom=`, `penguin=`, `oil=` and `fake=` lines, and on
  Citadel Island's two tracks the `hide_actor=` lines that hide the other weather's power-ups (192 -> 132 on the storm lap, 87 -> 75 on
  the town circuit); the line files the same.
- Mushrooms per track, before -> after: Desert 93 -> 18, storm lap 15 -> 3, town circuit 75 -> 15, Mosquibees 33 -> 6, Celebration 30 -> 6,
  lava lake 3 -> 3, Elevator 24 -> 6, Sendell's Well 6 -> 3, old moon 6 -> 3, Polar 117 -> 21, Francos 96 -> 18, Otringal 48 -> 9,
  Volcano 51 -> 9, Emerald 21 -> 9, Wannies 48 -> 9, palace 84 -> 15, Island CX 33 -> 6, Temple 126 -> 21.
- 144 races with the new layout (72 lead, 72 pack) all finished, as did the 144 on the old one. Rescues: 3 in each set of 144, none at a
  mushroom row. With the new layout: the Francos (lead), Twinsen's car held 3 s against De La Fontaine's, stopped right in front of it
  by Twinsen's own red magic ball; Mosquibees Island (pack), Twinsen's car shrunk by the Queen's lightning and stuck 3 s; Otringal
  (pack), off the road out of scene 89 by the roulette wheel. With the old one: the lava lake (a jump flown at twice its own speed came
  down short, into the lava), the Francos (pack: stuck 3 s just after the cars round it were hit) and the Emerald Moon (pack: a fall off the road).
