#include "MIDI_SDL.H"
#include "SAMPLE_SDL.H" /* Sample_Driver_Enabled: whether the game's audio is up at all */

#include <SDL3/SDL.h>

#include <ctype.h>
#include <math.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <mmsystem.h>
#endif

S32 InitMidiDriver(char *driver_name) {
    (void)driver_name;
    return TRUE;
}

/* ══════════════════════════════════════════════════════════════════════════
   A Standard MIDI File played as music (LBA Assembler, 2026-10-20; the user: "We should add support for midi ... The original music
   files in LBA1 are stored in XMIDI (.xmi) format inside compressed archive files (MIDI_MI.HQR and MIDI_SB.HQR)"). The race track's build
   writes LBA1's temple music as a MIDI file when the player's LBA1 has no CD music (LBA Assembler's Terrain/Temple/TempleMusicMidi.cs:
   MIDI_MI.HQR's score, converted), and the race file names it (music=, RACEMOD.CPP). STREAM.CPP hands a .mid name here, so it plays as
   any music does: kept going (looped) while the game wants it to, paused and resumed with the game, stopped with it, at the music's
   volume.

   The sequencer: the file read whole (format 0 or 1, its tracks merged, its tempo map worked into each event's time in microseconds,
   SMPTE divisions too), then a thread of its own sends each channel message when its time comes (SDL_GetTicksNS, the wait
   SDL_DelayPrecise: to the millisecond, at most 5 ms at a time so a stop or pause is heard at once) through WinMM's midiOut -- MIDI_MAPPER,
   Windows' General MIDI synth (the Microsoft GS Wavetable Synth: what LBA1 Classic plays its MIDI music through on Windows). Its
   volume: each channel's volume controller (CC7: the file's own value, 100 until it sets one) times sqrt(volume / 127) -- the synth's
   CC7 is a square law, so the music sounds at the same level a WAV would at that volume (STREAM.CPP's gain is volume / 127). Paused,
   stopped, or at the end of a loop: every channel's notes off (CC123) and sound off (CC120).

   The device is NEVER opened while the game's audio is off (--no-audio, --headless, no audio device: Sample_Driver_Enabled), while SDL's
   audio goes somewhere other than the speakers (the "disk" and "dummy" drivers a test records or silences the game with -- the synth would
   still sound), while the music's volume is 0, in a dump (LBA2_MIDI_DUMP), or with LBA2_MIDI_DEVICE=off: the sequencer runs all the same
   (its time, its loop, its end) and says once why nothing sounds. A device opened later (the volume raised from 0) is first given each
   channel's program, controllers and pitch bend as the music has them by then.

   LBA2_MIDI_DUMP=<file>: every message the sequencer would send written to that file with its time (since the music started, and its
   place in the music) instead of to a device -- for tests (LBA2_MIDI_DUMP_MAX lines, 4000 by default). Other platforms: no MIDI output
   (MidiOutputAvailable: the race's music= falls back to its scenes' own music, RACEMOD.CPP).
   ══════════════════════════════════════════════════════════════════════════ */

typedef struct {
    Uint64 us;   // when, from the start of the music (microseconds)
    Uint8 m[3];  // the channel message
    Uint8 len;   // its length: 2 or 3
} MidiEvent;

static SDL_Mutex *midiLock = NULL;
static SDL_Thread *midiThread = NULL;
static SDL_AtomicInt midiQuit;

static MidiEvent *midiEv = NULL;
static int midiEvCount = 0;
static Uint64 midiLengthUs = 0;
static char midiPath[512] = "";

static bool midiActive = false;    // a file loaded and started (until StopMidi)
static bool midiPaused = false;    // PauseMidi (the game's)
static bool midiSuspended = false; // SuspendMidiOutput (the window's focus)
static bool midiEnded = false;     // played to its end with looping off
static bool midiLoop = false;
static Uint64 midiOriginNs = 0; // SDL_GetTicksNS() at the music's start, while it runs
static Uint64 midiHeldUs = 0;   // where it is while held (paused, suspended)
static int midiNext = 0;
static int midiLoops = 0;
static S32 midiVolume = 127;
static Uint64 midiStartedNs = 0; // (the dump's clock)

// each channel as the music has it so far: for a device opened in the middle (-1: never set)
static Sint16 chProgram[16];
static Sint16 chCC[16][128];
static Sint16 chBend[16][2];
static Sint16 chPressure[16];

