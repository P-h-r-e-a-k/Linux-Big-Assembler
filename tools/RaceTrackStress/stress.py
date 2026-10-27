# Stress test of every race track, and car setups scored on each (2026-10-16).
#   python stress.py files            race files for every track (base = the standalone's car: 45/57/63/97/110 km/h, accel/brake/coast 300 %, steer 200 %)
#   python stress.py plan             every track's line planned for a grid of setups (the engine's raceplan): plan_<key>.txt
#   python stress.py pilot [keys...]  the test pilot's runs: base on the line, base off the line, a hot car, the best setup; run logs + summary.json
#   python stress.py report           summary of both
import json, os, re, subprocess, sys, itertools, concurrent.futures as cf

# (STRESS_ROOT: another folder of runs, its own game in it -- one track built alone)
ROOT = os.environ.get('STRESS_ROOT', 'E:/dump/TEMP/stress')
GAME = ROOT + '/game'
# (STRESS_ENGINE, STRESS_SR: another engine and ScriptRoundTrip -- a worktree's own builds)
ENG = os.environ.get('STRESS_ENGINE', 'E:/dump/LBAAssembler/native/lba2-classic-community/out/build/windows_ucrt64_static/SOURCES/lba2cc.exe')
SR = os.environ.get('STRESS_SR', 'E:/dump/LBAAssembler/tools/ScriptRoundTrip/bin/Debug/net10.0/ScriptRoundTrip.exe')

# key, the track's name in RACETRACK.JSON, its island file (RT_RACE), extra environment
TRACKS = [
    ('desert', 'Desert island', 'DESERT.ILE', {}),
    ('storm', 'Citadel Island', 'CITADEL.ILE', {'RT_WEATHER': 'rain'}),
    ('citabau', 'Citadel Island', 'CITABAU.ILE', {}),
    ('mosquibe', 'Mosquibees Island', 'MOSQUIBE.ILE', {}),
    ('celebration', 'Celebration Island', 'CELEBRA2.ILE', {}),
    ('lava', 'Celebration Island, lava lake', 'CELEBRAT.ILE', {}),
    ('elevator', 'Elevator Platform', 'ASCENCE.ILE', {}),
    ('sendell', "Sendell's Well", 'SENDELL.ILE', {}),
    ('moon', 'The old moon', 'MOON.ILE', {}),
    ('polar', 'Polar Island', 'POLAR.ILE', {}),
    ('francos', 'Island of the Francos', 'KNARTAS.ILE', {}),
    ('otringal', 'Otringal', 'OTRINGAL.ILE', {}),
    ('volcano', 'Volcano Island', 'SOUSCELB.ILE', {}),
    ('emerald', 'The Emerald Moon', 'EMERAUDE.ILE', {}),
    ('wannies', 'Wannies Island', 'PLATFORM.ILE', {}),
    ('palace', "The Emperor's palace", 'PALACE.ILE', {}),
    ('cx', 'Island CX', 'CXTOWER.ILE', {}),
    ('temple', 'The Temple of Bú', 'TEMPLE.ILE', {}),
]

def kmh(v): return round(v / 3.6 * 512)
BASE_TOPS = [45, 57, 63, 97, 110]
BASE = {'accel': 12.0, 'brake': 36.0, 'coast': 21.0, 'steer': 2048}

def car_keys(top=100, accel=100, brake=100, steer=100):
    s = [f'gears={len(BASE_TOPS)}']
    s += [f'gear{i + 1}={kmh(t * top / 100)}' for i, t in enumerate(BASE_TOPS)]
    s += [f"accel={BASE['accel'] * accel / 100:.3f}", f"brake={BASE['brake'] * brake / 100:.3f}", f"coast={BASE['coast']:.3f}",
          f'reverse={kmh(24)}', f"steer={round(BASE['steer'] * steer / 100)}", 'automatic=1', 'hud=1']
    return '\n'.join(s) + '\n'

def info():
    t = json.load(open(GAME + '/RACETRACK.JSON', encoding='utf-8'))
    out = {}
    for x in [t] + (t.get('Others') or []):
        out.setdefault(x['Island'], []).append(x)
        if x.get('Twin'): out.setdefault(x['Island'] + '#twin', []).append(x['Twin'])
    return out

def start_scene(key, name):
    tracks = info()
    if key == 'citabau': return tracks[name + '#twin'][0]['StartScene']
    return tracks[name][0]['StartScene']

