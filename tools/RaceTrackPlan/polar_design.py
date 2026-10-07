# Polar Island's dream race (2026-10-05; on the island at twice LBA1's size since 2026-10-06), from the user's drawing: Twinsen races
# FunFrock to Sendell, from the dock to the rocky peak. Run in E:\dump\TEMP\pbig with heights.csv (ScriptRoundTrip islandheights POLAR,
# LBA2_DIR a folder with the island) and columns.csv (ScriptRoundTrip polarcolumns) there; writes docs/racetrack/polar_track_plan.json.
#
# The route is a sprint, not a lap (the plan's `open`): from the start line on the dock north up the arm and the main straight through
# 107 into 109, round a hairpin at its top, back down 107's east side and west along 107's south strip, over a jump across the main
# straight where it meets the arm, on west into 108, and up its terraces along LBA1's own car tracks -- north, a jog east and north again,
# then back and forth west: south, north, south (a jog west on the way) and north -- and east along 108's top towards the rocky peak.
# There it leaves the ground on a raised road round the peak -- its north side over the sea, its east side, its south side over the lake --
# and turns in to a ramp straight at the peak's south face: the jump into it, where the race ends (`finish`, the ramp's lip) and Twinsen
# wakes up before he lands (the engine's wake_flight=). At LBA1's size 108's car tracks were six to eight cells apart, too close for an
# eight-cell road (it took three of their five ways up); twice the size, twelve to sixteen.
# Both jumps are carried jumps (the race-track mode carries the car along the plan's heights: RaceTrackPlan.ArcJumps), each on a raised
# stretch of its own (`raised`: two stretches, the ground road between them): the one over the main straight flies from cube (8, 7) into
# cube (7, 7); the one into the peak has no landing anyone reaches (its landing a point inside the peak, the route's end).
import json, csv, sys
import numpy as np

Hgt = {}
for row in csv.DictReader(open('heights.csv')):
    if row['height']: Hgt[(int(row['x']), int(row['z']))] = float(row['height'])


def ground(x, z):
    xi, zi = int(np.floor(x)), int(np.floor(z))
    fx, fz = x - xi, z - zi
    h = [Hgt.get((xi + a, zi + b), 0.0) for b in (0, 1) for a in (0, 1)]
    return (h[0] * (1 - fx) + h[1] * fx) * (1 - fz) + (h[2] * (1 - fx) + h[3] * fx) * fz


