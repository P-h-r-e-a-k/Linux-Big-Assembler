# cartrace.py <track key> <tag> <side> <from tick> <to tick>: a pilot race with the console's cartrace on between the ticks (a [cartrace] line
# each frame: the car's place, pitch, roll, speed, fall, floor); the run in tp/<tag>
import sys, os, subprocess
sys.argv, args = ['x'], sys.argv[1:]
exec(open('E:/dump/TEMP/stress/stress.py', encoding='utf-8').read().split("if __name__")[0])
key, tag, side, t0, t1 = args[0], args[1], args[2], int(args[3]), int(args[4])
d = f'{ROOT}/{key}'
t = json.load(open(d + '/track.json'))
run = f'{ROOT}/tp/{tag}'
os.makedirs(run + '/user', exist_ok=True)
extra = ['--exec-at', '90', f'autodrive 1 5 {side}', '--exec-at', str(t0), 'cartrace 1', '--exec-at', str(t1), 'cartrace 0']
env = dict(os.environ, LBA2_RACETRACK_FILE=f'{d}/base.txt')
cmd = [ENG, '--headless', '--no-audio', '--game-dir', GAME, '--user-dir', run + '/user', '--no-autosave', '--resolution', '640x480',
       '--fixed-dt', '20', '--exec-at', '4', 'skipmodals 1', '--exec-at', '6', f"cube {t['scene']}"] + extra + ['--tick', str(t1 + 10), '--exit']
with open(run + '/run.log', 'w', encoding='utf-8', errors='replace') as log:
    p = subprocess.run(cmd, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=3600)
print(p.returncode, run)
