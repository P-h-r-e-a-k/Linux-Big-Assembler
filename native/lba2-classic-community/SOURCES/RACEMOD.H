#ifndef RACEMOD_H
#define RACEMOD_H

#include <SYSTEM/ADELINE_TYPES.H>

// The race-track options of the engine (RACEMOD.CPP). All off unless the level editor names a car setup file in LBA2_RACETRACK_FILE.

#define RACE_MAX_GEARS 8

typedef struct {
    S32 Gears;                    // 1..RACE_MAX_GEARS
    S32 GearTop[RACE_MAX_GEARS];  // top speed of each gear, world units per second
    S32 Accel;                    // speed gained per ms of throttle, in thousandths (4000 = the original car's 4), for a gear whose top speed is RACE_REF_TOP
    S32 Brake;                    // speed lost per ms of braking, thousandths
    S32 Coast;                    // speed lost per ms with neither pedal (and above the gear's top speed), thousandths
    S32 Reverse;                  // top speed backwards
    S32 Steer;                    // turn per second, 4096ths of a turn
    S32 Automatic;                // the gears change by themselves
    S32 Hud;                      // gear, speed and lap times on screen
    S32 HasLine;                  // a start line to count laps at
    S32 LineCubeX, LineCubeZ;     // the island cube it is in
    S32 LineX0, LineZ0, LineX1, LineZ1; // its ends, cube-local world units
    S32 LineDirX, LineDirZ;       // the way a lap crosses it (any length)
} T_RACE_CAR;

// The top speed the acceleration is given for: the original buggy's (BUGGY.CPP MAX_SPEED).
#define RACE_REF_TOP 3800

// TRUE when the engine was started for a race-track mod.
S32 RaceMod_Active(void);
const T_RACE_CAR *RaceMod_Car(void);

// The current gear (1..Gears), and gear changes: X up, Z down (edges of the keys), or by themselves.
S32 RaceMod_Gear(void);
void RaceMod_ResetGear(void);
void RaceMod_UpdateGear(S32 speed);

// The laps: called every frame the hero drives, with his position (cube-local); a lap is counted when the move since the last call
// crosses the start line the right way.
void RaceMod_Lap(S32 x, S32 z);

// Every frame before the scene is drawn: the grid and the count-down, and the opponents moved along their lines and their cars put there,
// in the player's scene.
void RaceMod_Frame(void);

// TRUE while the player's car stands on the grid, before the count-down's GO, and for a moment after a hit (RaceMod_CarHit).
S32 RaceMod_Held(void);
S32 RaceMod_FadeFromWhite(void);

// The display (gear, speed, lap times, checkpoints, race position), drawn over the scene while the hero drives.
void RaceMod_Draw(void);

// A raised road (the car file's raised=<file>): a floor of its own, over the ground. While an object is moved the ground's height under
// it is the road's where the road passes (RaceMod_FloorFor: for something at height y; RaceMod_FloorForNothing afterwards: the ground
// alone again), and the hero is kept inside its edges (RaceMod_Rail: moving from (ox, oy, oz) to (*nx, *nz), cube-local).
void RaceMod_FloorFor(S32 y);
void RaceMod_FloorForNothing(void);
void RaceMod_Rail(S32 ox, S32 oy, S32 oz, S32 *nx, S32 *nz);
// ... and never under the surface of the road he stood on: moving from (ox, oy, oz) to (nx, *ny, nz), after everything else has had its
// say (an actor's box against him lowers him to see whether he stands on it).
void RaceMod_Floor(S32 ox, S32 oy, S32 oz, S32 nx, S32 *ny, S32 nz);

// gravity=<k>, a rollercoaster of a lap: k where the hero is on the raised road (0 anywhere else and without the key), and the road's
// slope the way he faces and its banking. The slope pulls at his car k x RACE_GRAVITY x its sine; up a climb the engine holds RaceMod_ClimbShare of the
// gear's top speed; speed over the gear's top is lost at RACE_OVERSPEED_DRAG of the excess a second, and RACE_RAIL_DRAG a second
// more of that excess while the road's rail holds the car (RaceMod_RailTouched: since it was last asked). Over its top gear's speed the car
// steers as much quicker as it goes faster, and a banked bend turns it (RACE_BANK_STEER): what it gathers down a drop it can take
// through the bend at the foot of it.
#define RACE_GRAVITY 5000.0          // world units a second squared: a cell is about a metre
#define RACE_CLIMB 0.6               // the share of the top speed lost up a climb, per unit of slope (and of k)
#define RACE_OVERSPEED_DRAG 0.5
#define RACE_RAIL_DRAG 2000.0
#define RACE_BANK_STEER 2.2          // a banked bend turns the car: its steering is quicker by this much per unit of banking
double RaceMod_Gravity(double *slope, double *bank);
double RaceMod_ClimbShare(double gravity, double slope);
S32 RaceMod_RailTouched(void);

// A jump's flight (the hero's animation 200-295, each island's and each jump's own, played by his track script) flown at the car's speed at the take-off: the flight's clock run
// that many times as fast as its own speed, the same arc (jump_speed=<lo> <hi>: the bounds; 0: off). RaceMod_JumpClock before the hero's
// animation is moved on, RaceMod_JumpClockAfter after; declared where they are called, OBJECT.CPP (they take the engine's T_OBJ_3D).

// Whether Celebration Island is drawn from its file with the statue (CELEBRA2) rather than the one before it rises (CELEBRAT), given
// whether it has risen in the game (variable 79): a race-track mod's file says which (statue=1, statue=0), else the game does.
S32 RaceMod_StatueShown(S32 risen);

// While racing, a car that goes into the lava or the sea is put back on the road behind where it was (rescue=0: as in the game, it
// burns or drowns Twinsen). GereCodesJeu asks: non-zero, and Twinsen is not to die there.
S32 RaceMod_Rescue(void);
// Once a frame from the main loop, before the scene is drawn: a sprint's story -- its intro, its end followed up (the dream's waking up or
// the race again), the wake line (RACEMOD.CPP sprint=, wake=). A new game's first scene: a set's dream=, else `scene`.
void RaceMod_Story(void);
S32 RaceMod_NewGameScene(S32 scene);

// While racing, a hit on Twinsen in his car (a land mine) stops the car dead and holds it for a moment, and costs him no life:
// non-zero when it is such a hit.
S32 RaceMod_CarHit(void);

// While racing, an island drawn from another file than its own (the car file's island_file=): that file's name, or NULL.
const char *RaceMod_IslandFile(S32 island);

// While racing, the hero's car carried round a vertical loop (the car file's loop=): non-zero when it is, with its new place in
// *nx..*nz (DoAnimExt). Stands the car level again whenever nothing pitches it.
S32 RaceMod_Loop(S32 ox, S32 oy, S32 oz, S32 *nx, S32 *ny, S32 *nz);

// A test pilot for headless runs (the console's `autodrive`): on, the player's car is driven along the first opponent's line. Its input,
// ORed into the frame's own.
void RaceMod_AutoDrive(int on, double lookCells, double sideCells);
U32 RaceMod_AutoInput(void);

#endif