#ifdef _WIN32
static HMIDIOUT midiDev = NULL;
#endif
static bool midiDevOpen = false;
static bool midiExiting = false;
static char midiWhyNot[160] = ""; // why the device isn't open (said once)

// the dump (LBA2_MIDI_DUMP): its lines kept in memory as the sequencer sends -- no file written while it keeps time -- and appended to the
// file when the music pauses or stops
static bool midiDump = false;
static char midiDumpFile[512] = "";
static char *midiDumpBuf = NULL;
static size_t midiDumpLen = 0, midiDumpCap = 0;
static int midiDumpLines = 0;
static int midiDumpMax = 4000;

/* ── Output ───────────────────────────────────────────────────────────── */

static const char *DumpPath(void) {
    const char *p = SDL_getenv("LBA2_MIDI_DUMP");
    return (p && p[0]) ? p : NULL;
}

static void DumpLine(const char *fmt, ...) {
    char line[512];
    va_list args;
    va_start(args, fmt);
    const int n = vsnprintf(line, sizeof(line), fmt, args);
    va_end(args);
    if (n <= 0) return;
    const size_t len = (size_t)n < sizeof(line) ? (size_t)n : sizeof(line) - 1;
    if (midiDumpLen + len + 1 > midiDumpCap) {
        const size_t cap = (midiDumpCap ? midiDumpCap * 2 : 65536) + len;
        char *grown = (char *)realloc(midiDumpBuf, cap);
        if (!grown) return;
        midiDumpBuf = grown;
        midiDumpCap = cap;
    }
    memcpy(midiDumpBuf + midiDumpLen, line, len);
    midiDumpLen += len;
}

static void DumpFlush(void) {
    if (!midiDumpLen || !midiDumpFile[0]) return;
    if (FILE *f = fopen(midiDumpFile, "ab")) {
        fwrite(midiDumpBuf, 1, midiDumpLen, f);
        fclose(f);
    }
    midiDumpLen = 0;
}

static Uint64 MidiPosUs(void) {
    if (!midiActive)
        return 0;
    if (midiPaused || midiSuspended || midiEnded)
        return midiHeldUs;
    const Uint64 now = SDL_GetTicksNS();
    return now > midiOriginNs ? (now - midiOriginNs) / 1000 : 0;
}

static void Send(Uint8 status, Uint8 d1, Uint8 d2, int len, const char *why) {
    if (midiDump && midiDumpLines < midiDumpMax) {
        const double wall = (SDL_GetTicksNS() - midiStartedNs) / 1e6;
        if (len == 2)
            DumpLine("%11.3f ms  at %11.3f ms  %02X %02X   %s\n", wall, MidiPosUs() / 1e3, status, d1, why ? why : "");
        else
            DumpLine("%11.3f ms  at %11.3f ms  %02X %02X %02X%s%s\n", wall, MidiPosUs() / 1e3, status, d1, d2, why ? "  " : "", why ? why : "");
        if (++midiDumpLines == midiDumpMax)
            DumpLine("(LBA2_MIDI_DUMP_MAX: %d lines -- no more written)\n", midiDumpMax);
    }
#ifdef _WIN32
    if (midiDevOpen && midiDev)
        midiOutShortMsg(midiDev, (DWORD)status | ((DWORD)d1 << 8) | (len == 3 ? ((DWORD)d2 << 16) : 0));
#endif
}

static Uint8 ScaledVolume(int fileValue) {
    if (midiVolume <= 0)
        return 0;
    double v = fileValue * sqrt((midiVolume > 127 ? 127 : midiVolume) / 127.0);
    return (Uint8)(v > 127 ? 127 : (int)(v + 0.5));
}

static void SendVolumes(const char *why) {
    for (int ch = 0; ch < 16; ch++)
        Send((Uint8)(0xB0 | ch), 7, ScaledVolume(chCC[ch][7] >= 0 ? chCC[ch][7] : 100), 3, ch == 0 ? why : NULL);
}

static void NotesOff(const char *why) {
    for (int ch = 0; ch < 16; ch++) {
        Send((Uint8)(0xB0 | ch), 123, 0, 3, ch == 0 ? why : NULL); // all notes off
        Send((Uint8)(0xB0 | ch), 120, 0, 3, NULL);                 // all sound off
    }
}

