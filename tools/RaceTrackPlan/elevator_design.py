# The Elevator Platform (ASCENCE, one cube, scene 120): a rollercoaster of a lap. The "island" is a few decks on legs over the sea -- 28
# cells by 24 -- with the elevator's tower in the middle, so the lap is built upwards and all of it is a raised road on piers:
#   the station on the south side of the tower, in front of the elevator's door (level with the main deck);
#   the lift: a helix round the tower, twice and a quarter, to above its top;
#   the first drop, north off the helix's east side, into a diving turn;
#   a camelback along the north edge, a turn-round, a second hill that passes under the drop and climbs to the east side;
#   over the airship moored there, a dive round the south-east corner, three hops along the south edge, and back round into the station.
#
# Run with E:\dump\TEMP\elev holding H.npy (the island's heights) and decorpoints.faces.csv (its decor bodies' triangles, from
# `ScriptRoundTrip decorpoints ASCENCE <folder>\decorpoints.csv`); island_mesh.py reads them. --pictures draws the lap over the platform.
# Writes docs/racetrack/elevator_track_plan.json.
import json, math, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
os.environ.setdefault('ISLAND_MESH_DIR', 'E:/dump/TEMP/elev/')
import island_mesh as mesh

D = mesh.D
OUT = 'E:/dump/LBAAssembler/docs/racetrack/elevator_track_plan.json'
ORIGIN = 448                      # the cube's first island cell (cube 7,7)

# ---- the road
# half widths, cells: the asphalt, the curbs' outer edge, the raised road's edge (its rail). Narrower than any other track's (a car is
# a cell and a half wide): a rollercoaster's road, and the helix has to fit between the tower and the crane house
ASPHALT, CURB, DECK = 2.4, 3.0, 3.25
STEP = 0.5
# the ring round the tower (the middle of the road): the tower with its side frames is x 23.0..32.0, z 31.4..40.4 (a lamp on a bracket
# reaches a cell further out at two of its corners, at 7500-8100, between the helix's levels); the crane house east of it stands on
# legs from x 39.32
# (the ring's corners are rounded and the tower's are not: the sides stand half a cell off it so that its corners clear the inside rail)
RING_W, RING_E = 19.05, 35.95
RING_N, RING_S = 27.45, 44.95     # (the south side is the station: its rail along the main deck's south edge, z 41.7)
RING_R = 5.0
TURNS = int(os.environ.get('ELEV_TURNS', '2'))     # whole turns of the helix before the quarter that leaves it
START_X = 25.5                    # the start line: in front of the elevator's door (x 24.4..30.7), well before the first bend
DECK_H = 4028.0                   # the main deck's top: the station is level with it
# the north edge, the straight back east (its rail a cell clear of the helix's north side, where that side's columns stand), the south edge
NORTH_Z, MID_Z, SOUTH_Z = 8.0, 19.5, 56.35
WEST_X, EAST_X = 6.5, 55.5        # the turn-round's west end, the east side (over the airship, between its hulls)
DROP_R, TURN_R = 6.0, 5.5
STATION_WEST = 6.7                # the west end of the turn into the station

def fillet(vertices):
    """a polyline of (x, z, radius) with its corners rounded: points 0.05 apart"""
    def line(a, b):
        n = max(1, int(round(np.linalg.norm(b - a) / 0.05)))
        return [a + (b - a) * k / n for k in range(1, n + 1)]
    out = [np.array(vertices[0][:2], float)]
    prev = out[0]
    for i in range(1, len(vertices) - 1):
        v = np.array(vertices[i][:2], float); a = np.array(vertices[i - 1][:2], float); b = np.array(vertices[i + 1][:2], float)
        radius = vertices[i][2]
        d1 = (v - a) / np.linalg.norm(v - a); d2 = (b - v) / np.linalg.norm(b - v)
        ang = math.acos(np.clip(d1 @ d2, -1, 1))
        t = radius * math.tan(ang / 2)
        p1 = v - d1 * t; p2 = v + d2 * t
        out += line(prev, p1)
        side = np.sign(d1[0] * d2[1] - d1[1] * d2[0])
        n1 = np.array([-d1[1], d1[0]]) * side
        c = p1 + n1 * radius
        a0 = math.atan2(p1[1] - c[1], p1[0] - c[0]); a1 = a0 + side * ang
        m = max(2, int(round(radius * ang / 0.05)))
        out += [c + radius * np.array([math.cos(a0 + (a1 - a0) * k / m), math.sin(a0 + (a1 - a0) * k / m)]) for k in range(1, m + 1)]
        prev = p2
    out += line(prev, np.array(vertices[-1][:2], float))
    return out

