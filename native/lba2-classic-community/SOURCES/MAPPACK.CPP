// LBA Assembler's maps, loaded from the game folder's MAPS folder (2026-10-15).
//
// The user: "The GR2 file contains the raw structural data for a map's physical geometry and tile architecture ... needs two external
// componets the .brk file that hold the individual graphic blocks/tiles. and the .bl2 file the layout library ... LS2 file: placement of
// twinsen, enemies, and characters ... I don't know if when loading a map raw it can also reference a brick package but that's what i'd
// like to do ... Maybe just a simple txt tile inside the zip that can be manually added later? ... save them as a zip but a zip with a
// custom extension so it can keep .lbamap but it is actually a zip file and can just be opened with a file extractor."
//
// A map is a zip, `<name>.lbamap`, of:
//   MAP.TXT     what it is, "key = value" lines ('#' a comment):
//                 game = lba2              (an LBA1 map is not this engine's: left alone)
//                 kind = interior | island
//                 name = ..., description = ...
//                 scene <number> = <file>.LS2   one line for each of its scenes, in its own order: the game's scene <number> is played
//                                               from it. A cube-change zone in a map's scene going to 30000 + k goes to its k-th.
//                 grid = <file>.GR2         an interior's grid (LBA_BKG.HQR's grid entry: its 34-byte header -- library, fragments, used
//                                           blocks -- and its columns)
//                 library = <file>.BL2      its own block library (else: the game's its grid's header names)
//                 bricks = <file>.BRK       its own bricks, an HQR: brick n is its entry n, as the library's brick numbers count them
//                                           (else: the game's)
//                 fragments = <file>.GRM    its own grid fragments, an HQR (else: the game's its grid's header names)
//                 forbidden brick = <n>     of its own bricks, the one never drawn (LBA_BKG.HQR's header's ForbidenBrick)
//                 ile = <file>.ILE, obl = <file>.OBL    an island's: its scenes are drawn from them
//                 weather = snow            an island's: snow falls on it (SNOW.CPP)
//                 ledges = drop             an island's: its cliffs -- ground too steep to walk on -- can be walked or ridden off as LBA1's
//                                           ledges can, the one going over falling (EXTFUNC.CPP DoCornerReajustExtPt); without it they are
//                                           walls whichever way, as the game's islands' (but in a jump)
//                 characters = <file>.TXT   its own characters (LBA Assembler's Character maker; the editor's list of them), with
//                 character bodies = <file>.HQR, character anims = <file>.HQR, character entities = <file>.HQR
//                                           their bodies, animations and entity records (RESS.HQR 44's kind: its bodies' and animations'
//                                           numbers its own HQRs' entries), stored HQRs. At boot the entities go after the game's (and
//                                           the maps' before it), their bodies and animations numbered after BODY.HQR's and ANIM.HQR's
//                                           (MapPack_AddEntities; the HQF hook serves them); a scene's actor of entity 30000 + k is of
//                                           the map's k-th (MapPack_Entity)
//                 text = <file>.TXT         its own lines of dialogue (UTF-8): "<number> = <line>" a line, or "<number>:<attribute> =
//                                           <line>" (the attribute MESSAGE.CPP's FlagDial: 1 an ordinary line, the default); its scenes'
//                                           messages of those numbers are its lines, not the island's (GetText, MapPack_Text), and
//                                           are shown, not spoken. (An island map installed into the game's files has them in
//                                           <file>_TEXT.TXT beside its <file>.ILE, ISLANDS.TXT's.)
//   and those files. Members are stored or deflated (ZIPREAD.CPP).
//
// A scene number another map took first is left to it (maps are read in their file names' order). A map's scene numbered as one of the
// game's is played instead of the game's.

#include "C_EXTERN.H"
#include "DIRECTORIES.H"
#include "MAPPACK.H"
#include "HQR_NAMES.H"

#include <SYSTEM/HQFILE.H>
#include <SYSTEM/HQRLOAD.H>
#include <SYSTEM/LOG.H>
#include <SYSTEM/ZIPREAD.H>

#include <dirent.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define MAPS_FOLDER "MAPS"
#define MAP_EXTENSION ".lbamap"
#define MAP_MANIFEST "MAP.TXT"
#define MAX_MAP_PACKS 64
#define MAX_PACK_MEMBERS 1100 // an island of 16 x 16 cubes has 256 scenes
#define MAX_PACK_SCENES 256
#define MAP_LOCAL_SCENE 30000
#define MAP_LOCAL_ENTITY 30000
#define MAX_PACK_BRICKS 20000 // GRILLE.CPP MAX_BRICK_GAME: LoadUsedBrick's renumbering table

// A map's lines of dialogue: number, attribute (MESSAGE.CPP FlagDial), the line (the game's characters, 0-ended, in Data)
typedef struct {
    S32 Read, Nb;
    S32 *Ids;
    U8 *Attributes;
    char **Lines;
    char *Data;
} T_MAP_TEXTS;

static void FreeTexts(T_MAP_TEXTS *t) {
    free(t->Ids);
    free(t->Attributes);
    free(t->Lines);
    free(t->Data);
    memset(t, 0, sizeof(*t));
}

