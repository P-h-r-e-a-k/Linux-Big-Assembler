#include "DEFINES.H"

/*══════════════════════════════════════════════════════════════════════════*
		      █▀▀▀▄ █▀▀▀▀ █▀▀▀▀ █▀▀▀█ █▀▀▀█ ██▀▀▀
		      ██  █ ██▀▀  ██    ██  █ ██▀█▀ ▀▀▀▀█
		      ▀▀▀▀  ▀▀▀▀▀ ▀▀▀▀▀ ▀▀▀▀▀ ▀▀  ▀ ▀▀▀▀▀
 *══════════════════════════════════════════════════════════════════════════*/
/*──────────────────────────────────────────────────────────────────────────*/

#ifdef LBA_EDITOR // Special PERSO.EXE
#define INFO_DECORS 1024
extern S32 FlagInfos;

#define SIZE_NAME 19

typedef struct
{
    char Name[SIZE_NAME + 1];
    S16 Value;
} T_VAR;

extern T_VAR ListVarGame[];
#else
extern S16 ListVarGame[];
#endif

extern S32 FlagMasque;

#define MAX_SOUS_OBJET 20
#define SIZE_BRICK_Y 256

/*══════════════════════════════════════════════════════════════════════════*/
/*══════════════════════════════════════════════════════════════════════════*/
// affiche les objets de decors

#ifdef LBA_EDITOR // uniquement PERSO
extern void ClearScreenMinMax();
extern void AdjustScreenMinMax();

S32 NumDecorsDraw;

//****************************************************************************
void DrawCubeNoBox(S32 ox, S32 oy, S32 oz,
                   S32 xw0, S32 yw0, S32 zw0,
                   S32 xw1, S32 yw1, S32 zw1) {
    S32 x0, y0, x1, y1, x2, y2, x3, y3, x4, y4, x5, y5, x6, y6, x7, y7;
    S16 coul = 11;
    S16 coul1 = 13;

    ClearScreenMinMax();
    LongWorldRotatePoint(ox, oy, oz);
    if (!LongProjectPoint(X0, Y0, Z0))
        return;
    AdjustScreenMinMax();
    Rect(Xp - 3, Yp - 3, Xp + 3, Yp + 3, coul); // point org

    CoulText(GREEN, -1);
    GraphPrintf(FALSE, Xp, Yp, "%d", NumDecorsDraw);

    LongWorldRotatePoint(xw0, yw0, zw0);
    if (!LongProjectPoint(X0, Y0, Z0))
        return;
    AdjustScreenMinMax();
    x0 = Xp;
    y0 = Yp;
    LongWorldRotatePoint(xw1, yw0, zw0);
    if (!LongProjectPoint(X0, Y0, Z0))
        return;
    AdjustScreenMinMax();
    x1 = Xp;
    y1 = Yp;
    LongWorldRotatePoint(xw1, yw0, zw1);
    if (!LongProjectPoint(X0, Y0, Z0))
        return;
    AdjustScreenMinMax();
    x2 = Xp;
    y2 = Yp;
    LongWorldRotatePoint(xw0, yw0, zw1);
    if (!LongProjectPoint(X0, Y0, Z0))
        return;
    AdjustScreenMinMax();
    x3 = Xp;
    y3 = Yp;
    LongWorldRotatePoint(xw0, yw1, zw0);
    if (!LongProjectPoint(X0, Y0, Z0))
        return;
    AdjustScreenMinMax();
    x4 = Xp;
    y4 = Yp;
    LongWorldRotatePoint(xw1, yw1, zw0);
    if (!LongProjectPoint(X0, Y0, Z0))
        return;
    AdjustScreenMinMax();
    x5 = Xp;
    y5 = Yp;
    LongWorldRotatePoint(xw1, yw1, zw1);
    if (!LongProjectPoint(X0, Y0, Z0))
        return;
    AdjustScreenMinMax();
    x6 = Xp;
    y6 = Yp;
    LongWorldRotatePoint(xw0, yw1, zw1);
    if (!LongProjectPoint(X0, Y0, Z0))
        return;
    AdjustScreenMinMax();
    x7 = Xp;
    y7 = Yp;

    Line(x0, y0, x1, y1, coul);
    Line(x1, y1, x2, y2, coul);
    Line(x2, y2, x3, y3, coul);
    Line(x3, y3, x0, y0, coul);

    Line(x0, y0, x4, y4, coul1);
    Line(x1, y1, x5, y5, coul1);
    Line(x2, y2, x6, y6, coul1);
    Line(x3, y3, x7, y7, coul1);
    Line(x4, y4, x5, y5, coul1);
    Line(x5, y5, x6, y6, coul1);
    Line(x6, y6, x7, y7, coul1);
    Line(x7, y7, x4, y4, coul1);
}

