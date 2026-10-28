# How chaotic the races are with the power-ups' rows spaced one way or another (2026-10-17, the user: "Currently items are too close
# together and they make for a chaotic race, let's do some bench marking to move them further apart"). Each variant is a folder of
# ITEMS_ROOT with all 18 tracks built into its game\ (ScriptRoundTrip buildtogether with RT_ITEM_SPACING).
#   python items.py build <variant> [spacing]  the 18 tracks built with that spacing ("40" cells, "8s" seconds; none: the builder's default)
#   python items.py files <variant>           each track's race file: the stress test's base car (stress.py), its opponents and power-ups,
#                                              no qualifying lap, three laps (the sprints one)
#   python items.py run <variant> [keys...]    RUNS races a track, qualifying_seed 1..RUNS (the race's randomness: the same draws for every
#                                              variant), PAR at a time, the test pilot on the racing line and the cars' places every second
#   python items.py report <variant>...        per track and overall: rows, pickups, item hits, rescues, places changed, lap spread
#                                              (and ITEMS_ROOT/<variant>/items_<SCEN>.json)
#   python items.py table <variant>...         those side by side as markdown tables (after report)
# SCEN=pack runs (and reports) the races with every opponent faster than the test pilot, so that he races in the pack and behind it.
import json, os, re, subprocess, sys, math, statistics, concurrent.futures as cf
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stress
from stress import TRACKS, car_keys

ROOT = os.environ.get('ITEMS_ROOT', 'E:/dump/TEMP/itemspacing')
# (the engine and ScriptRoundTrip: stress.py's -- the main tree's builds -- unless given; 2026-10-17's runs used copies in ITEMS_ROOT)
ENG = os.environ.get('ITEMS_ENG', stress.ENG)
SR = os.environ.get('ITEMS_SR', stress.SR)
RUNS = int(os.environ.get('RUNS', '4'))
# SCEN: lead (the opponents' own skills: the test pilot mostly leads from pole) or pack (all of them faster than him); runs r<seed>, p<seed>
SCEN = os.environ.get('SCEN', 'lead')
PREFIX = 'p' if SCEN == 'pack' else 'r'
# each track's planned lap with the base car (stress.py plan: the engine's raceplan), for how long a race may take
PLANNED = {'desert': 50.4, 'storm': 11.1, 'citabau': 39.6, 'mosquibe': 17.5, 'celebration': 17.8, 'lava': 12.2, 'elevator': 14.2,
           'sendell': 4.6, 'moon': 12.9, 'polar': 56.4, 'francos': 48.4, 'otringal': 28.4, 'volcano': 24.7, 'emerald': 25.3,
           'wannies': 23.4, 'palace': 40.3, 'cx': 20.2, 'temple': 55.3}
# the build's track names (RaceTrackIsland), Citadel Island's two tracks apart
NAMES = ['Desert island', 'Citadel Island', 'Citadel Island, storm track', 'Mosquibees Island', 'Celebration Island',
         'Celebration Island, lava lake', 'Elevator Platform', "Sendell's Well", 'The old moon', 'Polar Island', 'Island of the Francos',
         'Otringal', 'Volcano Island', 'The Emerald Moon', 'Wannies Island', "The Emperor's palace", 'Island CX', 'The Temple of B\u00fa']

# the variant's game folder -- a plain copy (never hard links) of ITEMS_SEED's the first time, a full game folder -- with the 18 tracks
# built into it from ITEMS_PRISTINE; the build's log in <variant>/build.log (each track's rows: layout), its files' md5 in files.md5
def build(v, spacing=None):
    import shutil, hashlib, glob
    game = f'{ROOT}/{v}/game'
    if not os.path.exists(game): shutil.copytree(os.environ.get('ITEMS_SEED', 'E:/dump/TEMP/stress/game'), game)
    env = dict(os.environ, RT_LOG='1')
    env.setdefault('LBA1_DIR', r'E:\dump\LBA1Assets\cd\LBA')
    env.pop('RT_ITEM_SPACING', None)
    if spacing: env['RT_ITEM_SPACING'] = spacing
    with open(f'{ROOT}/{v}/build.log', 'w', encoding='utf-8', errors='replace') as log:
        p = subprocess.run([SR, 'buildtogether', os.environ.get('ITEMS_PRISTINE', r'E:\dump\LBA2RaceTrackBuild\Pristine'), game.replace('/', '\\')]
                           + NAMES, env=env, stdout=log, stderr=subprocess.STDOUT)
    hashed = sorted(f for pattern in ['*.ILE', '*.OBL', 'SCENE.HQR', 'HOLOMAP.HQR', 'RACETRACK.JSON', 'RACECARS.JSON', 'BODY.HQR', 'RESS.HQR',
                                      'ANIM.HQR', 'TEXT.HQR', 'OBJFIX.HQR'] for f in glob.glob(f'{game}/{pattern}'))
    with open(f'{ROOT}/{v}/files.md5', 'w', newline='\n') as out:
        for f in hashed: out.write(f'{hashlib.md5(open(f, "rb").read()).hexdigest()} *{os.path.basename(f)}\n')
    print(f'{v}: build exit {p.returncode}, {len(hashed)} files hashed')

