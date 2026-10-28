#include "C_EXTERN.H"

#include "CONSOLE/CONSOLE.H" /* Console_RegisterCvar, for this module's own cvars */
#include "FOLLOWCAM.H"
#include "FOLLOWCAM_CFG.H"
#include "FOLLOWCAM_MATH.H"
#include "SETTINGS.H"
#include "INPUT.H"
#include <SYSTEM/KEYBOARD_KEYS.H>
#include <SYSTEM/LOG.H>
#include <math.h>

/* FollowCamera refreshes terrain with AFF_ALL_FLIP every movement frame.
 * AFF_ALL_FLIP runs the full AffScene preamble (MemoClipWindow / UnsetClipWindow
 * / ClearImpactRain) before the render, preventing black edges from active clip
 * regions. Wall-clock time during the render+vsync wait is correctly credited
 * to TimerRefHR by ManageTime() after the LIB386/SYSTEM/TIMER.CPP generalised
 * fix; see docs/TIMING.md. Vsync naturally caps terrain refreshes at the
 * display rate (~60 Hz). When the hero is idle, all work is skipped entirely. */
/* Settings, read from the cfg and mostly exposed as console cvars. Defaults in
 * FOLLOWCAM_CFG.H. Declared in FOLLOWCAM.H. */
S32 FollowCamera = FALSE;
/* The Options' third camera (2026-10-27, the user: "add an option for Auto when in a car, and classic when out"): FollowCamera set each
   frame by whether Twinsen drives (PERSO.CPP's main loop) */
S32 FollowCameraInCar = FALSE;
S32 FollowCamReengageFrames = FOLLOW_CAM_REENGAGE_FRAMES_DEFAULT;
S32 FollowCamDriftDivisor = FOLLOW_CAM_DRIFT_DIVISOR_DEFAULT;
S32 FollowCamHDRecompose = FOLLOW_CAM_HD_RECOMPOSE_DEFAULT;
S32 FollowCamHDExcessCap = FOLLOW_CAM_HD_EXCESS_CAP_DEFAULT;
S32 FollowCamHDPitchGain = FOLLOW_CAM_HD_PITCH_GAIN_DEFAULT;
S32 FollowCamHDDistGain = FOLLOW_CAM_HD_DIST_GAIN_DEFAULT;
S32 FollowCamHDLeanGain = FOLLOW_CAM_HD_LEAN_GAIN_DEFAULT;
S32 FollowCamGround = FOLLOW_CAM_GROUND_DEFAULT;
S32 FollowCamGroundClearance = FOLLOW_CAM_GROUND_CLEARANCE_DEFAULT;
S32 FollowCamManualHoldFrames = FOLLOW_CAM_MANUAL_HOLD_DEFAULT;
S32 FollowCamHoldAngle = FOLLOW_CAM_HOLD_ANGLE_DEFAULT;

/* Console `camtrace`. */
S32 DbgCamTrace = 0;

/* Live state. The public half is declared in FOLLOWCAM.H; everything below the
 * blank line is private to this file and reached only through the four entry
 * points. */
S32 FollowCamBaseDist = 0; /* player's chosen zoom distance (0 = uninitialised) */

static S32 FollowCamPrevHeroX = 0;
static S32 FollowCamPrevHeroY = 0;
static S32 FollowCamPrevHeroZ = 0;
static S32 FollowCamPrevHeroBeta = 0;
static S32 FollowCamPrevAddBeta = 0;    /* dirty check: last seen AddBetaCam */
static S32 FollowCamEffectiveDist = 0;  /* spring arm: smoothed distance (0 = uninitialised) */
static S32 FollowCamTargetBeta = 0;     /* rotation lag: desired BetaCam */
S32 FollowCamReengageDelay = 0;         /* frames before auto-center resumes after manual pan */
S32 FollowCamManualHold = 0;            /* frames the pitch/distance auto-assist yields after a manual nudge */
static S32 FollowCamPrevBaseDist = -1;  /* dirty: zoom from GereExtKeys (numpad * /) */
static S32 FollowCamPrevAlphaCam = -1;  /* dirty: numpad +/- elevation while follow */
static S32 FollowCamEyeLift = 0;        /* ground clearance: eased eye lift above terrain */
static S32 FollowCamAppliedLift = 0;    /* ... and the share of it last applied to the eye (FollowCamReapplyLift) */

/* An eye of a mod's own for the camera that follows the hero, in place of the arm behind
 * him (a race-track mod's rollercoaster of a road, where the eye rides the road behind the
 * car: RACEMOD.CPP): given his object, the eye's place and the point to look at, cube-local;
 * TRUE when it has one for where he is. NULL, or FALSE: the follow camera's own. */
S32 (*FollowCamEyeHook)(S32 *eye, S32 *target) = NULL;
/* Which of the hook's cameras placed the eye this frame (set by the hook: a race-track
 * mod's rail, loop, jump ... camera), for camtrace; NULL: the arm's. */
const char *FollowCamEyeWho = NULL;
/* The run's tick, for camtrace (CONTROL.CPP's tick hook sets it: -1 without one). */
long FollowCamTraceTick = -1;
static S32 FollowCamOwnEye = 0;         /* how far the view has moved onto the hook's eye, of FOLLOW_CAM_OWN_EYE_STEPS */
static S32 FollowCamOwnEyeAt[3], FollowCamOwnEyeLooks[3];
#define FOLLOW_CAM_OWN_EYE_STEPS 40
/* The view eases onto the hook's eye from where it was when the hook took it, and back
 * onto the arm's from where it was when the hook let go: each start held as the eye's and
 * the target's places from the hero, the eye swung round him from one to the other and
 * the target slid along, so he stays in the picture all the while. (It eased from the
 * arm's eye of the frame -- an arm behind a car going up a loop or a jump's ramp swings
 * round with it: 2,000 a frame near the ease's end -- and back onto the arm in one frame,
 * 16,000-22,000 and 90-136 degrees: the race-track mod's loops and its carried jump; then,
 * eased but eye and target apart, 3,000 and 27 degrees a frame, the hero out of the
 * picture's side, 2026-10-12.) */
static double FollowCamFromEye[3], FollowCamFromLooks[3];
static S32 FollowCamLeave = 0;          /* frames left of the ease back onto the arm's eye */
#define FOLLOW_CAM_LEAVE_STEPS 45

