/*──────────────────────────────────────────────────────────────────────────*/
#include <SYSTEM/ADELINE.H>
#include <SYSTEM/TIMER.H>
#include <SYSTEM/N_MALLOC.H>
#include <SYSTEM/LZ.H>
#include <SYSTEM/FILES.H>
#include <SYSTEM/HQFILE.H>
#include <SYSTEM/HQR.H>
#include <SYSTEM/HQRRESS.H>

#include <string.h>

//──────────────────────────────────────────────────────────────────────────
typedef struct {
    S32 Index;
    void *Ptr;
    S32 Size;
    U32 Time;
} T_HQR_BLOC;

//──────────────────────────────────────────────────────────────────────────
HQR_GET_CALLBACK *HQRGetErrorFunc = NULL;
HQR_GET_CALLBACK *HQRGetDelFunc = NULL;

//──────────────────────────────────────────────────────────────────────────
S32 HQR_Flag; // flag "load done" par HQR_Get

//──────────────────────────────────────────────────────────────────────────
// initialise la gestion bufferisée d'une ressource
// hqrname: nom du .HQR contenant les fiches compactées
// maxsize: taille buffer memoire
// MaxIndex: nombre de fiche max stocké
// (devrait être > maxsize/average file size )
T_HQR_HEADER *HQR_Init_Ressource(const char *hqrname,
                                 S32 maxsize,
                                 S32 maxrsrc) {
    T_HQR_HEADER *header;
    void *buffer;

    header = (T_HQR_HEADER *)Malloc(sizeof(T_HQR_HEADER) +
                                    sizeof(T_HQR_BLOC) * maxrsrc);

    if (!header) {
        return NULL;
    }

    buffer = Malloc(maxsize + RECOVER_AREA);
    if (!buffer) {
    error:
        Free(header);
        return NULL;
    }

    header->MaxSize = maxsize;
    header->MaxIndex = maxrsrc;
    header->Buffer = buffer;

    if (!HQR_Change_Ressource(header, hqrname)) {
        Free(buffer);
        goto error;
    }

    return header; // header
}

//──────────────────────────────────────────────────────────────────────────
// vide le buffer d'une ressource
void HQR_Reset_Ressource(T_HQR_HEADER *header) {
    header->FreeSize = header->MaxSize;
    header->NbIndex = 0;
}

//──────────────────────────────────────────────────────────────────────────
// change le nom du fichiers HQR qui contient les fiches (vide le buffer)
S32 HQR_Change_Ressource(T_HQR_HEADER *header, const char *newhqrname) {
    if (newhqrname && newhqrname[0]) {
        strcpy(header->Name, newhqrname);

        if (!FileSize(newhqrname)) {
            return FALSE;
        }

    } else {
        header->Name[0] = 0;
    }

    HQR_Reset_Ressource(header);

    return TRUE;
}

//──────────────────────────────────────────────────────────────────────────
// libère la mémoire allouée à la gestion bufferisée d'une ressource HQR
void HQR_Free_Ressource(T_HQR_HEADER *header) {
    if (!header) {
        return;
    }

    /*AIL_vmm_unlock((void*)header, sizeof(T_HQR_HEADER) +
				  sizeof(T_HQR_BLOC) * header->MaxIndex ) ;

	AIL_vmm_unlock(header->Buffer, header->MaxSize+RECOVER_AREA);
	*/
    Free(header->Buffer);
    Free(header);
}

//──────────────────────────────────────────────────────────────────────────
// retourne le pointeur mémoire de la fiche (index) demandée
T_HQR_BLOC *HQR_GiveBloc(S32 index, S32 nbindex, T_HQR_BLOC *bloc) {
    if (nbindex > 0) {
        T_HQR_BLOC *endOfBlocs = &bloc[nbindex];
        for (S32 n = 0; &bloc[n] != endOfBlocs; ++n) {
            T_HQR_BLOC *currBloc = &bloc[n];
            if (currBloc->Index == index) {
                return currBloc;
            }
        }
    }

    return NULL;
}

//──────────────────────────────────────────────────────────────────────────
// supprime une fiche dans le buffer d'une ressource
static inline void HQR_Del_Bloc(T_HQR_HEADER *header, S32 index) {
    S32 n;
    T_HQR_BLOC *ptrbloc;
    S32 delsize;
    void *ptrs, *ptrd;

    ptrbloc = (T_HQR_BLOC *)(header + 1);
    delsize = ptrbloc[index].Size;

    // if this is last index then skip this...
    if (index < header->NbIndex - 1) {
        // shift buffer
        ptrd = ptrbloc[index].Ptr;
        ptrs = (void *)((U8 *)ptrd + delsize);
        memmove(ptrd, ptrs, (header->MaxSize - header->FreeSize) - ((U64)ptrs - (U64)(header->Buffer)));

        // shift index table
        ptrd = (void *)&ptrbloc[index];
        ptrs = (void *)&ptrbloc[index + 1];
        memmove(ptrd, ptrs, (header->NbIndex - index - 1) * sizeof(T_HQR_BLOC));

        // shift index value
        for (n = index; n < (header->NbIndex - 1); n++) {
            *(S32 *)&(ptrbloc[n].Ptr) -= delsize;
        }
    }

    // update buffer status
    header->NbIndex--;
    header->FreeSize += delsize;
}

