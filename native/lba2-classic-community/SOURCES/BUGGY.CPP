/*══════════════════════════════════════════════════════════════════════════*
			 █▀▀█  █   █ █▀▀▀▀ █▀▀▀▀ █  ▄▀
			 ██▀▀█ ██  █ ██ ▀█ ██ ▀█ ██▀
                         ▀▀▀▀▀ ▀▀▀▀▀ ▀▀▀▀▀ ▀▀▀▀▀ ▀▀
 *══════════════════════════════════════════════════════════════════════════*/

#include "C_EXTERN.H"

#include "INPUT.H"
#include "AMBIANCE.H"
#include "RACEMOD.H"
#include <math.h> // sqrt: RaceSpeedOnSlope

#define MAX_SAMPLE_PITCH 11000
#define MIN_SAMPLE_PITCH2 5000
#define MAX_SAMPLE_PITCH2 8500
#define MAX_SPEED 3800
#define TEMPO_GEAR 1200 // tempo changement de vitesse

//▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀

S_BUGGY ListBuggy[MAX_BUGGYS];

S32 Gear = 0;
S32 TimerGear;

U8 NumBuggy = 0;

//▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀

//■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
// Initialise Pos et Cube Buggy Scenarique pris par Twinsen
void InitBuggy(U8 numobj, U32 flaginit) {
    S_BUGGY *ptb = &ListBuggy[0];
    T_OBJET *ptrobj = &ListObjet[numobj];

    // Pour que les objets suivent leurs tracks sans etre interrompus
    // par le buggy (tant pis, il sera poussé)
    ptrobj->Flags |= (PUSHABLE | CHECK_CODE_JEU);

    if ((flaginit == 2) // on force le repositionnement du buggy
        OR(flaginit
               AND !NumBuggy)) // premiere initialisation
                               // car le buggy vide ne peut pas etre Twinsen
    {
        ptb->Cube = NumCube; // Port-Ludo (Désert)

        ptb->X = ptrobj->Obj.X;
        ptb->Y = ptrobj->Obj.Y;
        ptb->Z = ptrobj->Obj.Z;

        ptb->Beta = ptrobj->Obj.Beta;

        InitBody(GEN_BODY_NORMAL, numobj);

        NumBuggy = (U8)(numobj | BUGGY_PRESENT);
    } else if (NumBuggy) {
        if (ListObjet[NUM_PERSO].Move != MOVE_BUGGY_MANUAL AND ListObjet[NUM_PERSO].Move != MOVE_BUGGY) {
#ifndef LBA_EDITOR
            S32 x, y;
#endif

            if (NumCube == ptb->Cube) {
                ptrobj->Obj.X = ptb->X;
                ptrobj->Obj.Y = ptb->Y;
                ptrobj->Obj.Z = ptb->Z;

                ptrobj->Obj.Beta = ptb->Beta;

                InitBody(GEN_BODY_NORMAL, numobj);
            }
#ifndef LBA_EDITOR
            else if (LoadSceneCubeXY(ptb->Cube, &x, &y)) {
                x -= CurrentCubeX;
                y -= CurrentCubeY;

                ptrobj->Obj.X = ptb->X + x * 32768;
                ptrobj->Obj.Y = ptb->Y;
                ptrobj->Obj.Z = ptb->Z + y * 32768;

                ptrobj->Obj.Beta = ptb->Beta;

                ptrobj->Flags = NO_SHADOW | OBJ_BACKGROUND | NO_CHOC | OBJ_ZBUFFER;

                InitBody(GEN_BODY_NORMAL, numobj);
            }
#endif
            else
                InitBody(NO_BODY, numobj);

            ClearRealAngle(ptrobj);
        } else
            InitBody(NO_BODY, numobj);

        NumBuggy = (U8)(numobj | BUGGY_PRESENT);
    } else
        InitBody(NO_BODY, numobj);
}

//■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

// A Appeler en début de partie

void ResetBuggy(void) {
    S_BUGGY *ptb = &ListBuggy[0];

    NumBuggy = 0;
    ptb->Cube = 0;
}

//■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

