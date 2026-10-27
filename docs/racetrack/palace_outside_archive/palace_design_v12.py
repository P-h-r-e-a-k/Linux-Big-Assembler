"""The Emperor's palace race (the user, 2026-10-12: "through the palace rooms, coming out the window and around the roof tops?
Point-to-point i.e. no laps, finishing with massive jump to harbor teleport? - Twinsen, quick, Dark monk's in the palace, he's killing
everybody. Leading into final showdown at Celebration"), on the Palace island (Terrain/Palace: PALACE.ILE, its layout PALACE.JSON).

Second version, the same day (the user: "The palace track is far too narrow ... Let's make the inside tracks wider if we can do, and
perhaps loop in more rooms. Add a section where we're outside on the roof looping around the palace roof tops"): the island's doors are
four bricks wide now (16 cells: PalaceIsland.DoorBricks).

Third version, the same day (the user: "For our palace track let's scale up the roof top so it covers more of the outside of the palace
area. For the inside of the palace let's remove the track but add some arrows to allow cars to take multiple directions, it has a single
entry and exit area so all cars will ultimately end up back on the same track"): no road in the rooms, arrows on their floor, the
opponents each their own way through; a loop round a flat roof top.

Fourth version, 2026-10-13 (the user: "The roof of our Palace needs a lot of work. Let's make the roof top bigger, and remove the track from
the outside for now. The track on the outside needs to be raised in the air looping around the giant turrets that form the roof, which
means the existing track section outside needs to go"): the hilltop is flat 54 cells round the palace now (PalaceCliff), and the race a
sprint (the plan's open) through the palace alone:
  - in the sixteen rooms no road at all (the plan's bare): the cars drive the rooms' floor -- the race-track mode keeps them on it and
    inside the rooms, its open_area= (the plan's openAreas) -- from the start in the south-east room (154) to the north-west one (163) and
    out of its north door into the long hall, each room's ways on west and north shown by arrows on its floor (floorArrows), any way
    through the rooms' doors (all four of a room's walls have one). The route itself, the test pilot's: west along the bottom row (153,
    152, 151) and north up the west column (155, 159, 163); the opponents after the first each their own way (openRoutes, in turn): north
    up the east column and west along the top row; a staircase from 153, north and west by turns; another from 158 (for a fourth opponent);
  - north up the long hall and up a ramp to the round window Twinsen leaves by: the finish line in the window, and the leap out of it over
    the cloud sea between the hill's two arms -- won, the Dark Monk's teleporter takes the car in mid-flight (the race-track mode's sprint,
    wake=, wake_flight=); lost, the race is run again from the grid before it comes down.
(Until the road round the roof's turrets, high in the air, is made.)

Fifth version, 2026-10-13, later (the user: "The start area needs changing so the start line lines up with the straight of the track",
"Add a raised track outside on the roof that loops around the roof turrets", "Enlarge the track width so it's the same width as a door
way", "Add an entrance door to the palace in the start room", and the fireplace in room 151 a teleporter to room 159's):
  - the grid in front of the palace's new doors in room 154's east wall, on the bottom row's carpet line, the start line ahead on it --
    the chest that stood there moved to the room's north wall (PalaceIsland);
  - out of the round window a raised road (RAISE over the roof's rock): north, east over the cloud sea and south past the long hall's
    turrets, climbing to the roof, on south down the middle way between the four turrets (they are SlotFill 0.80 of their places now: the
    ways between them some 24 cells), west through the middle way across, south down the west side, east along the south side and north
    up the east side -- round three sides of the south-west turret and two of the south-east one -- and east off the roof's edge: the
    leap over the harbour, through the Dark Monk's teleporter on its pillar (the finish line at the lip). (First east out over the sea
    and round by the turrets' north side: half a minute of road in the fog with nothing beside it);
  - the teleporter: the plan's teleports, from the island's layout (RACEMOD.CPP teleport=);
  - then the user's own way round the roof (2026-10-13, a drawing: "a proposed new track route for outside the palace ... starting at the
    point the current track exits the building, at the end of the route let's add a loop of track going up into the air, culminating
    with a large take off ramp"): out of the round window a hook east over the sea and back west between the long hall's two turrets,
    south down the west side of the second, east along the north-west turret's north side, south down the middle way, west through the
    middle way across, round the south-west turret -- south down the west side, east along the south side, north up the middle way --
    climbing all the way (BRIDGE: the second time through the middle crossing it passes over the first), east through the middle way's
    east half, north up the east side, west to a spiral out over the sea north of the turrets (SPIRAL_LOOPS climbing SPIRAL_RISE) and east
    off its top down a straight to a large kicker (KICK over RUNUP): the leap through the teleporter on its pillar -- moved onto the
    leap's line and raised to it (PalaceIsland.PillarNorth, PillarRise);
  - the carpets four bricks across (16 cells): the guards stand off them further.

Sixth version, 2026-10-13, late (the user, after a picture of the rooms from above: "We have lots of red that isn't the inside track. The
red track area should be the width of the door way, there should be no other red on the floor ... let's keep the corners nice and square.
The outside segment of track we have is still terrible. It needs to represent driving over the roof top, looping between the roof
turrets, more height. We also need the outside track colour to be our regular track colour with a red/white border on the edge"):
  - the floor's red only the route's (PalaceIsland's TrackRow, TrackColumn): the route here runs along the middle of it;
  - the user's way round the roof kept, high over it: out of the round window climbing over the sea to OVER_HALL over the long hall's roof
    where it comes in between its turrets, on up to HIGH over the roof by the first time through the middle crossing -- most of the way
    up the turrets -- down DIP round the south-west turret to pass under itself there the second time, gently up to the spiral, up the
    spiral (SPIRAL_RISE) and the take-off ramp (KICK) to the leap's lip as before;
  - out of the round window the race-track mode's own road: asphalt, red and white curbs (the deck theme standard; the palace's pale
    stone and turquoise was lost in the haze).

Seventh version, 2026-10-13, late (the user, with a drawing of the roof: "I've re-drawn the roof portion of our palace track again. The
previous version we had was too many straight lines and wasn't very interesting. This track exits the palace at the same place, loops around
twice, one loop on top of the other, a short straight into another double loop going down, then looping around and weaving in and out of
the buildings, as much as possible we should avoid straight track sections. The pink dots are a jump and the black dot is where the race
ends in a large jump where we'll then zone transition to CELEBRA2.ILE"; and "our current outside track section had very wide edges at the
side of the track, these should more closely resemble our other tracks"): see the route below. The road outside HALF either side, its
curbs and rails as the other tracks' (the rail a quarter of a cell past the curb; it was 2 to 3.5 cells of flat rail top).

Usage: palace_design.py <game folder with PALACE.ILE and PALACE.JSON: the island built on its own, `ScriptRoundTrip palace`> [out plan]
"""
import json, math, sys
import numpy as np
sys.path.insert(0, __file__.rsplit('\\', 1)[0] if '\\' in __file__ else '.')
from ile import load_ile, hqr_entries