def files(v):
    game = f'{ROOT}/{v}/game'
    t = json.load(open(game + '/RACETRACK.JSON', encoding='utf-8'))
    tracks = {}
    for x in [t] + (t.get('Others') or []):
        tracks.setdefault(x['Island'], x)
        if x.get('Twin'): tracks[x['Island'] + '#twin'] = x['Twin']
    for key, name, ile, env in TRACKS:
        d = f'{ROOT}/{v}/{key}'
        os.makedirs(d, exist_ok=True)
        subprocess.run([SR, 'racecarfile', game, d + '/race.txt'], env=dict(os.environ, RT_RACE=ile, **env), capture_output=True, text=True)
        text = open(d + '/race.txt', encoding='utf-8').read()
        sprint = re.search(r'^sprint=1', text, re.M) is not None
        laps = 1 if sprint else int((re.findall(r'^race_laps=(\d+)', text, re.M) or [3])[0])
        extra = '' if sprint or re.search(r'^race_laps=', text, re.M) else 'race_laps=3\n'
        open(d + '/base.txt', 'w', encoding='utf-8').write(text + car_keys() + 'start_in_car=1\nqualifying=0\n' + extra)
        scene = tracks[name + '#twin' if key == 'citabau' else name]['StartScene']
        mush = len(re.findall(r'^mushroom=', text, re.M))
        json.dump({'key': key, 'scene': scene, 'sprint': sprint, 'laps': laps, 'mushrooms': mush}, open(d + '/track.json', 'w'))
        print(f'{key:12} scene {scene:4} laps {laps} mushrooms {mush}')

def run_one(job):
    v, key, seed = job
    d = f'{ROOT}/{v}/{key}'
    t = json.load(open(d + '/track.json'))
    car = f'{d}/{PREFIX}s{seed}.txt'
    text = open(d + '/base.txt', encoding='utf-8').read()
    # (SCEN=pack: every opponent at 96-104 % of the speeds planned -- faster than the test pilot, who starts on pole and races from among
    # them and behind, the others' items coming at him from in front)
    if SCEN == 'pack': text = re.sub(r'^(opponent\d*_pace)=.*$', r'\1=96 104', text, flags=re.M)
    open(car, 'w', encoding='utf-8').write(text + f'qualifying_seed={seed}\n')
    ticks = int((t['laps'] * PLANNED[key] * 1.7 + 25) / 0.02)
    run = f'{d}/{PREFIX}{seed}'
    os.makedirs(run + '/user', exist_ok=True)
    trace = []
    for k in range(100, ticks, 50): trace += ['--exec-at', str(k), 'racecars']
    cmd = [ENG, '--headless', '--no-audio', '--game-dir', f'{ROOT}/{v}/game', '--user-dir', run + '/user', '--no-autosave', '--resolution',
           '640x480', '--fixed-dt', '20', '--exec-at', '4', 'skipmodals 1', '--exec-at', '6', f"cube {t['scene']}", '--exec-at', '90',
           'autodrive 1 5 0'] + trace + ['--tick', str(ticks), '--exit']
    with open(run + '/run.log', 'w', encoding='utf-8', errors='replace') as log:
        p = subprocess.run(cmd, env=dict(os.environ, LBA2_RACETRACK_FILE=car), stdout=log, stderr=subprocess.STDOUT, timeout=3600)
    s = read(run, t)
    s.update(variant=v, key=key, seed=seed, exit=p.returncode)
    json.dump(s, open(run + '/items.json', 'w'))
    return s

