# Celebration Island before the statue (CELEBRAT): the lava lake lap, round the whole island. (2026-10-01, third version: the first, a
# figure of eight on the plateau, was 92 cells and too small; the second went round the whole island but its last corner was a tight
# hairpin and it ended in a drop off the mesa. The user's sketch over the second: the corners onto the lake's causeway turned back more on
# themselves into the lake's jump, the inner part of the lap shifted over, the last turn opened up, and the drop a ramp instead.)
#
# The island is one cube: a dock along its west side (x 4-10, z 6-33, 420 high), a mesa over the rest of it -- a plateau 5,700-6,500 high
# round a lake of lava level with it (x 26-44, z 24-41), the temple on its west rim (its roofs 11,140 high: nothing goes over it), two lava
# channels cutting its north side down to the sea, steep slopes all round down to the sea. The lap, clockwise on the map, all of it a
# raised road on piers but the dock:
#   - the dock, heading north: the start line, the grid behind it;
#   - round onto the north shore, a low causeway over the sea in front of the lava falls: the first jump, over a gap in it;
#   - the long climb, 900 to 6,300: along the shore, round the north-east corner, down the east coast and round onto the south rim;
#   - a hook back on itself, 120 degrees, onto a causeway north-east across the lava lake: the second jump, over a gap in its middle;
#   - round to the west onto the north rim, round its corner and south down the west cliffs beside the temple: the third jump;
#   - the last turn, a question mark: a kink left, then round to the right over the south-west corner, wide, and back north down a
#     ramp -- 6,300 to the dock's 420 -- through a cutting in the south-west hill onto the dock.
# The lake stays lava (a raised road leaves the ground as it is). Its jumps are gaps in the raised road (no road and no floor there).
#
# The lap is drawn as circles it turns round, joined by the lines that touch them (each circle: its middle, radius, and which way the
# lap goes round it), so a turn can go round more than a half circle (the last one does).
#
# Run with E:\dump\TEMP\celebrat holding H.npy and code.npy (the untouched island's heights and game codes). Writes
# docs/racetrack/celebrat_track_plan.json; --pictures draws the lap over the island.
import json, math, os, sys
import numpy as np

D = 'E:/dump/TEMP/celebrat/'
OUT = 'E:/dump/LBAAssembler/docs/racetrack/celebrat_track_plan.json'
ORIGIN = 448
H = np.load(D + 'H.npy').astype(float)    # H[z, x], cells 0..64
CODE = np.load(D + 'code.npy')

def ground(x, z):
    xi, zi = min(63, max(0, int(x))), min(63, max(0, int(z))); fx, fz = x - xi, z - zi
    return (H[zi, xi] * (1 - fx) * (1 - fz) + H[zi, xi + 1] * fx * (1 - fz) + H[zi + 1, xi] * (1 - fx) * fz + H[zi + 1, xi + 1] * fx * fz)
def lava(x, z): return CODE[min(63, max(0, int(z))), min(63, max(0, int(x)))] in (9, 13)

# ---- the road: the dock's ground road a little narrower than the retail one, the raised road's rails RAISED_HALF from its middle
ASPHALT, CURB, VERGE, BLEND = 2.3, 2.8, 3.8, 2.0
RAISED_HALF = 3.05
STEP = 0.5
# the circles the lap turns round, in its order from the dock's north end: (x, z) middle, radius, +1 the lap goes round it clockwise on the
# map (as round the island), -1 the other way
C = [((10.7, 11.0), 3.5, +1),     # the dock's north end: onto the north shore
     ((50.5, 13.5), 6.0, +1),     # the north-east corner: onto the east coast
     ((51.0, 44.5), 6.0, +1),     # the south-east corner: onto the south rim
     ((37.0, 45.0), 5.5, +1),     # the hook, 120 degrees, back north-east onto the lake's causeway
     ((37.4, 22.2), 5.5, -1),     # off the causeway, round to the west onto the north rim
     ((19.6, 22.7), 6.0, -1),     # the north rim's west corner: south down the west cliffs, beside the temple
     ((16.6, 42.0), 3.0, -1),     # the last turn: a kink left ...
     ((10.4, 51.5), 5.8, +1)]     # ... and round to the right, wide, back north onto the ramp down to the dock, along the island's west
                                  # shore (x 4.6: the south-west hill is lowest there; the island's edge is 4.3 cells off at the least)
