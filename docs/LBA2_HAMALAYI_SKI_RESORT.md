# LBA1's ski resort (Hamalayi, scene 96) as an LBA2 island map, made through the interface alone

An interface test (2026-10-23): LBA1's scene 96 rebuilt as an LBA2 island map (`skiresort.lbamap`, island file `SKIRESRT`) only by
driving the editor as a user would -- every control reached through UI Automation, the mouse through posted messages. Where the interface
couldn't do something, the interface was extended (below). Tools outside the editor were used only to check the result against LBA1
(`tools/ScriptRoundTrip`: `lba1ground`, `lba1sceneactors`, `hamalayiscripts`).

To build it again yourself:

- [hamalayi/HOWTO.md](hamalayi/HOWTO.md): a step-by-step guide through the editor, with a picture of each window.
- [hamalayi/RECREATE.md](hamalayi/RECREATE.md): every value, in tables.
- [hamalayi/recipe/](hamalayi/recipe/): the scripts that drive the same buttons, and the plan files.

## What it has

- **Ground**: every vertex of the 64 x 64 cube set by brush strokes (Flatten at a typed vertex), to LBA1's brick columns; 50 atlas
  tiles made from LBA1's bricks and painted cell by cell; cliffs stood up the slope; light baked.
- **Objects**: 66, of 14 bodies made in the Object maker from LBA1's bricks: 43 snowy trees, a big tree, the hut, 4 lift pylons, the
  fence (4 + a corner), the snowman, 5 small trees, a stone and the rock wall in 5 parts.
- **Actors, zones, scripts**: LBA1's 14 actors with their life and track scripts rewritten in LBA2's language (the Rabbibunny instructor
  walks and talks, the slalom flags, the snowboard to pick up, the hidden bonus); its 8 zones.
- **Dialogue**: the instructor's lines as the map's own text (File > Map text…).
- **Twinsen with snowboard**: a character made in the Character maker -- its body and 9 keyframe animations -- with the slalom flag
  and the snowboard as characters too.

## Interface added for it

- Terrain: a brush at a typed vertex (Apply the tool here, Stroke to here), in the decor tools too; "Block steep ground on the whole
  island"; steep-ground collision for the four cells round a vertex.
- Ground atlas: a tile from an LBA1 brick (`AtlasBrickWindow`).
- Object maker (`ObjectMakerWindow`, `Terrain/BrickObject.cs`): an object built brick by brick from LBA1's bricks, saved as a design
  (`.lbaobject`), added to the island's OBL as a body. Its object pages are in undo.
- Objects: selecting again at the very same point steps through every object under it, so one standing in another can be reached.
- Scene panel (then the scene editor window): Choose a body reads the bodies again each time, so a character saved while the window is open is offered. The 3D view's
  renderer reads the entity table, bodies and animations again when the Character maker saves
  (`lba2_renderer_reload_entities`). Engine: an actor's entity past the table falls back to entity 0 (DISKFUNC.CPP).
- Map text (`MapTextWindow`), Map settings (`MapSettingsWindow`: ledges that drop).
- Character maker (`CharacterMakerWindow`, `Characters/`): bodies, keyframes in milliseconds, keyframes in the air.
- Scene panel (then the scene editor window): Place X/Y/Z typed for a new actor or zone; accessible names on the inspector's rows.
- Script window: Check commits the text being edited; a script's labels laid out on demand, so scripts can name each other's before save.

The map format and engine side: [LBA_MAPS.md](LBA_MAPS.md) (text, ledges, characters).

## Where it differs from LBA1

- Using the snowboard and the red card (LBA1's inventory items) is the action key in the zones where LBA1 used them.
- LBA1 plays the film SURF.FLA at the end of the run; here Twinsen is taken back to the top.
- The zone LBA1 changes cube with (to scene 15) leads back into the map's own scene: the rest of Hamalayi isn't in the map.
- The snowy tree's design keeps LBA1's brick column under its back cell, a snow block beside each trunk.
- Known: the headless engine's screenshots show a second, ghost snowboarder; the editor's Play shows one.

## Pictures

- [hamalayi/lba1_scene96.png](hamalayi/lba1_scene96.png): LBA1's scene 96.
- [hamalayi/overview.png](hamalayi/overview.png): the island from LBA1's angle.
- [hamalayi/play.png](hamalayi/play.png): Twinsen on his snowboard in the editor's Play.
- [hamalayi/trees.png](hamalayi/trees.png): the tree clump by the slope.