/* How far the hero's coordinates moved this frame by a change of cube (one cube's width,
 * either way, or none): the eye held from last frame -- the hook's, or the ease back from it
 * -- is in the old cube's coordinates, and taken from his new place it was a cube away
 * (the race-track mod's loop camera held past a ring as the car crossed into the next
 * cube, the view swung in from 32,000 away, 2026-10-12). */
static S32 FollowCamCubeJump(S32 now, S32 before) {
    const S32 d = now - before;
    return d > 16384 ? 32768 : d < -16384 ? -32768 : 0;
}

/* Easing in and out over k of n. */
static double FollowCamEaseW(S32 k, S32 n) {
    const double s = (double)k / n;
    return s * s * (3 - 2 * s);
}

/* The eye from the hero swung from `from` to `to` (w 0..1): round him, its length eased
 * from one's to the other's; and the target slid from one to the other. Placed at the
 * hero's place (x, y, z). */
static void FollowCamSwing(const double *from, const double *to, const double *fromLooks, const double *toLooks, double w,
                           S32 x, S32 y, S32 z, S32 *eye, S32 *looks) {
    const double la = sqrt(from[0] * from[0] + from[1] * from[1] + from[2] * from[2]);
    const double lb = sqrt(to[0] * to[0] + to[1] * to[1] + to[2] * to[2]);
    double d[3];
    if (la < 1 OR lb < 1) {
        for (S32 k = 0; k < 3; k++) d[k] = from[k] + (to[k] - from[k]) * w;
    } else {
        double dot = (from[0] * to[0] + from[1] * to[1] + from[2] * to[2]) / (la * lb);
        if (dot > 1) dot = 1; else if (dot < -1) dot = -1;
        const double th = acos(dot), s = sin(th);
        const double ka = s < 1e-3 ? 1 - w : sin((1 - w) * th) / s, kb = s < 1e-3 ? w : sin(w * th) / s;
        const double len = la + (lb - la) * w;
        double u[3], lu = 0;
        for (S32 k = 0; k < 3; k++) { u[k] = from[k] / la * ka + to[k] / lb * kb; lu += u[k] * u[k]; }
        lu = sqrt(lu);
        for (S32 k = 0; k < 3; k++) d[k] = lu > 1e-9 ? u[k] / lu * len : from[k] + (to[k] - from[k]) * w;
    }
    const S32 at[3] = {x, y, z};
    for (S32 k = 0; k < 3; k++) {
        eye[k] = (S32)(at[k] + d[k]);
        looks[k] = (S32)(at[k] + fromLooks[k] + (toLooks[k] - fromLooks[k]) * w);
    }
}

/* A shorter arm than the zoom chosen, where something asks for one (a race-track mod's
 * raised road coming steeply down: RACEMOD.CPP): given the chosen length, the length to
 * use now. NULL: the chosen one. */
S32 (*FollowCamArmHook)(S32 base) = NULL;
static S32 FollowCamGroundSettling = 0; /* ground clearance: lift still easing (keeps the dirty check live while the hero stands still) */

/* A mod's own say in the view (a race-track mod's camera views, RACEMOD.CPP camera=, 2026-10-21): the arm's length, in percent of the
 * zoom chosen (a view further back: 100 the zoom itself); with the hook's eye, the eye's own angles -- SetAngleCamera's alpha, beta and
 * gamma, FollowCamEyeTurn set by the hook with its eye -- instead of a point to look at (a view from inside a car riding round a loop,
 * past straight up, which a point to look at can't give); and a cut onto the hook's eye, or off it, instead of the ease (the player
 * changing the view: FollowCamEyeCut, set by the mod, the next update's). */
S32 FollowCamArmPercent = 100;
S32 FollowCamEyeTurn = FALSE;
S32 FollowCamEyeAngle[3] = {0, 0, 0};
S32 FollowCamEyeCut = FALSE;
static S32 FollowCamOwnTurn = FALSE;    /* the view on the hook's eye at the hook's own angles (FollowCamOwnAngle) */
static S32 FollowCamOwnAngle[3];

/* This file's live settings, applied to the rule in FOLLOWCAM_MATH.H (which the
 * host test covers). Callers pass 0 instead inside a camera zone, so an authored
 * shot is never recomposed. Tuning in FOLLOWCAM_CFG.H. */
static S32 FollowCamHDExcess(void) {
    return FollowCamHDExcessFor(FollowCamHDRecompose, (S32)ModeDesiredY, FollowCamHDExcessCap);
}

/* camtrace: one line per frame describing where the camera is and who is deciding
 * that.
 *
 * Emitted from the main loop rather than from the Auto camera's own update, which
 * runs only while the Auto camera holds the view: a trace written from in there
 * can never report a camera zone, a cutscene, an interior or the classic camera,
 * because it does not run in any of them. Those are most of the camera system and
 * all of the moments it changes hands.
 *
 * `step` is BetaCam's movement since the previous frame, so it counts whoever
 * moved it -- the Auto camera's lerp, a zone's authored angle, a scripted order,
 * or the classic camera's quarter turns -- rather than only the one writer the
 * old placement could see. `zone` and `forced` separate the frame a camera zone
 * re-aims the view from the state of being under its shot. */
