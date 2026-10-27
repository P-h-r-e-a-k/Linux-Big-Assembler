#ifndef RAIN_H
#define RAIN_H

typedef struct
{
    S32 XRain;
    S32 YRain;
    S32 ZRain;
    S32 Timer;
} T_RAIN;

/*--------------------------------------------------------------------------*/
extern void InitOneRain(T_RAIN *pt);
/*--------------------------------------------------------------------------*/
extern void InitRain(void);
/*--------------------------------------------------------------------------*/
extern void GereRain(void);
/*--------------------------------------------------------------------------*/
extern void ClearImpactRain(void);
/*--------------------------------------------------------------------------*/
extern void AffRain(void);
/*--------------------------------------------------------------------------*/

// Falling snow, on an island whose map says so (SNOW.CPP): whether it falls here (and its flakes set going when it starts to), and they
// moved and drawn as the rain's are
extern S32 SnowFalls(void);
extern void InitSnow(void);
extern void GereSnow(void);
extern void AffSnow(void);
/*--------------------------------------------------------------------------*/

extern S32 OldRealTime;
extern S32 DeltaRain;
extern U32 RealTime;
extern S32 LastTimer;

#endif // RAIN_H
