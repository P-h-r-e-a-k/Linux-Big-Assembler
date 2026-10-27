# Maps in files of their own: File > New map, Open map, Save map, Install map into game, Export map

The user (2026-10-15): "Do we have the ability to create new maps for both games? Perhaps add a File>New option then we can specify the type
ile, lba2 inside, lba1". Then: "Let's also add the ability to load new maps from a file instead of them having to be in a HQR file."

A **map** is one of three kinds:

| Kind | What it holds |
|---|---|
| LBA2 island | its ground and its objects (an ILE and an OBL), and an outside scene for each of its cubes |
| LBA2 interior | a scene and its grid of blocks, in one of the game's block libraries |
| LBA1 interior | a scene, its grid and its block library |

It lives in a file of its own, a **`.lbamap`** (`Maps/LbaMap.cs`), not inside the game's HQR files. The engine plays a map put in the
game folder's **`MAPS`** folder straight from that file (`MAPPACK.CPP`, below).

Then (2026-10-15), on the files a map is made of: "The GR2 file contains the raw structural data for a map's physical geometry and tile
architecture... needs two external componets the .brk file that hold the individual graphic blocks/tiles. and the .bl2 file the layout
library... LS2 file: placement of twinsen, enemies, and characters... I don't know if when loading a map raw it can also reference a brick
package but that's what i'd like to do... Maybe just a simple txt tile inside the zip that can be manually added later?... save them as a
zip but a zip with a custom extension so it can keep .lbamap but it is actually a zip file and can just be opened with a file extractor."

## The File menu

- **New map…** (`NewMapWindow`, `Maps/MapNew.cs`): the kind, a name, and the game's own map it takes its look from.
  - *An LBA2 island*:
    - Its file name (one to eight letters or digits) and the game island whose look it has: ground and sea textures, light, sky, music.
    - Its size, 1 to 6 cubes each way, and its land's height.
    - It is flat land with a beach all round sloping into the sea. Its flat ground, steep ground and sea are textured as the base island's most
      common ones, and its light is baked from the base island's sun.
    - Its OBL is the base island's, so that island's objects can be placed on it.
    - It has an outside scene for each cube, made from the base island's first outside scene, with Twinsen alone in the first cube and
      cube-change zones between the cubes.
  - *An LBA2 interior*: built from one of the game's interiors (its block library, light and music). It is 32 x 32 cells of that interior's
    floor, with Twinsen on it (`Lba2BlankScene`).
  - *An LBA1 interior*: built from an LBA1 scene slot (its island, music and block library), as a blank floor with Twinsen on it
    (`Lba1BlankScene`). LBA1 can't have scenes added, so installed into the game it **replaces that slot's scene**.
- **Open map…**:
  - Opens a `.lbamap`.
  - Also opens an LBA2 island (`.ILE`) from outside the game folder, with its `.OBL` from beside it, or the base island's. It is given an
    outside scene for each of its cubes.
  - File > Open (Ctrl+O) also takes both.
- **`--map <file>`** on the command line, or a `.lbamap` as the only argument, opens a map at start.
- **Save map / Save map as…** (Ctrl+S while a map is open): reads the map back out of its workspace (below) into its file. The island's
  unsaved ground is saved first.
- **Install map into game…**: puts the open map into the real game folder. An LBA2 map asks which way:
  - *Yes*: as a map file, `MAPS\<name>.lbamap` (`MapInstall.InstallInMapsFolder`). The engine plays it from there; nothing else in the game
    folder changes. Its scenes are numbered from 400 where neither SCENE.HQR nor another map in `MAPS` has one, and written in its `MAP.TXT`.
    A map file of the same name there is replaced (its numbers kept, a `.bak` of it left).
  - *No*: into the game's own files, as before: new scenes from 400 on (clear of the maps in `MAPS` too). A map installed before is put back
    where it was: an island onto the scenes `ISLANDS.TXT` draws on its file, an LBA2 interior onto the map scene with its name and onto its
    grid.
  - An LBA1 map goes into its slot.
  - Every file changed keeps a `.bak` (`FileTransaction`).