// Version ZVs absolues
void AffZVObjet(T_DECORS *ptrobj) {
    DrawCubeNoBox(ptrobj->Xworld, ptrobj->Yworld, ptrobj->Zworld,
                  ptrobj->XMin, ptrobj->YMin, ptrobj->ZMin,
                  ptrobj->XMax, ptrobj->YMax, ptrobj->ZMax);
}
#endif

/*══════════════════════════════════════════════════════════════════════════*/
void FixeObjetsDecorsInvisibles(void) {
    S32 i;
    T_DECORS *ptrobj;
    S32 numvar;

    ptrobj = ListDecors;

    for (i = 0; i < NbObjDecors; i++, ptrobj++) {
        ptrobj->Body &= ~(DEC_DRAWN | DEC_INVISIBLE);

        // Regarde si objet visible/invisible
        numvar = ptrobj->Beta >> 16;

        if (numvar) {
#ifdef LBA_EDITOR
            if (((numvar < 0) AND !ListVarGame[-numvar].Value)
                    OR((numvar >= 0) AND ListVarGame[numvar].Value))
#else
            if (((numvar < 0) AND !ListVarGame[-numvar])
                    OR((numvar >= 0) AND ListVarGame[numvar]))
#endif
                ptrobj->Body |= DEC_INVISIBLE;
        }
    }
}

/*══════════════════════════════════════════════════════════════════════════*/
// sous routine de tri temporaire

int sub_qsort_decors(const void *a, const void *b) {
    return (((T_LIST_TRI *)a)->Zrot - ((T_LIST_TRI *)b)->Zrot);
}

/*══════════════════════════════════════════════════════════════════════════*/
// sous routine de tri temporaire

int sub_qsort_decors_zbuf(const void *a, const void *b) {
    return (((T_LIST_TRI *)b)->Zrot - ((T_LIST_TRI *)a)->Zrot);
}

/*══════════════════════════════════════════════════════════════════════════*/

// Version ZVs absolues
int sub_qsort_zv_decors(const void *a, const void *b) {
    T_DECORS *ptrobja;
    T_DECORS *ptrobjb;

    S32 x0a, y0a, z0a, x1a, y1a, z1a;
    S32 x0b, y0b, z0b, x1b, y1b, z1b;

    ptrobja = ((T_LIST_TRI *)a)->PtrDec;
    ptrobjb = ((T_LIST_TRI *)b)->PtrDec;

    x0a = ptrobja->XMin;
    y0a = ptrobja->YMin;
    z0a = ptrobja->ZMin;
    x1a = ptrobja->XMax;
    y1a = ptrobja->YMax;
    z1a = ptrobja->ZMax;

    x0b = ptrobjb->XMin;
    y0b = ptrobjb->YMin;
    z0b = ptrobjb->ZMin;
    x1b = ptrobjb->XMax;
    y1b = ptrobjb->YMax;
    z1b = ptrobjb->ZMax;

    if (x0a >= x1b)
        goto pas_rec_sur_x;
    if (x1a <= x0b)
        goto pas_rec_sur_x;

    if (z0a >= z1b)
        goto pas_rec_sur_z;
    if (z1a <= z0b)
        goto pas_rec_sur_z;

    //	ordre sur y

    // affinement decision sur une des 2 faces paralelle
    if (abs(y0a - CameraY) < abs(y1a - CameraY))
        return (abs(y0b - CameraY) - abs(y0a - CameraY));
    else
        return (abs(y1b - CameraY) - abs(y1a - CameraY));

pas_rec_sur_z:

    //	ordre sur z

    if (abs(z0a - CameraZ) < abs(z1a - CameraZ))
        return (abs(z0b - CameraZ) - abs(z0a - CameraZ));
    else
        return (abs(z1b - CameraZ) - abs(z1a - CameraZ));

pas_rec_sur_x:

    if (z0a >= z1b)
        goto pas_rec_sur_z_2;
    if (z1a <= z0b)
        goto pas_rec_sur_z_2;

    if (abs(x0a - CameraZ) < abs(x1a - CameraZ))
        return (abs(x0b - CameraX) - abs(x0a - CameraX));
    else
        return (abs(x1b - CameraX) - abs(x1a - CameraX));

pas_rec_sur_z_2:

    // plus de methode dispo (obj en collision), utilise zrot

    return (((T_LIST_TRI *)b)->Zrot - ((T_LIST_TRI *)a)->Zrot);
}

