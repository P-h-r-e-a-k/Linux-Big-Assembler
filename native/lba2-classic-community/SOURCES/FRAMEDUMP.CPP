// FRAMEDUMP.CPP -- a video of the game, recorded from inside it (2026-10-26, the user: "Record an interesting video of game
// footage with clips from all tracks including showing off power ups to be uploaded to YouTube").
//
// `framedump start <video.mp4> [seconds] [fps]` pipes every presented frame, as the engine hands it to the window (before the
// console's overlay), into ffmpeg (LBA2_FFMPEG, else ffmpeg on the PATH) and the effects' mix beside it into <video.mp4>.wav.
// Each frame is placed by the engine's clock -- the fixed-dt clock, armed for the dump (16 ms a tick) unless a --fixed-dt run
// armed it already: the game's own time, however slowly the frames are written: frames repeated or left out to hold `fps`, and the clock's span of the mix (AIL Sample_CaptureMix) written with
// each, so picture and sound keep step. LBA2_AUDIO_CAPTURE=1 gives the sound device silence (nothing heard on the machine).
// Nothing of the screen is read: only the engine's own frame.

#include "FRAMEDUMP.H"

#include <AIL/SAMPLE.H>
#include "CONSOLE/CONSOLE.H"

#include <SYSTEM/LOG.H>
#include <SYSTEM/TIMER.H>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#define FD_POPEN _popen
#define FD_PCLOSE _pclose
#else
#define FD_POPEN popen
#define FD_PCLOSE pclose
#endif

static FILE *s_video = NULL;       // ffmpeg's stdin
static FILE *s_wav = NULL;
static char s_path[1024];
static U32 s_w = 0, s_h = 0;       // the frame size the pipe was opened for
static S32 s_fps = 60;
static S32 s_seconds = 0;          // 0: until `framedump stop`
static U32 s_startMs = 0;
static S32 s_started = 0;          // the first frame seen (the clock's start taken then)
static U32 s_frames = 0;           // frames written to the pipe
static U32 s_samples = 0;          // stereo frames written to the WAV
static U8 *s_row = NULL;           // one frame, packed (the staging buffer has its own pitch)
static int s_armedDt = 0;          // the fixed-dt clock armed by the dump (and let go at its end)
#define FD_STEP_MS 16              // the game's step while recording: the clock moves 16 ms a tick, however slow the frames

// `framedump at <seconds> <command>`: console commands run at a time of the recording (a power-up used on its frame)
#define FD_AT_MAX 64
static struct { U32 ms; char cmd[200]; } s_at[FD_AT_MAX];
static int s_nbAt = 0;

int FrameDump_At(double seconds, const char *cmd) {
    if (s_nbAt >= FD_AT_MAX) return 0;
    s_at[s_nbAt].ms = (U32)(seconds * 1000 + 0.5);
    snprintf(s_at[s_nbAt].cmd, sizeof(s_at[s_nbAt].cmd), "%s", cmd);
    s_nbAt++;
    return 1;
}

static void WavHeader(FILE *f, U32 frames) {
    const U32 rate = (U32)Sample_CaptureRate(), bytes = frames * 4;
    U8 h[44];
    memcpy(h, "RIFF", 4);
    const U32 riff = 36 + bytes;
    memcpy(h + 4, &riff, 4);
    memcpy(h + 8, "WAVEfmt ", 8);
    const U32 fmtLen = 16;
    const U16 pcm = 1, ch = 2, bits = 16, align = 4;
    const U32 byteRate = rate * 4;
    memcpy(h + 16, &fmtLen, 4);
    memcpy(h + 20, &pcm, 2);
    memcpy(h + 22, &ch, 2);
    memcpy(h + 24, &rate, 4);
    memcpy(h + 28, &byteRate, 4);
    memcpy(h + 32, &align, 2);
    memcpy(h + 34, &bits, 2);
    memcpy(h + 36, "data", 4);
    memcpy(h + 40, &bytes, 4);
    fseek(f, 0, SEEK_SET);
    fwrite(h, 1, sizeof(h), f);
    fseek(f, 0, SEEK_END);
}

int FrameDump_Start(const char *path, int seconds, int fps, int wallClock) {
    if (s_video || s_wav) FrameDump_Stop();
    snprintf(s_path, sizeof(s_path), "%s", path);
    s_seconds = seconds > 0 ? seconds : 0;
    s_fps = fps > 0 ? fps : 60;
    s_started = 0;
    s_frames = s_samples = 0;
    s_w = s_h = 0;
    char wav[1100];
    snprintf(wav, sizeof(wav), "%s.wav", path);
    s_wav = fopen(wav, "wb");
    if (!s_wav) {
        Log_Error("[framedump] can't write %s", wav);
        return 0;
    }
    WavHeader(s_wav, 0);
    // the game's own time from here: a tick a step (the tick hook advances a clock armed mid-session, as `rec start` arms it)
    s_armedDt = 0;
    // (wallClock: the clock left as it is -- a menu, whose own loop never runs the tick hook that moves an armed clock: it stood
    // still and the dump took one frame)
    if (!wallClock && !Timer_FixedDtActive()) {
        // (arming zeroes the game's clock, for a harness boot's sake: kept here -- a race's start, its laps, every timer
        // set by it, are times on that clock, and a race on the grid waited 27 s for its countdown)
        const U32 ref = (U32)TimerRefHR;
        Timer_EnableFixedDt(FD_STEP_MS);
        SetTimerHR(ref);
        s_armedDt = 1;
    }
    Sample_CaptureBegin();
    Log_Info("[framedump] waiting for the next frame: %s, %d s, %d fps", path, s_seconds, s_fps);
    return 1;
}