START_Z = 13.5                     # the start line, on the dock heading north
FIRST = (7.2, 20.0)                # the lap's first point (on the dock, between the start line and the ramp's foot)

# heights along the lap's stretches (set by where a point is), then smoothed and kept level over each jump
DOCK = 420.0
SHORE = 900.0                      # the north shore's causeway
TOP = 6300.0                       # the mesa: the south rim, the lake's causeway, the north rim, the west cliffs
# the jumps: (name, the gap's middle, its length in cells -- the second version's): the gap is the road's own straight there, its lips
# half that either side
JUMPS = [('north shore', (34.5, 7.5), 7.0), ('lake', (37.2, 33.6), 8.0), ('temple', (13.6, 32.2), 7.0)]
RAMP, LANDING = 5.0, 5.0           # the ramps' lengths either side of a gap (RaceTrackOptions.JumpRampLength, JumpLandingLength)

def circles(C):
    """The lap round circles C: on each an arc, between them the line touching both (math frame x, z: +1 is counter-clockwise there,
    which the map, z down, shows clockwise)."""
    n = len(C); legs = []
    for i in range(n):
        (c1, r1, d1), (c2, r2, d2) = C[i], C[(i + 1) % n]
        c1, c2 = np.array(c1, float), np.array(c2, float); D = c2 - c1; dist = np.linalg.norm(D)
        rho1, rho2 = d1 * r1, d2 * r2
        if abs(rho2 - rho1) >= dist: sys.exit(f'circles {i} and {(i + 1) % n}: no line touches both')
        al = math.atan2(D[1], D[0]) - math.asin((rho2 - rho1) / dist)
        u = np.array([math.cos(al), math.sin(al)]); L = np.array([-u[1], u[0]])
        legs.append((c1 - rho1 * L, c2 - rho2 * L))
    pts = []
    for i in range(n):
        c, r, d = C[i]; c = np.array(c, float)
        p_in, p_out = legs[i - 1][1], legs[i][0]
        a0 = math.atan2(p_in[1] - c[1], p_in[0] - c[0]); a1 = math.atan2(p_out[1] - c[1], p_out[0] - c[0])
        sweep = (a1 - a0) % (2 * math.pi) if d > 0 else -((a0 - a1) % (2 * math.pi))
        m = max(2, int(round(r * abs(sweep) / 0.02)))
        pts += [c + r * np.array([math.cos(a0 + sweep * k / m), math.sin(a0 + sweep * k / m)]) for k in range(m)]
        q1, q2 = legs[i]
        m = max(1, int(round(np.linalg.norm(q2 - q1) / 0.02)))
        pts += [q1 + (q2 - q1) * k / m for k in range(m)]
        print(f'  circle {i}: turns {math.degrees(sweep):+.0f} degrees; then a line {np.linalg.norm(q2 - q1):.1f} cells')
    return np.array(pts)

fine = circles(C)
# (the lap starts at FIRST: the fine line rolled round to its nearest point)
k0 = int(np.argmin(np.hypot(fine[:, 0] - FIRST[0], fine[:, 1] - FIRST[1])))
fine = np.roll(fine, -k0, axis=0)
seg = np.linalg.norm(np.diff(np.vstack([fine, fine[:1]]), axis=0), axis=1)
cum = np.concatenate([[0], np.cumsum(seg)]); total = cum[-1]
N = int(round(total / STEP)); s = np.arange(N) * total / N
X = np.interp(s, cum, np.append(fine[:, 0], fine[0, 0])); Z = np.interp(s, cum, np.append(fine[:, 1], fine[0, 1]))
T = np.stack([np.gradient(X), np.gradient(Z)], 1); T /= np.linalg.norm(T, axis=1)[:, None]
Nn = np.stack([-T[:, 1], T[:, 0]], 1)
def nearest(p, heading=None):
    d = np.hypot(X - p[0], Z - p[1])
    if heading is not None: d = d + 100 * (T @ np.array(heading) < 0.7)
    return int(np.argmin(d))
def ahead(a, b): return ((b - a) % N) * STEP

# ---- the stretches
iRaise = nearest((11.0, 7.5), (1, 0))             # the raised road's first point: off the dock, onto the shore
iClimb0 = nearest((47.0, 7.5), (1, 0))            # the climb from the shore's height ...
iClimb1 = nearest((42.0, 50.5), (-1, 0))          # ... to the mesa's
iDown0 = nearest((13.6, 42.0), (0, 1))            # the ramp down to the dock: from the end of the third jump's landing ...
iFoot = nearest((6.3, 26.0), (0, -1))             # ... to the dock's height
iEnd = nearest((6.4, 24.6), (0, -1))              # the raised road ends on the dock, just behind the grid (two abreast)
iStart = nearest((7.2, START_Z), (0, -1))

def mix(f): return f * f * (3 - 2 * f) * 0.3 + f * 0.7   # a climb's shape: mostly straight, eased at its ends
# The ramp down: its grade rising evenly from level over its first 15 %, back to level over its last 15 % (trap), and LATER times as steep
# near its foot as at its top (the south-west hill stands beside its last stretch: it keeps high until it is past most of it). (A grade
# that jumped from level to the ramp's at its top, smoothed, dipped there to 50 %.)
LATER = 1.6
def trap(t, q=0.15): return min(1, t / q, (1 - t) / q) if 0 < t < 1 else 0.0
def descent(f, n=400):
    fs = np.linspace(0, 1, n + 1); w = np.array([trap(t) * (1 + (LATER - 1) * t) for t in fs])
    acc = np.concatenate([[0], np.cumsum((w[1:] + w[:-1]) / 2)]); acc /= acc[-1]
    return float(np.interp(min(max(f, 0), 1), fs, acc))
target = np.zeros(N)
for k in range(N):
    a = ahead(iRaise, k)
    if a > ahead(iRaise, iEnd): target[k] = DOCK                                     # the dock
    elif a <= ahead(iRaise, iClimb0): target[k] = DOCK + (SHORE - DOCK) * min(1, a / 8)
    elif a <= ahead(iRaise, iClimb1):
        target[k] = SHORE + (TOP - SHORE) * mix((a - ahead(iRaise, iClimb0)) / (ahead(iRaise, iClimb1) - ahead(iRaise, iClimb0)))
    elif a <= ahead(iRaise, iDown0): target[k] = TOP
    else: target[k] = TOP + (DOCK - TOP) * descent((a - ahead(iRaise, iDown0)) / (ahead(iRaise, iFoot) - ahead(iRaise, iDown0)))
Y = target.copy()
# the jumps' level stretches: a ramp's length before the take-off lip to a landing ramp's length after the landing lip
jumps = []
fixed = np.zeros(N, bool)
for name, mid, gap in JUMPS:
    c = nearest(mid)
    lip = (c - int(round(gap / 2 / STEP))) % N; land = (c + int(round(gap / 2 / STEP))) % N
    jumps.append((name, lip, land))
    a = (lip - int(round((RAMP + 1) / STEP))) % N; b = (land + int(round((LANDING + 1) / STEP))) % N
    lvl = float(Y[c])
    k = a
    while True:
        Y[k] = lvl; fixed[k] = True
        if k == b: break
        k = (k + 1) % N
# smoothing (but not the level stretches), the raised road's own grade is free
w = int(round(4 / STEP))
for _ in range(3):
    pad_ = np.concatenate([Y[-w:], Y, Y[:w]])
    sm = np.convolve(pad_, np.ones(2 * w + 1) / (2 * w + 1), 'same')[w:-w]
    Y = np.where(fixed, Y, sm)
grade = (np.roll(Y, -1) - Y) / (STEP * 512)
raised = np.zeros(N, bool)
for k in range(N):
    raised[k] = ahead(iRaise, k) <= ahead(iRaise, iEnd)

# ---- checks
out = [f'lap {total:.1f} cells, {N} points; raised from point {iRaise} to {iEnd} ({ahead(iRaise, iEnd):.1f} cells), the dock {ahead(iEnd, iRaise):.1f}; '
       f'the ramp down {ahead(iDown0, iEnd):.1f} cells']
edge = np.minimum.reduce([X, Z, 64 - X, 64 - Z])
out.append(f'  nearest the cube edge {edge.min():.1f} cells (the road needs 4.3: its verge and half a cell)')
clear = [(Y[k] - ground(X[k], Z[k]), k) for k in range(N) if raised[k]]
low = min(clear)
out.append(f'  the raised road over the ground: {low[0]:.0f} at the least (point {low[1]}, ({X[low[1]]:.1f}, {Z[low[1]]:.1f}))')
gr = grade[raised]
out.append(f'  steepest {gr.max() * 100:.1f} % up, {-gr.min() * 100:.1f} % down on the raised road; heights {Y.min():.0f} to {Y.max():.0f}')
# (across the deck's whole width, not only its middle: what the cutting under it has to take away)
wide = []
for k in range(N):
    if not raised[k]: continue
    g = max(ground(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o) for o in np.linspace(-RAISED_HALF - 1, RAISED_HALF + 1, 9))
    wide.append((Y[k] - g, k))
for c, k in sorted(wide)[:4]: out.append(f'    the ground across the deck {-c:.0f} over it at ({X[k]:.1f}, {Z[k]:.1f}), road {Y[k]:.0f}' if c < 0 else f'    low over the ground: {c:.0f} at ({X[k]:.1f}, {Z[k]:.1f})')
for name, lip, land in jumps:
    gap = ahead(lip, land)
    before = (lip - int(round(RAMP / STEP))) % N; after = (land + int(round(LANDING / STEP))) % N
    turn = math.degrees(math.acos(np.clip(T[before] @ T[after], -1, 1)))
    out.append(f'  jump {name}: lips {gap:.1f} cells apart at ({X[lip]:.1f}, {Z[lip]:.1f}) -> ({X[land]:.1f}, {Z[land]:.1f}), level {Y[lip]:.0f}, the road turns {turn:.0f} degrees over its ramps')
# two parts of the lap side by side at one level
idx = np.arange(N); close = []
for i in range(0, N, 2):
    dd = np.hypot(X - X[i], Z - Z[i]); sep = np.minimum(np.abs(idx - i), N - np.abs(idx - i))
    near = (sep > 40) & (dd < 2 * RAISED_HALF + 0.2) & (np.abs(Y - Y[i]) < 1500)
    if near.any(): close.append((dd[near].min(), i))
if close: out.append(f'  WARNING: two parts of the lap {min(close)[0]:.1f} cells apart at one level, at ({X[min(close)[1]]:.1f}, {Z[min(close)[1]]:.1f})')
TEMPLE = (17.1, 31.7, 25.6, 41.8)
dx = np.maximum(np.maximum(TEMPLE[0] - X, 0), X - TEMPLE[2]); dz = np.maximum(np.maximum(TEMPLE[1] - Z, 0), Z - TEMPLE[3])
out.append(f'  the temple: the rails {(np.hypot(dx, dz) - RAISED_HALF)[raised].min():.1f} cells from it at the nearest')
out.append(f'  start line at point {iStart} ({X[iStart]:.1f}, {Z[iStart]:.1f}), heading ({T[iStart,0]:+.2f}, {T[iStart,1]:+.2f})')
print('\n'.join(out))