/*══════════════════════════════════════════════════════════════════════════*/

#ifdef LBA_EDITOR
void AffichageObjetDecorsZV(void) {
    S32 i;
    T_LIST_TRI *ptrtri;
    T_DECORS *ptrobj;

    // affiche les zv des objets statiques
    ptrtri = ListTriExt;
    for (i = 0; i < NbObjScreen; i++, ptrtri++) {
        ptrobj = ptrtri->PtrDec;

        if (ptrobj->Body & DEC_DRAWN) {
            UnsetClip();
            NumDecorsDraw = ptrtri->Num;
            AffZVObjet(ptrobj);
        }
    }

    UnsetClip();
}
#endif

/*══════════════════════════════════════════════════════════════════════════*/

// Version masques compris

// A race track's moving decors (RACEMOD.CPP: the roulette wheel's head turned, its ball moved), posed before a cube's decors are drawn.
void (*DecorPoseHook)(T_DECORS *list, S32 count) = NULL;

// LBA Assembler's drawn-only decors are many and big -- the Emperor's palace's rooms, rock and turrets are in every cube they can be seen
// from, some 380 a cube -- and a decor drawn has every point of its body turned and projected before anything of it shows. One wholly out
// of the view -- behind the camera, off to a side, past the far plane -- is left out first, by its body's box: its points' own, worked out
// once a body (a body of several groups has its points about their pivots: never left out this way).
// (A slot a body number, as many as the island's bodies kept unpacked -- ISLE_OBJ_SLOTS: the Temple of Bu's 2,147 bodies, two to a slot
// of the 1,024 there were, had a box worked out again each frame both were in view; and emptied as an island loads -- DecorsIslandLoaded:
// its bodies are unpacked in order from the cache's start, so another island's body went to where one of the last island's had been.)
typedef struct {
    const void *Body;
    S32 Num;
    S32 Ok;
    S32 Min[3], Max[3];
} T_BODY_BOX;
#define BODY_BOX_CACHE ISLE_OBJ_SLOTS
static T_BODY_BOX BodyBoxes[BODY_BOX_CACHE];

void DecorsIslandLoaded(void) {
    memset(BodyBoxes, 0, sizeof(BodyBoxes));
}

static const T_BODY_BOX *BodyBoxOf(S32 num, const T_BODY_HEADER *body) {
    T_BODY_BOX *b = &BodyBoxes[num & (BODY_BOX_CACHE - 1)];
    if (b->Body == body && b->Num == num)
        return b;
    b->Body = body;
    b->Num = num;
    b->Ok = body->NbGroupes <= 1 && body->NbPoints > 0;
    if (b->Ok) {
        const TYPE_VT16 *p = (const TYPE_VT16 *)((const U8 *)body + body->OffPoints);
        b->Min[0] = b->Max[0] = p->X;
        b->Min[1] = b->Max[1] = p->Y;
        b->Min[2] = b->Max[2] = p->Z;
        for (S32 k = 1; k < body->NbPoints; k++) {
            const S32 v[3] = {p[k].X, p[k].Y, p[k].Z};
            for (S32 a = 0; a < 3; a++) {
                if (v[a] < b->Min[a]) b->Min[a] = v[a];
                if (v[a] > b->Max[a]) b->Max[a] = v[a];
            }
        }
    }
    return b;
}

// The decors of the cubes round the camera's are drawn too, each cube's list as its ground is (TERRAIN.CPP DrawHorizon2ZBuf; the editor's
// view: every cube of the island, EXTFUNC.CPP AffGrilleExtArea), and one of LBA Assembler's drawn-only decors has a copy in each cube it
// can be seen from -- the Emperor's palace's rooms, rock and turrets in every cube within the far plane: each was drawn as many times as
// the cubes drawn that have it, nine times a frame in the game and some thirty in the editor (2026-10-13: "Our palace track is now very
// laggy, and it's very laggy to view in our editor window too"). Such a decor is drawn once a frame: the first of its copies -- the same
// body at the same place in the island, turned the same -- is, the others are left out.
#define DRAWN_ONCE_SIZE 8192 // (a power of two)
typedef struct {
    U32 Frame;
    S32 Body, X, Y, Z, Beta;
} T_DRAWN_ONCE;
static T_DRAWN_ONCE DrawnOnce[DRAWN_ONCE_SIZE];
U32 DecorFrame = 1;                 // (a frame: ClsTerrainZBuf moves it on)
S32 DecorCubeX = 0, DecorCubeZ = 0; // (the cube whose list ListDecors is: LoadCube's)

