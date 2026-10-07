# The Island of the Francos (KNARTAS.ILE) twice its size each way (RaceTrackIsland.Knartas.Scale 2: IslandScaler -- its ground, its
# objects and its scenes; cells 448..704): a lap in three parts, each with a look of its own (the user, 2026-10-07: "Make the track 3
# distinctly themed sections ... drop all the vertical loops ... add more height ... maybe some more loops, but not vertical ones"):
#
#  - the dock (its planks, blue and white curbs, wooden piles): the start up the dock's east arm, west along its north arm and out over
#    the sea in a loop that climbs over its own way in, south across the inlet's mouth in a jump over the island's rocket, and east along
#    the south arm, climbing over the start straight and the channel;
#  - the refinery (steel, yellow and black hazard curbs, red rails; pipes over the road that puff steam and drip oil, the gazogem
#    factory's steam blowing out of the road): a loop over its south fence up onto the high road at 14,000, north past its tanks, east
#    past its machine houses, and a leap off its north-east corner over the cracking tower down into the village;
#  - the village (earth, green and white curbs): through a furry dome hut at its floor, a loop over the hills, west through the village's
#    middle (a cluster of huts), south through a hut, east along the south shore, north through a hut, a leap over the refinery's east
#    fence and the climb to the top road over the rocks, and down the channel between the dock and the refinery onto the dock.
#
# A "loop" here is a cloverleaf: three corners turning the other way from the corner it replaces, the road going round 270 degrees and
# over its own way in (the deck at least LOOP_RISE over it there) -- a loop in the ground's plane, not a ring standing up.
#
# Run with E:\dump\TEMP\kbig holding the scaled island's H.npy (heights, H[z, x], cells 0..256 from 448) and obstacles.npz (the decors'
# tops and bottoms every quarter cell, `who` their body) -- IslandScaler's island: scaleisland, islandheights, decorpoints, obstacles.py;
# --pictures draws the lap over boxmap.png. Writes docs/racetrack/knartas_track_plan.json.
import json, math, sys
import numpy as np

D = 'E:/dump/TEMP/kbig/'
OUT = 'E:/dump/LBAAssembler/docs/racetrack/knartas_track_plan.json'
ORIGIN = 448
H = np.load(D + 'H.npy').astype(float)
H = np.nan_to_num(H, nan=0.0)
OB = np.load(D + 'obstacles.npz'); TOP, BOT, WHO, RQ = OB['top'], OB['bot'], OB['who'], int(OB['R'])
PRESENT = {(x, z) for x in (7, 8) for z in (7, 8)} | {(x, z) for x in (9, 10) for z in (7, 8, 9, 10)}
SIZE = 256

def ground(x, z):
    xi, zi = min(SIZE - 2, max(0, int(x))), min(SIZE - 2, max(0, int(z))); fx, fz = x - xi, z - zi
    return H[zi, xi] * (1 - fx) * (1 - fz) + H[zi, xi + 1] * fx * (1 - fz) + H[zi + 1, xi] * (1 - fx) * fz + H[zi + 1, xi + 1] * fx * fz

STEP = 0.5
H0 = 4.25                               # the deck's half width to its rail
ASPHALT, CURB = H0 - 1.0, H0 - 0.25
DOCK, OVER, HIGH, TOPL = 7000.0, 10500.0, 14000.0, 13200.0   # the dock's road (over its piers' crates, 6,500), over the start straight, the high road, the top road
LOOP_RISE = 3200.0                      # a loop's deck over its own way in where it crosses it

