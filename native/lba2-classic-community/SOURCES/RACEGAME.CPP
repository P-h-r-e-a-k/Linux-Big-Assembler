/* RACEGAME: the race game -- LBA Assembler's race tracks played as a game of their own, the standalone Little Big Halloween Racing
 * Adventure (2026-10-16), on when RACEGAME.TXT is beside the game's data (its installer writes it). The game's main menu is then the race
 * game's (GAMEMENU.CPP MainGameMenu asks RaceGame_Menu), in the retail game's look:
 *     Resume (with a race on)   Story Mode   Track select   Specials   Best laps   Options   Quit
 * Story Mode and Track select are greyed and passed over until the file opens them. Track select (2026-10-22): the code RACEGAME.TXT
 * asks for first (tracks_code=; once a run, CodePage), then its tracks in the file's order (TracksPage), each a race or a time trial as a
 * special is -- but its car from the car grid (GridPage: every gridcar= of the file, 7 by 7 a page, each drawn small once as its page
 * opens), not the specials' car page. Specials: the special tracks, then a race (the line-up,
 * the power-ups) or a time trial (Twinsen alone, no power-ups); a race on a track with levels (its race file has
 * difficulty_level= lines: the Temple of Bú's, 2026-10-17) then its difficulty, easy, medium or hard (the last one chosen first);
 * then the car, its body turning on the page; then the race -- the special's race file with the car's and the race game's keys
 * (RACEMOD.CPP drive_as=, start_scene=, start_in_car=, race_laps=, end_to_menu=, trial=, difficulty=) written to RACE_NOW.TXT in
 * the user's folder and loaded (RaceMod_LoadFile), and a new game at the start line, no introduction. A time trial's fastest single lap
 * (2026-10-18; before, its whole time) goes to the track's best laps, its ten fastest, kept in BESTTIMES.TXT in the user's folder; Best
 * laps shows them (a time trial is the same at every level: Twinsen alone, no opponents' levels to it). A race's laps, and a qualifying
 * lap's, never go there.
 * The first run (2026-10-17): the player's first race or time trial is preceded by the controls page, and the first race with power-ups
 * by the power-up page (the track's own power-ups, each in its item box with what it does) -- right after the car page, as the race is
 * loaded, before its scene: Enter turns the pages, Esc skips them. Options shows them again (its row "Race controls, power-ups"
 * -- two rows, Power-ups and Race controls, until 2026-10-19, when Updates took the menu's last place: GAMEMENU.CPP OptionsMenu asks
 * RaceGame_OptionsMenu). The first race always starts with a qualifying lap; once one has been driven, a race asks after
 * the car page -- a qualifying lap, or none and Twinsen at the back of the grid (RACEMOD.CPP grid_back=) -- the last answer chosen first.
 * The player's settings, RACEGAME_SETTINGS.TXT in the user's folder ("key=value" lines; other keys are kept as they are):
 *     controls_seen=1  powerups_seen=1     the first run's pages shown
 *     qualified=1                         a qualifying lap driven (RaceGame_Qualified): it may be skipped from then on
 *     skip_qualifying=0|1                 the last answer
 *     difficulty=0|1|2                    the difficulty chosen last (LevelPage)
 *     updates=auto|ask|off                new versions installed when the game is closed, asked first, or not looked for (auto)
 *     update_checked=<unix time>          when the game last started the updater (0: never)
 *     minimap=1|0                         the race's mini-map, bottom right, on (the first time) or off (Options, "Camera,
 *                                         window, updates"; RACEMOD.CPP DrawMinimap asks RaceGame_Minimap)
 *     camera=0|1|2                        the race camera's view: inside the car, just behind it, further back (the first
 *                                         time: Further back, 2026-10-21) -- Options, "Camera, window, updates", or V (a pad's
 *                                         Back button) during a race (RACEMOD.CPP camera=; RaceGame_CameraChosen)
 *     ghost=0|1|3|4|5|7                   the ghost chosen last on a time trial's ghost page: none, the player's own best lap, the
 *                                         game's Easy, Medium, Hard or Extreme ghost (RACEMOD.CPP ghost=)
 *     ghost_beaten=<1|2|3|4>|<track>      the game's ghosts beaten on a track: Easy, Medium, Hard, Extreme -- each beaten opens the next
 *                                         (a harder one's time beaten racing an easier one counts too)
 *     trial_laps=1|3|5|10|0               a time trial's laps, chosen last on its laps page (0: endless, until the player stops)
 *     <id>_unlocked=1                     a car won (its car= line's unlock=<id>: pumpkin_unlocked=1, the pumpkin F1)
 * Unlocking a car (2026-10-20 -- the user: a congratulations "featuring the F1 Pumpkin car, with a message saying they've unlocked the
 * pumpkin for use in time trials. Let's hold this car back until it's unlocked"): a car whose car= line says unlock=<id> is shown on the
 * car page dark, a padlock over it and how it is won (hint=), and can't be taken until the player wins a race -- first over the line, at
 * any difficulty (RaceGame_Finished: a race, not a time trial). That win unlocks it for good (<id>_unlocked=1) and, once, the
 * congratulations follow the race (RaceGame_Celebrate: RACEMOD.CPP goes to the menu a moment after the line, without its "You beat ...!"):
 * the car turning, fireworks over the page and RACESOUNDS/FIREWORK.WAV (CelebratePage). A car unlocked "for trial" is taken for time trials
 * only: on the page for a race it is shown, "For time trials", and not taken.
 * Updates (2026-10-19): the installer leaves a copy of itself in the game's folder, Update\LBRA-Setup.exe. At the start (the race game
 * as a player runs it: none of the command harness's flags, so never an editor's Play or a scripted test -- those skip the menu), if
 * updates aren't off and a week has gone by since the last check (or there was none), the game starts it hidden --
 * `--check-update "<install dir>" --wait-pid <its pid>` (RACEUPDATE.CPP) -- and goes on: the updater looks for a new version
 * (https://lbabirthdayevent.com/LBRA-Setup.exe), downloads it, and installs it once the game is closed (or asks first), keeping the
 * player's User\ folder. Options, Updates: the mode (Automatic, Ask first, Off) and "Check for updates now" (`--manual`: a check even
 * with updates off, asked first then); the updater's last word (Update\status.txt, "<unix time>|<text>") under them.
 *
 * RACEGAME.TXT, "key=value" lines ('#' starts a comment), UTF-8:
 *     title=<the game's name>
 *     story=0|1  tracks=0|1        Story Mode, Track select open (0: greyed)
 *     laps=<n>                     a race's laps (3), unless its race file has its own (race_laps=: the Temple of Bú's five)
 *     end_to_menu=<ms>             a finished race back to the menu this long after its end (8000)
 *     special=<name>|<race file>|<time trial file>|<start scene>     files beside the game's data (and so are those they name)
 *     track=<name>|<race file>|<time trial file>|<start scene>       one of Track select's, the same (a race file with sprint=1: no
 *                                  qualifying lap asked; a race won on a track unlocks no car -- the hints name the special's race)
 *     tracks_code=<digits>         Track select asks for them first (none: it doesn't); given right, open for the rest of the run
 *     gridcar=<as car=>            a car of Track select's car grid (the specials keep car='s); grid_car=<its name> in the player's
 *                                  settings the one taken last
 *     car=<name>|<drive_as value: BODY.HQR body, animation, shrunk body>[|<acceleration> <top speed> <handling>][|<option>...]
 *                                  the car's stats (2026-10-20), stars from 0 to 5 in half stars: shown on the car page, and the race
 *                                  drives the car by them (RACEMOD.CPP car_accel=, car_top=, car_handling=: 2.5 the track's own
 *                                  setup). A line without them: a car with no stars shown, driven as the track sets it.
 *                                  In place of the drive_as value, a line of the race file's own (it has a '='): written to the race as
 *                                  it is -- twinsen_body=<generics>, Twinsen's own buggy painted (the Halloween buggy, 2026-10-20).
 *                                  Options after the stats, each "key=value":
 *                                    page=<BODY.HQR body> <animation>   the body the car page shows (a car with a race file's line)
 *                                    unlock=<id>[ trial]                won by winning a race (<id>_unlocked=1); "trial": then for
 *                                                                       time trials only
 *                                    hint=<text>                        how it is won, under the padlock ("Win a race to unlock")
 * BESTTIMES.TXT: "<track>|<trial's time ms>|<best lap ms>|<car>|<date>|<laps>|lap" lines, a time trial's fastest single lap (and its
 * whole time, GO to the last lap's line, over <laps> laps), the fastest lap first. A line without "lap" at its end -- the whole trials'
 * times kept before 2026-10-18, "<track>|<time ms>|<best lap ms>|<car>|<date>[|<laps>]" -- is kept as it is but not shown, as are other
 * tracks' lines and lines that can't be read: a whole trial's time is never taken for a lap's.
 */
#include "C_EXTERN.H"
#include "CONFIG_FILE.H"
#include "CONSOLE/CONSOLE.H"
#include "CONTROL.H"
#include "DIRECTORIES.H"
#include "IMAGE_LOAD.H"
#include "INPUT.H"
#include "JOYSTICK.H"
#include "RACEGAME.H"
#include "RACEMOD.H"
#include "RACEUPDATE.H"
#include "RES_SWITCH.H"
#include "UI_LAYOUT.H"

#include <FILEIO/SAVEPNG.H>
#include <SVGA/SCREEN.H>
#include <SYSTEM/KEYBOARD.H>
#include <SYSTEM/KEYBOARD_KEYS.H>
#include <SYSTEM/WINDOW.H>
#include <SYSTEM/LOG.H>
#include <SYSTEM/MOUSE.H>
#include <SYSTEM/STRING.H>

#include <SDL3/SDL.h>
#include <cctype>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <ctime>

extern void RaceMod_DrawBody(S32 x0, S32 y0, S32 x1, S32 y1, S32 body, S32 beta, S32 measure, S32 anim);   // RACEMOD.CPP
extern U32 RaceMod_PlaySound(const char *name, S32 *ms);                                                   // ...
extern void RaceMod_StopSound(U32 handle);
extern S32 FollowCamera;                                                                                    // FOLLOWCAM.CPP

#define RG_MAX_SPECIALS 8
#define RG_MAX_ENTRIES 40                   // the specials and Track select's tracks together (s_sp)
#define RG_MAX_CARS 16
#define RG_MAX_GRID 320                     // Track select's cars (gridcar=)
#define RG_MAX_TIMES 10
#define RG_OTHER_SIZE 16384

// the menu's rows (BuildCustomMenuText labels them: RaceGame_MenuText)
#define RG_ID_STORY 2300
#define RG_ID_TRACKS 2301
#define RG_ID_SPECIALS 2302
#define RG_ID_TIMES 2303
#define RG_ID_RACE 2310
#define RG_ID_TRIAL 2311
#define RG_ID_EASY 2312                     // the difficulty's rows: easy, medium, hard
#define RG_ID_MEDIUM 2313
#define RG_ID_HARD 2314
#define RG_ID_SPECIAL 2320                  // + the special's index
#define RG_ID_QUALIFY 2350                  // the qualifying page's rows (QualifyingPage)
#define RG_ID_SKIP 2351
#define RG_ID_POWERUPS 2352                 // the Options menu's rows of the race game's (RaceGame_OptionsMenu)
#define RG_ID_CONTROLS 2353
#define RG_ID_UPDATES 2354
#define RG_ID_UPDATE_MODE 2355              // the Updates page's rows (UpdatesPage)
#define RG_ID_UPDATE_NOW 2356
#define RG_ID_WINDOW 2357                   // the Updates page's full screen <-> a window row (CONFIG_FILE.CPP ToggleDisplayFullscreen; F11 / Alt+Enter too)
#define RG_ID_MINIMAP 2358                  // ... and its mini-map on <-> off row (RaceGame_Minimap)
#define RG_ID_CAMERA 2359                   // ... and its camera's view row: inside the car, just behind, further back (camera=)
#define RG_ID_GHOST_NONE 2360               // the ghost page's rows (GhostPage): no ghost, the player's best lap, the game's Easy, Medium, Hard
#define RG_ID_GHOST_OWN 2361
#define RG_ID_GHOST_EASY 2362
#define RG_ID_GHOST_MEDIUM 2363
#define RG_ID_GHOST_HARD 2364
#define RG_ID_LAPS_1 2365                   // a time trial's laps (LapsPage): 1, 3, 5, 10, endless -- 2365 .. 2369
#define RG_ID_LAPS_ENDLESS 2369
#define RG_ID_GHOST_EXTREME 2370            // the game's Extreme ghost (2026-10-25): after Hard, opened by beating it
#define RG_ID_RESTART 2371                  // the main menu in a race: the race (or time trial) again from its start, as it was set up
#define RG_ID_GHOST_OTHER 2372            // the ghost page's "Other": another player's ghost of the track (OthersPage, 2026-10-26)
#define RG_ID_OTHER_FIRST 2380            // ... its rows: the ghosts, 2380 .. 2384 (RG_OTHERS_SHOWN) ...
#define RG_ID_GHOST_IMPORT 2385           // ... "Import ghosts..." (the setup's --import-ghosts) ...
#define RG_ID_GHOST_SHARE 2386            // ... "Share my ghosts..." (the setup's --export-ghosts)
#define RG_OTHERS_SHOWN 5
#define RG_ID_BACK 26                       // the game's own "Back"
#define RG_ID_RESUME 70
#define RG_ID_OPTIONS 74
#define RG_ID_QUIT 75

typedef struct {
    char Name[96], Race[160], Trial[160];
    char Was[96];                           // the name it had before (was=, its track= line's fifth field: a track renamed -- RenamedTracks)
    S32 Scene;
    int Levels;                             // its race has levels, easy to hard (difficulty_level= in its race file)
    int Laps;                               // its race's laps once looked up (SpecialLaps; 0: not yet)
    int Track;                              // one of Track select's (track=), not a special
    int Sprint;                             // a sprint, start line to finish line once (sprint=1 in its race file): no qualifying lap
} T_RG_SPECIAL;

typedef struct {
    char Name[96], Drive[64];
    S32 Body, Anim;                         // drive_as='s body and driving animation (the car page poses the car by it)
    int Rated;                              // its stats given (the car= line's third field)
    float Stars[3];                         // ... acceleration, top speed, handling: 0 to 5 stars, in halves
    int Line;                               // Drive is a line of the race file's own (twinsen_body=...), not a drive_as value
    char Unlock[24];                        // won by winning a race (unlock=<id>; empty: always there) ...
    int TrialOnly;                          // ... and then for time trials only
    char Hint[96];                          // ... how (hint=)
} T_RG_CAR;

typedef struct {
    S32 Time, Lap, Laps;                    // the trial's whole time, its fastest lap (what ranks it), its laps
    char Car[96], Date[16];
} T_RG_TIME;

static int s_read = 0, s_on = 0;
static char s_title[96] = "";
static char s_windowTitle[96] = "";        // window_title=: the window's title bar ("Little Big Racing Adventure"; the engine's own otherwise)
static int s_story = 0, s_tracks = 0, s_laps = 3;
static S32 s_endMs = 8000;
static T_RG_SPECIAL s_sp[RG_MAX_ENTRIES];   // the specials and the tracks, as RACEGAME.TXT lists them
static int s_nbSp = 0;
static int s_spIndex[RG_MAX_SPECIALS];      // the specials (Specials' rows): their places in s_sp
static int s_nbSpecials = 0, s_nbTracks = 0;
static T_RG_CAR s_cars[RG_MAX_CARS];        // the specials' cars (car=: the car page)
static int s_nbCars = 0;
static int s_car = 0;                       // the car chosen last
static T_RG_CAR s_grid[RG_MAX_GRID];        // Track select's cars (gridcar=: the car grid)
static int s_nbGrid = 0;
static int s_gridCar = 0;                   // the one chosen last (RACEGAME_SETTINGS.TXT grid_car=, by its name)
static char s_tracksCode[24] = "";          // tracks_code=: Track select asks for it (none: open)
static int s_tracksOpen = 0;                // ... given right this run
static int s_lastSp = 0, s_lastTrial = 0;
static int s_lastTrack = -1;                // the track chosen last (its place in s_sp)
static int s_level = 1;                     // the difficulty chosen last: 0 easy, 1 medium, 2 hard (RACEGAME_SETTINGS.TXT)
static const char *const s_levelName[3] = {"Easy", "Medium", "Hard"};

static T_RG_TIME s_times[RG_MAX_ENTRIES][RG_MAX_TIMES];    // each special's and track's best laps, the fastest first
static int s_nbTimes[RG_MAX_ENTRIES];
static char s_other[RG_OTHER_SIZE];         // BESTTIMES.TXT's other lines: other tracks', the whole trials' times of before 2026-10-18
static int s_timesRead = 0;

// the race on
static int s_raceSp = -1, s_raceTrial = 0, s_raceOver = 0;
static int s_raceLapCount = 0;              // the race's laps as written to it (race_laps=; 0 an endless time trial)
static S32 s_trialKept = 0;                 // the lap an endless time trial has put in Best laps so far (RaceGame_TrialBest; 0 none)
static const T_RG_CAR *s_raceCar = NULL;    // its car (s_cars' or s_grid's)
static char s_raceWatch[ADELINE_MAX_PATH];  // the ghost it plays back (Best laps' watch; "" a race or a time trial)
// the race to start: a qualifying lap first (1), none and Twinsen at the back of the grid (0), or as its file says (-1) -- QualifyingPage
static int s_qualifyNow = -1;
// ... and the race's own, kept for Restart (2026-10-27, the user: "when we select to "Restart the race" during a qualifying lap, it starts
// the race with Twinsen first on the grid even though we haven't completed a qualifying lap": the restart wrote no qualifying= line and the
// race file's own, none, put him on pole)
static int s_raceQualify = -1;
static void Introduction(int sp, int trial);
static int s_raceLevel = -1;                // its difficulty (-1: none, a time trial or a track without levels)

static U16 s_mainMenu[4 + 2 * 8];
static U16 s_spMenu[4 + 2 * (1 + RG_MAX_SPECIALS)];
static U16 s_modeMenu[4 + 2 * 3];
static U16 s_levelMenu[4 + 2 * 4];

// ---- RACEGAME.TXT ------------------------------------------------------------------------------------------------------------------

static void Trim(char *s) {
    char *end = s + strlen(s);
    while (end > s && (end[-1] == '\n' || end[-1] == '\r' || end[-1] == ' ' || end[-1] == '\t')) *--end = 0;
}

// The next "|" field of `p` into `out` (the rest of the line for the last).
static const char *Field(const char *p, char *out, size_t size) {
    size_t n = 0;
    while (*p && *p != '|') {
        if (n + 1 < size) out[n++] = *p;
        p++;
    }
    out[n] = 0;
    while (n > 0 && out[n - 1] == ' ') out[--n] = 0;
    return *p == '|' ? p + 1 : p;
}

// Whether a race file (beside the game's data) has a line starting `start`: levels (difficulty_level=), a sprint (sprint=1).
static int FileHas(const char *race, const char *start) {
    char path[ADELINE_MAX_PATH];
    GetResPath(path, ADELINE_MAX_PATH, race);
    FILE *f = fopen(path, "r");
    if (!f) return 0;
    char line[1024];
    int found = 0;
    const size_t n = strlen(start);
    while (!found && fgets(line, sizeof(line), f)) found = !strncmp(line, start, n);
    fclose(f);
    return found;
}
static int HasLevels(const char *race) { return FileHas(race, "difficulty_level="); }

// A car= (or gridcar=) line's value into `c`; non-zero: a car (its name and its drive_as value or race file's line given).
static int ParseCar(T_RG_CAR *c, const char *v) {
    char stars[64];
    v = Field(v, c->Name, sizeof(c->Name));
    v = Field(v, c->Drive, sizeof(c->Drive));
    Field(v, stars, sizeof(stars));
    c->Body = c->Drive[0] ? atoi(c->Drive) : -1;
    if (!c->Drive[0] || sscanf(c->Drive, "%*d %d", &c->Anim) != 1) c->Anim = -1;
    // (its stats: three numbers, each kept to 0..5 in half stars; none, or not three, and the car has none)
    c->Rated = sscanf(stars, "%f %f %f", &c->Stars[0], &c->Stars[1], &c->Stars[2]) == 3;
    for (int k = 0; k < 3; k++) {
        const float st = c->Rated ? c->Stars[k] : 2.5f;
        c->Stars[k] = (float)(floor((st < 0 ? 0 : st > 5 ? 5 : st) * 2 + 0.5) / 2);
    }
    // (a line of the race file's own in place of a drive_as value: no body to pose, but page= gives one)
    c->Line = strchr(c->Drive, '=') != NULL;
    if (c->Line) c->Body = c->Anim = -1;
    c->Unlock[0] = 0; c->TrialOnly = 0; c->Hint[0] = 0;
    // (its options: page=, unlock=, hint=)
    v = Field(v, stars, sizeof(stars));
    while (*v) {
        char option[128];
        v = Field(v, option, sizeof(option));
        if (!strncmp(option, "page=", 5) && sscanf(option + 5, "%d %d", &c->Body, &c->Anim) < 2) c->Anim = -1;
        else if (!strncmp(option, "unlock=", 7)) {
            char what[16] = "";
            sscanf(option + 7, "%23s %15s", c->Unlock, what);
            c->TrialOnly = !strcmp(what, "trial");
        } else if (!strncmp(option, "hint=", 5)) snprintf(c->Hint, sizeof(c->Hint), "%s", option + 5);
    }
    return c->Name[0] && c->Drive[0];
}

static void ReadSettings(void);
static void RenamedTracks(void);
static void UpdateAtStart(void);

// The window's title for INITADEL.C, before the window is made (RACEGAME.TXT's window_title= alone, read on its own: the race game's
// Read waits for the game's paths and settings); NULL without the file or the key -- the engine's own title then.
extern "C" const char *RaceGame_WindowTitle(void) {
    static char title[96];
    char path[ADELINE_MAX_PATH];
    GetResPath(path, ADELINE_MAX_PATH, "RACEGAME.TXT");
    FILE *f = fopen(path, "r");
    if (!f) return NULL;
    char line[512];
    title[0] = 0;
    while (fgets(line, sizeof(line), f))
        if (!strncmp(line, "window_title=", 13)) {
            snprintf(title, sizeof(title), "%s", line + 13);
            Trim(title);
            break;
        }
    fclose(f);
    return title[0] ? title : NULL;
}

