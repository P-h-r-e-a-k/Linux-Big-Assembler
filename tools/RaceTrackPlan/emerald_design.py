# The Emerald Moon (EMERAUDE): the lap from the user's sketch (2026-10-02) -- over the reactor in one jump, along the moon base's roof,
# through three vertical loops.
#
# The island is four cubes (cells 448-576 both ways): a crater in the middle (its floor 300-400 high) ringed by a rim 4,000-7,150 high,
# lower ground outside it to the island's edges. The moon base stands on the crater's floor -- a cross of tube-roofed buildings, the
# long arm north-south along x 57-64 (its roof 4,538 high at the middle), the short arm east-west along z 56-64 -- and the reactor
# on the north rim: a plateau 6,695 high (x 48-90), on it a cylinder 26 cells across up to 12,000 and a dish 17.3 cells across up to
# 18,715, its middle on the cubes' border (x 64, z 13). The lap, all of it a raised road (the inner part 5,000 high, the outer ring
# 7,700):
#   - the straight, south along the middle of the base's long arm on its roof, the pit lane beside it on the right (west): one deck,
#     wider there, a white stripe between the lanes; the start line on it;
#   - a hairpin at its south end, back north over the short arm, a U to the east climbing onto the east rim;
#   - south down the east rim through the first loop (whole), the south side west through the second (a gap at its top), the west rim
#     north through the third (whole);
#   - a wide banked turn onto the reactor's line, the road as wide as the reactor's dish (8.5 cells from its middle to its rail), and one
#     jump over the whole reactor: a curved ramp up to a lip, a flight over the dish, a curved landing hill down (RaceTrackPlan.ArcJumps:
#     the engine carries the car along it, over the cubes' border); a wide banked U-turn back west past the reactor's south side, down
#     onto the base's roof.
# The loops: the deck twice the track's width there, the ring as wide as the track drifting across it (the car goes in on one half and
# comes out on the other, RaceTrackLoopBody). The opponents race it (2026-10-03): they wait in the pit lane while the player qualifies.
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
H0, HJ, HL = 4.5, 8.5, 9.0             # the deck's half width (to the rail): the usual (the ground tracks' curbs), the reactor's jump (its dish), at a loop (twice)
ASPHALT, CURB = H0 - 1.0, H0 - 0.25    # the asphalt's edge and the curb's (from them to the rail: the curb, a cell's three quarters, and the rail's top)
PIT = 4.5                              # the pit lane's width beside the straight (cells), past a fence
INNER, OUTER = 5000.0, 7700.0          # the inner part's height (over the base's roof) and the outer ring's (over the west rim, 7,150)
SX = 60.5 - PIT / 2                    # the straight's middle line (the race lanes' middle stays on the roof's middle, x 60.5)
JZ = 13.0                              # the jump's line (through the reactor's middle)
# the corners in the lap's order, each (x, z) and the radius it is rounded with
K = [((SX, 92.0), 6.0),               # the straight's south end: the hairpin, east ...
     ((72.5, 92.0), 6.0),             # ... and back north
     ((72.5, 43.0), 5.5),             # north over the short arm, then east (the inner U), climbing
     ((104.0, 43.0), 5.0),            # south down the east rim: the first loop
     ((104.0, 74.0), 8.0),            # a kink south-west ...
     ((92.0, 86.0), 8.0),             # ... and south again
     ((92.0, 114.0), 9.0),            # west along the south side: the second loop
     ((24.0, 114.0), 8.0),            # north ...
     ((24.0, 88.0), 7.0),             # an S on the west rim ...
     ((33.0, 77.0), 7.0),
     ((18.0, 67.0), 7.0),             # ... north through the third loop
     ((18.0, JZ), 10.0),              # the wide banked turn east onto the reactor's line
     ((112.0, JZ), 10.0),             # past the reactor, the wide banked U-turn: south ...
     ((112.0, 33.0), 10.0),           # ... and west, past the reactor's south side, coming down (10 cells from the inner U: both 9 wide)
     ((SX, 33.0), 5.5)]               # south onto the base's roof: the straight