ring = [(RING_E, RING_S, RING_R), (RING_E, RING_N, RING_R), (RING_W, RING_N, RING_R), (RING_W, RING_S, RING_R)]
route = [(START_X, RING_S, 0)]
for _ in range(TURNS): route += ring
route += [(RING_E, RING_S, RING_R),                       # the last quarter, and north off the helix
          (RING_E, NORTH_Z, DROP_R),                      # the drop's diving turn, onto the north edge
          (WEST_X, NORTH_Z, (MID_Z - NORTH_Z) / 2), (WEST_X, MID_Z, (MID_Z - NORTH_Z) / 2),          # the turn-round
          (EAST_X, MID_Z, TURN_R),                        # east under the drop, and south
          (EAST_X, SOUTH_Z, TURN_R),                      # over the airship, and west along the south edge
          (STATION_WEST, SOUTH_Z, (SOUTH_Z - RING_S) / 2), (STATION_WEST, RING_S, (SOUTH_Z - RING_S) / 2),   # round into the station
          (START_X, RING_S, 0)]
fine = np.array(fillet(route)[:-1])
seg = np.linalg.norm(np.diff(np.vstack([fine, fine[:1]]), axis=0), axis=1)
cum = np.concatenate([[0], np.cumsum(seg)]); total = cum[-1]
N = int(round(total / STEP))
s = np.arange(N) * total / N
X = np.interp(s, cum, np.append(fine[:, 0], fine[0, 0])); Z = np.interp(s, cum, np.append(fine[:, 1], fine[0, 1]))

def s_at(x, z, nth=0):
    """how far round the lap the road passes (x, z), the nth time"""
    d = np.hypot(fine[:, 0] - x, fine[:, 1] - z)
    passes = [k for k in range(len(d)) if d[k] < 0.3 and d[k] <= d[k - 1] and d[k] <= d[(k + 1) % len(d)]]
    merged = [passes[0]] if passes else []
    for k in passes[1:]:
        if k - merged[-1] > 40: merged.append(k)
    return float(cum[merged[nth]])

