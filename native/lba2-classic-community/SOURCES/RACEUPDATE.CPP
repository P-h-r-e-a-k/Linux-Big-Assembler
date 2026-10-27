/* RACEUPDATE: the race game's updater started -- RACEUPDATE.H says what it is. Its own file for windows.h, whose names (Font, RGB, ...)
 * the engine's sources have too. */
#include "RACEUPDATE.H"

#include <SYSTEM/LOG.H>

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <cstdlib>
#include <cwchar>

// A UTF-8 path as a full wide one, its trailing separators off (a "dir\" before a closing quote would escape it). Zero: it can't be.
static int Wide(const char *utf8, wchar_t *out, DWORD size) {
    wchar_t raw[MAX_PATH * 4];
    if (!MultiByteToWideChar(CP_UTF8, 0, utf8, -1, raw, (int)(sizeof(raw) / sizeof(raw[0])))) return 0;
    const DWORD n = GetFullPathNameW(raw, size, out, NULL);
    if (!n || n >= size) return 0;
    size_t len = wcslen(out);
    while (len > 3 && (out[len - 1] == L'\\' || out[len - 1] == L'/')) out[--len] = 0;
    return 1;
}

int RaceUpdate_Exists(const char *pathUtf8) {
    wchar_t path[MAX_PATH * 4];
    if (!Wide(pathUtf8, path, MAX_PATH * 4)) return 0;
    const DWORD attr = GetFileAttributesW(path);
    return attr != INVALID_FILE_ATTRIBUTES && !(attr & FILE_ATTRIBUTE_DIRECTORY);
}

int RaceUpdate_Spawn(const char *exeUtf8, const char *installDirUtf8, int manual) {
    wchar_t exe[MAX_PATH * 4], dir[MAX_PATH * 4], work[MAX_PATH * 4];
    if (!Wide(exeUtf8, exe, MAX_PATH * 4) || !Wide(installDirUtf8, dir, MAX_PATH * 4)) return 0;
    const DWORD attr = GetFileAttributesW(exe);
    if (attr == INVALID_FILE_ATTRIBUTES || (attr & FILE_ATTRIBUTE_DIRECTORY)) return 0;
    wcscpy(work, exe);
    wchar_t *slash = wcsrchr(work, L'\\');
    if (slash) *slash = 0;
    static wchar_t cmd[MAX_PATH * 10];
    swprintf(cmd, sizeof(cmd) / sizeof(cmd[0]), L"\"%ls\" --check-update \"%ls\" --wait-pid %lu%ls", exe, dir, (unsigned long)GetCurrentProcessId(),
             manual ? L" --manual" : L"");
    // (no STARTF_USESHOWWINDOW/SW_HIDE: the updater has no window of its own, and a hidden show state would hide the first one it opens --
    // its "Install it now?" question)
    STARTUPINFOW si;
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    PROCESS_INFORMATION pi;
    ZeroMemory(&pi, sizeof(pi));
    // (no handles handed down, no window, a process group of its own: it outlives the game, which it waits for)
    if (!CreateProcessW(exe, cmd, NULL, NULL, FALSE, CREATE_NO_WINDOW | CREATE_NEW_PROCESS_GROUP, NULL, slash ? work : NULL, &si, &pi)) {
        Log_Warn("[racegame] the updater could not be started (%s): error %lu", exeUtf8, (unsigned long)GetLastError());
        return 0;
    }
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    Log_Info("[racegame] the updater started (pid %lu): %s --check-update%s", (unsigned long)pi.dwProcessId, exeUtf8, manual ? " --manual" : "");
    return 1;
}

int RaceUpdate_Tool(const char *exeUtf8, const char *what, const char *folderUtf8) {
    wchar_t exe[MAX_PATH * 4], folder[MAX_PATH * 4], work[MAX_PATH * 4], verb[64];
    if (!Wide(exeUtf8, exe, MAX_PATH * 4) || !Wide(folderUtf8, folder, MAX_PATH * 4)) return 0;
    if (!MultiByteToWideChar(CP_UTF8, 0, what, -1, verb, 64)) return 0;
    const DWORD attr = GetFileAttributesW(exe);
    if (attr == INVALID_FILE_ATTRIBUTES || (attr & FILE_ATTRIBUTE_DIRECTORY)) return 0;
    wcscpy(work, exe);
    wchar_t *slash = wcsrchr(work, L'\\');
    if (slash) *slash = 0;
    static wchar_t cmd[MAX_PATH * 10];
    swprintf(cmd, sizeof(cmd) / sizeof(cmd[0]), L"\"%ls\" %ls \"%ls\"", exe, verb, folder);
    STARTUPINFOW si;
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    PROCESS_INFORMATION pi;
    ZeroMemory(&pi, sizeof(pi));
    if (!CreateProcessW(exe, cmd, NULL, NULL, FALSE, CREATE_NEW_PROCESS_GROUP, NULL, slash ? work : NULL, &si, &pi)) {
        Log_Warn("[racegame] %s %s could not be started: error %lu", exeUtf8, what, (unsigned long)GetLastError());
        return 0;
    }
    // (its window brought in front of the game's: started by the program in front, it may take the foreground)
    AllowSetForegroundWindow(pi.dwProcessId);
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    Log_Info("[racegame] %s %s started (pid %lu)", exeUtf8, what, (unsigned long)pi.dwProcessId);
    return 1;
}

