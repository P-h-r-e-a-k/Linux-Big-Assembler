# The stress runs read up to the race's end: Twinsen's race laps, his place, and the trouble before the end (rescues, falls, missed
# checkpoints, cube-edge holds, loops failed), where his car stood still for 15 s or more (the racecars trace, every 5 s of game time).
import json, re, os, sys
ROOT = 'E:/dump/TEMP/stress'
keys = [l.split("'")[1] for l in open(ROOT + '/stress.py', encoding='utf-8') if l.strip().startswith("('") and "ILE" in l]
PROBLEMS = {
    'rescue': r'\[racemod\] back on the road',
    'fall': r'\[racemod\] fallen off the road',
    'missed': r'\[racemod\] MISSED checkpoint',
    'nolap': r'\[racemod\] missed checkpoint \d+ of \d+: no lap',
    'edge': r'\[racemod\] the car held at the edge',
    'offscene': r'\[racemod\] off the road out of scene',
    'loopfail': r'\[racemod\] loop \d+: off the ring',
}

def read(run):
    text = open(run + '/run.log', encoding='utf-8', errors='replace').read()
    # (the engine's lines and the console's are written apart, and one can land in the middle of another: each [INFO] on a line of its own)
    text = re.sub(r'(?<=.)\[INFO\]', '\n[INFO]', text)
    lines = text.splitlines()
    out = {'crash': 'CRASH' in text}
    end = len(lines)
    for i, l in enumerate(lines):
        if l.startswith('[INFO]') and re.search(r'\[racemod\] (race over after|sprint over in)', l): end = i; break
    body = [l for l in lines[:end + 1] if l.startswith('[INFO]')]
    b = '\n'.join(body)
    out.update({k: len(re.findall(p, b)) for k, p in PROBLEMS.items()})
    out['laps'] = [float(x) for x in re.findall(r'\[racemod\] lap \d+ in ([\d.]+) s', b)]
    m = re.search(r'\[racemod\] race over after \d+ laps: (\d+)\w\w of (\d+)', b) or re.search(r'\[racemod\] sprint over in ([\d.]+) s: (\d+)\w\w of (\d+)', b)
    out['place'] = None
    if m and 'sprint' in m.group(0): out['place'] = f'{m.group(2)}/{m.group(3)}'; out['sprint'] = float(m.group(1))
    elif m: out['place'] = f'{m.group(1)}/{m.group(2)}'
    out['finished'] = end < len(lines)
    # where the car stood still (before the end, after the start: the trace's racecars "car -1 x z beta")
    pos = []
    tick = 0
    for l in lines[:end + 1]:
        m = re.match(r'\[control\] car -1 (-?\d+) (-?\d+)', l)
        if m: pos.append((int(m.group(1)), int(m.group(2))))
    still = []
    for i in range(3, len(pos)):
        if (pos[i][0] - pos[i - 3][0]) ** 2 + (pos[i][1] - pos[i - 3][1]) ** 2 < (2 * 512) ** 2: still.append(i)
    out['still_s'] = len(still) * 5
    out['still_at'] = [(i * 5, round(pos[i][0] / 512), round(pos[i][1] / 512)) for i in still[:1]]
    return out

rows = {}
for k in keys:
    for tag in ['base', 'offline', 'hot', 'best']:
        run = f'{ROOT}/{k}/{tag}'
        if os.path.exists(run + '/run.log'): rows[(k, tag)] = read(run)
json.dump({f'{k}/{t}': v for (k, t), v in rows.items()}, open(ROOT + '/report.json', 'w'), indent=1)
for (k, tag), r in rows.items():
    probs = ' '.join(f'{p}{r[p]}' for p in PROBLEMS if r[p])
    race = r.get('sprint') and f"sprint {r['sprint']:.1f}" or ' '.join(f'{x:.1f}' for x in r['laps'][:3])
    print(f"{k:12} {tag:8} {'END' if r['finished'] else 'NO END':6} {str(r['place']):5} {race:24} still {r['still_s']:3}s {r['still_at']} {probs} {'CRASH' if r['crash'] else ''}")
