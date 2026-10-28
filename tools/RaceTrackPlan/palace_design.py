"""The Emperor's palace race (the user, 2026-10-12: "through the palace rooms, coming out the window and around the roof tops?
Point-to-point i.e. no laps, finishing with massive jump to harbor teleport? - Twinsen, quick, Dark monk's in the palace, he's killing
everybody. Leading into final showdown at Celebration"), on the Palace island (Terrain/Palace: PALACE.ILE, its layout PALACE.JSON).

Versions 1 to 12 (2026-10-12 to 2026-10-14) went out of the round window onto a raised road round the roof's turrets and rooftops, ending
in a leap off the roof: archived in docs/racetrack/palace_outside_archive (the git tag palace-outside-v12). In the rooms, since the third:
no road -- the cars drive the rooms' floor where they like (the plan's bare and openAreas: RACEMOD.CPP open_area=), arrows on the floor
showing the ways on, the opponents each their own way (openRoutes); the floor's red the route's only (PalaceIsland TrackRow, TrackColumn);
guards in the rooms (guards); the secret warp between room 151's fireplaces (teleports).

Thirteenth version, 2026-10-15 (the user: "The outside of the palace track just isn't working ... drop it totally. Let's add an upstairs to
the inside of the palace ... a couple of bedrooms, a throne room, and any other rooms you can think of that would fit with a palace
theme"): the palace two storeys high (PalaceUpstairs) and the race through both, inside only:
  - from the grid in front of the palace's doors in room 154 west along the bottom row (153, 152, 151) and north into room 155 -- a
    stairwell, open to the roof -- and up the ramp winding round its column (UP_TURNS round; the race-track mode's raised road, held on
    its rails) to the upstairs;
  - out of the stairwell south into the library, east through the music room, the princess's bedroom and the Emperor's bedchamber, north
    into his dressing room, west into the throne room -- past the throne on its dais -- north and west into the other stairwell, room
    159's, and down its ramp (DOWN_TURNS round);
  - out of it north through room 163 into the long hall, two storeys high now, and up its ramp to the round window: the finish line in
    the window, the leap out of it over the cloud sea -- won, the Dark Monk's teleporter takes the car in mid-flight (the race-track
    mode's sprint, wake=, wake_flight=); lost, the race is run again from the grid before it comes down;
  - the opponents' other ways: through rooms 158 and 157 before the bottom row's last rooms; upstairs north from the dressing room through
    the treasury into the throne room, or on through the armoury and the banquet hall and south into it.

Usage: palace_design.py <game folder with PALACE.ILE and PALACE.JSON: the island built on its own, `ScriptRoundTrip palace`> [out plan]
"""
import json, math, sys
import numpy as np
sys.path.insert(0, __file__.rsplit('\\', 1)[0] if '\\' in __file__ else '.')
from ile import load_ile, hqr_entries

GAME = sys.argv[1] if len(sys.argv) > 1 else r'E:\dump\TEMP\pal\up1'
OUT = sys.argv[2] if len(sys.argv) > 2 else r'E:\dump\LBAAssembler\docs\racetrack\palace_track_plan.json'
L = json.load(open(GAME + r'\PALACE.JSON'))
_, _, _, CUBES = load_ile(GAME + r'\PALACE.ILE')
SP = 0.5                                    # the plan's points, cells apart
FLOOR = L['floorTop']                       # the ground floor: what the cars drive on in its rooms
UP = L['upstairs']
UPF = UP['floorTop']                        # the upstairs' floor
DECK = 60                                   # the raised road over what it lies on
HALF = 4.5                                  # the road's half width (cells): the ramps', the other tracks' width
HALF_GRID = 3.5                             # ... the grid's places two abreast
JUMP_APEX = 1400                            # the leap's top over its lip
FLIGHT = 80                                 # the leap's length (cells): a car at three times the race car's speed is still in the air when
                                            # the dream fades (wake_flight=, 2.4 s)
PAD = 12                                    # where the leap comes down (no road: never reached)
RAMP = 80                                   # the ramp up the long hall to the round window: its length (cells) ...
WINDOW_H = 14600                            # ... and the road's height through the window (its opening 11,920 to 17,040)
UP_TURNS, DOWN_TURNS = 1.5, 1.75            # the stairwells' ramps: times round (up: in heading north, out heading south; down: in heading
                                            # west, out heading north)
