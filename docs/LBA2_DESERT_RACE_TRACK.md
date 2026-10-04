# LBA2 Desert island race track: what it is made of

Groundwork for a future feature. Everything below was read from the pristine data (`DESERT.ILE`, `SCENE.HQR`, `RESS.HQR`, `DESERT.OBL`), not guessed from screenshots.

Pictures: [route and decor boxes](racetrack/cube12_route.png), [component maps](racetrack/cube12_components.png), [decor bodies](racetrack/rt_decor_bodies.png). Per-component cell lists: [desert_cube12_components.json](racetrack/desert_cube12_components.json).

Tools: `ScriptRoundTrip racetrack 7 10 [--scripts]` lists every scene, actor, zone and route point sitting on the cube. `LBAAssembler --export <folder> --category placed --only "cube (7,10)"` exports each placed body.

## Where it is

The whole track is **one island cube**: map cell (7,10) of `DESERT.ILE`, which is cube id 12. A cube is 64 x 64 cells of 512 units (32768 units). Nothing of the track is in another cube. The cube has 10 decor objects, 56 texture definitions and a 65 x 65 height grid (0..6800).

Scene: **57, "White Leaf Desert, at the Von Kournil tour"** (island 2, exterior cube (7,10)). (Scenes 46 and 196 also list cube (7,10), but on island 0, the Citadel island, so they are unrelated.)

## Ground (polygon words of the cube)

All ground is drawn from the ILE ground atlas (record 1, 256 x 256, island palette RESS entry 29) or from flat palette colours (`(bank << 4) + 11`). Polygon fields: Bank bits 0-3, TexFlag 4-5, PolyFlag 6-7, texture def index bits 19-31.

| Piece | How it is drawn | Extent |
|---|---|---|
| **Track surface (asphalt)** | atlas rect (96,0)-(127,31), mean colour (77,74,66); TexFlag 1 and 2 (two orientations of one tile), plus a few TexFlag 3 | 2387 half cells, cells x 6..58, z 6..58 |
| **Red curb (inner line, red dashes)** | flat bank 4, colour (200,76,72), PolyFlag 3 | 167 half cells, x 10..57, z 11..52 |
| **White curb (inner line, white dashes)** | one white texel of the atlas, (180,155), colour (252,252,252), stretched over the cell | 161 half cells, x 13..59, z 11..51 |
| **Start / finish line** | the same white texel, row z = 29, cells x 52..59 (8 cells wide, one cell deep), TexFlag 3 | 16 half cells |
| **Orange arrows** | flat bank 5, colour (248,164,120); five patches of 4 x 4 cells | cells (9..12, 47..50), (14..17, 9..12), (32..35, 37..40), (48..51, 12..15), (48..51, 52..55) |
| **Red/gold hashing** | atlas tile (192,48)-(207,63), mean colour (164,100,51); two diagonal strips on the outer corners | 167 half cells, top right and left middle |
| **Gold flat edging** | flat bank 6, colour (244,184,84) | 194 half cells, cells x 0..13 along the west side |
| **Sand (infield and outside)** | flat bank 2, colour (224,200,160), PolyFlag 0 or 3 | ~3000 half cells |
| **Rock / cliff** | 32 x 32 atlas tiles in rows y = 128..191 (12 variants, three orientations) | ~2160 half cells, the banks either side of the road |
| **Sea** | PolyFlag 0 with TexFlag 0 (nothing drawn) | the west and south edge |

The curbs alternate by cell, so the red/white dash line is two separate polygon types, not one texture. The arrows are flat-coloured, not textured.

Heights: the road follows the terrain exactly (route point height is within about 100 units of the height grid everywhere). It climbs from Y 1785 at the start line to Y 6418 on the west side and comes back down. **The track never crosses itself on the ground** (checked segment against segment), so there is no separate bridge in the height grid; the "loop over the track" is a decor object (below).

## Decor objects (cube 12, `DESERT.OBL` bodies)

Positions are cube-local. Body numbers are `DESERT.OBL` indices.

