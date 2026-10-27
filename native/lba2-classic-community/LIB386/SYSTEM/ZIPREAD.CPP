//──────────────────────────────────────────────────────────────────────────
// Zip files and virtual files read from them: see ZIPREAD.H.
//──────────────────────────────────────────────────────────────────────────
#include <SYSTEM/ZIPREAD.H>

#include <SYSTEM/FILES.H> // ESeekOrigin values
#include <SYSTEM/LOG.H>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

//──────────────────────────────────────────────────────────────────────────
static U32 Le16(const U8 *p) { return (U32)p[0] | ((U32)p[1] << 8); }
static U32 Le32(const U8 *p) { return (U32)p[0] | ((U32)p[1] << 8) | ((U32)p[2] << 16) | ((U32)p[3] << 24); }

// Same path, '/' and '\' alike, case not counted.
static int SamePath(const char *a, const char *b) {
    for (;; a++, b++) {
        char ca = *a, cb = *b;
        if (ca == '\\') ca = '/';
        if (cb == '\\') cb = '/';
        if (ca >= 'a' && ca <= 'z') ca -= 32;
        if (cb >= 'a' && cb <= 'z') cb -= 32;
        if (ca != cb) return 0;
        if (!ca) return 1;
    }
}

//══════════════════════════════════════════════════════════════════════════
// Inflate (RFC 1951): canonical Huffman decoding, a symbol a bit at a time.
//══════════════════════════════════════════════════════════════════════════
typedef struct {
    const U8 *In;
    U32 InLen, InPos;
    U32 BitBuf;
    S32 BitCnt;
    U8 *Out;
    U32 OutLen, OutPos;
    S32 Error;
} T_INFLATE;

typedef struct {
    S16 *Count;  // codes of each length (0..15)
    S16 *Symbol; // symbols, by code
} T_HUFF;

static S32 Bits(T_INFLATE *s, S32 need) {
    U32 val = s->BitBuf;
    while (s->BitCnt < need) {
        if (s->InPos >= s->InLen) {
            s->Error = 1;
            return 0;
        }
        val |= (U32)s->In[s->InPos++] << s->BitCnt;
        s->BitCnt += 8;
    }
    s->BitBuf = need < 32 ? val >> need : 0;
    s->BitCnt -= need;
    return (S32)(val & ((1UL << need) - 1));
}

static S32 Stored(T_INFLATE *s) {
    U32 len, nlen;
    s->BitBuf = 0;
    s->BitCnt = 0;
    if (s->InPos + 4 > s->InLen) return -1;
    len = Le16(s->In + s->InPos);
    nlen = Le16(s->In + s->InPos + 2);
    s->InPos += 4;
    if (len != (~nlen & 0xFFFF)) return -2;
    if (s->InPos + len > s->InLen) return -1;
    if (s->OutPos + len > s->OutLen) return -3;
    memcpy(s->Out + s->OutPos, s->In + s->InPos, len);
    s->OutPos += len;
    s->InPos += len;
    return 0;
}

static S32 Decode(T_INFLATE *s, const T_HUFF *h) {
    S32 code = 0, first = 0, index = 0, len, count;
    for (len = 1; len <= 15; len++) {
        code |= Bits(s, 1);
        if (s->Error) return -10;
        count = h->Count[len];
        if (code - count < first) return h->Symbol[index + (code - first)];
        index += count;
        first += count;
        first <<= 1;
        code <<= 1;
    }
    return -10;
}

// The decoding tables for code lengths `length[0..n-1]`: 0 if the code is
// complete, > 0 if incomplete, < 0 if over-subscribed.
static S32 Construct(T_HUFF *h, const S16 *length, S32 n) {
    S16 offs[16];
    S32 symbol, len, left;
    for (len = 0; len <= 15; len++) h->Count[len] = 0;
    for (symbol = 0; symbol < n; symbol++) h->Count[length[symbol]]++;
    if (h->Count[0] == n) return 0;
    left = 1;
    for (len = 1; len <= 15; len++) {
        left <<= 1;
        left -= h->Count[len];
        if (left < 0) return left;
    }
    offs[1] = 0;
    for (len = 1; len < 15; len++) offs[len + 1] = (S16)(offs[len] + h->Count[len]);
    for (symbol = 0; symbol < n; symbol++)
        if (length[symbol] != 0) h->Symbol[offs[length[symbol]]++] = (S16)symbol;
    return left;
}