void FollowCamTrace(void) {
    static S32 prevBeta = 0;
    static S32 prevX = 0;
    static S32 prevZ = 0;
    static S32 primed = FALSE;

    T_OBJ_3D *ptr3d = &ListObjet[NumObjFollow].Obj;
    S32 step;
    S32 moving;

    if (!DbgCamTrace) {
        /* Keep the references current while the trace is off, so switching it on
         * mid-run reports a real step rather than the distance back to whenever
         * it was last on. */
        prevBeta = BetaCam;
        prevX = ptr3d->X;
        prevZ = ptr3d->Z;
        primed = TRUE;
        return;
    }

    /* The first traced frame has nothing to measure against, and reporting the
     * distance from an unset reference would put a step of a couple of thousand
     * units at the top of the run -- which is the shape of the very defects these
     * traces are read for, and would be picked up by anything scanning for the
     * largest movement. */
    step = 0;
    moving = 0;
    if (primed) {
        step = BetaCam - prevBeta;
        if (step > 2048)
            step -= 4096;
        if (step < -2048)
            step += 4096;

        moving = (ptr3d->X != prevX OR ptr3d->Z != prevZ);
    }
    primed = TRUE;

    /* (and, for a race-track mod's cameras: what the view looks at, the hero, the cube,
     * how far the view has eased onto the hook's eye and which of its cameras it is) */
    const S32 *looks = FollowCamOwnEye OR FollowCamLeave ? FollowCamOwnEyeLooks : NULL;
    Log_Info("[cam] tick %ld heroBeta %4d add %4d beta %4d target %4d step %5d delay %3d orbit %d moving %d "
             "zone %d forced %d cine %d follow %d ext %d vue %d alpha %4d dist %6d eye %d,%d,%d "
             "look %d,%d,%d hero %d,%d,%d cube %d,%d own %d who %s",
             FollowCamTraceTick, (int)ptr3d->Beta, (int)AddBetaCam, (int)BetaCam, (int)FollowCamTargetBeta,
             (int)step, (int)FollowCamReengageDelay, (int)ManualCameraOrbitActive, (int)moving,
             (int)CameraZone, (int)FlagCameraForcee, (int)CinemaMode, (int)FollowCamera,
             (int)(CubeMode == CUBE_EXTERIEUR), (int)VueCamera, (int)AlphaCam, (int)VueDistance,
             (int)CameraX, (int)CameraY, (int)CameraZ,
             (int)(looks ? looks[0] : VueOffsetX), (int)(looks ? looks[1] : VueOffsetY), (int)(looks ? looks[2] : VueOffsetZ),
             (int)ptr3d->X, (int)ptr3d->Y, (int)ptr3d->Z, (int)CurrentCubeX, (int)CurrentCubeY, (int)FollowCamOwnEye,
             FollowCamOwnEye ? (FollowCamEyeWho ? FollowCamEyeWho : "hook") : FollowCamLeave ? "leave" : "arm");

    prevBeta = BetaCam;
    prevX = ptr3d->X;
    prevZ = ptr3d->Z;
}

/* The follow camera re-renders when the hero moved or turned, the player panned,
 * BetaCam still has catch-up pending, the zoom/elevation changed, or the
 * ground-lift / manual-hold are still settling (the last two keep this true so
 * they finish easing while the hero stands still). */
static S32 FollowCamNeedsUpdate(T_OBJ_3D *ptr3d, S32 catchupDiff) {
    return ptr3d->X != FollowCamPrevHeroX OR ptr3d->Y != FollowCamPrevHeroY OR ptr3d->Z != FollowCamPrevHeroZ OR ptr3d->Beta != FollowCamPrevHeroBeta OR AddBetaCam != FollowCamPrevAddBeta OR(catchupDiff > FOLLOW_CAM_ROT_THRESHOLD OR catchupDiff < -FOLLOW_CAM_ROT_THRESHOLD) OR FollowCamBaseDist != FollowCamPrevBaseDist OR AlphaCam != FollowCamPrevAlphaCam OR FollowCamGroundSettling OR FollowCamManualHold OR FollowCamEyeCut;
}

/* The eye put at (x, y, z), looking at the view's target (VueOffset). Not the library's
 * SetTargetCamera: that takes the target's height from the eye's Z and its Z from the
 * eye's height (LIB386/3D/CAMERA.CPP), which nothing showed while every lifted eye was
 * put back by the next redraw (FollowCamReapplyLift); a lifted eye aimed with it looks
 * at the sky. */
static void FollowCamLiftEye(S32 x, S32 y, S32 z) {
    /* The target leads the hero (the forward lean). From an eye lifted high the view
     * is steep, and a point that far ahead of him leaves him at the screen's bottom
     * edge or under it: the higher the lift, the more of the lead is given up, down
     * to a third of it. */
    T_OBJ_3D *hero = &ListObjet[NumObjFollow].Obj;
    S32 keep = 100 - FollowCamAppliedLift * 100 / 6000;
    if (keep < 33)
        keep = 33;
    S32 tx = hero->X + (VueOffsetX - hero->X) * keep / 100;
    S32 tz = hero->Z + (VueOffsetZ - hero->Z) * keep / 100;

    SetPosCamera(x, y, z);
    GetAngleVector3D(tx - x, VueOffsetY - y, tz - z);
    SetAngleCamera(X0, Y0, GammaCam);
}

/* The eye put at a place of a mod's own choosing, looking at a point of its choosing. */
static void FollowCamPlaceEye(const S32 *eye, const S32 *target) {
    SetPosCamera(eye[0], eye[1], eye[2]);
    GetAngleVector3D(target[0] - eye[0], target[1] - eye[1], target[2] - eye[2]);
    SetAngleCamera(X0, Y0, GammaCam);
}

/* The view on the hook's eye as it was last placed (FollowCamOwnEyeAt): at the hook's own angles where it gave them, else looking at
 * its point (FollowCamOwnEyeLooks). */
static void FollowCamPlaceOwnEye(void) {
    if (FollowCamOwnTurn) {
        SetPosCamera(FollowCamOwnEyeAt[0], FollowCamOwnEyeAt[1], FollowCamOwnEyeAt[2]);
        SetAngleCamera(FollowCamOwnAngle[0], FollowCamOwnAngle[1], FollowCamOwnAngle[2]);
        return;
    }
    FollowCamPlaceEye(FollowCamOwnEyeAt, FollowCamOwnEyeLooks);
}

/* Per-frame Auto-camera update (exterior). Extracted from the main loop:
 * spring-arm zoom, lazy orbit + pan drift, HD recompose, terrain occlusion
 * clearance, and the manual-override fade. Reads/writes the FollowCam* globals
 * and ListObjet[NumObjFollow]. */