SP = 0.5               # cells between points
# control points, island cells, in race order (LBA1's car tracks at twice its size: a cell of LBA1's at 2 * x - 502, 2 * z - 420 of
# the island as it was at LBA1's size)
# (LBA1's grids are whole cubes here, and its tracks up the arm run along a cube's edge, x 512: the road ten cells east of them there --
# along an edge the car would change scene back and forth -- and kept off the edges elsewhere)
CTRL = [
    # the dock: the start straight, north
    (522, 692), (522, 676), (522, 656), (522, 632), (522, 604), (522, 576), (522, 548), (522, 522),
    # through the junction (the jump flies over here) and north along 107's lake shore
    (522, 496), (521.5, 472), (521, 448), (521, 424), (520, 400), (516, 376),
    # into 109 and round the hairpin at its top
    (508, 356), (496, 336), (480, 322), (466, 310), (458, 296), (456, 282), (462, 272), (476, 268), (496, 267), (514, 270),
    (529, 280), (538, 296), (543, 314), (546, 336),
    # down 107's east side
    (551, 360), (558, 384), (564, 408), (568, 432), (569, 454), (566, 472), (559, 485),
    # west along the south strip: the run-up, the jump over the main straight, the landing, and on west along LBA1's tracks
    (546, 492.5), (530, 492.5), (514, 492.5), (498, 492.5), (482, 492.5), (466, 492.5), (450, 492.5), (434, 492.5), (418, 492.5),
    (404, 492), (395, 488),
    # 108: LBA1's tracks up its terraces -- north (x 390.5), a jog east and north (x 402.5)
    (391, 480), (390.5, 468), (390.5, 456), (391.5, 448), (396, 444.5), (401, 441), (402.5, 434), (402.5, 425),
    # west along z 414.5, south (x 378.5)
    (401, 418), (396, 415), (389, 414.5), (382, 416), (379, 421), (378.5, 430), (378.5, 444), (378.5, 458), (378.5, 466),
    # round to the west, north (x 366.5): LBA1's turn is a square one twelve cells across, the road's a loop a little wider
    (379.5, 472), (378.5, 479), (375.5, 482.5), (372.5, 483.5), (369.5, 482.5), (366.5, 479), (365.5, 472), (366.5, 466), (366.5, 456), (366.5, 440), (366.5, 424), (366.5, 412),
    # round to the west, south (x 352.5), a jog west (z 432.5), south (x 342.5)
    (365.8, 409), (362.5, 405.8), (356.5, 405.8), (353.2, 409), (352.5, 415), (352.5, 421), (352, 426), (349, 431), (345, 433.5), (342.5, 438),
    (342.5, 448), (342.5, 458),
    # round to the west, north (x 326.5)
    (341.5, 464), (337.5, 468), (331.5, 468.5), (327, 465), (326.5, 456), (326.5, 440), (326.5, 424), (326.5, 408), (327, 398),
    # east along 108's top, along its north edge as LBA1's tracks run (a little inside it: past it is a cube's edge)
    (330, 394), (336, 392.5), (348, 392), (362, 392), (376, 392), (390, 392), (400, 391.5),
    # off its edge onto the raised road round the rocky peak: its north side over the sea, its east side, its south side over the lake
    (408, 388), (415, 379), (423, 369), (434, 361), (447, 357), (460, 356), (472, 358), (482, 363), (490, 372), (494, 384),
    (496, 397), (496, 410), (496, 424), (496, 438), (496, 452),
    # and in: round the south side over the lake to the north (a half circle 20 cells round), a straight to gather speed, the ramp at the
    # peak's south face, and the flight into it (eight cells off the cubes' edge at x 448)
    (490.1, 466.1), (476, 472), (461.9, 466.1), (456, 452), (456, 444), (456, 436), (456, 428), (456, 420), (456, 414), (456, 410),
    (456, 406), (456, 402),
]

# the jump over the main straight (along z 492.5, the straight at x 514, its road 8 cells either side): the ramp's foot and lip, the
# landing lip and the landing hill's foot, and the flight's top (over the straight: a car on it is about 1200 high)
CROSS = [(541, 492.5), (531, 492.5), (507, 492.5), (502, 492.5)]
CROSS_APEX = 5400
# the jump into the peak: the ramp's foot and lip (the finish line), the landing (inside the peak: never reached) and the route's end
ARC = [(456, 440), (456, 432), (456, 408), (456, 402)]
START = (522, 672)
MAXGRADE = 0.14
RAMP_UP = 1000
# the raised road round the peak: from where the road leaves 108's top, climbing gently to the ramp's foot (a car on the ground under it
# never meets it: the peak's lake, its shore, the sea)
LOOP_FROM, LOOP_TOP = (396, 391.7), (390, 392)
LOOP_RISE = 900
ARC_APEX_OVER = 1600          # the flight's top over the lip


def catmull(pts, sp):
    P = np.array(pts, float)
    P = np.vstack([P[0] * 2 - P[1], P, P[-1] * 2 - P[-2]])
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        seg = np.linalg.norm(p2 - p1)
        n = max(2, int(np.ceil(seg / sp * 4)))
        for k in range(n):
            t = k / n
            out.append(0.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(P[-2])
    out = np.array(out)
    # resampled every sp along the curve
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(out, axis=0), axis=1))]
    s = np.arange(0, d[-1], sp)
    return np.stack([np.interp(s, d, out[:, 0]), np.interp(s, d, out[:, 1])], 1), s