static void Read(void) {
    if (s_read) return;
    s_read = 1;
    char path[ADELINE_MAX_PATH];
    GetResPath(path, ADELINE_MAX_PATH, "RACEGAME.TXT");
    FILE *f = fopen(path, "r");
    if (!f) return;
    char line[512];
    while (fgets(line, sizeof(line), f)) {
        char *hash = strchr(line, '#');
        if (hash) *hash = 0;
        Trim(line);
        char *eq = strchr(line, '=');
        if (!eq) continue;
        *eq = 0;
        const char *key = line, *v = eq + 1;
        while (*v == ' ') v++;
        if (!strcmp(key, "title")) snprintf(s_title, sizeof(s_title), "%s", v);
        else if (!strcmp(key, "window_title")) snprintf(s_windowTitle, sizeof(s_windowTitle), "%s", v);
        else if (!strcmp(key, "story")) s_story = atoi(v);
        else if (!strcmp(key, "tracks")) s_tracks = atoi(v);
        else if (!strcmp(key, "laps")) s_laps = atoi(v) > 0 ? atoi(v) : 3;
        else if (!strcmp(key, "end_to_menu")) s_endMs = atoi(v);
        else if (!strcmp(key, "tracks_code")) snprintf(s_tracksCode, sizeof(s_tracksCode), "%s", v);
        else if (((!strcmp(key, "special") && s_nbSpecials < RG_MAX_SPECIALS) || !strcmp(key, "track")) && s_nbSp < RG_MAX_ENTRIES) {
            // (a special, or one of Track select's tracks: the same fields)
            T_RG_SPECIAL *sp = &s_sp[s_nbSp];
            char scene[16];
            v = Field(v, sp->Name, sizeof(sp->Name));
            v = Field(v, sp->Race, sizeof(sp->Race));
            v = Field(v, sp->Trial, sizeof(sp->Trial));
            v = Field(v, scene, sizeof(scene));
            // (was=<its old name>: a track renamed -- its best laps, unlocks and ghosts moved over, RenamedTracks)
            char was[112];
            Field(v, was, sizeof(was));
            snprintf(sp->Was, sizeof(sp->Was), "%s", !strncmp(was, "was=", 4) ? was + 4 : "");
            sp->Scene = scene[0] ? atoi(scene) : -1;
            sp->Levels = sp->Race[0] ? HasLevels(sp->Race) : 0;
            sp->Sprint = sp->Race[0] ? FileHas(sp->Race, "sprint=1") : 0;
            sp->Laps = 0;
            sp->Track = !strcmp(key, "track");
            if (sp->Name[0] && sp->Race[0]) {
                if (sp->Track) s_nbTracks++;
                else s_spIndex[s_nbSpecials++] = s_nbSp;
                s_nbSp++;
            }
        } else if (!strcmp(key, "car") && s_nbCars < RG_MAX_CARS) {
            if (ParseCar(&s_cars[s_nbCars], v)) s_nbCars++;
        } else if (!strcmp(key, "gridcar") && s_nbGrid < RG_MAX_GRID) {
            if (ParseCar(&s_grid[s_nbGrid], v)) s_nbGrid++;
        }
    }
    fclose(f);
    s_on = 1;
    if (s_windowTitle[0]) SetWindowTitle(s_windowTitle);
    ReadSettings();
    RenamedTracks();
    UpdateAtStart();
    // (the Auto camera on, whatever lba2.cfg says -- the user, 2026-10-22: "Let's force Auto camera on by default"; read after the config,
    // and the game's Options can still turn it off for the run)
    if (!FollowCamera) { FollowCamera = TRUE; Log_Info("[racegame] the Auto camera turned on (the race game's default)"); }
    if (s_nbSpecials > 0) s_lastSp = s_spIndex[0];
    int levelled = 0;
    for (int i = 0; i < s_nbSp; i++) levelled += s_sp[i].Levels;
    Log_Info("[racegame] %s: %d special track(s), %d track(s) (%d with levels: the last difficulty %s), %d cars, %d in the car grid%s%s%s",
             s_title[0] ? s_title : "the race game", s_nbSpecials, s_nbTracks, levelled, s_levelName[s_level], s_nbCars, s_nbGrid,
             s_story ? "" : ", Story Mode greyed", s_tracks && s_nbTracks ? "" : ", Track select greyed",
             s_tracks && s_nbTracks && s_tracksCode[0] ? ", Track select behind a code" : "");
}

S32 RaceGame_On(void) {
    Read();
    return s_on;
}

// ---- the player's settings ---------------------------------------------------------------------------------------------------------

static int s_setRead = 0;
static int s_controlsSeen = 0, s_powerupsSeen = 0;  // the first run's pages shown
static int s_qualified = 0;                 // a qualifying lap driven: the next races may skip it
static int s_skipQualifying = 0;            // the last answer to "a qualifying lap?"
static int s_updates = 0;                   // new versions: 0 installed when the game is closed, 1 asked first, 2 not looked for
static int s_minimap = 1;                   // the race's mini-map, bottom right (RACEMOD.CPP DrawMinimap)
// the race camera's view (RACEMOD.CPP camera=): 0 inside the car, 1 just behind it -- the camera as it always was -- or 2 further back,
// the first time (2026-10-21: the user found the car too close, the road ahead hard to see past Twinsen's Halloween buggy)
static int s_camera = 2;
// a time trial's ghost car (RACEMOD.CPP ghost=, 2026-10-24), the one chosen last on the ghost page (GhostPage): 0 none, 1 the player's own
// best lap, 3, 4, 5 the game's Easy, Medium, Hard ghost to beat, 7 its Extreme one (2 the fastest of all: no longer offered, the ghost page's first choice
// instead)
static int s_ghost = 0;
// ... 8: another player's ghost, this file (the "Other" page, OthersPage; ghost_other= -- RACEMOD.CPP ghost_file=)
static char s_ghostOther[ADELINE_MAX_PATH] = "";
// the game's ghosts beaten, each track's: 0 none, 1 Easy, 2 Medium, 3 Hard -- beating one opens the next (2026-10-25, the user: "force them to
// beat easy to unlock medium, and to beat medium to unlock hard"); ghost_beaten=<level>|<track> lines (RaceGame_GhostBeaten)
static int s_beaten[RG_MAX_ENTRIES];
// a time trial's laps (2026-10-25, the user: "allow them to select how many laps they wish to drive, let's also allow them to drive laps
// until they quit, maybe we should have "1, 3, 5, 10, infinite""): the laps page's last choice, 0 endless (RACEMOD.CPP race_laps=0)
static int s_trialLaps = 3;
static const int s_trialLapsChoice[5] = {1, 3, 5, 10, 0};
static int s_lapsRuns = 0;                  // (the laps page's rows say runs, not laps: a sprint's -- LapsPage)
static const char *const s_cameraLabel[3] = {"Camera: inside the car", "Camera: just behind", "Camera: further back"};
static long long s_updateChecked = 0;       // when the updater was last started (unix time; 0 never)
static const char *const s_updateKey[3] = {"auto", "ask", "off"};
static const char *const s_updateLabel[3] = {"Updates: Automatic", "Updates: Ask first", "Updates: Off"};
static char s_setOther[RG_OTHER_SIZE];      // RACEGAME_SETTINGS.TXT's lines of other keys, kept as they are
static char s_gridCarName[96] = "";         // the car grid's car chosen last (grid_car=), by its name
#define RG_MAX_UNLOCKED 8
static char s_unlocked[RG_MAX_UNLOCKED][24]; // the cars won: their unlock=<id>s (<id>_unlocked=1)
static int s_nbUnlocked = 0;

static void SettingsPath(char *path) { GetCfgPath(path, ADELINE_MAX_PATH, "RACEGAME_SETTINGS.TXT"); }

static void ReadSettings(void) {
    if (s_setRead) return;
    Read();   // (the tracks first: the ghosts beaten are kept by their names)
    s_setRead = 1;
    s_setOther[0] = 0;
    char path[ADELINE_MAX_PATH];
    SettingsPath(path);
    FILE *f = fopen(path, "r");
    if (!f) { Log_Info("[racegame] no settings yet (%s): the first run's pages, and a qualifying lap first", path); return; }
    char line[512];
    while (fgets(line, sizeof(line), f)) {
        Trim(line);
        if (!line[0] || line[0] == '#') continue;
        char *eq = strchr(line, '=');
        int *value = NULL;
        if (eq) {
            *eq = 0;
            if (!strcmp(line, "controls_seen")) value = &s_controlsSeen;
            else if (!strcmp(line, "powerups_seen")) value = &s_powerupsSeen;
            else if (!strcmp(line, "qualified")) value = &s_qualified;
            else if (!strcmp(line, "skip_qualifying")) value = &s_skipQualifying;
            else if (!strcmp(line, "difficulty")) value = &s_level;
            else if (!strcmp(line, "minimap")) value = &s_minimap;
            else if (!strcmp(line, "camera")) value = &s_camera;
            else if (!strcmp(line, "ghost")) value = &s_ghost;
            else if (!strcmp(line, "trial_laps")) value = &s_trialLaps;
            else if (!strcmp(line, "updates")) {
                value = &s_updates;
                *value = 0;
                for (int k = 0; k < 3; k++) if (!strcmp(eq + 1, s_updateKey[k])) *value = k;
                continue;
            } else if (!strcmp(line, "update_checked")) {
                s_updateChecked = strtoll(eq + 1, NULL, 10);
                continue;
            } else if (!strcmp(line, "grid_car")) {
                snprintf(s_gridCarName, sizeof(s_gridCarName), "%s", eq + 1);
                continue;
            } else if (!strcmp(line, "ghost_other")) {
                snprintf(s_ghostOther, sizeof(s_ghostOther), "%s", eq + 1);
                continue;
            } else if (!strcmp(line, "ghost_beaten")) {
                // (<level>|<track>; a track no longer in the game kept as it is)
                const char *bar = strchr(eq + 1, '|');
                int sp = -1;
                for (int i = 0; bar && i < s_nbSp && sp < 0; i++) if (!strcmp(s_sp[i].Name, bar + 1) || (s_sp[i].Was[0] && !strcmp(s_sp[i].Was, bar + 1))) sp = i;
                if (sp >= 0) { const int n = atoi(eq + 1); if (n > s_beaten[sp]) s_beaten[sp] = n > 4 ? 4 : n; continue; }
                *eq = '=';
                value = NULL;
                if (strlen(s_setOther) + strlen(line) + 2 < sizeof(s_setOther)) { strcat(s_setOther, line); strcat(s_setOther, "\n"); }
                continue;
            } else if (strlen(line) > 9 && !strcmp(line + strlen(line) - 9, "_unlocked")) {
                // (a car won: <id>_unlocked=1)
                line[strlen(line) - 9] = 0;
                if (atoi(eq + 1) && s_nbUnlocked < RG_MAX_UNLOCKED && line[0]) snprintf(s_unlocked[s_nbUnlocked++], sizeof(s_unlocked[0]), "%s", line);
                continue;
            }
            if (value) *value = atoi(eq + 1);
            else *eq = '=';
        }
        if (!value && strlen(s_setOther) + strlen(line) + 2 < sizeof(s_setOther)) { strcat(s_setOther, line); strcat(s_setOther, "\n"); }
    }
    fclose(f);
    if (s_level < 0 || s_level > 2) s_level = 1;
    s_minimap = s_minimap != 0;
    if (s_camera < 0 || s_camera > 2) s_camera = 2;
    if (s_ghost < 0 || s_ghost > 8 || s_ghost == 2 || s_ghost == 6) s_ghost = 0;
    Log_Info("[racegame] settings: controls seen %d, power-ups seen %d, qualified %d, skip qualifying %d, updates %s (checked %lld), mini-map %s, camera %d",
             s_controlsSeen, s_powerupsSeen, s_qualified, s_skipQualifying, s_updateKey[s_updates], s_updateChecked, s_minimap ? "on" : "off", s_camera);
}

// ---- updates -----------------------------------------------------------------------------------------------------------------------

#define RG_UPDATE_EVERY (7 * 24 * 3600)     // a check a week
#define RG_UPDATER "Update\\LBRA-Setup.exe"  // the installer's copy of itself (the installer's Updater.cs)
static long long s_updateStarted = 0;       // the updater started this run (by the page's "Check for updates now": the page says so)
static void WriteSettings(void);

// The updater started: `manual` from the Updates page (a check even with updates off). Non-zero: started.
static int UpdateSpawn(int manual) {
    char exe[ADELINE_MAX_PATH], dir[ADELINE_MAX_PATH];
    GetResPath(exe, ADELINE_MAX_PATH, RG_UPDATER);
    if (!RaceUpdate_Exists(exe)) {
        Log_Info("[racegame] no updater (%s): no update check", exe);
        return 0;
    }
    // (the game's folder: RACEGAME.TXT's)
    GetResPath(dir, ADELINE_MAX_PATH, "RACEGAME.TXT");
    char *cut = dir + strlen(dir);
    while (cut > dir && cut[-1] != '\\' && cut[-1] != '/') cut--;
    *cut = 0;
    if (!dir[0]) snprintf(dir, sizeof(dir), ".");
    if (!RaceUpdate_Spawn(exe, dir, manual)) return 0;
    ReadSettings();
    s_updateChecked = (long long)time(NULL);
    WriteSettings();
    return 1;
}

// At the start: the updater, when updates aren't off and the last check is a week old (or there was none, or it is in the future: a clock
// put back). Only as a player runs the game: an editor's Play and the tests run with the command harness.
static void UpdateAtStart(void) {
    if (Control_IsActive()) return;
    if (s_updates == 2) { Log_Info("[racegame] updates off: no update check"); return; }
    const long long now = (long long)time(NULL);
    if (s_updateChecked > 0 && now >= s_updateChecked && now - s_updateChecked < RG_UPDATE_EVERY) {
        Log_Info("[racegame] updates %s: checked %lld h ago, the next check in %lld h", s_updateKey[s_updates], (now - s_updateChecked) / 3600,
                 (s_updateChecked + RG_UPDATE_EVERY - now + 3599) / 3600);
        return;
    }
    if (UpdateSpawn(0)) Log_Info("[racegame] updates %s: a week since the last check (or none): checking", s_updateKey[s_updates]);
}

// The updater's last word (Update\status.txt: "<unix time>|<text>"), its text into `out` and its time; 0: none.
static long long UpdateStatus(char *out, size_t size) {
    out[0] = 0;
    char path[ADELINE_MAX_PATH];
    GetResPath(path, ADELINE_MAX_PATH, "Update\\status.txt");
    FILE *f = fopen(path, "r");
    if (!f) return 0;
    char line[512];
    long long at = 0;
    if (fgets(line, sizeof(line), f)) {
        Trim(line);
        const char *bar = strchr(line, '|');
        if (bar) {
            at = strtoll(line, NULL, 10);
            snprintf(out, size, "%s", bar + 1);
        }
    }
    fclose(f);
    return at;
}

static void WriteSettings(void) {
    char path[ADELINE_MAX_PATH];
    SettingsPath(path);
    FILE *f = fopen(path, "w");
    if (!f) { Log_Warn("[racegame] the settings could not be written to %s", path); return; }
    fprintf(f, "# %s: the player's settings (RACEGAME.CPP)\n", s_title[0] ? s_title : "The race game");
    fprintf(f, "controls_seen=%d\npowerups_seen=%d\nqualified=%d\nskip_qualifying=%d\ndifficulty=%d\n", s_controlsSeen, s_powerupsSeen, s_qualified,
            s_skipQualifying, s_level);
    fprintf(f, "updates=%s\nupdate_checked=%lld\n", s_updateKey[s_updates >= 0 && s_updates <= 2 ? s_updates : 0], s_updateChecked);
    fprintf(f, "minimap=%d\ncamera=%d\nghost=%d\ntrial_laps=%d\n", s_minimap, s_camera, s_ghost, s_trialLaps);
    if (s_ghostOther[0]) fprintf(f, "ghost_other=%s\n", s_ghostOther);
    if (s_gridCarName[0]) fprintf(f, "grid_car=%s\n", s_gridCarName);
    for (int sp = 0; sp < s_nbSp; sp++) if (s_beaten[sp] > 0) fprintf(f, "ghost_beaten=%d|%s\n", s_beaten[sp], s_sp[sp].Name);
    for (int i = 0; i < s_nbUnlocked; i++) fprintf(f, "%s_unlocked=1\n", s_unlocked[i]);
    fputs(s_setOther, f);
    fclose(f);
}

// Whether a car is there to take: one with unlock= only once it is won; one unlocked "for trial" only for a time trial.
static int Unlocked(const char *id) {
    for (int i = 0; i < s_nbUnlocked; i++) if (!strcmp(s_unlocked[i], id)) return 1;
    return 0;
}
static int Locked(const T_RG_CAR *c) { return c && c->Unlock[0] && (ReadSettings(), !Unlocked(c->Unlock)); }
static int TakenFor(const T_RG_CAR *c, int trial) { return c && !Locked(c) && (trial || !(c->Unlock[0] && c->TrialOnly)); }
static int CarLocked(int car) { return car >= 0 && car < s_nbCars && Locked(&s_cars[car]); }
static int CarFor(int car, int trial) { return car >= 0 && car < s_nbCars && TakenFor(&s_cars[car], trial); }

// ---- the best times ----------------------------------------------------------------------------------------------------------------

static void TimesPath(char *path) { GetCfgPath(path, ADELINE_MAX_PATH, "BESTTIMES.TXT"); }

// A special's race's laps: its race file's own (race_laps=: the Temple of Bú's five), else the menu's laps=. Its time trial is as long.
static int SpecialLaps(int sp) {
    T_RG_SPECIAL *s = &s_sp[sp];
    if (s->Laps > 0) return s->Laps;
    s->Laps = s_laps;
    char path[ADELINE_MAX_PATH];
    GetResPath(path, ADELINE_MAX_PATH, s->Race);
    FILE *f = fopen(path, "r");
    if (!f) return s->Laps;
    char line[512];
    while (fgets(line, sizeof(line), f))
        if (!strncmp(line, "race_laps=", 10) && atoi(line + 10) > 0) s->Laps = atoi(line + 10);
    fclose(f);
    return s->Laps;
}

// A best lap into its track's ten fastest, in its place (a lap as fast as one already there after it): its rank (1-10), 0 for none.
static int AddLap(int sp, const T_RG_TIME *t) {
    int k = 0;
    while (k < s_nbTimes[sp] && s_times[sp][k].Lap <= t->Lap) k++;
    if (k >= RG_MAX_TIMES) return 0;
    if (s_nbTimes[sp] < RG_MAX_TIMES) s_nbTimes[sp]++;
    for (int j = s_nbTimes[sp] - 1; j > k; j--) s_times[sp][j] = s_times[sp][j - 1];
    s_times[sp][k] = *t;
    return k + 1;
}

static void ReadTimes(void) {
    if (s_timesRead) return;
    s_timesRead = 1;
    memset(s_nbTimes, 0, sizeof(s_nbTimes));
    s_other[0] = 0;
    char path[ADELINE_MAX_PATH];
    TimesPath(path);
    FILE *f = fopen(path, "r");
    if (!f) return;
    char line[512];
    int laps = 0, kept = 0;
    while (fgets(line, sizeof(line), f)) {
        Trim(line);
        if (!line[0]) continue;
        char track[96], ms[16], lap[16], nb[16], kind[16];
        T_RG_TIME t;
        const char *p = Field(line, track, sizeof(track));
        p = Field(p, ms, sizeof(ms));
        p = Field(p, lap, sizeof(lap));
        p = Field(p, t.Car, sizeof(t.Car));
        p = Field(p, t.Date, sizeof(t.Date));
        p = Field(p, nb, sizeof(nb));
        Field(p, kind, sizeof(kind));
        t.Time = atoi(ms);
        t.Lap = atoi(lap);
        t.Laps = nb[0] ? atoi(nb) : s_laps;
        // (a best lap's line ends in "lap"; one without it -- a whole trial's time, as they were kept before 2026-10-18 -- is kept, never
        // shown as a lap. A lap is a lap however long its trial was: the Temple of Bú's three-lap trials' too.)
        int sp = -1;
        if (!strcmp(kind, "lap") && t.Lap > 0)
            for (int i = 0; i < s_nbSp && sp < 0; i++) if (!strcmp(s_sp[i].Name, track) || (s_sp[i].Was[0] && !strcmp(s_sp[i].Was, track))) sp = i;
        if (sp < 0) {
            if (strlen(s_other) + strlen(line) + 2 < sizeof(s_other)) { strcat(s_other, line); strcat(s_other, "\n"); kept++; }
            continue;
        }
        AddLap(sp, &t);
        laps++;
    }
    fclose(f);
    Log_Info("[racegame] best laps read: %d lap line(s), %d other line(s) kept as they are (other tracks', whole trials' times)", laps, kept);
}

static void WriteTimes(void) {
    char path[ADELINE_MAX_PATH];
    TimesPath(path);
    FILE *f = fopen(path, "w");
    if (!f) { Log_Warn("[racegame] the best laps could not be written to %s", path); return; }
    for (int sp = 0; sp < s_nbSp; sp++)
        for (int k = 0; k < s_nbTimes[sp]; k++) {
            const T_RG_TIME *t = &s_times[sp][k];
            fprintf(f, "%s|%d|%d|%s|%s|%d|lap\n", s_sp[sp].Name, (int)t->Time, (int)t->Lap, t->Car, t->Date, (int)t->Laps);
        }
    fputs(s_other, f);
    fclose(f);
}

// A track's name as a ghost file's (RACEMOD.CPP GhostOwnPath: letters, digits, spaces and - _ kept, the rest '_')
static void GhostFileName(const char *track, char *out, size_t size) {
    size_t n = 0;
    for (const char *p = track; *p && n + 1 < size; p++) {
        const unsigned char c = (unsigned char)*p;
        out[n++] = (isalnum(c) && c < 128) || c == ' ' || c == '-' || c == '_' ? (char)c : '_';
    }
    out[n] = 0;
}

// Tracks renamed (2026-10-26 -- the user: "Citadel Island: the storm" to "Citadel 1", and five more): a track= line with was=<the old
// name> -- the player's best laps and ghosts beaten under the old name read as the new one and written again under it, and the ghosts in
// the user's GHOSTS of the old name (the player's own, "<old>.ghost", and the ones shared with them, "<old> - <whose>.ghost") moved to
// the new one, their track= line with it. Once a run, at the start; nothing to do once moved.
static void RenamedTracks(void) {
    int any = 0;
    for (int sp = 0; sp < s_nbSp; sp++) if (s_sp[sp].Was[0]) any = 1;
    if (!any) return;
    // (best laps and ghosts beaten: the old names taken as the new as they are read -- ReadTimes, ReadSettings -- and written new)
    ReadTimes();
    WriteTimes();
    WriteSettings();
    char folder[ADELINE_MAX_PATH];
    RaceMod_GhostFolder(folder, sizeof(folder));
    int count = 0, moved = 0;
    char **files = SDL_GlobDirectory(folder, "*.ghost", SDL_GLOB_CASEINSENSITIVE, &count);
    for (int i = 0; files && i < count; i++)
        for (int sp = 0; sp < s_nbSp; sp++) {
            if (!s_sp[sp].Was[0]) continue;
            char oldName[112], newName[112];
            GhostFileName(s_sp[sp].Was, oldName, sizeof(oldName));
            GhostFileName(s_sp[sp].Name, newName, sizeof(newName));
            const size_t on = strlen(oldName);
            // ("<old>.ghost" or "<old> - <whose>.ghost")
            if (SDL_strncasecmp(files[i], oldName, on) || (SDL_strcasecmp(files[i] + on, ".ghost") && strncmp(files[i] + on, " - ", 3))) continue;
            char from[ADELINE_MAX_PATH], to[ADELINE_MAX_PATH];
            snprintf(from, sizeof(from), "%s/%s", folder, files[i]);
            snprintf(to, sizeof(to), "%s/%s%s", folder, newName, files[i] + on);
            FILE *in = fopen(from, "r");
            if (!in) continue;
            FILE *out = fopen(to, "wx");
            if (!out) { fclose(in); Log_Info("[racegame] renamed track: %s left as it is (%s is there already)", files[i], to); break; }
            char line[1024];
            int head = 1;
            while (fgets(line, sizeof(line), in)) {
                if (head && !strncmp(line, "track=", 6)) { fprintf(out, "track=%s\n", s_sp[sp].Name); continue; }
                if (head && !strncmp(line, "points=", 7)) head = 0;
                fputs(line, out);
            }
            fclose(in);
            const int good = !ferror(out);
            fclose(out);
            if (good) { remove(from); moved++; }
            else remove(to);
            break;
        }
    SDL_free(files);
    for (int sp = 0; sp < s_nbSp; sp++)
        if (s_sp[sp].Was[0]) Log_Info("[racegame] renamed track: \"%s\" was \"%s\"", s_sp[sp].Name, s_sp[sp].Was);
    if (moved) Log_Info("[racegame] renamed tracks: %d ghost file(s) moved to the new names", moved);
}

static void TimeText(char *out, size_t size, S32 ms) {
    if (ms < 0) ms = 0;
    snprintf(out, size, "%d:%02d.%02d", (int)(ms / 60000), (int)(ms / 1000 % 60), (int)(ms / 10 % 100));
}

static int RecordLap(S32 total, S32 lap, int laps);
static void TrialKeptOut(void);

// RACEMOD.CPP: a race is over -- `total` its time from GO to the last lap's line, `bestLap` its fastest lap (the lap clock's: line to
// line, a qualifying lap's never). A time trial's fastest lap into its track's best laps: the rank it took (1-10), 0 for none.
static int s_celebrate = -1;                // the car a race has just unlocked: the congratulations to show (CelebratePage), -1 none

