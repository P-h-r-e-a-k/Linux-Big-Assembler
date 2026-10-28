# Wannies Island (PLATFORM.ILE, island 6; outside scenes 96-99, cubes (7,7)..(8,8)) -- the race track from the user's drawing (2026-10-14:
# "Proposed track for Platform.ile. Pink is jump, and purple is a boost pad. The massive blue circle is to be a giant frog that we drive
# through. The frog repeatedly opens and closes its mouth and extends its tongue as it does so, so we have to time the jump into the mouth.
# Exiting the frog we go up a slope to the top of the tower, then there's a loop coming down the tower looping around about three times,
# into the start finish line and the pit lane"; then: the boost pad and its jump a shortcut beside the road round the right, the road
# built up to the frog's jump "which then lifts the frog higher into the air, this will then make the slope to the top of the tower less
# steep", and a car meeting a shut mouth bounced back). Second round, 2026-10-15: twice down the tower, not three times; the start
# straight on south from it, a thinner pit lane of its own beside it; the shortcut a real one -- a high leap over the west island's loop
# -- and boost pads round that loop. Round 4, 2026-10-15: the road close over the ground (it climbed high over the islands to the frog), the
# frog lower and the road up from it to the tower's top steeper; the pit lane's mouths wide tapers; the shortcut's boost pad gone.
#
# The drawing is the editor's map turned a quarter anticlockwise and stretched (E:\dump\TEMP\wan\trace.py: u = 55 + 9 (z - 448),
# v = 5 + 5.82 (576 - x)); its line read off it (route_points.py) and laid on the island:
#  - the start straight over the lava east of the pier (its pit lane down the pier beside it), onto the east island, down its east side,
#    west along its south side (the shortcut off its corner: over a boost pad, up a ramp and a leap onto the S before the frog), the west
#    island's south loop (boost pads round it), north-east and north-west up to the frog's jump -- climbing all the way, the road high
#    over the islands, to the frog high in the air;
#  - the jump into the frog's mouth, through the frog, out of its back, east over the lava along the island's north edge, climbing to the
#    elevator tower's top (the Wannies' elevator, bodies 19-31: 2,950 to 24,455);
#  - twice round the tower down a helix (a stadium round it: the tower stands 15 cells from the island's north edge, cube row 6
#    is missing), out of it straight on south onto the start straight, the pit lane beside it.
#
# Run with E:\dump\TEMP\wan holding H.npy and obstacles.npz (E:\dump\TEMP\otr\prep.py E:/dump/TEMP/wan 448 448 128) and boxes.txt (the
# pristine island's decors: `LBA2_DIR=<pristine> ScriptRoundTrip decorpoints PLATFORM <csv>`, its "cube ..." lines); --write writes
# docs/racetrack/wannies_track_plan.json; --pictures draws the lap over the island's map (E:\dump\TEMP\wan\lap.png).
import json, math, re, sys
import numpy as np

D = 'E:/dump/TEMP/wan/'
OUT = 'E:/dump/LBAAssembler/docs/racetrack/wannies_track_plan.json'
OX, OZ = 448, 448
H = np.nan_to_num(np.load(D + 'H.npy').astype(float), nan=0.0)
OB = np.load(D + 'obstacles.npz'); TOP, BOT, WHO, RQ = OB['top'], OB['bot'], OB['who'], int(OB['R'])
PRESENT = {(7, 7), (8, 7), (7, 8), (8, 8)}
SIZE = 128

def ground(x, z):
    xi, zi = min(SIZE - 1, max(0, int(x))), min(SIZE - 1, max(0, int(z))); fx, fz = x - xi, z - zi
    return H[zi, xi] * (1 - fx) * (1 - fz) + H[zi, xi + 1] * fx * (1 - fz) + H[zi + 1, xi] * (1 - fx) * fz + H[zi + 1, xi + 1] * fx * fz

STEP = 0.5
H0 = 4.75                               # the deck's half width to its rail (the other tracks')
ASPHALT, CURB = H0 - 1.0, H0 - 0.25
PIT_X = 550.5                           # the pit lane: down the pier's east side, beside the start straight (2026-10-15) ...
PIT_HALF = 2.75                         # ... thinner than the road (2.25 at first: "short, narrow" -- longer and wider since)
PIT_SPOTS = (489.0, 503.0, 508.5)       # ... the opponents' waiting places along it (clear of the start line at 497, past the entry's taper)
TOWER = (543.5, 467.5)                  # the elevator tower's middle (its top block 537.6..549.4 x 463.9..471.1, its legs 536.6..550.4 x 462.8..472.2)
TOWER_TOP = 24455
# the helix round the tower: a stadium -- two half circles (radius HR, their middles HZ0 and HZ1 on the tower's line north-south) joined
# by straights at x = TOWER.x -+ HR -- three times round clockwise, down from H_TOP; in from the west at its north point, out down its east
# straight
HR = 15.0
HZ0, HZ1 = 469.0, 478.0               # (the inner rail 10.25 from the middles: clear of the legs' corners, 9.3 off)
TURNS = 2                               # (three until 2026-10-15: "reduce the downward loops around the elevator by one")
H_TOP = 25000.0                         # the helix's top: over the tower's top
# (round 4: the start straight's height, the helix's foot's and the frog's worked out from the ground -- the heights below)
GRADE_LOW = 0.17                        # the road close over the ground: its steepest, up and down (Volcano Island's, Otringal's)
CLEAR, LAVA, LAVA_MIN = 550.0, 700.0, 1400.0   # over the highest ground across the road (the builder cuts the ground to 450 under a deck); ground under
                                        # LAVA is lava, the road LAVA_MIN over it
DECOR_CLEAR = 450.0                     # over the top of a decor's box the builder would count in the road's space -- a cell of the box with its middle
                                        # within the deck's half + 0.75 -- and take away unless the deck is 320 over it
JUMP_UP = 400.0                         # the shortcut's leap comes down at least this much over its lip
PAD_DROP, PAD_R, PAD_CLEAR = 2650.0, 6800 / 512, 300.0   # the frog's lily pad: its underside under the frog's floor, its radius (cells) -- RaceTrackFrog's
                                        # PadTop + PadThick, PadRadius -- and how far it stays over what is under it
# (the island's decors dropped -- RaceTrackIsland.DropDecors -- the box they stand in, their body: the pier's east post, in the pit lane's
# way now the lane is low)
DROPPED = [((549.0, 481.0), (552.0, 484.0), 33)]
DROP_ORIGINS = [(8, 7, 19712, 17664)]   # (... the same, as the island's record names it: cube, origin)
KEEP = {19, 20, 21, 27, 28, 29, 30, 31} # the plan's keepBodies (the Wannies' elevator): kept whatever the road does
# (the decors' boxes in island cells, as the builder tests them -- the middles of the box's cells -- from the island's origin, and their tops)
BOXES = []
for _line in open(D + 'boxes.txt'):
    _m = re.match(r'cube \((\d+),(\d+)\) decor \d+: body (\d+) at \((-?\d+),(-?\d+),(-?\d+)\).*box x (-?\d+)\.\.(-?\d+) y (-?\d+)\.\.(-?\d+) z (-?\d+)\.\.(-?\d+)', _line)
    if not _m: continue
    _cx, _cz, _body, _ox, _oy, _oz, _x0, _x1, _y0, _y1, _z0, _z1 = map(int, _m.groups())
    if _body in KEEP or (_cx, _cz, _ox, _oz) in DROP_ORIGINS: continue
    BOXES.append((math.floor((_cx * 32768 + _x0) / 512) + 0.5 - OX, math.floor((_cx * 32768 + _x1) / 512) + 0.5 - OX,
                  math.floor((_cz * 32768 + _z0) / 512) + 0.5 - OZ, math.floor((_cz * 32768 + _z1) / 512) + 0.5 - OZ, _y1, _y0, _body))
