# The Emerald Moon (EMERAUDE): the lap from the user's sketch (2026-10-02) -- over the reactor in one jump, along the moon base's roof,
# through four vertical loops.
#
# The island is four cubes (cells 448-576 both ways): a crater in the middle (its floor 300-400 high) ringed by a rim 4,000-7,150 high,
# lower ground outside it to the island's edges. The moon base stands on the crater's floor -- a cross of tube-roofed buildings, the
# long arm north-south along x 57-64 (its roof 4,538 high at the middle), the short arm east-west along z 56-64 -- and the reactor
# on the north rim: a plateau 6,695 high (x 48-90), on it a cylinder 26 cells across up to 12,000 and a dish 17.3 cells across up to
# 18,715, its middle on the cubes' border (x 64, z 13). The lap, all of it a raised road (the inner part 5,000 high, the outer ring
# 7,700):
#   - the straight, south along the middle of the base's long arm on its roof, the start line on it, the pit lane beside it on the right
#     (west): a strip of road of its own (RaceTrackPlan.RaisedStrips), apart from the straight with clear space between them. Its way in
#     opens out of the corner onto the straight and its way out into the hairpin at the straight's end, each edge one straight line from
#     the corner's middle to the pit lane's (2026-10-12, the user: "smooth out the track coming into and out of the pit lane so the
#     entrance and exit are both wider and feed directly out of and into their respective corners", green on a picture of it);
#   - a hairpin at its south end, back north over the short arm, a U to the east climbing onto the east rim;
#   - south down the east rim through the first loop, the south side west through the second and the third, the west rim north through
#     the fourth;
#   - a wide banked turn onto the reactor's line, the road as wide as the reactor's dish (8.5 cells from its middle to its rail), and one
#     jump over the whole reactor: a curved ramp up to a lip, a flight over the dish, a curved landing hill down (RaceTrackPlan.ArcJumps:
#     the engine carries the car along it, over the cubes' border); a wide banked U-turn back west past the reactor's south side, down
#     onto the base's roof.
# The loops (2026-10-12, the user: "it should be a single width going into loop, and an offset single width coming out of the loop ...
# two loops going from left to right, and two going from right to left to keep everything balanced. Let's alternate them so right goes
# into left, then left goes into right"): the road its own width into each ring in one lane and out of it in the other, the ring twice
# the road's width, its band drifting across from the way in's lane to the way out's (RaceTrackLoopBody) -- the road steps sideways under
# it (RaceTrackPlan.Loops' sixth number: no deck there). In on the right and out on the left at the first and the third, the other way
# round at the second and the fourth: the road is back on its own line after every two. Until then the deck was twice the road's width
# for ten cells either side of each of three rings, all in on the left. The opponents race it (2026-10-03): they wait in the pit lane
# while the player qualifies.
#
# Run with E:\dump\TEMP\emer holding H.npy (the island's heights, H[z, x], cells 0..128 from 448) and obstacles.npz (the decor's tops
# and bottoms every quarter cell, `who` their body); --pictures draws the lap. Writes docs/racetrack/emerald_track_plan.json.
import json, math, sys
import numpy as np

D = 'E:/dump/TEMP/emer/'
OUT = 'E:/dump/LBAAssembler/docs/racetrack/emerald_track_plan.json'
ORIGIN = 448
H = np.load(D + 'H.npy').astype(float)
OB = np.load(D + 'obstacles.npz'); TOP, BOT, WHO, RQ = OB['top'], OB['bot'], OB['who'], int(OB['R'])

def ground(x, z):
    xi, zi = min(126, max(0, int(x))), min(126, max(0, int(z))); fx, fz = x - xi, z - zi
    return H[zi, xi] * (1 - fx) * (1 - fz) + H[zi, xi + 1] * fx * (1 - fz) + H[zi + 1, xi] * (1 - fx) * fz + H[zi + 1, xi + 1] * fx * fz

