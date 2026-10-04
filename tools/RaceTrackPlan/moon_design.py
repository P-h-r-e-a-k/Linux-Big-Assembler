# The old moon (MOON.ILE, the Emerald Moon's older copy that no version of the game loads: raced as island 3 by the race-track mode): a
# test lap with two vertical loops, on the crater's floor round the moon base. The crater spans the island's four cubes (7,7)-(8,8);
# its floor is level at about 300, the base's buildings on it (the builder clears those on the road). The lap is an oval, anticlockwise on
# the map: east along the south side, north up the east side (the start line), west along the north side, and back down the west side.
# Both loops are on the south side, one after the other:
#   - the first has a gap at its top: the car is carried round it at the speed it came in with and over the gap upside down;
#   - the second is a whole ring, after a run-up of 30 cells: the car goes round it as a real car would, or falls off if too slow.
# Each loop's ring stands within one cube (the engine carries the car round in that cube's coordinates), on a straight that is level
# for the ring's radius and more either side of its foot, with a run-up long enough to come at it at full speed.
#
# Writes docs/racetrack/moon_track_plan.json; --pictures draws the lap over the crater's heights and its height profile.
import json, math, os, sys
import numpy as np

OUT = 'E:/dump/LBAAssembler/docs/racetrack/moon_track_plan.json'
D = 'E:/dump/TEMP/moon/'
ORIGIN = 448                      # the island's first cell (cube 7,7)
H = np.load(D + 'H.npy')          # H[z, x], island cells from the origin, 0..128 (E:\dump\TEMP\moon\grid.py from MOON.ILE)

def ground(x, z):
    xi, zi = min(127, max(0, int(x))), min(127, max(0, int(z)))
    fx, fz = x - xi, z - zi
    return (H[zi, xi] * (1 - fx) * (1 - fz) + H[zi, xi + 1] * fx * (1 - fz) + H[zi + 1, xi] * (1 - fx) * fz + H[zi + 1, xi + 1] * fx * fz)

ASPHALT, CURB, VERGE, BLEND = 3.0, 3.5, 4.5, 3.0
STEP = 0.5
# the corners, anticlockwise on the map from the south-west, and their radii
V = [(24.0, 92.0), (90.0, 92.0), (88.0, 54.0), (42.0, 56.0)]
R = [7.0, 8.0, 8.0, 8.0]
# the loops: where on its side the foot is (cells), the ring's radius, the shift across, the gap at the top (degrees)
LOOPS = [
    ('jump', (44.0, 92.0), 4.0, 3.0, 35.0),    # the loop with a gap at its top, leapt upside down (35 degrees: from 22 km/h to 43, 2026-10-03; it was 70, too fast over 24)
    ('whole', (74.0, 92.0), 4.0, 3.0, 0.0),    # the whole ring, driven round as a real car would go, after a long run-up
]
LEVEL = 6.0                       # level road this far past the ring's radius either side of its foot (cells)
WALL_FREE = 1150
MAXG = 0.08

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

# ---- heights: the ground across the road, smoothed, within WALL_FREE of the ground under its middle; level round each loop's foot
G = np.array([ground(x, z) for x, z in zip(X, Z)])
foot = np.array([np.median([ground(X[k] + Nn[k, 0] * t, Z[k] + Nn[k, 1] * t) for t in np.linspace(-CURB, CURB, 7)]) for k in range(N)])
Y = foot.copy()
w = int(round(8 / STEP))
for _ in range(3):
    pad_ = np.concatenate([Y[-w:], Y, Y[:w]])
    Y = np.convolve(pad_, np.ones(2 * w + 1) / (2 * w + 1), 'same')[w:-w]
fixed = np.zeros(N, bool)
loops = []
for name, (lx, lz), rad, shift, gap in LOOPS:
    k = int(np.argmin(np.hypot(X - lx, Z - lz)))
    reach = int(round((rad + LEVEL) / STEP))
    idx = [(k + d) % N for d in range(-reach, reach + 1)]
    level = float(np.max(foot[idx]))
    for i in idx: Y[i] = level; fixed[i] = True
    loops.append((name, k, rad, shift, gap, level))
Y = np.clip(Y, G - WALL_FREE, G + WALL_FREE)
lim = MAXG * STEP * 512 * 0.9
for _ in range(2 * N):
    changed = False
    for k in range(N):
        if fixed[k]: continue
        lo = max(Y[(k - 1) % N], Y[(k + 1) % N]) - lim
        if Y[k] < lo - 1e-6: Y[k] = lo; changed = True
    if not changed: break
grade = (np.roll(Y, -1) - Y) / (STEP * 512)

# ---- the start line: on the east side, heading north, half way up
START = int(np.argmin(np.hypot(X - 86.0, Z - 78.0) + 100 * (T[:, 1] > -0.5)))

# ---- checks
out = [f'lap {total:.1f} cells, {N} points']
def straight(k):
    def run(dirn):
        m = 0
        while m < N // 2 and abs(T[(k + dirn * (m + 1)) % N] @ T[k]) > math.cos(math.radians(1.0)): m += 1
        return m * STEP
    return run(-1), run(1)