// Whether this copy of a decor is already drawn this frame; `mark`: and if it isn't, it is now.
static int DrawnThisFrame(const T_DECORS *obj, int mark) {
    const S32 x = DecorCubeX * 32768 + obj->Xworld, y = obj->Yworld, z = DecorCubeZ * 32768 + obj->Zworld;
    const S32 body = obj->Body & 0xFFFF, beta = obj->Beta & 0xFFFF;
    const U32 h = (U32)x * 73856093u ^ (U32)y * 19349663u ^ (U32)z * 83492791u ^ (U32)body * 2654435761u ^ (U32)beta * 40503u;
    for (S32 probe = 0; probe < 64; probe++) {
        T_DRAWN_ONCE *e = &DrawnOnce[(h + probe) & (DRAWN_ONCE_SIZE - 1)];
        if (e->Frame != DecorFrame) {
            if (mark) {
                e->Frame = DecorFrame;
                e->Body = body; e->X = x; e->Y = y; e->Z = z; e->Beta = beta;
            }
            return 0;
        }
        if (e->Body == body && e->X == x && e->Y == y && e->Z == z && e->Beta == beta)
            return 1;
    }
    return 0; // (the table crowded: drawn again rather than left out)
}

// Whether a box (in the current cube's frame: its least and most x, y, z) is wholly out of the view: every corner of it outside one of
// the view's sides -- the near one `nearZ` in front of the camera, the far one at ClipZFar -- in the camera's space as BodyDisplay puts
// its points. (Also the editor's view of a whole island: a cube wholly out of it is not drawn, EXTFUNC.CPP AffGrilleExtArea.)
int BoxOutOfView(const S32 lo[3], const S32 hi[3], S32 nearZ) {
    const double fx = FRatioX, fy = (double)FRatioX * FRatioY;
    const double left = ClipWindowXMin - XCentre - 2, right = ClipWindowXMax - XCentre + 2;
    const double top = ClipWindowYMin - YCentre - 2, bottom = ClipWindowYMax - YCentre + 2;
    S32 all = 0x3F;
    for (S32 c = 0; c < 8 && all; c++) {
        LongWorldRotatePoint((c & 1) ? hi[0] : lo[0], (c & 2) ? hi[1] : lo[1], (c & 4) ? hi[2] : lo[2]);
        const double x = (double)X0 - CameraXr, y = (double)Y0 - CameraYr, z = (double)CameraZr - Z0;
        S32 out = 0;
        if (z < nearZ) out |= 1;
        if (z > ClipZFar) out |= 2;
        if (x * fx < left * z) out |= 4;
        if (x * fx > right * z) out |= 8;
        if (y * fy < top * z) out |= 16;
        if (y * fy > bottom * z) out |= 32;
        all &= out;
    }
    return all != 0;
}

// Whether a decor's body is wholly out of the view (BoxOutOfView: the near side CLOSE_NEAR in front of the camera, as near as a body is
// drawn). Turned (its beta), the box is the square round the body's turn.
static int DecorOutOfView(const T_DECORS *obj) {
    const T_BODY_HEADER *body = (const T_BODY_HEADER *)HQR_Get(HQR_Isle_Obj, obj->Body & 0xFFFF);
    if (!body)
        return 0;
    const T_BODY_BOX *b = BodyBoxOf(obj->Body & 0xFFFF, body);
    if (!b->Ok)
        return 0;
    S32 lo[3], hi[3];
    if ((obj->Beta & 0xFFF) == 0) {
        lo[0] = obj->Xworld + b->Min[0]; hi[0] = obj->Xworld + b->Max[0];
        lo[2] = obj->Zworld + b->Min[2]; hi[2] = obj->Zworld + b->Max[2];
    } else {
        const double ex = b->Max[0] > -b->Min[0] ? b->Max[0] : -b->Min[0], ez = b->Max[2] > -b->Min[2] ? b->Max[2] : -b->Min[2];
        const S32 r = (S32)ceil(sqrt(ex * ex + ez * ez));
        lo[0] = obj->Xworld - r; hi[0] = obj->Xworld + r;
        lo[2] = obj->Zworld - r; hi[2] = obj->Zworld + r;
    }
    lo[1] = obj->Yworld + b->Min[1]; hi[1] = obj->Yworld + b->Max[1];
    return BoxOutOfView(lo, hi, NearClip > CLOSE_NEAR ? CLOSE_NEAR : NearClip);
}