GAME = sys.argv[1] if len(sys.argv) > 1 else r'E:\dump\TEMP\palace3\v6'
OUT = sys.argv[2] if len(sys.argv) > 2 else r'E:\dump\LBAAssembler\docs\racetrack\palace_track_plan.json'
L = json.load(open(GAME + r'\PALACE.JSON'))
_, _, _, CUBES = load_ile(GAME + r'\PALACE.ILE')
SP = 0.5                                    # the plan's points, cells apart
FLOOR = L['floorTop']                       # the rooms' floor: what the cars drive on in them
TOP = L['hilltop']                          # the hilltop: the roofs' top
DECK = 60                                   # the raised road over what it lies on
HALF_IN = 5.5                               # the road's half width (cells): up to the round window (16 cells across) ...
HALF = 4.5                                  # ... and out of it, the other tracks' (the curbs a cell and a quarter of a cell in from its edge)
JUMP_APEX = 1400                            # the leap's top over its lip (its top under 32,767)
FLIGHT = 80                                 # the leap's length (cells): a car at three times the race car's speed is still in the air when
                                            # the dream fades (wake_flight=, 2.4 s) -- within the island's cubes
PAD = 12                                    # where the leap comes down (no road: never reached)
KICK = 1000                                 # the take-off ramps: their rise to the lip over their last RUNUP cells
RUNUP = 12
RAMP = 80                                   # the ramp up to the round window: its length (cells) ...
WINDOW_H = 14600                            # ... and the road's height through the window (its opening 11,920 to 17,040): the first loop
                                            # climbs out over the sea to over the long hall's roof in a third of a turn
H1_ROOF_OVER = 900                          # the first loop over the long hall's roof (at H1_ROOF) at the least this much over it ...
H1_TOP = 30500                              # ... and its top, out of it (the rail camera 2,100 over a road: under 32,767)
H2_IN_H = 30500                             # into the second loop; down it DESCENT each time round (its west side twice, one over the
DESCENT = 3300                              # other: 2,800 at the least between a deck and the one over it -- 3,400 until 2026-10-14)
# (2026-10-14, the user: "the outside of the palace track is too low to the ground, let's lift the track up, so we're hugging tighter
# to the buildings, so we get more of a sense of driving through the roof tops. Also the outside jump is far too long and needs to be
# shorter" -- every level up as far as the ways over and under each other leave room: the second loop in 200 higher and down 3,300 a turn (3,400), round
# the sixteen rooms 26,000 (25,000), under the way down the west side 23,800 (23,400; the way down the west side over it is the second
# loop's way out, and the second loop's south side is over it too), up to the jump past the second loop, home and the lip 500 higher;
# the jump half as long. The road can't go higher: the rail camera rides 2,100 over it, under 32,767)
AWAY_MID = 26000                            # the middle way across, west end, on the way round the sixteen rooms' south-west turret ...
AWAY_LOW = 23800                            # ... and by the north-west turret -- under the way down the west side
JUMP_FOOT_H = 25000                         # ... up to the jump's take-off ramp east of the second loop
HOME_SE = 28500                             # on the way home: the south-east corner, then up the middle way -- over the way up it before
HOME_HIGH = 29500
LIP = 30500                                 # the finish line: the leap's lip (its top JUMP_APEX more)
# (a car's collision box: some 1,420 units either way of its middle whichever way it faces: 2.8 cells. Where the cars drive the floor,
# nothing within CAR_HALF of a line they follow; on the ramp nothing within the road's half-width less the rail's inset and a box)
CAR_BOX = 2.8
CAR_HALF = 1.7

# ---- where things are
rooms = {r['scene']: r for r in L['rooms']}
def mid(scene):
    r = rooms[scene]; return ((r['x0'] + r['x1'] + 1) / 2, (r['z0'] + r['z1'] + 1) / 2)
# (a row's doors are in a line 2 cells past its rooms' middles, a column's the same: the rooms' grids put them there)
def row(scene): return mid(scene)[1] + 2
def col(scene): return mid(scene)[0] + 2
win = L['window']
wx = (win['x0'] + win['x1']) / 2            # the round window's middle across, the wall's inner face and outer
w_in, w_out = win['z1'], win['z0']
sill = win['sill']
hall = rooms[80]
SIXTEEN = [s for s in rooms if s != 80]
sq_x0, sq_x1 = min(rooms[s]['x0'] for s in SIXTEEN), max(rooms[s]['x1'] for s in SIXTEEN) + 1
sq_z0, sq_z1 = min(rooms[s]['z0'] for s in SIXTEEN), max(rooms[s]['z1'] for s in SIXTEEN) + 1

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
    hit = (boxes[:, 0] < X + ex) & (boxes[:, 3] > X - ex) & (boxes[:, 2] < Z + ez) & (boxes[:, 5] > Z - ez)           & (boxes[:, 1] < y + 900) & (boxes[:, 4] > y + 120)
    return bool(hit.any())