int RaceUpdate_ClipboardImage(const unsigned char *rgb, int w, int h, const char *pngUtf8) {
    if (!rgb || w <= 0 || h <= 0) return 0;
    // (tests: LBA2_NO_CLIPBOARD set -- the clipboard is the person's at the machine; the PNG is saved all the same)
    const char *no = getenv("LBA2_NO_CLIPBOARD");
    if (no && *no && *no != '0') { Log_Info("[raceupdate] clipboard: a %d x %d picture -- left off it (LBA2_NO_CLIPBOARD)", w, h); return 1; }
    if (!OpenClipboard(NULL)) return 0;
    EmptyClipboard();
    int ok = 0;
    // the DIB: bottom row first, BGR, rows padded to four bytes
    const int stride = (w * 3 + 3) & ~3;
    HGLOBAL dib = GlobalAlloc(GMEM_MOVEABLE, sizeof(BITMAPINFOHEADER) + (SIZE_T)stride * h);
    if (dib) {
        BITMAPINFOHEADER *bi = (BITMAPINFOHEADER *)GlobalLock(dib);
        ZeroMemory(bi, sizeof(*bi));
        bi->biSize = sizeof(*bi); bi->biWidth = w; bi->biHeight = h; bi->biPlanes = 1; bi->biBitCount = 24; bi->biCompression = BI_RGB;
        bi->biSizeImage = (DWORD)stride * h;
        unsigned char *px = (unsigned char *)(bi + 1);
        for (int y = 0; y < h; y++) {
            const unsigned char *src = rgb + (size_t)(h - 1 - y) * w * 3;
            unsigned char *dst = px + (size_t)y * stride;
            for (int x = 0; x < w; x++) { dst[x * 3] = src[x * 3 + 2]; dst[x * 3 + 1] = src[x * 3 + 1]; dst[x * 3 + 2] = src[x * 3]; }
        }
        GlobalUnlock(dib);
        if (SetClipboardData(CF_DIB, dib)) ok = 1;
        else GlobalFree(dib);
    }
    // the PNG as it was saved
    wchar_t path[MAX_PATH * 4];
    if (pngUtf8 && pngUtf8[0] && Wide(pngUtf8, path, MAX_PATH * 4)) {
        HANDLE f = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL);
        if (f != INVALID_HANDLE_VALUE) {
            const DWORD size = GetFileSize(f, NULL);
            HGLOBAL png = size != INVALID_FILE_SIZE && size > 0 ? GlobalAlloc(GMEM_MOVEABLE, size) : NULL;
            if (png) {
                DWORD got = 0;
                void *p = GlobalLock(png);
                const BOOL read = ReadFile(f, p, size, &got, NULL);
                GlobalUnlock(png);
                const UINT format = RegisterClipboardFormatW(L"PNG");
                if (!read || got != size || !format || !SetClipboardData(format, png)) GlobalFree(png);
            }
            CloseHandle(f);
        }
    }
    CloseClipboard();
    Log_Info("[raceupdate] clipboard: a %d x %d picture%s", w, h, ok ? "" : " -- not put there");
    return ok;
}

#else

int RaceUpdate_ClipboardImage(const unsigned char *rgb, int w, int h, const char *pngUtf8) {
    (void)rgb; (void)w; (void)h; (void)pngUtf8;
    return 0;
}

int RaceUpdate_Exists(const char *pathUtf8) {
    (void)pathUtf8;
    return 0;
}

int RaceUpdate_Spawn(const char *exeUtf8, const char *installDirUtf8, int manual) {
    (void)exeUtf8;
    (void)installDirUtf8;
    (void)manual;
    return 0;
}

int RaceUpdate_Tool(const char *exeUtf8, const char *what, const char *folderUtf8) {
    (void)exeUtf8;
    (void)what;
    (void)folderUtf8;
    return 0;
}

#endif
