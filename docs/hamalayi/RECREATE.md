# Recreating LBA1's ski resort in LBA2 through the interface: every step

This is the full recipe for building `skiresort.lbamap` from nothing, the way it was built on 2026-10-23: LBA1's scene 96 (Hamalayi's ski
resort) as an LBA2 island map, using only the editor's interface. Every value typed is here or in [recipe/](recipe/).

This is the reference: every value, in tables. For a walk through the editor's windows with a picture of each, read
[HOWTO.md](HOWTO.md) first.

You can follow it two ways:

- **By hand:** the numbered steps below say which window, which control and what to type. The long lists (875 ground strokes, 3,828 paint
  runs) are in the recipe's plan files; you follow them row by row.
- **Scripted:** each step names the script in [recipe/ui/](recipe/ui/) that drives the same controls through Windows UI Automation, as I
  did. Run them in PowerShell 5.1, in order, with the editor copy running on a sandbox.

The result was checked against LBA1's own data with the tools listed under **Checking against LBA1**. Those tools only check; nothing they
produce goes into the map except through the steps below.

## Before you start

1. **Build** the Debug editor: `dotnet build LBAAssembler.csproj -c Debug`. The map needs the engine changes from v2026-10-23 or later
   (map text, characters, ledges), so build the native targets too if yours are older.
