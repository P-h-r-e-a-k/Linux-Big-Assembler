//──────────────────────────────────────────────────────────────────────────
#ifndef LIB_SYSTEM_HQFILE
#define LIB_SYSTEM_HQFILE

//──────────────────────────────────────────────────────────────────────────
#include <SYSTEM/FILES.H>

//──────────────────────────────────────────────────────────────────────────
#ifdef __cplusplus
extern "C" {
#endif

//──────────────────────────────────────────────────────────────────────────
extern S32 HQF_File;

// When set, asked first for every entry HQF_Init opens: the entry's bytes (as stored, not compressed; kept by the hook until the next
// call) and their size, or NULL for the file's own. LBA Assembler's maps (SOURCES/MAPPACK.CPP) serve SCENE.HQR's entries of their
// scenes this way.
extern U8 *(*HQF_Hook)(const char *name, S32 index, U32 *size);

//──────────────────────────────────────────────────────────────────────────
extern U32 HQF_Init(const char *name, S32 index);

//──────────────────────────────────────────────────────────────────────────
extern void HQF_Close();

//──────────────────────────────────────────────────────────────────────────
extern U32 HQF_LoadClose(void *ptr);

//──────────────────────────────────────────────────────────────────────────
extern S32 HQF_ResSize(const char *name, S32 index);

//──────────────────────────────────────────────────────────────────────────
extern S32 HQF_NbRes(char *name);

//──────────────────────────────────────────────────────────────────────────
#ifdef __cplusplus
}
#endif

//──────────────────────────────────────────────────────────────────────────
#endif //LIB_SYSTEM_HQFILE

//──────────────────────────────────────────────────────────────────────────