void TakeBuggy(void) {
    S32 sample;
    T_OBJET *ptrobj = &ListObjet[NUM_PERSO];
    S_BUGGY *ptb = &ListBuggy[0];

    ptb->SpeedRot = 1024;
    ptb->LastTimer = TimerRefHR;

    ObjectClear(&ptrobj->Obj);

    // Blindage au cas où le Buggy aurait bougé (en étant poussé par ex)
    ptb->X = ListObjet[NUM_BUGGY].Obj.X;
    ptb->Y = ListObjet[NUM_BUGGY].Obj.Y;
    ptb->Z = ListObjet[NUM_BUGGY].Obj.Z;
    ///////////////////////////////////////////

    ptrobj->Obj.X = ptb->X;
    ptrobj->Obj.Y = ptb->Y;
    ptrobj->Obj.Z = ptb->Z;
    ptrobj->Obj.Beta = ptb->Beta;
    ClearRealAngle(ptrobj); // pour ne pas ecraser le beta

    ptrobj->WorkFlags |= MANUAL_INTER_FRAME;
    ptrobj->Flags |= OBJ_ZBUFFER;

    SetComportement(C_BUGGY);

    // Passe Buggy Scenarique en NoBody
    InitBody(NO_BODY, NUM_BUGGY);
    // A parked buggy is drawn into the static background copy, so that copy is
    // rebuilt, or the empty car stays on screen where it stood until the camera
    // moves (a race track parks it on the start line; Polar Island on its dock).
    FirstTime = AFF_ALL_FLIP;
    if (RaceMod_Active()) {
        RaceMod_ResetGear();
    }

    if (ptrobj->SampleAlways) {
        HQ_StopOneSample(ptrobj->SampleAlways);
        ptrobj->SampleAlways = 0;
    }

#ifdef LBA_EDITOR
    sample = NUM_SAMPLE(COMMON_NUM_DEF, SAMPLE_BUGGY);
    ptrobj->NumDef = COMMON_NUM_DEF;
#else
    sample = SAMPLE_BUGGY;
#endif

    if (IsSamplePlaying(sample)) {
        HQ_StopOneSample(sample);
    }

    ptrobj->SampleVolume = 20;

    ParmSampleVolume = ptrobj->SampleVolume;

    Gear = 0;
    TimerGear = 0;

#ifdef LBA_EDITOR
    ptrobj->SampleAlways = HQ_3D_MixSample(COMMON_NUM_DEF, SAMPLE_BUGGY, 4096, 0, 0,
                                           ptrobj->Obj.X, ptrobj->Obj.Y, ptrobj->Obj.Z);
#else
    ptrobj->SampleAlways = HQ_3D_MixSample(SAMPLE_BUGGY, 4096, 0, 0,
                                           ptrobj->Obj.X, ptrobj->Obj.Y, ptrobj->Obj.Z);
#endif
}

//■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

void LeaveBuggy(U8 newcomportement) {
    S32 sample;
    T_OBJET *ptrobj = &ListObjet[NUM_PERSO];
    S_BUGGY *ptb = &ListBuggy[0];

#ifdef LBA_EDITOR
    sample = NUM_SAMPLE(COMMON_NUM_DEF, SAMPLE_BUGGY);
#else
    sample = SAMPLE_BUGGY;
#endif

    if (IsSamplePlaying(sample)) {
        HQ_StopOneSample(sample);
        ptrobj->SampleAlways = 0;
    }

    ptb->X = ptrobj->Obj.X;
    ptb->Y = ptrobj->Obj.Y;
    ptb->Z = ptrobj->Obj.Z;
    ptb->Beta = ptrobj->Obj.Beta;
    ptb->Cube = NumCube;

    ObjectClear(&ptrobj->Obj);

    ptrobj->WorkFlags &= ~(MANUAL_INTER_FRAME);
    ptrobj->Flags &= ~(OBJ_ZBUFFER);

    InitBody(GEN_BODY_TUNIQUE, NUM_PERSO);

    SetComportement(newcomportement);

    //----- Restaure buggy scénarique

    ptrobj = &ListObjet[NUM_BUGGY];

    ptrobj->Obj.X = ptb->X;
    ptrobj->Obj.Y = ptb->Y;
    ptrobj->Obj.Z = ptb->Z;
    ptrobj->Obj.Beta = ptb->Beta;

    ptrobj->CodeJeu = ListObjet[NUM_PERSO].CodeJeu;

    ClearRealAngle(ptrobj); // pour ne pas ecraser le beta

    InitBody(GEN_BODY_NORMAL, NUM_BUGGY);

    // recherche pos libre pour Twinsen autour
    PosObjetAroundAnother(NUM_BUGGY, NUM_PERSO);
}