static const S16 LenBase[29] = {3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258};
static const S16 LenExtra[29] = {0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0};
static const U16 DistBase[30] = {1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577};
static const S16 DistExtra[30] = {0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13};

static S32 Codes(T_INFLATE *s, const T_HUFF *lencode, const T_HUFF *distcode) {
    S32 symbol, len;
    U32 dist;
    do {
        symbol = Decode(s, lencode);
        if (symbol < 0) return symbol;
        if (symbol < 256) {
            if (s->OutPos >= s->OutLen) return -3;
            s->Out[s->OutPos++] = (U8)symbol;
        } else if (symbol > 256) {
            symbol -= 257;
            if (symbol >= 29) return -10;
            len = LenBase[symbol] + Bits(s, LenExtra[symbol]);
            symbol = Decode(s, distcode);
            if (symbol < 0) return symbol;
            if (symbol >= 30) return -10;
            dist = DistBase[symbol] + (U32)Bits(s, DistExtra[symbol]);
            if (s->Error) return -10;
            if (dist > s->OutPos) return -11;
            if (s->OutPos + (U32)len > s->OutLen) return -3;
            while (len--) {
                s->Out[s->OutPos] = s->Out[s->OutPos - dist];
                s->OutPos++;
            }
        }
    } while (symbol != 256);
    return 0;
}

static S32 Fixed(T_INFLATE *s) {
    static S32 built = 0;
    static S16 lencnt[16], lensym[288], distcnt[16], distsym[30];
    static T_HUFF lencode = {lencnt, lensym}, distcode = {distcnt, distsym};
    if (!built) {
        S16 lengths[288];
        S32 symbol;
        for (symbol = 0; symbol < 144; symbol++) lengths[symbol] = 8;
        for (; symbol < 256; symbol++) lengths[symbol] = 9;
        for (; symbol < 280; symbol++) lengths[symbol] = 7;
        for (; symbol < 288; symbol++) lengths[symbol] = 8;
        Construct(&lencode, lengths, 288);
        for (symbol = 0; symbol < 30; symbol++) lengths[symbol] = 5;
        Construct(&distcode, lengths, 30);
        built = 1;
    }
    return Codes(s, &lencode, &distcode);
}

static S32 Dynamic(T_INFLATE *s) {
    static const S16 order[19] = {16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15};
    S16 lengths[286 + 30];
    S16 lencnt[16], lensym[286], distcnt[16], distsym[30];
    T_HUFF lencode = {lencnt, lensym}, distcode = {distcnt, distsym};
    S32 nlen, ndist, ncode, index, err, symbol, len;

    nlen = Bits(s, 5) + 257;
    ndist = Bits(s, 5) + 1;
    ncode = Bits(s, 4) + 4;
    if (s->Error || nlen > 286 || ndist > 30) return -3;
    for (index = 0; index < ncode; index++) lengths[order[index]] = (S16)Bits(s, 3);
    for (; index < 19; index++) lengths[order[index]] = 0;
    if (s->Error) return -10;
    if (Construct(&lencode, lengths, 19) != 0) return -4;

    index = 0;
    while (index < nlen + ndist) {
        symbol = Decode(s, &lencode);
        if (symbol < 0) return symbol;
        if (symbol < 16) {
            lengths[index++] = (S16)symbol;
        } else {
            len = 0;
            if (symbol == 16) {
                if (index == 0) return -5;
                len = lengths[index - 1];
                symbol = 3 + Bits(s, 2);
            } else if (symbol == 17) {
                symbol = 3 + Bits(s, 3);
            } else {
                symbol = 11 + Bits(s, 7);
            }
            if (s->Error) return -10;
            if (index + symbol > nlen + ndist) return -6;
            while (symbol--) lengths[index++] = (S16)len;
        }
    }
    if (lengths[256] == 0) return -9;
    err = Construct(&lencode, lengths, nlen);
    if (err && (err < 0 || nlen != lencode.Count[0] + lencode.Count[1])) return -7;
    err = Construct(&distcode, lengths + nlen, ndist);
    if (err && (err < 0 || ndist != distcode.Count[0] + distcode.Count[1])) return -8;
    return Codes(s, &lencode, &distcode);
}