def ground(x, z):
    """The island's ground height at (x, z) cells (bilinear in the cell)."""
    gx, gz = int(math.floor(x)), int(math.floor(z))
    cid = cubemap[gz // 64, gx // 64] & 127
    if not cid or cid not in CUBES: return -700
    H = CUBES[cid].heights
    lx, lz = gx % 64, gz % 64; fx, fz = x - gx, z - gz
    return (H[lz, lx] * (1 - fx) * (1 - fz) + H[lz, lx + 1] * fx * (1 - fz) + H[lz + 1, lx] * (1 - fx) * fz + H[lz + 1, lx + 1] * fx * fz)


def path(corners, radii, sp=SP):
    """A line through `corners` (cells), each inner one rounded to its radius (radii[i] for corners[i]; the ends' unused): straight runs
    and circular arcs, sp cells apart."""
    P = [np.array(c, float) for c in corners]
    out = [P[0]]
    cut = [0.0] * len(P)                    # (how far each corner's arc starts before it and ends after it)
    for i in range(1, len(P) - 1):
        a, b, c = P[i - 1], P[i], P[i + 1]
        u = (b - a) / np.linalg.norm(b - a); v = (c - b) / np.linalg.norm(c - b)
        turn = math.atan2(u[0] * v[1] - u[1] * v[0], float(u @ v))     # (> 0: to the right -- clockwise seen from above)
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


# ---- the rooms: the rows' door lines (z) and the columns' (x), a room's middle where its two cross
R0, R1, R2, R3 = row(154), row(158), row(162), row(166)    # the bottom row's ... the top row's
C0, C1, C2, C3 = col(151), col(152), col(153), col(154)    # the west column's ... the east column's
# (the start line in the east half of room 154, the grid behind it in front of the palace's doors in the room's east wall -- the way in --
# on the bottom row's door line, all in one cube -- the start's scene; the room's chest stood there: the island moved it to the north
# wall)
START_X = C3 + 3
GRID_Z = R0
BEHIND = (L['entrance'][0] if L.get('entrance') else rooms[154]['x1']) - 12   # (and the camera behind the grid clear of the doors' frame)
if START_X // 64 != BEHIND // 64:
    raise ValueError(f'the grid ({START_X}..{BEHIND}) crosses a cube edge')
TURN = 12                                                   # a turn at a room's middle: its radius (cells)
HALL_JOIN = (C0, hall['z1'] - 36)                           # where every way through the rooms is the route again, up the long hall
GRID = [(BEHIND, GRID_Z)]
GRID_R = [0]
# (the opponents' ways: north at once through room 154's north door, or west first; a turn at each room's middle)
NORTH_FIRST = [(BEHIND, GRID_Z), (C3, GRID_Z)]
NORTH_FIRST_R = [0, 7]
open_routes = [
    # north up the east column (158, 162, 166), west along the top row (165, 164, 163)
    (NORTH_FIRST + [(C3, R3), (C0, R3), HALL_JOIN], NORTH_FIRST_R + [TURN, TURN, 0]),
    # a staircase: west into 153, north into 157, west into 156, north into 160, west into 159, north into 163
    (GRID + [(C2, R0), (C2, R1), (C1, R1), (C1, R2), (C0, R2), HALL_JOIN], GRID_R + [TURN] * 5 + [0]),
    # another: north into 158, west into 157, north into 161, west into 160, north into 164, west into 163
    (NORTH_FIRST + [(C3, R1), (C2, R1), (C2, R2), (C1, R2), (C1, R3), (C0, R3), HALL_JOIN], NORTH_FIRST_R + [TURN] * 6 + [0]),
]
# the arrows on the floor: in each room, in front of its west door and its north door -- the ways on (none out of the west column's west
# walls nor the top row's north walls but room 163's, into the long hall); and up the long hall
arrows = []
for s in SIXTEEN:
    cx, cz = col(s), row(s)
    if s == 154:
        # (the start's room: the grid on its door line -- the arrows where the cars pass, on west, or turning north)
        arrows += [(cx - 16, cz, -1, 0), (cx, cz - 10, 0, -1)]
        continue
    if cx > C0: arrows.append((cx - 12, cz, -1, 0))
    if cz > R3 or s == 163: arrows.append((cx, cz - 12, 0, -1))
arrows += [(C0, hall['z1'] - 40, 0, -1), (C0, (hall['z0'] + hall['z1']) / 2 + 20, 0, -1)]

# ---- the route: the grid, west along the bottom row, north up the west column, the long hall, the ramp up to the round window; then the
# user's way round the roof (2026-10-13, late, a drawing: "This track exits the palace at the same place, loops around twice, one loop on top
# of the other, a short straight into another double loop going down, then looping around and weaving in and out of the buildings, as much
# as possible we should avoid straight track sections. The pink dots are a jump and the black dot is where the race ends in a large jump
# where we'll then zone transition to CELEBRA2.ILE"):
#  - out of the round window round the long hall's first turret one and a half times, clockwise seen from above (north up), climbing: the
#    second time round over the first (HELIX1);
#  - across to its second turret and round it the other way, coming down, the second time down its west side under the first (HELIX2);
#  - south down the sixteen rooms' west side, round the south-west turret -- east along the south side, north up the middle way, west through
#    the middle way across -- north by the north-west turret's west side (over the way down: the road there twice, one over the other) and
#    east along its north side to the jump: the drawing's (the pink dots) went over the north-east turret's west wing, 46,000 to 56,000
#    high, past what a car can reach (32,767), so it flies along the strip north of that turret instead;
#  - east along the north side, south down the east side, west along the south side and north up the middle way -- over the way up it
#    before -- to the drawing's black dot: the finish line at the lip of the leap north off the roof, a win fading to Celebration Island
#    with its statue (CELEBRA2: RaceTrackService's WinVar, game variable 79);
#  - every line between the turrets the middle of the room they leave (measured on the island's collision), curving with them.
if abs(C0 - wx) > 0.01:
    raise ValueError(f'the west column ({C0}) and the round window ({wx}) are not in line')
inside = path(GRID + [(C0, R0), (wx, w_out)], GRID_R + [TURN, 0])


def unit(v):
    v = np.array(v, float); return v / np.linalg.norm(v)


def resample(c, sp=SP, first=True):
    """A polyline's points again, sp cells apart along it (its first point kept, or left out)."""
    s = np.r_[0, np.cumsum(np.linalg.norm(np.diff(c, axis=0), axis=1))]
    k = np.arange(0 if first else sp, s[-1] + 1e-9, sp)
    return np.stack([np.interp(k, s, c[:, 0]), np.interp(k, s, c[:, 1])], 1)


def hermite(p0, d0, p1, d1, k=1.0):
    """A cubic from p0 heading d0 to p1 heading d1 (unit vectors; k the handles' length, a share of the distance between), sp cells apart,
    without its first point."""
    p0, d0, p1, d1 = (np.array(v, float) for v in (p0, d0, p1, d1))
    L_ = np.linalg.norm(p1 - p0) * k
    t = np.linspace(0, 1, 3000)[:, None]
    c = (2 * t ** 3 - 3 * t ** 2 + 1) * p0 + (t ** 3 - 2 * t ** 2 + t) * d0 * L_ + (-2 * t ** 3 + 3 * t ** 2) * p1 + (t ** 3 - t ** 2) * d1 * L_
    return resample(c, first=False)


def ellipse_pose(e, t):
    """A point of a loop and the way it runs there: e = (middle x, z, half-width across x, along z, way) -- way 1 clockwise seen from above
    with north up, the angle from the north point; -1 the other way, the angle from the west point."""
    cx, cz, a, b, way = e
    if way > 0: return (cx + a * math.sin(t), cz - b * math.cos(t)), unit((a * math.cos(t), b * math.sin(t)))
    return (cx - a * math.cos(t), cz + b * math.sin(t)), unit((a * math.sin(t), b * math.cos(t)))


def ellipse(e, t0, t1):
    """A loop's points from angle t0 to t1, sp cells apart, without the first; and each point's angle."""
    t = np.linspace(t0, t1, 6000)
    c = np.array([ellipse_pose(e, a)[0] for a in t])
    s = np.r_[0, np.cumsum(np.linalg.norm(np.diff(c, axis=0), axis=1))]
    k = np.arange(SP, s[-1] + 1e-9, SP)
    return np.stack([np.interp(k, s, c[:, 0]), np.interp(k, s, c[:, 1])], 1), np.interp(k, s, t)


def spline(ctrl, d0=None):
    """A centripetal Catmull-Rom curve through the control points (cells), sp cells apart, without the first (d0: the way it leaves it)."""
    P = [np.array(p, float) for p in ctrl]
    head = P[0] - (unit(d0) * np.linalg.norm(P[1] - P[0]) if d0 is not None else P[1] - P[0])
    P = [head] + P + [2 * P[-1] - P[-2]]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        def nxt(ti, a, b): return ti + max(1e-6, np.linalg.norm(b - a)) ** 0.5
        t0 = 0.0; t1 = nxt(t0, p0, p1); t2 = nxt(t1, p1, p2); t3 = nxt(t2, p2, p3)
        t = np.linspace(t1, t2, 80, endpoint=False)[:, None]
        A1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1
        A2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2
        A3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3
        B1 = (t2 - t) / (t2 - t0) * A1 + (t - t0) / (t2 - t0) * A2
        B2 = (t3 - t) / (t3 - t1) * A2 + (t - t1) / (t3 - t1) * A3
        out.append((t2 - t) / (t2 - t1) * B1 + (t - t1) / (t2 - t1) * B2)
    return resample(np.concatenate(out + [P[-2][None]]), first=False)


def line(p0, p1):
    """Straight on from p0 to p1, sp cells apart, without p0."""
    p0, p1 = np.array(p0, float), np.array(p1, float)
    k = np.arange(SP, np.linalg.norm(p1 - p0) + 1e-9, SP)
    return p0 + unit(p1 - p0) * k[:, None]


# (the loops: round each of the long hall's turrets -- the first cells x 357..400, z 350..414, the second x 357..400, z 442..506 -- the first
# out over the sea east and north, where it climbs from the window, and between the two turrets south; the second between them north and
# between it and the north-west turret south)
HELIX1 = (379, 365, 51, 57.5, 1)
H1_ROOF, H1_OUT = math.radians(124), math.radians(520)   # in from the window on its north side; over the long
                                                                                  # hall's roof from there; out on its south side, round again
HELIX2 = (377.5, 474.5, 42, 41.5, -1)
H2_IN, H2_OUT = math.radians(300), math.radians(720)                             # in on its north side, out down its west side the 2nd time
# (straight out of the window until the road is clear of the wall, then round onto the first loop: where it joins it, and how, the widest bend)
def min_radius(c, k=4):
    a, b, d = c[:-2 * k], c[k:-k], c[2 * k:]
    cross = np.abs((b[:, 0] - a[:, 0]) * (d[:, 1] - a[:, 1]) - (b[:, 1] - a[:, 1]) * (d[:, 0] - a[:, 0]))
    sides = np.linalg.norm(b - a, axis=1) * np.linalg.norm(d - b, axis=1) * np.linalg.norm(a - d, axis=1)
    return float(np.min(sides / np.maximum(2 * cross, 1e-9)))
STUB_STRAIGHT = 7
_, H1_IN, STUB_K = max((min_radius(np.concatenate([np.array([[wx, w_out - STUB_STRAIGHT]]),
                         hermite((wx, w_out - STUB_STRAIGHT), (0, -1), *ellipse_pose(HELIX1, t), k), ellipse(HELIX1, t, t + 0.6)[0]])), t, k)
                       for t in np.radians(np.arange(10, 61, 2.5)) for k in np.arange(0.5, 2.01, 0.1))
stub = np.concatenate([line((wx, w_out), (wx, w_out - STUB_STRAIGHT)), hermite((wx, w_out - STUB_STRAIGHT), (0, -1), *ellipse_pose(HELIX1, H1_IN), STUB_K)])
loop1, th1 = ellipse(HELIX1, H1_IN, H1_OUT)
bend = hermite(*ellipse_pose(HELIX1, H1_OUT), *ellipse_pose(HELIX2, H2_IN))
loop2, th2 = ellipse(HELIX2, H2_IN, H2_OUT)
JUMP_Z = 522                                # the jump: along the strip north of the north-east turret (cells x 473..545: its north edge 529..545)
A_FOOT, A_LIP, A_LAND, A_END = 451, 463, 485, 497     # its take-off ramp's foot and lip, the landing lip and the landing's end (cells x;
                                                      # 22 cells over since 2026-10-14, 44 before -- "far too long";
                                                      # none on a cube's edge: a line there wasn't seen crossed)
A_KICK, A_APEX = 1000, 900                  # its ramp's rise, and its top over the lips (1,500 over the 44 cells before)
LIP_X, LIP_Z = 463, 572                     # the finish line: the leap's lip, by the drawing's black dot -- north up the middle way off it
                                            # (at 576, on the edge of two cubes, the car flew over it uncounted and fell)
# (the sixteen rooms: down the west side, round the south-west turret, back north by the north-west turret, east along its north side to
# the jump's take-off)
# (weaving a little from side to side down the long sides -- the user: "as much as possible we should avoid straight track sections")
AWAY = [ellipse_pose(HELIX2, H2_OUT)[0], (341.5, 500), (342.5, 528), (346, 556), (342, 590), (347.5, 625), (342.5, 660), (347, 692),
        (344, 718), (352, 737), (370, 745), (395, 750), (420, 745), (440, 742), (452, 731), (458, 712), (454, 690), (458, 668), (456, 652),
        (450, 634), (440, 626), (420, 625), (400, 627), (384, 628), (372, 619), (368, 604), (362, 593), (353, 583), (346, 571), (344.5, 555),
        (346, 540), (353, 527), (366, 521), (390, 520.5), (410, 521), (424, JUMP_Z)]
around = spline(AWAY, ellipse_pose(HELIX2, H2_OUT)[1])
jump = line(AWAY[-1], (A_END, JUMP_Z))
# (east along the north side, south down the east side, west along the south side, north up the middle way to the finish's lip; the leap)
HOME = [(A_END, JUMP_Z), (516, 522), (532, 522), (548, 525), (559, 533), (563.5, 548), (566.5, 570), (561.5, 600), (566.5, 630), (561.5, 660),
        (566, 690), (563, 715), (560, 732), (550, 743), (532, 748), (510, 745), (488, 749), (472, 739), (463, 724), (459.5, 705),
        (462, 680), (457.5, 655), (460, 630), (461, 606), (463, LIP_Z + RUNUP)]
home = spline(HOME, (1, 0))
leap = line((LIP_X, LIP_Z + RUNUP), (LIP_X, LIP_Z - FLIGHT - PAD))
pieces = [inside, stub, loop1, bend, loop2, around, jump, home, leap]
pts = pieces[0]
for p in pieces[1:]:
    pts = np.concatenate([pts, p[1:] if np.linalg.norm(p[0] - pts[-1]) < 1e-6 else p])
n = len(pts)
S = np.r_[0, np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))]
o_stub = len(inside); o_loop1 = o_stub + len(stub); o_bend = o_loop1 + len(loop1); o_loop2 = o_bend + len(bend); o_around = o_loop2 + len(loop2)
o_jump = o_around + len(around); o_home = o_jump + len(jump); o_leap = o_home + len(home)