S32 RaceGame_Finished(S32 trial, S32 total, S32 bestLap, S32 place, S32 racers) {
    if (!RaceGame_On()) return 0;
    s_raceOver = 1;
    // (a race won -- first over the line, at any difficulty: the cars still locked are unlocked, for good; the first of them is the
    // congratulations' -- RaceGame_Celebrate)
    s_celebrate = -1;
    // (a special's race: the cars' hints say "Win the Halloween race" -- Track select's races don't unlock them, 2026-10-22)
    if (!trial && place == 1 && s_raceSp >= 0 && s_raceSp < s_nbSp && !s_sp[s_raceSp].Track) {
        ReadSettings();
        for (int i = 0; i < s_nbCars; i++) {
            if (!CarLocked(i) || s_nbUnlocked >= RG_MAX_UNLOCKED) continue;
            snprintf(s_unlocked[s_nbUnlocked++], sizeof(s_unlocked[0]), "%s", s_cars[i].Unlock);
            if (s_celebrate < 0) s_celebrate = i;
            Log_Info("[racegame] the race won: %s unlocked%s", s_cars[i].Name, s_cars[i].TrialOnly ? " for time trials" : "");
        }
        if (s_celebrate >= 0) WriteSettings();
    }
    char t[24], l[24];
    TimeText(t, sizeof(t), total);
    TimeText(l, sizeof(l), bestLap);
    Log_Info("[racegame] %s over: %s, best lap %s, %d of %d%s%s", trial ? "the time trial" : "the race", t, bestLap > 0 ? l : "none", (int)place,
             (int)racers, !trial && s_raceLevel >= 0 && s_raceLevel <= 2 ? ", difficulty " : "",
             !trial && s_raceLevel >= 0 && s_raceLevel <= 2 ? s_levelName[s_raceLevel] : "");
    if (!trial) return 0;
    // (a sprint's runs put the best so far in as they went -- RaceGame_TrialBest: taken out, the trial's own goes in)
    TrialKeptOut();
    return RecordLap(total, bestLap, s_raceLapCount);
}

// The lap this trial has put in Best laps as it was driven (RaceGame_TrialBest) taken out again
static void TrialKeptOut(void) {
    if (s_trialKept) {
        ReadTimes();
        const int sp = s_raceSp;
        for (int k = 0; k < s_nbTimes[sp]; k++) {
            const T_RG_TIME *t = &s_times[sp][k];
            if (t->Lap != s_trialKept || t->Laps != 0 || strcmp(t->Car, s_raceCar ? s_raceCar->Name : "")) continue;
            for (int j = k; j + 1 < s_nbTimes[sp]; j++) s_times[sp][j] = s_times[sp][j + 1];
            s_nbTimes[sp]--;
            WriteTimes();
            Log_Info("[racegame] best laps of %s: this trial's %.2f s so far taken out", s_sp[sp].Name, s_trialKept / 1000.0);
            break;
        }
        s_trialKept = 0;
    }
}

// RACEMOD.CPP: a time trial's new fastest lap while it goes on -- any trial's, kept for good the moment it is over the line (a trial left
// before its end lost it, 2026-10-26; an endless one has no end), a sprint's run with more to come -- into Best laps as it is driven,
// in place of the one this trial put there before. The trial's end (RaceGame_Finished) puts its own entry in that one's place.
void RaceGame_TrialBest(S32 lap) {
    if (!RaceGame_On() || !s_raceTrial || s_raceSp < 0 || s_raceSp >= s_nbSp || lap <= 0) return;
    TrialKeptOut();
    if (RecordLap(0, lap, 0)) s_trialKept = lap;
}

// RACEMOD.CPP: whether the race just over unlocked a car -- the congratulations follow it (RaceGame_Menu: CelebratePage), so the race
// goes back to the menu a moment after the line
S32 RaceGame_Celebrate(void) { return RaceGame_On() && s_celebrate >= 0; }

// RACEMOD.CPP: the race's track's best lap, Best laps' first (ms; 0 for none or no race of the race game's) -- a time trial's display
// shows it over the lap, or the trial's own fastest lap when that is quicker.
S32 RaceGame_BestLap(void) {
    if (!RaceGame_On() || s_raceSp < 0 || s_raceSp >= s_nbSp) return 0;
    ReadTimes();
    return s_nbTimes[s_raceSp] > 0 ? s_times[s_raceSp][0].Lap : 0;
}

// RACEMOD.CPP: the player changed the race camera's view during a race (V, a pad's Back button): the next race starts with it.
void RaceGame_CameraChosen(S32 view) {
    if (!RaceGame_On() || view < 0 || view > 2) return;
    ReadSettings();
    if (s_camera == view) return;
    s_camera = (int)view;
    WriteSettings();
}

// RACEMOD.CPP DrawMinimap: the race's mini-map on (1) or off (0) as the player set it (Options); -1 when this isn't the race game.
S32 RaceGame_Minimap(void) {
    if (!RaceGame_On()) return -1;
    ReadSettings();
    return s_minimap;
}

// A lap into the race's track's best laps (a time trial's fastest; a qualifying lap: RaceGame_Qualified -- no power-ups on either): the
// rank it took (1-10), 0 for none. `total` the whole run's time (a qualifying lap's own), `laps` how many laps that was.
static int RecordLap(S32 total, S32 lap, int laps) {
    if (s_raceSp < 0 || s_raceSp >= s_nbSp || lap <= 0) return 0;
    char l[24];
    TimeText(l, sizeof(l), lap);
    ReadTimes();
    const int sp = s_raceSp;
    T_RG_TIME e;
    e.Time = total;
    e.Lap = lap;
    e.Laps = laps;
    snprintf(e.Car, sizeof(e.Car), "%s", s_raceCar ? s_raceCar->Name : "");
    const time_t now = time(NULL);
    const struct tm *tm = localtime(&now);
    if (tm) strftime(e.Date, sizeof(e.Date), "%Y-%m-%d", tm);
    else e.Date[0] = 0;
    const int rank = AddLap(sp, &e);
    if (!rank) { Log_Info("[racegame] best laps of %s: %s not among the ten fastest", s_sp[sp].Name, l); return 0; }
    WriteTimes();
    Log_Info("[racegame] best laps of %s: %s is %d%s", s_sp[sp].Name, l, rank, rank == 1 ? " (the best)" : "");
    return rank;
}

// ---- the race ----------------------------------------------------------------------------------------------------------------------

static int Absolute(const char *p) { return p[0] == '/' || p[0] == '\\' || (isalpha((unsigned char)p[0]) && p[1] == ':'); }

// The race: the special's race file (or its time trial's), the files it names found beside the game's data, then the car and the race
// game's keys -- and the difficulty chosen (`level`, -1 none: after the file's own, so it counts) -- written to RACE_NOW.TXT in the user's
// folder and loaded in place of any other. `watch`: a ghost of the track's to play back (Best laps), not a race.
static int StartRace(int sp, int trial, const T_RG_CAR *car, int level, const char *watch = NULL) {
    const T_RG_SPECIAL *s = &s_sp[sp];
    char src[ADELINE_MAX_PATH], out[ADELINE_MAX_PATH];
    GetResPath(src, ADELINE_MAX_PATH, trial && s->Trial[0] ? s->Trial : s->Race);
    GetCfgPath(out, ADELINE_MAX_PATH, "RACE_NOW.TXT");
    FILE *in = fopen(src, "r");
    if (!in) { Log_Warn("[racegame] %s's race file %s could not be read", s->Name, src); return 0; }
    FILE *f = fopen(out, "w");
    if (!f) { fclose(in); Log_Warn("[racegame] %s could not be written", out); return 0; }
    fprintf(f, "# %s: %s, %s (RACEGAME.CPP)\n", s_title, s->Name, trial ? "a time trial" : "a race");
    char line[1024];
    while (fgets(line, sizeof(line), in)) {
        char *eq = strchr(line, '=');
        if (eq && line[0] != '#') {
            *eq = 0;
            const size_t n = strlen(line);
            char *v = eq + 1;
            while (*v == ' ') v++;
            Trim(v);
            if (((n > 5 && !strcmp(line + n - 5, "_path")) || !strcmp(line, "raised")) && v[0] && !Absolute(v)) {
                char full[ADELINE_MAX_PATH];
                GetResPath(full, ADELINE_MAX_PATH, v);
                fprintf(f, "%s=%s\n", line, full);
            } else fprintf(f, "%s=%s\n", line, v);
        } else fputs(line, f);
    }
    fclose(in);
    if (car) fprintf(f, car->Line ? "# %s\n%s\n" : "# %s\ndrive_as=%s\n", car->Name, car->Drive);
    // (its stats, which the race drives it by: RACEMOD.CPP car_accel= car_top= car_handling=)
    if (car && car->Rated)
        fprintf(f, "# its stats, in stars of 5\ncar_accel=%.1f\ncar_top=%.1f\ncar_handling=%.1f\n", car->Stars[0], car->Stars[1], car->Stars[2]);
    if (s->Scene >= 0) fprintf(f, "start_scene=%d\n", (int)s->Scene);
    // (a time trial's laps as chosen -- 0 endless -- a race's, a sprint's and a ghost played back the track's own)
    s_raceLapCount = trial && !s->Sprint && !(watch && watch[0]) ? s_trialLaps : SpecialLaps(sp);
    fprintf(f, "start_in_car=1\nrace_laps=%d\nend_to_menu=%d\n", s_raceLapCount, (int)s_endMs);
    if (trial) fprintf(f, "trial=1\n");
    // (a sprint's time trial: its runs, each from the start again -- RACEMOD.CPP SprintRunAgain)
    if (trial && s->Sprint && !(watch && watch[0])) fprintf(f, "trial_runs=%d\n", s_trialLaps);
    // (a time trial's ghost car: the track it is of, the car it names, which ghost is raced -- or the ghost played back)
    if (trial) {
        ReadSettings();
        fprintf(f, "ghost_name=%s\nghost_car=%s\nghost=%d\n", s->Name, car ? car->Name : "Twinsen's car", s_ghost);
        // (another player's: its file -- the "Other" page)
        if (s_ghost == 8) fprintf(f, "ghost_file=%s\n", s_ghostOther);
        if (watch && watch[0]) fprintf(f, "ghost_watch=%s\n", watch);
    }
    // (a qualifying lap or none, the player's answer -- QualifyingPage: the first race always has one)
    else if (s_qualifyNow >= 0) fprintf(f, s_qualifyNow ? "qualifying=1\n" : "qualifying=0\ngrid_back=1\n");
    if (level >= 0 && level <= 2) fprintf(f, "# the difficulty chosen: %s\ndifficulty=%d\n", s_levelName[level], level);
    // (the camera's view the player chose: Options, or V in the race before)
    ReadSettings();
    fprintf(f, "# the camera's view: 0 inside the car, 1 just behind, 2 further back\ncamera=%d\n", s_camera);
    fclose(f);
    s_raceSp = sp; s_raceTrial = trial; s_raceCar = car; s_raceOver = 0; s_raceLevel = level; s_trialKept = 0;
    if (watch != s_raceWatch) snprintf(s_raceWatch, sizeof(s_raceWatch), "%s", watch ? watch : "");
    Log_Info("[racegame] %s: %s with %s%s%s%s", s->Name, watch ? "its ghost played back" : trial ? "a time trial" : "a race", car ? car->Name : "Twinsen's car",
             level >= 0 && level <= 2 ? ", difficulty " : "", level >= 0 && level <= 2 ? s_levelName[level] : "",
             trial || s_qualifyNow < 0 ? "" : s_qualifyNow ? ", a qualifying lap first" : ", no qualifying: from the back of the grid");
    s_raceQualify = trial ? -1 : s_qualifyNow;
    s_qualifyNow = -1;
    RaceMod_LoadFile(out);
    // (the first run: the controls and the power-ups before the race -- once)
    Introduction(sp, trial);
    return 1;
}

// ---- the menu ----------------------------------------------------------------------------------------------------------------------

// The ghost page's track and what it has (GhostPage): the laps of the player's best and the game's three (0: none), and how many beaten
static S32 s_gpLap[5];
// ... and the other players' ghosts of it (RaceMod_GhostOthers: the "Other" row says how many; OthersPage lists them)
static T_RACE_GHOST_OTHER s_others[RG_OTHERS_SHOWN];
static int s_nbOthers = 0, s_gpOthers = 0;
static int s_gpBeaten = 0;
static int GhostLevelOpen(int level) { return level == 0 || s_gpBeaten >= level; }   // (0 Easy, 1 Medium, 2 Hard, 3 Extreme)
// A ghost page row's place in s_gpLap: 1 Easy .. 4 Extreme (0 the player's own)
static int GhostRowK(S32 num) { return num == RG_ID_GHOST_EXTREME ? 4 : 1 + num - RG_ID_GHOST_EASY; }
// The player's ghost's name: "Your best lap", "Your recorded lap" when a faster lap of the list has none (driven before ghosts were
// kept), or "Shared ghost" when one shared with them (their GHOSTS folder) is quicker than theirs
static const char *OwnLapName(int sp, S32 lap, S32 mine) { return !mine ? "Shared ghost" : sp < s_nbSp && s_nbTimes[sp] && s_times[sp][0].Lap < lap ? "Your recorded lap" : "Your best lap"; }
static const char *s_gpOwnName = "Your best lap";

S32 RaceGame_RowDisabled(S32 num) {
    if (!s_on) return 0;
    if (num == RG_ID_GHOST_OWN) return !s_gpLap[0];
    if ((num >= RG_ID_GHOST_EASY && num <= RG_ID_GHOST_HARD) || num == RG_ID_GHOST_EXTREME) return !s_gpLap[GhostRowK(num)] || !GhostLevelOpen(GhostRowK(num) - 1);
    return (num == RG_ID_STORY && !s_story) || (num == RG_ID_TRACKS && (!s_tracks || !s_nbTracks));
}

static void Label(char *string, const char *utf8) { CopyUtf8ToCp850(string, 256, utf8); }

S32 RaceGame_MenuText(S32 num, char *string) {
    if (!s_on || num < RG_ID_STORY || num >= RG_ID_STORY + 100) return 0;
    switch (num) {
    case RG_ID_STORY: Label(string, "Story Mode"); return 1;
    case RG_ID_TRACKS: Label(string, "Track select"); return 1;
    case RG_ID_RESTART: Label(string, s_raceWatch[0] ? "Watch again from the start" : s_raceTrial ? "Restart the time trial" : "Restart the race"); return 1;
    case RG_ID_SPECIALS: Label(string, "Specials"); return 1;
    case RG_ID_TIMES: Label(string, "Best laps"); return 1;
    case RG_ID_RACE: Label(string, "Race"); return 1;
    case RG_ID_TRIAL: Label(string, "Time trial"); return 1;
    case RG_ID_QUALIFY: Label(string, "Qualifying lap"); return 1;
    case RG_ID_SKIP: Label(string, "Skip: start at the back of the grid"); return 1;
    case RG_ID_POWERUPS: Label(string, "Power-ups"); return 1;
    case RG_ID_CONTROLS: Label(string, "Race controls, power-ups"); return 1;   // (the game's own Controls row changes the keys)
    case RG_ID_UPDATES: Label(string, "Camera, window, updates"); return 1;     // (the row's width: "Window mode, mini-map, updates" ran over it)
    case RG_ID_CAMERA: Label(string, s_cameraLabel[s_camera >= 0 && s_camera <= 2 ? s_camera : 2]); return 1;
    case RG_ID_MINIMAP: Label(string, s_minimap ? "Mini-map: On" : "Mini-map: Off"); return 1;
    case RG_ID_UPDATE_MODE: Label(string, s_updateLabel[s_updates >= 0 && s_updates <= 2 ? s_updates : 0]); return 1;
    case RG_ID_UPDATE_NOW: Label(string, "Check for updates now"); return 1;
    case RG_ID_WINDOW: Label(string, DisplayFullScreen ? "Window mode: full screen (F11)" : "Window mode: window (F11)"); return 1;
    case RG_ID_EASY: case RG_ID_MEDIUM: case RG_ID_HARD: Label(string, s_levelName[num - RG_ID_EASY]); return 1;
    case RG_ID_GHOST_NONE: Label(string, "No ghost"); return 1;
    case RG_ID_GHOST_OTHER: {
        char line[64];
        if (s_gpOthers > 0) snprintf(line, sizeof(line), "Other people's ghosts (%d)", s_gpOthers);
        else snprintf(line, sizeof(line), "Other people's ghosts");
        Label(string, line);
        return 1;
    }
    case RG_ID_GHOST_IMPORT: Label(string, "Import ghosts..."); return 1;
    case RG_ID_GHOST_SHARE: Label(string, "Share my ghosts..."); return 1;
    case RG_ID_OTHER_FIRST: case RG_ID_OTHER_FIRST + 1: case RG_ID_OTHER_FIRST + 2: case RG_ID_OTHER_FIRST + 3: case RG_ID_OTHER_FIRST + 4: {
        // (whose, and the lap: a long name cut to fit the row)
        const T_RACE_GHOST_OTHER *o = &s_others[num - RG_ID_OTHER_FIRST];
        char who[24], time[24], line[64];
        snprintf(who, sizeof(who), "%s", o->Driver);
        if (strlen(o->Driver) >= sizeof(who) - 1) snprintf(who + sizeof(who) - 4, 4, "...");
        TimeText(time, sizeof(time), o->Lap);
        snprintf(line, sizeof(line), "%s  %s", who, time);
        Label(string, line);
        return 1;
    }
    case RG_ID_LAPS_1: case RG_ID_LAPS_1 + 1: case RG_ID_LAPS_1 + 2: case RG_ID_LAPS_1 + 3: {
        char line[32];
        const int n = s_trialLapsChoice[num - RG_ID_LAPS_1];
        snprintf(line, sizeof(line), s_lapsRuns ? (n == 1 ? "%d run" : "%d runs") : n == 1 ? "%d lap" : "%d laps", n);
        Label(string, line);
        return 1;
    }
    case RG_ID_LAPS_ENDLESS: Label(string, "Endless: until you stop"); return 1;
    case RG_ID_GHOST_OWN:
    case RG_ID_GHOST_EASY: case RG_ID_GHOST_MEDIUM: case RG_ID_GHOST_HARD: case RG_ID_GHOST_EXTREME: {
        // (with its lap's time; a level not open yet: what opens it)
        const char *const what[5] = {s_gpOwnName, "Easy ghost", "Medium ghost", "Hard ghost", "Extreme ghost"};
        static const char *const before[5] = {"", "", "Easy", "Medium", "Hard"};
        const int k = num == RG_ID_GHOST_OWN ? 0 : GhostRowK(num);
        char line[96], time[24];
        if (k >= 2 && !GhostLevelOpen(k - 1)) snprintf(line, sizeof(line), "%s: beat %s", what[k], before[k]);
        else if (!s_gpLap[k]) snprintf(line, sizeof(line), k ? "%s: none" : "Your best lap: none yet", what[k]);
        else { TimeText(time, sizeof(time), s_gpLap[k]); snprintf(line, sizeof(line), "%s  %s", what[k], time); }
        Label(string, line);
        return 1;
    }
    }
    if (num >= RG_ID_SPECIAL && num < RG_ID_SPECIAL + s_nbSpecials) { Label(string, s_sp[s_spIndex[num - RG_ID_SPECIAL]].Name); return 1; }
    string[0] = 0;
    return 1;
}

static void SetRow(U16 *menu, S32 row, U16 id) {
    menu[4 + row * 2 + 0] = 0;
    menu[4 + row * 2 + 1] = id;
}

// A page of the menu: the menu's backdrop put back (the menu's picture, or the scene shaded behind it), a header line over the rows.
static void Page(const char *header, S32 y) {
    CopyScreen(Screen, Log);
    BoxStaticAdd(0, 0, (S32)ModeDesiredX - 1, (S32)ModeDesiredY - 1);
    if (header && header[0]) {
        char text[256];
        Label(text, header);
        DrawSingleString(UI_CENTER_X, y + UI_VCENTER_OFS, text);
    }
}

// DoGameMenu for a page of the race game's: MENU_RELAYOUT when the window was resized and the picture fitted to it -- the backdrop made
// again at the new size, the page (its header, its text) to be drawn again over it before the menu is asked again (each page's loop
// goes round for any row it doesn't know)
static S32 Menu(U16 *menu) {
    const S32 was = MenuRelayoutReturns;
    MenuRelayoutReturns = 1;
    const S32 select = DoGameMenu(menu);
    MenuRelayoutReturns = was;
    return select;
}

// A page's own loop (the car, Best laps, the controls and power-ups): the window resized and the picture fitted to it -- the backdrop
// made again at the new size, the page to be laid out and drawn again (non-zero)
static int Refitted(void) {
    if (!Res_ApplyWindowFit()) return 0;
    Menu_RepaintAfterWindowFit();
    Page(NULL, 0);
    return 1;
}

// A line of text centred on x, with the menus' dark shadow.
static void Text(S32 x, S32 y, const char *utf8, S32 colour) {
    char text[256];
    Label(text, utf8);
    const S32 w = SizeFont(text);
    ColorFont(BLACK);
    Font(x - w / 2 + 3, y + 3, text);
    ColorFont(colour);
    Font(x - w / 2, y, text);
}

// The same no wider than `width`: on two lines (y the first's, the second's a line under it) broken at the space nearest its middle when
// it doesn't fit on one -- else on one, a line down.
static void TextFitted(S32 x, S32 y, S32 width, const char *utf8, S32 colour) {
    char text[256];
    Label(text, utf8);
    const size_t n = strlen(utf8);
    if (SizeFont(text) <= width || n < 3) { Text(x, y + 13, utf8, colour); return; }
    size_t cut = 0;
    for (size_t i = 1; i + 1 < n; i++)
        if (utf8[i] == ' ' && (!cut || (i > n / 2 ? i - n / 2 : n / 2 - i) < (cut > n / 2 ? cut - n / 2 : n / 2 - cut))) cut = i;
    if (!cut) { Text(x, y + 13, utf8, colour); return; }
    char first[256];
    snprintf(first, sizeof(first), "%.*s", (int)cut, utf8);
    Text(x, y, first, colour);
    Text(x, y + 26, utf8 + cut + 1, colour);
}

// A key that steps a page's choice (the arrows, the pad's d-pad or stick, edge-only as DoGameMenu has them): -1 left, 1 right, 0 none.
// (the pad's way followed every pass: the pass it is pushed, MyKey holds the pad's own scancode, K_GAMEPAD_BASE up, for "press any
// key" -- the way left unrecorded then, the next pass took it for a new push and stepped again: "it skips over 1 car in every
// horizontal step", a player on a controller, 2026-10-26)
static S32 Step(S32 *padDir) {
    S32 key = MyKey >= K_GAMEPAD_BASE ? 0 : MyKey;
    const S32 raw = JoystickMenuNavOnly();
    if (raw != *padDir) {
        *padDir = raw;
        if (!key && raw == K_LEFT) key = K_GRAY_LEFT;
        else if (!key && raw == K_RIGHT) key = K_GRAY_RIGHT;
    }
    if (Input & I_LEFT || key == K_GRAY_LEFT) { InitWaitNoInput(I_LEFT); InitWaitNoKey(); return -1; }
    if (Input & I_RIGHT || key == K_GRAY_RIGHT) { InitWaitNoInput(I_RIGHT); InitWaitNoKey(); return 1; }
    return 0;
}

// A mouse button let go this pass (the menus' mouse on): 1 the left, 2 the right; where, in (*x, *y). s_clickWas primed as a page opens.
static S32 s_clickWas = 0;
static int Clicked(S32 *x, S32 *y) {
    const S32 now = Click & 3;
    const S32 released = s_clickWas & ~now;
    s_clickWas = now;
    if (!released || !FlagMenuMouse) return 0;
    *x = (S32)MouseX;
    *y = (S32)MouseY;
    return released & 1 ? 1 : 2;
}