def C(x, z): return (x - ORIGIN, z - ORIGIN)
R = 9.0                                 # a loop's corners' radius; its corners 2R + 0.5 apart
L = 2 * R + 0.5
RV = 7.0; LV = 2 * RV + 0.5             # (the village's narrower: between the way south and the way north along x 682)
XE = 682.0                              # the village's way north, east of its loop
# the corners in the lap's order, each (x, z) and the radius it is rounded with
K = [((548.0, 474.0), 10.0),            # DOCK: up the east arm (the start), round west along the north arm ...
     ((486.0 - L, 474.0), R),            # ... past the inlet's mouth, out over the sea: the dock's loop, three corners round to the right ...
     ((486.0 - L, 474.0 - L), R),
     ((486.0, 474.0 - L), R),            # ... south over its own way in, across the inlet's mouth over the rocket (a jump) ...
     ((486.0, 550.0), 10.0),             # ... east along the south arm, over the start straight and the channel ...
     ((622.0 + L, 550.0), R),            # REFINERY: past the refinery's south fence into its loop, three corners round to the right ...
     ((622.0 + L, 550.0 + L), R),
     ((622.0, 550.0 + L), R),            # ... north over its own way in, up past the tanks onto the high road ...
     ((622.0, 486.0), 10.0),             # ... east past the machine houses, under the pipes ...
     ((657.0, 486.0), 10.0),             # ... south: the leap over the cracking tower into the village ...
     ((657.0, 641.5), RV),               # VILLAGE: through the first hut, into the village's loop over the hills, three corners round to the left ...
     ((657.0 + LV, 641.5), RV),
     ((657.0 + LV, 623.5), RV),          # ... west over its own way in, through the village's middle (a cluster of huts) ...
     ((594.0, 623.5), 10.0),             # ... south through the hut on the west side ...
     ((594.0, 686.0), 10.0),             # ... east along the south shore ...
     ((XE, 686.0), 10.0),                # ... north through the hut on the east side, the leap past the refinery's east fence, the climb ...
     ((XE, 464.0), 10.0),                # ... west along the top road over the rocks ...
     ((570.0, 464.0), 10.0),             # ... down the channel between the dock and the refinery (DOCK again), over the bridge ...
     ((570.0, 562.0), 10.0),             # ... west round onto the start straight
     ((548.0, 562.0), 10.0)]
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
k0 = int(np.argmin(np.hypot(fine[:, 0] - (548 - ORIGIN), fine[:, 1] - (556 - ORIGIN))))
fine = np.roll(fine, -k0, axis=0)
seg = np.linalg.norm(np.diff(np.vstack([fine, fine[:1]]), axis=0), axis=1)
cum = np.concatenate([[0], np.cumsum(seg)]); total = cum[-1]
N = int(round(total / STEP)); s = np.arange(N) * total / N
X = np.interp(s, cum, np.append(fine[:, 0], fine[0, 0])); Z = np.interp(s, cum, np.append(fine[:, 1], fine[0, 1]))
T = np.stack([np.gradient(X), np.gradient(Z)], 1); T /= np.linalg.norm(T, axis=1)[:, None]
Nn = np.stack([-T[:, 1], T[:, 0]], 1)
def nearest(p, heading=None):
    p = C(*p); d = np.hypot(X - p[0], Z - p[1])
    if heading is not None: d = d + 100 * (T @ np.array(heading) < 0.7)
    return int(np.argmin(d))
def ahead(a, b): return ((b - a) % N) * STEP
def between(k, a, b): return ahead(a, k) <= ahead(a, b)
def ease(f): f = min(1, max(0, f)); return f * f * (3 - 2 * f)
def trap(f, q=0.25):
    f = min(1, max(0, f))
    if f < q: v = f * f / (2 * q)
    elif f <= 1 - q: v = q / 2 + (f - q)
    else: v = 1 - q - (1 - f) ** 2 / (2 * q)
    return min(1, v / (1 - q))