typedef struct {
    char Zip[ADELINE_MAX_PATH];
    char File[64]; // the zip's file name
    T_ZIP_MEMBER *Members;
    S32 NbMembers;
    S32 NbScenes;
    S32 Scenes[MAX_PACK_SCENES];      // the game scene numbers of its scenes, in its own order (-1: another map's)
    S32 SceneMember[MAX_PACK_SCENES]; // their LS2s
    S32 Grid, Library, Bricks, Fragments, Ile, Obl; // members, -1 none
    U32 NbBricks;
    S32 Forbidden;
    S32 Snow; // weather = snow
    S32 Ledges; // ledges = drop
    S32 CharBodies, CharAnims, CharEntities; // character bodies / anims / entities = (members, -1 none) ...
    U8 *BodiesHqr, *AnimsHqr;                // ... the two first in memory once the entities are added (stored HQRs)
    U32 BodiesSize, AnimsSize;
    S32 EntityBase, NbEntities;              // ... where the entities went in the table (-1: not added), how many
    S32 BodyBase, NbBodies, AnimBase, NbAnims; // ... and where their bodies and animations are numbered from
    S32 Text;          // text = (a member, -1 none) ...
    T_MAP_TEXTS Texts; // ... its lines, read when first asked for
    char Island[ADELINE_MAX_PATH]; // the island name its ILE and OBL are virtual files under
    S32 NeedsDone;
    T_MAP_GRID_NEEDS Needs;
} T_MAP_PACK;

static T_MAP_PACK *Packs = NULL;
static S32 NbPacks = 0;
static S32 Scanned = FALSE;
static U8 *HookEntry = NULL; // the last scene record served
// LBA_BKG.HQR's header's starts (MapPack_GridNeeds)
static U32 BkgBllStart = 0, BkgBrkStart = 0, BkgMaxBrk = 0;
static S32 BkgKnown = FALSE;

static U8 *SceneHook(const char *name, S32 index, U32 *size);
extern const char *SceneIslandFileName(S32 scene); // EXTFUNC.CPP: the island file ISLANDS.TXT draws a scene on, or NULL

// (installed with the program: HQF_Init asks it for every entry)
static struct HookInstaller {
    HookInstaller() { HQF_Hook = SceneHook; }
} TheHookInstaller;

//──────────────────────────────────────────────────────────────────────────
static void FreePacks(void) {
    for (S32 i = 0; i < NbPacks; i++) {
        free(Packs[i].Members);
        FreeTexts(&Packs[i].Texts);
        free(Packs[i].BodiesHqr);
        free(Packs[i].AnimsHqr);
    }
    free(Packs);
    Packs = NULL;
    NbPacks = 0;
    free(HookEntry);
    HookEntry = NULL;
    VFile_Clear();
}

void MapPack_Reset(void) {
    FreePacks();
    Scanned = FALSE;
}

static char *Trim(char *s) {
    while (*s == ' ' || *s == '\t') s++;
    char *e = s + strlen(s);
    while (e > s && (e[-1] == ' ' || e[-1] == '\t' || e[-1] == '\r' || e[-1] == '\n')) *--e = '\0';
    return s;
}

static S32 SceneTaken(S32 scene) {
    for (S32 p = 0; p < NbPacks; p++)
        for (S32 k = 0; k < Packs[p].NbScenes; k++)
            if (Packs[p].Scenes[k] == scene) return TRUE;
    return FALSE;
}

// A member MAP.TXT names ("game" or nothing: none, the game's own).
static S32 Member(T_MAP_PACK *p, const char *key, const char *value) {
    if (!value[0] || !strcasecmp(value, "game")) return -1;
    S32 m = Zip_FindMember(p->Members, p->NbMembers, value);
    if (m < 0) Log_Warn("MAPS: %s: its %s %s isn't in it", p->File, key, value);
    return m;
}

// The path a member of a map opens by (OpenRead: a virtual file).
static void MemberPath(const T_MAP_PACK *p, S32 member, char *path, U32 size) {
    snprintf(path, size, "%s|%s", p->Zip, p->Members[member].Name);
}