LOOPS = [((104.0, 59.0), 4.0, 0.0), ((54.0, 114.0), 4.0, 35.0), ((18.0, 54.5), 4.0, 0.0)]   # foot, radius, gap (degrees: 35 is leapt from 22 km/h to 43)
LOOP_RUN, LOOP_EASE = 10.0, 8.0        # straight and level either side of a loop's foot, then the deck narrowing back (cells)
KICK = 6.0                             # the jump's ramp and landing hill: their length along the road (cells)
LIP_X, LAND_X = 39.0, 90.0             # the take-off lip and the landing lip
CLEAR = 900                            # the flight over the reactor's top, at the least (a car's height and some)
START_Z = 62.0                         # the start line on the straight (its grid behind it on the level, not the way down)
PIT_WAIT = (49.0, 53.0, 57.0)          # where the opponents wait in the pit lane while the player qualifies (z: before the line, in its cube)

def rounded_polyline(K):
    """the closed lap through corners K, each rounded with its radius: points every 0.02 cells"""
    n = len(K); P = [np.array(p, float) for p, r in K]; out = []
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
        a0 = math.atan2(p1[1] - centre[1], p1[0] - centre[0])
        m = max(2, int(round(r * abs(turn) / 0.02)))
        for k in range(m): out.append(centre + r * np.array([math.cos(a0 + turn * k / m), math.sin(a0 + turn * k / m)]))
        q1, q2 = p2, arcs[(i + 1) % n][0]
        if np.dot(q2 - q1, P[(i + 1) % n] - P[i]) < -1e-9: sys.exit(f'corners {i} and {i + 1}: their roundings overlap')
        m = max(1, int(round(np.linalg.norm(q2 - q1) / 0.02)))
        for k in range(m): out.append(q1 + (q2 - q1) * k / m)
    return np.array(out)

fine = rounded_polyline(K)
k0 = int(np.argmin(np.hypot(fine[:, 0] - SX, fine[:, 1] - 40.0)))
fine = np.roll(fine, -k0, axis=0)
seg = np.linalg.norm(np.diff(np.vstack([fine, fine[:1]]), axis=0), axis=1)
cum = np.concatenate([[0], np.cumsum(seg)]); total = cum[-1]
N = int(round(total / STEP)); s = np.arange(N) * total / N
X = np.interp(s, cum, np.append(fine[:, 0], fine[0, 0])); Z = np.interp(s, cum, np.append(fine[:, 1], fine[0, 1]))
T = np.stack([np.gradient(X), np.gradient(Z)], 1); T /= np.linalg.norm(T, axis=1)[:, None]
Nn = np.stack([-T[:, 1], T[:, 0]], 1)                 # across the road (the builder's Across: west when heading south)
def nearest(p, heading=None):
    d = np.hypot(X - p[0], Z - p[1])
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
theta = None
for deg in np.arange(40, 75.01, 0.5):
    th = math.radians(deg)
    if all(prof[j] < 0 or OUTER + flight(th, (j / RQ - (LIP_X - KICK)) * 512) >= prof[j] + CLEAR for j in range(int(LIP_X * RQ), int(LAND_X * RQ))):
        theta = th; break
if theta is None: sys.exit('no ramp clears the reactor')

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
loops = [(nearest(foot), R, gp) for foot, R, gp in LOOPS]

# ---- widths: the usual; wide through the reactor's banked turns and the jump; double at the loops; the straight with its pit lane
def ramp(a, b, k): return ease(ahead(a, k) / ahead(a, b))
HALF = np.full(N, H0)
iWide0 = nearest((18.0, 32.0), (0, -1)); iWide1 = nearest((28.0, JZ), (1, 0))          # widening over the first banked turn
iWide2 = nearest((102.0, JZ), (1, 0)); iWide3 = nearest((112.0, 23.0), (0, 1))        # narrowing over the U-turn's first half (wide off the jump, back to the road's width before it runs beside the inner U)
for k in range(N):
    if between(k, iWide0, iWide1): HALF[k] = H0 + (HJ - H0) * ramp(iWide0, iWide1, k)
    elif between(k, iWide1, iWide2): HALF[k] = HJ
    elif between(k, iWide2, iWide3): HALF[k] = HJ + (H0 - HJ) * ramp(iWide2, iWide3, k)
for c, R, gp in loops:
    for k in range(N):
        d = min(ahead(c, k), ahead(k, c))
        if d <= LOOP_RUN: HALF[k] = max(HALF[k], HL)
        elif d <= LOOP_RUN + LOOP_EASE: HALF[k] = max(HALF[k], H0 + (HL - H0) * ease((LOOP_RUN + LOOP_EASE - d) / LOOP_EASE))