# ---- heights: keyframes along the lap (a mark and its height), each stretch between two a grade that eases in and out (trap)
E, W_, S_, N_ = (1, 0), (-1, 0), (0, 1), (0, -1)
def floor(x, z): return ground(x - ORIGIN, z - ORIGIN) + 40
MARK = {
    'start':    nearest((548, 530), N_),
    'dock0':    nearest((548, 556), N_),
    'nArm1':    nearest((496, 474), W_),       # the dock's loop: in at the dock's height ...
    'loopOut':  nearest((486, 478), S_),       # ... out over its way in, LOOP_RISE higher
    'sArm0':    nearest((494, 550), E),
    'over':     nearest((552, 550), E),        # over the start straight
    'refIn':    nearest((610, 550), E),        # the refinery's loop: in ...
    'refOut':   nearest((622, 545), N_),       # ... and out over its way in
    'north1':   nearest((622, 500), N_),
    'east1':    nearest((645, 486), E),
    'towerFoot': nearest((657, 526), S_),     # (level to the leap's ramp, over the factory's roof)
    'hut88':    nearest((657, 603), S_),       # (through it half way up its dome: the leap's landing comes down to it)
    'vilIn':    nearest((657, 612), S_),       # the village's loop: in ...
    'vilOut':   nearest((652, 623.5), W_),     # ... and out over its way in
    'mid':      nearest((612, 623.5), W_),     # the village's middle, the cluster of huts, half way up its domes
    'hut81':    nearest((594, 650), S_),
    'south0':   nearest((604, 686), E), 'south1': nearest((668, 686), E),
    'hut84':    nearest((XE, 662), N_),
    'up1':      nearest((XE, 490), N_),
    'top1':     nearest((585, 464), W_),
    'down1':    nearest((570, 547), S_),
}
MIDWAY = 1000.0                         # the village's huts driven through half way up their domes (they are twice their size)
KEYS = [('dock0', DOCK), ('start', DOCK), ('nArm1', DOCK), ('loopOut', DOCK + LOOP_RISE), ('sArm0', DOCK + 200), ('over', OVER - 200),
        ('refIn', OVER + 800), ('refOut', OVER + 800 + LOOP_RISE), ('north1', HIGH), ('east1', HIGH), ('towerFoot', HIGH),
        ('hut88', floor(657, 603) + 2700), ('vilIn', floor(657, 603) + 2800), ('vilOut', floor(657, 603) + 3000 + LOOP_RISE),
        ('mid', floor(612, 621) + 2000), ('hut81', floor(594, 650) + MIDWAY), ('south0', 2700.0), ('south1', 2700.0),
        ('hut84', floor(XE, 662) + MIDWAY), ('up1', TOPL), ('top1', TOPL), ('down1', DOCK + 100)]
KEYS = sorted(((MARK[m], h) for m, h in KEYS), key=lambda t: t[0])
Y = np.zeros(N)
for i in range(len(KEYS)):
    (a, ha), (b, hb) = KEYS[i], KEYS[(i + 1) % len(KEYS)]
    span = ahead(a, b)
    k = a
    while True:
        Y[k] = ha + (hb - ha) * trap(ahead(a, k) / span) if span > 0 else ha
        if k == b: break
        k = (k + 1) % N

# ---- the jumps the engine carries the car over: (name, foot, lip, landing lip, landing foot as (x, z, heading)) and the ramp's angle
def flight_heights(iFoot, iLip, iLand, iLandFoot, theta, top_needed, land_at=None):
    """the car's way from the ramp's foot to the landing hill's foot: a ramp curving up (a circle's arc, level at its foot) to the lip, a
    parabola to the landing lip, a hill curving down to its foot -- the heights at its ends the road's own there"""
    y0, y3 = Y[iFoot], Y[iLandFoot]
    k1 = ahead(iFoot, iLip) * 512; L_ = ahead(iLip, iLand) * 512; k2 = ahead(iLand, iLandFoot) * 512
    R1 = k1 / math.sin(theta); yl = y0 + R1 * (1 - math.cos(theta)); sl = math.tan(theta)
    def at(d):
        if d <= k1: return y0 + R1 - math.sqrt(max(0, R1 * R1 - d * d))
        if d <= k1 + L_:
            u = d - k1; yland = land_y
            a = (yland - yl - sl * L_) / (L_ * L_)
            return yl + sl * u + a * u * u
        u = d - k1 - L_; f = u / k2
        return land_y + (y3 - land_y) * ease(f)
    land_y = max(y3 + 300, min(yl, top_needed)) if land_at is None else land_at
    return at, yl, land_y