// each channel's state as the music has it so far: bank, program, controllers, pitch bend, pressure -- then the volumes
static void SendChase(const char *why) {
    bool said = false;
    for (int ch = 0; ch < 16; ch++) {
        const Uint8 cc = (Uint8)(0xB0 | ch);
        if (chCC[ch][0] >= 0) Send(cc, 0, (Uint8)chCC[ch][0], 3, said ? NULL : why), said = true;
        if (chCC[ch][32] >= 0) Send(cc, 32, (Uint8)chCC[ch][32], 3, said ? NULL : why), said = true;
        if (chProgram[ch] >= 0) Send((Uint8)(0xC0 | ch), (Uint8)chProgram[ch], 0, 2, said ? NULL : why), said = true;
        for (int c = 1; c < 120; c++)
            if (c != 7 && c != 32 && chCC[ch][c] >= 0) Send(cc, (Uint8)c, (Uint8)chCC[ch][c], 3, said ? NULL : why), said = true;
        if (chBend[ch][0] >= 0) Send((Uint8)(0xE0 | ch), (Uint8)chBend[ch][0], (Uint8)chBend[ch][1], 3, NULL);
        if (chPressure[ch] >= 0) Send((Uint8)(0xD0 | ch), (Uint8)chPressure[ch], 0, 2, NULL);
    }
    SendVolumes(said ? NULL : why);
}

// one of the music's events: kept as the channel's state, and sent (a note only when `sound`)
static void Emit(const MidiEvent *e, bool sound) {
    const Uint8 kind = e->m[0] & 0xF0, ch = e->m[0] & 0x0F;
    switch (kind) {
    case 0xB0:
        if (e->m[1] < 128)
            chCC[ch][e->m[1]] = e->m[2];
        if (e->m[1] == 7) {
            Send(e->m[0], 7, ScaledVolume(e->m[2]), 3, NULL);
            return;
        }
        break;
    case 0xC0: chProgram[ch] = e->m[1]; break;
    case 0xE0: chBend[ch][0] = e->m[1]; chBend[ch][1] = e->m[2]; break;
    case 0xD0: chPressure[ch] = e->m[1]; break;
    case 0x80: case 0x90: case 0xA0:
        if (!sound)
            return;
        break;
    }
    Send(e->m[0], e->m[1], e->m[2], e->len, NULL);
}

static void ResetChannels(void) {
    for (int ch = 0; ch < 16; ch++) {
        chProgram[ch] = -1;
        chBend[ch][0] = chBend[ch][1] = -1;
        chPressure[ch] = -1;
        for (int c = 0; c < 128; c++)
            chCC[ch][c] = -1;
    }
}

/* ── The device ───────────────────────────────────────────────────────── */

S32 MidiOutputAvailable(void) {
#ifdef _WIN32
    return TRUE;
#else
    return FALSE;
#endif
}

// whether a device may be opened now -- else why not
static const char *DeviceRefused(void) {
    const char *dev = SDL_getenv("LBA2_MIDI_DEVICE");
    if (dev && (!SDL_strcasecmp(dev, "off") || !strcmp(dev, "0") || !SDL_strcasecmp(dev, "none")))
        return "LBA2_MIDI_DEVICE=off";
    if (!Sample_Driver_Enabled)
        return "the game's audio is off (--no-audio, --headless or no audio device)";
    const char *driver = SDL_GetCurrentAudioDriver();
    if (driver && (!SDL_strcasecmp(driver, "disk") || !SDL_strcasecmp(driver, "dummy")))
        return SDL_strcasecmp(driver, "disk") ? "SDL's audio driver is \"dummy\": the game is silent, the synth would not be"
                                              : "SDL's audio driver is \"disk\": the game's sound goes to a file, the synth's would not";
    if (midiVolume <= 0)
        return "the music's volume is 0";
    if (!MidiOutputAvailable())
        return "no MIDI output on this platform";
    if (DumpPath())
        return "LBA2_MIDI_DUMP: its messages written to a file instead";
    return NULL;
}