# ---- heights: keys along the lap joined by straight grades, the kinks rounded into vertical curves
ring_lap = 2 * (RING_E - RING_W + RING_S - RING_N) - 8 * RING_R + 2 * math.pi * RING_R
# the helix: one grade from just past the start line, its levels PITCH apart (the camera that follows the car has to fit between them)
CLIMB_FROM = 3.0
PITCH = float(os.environ.get('ELEV_PITCH', '4800'))     # (and the second level has to pass over the lamp on the tower's south-east corner, 8060)
GRADE_HELIX = PITCH / (512 * ring_lap)
s_top = s_at(RING_E, 35.0, TURNS)          # the top: on the east side, the last time round
H_TOP = DECK_H + GRADE_HELIX * 512 * (s_top - CLIMB_FROM)
s_crest = s_at(RING_E, 25.0)
s_dive = s_at(RING_E, NORTH_Z + DROP_R)                        # the diving turn's start
s_north = s_at(RING_E - DROP_R, NORTH_Z)                       # ... and its end, heading west
s_nw0 = s_at(WEST_X + (MID_Z - NORTH_Z) / 2, NORTH_Z); s_nw1 = s_at(WEST_X + (MID_Z - NORTH_Z) / 2, MID_Z)
s_under = s_at(RING_E, MID_Z, 1)                                # under the drop
s_ne0 = s_at(EAST_X - TURN_R, MID_Z); s_ne1 = s_at(EAST_X, MID_Z + TURN_R)
s_se0 = s_at(EAST_X, SOUTH_Z - TURN_R); s_se1 = s_at(EAST_X - TURN_R, SOUTH_Z)
RS = (SOUTH_Z - RING_S) / 2
s_sw0 = s_at(STATION_WEST + RS, SOUTH_Z); s_sw1 = s_at(STATION_WEST + RS, RING_S, 0)
DROP = float(os.environ.get('ELEV_DROP', '1.25'))               # the first drop's grade
h_dive = H_TOP - DROP * 512 * (s_dive - s_crest)
h_north = h_dive - 0.42 * 512 * (s_north - s_dive)
# over the airship: its fins (7381) are under the road at z 28..31, its hulls (5825, 6225 at their tails) from there to z 45
FINS, HULL, TAILS = 7800.0, 6500.0, 7300.0
def z_on_east(zv): return s_ne1 + (zv - (MID_Z + TURN_R))
keys = [
    (0.0, DECK_H), (CLIMB_FROM, DECK_H), (s_top, H_TOP), (s_crest, H_TOP),
    (s_dive, h_dive), (s_north, h_north), (s_north + 5.0, 3500.0),          # ... the drop's foot, on the north edge
    (s_north + 12.0, 6200.0), (s_nw0 + 4.5, 3200.0),                          # the camelback along the north edge, down into the turn-round
    (s_nw1, 3000.0),
    (s_nw1 + 5.0, 3000.0), (s_nw1 + 12.5, 5200.0), (s_under - 3.5, 3300.0),   # a hill, and down under the drop
    (s_under + 1.0, 3300.0),
    (z_on_east(26.5), FINS), (z_on_east(31.0), FINS), (z_on_east(37.0), HULL), (z_on_east(43.0), TAILS), (z_on_east(45.0), TAILS),
    (s_se1 + 4.0, 3200.0),                                                      # the dive round the south-east corner
    (s_se1 + 11.0, 4900.0), (s_se1 + 18.0, 3100.0), (s_se1 + 25.0, 4700.0), (s_se1 + 32.0, 3100.0),   # the hops
    (total - 23.0, DECK_H), (total, DECK_H),
]
ks, kh = np.array([k[0] for k in keys]), np.array([k[1] for k in keys])
assert (np.diff(ks) > 0).all(), [(round(a, 1), round(b, 1)) for a, b in zip(ks[:-1], ks[1:]) if b <= a]
raw = np.interp(s, ks, kh)
def rounded(h, window):
    w = max(1, int(round(window / STEP)))
    k = np.ones(w) / w
    pad = np.concatenate([h[-2 * w:], h, h[:2 * w]])
    return np.convolve(np.convolve(pad, k, 'same'), k, 'same')[2 * w:-2 * w]
Y = rounded(raw, 3.0)
station = (s >= total - 19.5) | (s <= 0.1)
assert np.abs(Y[station] - DECK_H).max() < 1.0, np.abs(Y[station] - DECK_H).max()
Y[station] = DECK_H
grade = np.diff(np.append(Y, Y[0])) / (STEP * 512)

# ---- banking: the road leans into its bends (the height across it, per unit to the left of the way the lap runs)
T = np.stack([np.roll(X, -1) - np.roll(X, 1), np.roll(Z, -1) - np.roll(Z, 1)], 1)
T /= np.linalg.norm(T, axis=1)[:, None]
Nn = np.stack([-T[:, 1], T[:, 0]], 1)
Tn = np.roll(T, -1, axis=0); Tp = np.roll(T, 1, axis=0)
kappa = (Tp[:, 0] * Tn[:, 1] - Tp[:, 1] * Tn[:, 0]) / (2 * STEP)          # 1 / radius in cells, positive turning to the left (towards Nn)
# (less on the helix: its inside edge passes close to the tower, and under the lamps on its corners)
BANK_GAIN, BANK_MAX, BANK_HELIX = float(os.environ.get('ELEV_BANK', '2.2')), 0.45, 0.25
bank = -np.clip(BANK_GAIN * rounded(kappa, 2.5), -BANK_MAX, BANK_MAX)
bank[s < s_crest] = np.clip(bank[s < s_crest], -BANK_HELIX, BANK_HELIX)
# (the grid and the start line stand level, and so does the first corner out of the station: its inside edge is over the main deck)
flat = (s >= total - 18.0) | (s <= s_at(RING_E, RING_S - RING_R) + 1.0)
bank[flat] = 0
bank = rounded(bank, 1.5)
bank[(s >= total - 17.5) | (s <= 1.0)] = 0

