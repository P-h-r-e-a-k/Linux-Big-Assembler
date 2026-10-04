# The raised road in the engine (Celebration Island's lap): puts Twinsen's car, or Twinsen on foot, on a stretch of the road in a
# headless run of the engine's race-track mode, holds some keys, and checks from the engine's own trace of his position that he stays on
# the road -- at its height, inside its rails -- whatever he steers.
#   raisedtest.py <game folder> <car file> <scratch folder> [scenario ...]
# The car file is the race-track mode's (ScriptRoundTrip racecarfile); the raised road is read from the file its raised= line names (its
# points "x z y half [bank]", the banking in ten-thousandths). RAISED_SCENE: the scene to test in (95, Celebration Island's; 120 the
# Elevator Platform's). A banked road gets two more scenarios, on its most banked point.
import math, os, re, subprocess, sys

ENGINE = 'E:/dump/LBAAssembler/native/lba2-classic-community/out/build/windows_ucrt64_static/SOURCES/lba2cc.exe'
game, car, scratch = sys.argv[1], sys.argv[2], sys.argv[3]
only = sys.argv[4:]
os.makedirs(scratch, exist_ok=True)

info = dict(line.strip().split('=', 1) for line in open(car) if '=' in line and not line.startswith('#'))
road = [tuple(int(v) for v in line.split()) + (0,) * (5 - len(line.split())) for line in open(info['raised']) if line.strip()]
cube = [int(v) for v in info['startline'].split()[:2]]
scene = int(os.environ.get('RAISED_SCENE', '95'))
INSET = 640

def floor_at(wx, wz, ref):
    """the road's height at a world position for something at height ref, how far to the side of its middle, the point before"""
    best = None
    for i in range(len(road) - 1):
        ax, az, ay, ah, ab = road[i]; bx, bz, by, bh, bb = road[i + 1]
        sx, sz = bx - ax, bz - az; l2 = sx * sx + sz * sz
        if l2 == 0: continue
        t = max(0.0, min(1.0, ((wx - ax) * sx + (wz - az) * sz) / l2))
        px, pz = ax + sx * t, az + sz * t
        d = math.hypot(wx - px, wz - pz)
        if d > ah + 96: continue
        lat = (sx * (wz - pz) - sz * (wx - px)) / math.sqrt(l2)
        y = ay + (by - ay) * t + lat * (ab + (bb - ab) * t) / 10000
        if y > ref + 700: continue
        if best is None or y > best[0] + 1200 or (y > best[0] - 1200 and d < best[3]): best = (y, lat, i, d, ah)
    return best

def point(index, side=0.0, up=0):
    """a place on the road: point `index`, `side` units to the left of its middle; and the way the lap runs there (the engine's turn)"""
    ax, az, ay, ah, ab = road[index]; bx, bz, by, bh, bb = road[index + 1]
    sx, sz = bx - ax, bz - az; l = math.hypot(sx, sz)
    x, z = ax - sz / l * side, az + sx / l * side
    ay += side * ab / 10000
    beta = int(round(math.atan2(sx, sz) * 4096 / (2 * math.pi))) % 4096
    return int(x - cube[0] * 32768), int(ay + up), int(z - cube[1] * 32768), beta

rx = re.compile(r'\[obj\] t=(\d+) obj=0 pos=(-?\d+),(-?\d+),(-?\d+) .*? move=(-?\d+) flags=(\d+)')

def run(name, commands, ticks, driving=True):
    user = os.path.join(scratch, 'user_' + name)
    args = [ENGINE, '--headless', '--no-audio', '--game-dir', game, '--user-dir', user, '--no-autosave', '--resolution', '640x480', '--fixed-dt', '20',
            '--exec-at', '4', 'skipmodals 1', '--exec-at', '5', 'vargame 74 3', '--exec-at', '6', f'cube {scene}']
    if driving: args += ['--exec-at', '60', 'input action 5']
    for at, command in commands: args += ['--exec-at', str(at), command]
    args += ['--tick', str(ticks), '--exit']
    out = subprocess.run(args, capture_output=True, text=True, env=dict(os.environ, LBA2_RACETRACK_FILE=car), timeout=300)
    text = out.stdout + out.stderr
    open(os.path.join(scratch, name + '.log'), 'w').write(text)
    return [(int(t), int(x), int(y), int(z), int(move)) for t, x, y, z, move, flags in rx.findall(text) if '[INFO]' not in '']