PIT_HALF = H0 + PIT / 2
iPit0 = nearest((SX, 40.0), (0, 1)); iPit1 = nearest((SX, 46.0), (0, 1)); iPit2 = nearest((SX, 80.0), (0, 1)); iPit3 = nearest((SX, 86.0), (0, 1))
for k in range(N):
    if between(k, iPit0, iPit1): HALF[k] = max(HALF[k], H0 + (PIT_HALF - H0) * ramp(iPit0, iPit1, k))
    elif between(k, iPit1, iPit2): HALF[k] = PIT_HALF
    elif between(k, iPit2, iPit3): HALF[k] = max(HALF[k], PIT_HALF + (H0 - PIT_HALF) * ramp(iPit2, iPit3, k))

# ---- banking: the road leans into its bends, hard in the wide turns either side of the reactor's jump
kappa = np.zeros(N)
for k in range(N):
    a, b = T[(k - 2) % N], T[(k + 2) % N]
    kappa[k] = math.atan2(a[0] * b[1] - a[1] * b[0], a @ b) / (4 * STEP)
def smooth(v, cells):
    w = max(1, int(round(cells / STEP))); pad = np.concatenate([v[-w:], v, v[:w]])
    return np.convolve(pad, np.ones(2 * w + 1) / (2 * w + 1), 'same')[w:-w]
gain = np.full(N, 1.6)
for k in range(N):
    if between(k, iWide0, iWide1) or between(k, iWide2, iWide3): gain[k] = 3.2         # the red turns: banked hard
bank = -np.clip(gain * smooth(kappa, 2.0), -0.42, 0.42)
for c, R, gp in loops:
    for k in range(N):
        if min(ahead(c, k), ahead(k, c)) <= LOOP_RUN: bank[k] = 0
for k in range(N):
    if between(k, (iFoot - 4) % N, (iLandFoot + 4) % N) or between(k, iPit0, iPit3): bank[k] = 0
bank = smooth(bank, 1.0)
for k in range(N):
    if between(k, iFoot, iLandFoot): bank[k] = 0

grade = (np.roll(Y, -1) - Y) / (STEP * 512)
iStart = nearest((SX, START_Z), (0, 1))
ride = np.array([not between(k, iFoot, iLandFoot) for k in range(N)])   # the road a car drives (not the jump the engine carries it over)

# ---- checks
out = [f'lap {total:.1f} cells, {N} points; heights {Y[ride].min():.0f} to {Y[ride].max():.0f}; steepest driven {np.abs(grade[ride]).max() * 100:.1f} %; '
       f'banking up to {np.abs(bank).max():.2f}; widths {HALF.min():.2f} to {HALF.max():.2f}']
