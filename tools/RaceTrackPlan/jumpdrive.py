"""Drive the built jump headless: the buggy parked N cells before the take-off (driveprep), Twinsen mounts it, full throttle along the road.
Prints the car's path through the jump (distance along the jump line, sideways offset, height) and takes screenshots.

usage: jumpdrive.py <game> <tag> [lateral cells] [car file] [shots ticks,comma]"""
import sys, os, re, math, shutil, subprocess, glob, json
sys.path.insert(0, r'E:\dump\LBAAssembler\tools\RaceTrackPlan')
from ile import load_ile

SRT = r'E:\dump\TEMP\srt_new\ScriptRoundTrip.exe'
ENG = r'E:\dump\LBAAssembler\native\lba2-classic-community\out\build\windows_ucrt64_static\SOURCES\lba2cc.exe'
SC = {(7, 8): 55, (7, 9): 56, (7, 10): 57, (7, 11): 58, (8, 7): 59, (8, 8): 60, (8, 9): 61, (8, 10): 62, (8, 11): 63, (9, 7): 64, (9, 8): 65,
      (9, 9): 66, (9, 10): 67, (9, 11): 68, (10, 7): 69, (10, 8): 70, (10, 9): 71, (10, 10): 72, (10, 11): 73}

src, tag = sys.argv[1], sys.argv[2]
lateral = float(sys.argv[3]) if len(sys.argv) > 3 else 0.0
car = sys.argv[4] if len(sys.argv) > 4 and sys.argv[4] != '-' else ''
shots = [int(t) for t in sys.argv[5].split(',')] if len(sys.argv) > 5 else []
log = open(os.path.join(src, 'jumplog.txt'), encoding='utf-8', errors='replace').read() if os.path.exists(os.path.join(src, 'jumplog.txt')) else ''
m = re.search(r'starts at cell \(([\d.]+), ([\d.]+)\).*?lands .*? at \(([\d.]+), ([\d.]+)\); heading turn (\d+)', log, re.S)
sx, sz, lx, lz, beta = float(m[1]), float(m[2]), float(m[3]), float(m[4]), int(m[5])
hx, hz = math.sin(beta * 2 * math.pi / 4096), math.cos(beta * 2 * math.pi / 4096)
nx, nz = -hz, hx
back = 20
bx, bz = sx - hx * back + nx * lateral, sz - hz * back + nz * lateral
cube = (int(bx) // 64, int(bz) // 64); scene = SC[cube]

game = rf'E:\dump\TEMP\race\drive_{tag}'
if os.path.exists(game): shutil.rmtree(game)
shutil.copytree(src, game, ignore=shutil.ignore_patterns('SAVE', 'jumplog.txt'))
r = subprocess.run([SRT, 'driveprep', game, f'{bx:.3f}', f'{bz:.3f}', str(beta)], capture_output=True, text=True)
m2, _, _, cubes = load_ile(os.path.join(game, 'DESERT.ILE'))
def ground(x, z):
    c = cubes[m2[(int(z) // 64) * 16 + int(x) // 64] & 127]; return int(c.heights[min(64, int(round(z - (int(z) // 64) * 64))), min(64, int(round(x - (int(x) // 64) * 64)))])
# Twinsen a cell and a half behind the buggy, facing it
tx, tz = bx - hx * 1.5, bz - hz * 1.5
hero = (int((tx - cube[0] * 64) * 512), ground(tx, tz) + 400, int((tz - cube[1] * 64) * 512), beta)
user = rf'E:\dump\TEMP\race\user_{tag}'
shutil.rmtree(user, ignore_errors=True); os.makedirs(user)
args = [ENG, '--headless', '--no-audio', '--game-dir', game, '--user-dir', user, '--no-autosave', '--resolution', '640x480', '--fixed-dt', '20',
        '--exec-at', '4', 'skipmodals 1', '--exec-at', '5', 'vargame 74 3', '--exec-at', '6', f'cube {scene}',
        '--exec-at', '30', 'teleport %d %d %d %d' % hero, '--exec-at', '50', 'input action 5', '--exec-at', '80', 'objtrace 0', '--exec-at', '85', 'input up 500']
for t in shots: args += ['--exec-at', str(t), f'screenshot {user}\\shot_{t}.png']
args += ['--tick', str(max(shots + [560]) + 10), '--exit']
env = dict(os.environ)
if car: env['LBA2_RACETRACK_FILE'] = car
else: env.pop('LBA2_RACETRACK_FILE', None)
p = subprocess.run(args, capture_output=True, text=True, env=env)
text = (p.stdout + p.stderr)
pts = []
for mm in re.finditer(r't=(\d+) obj=0 pos=(-?\d+),(-?\d+),(-?\d+) rot=\S+ step=\S+ anim=(\d+) frame=\d+ track=\S+ label=\S+ comport=\d+\s+move=(\d+)', text):
    t, x, y, z, anim, move = map(int, mm.groups())
    pts.append((t, x, y, z, anim, move))
print(f'buggy parked {back} cells before the take-off, {lateral:+.1f} cells sideways, scene {scene}; {len(pts)} trace points; driveprep rc {r.returncode}')
# along/lateral relative to the take-off point, in the cube the car is in when it gets there (the jump is inside cube (9,9))
last = None
for (t, x, y, z, anim, move) in pts:
    cx = cube[0] * 64 + x / 512; cz = cube[1] * 64 + z / 512
    a = (cx - sx) * hx + (cz - sz) * hz; u = (cx - sx) * nx + (cz - sz) * nz
    if -6 <= a <= 30 and (last is None or t - last >= 60):
        print(f'  t={t} along {a:6.2f} side {u:5.2f} y {y:5d} ground {ground(cx, cz):5d} anim {anim} move {move}')
        last = t
for f in sorted(glob.glob(user + r'\shot_*.png')): print('shot', f)