# (a car's collision box: some 1,420 units either way of its middle whichever way it faces: 2.8 cells. Where the cars drive the floor,
# nothing within CAR_HALF of a line they follow; on the ramps nothing within the road's half-width less the rail's inset and a box)
CAR_BOX = 2.8
CAR_HALF = 1.7

# ---- where things are
rooms = {r['scene']: r for r in L['rooms']}
def mid(scene):
    r = rooms[scene]; return ((r['x0'] + r['x1'] + 1) / 2, (r['z0'] + r['z1'] + 1) / 2)
# (a row's doors are in a line 2 cells past its rooms' middles, a column's the same: the rooms' grids put them there -- upstairs too)
def row(scene): return mid(scene)[1] + 2
def col(scene): return mid(scene)[0] + 2
win = L['window']
wx = (win['x0'] + win['x1']) / 2            # the round window's middle across, the wall's inner face and outer
w_in, w_out = win['z1'], win['z0']
hall = rooms[80]
SIXTEEN = [s for s in rooms if s != 80]
sq_x0, sq_x1 = min(rooms[s]['x0'] for s in SIXTEEN), max(rooms[s]['x1'] for s in SIXTEEN) + 1
sq_z0, sq_z1 = min(rooms[s]['z0'] for s in SIXTEEN), max(rooms[s]['z1'] for s in SIXTEEN) + 1
STAIR_UP = next(s for s in UP['stairs'] if s['up'])
STAIR_DOWN = next(s for s in UP['stairs'] if not s['up'])
up_rooms = {r['name']: r for r in UP['rooms']}

# ---- the boxes: what a car hits, by height (decors with a real box; world units)
boxes = []
read, _ = hqr_entries(GAME + r'\PALACE.ILE')
cubemap = np.frombuffer(read(0)[:256], dtype=np.uint8).reshape(16, 16)
for cz in range(16):
    for cx in range(16):
        cid = cubemap[cz, cx] & 127
        if not cid or cid not in CUBES: continue
        for d in CUBES[cid].decors:
            if d[10] <= d[7]: continue
            boxes.append((cx * 32768 + d[6], d[7], cz * 32768 + d[8], cx * 32768 + d[9], d[10], cz * 32768 + d[11]))
boxes = np.array(sorted(set(boxes)), float)
print('collision boxes', len(boxes))


def blocked(x, z, y, half):
    """Whether a box stands in a car's way at (x, z) cells, its floor y: within `half` cells of the point, between y+120 and y+900."""
    X, Z = x * 512, z * 512
    hit = (boxes[:, 0] < X + half * 512) & (boxes[:, 3] > X - half * 512) & (boxes[:, 2] < Z + half * 512) & (boxes[:, 5] > Z - half * 512) \
          & (boxes[:, 1] < y + 900) & (boxes[:, 4] > y + 120)
    return bool(hit.any())


def blocked_road(x, z, y, dx, dz, across):
    """Whether a box stands in a car's way on a road at (x, z) cells heading (dx, dz) (a unit vector), its floor y: the car's middle anywhere
    up to `across` cells either side of the road's middle, and its box (CAR_BOX either way, axis-aligned, as the engine has it) round that."""
    ex, ez = (across * abs(dz) + CAR_BOX) * 512, (across * abs(dx) + CAR_BOX) * 512
    X, Z = x * 512, z * 512
    hit = (boxes[:, 0] < X + ex) & (boxes[:, 3] > X - ex) & (boxes[:, 2] < Z + ez) & (boxes[:, 5] > Z - ez) & (boxes[:, 1] < y + 900) & (boxes[:, 4] > y + 120)
    return bool(hit.any())


def path(corners, radii, sp=SP):
    """A line through `corners` (cells), each inner one rounded to its radius (radii[i] for corners[i]; the ends' unused): straight runs
    and circular arcs, sp cells apart."""
    P = [np.array(c, float) for c in corners]
    out = [P[0]]
    cut = [0.0] * len(P)
    for i in range(1, len(P) - 1):
        a, b, c = P[i - 1], P[i], P[i + 1]
        u = (b - a) / np.linalg.norm(b - a); v = (c - b) / np.linalg.norm(c - b)
        turn = math.atan2(u[0] * v[1] - u[1] * v[0], float(u @ v))
        r = radii[i]; cut[i] = r * math.tan(abs(turn) / 2)
        p0 = b - u * cut[i]
        centre = p0 + np.array([-u[1], u[0]]) * r * (1 if turn > 0 else -1)
        a0 = math.atan2(p0[1] - centre[1], p0[0] - centre[0])
        m = max(2, int(math.ceil(abs(turn) * r / (sp / 4))))
        out.extend(centre + r * np.array([math.cos(a0 + turn * k / m), math.sin(a0 + turn * k / m)]) for k in range(m + 1))
    out.append(P[-1])
    for i in range(len(P) - 1):
        if cut[i] + cut[i + 1] > np.linalg.norm(P[i + 1] - P[i]) + 1e-6:
            raise ValueError(f'corners {tuple(P[i])} and {tuple(P[i + 1])}: their arcs overlap ({cut[i]:.1f} + {cut[i + 1]:.1f} cells)')
    pts = [out[0]]
    for q in out[1:]:
        if np.linalg.norm(q - pts[-1]) > 1e-6: pts.append(q)
    pts = np.array(pts)
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))]
    s = np.arange(0, d[-1] + 1e-9, sp)
    return np.stack([np.interp(s, d, pts[:, 0]), np.interp(s, d, pts[:, 1])], 1)