# (the user's next rounds, 2026-10-07: the tower's and the fence's far too long -- 76 and 60 cells: 42 and 30, then still too long: 24
# and 18, the tower's ramp now over the factory's roof -- and the boat's landing a little closer -- 35 cells: 32)
JUMPS = [('inlet', (486, 486.0, S_), (486, 496.0, S_), (486, 528.0, S_), (486, 538.0, S_), 40.0, None),
         ('tower', (657, 528.0, S_), (657, 536.0, S_), (657, 560.0, S_), (657, 570.0, S_), 22.0, (5600.0, 7600.0)),
         ('fence', (XE, 604.0, N_), (XE, 594.0, N_), (XE, 576.0, N_), (XE, 566.0, N_), 30.0, None)]
gap = np.zeros(N, bool); arcs = []
for name, f, l, ld, lf, deg, landing in JUMPS:
    iF, iL, iD, iDF = nearest(f[:2], f[2]), nearest(l[:2], l[2]), nearest(ld[:2], ld[2]), nearest(lf[:2], lf[2])
    # (a landing given: the landing hill's foot and its lip -- the hill is carried with the flight, so it may be steeper than a road)
    foot_y, land_at = landing if landing is not None else (None, None)
    if foot_y is not None: Y[iDF] = foot_y
    at, yl, land_y = flight_heights(iF, iL, iD, iDF, math.radians(deg), Y[iDF] + 600, land_at)
    k = iF
    while True:
        Y[k] = at(ahead(iF, k) * 512)
        if k == iDF: break
        k = (k + 1) % N
    k = (iL + 1) % N
    while k != iD: gap[k] = True; k = (k + 1) % N
    arcs.append((name, iF, iL, iD, iDF))
# (after the tower's landing foot, down to the first hut's floor)
a, b = arcs[1][4], MARK['hut88']
k = a
while True:
    Y[k] = Y[a] + (Y[b] - Y[a]) * trap(ahead(a, k) / ahead(a, b))
    if k == b: break
    k = (k + 1) % N

# ---- the three sections, each its deck's colours
SECTIONS = [('dock', nearest((570, 488), S_), nearest((562, 550), E)),
            ('refinery', nearest((566, 550), E), nearest((657, 586), S_)),
            ('village', nearest((657, 588), S_), nearest((570, 484), S_))]

# ---- the refinery's pipes (RaceTrackPipes) over its roads, steam from their tops, oil dripping from each onto the road; and the gazogem
# factory's steam blowing out of the road between them (steam jets)
PIPES = [((576.0, 550.0), E, (612.0, 550.0), E), ((622.0, 540.0), N_, (622.0, 494.0), N_), ((630.0, 486.0), E, (650.0, 486.0), E)]
pipes = [(nearest(pa, ha), nearest(pb, hb)) for pa, ha, pb, hb in PIPES]
JETS = [((580.0, 550.0), E, (612.0, 550.0), E), ((622.0, 538.0), N_, (622.0, 496.0), N_), ((628.0, 486.0), E, (650.0, 486.0), E)]
jets = [(nearest(pa, ha), nearest(pb, hb)) for pa, ha, pb, hb in JETS]
for i0, i1 in pipes + jets:
    for name, iF, iL, iD, iDF in arcs:
        if between(iF, i0, i1) or between(i0, iF, iDF): sys.exit(f'pipes or jets {i0}-{i1}: in jump {name}')

# ---- the pipeline from the Gazogem factory (its west wall) to the air-boat in the inlet (its deck between its hulls), over the channel road
# and the start straight -- where it drips -- and under the high road: [x, z, height]
PIPE_Y = 12000.0
PIPELINE = [(648.0, 528.0, 7500.0), (641.0, 528.0, 7500.0), (636.0, 516.0, PIPE_Y), (526.0, 516.0, PIPE_Y), (517.0, 513.0, 9500.0), (511.5, 511.0, 3400.0)]
PIPE_R = 700.0

HALF = np.full(N, H0)
grade = (np.roll(Y, -1) - Y) / (STEP * 512)
ride = ~np.zeros(N, bool)
for name, iF, iL, iD, iDF in arcs:
    for k in range(N):
        if between(k, iF, iDF): ride[k] = False

