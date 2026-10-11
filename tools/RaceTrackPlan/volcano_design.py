# Volcano Island (SOUSCELB.ILE, the game's island 11 -- under Celebration Island; cells 448..576 each way, cube (7,8) not there). First
# from the user's sketch (2026-10-08: "Pink are jump points, green is track, orange is pitlane. For this track let's add lava balls shooting
# up and out of the lava at points and raining down onto the track"; repo root volcanoTrack.png, the editor's minimap turned half a turn and
# stretched: E:\dump\TEMP\volc\centreline.py). Redesigned on 2026-10-10 (the user: "Suggest a total redesign for the track, perhaps with the
# lava waterfall in a new location as pictured, perhaps as tall as the level allows that shoots lava balls" -- the pictured place the main
# lava channel's north bank -- and "The island heavily features red crystal scenary, perhaps we could make this a feature too"; the
# proposal, E:\dump\TEMP\volc\proposal.png, taken as it was: "make the changes you suggested along with these fixes" -- the pit lane "too
# hard to drive into", the boost panel's boost "a lot longer"). One raised road round the island, clockwise:
#
#  1. the lava falls: the ground north of the main lava channel raised into the island's summit, 30,000 (the island's ground holds 32,767;
#     twice the plateau), a lake of lava on top pouring over its south rim in four tiers down into the channel (RaceTrackLavaFall);
#  -  the start on the sand plateau's north straight (12,750), the pit lane on its right (inside the lap), the boost panel in it;
#  2. right at its east end, south down the plateau's east edge under the summit's west cliff, and left between the crystal gate off the
#     plateau's edge;
#  3. the canyon road: east down along the channel at the grade's limit, the falls on the left, balls of lava shot out of their foot;
#  4. the drop jump off the canyon's end, east over the lava lowland down onto the east ridge (the camera beside it: the falls behind);
#  5. south along the east ridge under a crystal arch, the crystal field in the lava below on the right (6) (another arch over the plateau's
#     west side);
#  7. the shore jump south-west down to the south shore, west along it;
#  8. the brazier climb back up the volcano's flank (the island's bowls of lava spitting balls) onto the plateau's south edge, its west side;
#  9. the crystal garden inside the plateau's loop.
#
# Run with E:\dump\TEMP\volc holding H.npy and obstacles.npz (E:\dump\TEMP\otr\prep.py E:/dump/TEMP/volc 448 448 128); --pictures draws the
# lap over boxmap.png; --write writes docs/racetrack/volcano_track_plan.json.
import json, math, sys
import numpy as np

D = 'E:/dump/TEMP/volc/'
OUT = 'E:/dump/LBAAssembler/docs/racetrack/volcano_track_plan.json'
OX, OZ = 448, 448
H = np.nan_to_num(np.load(D + 'H.npy').astype(float), nan=0.0)
OB = np.load(D + 'obstacles.npz'); TOP, BOT, WHO, RQ = OB['top'], OB['bot'], OB['who'], int(OB['R'])
PRESENT = {(7, 7), (8, 7), (8, 8)}
SIZE = 128

def ground(x, z):
    xi, zi = min(SIZE - 1, max(0, int(x))), min(SIZE - 1, max(0, int(z))); fx, fz = x - xi, z - zi
    return H[zi, xi] * (1 - fx) * (1 - fz) + H[zi, xi + 1] * fx * (1 - fz) + H[zi + 1, xi] * (1 - fx) * fz + H[zi + 1, xi + 1] * fx * fz

STEP = 0.5
H0 = 4.75                               # (4.0 until 2026-10-10: "make the entire track slightly wider if we can")
ASPHALT, CURB = H0 - 1.0, H0 - 0.25
PIT = 4.5                               # the pit lane beside the plateau's north straight, on its right (south: inside the lap)
TOPD = 13000.0                          # the plateau's road (over its sand, 12,750)
NZ = 461.5                              # the north straight's race lanes' middle
SZ = NZ + PIT / 2                       # the deck's middle there (the pit lane south of the race lanes: the deck's middle south of theirs)