extern "C" S32 Zip_Inflate(U8 *out, U32 outlen, const U8 *in, U32 inlen) {
    T_INFLATE s;
    S32 last, type, err;
    memset(&s, 0, sizeof(s));
    s.In = in;
    s.InLen = inlen;
    s.Out = out;
    s.OutLen = outlen;
    do {
        last = Bits(&s, 1);
        type = Bits(&s, 2);
        if (s.Error) return -1;
        err = type == 0 ? Stored(&s) : type == 1 ? Fixed(&s) : type == 2 ? Dynamic(&s) : -1;
        if (err) return err;
    } while (!last);
    return s.OutPos == outlen ? 0 : -1;
}

//══════════════════════════════════════════════════════════════════════════
// The central directory.
//══════════════════════════════════════════════════════════════════════════
extern "C" S32 Zip_ReadDirectory(const char *zip, T_ZIP_MEMBER *members, S32 max) {
    FILE *f;
    long size;
    U32 tail, i, at, cdsize, cdoffset, entries;
    U8 *buf = NULL, *cd = NULL;
    S32 count = -1;

    f = fopen(zip, "rb");
    if (!f) return -1;
    if (fseek(f, 0, SEEK_END) != 0 || (size = ftell(f)) < 22) goto done;

    // the end of central directory record: in the last 22 bytes, or before a comment of up to 65535
    tail = (U32)(size < 22 + 65535 ? size : 22 + 65535);
    buf = (U8 *)malloc(tail);
    if (!buf || fseek(f, size - (long)tail, SEEK_SET) != 0 || fread(buf, 1, tail, f) != tail) goto done;
    for (at = tail - 22 + 1; at-- > 0;)
        if (Le32(buf + at) == 0x06054B50) break;
    if (at == (U32)-1 || Le32(buf + at) != 0x06054B50) goto done;
    entries = Le16(buf + at + 10);
    cdsize = Le32(buf + at + 12);
    cdoffset = Le32(buf + at + 16);
    if (cdoffset == 0xFFFFFFFF || (long)cdoffset + (long)cdsize > size) goto done; // (Zip64)

    cd = (U8 *)malloc(cdsize ? cdsize : 1);
    if (!cd || fseek(f, (long)cdoffset, SEEK_SET) != 0 || fread(cd, 1, cdsize, f) != cdsize) goto done;

    count = 0;
    for (i = 0, at = 0; i < entries && at + 46 <= cdsize; i++) {
        U32 namelen, extralen, commentlen, flags, local;
        U8 lh[30];
        if (Le32(cd + at) != 0x02014B50) break;
        flags = Le16(cd + at + 8);
        namelen = Le16(cd + at + 28);
        extralen = Le16(cd + at + 30);
        commentlen = Le16(cd + at + 32);
        local = Le32(cd + at + 42);
        if (at + 46 + namelen > cdsize) break;
        if (!(flags & 1) && namelen < ZIP_MAX_NAME && count < max
            && fseek(f, (long)local, SEEK_SET) == 0 && fread(lh, 1, 30, f) == 30 && Le32(lh) == 0x04034B50) {
            T_ZIP_MEMBER *m = &members[count];
            memcpy(m->Name, cd + at + 46, namelen);
            m->Name[namelen] = '\0';
            m->Method = (U16)Le16(cd + at + 10);
            m->CompressedSize = Le32(cd + at + 20);
            m->Size = Le32(cd + at + 24);
            // (the local header's own name and extra field lengths: its extra field needn't be the central one's)
            m->DataOffset = local + 30 + Le16(lh + 26) + Le16(lh + 28);
            if ((m->Method == 0 || m->Method == 8) && m->Name[0] && m->Name[namelen - 1] != '/') count++;
        } else if (!(flags & 1) && namelen < ZIP_MAX_NAME && count >= max) {
            count++; // (counted, not kept: the caller's table is full)
        }
        at += 46 + namelen + extralen + commentlen;
    }

done:
    free(cd);
    free(buf);
    fclose(f);
    return count;
}

extern "C" S32 Zip_FindMember(const T_ZIP_MEMBER *members, S32 count, const char *name) {
    for (S32 i = 0; i < count; i++)
        if (SamePath(members[i].Name, name)) return i;
    return -1;
}