BOXA = np.array(BOXES, float)
def box_near(x, z, reach):
    """The decors' boxes a deck's point at (x, z) reaches (reach: its half + 0.75), as a mask over BOXA."""
    dx = np.maximum(0, np.maximum(BOXA[:, 0] - x, x - BOXA[:, 1])); dz = np.maximum(0, np.maximum(BOXA[:, 2] - z, z - BOXA[:, 3]))
    return dx * dx + dz * dz <= reach * reach

def C(x, z): return (x - OX, z - OZ)

# ---- the lap: corners (island cells) and their radii, from the start straight's foot round to the frog's take-off ramp
ROUND = [((558.5, 490.0), 0.0),          # (the start straight: straight on south from the helix, over the lava east of the pier) ...
         ((558.5, 518.0), 10.0),         # ... onto the east island, south-south-east past its buildings ...
         ((566.5, 537.0), 7.0),          # ... south down its east side ...
         ((569.5, 548.0), 6.0),          # ... out to the island's edge past the mine's machinery ...
         ((569.5, 565.0), 5.0),          # ... round east and south of the mine's machinery (87-93: 8,657 high, x 555.8..566, z 551.9..559.1;
                                         # the island's edges x 576, z 576: the rail 1.75 cells in) ...
         ((549.0, 565.0), 8.0),          # ... west along its south side ...
         ((532.5, 555.5), 8.0),          # ... (the shortcut's fork: next round) ...
         ((513.0, 557.0), 8.0),
         ((498.0, 547.5), 7.0),          # ... (the shortcut's join) ...
         ((470.0, 548.0), 7.0),          # ... west onto the west island ...
         ((466.5, 567.0), 6.0),          # ... its south loop: south ...
         ((455.0, 569.5), 6.0),          # ... west ...
         ((455.5, 545.0), 7.0),          # ... north ...
         ((468.0, 532.5), 8.0),          # ... north-east ...
         ((497.5, 521.5), 8.0),          # ... east along the channel ...
         ((497.5, 507.0), 7.0)]          # ... north, then west and round north-north-east up to the frog (FROG_ROUND) ...
# the frog: the road straight through it along the drawing's line, from its mouth (the drawing's pink dot 477.3, 484.7) to where the line
# leaves the circle (483.7, 466.9) -- and straight before it, up the take-off ramp (RAMP cells) and over the gap (GAP) into the mouth
FROG_MOUTH = (477.3, 484.7)
FROG_DIR = np.array([483.7 - 477.3, 466.9 - 484.7]); FROG_DIR /= np.linalg.norm(FROG_DIR)
FROG_LEN, GAP, RAMP = 18.0, 5.0, 10.0
FROG_HALF = 3.5                         # the road's half width through the frog (its throat 4.2 cells either side: RaceTrackFrog.ThroatHalf)
def along(t): return (float(FROG_MOUTH[0] + FROG_DIR[0] * t), float(FROG_MOUTH[1] + FROG_DIR[1] * t))
FROG_LIP, FROG_EXIT = along(-GAP), along(FROG_LEN)
ROUND += [(along(-26.0), 7.0),          # ... the turn onto the frog's line (its bend over 10.8 cells before the ramp's foot) ...
          (FROG_LIP, 0.0)]              # ... its take-off ramp's lip
APPROACH = [(FROG_LIP, 0.0), (along(26.0), 8.0),            # through the frog, out of its back, on along its line ...
            ((530.0, 454.0), 6.0)]       # ... east along the island's north edge to the helix's top (its north point 543.5, 454)

def rounded(K, closed=False):
    """A polyline through corners rounded with their radii (a corner of radius 0 is a point the line passes through)."""
    n = len(K); P = [np.array(p, float) for p, r in K]; out = []
    arcs = []
    for i in range(n):
        if not closed and (i == 0 or i == n - 1) or K[i][1] <= 0: arcs.append(None); continue
        a, b, c = P[i - 1], P[i], P[(i + 1) % n]; r = K[i][1]
        u1 = (b - a) / np.linalg.norm(b - a); u2 = (c - b) / np.linalg.norm(c - b)
        turn = math.atan2(u1[0] * u2[1] - u1[1] * u2[0], u1 @ u2)
        t = r * math.tan(abs(turn) / 2)
        p1, p2 = b - u1 * t, b + u2 * t
        nrm = np.array([-u1[1], u1[0]]) * (1 if turn > 0 else -1)
        arcs.append((p1, p2, p1 + nrm * r, r, turn))
    for i in range(n if closed else n - 1):
        if arcs[i] is None: q1 = P[i]
        else:
            p1, p2, centre, r, turn = arcs[i]
            a0 = math.atan2(p1[1] - centre[1], p1[0] - centre[0])
            m = max(2, int(round(r * abs(turn) / 0.02)))
            for k in range(m): out.append(centre + r * np.array([math.cos(a0 + turn * k / m), math.sin(a0 + turn * k / m)]))
            q1 = p2
        j = (i + 1) % n
        q2 = P[j] if arcs[j] is None else arcs[j][0]
        if np.dot(q2 - q1, P[j] - P[i]) < -1e-6: sys.exit(f'corners {i} and {j}: their roundings overlap')
        m = max(1, int(round(np.linalg.norm(q2 - q1) / 0.02)))
        for k in range(m): out.append(q1 + (q2 - q1) * k / m)
    if not closed: out.append(P[-1])
    return np.array(out)

# the helix: the stadium's centre line from its north point clockwise, `turns` times round and on to the foot of its east straight
def stadium(turns_):
    tx = TOWER[0]
    north, east_top, east_bot = (tx, HZ0 - HR), (tx + HR, HZ0), (tx + HR, HZ1)
    pts = []
    def arc(cz, a0, a1):
        m = int(round(HR * abs(math.radians(a1 - a0)) / 0.02))
        for k in range(m):
            a = math.radians(a0 + (a1 - a0) * k / m); pts.append((tx + HR * math.cos(a), cz + HR * math.sin(a)))
    def straight(p, q):
        m = int(round(math.dist(p, q) / 0.02))
        for k in range(m): pts.append((p[0] + (q[0] - p[0]) * k / m, p[1] + (q[1] - p[1]) * k / m))
    for t in range(turns_):
        arc(HZ0, -90, 0)                                   # the north half circle's east quarter
        straight(east_top, east_bot)                       # down the east side
        arc(HZ1, 0, 180)                                   # the south half circle
        straight((tx - HR, HZ1), (tx - HR, HZ0))           # up the west side
        arc(HZ0, 180, 270)                                 # the north half circle's west quarter
    arc(HZ0, -90, 0)
    straight(east_top, east_bot)
    return np.array(pts)
