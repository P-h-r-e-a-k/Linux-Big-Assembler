# Island CX (CXTOWER.ILE: Island CX as it is, the control tower's lower level under its gas -- RaceTrackIsland.CxTower) -- the race track
# (the user, 2026-10-15: "Create a track for our CXTower island. The basic concept for this track is a loop around the tower, the track enters
# the underground section through a slope down through one of the doors, which means we'll need to remove the metal grate that's currently
# there, loops around the underground section a few times then back out a door on the other side. Don't forget to add a pit lane. Let's have
# the cannons that normally fire at Twinsen outside randomly firing at any outside car"; then: "In the underground section there's some gas
# monsters that attack the player let's build on this theme for the underground section").
#
# The island is one cube (7,7): cells 0..64 each way, the plan's origin its corner (448, 448). Its plateau (2,560) x/z 18..46 with the
# control tower in its middle, two L-shaped trenches sunk in it (640) -- the south-west one's north end a door (A: 20.5, 31, its grate body 9),
# the north-east one's south end another (C: 43.5, 33.3) -- the rim and slopes round it, a cannon tower on each corner (8.2..16.3 and
# 47.7..55.8 each way) and the gas round it all (0). Under the gas the room the build makes for the race (ControlTowerIsland.RaceRoom): one
# ring of a hall round the control room (x/z 20..41), its floor at -5,376, its roof at -512 -- open where the road comes down through it.
#
# The lap (counterclockwise under the island, the outside clockwise round its east half):
#  - the start straight along the south side west (z 52), its pit lane straight on along z 60 from the S-bend before it, back onto the road
#    at the turn into the trench;
#  - north down the south-west trench (cut wider) and through door A, its grate gone, under the island down a tunnel, a U-turn under the
#    north-west tower and on down into the ring;
#  - once round the ring on its inner lanes (west, south, east, north) and on round the outer ones (west and south), past the gas monsters;
#  - north up a tunnel out of door C and the north-east trench, round the north-east tower, down the east side over the gas, round the
#    south-east tower and an S-bend onto the start straight.
# Run: E:\dump\TEMP\cxr holding H77.npy (the cube's heights: heights.py), race/top.npy (the ring's walls: topmap.py on the race island) and
# boxes.txt (the race island's decors: decorpoints); --write writes docs/racetrack/cxtower_track_plan.json; --pictures draws the lap.
import json, math, re, sys
import numpy as np

D = 'E:/dump/TEMP/cxr/'
OUT = 'E:/dump/LBAAssembler/docs/racetrack/cxtower_track_plan.json'
OX, OZ = 448, 448
H = np.load(D + 'H77.npy').astype(float)
TOPW = np.load(D + 'race_top.npy')             # the ring's walls: each cell's highest layer (-1 none, 0 the floor)
STEP = 0.5

def ground(x, z):
    x = min(63.999, max(0.0, x)); z = min(63.999, max(0.0, z))
    xi, zi = int(x), int(z); fx, fz = x - xi, z - zi
    return H[zi, xi] * (1 - fx) * (1 - fz) + H[zi, xi + 1] * fx * (1 - fz) + H[zi + 1, xi] * (1 - fx) * fz + H[zi + 1, xi + 1] * fx * fz

FLOOR = -5376.0                                # the ring's floor
ROOF = -512.0                                  # its roof's underside
DECK = 200.0                                   # a deck's thickness (RaceTrackRaisedBody.Thickness)
F = FLOOR + DECK + 10                          # the road's height on the ring's floor
HALF_OUT, HALF_IN = 3.5, 3.0                   # the road's half width outside, and under the island
DOOR_A, DOOR_C = (20.5, 31.0), (43.5, 33.3)    # the doors (their grates' middles)
TUN_ROOF, TUN_SLAB, TUN_UNDER = 1200.0, 320.0, 500.0   # the tunnels' roofs over the deck (RaceTrackTunnel: Slab, Under)
SUNK = 3.0                                     # cells of tunnel at each door sunk in the plateau (its roof flush with it)
PLATEAU = 2560.0
STEEP = 0.26                                   # the ramps under the island to and from the doors
GAS_MONSTER_FLOOR = FLOOR

# ---- the lap: corners (cells) and their radii, from the start straight round
ROUND = [((34.0, 52.0), 0.0),                  # the start straight, west
         ((20.5, 52.0), 5.5),                  # north into the south-west trench ...
         ((20.5, 11.5), 4.5),                  # ... through door A, under the island; the U-turn under the north-west tower ...
         ((11.5, 11.5), 4.5),
         ((11.5, 47.0), 5.0),                  # ... into the ring: its west side's inner lane, south ...
         ((55.0, 47.0), 5.0),                  # ... the south side's inner lane, east ...
         ((55.0, 4.5), 5.0),                   # ... the east side, north ...
         ((4.5, 4.5), 4.0),                    # ... the north side, west ...
         ((4.5, 58.0), 4.0),                   # ... the west side's outer lane, south ...
         ((43.5, 58.0), 5.0),                  # ... the south side's outer lane, east; north up the tunnel to door C ...
         ((43.5, 4.5), 5.0),                   # ... out of the north-east trench, north past the north-east tower ...
         ((59.5, 4.5), 4.0),                   # ... east, round it ...
         ((59.5, 59.5), 4.0),                  # ... down the east side over the gas, round the south-east tower ...
         ((44.0, 59.5), 4.0),                  # ... west, the S-bend ...
         ((40.0, 52.0), 4.0)]                  # ... onto the start straight

