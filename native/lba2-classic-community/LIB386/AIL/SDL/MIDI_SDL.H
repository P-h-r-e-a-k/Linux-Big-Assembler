#pragma once

// This header contains declarations private to SDL

#include <AIL/MIDI.H>

#ifdef __cplusplus
extern "C" {
#endif

/* A Standard MIDI File played as music (LBA Assembler, 2026-10-20: MIDI.CPP says how). STREAM.CPP plays a .mid name through these, so
   a MIDI file is music like any other there: StreamName, IsStreamPlaying, Pause/ResumeStream, the loop, the volume. */

// Whether a file's name is a MIDI file's (.mid, .midi, .smf)
S32 IsMidiFileName(const char *path);
// Whether this platform has a MIDI output to play one through (Windows: WinMM's)
S32 MidiOutputAvailable(void);
// The file read and started (any playing before it stopped): looped or once, at a volume (0-127: the music's), from `startMs` into it.
// TRUE: it is a MIDI file and its sequencer runs -- whether a device sounds it or not (MIDI.CPP: never with the audio off)
S32 PlayMidiFile(const char *path, S32 loop, S32 volume, S32 startMs);
void StopMidi(void);
void PauseMidi(void);
void ResumeMidi(void);
void SuspendMidiOutput(void); // (the window's focus lost: held without touching PauseMidi's)
void ResumeMidiOutput(void);
void SetMidiVolume(S32 volume);
void SetMidiLoop(S32 on);
// Playing: started, not paused, not played to its end
S32 IsMidiPlaying(void);
S32 HasMidiEnded(void);
S32 MidiPositionMs(void);
S32 MidiDeviceOpen(void);

#ifdef __cplusplus
}
#endif
