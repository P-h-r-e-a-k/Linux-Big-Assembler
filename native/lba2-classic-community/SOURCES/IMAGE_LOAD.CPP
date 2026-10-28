#include "IMAGE_LOAD.H"
#include "RACEGAME.H" /* RaceGame_On: the race game's menu picture in a window of any shape */

#include <SVGA/SCREEN.H>
#include <SYSTEM/HQRLOAD.H>

#include <string.h>

U8 *ImageStaging = NULL;

static void Image_CopyCenteredFromStaging(void *destVoid) {
    /* Treat staging as a tightly-packed 640x480 buffer; treat dest as a
       ModeDesiredX-stride buffer. Centre the 640x480 content in the
       (ModeDesiredX × ModeDesiredY) destination. At 640x480 the offsets are
       both zero and the loop degenerates to a simple memcpy with stride=640
       on both sides — byte-identical to a raw Load_HQR. */
    U8 *dest = (U8 *)destVoid;
    const S32 destStride = (S32)ModeDesiredX;
    const S32 xOffset = (destStride - IMAGE_AUTHORED_WIDTH) / 2;
    const S32 yOffset = ((S32)ModeDesiredY - IMAGE_AUTHORED_HEIGHT) / 2;
    const U8 *src = ImageStaging;
    for (S32 y = 0; y < IMAGE_AUTHORED_HEIGHT; y++) {
        memcpy(dest + (yOffset + y) * destStride + xOffset,
               src + y * IMAGE_AUTHORED_WIDTH,
               IMAGE_AUTHORED_WIDTH);
    }
}

static void Image_StretchFromStaging(void *destVoid) {
    /* Nearest-neighbour fill of (ModeDesiredX × ModeDesiredY) from the
       640x480 staging. Pixel-art friendly (matches the rest of the engine's
       blocky look) and stride-aware on both sides. At 640x480 every srcY/srcX
       equals y/x, so the result is byte-identical to a memcpy via stride=640. */
    U8 *dest = (U8 *)destVoid;
    const S32 destW = (S32)ModeDesiredX;
    const S32 destH = (S32)ModeDesiredY;
    const U8 *src = ImageStaging;
    /* The race game's window takes any shape (RES_SWITCH.CPP Res_ApplyWindowFit), and its menu's picture is stretched no wider than
       2:1 (a 16:9 screen, as every widescreen menu has it, or a maximised window on one) and never taller than it is: beyond that it
       keeps its shape, centred, and the bands either side of it take the colour its edge has most of (the night sky over it, the
       ground under it) -- never squashed or drawn out more. */
    if (RaceGame_On() && destW > 0 && destH > 0) {
        double sx = (double)destW / IMAGE_AUTHORED_WIDTH, sy = (double)destH / IMAGE_AUTHORED_HEIGHT;
        if (sx > sy * 1.5)
            sx = sy * 1.5;
        if (sy > sx)
            sy = sx;
        // (the picture's size and place: the whole destination when it isn't held back -- then exactly the plain stretch below)
        S32 cw = (S32)(IMAGE_AUTHORED_WIDTH * sx + 0.5), ch = (S32)(IMAGE_AUTHORED_HEIGHT * sy + 0.5);
        cw = cw > destW ? destW : cw < 1 ? 1 : cw;
        ch = ch > destH ? destH : ch < 1 ? 1 : ch;
        const S32 ox = (destW - cw) / 2, oy = (destH - ch) / 2;
        // (each edge's commonest colour, over a strip of it 8 pixels deep: 0 top, 1 bottom, 2 left, 3 right)
        U8 band[4];
        for (S32 e = 0; e < 4; e++) {
            S32 count[256] = {0};
            const S32 n = e < 2 ? IMAGE_AUTHORED_WIDTH : IMAGE_AUTHORED_HEIGHT;
            for (S32 d = 0; d < 8; d++)
                for (S32 i = 0; i < n; i++) {
                    const S32 x = e < 2 ? i : e == 2 ? d : IMAGE_AUTHORED_WIDTH - 1 - d;
                    const S32 y = e >= 2 ? i : e == 0 ? d : IMAGE_AUTHORED_HEIGHT - 1 - d;
                    count[src[y * IMAGE_AUTHORED_WIDTH + x]]++;
                }
            S32 best = 0;
            for (S32 c = 1; c < 256; c++)
                if (count[c] > count[best])
                    best = c;
            band[e] = (U8)best;
        }
        for (S32 y = 0; y < destH; y++) {
            U8 *destRow = dest + y * destW;
            if (y < oy || y >= oy + ch) {
                memset(destRow, band[y < oy ? 0 : 1], (size_t)destW);
                continue;
            }
            const U8 *srcRow = src + ((y - oy) * IMAGE_AUTHORED_HEIGHT / ch) * IMAGE_AUTHORED_WIDTH;
            for (S32 x = 0; x < destW; x++)
                destRow[x] = x < ox ? band[2] : x >= ox + cw ? band[3] : srcRow[(x - ox) * IMAGE_AUTHORED_WIDTH / cw];
        }
        return;
    }
    for (S32 y = 0; y < destH; y++) {
        const S32 srcY = (y * IMAGE_AUTHORED_HEIGHT) / destH;
        const U8 *srcRow = src + srcY * IMAGE_AUTHORED_WIDTH;
        U8 *destRow = dest + y * destW;
        for (S32 x = 0; x < destW; x++) {
            const S32 srcX = (x * IMAGE_AUTHORED_WIDTH) / destW;
            destRow[x] = srcRow[srcX];
        }
    }
}

U32 Image_LoadCentered(const char *file, void *dest, S32 index) {
    U32 size = Load_HQR(file, ImageStaging, index);
    if (size == 0)
        return 0;
    Image_CopyCenteredFromStaging(dest);
    return size;
}

U32 Image_LoadStretched(const char *file, void *dest, S32 index) {
    U32 size = Load_HQR(file, ImageStaging, index);
    if (size == 0)
        return 0;
    Image_StretchFromStaging(dest);
    return size;
}

U32 Image_LoadCenteredFillFromEdge(const char *file, void *dest, S32 index) {
    U32 size = Load_HQR(file, ImageStaging, index);
    if (size == 0)
        return 0;
    /* Sample the loaded bitmap's top-left pixel and use it as the
       sidebar fill. For centred-on-uniform-bg PCXs (boot bumpers, the
       Adeline logo with its white background) the edge colour is the
       natural background colour, so widescreen sidebars composite
       seamlessly with the image. At 640x480 the memset is fully
       overwritten by the centred copy, so this is byte-identical to
       Image_LoadCentered at the canonical width. */
    memset(dest, ImageStaging[0],
           (size_t)ModeDesiredX * (size_t)ModeDesiredY);
    Image_CopyCenteredFromStaging(dest);
    return size;
}

void Image_RenderCenteredFromStaging(void *dest) {
    Image_CopyCenteredFromStaging(dest);
}
