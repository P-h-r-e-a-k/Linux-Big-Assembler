# How a car setup DRIVES on each track, and each track's recommended setup chosen by it (2026-10-17). The user: "the best setup is not the
# one that gives the fastest time, the recommended setup for the Temple track means a slight side arrow tap takes us from one side of the
# narrow track to the other at the recommended speed. Reducing the speed to 80 and setting steering to 150 feels better to drive and allows
# us to more easily stay in the centre of the track."
#
#   python drive.py tap <key> <top km/h> <steer %> [tag] [tick]   a steering tap in the engine (headless, on a straight the test pilot
#                                       reaches by `tick`): the heading it turns and the speed across it leaves, against the model below
#   python drive.py points [keys]       each track's line planned point by point for every setup of the grid (raceplan file ... <points>)
#   python drive.py table [keys]        the tap's move of each setup, the fastest setup within the limit: drive.json
#   python drive.py report [keys]       the old setups and the new as the app has them (whole km/h, %), planned and measured again: the docs' table
#   python drive.py verify [keys]       the test pilot's time trials with the old setup and the new, and a race with the new
# (stress.py's folder and engine: STRESS_ROOT, STRESS_ENGINE, STRESS_SR; `stress.py files` first, for each track's race files. The results:
# docs/LBA2_RACE_TRACK_STRESS_TEST.md, "Easy to drive, not only fast")
#
# (the grid's percentages are of the stress test's base car, stress.py: five gears to 45/57/63/97/110 km/h, accel 300 %, brake 300 %, steer
# 200 %; the setups' own numbers -- OLD, the table -- are in the Race car window's terms, km/h and percentages of the original buggy)
#
# The measure. The buggy turns at a fixed rate while a steering key is held -- `steer` units of 4096 a second (BUGGY.CPP MoveBuggy: Beta +=
# SpeedRot * dt; over the top gear's speed SpeedRot grows with the speed), at once, and stays turned when the key is let go: nothing
# straightens it. So a tap of TAP seconds turns the car by w*TAP and leaves it crossing the road at v*sin(w*TAP) until the driver taps it
# back REACT seconds later. The tap's move is the way across from the first tap to the end of the second:
#     2*v*(1 - cos(w*TAP))/w + v*sin(w*TAP)*REACT        (v the car's speed, w the turn rate)
# nearly v*w*TAP*(TAP + REACT): the car's speed times its turn rate (the sideways pull of a full-lock bend). It is measured at every point of
# the racing line at the speed the planner drives there (raceplan, the setup's own), against the road's room there: its usable half width,
# the half width the car's middle can move in -- a raised road's rail to its middle less RAISED_INSET (640, the engine's own, a car's half
# width; of the pieces of road a point is on, the widest), a road on the ground its kerb's half width less the car's (633). A track's figure
# is its worst WINDOW seconds of driving (the share of the room the tap takes, averaged over that time): one narrow gate doesn't decide it, a
# narrow straight does. A jump the engine carries the car over is not steered.
#
# The limit is the user's own: the Temple's figure at 80 km/h with steering 150 %. Each track's setup is the fastest planned lap within it
# (the Temple's: the user's top speed and steering), no lower than the grid's 70 % (77 km/h, steering 140 %): where even that is over the
# limit (the narrowest tracks), the least twitchy of the grid.
import json, math, os, re, subprocess, sys, concurrent.futures as cf

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stress
from stress import ROOT, GAME, ENG, TRACKS

TAP, REACT, WINDOW, ROOM_SPAN = 0.1, 0.25, 2.0, 3.0
# (DRIVE_TAP, DRIVE_REACT, DRIVE_WINDOW: other values, to see how much the choice depends on them; DRIVE_OUT: the table's file then)
TAP = float(os.environ.get('DRIVE_TAP', TAP)); REACT = float(os.environ.get('DRIVE_REACT', REACT)); WINDOW = float(os.environ.get('DRIVE_WINDOW', WINDOW))
OUT = os.environ.get('DRIVE_OUT', 'drive')
CAR_HALF, RAISED_INSET = 633, 640
BASE_TOP, BASE_ACCEL, BASE_STEER = 110, 300, 200       # the base car in the Race car window's terms (km/h, %, %)
PLANS = os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../docs/racetrack')     # (the plans' kerb widths)
PLAN_FILE = {'desert': 'track_plan.json', 'storm': 'citadel_storm_track_plan.json', 'citabau': 'citadel_track_plan.json',
             'mosquibe': 'mosquibe_track_plan.json', 'celebration': 'celebration_track_plan.json', 'lava': 'celebrat_track_plan.json',
             'elevator': 'elevator_track_plan.json', 'sendell': 'sendell_track_plan.json', 'moon': 'moon_track_plan.json',
             'polar': 'polar_track_plan.json', 'francos': 'knartas_track_plan.json', 'otringal': 'otringal_track_plan.json',
             'volcano': 'volcano_track_plan.json', 'emerald': 'emerald_track_plan.json', 'wannies': 'wannies_track_plan.json',
             'palace': 'palace_track_plan.json', 'cx': 'cxtower_track_plan.json', 'temple': 'temple_track_plan.json'}