def rounded(K, closed=False):
    """A polyline through corners rounded with their radii (a corner of radius 0 is a point the line passes through)."""
    n = len(K); P = [np.array(p, float) for p, r in K]; out = []
    arcs = []
    for i in range(n):
        if not closed and (i == 0 or i == n - 1) or K[i][1] <= 0: arcs.append(None); continue
        a, b, c = P[i - 1], P[i], P[(i + 1) % n]; r = K[i][1]
        u1 = (b - a) / np.linalg.norm(b - a); u2 = (c - b) / np.linalg.norm(c - b)
        turn = math.atan2(u1[0] * u2[1] - u1[1] * u2[0], u1 @ u2)
        t = r * math.tan(abs(turn) / 2)
        p1, p2 = b - u1 * t, b + u2 * t
        nrm = np.array([-u1[1], u1[0]]) * (1 if turn > 0 else -1)
        arcs.append((p1, p2, p1 + nrm * r, r, turn))
    for i in range(n if closed else n - 1):
        if arcs[i] is None: q1 = P[i]
        else:
            p1, p2, centre, r, turn = arcs[i]
            a0 = math.atan2(p1[1] - centre[1], p1[0] - centre[0])
            m = max(2, int(round(r * abs(turn) / 0.02)))
            for k in range(m): out.append(centre + r * np.array([math.cos(a0 + turn * k / m), math.sin(a0 + turn * k / m)]))
            q1 = p2
        j = (i + 1) % n
        q2 = P[j] if arcs[j] is None else arcs[j][0]
        if np.dot(q2 - q1, P[j] - P[i]) < -1e-6: sys.exit(f'corners {i} and {j}: their roundings overlap')
        m = max(1, int(round(np.linalg.norm(q2 - q1) / 0.02)))
        for k in range(m): out.append(q1 + (q2 - q1) * k / m)
    if not closed: out.append(P[-1])
    return np.array(out)

fine = rounded(ROUND, closed=True)
fine = fine[np.concatenate([[True], np.hypot(*np.diff(fine, axis=0).T) > 1e-9])]
seg = np.linalg.norm(np.diff(np.vstack([fine, fine[:1]]), axis=0), axis=1)
cum = np.concatenate([[0], np.cumsum(seg)]); total = cum[-1]
N = int(round(total / STEP)); s = np.arange(N) * total / N
X = np.interp(s, cum, np.append(fine[:, 0], fine[0, 0])); Z = np.interp(s, cum, np.append(fine[:, 1], fine[0, 1]))
T = np.stack([np.gradient(X), np.gradient(Z)], 1); T /= np.linalg.norm(T, axis=1)[:, None]
Nn = np.stack([-T[:, 1], T[:, 0]], 1)
def ahead(a, b): return ((b - a) % N) * STEP
def between(k, a, b): return ahead(a, k) <= ahead(a, b)
def nearest_after(p, after, window):
    best, bd = after, 1e9
    for q in range(int(window / STEP)):
        k = (after + q) % N
        d = math.hypot(X[k] - p[0], Z[k] - p[1])
        if d < bd: bd, best = d, k
    return best
def ease(f): f = min(1, max(0, f)); return f * f * (3 - 2 * f)

# ---- marks round the lap
MARK = {}
MARK['start'] = nearest_after((30.0, 52.0), 0, 20)                        # the start line on the straight
MARK['trench'] = nearest_after((20.5, 46.0), MARK['start'], 40)           # in the trench's south end
MARK['doorA'] = nearest_after(DOOR_A, MARK['trench'], 30)                 # through door A
MARK['uA'] = nearest_after((20.5, 16.0), MARK['doorA'], 30)               # the U-turn's start ...
MARK['uB'] = nearest_after((11.5, 16.0), MARK['uA'], 30)                  # ... and end
MARK['sw1'] = nearest_after((11.5, 42.0), MARK['uB'], 40)                 # the ring's floor reached
MARK['sw2'] = nearest_after((8.5, 58.0), MARK['sw1'], 300)                # the outer lanes' south-west corner: the climb begins
MARK['exitArc'] = nearest_after((38.5, 58.0), MARK['sw2'], 60)
MARK['doorC'] = nearest_after(DOOR_C, MARK['exitArc'], 40)                # out of door C
MARK['trenchN'] = nearest_after((43.5, 20.0), MARK['doorC'], 20)          # the north-east trench's north end
MARK['north'] = nearest_after((43.5, 9.5), MARK['trenchN'], 20)
MARK['ne'] = nearest_after((60.0, 9.0), MARK['north'], 40)
MARK['se'] = nearest_after((56.0, 60.0), MARK['ne'], 80)
MARK['sbend'] = nearest_after((44.0, 60.0), MARK['se'], 30)
MARK['straight'] = nearest_after((38.0, 52.0), MARK['sbend'], 30)