//■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

// The speed of a race-track mod's car (RACEMOD.CPP), in place of the fixed rates below: the gear's top speed, and the
// acceleration scaled to the gear the way a gearbox does it (the pull goes with the gear ratio, the top speed against it, so a
// low gear pulls harder). Above the gear's top speed -- just after a shift down -- the car slows as it does off the throttle.
// ... on a rollercoaster of a lap (the car file's gravity=, on the raised road): the slope pulls at the car. Down a drop it gathers
// speed, past its top gear's -- that excess is lost again to drag, over a second or two, so it is carried over the next hill -- and
// up a climb the engine holds a share of the gear's top speed only. The gears change as on the level: by the speed the car would
// have there. The road's rail, while it holds the car, slows it.
static void RaceSpeedOnSlope(S_BUGGY *ptb, S32 deltatimer, S32 flagattack, double gravity, double slope, double bank) {
    const T_RACE_CAR *car = RaceMod_Car();
    const double share = RaceMod_ClimbShare(gravity, slope);
    // (a power-up's boost, or shrunk: RACEMOD.CPP RaceMod_SpeedFactor -- the gears change by the speed the car would have without it:
    // shrunk by an opponent's lightning, it never reached first gear's top and the automatic gearbox stayed in first)
    const double factor = RaceMod_SpeedFactor();
    RaceMod_UpdateGear((S32)(ptb->Speed / (share * factor)));
    const double gearTop = car->GearTop[RaceMod_Gear() - 1] * factor, engineTop = gearTop * share;
    const double dt = deltatimer / 1000.0;
    const double coast = dt * car->Coast * RaceMod_SkidCoast();
    double v = ptb->Speed;

    // (over the top gear's speed the car steers as much quicker as it goes faster: its tightest turn stays what it is; and a banked
    // bend turns it; and skidding on oil it turns round -- RACEMOD.CPP)
    const double fastest = car->GearTop[car->Gears - 1];
    const double quick = v > fastest ? v / fastest : 1, jet = RaceMod_JetSteer();
    ptb->SpeedRot = (S32)(car->Steer * (quick > jet ? quick : jet) * (1 + RACE_BANK_STEER * bank) * RaceMod_SkidSteer());
    const S32 touched = RaceMod_RailTouched();

    if (RaceMod_Held()) {
        ptb->Speed = 0;
        return;
    }

    // the slope's own pull, the way the car faces
    v -= dt * gravity * RACE_GRAVITY * slope / sqrt(1 + slope * slope);

    if ((Input & I_UP) AND !flagattack) {
        if (v <= engineTop) {
            double pull = (double)RACE_REF_TOP / gearTop;
            if (pull > 4.0)
                pull = 4.0;
            if (pull < 0.25)
                pull = 0.25;
            v += dt * car->Accel * pull * factor;
            if (v > engineTop)
                v = engineTop;
        }
    } else if ((Input & I_DOWN) AND !flagattack) {
        v -= dt * car->Brake;
        if (v < -car->Reverse)
            v = -car->Reverse;
    } else if (v > 0) {
        v -= coast;
        if (v < 0)
            v = 0;
    } else if (v < 0) {
        v += coast;
        if (v > 0)
            v = 0;
    }

    // (the golden Gazogem running: no top speed -- RACEMOD.CPP RaceMod_NoTopSpeed)
    if (v > gearTop && !RaceMod_NoTopSpeed()) {
        v -= dt * RACE_OVERSPEED_DRAG * (v - gearTop);
        // (the rail takes what the slopes gave: a car it holds loses its speed over the gear's top)
        if (touched) {
            v -= dt * RACE_RAIL_DRAG;
            if (v < gearTop)
                v = gearTop;
        }
    }
    ptb->Speed = (S32)v;
}

