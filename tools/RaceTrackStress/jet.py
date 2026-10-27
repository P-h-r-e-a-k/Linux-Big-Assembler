# jet.py <track key> <tag> <side> <first item tick> [more item ticks ...]: a track with the pilot at <side> cells, the super jet-pack
# forced on at each tick (raceitem 4), the cars' places every 10 ticks for 600 ticks after each one; the run in tp/<tag>
import sys, os, subprocess
sys.argv, args = ['x'], sys.argv[1:]
exec(open('E:/dump/TEMP/stress/stress.py', encoding='utf-8').read().split("if __name__")[0])
key, tag, side, at = args[0], args[1], args[2], [int(x) for x in args[3:]]
d = f'{ROOT}/{key}'
t = json.load(open(d + '/track.json'))
run = f'{ROOT}/tp/{tag}'
os.makedirs(run + '/user', exist_ok=True)
extra = ['--exec-at', '90', f'autodrive 1 5 {side}']
for a in at:
    extra += ['--exec-at', str(a), 'raceitem 4']
    for k in range(a + 10, a + 600, 10): extra += ['--exec-at', str(k), 'racecars']
env = dict(os.environ, LBA2_RACETRACK_FILE=f'{d}/base.txt')
cmd = [ENG, '--headless', '--no-audio', '--game-dir', GAME, '--user-dir', run + '/user', '--no-autosave', '--resolution', '640x480',
       '--fixed-dt', '20', '--exec-at', '4', 'skipmodals 1', '--exec-at', '6', f"cube {t['scene']}"] + extra + ['--tick', str(max(at) + 1500), '--exit']
with open(run + '/run.log', 'w', encoding='utf-8', errors='replace') as log:
    p = subprocess.run(cmd, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=3600)
print(p.returncode, run)