- **Close map**: asks to save unsaved changes, then goes back to the game folder.
- **Export map…**: the island or interior scene in view, out of the game folder into a `.lbamap`.

Map files are saved by default in the open map's own folder, else in a `Maps` folder beside the program (`release\Maps` for the
published editor). Until 2026-10-22 it was `Documents\LBA Assembler\Maps`; nothing goes on the system drive now.

## The workspace

Opening a map makes a copy of the game folder, `<game folder>\_MAP_WORKSPACE`. This is `LiveDataRoot`'s mirror, as Test edits makes: its
files are hard links until written, and every write goes beside the file and is renamed over it, so the game's own files are never written.
The map is installed in it (`Maps/MapInstall.cs`), and it stands in for the game folder (`EditorSettings.TestModeActive`).

The mirror has the folder's top-level files and its data folders the engine reads: `VIDEO`, `VOX`, `MUSIC` and `MAPS`. Until 2026-10-17 it
had the top-level files only, and the engine doesn't start without `VIDEO\VIDEO.HQR` (`ASSET_PREFLIGHT.CPP`: "required game data is
missing"): Play in a map's workspace (and in Test edits) went black and straight back to the editor, "The game ended: *** ERROR-> File not
found". With `MAPS` there, a map's scenes in the workspace are numbered clear of the maps in the game's `MAPS` folder, as in the game.

So the editor's island list, its scenes, the 3D view, Build mode, the scene, grid and actor editors, and Play all work on the map as on any
of the game's own. The window's title shows the workspace's folder.

**Saves go into the map (2026-10-17).** A tester: "if I was to give an npc a script it would not save it to the custom maps files but
instead to the main game directorys scene.hqr ... the .ls2 it already gives you that file, so it'd just need to also save the npc scripts
to it". What it did before (tried on a sandbox): the script window's save went into the workspace's SCENE.HQR, which is the map's, and File >
Save map put it into the .LS2 -- the game's own SCENE.HQR stayed byte for byte as it was -- but every word said otherwise ("Save to
SCENE.HQR…", "write scene 405 into ...\_MAP_WORKSPACE\SCENE.HQR ... keeps your original as SCENE.HQR.bak"). And a real bug: the script
editor's unsaved edits outlived Close map. An LBA1 map takes a scene slot of the game, so the game's own scene 28 then showed the map's edit,
and saving it wrote the map's scene over the game's scene 28 in the real SCENE.HQR. Now:
- The script window, the actor attributes windows, the zone inspector and the scene panel say where a save goes: *Save to the map…*,
  "Saved into the map "Name" (File > Save map writes its file)".
- The script editor's edits belong to the folder they were made in (`ScriptSession`): they are never shown as, or saved into, another
  folder's scenes. Close map asks about them with the map's other changes (Yes saves them into the map and the map into its file, No drops
  them); Save map and Install map into game ask whether to save them into the map first; opening a map, test edits, the 1996 demo, another
  folder in Settings and closing the editor ask too.
- While a map is open the game folder is guarded: `FileTransaction` refuses to write a file in it outside the workspace (every scene, script,
  zone, actor and HQR entry save goes through it), File > Install map into game excepted (`FileTransaction.AllowWrites`).
- The script editor's comments on a map's scripts travel with it: `<NAME>_COMMENTS.JSON` in the .lbamap (`comments = ` in MAP.TXT; a scene
  by its place in the map), put beside the workspace's SCENE.HQR when the map opens, read back by Save map. The engine leaves it alone.

Leaving the workspace:
- The process's working folder is moved out first, since the renderer's data root is set by `chdir`.
- If something still holds a file, it is removed again a little later.
- A workspace a crashed session left behind is removed at the next start, or at the next map opened.

## The `.lbamap` file