# ---- heights: keyed at marks, straight grades between, the kinks rounded
Y = np.zeros(N)
def climb(a, b, h0, h1, shape=lambda t: t):
    L = ahead(a, b); k = a
    while True:
        Y[k] = h0 + (h1 - h0) * shape(ahead(a, k) / L if L else 1)
        if k == b: break
        k = (k + 1) % N
H_OUT = 2100.0                                 # the outside road over the gas and the slopes
H_STRAIGHT = 2000.0
H_DOOR_A, H_DOOR_C = 450.0, 640.0
H_A_U = H_DOOR_A - ahead(MARK['doorA'], MARK['uA']) * 512 * STEEP
H_U_END = H_A_U - ahead(MARK['uA'], MARK['uB']) * 512 * STEEP
# (the exit: STEEP for its last EXIT_STEEP cells to door C, from the climb out of the floor before it)
EXIT_STEEP = 22.0
MARK['exitSteep'] = (MARK['doorC'] - int(EXIT_STEEP / STEP)) % N
H_EXIT_LOW = H_DOOR_C - EXIT_STEEP * 512 * STEEP
climb(MARK['start'], MARK['trench'], H_STRAIGHT, 1500.0)
climb(MARK['trench'], MARK['doorA'], 1500.0, H_DOOR_A)
climb(MARK['doorA'], MARK['uA'], H_DOOR_A, H_A_U)
climb(MARK['uA'], MARK['uB'], H_A_U, H_U_END)
climb(MARK['uB'], MARK['sw1'], H_U_END, F)
climb(MARK['sw1'], MARK['sw2'], F, F)
climb(MARK['sw2'], MARK['exitSteep'], F, H_EXIT_LOW)
climb(MARK['exitSteep'], MARK['doorC'], H_EXIT_LOW, H_DOOR_C)
climb(MARK['doorC'], MARK['trenchN'], H_DOOR_C, 1500.0)
climb(MARK['trenchN'], MARK['north'], 1500.0, H_OUT)
climb(MARK['north'], MARK['sbend'], H_OUT, H_OUT)
climb(MARK['sbend'], MARK['start'], H_OUT, H_STRAIGHT)
def smooth(v, w):
    pad = w // 2
    vv = np.concatenate([v[-pad:], v, v[:w - 1 - pad]])
    return np.convolve(vv, np.ones(w) / w, mode='valid')
Y = smooth(Y, 9)
# (the ring's floor stretch held level: the smoothing pulled its ends)
for k in range(N):
    if between(k, (MARK['sw1'] + 6) % N, (MARK['sw2'] - 6) % N): Y[k] = F
grade = (np.roll(Y, -1) - Y) / (STEP * 512)

HALF = np.full(N, HALF_OUT)
under = np.zeros(N, bool)
iDoorA_in = (MARK['doorA'] + int(SUNK / STEP)) % N
iDoorC_in = (MARK['doorC'] - int(SUNK / STEP)) % N
for k in range(N):
    if between(k, MARK['doorA'], MARK['doorC']): HALF[k] = HALF_IN
    if between(k, iDoorA_in, iDoorC_in): under[k] = True
# (the half width eased between the two over 4 cells either side of the doors)
for d, sign in ((MARK['doorA'], 1), (MARK['doorC'], -1)):
    for q in range(int(4 / STEP)):
        k = (d - sign * q) % N
        HALF[k] = HALF_IN + (HALF_OUT - HALF_IN) * ease(q / (4 / STEP))
