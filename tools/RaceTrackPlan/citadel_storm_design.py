# (Citadel Island in the storm, 2026-09-29; run in E:\dump\TEMP\cstorm with H.npy (fit.py) and terrain.png there: python citadel_storm_design.py)
"""Citadel Island's storm track (CITADEL.ILE): a short lap round the town from the user's drawing (citadelTrack.png, traced in trace.py),
designed by hand on the ground it runs over. The west side runs along the town's west rampart (a raised street, 2500 up) and jumps off
its end, where the drawing's two pink dots are; the lap comes down to the harbour's level round the south-west corner, runs along the
south and up the east side past the pit lane (the drawing's orange line) and the start line, and climbs back onto the north rampart
through the drawing's wiggle, made a double hairpin -- the drawing's three roads there were 7 cells apart, less than one road's width.
Designed anticlockwise on the map (south down the rampart, off its end) and built the other way round at the user's asking (REVERSE):
clockwise, north up the rampart's street, the jump from the harbour side onto the rampart's end."""
import json
import numpy as np
from PIL import Image, ImageDraw

H = np.load('H.npy')          # H[z-448, x-384]
OX, OZ = 384, 448

# control points in lap order: (name, x, z)
CP = [
    ('NW1', 523, 526.5), ('NW2', 519, 532),
    ('W1', 519, 545), ('W2', 519, 560),
    ('J0', 519, 570), ('J1', 519, 583), ('J2', 519, 594), ('J3', 519, 608),
    ('W3', 519, 616), ('SW1', 520.5, 623), ('SW2', 527, 626),
    ('S1', 537, 625.5), ('S2', 546, 622.5),
    ('SE1', 552, 616), ('SE2', 555, 606),
    ('E1', 557, 595), ('E2', 559, 580), ('E3', 560.5, 566), ('E4', 561, 557.5),
    ('A0', 558, 551), ('A1', 550, 549), ('A2', 543, 548.5),
    ('H1', 537, 546.5), ('H2', 535.5, 542), ('H3', 538.5, 537.5),
    ('B1', 545, 537), ('B2', 552, 537),
    ('H4', 558, 535), ('H5', 559.5, 530.5), ('H6', 556, 526.5),
    ('N1', 547, 526), ('N2', 537, 526), ('N3', 529, 526),
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
n = int(round(L / 0.5))
at = np.linspace(0, L, n, endpoint=False)
X = np.interp(at, S, dense[:, 0], period=L)
Z = np.interp(at, S, dense[:, 1], period=L)
sOf = {nm: S[np.argmax(owner == i)] for i, nm in enumerate(names)}

# ---- heights: keys, straight between, each kink rounded by a vertical curve (a parabola tangent to both grades, R cells either side);
# the jump's stretch exactly level from 10 cells before the take-off lip to 14 after the landing lip (the ramps go on top of it) ----
TOP, JUMP, LOW = 2500, 2000, 250
R = 5.0
key = [(sOf['N3'], TOP), (sOf['W1'], TOP),
       (sOf['J0'] - R, JUMP), (sOf['J3'] + R, JUMP),
       (sOf['S1'] + 4, LOW), (sOf['E4'], LOW),
       (sOf['N1'], TOP)]
key = sorted(((k % L), v) for k, v in key)
kx = [k for k, _ in key]; kv = [v for _, v in key]
KX = np.array([kx[-1] - L] + kx + [kx[0] + L]); KV = np.array([kv[-1]] + kv + [kv[0]])
H_road = np.interp(at, KX, KV)
for k in range(1, len(KX) - 1):
    sk, yk = KX[k], KV[k]
    g1 = (KV[k] - KV[k - 1]) / (KX[k] - KX[k - 1]); g2 = (KV[k + 1] - KV[k]) / (KX[k + 1] - KX[k])
    r = min(R, (KX[k] - KX[k - 1]) / 2, (KX[k + 1] - KX[k]) / 2)
    for i in range(n):
        for sh in (0, L, -L):
            d = at[i] + sh - sk
            if -r <= d <= r:
                H_road[i] = yk + g1 * d + (g2 - g1) / (4 * r) * (d + r) ** 2


# ---- checks ----
def ground(x, z):
    xi, zi = x - OX, z - OZ
    x0, z0 = int(np.floor(xi)), int(np.floor(zi))
    if x0 < 0 or z0 < 0 or x0 >= H.shape[1] - 1 or z0 >= H.shape[0] - 1: return np.nan
    fx, fz = xi - x0, zi - z0
    return (H[z0, x0] * (1 - fx) * (1 - fz) + H[z0, x0 + 1] * fx * (1 - fz) + H[z0 + 1, x0] * (1 - fx) * fz + H[z0 + 1, x0 + 1] * fx * fz)


PRESENT = {(6, 8), (7, 7), (7, 9), (7, 10), (8, 7), (8, 8), (8, 9), (8, 10)}
def edge_dist(x, z):
    best = 1e9
    cx, cz = int(x // 64), int(z // 64)
    for dz in (-1, 0, 1):
        for dx in (-1, 0, 1):
            q = (cx + dx, cz + dz)
            if q in PRESENT: continue
            qx = min(max(x, q[0] * 64), q[0] * 64 + 64); qz = min(max(z, q[1] * 64), q[1] * 64 + 64)
            best = min(best, np.hypot(x - qx, z - qz))
    return best


grade = np.diff(np.concatenate([H_road, H_road[:1]])) / (0.5 * 512)
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
tb = []
for i in np.argsort(rad):
    if all(min(abs(at[i] - at[j]), L - abs(at[i] - at[j])) > 10 for j in tb): tb.append(i)
    if len(tb) == 6: break
print('tightest bends:', ', '.join('%.1f@%s' % (rad[i], names[owner[np.argmin(np.abs(S - at[i]))]]) for i in tb))
print('closest to a missing cube: %.1f cells at (%.0f,%.0f) (needs 6.5)' % (edge.min(), X[edge.argmin()], Z[edge.argmin()]))
pts = np.stack([X, Z], 1)
from scipy.spatial import cKDTree
tree = cKDTree(pts)
close = []
for i, j in tree.query_pairs(13.0):
    sep = abs(at[i] - at[j]); sep = min(sep, L - sep)
    if sep < 30: continue
    close.append((np.hypot(*(pts[i] - pts[j])), i, j))
close.sort()
seen = []
for d, i, j in close:
    if any(abs(at[i] - at[a]) < 10 and abs(at[j] - at[b]) < 10 for a, b in seen): continue
    seen.append((i, j))
    ni = names[owner[np.argmin(np.abs(S - at[i]))]]; nj = names[owner[np.argmin(np.abs(S - at[j]))]]
    print('  %.1f cells apart: %s (%.0f) and %s (%.0f), height gap %.0f' % (d, ni, H_road[i], nj, H_road[j], abs(H_road[i] - H_road[j])))
    if len(seen) > 10: break


# ---- plan ----
def pidx(nm, ds=0.0): return int(round((sOf[nm] + ds) / 0.5)) % n


# the pit lane's two ends: lap points on the east straight, moved 3 cells to its inside (west) so the lane is built on that side
def inside(i):
    tx, tz = X[(i + 1) % n] - X[i - 1], Z[(i + 1) % n] - Z[i - 1]
    tl = np.hypot(tx, tz); tx, tz = tx / tl, tz / tl
    nx, nz = tz, -tx        # left of the way the lap runs (north): west
    return [round(float(X[i] + 3 * nx - OX), 3), round(float(Z[i] + 3 * nz - OZ), 3)]


PIT_A, PIT_B = pidx('SE2', 2), pidx('E3', -2)
pits = [inside(PIT_A), inside(PIT_B)]
# The lap is laid out above the way it was first built (south down the rampart, off its end); the user asked for it the other way round
# (2026-09-29): clockwise on the map, north up the rampart's street, so the jump takes off on the harbour side (J2's lip) and lands on the
# rampart's end (J1's), the start line on the east straight faces south, and the switchback comes down from the north rampart.
REVERSE = True
order = np.arange(n)[::-1].copy() if REVERSE else np.arange(n)
at_of = np.empty(n, int); at_of[order] = np.arange(n)          # a point's index in the lap as it runs
Xr, Zr, Hr = X[order], Z[order], H_road[order]
plan = {
    'originCellX': OX, 'originCellZ': OZ,
    'points': [[round(float(x - OX), 3), round(float(z - OZ), 3)] for x, z in zip(Xr, Zr)],
    'heights': [round(float(h), 1) for h in Hr],
    'maxGrade': round(float(max(grade.max(), -grade.min()) + 0.01), 3),
    'gapJump': [int(at_of[pidx('J2')]), int(at_of[pidx('J1')])] if REVERSE else [pidx('J1'), pidx('J2')],
    'pitA': pits[1] if REVERSE else pits[0],
    'pitB': pits[0] if REVERSE else pits[1],
    'pitTaper': 7,
}
json.dump(plan, open('E:/dump/LBAAssembler/docs/racetrack/citadel_storm_track_plan.json', 'w'))
json.dump({'plan': plan, 'marks': {nm: int(at_of[pidx(nm)]) for nm in names}}, open('design.json', 'w'))
np.save('design_lap.npy', np.stack([Xr, Zr], 1))
print('plan written: jump points %s, pit %s..%s, max grade %.3f' % (plan['gapJump'], plan['pitA'], plan['pitB'], plan['maxGrade']))

# ---- pictures ----
X0, X1, Z0, Z1, SC = 496, 584, 510, 640, 8
img = Image.open('terrain.png').convert('RGB')
img = img.crop(((X0 - OX) * 4, (Z0 - OZ) * 4, (X1 - OX) * 4, (Z1 - OZ) * 4)).resize(((X1 - X0) * SC, (Z1 - Z0) * SC), Image.NEAREST)
dr = ImageDraw.Draw(img)
def px(x, z): return ((x - X0) * SC, (z - Z0) * SC)
r0 = np.load('route_cells.npy'); dr.line([px(*p) for p in r0], fill=(170, 255, 60), width=1)
for i in range(n):
    j = (i + 1) % n
    t = (H_road[i] - LOW) / (TOP - LOW)
    col = (int(255 * t), int(255 * (1 - abs(2 * t - 1))), int(255 * (1 - t)))
    dr.line([px(X[i], Z[i]), px(X[j], Z[j])], fill=col, width=5)
for nm, x, z in CP:
    dr.text(px(x, z), nm, fill=(255, 255, 255))
for p in (plan['pitA'], plan['pitB']): dr.ellipse([px(p[0] + OX - .6, p[1] + OZ - .6), px(p[0] + OX + .6, p[1] + OZ + .6)], fill=(255, 140, 0))
img.save('design.png')
W, Hh = 1200, 300
pr = Image.new('RGB', (W, Hh), (20, 20, 30)); d2 = ImageDraw.Draw(pr)
TOPV = 4000
for i in range(n - 1):
    d2.line([(at[i] / L * W, Hh - 10 - H_road[i] / TOPV * (Hh - 20)), (at[i + 1] / L * W, Hh - 10 - H_road[i + 1] / TOPV * (Hh - 20))], fill=(255, 220, 0), width=2)
    if not np.isnan(g[i]): d2.point((at[i] / L * W, Hh - 10 - g[i] / TOPV * (Hh - 20)), fill=(120, 200, 120))
for nm in names:
    d2.text((sOf[nm] / L * W, 5 + (names.index(nm) % 3) * 10), nm, fill=(200, 200, 255))
pr.save('profile.png')