static void RaceSpeed(S_BUGGY *ptb, S32 deltatimer, S32 flagattack) {
    double slope, bank;
    const double gravity = RaceMod_Gravity(&slope, &bank);
    if (gravity > 0) {
        RaceSpeedOnSlope(ptb, deltatimer, flagattack, gravity, slope, bank);
        return;
    }
    const T_RACE_CAR *car = RaceMod_Car();
    // (a power-up's boost, or shrunk: RACEMOD.CPP RaceMod_SpeedFactor -- the gears change by the speed the car would have without it, as
    // on a slope)
    const double factor = RaceMod_SpeedFactor();
    RaceMod_UpdateGear((S32)(ptb->Speed / factor));
    const S32 top = (S32)(car->GearTop[RaceMod_Gear() - 1] * factor);
    // (skidding on oil the car slides on and turns round: RACEMOD.CPP)
    const S32 coast = (S32)(deltatimer * car->Coast * RaceMod_SkidCoast() / 1000);

    // (over the top gear's speed -- Gazogem fuel, a jet-pack -- it steers as much quicker as it goes faster: its tightest turn stays what
    // it is, as on a slope, RaceSpeedOnSlope; the jet-pack drives the line at twice the speed round its bends -- and steers as much
    // quicker below the top gear's speed too, RaceMod_JetSteer: through a hairpin taken at twice the line's speed, still under the top
    // speed, it turned on a circle twice as wide)
    const double fastest = car->GearTop[car->Gears - 1];
    const double quick = ptb->Speed > fastest ? ptb->Speed / fastest : 1, jet = RaceMod_JetSteer();
    ptb->SpeedRot = (S32)(car->Steer * (quick > jet ? quick : jet) * RaceMod_SkidSteer());

    // on the grid until the count-down's GO
    if (RaceMod_Held()) {
        ptb->Speed = 0;
        return;
    }

    if ((Input & I_UP) AND !flagattack) {
        if (ptb->Speed <= top) {
            double pull = (double)RACE_REF_TOP / top;
            if (pull > 4.0)
                pull = 4.0;
            if (pull < 0.25)
                pull = 0.25;
            ptb->Speed += (S32)(deltatimer * car->Accel * pull * factor / 1000.0);
            if (ptb->Speed > top)
                ptb->Speed = top;
        } else if (!RaceMod_NoTopSpeed()) {     // (the golden Gazogem running: no top speed)
            ptb->Speed -= coast;
            if (ptb->Speed < top)
                ptb->Speed = top;
        }
    } else if ((Input & I_DOWN) AND !flagattack) {
        ptb->Speed -= deltatimer * car->Brake / 1000;
        if (ptb->Speed < -car->Reverse)
            ptb->Speed = -car->Reverse;
    } else if (ptb->Speed > 0) {
        ptb->Speed -= coast;
        if (ptb->Speed < 0)
            ptb->Speed = 0;
    } else if (ptb->Speed < 0) {
        ptb->Speed += coast;
        if (ptb->Speed > 0)
            ptb->Speed = 0;
    }
}