STEP = 0.5
H0, HJ = 4.5, 8.5                      # the deck's half width (to the rail): the usual (the ground tracks' curbs), the reactor's jump (its dish)
ASPHALT, CURB = H0 - 1.0, H0 - 0.25    # the asphalt's edge and the curb's (from them to the rail: the curb, a cell's three quarters, and the rail's top)
PIT_GAP, PIT_W = 2.5, 8.0              # the pit lane: the clear space between it and the straight, and its width rail to rail (cells; its asphalt 6)
INNER, OUTER = 5000.0, 7700.0          # the inner part's height (over the base's roof) and the outer ring's (over the west rim, 7,150)
SX = 60.5                              # the straight's middle line: the roof's middle
JZ = 13.0                              # the jump's line (through the reactor's middle)
# The loops: each ring's middle at its foot (cells), the lap's way there, the side the road comes in on (+1 the right of the way -- the
# builder's Across -- and out on the left; -1 the other way round), the ring's radius (cells) and the side the camera watches it from
# (1 the side the car goes in on: from the other its swing round from behind the car passed behind the ring's way down, and the car
# was hidden; the fourth's is past the island's edge, and the engine brings it nearer, onto the island -- RACEMOD.CPP LoopCamera).
LANE = H0                              # each lane's middle from the ring's middle: the road's half width, the ring's band as wide as the road
STEP_PAST = 0.5                        # the way in runs this far past the ring's foot, the way out starts this far short of it (cells)
LOOPS = [((108.5, 58.5), (0, 1), +1, 4.0, 1),       # the east rim, south: in on the right (west), out on the left
         ((74.0, 109.5), (-1, 0), -1, 4.0, 1),      # the south side, west: in on the left (south), out on the right ...
         ((48.0, 109.5), (-1, 0), +1, 4.0, 1),      # ... and back: in on the right (north), out on the left
         ((13.5, 45.0), (0, -1), -1, 4.0, 1)]       # the west rim, north: in on the left (west), out on the right
LOOP_RUN = 10.0                        # straight and level either side of a loop's foot (cells)

def jog(foot, way, side, R, camera):
    """a loop's two corners: the way in's lane a little past the foot, the way out's a little short of it"""
    h = np.array(way, float); h /= np.linalg.norm(h); n = np.array([-h[1], h[0]]); f = np.array(foot, float)
    return [(tuple(f + n * side * LANE + h * STEP_PAST), 0.0), (tuple(f - n * side * LANE - h * STEP_PAST), 0.0)]

# the corners in the lap's order, each (x, z) and the radius it is rounded with (0: a loop's step)
K = ([((SX, 92.0), 6.0),               # the straight's south end: the hairpin, east ...
      ((72.5, 92.0), 6.0),             # ... and back north
      ((72.5, 43.0), 5.5),             # north over the short arm, then east (the inner U), climbing
      ((104.0, 43.0), 5.0)]            # south down the east rim: the first loop
     + jog(*LOOPS[0]) +
     [((113.0, 76.0), 8.0),            # a kink south-west ...
      ((96.0, 93.0), 8.0),             # ... and south again
      ((96.0, 114.0), 9.0)]            # west along the south side: the second loop and the third
     + jog(*LOOPS[1]) + jog(*LOOPS[2]) +
     [((22.0, 114.0), 8.0),            # north ...
      ((22.0, 88.0), 7.0),             # ... north-west onto the west rim ...
      ((9.0, 74.0), 7.0)]              # ... and north through the fourth loop
     + jog(*LOOPS[3]) +
     [((18.0, JZ), 10.0),              # the wide banked turn east onto the reactor's line
      ((112.0, JZ), 10.0),             # past the reactor, the wide banked U-turn: south ...
      ((112.0, 33.0), 10.0),           # ... and west, past the reactor's south side, coming down (10 cells from the inner U: both 9 wide)
      ((SX, 33.0), 5.5)])              # south onto the base's roof: the straight
KICK = 6.0                             # the jump's ramp and landing hill: their length along the road (cells)
LIP_X, LAND_X = 39.0, 90.0             # the take-off lip and the landing lip
CLEAR = 900                            # the flight over the reactor's top, at the least (a car's height and some)
START_Z = 62.0                         # the start line on the straight (its grid behind it on the level, not the way down)
PIT_WAIT = (52.0, 55.5, 59.0)          # where the opponents wait in the pit lane while the player qualifies (z: before the line, in its cube)
# the pit lane: the clear space between it and the straight opening (z, from its tip to its whole width) and closing; its outer edge at
# its whole width from PIT_FULL[0] to PIT_FULL[1], straight from there to the corner's middle before it and the hairpin's after it
PIT_OPEN, PIT_CLOSE = (46.0, 50.0), (74.0, 79.0)
PIT_FULL = (47.0, 76.0)