extern "C" U8 *Zip_LoadMember(const char *zip, const T_ZIP_MEMBER *member, U32 extra) {
    FILE *f;
    U8 *out, *packed;
    S32 ok = 0;

    out = (U8 *)malloc(member->Size + extra + 1);
    if (!out) return NULL;
    f = fopen(zip, "rb");
    if (!f) {
        free(out);
        return NULL;
    }
    if (fseek(f, (long)member->DataOffset, SEEK_SET) == 0) {
        if (member->Method == 0) {
            ok = member->CompressedSize == member->Size && fread(out, 1, member->Size, f) == member->Size;
        } else if (member->Method == 8) {
            packed = (U8 *)malloc(member->CompressedSize + 1);
            if (packed && fread(packed, 1, member->CompressedSize, f) == member->CompressedSize)
                ok = Zip_Inflate(out, member->Size, packed, member->CompressedSize) == 0;
            free(packed);
        }
    }
    fclose(f);
    if (!ok) {
        Log_Warn("ZIP: couldn't read %s from %s", member->Name, zip);
        free(out);
        return NULL;
    }
    if (extra) memset(out + member->Size, 0, extra);
    return out;
}

//══════════════════════════════════════════════════════════════════════════
// Virtual files.
//══════════════════════════════════════════════════════════════════════════
#define VFILE_MAX 512
#define VFILE_HANDLE_BASE 0x50000000
#define VFILE_MAX_HANDLES 32
#define VFILE_CACHE 4

typedef struct {
    char Path[ADELINE_MAX_PATH];
    char Zip[ADELINE_MAX_PATH];
    T_ZIP_MEMBER Member;
} T_VFILE;

typedef struct {
    S32 Used;
    S32 File;    // T_VFILE index
    FILE *Stream; // a stored member: read in place
    U8 *Data;     // a deflated one: inflated (a cache entry's)
    U32 Size, Pos;
} T_VHANDLE;

// (a deflated member inflated stays a while: an island's ILE is opened for every entry read from it)
typedef struct {
    S32 File;
    U8 *Data;
    U32 Age;
} T_VCACHE;

static T_VFILE *VFiles = NULL;
static S32 NbVFiles = 0;
static T_VHANDLE VHandles[VFILE_MAX_HANDLES];
static T_VCACHE VCache[VFILE_CACHE];
static U32 VClock = 0;

extern "C" void VFile_Clear(void) {
    for (S32 i = 0; i < VFILE_MAX_HANDLES; i++)
        if (VHandles[i].Used && VHandles[i].Stream) fclose(VHandles[i].Stream);
    memset(VHandles, 0, sizeof(VHandles));
    for (S32 i = 0; i < VFILE_CACHE; i++) free(VCache[i].Data);
    memset(VCache, 0, sizeof(VCache));
    free(VFiles);
    VFiles = NULL;
    NbVFiles = 0;
}

static S32 FindVFile(const char *path) {
    for (S32 i = 0; i < NbVFiles; i++)
        if (SamePath(VFiles[i].Path, path)) return i;
    return -1;
}

extern "C" S32 VFile_Add(const char *path, const char *zip, const T_ZIP_MEMBER *member) {
    S32 i = FindVFile(path);
    if (i < 0) {
        if (!VFiles) VFiles = (T_VFILE *)calloc(VFILE_MAX, sizeof(T_VFILE));
        if (!VFiles || NbVFiles >= VFILE_MAX) return FALSE;
        i = NbVFiles++;
    }
    strncpy(VFiles[i].Path, path, ADELINE_MAX_PATH - 1);
    VFiles[i].Path[ADELINE_MAX_PATH - 1] = '\0';
    strncpy(VFiles[i].Zip, zip, ADELINE_MAX_PATH - 1);
    VFiles[i].Zip[ADELINE_MAX_PATH - 1] = '\0';
    VFiles[i].Member = *member;
    // (a cache entry of whatever was under that path before goes)
    for (S32 c = 0; c < VFILE_CACHE; c++)
        if (VCache[c].Data && VCache[c].File == i) {
            free(VCache[c].Data);
            VCache[c].Data = NULL;
        }
    return TRUE;
}

extern "C" S32 VFile_Exists(const char *path) { return NbVFiles && FindVFile(path) >= 0; }

extern "C" bool VFile_IsHandle(S32 handle) {
    return handle >= VFILE_HANDLE_BASE && handle < VFILE_HANDLE_BASE + VFILE_MAX_HANDLES;
}