A zip: any zip program opens it. Its files are stored, not compressed (a zip program packing it again with compression is fine too: the
engine inflates them). They are named after the map (eight letters of its name, an island's file name):

| File | What it is |
|---|---|
| `MAP.TXT` | what the map is, `key = value` lines a person can edit (below) |
| `<NAME>.LS2` | a scene's record as SCENE.HQR holds it (an island's: `<NAME>_<k>.LS2`, one for each cube). LBA1: `.LS1` |
| `<NAME>.GR2` | an interior's grid: LBA_BKG.HQR's entry, its 34-byte header (library, fragments, used blocks) and its columns. LBA1: `.GR1`, LBA_GRI.HQR's |
| `<NAME>.BL2` | its own block library: LBA_BKG.HQR's library entry, its brick numbers the BRK's. LBA1: `.BL1`, LBA_BLL.HQR's |
| `<NAME>.BRK` | its own bricks: an HQR, brick n its entry n, each as LBA_BKG.HQR stores it |
| `<NAME>.GRM` | its own grid fragments (doors and the like): an HQR, fragment n its entry n |
| `<NAME>.ILE`, `.OBL` | an island's ground and objects |
| `<NAME>_COMMENTS.JSON` | the script editor's comments on its scenes' scripts (the editor's; the engine leaves it alone) |

`MAP.TXT` ('#' a comment):

```
format = 2
game = lba2                       (lba1: an LBA1 map, for its scene slot)
kind = interior                   (or island)
name = Brick room
island = 0                        (the game island its scenes borrow sky, palette, music and holomap from)
slot = 4                          (the scene it was made from; an LBA1 map's slot)
scene 400 = BRICKROO.LS2          (one line for each scene: the engine plays it as scene 400)
grid = BRICKROO.GR2
library = BRICKROO.BL2            (without it: the game's library its grid's header names)
bricks = BRICKROO.BRK             (without it: the game's bricks)
fragments = BRICKROO.GRM          (without it: the game's fragments its grid's header names)
forbidden brick = 0               (of its own bricks, the one the engine never draws)
ile = PACKISLE.ILE                (an island's)
obl = PACKISLE.OBL
comments = BRICKROO_COMMENTS.JSON (the script editor's comments; the engine leaves it alone)
text = SKIRESRT_TEXT.TXT          (the map's own lines of dialogue: File > Map text…)
ledges = drop                     (an island's: Twinsen walks off a terrain cliff and falls instead of stopping: File > Map settings…)
characters = SKIRESRT_CHARACTERS.TXT   (the map's own characters: the Character maker; with the three below)
character bodies = SKIRESRT_BODIES.HQR
character anims = SKIRESRT_ANIMS.HQR
character entities = SKIRESRT_ENTITIES.HQR
```

- **Text** (`Maps/MapText.cs`, `MapTextWindow.cs`): `<number> = <words>` lines, UTF-8 (`<number>:<attribute> = <words>`: 2 a big
  message, 4 full screen, 8 said in a bubble). A script's `message(n)` in the map's scenes says the map's line n before the game's text
  (`MapPack_Text`, MESSAGE.CPP `GetText`); there is no voice for it. Number new lines from 1000 so they don't hide the island's own.
- **Characters** (`Characters/*.cs`, `CharacterMakerWindow.cs`): an entity -- a kind of actor -- with its bodies and keyframe animations
  (milliseconds a keyframe; a keyframe can be "in the air", LBA2's gravity-free frame). `<STEM>_CHARACTERS.TXT` names them:
  `character <k> <name>`, `body <k> <generic> <entry> <name>`, `anim <k> <generic> <entry> <name>`, the entries the map's own (its
  `_BODIES.HQR`, `_ANIMS.HQR`; `_ENTITIES.HQR` holds each entity's record). A scene's actor of the map's k-th character names entity
  `30000 + k` in the map file, as a zone names a scene; scripts name its bodies and animations by their generic numbers (`anim(190)`).

- The scene lines' order is the map's own. A zone into another of the map's scenes names it `30000 + k`, k its place (`LbaMap.LocalScene`):
  the map works whatever numbers its scenes get. Change a number in `MAP.TXT` to move a scene.
- **The brick package** (`Maps/MapBricks.cs`): a new LBA2 interior and an exported one carry their block library, every brick it names
  (numbered from 0 in the game's order) and the grid fragments their scene's fragment zones use. A 32 x 32 floor from scene 4: 673 bricks,
  310 KB. Scene 10: 1,324 bricks, 554 KB. Such a map needs nothing of the game's LBA_BKG.HQR.
- A map of the first version (`map.json`, `scene<k>.bin`, `grid.bin`, `library.bin`, `island.ile`/`.obl`) still opens, and saves as the
  new one. Its interior keeps the game's library.

## Installing into a game folder (`MapInstall`)

- **LBA2 scenes** are appended to SCENE.HQR from scene 400 on, past the game's own and the race tracks' (222-300). SCENE.HQR is padded with
  empty entries up to them, entry 0 keeps the biggest scene's size, and they are named in the folder's SCENE.HQD.
- **An LBA2 interior's own library, bricks and fragments** (`MapBricks.Install`): a brick LBA_BKG.HQR has already (the same entry, or the
  same decoded brick) is used, the others are added after its bricks. The library, its brick numbers renumbered to them, is one of the file's
  own it equals, else it is added after its libraries. The fragments likewise. The header's sizes for a cube's bricks and masks are raised
  if the map's need more. Read back (`MapBricks.Capture`), the package is made again from what the folder has, with the grid's header as the
  map's.
- **An LBA2 interior's grid** is inserted into LBA_BKG.HQR after its last grid:
  - The grid fragments, block libraries, bricks and the scene-to-grid table after it each move up an entry.
  - Entry 0 says where they now start, and its biggest-grid size is raised if needed.
  - The table names the scene's new grid.
  - Grids are numbered with a byte, so there can be 256. The retail file has 149.
- **An LBA2 island's** ILE and OBL are written under its file name. The game folder's **`ISLANDS.TXT`** gets:
  - `island <file> <number>`: whose look it has (the editor's renderer and palette)
  - `scene <number> <file>`: each of its scenes (the engine draws them from that file)
  - `ledges <file> drop`: when the map has `ledges = drop`
- **Text**: beside the island's files as `<STEM>_TEXT.TXT`; the engine finds it through the scene's island file.
- **Characters**: their bodies and animations appended to BODY.HQR and ANIM.HQR, their entities to RESS.HQR entry 44's table, the
  entries renumbered to where they went (`MapCharacters.Renumber`); the scenes' actors of entity `30000 + k` take the k-th one's number.
  `<STEM>_CHARACTERS.TXT` beside the island's files names them, so Save map takes them back out.
- **LBA1**: the scene, grid and library entries of its slot are replaced.

## The engine (`native/lba2-classic-community`)

**Maps in the MAPS folder** (`SOURCES/MAPPACK.CPP`, `LIB386/SYSTEM/ZIPREAD.CPP`):

- The game folder's `MAPS\*.lbamap` are read when first needed (in their names' order): each one's `MAP.TXT`. The log says what each one
  has (`MAPS: room2.lbamap: 1 scene (409 to 409), an interior, its own block library, its own bricks`). An LBA1 map is left alone. A scene
  number another map took first is left to it, with a warning; one past 1023 too. A map's scene numbered as one of the game's is played
  instead of the game's.
- `ZIPREAD.CPP`: a zip's central directory, its stored and deflated members (an inflater of its own: the engine has no zlib), and
  **virtual files**: `OpenRead` (FILES.CPP) opens a file a map stands in for before the file system's; `Read`/`Seek`/`Close` follow it.
  A stored member is read in place; a deflated one is inflated, the last four kept.
- **Scenes**: `HQF_Hook` (HQFILE.CPP, asked first by `HQF_Init`) serves SCENE.HQR's entry n + 1 from the map's LS2 for scene n, so
  `LoadScene`, the buggy's `LoadSceneCubeXY` and the renderer read it as the game's. `AdjustLba2Mem` makes the scene buffer as big as the
  biggest LS2 too.
- **Zones**: a cube-change zone of a map's scene going to `30000 + k` goes to the map's k-th scene (`MapPack_ZoneScene`, OBJECT.CPP).
- **Interiors** (`InitGrille`): the grid from the GR2; the library from the BL2 if it has one, else the game's its grid's header names; the
  bricks from the BRK (`LoadUsedBrickFrom`: the brick table at 0 of the map's HQR) if it has them, else LBA_BKG.HQR's. Fragments
  (`IncrustGrm`) from the GRM. `InitBufferCube` makes the grid, library, brick and mask buffers room for the maps' (each map's used bricks'
  sizes added up from their headers); a map read later that doesn't fit (the renderer's data root changed) is refused with a warning.
- **Islands**: the ILE and OBL are virtual files under a name no game file can have (`<map file>|<NAME>.ile`); `SceneIslandFile` gives
  that name for the map's scenes.
- **Text**: `MapPack_Text(scene, number)` gives a map scene's line, UTF-8 turned into the game's code page.
- **Characters**: `MapPack_AddEntities` (PERSO.CPP, after the entity table is loaded) appends the maps' entities to it and their bodies and
  animations after BODY.HQR's and ANIM.HQR's entries (served by `HQF_Hook`); `MapPack_Entity` (DISKFUNC.CPP) turns a map scene's
  `30000 + k` into the k-th one's number.
- **Ledges** (`ledges = drop`): `DoCornerReajustExtPt` (EXTFUNC.CPP) lets an actor off the edge of a terrain wall cell whose ground is
  below its feet, so it falls rather than being pushed back.
- The renderer's `set_data_root` forgets the maps read (`MapPack_Reset`).

**Maps in the game's own files** (the other install):

- `EXTFUNC.CPP` `SceneIslandFile`: reads `ISLANDS.TXT` once. A scene listed there is drawn from its own island file instead of its island
  number's.
  - The island is loaded again when the file changes, not just the number: a map island's scenes share their number with the base
    island's.
  - Its sky, palette, music and holomap are still its number's.
- `RENDERER_API.CPP` `lba2_renderer_load_island`: an island name the engine doesn't know takes its look (sky, palette) from its
  `island <file> <number>` line.
- `DEFINES.H` `MAX_GRID_SCENES` 1024: LBA_BKG.HQR's scene-to-grid table (`TabAllCube`, 256 retail) can name interiors past scene 255.
  `InitGrille` guards the index.

## In the editor

- The scene list puts a scene that `ISLANDS.TXT` draws on an island file of its own under that file (`BuildSceneEntries`).
- The island's palette is its look's (`LoadIslandPalette`, `IslandMapRenderer.LoadPalette`): RESS.HQR entry 27 + its island number.

## Tests

`ScriptRoundTrip`:

- `mapnew lba2interior|lba1interior|lba2island ...`
- `mapexport lba2interior|lba1interior|lba2island ...`
- `mapinstall <map> <folder>`: installs the map into the folder's HQRs, reads it back, and says whether it is the same map.
- `mapmaps <map> <folder> [file name]`: puts it in the folder's MAPS folder, printing the scene numbers it was given.
- `mapinfo <map>`: what it is, its files and its MAP.TXT.
- `mapactors <sandbox game folder>` (2026-10-17): the workspace's mirror has `VIDEO\VIDEO.HQR` and the engine starts on it; a new interior
  map has actor 1, the Meca-penguin's slot; two maps in the mirror played headless -- one with actors made the old way (Zoé added first
  is hidden as actor 1, a skeleton is Twinsen), one as the editor makes them now (the LBA2 scene editor's New 3D actor: Zoé, a rabbibunny
  skeleton; Actor from another scene: scene 184's skeleton, which goes for Twinsen; the actor attributes window's body change to Zoé with
  her umbrella), each drawn with its body, also once saved and put in the MAPS folder.