// the device opened when it may be and the music runs (and given the music's state so far); said once why not
static void EnsureDevice(const char *when) {
    if (midiDevOpen || !midiActive || midiPaused || midiSuspended)
        return; // (held: opened as it resumes)
    const char *why = DeviceRefused();
    if (why) {
        if (strcmp(midiWhyNot, why) != 0) {
            snprintf(midiWhyNot, sizeof(midiWhyNot), "%s", why);
            SDL_Log("[MIDI] the MIDI output device was not opened: %s -- the music runs silently", why);
        }
        return;
    }
#ifdef _WIN32
    HMIDIOUT dev = NULL;
    const MMRESULT r = midiOutOpen(&dev, MIDI_MAPPER, 0, 0, CALLBACK_NULL);
    if (r != MMSYSERR_NOERROR || !dev) {
        snprintf(midiWhyNot, sizeof(midiWhyNot), "midiOutOpen failed (%u)", (unsigned)r);
        SDL_Log("[MIDI] the MIDI output device could not be opened (midiOutOpen: %u) -- the music runs silently", (unsigned)r);
        return;
    }
    midiDev = dev;
    midiDevOpen = true;
    midiWhyNot[0] = '\0';
    MIDIOUTCAPSA caps;
    const char *name = midiOutGetDevCapsA(MIDI_MAPPER, &caps, sizeof(caps)) == MMSYSERR_NOERROR ? caps.szPname : "?";
    SDL_Log("[MIDI] MIDI output device opened (%s, %s) at %.3f s of the music", name, when, MidiPosUs() / 1e6);
    SendChase("chase: the music's state so far");
#else
    (void)when;
#endif
}

static void CloseDevice(void) {
#ifdef _WIN32
    if (midiDevOpen && midiDev) {
        midiOutReset(midiDev); // (every note off on every channel)
        midiOutClose(midiDev);
        if (!midiExiting) SDL_Log("[MIDI] MIDI output device closed");
    }
    midiDev = NULL;
#endif
    midiDevOpen = false;
}

/* ── The file ─────────────────────────────────────────────────────────── */

typedef struct {
    Uint64 tick;
    int track, order;
    Uint8 m[3], len;
} RawEvent;

typedef struct {
    Uint64 tick;
    Uint32 usPerQuarter;
} TempoChange;

// ticks to microseconds, asked in order: SMPTE (frames a second x ticks a frame), or the tempo map over ticks a quarter note
typedef struct {
    bool smpte;
    double smpteUsPerTick;
    int ppq;
    const TempoChange *tempo;
    int tempoCount, next;
    double atUs; // the time at atTick
    Uint64 atTick;
    double usPerTick;
} TickClock;

static double TimeOf(TickClock *c, Uint64 tick) {
    if (c->smpte) return tick * c->smpteUsPerTick;
    while (c->next < c->tempoCount && c->tempo[c->next].tick <= tick) {
        c->atUs += (c->tempo[c->next].tick - c->atTick) * c->usPerTick;
        c->atTick = c->tempo[c->next].tick;
        c->usPerTick = (double)c->tempo[c->next].usPerQuarter / c->ppq;
        c->next++;
    }
    return c->atUs + (tick - c->atTick) * c->usPerTick;
}

static int CompareRaw(const void *a, const void *b) {
    const RawEvent *x = (const RawEvent *)a, *y = (const RawEvent *)b;
    if (x->tick != y->tick) return x->tick < y->tick ? -1 : 1;
    if (x->track != y->track) return x->track < y->track ? -1 : 1;
    return x->order < y->order ? -1 : x->order > y->order;
}

static int CompareTempo(const void *a, const void *b) {
    const TempoChange *x = (const TempoChange *)a, *y = (const TempoChange *)b;
    return x->tick < y->tick ? -1 : x->tick > y->tick;
}

static Uint8 *ReadWhole(const char *path, size_t *size) {
    FILE *f = fopen(path, "rb");
    if (!f) return NULL;
    Uint8 *data = NULL;
    if (fseek(f, 0, SEEK_END) == 0) {
        const long n = ftell(f);
        if (n > 0 && n < (64L << 20) && fseek(f, 0, SEEK_SET) == 0) {
            data = (Uint8 *)malloc((size_t)n);
            if (data && fread(data, 1, (size_t)n, f) != (size_t)n) {
                free(data);
                data = NULL;
            }
            *size = (size_t)n;
        }
    }
    fclose(f);
    return data;
}