def C(x, z): return (x - OX, z - OZ)
# the corners in the lap's order, each (x, z) and the radius it is rounded with (island cells)
K = [((507.0, SZ), 5.0),                # the north straight's east end: right, south down the plateau's east edge, the summit on the left
     ((507.0, 486.0), 5.0),             # left, east, between the crystal gate off the plateau's edge: the canyon road along the falls ...
     ((568.5, 487.0), 4.0),             # ... and over the drop jump off its end onto the east ridge: right, south along it (the island's
     ((568.0, 551.0), 4.0),             #     edge x 576), through the crystal arch; south-west: the shore jump ...
     ((556.0, 566.0), 4.0),             # ... west along the shore ...
     ((538.0, 566.0), 5.0),             # ... north up the volcano's flank, the brazier climb ...
     ((537.0, 556.0), 4.0),
     ((553.0, 547.0), 4.0),
     ((555.0, 538.0), 4.0),             # ... north-west, climbing onto the plateau ...
     ((512.0, 497.0), 8.0),             # ... west along its south edge ...
     ((462.0, 497.0), 6.0),             # ... north up its west side ...
     ((461.0, SZ), 6.0)]                # ... east along its north straight (the start, the pit lane on the right)
K = [(C(*p), r) for p, r in K]

def rounded_polyline(K):
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
        if np.dot(q2 - q1, P[(i + 1) % n] - P[i]) < -1e-6: sys.exit(f'corners {i} and {i + 1}: their roundings overlap')
        m = max(1, int(round(np.linalg.norm(q2 - q1) / 0.02)))
        for k in range(m): out.append(q1 + (q2 - q1) * k / m)
    return np.array(out)

fine = rounded_polyline(K)
START = (490.0, SZ)                     # the start line on the north straight (heading east: its grid behind it, west, on the level)
k0 = int(np.argmin(np.hypot(fine[:, 0] - C(*START)[0] + 20, fine[:, 1] - C(*START)[1])))
fine = np.roll(fine, -k0, axis=0)
seg = np.linalg.norm(np.diff(np.vstack([fine, fine[:1]]), axis=0), axis=1)
cum = np.concatenate([[0], np.cumsum(seg)]); total = cum[-1]
N = int(round(total / STEP)); s = np.arange(N) * total / N
X = np.interp(s, cum, np.append(fine[:, 0], fine[0, 0])); Z = np.interp(s, cum, np.append(fine[:, 1], fine[0, 1]))
T = np.stack([np.gradient(X), np.gradient(Z)], 1); T /= np.linalg.norm(T, axis=1)[:, None]
Nn = np.stack([-T[:, 1], T[:, 0]], 1)    # across the road (the builder's Across, (-Tz, Tx): south when heading east -- the pit lane is on +Nn)
def nearest(p, heading=None):
    p = C(*p); d = np.hypot(X - p[0], Z - p[1])
    if heading is not None: d = d + 100 * (T @ np.array(heading) < 0.7)
    return int(np.argmin(d))
def ahead(a, b): return ((b - a) % N) * STEP
def between(k, a, b): return ahead(a, k) <= ahead(a, b)
def ease(f): f = min(1, max(0, f)); return f * f * (3 - 2 * f)

E, W_, S_, N_ = (1, 0), (-1, 0), (0, 1), (0, -1)
SW_ = (-0.625, 0.781)
# ---- widths: the usual, and the north straight with its pit lane on its right (south: +Nn heading east). Easier to drive into since
# 2026-10-10 (the user: "the pitlane is too hard to drive into"): the deck widened over 8 cells from the north-west bend's end (6 until then,
# right where the fence began, the way in a slot two cells wide at the fence's first post), the fence 7 cells on from where it is full
# width -- a car keeping to the right out of the bend is in the pit lane without turning -- and ending sooner.
def ramp(a, b, k): return ease(ahead(a, k) / ahead(a, b))
HALF = np.full(N, H0)
PIT_HALF = H0 + PIT / 2
PIT_MID = PIT_HALF - PIT / 2
iPit0 = nearest((466.0, SZ), E); iPit1 = nearest((474.0, SZ), E); iPit2 = nearest((499.0, SZ), E); iPit3 = nearest((503.5, SZ), E)
iFence0 = nearest((481.0, SZ), E); iFence1 = nearest((497.0, SZ), E)
for k in range(N):
    if between(k, iPit0, iPit1): HALF[k] = max(HALF[k], H0 + (PIT_HALF - H0) * ramp(iPit0, iPit1, k))
    elif between(k, iPit1, iPit2): HALF[k] = PIT_HALF
    elif between(k, iPit2, iPit3): HALF[k] = max(HALF[k], PIT_HALF + (H0 - PIT_HALF) * ramp(iPit2, iPit3, k))