| Body | What it is | Position (x, y, z) / box | Role |
|---|---|---|---|
| 64 | horizontal beam, 5.4 x 0.85 units | (31296,1785,15360); box y 4926..5775 | top of the **start gantry** |
| 65 | thin post | same origin; x 30872..31155, y 1813..4926 | east post of the start gantry |
| 66 | thin post | same origin; x 25777..26060, y 1273..4926 | west post of the start gantry |
| 68 | arched bridge deck, 2.9 x 1.0 x 1.2 | (9728,5270,13312); box x 6229..9112, y 7530..8558 | **the overpass arch over the track** on the west straight ("the other loop over the track") |
| 69 | pillar / ramp | box x 5613..6229, y 5953..7962 | west abutment of the arch |
| 70 | pillar / ramp | box x 9112..9727, y 5355..7962 | east abutment of the arch |
| 67 | long low wall or banner, 3.6 x 1.35 x 0.5 | (13312,5440,3584); box x 9718..13312, y 5440..6781 | trackside billboard on the north straight |
| 71 | wedge, 0.5 x 0.8 x 2.6 | (22528,3230,21504); box z 18940..21504 | trackside wedge / small ramp beside the lower bend |
| 36 | ornamental metal lamp / sculpture | (31488,1785,20224), turn 1024 | at the garage entrance (zone 3 -> scene 54) |
| 1 | small crystal-like prop, 0.36 units | (5856,880,31168), hidden by variable | beside the "Desert sphero" actor, off the track |

The gantry is three bodies with a shared origin, the arch is three bodies with a shared origin.

The red-white striped look on the arch and gantry comes from the object texture atlas (ILE record 2), not from separate objects.

## Scene 57 (actors, zones, route)

**Actors**
- 1: Zoe placeholder, position (0,0,0).
- 2: invisible dummy (entity 16, no body) at (22528,4608,29696). Its life script removes it outside chapter 4 and otherwise only saves and restores its track position in game variable 80; its 784-byte track script is 97 labels of `WAIT_NB_DIZIEME(4,0)` looping. It looks like a placeholder that keeps a marker, not part of the race.
- 3: **the buggy** (entity 152, body 219, listed as "Clover box" in `BODY2.HQD`), start (27904,1785,17152), turn 2048. Life script: if `VAR_GAME(74) >= 3` it exists; `INIT_BUGGY(0/2)`; when `VAR_GAME(168) == 1` (race running) it starts at route point 42 turn 2048.
- 4: the attendant (entity 157, body 227) at (28800,2040,15872). Its life script keys on `VAR_GAME(168)` (1 = the buggy is out): it moves to route point 41 when 168 is 1, and otherwise walks its own track script over route points 5.. in order. Offering `ASK_CHOICE(479)` it takes **40 gold** (`GIVE_GOLD_PIECES(40)`) and sets `VAR_GAME(168)` to 0, i.e. it is the buggy hire/return exchange. Dialogue ids 479..489. Not decoded further.
- 5: "Desert sphero #1" (entity 154, body 221), at (5856,880,31168), the prop of decor 1.

**Zones**
- 4: scenario zone (type 2, num 0) box (26624,1579,15360)-(30208,2603,18432): the start area, in front of the gantry.
- 8: scenario zone (type 2, num 1) box (26624,1579,14336)-(30208,2603,14848): just past the start line.
- 5: fixed camera (type 1, number 0, on and forced) over (25600..30208, 16384..18432), the start straight in front of the garage/pit. (Earlier versions of this page called zones 5 and 3 scene changes; in LBA2 type 1 is a camera zone.)
- 3: fixed camera (type 1, number 1) over (31232..31744, 19968..20480), the garage entrance; message zone 7 names it.
- 6 and 7: type 5 zones, num 228, flags 12/1/8 and 12/1/4, at the garage doors (31744..32256 and 30720..31232, 19728..20752).
- 0, 1, 2: the island edge scene changes (type 0) to scenes 56, 58, 62.

**Route points (37 route points 4..40 are the lap, in order)**
0 (19968,1785,12800), 1 (22560,1785,15152), 2 (20224,1360,9984), 3 (17664,1742,14080) are helper points off the lap. **Lap: points 4..40**, from (28416,12544) north, west along the top, south down the west side, east along the bottom, and back up the east side (see the picture). 41 (29584,1785,17120) is the attendant's post, 42 (27904,1785,17152) is the buggy's start.

## Sources of the picture the request came from

The dotted red route with flags is the editor's own drawing of track points, the pink boxes are the scenario zones, and the black figure at the left edge is the invisible dummy actor's marker; none of them exist in the game world.

## Open items (needs a live look before building on it)

- Confirm in play which decor pieces are solid (the start gantry and arch have no `Col` bit checks yet).
- The lap counter logic lives in game variable 80 and scene 55/54 scripts; not decoded.
- The two hashing strips: confirm on screen that (192,48) is the "red/gold hashing" the user means rather than gold flat bank 6.