static void ReadPack(const char *zip, const char *file) {
    T_MAP_PACK *p = &Packs[NbPacks];
    memset(p, 0, sizeof(*p));
    strncpy(p->Zip, zip, ADELINE_MAX_PATH - 1);
    strncpy(p->File, file, sizeof(p->File) - 1);
    p->Grid = p->Library = p->Bricks = p->Fragments = p->Ile = p->Obl = p->Text = -1;
    p->CharBodies = p->CharAnims = p->CharEntities = p->EntityBase = -1;
    p->Forbidden = -1;

    p->Members = (T_ZIP_MEMBER *)malloc(sizeof(T_ZIP_MEMBER) * MAX_PACK_MEMBERS);
    if (!p->Members) return;
    p->NbMembers = Zip_ReadDirectory(zip, p->Members, MAX_PACK_MEMBERS);
    if (p->NbMembers < 0) {
        Log_Warn("MAPS: %s isn't a zip this reads", file);
        free(p->Members);
        return;
    }
    if (p->NbMembers > MAX_PACK_MEMBERS) {
        Log_Warn("MAPS: %s has %d files: the first %d are read", file, (int)p->NbMembers, MAX_PACK_MEMBERS);
        p->NbMembers = MAX_PACK_MEMBERS;
    }
    S32 txt = Zip_FindMember(p->Members, p->NbMembers, MAP_MANIFEST);
    U8 *text = txt < 0 ? NULL : Zip_LoadMember(zip, &p->Members[txt], 1);
    if (!text) {
        Log_Warn("MAPS: %s has no %s (a map an older LBA Assembler saved: open it and save it again)", file, MAP_MANIFEST);
        free(p->Members);
        return;
    }

    S32 lba2 = TRUE;
    char *line = (char *)text;
    while (line && *line) {
        char *next = strchr(line, '\n');
        if (next) *next++ = '\0';
        char *l = Trim(line);
        char *eq = strchr(l, '=');
        line = next;
        if (l[0] == '#' || !eq) continue;
        *eq = '\0';
        char *key = Trim(l), *value = Trim(eq + 1);
        if (!strncasecmp(key, "scene ", 6)) {
            S32 number = atoi(key + 6);
            S32 m = Member(p, "scene", value);
            if (p->NbScenes >= MAX_PACK_SCENES || m < 0) continue;
            if (number < 0 || number >= MAX_GRID_SCENES) {
                Log_Warn("MAPS: %s: scene %d is past the engine's %d scenes", file, (int)number, MAX_GRID_SCENES);
                number = -1;
            } else if (SceneTaken(number)) {
                Log_Warn("MAPS: %s: scene %d is another map's (change its number in its MAP.TXT)", file, (int)number);
                number = -1;
            }
            p->Scenes[p->NbScenes] = number;
            p->SceneMember[p->NbScenes] = m;
            p->NbScenes++;
        } else if (!strcasecmp(key, "game")) {
            lba2 = !strcasecmp(value, "lba2");
        } else if (!strcasecmp(key, "grid")) {
            p->Grid = Member(p, key, value);
        } else if (!strcasecmp(key, "library")) {
            p->Library = Member(p, key, value);
        } else if (!strcasecmp(key, "bricks")) {
            p->Bricks = Member(p, key, value);
        } else if (!strcasecmp(key, "fragments")) {
            p->Fragments = Member(p, key, value);
        } else if (!strcasecmp(key, "forbidden brick")) {
            p->Forbidden = atoi(value);
        } else if (!strcasecmp(key, "ile")) {
            p->Ile = Member(p, key, value);
        } else if (!strcasecmp(key, "obl")) {
            p->Obl = Member(p, key, value);
        } else if (!strcasecmp(key, "weather")) {
            p->Snow = !strcasecmp(value, "snow");
        } else if (!strcasecmp(key, "ledges")) {
            p->Ledges = !strcasecmp(value, "drop");
        } else if (!strcasecmp(key, "text")) {
            p->Text = Member(p, key, value);
        } else if (!strcasecmp(key, "character bodies")) {
            p->CharBodies = Member(p, key, value);
        } else if (!strcasecmp(key, "character anims")) {
            p->CharAnims = Member(p, key, value);
        } else if (!strcasecmp(key, "character entities")) {
            p->CharEntities = Member(p, key, value);
        }
    }
    free(text);
    if (!lba2 || !p->NbScenes) {
        if (lba2) Log_Warn("MAPS: %s names no scene", file);
        free(p->Members);
        return;
    }

    char path[ADELINE_MAX_PATH];
    // its own bricks: how many (its HQR's table, its last word the file's size)
    if (p->Bricks >= 0) {
        U32 first = 0;
        MemberPath(p, p->Bricks, path, sizeof(path));
        VFile_Add(path, zip, &p->Members[p->Bricks]);
        S32 h = OpenRead(path);
        if (h) {
            Read(h, &first, 4);
            Close(h);
        }
        p->NbBricks = first >= 8 ? first / 4 - 1 : 0;
        if (!p->NbBricks || p->NbBricks > MAX_PACK_BRICKS) {
            Log_Warn("MAPS: %s: its bricks %s can't be read (%u): the game's are used", file, p->Members[p->Bricks].Name, (unsigned)p->NbBricks);
            p->Bricks = -1;
        }
    }
    if (p->Fragments >= 0) {
        MemberPath(p, p->Fragments, path, sizeof(path));
        VFile_Add(path, zip, &p->Members[p->Fragments]);
    }
    // an island: its ILE and OBL under a name no file of the game folder can have ('|')
    if (p->Ile >= 0 && p->Obl >= 0) {
        char name[ADELINE_MAX_PATH];
        const char *stem = p->Members[p->Ile].Name;
        if (strrchr(stem, '/')) stem = strrchr(stem, '/') + 1;
        snprintf(p->Island, sizeof(p->Island), "%s|%s", file, stem);
        if (strrchr(p->Island, '.')) *strrchr(p->Island, '.') = '\0';
        snprintf(name, sizeof(name), "%s.ile", p->Island);
        GetResPath(path, ADELINE_MAX_PATH, name);
        VFile_Add(path, zip, &p->Members[p->Ile]);
        snprintf(name, sizeof(name), "%s.obl", p->Island);
        GetResPath(path, ADELINE_MAX_PATH, name);
        VFile_Add(path, zip, &p->Members[p->Obl]);
    } else if (p->Ile >= 0 || p->Obl >= 0) {
        Log_Warn("MAPS: %s: an island needs its ILE and its OBL", file);
    }

    S32 lo = 0x7FFFFFFF, hi = -1;
    for (S32 k = 0; k < p->NbScenes; k++)
        if (p->Scenes[k] >= 0) {
            if (p->Scenes[k] < lo) lo = p->Scenes[k];
            if (p->Scenes[k] > hi) hi = p->Scenes[k];
        }
    Log_Info("MAPS: %s: %d scene%s (%d to %d)%s%s%s", file, (int)p->NbScenes, p->NbScenes > 1 ? "s" : "", (int)lo, (int)hi,
             p->Island[0] ? ", an island" : p->Grid >= 0 ? ", an interior" : "", p->Library >= 0 ? ", its own block library" : "",
             p->Bricks >= 0 ? ", its own bricks" : "");
    NbPacks++;
}