def helix(cx, cz, r, phi0, turns):
    """A stairwell's ramp seen from above: round (cx, cz) at radius r from the angle phi0 (0 the east point, -pi/2 the north one), the
    angle falling -- round to the left: heading north at the east point -- `turns` times, sp cells apart, without its first point."""
    phi = np.linspace(phi0, phi0 - 2 * math.pi * turns, 20000)
    c = np.stack([cx + r * np.cos(phi), cz + r * np.sin(phi)], 1)
    s = np.r_[0, np.cumsum(np.linalg.norm(np.diff(c, axis=0), axis=1))]
    k = np.arange(SP, s[-1] + 1e-9, SP)
    return np.stack([np.interp(k, s, c[:, 0]), np.interp(k, s, c[:, 1])], 1)


def join(*pieces):
    pts = pieces[0]
    offsets = [0]
    for p in pieces[1:]:
        offsets.append(len(pts) - (1 if np.linalg.norm(p[0] - pts[-1]) < 1e-6 else 0))
        pts = np.concatenate([pts, p[1:] if np.linalg.norm(p[0] - pts[-1]) < 1e-6 else p])
    return pts, offsets


# ---- the route
R0 = row(154)                               # the bottom row's door line
C3, C2 = col(154), col(153)
TURN = 12                                   # a turn at a room's middle: its radius (cells)
START_X = C3 + 3
GRID_Z = R0
BEHIND = (L['entrance'][0] if L.get('entrance') else rooms[154]['x1']) - 12   # (the camera behind the grid clear of the doors' frame)
if START_X // 64 != BEHIND // 64:
    raise ValueError(f'the grid ({START_X}..{BEHIND}) crosses a cube edge')
ux, uz, ur = STAIR_UP['x'], STAIR_UP['z'], STAIR_UP['radius']
dx_, dz_, dr = STAIR_DOWN['x'], STAIR_DOWN['z'], STAIR_DOWN['radius']
FOOT_X = ux + ur                            # the up-ramp's foot: its east point, on room 151's north door's line (the door x 373..388)
TOP_X = ux - ur                             # ... its top: its west point, out south into the library
DOWN_Z = dz_ - dr                           # the down-ramp's top: its north point, in from the throne room heading west
OUT_X = dx_ + dr                            # ... its foot: its east point, out north through room 159's north door
ROW_UP = R0                                 # the upstairs' bottom row (the library, the music room, the bedrooms) on the ground's line
COL_E = col(154)                            # the bedchamber's and the dressing room's column
ROW_DR = row(158)                           # the dressing room's row, into the throne room
COL_T = col(156)                            # the throne room's west column: north up it to the down-stairwell's row
# (ground floor: the grid, west along the bottom row, north at the up-ramp's foot's line into the stairwell)
ground_in = path([(BEHIND, GRID_Z), (FOOT_X, R0), (FOOT_X, uz)], [0, TURN, 0])
up_ramp = helix(ux, uz, ur, 0.0, UP_TURNS)
# (upstairs: south out of the stairwell, east along the bottom row, north up the east column, west along the dressing room's row into the
# throne room, north up its west column, west into the down-stairwell to its ramp's top)
upstairs = path([(TOP_X, uz), (TOP_X, ROW_UP), (COL_E, ROW_UP), (COL_E, ROW_DR), (COL_T, ROW_DR), (COL_T, DOWN_Z), (dx_, DOWN_Z)],
                [0, TURN, TURN, TURN, TURN, TURN, 0])