def files(keys=None):
    for key, name, ile, env in TRACKS:
        if keys and key not in keys: continue
        d = f'{ROOT}/{key}'
        os.makedirs(d, exist_ok=True)
        e = dict(os.environ, RT_RACE=ile, **env)
        r = subprocess.run([SR, 'racecarfile', GAME, d + '/race.txt'], env=e, capture_output=True, text=True)
        text = open(d + '/race.txt', encoding='utf-8').read()
        sprint = re.search(r'^sprint=1', text, re.M) is not None
        laps = '' if sprint or re.search(r'^race_laps=', text, re.M) else 'race_laps=3\n'
        open(d + '/base.txt', 'w', encoding='utf-8').write(text + car_keys() + 'start_in_car=1\nqualifying=0\n' + laps)
        scene = start_scene(key, name)
        json.dump({'key': key, 'name': name, 'ile': ile, 'scene': scene, 'sprint': sprint}, open(d + '/track.json', 'w'))
        print(f'{key:12} scene {scene:4} sprint {sprint} {r.stdout.strip()}')

def grid():
    vals = range(70, 131, 10)
    g = [(t, a, b, s) for t, a, b, s in itertools.product(vals, vals, vals, vals) if t + a + b + s == 400]
    extra = [(100, 100, 100, 100), (110, 100, 100, 100), (100, 110, 100, 100), (100, 100, 110, 100), (100, 100, 100, 110),
             (120, 120, 120, 120)]
    return extra + [x for x in g if x not in extra]

def engine(key, args, tag, ticks, carfile='base.txt'):
    d = f'{ROOT}/{key}'
    t = json.load(open(d + '/track.json'))
    run = f'{d}/{tag}'
    os.makedirs(run + '/user', exist_ok=True)
    env = dict(os.environ, LBA2_RACETRACK_FILE=f'{d}/{carfile}')
    cmd = [ENG, '--headless', '--no-audio', '--game-dir', GAME, '--user-dir', run + '/user', '--no-autosave', '--resolution', '640x480',
           '--fixed-dt', '20', '--exec-at', '4', 'skipmodals 1', '--exec-at', '6', f"cube {t['scene']}"] + args + ['--tick', str(ticks), '--exit']
    with open(run + '/run.log', 'w', encoding='utf-8', errors='replace') as log:
        p = subprocess.run(cmd, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=3600)
    return p.returncode, run

def plan_one(key):
    d = f'{ROOT}/{key}'
    open(d + '/grid.txt', 'w').write('\n'.join(' '.join(map(str, x)) for x in grid()) + '\n')
    # (and the brakes left alone: top speed, pull and steering traded, 70-130 % in 5s, summing to 300 -- grid3.txt)
    code, run = engine(key, ['--exec-at', '40', f'raceplan file {d}/grid.txt {d}/plan.txt',
                             '--exec-at', '42', f'raceplan file {ROOT}/grid3.txt {d}/plan3.txt'], 'plan', 60)
    rows = [l.split() for l in open(d + '/plan.txt')] if os.path.exists(d + '/plan.txt') else []
    return key, code, len(rows)

def plan(keys):
    with cf.ThreadPoolExecutor(6) as ex:
        for key, code, n in ex.map(plan_one, keys): print(f'{key:12} exit {code} {n} setups planned')

def best_setup(key):
    rows = [list(map(float, l.split())) for l in open(f'{ROOT}/{key}/plan3.txt')]
    return min((r for r in rows if r[4] > 0), key=lambda r: r[4])

PROBLEMS = {
    'rescues': r'\[racemod\] back on the road',
    'falls': r'\[racemod\] fallen off the road',
    'missed': r'\[racemod\] (MISSED checkpoint|missed checkpoint \d+ of \d+: no lap)',
    'edge_holds': r'\[racemod\] the car held at the edge',
    'off_scene': r'\[racemod\] off the road out of scene',
    'loop_fails': r'\[racemod\] loop \d+: off the ring',
    'clock': r'\[racemod\] WARNING: the game clock',
    'no_lap': r'\[racemod\] start line crossed before halfway round',
}

def summarize(run):
    text = open(run + '/run.log', encoding='utf-8', errors='replace').read()
    lines = [l for l in text.splitlines() if l.startswith('[INFO]') or 'CRASH' in l or 'FATAL' in l]
    body = '\n'.join(lines)
    s = {k: len(re.findall(p, body)) for k, p in PROBLEMS.items()}
    s['laps'] = [float(x) for x in re.findall(r'\[racemod\] lap \d+ in ([\d.]+) s', body)]
    m = re.search(r'\[racemod\] (race over after \d+ laps: [^\n]*|sprint over in [^\n]*)', body)
    s['result'] = m.group(1) if m else None
    s['crash'] = 'CRASH' in text or 'Segmentation' in text
    # stuck: Twinsen's car (racecars' "car -1") within 2 cells of where it was 15 s before, the trace every 5 s of game time
    pos = [(float(a), float(b)) for a, b in re.findall(r'\[control\] car -1 (-?\d+) (-?\d+)', text)]
    stuck = [i for i in range(3, len(pos)) if (pos[i][0] - pos[i - 3][0]) ** 2 + (pos[i][1] - pos[i - 3][1]) ** 2 < (2 * 512) ** 2]
    s['stuck_at'] = [stuck[0] * 5, round(pos[stuck[0]][0]), round(pos[stuck[0]][1])] if stuck else None
    s['stuck_s'] = len(stuck) * 5
    m = re.search(r'opponent line of \d+ points planned from the car: a perfect lap ([\d.]+) s', body)
    s['perfect'] = float(m.group(1)) if m else None
    return s