static int CompareNames(const void *a, const void *b) { return strcasecmp((const char *)a, (const char *)b); }

static void Scan(void) {
    char dir[ADELINE_MAX_PATH], zip[ADELINE_MAX_PATH];
    Scanned = TRUE;
    FreePacks();
    GetResPath(dir, ADELINE_MAX_PATH, MAPS_FOLDER);
    DIR *d = opendir(dir);
    if (!d) return;
    // (in their names' order: which of two maps numbering a scene the same gets it doesn't change from one run to the next)
    static char names[MAX_MAP_PACKS][64];
    S32 n = 0;
    struct dirent *e;
    while ((e = readdir(d)) != NULL) {
        size_t len = strlen(e->d_name);
        if (len < strlen(MAP_EXTENSION) || len >= 64 || strcasecmp(e->d_name + len - strlen(MAP_EXTENSION), MAP_EXTENSION)) continue;
        if (n == MAX_MAP_PACKS) {
            Log_Warn("MAPS: more than %d maps: the rest are left", MAX_MAP_PACKS);
            break;
        }
        strcpy(names[n++], e->d_name);
    }
    closedir(d);
    if (!n) return;
    qsort(names, n, sizeof(names[0]), CompareNames);
    Packs = (T_MAP_PACK *)calloc(n, sizeof(T_MAP_PACK));
    if (!Packs) return;
    for (S32 i = 0; i < n; i++) {
        snprintf(zip, sizeof(zip), "%s%c%s", dir, '/', names[i]);
        ReadPack(zip, names[i]);
    }
}

static void Ensure(void) {
    if (!Scanned) Scan();
}

static T_MAP_PACK *PackOf(S32 scene, S32 *k) {
    Ensure();
    if (scene < 0) return NULL;
    for (S32 p = 0; p < NbPacks; p++)
        for (S32 i = 0; i < Packs[p].NbScenes; i++)
            if (Packs[p].Scenes[i] == scene) {
                if (k) *k = i;
                return &Packs[p];
            }
    return NULL;
}

//──────────────────────────────────────────────────────────────────────────
// A map's own characters (character entities / bodies / anims =).

// Entry `index` of an HQR in memory whose entries are stored (as the editor writes them): its bytes and their count, or NULL.
static U8 *StoredEntry(U8 *hqr, U32 size, S32 index, U32 *len) {
    if (!hqr || size < 8) return NULL;
    const U32 first = *(U32 *)hqr;
    const S32 count = (S32)(first / 4) - 1;
    if (index < 0 || index >= count || first > size) return NULL;
    const U32 at = ((U32 *)hqr)[index];
    if (!at || at + 10 > size) return NULL;
    const U32 n = *(U32 *)(hqr + at);
    if (*(U16 *)(hqr + at + 8) != 0 || at + 10 + n > size) {
        Log_Warn("MAPS: a character entry isn't stored (the map's HQR is packed: save the map again)");
        return NULL;
    }
    if (len) *len = n;
    return hqr + at + 10;
}

static S32 StoredCount(const U8 *hqr, U32 size) { return hqr && size >= 4 ? (S32)(*(const U32 *)hqr / 4) - 1 : 0; }

