"""The Temple of Bú's Halloween race (the user, 2026-10-16: "Port the temple of Bu from LBA1 into LBA2 as a separate island ... After it's been
ported generate a Halloween inspired race track for it. This will be a separate release for a Halloween special track before our full racing
mod is released. Let's make it as creepy, scary, and Halloween inspired as we can do, let's turn our mushroom pickups into mini pumpkins. Add
lots of typical Halloween decor, pumpkins, witches, ghosts etc"), on the temple island (Terrain/Temple: TEMPLE.ILE, its layout TEMPLE.JSON).

LBA1's temple is a dungeon of three scenes at three heights -- the first floor (scene 8) and its long corridor east, 14,080 up; a lower hall in
scene 8, 11,008; the crypt (scene 40's rooms), 1,792 -- joined in LBA1 by narrow stairs, with dead ends and no way round. The lap is a raised
road (the race-track mode's, held on its rails) a little over the floors, through doorways cut in the walls where it passes them (the plan's
templeCuts: TempleObjects leaves those cells out), and between the heights on ramps -- and, down to the crypt and back up, on spirals through
the black void round the temple:
  - the start straight west along the long corridor;
  - north into the first floor's south side, down a ramp into the lower hall and round it, up into the first floor's west rooms and round
    them;
  - out over the first floor's open south edge into the dark, and down a spiral to the crypt;
  - through the crypt's three rooms -- west to east along their bottom, north through the east room;
  - out of its east side into the dark, up a spiral to the long corridor's east end: the start straight.
The start and finish line is on the straight through the crypt's west room, a slip lane beside it where the opponents wait while Twinsen
drives his qualifying lap.

The east route (2026-10-19, the user's test: "the green line is the proposed new track route for us to try, and the red dots are teleport
points to act as a shortcut to match a hidden door that's in the original game"; TEMPLE_ROUTE_EAST=0 gives the lap as it was): off the long
straight north up the first floor's east side instead -- down the stairs into the sunken room with the hidden door and up its stairs
at the far end, round a hairpin in the room north of it -- back west along the gallery north of the lower hall and over the hall, into the
U-bend (the lap no longer goes down into the lower hall). After the user drove it (the same day: "let's have it sit on the ground and follow
the stairs down and back up again instead of being elevated. This should allow us to drop the track guard rails where it's level with the
ground"), that stretch lies on the temple's floors, down and up the stairs, with no rails where the floor beside it is level with it: the
cars may drive off it onto the floor there (the ground stretch, below). And LBA1's hidden passage as a secret shortcut (TEMPLE_SHORTCUT=0:
none): the hidden door at the sunken room's north end (LBA1's stone door, scene 8 actor 29; walking into the passage behind it, zone 36,
puts Twinsen at point 30 in the west rooms -- Twinsen's life script) walled up, plain wall; a car driven off the road across the room's
floor into that stretch of wall is taken by a teleporter (RACEMOD.CPP teleport=) and is at once on the west rooms' floor in front of their
north wall (the user's red dot), plain wall too, from where it drives a short way back onto the lap -- the U-bend's way north moved east
under that room, its corner west across the room's mouth; the lap's checkpoints keep off the stretch it skips, so a lap through it counts;
the opponents keep to the lap.

Usage: temple_design.py <game folder with TEMPLE.JSON (and TEMPLE.ILE)> <out plan>, with TEMPLE_SOLIDS=<a folder `ScriptRoundTrip
templebuild` built the island into on its own> -- its TEMPLE.ILE's solid boxes are the floors the ground stretch lies on (else GAME's).
"""
import json, math, os, sys
import numpy as np
sys.path.insert(0, __file__.rsplit('\\', 1)[0] if '\\' in __file__ else '.')
from ile import load_ile, hqr_entries

GAME = sys.argv[1] if len(sys.argv) > 1 else r'E:\dump\TEMP\temple\game'
OUT = sys.argv[2] if len(sys.argv) > 2 else r'E:\dump\LBAAssembler\docs\racetrack\temple_track_plan.json'
L = json.load(open(GAME + r'\TEMPLE.JSON'))
SCALE, OX, OZ, BASE, MINY, LAYER = L['scale'], L['offsetX'], L['offsetZ'], L['baseY'], L['minY'], L['layer']
W, D = L['width'], L['depth']
bits = {}
for x, z, b, t, c in L['columns']: bits[(x, z)] = b


def top(layer):
    """The world height of a layer's top (a cell at that layer stands from top(layer - 1) to top(layer))."""
    return BASE + (layer - MINY + 1) * LAYER


FIRST, LOWER, CRYPT = top(8), top(4), top(-8)  # the first floor and its corridor, scene 8's lower hall, the crypt
SP = 0.5                                     # the plan's points, cells apart
DECK = 60                                    # the raised road over the floor it lies on
HALF = 4.5                                   # the road's half width (cells) ...
HALF_CORRIDOR = 4.0                          # ... along the long corridor (its floor 12 cells across)
CUT = 1.6                                    # cells either side of the road cut out of the walls, past its half width: a car held on the
                                             # road (its middle the rail's 1.25 inside its edge) with its box (2.8 round it) touches none
HEAD = 3600                                  # ... and that high over the road: the cars and the camera behind them
GRADE = 0.12                                 # the ramps' steepest
CAR_BOX = 2.8


# ---- the collision boxes (decors with a real box; world units) -- the temple's solid cells as LBA1 has them, nothing cut: the TEMPLE.ILE of
# TEMPLE_SOLIDS (a folder `ScriptRoundTrip templebuild` built the island into on its own), else GAME's (the ground stretch's floors are read
# from them: a race track's build of the island has its cuts in them)
SOLIDS = os.environ.get('TEMPLE_SOLIDS', GAME)
boxes = []
read, _ = hqr_entries(SOLIDS + r'\TEMPLE.ILE')
_, _, _, CUBES = load_ile(SOLIDS + r'\TEMPLE.ILE')
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


def path(corners, radii, sp=SP):
    """A line through `corners` (cells), each inner one rounded to its radius: straight runs and circular arcs, sp cells apart."""
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
    return resample(np.array(pts), sp)


def resample(pts, sp=SP):
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))]
    s = np.arange(0, d[-1] + 1e-9, sp)
    return np.stack([np.interp(s, d, pts[:, 0]), np.interp(s, d, pts[:, 1])], 1)


def spiral(cx, cz, r, phi0, turns, sp=SP):
    """Round (cx, cz) at radius r from the angle phi0 (0 east, pi/2 south), `turns` times -- positive turns the angle growing: clockwise
    seen from above (x east, z south) -- sp cells apart."""
    phi = np.linspace(phi0, phi0 + 2 * math.pi * turns, 40000)
    return resample(np.stack([cx + r * np.cos(phi), cz + r * np.sin(phi)], 1), sp)


def join(*pieces):
    pts = pieces[0]
    offsets = [0]
    for p in pieces[1:]:
        same = np.linalg.norm(p[0] - pts[-1]) < 1e-6
        offsets.append(len(pts) - (1 if same else 0))
        pts = np.concatenate([pts, p[1:] if same else p])
    return pts, offsets


def ease(t): return t * t * (3 - 2 * t)


# ---- the route (island cells: x east, z south)
CORR_Z = 466                                 # the long corridor's middle (its floor z 460..471, the frieze's wall north of it)
E_END = 556                                  # its east end, where the spiral up comes in
W_END = 386                                  # its west end: north into the first floor
EDGE_Z = 446                                 # the first floor's open south edge
# the spirals, each (x, z) its middle and r its radius (cells), round the anticlockwise way seen from above (x east, z south)
SPIRAL_W = (302, 560, 26)                    # west of the crypt: in at its west point heading south, out at its south point heading east
SPIRAL_E = (604, 520, 24)                    # east of it: in at its south point heading east, out at its east point heading north


def spiral(cx, cz, r, phi0, turns, sp=SP):
    """Round (cx, cz) at radius r from the angle phi0 (0 the east point, pi/2 the south one), the anticlockwise way seen from above --
    the angle falling -- `turns` times, sp cells apart."""
    phi = np.linspace(phi0, phi0 - 2 * math.pi * turns, 40000)
    return resample(np.stack([cx + r * np.cos(phi), cz + r * np.sin(phi)], 1), sp)


# (the east route, ROUTE_EAST: off the first floor's road north from the corridor's west end -- an S to the east side, EAST_X -- north up
# it over the sunken room (layer 0) and its stairs, a hairpin round in the room north of it (its top HAIR_Z), back south and west along the
# gallery north of the lower hall (GALLERY_Z) and on over the hall into the U-bend's way down, all at the first floor's height)
ROUTE_EAST = os.environ.get('TEMPLE_ROUTE_EAST', '1') != '0'
SHORTCUT = ROUTE_EAST and os.environ.get('TEMPLE_SHORTCUT', '1') != '0'
# (EAST_X: the north stairs' middle -- they are 9 cells across between their walls, x 404..413, the road's width; the south stairs 18)
EAST_X, HAIR_Z, HAIR_W, GALLERY_Z, S_FROM, S_TO = 408.5, 266, 385, 296, 426, 404
# (the U-bend's way north, LEG_X: x 300 -- with the shortcut, moved east under the room its car comes out in (the user, 2026-10-19, after
# driving it: "let's also tweak the track near the shortcut exit to make it easier to rejoin, I've added a basic track outline, but let's
# keep the smooth turn we already have" -- their outline the way north 11 cells further east, against the room's east wall): x 307, as far
# east as it goes with that wall standing (x 313: the road's half width and CUT short of it, nothing of it cut -- past it the black pit,
# x 316..326), so the corner west at its top lies across the room's mouth, under the car coming out; its corners' radii as they were)
LEG_X = 307 if SHORTCUT else 300
if ROUTE_EAST:
    lap1 = path([(E_END, CORR_Z), (W_END, CORR_Z), (W_END, S_FROM), (EAST_X, S_TO), (EAST_X, HAIR_Z), (HAIR_W, HAIR_Z), (HAIR_W, GALLERY_Z),
                 (340, GALLERY_Z), (340, 372), (LEG_X, 372), (LEG_X, 312), (248, 312), (248, 400), (276, 400), (SPIRAL_W[0] - SPIRAL_W[2], SPIRAL_W[1])],
                [0, 12, 14, 14, 11, 11, 10, 10, 10, 10, 12, 10, 10, 10, 0])
else:
    lap1 = path([(E_END, CORR_Z), (W_END, CORR_Z), (W_END, 312), (340, 312), (340, 372), (300, 372), (300, 312), (248, 312), (248, 400),
                 (276, 400), (SPIRAL_W[0] - SPIRAL_W[2], SPIRAL_W[1])], [0, 12, 12, 10, 10, 10, 12, 10, 10, 10, 0])
down = spiral(SPIRAL_W[0], SPIRAL_W[1], SPIRAL_W[2], math.pi, 1.25)
CRYPT_Z = SPIRAL_W[1] + SPIRAL_W[2]          # the straight through the crypt's west room (the start's)
lap2 = path([tuple(down[-1]), (432, CRYPT_Z), (432, 618), (500, 618), (500, SPIRAL_E[1] + SPIRAL_E[2]),
             (SPIRAL_E[0], SPIRAL_E[1] + SPIRAL_E[2])], [0, 10, 10, 12, 12, 0])
up = spiral(SPIRAL_E[0], SPIRAL_E[1], SPIRAL_E[2], math.pi / 2, 1.25)
lap3 = path([tuple(up[-1]), (SPIRAL_E[0] + SPIRAL_E[2], CORR_Z), (E_END, CORR_Z)], [0, 12, 0])
pts, (o1, o_down, o2, o_up, o3) = join(lap1, down, lap2, up, lap3)
# (a lap: its last point is its first)
if np.linalg.norm(pts[-1] - pts[0]) < 1e-6: pts = pts[:-1]
n = len(pts)
S = np.r_[0, np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))]
print('route', n, 'points,', round(S[-1] + np.linalg.norm(pts[0] - pts[-1])), 'cells')


def nearest(p, lo=0, hi=None):
    hi = n if hi is None else hi
    d = np.hypot(pts[lo:hi, 0] - p[0], pts[lo:hi, 1] - p[1])
    return lo + int(d.argmin())


# ---- the heights: each level from where it is reached to where it is left, ramps between (eased, no steeper than GRADE)
i_west = nearest((W_END, CORR_Z))
i_hall_in = nearest((W_END, 392), i_west)            # the lower hall's south wall
i_hall_n = nearest((W_END, 312), i_west)
i_hall_sw = nearest((340, 372), i_hall_n)
i_west_rooms = nearest((LEG_X, 372), i_hall_sw)
i_edge = nearest((276, EDGE_Z), i_west_rooms)
i_down_in, i_down_out = o_down, o2
i_crypt_w = nearest((372, SPIRAL_W[1] + SPIRAL_W[2]), i_down_out)
i_turn_n = nearest((500, 612), i_crypt_w)
i_room_e = nearest((500, SPIRAL_E[1] + SPIRAL_E[2]), i_turn_n)
i_up_in, i_up_out = o_up, o3
# (the climb up the east spiral ends CLIMB_EARLY cells short of the corridor's east end, before the giant web's doorway: ending there, the
# road was still climbing through the doorway, up to 113 under the corridor's height, so its floor cells were cut from under the deck --
# the 2026-10-18 audit's holes either side of the road under the web)
CLIMB_EARLY = 6
i_climbed = nearest((E_END + CLIMB_EARLY, CORR_Z), o3)
anchors = [  # (point, height of the floor the road lies on there), in the lap's order
    (0, FIRST), (i_west, FIRST),
    (i_hall_in - 6, FIRST), (i_hall_in + int(1.5 * 3072 / (GRADE * 512) / SP), LOWER),  # down into the lower hall (eased: 1.5 times as long)
    (i_hall_sw - int(1.5 * 3072 / (GRADE * 512) / SP), LOWER), (i_hall_sw, FIRST),      # up again before it turns west out of it
    (i_edge, FIRST),
    (i_crypt_w, CRYPT),                                                                 # down: the spiral west of the crypt
    (i_turn_n, CRYPT), (i_room_e, top(-5)),                                             # up into the crypt's east room
    (i_up_in, top(-5)),
    (i_climbed, FIRST), (n - 1, FIRST),                                                 # up: the spiral east of it
]
if ROUTE_EAST:
    # (round the east side and over the lower hall at the first floor's height: not down into the hall and up again)
    anchors = [a for a in anchors if a[0] <= i_west or a[0] >= i_edge]
