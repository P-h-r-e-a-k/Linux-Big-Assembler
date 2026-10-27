#ifndef MAPPACK_H
#define MAPPACK_H

#include <SYSTEM/ADELINE_TYPES.H>

// LBA Assembler's maps, loaded from the game folder's MAPS folder (2026-10-15; see MAPPACK.CPP). A map's scenes are played as the game's
// scenes MAP.TXT numbers them: SCENE.HQR's entries for them come from the map's LS2s (an HQF hook), an interior's grid from its GR2 and,
// when it has them, its block library from its BL2, its bricks from its BRK and its grid fragments from its GRM; an island's scenes are
// drawn from its own ILE and OBL.

// Forget the maps read: the data root changed (the editor's renderer); the MAPS folder is read again when next needed.
void MapPack_Reset(void);

// Whether a map plays `scene`.
S32 MapPack_HasScene(S32 scene);

// The scene a cube-change zone of `scene` goes to: its Num, except in a map's scene, where Num 30000 + k is the map's k-th scene.
S32 MapPack_ZoneScene(S32 scene, S32 num);

// The island name a map's island scene is drawn from (its ILE and OBL are virtual files under that name), or NULL.
const char *MapPack_IslandFile(S32 scene);
// ... whether snow falls on it (MAP.TXT weather = snow)
S32 MapPack_Snows(S32 scene);
// ... whether its cliffs can be walked off (MAP.TXT ledges = drop)
S32 MapPack_DropLedges(S32 scene);

// A map scene's own line of dialogue `text` (MAP.TXT text = ...): the line (the game's characters, 0-ended), its size with the 0, and
// its attribute (MESSAGE.CPP FlagDial). FALSE: not a map's line -- the island's text of that number.
S32 MapPack_Text(S32 scene, S32 text, U8 **line, S32 *size, U8 *attribute);

// The maps' own characters added to the entity table (RESS.HQR 44, loaded once at boot): `*table` becomes the table with them after the
// game's (a new buffer); their bodies and animations are BODY.HQR's and ANIM.HQR's entries past the game's (the HQF hook serves them).
void MapPack_AddEntities(U8 **table);
// A map scene's actor's entity as the table has it: 30000 + k, the map's k-th character, made its number there.
S32 MapPack_Entity(S32 scene, S32 entity);

// The biggest scene record of the maps (0 if none): SCENE.HQR's entry 0 only knows the game's.
U32 MapPack_MaxSceneSize(void);

// What the maps' interiors need of the engine's grid buffers: a grid, a block library, a cube's bricks and their masks.
typedef struct {
    U32 Gri, Bll, Brick, Mask;
} T_MAP_GRID_NEEDS;

// The maps' biggest needs (InitBufferCube); LBA_BKG.HQR's header's starts tell where the game's libraries and bricks are.
void MapPack_GridNeeds(U32 bllStart, U32 brkStart, U32 maxBrk, T_MAP_GRID_NEEDS *needs);

// A map scene's grid into `bufmap`, and its own block library (if it has one; *ownLibrary says) into `tabblock`. FALSE: not a map's
// interior scene, or it doesn't fit the buffers (`room`) -- the game's grid of the scene number is loaded instead.
S32 MapPack_LoadGrid(S32 scene, U8 *bufmap, U8 *tabblock, const T_MAP_GRID_NEEDS *room, S32 *ownLibrary);

// A map scene's own bricks: the path its BRK (an HQR, brick n its entry n) opens by, how many it has and which is the forbidden one
// (-1 none). FALSE: the game's.
S32 MapPack_Bricks(S32 scene, char *path, U32 pathSize, U32 *count, S32 *forbidden);

// A map scene's own grid fragment `n` (its GRM, an HQR) into `dst`. FALSE: the game's.
S32 MapPack_LoadFragment(S32 scene, S32 n, void *dst);

#endif