down_ramp = helix(dx_, dz_, dr, -math.pi / 2, DOWN_TURNS)
# (ground floor again: north out of the stairwell through room 163 into the long hall, onto the round window's line, up the hall)
# (straight over onto the window's line just past the stairwell's foot, which sits 1.5 cells past the cube edge at x 384: a line along that
# edge -- north, then over to the window's line further on -- had the car changing cubes back and forth every half second, 2026-10-15
# later; the window's line is 3 cells inside the cube west of it)
JOG_AT, JOG = 3, 10
after = path([(OUT_X, dz_), (OUT_X, dz_ - JOG_AT), (wx, dz_ - JOG_AT - JOG), (wx, w_out)], [0, 12, 12, 0])
leap = path([(wx, w_out), (wx, w_out - FLIGHT - PAD)], [0, 0])
pts, (o_in, o_up, o_upstairs, o_down, o_after, o_leap) = join(ground_in, up_ramp, upstairs, down_ramp, after, leap)
n = len(pts)
S = np.r_[0, np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))]


def nearest(p, lo=0, hi=None):
    hi = n if hi is None else hi
    return lo + int(np.argmin(np.hypot(pts[lo:hi, 0] - p[0], pts[lo:hi, 1] - p[1])))


start = nearest((START_X, GRID_Z), 0, 200)
i_up_foot, i_up_top = o_up - 1, o_upstairs
# (the decks over the stairwells' open middles run on 3 cells into the upstairs doors' bands, where there is floor; the floor bare between)
up_door = next(d for d in UP['doors'] if d['a'] == STAIR_UP['scene'])
down_door = next(d for d in UP['doors'] if d['b'] == STAIR_DOWN['scene'])
i_up_floor = next(i for i in range(i_up_top, n) if pts[i][1] >= up_door['z0'] + 3)
i_down_floor = max(i for i in range(i_up_floor, o_down) if pts[i][0] >= down_door['x1'] - 3)
i_down_top, i_down_foot = o_down, o_after
i_ramp_foot = nearest((wx, w_in + RAMP + 8), o_after); i_ramp_top = nearest((wx, w_in + 8), i_ramp_foot)
i_window_in = nearest((wx, w_in), i_ramp_foot); lip = nearest((wx, w_out), i_window_in)
land = nearest((wx, w_out - FLIGHT), lip); land_foot = n - 1

# ---- heights: the ground floor's rooms (no road there); the up-ramp from the floor to the upstairs' floor, steady but for its ends; the
# upstairs (no road); the down-ramp the same way down; the ground floor; the ramp up the long hall to WINDOW_H, level through the window;
# the leap
def ease(t): return t * t * (3 - 2 * t)
def ramp(t, e=0.12):
    """A climb steady in its middle, easing in and out over its first and last `e` (the steepest 1 / (1 - e) times the mean)."""
    v = 1 / (1 - e)
    if t < e: return v * t * t / (2 * e)
    if t > 1 - e: return 1 - v * (1 - t) ** 2 / (2 * e)
    return v * (e / 2 + t - e)
def climb(a, b, h0, h1, shape=ease):
    for i in range(a, b + 1):
        h[i] = h0 + (h1 - h0) * shape((S[i] - S[a]) / max(1e-6, S[b] - S[a]))
h = np.full(n, FLOOR, float)
climb(i_up_foot, i_up_top, FLOOR, UPF, ramp)
h[i_up_top:i_down_top + 1] = UPF
climb(i_down_top, i_down_foot, UPF, FLOOR, ramp)
climb(i_ramp_foot, i_ramp_top, FLOOR, WINDOW_H, ramp)
h[i_ramp_top:lip + 1] = WINDOW_H
for i in range(lip, land + 1):
    t = (S[i] - S[lip]) / max(1e-6, S[land] - S[lip]); h[i] = WINDOW_H + JUMP_APEX * 4 * t * (1 - t)
h[land:] = WINDOW_H

# ---- widths (cells from the road's middle to its rail): the other tracks' everywhere (no road in the rooms: the width only places the
# grid and the scenes); the grid's places two abreast
half = np.full(n, HALF)
for i in range(0, n):
    if i <= start + 6: half[i] = HALF_GRID
    elif i <= start + 26: half[i] = HALF_GRID + (HALF - HALF_GRID) * (i - start - 6) / 20