print(f'lap {total:.1f} cells, {N} points; the helix {TURNS} turns and a quarter of {ring_lap:.1f} cells at {GRADE_HELIX * 100:.1f} %, '
      f'{PITCH:.0f} between its levels; top {H_TOP:.0f}; steepest down {-grade.min() * 100:.0f} %, up {grade.max() * 100:.0f} %; '
      f'lowest {Y.min():.0f}; banking up to {np.abs(bank).max():.2f}')

# ---- checks
# 1. the cube's edge: the engine sends Twinsen to the open sea half a cell from it
edge = np.minimum.reduce([X, Z, 64 - X, 64 - Z])
print(f'nearest the cube edge: {edge.min():.1f} cells (centre line)')
# 2. the lap against itself: where two parts lie over each other in plan, the height between them
# (a point of one part's cross-section, rail to rail, inside the other part's band)
idx = np.arange(N)
worst = (1e9, 0, 0); pairs = 0
across = np.linspace(-DECK, DECK, 7)
for i in range(N):
    d = np.hypot(X - X[i], Z - Z[i])
    sep = np.abs(idx - i); sep = np.minimum(sep, N - sep)
    for j in np.nonzero((d < 2 * DECK + 0.5) & (sep > 40))[0]:
        qx = X[j] + Nn[j, 0] * across - X[i]; qz = Z[j] + Nn[j, 1] * across - Z[i]
        inside = (np.abs(qx * T[i, 0] + qz * T[i, 1]) <= STEP) & (np.abs(qx * Nn[i, 0] + qz * Nn[i, 1]) <= DECK)
        if not inside.any(): continue
        dy = abs(Y[i] - Y[j]); pairs += 1
        if dy < worst[0]: worst = (dy, i, j)
print(f'parts of the lap over each other in plan ({pairs} pairs): least height between them {worst[0]:.0f} '
      f'(points {worst[1]} and {worst[2]}: cells ({X[worst[1]]:.1f},{Z[worst[1]]:.1f}) at {Y[worst[1]]:.0f} and ({X[worst[2]]:.1f},{Z[worst[2]]:.1f}) at {Y[worst[2]]:.0f})')
# 3. the platform's bodies: nothing within the road's space (its slab and a car's height over it), the banked surface counted
pts, body = mesh.cloud()
px, py, pz = pts[:, 0] / 512, pts[:, 1], pts[:, 2] / 512
hits = {}
for k in range(N):
    near = (np.abs(px - X[k]) < DECK + 1.2) & (np.abs(pz - Z[k]) < DECK + 1.2)
    if not near.any(): continue
    q = np.nonzero(near)[0]
    along = (px[q] - X[k]) * T[k, 0] + (pz[q] - Z[k]) * T[k, 1]; lat = (px[q] - X[k]) * Nn[k, 0] + (pz[q] - Z[k]) * Nn[k, 1]
    inside = (np.abs(along) <= STEP) & (np.abs(lat) <= DECK + 0.1)
    dy = py[q][inside] - (Y[k] + lat[inside] * 512 * bank[k])
    bad = (dy > -260) & (dy < 1300)
    for b in np.unique(body[q][inside][bad]): hits.setdefault(int(b), []).append(k)
for b, ks_ in sorted(hits.items()):
    print(f'  body {b} in the road\'s space at points {min(ks_)}..{max(ks_)} ({len(ks_)} points), e.g. cell ({X[ks_[0]]:.1f},{Z[ks_[0]]:.1f}) road at {Y[ks_[0]]:.0f}')