// The file's channel messages in time order, each with its time in microseconds; its length. False: not a MIDI file this reads.
static bool ParseSmf(const Uint8 *d, size_t size, MidiEvent **outEv, int *outCount, Uint64 *outLength, char *info, size_t infoMax) {
    if (size < 14 || memcmp(d, "MThd", 4) != 0) return false;
    const Uint32 hlen = (Uint32)d[4] << 24 | (Uint32)d[5] << 16 | (Uint32)d[6] << 8 | d[7];
    if (hlen < 6 || 8 + (size_t)hlen > size) return false;
    const int format = d[8] << 8 | d[9];
    const int tracks = d[10] << 8 | d[11];
    const int division = d[12] << 8 | d[13];
    if (format > 2 || division == 0) return false;

    RawEvent *raw = NULL;
    int rawCount = 0, rawCap = 0;
    TempoChange *tempo = NULL;
    int tempoCount = 0, tempoCap = 0;
    Uint64 endTick = 0;
    int sysex = 0, metas = 0, tracksRead = 0;
    size_t p = 8 + hlen;
    bool ok = true;
    for (int t = 0; ok && p + 8 <= size; t++) {
        const Uint32 len = (Uint32)d[p + 4] << 24 | (Uint32)d[p + 5] << 16 | (Uint32)d[p + 6] << 8 | d[p + 7];
        const size_t start = p + 8;
        const size_t stop = start + len > size ? size : start + len;
        const bool isTrack = memcmp(d + p, "MTrk", 4) == 0;
        p = stop;
        if (!isTrack) continue;
        tracksRead++;
        size_t q = start;
        Uint64 tick = 0;
        Uint8 status = 0;
        int order = 0;
        while (q < stop) {
            Uint32 delta = 0;
            for (int i = 0; i < 4 && q < stop; i++) {
                const Uint8 b = d[q++];
                delta = delta << 7 | (b & 0x7F);
                if (!(b & 0x80)) break;
            }
            tick += delta;
            if (q >= stop) break;
            if (d[q] & 0x80) status = d[q++];
            else if (status == 0 || status >= 0xF0) { ok = false; break; } // (running status only for channel messages)
            if (status == 0xFF) {
                if (q >= stop) break;
                const Uint8 type = d[q++];
                Uint32 mlen = 0;
                for (int i = 0; i < 4 && q < stop; i++) {
                    const Uint8 b = d[q++];
                    mlen = mlen << 7 | (b & 0x7F);
                    if (!(b & 0x80)) break;
                }
                if (type == 0x51 && mlen == 3 && q + 3 <= stop) {
                    if (tempoCount == tempoCap) {
                        tempoCap = tempoCap ? tempoCap * 2 : 16;
                        tempo = (TempoChange *)realloc(tempo, sizeof(TempoChange) * (size_t)tempoCap);
                        if (!tempo) { ok = false; break; }
                    }
                    tempo[tempoCount].tick = tick;
                    tempo[tempoCount].usPerQuarter = (Uint32)d[q] << 16 | (Uint32)d[q + 1] << 8 | d[q + 2];
                    tempoCount++;
                }
                metas++;
                q += mlen;
                status = 0; // (a meta event cancels running status)
                if (type == 0x2F) break;
                continue;
            }
            if (status == 0xF0 || status == 0xF7) {
                Uint32 slen = 0;
                for (int i = 0; i < 4 && q < stop; i++) {
                    const Uint8 b = d[q++];
                    slen = slen << 7 | (b & 0x7F);
                    if (!(b & 0x80)) break;
                }
                q += slen;
                sysex++;
                status = 0;
                continue;
            }
            const Uint8 kind = status & 0xF0;
            const int n = (kind == 0xC0 || kind == 0xD0) ? 1 : 2;
            if (q + (size_t)n > stop) break;
            if (rawCount == rawCap) {
                rawCap = rawCap ? rawCap * 2 : 1024;
                RawEvent *grown = (RawEvent *)realloc(raw, sizeof(RawEvent) * (size_t)rawCap);
                if (!grown) { ok = false; break; }
                raw = grown;
            }
            RawEvent *e = &raw[rawCount++];
            e->tick = tick;
            e->track = t;
            e->order = order++;
            e->m[0] = status;
            e->m[1] = d[q] & 0x7F;
            e->m[2] = n == 2 ? (d[q + 1] & 0x7F) : 0;
            e->len = (Uint8)(n + 1);
            q += (size_t)n;
        }
        if (tick > endTick) endTick = tick;
    }
    if (!ok || tracksRead == 0) {
        free(raw);
        free(tempo);
        return false;
    }
    qsort(raw, (size_t)rawCount, sizeof(RawEvent), CompareRaw);
    if (tempoCount > 1) qsort(tempo, (size_t)tempoCount, sizeof(TempoChange), CompareTempo);

    // ticks to microseconds: SMPTE (frames a second x ticks a frame) or the tempo map over ticks a quarter note
    MidiEvent *ev = (MidiEvent *)malloc(sizeof(MidiEvent) * (size_t)(rawCount > 0 ? rawCount : 1));
    if (!ev) {
        free(raw);
        free(tempo);
        return false;
    }
    const bool smpte = (division & 0x8000) != 0;
    const double smpteUsPerTick = smpte ? 1e6 / ((double)(-(Sint8)(division >> 8)) * (division & 0xFF)) : 0;
    const int ppq = smpte ? 1 : division;
    TickClock clock;
    clock.smpte = smpte;
    clock.smpteUsPerTick = smpteUsPerTick;
    clock.ppq = ppq;
    clock.tempo = tempo;
    clock.tempoCount = tempoCount;
    clock.next = 0;
    clock.atUs = 0;
    clock.atTick = 0;
    clock.usPerTick = 500000.0 / ppq; // (120 beats a minute until the file says otherwise)
    int notes = 0;
    for (int i = 0; i < rawCount; i++) {
        ev[i].us = (Uint64)(TimeOf(&clock, raw[i].tick) + 0.5);
        memcpy(ev[i].m, raw[i].m, 3);
        ev[i].len = raw[i].len;
        if ((raw[i].m[0] & 0xF0) == 0x90 && raw[i].m[2] > 0) notes++;
    }
    const Uint64 lengthUs = (Uint64)(TimeOf(&clock, endTick) + 0.5);
    snprintf(info, infoMax, "format %d, %d track%s, %s %d, %d events (%d notes), %d tempo change%s, %d sysex left out, %.3f s long",
             format, tracksRead, tracksRead == 1 ? "" : "s", smpte ? "SMPTE" : "ticks a quarter note", smpte ? (division & 0xFF) : division,
             rawCount, notes, tempoCount, tempoCount == 1 ? "" : "s", sysex, lengthUs / 1e6);
    (void)tracks;
    (void)metas;
    free(raw);
    free(tempo);
    *outEv = ev;
    *outCount = rawCount;
    *outLength = lengthUs;
    return true;
}