floor_h = np.zeros(n)
for (a, ha), (b, hb) in zip(anchors, anchors[1:]):
    for i in range(a, b + 1):
        t = (S[i] - S[a]) / max(1e-9, S[b] - S[a])
        floor_h[i] = ha + (hb - ha) * (ease(t) if abs(hb - ha) < 4000 else t)
h = floor_h + DECK


# ---- the ground stretch (ROUTE_EAST, the user, 2026-10-19, after driving the east route: "Our new stretch of road, let's have it sit on
# the ground and follow the stairs down and back up again instead of being elevated. This should allow us to drop the track guard rails
# where it's level with the ground"): from the first floor's south side (GROUND_FROM) round the east side, the hairpin and along the gallery
# to the lower hall's parapet (GROUND_TO), the road lies on the temple's own floors -- its deck DECK over the highest floor under it -- down
# the sunken room's south stairs, through the room and back up its north stairs, a ramp laid on the stairs' nosings (the stairs are as steep
# as LBA1 built them, 50 %). Its rise and fall smoothed over GROUND_SMOOTH points (a Gaussian's sigma), then lifted -- by as little as
# keeps it over every floor under it, that lift smoothed the same way -- so its slope is even down the steps and its crests and feet are
# rounded, never under the floor; past the gallery it is the first floor's height over the lower hall, as before (a bridge: rails). Beside it, where the floor is level with it, no rails -- the cars may drive off it onto the floor there: the
# race-track mode's floor reaches across that floor (RACEMOD.CPP's strips: the plan's raisedFloors, never drawn) to the walls round it, or
# to a drop's lip (its rail there, unseen); and the deck's pieces have flat curbs and no rails on those sides (raisedFlush).
GROUND_FROM, GROUND_TO = (W_END, 438), (360, GALLERY_Z)
GROUND_SMOOTH = 3
ground = np.zeros(n, bool)
if ROUTE_EAST:
    ground[nearest(GROUND_FROM, i_west):nearest(GROUND_TO, i_west) + 1] = True
gpts = np.flatnonzero(ground)
# the solid boxes by island cell (world units), for the floors under the road and beside it
box_at = {}
for b_, (bx0, by0, bz0, bx1, by1, bz1) in enumerate(boxes):
    for cz_ in range(int(math.floor(bz0 / 512)), int(math.floor((bz1 - 1) / 512)) + 1):
        for cx_ in range(int(math.floor(bx0 / 512)), int(math.floor((bx1 - 1) / 512)) + 1):
            box_at.setdefault((cx_, cz_), []).append(b_)
# (cells the race fills -- the hidden passage's wall, below: TEMPLE_WALLS -- solid for the floors beside the road)
extra_solid = []


def boxes_at(x, z):
    wx, wz = x * 512, z * 512
    out = [boxes[b_] for b_ in box_at.get((int(math.floor(x)), int(math.floor(z))), ()) if boxes[b_][0] <= wx <= boxes[b_][3] and boxes[b_][2] <= wz <= boxes[b_][5]]
    out += [e for e in extra_solid if e[0] <= wx <= e[3] and e[2] <= wz <= e[5]]
    return out


def floor_top(x, z, ceil):
    """The highest solid top under island cell place (x, z) no higher than ceil (world units), or None."""
    tops = [b_[4] for b_ in boxes_at(x, z) if b_[4] <= ceil]
    return max(tops) if tops else None


def tangent(i):
    a, b = pts[max(0, i - 1)], pts[min(n - 1, i + 1)]
    d = b - a
    return d / max(1e-9, np.linalg.norm(d))


# (the floor under the road: the highest solid top within GROUND_ALONG of the point along the road and across its width -- the stairs'
# drawn steps stand up to a step further up them than their solid ones, so a road laid on the solid nosings had the drawn ones through it)
GROUND_ALONG = 0.9
gfloor = np.zeros(n)
if ROUTE_EAST:
    for i in gpts:
        u = tangent(i); nrm = np.array([-u[1], u[0]])
        tops = [floor_top(*(pts[i] + nrm * a + u * b), FIRST + 100) for a in np.linspace(-(HALF - 0.3), HALF - 0.3, 13)
                for b in np.linspace(-GROUND_ALONG, GROUND_ALONG, 7)]
        tops = [t for t in tops if t is not None]
        gfloor[i] = max(tops) if tops else FIRST
    g = gfloor[gpts] + DECK
    xs = S[gpts] * 512
    kern = np.exp(-0.5 * (np.arange(-4 * GROUND_SMOOTH, 4 * GROUND_SMOOTH + 1) / GROUND_SMOOTH) ** 2)
    kern /= kern.sum()
    def smooth(f):
        w = len(kern) // 2
        return np.convolve(np.r_[np.full(w, f[0]), f, np.full(w, f[-1])], kern, 'valid')
    def widest(f, w=GROUND_SMOOTH):
        return np.array([f[max(0, k - w):k + w + 1].max() for k in range(len(f))])
    hg = smooth(g)
    lift = np.zeros(len(g))
    for _ in range(60):
        short = g - (hg + lift)
        if short.max() <= 0.5: break
        lift += smooth(widest(np.clip(short, 0, None)))
    hg = hg + lift
    assert (hg >= g - 0.5).all()
    h[gpts] = hg
    gd = np.diff(hg) / (np.diff(xs) + 1e-9)
    print('ground stretch: points %d..%d, %.0f cells; floors %d..%d; the road %d..%d over them (over the nosings at the most %d), grades to %.0f %%'
          % (gpts[0], gpts[-1], S[gpts[-1]] - S[gpts[0]], gfloor[gpts].min(), gfloor[gpts].max(), (hg - gfloor[gpts]).min(), (hg - gfloor[gpts]).max(),
             (hg - g).max(), 100 * np.abs(gd).max()))
grade = np.abs(np.diff(h)) / (np.maximum(np.diff(S), 1e-6) * 512)
print('steepest %.3f at point %d; the drop %d over %d cells, the climb %d over %d' % (grade.max(), grade.argmax(), FIRST - CRYPT, round(S[i_crypt_w] - S[i_edge]),
      FIRST - top(-5), round(S[n - 1] - S[i_up_in])))


# ---- the cells the road passes through: cut out of the temple (the plan's templeCuts) -- every layout cell within the road's half width and
# CUT of a point whose height overlaps the road's slab and the room over it
def layout_of(cx, cz): return int(math.floor((cx - OX) / SCALE)), int(math.floor((cz - OZ) / SCALE))


half = np.full(n, HALF)
corridor = (np.abs(pts[:, 1] - CORR_Z) < 1.0) & (pts[:, 0] > W_END + 10) & (pts[:, 0] < E_END + 1)
half[corridor] = HALF_CORRIDOR


# ---- the shortcut (SHORTCUT): LBA1's hidden passage. In LBA1 the sunken room under the first floor's east side (layer 0, LBA1's y 256) has a
# stone door at its north end (scene 8 actor 29, sprite 52, at LBA1's (29024, 256, 8464): island (394.1, 305.6); it stays only while the
# statue of Bù stands in its place, zone 18 -- Twinsen moves the statue and the door is gone); the alcove behind it is zone 36 (sceneric 16,
# island x 393.5..399.5, z 293.5..299.5): walking into it, Twinsen's life script puts him at point 30 (island (264.6, 273.6)) in the west
# rooms, through a stone door in their north wall (actor 21), and zone 37 behind that door takes him back to point 29 by the first door.
# The race's, a secret (the user, 2026-10-19: "Remove the section of track leading to the shortcut, and replace the highlighted portal with
# just plain wall. When we drive into the wall at the correct point we should teleport. Let's also do the same with the shortcut exit, this
# is to be a secret hidden shortcut so we don't wish to highlight it"): no road to it and no doorway -- the alcove's mouth walled up
# (TEMPLE_WALLS: its two columns of the room's north wall filled with the bricks of the wall beside them, layout column (59, 16), up to the
# lintel; LBA1's door, in there, left out), the room's floor in front of it the ground road's floor (the cars drive off the road onto it:
# raisedFloors). A car driven into that stretch of wall -- its middle within SECRET_DEEP of the wall's face, between SECRET_X -- is taken
# (RACEMOD.CPP teleport=) and comes out in the west rooms where the user's red dot is (OUT_X, OUT_Z: in front of their north wall, plain
# wall -- no doorway there either), looking OUT_BETA (south), on their floor (an open area: OPEN_EXIT, its floor the race-track mode's, the
# camera on the car's trail) a short drive down onto the lap's corner west across the room's mouth (the way north moved under it: LEG_X),
# whose rails on that side are left off (REENTRY: the floor beside it). Out at once (the user, after driving it with a passage of 3.5 s in
# the dark: "the screen fades to black for too long, we need to instantly come out of the exit"): OUT_PASSAGE 0 -- no black screen, the car
# out the frame it drives into the wall, the camera behind it there -- at OUT_SPEED at the most (world units a second: 10,000 is 70 km/h,
# the race car's 110 km/h kept to two thirds of it, slow enough to turn onto the corner 25 cells on). The test pilot's lap through it: 8.6 s
# quicker than its 59.1 s round the lap (out standing, OUT_SPEED 0: 7.5 s; at its own speed, -1: 8.8 s -- most of it the 307 cells it
# skips). The lap's checkpoints keep off the stretch it skips (the plan's shortcuts), so a lap through it counts; the opponents keep to the lap.
SECRET_X, SECRET_FACE, SECRET_DEEP = (395.4, 400.6), 307.0, 2.0
TEMPLE_WALLS = [[57, 16, 1, 6, 59, 16], [58, 16, 1, 6, 59, 16]] if SHORTCUT else []
OUT_X, OUT_Z, OUT_BETA, OUT_SPEED, OUT_PASSAGE = 306.0, 282.5, 0, 10000, 0
OPEN_EXIT = [299.2, 271.2, 313.8, 309.0] if SHORTCUT else None
REENTRY = ((LEG_X, 334), (298, 312))            # the lap's points between these (the way north's last stretch, the corner -- to the room's
                                                # railing, x 296..299: past it, the west rooms' wide floor): floor beside, no rail
SKIP_TO = (280, 312)                            # ... and the lap's point past where the car comes back onto it (the stretch it skips ends there)
# (no decor where the shortcut's car drives: on the sunken room's floor between the road and the walled-up door, from its face APPROACH
# cells south -- the user: "clean up the decor immediately on the approach to the shortcut to make it easier to enter" -- the floor strip
# beside the road past the niches in the room's west wall, the niches' fronts; and from where it comes out down onto the road, EXIT_WAY)
APPROACH = 43.0
EXIT_WAY = [OPEN_EXIT[0], OPEN_EXIT[1], OPEN_EXIT[2], 316.0] if SHORTCUT else None
for lx_, lz_, y0_, y1_, _, _ in TEMPLE_WALLS:
    extra_solid.append(((OX + SCALE * lx_) * 512, top(y0_ - 1), (OZ + SCALE * lz_) * 512, (OX + SCALE * (lx_ + 1)) * 512, top(y1_), (OZ + SCALE * (lz_ + 1)) * 512))


def secret_off(x, z, wide=False):
    """How far (x, z) is from the places the shortcut's car drives off the road -- the floor before the walled-up door, the west rooms'
    floor it comes out on (cells; 0 in them): no decor there. (Each piece of decor is placed clear of those, as since the shortcut was made;
    `wide`, the approach's APPROACH cells and the way from the room down onto the road, EXIT_WAY: what is in them is taken away after, so that
    the decor along the rest of the lap stays as it was.)"""
    if not SHORTCUT: return 1e9
    best = 1e9
    places = ((SECRET_X[0] - 2, SECRET_FACE, EAST_X - HALF, SECRET_FACE + (APPROACH if wide else 16)), tuple(EXIT_WAY if wide else OPEN_EXIT))
    for x0, z0, x1, z1 in places:
        best = min(best, math.hypot(max(x0 - x, 0, x - x1), max(z0 - z, 0, z - z1)))
    return best


# ---- the slip lane (the user, 2026-10-17: "We need to add a slip lane for the temple track, currently trying to complete a qualifying lap
# gets blocked with the other cars on the start/finish line"): where the opponents wait while Twinsen drives his qualifying lap, out of the
# racing lane -- their grid spots, where the race-track mode stood them while it had nowhere else (the waiting spots on the crypt's floor
# this plan gave before were in the cube north of the start's, and the build writes only the start cube's). A strip of road beside the
# start straight (the plan's raisedStrips, as the Emerald Moon's pit lane: RaceTrackBuilder's StripPieces, RACEMOD.CPP StripLo/Hi), on its
# north side -- the inside of the lap, away from the racing line, which keeps to the straight's middle and goes to its south side for the
# corner after the line: out of the road's edge past the west room's doorway walls, apart from it beside the grid and through the start
# line (clear space between them), back into it before the corner. Across, cells: negative north (the builder's Across, the left of the
# way the lap runs, is south here). All of it in the start's cube: the cube north of it (z < 576) isn't one of the race's scenes, so the
# engine holds a car half a cell inside the edge (z 576.5) -- the lane's rail (1.25 inside its edge) holds it no further north than that.
SLIP_FAR, SLIP_NEAR = -10.75, -6.25          # the lane's north edge and its south one (1.75 cells of clear space from the road's edge)
SLIP_OUT = (389.5, 394.5)                    # x: its north edge out of the road's edge, ...
SLIP_APART = (393.5, 395.5)                  # ... then its south edge away from it -- ...
SLIP_BACK = (414.5, 416.5)                   # ... back to it ...
SLIP_IN = (416.0, 421.0)                     # ... and its north edge back in, before the corner (its arc from x 422)
SLIP_SPOTS = [396.0, 400.0, 404.0, 408.0, 412.0]     # x: the waiting spots along its middle, the first opponent's first (five: a grid's)


def ramp(x, a, b): return min(1.0, max(0.0, (x - a) / (b - a)))