helix = stadium(TURNS)
OUTWAY = [((TOWER[0] + HR, HZ1), 0.0), ((TOWER[0] + HR, 486.0), 0.0)]   # out of it straight on south (the user, 2026-10-15)
lap_part = rounded(ROUND)
approach = rounded(APPROACH)
outway = rounded(OUTWAY)
join = rounded([(tuple(outway[-1]), 0.0), (ROUND[0][0], 0.0)])
pieces = [lap_part, approach, helix, outway, join]
pts = pieces[0]
piece_at = [0.0]                         # where each piece starts round the lap (cells)
for p in pieces[1:]:
    piece_at.append(piece_at[-1] + float(np.linalg.norm(np.diff(pts[-2:] if False else np.vstack([pts[-1:], p[:1]]), axis=0), axis=1).sum())
                    + 0.0)
    pts = np.concatenate([pts, p[1:] if np.linalg.norm(p[0] - pts[-1]) < 1e-6 else p])
lengths = [float(np.linalg.norm(np.diff(p, axis=0), axis=1).sum()) for p in pieces]
piece_at = [0.0]
for i in range(1, len(pieces)):
    piece_at.append(piece_at[-1] + lengths[i - 1] + float(np.linalg.norm(pieces[i][0] - pieces[i - 1][-1])))
fine = np.array([C(x, z) for x, z in pts])
fine = fine[np.concatenate([[True], np.hypot(*np.diff(fine, axis=0).T) > 1e-9])]
seg = np.linalg.norm(np.diff(np.vstack([fine, fine[:1]]), axis=0), axis=1)
gapclose = seg[-1]
cum = np.concatenate([[0], np.cumsum(seg)]); total = cum[-1]
N = int(round(total / STEP)); s = np.arange(N) * total / N
X = np.interp(s, cum, np.append(fine[:, 0], fine[0, 0])); Z = np.interp(s, cum, np.append(fine[:, 1], fine[0, 1]))
T = np.stack([np.gradient(X), np.gradient(Z)], 1); T /= np.linalg.norm(T, axis=1)[:, None]
Nn = np.stack([-T[:, 1], T[:, 0]], 1)    # across the road (the builder's Across, (-Tz, Tx): west when heading south)
def ahead(a, b): return ((b - a) % N) * STEP
def between(k, a, b): return ahead(a, k) <= ahead(a, b)
def nearest_after(p, after, window):
    best, bd = after, 1e9
    for q in range(int(window / STEP)):
        k = (after + q) % N
        d = math.hypot(X[k] - (p[0] - OX), Z[k] - (p[1] - OZ))
        if d < bd: bd, best = d, k
    return best
def ease(f): f = min(1, max(0, f)); return f * f * (3 - 2 * f)

# ---- marks round the lap
MARK = {}
MARK['start'] = nearest_after((558.5, 497.0), 0, 30)                           # the start line on the straight
MARK['lip'] = nearest_after(FROG_LIP, 0, total)                                # the frog's take-off lip ...
MARK['foot'] = (MARK['lip'] - int(RAMP / STEP)) % N                            # ... its ramp's foot
MARK['mouth'] = nearest_after(FROG_MOUTH, MARK['lip'], 20)                     # ... the mouth: the landing
MARK['back'] = nearest_after(FROG_EXIT, MARK['mouth'], 40)                     # ... out of its back
def at_cells(c): return int(round(c / total * N)) % N
MARK['top'] = at_cells(piece_at[2])                                            # the helix's top: its north point, the first time
MARK['hOut'] = at_cells(piece_at[3])                                           # out of the helix
MARK['pier'] = at_cells(piece_at[4] + lengths[4])                              # on the pier: the start straight's foot
# (the shortcut's ends on the lap -- its own way further on: where it leaves the lap, where its leap comes down onto it, where it is back)
SC_F = nearest_after((536.2, 557.4), MARK['start'], 300)                   # off the lap: straight on north-west where the lap turns west
SC_LIP = np.array([510.0, 533.0])                                         # its take-off (the user's pink, 2026-10-15) ...
# ... its landing: the lap's first point past the loop it skips that its leap can come down on (the user, 2026-10-15 later: "Our jump on
# platform.ile is far too long, we need to change it so the car lands as soon as it can" -- it came down on the S west past its turn, the
# other pink, 38 cells on and 2.4 s in the air): from where the lap comes back east under the take-off, the first whose way the leap turns
# onto by LAND_TURN at the most, with LAND_ROOM cells of it before the landing to curve onto it (the leap's way: the ramp's, towards the S's
# turn) -- just past the S's turn, 25 cells on
LAND_TURN, LAND_ROOM = math.radians(40), 1.5
def leap_landing():
    lip = np.array([510.0, 533.0]); u_ = np.array([497.5, 514.0]) - lip; u_ /= np.linalg.norm(u_)
    back = nearest_after((486.8, 525.5), MARK['start'], 400)
    for j in range(int(60 / STEP)):
        k = (back + j) % N
        t_ = T[k] / np.linalg.norm(T[k])
        if math.acos(max(-1, min(1, float(u_ @ t_)))) > LAND_TURN: continue
        a_, b_ = np.linalg.solve(np.array([[u_[0], t_[0]], [u_[1], t_[1]]]), np.array([X[k] + OX, Z[k] + OZ]) - lip)
        if a_ > 0 and b_ >= LAND_ROOM: return k
    return nearest_after((484.0, 507.8), MARK['start'], 400)
SC_D = leap_landing()
SC_J = (SC_D + int(7 / STEP)) % N                                         # ... back on the lap's own road

# ---- heights (2026-10-15, round 4 -- the user: "Let's make all the track closer to the ground which means our inclines will be steeper
# exiting the frog"; the lap climbed at 13.5 % from the start straight's 6,500 to the frog's 20,400 before, high over the islands): the road
# as low as the island lets it -- CLEAR over the highest ground across it, LAVA_MIN over the lava, DECOR_CLEAR over the top of any decor
# near it (the builder takes away a decor that reaches into a raised road's space: the Wannies' houses and posts stay) -- rising and falling
# at GRADE_LOW at the most, as Volcano Island's and Otringal's roads close over the ground; round the loop the shortcut leaps over, up to
# where the leap comes down, JUMP_UP over its lip; up to the frog, as high as its lily pad needs to clear the Wannies' house on the hill
# under it (bodies 12 and 13: 5,613 to 10,637); level through the frog; from it up to the helix's top, steeply; down the helix to the start.
HALF = np.full(N, H0)
Y = np.zeros(N)
def climb(a, b, h0, h1, shape=lambda t: t):
    L = ahead(a, b)
    k = a
    while True:
        Y[k] = h0 + (h1 - h0) * shape(ahead(a, k) / L if L else 1)
        if k == b: break
        k = (k + 1) % N
# (narrower up the frog's ramp and through it -- into its mouth -- and back out after it)
iWide = (MARK['back'] + int(6 / STEP)) % N
for k in range(N):
    if between(k, MARK['foot'], MARK['lip']): HALF[k] = H0 + (FROG_HALF - H0) * ease(ahead(MARK['foot'], k) / ahead(MARK['foot'], MARK['lip']))
    elif between(k, MARK['lip'], MARK['back']): HALF[k] = FROG_HALF
    elif between(k, MARK['back'], iWide): HALF[k] = FROG_HALF + (H0 - FROG_HALF) * ease(ahead(MARK['back'], k) / ahead(MARK['back'], iWide))