/* ── The sequencer ────────────────────────────────────────────────────── */

#define MIDI_WAIT_MAX_NS (5ull * 1000000ull) // (a stop, pause or volume heard within this)

static int SDLCALL MidiThreadFunc(void *unused) {
    (void)unused;
    // (its timing before the game's frames: an audio thread's priority, as SDL gives its own)
    SDL_SetCurrentThreadPriority(SDL_THREAD_PRIORITY_HIGH);
    while (!SDL_GetAtomicInt(&midiQuit)) {
        Uint64 waitNs = MIDI_WAIT_MAX_NS;
        SDL_LockMutex(midiLock);
        if (midiActive && !midiPaused && !midiSuspended && !midiEnded) {
            Uint64 pos = MidiPosUs();
            while (midiNext < midiEvCount && midiEv[midiNext].us <= pos)
                Emit(&midiEv[midiNext++], true);
            if (midiNext >= midiEvCount && pos >= midiLengthUs) {
                if (midiLoop && midiLengthUs >= 100000) {
                    // played again from its start: its time goes on from where its end is
                    NotesOff("loop: the music played again from its start");
                    midiOriginNs += midiLengthUs * 1000;
                    midiNext = 0;
                    midiLoops++;
                    SDL_Log("[MIDI] %s played again from its start (%d times now)", midiPath, midiLoops);
                    waitNs = 0;
                } else {
                    midiHeldUs = midiLengthUs;
                    midiEnded = true;
                    NotesOff("the end of the music");
                    SDL_Log("[MIDI] %s played to its end (%.3f s)", midiPath, midiLengthUs / 1e6);
                }
            } else {
                const Uint64 due = midiNext < midiEvCount ? midiEv[midiNext].us : midiLengthUs;
                const Uint64 ns = due > pos ? (due - pos) * 1000 : 0;
                waitNs = ns < waitNs ? ns : waitNs;
            }
        }
        SDL_UnlockMutex(midiLock);
        if (waitNs > 0)
            SDL_DelayPrecise(waitNs);
    }
    return 0;
}