void MapPack_AddEntities(U8 **table) {
    Ensure();
    if (!table || !*table) return;
    char path[ADELINE_MAX_PATH];
    GetResPath(path, ADELINE_MAX_PATH, BODY_HQR_NAME);
    S32 bodies = HQF_NbRes(path);
    GetResPath(path, ADELINE_MAX_PATH, ANIM_HQR_NAME);
    S32 anims = HQF_NbRes(path);
    U32 *offs = (U32 *)*table;
    const S32 count = (S32)(offs[0] / 4) - 1;
    const U32 end = offs[count];
    // (the records to add, in order: each pack's entities, renumbered)
    S32 added = 0;
    U32 addedBytes = 0;
    for (S32 i = 0; i < NbPacks; i++) {
        T_MAP_PACK *p = &Packs[i];
        if (p->CharEntities < 0 || p->CharBodies < 0 || p->CharAnims < 0) continue;
        U8 *ents = Zip_LoadMember(p->Zip, &p->Members[p->CharEntities], 0);
        p->BodiesHqr = Zip_LoadMember(p->Zip, &p->Members[p->CharBodies], 0);
        p->AnimsHqr = Zip_LoadMember(p->Zip, &p->Members[p->CharAnims], 0);
        p->BodiesSize = p->Members[p->CharBodies].Size;
        p->AnimsSize = p->Members[p->CharAnims].Size;
        if (!ents || !p->BodiesHqr || !p->AnimsHqr) {
            Log_Warn("MAPS: %s: its characters can't be read", p->File);
            free(ents);
            continue;
        }
        const U32 entsSize = p->Members[p->CharEntities].Size;
        p->NbEntities = StoredCount(ents, entsSize);
        p->NbBodies = StoredCount(p->BodiesHqr, p->BodiesSize);
        p->NbAnims = StoredCount(p->AnimsHqr, p->AnimsSize);
        p->EntityBase = count + added;
        p->BodyBase = bodies;
        p->AnimBase = anims;
        bodies += p->NbBodies;
        anims += p->NbAnims;
        for (S32 k = 0; k < p->NbEntities; k++) {
            U32 len = 0;
            if (StoredEntry(ents, entsSize, k, &len)) addedBytes += len;
        }
        added += p->NbEntities;
        free(ents);
    }
    if (!added) return;
    // the new table: the offsets one an entity more, the game's records as they were, the maps' after them
    const U32 head = (U32)(count + added + 1) * 4;
    U8 *result = (U8 *)malloc(head + (end - offs[0]) + addedBytes + RECOVER_AREA);
    if (!result) return;
    U32 *ro = (U32 *)result;
    for (S32 i = 0; i < count; i++) ro[i] = offs[i] - offs[0] + head;
    memcpy(result + head, *table + offs[0], end - offs[0]);
    U32 at = head + (end - offs[0]);
    S32 e = count;
    for (S32 i = 0; i < NbPacks; i++) {
        T_MAP_PACK *p = &Packs[i];
        if (p->EntityBase < 0) continue;
        U8 *ents = Zip_LoadMember(p->Zip, &p->Members[p->CharEntities], 0);
        const U32 entsSize = p->Members[p->CharEntities].Size;
        for (S32 k = 0; k < p->NbEntities; k++, e++) {
            U32 len = 0;
            U8 *rec = StoredEntry(ents, entsSize, k, &len);
            ro[e] = at;
            if (!rec) continue;
            memcpy(result + at, rec, len);
            // (its bodies' and animations' numbers: the map's own HQRs' entries, made the game's)
            for (U32 q = at; q + 2 < at + len && result[q] != 255;) {
                const U8 command = result[q];
                if (command == 1) *(S16 *)(result + q + 3) = (S16)(*(S16 *)(result + q + 3) + p->BodyBase);
                else if (command == 3) *(S16 *)(result + q + 4) = (S16)(*(S16 *)(result + q + 4) + p->AnimBase);
                q += command == 3 ? 3 + result[q + 3] : 2 + result[q + 2];
            }
            at += len;
        }
        free(ents);
        Log_Info("MAPS: %s: %d characters (entities %d on, bodies %d on, animations %d on)", p->File, (int)p->NbEntities, (int)p->EntityBase,
                 (int)p->BodyBase, (int)p->AnimBase);
    }
    ro[count + added] = at;
    *table = result; // (the game's table, loaded once at boot, is left: its buffer is the allocator's)
}

S32 MapPack_Entity(S32 scene, S32 entity) {
    if (entity < MAP_LOCAL_ENTITY) return entity;
    T_MAP_PACK *p = PackOf(scene, NULL);
    if (!p || p->EntityBase < 0 || entity - MAP_LOCAL_ENTITY >= p->NbEntities) {
        Log_Warn("MAPS: scene %d: an actor of the map's character %d, which it hasn't", (int)scene, (int)(entity - MAP_LOCAL_ENTITY));
        return 0;
    }
    return p->EntityBase + entity - MAP_LOCAL_ENTITY;
}

// A map character's body or animation: BODY.HQR's / ANIM.HQR's entry past the game's.
static U8 *CharacterHook(const char *base, S32 index, U32 *size) {
    const S32 body = !strcasecmp(base, BODY_HQR_NAME);
    if (!body && strcasecmp(base, ANIM_HQR_NAME)) return NULL;
    for (S32 i = 0; i < NbPacks; i++) {
        T_MAP_PACK *p = &Packs[i];
        if (p->EntityBase < 0) continue;
        const S32 from = body ? p->BodyBase : p->AnimBase, n = body ? p->NbBodies : p->NbAnims;
        if (index < from || index >= from + n) continue;
        U32 len = 0;
        U8 *data = body ? StoredEntry(p->BodiesHqr, p->BodiesSize, index - from, &len) : StoredEntry(p->AnimsHqr, p->AnimsSize, index - from, &len);
        if (!data) return NULL;
        free(HookEntry);
        HookEntry = (U8 *)malloc(len);
        if (!HookEntry) return NULL;
        memcpy(HookEntry, data, len);
        *size = len;
        return HookEntry;
    }
    return NULL;
}