static int OpenVideo(U32 w, U32 h) {
    const char *ff = getenv("LBA2_FFMPEG");
    char cmd[2600];
    // (cmd.exe /c takes the whole line's outer quotes off: the line wrapped in a pair of its own)
    snprintf(cmd, sizeof(cmd),
             "\"\"%s\" -y -loglevel error -f rawvideo -pix_fmt bgra -s %ux%u -framerate %d -i - "
             "-c:v libx264 -preset medium -crf 16 -pix_fmt yuv420p -movflags +faststart \"%s\"\"",
             ff && *ff ? ff : "ffmpeg", w, h, s_fps, s_path);
#ifndef _WIN32
    // (no outer pair for sh)
    snprintf(cmd, sizeof(cmd),
             "\"%s\" -y -loglevel error -f rawvideo -pix_fmt bgra -s %ux%u -framerate %d -i - "
             "-c:v libx264 -preset medium -crf 16 -pix_fmt yuv420p -movflags +faststart \"%s\"",
             ff && *ff ? ff : "ffmpeg", w, h, s_fps, s_path);
#endif
    s_video = FD_POPEN(cmd, "wb");
    if (!s_video) {
        Log_Error("[framedump] can't start ffmpeg: %s", cmd);
        return 0;
    }
    s_w = w;
    s_h = h;
    free(s_row);
    s_row = (U8 *)malloc((size_t)w * h * 4);
    Log_Info("[framedump] recording %ux%u to %s", w, h, s_path);
    return 1;
}

void FrameDump_Frame(const U32 *pixels, U32 pitch, U32 w, U32 h) {
    if (!s_wav) return;
    const U32 now = (U32)TimerSystemHR;
    if (!s_started) {
        if (!OpenVideo(w, h)) {
            FrameDump_Stop();
            return;
        }
        s_started = 1;
        s_startMs = now;
    }
    if (w != s_w || h != s_h) {
        Log_Warn("[framedump] the frame's size changed (%ux%u, recording %ux%u): stopped", w, h, s_w, s_h);
        FrameDump_Stop();
        return;
    }
    const U32 elapsed = now - s_startMs;
    // the sound up to the clock
    const U32 wantSamples = (U32)((unsigned long long)elapsed * (U32)Sample_CaptureRate() / 1000);
    if (wantSamples > s_samples) {
        static S16 buf[4096 * 2];
        U32 left = wantSamples - s_samples;
        while (left) {
            const U32 n = left > 4096 ? 4096 : left;
            Sample_CaptureMix(buf, (S32)n);
            fwrite(buf, 4, n, s_wav);
            left -= n;
        }
        s_samples = wantSamples;
    }
    // this frame, as many times as the clock has come on by (the first at once)
    U32 copies = 0;
    while ((unsigned long long)s_frames * 1000 <= (unsigned long long)elapsed * (U32)s_fps) {
        if (!copies) {
            for (U32 y = 0; y < h; y++) memcpy(s_row + (size_t)y * w * 4, (const U8 *)pixels + (size_t)y * pitch, (size_t)w * 4);
        }
        if (fwrite(s_row, (size_t)w * 4, h, s_video) != h) {
            Log_Error("[framedump] ffmpeg stopped taking frames: stopped");
            FrameDump_Stop();
            return;
        }
        s_frames++;
        copies++;
    }
    // the commands due (each once; run here as the console runs its own over a modal)
    for (int i = 0; i < s_nbAt; i++) {
        if (s_at[i].cmd[0] && elapsed >= s_at[i].ms) {
            char cmd[200];
            snprintf(cmd, sizeof(cmd), "%s", s_at[i].cmd);
            s_at[i].cmd[0] = 0;
            Log_Info("[framedump] at %.2f s: %s", elapsed / 1000.0, cmd);
            Console_Execute(cmd);
        }
    }
    if (s_seconds && elapsed >= (U32)s_seconds * 1000) FrameDump_Stop();
}

void FrameDump_Stop(void) {
    if (!s_wav && !s_video) return;
    Sample_CaptureEnd();
    if (s_armedDt) {
        Timer_DisableFixedDt();
        s_armedDt = 0;
    }
    if (s_wav) {
        WavHeader(s_wav, s_samples);
        fclose(s_wav);
        s_wav = NULL;
    }
    if (s_video) {
        FD_PCLOSE(s_video);
        s_video = NULL;
    }
    free(s_row);
    s_row = NULL;
    s_nbAt = 0;
    Log_Info("[framedump] ended: %s, %u frames (%.2f s), sound %.2f s", s_path, s_frames, s_fps ? (double)s_frames / s_fps : 0.0,
             (double)s_samples / Sample_CaptureRate());
}

int FrameDump_Active(void) {
    return s_wav != NULL;
}
