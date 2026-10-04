# The test pilot's run on a raised road, from the engine's log: the car's speed along the lap (km/h, each half second), where the road's
# rail holds it, how far it is ever off the road's height; and, frame by frame, whether it is ever under the road it was on a frame
# before (gone through it) or further from the road's middle than the rail allows.
#   pilottrace.py <engine log> <raised road file> [cube x] [cube z]
# The log is a headless run of the race-track mode with `autodrive 1 5` and `objtrace 0` (Twinsen's place every frame); the road file is
# the one the car file's raised= line names (x z y half [bank]). The cube is the start line's, (7, 7) when not given.
import re, sys, math
log, raised = sys.argv[1], sys.argv[2]
road = [tuple(int(v) for v in line.split()) + (0,) * (5 - len(line.split())) for line in open(raised) if line.strip()]
rx = re.compile(r'\[obj\] t=(\d+) obj=0 pos=(-?\d+),(-?\d+),(-?\d+)')
rows = [tuple(int(v) for v in m.groups()) for m in rx.finditer(open(log, errors='replace').read())]
seen = set(); pts = []
for r in rows:
    if r[0] in seen: continue
    seen.add(r[0]); pts.append(r)
ox = int(sys.argv[3]) * 32768 if len(sys.argv) > 3 else 7 * 32768
oz = int(sys.argv[4]) * 32768 if len(sys.argv) > 4 else 7 * 32768
def floor(wx, wz, ref):
    best = None
    for i in range(len(road) - 1):
        ax, az, ay, ah, ab = road[i]; bx, bz, by, bh, bb = road[i + 1]
        sx, sz = bx - ax, bz - az; l2 = sx * sx + sz * sz
        if not l2: continue
        if abs(wx - ax) > 3000 or abs(wz - az) > 3000: continue
        t = max(0.0, min(1.0, ((wx - ax) * sx + (wz - az) * sz) / l2))
        px, pz = ax + sx * t, az + sz * t
        d = math.hypot(wx - px, wz - pz)
        if d > ah + 96: continue
        lat = (sx * (wz - pz) - sz * (wx - px)) / math.sqrt(l2)
        y = ay + (by - ay) * t + lat * (ab + (bb - ab) * t) / 10000
        if y > ref + 700: continue
        if best is None or y > best[0] + 1200 or (y > best[0] - 1200 and d < best[3]): best = (y, lat, i, d, ah)
    return best
out = []; worst = 0; rail = 0; off = 0
for k in range(25, len(pts), 25):
    a, b = pts[k - 25], pts[k]
    dt = (b[0] - a[0]) / 1000
    v = math.hypot(b[1] - a[1], b[3] - a[3]) / dt if dt > 0 else 0
    f = floor(b[1] + ox, b[3] + oz, b[2] + 64)
    if f is None: off += 1; out.append('  off'); continue
    worst = max(worst, abs(b[2] - f[0]))
    touching = abs(f[1]) >= f[4] - 640 - 8
    rail += touching
    out.append(f'{v * 3.6 / 512:3.0f}{"|" if touching else " "}')
print('speed each half second, km/h (| against the rail):')
for k in range(0, len(out), 20): print(f'  {k / 2:5.1f}s  ' + ' '.join(out[k:k + 20]))
print(f'{len(pts)} frames; off the road in {off} samples; up to {worst:.0f} from the road\'s height; against the rail in {rail} of {len(out)} samples')

# frame by frame: under the road it was on a frame before (the level for its height then), and how far from the road's middle
def level(wx, wz, ref):
    cands = []
    for i in range(len(road) - 1):
        ax, az, ay, ah, ab = road[i]; bx, bz, by, bh, bb = road[i + 1]
        if abs(wx - ax) > 3000 or abs(wz - az) > 3000: continue
        sx, sz = bx - ax, bz - az; l2 = sx * sx + sz * sz
        if not l2: continue
        t = max(0.0, min(1.0, ((wx - ax) * sx + (wz - az) * sz) / l2))
        px, pz = ax + sx * t, az + sz * t
        d = math.hypot(wx - px, wz - pz)
        if d > ah + 96: continue
        lat = (sx * (wz - pz) - sz * (wx - px)) / math.sqrt(l2)
        y = ay + (by - ay) * t + lat * (ab + (bb - ab) * t) / 10000
        if y > ref + 700: continue
        cands.append((y, d))
    if not cands: return None
    top = max(c[0] for c in cands)
    return min((c for c in cands if c[0] >= top - 1200), key=lambda c: c[1])
def middle(wx, wz, y):
    """how far from the middle of the road at its own height (within 700 of it)"""
    best = None
    for i in range(len(road) - 1):
        ax, az, ay, ah, ab = road[i]; bx, bz, by, bh, bb = road[i + 1]
        if abs(wx - ax) > 3000 or abs(wz - az) > 3000: continue
        sx, sz = bx - ax, bz - az; l2 = sx * sx + sz * sz
        if not l2: continue
        t = max(0.0, min(1.0, ((wx - ax) * sx + (wz - az) * sz) / l2))
        if abs(ay + (by - ay) * t - y) > 700: continue
        d = math.hypot(wx - ax - sx * t, wz - az - sz * t)
        if best is None or d < best: best = d
    return best
under = 0; farthest = 0; before = None
for t, x, y, z in pts:
    here = level(x + ox, z + oz, (before if before is not None else y) + 64)
    if here is not None and y < here[0] - 100: under += 1
    m = middle(x + ox, z + oz, y)
    if m is not None: farthest = max(farthest, m)
    before = y
print(f"frame by frame: under the road it was on in {under} frames; at most {farthest:.0f} from the road's middle (the rail holds a car at {road[0][3] - 640})")
