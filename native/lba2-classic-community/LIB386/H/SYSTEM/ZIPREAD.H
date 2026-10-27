//──────────────────────────────────────────────────────────────────────────
#ifndef LIB_SYSTEM_ZIPREAD
#define LIB_SYSTEM_ZIPREAD

//──────────────────────────────────────────────────────────────────────────
// Reading zip files, and files inside them standing in for files of the game
// folder (LBA Assembler's maps, 2026-10-15: a map is a zip, `.lbamap`, of its
// own files -- MAP.TXT, its scenes' LS2s, its grid's GR2, a block library's
// BL2 and its bricks' BRK, an island's ILE and OBL -- that the engine loads
// from the game folder's MAPS folder: SOURCES/MAPPACK.CPP).
//
// Members are stored (method 0) or deflated (8): a map written by LBA Assembler
// stores them, and one a zip program has packed again deflates them. Zip64,
// encrypted and multi-disk archives are not read.
//
// A virtual file is a member read through OpenRead/Read/Seek/Close (FILES.CPP
// asks here before the file system), under the path the engine would open the
// file it stands for by.
//──────────────────────────────────────────────────────────────────────────
#include <SYSTEM/ADELINE_TYPES.H>
#include <SYSTEM/LIMITS.H>

//──────────────────────────────────────────────────────────────────────────
#ifdef __cplusplus
extern "C" {
#endif

#define ZIP_MAX_NAME 128

typedef struct {
    char Name[ZIP_MAX_NAME]; // its name in the zip ('/' between folders)
    U32 DataOffset;          // where its data starts in the zip file
    U32 CompressedSize;
    U32 Size;                // uncompressed
    U16 Method;              // 0 stored, 8 deflated
} T_ZIP_MEMBER;

//──────────────────────────────────────────────────────────────────────────
// The members of a zip file, from its central directory: how many there are
// (the first `max` written to `members`), or -1 if it isn't a zip this reads.
S32 Zip_ReadDirectory(const char *zip, T_ZIP_MEMBER *members, S32 max);

// The member named `name` (case not counted), or -1.
S32 Zip_FindMember(const T_ZIP_MEMBER *members, S32 count, const char *name);

// A member's bytes, uncompressed, in a buffer `extra` bytes longer than it
// (malloc'ed: free() it); NULL if it can't be read.
U8 *Zip_LoadMember(const char *zip, const T_ZIP_MEMBER *member, U32 extra);

// Inflates a raw deflate stream (RFC 1951) into `out`; 0 if it filled `out`
// exactly, a negative number if the stream is damaged or not that long.
S32 Zip_Inflate(U8 *out, U32 outlen, const U8 *in, U32 inlen);

//──────────────────────────────────────────────────────────────────────────
// Virtual files.
void VFile_Clear(void);
// `path` opens `member` of `zip` from now on (an earlier one under the same
// path is replaced). FALSE when the table is full.
S32 VFile_Add(const char *path, const char *zip, const T_ZIP_MEMBER *member);
S32 VFile_Exists(const char *path);

bool VFile_IsHandle(S32 handle);
S32 VFile_OpenRead(const char *path); // 0: no such virtual file
S32 VFile_Read(S32 handle, void *buffer, U32 maxBytes);
S32 VFile_Seek(S32 handle, S32 offset, S32 origin);
S32 VFile_Close(S32 handle);

//──────────────────────────────────────────────────────────────────────────
#ifdef __cplusplus
}
#endif

//──────────────────────────────────────────────────────────────────────────
#endif // LIB_SYSTEM_ZIPREAD

//──────────────────────────────────────────────────────────────────────────