def rounded_polyline(K):
    """the closed lap through corners K, each rounded with its radius (none: a sharp corner, a loop's step): points every 0.02 cells,
    and where each sharp corner is along them"""
    n = len(K); P = [np.array(p, float) for p, r in K]; out = []; sharp = []
    arcs = []
    for i in range(n):
        a, b, c = P[i - 1], P[i], P[(i + 1) % n]; r = K[i][1]
        u1 = (b - a) / np.linalg.norm(b - a); u2 = (c - b) / np.linalg.norm(c - b)
        turn = math.atan2(u1[0] * u2[1] - u1[1] * u2[0], u1 @ u2)
        t = r * math.tan(abs(turn) / 2)
        p1, p2 = b - u1 * t, b + u2 * t
        nrm = np.array([-u1[1], u1[0]]) * (1 if turn > 0 else -1)
        arcs.append((p1, p2, p1 + nrm * r, r, turn))
    for i in range(n):
        p1, p2, centre, r, turn = arcs[i]
        if r <= 0: sharp.append(len(out)); out.append(p1.copy())
        else:
            a0 = math.atan2(p1[1] - centre[1], p1[0] - centre[0])
            m = max(2, int(round(r * abs(turn) / 0.02)))
            for k in range(m): out.append(centre + r * np.array([math.cos(a0 + turn * k / m), math.sin(a0 + turn * k / m)]))
        q1, q2 = p2, arcs[(i + 1) % n][0]
        if np.dot(q2 - q1, P[(i + 1) % n] - P[i]) < -1e-9: sys.exit(f'corners {i} and {i + 1}: their roundings overlap')
        m = max(1, int(round(np.linalg.norm(q2 - q1) / 0.02)))
        for k in range(1 if r <= 0 else 0, m): out.append(q1 + (q2 - q1) * k / m)
    return np.array(out), sharp

fine, sharp = rounded_polyline(K)
k0 = int(np.argmin(np.hypot(fine[:, 0] - SX, fine[:, 1] - 40.0)))
fine = np.roll(fine, -k0, axis=0); sharp = [(s - k0) % len(fine) for s in sharp]
seg = np.linalg.norm(np.diff(np.vstack([fine, fine[:1]]), axis=0), axis=1)
cum = np.concatenate([[0], np.cumsum(seg)]); total = cum[-1]
N = int(round(total / STEP)); s = np.arange(N) * total / N
X = np.interp(s, cum, np.append(fine[:, 0], fine[0, 0])); Z = np.interp(s, cum, np.append(fine[:, 1], fine[0, 1]))
# the loops' steps: the way in's last point (at or a little past the foot), the way out's first (at or a little short of it), the points
# between them the step under the ring
sharpS = sorted(cum[k] for k in sharp)
pairs = list(zip(sharpS[0::2], sharpS[1::2]))
if len(sharpS) != 2 * len(LOOPS): sys.exit(f'{len(sharpS)} sharp corners for {len(LOOPS)} loops')
steps = []
jogged = np.zeros(N, bool)
for a, b in pairs:
    # (the points nearest the step's two corners put on them: the way in's last point, in its lane, and the way out's first -- the plan's
    # points are the builder's as they are, RaceTrackPlan.KeepPoints)
    i0, i1 = int(np.argmin(np.abs(s - a))), int(np.argmin(np.abs(s - b)))
    for i, at in ((i0, a), (i1, b)):
        X[i] = np.interp(at, cum, np.append(fine[:, 0], fine[0, 0])); Z[i] = np.interp(at, cum, np.append(fine[:, 1], fine[0, 1]))
    jogged[i0 + 1:i1] = True
    steps.append((i0, i1))
# the way the road runs, point by point: not across a step (the way in and the way out each their own, the step its own)
T = np.zeros((N, 2))
for k in range(N):
    a, b = (k - 1) % N, (k + 1) % N
    if jogged[a] != jogged[k]: a = k
    if jogged[b] != jogged[k]: b = k
    d = np.array([X[b] - X[a], Z[b] - Z[a]]); T[k] = d / np.linalg.norm(d)
Nn = np.stack([-T[:, 1], T[:, 0]], 1)                 # across the road (the builder's Across: west when heading south)
def nearest(p, heading=None):
    d = np.hypot(X - p[0], Z - p[1]) + 1000 * jogged
    if heading is not None: d = d + 100 * (T @ np.array(heading) < 0.7)
    return int(np.argmin(d))
def ahead(a, b): return ((b - a) % N) * STEP
def between(k, a, b): return ahead(a, k) <= ahead(a, b)
def ease(f): f = min(1, max(0, f)); return f * f * (3 - 2 * f)
def trap(f, q=0.22):
    """0..1 along a climb whose grade rises evenly over its first q, holds, and eases off over its last q"""
    f = min(1, max(0, f))
    if f < q: v = f * f / (2 * q)
    elif f <= 1 - q: v = q / 2 + (f - q)
    else: v = 1 - q - (1 - f) ** 2 / (2 * q)
    return min(1, v / (1 - q))