void AffichageObjetDecorsZBuf(void) {
    S32 i;
    T_LIST_TRI *ptrtri;
    T_DECORS *ptrobj;
    S32 clipzstart;
    S32 numvar;
    S32 zr;

    ObjPtrMap = ObjTexture; // texture obj

    if (DecorPoseHook && NbObjDecors > 0)
        DecorPoseHook(ListDecors, NbObjDecors);

    ptrtri = ListTriExt;
    ptrobj = ListDecors;

    // projection des centres
    NbObjScreen = 0;
    for (i = 0; i < NbObjDecors; i++, ptrobj++) {
        ptrobj->Body &= ~(DEC_DRAWN | DEC_INVISIBLE);

        // Regarde si objet visible/invisible
        numvar = ptrobj->Beta >> 16;

        if (numvar) {
#ifdef LBA_EDITOR
            if (((numvar < 0) AND !ListVarGame[-numvar].Value)
                    OR((numvar >= 0) AND ListVarGame[numvar].Value))
#else
            if (((numvar < 0) AND !ListVarGame[-numvar])
                    OR((numvar >= 0) AND ListVarGame[numvar]))
#endif
                ptrobj->Body |= DEC_INVISIBLE;
            //			else	ptrobj->Body &= ~(DEC_INVISIBLE) ;
        }

        // SEB le 12/04/96
        if (!(ptrobj->Body & DEC_INVISIBLE)) {
            // (a game decor is left out when any corner of its foot is past the far clip; one of LBA Assembler's -- its box's top under its
            // bottom, drawn only -- only when all of them are: the Emperor's palace's domes, 24,000 across, went from the rooftops' road with
            // their far corners)
            const int ours = ptrobj->YMax < ptrobj->YMin;
            if (ours && DrawnThisFrame(ptrobj, 0))
                continue;
            int beyond = 0;
            for (S32 c = 0; c < 4; c++) {
                LongWorldRotatePoint((c & 2) ? ptrobj->XMax : ptrobj->XMin, ptrobj->YMin, (c & 1) ? ptrobj->ZMax : ptrobj->ZMin);
                zr = CameraZr - Z0;
                if (zr > ClipZFar) beyond++;
            }
            if (ours ? beyond == 4 : beyond > 0)
                continue;
            if (ours && DecorOutOfView(ptrobj))
                continue;

            // point chaud obj pour calcul profondeur fog
            LongWorldRotatePoint(ptrobj->Xworld,
                                 ptrobj->Yworld,
                                 ptrobj->Zworld);

            S32 seen = LongProjectPoint(X0, Y0, Z0);
            const S32 zrot = CameraZr - Z0;
            // A race-track mod's piece of raised road (LBA Assembler: its box over its footprint and touching nothing, its top under its
            // bottom -- no game decor has one) is still drawn while a corner of its box is in front of the camera, though its middle has
            // gone behind it: a long piece passing under a close camera left a hole in the road. In front of the camera by CLOSE_NEAR,
            // not the near plane: a body drawn close (MASK_OBJECT_CLOSE, the palace's rooms) is drawn nearer than that, and a small piece of
            // the palace's floor right under the camera would be left out whole.
            if (!seen && ptrobj->YMax < ptrobj->YMin) {
                const S32 closeZ = NearClip > CLOSE_NEAR ? CLOSE_NEAR : NearClip;
                for (S32 c = 0; c < 4 && !seen; c++) {
                    LongWorldRotatePoint((c & 1) ? ptrobj->XMax : ptrobj->XMin, ptrobj->YMin, (c & 2) ? ptrobj->ZMax : ptrobj->ZMin);
                    seen = CameraZr - Z0 >= closeZ;
                }
            }

            if (seen && ours)
                DrawnThisFrame(ptrobj, 1); // (its other copies left out, this frame: see DrawnThisFrame)
            if (seen) {
                // Test preclipping a mettre ici
                //				if( (Xp>VIEW_EXT_X0) AND (Xp<VIEW_EXT_X1)
                //				AND (Yp>VIEW_EXT_Y0) AND (Yp<VIEW_EXT_Y1) )
                {
                    // add dans listtri
                    ptrtri->Zrot = zrot;
                    ptrtri->Num = i;
                    ptrtri->PtrDec = ptrobj;
                    ptrtri++;
                    NbObjScreen++;
                }
            }
        }
    }

    if (!NbObjScreen)
        return;

    // trie les objet en z 'front to back'
    qsort(ListTriExt, NbObjScreen, sizeof(T_LIST_TRI), sub_qsort_decors_zbuf);

    clipzstart = ClipZFar - StartZFog;

    //	Switch_Fillers( FILL_POLY_FOG_ZBUFFER );

    // affiche les objets statiques
    ptrtri = ListTriExt;
    for (i = 0; i < NbObjScreen; i++, ptrtri++) {
        UnsetClip();
        ptrobj = ptrtri->PtrDec;

        const U32 clut = PalLevel +
                         16 * BoundRegleTrois(0, 15,
                                              clipzstart,
                                              ptrtri->Zrot - StartZFog);
        SetCLUT(clut);

        // (an island with more object pages: the page this decor's body is textured from -- VAR_EXT.H)
        if (NbObjPages > 1) {
            U32 page = ((U32)ptrobj->Body >> DEC_PAGE_SHIFT) & (MAX_OBJ_PAGES - 1);
            ObjPtrMap = (page AND page < (U32)NbObjPages) ? ObjPageBuf + (page - 1) * 256 * 256 : ObjTexture;
        }

        // One of LBA Assembler's drawn-only decors is fogged by depth -- each polygon in its own band, one spanning bands pixel by pixel
        // (AFF_OBJ.H ObjFogByDepth) -- not one level for the whole body by its origin's: the Temple of Bu's chunks of wall and floor, up
        // to 6 x 6 of its columns (9,216 units) across and its full height, came out a patchwork beyond the fog's start -- a step of
        // brightness at each chunk's edge, each chunk going a sixteenth darker or lighter at once as the camera moved, several times a
        // second at speed (audit, 2026-10-18). The game's own decors keep the game's look: one level each.
        const int ours = ptrobj->YMax < ptrobj->YMin;
        ObjFogByDepth = ours;
        if (BodyDisplay(ptrobj->Xworld,
                        ptrobj->Yworld,
                        ptrobj->Zworld,
                        0,
                        (ptrobj->Beta & 0xFFFF), 0,
                        HQR_Get(HQR_Isle_Obj, ptrobj->Body & 0xFFFF))) {
            SetClip(ScreenXMin, ScreenYMin, ScreenXMax, ScreenYMax);
            if (ClipXMin <= ClipXMax
                                AND ClipYMin <= ClipYMax) {
                ptrobj->Body |= DEC_DRAWN;
            }
        }
        if (ours) {
            ObjFogByDepth = 0;
            Fill_FogPerPixel = 0;
            SetCLUT(clut); // (its polygons took their own bands: AFF_OBJ.CPP ObjFogPolygon)
        }
    }
    ObjPtrMap = ObjTexture; // (back to the first page: what is drawn next expects it)

    //	Switch_Fillers( FILL_TEXTURES_ON );

    UnsetClip();
}