void MoveBuggy(T_OBJET *ptrobj) {
    S_BUGGY *ptb = &ListBuggy[0];
    T_OBJ_3D *ptb3d = &ptrobj->Obj;
    S32 pitch = 0;
    S32 flagattack = FALSE;

    //	S32	speedlevel ;
    S32 speedinc;
    S32 rotlevel;
    S32 timerhr, deltatimer;

    timerhr = TimerRefHR;
    deltatimer = timerhr - ptb->LastTimer;

    // (a race-track mod's car fires as it drives: the throw doesn't stop it, nor take its steering for aiming -- RACEMOD.CPP; the user,
    // 2026-10-08: "Currently Twinsen's car can shoot small balls but only when he's stationary")
    const S32 raceFires = RaceMod_Active();
    // (one of Twinsen's laser cars fires the same way: its gun's shots are laser bolts -- FICHE.CPP, RACEMOD.CPP RaceMod_LaserThrow)
    if ((Input & I_THROW)
            AND(PtrComportement->Flags & CF_WEAPON)) {
        // est-on en mage ?
        if (TabInv[FLAG_TUNIQUE].IdObj3D == 0) {
            InitBody(GEN_BODY_TUNIQUE_TIR, NUM_PERSO);
        } else {
            InitBody(GEN_BODY_MAGE_TIR, NUM_PERSO);
        }

        InitAnim(GEN_ANIM_LANCE, ANIM_REPEAT, NUM_PERSO);

        /* control direction pendant visée */
        if (!(ptrobj->WorkFlags & ANIM_MASTER_ROT) AND !raceFires) {
            ptb3d->Beta += GetDeltaMove(&ptrobj->BoundAngle.Move);
            ptb3d->Beta &= 4095;

            ManualRealAngle(ptrobj);
        }

        //		MagicBall = TRUE ;
        LastJoyFlag = TRUE;
        flagattack = TRUE;
    } else {
        if (LastInput & I_THROW) {
            // on a fini de tirer avec le buggy,
            // on referme le capot
            InitBody(GEN_BODY_TUNIQUE, NUM_PERSO);
            InitAnim(GEN_ANIM_RIEN, ANIM_REPEAT, NUM_PERSO);
        }
    }

    if (!flagattack
            AND !(ptrobj->WorkFlags & (FALLING | ANIM_MASTER_GRAVITY))) {
        ClearRealAngle(ptrobj);

        if (LastJoyFlag
                AND(((Input & I_JOY) != LastMyJoy) OR((Input & I_FIRE) != LastMyFire))) {
            InitAnim(GEN_ANIM_RIEN, ANIM_REPEAT, NUM_PERSO);
            Pushing = FALSE;
        }

        LastJoyFlag = FALSE;

        // Pushing contient le numero de l'objet que l'on pousse
        // Donc 1000 est une valeur impossible qui sert de flag d'init
        // anim rien
        if (Pushing == 1000) {
            InitAnim(GEN_ANIM_RIEN, ANIM_REPEAT, NUM_PERSO);
            Pushing = FALSE;
        }

        if (Input & I_UP) {
            if (Pushing) {
                InitAnim(GEN_ANIM_POUSSE, ANIM_TEMPO, NUM_PERSO);
                LastJoyFlag = TRUE;
            } else {
                InitAnim(GEN_ANIM_MARCHE, ANIM_REPEAT, NUM_PERSO);

                LastJoyFlag = TRUE;
            }
        } else if (Input & I_DOWN) {
            InitAnim(GEN_ANIM_RECULE, ANIM_REPEAT, NUM_PERSO);
            LastJoyFlag = TRUE;
        }
    }

    if (!(ptrobj->WorkFlags & (FALLING | ANIM_MASTER_GRAVITY))) {
        // check speed command
        if (RaceMod_Active()) // a race-track mod's car (RACEMOD.CPP)
        {
            RaceSpeed(ptb, deltatimer, flagattack AND !raceFires);
            RaceMod_Skid(&ptb3d->Beta, &ptb->Speed);
        } else if ((Input & I_UP) // j'accelere
                   AND !flagattack) {
            ptb->Speed += deltatimer * 4;

            if (!TimerGear)
                TimerGear = TimerRefHR + TEMPO_GEAR;
            else {
                if (Gear < 0)
                    Gear = 0;

                if (TimerRefHR > TimerGear
                                     AND Gear < 2) {
                    Gear++;
                    TimerGear = TimerRefHR + TEMPO_GEAR;
                }
            }
        } else if ((Input & I_DOWN) // je freine / recule
                   AND !flagattack) {
            ptb->Speed -= deltatimer * 12;
            Gear = -1;
            TimerGear = 0;
        } else // je laisse ralentir
        {
            if (ptb->Speed > 0) {
                ptb->Speed -= deltatimer * 7;
                if (ptb->Speed < 0) {
                    ptb->Speed = 0;
                }
            }
            if (ptb->Speed < 0) {
                ptb->Speed += deltatimer * 7;
                if (ptb->Speed > 0) {
                    ptb->Speed = 0;
                }
            }
            Gear = 0;
            TimerGear = 0;
        }

        if (!RaceMod_Active()) {
            if (ptb->Speed < -2000)
                ptb->Speed = -2000;
            if (ptb->Speed > MAX_SPEED)
                ptb->Speed = MAX_SPEED;
        }

        speedinc = ptb->Speed * deltatimer / 1000;
    } else {
        speedinc = 0;
    }

    // check dir

    if (!flagattack OR raceFires) {
        if (Input & I_RIGHT) {
            ptb->BetaWheel = -300;
            if (ptb->Speed) {
                rotlevel = -ptb->SpeedRot * speedinc / ptb->Speed;
                //				ptb->BetaWheel = rotlevel*1000 ;
            } else {
                rotlevel = 0;
            }
        } else if (Input & I_LEFT) {
            ptb->BetaWheel = 300;
            if (ptb->Speed) {
                rotlevel = ptb->SpeedRot * speedinc / ptb->Speed;
                //				ptb->BetaWheel = rotlevel*1000 ;
            } else {
                rotlevel = 0;
            }
        } else {
            ptb->BetaWheel = 0;
            rotlevel = 0;
        }

        if (ptrobj->Flags & SKATING) {
            ptb->Speed = 3000;
            speedinc = ptb->Speed * deltatimer / 1000;
        } else {
            if (ptb->Speed >= 0) {
                ptb3d->Beta += rotlevel;
            } else {
                ptb3d->Beta -= rotlevel;
            }

            ptb3d->Beta &= 4095;
        }
    } else {
        ptb->BetaWheel = 0;
    }

    LastMyJoy = Input & I_JOY;
    LastMyFire = Input & I_FIRE;
    LastInput = Input;

    ptb->LastTimer = timerhr;
    ptb->SpeedInc = speedinc;

    if (RaceMod_Active()) {
        RaceMod_Lap(ptb3d->X, ptb3d->Z);
    }

    if (ptrobj->SampleAlways
            AND IsSamplePlaying(ptrobj->SampleAlways)) {
        S32 pitch;

        if (RaceMod_Active()) {
            // the engine revs up through each gear
            const T_RACE_CAR *car = RaceMod_Car();
            S32 top = ptb->Speed < 0 ? car->Reverse : car->GearTop[RaceMod_Gear() - 1];
            pitch = BoundRegleTrois(3000, MAX_SAMPLE_PITCH, top > 0 ? top : 1, abs(ptb->Speed));
        } else
        switch (Gear) {
        case -1:
            pitch = BoundRegleTrois(3000, MAX_SAMPLE_PITCH2, MAX_SPEED, abs(ptb->Speed));
            break;

        case 0:
            pitch = BoundRegleTrois(3000, MAX_SAMPLE_PITCH, MAX_SPEED, abs(ptb->Speed));
            if (pitch >= MAX_SAMPLE_PITCH)
                TimerGear = 1;
            break;

        case 1:
            pitch = BoundRegleTrois(MAX_SAMPLE_PITCH2, MIN_SAMPLE_PITCH2, TEMPO_GEAR, TimerGear - TimerRefHR);
            break;

        default:
            pitch = MAX_SAMPLE_PITCH2;
        }

        ChangePitchbendSample(ptrobj->SampleAlways, pitch);
    }
}

