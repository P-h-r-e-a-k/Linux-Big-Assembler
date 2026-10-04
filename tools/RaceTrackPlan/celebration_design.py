# Celebration Island (the statue's file, CELEBRA2): the lap of the user's drawing (CelebrationTrackStatue.png) -- from the dock where the
# taxi lands, round the island and up round the statue in a spiral to its shoulders, a ring round its head, and a bridge straight back
# down to the dock. Everything from the dock's cutting to the foot of the bridge is a raised road on piers (the plan's "raised" span):
# the engine's ground is one height map, and this lap passes over and under itself and stands in the air round the statue.
#
# Run with E:\dump\TEMP\celeb holding H.npy (the island's heights) and decorpoints.faces.csv (its decor bodies' triangles, from
# `ScriptRoundTrip decorpoints CELEBRA2 <folder>\decorpoints.csv`); celebration_mesh.py reads them. --pictures draws the lap over the island.
# Writes docs/racetrack/celebration_track_plan.json and pictures of the lap over the island.
import json, math, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import celebration_mesh as mesh

D = 'E:/dump/TEMP/celeb/'
OUT = 'E:/dump/LBAAssembler/docs/racetrack/celebration_track_plan.json'
ORIGIN = 448                      # the cube's first island cell (cube 7,7)
if not os.path.exists(D + 'H.npy'):
    # (the island's one cube, from the untouched file)
    from ile import load_ile
    np.save(D + 'H.npy', list(load_ile('E:/dump/LBA2RaceTrackBuild/Pristine/CELEBRA2.ILE')[3].values())[0].heights)
H = np.load(D + 'H.npy').astype(float)   # H[z, x], cells 0..64

# ---- the road
ASPHALT, CURB, DECK = 2.75, 3.5, 3.75     # half widths, cells: the asphalt, the curbs' outer edge, the raised road's edge (its rail)
STEP = 0.5
DOCK_X = 7.0                       # the start straight, down the dock
LOW = 420.0                        # the dock's height
QUAY = 570.0                       # the hairpin at the foot of the bridge: raised road too, a quay 150 over the dock (two surfaces
                                   # a few units apart flicker through each other), with a ramp down onto the dock after it
QUAY_RAMP = 3.0                    # cells of that ramp, along the dock
TOP = 16560.0                      # the ring round the head: just over the statue's shoulders (its boxes end at 16429)
HEAD = (34.8, 33.4); RING = 10.3   # the ring's middle and radius: its inner edge 6.5 cells from the head's middle, the head is 3.3 across
HAIRPIN = (11.0, 9.2); HAIRPIN_R = 4.0
R = 8.0                            # the spiral's corners

def fillet(vertices, radius):
    """a polyline with its corners rounded: list of points STEP apart"""
    out = [np.array(vertices[0], float)]
    def line(a, b):
        n = max(1, int(round(np.linalg.norm(b - a) / 0.05)))
        return [a + (b - a) * k / n for k in range(1, n + 1)]
    prev = np.array(vertices[0], float)
    for i in range(1, len(vertices) - 1):
        v = np.array(vertices[i], float); a = np.array(vertices[i - 1], float); b = np.array(vertices[i + 1], float)
        d1 = (v - a) / np.linalg.norm(v - a); d2 = (b - v) / np.linalg.norm(b - v)
        ang = math.acos(np.clip(d1 @ d2, -1, 1))
        t = radius * math.tan(ang / 2)
        p1 = v - d1 * t; p2 = v + d2 * t
        out += line(prev, p1)
        # the arc from p1 to p2
        side = np.sign(d1[0] * d2[1] - d1[1] * d2[0])
        n1 = np.array([-d1[1], d1[0]]) * side
        c = p1 + n1 * radius
        a0 = math.atan2(p1[1] - c[1], p1[0] - c[0]); a1 = a0 + side * ang
        m = max(2, int(round(radius * ang / 0.05)))
        out += [c + radius * np.array([math.cos(a0 + (a1 - a0) * k / m), math.sin(a0 + (a1 - a0) * k / m)]) for k in range(1, m + 1)]
        prev = p2
    out += line(prev, np.array(vertices[-1], float))
    return out

def arc(c, r, a0, a1):
    m = max(2, int(round(r * abs(a1 - a0) / 0.05)))
    return [np.array(c) + r * np.array([math.cos(a0 + (a1 - a0) * k / m), math.sin(a0 + (a1 - a0) * k / m)]) for k in range(1, m + 1)]

