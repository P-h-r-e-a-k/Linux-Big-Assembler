"""Walks every place a built race track's racing line crosses a cube edge, both ways, in the engine: for each, an engine run loads the
scene on the near side, puts Twinsen on the line a few cells before the edge facing across it, walks him over and dumps the state; the
report says which scene he ended in. A cube edge the road crosses with no cube-change zone over it is an invisible wall (the engine holds
the hero at the edge), so every crossing should end OK.

    python edgetest.py <game folder> <scratch folder> [island byte] [first scene] [last scene]
      Citadel Island: 0 42 50 (the default); the Desert island: 2 55 73
    EDGE_WALK=<ticks>  how long each walk is (360); a crossing next to a cube corner can take a long walk over two edges
    EDGE_ONLY=4,7,..   only those crossings
    EDGE_TWIN=1        the track of the island's other-weather file (RACETRACK.JSON's Twin: Citadel Island's town circuit), with
                       LBA2_RACETRACK_FILE naming a car file that has weather=fine

Loading a scene cold starts Twinsen at its own start point, which in Citadel scene 48 is inside a scripted arrival (scenario zone 7)
that holds him: walk into such a scene from a neighbour first (as a car arrives) before testing its departures."""
import json, math, os, subprocess, sys, glob
import re as _re

game, out = sys.argv[1], sys.argv[2]
ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
ENGINE = os.path.join(ROOT, 'native', 'lba2-classic-community', 'out', 'build', 'windows_ucrt64_static', 'SOURCES', 'lba2cc.exe')
PROBE = os.path.join(ROOT, 'tools', 'ScriptRoundTrip', 'bin', 'Debug', 'net10.0', 'ScriptRoundTrip.exe')
# the island's outside scenes by cube, from the game itself
_island = int(sys.argv[3]) if len(sys.argv) > 3 else 0
_lo, _hi = (int(sys.argv[4]), int(sys.argv[5])) if len(sys.argv) > 5 else (42, 50)
_out = subprocess.run([PROBE, 'racetrack', 'island', str(_island)], capture_output=True, text=True, env={**os.environ, 'LBA2_DIR': game}).stdout
SCENES = {}
for _m in _re.finditer(r'scene (\d+) .*?cubeMode 1 cube \((\d+),(\d+)\)', _out):
    _sc = int(_m.group(1))
    if _lo <= _sc <= _hi: SCENES.setdefault((int(_m.group(2)), int(_m.group(3))), _sc)
print('scenes by cube:', SCENES)
BACK = 4.0          # cells before the edge the walk starts
WALK = int(os.environ.get("EDGE_WALK", "360"))   # ticks of walking

j = json.load(open(os.path.join(game, 'RACETRACK.JSON')))
# EDGE_TWIN=1: the other-weather file's own track (Citadel Island's town circuit; run with LBA2_RACETRACK_FILE naming a car file with weather=fine)
if os.environ.get('EDGE_TWIN') == '1': j = j['Twin']
pts = [(p[0] / 512.0, p[1] / 512.0, p[2]) for p in j['Path']]
n = len(pts)
tests = []
for i in range(n):
    a, b = pts[i], pts[(i + 1) % n]
    ca = (int(a[0] // 64), int(a[1] // 64)); cb = (int(b[0] // 64), int(b[1] // 64))
    if ca == cb or ca not in SCENES or cb not in SCENES:
        continue
    dx, dz = b[0] - a[0], b[1] - a[1]
    L = math.hypot(dx, dz); dx, dz = dx / L, dz / L
    for (frm, to, sign) in ((ca, cb, 1), (cb, ca, -1)):
        # the start: BACK cells before the edge, on the line, in the `frm` cube, facing across
        mx, mz, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2
        sx, sz = mx - sign * dx * BACK, mz - sign * dz * BACK
        # (keep the start inside its own cube)
        if (int(sx // 64), int(sz // 64)) != frm:
            continue
        beta = int(round(math.atan2(sign * dx, sign * dz) / (2 * math.pi) * 4096)) % 4096
        lx, lz = int(round((sx - frm[0] * 64) * 512)), int(round((sz - frm[1] * 64) * 512))
        tests.append(dict(frm=SCENES[frm], to=SCENES[to], x=lx, y=int(my) + 300, z=lz, beta=beta, at=(round(mx, 1), round(mz, 1))))
print(len(tests), 'crossings to walk (both ways)')

os.makedirs(out, exist_ok=True)
results = []
ONLY = {int(v) for v in os.environ.get("EDGE_ONLY", "").split(",") if v}
for k, t in enumerate(tests):
    if ONLY and k not in ONLY: continue
    user = os.path.join(out, 'u%02d' % k)
    if os.path.isdir(user):
        for f in glob.glob(os.path.join(user, 'save', 'shoot', '*')):
            os.remove(f)
    os.makedirs(user, exist_ok=True)
    args = [ENGINE, '--headless', '--no-audio', '--game-dir', game, '--user-dir', user, '--no-autosave', '--resolution', '320x240',
            '--fixed-dt', '20', '--exec-at', '4', 'skipmodals 1', '--exec-at', '6', 'cube %d' % t['frm'],
            '--exec-at', '60', 'teleport %d %d %d %d' % (t['x'], t['y'], t['z'], t['beta']),
            '--exec-at', '70', 'input up %d' % WALK, '--exec-at', str(70 + WALK + 20), 'dumpstate', '--tick', str(70 + WALK + 30), '--exit']
    subprocess.run(args, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=240)
    states = sorted(glob.glob(os.path.join(user, 'save', 'shoot', 'state_*.json')), key=os.path.getmtime)
    if not states:
        results.append((t, None, None)); print('%2d  %d -> %d at %s: NO STATE' % (k, t['frm'], t['to'], t['at'])); continue
    d = json.load(open(states[-1])); h = d['actors'][0]
    scene = d['scene']['cube']
    ok = scene == t['to']
    results.append((t, scene, (h['x'], h['y'], h['z'])))
    print('%2d  %d -> %d at cell %s: ended in scene %d at (%d,%d,%d)  %s' % (k, t['frm'], t['to'], t['at'], scene, h['x'], h['y'], h['z'], 'OK' if ok else 'BLOCKED'))
bad = [r for r in results if r[1] != r[0]['to']]
print('%d of %d crossings passed' % (len(results) - len(bad), len(results)))