def pilot_one(job):
    key, tag, side, setup = job
    d = f'{ROOT}/{key}'
    t = json.load(open(d + '/track.json'))
    car = 'base.txt'
    if setup != (100, 100, 100, 100):
        car = f'car_{tag}.txt'
        base = open(d + '/base.txt', encoding='utf-8').read()
        open(f'{d}/{car}', 'w', encoding='utf-8').write(base + car_keys(*setup))
    rows = [list(map(float, l.split())) for l in open(d + '/plan.txt')]
    lap = next((r[4] for r in rows if tuple(r[:4]) == tuple(map(float, setup))), 90) or 90
    laps = 1 if t['sprint'] else 3
    ticks = int((laps * lap * 1.9 + 25) / 0.02)
    trace = []
    for k in range(250, ticks, 250): trace += ['--exec-at', str(k), 'racecars']
    code, run = engine(key, ['--exec-at', '90', f'autodrive 1 5 {side}'] + trace, tag, ticks, car)
    s = summarize(run)
    s.update(key=key, tag=tag, side=side, setup=setup, exit=code, ticks=ticks, planned=lap)
    json.dump(s, open(run + '/summary.json', 'w'))
    return s

def pilot(keys):
    jobs = []
    for key in keys:
        b = best_setup(key)
        jobs += [(key, 'base', 0, (100, 100, 100, 100)), (key, 'offline', 1.5, (100, 100, 100, 100)),
                 (key, 'hot', 0, (120, 120, 120, 120)), (key, 'best', 0, tuple(int(x) for x in b[:4]))]
    with cf.ThreadPoolExecutor(int(os.environ.get('PAR', '6'))) as ex:
        for s in ex.map(pilot_one, jobs):
            probs = {k: v for k, v in s.items() if k in PROBLEMS and v}
            print(f"{s['key']:12} {s['tag']:8} exit {s['exit']} laps {s['laps']} {s['result']} {probs} {'CRASH' if s['crash'] else ''}", flush=True)

def trial_files(keys):
    # a clean time trial: Twinsen alone (the one to beat's line his guide), no power-ups
    for key, name, ile, env in TRACKS:
        if key not in keys: continue
        d = f'{ROOT}/{key}'
        e = dict(os.environ, RT_RACE=ile, RT_NO_OPPONENTS='1', **env)
        subprocess.run([SR, 'racecarfile', GAME, d + '/trial_race.txt'], env=e, capture_output=True, text=True)
        text = open(d + '/trial_race.txt', encoding='utf-8').read()
        text = '\n'.join(l for l in text.splitlines() if not re.match(r'(powerups|mushroom|penguin|oil|fake)=', l)) + '\n'
        sprint = re.search(r'^sprint=1', text, re.M) is not None
        laps = '' if sprint or re.search(r'^race_laps=', text, re.M) else 'race_laps=3\n'
        open(d + '/trial.txt', 'w', encoding='utf-8').write(text + car_keys() + 'start_in_car=1\nqualifying=0\n' + laps)

def trial_one(job):
    key, tag, setup = job
    d = f'{ROOT}/{key}'
    t = json.load(open(d + '/track.json'))
    base = open(d + '/trial.txt', encoding='utf-8').read()
    open(f'{d}/car_{tag}.txt', 'w', encoding='utf-8').write(base + car_keys(*setup))
    rows = [list(map(float, l.split())) for l in open(d + '/plan.txt')]
    lap = rows[0][4]
    ticks = int(((1 if t['sprint'] else 3) * lap * 1.6 + 20) / 0.02)
    trace = []
    for k in range(250, ticks, 250): trace += ['--exec-at', str(k), 'racecars']
    code, run = engine(key, ['--exec-at', '90', 'autodrive 1 5 0'] + trace, tag, ticks, f'car_{tag}.txt')
    return key, tag, code

def trial(keys):
    trial_files(keys)
    jobs = []
    for key in keys:
        b = best_setup(key)
        jobs += [(key, 'tbase', (100, 100, 100, 100)), (key, 'tbest', tuple(int(x) for x in b[:4]))]
    with cf.ThreadPoolExecutor(int(os.environ.get('PAR', '6'))) as ex:
        for key, tag, code in ex.map(trial_one, jobs): print(key, tag, code, flush=True)

if __name__ == '__main__':
    what = sys.argv[1]
    keys = sys.argv[2:] or [t[0] for t in TRACKS]
    if what == 'files': files(sys.argv[2:])
    elif what == 'plan': plan(keys)
    elif what == 'pilot': pilot(keys)
    elif what == 'trial': trial(keys)