# the steps in LOOPS' order (each loop's step nearest its foot)
order = []
for foot, way, side, R, cam in LOOPS:
    order.append(min(range(len(steps)), key=lambda q: math.hypot((X[steps[q][0]] + X[steps[q][1]]) / 2 - foot[0], (Z[steps[q][0]] + Z[steps[q][1]]) / 2 - foot[1])))
steps = [steps[q] for q in order]

# ---- the jump: its ramp's foot, lip, landing lip and the landing hill's foot, and the flight between the lips -- a parabola, its slope at
# each lip the ramp's own there (a circle's arc, level at its foot); the gentlest ramp that clears the reactor
iFoot = nearest((LIP_X - KICK, JZ), (1, 0)); iLip = nearest((LIP_X, JZ), (1, 0))
iLand = nearest((LAND_X, JZ), (1, 0)); iLandFoot = nearest((LAND_X + KICK, JZ), (1, 0))
L = (LAND_X - LIP_X) * 512
prof = np.full(int(128 * RQ), -1.0)                    # the reactor's top along the jump's line, across the whole deck's width
for j in range(len(prof)):
    col = TOP[max(0, int((JZ - HJ) * RQ)):int((JZ + HJ) * RQ), j]
    if col.size: prof[j] = col.max()
def flight(theta, d):
    """the jump's height over the deck a horizontal distance d (units) from the ramp's foot"""
    k = KICK * 512; R = k / math.sin(theta); yl = R * (1 - math.cos(theta)); sl = math.tan(theta)
    if d <= 0: return 0
    if d <= k: return R - math.sqrt(max(0, R * R - d * d))
    if d <= k + L: u = d - k; return yl + sl * u * (1 - u / L)
    u = d - k - L
    return R - math.sqrt(max(0, R * R - (k - u) ** 2)) if u < k else 0
# (the ramp as built 2026-10-02: 60 degrees, the flight's top 20,780 -- the search below picked it from the island's objects as read then;
# read again since, a little differently, it picks 61. The jump is kept as it was; the search only says if it no longer clears the reactor.)
THETA_DEG = 60.0
theta = None
for deg in np.arange(40, 75.01, 0.5):
    th = math.radians(deg)
    if all(prof[j] < 0 or OUTER + flight(th, (j / RQ - (LIP_X - KICK)) * 512) >= prof[j] + CLEAR for j in range(int(LIP_X * RQ), int(LAND_X * RQ))):
        theta = th; break
if theta is None: sys.exit('no ramp clears the reactor')
if THETA_DEG is not None:
    if math.radians(THETA_DEG) < theta - 1e-9: print(f'WARNING: a ramp of {THETA_DEG} degrees clears the reactor by less than {CLEAR}: {math.degrees(theta):.1f} would')
    theta = math.radians(THETA_DEG)

# ---- heights: the inner part, the outer ring, the climb and the descent between them; the jump
iUp0 = nearest((72.5, 54.0), (0, -1)); iUp1 = nearest((104.0, 48.0), (0, 1))
iDown0 = nearest((88.0, 33.0), (-1, 0)); iDown1 = nearest((SX, 48.0), (0, 1))
Y = np.zeros(N)
for k in range(N):
    if between(k, iDown1, iUp0): Y[k] = INNER
    elif between(k, iUp0, iUp1): Y[k] = INNER + (OUTER - INNER) * trap(ahead(iUp0, k) / ahead(iUp0, iUp1))
    elif between(k, iUp1, iDown0): Y[k] = OUTER
    else: Y[k] = OUTER + (INNER - OUTER) * trap(ahead(iDown0, k) / ahead(iDown0, iDown1))
for k in range(N):
    if between(k, iFoot, iLandFoot): Y[k] = OUTER + flight(theta, ahead(iFoot, k) * 512)
gap = np.zeros(N, bool)
k = (iLip + 1) % N
while k != iLand: gap[k] = True; k = (k + 1) % N
gap |= jogged

# ---- widths: the usual; wide through the reactor's banked turns and the jump; the straight with its pit lane
def ramp(a, b, k): return ease(ahead(a, k) / ahead(a, b))
HALF = np.full(N, H0)
iWide0 = nearest((18.0, 32.0), (0, -1)); iWide1 = nearest((28.0, JZ), (1, 0))          # widening over the first banked turn
iWide2 = nearest((102.0, JZ), (1, 0)); iWide3 = nearest((112.0, 23.0), (0, 1))        # narrowing over the U-turn's first half (wide off the jump, back to the road's width before it runs beside the inner U)
for k in range(N):
    if between(k, iWide0, iWide1): HALF[k] = H0 + (HJ - H0) * ramp(iWide0, iWide1, k)
    elif between(k, iWide1, iWide2): HALF[k] = HJ
    elif between(k, iWide2, iWide3): HALF[k] = HJ + (H0 - HJ) * ramp(iWide2, iWide3, k)