# ---- wider where there is room (the user, 2026-10-17: "CXTower track feels too tight let's see where there's scope for widening the track
# slightly but let's keep the current route just making changes where we're able too"): the same middle, and the road wider where nothing
# stands near it -- 4 cells from its middle to its rail outside, 3.5 in the ring (from 3.5 and 3) -- less where the ring's outer walls or
# its two lanes side by side are near, and as it was round the corner towers and through the doors; eased between over the stretch from one
# place of WIDTHS to the next (the lap's places in its order, each with its half width). The racing line is left where it was: planned
# within the old widths (the plan's lineHalfs). What limits each (cells from the road's middle; the rail keeps a car's middle 1.25 inside
# the deck's edge):
#   - the start straight: the pit lane beside it (7.5 middle to middle, its half 2.75: 0.75 between the decks); the turn into the
#     south-west trench, and the trench: the south-west tower's base at the turn's end (0.7 clear at 3.75 -- 0.45 for a car's box, which
#     is square to the island; at 4 that would be 0.2), the trench's sides the plateau's ground, cut back to the wider road;
#   - the doors -- their portals and the tunnels down to the ring and up from it, as they were (3): a tunnel's roof is kept under the ground
#     over its walls (TunnelRun.UnderGround), and the plateau's edge falls away beside both ramps -- half a cell wider, its roof 240 lower,
#     1,200-1,400 over the deck at the doors' faces (the car is 940 tall: the camera had no room over it);
#   - the ring's west side: its inner lane (the ramp down from the U-turn) and its outer one side by side 7 apart (3.15 and 3.2: 0.65
#     between them), the outer one beside the west wall (3.5: 0.3 clear, as this script's check of the walls asks); the north side beside
#     the north wall (the same);
#   - the south side: the gas monster between its two lanes -- it stands its run's out past the inner lane's rail and must be the outer
#     lane's half and 2.5 from that lane's middle (RaceTrackGasMonster ClearOfLap), the lanes 11 apart: 2 out (from 2.5; nearer the rail
#     the poses put its neck through the rail), the inner lane 3.4 and the outer one as it was, 3 (0.1 to spare); else the control room's
#     wall (5 from the inner lane: 1.6 clear) and the south wall (4 from the outer one);
#   - the east side: nothing near (the wall 7 off, the control room 13);
#   - the north-east trench: ground, cut back; round the north-east tower and the south-east one, and between them along the east side over
#     the gas: the towers' bases and struts (0.15-0.6 clear at 3.5: no wider) and the cube's edge (4.5: the rail 0.5 from it between them);
#   - the S-bend onto the start straight: the south-east tower, the pit lane branching off.
LINE_HALF = HALF.copy()
WIDTHS = [((38.0, 52.0), 3.5), ((34.0, 52.0), 4.0),           # onto the start straight: wider ...
          ((26.0, 52.0), 4.0), ((20.7, 47.9), 3.75),           # ... round into the trench by the south-west tower ...
          ((20.5, 38.0), 3.75), (DOOR_A, 3.0),                 # ... narrowing to door A; its tunnels as they were
          ((20.5, 16.0), 3.0), ((11.5, 16.0), 3.15),           # round the U-turn; down the ring's west side, its outer lane beside
          ((11.5, 42.0), 3.15), ((16.5, 47.0), 3.4),           # round into the south side's inner lane ...
          ((50.0, 47.0), 3.4), ((55.0, 42.0), 3.5),            # ... round the east side ...
          ((55.0, 9.5), 3.5), ((50.0, 4.5), 3.2),              # ... round into the north side, its wall near
          ((8.5, 4.5), 3.2), ((4.5, 8.5), 3.2),                # the west side's outer lane
          ((4.5, 54.0), 3.2), ((8.5, 58.0), 3.0),              # the south side's outer lane as it was
          ((38.5, 58.0), 3.0), ((43.5, 53.0), 3.0),            # round and up to door C, its tunnels as they were
          (DOOR_C, 3.0), ((43.5, 26.3), 4.0),                  # out of it, the north-east trench wider ...
          ((43.5, 19.5), 4.0), ((43.5, 15.5), 3.5),            # ... to before the north-east tower's base
          ((59.5, 17.5), 3.5), ((59.5, 21.5), 4.0),            # the east side between the towers wider ...
          ((59.5, 43.5), 4.0), ((59.5, 47.5), 3.5)]            # ... and as it was from the south-east tower to the S-bend
# (the ring's south-west inner corner keeps its checkpoint: the builder lays a line across a corner only where it reaches past the road's
# edge, or is stopped by another part of the lap no nearer than that edge -- probing in half cells, so 3 wide it stopped at the edge and
# wider short of it (the west outer lane beside): a line of the plan's own there, the corner's middle)
CHECKPOINT_TWEAKS = [{'add': True, 'at': [round(16.5 - 5 * math.sqrt(0.5), 3), round(42.0 + 5 * math.sqrt(0.5), 3)]}]
at = [int(np.argmin((X - WIDTHS[0][0][0]) ** 2 + (Z - WIDTHS[0][0][1]) ** 2))]
for p, _ in WIDTHS[1:]: at.append(nearest_after(p, at[-1], 120))
for w in range(len(WIDTHS)):
    a, b = at[w], at[(w + 1) % len(WIDTHS)]
    ha, hb = WIDTHS[w][1], WIDTHS[(w + 1) % len(WIDTHS)][1]
    L = ahead(a, b)
    for q in range(int(round(L / STEP)) + 1): HALF[(a + q) % N] = ha + (hb - ha) * ease(q * STEP / L)