2. **Make a sandbox** (the scripts' default is `E:\dump\TEMP\hamalayi`; set `$Global:Sandbox` before running them to use another):
   - `<sandbox>\game`: a plain copy of an LBA2 game folder (not hard links).
   - `<sandbox>\app`: `Start-Editor` (in `recipe\ui\lib.ps1`) copies `bin\Debug\net10.0-windows` here each time it starts the editor,
     keeping `app\Maps`, where the map file lives.
   - `<sandbox>\settings`: `Start-Editor` writes the editor's settings here, with all sound muted.
   - A copy of LBA1's folder for its bricks (`$Global:Lba1Dir`, default `E:\dump\TEMP\mappack\lba1`). The ground atlas and the Object
     maker read LBA1's bricks from it.
3. **Coordinates.** The map has one cube, cube (7, 7) of the island's 8 x 8 grid:
   - Its vertices are **448 to 512** across and down. LBA1's cell (x, z) is vertex (448 + x, 448 + z).
   - World X and Z inside the cube run 0 to 32768, 512 a cell. Y is 256 a brick layer.
   - LBA1's scene coordinates are used unchanged.
4. **Save the map after every step** (File > Save map). Opening the map rebuilds its workspace from the `.lbamap` file, so anything saved
   only to the workspace is lost the next time it opens. I lost the zones once this way.

The recipe folder:

| Folder | What |
| --- | --- |
| `recipe/ui/` | The UI scripts. `lib.ps1` and `charlib.ps1` are the helpers. |
| `recipe/plan/` | The plans: `targets.json` (heights), `tile_plan.json` (atlas and paint), `hut_fix.json`, `object_plan.json`, `zone_plan.json`, `actor_ref.json` (LBA1's actors and track points), `bricks.csv` and `ground.csv` (LBA1's bricks, from `ScriptRoundTrip lba1ground`), `ground_fix.py`, and the generators `targets.py`, `tile_plan.py`, `object_plan.py` (kept for reference; their paths are my scratch folder's). |
| `recipe/objects/` | The 14 object designs (`.lbaobject`), final (the hut's roof made whole). |
| `recipe/scripts/` | The LBA2 scripts, `actor<N>_life.c` and `actor<N>_track.c`. |

## 1. Make the map

*Script: `new_map.ps1`.*

1. Start the editor on the sandbox (`Start-Editor`).
2. **File > New map…**:

   | Field | Value |
   | --- | --- |
   | Name | `Ski resort` |
   | File name | `SKIRESRT` |
   | Cubes across (X) | `1` |
   | Cubes down (Z) | `1` |
   | Land height | `512` (the dialog refuses 256: its range is 300 to 20,000) |
   | Look: the game's island | leave the default, **DESERT** (it becomes `island = 2` in MAP.TXT) |

   Leave the picture empty and its levels at their defaults (black -400, white 3600, sea 0). Press **Create**.
3. **File > Save map as…** and save it as `<sandbox>\app\Maps\skiresort.lbamap`.

From here on, each step starts with the map open in Build mode: `Open-SkiResort` does File > Open map on that file, Mode **Build**, the
**Build** tab, then **Terrain**. The scripts expect the editor running on the map. `Stop-Editor` then `Open-SkiResort` restarts it, for
example after a rebuild.

## 2. The ground's heights

*Script: `paint_ground.ps1`. Plan: `plan/targets.json`, `runs`: 875 rows.*

In **Build > Terrain**:

1. Tool **Flatten to level**.
2. Tick **Square brush (whole cells, full strength)**. **Radius** 0, **Strength** 100.
3. Tick **Block terrain steeper than the angle below** and set **Unwalkable angle °** to `45`.
4. For each row of `runs` (`level`, `z`, `x0`, `x1`), in the file's order:
   1. Type `level` into **Level** (it only changes when the level does; the rows are sorted by level, -256 up to 6400, 43 levels).
   2. Under **Brush at a vertex**, type **Vertex X** = 448 + `x0` and **Vertex Z** = 448 + `z`, then press **Apply the tool here**.
   3. If `x1` isn't `x0`, type **Vertex X** = 448 + `x1` and press **Stroke to here**.
5. Save (the terrain's **Save** button), then File > Save map.

This sets all 4,225 vertices of the cube to LBA1's ground. The heights come from LBA1's bricks: each vertex is the highest of its four
cells' tops, so a terrace keeps its whole cells and its cliff falls in the cell below it, as LBA1 draws it. The lone trees were taken off
the ground. The tree clumps weren't, which step 5 fixes.

## 3. The ground's pictures

*Script: `texture_ground.ps1`. Plan: `plan/tile_plan.json`: 50 slots, 3,828 runs, 6 sea cells.*

**The atlas.** Open the **Ground atlas** section. For each slot in `slots` (the table is in the appendix):

1. Type **Tile X** and **Tile Y** (the slot's `x`, `y`), **Tile size** 32, and press **Choose this tile**.
2. Press **Put an LBA1 brick here…**. In the dialog **Put an LBA1 brick in the ground atlas**:
   - For a ground slot: **Brick number** = `brick`. Leave **Which of its faces** at *Its top (the ground)*.
   - For a cliff slot (slots 0-3): **Brick number** = the pair's first brick and **Which of its faces** = *Its side over another brick's
     (a cliff two layers high)*. Then **Brick under it** = the pair's second brick.
   - Press **Put it in**.

Slot 19 is skipped: it holds the island's sea tile.

**The painting.** Tool **Paint atlas tile**, **Square brush** ticked, **Radius** 0. For each slot:

1. Choose its tile as above (Tile X, Tile Y, size 32, **Choose this tile**).
2. Tick **Stand the tile up the slope (cliffs)** for the four cliff slots; untick it for the others.
3. For each of its `runs` (`z`, `x0`, `x1`): **Vertex X** = 448 + `x0`, **Vertex Z** = 448 + `z`, **Apply the tool here**. If
   `x1` ≠ `x0`: **Vertex X** = 448 + `x1`, **Stroke to here**.

**The game codes.** The new map's land was coded as sea where its beach was:

1. Tool **Paint game code**. **Game code** = *0: none*. **Radius** 32. Vertex 480, 480: **Apply the tool here** (the whole cube).
2. **Radius** 0. **Game code** = *1: water (drowns)*. For each of `sea_cells` (63,0 to 63,5): Vertex 448 + x, 448 + z, **Apply the tool
   here**.

Save, then File > Save map.

## 4. The hut's ground

*Script: `fix_hut.ps1`. Plan: `plan/hut_fix.json`.*

LBA1 roofs the hut with snow-coded bricks, so the heights took its roof for ground. In Terrain:

1. **Flatten to level**, square brush, radius 0, strength 100, **Level** 256. For each z from 54 to 57: Vertex (448 + 39, 448 + z)
   **Apply the tool here**, then Vertex X 448 + 42 **Stroke to here**.
2. Untick **Stand the tile up the slope**. For each of the 25 `cells` (`x`, `z`, `tile`): choose the tile (Tile X, Tile Y = `tile`, size
   32, **Choose this tile**), tool **Paint atlas tile**, Vertex (448 + x, 448 + z), **Apply the tool here**.
3. Save, File > Save map.

## 5. The ground under the tree clumps

*Script: `fix_ground2.ps1`, after running `plan/ground_fix.py`.*

The first heights took the snowy tree clumps for ground, which left the trees on pillars. `ground_fix.py` works out the ground without the
blocks that become objects (the trees, the hut, the small trees, the snowman, the stone) and compares it with the island as it now is:

```
python plan\ground_fix.py <sandbox>\game\_MAP_WORKSPACE\SKIRESRT.ILE
```

It writes `plan\ground_fix.json`: runs of vertices to lower, and the cells round them with their tiles. The sea's corner cells keep the
island's sea tile. On the finished island it reports 0 vertices different. Then, in Terrain:

1. **Flatten to level**, square brush, radius 0, strength 100. For each of `runs`: Level, Vertex, **Apply the tool here**, and **Stroke
   to here** to `x1` (as step 2).
2. For each of `tiles`: choose its tile, **Paint atlas tile**, tick **Stand the tile up the slope** if `cliff` is true, Vertex (448 + x,
   448 + z), **Apply the tool here**.
3. **Unwalkable angle °** `45`, then press **Block steep ground on the whole island**.
4. Save, File > Save map.

## 6. Light

*Script: `bake.ps1`.* Open the **Baked light and shadows** section. Set **Gain** to `8` and **Offset** to `5`, and press **Bake all
light**. Save, File > Save map.

Bake after the last ground change. I baked after step 4 and changed the ground again later, so my island's light may differ a little from
yours.

## 7. The objects

*Script: `make_objects.ps1` (it opens the designs from `recipe/objects/`; `-Type` types the bricks in instead). Plan:
`plan/object_plan.json`.*

In **Build > Buildings and decor**, press **Make an object…** to open the **Object maker**. For each object, in this order:

1. **Open design…** and pick `recipe/objects/<name>.lbaobject`. By hand you can instead press **Start again** and, for each brick of the
   design file (one a line: cell X, layer, cell Z, LBA1 brick), type **X**, **Layer**, **Z** and **Brick**, then press **Put the brick
   here**. Those are LBA1's own bricks, cut out of scene 96.
2. Press **Add it to the island**. The status line says **Body N added…**; note N.

| Object | Design | Bricks | Body (mine) | Copies |
| --- | --- | --- | --- | --- |
| tree | `tree.lbaobject` | 24 | 106 | 43 |
| bigtree | `bigtree.lbaobject` | 24 | 107 | 1 |
| hut | `hut.lbaobject` | 81 | 120 (108 in a clean run) | 1 |
| pylon | `pylon.lbaobject` | 17 | 109 | 4 |
| fence | `fence.lbaobject` | 9 | 110 | 4 |
| fencecorner | `fencecorner.lbaobject` | 12 | 111 | 1 |
| snowman | `snowman.lbaobject` | 4 | 112 | 1 |
| smalltree | `smalltree.lbaobject` | 3 | 113 | 5 |
| stone | `stone.lbaobject` | 1 | 114 | 1 |
| rockwall1-5 | `rockwall1-5.lbaobject` | 50, 70, 50, 38, 56 | 115-119 | 1 each |

The rock wall is in five parts because a body holds at most 500 points and faces. Place each body before closing the island: the maker
keeps a body's object page in memory until a copy of it is placed.

**Placing them.** For each copy in the appendix's object table:

1. Untick **Objects follow the ground**.
2. Type the body under **Object body**, then choose the **Add object** tool.
3. Under **Brush at a vertex**: **Vertex X** = 448 + round(X / 512) and **Vertex Z** = 448 + round(Z / 512), but **at most 511**. The
   places at X or Z 32512 are on the cube's far edge, and vertex 512 is outside the cube. Press **Apply the tool here**.
4. Under **Selected object**, type **X**, **Y** and **Z** exactly, and press **Apply**.

Check the list under **Used here**: 43 of body 106, 5 of 113, and so on, **66 objects** in all. If one ever lands on top of another,
choose **Select / move** and select at the very same vertex again to step through every object there. Save, then File > Save map.

## 8. Map settings

*Script: `map_settings.ps1`.* **File > Map settings…**: **Weather** *Clear*; **Cliffs** *Ledges to walk or ride off, as LBA1's
(falling)*. Press **OK**, then File > Save map. Without this setting, the snowboarder stops at the runs' cliff edges instead of riding off
them.

## 9. Map text

*Script: `map_text.ps1`.* **File > Map text…**. For each line, type **Number** and **Words**, then press **Put this line**. Press **Save**,
then File > Save map.

| Number | Words (LBA1 island 6 text, file 9, lines 0, 23, 36, 37) |
| --- | --- |
| 1000 | Good day. |
| 1001 | I'm looking for the plans for the Teleportation Center. |
| 1002 | Use your red card to use the skilift. |
| 1003 | Sorry, I don't know where they are. |

## 10. The characters

Open **Build > Character maker…**. Make the three characters in this order, so they get the entities that follow the game folder's last
ones (347, 348 and 349 in mine). A part is added by setting **Shape** (Box unless said), **From point**, **Offset X/Y/Z**, **Size X/Y/Z**,
**Turn** and **Colour**, then pressing **Add the part**.

### Twinsen with snowboard

*Scripts: `char_rider_body.ps1`, then `char_rider_anims.ps1`.*

**Body tab:** **Game body** `1` (the tunic Twinsen), press **Start from it**. Then add three parts, all from point 0 with Turn 0:

| Part | Offset X, Y, Z | Size X, Y, Z | Colour |
| --- | --- | --- | --- |
| the board | 0, -25, 0 | 260, 50, 1000 | 70 |
| binding | 0, 8, 230 | 230, 16, 70 | 48 |
| binding | 0, 8, -230 | 230, 16, 70 | 48 |

**Body name** `Twinsen on his snowboard`, press **Set**. **Character name** `Twinsen with snowboard`. Press **Save into the map**: that
makes body 0 and a still animation 0.

**Animations tab.** The **stance** is these bone turns (Turn X, Y, Z in degrees), set with **Pose bone**, **Turn X/Y/Z** and **Same in
every keyframe**:

- bone 1: 0, *side*, 0
- bone 3: *lean*, 0, 0
- bone 4: 0, 0, *arms*
- bone 6: 0, 0, -*arms*
- bone 14: 0, 45, 0

The defaults are lean 15, arms 40, side -60. A keyframe's turn is set the same way with **Set the bone**. Its time and move are set with
**Milliseconds**, **Move X/Y/Z**, **Leaves the ground**, and the **Set** beside them. For each animation:

1. Type **Animation number** and **Animation name**, leave **Copy of game animation** empty, and press **New animation**. Animation 0
   already exists: rename it and press **Set number and name**.
2. Set the stance, then press **Add a keyframe (a copy of this one)** until it has its keyframes.
3. Set each keyframe as in the table.

| Anim | Name | Stance (lean, arms, side) | Keyframes: ms, move Z (move Y), bone changes | Loop back to |
| --- | --- | --- | --- | --- |
| 0 | Standing on the board | 15, 40, -60 | k0: 1000, 0 | – |
| 1 | Pushing off | 15, 40, -60 | k0: 500, 300 · k1: 500, 300; bone 3 25,0,0; bone 4 0,0,30; bone 6 0,0,-30 | – |
| 190 | Riding straight | 20, 40, -60 | k0: 200, 400 · k1: 200, 666; bone 3 25; bones 4/6 ±45 · k2: 200, 666 | k1 |
| 188 | Turning left | 20, 40, -60; then in every keyframe bone 1 0,-60,15 and bone 14 0,45,-10 | k0: 200, 666 · k1: 200, 666; bone 4 0,0,55; bone 6 0,0,-25 | – |
| 189 | Turning right | 20, 40, -60; then in every keyframe bone 1 0,-60,-15 and bone 14 0,45,10 | k0: 200, 666 · k1: 200, 666; bone 4 0,0,25; bone 6 0,0,-55 | – |
| 106 | Jumping | 20, 40, -60 | k0: 120, 400; bone 3 35 · k1: 160, 700 (Y 300), **in the air**; bone 3 5; bones 4/6 ±75 · k2: 160, 700 (Y 100), in the air; bone 3 10; bones 4/6 ±65 · k3: 160, 700 (Y -100), in the air; bone 3 35; bones 4/6 ±35 · k4 (a copy of k3): 160, 700 (Y -300), in the air | k4 |
| 110 | Stopping | -5, 55, -80 | k0: 200, 400; bone 3 -10 · k1: 300, 150; bone 3 -15; bones 4/6 ±60 · k2: 300, 0; bone 3 5; bones 4/6 ±30 | k2 |
| 7 | Falling | -20, 80, -60 | k0: 160, 0 · k1: 160, 0; bones 4/6 ±95; bone 3 -30 | – |
| 8 | Landing | 35, 30, -60 | k0: 120, 0 · k1: 200, 0; bone 3 15; bones 4/6 ±40 | k1 |

"Bones 4/6 ±a" means bone 4 is 0, 0, a and bone 6 is 0, 0, -a; "bone 3 n" means bone 3 is n, 0, 0. Move X is 0 everywhere, and Move Y is 0
except in the jump. For **Loop back to**, pick that keyframe and press **Loop back to it**. Press **Save into the map**.

### Slalom flag

*Script: `char_flag_board.ps1` (first half).*

1. In **Character**, pick *(a new character)* and press **Open it**. **Character name** `Slalom flag`.
2. **Body tab**, press **Start empty**, then add two parts:
   - The pole: Box, from point 0, offset 0, 750, 0, size 40, 1500, 40, turn 0, colour 16.
   - The pennant: **Flat triangle**, from point 7 (the pole's top corner), offset 250, -150, -20, size 500, 300, 0, turn 0, colour 64.
3. **Body number** 0, **Body name** `Red flag`, press **Set**.
4. Then, for each of these, type **Body number** and **Body name**, press **New body (a copy of this one)**, set **Colour** and **Recolour
   bone** 2, and press **Recolour it**:
   - 17 `Green flag`, colour 128
   - 48 `Blue flag`, colour 192
   - 49 `Light blue flag`, colour 160
5. **Animations tab:** animation 0 `Waving` (**New animation**), then **Add a keyframe**.
   - k0: 300 ms, bone 2 turned 0, 12, 0 (**Set the bone**).
   - k1: 300 ms, bone 2 turned 0, -12, 0.
6. Press **Save into the map**.

### Snowboard (the one to pick up)

*Script: `char_flag_board.ps1` (second half).*

1. A new character `Snowboard`. **Start empty**, then add four Box parts, all from point 0 with turn 0:
   - board: offset 0, 500, 0, size 280, 1000, 50, colour 64
   - edge: offset -145, 500, 0, size 12, 1000, 56, colour 128
   - binding: offset 0, 650, 30, size 220, 70, 16, colour 48
   - binding: offset 0, 350, 30, size 220, 70, 16, colour 48
2. **Body name** `Snowboard`, press **Set**, then **Save into the map**.

Then File > Save map. The characters travel in the map file and are put after the game's entries wherever the map is installed.

## 11. Points, actors and zones

Choose **Build > What to edit > Actors and zones**: the Build tab becomes the scene panel, its scene box saying which scene it is (405
in mine). Everything is placed with **Add ▾**: click the spot in the view, or type the place into the **Place X / Y / Z** bar that appears
and press **Place here**; then type the inspector's fields. A field takes its value when it loses focus. Each change is written at once
(one step on Edit > Undo).

**Track points** (*`actors.ps1`*). Use **Add > Track point** 34 times, in order, at the points in the appendix (LBA1's points 0-33).

**Twinsen's start.** In the **Actors** tab, choose actor 0 and type **X** 517, **Y** 6400, **Z** 1292.

**Actors** (*`actors.ps1`*). For each actor, use **Add > New 3D actor without a script…**. In **Choose a body**, type the filter, take the
first match and press **OK**. Then place it and type its fields. When you add the first actor, the panel fills slot 1 with the
meca-penguin's placeholder (the game clears slot 1 at every scene start), so the actors below get the numbers LBA1 gave them and the
scripts' actor numbers hold.

| Actor | What | Body filter | X, Y, Z | Flags | Life | Armour | Other |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2 | ski instructor (Rabbibunny) | `blue-white` | 20416, 256, 29216 | 0x0803 | 20 | 2 | Bonus option 48, Bonus count 2, **Move 3** |
| 3 | snowboard to pick up | `Snowboard: Snowboard` | 2864, 6400, 448 | 0x1000 | 1 | 51 | |
| 4 | flag | `Slalom flag: Red flag` | 880, 6144, 5328 | 0x1000 | 1 | 51 | |
| 5 | flag | `Slalom flag: Red flag` | 2688, 6144, 5248 | 0x1000 | 1 | 51 | |
| 6 | flag | `Slalom flag: Blue flag` | 5344, 5632, 5280 | 0x1000 | 1 | 51 | |
| 7 | flag | `Slalom flag: Blue flag` | 5264, 5632, 6800 | 0x1000 | 1 | 51 | |
| 8 | flag | `Slalom flag: Light blue flag` | 9104, 6400, 3168 | 0x1000 | 1 | 51 | |
| 9 | flag | `Slalom flag: Light blue flag` | 10304, 6400, 3072 | 0x1000 | 1 | 51 | |
| 10 | flag | `Slalom flag: Green flag` | 21856, 2816, 10416 | 0x1000 | 1 | 51 | |
| 11 | flag | `Slalom flag: Green flag` | 23920, 2816, 10320 | 0x1000 | 1 | 51 | |
| 12 | hidden bonus (invisible) | `Snowboard: Snowboard` | 11968, 6016, 1680 | 0x1200 | 1 | 51 | Bonus option 48, Bonus count 3 |
| 13 | Twinsen on his snowboard | `Twinsen on his snowboard` | 4320, 6400, 384 | 0x8803 | 1 | 51 | **Move 3** |

**Move 3** (track) makes an actor's track script run from the start. The other actors keep Move 0 and wait for their life script. (On the
first run I typed the two Move values after the rest. `actors.ps1` now types them with the other fields.)

**Zones** (*`zones.ps1`*). Use **Add > Zone > ***type***. Place it at the box's centre X, its **Y min** and its centre Z. Then type
**X min**, **X max**, **Y min**, **Y max**, **Z min**, **Z max** and the zone's own field.

| LBA1 zone | Type | X min-max | Y min-max | Z min-max | Field |
| --- | --- | --- | --- | --- | --- |
| 0 | cube change | 31488-31999 | 256-1535 | 8960-11519 | **Destination scene** = this scene's number (the scene box's), **Arrival X/Y/Z** 1504, 6144, 1408 |
| 1 | scenaric | 21248-22271 | 256-1535 | 27616-28639 | **Zone number** 5 (the ski lift) |
| 2 | scenaric | 768-2815 | 6144-7423 | 4352-5887 | Zone number 1 (a run's start) |
| 3 | scenaric | 4352-5887 | 5632-6911 | 4864-6911 | Zone number 2 (a run's start) |
| 4 | scenaric | 8960-10495 | 6400-7423 | 1792-3839 | Zone number 3 (a run's start) |
| 5 | scenaric | 256-1791 | 6400-7679 | 256-1791 | Zone number 0 (the top) |
| 6 | scenaric | 11520-12543 | 4352-5631 | 1792-2815 | Zone number 6 (the hidden bonus) |
| 7 | scenaric | 21760-23807 | 2816-4095 | 9472-11007 | Zone number 4 (a run's start) |

Then File > Save map. On save, the map stores zone 0's destination as its own scene (30000), so the zone still works wherever the map is
installed.

## 12. Scripts

*Script: `scripts.ps1 -Only 3,2,12,13,0 -SaveLast`. Text: `recipe/scripts/`.*

Five actors have scripts: 0 (Twinsen), 2 (the instructor), 3 (the board to pick up), 12 (the hidden bonus) and 13 (the rider). For each,
in the order 3, 2, 12, 13, 0:

1. In the scene panel's **Actors** tab, choose the actor and press **Edit script…**.
2. If it has a `_track.c`: switch to **Track (C)** and paste it in. Do the track first, because the life script names its labels.
3. Switch to **Life (C)** and paste `actor<N>_life.c` in.
4. Leave the window open.

The scripts name one another's labels (Twinsen's starts the rider's track and the rider's ends Twinsen's), so a script saved alone fails its
check. Type all five first, then press **Save** in the last window (actor 0's) and answer **Save scripts** with **OK**. Close the windows,
then File > Save map.

What they do, from LBA1's scripts:

- **Actor 0, Twinsen.**
  - The action key within 1250 of the instructor: Twinsen says line 1000 ("Good day."), or 1001 in LBA1's chapters 13 and 14, and the
    instructor answers.
  - Touching the board takes it (game flag 200).
  - With the board, the action key in a run's start zone (1-4) hides him and sends the rider (actor 13) down that run.
  - With the board, the action key in the lift zone (5) takes him back to the top (point 3; flag 201).
- **Actor 2, the instructor.**
  - Walks between the lift and the hut, and turns to face Twinsen when he comes within 1300.
  - Answers with line 1002 (the red card), after 1003 in chapters 13 and 14.
- **Actor 3, the board.** Gone once Twinsen has it.
- **Actor 12, the hidden bonus.** In zone 6, in discreet mode, the action key gives its bonus once.
- **Actor 13, the rider.** Hidden until a run starts, then rides it: tracks 3-6, through the track points, with the jump animation over
  the drops. At the bottom (label 111) Twinsen stands there again.

## 13. Play it

Open the **Play** tab with **Mute all sound** ticked, press **Play** and check:

- [ ] Twinsen starts at the top of the mountain. Down by the hut, the instructor walks between the lift and the hut.
- [ ] Walk into the snowboard by the start: it disappears.
- [ ] In a run's start zone, press the action key: Twinsen turns into the rider, who rides the run past the flags, jumps off the cliffs
  and stops at the bottom, where Twinsen stands again.
- [ ] Walk to the instructor and press the action key: Twinsen says "Good day.", and the instructor turns and answers with the red card
  line.
- [ ] In the lift zone by the hut, the action key takes Twinsen back to the top.
- [ ] In discreet mode in zone 6, the action key gives the hidden bonus once.

Then File > Save map one last time. The `.lbamap` in `<sandbox>\app\Maps` is the map. Put it in a game folder's `MAPS` folder, or open it
with File > Open map.

## What I did differently

The steps above are the cleaned-up order. My run went the same way except for these detours, each now folded into a step:

| What happened | Where it's handled |
| --- | --- |
| New map refused land height 256. | Step 1 uses 512. |
| The first heights took the snowy tree clumps for ground: trees on pillars. | Step 5 (I did it after the objects). |
| With **Objects follow the ground** ticked, step 5 lowered the objects with the ground, burying 31 of them. | Step 5 comes before the objects, and step 7 unticks the box. I put them back by typing their plan Y. |
| The hut's roof used LBA1's blank brick 222 (cells 0,8,0 and 0,8,1) and showed black. | The recipe's `hut.lbaobject` has 7870 and 7880 there. I added a second hut body (120) and deleted the first, so my OBL still holds the unused body 108. |
| Two small trees at X 32512 weren't placed: vertex 512 is outside the cube. | Step 7 uses vertex 511. |
| Keyframe times were typed as 50ths of a second: the snowboarder rode 20 times too fast. | Step 10 gives milliseconds. I retyped each keyframe ×20. |
| The Landing animation (8) was missed by that retyping: the Character maker's list only realizes the items in view, and the script never scrolled to the ninth. | Step 10 gives 120 ms and 200 ms. The released map was fixed through the Character maker. |
| Remaking the flag's pennant lost its Waving animation's bone turns: the released flags didn't wave. | Step 10 sets them. The released map was fixed through the Character maker. |
| The pickup snowboard's still frame kept a tick time (50 ms). | A fresh map makes it 1000 ms. The released map was fixed. |
| Animation 1 was first made standing still and named "Getting ready". | Step 10 has it pushing off: 300 forward a keyframe. |
| The rider stopped at cliff edges. | Step 10's jump keyframes are in the air, and step 8 makes the cliffs ledges. |
| The flag's pennant was lit on one side only. | Fixed in the Character maker. I remade the pennant; a fresh run gets it right the first time. |
| I lost the zones by not saving the map. | Save the map after every step. |
| Saving one script at a time failed on the other scripts' labels. | Step 12 types all, then saves once. |
| A repair run read a stale body list and added 60 objects of body 0. | I deleted them with Select / move, stepping through each stack of objects (a new feature), as step 7's last note says. |

## Where it differs from LBA1

- LBA1's inventory items (the snowboard, the red card) don't exist in LBA2. Using them is the action key, in the zones where LBA1 used
  them.
- LBA1 plays the film SURF.FLA at the lift; here Twinsen is put back at the top.
- Zone 0 leads to the rest of Hamalayi in LBA1 (scene 15). Here it leads back into the map's own scene, arriving at the top.
- `chapter()` is LBA1's chapter test kept as it is, so the instructor's chapter 13/14 line only shows in those chapters.
- Each snowy tree has a snow block beside its trunk: LBA1's brick column under the tree's back cell, kept in the design.
- Screenshots from the headless engine show a second, ghost snowboarder. The editor's Play shows one.

## How far this was checked

The whole recipe was run end to end from an empty sandbox (a fresh copy of the game folder) on 2026-10-23, and its map was compared with
the released one, file by file:

- **Identical:**
  - the ground's heights, light and atlas;
  - the 66 objects (every body, X, Y and Z);
  - the scene (actors, zones, track points and all five scripts, byte for byte);
  - all eleven animations;
  - the entity records, the characters list and the map text.
- **Different:**
  - 13 cells round the tree clumps carry another snow tile (both from LBA1's bricks: the first run's painting plan and `ground_fix.py`
    chose differently).
  - The rider's body differs by rounding in 14 bytes.
  - The released map's OBL still holds the unused first hut body (108).

For the step-by-step walk through the editor, with pictures of each window, see [HOWTO.md](HOWTO.md).

## Checking against LBA1

These read LBA1 and only check:

- `ScriptRoundTrip lba1ground <LBA1 folder> 96 <out>` wrote `ground.csv`, `bricks.csv` and `tops.txt`. The plans were made from them.
- `ScriptRoundTrip lba1sceneactors` and `hamalayiscripts` gave LBA1's actors, track points, zones and scripts (`actor_ref.json`,
  `zone_plan.json`, the scripts' logic).
- `plan/ground_fix.py <ILE>` lists every vertex that differs from LBA1's ground. 0 on the finished island.
- `ScriptRoundTrip sceneactors <workspace> 405 points` and `zonedump <workspace> 405` print the scene's actors, points and zones, to
  compare with the tables above.

## Appendix: the atlas slots

Tile X, Y are pixels in the 256 x 256 atlas. Slot 19 is the sea's.

| Slot | Tile X, Y | Brick | Paint runs |
| --- | --- | --- | --- |
| 0 | 0, 0 | cliff: 3033 over 3034 | 230 |
| 1 | 32, 0 | cliff: 3040 over 3041 | 232 |
| 2 | 64, 0 | cliff: 3042 over 3048 | 221 |
| 3 | 96, 0 | cliff: 3049 over 3050 | 220 |
| 4 | 128, 0 | top of 2721 | 197 |
| 5 | 160, 0 | top of 2722 | 200 |
| 6 | 192, 0 | top of 2713 | 360 |
| 7 | 224, 0 | top of 2720 | 185 |
| 8 | 0, 32 | top of 2719 | 179 |
| 9 | 32, 32 | top of 2714 | 170 |
| 10 | 64, 32 | top of 2711 | 163 |
| 11 | 96, 32 | top of 2712 | 148 |
| 12 | 128, 32 | top of 2715 | 104 |
| 13 | 160, 32 | top of 2726 | 98 |
| 14 | 192, 32 | top of 2717 | 98 |
| 15 | 224, 32 | top of 2718 | 97 |
| 16 | 0, 64 | top of 2724 | 93 |
| 17 | 32, 64 | top of 2725 | 92 |
| 18 | 64, 64 | top of 2716 | 87 |
| 20 | 128, 64 | top of 2723 | 85 |
| 21 | 160, 64 | top of 3075 | 45 |
| 22 | 192, 64 | top of 3051 | 41 |
| 23 | 224, 64 | top of 3074 | 43 |
| 24 | 0, 96 | top of 3073 | 42 |
| 25 | 32, 96 | top of 3035 | 36 |
| 26 | 64, 96 | top of 3043 | 36 |
| 27 | 96, 96 | top of 2830 | 18 |
| 28 | 128, 96 | top of 2831 | 18 |
| 29 | 160, 96 | top of 2842 | 18 |
| 30 | 192, 96 | top of 2843 | 18 |
| 31 | 224, 96 | top of 3158 | 16 |
| 32 | 0, 128 | top of 2937 | 14 |
| 33 | 32, 128 | top of 2938 | 14 |
| 34 | 64, 128 | top of 2941 | 14 |
| 35 | 96, 128 | top of 2942 | 14 |
| 36 | 128, 128 | top of 3161 | 14 |
| 37 | 160, 128 | top of 3167 | 14 |
| 38 | 192, 128 | top of 2935 | 13 |
| 39 | 224, 128 | top of 2936 | 13 |
| 40 | 0, 160 | top of 2939 | 13 |
| 41 | 32, 160 | top of 2940 | 13 |
| 42 | 64, 160 | top of 3160 | 13 |
| 43 | 96, 160 | top of 3166 | 12 |
| 44 | 128, 160 | top of 3099 | 12 |
| 45 | 160, 160 | top of 2731 | 11 |
| 46 | 192, 160 | top of 3165 | 11 |
| 47 | 224, 160 | top of 2959 | 11 |
| 48 | 0, 192 | top of 3159 | 11 |
| 49 | 32, 192 | top of 2962 | 11 |
| 50 | 64, 192 | top of 2946 | 10 |

## Appendix: the objects' places (X, Y, Z)

- **tree** (43): 17408, 2816, 512; 19456, 2816, 512; 20480, 2816, 512; 18432, 2816, 1024; 21504, 2816, 1024; 26112, 512, 3584;
  29696, 256, 5120; 28672, 256, 7680; 31744, 256, 8704; 31744, 256, 12288; 512, 2816, 12800; 4608, 2816, 12800; 3072, 2816, 13824;
  7168, 2816, 13824; 512, 2816, 14336; 1536, 2816, 15360; 5120, 2816, 15360; 1024, 2816, 16384; 1024, 1024, 20480; 1536, 1024, 21504;
  3584, 1024, 21504; 512, 1024, 22016; 7168, 1024, 22016; 19456, 512, 22016; 6144, 1024, 22528; 1536, 1024, 23040; 4096, 1024, 23552;
  12288, 1024, 23552; 512, 1024, 24064; 2048, 1024, 24064; 1024, 1024, 25088; 6656, 1024, 25088; 512, 1024, 26112; 26112, 256, 26624;
  28672, 256, 27136; 4608, 1792, 27648; 9216, 1024, 28160; 29696, 256, 30208; 28160, 256, 30720; 25600, 256, 31232; 4608, 1792, 31744;
  10752, 1024, 31744; 31744, 256, 31744
- **bigtree**: 12288, 4352, 1536
- **hut**: 20736, 256, 28416
- **pylon** (4): 2816, 1792, 30976; 9472, 1024, 30464; 15616, 256, 30464; 20736, 256, 30464
- **fence** (4): 21248, 256, 26880; 22784, 256, 26880; 22784, 256, 28416; 22784, 256, 29952
- **fencecorner**: 22016, 256, 30976
- **snowman**: 22528, 2048, 256
- **smalltree** (5): 8960, 5632, 4352; 23296, 2816, 6400; 32512, 256, 8960 (vertex X 511); 32512, 256, 13056 (vertex X 511);
  28928, 1024, 15104
- **stone**: 3328, 1792, 31488
- **rockwall1-5**: 23808, 768, 512; 26368, 768, 512; 28928, 768, 512; 31488, 768, 512; 32256, 256, 2048

## Appendix: the track points (X, Y, Z)

| # | Point | # | Point | # | Point |
| --- | --- | --- | --- | --- | --- |
| 0 | 17408, 256, 29184 | 12 | 10128, 2816, 13712 | 24 | 12672, 256, 26624 |
| 1 | 15360, 256, 26624 | 13 | 23856, 256, 24768 | 25 | 13312, 512, 30720 |
| 2 | 20480, 256, 29184 | 14 | 10560, 1024, 26368 | 26 | 15872, 256, 31744 |
| 3 | 1504, 6400, 1408 | 15 | 16448, 256, 30016 | 27 | 9728, 6400, 2560 |
| 4 | 1760, 6144, 4752 | 16 | 19920, 256, 31936 | 28 | 9712, 5632, 4768 |
| 5 | 3232, 5376, 8624 | 17 | 7824, 2816, 18576 | 29 | 15360, 2560, 15120 |
| 6 | 7056, 4864, 9072 | 18 | 23552, 256, 30720 | 30 | 20480, 512, 15872 |
| 7 | 10912, 2816, 11616 | 19 | 24064, 256, 26624 | 31 | 22384, 1536, 13824 |
| 8 | 4608, 6400, 512 | 20 | 4736, 5632, 5952 | 32 | 24192, 256, 23120 |
| 9 | 7728, 2816, 16352 | 21 | 10576, 3592, 6640 | 33 | 22528, 2816, 9728 |
| 10 | 8224, 1040, 21776 | 22 | 12416, 1760, 18384 | | |
| 11 | 8256, 1024, 24880 | 23 | 11184, 1024, 23696 | | |