for name, k, rad, shift, gap, level in loops:
    before, after = straight(k)
    cx, cz = int((X[k] + ORIGIN) // 64), int((Z[k] + ORIGIN) // 64)
    lx, lz = X[k] + ORIGIN - cx * 64, Z[k] + ORIGIN - cz * 64
    reach = rad + 0.5
    wide = shift / 2 + 1.6
    edge_along = min(lx - reach * abs(T[k, 0]) - wide * abs(T[k, 1]), 64 - lx - reach * abs(T[k, 0]) - wide * abs(T[k, 1]),
                     lz - reach * abs(T[k, 1]) - wide * abs(T[k, 0]), 64 - lz - reach * abs(T[k, 1]) - wide * abs(T[k, 0]))
    gaps = abs((START - k + N // 2) % N - N // 2) * STEP
    out.append(f'  {name} loop: foot at point {k} ({X[k]:.1f}, {Z[k]:.1f}), cube ({cx},{cz}) local ({lx:.1f}, {lz:.1f}), heading ({T[k,0]:+.2f}, {T[k,1]:+.2f}); '
               f'straight {before:.1f} before, {after:.1f} after (needs {rad + LEVEL:.1f}); the ring {edge_along:.1f} cells inside its cube; level at {level:.0f}; {gaps:.0f} cells from the start line')
edge = np.minimum.reduce([X, Z, 128 - X, 128 - Z])
out.append(f'  nearest the island\'s edge {edge.min():.1f} cells')
off = Y - G
out.append(f'  the road against the ground under its middle: {off.min():+.0f} to {off.max():+.0f}')
out.append(f'  steepest {grade.max() * 100:.1f} % up, {-grade.min() * 100:.1f} % down; heights {Y.min():.0f} to {Y.max():.0f}')
out.append(f'  start line at point {START} ({X[START]:.1f}, {Z[START]:.1f}), heading ({T[START, 0]:+.2f}, {T[START, 1]:+.2f})')
print('\n'.join(out))

plan = {
    'originCellX': ORIGIN, 'originCellZ': ORIGIN,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in zip(X, Z)],
    'heights': [round(float(y), 1) for y in Y],
    'maxGrade': round(float(max(grade.max(), -grade.min())) + 0.01, 3),
    'asphaltHalf': ASPHALT, 'curbHalf': CURB, 'vergeHalf': VERGE, 'blend': BLEND,
    'start': int(START),
    'loops': [[int(k), rad, shift, gap] for name, k, rad, shift, gap, level in loops],
    # (2026-10-03: opponents race it, round the loops; they wait here while the player qualifies -- inside the oval on the crater's
    # floor, beside the start line's straight and clear of it, facing up it: on the grid behind the line they stood on the lap's last
    # bend, in the player's way)
    'pitSpots': [[80.0, z, 0.0, -1.0] for z in (66.0, 70.0, 74.0)],
}
json.dump(plan, open(OUT, 'w'))
print('wrote', OUT)

if '--pictures' in sys.argv:
    from PIL import Image, ImageDraw
    S, M = 6, 10
    hi = H.max()
    im = Image.new('RGB', (128 * S + 2 * M, 128 * S + 2 * M), (20, 30, 60)); d = ImageDraw.Draw(im)
    for zc in range(128):
        for xc in range(128):
            h = H[zc, xc]
            g = int(40 + 200 * min(1, h / 7000))
            d.rectangle([M + xc * S, M + zc * S, M + xc * S + S - 1, M + zc * S + S - 1], fill=(g, g, int(g * 0.9)))
    for c in (64,):
        d.line([(M + c * S, M), (M + c * S, M + 128 * S)], fill=(90, 90, 160)); d.line([(M, M + c * S), (M + 128 * S, M + c * S)], fill=(90, 90, 160))
    def px(x, z): return (M + x * S, M + z * S)
    for i in range(N):
        a = px(X[i] + Nn[i, 0] * CURB, Z[i] + Nn[i, 1] * CURB); b = px(X[i] - Nn[i, 0] * CURB, Z[i] - Nn[i, 1] * CURB)
        d.line([a, b], fill=(70, 70, 70), width=3)
    for i in range(N): d.line([px(X[i], Z[i]), px(X[(i + 1) % N], Z[(i + 1) % N])], fill=(230, 230, 230), width=1)
    for name, k, rad, shift, gap, level in loops:
        x0, z0 = X[k] - T[k, 0] * 0 , Z[k]
        a = px(X[k] - Nn[k, 0] * 3, Z[k] - Nn[k, 1] * 3); b = px(X[k] + T[k, 0] * rad * 2 - Nn[k, 0] * 3, Z[k] + T[k, 1] * rad * 2 - Nn[k, 1] * 3)
        c = px(X[k] + T[k, 0] * rad * 2 + Nn[k, 0] * 3, Z[k] + T[k, 1] * rad * 2 + Nn[k, 1] * 3); e = px(X[k] + Nn[k, 0] * 3, Z[k] + Nn[k, 1] * 3)
        d.polygon([a, b, c, e], outline=(80, 220, 80))
        d.text(px(X[k], Z[k] - 6), name + (' (gap)' if gap else ''), fill=(120, 255, 120))
    a = px(X[START] + Nn[START, 0] * VERGE, Z[START] + Nn[START, 1] * VERGE); b = px(X[START] - Nn[START, 0] * VERGE, Z[START] - Nn[START, 1] * VERGE)
    d.line([a, b], fill=(255, 255, 255), width=3)
    os.makedirs(D, exist_ok=True)
    im.save(D + 'lap.png')
    print('picture in', D + 'lap.png')