PIT_WAIT = (482.5, 486.0, 489.5)        # the opponents' waiting spots in the pit lane, within the fence, behind the line (cube (7,7))

# ---- heights, close over the ground (as Otringal's version 2): CLEAR over the median of the ground across the deck (a rock at one rail is
# cut away, RaisedCut), at least LAVA_MIN over the lava (its ground is 0..700), then spread out at GRADE each way and eased -- but not
# across a jump's gap (the drop jump's landing is far under its lip)
MARK = {
    'start':     nearest(START, E),
    'turnS':     nearest((507, 472), S_),         # down the plateau's east edge
    'canyon0':   nearest((514, 486), E),          # the canyon road
    'canyon1':   nearest((528, 486), E),
    'canyon2':   nearest((542, 486), E),
    'eastN':     nearest((568, 497), S_),         # the east ridge
    'arch':      nearest((568, 527), S_),
    'eastS':     nearest((568, 546), S_),
    'shoreE':    nearest((548, 566), W_),         # the south shore
    'shoreW':    nearest((538, 563), N_),
    'flank1':    nearest((554, 543), N_),         # up the volcano's flank
    'flank2':    nearest((530, 520), (-0.72, -0.69)),
    'plateauS':  nearest((505, 497), W_),         # onto the plateau
    'plateauW':  nearest((462, 480), N_),
}
# the jumps the engine carries the car over: (name, foot, lip, landing lip, landing foot as (x, z, heading)), the ramp's angle, the camera
# (0 behind the car, 1 beside it on the side Across points to: south of the drop jump, the falls behind the car)
JUMPS = [('drop', (542.5, 486.5, E), (547.0, 486.6, E), (560.5, 486.9, E), (563.5, 487.0, E), 6.0, 1),
         ('shore', (567.4, 552.8, SW_), (565.6, 555.0, SW_), (559.6, 562.5, SW_), (558.6, 563.7, SW_), 16.0, 0)]
JI = [(name, nearest(f[:2], f[2]), nearest(l[:2], l[2]), nearest(ld[:2], ld[2]), nearest(lf[:2], lf[2]), deg, cam) for name, f, l, ld, lf, deg, cam in JUMPS]
# (only the drop jump's: its landing is far under its lip; the shore jump's landing hill is shaped by its lip's height as before)
def in_flight(k): return any(between(k, iL, iD) and k != iD for name, iF, iL, iD, iDF, deg, cam in JI if name == 'drop')

CLEAR, LAVA, LAVA_MIN, GRADE = 400.0, 700.0, 1400.0, 0.17
gs = np.array([np.median([ground(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o) for o in np.linspace(-HALF[k], HALF[k], 9)]) for k in range(N)])
floor = np.where(gs < LAVA, LAVA_MIN, np.maximum(gs + CLEAR, LAVA_MIN))
g = GRADE * STEP * 512
F = floor.copy()
for _ in range(3):
    for k in range(N):
        j = (k + 1) % N
        if not in_flight(k): F[j] = max(F[j], F[k] - g)
    for k in range(N - 1, -1, -1):
        j = (k + 1) % N
        if not in_flight(k): F[k] = max(F[k], F[j] - g)
