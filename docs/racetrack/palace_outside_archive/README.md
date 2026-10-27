# The Emperor's palace race: its outside (archived)

On 2026-10-15 the user asked to drop the palace race's outside: "The outside of the palace track just isn't working, and we've tried a few
revisions, let's keep a copy of it so we can rebuild it in the future if we need to, but for now drop it totally."

This folder keeps that version (v12, the last one with the outside):

- `palace_design_v12.py` is the design script as it was, `tools/RaceTrackPlan/palace_design.py` at commit `60c8e61`.
- `palace_track_plan_v12.json` is the plan it wrote, `docs/racetrack/palace_track_plan.json` at that commit. It is the program's embedded
  `RaceTrackPlan.Palace.json`.
- The git tag `palace-outside-v12` marks commit `60c8e61` (pushed to GitHub).

## What the outside was

The race left the palace through the long hall's round window. A raised road climbed out over the cloud sea and went round the long hall's
two turrets in two helices: up 1.5 turns, then down about 1.2 turns. It then wove round the four turrets over the sixteen rooms' roof: the
road took the middle ways between them, went round the south-west turret and passed under itself. A jump ran along the strip north of the
north-east turret. The road came home up the middle way to the lip of a leap north off the roof. A win set game variable 79, so the race
woke on Celebration Island with its statue risen (CELEBRA2).

The island side of it:

- `PalaceOutside.Add`: the turrets (Otringal's halls, scaled) and the 60 rooftops round the race's road, read from the embedded plan by
  `PalaceOutside.RaceRoad`.
- `PalaceTeleporter`: the Dark Monk's gold ring on the harbour's pillar.
- The ways across the rocks: `PalaceCliff` corridors (the runway east, the ring round the palace's west and south sides).

That code is still in the program. `PalaceIsland.Build(..., outside: true)` puts it back, as does `ScriptRoundTrip palace ... --outside`.

## Rebuilding it

The simplest way is to check out the tag, `git checkout palace-outside-v12`, and build the track from that tree.

To bring it back onto a later tree:

1. Copy `palace_design_v12.py` back to `tools/RaceTrackPlan/palace_design.py`. Run it against a plain palace island built with the
   outside, `ScriptRoundTrip palace <game> <out> <pristine source> --outside --no-upstairs`, to write `docs/racetrack/palace_track_plan.json`.
   Or copy `palace_track_plan_v12.json` there directly.
2. Build the island with the outside: `PalaceTrack.Install` calls `PalaceIsland.Build`, whose `outside` now defaults to false, and whose
   `upstairs` is true. The outside's rooftops are placed round the plan's road over the roof, and the upstairs raises the roof by a
   storey, so the two don't go together as they are.
3. Rebuild ScriptRoundTrip (Release and Debug) and the app: the plan is embedded.

The race's lines (`PalaceTrack.Intro`) mentioned "over the rooftops to his teleporter at the harbour" until then.

Notes on its versions (v1 to v12) are in docs/LBA2_DESERT_RACE_TRACK_BUILD.md, "The Emperor's palace".