//──────────────────────────────────────────────────────────────────────────
// SCENE.HQR's entry n + 1 (scene n) for a map's scene: its LS2.
static U8 *SceneHook(const char *name, S32 index, U32 *size) {
    if (index < 0 || !name) return NULL;
    const char *base = name + strlen(name);
    while (base > name && base[-1] != '/' && base[-1] != '\\') base--;
    if (U8 *character = CharacterHook(base, index, size)) return character;
    if (index < 1) return NULL;
    if (strcasecmp(base, SCENE_HQR_NAME)) return NULL;
    S32 k;
    T_MAP_PACK *p = PackOf(index - 1, &k);
    if (!p) return NULL;
    free(HookEntry);
    HookEntry = Zip_LoadMember(p->Zip, &p->Members[p->SceneMember[k]], 0);
    if (!HookEntry) return NULL;
    *size = p->Members[p->SceneMember[k]].Size;
    return HookEntry;
}

S32 MapPack_HasScene(S32 scene) { return PackOf(scene, NULL) != NULL; }

S32 MapPack_ZoneScene(S32 scene, S32 num) {
    if (num < MAP_LOCAL_SCENE) return num;
    T_MAP_PACK *p = PackOf(scene, NULL);
    if (!p) return num;
    S32 k = num - MAP_LOCAL_SCENE;
    if (k >= p->NbScenes || p->Scenes[k] < 0) {
        Log_Warn("MAPS: %s: a zone of scene %d goes to its scene %d, which it hasn't", p->File, (int)scene, (int)k);
        return scene;
    }
    return p->Scenes[k];
}

const char *MapPack_IslandFile(S32 scene) {
    T_MAP_PACK *p = PackOf(scene, NULL);
    return p && p->Island[0] ? p->Island : NULL;
}

S32 MapPack_Snows(S32 scene) {
    T_MAP_PACK *p = PackOf(scene, NULL);
    return p && p->Snow;
}

S32 MapPack_DropLedges(S32 scene) {
    T_MAP_PACK *p = PackOf(scene, NULL);
    return p && p->Ledges;
}

//──────────────────────────────────────────────────────────────────────────
// A map's lines of dialogue (text = TEXT.TXT): UTF-8 made the game's characters (code page 850, as TEXT.HQR's are) -- the Latin-1
// letters each its CP850 byte, anything else '?'.
static const U8 Cp850OfLatin1[96] = {
    0xFF, 0xAD, 0xBD, 0x9C, 0xCF, 0xBE, 0xDD, 0xF5, 0xF9, 0xB8, 0xA6, 0xAE, 0xAA, 0xF0, 0xA9, 0xEE, 0xF8, 0xF1, 0xFD, 0xFC,
    0xEF, 0xE6, 0xF4, 0xFA, 0xF7, 0xFB, 0xA7, 0xAF, 0xAC, 0xAB, 0xF3, 0xA8, 0xB7, 0xB5, 0xB6, 0xC7, 0x8E, 0x8F, 0x92, 0x80,
    0xD4, 0x90, 0xD2, 0xD3, 0xDE, 0xD6, 0xD7, 0xD8, 0xD1, 0xA5, 0xE3, 0xE0, 0xE2, 0xE5, 0x99, 0x9E, 0x9D, 0xEB, 0xE9, 0xEA,
    0x9A, 0xED, 0xE8, 0xE1, 0x85, 0xA0, 0x83, 0xC6, 0x84, 0x86, 0x91, 0x87, 0x8A, 0x82, 0x88, 0x89, 0x8D, 0xA1, 0x8C, 0x8B,
    0xD0, 0xA4, 0x95, 0xA2, 0x93, 0xE4, 0x94, 0xF6, 0x9B, 0x97, 0xA3, 0x96, 0x81, 0xEC, 0xE7, 0x98};

// (in place: the result is never longer than the UTF-8)
static void Utf8ToGame(char *s) {
    const U8 *r = (const U8 *)s;
    U8 *w = (U8 *)s;
    if (r[0] == 0xEF && r[1] == 0xBB && r[2] == 0xBF) r += 3; // (a byte-order mark)
    while (*r) {
        U32 c = *r++;
        if (c >= 0x80) {
            S32 more = (c & 0xE0) == 0xC0 ? 1 : (c & 0xF0) == 0xE0 ? 2 : (c & 0xF8) == 0xF0 ? 3 : 0;
            c &= more == 1 ? 0x1F : more == 2 ? 0x0F : 0x07;
            for (S32 i = 0; i < more && (*r & 0xC0) == 0x80; i++) c = (c << 6) | (*r++ & 0x3F);
            if (c == 0x2019 || c == 0x2018) c = '\'';
            else if (c == 0x201C || c == 0x201D) c = '"';
            else if (c == 0x2026) c = '.';
            *w++ = c >= 0xA0 && c <= 0xFF ? Cp850OfLatin1[c - 0xA0] : c < 0x80 ? (U8)c : '?';
        } else {
            *w++ = (U8)c;
        }
    }
    *w = 0;
}