def nearest(p, lo=0, hi=None):
    hi = n if hi is None else hi
    return lo + int(np.argmin(np.hypot(pts[lo:hi, 0] - p[0], pts[lo:hi, 1] - p[1])))


start = nearest((START_X, GRID_Z), 0, 200)
i_ramp_foot = nearest((wx, w_in + RAMP + 8)); i_ramp_top = nearest((wx, w_in + 8), i_ramp_foot)
i_window_in = nearest((wx, w_in), i_ramp_foot); i_window_out = nearest((wx, w_out), i_window_in)
i_hall = nearest(HALL_JOIN, 0, i_ramp_foot)
i_roof1 = o_loop1 + int(np.searchsorted(th1, H1_ROOF))           # the first loop over the long hall's roof from here
i_top1 = o_bend - 1                                               # ... and out of it
i_in2, i_out2 = o_loop2, o_around - 1                             # the second loop
a_foot = nearest((A_FOOT, JUMP_Z), o_around); a_lip = nearest((A_LIP, JUMP_Z), a_foot)
a_land = nearest((A_LAND, JUMP_Z), a_lip); a_end = nearest((A_END, JUMP_Z), a_land)
i_west2 = nearest((353, 583), o_around)                           # by the north-west turret: from here over the way down
foot = nearest((LIP_X, LIP_Z + RUNUP), o_home); lip = nearest((LIP_X, LIP_Z), foot)
land = nearest((LIP_X, LIP_Z - FLIGHT), lip); land_foot = n - 1