def check(name, trace, since, expect_on=True, slack=40):
    """from the trace (after the first `since` samples): how far off the road's height, how near its edge, how far he travelled"""
    rows = trace[since:]
    if not rows: return f'{name}: NO TRACE'
    worst_y = 0; worst_lat = 0; off = 0; half = 0
    for t, x, y, z, move in rows:
        f = floor_at(x + cube[0] * 32768, z + cube[1] * 32768, y + 64)
        if f is None: off += 1; continue
        worst_y = max(worst_y, abs(y - f[0])); worst_lat = max(worst_lat, abs(f[1])); half = f[4]
    travelled = math.hypot(rows[-1][1] - rows[0][1], rows[-1][3] - rows[0][3])
    ok = off == 0 and worst_y <= slack and worst_lat <= half - INSET + 8
    if not expect_on: ok = off == len(rows) or worst_y > 1000
    return (f'{"ok  " if ok else "FAIL"} {name}: {len(rows)} frames, {travelled / 512:.1f} cells travelled, off the road in {off}, '
            f'up to {worst_y} from its height, up to {worst_lat:.0f} from its middle (the rail is at {half - INSET}); ends at {rows[-1][1:4]}')

results = []
def scenario(name, index, keys, side=0.0, ticks=260, driving=True, up=0, turn=0, expect_on=True, slack=40):
    if only and name not in only: return
    x, y, z, beta = point(index, side, up)
    beta = (beta + turn) % 4096
    commands = [(100, f'teleport {x} {y} {z} {beta}'), (110, 'objtrace 0'), (120, f'input {keys} {ticks}')]
    trace = run(name, commands, 130 + ticks, driving)
    results.append(check(name, trace, 12, expect_on, slack))
    print(results[-1], flush=True)

n = len(road)
spiral = n // 4                     # on the first loop, over the sea
upper = n // 2                      # on the second loop
steep = [i for i in range(n - 1) if road[i][2] - road[i + 1][2] > 200]                      # the bridge: where the road falls steeply
bridge = steep[len(steep) // 3] if steep else upper
# the car: ahead, and steered hard either way, on the loops and on the bridge
scenario('car-ahead', spiral, 'up')
scenario('car-left', spiral, 'up+left')
scenario('car-right', spiral, 'up+right')
scenario('car-upper-left', upper, 'up+left')
scenario('car-upper-right', upper, 'up+right')
scenario('car-reverse', spiral, 'down')
scenario('car-from-the-rail', spiral, 'up', side=1200)
scenario('car-bridge', bridge, 'up', ticks=60)
scenario('car-bridge-left', bridge, 'up+left', ticks=100)
scenario('car-bridge-right', bridge, 'up+right', ticks=100)
scenario('car-bridge-up', bridge, 'down', ticks=200)
# Twinsen on foot: dropped onto the road, walking ahead and across it
scenario('foot-ahead', spiral, 'up', driving=False, up=300)
scenario('foot-across', spiral, 'up', driving=False, up=300, turn=1024)
scenario('foot-across-right', upper, 'up', driving=False, up=300, turn=3072)
# (walking down the bridge he is a step behind the floor: each frame's step forward takes it a little further down than he comes)
scenario('foot-bridge', bridge, 'up', driving=False, up=300, ticks=150, slack=150)
# a banked road: the car steered into and away from its most banked bend, and Twinsen walking across it
banked = max(range(n - 1), key=lambda i: abs(road[i][4]))
if road[banked][4]:
    scenario('car-banked-left', banked, 'up+left')
    scenario('car-banked-right', banked, 'up+right')
    scenario('foot-banked-across', banked, 'up', driving=False, up=300, turn=1024)
print()
print(sum(r.startswith('ok') for r in results), 'of', len(results), 'ok')