// The lines of a text file (`data`, 0-ended UTF-8, kept: the lines point into it) -- "<number> = <line>" or "<number>:<attribute> = <line>".
static void ParseTexts(T_MAP_TEXTS *t, char *data, const char *what) {
    t->Data = data;
    Utf8ToGame(data);
    S32 lines = 1;
    for (const char *c = data; *c; c++)
        if (*c == '\n') lines++;
    t->Ids = (S32 *)malloc(sizeof(S32) * lines);
    t->Attributes = (U8 *)malloc(lines);
    t->Lines = (char **)malloc(sizeof(char *) * lines);
    if (!t->Ids || !t->Attributes || !t->Lines) return;
    char *line = data;
    while (line && *line) {
        char *next = strchr(line, '\n');
        if (next) *next++ = '\0';
        char *l = Trim(line);
        char *eq = strchr(l, '=');
        line = next;
        if (l[0] == '#' || !eq || l[0] < '0' || l[0] > '9') continue;
        *eq = '\0';
        char *colon = strchr(l, ':');
        t->Ids[t->Nb] = atoi(l);
        t->Attributes[t->Nb] = (U8)(colon ? atoi(colon + 1) : 1);
        t->Lines[t->Nb] = Trim(eq + 1);
        t->Nb++;
    }
    Log_Info("MAPS: %s: %d lines of its own text", what, (int)t->Nb);
}

static S32 FindLine(const T_MAP_TEXTS *t, S32 text, U8 **line, S32 *size, U8 *attribute) {
    for (S32 i = 0; i < t->Nb; i++)
        if (t->Ids[i] == text) {
            if (line) *line = (U8 *)t->Lines[i];
            if (size) *size = (S32)strlen(t->Lines[i]) + 1; // (with its 0, as GetText's SizeText)
            if (attribute) *attribute = t->Attributes[i];
            return TRUE;
        }
    return FALSE;
}

// (an island map installed into the game's files: its <file>_TEXT.TXT, the last island's kept)
static char InstalledTextIsland[64] = "";
static T_MAP_TEXTS InstalledTexts;

S32 MapPack_Text(S32 scene, S32 text, U8 **line, S32 *size, U8 *attribute) {
    T_MAP_PACK *p = PackOf(scene, NULL);
    if (p) {
        if (p->Text < 0) return FALSE;
        if (!p->Texts.Read) {
            p->Texts.Read = TRUE;
            char *data = (char *)Zip_LoadMember(p->Zip, &p->Members[p->Text], 1);
            if (data) ParseTexts(&p->Texts, data, p->File);
            else Log_Warn("MAPS: %s: its text %s can't be read", p->File, p->Members[p->Text].Name);
        }
        return FindLine(&p->Texts, text, line, size, attribute);
    }
    const char *island = SceneIslandFileName(scene);
    if (!island) return FALSE;
    if (strcasecmp(island, InstalledTextIsland)) {
        FreeTexts(&InstalledTexts);
        strncpy(InstalledTextIsland, island, sizeof(InstalledTextIsland) - 1);
        InstalledTexts.Read = TRUE;
        char name[ADELINE_MAX_PATH], path[ADELINE_MAX_PATH];
        snprintf(name, sizeof(name), "%s_TEXT.TXT", island);
        GetResPath(path, ADELINE_MAX_PATH, name);
        FILE *f = fopen(path, "rb");
        if (f) {
            fseek(f, 0, SEEK_END);
            long n = ftell(f);
            fseek(f, 0, SEEK_SET);
            char *data = (char *)malloc(n + 1);
            if (data && fread(data, 1, n, f) == (size_t)n) {
                data[n] = 0;
                ParseTexts(&InstalledTexts, data, name);
            } else free(data);
            fclose(f);
        }
    }
    return FindLine(&InstalledTexts, text, line, size, attribute);
}

U32 MapPack_MaxSceneSize(void) {
    U32 biggest = 0;
    Ensure();
    for (S32 p = 0; p < NbPacks; p++)
        for (S32 k = 0; k < Packs[p].NbScenes; k++)
            if (Packs[p].Members[Packs[p].SceneMember[k]].Size > biggest) biggest = Packs[p].Members[Packs[p].SceneMember[k]].Size;
    return biggest;
}