# (the drop jump's gap, from its lip to its landing, a straight line for the easing: the ground's heights under it pulled the road down
# either side)
for name, iF, iL, iD, iDF, deg, cam in JI:
    if name != 'drop': continue
    n_ = int(ahead(iL, iD) / STEP)
    for q in range(1, n_): F[(iL + q) % N] = F[iL] + (F[iD] - F[iL]) * q / n_
Y = F.copy()
for k in range(N): Y[k] = np.mean(F[[(k + d) % N for d in range(-6, 7)]])
Y = np.maximum(Y, F - 60)

def flight_heights(iFoot, iLip, iLand, iLandFoot, theta, top_needed, land_at=None):
    y0, y3 = Y[iFoot], Y[iLandFoot]
    k1 = ahead(iFoot, iLip) * 512; L_ = ahead(iLip, iLand) * 512; k2 = ahead(iLand, iLandFoot) * 512
    R1 = k1 / math.sin(theta); yl = y0 + R1 * (1 - math.cos(theta)); sl = math.tan(theta)
    def at(d):
        if d <= k1: return y0 + R1 - math.sqrt(max(0, R1 * R1 - d * d))
        if d <= k1 + L_:
            u = d - k1
            a = (land_y - yl - sl * L_) / (L_ * L_)
            return yl + sl * u + a * u * u
        u = d - k1 - L_; f = u / k2
        return land_y + (y3 - land_y) * ease(f)
    land_y = max(y3 + 300, min(yl, top_needed)) if land_at is None else land_at
    return at, yl, land_y

gap = np.zeros(N, bool); arcs = []
for name, iF, iL, iD, iDF, deg, cam in JI:
    at, yl, land_y = flight_heights(iF, iL, iD, iDF, math.radians(deg), Y[iDF] + 600)
    k = iF
    while True:
        Y[k] = at(ahead(iF, k) * 512)
        if k == iDF: break
        k = (k + 1) % N
    k = (iL + 1) % N
    while k != iD: gap[k] = True; k = (k + 1) % N
    arcs.append((name, iF, iL, iD, iDF, cam))

# ---- 1. the lava falls (RaceTrackLavaFall): the summit over the channel's north bank, its south face's foot at z 473 from x 518 to 548,
# 30,000 at its top (its rim's rock; its lake of lava a little under it), four tiers down to the channel; rock and lava copied from the
# island's own (cells (530, 466) and (515, 479))
FALL_AT, FALL_W = (533.0, 473.0), 30.0
lava_fall = {'at': [FALL_AT[0] - OX, FALL_AT[1] - OZ], 'facing': [0.0, 1.0], 'width': FALL_W, 'top': 30000.0, 'depth': 20.0, 'wing': 3.0,
             'pool': 1.0, 'tiers': 4, 'tierDepth': 2.0, 'rock': [530.0 - OX, 466.0 - OZ], 'lava': [515.0 - OX, 479.0 - OZ]}

# ---- lava balls: from points in the lava beside the lap, onto the road -- each source (x, z[, its height: a bowl's brim]), the road it
# rains on (from a point to a point: the race-track mode picks a place along it each time), every so many ms
def P_(x, z, h=None): return [x - OX, z - OZ] + ([h] if h is not None else [])
LAVA = [  # 3. out of the falls' foot (the channel at the face), onto the canyon road beside each
        (P_(518.0, 474.5), nearest((512, 486), E), nearest((524, 486), E), 3600),
        (P_(525.0, 474.5), nearest((519, 486), E), nearest((531, 486), E), 3900),
        (P_(532.0, 474.5), nearest((526, 486), E), nearest((538, 486), E), 3500),
        (P_(539.0, 474.5), nearest((533, 486), E), nearest((544, 486), E), 4100),
        (P_(545.5, 474.5), nearest((538, 486), E), nearest((545, 486), E), 3800),
          # 6. geysers in the lowland lava, onto the east ridge
        (P_(556.0, 506.0), MARK['eastN'], nearest((568, 515), S_), 5200),
        (P_(557.0, 532.0), nearest((568, 522), S_), MARK['eastS'], 4900),
          # 8. the island's bowls of lava up the flank (their brims), onto the climb
        (P_(541.5, 549.5, 4700.0), nearest((538, 560), N_), nearest((550, 545), (0.85, -0.5)), 4700),
        (P_(543.5, 534.5, 5550.0), MARK['flank1'], nearest((540, 528), (-0.72, -0.69)), 4400),
          # the south shore's (kept)
        (P_(556.0, 572.0), MARK['shoreE'], MARK['shoreW'], 5000),
        (P_(526.0, 560.0), MARK['shoreW'], MARK['flank1'], 5900)]