static void StopThread(void) {
    if (midiThread) {
        SDL_SetAtomicInt(&midiQuit, 1);
        SDL_WaitThread(midiThread, NULL);
        midiThread = NULL;
    }
    SDL_SetAtomicInt(&midiQuit, 0);
}

static void MidiAtExit(void) {
    midiExiting = true; // (SDL may be shut down by now: nothing logged)
    StopMidi();
}

void StopMidi(void) {
    StopThread();
    if (midiLock) SDL_LockMutex(midiLock);
    if (midiActive) {
        NotesOff("stop");
        if (!midiExiting) SDL_Log("[MIDI] %s stopped at %.3f s (played again %d times)", midiPath, MidiPosUs() / 1e6, midiLoops);
    }
    CloseDevice();
    free(midiEv);
    midiEv = NULL;
    midiEvCount = 0;
    midiLengthUs = 0;
    midiActive = midiPaused = midiSuspended = midiEnded = false;
    midiNext = 0;
    midiLoops = 0;
    midiPath[0] = '\0';
    midiWhyNot[0] = '\0';
    if (midiDump) {
        DumpFlush();
        free(midiDumpBuf);
        midiDumpBuf = NULL;
        midiDumpCap = 0;
        midiDump = false;
    }
    if (midiLock) SDL_UnlockMutex(midiLock);
}

S32 IsMidiFileName(const char *path) {
    if (!path) return FALSE;
    const char *dot = strrchr(path, '.');
    if (!dot) return FALSE;
    return !SDL_strcasecmp(dot, ".mid") || !SDL_strcasecmp(dot, ".midi") || !SDL_strcasecmp(dot, ".smf");
}

S32 PlayMidiFile(const char *path, S32 loop, S32 volume, S32 startMs) {
    static bool exitHooked = false;
    StopMidi();
    if (!path || !path[0]) return FALSE;
    size_t size = 0;
    Uint8 *data = ReadWhole(path, &size);
    if (!data) {
        SDL_Log("[MIDI] %s: could not be read", path);
        return FALSE;
    }
    MidiEvent *ev = NULL;
    int count = 0;
    Uint64 length = 0;
    char info[256] = "";
    const bool parsed = ParseSmf(data, size, &ev, &count, &length, info, sizeof(info));
    free(data);
    if (!parsed) {
        SDL_Log("[MIDI] %s: not a MIDI file this reads", path);
        return FALSE;
    }
    if (!midiLock) midiLock = SDL_CreateMutex();
    if (!exitHooked) {
        atexit(MidiAtExit); // (no note left sounding behind the game)
        exitHooked = true;
    }

    SDL_LockMutex(midiLock);
    midiEv = ev;
    midiEvCount = count;
    midiLengthUs = length;
    snprintf(midiPath, sizeof(midiPath), "%s", path);
    midiLoop = loop != 0;
    midiVolume = volume;
    midiLoops = 0;
    ResetChannels();
    midiStartedNs = SDL_GetTicksNS();
    // (started in the middle -- a resume after other music played: the channels' state to there, no notes)
    Uint64 startUs = startMs > 0 ? (Uint64)startMs * 1000 : 0;
    if (midiLengthUs > 0 && startUs >= midiLengthUs) startUs %= midiLengthUs;
    midiNext = 0;
    while (midiNext < midiEvCount && midiEv[midiNext].us < startUs)
        Emit(&midiEv[midiNext++], false);
    midiOriginNs = midiStartedNs - startUs * 1000;
    midiHeldUs = startUs;
    midiActive = true;
    midiPaused = midiSuspended = midiEnded = false;
    midiWhyNot[0] = '\0';
    SDL_Log("[MIDI] %s: %s; %s, at volume %d%s", path, info, midiLoop ? "looped (kept going)" : "played once",
            (int)volume, startUs ? " -- from where it was" : "");
    if (const char *dump = DumpPath()) {
        const char *max = SDL_getenv("LBA2_MIDI_DUMP_MAX");
        midiDumpMax = (max && atoi(max) > 0) ? atoi(max) : 4000;
        midiDumpLines = 0;
        midiDumpLen = 0;
        snprintf(midiDumpFile, sizeof(midiDumpFile), "%s", dump);
        midiDump = true;
        DumpLine("# %s: %s; %s, volume %d, from %.3f s\n#   wall clock (since PlayMidiFile)   place in the music   message\n",
                 path, info, midiLoop ? "looped" : "once", (int)volume, startUs / 1e6);
        SDL_Log("[MIDI] its messages written to %s (LBA2_MIDI_DUMP) as the music pauses or stops, not sent to a device", dump);
        SendChase("start: volumes (and the state to where it starts)");
    }
    EnsureDevice("the music starting");
    SDL_SetAtomicInt(&midiQuit, 0);
    midiThread = SDL_CreateThread(MidiThreadFunc, "MIDISequencer", NULL);
    if (!midiThread)
        SDL_Log("[MIDI] could not start the sequencer's thread: %s", SDL_GetError());
    SDL_UnlockMutex(midiLock);
    return midiThread != NULL;
}