static int Confirmed(void) { return Input & I_RETURN || Input & I_ACTION_M || MyKey == K_ENTER || MyKey == K_NUMPAD_ENTER || MyKey == K_SPACE; }
static int Escaped(void) { return MyKey == K_ESC || Input & I_MENUS; }

#define RG_ORANGE 243                       // the palette's fire orange, for the pages' headers
#define RG_GREY (3 * 16 + 11)               // the greyed rows' colour (GAMEMENU.CPP COUL_TEXT_MENU_DISABLE)

// The palette's colour nearest to (r, g, b) (0..255 each), in the menus' palette (LBA2's own: the menu's picture is drawn in it)
static U8 NearestColour(int r, int g, int b) {
    const U8 *pal = PtrPalNormal ? PtrPalNormal : PtrPal;
    if (!pal) return LBAWHITE;
    int best = 0;
    long bd = 0x7FFFFFFF;
    for (int j = 1; j < 256; j++) {
        const long dr = pal[j * 3] - r, dg = pal[j * 3 + 1] - g, db = pal[j * 3 + 2] - b;
        const long d = 3 * dr * dr + 4 * dg * dg + 2 * db * db;
        if (d < bd) { bd = d; best = j; }
    }
    return (U8)best;
}

// A five-pointed star, point up, centred on (cx, cy), `r` its points' radius: filled `fill` (0 to 1, from the left: half a star is its
// left half) in gold, the rest dim, with the menus' dark shadow under it. Drawn straight into the page (Log), clipped to the screen.
#define RG_STAR_R 11                        // a star's radius
#define RG_STAR_STEP 26                     // ... one to the next
static void Star(S32 cx, S32 cy, S32 r, double fill, U8 gold, U8 dim, U8 shadow) {
    double px[10], py[10];
    for (int k = 0; k < 10; k++) {
        const double a = -3.14159265358979 / 2 + k * 3.14159265358979 / 5, rr = k & 1 ? r * 0.42 : r;
        px[k] = rr * cos(a);
        py[k] = rr * sin(a);
    }
    const S32 split = (S32)floor(-r + fill * 2 * r + 0.5);
    for (int pass = 0; pass < 2; pass++) {
        const S32 off = pass ? 0 : 2;       // (the shadow first, 2 pixels down and right)
        for (S32 dy = -r - 1; dy <= r + 1; dy++)
            for (S32 dx = -r - 1; dx <= r + 1; dx++) {
                const S32 x = cx + dx + off, y = cy + dy + off;
                if (x < 0 || y < 0 || x >= (S32)ModeDesiredX || y >= (S32)ModeDesiredY) continue;
                // (the pixel's centre inside the star: even-odd crossings of its ten edges)
                const double tx = dx, ty = dy;
                int inside = 0;
                for (int i = 0, j = 9; i < 10; j = i++)
                    if ((py[i] > ty) != (py[j] > ty) && tx < (px[j] - px[i]) * (ty - py[i]) / (py[j] - py[i]) + px[i]) inside = !inside;
                if (!inside) continue;
                ((U8 *)Log)[TabOffLine[y] + x] = pass ? (dx < split ? gold : dim) : shadow;
            }
    }
}

// A car's stats under its name: a row each -- its label, then five stars, full, half or empty -- the rows `step` apart from y. The
// rows' labels and stars a block centred on cx.
static const char *const s_statName[3] = {"Acceleration", "Top speed", "Handling"};
static void TextAt(S32 x, S32 y, const char *cp850, S32 colour);
// (the stars' radius, one to the next, and the gap after the labels: the car grid's smaller -- StatsSized)
static void StatsSized(S32 cx, S32 y, S32 step, const T_RG_CAR *c, S32 starR, S32 starStep, S32 gap);
static void Stats(S32 cx, S32 y, S32 step, const T_RG_CAR *c) { StatsSized(cx, y, step, c, RG_STAR_R, RG_STAR_STEP, 18); }
static void StatsSized(S32 cx, S32 y, S32 step, const T_RG_CAR *c, S32 starR, S32 starStep, S32 gap) {
    static U8 gold = 0, dim = 0, shadow = 0;
    static const U8 *paletteWas = NULL;
    const U8 *pal = PtrPalNormal ? PtrPalNormal : PtrPal;
    if (pal != paletteWas) {
        paletteWas = pal;
        gold = NearestColour(255, 200, 40);
        dim = NearestColour(64, 58, 52);
        shadow = NearestColour(0, 0, 0);
    }
    char text[256];
    S32 labels = 0;
    for (int k = 0; k < 3; k++) {
        Label(text, s_statName[k]);
        const S32 w = SizeFont(text);
        if (w > labels) labels = w;
    }
    const S32 stars = 4 * starStep + 2 * starR + 2, x0 = cx - (labels + gap + stars) / 2, sx = x0 + labels + gap + starR;
    for (int k = 0; k < 3; k++) {
        Label(text, s_statName[k]);
        TextAt(x0, y + k * step, text, RG_GREY);
        // (the stars' middle level with the text's: its capitals about 26 pixels tall from where it is drawn)
        const S32 sy = y + k * step + 13;
        for (int n = 0; n < 5; n++) {
            const double fill = c->Stars[k] - n;
            Star(sx + n * starStep, sy, starR, fill <= 0 ? 0 : fill >= 1 ? 1 : fill, gold, dim, shadow);
        }
    }
}

// The car: its body turning on the page, its name under it, its stats (when RACEGAME.TXT gives them) under that; left and right go
// through the cars, Enter takes it, Esc goes back. The car taken (its index), or -1.
// (2026-10-20, the stats' three rows: the box 156 tall where it was 200, under the menu picture's title as before; what it is for in
// the header, "Choose your car for a race", and the car's number in the box's top right corner, where the line "Car 1 of 12, for a
// race" was under the name -- the keys still on the 480-tall canvas)
#define RG_CAR_BOTTOM 286
#define RG_STAT_LINE 26
static void Padlock(S32 cx, S32 cy);
static int CarPage(const char *what, int trial) {
    if (s_nbCars <= 0) return -1;
    if (s_car < 0 || s_car >= s_nbCars) s_car = 0;
    // (2026-10-20: a car locked -- the pumpkin F1 until a race is won -- or for time trials only is shown but can't be taken: the page
    // opens on the car chosen last if it can be, else on the first that can, the Halloween buggy)
    if (!CarFor(s_car, trial)) {
        s_car = 0;
        while (s_car + 1 < s_nbCars && !CarFor(s_car, trial)) s_car++;
        if (!CarFor(s_car, trial)) s_car = 0;
    }
    Page(NULL, 0);
    BoxUpdate();
    ManageTime();
    InitWaitNoKey();
    InitWaitNoInput(I_JOY | I_FIRE | I_MENUS | I_ACTION);
    S32 padDir = JoystickMenuNavOnly();
    AsciiMode = TRUE;
    S32 cx = UI_CENTER_X, oy = UI_VCENTER_OFS;
    // (the cars with stats: the box higher and shorter, their three rows under the name -- a list without them laid out as before)
    int rated = 0;
    for (int i = 0; i < s_nbCars; i++) rated |= s_cars[i].Rated;
    const S32 boxTop = 130, boxBottom = rated ? RG_CAR_BOTTOM : 330;
    S32 x0 = cx - 230, x1 = cx + 230, y0 = boxTop + oy, y1 = boxBottom + oy;
    int chosen = -2;
    S32 start = TimerRefHR;
    s_clickWas = Click & 3;
    while (chosen == -2) {
        ManageTime();
        MyGetInput();
        // (the window resized: the page laid out again at the new size)
        if (Refitted()) {
            cx = UI_CENTER_X; oy = UI_VCENTER_OFS;
            x0 = cx - 230; x1 = cx + 230; y0 = boxTop + oy; y1 = boxBottom + oy;
        }
        S32 step = Step(&padDir);
        // (the mouse: a click on the box's left or right quarter the car before or after, in its middle this one; the right button back)
        S32 mx, my;
        const int click = Clicked(&mx, &my);
        if (click == 2) chosen = -1;
        else if (click == 1 && my >= y0 && my <= y1 && mx >= x0 && mx <= x1) {
            if (mx < x0 + (x1 - x0) / 4) step = -1;
            else if (mx > x1 - (x1 - x0) / 4) step = 1;
            else if (CarFor(s_car, trial)) { WaitNoInput(); chosen = s_car; }
        }
        if (step) { s_car = (s_car + step + s_nbCars) % s_nbCars; start = TimerRefHR; }
        // (a car not seen yet measured first, under the backdrop drawn next: RaceMod_DrawBody)
        RaceMod_DrawBody(x0 + 2, y0 + 2, x1 - 2, y1 - 2, s_cars[s_car].Body, 0, 1, s_cars[s_car].Anim);
        // the page: the backdrop, the header, the box with the car, its name and the keys
        const S32 last = rated ? y1 + 3 * RG_STAT_LINE + 66 : y1 + 82;   // (the keys' line)
        const S32 top = y0 - 36, bottom = last + 38 < (S32)ModeDesiredY ? last + 38 : (S32)ModeDesiredY - 1;
        CopyBlock(0, top, (S32)ModeDesiredX - 1, bottom, Screen, 0, top, Log);
        char header[96];
        snprintf(header, sizeof(header), rated ? "Choose your car for %s" : "Choose your car", what);
        Text(cx, y0 - 34, header, RG_ORANGE);
        ShadeBoxBlk(x0, y0, x1, y1, MENU_SHADE_LVL);
        RaceMod_DrawBody(x0 + 2, y0 + 2, x1 - 2, y1 - 2, s_cars[s_car].Body, (S32)(((TimerRefHR - start) / 3 + 3072) & 4095), 0, s_cars[s_car].Anim);
        // (a car locked: dark, a padlock over it and how it is won; one for time trials only, on the page for a race: dimmed, and so said)
        const int locked = CarLocked(s_car), takeable = CarFor(s_car, trial);
        if (!takeable) ShadeBoxBlk(x0 + 2, y0 + 2, x1 - 2, y1 - 2, locked ? 10 : 7);
        if (locked) {
            Padlock(cx, (y0 + y1) / 2 - 28);
            TextFitted(cx, y1 - 62, x1 - x0 - 24, s_cars[s_car].Hint[0] ? s_cars[s_car].Hint : "Win a race to unlock", RG_ORANGE);
        } else if (!takeable) Text(cx, y1 - 38, "For time trials", RG_ORANGE);
        DrawCadre(x0, y0, x1, y1, ALL_ANGLES);
        char name[160];
        snprintf(name, sizeof(name), "%s", s_cars[s_car].Name);
        Text(cx, y1 + 8, name, takeable ? LBAWHITE : RG_GREY);
        char count[64];
        if (rated) {
            if (s_cars[s_car].Rated) Stats(cx, y1 + 42, RG_STAT_LINE, &s_cars[s_car]);
            char text[256];
            snprintf(count, sizeof(count), "%d of %d", s_car + 1, s_nbCars);
            Label(text, count);
            TextAt(x1 - 12 - SizeFont(text), y0 + 6, text, RG_GREY);
        } else {
            snprintf(count, sizeof(count), "Car %d of %d, for %s", s_car + 1, s_nbCars, what);
            Text(cx, y1 + 46, count, RG_GREY);
        }
        Text(cx, last, takeable ? "Left, right: car    Enter: race    Esc: back" : "Left, right: car    Esc: back", RG_GREY);
        BoxStaticAdd(0, top, (S32)ModeDesiredX - 1, bottom);
        BoxUpdate();
        if (chosen != -2) break;
        if (Confirmed()) { WaitNoInput(); if (takeable) chosen = s_car; }
        else if (Escaped()) chosen = -1;
    }
    AsciiMode = FALSE;
    Page(NULL, 0);
    return chosen;
}

// Track select open: on (tracks=1, with tracks), and its code given this run (or none asked: no tracks_code=).
static int TracksOpen(void) { return s_tracks && s_nbTracks > 0 && (!s_tracksCode[0] || s_tracksOpen); }
// A special or track Best laps shows: Track select's only once it is open.
static int TimesShown(int sp) { return sp >= 0 && sp < s_nbSp && (!s_sp[sp].Track || TracksOpen()); }

// ---- Best laps' last page: every track's best lap, and their sum (2026-10-27) --------------------------------------------------------
// The user: "Under "Best Laps" Let's add a new page in at the end of loop so it's page 17 that's a list of each track name, along with a
// users best time, then at the bottom show the total time to complete all the laps. Also add a button in that generates an image to the
// clipboard in the format "Track name", "Time", "Car used", along with a sum of best at the bottom of the image"; then "If a user selects
// their sum of best, then play back all track ghosts in order".

// A line two-thirds the font's size (the font has one size: drawn full size where it goes, read back over a colour the text hasn't got,
// the picture put back, then drawn two-thirds down, nearest pixel -- as RACEMOD.CPP's display lines are). `align`: -1 from x, 0 centred
// on it, 1 ending at it.
#define RG_SMALL_NUM 2
#define RG_SMALL_DEN 3
#define RG_SMALL_H 40
static void SmallText(S32 x, S32 y, const char *utf8, S32 colour, int align) {
    static U8 saved[RG_SMALL_H][1024], drawn[RG_SMALL_H][1024];
    char buf[256];
    Label(buf, utf8);
    const S32 fw = SizeFont(buf) + 4, fh = RG_SMALL_H, sw = fw * RG_SMALL_NUM / RG_SMALL_DEN, sh = fh * RG_SMALL_NUM / RG_SMALL_DEN;
    const S32 W = (S32)ModeDesiredX, H = (S32)ModeDesiredY;
    const S32 sx0 = align < 0 ? x : align > 0 ? x - sw : x - sw / 2;
    S32 px0 = sx0, py0 = y;
    if (px0 + fw > W) px0 = W - fw;
    if (py0 + fh > H) py0 = H - fh;
    if (fw > 1024 || px0 < 0 || py0 < 0) return;
    const U8 none = (U8)(colour == 1 || colour == 0 ? 2 : 1);
    for (S32 r = 0; r < fh; r++) {
        U8 *row = (U8 *)Log + TabOffLine[py0 + r] + px0;
        memcpy(saved[r], row, (size_t)fw);
        memset(row, none, (size_t)fw);
    }
    ColorFont(BLACK);
    Font(px0 + 3, py0 + 3, buf);
    ColorFont(colour);
    Font(px0, py0, buf);
    for (S32 r = 0; r < fh; r++) {
        U8 *row = (U8 *)Log + TabOffLine[py0 + r] + px0;
        memcpy(drawn[r], row, (size_t)fw);
        memcpy(row, saved[r], (size_t)fw);
    }
    for (S32 r = 0; r < sh; r++) {
        const S32 dy = y + r;
        if (dy < 0 || dy >= H) continue;
        U8 *row = (U8 *)Log + TabOffLine[dy];
        const U8 *src = drawn[r * RG_SMALL_DEN / RG_SMALL_NUM];
        for (S32 c = 0; c < sw; c++) {
            const S32 dx = sx0 + c;
            const U8 v = src[c * RG_SMALL_DEN / RG_SMALL_NUM];
            if (v != none && dx >= 0 && dx < W) row[dx] = v;
        }
    }
}
// ... cut with "..." to no wider than `width` (at its small size)
static void SmallFitted(S32 x, S32 y, S32 width, const char *utf8, S32 colour, int align) {
    char text[256], cut[260], probe[256];
    snprintf(text, sizeof(text), "%s", utf8);
    snprintf(cut, sizeof(cut), "%s", text);
    Label(probe, text);
    size_t n = strlen(text);
    while (n > 4 && SizeFont(probe) * RG_SMALL_NUM / RG_SMALL_DEN > width) {
        n--;
        while (n > 0 && (text[n] & 0xC0) == 0x80) n--;      // (not inside a UTF-8 letter)
        snprintf(cut, sizeof(cut), "%.*s...", (int)n, text);
        Label(probe, cut);
    }
    SmallText(x, y, cut, colour, align);
}

// The player's name, as Share my ghosts keeps it (GHOST_NAME.TXT beside the ghosts); "" none
static void GhostOwnerName(char *out, size_t size) {
    out[0] = 0;
    char path[ADELINE_MAX_PATH];
    GetCfgPath(path, ADELINE_MAX_PATH, "GHOST_NAME.TXT");
    FILE *f = fopen(path, "r");
    if (!f) return;
    if (fgets(out, (int)size, f)) {
        size_t n = strlen(out);
        while (n && (out[n - 1] == '\n' || out[n - 1] == '\r' || out[n - 1] == ' ')) out[--n] = 0;
    }
    fclose(f);
}

// The sum of best: every track shown's best lap added up (ms), and how many have none
static S32 SumOfBest(int *missing) {
    S32 sum = 0;
    *missing = 0;
    for (int sp = 0; sp < s_nbSp; sp++) {
        if (!TimesShown(sp)) continue;
        if (s_nbTimes[sp] > 0) sum += s_times[sp][0].Lap;
        else (*missing)++;
    }
    return sum;
}

// The list drawn in (x0..x1) from y: each track shown, its best lap -- and with `cars`, the car it was driven in (the picture) -- rows
// `step` apart. The y under the last.
static S32 AllBestRows(S32 x0, S32 x1, S32 y, S32 step, int cars, S32 nameW = 190) {
    const S32 timeX = cars ? x0 + 24 + nameW + 84 : x1 - 24, carX = timeX + 22;
    for (int sp = 0; sp < s_nbSp; sp++) {
        if (!TimesShown(sp)) continue;
        char time[24];
        if (s_nbTimes[sp] > 0) TimeText(time, sizeof(time), s_times[sp][0].Lap);
        else snprintf(time, sizeof(time), "--:--.--");
        SmallFitted(x0 + 24, y, cars ? nameW : x1 - x0 - 150, s_sp[sp].Name, LBAWHITE, -1);
        SmallText(timeX, y, time, s_nbTimes[sp] > 0 ? RG_ORANGE : RG_GREY, 1);
        if (cars && s_nbTimes[sp] > 0) SmallFitted(carX, y, x1 - 16 - carX, s_times[sp][0].Car, LBAWHITE, -1);
        y += step;
    }
    return y;
}

// The picture of it (Copy as an image): drawn over the screen, taken, saved as "Best laps.png" beside the settings and put on the
// clipboard; the page is drawn again after. Non-zero: on the clipboard.
static int AllBestPicture(void) {
    const S32 cx = UI_CENTER_X, oy = UI_VCENTER_OFS;
    // (as wide as the screen gives, up to 800: the tracks' and the cars' names whole)
    const S32 W = (S32)ModeDesiredX - 16 < 800 ? (S32)ModeDesiredX - 16 : 800, nameW = W >= 740 ? 290 : 190;
    const S32 x0 = cx - W / 2, x1 = cx + W / 2, y0 = 8 + oy;
    int shownTracks = 0;
    for (int sp = 0; sp < s_nbSp; sp++) shownTracks += TimesShown(sp);
    const S32 step = 20, y1 = y0 + 100 + shownTracks * step + 46;
    if (x0 < 0 || y1 >= (S32)ModeDesiredY || !Log || !PtrPalNormal) return 0;
    Page(NULL, 0);
    ShadeBoxBlk(x0, y0, x1, y1, MENU_SHADE_LVL);
    DrawCadre(x0, y0, x1, y1, ALL_ANGLES);
    char name[64], title[128];
    GhostOwnerName(name, sizeof(name));
    if (name[0]) snprintf(title, sizeof(title), "%s's best laps", name);
    else snprintf(title, sizeof(title), "Best laps");
    Text(cx, y0 + 8, title, RG_ORANGE);
    SmallText(cx, y0 + 44, "Little Big Halloween Racing Adventure", RG_GREY, 0);
    SmallText(x0 + 24, y0 + 74, "Track name", RG_GREY, -1);
    SmallText(x0 + 24 + nameW + 84, y0 + 74, "Time", RG_GREY, 1);
    SmallText(x0 + 24 + nameW + 106, y0 + 74, "Car used", RG_GREY, -1);
    const S32 y = AllBestRows(x0, x1, y0 + 100, step, 1, nameW);
    int missing;
    const S32 sum = SumOfBest(&missing);
    char time[24], line[96];
    TimeText(time, sizeof(time), sum);
    if (missing) snprintf(line, sizeof(line), "Sum of best  %s  (%d without a lap)", time, missing);
    else snprintf(line, sizeof(line), "Sum of best  %s", time);
    Text(cx, y + 6, line, RG_ORANGE);
    // taken: the box's pixels, through the menus' palette
    const S32 w = x1 - x0 + 1, h = y1 - y0 + 1;
    U8 *idx = (U8 *)malloc((size_t)w * h), *rgb = (U8 *)malloc((size_t)w * h * 3);
    int ok = 0;
    if (idx && rgb) {
        for (S32 r = 0; r < h; r++) memcpy(idx + (size_t)r * w, (U8 *)Log + TabOffLine[y0 + r] + x0, (size_t)w);
        for (S32 i = 0; i < w * h; i++) memcpy(rgb + (size_t)i * 3, PtrPalNormal + idx[i] * 3, 3);
        char path[ADELINE_MAX_PATH];
        GetCfgPath(path, ADELINE_MAX_PATH, "Best laps.png");
        // (SavePNG reads its rows a screen's width apart)
        U8 *rows = (U8 *)malloc((size_t)ModeDesiredX * h);
        if (rows) {
            for (S32 r = 0; r < h; r++) memcpy(rows + (size_t)r * ModeDesiredX, idx + (size_t)r * w, (size_t)w);
            SavePNG(path, rows, w, h, PtrPalNormal);
            free(rows);
        }
        ok = RaceUpdate_ClipboardImage(rgb, w, h, path);
        Log_Info("[racegame] Best laps: the sum of best %s as a picture (%d x %d) -- %s, saved as %s", time, (int)w, (int)h,
                 ok ? "on the clipboard" : "NOT on the clipboard", path);
    }
    free(idx);
    free(rgb);
    return ok;
}

// "Watch the sum of best": the player's best lap of every track played back, one after another -- each once (RACEMOD.CPP GhostWatch asks
// RaceGame_WatchDone at its end), then the next track's.
static int s_watchList[RG_MAX_ENTRIES], s_watchListN = 0, s_watchListAt = 0, s_watchNext = 0;
static char s_watchListPath[RG_MAX_ENTRIES][ADELINE_MAX_PATH];

static int WatchListStart(void) {
    s_watchListN = s_watchListAt = s_watchNext = 0;
    for (int sp = 0; sp < s_nbSp && s_watchListN < RG_MAX_ENTRIES; sp++) {
        S32 lap = 0, mine = 1;
        if (!TimesShown(sp) || !RaceMod_GhostFind(s_sp[sp].Name, 1, s_watchListPath[s_watchListN], ADELINE_MAX_PATH, &lap, NULL, 0, &mine, NULL, 0) || lap <= 0)
            continue;
        s_watchList[s_watchListN++] = sp;
    }
    Log_Info("[racegame] Best laps: the sum of best played back -- %d track(s) with a best lap of the player's", s_watchListN);
    if (!s_watchListN) return 0;
    s_lastSp = s_watchList[0];
    return StartRace(s_watchList[0], 1, NULL, -1, s_watchListPath[0]);
}

// RACEMOD.CPP: a ghost played back has come to its lap's end. Non-zero: one of the sum of best's -- the race goes back to the menu, which
// starts the next (RaceGame_Menu); zero: it goes round again.
S32 RaceGame_WatchDone(void) {
    if (!RaceGame_On() || s_watchListN <= 0) return 0;
    if (s_watchListAt + 1 >= s_watchListN) {
        Log_Info("[racegame] the sum of best played back: the last track's done");
        s_watchListN = 0;
        return 1;
    }
    s_watchNext = 1;
    return 1;
}