# ---- checks
out = [f'lap {total:.1f} cells, {N} points; heights {Y.min():.0f} to {Y.max():.0f}; steepest driven {np.abs(grade[ride]).max() * 100:.1f} %']
steep = sorted(((abs(grade[k]), k) for k in range(N) if ride[k]), reverse=True)
sp = []
for g_, k in steep:
    if all(min(abs(k - j), N - abs(k - j)) > 20 for j in sp): sp.append(k)
    if len(sp) == 5: break
out.append('  steepest at: ' + ', '.join(f'{abs(grade[k]) * 100:.1f}% point {k} ({X[k] + ORIGIN:.0f}, {Z[k] + ORIGIN:.0f})' for k in sp))
def edge_dist(x, z):
    x, z = x + ORIGIN, z + ORIGIN; best = 1e9
    for dz in (-1, 0, 1):
        for dx in (-1, 0, 1):
            q = (int(x // 64) + dx, int(z // 64) + dz)
            if q in PRESENT: continue
            qx = min(max(x, q[0] * 64), q[0] * 64 + 64); qz = min(max(z, q[1] * 64), q[1] * 64 + 64)
            best = min(best, math.hypot(x - qx, z - qz))
    return best
edge = np.array([edge_dist(x, z) for x, z in zip(X, Z)])
out.append(f'  nearest a missing cube: {edge.min():.1f} cells at ({X[edge.argmin()] + ORIGIN:.1f}, {Z[edge.argmin()] + ORIGIN:.1f}) (needs 6.5)')
clear = sorted((Y[k] - max(ground(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o) for o in np.linspace(-HALF[k], HALF[k], 9)), k) for k in range(N) if not gap[k])
out.append('  the deck under the ground: ' + ', '.join(f'{c:.0f} at ({X[k] + ORIGIN:.1f}, {Z[k] + ORIGIN:.1f}) point {k}' for c, k in clear[:6] if c < 0))
hits = {}
for k in range(N):
    if gap[k]: continue
    for o in np.linspace(-HALF[k] - 0.3, HALF[k] + 0.3, 17):
        x, z = X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o
        i, j = int(z * RQ), int(x * RQ)
        if not (0 <= i < TOP.shape[0] and 0 <= j < TOP.shape[1]) or TOP[i, j] < 0: continue
        if TOP[i, j] > Y[k] - 350 and BOT[i, j] < Y[k] + 1400:
            b = int(WHO[i, j]); hits.setdefault(b, []).append((round(float(x + ORIGIN), 1), round(float(z + ORIGIN), 1), int(TOP[i, j]), int(Y[k])))
out.append('  decor in the road\'s space (body: places, e.g.): ' + '; '.join(f'{b}: {len(h)} {h[0]}' for b, h in sorted(hits.items())))
# (a jump's flight over what stands under it: the least clearance over the decors' tops and the ground)
for name, iF, iL, iD, iDF in arcs:
    worst = (1e9, None)
    k = iL
    while k != iD:
        for o in np.linspace(-1.5, 1.5, 5):
            x, z = X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o
            i, j = int(z * RQ), int(x * RQ)
            if 0 <= i < TOP.shape[0] and 0 <= j < TOP.shape[1] and TOP[i, j] >= 0 and Y[k] - TOP[i, j] < worst[0]: worst = (Y[k] - TOP[i, j], int(WHO[i, j]))
        if Y[k] - ground(X[k], Z[k]) < worst[0]: worst = (Y[k] - ground(X[k], Z[k]), -1)
        k = (k + 1) % N
    flight = [Y[q % N] for q in range(iL, iD + (N if iD < iL else 0))]
    out.append(f'  jump {name}: lip {Y[iL]:.0f}, landing {Y[iD]:.0f}, top {max(flight):.0f}; least clearance {worst[0]:.0f} over {"the ground" if worst[1] == -1 else "body " + str(worst[1])}')
idx = np.arange(N); close = []
def seg_dist(p1, p2, q1, q2):
    def d_pt(p, a, b):
        ab = b - a; t = np.clip(((p - a) @ ab) / max(ab @ ab, 1e-12), 0, 1); return np.linalg.norm(p - a - ab * t)
    def cross(a, b, c, d):
        def o(p, q, r): return np.sign((q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]))
        return o(a, b, c) != o(a, b, d) and o(c, d, a) != o(c, d, b)
    if cross(p1, p2, q1, q2): return 0.0
    return min(d_pt(p1, q1, q2), d_pt(p2, q1, q2), d_pt(q1, p1, p2), d_pt(q2, p1, p2))
P2 = np.stack([X, Z], 1)
crossings = []
for i in range(0, N, 2):
    dd = np.hypot(X - X[i], Z - Z[i]); sep = np.minimum(np.abs(idx - i), N - np.abs(idx - i))
    cand = np.where((sep > 40) & (dd < HALF + HALF[i] + 1))[0]
    for j in cand:
        d = seg_dist(P2[i] - Nn[i] * HALF[i], P2[i] + Nn[i] * HALF[i], P2[j] - Nn[j] * HALF[j], P2[j] + Nn[j] * HALF[j])
        if d < 0.6:
            if abs(Y[j] - Y[i]) < 2600 and not (gap[i] or gap[j]): close.append((abs(Y[j] - Y[i]), dd[j], i, int(j)))
            elif j > i: crossings.append((i, int(j), abs(Y[j] - Y[i])))
seen = []
for dy, dmin, i, j in sorted(close):
    if any(abs(i - a) < 20 and abs(j - b) < 20 for a, b in seen): continue
    seen.append((i, j))
    out.append(f'  CLASH: points {i} ({X[i] + ORIGIN:.1f}, {Z[i] + ORIGIN:.1f}) {Y[i]:.0f} and {j} ({X[j] + ORIGIN:.1f}, {Z[j] + ORIGIN:.1f}) {Y[j]:.0f}: {dmin:.1f} cells apart, {dy:.0f} between')
    if len(seen) > 12: break
cr = []
for i, j, dy in sorted(crossings, key=lambda c: c[2]):
    if any(abs(i - a) < 30 and abs(j - b) < 30 for a, b, _ in cr): continue
    cr.append((i, j, dy))
out.append('  the lap over itself: ' + ', '.join(f'({X[i] + ORIGIN:.0f}, {Z[i] + ORIGIN:.0f}) {dy:.0f} between' for i, j, dy in cr))
for m, k in MARK.items():
    out.append(f'    {m:8s} point {k:4d} ({X[k] + ORIGIN:6.1f}, {Z[k] + ORIGIN:6.1f}) {Y[k]:6.0f} (ground {ground(X[k], Z[k]):5.0f})')
for nm, a, b in SECTIONS: out.append(f'  {nm}: points {a}..{b}, {ahead(a, b):.0f} cells')
# (the pipeline over and under the lap: where a part of the lap passes within its width of it, the gap between them)
reported = []
for (x0, z0, y0), (x1, z1, y1) in zip(PIPELINE, PIPELINE[1:]):
    for f in np.linspace(0, 1, 400):
        px, pz, py = x0 + (x1 - x0) * f - ORIGIN, z0 + (z1 - z0) * f - ORIGIN, y0 + (y1 - y0) * f
        d = np.hypot(X - px, Z - pz); k = int(d.argmin())
        if d[k] < HALF[k] + 1.2 and all(min(abs(k - q), N - abs(k - q)) > 30 for q in reported):
            reported.append(k)
            gap_ = (py - PIPE_R * 1.3 - Y[k] - 1100) if Y[k] < py else (Y[k] - 200 - py - PIPE_R * 1.3)
            out.append(f'    pipeline at ({px + ORIGIN:.0f}, {pz + ORIGIN:.0f}) {py:.0f}: the lap {"under" if Y[k] < py else "over"} it at {Y[k]:.0f}, {gap_:.0f} to spare')
print('\n'.join(out))

plan = {
    'originCellX': ORIGIN, 'originCellZ': ORIGIN,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in zip(X, Z)],
    'heights': [round(float(y), 1) for y in Y],
    'maxGrade': round(float(np.abs(grade).max()) + 0.05, 3),
    'raised': [0, N - 1], 'raisedHalf': H0, 'asphaltHalf': ASPHALT, 'curbHalf': CURB,
    'arcJumps': [[int(iF), int(iL), int(iD), int(iDF), 0] for name, iF, iL, iD, iDF in arcs],
    'start': int(MARK['start']),
    # (where the opponents wait while the player qualifies: on the dock's east arm, under the start straight's west edge)
    'pitSpots': [[535.5 - ORIGIN, z - ORIGIN, 0.0, -1.0, 3100.0] for z in (516.0, 524.0, 532.0)],   # (all in the start's cube, 8,8)
    'raisedCut': True,
    # (the huts the road drives through, cut open where it runs -- twice their size with the island, big enough as they are)
    'driveThrough': [{'body': 88, 'scale': 1.0}, {'body': 81, 'scale': 1.0}, {'body': 84, 'scale': 1.0}, {'body': 77, 'scale': 1.0}],
    # (a hut beside the village's middle road, kept)
    'keepBodies': [73],
    'themes': [{'from': int(a), 'to': int(b), 'theme': nm} for nm, a, b in SECTIONS],
    'pipes': [{'from': int(i0), 'to': int(i1), 'every': 6.0, 'drip': True, 'dripsEvery': 1} for i0, i1 in pipes],
    'steamJets': [{'from': int(i0), 'to': int(i1), 'every': 7.0} for i0, i1 in jets],
    'pipeline': {'points': [[x - ORIGIN, z - ORIGIN, y] for x, z, y in PIPELINE], 'radius': PIPE_R, 'supportEvery': 12.0, 'dripEvery': 2.5, 'dripMs': 4500},
}
if '--write' in sys.argv:
    json.dump(plan, open(OUT, 'w'))
    print('written', OUT)
