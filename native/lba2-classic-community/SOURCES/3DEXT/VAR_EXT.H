/*══════════════════════════════════════════════════════════════════════════*
             █   █ █▀▀▀█ █▀▀▀█       █▀▀▀▀ ▀▄ ▄▀ ▀▀█▀▀       █   █
             ██ ▄▀ ██▀▀█ ██▀█▀       ██▀▀   ▄▀▄    ██   ▄▄   ██▀▀█
             ▀▀▀   ▀▀  ▀ ▀▀  ▀ ▀▀▀▀▀ ▀▀▀▀▀ ▀   ▀   ▀▀   ▀▀   ▀▀  ▀
 *══════════════════════════════════════════════════════════════════════════*/

#ifndef LBA_GAME
extern S16 FillFeuille;
#endif

extern S32 ClipZFar;
extern S32 StartZFog;

//camera

extern S32 CamPosX;
extern S32 CamPosY;
extern S32 CamPosZ;

extern S32 AlphaCam;
extern S32 BetaCam;
extern S32 GammaCam;
extern S32 ChampX;
extern S32 ChampZ;

extern S32 VueDistance;
extern S32 VueOffsetX, VueOffsetY, VueOffsetZ;

extern S32 WorldLightAlpha;
extern S32 WorldLightBeta;

// terrain

//extern	S32		ListCubeInfos[] ;
extern S32 *ListCubeInfos;

extern U8 *IsleMapIndex;
extern T_HALF_POLY *MapPolyGround;

//extern	char		IsleMapIndex[16][16] ;
//extern	T_HALF_POLY	MapPolyGround[64][64][2] ;
extern T_HALF_TEX *ListTexDef;

extern S16 *MapSommetY;
extern U8 *MapIntensity;
extern T_SOMMET_ROT SommetRot[65][65];
extern U8 SommetAffichable[65][65];

/*
extern	S16		MapSommetY[65][65] ;
extern	U8		MapIntensity[65][65] ;
extern	T_SOMMET_ROT	SommetRot[65][65] ;
*/

extern S32 PalLevel;

#ifdef __cplusplus
extern "C" S32 FogCoul;
#else
extern S32 FogCoul;
#endif

extern U8 *SkySeaTexture;
extern U8 *ObjTexture;
extern U8 *GroundTexture;
/* More texture pages for an island that has them (POLAR.ILE, the LBA Assembler's port of LBA1's Polar Island; a retail island has
   none): in the .ILE's entries after its cubes' records, a header (ISLE_PAGES_MAGIC, the ground pages past GroundTexture, the object
   pages past ObjTexture), then those ground pages, then those object pages, 256 x 256 each (LOADISLE.CPP LoadIsland).
   - ground: a ground triangle's IndexTex is its page in the top bits (>> GROUND_PAGE_SHIFT) and its texture definition in the cube's
     list in the rest (TERRAIN.CPP GroundTexDef);
   - objects: a decor's page is in its Body's bits DEC_PAGE_SHIFT.. (4 bits; the body number is the low 16, DEC_INVISIBLE and DEC_DRAWN
     the next two), the page all its body's textures are on (DECORS.CPP). */
#define ISLE_PAGES_MAGIC 0x45474150 // 'PAGE'
#define MAX_GROUND_PAGES 8
#define MAX_OBJ_PAGES 16
#define GROUND_PAGE_SHIFT 10
#define DEC_PAGE_SHIFT 18
extern U8 *GroundPageBuf;   // ground pages 1.. (page 0 is GroundTexture)
extern S32 NbGroundPages;
extern U8 *ObjPageBuf;      // object pages 1.. (page 0 is ObjTexture)
extern S32 NbObjPages;

extern Struc_Point Tab_Points[];
//extern	Struc_Point	MemoTab_Points[] ;

// decors

extern S32 NbObjDecors;
extern T_DECORS *ListDecors;
//extern	T_HQR_HEADER	*HQR_Isle ;
extern T_HQR_HEADER *HQR_Isle_Obj;
extern T_HQR_HEADER *HQR_CubeInfos;
extern T_HQR_HEADER *HQR_ListDecors;
extern T_HQR_HEADER *HQR_MapPGround;
extern T_HQR_HEADER *HQR_ListTexDef;
extern T_HQR_HEADER *HQR_MapSommetY;
extern T_HQR_HEADER *HQR_MapIntensity;

extern S32 NbObjScreen;
extern T_LIST_TRI *ListTriExt;

// collision avec le terrain
extern S32 IndexMapPolyTested;
extern U16 CubeBitField;

//--------------------------------------------------------------------
// Animation de l'eau

#define SIZE_BUFFER_POLY_SEA 100000L
#define MAX_POLY_SEA (SIZE_BUFFER_POLY_SEA / sizeof(S_ANI_POLY))

extern S32 TimerAniPoly;
extern S32 CntAniPoly8; // anim 0-7
extern S32 CntAniPoly6; // anim 0-5

extern S_ANI_POLY *PtrPolySea; // buffer stockage
extern S32 MaxPolySea;

extern S_ANI_POLY *PtrAniPoly; // pointeur poly en cours
extern S32 NbAniPoly;

extern S32 OffsetAniPoly1;
extern S32 OffsetAniPoly2;
extern S32 OffsetAniPoly3;
extern S32 OffsetAniPoly4;