pts, S = catmull(CTRL, SP)
n = len(pts)


def nearest(p, lo=0):
    return lo + int(np.argmin(np.hypot(pts[lo:, 0] - p[0], pts[lo:, 1] - p[1])))


# (the return leg's points: after the hairpin at 109's top, so the main straight's own points under the jump are never taken)
leg = nearest((559, 485))
cross = [nearest(p, leg) for p in CROSS]
loop = nearest(LOOP_FROM, cross[-1])
arc = [nearest(p, loop) for p in ARC]
arc[3] = n - 1
fin, start = arc[1], nearest(START)
print('points', n, 'length %.0f cells' % S[-1], 'start', start, 'jump over the straight', cross, 'the raised road round the peak from', loop,
      'jump into the peak', arc, 'finish', fin)

# heights: the ground under the line (the highest of a small cross round each point: the line runs over terraces' edges), smoothed
raw = np.array([max(ground(x + dx, z + dz) for dx, dz in ((0, 0), (1.5, 0), (-1.5, 0), (0, 1.5), (0, -1.5))) for x, z in pts])
raw = np.maximum(raw, 1200)                # (over the sea: a causeway at least this high)


def smooth(v, cells):
    w = int(cells / SP)
    k = np.exp(-0.5 * (np.arange(-3 * w, 3 * w + 1) / w) ** 2); k /= k.sum()
    pad = np.r_[np.full(3 * w, v[0]), v, np.full(3 * w, v[-1])]
    return np.convolve(pad, k, 'valid')


h = smooth(raw, 6)


# A carried jump's way: from the ramp's foot (on the road as it is there) up RAMP_UP, curving, to the lip; a flight to the landing lip,
# `after` + 250, its top `apex`; a hill curving down onto `after` at the landing hill's foot. Returns the bump the flight was given.
def carried(foot, lip, land, landFoot, after, apex):
    base = h[foot]
    for i in range(foot, lip + 1):
        t = (i - foot) / max(1, lip - foot); h[i] = base + RAMP_UP * t * t
    L = base + RAMP_UP
    for i in range(lip, land + 1):
        t = (i - lip) / max(1, land - lip); h[i] = L + (after + 250 - L) * t
    ts = np.linspace(0, 1, 201)
    bump = next(b for b in np.arange(0, 20000, 50) if (L + (after + 250 - L) * ts + b * 4 * ts * (1 - ts)).max() >= apex)
    for i in range(lip, land + 1):
        t = (i - lip) / max(1, land - lip); h[i] += bump * 4 * t * (1 - t)
    for i in range(land, landFoot + 1):
        t = (i - land) / max(1, landFoot - land); h[i] = after + 250 * (1 - t) ** 2
    return bump


# the jump over the straight: from the road's own height to the road's own height beyond it
crossBump = carried(*cross, h[cross[3]], CROSS_APEX)
# the raised road round the peak: from 108's top, where it leaves it, rising LOOP_RISE (eased in and out) to the ramp's foot -- the ground
# under it the sea, the lake, its shore, far below
base = h[nearest(LOOP_TOP)]
for i in range(loop, arc[0] + 1):
    t = (i - loop) / max(1, arc[0] - loop); h[i] = base + LOOP_RISE * t * t * (3 - 2 * t)
# the jump into the peak: its landing as high as the lip (inside the peak: never reached)
lipH = h[arc[0]] + RAMP_UP
arcBump = carried(*arc, lipH - 250, lipH + ARC_APEX_OVER)
# grade limits outside the jumps and the raised road (the road climbs 108's terraces as a ramp, not a cliff): each point no further from
# its neighbour than the grade allows, sweeping both ways; the planned stretches held
held = np.zeros(n, bool); held[cross[0]:cross[3] + 1] = True; held[loop:] = True
step = MAXGRADE * SP * 512
for _ in range(4):
    for i in range(1, n):
        if not held[i]: h[i] = np.clip(h[i], h[i - 1] - step, h[i - 1] + step)
    for i in range(n - 2, -1, -1):
        if not held[i]: h[i] = np.clip(h[i], h[i + 1] - step, h[i + 1] + step)
