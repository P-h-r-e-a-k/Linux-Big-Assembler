#ifndef RACEGAME_H
#define RACEGAME_H

#include <SYSTEM/ADELINE_TYPES.H>

/* RACEGAME: the race game -- LBA Assembler's race tracks played as a game of their own (the standalone Little Big Halloween Racing
 * Adventure), when RACEGAME.TXT is beside the game's data. RACEGAME.CPP says what is in it. */

#define RACEGAME_START 2399                 // RaceGame_Menu: a race is set up (RaceMod_LoadFile) -- a new game, no introduction

S32 RaceGame_On(void);                      // RACEGAME.TXT read: the main menu is the race game's
// The race game's main menu, and its pages (Specials, a race or a time trial, the car, Best times): what MainGameMenu does next --
// 70 resume (1000 for Esc with a race on), 74 the options, 75 quit, RACEGAME_START a race. `inGame`: a race is on to resume.
S32 RaceGame_Menu(S32 inGame);
S32 RaceGame_MenuText(S32 num, char *string);   // BuildCustomMenuText: a row of the race game's (non-zero), its label in `string`
S32 RaceGame_RowDisabled(S32 num);          // a row greyed and passed over (Story Mode, Track select until they open)
// OptionsMenu: the menu to show -- `menu` (the game's own), or in the race game a copy with its own rows after the game's (Race
// controls, power-ups; Updates); and a row picked there that isn't the game's: its page shown (non-zero), or 0 (not the race game's)
U16 *RaceGame_OptionsMenu(U16 *menu);
S32 RaceGame_OptionsRow(S32 num);
// RACEMOD.CPP: the player changed the race camera's view during a race (V): kept for the next race (RACEGAME_SETTINGS.TXT camera=)
void RaceGame_CameraChosen(S32 view);

#endif