# ---- heights: the rooms' floor (no road there); the ramp up to WINDOW_H, level through the window; out of it round the first loop, climbing
# over the sea to over the long hall's roof by H1_ROOF, on up to H1_TOP; across to the second loop, down it DESCENT a time round; level
# down the west side (over the way back by AWAY_LOW); down by the north-west turret to AWAY_LOW; the jump; up round the east side and
# the south side to HOME_HIGH up the middle way, over the way up it before; the take-off ramp to the lip; the leap
def ease(t): return t * t * (3 - 2 * t)
def ramp(t, e=0.2):
    """A climb steady in its middle, easing in and out over its first and last `e` (the steepest 1 / (1 - e) times the mean)."""
    v = 1 / (1 - e)
    if t < e: return v * t * t / (2 * e)
    if t > 1 - e: return 1 - v * (1 - t) ** 2 / (2 * e)
    return v * (e / 2 + t - e)
def climb(a, b, h0, h1, shape=ease):
    for i in range(a, b + 1):
        h[i] = h0 + (h1 - h0) * shape((S[i] - S[a]) / max(1e-6, S[b] - S[a]))
def linear(t): return t
h = np.full(n, FLOOR, float)
climb(i_ramp_foot, i_ramp_top, FLOOR, WINDOW_H, ramp)
h[i_ramp_top:i_window_out + 1] = WINDOW_H
climb(i_window_out, i_roof1, WINDOW_H, TOP + H1_ROOF_OVER, linear)
climb(i_roof1, i_top1, TOP + H1_ROOF_OVER, H1_TOP, linear)
climb(i_top1, i_in2, H1_TOP, H2_IN_H)
for i in range(i_in2, i_out2 + 1):
    h[i] = H2_IN_H - DESCENT * (th2[i - i_in2] - H2_IN) / (2 * math.pi)