static T_VHANDLE *VSlot(S32 handle) {
    if (!VFile_IsHandle(handle)) return NULL;
    T_VHANDLE *h = &VHandles[handle - VFILE_HANDLE_BASE];
    return h->Used ? h : NULL;
}

static U8 *Inflated(S32 file) {
    S32 c, oldest = 0;
    for (c = 0; c < VFILE_CACHE; c++)
        if (VCache[c].Data && VCache[c].File == file) {
            VCache[c].Age = ++VClock;
            return VCache[c].Data;
        }
    for (c = 0; c < VFILE_CACHE; c++) {
        if (!VCache[c].Data) {
            oldest = c;
            break;
        }
        if (VCache[c].Age < VCache[oldest].Age) oldest = c;
    }
    U8 *data = Zip_LoadMember(VFiles[file].Zip, &VFiles[file].Member, 0);
    if (!data) return NULL;
    // (one still being read keeps its own: a handle's Data is never the one freed here)
    for (S32 h = 0; h < VFILE_MAX_HANDLES; h++)
        if (VHandles[h].Used && VHandles[h].Data == VCache[oldest].Data) VCache[oldest].Data = NULL;
    free(VCache[oldest].Data);
    VCache[oldest].File = file;
    VCache[oldest].Data = data;
    VCache[oldest].Age = ++VClock;
    return data;
}

extern "C" S32 VFile_OpenRead(const char *path) {
    S32 file, i;
    if (!NbVFiles || (file = FindVFile(path)) < 0) return 0;
    for (i = 0; i < VFILE_MAX_HANDLES; i++)
        if (!VHandles[i].Used) break;
    if (i == VFILE_MAX_HANDLES) {
        Log_Warn("ZIP: out of virtual file handles opening %s", path);
        return 0;
    }
    T_VHANDLE *h = &VHandles[i];
    memset(h, 0, sizeof(*h));
    h->File = file;
    h->Size = VFiles[file].Member.Size;
    if (VFiles[file].Member.Method == 0) {
        h->Stream = fopen(VFiles[file].Zip, "rb");
        if (!h->Stream) return 0;
    } else {
        h->Data = Inflated(file);
        if (!h->Data) return 0;
    }
    h->Used = 1;
    return VFILE_HANDLE_BASE + i;
}

extern "C" S32 VFile_Read(S32 handle, void *buffer, U32 maxBytes) {
    T_VHANDLE *h = VSlot(handle);
    if (!h || !buffer) return -1;
    U32 n = h->Pos >= h->Size ? 0 : h->Size - h->Pos;
    if (n > maxBytes) n = maxBytes;
    if (!n) return 0;
    if (h->Data) {
        memcpy(buffer, h->Data + h->Pos, n);
    } else {
        if (fseek(h->Stream, (long)(VFiles[h->File].Member.DataOffset + h->Pos), SEEK_SET) != 0) return -1;
        n = (U32)fread(buffer, 1, n, h->Stream);
    }
    h->Pos += n;
    return (S32)n;
}

extern "C" S32 VFile_Seek(S32 handle, S32 offset, S32 origin) {
    T_VHANDLE *h = VSlot(handle);
    if (!h) return -1;
    long np;
    switch (origin) {
    case SEEK_FROM_START: np = offset; break;
    case SEEK_FROM_CURRENT: np = (long)h->Pos + offset; break;
    case SEEK_FROM_END: np = (long)h->Size + offset; break;
    default: return -1;
    }
    if (np < 0) np = 0;
    if (np > (long)h->Size) np = (long)h->Size;
    h->Pos = (U32)np;
    return (S32)h->Pos;
}

extern "C" S32 VFile_Close(S32 handle) {
    T_VHANDLE *h = VSlot(handle);
    if (!h) return -1;
    if (h->Stream) fclose(h->Stream);
    // (an inflated member no cache entry holds any more -- pushed out while this was open -- goes with its last reader)
    if (h->Data) {
        S32 cached = 0;
        for (S32 c = 0; c < VFILE_CACHE; c++)
            if (VCache[c].Data == h->Data) cached = 1;
        S32 shared = 0;
        for (S32 o = 0; o < VFILE_MAX_HANDLES; o++)
            if (&VHandles[o] != h && VHandles[o].Used && VHandles[o].Data == h->Data) shared = 1;
        if (!cached && !shared) free(h->Data);
    }
    memset(h, 0, sizeof(*h));
    return 0;
}

//──────────────────────────────────────────────────────────────────────────