- `mapscripts <sandbox LBA2 folder> [<sandbox LBA1 folder>]` (2026-10-17): a map's script saved into the map by the script editor's
  session, File > Save map's .LS2 and comments, the game's SCENE.HQR byte for byte unchanged, the guard; an LBA1 map's unsaved edit after
  Close map neither shown as the game's scene 28 nor saved over it; behaviours (see the README's LBA2 scene editor) on every kind; a new
  map's skeleton with its kind's usual behaviour and one with the attack template played headless (`--fixed-dt 16`): they go for Twinsen
  and hit him; a talker says its line when he presses action.

From the MAPS folder, headless (`lba2cc --headless --no-audio --game-dir <sandbox> --exec-at 6 "cube <n>" ...`), 2026-10-15, on a sandbox
(`E:\dump\TEMP\mappack\game`):

- A new interior from scene 4, with its own library and 673 bricks, played as scene 405. Its library's brick numbers are its own (0 to 672):
  drawn from the game's bricks they would be the wrong ones.
- The same file packed again by a zip program, compressed, its `MAP.TXT` changed by hand to scene 777: the same picture.
- Scene 10 exported (its own library, 1,324 bricks, its fragments; a fragment zone on from the start) and played as scene 407: the same
  picture, pixel for pixel, as the game's own scene 10.