# The setups of 2026-10-16 (RaceCarSetup.TrackSetups before this): top km/h, accel %, steer %
OLD = {'desert': (110, 210, 260), 'storm': (110, 210, 260), 'citabau': (110, 210, 260), 'mosquibe': (116, 210, 250),
       'celebration': (104, 225, 260), 'lava': (104, 225, 260), 'elevator': (110, 210, 260), 'sendell': (94, 255, 260),
       'moon': (94, 255, 260), 'polar': (143, 210, 200), 'francos': (116, 210, 250), 'otringal': (110, 210, 260),
       'volcano': (116, 210, 250), 'emerald': (110, 210, 260), 'wannies': (116, 210, 250), 'palace': (126, 210, 230),
       'cx': (99, 240, 260), 'temple': (132, 210, 220)}
# The user's verdict on the Temple (2026-10-17): the limit's calibration
USER = ('temple', 80, 150)

def pct(top_kmh=BASE_TOP, accel=BASE_ACCEL, steer=BASE_STEER):
    return (top_kmh * 100.0 / BASE_TOP, accel * 100.0 / BASE_ACCEL, 100.0, steer * 100.0 / BASE_STEER)

def tap_move(v, w):
    """How far across (v's units) a tap of TAP s and the tap back REACT s later move a car going v, turning w rad/s."""
    if w <= 0 or v <= 0: return 0.0
    a = w * TAP
    return 2 * v * (1 - math.cos(a)) / w + v * math.sin(a) * REACT

def kerb_half(key):
    p = json.load(open(os.path.join(PLANS, PLAN_FILE[key]), encoding='utf-8'))
    k = {a.lower(): b for a, b in p.items()}
    return k.get('curbhalf') or 4.5

# ---- the grid: top speed and steering 70-130 % of the base car's in 2.5 % steps, acceleration 70-130 % in 5s; all three summing to no more
# than 300 (the stress test's budget: a setup spends it, and may now leave some unspent where the tap's limit won't let top speed or
# steering have it) -- and the user's own setup on the Temple
def grid():
    steps = [70 + 2.5 * i for i in range(25)]
    out = []
    for t in steps:
        for s in steps:
            for a in range(70, 131, 5):
                if t + a + s <= 300 + 1e-6: out.append((t, a, 100, s))
    return out

def extra_setups(key):
    x = [pct(*OLD[key])]
    if key == USER[0]:
        for a in range(70, 131, 5): x.append((USER[1] * 100.0 / BASE_TOP, a, 100, USER[2] * 100.0 / BASE_STEER))
    return x

def fmt(s): return ' '.join(f'{v:g}' for v in s)
# a setup's key: its top speed and steering (the measure's), to two places (the engine prints six figures)
def pair(s): return f'{float(s[0]):.2f} {float(s[3]):.2f}'
def K(s): return tuple(round(float(x), 2) for x in s)

def points_one(key):
    d = f'{ROOT}/{key}'
    dd = f'{d}/drive'
    os.makedirs(dd, exist_ok=True)
    # the points only for top speed x steering, at the grid's hardest pull (130 %): the tap's move hardly depends on it -- a harder pull is a
    # little quicker between the bends, so a pair within the limit at it is within at any; the laps for the whole grid
    pairs = sorted({(t, s) for t, a, b, s in grid() + extra_setups(key)})
    open(dd + '/pairs.txt', 'w').write('\n'.join(f'{t:g} 130 100 {s:g}' for t, s in pairs) + '\n')
    open(dd + '/grid.txt', 'w').write('\n'.join(fmt(s) for s in grid() + extra_setups(key)) + '\n')
    code, run = stress.engine(key, ['--exec-at', '40', f'raceplan file {dd}/pairs.txt {dd}/pairs_lap.txt {dd}/points.txt',
                                    '--exec-at', '42', f'raceplan file {dd}/grid.txt {dd}/laps.txt'], 'drive/plan', 60)
    # reduced at once (the points file is large): the measure for each pair
    meas = reduce_points(key, f'{dd}/points.txt')
    json.dump(meas, open(f'{dd}/{OUT}_measure.json', 'w'))
    os.remove(f'{dd}/points.txt')
    return key, code, len(meas)