# (the floors: the decor raster without the decors dropped -- RaceTrackIsland's DropDecors)
TOPX = TOP.copy()
for (x0, z0), (x1, z1), body in DROPPED:
    sl = (slice(max(0, int((z0 - OZ) * RQ)), int((z1 - OZ) * RQ) + 1), slice(max(0, int((x0 - OX) * RQ)), int((x1 - OX) * RQ) + 1))
    sub = TOPX[sl]; sub[WHO[sl] == body] = -1
def decor_top(x, z, reach):
    """The highest decor top within `reach` cells of (x, z) (cells from the origin), or -1."""
    i0, i1 = max(0, int((z - reach) * RQ)), min(TOPX.shape[0], int((z + reach) * RQ) + 1)
    j0, j1 = max(0, int((x - reach) * RQ)), min(TOPX.shape[1], int((x + reach) * RQ) + 1)
    if i0 >= i1 or j0 >= j1: return -1.0
    sub = TOPX[i0:i1, j0:j1]
    zz = (np.arange(i0, i1) + 0.5) / RQ - z; xx = (np.arange(j0, j1) + 0.5) / RQ - x
    m = (zz[:, None] ** 2 + xx[None, :] ** 2 <= reach * reach) & (sub >= 0)
    return float(sub[m].max()) if m.any() else -1.0
def floor_at(x, z, half, nx, nz):
    """How low a deck `half` wide across (nx, nz) may be at (x, z)."""
    gs = [ground(x + nx * o, z + nz * o) for o in np.linspace(-half - 1.0, half + 1.0, 11)]
    f = max(gs) + CLEAR
    if min(gs) < LAVA: f = max(f, LAVA_MIN)
    m = box_near(x, z, half + 0.85)
    return max(f, BOXA[m, 4].max() + DECOR_CLEAR) if m.any() else f
F = np.array([floor_at(X[k], Z[k], HALF[k], Nn[k, 0], Nn[k, 1]) for k in range(N)])

def envelope(f, g):
    """The lowest profile over f with no step steeper than g (a grade) from point to point."""
    L = np.array(f, float); d = g * STEP * 512
    for i in range(1, len(L)): L[i] = max(L[i], L[i - 1] - d)
    for i in range(len(L) - 2, -1, -1): L[i] = max(L[i], L[i + 1] - d)
    return L
SMOOTH = 12                                 # points: crests and dips rounded over 6 cells
def smooth(v, w):
    pad = w // 2
    return np.convolve(np.concatenate([np.full(pad, v[0]), v, np.full(w - 1 - pad, v[-1])]), np.ones(w) / w, mode='valid')
def low_road(f, g, soft):
    """The lowest road over the floors f, no steeper than g, its crests and dips rounded (the floors marked soft raised first by what
    the rounding takes off a crest; the others -- the ends held -- as they are)."""
    f = np.asarray(f, float)
    L = envelope(f + soft * g * 512 * SMOOTH * STEP / 4, g * 0.9)
    return envelope(np.maximum(smooth(L, SMOOTH), f), g)

# (the frog: its lily pad clear of the ground and the decor under it)
FMX, FMZ = (X[MARK['mouth']] + X[MARK['back']]) / 2, (Z[MARK['mouth']] + Z[MARK['back']]) / 2
pad_ground = max(ground(FMX + r * math.cos(a), FMZ + r * math.sin(a)) for r in np.linspace(0, PAD_R, 14) for a in np.linspace(0, 2 * math.pi, 37))
pad_decor = decor_top(FMX, FMZ, PAD_R)
H_FROG_PAD = max(pad_ground, pad_decor) + PAD_DROP + PAD_CLEAR

# (the lap from the start line to the frog's ramp: the lowest road over its floors; the start straight as high as the highest floor from
# the helix to it; level for the last LEVEL_IN points before the ramp's foot)
SEG = [(MARK['start'] + i) % N for i in range(int(round(ahead(MARK['start'], MARK['foot']) / STEP)) + 1)]
LEVEL_IN = int(5 / STEP)
start_floor = max(F[(MARK['hOut'] + i) % N] for i in range(int(round(ahead(MARK['hOut'], MARK['start']) / STEP)) + 1))
LAPX = np.zeros(N)                         # (the lap's floors where the shortcut is on it: as high as the shortcut needs to be there)
def lap_low(need_land, h_frog):
    f = np.array([max(F[k], LAPX[k]) for k in SEG])
    f[0] = max(f[0], start_floor)
    f[SEG.index(SC_D)] = max(f[SEG.index(SC_D)], need_land)
    f[-LEVEL_IN:] = np.maximum(f[-LEVEL_IN:], h_frog - 900)
    soft = np.ones(len(f)); soft[0] = 0; soft[-LEVEL_IN:] = 0
    return low_road(f, GRADE_LOW, soft)

# ---- the shortcut (the user, 2026-10-15: "change the jump section so it acts as a short cut ... add some boost pads around the other loop
# of track that the jump skips so if driven well it's the same as if not quicker than taking the jump"): a branch of the raised road.
# Where the lap turns west at the east island's south-west corner it goes straight on north-west, bends north-north-west up a ramp to the
# take-off (the user's pink), and leaps -- high, hanging in the air SC_FLIGHT_MS, curving west over the S's turn -- onto the lap past the
# turn (the other pink): the loop round the west island's south left out. The lap comes up to where the leap lands JUMP_UP over the lip (no
# jump lands lower than it took off). Round the loop, stronger boost pads (LOOP_PADS, SC_LOOP_LIFT times the car's top speed); the shortcut's
# own boost pad taken away (round 4: "let's remove the boost panel leading up to the jump on the right path"). Level with the lap where it
# leaves it (a little over it while the two decks overlap), no deck of its own where it lies on the lap's road; then as low as the ground
# lets it, as the lap.
SC_HALF = 3.5                           # its half width
SC_OVER = 40.0                          # over the lap's deck where they overlap
SC_RAMP, SC_KICK = 9.0, 700.0
# (its arc over the straight line from the lip to where it comes down, and its time in the air -- the engine's arcjump= sixth number: as the
# first leap's, 2,500 and 2.4 s over its 38.2 cells, for the cells it has now)
SC_ARC_PER, SC_FLIGHT_PER = 2500.0 / 38.2, 2400 / 38.2
SC_LOOP_LIFT = 2.1                      # the loop's boost pads' lift (the engine's own: 1.7)
P0 = np.array([X[SC_F] + OX, Z[SC_F] + OZ]); d0 = T[SC_F] / np.linalg.norm(T[SC_F])
# (the ramp's way: from the take-off towards the S's turn; the bend onto it where the north-west line meets it)
u = np.array([497.5, 514.0]) - SC_LIP; u /= np.linalg.norm(u)
t_, s_ = np.linalg.solve(np.array([[d0[0], u[0]], [d0[1], u[1]]]), SC_LIP - P0)
V2 = P0 + d0 * t_
turn2 = math.acos(max(-1, min(1, float(d0 @ u))))
tan2 = 10.0 * math.tan(turn2 / 2)
dr = rounded([(tuple(P0), 0.0), (tuple(V2), 10.0), (tuple(SC_LIP), 0.0)])
# (the leap: on along the ramp's way, curving round onto the lap's way where it comes down)
PD = np.array([X[SC_D] + OX, Z[SC_D] + OZ]); tD = T[SC_D] / np.linalg.norm(T[SC_D])
a_, b_ = np.linalg.solve(np.array([[u[0], tD[0]], [u[1], tD[1]]]), PD - SC_LIP)
V3 = SC_LIP + u * a_
turn3 = math.acos(max(-1, min(1, float(u @ tD))))
R3 = min(18.0, 0.9 * min(a_, b_) / math.tan(turn3 / 2))
fl = rounded([(tuple(SC_LIP), 0.0), (tuple(V3), R3), (tuple(PD), 0.0)])
def resample(pl):
    pl = pl[np.concatenate([[True], np.hypot(*np.diff(pl, axis=0).T) > 1e-9])]
    cum = np.concatenate([[0], np.cumsum(np.hypot(*np.diff(pl, axis=0).T))])
    m = max(1, int(round(cum[-1] / STEP)))
    t = np.linspace(0, cum[-1], m + 1)
    return np.interp(t, cum, pl[:, 0]), np.interp(t, cum, pl[:, 1]), cum[-1]
