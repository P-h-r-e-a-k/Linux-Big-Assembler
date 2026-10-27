#ifndef RACEUPDATE_H
#define RACEUPDATE_H

/* RACEUPDATE: the race game's updater started (RACEGAME.CPP, 2026-10-19): the installer's copy beside the game (Update\LBRA-Setup.exe)
 * run as `LBRA-Setup.exe --check-update "<install dir>" --wait-pid <the game's pid> [--manual]`, hidden and detached -- it looks for a
 * new version, downloads it, and installs it once the game is closed (the installer's Updater.cs). Windows only: elsewhere nothing. */

// Whether the file is there.
int RaceUpdate_Exists(const char *pathUtf8);
// The updater started (non-zero), or not (its exe missing, CreateProcess failed). Paths in UTF-8 (the engine's code page).
int RaceUpdate_Spawn(const char *exeUtf8, const char *installDirUtf8, int manual);
// The same copy started for something else with its window, detached (2026-10-26: the ghosts' "Share my ghosts" and "Import ghosts" --
// `LBRA-Setup.exe --export-ghosts|--import-ghosts "<ghosts folder>"`): `what` its first argument, `folder` its second. Non-zero: started.
int RaceUpdate_Tool(const char *exeUtf8, const char *what, const char *folderUtf8);
// A picture onto the clipboard (2026-10-27: Best laps' "Copy as an image"): `rgb` w x h, 3 bytes a pixel, top row first, as a DIB, and the
// PNG file at `pngUtf8` (if any) as the clipboard's "PNG" too -- what chat programs paste. Non-zero: put there.
int RaceUpdate_ClipboardImage(const unsigned char *rgb, int w, int h, const char *pngUtf8);

#endif