void UpdateFollowCameraExt(void) {
    T_OBJ_3D *ptr3d = &ListObjet[NumObjFollow].Obj;
    S32 manualPan = CheckKey(K_LBRACKET) OR CheckKey(K_RBRACKET);
    S32 heroMoving = (ptr3d->X != FollowCamPrevHeroX OR ptr3d->Z != FollowCamPrevHeroZ);

    /* First-frame init: sync to current camera state.
                     * FollowCamTargetBeta defaults to 0 but BetaCam starts ~2048
                     * after CameraCenter(1), causing a bogus initial spin. */
    if (FollowCamBaseDist == 0)
        FollowCamBaseDist = FOLLOW_CAM_INITIAL_DIST;
    if (FollowCamEffectiveDist == 0) {
        FollowCamEffectiveDist = FollowCamBaseDist;
        FollowCamTargetBeta = BetaCam;
        FollowCamPrevBaseDist = FollowCamBaseDist;
        FollowCamPrevAlphaCam = AlphaCam;
    }

    /* --- AddBetaCam drift ---
                     * Auto-recenter the manual pan back to behind the hero while
                     * walking. Disabled when FollowCamHoldAngle (cam_hold_angle,
                     * default on): the camera then holds whatever angle the player
                     * set until the scene changes or they hit Center camera
                     * (both go through CameraCenter(1), which zeroes AddBetaCam),
                     * matching a free third-person camera. */
    if (manualPan) {
        FollowCamReengageDelay = FollowCamReengageFrames;
    } else if (heroMoving) {
        if (FollowCamReengageDelay > 0) {
            FollowCamReengageDelay--;
        } else if (!FollowCamHoldAngle AND AddBetaCam != 0) {
            S32 drift = AddBetaCam;
            if (drift > 2048)
                drift -= 4096;
            S32 driftDiv = FollowCamDriftDivisor;
            if (driftDiv < 1)
                driftDiv = 1; /* console can set 0; never divide by zero */
            S32 step = drift / driftDiv;
            if (step > 0 AND step < FOLLOW_CAM_DRIFT_MIN_STEP)
                step = FOLLOW_CAM_DRIFT_MIN_STEP;
            if (step<0 AND step> - FOLLOW_CAM_DRIFT_MIN_STEP)
                step = -FOLLOW_CAM_DRIFT_MIN_STEP;
            if (drift > FOLLOW_CAM_DRIFT_THRESHOLD OR drift < -FOLLOW_CAM_DRIFT_THRESHOLD)
                AddBetaCam = (AddBetaCam - step) & 4095;
            else
                AddBetaCam = 0;
        }
    }

    /* --- Dirty check ---
                     * Triggers on position, facing, pan, or pending BetaCam
                     * catchup.  Hero Beta IS included: the camera always lazily
                     * orbits behind the hero whether running, turning in place,
                     * or standing.  Smoothness comes from the lerp speed, not
                     * from skipping updates. */
    S32 catchupDiff = FollowCamAngleDiff(FollowCamTargetBeta, BetaCam);

    if (FollowCamNeedsUpdate(ptr3d, catchupDiff)) {
        /* HD recompose strength (kExcess): 0 at 480 or when disabled. Shared by
                         * the forward-lean cut here and the boom pull-in + pitch steepen at
                         * the apply below, so the whole recompose is a no-op at 480.
                         *
                         * Not gated on CameraZone. This function has one caller and it sits
                         * inside `if (!CameraZone)`, so a camera zone never reaches here to
                         * be tested for: it keeps the follow camera from running at all,
                         * rather than running it with the correction switched off. */
        S32 hdExcess = FollowCamHDExcess();
        /* (the arm's resting length: the zoom chosen, or the share of it a mod asks for -- FollowCamArmPercent) */
        S32 armBase = FollowCamBaseDist;
        if (FollowCamArmPercent > 0 AND FollowCamArmPercent != 100)
            armBase = FollowCamBaseDist * FollowCamArmPercent / 100;

        /* Manual-override fade: a manual nudge set FollowCamManualHold
                         * (EXTFUNC), driving the auto-assist strength to 0 so the
                         * player's tilt/orbit wins; it eases back to full as the hold
                         * counts down. Scales the recompose pitch steepen and the
                         * ground-lift re-aim below (the two that fight a manual tilt);
                         * 100 (full assist) when no hold. */
        if (FollowCamManualHold > 0)
            FollowCamManualHold--;
        /* The console sets cvars without going through the config's range check,
         * so clamp where it is used, as the orbit glide does in EXTFUNC. Both
         * bounds are load-bearing here rather than tidiness: the hold counter is
         * seeded from the frame count, so an unbounded cam_manual_hold makes
         * `hold * 100` leave S32, and the negative product that follows drives
         * the fade to full assist exactly when the player has asked for none. */
        S32 holdFrames = FollowCamManualHoldFrames;
        if (holdFrames > FOLLOW_CAM_MANUAL_HOLD_MAX)
            holdFrames = FOLLOW_CAM_MANUAL_HOLD_MAX;
        S32 hold = FollowCamManualHold;
        if (hold > holdFrames)
            hold = holdFrames;
        S32 autoFactor = 100;
        if (holdFrames > 0)
            autoFactor = 100 - hold * 100 / holdFrames;
        if (autoFactor < 0)
            autoFactor = 0;
        if (autoFactor > 100)
            autoFactor = 100;

        /* Use the camera's current angle for the forward lean,
                         * not the hero's facing.  This keeps the orbit center
                         * stable when the hero turns in place; the lean point
                         * drifts smoothly as BetaCam lerps toward the target,
                         * instead of snapping with the hero's Beta. */
        S32 leanBeta = (2048 - BetaCam) & 4095;
        S32 leanDist = DISTANCE_VISEE;
        if (hdExcess) {
            /* Shorten the forward lean so the hero is not pushed
                             * toward the bottom edge of the taller frame. */
            leanDist = FollowCamHDShrink(DISTANCE_VISEE, hdExcess,
                                         FollowCamHDLeanGain, FOLLOW_CAM_HD_LEAN_MAX_CUT);
        }
        Rotate(0, leanDist, leanBeta);

        VueOffsetX = ptr3d->X + X0;
        VueOffsetY = ptr3d->Y;
        VueOffsetZ = ptr3d->Z + Z0;

        StartXCube = VueOffsetX / SIZE_BRICK_XZ;
        StartYCube = VueOffsetY / SIZE_BRICK_Y;
        StartZCube = VueOffsetZ / SIZE_BRICK_XZ;

        /* Rotation lag with distance-based inertia.
                         * The camera always tracks "behind the hero".  The lerp
                         * divisor scales with the spring arm length: closer camera
                         * (arm shortened near terrain) → tighter follow; full
                         * distance → lazy, cinematic drift.  Floor at divisor/3
                         * to avoid jitter when very close. */
        FollowCamTargetBeta = FollowCamTargetBetaFor(AddBetaCam, ptr3d->Beta);

        if (manualPan) {
            BetaCam = FollowCamTargetBeta;
        } else if (ManualCameraOrbitActive OR FollowCamReengageDelay == 0) {
            S32 diff = FollowCamTargetBeta - BetaCam;
            if (diff > 2048)
                diff -= 4096;
            if (diff < -2048)
                diff += 4096;
            if (diff > FOLLOW_CAM_ROT_THRESHOLD OR diff < -FOLLOW_CAM_ROT_THRESHOLD) {
                /* Manual orbit (mouse/stick): tight lerp for responsive feel.
                                 * Auto-follow: distance-scaled inertia for cinematic drift. */
                S32 dynamicDiv;
                if (ManualCameraOrbitActive) {
                    dynamicDiv = ManualCameraSmoothing;
                } else {
                    dynamicDiv = FOLLOW_CAM_ROT_DIVISOR * FollowCamEffectiveDist / armBase;
                    if (dynamicDiv < FOLLOW_CAM_ROT_DIVISOR / 3)
                        dynamicDiv = FOLLOW_CAM_ROT_DIVISOR / 3;
                }
                /* The minimum step is what stops integer division truncating to zero
                 * a few units short of the target, where the camera would neither
                 * step nor snap: it would sit off target for ever with the dirty
                 * check firing and the exterior re-rendering every idle frame. */
                S32 step = FollowCamRotStep(diff, dynamicDiv, FOLLOW_CAM_ROT_MIN_STEP);
                BetaCam = (BetaCam + step) & 4095;
            } else {
                BetaCam = FollowCamTargetBeta;
            }
        }

        /* --- Zoom distance: converge FollowCamEffectiveDist toward
                         * FollowCamBaseDist (numpad * /).  VueDistance must match
                         * FollowCamEffectiveDist before CameraCenter(3) so terrain
                         * render (AffGrilleExt) stays consistent. */
        if (!CameraZone) {
            /* (the arm's length to reach: the zoom chosen, or shorter where the hook
             * asks -- reached a fifth of the way a frame then, it is needed at once) */
            S32 armTarget = armBase;
            S32 armPull = FOLLOW_CAM_SPRING_RECOVER;
            if (FollowCamArmHook) {
                S32 asked = FollowCamArmHook(armBase);
                if (asked < armTarget) {
                    armTarget = asked;
                    if ((FollowCamEffectiveDist - armTarget) / 5 > armPull)
                        armPull = (FollowCamEffectiveDist - armTarget) / 5;
                }
            }
            if (FollowCamEffectiveDist < armTarget) {
                FollowCamEffectiveDist += FOLLOW_CAM_SPRING_RECOVER;
                if (FollowCamEffectiveDist > armTarget)
                    FollowCamEffectiveDist = armTarget;
            } else if (FollowCamEffectiveDist > armTarget) {
                FollowCamEffectiveDist -= armPull;
                if (FollowCamEffectiveDist < armTarget)
                    FollowCamEffectiveDist = armTarget;
            }

            /* HD recompose (render-time only; logical AlphaCam and
                             * FollowCamBaseDist are left untouched and restored
                             * below). Pull the boom in so the subject fills the
                             * taller frame instead of dead sky, and steepen the
                             * pitch to trade sky for ground. No-op when hdExcess
                             * is 0 (480 / disabled / camera zone). */
            S32 renderDist = FollowCamEffectiveDist;
            S32 saveAlphaCam = AlphaCam;
            if (hdExcess) {
                renderDist = FollowCamHDShrink(FollowCamEffectiveDist, hdExcess,
                                               FollowCamHDDistGain, FOLLOW_CAM_HD_DIST_MAX_PULL);

                /* Only the pitch steepen fades under manual control
                                 * (autoFactor): it is what fights the player's tilt.
                                 * The boom pull-in and lean do not fight tilt/orbit,
                                 * so they stay put and the camera does not pop out
                                 * when the player grabs the stick. */
                AlphaCam += hdExcess * FollowCamHDPitchGain / 100 * autoFactor / 100;
                if (AlphaCam > FOLLOW_CAM_ALPHA_MAX)
                    AlphaCam = FOLLOW_CAM_ALPHA_MAX;
            }
            VueDistance = renderDist;
            CameraCenter(3);
            AlphaCam = saveAlphaCam;

            /* A mod's own eye (FollowCamEyeHook): the view moves onto it over a few
             * frames from where the arm had it, and then is it. Nothing of the ground
             * clearance below: the hook's eye is where the mod wants it. */
            S32 ownEye[3], ownTarget[3];
            FollowCamEyeWho = NULL;
            FollowCamEyeTurn = FALSE;
            if (FollowCamEyeHook AND FollowCamEyeHook(ownEye, ownTarget)) {
                const S32 here[3] = {ptr3d->X, ptr3d->Y, ptr3d->Z};
                const S32 jump[3] = {FollowCamCubeJump(ptr3d->X, FollowCamPrevHeroX), 0, FollowCamCubeJump(ptr3d->Z, FollowCamPrevHeroZ)};
                if (FollowCamOwnEye == 0) {
                    /* (from the view as it is: the arm's, or partway back to it -- last frame's, in
                     * this frame's cube) */
                    for (S32 k = 0; k < 3; k++) {
                        FollowCamFromEye[k] = (FollowCamLeave > 0 ? FollowCamOwnEyeAt[k] + jump[k] : (k == 0 ? CameraX : k == 1 ? CameraY : CameraZ)) - here[k];
                        FollowCamFromLooks[k] = (FollowCamLeave > 0 ? FollowCamOwnEyeLooks[k] + jump[k] : (k == 0 ? VueOffsetX : k == 1 ? VueOffsetY : VueOffsetZ)) - here[k];
                    }
                    FollowCamLeave = 0;
                }
                /* (a cut asked for: on it at once) */
                if (FollowCamEyeCut)
                    FollowCamOwnEye = FOLLOW_CAM_OWN_EYE_STEPS;
                else if (FollowCamOwnEye < FOLLOW_CAM_OWN_EYE_STEPS)
                    FollowCamOwnEye++;
                double to[3], toLooks[3];
                for (S32 k = 0; k < 3; k++) { to[k] = ownEye[k] - here[k]; toLooks[k] = ownTarget[k] - here[k]; }
                FollowCamSwing(FollowCamFromEye, to, FollowCamFromLooks, toLooks, FollowCamEaseW(FollowCamOwnEye, FOLLOW_CAM_OWN_EYE_STEPS),
                               here[0], here[1], here[2], FollowCamOwnEyeAt, FollowCamOwnEyeLooks);
                FollowCamEyeLift = 0;
                FollowCamAppliedLift = 0;
                FollowCamGroundSettling = 0;
                /* (the hook's own angles, once the view is all the way onto its eye) */
                FollowCamOwnTurn = FollowCamEyeTurn AND FollowCamOwnEye >= FOLLOW_CAM_OWN_EYE_STEPS;
                if (FollowCamOwnTurn)
                    for (S32 k = 0; k < 3; k++) FollowCamOwnAngle[k] = FollowCamEyeAngle[k] & 4095;
                FollowCamPlaceOwnEye();
            } else {
            FollowCamOwnTurn = FALSE;
            if (FollowCamEyeCut) {
                /* (a cut asked for: the arm's eye at once) */
                FollowCamLeave = 0;
            } else if (FollowCamOwnEye > 0) {
                /* (the hook let go: back onto the arm from where the view is -- last frame's, in
                 * this frame's cube) */
                const S32 here[3] = {ptr3d->X, ptr3d->Y, ptr3d->Z};
                const S32 jump[3] = {FollowCamCubeJump(ptr3d->X, FollowCamPrevHeroX), 0, FollowCamCubeJump(ptr3d->Z, FollowCamPrevHeroZ)};
                for (S32 k = 0; k < 3; k++) {
                    FollowCamFromEye[k] = FollowCamOwnEyeAt[k] + jump[k] - here[k];
                    FollowCamFromLooks[k] = FollowCamOwnEyeLooks[k] + jump[k] - here[k];
                }
                FollowCamLeave = FOLLOW_CAM_LEAVE_STEPS;
            }
            FollowCamOwnEye = 0;

            /* Terrain occlusion clearance: a smooth port of the
                             * classic SearchCameraPos awareness. CameraCenter(3)
                             * left the eye at CameraX/Y/Z; march a few points
                             * along the eye->hero line and, where terrain would
                             * rise above the line of sight (a hill between camera
                             * and hero, or the eye sinking into rising ground),
                             * raise the eye just enough to clear the worst
                             * occluder, then re-aim at the hero with the same lib
                             * calls the classic recenter uses. The lift eases
                             * every frame instead of snapping, so it never fights
                             * the orbit (which is what sank the earlier always-on
                             * attempt). Skipped out-of-cube, where
                             * CalculAltitudeObjet is invalid. */
            if (FollowCamGround
                    AND CameraX >= 0 AND CameraX <= 32768 AND CameraZ >= 0 AND CameraZ <= 32768) {
                S32 hx = ptr3d->X, hy = ptr3d->Y, hz = ptr3d->Z;
                S32 ex = CameraX, ey = CameraY, ez = CameraZ;
                S32 targetEyeY = ey; /* never push the eye lower */

                /* Sample fractions along hero(0)->eye(100). Start
                                 * past the hero so the ground it stands on is not
                                 * read as an occluder; 100 is the under-eye case. */
                static const S32 marchFrac[] = {40, 55, 70, 85, 100};
                for (S32 mi = 0; mi < 5; mi++) {
                    S32 f = marchFrac[mi];
                    S32 sx = hx + (ex - hx) * f / 100;
                    S32 sz = hz + (ez - hz) * f / 100;
                    if (sx < 0 OR sx > 32768 OR sz < 0 OR sz > 32768)
                        continue;
                    S32 g = PtrCalculAltitudeY(sx, sz);
                    /* (a race-track mod's raised road: the stretch the hero is on hides him from an eye under it) */
                    {
                        extern S32 (*SightFloorHook)(S32 x, S32 z, S32 y);
                        if (SightFloorHook)
                            g = SightFloorHook(sx, sz, g);
                    }
                    S32 sightY = hy + (ey - hy) * f / 100;
                    if (g + FollowCamGroundClearance > sightY) {
                        /* eye height that drops this occluder just
                                         * under the eye->hero line (pivot at hero) */
                        S32 reqEyeY = hy + (g + FollowCamGroundClearance - hy) * 100 / f;
                        if (reqEyeY > targetEyeY)
                            targetEyeY = reqEyeY;
                    }
                }

                S32 targetLift = targetEyeY - ey;
                if (targetLift < 0)
                    targetLift = 0;
                if (targetLift > FOLLOW_CAM_GROUND_MAX_LIFT)
                    targetLift = FOLLOW_CAM_GROUND_MAX_LIFT;

                S32 d = targetLift - FollowCamEyeLift;
                if (d > FOLLOW_CAM_GROUND_SNAP OR d < -FOLLOW_CAM_GROUND_SNAP) {
                    S32 step = d / FOLLOW_CAM_GROUND_EASE;
                    if (step >= 0 AND step < FOLLOW_CAM_GROUND_MIN_STEP)
                        step = FOLLOW_CAM_GROUND_MIN_STEP;
                    else if (step<0 AND step> - FOLLOW_CAM_GROUND_MIN_STEP)
                        step = -FOLLOW_CAM_GROUND_MIN_STEP;
                    FollowCamEyeLift += step;
                } else {
                    FollowCamEyeLift = targetLift;
                }

                /* Keep the dirty check live until the lift
                                 * settles, so it converges even while the hero
                                 * stands still (e.g. at the foot of a hill). */
                FollowCamGroundSettling = (FollowCamEyeLift != targetLift) ? 1 : 0;

                /* Fade the lift + re-aim out under manual control
                                 * (autoFactor 0): the re-aim would otherwise pin
                                 * the pitch to the geometry and override the
                                 * player's tilt. At autoFactor 0 effLift is 0, so
                                 * the player's own angle from CameraCenter stands. */
                S32 effLift = FollowCamEyeLift * autoFactor / 100;
                FollowCamAppliedLift = effLift;
                if (effLift > 0) {
                    FollowCamLiftEye(ex, ey + effLift, ez);
                }
            } else {
                FollowCamGroundSettling = 0;
                FollowCamAppliedLift = 0;
            }
            /* (easing back onto the arm's eye, as it has just been placed, from the held start) */
            if (FollowCamLeave > 0) {
                const S32 here[3] = {ptr3d->X, ptr3d->Y, ptr3d->Z};
                const S32 armEye[3] = {CameraX, CameraY, CameraZ};
                const S32 armLooks[3] = {VueOffsetX, VueOffsetY, VueOffsetZ};
                double to[3], toLooks[3];
                for (S32 i = 0; i < 3; i++) { to[i] = armEye[i] - here[i]; toLooks[i] = armLooks[i] - here[i]; }
                FollowCamSwing(FollowCamFromEye, to, FollowCamFromLooks, toLooks,
                               FollowCamEaseW(FOLLOW_CAM_LEAVE_STEPS - FollowCamLeave + 1, FOLLOW_CAM_LEAVE_STEPS),
                               here[0], here[1], here[2], FollowCamOwnEyeAt, FollowCamOwnEyeLooks);
                FollowCamLeave--;
                /* (the arm's own angle held on its aim meanwhile: it had stood still or
                 * wandered while the hook had the view, lagged behind its aim coming out,
                 * and caught up 16 degrees at once in the middle of the ease -- the view
                 * jumped with it) */
                BetaCam = FollowCamTargetBeta;
                if (FollowCamLeave > 0)
                    FollowCamPlaceEye(FollowCamOwnEyeAt, FollowCamOwnEyeLooks);
            }
            } /* (not a mod's own eye) */
        } else {
            FollowCamOwnEye = 0;
            FollowCamOwnTurn = FALSE;
            VueDistance = FollowCamEffectiveDist;
            CameraCenter(3);
            /* Clear the ground-lift keep-alive too: otherwise a lift left
             * mid-ease when the hero enters a camera zone keeps the dirty
             * check true forever, re-rendering the terrain every idle frame. */
            FollowCamGroundSettling = 0;
        }

        FirstTime = AFF_ALL_FLIP;

        FollowCamPrevHeroX = ptr3d->X;
        FollowCamPrevHeroY = ptr3d->Y;
        FollowCamPrevHeroZ = ptr3d->Z;
        FollowCamPrevHeroBeta = ptr3d->Beta;
        FollowCamPrevAddBeta = AddBetaCam;
        FollowCamPrevBaseDist = FollowCamBaseDist;
        FollowCamPrevAlphaCam = AlphaCam;
        FollowCamEyeCut = FALSE;
    }
}