DX_, DZ_, dr_len = resample(dr); FX_, FZ_, fl_len = resample(fl)
SC_ARC = max(900.0, SC_ARC_PER * fl_len)
SC_FLIGHT_MS = int(round(max(900, SC_FLIGHT_PER * fl_len) / 50) * 50)
tailK = [k % N for k in range(SC_D + 1, SC_J + 1)]
BX = list(DX_) + list(FX_[1:]) + [X[k] + OX for k in tailK]; BZ = list(DZ_) + list(FZ_[1:]) + [Z[k] + OZ for k in tailK]
BM = len(BX)
sL = len(DX_) - 1                       # the take-off
sF = sL - int(SC_RAMP / STEP)           # the ramp's foot
sD = sL + len(FX_) - 1                  # the landing (on the lap)
sDF = min(BM - 1, sD + int(4 / STEP))   # its foot
bend_start = int((t_ - tan2) / STEP)
def lap_k(x, z): return min(range(N), key=lambda k: (X[k] + OX - x) ** 2 + (Z[k] + OZ - z) ** 2)
def lap_y(x, z):
    k = lap_k(x, z)
    return Y[k], math.hypot(X[k] + OX - x, Z[k] + OZ - z), HALF[k]
def b_normal(k):
    nx_, nz_ = -(BZ[min(k + 1, BM - 1)] - BZ[max(k - 1, 0)]), BX[min(k + 1, BM - 1)] - BX[max(k - 1, 0)]
    ln = math.hypot(nx_, nz_) or 1
    return nx_ / ln, nz_ / ln
BF = np.array([floor_at(BX[k] - OX, BZ[k] - OZ, SC_HALF, *b_normal(k)) for k in range(BM)])
# (with the lap -- a little over its deck, eased in -- until the two have parted, then as low as the ground lets it to the ramp's foot)
def over(dist, lh, half): return SC_OVER * min(1.0, max(0.0, (dist + half - lh - 0.3) / 1.5))
kd = next(k for k in range(BM) if lap_y(BX[k], BZ[k])[1] >= lap_y(BX[k], BZ[k])[2] + SC_HALF)
BY = np.zeros(BM)
# (how high it needs to be at each point to climb no steeper than GRADE_LOW over its floors to the ramp's foot; the lap under it as high
# while they are together -- off the east island's plateau the lap goes down, the shortcut stays over it)
_bneed = BF[:sF + 1].copy()
for i in range(sF - 1, -1, -1): _bneed[i] = max(_bneed[i], _bneed[i + 1] - GRADE_LOW * STEP * 512)
for k in range(kd): LAPX[lap_k(BX[k], BZ[k])] = max(LAPX[lap_k(BX[k], BZ[k])], _bneed[k])
def shortcut_heights():
    for k in range(kd): ly, dist, lh = lap_y(BX[k], BZ[k]); BY[k] = ly + over(dist, lh, SC_HALF)
    f = BF[kd - 1:sF + 1].copy(); f[0] = BY[kd - 1]
    BY[kd - 1:sF + 1] = envelope(f, GRADE_LOW)
    yFoot = BY[sF]; yLip = yFoot + SC_KICK
    for k in range(sF + 1, sL + 1): BY[k] = yFoot + SC_KICK * ease((k - sF) / (sL - sF))
    return yLip

# (the lap and the shortcut together: the leap's landing up over its lip, the frog's foot up to the frog -- till they settle)
need_land, H_FROG = 0.0, H_FROG_PAD
for _ in range(6):
    Lr = lap_low(need_land, H_FROG)
    H_FROG = max(H_FROG, Lr[-1] + 900)
    START_H = Lr[0]
    for i, k in enumerate(SEG): Y[k] = Lr[i]
    yLip = shortcut_heights()
    if Y[SC_D] >= yLip + JUMP_UP - 1 and Lr[-1] <= H_FROG - 900 + 1 and all(Y[k] >= LAPX[k] - 1 for k in SEG): break
    need_land = yLip + JUMP_UP
H_BOTTOM = START_H + 300.0              # (out of the helix, eased down onto the start straight)
for k in SEG[-LEVEL_IN:]: Y[k] = H_FROG - 900
climb(MARK['pier'], MARK['start'], START_H, START_H)
for k in range(MARK['foot'], MARK['lip'] + 1): Y[k % N] = H_FROG - 900 + 900 * ease(ahead(MARK['foot'], k) / ahead(MARK['foot'], MARK['lip']))
climb(MARK['lip'], MARK['back'], H_FROG, H_FROG)
# (from the frog up to the helix's top at an even grade, eased in and out over CLIMB_EASE of it)
CLIMB_EASE = 0.08
def even(t, e=CLIMB_EASE):
    if t < e: return t * t / (2 * e * (1 - e))
    if t > 1 - e: return 1 - (1 - t) ** 2 / (2 * e * (1 - e))
    return (t - e / 2) / (1 - e)
climb(MARK['back'], MARK['top'], H_FROG, H_TOP, even)
climb(MARK['top'], MARK['hOut'], H_TOP, H_BOTTOM)
climb(MARK['hOut'], MARK['pier'], H_BOTTOM, START_H, ease)
yLip = shortcut_heights()
H_FORK, H_LAND = Y[SC_F], Y[SC_D]
# (the leap: from the lip to the lap where it comes down, an arc SC_ARC over the straight line; the lap's own after it)
yLand = Y[SC_D]
for k in range(sL + 1, BM):
    if k < sD:
        f_ = (k - sL) / (sD - sL)
        BY[k] = yLip + (yLand - yLip) * f_ + 4 * SC_ARC * f_ * (1 - f_)
    else: BY[k] = lap_y(BX[k], BZ[k])[0]
B_HALF = np.full(BM, SC_HALF); b_hidden = np.zeros(BM, bool)
for k in range(BM):
    ly, dist, lh = lap_y(BX[k], BZ[k])
    if dist + SC_HALF <= lh + 0.3 and abs(BY[k] - ly) < 120: b_hidden[k] = True        # wholly on the lap's road, at its height
