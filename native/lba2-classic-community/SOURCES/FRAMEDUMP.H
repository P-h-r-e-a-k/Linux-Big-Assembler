// FRAMEDUMP.H -- a video of the game recorded from inside it (FRAMEDUMP.CPP; the console's `framedump`).
#pragma once

#include <SYSTEM/ADELINE_TYPES.H>

int FrameDump_Start(const char *path, int seconds, int fps, int wallClock);
void FrameDump_Frame(const U32 *pixels, U32 pitch, U32 w, U32 h);
void FrameDump_Stop(void);
int FrameDump_At(double seconds, const char *cmd);
int FrameDump_Active(void);