def slip_lane(half, quiet=False):
    """The slip lane beside a road of these half widths: its edges at each point (cells across, 0 0 where there is none) and its points."""
    lo, hi = np.zeros(n), np.zeros(n)
    for i in range(n):
        x, z = pts[i]
        if abs(z - CRYPT_Z) > 0.01 or abs(h[i] - (CRYPT + DECK)) > 1 or not SLIP_OUT[0] <= x <= SLIP_IN[1]: continue
        far = -half[i] + (SLIP_FAR + half[i]) * ramp(x, *SLIP_OUT) * (1 - ramp(x, *SLIP_IN))
        near = -half[i] + (SLIP_NEAR + half[i]) * ramp(x, *SLIP_APART) * (1 - ramp(x, *SLIP_BACK))
        if near - far > 1e-6: lo[i], hi[i] = round(far, 3), round(near, 3)
    on = np.nonzero(hi > lo)[0]
    if not quiet: print('slip lane: points %d..%d, x %.1f..%.1f, %.2f..%.2f cells across' % (on[0], on[-1], pts[on[0]][0], pts[on[-1]][0], lo.min(), hi[on].max()))
    return lo, hi, on


slip_lo, slip_hi, slip = slip_lane(half, quiet=True)


def slip_off(x, z):
    """How far (x, z) is from the slip lane (cells; 0 on it)."""
    best = 1e9
    if not (SLIP_OUT[0] - 30 < x < SLIP_IN[1] + 30 and abs(z - CRYPT_Z) < 30): return best
    for i in slip:
        u = pts[min(n - 1, i + 1)] - pts[max(0, i - 1)]; u = u / max(1e-9, np.linalg.norm(u)); nrm = np.array([-u[1], u[0]])
        d = np.array([x, z]) - pts[i]
        a, s = float(d @ nrm), abs(float(d @ u))
        best = min(best, math.hypot(s, max(slip_lo[i] - a, 0.0, a - slip_hi[i])))
    return best


# (2026-10-18, the port's audit: the cuts left black pits beside the road -- floor cells beside the ramps cut down with the walls -- and
# thin tops of walls hanging over it) -- each island cell within reach of the road's points (every other point): a cell of a layout
# column there is cut where it reaches into the room over the road, from DECK - 20 under the road's surface (no floor pokes through the
# deck) to HEAD over a point's. The surface it is kept under: under the deck (within DECK_EDGE of its edge: the car's box, 633 round its
# middle and unturned, reaches half a cell past the edge on a diagonal), the lowest of the road's within the car's length (CAR_ALONG)
# of the point nearest the cell; past it, beside the road, the highest within reach -- so the floor beside a ramp stays where it is no
# higher than the road beside it, the car held on its deck never reaching it. Each pass of the road by a column (its points within
# reach SAME_PASS apart in height: the spirals' turns, the corridor over the crypt) on its own.
SAME_PASS = 1500
DECK_EDGE = 1.0
CAR_ALONG = 2.0
LAP = S[-1] + float(np.linalg.norm(pts[0] - pts[-1]))
def along(a, b):
    d = abs(S[a] - S[b])
    return min(d, LAP - d)


def passes(points):
    """The points (indices) in groups by height: each group a pass of the road (SAME_PASS apart from the next)."""
    out = []
    for i in sorted(points, key=lambda k: (h[k], k)):
        if out and h[i] - h[out[-1][-1]] < SAME_PASS: out[-1].append(i)
        else: out.append([i])
    return out


def present(b, y): return (b >> (y - MINY)) & 1


def road_cuts(half, reach, quiet=False):
    """The cells cut for a road of these half widths, each island cell within `reach` of a point (and CUT of the slip lane's edges, as it
    is now: slip_lo, slip_hi) -- and the maps the floors under them are worked out from: (cuts, cell_points, col_cells, col_points,
    rooms)."""
    cell_points = {}                                # island cell -> {point: (distance squared, under the deck)}

    def near(cell, i, d2, deck):
        e = cell_points.setdefault(cell, {})
        if i not in e or d2 < e[i][0]: e[i] = (d2, e.get(i, (0, False))[1] or deck)
        elif deck: e[i] = (e[i][0], True)

    for i in range(0, n, 2):
        # (the ground stretch's own rule, below: GROUND_CUT)
        if ground[i]: continue
        x, z = pts[i]
        r_ = reach[i]
        for cz in range(int(math.floor(z - r_)), int(math.floor(z + r_)) + 1):
            for cx in range(int(math.floor(x - r_)), int(math.floor(x + r_)) + 1):
                d2 = (cx + 0.5 - x) ** 2 + (cz + 0.5 - z) ** 2
                if d2 > r_ * r_: continue
                near((cx, cz), i, d2, d2 <= (half[i] + DECK_EDGE) ** 2)
    # (and over the slip lane, as over the road: CUT past its edges)
    for i in slip:
        for cz in range(int(math.floor(pts[i][1] + slip_lo[i] - CUT)), int(math.floor(pts[i][1] + slip_hi[i] + CUT)) + 1):
            for cx in range(int(math.floor(pts[i][0] - 1)), int(math.floor(pts[i][0] + 1)) + 1):
                off = slip_off(cx + 0.5, cz + 0.5)
                if off > CUT: continue
                near((cx, cz), i, (cx + 0.5 - pts[i][0]) ** 2 + (cz + 0.5 - pts[i][1]) ** 2, off <= DECK_EDGE)
    col_cells = {}
    for cell in cell_points: col_cells.setdefault(layout_of(*cell), []).append(cell)
    col_points = {key: sorted({i for c in cells_ for i in cell_points[c]}) for key, cells_ in col_cells.items()}

    def room_of(key):
        """A column's rooms over the road: each pass's (the highest a cell may top out at and not be cut, and its points' heights)."""
        out = []
        for p in passes(col_points[key]):
            mine = set(p)
            lo = math.inf
            for cell in col_cells[key]:
                e = {i: v for i, v in cell_points[cell].items() if i in mine}
                if not e: continue
                j = min(e, key=lambda i: (e[i][0], i))
                if any(deck for _, deck in e.values()):
                    lo = min(lo, min(h[i] for i in e if along(i, j) <= CAR_ALONG) - DECK + 20)
                else:
                    lo = min(lo, max(h[i] for i in p) - DECK + 20)
            out.append((lo, [h[i] for i in p]))
        return out

    cuts = set()
    rooms = {key: room_of(key) for key in col_points}
    for key, room in rooms.items():
        b = bits.get(key, 0)
        for y in range(MINY, L['maxY'] + 1):
            if present(b, y) and any(top(y) > lo and top(y - 1) < max(hs) + HEAD for lo, hs in room): cuts.add((key[0], y, key[1]))
    # (the ground stretch: the road on the floor, its walls standing beside it -- the north stairs' between them, as wide as the road -- so
    # only what stands on the floor under the road and GROUND_CUT past its edge, up to HEAD over it, is cut: each island cell there by the
    # road's point nearest it, a cell whose foot is the floor there or higher -- never a stair under the deck, whose foot is its step below)
    ground_cells = {}
    for i in gpts:
        x, z = pts[i]
        r_ = half[i] + GROUND_CUT
        for cz in range(int(math.floor(z - r_)), int(math.floor(z + r_)) + 1):
            for cx in range(int(math.floor(x - r_)), int(math.floor(x + r_)) + 1):
                d2 = (cx + 0.5 - x) ** 2 + (cz + 0.5 - z) ** 2
                if d2 <= r_ * r_ and d2 < ground_cells.get((cx, cz), (1e9, 0))[0]: ground_cells[(cx, cz)] = (d2, i)
    ground_cut = 0
    for (cx, cz), (_, i) in ground_cells.items():
        key = layout_of(cx + 0.5, cz + 0.5)
        b = bits.get(key, 0)
        for y in range(MINY, L['maxY'] + 1):
            if present(b, y) and top(y - 1) >= gfloor[i] - 20 and top(y - 1) < h[i] + HEAD and (key[0], y, key[1]) not in cuts:
                cuts.add((key[0], y, key[1])); ground_cut += 1
    if ROUTE_EAST and not quiet: print('ground stretch: %d cells cut where something stands on the floor under the road' % ground_cut)
    # ... and what a cut leaves of a column over it one or two layers thick -- a wall's top hanging over the road's edge, a thin slab of
    # lintel -- taken with it
    caps = 0
    for key in {(c[0], c[2]) for c in cuts}:
        b = bits.get(key, 0)
        high = max(c[1] for c in cuts if (c[0], c[2]) == key)
        over = [y for y in range(high + 1, L['maxY'] + 1) if present(b, y)]
        if 0 < len(over) <= THIN:
            cuts.update((key[0], y, key[1]) for y in over)
            caps += len(over)
    if not quiet: print('cells cut', len(cuts), f'({caps} of them thin tops left over the cuts)')
    return cuts, cell_points, col_cells, col_points, rooms


GROUND_CUT = 0.3
THIN = 2
# (the cuts the road as it was before 2026-10-20 made, 4.5 cells either side of its middle (4 along the long corridor) and CUT past that:
# the doorways the wider road fills, WIDEN below)
cuts0 = road_cuts(half, half + CUT, quiet=True)[0]


# ---- the road's width (the user, 2026-10-20: "Our Temple track is too narrow, let's widen it at every possible point but keep the same
# layout. For example the start/finish lane could be widened to fill the entire gap we've made in the wall, this should be mirrored in as
# many places as possible to add extra track width"): the lap's middle as it was, the raised road as wide as the temple lets it be --
# out from each side of its middle, a ray every point, to the first thing standing in the road's room (from DECK - 20 under its surface
# to HEAD over it) that the road's doorways as they were (cuts0) left standing -- a wall's face, a doorway's jamb, a column, a step
# up, the walled-up door (TEMPLE_WALLS) -- WALL_GAP short of it: the doorways the road went through filled, their walls left as they
# were, and in the halls and over the dark out to HALF_MOST at the most. Short of another pass of the lap at about its height -- each
# of the two to the middle between them, ROAD_GAP apart -- and of the slip lane beside the start straight (SLIP_GAP from it, where it is
# apart from the road); within the bends' radii (BEND_GAP inside their middles). The narrower side's for both (the middle stays where it
# is), never narrower than before, changing by WIDEN_RATE a cell along at the most -- the road narrows before a doorway, widens after
# it. The ground stretch keeps its width: its kerbs are flat where the floor beside it is level, the cars drive onto that floor.
# TEMPLE_WIDE=0: the road as it was.
WIDE = os.environ.get('TEMPLE_WIDE', '1') != '0'
HALF_MOST = 8.0
WALL_GAP = 0.5                                  # a wall's face from the road's edge, at the least: a car held 1.25 inside its rail, its
                                                # box (1.24 round its middle) half a cell past it on a diagonal
ROAD_GAP = 2.5
SLIP_GAP = 0.75
BEND_GAP = 2.0
WIDEN_RATE = 0.2
WIDEN_STEP = 0.25
CUT_WIDE = 0.0                                  # the wider road's own cuts: what is under its deck (nothing stands there but what its
                                                # old cuts took -- the rays saw to that) and CUT past its old edge, as before


# (and LBA1's actors standing beside the lap that the build would leave out under a road over them -- TempleActors.NearRoad, within its
# RoadMargin of the deck's edge -- each [x0, z0, x1, z1, its foot, its top] (island cells, world units), from `ScriptRoundTrip templeactors
# models`: the spike trap at the west rooms' south-west corner, 8:2; the others near the road lie under its deck or were left out already)
ACTORS_BESIDE = [[239.4, 310.5, 243.2, 312.1, 14050, 21610]]
ACTOR_GAP = 0.75                                # past WALL_GAP: NearRoad's margin is half a cell, from the built road's edge


def stands_in(p, lo, hi, cutset):
    """Whether something of the temple left standing (not in cutset) at island place p reaches between heights lo and hi."""
    if any(x0 - ACTOR_GAP <= p[0] <= x1 + ACTOR_GAP and z0 - ACTOR_GAP <= p[1] <= z1 + ACTOR_GAP and y0 < hi and y1 > lo for x0, z0, x1, z1, y0, y1 in ACTORS_BESIDE):
        return True
    key = layout_of(*p)
    b = bits.get(key, 0)
    if b and any(present(b, y) and (key[0], y, key[1]) not in cutset and top(y) > lo and top(y - 1) < hi for y in range(MINY, L['maxY'] + 1)):
        return True
    return any(e[0] <= p[0] * 512 <= e[3] and e[2] <= p[1] * 512 <= e[5] and e[1] < hi and e[4] > lo for e in extra_solid)


def bend_radius(i, k=6):
    """The lap's radius of turn at point i (cells; inf on a straight) and the side its middle is to (1 the normal's, -1 the other)."""
    a, b, c = pts[(i - k) % n], pts[i], pts[(i + k) % n]
    ab, bc, ca = np.linalg.norm(b - a), np.linalg.norm(c - b), np.linalg.norm(a - c)
    cross = (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0])
    if abs(cross) < 1e-9: return math.inf, 0
    return ab * bc * ca / (2 * abs(cross)), (1 if cross > 0 else -1)