// Best laps (2026-10-25, the user: "Let's simplify the Best laps screen, let's show the users top 5 times for each track. At the bottom have a
// watch section, where they can either watch their best time, or watch a ghost, and when "Watch ghost" is selected left/right goes from
// easy/medium/hard in a loop. If the user has the track name selected, then left/right cycle through tracks instead"): the track's five
// fastest laps, and under them Watch -- the player's best lap, or one of the game's ghosts. Up and down choose the track's row or one of
// the two to watch; left and right change the track, or the ghost (Easy, Medium, Hard, round); Enter watches it (non-zero: the race game
// starts it). Esc or a click goes back.
#define RG_TIMES_SHOWN 5
static int TimesPage(void) {
    ReadTimes();
    Page(NULL, 0);
    BoxUpdate();
    ManageTime();
    InitWaitNoKey();
    InitWaitNoInput(I_JOY | I_FIRE | I_MENUS | I_ACTION);
    S32 padDir = JoystickMenuNavOnly();
    AsciiMode = TRUE;
    // (the track raced or chosen last -- a special, or one of Track select's once it is open)
    int sp = s_raceSp >= 0 && TimesShown(s_raceSp) ? s_raceSp : TimesShown(s_lastSp) ? s_lastSp : 0, flag = 1, done = 0;
    while (sp + 1 < s_nbSp && !TimesShown(sp)) sp++;
    int shown = 0;
    for (int i = 0; i < s_nbSp; i++) shown += TimesShown(i);
    // (and after them, with two or more, the page of every track's best lap: page s_nbSp -- "page 17" with Track select open)
    const int all = s_nbSp;
    const int pages = shown + (shown > 1);
    int copied = 0;                         // (the picture just put on the clipboard: said under the keys till the page changes)
    S32 cx = UI_CENTER_X, oy = UI_VCENTER_OFS;
    S32 x0 = cx - 280, x1 = cx + 280, y0 = 20 + oy, y1 = 466 + oy;
    s_clickWas = Click & 3;
    static const char *const levelName[4] = {"Easy", "Medium", "Hard", "Extreme"};
    // (the track's ghosts: the player's best lap, the game's four -- looked for as the page turns to it)
    char ownPath[ADELINE_MAX_PATH], levelPath[4][ADELINE_MAX_PATH];
    S32 ownLap = 0, ownMine = 1, levelLap[4] = {0, 0, 0, 0};
    // (the row chosen: 0 the track, 1 the player's best lap, 2 a ghost; and the ghost shown)
    int of = -1, row = 0, level = 0, watch = 0;
    while (!done) {
        ManageTime();
        MyGetInput();
        // (the window resized: the page laid out and drawn again at the new size)
        if (Refitted()) {
            cx = UI_CENTER_X; oy = UI_VCENTER_OFS;
            x0 = cx - 280; x1 = cx + 280; y0 = 20 + oy; y1 = 466 + oy;
            flag = 1;
        }
        if (sp < s_nbSp && of != sp) {
            of = sp;
            ownLap = 0;
            RaceMod_GhostFind(s_sp[sp].Name, 1, ownPath, ADELINE_MAX_PATH, &ownLap, NULL, 0, &ownMine, NULL, 0);
            for (int k = 0; k < 4; k++) {
                levelLap[k] = 0;
                RaceMod_GhostFind(s_sp[sp].Name, k < 3 ? 3 + k : 7, levelPath[k], ADELINE_MAX_PATH, &levelLap[k], NULL, 0, NULL, NULL, 0);
            }
            // (the ghost shown: the hardest the player has unlocked on the track -- Easy at first, Medium once Easy is beaten, ... Extreme
            // once Hard is: the user, 2026-10-27, "let's have the ghost time shown to be the time of the highest ghost they've unlocked")
            ReadSettings();
            const int open = s_beaten[sp] < 3 ? s_beaten[sp] : 3;
            level = -1;
            for (int k = open; k >= 0 && level < 0; k--) if (levelLap[k]) level = k;
            if (level < 0) { level = 0; for (int k = 0; k < 4; k++) if (levelLap[k]) { level = k; break; } }
            flag = 1;
        }
        const int anyGhost = sp < s_nbSp && (levelLap[0] || levelLap[1] || levelLap[2] || levelLap[3]);
        // (a row there is something on: the track always, the best lap once there is one, a ghost once there is one; on the last page the
        // sum of best -- watched -- and Copy as an image)
        int missingLaps = 0;
        const S32 sumOfBest = sp == all ? SumOfBest(&missingLaps) : 0;
        const int rowOpen[3] = {1, sp == all ? sumOfBest > 0 : ownLap > 0, sp == all ? sumOfBest > 0 : anyGhost};
        if (!rowOpen[row]) row = 0;
        S32 mx, my;
        if (Clicked(&mx, &my)) done = 1;
        // up and down: the row
        S32 dir = 0;
        if (Input & I_UP || MyKey == K_UP) { InitWaitNoInput(I_UP); InitWaitNoKey(); dir = -1; }
        else if (Input & I_DOWN || MyKey == K_DOWN) { InitWaitNoInput(I_DOWN); InitWaitNoKey(); dir = 1; }
        for (int k = 1; dir && k < 3; k++) {
            const int r = ((row + dir * k) % 3 + 3) % 3;
            if (rowOpen[r]) { row = r; flag = 1; break; }
        }
        // left and right: the track, or the ghost
        const S32 step = Step(&padDir);
        if (step && row == 0 && pages > 1) {
            do sp = (sp + step + s_nbSp + 1) % (s_nbSp + 1);
            while (!(sp == all ? shown > 1 : TimesShown(sp)));
            copied = 0;
            flag = 1;
        } else if (step && row == 2 && sp < s_nbSp) {
            for (int k = 1; k <= 4; k++) {
                const int l = ((level + step * k) % 4 + 4) % 4;
                if (levelLap[l]) { level = l; break; }
            }
            flag = 1;
        }
        if (flag) {
            flag = 0;
            Page(NULL, 0);
            ShadeBoxBlk(x0, y0, x1, y1, MENU_SHADE_LVL);
            DrawCadre(x0, y0, x1, y1, ALL_ANGLES);
            Text(cx, y0 + 8, "Best laps", RG_ORANGE);
            // (which of how many: 16 tracks once Track select is open)
            if (pages > 2) {
                int at = 0;
                for (int i = 0; i <= sp && i <= s_nbSp; i++) at += i == all ? shown > 1 : TimesShown(i);
                char count[32], text[64];
                snprintf(count, sizeof(count), "%d of %d", at, pages);
                Label(text, count);
                TextAt(x1 - 16 - SizeFont(text), y0 + 8, text, RG_GREY);
            }
            if (sp == all) {
                // the last page: every track's best lap, the sum of best -- Enter watches them all, one after another -- and the picture
                const S32 allY[3] = {y0 + 44, y0 + 344, y0 + 378};
                DrawCadre(x0 + 36, allY[row] - 5, x1 - 36, allY[row] + 31, ALL_ANGLES);
                Text(cx, allY[0], "All tracks", row == 0 ? RG_ORANGE : LBAWHITE);
                AllBestRows(x0 + 24, x1 - 24, y0 + 80, 16, 0);
                char time[24], line[96];
                // (the sum as the page is drawn: the one worked out above was for the page before a left or right)
                const S32 sumNow = SumOfBest(&missingLaps);
                TimeText(time, sizeof(time), sumNow);
                if (missingLaps) snprintf(line, sizeof(line), "Sum of best  %s  (%d without a lap)", time, missingLaps);
                else snprintf(line, sizeof(line), "Sum of best  %s", time);
                Text(cx, allY[1], line, row == 1 ? RG_ORANGE : sumNow > 0 ? LBAWHITE : RG_GREY);
                Text(cx, allY[2], copied > 0 ? "Copied to the clipboard!" : copied < 0 ? "Couldn't copy it" : "Copy as an image",
                     row == 2 ? RG_ORANGE : sumNow > 0 ? LBAWHITE : RG_GREY);
                SmallText(cx, y0 + 418, row == 0 ? "Left, right: track    Up, down: choose    Esc: back"
                                        : row == 1 ? "Enter: watch every best lap, one after another    Esc: back"
                                                   : "Enter: copy a picture of them    Esc: back", RG_GREY, 0);
                BoxStaticAdd(0, 0, (S32)ModeDesiredX - 1, (S32)ModeDesiredY - 1);
                goto drawn;
            }
            {
            // the rows that can be chosen framed when chosen: the track, the two to watch
            const S32 rowY[3] = {y0 + 44, y0 + 286, y0 + 324};
            DrawCadre(x0 + 36, rowY[row] - 5, x1 - 36, rowY[row] + 31, ALL_ANGLES);
            Text(cx, rowY[0], s_nbSp ? s_sp[sp].Name : "No tracks", row == 0 ? RG_ORANGE : LBAWHITE);
            // the five fastest laps
            if (sp < s_nbSp && !s_nbTimes[sp]) {
                Text(cx, y0 + 130, "No laps yet: drive a time trial!", RG_GREY);
                Text(cx, y0 + 166, "Its fastest lap goes here.", RG_GREY);
            }
            for (int k = 0; sp < s_nbSp && k < s_nbTimes[sp] && k < RG_TIMES_SHOWN; k++) {
                const T_RG_TIME *t = &s_times[sp][k];
                char time[24], line[200];
                TimeText(time, sizeof(time), t->Lap);
                snprintf(line, sizeof(line), "%d.  %s   %s", k + 1, time, t->Car);
                char text[256];
                Label(text, line);
                // (a long car name cut to the page's width: "Twinsen's Halloween buggy" ran past its edge)
                for (size_t n = strlen(text); n > 12 && SizeFont(text) > x1 - x0 - 96; n--) snprintf(text + n - 3, 4, "...");
                const S32 y = y0 + 92 + k * 28;
                ColorFont(BLACK);
                Font(x0 + 54 + 3, y + 3, text);
                ColorFont(k == 0 ? RG_ORANGE : LBAWHITE);
                Font(x0 + 54, y, text);
            }
            // Watch: the player's best lap, a ghost of the game's
            Text(cx, y0 + 248, "Watch", RG_ORANGE);
            char line[160], time[24];
            if (ownLap > 0) { TimeText(time, sizeof(time), ownLap); snprintf(line, sizeof(line), "%s  %s", OwnLapName(sp, ownLap, ownMine), time); }
            else snprintf(line, sizeof(line), "Your best lap: none yet");
            Text(cx, rowY[1], line, row == 1 ? RG_ORANGE : ownLap > 0 ? LBAWHITE : RG_GREY);
            if (anyGhost) { TimeText(time, sizeof(time), levelLap[level]); snprintf(line, sizeof(line), "Ghost:  %s  %s", levelName[level], time); }
            else snprintf(line, sizeof(line), "No ghosts for this track");
            Text(cx, rowY[2], line, row == 2 ? RG_ORANGE : anyGhost ? LBAWHITE : RG_GREY);
            // the keys
            Text(cx, y0 + 382, row == 0 ? "Left, right: track    Up, down: choose" : row == 2 ? "Left, right: ghost    Up, down: choose"
                                                                                   : "Up, down: choose", RG_GREY);
            Text(cx, y0 + 410, row == 0 ? "Esc: back" : "Enter: watch    Esc: back", RG_GREY);
            BoxStaticAdd(0, 0, (S32)ModeDesiredX - 1, (S32)ModeDesiredY - 1);
            }
        drawn:;
        }
        // (a frame every pass, drawn or not -- a box of it put up again: a present happens only for one -- the mouse's pointer goes with
        // it, and the engine's control socket is served at a present)
        BoxStaticAdd(x0, y1 - 40, x1, y1 - 8);
        BoxUpdate();
        if (Confirmed()) {
            WaitNoInput();
            if (sp == all) {
                if (row == 1 && sumOfBest > 0) { watch = 3; done = 1; }
                else if (row == 2 && sumOfBest > 0) { copied = AllBestPicture() ? 1 : -1; flag = 1; }
            } else if ((row == 1 && ownLap > 0) || (row == 2 && levelLap[level] > 0)) { watch = row; done = 1; }
        } else if (Escaped()) done = 1;
    }
    AsciiMode = FALSE;
    Page(NULL, 0);
    // (the sum of best: every track's best lap of the player's played back in turn)
    if (watch == 3) return WatchListStart();
    // (the lap played back: the track's time trial, its car driven along the ghost)
    if (watch && sp < s_nbSp) {
        const char *path = watch == 1 ? ownPath : levelPath[level];
        Log_Info("[racegame] Best laps: %s's %s played back (%s)", s_sp[sp].Name, watch == 1 ? "best lap" : levelName[level], path);
        s_lastSp = sp;
        return StartRace(sp, 1, NULL, -1, path);
    }
    return 0;
}

// ---- the power-ups, the controls, the qualifying -------------------------------------------------------------------------------------

// The console's screenshot (PERSO.CPP takes it in the game's loop) taken on these pages too, as they stand drawn -- in the menus'
// palette, which they are shown in
static void Shot(void) {
    char path[ADELINE_MAX_PATH];
    int png = 0;
    if (!Console_HasPendingScreenshot(path, sizeof(path), &png)) return;
    if (png) SavePNG(path, Log, (S32)ModeDesiredX, (S32)ModeDesiredY, PtrPalNormal);
    Console_ClearPendingScreenshot();
    Log_Info("[racegame] screenshot %s", path);
}

// A line of text from x (its left end), with the menus' dark shadow; already in the font's code page.
static void TextAt(S32 x, S32 y, const char *cp850, S32 colour) {
    ColorFont(BLACK);
    Font(x + 3, y + 3, (char *)cp850);
    ColorFont(colour);
    Font(x, y, (char *)cp850);
}

// A sentence from (x, y), broken between words into lines no wider than `width`, `step` apart: the lines drawn.
static int Wrapped(S32 x, S32 y, S32 width, S32 step, const char *utf8, S32 colour) {
    char text[512], line[512];
    CopyUtf8ToCp850(text, sizeof(text), utf8);
    int lines = 0;
    const char *p = text;
    while (*p) {
        while (*p == ' ') p++;
        size_t n = 0;
        // (word by word while the line still fits: at least one word a line)
        for (const char *q = p; *q;) {
            const char *end = q;
            while (*end && *end != ' ') end++;
            const size_t len = (size_t)(end - p);
            if (len >= sizeof(line)) break;
            memcpy(line, p, len);
            line[len] = 0;
            if (n && SizeFont(line) > width) break;
            n = len;
            q = end;
            while (*q == ' ') q++;
        }
        if (!n) break;
        memcpy(line, p, n);
        line[n] = 0;
        TextAt(x, y + lines * step, line, colour);
        lines++;
        p += n;
    }
    return lines;
}

// A page's frame: the menu's backdrop, a box shaded darker than the menus' (a page of text over the menu's picture) and framed as they
// are, the page's header in it
#define RG_PAGE_HALF 300                    // the box's half width
#define RG_PAGE_TOP 8                       // ... its top and bottom on the 480-tall canvas
#define RG_PAGE_BOTTOM 472
#define RG_PAGE_SHADE 13
#define RG_LINE 26                          // a line of text
static void PageFrame(const char *header) {
    const S32 cx = UI_CENTER_X, oy = UI_VCENTER_OFS;
    Page(NULL, 0);
    ShadeBoxBlk(cx - RG_PAGE_HALF, RG_PAGE_TOP + oy, cx + RG_PAGE_HALF, RG_PAGE_BOTTOM + oy, RG_PAGE_SHADE);
    DrawCadre(cx - RG_PAGE_HALF, RG_PAGE_TOP + oy, cx + RG_PAGE_HALF, RG_PAGE_BOTTOM + oy, ALL_ANGLES);
    Text(cx, RG_PAGE_TOP + oy + 8, header, RG_ORANGE);
}

// The controls: the keys the race game uses (the game's defaults -- Options, Controller changes the keyboard's), then a pad's.
// (2026-10-19: and, on a track whose race lets Twinsen out of his car to explore -- RACEMOD.CPP on_foot=, the Temple of Bú's secret
// party -- the action key's row, its lines a little closer: two more rows over the page's own keys at its foot)
// (2026-10-21: and the handbrake's -- Shift, a pad's Y: RACEMOD.CPP RaceMod_Handbrake -- P and Esc on one row as a pad's Back and Start
// are, the rows a little higher and the gamepad's heading closer: the lines as far apart as the rows shown leave room for, RG_LINE at the
// most -- 23 with the action key's rows, as before -- so that the page fits at 480 high whichever rows it shows)
#define RG_CONTROLS_TOP 44                  // (the first row's top under the page's top)
#define RG_CONTROLS_FOOT 412                // (... and the last row's top no lower than this: clear of the page's keys at its foot)
#define RG_ON_FOOT "Get out / back in"
static void DrawControls(const T_RACE_PAGE *pg) {
    static const char *const keys[][2] = {
        {"Up arrow", "Accelerate"},
        {"Down arrow", "Brake, reverse"},
        {"Left, right", "Steer"},
        {"Ctrl", "Use your item"},
        {"Shift", "Handbrake"},
        {"", ""},                           // (the gears: by hand or by themselves)
        {"V", "Camera view"},               // (2026-10-21: RACEMOD.CPP RaceCameraKey -- and the gears' row only for gears by hand, so
        {"Space", RG_ON_FOOT},              // that the page still fits)
        {"P, Esc", "Pause, menu"},
    };
    static const char *const pad[][2] = {
        {"Right trigger", "Accelerate"},
        {"Left trigger", "Brake, reverse"},
        {"Left stick", "Steer"},
        {"Left shoulder", "Use your item"},
        {"Y button", "Handbrake"},
        {"X button", RG_ON_FOOT},
        {"Back, Start", "Camera view, menu"},
    };
    const S32 cx = UI_CENTER_X, oy = UI_VCENTER_OFS, kx = cx - RG_PAGE_HALF + 36, ax = cx - 40;
    const int onFoot = pg && pg->OnFoot, manual = pg && pg->Manual;
    // (the rows shown and the room for them: the last row's top is the first's, a line for each row, a quarter more and 2 -- the gamepad's
    // heading -- at most RG_CONTROLS_FOOT)
    const int rows = (int)(sizeof(keys) / sizeof(keys[0]) + sizeof(pad) / sizeof(pad[0])) - (onFoot ? 0 : 2) - (manual ? 0 : 1);
    S32 line = (S32)((RG_CONTROLS_FOOT - RG_CONTROLS_TOP - RG_PAGE_TOP - 2) * 4 / (4 * rows + 1));
    if (line > RG_LINE) line = RG_LINE;
    PageFrame("Controls");
    S32 y = RG_PAGE_TOP + oy + RG_CONTROLS_TOP;
    char text[256];
    for (size_t i = 0; i < sizeof(keys) / sizeof(keys[0]); i++) {
        const char *key = keys[i][0], *what = keys[i][1];
        if (!onFoot && !strcmp(what, RG_ON_FOOT)) continue;
        if (!key[0]) {
            if (!manual) continue;
            key = "X, Z";
            what = "Gear up, down";
        }
        Label(text, key);
        TextAt(kx, y, text, LBAWHITE);
        Label(text, what);
        TextAt(ax, y, text, RG_GREY);
        y += line;
    }
    y += line / 4;
    Label(text, "With a gamepad");
    TextAt(kx, y, text, RG_ORANGE);
    y += line + 2;
    for (size_t i = 0; i < sizeof(pad) / sizeof(pad[0]); i++) {
        if (!onFoot && !strcmp(pad[i][1], RG_ON_FOOT)) continue;
        Label(text, pad[i][0]);
        TextAt(kx, y, text, LBAWHITE);
        Label(text, pad[i][1]);
        TextAt(ax, y, text, RG_GREY);
        y += line;
    }
}

// The power-ups: the pickups first (what a car drives into for an item), then the track's power-ups `first`.. (RG_PAGE_ITEMS of them a
// page), each in its item box turning as the race shows it, its name and what it does.
#define RG_PAGE_ITEMS 4
#define RG_ICON 52
#define RG_ITEM_LINE 24                     // (the items' lines a little closer, the items further apart)
#define RG_ITEM_STEP 84
static void DrawPowerUps(const T_RACE_PAGE *pg, int first, S32 now) {
    const S32 cx = UI_CENTER_X, oy = UI_VCENTER_OFS, ix = cx - RG_PAGE_HALF + 18, tx = ix + RG_ICON + 16, width = cx + RG_PAGE_HALF - 12 - tx;
    PageFrame("Power-ups");
    S32 y = RG_PAGE_TOP + oy + 38;
    if (!pg || !pg->Count) { Text(cx, y + 100, "No power-ups on this track.", LBAWHITE); return; }
    // (a time trial has no power-ups since 2026-10-19: its laps, as the qualifying laps, go to the best laps)
    if (pg->Trial) { Wrapped(ix, y + 2, cx + RG_PAGE_HALF - 12 - ix, RG_ITEM_LINE, "A time trial: no power-ups, just you and the clock. Its fastest lap goes to Best laps.", LBAWHITE); return; }
    else {
        RaceMod_DrawItem(pg, -1, ix, y, RG_ICON, now);
        Wrapped(tx, y + 2, width, RG_ITEM_LINE, "Drive into these for a power-up in your item box. Ctrl uses it.", LBAWHITE);
    }
    y += RG_ICON + 12;
    char text[256];
    for (int k = first; k < pg->Count && k < first + RG_PAGE_ITEMS; k++, y += RG_ITEM_STEP) {
        RaceMod_DrawItem(pg, pg->Item[k], ix, y, RG_ICON, now);
        Label(text, RaceMod_ItemName(pg->Item[k]));
        TextAt(tx, y - 2, text, RG_ORANGE);
        Wrapped(tx, y - 2 + RG_ITEM_LINE, width, RG_ITEM_LINE, RaceMod_ItemHelp(pg, pg->Item[k]), LBAWHITE);
    }
}

// The track a page shows: the special raced last (or chosen last), its race file read -- once a special
static T_RACE_PAGE s_page;
static int s_pageSp = -1;
static const T_RACE_PAGE *TrackPage(int sp) {
    if (sp < 0 || sp >= s_nbSp) return NULL;
    if (sp != s_pageSp) {
        char path[ADELINE_MAX_PATH];
        GetResPath(path, ADELINE_MAX_PATH, s_sp[sp].Race);
        RaceMod_ReadPage(path, &s_page);
        s_pageSp = sp;
    }
    return &s_page;
}

// The pages in a row: the controls (`controls`), then the power-ups (`items`: the track's, a few a page). Left and right turn them, Enter
// the next (after the last: done), Esc done; the mouse's left button the next, its right done. `intro`: before a race (the first run's).
static void Pages(const T_RACE_PAGE *pg, int controls, int items, int intro) {
    const int itemPages = !items ? 0 : pg && pg->Count ? (pg->Count + RG_PAGE_ITEMS - 1) / RG_PAGE_ITEMS : 1;
    const int pages = (controls ? 1 : 0) + itemPages;
    if (pages <= 0) return;
    Page(NULL, 0);
    BoxUpdate();
    ManageTime();
    InitWaitNoKey();
    InitWaitNoInput(I_JOY | I_FIRE | I_MENUS | I_ACTION);
    S32 padDir = JoystickMenuNavOnly();
    AsciiMode = TRUE;
    s_clickWas = Click & 3;
    int at = 0, done = 0;
    S32 cx = UI_CENTER_X, oy = UI_VCENTER_OFS;
    Log_Info("[racegame] %s: %d page(s)%s", controls && itemPages ? "the controls and the power-ups" : controls ? "the controls" : "the power-ups",
             pages, intro ? " before the race" : "");
    while (!done) {
        ManageTime();
        MyGetInput();
        // (the window resized: the page laid out again at the new size -- it is drawn whole every pass)
        if (Refitted()) { cx = UI_CENTER_X; oy = UI_VCENTER_OFS; }
        S32 mx, my;
        const int click = Clicked(&mx, &my);
        S32 step = pages > 1 ? Step(&padDir) : 0;
        if (click == 2) done = 1;
        else if (click == 1) { if (at + 1 < pages) at++; else done = 1; }
        if (step && at + step >= 0 && at + step < pages) at += step;
        if (done) break;
        const S32 now = TimerRefHR;
        if (controls && at == 0) DrawControls(pg);
        else DrawPowerUps(pg, (at - (controls ? 1 : 0)) * RG_PAGE_ITEMS, now);
        char keys[160];
        const char *last = intro ? "Enter: start" : "Enter: back";
        if (pages > 1) snprintf(keys, sizeof(keys), "Page %d of %d    %s    Esc: %s", at + 1, pages, at + 1 < pages ? "Enter: next" : last, intro ? "skip" : "back");
        else if (intro) snprintf(keys, sizeof(keys), "%s    Esc: skip", last);
        else snprintf(keys, sizeof(keys), "Esc: back");
        Text(cx, RG_PAGE_BOTTOM + oy - 32, keys, RG_GREY);
        BoxStaticAdd(0, 0, (S32)ModeDesiredX - 1, (S32)ModeDesiredY - 1);
        Shot();
        BoxUpdate();
        if (Confirmed()) { WaitNoInput(); if (at + 1 < pages) at++; else done = 1; }
        else if (Escaped()) { WaitNoInput(); done = 1; }
    }
    AsciiMode = FALSE;
    Page(NULL, 0);
}

