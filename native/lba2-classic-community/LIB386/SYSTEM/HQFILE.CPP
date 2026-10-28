/*──────────────────────────────────────────────────────────────────────────*/
#include <SYSTEM/ADELINE.H>
#include <SYSTEM/FILES.H>
#include <SYSTEM/LZ.H>
#include <SYSTEM/HQR.H>
#include <SYSTEM/HQFILE.H>

#include <stdio.h>
#include <string.h>

/*──────────────────────────────────────────────────────────────────────────*/
S32 HQF_File;
static COMPRESSED_HEADER HQF_header;

// An entry served from elsewhere (HQFILE.H HQF_Hook: LBA Assembler's maps in the MAPS folder -- SCENE.HQR's entries for their scenes,
// SOURCES/MAPPACK.CPP), between HQF_Init and HQF_LoadClose.
U8 *(*HQF_Hook)(const char *name, S32 index, U32 *size) = NULL;
static U8 *HQF_HookData = NULL;

/*──────────────────────────────────────────────────────────────────────────*/
U32 HQF_Init(const char *name, S32 index) {
    S32 nbbloc;

    HQF_HookData = NULL;
    if (HQF_Hook) {
        U32 size = 0;
        U8 *data = HQF_Hook(name, index, &size);
        if (data && size) {
            HQF_HookData = data;
            HQF_header.SizeFile = size;
            HQF_header.CompressedSizeFile = size;
            HQF_header.CompressMethod = 0;
            return size;
        }
    }

    HQF_File = OpenRead(name);
    if (!HQF_File) {
        printf("Error loading file %s\n", name);
        return 0;
    }

    index *= 4;
    Read(HQF_File, &nbbloc, 4);

    if (index >= nbbloc) {
    error:
        HQF_Close();
        return 0;
    }

    Seek(HQF_File, index, SEEK_FROM_START);
    Read(HQF_File, &index, 4);

    if (!index) {
        goto error;
    }

    Seek(HQF_File, index, SEEK_FROM_START);
    Read(HQF_File, (void *)&HQF_header, sizeof(HQF_header));

    return HQF_header.SizeFile;
}

/*──────────────────────────────────────────────────────────────────────────*/
void HQF_Close() {
    HQF_HookData = NULL;
    if (HQF_File) {
        Close(HQF_File);
        HQF_File = 0;
    }
}

/*──────────────────────────────────────────────────────────────────────────*/
U32 HQF_LoadClose(void *ptr) {
    if (HQF_HookData) {
        memcpy(ptr, HQF_HookData, HQF_header.SizeFile);
        HQF_HookData = NULL;
        return HQF_header.SizeFile;
    }
    if (!HQF_File) {
        return 0;
    }

    if (HQF_header.CompressMethod == 0) {
        /* Stored */
        Read(HQF_File, ptr, HQF_header.SizeFile);
    } else if (HQF_header.CompressMethod <= 2) {
        void *ptrdecomp;

        ptrdecomp = (void *)((U8 *)ptr + HQF_header.SizeFile - HQF_header.CompressedSizeFile + RECOVER_AREA);

        Read(HQF_File, ptrdecomp, HQF_header.CompressedSizeFile);

        /* LZSS/LZMIT */
        ExpandLZ(ptr, ptrdecomp, HQF_header.SizeFile, HQF_header.CompressMethod + 1);
    } else {
        HQF_header.SizeFile = 0;
    }

    HQF_Close();
    return HQF_header.SizeFile;
}

/*──────────────────────────────────────────────────────────────────────────*/
S32 HQF_ResSize(const char *name, S32 index) {
    U32 size;

    size = HQF_Init(name, index);
    HQF_Close();

    return size;
}

/*──────────────────────────────────────────────────────────────────────────*/
// retourne le nombre de bloc d'un HQR ( 0 si HQR non trouvé )
S32 HQF_NbRes(char *name) {
    S32 handle;
    S32 nbbloc;

    handle = OpenRead(name);
    if (!handle) {
        return 0;
    }

    Read(handle, &nbbloc, 4);
    Close(handle);

    return (nbbloc / 4 - 1);
}

/*──────────────────────────────────────────────────────────────────────────*/