/* The eye lift has to outlive the exterior's own camera set-up. Every full redraw
 * goes through AffGrilleExt, which sets the camera again from its angles and distance
 * -- the eye back where CameraCenter(3) had left it, before the lift -- and the follow
 * camera redraws in full every frame the hero moves: the lift was worked out and then
 * dropped before anything was drawn, so an eye behind a hill (or under a race track's
 * raised road) stayed there. AffGrilleExt calls this after its own set-up. */
void FollowCamReapplyLift(void) {
    if (FollowCamera AND CubeMode == CUBE_EXTERIEUR AND !CameraZone AND !CinemaMode AND (FollowCamOwnEye > 0 OR FollowCamLeave > 0)) {
        FollowCamPlaceOwnEye();
        return;
    }
    if (!FollowCamera OR CubeMode != CUBE_EXTERIEUR OR CameraZone OR CinemaMode OR FollowCamAppliedLift <= 0)
        return;
    FollowCamLiftEye(CameraX, CameraY + FollowCamAppliedLift, CameraZ);
}

/* The two entry points below are the assignments the main loop used to make
 * into this file's statics, named for the moments that need them. */

void FollowCamResetToView(void) {
    FollowCamBaseDist = FOLLOW_CAM_INITIAL_DIST;
    FollowCamEffectiveDist = FollowCamBaseDist;
    FollowCamSyncTarget();
    FollowCamReengageDelay = 0;
    FollowCamPrevBaseDist = FollowCamBaseDist;
    FollowCamPrevAlphaCam = AlphaCam;
    /* Drop the smoothed ground lift so the recenter starts from the clean reset
     * position instead of gliding down from a stale terrain-occluder lift. */
    FollowCamEyeLift = 0;
    FollowCamAppliedLift = 0;
    FollowCamGroundSettling = 0;
    FollowCamOwnEye = 0;
    FollowCamLeave = 0;
    FollowCamOwnTurn = FALSE;
}

void FollowCamSyncTarget(void) {
    FollowCamTargetBeta = BetaCam;
}

/* A mod's own eye forgotten, and the ease back from it: a new game, or a race begun again
 * (RACEMOD.CPP). Both are held as places in the cube the hero was in, and FollowCamReapplyLift
 * puts the view back on them every frame -- a race started again from the menu after one left
 * round a loop opened on the old run's eye, the car out of the picture, until it moved
 * (the Emerald Moon, 2026-10-25). */
void FollowCamDropOwnEye(void) {
    FollowCamOwnEye = 0;
    FollowCamLeave = 0;
    FollowCamOwnTurn = FALSE;
    FollowCamEyeWho = NULL;
}

/* The cfg keys this module owns. Declared in FOLLOWCAM.H, called by
 * CONFIG_FILE.CPP. Every clamp here sits in the same file as the arithmetic
 * that needs it: FollowCamGroundClearance's ceiling, for one, exists to keep
 * (g + clearance - hy) * 100 inside S32 in the occlusion march below. */
/* Every tunable this module owns, declared once. The cfg reader, the cfg writer
 * and the console all read this rather than naming a setting, so adding one is a
 * single row and its range travels with it to whichever surface writes it.
 *
 * Row order is the cfg's key order, which is user-visible: lba2.cfg is rewritten
 * in this order, and reordering rows would reorder every player's file.
 *
 * cam_follow has no key here. FollowCamera is persisted by CONFIG_FILE.CPP at its
 * own position in the file, apart from this block. */
static const T_SETTING FollowCamSettings[] = {
    {"FollowCamReengageFrames", NULL, "cam_recenter_delay", &FollowCamReengageFrames, SETTING_CLAMP,
     FOLLOW_CAM_REENGAGE_FRAMES_DEFAULT, 0, 600, NULL, NULL, NULL, NULL,
     "Frames before auto-cam re-centers after a manual pan"},
    {"FollowCamDriftDivisor", NULL, "cam_recenter_drift", &FollowCamDriftDivisor, SETTING_CLAMP,
     FOLLOW_CAM_DRIFT_DIVISOR_DEFAULT, 1, 200, NULL, NULL, NULL, NULL,
     "Auto-cam re-center drift divisor (higher = gentler)"},
    {"FollowCamHDRecompose", NULL, "cam_hd", &FollowCamHDRecompose, SETTING_TRUTHY,
     FOLLOW_CAM_HD_RECOMPOSE_DEFAULT, 0, 1, NULL, NULL, NULL, NULL,
     "Auto cam HD recompose: pull in + steepen at tall render heights"},
    {"FollowCamHDExcessCap", NULL, "cam_hd_cap", &FollowCamHDExcessCap, SETTING_CLAMP,
     FOLLOW_CAM_HD_EXCESS_CAP_DEFAULT, 0, SETTING_MAX_NONE, NULL, NULL, NULL, NULL,
     "Cap on recompose strength so tall res converges (0 = pure linear; 70 ~= 816p)"},
    {"FollowCamHDPitchGain", NULL, "cam_hd_pitch", &FollowCamHDPitchGain, SETTING_CLAMP,
     FOLLOW_CAM_HD_PITCH_GAIN_DEFAULT, SETTING_MIN_NONE, SETTING_MAX_NONE, NULL, NULL, NULL, NULL,
     "HD recompose pitch gain (AlphaCam units per 1.0 of k-1)"},
    {"FollowCamHDDistGain", NULL, "cam_hd_dist", &FollowCamHDDistGain, SETTING_CLAMP,
     FOLLOW_CAM_HD_DIST_GAIN_DEFAULT, SETTING_MIN_NONE, SETTING_MAX_NONE, NULL, NULL, NULL, NULL,
     "HD recompose distance pull (% of boom per 1.0 of k-1)"},
    {"FollowCamHDLeanGain", NULL, "cam_hd_lean", &FollowCamHDLeanGain, SETTING_CLAMP,
     FOLLOW_CAM_HD_LEAN_GAIN_DEFAULT, SETTING_MIN_NONE, SETTING_MAX_NONE, NULL, NULL, NULL, NULL,
     "HD recompose forward-lean cut (% per 1.0 of k-1)"},
    {"FollowCamGround", NULL, "cam_ground", &FollowCamGround, SETTING_TRUTHY,
     FOLLOW_CAM_GROUND_DEFAULT, 0, 1, NULL, NULL, NULL, NULL,
     "Auto cam: smooth terrain ground-clearance lift"},
    /* The ceiling keeps (g + clearance - hy) * 100 inside S32 in the occlusion
     * march below, which is why it is stated with the setting rather than at the
     * one surface that used to check it. */
    {"FollowCamGroundClearance", NULL, "cam_ground_clear", &FollowCamGroundClearance, SETTING_CLAMP,
     FOLLOW_CAM_GROUND_CLEARANCE_DEFAULT, 0, 20000, NULL, NULL, NULL, NULL,
     "Ground clearance: keep the eye this far above the ground under it"},
    {"FollowCamManualHoldFrames", NULL, "cam_manual_hold", &FollowCamManualHoldFrames, SETTING_CLAMP,
     FOLLOW_CAM_MANUAL_HOLD_DEFAULT, 0, FOLLOW_CAM_MANUAL_HOLD_MAX, NULL, NULL, NULL, NULL,
     "Frames the pitch/distance auto-assist yields after a manual camera nudge"},
    {"FollowCamHoldAngle", NULL, "cam_hold_angle", &FollowCamHoldAngle, SETTING_TRUTHY,
     FOLLOW_CAM_HOLD_ANGLE_DEFAULT, 0, 1, NULL, NULL, NULL, NULL,
     "Hold the manual camera rotation (free-camera); off = classic auto-recenter drift"},
    /* cvar only, and it must stay that way. FollowCamera's cfg row lives in
       CONFIG_FILE.CPP's table, which reads it with the legacy AutoCameraCenter
       fallback and its own rule; giving this row a key would read the same
       global twice under two rules, and this table runs last. */
    {NULL, NULL, "cam_follow", &FollowCamera, SETTING_TRUTHY, FALSE, 0, 1, NULL, NULL, NULL, NULL,
     "Auto camera (third-person follow) enable"},
};
#define FOLLOW_CAM_SETTING_COUNT ((S32)(sizeof(FollowCamSettings) / sizeof(FollowCamSettings[0])))

