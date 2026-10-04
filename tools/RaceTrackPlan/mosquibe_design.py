# (Mosquibees Island, 2026-09-29; run in E:\dump\TEMP\mosq with H.npy and terrain.png there: python mosquibe_design.py 11100 12450 750)
"""Mosquibees Island race track: the centre line designed by hand from the user's drawing (images/14.png, traced in trace.py), with its
height profile, the bridge's deck and the jump's gap, checked against the island (grades, bends, separation, the island's edge)."""
import json, sys
import numpy as np
from PIL import Image, ImageDraw

H = np.load('H.npy')          # H[z-448, x-448]
O = 448

# control points in lap order: (name, x, z)
CP = [
    ('N1', 536, 457.5), ('N2', 546, 457.5), ('N3', 553, 458),
    ('NE1', 559.5, 460.5), ('NE1b', 562.5, 466), ('NE2', 559.5, 471.5),
    ('D1', 553, 477), ('D2', 548.5, 485),
    ('S1', 547.5, 493), ('S2', 552, 501), ('S3', 557, 507.5),
    ('E1', 557.5, 516.5), ('E2', 555.5, 530), ('E3', 554, 544), ('E4', 552, 554.5),
    ('B1', 545, 563), ('B2', 532, 566.5), ('B3', 516, 567), ('B4', 500, 566.5), ('B5', 485, 566), ('B6', 471, 564),
    ('L1a', 461.5, 557), ('L1b', 458.5, 545), ('L1c', 459.5, 532), ('L1d', 465, 523.5), ('L1e', 477, 519), ('L1f', 490, 520),
    ('L1g', 499, 525), ('L1h', 504, 535), ('L1i', 503, 545), ('L1j', 497, 552),
    ('L2a', 486, 553.5), ('L2b', 475.5, 552.5), ('L2c', 470, 546), ('L2d', 470, 538), ('L2e', 474.5, 533), ('L2f', 482, 531.5),
    ('BR1', 491, 530.5), ('BR2', 505, 530.5), ('BR3', 517, 530.5),
    ('R1', 524, 527.5), ('R2', 527, 518),
    ('J0', 527, 503), ('J1', 527, 493), ('J2', 527, 476), ('J3', 527.5, 468),
    ('NW1', 530.5, 460.5),
]
names = [c[0] for c in CP]
P = np.array([[c[1], c[2]] for c in CP], float)