// The first run's: before the player's first race or time trial the controls, before the first race with power-ups its power-ups --
// each once (the settings say so after).
static void Introduction(int sp, int trial) {
    ReadSettings();
    const T_RACE_PAGE *pg = TrackPage(sp);
    const int controls = !s_controlsSeen, items = !trial && pg && pg->Count && !s_powerupsSeen;
    if (!controls && !items) return;
    Pages(pg, controls, items, 1);
    if (controls) s_controlsSeen = 1;
    if (items) s_powerupsSeen = 1;
    WriteSettings();
}

// After the car, before a race: a qualifying lap, or none and Twinsen at the back of the grid (the opponents before him in their usual
// order) -- the answer last given chosen first. Not asked for a time trial, on a track with no grid, nor before the player has driven a
// qualifying lap (the first race always has one). Zero: back (Esc), the car page again.
static U16 s_qualMenu[4 + 2 * 3];
static int QualifyingPage(int sp, int trial) {
    s_qualifyNow = -1;
    // (a sprint has no lap to qualify on: its file's own start, qualifying=0)
    if (trial || s_sp[sp].Sprint) return 1;
    ReadSettings();
    const T_RACE_PAGE *pg = TrackPage(sp);
    if (!pg || !pg->Spots) return 1;
    if (!s_qualified) {
        s_qualifyNow = 1;
        Log_Info("[racegame] the first race: a qualifying lap, not to be skipped");
        return 1;
    }
    s_qualMenu[0] = (U16)(s_skipQualifying ? 2 : 1);
    s_qualMenu[1] = 3;
    s_qualMenu[2] = 280;
    s_qualMenu[3] = 0;
    SetRow(s_qualMenu, 0, RG_ID_BACK);
    SetRow(s_qualMenu, 1, RG_ID_QUALIFY);
    SetRow(s_qualMenu, 2, RG_ID_SKIP);
    for (;;) {
        Page("Qualifying", 128);
        const S32 select = Menu(s_qualMenu);
        if (select == RG_ID_BACK || select == 1000) { Page(NULL, 0); return 0; }
        if (select != RG_ID_QUALIFY && select != RG_ID_SKIP) continue;
        s_skipQualifying = select == RG_ID_SKIP;
        s_qualifyNow = !s_skipQualifying;
        WriteSettings();
        Page(NULL, 0);
        return 1;
    }
}

// RACEMOD.CPP: a qualifying lap driven (in a race the race game started). The first: qualifying may be skipped from the next race on.
// Every one goes to the track's best laps too (the user, 2026-10-19: "qualifying laps also count for Best laps, as these don't contain
// powerups"), as a lap of its own.
void RaceGame_Qualified(S32 lap) {
    if (!RaceGame_On() || s_raceSp < 0) return;
    RecordLap(lap, lap, 1);
    ReadSettings();
    if (s_qualified) return;
    s_qualified = 1;
    WriteSettings();
    Log_Info("[racegame] the first qualifying lap driven (%.2f s): the next races may skip it", lap / 1000.0);
}

// Options, "Window, mini-map, updates" ("Window mode, updates" until the mini-map, 2026-10-20): full screen or a window (F11 / Alt+Enter too), the race's mini-map on or off (2026-10-20:
// RACEMOD.CPP DrawMinimap), then the update mode (Automatic, Ask first, Off: Enter goes round them) and "Check for updates now", what the
// mode does and the updater's last word under them.
// (2026-10-21: and first the camera's view -- inside the car, just behind it, further back: Enter goes round them -- the page "Camera,
// window, updates", its six rows a little higher)
#define RG_UPD_ROWS 6
static U16 s_updMenu[4 + 2 * RG_UPD_ROWS];
// (two lines each at most, and the updater's word two more: the page's text below the rows fits the 480-tall canvas)
static const char *const s_updateHelp[3] = {
    // (a line each since the camera's row, 2026-10-21: the updater's word under it still fits)
    "Weekly: installed as you quit.",
    "Weekly: asked before installing.",
    "No weekly check: check here.",
};
#define RG_UPD_Y 222                        // the rows' centre (six rows: the header at 8, the text under them from 392 to 470)
// The updater's word as the Updates page drew it (its time), and whether Update\status.txt has a newer one -- looked at twice a second
// while the page's menu waits (GAMEMENU.CPP MenuRedrawHook)
static long long s_updDrawnAt = 0;
static S32 UpdateWordChanged(void) {
    static S32 lastLook = 0;
    if ((S32)TimerRefHR - lastLook >= 0 && (S32)TimerRefHR - lastLook < 500) return 0;
    lastLook = TimerRefHR;
    char said[400];
    return UpdateStatus(said, sizeof(said)) != s_updDrawnAt;
}
static void UpdatesPage(void) {
    ReadSettings();
    s_updMenu[0] = 1;
    s_updMenu[1] = RG_UPD_ROWS;
    s_updMenu[2] = RG_UPD_Y;
    s_updMenu[3] = 0;
    SetRow(s_updMenu, 0, RG_ID_BACK);
    SetRow(s_updMenu, 1, RG_ID_CAMERA);
    SetRow(s_updMenu, 2, RG_ID_WINDOW);
    SetRow(s_updMenu, 3, RG_ID_MINIMAP);
    SetRow(s_updMenu, 4, RG_ID_UPDATE_MODE);
    SetRow(s_updMenu, 5, RG_ID_UPDATE_NOW);
    char exe[ADELINE_MAX_PATH];
    GetResPath(exe, ADELINE_MAX_PATH, RG_UPDATER);
    for (;;) {
        const S32 cx = UI_CENTER_X, oy = UI_VCENTER_OFS;
        Page("Camera, window, mini-map, updates", RG_UPD_Y - (56 * RG_UPD_ROWS) / 2 - 46);
        const S32 textY = RG_UPD_Y + (56 * RG_UPD_ROWS) / 2 + 2 + oy;
        const int lines = Wrapped(cx - 280, textY, 560, RG_LINE, s_updateHelp[s_updates], LBAWHITE);
        // (the updater's last word -- or, while a check started here hasn't said anything yet, that it is checking)
        char status[400], said[400];
        const long long at = UpdateStatus(said, sizeof(said));
        if (!RaceUpdate_Exists(exe)) snprintf(status, sizeof(status), "No updater in the game's folder: install the game again.");
        else if (s_updateStarted && at < s_updateStarted) snprintf(status, sizeof(status), "Checking for a new version...");
        else if (said[0]) snprintf(status, sizeof(status), "%s", said);
        else status[0] = 0;
        if (status[0]) Wrapped(cx - 280, textY + lines * RG_LINE + 2, 560, RG_LINE, status, RG_GREY);
        // (the updater's word watched while the menu waits: a new one -- the check's answer -- and the page is drawn again. Drawn only
        // as the menu was entered, it said "Checking for a new version..." until a key was pressed, the check long over, 2026-10-26)
        s_updDrawnAt = at;
        MenuRedrawHook = UpdateWordChanged;
        const S32 select = Menu(s_updMenu);
        MenuRedrawHook = NULL;
        if (select == RG_ID_BACK || select == 1000) { Page(NULL, 0); return; }
        for (S32 i = 0; i < RG_UPD_ROWS; i++) if (s_updMenu[4 + 2 * i + 1] == (U16)select) s_updMenu[0] = (U16)i;
        if (select == RG_ID_WINDOW) ToggleDisplayFullscreen();
        else if (select == RG_ID_CAMERA) {
            // (inside the car, just behind, further back, and round again; a race on takes it at once -- RACEMOD.CPP)
            s_camera = (s_camera + 1) % 3;
            WriteSettings();
            if (s_raceSp >= 0 && !s_raceOver) RaceMod_SetCameraView(s_camera);
            Log_Info("[racegame] the camera: %s", s_cameraLabel[s_camera]);
        } else if (select == RG_ID_MINIMAP) {
            s_minimap = !s_minimap;
            WriteSettings();
            Log_Info("[racegame] the mini-map: %s", s_minimap ? "on" : "off");
        } else if (select == RG_ID_UPDATE_MODE) {
            s_updates = (s_updates + 1) % 3;
            WriteSettings();
            Log_Info("[racegame] updates: %s", s_updateKey[s_updates]);
        } else if (select == RG_ID_UPDATE_NOW && UpdateSpawn(1)) {
            s_updateStarted = (long long)time(NULL);
        }
    }
}

// GAMEMENU.CPP OptionsMenu: the race game's options -- the game's own rows, then the race's controls and power-ups (one row: the menu has
// room for eight) and Updates (RaceGame_OptionsRow); the game's own menu when the race game isn't on.
static U16 s_optMenu[4 + 2 * 12];
U16 *RaceGame_OptionsMenu(U16 *menu) {
    if (!RaceGame_On() || !menu || menu[1] + 3 > 12) return menu;
    const int nb = menu[1];
    memcpy(s_optMenu, menu, (4 + 2 * nb) * sizeof(U16));
    SetRow(s_optMenu, nb, RG_ID_CONTROLS);
    SetRow(s_optMenu, nb + 1, RG_ID_UPDATES);
    s_optMenu[1] = (U16)(nb + 2);
    return s_optMenu;
}

S32 RaceGame_OptionsRow(S32 num) {
    if (!RaceGame_On() || (num != RG_ID_POWERUPS && num != RG_ID_CONTROLS && num != RG_ID_UPDATES)) return 0;
    if (num == RG_ID_UPDATES) {
        UpdatesPage();
        return 1;
    }
    const T_RACE_PAGE *pg = TrackPage(s_raceSp >= 0 ? s_raceSp : s_lastSp >= 0 && s_lastSp < s_nbSp ? s_lastSp : 0);
    if (num == RG_ID_POWERUPS) Pages(pg, 0, 1, 0);
    else Pages(pg, 1, 1, 0);
    return 1;
}

// ---- Track select ------------------------------------------------------------------------------------------------------------------
// (2026-10-22 -- the user: "Pull all tracks into LBRA under Track select, but temporarily password gate them with the password 1234", in
// the order RACEGAME.TXT's track= lines give; "a car selection dialogue that displays more than one car ... 7 cars x 7, let's pull all
// cars into LBRA, and give them all their own stats"; "Car selection should only be for 'Track select'": the specials keep their car page)

static int ModePage(int sp);
static void Plot(S32 x, S32 y, U8 colour, S32 size);

// A key of the four ways (the arrows, the pad's d-pad or stick; edge-only, as Step): *dx, *dy -1, 0 or 1. Non-zero: one was struck.
static int Nav(S32 *padDir, S32 *dx, S32 *dy) {
    *dx = *dy = 0;
    S32 key = MyKey >= K_GAMEPAD_BASE ? 0 : MyKey;   // (the pad's way followed every pass, as Step)
    const S32 raw = JoystickMenuNavOnly();
    if (raw != *padDir) {
        *padDir = raw;
        if (!key) key = raw;                // (K_UP .. K_RIGHT: the arrows' own scancodes)
    }
    if (Input & I_LEFT || key == K_GRAY_LEFT) { *dx = -1; InitWaitNoInput(I_LEFT); }
    else if (Input & I_RIGHT || key == K_GRAY_RIGHT) { *dx = 1; InitWaitNoInput(I_RIGHT); }
    else if (Input & I_UP || key == K_GRAY_UP) { *dy = -1; InitWaitNoInput(I_UP); }
    else if (Input & I_DOWN || key == K_GRAY_DOWN) { *dy = 1; InitWaitNoInput(I_DOWN); }
    else return 0;
    InitWaitNoKey();
    return 1;
}

// A page's own loop begins: the backdrop up, the keys of the page before let go.
static void PageLoopStart(S32 *padDir) {
    Page(NULL, 0);
    BoxUpdate();
    ManageTime();
    InitWaitNoKey();
    InitWaitNoInput(I_JOY | I_FIRE | I_MENUS | I_ACTION);
    *padDir = JoystickMenuNavOnly();
    AsciiMode = TRUE;
    s_clickWas = Click & 3;
}

// The code Track select asks for (RACEGAME.TXT tracks_code=): digits (the keyboard's row or its keypad), Backspace, Enter to try it,
// Esc back. Each digit shown as a star; a wrong code says so and empties. Non-zero: given right -- Track select is open for the rest of
// the run (not kept: the user, "temporarily password gate them").
#define RG_CODE_MAX 12
static int CodePage(void) {
    S32 padDir;
    PageLoopStart(&padDir);
    char code[RG_CODE_MAX + 1] = "";
    int n = 0, wrong = 0, result = -1;
    S32 lastKey = 0, cx = UI_CENTER_X, oy = UI_VCENTER_OFS;
    Log_Info("[racegame] Track select: asking for its code");
    while (result < 0) {
        ManageTime();
        MyGetInput();
        if (Refitted()) { cx = UI_CENTER_X; oy = UI_VCENTER_OFS; }
        // (a key struck: MyKey is the key held, pass after pass -- a digit taken as it goes down)
        const S32 key = MyKey;
        if (key != lastKey) {
            lastKey = key;
            int digit = -1;
            if (key >= K_1 && key <= K_9) digit = 1 + (int)(key - K_1);
            else if (key == K_0) digit = 0;
            else if (key >= K_NUMPAD_1 && key <= K_NUMPAD_9) digit = 1 + (int)(key - K_NUMPAD_1);
            else if (key == K_NUMPAD_0) digit = 0;
            if (digit >= 0 && n < RG_CODE_MAX) { code[n++] = (char)('0' + digit); code[n] = 0; wrong = 0; }
            else if (key == K_BACKSPACE && n > 0) code[--n] = 0;
        }
        S32 mx, my;
        const int click = Clicked(&mx, &my);
        // the page: a box with what it asks, the stars, and the keys
        const S32 x0 = cx - 230, x1 = cx + 230, y0 = 150 + oy, y1 = 352 + oy;
        Page(NULL, 0);
        ShadeBoxBlk(x0, y0, x1, y1, RG_PAGE_SHADE);
        DrawCadre(x0, y0, x1, y1, ALL_ANGLES);
        Text(cx, y0 + 14, "Track select", RG_ORANGE);
        Text(cx, y0 + 52, "Enter the code", LBAWHITE);
        // (a gold dot for each digit, then a dim one blinking where the next goes; the font's '*' sits too high and small)
        static U8 gold = 0, dim = 0, shadow = 0;
        static const U8 *paletteWas = NULL;
        const U8 *pal = PtrPalNormal ? PtrPalNormal : PtrPal;
        if (pal != paletteWas) {
            paletteWas = pal;
            gold = NearestColour(255, 200, 40);
            dim = NearestColour(110, 100, 90);
            shadow = NearestColour(0, 0, 0);
        }
        const int dots = n + (n < RG_CODE_MAX ? 1 : 0);
        const S32 step = 30, dx0 = cx - (dots - 1) * step / 2, dy = y0 + 108;
        for (int k = 0; k < dots; k++) {
            if (k == n && (TimerRefHR / 400) % 2) continue;
            const U8 c = k < n ? gold : dim;
            for (int pass = 0; pass < 2; pass++)
                for (S32 ry = -8; ry <= 8; ry++)
                    for (S32 rx = -8; rx <= 8; rx++)
                        if (rx * rx + ry * ry <= (k < n ? 64 : 20)) Plot(dx0 + k * step + rx + (pass ? 0 : 2), dy + ry + (pass ? 0 : 2), pass ? c : shadow, 1);
        }
        if (wrong) Text(cx, y0 + 132, "Wrong code", RG_ORANGE);
        Text(cx, y1 - 38, "Enter: open    Esc: back", RG_GREY);
        BoxStaticAdd(0, 0, (S32)ModeDesiredX - 1, (S32)ModeDesiredY - 1);
        Shot();
        BoxUpdate();
        if (click == 2 || Escaped()) result = 0;
        else if (Confirmed()) {
            WaitNoInput();
            if (n && !strcmp(code, s_tracksCode)) result = 1;
            else if (n) {
                wrong = 1;
                Log_Info("[racegame] Track select: a wrong code (%d digits)", n);
                n = 0;
                code[0] = 0;
            }
        }
    }
    AsciiMode = FALSE;
    Page(NULL, 0);
    if (result) Log_Info("[racegame] Track select: the code given, open for this run");
    return result;
}

// Track select's tracks, in RACEGAME.TXT's order, each with its best lap: up and down (or the mouse) to choose, Enter the track's race or
// time trial (ModePage), Esc back. Non-zero: a race is set up.
// (30, was 24: at 24 a name's apostrophe -- "14. Emperor's Palace" -- reached up to the line above's, the user, 2026-10-22; spaced out, the
// list scrolls)
#define RG_TRACK_LINE 30
static int TracksPage(void) {
    int list[RG_MAX_ENTRIES], n = 0, at = 0;
    for (int i = 0; i < s_nbSp; i++)
        if (s_sp[i].Track) {
            if (i == s_lastTrack) at = n;
            list[n++] = i;
        }
    if (n <= 0) return 0;
    ReadTimes();
    int first = 0;
    for (;;) {
        S32 padDir;
        PageLoopStart(&padDir);
        S32 cx = UI_CENTER_X, oy = UI_VCENTER_OFS;
        int chosen = -2;
        while (chosen == -2) {
            ManageTime();
            MyGetInput();
            if (Refitted()) { cx = UI_CENTER_X; oy = UI_VCENTER_OFS; }
            // (the rows that fit between the header and the best lap's line over the keys, the list scrolling past them -- room kept above
            // and below for its "..." marks)
            const S32 top = RG_PAGE_TOP + oy + 50, keysY = RG_PAGE_BOTTOM + oy - 32, bestY = keysY - 30;
            const int rows = (int)((bestY - 16 - top) / RG_TRACK_LINE);
            S32 dx, dy;
            if (Nav(&padDir, &dx, &dy) && dy) at = (at + dy + n) % n;
            if (at < first) first = at;
            if (at >= first + rows) first = at - rows + 1;
            const S32 lx0 = cx - RG_PAGE_HALF + 14, lx1 = cx + RG_PAGE_HALF - 14;
            // the scrollbar (2026-10-26 -- the user: "add a scrollbar to the "Track Select" window"): down the list's right edge, its thumb
            // the rows shown of them all, where they are; a click or a drag on it scrolls there, as the mouse wheel does over the page --
            // the track chosen kept among the rows shown
            const S32 bx0 = lx1 - 16, bx1 = lx1 - 2, by0 = top - 2, by1 = top + rows * RG_TRACK_LINE - 10;
            const int scrolls = n > rows;
            if (scrolls) {
                const S32 wheel = ConsumeMouseWheelY();
                if (wheel) first -= wheel;
                if (FlagMenuMouse && (Click & 1) && (S32)MouseX >= bx0 - 6 && (S32)MouseX <= bx1 + 6 && (S32)MouseY >= by0 && (S32)MouseY <= by1)
                    first = (int)(((S32)MouseY - by0) * (n - rows + 1) / (by1 - by0 + 1));
                if (first < 0) first = 0;
                if (first > n - rows) first = n - rows;
                if (at < first) at = first;
                if (at >= first + rows) at = first + rows - 1;
            }
            S32 mx, my;
            const int click = Clicked(&mx, &my);
            if (click == 2) chosen = -1;
            else if (click == 1 && mx >= lx0 && mx <= (scrolls ? bx0 - 8 : lx1) && my >= top - 4) {
                const int row = (int)((my - (top - 4)) / RG_TRACK_LINE);
                if (row < rows && first + row < n) { at = first + row; chosen = list[at]; }
            }
            PageFrame("Track select");
            for (int r = 0; r < rows && first + r < n; r++) {
                const int k = first + r, sp = list[k];
                const S32 y = top + r * RG_TRACK_LINE;
                // (the chosen one in orange -- no frame round it: it didn't fit the line, the user, 2026-10-22)
                char line[160], text[256];
                snprintf(line, sizeof(line), "%d. %s", k + 1, s_sp[sp].Name);
                Label(text, line);
                TextAt(lx0 + 12, y, text, k == at ? RG_ORANGE : LBAWHITE);
            }
            // (more above or below: the scrollbar, its thumb the rows shown -- in the place of the "..." marks it had)
            if (scrolls) {
                ShadeBoxBlk(bx0, by0, bx1, by1, 10);
                DrawCadre(bx0, by0, bx1, by1, ALL_ANGLES);
                const S32 h = by1 - by0 - 4, thumb = h * rows / n > 18 ? h * rows / n : 18;
                const S32 ty = by0 + 2 + (h - thumb) * first / (n - rows);
                Box(bx0 + 3, ty, bx1 - 3, ty + thumb, RG_ORANGE);
            }
            // the track's best lap, under the list (the names take the page's width)
            {
                const int sp = list[at];
                char line[160], best[24];
                if (s_nbTimes[sp] > 0) {
                    TimeText(best, sizeof(best), s_times[sp][0].Lap);
                    snprintf(line, sizeof(line), "Best lap: %s", best);
                } else snprintf(line, sizeof(line), "No best lap yet");
                Text(cx, bestY, line, RG_GREY);
            }
            Text(cx, keysY, "Enter: choose    Esc: back", RG_GREY);
            BoxStaticAdd(0, 0, (S32)ModeDesiredX - 1, (S32)ModeDesiredY - 1);
            Shot();
            BoxUpdate();
            if (chosen != -2) break;
            if (Confirmed()) { WaitNoInput(); chosen = list[at]; }
            else if (Escaped()) chosen = -1;
        }
        AsciiMode = FALSE;
        Page(NULL, 0);
        if (chosen < 0) return 0;
        s_lastTrack = chosen;
        Log_Info("[racegame] Track select: %s", s_sp[chosen].Name);
        if (ModePage(chosen)) return 1;
    }
}

// Track select from the main menu: its code first when RACEGAME.TXT asks one (once a run), then its tracks. Non-zero: a race is set up.
static int TrackSelect(void) {
    if (!s_tracks || s_nbTracks <= 0) return 0;
    if (s_tracksCode[0] && !s_tracksOpen) {
        if (!CodePage()) return 0;
        s_tracksOpen = 1;
    }
    return TracksPage();
}

// The car grid (Track select's): every car of the game, 7 by 7 a page, each drawn small -- once, as its page opens, kept (s_thumb) -- the
// one chosen large on the right, turning as the car page's do, its name and its stats under it. The arrows (or a pad) go through the
// cars, a row up and down, page to page past the ends; Page up and Page down a page; Enter (or a click on the car chosen, or its big
// box) takes it, Esc goes back. The car taken (its place in s_grid), or -1.
#define RG_GRID_COLS 7
#define RG_GRID_ROWS 7
#define RG_GRID_PAGE (RG_GRID_COLS * RG_GRID_ROWS)
#define RG_CELL_W 44
#define RG_CELL_H 40
#define RG_THUMB_BETA 3456                  // the small cars' turn: three quarters from the front
static U8 s_thumb[RG_GRID_PAGE][RG_CELL_W * RG_CELL_H];
static int s_thumbPage = -1;                // the page s_thumb holds (-1: none) ...
static S32 s_thumbX = 0, s_thumbY = 0;      // ... drawn with its grid here

