#include "C_EXTERN.H"

#include <SYSTEM/LOG.H>

// Falling snow (LBA Assembler, 2026-10-22: the snow island's -- a map whose MAP.TXT says "weather = snow", or ISLANDS.TXT's "weather <file>
// snow" for one installed into the game's files; EXTFUNC.CPP SceneSnows). The rain's way (RAIN.CPP): flakes in a box round the point the
// camera looks at, moved every frame and drawn as dots into the depth buffer (LineRain), so the ground and what stands on it hide those
// behind them. A flake falls slowly, drifts with the wind and sways; one that falls below the box, or that the camera has left behind,
// comes back in at the far side -- the box goes where the camera goes. Near flakes are white and two pixels, far ones greyer, into the fog.

#define MAX_SNOW 1600
#define SNOW_HALF 14000  // the box: this far either way of the camera's point
#define SNOW_ABOVE 9000  // ... from over it
#define SNOW_BELOW 4000  // ... to under it
#define SNOW_FALL 9      // world units a millisecond is ten of: falling ~900 a second (the rain falls 10,000)
#define SNOW_DRIFT 3     // ... and the wind's drift
#define SNOW_SWAY 260    // the sway's reach either way
#define SNOW_NEAR 7000   // flakes nearer the camera than this drawn three pixels across, nearer than SNOW_MID two
#define SNOW_MID 15000

typedef struct {
    S32 X, Y, Z;
    S32 Phase; // its sway, its own (0..4095)
} T_SNOW;

static T_SNOW TabSnow[MAX_SNOW];
static S32 SnowLastTimer = 0;
static S32 SnowScene = -2, SnowHere = FALSE;

extern S32 SceneSnows(S32 scene); // EXTFUNC.CPP

S32 SnowFalls(void) {
    if (CubeMode != CUBE_EXTERIEUR) return FALSE;
    if (NumCube != SnowScene) {
        SnowScene = NumCube;
        SnowHere = SceneSnows(NumCube);
        if (SnowHere) InitSnow();
        if (SnowHere) Log_Info("[snow] scene %d: snow falls%s", (int)NumCube, RainEnable ? "" : " (not drawn: the detail level's weather is off)");
    }
    return SnowHere && RainEnable;
}

static void InitOneSnow(T_SNOW *pt, S32 anyHeight) {
    pt->X = VueOffsetX - SNOW_HALF + MyRnd(2 * SNOW_HALF);
    pt->Z = VueOffsetZ - SNOW_HALF + MyRnd(2 * SNOW_HALF);
    pt->Y = anyHeight ? VueOffsetY - SNOW_BELOW + MyRnd(SNOW_ABOVE + SNOW_BELOW) : VueOffsetY + SNOW_ABOVE - MyRnd(1500);
    pt->Phase = MyRnd(4096);
}

void InitSnow(void) {
    for (S32 i = 0; i < MAX_SNOW; i++) InitOneSnow(&TabSnow[i], TRUE);
    SnowLastTimer = 0;
}

// (a coordinate brought back into the box round `centre`: the camera moved on, the flake comes in at the other side)
static S32 Wrap(S32 v, S32 centre) {
    while (v < centre - SNOW_HALF) v += 2 * SNOW_HALF;
    while (v >= centre + SNOW_HALF) v -= 2 * SNOW_HALF;
    return v;
}

void GereSnow(void) {
    const S32 now = TimerRefHR;
    const S32 dt = SnowLastTimer ? now - SnowLastTimer : 0;
    SnowLastTimer = now;
    if (dt <= 0 || dt > 500) return;
    for (S32 i = 0; i < MAX_SNOW; i++) {
        T_SNOW *pt = &TabSnow[i];
        pt->Y -= dt * SNOW_FALL / 10;
        pt->X += dt * SNOW_DRIFT / 10;
        pt->Z += dt * SNOW_DRIFT / 20;
        pt->Phase = (pt->Phase + dt) & 4095;
        if (pt->Y < VueOffsetY - SNOW_BELOW) InitOneSnow(pt, FALSE);
        pt->X = Wrap(pt->X, VueOffsetX);
        pt->Z = Wrap(pt->Z, VueOffsetZ);
    }
}

void AffSnow(void) {
    S32 drawn = FALSE;
    UnsetClip();
    for (S32 i = 0; i < MAX_SNOW; i++) {
        const T_SNOW *pt = &TabSnow[i];
        // (its sway: round a little circle, its own phase)
        const S32 sway = (SNOW_SWAY * SinTab[pt->Phase & 4095]) >> 14;
        const S32 swayZ = (SNOW_SWAY * SinTab[(pt->Phase + 1024) & 4095]) >> 14;
        LongWorldRotatePoint(pt->X + sway, pt->Y, pt->Z + swayZ);
        if (!LongProjectPoint(X0, Y0, Z0)) continue;
        const S32 zr = CameraZr - Z0;
        if (zr <= 0 || zr >= ClipZFar) continue;
        const S32 x = Xp, y = Yp;
        // (the screen's size -- not ScreenXMin and the rest: LineRain's box of what it drew, emptied when it draws nothing)
        if (x < 0 || y < 0 || x + 3 >= (S32)ModeDesiredX || y + 3 >= (S32)ModeDesiredY) continue;
        const S32 z = RegleTrois(0, 65535, ClipZFar, zr);
        // (white near, the grey ramp's paler greys into the fog)
        const S32 c = BoundRegleTrois(16 * 3 + 15, 16 * 3 + 9, ClipZFar - StartZFog, zr);
        // (a dot of lines a pixel or two down -- LineRain leaves out a flat one: three pixels square near the camera, two further, one far)
        const S32 size = zr < SNOW_NEAR ? 3 : zr < SNOW_MID ? 2 : 1;
        S32 f = 0;
        for (S32 k = 0; k < size; k++) f |= LineRain(x + k, y, z, x + k, y + (size > 1 ? size - 1 : 1), z, c);
        if (f & 1) drawn = TRUE;
    }
    if (drawn) BoxMovingAdd(0, 0, (S32)ModeDesiredX - 1, (S32)ModeDesiredY - 1);
}