b_gap = np.zeros(BM, bool); b_gap[sL + 1:sD] = True
b_hidden &= ~b_gap
bgrade = np.diff(BY) / (STEP * 512)
hid = [[int(a), int(b)] for a, b in zip(*[np.flatnonzero(np.diff(np.concatenate([[0], b_hidden.astype(int), [0]])) == v) - (0 if v == 1 else 1) for v in (1, -1)])]
# (the jump into the frog: the gap from the lip to the mouth -- no road there)
gap = np.zeros(N, bool)
for k in range(MARK['lip'] + 1, MARK['mouth']): gap[k % N] = True
# (the car carried only from the lip -- the user drives up the ramp: "The game takes control of the car too early ... let's wait until the
# last second to guide it through the frog", 2026-10-15; from the ramp's foot before)
arcs = [('frog', (MARK['lip'] - 1) % N, MARK['lip'], MARK['mouth'], (MARK['mouth'] + int(4 / STEP)) % N)]
grade = (np.roll(Y, -1) - Y) / (STEP * 512)
ride = ~gap
# the loop's boost pads: just past the fork, on the lap's way west past its corner, on the S's lower leg -- each 4 cells, the road's middle
LOOP_PADS = []
for where, after in (((None, 14.0), SC_F), (((487.0, 547.3), None), SC_F), (((481.0, 527.0), None), SC_F)):
    if where[0] is None: k = (after + int(where[1] / STEP)) % N
    else: k = nearest_after(where[0], after, 300)
    LOOP_PADS.append([int(k), int((k + int(4 / STEP)) % N), -1.6, 1.6, SC_LOOP_LIFT])

# ---- the pit lane (the user, 2026-10-15: "The main path now goes straight on, with a thinner pitlane to the side"; round 4: "Let's widen
# up the pitlane entry and exit as shown"): a branch of the raised road beside the start straight, down the pier's east side past the start
# line -- the opponents' waiting places along it. Laid out in the lap's own frame: at each of the lap's points from PL_F to PL_J its outer
# edge OUT_EDGE(s) and inner edge IN_EDGE(s), cells west of the lap's middle (Nn). Its outer edge a straight taper from the lap's own edge out to
# the pit lane's straight at each end -- the helix's last straight from z Z_A to the straight's start at Z_B, and from the straight's end at
# Z_C round the lap EXIT_LEN -- a wide mouth, one deck with the lap's there (no rail between them, no curb: RaceTrackRaisedBody.Tile's open
# sides); its inner edge 1.5 cells into the lap's deck until the two part. A little over the lap's deck where the two overlap; no deck of
# its own where it lies wholly on the lap's.
Z_A, Z_B, Z_C = 473.5, 486.0, 511.0          # (the entry's taper clear of the elevator's landing platform's corner, body 20: 549.6, 479.6)
EXIT_LEN = 14.0
O_PIT, I_PIT = 558.5 - (PIT_X - PIT_HALF), 558.5 - (PIT_X + PIT_HALF)    # the straight's outer and inner edges west of the lap's middle
I_IN = H0 - 1.5
_kA = nearest_after((558.5, Z_A), (MARK['hOut'] - int(16 / STEP)) % N, 20)
PL_F = (_kA - int(8 / STEP)) % N
_kB = nearest_after((558.5, Z_B), _kA, 30)
_kC = nearest_after((558.5, Z_C), _kB, 60)
sA, sB, sC = ahead(PL_F, _kA), ahead(PL_F, _kB), ahead(PL_F, _kC)
sE = sC + EXIT_LEN
PL_J = (PL_F + int(round((sE + 6.0) / STEP))) % N
sJ = ahead(PL_F, PL_J)
def lerp_e(a, b, t): return a + (b - a) * ease(t)
def OUT_EDGE(s):
    if s < sA: return lerp_e(PIT_HALF, H0, s / sA)
    if s < sB: return H0 + (O_PIT - H0) * (s - sA) / (sB - sA)
    if s < sC: return O_PIT
    if s < sE: return O_PIT + (H0 - O_PIT) * (s - sC) / (sE - sC)
    return lerp_e(H0, PIT_HALF, (s - sE) / (sJ - sE))
def IN_EDGE(s):
    if s < sA: return lerp_e(-PIT_HALF, I_IN, s / sA)
    if s < sB - 1.0: return I_IN
    if s < sB + 2.0: return lerp_e(I_IN, I_PIT, (s - sB + 1.0) / 3.0)
    if s < sC - 1.0: return I_PIT
    if s < sC + 2.0: return lerp_e(I_PIT, I_IN, (s - sC + 1.0) / 3.0)
    if s < sE: return I_IN
    return lerp_e(I_IN, -PIT_HALF, (s - sE) / (sJ - sE))
pitK = [(PL_F + i) % N for i in range(int(round(sJ / STEP)) + 1)]
_s = np.array([ahead(PL_F, k) for k in pitK])
# (the outer edge's corners where the tapers meet the straight rounded: the edges smoothed over 3 cells)
_o = smooth(np.array([OUT_EDGE(s) for s in _s]), 6); _i = smooth(np.array([IN_EDGE(s) for s in _s]), 4)
_o[0], _o[-1], _i[0], _i[-1] = PIT_HALF, PIT_HALF, -PIT_HALF, -PIT_HALF
PM = len(pitK) - 1
PX = np.array([X[k] + OX + Nn[k, 0] * (_o[n] + _i[n]) / 2 for n, k in enumerate(pitK)])
PZ = np.array([Z[k] + OZ + Nn[k, 1] * (_o[n] + _i[n]) / 2 for n, k in enumerate(pitK)])
P_HALF = (_o - _i) / 2
pl_cum = np.concatenate([[0], np.cumsum(np.hypot(np.diff(PX), np.diff(PZ)))])
p_hidden = _o <= H0 + 0.3
# (over the lap's deck while the two overlap, eased in from where its own deck begins; level with the lap beside it once apart)
PY = np.array([Y[k] + (0.0 if p_hidden[n] else SC_OVER * min(1.0, (_o[n] - H0 - 0.3) / 2.0, max(0.0, (H0 + 0.3 - _i[n]) / 1.5)))
               for n, k in enumerate(pitK)])
PF = np.array([floor_at(PX[n] - OX, PZ[n] - OZ, P_HALF[n], Nn[k, 0], Nn[k, 1]) for n, k in enumerate(pitK)])
pit_hid = [[int(a), int(b)] for a, b in zip(*[np.flatnonzero(np.diff(np.concatenate([[0], p_hidden.astype(int), [0]])) == v) - (0 if v == 1 else 1) for v in (1, -1)])]
PIT_SPOT_LIST = []
for zs in PIT_SPOTS:
    k = int(np.argmin(np.abs(PZ - zs) + 100 * (np.abs(PX - PIT_X) > 0.2)))
    PIT_SPOT_LIST.append([round(float(PX[k] - OX), 3), round(float(PZ[k] - OZ), 3), 0.0, 1.0, round(float(PY[k]), 1)])

# ---- checks
out = [f'lap {total:.1f} cells, {N} points (the last joint {gapclose:.2f} cells); heights {Y.min():.0f} to {Y.max():.0f}; steepest driven '
       f'{np.abs(grade[ride]).max() * 100:.1f} %']
for name, (a, b) in {'start to the frog': (MARK['start'], MARK['foot']), 'frog to the top': (MARK['back'], MARK['top']),
                     'the helix': (MARK['top'], MARK['hOut']), 'out of it': (MARK['hOut'], MARK['pier'])}.items():
    out.append(f'  {name}: {ahead(a, b):.0f} cells, {Y[a]:.0f} to {Y[b]:.0f}, mean grade {abs(Y[b] - Y[a]) / (ahead(a, b) * 512) * 100:.1f} %')
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
        if not (0 <= i < TOPX.shape[0] and 0 <= j < TOPX.shape[1]) or TOPX[i, j] < 0: continue
        if TOPX[i, j] > Y[k] - 350 and BOT[i, j] < Y[k] + 1400:
            b = int(WHO[i, j]); hits.setdefault(b, []).append((round(float(x + OX), 1), round(float(z + OZ), 1), int(TOPX[i, j]), int(Y[k])))