- A first-version interior (the game's library) as scene 408: the same picture as the one with its own bricks.
- A 2 x 1 island (`PACKISLE`, no ILE of that name in the folder) as scenes 405-406: Twinsen on its land; walking east over the cube's edge
  took him to scene 406 (its zone names 30001).
- Installed into the HQRs and read back: the same map (the 673 bricks all found in LBA_BKG.HQR, its library one of the file's); scene 10's
  too; an LBA1 map in its slot.
- The UI (the Debug editor on the sandbox): `--map` with the map of its own bricks, Install map into game, Yes (`MAPS\room2.lbamap`, scene
  409, played headless: the same picture), Save map, Close map.
- The 15 race tracks build byte for byte as before (42 files).

On sandboxes (2026-10-15):

- An LBA2 interior built from scene 4 became scene 400, grid 149. Played headless (`cube 400`), Twinsen stands on its floor.
- A 2 x 2 island built from Citadel became MYISLE, scenes 401-404. Played headless (`cube 401`), Twinsen stands on its land with the sea
  beyond, in Citadel's rain.
- An LBA1 interior from slot 28 was installed and read back the same.
- Scene 5, MYISLE and LBA1's scene 1 were exported.
- Installing again replaced the earlier copy.
- The UI (UI Automation on a sandbox): New map for all three kinds, `--map`, Save map as, Install map into game, Close map.

## Limits

- The engine plays maps from the MAPS folder; the editor's scene list doesn't show them. File > Open map opens one to edit (in the
  workspace, installed in its HQRs).
- A life script's own scene change (`CHANGE_CUBE`) to a map's scene names its number, not `30000 + k`.
- LBA1 maps are for their slot: the LBA2 engine leaves them alone in MAPS.

- LBA1's scenes can't be added to: an LBA1 map takes a scene slot of the game.
- An LBA2 scene's actor 1 is the Meca-penguin's (the engine hides whatever is there): a map's own actors start at 2. A new map has the slot;
  a map made before 2026-10-17 gets it when an actor is added in Build > Actors and zones (see the README's "Its actors").
- Add Actor Here (Build mode) doesn't work on a map's island: the renderer lists no actors for it (an island of the maps has no island
  number of its own, and its scenes are past 221), so it says the cube is full. Add a map island's actors in Build > Actors and zones.
- An LBA2 map island's scenes have no place on the holomap, and the renderer shows no actors on it in the 3D view outside Play: its scenes
  carry the base island's number.
- A race track build puts SCENE.HQR back from its own copy of the originals (`.before-racetrack`), so maps installed after a race track
  build go with the next one. Install them again after it.
- Islands are made from the game's own islands: those with an OBL beside their ILE.
