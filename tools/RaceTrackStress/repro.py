# repro.py <key> <tag> <side> <top> <accel> <brake> <steer> <from s> <to s> [ticks]: a pilot run with screenshots and state every 2 s in a window
import sys, os, json
sys.argv, args = ['x'], sys.argv[1:]
exec(open('E:/dump/TEMP/stress/stress.py', encoding='utf-8').read().split("if __name__")[0])
key, tag, side = args[0], args[1], float(args[2])
setup = tuple(int(x) for x in args[3:7]); t0, t1 = int(args[7]), int(args[8])
ticks = int(args[9]) if len(args) > 9 else t1 * 50 + 500
d = f'{ROOT}/{key}'
base = open(d + '/base.txt', encoding='utf-8').read()
open(f'{d}/car_{tag}.txt', 'w', encoding='utf-8').write(base + car_keys(*setup))
run = f'{d}/{tag}'
os.makedirs(run + '/shots', exist_ok=True); os.makedirs(run + '/state', exist_ok=True)
extra = ['--exec-at', '90', f'autodrive 1 5 {side}']
for k in range(250, ticks, 250): extra += ['--exec-at', str(k), 'racecars']
for t in range(t0, t1 + 1, 2):
    extra += ['--exec-at', str(t * 50 + 1), f'screenshot {run}/shots/t{t:03d}.png', '--exec-at', str(t * 50 + 3), f'dumpstate {run}/state/s{t:03d}.json']
code, run = engine(key, extra, tag, ticks, f'car_{tag}.txt')
print(code, run)