h2 = smooth(h, 2)
h = np.where(held, h, h2)

# checks: grades, how close the route comes to itself, the lowest over the ground, the tightest turns
grade = np.abs(np.diff(h)) / (SP * 512)
print('steepest grade outside the jumps %.3f' % max(g for i, g in enumerate(grade) if not held[i] and not held[i + 1]))
mind = 1e9; where = None
flight = set(range(cross[1], cross[2] + 1))
for i in range(0, n, 2):
    d = np.hypot(pts[:, 0] - pts[i, 0], pts[:, 1] - pts[i, 1])
    far = np.abs(S - S[i]) > 30
    j = np.argmin(np.where(far, d, 1e9))
    if d[j] < mind and i not in flight and j not in flight:
        mind, where = d[j], (i, j)
print('closest approach of two parts of the route (outside the flight over the straight) %.1f cells at %s and %s'
      % (mind, tuple(np.round(pts[where[0]], 1)), tuple(np.round(pts[where[1]], 1))))
under = [i for i in range(cross[0]) if np.hypot(pts[i, 0] - 522, pts[i, 1] - 492.5) < 1]
print('the flight over the straight: %.0f at its top, the straight under it at %.0f' % (h[cross[1]:cross[2] + 1].max(), h[under[0]] if under else -1))
diff = h - np.array([ground(x, z) for x, z in pts])
print('road over the ground: from %.0f to %.0f (outside the jumps and the raised road)' % (diff[~held].min(), diff[~held].max()))
print('the raised road round the peak: %.0f to %.0f, the lip %.0f, the flight\'s top %.0f; the ground under it at most %.0f'
      % (h[loop], h[arc[0]], h[arc[1]], h[arc[1]:arc[2] + 1].max(), max(ground(x, z) for x, z in pts[loop:arc[1]])))
# (turn radius over 4 cells of road)
k = 8
rad = []
for i in range(k, n - k):
    a, b, c = pts[i - k], pts[i], pts[i + k]
    ab, bc, ca = np.linalg.norm(b - a), np.linalg.norm(c - b), np.linalg.norm(a - c)
    area = abs((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])) / 2
    rad.append((ab * bc * ca / (4 * area) if area > 1e-6 else 1e9, i))
rad.sort()
shown = []
for r, i in rad:
    if len(shown) == 8: break
    if all(np.hypot(*(pts[i] - pts[j])) > 10 for _, j in shown): shown.append((r, i))
print('tightest turns (radius in cells at point):', ', '.join('%.1f at %s' % (r, tuple(np.round(pts[i], 1))) for r, i in shown))

plan = {
    'originCellX': 0, 'originCellZ': 0,
    'points': [[round(float(x), 3), round(float(z), 3)] for x, z in pts],
    'heights': [round(float(v), 1) for v in h],
    'open': True, 'start': start, 'finish': fin,
    'maxGrade': 1.8,
    'asphaltHalf': 3.0, 'curbHalf': 4.0, 'vergeHalf': 5.0, 'blend': 4.0,
    'raised': [cross[0], cross[3], loop, n - 1], 'raisedHalf': 3.25,
    # (both jumps with the camera behind the car, as on the road: 0)
    'arcJumps': [cross + [0], arc + [0]],
    # (the ground made the ramps' own surface under their ends; and the rocky peak and its plateau, decors standing that high, kept where
    # the jump flies into them)
    'raisedCut': True, 'keepAbove': 15000,
}
out = sys.argv[1] if len(sys.argv) > 1 else r'E:\dump\LBAAssembler\docs\racetrack\polar_track_plan.json'
json.dump(plan, open(out, 'w'))
json.dump([[round(float(x), 2), round(float(z), 2)] for x, z in pts], open('route.json', 'w'))
print('wrote', out, 'flight bumps', crossBump, arcBump)