//──────────────────────────────────────────────────────────────────────────
// retourne le pointeur mémoire de la fiche (index) demandée
void *HQR_Get(T_HQR_HEADER *header, S32 index, S32 *outSize) {
    S32 n, oldest;
    U32 testtime;
    void *ptr;
    T_HQR_BLOC *ptrbloc;
    S32 size;

    if (index < 0)
        goto error;

    ptrbloc = HQR_GiveBloc(index, header->NbIndex, (T_HQR_BLOC *)(header + 1));

    if (ptrbloc) {
        // existing index
        ptrbloc->Time = TimerSystemHR; // update LRU data

        HQR_Flag = FALSE; // NOT NEWLY LOADED

        if (outSize != NULL) {
            *outSize = ptrbloc->Size;
        }

        return ptrbloc->Ptr;
    } else // need load
    {
        // load hqr bloc
        size = HQF_Init(header->Name, index);
        if (!size) {
            goto error;
        }

        // memory management
        ptrbloc = (T_HQR_BLOC *)(header + 1);

        // check if enough space for bloc or index
        while ((size > header->FreeSize) OR(header->NbIndex >= header->MaxIndex)) {
            // delete oldest bloc
            oldest = -1;
            testtime = -1;

            for (n = 0; n < header->NbIndex; n++) {
                if (ptrbloc[n].Time < testtime) {
                    testtime = ptrbloc[n].Time;
                    oldest = n;
                }
            }
            if (oldest == -1) // not enough ram or big trouble
            {
                HQF_Close();
            error:
                if (HQRGetErrorFunc)
                    HQRGetErrorFunc(header->Name, index);
                return NULL;
            }

            if (HQRGetDelFunc)
                HQRGetDelFunc(header->Name, index);

            HQR_Del_Bloc(header, oldest);
        }

        // compute ptr
        ptr = (void *)((U8 *)header->Buffer + header->MaxSize - header->FreeSize);

        // space size ok, update struct
        ptrbloc[header->NbIndex].Index = index;
        ptrbloc[header->NbIndex].Time = TimerSystemHR;
        ptrbloc[header->NbIndex].Ptr = ptr;
        ptrbloc[header->NbIndex].Size = size;

        // load it
        if (!HQF_LoadClose(ptr)) {
            goto error;
        }

        header->NbIndex++;
        header->FreeSize -= size;

        HQR_Flag = TRUE; // NEWLY LOADED

        if (outSize != NULL) {
            *outSize = size;
        }

        return ptr;
    }
}

//──────────────────────────────────────────────────────────────────────────
// Every entry of the resource's file unpacked into its buffer at once, the file opened once, while the buffer and the bloc table have
// room for them -- nothing already there dropped: it stops at the first that would not fit, `reserve` bytes of the buffer kept free (LBA
// Assembler, 2026-10-18: an island's bodies unpacked as the island loads -- LOADISLE.CPP -- rather than the first time each comes into
// view, which on a big island cost a 40-50 ms frame each time the camera came into a new part of it). It stops at an entry served from
// elsewhere (HQF_Hook): HQR_Get has those. Returns how many entries it unpacked.
S32 HQR_Preload(T_HQR_HEADER *header, S32 reserve) {
    if (!header || !header->Name[0])
        return 0;
    S32 file = OpenRead(header->Name);
    if (!file)
        return 0;
    S32 first = 0, done = 0;
    if (Read(file, &first, 4) != 4 || first < 8 || first > 4 * 65536) {
        Close(file);
        return 0;
    }
    const S32 count = first / 4 - 1; // (the table's last offset is the file's end: HQF_NbRes)
    U32 *offsets = (U32 *)Malloc((U32)first);
    if (!offsets) {
        Close(file);
        return 0;
    }
    offsets[0] = (U32)first;
    if (Read(file, offsets + 1, (U32)(first - 4)) != first - 4) {
        Free(offsets);
        Close(file);
        return 0;
    }
    T_HQR_BLOC *blocs = (T_HQR_BLOC *)(header + 1);
    for (S32 i = 0; i < count; i++) {
        if (!offsets[i] || HQR_GiveBloc(i, header->NbIndex, blocs))
            continue;
        if (header->NbIndex >= header->MaxIndex)
            break;
        U32 hooked = 0;
        if (HQF_Hook && HQF_Hook(header->Name, i, &hooked) && hooked)
            break;
        COMPRESSED_HEADER h;
        Seek(file, (S32)offsets[i], SEEK_FROM_START);
        if (Read(file, &h, sizeof(h)) != (S32)sizeof(h) || h.CompressMethod < 0 || h.CompressMethod > 2 || !h.SizeFile
                || (S64)h.SizeFile + reserve > header->FreeSize || (h.CompressMethod && h.CompressedSizeFile > h.SizeFile + RECOVER_AREA))
            break;
        U8 *ptr = (U8 *)header->Buffer + header->MaxSize - header->FreeSize;
        if (h.CompressMethod == 0) {
            if (Read(file, ptr, h.SizeFile) != (S32)h.SizeFile)
                break;
        } else {
            U8 *packed = ptr + h.SizeFile - h.CompressedSizeFile + RECOVER_AREA;
            if (Read(file, packed, h.CompressedSizeFile) != (S32)h.CompressedSizeFile)
                break;
            ExpandLZ(ptr, packed, h.SizeFile, h.CompressMethod + 1);
        }
        blocs[header->NbIndex].Index = i;
        blocs[header->NbIndex].Time = TimerSystemHR;
        blocs[header->NbIndex].Ptr = ptr;
        blocs[header->NbIndex].Size = (S32)h.SizeFile;
        header->NbIndex++;
        header->FreeSize -= (S32)h.SizeFile;
        done++;
    }
    Free(offsets);
    Close(file);
    return done;
}