# ---- the tunnels: at each door its first SUNK cells sunk in the plateau (the ground cut between its walls, its roof flush with the plateau),
# then on under the island until the road is in the ring under its roof
def tunnel_needed(k): return Y[k] + TUN_ROOF + TUN_SLAB > ROOF - 300
kA_end = next(k % N for k in range(iDoorA_in, iDoorA_in + N) if not tunnel_needed(k % N))
kC_start = next((iDoorC_in - q) % N for q in range(N) if not tunnel_needed((iDoorC_in - q) % N))
# (2026-10-16, the user: "the camera needs fixing on the entry and exit it's too hard to see". A tunnel's roof 1,200 over the deck kept the
# camera's eye 300 over the road behind the car -- under the car's deck on the 26 % climb out to door C -- and door A's tunnel ran on round
# the U-turn under the north-west tower, its inner wall's end between the eye and the car. Now each ramp's tunnel only where the ramp is
# straight and under the plateau, TUN_HIGH tall (kept under the ground over it: underGround), and from there into the ring a tunnel of the
# camera's alone whose eye is kept under the ring's roof -- its ceiling, RACEMOD.CPP tunnel='s fourth number)
TUN_HIGH = 2200.0
kA_split = MARK['uA']                                                     # door A's: on to the U-turn's start
kC_split = nearest_after((43.5, 52.5), MARK['exitArc'], 20)               # door C's: from the exit arc's end
# (where it comes out into the ring, closeAbove: a wall across its end over the ring's roof -- the rock over the roof isn't drawn, and
# the gas over the island showed through it.) (Each in two: its first two cells from the door, under the plateau's face -- its roof lower
# there, under the face's slope -- and the rest: the camera keeps its eye under a tunnel's least roof.)
DOOR_PART = int(2.0 / STEP)
kA_face, kC_face = (iDoorA_in + DOOR_PART) % N, (iDoorC_in - DOOR_PART) % N
TUNNELS = [{'from': int(MARK['doorA']), 'to': int(iDoorA_in), 'roof': TUN_ROOF, 'sunk': True, 'top': PLATEAU, 'eave': 0.5},
           {'from': int(iDoorA_in), 'to': int(kA_face), 'roof': TUN_HIGH, 'underGround': 40.0},
           {'from': int(kC_face), 'to': int(iDoorC_in), 'roof': TUN_HIGH, 'underGround': 40.0},
           {'from': int(iDoorC_in), 'to': int(MARK['doorC']), 'roof': TUN_ROOF, 'sunk': True, 'top': PLATEAU, 'eave': 0.5},
           {'from': int(kA_face), 'to': int(kA_split), 'roof': TUN_HIGH, 'underGround': 40.0, 'closeAbove': ROOF},
           {'from': int(kC_split), 'to': int(kC_face), 'roof': TUN_HIGH, 'underGround': 40.0, 'closeAbove': ROOF}]
# (and in the ring, tunnels of the camera's alone -- no pieces -- their roofs the ring's roof over the road: the camera that follows the car
# stood 6,000 over it, out over the ring's roof and the gas, where the island's ground near it isn't drawn: the car was seen from over the
# island through its ground. Under them the race-track mode's tunnel camera rides behind the car under the roof)
def camera_tunnel(a, b):
    least = min(ROOF - 250 - Y[k % N] for k in range(a, a + int(ahead(a, b) / STEP) + 1))
    return {'from': int(a), 'to': int(b), 'roof': round(float(least)), 'pieces': False}
TUNNELS += [camera_tunnel(MARK['sw1'], MARK['sw2'])]
# (the ramps in the ring, from door A's tunnel round the U-turn down to the floor and from the outer lanes' corner up to door C's: the
# eye behind the car the way the road runs at it, under the ring's roof -- its ceiling -- as high over the road as it likes, RACEMOD.CPP
# HallEye; on the road behind the car round the U-turn, it stood off to the car's side)
TUNNELS += [{'from': int(kA_split), 'to': int(MARK['sw1']), 'roof': 2400, 'pieces': False, 'ceiling': ROOF},
            {'from': int(MARK['sw2']), 'to': int(kC_split), 'roof': 2400, 'pieces': False, 'ceiling': ROOF}]
in_tunnel = np.zeros(N, bool)
tunnel_roof = np.zeros(N)
for t in [t for t in TUNNELS if t.get('pieces', True)]:
    k = t['from']
    while True:
        in_tunnel[k] = True; tunnel_roof[k] = t['roof']
        if k == t['to']: break
        k = (k + 1) % N
def runs(mask):
    out = []; k0 = None
    start = int(np.argmin(mask)) if not mask.all() else 0
    for q in range(N + 1):
        k = (start + q) % N
        if mask[k] and k0 is None: k0 = k
        if (not mask[k] or q == N) and k0 is not None: out.append([int(k0), int((k - 1) % N)]); k0 = None
    return out
UNDER_RUNS = runs(under)