def read_blocks(path):
    """Each setup's plan from a points file: (setup, top units, steer units, rows)."""
    setup = top = steer = None
    rows = []
    for line in open(path):
        if line.startswith('# setup'):
            if setup is not None: yield setup, top, steer, rows
            setup = tuple(float(x) for x in line.split()[2:6]); rows = []
        elif line.startswith('# top'):
            p = line.split(); top, steer = int(p[2]), int(p[4])
        elif line and not line.startswith('#'):
            rows.append([float(x) for x in line.split()])
    if setup is not None: yield setup, top, steer, rows

def carried(key, rows):
    """The line's points the engine carries the car over (arcjump=: from the take-off ramp's foot to the landing hill's foot, places in the
    raised road's file): no steering there."""
    race = open(f'{ROOT}/{key}/race.txt', encoding='utf-8').read()
    jumps = [list(map(int, m.split()[:4])) for m in re.findall(r'^arcjump=(.*)$', race, re.M)]
    m = re.search(r'^raised=(.*)$', race, re.M)
    if not jumps or not m: return set()
    road = [list(map(int, l.split()[:3])) for l in open(m.group(1).strip()) if len(l.split()) >= 4]
    def nearest(p):
        return min(range(len(rows)), key=lambda i: (rows[i][0] - p[0]) ** 2 + (rows[i][1] - p[1]) ** 2 + 4 * (rows[i][2] - p[2]) ** 2)
    out = set()
    for foot, lip, land, landfoot in jumps:
        if max(foot, landfoot) >= len(road): continue
        a, b = nearest(road[foot]), nearest(road[landfoot])
        if a <= b: out.update(range(a, b + 1))
        else: out.update(range(a, len(rows))); out.update(range(0, b + 1))
    return out