lava = [{'source': src, 'from': int(a), 'to': int(b), 'every': every} for src, a, b, every in LAVA]

# ---- the crystals (RaceTrackCrystals): the island's red crystal clusters (its bodies 1 and 2) made big -- each where it stands (cells),
# which, how much bigger, its turn (degrees) and its lean (degrees, the way its turn faces). The builder makes one smaller till it is clear
# of the road (2,000 over the deck where it is over it).
CRYSTALS = [  # 2. the crystal gate either side of the way off the plateau into the canyon (north of it the summit's cliff leaves no room)
            (499.5, 490.0, 2, 3.0, 135, 0), (521.0, 494.5, 1, 3.0, 300, 0),
              # 6. the crystal field in the lava lowland -- south of z 512: the drop jump's camera flies beside it 18 cells south of its way,
              #    and a crystal there stood in front of it
            (557.5, 516.0, 2, 3.0, 300, 10), (551.0, 517.0, 1, 3.5, 250, 5), (552.0, 524.0, 2, 3.5, 80, 7), (557.0, 529.0, 1, 3.0, 20, 8),
            (558.0, 538.0, 1, 3.0, 190, 9), (553.0, 540.0, 2, 3.5, 330, 6),
              # 9. the crystal garden inside the plateau's loop
            (475.0, 476.0, 1, 3.0, 20, 0), (486.0, 480.0, 2, 3.5, 160, 0), (495.0, 476.0, 1, 2.5, 280, 4), (474.0, 488.0, 2, 3.0, 100, 0),
            (486.0, 489.0, 1, 3.0, 230, 3), (496.0, 487.0, 2, 2.5, 60, 0)]
crystals = [{'at': [x - OX, z - OZ], 'body': b, 'scale': sc, 'turn': t, 'lean': l} for x, z, b, sc, t, l in CRYSTALS]
# 5. the crystal arches over the road (RaceTrackCrystals.Arch): on the east ridge, and over the plateau's west side
ARCHES = [(568.0, 527.0), (461.5, 481.0)]
arches = [{'at': [x - OX, z - OZ], 'spread': 3.0} for x, z in ARCHES]

# ---- the boost panel (RACEMOD.CPP boostpanel=: a car with two wheels on it is at 120 km/h at once, held a while): in the pit lane,
# past the start line and the opponents' waiting spots -- its first and last point, how far across the road (Across, cells)
BOOST = [(491.5, 496.5)]
boost = [[int(nearest((a, SZ), E)), int(nearest((b, SZ), E)), PIT_MID - 1.5, PIT_MID + 1.5] for a, b in BOOST]

grade = (np.roll(Y, -1) - Y) / (STEP * 512)
ride = ~np.zeros(N, bool)
for name, iF, iL, iD, iDF, cam in arcs:
    for k in range(N):
        if between(k, iF, iDF): ride[k] = False

# ---- checks
out = [f'lap {total:.1f} cells, {N} points; heights {Y.min():.0f} to {Y.max():.0f}; steepest driven {np.abs(grade[ride]).max() * 100:.1f} %']
steep = sorted(((abs(grade[k]), k) for k in range(N) if ride[k]), reverse=True)
sp = []
for g_, k in steep:
    if all(min(abs(k - j), N - abs(k - j)) > 20 for j in sp): sp.append(k)
    if len(sp) == 5: break