// The page's small cars drawn into s_thumb: each measured in the car page's box (RaceMod_DrawBody keeps it), its cell's backdrop put
// back and shaded, the car drawn in it, a car still locked dimmed.
static void GridThumbs(int page, S32 gx, S32 gy, S32 cx, S32 oy) {
    const clock_t start = clock();
    int drawn = 0;
    for (int k = 0; k < RG_GRID_PAGE; k++) {
        const int idx = page * RG_GRID_PAGE + k;
        if (idx >= s_nbGrid) break;
        const T_RG_CAR *c = &s_grid[idx];
        const S32 x = gx + (k % RG_GRID_COLS) * RG_CELL_W, y = gy + (k / RG_GRID_COLS) * RG_CELL_H;
        if (c->Body >= 0) RaceMod_DrawBody(cx - 228, 132 + oy, cx + 228, RG_CAR_BOTTOM - 2 + oy, c->Body, 0, 1, c->Anim);
        CopyBlock(x, y, x + RG_CELL_W - 1, y + RG_CELL_H - 1, Screen, x, y, Log);
        ShadeBoxBlk(x, y, x + RG_CELL_W - 1, y + RG_CELL_H - 1, MENU_SHADE_LVL);
        if (c->Body >= 0) { RaceMod_DrawBody(x + 2, y + 2, x + RG_CELL_W - 3, y + RG_CELL_H - 3, c->Body, RG_THUMB_BETA, 0, c->Anim); drawn++; }
        if (Locked(c)) ShadeBoxBlk(x + 1, y + 1, x + RG_CELL_W - 2, y + RG_CELL_H - 2, 10);
        for (S32 r = 0; r < RG_CELL_H; r++) memcpy(&s_thumb[k][r * RG_CELL_W], (U8 *)Log + TabOffLine[y + r] + x, RG_CELL_W);
    }
    s_thumbPage = page; s_thumbX = gx; s_thumbY = gy;
    Log_Info("[racegame] the car grid: page %d, %d cars drawn small in %d ms", page + 1, drawn, (int)((clock() - start) * 1000 / CLOCKS_PER_SEC));
}

// A frame two pixels wide inside (x0, y0)-(x1, y1).
static void FrameBox(S32 x0, S32 y0, S32 x1, S32 y1, U8 colour) {
    for (S32 x = x0; x <= x1; x++) { Plot(x, y0, colour, 2); Plot(x, y1 - 1, colour, 2); }
    for (S32 y = y0; y <= y1; y++) { Plot(x0, y, colour, 2); Plot(x1 - 1, y, colour, 2); }
}

static int GridPage(const char *what, int trial) {
    if (s_nbGrid <= 0) return -1;
    // (the car chosen last, by its name; one that can't be taken for this -- the pumpkin F1 still locked -- the first that can)
    ReadSettings();
    if (s_gridCarName[0])
        for (int i = 0; i < s_nbGrid; i++) if (!strcmp(s_grid[i].Name, s_gridCarName)) { s_gridCar = i; break; }
    int sel = s_gridCar >= 0 && s_gridCar < s_nbGrid ? s_gridCar : 0;
    if (!TakenFor(&s_grid[sel], trial)) {
        sel = 0;
        while (sel + 1 < s_nbGrid && !TakenFor(&s_grid[sel], trial)) sel++;
    }
    const int pages = (s_nbGrid + RG_GRID_PAGE - 1) / RG_GRID_PAGE;
    S32 padDir;
    PageLoopStart(&padDir);
    // (the backdrop may not be the one the small cars were drawn on: a race's, a window resized)
    s_thumbPage = -1;
    S32 cx = UI_CENTER_X, oy = UI_VCENTER_OFS;
    int chosen = -2, was = -1;
    S32 start = TimerRefHR;
    Log_Info("[racegame] the car grid: %d cars on %d page(s), %s chosen", s_nbGrid, pages, s_grid[sel].Name);
    while (chosen == -2) {
        ManageTime();
        MyGetInput();
        if (Refitted()) { cx = UI_CENTER_X; oy = UI_VCENTER_OFS; s_thumbPage = -1; }
        // the layout: the grid on the left, the car chosen on the right
        // (the stats' labels in the menus' font are about 200 wide: the panel takes half the canvas, the grid the other half)
        const S32 gx = cx - 314, gy = 50 + oy, gx1 = gx + RG_GRID_COLS * RG_CELL_W, gy1 = gy + RG_GRID_ROWS * RG_CELL_H;
        const S32 px0 = cx + 4, px1 = cx + 316, py0 = 46 + oy, py1 = 198 + oy, pcx = (px0 + px1) / 2;
        S32 dx, dy;
        if (Nav(&padDir, &dx, &dy)) {
            const int d = dx ? dx : dy * RG_GRID_COLS;
            sel = ((sel + d) % s_nbGrid + s_nbGrid) % s_nbGrid;
        } else if (MyKey == K_PAGE_DOWN || MyKey == K_PAGE_UP) {
            const int page = ((sel / RG_GRID_PAGE + (MyKey == K_PAGE_DOWN ? 1 : -1)) % pages + pages) % pages;
            sel = page * RG_GRID_PAGE + sel % RG_GRID_PAGE;
            if (sel >= s_nbGrid) sel = s_nbGrid - 1;
            InitWaitNoKey();
        }
        // (the mouse: a click on a car chooses it, on the car chosen -- or its big box -- takes it; the right button back)
        S32 mx, my;
        const int click = Clicked(&mx, &my);
        int take = 0;
        if (click == 2) chosen = -1;
        else if (click == 1 && mx >= gx && mx < gx1 && my >= gy && my < gy1) {
            const int idx = (sel / RG_GRID_PAGE) * RG_GRID_PAGE + (int)((my - gy) / RG_CELL_H) * RG_GRID_COLS + (int)((mx - gx) / RG_CELL_W);
            if (idx < s_nbGrid) { if (idx == sel) take = 1; else sel = idx; }
        } else if (click == 1 && mx >= px0 && mx <= px1 && my >= py0 && my <= py1) take = 1;
        if (sel != was) { was = sel; start = TimerRefHR; }
        const T_RG_CAR *c = &s_grid[sel];
        const int page = sel / RG_GRID_PAGE;
        // (the car chosen measured first, in the car page's box, under the backdrop drawn next; then the page's small cars when it is new)
        RaceMod_DrawBody(cx - 228, 132 + oy, cx + 228, RG_CAR_BOTTOM - 2 + oy, c->Body, 0, 1, c->Anim);
        if (page != s_thumbPage || gx != s_thumbX || gy != s_thumbY) GridThumbs(page, gx, gy, cx, oy);
        // the page: the backdrop, the header, the grid
        CopyScreen(Screen, Log);
        char header[96];
        snprintf(header, sizeof(header), "Choose your car for %s", what);
        // (the header over the menu picture's title: a shaded strip under it)
        ShadeBoxBlk(cx - 318, 4 + oy, cx + 316, 38 + oy, MENU_SHADE_LVL);
        Text(cx, 8 + oy, header, RG_ORANGE);
        ShadeBoxBlk(gx - 4, gy - 4, gx1 + 3, gy1 + 3, MENU_SHADE_LVL);
        for (int k = 0; k < RG_GRID_PAGE && page * RG_GRID_PAGE + k < s_nbGrid; k++) {
            const S32 x = gx + (k % RG_GRID_COLS) * RG_CELL_W, y = gy + (k / RG_GRID_COLS) * RG_CELL_H;
            for (S32 r = 0; r < RG_CELL_H; r++) memcpy((U8 *)Log + TabOffLine[y + r] + x, &s_thumb[k][r * RG_CELL_W], RG_CELL_W);
        }
        DrawCadre(gx - 4, gy - 4, gx1 + 3, gy1 + 3, ALL_ANGLES);
        {
            const int k = sel % RG_GRID_PAGE;
            const S32 x = gx + (k % RG_GRID_COLS) * RG_CELL_W, y = gy + (k / RG_GRID_COLS) * RG_CELL_H;
            FrameBox(x, y, x + RG_CELL_W - 1, y + RG_CELL_H - 1, RG_ORANGE);
        }
        char text[256], line[96];
        snprintf(line, sizeof(line), "Page %d of %d", page + 1, pages);
        Text((gx + gx1) / 2, gy1 + 10, line, RG_GREY);
        // the car chosen: turning in its box, dark and padlocked while it is locked, its name and stats under it
        const int locked = Locked(c), takeable = TakenFor(c, trial);
        ShadeBoxBlk(px0, py0, px1, py1, MENU_SHADE_LVL);
        RaceMod_DrawBody(px0 + 2, py0 + 2, px1 - 2, py1 - 2, c->Body, (S32)(((TimerRefHR - start) / 3 + 3072) & 4095), 0, c->Anim);
        if (!takeable) ShadeBoxBlk(px0 + 2, py0 + 2, px1 - 2, py1 - 2, locked ? 10 : 7);
        if (locked) {
            Padlock(pcx, (py0 + py1) / 2 - 32);
            TextFitted(pcx, py1 - 60, px1 - px0 - 20, c->Hint[0] ? c->Hint : "Win a race to unlock", RG_ORANGE);
        } else if (!takeable) Text(pcx, py1 - 36, "For time trials", RG_ORANGE);
        DrawCadre(px0, py0, px1, py1, ALL_ANGLES);
        snprintf(line, sizeof(line), "%d of %d", sel + 1, s_nbGrid);
        Label(text, line);
        TextAt(px1 - 10 - SizeFont(text), py0 + 6, text, RG_GREY);
        TextFitted(pcx, py1 + 6, px1 - px0 - 8, c->Name, takeable ? LBAWHITE : RG_GREY);
        if (c->Rated) StatsSized(pcx, py1 + 64, RG_STAT_LINE, c, 8, 18, 8);
        // the keys
        Text(cx, 404 + oy, "Arrows: car    Page up, down: page", RG_GREY);
        Text(cx, 440 + oy, takeable ? "Enter: race    Esc: back" : "Esc: back", RG_GREY);
        BoxStaticAdd(0, 0, (S32)ModeDesiredX - 1, (S32)ModeDesiredY - 1);
        Shot();
        BoxUpdate();
        if (chosen != -2) break;
        if (take && takeable) { WaitNoInput(); chosen = sel; }
        else if (Confirmed()) { WaitNoInput(); if (takeable) chosen = sel; }
        else if (Escaped()) chosen = -1;
    }
    AsciiMode = FALSE;
    Page(NULL, 0);
    if (chosen >= 0) {
        s_gridCar = chosen;
        snprintf(s_gridCarName, sizeof(s_gridCarName), "%s", s_grid[chosen].Name);
        WriteSettings();
    }
    return chosen;
}


// A race's difficulty, on a track with levels: easy, medium or hard -- the one chosen last first -- kept for the next time. The level
// (0-2), or -1 for Back.
static int LevelPage(void) {
    s_levelMenu[0] = (U16)(1 + s_level);
    s_levelMenu[1] = 4;
    s_levelMenu[2] = 280;
    s_levelMenu[3] = 0;
    SetRow(s_levelMenu, 0, RG_ID_BACK);
    SetRow(s_levelMenu, 1, RG_ID_EASY);
    SetRow(s_levelMenu, 2, RG_ID_MEDIUM);
    SetRow(s_levelMenu, 3, RG_ID_HARD);
    for (;;) {
        Page("Difficulty", 280 - (56 * 4) / 2 - 50);
        // (what the levels do, under the rows)
        Text(UI_CENTER_X, 400 + UI_VCENTER_OFS, "Easy: slower monsters, fewer items", RG_GREY);
        Text(UI_CENTER_X, 436 + UI_VCENTER_OFS, "Hard: quicker monsters, aimed attacks", RG_GREY);
        const S32 select = Menu(s_levelMenu);
        if (select == RG_ID_BACK || select == 1000) { Page(NULL, 0); return -1; }
        if (select < RG_ID_EASY || select > RG_ID_HARD) continue;
        s_level = select - RG_ID_EASY;
        WriteSettings();
        Log_Info("[racegame] difficulty: %s", s_levelName[s_level]);
        return s_level;
    }
}

// A time trial's laps: 1, 3, 5, 10 or endless (s_trialLaps, kept) -- a sprint's runs, from the start line to the finish and back to the
// start for the next (RACEMOD.CPP trial_runs=, SprintRunAgain). Non-zero: chosen; 0: Back or Esc.
static U16 s_lapsMenu[4 + 2 * 6];
static int LapsPage(int runs) {
    ReadSettings();
    s_lapsRuns = runs;
    s_lapsMenu[0] = 2;
    s_lapsMenu[1] = 6;
    s_lapsMenu[2] = 308;
    s_lapsMenu[3] = 0;
    SetRow(s_lapsMenu, 0, RG_ID_BACK);
    for (int r = 0; r < 5; r++) {
        SetRow(s_lapsMenu, r + 1, (U16)(RG_ID_LAPS_1 + r));
        if (s_trialLapsChoice[r] == s_trialLaps) s_lapsMenu[0] = (U16)(r + 1);
    }
    for (;;) {
        Page(runs ? "How many runs?" : "How many laps?", 92);
        const S32 select = Menu(s_lapsMenu);
        if (select == RG_ID_BACK || select == 1000) { Page(NULL, 0); return 0; }
        if (select < RG_ID_LAPS_1 || select > RG_ID_LAPS_ENDLESS) continue;
        s_trialLaps = s_trialLapsChoice[select - RG_ID_LAPS_1];
        WriteSettings();
        if (s_trialLaps) Log_Info("[racegame] the time trial: %d lap(s)", s_trialLaps);
        else Log_Info("[racegame] the time trial: endless, until the player stops");
        return 1;
    }
}

// A time trial's ghost to race (2026-10-25, the user: "To race a ghost, let's first have the user select the track, then Time Trial, then add
// a screen so they can choose to race a ghost, either their best time, or the easy/medium/hard ghost"): no ghost, the player's best lap
// (once there is one), or the game's Easy ghost -- its Medium once Easy is beaten, its Hard once Medium is, its Extreme once Hard is. The
// one chosen last first. The ghost (RACEMOD.CPP ghost=: 0, 1, 3, 4, 5, 7), or -1 for Back.
// "Other" (2026-10-26 -- the user: "update the "Race a ghost" screen so they can select a specific person's ghost relevant to the track
// they're about to drive. Let's add this as an "Other" option, that then lists all of the installed ghosts from other people relevant to
// that track"): OthersPage -- ghost=8, the file chosen.
//
// The other players' ghosts of a track (RACEMOD.CPP RaceMod_GhostOthers: each person's quickest, the quickest first), a row each, and
// "Import ghosts..." and "Share my ghosts..." under them -- the setup beside the game (Update\LBRA-Setup.exe) does those, its own window
// over the game's: a zip a friend shared, or .ghost files, into the user's GHOSTS; the player's own ghosts into a zip, under their name.
// The page drawn again when the GHOSTS folder changes (an import done while it waits). Non-zero: a ghost chosen (s_ghostOther).
static U32 s_othersSig = 0;
static S32 OthersChanged(void) {
    static S32 lastLook = 0;
    if ((S32)TimerRefHR - lastLook >= 0 && (S32)TimerRefHR - lastLook < 1000) return 0;
    lastLook = TimerRefHR;
    return RaceMod_GhostFolderSignature() != s_othersSig;
}
static int OthersPage(int sp) {
    static U16 menu[4 + 2 * 9];
    char exe[ADELINE_MAX_PATH], folder[ADELINE_MAX_PATH];
    GetResPath(exe, ADELINE_MAX_PATH, RG_UPDATER);
    RaceMod_GhostFolder(folder, sizeof(folder));
    AddDirIfNot(folder);
    const char *said = NULL;
    menu[0] = 1;
    for (;;) {
        s_othersSig = RaceMod_GhostFolderSignature();
        s_nbOthers = RaceMod_GhostOthers(s_sp[sp].Name, s_others, RG_OTHERS_SHOWN);
        int nb = 0;
        SetRow(menu, nb++, RG_ID_BACK);
        for (int k = 0; k < s_nbOthers; k++) SetRow(menu, nb++, (U16)(RG_ID_OTHER_FIRST + k));
        SetRow(menu, nb++, RG_ID_GHOST_IMPORT);
        SetRow(menu, nb++, RG_ID_GHOST_SHARE);
        // (the one chosen last first, when it is still there)
        if (menu[0] >= nb) menu[0] = 1;
        for (int k = 0; k < s_nbOthers; k++) if (!SDL_strcasecmp(s_others[k].Path, s_ghostOther) && menu[0] == 1) menu[0] = (U16)(1 + k);
        menu[1] = (U16)nb;
        menu[2] = (U16)(nb > 6 ? 270 : 250);
        menu[3] = 0;
        Page(NULL, 0);
        Text(UI_CENTER_X, 8 + UI_VCENTER_OFS, "Other people's ghosts", LBAWHITE);
        const S32 below = menu[2] + (nb * 56) / 2 + 8 + UI_VCENTER_OFS;
        const char *line = said ? said : !s_nbOthers ? "None of this track yet. Import a zip of ghosts a friend shared, or share yours." : NULL;
        if (line) Wrapped(UI_CENTER_X - 280, below, 560, RG_LINE, line, RG_GREY);
        MenuRedrawHook = OthersChanged;
        const S32 select = Menu(menu);
        MenuRedrawHook = NULL;
        if (select == RG_ID_BACK || select == 1000) { Page(NULL, 0); return 0; }
        if (select == RG_ID_GHOST_IMPORT || select == RG_ID_GHOST_SHARE) {
            const int import = select == RG_ID_GHOST_IMPORT;
            if (!RaceUpdate_Exists(exe)) said = "The game's folder has no setup to do that: install the game again.";
            else if (RaceUpdate_Tool(exe, import ? "--import-ghosts" : "--export-ghosts", folder))
                said = import ? "Choose the zip (or .ghost files) in the window that opened." : "Give your name and where to save the zip in the window that opened.";
            else said = "It couldn't be started.";
            continue;
        }
        if (select >= RG_ID_OTHER_FIRST && select < RG_ID_OTHER_FIRST + s_nbOthers) {
            const T_RACE_GHOST_OTHER *o = &s_others[select - RG_ID_OTHER_FIRST];
            snprintf(s_ghostOther, sizeof(s_ghostOther), "%s", o->Path);
            Log_Info("[racegame] %s's time trial: against %s's ghost (%.2f s, %s)", s_sp[sp].Name, o->Driver, o->Lap / 1000.0, o->Path);
            Page(NULL, 0);
            return 1;
        }
    }
}

static U16 s_ghostMenu[4 + 2 * 8];
static int GhostPage(int sp) {
    ReadSettings();
    S32 mine = 1;
    for (int k = 0; k < 5; k++) {
        char path[ADELINE_MAX_PATH];
        s_gpLap[k] = 0;
        RaceMod_GhostFind(s_sp[sp].Name, k == 0 ? 1 : k < 4 ? 2 + k : 7, path, sizeof(path), &s_gpLap[k], NULL, 0, k ? NULL : &mine, NULL, 0);
    }
    s_gpBeaten = s_beaten[sp];
    s_gpOwnName = OwnLapName(sp, s_gpLap[0], mine);
    s_gpOthers = RaceMod_GhostOthers(s_sp[sp].Name, s_others, RG_OTHERS_SHOWN);
    static const U16 ids[7] = {RG_ID_GHOST_NONE, RG_ID_GHOST_OWN, RG_ID_GHOST_EASY, RG_ID_GHOST_MEDIUM, RG_ID_GHOST_HARD, RG_ID_GHOST_EXTREME, RG_ID_GHOST_OTHER};
    static const int modes[7] = {0, 1, 3, 4, 5, 7, 8};
    static const char *const what[6] = {"no ghost", "against the player's best lap", "against the Easy ghost", "against the Medium ghost",
                                        "against the Hard ghost", "against the Extreme ghost"};
    // (eight rows, 50 high with 6 between: centred lower than the main menu's seven, the last just inside the 480-tall canvas)
    s_ghostMenu[1] = 8;
    s_ghostMenu[2] = 284;
    s_ghostMenu[3] = 0;
    SetRow(s_ghostMenu, 0, RG_ID_BACK);
    for (int r = 0; r < 7; r++) SetRow(s_ghostMenu, r + 1, ids[r]);
    // (the row chosen last, if it is there to choose -- else no ghost)
    s_ghostMenu[0] = 1;
    for (int r = 0; r < 7; r++) if (modes[r] == s_ghost && !RaceGame_RowDisabled(ids[r])) s_ghostMenu[0] = (U16)(r + 1);
    for (;;) {
        // (the header over the menu picture's title, in white -- orange was lost in the title's own orange letters, and a shaded strip
        // under it doesn't show over the picture)
        Page(NULL, 0);
        Text(UI_CENTER_X, 8 + UI_VCENTER_OFS, "Race a ghost", LBAWHITE);
        const S32 select = Menu(s_ghostMenu);
        if (select == RG_ID_BACK || select == 1000) { Page(NULL, 0); return -1; }
        if (select == RG_ID_GHOST_OTHER) {
            // (another player's: chosen on its own page -- or Back, this page again)
            if (OthersPage(sp)) {
                s_ghost = 8;
                WriteSettings();
                return s_ghost;
            }
            s_gpOthers = RaceMod_GhostOthers(s_sp[sp].Name, s_others, RG_OTHERS_SHOWN);
            continue;
        }
        for (int r = 0; r < 6; r++)
            if (select == ids[r] && !RaceGame_RowDisabled(ids[r])) {
                s_ghost = modes[r];
                WriteSettings();
                Log_Info("[racegame] %s's time trial: %s", s_sp[sp].Name, what[r]);
                return s_ghost;
            }
    }
}

// RACEMOD.CPP: the game's ghost of `level` ("Easy", "Medium", "Hard", "Extreme") beaten on the race's track: kept, and the next opened.
// The levels it opens ("Medium", "Hard and Extreme", ...), or NULL when it opens none (Extreme, or beaten before).
const char *RaceGame_GhostBeaten(const char *level) {
    if (!RaceGame_On() || s_raceSp < 0 || s_raceSp >= s_nbSp || !level) return NULL;
    const int n = !strcmp(level, "Easy") ? 1 : !strcmp(level, "Medium") ? 2 : !strcmp(level, "Hard") ? 3 : !strcmp(level, "Extreme") ? 4 : 0;
    ReadSettings();
    if (!n || n <= s_beaten[s_raceSp]) return NULL;
    // (the levels open: Medium from 1, Hard from 2, Extreme from 3 -- a harder ghost's time beaten racing an easier one opens the ones
    // past it too)
    const int was = s_beaten[s_raceSp] < 3 ? s_beaten[s_raceSp] : 3, now = n < 3 ? n : 3;
    s_beaten[s_raceSp] = n;
    WriteSettings();
    static const char *const names[4] = {"", "Medium", "Hard", "Extreme"};
    static char opened[48];
    opened[0] = 0;
    for (int k = was + 1; k <= now; k++)
        snprintf(opened + strlen(opened), sizeof(opened) - strlen(opened), "%s%s", k == was + 1 ? "" : k == now ? " and " : ", ", names[k]);
    if (!opened[0]) return NULL;
    Log_Info("[racegame] %s: the %s ghost beaten%s%s%s", s_sp[s_raceSp].Name, level, opened ? ", " : "", opened ? opened : "", opened ? " open" : "");
    return opened;
}

// RACEMOD.CPP: the race's track's game's ghosts beaten so far: 0 none, 1 Easy .. 4 Extreme (0 outside the race game).
int RaceGame_GhostBeatenLevel(void) {
    if (!RaceGame_On() || s_raceSp < 0 || s_raceSp >= s_nbSp) return 0;
    ReadSettings();
    return s_beaten[s_raceSp];
}

