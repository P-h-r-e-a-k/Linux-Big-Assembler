/* RACEMOD: the engine's race-track options, for the race-track mods the level editor builds (LBA Assembler, Tools > LBA2: Desert island
 * race track).
 *
 * Off unless the environment names a car setup file (LBA2_RACETRACK_FILE). The editor sets it only when the game folder it plays has a race
 * track built, so with any other game the engine is unchanged. With it on:
 *   - the buggy's body follows the decor floor that carries it (a bridge deck) instead of the ground under it (BUGGY.CPP BuggyFloorY);
 *   - the car handles as the file says: a gearbox (X shifts up, Z down, or automatic), each gear with its own top speed, the acceleration
 *     scaled to the gear (a low gear pulls harder), braking, coasting, the speed backwards and the steering;
 *   - laps are counted at a start line, and only once the car has crossed every checkpoint round the lap in order (reversing back over
 *     the line and across it again is not a lap);
 *   - opponents (up to six): each a copy of a car the build put in every scene, driven along its own racing line round the lap, placed
 *     in whichever scene the player is in. Each drives the player's own car as the setup makes it -- its top gear, its steering (the
 *     buggy turns at a fixed rate, so a bend of radius R is taken at up to that rate times R), its pull through the gears and its
 *     brakes -- as well as it can on its line, times its skill (the pace); a character makes one faster on the straights or in the
 *     bends; and one that has fallen behind the player pushes harder, up to its catch-up;
 *   - a grid and a start: the opponents wait in the pit lane while the player drives his qualifying lap (on the grid they stand across
 *     the start line he has to cross), then the cars line up on grid spots (behind the start line, staggered), in the order the
 *     qualifying decides (the player's timed lap against each opponent's lap at its skill, a few seconds either way at random), then a
 *     count-down, 3, 2, 1, GO, the player's car held until GO;
 *   - a small display of the gear, the speed, the lap times, the checkpoints, the qualifying and the grid, and the race position.
 *
 * The file is "key=value" lines ('#' starts a comment); anything missing keeps the original car's value. Speeds are world units per second
 * (512 to a cell), rates the speed gained or lost per millisecond (decimals allowed), the steering in 4096ths of a turn per second:
 *     gears=5
 *     gear1=1500  gear2=2300  ...          top speed of each gear
 *     accel=4  brake=12  coast=7  reverse=2000  steer=1024  automatic=0  hud=1
 *     startline=<cube x> <cube z> <x0> <z0> <x1> <z1> <dir x> <dir z>     cube-local ends, and the way a lap crosses it
 *     checkpoint=<the same>                                                  one per line, in the order a lap crosses them
 *     opponent_path=<file>       lines "x z y speed [radius]": world units from the island's corner (32768 to a cube), from the start
 *                                line round; with the line's bend radius at each point the speeds are planned from the player's car,
 *                                without it the file's speeds are driven
 *     opponent_grid=<points>     how many of the path's last points the opponent starts before the start line
 *     opponent_pace=<percent>    its skill: of the speeds planned (100: the player's car driven perfectly on the line)
 *     opponent_top=<percent>     its character: the top speed and pull, and the cornering, as a share of the player's car's
 *     opponent_grip=<percent>
 *     opponent_catchup=<percent> how much harder it pushes when it has fallen behind the player (at most, 60 cells behind)
 *     opponent_actor=<scene> <actor>   the opponent's car in that scene (one line per scene)
 *     opponent_name=<name>       for the grid's list
 *     opponent2_path=...  opponent2_grid=...  opponent2_pace=...  opponent2_actor=...   the second opponent, and so on up to opponent6
 *     grid=<cube x> <cube z> <x> <y> <z> <turn>   a grid spot, cube-local; one line per spot, pole first (with none, the old start:
 *                                the opponents set off when the player first drives)
 *     pit=<the same>             a spot in the pit lane an opponent waits on while the player qualifies; one line per spot
 *     weather=fine               Citadel Island's weather once the storm is over (RaceMod_FineWeather), whatever the chapter
 *     holo_arrow=<n>             the holomap arrow the mod's story points at the start line: cleared once Twinsen drives
 *     qualifying=1               a qualifying lap first (0: the grid is formed as soon as the player is in his car, he on pole)
 *     qualifying_seed=<n>        the opponents' qualifying times' randomness (default: the clock)
 *     hide_actor=<scene> <actor> a car in a scene that isn't racing (an opponent the player left out): kept out of sight
 *     raised=<file>              a raised road: lines "x z y half", its middle point by point in lap order and half its width, world
 *                                units from the island's corner. A floor of its own over the ground, at any grade (see "the raised road")
 *     statue=1                   Celebration Island drawn from its file with the statue (CELEBRA2), whatever game variable 79 says;
 *                                statue=0 from its file before the statue rises (CELEBRAT, the lava lake)
 *     rescue=0                   a car that goes into the lava or the sea burns or drowns Twinsen as in the game (default 1: it is put
 *                                back on the road, RaceMod_Rescue)
 *     jump_speed=<lo> <hi>       a jump's flight flown at the car's speed at the take-off (RaceMod_JumpClock): its clock that many times
 *                                as fast as the flight's own speed, kept between lo and hi (default 0.6 4; 0: the flight in its own time)
 *     jumpcam=<anim> <cube x> <cube z> <x> <y> <z>   while the hero flies the jump whose flight is generic animation <anim>, and a moment
 *                                after it lands, the camera that follows the car stands there (cube-local) and looks at the car (JumpCamera)
 *     arcjump=<foot> <lip> <land> <land foot> [side]   a jump the car is carried over (RaceArc): places in the raised road's file, its
 *                                ramp's foot, its lip, the landing lip and the landing hill's foot; the camera beside it on `side` (1 or -1),
 *                                or with 0 behind the car as on the road (the follow camera)
 *     cube_scene=<cube x> <cube z> <scene>   the island's outside scene of that cube, for the cube changes a carried jump makes
 *   The story (the race-track mod's, on Citadel Island; the editor writes these only for a game played as a game, not for a race
 *   started on the line):
 *     track=<island> <when> <first scene> <last scene> <file>   one of the folder's tracks (a car file of its own) and when it is
 *                                raced: when the game enters an outside scene of that island (between those scenes; -1 -1 any) -- <when>
 *                                0 always, 1 in the storm (chapter < 2), 2 in fine weather, 3 with Celebration Island's statue risen, 4
 *                                before it rises -- that file's track is loaded instead of the one before (RaceMod_Island). The first
 *                                that fits is raced. A file with track= lines and no start line of its own is a set: its car's
 *                                handling is kept on an island with no track. (track= and tired= are the set's: they stay through the
 *                                tracks' changes)
 *     gate=<var> <least> <text>  the start line counts no lap until game variable <var> is at least <least>; until then crossing it
 *                                shows <text> (Mr. Paul's racing gloves, the aliens' track ready the next day). Up to four
 *     beat=<n> <var> <value> [<var> <value>]...  opponent n (1 the first) has no car on the track: its lap, at its skill, is the time to
 *                                beat (shown with its name); a lap under it sets the variables, once
 *     beat_text=<text>           the island's text that tells the time to beat: its "##" is that time (MESSAGE.CPP GetText, RaceMod_Text)
 *     parade=<n> <var> <value>   (2026-10-06) opponent n -- the time to beat's -- laps on its own at its skill in the scenes' copies of a
 *                                car (parade_actor=<scene> <actor>, each scene's) while game variable <var> is below <value>, then its
 *                                car stands parked (parade_park=<scene> <x> <y> <z> <turn>) or out of sight (ParadeStep): Raph on
 *                                Citadel Island's storm track, until Twinsen comes to talk to him
 *     race_laps=<n>              the race (after the grid's GO) is n laps long: the player's place is shown when he finishes ...
 *     win=<var> <value> [<var> <value>]...   ... and a win sets these variables
 *     tired=<var> <value> <text> while game variable <var> is <value>, on Citadel Island everyone Twinsen talks to says <text> (of the
 *                                island's texts) instead of their own line (RaceMod_Dial)
 *     main=<n>                   opponent n is the one to beat: the display says whether Twinsen is ahead of him, and win= is finishing
 *                                ahead of him (not first)
 *     finish=<var> <value>...    what the race's end sets, won or not
 *   The opponents (2026-10-03, later): opponent_pace=<lo> <hi> draws its skill between the two each time the track is loaded (the main
 *   opponent's is one number); opponent_small=<generic body> its car shrunk to half its size (the lightning spell).
 *   Power-ups (powerups=1, mushroom=<scene> <actor> [<y>], penguin=<scene> <actor>): small brown mushrooms along the lap, the game's own
 *   (built out of sight, stood on the road at <y> here),
 *   each with a power-up inside that a car driving over it gets -- Gazogem fuel (a boost), the protection spell (30 s: no mine, penguin or
 *   lightning hurts the car), the lightning spell (the other cars shrunk and slowed, 15-25 s: longer the further ahead), a clover (life and magic full), health, a
 *   nitro penguin (dropped behind the car, it goes off 2 s later and stops every car near it) and the super jet-pack (the game drives for
 *   10 s, faster). It grows back a while later. An opponent that drives over one gets a boost, drops a penguin or shrinks Twinsen.
 *   The lightning spell fills Twinsen's magic too (the game's own spell takes it all), with the game's lightning flash.
 *   Oil (oil=<scene> <actor>: the slicks' copies in each scene): dropped behind the car, a slick lies on the road until a car drives over
 *   it, and that car skids for RACE_SKID_MS -- once round and slowing to a standstill, sliding on the way it went (Twinsen's keys left
 *   out). The protection spell and the super jet-pack keep a car off it.
 *   Twinsen holds the oil or a penguin he picks up until he drops it, with either Shift (the left one is the game's inventory key: while
 *   he holds something, and until the key is let go after a drop, it doesn't open the inventory). The test pilot drops it a second on.
 *   A penguin dropped (penguin_mode=walk, the default) walks the track at random -- along the first opponent's line, this way and that,
 *   a little to one side or the other -- until a car comes near it, and goes off; penguin_mode=fuse: it goes off a second after it is
 *   dropped.
 *   No one dies in a race (RaceMod_NoHarm): a car a penguin blows up stops with the game's own hit reaction, as Twinsen's does on a land
 *   mine (HitObj: the hit animation and the stars, no life lost; an opponent's car its own hit animation), and a long fall or a lightning
 *   strike stops Twinsen's car the same way instead of killing him.
 *   A startline or checkpoint line may end in a ninth number, the road's height there: the line then only counts for a car within
 *   RACE_LINE_REACH of that height (a lap that passes over itself crosses a line's place at other heights too).
 *   A sprint (2026-10-06, Polar Island's dream race: the route has two ends and is driven once):
 *     sprint=1                   the opponents' lines run from the start line to the route's end, not round a lap
 *     finishline=<as startline>  the race is over when the player crosses it -- or when the one to beat (main=) gets there first, which
 *                                loses it. An opponent stops at it. The player's car stops there too (RaceMod_Held)
 *     intro=<text>               said by Twinsen as the grid forms, before the count-down (of the race's island's texts)
 *     wake=<scene> <text> [<actor>]   a win: RACE_SPRINT_END_MS after the line, Twinsen is out of his car and wakes up in <scene>, at its
 *                                own start (the scene started afresh: its opening can lay him on his bed); <actor> says <text> there (of that scene's island's texts) before the first line it says --
 *                                Zoe shakes him awake as she comes up to him in the game's own opening -- or, if it says none, a while
 *                                after he woke. A loss: the race is run again from the grid (lose=<text>: said first, of the race's
 *                                island's texts)
 *     dream=<scene>              (a set's key) a new game starts in <scene> instead of Twinsen's house (RaceMod_NewGameScene)
 *     after_gears=<top>...       the gears' top speeds once Twinsen has woken up (the race's own are faster: a dream's car): at the wake
 *                                the race's track is put away -- its finished race, the power-ups' effects, its weather -- and the car
 *                                keeps its handling with these gears
 *     wake_flight=<ms>           a win wakes Twinsen up <ms> after the line instead, the car not held there -- the finish line at a carried
 *                                jump's lip: he never lands -- the picture fading to white (WhiteFade) and the waking scene fading in
 *                                from white (RaceMod_FadeFromWhite: Polar Island's jump into the rocky peak, 2026-10-06)
 */
#include "C_EXTERN.H"
#include "COMMON.H"
#include "AMBIANCE.H"
#include "INPUT.H"
#include "RACEMOD.H"
extern void ClrHoloPos(S32 num);   // HOLOGLOB.CPP (HOLO.H itself clashes with C_EXTERN.H)
extern void DoImpact(S32 num, S32 x, S32 y, S32 z, U8 owner);   // IMPACT.CPP
extern void InitBody(U8 gennewbody, U8 numobj);                  // OBJECT.CPP
extern void HitObj(U8 numhitter, U8 num, S32 hitforce, S32 beta); // OBJECT.CPP
extern void *GivePtrBody(S32 index);                              // OBJECT.CPP
extern void *GivePtrAnim(S32 index);                              // OBJECT.CPP
extern void InitHitExtraPof(S32 x, S32 y, S32 z, S32 hitforce);   // OBJECT.CPP
extern S32 InitIncrustDisp(S16 type, S16 num, S32 x, S32 y, S16 info, S16 move, S32 timeout);   // INCRUST.CPP
extern void Dial(S32 text, S32 drawscene);                       // MESSAGE.CPP
extern void LeaveBuggy(U8 newcomportement);                      // BUGGY.CPP
extern void ResetBuggy(void);                                    // BUGGY.CPP
#define RACE_PENGUIN_IMPACT 15             // the impact a nitro penguin goes off with (the land mines')

#include <POLYGON/POLY.H>
#include <3D/CAMERA.H>
#include <3D/DATAMAT.H>
#include <3D/LIGHT.H>
#include <3D/LPROJ.H>
#include <SVGA/BOX.H>
#include <SVGA/CLIP.H>
#include <SYSTEM/LOG.H>
#include <SVGA/FONT.H>
#include <SVGA/SCREEN.H>
#include <SYSTEM/KEYBOARD.H>
#include <SYSTEM/KEYBOARD_KEYS.H>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <ctime>

static int s_checked = 0;
static int s_active = 0;
static T_RACE_CAR s_car;

static S32 s_gear = 1;
static int s_lastX = 0, s_lastZ = 0;       // the shift keys' state last frame

static S32 s_laps = 0;                     // laps started (0: the start line not crossed yet)
static S32 s_lapStart = 0, s_lastLap = 0, s_bestLap = 0;

// a line across the road: the start line, the checkpoints
typedef struct {
    S32 CubeX, CubeZ, X0, Z0, X1, Z1, DirX, DirZ;
    S32 Y, HasY;                           // the road's height at the line, when the file gives it
} T_RACE_LINE;
#define RACE_LINE_REACH 1200
static S32 s_lineY = 0, s_lineHasY = 0;    // the start line's
// the checkpoints, one in the middle of each corner (the builder's), and the next one the car has to cross: a lap counts once the car
// has crossed them all in order -- cutting a corner, it goes round the line's inner end. Drawn as red lines across the road while
// checkpoint_lines=1 (for now: they are to be unseen).
#define RACE_MAX_CHECKPOINTS 64
static T_RACE_LINE s_checkpoints[RACE_MAX_CHECKPOINTS];
static int s_nbCheckpoints = 0, s_next = 0, s_showCheckpoints = 0;
// ... and whether the car has been halfway round the line it follows (the first opponent's, or the guide): with no checkpoints a lap
// counts then; and where the car is along the lap (PlayerProgress)
static int s_halfway = 0;

// a short message on the display ("Missed checkpoint 3") and until when
static char s_message[64];
static S32 s_messageUntil = 0;

// A car going round a vertical loop (LoopStep): the player's, and each opponent's.
#define RACE_MAX_LOOPS 8                   // (4 until 2026-10-07: the Island of the Francos has five)
#define RACE_MAX_ARCS 4
typedef struct {
    int State;                             // LOOP_RIDE on the ring, LOOP_AIR flying inside it or over its gap, LOOP_FALL off it, outside it
    double Theta, V;                       // on the ring: how far round from the foot (radians) and how fast along it (units/s)
    double Lat, Steer;                     // how far across the ring's band from its middle (+ right of the lap's way), and the car turned across it
    double A, U, C, VA, VU, VC, Polar;     // in the air: along the way and up from the foot, across, how fast each way, and the angle round the ring
    int Inside;                            // ... inside the ring's line
    double X, Y, Z, VX, VY, VZ, Pitch, Spin; // falling outside the ring (cube-local), and turning as it falls
} T_LOOP_RIDE;
enum { LOOP_NONE, LOOP_RIDE, LOOP_AIR, LOOP_FALL };
enum { STEP_ON, STEP_ROUND, STEP_BACK, STEP_FELL };
static int s_loopOn = -1, s_loopState = LOOP_NONE;

// the opponents: each one's line, where it is along it (world units from the start line), laps begun, the scenes' copies of its car
#define RACE_MAX_OPPONENTS 6
#define RACE_MAX_OPPONENT_SCENES 64
typedef struct {
    int Count;                             // points on its line (0: no line, no opponent)
    S32 *X, *Z, *Y, *V;
    double *Cum;                           // distance from the first point to each point
    double Length;                         // the lap along the line
    double S;
    int Laps, Grid, Pace;
    S32 *R;                                // the line's bend radius at each point (NULL: drive the file's speeds)
    int Top, Grip, Catchup;                // its character (percent) and how much harder it pushes when behind
    S32 LapStart;
    int Scene[RACE_MAX_OPPONENT_SCENES], Actor[RACE_MAX_OPPONENT_SCENES];
    int Scenes;
    char Name[32];
    int AnimIdle, AnimDrive, HasAnims;   // opponentN_anim: the generic animations it stands and drives with (else 0 and 1, the racer car's)
    double PlannedLap;                     // seconds, at its skill
    S32 QualTime;                          // its qualifying time, ms
    // off its line on the grid: the way across from the line to its grid spot, which fades out over its first RACE_MERGE units of
    // driving (it moves onto its line), and the way it faces there
    double MergeX, MergeY, MergeZ, MergeLeft;
    S32 SpotBeta;
    double Speed;                          // how fast it is going (it starts from a standstill and pulls away as the car would)
    int LoopOn;                            // the loop it is going round (-1 none), as the player's car does (LoopStep)
    T_LOOP_RIDE Ride;
    double LoopS[RACE_MAX_LOOPS];          // where each loop's foot is along its line (-1: not on it) ...
    int LoopAt[RACE_MAX_LOOPS];            // ... and the line's point there
    double ArcS0[RACE_MAX_ARCS], ArcS1[RACE_MAX_ARCS]; // each carried jump along its line, foot to foot (-1: none)
    int PaceLo, PaceHi;                    // its skill drawn between these when the track loads (opponent_pace=<lo> <hi>; 0: Pace as given)
    S32 BoostUntil, HitUntil;              // a power-up's boost, and a penguin's blast that stopped it, until when
    S32 TakeAt;                            // when it last took a mushroom's power-up (one a row)
    S32 JetUntil; double JetFactor;        // a jet-pack: its pace times JetFactor until then
    S32 ShoveStart, ShoveUntil;            // pushed aside by Twinsen's super jet-pack: since when, until when ...
    double ShoveX, ShoveZ;                 // ... and which way (world, a unit long)
    S32 SkidUntil, SkidStart; int SkidDir; // skidding on oil: until when, since when, and which way it spins ...
    double SkidSpeed;                      // ... and how fast it was going
    int Small, Normal;                     // its car's shrunk generic body (-1 none) and its own (-1: not seen yet)
    S32 ShrunkUntil;                       // shrunk by Twinsen's lightning until when (ShrinkMs: longer the further ahead it is)
    double FinishS;                        // a sprint's: where its line crosses the finish line (it stops there)
} T_RACE_OPPONENT;
static T_RACE_OPPONENT s_opp[RACE_MAX_OPPONENTS];
// With no opponent raced, the line of the one to beat all the same (guide_path=, guide_top=, guide_grip=: the builder writes it then),
// for what follows a line -- the jet-pack's driving, the penguins' walks, the laps, the rescue. The first opponent's when there is one.
static T_RACE_OPPONENT s_guide;
// this scene's actor of each opponent's car, as placed this frame (-1 none), and the scene (RaceMod_FreeOfCube)
static int s_carActor[RACE_MAX_OPPONENTS] = {-1, -1, -1, -1, -1, -1};
static S32 s_carsScene = -1;
static const T_RACE_OPPONENT *Guide(void) { return s_opp[0].Count ? &s_opp[0] : s_guide.Count ? &s_guide : NULL; }
static int s_oppStarted = 0;               // (no grid: the race has started when the player first drove off)
#define RACE_MERGE (12 * 512)

// the grid and the start: a qualifying lap (when the file asks for one), then the grid and its count-down, then the race
#define RACE_MAX_SPOTS 6
typedef struct {
    S32 CubeX, CubeZ, X, Y, Z, Beta;
} T_RACE_SPOT;
static T_RACE_SPOT s_spots[RACE_MAX_SPOTS];
static int s_nbSpots = 0;
static T_RACE_SPOT s_pits[RACE_MAX_SPOTS];
static int s_nbPits = 0;
enum { PHASE_RACE, PHASE_QUALIFYING, PHASE_WAIT, PHASE_GRID };
static int s_phase = PHASE_RACE;
static int s_qualifying = 1;
static int s_fineWeather = 0;
static int s_holoArrow = -1;
static unsigned s_seed = 0;
static S32 s_qualifyTime = 0;              // the player's, ms
static int s_wantGrid = 0;                 // form the grid (asked for by the lap, done before the next drawing)
static S32 s_gridAt = 0, s_goAt = 0;       // when the grid was formed, and when the count-down says GO
static int s_order[RACE_MAX_OPPONENTS + 1]; // the grid, pole first: -1 the player, n the opponent n
static S32 s_orderTime[RACE_MAX_OPPONENTS + 1];
static int s_nbOrder = 0;
static int s_forgetLap = 0;                // the player's car was moved: the lap check forgets its last position
#define RACE_MAX_HIDDEN 256
static int s_hideScene[RACE_MAX_HIDDEN], s_hideActor[RACE_MAX_HIDDEN];
static int s_nbHidden = 0;
#define RACE_RESULTS_MS 4000               // how long the qualifying's grid is shown before the count-down
#define RACE_QUALIFY_SPREAD 3000           // an opponent's qualifying time: its lap at its skill, this many ms either way at random
#define RACE_QUALIFY_SHARE 0.1             // ... or this share of that lap, if less (a short lap: the lava lake's is 10 s)
static S32 s_oppLast = 0;
static int s_playerIndex = -1;             // the first opponent's line point nearest the player, for the race position
// The whole scene is redrawn while the opponent moves: with the camera standing still, only what moves is redrawn over a copy of the
// scene, and the car, drawn into that copy while it waited on the grid, stayed there as a picture after it set off. Asked for while
// drawing (RaceMod_Frame) and set from the buggy's move before the next frame's drawing (RaceMod_Lap): the main loop resets FirstTime
// right after drawing, so setting it while drawing had no effect.
static int s_wantFlip = 0;

// The story's keys (see the file's description): the gates, the time to beat, the race's length and its result, the tired Twinsen, and
// the folder's tracks.
#define RACE_MAX_GATES 4
#define RACE_MAX_SETS 4
#define RACE_MAX_TRACKS 16
typedef struct { int Var, Least; char Text[64]; } T_RACE_GATE;
static T_RACE_GATE s_gates[RACE_MAX_GATES];
static int s_nbGates = 0;
// Game variables held while the race-track mode runs (vargame=<n> <value>): an island's travel cutscenes kept from playing in a race --
// Otringal's square island plays the Dino-Fly's landing (Twinsen held at its pad while the Dino-Fly comes in) whenever variable 83 is
// under 2, and the car flew into it off the jump over the sea and stood there 11 s (2026-10-08).
#define RACE_MAX_HELD 16
static int s_held[RACE_MAX_HELD][2];
static int s_nbHeld = 0;
static int s_beatOpp = -1;                 // the opponent whose lap is the time to beat (-1 none)
// beat_text=: the text (the island's) that tells the time to beat -- its "##" is that time, as the display shows it (RaceMod_Text)
static int s_beatText = -1;
// parade=: a driver lapping on his own -- the time to beat's driver (no car in the race), his line at his skill -- in a car of the scenes
// (parade_actor=, each scene's), while a game variable is below a value; then his car stands parked (parade_park=, in one scene) or out of
// sight. Citadel Island's storm track: Raph laps until Twinsen comes to the start line on foot to talk to him (RaceTrackStory).
#define RACE_MAX_PARADE 16
static int s_paradeOpp = -1, s_paradeVar = -1, s_paradeValue = 0;
static int s_paradeScene[RACE_MAX_PARADE], s_paradeActor[RACE_MAX_PARADE], s_nbParade = 0;
static int s_paradeParkScene = -1;
static S32 s_paradeParkX, s_paradeParkY, s_paradeParkZ, s_paradeParkBeta;
static double s_paradeS = 0, s_paradeSpeed = 0;
static int s_paradeActorNow = -1;          // (its car in this scene: nothing collides with it, RaceMod_NoCollide)
static int s_paradeFlip = 0;               // (the next frame drawn whole: RaceMod_Story)
static int s_beatSet[2 * RACE_MAX_SETS], s_nbBeatSet = 0;
static int s_raceLaps = 0;                 // the race's laps (0: laps go on being counted, no finish)
static int s_winSet[2 * RACE_MAX_SETS], s_nbWinSet = 0;
static int s_place = 0, s_racers = 0;      // the player's place when he finished the race (0: not finished)
static int s_main = -1;                    // the opponent to beat (main=; -1 none) ...
static int s_beatMain = 0;                 // ... and whether the player finished ahead of him
static int s_finishSet[2 * RACE_MAX_SETS], s_nbFinishSet = 0;
// A sprint (sprint=, finishline=): the race from the start line to the finish line, once. Each opponent's line ends where it crosses the
// finish (FinishS); the race's end -- the player over the line, or the one to beat there first -- is followed, RACE_SPRINT_END_MS later,
// by the dream's waking up (wake=) or the race run again (a loss).
#define RACE_SPRINT_END_MS 3500
#define RACE_WAKE_DIAL_MS 15000            // the wake line: before the waking scene's actor's first line, or this long after Twinsen wakes up
static int s_sprint = 0, s_hasFinish = 0;
static T_RACE_LINE s_finishLine;
static S32 s_endAt = 0;                    // when the race's end is followed up (0: not yet)
static int s_endWon = 0;
static int s_wakeFlight = 0;                // wake_flight= (ms; 0: RACE_SPRINT_END_MS, the car held at the line)
static int s_fadeFromWhite = 0;             // the waking scene fades in from white (set by the wake; RaceMod_FadeFromWhite takes it)
static S32 s_whiteUntil = 0;                // ... once its opening has laid Twinsen in his bed: the picture held white till then (RaceMod_Story)
#define RACE_WHITE_HOLD_MS 400
static int s_wakeScene = -1, s_wakeText = -1, s_wakeActor = -1, s_introText = -1, s_loseText = -1, s_introShown = 0;
// ... and once Twinsen is on his way to the waking scene: kept through a change of track (the set's island loads another)
static int s_pendWakeScene = -1, s_pendWakeText = -1, s_pendWakeActor = -1;
static S32 s_pendWakeAt = 0;
static int s_pendLose = -1;
static int s_afterGears[RACE_MAX_GEARS], s_nbAfterGears = 0;   // after_gears=
static int s_dreamScene = -1;              // dream=: the scene a new game starts in (-1: Twinsen's house, the game's own)

// Power-ups (powerups=): the mushrooms along the lap, scene by scene, each with the height it stands at and when it grows back after a car
// took its power-up; the penguins dropped; and what the player has on.
#define RACE_MAX_MUSHROOMS 512
// how near a car's middle comes to one to take it: further than the car's own box reaches (the engine stops a car at any object's box,
// CheckObjCol, so a mushroom has to be gone before the car touches it -- the lava lake's test pilot sat jammed against one)
#define RACE_MUSHROOM_REACH 1000
#define RACE_MUSHROOM_BACK 15000           // ms until it grows back
typedef struct { int Scene, Actor; S32 Y; int HasY; S32 Back; } T_RACE_MUSHROOM;
static T_RACE_MUSHROOM s_mush[RACE_MAX_MUSHROOMS];
static int s_nbMush = 0;
static int s_powerups = 0;
static int s_fuelAlways = 0;               // fuel_always=1: every car on Gazogem fuel all race, and no mushroom gives it (the Island of the Francos', 2026-10-07)
static int s_prizeForce = -1;              // roulette_prize=<0|1|2> (tests): every drop's prize black, red or the green
static int s_powerupOnly = -1;             // powerup_only=<n> [<n>...] (tests): every mushroom has that one -- or one of those, at random
static int s_onlyList[8], s_nbOnly = 0;
#define RACE_MAX_PENGUIN_ACTORS 192
static int s_penguinScene[RACE_MAX_PENGUIN_ACTORS], s_penguinActor[RACE_MAX_PENGUIN_ACTORS];
static int s_nbPenguinActors = 0;
#define RACE_MAX_PENGUINS 6
#define RACE_PENGUIN_FUSE 1000
#define RACE_PENGUIN_REACH (4 * 512)
#define RACE_PENGUIN_HOLD 1500
// a penguin dropped: where (world units), when it goes off (a fuse's) or may first (a walker's), and its walk -- along the first
// opponent's line at S (Dir the way), Side to the right of it going to SideTo, turning at NextTurn; or (no line) round where it was dropped
typedef struct {
    double X, Y, Z; S32 At; int On;
    double S, Side, SideTo, HomeX, HomeZ, Heading; int Dir; S32 NextTurn, Dropped;
    int Dropper;                           // the car that dropped it: -1 Twinsen's, else the opponent's
} T_RACE_PENGUIN;
static int s_penguinWalk = 1;              // penguin_mode=walk (1) or fuse (0)
#define RACE_PENGUIN_ARM 1000              // the car that dropped it can't set it off for this long (it is away by then); any other car at once ...
#define RACE_PENGUIN_TRIGGER 1100          // ... when its middle comes this near it
#define RACE_PENGUIN_BUMP 500              // another penguin or a mushroom this near: it has run into it
#define RACE_PENGUIN_WALL 512              // the ground this much higher than it stands: a wall it has walked into
#define RACE_PENGUIN_WALK 450.0            // how fast it walks (units a second)
#define RACE_PENGUIN_SIDE (1.5 * 512)      // how far to either side of the line it wanders
#define RACE_PENGUIN_HOME (3 * 512)        // ... or, with no line, round where it was dropped
// Twinsen's items: two slots, each a power-up (-1 empty) and when its roulette stops (it can be used from then); the slot Shift uses
// (Q selects the other); when he last took a mushroom's item (a car takes one from a row: the mushrooms beside it give way, empty); and
// Shift and Q last frame
#define RACE_ITEM_SLOTS 2
// the item box's roulette: this long, this many items going by, fast at first and slowing to the one drawn (the user, 2026-10-08: "let's
// have this go through them a lot faster so it only takes 4-5 seconds to decide on a powerup" -- 20 items; and RACE_ROLL_MS, 1,500,
// was the green ball's 6,000 from its define on, that name's second: six seconds)
#define RACE_ITEM_ROLL_MS 4500
#define RACE_ITEM_ROLL_STEPS 45
#define RACE_ROW_MS 700                    // one item a row
static int s_item[RACE_ITEM_SLOTS] = {-1, -1};
static S32 s_itemAt[RACE_ITEM_SLOTS];
static int s_sel = 0;
static S32 s_takeAt = 0;
static int s_shiftWas = 0, s_shiftBlock = 0, s_qWas = 0;
static int s_oilIcon = -1;                 // oil_icon=: the oil's model in OBJFIX.HQR, for the item box (the builder adds it)
static S32 s_autoDrop = 1000;              // autodrop_ms=<ms> (tests): how long after its roulette the test pilot uses an item (0: never)
static T_RACE_PENGUIN s_penguins[RACE_MAX_PENGUINS];
// (health and clovers were power-ups too: no one dies in a race, so they did nothing -- out since 2026-10-03)
// (the super jet-pack: the game drives, at twice the car's speed; the plain jet-pack, one and a half times it, is out since 2026-10-04)
// (2026-10-08, the user: "an attacking powerup for cars to hit other drivers with utilising Twinsen's magic ball and it's different
// colours with different behaviours" -- the yellow ball darts a short way straight ahead, the green rolls along the track bouncing from
// rail to rail for a few seconds, the red homes in on the car in front, the fire ball splits into one for each of the other cars, each
// hunting its own down (Shots); and "a fake powerup mushroom ... identical to the real powerup mushrooms, the car can drop this powerup
// anywhere, and when another car drives into it the fake mushroom explodes" (Fakes))
// (2026-10-10: the golden Gazogem, the roulette wheel's green zero's prize -- the user: "if they get the green super jackpot they get a
// golden gazogem bottle where they can use the power up for a speedboost as many times as they like for 8 seconds"; last, and never drawn
// from a mushroom nor the item box's roulette, PU_ROLLED)
enum { PU_FUEL, PU_PROTECT, PU_LIGHTNING, PU_PENGUIN, PU_SUPERJET, PU_OIL, PU_BALL_YELLOW, PU_BALL_GREEN, PU_BALL_RED, PU_BALL_FIRE, PU_FAKE, PU_GOLD, PU_COUNT };
#define PU_ROLLED PU_GOLD
static const char *const s_puName[PU_COUNT] = {"Gazogem fuel!", "Protection spell!", "Lightning spell!", "Nitro penguin!", "Super jet-pack!", "Oil slick!",
                                               "Yellow magic ball!", "Green magic ball!", "Red magic ball!", "Fire ball!", "Fake mushroom!", "Golden Gazogem!"};
static const int s_puWeight[PU_COUNT] = {20, 12, 10, 16, 12, 16, 12, 10, 9, 5, 10, 0};
static const double s_puJet[PU_COUNT] = {0, 0, 0, 0, 2.0, 0, 0, 0, 0, 0, 0, 0};
// each one's weight times these for the leader and for the last car (between them by the car's place: RandomPowerUp) -- the ones to
// drop behind (penguins, oil, a fake mushroom) for the cars in front, the ones to catch up with (the super jet-pack, lightning, the red
// ball and the fire ball) for the cars at the back
static const double s_puLead[PU_COUNT] = {1.0, 0.8, 0.05, 1.8, 0.0, 2.0, 1.2, 1.0, 0.3, 0.0, 1.8, 0};
static const double s_puLast[PU_COUNT] = {1.0, 1.2, 2.0, 0.4, 2.5, 0.4, 0.8, 1.0, 1.8, 2.4, 0.5, 0};
// what the item box shows (OBJFIX.HQR, the inventory's own: the Gazogem, the protection spell, the lightning ring, the meca-penguin,
// the super jet-pack, the magic ball in each of its colours -- INVENT.CPP's TabObj3D for FLAG_BALLE_MAGIQUE, {1, 40, 41, 42}, picked by
// MagicLevel - 1: until 2026-10-08 the balls had 0-3 here, OBJFIX's own 0-3, the holomap, the yellow ball, the dart and Sendell's ball;
// -2 the oil's, oil_icon=; -3 the mushroom's own body; -4 the golden Gazogem, gold_icon=: the Gazogem in gold)
static const int s_puModel[PU_COUNT] = {15, 39, 19, 14, 48, -2, 1, 40, 41, 42, -3, -4};
static int s_goldIcon = -1;                // gold_icon=: the golden Gazogem's model in OBJFIX.HQR (the builder adds it)
#define RACE_GOLD_MS 8000                  // the golden Gazogem: from its first use, this long to boost with it as often as wanted ...
#define RACE_GOLD_BURST_MS 1200            // ... each use Gazogem's boost this long ...
#define RACE_GOLD_INJECT (10 * 5120 / 36)  // ... and 10 km/h more speed at once (the dial's km/h: speed * 36 / 5120; the user, 2026-10-10:
                                           // "every time the user presses the powerup button they get an instant 10 kmph speed injection"),
                                           // with no top speed while it runs ("Remove the top speed limit whilst we have golden gazogem,
                                           // let's allow the user to inject as much speed as they can handle": RaceMod_NoTopSpeed)
#define RACE_GOLD_AUTO_MS 600              // (the test pilot: once in this long)
static S32 s_goldUntil = 0, s_goldLast = 0;
static int s_slotOn = 0;                   // the roulette wheel's slot machine spinning (SlotSpin)
// oil: the slicks on the road (world units; until when), and the scenes' copies of the slick that show them
#define RACE_MAX_SLICKS 16                 // (6 until 2026-10-07: the pipes' drips have slicks too)
#define RACE_MAX_OIL_ACTORS 256
#define RACE_OIL_REACH 760                 // how near a car's middle comes to a slick's to drive over it
// (2026-10-04: once round and to a standstill -- it slid on spinning three times, too much of a penalty)
#define RACE_SKID_MS 1400                  // how long a car skids: it turns round once, quickly at first, and runs down to a stop
// (Y: the road's height under it once it is in sight -- PoseSlick -- before that the dropping car's; Posed: the scene it was posed in,
// -1 not yet; Alpha, Gamma: tilted to the road's slope; Lift: its middle over the road, so that no part of it is under the road)
// Until: when it dries up (a drip's), 0 never
typedef struct { double X, Y, Z; S32 Dropped; int On; S32 Posed, Alpha, Gamma; double Lift; S32 Until; } T_RACE_SLICK;
static T_RACE_SLICK s_slicks[RACE_MAX_SLICKS];
// The magic balls in flight (Shots): each one's kind (its colour), whose (-1 Twinsen's, else the opponent's), what it is after (-1
// Twinsen, an opponent, -2 none), where it is (world units) and which way it goes; the guide line's S and how far to its side for those
// that follow the track; its sprite in this scene (-1 none) and when it last left a trail
#define RACE_MAX_SHOTS 16
#define RACE_SHOT_REACH 950                // a car this near a ball is hit
#define RACE_SHOT_FORCE 20
// (the ball's sprite: Twinsen's magic ball, without a trail -- "It looks like we're using a different model for the magic ball to the
// in-game one, let's switch to Twinsen's normal ball" -- at 16, four and a half times the game's own size (70: ThrowExtra's -1), since
// "The magic ball powerup when thrown seems very small, let's make this a lot bigger and faster"; 22 with a trail until 2026-10-08, then
// -1 for a while)
#define RACE_SHOT_SCALE 16
#define RACE_SHOT_RISE 450                 // over the road
// (the speeds since 2026-10-08, the user: "it moves too slowly to catch up with the car in front ... especially for the red and fire
// balls so they home in on their target" -- 11,000, 9,500 and 10,500 before; a car's top speed is about 4,800, the super jet-pack's
// twice that)
// (and faster again the same evening: 15,000, 20 cells, 12,500, 16,000 and 5,000 for a while)
#define RACE_DART_SPEED 22000.0            // the yellow: straight ahead, this fast, ...
#define RACE_DART_RANGE (56 * 512.0)       // ... this far (28 cells until 2026-10-10: the user, "increase the cells the yellow ball is
                                           // active to 56, double what it is now")
#define RACE_ROLL_SPEED 18000.0            // the green: along the track, this fast, ...
#define RACE_GREEN_MS 10000                // ... this long (6 s until 2026-10-10), ...
#define RACE_ROLL_SIDE (2.6 * 512)         // ... from side to side of the road ...
#define RACE_ROLL_ACROSS 2600.0            // ... this fast
#define RACE_HOME_SPEED 24000.0            // the red and the fire: along the track after its car, this fast ...
#define RACE_HOME_DIRECT 6000.0            // ... straight at it once this near
#define RACE_HOME_MS 15000                 // ... and gone after this long -- only the red with no car in front: the red goes on till it
                                           // hits the car it is after and the fire's till each hits its own (2026-10-10, the user: "Red ball
                                           // to last until it hits the car in front, and fire balls to always hit all cars")
enum { SHOT_YELLOW, SHOT_GREEN, SHOT_RED, SHOT_FIRE, SHOT_LASER_GREEN, SHOT_LASER_RED };
// Twinsen's laser cars (drive_laser=1 green, 2 red): his own car, its gun firing laser bolts instead of its magic balls -- the bolts' models
// (laser_models=<green> <red>, OBJFIX.HQR: the builder adds them) flown straight ahead, one for each shot of the gun's firing animation
// (FICHE.CPP: RaceMod_LaserThrow)
#define RACE_LASER_SPEED 17000.0
#define RACE_LASER_RANGE (34 * 512.0)
#define RACE_LASER_MUZZLE 420.0            // a bolt starts this far ahead of the car (the gun's muzzle, on its bonnet)
#define RACE_LASER_REACH 1050
static int s_driveLaser = 0, s_laserModel[2] = {-1, -1};
static int LaserExtra(int model, S32 x, S32 y, S32 z);
typedef struct { int On, Kind, Owner, Target, Extra; double X, Y, Z, Hx, Hz, S, Side, SideV, Travelled; S32 T0, Trail; } T_RACE_SHOT;
static T_RACE_SHOT s_shots[RACE_MAX_SHOTS];
static void LaunchShot(int kind, int owner, int target, double x, double y, double z, double hx, double hz, S32 now);
static void Attack(int pu, int owner, double x, double y, double z, double beta, S32 now);
// The fake mushrooms (Fakes): where each lies (world units), who dropped it and when (it can't go off under that car for a moment), the
// scene its height was found in; shown by the scenes' copies of the mushroom (fake=), the box a point; the mushroom's body for the item box
#define RACE_MAX_FAKES 8
#define RACE_MAX_FAKE_ACTORS 256
#define RACE_FAKE_ARM 1200
typedef struct { int On, Dropper; double X, Y, Z; S32 Dropped, Posed; } T_RACE_FAKE;
static T_RACE_FAKE s_fakes[RACE_MAX_FAKES];
static int s_fakeScene[RACE_MAX_FAKE_ACTORS], s_fakeActor[RACE_MAX_FAKE_ACTORS];
static int s_nbFakeActors = 0, s_fakeIconBody = -1;
static void DropFake(double x, double y, double z, int dropper, S32 now);
extern void MagicBallExplo(S32 x, S32 y, S32 z, S32 beta);  // EXTRA.CPP
extern void SampleMagicBall(S32 x, S32 y, S32 z);           // FICHE.CPP
static int s_oilScene[RACE_MAX_OIL_ACTORS], s_oilActor[RACE_MAX_OIL_ACTORS];
static int s_nbOilActors = 0;
static S32 s_skidUntil = 0, s_skidStart = 0, s_skidBeta = 0, s_skidSpeed = 0;
static int s_skidDir = 1;
#define RACE_BOOST_MS 4000
#define RACE_PROTECT_MS 12000              // (30 s until 2026-10-10: the user, "decrease the length the protection spell lasts for, perhaps
                                           // 12 seconds, let's also have it no longer cancel a shrink" -- it still keeps a new strike off)
#define RACE_SHRINK_MS 20000
#define RACE_SHRINK_SPREAD 0.25            // ... longer for a car ahead, shorter for one behind: the leader's x1.25, the last's x0.75
#define RACE_JETPACK_MS 10000
#define RACE_BOOST 1.5                     // the top speed and the pull with Gazogem fuel
#define RACE_SHRUNK 0.7                    // a shrunk car's
#define RACE_FLASH_MS 700                  // the lightning spell's flash
#define RACE_BOLT_MS 600                   // ... and its bolts onto the cars it strikes
#define RACE_GOLD_EDGE 244                 // (the palette's gold: the bolts' edges)
#define RACE_BLAST_FORCE 20                // a penguin's blast, as HitObj takes it
static S32 s_boostUntil = 0, s_protectUntil = 0, s_shrunkUntil = 0, s_jetUntil = 0;
static double s_jetFactor = 1;             // the jet-pack on: the car's speed times this (s_puJet) -- easing back to 1 at its end (JetFactor)
static double JetFactor(S32 until, double full);
// ... and the car turned into it: the super jet-pack's model at about the car's size (superjet_model=, OBJFIX.HQR: the builder adds it),
// drawn in the car's place, lying flat (the build lays it so), while Twinsen's car is hidden
static int s_superModel = -1, s_heroHidden = 0, s_superShown = 0;
// Driving as another car (drive_as=<BODY.HQR body> <ANIM.HQR animation> [<BODY.HQR shrunk body>]: a test drive in one of the opponents'
// cars): Twinsen's car hidden as it is under the super jet-pack, that car drawn in its place with the opponents' driving animation,
// pitched with the way the car climbs or falls (DrawDriveAs) -- and its shrunk body while an opponent's lightning has him shrunk.
static int s_driveBody = -1, s_driveAnim = -1, s_driveSmall = -1, s_driveShown = -1, s_driveSmallNow = 0;
static T_OBJ_3D s_driveObj;
static double s_drivePitch = 0;
static S32 s_drivePrevX, s_drivePrevY, s_drivePrevZ;
// Twinsen's car at half its size, while an opponent's lightning has him shrunk (twinsen_small=: the buggy's own body -- the buggy
// behaviour's entity's GEN_BODY_TUNIQUE -- halved, its generic body; the builder adds it)
static int s_heroSmall = -1, s_heroIsSmall = 0;
#define RACE_SUPERJET_LEAN 0.0             // radians more it leans forward: none -- the build lays the model flat (RaceTrackSuperJet.LaidFlat)
#define RACE_JET_EASE_MS 2500              // its last this long: the speed eased back to the car's own, and the jet-pack flashing
#define RACE_JET_FLASH_MS 250              // ... the car and the jet-pack this long each in turn
static double s_superLean = RACE_SUPERJET_LEAN;          // superjet_lean= (radians, tests)
static S32 s_superTurn = 0;                // superjet_turn= (the model turned on the car's heading, 4096 a turn; tests)
#define RACE_SUPERJET_LIFT 560             // its middle over the road (lying flat, its underside about a hundred over)
// ... and a car in its way pushed aside, the super jet-pack driving through it: how near, how far aside, for how long
#define RACE_SHOVE_REACH 1400
#define RACE_SHOVE_SIDE (2 * 512)
#define RACE_SHOVE_MS 1500
static int s_protectSpell = 0;             // the game's protection spell is on for the power-up (it goes off with it)
#define RACE_PROTECT_ENDING 3000           // ... and blinks this long before
static int s_tiredVar = -1, s_tiredValue = 0, s_tiredText = -1;
typedef struct { int Island, When, First, Last; char Path[260]; } T_RACE_TRACK;
static T_RACE_TRACK s_tracks[RACE_MAX_TRACKS];
static int s_nbTracks = 0;
static char s_loaded[260] = "";            // the car file loaded ("": none yet)
static int s_ownTrack = 0;                 // it has a track of its own (a start line), not only the car and the set
static void ResetTrack(void);

static void Defaults(T_RACE_CAR *c) {
    memset(c, 0, sizeof(*c));
    memset(s_opp, 0, sizeof(s_opp));
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) { s_opp[n].Grid = 4; s_opp[n].Pace = 100; s_opp[n].Top = 100; s_opp[n].Grip = 100; s_opp[n].Small = -1; s_opp[n].Normal = -1; }
    memset(&s_guide, 0, sizeof(s_guide));
    s_guide.Pace = s_guide.Top = s_guide.Grip = 100; s_guide.Small = s_guide.Normal = -1;
    c->Gears = 1;
    c->GearTop[0] = RACE_REF_TOP;
    c->Accel = 4000;
    c->Brake = 12000;
    c->Coast = 7000;
    c->Reverse = 2000;
    c->Steer = 1024;
    c->Hud = 1;
}

static S32 Milli(const char *v) { return (S32)(atof(v) * 1000.0 + 0.5); }

static int ReadLine(const char *v, T_RACE_LINE *l) {
    const int n = sscanf(v, "%d %d %d %d %d %d %d %d %d", &l->CubeX, &l->CubeZ, &l->X0, &l->Z0, &l->X1, &l->Z1, &l->DirX, &l->DirZ, &l->Y);
    l->HasY = n == 9;
    if (!l->HasY) l->Y = 0;
    return n >= 8;
}

// ---- the raised road --------------------------------------------------------------------------------------------------------------
// A race track's raised road (raised=<file>): a road that stands in the air on piers, passes over itself and climbs at any grade --
// Celebration Island's lap round the statue -- which the ground, one height map, cannot be, and which decor boxes, level and
// axis-aligned, cannot carry on a slope (a car stepping down from one box to the next is dropped by the engine's fall, which stops it
// dead each time). So it is a floor of its own: while an object is moved (RaceMod_FloorFor), the ground's height under it is the
// road's wherever the road passes within its width and no more than RAISED_STEP above the object -- the lap's other levels lie
// thousands over or under it -- and the ground's blocking triangles far below don't count there. The hero is kept RAISED_INSET inside
// its edges (RaceMod_Rail), a rail that needs no collision boxes. The build's decor pieces are only what it looks like.
// A point with no width (Half 0) is in a gap of the road -- a jump's, between its take-off lip and its landing lip (the lava lake's
// causeways, 2026-10-01): a segment that touches one has no floor, no rail and hides nothing.
typedef struct {
    S32 X, Z, Y, Half;
    S32 Bank;                              // its banking: the height gained across it, per RAISED_BANK_UNITS to the left of the way it runs
} T_RACE_RAISED;
#define RAISED_BANK_UNITS 10000.0
static T_RACE_RAISED *s_raised = NULL;
static int s_nbRaised = 0;
#define RAISED_BUCKET 2048                 // the road's points are kept in squares of four cells
#define RAISED_STEP 700                    // how far above an object the road may be and still be its floor
#define RAISED_LEVEL 1200                  // two parts of the road within this of each other's height are one level (a bend's segments)
#define RAISED_PAD 96                      // the floor reaches this far past the road's edge
#define RAISED_INSET 640                   // how far inside the edge the hero's middle is kept: a car's half width
#define RAISED_NOREF 0x7FFFFFFF
static int s_rbX0 = 0, s_rbZ0 = 0, s_rbW = 0, s_rbH = 0;
static int s_rbExtra = 0;                  // squares more to look in for a road wider than one (the Emerald Moon's, as wide as the reactor's dish)
static int *s_rbStart = NULL, *s_rbItem = NULL;
static S32 s_floorRef = RAISED_NOREF;      // the height of what the ground is being asked for (nothing: the ground alone)
static int s_onlyNear = -1;                // RaisedAt looks only at the road's points within RAISED_SIGHT of this one (-1: all of it)
#define RAISED_SIGHT 70                    // 35 cells either way: the stretch of road round the hero that can hide him from the camera
#define RAISED_OWN 24                      // twelve cells either way: the stretch of road a place on it belongs to (RaisedBetween)
#define RAISED_STACK 12                    // six cells either way: a level over the road all that way runs with it (a helix)
#define RAIL_CAMERA_CLEAR 500               // the rail camera's line to the car passes this far over the road between them
#define RAIL_CAMERA_AIM 300                 // ... and it looks at a point this far over the road ahead of the car
#define RAIL_CAMERA_LOOK 0.6                // ... that far of the way from the car to the road railcam's lead ahead
#define RAIL_CAMERA_SIDE 50                 // the rail camera's eye comes nearer round a bend until it looks this near the car's way (degrees)
#define RAIL_CAMERA_NEAREST 2560            // ... but no nearer than this
#define RAISED_UNDER 650                   // how far under such a level's surface the following camera's eye stays (its slab is 200)
static int s_rescueOn = 1;                 // rescue=: a car in the lava or the sea is put back on the road (RaceMod_Rescue)
// island_file=<island> <name>: while racing, that island is drawn from the file <name> instead of its own -- a track on an island file the
// game never loads (MOON.ILE, the Emerald Moon's older copy, raced as island 3's)
static int s_fileIsland = -1;
static char s_fileName[32] = "";
// Vertical loops (loop=, RaceMod_Loop): a ring standing on the road, its foot at the lap's point there (X, Y, Z, cube-local) and its plane
// along the lap's way (DirX, DirZ, a unit vector). R: from the ring's middle to its road surface. Shift: how far across the car comes
// down from where it went in (+ to the right of the way, as the builder's lanes are: in on the left, out on the right, past the ring's
// own foot). Gap: the part of its top that is missing (radians, 0 none), which the car leaps upside down. Band: from the band's middle to
// its rails. Lo, Hi: the speeds at its foot a car with the throttle held gets round a ring with a gap at (LoopWindow), Least the least
// it gets round a whole one at.
typedef struct {
    int CubeX, CubeZ;
    double X, Y, Z, DirX, DirZ, R, Shift, Gap, Band;
    double Lo, Hi, Least;
} T_RACE_LOOP;
static T_RACE_LOOP s_loops[RACE_MAX_LOOPS];
static int s_nbLoops = 0;
// The pipes over the road (steam=, drip=; the Island of the Francos' refinery, 2026-10-07): steam puffing from their tops, each on its own
// time, and oil dripping from them onto the road, where it lies a while as a slick a car skids on (Vents, DripOil). World units from the
// island's corner; only near Twinsen, in the scene he is in.
#define RACE_MAX_VENTS 64
#define RACE_MAX_DRIPS 48                  // (32 until 2026-10-07: the pipeline from the factory drips too)
#define RACE_VENT_NEAR (26 * 512)          // how near Twinsen a pipe puffs and drips (further off, out of sight)
#define RACE_MAX_PUFFS 24                  // the extras the steam takes at most (the game has 50 for everything: penguins, bolts...)
#define RACE_VENT_PUFFS 12                 // ... of them the pipes' tops (the rest the jets': nine gantries in reach, eighteen tops, took them all
                                           // and the jets out of their uprights blew nothing to be seen, 2026-10-07)
#define RACE_DRIP_SLICK_MS 7000            // how long a drip's slick lies on the road if no car takes it
typedef struct { double X, Y, Z; S32 Every, Next; } T_RACE_VENT;
typedef struct { double X, Y, Z, RoadY; S32 Every, Next, LandAt; } T_RACE_DRIP;
static T_RACE_VENT s_vents[RACE_MAX_VENTS];
static T_RACE_DRIP s_drips[RACE_MAX_DRIPS];
static int s_nbVents = 0, s_nbDrips = 0;
static S32 s_puffEnd[RACE_MAX_PUFFS];
static unsigned s_ventSeed = 20261007u;   // (their own dice: the race's, RaceRandom, stay as they were)
// Steam jets across the road (jet=; the Island of the Francos' refinery, 2026-10-07): the Gazogem factory's own steam -- its impact 37,
// a hiss, a puff of steam (sprites 100-109) and a spray of sparks (flow 16), played where its scripts turn their hit zones on --
// blowing in bursts, each its own time (On ms blowing, Off ms not, from Phase). Since the user's next round (2026-10-07: "make it
// sideways ... out of the left side on one lap, then the right on the other"): out of the road's rail, across it -- the left rail
// on the player's odd laps, the right on his even ones -- Reach of the way over (Half: the road's half width; Dir: its way, a thousand
// long), puffs in a row across the road and the sparks blown sideways. A car in that band of the road while it blows is hit as by a
// penguin's blast, held a shorter while -- once a burst. (A jet with no way, jet='s first form, blows up out of the deck as before.)
#define RACE_MAX_JETS 48
#define RACE_STEAM_IMPACT 37
#define RACE_STEAM_SAMPLE 17               // impact 38's -- the factory's jets out of its walls, across its rooms -- its hiss
#define RACE_JET_FIRST 100                 // the factory's jet straight up (impact 37): sprites 100 and 101 growing into 102, laid on its side
#define RACE_JET_SPRITE 102
#define RACE_JET_HIGH 71                   // ... 102's height (its length laid on its side: drawn as long as the jet's reach)
#define RACE_JET_GROW 240                  // ... how long it grows at a burst's start (ms)
#define RACE_STEAM_FLOW 76                 // ... its sparks, a flow blowing on the level the way it is turned
#define RACE_STEAM_HOLD 900                // how long a jet's blast holds a car
#define RACE_STEAM_PUFF 180                // how often a blowing jet puffs again
#define RACE_STEAM_BAND 700                // the band's half depth along the road
#define RACE_STEAM_FORCE 10                // the blast, as HitObj takes it (the factory's hit zones: 10)
// (High: how far over the deck it blows -- out of a gantry's upright, a car's height up, since the user's round after: "coming out of the
// vertical pipes on the track fully horizontally", 2026-10-07; out of the rail, near the deck, before)
#define RACE_STEAM_HIGH 380
typedef struct { double X, Y, Z, DirX, DirZ, Half; S32 Reach, On, Off, Phase, Puff, Burst, High; U32 HitOpp; int HitHero, Stage, Side; } T_RACE_JET;
extern T_FLOW *TabPartFlow;
static T_RACE_JET s_jets[RACE_MAX_JETS];
static int s_nbJets = 0;
// Tunnels over the raised road (tunnel=<from> <to> <roof>: its first and last point, its roof's height over the deck; Otringal's over the
// bridge to its square island, 2026-10-08): the camera that follows the car rides the road behind it under the roof from RACE_TUNNEL_LEAD
// points before the tunnel's mouth to its end (TunnelCamera) -- out at the arm's usual length the eye stood over the roof, which hid the
// car; and the follow camera's own, its arm only shortened, looks at a point well ahead of the car (DISTANCE_VISEE): the car went out of
// the bottom of the view.
#define RACE_MAX_TUNNELS 8
#define RACE_TUNNEL_LEAD 40
#define RACE_TUNNEL_TAIL 16                // ... and this many past its end (the car out from under the roof before the eye rises)
#define RACE_TUNNEL_EYE 900                // the eye this far under the roof at the most
#define RACE_TUNNEL_BACK 4200              // ... and this far behind the car along the road (TunnelCamera)
#define RACE_TUNNEL_UP 1500
#define RACE_TUNNEL_AHEAD 3000
#define RACE_TUNNEL_SIGHT 700              // ... its sight line clearing the road to the car's middle
#define RACE_TUNNEL_UNDER 250              // ... and the eye at least this far under the roof
typedef struct { int From, To; double Roof; } T_RACE_TUNNEL;
static T_RACE_TUNNEL s_tunnels[RACE_MAX_TUNNELS];
static int s_nbTunnels = 0;
// Lava balls (lavaball=<x> <y> <z> <from> <to> <every> <phase>: a source on the lava's surface, island units, and the stretch of the raised
// road it rains on, its first and last point): Volcano Island's (the user, 2026-10-08: "lava balls shooting up and out of the lava at
// points and raining down onto the track"). Every so often (Every ms, give or take a quarter, from Phase) while Twinsen is near, a ball
// of lava is shot up out of the lava and falls on a place along that stretch, its shadow on the road there growing as it comes; where it
// lands it bursts, and a car in the burst is stopped. Since 2026-10-10 the game's own lava (the user: "the lava balls that are currently
// coming out of the lava are different to the retail ones, so let's switch these"): the ball is its lava ball (OBJFIX.HQR entry 69, the
// one Wannies Island's mine spits; lava_ball_model=, the builder's copy of it RaceTrackOil.LavaBallScale times as big) tumbling along its
// arc, shot out of the lava with the mine's lava burst (impact 25: a spray of sparks and of little balls, its sound) and bursting the same
// way where it lands -- the magic ball's fire sprite with a trail of fire, and a nitro penguin's blast, until then.
#define RACE_MAX_LAVA 16
#define RACE_LAVA_MODEL 69                 // OBJFIX.HQR: the game's lava ball (lava_ball_model=: its bigger copy)
#define RACE_LAVA_BURST 25                 // IMPACT 25: the mine's lava burst
#define RACE_LAVA_BURST_FLOW 67            // ... its sparks, alone when the extras are short
#define RACE_LAVA_SPIT_FLOW 90             // impact 23's flow, the mine's fall's foot: sparks thrown out and down, little balls with them
#define RACE_LAVA_FREE 24                  // the little balls only while this many extras are free (the race's own need theirs)
#define RACE_LAVA_SHADOW 12                // SPRITE_BALLE_SHADOW
#define RACE_LAVA_SCALE 14                 // the shadow's size where it lands
#define RACE_LAVA_NEAR (40 * 512)          // shot only while Twinsen is this near the place it will land
#define RACE_LAVA_REACH 1100               // a car this near the place it lands is caught in its burst
#define RACE_LAVA_FORCE 20
typedef struct { double SrcX, SrcY, SrcZ, DstX, DstY, DstZ; int From, To; S32 Every, Phase, Next, T0, Dur, Trail; int On, Ball, Shadow; } T_RACE_LAVA;
static T_RACE_LAVA s_lava[RACE_MAX_LAVA];
static int s_nbLava = 0;
static int s_lavaModel = -1;               // lava_ball_model=
// Lava falls (lavafall=<lip x> <y> <z> <foot x> <y> <z> <beta>, island units, one a place across the fall's face: RaceTrackLavaFall,
// Volcano Island's crag by its plateau's south edge -- the user, 2026-10-10: "In scene 100 ... there's a lava water fall next to a section
// of track that shoots lava balls, let's steal this idea"). While Twinsen is near, the mine's fall's spit: sparks and little lava balls
// thrown out of its foot every RACE_FALL_FOOT_MS or so (impact 23's, the way its face looks), sparks off its lip and down its face every
// RACE_FALL_LIP_MS or so. The balls it shoots onto the road are lavaball='s, its pool their source.
#define RACE_MAX_FALLS 16
#define RACE_FALL_FOOT_MS 1500
#define RACE_FALL_LIP_MS 600
#define RACE_FALL_NEAR (48 * 512)
#define RACE_FALL_DROP 1000                // (a flow's sparks die this far under where it starts: one at a height down the face)
typedef struct { double LipX, LipY, LipZ, FootX, FootY, FootZ; S32 Beta; } T_RACE_FALL;
static T_RACE_FALL s_falls[RACE_MAX_FALLS];
static int s_nbFalls = 0;
static S32 s_fallFootNext = 0, s_fallLipNext = 0;
// Boost panels (boostpanel=<x> <y> <z> <way x> <way z> <half length> <half width>, island units, the way in thousandths: RaceTrackPlan.
// BoostPanels, chevrons on the deck): a car with two of its wheels or more on one is at RACE_PANEL_KMH at once (the user, 2026-10-10: "a
// boost panel that cars can drive over for a speed boost, they'd only need to drive over it with at least two wheels for it to activate,
// this should immediately shoot a car up to 120kph"). It lasts (the user, the same day: "The current boost panel duration needs to be a lot
// longer" -- it had been lost as any speed over the gear's top is, in 1.6 s): the car's top speed is RACE_PANEL_KMH for RACE_PANEL_HOLD_MS
// (RaceMod_SpeedFactor: the accelerator held keeps it there), then comes back down to its own over RACE_PANEL_FADE_MS.
// Twinsen's car by its four wheels where BUGGY.CPP has them. (The opponents' lines keep to the race lanes: Volcano Island's panel is in
// its pit lane, "so if cars want the speed boost they have to deviate from the normal racing line".)
#define RACE_MAX_PANELS 8
#define RACE_PANEL_KMH 120
#define RACE_PANEL_SPEED (RACE_PANEL_KMH * 5120.0 / 36)
#define RACE_PANEL_ABOVE 700               // the car this far over the panel or under it is on it (the deck's slope, the car's bounce)
#define RACE_PANEL_HOLD_MS 4500
#define RACE_PANEL_FADE_MS 3500
typedef struct { double X, Y, Z, Dx, Dz, HalfLen, HalfWidth; int OnHero; } T_RACE_PANEL;
static T_RACE_PANEL s_panels[RACE_MAX_PANELS];
static int s_nbPanels = 0;
#define RACE_PANEL_PILOT_MS (RACE_PANEL_HOLD_MS + RACE_PANEL_FADE_MS) // (the test pilot holds the accelerator while it lasts, as a player would)
static S32 s_panelAt = -1000000;
// Machine gunners (gunner=<scene> <gun's actor> <Franco's actor> <muzzle x> <y> <z>, island units, then gunner_target=<x> <z> <y> <radius>,
// the places its bullets land): Otringal's Franco machine gun on its knoll inside the corner past the start -- the user, 2026-10-09: "add in
// a franco to fire in bursts so that the bullets only land on the inside of the corner so they can be avoided by driving the corner on the
// outside". Every RACE_GUN_EVERY ms or so a burst, whenever a race is on -- alone or against others (2026-10-10: it never fired in a race
// without opponents): the gun plays its own firing animation (its kick and its rattle; the bullets that animation throws left out, FICHE.CPP
// RaceMod_SilentThrow, as they fly level from it whichever way it faces) and RACE_GUN_SHOTS of the game's own bullets fly from its muzzle to
// places among its targets, the gun turning to each: thrown as its animation throws them (F_THROW_3D: raw sprite 50 at its own size, 100 a
// frame, its weight 12, ThrowExtra) at the height that brings each down on its place (the user, 2026-10-10: "he's not firing the default
// bullets the retail version normally fires" -- they were a tracer of the race mode's, the sprite made bigger and flown straight); where
// one lands, a spark, and a car there is stopped as by a magic ball.
#define RACE_MAX_GUNNERS 4
#define RACE_MAX_GUN_TARGETS 32
#define RACE_MAX_BULLETS 24
#define RACE_GUN_EVERY 4200
#define RACE_GUN_SHOTS 10
#define RACE_GUN_GAP 110                   // ms between its bullets
#define RACE_GUN_SPRITE 50                 // the gun's bullet (raw sprites), as its firing animation throws it (entity 200, anim 173) ...
#define RACE_GUN_SPEED 100                 // ... this fast (a 20 ms frame's way) ...
#define RACE_GUN_WEIGHT 12                 // ... and falling with this weight (ThrowExtra, InitFly)
#define RACE_GUN_REACH 650                 // a car this near where a bullet lands is hit
#define RACE_GUN_FIRING 173                // the gun's firing animation (entity 200)
#define RACE_GUN_AT_IT 474                 // the Franco sitting at it (entity 201)
typedef struct {
    S32 Scene, Gun, Franco; double MX, MY, MZ;
    int NbTargets; double TX[RACE_MAX_GUN_TARGETS], TZ[RACE_MAX_GUN_TARGETS], TY[RACE_MAX_GUN_TARGETS], TR[RACE_MAX_GUN_TARGETS];
    S32 Next, BurstAt; int Fired, Firing, Ready;
} T_RACE_GUNNER;
typedef struct { int On, Extra, Owner; double FromX, FromY, FromZ, ToX, ToY, ToZ; S32 T0, Dur; } T_RACE_BULLET;
static T_RACE_GUNNER s_gunners[RACE_MAX_GUNNERS];
static int s_nbGunners = 0;
static T_RACE_BULLET s_bullets[RACE_MAX_BULLETS];
// The roulette wheel the lap runs through (roulette=<cube x> <cube z> <x> <z> <spin> <the ball's body> <its radius> <landing> <the floor's
// height at the hole's edge> <its rise a cell outwards> <its steepening a cell a cell, hundredths> <the hole's radius> <the wall's> <its
// outside's> <its underside under its floor> <its turning bodies...>, island units): Otringal's, floating over where its casino stood --
// the user, 2026-10-09: "a giant rotating roulette wheel complete with colours, numbers, and a giant white ball that rolls around for cars to
// avoid"; then "twice as wide and over the casino location ... magically floating ... Let's make the sides of the roulette wheel steeper
// so instead of a guide rail cars are slowly drawn down with gravity". Its parts are decors of the island's (RaceTrackRoulette), drawn from
// wherever the wheel is seen: as each cube's decors are drawn (3DEXT/DECORS.CPP DecorPoseHook) they are turned, <spin> tenths of a degree a
// second, and the ball's moved to where it rolls, round the bowl the other way. Its floor is the race-track mode's own (WheelFloorAt: no
// rails -- the lap's way round it has no width in the raised road's file): Twinsen's car drives it freely, drawn down it towards the hole
// as hard as it is steep (WheelMove; and its slopes speed it up and slow it down, RaceMod_Gravity), kept inside its wall, and dropped
// through the hole onto the road under it -- place <landing> of the raised road's file -- from wherever it goes in. A car the ball rolls
// into is struck, as by a magic ball.
#define RACE_MAX_WHEEL_PARTS 12
// The user, 2026-10-10: "The roulette wheel we've created isn't really fun to drive" -- of the ideas offered, all four: a prize by the
// pocket the car goes into the hole over, the turning floor carrying the car round, a quicker way down, balls that bounce and knock cars.
#define RACE_MAX_BALLS 4                   // roulette_balls=<n>: its balls, rolling down the bowl, carried round by its turn, bouncing off its
#define RACE_BALL_G 16000.0                // wall, the ridge round its hole, each other and the cars: pulled down it this hard a unit of slope ...
#define RACE_BALL_GRIP 0.08                // ... brought to the turning floor's own speed this much a second (much more, and they rode round
                                           // against the wall with it) ...
#define RACE_BALL_BOUNCE 0.85              // ... keeping this much of their speed off what they hit ...
#define RACE_BALL_KICK 1.15                // ... and kicked back up the bowl off the ridge round the hole, this much faster ...
#define RACE_BALL_TOP 9000.0               // ... no faster than this ...
#define RACE_BALL_DAMP 0.03                // ... losing this share of their speed a second
#define RACE_BALL_KNOCK 1800.0             // a car a ball rolls into is knocked this far aside ...
#define RACE_BALL_KNOCK_MS 450             // ... over this long ...
#define RACE_BALL_SLOW 0.6                 // ... and left this much of its speed
#define RACE_WHEEL_DRAG 0.4                // how much of the turning floor's speed carries Twinsen's car round with it
#define RACE_WHEEL_RED_MS 2500             // a red pocket into the hole: Gazogem's boost this long on the road under the wheel ...
#define RACE_WHEEL_ZERO_MS 5000            // ... the green zero: this long, and an item if a slot is free
#define RACE_BALL_REACH 330                // a car is hit with its middle this much further from the ball's than the ball's radius, or nearer
#define RACE_BALL_AGAIN 1500               // ms before a ball can knock the same car again
#define RACE_WHEEL_TURN (-1)               // the way the wheel turns (the cars go round it anticlockwise seen from above, the ball the other way)
#define RACE_WHEEL_PULL 3500.0             // units a second a car on the bowl is drawn in, a unit of slope (was 1,500: a slow way down)
#define RACE_WHEEL_DROP_LEAST 3000.0       // the drop through the hole: carried at least this fast ...
#define RACE_WHEEL_DROP_MS 900             // ... and taking at least this long
typedef struct {
    int On, CubeX, CubeZ, Ball, BallR, Landing, NbParts, Parts[RACE_MAX_WHEEL_PARTS];
    double X, Z, Spin, HoleY, S1, S2, Hole, Rim, Outer, Under;
    // (its balls: where, how fast, their last step's time; their bodies Ball.. Ball+NbBalls-1)
    int NbBalls, BallsSet, BallShown, Pockets;
    double BallX[RACE_MAX_BALLS], BallY[RACE_MAX_BALLS], BallZ[RACE_MAX_BALLS], BallVX[RACE_MAX_BALLS], BallVZ[RACE_MAX_BALLS];
    S32 BallT;
    // (Twinsen's car knocked by a ball: since when, which way; the pocket it went into the hole over: its number, its prize 0 black, 1 red,
    // 2 the zero)
    S32 KnockT0; double KnockX, KnockZ;
    int Prize, PrizeNumber;
    S32 Beta, Again[RACE_MAX_OPPONENTS + 1];
    // (Twinsen's car: its last move, and the drop through the hole -- from D0, by D1 and D2, to DL, DropMs long at DropV)
    S32 MoveT;
    int DropOn;
    S32 DropT0, DropMs, DropBeta;
    double D0x, D0y, D0z, D1x, D1z, D2x, D2z, DLx, DLy, DLz, DropV;
    // (the camera's eye in the bowl, kept while the car drops: cube-local)
    S32 Eye[3];
} T_RACE_ROULETTE;
static T_RACE_ROULETTE s_wheel;
static S32 WheelMove(S32 ox, S32 oy, S32 oz, S32 *nx, S32 *ny, S32 *nz);
// The bowl's floor (world units): at `r` from its middle, its slope outwards (a unit a unit), its underside, and whether a place is on it.
static double WheelR(double wx, double wz) { return sqrt((wx - s_wheel.X) * (wx - s_wheel.X) + (wz - s_wheel.Z) * (wz - s_wheel.Z)); }
static double WheelCells(double r) { const double d = (r - s_wheel.Hole) / 512.0; return d > 0 ? d : 0; }
static double WheelFloorAt(double r) { const double d = WheelCells(r); return s_wheel.HoleY + s_wheel.S1 * d + s_wheel.S2 * d * d; }
static double WheelSlopeAt(double r) { return (s_wheel.S1 + 2 * s_wheel.S2 * WheelCells(r)) / 512.0; }
static double WheelUnderAt(double r) { return WheelFloorAt(r < s_wheel.Rim ? r : s_wheel.Rim) - s_wheel.Under; }
static int OnWheel(double wx, double wy, double wz) {
    if (!s_wheel.On) return 0;
    const double r = WheelR(wx, wz);
    if (r > s_wheel.Rim + 64 || r < s_wheel.Hole - 64) return 0;
    const double f = WheelFloorAt(r);
    return wy > f - 1500 && wy < f + 1500;
}
extern void (*DecorPoseHook)(T_DECORS *list, S32 count);  // 3DEXT/DECORS.CPP
static void WheelDecors(T_DECORS *list, S32 count);
// Gas monsters (gasmonster=<scene> <its actor> <its body> <x> <z> <bite x> <bite z> <bite y> <foot y> <first anim> [<scene> <actor>]...,
// island units: where it stands out of the gas, its place on the road and the road's height there, its foot's height; the first of its
// animations; then its actor in each other scene it is seen from -- the engine draws a scene's actors only while it is the one about, and
// the islets' monsters stood up out of nowhere as the car came into their scene, 2026-10-10): Otringal's,
// at its docks and round the islets where Baldino's plane lies crashed -- the user, 2026-10-09: "let's have gas monsters coming out of the
// gas and attempting to bite cars, any bitten cars should be stunned for a couple of seconds as though they've been hit then be able to
// continue"; then: "let's have them permanently visible with their necks above the track and randomly striking down. Let's have each gas
// monster always attack the same point in the track, so once their positions are learnt they're avoidable"; then: "Let's make the gas
// monsters necks longer ... so they're above the track then when they're ready they strike down onto the track into their strike zone".
// The game's own gas monster (entity 222), twice its size, its neck as long as its place wants (a body of its own), stands beside the road
// for good, swaying with its head poised high over its strike zone on the road's near half, mouth open (its first animation). Every
// RACE_MONSTER_EVERY to twice that, at random, it rears back and strikes down (the next), its mouth on the strike zone RACE_MONSTER_HIT after
// the strike begins: a car there, or one driving into it while its jaw snaps shut and holds (RACE_MONSTER_HOLD), is bitten and held
// RACE_MONSTER_STUN; then it goes back to poised (the third). Its animations are made for its place by
// RaceTrackGasMonster.InstallPoses, and these times are theirs. Its actor is posed while its scene is the one about; elsewhere it strikes
// all the same, for the opponents there.
#define RACE_MAX_MONSTERS 16
#define RACE_MONSTER_SEEN 8                // the scenes a monster has an actor in, at the most
#define RACE_MONSTER_SEEN_FAR (40 * 512.0) // drawn only with the car this near it, and not from the roulette wheel's bowl: the decors are drawn
                                           // without the z-buffer, and a monster far behind one -- the wheel, a road's deck -- was drawn over it,
                                           // white in the fog (the user, 2026-10-10: "random shadow images of our gas monsters at points in
                                           // the track")
#define RACE_MONSTER_HIT (150 + 650 + 220) // ms from the strike's start to its head on the road (InstallPoses: PoiseMs, RearMs, LungeMs) ...
#define RACE_MONSTER_BACK (20 + 120 + 330 + 700) // ... and from there, biting, back to poised (20, SnapMs, HoldMs, BackMs) ...
#define RACE_MONSTER_HOLD (120 + 330)      // ... the first part of it its jaw snapping shut on the road and held (SnapMs, HoldMs)
#define RACE_MONSTER_EVERY 1400            // ms poised between strikes, at the least (and up to twice as long)
#define RACE_MONSTER_BITE 950              // a car this near its place (on the level) when its head comes down is bitten ...
#define RACE_MONSTER_STUN 2200             // ... and held this long
enum { MONSTER_POISED, MONSTER_STRIKE, MONSTER_BITE };
typedef struct {
    S32 Scene; int Neck, Body, Anim0; double RiseX, RiseZ, BiteX, BiteZ, BiteY, FootY;
    int State, Shown, Anim; S32 T0, Next;
    int NbSeen; S32 SeenScene[RACE_MONSTER_SEEN], ShownScene; int SeenActor[RACE_MONSTER_SEEN];   // its actors, its own scene's first
} T_RACE_MONSTER;
static T_RACE_MONSTER s_monsters[RACE_MAX_MONSTERS];
static int s_nbMonsters = 0;
static S32 s_bittenUntil[RACE_MAX_OPPONENTS + 1];  // a car just bitten is let be till then -- a monster beside it doesn't bite it again
#define RACE_MONSTER_GRACE 2500            // ms after its stun
// Shuttles (shuttle=<scene> <actor> <x> <y> <z>: the scene's actor, and the pad it lands on, cube-local, on the road): Otringal's spaceship
// on the square island, which in the game comes down onto its pad on the road and takes off again at random -- the user, 2026-10-08:
// "leave this in as a feature but perhaps have it hover for a few seconds, land for a couple of seconds before rising straight up
// again". Its own scripts are stopped and it is flown here, on the race's clock (the same for every lap): out of sight, then down out of
// the sky over its pad with its legs coming out, hovering over the road high enough for the cars to pass under it, down onto the road,
// a while there, and straight up again with its legs going in. A car that runs into it while it is down is stopped, as by a penguin's
// blast (the game's own script hit Twinsen for 50 when it touched him).
#define RACE_MAX_SHUTTLES 4
#define RACE_SHUTTLE_AWAY 5500             // out of sight (ms)
#define RACE_SHUTTLE_DOWN 2600             // ... coming down out of the sky to hover
#define RACE_SHUTTLE_HOVER 3600            // ... hovering over the pad
#define RACE_SHUTTLE_TOUCH 900             // ... down onto it
#define RACE_SHUTTLE_LANDED 2200           // ... on the road
#define RACE_SHUTTLE_RISE 3400             // ... straight up out of sight
#define RACE_SHUTTLE_HOVERY 2700           // its hover over the pad (the cars pass under it)
#define RACE_SHUTTLE_HIGH 15000            // where it comes down from and rises to, over the pad
#define RACE_SHUTTLE_LOW 1200              // under this over the pad it is in the cars' way ...
#define RACE_SHUTTLE_REACH 1700            // ... a car this near its middle is hit
#define RACE_SHUTTLE_FORCE 20
typedef struct { S32 Scene, Actor, X, Y, Z; int Ready; S32 Hit; } T_RACE_SHUTTLE;
static T_RACE_SHUTTLE s_shuttles[RACE_MAX_SHUTTLES];
static int s_nbShuttles = 0;
// Jump cameras (jumpcam=, JumpCamera): a drop -- a flight off a raised road's end and down a hillside onto the ground far below (the lava
// lake's, 2026-10-01) -- took the camera behind the car down into the hill, and the landing was never seen. While the hero flies such a
// jump, and RACE_JUMPCAM_HOLD after he lands, the camera stands at a place of its own beside the flight (the builder's, with the whole
// flight and the landing in sight) and looks at the car.
#define RACE_MAX_JUMPCAMS 4
#define RACE_JUMPCAM_HOLD 1500
typedef struct { int Anim, CubeX, CubeZ, X, Y, Z; } T_RACE_JUMPCAM;
static T_RACE_JUMPCAM s_jumpCams[RACE_MAX_JUMPCAMS];
static int s_nbJumpCams = 0;
// Carried jumps (arcjump=, RaceArc: see "a jump the car is carried over").
#define RACE_MAX_CUBE_SCENES 64
#define RACE_ARC_LEAST 4500.0
#define RACE_ARC_ENTRY 600.0       // how far over or under the ramp's foot a car is still taken onto it
#define RACE_ARC_CAM_FAR 18.0      // the camera beside the flight: cells out to the side ...
#define RACE_ARC_CAM_BACK 5.0      // ... and back along the way from the car ...
#define RACE_ARC_CAM_UP 1800.0     // ... and over it
#define RACE_ARC_CAM_HOLD 500      // ms it stays there after the car lands
typedef struct {
    int Foot, Lip, Land, LandFoot, Side;
    double *S;                     // the way's distance along the ground from the foot, point by point
} T_RACE_ARC;
static T_RACE_ARC s_arcs[RACE_MAX_ARCS];
static int s_nbArcs = 0;
static int s_cubeScenes[RACE_MAX_CUBE_SCENES][3];
static int s_nbCubeScenes = 0;
static int s_arcOn = -1, s_arcLast = -1;
static double s_arcS = 0, s_arcV = 0, s_arcAcross = 0;
static double s_arcAcross1 = 0;
static int s_arcSeg = -1;                 // the raised road's segment the carried car is over, and how far along it (ArcPlace)
static double s_arcT = 0;           // ... and where it is set down: within the landing's rails (a narrower road there: the roulette wheel's lane)
static S32 s_arcTime = 0, s_arcLandedAt = 0;

static int ArcPrepare(T_RACE_ARC *a) {
    if (a->Foot < 0 || a->LandFoot >= s_nbRaised || !(a->Foot < a->Lip && a->Lip < a->Land && a->Land < a->LandFoot)) return 0;
    const int n = a->LandFoot - a->Foot + 1;
    a->S = (double *)malloc(n * sizeof(double));
    a->S[0] = 0;
    for (int k = 1; k < n; k++) {
        const T_RACE_RAISED *p = &s_raised[a->Foot + k - 1], *q = p + 1;
        a->S[k] = a->S[k - 1] + sqrt((double)(q->X - p->X) * (q->X - p->X) + (double)(q->Z - p->Z) * (q->Z - p->Z));
    }
    return 1;
}

static S32 s_flying = -1;                  // the flight the hero is flying (its generic animation), -1 none
static S32 s_landed = -1;                  // the flight he last landed from ...
static U32 s_landedAt = 0;                 // ... and when (TimerRefHR)
static int s_statue = -1;                // statue=: 1 with the statue, 0 without it, -1 (not given) as the game says
static int s_raisedLoop = 0;               // the road is a whole lap: its last point is its first
static double s_gravity = 0;               // gravity=<k>: a car on the raised road is pulled down its slopes (RaceMod_Gravity), k times as hard as RACE_GRAVITY
// railcam=<back> <up> <lead>: the camera that follows the car rides the raised road behind it (RailCamera)
static double s_railBack = 0, s_railUp = 0, s_railLead = 0;
static int s_railTouch = 0;                // the rail held the hero back since RaceMod_RailTouched was last asked

extern S32 (*AltitudeHook)(S32 x, S32 z, S32 y);         // 3DEXT/MAPTOOLS.CPP
extern S32 (*TerrainColHook)(S32 x, S32 z, S32 col);
extern S32 (*SightFloorHook)(S32 x, S32 z, S32 y);
extern S32 FollowCamera;                                 // FOLLOWCAM.CPP
extern S32 (*FollowCamArmHook)(S32 base);
extern S32 (*FollowCamEyeHook)(S32 *eye, S32 *target);
extern S32 CalculAltitudeTerrain(S32 x, S32 z, S32 cj);

// Whether a place `tt` along segment i (its projection, unclamped) is past an open end of the road -- the road's own last point, or a
// jump's take-off lip, where a gap (Half 0) follows: there is no floor out there (a segment's ends are otherwise round, so the deck
// reached a half-width past the lava lake's drop lip and held the car up over the gap, 2026-10-01).
static int RaisedOpenEnd(int i, double tt) {
    if (tt > 1) return i + 2 >= s_nbRaised ? !s_raisedLoop : s_raised[i + 2].Half <= 0;
    if (tt < 0) return i == 0 ? !s_raisedLoop : s_raised[i - 1].Half <= 0;
    return 0;
}

// The road at an island position (world units) for something at height `ref`, looked for `pad` past its edges: its height there, the
// segment (from point *seg to the next), how far along it (*t, 0..1) and how far to the side of its middle (*lat, to the left of the way
// the lap runs positive). Of the parts of the road in reach -- each no more than RAISED_STEP above `ref` -- the highest level, and of
// that level's segments the nearest. 0 when the road isn't there. A banked road's height is its surface's at that place, not its
// middle's.
static int RaisedAt(double wx, double wz, S32 ref, double pad, S32 *y, int *seg, double *lat, double *t) {
    if (!s_nbRaised) return 0;
    const int bx = (int)floor(wx / RAISED_BUCKET) - s_rbX0, bz = (int)floor(wz / RAISED_BUCKET) - s_rbZ0;
    const int reach = 1 + (int)(pad / RAISED_BUCKET) + s_rbExtra;
    double top = -1e18;
    for (int pass = 0; pass < 2; pass++) {
        double bestD = 1e30;
        int found = 0;
        for (int dz = -reach; dz <= reach; dz++)
            for (int dx = -reach; dx <= reach; dx++) {
                const int x = bx + dx, z = bz + dz;
                if (x < 0 || z < 0 || x >= s_rbW || z >= s_rbH) continue;
                const int b = z * s_rbW + x;
                for (int k = s_rbStart[b]; k < s_rbStart[b + 1]; k++) {
                    const int i = s_rbItem[k];
                    if (i >= s_nbRaised - 1) continue;
                    if (s_onlyNear >= 0) {
                        int apart = abs(i - s_onlyNear);
                        if (s_raisedLoop && apart > (s_nbRaised - 1) / 2) apart = s_nbRaised - 1 - apart;
                        if (apart > RAISED_SIGHT) continue;
                    }
                    const T_RACE_RAISED *a = &s_raised[i], *c = a + 1;
                    if (a->Half <= 0 || c->Half <= 0) continue;
                    const double sx = c->X - a->X, sz = c->Z - a->Z, len2 = sx * sx + sz * sz;
                    if (len2 <= 0) continue;
                    double tt = ((wx - a->X) * sx + (wz - a->Z) * sz) / len2;
                    if (RaisedOpenEnd(i, tt)) continue;
                    if (tt < 0) tt = 0; else if (tt > 1) tt = 1;
                    const double px = a->X + sx * tt, pz = a->Z + sz * tt;
                    const double d2 = (wx - px) * (wx - px) + (wz - pz) * (wz - pz);
                    const double half = a->Half + (c->Half - a->Half) * tt + pad;
                    if (d2 > half * half) continue;
                    const double side = (sx * (wz - pz) - sz * (wx - px)) / sqrt(len2);
                    const double yy = a->Y + (c->Y - a->Y) * tt + side * (a->Bank + (c->Bank - a->Bank) * tt) / RAISED_BANK_UNITS;
                    if (yy > ref + RAISED_STEP) continue;
                    if (!pass) { if (yy > top) top = yy; continue; }
                    if (yy < top - RAISED_LEVEL || d2 >= bestD) continue;
                    bestD = d2;
                    found = 1;
                    *y = (S32)floor(yy + 0.5);
                    *seg = i;
                    *t = tt;
                    *lat = side;
                }
            }
        if (pass) return found;
        if (top < -1e17) return 0;
    }
    return 0;
}

static S32 RaisedAltitude(S32 x, S32 z, S32 y) {
    if (s_floorRef == RAISED_NOREF || CubeMode != CUBE_EXTERIEUR) return y;
    // (the roulette wheel's bowl, for something about its floor's height there)
    if (s_wheel.On) {
        const double wx = x + CurrentCubeX * 32768.0, wz = z + CurrentCubeY * 32768.0, r = WheelR(wx, wz);
        if (r <= s_wheel.Rim + 64 && r >= s_wheel.Hole) {
            const double f = WheelFloorAt(r);
            if (s_floorRef > f - 1500 && s_floorRef < f + 1500) return f > y ? (S32)floor(f + 0.5) : y;
        }
    }
    S32 f;
    int seg;
    double lat, t;
    if (!RaisedAt(x + CurrentCubeX * 32768.0, z + CurrentCubeY * 32768.0, s_floorRef, RAISED_PAD, &f, &seg, &lat, &t)) return y;
    return f > y ? f : y;
}

static S32 RaisedCol(S32 x, S32 z, S32 col) {
    if (!col || s_floorRef == RAISED_NOREF || CubeMode != CUBE_EXTERIEUR) return col;
    if (OnWheel(x + CurrentCubeX * 32768.0, s_floorRef, z + CurrentCubeY * 32768.0)) return 0;
    S32 f;
    int seg;
    double lat, t;
    if (!RaisedAt(x + CurrentCubeX * 32768.0, z + CurrentCubeY * 32768.0, s_floorRef, RAISED_PAD, &f, &seg, &lat, &t)) return col;
    return f > CalculAltitudeTerrain(x, z, 0) ? 0 : col;
}

// For the cameras: what would hide the hero from an eye at (x, z) -- the stretch of raised road he is on, where it passes there (the
// road's other levels, over and under him, are not between him and an eye that follows him), or the ground (y) where that is higher.
static S32 RaisedSight(S32 x, S32 z, S32 y) {
    if (CubeMode != CUBE_EXTERIEUR) return y;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    S32 f;
    int seg;
    double lat, t;
    if (!RaisedAt(hero->Obj.X + cx, hero->Obj.Z + cz, hero->Obj.Y + 64, RAISED_PAD, &f, &seg, &lat, &t) || abs(f - hero->Obj.Y) > 300) return y;
    s_onlyNear = seg;
    const int found = RaisedAt(x + cx, z + cz, 0x3FFFFFFF, RAISED_PAD, &f, &seg, &lat, &t);
    s_onlyNear = -1;
    return found && f > y ? f : y;
}

// The road's slope at a segment for something facing `beta`: the height it gains per unit that way (over a cell or two of road).
static double RaisedSlope(int seg, S32 beta) {
    int a = seg - 2, b = seg + 3;
    if (s_raisedLoop) {
        const int n = s_nbRaised - 1;
        a = ((a % n) + n) % n; b = b % n;
    } else {
        if (a < 0) a = 0;
        if (b > s_nbRaised - 1) b = s_nbRaised - 1;
    }
    const double sx = s_raised[b].X - s_raised[a].X, sz = s_raised[b].Z - s_raised[a].Z, len = sqrt(sx * sx + sz * sz);
    if (len < 1) return 0;
    const double grade = (s_raised[b].Y - s_raised[a].Y) / len;
    const double angle = (beta & 4095) * 2 * 3.14159265358979 / 4096.0;
    return grade * (sin(angle) * sx + cos(angle) * sz) / len;
}

// gravity=<k>: a rollercoaster of a lap. A car on the raised road is pulled down its slopes and held back up them (BUGGY.CPP
// RaceSpeedOnSlope): it gathers speed down a drop, past what its gears give, and carries it over the next hill. The key's value (k
// times RACE_GRAVITY) where the hero is on the raised road, with the road's slope the way he faces (the height gained per unit
// forwards) and how much it is banked there (the height across it per unit, either way); 0 anywhere else and without the key.
double RaceMod_Gravity(double *slope, double *bank) {
    *slope = 0;
    *bank = 0;
    // (the roulette wheel's bowl: its slope the way the car faces, rising outwards; its bank across it -- whatever gravity= says)
    if (s_wheel.On && !s_wheel.DropOn && CubeMode == CUBE_EXTERIEUR) {
        const T_OBJET *hero = &ListObjet[NUM_PERSO];
        const double wx = hero->Obj.X + CurrentCubeX * 32768.0, wz = hero->Obj.Z + CurrentCubeY * 32768.0, r = WheelR(wx, wz);
        if (r > 1 && OnWheel(wx, hero->Obj.Y, wz)) {
            const double a = (hero->Obj.Beta & 4095) * 2 * 3.14159265358979 / 4096.0, fx = sin(a), fz = cos(a), rx = (wx - s_wheel.X) / r, rz = (wz - s_wheel.Z) / r;
            const double k = WheelSlopeAt(r);
            *slope = k * (rx * fx + rz * fz);
            *bank = k * fabs(rx * fz - rz * fx);
            return 1;
        }
    }
    if (s_gravity <= 0 || !s_nbRaised || CubeMode != CUBE_EXTERIEUR) return 0;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    S32 f;
    int seg;
    double lat, t;
    if (!RaisedAt(hero->Obj.X + CurrentCubeX * 32768.0, hero->Obj.Z + CurrentCubeY * 32768.0, hero->Obj.Y + 64, RAISED_PAD, &f, &seg, &lat, &t)
            || abs(f - hero->Obj.Y) > 300) return 0;
    *slope = RaisedSlope(seg, hero->Obj.Beta);
    *bank = fabs(s_raised[seg].Bank + (s_raised[seg + 1].Bank - s_raised[seg].Bank) * t) / RAISED_BANK_UNITS;
    return s_gravity;
}

// The share of a gear's top speed the engine can hold up a slope (RACE_CLIMB of it lost per unit of slope, a half at least).
double RaceMod_ClimbShare(double gravity, double slope) {
    if (slope <= 0 || gravity <= 0) return 1;
    const double share = 1 - gravity * RACE_CLIMB * slope;
    return share < 0.5 ? 0.5 : share;
}

S32 RaceMod_RailTouched(void) {
    const int touched = s_railTouch;
    s_railTouch = 0;
    return touched;
}

// The lowest part of the road over a place between two heights: 1 and its height there, or 0. Not the stretch of road round point
// `own` (RAISED_OWN points either way): on a steep slope the road a few cells further up it is "over" a place on it too.
static int RaisedBetween(double wx, double wz, double lo, double hi, int own, double *at) {
    if (!s_nbRaised) return 0;
    const int bx = (int)floor(wx / RAISED_BUCKET) - s_rbX0, bz = (int)floor(wz / RAISED_BUCKET) - s_rbZ0;
    int found = 0;
    for (int dz = -1 - s_rbExtra; dz <= 1 + s_rbExtra; dz++)
        for (int dx = -1 - s_rbExtra; dx <= 1 + s_rbExtra; dx++) {
            const int x = bx + dx, z = bz + dz;
            if (x < 0 || z < 0 || x >= s_rbW || z >= s_rbH) continue;
            const int b = z * s_rbW + x;
            for (int k = s_rbStart[b]; k < s_rbStart[b + 1]; k++) {
                const int i = s_rbItem[k];
                if (i >= s_nbRaised - 1) continue;
                if (own >= 0) {
                    int apart = abs(i - own);
                    if (s_raisedLoop && apart > (s_nbRaised - 1) / 2) apart = s_nbRaised - 1 - apart;
                    if (apart <= RAISED_OWN) continue;
                }
                const T_RACE_RAISED *a = &s_raised[i], *c = a + 1;
                if (a->Half <= 0 || c->Half <= 0) continue;
                const double sx = c->X - a->X, sz = c->Z - a->Z, len2 = sx * sx + sz * sz;
                if (len2 <= 0) continue;
                double tt = ((wx - a->X) * sx + (wz - a->Z) * sz) / len2;
                if (RaisedOpenEnd(i, tt)) continue;
                if (tt < 0) tt = 0; else if (tt > 1) tt = 1;
                const double px = a->X + sx * tt, pz = a->Z + sz * tt;
                const double half = a->Half + (c->Half - a->Half) * tt;
                if ((wx - px) * (wx - px) + (wz - pz) * (wz - pz) > half * half) continue;
                const double yy = a->Y + (c->Y - a->Y) * tt;
                if (yy <= lo || yy >= hi || (found && yy >= *at)) continue;
                found = 1;
                *at = yy;
            }
        }
    return found;
}

// The road's place `distance` along it from a point of it (segment `seg`, `t` along it), forwards or (negative) backwards: its middle
// there, at the road's own height. Round a loop's seam; stopped at an open road's ends.
static int RaisedAlong(int seg, double t, double distance, double *x, double *y, double *z) {
    const int n = s_nbRaised - 1;                       // its segments
    int i = seg;
    double at = t;
    for (int guard = 0; guard < 2 * n + 8; guard++) {
        const T_RACE_RAISED *a = &s_raised[i], *c = a + 1;
        const double len = sqrt((double)(c->X - a->X) * (c->X - a->X) + (double)(c->Z - a->Z) * (c->Z - a->Z));
        const int forward = distance >= 0;
        const double room = forward ? (1 - at) * len : at * len;      // what is left of this segment that way
        if (len > 1e-6 && fabs(distance) <= room) { at += distance / len; break; }
        distance += forward ? -room : room;
        if (forward) {
            if (i + 1 >= n) { if (!s_raisedLoop) { at = 1; break; } i = 0; } else i++;
            at = 0;
        } else {
            if (i == 0) { if (!s_raisedLoop) { at = 0; break; } i = n - 1; } else i--;
            at = 1;
        }
    }
    const T_RACE_RAISED *a = &s_raised[i], *c = a + 1;
    *x = a->X + (c->X - a->X) * at;
    *y = a->Y + (c->Y - a->Y) * at;
    *z = a->Z + (c->Z - a->Z) * at;
    return i;
}

// railcam=: the camera that follows the car on a rollercoaster of a road. An arm behind the car does badly there -- the road's other
// levels are over it, a drop's crest between it and the car, a bend swings it out over nothing -- so the eye rides the road itself:
// over its middle s_railBack behind the car (behind the way the car faces), s_railUp over it, looking at the road s_railLead ahead of
// the car. Kept under a level of the road that passes over it, and high enough to see the car over a crest between them.
// (TunnelCamera's: the sight line to the car ends this far over the road, at the car's middle -- to its wheels, near the car the line
// is so near the road that the eye was lifted 3,300 over the road to clear it, over the roof of Otringal's bridge, 2026-10-08 -- and
// the eye no higher than this over the road under it, under a tunnel's roof: -1 none)
static double s_railSight = 0, s_railCeil = -1;
// Whether a jump's gap (points with no width) lies on the raised road between segments a and b (either way round, the shorter): the rail
// camera's eye is not let ride back over the gap the car has just flown -- under the roulette wheel, up on the wheel's lane over the car.
static int GapBetween(int a, int b) {
    const int n = s_nbRaised - 1;
    if (n <= 0 || a < 0 || b < 0) return 0;
    int from = a, to = b;
    if (s_raisedLoop) { if (((b - a) % n + n) % n > n / 2) { from = b; to = a; } }
    else if (a > b) { from = b; to = a; }
    for (int i = from, guard = 0; guard <= n; guard++) {
        if (s_raised[i].Half <= 0) return 1;
        if (i == to) break;
        i = s_raisedLoop ? (i + 1) % n : i + 1;
        if (i > n) break;
    }
    return 0;
}

static S32 RailCamera(S32 *eye, S32 *target) {
    if (s_railBack <= 0 || !s_nbRaised || CubeMode != CUBE_EXTERIEUR || Comportement != C_BUGGY) return FALSE;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    S32 f;
    int seg;
    double lat, t;
    // (carried over a jump with no camera of its own beside it -- the roulette wheel's drop through its hole, 2026-10-09: the follow
    // camera took over there from where it had last been, out past the wheel behind a rock -- where along the road it is)
    const int carried = s_arcOn >= 0 && s_arcSeg >= 0 && s_arcs[s_arcOn].Side == 0;
    if (carried) { seg = s_arcSeg; t = s_arcT; }
    else if (!RaisedAt(hero->Obj.X + cx, hero->Obj.Z + cz, hero->Obj.Y + 64, RAISED_PAD, &f, &seg, &lat, &t) || abs(f - hero->Obj.Y) > 300) return FALSE;
    // which way along the road the car faces
    const T_RACE_RAISED *a = &s_raised[seg], *c = a + 1;
    const double angle = (hero->Obj.Beta & 4095) * 2 * 3.14159265358979 / 4096.0;
    const double way = sin(angle) * (c->X - a->X) + cos(angle) * (c->Z - a->Z) >= 0 ? 1 : -1;
    double ex, ey, ez, tx, ty, tz;
    const double hx = hero->Obj.X + cx, hy = hero->Obj.Y, hz = hero->Obj.Z + cz;
    // (round a tight bend the road s_railBack behind the car is off to its side: the eye comes nearer, until it looks along the way
    // the car faces within RAIL_CAMERA_SIDE)
    const double fx = sin(angle), fz = cos(angle);
    double back = s_railBack;
    int behind = RaisedAlong(seg, t, -way * back, &ex, &ey, &ez);
    // (... and nearer still, past RAIL_CAMERA_NEAREST, while it would ride over a jump's gap the car has just flown)
    while (back > 512) {
        const int gap = !carried && GapBetween(behind, seg);
        if (back <= RAIL_CAMERA_NEAREST && !gap) break;
        const double dx = hx - ex, dz = hz - ez, d = sqrt(dx * dx + dz * dz);
        if ((d < 1 || (dx * fx + dz * fz) / d >= cos(RAIL_CAMERA_SIDE * 3.14159265358979 / 180)) && !gap) break;
        back -= 256;
        behind = RaisedAlong(seg, t, -way * back, &ex, &ey, &ez);
    }
    RaisedAlong(seg, t, way * s_railLead, &tx, &ty, &tz);
    // (it looks between the car and the road ahead of it: the car stays in the middle of the view, the road ahead above it)
    tx = hx + (tx - hx) * RAIL_CAMERA_LOOK; ty = hy + (ty - hy) * RAIL_CAMERA_LOOK; tz = hz + (tz - hz) * RAIL_CAMERA_LOOK;
    // (lower too when nearer, so the view is no steeper)
    const double up = s_railUp * sqrt(back / s_railBack);
    const double roadBehind = ey;
    ey += up;
    // over a crest between the eye and the car: the line to the car clears the road by RAIL_CAMERA_CLEAR all the way
    double lift = 0;
    for (int k = 1; k < 8; k++) {
        double mx, my, mz;
        RaisedAlong(seg, t, -way * back * k / 8.0, &mx, &my, &mz);
        const double line = hy + s_railSight + (ey - hy - s_railSight) * k / 8.0;
        const double need = (my + RAIL_CAMERA_CLEAR - line) * 8.0 / k;      // what the eye has to rise for the line to clear this point
        if (need > lift) lift = need;
    }
    ey += lift;
    if (s_railCeil >= 0 && ey > roadBehind + s_railCeil) ey = roadBehind + s_railCeil;
    // (the car under the roulette wheel -- the road on from its hole runs under the bowl -- the eye under the bowl too)
    if (s_wheel.On && !carried && hy < s_wheel.HoleY - s_wheel.Under) {
        double r = sqrt((ex - s_wheel.X) * (ex - s_wheel.X) + (ez - s_wheel.Z) * (ez - s_wheel.Z));
        if (r < s_wheel.Outer + 600) {
            const double ceiling = WheelUnderAt(r) - 250;
            if (ey > ceiling) ey = ceiling;
        }
    }
    // under a level of the road over the eye
    double over;
    if (RaisedBetween(ex, ez, ey - up - lift + 800.0, ey + RAISED_UNDER, behind, &over) && ey > over - RAISED_UNDER) ey = over - RAISED_UNDER;
    eye[0] = (S32)(ex - cx); eye[1] = (S32)ey; eye[2] = (S32)(ez - cz);
    target[0] = (S32)(tx - cx); target[1] = (S32)(ty + RAIL_CAMERA_AIM); target[2] = (S32)(tz - cz);
    return TRUE;
}

// For the camera that follows the hero: how long its arm may be while he is on the raised road. Behind a car coming down a steep
// stretch the road rises: an eye out at the arm's usual length would have to stand as high above the car as the road climbs in that
// length to see it over the road -- 10,000 and more behind a car on Celebration Island's bridge -- which is above the lap's other
// levels that the bridge dives under, and they then hide the car. So the arm is shortened as the road behind the car steepens (the eye
// is lifted over the road by the camera's own ground clearance, which asks RaisedSight): a close view from above, under the levels
// overhead.
static S32 RaisedArm(S32 base) {
    if (CubeMode != CUBE_EXTERIEUR) return base;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    S32 f;
    int seg;
    double lat, t;
    if (!RaisedAt(hero->Obj.X + CurrentCubeX * 32768.0, hero->Obj.Z + CurrentCubeY * 32768.0, hero->Obj.Y + 64, RAISED_PAD, &f, &seg, &lat, &t)
            || abs(f - hero->Obj.Y) > 300) return base;
    // the road's rise over a cell or two, the way the lap runs, and whether the car faces that way
    const double behind = -RaisedSlope(seg, hero->Obj.Beta);   // how steeply the road rises behind the car
    double arm = behind < 0.3 ? base : 2800 / behind + 1200;
    // Under another level of the road that runs with this one -- a helix, its levels one over the other -- the eye has to stay under
    // that level: over the car, and over the road a few cells before or after it. (A level that only crosses overhead hides the car
    // for a moment and is let pass.)
    double ceiling = 1e18;
    int over[3] = {0, 0, 0};
    for (int k = 0; k < 3; k++) {
        int i = seg + (k - 1) * RAISED_STACK;
        if (s_raisedLoop) { const int n = s_nbRaised - 1; i = ((i % n) + n) % n; }
        else if (i < 0 || i > s_nbRaised - 1) continue;
        double level;
        if (!RaisedBetween(s_raised[i].X, s_raised[i].Z, s_raised[i].Y + 800.0, s_raised[i].Y + 9000.0, i, &level)) continue;
        over[k] = 1;
        if (level - s_raised[i].Y < ceiling) ceiling = level - s_raised[i].Y;
    }
    if (over[1] && (over[0] || over[2])) {
        double up = sin((AlphaCam & 4095) * 2 * 3.14159265358979 / 4096.0);
        if (up < 0.2) up = 0.2;
        const double under = (ceiling - RAISED_UNDER) / up;     // the arm whose eye is RAISED_UNDER below the level overhead
        if (under < arm) arm = under;
    }
    return arm < 3000 ? 3000 : arm > base ? base : (S32)arm;
}

// The tunnel the car is in, or coming up to (RACE_TUNNEL_LEAD points before its mouth): -1 none. `seg` a raised road segment.
static int InTunnel(int seg) {
    const int n = s_raisedLoop ? s_nbRaised - 1 : s_nbRaised;
    for (int k = 0; k < s_nbTunnels && n > 0; k++) {
        const T_RACE_TUNNEL *tn = &s_tunnels[k];
        const int from = ((tn->From - RACE_TUNNEL_LEAD) % n + n) % n;
        const int span = ((tn->To + RACE_TUNNEL_TAIL - from) % n + n) % n, at = ((seg - from) % n + n) % n;
        if (at <= span) return k;
    }
    return -1;
}

void RaceMod_FloorFor(S32 y) { s_floorRef = s_nbRaised ? y : RAISED_NOREF; }
void RaceMod_FloorForNothing(void) { s_floorRef = RAISED_NOREF; }

// The hero kept on the raised road: standing on it at (ox, oy, oz) (cube-local) -- not under it -- a move to (*nx, *nz) that would take
// his middle nearer than RAISED_INSET to an edge ends on that line instead, beside the same point of the road; a move that leaves the
// road altogether is not made. Where the road lies on the ground (its ends, coming down onto it) he may drive off it to the side -- but
// not on a side where the ground beside it falls away: the lava lake's ramp comes down onto the dock along the island's west cliff, and
// a car against its left rail, there where the road met the ground, went over the cliff into the sea (2026-10-02).
#define RAISED_GROUNDED 300                // a road no higher than this over the ground under it lies on the ground
#define RAISED_BESIDE 384                  // how far past the floor's edge the ground beside the road is looked at

// Whether a place (world units, at height wy) is past one of the road's open ends -- its own first or last point (a road that isn't a
// loop), or a jump's lip, where its gap begins -- beyond the end, within the road's width there and level with it: no edge to be held at.
// (RaisedAt finds the segment before the open one, clamped to its end: a car against a rail there was pulled back beside that point at
// every step -- at the foot of the lava lake's ramp, and at a jump's lip, where the car on the rail then went nowhere and dropped into
// the lava, 2026-10-02. The first version of this looked at any place within seven cells of the road's last point, at any height: it
// let go of the rail on the lava lap's third jump, 6,000 above the ramp's foot, and a car on its right dropped onto the track below.)
static int RaisedPastEnd(double wx, double wz, double wy) {
    for (int i = 0; i < s_nbRaised; i++) {
        const T_RACE_RAISED *tip = &s_raised[i];
        if (tip->Half <= 0) continue;
        for (int end = 0; end < 2; end++) {
            // (end 1: the last point before a gap or the road's end; end 0: the first point after one)
            const int open = end ? (i + 1 >= s_nbRaised ? !s_raisedLoop : s_raised[i + 1].Half <= 0)
                                 : (i == 0 ? !s_raisedLoop : s_raised[i - 1].Half <= 0);
            const int j = end ? i - 1 : i + 1;
            if (!open || j < 0 || j >= s_nbRaised) continue;
            double ox = tip->X - s_raised[j].X, oz = tip->Z - s_raised[j].Z;
            const double len = sqrt(ox * ox + oz * oz);
            if (len <= 0) continue;
            ox /= len; oz /= len;                                           // (out of the road at that end)
            const double dx = wx - tip->X, dz = wz - tip->Z, along = dx * ox + dz * oz, side = fabs(dx * oz - dz * ox);
            if (along > 0 && along < 4 * 512.0 && side < tip->Half + RAISED_PAD && fabs(wy - tip->Y) < RAISED_LEVEL) return 1;
        }
    }
    return 0;
}

void RaceMod_Rail(S32 ox, S32 oy, S32 oz, S32 *nx, S32 *nz) {
    if (!s_nbRaised || CubeMode != CUBE_EXTERIEUR) return;
    if (s_flying >= 0 || s_arcOn >= 0) return; // (a jump's flight is scripted: the rail held a car on it back at the lip, and the flight went nowhere)
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    S32 f;
    int seg;
    double lat, t;
    if (!RaisedAt(ox + cx, oz + cz, oy + 64, RAISED_PAD, &f, &seg, &lat, &t)) return;
    if (oy < f - 200 || oy > f + 1500) return;
    const int grounded = f - CalculAltitudeTerrain(ox, oz, 0) < RAISED_GROUNDED;
    if (!RaisedAt(*nx + cx, *nz + cz, oy + 64, 4 * 512, &f, &seg, &lat, &t)) {
        if (grounded) return;
        *nx = ox; *nz = oz; s_railTouch = 1; return;
    }
    // (past the road's own end: onto the ground it comes down to)
    if ((!s_raisedLoop && ((seg == 0 && t <= 0) || (seg == s_nbRaised - 2 && t >= 1))) || RaisedPastEnd(*nx + cx, *nz + cz, oy)) return;
    const T_RACE_RAISED *a = &s_raised[seg], *c = a + 1;
    const double half = a->Half + (c->Half - a->Half) * t, limit = half - RAISED_INSET;
    if (fabs(lat) <= limit) return;
    const double sx = c->X - a->X, sz = c->Z - a->Z, len = sqrt(sx * sx + sz * sz);
    const double px = a->X + sx * t, pz = a->Z + sz * t, side = lat > 0 ? limit : -limit;
    if (grounded) {
        // the ground just past the floor on the side he is going: about as high as the road, he drives off onto it
        const double out = (lat > 0 ? 1 : -1) * (half + RAISED_PAD + RAISED_BESIDE);
        const S32 bx = (S32)floor(px - sz / len * out - cx + 0.5), bz = (S32)floor(pz + sx / len * out - cz + 0.5);
        if (f - CalculAltitudeTerrain(bx, bz, 0) < RAISED_GROUNDED) return;
    }
    s_railTouch = 1;
    *nx = (S32)floor(px - sz / len * side - cx + 0.5);
    *nz = (S32)floor(pz + sx / len * side - cz + 0.5);
}

// The hero is not let under the surface of the road he stood on. The engine lowers an object that nothing of the ground holds up by half
// a brick to see whether it stands on another object, and leaves it lowered when that finds one: with an opponent's car against his --
// their boxes overlapping -- he rode half a brick into the road. Called with the rail (RaceMod_Rail) after everything else in his move:
// the same car's box also pushed him sideways in places after the rail had had its say, past the road's edge, and he fell to the level
// below. Baldino's car, the widest of the opponents' (its box 1,396 across, the racer's 1,266, the bike's 894), did it the most.
void RaceMod_Floor(S32 ox, S32 oy, S32 oz, S32 nx, S32 *ny, S32 nz) {
    if (!s_nbRaised || CubeMode != CUBE_EXTERIEUR) return;
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    S32 f;
    int seg;
    double lat, t;
    // (standing on the road -- or a little over it, a jump on foot -- where he was)
    if (!RaisedAt(ox + cx, oz + cz, oy + 64, RAISED_PAD, &f, &seg, &lat, &t) || oy < f - 200 || oy > f + 1500) return;
    if (f - CalculAltitudeTerrain(ox, oz, 0) < 300) return;
    if (!RaisedAt(nx + cx, nz + cz, oy + 64, RAISED_PAD, &f, &seg, &lat, &t)) return;
    if (*ny < f) *ny = f;
}

// ---- a jump flown at the car's speed ------------------------------------------------------------------------------------------------
// A jump is a scripted flight: the hero's track script plays one of RACE_JUMP_ANIM_FIRST..LAST (the build's copy of the retail flight,
// sized to the gap: each island's own, so the tracks of several islands can be built into one folder), and
// the animation's own steps carry him along its arc in its own time, whatever speed the car came to the take-off at. Here the flight's
// clock runs as many times as fast as the car's speed at the take-off is over the flight's own speed (its keyframes' steps along the
// ground over their time): the same arc, so the same landing on the far ramp, flown faster by a faster car and slower by a slower one,
// within jump_speed's bounds (a car crawling onto the take-off still jumps, not in slow motion). The car keeps its speed through the air
// (BUGGY.CPP leaves it alone while the animation carries the car), so it lands going as fast as it took off.
// The animation moves on a keyframe at most per frame, and the engine drops the time it ran past a keyframe's end (LastTimer = Time):
// that time is carried into the next keyframe here, or a flight flown fast would lose a good part of its rate to it (at 60 frames a
// second, twice as fast, a third of it).
#define RACE_JUMP_ANIM_FIRST 200           // the flights' generic animations (Terrain/RaceTrackJumpAnim.cs Generic..Last: 200 + the island's
#define RACE_JUMP_ANIM_LAST 295            // number, 12 more for a track in the island's other file, 24 more for each of a track's jumps after its first)
static double s_jumpLo = 0.6, s_jumpHi = 4.0; // jump_speed=<lo> <hi>: the bounds of the rate (0: the flight in its own time)
static double s_jumpRate = 0;              // the flight's clock rate (0: not flying)
static double s_jumpFrac = 0;              // the part of a millisecond of the flight's clock carried to the next frame
static U32 s_jumpOver = 0;                 // how far the flight's clock ran past a keyframe's end this frame

// The flight's own speed along the ground, world units a second: its keyframes' root steps over their times, each as the engine moves
// the car by it (LIB386/ANIM/INTERDEP.CPP: by M = M(Alpha) M(Gamma) M(Beta) of the car's angles, which a flight that pitches the car --
// the lava lake's drop -- turns as it goes: its steps are in the car's own pitched frame).
static double FlightSpeed(const T_OBJ_3D *obj) {
    const U8 *anim = (const U8 *)obj->Anim.Ptr;
    if (TransFctAnim) anim = (const U8 *)TransFctAnim(obj->Anim.Num);
    if (!anim) return 0;
    const S32 frames = ((const U16 *)anim)[0], groups = ((const U16 *)anim)[1];
    const double unit = 2 * 3.14159265358979 / 4096;
    double along = 0, ms = 0, alpha = obj->Alpha, beta = obj->Beta, gamma = obj->Gamma;
    for (S32 k = 1; k < frames; k++) {
        const S16 *f = (const S16 *)(anim + 8 + k * (groups + 1) * 8); // time, step X, Y, Z, master bits, Alpha, Beta, Gamma, ...
        double da = 0, db = 0, dg = 0;
        if (f[4] & 1) { da = (S16)(f[5] << 4) / 16; db = (S16)(f[6] << 4) / 16; dg = (S16)(f[7] << 4) / 16; }
        const double a = (alpha + da / 2) * unit, b = (beta + db / 2) * unit, g = (gamma + dg / 2) * unit;
        alpha += da; beta += db; gamma += dg;
        const double sa = sin(a), ca = cos(a), sb = sin(b), cb = cos(b), sg = sin(g), cg = cos(g);
        const double wx = cg * cb * f[1] - sg * f[2] + cg * sb * f[3];
        const double wz = (sa * sg * cb - ca * sb) * f[1] + sa * cg * f[2] + (sa * sg * sb + ca * cb) * f[3];
        ms += (U16)f[0];
        along += sqrt(wx * wx + wz * wz);
    }
    return ms > 0 ? along * 1000.0 / ms : 0;
}

// Before the hero's animation is moved on (OBJECT.CPP GereObjAnim): while he flies a jump, the keyframe's start and end are brought nearer
// by the extra time the flight's clock runs this frame (or put back, flown slower), so ObjectSetInterDep finds it that much further on.
void RaceMod_JumpClock(T_OBJ_3D *obj, S32 genAnim, S32 move) {
    if (!s_active || genAnim < RACE_JUMP_ANIM_FIRST || genAnim > RACE_JUMP_ANIM_LAST || move != MOVE_BUGGY) {
        if (s_flying >= 0) { s_landed = s_flying; s_landedAt = TimerRefHR; s_flying = -1; }
        s_jumpRate = 0;
        return;
    }
    s_flying = genAnim;
    if (s_jumpHi <= 0) { s_jumpRate = 0; return; }
    if ((S32)(TimerRefHR - obj->Time) <= 0) return; // no time has passed (or the clock went back: ObjectSetInterDep's own case)
    if (!s_jumpRate) {
        const double own = FlightSpeed(obj), car = ListBuggy[0].Speed;
        double rate = own > 0 ? car / own : 1;
        if (rate < s_jumpLo) rate = s_jumpLo;
        if (rate > s_jumpHi) rate = s_jumpHi;
        s_jumpRate = rate; s_jumpFrac = 0;
        Log_Info("[racemod] jump: the car at %.0f km/h, the flight's own speed %.0f km/h: flown x%.2f", car * 3.6 / 512, own * 3.6 / 512, rate);
    }
    const U32 dt = TimerRefHR - obj->Time;
    const double run = s_jumpRate * dt + s_jumpFrac;
    const U32 step = (U32)run;
    s_jumpFrac = run - step;
    obj->LastTimer += dt - step; // (modulo 2^32: brought nearer when the step is longer than the frame)
    obj->NextTimer += dt - step;
    s_jumpOver = (S32)(TimerRefHR - obj->NextTimer) > 0 ? TimerRefHR - obj->NextTimer : 0;
}

// ... and after it: a keyframe just reached, the time run past its end goes into the next one (no more than the whole of it).
void RaceMod_JumpClockAfter(T_OBJ_3D *obj, S32 flag) {
    if (!s_jumpRate || !(flag & FLAG_FRAME) || (flag & FLAG_LAST_FRAME) || !s_jumpOver) return;
    U32 over = s_jumpOver;
    const U32 whole = obj->NextTimer - obj->LastTimer;
    if (over > whole) over = whole;
    obj->LastTimer -= over;
    obj->NextTimer -= over;
    s_jumpOver = 0;
}

static void LoadRaised(const char *path) {
    FILE *f = fopen(path, "r");
    if (!f) { Log_Info("[racemod] raised road: %s could not be read", path); return; }
    int capacity = 2048, count = 0;
    T_RACE_RAISED *r = (T_RACE_RAISED *)malloc(capacity * sizeof(T_RACE_RAISED));
    char line[128];
    while (fgets(line, sizeof(line), f)) {
        int a, b, c, d, e = 0;
        if (sscanf(line, "%d %d %d %d %d", &a, &b, &c, &d, &e) < 4) continue;
        if (count == capacity) { capacity *= 2; r = (T_RACE_RAISED *)realloc(r, capacity * sizeof(T_RACE_RAISED)); }
        r[count].X = a; r[count].Z = b; r[count].Y = c; r[count].Half = d; r[count].Bank = e;
        count++;
    }
    fclose(f);
    if (count < 2) { free(r); return; }
    // the squares over its extent, each with the points in it
    int x0 = 0x7FFFFFFF, z0 = 0x7FFFFFFF, x1 = -0x7FFFFFFF, z1 = -0x7FFFFFFF;
    for (int i = 0; i < count; i++) {
        const int bx = (int)floor((double)r[i].X / RAISED_BUCKET), bz = (int)floor((double)r[i].Z / RAISED_BUCKET);
        if (bx < x0) x0 = bx;
        if (bx > x1) x1 = bx;
        if (bz < z0) z0 = bz;
        if (bz > z1) z1 = bz;
    }
    s_rbX0 = x0; s_rbZ0 = z0; s_rbW = x1 - x0 + 1; s_rbH = z1 - z0 + 1;
    int widest = 0;
    for (int i = 0; i < count; i++) if (r[i].Half > widest) widest = r[i].Half;
    s_rbExtra = widest / RAISED_BUCKET;
    s_rbStart = (int *)calloc(s_rbW * s_rbH + 1, sizeof(int));
    s_rbItem = (int *)malloc(count * sizeof(int));
    for (int i = 0; i < count; i++)
        s_rbStart[((int)floor((double)r[i].Z / RAISED_BUCKET) - z0) * s_rbW + (int)floor((double)r[i].X / RAISED_BUCKET) - x0 + 1]++;
    for (int b = 0; b < s_rbW * s_rbH; b++) s_rbStart[b + 1] += s_rbStart[b];
    int *fill = (int *)calloc(s_rbW * s_rbH, sizeof(int));
    for (int i = 0; i < count; i++) {
        const int b = ((int)floor((double)r[i].Z / RAISED_BUCKET) - z0) * s_rbW + (int)floor((double)r[i].X / RAISED_BUCKET) - x0;
        s_rbItem[s_rbStart[b] + fill[b]++] = i;
    }
    free(fill);
    s_raised = r;
    s_nbRaised = count;
    s_raisedLoop = count > 3 && r[0].X == r[count - 1].X && r[0].Z == r[count - 1].Z && r[0].Y == r[count - 1].Y;
    AltitudeHook = RaisedAltitude;
    TerrainColHook = RaisedCol;
    SightFloorHook = RaisedSight;
    FollowCamArmHook = RaisedArm;
    FollowCamEyeHook = RailCamera;
    // (the camera that follows the car is turned on with the first frame: RaceMod_Frame)
    int lowest = r[0].Y, highest = r[0].Y;
    for (int i = 1; i < count; i++) {
        if (r[i].Y < lowest) lowest = r[i].Y;
        if (r[i].Y > highest) highest = r[i].Y;
    }
    Log_Info("[racemod] raised road: %d points, heights %d to %d%s", count, lowest, highest, s_raisedLoop ? ", a loop" : "");
}

S32 RaceMod_StatueShown(S32 risen) { return RaceMod_Active() && s_statue >= 0 ? s_statue != 0 : risen != 0; }

static void LoadPath(T_RACE_OPPONENT *o, const char *path) {
    FILE *f = fopen(path, "r");
    if (!f) return;
    int capacity = 4096, count = 0;
    S32 *x = (S32 *)malloc(capacity * sizeof(S32)), *z = (S32 *)malloc(capacity * sizeof(S32));
    S32 *y = (S32 *)malloc(capacity * sizeof(S32)), *v = (S32 *)malloc(capacity * sizeof(S32));
    S32 *r = (S32 *)malloc(capacity * sizeof(S32));
    int radii = 1;
    char line[128];
    while (fgets(line, sizeof(line), f) && count < capacity) {
        int a, b, c, d, e;
        const int n = sscanf(line, "%d %d %d %d %d", &a, &b, &c, &d, &e);
        if (n < 4) continue;
        x[count] = a; z[count] = b; y[count] = c; v[count] = d;
        if (n == 5) r[count] = e; else radii = 0;
        count++;
    }
    fclose(f);
    if (count < 3) { free(x); free(z); free(y); free(v); free(r); return; }
    if (!radii) { free(r); r = NULL; }
    o->X = x; o->Z = z; o->Y = y; o->V = v; o->R = r; o->Count = count;
    o->Cum = (double *)malloc((count + 1) * sizeof(double));
    o->Cum[0] = 0;
    for (int i = 1; i <= count; i++) {
        const int a = i - 1, b = i % count;
        o->Cum[i] = o->Cum[i - 1] + sqrt((double)(x[b] - x[a]) * (x[b] - x[a]) + (double)(z[b] - z[a]) * (z[b] - z[a]));
    }
    o->Length = o->Cum[count];
}

// "opponent_..." is the first opponent, "opponent2_..." the second, and so on: which one, and the rest of the key after the '_'
static T_RACE_OPPONENT *OpponentKey(const char *key, const char **rest) {
    if (!strncmp(key, "guide_", 6)) { *rest = key + 6; return &s_guide; }
    if (strncmp(key, "opponent", 8)) return NULL;
    int n = 0;
    const char *p = key + 8;
    if (*p >= '2' && *p <= '0' + RACE_MAX_OPPONENTS) n = *p++ - '1';
    if (*p != '_') return NULL;
    *rest = p + 1;
    return &s_opp[n];
}

// How hard the player's car pulls at a speed: in the gear the automatic gearbox would be in, the gear's pull (RaceSpeed in BUGGY.CPP).
static double Pull(double speed) {
    S32 g = 0;
    while (g < s_car.Gears - 1 && s_car.GearTop[g] <= speed) g++;
    double pull = (double)RACE_REF_TOP / s_car.GearTop[g];
    return pull > 4.0 ? 4.0 : pull < 0.25 ? 0.25 : pull;
}

// An opponent's speeds along its line, from the player's car: as fast as the car's steering takes each bend (the buggy turns at a fixed
// rate, SpeedRot a second, so a bend of radius R is taken at up to that rate times R) and no faster than its top gear, braking before
// the bends as hard as its brakes and pulling away after them as hard as its gears; the opponent's character scales the top speed and
// pull, and the cornering. Two rounds each way, since the lap is a loop.
static void LoopCaps(const T_RACE_OPPONENT *o, double *v);
static void PlanSpeeds(T_RACE_OPPONENT *o) {
    if (!o->R) return;
    const int count = o->Count;
    const double top = (double)s_car.GearTop[s_car.Gears - 1] * o->Top / 100.0;
    const double turn = s_car.Steer * 2 * 3.14159265358979 / 4096.0 * o->Grip / 100.0;
    const double brake = s_car.Brake, accel = s_car.Accel * o->Top / 100.0;
    double *v = (double *)malloc(count * sizeof(double));
    if (s_gravity > 0 && s_nbRaised) {
        // A rollercoaster of a lap (gravity=): the line's slopes pull at the car as they do at the player's (BUGGY.CPP
        // RaceSpeedOnSlope) -- the engine only up to the share of its top speed it holds on a climb, the slope's own pull, and the drag
        // that takes a car's speed over its top gear's back off it. Braking for a bend, the slope counts too.
        double *pullBySlope = (double *)malloc(count * sizeof(double)), *share = (double *)malloc(count * sizeof(double));
        for (int i = 0; i < count; i++) {
            const int next = (i + 1) % count;
            const double ds = o->Cum[i + 1] - o->Cum[i];
            const double slope = ds > 1 ? (o->Y[next] - o->Y[i]) / ds : 0;
            pullBySlope[i] = -s_gravity * RACE_GRAVITY * slope / sqrt(1 + slope * slope);
            share[i] = RaceMod_ClimbShare(s_gravity, slope);
        }
        // (over its top gear's speed the car steers as much quicker as it goes faster -- BUGGY.CPP -- so a bend it can take at that
        // speed it can take at any; and a banked bend turns it: the line's radii come with the banking counted in)
        for (int i = 0; i < count; i++) {
            const double limit = turn * o->R[i];
            v[i] = limit < top ? limit : 4 * top;
            if (v[i] < 400) v[i] = 400;
        }
        LoopCaps(o, v);
        for (int round = 0; round < 3; round++) {
            for (int i = 0; i < count; i++) {
                const int next = (i + 1) % count;
                const double ds = o->Cum[i + 1] - o->Cum[i];
                double a = pullBySlope[i];
                if (v[i] < top * share[i]) a += accel * Pull(v[i] / share[i]);
                if (v[i] > top) a -= RACE_OVERSPEED_DRAG * (v[i] - top);
                double reach2 = v[i] * v[i] + 2 * a * ds;
                // (the engine holds the car at its share of the top speed up a climb: the slope does not slow it further)
                if (a < 0 && v[i] >= top * share[i] && reach2 < top * share[i] * top * share[i]) reach2 = top * share[i] * top * share[i];
                const double reach = reach2 > 400.0 * 400.0 ? sqrt(reach2) : 400;
                if (v[next] > reach) v[next] = reach;
            }
            for (int i = count - 1; i >= 0; i--) {
                const int next = (i + 1) % count;
                const double ds = o->Cum[i + 1] - o->Cum[i];
                double slowing = brake - pullBySlope[i];
                if (slowing < brake / 4) slowing = brake / 4;
                const double reach = sqrt(v[next] * v[next] + 2 * slowing * ds);
                if (v[i] > reach) v[i] = reach;
            }
        }
        free(pullBySlope);
        free(share);
    } else {
        for (int i = 0; i < count; i++) {
            double limit = turn * o->R[i];
            v[i] = limit < top ? limit : top;
            if (v[i] < 400) v[i] = 400;
        }
        LoopCaps(o, v);
        for (int round = 0; round < 2; round++) {
            for (int i = count - 1; i >= 0; i--) {
                const int next = (i + 1) % count;
                const double ds = o->Cum[i + 1] - o->Cum[i];
                const double reach = sqrt(v[next] * v[next] + 2 * brake * ds);
                if (v[i] > reach) v[i] = reach;
            }
            for (int i = 0; i < count; i++) {
                const int next = (i + 1) % count;
                const double ds = o->Cum[i + 1] - o->Cum[i];
                const double reach = sqrt(v[i] * v[i] + 2 * accel * Pull(v[i]) * ds);
                if (v[next] > reach) v[next] = reach;
            }
        }
    }
    double lap = 0;
    for (int i = 0; i < count; i++) {
        o->V[i] = (S32)v[i];
        lap += (o->Cum[i + 1] - o->Cum[i]) / v[i];
    }
    free(v);
    Log_Info("[racemod] opponent line of %d points planned from the car: a perfect lap %.1f s (skill %d%%: %.1f s)", count, lap,
             o->Pace, lap * 100.0 / o->Pace);
}

static void PathAt(const T_RACE_OPPONENT *o, double s, double *x, double *z, double *y, double *v, double *dx, double *dz);
static void LoopWindow(T_RACE_LOOP *l);
static void OpponentLoops(T_RACE_OPPONENT *o);
static double LoopsTime(const T_RACE_OPPONENT *o);
static double LoopAim(const T_RACE_LOOP *l);

// the qualifying times' randomness: 0..1
static double RaceRandom(void) {
    s_seed = s_seed * 1103515245u + 12345u;
    return ((s_seed >> 8) & 0xFFFFFF) / 16777216.0;
}

// An opponent on a spot (a grid spot, or a pit spot while the player qualifies): along its line at the line's point nearest the spot
// (behind the start line: its lap 1 is ahead), with the way across to the spot kept (MergeX..) and faded out as it drives off.
static double LineDistance(const T_RACE_OPPONENT *o, int i, double wx, double wy, double wz);

static void OnSpot(T_RACE_OPPONENT *o, const T_RACE_SPOT *spot) {
    const double wx = spot->CubeX * 32768.0 + spot->X, wz = spot->CubeZ * 32768.0 + spot->Z;
    int best = 0;
    double bd = 1e30;
    // (the line's nearest point at the spot's height: a road that winds up over its own start has points of its other levels over
    // the grid, and a car put on one of those started the race a third of a lap ahead)
    for (int i = 0; i < o->Count; i++) {
        const double d = LineDistance(o, i, wx, spot->Y, wz);
        if (d < bd) { bd = d; best = i; }
    }
    o->S = o->Cum[best];
    o->Laps = o->S > o->Length / 2 ? 0 : 1;
    double x, z, y, v, dx, dz;
    PathAt(o, o->S, &x, &z, &y, &v, &dx, &dz);
    o->MergeX = wx - x; o->MergeZ = wz - z; o->MergeY = spot->Y - y;
    o->MergeLeft = RACE_MERGE;
    o->SpotBeta = spot->Beta;
    o->Speed = 0;
}

static S32 RaceCameraEye(S32 *eye, S32 *target);

// The keys an island with no track keeps from the set (Load's carOnly): the car's handling, and the set's own.
static int CarKey(const char *key) {
    static const char *const keys[] = {"gears", "accel", "brake", "coast", "reverse", "steer", "automatic", "hud", "track", "tired"};
    for (size_t i = 0; i < sizeof(keys) / sizeof(keys[0]); i++) if (!strcmp(key, keys[i])) return 1;
    return !strncmp(key, "gear", 4) && key[4] >= '1' && key[4] <= '0' + RACE_MAX_GEARS && !key[5];
}

// "<var> <value> [<var> <value>]...": the game variables a result sets, as pairs in `sets`; how many numbers were read.
static int ReadSets(const char *v, int *sets) {
    int n = 0, used = 0, a, b;
    while (n < 2 * RACE_MAX_SETS && sscanf(v, "%d %d%n", &a, &b, &used) == 2) { sets[n++] = a; sets[n++] = b; v += used; }
    return n;
}

static void Load(const char *path, int carOnly = 0) {
    FILE *f = fopen(path, "r");
    if (!f) return;
    snprintf(s_loaded, sizeof(s_loaded), "%s", path);
    // (the set's own keys are read once: a track's file loaded later doesn't add to them)
    const int setRead = s_nbTracks > 0 || s_tiredVar >= 0;
    char line[512];
    while (fgets(line, sizeof(line), f)) {
        char *hash = strchr(line, '#');
        if (hash) *hash = 0;
        char *eq = strchr(line, '=');
        if (!eq) continue;
        *eq = 0;
        char key[64];
        if (sscanf(line, " %63s", key) != 1) continue;
        char *v = eq + 1;
        char *end = v + strlen(v);
        while (end > v && (end[-1] == '\n' || end[-1] == '\r' || end[-1] == ' ')) *--end = 0;
        while (*v == ' ') v++;
        if (carOnly && !CarKey(key)) continue;
        if (!strcmp(key, "gears")) s_car.Gears = atoi(v);
        else if (!strncmp(key, "gear", 4) && key[4] >= '1' && key[4] <= '0' + RACE_MAX_GEARS && !key[5]) s_car.GearTop[key[4] - '1'] = atoi(v);
        else if (!strcmp(key, "accel")) s_car.Accel = Milli(v);
        else if (!strcmp(key, "brake")) s_car.Brake = Milli(v);
        else if (!strcmp(key, "coast")) s_car.Coast = Milli(v);
        else if (!strcmp(key, "reverse")) s_car.Reverse = atoi(v);
        else if (!strcmp(key, "steer")) s_car.Steer = atoi(v);
        else if (!strcmp(key, "automatic")) s_car.Automatic = atoi(v);
        else if (!strcmp(key, "hud")) s_car.Hud = atoi(v);
        else if (!strcmp(key, "startline")) {
            T_RACE_LINE l;
            if (ReadLine(v, &l)) {
                s_car.HasLine = 1;
                s_car.LineCubeX = l.CubeX; s_car.LineCubeZ = l.CubeZ;
                s_car.LineX0 = l.X0; s_car.LineZ0 = l.Z0; s_car.LineX1 = l.X1; s_car.LineZ1 = l.Z1;
                s_car.LineDirX = l.DirX; s_car.LineDirZ = l.DirZ;
                s_lineY = l.Y; s_lineHasY = l.HasY;
            }
        } else if (!strcmp(key, "checkpoint")) {
            if (s_nbCheckpoints < RACE_MAX_CHECKPOINTS && ReadLine(v, &s_checkpoints[s_nbCheckpoints])) s_nbCheckpoints++;
        } else if (!strcmp(key, "checkpoint_lines")) s_showCheckpoints = atoi(v);
        else if (!strcmp(key, "grid")) {
            T_RACE_SPOT *g = &s_spots[s_nbSpots];
            if (s_nbSpots < RACE_MAX_SPOTS && sscanf(v, "%d %d %d %d %d %d", &g->CubeX, &g->CubeZ, &g->X, &g->Y, &g->Z, &g->Beta) == 6) s_nbSpots++;
        } else if (!strcmp(key, "pit")) {
            T_RACE_SPOT *g = &s_pits[s_nbPits];
            if (s_nbPits < RACE_MAX_SPOTS && sscanf(v, "%d %d %d %d %d %d", &g->CubeX, &g->CubeZ, &g->X, &g->Y, &g->Z, &g->Beta) == 6) s_nbPits++;
        } else if (!strcmp(key, "qualifying")) s_qualifying = atoi(v);
        else if (!strcmp(key, "weather")) s_fineWeather = !strcmp(v, "fine");
        else if (!strcmp(key, "wake_flight")) s_wakeFlight = atoi(v);
        else if (!strcmp(key, "raised")) LoadRaised(v);
        else if (!strcmp(key, "statue")) s_statue = atoi(v);
        else if (!strcmp(key, "rescue")) s_rescueOn = atoi(v);
        else if (!strcmp(key, "gravity")) s_gravity = atof(v);
        else if (!strcmp(key, "jump_speed")) {
            if (sscanf(v, "%lf %lf", &s_jumpLo, &s_jumpHi) != 2) s_jumpHi = 0;
        }
        else if (!strcmp(key, "railcam")) {
            if (sscanf(v, "%lf %lf %lf", &s_railBack, &s_railUp, &s_railLead) != 3) s_railBack = 0;
        }
        else if (!strcmp(key, "holo_arrow")) s_holoArrow = atoi(v);
        else if (!strcmp(key, "island_file")) {
            if (sscanf(v, "%d %31s", &s_fileIsland, s_fileName) != 2) s_fileIsland = -1;
        }
        else if (!strcmp(key, "loop")) {
            T_RACE_LOOP *l = &s_loops[s_nbLoops];
            double dx, dz, gap, band = 1.3 * 512;
            if (s_nbLoops < RACE_MAX_LOOPS
                    && sscanf(v, "%d %d %lf %lf %lf %lf %lf %lf %lf %lf %lf", &l->CubeX, &l->CubeZ, &l->X, &l->Y, &l->Z, &dx, &dz, &l->R, &l->Shift, &gap, &band) >= 10) {
                const double len = sqrt(dx * dx + dz * dz);
                if (len > 0 && l->R > 0) {
                    l->DirX = dx / len; l->DirZ = dz / len;
                    l->Gap = gap * 3.14159265358979 / 180;
                    l->Band = band;
                    s_nbLoops++;
                }
            }
        }
        else if (!strcmp(key, "arcjump")) {
            T_RACE_ARC *a = &s_arcs[s_nbArcs];
            a->Side = 1;
            if (s_nbArcs < RACE_MAX_ARCS && sscanf(v, "%d %d %d %d %d", &a->Foot, &a->Lip, &a->Land, &a->LandFoot, &a->Side) >= 4) s_nbArcs++;
        }
        else if (!strcmp(key, "cube_scene")) {
            int *c = s_cubeScenes[s_nbCubeScenes];
            if (s_nbCubeScenes < RACE_MAX_CUBE_SCENES && sscanf(v, "%d %d %d", &c[0], &c[1], &c[2]) == 3) s_nbCubeScenes++;
        }
        else if (!strcmp(key, "steam")) {
            T_RACE_VENT *p = &s_vents[s_nbVents];
            int x, y, z, every;
            if (s_nbVents < RACE_MAX_VENTS && sscanf(v, "%d %d %d %d", &x, &y, &z, &every) == 4 && every > 0) {
                p->X = x; p->Y = y; p->Z = z; p->Every = every; p->Next = 0; s_nbVents++;
            }
        }
        else if (!strcmp(key, "gunner")) {
            T_RACE_GUNNER *g = &s_gunners[s_nbGunners];
            int scene, gun, franco, x, y, z;
            if (s_nbGunners < RACE_MAX_GUNNERS && sscanf(v, "%d %d %d %d %d %d", &scene, &gun, &franco, &x, &y, &z) == 6) {
                memset(g, 0, sizeof(*g));
                g->Scene = scene; g->Gun = gun; g->Franco = franco; g->MX = x; g->MY = y; g->MZ = z;
                s_nbGunners++;
            }
        }
        else if (!strcmp(key, "gasmonster")) {
            int scene, neck, size, x, z, bx, bz, by, fy, anim0;
            int used = 0;
            if (s_nbMonsters < RACE_MAX_MONSTERS && sscanf(v, "%d %d %d %d %d %d %d %d %d %d%n", &scene, &neck, &size, &x, &z, &bx, &bz, &by, &fy, &anim0, &used) == 10 && anim0 >= 0) {
                T_RACE_MONSTER *m = &s_monsters[s_nbMonsters++];
                memset(m, 0, sizeof(*m));
                m->Scene = scene; m->Neck = neck; m->Body = size; m->RiseX = x; m->RiseZ = z; m->BiteX = bx; m->BiteZ = bz; m->BiteY = by; m->FootY = fy; m->Anim0 = anim0;
                m->Shown = -1;
                m->SeenScene[0] = scene; m->SeenActor[0] = neck; m->NbSeen = 1;
                // (and its actor in each other scene it is seen from)
                const char *rest = v + used;
                int sc, ac, n2 = 0;
                while (m->NbSeen < RACE_MONSTER_SEEN && sscanf(rest, "%d %d%n", &sc, &ac, &n2) == 2) {
                    m->SeenScene[m->NbSeen] = sc; m->SeenActor[m->NbSeen] = ac; m->NbSeen++;
                    rest += n2;
                }
            }
        }
        else if (!strcmp(key, "roulette")) {
            int cx, cz, x, z, spin, ball, radius, landing, holeY, s1, s2, hole, rim, outer, under, used = 0;
            memset(&s_wheel, 0, sizeof(s_wheel));
            if (sscanf(v, "%d %d %d %d %d %d %d %d %d %d %d %d %d %d %d%n", &cx, &cz, &x, &z, &spin, &ball, &radius, &landing, &holeY, &s1, &s2, &hole, &rim, &outer, &under, &used) == 15) {
                s_wheel.CubeX = cx; s_wheel.CubeZ = cz; s_wheel.X = x; s_wheel.Z = z; s_wheel.Spin = spin / 10.0;
                s_wheel.Ball = ball; s_wheel.BallR = radius; s_wheel.Landing = landing;
                s_wheel.HoleY = holeY; s_wheel.S1 = s1; s_wheel.S2 = s2 / 100.0; s_wheel.Hole = hole; s_wheel.Rim = rim; s_wheel.Outer = outer; s_wheel.Under = under;
                const char *p = v + used;
                int part, n;
                while (s_wheel.NbParts < RACE_MAX_WHEEL_PARTS && sscanf(p, "%d%n", &part, &n) == 1) { s_wheel.Parts[s_wheel.NbParts++] = part; p += n; }
                s_wheel.On = 1; s_wheel.NbBalls = 1; s_wheel.Pockets = 18;
            }
        }
        else if (!strcmp(key, "roulette_prize")) s_prizeForce = atoi(v);
        else if (!strcmp(key, "roulette_pockets")) { if (s_wheel.On) s_wheel.Pockets = atoi(v) > 2 ? atoi(v) : 18; }
        else if (!strcmp(key, "roulette_balls")) {
            const int n = atoi(v);
            if (s_wheel.On) s_wheel.NbBalls = n < 1 ? 1 : n > RACE_MAX_BALLS ? RACE_MAX_BALLS : n;
        }
        else if (!strcmp(key, "gunner_target")) {
            int x, z, y, r;
            if (s_nbGunners > 0 && sscanf(v, "%d %d %d %d", &x, &z, &y, &r) == 4) {
                T_RACE_GUNNER *g = &s_gunners[s_nbGunners - 1];
                if (g->NbTargets < RACE_MAX_GUN_TARGETS) {
                    g->TX[g->NbTargets] = x; g->TZ[g->NbTargets] = z; g->TY[g->NbTargets] = y; g->TR[g->NbTargets] = r; g->NbTargets++;
                }
            }
        }
        else if (!strcmp(key, "lava_ball_model")) s_lavaModel = atoi(v);
        else if (!strcmp(key, "lavafall")) {
            int lx, ly, lz, fx, fy, fz, beta;
            if (s_nbFalls < RACE_MAX_FALLS && sscanf(v, "%d %d %d %d %d %d %d", &lx, &ly, &lz, &fx, &fy, &fz, &beta) == 7) {
                T_RACE_FALL *f = &s_falls[s_nbFalls++];
                f->LipX = lx; f->LipY = ly; f->LipZ = lz; f->FootX = fx; f->FootY = fy; f->FootZ = fz; f->Beta = beta & 4095;
            }
        }
        else if (!strcmp(key, "boostpanel")) {
            int x, y, z, dx, dz, hl, hw;
            if (s_nbPanels < RACE_MAX_PANELS && sscanf(v, "%d %d %d %d %d %d %d", &x, &y, &z, &dx, &dz, &hl, &hw) == 7 && (dx || dz) && hl > 0 && hw > 0) {
                T_RACE_PANEL *p = &s_panels[s_nbPanels++];
                const double l = sqrt((double)dx * dx + (double)dz * dz);
                p->X = x; p->Y = y; p->Z = z; p->Dx = dx / l; p->Dz = dz / l; p->HalfLen = hl; p->HalfWidth = hw; p->OnHero = 0;
            }
        }
        else if (!strcmp(key, "lavaball")) {
            T_RACE_LAVA *b = &s_lava[s_nbLava];
            int x, y, z, from, to, every, phase;
            if (s_nbLava < RACE_MAX_LAVA && sscanf(v, "%d %d %d %d %d %d %d", &x, &y, &z, &from, &to, &every, &phase) == 7 && every > 0) {
                memset(b, 0, sizeof(*b));
                b->SrcX = x; b->SrcY = y; b->SrcZ = z; b->From = from; b->To = to; b->Every = every; b->Phase = phase; b->Ball = b->Shadow = -1;
                s_nbLava++;
            }
        }
        else if (!strcmp(key, "shuttle")) {
            T_RACE_SHUTTLE *sh = &s_shuttles[s_nbShuttles];
            int scene, actor, x, y, z;
            if (s_nbShuttles < RACE_MAX_SHUTTLES && sscanf(v, "%d %d %d %d %d", &scene, &actor, &x, &y, &z) == 5 && actor > 0) {
                memset(sh, 0, sizeof(*sh));
                sh->Scene = scene; sh->Actor = actor; sh->X = x; sh->Y = y; sh->Z = z;
                s_nbShuttles++;
            }
        }
        else if (!strcmp(key, "tunnel")) {
            T_RACE_TUNNEL *t = &s_tunnels[s_nbTunnels];
            int from, to, roof;
            if (s_nbTunnels < RACE_MAX_TUNNELS && sscanf(v, "%d %d %d", &from, &to, &roof) == 3 && roof > 0) {
                t->From = from; t->To = to; t->Roof = roof; s_nbTunnels++;
            }
        }
        else if (!strcmp(key, "jet")) {
            T_RACE_JET *p = &s_jets[s_nbJets];
            int x, y, z, a, b, c, d, e, f, g, h = RACE_STEAM_HIGH;
            const int n = s_nbJets < RACE_MAX_JETS ? sscanf(v, "%d %d %d %d %d %d %d %d %d %d %d", &x, &y, &z, &a, &b, &c, &d, &e, &f, &g, &h) : 0;
            memset(p, 0, sizeof(*p));
            p->X = x; p->Y = y; p->Z = z; p->Burst = -1; p->High = h;
            // (x y z half reach on off phase dirx dirz [high]: across the road, from Half out of its middle, High over the deck; x y z reach
            // on off phase: up out of it)
            if ((n == 10 || n == 11) && a > 0 && b > 0 && c > 0 && d >= 0 && (f || g)) {
                const double len = sqrt((double)f * f + (double)g * g);
                p->Half = a; p->Reach = b; p->On = c; p->Off = d; p->Phase = e; p->DirX = f / len; p->DirZ = g / len;
                s_nbJets++;
            } else if (n == 7 && a > 0 && b > 0 && c >= 0) {
                p->Reach = a; p->On = b; p->Off = c; p->Phase = d;
                s_nbJets++;
            }
        }
        else if (!strcmp(key, "drip")) {
            T_RACE_DRIP *p = &s_drips[s_nbDrips];
            int x, y, z, road, every;
            if (s_nbDrips < RACE_MAX_DRIPS && sscanf(v, "%d %d %d %d %d", &x, &y, &z, &road, &every) == 5 && every > 0 && y > road) {
                p->X = x; p->Y = y; p->Z = z; p->RoadY = road; p->Every = every; p->Next = p->LandAt = 0; s_nbDrips++;
            }
        }
        else if (!strcmp(key, "jumpcam")) {
            T_RACE_JUMPCAM *c = &s_jumpCams[s_nbJumpCams];
            if (s_nbJumpCams < RACE_MAX_JUMPCAMS && sscanf(v, "%d %d %d %d %d %d", &c->Anim, &c->CubeX, &c->CubeZ, &c->X, &c->Y, &c->Z) == 6) s_nbJumpCams++;
        }
        else if (!strcmp(key, "qualifying_seed")) s_seed = (unsigned)strtoul(v, NULL, 10);
        else if (!strcmp(key, "vargame")) {
            int n, value;
            if (s_nbHeld < RACE_MAX_HELD && sscanf(v, "%d %d", &n, &value) == 2 && n >= 0 && n < 256) { s_held[s_nbHeld][0] = n; s_held[s_nbHeld][1] = value; s_nbHeld++; }
        }
        else if (!strcmp(key, "gate")) {
            T_RACE_GATE *g = &s_gates[s_nbGates];
            int used = 0;
            if (s_nbGates < RACE_MAX_GATES && sscanf(v, "%d %d %n", &g->Var, &g->Least, &used) == 2 && used > 0) {
                snprintf(g->Text, sizeof(g->Text), "%s", v + used);
                s_nbGates++;
            }
        }
        else if (!strcmp(key, "beat")) {
            int n, used = 0;
            if (sscanf(v, "%d%n", &n, &used) == 1 && n >= 1 && n <= RACE_MAX_OPPONENTS) { s_beatOpp = n - 1; s_nbBeatSet = ReadSets(v + used, s_beatSet); }
        }
        else if (!strcmp(key, "race_laps")) s_raceLaps = atoi(v);
        else if (!strcmp(key, "main")) s_main = atoi(v) - 1;
        else if (!strcmp(key, "finish")) s_nbFinishSet = ReadSets(v, s_finishSet);
        else if (!strcmp(key, "powerups")) s_powerups = atoi(v);
        else if (!strcmp(key, "fuel_always")) s_fuelAlways = atoi(v);
        else if (!strcmp(key, "powerup_only")) {
            int used = 0, value;
            s_nbOnly = 0;
            for (const char *p = v; s_nbOnly < 8 && sscanf(p, "%d%n", &value, &used) == 1; p += used) s_onlyList[s_nbOnly++] = value;
            s_powerupOnly = s_nbOnly ? s_onlyList[0] : -1;
        }
        else if (!strcmp(key, "penguin_mode")) s_penguinWalk = strcmp(v, "fuse") != 0;
        else if (!strcmp(key, "autodrop_ms")) s_autoDrop = atoi(v);
        else if (!strcmp(key, "protect_ms")) s_protectUntil = TimerRefHR + atoi(v);   // (tests: protected from the start, without the spell's sprites)
        else if (!strcmp(key, "shrunk_ms")) s_shrunkUntil = TimerRefHR + atoi(v);     // (tests: shrunk from the start)
        else if (!strcmp(key, "oil_icon")) s_oilIcon = atoi(v);
        else if (!strcmp(key, "gold_icon")) s_goldIcon = atoi(v);
        else if (!strcmp(key, "superjet_model")) s_superModel = atoi(v);
        else if (!strcmp(key, "twinsen_small")) s_heroSmall = atoi(v);
        else if (!strcmp(key, "drive_as")) {
            s_driveSmall = -1;
            if (sscanf(v, "%d %d %d", &s_driveBody, &s_driveAnim, &s_driveSmall) < 2) s_driveBody = s_driveAnim = -1;
        }
        else if (!strcmp(key, "superjet_lean")) s_superLean = atof(v);
        else if (!strcmp(key, "superjet_turn")) s_superTurn = atoi(v);
        else if (!strcmp(key, "mushroom")) {
            // (the height it stands at: built out of sight since 2026-10-05, so another island file's scene never shows it in the air;
            // without one, an older build's, where its actor stands)
            T_RACE_MUSHROOM *m = &s_mush[s_nbMush];
            int y = 0, got = 0;
            if (s_nbMush < RACE_MAX_MUSHROOMS && (got = sscanf(v, "%d %d %d", &m->Scene, &m->Actor, &y)) >= 2) {
                m->Y = got == 3 ? y : 0; m->HasY = got == 3; m->Back = 0; s_nbMush++;
            }
        }
        else if (!strcmp(key, "drive_laser")) s_driveLaser = atoi(v);
        else if (!strcmp(key, "laser_models")) sscanf(v, "%d %d", &s_laserModel[0], &s_laserModel[1]);
        else if (!strcmp(key, "fake")) {
            if (s_nbFakeActors < RACE_MAX_FAKE_ACTORS && sscanf(v, "%d %d", &s_fakeScene[s_nbFakeActors], &s_fakeActor[s_nbFakeActors]) == 2) s_nbFakeActors++;
        }
        else if (!strcmp(key, "oil")) {
            if (s_nbOilActors < RACE_MAX_OIL_ACTORS && sscanf(v, "%d %d", &s_oilScene[s_nbOilActors], &s_oilActor[s_nbOilActors]) == 2) s_nbOilActors++;
        }
        else if (!strcmp(key, "penguin")) {
            if (s_nbPenguinActors < RACE_MAX_PENGUIN_ACTORS && sscanf(v, "%d %d", &s_penguinScene[s_nbPenguinActors], &s_penguinActor[s_nbPenguinActors]) == 2) s_nbPenguinActors++;
        }
        else if (!strcmp(key, "win")) s_nbWinSet = ReadSets(v, s_winSet);
        else if (!strcmp(key, "sprint")) s_sprint = atoi(v);
        else if (!strcmp(key, "finishline")) s_hasFinish = ReadLine(v, &s_finishLine);
        else if (!strcmp(key, "intro")) s_introText = atoi(v);
        else if (!strcmp(key, "lose")) s_loseText = atoi(v);
        else if (!strcmp(key, "wake")) {
            s_wakeActor = -1;
            if (sscanf(v, "%d %d %d", &s_wakeScene, &s_wakeText, &s_wakeActor) < 2) s_wakeScene = s_wakeText = -1;
        }
        else if (!strcmp(key, "dream")) { if (!setRead) s_dreamScene = atoi(v); }
        else if (!strcmp(key, "after_gears")) {
            int used = 0, top;
            s_nbAfterGears = 0;
            for (const char *q = v; s_nbAfterGears < RACE_MAX_GEARS && sscanf(q, "%d%n", &top, &used) == 1; q += used) s_afterGears[s_nbAfterGears++] = top;
        }
        else if (!strcmp(key, "tired")) {
            if (!setRead && sscanf(v, "%d %d %d", &s_tiredVar, &s_tiredValue, &s_tiredText) != 3) s_tiredVar = -1;
        }
        else if (!strcmp(key, "track")) {
            T_RACE_TRACK *t = &s_tracks[s_nbTracks];
            int used = 0;
            if (!setRead && s_nbTracks < RACE_MAX_TRACKS && sscanf(v, "%d %d %d %d %n", &t->Island, &t->When, &t->First, &t->Last, &used) == 4 && used > 0 && v[used]) {
                snprintf(t->Path, sizeof(t->Path), "%s", v + used);
                s_nbTracks++;
            }
        }
        else if (!strcmp(key, "hide_actor")) {
            if (s_nbHidden < RACE_MAX_HIDDEN && sscanf(v, "%d %d", &s_hideScene[s_nbHidden], &s_hideActor[s_nbHidden]) == 2) s_nbHidden++;
        }
        else if (!strcmp(key, "beat_text")) s_beatText = atoi(v);
        else if (!strcmp(key, "parade")) {
            int n, var, value;
            if (sscanf(v, "%d %d %d", &n, &var, &value) == 3 && n >= 1 && n <= RACE_MAX_OPPONENTS) { s_paradeOpp = n - 1; s_paradeVar = var; s_paradeValue = value; }
        }
        else if (!strcmp(key, "parade_actor")) {
            if (s_nbParade < RACE_MAX_PARADE && sscanf(v, "%d %d", &s_paradeScene[s_nbParade], &s_paradeActor[s_nbParade]) == 2) s_nbParade++;
        }
        else if (!strcmp(key, "parade_park")) {
            int scene, x, y, z, beta;
            if (sscanf(v, "%d %d %d %d %d", &scene, &x, &y, &z, &beta) == 5) { s_paradeParkScene = scene; s_paradeParkX = x; s_paradeParkY = y; s_paradeParkZ = z; s_paradeParkBeta = beta; }
        }
        else {
            const char *rest;
            T_RACE_OPPONENT *o = OpponentKey(key, &rest);
            if (!o) continue;
            if (!strcmp(rest, "path")) LoadPath(o, v);
            else if (!strcmp(rest, "grid")) o->Grid = atoi(v);
            else if (!strcmp(rest, "pace")) {
                int lo, hi;
                if (sscanf(v, "%d %d", &lo, &hi) == 2 && hi >= lo) { o->PaceLo = lo; o->PaceHi = hi; o->Pace = (lo + hi) / 2; }
                else o->Pace = atoi(v);
            }
            else if (!strcmp(rest, "small")) o->Small = atoi(v);
            else if (!strcmp(rest, "top")) o->Top = atoi(v);
            else if (!strcmp(rest, "grip")) o->Grip = atoi(v);
            else if (!strcmp(rest, "catchup")) o->Catchup = atoi(v);
            else if (!strcmp(rest, "name")) snprintf(o->Name, sizeof(o->Name), "%s", v);
            else if (!strcmp(rest, "anim")) { int idle, drive; if (sscanf(v, "%d %d", &idle, &drive) == 2) { o->AnimIdle = idle; o->AnimDrive = drive; o->HasAnims = 1; } }
            else if (!strcmp(rest, "actor")) {
                int scene, actor;
                if (o->Scenes < RACE_MAX_OPPONENT_SCENES && sscanf(v, "%d %d", &scene, &actor) == 2) {
                    o->Scene[o->Scenes] = scene; o->Actor[o->Scenes] = actor; o->Scenes++;
                }
            }
        }
    }
    fclose(f);
    // (a lap with loops: the camera that follows the car looks at a loop from its side while the car goes round it; and at a drop from
    // its own place beside it)
    for (int i = 0; i < s_nbArcs; i++)
        if (!ArcPrepare(&s_arcs[i])) { Log_Info("[racemod] carried jump %d: not along the raised road -- left out", i + 1); s_arcs[i--] = s_arcs[--s_nbArcs]; }
    if (s_nbArcs) Log_Info("[racemod] %d carried jump(s), %d cube scenes", s_nbArcs, s_nbCubeScenes);
    if (s_nbLoops || s_nbJumpCams || s_nbArcs || s_nbTunnels) FollowCamEyeHook = RaceCameraEye;
    // (a roulette wheel: its head and its ball posed as their cube's decors are drawn)
    DecorPoseHook = s_wheel.On ? WheelDecors : NULL;
    if (s_wheel.On) Log_Info("[racemod] roulette wheel at (%.0f,%.0f): %d turning pieces, its floor %.0f at the hole to %.0f at the wall, the drop onto place %d", s_wheel.X, s_wheel.Z,
                             s_wheel.NbParts, WheelFloorAt(s_wheel.Hole), WheelFloorAt(s_wheel.Rim), s_wheel.Landing);
    if (s_car.Gears < 1) s_car.Gears = 1;
    if (s_car.Gears > RACE_MAX_GEARS) s_car.Gears = RACE_MAX_GEARS;
    for (S32 g = 0; g < s_car.Gears; g++)
        if (s_car.GearTop[g] < 200) s_car.GearTop[g] = g ? s_car.GearTop[g - 1] : RACE_REF_TOP;
    if (s_car.Accel < 100) s_car.Accel = 100;
    if (s_car.Brake < 100) s_car.Brake = 100;
    if (s_car.Coast < 0) s_car.Coast = 0;
    if (s_car.Reverse < 0) s_car.Reverse = 0;
    if (!s_seed) s_seed = (unsigned)time(NULL);
    // (a sprint's line has two ends: no way back from its last point to its first -- LoadPath measured one -- and it ends for the
    // opponent where it crosses the finish line, the point nearest the line's middle)
    for (int n = 0; n <= RACE_MAX_OPPONENTS && s_sprint; n++) {
        T_RACE_OPPONENT *o = n < RACE_MAX_OPPONENTS ? &s_opp[n] : &s_guide;
        if (!o->Count) continue;
        o->Cum[o->Count] = o->Cum[o->Count - 1];
        o->Length = o->Cum[o->Count - 1];
        o->FinishS = o->Length;
        if (!s_hasFinish) continue;
        const double fx = s_finishLine.CubeX * 32768.0 + (s_finishLine.X0 + s_finishLine.X1) / 2.0;
        const double fz = s_finishLine.CubeZ * 32768.0 + (s_finishLine.Z0 + s_finishLine.Z1) / 2.0;
        double bd = 1e30;
        for (int i = 0; i < o->Count; i++) {
            const double d = (o->X[i] - fx) * (o->X[i] - fx) + (o->Z[i] - fz) * (o->Z[i] - fz)
                             + (s_finishLine.HasY ? 4.0 * (o->Y[i] - s_finishLine.Y) * (double)(o->Y[i] - s_finishLine.Y) : 0);
            if (d < bd) { bd = d; o->FinishS = o->Cum[i]; }
        }
    }
    if (s_sprint) Log_Info("[racemod] a sprint: %s%s", s_hasFinish ? "to the finish line" : "to the line's end", s_wakeScene >= 0 ? ", a win wakes Twinsen up" : "");
    for (int i = 0; i < s_nbLoops; i++) LoopWindow(&s_loops[i]);
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
        T_RACE_OPPONENT *o = &s_opp[n];
        // (a skill between two: drawn each time the track is loaded, so the race is never the same twice)
        if (o->PaceHi > 0) o->Pace = o->PaceLo + (int)(RaceRandom() * (o->PaceHi - o->PaceLo + 1));
        if (o->Pace < 10) o->Pace = 10;
        if (!o->Name[0]) snprintf(o->Name, sizeof(o->Name), "Opponent %d", n + 1);
        if (o->Count && o->PaceHi > 0) Log_Info("[racemod] %s: skill %d%% (drawn from %d-%d%%)", o->Name, o->Pace, o->PaceLo, o->PaceHi);
        OpponentLoops(o);
        if (!o->Count) continue;
        PlanSpeeds(o);
        // its lap at its skill, and a qualifying time a few seconds either way of it (with the time round its loops)
        o->PlannedLap = 0;
        for (int i = 0; i < o->Count; i++) o->PlannedLap += (o->Cum[i + 1] - o->Cum[i]) / (o->V[i] > 1 ? o->V[i] : 1);
        o->PlannedLap += LoopsTime(o);
        o->PlannedLap *= 100.0 / o->Pace;
        const double spread = o->PlannedLap * 1000 * RACE_QUALIFY_SHARE < RACE_QUALIFY_SPREAD ? o->PlannedLap * 1000 * RACE_QUALIFY_SHARE : RACE_QUALIFY_SPREAD;
        o->QualTime = (S32)(o->PlannedLap * 1000 + (RaceRandom() * 2 - 1) * spread);
        // the grid: a few points before the start line
        const int grid = o->Grid < 0 ? 0 : o->Grid >= o->Count ? o->Count - 1 : o->Grid;
        o->S = grid ? o->Cum[o->Count - grid] : 0;
        o->Laps = grid ? 0 : 1;
    }
    if (s_guide.Count) {
        OpponentLoops(&s_guide);
        PlanSpeeds(&s_guide);
        Log_Info("[racemod] no opponent raced: the guide line (%d points)", s_guide.Count);
    }
    if (s_nbSpots && s_car.HasLine) {
        // where they wait until the grid is formed: their pit spots, or (with no pit lane) the grid spots behind the player
        int waiting = 0, spot = 1;
        for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
            if (!s_opp[n].Count || !s_opp[n].Scenes) continue;      // (a time to beat has no car)
            if (waiting < s_nbPits) OnSpot(&s_opp[n], &s_pits[waiting++]);
            else OnSpot(&s_opp[n], &s_spots[spot < s_nbSpots ? spot++ : s_nbSpots - 1]);
        }
        s_phase = s_qualifying ? PHASE_QUALIFYING : PHASE_WAIT;
    }
    s_ownTrack = s_car.HasLine;
}

S32 RaceMod_Active(void) {
    if (!s_checked) {
        s_checked = 1;
        Defaults(&s_car);
        const char *path = getenv("LBA2_RACETRACK_FILE");
        if (path && path[0]) {
            s_active = 1;
            Load(path);
        }
    }
    return s_active;
}

// weather=fine: Citadel Island as it is once the storm is over (the chapter-2 weather: no rain or thunder, the CITABAU island file with
// its own light and palette, the clear sky). Asked by COMMON.H's storm macros whenever the island loads -- which can be before anything
// else has asked for the race file, so this reads it too.
S32 RaceMod_FineWeather(void) {
    return RaceMod_Active() && s_fineWeather;
}

const T_RACE_CAR *RaceMod_Car(void) {
    RaceMod_Active();
    return &s_car;
}

S32 RaceMod_Gear(void) { return s_gear; }

void RaceMod_ResetGear(void) {
    s_gear = 1;
    s_lastX = CheckKey(K_X) ? 1 : 0;
    s_lastZ = CheckKey(K_Z) ? 1 : 0;
}

void RaceMod_UpdateGear(S32 speed) {
    const int x = CheckKey(K_X) ? 1 : 0, z = CheckKey(K_Z) ? 1 : 0;
    const S32 was = s_gear;
    if (s_car.Automatic) {
        // up at the gear's top speed, down when the car has slowed well into the gear below's range
        if (s_gear < s_car.Gears && speed >= s_car.GearTop[s_gear - 1] - 40) s_gear++;
        else if (s_gear > 1 && speed < s_car.GearTop[s_gear - 2] * 7 / 10) s_gear--;
    } else {
        if (x && !s_lastX && s_gear < s_car.Gears) s_gear++;
        if (z && !s_lastZ && s_gear > 1) s_gear--;
    }
    s_lastX = x;
    s_lastZ = z;
    // (every change in the log, with what made it: the keys are the game window's keyboard, so a key typed while it has the focus counts)
    if (s_gear != was) Log_Info("[racemod] gear %d (%s)", (int)s_gear, s_car.Automatic ? "automatic" : s_gear > was ? "X" : "Z");
}

static void Message(const char *text) {
    snprintf(s_message, sizeof(s_message), "%s", text);
    s_messageUntil = TimerRefHR + 2500;
}

// Whether the move (x0,z0)->(x1,z1) crosses the segment (ax,az)-(bx,bz).
static int Crosses(double x0, double z0, double x1, double z1, double ax, double az, double bx, double bz) {
    const double d1 = (bx - ax) * (z0 - az) - (bz - az) * (x0 - ax);
    const double d2 = (bx - ax) * (z1 - az) - (bz - az) * (x1 - ax);
    if ((d1 > 0) == (d2 > 0) || d1 == d2) return 0;
    const double e1 = (x1 - x0) * (az - z0) - (z1 - z0) * (ax - x0);
    const double e2 = (x1 - x0) * (bz - z0) - (z1 - z0) * (bx - x0);
    return (e1 > 0) != (e2 > 0);
}

// +1 when the move crosses the line the lap's way, -1 the other way, 0 when it doesn't cross it (or the line is in another cube).
// slack: how far past each of its ends the line counts too.
static int CrossLine(const T_RACE_LINE *l, S32 x0, S32 z0, S32 x1, S32 z1, S32 slack = 0) {
    if (CurrentCubeX != l->CubeX || CurrentCubeY != l->CubeZ) return 0;
    // (a line with a height: only for a car on the road it lies across, not one passing over or under that place)
    if (l->HasY && abs(ListObjet[NUM_PERSO].Obj.Y - l->Y) > RACE_LINE_REACH) return 0;
    double ax = l->X0, az = l->Z0, bx = l->X1, bz = l->Z1;
    const double len = sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
    if (slack > 0 && len > 0) {
        const double ux = (bx - ax) / len * slack, uz = (bz - az) / len * slack;
        ax -= ux; az -= uz; bx += ux; bz += uz;
    }
    if (!Crosses(x0, z0, x1, z1, ax, az, bx, bz)) return 0;
    return (double)(x1 - x0) * l->DirX + (double)(z1 - z0) * l->DirZ > 0 ? 1 : -1;
}

// A checkpoint is taken going the lap's way RACE_CHECKPOINT_SLACK past either end of its line: the builder stops a line short of any
// other part of the lap (a car there must not cross it), so on a tight bend, where the road comes back round, its inner end is little
// past the verge -- and a car cutting the bend went round it (the lava lake's first loop, 2026-10-01). Crossed backwards it counts only
// on its own length: past its ends lies the road coming back the other way.

// The story's results: a gate not yet open (gate=), a result's variables set (beat=, win=), and whether they are all set already.
static const T_RACE_GATE *ClosedGate(void) {
    for (int i = 0; i < s_nbGates; i++)
        if (s_gates[i].Var >= 0 && s_gates[i].Var < 256 && ListVarGame[s_gates[i].Var] < s_gates[i].Least) return &s_gates[i];
    return NULL;
}

static int SetsDone(const int *sets, int n) {
    for (int i = 0; i + 1 < n; i += 2)
        if (sets[i] >= 0 && sets[i] < 256 && ListVarGame[sets[i]] != sets[i + 1]) return 0;
    return n > 0;
}

static void ApplySets(const int *sets, int n) {
    for (int i = 0; i + 1 < n; i += 2)
        if (sets[i] >= 0 && sets[i] < 256) {
            ListVarGame[sets[i]] = (S16)sets[i + 1];
            Log_Info("[racemod] game variable %d = %d", sets[i], sets[i + 1]);
        }
}

static const char *Ordinal(int n) { return n % 100 / 10 == 1 ? "th" : n % 10 == 1 ? "st" : n % 10 == 2 ? "nd" : n % 10 == 3 ? "rd" : "th"; }

// The time to beat (beat=): its opponent's lap at its skill, ms (0: none).
static S32 BeatTime(void) {
    if (s_beatOpp < 0 || !s_opp[s_beatOpp].Count) return 0;
    return (S32)(s_opp[s_beatOpp].PlannedLap * 1000 + 0.5);
}

static double PlayerProgress(void);
static void SprintOver(S32 now, int crossed);
void RaceMod_Lap(S32 x, S32 z) {
    // (the previous frame's position; forgotten when the car changes cube, where the coordinates jump, or is put on the grid)
    static S32 px = 0, pz = 0, pcx = -1, pcz = -1;
    if (s_wantFlip) { FirstTime = AFF_ALL_FLIP; s_wantFlip = 0; }
    if (s_forgetLap) { pcx = -1; s_forgetLap = 0; }
    if (!s_active || !s_car.HasLine || CubeMode != CUBE_EXTERIEUR) { pcx = -1; return; }
    // the story's arrow to the start line (holo_arrow): Twinsen is at the wheel, so he has found it
    if (s_holoArrow >= 0) { ClrHoloPos(s_holoArrow); Log_Info("[racemod] holomap arrow %d cleared", s_holoArrow); s_holoArrow = -1; }
    const S32 x0 = px, z0 = pz;
    const int same = pcx == CurrentCubeX && pcz == CurrentCubeY;
    px = x; pz = z; pcx = CurrentCubeX; pcz = CurrentCubeY;
    if (!same || (x == x0 && z == z0)) return;

    // halfway round the lap (the line it follows, a third to two thirds along): the next crossing of the start line is a lap
    if (s_laps > 0 && !s_halfway) {
        if (const T_RACE_OPPONENT *o = Guide()) {
            PlayerProgress();
            const double s = s_playerIndex >= 0 ? o->Cum[s_playerIndex] : 0;
            if (s > o->Length / 3 && s < o->Length * 2 / 3) { s_halfway = 1; Log_Info("[racemod] halfway round lap %d", (int)s_laps); }
        }
    }

    // the checkpoints: the next one crossed the lap's way moves on; the last one crossed backwards moves back
    if (s_laps > 0)
        for (int i = 0; i < s_nbCheckpoints; i++) {
            if (i == s_next && CrossLine(&s_checkpoints[i], x0, z0, x, z) > 0) {
                s_next++;
                Log_Info("[racemod] checkpoint %d of %d", s_next, s_nbCheckpoints);
            } else if (i == s_next - 1 && CrossLine(&s_checkpoints[i], x0, z0, x, z) < 0) {
                s_next--;
                Log_Info("[racemod] checkpoint %d crossed backwards", s_next + 1);
            }
        }

    // a sprint's finish line, once the race is on
    if (s_sprint && s_hasFinish && s_laps > 0 && !s_place && s_phase == PHASE_RACE && CrossLine(&s_finishLine, x0, z0, x, z) > 0) {
        SprintOver(TimerRefHR, 1);
        return;
    }
    T_RACE_LINE line = {s_car.LineCubeX, s_car.LineCubeZ, s_car.LineX0, s_car.LineZ0, s_car.LineX1, s_car.LineZ1, s_car.LineDirX, s_car.LineDirZ, s_lineY, s_lineHasY};
    if (CrossLine(&line, x0, z0, x, z) <= 0) return;
    // (a sprint's start line counts once: its race begins there)
    if (s_sprint && s_laps > 0) return;
    const S32 now = TimerRefHR;
    // (the story's gate: the race isn't open yet -- no lap begins)
    if (s_laps == 0) {
        if (const T_RACE_GATE *gate = ClosedGate()) {
            Message(gate->Text);
            Log_Info("[racemod] start line crossed, but the race is closed: %s", gate->Text);
            return;
        }
    }
    // (a checkpoint missed -- a corner cut -- or, with none, the start line crossed again before the car has been round: no lap)
    if (s_laps > 0 && s_nbCheckpoints && s_next < s_nbCheckpoints) {
        Log_Info("[racemod] missed checkpoint %d of %d: no lap", s_next + 1, s_nbCheckpoints);
        return;
    }
    if (s_laps > 0 && !s_nbCheckpoints && !s_halfway && Guide()) {
        Log_Info("[racemod] start line crossed before halfway round: no lap");
        return;
    }
    if (s_laps > 0) {
        s_lastLap = now - s_lapStart;
        if (!s_bestLap || s_lastLap < s_bestLap) s_bestLap = s_lastLap;
        Log_Info("[racemod] lap %d in %.2f s%s", (int)s_laps, s_lastLap / 1000.0, s_phase == PHASE_QUALIFYING ? " (qualifying)" : "");
        // the time to beat: under it, the story moves on (once)
        if (const S32 target = BeatTime()) {
            char text[64];
            if (s_lastLap < target && !SetsDone(s_beatSet, s_nbBeatSet)) {
                ApplySets(s_beatSet, s_nbBeatSet);
                snprintf(text, sizeof(text), "You beat %s's time!", s_opp[s_beatOpp].Name);
                Message(text);
                Log_Info("[racemod] %s (%.2f s against %.2f s)", text, s_lastLap / 1000.0, target / 1000.0);
            } else if (s_lastLap >= target) {
                snprintf(text, sizeof(text), "%s's time not beaten", s_opp[s_beatOpp].Name);
                Message(text);
            }
        }
        // the race's last lap: the player's place, and a win's variables
        if (s_raceLaps > 0 && s_phase == PHASE_RACE && s_nbOrder > 0 && !s_place && s_laps == s_raceLaps) {
            int place = 1, racers = 1;
            for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
                if (!s_opp[n].Count || !s_opp[n].Scenes) continue;
                racers++;
                if (s_opp[n].Laps > s_raceLaps) place++;
            }
            s_place = place; s_racers = racers;
            // (with a main opponent, the race is his to lose: ahead of him is a win, whatever the others did)
            const int mainOn = s_main >= 0 && s_main < RACE_MAX_OPPONENTS && s_opp[s_main].Count && s_opp[s_main].Scenes;
            s_beatMain = mainOn ? s_opp[s_main].Laps <= s_raceLaps : place == 1;
            char text[64];
            if (mainOn) snprintf(text, sizeof(text), s_beatMain ? "You beat %s!" : "%s beat you", s_opp[s_main].Name);
            else if (place == 1) snprintf(text, sizeof(text), "You win!");
            else snprintf(text, sizeof(text), "You finished %d%s", place, Ordinal(place));
            Message(text);
            s_messageUntil = now + 6000;
            Log_Info("[racemod] race over after %d laps: %d%s of %d%s", s_raceLaps, place, Ordinal(place), racers,
                     mainOn ? (s_beatMain ? ", ahead of the main opponent" : ", behind the main opponent") : "");
            if (s_beatMain) ApplySets(s_winSet, s_nbWinSet);
            ApplySets(s_finishSet, s_nbFinishSet);
        }
        if (s_phase == PHASE_QUALIFYING) {
            // the qualifying lap: its time decides the grid (formed before the next drawing, in the start line's scene)
            s_qualifyTime = s_lastLap;
            s_wantGrid = 1;
            s_phase = PHASE_WAIT;
            return;
        }
    }
    s_laps++;
    s_lapStart = now;
    s_halfway = 0;
    s_next = 0;
}

// A sprint is over (`crossed`: the player crossed the finish line; else the one to beat got there first): the player's place, the result,
// what a win and the end set, and -- RACE_SPRINT_END_MS later, RaceMod_Story -- the dream's waking up or the race again.
static void SprintOver(S32 now, int crossed) {
    if (s_place) return;
    int place = 1, racers = 1;
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
        if (!s_opp[n].Count || !s_opp[n].Scenes) continue;
        racers++;
        if (s_opp[n].Laps > 1) place++;
    }
    if (!crossed) place = racers;          // (not there yet: last, whoever else is still on the way)
    s_place = place; s_racers = racers;
    const int mainOn = s_main >= 0 && s_main < RACE_MAX_OPPONENTS && s_opp[s_main].Count && s_opp[s_main].Scenes;
    s_beatMain = crossed && (mainOn ? s_opp[s_main].Laps <= 1 : place == 1);
    char text[64];
    if (s_beatMain && mainOn) snprintf(text, sizeof(text), "You beat %s!", s_opp[s_main].Name);
    else if (s_beatMain) snprintf(text, sizeof(text), "You win!");
    else if (mainOn) snprintf(text, sizeof(text), "%s got there first", s_opp[s_main].Name);
    else snprintf(text, sizeof(text), "You finished %d%s", place, Ordinal(place));
    Message(text);
    // (a won dream with wake_flight=: Twinsen wakes up in mid-flight, the car not held at the line)
    const S32 after = s_beatMain && s_wakeFlight > 0 && s_wakeScene >= 0 ? s_wakeFlight : RACE_SPRINT_END_MS;
    s_messageUntil = now + after;
    Log_Info("[racemod] sprint over in %.2f s: %d%s of %d%s", (now - s_lapStart) / 1000.0, place, Ordinal(place), racers, s_beatMain ? ", won" : ", lost");
    if (s_beatMain) ApplySets(s_winSet, s_nbWinSet);
    ApplySets(s_finishSet, s_nbFinishSet);
    s_endAt = now + after;
    s_endWon = s_beatMain;
}

// The scene the start line is in (cube_scene=), -1 when the file doesn't say.
static int StartScene(void) {
    if (!s_nbSpots) return -1;
    for (int c = 0; c < s_nbCubeScenes; c++)
        if (s_cubeScenes[c][0] == s_spots[0].CubeX && s_cubeScenes[c][1] == s_spots[0].CubeZ) return s_cubeScenes[c][2];
    return -1;
}

// Called once a frame from the main loop, before the scene is drawn (PERSO.CPP), where a dialog can be opened as the life scripts do:
// the intro as the grid forms, a sprint's end followed up -- a win wakes Twinsen up (out of the car, in the waking scene), a loss runs
// the race again from the grid -- and the wake line said a moment after he has woken up.
void RaceMod_Story(void) {
    // (a car lapping on its own, parade=: the whole picture drawn again -- this runs just before the frame is drawn, AffScene(FirstTime))
    if (s_paradeFlip) { FirstTime = AFF_ALL_FLIP; s_paradeFlip = 0; }
    // (and every frame of a race out of doors: with the camera still -- the car stopped, bitten, hit -- the engine draws only what moves over
    // the picture kept from the last whole one, and what the race-track mode moves outside that left its copies behind: the user,
    // 2026-10-10, a gas monster's dark double standing behind it as it struck; and the roulette wheel, decors, stood still)
    else if (RaceMod_Active() && CubeMode == CUBE_EXTERIEUR && NewCube == -1) FirstTime = AFF_ALL_FLIP;
    // (the waking scene held white: now it fades in)
    if (s_whiteUntil && TimerRefHR >= s_whiteUntil && NewCube == -1) {
        s_whiteUntil = 0;
        FadeWhiteToPalAndSamples(PtrPal);
    }
    if (!RaceMod_Active()) return;
    const S32 now = TimerRefHR;
    // (the wake line, if the waking scene's actor has said nothing since Twinsen woke up: RaceMod_Dial says it before her first line)
    if (s_pendWakeScene >= 0 && NewCube == -1) {
        if ((S32)NumCube != s_pendWakeScene) { if (s_pendWakeAt && now - s_pendWakeAt > 20000) s_pendWakeScene = -1; }
        else if (!s_pendWakeAt) s_pendWakeAt = now;
        else if (now - s_pendWakeAt >= RACE_WAKE_DIAL_MS) {
            const int text = s_pendWakeText, actor = s_pendWakeActor;
            s_pendWakeScene = -1;
            if (text >= 0) {
                NumObjDial = (U8)(actor > 0 && actor < NbObjets ? actor : NUM_PERSO);
                Log_Info("[racemod] awake: text %d said by actor %d", text, (int)NumObjDial);
                Dial(text, TRUE);
            }
        }
    }
    if (!s_sprint || CubeMode != CUBE_EXTERIEUR) return;
    // the intro, as the grid forms (the count-down from when it is read)
    if (s_introText >= 0 && !s_introShown && s_phase == PHASE_GRID) {
        s_introShown = 1;
        NumObjDial = NUM_PERSO;
        Log_Info("[racemod] the race's intro: text %d", s_introText);
        Dial(s_introText, TRUE);
        s_goAt = TimerRefHR + 4000;
        s_wantFlip = 1;
        return;
    }
    // a loss's line, before the race again
    if (s_pendLose >= 0 && s_phase == PHASE_GRID) {
        const int text = s_pendLose;
        s_pendLose = -1;
        NumObjDial = NUM_PERSO;
        Dial(text, TRUE);
        s_goAt = TimerRefHR + 4000;
        s_wantFlip = 1;
        return;
    }
    if (!s_place || !s_endAt || now < s_endAt || NewCube != -1) return;
    s_endAt = 0;
    if (s_endWon && s_wakeScene >= 0) {
        // the dream is over: Twinsen wakes up, out of his car (the car is no one's: a new game's) -- in mid-flight with wake_flight=, the
        // picture fading to white there and the waking scene fading in from it
        if (s_wakeFlight > 0) {
            Log_Info("[racemod] the dream fades to white in mid-flight");
            // (the picture as it is to white -- WhiteFade fades from black)
            U8 white[768];
            memset(white, 255, sizeof(white));
            FadePalToPal(PtrPal, white);
            s_fadeFromWhite = 1;
        }
        if (Comportement == C_BUGGY) LeaveBuggy(C_NORMAL);
        // (and on foot: LeaveBuggy leaves his move the car's, and a change of scene puts a hero whose move is the car's back in it --
        // OBJECT.CPP ChangeCube, MemoMove -- where he drove off his bed at the race's speed)
        ListObjet[NUM_PERSO].Move = MOVE_MANUAL;
        ListBuggy[0].Speed = 0;
        ResetBuggy();
        s_pendWakeScene = s_wakeScene; s_pendWakeText = s_wakeText; s_pendWakeActor = s_wakeActor; s_pendWakeAt = 0;
        const int wakeScene = s_wakeScene;
        // (and the race's track is put away: its finished race held the car still wherever Twinsen drove next, a power-up still on -- the
        // super jet-pack -- came back with the next car, and its weather stayed. The car keeps its handling: a story set's own, or the
        // race file's, with the gears Twinsen drives with once awake.)
        char path[260];
        const char *set = getenv("LBA2_RACETRACK_FILE");
        snprintf(path, sizeof(path), "%s", s_nbTracks && set ? set : s_loaded);
        int after[RACE_MAX_GEARS];
        const int nbAfter = s_nbAfterGears;
        for (int g = 0; g < nbAfter; g++) after[g] = s_afterGears[g];
        ResetTrack();
        Load(path, 1);
        s_fineWeather = 0;                  // (awake in the game's own weather, whatever the dream's)
        if (nbAfter > 0) {
            s_car.Gears = nbAfter;
            for (int g = 0; g < nbAfter; g++) s_car.GearTop[g] = after[g];
        }
        Log_Info("[racemod] the dream's race put away: the car alone, %d gears to %d", (int)s_car.Gears, (int)s_car.GearTop[s_car.Gears - 1]);
        // (a fresh start of the scene, at its own start, as a new game's or the console's `cube`: FlagChgCube 2 carries the hero's animation
        // over the change -- FlagReinit, ChangeCube -- and the scene's own opening, which lays him on his bed, lost its animation to it)
        // (with FlagChgCube 0 ChangeCube doesn't open the game clock's bracket but closes it: opened here, or it closes the one a loaded
        // game leaves open and the clock goes back to the load -- as the rescue's did, Rescue)
        SaveTimer();
        NewCube = wakeScene;
        FlagChgCube = 0;
        Log_Info("[racemod] the dream is over: Twinsen wakes up in scene %d", wakeScene);
        return;
    }
    if (s_endWon) return;
    // a loss: the race again, from the grid in the start line's scene (the car taken there)
    const int scene = StartScene();
    if (s_loseText >= 0) s_pendLose = s_loseText;
    s_place = 0; s_laps = 0; s_halfway = 0; s_next = 0; s_beatMain = 0;
    s_phase = PHASE_WAIT; s_wantGrid = 1;
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) if (s_opp[n].Count) { s_opp[n].Laps = 1; s_opp[n].S = 0; s_opp[n].Speed = 0; }
    if (scene >= 0 && !(CurrentCubeX == s_spots[0].CubeX && CurrentCubeY == s_spots[0].CubeZ)) {
        // (the game clock's bracket opened here: asked for before AffScene, whose end clears FlagChgCube before the change is made, so
        // ChangeCube doesn't open it but closes it -- as the rescue's, Rescue)
        SaveTimer();
        NewCube = scene;
        NewPosX = s_spots[0].X; NewPosY = s_spots[0].Y; NewPosZ = s_spots[0].Z;
        FlagChgCube = 1;
        FlagReajustPosTwinsen = FALSE;
    }
    s_forgetLap = 1;
    Log_Info("[racemod] the race again, from the grid in scene %d", scene);
}

// A new game (PERSO.CPP InitGame): the scene it starts in -- a set's dream= (Polar Island's dream race), else `scene`, the game's own.
S32 RaceMod_NewGameScene(S32 scene) {
    if (!RaceMod_Active() || s_dreamScene < 0) return scene;
    Log_Info("[racemod] a new game starts in the dream: scene %d", s_dreamScene);
    return s_dreamScene;
}

// A test pilot (the console's `autodrive`): while the player drives, the pedals and the steering that keep his car on the first opponent's
// line -- the bearing to a point s_autoLook ahead of the car's place on the line, steered to when the heading is more than a little off it,
// and the line's own speed (the build planned it for the reference car), braked down to when the car is quicker. For headless tests of a
// whole lap; nothing turns it on in play.
static int s_auto = 0, s_autoIndex = -1;
static double s_autoLook = 5 * 512;
static double s_autoSide = 0;              // how far to the side of the line it drives (to the left of the way it runs, x east, z south)

// How far (squared) a place is from point i of an opponent's line. With a raised road the lap passes over itself, so the height counts
// too, and twice: the line's point on another level is then never the nearest.
static double LineDistance(const T_RACE_OPPONENT *o, int i, double wx, double wy, double wz) {
    const double d = (o->X[i] - wx) * (o->X[i] - wx) + (o->Z[i] - wz) * (o->Z[i] - wz);
    return s_nbRaised ? d + 4.0 * (o->Y[i] - wy) * (o->Y[i] - wy) : d;
}

void RaceMod_AutoDrive(int on, double lookCells, double sideCells) {
    s_auto = on;
    s_autoIndex = -1;
    if (lookCells > 0) s_autoLook = lookCells * 512;
    s_autoSide = sideCells * 512;
    Log_Info("[racemod] autodrive %s (looking %.1f cells ahead, %.1f to the side)", on ? "on" : "off", s_autoLook / 512, s_autoSide / 512);
}

U32 RaceMod_AutoInput(void) {
    // (skidding on oil: the car turns round, no throttle and no brake)
    if (s_active && Comportement == C_BUGGY && TimerRefHR < s_skidUntil) return 0;   // (skidding on oil: the skid has the car, RaceMod_Skid)
    if (!(s_auto || TimerRefHR < s_jetUntil) || !s_active || Comportement != C_BUGGY || CubeMode != CUBE_EXTERIEUR) return 0;
    if (s_loopState != LOOP_NONE) return I_UP;
    const T_RACE_OPPONENT *o = Guide();
    if (!o) return 0;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    const double wx = hero->Obj.X + CurrentCubeX * 32768.0, wz = hero->Obj.Z + CurrentCubeY * 32768.0, wy = hero->Obj.Y;
    int best = -1;
    double bd = 1e30;
    if (s_autoIndex >= 0)
        for (int k = -40; k <= 80; k++) {
            const int i = ((s_autoIndex + k) % o->Count + o->Count) % o->Count;
            const double d = LineDistance(o, i, wx, wy, wz);
            if (d < bd) { bd = d; best = i; }
        }
    if (best < 0 || bd > 12.0 * 512 * 12 * 512)
        for (int i = 0; i < o->Count; i++) {
            const double d = LineDistance(o, i, wx, wy, wz);
            if (d < bd) { bd = d; best = i; }
        }
    s_autoIndex = best;
    double x, z, y, v, dx, dz;
    // (looking further ahead the faster the car goes over its top gear's speed: a rollercoaster of a lap, gravity= -- over the jet-pack's
    // own top speed: the jet-pack steers the car as much quicker, BUGGY.CPP, and looking further ahead at its speed it cut inside the
    // corners' checkpoints, and into a wall on Mosquibees Island)
    const double fastest = s_car.GearTop[s_car.Gears - 1] * JetFactor(s_jetUntil, s_jetFactor);
    const double look = ListBuggy[0].Speed > fastest ? s_autoLook * ListBuggy[0].Speed / fastest : s_autoLook;
    PathAt(o, o->Cum[best] + look, &x, &z, &y, &v, &dx, &dz);
    const double dl = sqrt(dx * dx + dz * dz);
    if (s_autoSide != 0 && dl > 0) { x += dz / dl * s_autoSide; z -= dx / dl * s_autoSide; }
    const double want = atan2(x - wx, z - wz) * 4096.0 / (2 * 3.14159265358979);
    double err = want - (hero->Obj.Beta & 4095);
    while (err >= 2048) err -= 4096;
    while (err < -2048) err += 4096;
    U32 bits = 0;
    if (err > 24) bits |= I_LEFT;
    else if (err < -24) bits |= I_RIGHT;
    // the line's speed here (the reference car's; the jet-pack's times its factor), and the car's own
    // (on Gazogem fuel all race, fuel_always=: the line's speed times its boost, as the car's own top speed is)
    const double here = o->V[best] * JetFactor(s_jetUntil, s_jetFactor) * (s_fuelAlways && (S32)TimerRefHR >= s_jetUntil ? RACE_BOOST : 1.0), speed = ListBuggy[0].Speed;
    // (coming up to a ring with a gap: held close to the speed it is leapt at -- coasting a little over it, braking only well over it --
    // and no brakes in the last cell, which a car carries into the ring)
    for (int k = 0; k < s_nbLoops; k++) {
        const T_RACE_LOOP *l = &s_loops[k];
        if (l->Gap <= 0 || l->Hi <= l->Lo || l->Hi >= s_car.GearTop[s_car.Gears - 1] || l->CubeX != CurrentCubeX || l->CubeZ != CurrentCubeY || fabs(wy - l->Y) > 600) continue;
        const double a = (hero->Obj.X - l->X) * l->DirX + (hero->Obj.Z - l->Z) * l->DirZ;
        const double c = -(hero->Obj.X - l->X) * l->DirZ + (hero->Obj.Z - l->Z) * l->DirX;
        if (a < -15 * 512 || a >= 0 || fabs(c) > l->Shift / 2 + l->Band + 1024) continue;
        // (the car's own pull in a low gear adds a couple of hundred a frame: coasting is the gentlest way down)
        const double want = LoopAim(l);
        return bits | (speed > want + 150 ? I_DOWN : speed > want - 60 ? 0 : I_UP);
    }
    // (the golden Gazogem running, or just off a boost panel: no brakes -- a player holds the accelerator, keeping every 10 km/h it gives)
    bits |= speed > here * 1.05 + 150 && !RaceMod_NoTopSpeed() && (S32)TimerRefHR - s_panelAt > RACE_PANEL_PILOT_MS ? I_DOWN : I_UP;
    return bits;
}

// An opponent's line at distance s: position (world units) and heading.
static void PathAt(const T_RACE_OPPONENT *o, double s, double *x, double *z, double *y, double *v, double *dx, double *dz) {
    // (a sprint's line has ends: held at them)
    if (s_sprint) { if (s < 0) s = 0; if (s > o->Length - 1) s = o->Length - 1 > 0 ? o->Length - 1 : 0; }
    while (s < 0) s += o->Length;
    while (s >= o->Length) s -= o->Length;
    int lo = 0, hi = o->Count;
    while (hi - lo > 1) { const int mid = (lo + hi) / 2; if (o->Cum[mid] <= s) lo = mid; else hi = mid; }
    const int a = lo, b = (lo + 1) % o->Count;
    const double span = o->Cum[lo + 1] - o->Cum[lo];
    const double t = span > 0 ? (s - o->Cum[lo]) / span : 0;
    *x = o->X[a] + (o->X[b] - o->X[a]) * t;
    *z = o->Z[a] + (o->Z[b] - o->Z[a]) * t;
    *y = o->Y[a] + (o->Y[b] - o->Y[a]) * t;
    *v = o->V[a] + (o->V[b] - o->V[a]) * t;
    *dx = o->X[b] - o->X[a];
    *dz = o->Z[b] - o->Z[a];
}

// Every frame (parade=): the driver lapping on his own -- along his line at his skill, from a standstill as the opponents pull away -- in
// this scene's copy of his car while the variable is below the value, then parked (parade_park=) or out of sight.
static double Pull(double speed);
static void ParadeStep(S32 now, S32 dt) {
    (void)now;
    s_paradeActorNow = -1;
    if (s_paradeOpp < 0 || s_paradeVar < 0 || s_paradeVar > 255) return;
    const T_RACE_OPPONENT *o = &s_opp[s_paradeOpp];
    if (!o->Count || o->Length <= 0) return;
    const int going = ListVarGame[s_paradeVar] < s_paradeValue;
    double x, z, y, v, dx, dz;
    if (going) {
        PathAt(o, s_paradeS, &x, &z, &y, &v, &dx, &dz);
        const double target = v * o->Pace / 100.0;
        if (s_paradeSpeed < target) {
            s_paradeSpeed += s_car.Accel * Pull(s_paradeSpeed) * o->Top / 100.0 * o->Pace / 100.0 * dt / 1000.0;
            if (s_paradeSpeed > target) s_paradeSpeed = target;
        } else
            s_paradeSpeed = target;
        s_paradeS += s_paradeSpeed * dt / 1000.0;
        while (s_paradeS >= o->Length) s_paradeS -= o->Length;
    }
    if (CubeMode != CUBE_EXTERIEUR) return;
    int index = -1;
    for (int i = 0; i < s_nbParade; i++)
        if (s_paradeScene[i] == (S32)NumCube && s_paradeActor[i] > 0 && s_paradeActor[i] < NbObjets) index = s_paradeActor[i];
    if (index < 0) return;
    T_OBJET *car = &ListObjet[index];
    s_paradeActorNow = index;
    U16 anim = (U16)(o->HasAnims ? o->AnimIdle : 0);
    if (going) {
        PathAt(o, s_paradeS, &x, &z, &y, &v, &dx, &dz);
        car->Obj.X = (S32)(x - CurrentCubeX * 32768.0);
        car->Obj.Z = (S32)(z - CurrentCubeY * 32768.0);
        car->Obj.Y = (S32)y;
        car->Obj.Beta = (S32)(atan2(dx, dz) * 4096.0 / (2 * 3.14159265358979)) & 4095;
        anim = (U16)(o->HasAnims ? o->AnimDrive : 1);
    } else if (s_paradeParkScene == (S32)NumCube) {
        car->Obj.X = s_paradeParkX; car->Obj.Y = s_paradeParkY; car->Obj.Z = s_paradeParkZ; car->Obj.Beta = s_paradeParkBeta & 4095;
    } else
        car->Obj.Y = -20000;
    // (the whole picture drawn again while it moves, and once as it stops: a depth-buffered car is drawn into the scene's background, and
    // the request the opponents make, s_wantFlip, is only taken up while Twinsen drives -- on foot, its first picture stayed on the road.
    // This runs inside the frame's drawing, which has its picture's kind already: RaceMod_Story asks for the next one)
    static int wasGoing = -1;
    if (going || going != wasGoing) s_paradeFlip = 1;
    wasGoing = going;
    if (car->Obj.Y <= -10000) return;
    car->Obj.Alpha = 0; car->Obj.Gamma = 0;
    ClearRealAngle(car);
    if (car->GenAnim != anim) InitAnim(anim, ANIM_REPEAT, (U8)index);
}

// MESSAGE.CPP GetText: the text that tells the time to beat (beat_text=), its "##" made the time -- the opponent's planned lap at the
// setup's skill, as the display shows it (s.cc).
S32 RaceMod_Text(S32 text, U8 **pt, S32 *size) {
    if (!RaceMod_Active() || s_beatText < 0 || text != s_beatText || !pt || !*pt || !size) return 0;
    const S32 t = BeatTime();
    if (t <= 0) return 0;
    static char buf[1024];
    char time[32];
    snprintf(time, sizeof(time), "%d.%02d", (int)(t / 1000), (int)((t % 1000) / 10));
    const char *src = (const char *)*pt;
    int n = 0;
    for (int i = 0; i < *size && src[i] && n < (int)sizeof(buf) - 40; i++) {
        if (src[i] == '#' && i + 1 < *size && src[i + 1] == '#') { n += snprintf(buf + n, sizeof(buf) - n, "%s", time); i++; }
        else buf[n++] = src[i];
    }
    buf[n++] = 0;
    *pt = (U8 *)buf;
    *size = n;
    Log_Info("[racemod] text %d said with the time to beat: %s", (int)text, buf);
    return 1;
}

static int OpponentActor(const T_RACE_OPPONENT *o) {
    for (int i = 0; i < o->Scenes; i++)
        if (o->Scene[i] == NumCube && o->Actor[i] > 0 && o->Actor[i] < NbObjets) return o->Actor[i];
    return -1;
}

static double PlayerProgress(void);
static double OpponentProgress(const T_RACE_OPPONENT *o);

// the opponents racing: a line and cars in the scenes
static int Opponents(void) {
    int n = 0;
    for (int i = 0; i < RACE_MAX_OPPONENTS; i++) if (s_opp[i].Count && s_opp[i].Scenes) n++;
    return n;
}

// The player's car on a grid spot (in the start line's scene only), standing.
static void PlayerOnSpot(const T_RACE_SPOT *spot) {
    if (CurrentCubeX != spot->CubeX || CurrentCubeY != spot->CubeZ) return;
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    hero->Obj.X = spot->X;
    hero->Obj.Y = spot->Y;
    hero->Obj.Z = spot->Z;
    hero->Obj.Beta = spot->Beta;
    ClearRealAngle(hero);
    ListBuggy[0].Speed = 0;
    ListBuggy[0].SpeedInc = 0;
    s_forgetLap = 1;
}

// The grid: every racer by its time (the player's qualifying lap; without qualifying the player is on pole), each on its spot, the laps
// counted afresh from the start line; then the grid is shown and the count-down runs.
static void FormGrid(S32 now) {
    s_nbOrder = 0;
    s_order[s_nbOrder] = -1;
    s_orderTime[s_nbOrder++] = s_qualifying ? s_qualifyTime : 0;
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++)
        if (s_opp[n].Count && s_opp[n].Scenes) { s_order[s_nbOrder] = n; s_orderTime[s_nbOrder++] = s_opp[n].QualTime; }
    for (int i = 1; i < s_nbOrder; i++)
        for (int j = i; j > 0 && s_orderTime[j] < s_orderTime[j - 1]; j--) {
            const int t = s_order[j]; s_order[j] = s_order[j - 1]; s_order[j - 1] = t;
            const S32 u = s_orderTime[j]; s_orderTime[j] = s_orderTime[j - 1]; s_orderTime[j - 1] = u;
        }
    for (int i = 0; i < s_nbOrder; i++) {
        const T_RACE_SPOT *spot = &s_spots[i < s_nbSpots ? i : s_nbSpots - 1];
        if (s_order[i] < 0) PlayerOnSpot(spot);
        else OnSpot(&s_opp[s_order[i]], spot);
    }
    s_laps = 0; s_halfway = 0; s_next = 0; s_lastLap = 0; s_bestLap = 0;
    s_place = 0;
    s_gridAt = now;
    s_goAt = now + (s_qualifying ? RACE_RESULTS_MS : 1000) + 3000;
    s_phase = PHASE_GRID;
    s_wantFlip = 1;
    char text[256];
    int used = snprintf(text, sizeof(text), "[racemod] grid:");
    for (int i = 0; i < s_nbOrder && used < (int)sizeof(text); i++)
        used += snprintf(text + used, sizeof(text) - used, " %d %s %.2f s;", i + 1, s_order[i] < 0 ? "Twinsen" : s_opp[s_order[i]].Name, s_orderTime[i] / 1000.0);
    Log_Info("%s", text);
}

// A game that starts on foot beside the car on pole (Play's start, the scene's own start): Twinsen turned to face it, so the action key
// gets him in. A scene's start keeps no facing -- he comes in facing whatever way he last did -- and the car takes him only when he
// faces it. Once, in the start line's scene.
static int s_faced = 0;
// (a track with no grid -- no opponents to line up: the old moon's loops -- has the car itself to go by, and Twinsen is brought up to its
// side, where the action key reaches it: the build leaves him a couple of cells behind it)
static void FaceTheCar(void) {
    if (s_faced || CubeMode != CUBE_EXTERIEUR) return;
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    double dx, dz;
    S32 y;
    int own = 0;
    if (s_nbSpots) {
        const T_RACE_SPOT *pole = &s_spots[0];
        if (CurrentCubeX != pole->CubeX || CurrentCubeY != pole->CubeZ) return;
        dx = pole->X - hero->Obj.X; dz = pole->Z - hero->Obj.Z; y = pole->Y;
    } else {
        if (!s_car.HasLine || !IsBuggyPresent() || CurrentCubeX != s_car.LineCubeX || CurrentCubeY != s_car.LineCubeZ) return;
        const T_OBJET *buggy = &ListObjet[NUM_BUGGY];
        dx = buggy->Obj.X - hero->Obj.X; dz = buggy->Obj.Z - hero->Obj.Z; y = buggy->Obj.Y;
        own = 1;
    }
    s_faced = 1;
    if (Comportement == C_BUGGY) return;
    if (dx * dx + dz * dz > 3.0 * 512 * 3.0 * 512 || abs(y - hero->Obj.Y) > 600) return;
    hero->Obj.Beta = (S32)(atan2(dx, dz) * 4096.0 / (2 * 3.14159265358979)) & 4095;
    if (own) {
        // (a cell short of the car's middle: within the action key's reach, outside its box)
        const double d = sqrt(dx * dx + dz * dz), step = d - 512;
        if (d > 0 && step > 0) { hero->Obj.X += (S32)(dx / d * step); hero->Obj.Z += (S32)(dz / d * step); }
    }
    ClearRealAngle(hero);
    Log_Info("[racemod] Twinsen turned to face his car (turn %d)", (int)hero->Obj.Beta);
}

// ---- back on the road -------------------------------------------------------------------------------------------------------------
// A car that goes into the lava or the sea (the ground's deadly codes) would burn or drown Twinsen, and the game would start again from
// his last save -- on Celebration Island's lava lake, after a leap that fell short or a wheel off a causeway. While he races it is put back
// on the road instead (OBJECT.CPP GereCodesJeu asks RaceMod_Rescue, and RaceMod_Frame does it): on the first opponent's line, a couple
// of cells behind the last place the car was on the ground beside it, facing the way the lap runs, stopped. The lap's count doesn't see
// the move. A track with no opponent has no line: there the game's own death stands.
#define RACE_RESCUE_BACK 2                 // line points (a cell each) behind the last safe place
static int s_safeIndex = -1;               // the first opponent's line point the car was last on the ground beside
static int s_safeScene = -1;               // ... the scene it was in then
static int s_rescue = 0;                   // asked for by GereCodesJeu, done by the next RaceMod_Frame
static int s_rescueArrive = -1;            // a rescue into the safe place's scene: the line point, finished once the scene is in (RescueArrive)

// A point of the line on the ground, in the cube the player is in: not over a jump's gap, where the line is the flight's arc.
// A place over a jump's gap in a raised road, or within a point of it (the raised point nearest it, or one either side, with no floor --
// Half 0): a car there would be in the air.
static int OverRaisedGap(double x, double z) {
    int best = -1;
    double bd = 1e30;
    for (int k = 0; k < s_nbRaised; k++) {
        const double dx = s_raised[k].X - x, dz = s_raised[k].Z - z, d = dx * dx + dz * dz;
        if (d < bd) { bd = d; best = k; }
    }
    if (best < 0) return 0;
    for (int d = -1; d <= 1; d++) {
        const int k = best + d;
        if (k >= 0 && k < s_nbRaised && s_raised[k].Half == 0) return 1;
    }
    return 0;
}

static int LineOnGround(const T_RACE_OPPONENT *o, int i) {
    if (o->X[i] / 32768 != CurrentCubeX || o->Z[i] / 32768 != CurrentCubeY) return 0;
    // (on a raised road, not over a jump's gap: a car that missed a carried jump -- stopped by a fake mushroom's blast at the ramp, 2026-10-10
    // -- drove off the lip, and its last safe place had been taken in the gap past it, the line there in the air: put back there over and
    // over, it fell for the rest of the race)
    if (s_nbRaised) return !OverRaisedGap(o->X[i], o->Z[i]);
    return abs(o->Y[i] - CalculAltitudeTerrain(o->X[i] - CurrentCubeX * 32768, o->Z[i] - CurrentCubeY * 32768, 0)) < 400;
}

static void TrackSafePlace(void) {
    const T_RACE_OPPONENT *o = Guide();
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    if (!o || CubeMode != CUBE_EXTERIEUR || hero->Move == MOVE_BUGGY || (hero->WorkFlags & FALLING)) return;
    const double wx = hero->Obj.X + CurrentCubeX * 32768.0, wz = hero->Obj.Z + CurrentCubeY * 32768.0, wy = hero->Obj.Y;
    int best = -1;
    double bd = 1e30;
    // (near the last safe place: where the lap crosses itself the other road's points are as near)
    const int window = s_safeIndex >= 0 ? 40 : o->Count;
    for (int k = -window; k <= window; k++) {
        const int i = ((s_safeIndex >= 0 ? s_safeIndex : 0) + k + 2 * o->Count) % o->Count;
        const double d = LineDistance(o, i, wx, wy, wz);
        if (d < bd) { bd = d; best = i; }
    }
    if (best < 0 || !LineOnGround(o, best) || abs(o->Y[best] - hero->Obj.Y) > 600) return;
    const double dx = o->X[best] - wx, dz = o->Z[best] - wz;
    if (dx * dx + dz * dz < 3.5 * 512 * 3.5 * 512) { s_safeIndex = best; s_safeScene = NumCube; }
}

// A hit -- a land mine going off under the car (Mosquibees Island's way round its jump) -- stops the car dead and holds it for
// RACE_HIT_HOLD ms (through RaceMod_Held): the mines are a penalty, and a hit that let the car drive on at full speed was none. It costs
// Twinsen no life: held in the blast the car takes a dozen pieces of it, and one pass through a minefield took 70 of his 100 -- a race
// is not to end in his death (HitObj, OBJECT.CPP, which asks). Non-zero: the hit was the race car's.
#define RACE_HIT_HOLD 600
static S32 s_hitUntil = 0;

S32 RaceMod_CarHit(void) {
    if (!RaceMod_Active() || Comportement != C_BUGGY) return 0;
    // (the protection spell: the mine goes off, the car drives on)
    if (TimerRefHR < s_protectUntil) { Log_Info("[racemod] the car is hit, but protected"); return 1; }
    if (TimerRefHR < s_jetUntil) { Log_Info("[racemod] the car is hit, but on the super jet-pack"); return 1; }
    ListBuggy[0].Speed = 0;
    s_hitUntil = TimerRefHR + RACE_HIT_HOLD;
    Log_Info("[racemod] the car is hit: stopped");
    return 1;
}

// ---- vertical loops ----
// The car is driven round a loop's ring as round a road bent into a circle: up from its foot, over the top upside down and down again,
// its speed changed by the climb and the drop as a real car's would be. Going round a ring of radius R it stays on while v^2/R + g cos
// theta is positive (theta 0 at the foot, half a turn at the top); RACE_LOOP_G is that g (units/s^2). The throttle pushes it on with
// RACE_LOOP_PUSH as far as the ring presses back on its wheels, the brakes slow it as far, and the steering turns it across the ring's
// band, between its rails -- upside down as on the road (until 2026-10-03 the car was carried along the band's middle, and over a gap at
// the top at the speed it came in with, whatever it was). Off the ring -- too slow over the top, or at a gap's edge -- it flies: inside the
// ring it comes down on it again further round (a hard landing costs most of its speed) or, falling through the middle, onto the road at
// the ring's foot, "fell off"; over a gap it leaps to the far edge, caught there by the ring if no more than RACE_LOOP_CATCH over it, or
// overshoots and falls outside the ring. With the throttle held a whole ring of 4 cells takes 22 km/h at its foot; one with a gap of 35
// degrees at its top 22-43 km/h, of 70 degrees only 21-24 (LoopWindow works it out for each; where the car's top speed would overshoot
// it, the opponents brake to it and the display says it as the car comes up to it). Under the quarter, where nothing throws it off, a car that is too slow rolls back down and out backwards. The opponents go
// round the same way, never falling (OpponentRide).
#define RACE_PI 3.14159265358979
#define RACE_LOOP_G 1600.0
#define RACE_LOOP_PUSH 600.0
#define RACE_LOOP_ENTRY 1100.0     // how far past either lane across the road the ring's foot still takes a car in (units)
#define RACE_LOOP_FALL_HOLD 900
#define RACE_LOOP_CATCH 500.0      // a car leaping a gap no more than this over the far edge's road is caught by it
#define RACE_LOOP_BOTTOM 0.44      // radians either side of the ring's foot: a car falling inside the ring that comes down there has fallen off
#define RACE_LOOP_HARD 2400.0      // a landing faster than this into the ring is a hard one: the car keeps a third of its speed along it
#define RACE_LOOP_STEER 0.35       // the car turned across the band at the most (radians)
#define RACE_LOOP_INSET 640.0      // the car's half width: its middle is kept this far inside the band's rails
#define RACE_LOOP_FUNNEL 8.0       // cells before the ring over which the road's right half closes (the ring's way down stands there)
#define RACE_LOOP_HINT 30.0        // cells before a ring with a gap at which the display says how fast to take it
static T_LOOP_RIDE s_ride;
static S32 s_loopBeta = 0, s_loopTime = 0;
static int s_hinted = -1;

const char *RaceMod_IslandFile(S32 island) {
    return RaceMod_Active() && s_fileName[0] && s_fileIsland == island ? s_fileName : NULL;
}

// The car turned to heading `beta`, pitched `phi` (radians, nose down positive) in its own frame, and yawed `psi` about its own up axis
// (radians, to the left positive), all three at once. The engine turns a body (and an animation's step) by M = M(Alpha) M(Gamma) M(Beta)
// (LIB386/3D/IMATSTDF.CPP): the heading first, then Gamma about the world's Z axis and Alpha about its X axis -- so Alpha alone pitches
// only a car heading along Z, and rolls one heading along X (the old moon's loops, 2026-10-01: the car went round them on its side). The
// rotation wanted is Mb(beta) Ma(phi) Mb(psi); its matrix's elements give the engine's angles: Gamma = asin(-m01), Alpha = atan2(m21, m11),
// Beta = atan2(m02, m00) -- with no yaw, Alpha = atan2(cos b sin p, cos p), Beta = atan2(sin b cos p, cos b), Gamma = asin(-sin b sin p).
static void CarPose(T_OBJ_3D *obj, S32 beta, double phi, double psi) {
    const double b = beta * 2 * RACE_PI / 4096, sb = sin(b), cb = cos(b), sp = sin(phi), cp = cos(phi), sy = sin(psi), cy = cos(psi);
    // Mb(b) Ma(p) = [cb, sb sp, sb cp; 0, cp, -sp; -sb, cb sp, cb cp], times Mb(y) = [cy, 0, sy; 0, 1, 0; -sy, 0, cy]
    const double m00 = cb * cy - sb * cp * sy, m01 = sb * sp, m02 = cb * sy + sb * cp * cy;
    const double m11 = cp, m21 = cb * sp;
    double g = -m01;
    if (g > 1) g = 1; else if (g < -1) g = -1;
    obj->Alpha = (S32)floor(atan2(m21, m11) * 4096 / (2 * RACE_PI) + 0.5) & 4095;
    obj->Beta = (S32)floor(atan2(m02, m00) * 4096 / (2 * RACE_PI) + 0.5) & 4095;
    obj->Gamma = (S32)floor(asin(g) * 4096 / (2 * RACE_PI) + 0.5) & 4095;
}

static void CarPitch(T_OBJ_3D *obj, S32 beta, double phi) { CarPose(obj, beta, phi, 0); }

// The middle of the ring's band `theta` round from its foot, across the road: from Shift/2 left of the road's middle at the foot round to
// Shift/2 right of it (+ is right of the lap's way).
static double Lane(const T_RACE_LOOP *l, double theta) {
    if (theta < 0) theta = 0; else if (theta > 2 * RACE_PI) theta = 2 * RACE_PI;
    return -l->Shift / 2 + l->Shift * theta / (2 * RACE_PI);
}

static int InGap(const T_RACE_LOOP *l, double polar) {
    if (l->Gap <= 0) return 0;
    double p = fmod(polar, 2 * RACE_PI);
    if (p < 0) p += 2 * RACE_PI;
    return p > RACE_PI - l->Gap / 2 && p < RACE_PI + l->Gap / 2;
}

static S32 LoopBeta(const T_RACE_LOOP *l) { return (S32)floor(atan2(l->DirX, l->DirZ) * 4096 / (2 * RACE_PI) + 0.5) & 4095; }

// Where a ride puts the car (cube-local), and how it is pitched.
static void RidePlace(const T_RACE_LOOP *l, const T_LOOP_RIDE *r, double *x, double *y, double *z, double *pitch) {
    if (r->State == LOOP_FALL) { *x = r->X; *y = r->Y; *z = r->Z; *pitch = -r->Pitch; return; }
    double along, up, across;
    if (r->State == LOOP_RIDE) { along = l->R * sin(r->Theta); up = l->R - l->R * cos(r->Theta); across = Lane(l, r->Theta) + r->Lat; *pitch = -r->Theta; }
    else { along = r->A; up = r->U; across = r->C; *pitch = -r->Polar; }
    *x = l->X + l->DirX * along - l->DirZ * across;
    *z = l->Z + l->DirZ * along + l->DirX * across;
    *y = l->Y + up;
}

// Off the ring at `theta`: flying on as it was going, a hair inside the ring's line.
static void LeaveRing(T_LOOP_RIDE *r, const T_RACE_LOOP *l, double theta) {
    const double R = l->R - 1;
    r->A = R * sin(theta); r->U = l->R - R * cos(theta);
    r->VA = r->V * cos(theta); r->VU = r->V * sin(theta);
    r->C = Lane(l, theta) + r->Lat; r->VC = r->V * sin(r->Steer);
    r->Polar = theta; r->Inside = 1;
    r->State = LOOP_AIR;
}

// Back on the ring at `theta`: its speed along it kept, its speed into it lost (a hard landing loses more).
static int Land(T_LOOP_RIDE *r, const T_RACE_LOOP *l, double theta, const char **said) {
    const double along = r->VA * cos(theta) + r->VU * sin(theta), into = r->VA * sin(theta) - r->VU * cos(theta);
    r->Theta = theta; r->V = along; r->Lat = r->C - Lane(l, theta);
    r->State = LOOP_RIDE;
    if (into > RACE_LOOP_HARD) { r->V /= 3; *said = "Hard landing"; }
    return STEP_ON;
}

// Off the ring outside it (over a gap's far edge, or into its back): falling, still turning as it was.
static void ToFall(T_LOOP_RIDE *r, const T_RACE_LOOP *l) {
    double pitch;
    RidePlace(l, r, &r->X, &r->Y, &r->Z, &pitch);
    r->VX = l->DirX * r->VA - l->DirZ * r->VC; r->VZ = l->DirZ * r->VA + l->DirX * r->VC; r->VY = r->VU;
    r->Pitch = r->Polar; r->Spin = (r->VA * cos(r->Polar) + r->VU * sin(r->Polar)) / l->R;
    r->State = LOOP_FALL;
}

// A step of `dt` seconds of a car going round loop `l`. up, down: the throttle and the brakes held; steer: -1 left, 1 right; ai: an
// opponent's (no steering: it keeps to the band; never falls off over the top). STEP_ON while it is still in the loop; *said: a word for
// the display (a landing, a fall).
static int LoopStep(T_LOOP_RIDE *r, const T_RACE_LOOP *l, double dt, int up, int down, int steer, int ai, const char **said) {
    const double R = l->R, g = RACE_LOOP_G;
    *said = NULL;
    if (r->State == LOOP_RIDE) {
        double press = r->V * r->V / R + g * cos(r->Theta);
        if (press < 0) {
            if (!ai) { LeaveRing(r, l, r->Theta); return STEP_ON; }
            r->V = sqrt(-g * R * cos(r->Theta)) + 20;
            press = r->V * r->V / R + g * cos(r->Theta);
        }
        const double grip = press >= g ? 1 : press / g;
        double a = -g * sin(r->Theta);
        if (up && r->V < s_car.GearTop[s_car.Gears - 1]) a += RACE_LOOP_PUSH * grip;
        if (down && r->V > 0) a -= s_car.Brake * grip;
        r->V += a * dt;
        const double before = r->Theta;
        r->Theta += r->V * dt / R;
        if (!ai) {
            // (steered as the car turns on the road, up to RACE_LOOP_STEER across the band; straight again when let go)
            const double rate = s_car.Steer * 2 * RACE_PI / 4096, want = steer * RACE_LOOP_STEER;
            if (r->Steer < want) { r->Steer += rate * dt; if (r->Steer > want) r->Steer = want; }
            else if (r->Steer > want) { r->Steer -= rate * dt; if (r->Steer < want) r->Steer = want; }
            r->Lat += fabs(r->V) * sin(r->Steer) * dt;
        }
        // (the band's rails: a car that came in wide of its lane is pushed onto the band over the first part of the ring)
        double limit = l->Band - RACE_LOOP_INSET;
        if (limit < 0) limit = 0;
        if (fabs(r->Lat) > limit) {
            const double over = fabs(r->Lat) - limit, push = fabs(r->V) * 0.6 * dt + 1;
            r->Lat -= (r->Lat > 0 ? 1 : -1) * (over < push ? over : push);
            if ((r->Lat > 0) == (r->Steer > 0)) r->Steer = 0;
        }
        if (l->Gap > 0) {
            const double t0 = RACE_PI - l->Gap / 2, t1 = RACE_PI + l->Gap / 2;
            if (r->V > 0 && before <= t0 && r->Theta > t0) { r->Theta = t0; LeaveRing(r, l, t0); return STEP_ON; }
            if (r->V < 0 && before >= t1 && r->Theta < t1) { r->Theta = t1; LeaveRing(r, l, t1); return STEP_ON; }
        }
        if (r->Theta >= 2 * RACE_PI) return STEP_ROUND;
        if (r->Theta < 0) return STEP_BACK;
        return STEP_ON;
    }
    if (r->State == LOOP_AIR) {
        r->VU -= g * dt; r->A += r->VA * dt; r->U += r->VU * dt; r->C += r->VC * dt;
        const double rr = sqrt(r->A * r->A + (r->U - R) * (r->U - R));
        double p = atan2(r->A, R - r->U);
        while (p - r->Polar > RACE_PI) p -= 2 * RACE_PI;
        while (p - r->Polar < -RACE_PI) p += 2 * RACE_PI;
        double pn = fmod(p, 2 * RACE_PI);
        if (pn < 0) pn += 2 * RACE_PI;
        const int inside = rr < R, gap = InGap(l, pn);
        if (r->Inside && !inside && !gap) {
            // onto the ring from inside it -- or, through its middle, onto the road at its foot
            if (pn < RACE_LOOP_BOTTOM || pn > 2 * RACE_PI - RACE_LOOP_BOTTOM) {
                double pitch;
                RidePlace(l, r, &r->X, &r->Y, &r->Z, &pitch);
                r->Y = l->Y;
                *said = "Fell off the loop";
                return STEP_FELL;
            }
            return Land(r, l, pn, said);
        }
        if (!r->Inside) {
            // over the gap, outside the ring's line: the far edge catches the car no more than RACE_LOOP_CATCH over it
            const double t0 = RACE_PI - l->Gap / 2, t1 = RACE_PI + l->Gap / 2;
            if ((r->Polar < t1 && p >= t1) || (r->Polar > t0 && p <= t0)) {
                if (rr - R <= RACE_LOOP_CATCH) { Land(r, l, p >= t1 ? t1 : t0, said); if (!*said) *said = "Over the gap"; return STEP_ON; }
                ToFall(r, l);
                *said = "Overshot the loop";
                return STEP_ON;
            }
            if (!gap) { ToFall(r, l); *said = "Overshot the loop"; return STEP_ON; }
        }
        r->Inside = inside; r->Polar = p;
        return STEP_ON;
    }
    // falling outside the ring, onto the road or the ground under it
    r->VY -= g * dt;
    r->X += r->VX * dt; r->Y += r->VY * dt; r->Z += r->VZ * dt;
    r->Pitch += r->Spin * dt;
    if (r->X < 600) r->X = 600;
    if (r->X > 32168) r->X = 32168;
    if (r->Z < 600) r->Z = 600;
    if (r->Z > 32168) r->Z = 32168;
    RaceMod_FloorFor((S32)r->Y);
    const S32 ground = CalculAltitudeObjet((S32)r->X, (S32)r->Z, -1);
    RaceMod_FloorForNothing();
    if (r->Y <= ground) { r->Y = ground; return STEP_FELL; }
    return STEP_ON;
}

// A ride round loop `l` from its foot at `v0`, the throttle held all the way (at the engine's frame of 16 ms): how it ends, how long it
// took, and whether it went cleanly -- on the ring all the way to a gap, and no hard landing (the car still gets round from a little
// slower, off the ring before the gap and down hard on the far side).
static int LoopTry(const T_RACE_LOOP *l, double v0, double *time, int *clean = NULL) {
    T_LOOP_RIDE r;
    memset(&r, 0, sizeof(r));
    r.State = LOOP_RIDE; r.V = v0;
    const char *said;
    *time = 0;
    if (clean) *clean = 1;
    const double t0 = RACE_PI - l->Gap / 2;
    for (int k = 0; k < 2000; k++) {
        const int was = r.State;
        const int out = LoopStep(&r, l, 0.016, 1, 0, 0, 0, &said);
        *time += 0.016;
        // (off the ring anywhere but at the gap's edge, where LoopStep leaves it at exactly that edge)
        if (clean && ((was == LOOP_RIDE && r.State == LOOP_AIR && (l->Gap <= 0 || r.Theta < t0 - 1e-6)) || (said && !strcmp(said, "Hard landing")))) *clean = 0;
        if (out != STEP_ON) return out;
        if (r.State == LOOP_FALL) return STEP_FELL;
    }
    return STEP_FELL;
}

// The speeds at its foot a car gets cleanly round a loop at, the throttle held: a whole ring from Least up, one with a gap between Lo
// and Hi.
static void LoopWindow(T_RACE_LOOP *l) {
    double time;
    l->Lo = l->Hi = l->Least = 0;
    int run = 0, clean;
    double from = 0;
    for (double v = 1000; v <= 9000; v += 20) {
        const int round = LoopTry(l, v, &time, &clean) == STEP_ROUND && clean;
        if (round && !run) { run = 1; from = v; }
        if ((!round || v + 20 > 9000) && run) {
            const double to = round ? v : v - 20;
            if (to - from > l->Hi - l->Lo) { l->Lo = from; l->Hi = to; }
            run = 0;
        }
    }
    l->Least = l->Lo;
    if (l->Gap > 0) Log_Info("[racemod] loop at (%d,%d): a gap of %.0f degrees at its top, taken at %.0f-%.0f km/h", (int)l->X, (int)l->Z, l->Gap * 180 / RACE_PI, l->Lo * 3.6 / 512, l->Hi * 3.6 / 512);
    else Log_Info("[racemod] loop at (%d,%d): whole, taken at %.0f km/h and over", (int)l->X, (int)l->Z, l->Least * 3.6 / 512);
}

// The speed an opponent takes a loop at: within the speeds it gets cleanly round at, well inside them.
static double LoopEntry(const T_RACE_LOOP *l, double v) {
    if (l->Hi <= l->Lo) return v;
    const double w = l->Hi - l->Lo;
    if (l->Gap > 0) { const double lo = l->Lo + 0.2 * w, hi = l->Hi - 0.2 * w; return v < lo ? lo : v > hi ? hi : v; }
    return v < l->Least * 1.05 ? l->Least * 1.05 : v;
}

// The most a car takes a ring with a gap at: well inside the speeds it is cleanly leapt at (faster -- a boost, the super jet-pack -- it
// leapt high over the far edge and fell off outside the ring, 2026-10-04). Twinsen's car is slowed to it over the road before the ring,
// braking no harder than RACE_LOOP_SLOW (units/s^2), as the opponents are (LoopEntry).
#define RACE_LOOP_SLOW 8000.0
static double LoopCap(const T_RACE_LOOP *l) { return l->Hi - 0.2 * (l->Hi - l->Lo); }

// The speed to take a ring with a gap at: the middle of the speeds it is cleanly leapt at.
static double LoopAim(const T_RACE_LOOP *l) { return l->Lo + 0.5 * (l->Hi - l->Lo); }

// Where each loop's foot is on an opponent's line (crossed the lap's way, near the loop's height and within its road), and each carried
// jump's two feet.
static void OpponentLoops(T_RACE_OPPONENT *o) {
    o->LoopOn = -1;
    for (int k = 0; k < RACE_MAX_LOOPS; k++) { o->LoopS[k] = -1; o->LoopAt[k] = -1; }
    for (int k = 0; k < RACE_MAX_ARCS; k++) o->ArcS0[k] = o->ArcS1[k] = -1;
    if (!o->Count) return;
    for (int k = 0; k < s_nbLoops; k++) {
        const T_RACE_LOOP *l = &s_loops[k];
        const double wx = l->CubeX * 32768.0 + l->X, wz = l->CubeZ * 32768.0 + l->Z;
        for (int i = 0; i < o->Count; i++) {
            const int j = (i + 1) % o->Count;
            const double a0 = (o->X[i] - wx) * l->DirX + (o->Z[i] - wz) * l->DirZ, a1 = (o->X[j] - wx) * l->DirX + (o->Z[j] - wz) * l->DirZ;
            if (!(a0 < 0 && a1 >= 0) || fabs(o->Y[i] - l->Y) > 800) continue;
            const double across = -(o->X[i] - wx) * l->DirZ + (o->Z[i] - wz) * l->DirX;
            if (fabs(across) > l->Shift / 2 + l->Band + 1500) continue;
            o->LoopS[k] = o->Cum[i] + (o->Cum[i + 1] - o->Cum[i]) * (-a0) / (a1 - a0);
            o->LoopAt[k] = j;
            break;
        }
    }
    for (int k = 0; k < s_nbArcs; k++) {
        for (int end = 0; end < 2; end++) {
            const T_RACE_RAISED *p = &s_raised[end ? s_arcs[k].LandFoot : s_arcs[k].Foot];
            double bd = 1e30;
            int best = -1;
            for (int i = 0; i < o->Count; i++) {
                const double d = (o->X[i] - p->X) * (double)(o->X[i] - p->X) + (o->Z[i] - p->Z) * (double)(o->Z[i] - p->Z) + (o->Y[i] - p->Y) * (double)(o->Y[i] - p->Y);
                if (d < bd) { bd = d; best = i; }
            }
            if (best >= 0 && bd < 3.0 * 512 * 3 * 512) (end ? o->ArcS1 : o->ArcS0)[k] = o->Cum[best];
        }
    }
}

// The opponents' speeds are planned to take each loop with a gap at the speed it is leapt at, where the car's top speed would overshoot it
// (the test pilot brakes to it too).
static void LoopCaps(const T_RACE_OPPONENT *o, double *v) {
    for (int k = 0; k < s_nbLoops; k++) {
        const T_RACE_LOOP *l = &s_loops[k];
        if (o->LoopAt[k] < 0 || l->Gap <= 0 || l->Hi <= l->Lo || l->Hi >= s_car.GearTop[s_car.Gears - 1]) continue;
        const double want = LoopAim(l);
        for (int d = -1; d <= 1; d++) {
            const int i = (o->LoopAt[k] + d + o->Count) % o->Count;
            if (v[i] > want) v[i] = want;
        }
    }
}

// How long an opponent's laps take round its loops, at the speeds its line comes to them at.
static double LoopsTime(const T_RACE_OPPONENT *o) {
    double all = 0, time;
    for (int k = 0; k < s_nbLoops; k++)
        if (o->LoopAt[k] >= 0 && LoopTry(&s_loops[k], LoopEntry(&s_loops[k], o->V[o->LoopAt[k]]), &time) == STEP_ROUND) all += time;
    return all;
}

static void LoopEnd(T_OBJ_3D *obj, double v, const char *how) {
    obj->Alpha = 0; obj->Gamma = 0;
    obj->Beta = (s_loopBeta - (S32)floor(s_ride.Steer * 4096 / (2 * RACE_PI) + 0.5)) & 4095;
    ListBuggy[0].Speed = (S32)v;
    Log_Info("[racemod] loop %d: %s at %.0f units/s", s_loopOn + 1, how, v);
    s_loopOn = -1; s_loopState = LOOP_NONE;
    s_forgetLap = 1;
}

// ---- a jump the car is carried over (arcjump=, RaceArc) -----------------------------------------------------------------------------
// The Emerald Moon's leap over its reactor (2026-10-02): a ramp curving up to 60 degrees, a flight over the reactor's dish 20,000 up, and
// a hill curving back down, 64 cells from foot to foot. No flight animation of the hero's could fly it: an animation moves him in his
// cube's own units, and this flight crosses into the next cube over the reactor, whose middle is on the cubes' border. The raised road's
// own points from the ramp's foot to the hill's foot are the way -- the deck's heights on the ramp and the hill, the flight's over the gap
// between the lips (Half 0, no floor). A car that drives onto the ramp's foot is carried along it at its speed along the ground (at least
// RACE_ARC_LEAST), as far across the road as it came in, pitched with the way (CarPitch), and set down on the hill's foot going as fast.
// Where the way crosses into another cube, the scene is changed here (cube_scene= names each cube's) and the car carried on in the new
// one. The camera meanwhile flies beside the car (ArcCamera).
// The place `s` along an arc's way (world units along the ground from its foot), `across` to the side of its middle (the builder's Across:
// the way turned a quarter, (-dz, dx)), and the way's heading and slope there.
static void ArcPlace(const T_RACE_ARC *a, double s, double across, double *x, double *y, double *z, double *dx, double *dz, double *slope) {
    const int n = a->LandFoot - a->Foot + 1;
    int k = 0;
    while (k < n - 2 && a->S[k + 1] < s) k++;
    const T_RACE_RAISED *p = &s_raised[a->Foot + k], *q = p + 1;
    const double len = a->S[k + 1] - a->S[k];
    double t = len > 0 ? (s - a->S[k]) / len : 0;
    if (t < 0) t = 0; else if (t > 1) t = 1;
    s_arcSeg = a->Foot + k; s_arcT = t;
    double ux = q->X - p->X, uz = q->Z - p->Z;
    const double ul = sqrt(ux * ux + uz * uz);
    if (ul > 0) { ux /= ul; uz /= ul; } else { ux = 1; uz = 0; }
    *x = p->X + (q->X - p->X) * t - uz * across;
    *z = p->Z + (q->Z - p->Z) * t + ux * across;
    *y = p->Y + (q->Y - p->Y) * t;
    *dx = ux; *dz = uz;
    *slope = len > 0 ? (q->Y - p->Y) / len : 0;
}

// A carried jump just taken, `across` its way: where across the landing the car comes down (the narrower of the lip -- a lane narrowing
// into the roulette wheel's hole -- and the landing), how fast it is carried, from when.
static void ArcTaken(const T_RACE_ARC *a, double across) {
    const double room = (s_raised[a->LandFoot].Half < s_raised[a->Lip].Half ? s_raised[a->LandFoot].Half : s_raised[a->Lip].Half) - RAISED_INSET;
    s_arcAcross1 = room <= 0 ? 0 : across > room ? room : across < -room ? -room : across;
    s_arcV = ListBuggy[0].Speed > RACE_ARC_LEAST ? ListBuggy[0].Speed : RACE_ARC_LEAST;
    s_arcTime = TimerRefHR;
}

// For RaceMod_Loop: non-zero when a carried jump carries the car this frame (its new place, cube-local, in *nx..*nz).
static S32 RaceArc(S32 ox, S32 oy, S32 oz, S32 *nx, S32 *ny, S32 *nz) {
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    T_OBJ_3D *obj = &hero->Obj;
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    if (s_arcOn < 0) {
        if (!s_nbArcs || ListBuggy[0].Speed <= 0 || (hero->WorkFlags & ANIM_MASTER_GRAVITY)) return 0;
        for (int i = 0; i < s_nbArcs; i++) {
            const T_RACE_ARC *a = &s_arcs[i];
            const T_RACE_RAISED *f = &s_raised[a->Foot], *g = f + 1;
            double ux = g->X - f->X, uz = g->Z - f->Z;
            const double ul = sqrt(ux * ux + uz * uz);
            if (ul <= 0 || fabs(oy - f->Y) > RACE_ARC_ENTRY) continue;
            ux /= ul; uz /= ul;
            // (the line across the road at the ramp's foot, crossed the lap's way)
            const double a0 = (ox + cx - f->X) * ux + (oz + cz - f->Z) * uz, a1 = (*nx + cx - f->X) * ux + (*nz + cz - f->Z) * uz;
            if (!(a0 < 0 && a1 >= 0)) continue;
            const double t = a0 / (a0 - a1);
            const double px = ox + cx + (*nx - ox) * t, pz = oz + cz + (*nz - oz) * t;
            const double across = -(px - f->X) * uz + (pz - f->Z) * ux;
            if (fabs(across) > f->Half) continue;
            s_arcOn = i; s_arcLast = i; s_arcS = 0; s_arcAcross = across;
            ArcTaken(a, across);
            Log_Info("[racemod] carried jump %d: on at %d units/s (carried at %.0f), %.0f across", i + 1, (int)ListBuggy[0].Speed, s_arcV, across);
            break;
        }
        // (or a car on the ramp, past its foot, going up it: one that crossed the foot stopped -- held by a fake mushroom's blast, it slid
        // on over the line at no speed and was never taken; it drove up the ramp and off its lip, 2026-10-10 -- carried on from where it is)
        for (int i = 0; i < s_nbArcs && s_arcOn < 0; i++) {
            const T_RACE_ARC *a = &s_arcs[i];
            for (int k = a->Foot; k < a->Lip; k++) {
                const T_RACE_RAISED *p = &s_raised[k], *q = p + 1;
                double ux = q->X - p->X, uz = q->Z - p->Z;
                const double ul = sqrt(ux * ux + uz * uz);
                if (ul <= 0) continue;
                ux /= ul; uz /= ul;
                const double px = *nx + cx - p->X, pz = *nz + cz - p->Z;
                const double along = px * ux + pz * uz, across = -px * uz + pz * ux;
                if (along < 0 || along > ul || fabs(across) > p->Half) continue;
                const double y = p->Y + (q->Y - p->Y) * along / ul;
                const double moved = (*nx - ox) * ux + (*nz - oz) * uz;
                if (fabs(oy - y) > RACE_ARC_ENTRY || moved <= 0) continue;
                s_arcOn = i; s_arcLast = i; s_arcS = a->S[k - a->Foot] + along; s_arcAcross = across;
                ArcTaken(a, across);
                Log_Info("[racemod] carried jump %d: on, up its ramp already (%.0f along), at %d units/s (carried at %.0f), %.0f across", i + 1, s_arcS,
                         (int)ListBuggy[0].Speed, s_arcV, across);
                break;
            }
        }
        if (s_arcOn < 0) return 0;
    }
    const T_RACE_ARC *a = &s_arcs[s_arcOn];
    double dt = (TimerRefHR - s_arcTime) / 1000.0;
    s_arcTime = TimerRefHR;
    if (dt < 0) dt = 0;
    if (dt > 0.1) dt = 0.1;
    s_arcS += s_arcV * dt;
    const double end = a->S[a->LandFoot - a->Foot];
    const int done = s_arcS >= end;
    double x, y, z, dx, dz, slope;
    // (eased over the flight from where it was across the ramp to where the landing's rails let it be)
    double f = end > 0 ? (done ? end : s_arcS) / end : 1;
    f = f < 0 ? 0 : f > 1 ? 1 : f * f * (3 - 2 * f);
    ArcPlace(a, done ? end : s_arcS, s_arcAcross + (s_arcAcross1 - s_arcAcross) * f, &x, &y, &z, &dx, &dz, &slope);
    const S32 beta = (S32)floor(atan2(dx, dz) * 4096 / (2 * RACE_PI) + 0.5) & 4095;
    ListBuggy[0].Speed = (S32)s_arcV;
    if (done) {
        obj->Alpha = 0; obj->Gamma = 0; obj->Beta = beta;
        Log_Info("[racemod] carried jump %d: down at (%.0f,%.0f,%.0f)", s_arcOn + 1, x, y, z);
        s_arcOn = -1;
        s_arcLandedAt = TimerRefHR;
        s_forgetLap = 1;
    } else
        CarPitch(obj, beta, -atan(slope));
    // (into another cube: its scene, the car carried on in it; this frame at the old cube's edge)
    const int qx = (int)floor(x / 32768.0), qz = (int)floor(z / 32768.0);
    if (qx != CurrentCubeX || qz != CurrentCubeY) {
        int scene = -1;
        for (int c = 0; c < s_nbCubeScenes; c++)
            if (s_cubeScenes[c][0] == qx && s_cubeScenes[c][1] == qz) scene = s_cubeScenes[c][2];
        if (scene >= 0 && NewCube == -1) {
            NewCube = scene;
            NewPosX = (S32)floor(x - qx * 32768.0 + 0.5); NewPosY = (S32)floor(y + 0.5); NewPosZ = (S32)floor(z - qz * 32768.0 + 0.5);
            FlagChgCube = 1;
            FlagReajustPosTwinsen = FALSE;
            Log_Info("[racemod] carried jump: into cube (%d,%d), scene %d, at (%d,%d,%d)", qx, qz, scene, (int)NewPosX, (int)NewPosY, (int)NewPosZ);
        } else if (scene < 0)
            Log_Info("[racemod] carried jump: no scene for cube (%d,%d)", qx, qz);
        x = x - cx < 1 ? cx + 1 : x - cx > 32767 ? cx + 32767 : x;
        z = z - cz < 1 ? cz + 1 : z - cz > 32767 ? cz + 32767 : z;
    }
    *nx = (S32)floor(x - cx + 0.5); *ny = (S32)floor(y + 0.5); *nz = (S32)floor(z - cz + 0.5);
    return 1;
}

// The camera while the car is carried over a jump: an arm behind it would swing up and down with the way and look at the ramp's face,
// so the eye flies beside the car, RACE_ARC_CAM_FAR cells out to the jump's side (arcjump='s fifth number: the side the builder's Across
// points to, or -1 the other) and a little behind and over it, looking at it -- the reactor passing under the car between them.
static S32 ArcCamera(S32 *eye, S32 *target) {
    const int on = s_arcOn >= 0 ? s_arcOn : s_arcLast >= 0 && (S32)(TimerRefHR - s_arcLandedAt) < RACE_ARC_CAM_HOLD ? s_arcLast : -1;
    if (on < 0) return FALSE;
    const T_RACE_ARC *a = &s_arcs[on];
    if (a->Side == 0) return FALSE;      // (behind the car: the follow camera's own)
    const T_OBJ_3D *car = &ListObjet[NUM_PERSO].Obj;
    const T_RACE_RAISED *f = &s_raised[a->Foot], *g = &s_raised[a->LandFoot];
    double ux = g->X - f->X, uz = g->Z - f->Z;
    const double ul = sqrt(ux * ux + uz * uz);
    if (ul <= 0) return FALSE;
    ux /= ul; uz /= ul;
    const double side = a->Side < 0 ? -1 : 1, far = RACE_ARC_CAM_FAR * 512, back = RACE_ARC_CAM_BACK * 512;
    eye[0] = (S32)(car->X - uz * far * side - ux * back);
    eye[1] = (S32)(car->Y + RACE_ARC_CAM_UP);
    eye[2] = (S32)(car->Z + ux * far * side - uz * back);
    target[0] = car->X; target[1] = car->Y + 300; target[2] = car->Z;
    return TRUE;
}

// For DoAnimExt (EXTFUNC.CPP), on the hero, after his car's move from (ox,oy,oz) towards (*nx,*ny,*nz): non-zero when a loop carries
// the car this frame -- the new place is in *nx..*nz, and nothing else is to move it (no ground, no collisions: the ring has none).
// Also stands the car level again whenever neither a loop nor a flight's master rotation pitches it (a flight cut short left it so).
S32 RaceMod_Loop(S32 ox, S32 oy, S32 oz, S32 *nx, S32 *ny, S32 *nz) {
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    T_OBJ_3D *obj = &hero->Obj;
    if (!RaceMod_Active() || Comportement != C_BUGGY || CubeMode != CUBE_EXTERIEUR) { s_loopOn = -1; s_loopState = LOOP_NONE; s_arcOn = -1; return 0; }
    if (s_loopState == LOOP_NONE && RaceArc(ox, oy, oz, nx, ny, nz)) return 1;
    if (s_loopState == LOOP_NONE && s_arcOn < 0 && WheelMove(ox, oy, oz, nx, ny, nz)) return 1;
    if (s_loopState == LOOP_NONE) {
        if (!(obj->Master & 1) && (obj->Alpha || obj->Gamma)) { obj->Alpha = 0; obj->Gamma = 0; }
        if (!s_nbLoops || (hero->WorkFlags & (FALLING | ANIM_MASTER_GRAVITY))) return 0;
        for (int i = 0; i < s_nbLoops; i++) {
            const T_RACE_LOOP *l = &s_loops[i];
            if (l->CubeX != CurrentCubeX || l->CubeZ != CurrentCubeY || fabs(oy - l->Y) > 600) continue;
            const double a1 = (*nx - l->X) * l->DirX + (*nz - l->Z) * l->DirZ;
            const double c1 = -(*nx - l->X) * l->DirZ + (*nz - l->Z) * l->DirX;
            if (fabs(c1) > l->Shift / 2 + l->Band + 1024) continue;            // (another road, beside this one)
            // (coming up to a ring with a gap: the least to take it at -- too fast, the car is slowed, below)
            if (l->Gap > 0 && l->Hi > l->Lo && a1 > -RACE_LOOP_HINT * 512 && a1 < -RACE_LOOP_HINT * 256 && s_hinted != i) {
                char text[64];
                snprintf(text, sizeof(text), "Jump loop: over %.0f km/h", l->Lo * 3.6 / 512 + 0.5);
                Message(text);
                s_hinted = i;
            }
            // (and faster than its gap is cleanly leapt at: slowed to that by the ring's foot)
            if (l->Gap > 0 && l->Hi > l->Lo && a1 > -RACE_LOOP_HINT * 512 && a1 < 0) {
                const double cap = LoopCap(l), allowed = sqrt(cap * cap + 2.0 * RACE_LOOP_SLOW * -a1);
                if (ListBuggy[0].Speed > allowed) {
                    static int said = -1;
                    if (said != i) { said = i; Log_Info("[racemod] loop %d: slowed from %d to the %d it is leapt at", i + 1, (int)ListBuggy[0].Speed, (int)cap); }
                    ListBuggy[0].Speed = (S32)allowed;
                }
            }
            // (the road's right half closes before the ring: its way down comes down onto the road there)
            const double funnel = l->R + RACE_LOOP_FUNNEL * 512;
            if (a1 > -funnel && a1 < 0) {
                const double t = (a1 + funnel) / (funnel - l->R) > 1 ? 1 : (a1 + funnel) / (funnel - l->R);
                const double right = (l->Shift / 2 + l->Band) * (1 - t) + (-l->Shift / 2 + l->Band - RACE_LOOP_INSET) * t;
                if (c1 > right) { *nx = (S32)floor(*nx + l->DirZ * (c1 - right) + 0.5); *nz = (S32)floor(*nz - l->DirX * (c1 - right) + 0.5); s_railTouch = 1; }
            }
            if (ListBuggy[0].Speed <= 0) continue;
            // (the line across the road at the ring's foot, crossed the lap's way)
            const double a0 = (ox - l->X) * l->DirX + (oz - l->Z) * l->DirZ;
            const double a2 = (*nx - l->X) * l->DirX + (*nz - l->Z) * l->DirZ;
            if (!(a0 < 0 && a2 >= 0)) continue;
            const double t = a0 / (a0 - a2);
            const double cx = ox + (*nx - ox) * t, cz = oz + (*nz - oz) * t;
            const double across = -(cx - l->X) * l->DirZ + (cz - l->Z) * l->DirX;
            if (fabs(across) > l->Shift / 2 + RACE_LOOP_ENTRY) continue;
            s_loopOn = i; s_loopState = LOOP_RIDE;
            memset(&s_ride, 0, sizeof(s_ride));
            s_ride.State = LOOP_RIDE; s_ride.V = ListBuggy[0].Speed; s_ride.Lat = across - Lane(l, 0);
            if (l->Gap > 0 && l->Hi > l->Lo && s_ride.V > LoopCap(l)) s_ride.V = LoopCap(l);
            s_loopBeta = LoopBeta(l);
            s_loopTime = TimerRefHR;
            s_hinted = -1;
            Log_Info("[racemod] loop %d: in at %d units/s (%.0f km/h), %.0f across its band", i + 1, (int)ListBuggy[0].Speed, ListBuggy[0].Speed * 3.6 / 512, s_ride.Lat);
            break;
        }
        if (s_loopState == LOOP_NONE) return 0;
    }
    const T_RACE_LOOP *l = &s_loops[s_loopOn];
    double dt = (TimerRefHR - s_loopTime) / 1000.0;
    s_loopTime = TimerRefHR;
    if (dt < 0) dt = 0;
    if (dt > 0.1) dt = 0.1;
    const int held = RaceMod_Held();
    const int steer = held ? 0 : ((Input & I_RIGHT) ? 1 : 0) - ((Input & I_LEFT) ? 1 : 0);
    const int was = s_ride.State;
    const char *said;
    const int out = LoopStep(&s_ride, l, dt, !held && (Input & I_UP), !held && (Input & I_DOWN), steer, 0, &said);
    s_loopState = s_ride.State;
    if (said) { Message(said); Log_Info("[racemod] loop %d: %s", s_loopOn + 1, said); }
    if (was == LOOP_RIDE && s_ride.State == LOOP_AIR)
        Log_Info("[racemod] loop %d: off the ring at %.0f degrees, %.0f units/s", s_loopOn + 1, s_ride.Theta * 180 / RACE_PI, s_ride.V);
    double x, y, z, pitch;
    if (out == STEP_ROUND) {
        s_ride.Theta = 2 * RACE_PI;
        RidePlace(l, &s_ride, &x, &y, &z, &pitch);
        y = l->Y;
        LoopEnd(obj, s_ride.V, "round");
    } else if (out == STEP_BACK) {
        s_ride.Theta = 0;
        RidePlace(l, &s_ride, &x, &y, &z, &pitch);
        y = l->Y;
        LoopEnd(obj, s_ride.V, "rolled back out");
    } else if (out == STEP_FELL) {
        x = s_ride.X; y = s_ride.Y; z = s_ride.Z;
        s_hitUntil = TimerRefHR + RACE_LOOP_FALL_HOLD;
        s_ride.Steer = 0;
        LoopEnd(obj, 0, "fell off, landed");
    } else {
        RidePlace(l, &s_ride, &x, &y, &z, &pitch);
        CarPose(obj, s_loopBeta, pitch, s_ride.State == LOOP_RIDE ? -s_ride.Steer : 0);
        if (s_ride.State == LOOP_RIDE) ListBuggy[0].Speed = (S32)s_ride.V;
    }
    *nx = (S32)floor(x + 0.5); *ny = (S32)floor(y + 0.5); *nz = (S32)floor(z + 0.5);
    return 1;
}

// The camera while the car goes round a loop: an arm behind the car would look into the ring's own back -- the part of it coming down
// behind its foot, between the arm's eye and the car -- so the eye stands out to the ring's side, RACE_LOOP_CAM_FAR radii from its
// middle on the side the car goes in on and a little over it, looking at the ring's middle drawn a little towards the car.
#define RACE_LOOP_CAM_FAR 3.2
static S32 LoopCamera(S32 *eye, S32 *target) {
    if (s_loopOn < 0 || s_loopState == LOOP_NONE) return FALSE;
    const T_RACE_LOOP *l = &s_loops[s_loopOn];
    const T_OBJ_3D *car = &ListObjet[NUM_PERSO].Obj;
    const double nx = -l->DirZ, nz = l->DirX;
    const double mx = l->X, my = l->Y + l->R, mz = l->Z;
    const double far = l->R * RACE_LOOP_CAM_FAR + l->Shift / 2;
    eye[0] = (S32)(mx - nx * far); eye[1] = (S32)(l->Y + l->R * 1.2); eye[2] = (S32)(mz - nz * far);
    target[0] = (S32)(mx + (car->X - mx) * 0.3); target[1] = (S32)(my + (car->Y - my) * 0.3); target[2] = (S32)(mz + (car->Z - mz) * 0.3);
    return TRUE;
}

static S32 JumpCamera(S32 *eye, S32 *target) {
    if (!s_nbJumpCams) return FALSE;
    const S32 anim = s_flying >= 0 ? s_flying : (S32)(TimerRefHR - s_landedAt) < RACE_JUMPCAM_HOLD ? s_landed : -1;
    if (anim < 0) return FALSE;
    for (int i = 0; i < s_nbJumpCams; i++) {
        const T_RACE_JUMPCAM *c = &s_jumpCams[i];
        if (c->Anim != anim || c->CubeX != CurrentCubeX || c->CubeZ != CurrentCubeY) continue;
        const T_OBJ_3D *car = &ListObjet[NUM_PERSO].Obj;
        eye[0] = c->X; eye[1] = c->Y; eye[2] = c->Z;
        target[0] = car->X; target[1] = car->Y + 300; target[2] = car->Z;
        return TRUE;
    }
    return FALSE;
}

// In a tunnel (InTunnel): the rail camera's eye -- behind the car along the road, looking at it -- low enough to be under the roof.
static S32 TunnelCamera(S32 *eye, S32 *target) {
    if (!s_nbTunnels || !s_nbRaised || CubeMode != CUBE_EXTERIEUR || Comportement != C_BUGGY) return FALSE;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    S32 f;
    int seg;
    double lat, t;
    if (!RaisedAt(hero->Obj.X + CurrentCubeX * 32768.0, hero->Obj.Z + CurrentCubeY * 32768.0, hero->Obj.Y + 64, RAISED_PAD, &f, &seg, &lat, &t) || abs(f - hero->Obj.Y) > 300) return FALSE;
    const int k = InTunnel(seg);
    if (k < 0) return FALSE;
    const double back = s_railBack, upKept = s_railUp, lead = s_railLead;
    s_railBack = RACE_TUNNEL_BACK;
    s_railUp = s_tunnels[k].Roof - RACE_TUNNEL_EYE < RACE_TUNNEL_UP ? s_tunnels[k].Roof - RACE_TUNNEL_EYE : RACE_TUNNEL_UP;
    s_railLead = RACE_TUNNEL_AHEAD;
    s_railSight = RACE_TUNNEL_SIGHT; s_railCeil = s_tunnels[k].Roof - RACE_TUNNEL_UNDER;
    const S32 done = RailCamera(eye, target);
    s_railBack = back; s_railUp = upKept; s_railLead = lead; s_railSight = 0; s_railCeil = -1;
    return done;
}

// The camera at the roulette wheel (roulette=). In its bowl: behind the car and over the bowl's floor there (the camera that follows the car
// knows nothing of the bowl, and went into it). Dropping through the hole: the eye kept where it was, looking at the car going down. On the
// road under the wheel, out from its hole: the camera that follows the car lifts its eye over the road it is on, and the wheel's way over
// it is that stretch -- the eye went up over the bowl and the car was hidden under it -- so behind the car and low, under the bowl,
// RACE_WHEEL_CAM_BACK behind it (past the near plane, CLIP_NEAR 3,000: an eye nearer than that and the car isn't drawn) until the eye itself
// is out from under it.
#define RACE_WHEEL_CAM_BACK 4200.0
#define RACE_WHEEL_CAM_UP 2000.0           // (was 1,000, looking at a point 900 ahead of the car: the car hid the road -- the user, 2026-10-10:
#define RACE_WHEEL_CAM_AHEAD 4000.0        // "When Twinsen drops out of the roulette wheel it's very hard for him to see where he's going")
#define RACE_WHEEL_BOWL_BACK 4600.0
#define RACE_WHEEL_BOWL_UP 2200.0
#define RACE_WHEEL_BOWL_OVER 1500.0
static S32 WheelCamera(S32 *eye, S32 *target) {
    if (!s_wheel.On || CubeMode != CUBE_EXTERIEUR || Comportement != C_BUGGY) return FALSE;
    const T_OBJ_3D *car = &ListObjet[NUM_PERSO].Obj;
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0, hx = car->X + cx, hz = car->Z + cz;
    const double a = (car->Beta & 4095) * 2 * RACE_PI / 4096.0, fx = sin(a), fz = cos(a);
    if (s_wheel.DropOn) {
        eye[0] = s_wheel.Eye[0]; eye[1] = s_wheel.Eye[1]; eye[2] = s_wheel.Eye[2];
        target[0] = car->X; target[1] = car->Y + 300; target[2] = car->Z;
        return TRUE;
    }
    if (s_arcOn < 0 && OnWheel(hx, car->Y, hz)) {
        const double ex = hx - fx * RACE_WHEEL_BOWL_BACK, ez = hz - fz * RACE_WHEEL_BOWL_BACK, re = WheelR(ex, ez);
        const double floorE = WheelFloorAt(re < s_wheel.Rim ? re : s_wheel.Rim) + (re < s_wheel.Rim ? 0 : 600);
        double ey = car->Y + RACE_WHEEL_BOWL_UP;
        if (ey < floorE + RACE_WHEEL_BOWL_OVER) ey = floorE + RACE_WHEEL_BOWL_OVER;
        eye[0] = (S32)(ex - cx); eye[1] = (S32)ey; eye[2] = (S32)(ez - cz);
        target[0] = (S32)(car->X + fx * 900); target[1] = car->Y + 400; target[2] = (S32)(car->Z + fz * 900);
        s_wheel.Eye[0] = eye[0]; s_wheel.Eye[1] = eye[1]; s_wheel.Eye[2] = eye[2];
        return TRUE;
    }
    const double under = WheelUnderAt(s_wheel.Hole);
    if (s_arcOn >= 0 || car->Y > under - 600) return FALSE;
    const double ex = hx - fx * RACE_WHEEL_CAM_BACK, ez = hz - fz * RACE_WHEEL_CAM_BACK;
    const double r = WheelR(ex, ez);
    if (r > s_wheel.Outer + 600 && WheelR(hx, hz) > s_wheel.Outer) return FALSE;
    double ey = car->Y + RACE_WHEEL_CAM_UP;
    if (r < s_wheel.Outer + 600) { const double ceiling = WheelUnderAt(r) - 250; if (ey > ceiling) ey = ceiling; }
    eye[0] = (S32)(ex - cx); eye[1] = (S32)ey; eye[2] = (S32)(ez - cz);
    // (looking down the road over the car, at a place well ahead of it on the road)
    target[0] = (S32)(car->X + fx * RACE_WHEEL_CAM_AHEAD); target[1] = car->Y + 200; target[2] = (S32)(car->Z + fz * RACE_WHEEL_CAM_AHEAD);
    return TRUE;
}

static S32 RaceCameraEye(S32 *eye, S32 *target) { return ArcCamera(eye, target) || JumpCamera(eye, target) || LoopCamera(eye, target) || TunnelCamera(eye, target) || WheelCamera(eye, target) || RailCamera(eye, target); }

S32 RaceMod_Rescue(void) {
    if (!RaceMod_Active() || !s_rescueOn || Comportement != C_BUGGY || !Guide() || s_safeIndex < 0) return 0;
    s_rescue = 1;
    return 1;
}

// The car put on the line at point i, facing the way the line runs, stopped.
static void RescuePlace(const T_RACE_OPPONENT *o, int i) {
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    const int j = s_sprint ? (i + 1 < o->Count ? i + 1 : i - 1) : (i + 1) % o->Count;
    hero->Obj.Beta = (S32)(atan2((double)(o->X[j] - o->X[i]), (double)(o->Z[j] - o->Z[i])) * 4096.0 / (2 * 3.14159265358979)) & 4095;
    ClearRealAngle(hero);
    hero->WorkFlags &= ~FALLING;
    hero->CodeJeu = 0;
    ListBuggy[0].Speed = 0;
    s_forgetLap = 1;
    s_safeIndex = i;
    Message("Back on the road");
}

// A rescue into another scene (Rescue): once it is in -- the car at the scene's own start (asked for from RaceMod_Frame, inside AffScene,
// the scene change's place is lost: the frame's end clears FlagChgCube before the next frame changes the scene) -- the car is put at
// the place on the line, facing the way it runs, stopped.
static void RescueArrive(void) {
    const T_RACE_OPPONENT *o = Guide();
    if (s_rescueArrive < 0 || NewCube != -1 || (S32)NumCube != s_safeScene) return;
    const int i = s_rescueArrive;
    s_rescueArrive = -1;
    if (!o || i >= o->Count || !LineOnGround(o, i)) return;
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    const S32 lx = o->X[i] - CurrentCubeX * 32768, lz = o->Z[i] - CurrentCubeY * 32768;
    hero->Obj.X = lx;
    hero->Obj.Z = lz;
    hero->Obj.Y = s_nbRaised ? o->Y[i] : CalculAltitudeTerrain(lx, lz, 0);
    RescuePlace(o, i);
    Log_Info("[racemod] back on the road in scene %d: line point %d (%d,%d,%d)", s_safeScene, i, (int)hero->Obj.X, (int)hero->Obj.Y, (int)hero->Obj.Z);
}

static void Rescue(void) {
    const T_RACE_OPPONENT *o = Guide();
    if (!o) return;
    // (out of the scene the car was last safe in -- over the island's edge, where the engine has its sea of no scene, the phantom cube:
    // under the super jet-pack, off the end of a road that turned, 2026-10-06 -- back into it, at that place)
    if (s_safeScene >= 0 && (S32)NumCube != s_safeScene) {
        if (NewCube != -1 || s_rescueArrive >= 0 || s_safeIndex < 0 || s_safeIndex >= o->Count) return;
        // (the game clock's bracket opened here: ChangeCube opens it only on FlagChgCube -- which the frame's end clears before a change
        // asked for from here is made -- but always closes it, and without this its RestoreTimer closed the one a loaded game leaves
        // open, and the clock went back to the load. The user's race, 2026-10-09, played from Play's save: Twinsen off the roulette
        // wheel's south rim into the next cube, rescued back, the clock 153 s back -- his car held stopped by a stun now 153 s off, and
        // Baldino lapping every few frames, his oil skid's speed run away)
        SaveTimer();
        NewCube = s_safeScene;
        s_rescueArrive = s_safeIndex;
        Log_Info("[racemod] off the road out of scene %d (in %d): back to it", s_safeScene, (int)NumCube);
        return;
    }
    int i = s_sprint ? (s_safeIndex > RACE_RESCUE_BACK ? s_safeIndex - RACE_RESCUE_BACK : 0) : ((s_safeIndex - RACE_RESCUE_BACK) % o->Count + o->Count) % o->Count;
    for (int k = 0; k < 40 && !LineOnGround(o, i); k++) i = s_sprint ? (i > 0 ? i - 1 : 0) : (i - 1 + o->Count) % o->Count;
    if (!LineOnGround(o, i)) return;
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    const S32 lx = o->X[i] - CurrentCubeX * 32768, lz = o->Z[i] - CurrentCubeY * 32768;
    hero->Obj.X = lx;
    hero->Obj.Z = lz;
    hero->Obj.Y = s_nbRaised ? o->Y[i] : CalculAltitudeTerrain(lx, lz, 0);
    RescuePlace(o, i);
    Log_Info("[racemod] back on the road: line point %d (%d,%d,%d)", i, (int)hero->Obj.X, (int)hero->Obj.Y, (int)hero->Obj.Z);
}

// A car fallen off a raised road onto what is under it or beside it -- the ground, a roof -- with the lap high over it: off the lap, and
// no way back up. Once it has stood there RACE_BELOW_MS it is put back on the road (Rescue), as from the sea. (The user, 2026-10-07:
// Twinsen through the track on the Island of the Francos, off the high road where it turns by the refinery, 10,800 below it on the
// ground inside the bend, stuck there for the rest of the race -- the rescue was only for lava and the sea.) Off the lap: no point of
// the line (Guide) within RACE_BELOW_LAP of him at about his height; the lap high over him: the line's nearest point within
// RACE_BELOW_REACH at least RACE_BELOW_DROP above him. (The line, not the raised road: where the lap runs on the ground beside a raised
// part -- the Desert island's -- the ground is the lap.) Not in a jump's flight, a loop, the super jet-pack's flight or a fall still going on.
#define RACE_BELOW_MS 1500
#define RACE_BELOW_DROP 1500
#define RACE_BELOW_LAP 4500.0
#define RACE_BELOW_REACH (16 * 512.0)
static S32 s_belowSince = 0;
static void FallenBelow(S32 now) {
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    const T_RACE_OPPONENT *o = Guide();
    if (!s_nbRaised || !s_rescueOn || !o || Comportement != C_BUGGY || CubeMode != CUBE_EXTERIEUR || s_flying >= 0 || s_arcOn >= 0 || s_loopOn >= 0
            || now < s_jetUntil || s_rescue || s_rescueArrive >= 0 || (hero->WorkFlags & FALLING) || s_safeIndex < 0
            || s_wheel.DropOn || OnWheel(hero->Obj.X + CurrentCubeX * 32768.0, hero->Obj.Y, hero->Obj.Z + CurrentCubeY * 32768.0)) { s_belowSince = 0; return; }
    const double wx = hero->Obj.X + CurrentCubeX * 32768.0, wz = hero->Obj.Z + CurrentCubeY * 32768.0;
    double near2 = 1e30;
    S32 nearY = 0;
    for (int i = 0; i < o->Count; i++) {
        const double dx = o->X[i] - wx, dz = o->Z[i] - wz, d2 = dx * dx + dz * dz;
        if (d2 < RACE_BELOW_LAP * RACE_BELOW_LAP && abs(o->Y[i] - hero->Obj.Y) < 900) { s_belowSince = 0; return; }   // (on the lap)
        if (d2 < near2) { near2 = d2; nearY = o->Y[i]; }
    }
    if (near2 > RACE_BELOW_REACH * RACE_BELOW_REACH || nearY < hero->Obj.Y + RACE_BELOW_DROP) { s_belowSince = 0; return; }
    if (!s_belowSince) { s_belowSince = now; return; }
    if (now - s_belowSince < RACE_BELOW_MS) return;
    s_belowSince = 0;
    Log_Info("[racemod] fallen off the road to (%.0f,%d,%.0f), %d below the lap %.0f away: put back on it", wx, (int)hero->Obj.Y, wz,
             (int)(nearY - hero->Obj.Y), sqrt(near2));
    s_rescue = 1;
}

// ---- power-ups ---------------------------------------------------------------------------------------------------------------
// A mushroom of this scene: its actor (-1 not here) -- the scene's copy, standing where it does (its height kept the first time it is
// seen, as it is moved out of sight while gone).
static int MushroomActor(T_RACE_MUSHROOM *m) {
    if (m->Scene != NumCube || m->Actor <= 0 || m->Actor >= NbObjets) return -1;
    if (!m->HasY && ListObjet[m->Actor].Obj.Y > -10000) { m->Y = ListObjet[m->Actor].Obj.Y; m->HasY = 1; }
    if (s_fakeIconBody < 0 && ListObjet[m->Actor].Obj.Body.Num >= 0) s_fakeIconBody = ListObjet[m->Actor].Obj.Body.Num;
    return m->HasY ? m->Actor : -1;
}

static int Near(const T_OBJET *a, S32 x, S32 y, S32 z, S32 reach) {
    const double dx = a->Obj.X - x, dz = a->Obj.Z - z;
    return dx * dx + dz * dz < (double)reach * reach && abs(a->Obj.Y - y) < 900;
}

// Whether a race is running: after the grid's GO, alone or against others (not qualifying, nor waiting).
static int RaceRunning(void) {
    return s_phase == PHASE_RACE && (s_nbSpots ? s_goAt && TimerRefHR >= s_goAt : 1);
}

// Whether a race is on: other cars on the lap, after the grid's GO (not qualifying, nor the others waiting in the pits).
static int RaceOn(void) {
    return Opponents() && s_opp[0].Count && s_phase == PHASE_RACE && (s_nbSpots ? s_goAt && TimerRefHR >= s_goAt : s_oppStarted);
}

// The player's place in the race as it stands (1 leading) and how many race (a time to beat has no car: not one of them); 0 with no race.
static int RacePlace(int *racers) {
    if (!RaceOn()) return 0;
    const double mine = PlayerProgress();
    int ahead = 0, count = 1;
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
        if (!s_opp[n].Count || !s_opp[n].Scenes) continue;
        count++;
        if (OpponentProgress(&s_opp[n]) > mine) ahead++;
    }
    *racers = count;
    return count > 1 ? ahead + 1 : 0;
}

// How far back in the race a car is: 0 leading, 1 last; -1 with no race (no other car on the lap, or not racing yet: qualifying, the
// others waiting in the pits). `me`: -1 the player, else an opponent.
static double BackShare(int me) {
    if (!RaceOn()) return -1;
    const double player = PlayerProgress();
    const double mine = me < 0 ? player : OpponentProgress(&s_opp[me]);
    int ahead = 0, racers = 1;
    if (me >= 0) { racers++; if (player > mine) ahead++; }
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
        if (n == me || !s_opp[n].Count || !s_opp[n].Scenes) continue;
        racers++;
        if (OpponentProgress(&s_opp[n]) > mine) ahead++;
    }
    return racers > 1 ? (double)ahead / (racers - 1) : -1;
}

// How long the lightning spell shrinks a car `back` of the way back in the race (BackShare: 0 leading, 1 last, -1 no race): longer for
// the cars ahead, shorter for the ones behind -- RACE_SHRINK_MS either way by RACE_SHRINK_SPREAD -- to keep the cars together.
static S32 ShrinkMs(double back) {
    return back < 0 ? RACE_SHRINK_MS : (S32)(RACE_SHRINK_MS * (1 + RACE_SHRINK_SPREAD * (1 - 2 * back)));
}

// A mushroom's power-up, at random -- the weights by how far back the car is (`back`, BackShare: -1 the plain weights): the strong ones
// go to the cars at the back and hardly ever to the leader, who draws the weak ones.
static int RandomPowerUp(double back) {
    if (s_nbOnly > 1) { const int pick = s_onlyList[(int)(RaceRandom() * s_nbOnly) % s_nbOnly]; if (pick >= 0 && pick < PU_COUNT) return pick; }
    if (s_powerupOnly >= 0 && s_powerupOnly < PU_COUNT) return s_powerupOnly;
    double w[PU_COUNT], total = 0;
    for (int i = 0; i < PU_COUNT; i++) {
        w[i] = s_puWeight[i] * (back < 0 ? 1 : s_puLead[i] + (s_puLast[i] - s_puLead[i]) * back);
        if (i == PU_FUEL && s_fuelAlways) w[i] = 0;   // (every car has it all race)
        if (i == PU_FAKE && !s_nbFakeActors) w[i] = 0;  // (a track built before them has no copies of the mushroom to show one)
        total += w[i];
    }
    double r = RaceRandom() * total;
    for (int i = 0; i < PU_COUNT; i++) { if (r < w[i]) return i; r -= w[i]; }
    return s_fuelAlways ? PU_PROTECT : PU_FUEL;
}

// An oil slick dropped at (x, y, z) (world units): on the road until a car drives over it (the oldest goes when there are too many).
static void DropOil(double x, double y, double z) {
    int k = 0;
    for (int i = 0; i < RACE_MAX_SLICKS; i++) {
        if (!s_slicks[i].On) { k = i; break; }
        if (s_slicks[i].Dropped < s_slicks[k].Dropped) k = i;
    }
    s_slicks[k].X = x; s_slicks[k].Y = y; s_slicks[k].Z = z; s_slicks[k].Dropped = TimerRefHR; s_slicks[k].On = 1; s_slicks[k].Posed = -1;
    s_slicks[k].Until = 0;
}

// A drip's slick (drip=) at (x, y, z), lying RACE_DRIP_SLICK_MS unless a car takes it first -- only where there is room for it: a free
// slick (never a car's dropped oil taken for it), one of its scene's slicks free to show it (none on the road unseen: each scene has as
// many as this one), and none already under the drip. 1 if it lies there.
static int DripOil(double x, double y, double z, S32 now) {
    int free = -1, shown = 0, actors = 0;
    for (int k = 0; k < s_nbOilActors; k++) if (s_oilScene[k] == NumCube && s_oilActor[k] > 0 && s_oilActor[k] < NbObjets) actors++;
    const int cubeX = (int)floor(x / 32768.0), cubeZ = (int)floor(z / 32768.0);
    for (int i = 0; i < RACE_MAX_SLICKS; i++) {
        const T_RACE_SLICK *sl = &s_slicks[i];
        if (!sl->On) { if (free < 0) free = i; continue; }
        if ((sl->X - x) * (sl->X - x) + (sl->Z - z) * (sl->Z - z) < 1200.0 * 1200.0) return 0;
        if ((int)floor(sl->X / 32768.0) == cubeX && (int)floor(sl->Z / 32768.0) == cubeZ) shown++;
    }
    if (free < 0 || shown >= actors) return 0;
    T_RACE_SLICK *sl = &s_slicks[free];
    sl->X = x; sl->Y = y; sl->Z = z; sl->Dropped = now; sl->On = 1; sl->Posed = -1; sl->Until = now + RACE_DRIP_SLICK_MS;
    return 1;
}

// The road's height under a place of this scene (cube-local) for something at height `ref`: a raised road's there if it is in reach of
// `ref` and over the ground, else the ground's. 0 in *ok when neither is there (off this scene's ground, no road).
static S32 SlickGround(S32 lx, S32 lz, S32 ref, int *ok) {
    const S32 ground = CalculAltitudeTerrain(lx, lz, 0);
    S32 f;
    int seg;
    double lat, t;
    const int deck = RaisedAt(lx + CurrentCubeX * 32768.0, lz + CurrentCubeY * 32768.0, ref + 64, 0, &f, &seg, &lat, &t);
    *ok = deck || ground != -1;
    return deck && f > ground ? f : ground;
}

// A slick laid on the road where it is (cube-local), once it is in this scene: at the road's height there -- it was dropped at the car's,
// 1300 behind it, and on a slope that is in the road or over it -- tilted to the road's slope (the engine turns a body by
// M(Alpha) M(Gamma) M(Beta): with no heading, Gamma tips its up towards -x, Alpha towards +z), and lifted so that the road under its rim
// is nowhere over it (a flat puddle on a slope or a crest had half of it under the road, 2026-10-04).
#define RACE_SLICK_RADIUS 620
// (2026-10-07, the user: "oil at certain points close to the ground passing through the track and landing on the floor below" -- a
// slick dropped behind a car where that is just off the deck, on a bend's outside or past a jump's lip, or by a car in the air, found no
// deck under it and lay on the ground there: now one that misses the deck but has it within RACE_SLICK_ONTO of it at its height is moved
// onto it, and one with no deck near that would lie far under where it was dropped is gone.)
#define RACE_SLICK_ONTO 1100
#define RACE_SLICK_FALL 900
static void PoseSlick(T_RACE_SLICK *sl, S32 lx, S32 lz) {
    const S32 ref = (S32)sl->Y;
    int ok, okA, okB;
    sl->Posed = NumCube; sl->Alpha = sl->Gamma = 0; sl->Lift = 20;
    S32 h0 = SlickGround(lx, lz, ref, &ok);
    if (!ok) return;
    if (s_nbRaised && ref - h0 > RACE_SLICK_FALL) {
        S32 f;
        int seg;
        double lat, t;
        const double wx = lx + CurrentCubeX * 32768.0, wz = lz + CurrentCubeY * 32768.0;
        if (RaisedAt(wx, wz, ref + 64, RACE_SLICK_ONTO, &f, &seg, &lat, &t) && abs(f - ref) < RACE_SLICK_FALL) {
            // onto the deck: to the nearest place on it -- across to it, its edge a slick's width in, and along to the end of it where
            // the slick lay past one (a jump's lip: a car that has just landed drops its oil 1,300 behind it, over the jump's gap, where
            // there is no deck -- the sea or the ground far below, 2026-10-07)
            const T_RACE_RAISED *a = &s_raised[seg], *c = a + 1;
            const double sx = c->X - a->X, sz = c->Z - a->Z, len = sqrt(sx * sx + sz * sz);
            const double half = a->Half + (c->Half - a->Half) * t - RACE_SLICK_RADIUS;
            if (len > 0) {
                const double nx = -sz / len, nz = sx / len, side = half > 0 ? (lat > half ? half : lat < -half ? -half : lat) : 0;
                // (lat is measured to the left of the segment's way: sx * (wz - pz) - sz * (wx - px), over len; t is where along it)
                const double px = a->X + sx * t + nx * side, pz = a->Z + sz * t + nz * side;
                lx = (S32)(px - CurrentCubeX * 32768.0); lz = (S32)(pz - CurrentCubeY * 32768.0);
                sl->X = px; sl->Z = pz;
            }
            h0 = SlickGround(lx, lz, ref, &ok);
            if (!ok || ref - h0 > RACE_SLICK_FALL) {
                Log_Info("[racemod] a slick dropped at %d with no road under it or near it: gone", (int)ref);
                sl->On = 0;
                return;
            }
            Log_Info("[racemod] a slick off the deck moved onto it (%.0f across), at %d", fabs(lat), (int)h0);
        } else {
            Log_Info("[racemod] a slick dropped at %d with no road under it (the ground %d below): gone", (int)ref, (int)(ref - h0));
            sl->On = 0;
            return;
        }
    }
    const S32 d = 256;
    double sx = 0, sz = 0;
    {
        const S32 a = SlickGround(lx + d, lz, ref, &okA), b = SlickGround(lx - d, lz, ref, &okB);
        if (okA && okB) sx = (a - b) / (2.0 * d);
    }
    {
        const S32 a = SlickGround(lx, lz + d, ref, &okA), b = SlickGround(lx, lz - d, ref, &okB);
        if (okA && okB) sz = (a - b) / (2.0 * d);
    }
    // (a step -- a deck's edge to the ground under it -- is no slope)
    if (fabs(sx) > 0.6) sx = 0;
    if (fabs(sz) > 0.6) sz = 0;
    const double n = sqrt(sx * sx + 1 + sz * sz), nx = -sx / n, ny = 1 / n, nz = -sz / n;
    sl->Gamma = (S32)floor(asin(-nx) * 4096 / (2 * RACE_PI) + 0.5) & 4095;
    sl->Alpha = (S32)floor(atan2(nz, ny) * 4096 / (2 * RACE_PI) + 0.5) & 4095;
    double lift = 0;
    for (int k = 0; k < 12; k++) {
        const double a = k * RACE_PI / 6, rx = sin(a) * RACE_SLICK_RADIUS, rz = cos(a) * RACE_SLICK_RADIUS;
        const S32 h = SlickGround(lx + (S32)rx, lz + (S32)rz, ref, &okA);
        if (!okA) continue;
        const double over = h - (h0 + sx * rx + sz * rz);
        if (over > lift && over < 300) lift = over;
    }
    sl->Y = h0;
    sl->Lift = 14 + lift;
}

// A nitro penguin dropped at (x, y, z) (world units): a walker, or on its fuse (RACE_PENGUIN_FUSE). With all of them out, the oldest goes
// off first.
static double LineDistance(const T_RACE_OPPONENT *o, int i, double wx, double wy, double wz);
static void PenguinOff(int i, S32 now);
static int DropPenguin(double x, double y, double z, int dropper) {
    const S32 now = TimerRefHR;
    int k = -1;
    for (int i = 0; i < RACE_MAX_PENGUINS && k < 0; i++) if (!s_penguins[i].On) k = i;
    if (k < 0) {
        k = 0;
        for (int i = 1; i < RACE_MAX_PENGUINS; i++) if (s_penguins[i].Dropped < s_penguins[k].Dropped) k = i;
        PenguinOff(k, now);
    }
    T_RACE_PENGUIN *pg = &s_penguins[k];
    memset(pg, 0, sizeof(*pg));
    pg->X = x; pg->Y = y; pg->Z = z; pg->HomeX = x; pg->HomeZ = z; pg->On = 1; pg->Dropped = now; pg->Dropper = dropper;
    pg->At = now + (s_penguinWalk ? RACE_PENGUIN_ARM : RACE_PENGUIN_FUSE);
    pg->Dir = RaceRandom() < 0.5 ? 1 : -1;
    pg->NextTurn = now + 2000 + (S32)(RaceRandom() * 3000);
    pg->Heading = RaceRandom() * 2 * 3.14159265358979;
    // (its place along the line, and to the side of it)
    const T_RACE_OPPONENT *o = Guide();
    if (s_penguinWalk && o) {
        int best = 0;
        double bd = 1e30;
        for (int i = 0; i < o->Count; i++) { const double d = LineDistance(o, i, x, y, z); if (d < bd) { bd = d; best = i; } }
        pg->S = o->Cum[best];
        double lx, lz, ly, v, dx, dz;
        PathAt(o, pg->S, &lx, &lz, &ly, &v, &dx, &dz);
        const double len = sqrt(dx * dx + dz * dz);
        pg->Side = len > 0 ? ((x - lx) * dz - (z - lz) * dx) / len : 0;
        if (pg->Side > RACE_PENGUIN_SIDE) pg->Side = RACE_PENGUIN_SIDE;
        if (pg->Side < -RACE_PENGUIN_SIDE) pg->Side = -RACE_PENGUIN_SIDE;
        pg->SideTo = pg->Side;
    }
    return k;
}

// Console (tests): Twinsen uses power-up `pu` now, as from his item box (raceitem); a nitro penguin goes off `cells` ahead of his car
// (raceblast), as if dropped by another car -- the blast's own rules decide what it does to him.
static void UseItem(int pu, S32 now);
void RaceMod_TestItem(int pu) {
    if (!RaceMod_Active() || pu < 0 || pu >= PU_COUNT) return;
    UseItem(pu, TimerRefHR);
}
void RaceMod_TestBlast(double cells) {
    if (!RaceMod_Active() || CubeMode != CUBE_EXTERIEUR) return;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    const double b = hero->Obj.Beta * 2 * 3.14159265358979 / 4096;
    const int k = DropPenguin(hero->Obj.X + CurrentCubeX * 32768.0 + sin(b) * cells * 512, hero->Obj.Y,
                              hero->Obj.Z + CurrentCubeY * 32768.0 + cos(b) * cells * 512, 0);
    PenguinOff(k, TimerRefHR);
}

// The lightning spell striking cars (`actors`, this scene's): the game's flash and thunder (EXTRA.CPP InvoqueFoudre's), and a bolt from
// the sky onto each of them while RACE_BOLT_MS lasts (DrawBolts) -- Twinsen's spell on the other cars, or another car's on Twinsen's.
static int s_boltActor[RACE_MAX_OPPONENTS + 1], s_nbBolts = 0;
static S32 s_boltUntil = 0;

static void Strike(S32 now, const int *actors, int count) {
    const S32 flash = InitIncrustDisp(INCRUST_ECLAIR, 0, 0, 0, 0, 0, 1);
    if (flash != -1) ListIncrustDisp[flash].TimerEnd = now + RACE_FLASH_MS;
    s_nbBolts = 0;
    for (int i = 0; i < count && s_nbBolts < RACE_MAX_OPPONENTS + 1; i++)
        if (actors[i] >= 0 && actors[i] < NbObjets) s_boltActor[s_nbBolts++] = actors[i];
    s_boltUntil = now + RACE_BOLT_MS;
    const T_OBJET *at = &ListObjet[s_nbBolts ? s_boltActor[0] : NUM_PERSO];
    HQ_3D_MixSample(SAMPLE_FOUDRE_STEP3, 0x1000, 500, 1, at->Obj.X, at->Obj.Y + 1000, at->Obj.Z);
}

// Twinsen uses an item (Shift).
static void UseItem(int pu, S32 now) {
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    switch (pu) {
    case PU_FUEL: s_boostUntil = now + RACE_BOOST_MS; break;
    case PU_GOLD: {
        // (its time from the first use; each use a burst of Gazogem's boost and a kick)
        if (!s_goldUntil) s_goldUntil = now + RACE_GOLD_MS;
        s_goldLast = now;
        if (s_boostUntil < now + RACE_GOLD_BURST_MS) s_boostUntil = now + RACE_GOLD_BURST_MS;
        if (ListBuggy[0].Speed >= 0) ListBuggy[0].Speed += RACE_GOLD_INJECT;
        Log_Info("[racemod] the golden Gazogem: %d km/h", (int)((ListBuggy[0].Speed * 36 + 2560) / 5120));
        break;
    }
    case PU_PROTECT:
        // (the game's own protection spell round him, its sprites and its hum -- taking no magic while it lasts: EXTRA.CPP)
        s_protectUntil = now + RACE_PROTECT_MS;
        if (!ProtectActif) { ToggleSortProtection(); s_protectSpell = 1; }
        break;
    case PU_LIGHTNING: {
        // (the game's own lightning spell takes all of Twinsen's magic: it is filled, as it is for the spell; and its strike on the
        // other cars in sight)
        // (each car for longer the further ahead it is: ShrinkMs)
        MagicPoint = (U8)(MagicLevel * 20);
        int hit[RACE_MAX_OPPONENTS], struck = 0;
        for (int k = 0; k < RACE_MAX_OPPONENTS; k++) {
            if (!s_opp[k].Count || !s_opp[k].Scenes) continue;
            const S32 ms = ShrinkMs(BackShare(k));
            s_opp[k].ShrunkUntil = now + ms;
            Log_Info("[racemod] %s's car shrunk for %.1f s", s_opp[k].Name, ms / 1000.0);
            const int a = OpponentActor(&s_opp[k]);
            if (a > 0) hit[struck++] = a;
        }
        Strike(now, hit, struck);
        Log_Info("[racemod] the other cars shrunk (%d struck in this scene); magic full (%d)", struck, (int)MagicPoint);
        break;
    }
    case PU_PENGUIN:
    case PU_OIL: {
        // dropped behind the car
        const double b = hero->Obj.Beta * 2 * 3.14159265358979 / 4096;
        const double x = hero->Obj.X + CurrentCubeX * 32768.0 - sin(b) * 1300, z = hero->Obj.Z + CurrentCubeY * 32768.0 - cos(b) * 1300;
        if (pu == PU_OIL) DropOil(x, hero->Obj.Y, z); else DropPenguin(x, hero->Obj.Y, z, -1);
        break;
    }
    case PU_BALL_YELLOW:
    case PU_BALL_GREEN:
    case PU_BALL_RED:
    case PU_BALL_FIRE:
        Attack(pu, -1, hero->Obj.X + CurrentCubeX * 32768.0, hero->Obj.Y, hero->Obj.Z + CurrentCubeY * 32768.0, hero->Obj.Beta * 2 * 3.14159265358979 / 4096, now);
        break;
    case PU_FAKE: {
        // dropped behind the car, where it is now
        const double b = hero->Obj.Beta * 2 * 3.14159265358979 / 4096;
        DropFake(hero->Obj.X + CurrentCubeX * 32768.0 - sin(b) * 1300, hero->Obj.Y, hero->Obj.Z + CurrentCubeY * 32768.0 - cos(b) * 1300, -1, now);
        break;
    }
    case PU_SUPERJET:
        // (nothing touches it: a skid, a blast's stop, a shrinking are over)
        s_jetUntil = now + RACE_JETPACK_MS; s_jetFactor = s_puJet[pu]; s_autoIndex = -1;
        s_skidUntil = 0; s_hitUntil = 0; s_shrunkUntil = 0;
        break;
    }
    // (its name not said on the screen: the item box shows it -- the user, 2026-10-10: "We also don't need a message for each pickup")
    Log_Info("[racemod] Twinsen uses %s%s", s_puName[pu], s_puJet[pu] > 0 && !Guide() ? " (no line: a boost, the keys his)" : "");
}

// EXTRA.CPP: the protection spell is the power-up's -- no magic taken while it lasts; it blinks for its last seconds.
S32 RaceMod_ProtectionFree(void) { return RaceMod_Active() && s_protectSpell && TimerRefHR < s_protectUntil; }
S32 RaceMod_ProtectionEnding(void) { return s_protectUntil - TimerRefHR < RACE_PROTECT_ENDING; }
static void ProtectionOff(void) {
    if (s_protectSpell && ProtectActif) ToggleSortProtection();
    s_protectSpell = 0;
}

// The power-ups' times are the game clock's, and a loaded game sets the clock to the save's (SAVEGAME.CPP): a protection spell or a super
// jet-pack taken after that save then ran on, unseen, until the clock caught up with it -- the spell's sparkles gone with the load, but
// Twinsen's car untouched by a penguin's blast, the jet-pack's hand on the wheel (the user, 2026-10-06: "Twinsen can get hit by a penguin
// and not get affected by the blast"). They belong to the moment they were taken, so a loaded game has none: the item box empty, the
// spells and the jet-pack over, every car's boost, skid, shrinking and stop over, the penguins and the oil slicks gone. (Not on any clock
// that goes back: the game pauses its clock for a dialog, a menu or a scene's loading by setting it back to where it was -- TIMER.CPP
// RestoreTimer -- and the power-ups wait with it.)
static void ForgetPowerUps(const char *why) {
    ProtectionOff();
    // (a protection spell the save had on while Twinsen drives: the race's is the power-up's, gone with it)
    if (ProtectActif && Comportement == C_BUGGY) ToggleSortProtection();
    s_boostUntil = s_protectUntil = s_shrunkUntil = s_jetUntil = 0;
    s_skidUntil = s_hitUntil = 0; s_autoIndex = -1; s_boltUntil = 0; s_nbBolts = 0;
    for (int k = 0; k < RACE_ITEM_SLOTS; k++) s_item[k] = -1;
    s_sel = 0; s_takeAt = 0; s_goldUntil = 0; s_goldLast = 0; s_slotOn = 0;
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
        T_RACE_OPPONENT *o = &s_opp[n];
        o->BoostUntil = o->HitUntil = o->JetUntil = o->ShoveUntil = o->SkidUntil = o->ShrunkUntil = 0;
    }
    for (int i = 0; i < RACE_MAX_PENGUINS; i++) s_penguins[i].On = 0;
    for (int i = 0; i < RACE_MAX_SLICKS; i++) s_slicks[i].On = 0;
    for (int i = 0; i < RACE_MAX_FAKES; i++) s_fakes[i].On = 0;
    for (int i = 0; i < RACE_MAX_SHOTS; i++) s_shots[i].On = 0;
    for (int i = 0; i < s_nbMush; i++) s_mush[i].Back = 0;
    if (s_heroHidden) { ListObjet[NUM_PERSO].Flags &= ~INVISIBLE; s_heroHidden = 0; }
    if (s_heroIsSmall && Comportement == C_BUGGY) InitBody(GEN_BODY_TUNIQUE, NUM_PERSO);
    s_heroIsSmall = 0;
    Log_Info("[racemod] %s: no power-up from before it lasts", why);
}

// SAVEGAME.CPP, once a saved game is loaded (its clock set).
void RaceMod_GameLoaded(void) {
    if (RaceMod_Active()) ForgetPowerUps("a saved game loaded");
}

// A jet-pack's factor now (`until`: when it ends, `full`: its factor): full, then over its last RACE_JET_EASE_MS back down to 1 -- the car
// slowed to its own top speed before it ends, not dropped to it at once.
static double JetFactor(S32 until, double full) {
    const S32 left = until - TimerRefHR;
    if (left <= 0) return 1;
    return left >= RACE_JET_EASE_MS ? full : 1 + (full - 1) * left / (double)RACE_JET_EASE_MS;
}

// The penguin that goes off, as HitObj's hitter: this scene's (not Twinsen: HitObj spares him his own hits, the meca-penguin's armour).
static U8 PenguinHitter(void) {
    for (int k = 0; k < s_nbPenguinActors; k++)
        if (s_penguinScene[k] == NumCube && s_penguinActor[k] > 0 && s_penguinActor[k] < NbObjets) return (U8)s_penguinActor[k];
    return (U8)(NbObjets > 1 ? NbObjets - 1 : NUM_PERSO);
}

// Twinsen races: nothing is to kill him -- a long fall or a lightning strike stops his car as a land mine does (OBJECT.CPP).
S32 RaceMod_NoHarm(void) {
    return RaceMod_Active() && Comportement == C_BUGGY;
}

// An opponent's car (actor `car`) over a mushroom of this scene: it takes it -- a boost, a penguin dropped, or Twinsen shrunk.
static void OpponentMushrooms(int n, T_OBJET *car) {
    const S32 now = TimerRefHR;
    T_RACE_OPPONENT *o = &s_opp[n];
    for (int i = 0; i < s_nbMush; i++) {
        T_RACE_MUSHROOM *m = &s_mush[i];
        const int a = MushroomActor(m);
        if (a < 0 || m->Back || !Near(car, ListObjet[a].Obj.X, m->Y, ListObjet[a].Obj.Z, RACE_MUSHROOM_REACH)) continue;
        m->Back = now + RACE_MUSHROOM_BACK;
        // (one from a row: the others it passes give way, empty)
        if (now - o->TakeAt < RACE_ROW_MS) continue;
        o->TakeAt = now;
        const int pu = RandomPowerUp(BackShare(n));
        if (pu >= PU_BALL_YELLOW && pu <= PU_BALL_FIRE) {
            Attack(pu, n, car->Obj.X + CurrentCubeX * 32768.0, car->Obj.Y, car->Obj.Z + CurrentCubeY * 32768.0, car->Obj.Beta * 2 * 3.14159265358979 / 4096, now);
        } else if (pu == PU_FAKE) {
            const double b = car->Obj.Beta * 2 * 3.14159265358979 / 4096;
            DropFake(car->Obj.X + CurrentCubeX * 32768.0 - sin(b) * 1300, car->Obj.Y, car->Obj.Z + CurrentCubeY * 32768.0 - cos(b) * 1300, n, now);
        } else if (pu == PU_PENGUIN || pu == PU_OIL) {
            const double b = car->Obj.Beta * 2 * 3.14159265358979 / 4096;
            const double x = car->Obj.X + CurrentCubeX * 32768.0 - sin(b) * 1300, z = car->Obj.Z + CurrentCubeY * 32768.0 - cos(b) * 1300;
            if (pu == PU_OIL) DropOil(x, car->Obj.Y, z); else DropPenguin(x, car->Obj.Y, z, n);
        } else if (pu == PU_LIGHTNING && now >= s_jetUntil) {
            // (struck as Twinsen's spell strikes the others: the flash, the thunder and a bolt onto his car -- which the protection spell
            // shrugs off)
            const int me = NUM_PERSO;
            Strike(now, &me, 1);
            const int protect = now < s_protectUntil;
            const S32 ms = ShrinkMs(BackShare(-1));
            if (!protect) s_shrunkUntil = now + ms;
            if (protect) Log_Info("[racemod] %s's lightning strikes Twinsen's car: the protection spell holds", o->Name);
            else Log_Info("[racemod] Twinsen's car struck and shrunk by %s's lightning for %.1f s", o->Name, ms / 1000.0);
        } else if (s_puJet[pu] > 0) {
            o->JetUntil = now + RACE_JETPACK_MS;
            o->JetFactor = s_puJet[pu];
        } else
            o->BoostUntil = now + RACE_BOOST_MS;
        Log_Info("[racemod] %s takes a mushroom's power-up: %s", o->Name, s_puName[pu]);
    }
}

static void Penguins(S32 now, int driving);

// Every frame: the oil slicks -- shown by this scene's copies of the slick, and any car over one skidding (and taking it with it).
static void Slicks(S32 now, int driving) {
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    int k = 0;
    for (int i = 0; i < RACE_MAX_SLICKS; i++) {
        T_RACE_SLICK *sl = &s_slicks[i];
        if (!sl->On) continue;
        if (sl->Until && now >= sl->Until) { sl->On = 0; continue; }   // (a drip's, dried up)
        const S32 lx = (S32)(sl->X - CurrentCubeX * 32768.0), lz = (S32)(sl->Z - CurrentCubeY * 32768.0);
        if (lx < 0 || lx >= 32768 || lz < 0 || lz >= 32768) continue;
        if (sl->Posed != (S32)NumCube && CubeMode == CUBE_EXTERIEUR) PoseSlick(sl, lx, lz);
        // (shown by the next of this scene's slicks; its box a point, as the mushrooms': no car is stopped by it)
        for (; k < s_nbOilActors; k++) {
            if (s_oilScene[k] != NumCube || s_oilActor[k] <= 0 || s_oilActor[k] >= NbObjets) continue;
            T_OBJET *a = &ListObjet[s_oilActor[k]];
            a->Obj.X = lx; a->Obj.Y = (S32)(sl->Y + sl->Lift); a->Obj.Z = lz;   // (a little over the road: no fight with it in the depth buffer)
            a->Obj.Beta = 0; a->Obj.Alpha = sl->Alpha; a->Obj.Gamma = sl->Gamma;
            a->XMin = a->XMax = a->ZMin = a->ZMax = 0;
            k++;
            break;
        }
        // Twinsen's car over it: it skids, unless the protection spell is on
        if (driving && CubeMode == CUBE_EXTERIEUR && now >= s_skidUntil && now >= s_protectUntil && now >= s_jetUntil && Near(hero, lx, (S32)sl->Y, lz, RACE_OIL_REACH)) {
            s_skidUntil = now + RACE_SKID_MS; s_skidStart = now; s_skidBeta = hero->Obj.Beta; s_skidSpeed = ListBuggy[0].Speed;
            s_skidDir = RaceRandom() < 0.5 ? 1 : -1;
            Log_Info("[racemod] Twinsen skids on oil");
            sl->On = 0;                     // (the car took it with it: a slick is one car's)
        } else if (driving && CubeMode == CUBE_EXTERIEUR && (now < s_protectUntil || now < s_jetUntil) && Near(hero, lx, (S32)sl->Y, lz, RACE_OIL_REACH)) {
            // (protected, or on the super jet-pack: over it unharmed -- it stays for the next car)
            static S32 said = 0;
            if (now > said) { said = now + 1000; Log_Info("[racemod] Twinsen drives over oil, %s", now < s_jetUntil ? "on the super jet-pack" : "protected"); }
        }
        // an opponent's
        for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
            T_RACE_OPPONENT *o = &s_opp[n];
            if (!o->Count || !o->Scenes || now < o->SkidUntil || !RaceOn()) continue;   // (none waiting on its spot)
            double x, z, y, v, dx, dz;
            PathAt(o, o->S, &x, &z, &y, &v, &dx, &dz);
            if ((x - sl->X) * (x - sl->X) + (z - sl->Z) * (z - sl->Z) >= (double)RACE_OIL_REACH * RACE_OIL_REACH || fabs(y - sl->Y) > 900) continue;
            if (now < o->JetUntil) continue;  // (on a super jet-pack: nothing touches it)
            o->SkidUntil = now + RACE_SKID_MS; o->SkidStart = now; o->SkidDir = RaceRandom() < 0.5 ? 1 : -1; o->SkidSpeed = o->Speed;
            Log_Info("[racemod] %s skids on oil", o->Name);
            sl->On = 0;
            break;
        }
        if (!sl->On) continue;
    }
    // the scene's slicks not on the road, out of sight
    for (; k < s_nbOilActors; k++)
        if (s_oilScene[k] == NumCube && s_oilActor[k] > 0 && s_oilActor[k] < NbObjets) ListObjet[s_oilActor[k]].Obj.Y = -20000;
}

// ---- the magic ball's attacks and the fake mushrooms -------------------------------------------------------------------------------

// A car where it is (world units): -1 Twinsen's, else an opponent's -- its car where this scene has it in sight, else where its line has
// it in the race. 0 when it isn't anywhere (an opponent waiting out of sight, no race).
static int CarAt(int who, double *x, double *y, double *z) {
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    if (who < 0) {
        if (CubeMode != CUBE_EXTERIEUR) return 0;
        const T_OBJET *h = &ListObjet[NUM_PERSO];
        *x = h->Obj.X + cx; *y = h->Obj.Y; *z = h->Obj.Z + cz;
        return 1;
    }
    T_RACE_OPPONENT *op = &s_opp[who];
    if (!op->Count || !op->Scenes) return 0;
    const int a = OpponentActor(op);
    if (a >= 0 && ListObjet[a].Obj.Y > -10000) { *x = ListObjet[a].Obj.X + cx; *y = ListObjet[a].Obj.Y; *z = ListObjet[a].Obj.Z + cz; return 1; }
    if (!RaceOn()) return 0;
    double v, dx, dz;
    PathAt(op, op->S, x, z, y, &v, &dx, &dz);
    return 1;
}

static const char *CarName(int who) { return who < 0 ? "Twinsen" : s_opp[who].Name; }

// The car nearest ahead of `who` in the race (-2 none: it leads), by how far round each is.
static int CarInFront(int who) {
    const double mine = who < 0 ? PlayerProgress() : OpponentProgress(&s_opp[who]);
    int best = -2;
    double gap = 1e30;
    if (who >= 0) { const double p = PlayerProgress(); if (p > mine && p - mine < gap) { gap = p - mine; best = -1; } }
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
        if (n == who || !s_opp[n].Count || !s_opp[n].Scenes) continue;
        const double p = OpponentProgress(&s_opp[n]);
        if (p > mine && p - mine < gap) { gap = p - mine; best = n; }
    }
    return best;
}

// Where a place is along the guide line (its S) and to the side of it -- the nearest point within `window` points of `hint` (a fresh
// search with no hint).
static double GuideS(const T_RACE_OPPONENT *o, double x, double y, double z, double hint, double *side) {
    int best = -1;
    double bd = 1e30;
    if (hint >= 0) {
        int i0 = 0;
        while (i0 < o->Count - 1 && o->Cum[i0 + 1] < hint) i0++;
        for (int k = -120; k <= 120; k++) {
            const int i = ((i0 + k) % o->Count + o->Count) % o->Count;
            const double d = LineDistance(o, i, x, y, z);
            if (d < bd) { bd = d; best = i; }
        }
    }
    if (best < 0 || bd > 12.0 * 512 * 12 * 512)
        for (int i = 0; i < o->Count; i++) { const double d = LineDistance(o, i, x, y, z); if (d < bd) { bd = d; best = i; } }
    const double s = o->Cum[best];
    double lx, lz, ly, v, dx, dz;
    PathAt(o, s, &lx, &lz, &ly, &v, &dx, &dz);
    const double len = sqrt(dx * dx + dz * dz);
    if (side) *side = len > 0 ? ((x - lx) * dz - (z - lz) * dx) / len : 0;
    return s;
}

// A point of the guide line and to its side (the inverse of GuideS), its height the line's.
static void GuideAt(const T_RACE_OPPONENT *o, double s, double side, double *x, double *y, double *z, double *hx, double *hz) {
    double lx, lz, ly, v, dx, dz;
    s = fmod(s, o->Length); if (s < 0) s += o->Length;
    PathAt(o, s, &lx, &lz, &ly, &v, &dx, &dz);
    const double len = sqrt(dx * dx + dz * dz);
    *hx = len > 0 ? dx / len : 0; *hz = len > 0 ? dz / len : 1;
    *x = lx + side * *hz; *z = lz - side * *hx; *y = ly;
}

// A car struck (a ball's hit, a fake mushroom's blast): -1 Twinsen's -- stopped as by a penguin's blast unless the protection spell or the
// super jet-pack is on -- else an opponent's, stopped with its hit animation unless it is on a super jet-pack.
static void CarStruckFor(int who, S32 now, const char *what, const char *by, S32 hold, const char *message);
static void CarStruck(int who, S32 now, const char *what, const char *by) { CarStruckFor(who, now, what, by, RACE_PENGUIN_HOLD, NULL); }

// ... held `hold` ms, and Twinsen told `message` (NULL: "Hit by ...!") -- a gas monster's bite (Monsters).
static void CarStruckFor(int who, S32 now, const char *what, const char *by, S32 hold, const char *message) {
    if (who < 0) {
        T_OBJET *hero = &ListObjet[NUM_PERSO];
        if (now < s_protectUntil || now < s_jetUntil || (hero->Flags & NO_CHOC)) {
            Log_Info("[racemod] %s hits Twinsen's car: %s", what, now < s_protectUntil ? "the protection spell holds" : now < s_jetUntil ? "the super jet-pack" : "he can't be hit");
            return;
        }
        HitObj(PenguinHitter(), NUM_PERSO, RACE_SHOT_FORCE, -1);
        ListBuggy[0].Speed = 0;
        if (s_hitUntil < now + hold) s_hitUntil = now + hold;
        // (not said on the screen: the car stopped, its stars, say it -- the user, 2026-10-10: "We don't need messages saying 'Hit by
        // shuttle', 'bitten by gas monster', or 'Hit by gunner'"; the log has it)
        (void)message;
        Log_Info("[racemod] Twinsen's car hit by %s%s%s", by ? by : "", by ? "'s " : "", what);
        return;
    }
    T_RACE_OPPONENT *op = &s_opp[who];
    if (now < op->JetUntil) return;
    if (op->HitUntil < now + hold) op->HitUntil = now + hold;
    Log_Info("[racemod] %s hit by %s%s%s", op->Name, by ? by : "", by ? "'s " : "", what);
    const int index = OpponentActor(op);
    if (index < 0 || ListObjet[index].Obj.Y < -10000) return;
    T_OBJET *car = &ListObjet[index];
    InitAnim(GEN_ANIM_CHOC, ANIM_ALL_THEN, (U8)index);
    InitHitExtraPof(car->Obj.X, car->Obj.Y + car->YMax + 1, car->Obj.Z, RACE_SHOT_FORCE);
}

// OBJECT.CPP HitObj: an opponent's car hit by Twinsen -- his own magic ball thrown from the car -- is stopped as the power-ups' balls stop
// it (its armour took the game's hits without a flinch). 1 when it was one.
S32 RaceMod_OpponentStruck(U8 num, U8 hitter) {
    if (!RaceMod_Active() || hitter != NUM_PERSO || num == NUM_PERSO) return 0;
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
        if (!s_opp[n].Count || !s_opp[n].Scenes || OpponentActor(&s_opp[n]) != num) continue;
        const S32 now = TimerRefHR;
        if (now >= s_opp[n].HitUntil) CarStruck(n, now, "magic ball", "Twinsen");
        return 1;
    }
    return 0;
}

static void LaunchShot(int kind, int owner, int target, double x, double y, double z, double hx, double hz, S32 now) {
    int k = -1;
    for (int i = 0; i < RACE_MAX_SHOTS && k < 0; i++) if (!s_shots[i].On) k = i;
    if (k < 0) {
        k = 0;
        for (int i = 1; i < RACE_MAX_SHOTS; i++) if (s_shots[i].T0 < s_shots[k].T0) k = i;
        if (s_shots[k].Extra >= 0 && s_shots[k].Extra < MAX_EXTRAS) ListExtra[s_shots[k].Extra].Sprite = -1;
    }
    T_RACE_SHOT *sh = &s_shots[k];
    memset(sh, 0, sizeof(*sh));
    sh->On = 1; sh->Kind = kind; sh->Owner = owner; sh->Target = target; sh->Extra = -1;
    sh->Hx = hx; sh->Hz = hz;
    sh->X = x + hx * 900; sh->Y = y + RACE_SHOT_RISE; sh->Z = z + hz * 900;      // (out in front of the car)
    sh->T0 = now; sh->Trail = now;
    const T_RACE_OPPONENT *o = Guide();
    if (o) sh->S = GuideS(o, sh->X, y, sh->Z, -1, &sh->Side);
    sh->SideV = RaceRandom() < 0.5 ? RACE_ROLL_ACROSS : -RACE_ROLL_ACROSS;
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    if (CubeMode == CUBE_EXTERIEUR && sh->X >= cx && sh->X < cx + 32768 && sh->Z >= cz && sh->Z < cz + 32768)
        SampleMagicBall((S32)(sh->X - cx), (S32)sh->Y, (S32)(sh->Z - cz));
}

// A ball power-up used by a car (`owner` -1 Twinsen, else an opponent) at (x, y, z) facing `beta` (radians): each out of the front of
// the car -- the yellow darts ahead, the green rolls ahead, the red goes after the car in front (with none, it darts ahead as far again),
// the fire ball one after every other car, every one of them going forwards round the lap to its car (the ones after the cars behind
// went back along the track: the user, 2026-10-10, "Make all magic balls exit the car forwards instead of being dropped behind").
static void Attack(int pu, int owner, double x, double y, double z, double beta, S32 now) {
    const double hx = sin(beta), hz = cos(beta);
    switch (pu) {
    case PU_BALL_YELLOW: LaunchShot(SHOT_YELLOW, owner, -2, x, y, z, hx, hz, now); break;
    case PU_BALL_GREEN: LaunchShot(SHOT_GREEN, owner, -2, x, y, z, hx, hz, now); break;
    case PU_BALL_RED: {
        const int target = CarInFront(owner);
        LaunchShot(SHOT_RED, owner, target, x, y, z, hx, hz, now);
        Log_Info("[racemod] %s's red ball goes after %s", CarName(owner), target == -2 ? "no one (in the lead)" : CarName(target));
        break;
    }
    case PU_BALL_FIRE: {
        int count = 0;
        if (owner >= 0) { LaunchShot(SHOT_FIRE, owner, -1, x, y, z, hx, hz, now); count++; }
        for (int n = 0; n < RACE_MAX_OPPONENTS; n++)
            if (n != owner && s_opp[n].Count && s_opp[n].Scenes) { LaunchShot(SHOT_FIRE, owner, n, x, y, z, hx, hz, now); count++; }
        Log_Info("[racemod] %s's fire ball splits in %d", CarName(owner), count);
        break;
    }
    }
    if (owner >= 0) Log_Info("[racemod] %s uses %s", s_opp[owner].Name, s_puName[pu]);
}

// Every frame: the balls in flight -- moved, shown where they are in this scene (a sprite each, a trail behind it), and any car they reach
// stopped.
static void Shots(S32 now, int driving) {
    const T_RACE_OPPONENT *o = Guide();
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    static S32 last = 0;
    double dt = last ? (now - last) / 1000.0 : 0;
    last = now;
    if (dt < 0) dt = 0;
    if (dt > 0.1) dt = 0.1;
    static const char *const names[] = {"yellow magic ball", "green magic ball", "red magic ball", "fire ball", "green laser", "red laser"};
    for (int i = 0; i < RACE_MAX_SHOTS; i++) {
        T_RACE_SHOT *sh = &s_shots[i];
        if (!sh->On) continue;
        const S32 age = now - sh->T0;
        const double fromX = sh->X, fromZ = sh->Z;      // (where it was: a car hit anywhere along its way since -- fast, it can pass one in a frame)
        int done = 0;
        const int laser = sh->Kind == SHOT_LASER_GREEN || sh->Kind == SHOT_LASER_RED;
        if (sh->Kind == SHOT_YELLOW || (sh->Kind == SHOT_RED && sh->Target == -2) || laser || !o) {
            // straight ahead, over the road
            const double step = (laser ? RACE_LASER_SPEED : RACE_DART_SPEED) * dt;
            sh->X += sh->Hx * step; sh->Z += sh->Hz * step; sh->Travelled += step;
            if (o) { double side, y, x2, z2, hx, hz; sh->S = GuideS(o, sh->X, sh->Y, sh->Z, sh->S, &side); GuideAt(o, sh->S, side, &x2, &y, &z2, &hx, &hz); sh->Y = y + RACE_SHOT_RISE; }
            if (sh->Travelled >= (laser ? RACE_LASER_RANGE : RACE_DART_RANGE * (sh->Kind == SHOT_RED ? 2 : 1))) done = 1;
        } else if (sh->Kind == SHOT_GREEN) {
            // along the track, from rail to rail, hopping
            sh->S += RACE_ROLL_SPEED * dt;
            sh->Side += sh->SideV * dt;
            if (sh->Side > RACE_ROLL_SIDE) { sh->Side = RACE_ROLL_SIDE; sh->SideV = -fabs(sh->SideV); }
            if (sh->Side < -RACE_ROLL_SIDE) { sh->Side = -RACE_ROLL_SIDE; sh->SideV = fabs(sh->SideV); }
            double y, hx, hz;
            GuideAt(o, sh->S, sh->Side, &sh->X, &y, &sh->Z, &hx, &hz);
            sh->Y = y + 250 + 650 * fabs(sin(age * 3.14159265358979 / 450.0));
            if (age >= RACE_GREEN_MS) done = 1;
        } else {
            // after its car: forwards along the track, round the lap to it however far behind the thrower it is, straight at it once near
            // and ahead; until it hits it (a car not on the lap for the moment -- in another scene, not placed -- waited for)
            double tx, ty, tz;
            if (!CarAt(sh->Target, &tx, &ty, &tz)) {
                if (sh->Target < -1 || age >= RACE_HOME_MS * 4) done = 1;
                else {
                    sh->S += RACE_HOME_SPEED * dt;
                    double y, hx, hz;
                    GuideAt(o, sh->S, sh->Side, &sh->X, &y, &sh->Z, &hx, &hz);
                    sh->Y = y + RACE_SHOT_RISE;
                }
            } else {
                const double dx = tx - sh->X, dz = tz - sh->Z, d = sqrt(dx * dx + dz * dz), step = RACE_HOME_SPEED * dt;
                double tside;
                double ahead = fmod(GuideS(o, tx, ty, tz, -1, &tside) - sh->S, o->Length);
                if (ahead < 0) ahead += o->Length;
                if (d < RACE_HOME_DIRECT && (ahead < RACE_HOME_DIRECT * 2 || age > 600)) {
                    const double dy = ty + RACE_SHOT_RISE - sh->Y;
                    const double f = d > step ? step / d : 1;
                    sh->X += dx * f; sh->Z += dz * f; sh->Y += dy * (d > 1 ? (f < 1 ? f : 1) : 1);
                    sh->S = GuideS(o, sh->X, sh->Y, sh->Z, sh->S, NULL);
                } else {
                    sh->S += ahead < step ? ahead : step;
                    sh->Side += (tside - sh->Side) * (dt * 2 < 1 ? dt * 2 : 1);
                    double y, hx, hz;
                    GuideAt(o, sh->S, sh->Side, &sh->X, &y, &sh->Z, &hx, &hz);
                    sh->Y = y + RACE_SHOT_RISE;
                }
            }
        }
        // a car it reaches (not its own: the red and the fire only their own targets)
        if (!done && (age > 150 || sh->Kind >= SHOT_RED))
            for (int who = -1; who < RACE_MAX_OPPONENTS && !done; who++) {
                if (who == sh->Owner || (sh->Kind >= SHOT_RED && sh->Target != -2 && who != sh->Target)) continue;
                if (who < 0 && !driving) continue;
                if (who >= 0 && (!s_opp[who].Count || !s_opp[who].Scenes || now < s_opp[who].HitUntil)) continue;
                double x, y, z;
                if (!CarAt(who, &x, &y, &z)) continue;
                const double wx = sh->X - fromX, wz = sh->Z - fromZ, w2 = wx * wx + wz * wz;
                double along = w2 > 0 ? ((x - fromX) * wx + (z - fromZ) * wz) / w2 : 1;
                if (along < 0) along = 0;
                if (along > 1) along = 1;
                const double dx = x - (fromX + wx * along), dz = z - (fromZ + wz * along);
                const double reach = laser ? RACE_LASER_REACH : RACE_SHOT_REACH;
                if (dx * dx + dz * dz > reach * reach || fabs(y + RACE_SHOT_RISE - sh->Y) > 1400) continue;
                const S32 lx = (S32)(sh->X - cx), lz = (S32)(sh->Z - cz);
                if (CubeMode == CUBE_EXTERIEUR && lx >= 0 && lx < 32768 && lz >= 0 && lz < 32768) {
                    if (laser) InitHitExtraPof(lx, (S32)sh->Y, lz, RACE_SHOT_FORCE); else MagicBallExplo(lx, (S32)sh->Y, lz, 0);
                }
                CarStruck(who, now, names[sh->Kind], CarName(sh->Owner));
                Log_Info("[racemod] (the %s hit after %.1f s)", names[sh->Kind], age / 1000.0);
                done = 1;
            }
        // shown: its sprite in this scene, moved; a trail behind it
        const S32 lx = (S32)(sh->X - cx), lz = (S32)(sh->Z - cz);
        const int here = !done && CubeMode == CUBE_EXTERIEUR && lx >= 0 && lx < 32768 && lz >= 0 && lz < 32768;
        const S16 sprite = laser ? (S16)(3 + 32768) : (S16)(8 + sh->Kind);
        if (sh->Extra >= 0 && (sh->Extra >= MAX_EXTRAS || ListExtra[sh->Extra].Sprite != sprite)) sh->Extra = -1;
        if (!here) {
            if (sh->Extra >= 0) { ListExtra[sh->Extra].Sprite = -1; sh->Extra = -1; }
        } else {
            if (sh->Extra < 0) sh->Extra = laser ? LaserExtra(s_laserModel[sh->Kind == SHOT_LASER_RED], lx, (S32)sh->Y, lz)
                                                 : InitExtraAnimSprite(255, lx, (S32)sh->Y, lz, sprite, sprite, RACE_HOME_MS + 1000, RACE_SHOT_SCALE, 0, 0);
            if (sh->Extra >= 0) {
                ListExtra[sh->Extra].PosX = lx; ListExtra[sh->Extra].PosY = (S32)sh->Y; ListExtra[sh->Extra].PosZ = lz;
                if (laser) ListExtra[sh->Extra].Beta = (S16)((S32)(atan2(sh->Hx, sh->Hz) * 4096 / (2 * 3.14159265358979)) & 4095);
            }
        }
        if (done) sh->On = 0;
    }
}

// A laser's bolt in this scene: the model as a 3D extra (EXTRA.CPP AffSpecial's case 3 draws it, its Scale the pitch), moved by Shots
// rather than flown by the extras' own code.
static int LaserExtra(int model, S32 x, S32 y, S32 z) {
    if (model < 0) return -1;
    for (int n = 0; n < MAX_EXTRAS; n++) {
        T_EXTRA *e = &ListExtra[n];
        if (e->Sprite != -1) continue;
        e->Body = (S16)model; e->PtrBody = (U8 *)GivePtrObjFix(model);
        e->Sprite = (S16)(3 + 32768); e->Scale = 0; e->Flags = EXTRA_NO_SHADOW;
        e->PosX = x; e->PosY = y; e->PosZ = z; e->Vx = e->Vy = e->Vz = 0;
        e->Beta = 0; e->Owner = 255; e->Timer = TimerRefHR; e->TimeOut = 0; e->Divers = 0; e->HitForce = 0; e->Poids = 0; e->Info = 0;
        return n;
    }
    return -1;
}

// Twinsen drives one of his laser cars (his own car: the user, 2026-10-08, "let's keep the look of his original car, it already has an
// animation for firing bullets just make it so this gun fires the lasers instead").
S32 RaceMod_LaserCar(void) {
    return RaceMod_Active() && s_driveLaser > 0 && s_laserModel[s_driveLaser == 2] >= 0 && Comportement == C_BUGGY;
}

// FICHE.CPP F_THROW_MAGIC_EXTRA (the buggy's gun: a magic ball out of its muzzle on each shot of its firing animation): in a laser car,
// a laser bolt instead, straight ahead out of the front of the car. Non-zero: fired, the animation's own ball left out.
S32 RaceMod_LaserThrow(S32 numobj) {
    if (numobj != NUM_PERSO || !RaceMod_LaserCar() || CubeMode != CUBE_EXTERIEUR) return 0;
    static int told = 0;
    if (!told) { told = 1; Log_Info("[racemod] Twinsen's car's gun fires %s lasers", s_driveLaser == 2 ? "red" : "green"); }
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    const double b = hero->Obj.Beta * 2 * 3.14159265358979 / 4096, hx = sin(b), hz = cos(b);
    LaunchShot(s_driveLaser == 2 ? SHOT_LASER_RED : SHOT_LASER_GREEN, -1, -2, hero->Obj.X + CurrentCubeX * 32768.0 + hx * RACE_LASER_MUZZLE, hero->Obj.Y,
               hero->Obj.Z + CurrentCubeY * 32768.0 + hz * RACE_LASER_MUZZLE, hx, hz, TimerRefHR);
    return 1;
}

static void DropFake(double x, double y, double z, int dropper, S32 now) {
    int k = -1;
    for (int i = 0; i < RACE_MAX_FAKES && k < 0; i++) if (!s_fakes[i].On) k = i;
    if (k < 0) { k = 0; for (int i = 1; i < RACE_MAX_FAKES; i++) if (s_fakes[i].Dropped < s_fakes[k].Dropped) k = i; }
    T_RACE_FAKE *f = &s_fakes[k];
    f->On = 1; f->Dropper = dropper; f->X = x; f->Y = y; f->Z = z; f->Dropped = now; f->Posed = -1;
    Log_Info("[racemod] %s drops a fake mushroom", CarName(dropper));
}

// Every frame: the fake mushrooms -- shown by this scene's copies of the mushroom, on the road where they lie; a car that drives into one
// (not the one that dropped it, for a moment) sets it off: a blast, the car stopped.
static void Fakes(S32 now, int driving) {
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    int k = 0;
    for (int i = 0; i < RACE_MAX_FAKES; i++) {
        T_RACE_FAKE *f = &s_fakes[i];
        if (!f->On) continue;
        const S32 lx = (S32)(f->X - cx), lz = (S32)(f->Z - cz);
        const int here = CubeMode == CUBE_EXTERIEUR && lx >= 0 && lx < 32768 && lz >= 0 && lz < 32768;
        if (here && f->Posed != (S32)NumCube) {
            int ok;
            const S32 ground = SlickGround(lx, lz, (S32)f->Y, &ok);
            if (ok) f->Y = ground;
            f->Posed = NumCube;
        }
        if (here)
            for (; k < s_nbFakeActors; k++) {
                if (s_fakeScene[k] != NumCube || s_fakeActor[k] <= 0 || s_fakeActor[k] >= NbObjets) continue;
                T_OBJET *a = &ListObjet[s_fakeActor[k]];
                a->Obj.X = lx; a->Obj.Y = (S32)f->Y; a->Obj.Z = lz;
                a->XMin = a->XMax = a->ZMin = a->ZMax = 0;
                k++;
                break;
            }
        // a car into it
        int hit = -3;
        for (int who = -1; who < RACE_MAX_OPPONENTS && hit == -3; who++) {
            if (who == f->Dropper && now - f->Dropped < RACE_FAKE_ARM) continue;
            if (who < 0 && !driving) continue;
            if (who >= 0 && (!s_opp[who].Count || !s_opp[who].Scenes || !RaceOn())) continue;
            double x, y, z;
            if (!CarAt(who, &x, &y, &z)) continue;
            if ((x - f->X) * (x - f->X) + (z - f->Z) * (z - f->Z) < (double)RACE_MUSHROOM_REACH * RACE_MUSHROOM_REACH && fabs(y - f->Y) < 900) hit = who;
        }
        if (hit == -3) continue;
        if (here) DoImpact(RACE_PENGUIN_IMPACT, lx, (S32)f->Y, lz, 255);
        CarStruck(hit, now, "a fake mushroom", NULL);
        f->On = 0;
    }
    // the scene's copies not in use, out of sight
    for (; k < s_nbFakeActors; k++)
        if (s_fakeScene[k] == NumCube && s_fakeActor[k] > 0 && s_fakeActor[k] < NbObjets) ListObjet[s_fakeActor[k]].Obj.Y = -20000;
}

// Every frame: the mushrooms of this scene (gone while taken, grown back after), the player's car over one, the penguins going off.
static void PowerUps(S32 now, int driving) {
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    for (int i = 0; i < s_nbMush; i++) {
        T_RACE_MUSHROOM *m = &s_mush[i];
        if (m->Back && now >= m->Back) m->Back = 0;
        const int a = MushroomActor(m);
        if (a < 0) continue;
        ListObjet[a].Obj.Y = m->Back ? -20000 : m->Y;
        // (its box a point: nothing is stopped by it before it is taken)
        ListObjet[a].XMin = ListObjet[a].XMax = ListObjet[a].ZMin = ListObjet[a].ZMax = 0;
        if (m->Back || !driving || CubeMode != CUBE_EXTERIEUR) continue;
        if (!Near(hero, ListObjet[a].Obj.X, m->Y, ListObjet[a].Obj.Z, RACE_MUSHROOM_REACH)) continue;
        m->Back = now + RACE_MUSHROOM_BACK;
        ListObjet[a].Obj.Y = -20000;
        // the row's first: its item into a free slot, the roulette turning (the others of the row give way, empty; so does every
        // mushroom while both slots are full)
        if (now - s_takeAt < RACE_ROW_MS) continue;
        int slot = -1;
        for (int k = 0; k < RACE_ITEM_SLOTS && slot < 0; k++) if (s_item[k] < 0) slot = k;
        if (slot < 0) continue;
        s_takeAt = now;
        const double back = BackShare(-1);
        s_item[slot] = RandomPowerUp(back);
        s_itemAt[slot] = now + RACE_ITEM_ROLL_MS;
        if (s_item[s_sel] < 0 || s_sel == slot) s_sel = slot;
        Log_Info("[racemod] Twinsen takes %s (slot %d; %.2f of the way back)", s_puName[s_item[slot]], slot + 1, back);
    }
    // Shift uses the item in the selected slot once its roulette has stopped (the other slot's when that one is empty), Q selects the
    // other slot; the test pilot uses each item a moment after its roulette stops
    const int shift = CheckKey(K_SHIFT) || CheckKey(K_SHIFT_RIGHT), q = CheckKey(K_Q);
    if (driving && q && !s_qWas && s_item[s_sel ^ 1] >= 0) { s_sel ^= 1; Log_Info("[racemod] item slot %d selected", s_sel + 1); }
    const int k = s_item[s_sel] >= 0 ? s_sel : s_sel ^ 1;
    // (the golden Gazogem's time over: gone from its slot)
    for (int g = 0; g < RACE_ITEM_SLOTS; g++)
        if (s_item[g] == PU_GOLD && s_goldUntil && now >= s_goldUntil) {
            s_item[g] = -1; s_goldUntil = 0;
            if (s_sel == g && s_item[g ^ 1] >= 0) s_sel = g ^ 1;
            Log_Info("[racemod] the golden Gazogem is used up");
        }
    const int gold = s_item[k] == PU_GOLD;
    if (driving && s_item[k] >= 0 && now >= s_itemAt[k] && ((shift && !s_shiftWas)
            || (s_auto && s_autoDrop > 0 && now >= s_itemAt[k] + s_autoDrop && (!gold || now >= s_goldLast + RACE_GOLD_AUTO_MS)))) {
        const int pu = s_item[k];
        if (!gold) {
            s_item[k] = -1;
            if (s_item[k ^ 1] >= 0) s_sel = k ^ 1;
        }
        s_shiftBlock = shift;
        UseItem(pu, now);
    }
    if (!shift) s_shiftBlock = 0;
    s_shiftWas = shift;
    s_qWas = q;
    // the super jet-pack: the car turned into it -- hidden, the jet-pack drawn in its place (DrawSuperJet)
    // (over its last RACE_JET_EASE_MS, flashing: the car and the jet-pack in turn, so the player sees it is about to end)
    const S32 jetLeft = s_jetUntil - now;
    const int superJet = driving && jetLeft > 0 && s_superModel >= 0 && CubeMode == CUBE_EXTERIEUR
                         && (jetLeft >= RACE_JET_EASE_MS || (jetLeft / RACE_JET_FLASH_MS) % 2 == 0);
    // (every frame: the game sets the car's flags again as it drives -- hidden once, it showed under the jet-pack; and hidden while it is
    // driven as another car, drawn in its place: DrawDriveAs)
    const int driveAs = driving && s_driveBody >= 0 && s_driveAnim >= 0 && CubeMode == CUBE_EXTERIEUR;
    s_superShown = superJet;
    if (superJet || driveAs) { hero->Flags |= INVISIBLE; s_heroHidden = 1; }
    else if (s_heroHidden) { hero->Flags &= ~INVISIBLE; s_heroHidden = 0; }
    // shrunk by an opponent's lightning: Twinsen's car at half its size while it lasts
    const int small = driving && s_heroSmall >= 0 && now < s_shrunkUntil && now >= s_jetUntil;
    s_driveSmallNow = driving && now < s_shrunkUntil && now >= s_jetUntil;
    if (small && hero->GenBody != s_heroSmall) { InitBody((U8)s_heroSmall, NUM_PERSO); s_heroIsSmall = 1; }
    else if (!small && s_heroIsSmall) { if (driving) InitBody(GEN_BODY_TUNIQUE, NUM_PERSO); s_heroIsSmall = 0; }
    // the protection spell's time up
    if (s_protectSpell && now >= s_protectUntil) { ProtectionOff(); Log_Info("[racemod] the protection spell is over"); }
    // ... or the game's spell gone before it: the game ends it whenever a script takes Twinsen's moves (GERELIFE.CPP SET_DIR) -- a jump's
    // flight, at its take-off and its landing -- or at the spell's own key, and a scene's change clears its bubbles after putting it
    // back (OBJECT.CPP ClearExtra, with the hero's anim kept). The race's protection lasts its time all the same (the blasts, the oil,
    // the lightning leave the car alone), so the spell is put back while he drives: the bubbles round the car say so. (They went with
    // the jump, and a penguin's blast after it seemed to do nothing: the user, 2026-10-06.)
    else if (s_protectSpell && driving) {
        int shown = 0;
        for (int e = 0; e < MAX_EXTRAS && !shown; e++) shown = ListExtra[e].Sprite != -1 && (ListExtra[e].Flags & EXTRA_PROTECT);
        if (!ProtectActif || !shown) {
            if (ProtectActif) ToggleSortProtection();
            ToggleSortProtection();
            Log_Info("[racemod] the protection spell put back: %d ms of it left", (int)(s_protectUntil - now));
        }
    }
    // the penguins: walking the track until a car comes near (or on their fuse), shown by this scene's penguins
    Penguins(now, driving);
    if (now >= s_jetUntil && s_jetUntil) { s_jetUntil = 0; s_autoIndex = -1; }
    Slicks(now, driving);
    Fakes(now, driving);
    Shots(now, driving);
}

// A penguin goes off: the blast (where the player is) and every car near it stopped.
static void PenguinOff(int i, S32 now) {
    T_RACE_PENGUIN *pg = &s_penguins[i];
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    const int driving = Comportement == C_BUGGY;
    {
        pg->On = 0;
        Log_Info("[racemod] a nitro penguin goes off");
        const S32 lx = (S32)(pg->X - CurrentCubeX * 32768.0), lz = (S32)(pg->Z - CurrentCubeY * 32768.0);
        if (lx < 0 || lx >= 32768 || lz < 0 || lz >= 32768 || CubeMode != CUBE_EXTERIEUR) return;
        DoImpact(RACE_PENGUIN_IMPACT, lx, (S32)pg->Y, lz, NUM_PERSO);
        // every car near it stopped for a moment, with the hit reaction a land mine gives Twinsen's (Twinsen's unless the protection spell is
        // on): HitObj -- the hit animation, the stars, the car stopped and no life lost (RaceMod_CarHit); an opponent's car its own hit
        // animation (the racer entity's 6) and the stars
        if (driving && Near(hero, lx, (S32)pg->Y, lz, RACE_PENGUIN_REACH) && (now < s_protectUntil || now < s_jetUntil || (hero->Flags & NO_CHOC) || hero->Armure > RACE_BLAST_FORCE))
            Log_Info("[racemod] the blast leaves Twinsen's car as it is: %s", now < s_protectUntil ? "the protection spell" : now < s_jetUntil ? "the super jet-pack"
                     : (hero->Flags & NO_CHOC) ? (ProtectActif ? "the game's protection spell is on" : "he can't be hit (NO_CHOC)") : "his armour");
        if (driving && now >= s_protectUntil && now >= s_jetUntil && Near(hero, lx, (S32)pg->Y, lz, RACE_PENGUIN_REACH)) {
            HitObj(PenguinHitter(), NUM_PERSO, RACE_BLAST_FORCE, -1);
            ListBuggy[0].Speed = 0;
            if (s_hitUntil < now + RACE_PENGUIN_HOLD) s_hitUntil = now + RACE_PENGUIN_HOLD;
            Log_Info("[racemod] Twinsen's car blown up by a penguin: stopped");
        }
        for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
            T_RACE_OPPONENT *o = &s_opp[n];
            if (!o->Count) continue;
            double x, z, y2, v, dx, dz;
            PathAt(o, o->S, &x, &z, &y2, &v, &dx, &dz);
            if ((x - pg->X) * (x - pg->X) + (z - pg->Z) * (z - pg->Z) >= (double)RACE_PENGUIN_REACH * RACE_PENGUIN_REACH) continue;
            o->HitUntil = now + RACE_PENGUIN_HOLD;
            const int index = OpponentActor(o);
            if (index < 0) continue;
            T_OBJET *car = &ListObjet[index];
            InitAnim(GEN_ANIM_CHOC, ANIM_ALL_THEN, (U8)index);
            InitHitExtraPof(car->Obj.X, car->Obj.Y + car->YMax + 1, car->Obj.Z, RACE_BLAST_FORCE);
            Log_Info("[racemod] %s blown up by a penguin: stopped", o->Name);
        }
    }
}

// Whether a penguin has run into something: a car (the one that dropped it not for its first RACE_PENGUIN_ARM; every other at once: a car
// right behind the one that dropped it drove through it), another penguin (`other`: which), a mushroom standing on the road (taken with
// it), or a wall -- the ground higher than it stands.
static int PenguinHits(int i, S32 now, int driving, int *other) {
    const T_RACE_PENGUIN *pg = &s_penguins[i];
    const int fresh = now < pg->Dropped + RACE_PENGUIN_ARM;
    *other = -1;
    const S32 lx = (S32)(pg->X - CurrentCubeX * 32768.0), lz = (S32)(pg->Z - CurrentCubeY * 32768.0);
    const int here = CubeMode == CUBE_EXTERIEUR && lx >= 0 && lx < 32768 && lz >= 0 && lz < 32768;
    if (driving && CubeMode == CUBE_EXTERIEUR && !(fresh && pg->Dropper < 0) && Near(&ListObjet[NUM_PERSO], lx, (S32)pg->Y, lz, RACE_PENGUIN_TRIGGER)) return 1;   // (the codes: what it ran into, for the log)
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
        const T_RACE_OPPONENT *o = &s_opp[n];
        if (!o->Count || !o->Scenes || (fresh && pg->Dropper == n)) continue;
        double x, z, y, v, dx, dz;
        PathAt(o, o->S, &x, &z, &y, &v, &dx, &dz);
        if ((x - pg->X) * (x - pg->X) + (z - pg->Z) * (z - pg->Z) < (double)RACE_PENGUIN_TRIGGER * RACE_PENGUIN_TRIGGER && fabs(y - pg->Y) < 900) return 2 + n;
    }
    for (int j = 0; j < RACE_MAX_PENGUINS; j++) {
        const T_RACE_PENGUIN *b = &s_penguins[j];
        if (j == i || !b->On || (pg->X - b->X) * (pg->X - b->X) + (pg->Z - b->Z) * (pg->Z - b->Z) >= (double)RACE_PENGUIN_BUMP * RACE_PENGUIN_BUMP || fabs(pg->Y - b->Y) > 900) continue;
        *other = j;
        return 10;
    }
    if (!here) return 0;
    for (int m = 0; m < s_nbMush; m++) {
        T_RACE_MUSHROOM *mu = &s_mush[m];
        const int a = MushroomActor(mu);
        if (a < 0 || mu->Back) continue;
        const double dx = ListObjet[a].Obj.X - lx, dz = ListObjet[a].Obj.Z - lz;
        if (dx * dx + dz * dz >= (double)RACE_PENGUIN_BUMP * RACE_PENGUIN_BUMP || fabs(mu->Y - pg->Y) > 900) continue;
        mu->Back = now + RACE_MUSHROOM_BACK;
        ListObjet[a].Obj.Y = -20000;
        return 11;
    }
    return CalculAltitudeTerrain(lx, lz, 0) > pg->Y + RACE_PENGUIN_WALL ? 12 : 0;
}

static void PenguinHitLog(int code) {
    if (code == 1) Log_Info("[racemod] a nitro penguin runs into Twinsen's car");
    else if (code >= 2 && code < 2 + RACE_MAX_OPPONENTS) Log_Info("[racemod] a nitro penguin runs into %s's car", s_opp[code - 2].Name);
    else if (code == 10) Log_Info("[racemod] two nitro penguins run into each other");
    else if (code == 11) Log_Info("[racemod] a nitro penguin runs into a mushroom");
    else if (code == 12) Log_Info("[racemod] a nitro penguin walks into a wall");
}

// Every frame: each penguin walking (along the first opponent's line, this way and that, a little to one side or the other; with no line,
// round where it was dropped), going off at whatever it runs into (or when its fuse is out), and shown by this scene's penguins, facing
// the way it walks.
static void Penguins(S32 now, int driving) {
    static S32 last = 0;
    S32 ms = last ? now - last : 0;
    last = now;
    if (ms < 0) ms = 0;
    if (ms > 100) ms = 100;
    const double dt = ms / 1000.0;
    static const T_RACE_OPPONENT none = {0};
    const T_RACE_OPPONENT *line = Guide() ? Guide() : &none;
    int k = 0;
    for (int i = 0; i < RACE_MAX_PENGUINS; i++) {
        T_RACE_PENGUIN *pg = &s_penguins[i];
        if (!pg->On) continue;
        int other;
        if (!s_penguinWalk) {
            const int hit = now >= pg->At ? 0 : PenguinHits(i, now, driving, &other);
            if (now >= pg->At || hit) {
                PenguinHitLog(hit);
                if (now < pg->At && other >= 0) PenguinOff(other, now);
                PenguinOff(i, now);
                continue;
            }
        } else {
            // a turn now and then: the other way, or another side
            if (now >= pg->NextTurn) {
                if (RaceRandom() < 0.5) pg->Dir = -pg->Dir;
                pg->SideTo = (RaceRandom() * 2 - 1) * RACE_PENGUIN_SIDE;
                pg->NextTurn = now + 2000 + (S32)(RaceRandom() * 3000);
                if (!line->Count) pg->Heading += (RaceRandom() * 2 - 1) * 2.0;
            }
            if (line->Count) {
                pg->S += pg->Dir * RACE_PENGUIN_WALK * dt;
                if (pg->S < 0) pg->S += line->Length;
                if (pg->S >= line->Length) pg->S -= line->Length;
                const double step = RACE_PENGUIN_WALK * 0.6 * dt;
                pg->Side += pg->SideTo > pg->Side + step ? step : pg->SideTo < pg->Side - step ? -step : pg->SideTo - pg->Side;
                double lx, lz, ly, v, dx, dz;
                PathAt(line, pg->S, &lx, &lz, &ly, &v, &dx, &dz);
                const double len = sqrt(dx * dx + dz * dz);
                if (len > 0) { pg->X = lx + dz / len * pg->Side; pg->Z = lz - dx / len * pg->Side; pg->Heading = atan2(dx * pg->Dir, dz * pg->Dir); }
                pg->Y = ly;
            } else {
                // (round where it was dropped: turned back towards it once it is too far)
                if ((pg->X - pg->HomeX) * (pg->X - pg->HomeX) + (pg->Z - pg->HomeZ) * (pg->Z - pg->HomeZ) > RACE_PENGUIN_HOME * RACE_PENGUIN_HOME)
                    pg->Heading = atan2(pg->HomeX - pg->X, pg->HomeZ - pg->Z);
                pg->X += sin(pg->Heading) * RACE_PENGUIN_WALK * dt;
                pg->Z += cos(pg->Heading) * RACE_PENGUIN_WALK * dt;
                // (on the ground as it walks, up a slope and down -- a step higher than RACE_PENGUIN_WALL is a wall: PenguinHits)
                const S32 gx = (S32)(pg->X - CurrentCubeX * 32768.0), gz = (S32)(pg->Z - CurrentCubeY * 32768.0);
                if (CubeMode == CUBE_EXTERIEUR && gx >= 0 && gx < 32768 && gz >= 0 && gz < 32768) {
                    const S32 ground = CalculAltitudeTerrain(gx, gz, 0);
                    if (ground <= pg->Y + RACE_PENGUIN_WALL) pg->Y = ground;
                }
            }
            // whatever it has run into
            if (const int hit = PenguinHits(i, now, driving, &other)) {
                PenguinHitLog(hit);
                if (other >= 0) PenguinOff(other, now);
                PenguinOff(i, now);
                continue;
            }
        }
        // shown by the next of this scene's penguins
        const S32 lx = (S32)(pg->X - CurrentCubeX * 32768.0), lz = (S32)(pg->Z - CurrentCubeY * 32768.0);
        if (lx < 0 || lx >= 32768 || lz < 0 || lz >= 32768) continue;
        for (; k < s_nbPenguinActors; k++) {
            if (s_penguinScene[k] != NumCube || s_penguinActor[k] <= 0 || s_penguinActor[k] >= NbObjets) continue;
            T_OBJET *pa = &ListObjet[s_penguinActor[k]];
            pa->Obj.X = lx; pa->Obj.Y = (S32)pg->Y; pa->Obj.Z = lz;
            pa->Obj.Beta = (S32)(pg->Heading * 4096.0 / (2 * 3.14159265358979)) & 4095;
            pa->XMin = pa->XMax = pa->ZMin = pa->ZMax = 0;
            k++;
            break;
        }
    }
    for (; k < s_nbPenguinActors; k++)
        if (s_penguinScene[k] == NumCube && s_penguinActor[k] > 0 && s_penguinActor[k] < NbObjets) ListObjet[s_penguinActor[k]].Obj.Y = -20000;
}

// OBJECT.CPP CheckObjCol: this scene's mushrooms, penguins and oil slicks are not in a car's way. Their boxes were only made points
// across (the engine stops a car at any object's box, and stands it on one it comes down on): a mushroom had to be gone before a car
// came within its box's reach, and a slick a protected car drives over -- left on the road for the next car -- was a wall, and lowered
// the car into Ascence's deck as it stood it on the slick's box. (Which actors they are, cached for the scene.)
static U8 s_noCollide[MAX_OBJETS];
static S32 s_noCollideScene = -1;
S32 RaceMod_NoCollide(S32 numobj) {
    // (the car lapping on its own, parade=: through Twinsen and his car alike -- it keeps to its line whatever is in its way)
    if (RaceMod_Active() && numobj > 0 && numobj == s_paradeActorNow) return 1;
    // (a gas monster: its bite is the race-track mode's own -- Monsters)
    if (RaceMod_Active() && numobj > 0)
        for (int i = 0; i < s_nbMonsters; i++)
            for (int k = 0; k < s_monsters[i].NbSeen; k++)
                if (s_monsters[i].SeenScene[k] == (S32)NumCube && s_monsters[i].SeenActor[k] == numobj) return 1;
    // (nothing runs into Twinsen's car on the super jet-pack either: a shuttle coming down onto it)
    if (numobj == NUM_PERSO && RaceMod_JetGhost()) return 1;
    if (!RaceMod_Active() || !s_powerups || numobj <= 0 || numobj >= MAX_OBJETS) return 0;
    // (a super jet-pack goes through the cars: Twinsen's through any in its way -- it pushes them aside, RACE_SHOVE_* -- and an
    // opponent's through Twinsen's; on the same line, the jet-pack sat stuck behind the car in front)
    if (s_carsScene == (S32)NumCube)
        for (int n = 0; n < RACE_MAX_OPPONENTS; n++)
            if (s_carActor[n] == numobj && (TimerRefHR < s_jetUntil || TimerRefHR < s_opp[n].JetUntil)) return 1;
    if (s_noCollideScene != (S32)NumCube) {
        memset(s_noCollide, 0, sizeof(s_noCollide));
        for (int i = 0; i < s_nbMush; i++) if (s_mush[i].Scene == NumCube && s_mush[i].Actor > 0 && s_mush[i].Actor < MAX_OBJETS) s_noCollide[s_mush[i].Actor] = 1;
        for (int i = 0; i < s_nbPenguinActors; i++) if (s_penguinScene[i] == NumCube && s_penguinActor[i] > 0 && s_penguinActor[i] < MAX_OBJETS) s_noCollide[s_penguinActor[i]] = 1;
        for (int i = 0; i < s_nbOilActors; i++) if (s_oilScene[i] == NumCube && s_oilActor[i] > 0 && s_oilActor[i] < MAX_OBJETS) s_noCollide[s_oilActor[i]] = 1;
        s_noCollideScene = NumCube;
    }
    return s_noCollide[numobj];
}

// EXTFUNC.CPP, OBJECT.CPP CheckObjCol: Twinsen's car on the super jet-pack goes through the island's objects and the scene's actors -- the
// user, 2026-10-10: "Sometimes on this track our super jetpack can get stuck on things such as the fence on the start/finish line that
// separates the track from the pit lane, or the side of one of the space shuttles that protrudes onto the track. The jet pack needs updating
// so it doesn't get stuck on objects" (it steers the car back onto its line from wherever it is: from the pit lane, along the fence). The
// road's own floor and rails, and the ground, still hold it.
S32 RaceMod_JetGhost(void) {
    return RaceMod_Active() && Comportement == C_BUGGY && TimerRefHR < s_jetUntil;
}

// BUGGY.CPP RaceSpeed: the golden Gazogem running -- from its first use till its time is up -- the car keeps whatever speed it has over
// the gear's top (each use's 10 km/h on the last's); after it, the extra is lost as any is (RACE_OVERSPEED_DRAG, the coast).
S32 RaceMod_NoTopSpeed(void) {
    return RaceMod_Active() && Comportement == C_BUGGY && s_goldUntil && TimerRefHR < s_goldUntil;
}

// EXTFUNC.CPP DoAnimExt: an opponent's car, placed where it is on the lap, not kept inside the player's cube.
S32 RaceMod_FreeOfCube(S32 numobj) {
    if (!RaceMod_Active() || numobj <= 0 || s_carsScene != (S32)NumCube) return 0;
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) if (s_carActor[n] == numobj) return 1;
    return 0;
}

// PERSO.CPP: Shift is the game's inventory key -- while Twinsen has an item, and until the key is let go after he used one, it doesn't
// open the inventory.
S32 RaceMod_BlocksInventory(void) {
    return RaceMod_Active() && Comportement == C_BUGGY && (s_item[0] >= 0 || s_item[1] >= 0 || s_shiftBlock);
}

// BUGGY.CPP RaceSpeed: how much faster or slower the player's car is than its setup (its top speed and pull): Gazogem fuel and the
// jet-pack, or shrunk by an opponent's lightning.
// A boost panel's lift on the car's top speed now: RACE_PANEL_KMH for RACE_PANEL_HOLD_MS after it was last on one, then eased back to its own.
static double PanelFactor(S32 now) {
    const S32 since = now - s_panelAt;
    if (since < 0 || since >= RACE_PANEL_HOLD_MS + RACE_PANEL_FADE_MS || s_car.Gears < 1) return 1;
    const double full = RACE_PANEL_SPEED / s_car.GearTop[s_car.Gears - 1];
    if (full <= 1) return 1;
    if (since < RACE_PANEL_HOLD_MS) return full;
    const double u = (double)(since - RACE_PANEL_HOLD_MS) / RACE_PANEL_FADE_MS;
    return full + (1 - full) * u * u * (3 - 2 * u);
}

double RaceMod_SpeedFactor(void) {
    if (!RaceMod_Active()) return 1;
    const S32 now = TimerRefHR;
    double f = 1;
    // (Gazogem fuel all race, fuel_always=: but the super jet-pack's own speed while it drives)
    if (now < s_boostUntil || (s_fuelAlways && now >= s_jetUntil)) f *= RACE_BOOST;
    else if (now < s_jetUntil) f *= JetFactor(s_jetUntil, s_jetFactor);
    // (a boost panel's, unless the rest lifts it more)
    const double panel = PanelFactor(now);
    if (panel > f) f = panel;
    if (now < s_shrunkUntil && now >= s_jetUntil) f *= RACE_SHRUNK;
    return f;
}

// BUGGY.CPP RaceSpeed: the jet-pack drives the car along the line at its factor times the line's own speed (RaceMod_AutoInput), and the
// line was planned for the car's own steering: through a hairpin at twice its speed the car turned on a circle twice as wide -- off the
// side of Polar Island's 108 terraces, where the road turns north at their west end, and over the island's edge into the sea (the user,
// 2026-10-06). So while it drives, the car steers as much quicker as the jet-pack makes it go: the same circles, at its speed.
double RaceMod_JetSteer(void) {
    if (!RaceMod_Takeover() || TimerRefHR >= s_jetUntil) return 1;
    return JetFactor(s_jetUntil, s_jetFactor);
}

// PERSO.CPP: the jet-pack has the controls (the player's own keys are left out while it lasts) -- when there is a line for it to drive
// by (with none it is only a boost: it had the keys and gave none back, and the car stood still until it ran out); skidding on oil too.
S32 RaceMod_Takeover(void) {
    return RaceMod_Active() && Comportement == C_BUGGY && ((TimerRefHR < s_jetUntil && Guide()) || TimerRefHR < s_skidUntil);
}

// BUGGY.CPP: (the skid steered and slid the car until 2026-10-04: now RaceMod_Skid turns it and runs it down itself)
double RaceMod_SkidSteer(void) { return 1; }
double RaceMod_SkidCoast(void) { return 1; }

// BUGGY.CPP MoveBuggy, after the car's speed: Twinsen's car skidding on oil -- its heading turned round once from where it pointed
// (quickly at first, slowing), its speed run down to a stop, both over RACE_SKID_MS; and (RaceMod_MoveBeta) it slides on the way it
// was going as it turns, not round in a circle.
void RaceMod_Skid(S32 *beta, S32 *speed) {
    if (!RaceMod_Active() || Comportement != C_BUGGY || TimerRefHR >= s_skidUntil) return;
    double p = (double)(TimerRefHR - s_skidStart) / RACE_SKID_MS;
    if (p < 0) p = 0; else if (p > 1) p = 1;
    *beta = (s_skidBeta + s_skidDir * (S32)(4096.0 * (1 - (1 - p) * (1 - p)))) & 4095;
    *speed = (S32)(s_skidSpeed * (1 - p) * (1 - p));
}
S32 RaceMod_MoveBeta(S32 beta) {
    return RaceMod_Active() && Comportement == C_BUGGY && TimerRefHR < s_skidUntil ? s_skidBeta : beta;
}

// Game variable RACE_GATE_VAR: 1 while one of the track's gates (gate=) is shut -- the story's start line until Twinsen has his gloves --
// so the scenes' scripts can tell a story's race from one started on its line, which has none (Mr. Paul stops a car only then:
// RaceTrackStory). 0 with no track, or none shut.
#define RACE_GATE_VAR 209
// Every frame: the pipes over the road near Twinsen (steam=, drip=). A puff of steam from a pipe's top every so often, rising and
// growing as it fades; a drop of oil falling from a pipe onto the road, and where it lands, a slick (DripOil; drawn and skidded on
// with the power-ups' oil, Slicks).
static double VentRandom(void) {
    s_ventSeed = s_ventSeed * 1103515245u + 12345u;
    return ((s_ventSeed >> 8) & 0xFFFFFF) / 16777216.0;
}
static void Vents(S32 now) {
    if (CubeMode != CUBE_EXTERIEUR || (!s_nbVents && !s_nbDrips)) return;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    const double hx = hero->Obj.X + cx, hz = hero->Obj.Z + cz;
    const double near2 = (double)RACE_VENT_NEAR * RACE_VENT_NEAR;
    int puffs = 0;
    for (int k = 0; k < RACE_MAX_PUFFS; k++) if (s_puffEnd[k] > now) puffs++;
    for (int i = 0; i < s_nbVents; i++) {
        T_RACE_VENT *p = &s_vents[i];
        if ((p->X - hx) * (p->X - hx) + (p->Z - hz) * (p->Z - hz) > near2) { p->Next = 0; continue; }
        // (each on its own time from when it comes in sight: never all of them at once)
        if (!p->Next) { p->Next = now + (S32)(VentRandom() * p->Every); continue; }
        if (now < p->Next) continue;
        p->Next = now + p->Every * 3 / 4 + (S32)(VentRandom() * p->Every / 2);
        if (puffs >= RACE_VENT_PUFFS) continue;
        // up and a little to one side, growing (a pof's scale is how far off it looks: down from 420 to 140, three times as big)
        const S32 life = 1500 + (S32)(VentRandom() * 600);
        const S32 alpha = 1024 + (S32)(VentRandom() * 240) - 120, beta = (S32)(VentRandom() * 4096);
        if (InitExtraPof((S32)(p->X - cx), (S32)p->Y, (S32)(p->Z - cz), 1, alpha, beta, 16, 0, 420, 140, life, 0, life) < 0) continue;
        for (int k = 0; k < RACE_MAX_PUFFS; k++) if (s_puffEnd[k] <= now) { s_puffEnd[k] = now + life; break; }
        puffs++;
    }
    // (the pipes drip wherever Twinsen is -- a slick may be lying under one when he comes to it -- the drop seen only near him)
    for (int i = 0; i < s_nbDrips; i++) {
        T_RACE_DRIP *p = &s_drips[i];
        if (p->LandAt) {
            // landed: a slick where it fell (with the power-ups' oil: their slicks show it)
            if (now < p->LandAt) continue;
            p->LandAt = 0;
            if (s_powerups && s_nbOilActors && DripOil(p->X, p->RoadY, p->Z, now)) Log_Info("[racemod] oil dripped onto the road at %.0f %.0f", p->X, p->Z);
            continue;
        }
        if (!p->Next) { p->Next = now + (S32)(VentRandom() * p->Every); continue; }
        if (now < p->Next) continue;
        p->Next = now + p->Every * 3 / 4 + (S32)(VentRandom() * p->Every / 2);
        // a drop falling from a standstill (weight 16: down t*t in t 20 ms ticks), gone when it reaches the road
        const S32 ms = (S32)(sqrt(p->Y - p->RoadY) * 20);
        p->LandAt = now + ms;
        if ((p->X - hx) * (p->X - hx) + (p->Z - hz) * (p->Z - hz) <= near2)
            InitExtraPof((S32)(p->X - cx), (S32)p->Y, (S32)(p->Z - cz), 1, 1024, 0, 0, 16, 2600, 2601, ms, 0, ms);
    }
}

// Every frame: the steam jets (jet=). Blowing: the factory's steam played over it every RACE_STEAM_PUFF ms (near Twinsen, in his scene),
// and a car in it hit -- Twinsen's as by a penguin (HitObj: the hit animation and the stars, the car stopped a moment; not with the
// protection spell or on the super jet-pack), an opponent's stopped with its own hit animation -- once a burst.
static void Jets(S32 now, int driving) {
    if (CubeMode != CUBE_EXTERIEUR || !s_nbJets) return;
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    const double hx = hero->Obj.X + cx, hz = hero->Obj.Z + cz;
    const double near2 = (double)RACE_VENT_NEAR * RACE_VENT_NEAR;
    for (int i = 0; i < s_nbJets; i++) {
        T_RACE_JET *j = &s_jets[i];
        const S32 cycle = j->On + j->Off;
        const S32 t = (S32)(((S64)now + j->Phase) % cycle);
        const S32 burst = (S32)(((S64)now + j->Phase) / cycle);
        if (t >= j->On) { j->Stage = 0; continue; }
        if (burst != j->Burst) { j->Burst = burst; j->HitOpp = 0; j->HitHero = 0; j->Puff = 0; j->Stage = 0; }
        const S32 lx = (S32)(j->X - cx), lz = (S32)(j->Z - cz);
        const int here = (j->X - hx) * (j->X - hx) + (j->Z - hz) * (j->Z - hz) <= near2;
        const int across = j->Half > 0;
        // (across the road: out of the left rail on the player's odd laps, the right on his even ones -- a burst keeps the side it began on)
        if (j->Stage == 0) j->Side = across && !(s_laps & 1) ? -1 : 1;
        const double leftX = j->DirZ, leftZ = -j->DirX;   // (the driver's left: x east, z south -- facing south, his left is east)
        const double nozX = j->X + leftX * j->Side * j->Half, nozZ = j->Z + leftZ * j->Side * j->Half, nozY = j->Y + j->High;
        if (here && now >= j->Puff) {
            j->Puff = now + RACE_STEAM_PUFF;
            if (!across) DoImpact(RACE_STEAM_IMPACT, lx, (S32)j->Y, lz, 255);
            else {
                // the factory's hiss (impact 38's) at the burst's start; its jet straight up (impact 37's sprites 100-102) laid on its
                // side out of the nozzle's mouth (OBJECT.CPP DrawTurnedExtra), as long as the band it hits -- growing at the burst's
                // start; its sparks, a flow on the level, blown across. (Impact 38's own jet, sprites 110-118, is drawn on a slant for
                // the factory's rooms' cameras and blows to the screen's left from its right edge: drawn the wrong way round, it blew
                // out of the left upright away from the road -- the user, with pictures, 2026-10-07.)
                if (j->Stage == 0) { ParmSampleVolume = 40; HQ_3D_MixSample(RACE_STEAM_SAMPLE, 4096, 1000, 1, (S32)(nozX - cx), (S32)nozY, (S32)(nozZ - cz)); }
                const double bx = -leftX * j->Side, bz = -leftZ * j->Side;   // (the way it blows)
                int puffs = 0;
                for (int q = 0; q < RACE_MAX_PUFFS; q++) if (s_puffEnd[q] > now) puffs++;
                if (puffs < RACE_MAX_PUFFS) {
                    const S32 life = j->Stage == 0 ? RACE_JET_GROW : RACE_STEAM_PUFF + 40;
                    const S32 scale = j->Reach > 0 ? RACE_JET_HIGH * 1000 / j->Reach : 70;
                    const S32 e = InitExtraAnimSprite(255, (S32)(nozX - cx), (S32)nozY, (S32)(nozZ - cz),
                                                      j->Stage == 0 ? RACE_JET_FIRST : RACE_JET_SPRITE, RACE_JET_SPRITE, life, scale, 1, 0);
                    if (e >= 0) {
                        ListExtra[e].Vx = (S16)(bx * 1000); ListExtra[e].Vy = 0; ListExtra[e].Vz = (S16)(bz * 1000);
                        ListExtra[e].Scale |= EXTRA_SCALE_TURNED;
                        for (int q = 0; q < RACE_MAX_PUFFS; q++) if (s_puffEnd[q] <= now) { s_puffEnd[q] = now + life; break; }
                    }
                }
                if (j->Stage % 2 == 0) {
                    const S32 beta = (S32)(atan2(bx, bz) * 4096.0 / (2 * 3.14159265358979)) & 4095;
                    T_FLOW *flow = TabPartFlow + RACE_STEAM_FLOW;
                    CreateParticleFlow(0, 255, 0, (S32)(nozX - cx), (S32)nozY, (S32)(nozZ - cz), (beta - flow->Beta) & 4095, RACE_STEAM_FLOW);
                }
            }
            j->Stage++;
        }
        // the cars in it: within Reach of it (up out of the deck), or (across) in the band from its rail Reach across the road
        const double dx = hero->Obj.X - lx, dz = hero->Obj.Z - lz;
        double along = 0, side = 0;
        if (across) { along = dx * j->DirX + dz * j->DirZ; side = (dx * leftX + dz * leftZ) * j->Side; }
        const int heroIn = across ? fabs(along) < RACE_STEAM_BAND && side > j->Half - j->Reach && side < j->Half + 300
                                  : dx * dx + dz * dz < (double)j->Reach * j->Reach;
        if (driving && !j->HitHero && heroIn && hero->Obj.Y > j->Y - 600 && hero->Obj.Y < j->Y + 1600) {
            j->HitHero = 1;
            if (now < s_protectUntil || now < s_jetUntil || (hero->Flags & NO_CHOC))
                Log_Info("[racemod] through the steam unharmed: %s", now < s_protectUntil ? "the protection spell" : now < s_jetUntil ? "the super jet-pack" : "he can't be hit");
            else {
                HitObj(PenguinHitter(), NUM_PERSO, RACE_STEAM_FORCE, -1);
                ListBuggy[0].Speed = 0;
                if (s_hitUntil < now + RACE_STEAM_HOLD) s_hitUntil = now + RACE_STEAM_HOLD;
                Log_Info("[racemod] Twinsen's car hit by a steam jet");
            }
        }
        // an opponent's
        for (int n = 0; n < RACE_MAX_OPPONENTS && n < 32; n++) {
            T_RACE_OPPONENT *o = &s_opp[n];
            if (!o->Count || (j->HitOpp & (1u << n)) || now < o->JetUntil || !RaceOn()) continue;   // (none waiting on its spot)
            double x, z, y, v, ddx, ddz;
            PathAt(o, o->S, &x, &z, &y, &v, &ddx, &ddz);
            if (fabs(y - j->Y) > 1600) continue;
            if (across) {
                const double ox = x - j->X, oz = z - j->Z, oa = ox * j->DirX + oz * j->DirZ, os = (ox * leftX + oz * leftZ) * j->Side;
                if (fabs(oa) >= RACE_STEAM_BAND || os <= j->Half - j->Reach || os >= j->Half + 300) continue;
            } else if ((x - j->X) * (x - j->X) + (z - j->Z) * (z - j->Z) >= (double)j->Reach * j->Reach) continue;
            j->HitOpp |= 1u << n;
            if (o->HitUntil < now + RACE_STEAM_HOLD) o->HitUntil = now + RACE_STEAM_HOLD;
            Log_Info("[racemod] %s hit by a steam jet", o->Name);
            const int index = OpponentActor(o);
            if (index < 0) continue;
            T_OBJET *car = &ListObjet[index];
            InitAnim(GEN_ANIM_CHOC, ANIM_ALL_THEN, (U8)index);
            InitHitExtraPof(car->Obj.X, car->Obj.Y + car->YMax + 1, car->Obj.Z, RACE_STEAM_FORCE);
        }
    }
}

// The game's lava at (x, y, z), cube-local: the mine's burst (impact 25) or its fall's spit (`spit`: impact 23's flow, thrown the way
// `beta` says), its sparks and, when there are extras to spare, its little balls -- those only to see: the burst's own reach is what stops
// a car (they bounce about the deck, and an opponent's car hit by one would be hurt as an actor is, not stopped as a car is).
static void GameLava(S32 x, S32 y, S32 z, int spit, S32 beta, int balls) {
    U8 before[MAX_EXTRAS];
    int free = 0;
    for (int i = 0; i < MAX_EXTRAS; i++) { before[i] = ListExtra[i].Sprite != -1; free += !before[i]; }
    balls = balls && free >= RACE_LAVA_FREE;
    if (!spit) {
        if (balls) DoImpact(RACE_LAVA_BURST, x, y, z, 255);
        else CreateParticleFlow(0, 255, 0, x, y, z, 0, RACE_LAVA_BURST_FLOW);
    } else {
        const S32 b = (beta - (TabPartFlow + RACE_LAVA_SPIT_FLOW)->Beta) & 4095;
        CreateParticleFlow(0, 255, 0, x, y, z, b, RACE_LAVA_SPIT_FLOW);
        if (balls) CreateExtraParticleFlow(FLOW_EXTRA_OBJ, 255, RACE_LAVA_MODEL, 0, x, y, z, b, RACE_LAVA_SPIT_FLOW, 10, 0, 0, 0);
    }
    for (int i = 0; i < MAX_EXTRAS; i++)
        if (!before[i] && ListExtra[i].Sprite == (S16)(3 + 32768)) ListExtra[i].Flags &= ~(EXTRA_END_OBJ | EXTRA_HIT_ANY);
}

static int LaserExtra(int model, S32 x, S32 y, S32 z);

// Every frame: the lava balls (lavaball=). A ball in the air is the game's lava ball tumbling along its arc, and the shadow at the place it
// will land; landed, the game's lava burst and every car in its reach stopped -- Twinsen's as by a penguin's blast unless the protection
// spell or the super jet-pack is on, an opponent's with its hit animation.
static void LavaBalls(S32 now, int driving) {
    if (CubeMode != CUBE_EXTERIEUR || !s_nbLava || !s_nbRaised) return;
    // (the lava ball's faces are the objects' texture page's fire, black until something animates it -- the scene does only with animated
    // textures of its own or the protection spell on: the balls flew black, 2026-10-10)
    if (!AnimateTexture) DoTextureAnimation();
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    const double hx = hero->Obj.X + cx, hz = hero->Obj.Z + cz;
    const int n = s_raisedLoop ? s_nbRaised - 1 : s_nbRaised - 1;
    for (int i = 0; i < s_nbLava; i++) {
        T_RACE_LAVA *b = &s_lava[i];
        if (!b->On) {
            if (!b->Next) { b->Next = now + b->Phase % b->Every; continue; }
            if (now < b->Next) continue;
            b->Next = now + b->Every * 3 / 4 + (S32)(VentRandom() * b->Every / 2);
            // a place along its stretch of the road, on the deck (not over a jump's gap), within its rails
            const int span = ((b->To - b->From) % n + n) % n;
            int k = -1;
            for (int tries = 0; tries < 6 && k < 0; tries++) {
                const int c = (b->From + (int)(VentRandom() * (span + 1))) % n;
                if (s_raised[c].Half > 0 && s_raised[c + 1].Half > 0) k = c;
            }
            if (k < 0) continue;
            const T_RACE_RAISED *a = &s_raised[k], *c = a + 1;
            const double t = VentRandom(), dx = c->X - a->X, dz = c->Z - a->Z, len = sqrt(dx * dx + dz * dz);
            if (len < 1) continue;
            const double half = (a->Half + (c->Half - a->Half) * t) * 0.6, side = (VentRandom() * 2 - 1) * half;
            b->DstX = a->X + dx * t - dz / len * side; b->DstZ = a->Z + dz * t + dx / len * side; b->DstY = a->Y + (c->Y - a->Y) * t;
            // (only near Twinsen, and landing in his scene: the extras are the scene's)
            if ((b->DstX - hx) * (b->DstX - hx) + (b->DstZ - hz) * (b->DstZ - hz) > (double)RACE_LAVA_NEAR * RACE_LAVA_NEAR) continue;
            if (b->DstX < cx || b->DstX >= cx + 32768 || b->DstZ < cz || b->DstZ >= cz + 32768) continue;
            const double far = sqrt((b->DstX - b->SrcX) * (b->DstX - b->SrcX) + (b->DstZ - b->SrcZ) * (b->DstZ - b->SrcZ));
            b->Dur = 1500 + (S32)(far / 10);
            b->T0 = now; b->On = 1; b->Trail = now;
            b->Ball = LaserExtra(s_lavaModel >= 0 ? s_lavaModel : RACE_LAVA_MODEL, (S32)(b->SrcX - cx), (S32)b->SrcY, (S32)(b->SrcZ - cz));
            b->Shadow = InitExtraAnimSprite(255, (S32)(b->DstX - cx), (S32)b->DstY + 40, (S32)(b->DstZ - cz), RACE_LAVA_SHADOW, RACE_LAVA_SHADOW, b->Dur + 100, 60, 1, 0);
            if (b->Shadow >= 0) ListExtra[b->Shadow].Flags |= EXTRA_NO_SHADOW;
            // (out of the lava: the game's burst where it leaves it -- in Twinsen's cube: the little balls fly by its ground)
            if (b->SrcX >= cx && b->SrcX < cx + 32768 && b->SrcZ >= cz && b->SrcZ < cz + 32768)
                GameLava((S32)(b->SrcX - cx), (S32)b->SrcY, (S32)(b->SrcZ - cz), 0, 0, 1);
            continue;
        }
        const double u = (double)(now - b->T0) / b->Dur;
        if (u < 1) {
            // along its arc: up out of the lava, over and down onto the road
            const double far = sqrt((b->DstX - b->SrcX) * (b->DstX - b->SrcX) + (b->DstZ - b->SrcZ) * (b->DstZ - b->SrcZ));
            const double top = (b->SrcY > b->DstY ? b->SrcY : b->DstY) - (b->SrcY + (b->DstY - b->SrcY) * 0.5) + (far * 0.35 > 2500 ? far * 0.35 : 2500);
            const double x = b->SrcX + (b->DstX - b->SrcX) * u, z = b->SrcZ + (b->DstZ - b->SrcZ) * u, y = b->SrcY + (b->DstY + 250 - b->SrcY) * u + 4 * top * u * (1 - u);
            if (b->Ball >= 0 && ListExtra[b->Ball].Sprite == (S16)(3 + 32768)) {
                T_EXTRA *e = &ListExtra[b->Ball];
                e->PosX = (S32)(x - cx); e->PosY = (S32)y; e->PosZ = (S32)(z - cz);
                // (tumbling as it flies)
                e->Beta = (S16)(((now - b->T0) * 3) & 4095); e->Scale = ((now - b->T0) * 2) & 4095;
            }
            if (b->Shadow >= 0 && ListExtra[b->Shadow].Sprite == RACE_LAVA_SHADOW) ListExtra[b->Shadow].Scale = (S32)(60 - (60 - RACE_LAVA_SCALE) * u);
            continue;
        }
        // landed: its burst, and the cars in it
        b->On = 0;
        if (b->Ball >= 0 && ListExtra[b->Ball].Sprite == (S16)(3 + 32768)) ListExtra[b->Ball].Sprite = -1;
        if (b->Shadow >= 0 && ListExtra[b->Shadow].Sprite == RACE_LAVA_SHADOW) ListExtra[b->Shadow].Sprite = -1;
        b->Ball = b->Shadow = -1;
        const S32 lx = (S32)(b->DstX - cx), lz = (S32)(b->DstZ - cz), ly = (S32)b->DstY;
        if (lx < 0 || lx >= 32768 || lz < 0 || lz >= 32768) continue;
        GameLava(lx, ly, lz, 0, 0, 1);
        if (driving && Near(hero, lx, ly, lz, RACE_LAVA_REACH)) {
            if (now < s_protectUntil || now < s_jetUntil || (hero->Flags & NO_CHOC))
                Log_Info("[racemod] a lava ball lands on Twinsen's car: %s", now < s_protectUntil ? "the protection spell holds" : now < s_jetUntil ? "the super jet-pack" : "he can't be hit");
            else {
                HitObj(PenguinHitter(), NUM_PERSO, RACE_LAVA_FORCE, -1);
                ListBuggy[0].Speed = 0;
                if (s_hitUntil < now + RACE_PENGUIN_HOLD) s_hitUntil = now + RACE_PENGUIN_HOLD;
                Log_Info("[racemod] Twinsen's car hit by a lava ball");
            }
        }
        for (int o = 0; o < RACE_MAX_OPPONENTS; o++) {
            T_RACE_OPPONENT *op = &s_opp[o];
            if (!op->Count || !RaceOn()) continue;
            double x, z, y, v, ddx, ddz;
            PathAt(op, op->S, &x, &z, &y, &v, &ddx, &ddz);
            if ((x - b->DstX) * (x - b->DstX) + (z - b->DstZ) * (z - b->DstZ) > (double)RACE_LAVA_REACH * RACE_LAVA_REACH || fabs(y - b->DstY) > 1500) continue;
            if (op->HitUntil < now + RACE_PENGUIN_HOLD) op->HitUntil = now + RACE_PENGUIN_HOLD;
            Log_Info("[racemod] %s hit by a lava ball", op->Name);
            const int index = OpponentActor(op);
            if (index < 0) continue;
            T_OBJET *car = &ListObjet[index];
            InitAnim(GEN_ANIM_CHOC, ANIM_ALL_THEN, (U8)index);
            InitHitExtraPof(car->Obj.X, car->Obj.Y + car->YMax + 1, car->Obj.Z, RACE_LAVA_FORCE);
        }
    }
}

// Every frame: the lava falls (lavafall=), near Twinsen -- sparks off a lip and down the face below it, at a height down it each time (a
// flow's sparks die RACE_FALL_DROP under where they start), and the game's spit out of a foot.
static void LavaFalls(S32 now) {
    if (CubeMode != CUBE_EXTERIEUR || !s_nbFalls) return;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0, hx = hero->Obj.X + cx, hz = hero->Obj.Z + cz;
    for (int pass = 0; pass < 2; pass++) {
        S32 *next = pass ? &s_fallFootNext : &s_fallLipNext;
        const S32 every = pass ? RACE_FALL_FOOT_MS : RACE_FALL_LIP_MS;
        if (now < *next) continue;
        *next = now + every / 2 + (S32)(VentRandom() * every);
        const T_RACE_FALL *f = &s_falls[(int)(VentRandom() * s_nbFalls) % s_nbFalls];
        const double x = pass ? f->FootX : f->LipX, z = pass ? f->FootZ : f->LipZ;
        if ((x - hx) * (x - hx) + (z - hz) * (z - hz) > (double)RACE_FALL_NEAR * RACE_FALL_NEAR) continue;
        if (x < cx || x >= cx + 32768 || z < cz || z >= cz + 32768) continue;
        if (pass) GameLava((S32)(x - cx), (S32)f->FootY, (S32)(z - cz), 1, f->Beta, 1);
        else {
            // (out from the face a little, at a height down it)
            const double b = f->Beta * 2 * 3.14159265358979 / 4096, drop = (f->LipY - f->FootY) * VentRandom();
            GameLava((S32)(x - cx + sin(b) * 160), (S32)(f->LipY - drop), (S32)(z - cz + cos(b) * 160), 1, f->Beta, 0);
        }
    }
}

// Every frame: the boost panels (boostpanel=) under Twinsen's car -- two of its wheels or more on one, and it is going at RACE_PANEL_KMH at
// least (going forwards).
static void BoostPanels(S32 now, int driving) {
    if (CubeMode != CUBE_EXTERIEUR || !s_nbPanels) return;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    for (int i = 0; i < s_nbPanels; i++) {
        T_RACE_PANEL *p = &s_panels[i];
        int on = 0;
        if (driving && fabs(hero->Obj.Y - p->Y) <= RACE_PANEL_ABOVE)
            for (int w = 0; w < 4; w++) {
                const double dx = ListBuggy[0].Wheel[w].X + cx - p->X, dz = ListBuggy[0].Wheel[w].Z + cz - p->Z;
                if (fabs(dx * p->Dx + dz * p->Dz) <= p->HalfLen && fabs(-dx * p->Dz + dz * p->Dx) <= p->HalfWidth) on++;
            }
        if (on >= 2 && ListBuggy[0].Speed >= 0) {
            if (ListBuggy[0].Speed < RACE_PANEL_SPEED) ListBuggy[0].Speed = (S32)RACE_PANEL_SPEED;
            if (!p->OnHero) Log_Info("[racemod] boost panel %d: %d of the car's wheels on it -- %d km/h", i + 1, on, (int)((ListBuggy[0].Speed * 36 + 2560) / 5120));
            p->OnHero = 1; s_panelAt = now;
        } else p->OnHero = 0;
    }
}

// FICHE.CPP's throws (F_THROW_3D): a machine gunner's gun plays its firing animation for its kick and its rattle, the bullets it would
// throw left out -- the race-track mode flies its own, onto its targets (Gunners). Non-zero: leave this throw out.
S32 RaceMod_SilentThrow(S32 numobj) {
    if (!RaceMod_Active()) return 0;
    for (int i = 0; i < s_nbGunners; i++)
        if (s_gunners[i].Scene == (S32)NumCube && s_gunners[i].Gun == numobj) return 1;
    return 0;
}

// A bullet of a gunner's burst: from its muzzle to a place among its targets (a point in the disc round one), the gun and its Franco
// turned to it; where the gunner's scene is the one drawn, the game's bullet thrown so that it comes down there -- its height the lower
// of the two that do, at its speed and weight (flying in frames of 20 ms: its way v a frame, falling weight/16 a frame a frame).
static void GunnerShot(T_RACE_GUNNER *g, S32 now) {
    if (!g->NbTargets) return;
    int b = -1;
    for (int i = 0; i < RACE_MAX_BULLETS && b < 0; i++) if (!s_bullets[i].On) b = i;
    if (b < 0) return;
    T_RACE_BULLET *u = &s_bullets[b];
    const int k = (int)(VentRandom() * g->NbTargets) % g->NbTargets;
    const double a = VentRandom() * 2 * 3.14159265358979, r = sqrt(VentRandom()) * g->TR[k];
    u->On = 1; u->T0 = now; u->Extra = -1; u->Owner = g->Gun > 0 ? g->Gun : 255;
    u->FromX = g->MX; u->FromY = g->MY; u->FromZ = g->MZ;
    u->ToX = g->TX[k] + cos(a) * r; u->ToZ = g->TZ[k] + sin(a) * r; u->ToY = g->TY[k] + 60;
    // (its height: tan h from c R^2/v^2 T^2 - R T + (dy + c R^2/v^2) = 0, the lower root; out of reach, straight at it)
    const double R = sqrt((u->ToX - u->FromX) * (u->ToX - u->FromX) + (u->ToZ - u->FromZ) * (u->ToZ - u->FromZ)), dy = u->ToY - u->FromY;
    const double v = RACE_GUN_SPEED, c = RACE_GUN_WEIGHT / 16.0, q = c * R * R / (v * v);
    const double disc = R * R - 4 * q * (dy + q);
    const double h = q > 0 && disc >= 0 ? atan((R - sqrt(disc)) / (2 * q)) : atan2(dy, R);
    u->Dur = (S32)(R / (v * cos(h)) * 20);
    if (g->Scene != (S32)NumCube || CubeMode != CUBE_EXTERIEUR) return;
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    const S32 beta = (S32)(atan2(u->ToX - g->MX, u->ToZ - g->MZ) * 4096 / (2 * 3.14159265358979)) & 4095;
    if (g->Gun > 0 && g->Gun < NbObjets) ListObjet[g->Gun].Obj.Beta = beta;
    if (g->Franco > 0 && g->Franco < NbObjets) ListObjet[g->Franco].Obj.Beta = beta;
    const S32 alpha = (S32)floor(h * 4096 / (2 * 3.14159265358979) + 0.5) & 4095;
    // (thrown as the gun throws its own; its hits are the race-track mode's, where it lands, so it does no harm of the game's and nothing
    // stops it on the way -- an actor, a rail)
    u->Extra = ThrowExtra((U8)(g->Gun > 0 ? g->Gun : 255), (S32)(u->FromX - cx), (S32)u->FromY, (S32)(u->FromZ - cz), RACE_GUN_SPRITE, alpha, beta, RACE_GUN_SPEED, RACE_GUN_WEIGHT, 0);
    if (u->Extra >= 0) ListExtra[u->Extra].Flags &= ~(EXTRA_END_OBJ | EXTRA_END_COL | EXTRA_IMPACT);
}

// Every frame: the machine gunners (gunner=). Between bursts the gun stands still, its Franco sitting at it; a burst every RACE_GUN_EVERY
// ms or so while the race is on (a quarter either way), its bullets flown, and where each lands, the cars there hit.
static void Gunners(S32 now, int driving) {
    if (!s_nbGunners) return;
    for (int i = 0; i < s_nbGunners; i++) {
        T_RACE_GUNNER *g = &s_gunners[i];
        const int here = g->Scene == (S32)NumCube && CubeMode == CUBE_EXTERIEUR;
        if (!here) g->Ready = 0;
        else if (!g->Ready) {
            // (their own scripts stopped -- the builder emptied them: the guns' put them back by the road every frame)
            if (g->Gun > 0 && g->Gun < NbObjets) { ListObjet[g->Gun].OffsetLife = -1; ListObjet[g->Gun].OffsetTrack = -1; InitAnim(GEN_ANIM_RIEN, ANIM_REPEAT, (U8)g->Gun); }
            if (g->Franco > 0 && g->Franco < NbObjets) { ListObjet[g->Franco].OffsetLife = -1; ListObjet[g->Franco].OffsetTrack = -1; InitAnim(RACE_GUN_AT_IT, ANIM_REPEAT, (U8)g->Franco); }
            g->Ready = 1;
            Log_Info("[racemod] gunner: actor %d of scene %d at its gun (actor %d), %d places to fire at", (int)g->Franco, (int)g->Scene, (int)g->Gun, g->NbTargets);
        }
        if (!RaceRunning()) continue;
        if (!g->Next) { g->Next = now + RACE_GUN_EVERY / 2 + i * 900; continue; }
        if (!g->Firing && now >= g->Next) {
            g->Firing = 1; g->Fired = 0; g->BurstAt = now;
            if (here && g->Gun > 0 && g->Gun < NbObjets) InitAnim(RACE_GUN_FIRING, ANIM_REPEAT, (U8)g->Gun);
        }
        if (g->Firing) {
            while (g->Fired < RACE_GUN_SHOTS && now >= g->BurstAt + g->Fired * RACE_GUN_GAP) { GunnerShot(g, now); g->Fired++; }
            if (g->Fired >= RACE_GUN_SHOTS && now >= g->BurstAt + RACE_GUN_SHOTS * RACE_GUN_GAP) {
                g->Firing = 0;
                g->Next = now + RACE_GUN_EVERY * 3 / 4 + (S32)(VentRandom() * RACE_GUN_EVERY / 2);
                if (here && g->Gun > 0 && g->Gun < NbObjets) InitAnim(GEN_ANIM_RIEN, ANIM_REPEAT, (U8)g->Gun);
            }
        }
    }
    // the bullets: along their way, and where each lands
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    for (int b = 0; b < RACE_MAX_BULLETS; b++) {
        T_RACE_BULLET *u = &s_bullets[b];
        if (!u->On) continue;
        // (flown by the game, as its own are; taken away where it comes down)
        const int shown = u->Extra >= 0 && u->Extra < MAX_EXTRAS && ListExtra[u->Extra].Sprite == RACE_GUN_SPRITE && ListExtra[u->Extra].Owner == (U8)u->Owner;
        if (now - u->T0 < u->Dur) continue;
        u->On = 0;
        if (shown) ListExtra[u->Extra].Sprite = -1;
        const S32 lx = (S32)(u->ToX - cx), lz = (S32)(u->ToZ - cz);
        if (CubeMode == CUBE_EXTERIEUR && lx >= 0 && lx < 32768 && lz >= 0 && lz < 32768) InitHitExtraPof(lx, (S32)u->ToY, lz, RACE_SHOT_FORCE);
        for (int who = -1; who < RACE_MAX_OPPONENTS; who++) {
            if (who < 0 && !driving) continue;
            if (who >= 0 && (!s_opp[who].Count || !s_opp[who].Scenes || now < s_opp[who].HitUntil)) continue;
            double x, y, z;
            if (!CarAt(who, &x, &y, &z)) continue;
            if ((x - u->ToX) * (x - u->ToX) + (z - u->ToZ) * (z - u->ToZ) > (double)RACE_GUN_REACH * RACE_GUN_REACH || fabs(y - u->ToY) > 1400) continue;
            CarStruck(who, now, "machine gun", "the Franco gunner");
        }
    }
}

// The roulette wheel's decors as a cube's are drawn (DECORS.CPP DecorPoseHook): its head's turned, its ball's moved to where it rolls (its
// box with it: the engine leaves out a decor whose box is out of sight).
static void WheelDecors(T_DECORS *list, S32 count) {
    if (!s_wheel.On || !RaceMod_Active() || CubeMode != CUBE_EXTERIEUR) return;
    for (S32 i = 0; i < count; i++) {
        T_DECORS *d = &list[i];
        const int body = d->Body & 0xFFFF;
        if (s_wheel.BallShown && body >= s_wheel.Ball && body < s_wheel.Ball + s_wheel.NbBalls) {
            const int k = body - s_wheel.Ball;
            const S32 x = (S32)floor(s_wheel.BallX[k] - s_wheel.CubeX * 32768.0 + 0.5), y = (S32)floor(s_wheel.BallY[k] + 0.5), z = (S32)floor(s_wheel.BallZ[k] - s_wheel.CubeZ * 32768.0 + 0.5);
            d->XMin += x - d->Xworld; d->XMax += x - d->Xworld; d->ZMin += z - d->Zworld; d->ZMax += z - d->Zworld; d->YMin += y - d->Yworld;
            d->Xworld = x; d->Yworld = y; d->Zworld = z;
            continue;
        }
        for (int p = 0; p < s_wheel.NbParts; p++)
            if (body == s_wheel.Parts[p]) { d->Beta = (d->Beta & ~0xFFFF) | (s_wheel.Beta & 4095); break; }
    }
}

// The wheel's pockets round it from its body's x axis towards its z (RaceTrackRoulette): roulette_pockets= of them (19 on 2026-10-10: the
// user, "lose the numbers on the roulette wheel, half the segments, but double the width of the remaining ones. Red is win, black is lose,
// and green is super jackpot"; then 18, "drop a segment ... and make all other slots slightly wider"), the first green, then red and black
// in turn. A pocket's prize: 2 the green, 1 red, 0 black.
static int PocketPrize(int k) { return k == 0 ? 2 : k % 2 ? 1 : 0; }

// The slot machine the roulette wheel's red pockets spin (the user, 2026-10-10: "For red we spin three wheels at the top middle of the
// screen like a fruit machine, and they spin stopping one at a time from left to right displaying a powerup, let's make it so roughly 75%
// of the time the user wins a power up prize"): three reels of power-ups at the top of the screen, stopping left to right; three the same,
// that power-up won (RACE_SLOT_WIN of the time), into a free item slot -- or the one not selected.
#define RACE_SLOT_WIN 0.75
#define RACE_SLOT_STOP0 1200               // ms: the first reel stops ...
#define RACE_SLOT_STOP_GAP 700             // ... and each next this long after ...
#define RACE_SLOT_SHOW 1800                // ... and the reels stay this long after the last
#define RACE_SLOT_TURN 70                  // ms an item a reel turns over
static int s_slotReel[3], s_slotWin = 0, s_slotPaid = 0;
static S32 s_slotT0 = 0;

// An item into Twinsen's item box, shown at once (no roulette): a free slot, or the one not selected.
static void GiveItem(int pu, S32 now) {
    int slot = -1;
    for (int k = 0; k < RACE_ITEM_SLOTS && slot < 0; k++) if (s_item[k] < 0) slot = k;
    if (slot < 0) slot = s_sel ^ 1;
    if (s_item[slot] == PU_GOLD) s_goldUntil = 0;
    s_item[slot] = pu;
    s_itemAt[slot] = now;
    if (s_item[s_sel] < 0 || s_sel == slot) s_sel = slot;
    Log_Info("[racemod] Twinsen wins %s (slot %d)", s_puName[pu], slot + 1);
}

static void SlotSpin(S32 now) {
    s_slotOn = 1; s_slotT0 = now; s_slotPaid = 0;
    s_slotWin = RaceRandom() < RACE_SLOT_WIN;
    const int prize = RandomPowerUp(BackShare(-1));
    if (s_slotWin) s_slotReel[0] = s_slotReel[1] = s_slotReel[2] = prize;
    else {
        // (a miss: often the first two the same and the last not -- so near)
        s_slotReel[0] = prize;
        s_slotReel[1] = RaceRandom() < 0.6 ? prize : (prize + 1 + (int)(RaceRandom() * (PU_ROLLED - 1))) % PU_ROLLED;
        do s_slotReel[2] = (int)(RaceRandom() * PU_ROLLED) % PU_ROLLED; while (s_slotReel[2] == s_slotReel[0] && s_slotReel[1] == s_slotReel[0]);
    }
    Log_Info("[racemod] slot machine: %s %s %s", s_puName[s_slotReel[0]], s_puName[s_slotReel[1]], s_puName[s_slotReel[2]]);
}

// Every frame: the slot machine -- its prize paid as its last reel stops, gone when it has been shown.
static void SlotMachine(S32 now) {
    if (!s_slotOn) return;
    const S32 last = s_slotT0 + RACE_SLOT_STOP0 + 2 * RACE_SLOT_STOP_GAP;
    if (!s_slotPaid && now >= last) {
        s_slotPaid = 1;
        if (s_slotWin) { GiveItem(s_slotReel[0], now); Message("Winner!"); }
        else { Message("No luck"); Log_Info("[racemod] slot machine: no prize"); }
    }
    if (now >= last + RACE_SLOT_SHOW) s_slotOn = 0;
}

// What a reel shows: turning over until it stops, then its own.
static int SlotShown(int r, S32 now) {
    if (now >= s_slotT0 + RACE_SLOT_STOP0 + r * RACE_SLOT_STOP_GAP) return s_slotReel[r];
    return (int)((now - s_slotT0) / RACE_SLOT_TURN + r * 4) % PU_ROLLED;
}

// The turning floor's speed (radians a second, round from x towards z, as the world's angles go: its head's Beta turns the other way).
static double WheelOmega(void) { return -RACE_WHEEL_TURN * s_wheel.Spin * RACE_PI / 180.0; }

// Every frame: the roulette wheel (roulette=) -- its turn; its balls rolled down the bowl, carried round by its turn, bounced off its wall,
// the ridge round its hole, each other and the cars, and the cars they roll into knocked aside.
static void Wheel(S32 now, int driving) {
    T_RACE_ROULETTE *w = &s_wheel;
    if (!w->On) return;
    w->Beta = (S32)floor(fmod(now / 1000.0 * w->Spin * RACE_WHEEL_TURN, 360.0) * 4096 / 360.0) & 4095;
    const int nb = w->NbBalls < 1 ? 1 : w->NbBalls;
    const double om = WheelOmega(), inner = w->Hole + 2.0 * 512, outer = w->Rim - w->BallR;
    double dt = w->BallT ? (now - w->BallT) / 1000.0 : 0;
    w->BallT = now;
    if (dt < 0) dt = 0; else if (dt > 0.05) dt = 0.05;
    if (!w->BallsSet) {
        // (spread round the bowl half way down, rolling round one way and the other, a little down the slope)
        for (int k = 0; k < nb; k++) {
            const double a = 2 * RACE_PI * k / nb, r = (inner + outer) / 2, v = (k % 2 ? -1 : 1) * 4000.0;
            w->BallX[k] = w->X + r * cos(a); w->BallZ[k] = w->Z + r * sin(a);
            w->BallVX[k] = -sin(a) * v - cos(a) * 800; w->BallVZ[k] = cos(a) * v - sin(a) * 800;
        }
        w->BallsSet = 1;
    }
    for (int k = 0; k < nb; k++) {
        double dx = w->BallX[k] - w->X, dz = w->BallZ[k] - w->Z, r = sqrt(dx * dx + dz * dz);
        if (r < 1) { dx = 1; dz = 0; r = 1; }
        const double ux = dx / r, uz = dz / r, a = RACE_BALL_G * WheelSlopeAt(r);
        // (down the slope; brought towards the floor's own speed there)
        w->BallVX[k] -= ux * a * dt; w->BallVZ[k] -= uz * a * dt;
        const double fvx = -uz * om * r, fvz = ux * om * r;
        w->BallVX[k] += (fvx - w->BallVX[k]) * RACE_BALL_GRIP * dt; w->BallVZ[k] += (fvz - w->BallVZ[k]) * RACE_BALL_GRIP * dt;
        const double damp = 1 - RACE_BALL_DAMP * dt, sp = sqrt(w->BallVX[k] * w->BallVX[k] + w->BallVZ[k] * w->BallVZ[k]);
        const double cap = sp > RACE_BALL_TOP ? RACE_BALL_TOP / sp : 1;
        w->BallVX[k] *= damp * cap; w->BallVZ[k] *= damp * cap;
        w->BallX[k] += w->BallVX[k] * dt; w->BallZ[k] += w->BallVZ[k] * dt;
        // (off the wall, and kicked back up off the ridge round the hole)
        dx = w->BallX[k] - w->X; dz = w->BallZ[k] - w->Z; r = sqrt(dx * dx + dz * dz);
        if (r > 1 && (r > outer || r < inner)) {
            const double nx = dx / r, nz = dz / r, lim = r > outer ? outer : inner, vr = w->BallVX[k] * nx + w->BallVZ[k] * nz;
            w->BallX[k] = w->X + nx * lim; w->BallZ[k] = w->Z + nz * lim;
            const double e = r > outer ? RACE_BALL_BOUNCE : RACE_BALL_KICK;
            if ((r > outer && vr > 0) || (r < inner && vr < 0)) { w->BallVX[k] -= (1 + e) * vr * nx; w->BallVZ[k] -= (1 + e) * vr * nz; }
        }
    }
    // (off each other)
    for (int i2 = 0; i2 < nb; i2++)
        for (int j2 = i2 + 1; j2 < nb; j2++) {
            const double dx = w->BallX[j2] - w->BallX[i2], dz = w->BallZ[j2] - w->BallZ[i2], d = sqrt(dx * dx + dz * dz), touch = 2.0 * w->BallR;
            if (d >= touch || d < 1) continue;
            const double nx = dx / d, nz = dz / d, push = (touch - d) / 2;
            w->BallX[i2] -= nx * push; w->BallZ[i2] -= nz * push; w->BallX[j2] += nx * push; w->BallZ[j2] += nz * push;
            const double vi = w->BallVX[i2] * nx + w->BallVZ[i2] * nz, vj = w->BallVX[j2] * nx + w->BallVZ[j2] * nz;
            if (vi - vj <= 0) continue;
            const double e = (vi - vj) * (1 + RACE_BALL_BOUNCE) / 2;
            w->BallVX[i2] -= e * nx; w->BallVZ[i2] -= e * nz; w->BallVX[j2] += e * nx; w->BallVZ[j2] += e * nz;
        }
    for (int k = 0; k < nb; k++) w->BallY[k] = WheelFloorAt(WheelR(w->BallX[k], w->BallZ[k])) + w->BallR;
    w->BallShown = 1;
    // (the cars they roll into: knocked aside, the ball bounced off)
    for (int who = -1; who < RACE_MAX_OPPONENTS; who++) {
        if (who < 0 && !driving) continue;
        if (who >= 0 && (!s_opp[who].Count || !s_opp[who].Scenes)) continue;
        if (now < w->Again[who + 1]) continue;
        double x, y, z;
        if (!CarAt(who, &x, &y, &z)) continue;
        const double reach = w->BallR + RACE_BALL_REACH;
        for (int k = 0; k < nb; k++) {
            const double dx = x - w->BallX[k], dz = z - w->BallZ[k], d = sqrt(dx * dx + dz * dz);
            if (d > reach || fabs(y - (w->BallY[k] - w->BallR)) > 900) continue;
            const double nx = d > 1 ? dx / d : 1, nz = d > 1 ? dz / d : 0, vn = w->BallVX[k] * nx + w->BallVZ[k] * nz;
            if (vn > 0) { w->BallVX[k] -= (1 + RACE_BALL_BOUNCE) * vn * nx; w->BallVZ[k] -= (1 + RACE_BALL_BOUNCE) * vn * nz; }
            w->Again[who + 1] = now + RACE_BALL_AGAIN;
            if (who < 0) {
                w->KnockT0 = now; w->KnockX = nx; w->KnockZ = nz;
                ListBuggy[0].Speed = (S32)(ListBuggy[0].Speed * RACE_BALL_SLOW);
                Log_Info("[racemod] Twinsen's car knocked aside by a roulette ball");
            } else {
                T_RACE_OPPONENT *o = &s_opp[who];
                o->ShoveStart = now; o->ShoveUntil = now + RACE_SHOVE_MS; o->ShoveX = nx; o->ShoveZ = nz;
                o->Speed *= RACE_BALL_SLOW;
                Log_Info("[racemod] %s knocked aside by a roulette ball", o->Name);
            }
            break;
        }
    }
}

// For RaceMod_Loop, on Twinsen's car: in the roulette wheel's bowl (roulette=), drawn down it towards the hole -- as hard as it is steep --
// and kept inside its wall; into the hole, dropped through it onto the road under it (carried: non-zero, its place this frame in *nx..*nz).
static S32 WheelMove(S32 ox, S32 oy, S32 oz, S32 *nx, S32 *ny, S32 *nz) {
    T_RACE_ROULETTE *w = &s_wheel;
    if (!w->On || w->Landing < 0 || w->Landing + 1 >= s_nbRaised) return 0;
    T_OBJET *hero = &ListObjet[NUM_PERSO];
    T_OBJ_3D *obj = &hero->Obj;
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    const S32 now = TimerRefHR;
    double dt = (now - w->MoveT) / 1000.0;
    w->MoveT = now;
    if (dt < 0) dt = 0; else if (dt > 0.1) dt = 0.1;
    if (!w->DropOn) {
        if (!OnWheel(ox + cx, oy, oz + cz)) return 0;
        double tx = *nx + cx, tz = *nz + cz, tr = WheelR(tx, tz);
        if (tr >= w->Hole + 64) {
            // (drawn in, down the slope)
            const double pull = RACE_WHEEL_PULL * WheelSlopeAt(tr) * dt;
            if (tr > 1) { tx -= (tx - w->X) / tr * pull; tz -= (tz - w->Z) / tr * pull; }
            // (carried round by the turning floor, its heading with it)
            const double da = WheelOmega() * RACE_WHEEL_DRAG * dt, ca = cos(da), sa = sin(da), rx = tx - w->X, rz = tz - w->Z;
            tx = w->X + rx * ca - rz * sa; tz = w->Z + rx * sa + rz * ca;
            obj->Beta = (obj->Beta - (S32)floor(da * 4096 / (2 * RACE_PI) + 0.5)) & 4095;
            // (knocked aside by a ball: RACE_BALL_KNOCK in all, quickly at first)
            if (w->KnockT0 && now - w->KnockT0 < RACE_BALL_KNOCK_MS) {
                const double kt = (double)(now - w->KnockT0) / RACE_BALL_KNOCK_MS, v = 2 * RACE_BALL_KNOCK / (RACE_BALL_KNOCK_MS / 1000.0) * (1 - kt);
                tx += w->KnockX * v * dt; tz += w->KnockZ * v * dt;
            }
            // (and kept inside the wall, a car's half width in)
            tr = WheelR(tx, tz);
            const double room = w->Rim - RAISED_INSET;
            if (tr > room) { tx = w->X + (tx - w->X) * room / tr; tz = w->Z + (tz - w->Z) * room / tr; }
            *nx = (S32)floor(tx - cx + 0.5); *nz = (S32)floor(tz - cz + 0.5);
            return 0;
        }
        // (into the hole: the drop, from here by the hole's middle to the road under the wheel, coming in along it)
        const T_RACE_RAISED *l = &s_raised[w->Landing], *m = l + 1;
        double ux = m->X - l->X, uz = m->Z - l->Z;
        const double ul = sqrt(ux * ux + uz * uz);
        if (ul > 0) { ux /= ul; uz /= ul; } else { ux = 0; uz = 1; }
        w->D0x = tx; w->D0z = tz; w->D0y = WheelFloorAt(w->Hole);
        w->D1x = tx + (w->X - tx) * 0.7; w->D1z = tz + (w->Z - tz) * 0.7;
        w->DLx = l->X; w->DLy = l->Y; w->DLz = l->Z;
        w->D2x = w->DLx - ux * 1024; w->D2z = w->DLz - uz * 1024;
        w->DropBeta = (S32)floor(atan2(ux, uz) * 4096 / (2 * RACE_PI) + 0.5) & 4095;
        w->DropV = ListBuggy[0].Speed > RACE_WHEEL_DROP_LEAST ? ListBuggy[0].Speed : RACE_WHEEL_DROP_LEAST;
        const double length = sqrt((w->D1x - w->D0x) * (w->D1x - w->D0x) + (w->D1z - w->D0z) * (w->D1z - w->D0z))
                             + sqrt((w->D2x - w->D1x) * (w->D2x - w->D1x) + (w->D2z - w->D1z) * (w->D2z - w->D1z))
                             + sqrt((w->DLx - w->D2x) * (w->DLx - w->D2x) + (w->DLz - w->D2z) * (w->DLz - w->D2z));
        w->DropMs = (S32)(length / w->DropV * 1000);
        if (w->DropMs < RACE_WHEEL_DROP_MS) w->DropMs = RACE_WHEEL_DROP_MS;
        w->DropT0 = now; w->DropOn = 1;
        // (the pocket it goes in over: its angle on the turning head -- a body's point at angle a is drawn at a - Beta)
        double a = atan2(tz - w->Z, tx - w->X) + (w->Beta & 4095) * 2 * RACE_PI / 4096.0;
        a = fmod(a, 2 * RACE_PI); if (a < 0) a += 2 * RACE_PI;
        const int pockets = w->Pockets > 2 ? w->Pockets : 18;
        w->PrizeNumber = (int)(a / (2 * RACE_PI / pockets)) % pockets;
        w->Prize = s_prizeForce >= 0 && s_prizeForce <= 2 ? s_prizeForce : PocketPrize(w->PrizeNumber);
        Log_Info("[racemod] roulette wheel: Twinsen's car through its hole at (%.0f,%.0f) over %s (pocket %d), %d ms down onto the road", tx, tz,
                 w->Prize == 2 ? "the green" : w->Prize ? "red" : "black", w->PrizeNumber, (int)w->DropMs);
    }
    double t = (double)(now - w->DropT0) / w->DropMs;
    if (t > 1) t = 1;
    const double u = 1 - t, b0 = u * u * u, b1 = 3 * u * u * t, b2 = 3 * u * t * t, b3 = t * t * t;
    const double x = b0 * w->D0x + b1 * w->D1x + b2 * w->D2x + b3 * w->DLx, z = b0 * w->D0z + b1 * w->D1z + b2 * w->D2z + b3 * w->DLz;
    const double y = w->D0y + (w->DLy - w->D0y) * pow(t, 1.6);
    const double dx = 3 * u * u * (w->D1x - w->D0x) + 6 * u * t * (w->D2x - w->D1x) + 3 * t * t * (w->DLx - w->D2x);
    const double dz = 3 * u * u * (w->D1z - w->D0z) + 6 * u * t * (w->D2z - w->D1z) + 3 * t * t * (w->DLz - w->D2z);
    ListBuggy[0].Speed = (S32)w->DropV;
    if (t >= 1) {
        obj->Alpha = 0; obj->Gamma = 0; obj->Beta = w->DropBeta;
        w->DropOn = 0;
        s_forgetLap = 1;
        Log_Info("[racemod] roulette wheel: down on the road at (%.0f,%.0f,%.0f)", x, y, z);
        // (the pocket's prize: red the slot machine's spin, the green the golden Gazogem, black nothing; without power-ups, red and the
        // green a boost)
        if (w->Prize == 2) {
            if (s_powerups) {
                GiveItem(PU_GOLD, now);
                Message("Super jackpot! A golden Gazogem!");
            } else { s_boostUntil = now + RACE_WHEEL_ZERO_MS; Message("Super jackpot!"); }
        } else if (w->Prize == 1) {
            if (s_powerups) SlotSpin(now);
            else { s_boostUntil = now + RACE_WHEEL_RED_MS; Message("Red: a boost!"); }
        } else Message("Black: no luck");
        Log_Info("[racemod] roulette wheel: %s", w->Prize == 2 ? "the green: super jackpot" : w->Prize ? "red: the slot machine" : "black: nothing");
    } else {
        const S32 beta = dx * dx + dz * dz > 1 ? (S32)floor(atan2(dx, dz) * 4096 / (2 * RACE_PI) + 0.5) & 4095 : obj->Beta;
        CarPitch(obj, beta, 0.6 * sin(RACE_PI * t));
    }
    *nx = (S32)floor(x - cx + 0.5); *ny = (S32)floor(y + 0.5); *nz = (S32)floor(z - cz + 0.5);
    return 1;
}

// A gas monster's jaws on its place on the road: the cars there bitten (not one just bitten).
static void MonsterBites(const T_RACE_MONSTER *m, S32 now, int driving) {
    for (int who = -1; who < RACE_MAX_OPPONENTS; who++) {
        if (who < 0 && !driving) continue;
        if (who >= 0 && (!RaceOn() || !s_opp[who].Count || !s_opp[who].Scenes)) continue;
        double x, y, z;
        if (!CarAt(who, &x, &y, &z) || fabs(y - m->BiteY) > 900) continue;
        if ((x - m->BiteX) * (x - m->BiteX) + (z - m->BiteZ) * (z - m->BiteZ) > (double)RACE_MONSTER_BITE * RACE_MONSTER_BITE) continue;
        if (now < s_bittenUntil[who + 1]) continue;
        s_bittenUntil[who + 1] = now + RACE_MONSTER_STUN + RACE_MONSTER_GRACE;
        CarStruckFor(who, now, "gas monster's bite", NULL, RACE_MONSTER_STUN, "Bitten by a gas monster!");
    }
}

// Every frame: the gas monsters (gasmonster=) -- poised, striking now and then at random, biting; posed where their scene is the one about.
static void Monsters(S32 now, int driving) {
    if (!s_nbMonsters) return;
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    for (int i = 0; i < s_nbMonsters; i++) {
        T_RACE_MONSTER *m = &s_monsters[i];
        const S32 t = now - m->T0;
        int anim = m->Anim0;
        switch (m->State) {
        case MONSTER_POISED:
            // (the first strike any time in the first two waits, so that they don't strike together)
            if (!m->Next) m->Next = now + (S32)(VentRandom() * 2 * RACE_MONSTER_EVERY);
            if (now >= m->Next) { m->State = MONSTER_STRIKE; m->T0 = now; anim = m->Anim0 + 1; }
            break;
        case MONSTER_STRIKE:
            anim = m->Anim0 + 1;
            if (t >= RACE_MONSTER_HIT) {
                // (its head down on its place: the cars there bitten)
                m->State = MONSTER_BITE; m->T0 = now; anim = m->Anim0 + 2;
                MonsterBites(m, now, driving);
            }
            break;
        case MONSTER_BITE:
            anim = m->Anim0 + 2;
            // (and those driving into its jaws while they snap shut and hold)
            if (t < RACE_MONSTER_HOLD) MonsterBites(m, now, driving);
            if (t >= RACE_MONSTER_BACK) {
                m->State = MONSTER_POISED; anim = m->Anim0;
                m->Next = now + RACE_MONSTER_EVERY + (S32)(VentRandom() * RACE_MONSTER_EVERY);
            }
            break;
        }
        // posed where it has an actor in the scene about -- its own, or one it is seen from: standing at its place out of the gas (in that
        // scene's cube's terms, past its edge for one of the next cube's), facing its place on the road
        int actor = -1;
        if (CubeMode == CUBE_EXTERIEUR)
            for (int k = 0; k < m->NbSeen && actor < 0; k++)
                if (m->SeenScene[k] == (S32)NumCube && m->SeenActor[k] > 0 && m->SeenActor[k] < NbObjets) actor = m->SeenActor[k];
        if (actor < 0) { m->Shown = -1; continue; }
        T_OBJET *monster = &ListObjet[actor];
        {
            const T_OBJ_3D *car = &ListObjet[NUM_PERSO].Obj;
            const double hx = car->X + cx, hz = car->Z + cz;
            if ((hx - m->RiseX) * (hx - m->RiseX) + (hz - m->RiseZ) * (hz - m->RiseZ) > RACE_MONSTER_SEEN_FAR * RACE_MONSTER_SEEN_FAR
                    || s_wheel.DropOn || OnWheel(hx, car->Y, hz)) {
                monster->Obj.Y = -20000;
                continue;
            }
        }
        if (m->Shown < 0 || m->ShownScene != (S32)NumCube) {
            // (its own scripts stopped: it is the race-track mode's; its own body)
            monster->OffsetLife = monster->OffsetTrack = -1;
            InitBody((U8)m->Body, (U8)actor);
            m->Shown = 0; m->ShownScene = (S32)NumCube; m->Anim = -1;
        }
        const double fx = m->BiteX - m->RiseX, fz = m->BiteZ - m->RiseZ;
        const S32 beta = (S32)floor(atan2(fx, fz) * 4096 / (2 * RACE_PI) + 0.5) & 4095;
        monster->Obj.X = (S32)(m->RiseX - cx); monster->Obj.Y = (S32)m->FootY; monster->Obj.Z = (S32)(m->RiseZ - cz);
        CarPitch(&monster->Obj, beta, 0);
        if (anim != m->Anim) { InitAnim((U16)anim, ANIM_REPEAT, (U8)actor); m->Anim = anim; }
    }
}

// Every frame: the shuttles (shuttle=) of this scene, flown round their cycle. Its legs (the game's bodies: 58 down, 68 in, 64 60 65 61 66
// 62 67 63 between, as its own take-off and landing scripts change them) come out on the way down and go in on the way up; its thrust
// (body 59, every other fifth of a second against 58, as its scripts flicker it) while it hovers, touches down and lifts off.
static const S32 s_legsOut[] = { 68, 63, 67, 62, 66, 61, 65, 60, 64, 58 };
static double Ease(double u) { u = u < 0 ? 0 : u > 1 ? 1 : u; return u * u * (3 - 2 * u); }
static void Shuttles(S32 now, int driving) {
    for (int i = 0; i < s_nbShuttles; i++) {
        T_RACE_SHUTTLE *sh = &s_shuttles[i];
        if (sh->Scene != (S32)NumCube || sh->Actor >= NbObjets) { sh->Ready = 0; continue; }
        T_OBJET *ship = &ListObjet[sh->Actor];
        if (!sh->Ready) {
            // its own scripts stopped, standing still: flown from here on
            ship->OffsetLife = -1; ship->OffsetTrack = -1;
            InitAnim(GEN_ANIM_RIEN, ANIM_REPEAT, (U8)sh->Actor);
            sh->Ready = 1;
            Log_Info("[racemod] shuttle: actor %d of scene %d flown over its pad (%d,%d,%d)", (int)sh->Actor, (int)sh->Scene, (int)sh->X, (int)sh->Y, (int)sh->Z);
        }
        const S32 cycle = RACE_SHUTTLE_AWAY + RACE_SHUTTLE_DOWN + RACE_SHUTTLE_HOVER + RACE_SHUTTLE_TOUCH + RACE_SHUTTLE_LANDED + RACE_SHUTTLE_RISE;
        S32 t = (now + i * cycle / s_nbShuttles) % cycle;      // (several take turns)
        double y;
        S32 body = 58;
        const int thrust = (now / 200) & 1;
        if (t < RACE_SHUTTLE_AWAY) {
            ship->Flags |= INVISIBLE | NO_SHADOW;
            ship->Obj.X = sh->X; ship->Obj.Y = sh->Y + RACE_SHUTTLE_HIGH; ship->Obj.Z = sh->Z;
            continue;
        }
        ship->Flags &= ~(INVISIBLE | NO_SHADOW);
        if ((t -= RACE_SHUTTLE_AWAY) < RACE_SHUTTLE_DOWN) {
            // down out of the sky, slowing to the hover; its legs out over the second half
            const double u = (double)t / RACE_SHUTTLE_DOWN, k = 1 - (1 - u) * (1 - u);
            y = RACE_SHUTTLE_HIGH + (RACE_SHUTTLE_HOVERY - RACE_SHUTTLE_HIGH) * k;
            const int leg = u < 0.5 ? 0 : (int)((u - 0.5) * 2 * 10);
            body = s_legsOut[leg > 9 ? 9 : leg];
        } else if ((t -= RACE_SHUTTLE_DOWN) < RACE_SHUTTLE_HOVER) {
            y = RACE_SHUTTLE_HOVERY + 90 * sin(t * 2 * 3.14159265 / 1800.0);
            body = thrust ? 59 : 58;
        } else if ((t -= RACE_SHUTTLE_HOVER) < RACE_SHUTTLE_TOUCH) {
            y = RACE_SHUTTLE_HOVERY * (1 - Ease((double)t / RACE_SHUTTLE_TOUCH));
            body = thrust ? 59 : 58;
        } else if ((t -= RACE_SHUTTLE_TOUCH) < RACE_SHUTTLE_LANDED) {
            y = 0;
            body = 58;
        } else {
            // straight up, faster and faster; its legs in over the middle of it
            t -= RACE_SHUTTLE_LANDED;
            const double u = (double)t / RACE_SHUTTLE_RISE;
            y = RACE_SHUTTLE_HIGH * u * u;
            if (u < 0.25) body = thrust ? 59 : 58;
            else { const int leg = (int)((u - 0.25) / 0.5 * 10); body = s_legsOut[9 - (leg > 9 ? 9 : leg)]; }
        }
        ship->Obj.X = sh->X; ship->Obj.Y = sh->Y + (S32)y; ship->Obj.Z = sh->Z;
        if (ship->GenBody != body) InitBody((U8)body, (U8)sh->Actor);
        if (y >= RACE_SHUTTLE_LOW || now < sh->Hit) continue;
        // down: the cars that run into it
        T_OBJET *hero = &ListObjet[NUM_PERSO];
        if (driving && Near(hero, sh->X, sh->Y, sh->Z, RACE_SHUTTLE_REACH)) {
            sh->Hit = now + RACE_PENGUIN_HOLD;
            if (now < s_protectUntil || now < s_jetUntil || (hero->Flags & NO_CHOC))
                Log_Info("[racemod] Twinsen's car runs into the shuttle: %s", now < s_protectUntil ? "the protection spell holds" : now < s_jetUntil ? "the super jet-pack" : "he can't be hit");
            else {
                HitObj(PenguinHitter(), NUM_PERSO, RACE_SHUTTLE_FORCE, -1);
                ListBuggy[0].Speed = 0;
                if (s_hitUntil < now + RACE_PENGUIN_HOLD) s_hitUntil = now + RACE_PENGUIN_HOLD;
                Log_Info("[racemod] Twinsen's car hits the shuttle");
            }
        }
        const double wx = sh->X + CurrentCubeX * 32768.0, wz = sh->Z + CurrentCubeY * 32768.0;
        for (int o = 0; o < RACE_MAX_OPPONENTS; o++) {
            T_RACE_OPPONENT *op = &s_opp[o];
            if (!op->Count || !RaceOn() || now < op->HitUntil) continue;
            double x, z, oy, v, ddx, ddz;
            PathAt(op, op->S, &x, &z, &oy, &v, &ddx, &ddz);
            if ((x - wx) * (x - wx) + (z - wz) * (z - wz) > (double)RACE_SHUTTLE_REACH * RACE_SHUTTLE_REACH || fabs(oy - sh->Y) > 1500) continue;
            op->HitUntil = now + RACE_PENGUIN_HOLD;
            Log_Info("[racemod] %s hits the shuttle", op->Name);
            const int index = OpponentActor(op);
            if (index < 0) continue;
            T_OBJET *car = &ListObjet[index];
            InitAnim(GEN_ANIM_CHOC, ANIM_ALL_THEN, (U8)index);
            InitHitExtraPof(car->Obj.X, car->Obj.Y + car->YMax + 1, car->Obj.Z, RACE_SHUTTLE_FORCE);
        }
    }
}

// Where the cars are, for the editor's minimap while it plays a race (the console's racecars, asked over the control socket a few times a
// second): Twinsen's, then each opponent's that races in a car -- "car <n> <x> <z> <beta> <name>", island units, n -1 for Twinsen, else
// the opponent's number. An opponent's own car where this scene has it in sight, else where its line has it in the race; one waiting
// out of sight (in another scene's pits, or a race not on) is left out. Returns how many lines.
int RaceMod_Cars(char lines[][96], int most) {
    if (!RaceMod_Active() || CubeMode != CUBE_EXTERIEUR || most <= 0) return 0;
    const double cx = CurrentCubeX * 32768.0, cz = CurrentCubeY * 32768.0;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    int n = 0;
    snprintf(lines[n++], 96, "car -1 %d %d %d Twinsen", (int)(hero->Obj.X + cx), (int)(hero->Obj.Z + cz), (int)(hero->Obj.Beta & 4095));
    for (int o = 0; o < RACE_MAX_OPPONENTS && n < most; o++) {
        T_RACE_OPPONENT *op = &s_opp[o];
        if (!op->Count || !op->Scenes) continue;
        double x, z, y, v, dx, dz;
        S32 beta = 0;
        const int index = OpponentActor(op);
        if (index >= 0 && ListObjet[index].Obj.Y > -10000) {
            x = ListObjet[index].Obj.X + cx; z = ListObjet[index].Obj.Z + cz; beta = ListObjet[index].Obj.Beta & 4095;
        } else if (RaceOn()) {
            PathAt(op, op->S, &x, &z, &y, &v, &dx, &dz);
            beta = (S32)(atan2(dx, dz) * 4096 / (2 * 3.14159265358979)) & 4095;
        } else continue;
        snprintf(lines[n++], 96, "car %d %d %d %d %s", o, (int)x, (int)z, (int)beta, op->Name);
    }
    return n;
}

void RaceMod_Frame(void) {
    if (!RaceMod_Active()) return;
    ListVarGame[RACE_GATE_VAR] = (S16)(ClosedGate() ? 1 : 0);
    for (int i = 0; i < s_nbHeld; i++)
        if (ListVarGame[s_held[i][0]] != s_held[i][1]) {
            ListVarGame[s_held[i][0]] = (S16)s_held[i][1];
            Log_Info("[racemod] game variable %d held at %d", s_held[i][0], s_held[i][1]);
        }
    if (Comportement == C_BUGGY) TrackSafePlace();
    FallenBelow(TimerRefHR);
    if (s_rescue) { s_rescue = 0; Rescue(); }
    RescueArrive();
    // A raised road winds round itself in the air: the classic camera, which stands still until the hero leaves the screen, would watch
    // the car go out of sight behind the road's other levels without its ever leaving the screen. So the camera that follows the car
    // is turned on -- once, with the first frame (the settings have been read by then), and the console's cam_follow can still turn it off.
    static int followed = 0;
    if (!followed && s_nbRaised) { FollowCamera = TRUE; followed = 1; Log_Info("[racemod] raised road: the camera follows the car"); }
    if (!followed && s_nbLoops) { FollowCamera = TRUE; followed = 1; Log_Info("[racemod] loops: the camera follows the car"); }
    FaceTheCar();
    const S32 now = TimerRefHR;
    // (the game clock gone back -- every time the race keeps, a stun's end, a skid's, a lap's start, then lies ahead of it: said, so
    // that a race that goes strange afterwards can be read from the log)
    if (s_oppLast && now < s_oppLast - 1000) Log_Info("[racemod] WARNING: the game clock went back %.1f s (%d to %d)", (s_oppLast - now) / 1000.0, (int)s_oppLast, (int)now);
    S32 dt = s_oppLast ? now - s_oppLast : 0;
    s_oppLast = now;
    if (dt < 0) dt = 0;
    if (dt > 100) dt = 100;
    const int driving = Comportement == C_BUGGY;
    const int inStartScene = s_nbSpots && CubeMode == CUBE_EXTERIEUR && CurrentCubeX == s_spots[0].CubeX && CurrentCubeY == s_spots[0].CubeZ;
    // the grid: after the qualifying lap, or (without qualifying) as soon as the player is in his car; formed in the start line's scene
    if (inStartScene && driving && !ClosedGate() && (s_wantGrid || (s_phase == PHASE_WAIT && !s_qualifying))) { s_wantGrid = 0; FormGrid(now); }
    // the cars that aren't racing, out of sight
    for (int i = 0; i < s_nbHidden; i++)
        if (s_hideScene[i] == NumCube && s_hideActor[i] > 0 && s_hideActor[i] < NbObjets) ListObjet[s_hideActor[i]].Obj.Y = -20000;
    // (and a driver lapping on his own: parade=)
    ParadeStep(now, dt);
    if (s_phase == PHASE_GRID && now >= s_goAt) {
        s_phase = PHASE_RACE;
        for (int n = 0; n < RACE_MAX_OPPONENTS; n++) s_opp[n].LapStart = now;
        Log_Info("[racemod] go");
    }
    // the power-ups: the mushrooms in this scene, the player's car over one, the penguins' fuses
    if (s_powerups) PowerUps(now, driving);
    Vents(now);
    Jets(now, driving);
    LavaBalls(now, driving);
    LavaFalls(now);
    BoostPanels(now, driving);
    Gunners(now, driving);
    Wheel(now, driving);
    SlotMachine(now);
    Monsters(now, driving);
    Shuttles(now, driving);
    if (!Opponents()) return;
    // the race starts at GO (without a grid: when the player first drives off); the opponents wait while he is out of his car
    if (!s_nbSpots && !s_oppStarted && driving && (Input & I_UP)) s_oppStarted = 1;
    const int moving = driving && (s_nbSpots ? s_phase == PHASE_RACE : s_oppStarted);
    // (how far round the player is, for the opponents that push harder when they have fallen behind)
    const double me = moving && s_opp[0].Count ? PlayerProgress() : 0;
    s_carsScene = NumCube;
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) s_carActor[n] = -1;
    for (int n = 0; n < RACE_MAX_OPPONENTS; n++) {
        T_RACE_OPPONENT *o = &s_opp[n];
        if (!o->Count) continue;
        double x, z, y, v, dx, dz;
        PathAt(o, o->S, &x, &z, &y, &v, &dx, &dz);
        if (moving) {
            double pace = o->Pace;
            if (o->Catchup > 0 && s_opp[0].Count && s_laps > 0) {
                const double behind = me - OpponentProgress(o);
                if (behind > 0) pace *= 1 + o->Catchup / 100.0 * (behind < 60 * 512 ? behind / (60 * 512) : 1);
            }
            // (a power-up's boost, shrunk by Twinsen's lightning, stopped by a penguin's blast)
            if (now < o->JetUntil) pace *= JetFactor(o->JetUntil, o->JetFactor);
            else if (now < o->BoostUntil || s_fuelAlways) pace *= RACE_BOOST * 0.9;
            if (now < o->ShrunkUntil) pace *= RACE_SHRUNK;
            if (now < o->HitUntil) { pace = 0; o->Speed = 0; }

            // its line's speed at its skill, reached from a standstill no faster than the car pulls away (the plan's braking is kept)
            const double target = v * pace / 100.0;
            if (o->Speed < target) {
                o->Speed += s_car.Accel * Pull(o->Speed) * o->Top / 100.0 * pace / 100.0 * dt / 1000.0;
                if (o->Speed > target) o->Speed = target;
            } else
                o->Speed = target;
            if (o->LoopOn >= 0) {
                // round a loop, its line waiting at the loop's foot
                const char *said;
                int out = LoopStep(&o->Ride, &s_loops[o->LoopOn], dt / 1000.0, 1, 0, 0, 1, &said);
                if (out == STEP_FELL || out == STEP_BACK || o->Ride.State == LOOP_FALL) { out = STEP_ROUND; o->Ride.V = LoopEntry(&s_loops[o->LoopOn], 0); }
                if (out == STEP_ROUND) {
                    o->Speed = fabs(o->Ride.V);
                    o->LoopOn = -1;
                }
            } else {
                // (skidding on oil: run down to a stop as it turns round once, then away again from a standstill)
                if (now < o->SkidUntil) { const double p = (double)(now - o->SkidStart) / RACE_SKID_MS; o->Speed = o->SkidSpeed * (1 - p) * (1 - p); }
                const double before = o->S, step = o->Speed * dt / 1000.0;
                o->S += step;
                o->MergeLeft = o->MergeLeft > step ? o->MergeLeft - step : 0;
                if (s_sprint) {
                    // (a sprint: at the finish line it stops, finished -- ahead of the player, who hasn't; the one to beat there first
                    // has won)
                    if (o->S >= o->FinishS) {
                        o->S = o->FinishS; o->Speed = 0;
                        if (o->Laps <= 1) {
                            o->Laps = 2;
                            Log_Info("[racemod] %s finished in %.2f s", o->Name, (now - o->LapStart) / 1000.0);
                            if (n == s_main && !s_place) SprintOver(now, 0);
                        }
                    }
                } else if (o->S >= o->Length) o->S -= o->Length;
                if (!s_sprint && o->S < before) {    // past the start line
                    o->Laps++;
                    if (o->Laps > 1) Log_Info("[racemod] opponent %d: lap %d in %.2f s", n + 1, o->Laps - 1, (now - o->LapStart) / 1000.0);
                    o->LapStart = now;
                }
                // (a loop's foot crossed: into its ring, at a speed it gets round at)
                for (int k = 0; k < s_nbLoops; k++) {
                    const double f = o->LoopS[k];
                    if (f < 0 || !(o->S >= before ? before < f && f <= o->S : f > before || f <= o->S)) continue;
                    const T_RACE_LOOP *l = &s_loops[k];
                    double fx, fz, fy, fv, fdx, fdz;
                    PathAt(o, f, &fx, &fz, &fy, &fv, &fdx, &fdz);
                    memset(&o->Ride, 0, sizeof(o->Ride));
                    o->Ride.State = LOOP_RIDE;
                    o->Ride.V = LoopEntry(l, o->Speed);
                    o->Ride.Lat = -(fx - l->CubeX * 32768.0 - l->X) * l->DirZ + (fz - l->CubeZ * 32768.0 - l->Z) * l->DirX - Lane(l, 0);
                    o->S = f;
                    o->LoopOn = k;
                    Log_Info("[racemod] opponent %d: loop %d in at %.0f units/s", n + 1, k + 1, o->Ride.V);
                    break;
                }
            }
            PathAt(o, o->S, &x, &z, &y, &v, &dx, &dz);
        }
        if (CubeMode != CUBE_EXTERIEUR) continue;
        const int index = OpponentActor(o);
        if (index < 0) continue;
        s_carActor[n] = index;
        // Twinsen's super jet-pack coming through: pushed aside, away from its way, and slowed
        if (driving && now < s_jetUntil && now >= o->ShoveUntil && CubeMode == CUBE_EXTERIEUR) {
            const T_OBJET *h = &ListObjet[NUM_PERSO];
            const double hx = h->Obj.X + CurrentCubeX * 32768.0, hz = h->Obj.Z + CurrentCubeY * 32768.0;
            const double ox = x - hx, oz = z - hz;
            if (ox * ox + oz * oz < (double)RACE_SHOVE_REACH * RACE_SHOVE_REACH && fabs(y - h->Obj.Y) < 900) {
                const double hb = h->Obj.Beta * 2 * RACE_PI / 4096, fx = sin(hb), fz = cos(hb), along = ox * fx + oz * fz;
                double px = ox - along * fx, pz = oz - along * fz, pl = sqrt(px * px + pz * pz);
                if (pl < 50) { px = fz; pz = -fx; pl = 1; }   // (right ahead: to the right)
                o->ShoveX = px / pl; o->ShoveZ = pz / pl;
                o->ShoveStart = now; o->ShoveUntil = now + RACE_SHOVE_MS;
                o->Speed *= 0.5;
                Log_Info("[racemod] Twinsen's super jet-pack pushes %s aside", o->Name);
            }
        }
        // off its line while it moves onto it from its grid spot
        const double f = o->MergeLeft / RACE_MERGE;
        T_OBJET *car = &ListObjet[index];
        car->Obj.X = (S32)(x + o->MergeX * f - CurrentCubeX * 32768.0);
        car->Obj.Z = (S32)(z + o->MergeZ * f - CurrentCubeY * 32768.0);
        // (pushed aside: out quickly, back onto its line slowly)
        if (now < o->ShoveUntil) {
            const double t = (double)(now - o->ShoveStart) / RACE_SHOVE_MS, out = t < 0.2 ? t / 0.2 : 1 - (t - 0.2) / 0.8;
            car->Obj.X += (S32)(o->ShoveX * RACE_SHOVE_SIDE * out);
            car->Obj.Z += (S32)(o->ShoveZ * RACE_SHOVE_SIDE * out);
        }
        car->Obj.Y = (S32)(y + o->MergeY * f);
        S32 beta = (S32)(atan2(dx, dz) * 4096.0 / (2 * 3.14159265358979)) & 4095;
        car->Obj.Beta = f >= 1 ? o->SpotBeta : beta;
        car->Obj.Alpha = 0; car->Obj.Gamma = 0;
        if (o->LoopOn >= 0) {
            // round its loop
            const T_RACE_LOOP *l = &s_loops[o->LoopOn];
            double lx, ly, lz, pitch;
            RidePlace(l, &o->Ride, &lx, &ly, &lz, &pitch);
            car->Obj.X = (S32)(lx + (l->CubeX - CurrentCubeX) * 32768.0);
            car->Obj.Y = (S32)ly;
            car->Obj.Z = (S32)(lz + (l->CubeZ - CurrentCubeY) * 32768.0);
            CarPose(&car->Obj, LoopBeta(l), pitch, 0);
        } else
            for (int k = 0; k < s_nbArcs; k++) {
                // over a carried jump: pitched with its line's slope
                if (o->ArcS0[k] < 0 || o->ArcS1[k] < 0 || o->S < o->ArcS0[k] || o->S > o->ArcS1[k]) continue;
                double x2, z2, y2, v2, dx2, dz2;
                PathAt(o, o->S + 256, &x2, &z2, &y2, &v2, &dx2, &dz2);
                const double run = sqrt((x2 - x) * (x2 - x) + (z2 - z) * (z2 - z));
                if (run > 1) CarPose(&car->Obj, beta, -atan2(y2 - y, run), 0);
            }
        // (in another cube than the player's it stands where it is all the same, in sight: the engine keeps every object of a scene within
        // its cube, DoAnimExt, so a car waiting just over the edge stood on the edge itself, a wall the player's car was stopped at (the old
        // moon's grid) -- these cars are let out of it, RaceMod_FreeOfCube. Hidden instead, as they were for a while (2026-10-03), the cars
        // ahead vanished at every cube edge of the lap and came back.)
        ClearRealAngle(car);
        // skidding on oil: the car turning round once, quickly at first, as it slides to a stop along its line
        if (now < o->SkidUntil) {
            const double p = (double)(now - o->SkidStart) / RACE_SKID_MS, turn = 4096.0 * (1 - (1 - p) * (1 - p));
            car->Obj.Beta = (car->Obj.Beta + o->SkidDir * (S32)turn) & 4095;
        }
        // shrunk by the lightning spell: its car's half-size body while it lasts (the build's, opponent_small=)
        if (o->Small >= 0) {
            if (o->Normal < 0 && now >= o->ShrunkUntil) o->Normal = car->GenBody;
            const int want = now < o->ShrunkUntil ? o->Small : o->Normal;
            if (want >= 0 && car->GenBody != want) InitBody((U8)want, (U8)index);
        }
        // a mushroom it drives over
        if (s_powerups && moving) OpponentMushrooms(n, car);
        // the driving animation while it moves (its steps are undone every frame: the position is set here)
        const U16 anim = (U16)(o->HasAnims ? (moving ? o->AnimDrive : o->AnimIdle) : (moving ? 1 : 0));
        if (now >= o->HitUntil && car->GenAnim != anim) InitAnim(anim, ANIM_REPEAT, (U8)index);   // (a penguin's blast: its hit reaction plays)
        if (moving) s_wantFlip = 1;
    }
}

// The player's car stands on the grid until the count-down's GO (BUGGY.CPP RaceSpeed).
S32 RaceMod_Held(void) {
    // (not a won dream's car over its jump: wake_flight=)
    return RaceMod_Active() && (s_phase == PHASE_GRID || TimerRefHR < s_hitUntil || (s_sprint && s_place && !(s_endWon && s_wakeFlight > 0)));
}

// Citadel Island's weather, for the scenes' scripts (2026-10-06): game variable RACE_WEATHER_VAR is 1 while its fine-weather file
// (CITABAU) is shown, 0 in the storm (CITADEL) -- set as the island's file is chosen (EXTFUNC.CPP InitGrilleExt), before the scene's
// scripts run, in any game. A race track's start car on an island with a track in each weather parks where the other weather's ground
// has room for it (RaceTrackScenes: the town circuit's start, scene 49, is the sea in the storm -- the car stood in the water after the
// dream's waking up).
#define RACE_WEATHER_VAR 206
void RaceMod_CitadelWeather(S32 fine) {
    ListVarGame[RACE_WEATHER_VAR] = (S16)(fine ? 1 : 0);
}

// Whether the scene now fading in comes from white (the dream's end: wake_flight=), once: the picture is held white, and fades in
// RACE_WHITE_HOLD_MS later (RaceMod_Story) -- the scene's first picture is Twinsen at its start, before its opening lays him in bed.
S32 RaceMod_FadeFromWhite(void) {
    if (!s_fadeFromWhite) return 0;
    s_fadeFromWhite = 0;
    s_whiteUntil = TimerRefHR + RACE_WHITE_HOLD_MS;
    return 1;
}

// How far round the race the player is, and an opponent, in world units from the start of lap 1 (negative before it). The player is
// placed on the first opponent's line (the lines run side by side round the same lap).
static double PlayerProgress(void) {
    const T_RACE_OPPONENT *o = Guide();
    if (!o) return 0;
    const double wx = ListObjet[NUM_PERSO].Obj.X + CurrentCubeX * 32768.0, wz = ListObjet[NUM_PERSO].Obj.Z + CurrentCubeY * 32768.0;
    const double wy = ListObjet[NUM_PERSO].Obj.Y;
    int best = -1;
    double bd = 1e30;
    // (round a loop: at its foot, as an opponent going round it is -- up in the ring the line's nearest point is any)
    if (s_loopState != LOOP_NONE && s_loopOn >= 0 && o->LoopAt[s_loopOn] >= 0) { best = o->LoopAt[s_loopOn]; bd = 0; }
    else if (s_playerIndex >= 0)
        for (int k = -150; k <= 150; k++) {
            const int i = ((s_playerIndex + k) % o->Count + o->Count) % o->Count;
            const double d = LineDistance(o, i, wx, wy, wz);
            if (d < bd) { bd = d; best = i; }
        }
    if (best < 0 || bd > 20.0 * 512 * 20 * 512)
        for (int i = 0; i < o->Count; i++) {
            const double d = LineDistance(o, i, wx, wy, wz);
            if (d < bd) { bd = d; best = i; }
        }
    s_playerIndex = best;
    const double s = o->Cum[best];
    if (s_sprint) return s;
    // before lap 1 the player is behind the line, near the line's end
    if (s_laps == 0) return s > o->Length / 2 ? s - o->Length : s;
    // (by the start line the line's nearest point can be either end of it: halfway round, the car is at its end, not back at its start)
    if (s < o->Length * 0.1 && s_halfway) return s_laps * o->Length + s;
    return (s_laps - 1) * o->Length + (s > o->Length * 0.9 && !s_halfway ? s - o->Length : s);
}

static double OpponentProgress(const T_RACE_OPPONENT *o) {
    if (o->Laps == 0) return o->S > o->Length / 2 ? o->S - o->Length : o->S;
    return (o->Laps - 1) * o->Length + o->S;
}

static void Time(char *out, size_t size, S32 ms) {
    if (ms < 0) ms = 0;
    snprintf(out, size, "%d:%02d.%02d", (int)(ms / 60000), (int)(ms / 1000 % 60), (int)(ms / 10 % 100));
}

// One line of the display at y: starting at x (align < 0) or ending there (align > 0), with a dark shadow so it reads on sand and on
// asphalt alike; its place marked to redraw.
#define RACE_LINE_STEP 34
static void HudLine(S32 x, S32 y, const char *text, int align) {
    char buf[96];
    snprintf(buf, sizeof(buf), "%s", text);
    const S32 w = SizeFont(buf), x0 = align > 0 ? x - w : x;
    ColorFont(0);
    Font(x0 + 2, y + 2, buf);
    ColorFont(LBAWHITE);
    Font(x0, y, buf);
    BoxMovingAdd(x0 - 4, y - 4, x0 + w + 6, y + RACE_LINE_STEP + 4);
}

// The super jet-pack in the car's place (Twinsen's car hidden: PowerUps): the model at the car's size, lying flat and a little over the
// road, drawn after the scene's objects as they are drawn -- tested against the depth of the ground and the decor, in the colours of
// things near -- its place on the screen marked to be drawn over next frame.
static void DrawSuperJet(S32 now) {
    if (!s_superShown || s_superModel < 0 || CubeMode != CUBE_EXTERIEUR) return;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    T_OBJ_3D pose = hero->Obj;
    CarPose(&pose, (hero->Obj.Beta + s_superTurn) & 4095, s_superLean, 0);
    const int fog = Fill_Flag_Fog, depth = Fill_Flag_ZBuffer, keep = Fill_Flag_NZW;
    const U32 clut = PtrTruePal && PtrCLUTFog ? (U32)((PtrTruePal - PtrCLUTFog) >> 8) : (U32)PalLevel;
    Switch_Fillers(FILL_POLY_FOG_NZW);
    SetCLUT(PalLevel);
    const S32 bob = (S32)(40 * sin(now * 2 * RACE_PI / 600));
    if (BodyDisplay(hero->Obj.X, hero->Obj.Y + RACE_SUPERJET_LIFT + bob, hero->Obj.Z, pose.Alpha, pose.Beta, pose.Gamma, (U8 *)GivePtrObjFix(s_superModel))) {
        SetClip(ScreenXMin, ScreenYMin, ScreenXMax, ScreenYMax);
        if (ClipXMin <= ClipXMax && ClipYMin <= ClipYMax) BoxMovingAdd(ClipXMin, ClipYMin, ClipXMax, ClipYMax);
        UnsetClip();
    }
    SetCLUT(clut);
    Switch_Fillers(depth ? (keep ? (fog ? FILL_POLY_FOG_NZW : FILL_POLY_NZW) : (fog ? FILL_POLY_FOG_ZBUFFER : FILL_POLY_ZBUFFER)) : fog ? FILL_POLY_FOG : FILL_POLY_TEXTURES);
}

// The car Twinsen drives as (drive_as=), in his car's place -- hidden: the frame's update -- with the opponents' driving animation, facing
// his car's way and pitched with the way it climbs or falls (the slope it has just driven, smoothed), drawn after the scene's objects
// as the super jet-pack is.
static void DrawDriveAs(S32 now) {
    if (!s_heroHidden || s_superShown || s_driveBody < 0 || s_driveAnim < 0 || CubeMode != CUBE_EXTERIEUR) return;
    const T_OBJET *hero = &ListObjet[NUM_PERSO];
    const int body = s_driveSmallNow && s_driveSmall >= 0 ? s_driveSmall : s_driveBody;
    if (s_driveShown != body) {
        if (!GivePtrBody(body) || !GivePtrAnim(s_driveAnim)) { Log_Info("[racemod] drive_as: no body %d or animation %d", body, s_driveAnim); s_driveBody = -1; return; }
        if (s_driveShown < 0) { s_drivePrevX = hero->Obj.X; s_drivePrevY = hero->Obj.Y; s_drivePrevZ = hero->Obj.Z; s_drivePitch = 0; }
        ObjectClear(&s_driveObj);
        s_driveObj.Body.Num = -1;
        ObjectInitBody(&s_driveObj, (void *)(uintptr_t)(U32)body);
        s_driveObj.Texture = BufferTexture; s_driveObj.NextTexture = BufferTexture;
        s_driveObj.Anim.Num = -1;
        ObjectInitAnim(&s_driveObj, (void *)(uintptr_t)(U32)s_driveAnim);
        s_driveObj.Status |= FLAG_BODY;
        s_driveShown = body;
        Log_Info("[racemod] driving as body %d (animation %d)", body, s_driveAnim);
    }
    // the pitch: the height it has gained over the way it has gone along its heading (backwards too), smoothed
    const double b = hero->Obj.Beta * 2 * RACE_PI / 4096;
    const double along = (hero->Obj.X - s_drivePrevX) * sin(b) + (hero->Obj.Z - s_drivePrevZ) * cos(b);
    if (fabs(along) > 48) {
        const double slope = (hero->Obj.Y - s_drivePrevY) / along;
        s_drivePitch += (-atan(slope) - s_drivePitch) * 0.35;
        s_drivePrevX = hero->Obj.X; s_drivePrevY = hero->Obj.Y; s_drivePrevZ = hero->Obj.Z;
    } else if (abs(hero->Obj.X - s_drivePrevX) + abs(hero->Obj.Z - s_drivePrevZ) > 600) {
        s_drivePrevX = hero->Obj.X; s_drivePrevY = hero->Obj.Y; s_drivePrevZ = hero->Obj.Z;
    }
    // (its animation stepped -- in place: the opponents' cars are moved by their lines, not by it)
    s_driveObj.X = hero->Obj.X; s_driveObj.Y = hero->Obj.Y; s_driveObj.Z = hero->Obj.Z;
    ObjectSetInterDep(&s_driveObj);
    if (s_driveObj.Status & FLAG_LAST_FRAME) { s_driveObj.Anim.Num = -1; ObjectInitAnim(&s_driveObj, (void *)(uintptr_t)(U32)s_driveAnim); }
    s_driveObj.X = hero->Obj.X; s_driveObj.Y = hero->Obj.Y; s_driveObj.Z = hero->Obj.Z;
    CarPitch(&s_driveObj, hero->Obj.Beta, s_drivePitch);
    ObjectSetInterFrame(&s_driveObj);
    const int fog = Fill_Flag_Fog, depth = Fill_Flag_ZBuffer, keep = Fill_Flag_NZW;
    const U32 clut = PtrTruePal && PtrCLUTFog ? (U32)((PtrTruePal - PtrCLUTFog) >> 8) : (U32)PalLevel;
    Switch_Fillers(FILL_POLY_FOG_NZW);
    SetCLUT(PalLevel);
    if (ObjectDisplay(&s_driveObj)) {
        SetClip(ScreenXMin, ScreenYMin, ScreenXMax, ScreenYMax);
        if (ClipXMin <= ClipXMax && ClipYMin <= ClipYMax) BoxMovingAdd(ClipXMin, ClipYMin, ClipXMax, ClipYMax);
        UnsetClip();
    }
    SetCLUT(clut);
    Switch_Fillers(depth ? (keep ? (fog ? FILL_POLY_FOG_NZW : FILL_POLY_NZW) : (fog ? FILL_POLY_FOG_ZBUFFER : FILL_POLY_ZBUFFER)) : fog ? FILL_POLY_FOG : FILL_POLY_TEXTURES);
    (void)now;
}

// The checkpoints of this cube as red lines on the road (checkpoint_lines=1, for now): each followed along its length -- the raised
// road's height where it crosses that, else the ground's, or the line's own where the ground is far from it (a deck) -- and drawn over
// the scene a few pixels thick.
static void DrawCheckpoints(S32 height) {
    if (!s_showCheckpoints || CubeMode != CUBE_EXTERIEUR) return;
    const S32 thick = height / 128 > 3 ? height / 128 : 3;
    for (int c = 0; c < s_nbCheckpoints; c++) {
        const T_RACE_LINE *l = &s_checkpoints[c];
        if (l->CubeX != CurrentCubeX || l->CubeZ != CurrentCubeY) continue;
        const double len = sqrt((double)(l->X1 - l->X0) * (l->X1 - l->X0) + (double)(l->Z1 - l->Z0) * (l->Z1 - l->Z0));
        const int steps = (int)(len / 256) + 1 > 255 ? 255 : (int)(len / 256) + 1;
        // the height along it: on a raised road the deck's at the line's own level (another level of a road that winds over itself, or
        // the ground far under the deck, made the lines on Ascence leap up and down), past the deck's edge the edge's; elsewhere the
        // ground's where it is near the line's height, else the line's height
        S32 ys[256];
        int deck[256], any = 0;
        for (int i = 0; i <= steps; i++) {
            const double t = (double)i / steps;
            const S32 x = (S32)(l->X0 + (l->X1 - l->X0) * t), z = (S32)(l->Z0 + (l->Z1 - l->Z0) * t);
            ys[i] = CalculAltitudeTerrain(x, z, 0);
            if (l->HasY && abs(ys[i] - l->Y) > 400) ys[i] = l->Y;
            S32 ry; int seg; double lat, tt;
            deck[i] = s_nbRaised && l->HasY && RaisedAt(x + CurrentCubeX * 32768.0, z + CurrentCubeY * 32768.0, l->Y + 64, 0, &ry, &seg, &lat, &tt) && abs(ry - l->Y) < 600;
            if (deck[i]) { ys[i] = ry; any = 1; }
        }
        if (any)
            for (int i = 0; i <= steps; i++) {
                if (deck[i]) continue;
                int j = -1;
                for (int d = 1; d <= steps && j < 0; d++) { if (i - d >= 0 && deck[i - d]) j = i - d; else if (i + d <= steps && deck[i + d]) j = i + d; }
                if (j >= 0) ys[i] = ys[j];
            }
        S32 px = 0, py = 0;
        int had = 0;
        for (int i = 0; i <= steps; i++) {
            const double t = (double)i / steps;
            const S32 x = (S32)(l->X0 + (l->X1 - l->X0) * t), z = (S32)(l->Z0 + (l->Z1 - l->Z0) * t), y = ys[i];
            const int seen = PtrProjectPoint(x, y + 30, z);
            const S32 sx = Xp, sy = Yp;
            if (seen && had) {
                ScreenXMin = (px < sx ? px : sx) - 1; ScreenXMax = (px < sx ? sx : px) + 1;
                ScreenYMin = (py < sy ? py : sy) - 1; ScreenYMax = (py < sy ? sy : py) + thick + 1;
                SetClip(ScreenXMin, ScreenYMin, ScreenXMax, ScreenYMax);
                if (ClipXMin <= ClipXMax && ClipYMin <= ClipYMax) {
                    for (S32 k = 0; k < thick; k++) Line(px, py + k, sx, sy + k, 72);   // (the count-down's red, RACE_RED)
                    BoxMovingAdd(ClipXMin, ClipYMin, ClipXMax, ClipYMax);
                }
            }
            px = sx; py = sy; had = seen;
        }
    }
    UnsetClip();
}

// The lightning's bolts (Strike): from high over each car struck -- the top of the view where that is out of it -- down onto the car,
// a jagged white line edged in gold with a branch off it, jagged anew every frame (it flickers), over the scene until s_boltUntil.
static void DrawBolts(S32 now, S32 height) {
    if (now >= s_boltUntil || !s_nbBolts || CubeMode != CUBE_EXTERIEUR) return;
    const S32 thick = height / 200 > 2 ? height / 200 : 2;
    for (int b = 0; b < s_nbBolts; b++) {
        const T_OBJET *car = &ListObjet[s_boltActor[b]];
        if (car->Obj.Y < -10000 || !PtrProjectPoint(car->Obj.X, car->Obj.Y + 300, car->Obj.Z)) continue;
        const S32 ex = Xp, ey = Yp;
        S32 sx = ex, sy = 0;
        if (PtrProjectPoint(car->Obj.X, car->Obj.Y + 9000, car->Obj.Z) && Yp < ey) { sx = Xp; sy = Yp > 0 ? Yp : 0; }
        if (ey - sy < 8) continue;
        // the bolt's corners: STEPS pieces, each corner pushed aside at random (none at its ends), and a branch from one of them
        enum { STEPS = 7 };
        S32 px[STEPS + 1], py[STEPS + 1];
        const S32 jag = (ey - sy) / 7 + 2;
        for (int i = 0; i <= STEPS; i++) {
            px[i] = sx + (ex - sx) * i / STEPS + (i && i < STEPS ? rand() % (2 * jag + 1) - jag : 0);
            py[i] = sy + (ey - sy) * i / STEPS;
        }
        const int from = 2 + rand() % 3;
        const S32 bx = px[from] + (rand() % 2 ? 1 : -1) * (jag + rand() % (jag + 1)), by = py[from] + (ey - sy) / 5;
        S32 x0 = ex, x1 = ex, y0 = sy, y1 = ey;
        for (int i = 0; i <= STEPS; i++) { if (px[i] < x0) x0 = px[i]; if (px[i] > x1) x1 = px[i]; }
        if (bx < x0) x0 = bx;
        if (bx > x1) x1 = bx;
        SetClip(x0 - thick - 2, y0 - 2, x1 + thick + 2, y1 + 2);
        if (ClipXMin > ClipXMax || ClipYMin > ClipYMax) continue;
        for (int i = 0; i < STEPS; i++) {
            Line(px[i] - thick / 2 - 1, py[i], px[i + 1] - thick / 2 - 1, py[i + 1], RACE_GOLD_EDGE);
            Line(px[i] + thick - thick / 2, py[i], px[i + 1] + thick - thick / 2, py[i + 1], RACE_GOLD_EDGE);
            for (S32 k = 0; k < thick; k++) Line(px[i] + k - thick / 2, py[i], px[i + 1] + k - thick / 2, py[i + 1], LBAWHITE);
        }
        Line(px[from], py[from], bx, by, LBAWHITE);
        Line(px[from] + 1, py[from], bx + 1, by, RACE_GOLD_EDGE);
        BoxMovingAdd(ClipXMin, ClipYMin, ClipXMax, ClipYMax);
    }
    UnsetClip();
}

// The 3D view the library draws with (LIB386 3D: CAMERA.CPP, PROJ.CPP, LIGHT.CPP), saved whole and put back.
typedef struct {
    S32 XCentre, YCentre, NearClip, LFactorX, LFactorY, TypeProj;
    Func_LongProjectPoint *Project;
    Func_ProjectList *ProjectList;
    float FRatioX, FRatioY;
    S32 CameraAlpha, CameraBeta, CameraGamma, CameraX, CameraY, CameraZ, CameraXr, CameraYr, CameraZr, CameraZrClip;
    TYPE_MAT World;
    S32 AlphaLight, BetaLight, GammaLight, NormalXLight, NormalYLight, NormalZLight, CameraXLight, CameraYLight, CameraZLight;
} T_RACE_VIEW;
static void SaveView(T_RACE_VIEW *v) {
    v->XCentre = XCentre; v->YCentre = YCentre; v->NearClip = NearClip; v->LFactorX = LFactorX; v->LFactorY = LFactorY; v->TypeProj = TypeProj;
    v->Project = LongProjectPoint; v->ProjectList = ProjectList; v->FRatioX = FRatioX; v->FRatioY = FRatioY;
    v->CameraAlpha = CameraAlpha; v->CameraBeta = CameraBeta; v->CameraGamma = CameraGamma;
    v->CameraX = CameraX; v->CameraY = CameraY; v->CameraZ = CameraZ; v->CameraXr = CameraXr; v->CameraYr = CameraYr; v->CameraZr = CameraZr;
    v->CameraZrClip = CameraZrClip;
    v->World = MatriceWorld;
    v->AlphaLight = AlphaLight; v->BetaLight = BetaLight; v->GammaLight = GammaLight;
    v->NormalXLight = NormalXLight; v->NormalYLight = NormalYLight; v->NormalZLight = NormalZLight;
    v->CameraXLight = CameraXLight; v->CameraYLight = CameraYLight; v->CameraZLight = CameraZLight;
}
static void RestoreView(const T_RACE_VIEW *v) {
    XCentre = v->XCentre; YCentre = v->YCentre; NearClip = v->NearClip; LFactorX = v->LFactorX; LFactorY = v->LFactorY; TypeProj = v->TypeProj;
    LongProjectPoint = v->Project; ProjectList = v->ProjectList; FRatioX = v->FRatioX; FRatioY = v->FRatioY;
    CameraAlpha = v->CameraAlpha; CameraBeta = v->CameraBeta; CameraGamma = v->CameraGamma;
    CameraX = v->CameraX; CameraY = v->CameraY; CameraZ = v->CameraZ; CameraXr = v->CameraXr; CameraYr = v->CameraYr; CameraZr = v->CameraZr;
    CameraZrClip = v->CameraZrClip;
    MatriceWorld = v->World;
    AlphaLight = v->AlphaLight; BetaLight = v->BetaLight; GammaLight = v->GammaLight;
    NormalXLight = v->NormalXLight; NormalYLight = v->NormalYLight; NormalZLight = v->NormalZLight;
    CameraXLight = v->CameraXLight; CameraYLight = v->CameraYLight; CameraZLight = v->CameraZLight;
}

// The item box (top left): a slot's item, or the roulette going by -- the items turning over, slowing to the one drawn.
static int ShownItem(int k, S32 now) {
    if (s_item[k] < 0 || now >= s_itemAt[k]) return s_item[k];
    const double t = 1.0 - (double)(s_itemAt[k] - now) / RACE_ITEM_ROLL_MS, p = RACE_ITEM_ROLL_STEPS * (1 - (1 - t) * (1 - t));
    return ((s_item[k] + (int)floor(p) - RACE_ITEM_ROLL_STEPS) % PU_ROLLED + 6 * PU_ROLLED) % PU_ROLLED;
}

#define RACE_GOLD 244                      // the palette's gold, round the selected slot

// A slot of the item box: black in the game's own frame, as the found-object display is (OBJECT.CPP, INCRUST_OBJ), and in it the item
// turning as it does there; `size` pixels across, the selected one ringed in gold.
static void ItemBox(S32 x0, S32 y0, S32 size, int pu, int selected, S32 now) {
    const S32 x1 = x0 + size - 1, y1 = y0 + size - 1;
    if (selected)
        for (S32 i = 3; i <= 5; i++) Rect(x0 - i, y0 - i, x1 + i, y1 + i, RACE_GOLD);
    SetClip(x0, y0, x1, y1);
    BackupAngles(ClipXMin, ClipYMin, ClipXMax, ClipYMax);
    Box(ClipXMin, ClipYMin, ClipXMax, ClipYMax, 0);
    const int model = pu < 0 ? -1 : s_puModel[pu] == -2 ? s_oilIcon : s_puModel[pu] == -3 ? -1 : s_puModel[pu] == -4 ? (s_goldIcon >= 0 ? s_goldIcon : s_puModel[PU_FUEL]) : s_puModel[pu];
    U8 *shape = model >= 0 ? (U8 *)GivePtrObjFix(model) : pu >= 0 && s_puModel[pu] == -3 && s_fakeIconBody >= 0 ? (U8 *)GivePtrBody(s_fakeIconBody) : NULL;
    if (shape) {
        // (the scene's view kept whole -- its projection, its camera and its light -- and put back exactly as it was after the item is
        // drawn. The game's own found-object display resets the view instead (PtrInit3DView): the plain follow camera, without the
        // race-track mode's own eye or lift (FOLLOWCAM.CPP FollowCamReapplyLift), so a frame that drew only the objects over the last
        // drawn ground -- most of them at a high frame rate -- drew the cars from another eye than the road's, and Twinsen's car sank
        // into Ascence's deck, or was hidden by it, from the first mushroom on)
        T_RACE_VIEW view;
        SaveView(&view);
        SaveCamera();
        // (lit as the inventory lights its objects: the light set with the camera level, so it stays where it is on the screen -- set with
        // the scene's camera, as the found-object display does, it came from behind some and left them dark)
        SetAngleCamera(0, 0, 0);
        SetLightVector(OBJ_ALPHA_LIGHT, OBJ_BETA_LIGHT, 0);
        const S32 lf = 200 * size / 60;
        SetProjection(x0 + size / 2, y0 + size / 2, 128, lf, lf);
        // (an object much smaller than the inventory's, round about 4,000 tall -- the fake mushroom, the lap's own, a few hundred -- drawn
        // nearer, round its middle, to fill the box as they do: the user, 2026-10-08, of the item box, "The mushroom pick up is far too
        // small to see". Its box: the body header's x, y and z extents, 8-31)
        S32 lookY = 0, lookFrom = 16000;
        {
            S32 box[6];
            memcpy(box, shape + 8, sizeof(box));
            S32 half = box[1] - box[0];
            if (box[3] - box[2] > half) half = box[3] - box[2];
            if (box[5] - box[4] > half) half = box[5] - box[4];
            half /= 2;
            if (half > 0 && half < 1200) { lookY = (box[2] + box[3]) / 2; lookFrom = 16000 * half / 1900; }
        }
        SetFollowCamera(0, lookY, 0, 60 * MUL_ANGLE, 0, 0, lookFrom);
        // (drawn without the outside's fog -- 16,000 away it darkened the meca-penguin to a shadow -- and without its depth buffer,
        // which the scene's ground nearer than that would hide it behind; then the outside's way of drawing back)
        // ... and in the colours of things near: outside, the fog is the colour table each object is drawn with (OBJECT.CPP: SetCLUT,
        // PalLevel and a line further for each step of its distance), and the box had the last object's -- a far one's, all shadow
        const int fog = Fill_Flag_Fog, depth = Fill_Flag_ZBuffer, keep = Fill_Flag_NZW;
        const U32 clut = PtrTruePal && PtrCLUTFog ? (U32)((PtrTruePal - PtrCLUTFog) >> 8) : (U32)PalLevel;
        Switch_Fillers(FILL_POLY_TEXTURES);
        if (CubeMode == CUBE_EXTERIEUR) SetCLUT(PalLevel);
        // (from the objects' own texture page, as the inventory and the scene's objects are drawn (OBJECT.CPP), and back after)
        // (the animated textures, as the inventory animates them while it shows its objects: the fire ball's faces are the fire, and the
        // protection spell's its sparkles, which start black and move only while something animates them -- the scene does only with
        // animated textures of its own or the protection spell on: in the item box both were black on black, empty boxes, 2026-10-08)
        DoTextureAnimation();
        const PTR_U8 map = PtrMap;
        U8 *const objMap = ObjPtrMap;
        const S32 rep = RepMask;
        PtrMap = ObjPtrMap = BufferTexture;
        RepMask = 0xFFFF;
        BodyDisplay(0, 0, 0, 0, (now * 4) & 4095, 0, shape);
        PtrMap = map; ObjPtrMap = objMap; RepMask = rep;
        if (CubeMode == CUBE_EXTERIEUR) SetCLUT(clut);
        Switch_Fillers(depth ? (keep ? (fog ? FILL_POLY_FOG_NZW : FILL_POLY_NZW) : (fog ? FILL_POLY_FOG_ZBUFFER : FILL_POLY_ZBUFFER)) : fog ? FILL_POLY_FOG : FILL_POLY_TEXTURES);
        RestoreCamera();
        RestoreView(&view);
    }
    DrawCadre(ClipXMin, ClipYMin, ClipXMax, ClipYMax, ALL_ANGLES);
    RestoreAngles(ClipXMin, ClipYMin, ClipXMax, ClipYMax);
    UnsetClip();
    BoxMovingAdd(x0 - 6, y0 - 6, x1 + 6, y1 + 6);
}

// The count-down's big letters: 5 x 7 blocks each ('.' empty), drawn as filled boxes with a dark shadow.
static const struct {
    char C;
    const char *Rows;
} s_glyphs[] = {
    {'1', "..#.." ".##.." "..#.." "..#.." "..#.." "..#.." ".###."},
    {'2', ".###." "#...#" "....#" "...#." "..#.." ".#..." "#####"},
    {'3', "####." "....#" "....#" ".###." "....#" "....#" "####."},
    {'G', ".###." "#...#" "#...." "#.###" "#...#" "#...#" ".###."},
    {'O', ".###." "#...#" "#...#" "#...#" "#...#" "#...#" ".###."},
    {'!', "..#.." "..#.." "..#.." "..#.." "..#.." "....." "..#.."},
};

// The text centred on (cx, cy) in blocks of `k` pixels, in palette colour `colour`; returns nothing, marks the area to redraw.
static void BigText(S32 cx, S32 cy, const char *text, S32 k, S32 colour) {
    const S32 n = (S32)strlen(text), w = n * 6 * k - k, h = 7 * k;
    const S32 x0 = cx - w / 2, y0 = cy - h / 2, shadow = k / 3 > 1 ? k / 3 : 1;
    for (int pass = 0; pass < 2; pass++)
        for (S32 i = 0; i < n; i++) {
            const char *rows = NULL;
            for (size_t g = 0; g < sizeof(s_glyphs) / sizeof(s_glyphs[0]); g++)
                if (s_glyphs[g].C == text[i]) rows = s_glyphs[g].Rows;
            if (!rows) continue;
            for (S32 r = 0; r < 7; r++)
                for (S32 c = 0; c < 5; c++) {
                    if (rows[r * 5 + c] != '#') continue;
                    const S32 x = x0 + (i * 6 + c) * k + (pass ? 0 : shadow), y = y0 + r * k + (pass ? 0 : shadow);
                    Box(x, y, x + k - 1, y + k - 1, pass ? colour : 0);
                }
        }
    BoxMovingAdd(x0 - 2, y0 - 2, x0 + w + shadow + 2, y0 + h + shadow + 2);
}

// palette colours for the count-down (the game palette, RESS.HQR entry 0): its strongest red and its brightest green (it has no vivid one)
#define RACE_RED 72
#define RACE_GREEN 141

void RaceMod_Draw(void) {
    if (!RaceMod_Active() || !LbaFont) return;
    if (Comportement != C_BUGGY || CubeMode != CUBE_EXTERIEUR) return;
    const int items = s_powerups && s_nbMush > 0;
    if (!s_car.Hud && !items) return;
    const S32 speed = ListBuggy[0].Speed;

    void *font = PtrFont;
    const S32 leave = InterLeave, space = InterSpace;
    const S32 top = ClipWindowYMin;
    SetFont(LbaFont, 2, 8);
    MemoClipWindow();
    UnsetClipWindow();

    const S32 now = TimerRefHR, width = (S32)ModeDesiredX, height = (S32)ModeDesiredY;
    DrawSuperJet(now);
    DrawDriveAs(now);
    DrawCheckpoints(height);
    DrawBolts(now, height);
    // the item box, top left: its two slots
    if (items) {
        const S32 size = height / 8 > 60 ? height / 8 : 60, gap = size / 5;
        for (int k = 0; k < RACE_ITEM_SLOTS; k++) {
            ItemBox(16 + k * (size + gap), top + 16, size, ShownItem(k, now), k == s_sel && s_item[k] >= 0, now);
            // (the golden Gazogem in use: a gold bar under its slot, its time left)
            if (s_item[k] == PU_GOLD && s_goldUntil > now) {
                const S32 x0 = 16 + k * (size + gap), w = size * (s_goldUntil - now) / RACE_GOLD_MS;
                Box(x0, top + 16 + size + 8, x0 + w, top + 16 + size + 13, RACE_GOLD);
                BoxMovingAdd(x0, top + 16 + size + 8, x0 + size, top + 16 + size + 13);
            }
        }
        // the slot machine, top middle: its three reels, the winning line ringed in gold once all three have stopped the same
        if (s_slotOn) {
            const S32 reel = size * 5 / 4, sgap = reel / 6, x0 = width / 2 - (3 * reel + 2 * sgap) / 2;
            const int stopped = now >= s_slotT0 + RACE_SLOT_STOP0 + 2 * RACE_SLOT_STOP_GAP;
            for (int r = 0; r < 3; r++) ItemBox(x0 + r * (reel + sgap), top + 16, reel, SlotShown(r, now), stopped && s_slotWin, now);
        }
    }
    if (s_car.Hud) {
        char text[96], a[24], b[24];
        const S32 bottom = height - 16 - RACE_LINE_STEP;
        // the gear and the speed, bottom right
        if (speed < 0) snprintf(text, sizeof(text), "Gear R");
        else snprintf(text, sizeof(text), s_car.Automatic ? "Gear %d/%d auto" : "Gear %d/%d", (int)s_gear, (int)s_car.Gears);
        HudLine(width - 16, bottom - RACE_LINE_STEP, text, 1);
        // (a cell taken as a metre: the original buggy's top speed, 3800 units a second, is 27 km/h)
        snprintf(text, sizeof(text), "%d km/h", (int)((abs(speed) * 36 + 2560) / 5120));
        HudLine(width - 16, bottom, text, 1);
        // the lap and its time, bottom left -- before the start what the start line wants, after the race the place
        text[0] = 0;
        if (s_car.HasLine) {
            if (s_place) snprintf(text, sizeof(text), "Finished %d%s of %d", s_place, Ordinal(s_place), s_racers);
            else if (s_phase == PHASE_WAIT) snprintf(text, sizeof(text), "Drive to the start line");
            else if (s_phase == PHASE_GRID) text[0] = 0;
            else if (s_laps > 0) {
                Time(a, sizeof(a), now - s_lapStart);
                if (s_phase == PHASE_QUALIFYING) snprintf(text, sizeof(text), "Qualifying lap  %s", a);
                else if (s_sprint) snprintf(text, sizeof(text), "Race  %s", a);
                else if (s_raceLaps > 0 && s_nbOrder > 0 && s_laps <= s_raceLaps) snprintf(text, sizeof(text), "Lap %d/%d  %s", (int)s_laps, s_raceLaps, a);
                else if (const S32 target = BeatTime()) {
                    // (a time to beat: Raph's)
                    Time(b, sizeof(b), target);
                    snprintf(text, sizeof(text), "Lap %d  %s  to beat %s", (int)s_laps, a, b);
                } else snprintf(text, sizeof(text), "Lap %d  %s", (int)s_laps, a);
            } else if (const T_RACE_GATE *gate = ClosedGate())
                snprintf(text, sizeof(text), "%s", gate->Text);
            else
                snprintf(text, sizeof(text), "Cross the start line");
        }
        if (text[0]) HudLine(16, bottom, text, -1);
        // the place as the race stands, over the lap
        int racers = 0;
        if (s_car.HasLine && !s_place) if (const int place = RacePlace(&racers)) {
            snprintf(text, sizeof(text), "Position %d%s of %d", place, Ordinal(place), racers);
            HudLine(16, bottom - RACE_LINE_STEP, text, -1);
        }

        // the count-down in the middle of the screen: 3, 2, 1 a second each, then GO for a second
        const char *count = NULL;
        if (s_phase == PHASE_GRID && now >= s_goAt - 3000) count = now < s_goAt - 2000 ? "3" : now < s_goAt - 1000 ? "2" : "1";
        else if (s_nbSpots && s_phase == PHASE_RACE && s_goAt && now < s_goAt + 1000) count = "GO!";
        if (count) {
            const S32 k = height / 48 > 3 ? height / 48 : 3;
            BigText(width / 2, height / 3, count, k, count[0] == 'G' ? RACE_GREEN : RACE_RED);
        }
    }
    // a message of the race-track mode's (Message: "Back on the road", the roulette wheel's prize), in the middle near the top -- set
    // since the first tracks and never drawn till 2026-10-10
    if (now < s_messageUntil && s_message[0]) HudLine(width / 2 - SizeFont(s_message) / 2, height / 5, s_message, -1);

    RestoreClipWindow();
    UnsetClip();
    PtrFont = font;
    InterLeave = leave;
    InterSpace = space;
}

// ---- the story's tracks -----------------------------------------------------------------------------------------------------------
// Everything a track's file set up, back to how the engine starts (the car's handling too: the next file gives it again). The set's own
// keys (track=, tired=) stay. The memory the old track's lines and road took is given back.
static void ResetTrack(void) {
    for (int n = 0; n <= RACE_MAX_OPPONENTS; n++) {
        T_RACE_OPPONENT *o = n < RACE_MAX_OPPONENTS ? &s_opp[n] : &s_guide;
        free(o->X); free(o->Z); free(o->Y); free(o->V); free(o->R); free(o->Cum);
    }
    for (int i = 0; i < s_nbArcs; i++) free(s_arcs[i].S);
    free(s_raised); free(s_rbStart); free(s_rbItem);
    s_raised = NULL; s_rbStart = NULL; s_rbItem = NULL;
    s_nbRaised = 0; s_rbX0 = s_rbZ0 = s_rbW = s_rbH = s_rbExtra = 0;
    s_floorRef = RAISED_NOREF; s_onlyNear = -1;
    Defaults(&s_car);                       // (the opponents too)
    s_gear = 1;
    s_laps = 0; s_lapStart = s_lastLap = s_bestLap = 0;
    s_lineY = s_lineHasY = 0; s_halfway = 0; s_nbCheckpoints = 0; s_next = 0; s_showCheckpoints = 0;
    s_messageUntil = 0;
    s_loopOn = -1; s_loopState = LOOP_NONE;
    s_oppStarted = 0; s_nbSpots = 0; s_nbPits = 0;
    s_phase = PHASE_RACE; s_qualifying = 1; s_fineWeather = 0; s_holoArrow = -1;
    s_qualifyTime = 0; s_wantGrid = 0; s_gridAt = s_goAt = 0; s_nbOrder = 0;
    s_forgetLap = 1; s_nbHidden = 0; s_oppLast = 0; s_playerIndex = -1; s_wantFlip = 1;
    s_rescueOn = 1; s_fileIsland = -1; s_fileName[0] = 0;
    s_nbLoops = 0; s_nbJumpCams = 0; s_nbArcs = 0; s_nbCubeScenes = 0;
    s_nbVents = s_nbDrips = 0; memset(s_puffEnd, 0, sizeof(s_puffEnd));
    s_nbFalls = 0; s_nbPanels = 0; s_lavaModel = -1; s_fallFootNext = s_fallLipNext = 0;
    s_nbJets = 0; s_nbTunnels = 0; s_nbLava = 0; s_nbShuttles = 0; s_nbGunners = 0; memset(s_bullets, 0, sizeof(s_bullets));
    memset(&s_wheel, 0, sizeof(s_wheel)); DecorPoseHook = NULL; s_nbMonsters = 0; memset(s_bittenUntil, 0, sizeof(s_bittenUntil));
    s_arcOn = s_arcLast = -1; s_arcS = s_arcV = s_arcAcross = s_arcAcross1 = 0; s_arcTime = s_arcLandedAt = 0;
    s_flying = -1; s_landed = -1; s_landedAt = 0; s_statue = -1; s_raisedLoop = 0; s_gravity = 0;
    s_railBack = s_railUp = s_railLead = 0; s_railTouch = 0;
    s_jumpLo = 0.6; s_jumpHi = 4.0; s_jumpRate = 0; s_jumpFrac = 0; s_jumpOver = 0;
    s_autoIndex = -1;
    s_faced = 0; s_safeIndex = -1; s_safeScene = -1; s_rescueArrive = -1; s_rescue = 0; s_hitUntil = 0; s_belowSince = 0;
    memset(&s_ride, 0, sizeof(s_ride)); s_loopBeta = s_loopTime = 0; s_hinted = -1;
    s_nbGates = 0; s_nbHeld = 0; s_beatOpp = -1; s_nbBeatSet = 0; s_raceLaps = 0; s_nbWinSet = 0; s_place = s_racers = 0;
    ListVarGame[209] = 0;
    s_beatText = -1; s_paradeOpp = -1; s_paradeVar = -1; s_nbParade = 0; s_paradeParkScene = -1; s_paradeS = s_paradeSpeed = 0; s_paradeActorNow = -1;
    s_main = -1; s_beatMain = 0; s_nbFinishSet = 0;
    s_sprint = 0; s_hasFinish = 0; s_endAt = 0; s_endWon = 0; s_wakeScene = s_wakeText = s_wakeActor = s_introText = s_loseText = -1; s_introShown = 0; s_pendLose = -1;
    s_nbAfterGears = 0; s_wakeFlight = 0;
    s_powerups = 0; s_fuelAlways = 0; s_powerupOnly = -1; s_prizeForce = -1; s_nbOnly = 0; s_nbMush = 0; s_nbOilActors = 0; memset(s_slicks, 0, sizeof(s_slicks));
    s_nbFakeActors = 0; s_fakeIconBody = -1; memset(s_fakes, 0, sizeof(s_fakes)); memset(s_shots, 0, sizeof(s_shots));
    s_driveLaser = 0; s_laserModel[0] = s_laserModel[1] = -1; s_skidUntil = 0;
    s_carsScene = -1; for (int n = 0; n < RACE_MAX_OPPONENTS; n++) s_carActor[n] = -1;
    s_noCollideScene = -1;
    if (s_heroHidden) { ListObjet[NUM_PERSO].Flags &= ~INVISIBLE; s_heroHidden = 0; }
    s_superModel = -1; s_superLean = RACE_SUPERJET_LEAN; s_superTurn = 0; s_superShown = 0;
    s_driveBody = s_driveAnim = s_driveSmall = s_driveShown = -1; s_driveSmallNow = 0;
    if (s_heroIsSmall && Comportement == C_BUGGY) InitBody(GEN_BODY_TUNIQUE, NUM_PERSO);
    s_heroSmall = -1; s_heroIsSmall = 0;
    s_penguinWalk = 1; s_item[0] = s_item[1] = -1; s_sel = 0; s_takeAt = 0; s_shiftBlock = 0; s_autoDrop = 1000; s_oilIcon = -1; ProtectionOff(); s_nbPenguinActors = 0; memset(s_penguins, 0, sizeof(s_penguins));
    s_boostUntil = s_protectUntil = s_shrunkUntil = s_jetUntil = 0; for (int n = 0; n < RACE_MAX_OPPONENTS; n++) s_opp[n].ShrunkUntil = 0; s_jetFactor = 1; s_boltUntil = 0; s_nbBolts = 0;
    s_ownTrack = 0; s_loaded[0] = 0;
    FollowCamEyeHook = NULL;
}

// An outside scene of `island` is loading (EXTFUNC.CPP InitGrilleExt, before the island's file is chosen): with a set of tracks (track=),
// the one raced there now -- in this weather, with the statue or without it -- is loaded in place of the one before; on an island with
// none, the car alone.
void RaceMod_Island(S32 island) {
    if (!RaceMod_Active() || !s_nbTracks) return;
    const int fine = ListVarGame[FLAG_CHAPTER] >= 2, risen = ListVarGame[FLAG_CELEBRATION] != 0;
    const char *want = NULL;
    for (int i = 0; i < s_nbTracks && !want; i++) {
        const T_RACE_TRACK *t = &s_tracks[i];
        if (t->Island != island || (t->First >= 0 && ((S32)NumCube < t->First || (S32)NumCube > t->Last))) continue;
        if (t->When == 0 || (t->When == 1 && !fine) || (t->When == 2 && fine) || (t->When == 3 && risen) || (t->When == 4 && !risen)) want = t->Path;
    }
    // (the same track goes on -- unless its race is over: coming back to it is a race afresh)
    if (want ? s_ownTrack && !strcmp(want, s_loaded) && !s_place : !s_ownTrack) return;
    const char *base = getenv("LBA2_RACETRACK_FILE");
    char path[260];
    snprintf(path, sizeof(path), "%s", want ? want : base ? base : "");
    ResetTrack();
    Load(path, want ? 0 : 1);
    if (want) Log_Info("[racemod] island %d%s: its track, %s", (int)island, island == 0 ? (fine ? " (fine weather)" : " (storm)") : "", path);
    else Log_Info("[racemod] island %d: no track -- the car alone", (int)island);
}

// tired=: on Citadel Island, while the story's variable says so, anyone but Twinsen says the tired line instead of their own (GERELIFE.CPP,
// the life scripts' messages: NumObjDial is the one speaking).
// wake=: a dream won, the waking scene's actor -- Zoe -- says the wake line before her first line there: she shakes Twinsen awake.
S32 RaceMod_Dial(S32 num) {
    if (RaceMod_Active() && s_pendWakeScene >= 0 && (S32)NumCube == s_pendWakeScene && NewCube == -1
            && (s_pendWakeActor <= 0 || NumObjDial == s_pendWakeActor)) {
        const int text = s_pendWakeText;
        const U8 speaker = NumObjDial;
        s_pendWakeScene = -1;
        if (text >= 0) {
            Log_Info("[racemod] awake: text %d said by actor %d, before its own line %d", text, (int)speaker, (int)num);
            Dial(text, TRUE);
            NumObjDial = speaker;
        }
    }
    if (!RaceMod_Active() || s_tiredVar < 0 || s_tiredVar > 255 || Island != 0 || NumObjDial == NUM_PERSO) return num;
    if (ListVarGame[s_tiredVar] != s_tiredValue) return num;
    Log_Info("[racemod] text %d said as the tired line %d", (int)num, s_tiredText);
    return s_tiredText;
}