# the tangent from the ring (left by the lap at angle phi, heading (sin phi, -cos phi)) to the hairpin's circle, both turned the same way
C = np.array(HEAD); c2 = np.array(HAIRPIN)
dv = c2 - C; Dd = np.linalg.norm(dv); alpha = math.atan2(dv[1], dv[0])
phi = alpha + math.acos((RING - HAIRPIN_R) / Dd)
if math.sin(phi) * dv[0] - math.cos(phi) * dv[1] < 0: phi = alpha - math.acos((RING - HAIRPIN_R) / Dd)
P = C + RING * np.array([math.cos(phi), math.sin(phi)])
Q = c2 + HAIRPIN_R * np.array([math.cos(phi), math.sin(phi)])

WEST_B = 12.75                     # the second loop's west side: between the dock's road and the temple
fine = fillet([(DOCK_X, HAIRPIN[1]), (DOCK_X, 58.75), (59.0, 58.75), (59.0, 5.5), (WEST_B, 5.5), (WEST_B, 51), (52.5, 51), (52.5, 13.5),
               (HEAD[0] - RING, 13.5), (HEAD[0] - RING, HEAD[1])], R)
n_spiral = len(fine)
fine += arc(HEAD, RING, math.pi, phi)                # round the head: west, south, east, to the bridge's head
n_ring = len(fine)
m = int(round(np.linalg.norm(Q - P) / 0.05)); fine += [P + (Q - P) * k / m for k in range(1, m + 1)]
n_bridge = len(fine)
fine += arc(HAIRPIN, HAIRPIN_R, phi, -math.pi)[:-1]   # the hairpin back onto the dock
fine = np.array(fine)
# resample STEP apart round the closed lap
seg = np.linalg.norm(np.diff(np.vstack([fine, fine[:1]]), axis=0), axis=1)
cum = np.concatenate([[0], np.cumsum(seg)]); total = cum[-1]
N = int(round(total / STEP))
s = np.arange(N) * total / N
X = np.interp(s, cum, np.append(fine[:, 0], fine[0, 0])); Z = np.interp(s, cum, np.append(fine[:, 1], fine[0, 1]))
def at(k): return cum[k - 1] if k > 0 else 0.0
S_SPIRAL, S_RING, S_BRIDGE = at(n_spiral), at(n_ring), at(n_bridge)

# ---- heights: level on the dock, one grade up the spiral to the top, level round the head, straight down the bridge; the kinks rounded
CLIMB_FROM = 33.0 - HAIRPIN[1]                      # along the dock: where the climb starts (z = 33)
# the climb ends where the spiral turns in over the statue's shoulders (the last corner, from the north leg onto the west side of the ring)
corner_start = None
for k in range(n_spiral):
    if abs(fine[k][1] - 13.5) < 1e-6 and fine[k][0] <= HEAD[0] - RING + R + 1e-6 and k > n_spiral * 0.8: corner_start = at(k); break
CLIMB_TO = corner_start
def height(sv):
    if sv <= QUAY_RAMP: return QUAY + (LOW - QUAY) * sv / QUAY_RAMP
    if sv <= CLIMB_FROM: return LOW
    if sv <= CLIMB_TO: return LOW + (TOP - LOW) * (sv - CLIMB_FROM) / (CLIMB_TO - CLIMB_FROM)
    if sv <= S_RING: return TOP
    if sv <= S_BRIDGE: return bridge(sv - S_RING)
    return QUAY                 # the hairpin, level
# the bridge: it leaves the ring over the statue's right shoulder, so it starts as a parabola (level at the ring, steepening over
# BRIDGE_IN cells), runs straight, and levels out over its last BRIDGE_OUT cells onto the dock
BRIDGE_IN, BRIDGE_OUT = 5.5, 4.0
def bridge(t):
    L = S_BRIDGE - S_RING
    m = (TOP - QUAY) / (L - (BRIDGE_IN + BRIDGE_OUT) / 2)         # the straight part's fall per cell
    if t <= BRIDGE_IN: return TOP - m * t * t / (2 * BRIDGE_IN)
    if t <= L - BRIDGE_OUT: return TOP - m * BRIDGE_IN / 2 - m * (t - BRIDGE_IN)
    u = L - t
    return QUAY + m * u * u / (2 * BRIDGE_OUT)
raw = np.array([height(v) for v in s])
def rounded(h, window):
    """vertical curves: the profile averaged over `window` cells twice (a kink becomes two parabolas)"""
    w = max(1, int(round(window / STEP)))
    k = np.ones(w) / w
    pad = np.concatenate([h[-2 * w:], h, h[:2 * w]])
    return np.convolve(np.convolve(pad, k, 'same'), k, 'same')[2 * w:-2 * w]