# ---- the pit lane: straight on west along z 60 where the lap's S-bend turns up onto the start straight, then north onto the lap at the turn
# into the trench -- a branch of the raised road
PIT_Z, PIT_HALF = 59.5, 2.75
PL_F = nearest_after((48.0, 60.0), MARK['se'], 30)                       # off the lap: just before the S-bend
PL_J = nearest_after((20.5, 44.0), MARK['start'], 60)                    # back on: in the trench's mouth
pit = rounded([((X[PL_F], Z[PL_F]), 0.0), ((20.5, PIT_Z), 4.5), ((X[PL_J], Z[PL_J]), 0.0)])
def resample(pl):
    pl = pl[np.concatenate([[True], np.hypot(*np.diff(pl, axis=0).T) > 1e-9])]
    c = np.concatenate([[0], np.cumsum(np.hypot(*np.diff(pl, axis=0).T))])
    m = max(1, int(round(c[-1] / STEP)))
    t = np.linspace(0, c[-1], m + 1)
    return np.interp(t, c, pl[:, 0]), np.interp(t, c, pl[:, 1]), c[-1]
PX, PZ, pit_len = resample(pit)
PM = len(PX)
OUTSIDE = (Y > 0) & ~under
def lap_at(x, z):
    """The lap's outside point nearest (x, z) -- not the ring's lanes under it -- and how far."""
    k = int(np.argmin(np.where(OUTSIDE, (X - x) ** 2 + (Z - z) ** 2, 1e9))); return k, math.hypot(X[k] - x, Z[k] - z)
P_HALF = np.full(PM, PIT_HALF)
PY = np.zeros(PM)
p_hidden = np.zeros(PM, bool)
for n in range(PM):
    k, d = lap_at(PX[n], PZ[n])
    if d + PIT_HALF <= HALF[k] + 0.3: p_hidden[n] = True
# (level with the lap where it leaves and where it comes back, 40 over the lap's deck while the two overlap, H_OUT along the gas between)
yF, yJ = Y[PL_F], Y[PL_J]
for n in range(PM):
    t = n / (PM - 1)
    PY[n] = yF + (yJ - yF) * ease(t)
    k, d = lap_at(PX[n], PZ[n])
    if not p_hidden[n] and d < HALF[k] + PIT_HALF + 0.5: PY[n] = Y[k] + 40
PIT_SPOT_LIST = []
for xs in (40.0, 35.5, 31.0):
    n = int(np.argmin(np.abs(PX - xs) + 100 * (np.abs(PZ - PIT_Z) > 0.2)))
    PIT_SPOT_LIST.append([round(float(PX[n]), 3), round(float(PZ[n]), 3), -1.0, 0.0, round(float(PY[n]), 1)])
pit_hid = [[int(a), int(b)] for a, b in zip(*[np.flatnonzero(np.diff(np.concatenate([[0], p_hidden.astype(int), [0]])) == v) - (0 if v == 1 else 1) for v in (1, -1)])]

# ---- the gas monsters: on the ring's floor beside its lanes, along the south side's inner lane, the east side, the north side and the
# outer lanes, each striking at its own place on the road
iS_in = nearest_after((25.0, 47.0), MARK['sw1'], 40); iS_in2 = nearest_after((46.0, 47.0), iS_in, 60)
iE = nearest_after((55.0, 38.0), iS_in2, 40); iE2 = nearest_after((55.0, 14.0), iE, 60)
iN = nearest_after((44.0, 4.5), iE2, 40); iN2 = nearest_after((14.0, 4.5), iN, 80)
iW2 = nearest_after((4.5, 18.0), iN2, 40); iW22 = nearest_after((4.5, 50.0), iW2, 80)
iS2 = nearest_after((14.0, 58.0), iW22, 40); iS22 = nearest_after((33.0, 58.0), iS2, 60)
# (fewer since 2026-10-15 later -- the user: "Let's reduce the number of gas monsters you've already added, and add some of the flying ones
# that fire fireballs": one on the south side's inner lane, one on the north side's, one on the west outer lane) (the south side's 2
# past its lane's rail since the lane is wider, 2026-10-17: see WIDTHS)
MONSTERS = [{'from': int(a), 'to': int(b), 'every': 9.0, 'out': o, 'most': m, 'floor': GAS_MONSTER_FLOOR}
            for a, b, m, o in ((iS_in, iS_in2, 1, 2.0), (iN, iN2, 1, 2.5), (iW2, iW22, 1, 2.5))]
# ---- the flying gas monsters (RaceTrackFlyers: the game's entity 329, spitting red fire balls): to and fro along the south side's inner
# lane, the east side's, the north side's and the west outer lane, 2,600 over the floor, beside the lane on the side away from the walls --
# between the lane and the control room or the next lane ('out': cells to the lane's left as it runs, minus to its right)
FLYERS = [{'from': int(a), 'to': int(b), 'out': o, 'height': 2600.0, 'floor': GAS_MONSTER_FLOOR}
          for a, b, o in ((iS_in, iS_in2, 5.5), (iE, iE2, -6.0), (iN, iN2, -6.0), (iW2, iW22, -3.5))]

