#ifndef DECORS_H
#define DECORS_H

/*--------------------------------------------------------------------------*/
extern void FixeObjetsDecorsInvisibles(void);
/*--------------------------------------------------------------------------*/
extern int sub_qsort_decors(const void *a, const void *b);
/*--------------------------------------------------------------------------*/
extern int sub_qsort_decors_zbuf(const void *a, const void *b);
/*--------------------------------------------------------------------------*/
extern void AffichageObjetDecorsZV(void);
/*--------------------------------------------------------------------------*/
extern void AffichageObjetDecorsZBuf(void);
/*--------------------------------------------------------------------------*/
// (LBA Assembler: decors a race track moves -- RACEMOD.CPP's roulette wheel, its turning head and its rolling ball -- posed as each cube's
// decors are drawn; NULL: none)
extern void (*DecorPoseHook)(T_DECORS *list, S32 count);
/*--------------------------------------------------------------------------*/
extern S32 TestZVDecors(S32 xw, S32 yw, S32 zw, T_DECORS *objet);

#endif // DECORS_H