START = 0
# the platform's own structures stay (the decks, the tower, the crane house, the airship and its gangway, the fences): only the crates
# and barrels in the road's way go (bodies 17, 20, 21, 22)
KEEP = [b for b in range(36) if b not in (17, 20, 21, 22)]
plan = {
    'originCellX': ORIGIN, 'originCellZ': ORIGIN,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in zip(X, Z)],
    'heights': [round(float(y), 1) for y in Y],
    'bank': [round(float(b), 4) for b in bank],
    'maxGrade': 0.16,
    'asphaltHalf': ASPHALT, 'curbHalf': CURB, 'vergeHalf': DECK + 0.75, 'blend': 3.0,
    'raised': [0, N - 1], 'raisedHalf': DECK,
    'start': START,
    # where the other cars wait while Twinsen qualifies: on the landing east of the station (its deck is at 3270), facing the road
    'pitSpots': [[43.6, 44.3, -1.0, 0.0, 3270], [41.2, 46.9, -1.0, 0.0, 3270], [43.6, 46.9, -1.0, 0.0, 3270], [41.2, 49.4, -1.0, 0.0, 3270], [43.6, 49.4, -1.0, 0.0, 3270]],
    'keepBodies': KEEP,
    # the decks a pier may stand on: the main deck, the two lower ones and the landing between them
    'pierBodies': [1, 9, 14, 15, 19],
    'gravity': float(os.environ.get('ELEV_GRAVITY', '1.0')),
    # the camera rides the road: cells behind the car, units over the road, cells ahead of the car that it looks at
    'railCamera': [float(v) for v in os.environ.get('ELEV_CAMERA', '9 3400 4').split()],
}
json.dump(plan, open(OUT, 'w'))
np.savez(D + 'lap.npz', X=X, Z=Z, Y=Y, T=T, Nn=Nn, bank=bank, s=s)
print('wrote', OUT)

if '--pictures' in sys.argv:
    # the raised road and its piers as triangles, over the platform
    tris = []; kinds = []
    for k in range(N):
        a, b = k, (k + 1) % N
        def edge_point(i, side):
            return np.array([(X[i] + Nn[i, 0] * DECK * side) * 512, Y[i] + side * DECK * 512 * bank[i], (Z[i] + Nn[i, 1] * DECK * side) * 512])
        l0, r0, l1, r1 = edge_point(a, -1), edge_point(a, 1), edge_point(b, -1), edge_point(b, 1)
        tris += [np.array([l0, l1, r1]), np.array([l0, r1, r0])]; kinds += [100 if s[k] < s_crest else 101] * 2
        if k % 20 == 10:
            x0, z0 = X[a] * 512, Z[a] * 512; w = 230
            for (ax, az, bx, bz) in ((-w, -w, w, -w), (w, -w, w, w), (w, w, -w, w), (-w, w, -w, -w)):
                p0 = [x0 + ax, 0, z0 + az]; p1 = [x0 + bx, 0, z0 + bz]; p2 = [x0 + bx, Y[a] - 200, z0 + bz]; p3 = [x0 + ax, Y[a] - 200, z0 + az]
                tris.append(np.array([p0, p1, p2])); tris.append(np.array([p0, p2, p3])); kinds += [102, 102]
    extra = (np.array(tris), np.array(kinds))
    for v in ('west', 'south', 'north', 'east', 'top'): mesh.render(v, D + f'design_{v}.png', scale=0.03, extra=extra, ymax=16000)
    # the profile
    from PIL import Image, ImageDraw
    W, Hh = 1400, 360
    im = Image.new('RGB', (W, Hh), (250, 250, 250)); dr = ImageDraw.Draw(im)
    for yv in range(0, 16001, 2000):
        yy = Hh - 20 - yv / 16000 * (Hh - 40); dr.line([(40, yy), (W - 10, yy)], fill=(225, 225, 225)); dr.text((4, yy - 6), str(yv), fill=(90, 90, 90))
    pts2 = [(40 + sv / total * (W - 50), Hh - 20 - yv / 16000 * (Hh - 40)) for sv, yv in zip(s, Y)]
    dr.line(pts2, fill=(200, 40, 40), width=2)
    for name, sv in (('top', s_top), ('drop', s_crest), ('north edge', s_north), ('turn-round', s_nw0), ('under the drop', s_under), ('airship', z_on_east(28)),
                     ('south edge', s_se1), ('station', total - 19.5)):
        xx = 40 + sv / total * (W - 50); dr.line([(xx, 20), (xx, Hh - 20)], fill=(170, 170, 210)); dr.text((xx + 3, 22), name, fill=(60, 60, 120))
    im.save(D + 'design_profile.png')
    print('pictures written')