def run(v, keys):
    jobs = [(v, k, s) for s in range(1, RUNS + 1) for k in keys]
    with cf.ThreadPoolExecutor(int(os.environ.get('PAR', '8'))) as ex:
        for s in ex.map(run_one, jobs):
            print(f"{s['variant']:6} {s['key']:12} seed {s['seed']} exit {s['exit']} {'END' if s['finished'] else 'NO END'} "
                  f"take {s['tw_take']}/{s['op_take']} hits {s['tw_hit']}/{s['op_hit']} rescues {s['rescues']} laps {s['laps']}", flush=True)

# what an item does to a car (its effect taking hold: stopped, skidding, shrunk, haunted); the hazards' hits are the track's own
NOT_ITEMS = r"(?!a witch|a flying gas monster|an Island CX gun|the Franco gunner)"
BALLS = r"(?:yellow magic ball|green magic ball|red magic ball|fire ball|magic ball)"
TW_HIT = [r"Twinsen's car blown up by a penguin: stopped", r"Twinsen skids on oil$", r"Twinsen's car struck and shrunk by",
          r"Twinsen's car hit by (?:a fake mushroom|" + NOT_ITEMS + r"[^']+'s " + BALLS + r")$", r"'s ghost haunts Twinsen: over his screen"]
OP_HIT = [r"^(?!Twinsen)(.+?) blown up by a penguin: stopped", r"^(?!Twinsen)(.+?) skids on oil$", r"^(?!Twinsen|the other)(.+?)'s car shrunk for",
          r"^(?!Twinsen)(.+?) hit by (?:a fake mushroom|" + NOT_ITEMS + r"[^']+'s " + BALLS + r")$", r"Twinsen's ghost haunts (.+?) for"]
TW_HAZARD = r"Twinsen's car (?:hit by (?:a steam jet|a lava ball|a crystal shard|" + r"(?:a witch|a flying gas monster|an Island CX gun|the Franco gunner)'s)|runs into the shuttle|hits the shuttle)|bitten|frog's mouth shut|knocked aside by a roulette ball"

def lines_of(run):
    text = open(run + '/run.log', encoding='utf-8', errors='replace').read()
    # (the engine's lines and the console's are written apart, and one can land in the middle of another: each on a line of its own)
    text = re.sub(r'(?<=.)\[(INFO|control)\]', r'\n[\1]', text)
    return text.splitlines()

def read(run, t):
    lines = lines_of(run)
    info = [l[len('[INFO] [racemod] '):] for l in lines if l.startswith('[INFO] [racemod] ')]
    # the race: from GO to Twinsen's finish (the storm lap, with no other car, to his last lap)
    go = next((i for i, l in enumerate(info) if l == 'go'), 0)
    end = next((i for i, l in enumerate(info) if i > go and re.match(r'(race over after|sprint over in)', l)), None)
    if end is None: end = next((i for i, l in enumerate(info) if i > go and re.match(rf"lap {t['laps']} in", l)), None)
    finished = end is not None
    race = info[go:(end if end is not None else len(info)) + 1]
    grid = next((l for l in info if l.startswith('grid: ')), '')
    cars = max(1, len(re.findall(r'\d+ [^;]+? [\d.]+ s;', grid)))
    s = {'finished': finished, 'cars': cars}
    # ("(slot N; ..." before the one item box, 2026-10-17; "(0.50 of the way back)" since)
    s['tw_take'] = sum(1 for l in race if re.match(r"Twinsen takes .+? \((slot|[\d.]+ of the way back)", l))
    s['tw_use'] = sum(1 for l in race if re.match(r"Twinsen uses ", l))
    s['op_take'] = sum(1 for l in race if re.search(r" takes a mushroom's power-up: ", l))
    s['tw_hit'] = sum(1 for l in race if any(re.search(p, l) for p in TW_HIT))
    s['op_hit'] = sum(1 for l in race if any(re.search(p, l) for p in OP_HIT))
    s['tw_hazard'] = sum(1 for l in race if re.search(TW_HAZARD, l))
    s['tw_saved'] = sum(1 for l in race if re.search(r"(hits|lands on|strikes) Twinsen's car: |the blast leaves Twinsen's car as it is", l))
    # (a rescue's own line: "back on the road[ in scene n]: line point ..." -- a stuck car's reset says why before it)
    s['rescues'] = sum(1 for l in race if l.startswith('back on the road'))
    s['laps'] = [float(x) for x in re.findall(r'^lap \d+ in ([\d.]+) s', '\n'.join(race), re.M)]
    m = re.search(r'sprint over in ([\d.]+) s', '\n'.join(race))
    if m and not s['laps']: s['laps'] = [float(m.group(1))]
    s['race_s'] = sum(s['laps'][:t['laps']])
    m = re.search(r'(?:race over after \d+ laps|sprint over in [\d.]+ s): (\d+)\w\w of (\d+)', '\n'.join(race))
    s['place'] = int(m.group(1)) if m else None
    # the others' laps, up to Twinsen's finish
    ops = {}
    for l in race:
        m = re.match(r'opponent (\d+): lap \d+ in ([\d.]+) s', l)
        if m: ops.setdefault(int(m.group(1)), []).append(float(m.group(2)))
    s['op_laps'] = ops
    perfect = re.search(r'a perfect lap ([\d.]+) s', '\n'.join(info))
    s['perfect'] = float(perfect.group(1)) if perfect else None
    s.update(places(lines, run, t))
    return s