//■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
// Floor height under one point of the buggy (x,z cube-local), for the body's
// pitch/roll and the wheels. CalculAltitudeObjet() only knows the terrain, and
// a car carried by a decor floor (a bridge deck, a plank) would take the slope
// of whatever terrain lies underneath it. A decor top counts here by the same
// rule ReajustPosDecors() uses to put the car on it (a top between the ground
// and the car) plus the step DoCornerReajustDecors() lets it climb
// (DEMI_BRICK_Y). With no decor over the point the result is the terrain.
// Only for a race-track mod (RACEMOD.CPP); otherwise the terrain, as ever.

static S32 BuggyFloorY(T_OBJET *ptrobj, S32 x, S32 z) {
    S32 y = CalculAltitudeObjet(x, z, -1);
    if (!RaceMod_Active())
        return y;
    S32 top = ptrobj->Obj.Y + DEMI_BRICK_Y;
    S32 found = FALSE;
    T_DECORS *ptrd = ListDecors;
    S32 i;

    for (i = 0; i < NbObjDecors; i++, ptrd++) {
        if (ptrd->Body & DEC_INVISIBLE)
            continue;
        if (x < ptrd->XMin OR x > ptrd->XMax OR z < ptrd->ZMin OR z > ptrd->ZMax)
            continue;
        if (ptrd->YMax > top OR ptrd->YMax < y)
            continue;

        y = ptrd->YMax;
        found = TRUE;
    }

    // Carried by a decor, the engine holds the car level on its top for as long
    // as its ZV touches it (CheckCarryBy), so a point hanging over the edge must
    // not drop to the ground far below.
    if (!found
            AND ptrobj->CarryBy != -1
                AND(ptrobj->CarryBy & CARRY_BY_DECORS)
                    AND y < ptrobj->Obj.Y - DEMI_BRICK_Y) {
        y = ptrobj->Obj.Y;
    }

    return y;
}