Y = rounded(raw, 3.0)
bridge_part = s >= S_RING - 1e-9
Y[bridge_part] = raw[bridge_part]          # (the bridge's own curves are its profile, and the quay is level)
ramp = s <= QUAY_RAMP + 2.0
Y[ramp] = raw[ramp]
grade = np.diff(np.append(Y, Y[0])) / (STEP * 512)
i_spiral, i_ring, i_bridge = [int(round(v / total * N)) for v in (S_SPIRAL, S_RING, S_BRIDGE)]
i_climb0, i_climb1 = int(round(CLIMB_FROM / total * N)), int(round(CLIMB_TO / total * N))
print(f'lap {total:.1f} cells, {N} points; climb {CLIMB_TO - CLIMB_FROM:.1f} cells at {grade[i_climb0 + 20:i_climb1 - 20].max() * 100:.1f} %; '
      f'ring {S_RING - CLIMB_TO:.1f} cells level at {TOP:.0f}; bridge {S_BRIDGE - S_RING:.1f} cells, steepest {-grade.min() * 100:.0f} %; hairpin {total - S_BRIDGE:.1f} cells')
print(f'bridge from ({P[0]:.1f}, {P[1]:.1f}) to ({Q[0]:.1f}, {Q[1]:.1f}), heading ({math.sin(phi):.2f}, {-math.cos(phi):.2f})')

# ---- what is raised: from where the climb comes out of the hill south of the dock, over the sea, to the foot of the bridge
def ground(x, z):
    xi, zi = min(63, max(0, int(x))), min(63, max(0, int(z)))
    fx, fz = x - xi, z - zi
    return (H[zi, xi] * (1 - fx) * (1 - fz) + H[zi, xi + 1] * fx * (1 - fz) + H[zi + 1, xi] * (1 - fx) * fz + H[zi + 1, xi + 1] * fx * fz)
G = np.array([ground(x, z) for x, z in zip(X, Z)])
RAISE_FROM_Z = 46.0                # on the dock's line: the cutting through the hill ends here, the sea begins
raised0 = int(np.argmax((np.abs(X - DOCK_X) < 1e-6) & (Z >= RAISE_FROM_Z) & (s < 60)))
raised1 = int(round(QUAY_RAMP / STEP))   # past the lap's first point: the hairpin is a quay, and its ramp comes down onto the dock
raised = [k % N for k in range(raised0, N + raised1 + 1)]      # (it runs on through the lap's first point)
print(f'raised from point {raised0} (cell {X[raised0]:.1f}, {Z[raised0]:.1f}, height {Y[raised0]:.0f}) to point {raised1} ({X[raised1]:.1f}, {Z[raised1]:.1f}, {Y[raised1]:.0f}): {len(raised) * STEP:.0f} cells')

# ---- checks
T = np.stack([np.gradient(X), np.gradient(Z)], 1); T /= np.linalg.norm(T, axis=1)[:, None]
Nn = np.stack([-T[:, 1], T[:, 0]], 1)
issues = []
# 1. the cube's edge: the engine sends Twinsen to the open sea half a cell from it
edge = np.minimum.reduce([X, Z, 64 - X, 64 - Z])
print(f'nearest the cube edge: {edge.min():.1f} cells (centre line)')
# 2. the lap against itself: where two parts lie over each other in plan, the height between them
idx = np.arange(N)
worst = (1e9, 0, 0)
cross = []
for i in range(0, N, 2):
    d = np.hypot(X - X[i], Z - Z[i])
    sep = np.abs(idx - i); sep = np.minimum(sep, N - sep)
    near = (d < 2 * DECK - 0.6) & (sep > 40)
    for j in np.nonzero(near)[0]:
        dy = abs(Y[i] - Y[j])
        cross.append((i, j, d[j], dy))
        if dy < worst[0]: worst = (dy, i, j)
print(f'parts of the lap over each other in plan ({len(cross)} pairs within {2 * DECK - 0.6} cells): least height between them {worst[0]:.0f} '
      f'(points {worst[1]} and {worst[2]}: cells ({X[worst[1]]:.1f},{Z[worst[1]]:.1f}) at {Y[worst[1]]:.0f} and ({X[worst[2]]:.1f},{Z[worst[2]]:.1f}) at {Y[worst[2]]:.0f})')