def places(lines, run, t):
    # the racecars trace: each car's way along the first opponent's line (its nearest point, lap by lap), the cars ranked every second
    race = open(os.path.dirname(run) + '/race.txt', encoding='utf-8').read()
    m = re.search(r'^opponent_path=(.+)$', race, re.M)
    if not m or not os.path.exists(m.group(1).strip()): return {'swaps': None, 'tw_moves': None}
    path = [tuple(map(float, l.split()[:2])) for l in open(m.group(1).strip()) if l.strip()]
    n = len(path)
    # (the line's cells a point: the window a car's next place is looked for in, 8 cells back to 40 on -- a second's driving -- keeps it
    # on its own stretch where the lap passes over or beside itself)
    step = sum(math.dist(path[k], path[k + 1]) for k in range(n - 1)) / (n - 1) / 512
    back, ahead = max(2, round(8 / step)), max(10, round(40 / step))
    samples, cur = [], None
    for l in lines:
        if re.match(r'\[control\] racecars \d+', l): cur = {}; samples.append(cur); continue
        mm = re.match(r'\[control\] car (-?\d+) (-?\d+) (-?\d+) ', l)
        if mm and cur is not None: cur[int(mm.group(1))] = (int(mm.group(2)), int(mm.group(3)))
    last, lap, prog, best = {}, {}, [], {}
    for smp in samples:
        p = {}
        for car, (x, z) in smp.items():
            if car in last:
                i0 = last[car]
                near = range(i0 - back, i0 + ahead) if not t['sprint'] else range(max(0, i0 - back), min(n, i0 + ahead))
            else:
                # (first seen on the grid: behind the start line, where the line begins -- or a lap's line ends)
                near = range(-round(40 / step), round(10 / step)) if not t['sprint'] else range(0, min(n, round(40 / step)))
                lap[car] = 0
            idx = min(near, key=lambda k: (path[k % n][0] - x) ** 2 + (path[k % n][1] - z) ** 2)
            if car not in last and idx < 0: lap[car] = -1
            idx %= n
            if car in last and not t['sprint']:
                if idx < last[car] - n / 2: lap[car] += 1
                elif idx > last[car] + n / 2: lap[car] -= 1
            last[car] = idx
            # (never back: a car over a vertical loop's top goes back along the line's ground track for a moment)
            best[car] = max(best.get(car, -10 ** 9), lap[car] * n + idx)
            p[car] = best[car]
        prog.append(p)
    # from the first second any car has moved to Twinsen's finish (his way round reaching the race's laps)
    goal = n * (t['laps'] if not t['sprint'] else 1) - (5 if t['sprint'] else 0)
    start = next((i for i in range(1, len(prog)) if any(abs(prog[i].get(c, 0) - prog[i - 1].get(c, 0)) > 4 for c in prog[i])), None)
    if start is None: return {'swaps': None, 'tw_moves': None}
    swaps = moves = 0
    seconds = 0
    for i in range(start, len(prog)):
        a, b = prog[i - 1], prog[i]
        if -1 in a and a[-1] >= goal: break
        common = [c for c in a if c in b]
        if len(common) < 2: continue
        seconds += 1
        for x in range(len(common)):
            for y in range(x + 1, len(common)):
                p, q = common[x], common[y]
                if (a[p] - a[q]) * (b[p] - b[q]) < 0:
                    swaps += 1
                    if -1 in (p, q): moves += 1
    return {'swaps': swaps, 'tw_moves': moves, 'trace_s': seconds}