//■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

void DoAnimBuggy(T_OBJET *ptrobj) {
    S32 x1, y1, z1, yw;
    S_BUGGY *ptb = &ListBuggy[0];
    T_OBJ_3D *ptb3d = &ptrobj->Obj;

    //	S32	oldx, oldy, oldz ;

    // wheels rot
    S32 c, d;

    S32 x, y, z;

    // Magouille pour ne pas ecraser les groupes dans AffOneObjet()
    ObjectSetInterFrame(ptb3d);

    //	oldx = ptb3d->X ;
    //	oldy = ptb3d->Y ;
    //	oldz = ptb3d->Z ;

    if (ptrobj->WorkFlags & (FALLING | ANIM_MASTER_GRAVITY)) {
        return;
    }

    LongRotate(0, ptb->SpeedInc * 1024, RaceMod_MoveBeta(ptb3d->Beta));   // (skidding on oil it slides on the way it was going: RACEMOD.CPP)
    Nxw = ptb3d->X + X0 / 1024;
    Nzw = ptb3d->Z + Z0 / 1024;

    // altitude ideal
    //	ny = CalculAltitudeObjet( ptb3d->X, ptb3d->Z ) ;	// ground y pour xz
    yw = BuggyFloorY(ptrobj, Nxw, Nzw); // ground y pour xz

    // test altitude #2: triangles interdits

    // roue avant gauche direction
    ptb3d->CurrentFrame[3].Beta = (S16)ptb->BetaWheel;
    // roue avant droite direction
    ptb3d->CurrentFrame[6].Beta = (S16)ptb->BetaWheel;

    // gestion 4 roues séparées.

    // +++++++++++ front right wheel

    LongRotate(-400, 400, ptb3d->Beta);
    x = Nxw + X0;
    z = Nzw + Z0;
    y = yw;

    if (x >= 0 AND x < 32768 AND z >= 0 AND z < 32768) {
        y += BuggyFloorY(ptrobj, x, z);
    }

    c = (260 * 31415) / 1000; // circonsference * 10
    d = Distance3D(ptb->Wheel[0].X, ptb->Wheel[0].Y, ptb->Wheel[0].Z, x, y, z);

    if (ptb->Speed >= 0) {
        ptb->Wheel[0].Angle += (4096 * 10 * d) / c;
    } else {
        ptb->Wheel[0].Angle -= (4096 * 10 * d) / c;
    }

    ptb->Wheel[0].X = x;
    ptb->Wheel[0].Y = y;
    ptb->Wheel[0].Z = z;

    // +++++++++++ front left wheel

    LongRotate(400, 400, ptb3d->Beta);
    x = Nxw + X0;
    z = Nzw + Z0;
    y = yw;

    if (x >= 0 AND x < 32768 AND z >= 0 AND z < 32768) {
        y += BuggyFloorY(ptrobj, x, z);
    }

    c = (260 * 31415) / 1000; // circonsference * 10
    d = Distance3D(ptb->Wheel[1].X, ptb->Wheel[1].Y, ptb->Wheel[1].Z, x, y, z);

    if (ptb->Speed >= 0) {
        ptb->Wheel[1].Angle += (4096 * 10 * d) / c;
    } else {
        ptb->Wheel[1].Angle -= (4096 * 10 * d) / c;
    }

    ptb->Wheel[1].X = x;
    ptb->Wheel[1].Y = y;
    ptb->Wheel[1].Z = z;

    // +++++++++++ back left wheel

    LongRotate(400, -350, ptb3d->Beta);
    x = Nxw + X0;
    z = Nzw + Z0;
    y = yw;

    if (x >= 0 AND x < 32768 AND z >= 0 AND z < 32768) {
        y += BuggyFloorY(ptrobj, x, z);
    }

    c = (360 * 31415) / 1000; // circonsference * 10
    d = Distance3D(ptb->Wheel[2].X, ptb->Wheel[2].Y, ptb->Wheel[2].Z, x, y, z);

    if (ptb->Speed >= 0) {
        ptb->Wheel[2].Angle += (4096 * 10 * d) / c;
    } else {
        ptb->Wheel[2].Angle -= (4096 * 10 * d) / c;
    }

    ptb->Wheel[2].X = x;
    ptb->Wheel[2].Y = y;
    ptb->Wheel[2].Z = z;

    // +++++++++++ back right wheel

    LongRotate(-400, -350, ptb3d->Beta);
    x = Nxw + X0;
    z = Nzw + Z0;
    y = yw;

    if (x >= 0 AND x < 32768 AND z >= 0 AND z < 32768) {
        y += BuggyFloorY(ptrobj, x, z);
    }

    c = (360 * 31415) / 1000; // circonsference * 10
    d = Distance3D(ptb->Wheel[3].X, ptb->Wheel[3].Y, ptb->Wheel[3].Z, x, y, z);

    if (ptb->Speed >= 0) {
        ptb->Wheel[3].Angle += (4096 * 10 * d) / c;
    } else {
        ptb->Wheel[3].Angle -= (4096 * 10 * d) / c;
    }

    ptb->Wheel[3].X = x;
    ptb->Wheel[3].Y = y;
    ptb->Wheel[3].Z = z;

    // +++++++++++ front right wheel
    ptb3d->CurrentFrame[4].Alpha = (S16)ptb->Wheel[1].Angle;
    // +++++++++++ front left wheel
    ptb3d->CurrentFrame[7].Alpha = (S16)ptb->Wheel[0].Angle;
    // +++++++++++ back left wheel
    ptb3d->CurrentFrame[11].Alpha = (S16)ptb->Wheel[2].Angle;
    // +++++++++++ back right wheel
    ptb3d->CurrentFrame[9].Alpha = (S16)ptb->Wheel[3].Angle;

    // inclinaison voiture (pitch)
    ptb3d->CurrentFrame[1].Type = 0;

    LongRotate(0, 400, ptb3d->Beta);
    x1 = X0;
    z1 = Z0;
    LongRotate(0, -400, ptb3d->Beta);

    if (Nxw + x1 >= 0 AND Nxw + x1 < 32768 AND Nzw + z1 >= 0 AND Nzw + z1 < 32768) {
        y = BuggyFloorY(ptrobj, Nxw + x1, Nzw + z1);
    } else
        y = yw;

    if (Nxw + X0 >= 0 AND Nxw + X0 < 32768 AND Nzw + Z0 >= 0 AND Nzw + Z0 < 32768) {
        y1 = BuggyFloorY(ptrobj, Nxw + X0, Nzw + Z0);
    } else
        y1 = yw;

    ptb3d->CurrentFrame[1].Alpha = (S16)(1024 - GetAngle2D(0, y, 800, y1));
    ptb->Alpha = ptb3d->CurrentFrame[1].Alpha;

    // inclinaison voiture (roll)
    LongRotate(400, 0, ptb3d->Beta);
    x1 = X0;
    z1 = Z0;
    LongRotate(-400, 0, ptb3d->Beta);

    if (Nxw + X0 >= 0 AND Nxw + X0 < 32768 AND Nzw + Z0 >= 0 AND Nzw + Z0 < 32768) {
        y = BuggyFloorY(ptrobj, Nxw + X0, Nzw + Z0);
    } else
        y = yw;

    if (Nxw + x1 >= 0 AND Nxw + x1 < 32768 AND Nzw + z1 >= 0 AND Nzw + z1 < 32768) {
        y1 = BuggyFloorY(ptrobj, Nxw + x1, Nzw + z1);
    } else
        y1 = yw;

    ptb3d->CurrentFrame[1].Gamma = (S16)GetAngle2D(y, 0, y1, 800);

    // volant
    ptb3d->CurrentFrame[12].Gamma = (S16)-ptb->BetaWheel;

    // tete twinsen
    ptb3d->CurrentFrame[14].Beta = (S16)ptb->BetaWheel;
}

//▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀
