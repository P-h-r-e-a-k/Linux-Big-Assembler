# Sendell's Well (island 1, made by the build: Terrain/SendellWell.cs): a lap round the well. The island is one round cube -- a beach
# (r 22.5-26 cells from the middle, wavy), grassy slopes up to a flat plateau (r 11, 1900 high), a paved rim (r 4.5-6.5) round the well.
# The lap is an egg round the well, clockwise on the map: along the plateau's north edge, down the east slope, along the south beach
# (the start line, heading west), and back up the west slope through an S -- in towards the well over a crest, out over a dip, and
# in again onto the plateau: 1200 units of climb and drop, no crossing.
#
# The ground is the island's own formula (SendellWell.HeightAt), so the heights need no island file. Writes
# docs/racetrack/sendell_track_plan.json; --pictures draws the lap over the island's heights and its height profile.
import json, math, os, sys
import numpy as np

OUT = 'E:/dump/LBAAssembler/docs/racetrack/sendell_track_plan.json'
D = 'E:/dump/TEMP/sendell_track/'
ORIGIN = 448                      # the cube's first island cell (cube 7,7)
MID = 32.0

# ---- the island (SendellWell.cs)
COAST, BEACH_TOP, PLATEAU, RIM_OUT, RIM_IN, WELL_BOTTOM = 26, 22.5, 11, 6.5, 4.5, 3.5
BEACH_H, PLATEAU_H, RIM_H, BOTTOM_H = 350, 1900, 2250, 120
def wobble(a): return 1 + 0.07 * math.sin(3 * a + 0.4) + 0.045 * math.sin(5 * a + 1.9) + 0.03 * math.sin(8 * a)
def smooth(t): t = min(1, max(0, t)); return t * t * (3 - 2 * t)
def height_at(dx, dz):
    r = math.hypot(dx, dz); w = wobble(math.atan2(dz, dx)); coast = COAST * w; top = BEACH_TOP * w
    if r >= coast: return 0
    if r >= top: return BEACH_H * smooth((coast - r) / (coast - top))
    if r >= PLATEAU: return BEACH_H + (PLATEAU_H - BEACH_H) * smooth((top - r) / (top - PLATEAU))
    if r >= RIM_OUT: return PLATEAU_H
    if r >= RIM_IN: return RIM_H
    if r >= WELL_BOTTOM: return BOTTOM_H + (RIM_H - BOTTOM_H) * smooth((r - WELL_BOTTOM) / (RIM_IN - WELL_BOTTOM))
    return BOTTOM_H
def ground(x, z): return height_at(x - MID, z - MID)

# ---- the road: narrower than the retail road (the island is 50 cells across)
ASPHALT, CURB, VERGE, BLEND = 3.0, 3.5, 4.5, 3.0
STEP = 0.5
# the corners, clockwise on the map from the plateau's north-west, and each corner's radius
V = [(20.5, 24.5), (32.0, 20.5), (43.5, 24.5), (48.5, 34.0), (45.5, 46.0), (39.0, 51.0), (25.5, 51.0), (16.5, 46.5), (19.0, 37.5), (13.0, 30.0)]
R = [5.0, 9.0, 8.0, 8.0, 6.0, 5.0, 5.0, 5.0, 4.0, 4.0]
WALL_FREE = 1150                   # the most the road is built up or cut down from the ground under its middle (the builder walls at 1200)
MAXG = 0.10

def fillet(V, R):
    n = len(V); V = [np.array(v, float) for v in V]; tang = []
    for i in range(n):
        a, v, b = V[i - 1], V[i], V[(i + 1) % n]
        d1 = (v - a) / np.linalg.norm(v - a); d2 = (b - v) / np.linalg.norm(b - v)
        ang = math.acos(np.clip(d1 @ d2, -1, 1)); t = R[i] * math.tan(ang / 2)
        tang.append((v - d1 * t, v + d2 * t, d1, d2, ang, t))
    pts = []
    for i in range(n):
        p1, p2, d1, d2, ang, t = tang[i]
        if ang > 1e-6:
            side = np.sign(d1[0] * d2[1] - d1[1] * d2[0]); n1 = np.array([-d1[1], d1[0]]) * side; c = p1 + n1 * R[i]
            a0 = math.atan2(p1[1] - c[1], p1[0] - c[0]); a1 = a0 + side * ang
            m = max(2, int(round(R[i] * ang / 0.02)))
            pts += [c + R[i] * np.array([math.cos(a0 + (a1 - a0) * k / m), math.sin(a0 + (a1 - a0) * k / m)]) for k in range(m)]
        q1, q2 = tang[i][1], tang[(i + 1) % n][0]
        if np.dot(q2 - q1, tang[(i + 1) % n][2]) < -1e-6: sys.exit(f'corners {i} and {(i + 1) % n} overlap')
        m = max(1, int(round(np.linalg.norm(q2 - q1) / 0.02)))
        pts += [q1 + (q2 - q1) * k / m for k in range(m)]
    return np.array(pts)

fine = fillet(V, R)
seg = np.linalg.norm(np.diff(np.vstack([fine, fine[:1]]), axis=0), axis=1)
cum = np.concatenate([[0], np.cumsum(seg)]); total = cum[-1]
N = int(round(total / STEP)); s = np.arange(N) * total / N
X = np.interp(s, cum, np.append(fine[:, 0], fine[0, 0])); Z = np.interp(s, cum, np.append(fine[:, 1], fine[0, 1]))
T = np.stack([np.gradient(X), np.gradient(Z)], 1); T /= np.linalg.norm(T, axis=1)[:, None]
Nn = np.stack([-T[:, 1], T[:, 0]], 1)

# ---- heights: the ground across the road's footprint, smoothed along the lap, kept within WALL_FREE of the ground under its middle,
# the grades limited
G = np.array([ground(x, z) for x, z in zip(X, Z)])
foot = np.array([np.median([ground(X[k] + Nn[k, 0] * t, Z[k] + Nn[k, 1] * t) for t in np.linspace(-CURB, CURB, 7)]) for k in range(N)])
Y = foot.copy()
w = int(round(6 / STEP))
for _ in range(3):
    pad_ = np.concatenate([Y[-w:], Y, Y[:w]])
    Y = np.convolve(pad_, np.ones(2 * w + 1) / (2 * w + 1), 'same')[w:-w]
Y = np.clip(Y, G - WALL_FREE, G + WALL_FREE)
lim = MAXG * STEP * 512 * 0.9
for _ in range(2 * N):
    changed = False
    for k in range(N):
        lo = max(Y[(k - 1) % N], Y[(k + 1) % N]) - lim
        if Y[k] < lo - 1e-6: Y[k] = lo; changed = True
    if not changed: break
Y = np.maximum(Y, BEACH_H * 0.6)                 # never down at the sea's level
grade = (np.roll(Y, -1) - Y) / (STEP * 512)

# ---- the start line: in the middle of the south beach's straight, heading west (the gantry's legs stand at the verges: on a bend
# the racing line cut into the inner one); the grid behind it up the south-east; the others wait on the plateau round
# the rim while the player qualifies
START = int(np.argmin(np.hypot(X - 31.5, Z - 51.0) + 100 * (T[:, 0] > -0.5)))
PIT_SPOTS = [[25.0, 33.0, 0.0, 1.0], [40.5, 33.0, 0.0, 1.0], [32.0, 41.0, 1.0, 0.0], [26.0, 39.0, 1.0, 0.0], [38.0, 39.0, -1.0, 0.0]]

# ---- checks
out = [f'lap {total:.1f} cells, {N} points']
r = np.hypot(X - MID, Z - MID)
out.append(f'  from the middle: {r.min():.1f} to {r.max():.1f} cells (the rim reaches {RIM_OUT}, its inner verge {r.min() - VERGE:.1f})')
coast = np.array([COAST * wobble(math.atan2(z - MID, x - MID)) for x, z in zip(X, Z)])
out.append(f'  the outer verge against the coast: {(coast - r - VERGE).min():+.1f} cells at the nearest')
edge = np.minimum.reduce([X, Z, 64 - X, 64 - Z])
out.append(f'  nearest the cube edge {edge.min():.1f} cells (the road needs 6.5)')
off = Y - G
out.append(f'  the road against the ground under its middle: {off.min():+.0f} to {off.max():+.0f}')
out.append(f'  steepest {grade.max() * 100:.1f} % up, {-grade.min() * 100:.1f} % down; heights {Y.min():.0f} to {Y.max():.0f}')
for (px, pz, _, _) in PIT_SPOTS:
    d = np.hypot(X - px, Z - pz).min()
    if d < VERGE + 2: out.append(f'  WARNING: pit spot ({px}, {pz}) {d:.1f} cells from the road')
out.append(f'  start line at point {START} ({X[START]:.1f}, {Z[START]:.1f}), heading ({T[START, 0]:+.2f}, {T[START, 1]:+.2f})')
print('\n'.join(out))

plan = {
    'originCellX': ORIGIN, 'originCellZ': ORIGIN,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in zip(X, Z)],
    'heights': [round(float(y), 1) for y in Y],
    'maxGrade': round(float(max(grade.max(), -grade.min())) + 0.01, 3),
    'asphaltHalf': ASPHALT, 'curbHalf': CURB, 'vergeHalf': VERGE, 'blend': BLEND,
    'start': int(START),
    'pitSpots': PIT_SPOTS,
}
json.dump(plan, open(OUT, 'w'))
print('wrote', OUT)

if '--pictures' in sys.argv:
    from PIL import Image, ImageDraw
    os.makedirs(D, exist_ok=True)
    S, M = 12, 20
    im = Image.new('RGB', (64 * S + 2 * M,) * 2, (30, 60, 110)); d = ImageDraw.Draw(im)
    for zc in range(64):
        for xc in range(64):
            h = ground(xc + 0.5, zc + 0.5)
            if h <= 0: continue
            g = int(80 + 150 * h / RIM_H)
            d.rectangle([M + xc * S, M + zc * S, M + xc * S + S - 1, M + zc * S + S - 1], fill=(g // 2, g, g // 3))
    def px(x, z): return (M + x * S, M + z * S)
    for i in range(N):
        a = px(X[i] + Nn[i, 0] * CURB, Z[i] + Nn[i, 1] * CURB); b = px(X[i] - Nn[i, 0] * CURB, Z[i] - Nn[i, 1] * CURB)
        d.line([a, b], fill=(60, 60, 60), width=4)
    for i in range(N):
        t = (Y[i] - Y.min()) / max(1, Y.max() - Y.min())
        d.line([px(X[i], Z[i]), px(X[(i + 1) % N], Z[(i + 1) % N])], fill=(int(80 + 175 * t), 80, int(255 - 175 * t)), width=2)
    a = px(X[START] + Nn[START, 0] * VERGE, Z[START] + Nn[START, 1] * VERGE); b = px(X[START] - Nn[START, 0] * VERGE, Z[START] - Nn[START, 1] * VERGE)
    d.line([a, b], fill=(255, 255, 255), width=3)
    for (x, z, _, _) in PIT_SPOTS: d.ellipse([px(x, z)[0] - 4, px(x, z)[1] - 4, px(x, z)[0] + 4, px(x, z)[1] + 4], fill=(255, 200, 0))
    im.save(D + 'lap.png')
    W, H = 900, 260
    pr = Image.new('RGB', (W, H), 'white'); dp = ImageDraw.Draw(pr)
    lo, hi = min(G.min(), Y.min()), max(G.max(), Y.max())
    def py(v): return H - 20 - (v - lo) / (hi - lo) * (H - 40)
    for i in range(N - 1):
        x0, x1 = i / N * W, (i + 1) / N * W
        dp.line([(x0, py(G[i])), (x1, py(G[i + 1]))], fill=(120, 160, 90), width=2)
        dp.line([(x0, py(Y[i])), (x1, py(Y[i + 1]))], fill=(40, 40, 40), width=2)
    pr.save(D + 'profile.png')
    print('pictures in', D)