away0 = h[i_out2]
i_level = nearest((344, 600), o_around)                          # level down the west side past where the way back joins it
h[i_out2:i_level + 1] = away0
i_cross = nearest((384, 628), i_level)                            # the middle way across, west end
climb(i_level, i_cross, away0, AWAY_MID)
climb(i_cross, i_west2, AWAY_MID, AWAY_LOW)
h[i_west2:a_foot + 1] = AWAY_LOW
i_free = nearest((424, JUMP_Z), i_west2)                          # past the second loop's south side: up to the jump's foot from here
climb(i_free, a_foot, AWAY_LOW, JUMP_FOOT_H)
for i in range(a_foot, a_lip + 1):
    h[i] = JUMP_FOOT_H + A_KICK * ease((S[i] - S[a_foot]) / max(1e-6, S[a_lip] - S[a_foot]))
a_lipH = JUMP_FOOT_H + A_KICK
for i in range(a_lip, a_land + 1):
    t = (S[i] - S[a_lip]) / max(1e-6, S[a_land] - S[a_lip]); h[i] = a_lipH + A_APEX * 4 * t * (1 - t)
h[a_land:a_end + 1] = a_lipH
i_se = nearest((560, 732), a_end)                                 # the south-east corner
climb(a_end, i_se, a_lipH, HOME_SE)
i_mid = nearest((459.5, 705), i_se)                               # up the middle way
climb(i_se, i_mid, HOME_SE, HOME_HIGH)
climb(i_mid, foot, HOME_HIGH, LIP - KICK)
for i in range(foot, lip + 1):
    h[i] = LIP - KICK + KICK * ease((S[i] - S[foot]) / max(1e-6, S[lip] - S[foot]))
lipH = LIP
h[lip:] = lipH
for i in range(lip, land + 1):
    t = (S[i] - S[lip]) / max(1e-6, S[land] - S[lip]); h[i] = lipH + JUMP_APEX * 4 * t * (1 - t)

# ---- widths (cells from the road's middle to its rail): up to the round window as wide as it allows, the other tracks' width beyond (10
# cells on, clear of its wall); the grid's places two abreast. (In the rooms there is no road: the width only places the grid and the scenes)
half = np.full(n, HALF_IN)
for i in range(i_window_out, n):
    half[i] = HALF_IN + (HALF - HALF_IN) * min(1, max(0, (S[i] - S[i_window_out] - 10) / 10))
HALF_GRID = 3.5
for i in range(0, n):
    if i <= start + 6: half[i] = HALF_GRID
    elif i <= start + 26: half[i] = HALF_GRID + (HALF_IN - HALF_GRID) * (i - start - 6) / 20

# ---- the opponents' ways through the rooms, a cell apart (RaceTrackBuilder.RouteThrough)
routes = [path(c, r, 1.0) for c, r in open_routes]

# ---- the palace's own people in the cars' way (2026-10-13, the user: "The retail game has enemies inside the palace, 3 pigs, Stan from
# Time commando, and lots of soldiers with swords, let's add some of these enemies inside as obstacles, perhaps the could chase and attack
# the cars, or throw random things at them"; RACEMOD.CPP guard=): each [kind, which, x, z, floor, face x, face z, leash] -- kind 0 one of
# the Emperor's soldiers, sword out (steps into the road at a car and strikes), 1 a pighead (throws things at the cars; which: the red
# one with the hammer, the brown with the garden tool, the yellow with the broom), 2 Stan (as a soldier). Each stands on the tiles by a
# room's carpets -- they run through the room's middle, a row's and a column's, two bricks (8 cells) wide -- facing where they cross, and
# steps out at most `leash` cells -- far enough to reach a car on the carpet's middle.
CARPET_HALF = 8.0                           # (the carpets four bricks across since 2026-10-13, as the doors)
def free(x, z, r=1.2): return not blocked(x, z, FLOOR, r)
def post(cx, cz, ox, oz, kind, which, leash, room):
    """A guard by the carpets' crossing at (cx, cz): `ox`, `oz` cells off it (the first free spot along the way out, or None)."""
    for k in range(0, 8):
        x, z = cx + ox * (1 + k * 0.12), cz + oz * (1 + k * 0.12)
        if free(x, z) and all(free(cx + (x - cx) * t, cz + (z - cz) * t, 0.6) for t in (0.85, 0.7)):
            fx, fz = cx - x, cz - z; l = math.hypot(fx, fz)
            return [kind, which, round(x, 2), round(z, 2), FLOOR, round(fx / l, 3), round(fz / l, 3), leash]
    print(f'  WARNING: no free spot for a guard in room {room} at ({cx + ox:.1f}, {cz + oz:.1f})')
    return None
guards = []
SOLDIER, PIG, STAN = 0, 1, 2
# (a soldier in every room but the start's, by turns in a corner of its carpets' crossing; the pigheads further off in two rooms on the
# race's way -- 152 on the bottom row, and 163, where every way meets before the long hall: in 157 and 160, on the opponents' other ways
# only, no car came near enough for a throw; soldiers in the long hall, by its carpet. Not Stan nor the pighead with the broom: they race,
# the user's line-up -- CharactersForTrack.txt -- Stan in his time machine, and the road sweeper)
corner = [(1, 1), (-1, 1), (-1, -1), (1, -1)]
for k, s in enumerate(sorted(SIXTEEN)):
    if s == 154: continue
    sx, sz = corner[k % 4]
    g = post(col(s), row(s), sx * (CARPET_HALF + 3), sz * (CARPET_HALF + 3), SOLDIER, 0, 10.0, s)
    if g: guards.append(g)