// A special's race or time trial; then (a race on a track with levels) its difficulty, or (a time trial) its ghost; then the car -- Esc
// there back to the difficulty or the ghost. Non-zero: the race is set up.
static int ModePage(int sp) {
    s_modeMenu[0] = (U16)(s_lastTrial ? 2 : 1);
    s_modeMenu[1] = 3;
    s_modeMenu[2] = 280;
    s_modeMenu[3] = 0;
    SetRow(s_modeMenu, 0, RG_ID_BACK);
    SetRow(s_modeMenu, 1, RG_ID_RACE);
    SetRow(s_modeMenu, 2, RG_ID_TRIAL);
    for (;;) {
        Page(s_sp[sp].Name, 128);
        const S32 select = Menu(s_modeMenu);
        if (select == RG_ID_BACK || select == 1000) { Page(NULL, 0); return 0; }
        if (select != RG_ID_RACE && select != RG_ID_TRIAL) continue;
        const int trial = select == RG_ID_TRIAL;
        s_lastTrial = trial;
        // (a race on a track with levels: its difficulty first; then the car; then a race's qualifying lap, or none -- Esc on the
        // qualifying page is the car page again, Esc on the car page the difficulty again)
        int lapsChosen = 0;
        for (;;) {
            int level = -1;
            if (!trial && s_sp[sp].Levels && (level = LevelPage()) < 0) break;
            // (a time trial: its laps -- a sprint's runs -- then the ghost to race; Esc on the car page is the ghost page again, Esc there
            // the laps page)
            if (trial && !lapsChosen && !(lapsChosen = LapsPage(s_sp[sp].Sprint))) break;
            if (trial && GhostPage(sp) < 0) {
                lapsChosen = 0;
                continue;
            }
            // (a special's car page -- its own few cars, one at a time -- or Track select's car grid, every car: 2026-10-22)
            const char *what = trial ? "a time trial" : "a race";
            int car;
            while ((car = s_sp[sp].Track ? GridPage(what, trial) : CarPage(what, trial)) >= 0 && !QualifyingPage(sp, trial)) {}
            if (car >= 0) { if (StartRace(sp, trial, s_sp[sp].Track ? &s_grid[car] : &s_cars[car], level)) return 1; break; }
            if (level < 0 && !trial) break;
        }
    }
}

static int SpecialsPage(void) {
    if (s_nbSpecials <= 0) return 0;
    int last = 0;
    for (int k = 0; k < s_nbSpecials; k++) if (s_spIndex[k] == s_lastSp) last = k;
    s_spMenu[0] = (U16)(1 + last);
    s_spMenu[1] = (U16)(1 + s_nbSpecials);
    s_spMenu[2] = 280;
    s_spMenu[3] = 0;
    SetRow(s_spMenu, 0, RG_ID_BACK);
    for (int i = 0; i < s_nbSpecials; i++) SetRow(s_spMenu, 1 + i, (U16)(RG_ID_SPECIAL + i));
    for (;;) {
        Page("Specials", 280 - (56 * (1 + s_nbSpecials)) / 2 - 50);
        const S32 select = Menu(s_spMenu);
        if (select == RG_ID_BACK || select == 1000) { Page(NULL, 0); return 0; }
        if (select < RG_ID_SPECIAL || select >= RG_ID_SPECIAL + s_nbSpecials) continue;
        s_lastSp = s_spIndex[select - RG_ID_SPECIAL];
        if (ModePage(s_lastSp)) return 1;
    }
}

// ---- the congratulations ------------------------------------------------------------------------------------------------------------

// A pixel block of the page (Log), clipped to the screen.
static void Plot(S32 x, S32 y, U8 colour, S32 size) {
    for (S32 dy = 0; dy < size; dy++)
        for (S32 dx = 0; dx < size; dx++) {
            const S32 px = x + dx, py = y + dy;
            if (px >= 0 && py >= 0 && px < (S32)ModeDesiredX && py < (S32)ModeDesiredY) ((U8 *)Log)[TabOffLine[py] + px] = colour;
        }
}

// A padlock centred on (cx, cy), in gold with the menus' dark shadow: its shackle (a thick ring's upper half and its legs), its body and
// its keyhole. Drawn straight into the page, as the stars are.
static void Padlock(S32 cx, S32 cy) {
    static U8 gold = 0, dark = 0, shadow = 0;
    static const U8 *paletteWas = NULL;
    const U8 *pal = PtrPalNormal ? PtrPalNormal : PtrPal;
    if (pal != paletteWas) {
        paletteWas = pal;
        gold = NearestColour(235, 185, 60);
        dark = NearestColour(70, 45, 15);
        shadow = NearestColour(0, 0, 0);
    }
    const S32 halfW = 22, top = cy - 4, bottom = cy + 26, ringY = top, outer = 17, inner = 10;
    for (int pass = 0; pass < 2; pass++) {
        const S32 off = pass ? 0 : 3;
        const U8 c = pass ? gold : shadow;
        for (S32 dy = -outer; dy <= bottom - cy; dy++)
            for (S32 dx = -halfW; dx <= halfW; dx++) {
                const S32 y = cy + dy;
                // (the body)
                int in = y >= top && y <= bottom;
                // (the shackle: the ring's upper half over the body, its legs down into it)
                const S32 ry = y - ringY, d2 = dx * dx + ry * ry;
                if (!in && ry <= 0 && d2 <= outer * outer && d2 >= inner * inner) in = 1;
                if (in) Plot(cx + dx + off, y + off, c, 1);
            }
    }
    // the keyhole
    for (S32 dy = -4; dy <= 4; dy++)
        for (S32 dx = -4; dx <= 4; dx++)
            if (dx * dx + dy * dy <= 16) Plot(cx + dx, top + 11 + dy, dark, 1);
    for (S32 dy = 0; dy < 10; dy++) Plot(cx - 1, top + 13 + dy, dark, 3);
}

// Fireworks over the page (2026-10-20 -- the user: "Let's also add some on screen fireworks to accompany the firework.mp3 sound"): rockets
// rising from the foot of the screen with a spark of a trail, bursting near their top into a ring of sparks that fly out, fall, fade
// through their colour's shades and twinkle out -- red, gold, green, blue, violet, orange or white, the menus' palette's nearest. Drawn
// into the page (Log) like the stars, a few pixels each.
#define RG_FW_SPARKS 640
#define RG_FW_ROCKETS 8
#define RG_FW_KINDS 7
#define RG_FW_SHADES 4
typedef struct { float X, Y, VX, VY; S32 Born, Life; U8 Kind, On; } T_RG_SPARK;
typedef struct { float X, Y, VX, VY, Top; U8 Kind, On; } T_RG_ROCKET;
static T_RG_SPARK s_spark[RG_FW_SPARKS];
static T_RG_ROCKET s_rocket[RG_FW_ROCKETS];
static U8 s_fwShade[RG_FW_KINDS][RG_FW_SHADES];
static U32 s_fwSeed = 1;

static float FwRandom(void) {
    s_fwSeed = s_fwSeed * 1664525u + 1013904223u;
    return (float)(s_fwSeed >> 8) / 16777216.0f;
}

static void FwColours(void) {
    static const U8 *paletteWas = NULL;
    const U8 *pal = PtrPalNormal ? PtrPalNormal : PtrPal;
    if (pal == paletteWas) return;
    paletteWas = pal;
    static const int rgb[RG_FW_KINDS][3] = {{255, 70, 50}, {255, 205, 60}, {90, 255, 110}, {100, 165, 255}, {215, 110, 255}, {255, 140, 30}, {255, 255, 255}};
    // (dimmed no further than this: the menu picture's night is a dark violet, and a spark dimmer still came out in its colours, lost in it)
    static const float level[RG_FW_SHADES] = {1.0f, 0.88f, 0.74f, 0.6f};
    for (int k = 0; k < RG_FW_KINDS; k++)
        for (int l = 0; l < RG_FW_SHADES; l++)
            s_fwShade[k][l] = NearestColour((int)(rgb[k][0] * level[l]), (int)(rgb[k][1] * level[l]), (int)(rgb[k][2] * level[l]));
}

// A rocket off the foot of the screen, in its left or right third (the car's box is in the middle), to burst two fifths to nine tenths of
// the way up.
static void FwLaunch(void) {
    static int left = 0;
    for (int i = 0; i < RG_FW_ROCKETS; i++) {
        T_RG_ROCKET *r = &s_rocket[i];
        if (r->On) continue;
        const float w = (float)ModeDesiredX, h = (float)ModeDesiredY, across = 0.04f + 0.24f * FwRandom();
        left = !left;
        r->X = w * (left ? across : 1 - across);
        r->Y = h + 4;
        r->VX = (FwRandom() - 0.5f) * 0.05f;
        r->VY = -(0.42f + 0.16f * FwRandom()) * h / 480;
        r->Top = h * (0.1f + 0.5f * FwRandom());
        r->Kind = (U8)(FwRandom() * RG_FW_KINDS) % RG_FW_KINDS;
        r->On = 1;
        return;
    }
}

// A burst where a rocket got to: a ring of sparks flying out, a few of them white.
static void FwBurst(const T_RG_ROCKET *r, S32 now) {
    const int n = 60 + (int)(FwRandom() * 36);
    const float speed = (0.13f + 0.08f * FwRandom()) * (float)ModeDesiredY / 480;
    for (int k = 0, i = 0; k < n && i < RG_FW_SPARKS; i++) {
        T_RG_SPARK *p = &s_spark[i];
        if (p->On) continue;
        const float a = 6.2831853f * (k + FwRandom() * 0.6f) / n, v = speed * (0.55f + 0.45f * FwRandom());
        p->X = r->X; p->Y = r->Y;
        p->VX = cosf(a) * v; p->VY = sinf(a) * v;
        p->Born = now; p->Life = 900 + (S32)(FwRandom() * 700);
        p->Kind = FwRandom() < 0.12f ? (U8)(RG_FW_KINDS - 1) : r->Kind;
        p->On = 1;
        k++;
    }
}

// The rockets and the sparks `dt` ms on: rising (slowing) and bursting, flying out (slowing) and falling.
static void FwStep(S32 now, S32 dt) {
    const float g = 0.00030f * (float)ModeDesiredY / 480;
    for (int i = 0; i < RG_FW_ROCKETS; i++) {
        T_RG_ROCKET *r = &s_rocket[i];
        if (!r->On) continue;
        r->VY += g * dt;
        r->X += r->VX * dt; r->Y += r->VY * dt;
        if (r->Y <= r->Top || r->VY >= -0.05f) { FwBurst(r, now); r->On = 0; }
    }
    const float drag = 1 - 0.0011f * dt;
    for (int i = 0; i < RG_FW_SPARKS; i++) {
        T_RG_SPARK *p = &s_spark[i];
        if (!p->On) continue;
        if (now - p->Born >= p->Life) { p->On = 0; continue; }
        p->VX *= drag; p->VY = p->VY * drag + g * 0.4f * dt;
        p->X += p->VX * dt; p->Y += p->VY * dt;
    }
}

static void FwDraw(S32 now) {
    for (int i = 0; i < RG_FW_ROCKETS; i++) {
        const T_RG_ROCKET *r = &s_rocket[i];
        if (!r->On) continue;
        // (its head white, a trail of gold sparks under it)
        Plot((S32)r->X - 1, (S32)r->Y - 1, s_fwShade[RG_FW_KINDS - 1][0], 3);
        for (int k = 1; k <= 5; k++)
            if (FwRandom() < 0.8f) Plot((S32)(r->X - r->VX * k * 10 + (FwRandom() - 0.5f) * 3), (S32)(r->Y - r->VY * k * 10), s_fwShade[1][k < 3 ? k - 1 : 3], 1 + (k < 3));
    }
    for (int i = 0; i < RG_FW_SPARKS; i++) {
        const T_RG_SPARK *p = &s_spark[i];
        if (!p->On) continue;
        const float age = (float)(now - p->Born) / p->Life;
        // (bright for half its life, then a shade darker in steps -- twinkling out over its last third)
        if (age > 0.66f && FwRandom() < 0.4f) continue;
        const int shade = age < 0.5f ? 0 : age < 0.68f ? 1 : age < 0.84f ? 2 : 3;
        Plot((S32)p->X - (age < 0.2f), (S32)p->Y - (age < 0.2f), s_fwShade[p->Kind][shade], age < 0.2f ? 3 : age < 0.6f ? 2 : 1);
        // (and a streak behind it, a shade darker)
        const int tail = shade + 1 < RG_FW_SHADES ? shade + 1 : RG_FW_SHADES - 1;
        Plot((S32)(p->X - p->VX * 18), (S32)(p->Y - p->VY * 18), s_fwShade[p->Kind][tail], 1);
    }
}

// The congratulations for a car a race has just unlocked (the user, 2026-10-20: "play this sound E:\dump\LBAAssembler\sounds\firework.mp3
// and play a congratulations message on screen featuring the F1 Pumpkin car, with a message saying they've unlocked the pumpkin for use
// in time trials"): on the menu's own picture (the Halloween night the race game opens on -- not the race's scene, its display and its
// last message left in it, which the menu after a race has behind it) fireworks, "Congratulations!", the car turning in its box, the race
// won and the car unlocked -- RACESOUNDS/FIREWORK.WAV playing. Enter, Esc or a click (after a moment: the keys the race was driven with
// aren't taken for it) or the sound's end and a little more: the menu, its backdrop as it was.
#define RG_CELEBRATE_LEAST 9000             // shown this long at least ...
#define RG_CELEBRATE_MOST 32000             // ... and at most (the fireworks sound and RG_CELEBRATE_AFTER: 30 s)
#define RG_CELEBRATE_AFTER 1500
#define RG_CELEBRATE_ARMED 800              // the keys taken from this far in
static void CelebratePage(int car) {
    if (car < 0 || car >= s_nbCars) return;
    const T_RG_CAR *c = &s_cars[car];
    Page(NULL, 0);
    BoxUpdate();
    ManageTime();
    InitWaitNoKey();
    InitWaitNoInput(I_JOY | I_FIRE | I_MENUS | I_ACTION);
    AsciiMode = TRUE;
    s_clickWas = Click & 3;
    FwColours();
    memset(s_spark, 0, sizeof(s_spark));
    memset(s_rocket, 0, sizeof(s_rocket));
    s_fwSeed = (U32)time(NULL) * 2654435761u + 1;
    // (the menu's picture in the backdrop's place -- the backdrop kept, and put back after)
    const size_t screenBytes = (size_t)TabOffLine[1] * (size_t)ModeDesiredY;
    U8 *backdrop = (U8 *)malloc(screenBytes);
    if (backdrop) memcpy(backdrop, Screen, screenBytes);
    char picture[ADELINE_MAX_PATH];
    GetResPath(picture, ADELINE_MAX_PATH, SCREEN_HQR_NAME);
    Image_LoadStretched(picture, Screen, PCR_MENU);
    int refitted = 0;
    S32 soundMs = 0;
    const U32 sound = RaceMod_PlaySound("FIREWORK.WAV", &soundMs);
    S32 length = soundMs + RG_CELEBRATE_AFTER;
    if (length < RG_CELEBRATE_LEAST) length = RG_CELEBRATE_LEAST;
    if (length > RG_CELEBRATE_MOST) length = RG_CELEBRATE_MOST;
    const S32 start = TimerRefHR;
    S32 last = start, nextLaunch = start + 150;
    int salvo = 3, done = 0, frames = 0;
    S32 cx = UI_CENTER_X, oy = UI_VCENTER_OFS;
    char won[160], line1[160], line2[160];
    snprintf(won, sizeof(won), "You won the %s race!", s_raceSp >= 0 && s_raceSp < s_nbSp ? s_sp[s_raceSp].Name : "");
    if (s_raceSp < 0 || s_raceSp >= s_nbSp) snprintf(won, sizeof(won), "You won the race!");
    snprintf(line1, sizeof(line1), "%s is unlocked", c->Name);
    snprintf(line2, sizeof(line2), c->TrialOnly ? "for your time trials!" : "for your races!");
    Log_Info("[racegame] congratulations: %s (%s %s), fireworks for %.1f s%s", won, line1, line2, length / 1000.0,
             sound == 0xFFFFFFFF ? ", no sound" : "");
    while (!done) {
        ManageTime();
        MyGetInput();
        // (the window resized: the picture again at the new size -- the backdrop made again after, not the one kept)
        if (Refitted()) {
            cx = UI_CENTER_X; oy = UI_VCENTER_OFS; FwColours();
            Image_LoadStretched(picture, Screen, PCR_MENU);
            refitted = 1;
        }
        const S32 now = TimerRefHR;
        S32 dt = now - last;
        if (dt < 0) dt = 0;
        if (dt > 250) dt = 250;
        last = now;
        // the rockets: a salvo of four, then one every half second or so (now and then two), none over the last two seconds
        if (now - start < length - 2000 && now >= nextLaunch) {
            FwLaunch();
            if (FwRandom() < 0.3f) FwLaunch();
            nextLaunch = now + (salvo > 0 ? 150 + (S32)(FwRandom() * 150) : 280 + (S32)(FwRandom() * 480));
            if (salvo > 0) salvo--;
        }
        // (in steps of a 60th of a second at most, however long the frame took: the sparks fly as far in a slow frame as in quick ones)
        for (S32 done = 0; done < dt;) {
            const S32 step = dt - done > 16 ? 16 : dt - done;
            done += step;
            FwStep(now - dt + done, step);
        }
        frames++;
        // (laid out as the car page is, under the picture's title: the header, the box, the words under it)
        const S32 x0 = cx - 175, x1 = cx + 175, y0 = 130 + oy, y1 = 280 + oy;
        // (the car measured first, under the backdrop: RaceMod_DrawBody)
        RaceMod_DrawBody(x0 + 2, y0 + 2, x1 - 2, y1 - 2, c->Body, 0, 1, c->Anim);
        // the page: the picture, the fireworks over its night sky, the car in its box, the words
        CopyScreen(Screen, Log);
        FwDraw(now);
        Text(cx, y0 - 34, "Congratulations!", RG_ORANGE);
        ShadeBoxBlk(x0, y0, x1, y1, MENU_SHADE_LVL);
        RaceMod_DrawBody(x0 + 2, y0 + 2, x1 - 2, y1 - 2, c->Body, (S32)(((now - start) / 3 + 3072) & 4095), 0, c->Anim);
        DrawCadre(x0, y0, x1, y1, ALL_ANGLES);
        TextFitted(cx, y1 + 6, (S32)ModeDesiredX - 40, won, LBAWHITE);
        Text(cx, y1 + 66, line1, RG_ORANGE);
        Text(cx, y1 + 92, line2, RG_ORANGE);
        Text(cx, y1 + 150, "Enter: continue", RG_GREY);
        BoxStaticAdd(0, 0, (S32)ModeDesiredX - 1, (S32)ModeDesiredY - 1);
        Shot();
        BoxUpdate();
        S32 mx, my;
        const int click = Clicked(&mx, &my);
        if (now - start >= length) done = 1;
        else if (now - start >= RG_CELEBRATE_ARMED && (click || Confirmed() || Escaped())) { WaitNoInput(); done = 1; }
    }
    RaceMod_StopSound(sound);
    // (the menu's backdrop as it was -- or, the window resized meanwhile, made again at its size)
    if (refitted) Menu_RepaintAfterWindowFit();
    else if (backdrop) memcpy(Screen, backdrop, screenBytes);
    free(backdrop);
    AsciiMode = FALSE;
    InitWaitNoKey();
    Page(NULL, 0);
    Log_Info("[racegame] congratulations over after %.1f s (%d frames)", (TimerRefHR - start) / 1000.0, frames);
}

S32 RaceGame_Menu(S32 inGame) {
    Read();
    // (the sum of best played back: a track's best lap over -- the next track's straight on, no menu)
    if (s_watchNext && s_watchListN > 0) {
        s_watchNext = 0;
        s_watchListAt++;
        const int sp = s_watchList[s_watchListAt];
        Log_Info("[racegame] the sum of best played back: %d of %d, %s", s_watchListAt + 1, s_watchListN, s_sp[sp].Name);
        s_lastSp = sp;
        if (StartRace(sp, 1, NULL, -1, s_watchListPath[s_watchListAt])) { Page(NULL, 0); return RACEGAME_START; }
        s_watchListN = 0;
    }
    // (the menu shown in a played-back list's run -- Esc -- leaves the list where it is: Resume goes on with it; anything else, below, ends it)
    // (a race that has just unlocked a car: the congratulations first, once)
    if (s_celebrate >= 0) {
        const int car = s_celebrate;
        s_celebrate = -1;
        CelebratePage(car);
    }
    const int resume = inGame && !s_raceOver;
    // (Restart, 2026-10-25 -- the user: "We should also add in a restart menu option": in a race of the race game's, going on or just over;
    // the greyed Story Mode left out for it, seven rows as ever)
    const int restart = inGame && s_raceSp >= 0 && s_raceSp < s_nbSp;
    S32 nb = 0;
    if (resume) SetRow(s_mainMenu, nb++, RG_ID_RESUME);
    if (restart) SetRow(s_mainMenu, nb++, RG_ID_RESTART);
    if (!restart || s_story) SetRow(s_mainMenu, nb++, RG_ID_STORY);
    SetRow(s_mainMenu, nb++, RG_ID_TRACKS);
    const S32 specials = nb;
    SetRow(s_mainMenu, nb++, RG_ID_SPECIALS);
    SetRow(s_mainMenu, nb++, RG_ID_TIMES);
    SetRow(s_mainMenu, nb++, RG_ID_OPTIONS);
    SetRow(s_mainMenu, nb++, RG_ID_QUIT);
    s_mainMenu[0] = (U16)(resume ? 0 : specials);
    // (the row chosen last -- Track select, Specials, Best laps -- when the menu comes back after a race; Resume first in one)
    static U16 lastChosen = 0;
    if (!resume && lastChosen)
        for (S32 i = 0; i < nb; i++) if (s_mainMenu[4 + 2 * i + 1] == lastChosen && !RaceGame_RowDisabled(lastChosen)) s_mainMenu[0] = (U16)i;
    s_mainMenu[1] = (U16)nb;
    s_mainMenu[2] = (U16)(nb > 6 ? 290 : 300);
    s_mainMenu[3] = 0;
    for (;;) {
        const S32 select = Menu(s_mainMenu);
        // (the window resized: the backdrop is made again -- shaded in a race -- and the rows go on it)
        if (select == MENU_RELAYOUT) continue;
        if (select == RG_ID_TRACKS || select == RG_ID_SPECIALS || select == RG_ID_TIMES) lastChosen = (U16)select;
        if (select != 1000 && select != RG_ID_RESUME && select != MENU_RELAYOUT) s_watchListN = 0;
        switch (select) {
        case 1000:
            if (resume) return 1000;
            break;
        case RG_ID_RESUME:
        case RG_ID_OPTIONS:
        case RG_ID_QUIT:
            return select;
        case RG_ID_TRACKS:
            if (TrackSelect()) return RACEGAME_START;
            break;
        case RG_ID_SPECIALS:
            if (SpecialsPage()) return RACEGAME_START;
            break;
        case RG_ID_TIMES:
            if (TimesPage()) return RACEGAME_START;
            break;
        case RG_ID_RESTART:
            // (the race as it was set up: its track, race or time trial, car, difficulty, ghost and laps -- or the ghost played back again)
            Log_Info("[racegame] restart: %s's %s again", s_sp[s_raceSp].Name, s_raceWatch[0] ? "ghost played back" : s_raceTrial ? "time trial" : "race");
            // (a race restarted as it was chosen: its qualifying lap again, or from the back of the grid again)
            if (!s_raceTrial && !s_raceWatch[0]) s_qualifyNow = s_raceQualify;
            if (StartRace(s_raceSp, s_raceTrial, s_raceCar, s_raceLevel, s_raceWatch[0] ? s_raceWatch : NULL)) { Page(NULL, 0); return RACEGAME_START; }
            break;
        }
        // (the main menu again: its page put back; DoGameMenu draws its rows)
        for (S32 i = 0; i < nb; i++) if (s_mainMenu[4 + 2 * i + 1] == (U16)select) s_mainMenu[0] = (U16)i;
        Page(NULL, 0);
    }
}