def road_rooms(key, rows, kerb):
    """The room at each of the line's points: on a raised road the widest of its pieces there (at a junction the lap's road and a branch's
    tapering end overlap, and the engine's nearest piece -- the line points' own `half` -- can be the branch's, a cell wide), its half width
    less RAISED_INSET; on the ground the plan's kerb less the car's half width."""
    race = open(f'{ROOT}/{key}/race.txt', encoding='utf-8').read()
    m = re.search(r'^raised=(.*)$', race, re.M)
    ground = kerb - CAR_HALF / 512
    if not m: return [ground] * len(rows)
    road = [list(map(int, l.split()[:4])) for l in open(m.group(1).strip()) if len(l.split()) >= 4]
    B = 2048
    buckets = {}
    for i in range(len(road) - 1):
        a, c = road[i], road[i + 1]
        if a[3] <= 0 or c[3] <= 0 or (c[0] - a[0]) ** 2 + (c[1] - a[1]) ** 2 > (4 * 512) ** 2: continue   # (a gap, or a piece's end to the next's start)
        reach = max(a[3], c[3]) + 96
        for bx in range((min(a[0], c[0]) - reach) // B, (max(a[0], c[0]) + reach) // B + 1):
            for bz in range((min(a[1], c[1]) - reach) // B, (max(a[1], c[1]) + reach) // B + 1):
                buckets.setdefault((bx, bz), []).append(i)
    out = []
    for r in rows:
        x, z, y = r[0], r[1], r[2]
        best = 0
        for i in buckets.get((int(x) // B, int(z) // B), ()):
            a, c = road[i], road[i + 1]
            sx, sz = c[0] - a[0], c[1] - a[1]
            l2 = sx * sx + sz * sz
            t = 0 if l2 == 0 else max(0.0, min(1.0, ((x - a[0]) * sx + (z - a[1]) * sz) / l2))
            px, pz, py = a[0] + sx * t, a[1] + sz * t, a[2] + (c[2] - a[2]) * t
            half = a[3] + (c[3] - a[3]) * t
            if abs(py - y) > 700 or (x - px) ** 2 + (z - pz) ** 2 > (half + 96) ** 2: continue
            best = max(best, half)
        out.append((best - RAISED_INSET) / 512 if best > 0 else ground)
    return out

def measure(key, top, steer, rows, kerb, skip=(), rooms=None):
    """The tap's move along the line: per point (time, its share of the room), and the track's figures. `skip`: points carried (no steering);
    `rooms`: the room at each point (road_rooms)."""
    n = len(rows)
    if rooms is None: rooms = [(r[6] - RAISED_INSET) / 512 if r[6] > 0 else kerb - CAR_HALF / 512 for r in rows]
    # (the room as the road has it over a few cells -- the median within ROOM_SPAN cells either way: where a branch leaves or joins the lap,
    # or at a gap's lip, the engine's road at a single point can be a tapering piece's, a cell or two wide, or none)
    smooth = []
    lo = hi = 0
    for i in range(n):
        while rows[lo][5] < rows[i][5] - ROOM_SPAN * 512: lo += 1
        while hi + 1 < n and rows[hi + 1][5] <= rows[i][5] + ROOM_SPAN * 512: hi += 1
        near = sorted(rooms[lo:hi + 1])
        smooth.append(max(near[len(near) // 2], 0.5))
    out = []
    for i, r in enumerate(rows):
        x, z, y, v, R, cum, half, lat = r[:8]
        ds = (rows[i + 1][5] - cum) if i + 1 < n else (cum - rows[i - 1][5])
        v = max(v, 1.0)
        w = steer * max(1.0, v / top) * 2 * math.pi / 4096
        move = 0.0 if i in skip else tap_move(v / 512, w)
        out.append((ds / v, move, smooth[i], v))
    total = sum(o[0] for o in out)
    # the worst WINDOW seconds: the time-weighted mean of move/room over a window sliding along the lap (round the loop)
    share = [o[1] / o[2] for o in out]
    dt = [o[0] for o in out]
    worst, worst_at = 0.0, 0
    j, acc, accw = 0, 0.0, 0.0
    m = len(out)
    for i in range(m):
        while accw < WINDOW and j < i + m:
            acc += share[j % m] * dt[j % m]; accw += dt[j % m]; j += 1
        if accw > 0 and acc / accw > worst: worst, worst_at = acc / accw, i
        acc -= share[i] * dt[i]; accw -= dt[i]
    # the room, by time: typical (the median) and the narrowest tenth; and the tap's move at the car's fastest
    by_room = sorted(zip([o[2] for o in out], dt))
    def q(frac):
        c = 0
        for room, t in by_room:
            c += t
            if c >= frac * total: return room
        return by_room[-1][0]
    fastest = max(out, key=lambda o: o[3])
    return {'worst': worst, 'worst_at': worst_at, 'room_typ': q(0.5), 'room_narrow': q(0.1), 'room_min': by_room[0][0],
            'move_fast': fastest[1], 'v_fast': fastest[3], 'time': total}

def reduce_points(key, path):
    kerb = kerb_half(key)
    out = {}
    skip = rooms = None
    for setup, top, steer, rows in read_blocks(path):
        if skip is None: skip, rooms = carried(key, rows), road_rooms(key, rows, kerb)
        out[pair(setup)] = measure(key, top, steer, rows, kerb, skip, rooms)
    return out

def points(keys):
    with cf.ThreadPoolExecutor(int(os.environ.get('PAR', '6'))) as ex:
        for key, code, n in ex.map(points_one, keys): print(f'{key:12} exit {code} {n} setups measured', flush=True)

def laps(key):
    rows = [l.split() for l in open(f'{ROOT}/{key}/drive/laps.txt')]
    return {K(r[:4]): float(r[4]) for r in rows}

# (a whole km/h, as the window has it: down, so the setup stays within the limit it was measured at)
def kmh(top_pct): return math.floor(BASE_TOP * top_pct / 100 + 1e-6)

def table(keys):
    meas = {k: json.load(open(f'{ROOT}/{k}/drive/{OUT}_measure.json')) for k in keys}
    lp = {k: laps(k) for k in keys}
    # the limit: the share of the room the tap takes on the Temple in the user's setup
    u = pct(USER[1], BASE_ACCEL, USER[2])
    limit = meas[USER[0]][pair(u)]['worst']
    print(f'limit (the Temple at {USER[1]} km/h, steering {USER[2]} %): the tap moves the car {limit:.3f} of the room')
    out = {'limit': limit, 'tap': TAP, 'react': REACT, 'window': WINDOW, 'tracks': {}}
    for k in keys:
        m = meas[k]
        old = pct(*OLD[k])
        oldm = m[pair(old)]
        oldlap = lp[k].get(K(old))
        cand = [(lap, s) for s, lap in lp[k].items() if lap > 0 and pair(s) in m]
        within = [(lap, s) for lap, s in cand if m[pair(s)]['worst'] <= limit + 1e-9]
        if k == USER[0]:
            # (the user's own: top speed and steering as they set them, the fastest acceleration with them)
            within = [(lap, s) for lap, s in within if pair(s) == pair(u)]
        if within:
            lap, best = min(within)
            ok = True
        else:
            # (none within: the least twitchy of the grid, the fastest of those)
            least = min(m[pair(s)]['worst'] for lap, s in cand)
            lap, best = min((lap, s) for lap, s in cand if m[pair(s)]['worst'] <= least + 1e-9)
            ok = False
        newm = m[pair(best)]
        row = {'old': OLD[k], 'old_lap': oldlap, 'old_worst': oldm['worst'], 'old_move': oldm['move_fast'],
               'new': (kmh(best[0]), round(BASE_ACCEL * best[1] / 100), round(BASE_STEER * best[3] / 100)), 'new_pct': best,
               'new_lap': lap, 'new_worst': newm['worst'], 'new_move': newm['move_fast'], 'within': ok,
               'room_typ': newm['room_typ'], 'room_narrow': newm['room_narrow'], 'room_min': newm['room_min']}
        out['tracks'][k] = row
        print(f"{k:12} room {row['room_typ']:.2f}/{row['room_narrow']:.2f} cells | old {OLD[k]} lap {oldlap:.2f} worst {oldm['worst']:.2f} "
              f"| new {row['new']} lap {lap:.2f} worst {newm['worst']:.2f} {'' if ok else 'NONE WITHIN'}")
    json.dump(out, open(f'{ROOT}/{OUT}.json', 'w'), indent=1)
    return out

# ---- the tap in the engine: the test pilot drives to a straight, then lets go and the car is driven by `input` -- the throttle held, one tap
# of the steering key, the throttle held on -- with `cartrace 1` logging the car each frame (its place and heading)
def tap(key, top_kmh, steer, tag='tap', at=None, ticks=6):
    d = f'{ROOT}/{key}'
    base = open(d + '/trial.txt', encoding='utf-8').read() if os.path.exists(d + '/trial.txt') else open(d + '/base.txt', encoding='utf-8').read()
    p = pct(top_kmh, BASE_ACCEL, steer)
    car = f'car_{tag}.txt'
    open(f'{d}/{car}', 'w', encoding='utf-8').write(base + stress.car_keys(p[0], p[1], p[2], p[3]))
    t0 = at or 1500
    # (the trace from after the pilot lets go: its own small steering corrections before that are not the tap)
    args = ['--exec-at', '90', 'autodrive 1 5 0', '--exec-at', str(t0 + 20), 'cartrace 1',
            '--exec-at', str(t0), 'autodrive 0', '--exec-at', str(t0 + 1), 'input up 60',
            '--exec-at', str(t0 + 30), f'input up+left {ticks}', '--exec-at', str(t0 + 30 + ticks), 'input up 120',
            '--exec-at', str(t0 + 90), 'cartrace 0']
    code, run = stress.engine(key, args, f'drive/{tag}', t0 + 100, car)
    rows, seen = [], set()
    for l in open(run + '/run.log', encoding='utf-8', errors='replace'):
        m = re.search(r'\[cartrace\] t (\d+) x (-?\d+) y (-?\d+) z (-?\d+) .* speed (-?\d+) .* beta (\d+)', l)
        # (each line is logged twice, the engine's and the console's copy)
        if m and int(m.group(1)) not in seen:
            seen.add(int(m.group(1))); rows.append(tuple(int(x) for x in m.groups()))
    return code, run, rows

def tap_report(rows, top_kmh, steer, ticks=6, step_ms=16):
    """The tap as the engine drove it: the heading it turned against the model (steer units a second for as long as the key is held: `ticks`
    simulation steps of step_ms), whether it stays turned, and the speed across the way the car went before it, against v*sin(turn)."""
    rows = sorted(rows)
    turn = [i for i in range(1, len(rows)) if rows[i][5] != rows[i - 1][5]]
    if not turn: print('no turn traced'); return None
    a = b = turn[0]
    while b + 1 < len(rows) and rows[b + 1][5] != rows[b][5]: b += 1
    a -= 1
    d_beta = ((rows[b][5] - rows[a][5] + 2048) % 4096) - 2048
    steer_u = 1024 * steer / 100
    want = steer_u * ticks * step_ms / 1000
    after = [r[5] for r in rows[b:b + 25]]
    def vel(lo, hi):
        lo, hi = max(lo, 0), min(hi, len(rows) - 1)
        dt = (rows[hi][0] - rows[lo][0]) / 1000
        return ((rows[hi][1] - rows[lo][1]) / dt, (rows[hi][3] - rows[lo][3]) / dt) if dt > 0 else (0, 0)
    v0, v1 = vel(a - 10, a), vel(b + 1, b + 11)
    sp = math.hypot(*v0)
    across0 = 0.0
    across1 = (v1[0] * v0[1] - v1[1] * v0[0]) / sp if sp else 0
    model = math.hypot(*v1) * math.sin(abs(d_beta) * 2 * math.pi / 4096)
    print(f'speed {sp:.0f} units/s ({sp * 3.6 / 512:.1f} km/h); the key held {ticks} steps of {step_ms} ms: heading turned {d_beta} units '
          f'({d_beta * 360 / 4096:.1f} deg) over {b - a} frames; the model {want:.0f} units; held after it: {len(set(after)) == 1}')
    print(f'  speed across after the tap {abs(across1) / 512:.2f} cells/s (v*sin(turn) {model / 512:.2f}); '
          f'the tap and the tap back {REACT} s later move it {tap_move(sp / 512, steer_u * 2 * math.pi / 4096):.2f} cells')
    return {'speed': sp, 'turned': d_beta, 'model': want, 'across': abs(across1) / 512, 'across_model': model / 512}

# ---- the table for the docs: the old setup and the new one as RaceCarSetup.TrackSetups has it (whole km/h and percentages), planned and
# measured again exactly so
def report_one(key):
    new = json.load(open(f'{ROOT}/{OUT}.json'))['tracks'][key]['new']
    dd = f'{ROOT}/{key}/drive'
    setups = [pct(*OLD[key]), pct(*new)]
    open(dd + '/report.txt', 'w').write('\n'.join(fmt(s) for s in setups) + '\n')
    stress.engine(key, ['--exec-at', '40', f'raceplan file {dd}/report.txt {dd}/report_lap.txt {dd}/report_pts.txt'], 'drive/report', 50)
    kerb = kerb_half(key)
    got = []
    for setup, top, steer, rows in read_blocks(dd + '/report_pts.txt'):
        rooms = road_rooms(key, rows, kerb)
        got.append((measure(key, top, steer, rows, kerb, carried(key, rows), rooms), rooms))
    lapsr = [float(l.split()[4]) for l in open(dd + '/report_lap.txt')]
    os.remove(dd + '/report_pts.txt')
    (mo, rooms), (mn, _) = got
    return key, {'old': OLD[key], 'new': new, 'old_lap': lapsr[0], 'new_lap': lapsr[1], 'old_worst': mo['worst'], 'new_worst': mn['worst'],
                 'old_move': mo['move_fast'], 'new_move': mn['move_fast'], 'old_v': mo['v_fast'], 'new_v': mn['v_fast'],
                 'room_typ': mn['room_typ'], 'room_narrow': mn['room_narrow'], 'room_min': min(rooms), 'kerb': kerb}

def report(keys):
    with cf.ThreadPoolExecutor(int(os.environ.get('PAR', '6'))) as ex:
        rows = dict(ex.map(report_one, keys))
    json.dump(rows, open(f'{ROOT}/{OUT}_report.json', 'w'), indent=1)
    limit = json.load(open(f'{ROOT}/{OUT}.json'))['limit']
    names = {t[0]: t[1] for t in TRACKS}
    names['storm'] = 'Citadel, storm lap'; names['citabau'] = 'Citadel, town circuit'; names['polar'] = 'Polar Island (sprint)'
    names['palace'] = "The Emperor's palace (sprint)"; names['lava'] = 'Celebration, lava lake'
    print(f'limit {limit:.3f}')
    print('| track | room: typical / narrowest (cells) | old setup | old tap move | old lap (s) | new setup | new tap move | new lap (s) |')
    print('|---|---|---|---|---|---|---|---|')
    for k in keys:
        r = rows[k]
        def setup(s): return f'{s[0]} km/h, {s[1]} %, {s[2]} %'
        def move(m, w): return f'{m:.1f} cells ({w:.2f})'
        flag = '' if r['new_worst'] <= limit + 0.005 else ' *'
        print(f"| {names[k]} | {r['room_typ']:.2f} / {r['room_narrow']:.2f} | {setup(r['old'])} | {move(r['old_move'], r['old_worst'])} | {r['old_lap']:.1f} "
              f"| {setup(r['new'])} | {move(r['new_move'], r['new_worst'])}{flag} | {r['new_lap']:.1f} |")

# ---- the new setups driven: the test pilot's clean time trials (no opponents, no power-ups) with the old setup and the new, and a race with
# the new (opponents and power-ups), read as stress.py reads its runs (rescues, falls, missed checkpoints, standing still)
def verify_one(job):
    key, tag, setup, kind, planned = job
    d = f'{ROOT}/{key}'
    t = json.load(open(d + '/track.json'))
    base = open(d + ('/trial.txt' if kind == 'trial' else '/base.txt'), encoding='utf-8').read()
    car = f'car_{tag}.txt'
    open(f'{d}/{car}', 'w', encoding='utf-8').write(base + stress.car_keys(*setup))
    lap = planned or laps(key).get(K(setup)) or 90
    ticks = int(((1 if t['sprint'] else 3) * lap * 1.9 + 25) / 0.02)
    trace = []
    for k in range(250, ticks, 250): trace += ['--exec-at', str(k), 'racecars']
    code, run = stress.engine(key, ['--exec-at', '90', 'autodrive 1 5 0'] + trace, f'drive/{tag}', ticks, car)
    s = stress.summarize(run)
    s.update(key=key, tag=tag, setup=setup, kind=kind, exit=code, planned=lap)
    json.dump(s, open(run + '/summary.json', 'w'))
    return s

def verify(keys):
    stress.trial_files(keys)
    new = json.load(open(f'{ROOT}/drive.json'))['tracks']
    rep = json.load(open(f'{ROOT}/drive_report.json')) if os.path.exists(f'{ROOT}/drive_report.json') else {}
    jobs = []
    for k in keys:
        # (the new setup as the app has it: whole km/h and percentages; the planned laps from `report`)
        r = rep.get(k, {})
        jobs += [(k, 'v_old_trial', pct(*OLD[k]), 'trial', r.get('old_lap')), (k, 'v_new_trial', pct(*new[k]['new']), 'trial', r.get('new_lap')),
                 (k, 'v_new_race', pct(*new[k]['new']), 'race', r.get('new_lap'))]
    with cf.ThreadPoolExecutor(int(os.environ.get('PAR', '6'))) as ex:
        for s in ex.map(verify_one, jobs):
            probs = {p: v for p, v in s.items() if p in stress.PROBLEMS and v}
            laps_ = ' '.join(f'{x:.1f}' for x in s['laps'][:3])
            print(f"{s['key']:12} {s['tag']:12} exit {s['exit']} planned {s['planned']:.1f} laps [{laps_}] {s['result']} {probs} "
                  f"still {s['stuck_s']}s {'CRASH' if s['crash'] else ''}", flush=True)

if __name__ == '__main__':
    what = sys.argv[1]
    if what == 'tap':
        key, top_kmh, steer = sys.argv[2], float(sys.argv[3]), float(sys.argv[4])
        tag = sys.argv[5] if len(sys.argv) > 5 else f'tap_{int(top_kmh)}_{int(steer)}'
        at = int(sys.argv[6]) if len(sys.argv) > 6 else None
        code, run, rows = tap(key, top_kmh, steer, tag, at)
        print(code, run, len(rows), 'frames traced')
        tap_report(rows, top_kmh, steer)
    else:
        keys = sys.argv[2:] or [t[0] for t in TRACKS]
        if what == 'points': points(keys)
        elif what == 'table': table(keys)
        elif what == 'verify': verify(keys)
        elif what == 'report': report(keys)