# the pit lane: a strip from SLO to SHI across (west: +Across heading south), nothing where they are equal. Its outer edge: straight from
# the middle of the corner onto the straight (E0, on the road's own outer edge there) to its own edge at its whole width (E1), along that
# (to E2), and straight into the middle of the hairpin at the straight's end (E3, on the road's outer edge there)
PIT_OUT = SX - (H0 + PIT_GAP + PIT_W)
cIn, rIn = np.array([SX + 5.5, 33.0 + 5.5]), 5.5 + H0          # (the corner's middle and its outer edge's radius)
cOut, rOut = np.array([SX + 6.0, 92.0 - 6.0]), 6.0 + H0        # (the hairpin's first half's)
E = [cIn + np.array([-1, -1]) / math.sqrt(2) * rIn, np.array([PIT_OUT, PIT_FULL[0]]), np.array([PIT_OUT, PIT_FULL[1]]),
     cOut + np.array([-1, 1]) / math.sqrt(2) * rOut]
def to_edge(k):
    """how far across from point k (along Nn) the pit lane's outer edge is, or None"""
    p, d = np.array([X[k], Z[k]]), Nn[k]
    best = None
    for a, b in zip(E[:-1], E[1:]):
        e = b - a; m = np.array([[d[0], -e[0]], [d[1], -e[1]]])
        if abs(np.linalg.det(m)) < 1e-9: continue
        t, u = np.linalg.solve(m, a - p)
        if t > 0 and -1e-6 <= u <= 1 + 1e-6 and (best is None or t < best): best = t
    return best
SLO, SHI = np.zeros(N), np.zeros(N)
iPit0 = nearest((SX + 5.5 - 7.8, 33.0 - 2.0)); iPit3 = nearest((SX + 6.0 - 4.0, 92.0 + 4.0))   # (the corner's start and the hairpin's middle)
iPitO0, iPitO1 = nearest((SX, PIT_OPEN[0]), (0, 1)), nearest((SX, PIT_OPEN[1]), (0, 1))
iPitC0, iPitC1 = nearest((SX, PIT_CLOSE[0]), (0, 1)), nearest((SX, PIT_CLOSE[1]), (0, 1))
for k in range(N):
    if not between(k, iPit0, iPit3): continue
    hi = to_edge(k)
    if hi is None or hi <= H0 + 0.02: continue
    lo = H0
    if between(k, iPitO0, iPitO1): lo = H0 + PIT_GAP * ramp(iPitO0, iPitO1, k)
    elif between(k, iPitO1, iPitC0): lo = H0 + PIT_GAP
    elif between(k, iPitC0, iPitC1): lo = H0 + PIT_GAP * (1 - ramp(iPitC0, iPitC1, k))
    SLO[k], SHI[k] = lo, max(hi, lo + 0.01)

# ---- banking: the road leans into its bends, hard in the wide turns either side of the reactor's jump
kappa = np.zeros(N)
for k in range(N):
    a, b = (k - 2) % N, (k + 2) % N
    if jogged[a] or jogged[b] or jogged[k]: continue
    ta, tb = T[a], T[b]
    kappa[k] = math.atan2(ta[0] * tb[1] - ta[1] * tb[0], ta @ tb) / (4 * STEP)
def smooth(v, cells):
    w = max(1, int(round(cells / STEP))); pad = np.concatenate([v[-w:], v, v[:w]])
    return np.convolve(pad, np.ones(2 * w + 1) / (2 * w + 1), 'same')[w:-w]
gain = np.full(N, 1.6)
for k in range(N):
    if between(k, iWide0, iWide1) or between(k, iWide2, iWide3): gain[k] = 3.2         # the red turns: banked hard
bank = -np.clip(gain * smooth(kappa, 2.0), -0.42, 0.42)
def near_step(k, cells):
    """whether point k is within `cells` of a loop's step (along the lap)"""
    return any(ahead(k, i0) <= cells or ahead(i1, k) <= cells or between(k, i0, i1) for i0, i1 in steps)
for k in range(N):
    if near_step(k, LOOP_RUN): bank[k] = 0
for k in range(N):
    if between(k, (iFoot - 4) % N, (iLandFoot + 4) % N) or between(k, iPit0, iPit3): bank[k] = 0
bank = smooth(bank, 1.0)
for k in range(N):
    if between(k, iFoot, iLandFoot) or jogged[k]: bank[k] = 0