out.append('  decor in the road\'s space (body: places, e.g.): ' + '; '.join(f'{b}: {len(h)} {h[0]}' for b, h in sorted(hits.items())))
def taken(xs, zs, ys, halfs, skip):
    t = {}
    for n in range(len(xs)):
        if skip[n]: continue
        m = box_near(xs[n] - OX, zs[n] - OZ, halfs[n] + 0.75) & (BOXA[:, 4] >= ys[n] - 320) & (BOXA[:, 5] <= ys[n] + 1400)
        for b in np.flatnonzero(m): t.setdefault(int(BOXA[b, 6]), (round(float(xs[n]), 1), round(float(zs[n]), 1), int(ys[n])))
    return t
low = [k for k in SEG if Y[k] < F[k] - 1]
out.append(f'  start straight {START_H:.0f}, out of the helix {H_BOTTOM:.0f}; the frog {H_FROG:.0f} (its pad needs {H_FROG_PAD:.0f}: the ground under it {pad_ground:.0f}, '
           f'decor {pad_decor:.0f}); under a floor from the start to the frog: {len(low)} points' + (f' (the most {max(F[k] - Y[k] for k in low):.0f})' if low else ''))
out.append(f'  over the ground from the start to the frog: median {np.median([Y[k] - F[k] + CLEAR for k in SEG]):.0f}, the most {max(Y[k] - F[k] + CLEAR for k in SEG):.0f} '
           f'(was 4,000 to 14,000); steepest there {max(abs(grade[k]) for k in SEG[:-1]) * 100:.1f} %; frog to the top {max(abs(grade[k]) for k in range(MARK["back"], MARK["top"])) * 100:.1f} %; '
           f'the helix {abs(grade[(MARK["top"] + 20) % N]) * 100:.1f} %')
idx = np.arange(N); close = []; under = []
for i in range(0, N, 2):
    if gap[i]: continue
    dd = np.hypot(X - X[i], Z - Z[i]); sep = np.minimum(np.abs(idx - i), N - np.abs(idx - i))
    near_ = (sep > 40) & (dd < HALF + HALF[i] + 0.6) & ~gap
    if (near_ & (np.abs(Y - Y[i]) < 2800)).any():
        m = near_ & (np.abs(Y - Y[i]) < 2800); j = int(np.argmax(m)); close.append((abs(Y[i] - Y[j]), i, j))
    below = near_ & (Y < Y[i])
    if below.any(): under.append((Y[i] - Y[below].max(), i))
for dy, i, j in sorted(close)[:4]:
    out.append(f'  CLASH: ({X[i] + OX:.1f}, {Z[i] + OZ:.1f}) {Y[i]:.0f} and ({X[j] + OX:.1f}, {Z[j] + OZ:.1f}) {Y[j]:.0f}: {dy:.0f} apart')
if under:
    out.append('  stacked: the least ' + ', '.join(f'{c:.0f} at ({X[i] + OX:.1f}, {Z[i] + OZ:.1f})' for c, i in sorted(under)[:3]))
for m, k in MARK.items():
    out.append(f'    {m:6s} point {k:4d} ({X[k] + OX:6.1f}, {Z[k] + OZ:6.1f}) {Y[k]:6.0f} (ground {ground(X[k], Z[k]):5.0f})')
# the shortcut: its marks, steepest grade, the jump, what it passes over, its decor hits, how near the rest of the lap is
out.append(f'  shortcut: {BM} points, {dr_len:.1f} cells to its take-off, a leap of {fl_len:.1f} (curving {math.degrees(turn3):.0f} deg, radius {R3:.1f}), then {len(tailK) * STEP:.1f} along the lap; '
           f'the lap there {ahead(SC_F, SC_J):.1f} cells; the lap {H_FORK:.0f} at the fork, {H_LAND:.0f} where the leap comes down; loop pads {LOOP_PADS}')
for name, k in (('fork', 0), ('bend0', bend_start), ('foot', sF), ('lip', sL), ('land', sD), ('landFt', sDF), ('join', BM - 1)):
    out.append(f'    {name:6s} {k:4d} ({BX[k]:6.1f}, {BZ[k]:6.1f}) {BY[k]:6.0f} (ground {ground(BX[k] - OX, BZ[k] - OZ):5.0f}){" hidden" if b_hidden[k] else ""}')
out.append(f'    steepest driven {max(abs(g) for i, g in enumerate(bgrade) if not b_gap[i] and not b_gap[i + 1]) * 100:.1f} %; the jump {BY[sD] - BY[sL]:+.0f} over {(sD - sL) * STEP:.1f} cells; hidden {hid}')
bh = {}
for k in range(BM):
    if b_gap[k]: continue
    nx_, nz_ = -(BZ[min(k + 1, BM - 1)] - BZ[max(k - 1, 0)]), BX[min(k + 1, BM - 1)] - BX[max(k - 1, 0)]
    ln = math.hypot(nx_, nz_) or 1
    for o in np.linspace(-SC_HALF - 0.3, SC_HALF + 0.3, 9):
        x, z = BX[k] - OX + nx_ / ln * o, BZ[k] - OZ + nz_ / ln * o
        i, j = int(z * RQ), int(x * RQ)
        if not (0 <= i < TOPX.shape[0] and 0 <= j < TOPX.shape[1]) or TOPX[i, j] < 0: continue
        if TOPX[i, j] > BY[k] - 350 and BOT[i, j] < BY[k] + 1400: bh.setdefault(int(WHO[i, j]), []).append((round(x + OX, 1), round(z + OZ, 1)))
out.append('    decor in its space: ' + '; '.join(f'{b}: {len(h)} {h[0]}' for b, h in sorted(bh.items())))
fl = list(range(sL, sD + 1))
out.append(f'    under the flight: the ground at most {max(ground(BX[k] - OX, BZ[k] - OZ) for k in fl):.0f}, the flight at least {min(BY[k] for k in fl):.0f}')
_fo = [(BY[k] - Y[m], k, m) for k in fl for m in range(N) if not between(m, (SC_D - int(10 / STEP)) % N, SC_J) and math.hypot(X[m] + OX - BX[k], Z[m] + OZ - BZ[k]) < HALF[m] + 2.0]
out.append(f'    the flight over the lap: {min(_fo)[0]:.0f} at the least (its point {min(_fo)[1]}, the lap\'s {min(_fo)[2]})' if _fo else '    the flight over the lap: nowhere')
out.append(f'    under a floor up to its lip: {sum(1 for k in range(kd, sL + 1) if BY[k] < BF[k] - 1)} points; the lip {yLip:.0f}, the landing {Y[SC_D]:.0f}')
cl = []
for k in range(0, BM, 2):
    if b_hidden[k] or b_gap[k]: continue
    for m in range(0, N, 2):
        if between(m, SC_F, SC_J) and abs(Y[m] - BY[k]) < 2600:
            dd = math.hypot(X[m] + OX - BX[k], Z[m] + OZ - BZ[k]) - HALF[m] - SC_HALF
            if dd < 0.5: cl.append((round(dd, 1), k, m, round(BY[k] - Y[m])))
out.append(f'    its deck near the lap (cells apart, its point, the lap point, height over it): {sorted(cl)[:6]}')
pgrade = np.diff(PY) / (STEP * 512)
ph = {}
for k in range(PM + 1):
    if p_hidden[k]: continue
    nx_, nz_ = -(PZ[min(k + 1, PM)] - PZ[max(k - 1, 0)]), PX[min(k + 1, PM)] - PX[max(k - 1, 0)]
    ln = math.hypot(nx_, nz_) or 1
    for o in np.linspace(-P_HALF[k] - 0.3, P_HALF[k] + 0.3, 9):
        x, z = PX[k] - OX + nx_ / ln * o, PZ[k] - OZ + nz_ / ln * o
        i, j = int(z * RQ), int(x * RQ)
        if not (0 <= i < TOPX.shape[0] and 0 <= j < TOPX.shape[1]) or TOPX[i, j] < 0: continue
        if TOPX[i, j] > PY[k] - 350 and BOT[i, j] < PY[k] + 1400: ph.setdefault(int(WHO[i, j]), []).append((round(x + OX, 1), round(z + OZ, 1), int(TOPX[i, j]), int(PY[k])))
out.append('  pit lane decor in its space: ' + '; '.join(f'{b}: {len(h)} {h[0]}' for b, h in sorted(ph.items())))
_tl = taken(X + OX, Z + OZ, Y, HALF, gap); _tb = taken(np.array(BX), np.array(BZ), BY, B_HALF, b_gap | b_hidden); _tp = taken(PX, PZ, PY, P_HALF, p_hidden)
out.append(f'  decors the builder would take away (body: a place, the deck): lap {_tl}; shortcut {_tb}; pit lane {_tp}')
out.append(f'  pit lane under a floor: {sum(1 for n in range(PM + 1) if not p_hidden[n] and PY[n] < PF[n] - 1)} points; its widest {P_HALF.max():.2f} either side; '
           f'its mouths: z {Z_A:.0f} to {Z_B:.0f}, from z {Z_C:.0f} {EXIT_LEN:.0f} cells round')
out.append(f'  pit lane: {PM + 1} points, {pl_cum[-1]:.1f} cells from the straight at ({PX[0]:.1f}, {PZ[0]:.1f}) to ({PX[-1]:.1f}, {PZ[-1]:.1f}), {PY.min():.0f} to {PY.max():.0f}, '
           f'steepest {np.abs(pgrade).max() * 100:.1f} %, hidden {pit_hid}; its spots {[(p[0] + OX, p[1] + OZ, p[4]) for p in PIT_SPOT_LIST]}; '
           f'the straight at the start line {X[MARK["start"]] + OX:.1f}, the pit lane {PX[int(np.argmin(np.abs(PZ - (Z[MARK["start"]] + OZ))))]:.1f}')
print('\n'.join(out))

plan = {
    'originCellX': OX, 'originCellZ': OZ,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in zip(X, Z)],
    'heights': [round(float(y), 1) for y in Y],
    'maxGrade': round(float(np.abs(grade[ride]).max()) + 0.05, 3),
    'raised': [0, N - 1], 'raisedHalf': H0, 'asphaltHalf': ASPHALT, 'curbHalf': CURB,
    'raisedHalfs': [round(float(h), 3) for h in HALF],
    'arcJumps': [[int(iF), int(iL), int(iD), int(iDF), 0] for name, iF, iL, iD, iDF in arcs],
    'start': int(MARK['start']),
    'pitSpots': PIT_SPOT_LIST,
    'boostPanels': LOOP_PADS,
    'raisedCut': True,
    'keepBodies': [19, 20, 21, 27, 28, 29, 30, 31],
    # the giant frog (RaceTrackFrog): into its mouth -- the carried jump's landing -- and out of its back; the camera kept low in its throat
    'frog': {'mouth': int(MARK['mouth']), 'back': int(MARK['back'])},
    'tunnels': [{'from': int(MARK['mouth']), 'to': int(MARK['back']), 'roof': 2700, 'pieces': False}],
    # the shortcut (RaceTrackPlan.Branches): off the lap at SC_F, over the boost pad, up the ramp, the jump, onto the lap at SC_J
    'branches': [{
        'points': [[round(float(x - OX), 3), round(float(z - OZ), 3)] for x, z in zip(BX, BZ)],
        'heights': [round(float(y), 1) for y in BY],
        'halfs': [round(float(h), 3) for h in B_HALF],
        'from': int(SC_F), 'to': int(SC_J),
        'arcJumps': [[int(sL - 1), int(sL), int(sD), int(sDF), -1, SC_FLIGHT_MS]],      # (carried from its lip, as the frog's jump)
        'hidden': hid,
    }, {
        # the pit lane: off the start straight at PL_F, back onto it at PL_J
        'points': [[round(float(x - OX), 3), round(float(z - OZ), 3)] for x, z in zip(PX, PZ)],
        'heights': [round(float(y), 1) for y in PY],
        'halfs': [round(float(h), 3) for h in P_HALF],
        'from': int(PL_F), 'to': int(PL_J),
        'hidden': pit_hid,
    }],
}
if '--write' in sys.argv:
    json.dump(plan, open(OUT, 'w'))
    print('written', OUT)
np.savez(D + 'lap.npz', X=X + OX, Z=Z + OZ, Y=Y, HALF=HALF, gap=gap)

if '--pictures' in sys.argv:
    from PIL import Image, ImageDraw
    Zm = 6
    img = Image.open('E:/dump/TEMP/kmq/plat_top.png').convert('RGB')
    d = ImageDraw.Draw(img)
    P = lambda x, z: (x * Zm, z * Zm)
    lo, hi = Y.min(), Y.max()
    for k in range(N):
        j = (k + 1) % N
        t = (Y[k] - lo) / (hi - lo)
        col = (int(255 * t), int(255 * (1 - abs(2 * t - 1))), int(255 * (1 - t)))
        if gap[k]: col = (255, 255, 255)
        for o in (-HALF[k], HALF[k]):
            d.line([P(X[k] + Nn[k, 0] * o, Z[k] + Nn[k, 1] * o), P(X[j] + Nn[j, 0] * o, Z[j] + Nn[j, 1] * o)], fill=col, width=1)
    for m, k in MARK.items(): d.text(P(X[k], Z[k]), m, fill=(255, 255, 255))
    for k in range(PM):
        d.line([P(PX[k] - OX, PZ[k] - OZ), P(PX[k + 1] - OX, PZ[k + 1] - OZ)], fill=(80, 80, 80) if p_hidden[k] else (255, 120, 0), width=3)
    for p_ in PIT_SPOT_LIST: d.ellipse([P(p_[0], p_[1])[0] - 4, P(p_[0], p_[1])[1] - 4, P(p_[0], p_[1])[0] + 4, P(p_[0], p_[1])[1] + 4], fill=(255, 255, 0))
    for k in range(BM - 1):
        col = (255, 255, 255) if b_gap[k] else (80, 80, 80) if b_hidden[k] else (255, 160, 0)
        d.line([P(BX[k] - OX, BZ[k] - OZ), P(BX[k + 1] - OX, BZ[k + 1] - OZ)], fill=col, width=3)
    img.save(D + 'lap.png')