# 3. the decor bodies (the statue, the temple, the columns): nothing within the road's space (its slab and a car's height over it)
pts, body = mesh.cloud()
px, py, pz = pts[:, 0] / 512, pts[:, 1], pts[:, 2] / 512
hits = {}
for k in raised:
    near = (np.abs(px - X[k]) < DECK + 1.2) & (np.abs(pz - Z[k]) < DECK + 1.2)
    if not near.any(): continue
    q = np.nonzero(near)[0]
    along = (px[q] - X[k]) * T[k, 0] + (pz[q] - Z[k]) * T[k, 1]; lat = (px[q] - X[k]) * Nn[k, 0] + (pz[q] - Z[k]) * Nn[k, 1]
    inside = (np.abs(along) <= STEP) & (np.abs(lat) <= DECK + 0.25)
    dy = py[q][inside] - Y[k]
    bad = (dy > -260) & (dy < 1300)
    for b in np.unique(body[q][inside][bad]): hits.setdefault(int(b), []).append(k)
for b, ks in sorted(hits.items()):
    print(f'  body {b} in the road\'s space at points {min(ks)}..{max(ks)} ({len(ks)} points), e.g. cell ({X[ks[0]]:.1f},{Z[ks[0]]:.1f}) road at {Y[ks[0]]:.0f}')
# 4. the raised road over the ground
low = [(k, Y[k] - G[k]) for k in raised[:-1] if Y[k] - 200 < G[k]]
if low: print(f'  the raised road is into the ground at {len(low)} points, e.g. point {low[0][0]} ({X[low[0][0]]:.1f},{Z[low[0][0]]:.1f}) road {Y[low[0][0]]:.0f} ground {G[low[0][0]]:.0f}')

START = int(round((29.0 - HAIRPIN[1]) / total * N))     # the start line: on the dock at z = 29, the grid's five spots behind it
plan = {
    'originCellX': ORIGIN, 'originCellZ': ORIGIN,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in zip(X, Z)],
    'heights': [round(float(y), 1) for y in Y],
    'maxGrade': 0.16,
    'asphaltHalf': ASPHALT, 'curbHalf': CURB, 'vergeHalf': 4.5, 'blend': 3.0,
    'raised': [raised0, raised1], 'raisedHalf': DECK,
    'start': START,
    'seaClearance': LOW,
    # where the other cars wait while Twinsen qualifies: on the apron east of the dock, facing the road
    'pitSpots': [[14.0, 16.2, -1.0, 0.0], [14.0, 18.9, -1.0, 0.0], [14.0, 21.6, -1.0, 0.0], [16.2, 17.2, -1.0, 0.0], [16.2, 19.9, -1.0, 0.0]],
    # the statue (and the pillar under its hand) and the temple stay where the road passes over them
    'keepBodies': [12, 16, 17, 18, 19, 20, 21, 0, 1, 10, 11, 13],
}
json.dump(plan, open(OUT, 'w'))
np.savez(D + 'lap.npz', X=X, Z=Z, Y=Y, raised0=raised0, raised1=raised1, T=T, Nn=Nn, G=G)
print('wrote', OUT)

if '--pictures' in sys.argv:
    # the raised road and its piers as triangles, over the island
    tris = []; kinds = []
    for k in raised[:-1]:
        a, b = k % N, (k + 1) % N
        l0 = np.array([X[a] - Nn[a, 0] * DECK, Y[a], Z[a] - Nn[a, 1] * DECK]); r0 = np.array([X[a] + Nn[a, 0] * DECK, Y[a], Z[a] + Nn[a, 1] * DECK])
        l1 = np.array([X[b] - Nn[b, 0] * DECK, Y[b], Z[b] - Nn[b, 1] * DECK]); r1 = np.array([X[b] + Nn[b, 0] * DECK, Y[b], Z[b] + Nn[b, 1] * DECK])
        for t in ([l0, l1, r1], [l0, r1, r0]):
            t = np.array(t); t[:, 0] *= 512; t[:, 2] *= 512; tris.append(t); kinds.append(100 if k < i_ring else 101)
        if (k - raised0) % 16 == 8 and Y[a] - G[a] > 600:
            # a pier
            x0, z0 = X[a] * 512, Z[a] * 512; w = 230
            for (ax, az, bx, bz) in ((-w, -w, w, -w), (w, -w, w, w), (w, w, -w, w), (-w, w, -w, -w)):
                p0 = [x0 + ax, G[a], z0 + az]; p1 = [x0 + bx, G[a], z0 + bz]; p2 = [x0 + bx, Y[a] - 200, z0 + bz]; p3 = [x0 + ax, Y[a] - 200, z0 + az]
                tris.append(np.array([p0, p1, p2])); tris.append(np.array([p0, p2, p3])); kinds += [102, 102]
    extra = (np.array(tris), np.array(kinds))
    for v in ('west', 'south', 'north', 'east', 'top'): mesh.render(v, D + f'design_{v}.png', extra=extra)
    print('pictures written')