edge = np.minimum.reduce([X - HALF, Z - HALF, 128 - X - HALF, 128 - Z - HALF])
out.append(f'  the rails nearest the island edge: {edge.min():.1f} cells (at ({X[np.argmin(edge)]:.1f}, {Z[np.argmin(edge)]:.1f}))')
clear = sorted((Y[k] - max(ground(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o) for o in np.linspace(-HALF[k], HALF[k], 9)), k) for k in range(N) if not gap[k])
out.append('  the deck over the ground, least: ' + ', '.join(f'{c:.0f} at ({X[k]:.1f}, {Z[k]:.1f})' for c, k in clear[:3]))
hits = {}
for k in range(N):
    if gap[k]: continue
    for o in np.linspace(-HALF[k] - 0.3, HALF[k] + 0.3, 17):
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
for c, R, gp in loops:
    cx, cz = int(X[c] // 64), int(Z[c] // 64)
    ends = [(X[c] + T[c, 0] * a * (R + 0.75), Z[c] + T[c, 1] * a * (R + 0.75)) for a in (-1, 1)]   # (the ring: its radius, its band's thickness and its legs)
    same = all(int(x // 64) == cx and int(z // 64) == cz for x, z in ends)
    w = int(LOOP_RUN / STEP); run = [(c + j) % N for j in range(-w, w + 1)]
    out.append(f'  loop at ({X[c]:.1f}, {Z[c]:.1f}) {Y[c]:.0f}: cube ({cx + 7}, {cz + 7}) {"whole" if same else "ACROSS A CUBE EDGE"}, its run bends {max(abs(kappa[j]) for j in run):.4f}, '
               f'rises {max(Y[run]) - min(Y[run]):.0f}, {"a gap of %d degrees" % gp if gp else "whole"}')
yl = OUTER + flight(theta, KICK * 512)
out.append(f'  the jump: a ramp {KICK:.0f} cells up to {math.degrees(theta):.1f} degrees, lips ({X[iLip]:.1f}, {Z[iLip]:.1f}) -> ({X[iLand]:.1f}, {Z[iLand]:.1f}) at {yl:.0f}, '
           f'{(LAND_X - LIP_X):.0f} cells apart, the top {Y[gap].max():.0f} (the reactor {prof[prof > 0].max():.0f}); points {iFoot} {iLip} {iLand} {iLandFoot}')
out.append(f'  start line at point {iStart} ({X[iStart]:.1f}, {Z[iStart]:.1f}) at {Y[iStart]:.0f}; pit lane from point {iPit1} to {iPit2}')
print('\n'.join(out))

plan = {
    'originCellX': ORIGIN, 'originCellZ': ORIGIN,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in zip(X, Z)],
    'heights': [round(float(y), 1) for y in Y],
    'maxGrade': round(float(np.abs(grade).max()) + 0.05, 3),
    'raised': [0, N - 1], 'raisedHalf': H0, 'asphaltHalf': ASPHALT, 'curbHalf': CURB,
    'raisedHalfs': [round(float(h), 3) for h in HALF],
    'bank': [round(float(b), 4) for b in bank],
    'loops': [[int(c), R, 2 * H0, gp, H0] for c, R, gp in loops],
    'arcJumps': [[int(iFoot), int(iLip), int(iLand), int(iLandFoot)]],
    'start': int(iStart),
    # the stripe between the pit lane and the race lanes: across the road (the builder's Across, cells from the middle), first and last point
    'pitStripe': [int(iPit1), int(iPit2), round(float(PIT_HALF - PIT), 3)],
    # (the opponents' waiting spots, in the pit lane's middle, facing down the straight, on the deck)
    'pitSpots': [[round(SX - (PIT_HALF - PIT / 2), 3), z, 0.0, 1.0, INNER] for z in PIT_WAIT],
    # (a fence along the stripe, Citadel Island's white one; the grid on the race lanes, whose middle is half the pit lane's width over)
    'pitFence': True,
    'gridShift': -PIT / 2,
    'keepBodies': sorted(set(int(b) for b in np.unique(WHO[WHO >= 0]))),
}
json.dump(plan, open(OUT, 'w'))
np.savez(D + 'lap.npz', X=X, Z=Z, Y=Y, T=T, Nn=Nn, HALF=HALF, bank=bank, gap=gap)
print('wrote', OUT)

if '--pictures' in sys.argv:
    from PIL import Image, ImageDraw
    S = 8
    im = Image.open(D + 'heightmap.png').convert('RGB'); d = ImageDraw.Draw(im)
    def px(x, z): return (x * S, z * S)
    for i in range(N):
        if gap[i]: continue
        a = px(X[i] + Nn[i, 0] * HALF[i], Z[i] + Nn[i, 1] * HALF[i]); b = px(X[i] - Nn[i, 0] * HALF[i], Z[i] - Nn[i, 1] * HALF[i])
        t = min(1, max(0, (Y[i] - INNER) / (OUTER - INNER)))
        d.line([a, b], fill=(int(80 + 170 * t), int(200 - 80 * t), 60), width=2)
    for i in range(N):
        if gap[i]: d.point(px(X[i], Z[i]), fill=(255, 255, 0))
    for c, R, gp in loops:
        a = px(X[c] + Nn[c, 0] * HL, Z[c] + Nn[c, 1] * HL); b = px(X[c] - Nn[c, 0] * HL, Z[c] - Nn[c, 1] * HL)
        d.line([a, b], fill=(40, 160, 255), width=6)
    a = px(X[iStart] + Nn[iStart, 0] * HALF[iStart], Z[iStart] + Nn[iStart, 1] * HALF[iStart]); b = px(X[iStart] - Nn[iStart, 0] * HALF[iStart], Z[iStart] - Nn[iStart, 1] * HALF[iStart])
    d.line([a, b], fill=(255, 255, 255), width=4)
    for k in range(iPit1, iPit2):
        o = plan['pitStripe'][2]; d.point(px(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o), fill=(255, 255, 255))
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