for which, s in enumerate([152, 163]):
    # (the first of the crossing's four corners with room for him, from a different one in each room)
    ways = [corner[(which + 2 + t) % 4] for t in range(4)]
    sx, sz = next((w for w in ways if free(col(s) + w[0] * (CARPET_HALF + 10), row(s) + w[1] * (CARPET_HALF + 9))), ways[0])
    g = post(col(s), row(s), sx * (CARPET_HALF + 10), sz * (CARPET_HALF + 9), PIG, which, 1.0, s)
    if g: guards.append(g)
# (the long hall: soldiers by its carpet either side, by turns, facing across it)
hx = col(163)
for k, hz in enumerate([hall['z1'] - 22, hall['z1'] - 42, hall['z1'] - 62, hall['z1'] - 82]):
    side = 1 if k % 2 == 0 else -1
    g = post(hx, hz, side * (CARPET_HALF + 4), 0.01, SOLDIER, 0, 10.0, 80)
    if g: guards.append(g)
print('guards:', len(guards), '--', ', '.join(f'{["soldier", "pighead", "Stan"][g[0]]} at ({g[2]:.1f}, {g[3]:.1f})' for g in guards))

# ---- checks
def check(lo, hi, what):
    # (the car's middle up to the road's half-width less a curb's width and a quarter either side of the road's middle, across the way the
    # road runs there: a square round the road's middle as wide as that and the car's box was too much on a slant -- the long hall's second
    # turret's tip, 2026-10-13)
    def way(i):
        a, b = pts[max(0, i - 2)], pts[min(n - 1, i + 2)]; d = b - a; return d / max(1e-9, np.linalg.norm(d))
    bad = [i for i in range(lo, hi) if blocked_road(pts[i][0], pts[i][1], h[i] - DECK, *way(i), max(0.5, half[i] - 1.25))]
    if bad: print(f'  {what}: {len(bad)} points blocked, e.g. {[tuple(np.round(pts[i], 1)) for i in bad[:8]]}')
    else: print(f'  {what}: clear')
    return bad
def check_floor(line, what):
    bad = [p for p in line if blocked(p[0], p[1], FLOOR, CAR_HALF)]
    if bad: print(f'  {what}: {len(bad)} points blocked, e.g. {[tuple(np.round(p, 1)) for p in bad[:8]]}')
    else: print(f'  {what}: clear ({len(line)} points)')
    return bad
print('route', n, 'points,', round(S[-1]), 'cells; start', start, 'the hall', i_hall, 'window', i_window_in, i_window_out,
      'the loops', o_loop1, i_top1, i_in2, i_out2, 'the jump', a_foot, a_lip, a_land, a_end, 'the leap', foot, lip, land, land_foot)
check_floor(pts[:i_ramp_foot], 'the rooms and the hall (the route)')
for k, rt in enumerate(routes): check_floor(rt, f'the rooms (way {k + 1})')
check(i_ramp_foot, i_window_out + 1, 'the ramp and the window')
check(i_window_out, a_lip + 1, 'the loops and the way to the jump')
check(a_land, lip + 1, 'the way home')
# (the jumps' flights: nothing within a car of the way it is carried)
for lo, hi, what in ((a_lip, a_land, 'the jump'), (lip, land, 'the leap')):
    bad = [i for i in range(lo, hi + 1) if blocked(pts[i][0], pts[i][1], h[i], CAR_BOX)]
    print(f'  {what}\'s flight: ' + (f'{len(bad)} points blocked, e.g. {[tuple(np.round(pts[i], 1)) for i in bad[:6]]}' if bad else 'clear'))
grade = np.abs(np.diff(h)) / (np.maximum(np.diff(S), 1e-6) * 512)
print('steepest: the ramp to the window %.3f, out of the window to over the roof %.3f, on up the first loop %.3f, down the second %.3f, '
      'round the south-west turret %.3f, down to the jump %.3f, its ramp %.3f, home %.3f, the take-off ramp %.3f' %
      (grade[i_ramp_foot:i_window_in].max(), grade[i_window_out:i_roof1].max(), grade[i_roof1:i_top1].max(), grade[i_in2:i_out2].max(),
       grade[i_out2:i_cross].max(), grade[i_cross:a_foot].max(), grade[a_foot:a_lip].max(), grade[a_end:foot].max(), grade[foot:lip].max()))
# (the bends: the tightest radius out of the window, from three points 2 cells apart)
def radius(i, k=4):
    a, b, c = pts[i - k], pts[i], pts[i + k]
    ab, bc, ca = np.linalg.norm(b - a), np.linalg.norm(c - b), np.linalg.norm(a - c)
    cross = abs((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]))
    return 1e9 if cross < 1e-9 else ab * bc * ca / (2 * cross)
bends = sorted((radius(i), i) for i in range(i_window_out + 4, lip - 4) if not (a_lip <= i <= a_land))
seen = []
for r, i in bends:
    if all(abs(S[i] - S[j]) > 15 for _, j in seen): seen.append((r, i))
    if len(seen) == 5: break
print('the tightest bends out of the window: ' + ', '.join('radius %.1f at %s' % (r, tuple(np.round(pts[i], 1))) for r, i in seen))
# (straight runs: the longest stretch whose way turns less than 2 degrees over 10 cells)
heading = np.degrees(np.arctan2(np.diff(pts[:, 1]), np.diff(pts[:, 0])))
run = best = 0; best_at = None
for i in range(i_window_out, lip - 20):
    dh = abs((heading[i + 20] - heading[i] + 180) % 360 - 180)
    run = run + 1 if dh < 2 else 0
    if run > best: best, best_at = run, i