# ---- the opponents' ways (RaceTrackBuilder.RouteThrough: each replaces an opponent's line up to its nearest point to the way's end): the
# ground floor's detour north through 158 and west through 157 back to the bottom row in 153, the route on up the ramp and through the
# upstairs to the dressing room, then north through the treasury and west into the throne room; or the route to the dressing room, north
# through the treasury and the armoury, west into the banquet hall and south through its door into the throne room
C1 = col(156)
R1 = row(158)
detour = path([(BEHIND, GRID_Z), (C3, GRID_Z), (C3, R1), (C2, R1), (C2, R0), (FOOT_X, R0), (FOOT_X, uz)], [0, 7, TURN, TURN, TURN, TURN, 0], 1.0)
i_dressing = nearest((COL_E, ROW_DR), o_upstairs)
main_to_dressing = pts[o_up:i_dressing + 1:2]
TREASURY_Z = row(162) + 8                   # (its door into the throne room two bricks south of the row's: clear of the throne's dais)
via_treasury = path([(COL_E, ROW_DR), (COL_E, TREASURY_Z), (COL_T, TREASURY_Z)], [0, TURN, 0], 1.0)
via_banquet = path([(COL_E, ROW_DR), (COL_E, row(166)), (col(164), row(166)), (col(164), DOWN_Z)], [0, TURN, TURN, 0], 1.0)
routes = [np.concatenate([detour, main_to_dressing, via_treasury]),
          np.concatenate([pts[:i_dressing + 1:2], via_banquet])]

# ---- arrows on the floor: the ground floor's ways on -- along the bottom row west, north into the stairwell, the other rooms' south (to
# the bottom row: rooms 159 and 163 and the long hall are only reached down the stairs) -- and from the stairs on north to the long hall;
# upstairs along the race's way and the others'
arrows = []
for s in SIXTEEN:
    cx, cz = col(s), row(s)
    if s in (159, 163, 155): continue
    if s == 154: arrows += [(cx - 16, cz, FLOOR, -1, 0), (cx, cz - 10, FLOOR, 0, -1)]
    elif s in (151, 152, 153): arrows.append((cx - 12, cz, FLOOR, -1, 0))
    else: arrows.append((cx, cz + 12, FLOOR, 0, 1))
arrows += [(FOOT_X, row(151) - 14, FLOOR, 0, -1), (FOOT_X, uz + 10, FLOOR, 0, -1), (OUT_X, mid(163)[1] + 4, FLOOR, 0, -1),
           (wx, hall['z1'] - 40, FLOOR, 0, -1), (wx, (hall['z0'] + hall['z1']) / 2 + 20, FLOOR, 0, -1)]
arrows += [(TOP_X, ROW_UP - 14, UPF, 0, 1), (col(151) + 8, ROW_UP, UPF, 1, 0), (col(152), ROW_UP, UPF, 1, 0), (col(153), ROW_UP, UPF, 1, 0),
           (COL_E - 12, ROW_UP, UPF, 1, 0), (COL_E, ROW_UP - 14, UPF, 0, -1), (COL_E, ROW_DR + 12, UPF, 0, -1), (COL_E - 14, ROW_DR, UPF, -1, 0),
           (col(157), ROW_DR, UPF, -1, 0), (COL_T + 14, ROW_DR, UPF, -1, 0), (COL_T, ROW_DR - 14, UPF, 0, -1), (COL_T, row(160) + 4, UPF, 0, -1),
           (COL_T - 14, DOWN_Z, UPF, -1, 0),
           (COL_E, row(162) + 14, UPF, 0, -1), (COL_E - 12, TREASURY_Z, UPF, -1, 0), (COL_E, row(166) + 8, UPF, 0, -1), (COL_E - 12, row(166), UPF, -1, 0),
           (col(165), row(166), UPF, -1, 0), (col(164), row(166) + 12, UPF, 0, 1)]
# (only those on the race's own way, each moved onto its middle and turned the way it runs there -- the user, 2026-10-16: "Clean up any
# arrows on the floor that aren't on the track, such as the one pointing at a fire place. Make sure all remaining arrows are in the centre
# of the track": the other rooms' arrows south, and the detours', were on the opponents' ways only, and room 151's pointed west past the
# turn north, at its fireplace)
ARROW_NEAR, ARROW_TURN = 4.0, 35.0          # an arrow this near the way (cells), pointing within this of the way's heading (degrees)
def on_track(a):
    x, z, y, adx, adz = a
    floor = [i for i in range(n) if abs(h[i] - y) < 1 and 0 < i < n - 1]
    i = min(floor, key=lambda i: math.hypot(pts[i][0] - x, pts[i][1] - z))
    if math.hypot(pts[i][0] - x, pts[i][1] - z) > ARROW_NEAR: return None
    t = pts[i + 1] - pts[i - 1]; t = t / np.linalg.norm(t)
    if math.degrees(math.acos(max(-1.0, min(1.0, (t[0] * adx + t[1] * adz) / math.hypot(adx, adz))))) > ARROW_TURN: return None
    # (square to the rooms where the way runs square to them)
    if abs(t[0]) > 0.985: t = np.array([math.copysign(1.0, t[0]), 0.0])
    elif abs(t[1]) > 0.985: t = np.array([0.0, math.copysign(1.0, t[1])])
    return (float(pts[i][0]), float(pts[i][1]), y, round(float(t[0]), 3), round(float(t[1]), 3))