void FollowCam_ReadConfig(void) {
    Settings_ReadConfig(FollowCamSettings, FOLLOW_CAM_SETTING_COUNT);
}

void FollowCam_WriteConfig(void) {
    Settings_WriteConfig(FollowCamSettings, FOLLOW_CAM_SETTING_COUNT);
}

void FollowCam_RegisterCvars(void) {
    Settings_RegisterCvars(FollowCamSettings, FOLLOW_CAM_SETTING_COUNT);
}

/* The console commands this module owns, registered from Console_RegisterAll
 * beside its cvars.
 *
 * camnudge is not here. It writes DbgCamNudge*, which EXTFUNC.CPP's manual
 * camera path consumes, so it belongs to that path and stays with the other
 * bus state until that path has a module of its own. */
/* camtrace <0|1>: log the Auto-camera angle state on every follow-cam update, so a headless run can
   show frame by frame what the camera did while the hero turned and the player orbited. */
static void cmd_camtrace(int argc, const char *argv[]) {
    if (argc >= 2)
        DbgCamTrace = atoi(argv[1]) ? 1 : 0;
    Console_Print("camtrace: %s", DbgCamTrace ? "ON" : "OFF");
}

void FollowCam_RegisterCommands(void) {
    Console_RegisterCommandEx("camtrace", cmd_camtrace, "Log the Auto-camera angle state each update",
                              "camtrace <0|1>",
                              "Logs hero Beta, AddBetaCam, BetaCam and its target, the re-engage "
                              "countdown and the manual-orbit flag on every follow-cam update.");
}