out.append('  steepest at: ' + ', '.join(f'{abs(grade[k]) * 100:.1f}% point {k} ({X[k] + OX:.0f}, {Z[k] + OZ:.0f})' for k in sp))
def edge_dist(x, z):
    x, z = x + OX, z + OZ; best = 1e9
    for dz in (-1, 0, 1):
        for dx in (-1, 0, 1):
            q = (int(x // 64) + dx, int(z // 64) + dz)
            if q in PRESENT: continue
            qx = min(max(x, q[0] * 64), q[0] * 64 + 64); qz = min(max(z, q[1] * 64), q[1] * 64 + 64)
            best = min(best, math.hypot(x - qx, z - qz))
    return best
edge = np.array([edge_dist(x, z) - HALF[k] for k, (x, z) in enumerate(zip(X, Z))])
out.append(f'  the rail nearest a missing cube: {edge.min():.1f} cells at ({X[edge.argmin()] + OX:.1f}, {Z[edge.argmin()] + OZ:.1f})')
clear = sorted((Y[k] - max(ground(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o) for o in np.linspace(-HALF[k], HALF[k], 9)), k) for k in range(N) if not gap[k])
out.append('  the deck under the ground: ' + ', '.join(f'{c:.0f} at ({X[k] + OX:.1f}, {Z[k] + OZ:.1f}) point {k}' for c, k in clear[:6] if c < 300))
hits = {}
for k in range(N):
    if gap[k]: continue
    for o in np.linspace(-HALF[k] - 0.3, HALF[k] + 0.3, 17):
        x, z = X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o
        i, j = int(z * RQ), int(x * RQ)
        if not (0 <= i < TOP.shape[0] and 0 <= j < TOP.shape[1]) or TOP[i, j] < 0: continue
        if TOP[i, j] > Y[k] - 350 and BOT[i, j] < Y[k] + 1400:
            b = int(WHO[i, j]); hits.setdefault(b, []).append((round(float(x + OX), 1), round(float(z + OZ), 1), int(TOP[i, j]), int(Y[k])))
out.append('  decor in the road\'s space (body: places, e.g.): ' + '; '.join(f'{b}: {len(h)} {h[0]}' for b, h in sorted(hits.items())))
for name, iF, iL, iD, iDF, cam in arcs:
    flight = [Y[q % N] for q in range(iL, iD + (N if iD < iL else 0))]
    out.append(f'  jump {name}: lip {Y[iL]:.0f}, landing {Y[iD]:.0f}, top {max(flight):.0f}, {ahead(iL, iD):.1f} cells; foot {Y[iF]:.0f} landing foot {Y[iDF]:.0f}')
idx = np.arange(N); close = []
for i in range(0, N, 2):
    if gap[i]: continue
    dd = np.hypot(X - X[i], Z - Z[i]); sep = np.minimum(np.abs(idx - i), N - np.abs(idx - i))
    nearby = (sep > 40) & (dd < HALF + HALF[i] + 0.6) & (np.abs(Y - Y[i]) < 2600) & ~gap
    if nearby.any(): close.append((dd[nearby].min(), i, int(np.argmax(nearby))))
for dmin, i, j in sorted(close)[:4]:
    out.append(f'  CLASH: ({X[i] + OX:.1f}, {Z[i] + OZ:.1f}) {Y[i]:.0f} and ({X[j] + OX:.1f}, {Z[j] + OZ:.1f}) {Y[j]:.0f}: {dmin:.1f} cells apart')
for l in lava:
    sx, sz = l['source'][:2]
    out.append(f'  lava ball from ({sx + OX:.0f}, {sz + OZ:.0f}) (ground {ground(sx, sz):.0f}) onto points {l["from"]}..{l["to"]}, {min(np.hypot(X - sx, Z - sz)):.1f} cells from the road')
for m, k in MARK.items():
    out.append(f'    {m:9s} point {k:4d} ({X[k] + OX:6.1f}, {Z[k] + OZ:6.1f}) {Y[k]:6.0f} (ground {ground(X[k], Z[k]):5.0f})')
out.append(f'  pit lane points {iPit1}..{iPit2}, its fence {iFence0}..{iFence1}; start point {MARK["start"]}')
print('\n'.join(out))

plan = {
    'originCellX': OX, 'originCellZ': OZ,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in zip(X, Z)],
    'heights': [round(float(y), 1) for y in Y],
    'maxGrade': round(float(np.abs(grade).max()) + 0.05, 3),
    'raised': [0, N - 1], 'raisedHalf': H0, 'asphaltHalf': ASPHALT, 'curbHalf': CURB,
    'raisedHalfs': [round(float(h), 3) for h in HALF],
    'arcJumps': [[int(iF), int(iL), int(iD), int(iDF), int(cam)] for name, iF, iL, iD, iDF, cam in arcs],
    'start': int(MARK['start']),
    # the stripe between the pit lane and the race lanes (south of them, +Across), first and last point (the fence's); the opponents'
    # waiting spots in the pit lane's middle, facing along the straight (east), on the deck; Citadel Island's white fence along the
    # stripe; the grid on the race lanes (north of the deck's middle, -Across)
    'pitStripe': [int(iFence0), int(iFence1), round(float(PIT_HALF - PIT), 3)],
    'pitSpots': [[x - OX, round(SZ + PIT_MID - OZ, 3), 1.0, 0.0, TOPD] for x in PIT_WAIT],
    'pitFence': True,
    'gridShift': -PIT / 2,
    'raisedCut': True,
    'lavaBalls': lava,
    'lavaFalls': [lava_fall],
    'crystals': crystals,
    'crystalArches': arches,
    'boostPanels': boost,
}
if '--write' in sys.argv:
    json.dump(plan, open(OUT, 'w'))
    print('written', OUT)
np.savez(D + 'lap.npz', X=X + OX, Z=Z + OZ, Y=Y, HALF=HALF, gap=gap)

if '--pictures' in sys.argv:
    from PIL import Image, ImageDraw
    Zm = 8
    img = Image.open(D + 'boxmap.png').convert('RGB')
    d = ImageDraw.Draw(img)
    P = lambda x, z: (x * Zm, z * Zm)
    lo, hi = Y.min(), Y.max()
    for k in range(N):
        j = (k + 1) % N
        t = (Y[k] - lo) / (hi - lo)
        col = (int(255 * t), int(255 * (1 - abs(2 * t - 1))), int(255 * (1 - t)))
        if gap[k]: col = (255, 255, 255)
        for o in (-HALF[k], HALF[k]):
            d.line([P(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o), P(X[j] + Nn[j, 0] * o, Z[j] + Nn[j, 1] * o)], fill=col, width=2 if not gap[k] else 1)
    for l in lava:
        sx, sz = l['source'][:2]
        d.ellipse([P(sx - 1, sz - 1), P(sx + 1, sz + 1)], outline=(255, 80, 0), width=3)
        for k in (l['from'], l['to']): d.line([P(sx, sz), P(X[k], Z[k])], fill=(255, 120, 0), width=1)
    for m, k in MARK.items(): d.text(P(X[k], Z[k]), m, fill=(255, 255, 255))
    st = plan['pitStripe']
    for k in range(st[0], st[1]): d.point(P(X[k] + Nn[k, 0] * st[2], Z[k] + Nn[k, 1] * st[2]), fill=(255, 255, 255))
    for a, b, lo_, hi_ in boost:
        for k in range(a, b + 1):
            for o in np.linspace(lo_, hi_, 7): d.point(P(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o), fill=(0, 255, 255))
    fx, fz = lava_fall['at']; half = FALL_W / 2 + lava_fall['wing']
    d.rectangle([P(fx - half, fz - lava_fall['depth']), P(fx + half, fz)], outline=(160, 160, 160), width=2)
    d.rectangle([P(fx - FALL_W / 2, fz - 2 * lava_fall['tiers']), P(fx + FALL_W / 2, fz + lava_fall['pool'])], outline=(255, 90, 0), width=2)
    for c in crystals:
        cx, cz = c['at']
        d.ellipse([P(cx - 0.8, cz - 0.8), P(cx + 0.8, cz + 0.8)], fill=(230, 40, 100))
    img.save(D + 'design.png')