# ---- checks
ride = np.ones(N, bool)
out = [f'lap {total:.1f} cells, {N} points; heights {Y.min():.0f} to {Y.max():.0f}; steepest {np.abs(grade).max() * 100:.1f} %']
for name, (a, b) in {'start to door A': (MARK['start'], MARK['doorA']), 'door A down to the ring': (MARK['doorA'], MARK['sw1']),
                     'round the ring': (MARK['sw1'], MARK['sw2']), 'up to door C': (MARK['sw2'], MARK['doorC']),
                     'door C round to the start': (MARK['doorC'], MARK['start'])}.items():
    gmax = max(abs(grade[k % N]) for k in range(a, a + int(ahead(a, b) / STEP)))
    out.append(f'  {name}: {ahead(a, b):.0f} cells, {Y[a]:.0f} to {Y[b]:.0f}, steepest {gmax * 100:.1f} %')
# (the tightest bends)
kap = np.abs(np.gradient(np.unwrap(np.arctan2(T[:, 1], T[:, 0]))) / STEP)
out.append(f'  tightest bend: radius {1 / kap.max():.1f} cells at ({X[kap.argmax()]:.1f}, {Z[kap.argmax()]:.1f})')
# (the lap over itself: where two parts of it are near each other, how far apart up and down)
idx = np.arange(N); worst = []
for i in range(0, N, 2):
    dd = np.hypot(X - X[i], Z - Z[i]); sep = np.minimum(np.abs(idx - i), N - np.abs(idx - i))
    near_ = (sep > 40) & (dd < HALF + HALF[i] + 0.6)
    if near_.any():
        j = int(np.argmin(np.where(near_, np.abs(Y - Y[i]), 1e9)))
        need = max(2600, max(tunnel_roof[i], tunnel_roof[j]) + TUN_SLAB + TUN_UNDER) if in_tunnel[i] or in_tunnel[j] else 2600
        worst.append((abs(Y[i] - Y[j]) - need, abs(Y[i] - Y[j]), i, j))
for m, dy, i, j in sorted(worst)[:5]:
    out.append(f'  {"CLASH" if m < 0 else "over itself"}: ({X[i]:.1f}, {Z[i]:.1f}) {Y[i]:.0f} and ({X[j]:.1f}, {Z[j]:.1f}) {Y[j]:.0f}: {dy:.0f} apart ({m:+.0f})')
# (in the ring: clear of its walls -- the control room, the outer walls -- and under its roof)
bad = []
for k in range(N):
    if Y[k] > ROOF - 1500 and not in_tunnel[k]: pass
    if Y[k] >= ROOF: continue
    for o in np.linspace(-HALF[k] - 0.3, HALF[k] + 0.3, 13):
        x, z = X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o
        i, j = int(z), int(x)
        if not (0 <= i < 64 and 0 <= j < 64): bad.append((k, 'outside the room')); break
        top = TOPW[i, j]
        if top > 0 and FLOOR - 256 + (top + 1) * 256 > Y[k] - DECK - 50: bad.append((k, f'wall {top} at ({j},{i})')); break
out.append(f'  in the ring, into its walls: {len(bad)} points' + (f' e.g. {[(X[k].round(1), Z[k].round(1), int(Y[k]), w) for k, w in bad[:4]]}' if bad else ''))
low_roof = [k for k in range(N) if Y[k] < ROOF and not in_tunnel[k] and Y[k] + 1700 > ROOF]
out.append(f'  under the ring\'s roof with less than 1,700 over the road (no tunnel): {len(low_roof)} points' + (f' e.g. ({X[low_roof[0]]:.1f}, {Z[low_roof[0]]:.1f}) {Y[low_roof[0]]:.0f}' if low_roof else ''))
# (the tunnels under the island: their roofs under the ground)
poke = []
for k in range(N):
    if not in_tunnel[k] or not under[k]: continue
    g = min(ground(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o) for o in np.linspace(-HALF[k] - 0.85, HALF[k] + 0.85, 9))
    top = Y[k] + tunnel_roof[k] + TUN_SLAB
    if tunnel_roof[k] > TUN_ROOF and g > Y[k]: top = min(top, g - 40)   # (underGround: kept under the ground over it)
    if top > g - 60 + 20: poke.append((top - g, k))