def widths(half0):
    """The wider road's half widths, the room at each point before the tapers, and what holds each point ('wall', 'road', 'slip',
    'bend', 'most', 'ground')."""
    room = np.full(n, HALF_MOST)
    side_room = np.zeros((n, 2))
    why = np.array(['most'] * n, dtype=object)
    for i in range(n):
        if ground[i]:
            room[i], why[i] = half0[i], 'ground'
            continue
        u = tangent(i); nrm = np.array([-u[1], u[0]])
        # (the room over the deck as the cuts have it: from the lowest of the road's within a car's length, CAR_ALONG)
        k_ = int(CAR_ALONG / SP)
        lo, hi = min(h[(i + k) % n] for k in range(-k_, k_ + 1)) - DECK + 20, h[i] + HEAD
        r_, side_ = bend_radius(i)
        # (the lap's other passes near it at about its height)
        d_ = np.hypot(pts[:, 0] - pts[i][0], pts[:, 1] - pts[i][1])
        a_ = np.abs(S - S[i]); a_ = np.minimum(a_, LAP - a_)
        others = np.flatnonzero((d_ < 2 * HALF_MOST + ROAD_GAP + 1) & (a_ > 2 * ROAD_GAP + 2) & (np.abs(h - h[i]) < HEAD + DECK))
        # (beside the start straight: the slip lane where it is apart from the road -- the road SLIP_GAP short of its near edge; where it
        # leaves the road and comes back to it, it is the road's side, wherever that is)
        x_, z_ = pts[i]
        by_slip = abs(z_ - CRYPT_Z) < 0.01 and abs(h[i] - (CRYPT + DECK)) <= 1 and SLIP_OUT[0] - 1 <= x_ <= SLIP_IN[1] + 1
        for sd in (-1, 1):
            t = half0[i]
            got, what = HALF_MOST, 'most'
            while t < HALF_MOST + WALL_GAP:
                t += WIDEN_STEP
                p = pts[i] + nrm * sd * t
                if stands_in(p, lo, hi, cuts0):
                    # (the face: between the last clear step and this one, to a fiftieth of a cell)
                    a_t, b_t = t - WIDEN_STEP, t
                    while b_t - a_t > 0.02:
                        m_t = (a_t + b_t) / 2
                        if stands_in(pts[i] + nrm * sd * m_t, lo, hi, cuts0): b_t = m_t
                        else: a_t = m_t
                    got, what = a_t - WALL_GAP, 'wall'
                    break
                if t > HALF_MOST: continue
                # (another pass of the lap: each to the middle between them, less half ROAD_GAP)
                if len(others) and any(a_[j] > 2 * (t + ROAD_GAP) + 2 and math.hypot(pts[j][0] - p[0], pts[j][1] - p[1]) < t + ROAD_GAP for j in others):
                    got, what = t - WIDEN_STEP, 'road'
                    break
                if not by_slip and slip_off(*p) < SLIP_GAP:
                    got, what = t - WIDEN_STEP, 'slip'
                    break
            # (from the west room's doorway, 4 cells before the lane leaves the road: no wider there than beside the lane -- the road
            # through the doorway as wide as the lane lets it be all along the grid, not wider and narrower again)
            if by_slip and sd == -1 and SLIP_OUT[0] - 4 <= x_ <= SLIP_BACK[1] and -SLIP_NEAR - SLIP_GAP < got:
                got, what = -SLIP_NEAR - SLIP_GAP, 'slip'
            if sd == side_ and r_ - BEND_GAP < got:
                got, what = r_ - BEND_GAP, 'bend'
            side_room[i, (sd + 1) // 2] = got
            if got < room[i]: room[i], why[i] = got, what
        room[i] = max(room[i], half0[i])
    # (no wider than a point's room any further along than WIDEN_RATE lets it narrow to the next's: the least over the lap of each
    # point's room and WIDEN_RATE a cell from it)
    out = room.copy()
    for i in np.argsort(room):
        span = int((HALF_MOST - out[i]) / WIDEN_RATE / SP) + 2
        for k in range(-span, span + 1):
            j = (i + k) % n
            lim = out[i] + WIDEN_RATE * along(i, j)
            if lim < out[j]: out[j] = lim
    out = np.maximum(out, half0)
    widths.sides = side_room
    return out, room, why


half0 = half.copy()
if WIDE:
    half, room_, why_ = widths(half0)
    slip_lo, slip_hi, slip = slip_lane(half)
    # (two passes of the lap side by side, edge to edge, after the narrowing's tapers)
    tight = min(((math.hypot(*(pts[i] - pts[j])) - half[i] - half[j], i, j) for i in range(0, n, 4) for j in range(0, n, 4)
                 if i < j and along(i, j) > 2 * (half[i] + half[j] + ROAD_GAP) + 2 and abs(h[i] - h[j]) < HEAD + DECK), default=(math.inf, -1, -1))
    raised_ = ~ground
    print('the road widened: %.2f..%.2f cells either side of its middle (%.2f on average) from %.2f..%.2f (%.2f); held by %s; two passes of the lap '
          'side by side %.1f cells apart at the least (points %d, %d)' % (half[raised_].min(), half[raised_].max(), half[raised_].mean(), half0[raised_].min(),
          half0[raised_].max(), half0[raised_].mean(), {w: int((why_ == w).sum()) for w in sorted(set(why_))}, *tight))
    cuts, cell_points, col_cells, col_points, rooms = road_cuts(half, np.maximum(half0 + CUT, half + CUT_WIDE))
    print('cells cut for the wider road: %d more than before (%d), %d fewer' % (len(cuts - cuts0), len(cuts0), len(cuts0 - cuts)))
else:
    slip_lo, slip_hi, slip = slip_lane(half)
    cuts, cell_points, col_cells, col_points, rooms = road_cuts(half, half + CUT)


# ---- the floors the cuts take (TempleIsland.Carve fills them again): for each column the road comes within reach of, the layer of the
# floor round it -- the commonest top among its neighbours' kept cells no higher than the road's surface there (the nearest point's) --
# and the highest a cell may stand in it without reaching into the road's room. Where the cuts (or what they took with them: a statue
# standing on no floor, a row of rocks down to its foot) leave the column nothing from that floor up to that height, the build lays a
# floor cell there, at the lower of the two: a doorway's floor where LBA1 had a wall standing on nothing, the ledge of a trench where a
# ramp goes down through a floor. Each [x, z, the highest, the layer to lay it at, the floor's layer round it] -- or [x, z, the highest]
# where no floor is round it (the build then takes away the pieces the cuts split off higher than that, hanging over the road).
def kept(key, y): return present(bits.get(key, 0), y) and (key[0], y, key[1]) not in cuts


def kept_top(key, surface):
    return next((y for y in range(L['maxY'], MINY - 1, -1) if top(y) <= surface + 1 and kept(key, y)), None)


cut_cols = {(c[0], c[2]) for c in cuts}
SKIRT_MOST = 5                                  # a trench's wall no more than this many layers over its bottom gets a skirt


def kept_above(key, y):
    """The lowest kept cell of a column over layer y."""
    return next((k for k in range(y + 1, L['maxY'] + 1) if kept(key, k)), None)


def highest_of(key):
    lo = min(r[0] for r in rooms[key])
    return max((y for y in range(MINY - 1, L['maxY'] + 1) if top(y) <= lo), default=MINY - 1)


floors, bottoms = [], {}
for key in sorted(col_points):
    lx, lz = key
    highest = highest_of(key)
    if highest < MINY: continue
    cx, cz = OX + SCALE * (lx + 0.5), OZ + SCALE * (lz + 0.5)
    surface = h[min(col_points[key], key=lambda i: (pts[i][0] - cx) ** 2 + (pts[i][1] - cz) ** 2)]
    tops = [kept_top((lx + dx, lz + dz), surface) for dx in (-1, 0, 1) for dz in (-1, 0, 1)
            if (dx or dz) and (lx + dx, lz + dz) not in cut_cols]
    tops = [t for t in tops if t is not None]
    if tops:
        floor = max(set(tops), key=lambda t: (tops.count(t), t))
        floors.append([lx, lz, highest, min(floor, highest), floor])
        if key in cut_cols: bottoms[key] = min(floor, highest)
        continue
    # (no floor round it at the road's height: where the road goes down through a floor standing higher than it on both sides -- the crypt
    # west room's stepped gallery, its steps up to four layers over the road -- a cut column's floor laid at the road's height under the
    # deck, the steps' walls beside it skirted down to it, below; over the void, nothing)
    walls = [kept_above((lx + dx, lz + dz), highest) for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1)) if (lx + dx, lz + dz) not in cut_cols]
    walls = [w for w in walls if w is not None and w <= highest + SKIRT_MOST]
    if key in cut_cols and walls:
        floors.append([lx, lz, highest, highest, min(walls)])
        bottoms[key] = highest
    else:
        floors.append([lx, lz, highest])
# (and a cut column with neither, among cut columns whose floors are laid -- under the deck where a ramp leaves a floor, all round it cut:
# the floor its neighbours' are laid at, the commonest, no higher than its own highest)
grew = True
while grew:
    grew = False
    for f in floors:
        if len(f) != 3 or (f[0], f[1]) not in cut_cols: continue
        near_ = [bottoms[(f[0] + dx, f[1] + dz)] for dx in (-1, 0, 1) for dz in (-1, 0, 1) if (dx or dz) and (f[0] + dx, f[1] + dz) in bottoms]
        near_ = [b_ for b_ in near_ if b_ <= f[2]]
        if not near_: continue
        lay = max(set(near_), key=lambda t: (near_.count(t), t))
        f.extend([lay, lay])
        bottoms[(f[0], f[1])] = lay
        grew = True
print('floors under the cuts', len(floors), f'({len(bottoms)} cut columns with a floor to lay)')
# ---- the skirts: under a kept floor (a step, a ledge) beside a cut column whose floor is laid lower, the void down to that floor filled
# (TempleIsland.Carve) -- a trench's wall from its floor up, where it showed the dark under the floor beside it. Each [x, z, from layer,
# to layer].
skirts = {}
for (lx, lz), b in sorted(bottoms.items()):
    for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        k = (lx + dx, lz + dz)
        if k in cut_cols: continue
        w = kept_above(k, b)
        if w is None or w - b - 1 < 1 or w > b + SKIRT_MOST: continue
        if any(kept(k, y) for y in range(b + 1, w)): continue
        y1 = min(w - 1, highest_of(k)) if k in col_points else w - 1
        if y1 <= b: continue
        skirts[k] = (min(b + 1, skirts.get(k, (99, 0))[0]), max(y1, skirts.get(k, (99, -99))[1]))
skirts = [[k[0], k[1], a, b_] for k, (a, b_) in sorted(skirts.items())]
print('skirts', len(skirts), 'columns,', sum(b_ - a + 1 for _, _, a, b_ in skirts), 'cells')