grade = (np.roll(Y, -1) - Y) / (STEP * 512)
iStart = nearest((SX, START_Z), (0, 1))
ride = np.array([not between(k, iFoot, iLandFoot) and not jogged[k] for k in range(N)])   # the road a car drives (not the jump the engine carries it over, nor a step)

# ---- checks
out = [f'lap {total:.1f} cells, {N} points; heights {Y[ride].min():.0f} to {Y[ride].max():.0f}; steepest driven {np.abs(grade[ride & np.roll(ride, -1)]).max() * 100:.1f} %; '
       f'banking up to {np.abs(bank).max():.2f}; widths {HALF.min():.2f} to {HALF.max():.2f}']
edge = np.where(gap, 99, np.minimum.reduce([X - HALF, Z - HALF, 128 - X - HALF, 128 - Z - HALF]))
out.append(f'  the rails nearest the island edge: {edge.min():.1f} cells (at ({X[np.argmin(edge)]:.1f}, {Z[np.argmin(edge)]:.1f}))')
# (across the road and the pit lane's strip beside it)
def across(k, n=9):
    a = list(np.linspace(-HALF[k], HALF[k], n))
    if SHI[k] > SLO[k]: a += list(np.linspace(SLO[k], SHI[k], n))
    return a
clear = sorted((Y[k] - max(ground(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o) for o in across(k)), k) for k in range(N) if not gap[k])
out.append('  the deck over the ground, least: ' + ', '.join(f'{c:.0f} at ({X[k]:.1f}, {Z[k]:.1f})' for c, k in clear[:3]))
hits = {}
for k in range(N):
    if gap[k]: continue
    for o in across(k, 17):
        x, z = X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o
        i, j = int(z * RQ), int(x * RQ)
        if not (0 <= i < TOP.shape[0] and 0 <= j < TOP.shape[1]) or TOP[i, j] < 0: continue
        y = Y[k] + o * 512 * bank[k]
        if TOP[i, j] > y - 200 - 150 and BOT[i, j] < y + 1400:
            b = int(WHO[i, j]); hits.setdefault(b, []).append((round(float(x), 1), round(float(z), 1), int(TOP[i, j]), int(y)))
for b, h in hits.items():
    out.append(f'  WARNING: decor body {b} in the road\'s space at {len(h)} places, e.g. {h[0]} (x, z, its top, the deck)')
idx = np.arange(N); close = []
for i in range(0, N, 2):
    if gap[i]: continue
    dd = np.hypot(X - X[i], Z - Z[i]); sep = np.minimum(np.abs(idx - i), N - np.abs(idx - i))
    nearby = (sep > 40) & (dd < HALF + HALF[i] + 0.6) & (np.abs(Y - Y[i]) < 1600) & ~gap
    if nearby.any(): close.append((dd[nearby].min(), i, int(np.argmax(nearby))))
for dmin, i, j in sorted(close)[:3]:
    out.append(f'  WARNING: two parts of the lap {dmin:.1f} cells apart at one level: ({X[i]:.1f}, {Z[i]:.1f}) {Y[i]:.0f} and ({X[j]:.1f}, {Z[j]:.1f}) {Y[j]:.0f}')