def catmull(P, per=40, alpha=0.5):
    n = len(P); out = []; idx = []
    for i in range(n):
        p0, p1, p2, p3 = P[(i - 1) % n], P[i], P[(i + 1) % n], P[(i + 2) % n]
        def tj(ti, a, b): return ti + np.linalg.norm(b - a) ** alpha
        t0 = 0; t1 = tj(t0, p0, p1); t2 = tj(t1, p1, p2); t3 = tj(t2, p2, p3)
        for t in np.linspace(t1, t2, per, endpoint=False):
            a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1
            a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2
            a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3
            b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2
            b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3
            out.append((t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2)
            idx.append(i)
    return np.array(out), np.array(idx)


dense, owner = catmull(P)
seg = np.linalg.norm(np.diff(np.vstack([dense, dense[:1]]), axis=0), axis=1)
S = np.concatenate([[0], np.cumsum(seg)])[:-1]
L = S[-1] + seg[-1]
# resample every 0.5 cells
n = int(round(L / 0.5))
at = np.linspace(0, L, n, endpoint=False)
X = np.interp(at, S, dense[:, 0], period=L)
Z = np.interp(at, S, dense[:, 1], period=L)
sOf = {nm: S[np.argmax(owner == i)] for i, nm in enumerate(names)}

# ---- heights: keys at control points, straight between, each kink rounded by a vertical curve (a parabola tangent to both grades,
# R cells either side) -- never steeper than the steeper grade, and the flats (keyed R cells wider) exactly flat ----
TOP = 12750
LOW = float(sys.argv[3]) if len(sys.argv) > 3 else 900
DECK = float(sys.argv[1]) if len(sys.argv) > 1 else 10700
JUMP = float(sys.argv[2]) if len(sys.argv) > 2 else 12300
R = 5.0
FLATS = [('N1', 'D2', TOP), ('BR1', 'BR3', DECK), ('J0', 'J3', JUMP)]
key = []                                    # (s, height)
for a_, b_, v in FLATS: key += [(sOf[a_] - R, v), (sOf[b_] + R, v)]
key += [(sOf['S1'] + R, TOP), (sOf['L1a'], LOW)]
key = sorted(((k % L), v) for k, v in key)
kx = [k for k, _ in key]; kv = [v for _, v in key]
KX = np.array([kx[-1] - L] + kx + [kx[0] + L]); KV = np.array([kv[-1]] + kv + [kv[0]])
def lin(s): return np.interp(s, KX, KV)
H_road = lin(at)
for k in range(1, len(KX) - 1):
    sk, yk = KX[k], KV[k]
    g1 = (KV[k] - KV[k - 1]) / (KX[k] - KX[k - 1]); g2 = (KV[k + 1] - KV[k]) / (KX[k + 1] - KX[k])
    r = min(R, (KX[k] - KX[k - 1]) / 2, (KX[k + 1] - KX[k]) / 2)
    for i in range(n):
        for sh in (0, L, -L):
            d = at[i] + sh - sk
            if -r <= d <= r:
                H_road[i] = yk + g1 * d + (g2 - g1) / (4 * r) * (d + r) ** 2
def between(s, a, b):
    sa, sb = sOf[a], sOf[b]
    return (s >= sa) & (s <= sb) if sa <= sb else (s >= sa) | (s <= sb)

# ---- checks ----
def ground(x, z):
    xi, zi = x - O, z - O
    x0, z0 = int(np.floor(xi)), int(np.floor(zi))
    if x0 < 0 or z0 < 0 or x0 >= 128 or z0 >= 128: return np.nan
    fx, fz = xi - x0, zi - z0
    return (H[z0, x0] * (1 - fx) * (1 - fz) + H[z0, x0 + 1] * fx * (1 - fz) + H[z0 + 1, x0] * (1 - fx) * fz + H[z0 + 1, x0 + 1] * fx * fz)

def edge_dist(x, z):
    # present cubes: (7,8), (8,7), (8,8)
    present = {(7, 8), (8, 7), (8, 8)}
    best = 1e9
    cx, cz = int(x // 64), int(z // 64)
    for dz in (-1, 0, 1):
        for dx in (-1, 0, 1):
            q = (cx + dx, cz + dz)
            if q in present: continue
            qx = min(max(x, q[0] * 64), q[0] * 64 + 64); qz = min(max(z, q[1] * 64), q[1] * 64 + 64)
            best = min(best, np.hypot(x - qx, z - qz))
    return best

grade = np.diff(np.concatenate([H_road, H_road[:1]])) / (0.5 * 512)
# bend radius over 3 cells either side
k = 6
rad = np.full(n, 1e9)
for i in range(n):
    a, b, c = np.array([X[i - k], Z[i - k]]), np.array([X[i], Z[i]]), np.array([X[(i + k) % n], Z[(i + k) % n]])
    ab, bc, ca = np.linalg.norm(b - a), np.linalg.norm(c - b), np.linalg.norm(a - c)
    area2 = abs((b - a)[0] * (c - a)[1] - (b - a)[1] * (c - a)[0])
    if area2 > 1e-9: rad[i] = ab * bc * ca / (2 * area2)
edge = np.array([edge_dist(x, z) for x, z in zip(X, Z)])
g = np.array([ground(x, z) for x, z in zip(X, Z)])

print('lap %.0f cells (%d points)' % (L, n))
for nm in names:
    i = int(round(sOf[nm] / 0.5)) % n
    print('  %-4s s %5.1f  (%5.1f,%5.1f)  road %6.0f  ground %6.0f' % (nm, sOf[nm], X[i], Z[i], H_road[i], g[i]))
print('steepest climb %.1f%% at s %.0f, steepest descent %.1f%% at s %.0f' % (grade.max() * 100, at[grade.argmax()], -grade.min() * 100, at[grade.argmin()]))
print('tightest bends:', ', '.join('%.1f at (%.0f,%.0f)' % (rad[i], X[i], Z[i]) for i in np.argsort(rad)[:1]))
tb = []
for i in np.argsort(rad):
    if all(min(abs(at[i] - at[j]), L - abs(at[i] - at[j])) > 15 for j in tb): tb.append(i)
    if len(tb) == 6: break
print('   ', ', '.join('%.1f@%s' % (rad[i], names[owner[np.argmin(np.abs(S - at[i]))]]) for i in tb))
print('closest to a missing cube: %.1f cells at (%.0f,%.0f) (needs 6.5)' % (edge.min(), X[edge.argmin()], Z[edge.argmin()]))
# separation between parts of the lap more than 30 cells apart along it
pts = np.stack([X, Z], 1)
from scipy.spatial import cKDTree
tree = cKDTree(pts)
close = []
for i, j in tree.query_pairs(13.0):
    sep = abs(at[i] - at[j]); sep = min(sep, L - sep)
    if sep < 40: continue
    close.append((np.hypot(*(pts[i] - pts[j])), i, j))
close.sort()
seen = []
for d, i, j in close:
    if any(abs(at[i] - at[a]) < 15 and abs(at[j] - at[b]) < 15 for a, b in seen): continue
    seen.append((i, j))
    ni = names[owner[np.argmin(np.abs(S - at[i]))]]; nj = names[owner[np.argmin(np.abs(S - at[j]))]]
    print('  %.1f cells apart: %s (%.0f) and %s (%.0f), height gap %.0f' % (d, ni, H_road[i], nj, H_road[j], abs(H_road[i] - H_road[j])))
    if len(seen) > 12: break

# ---- plan ----
def pidx(nm, ds=0.0): return int(round((sOf[nm] + ds) / 0.5)) % n
PIT_A, PIT_B = pidx('NW1', 5), pidx('NE1', -1)
plan = {
    'originCellX': O, 'originCellZ': O,
    'points': [[round(float(x - O), 3), round(float(z - O), 3)] for x, z in zip(X, Z)],
    'heights': [round(float(h), 1) for h in H_road],
    'maxGrade': round(float(max(grade.max(), -grade.min()) + 0.01), 3),
    'deck': [pidx('BR1'), pidx('BR3')],
    'gapJump': [pidx('J1'), pidx('J2')],
    'pitA': [round(float(X[PIT_A] - O), 3), round(float(Z[PIT_A] - O), 3)],
    'pitB': [round(float(X[PIT_B] - O), 3), round(float(Z[PIT_B] - O), 3)],
    'pitTaper': 7,
    # (2026-10-01) land mines on the way round the jump: three staggered rows of two across the corridor between the jump's east walls
    # and the lap's other road, each at least 7 cells from the road (4 from the flight's line)
    'mines': [[89.5, 33.5], [92.5, 33.5], [88.5, 37.5], [91.5, 37.5], [87.5, 41.5], [90.5, 41.5]],
}
json.dump(plan, open('E:/dump/LBAAssembler/docs/racetrack/mosquibe_track_plan.json', 'w'))
json.dump({'plan': plan, 'marks': {nm: pidx(nm) for nm in names}}, open('design.json', 'w'))
print('plan written: deck points %s, jump points %s, pit %s..%s, max grade %.3f' % (plan['deck'], plan['gapJump'], plan['pitA'], plan['pitB'], plan['maxGrade']))

# ---- pictures ----
img = Image.open('terrain.png').convert('RGB').resize((1024, 1024), Image.NEAREST)
dr = ImageDraw.Draw(img)
def px(x, z): return ((x - O) * 8, (z - O) * 8)
hmin, hmax = 0, TOP
for i in range(n):
    j = (i + 1) % n
    t = (H_road[i] - hmin) / (hmax - hmin)
    col = (int(255 * t), int(255 * (1 - abs(2 * t - 1))), int(255 * (1 - t)))
    dr.line([px(X[i], Z[i]), px(X[j], Z[j])], fill=col, width=5)
for nm, x, z in CP:
    dr.text(px(x, z), nm, fill=(255, 255, 255))
img.save('design.png')
# the profile
W, Hh = 1200, 300
pr = Image.new('RGB', (W, Hh), (20, 20, 30)); d2 = ImageDraw.Draw(pr)
for i in range(n - 1):
    d2.line([(at[i] / L * W, Hh - 10 - H_road[i] / TOP * (Hh - 20)), (at[i + 1] / L * W, Hh - 10 - H_road[i + 1] / TOP * (Hh - 20))], fill=(255, 220, 0), width=2)
    if not np.isnan(g[i]): d2.point((at[i] / L * W, Hh - 10 - g[i] / TOP * (Hh - 20)), fill=(120, 200, 120))
for nm in names:
    d2.text((sOf[nm] / L * W, 5 + (names.index(nm) % 3) * 10), nm, fill=(200, 200, 255))
pr.save('profile.png')