arrows_all = arrows
arrows = [b for b in (on_track(a) for a in arrows_all) if b is not None]
print(f'floor arrows: {len(arrows)} on the race\'s way of {len(arrows_all)}')

# ---- the palace's own people in the cars' way (RACEMOD.CPP guard=): each [kind, which, x, z, floor, face x, face z, leash] -- kind 0 one
# of the Emperor's soldiers, sword out, 1 a pighead (throws things at the cars). Each stands by a room's carpets, facing where they cross,
# and steps out at most `leash` cells -- far enough to reach a car on the carpet's middle. Upstairs a soldier in most rooms on the way,
# and two by the throne.
CARPET_HALF = 8.0
def free(x, z, floor, r=1.2): return not blocked(x, z, floor, r)
def post(cx, cz, ox, oz, kind, which, leash, room, floor=FLOOR):
    """A guard by the carpets' crossing at (cx, cz): `ox`, `oz` cells off it (the first free spot along the way out, or None)."""
    for k in range(0, 8):
        x, z = cx + ox * (1 + k * 0.12), cz + oz * (1 + k * 0.12)
        if free(x, z, floor) and all(free(cx + (x - cx) * t, cz + (z - cz) * t, floor, 0.6) for t in (0.85, 0.7)):
            fx, fz = cx - x, cz - z; l = math.hypot(fx, fz)
            return [kind, which, round(x, 2), round(z, 2), floor, round(fx / l, 3), round(fz / l, 3), leash]
    print(f'  WARNING: no free spot for a guard in room {room} at ({cx + ox:.1f}, {cz + oz:.1f})')
    return None
guards = []
SOLDIER, PIG, TORTURER = 0, 1, 3
corner = [(1, 1), (-1, 1), (-1, -1), (1, -1)]
# (the ground floor: a soldier in the rooms on the way and beside it; the pigheads in 152 and 163; soldiers up the long hall)
for k, s in enumerate([153, 152, 157, 158, 156, 163]):
    sx, sz = corner[k % 4]
    g = post(col(s), row(s), sx * (CARPET_HALF + 3), sz * (CARPET_HALF + 3), SOLDIER, 0, 10.0, s)
    if g: guards.append(g)
for which, s in enumerate([152, 163]):
    ways = [corner[(which + 2 + t) % 4] for t in range(4)]
    sx, sz = next((w for w in ways if free(col(s) + w[0] * (CARPET_HALF + 7), row(s) + w[1] * (CARPET_HALF + 7), FLOOR)), ways[0])
    g = post(col(s), row(s), sx * (CARPET_HALF + 7), sz * (CARPET_HALF + 7), PIG, which, 1.0, s)
    if g: guards.append(g)
for k, hz in enumerate([hall['z1'] - 22, hall['z1'] - 52, hall['z1'] - 82]):
    side = 1 if k % 2 == 0 else -1
    g = post(wx, hz, side * (CARPET_HALF + 4), 0.01, SOLDIER, 0, 10.0, 80)
    if g: guards.append(g)
# (upstairs: the music room, the bedchamber, the dressing room -- at the corners of the way through them -- and the throne room: two by
# the way past the throne, one by its corner north)
for k, (x, z, ox, oz, room) in enumerate([(col(152), ROW_UP, -10, CARPET_HALF + 4, 'the music room'), (COL_E - 3, ROW_UP, 0.01, CARPET_HALF + 4, 'the bedchamber'),
                                          (COL_E, ROW_DR, CARPET_HALF + 3, -(CARPET_HALF + 3), 'the dressing room'),
                                          (col(157), ROW_DR, 0, -(CARPET_HALF + 4), 'the throne room'), (COL_T + 14, ROW_DR, 0, CARPET_HALF + 4, 'the throne room'),
                                          (COL_T, DOWN_Z, CARPET_HALF + 3, CARPET_HALF + 2, 'the throne room')]):
    g = post(x, z, ox if ox else 0.01, oz, SOLDIER, 0, 10.0, room, UPF)
    if g: guards.append(g)