/*══════════════════════════════════════════════════════════════════════════*
	  █▀▀▀▀ █▀▀▀█ █     █      █    ██▀▀▀  █    █▀▀▀█ ██▄ █ ██▀▀▀
	  ██    ██  █ ██    ██     ██   ▀▀▀▀█  ██   ██  █ ██▀██ ▀▀▀▀█
	  ▀▀▀▀▀ ▀▀▀▀▀ ▀▀▀▀▀ ▀▀▀▀▀  ▀▀   ▀▀▀▀▀  ▀▀   ▀▀▀▀▀ ▀▀  ▀ ▀▀▀▀▀
 *══════════════════════════════════════════════════════════════════════════*/
/*──────────────────────────────────────────────────────────────────────────*/

// Version ZVs absolues précalculées
// on teste par rapport a un seul point
S32 TestZVDecors(S32 xw, S32 yw, S32 zw, T_DECORS *objet) {
    if (objet->Body & DEC_INVISIBLE)
        return FALSE;

    if (objet->XMin > xw)
        return FALSE;
    if (objet->XMax < xw)
        return FALSE;

    if (objet->YMin > yw)
        return FALSE;
    if (objet->YMax < yw)
        return FALSE;

    if (objet->ZMin > zw)
        return FALSE;
    if (objet->ZMax < zw)
        return FALSE;

    return TRUE;
}