# each ring: in one cube, its runs straight and level, nothing else of the lap or the island's objects in its footprint (the band's reach
# across, its radius and thickness along, up to its top), and the camera's eye on the island
loops = []
for q, ((foot, way, side, R, cam), (i0, i1)) in enumerate(zip(LOOPS, steps)):
    fx, fz = (X[i0] + X[i1]) / 2, (Z[i0] + Z[i1]) / 2
    h = T[i0]; n = np.array([-h[1], h[0]])
    cx, cz = int(fx // 64), int(fz // 64)
    reach, wide = R + 0.75, 2 * LANE + 0.2
    corners = [(fx + h[0] * a * reach + n[0] * c * wide, fz + h[1] * a * reach + n[1] * c * wide) for a in (-1, 1) for c in (-1, 1)]
    same = all(int(x // 64) == cx and int(z // 64) == cz for x, z in corners)
    w = int(LOOP_RUN / STEP); run = [(i0 - j) % N for j in range(w)] + [(i1 + j) % N for j in range(w)]
    others = [k for k in range(N) if not gap[k] and not near_step(k, LOOP_RUN + 2) and abs(Y[k] - Y[i0]) < 2 * R * 512 + 1600
              and abs((X[k] - fx) * h[0] + (Z[k] - fz) * h[1]) < reach + HALF[k] and abs((X[k] - fx) * n[0] + (Z[k] - fz) * n[1]) < wide + HALF[k]]
    things = set()
    for zi in range(int((min(c[1] for c in corners)) * RQ), int((max(c[1] for c in corners)) * RQ) + 1):
        for xi in range(int((min(c[0] for c in corners)) * RQ), int((max(c[0] for c in corners)) * RQ) + 1):
            if not (0 <= zi < TOP.shape[0] and 0 <= xi < TOP.shape[1]) or TOP[zi, xi] < 0: continue
            x, z = xi / RQ, zi / RQ
            if abs((x - fx) * h[0] + (z - fz) * h[1]) > reach or abs((x - fx) * n[0] + (z - fz) * n[1]) > wide: continue
            if TOP[zi, xi] > Y[i0] - 200 and BOT[zi, xi] < Y[i0] + 2 * R * 512 + 400: things.add(int(WHO[zi, xi]))
    far = (R * 3.2 + LANE) * side * -1 * cam      # (the camera: across, from the ring's middle -- RACEMOD.CPP LoopCamera)
    ex, ez = fx + n[0] * -far, fz + n[1] * -far
    out.append(f'  loop {q + 1} at ({fx:.1f}, {fz:.1f}) {Y[i0]:.0f}: in on the {"right" if side > 0 else "left"}, cube ({cx + 7}, {cz + 7}) {"whole" if same else "ACROSS A CUBE EDGE"}, '
               f'its runs bend {max(abs(kappa[j]) for j in run):.4f} and rise {max(Y[run]) - min(Y[run]):.0f}; the step {ahead(i0, i1):.1f} cells (points {i0}..{i1}); '
               f'{"WARNING: the lap in its footprint at %d points, " % len(others) if others else ""}{"WARNING: decor bodies %s in it, " % sorted(things) if things else ""}'
               f'the camera at ({ex:.1f}, {ez:.1f}){" (off the island: the engine brings it nearer)" if not (0 <= ex < 128 and 0 <= ez < 128) else ""}')
    loops.append((i0, i1, R, -2 * LANE * side, cam))
yl = OUTER + flight(theta, KICK * 512)
out.append(f'  the jump: a ramp {KICK:.0f} cells up to {math.degrees(theta):.1f} degrees, lips ({X[iLip]:.1f}, {Z[iLip]:.1f}) -> ({X[iLand]:.1f}, {Z[iLand]:.1f}) at {yl:.0f}, '
           f'{(LAND_X - LIP_X):.0f} cells apart, the top {Y[gap & ~jogged].max():.0f} (the reactor {prof[prof > 0].max():.0f}); points {iFoot} {iLip} {iLand} {iLandFoot}')
pit = [k for k in range(N) if SHI[k] > SLO[k]]
out.append(f'  start line at point {iStart} ({X[iStart]:.1f}, {Z[iStart]:.1f}) at {Y[iStart]:.0f}; the pit lane points {pit[0]}..{pit[-1]} ({X[pit[0]]:.1f}, {Z[pit[0]]:.1f}) .. '
           f'({X[pit[-1]]:.1f}, {Z[pit[-1]]:.1f}), {PIT_GAP} cells of clear space from z {PIT_OPEN[0]:.0f} to {PIT_CLOSE[1]:.0f}, {PIT_W} wide; its waiting spots at x {SX - (SLO[iStart] + SHI[iStart]) / 2:.1f}')
print('\n'.join(out))

plan = {
    'originCellX': ORIGIN, 'originCellZ': ORIGIN,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in zip(X, Z)],
    'heights': [round(float(y), 1) for y in Y],
    'maxGrade': round(float(np.abs(grade).max()) + 0.05, 3),
    'raised': [0, N - 1], 'raisedHalf': H0, 'asphaltHalf': ASPHALT, 'curbHalf': CURB, 'keepPoints': True,
    'raisedHalfs': [round(float(h), 3) for h in HALF],
    'bank': [round(float(b), 4) for b in bank],
    # (each: the way in's last point, the ring's radius, its shift -- negative: in on the right -- no gap, its band as wide as the road,
    # the way out's first point, the camera's side)
    'loops': [[int(i0), R, sh, 0, H0, int(i1), cam] for i0, i1, R, sh, cam in loops],
    'arcJumps': [[int(iFoot), int(iLip), int(iLand), int(iLandFoot)]],
    'start': int(iStart),
    # the pit lane, a strip of road beside the straight: from and to across (the builder's Across, cells from the middle), point by point
    'raisedStrips': [[round(float(a), 3), round(float(b), 3)] for a, b in zip(SLO, SHI)],
    # (the opponents' waiting spots, in the pit lane's middle, facing down the straight, on its deck)
    'pitSpots': [[round(float(SX - (SLO[iStart] + SHI[iStart]) / 2), 3), z, 0.0, 1.0, INNER] for z in PIT_WAIT],
    'keepBodies': sorted(set(int(b) for b in np.unique(WHO[WHO >= 0]))),
}
json.dump(plan, open(OUT, 'w'))
np.savez(D + 'lap.npz', X=X, Z=Z, Y=Y, T=T, Nn=Nn, HALF=HALF, bank=bank, gap=gap, SLO=SLO, SHI=SHI, jogged=jogged)
print('wrote', OUT)

if '--pictures' in sys.argv:
    from PIL import Image, ImageDraw
    S = 8
    import os
    base = D + 'heightmap.png' if os.path.exists(D + 'heightmap.png') else D + 'map4.png'
    im = Image.open(base).convert('RGB'); im = im.resize((128 * S, 128 * S)); d = ImageDraw.Draw(im)
    def px(x, z): return (x * S, z * S)
    for i in range(N):
        if gap[i]: continue
        a = px(X[i] + Nn[i, 0] * HALF[i], Z[i] + Nn[i, 1] * HALF[i]); b = px(X[i] - Nn[i, 0] * HALF[i], Z[i] - Nn[i, 1] * HALF[i])
        t = min(1, max(0, (Y[i] - INNER) / (OUTER - INNER)))
        d.line([a, b], fill=(int(80 + 170 * t), int(200 - 80 * t), 60), width=2)
    for i in range(N):
        if gap[i] and not jogged[i]: d.point(px(X[i], Z[i]), fill=(255, 255, 0))
    for (i0, i1, R, sh, cam), (foot, way, side, _, _) in zip(loops, LOOPS):
        fx, fz = (X[i0] + X[i1]) / 2, (Z[i0] + Z[i1]) / 2; h = T[i0]; n = np.array([-h[1], h[0]])
        reach, wide = R + 0.75, 2 * LANE + 0.2
        poly = [px(fx + h[0] * a * reach + n[0] * c * wide, fz + h[1] * a * reach + n[1] * c * wide) for a, c in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        d.polygon(poly, outline=(40, 160, 255))
        d.line([px(X[i0], Z[i0]), px(X[i1], Z[i1])], fill=(40, 160, 255), width=4)
    a = px(X[iStart] + Nn[iStart, 0] * HALF[iStart], Z[iStart] + Nn[iStart, 1] * HALF[iStart]); b = px(X[iStart] - Nn[iStart, 0] * HALF[iStart], Z[iStart] - Nn[iStart, 1] * HALF[iStart])
    d.line([a, b], fill=(255, 255, 255), width=4)
    for k in range(N):
        if SHI[k] > SLO[k]:
            a = px(X[k] + Nn[k, 0] * SLO[k], Z[k] + Nn[k, 1] * SLO[k]); b = px(X[k] + Nn[k, 0] * SHI[k], Z[k] + Nn[k, 1] * SHI[k])
            d.line([a, b], fill=(255, 160, 40), width=2)
    for i in range(0, N, 30): d.text(px(X[i] + 0.8, Z[i] + 0.8), f'{int(Y[i])}', fill=(255, 255, 255))
    im.save(D + 'lap.png')
    # the jump from the side
    import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
    fig, ax = plt.subplots(figsize=(11, 4.2), dpi=100)
    xs = np.arange(len(prof)) / RQ
    ax.fill_between(xs, 0, np.where(prof > 0, prof, np.nan), color='#c9a227', alpha=0.6, label='the reactor and the base (their tops, across the deck\'s width)')
    gx = np.arange(0, 127, 0.25); ax.fill_between(gx, 0, [max(ground(x, JZ + o) for o in (-HJ, 0, HJ)) for x in gx], color='#8a8f99', alpha=0.6, label='the ground')
    jk = [k for k in range(N) if between(k, (iFoot - 20) % N, (iLandFoot + 20) % N)]
    ax.plot(X[jk], Y[jk], color='#1b6ec2', lw=2.5, label='the car\'s way: the ramp, the flight, the landing hill')
    road = [k for k in jk if not gap[k]]
    ax.plot(X[road], Y[road], '.', color='#222', ms=3, label='the road (deck points)')
    ax.set_xlim(20, 110); ax.set_ylim(0, 23000); ax.set_xlabel('x (cells, east)'); ax.set_ylabel('height'); ax.legend(loc='upper right', fontsize=8)
    ax.set_title('Over the reactor: along z %.0f' % JZ); fig.tight_layout(); fig.savefig(D + 'jump_side.png')
    print('pictures', D + 'lap.png', D + 'jump_side.png')