# (the dungeon under 159 and 163 -- PalaceIsland's, reached only through 159's fireplace, the user, 2026-10-16: "complete with NPC's":
# its jailers, two soldiers by the way down its middle, and its prisoners, pigheads in two of the cages, leashed in them, looking out)
DUNGEON = L.get('dungeon')
if DUNGEON:
    DF = DUNGEON['floorTop']
    dmx = (DUNGEON['x0'] + DUNGEON['x1']) / 2
    for t, side in ((0.35, 1), (0.7, -1)):
        guards.append([SOLDIER, 0, round(dmx + side * 3.0, 2), round(DUNGEON['z0'] + (DUNGEON['z1'] - DUNGEON['z0']) * t, 2), DF, -side, 0.0, 6.0])
    for k, (cx, cz, _) in enumerate(DUNGEON['cages'][0:3:2]):
        guards.append([PIG, k % 2, round(cx, 2), round(cz, 2), DF, 1.0 if cx < dmx else -1.0, 0.0, 0.0])
    # (made medieval, 2026-10-17 -- the user: "Let's add some grobo torturers with bull whips etc.": its torturers at their posts by its
    # walls, cracking their whips -- at the cars going by, never touching one: RACEMOD.CPP guard= kind 3, LBA Assembler's PalaceTorturers)
    for x, z, fx, fz in DUNGEON.get('torturers', []):
        guards.append([TORTURER, 0, round(x, 2), round(z, 2), DF, round(fx, 3), round(fz, 3), 0.0])
print('guards:', len(guards), '--', ', '.join(f'{["soldier", "pighead", "Stan", "torturer"][g[0]]} at ({g[2]:.1f}, {g[3]:.1f}, {g[4]:.0f})' for g in guards))

# ---- checks
def way(i):
    a, b = pts[max(0, i - 2)], pts[min(n - 1, i + 2)]; d = b - a; return d / max(1e-9, np.linalg.norm(d))
def check(lo, hi, what):
    bad = [i for i in range(lo, hi) if blocked_road(pts[i][0], pts[i][1], h[i] - DECK, *way(i), max(0.5, half[i] - 1.25))]
    print(f'  {what}: ' + (f'{len(bad)} points blocked, e.g. {[(tuple(np.round(pts[i], 1)), round(h[i])) for i in bad[:8]]}' if bad else 'clear'))
    return bad
def check_floor(line, floor, what):
    bad = [p for p in line if blocked(p[0], p[1], floor, CAR_HALF)]
    print(f'  {what}: ' + (f'{len(bad)} points blocked, e.g. {[tuple(np.round(p, 1)) for p in bad[:8]]}' if bad else f'clear ({len(line)} points)'))
    return bad
print('route', n, 'points,', round(S[-1]), 'cells; start', start, 'up-ramp', i_up_foot, i_up_top, 'upstairs', i_up_floor, i_down_floor,
      'down-ramp', i_down_top, i_down_foot, 'the hall\'s ramp', i_ramp_foot, i_ramp_top, 'window', i_window_in, 'lip', lip, 'land', land)
check_floor(pts[:i_up_foot], FLOOR, 'the ground floor to the stairs')
check(i_up_foot, i_up_floor + 1, 'the up-ramp')
check_floor(pts[i_up_floor:i_down_floor + 1], UPF, 'upstairs')
check(i_down_floor, i_down_foot + 1, 'the down-ramp')
check_floor(pts[i_down_foot:i_ramp_foot], FLOOR, 'the ground floor to the long hall\'s ramp')
check(i_ramp_foot, lip + 1, 'the ramp and the window')
check_floor(routes[0][:len(detour)], FLOOR, 'the opponents\' detour through 158 and 157')
check_floor(via_treasury, UPF, 'the opponents\' way through the treasury')
check_floor(via_banquet, UPF, 'the opponents\' way through the armoury and the banquet hall')
bad = [i for i in range(lip, land + 1) if blocked(pts[i][0], pts[i][1], h[i], CAR_BOX)]
print('  the leap\'s flight: ' + (f'{len(bad)} points blocked' if bad else 'clear'))
grade = np.abs(np.diff(h)) / (np.maximum(np.diff(S), 1e-6) * 512)
print('steepest: the up-ramp %.3f, the down-ramp %.3f, the hall\'s ramp %.3f' % (grade[i_up_foot:i_up_top].max(), grade[i_down_top:i_down_foot].max(),
                                                                                grade[i_ramp_foot:i_ramp_top].max()))