out.append(f'  tunnel roofs out of the ground: {len(poke)} points' + (f', the most {max(poke)[0]:.0f} at ({X[max(poke)[1]]:.1f}, {Z[max(poke)[1]]:.1f})' if poke else ''))
# (outside: the deck under the ground -- the builder cuts the ground there)
cutting = [(max(ground(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o) for o in np.linspace(-HALF[k], HALF[k], 7)) - Y[k], k) for k in range(N) if not under[k] and not in_tunnel[k]]
cutting = [c for c in cutting if c[0] > -300]
out.append(f'  outside, the ground within 300 of the deck (cut by the build): {len(cutting)} points' + (f', the most {max(cutting)[0]:+.0f} at ({X[max(cutting)[1]]:.1f}, {Z[max(cutting)[1]]:.1f})' if cutting else ''))
# (the corner towers: their boxes 8.15..16.35 and 47.65..55.85 each way, up to their cannons' tops)
tw = [(8.15, 16.35), (47.65, 55.85)]
th = [k for k in range(N) if Y[k] > 0 and any(any(a - HALF[k] - 0.3 < X[k] < b + HALF[k] + 0.3 and c - HALF[k] - 0.3 < Z[k] < d + HALF[k] + 0.3 for c, d in tw) for a, b in tw)]
out.append(f'  near a corner tower: {len(th)} points' + (f' e.g. ({X[th[0]]:.1f}, {Z[th[0]]:.1f})' if th else ''))
edge = min(min(X[k] - HALF[k], 64 - X[k] - HALF[k], Z[k] - HALF[k], 64 - Z[k] - HALF[k]) for k in range(N))
out.append(f'  the rail nearest the cube\'s edge: {edge:.2f} cells')
for m, k in MARK.items():
    out.append(f'    {m:9s} point {k:4d} ({X[k]:5.1f}, {Z[k]:5.1f}) {Y[k]:6.0f}  ground {ground(X[k], Z[k]):5.0f}{"  under" if under[k] else ""}{"  tunnel" if in_tunnel[k] else ""}')
out.append(f'  under: {UNDER_RUNS}; tunnels {[(t["from"], t["to"]) for t in TUNNELS]}')
out.append(f'  pit lane: {PM} points, {pit_len:.1f} cells, hidden {pit_hid}, spots {PIT_SPOT_LIST}, heights {PY.min():.0f}..{PY.max():.0f}')
out.append(f'  gas monster runs: {[(m["from"], m["to"]) for m in MONSTERS]}; flyers {[(f["from"], f["to"], f["out"]) for f in FLYERS]}')
print('\n'.join(out))

plan = {
    'originCellX': OX, 'originCellZ': OZ,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in zip(X, Z)],
    'heights': [round(float(y), 1) for y in Y],
    'maxGrade': round(float(np.abs(grade).max()) + 0.05, 3),
    'raised': [0, N - 1], 'raisedHalf': HALF_OUT, 'asphaltHalf': HALF_OUT - 1.0, 'curbHalf': HALF_OUT - 0.25,
    'raisedHalfs': [round(float(h), 3) for h in HALF],
    'lineHalfs': [round(float(h), 3) for h in LINE_HALF],
    'checkpointTweaks': CHECKPOINT_TWEAKS,
    'start': int(MARK['start']),
    'pitSpots': PIT_SPOT_LIST,
    'raisedCut': True,
    'edgeMargin': 4.0,                         # (the rails 0.5 from the cube's edges: the halls' outer lanes, the road round the towers)
    'keepBodies': [0, 1, 10, 11],
    'under': UNDER_RUNS,
    'tunnels': TUNNELS,
    'gasMonsters': MONSTERS,
    # (no steam jets in the ring since 2026-10-15 later -- the user: "you've added steam that fires underground, this needs removing")
    'flyers': FLYERS,
    'branches': [{
        # the pit lane: off the lap before the S-bend, back on at the trench's mouth
        'points': [[round(float(x), 3), round(float(z), 3)] for x, z in zip(PX, PZ)],
        'heights': [round(float(y), 1) for y in PY],
        'halfs': [round(float(h), 3) for h in P_HALF],
        'from': int(PL_F), 'to': int(PL_J),
        'hidden': pit_hid,
    }],
}
if '--write' in sys.argv:
    json.dump(plan, open(OUT, 'w'))
    print('written', OUT)
np.savez(D + 'lap.npz', X=X, Z=Z, Y=Y, HALF=HALF, under=under, tunnel=in_tunnel)

if '--pictures' in sys.argv:
    from PIL import Image, ImageDraw
    Zm = 16
    img = Image.open(D + 'plain/top.png').convert('RGB').resize((64 * Zm, 64 * Zm))
    d = ImageDraw.Draw(img)
    P = lambda x, z: (x * Zm, z * Zm)
    lo, hi = Y.min(), Y.max()
    for k in range(N):
        j = (k + 1) % N
        t = (Y[k] - lo) / (hi - lo)
        col = (int(255 * t), int(255 * (1 - abs(2 * t - 1))), int(255 * (1 - t)))
        for o in (-HALF[k], HALF[k]):
            d.line([P(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o), P(X[j] + Nn[j, 0] * o, Z[j] + Nn[j, 1] * o)], fill=col, width=2)
    for m, k in MARK.items(): d.text(P(X[k], Z[k]), m, fill=(255, 255, 255))
    for n in range(PM - 1):
        d.line([P(PX[n], PZ[n]), P(PX[n + 1], PZ[n + 1])], fill=(80, 80, 80) if p_hidden[n] else (255, 120, 0), width=3)
    img.save(D + 'lap.png')