// (held: its place kept, every note off)
static void Hold(const char *why) {
    midiHeldUs = MidiPosUs();
    NotesOff(why);
}

static void Release(void) {
    midiOriginNs = SDL_GetTicksNS() - midiHeldUs * 1000;
}

void PauseMidi(void) {
    if (!midiLock) return;
    SDL_LockMutex(midiLock);
    if (midiActive && !midiPaused) {
        if (!midiSuspended && !midiEnded) Hold("pause");
        midiPaused = true;
        SDL_Log("[MIDI] paused at %.3f s", midiHeldUs / 1e6);
        if (midiDump) DumpFlush();
    }
    SDL_UnlockMutex(midiLock);
}

void ResumeMidi(void) {
    if (!midiLock) return;
    SDL_LockMutex(midiLock);
    if (midiActive && midiPaused) {
        midiPaused = false;
        if (!midiSuspended && !midiEnded) Release();
        SDL_Log("[MIDI] resumed at %.3f s", midiHeldUs / 1e6);
        EnsureDevice("resumed");
    }
    SDL_UnlockMutex(midiLock);
}

void SuspendMidiOutput(void) {
    if (!midiLock) return;
    SDL_LockMutex(midiLock);
    if (midiActive && !midiSuspended) {
        if (!midiPaused && !midiEnded) Hold("the window lost the focus");
        midiSuspended = true;
    }
    SDL_UnlockMutex(midiLock);
}

void ResumeMidiOutput(void) {
    if (!midiLock) return;
    SDL_LockMutex(midiLock);
    if (midiActive && midiSuspended) {
        midiSuspended = false;
        if (!midiPaused && !midiEnded) Release();
        EnsureDevice("the window has the focus again");
    }
    SDL_UnlockMutex(midiLock);
}

void SetMidiVolume(S32 volume) {
    if (!midiLock) {
        midiVolume = volume;
        return;
    }
    SDL_LockMutex(midiLock);
    if (volume != midiVolume) {
        midiVolume = volume;
        if (midiActive) {
            SendVolumes("the music's volume");
            if (volume > 0) EnsureDevice("the music's volume raised");
        }
    }
    SDL_UnlockMutex(midiLock);
}

void SetMidiLoop(S32 on) {
    if (!midiLock) return;
    SDL_LockMutex(midiLock);
    midiLoop = on != 0;
    SDL_UnlockMutex(midiLock);
}

S32 IsMidiPlaying(void) {
    if (!midiLock) return FALSE;
    SDL_LockMutex(midiLock);
    const S32 playing = midiActive && !midiPaused && !midiEnded;
    SDL_UnlockMutex(midiLock);
    return playing;
}

S32 HasMidiEnded(void) {
    if (!midiLock) return FALSE;
    SDL_LockMutex(midiLock);
    const S32 ended = midiActive && midiEnded;
    SDL_UnlockMutex(midiLock);
    return ended;
}

S32 MidiPositionMs(void) {
    if (!midiLock) return 0;
    SDL_LockMutex(midiLock);
    const S32 ms = (S32)(MidiPosUs() / 1000);
    SDL_UnlockMutex(midiLock);
    return ms;
}

S32 MidiDeviceOpen(void) {
    return midiDevOpen ? TRUE : FALSE;
}