def report(variants):
    rows = {}
    for v in variants:
        for key, *_ in TRACKS:
            d = f'{ROOT}/{v}/{key}'
            if not os.path.exists(d + '/track.json'): continue
            t = json.load(open(d + '/track.json'))
            for seed in range(1, RUNS + 1):
                # (each run read again: the measures can change after the races)
                run = f'{d}/{PREFIX}{seed}'
                if os.path.exists(run + '/items.json'): rows.setdefault((v, key), []).append(read(run, t))
    out = {}
    def mean(xs): xs = [x for x in xs if x is not None]; return sum(xs) / len(xs) if xs else None
    print(f"{'variant':6} {'track':12} {'mush':>4} {'runs':>4} {'end':>3} | per lap: {'tw take':>7} {'op take':>7} {'tw hit':>6} {'op hit':>6} | per car-min: {'hits':>5} | {'rescue':>6} {'swaps/min':>9} {'tw moves':>8} {'lap sd':>6} {'lap/perf':>8}")
    for (v, key), runs in rows.items():
        t = json.load(open(f'{ROOT}/{v}/{key}/track.json'))
        L = t['laps']
        r = {'mushrooms': t['mushrooms'], 'runs': len(runs), 'finished': sum(x['finished'] for x in runs)}
        # per lap: Twinsen's, each opponent's on average (laps: the race's), hits per car and minute of racing (all cars)
        r['tw_take'] = mean(x['tw_take'] / L for x in runs)
        r['op_take'] = mean(x['op_take'] / L / max(1, x['cars'] - 1) for x in runs if x['cars'] > 1)
        r['tw_hit'] = mean(x['tw_hit'] / L for x in runs)
        r['op_hit'] = mean(x['op_hit'] / L / max(1, x['cars'] - 1) for x in runs if x['cars'] > 1)
        r['hits_car_min'] = mean((x['tw_hit'] + x['op_hit']) / x['cars'] / (x['race_s'] / 60) for x in runs if x['race_s'] > 0)
        # ... and a minute of racing: Twinsen's pickups, each opponent's
        r['tw_take_min'] = mean(x['tw_take'] / (x['race_s'] / 60) for x in runs if x['race_s'] > 0)
        r['op_take_min'] = mean(x['op_take'] / max(1, x['cars'] - 1) / (x['race_s'] / 60) for x in runs if x['race_s'] > 0 and x['cars'] > 1)
        r['rescues'] = mean(x['rescues'] for x in runs)
        r['swaps_min'] = mean(x['swaps'] / (x['trace_s'] / 60) for x in runs if x.get('swaps') is not None and x.get('trace_s'))
        r['tw_moves'] = mean(x['tw_moves'] for x in runs if x.get('tw_moves') is not None)
        r['lap_sd'] = mean(statistics.pstdev(x['laps'][1:L]) if L > 2 and len(x['laps']) >= L else None for x in runs)
        r['lap_perf'] = mean(statistics.mean(x['laps'][1:L] if L > 1 else x['laps'][:1]) / x['perfect'] if x['perfect'] and len(x['laps']) >= L else None for x in runs)
        r['tw_hazard'] = mean(x['tw_hazard'] / L for x in runs)
        r['place'] = mean(x['place'] for x in runs)
        out.setdefault(v, {})[key] = r
        f = lambda x, w=7, p=2: f'{x:{w}.{p}f}' if x is not None else ' ' * (w - 1) + '-'
        print(f"{v:6} {key:12} {t['mushrooms']:4} {len(runs):4} {r['finished']:3} | per lap: {f(r['tw_take'])} {f(r['op_take'])} {f(r['tw_hit'], 6)} {f(r['op_hit'], 6)} | per car-min: {f(r['hits_car_min'], 5)} | {f(r['rescues'], 6)} {f(r['swaps_min'], 9)} {f(r['tw_moves'], 8)} {f(r['lap_sd'], 6)} {f(r['lap_perf'], 8)}")
    for v, tracks in out.items():
        keys = [k for k in tracks if k != 'storm']
        agg = {m: mean(tracks[k][m] for k in keys) for m in ['tw_take', 'op_take', 'tw_take_min', 'op_take_min', 'tw_hit', 'op_hit', 'hits_car_min', 'rescues', 'swaps_min', 'tw_moves', 'lap_sd', 'lap_perf']}
        print(f"{v:6} {'ALL (mean)':12} " + ' '.join(f'{m} {x:.2f}' for m, x in agg.items() if x is not None))
        json.dump(tracks, open(f'{ROOT}/{v}/items_{SCEN}.json', 'w'), indent=1)