PIT_SPOTS = [[13.0, 17.0, 0.0, -1.0], [15.5, 17.0, 0.0, -1.0], [13.0, 20.5, 0.0, -1.0], [15.5, 20.5, 0.0, -1.0], [14.2, 23.5, 0.0, -1.0]]
plan = {
    'originCellX': ORIGIN, 'originCellZ': ORIGIN,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in zip(X, Z)],
    'heights': [round(float(y), 1) for y in Y],
    'maxGrade': round(float(max(grade[raised].max(), -grade[raised].min())) + 0.01, 3),
    'asphaltHalf': ASPHALT, 'curbHalf': CURB, 'vergeHalf': VERGE, 'blend': BLEND,
    'raised': [int(iRaise), int(iEnd)], 'raisedHalf': RAISED_HALF, 'raisedCut': True,
    'gapJumps': [[int(lip), int(land)] for name, lip, land in jumps],
    'jumpRampLength': RAMP, 'jumpLandingLength': LANDING, 'jumpMinScale': 0.6,
    'start': int(iStart),
    'pitSpots': PIT_SPOTS,
    'gridStep': 1.75,
    'gantryFootings': True,
    'gravity': 0.8,
    # the temple (its hall, its west tower and its roofs) stays where it is
    'keepBodies': [0, 1, 6, 7],
}
json.dump(plan, open(OUT, 'w'))
np.savez(D + 'lap3.npz', X=X, Z=Z, Y=Y, T=T, raised=raised)
print('wrote', OUT)

if '--pictures' in sys.argv:
    from PIL import Image, ImageDraw
    S, M = 14, 20
    im = Image.new('RGB', (64 * S + 2 * M,) * 2, (25, 45, 90)); d = ImageDraw.Draw(im)
    for zc in range(64):
        for xc in range(64):
            h = ground(xc + 0.5, zc + 0.5)
            if lava(xc + 0.5, zc + 0.5): col = (200, 40, 20)
            elif h <= 50: continue
            else: g = int(60 + 170 * h / 7500); col = (g, int(g * 0.85), int(g * 0.7))
            d.rectangle([M + xc * S, M + zc * S, M + xc * S + S - 1, M + zc * S + S - 1], fill=col)
    def px(x, z): return (M + x * S, M + z * S)
    for i in range(N):
        a = px(X[i] + Nn[i, 0] * RAISED_HALF, Z[i] + Nn[i, 1] * RAISED_HALF); b = px(X[i] - Nn[i, 0] * RAISED_HALF, Z[i] - Nn[i, 1] * RAISED_HALF)
        t = (Y[i] - 400) / 6200
        d.line([a, b], fill=(int(60 + 190 * t), int(60 + 120 * t), 200 - int(120 * t)) if raised[i] else (90, 90, 90), width=3)
    for name, lip, land in jumps:
        k = lip
        while k != land:
            d.line([px(X[k], Z[k]), px(X[(k + 1) % N], Z[(k + 1) % N])], fill=(255, 255, 0), width=5); k = (k + 1) % N
        d.text(px(X[lip] + 1, Z[lip] - 2), name, fill=(255, 255, 255))
    a = px(X[iStart] + Nn[iStart, 0] * 3.5, Z[iStart] + Nn[iStart, 1] * 3.5); b = px(X[iStart] - Nn[iStart, 0] * 3.5, Z[iStart] - Nn[iStart, 1] * 3.5)
    d.line([a, b], fill=(255, 255, 255), width=4)
    for i in range(0, N, 24):
        d.text(px(X[i] + 0.6, Z[i] + 0.6), f'{int(Y[i])}', fill=(230, 230, 230))
    im.save(D + 'lap3.png'); print('picture', D + 'lap3.png')