# ---- a picture of the route over the temple from above (PLOT=<png>: E:\dump\TEMP\temple\design\top.png's frame, a cell 4 pixels)
import os
if os.environ.get('PLOT'):
    from PIL import Image, ImageDraw
    im = Image.open(os.environ.get('TOP', r'E:\dump\TEMP\temple\design\top.png')).convert('RGB')
    dr = ImageDraw.Draw(im)
    def px(x, z): return (4 * x - 896, 4 * z - 1024)
    lo_h, hi_h = h.min(), h.max()
    for i in range(n):
        x, z = pts[i]; x2, z2 = pts[(i + 1) % n]
        t = (h[i] - lo_h) / (hi_h - lo_h)
        dr.line([px(x, z), px(x2, z2)], fill=(int(255 * t), int(80 + 100 * (1 - t)), int(255 * (1 - t))), width=4 * int(2 * HALF) // 2)
    for (lx, y, lz) in cuts:
        u, v = px(OX + SCALE * lx + 1.5, OZ + SCALE * lz + 1.5)
        dr.rectangle([u - 2, v - 2, u + 2, v + 2], outline=(255, 255, 255))
    for k in range(0, n, 100):
        dr.text(px(*pts[k]), str(k), fill=(255, 255, 0))
    im.resize((im.width // 2, im.height // 2)).save(os.environ['PLOT'])
    print('plot', os.environ['PLOT'])


# ---- Halloween (Terrain/Temple/TempleHalloween): the decor about the road and in the dark round it, the spooks flying over it
LANTERN, GIANT, GRAVE, CROSS, CANDLES, CAULDRON = range(6)
cut_columns = {(c[0], c[2]) for c in cuts}
lap_pts = pts                                   # (the lap as built, in its own order: distances from it)


def column_top(lx, lz):
    b = bits.get((lx, lz), 0)
    if not b: return None
    return max(y for y in range(MINY, L['maxY'] + 1) if (b >> (y - MINY)) & 1)


def flat_floor(cx, cz, r):
    """The layer of the floor round island cell (cx, cz), r layout cells round it all at one top and none of them cut; else None."""
    lx, lz = layout_of(cx, cz)
    tops = set()
    for dz in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if (lx + dx, lz + dz) in cut_columns: return None
            t = column_top(lx + dx, lz + dz)
            if t is None: return None
            tops.add(t)
    return tops.pop() if len(tops) == 1 else None


# how far each piece of decor standing on a floor reaches round its middle (cells: TempleHalloween's models)
REACH = {LANTERN: 1.25, GRAVE: 1.05, CROSS: 1.0, CANDLES: 0.5, CAULDRON: 1.25}


def footprint_on(x, z, layer, reach):
    """Every layout column under the square `reach` cells round (x, z) a floor at `layer`, none of them cut (the 2026-10-18 audit: a
    jack-o'-lantern by the west rooms stood part-way into a wall, the floor under its middle only looked at)."""
    cols_ = {layout_of(x + a * reach, z + b * reach) for a in (-1, 0, 1) for b in (-1, 0, 1)}
    return all(c not in cut_columns and column_top(*c) == layer for c in cols_)


def clear_of_road(x, z, margin):
    d = np.hypot(lap_pts[:, 0] - x, lap_pts[:, 1] - z) - half
    # (and of the slip lane beside the start straight; and of where the shortcut's car drives off the road)
    return float(d.min()) >= margin and slip_off(x, z) >= margin and secret_off(x, z) >= margin


def heading(i):
    a, b = pts[max(0, i - 2)], pts[min(n - 1, i + 2)]
    d = b - a
    return d / max(1e-9, np.linalg.norm(d))


def turn_to(fx, fz, tx, tz):
    """The decor's turn (0..4095) that has its front (+z) look from (fx, fz) towards (tx, tz)."""
    return int(round(math.atan2(tx - fx, tz - fz) * 4096 / (2 * math.pi))) & 4095


decor = []
cycle = [LANTERN, GRAVE, CANDLES, LANTERN, CROSS, CAULDRON, LANTERN, GRAVE, GRAVE, CANDLES]
k = 0
for i in range(10, n, int(18 / SP)):
    u = heading(i)
    side = 1 if (i // int(18 / SP)) % 2 == 0 else -1
    nrm = np.array([-u[1], u[0]]) * side
    placed = False
    kind = cycle[k % len(cycle)]
    for off in (half[i] + 3.2, half[i] + 4.6, half[i] + 6.5, half[i] + 8.5):
        x, z = pts[i] + nrm * off
        top_ = flat_floor(x, z, 1 if kind in (CAULDRON, CROSS) else 0)
        if top_ is not None and abs(top(top_) - (h[i] - DECK)) <= LAYER + 10 and clear_of_road(x, z, 2.4):
            # (a place for it; but where its footprint is on a wall or over a drop, the next one out -- none: this one left out, the
            # cycle of kinds going on as if it stood there)
            placed = True
            if not footprint_on(x, z, top_, REACH[kind]): continue
            decor.append([kind, round(float(x), 2), round(float(z), 2), float(top(top_)), turn_to(x, z, *pts[i])])
            if kind in (GRAVE, CROSS):
                # (a grave's candles in front of it, toward the road)
                cx_, cz_ = pts[i] + nrm * (off - 1.4) + u * 0.8
                if flat_floor(cx_, cz_, 0) == top_ and clear_of_road(cx_, cz_, 1.6) and footprint_on(cx_, cz_, top_, REACH[CANDLES]):
                    decor.append([CANDLES, round(float(cx_), 2), round(float(cz_), 2), float(top(top_)), turn_to(cx_, cz_, *pts[i])])
            break
    if placed: k += 1
    else:
        # (over the dark: a jack-o'-lantern floating beside the road, a little over it)
        x, z = pts[i] + nrm * (half[i] + 4.0)
        lx, lz = layout_of(x, z)
        if all(column_top(lx + a, lz + b) is None for a in (-1, 0, 1) for b in (-1, 0, 1)) and clear_of_road(x, z, 2.4):
            decor.append([LANTERN, round(float(x), 2), round(float(z), 2), round(float(h[i] + 250 + 500 * ((i // 36) % 3)), 1), turn_to(x, z, *pts[i])])
# (up each spiral's middle, a totem of giant jack-o'-lanterns looking out every way)
for (cx_, cz_, r_), lo_, hi_ in ((SPIRAL_W, CRYPT, FIRST), (SPIRAL_E, top(-5), FIRST)):
    for j, y in enumerate(np.arange(lo_ - 1500, hi_ + 1, 2400)):
        decor.append([GIANT, float(cx_), float(cz_), round(float(y), 1), (j * 1365) & 4095])
print('halloween decor', len(decor), {nm: sum(1 for d in decor if d[0] == kk) for kk, nm in enumerate(['lantern', 'giant', 'grave', 'cross', 'candles', 'cauldron'])})


# ---- skeletons (the user, 2026-10-17: "Let's make the temple track creepier with some skeletons"): LBA1's own skeleton, posed
# (TempleHalloween.Skeletons) -- lying by the road, slumped against the walls beside it, hanging in chains on those walls, and hanging on long
# chains in the dark beside the spirals -- seen from the road, never on it (decor drawn only, nothing to bump into)
SK_LYING, SK_SLUMPED, SK_HANGING, SK_CHAINED = range(6, 10)
SLUMP_REACH = 2.7                               # how far a slumped skeleton's feet reach out from the wall it sits against (cells)
HANG_LIFT = 900                                 # a skeleton hung on a wall: its feet this far over the floor
SK_MOST = {SK_LYING: 7, SK_SLUMPED: 9, SK_HANGING: 9, SK_CHAINED: 7}


def far_from_decor(x, z, d=2.6):
    return all((x - e[1]) ** 2 + (z - e[2]) ** 2 >= d * d for e in decor)


def wall_beside(i, side, most=9.0):
    """Out from the road at point i, to its `side` (1 its left, -1 its right): the floor the road lies on, all the way to the first wall --
    a column standing two layers or more over that floor -- within `most` cells of the road's middle; (cells out to the wall's face, the
    floor's layer, the way out) or None (the dark, a step, a doorway cut)."""
    u = heading(i); nrm = np.array([-u[1], u[0]]) * side
    floor_layer = None
    for off in np.arange(half[i] + 0.5, half[i] + most, 0.25):
        x, z = pts[i] + nrm * off
        lx, lz = layout_of(x, z)
        if (lx, lz) in cut_columns: return None
        t = column_top(lx, lz)
        if t is None: return None
        if floor_layer is None:
            if abs(top(t) - (h[i] - DECK)) > LAYER + 10: return None
            floor_layer = t; continue
        if t >= floor_layer + 2: return off, floor_layer, nrm
        if t != floor_layer: return None
    return None


skeletons = []
for c, i in enumerate(range(19, n, int(15 / SP))):
    side = 1 if c % 2 else -1
    count = lambda kind: sum(1 for s_ in skeletons if s_[0] == kind)
    placed = False
    # (none by the giant web's doorway, its columns at layout x 111: there it is the web the car comes up to -- the cuts keep the doorway's
    # floor whole since 2026-10-18, walls to hang one on)
    if math.hypot(pts[i][0] - (OX + SCALE * 111 + 1.5), pts[i][1] - CORR_Z) < 10: continue
    for sd in (side, -side):
        w = wall_beside(i, sd)
        if w is None: continue
        off, layer, nrm = w
        # (its back to the wall, facing the road: slumped where its feet stay clear of the road's edge, else hung on the wall)
        at = off - 0.35
        kind = SK_SLUMPED if at - SLUMP_REACH >= half[i] + 0.4 and count(SK_SLUMPED) < SK_MOST[SK_SLUMPED] else SK_HANGING
        if kind == SK_HANGING and (count(SK_HANGING) >= SK_MOST[SK_HANGING] or c % 2): continue
        x, z = pts[i] + nrm * at
        if not far_from_decor(x, z) or any((x - s_[1]) ** 2 + (z - s_[2]) ** 2 < 36 for s_ in skeletons): continue
        y = top(layer) + (HANG_LIFT if kind == SK_HANGING else 0)
        skeletons.append([kind, round(float(x), 2), round(float(z), 2), float(y), turn_to(x, z, *(pts[i]))])
        placed = True
        break
    if placed or c % 3: continue
    # (no wall near: lying on the floor beside the road, or hung in the dark over the void beside it)
    u = heading(i); nrm = np.array([-u[1], u[0]]) * side
    for off in (half[i] + 2.6, half[i] + 3.6, half[i] + 5.0):
        x, z = pts[i] + nrm * off
        t = flat_floor(x, z, 1)
        if count(SK_LYING) < SK_MOST[SK_LYING] and t is not None and abs(top(t) - (h[i] - DECK)) <= LAYER + 10 and clear_of_road(x, z, 2.2) and far_from_decor(x, z):
            skeletons.append([SK_LYING, round(float(x), 2), round(float(z), 2), float(top(t)), (turn_to(x, z, *pts[i]) + 1024 * (1 if c % 2 else -1)) & 4095])
            placed = True
            break
    if placed: continue
    x, z = pts[i] + nrm * (half[i] + 3.2)
    lx, lz = layout_of(x, z)
    if count(SK_CHAINED) < SK_MOST[SK_CHAINED] and all(column_top(lx + a, lz + b) is None for a in (-1, 0, 1) for b in (-1, 0, 1)) \
            and clear_of_road(x, z, 2.4) and far_from_decor(x, z, 3.0):
        skeletons.append([SK_CHAINED, round(float(x), 2), round(float(z), 2), round(float(h[i] + 700), 1), turn_to(x, z, *pts[i])])
decor += skeletons
print('skeletons', len(skeletons), {nm: sum(1 for d in skeletons if d[0] == kk) for kk, nm in ((SK_LYING, 'lying'), (SK_SLUMPED, 'slumped'), (SK_HANGING, 'hanging'), (SK_CHAINED, 'chained'))})


def circle(cx, cz, r, m=24, phase=0.0):
    return [[round(float(cx + r * math.cos(phase + 2 * math.pi * j / m)), 2), round(float(cz + r * math.sin(phase + 2 * math.pi * j / m)), 2)] for j in range(m)]


def across(i, reach):
    """A way across the road at point i, `reach` cells either side of it."""
    u = heading(i); nrm = np.array([-u[1], u[0]])
    a, b = pts[i] - nrm * reach, pts[i] + nrm * reach
    return [[round(float(a[0]), 2), round(float(a[1]), 2)], [round(float(b[0]), 2), round(float(b[1]), 2)]]


# (2026-10-18, the port's audit: the ghosts drifted across the road 1,300-1,900 over it, the chase camera's eye 2,100 over it went through
# them and the screen filled with their sheets; witches crossed ramps at the cars' height and passed through the deck) -- every spook's
# height is kept clear of the road wherever its way comes over it (within its half width and two cells): its body's lowest (bobbing) at
# least SPOOK_CLEAR over the road there, and over the road the camera comes from (its eye 2,100 over the road 9 cells behind the car: the
# 20 points before, up a ramp higher); or, a witch's, its highest SPOOK_UNDER under the road's surface (the deck under it) -- the
# height asked for, or the nearest that is
# (the flying rat -- LBA2's own, BODY.HQR 36, twice its size: TempleHalloween.Creatures -- reaches 850 under its origin flapping, 660 over)
SPOOK_BODY = {'witch': (390, 1420), 'ghost': (0, 1680), 'rat': (850, 660)}     # its body under and over its height (the bodies')
SPOOK_BOB = 260                                                                  # RACEMOD.CPP RACE_SPOOK_BOB
SPOOK_CLEAR, SPOOK_UNDER = 2500, 1000


def way_points(way, loop):
    w = [np.array(p, float) for p in way]
    segs = list(zip(w, w[1:] + ([w[0]] if loop else [])))
    out = []
    for a, b in segs:
        m = max(1, int(np.linalg.norm(b - a) / 0.5))
        out.extend(a + (b - a) * k / m for k in range(m + 1))
    return out


def spook_height(kind, way, loop, y0, under=False):
    over = set()
    for q in way_points(way, loop):
        d = np.hypot(pts[:, 0] - q[0], pts[:, 1] - q[1])
        over.update(int(i) for i in np.nonzero(d < half + 2)[0])
    if not over: return round(float(y0), 1)
    low, tall = SPOOK_BODY[kind]
    road = [(h[i], max(h[(i - k) % n] for k in range(21))) for i in sorted(over)]
    def clear(y):
        return all(y - low - SPOOK_BOB >= cam + SPOOK_CLEAR or (under and y + tall + SPOOK_BOB <= r - SPOOK_UNDER) for r, cam in road)
    y = next(y0 + s * d for d in range(0, 30000, 25) for s in (1, -1) if clear(y0 + s * d))
    return round(float(y), 1)


spooks = []
# witches round the spirals, outside the road, a third and two thirds of the way up, throwing fire balls at the cars below them
for (cx_, cz_, r_), lo_, hi_ in ((SPIRAL_W, CRYPT, FIRST), (SPIRAL_E, top(-5), FIRST)):
    for frac, ph in ((0.42, 0.0), (0.85, math.pi)):
        way = circle(cx_, cz_, r_ + 13, 24, ph)
        spooks.append({'kind': 'witch', 'flags': 3, 'y': spook_height('witch', way, True, lo_ + (hi_ - lo_) * frac + 2500, under=True), 'way': way})
# witches over the halls: along the long corridor and across the lower hall (over its floor, where the road is down on it, not the ramps
# either side), the crypt's rooms
for p0, p1, hy in (((W_END + 30, CORR_Z), (E_END - 20, CORR_Z), FIRST + 3200), ((360, 300), (392, 328), LOWER + 3400),
                   ((380, 560), (440, 600), CRYPT + 3400), ((470, 585), (530, 630), CRYPT + 3400)):
    way = [list(map(float, p0)), list(map(float, p1))]
    spooks.append({'kind': 'witch', 'flags': 1, 'y': spook_height('witch', way, False, hy), 'way': way})
# ghosts drifting across the road, over the cars and the camera (the crypt's west room's past the start line and the flag marshal's places
# by it: across at the line, it drifted through her -- RACEMOD.CPP marshal=)
ghost_at = [nearest(p) for p in ((420, CORR_Z), (500, CORR_Z), (W_END, 360), (LEG_X, 340), (262, 372), (417, CRYPT_Z), (470, 618), (500, 560), (540, SPIRAL_E[1] + SPIRAL_E[2]))]
for j, i in enumerate(ghost_at):
    way = across(i, half[i] + 10)
    spooks.append({'kind': 'ghost', 'flags': 0, 'y': spook_height('ghost', way, False, h[i] + 2800 + 300 * (j % 3)), 'way': way})
# flying rats wheeling high over the road (bats until 2026-10-18 -- the user: "The game contains an actor called '36: The flying rat' ...
# let's replace the current bats we have with the ones from the game": the same ways, each kept clear of the camera as the bats were, the
# rat's bigger body taken into account; flapping its own wings, RACEMOD.CPP spook= flags 4)
bat_at = [nearest(p) for p in ((450, CORR_Z), (360, 320), (270, 330), (400, 590), (500, 600), (520, 520))]
for j, i in enumerate(bat_at):
    way = circle(pts[i][0], pts[i][1], 6 + 2 * (j % 3), 12, j)
    spooks.append({'kind': 'rat', 'flags': 2, 'y': spook_height('rat', way, True, h[i] + 2600 + 400 * (j % 2)), 'way': way})
for (cx_, cz_, r_), lo_, hi_ in ((SPIRAL_W, CRYPT, FIRST), (SPIRAL_E, top(-5), FIRST)):
    for frac in (0.2, 0.62):
        spooks.append({'kind': 'rat', 'flags': 2, 'y': round(float(lo_ + (hi_ - lo_) * frac + 1800), 1), 'way': circle(cx_, cz_, r_ * 0.5, 12, frac * 9)})
print('spooks', len(spooks), {kk: sum(1 for s_ in spooks if s_['kind'] == kk) for kk in ('witch', 'ghost', 'rat')})
assert len(spooks) <= 48


# ---- the scares (the user, 2026-10-17: "also do visual scares, perhaps a witch dive bombing at the player and pulling up at the last second
# to avoid a collision"): the places the witches dive at Twinsen's car from (RACEMOD.CPP dive=: as the car comes 28 cells from one along the
# lap, now and then a witch sets off from high over it and dives along the road at the car) -- where nothing stands over the road, from a
# few cells past the place back to where she climbs away behind the car: the long corridor, the straight over the dark, the spirals, the
# crypt's straight -- each [x, z, the road's height there]
DIVE_SPAN = (-24, 3)                            # cells along the lap she flies over, from the place (the car comes from behind it)
DIVE_ROOM = 9000                                # nothing over the road this high over it


def open_over(i):
    """Nothing of the temple over the road at point i, its width and a cell either side, up to DIVE_ROOM over it. (The road's width as it
    was before it was widened, half0: her way is along its middle -- the long corridor's walls, 6 cells from it, kept its dives out.)"""
    u = heading(i); nrm = np.array([-u[1], u[0]])
    for off in np.arange(-half0[i] - 1, half0[i] + 1.01, 0.5):
        x, z = pts[i] + nrm * off
        b = bits.get(layout_of(x, z), 0)
        if not b: continue
        for y in range(MINY, L['maxY'] + 1):
            if (b >> (y - MINY)) & 1 and top(y - 1) >= h[i] - DECK + 20 and top(y - 1) < h[i] + DIVE_ROOM: return False
    return True


def dive_clear(i):
    return all(open_over((i + j) % n) for j in range(int(DIVE_SPAN[0] / SP), int(DIVE_SPAN[1] / SP) + 1, 2))


# ... and the start gantry (its beam over the road at the line) no nearer than GANTRY_CLEAR cells to where she flies (the 2026-10-18 audit:
# the crypt straight's witch, her place 1 cell past the line, dived through the beam)
GANTRY_CLEAR = 10
i_line = nearest((410, CRYPT_Z), i_down_out)


def gantry_clear(i):
    ahead = S[i_line] - S[i]
    ahead = (ahead + LAP / 2) % LAP - LAP / 2
    return not (DIVE_SPAN[0] - GANTRY_CLEAR <= ahead <= DIVE_SPAN[1] + GANTRY_CLEAR)


dives = []
for want in [nearest((412, CORR_Z)), nearest((505, CORR_Z)), nearest((276, 520), i_edge), o_down + int(0.62 * (o2 - o_down)),
             nearest((392, CRYPT_Z), i_down_out), o_up + int(0.55 * (o3 - o_up))]:
    # (the nearest place to the one wanted, along the lap, with the room over the road)
    i = next((want + s * j for j in range(0, 80, 2) for s in (1, -1) if dive_clear((want + s * j) % n) and gantry_clear((want + s * j) % n)), None)
    if i is None: print('dive place near point', want, 'has no room over the road'); continue
    i %= n
    dives.append([round(float(pts[i][0]), 2), round(float(pts[i][1]), 2), round(float(h[i]), 1)])
print('dives', len(dives), dives)

# ... and what jumps up out of the floor beside the road as the car comes by (scare=): skeletons reaching out at it, and ghosts -- each
# [0 skeleton | 1 ghost, x, z, the floor's height, the way it faces (at the road)]
scares = []
for j, i in enumerate(range(60, n, int(48 / SP))):
    u = heading(i)
    # (and, where the wider road took the floor those had, further out)
    for sd, off in ((1, 2.3), (-1, 2.3), (1, 3.1), (-1, 3.1), (1, 4.0), (-1, 4.0), (1, 5.0), (-1, 5.0)):
        nrm = np.array([-u[1], u[0]]) * sd
        x, z = pts[i] + nrm * (half[i] + off)
        t = flat_floor(x, z, 1)
        if t is None or abs(top(t) - (h[i] - DECK)) > LAYER + 10 or not clear_of_road(x, z, 1.9) or not far_from_decor(x, z, 2.2): continue
        scares.append([0 if j % 3 != 2 else 1, round(float(x), 2), round(float(z), 2), float(top(t)), turn_to(x, z, *pts[i])])
        break
print('scares', len(scares), {nm: sum(1 for s_ in scares if s_[0] == kk) for kk, nm in enumerate(('skeleton', 'ghost'))})


# ---- the giant web (the user, 2026-10-17: "Can we also improve the temple track add in some cobwebs, perhaps a giant cobweb we have to
# drive through"): across the long corridor's east doorway, between the two columns either side of the road there (LBA1's, layout x 111, z
# 67 and 72: their faces 6 cells either side of the road's middle) -- the doorway the lap comes into from the dark at the top of the east
# spiral, 60 cells of straight leading up to it: seen coming across the dark, and driven straight through (RACEMOD.CPP web=: the cars tear
# it) -- [x, z, the road's height there, the lap's way through it x and z, the columns' faces either side of the road's middle (cells)]
WEB_X = OX + SCALE * 111 + 1.5                  # the columns' middles
WEB_HALF = 6.0
i_web = nearest((WEB_X, CORR_Z))
u_web = heading(i_web)
giant_webs = [[round(float(WEB_X), 2), float(CORR_Z), round(float(h[i_web]), 1), round(float(u_web[0]), 3), round(float(u_web[1]), 3), WEB_HALF]]
print('giant web', giant_webs)

# ... and cobwebs about the lap (TempleHalloween.Cobwebs: decor drawn only, off the road -- never over it -- where the camera following the
# car sees them), each [kind, x, z, y, turn (, a drape's span in cells; which of three a corner's or a wall's is)]:
#   - hanging from the tops of two walls across the corner they make (y the lower wall's top; a big one where both walls run on for two
#     layout cells and rise high enough): the rooms' and corridors' corners beside the road, their walls WEB_RISE layers or more over the
#     floor the road lies on, the corner facing the road with nothing between;
#   - across an alcove or a door gap off the road (y its floor): a layout cell of that floor between two such walls, open towards the road;
#   - between two of LBA1's columns beside the road (block 24, `ScriptRoundTrip templeblocks`) with nothing between them (y the top
#     thread's height): WEB_COLUMNS;
#   - flat on a wall, as LBA1's own cobwebs are (y the wall's top): either side of each doorway the road goes through, on the wall's face
#     the car comes up to; and on the walls beside the road where none of the others is near;
#   - a spider hanging on its thread in front of every third corner's and every third wall's (y its middle).
# No two nearer than WEB_APART cells, nor nearer the giant web than WEB_CLEAR.
CW_CORNER, CW_CORNER_BIG, CW_SHEET, CW_DRAPE, CW_SPIDER, CW_WALL = range(10, 16)
WEB_RISE = 3                                    # layers over the floor, at the least
WEB_APART = 7.0
WEB_CLEAR = 14.0
WEB_EDGE = 1.0                                  # cells from the road's edge to a web's nearest point, at the least ...
WEB_FAR = 14.0                                  # ... and from it to a corner, at the most
CORNER_REACH = {CW_CORNER: 1700 / 512, CW_CORNER_BIG: 2700 / 512}      # how far along each wall a corner's web reaches (cells)
WALL_HALF = 1500 / 512                          # how far either side of its middle a wall's reaches (cells)
WALL_EVERY = 12.0                               # a wall's beside the road about every this many cells of the lap
WEB_COLUMNS = [((107, 67), (111, 67)), ((81, 123), (81, 127)), ((94, 107), (98, 107))]
TURN_INTO = {(1, 1): 0, (1, -1): 1024, (-1, -1): 2048, (-1, 1): 3072}  # the turn that has a corner's web face into the room that way


def rise(lx, lz, floor_layer):
    t = column_top(lx, lz)
    return -99 if t is None or (lx, lz) in cut_columns else t - floor_layer


def in_sight(x0, z0, x1, z1, floor_layer):
    """Nothing rising two layers or more over floor_layer between (x0, z0) and (x1, z1) (island cells), the road's doorways apart."""
    steps = int(math.hypot(x1 - x0, z1 - z0) / 0.5) + 1
    return all(rise(*layout_of(x0 + (x1 - x0) * k / steps, z0 + (z1 - z0) * k / steps), floor_layer) < 2 for k in range(1, steps))


def road_off(x, z):
    """The lap's point nearest (x, z), and how far (x, z) is from the road's edge there (cells)."""
    d = np.hypot(lap_pts[:, 0] - x, lap_pts[:, 1] - z)
    i = int(d.argmin())
    return i, float(d[i] - half[i])


webs = []
for (lx, lz) in sorted(bits):
    t = column_top(lx, lz)
    if t is None or (lx, lz) in cut_columns: continue
    cx, cz = OX + SCALE * (lx + 0.5), OZ + SCALE * (lz + 0.5)
    i, off = road_off(cx, cz)
    if off > WEB_FAR + 3 or abs(top(t) - (h[i] - DECK)) > LAYER + 10: continue
    for sx in (-1, 1):
        for sz in (-1, 1):
            walls = [rise(lx + sx, lz, t), rise(lx, lz + sz, t), rise(lx + sx, lz + sz, t)]
            if min(walls) < WEB_RISE: continue
            vx, vz = OX + SCALE * (lx + (sx > 0)), OZ + SCALE * (lz + (sz > 0))
            i2, off2 = road_off(vx, vz)
            into = np.array([-sx, -sz]) / math.sqrt(2)
            to_road = (lap_pts[i2] - np.array([vx, vz])) / max(1e-6, off2 + half[i2])
            if off2 > WEB_FAR or float(into @ to_road) < 0.25 or not in_sight(vx - sx, vz - sz, *lap_pts[i2], t): continue
            low = min(walls[0], walls[1])
            big = rise(lx + sx, lz - sz, t) >= WEB_RISE and rise(lx - sx, lz + sz, t) >= WEB_RISE and low >= 5
            kind = CW_CORNER_BIG if big and off2 >= CORNER_REACH[CW_CORNER_BIG] + WEB_EDGE else CW_CORNER
            if off2 < CORNER_REACH[kind] + WEB_EDGE: continue
            webs.append((i2, [kind, float(vx), float(vz), float(top(t + low) - 40), TURN_INTO[(-sx, -sz)]], (sx, sz)))
    for ux, uz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        vx_, vz_ = -uz, ux
        if min(rise(lx + vx_, lz + vz_, t), rise(lx - vx_, lz - vz_, t)) < WEB_RISE or rise(lx - ux, lz - uz, t) != 0: continue
        back = rise(lx + ux, lz + uz, t)
        if back < WEB_RISE and (back != 0 or ux < 0 or uz < 0): continue      # (an alcove; or a door gap, once)
        mx, mz = cx - ux * (1.2 if back > 0 else 0), cz - uz * (1.2 if back > 0 else 0)
        i2, off2 = road_off(mx, mz)
        to_road = (lap_pts[i2] - np.array([mx, mz])) / max(1e-6, off2 + half[i2])
        if off2 < WEB_EDGE + 1.5 or off2 > WEB_FAR or (back > 0 and float(np.array([-ux, -uz]) @ to_road) < 0.25): continue
        if not in_sight(mx - ux * 0.6, mz - uz * 0.6, *lap_pts[i2], t): continue
        webs.append((i2, [CW_SHEET, round(float(mx), 2), round(float(mz), 2), float(top(t)), turn_to(0, 0, ux, uz)], None))
for (ax_, az_), (bx_, bz_) in WEB_COLUMNS:
    x0_, z0_ = OX + SCALE * (ax_ + 0.5), OZ + SCALE * (az_ + 0.5)
    x1_, z1_ = OX + SCALE * (bx_ + 0.5), OZ + SCALE * (bz_ + 0.5)
    mx, mz = (x0_ + x1_) / 2, (z0_ + z1_) / 2
    i2, off2 = road_off(mx, mz)
    span = math.hypot(x1_ - x0_, z1_ - z0_)
    # (the top thread at the columns' capitals: 6.5 of their 8 layers up from the floor beside them)
    floor_layer = min(column_top(*c) for c in ((ax_ + (bz_ - az_) // 4, az_ + (bx_ - ax_) // 4), (ax_ - (bz_ - az_) // 4, az_ - (bx_ - ax_) // 4)) if column_top(*c) is not None)
    webs.append((i2, [CW_DRAPE, round(float(mx), 2), round(float(mz), 2), float(top(floor_layer) + 6.5 * LAYER), turn_to(0, 0, -(z1_ - z0_), x1_ - x0_), round(span, 1)], None))
# (the picks: along the lap, none too near another or the giant web; a spider in front of every third corner's)
cobwebs = []


def by_skeleton(x, z):
    """A skeleton sitting or hanging against a wall within SKELETON_CLEAR cells (the 2026-10-18 audit: corner webs drawn through the crypt's
    slumped skeletons)."""
    return any(d_[0] in (SK_HANGING, SK_SLUMPED) and math.hypot(x - d_[1], z - d_[2]) < SKELETON_CLEAR for d_ in decor)


SKELETON_CLEAR = 4.0
WEB_SECRET = WEB_EDGE + 3.0                     # (and none where the shortcut's car drives off the road: its middle this far from there)
for i2, w, corner in sorted(webs, key=lambda e: (e[0], e[1][0])):
    if any(math.hypot(w[1] - c[1], w[2] - c[2]) < WEB_APART for c in cobwebs) or math.hypot(w[1] - WEB_X, w[2] - CORR_Z) < WEB_CLEAR: continue
    if by_skeleton(w[1], w[2]) or secret_off(w[1], w[2]) < WEB_SECRET: continue
    # (a corner's one of three, in turn)
    if corner: w = w + [sum(1 for c in cobwebs if c[0] in (CW_CORNER, CW_CORNER_BIG)) % 3]
    cobwebs.append(w)
    if corner and sum(1 for c in cobwebs if c[0] in (CW_CORNER, CW_CORNER_BIG)) % 3 == 1:
        # (in front of the web's middle, its back to the room)
        sx, sz = corner
        reach = CORNER_REACH[w[0]]
        cobwebs.append([CW_SPIDER, round(w[1] - sx * reach * 0.6, 2), round(w[2] - sz * reach * 0.6, 2), w[3] - 1750, turn_to(0, 0, -sx, -sz)])
# (then webs flat on the walls, where none of those is near, each on a wall rising 3 to 6 layers over the floor -- not the corridor's
# frieze -- along its whole width: first either side of each doorway the road goes through, on the wall's face the car comes up to, clear
# of the doorway; then on the walls beside the road about every WALL_EVERY cells of the lap, not where a skeleton hangs)
for i in range(0, n, 2):
    lx, lz = layout_of(*pts[i])
    if (lx, lz) in cut_columns or (layout_of(*pts[(i + 4) % n]) not in cut_columns and layout_of(*pts[(i + 6) % n]) not in cut_columns): continue
    u = heading(i); nrm_ = np.array([-u[1], u[0]])
    # (the face: the first cut cell's edge ahead along the road)
    face = next((a for a in np.arange(0.25, 4.0, 0.25) if layout_of(*(pts[i] + u * a)) in cut_columns), None)
    floor_layer = column_top(lx, lz)
    if face is None or floor_layer is None: continue
    for sd in (1, -1):
        across_ = half[i] + CUT + WALL_HALF + 0.4
        x, z = pts[i] + u * (face - 0.3) + nrm_ * sd * across_
        rises = [rise(*layout_of(*(pts[i] + u * (face + 0.5) + nrm_ * sd * (across_ + s_))), floor_layer) for s_ in (-WALL_HALF, 0, WALL_HALF)]
        if min(rises) < 3 or max(rises) > 6: continue
        if any(math.hypot(x - c[1], z - c[2]) < WEB_APART for c in cobwebs) or math.hypot(x - WEB_X, z - CORR_Z) < WEB_CLEAR or by_skeleton(x, z) or secret_off(x, z) < WEB_SECRET: continue
        cobwebs.append([CW_WALL, round(float(x), 2), round(float(z), 2), float(top(floor_layer + min(rises)) - 40), turn_to(0, 0, -u[0], -u[1]),
                        sum(1 for c in cobwebs if c[0] == CW_WALL) % 3])
last_wall = -1e9
for i in range(0, n, 2):
    if S[i] - last_wall < WALL_EVERY: continue
    for sd in ((1, -1) if (i // 40) % 2 else (-1, 1)):
        found = wall_beside(i, sd, most=8.0)
        if found is None: continue
        off, layer, nrm = found
        x, z = pts[i] + nrm * (off - 0.3)
        tang = heading(i)
        rises = [rise(*layout_of(*(pts[i] + nrm * (off + 0.4) + tang * s_)), layer) for s_ in (-WALL_HALF, 0, WALL_HALF)]
        if min(rises) < 3 or max(rises) > 6: continue
        if any(math.hypot(x - c[1], z - c[2]) < WEB_APART for c in cobwebs) or math.hypot(x - WEB_X, z - CORR_Z) < WEB_CLEAR: continue
        if by_skeleton(x, z) or secret_off(x, z) < WEB_SECRET: continue
        cobwebs.append([CW_WALL, round(float(x), 2), round(float(z), 2), float(top(layer + min(rises)) - 40), turn_to(x, z, *pts[i]),
                        sum(1 for c in cobwebs if c[0] == CW_WALL) % 3])
        # (a spider hanging in front of every third one's middle)
        if sum(1 for c in cobwebs if c[0] == CW_WALL) % 3 == 2:
            sx_, sz_ = pts[i] + nrm * (off - 0.75)
            cobwebs.append([CW_SPIDER, round(float(sx_), 2), round(float(sz_), 2), cobwebs[-1][3] - 1750, turn_to(sx_, sz_, *pts[i])])
        last_wall = S[i]
        break
decor += cobwebs
print('cobwebs', len(cobwebs), {nm: sum(1 for c in cobwebs if c[0] == kk) for kk, nm in ((CW_CORNER, 'corner'), (CW_CORNER_BIG, 'big corner'), (CW_SHEET, 'sheet'), (CW_WALL, 'wall'),
                                                                                     (CW_DRAPE, 'drape'), (CW_SPIDER, 'spider'))})


# ---- LBA2's own skeletons (the user, 2026-10-18: "add more of the games in-built skeletons to our temple track too"): the rabbibunny
# skeleton of the Tralu's cave (BODY.HQR 35; TempleHalloween.Creatures), decor posed by its own animations -- sitting against the walls
# beside the road (its legs out towards it), standing guard and crouching on the floors beside it looking at it, lying on its back -- on the
# floors the road runs over, in the rooms it passes, after all the other decor and clear of it (and of the scares, which jump up out of the
# floor): never on the road or the slip lane, no part of it over a wall or a drop (footprint_on), each its own place along the lap
RB_LYING, RB_SITTING, RB_STANDING, RB_CROUCHING = range(16, 20)
RB_REACH = {RB_LYING: 1.4, RB_STANDING: 0.8, RB_CROUCHING: 1.1}         # how far each reaches round its middle (cells)
RB_SIT_REACH = 1080 / 512                       # how far a sitting one's feet reach out from the wall behind it (cells)
RB_MOST = {RB_LYING: 5, RB_SITTING: 5, RB_STANDING: 5, RB_CROUCHING: 4}
RB_CYCLE = [RB_SITTING, RB_STANDING, RB_LYING, RB_CROUCHING, RB_SITTING, RB_STANDING, RB_LYING]
RB_APART = 3.0                                  # cells from any other decor's middle, at the least
RB_SCARE_APART = 4.0                            # ... and from a scare's place


def rb_clear(x, z, d=RB_APART):
    return far_from_decor(x, z, d) and all(math.hypot(x - s_[1], z - s_[2]) >= RB_SCARE_APART for s_ in scares) \
        and math.hypot(x - WEB_X, z - CORR_Z) >= WEB_CLEAR


rabbis = []
for c, i in enumerate(range(37, n, int(14 / SP))):
    want = RB_CYCLE[c % len(RB_CYCLE)]
    count = lambda kind: sum(1 for r_ in rabbis if r_[0] == kind)
    if any(math.hypot(*(pts[i] - np.array(r_[1:3]))) < 9 for r_ in rabbis): continue
    side = 1 if c % 2 else -1
    placed = None
    for kind in [want] + [k_ for k_ in (RB_SITTING, RB_STANDING, RB_CROUCHING, RB_LYING) if k_ != want]:
        # (each kind's share spread along the lap: no more by point i than its share of the lap so far, and one)
        if count(kind) >= min(RB_MOST[kind], RB_MOST[kind] * i / n + 1): continue
        for sd in (side, -side):
            u = heading(i); nrm = np.array([-u[1], u[0]]) * sd
            if kind == RB_SITTING:
                w = wall_beside(i, sd)
                if w is None: continue
                off, layer, nrm = w
                at = off - 0.3
                # (its feet clear of the road's edge, the floor under its legs whole)
                if at - RB_SIT_REACH < half[i] + 0.6: continue
                x, z = pts[i] + nrm * at
                lx_, lz_ = pts[i] + nrm * (at - RB_SIT_REACH / 2)
                if not footprint_on(lx_, lz_, layer, 0.6) or not rb_clear(x, z) or not clear_of_road(lx_, lz_, 1.2): continue
                placed = [kind, round(float(x), 2), round(float(z), 2), float(top(layer)), turn_to(x, z, *pts[i])]
                break
            for off in (half[i] + 2.4, half[i] + 3.2, half[i] + 4.2, half[i] + 5.5):
                x, z = pts[i] + nrm * off
                t = flat_floor(x, z, 1)
                reach = RB_REACH[kind]
                if t is None or abs(top(t) - (h[i] - DECK)) > LAYER + 10 or not clear_of_road(x, z, reach + 0.6) \
                        or not footprint_on(x, z, t, reach) or not rb_clear(x, z): continue
                # (lying: along the road, its head the way the lap comes from, now and then the other way; the others looking at the road)
                turn = (turn_to(0, 0, *u) + (2048 if c % 3 == 0 else 0)) & 4095 if kind == RB_LYING else turn_to(x, z, *pts[i])
                placed = [kind, round(float(x), 2), round(float(z), 2), float(top(t)), turn]
                break
            if placed: break
        if placed: break
    if placed: rabbis.append(placed)
decor += rabbis
print('rabbibunny skeletons', len(rabbis), {nm: sum(1 for d in rabbis if d[0] == kk) for kk, nm in ((RB_LYING, 'lying'), (RB_SITTING, 'sitting'), (RB_STANDING, 'standing'),
                                                                                                (RB_CROUCHING, 'crouching'))})

# ... and a few of the game's own by the road, drawn by the race-track mode (spook= flags 8: standing at its way's first point, facing its
# second): fallen in a heap of bones until Twinsen's car comes near, then getting up and swiping at the road, and falling back down -- on
# the floor beside the road, far enough out that its arms never reach over the road's edge, away from the scares and the other decor
RISER_AT = [(0.12, 1), (0.47, -1), (0.8, 1)]     # where along the lap (a share of it from the start's straight) and which side first
risers = []
for frac, side in RISER_AT:
    base = int(frac * n)
    spot = None
    for j in range(0, 300, 2):
        for s_ in (1, -1):
            i = (base + s_ * j) % n
            u = heading(i)
            for sd in (side, -side):
                nrm = np.array([-u[1], u[0]]) * sd
                for off in (half[i] + 2.8, half[i] + 3.6, half[i] + 4.4):
                    x, z = pts[i] + nrm * off
                    t = flat_floor(x, z, 1)
                    if t is None or abs(top(t) - (h[i] - DECK)) > LAYER + 10 or not clear_of_road(x, z, 2.6) or not footprint_on(x, z, t, 1.3) \
                            or not rb_clear(x, z, 3.5) or any(math.hypot(x - r_[1], z - r_[2]) < 3.5 for r_ in rabbis): continue
                    # (in sight of the road beside it and of the road 8 and 15 cells before it: seen getting up as the car comes)
                    if not all(in_sight(x, z, *pts[(i - k_) % n], t) for k_ in (0, 16, 30)): continue
                    spot = (i, x, z, t)
                    break
                if spot: break
            if spot: break
        if spot: break
    if spot is None: print('no place for a riser near', frac); continue
    i, x, z, t = spot
    risers.append({'kind': 'riser', 'flags': 8, 'y': float(top(t)), 'way': [[round(float(x), 2), round(float(z), 2)], [round(float(pts[i][0]), 2), round(float(pts[i][1]), 2)]]})
spooks += risers
print('risers', len(risers), [r_['way'][0] for r_ in risers])
assert len(spooks) <= 48


# ---- the start: on the straight through the crypt's west room, heading east (the grid behind it, in the room)
i_start = nearest((410, CRYPT_Z), i_down_out)
print('start', i_start, tuple(np.round(pts[i_start], 1)), 'height', round(h[i_start]))
# ... the grid in two columns with a lane between them (the user, 2026-10-18: "let's have her start the race from the grid between the
# cars, with cars going either side of her"): the flag marshal stands in the road's middle ahead of the front row (Temple.TempleMarshal),
# the cars GRID_SIDE either side of it -- 2.75 cells: a gap of 3 cells between the columns' car boxes (1.24 cells round a car's middle), and
# a car's middle half a cell inside its rail (1.25 inside the 4.5-cell road's edge) -- pole GRID_FIRST behind the line, its nose (1.74
# cells ahead of its middle) a cell and a quarter behind her (RaceTrackScenes.GridSpot; the usual: 3 back, 1.75 across)
GRID_FIRST, GRID_SIDE = 5.0, 2.75
# ---- the pumpkin rows (RaceTrackScenes.MushroomSpots, a row every ~8 s of driving): each pumpkin of a row at least this far from its
# cube's edges (the 2026-10-18 audit: the row on the ramp down into the lower hall stood across x 384, the ramp running along that cube
# edge, so the car in one cube's scene saw only the two pumpkins in it)
ITEM_CUBE_MARGIN = 3.0
# ---- where the opponents wait while the player qualifies: in the slip lane, along its middle, heading the way the lap runs, on its deck
# (the road's height there) -- in the start's cube (the build writes no other cube's) and clear of the lane's edges and ends
slip_mid = (SLIP_FAR + SLIP_NEAR) / 2
pit_spots = []
for px_ in SLIP_SPOTS:
    i = nearest((px_, CRYPT_Z + slip_mid), i_down_out)
    k = [j for j in slip if abs(pts[j][0] - px_) <= 1.2]
    ok = len(k) > 0 and all(slip_lo[j] <= slip_mid - 1.2 and slip_hi[j] >= slip_mid + 1.2 for j in k) and CRYPT_Z + slip_mid - 1.2 >= 576
    print('pit spot', px_, CRYPT_Z + slip_mid, 'in the slip lane' if ok else 'NOT IN THE SLIP LANE')
    pit_spots.append([float(px_), float(CRYPT_Z + slip_mid), 1.0, 0.0, float(h[i])])

# ---- the floor beside the ground stretch (the plan's raisedFloors: [from, to] cells across each point, the builder's Across -- the left of
# the way the lap runs -- as raisedStrips; the race-track mode's floor there, never drawn) and its sides with no rail (raisedFlush: [the
# -Across side, the +Across side], 1 no rail -- a flat curb). Out from each side of the road, the floor level with the road's (the highest
# under it, gfloor: within FLOOR_UNDER under it, nothing over it to FLOOR_HEAD) as far as it goes, FLOOR_MOST at the most: to a wall (or
# another stretch of the lap), where the race-track mode's rail stands in its face, or to a drop's lip, where it stands at the lip, unseen.
# A side keeps its rail where a drop (or another stretch of the lap) is within FLOOR_RAIL of the road's edge; a run of either of fewer than
# FLOOR_RUN points goes with the runs round it. And the lap's points by the shortcut's way back onto it (REENTRY): the floor on the side it
# comes from, no rail.
FLOOR_UNDER, FLOOR_HEAD, FLOOR_MOST, FLOOR_RAIL, FLOOR_RUN, FLOOR_STEP, FLOOR_ALONG = 230, 1400, 20.0, 3.0, 8, 0.25, 0.4
FLOOR_SHORT = 0.4
lap_grid = {}
for j in range(n): lap_grid.setdefault((int(pts[j][0] // 2), int(pts[j][1] // 2)), []).append(j)


def other_road(i, p, t):
    """Whether place p is on another stretch of the lap than point i's (by more than t cells along it), at about its height."""
    gx, gz = int(p[0] // 2), int(p[1] // 2)
    for dz in (-3, -2, -1, 0, 1, 2, 3):
        for dx in (-3, -2, -1, 0, 1, 2, 3):
            for j in lap_grid.get((gx + dx, gz + dz), ()):
                if along(i, j) > t + HALF + 2 and abs(h[j] - h[i]) < 1500 and math.hypot(pts[j][0] - p[0], pts[j][1] - p[1]) < half[j] + 0.5: return True
    return False


def solid_in(x, z, lo, hi):
    """Whether something solid at (x, z) reaches between heights lo and hi -- not what the cuts take: a solid box there, and a cell of its
    layout column there between those heights not cut (or the race's own wall, TEMPLE_WALLS)."""
    if any(e[0] <= x * 512 <= e[3] and e[2] <= z * 512 <= e[5] and e[1] < hi and e[4] > lo for e in extra_solid): return True
    key = layout_of(x, z)
    b = bits.get(key, 0)
    kept_ = [y for y in range(MINY, L['maxY'] + 1) if present(b, y) and (key[0], y, key[1]) not in cuts and top(y - 1) < hi and top(y) > lo]
    # (and a cell standing on the floor there with no box of its own -- a railing, a grille: LBA1 draws a wall)
    if any(top(y - 1) >= lo - 100 for y in kept_): return True
    return bool(kept_) and any(b_[1] < hi and b_[4] > lo for b_ in boxes_at(x, z))


def floor_reach(i, sd, f0):
    """How far the floor level with the road reaches across from point i's middle on side sd (cells), and what stops it. (The floor at a
    place the highest within FLOOR_ALONG of it along the road: the stairs' solid steps leave a gap of half a cell in each of their cells.)"""
    u = tangent(i); nrm = np.array([-u[1], u[0]]) * sd
    t, good = half[i], half[i]
    while t < half[i] + FLOOR_MOST:
        t += FLOOR_STEP
        p = pts[i] + nrm * t
        if other_road(i, p, t): return good, 'road'
        if solid_in(p[0], p[1], f0 + 80, f0 + FLOOR_HEAD): return good, 'wall'
        tops = [floor_top(*(p + u * a), f0 + 80) for a in (-FLOOR_ALONG, 0.0, FLOOR_ALONG)]
        tops = [ft for ft in tops if ft is not None]
        if not tops or max(tops) < f0 - FLOOR_UNDER: return good, 'drop'
        good = t
    return good, 'most'


floor_lo, floor_hi = np.zeros(n), np.zeros(n)
flush = np.zeros((n, 2), bool)
reentry = np.zeros(n, bool)
if SHORTCUT:
    a_, b_ = nearest(REENTRY[0], i_west, o_down), nearest(REENTRY[1], i_west, o_down)
    reentry[a_:b_ + 1] = True
sides = {}
for i in np.flatnonzero(ground | reentry):
    f0 = gfloor[i] if ground[i] else h[i] - DECK
    for s_, sd in enumerate((-1, 1)):
        if reentry[i] and not ground[i] and sd != 1: continue
        sides[(i, s_)] = floor_reach(i, sd, f0)
for s_ in (0, 1):
    idx = [i for i in range(n) if (i, s_) in sides]
    reach = np.array([sides[(i, s_)][0] for i in idx])
    rail = np.array([sides[(i, s_)][1] in ('drop', 'road') and sides[(i, s_)][0] - half[i] < FLOOR_RAIL for i in idx])
    # (short runs of either go with the runs round them; the reach the least of the points round it)
    for _ in range(2):
        runs = np.split(np.arange(len(idx)), np.flatnonzero(np.diff(rail.astype(int))) + 1)
        for r_ in runs:
            if 0 < len(r_) < FLOOR_RUN and r_[0] > 0 and r_[-1] < len(idx) - 1: rail[r_] = ~rail[r_]
    reach = np.array([reach[max(0, k - 2):k + 3].min() for k in range(len(idx))])
    for k, i in enumerate(idx):
        flush[i, s_] = not rail[k]
        # (FLOOR_SHORT short of what stops it: the reach is found a FLOOR_STEP at a time, and the road's points between the plan's take theirs)
        out = reach[k] - FLOOR_SHORT if not rail[k] and reach[k] > half[i] + FLOOR_SHORT + 0.25 else half[i]
        if s_ == 0: floor_lo[i] = -out
        else: floor_hi[i] = out
# (and between two points, where the race-track mode takes the floor's edge across each segment of the road from one point's to the next's:
# that floor clear of walls and drops too -- round a bend, the edge cuts across a corner the points' own ways out didn't see -- or both ends
# brought in a little, until it is)
def floor_clear(i, j, s_):
    a, c = pts[i], pts[j]
    sv = c - a; ln = float(np.linalg.norm(sv))
    if ln < 1e-6: return True
    u = sv / ln; nseg = np.array([-u[1], u[0]]) * (1 if s_ else -1)
    f0 = min(gfloor[i] if ground[i] else h[i] - DECK, gfloor[j] if ground[j] else h[j] - DECK)
    f1 = max(gfloor[i] if ground[i] else h[i] - DECK, gfloor[j] if ground[j] else h[j] - DECK)
    for t in (0.0, 0.25, 0.5, 0.75, 1.0):
        out = abs((floor_lo if not s_ else floor_hi)[i] * (1 - t) + (floor_lo if not s_ else floor_hi)[j] * t)
        hw = half[i] * (1 - t) + half[j] * t
        d = hw + 0.25
        while d <= out + 1e-6:
            q = a + sv * t + nseg * d
            if solid_in(q[0], q[1], f1 + 80, f1 + FLOOR_HEAD): return False
            tops = [ft for ft in (floor_top(*(q + u * b), f1 + 80) for b in (-FLOOR_ALONG, 0.0, FLOOR_ALONG)) if ft is not None]
            if not tops or max(tops) < f0 - FLOOR_UNDER: return False
            d += 0.5
    return True


shrunk = 0
for _ in range(80):
    bad = 0
    for s_ in (0, 1):
        arr = floor_lo if not s_ else floor_hi
        for i in np.flatnonzero(ground | reentry)[:-1]:
            j = i + 1
            if abs(arr[i]) <= half[i] + 1e-6 and abs(arr[j]) <= half[j] + 1e-6: continue
            if floor_clear(i, j, s_): continue
            bad += 1
            for k in (i, j):
                if abs(arr[k]) > half[k] + 1e-6: arr[k] = (1 if s_ else -1) * max(half[k], abs(arr[k]) - 0.25); shrunk += 1
    if not bad: break
if ROUTE_EAST: print('floors beside the road: brought in %d times where the edge between two points crossed a wall or a drop (%d passes)' % (shrunk, _ + 1))
has_floor = (floor_lo < -half - 1e-6) | (floor_hi > half + 1e-6)
floor_lo = np.where(has_floor, np.where(floor_lo == 0, -half, floor_lo), 0.0)
floor_hi = np.where(has_floor, np.where(floor_hi == 0, half, floor_hi), 0.0)
if ROUTE_EAST:
    why = {}
    for (i, s_), (r_, w_) in sides.items(): why[w_] = why.get(w_, 0) + 1
    print('floors beside the road: %d points (%d of them the shortcut\'s way back), no rail on %d sides of %d; the floor reaching %.1f cells past the edge at the most, '
          'stopped by %s' % (int(has_floor.sum()), int(reentry.sum()), int(flush.sum()), len(sides), max(0.0, float((-floor_lo - half).max()), float((floor_hi - half).max())), why))

# ---- the shortcut as the plan has it: the teleporter (its box: the car's middle within SECRET_DEEP of the walled-up door's face, between
# SECRET_X, about the room's floor; where the car comes out, its height, the way it looks -- south -- and its speed), the wall (TEMPLE_WALLS:
# templeWalls, [x, z, from layer, to layer, the column whose bricks it takes]), the open area it comes out into (openAreas: [x0, z0, x1, z1,
# its floor]) and the stretch of the lap it skips, from the point nearest the walled-up door to the lap's way west past where it comes back
# (shortcuts: [first, last], the plan's points) -- the checkpoints keep off it
teleports, shortcuts = [], []
if SHORTCUT:
    def plan_index(i): return int((i - i_start) % n)
    room = top(0)
    teleports.append([SECRET_X[0], SECRET_FACE, SECRET_X[1], SECRET_FACE + SECRET_DEEP, room - 600, room + 2400, OUT_X, OUT_Z, FIRST + 20, OUT_BETA, OUT_SPEED, OUT_PASSAGE])
    i_in, i_back = nearest((EAST_X, SECRET_FACE + 3), i_west, o_down), nearest(SKIP_TO, i_west, o_down)
    shortcuts.append([plan_index(i_in), plan_index(i_back)])
    print('shortcut: the walled-up door (x %.1f..%.1f, its face z %.1f), the lap left at point %d (%.1f, %.1f), out at (%.1f, %.1f) at %d units/s after %d ms, back on the lap by '
          'point %d (%.1f, %.1f): skips %.0f cells of the lap' % (*SECRET_X, SECRET_FACE, i_in, *pts[i_in], OUT_X, OUT_Z, OUT_SPEED, OUT_PASSAGE, i_back, *pts[i_back], S[i_back] - S[i_in]))
    # (no decor where the car drives off the road for it -- the whole approach -- nor where it comes out and on its way down to the road)
    before = len(decor) + len(scares) + len(spooks)
    decor = [d for d in decor if secret_off(d[1], d[2], wide=True) >= 1.5]
    scares = [s_ for s_ in scares if secret_off(s_[1], s_[2], wide=True) >= 1.5]
    spooks = [s_ for s_ in spooks if s_['kind'] != 'riser' or secret_off(*s_['way'][0], wide=True) >= 1.5]
    print('shortcut: %d pieces of decor left out where its car drives' % (before - len(decor) - len(scares) - len(spooks)))

# ---- the spiral's drop (the user, 2026-10-20: "The upward spiral, let's remove the guard rail where we're over the track below to add in
# a shortcut", their picture the spiral west of the crypt -- turned as their pictures are, east up -- the road out of its foot heading east
# to the crypt, and the turn of the spiral one over it, where it passes over that road just after its south point): along the outer edge
# of that turn where it lies over the road below -- the road out of the spiral's foot, a turn (DROP_LOW..DROP_HIGH) under it -- no rail
# (raisedFlush) and the race-track mode's rail lets the car go there (railOpens: RACEMOD.CPP rail_open=, a polygon from DROP_IN inside the
# edge to DROP_OUT past it, at the turn's height): a car steered off that edge falls onto the road below, a turn of the spiral further
# on (LBA2 drops a falling car straight down: it lands under where it left), and lands as softly as from a step (rail_open='s fall hurts
# no car). Only where the road below is under the edge, DROP_LAND cells and more inside its own edges either side of where the car goes
# over (DROP_OVER past the edge: a car held on the road has its middle RAISED_INSET inside it, let go it is past it a little before it
# drops). The lap's checkpoints keep off the turn it skips (the plan's shortcuts, as the hidden passage's); the opponents keep to the lap.
# TEMPLE_DROP=0: none.
DROP = os.environ.get('TEMPLE_DROP', '1') != '0'
DROP_LOW, DROP_HIGH = HEAD + DECK, 7000
DROP_IN, DROP_OUT = 2.0, 5.0
DROP_OVER = (0.5, 2.5)
DROP_LAND = 1.0
rail_opens = []


def plan_index(i): return int((i - i_start) % n)


if DROP:
    def lower_road(p, i):
        """The point of the road a turn under point i nearest place p, and how far p is inside its edge (cells; negative: outside it)."""
        d = np.hypot(pts[:, 0] - p[0], pts[:, 1] - p[1])
        dh = h[i] - h
        ok = (dh >= DROP_LOW) & (dh <= DROP_HIGH)
        if not ok.any(): return None, -1e9
        j = int(np.flatnonzero(ok)[d[ok].argmin()])
        return j, float(half[j] - d[j])
    cx_, cz_, r_ = SPIRAL_W
    run, lands = [], []
    for i in range(o_down, o2):
        u = tangent(i); nrm = np.array([-u[1], u[0]])
        # (the outer side: away from the spiral's middle)
        sd = 1 if float(nrm @ (pts[i] - np.array([cx_, cz_]))) > 0 else -1
        got = [lower_road(pts[i] + nrm * sd * (half[i] + o), i) for o in DROP_OVER]
        if all(j is not None and inside >= DROP_LAND for j, inside in got):
            run.append((i, sd)); lands.append(max(j for j, _ in got))
    if run:
        # (one stretch: the longest run of points in a row)
        idx = [i for i, _ in run]
        runs_ = np.split(np.arange(len(idx)), np.flatnonzero(np.diff(idx) != 1) + 1)
        best = max(runs_, key=len)
        run = [run[k] for k in best]; lands = [lands[k] for k in best]
        a_, b_ = run[0][0], run[-1][0]
        sd = run[0][1]
        poly = []
        for i, _ in run[::4] + [run[-1]]:
            u = tangent(i); nrm = np.array([-u[1], u[0]])
            poly.append(pts[i] + nrm * sd * (half[i] - DROP_IN))
        for i, _ in (run[::4] + [run[-1]])[::-1]:
            u = tangent(i); nrm = np.array([-u[1], u[0]])
            poly.append(pts[i] + nrm * sd * (half[i] + DROP_OUT))
        y0, y1 = float(h[a_:b_ + 1].min() - 300), float(h[a_:b_ + 1].max() + 1500)
        rail_opens.append([round(y0), round(y1)] + [round(float(v), 2) for q in poly for v in q])
        for i in range(a_, b_ + 1): flush[i, (sd + 1) // 2] = True
        land = max(lands)
        shortcuts.append([plan_index(a_), plan_index(land)])
        print('the spiral\'s drop: no rail along the outer edge of its turn over the road out of its foot, points %d..%d (plan %d..%d, cells (%.1f, %.1f)..(%.1f, %.1f), '
              '%.1f cells), %.0f..%.0f over the road below (landing by point %d, plan %d: skips %.0f cells of the lap); its polygon %d corners'
              % (a_, b_, plan_index(a_), plan_index(b_), *pts[a_], *pts[b_], S[b_] - S[a_], min(h[i] - h[j] for (i, _), j in zip(run, lands)),
                 max(h[i] - h[j] for (i, _), j in zip(run, lands)), land, plan_index(land), S[land] - S[a_], len(poly)))
    else:
        print('the spiral\'s drop: nowhere over the road below')

# ---- the lap from the start
order = np.r_[i_start:n, 0:i_start]
P, H, HF = pts[order], h[order], half[order]
plan = {
    'originCellX': 0, 'originCellZ': 0,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in P],
    'heights': [round(float(v), 1) for v in H],
    'start': 0,
    'asphaltHalf': HALF - 1.0, 'curbHalf': HALF - 0.25, 'vergeHalf': HALF + 0.5, 'blend': 2.0,
    'raised': [0, n - 1], 'raisedHalf': HALF,
    'raisedHalfs': [round(float(v), 2) for v in HF],
    'raisedStrips': [[float(a), float(b)] for a, b in zip(slip_lo[order], slip_hi[order])],
    'themes': [{'from': 0, 'to': n - 1, 'theme': 'halloween'}],
    'railCamera': [9, 2100, 7, 500, 1000],
    'templeCuts': sorted([list(c) for c in cuts]),
    'templeFloors': floors,
    'templeSkirts': skirts,
    'pitSpots': pit_spots,
    'gridFirst': GRID_FIRST, 'gridSide': GRID_SIDE,
    'itemCubeMargin': ITEM_CUBE_MARGIN,
    'halloween': {'decor': decor, 'spooks': spooks, 'pumpkins': True, 'dives': dives, 'scares': scares, 'haunt': True, 'giantWebs': giant_webs},
    'keepAbove': 0,
}
if ROUTE_EAST:
    # (and the camera across with the car where it drives off the road onto the floor beside it: RACEMOD.CPP railcam='s sixth)
    plan['railCamera'] = plan['railCamera'] + [1]
    plan.update({'raisedFloors': [[round(float(a), 2), round(float(b), 2)] for a, b in zip(floor_lo[order], floor_hi[order])],
                 'raisedFlush': [[int(a), int(b)] for a, b in flush[order]]})
if SHORTCUT:
    plan.update({'teleports': teleports, 'shortcuts': shortcuts, 'templeWalls': TEMPLE_WALLS, 'openAreas': [OPEN_EXIT + [FIRST]]})
if shortcuts: plan['shortcuts'] = shortcuts
if rail_opens: plan['railOpens'] = rail_opens
json.dump(plan, open(OUT, 'w'))
print('wrote', OUT, len(P), 'points')