def layout(v):
    # each track's rows and the lap's driving time they are spaced by, from the build's log ("the power-ups: N mushrooms in R rows ...,
    # a row about every S s of driving (a lap L s)": the tracks in TRACKS's order, one with no mushrooms left out of it)
    found = []
    for l in open(f'{ROOT}/{v}/build.log', encoding='utf-8', errors='replace'):
        m = re.search(r'the power-ups: (\d+) mushrooms in (\d+) rows .*?a lap ([\d.]+) s', l)
        if m: found.append((int(m.group(1)), int(m.group(2)), float(m.group(3))))
    out, k = {}, 0
    for key, *_ in TRACKS:
        t = json.load(open(f'{ROOT}/{v}/{key}/track.json'))
        if t['mushrooms'] == 0: out[key] = (0, None); continue
        if k < len(found) and found[k][0] == t['mushrooms']: out[key] = (found[k][1], found[k][2]); k += 1
        else: out[key] = (None, None)
    return out

# python items.py table <variant>...: the reports' measures side by side, a markdown table each (the variants' items_<SCEN>.json; the rows
# from each variant's build log, or ITEMS_LAYOUT_<variant>=<another variant> for a build that logged none)
def table(variants):
    data = {v: json.load(open(f'{ROOT}/{v}/items_{SCEN}.json')) for v in variants}
    lay = {v: layout(os.environ.get(f'ITEMS_LAYOUT_{v}', v)) for v in variants}
    names = {k: n for k, n, *_ in TRACKS}
    names['storm'] = 'Citadel, storm lap'; names['citabau'] = 'Citadel, town circuit'
    def cell(x, p=2): return '-' if x is None else f'{x:.{p}f}'
    def mean(xs): xs = [x for x in xs if x is not None]; return sum(xs) / len(xs) if xs else None
    print('| track | ' + ' | '.join(f'{v} rows (s apart)' for v in variants) + ' |')
    print('|---|' + '---|' * len(variants))
    for key, *_ in TRACKS:
        print(f'| {names[key]} | ' + ' | '.join(f"{lay[v][key][0]} ({cell(lay[v][key][1] / lay[v][key][0], 1) if lay[v][key][0] else '-'})" if lay[v][key][0] is not None else '?' for v in variants) + ' |')
    for metric, title, p in [('tw_take', "Twinsen's pickups a lap", 1), ('op_take', "each opponent's pickups a lap", 2),
                             ('tw_take_min', "Twinsen's pickups a minute", 1), ('op_take_min', "each opponent's pickups a minute", 1),
                             ('tw_hit', 'item hits on Twinsen a lap', 2), ('op_hit', 'item hits on each opponent a lap', 2),
                             ('hits_car_min', 'item hits a car a minute (all cars)', 2), ('swaps_min', 'places changed a minute', 1),
                             ('tw_moves', "Twinsen's place changes a race", 1), ('lap_sd', "Twinsen's lap times' spread (s)", 2),
                             ('lap_perf', "Twinsen's laps over the planned lap", 2), ('rescues', 'rescues a race', 2)]:
        print(f'\n{title}:\n')
        print('| track | ' + ' | '.join(variants) + ' |')
        print('|---|' + '---|' * len(variants))
        for key, *_ in TRACKS:
            print(f'| {names[key]} | ' + ' | '.join(cell(data[v].get(key, {}).get(metric), p) for v in variants) + ' |')
        print('| **all (mean)** | ' + ' | '.join(cell(mean(data[v][k].get(metric) for k in data[v] if k != 'storm'), p) for v in variants) + ' |')

if __name__ == '__main__':
    what = sys.argv[1]
    if what == 'build': build(sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else None)
    elif what == 'files': files(sys.argv[2])
    elif what == 'run': run(sys.argv[2], sys.argv[3:] or [t[0] for t in TRACKS])
    elif what == 'report': report(sys.argv[2:])
    elif what == 'table': table(sys.argv[2:])