//──────────────────────────────────────────────────────────────────────────
// The bricks a grid's used blocks name (GRILLE.CPP LoadUsedBrick's first pass) and their sizes: what its buffers need.
static void PackNeeds(T_MAP_PACK *p) {
    char bkg[ADELINE_MAX_PATH], path[ADELINE_MAX_PATH];
    U8 *grid = NULL, *library = NULL, *flags = NULL;
    U32 libsize = 0, count, table, i, j;
    S32 h = 0;

    p->NeedsDone = TRUE;
    memset(&p->Needs, 0, sizeof(p->Needs));
    if (p->Grid < 0 || !BkgKnown) return;
    GetResPath(bkg, ADELINE_MAX_PATH, BKG_HQR_NAME);
    p->Needs.Gri = p->Members[p->Grid].Size;
    grid = Zip_LoadMember(p->Zip, &p->Members[p->Grid], 0);
    if (!grid || p->Members[p->Grid].Size < 34) goto done;
    if (p->Library >= 0) {
        library = Zip_LoadMember(p->Zip, &p->Members[p->Library], 0);
        libsize = p->Members[p->Library].Size;
        p->Needs.Bll = libsize;
    } else {
        libsize = HQF_Init(bkg, BkgBllStart + grid[0]);
        library = libsize ? (U8 *)malloc(libsize + RECOVER_AREA) : NULL;
        if (library) HQF_LoadClose(library);
        else HQF_Close();
    }
    if (!library || libsize < 4) goto done;

    if (p->Bricks >= 0) {
        MemberPath(p, p->Bricks, path, sizeof(path));
        table = 0;
        count = p->NbBricks;
    } else {
        strcpy(path, bkg);
        table = BkgBrkStart << 2;
        count = BkgMaxBrk;
    }
    flags = (U8 *)calloc(count + 1, 1);
    if (!flags) goto done;
    {
        U32 nbblock = *(U32 *)library / 4;
        for (i = 1; i < 256; i++) {
            if (!(grid[2 + (i >> 3)] & (1 << (7 - (i & 7)))) || i - 1 >= nbblock) continue;
            U32 at = *(U32 *)(library + (i - 1) * 4);
            if (at + 3 > libsize) continue;
            U32 cells = library[at] * library[at + 1] * library[at + 2];
            for (j = 0; j < cells && at + 5 + j * 4 + 2 <= libsize; j++) {
                U32 brick = *(U16 *)(library + at + 5 + j * 4);
                if (brick && brick - 1 < count) flags[brick - 1] = 1;
            }
        }
    }
    h = OpenRead(path);
    if (!h) goto done;
    {
        U32 *offsets = (U32 *)malloc(count * 4 + 4);
        U32 n = 0, bytes = 0;
        if (offsets) {
            Seek(h, (S32)table, SEEK_FROM_START);
            Read(h, offsets, count * 4);
            for (i = 0; i < count; i++) {
                if (!flags[i]) continue;
                COMPRESSED_HEADER header;
                Seek(h, (S32)offsets[i], SEEK_FROM_START);
                if (Read(h, &header, sizeof(header)) != (S32)sizeof(header)) continue;
                n++;
                bytes += header.SizeFile;
            }
            free(offsets);
        }
        // (LoadUsedBrick: an offset for each brick and one after; CreateMaskGph: a mask no bigger than its brick and a byte a line more)
        p->Needs.Brick = (n + 1) * 4 + bytes;
        p->Needs.Mask = (n + 1) * 4 + bytes + n * 64;
    }
    Close(h);
done:
    free(flags);
    free(library);
    free(grid);
}

void MapPack_GridNeeds(U32 bllStart, U32 brkStart, U32 maxBrk, T_MAP_GRID_NEEDS *needs) {
    BkgBllStart = bllStart;
    BkgBrkStart = brkStart;
    BkgMaxBrk = maxBrk;
    BkgKnown = TRUE;
    memset(needs, 0, sizeof(*needs));
    Ensure();
    for (S32 i = 0; i < NbPacks; i++) {
        T_MAP_PACK *p = &Packs[i];
        PackNeeds(p);
        if (p->Needs.Gri > needs->Gri) needs->Gri = p->Needs.Gri;
        if (p->Needs.Bll > needs->Bll) needs->Bll = p->Needs.Bll;
        if (p->Needs.Brick > needs->Brick) needs->Brick = p->Needs.Brick;
        if (p->Needs.Mask > needs->Mask) needs->Mask = p->Needs.Mask;
    }
}

S32 MapPack_LoadGrid(S32 scene, U8 *bufmap, U8 *tabblock, const T_MAP_GRID_NEEDS *room, S32 *ownLibrary) {
    T_MAP_PACK *p = PackOf(scene, NULL);
    *ownLibrary = FALSE;
    if (!p || p->Grid < 0) return FALSE;
    if (!p->NeedsDone) PackNeeds(p);
    if (p->Needs.Gri > room->Gri || p->Needs.Bll > room->Bll || p->Needs.Brick > room->Brick || p->Needs.Mask > room->Mask) {
        Log_Warn("MAPS: %s: scene %d's grid doesn't fit the engine's buffers (grid %u/%u, library %u/%u, bricks %u/%u, masks %u/%u): "
                 "the game is started again with it in MAPS to make room",
                 p->File, (int)scene, (unsigned)p->Needs.Gri, (unsigned)room->Gri, (unsigned)p->Needs.Bll, (unsigned)room->Bll,
                 (unsigned)p->Needs.Brick, (unsigned)room->Brick, (unsigned)p->Needs.Mask, (unsigned)room->Mask);
        return FALSE;
    }
    U8 *grid = Zip_LoadMember(p->Zip, &p->Members[p->Grid], 0);
    if (!grid) return FALSE;
    memcpy(bufmap, grid, p->Members[p->Grid].Size);
    free(grid);
    if (p->Library >= 0) {
        U8 *library = Zip_LoadMember(p->Zip, &p->Members[p->Library], 0);
        if (library) {
            memcpy(tabblock, library, p->Members[p->Library].Size);
            free(library);
            *ownLibrary = TRUE;
        }
    }
    return TRUE;
}

S32 MapPack_Bricks(S32 scene, char *path, U32 pathSize, U32 *count, S32 *forbidden) {
    T_MAP_PACK *p = PackOf(scene, NULL);
    if (!p || p->Bricks < 0) return FALSE;
    MemberPath(p, p->Bricks, path, pathSize);
    *count = p->NbBricks;
    *forbidden = p->Forbidden;
    return TRUE;
}

S32 MapPack_LoadFragment(S32 scene, S32 n, void *dst) {
    char path[ADELINE_MAX_PATH];
    T_MAP_PACK *p = PackOf(scene, NULL);
    if (!p || p->Fragments < 0) return FALSE;
    MemberPath(p, p->Fragments, path, sizeof(path));
    return Load_HQR(path, dst, n) != 0;
}