# (the ramps over themselves: the least height between a turn and the one over it)
for lo, hi, what in ((i_up_foot, i_up_floor, 'the up-ramp'), (i_down_floor, i_down_foot, 'the down-ramp')):
    least = 1e9
    for i in range(lo, hi, 2):
        d = np.hypot(pts[lo:hi, 0] - pts[i][0], pts[lo:hi, 1] - pts[i][1])
        far = (np.abs(S[lo:hi] - S[i]) > 20) & (d < 2 * HALF)
        if far.any(): least = min(least, float(np.abs(h[lo:hi][far] - h[i]).min()))
    print(f'  {what} over itself: {least:.0f} at the least' + ('  WARNING: under 2,800' if least < 2800 else ''))
print('the parts: the ground floor to the stairs %.0f cells, up %.0f, upstairs %.0f, down %.0f, on to the hall\'s ramp %.0f, the ramp and the window %.0f, '
      'the leap %.0f' % (S[i_up_foot], S[i_up_top] - S[i_up_foot], S[i_down_top] - S[i_up_top], S[i_down_foot] - S[i_down_top],
                         S[i_ramp_foot] - S[i_down_foot], S[lip] - S[i_ramp_foot], S[land] - S[lip]))
print('the opponents\' ways: ' + ', '.join('%d points' % len(rt) for rt in routes))
print('about %.0f s to the lip at the top speed (34 km/h, 9.4 cells a second)' % (S[lip] / 9.44))

# ---- open areas: the ground floor's rooms and the long hall; upstairs, the rooms but the stairwells' open middles (a car on the ramp
# under the upstairs' floor would be taken for one on it): over the stairwells' column the rooms west of them, north and south
st_x1 = max(STAIR_UP['x1'], STAIR_DOWN['x1']) + 1
open_areas = [[sq_x0, sq_z0, sq_x1, sq_z1, FLOOR], [hall['x0'], hall['z0'], hall['x1'] + 1, hall['z1'] + 1, FLOOR],
              [st_x1, sq_z0, sq_x1, sq_z1, UPF], [sq_x0, sq_z0, st_x1, STAIR_DOWN['z0'] - 1, UPF], [sq_x0, STAIR_UP['z1'] + 1, st_x1, sq_z1, UPF]]
# (and the dungeon's floor, under the ground floor's: an area is the one whose floor the car is near, RACEMOD.CPP OpenAreaAt)
if DUNGEON: open_areas.append([round(DUNGEON['x0'], 2), round(DUNGEON['z0'], 2), round(DUNGEON['x1'], 2), round(DUNGEON['z1'], 2), DF])

plan = {
    'originCellX': 0, 'originCellZ': 0,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in pts],
    'heights': [round(float(v), 1) for v in h],
    'open': True, 'start': start, 'finish': lip,
    'maxGrade': 2.0,
    'asphaltHalf': HALF - 1.0, 'curbHalf': HALF - 0.25, 'vergeHalf': HALF + 0.5, 'blend': 2.0,
    'raised': [0, n - 1], 'raisedHalf': HALF,
    'raisedHalfs': [round(float(v), 2) for v in half],
    # (no road on the floors -- the ground floor's rooms to the up-stairwell, the upstairs between the stairwells, the ground floor from the
    # down-stairwell to the long hall's ramp -- nor where the leap comes down: never reached)
    'bare': [[0, i_up_foot - 1], [i_up_floor, i_down_floor], [i_down_foot + 1, i_ramp_foot - 1], [land, n - 1]],
    'openAreas': open_areas,
    'floorArrows': [[round(float(x), 2), round(float(z), 2), float(y), dx, dz] for x, z, y, dx, dz in arrows],
    'openRoutes': [[[round(float(x), 2), round(float(z), 2)] for x, z in rt] for rt in routes],
    'guards': guards,
    # (the leap out of the round window: its lip -- the finish line -- the car carried from just before it)
    'arcJumps': [[lip - 1, lip, land, land_foot, 0]],
    'teleports': L.get('teleports') or [],
    'railCamera': [9, 2100, 7, 500, 1000],
    # (the ramps the rooms' red carpet edged in gold)
    'themes': [{'from': 0, 'to': n - 1, 'theme': 'carpet'}],
    'keepAbove': 0,
}
json.dump(plan, open(OUT, 'w'))
print('wrote', OUT)