print('the longest straight: %.0f cells, ending at %s' % (best * SP + 10, tuple(np.round(pts[best_at], 1)) if best_at else None))
# (where the road passes over itself -- the loops, the sixteen rooms' west side and middle way: the least height between the two)
over_min = (1e9, None)
for i in range(i_window_out, lip, 4):
    d = np.hypot(pts[i:lip, 0] - pts[i][0], pts[i:lip, 1] - pts[i][1])
    for j in np.nonzero((d < half[i] + half[i:lip]) & (S[i:lip] - S[i] > 60))[0][::2]:
        k = i + j; dh = abs(h[k] - h[i])
        if dh < over_min[0]: over_min = (dh, (tuple(np.round(pts[i], 1)), round(h[i]), round(h[k])))
print('the road over itself: the least height between two of its ways %.0f at %s' % over_min)
if over_min[0] < 2800: print('  WARNING: under 2,800 -- a car would hit the deck over it')
low = min((h[i] - DECK - max(ground(pts[i][0], pts[i][1]), -700), i) for i in range(i_window_out, lip))
print('the road out of the window: the least clearance over the ground %.0f at %s (height %.0f)' % (low[0], tuple(np.round(pts[low[1]], 1)), h[low[1]]))
low = min((h[i] - DECK - max(ground(pts[i][0] + dx, pts[i][1]) for dx in (-HALF - 3, 0, HALF + 3)), i) for i in range(lip, n))
print('the leap: lip %.0f at %s, its top %.0f, down at %s; %.1f cells; the least clearance over the ground under it and beyond %.0f at %s' %
      (h[lip], tuple(np.round(pts[lip], 1)), h[lip:land + 1].max(), tuple(np.round(pts[land], 1)), S[land] - S[lip], low[0], tuple(np.round(pts[low[1]], 1))))
print('the jump: lip %.0f at %s, its top %.0f, down at %s; %.1f cells' % (h[a_lip], tuple(np.round(pts[a_lip], 1)), h[a_lip:a_land + 1].max(),
      tuple(np.round(pts[a_land], 1)), S[a_land] - S[a_lip]))
print('the parts: the rooms and the hall %.0f cells, the ramp and the window %.0f, the first loop %.0f, across %.0f, the second loop %.0f, '
      'round the sixteen rooms to the jump %.0f, the jump %.0f, home to the lip %.0f, the leap %.0f' %
      (S[i_ramp_foot], S[i_window_out] - S[i_ramp_foot], S[i_top1] - S[i_window_out], S[i_in2] - S[i_top1], S[i_out2] - S[i_in2],
       S[a_lip] - S[i_out2], S[a_land] - S[a_lip], S[lip] - S[a_land], S[land] - S[lip]))
print('heights: through the window %.0f, the first loop over the roof %.0f and out %.0f, the second in %.0f and out %.0f, round the sixteen rooms '
      '%.0f down to %.0f, the jump\'s lip %.0f, home %.0f to %.0f, the lip %.0f (the roof %.0f)' %
      (WINDOW_H, TOP + H1_ROOF_OVER, H1_TOP, H2_IN_H, h[i_out2], away0, AWAY_LOW, a_lipH, HOME_SE, HOME_HIGH, lipH, TOP))
print('the opponents\' ways through the rooms: ' + ', '.join('%.0f cells' % (len(rt) - 1) for rt in routes) + ' (the route %.0f to the same place)' % S[i_hall])
print('about %.0f s to the lip at the top speed (34 km/h, 9.4 cells a second)' % (S[lip] / 9.44))

plan = {
    'originCellX': 0, 'originCellZ': 0,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in pts],
    'heights': [round(float(v), 1) for v in h],
    'open': True, 'start': start, 'finish': lip,
    'maxGrade': 2.0,
    'asphaltHalf': HALF - 1.0, 'curbHalf': HALF - 0.25, 'vergeHalf': HALF + 0.5, 'blend': 2.0,
    'raised': [0, n - 1], 'raisedHalf': HALF,
    'raisedHalfs': [round(float(v), 2) for v in half],
    # (no road through the rooms and up the long hall to the ramp: the cars on the floor, where they like; nor where the leap comes down --
    # never reached: a win's teleporter takes the car in mid-air, a loss's race again from the grid starts before it lands -- a stretch of
    # road on a pier out in the haze)
    'bare': [[0, i_ramp_foot - 1], [land, n - 1]],
    'openAreas': [[sq_x0, sq_z0, sq_x1, sq_z1, FLOOR], [hall['x0'], hall['z0'], hall['x1'] + 1, hall['z1'] + 1, FLOOR]],
    'floorArrows': [[round(float(x), 2), round(float(z), 2), FLOOR, dx, dz] for x, z, dx, dz in arrows],
    'openRoutes': [[[round(float(x), 2), round(float(z), 2)] for x, z in rt] for rt in routes],
    'guards': guards,
    # (the jump along the strip north of the north-east turret, and the leap north off the roof's middle way: each its ramp's foot, its lip,
    # the landing's lip and end; the camera behind the car)
    'arcJumps': [[a_foot, a_lip, a_land, a_end, 0], [foot, lip, land, land_foot, 0]],
    # (the fireplace made a teleporter: into room 151's west one, out of the one made on its south wall -- the island's layout)
    'teleports': L.get('teleports') or [],
    # (the camera rides the road behind the car, low -- 9 cells behind, 2,100 over the road, looking 7 cells ahead at a point 1,000 over
    # it, its sight line to the car's middle 500 over the road; in the rooms, where there is no road, it follows the car's own trail, as
    # low -- RACEMOD.CPP OpenCamera)
    'railCamera': [9, 2100, 7, 500, 1000],
    # (the ramp up to the round window the rooms' red carpet edged in gold; out of it the race-track mode's own road -- the user: "our
    # regular track colour with a red/white border on the edge")
    'themes': [{'from': 0, 'to': i_window_out, 'theme': 'carpet'}, {'from': i_window_out, 'to': n - 1, 'theme': 'standard'}],
    # (the palace -- its rooms, roofs, furniture and domes -- stays where it is under and beside the road: every decor of the island's own
    # bodies on, PALACE.OBL's from 151; and nothing is cut into the ground)
    'keepAbove': 0,
}
json.dump(plan, open(OUT, 'w'))
print('wrote', OUT)