np.savez(D + 'lap.npz', X=X + ORIGIN, Z=Z + ORIGIN, Y=Y, HALF=HALF, gap=gap)

if '--pictures' in sys.argv:
    from PIL import Image, ImageDraw
    Zm = 4
    img = Image.open(D + 'boxmap.png').convert('RGB')
    d = ImageDraw.Draw(img)
    P = lambda x, z: (x * Zm, z * Zm)
    lo, hi = Y.min(), Y.max()
    colours = {'dock': (60, 140, 255), 'refinery': (255, 210, 0), 'village': (80, 230, 80)}
    for k in range(N):
        j = (k + 1) % N
        t = (Y[k] - lo) / (hi - lo)
        col = (int(255 * t), int(255 * (1 - abs(2 * t - 1))), int(255 * (1 - t)))
        if gap[k]: col = (255, 255, 255)
        d.line([P(X[k], Z[k]), P(X[j], Z[j])], fill=col, width=3 if not gap[k] else 1)
    for nm, a, b in SECTIONS:
        k = a
        while k != b:
            d.point(P(X[k] + Nn[k, 0] * 3, Z[k] + Nn[k, 1] * 3), fill=colours[nm]); k = (k + 1) % N
    for i0, i1 in pipes:
        for step in range(0, int(ahead(i0, i1) / STEP) + 1, 12):
            k = (i0 + step) % N
            d.ellipse([P(X[k] - 0.6, Z[k] - 0.6), P(X[k] + 0.6, Z[k] + 0.6)], outline=(255, 160, 0))
    for m, k in MARK.items(): d.text(P(X[k], Z[k]), m, fill=(255, 255, 255))
    img.save(D + 'design.png')
    W, Hh = 1600, 360
    prof = Image.new('RGB', (W, Hh), (20, 20, 30)); pd = ImageDraw.Draw(prof)
    for k in range(N - 1):
        x0, x1 = k * W / N, (k + 1) * W / N
        pd.line([(x0, Hh - Y[k] / 16000 * Hh), (x1, Hh - Y[k + 1] / 16000 * Hh)], fill=(255, 255, 255) if not gap[k] else (255, 80, 80))
        g0 = ground(X[k], Z[k]); pd.point((x0, Hh - g0 / 16000 * Hh), fill=(120, 90, 60))
    for m, k in MARK.items(): pd.text((k * W / N, 4), m[:6], fill=(200, 200, 100))
    prof.save(D + 'profile.png')
