"""Drives the built storm track (E:\\dump\\TEMP\\rt_cstorm) with the test pilot at a given frame time and reports the jump: when Twinsen is
switched to the flight's movement (move 12, moved by his animation), how long until the flight animation (200) is really playing, and
what animation was holding it off (genanim / flaganim: 2 is ANIM_ALL_THEN, which nothing interrupts); then the flight itself -- how long,
how far and how fast along the ground -- and the car's speed down the far ramp.
    python jumptrace.py <dt ms> [car file] [look cells]
    python jumptrace.py <engine log>      the same report from another track's run (a log with `autodrive` and `objtrace 0`)
The engine still simulates in its own 16 ms steps whatever the clock does (FixedTimestep); SIMSTEP=<ms> in the environment sets that too
(SIMSTEP=0: one step a frame, so the frame time is the simulation's). JT_LOG=<file> keeps the engine's whole log; JT_GAME=<folder> drives another
build of the storm track (one with several tracks); JT_SHOTS=<tick>,...
takes screenshots at those ticks, into E:\\dump\\TEMP\\cstorm\\user_jt_<dt>."""
import re, subprocess, sys, os
if not sys.argv[1].isdigit():
    dt = 0
    out = open(sys.argv[1], errors='replace').read()
else:
    dt = int(sys.argv[1])
    car = sys.argv[2] if len(sys.argv) > 2 else r'E:\dump\TEMP\cstorm\car_storm_auto.txt'
    look = sys.argv[3] if len(sys.argv) > 3 else '5'
    ENG = r'E:\dump\LBAAssembler\native\lba2-classic-community\out\build\windows_ucrt64_static\SOURCES\lba2cc.exe'
    user = r'E:\dump\TEMP\cstorm\user_jt_%d' % dt + os.environ.get('JT_TAG', '')   # (JT_TAG: a folder of its own, for runs side by side)
    os.makedirs(user, exist_ok=True)
    ticks = int(40000 / dt) + 100          # two laps' worth
    env = dict(os.environ, LBA2_RACETRACK_FILE=car)
    args = [ENG, '--headless', '--no-audio', '--game-dir', os.environ.get('JT_GAME', r'E:\dump\TEMP\rt_cstorm'), '--user-dir', user, '--no-autosave', '--resolution', '640x480',
            '--fixed-dt', str(dt), '--exec-at', '4', 'skipmodals 1', '--exec-at', '5', 'vargame 74 3', '--exec-at', '6', 'cube 42',
            '--exec-at', '40', 'teleport 24897 352 2540 4013', '--exec-at', '60', 'input action 5', '--exec-at', '90', 'autodrive 1 ' + look,
            '--exec-at', '91', 'objtrace 0', '--tick', str(ticks), '--exit']
    if os.environ.get('SIMSTEP'): args[1:1] = ['--fixed-timestep', os.environ['SIMSTEP']]
    for t in filter(None, os.environ.get('JT_SHOTS', '').split(',')):   # JT_SHOTS=<tick>,<tick>...: screenshots into the user folder
        args[-3:-3] = ['--exec-at', t, 'screenshot ' + os.path.join(user, f'shot_{int(t):05d}.png')]
    out = subprocess.run(args, capture_output=True, text=True, env=env, timeout=600).stdout
    if os.environ.get('JT_LOG'): open(os.environ['JT_LOG'], 'w').write(out)
rx = re.compile(r'\[obj\] t=(\d+) obj=0 pos=(-?\d+),(-?\d+),(-?\d+) .*? anim=(-?\d+) genanim=(-?\d+) flaganim=(-?\d+) frame=(-?\d+) track=(-?\d+) label=(-?\d+) comport=(-?\d+) move=(-?\d+) flags=(\d+)')
rows = [tuple(int(v) for v in m.groups()) for m in rx.finditer(out)]
seen = set()
FLIGHT = lambda anim: 200 <= anim <= 295   # the flights: each island's and each jump's own (RaceTrackJumpAnim.GenericFor)
rows = sorted(r for r in rows if not (r[0] in seen or seen.add(r[0])))   # (a log with two copies of each line, out of step: each frame once)
laps = list(dict.fromkeys(re.findall(r'\[racemod\] lap (\d+) in ([\d.]+) s', out)))
print(f'dt {dt}: {len(rows)} traced frames; laps {laps}')
i = 0
jumps = 0
while i < len(rows):
    if rows[i][11] == 12:
        start = i
        while i < len(rows) and rows[i][11] == 12: i += 1
        seg = rows[start:i]
        first200 = next((k for k, r in enumerate(seg) if FLIGHT(r[5])), None)
        held = seg[:first200] if first200 is not None else seg
        holders = sorted({(r[5], r[6]) for r in held})
        t0 = seg[0][0]
        print(f'  jump {jumps + 1}: move 12 for {len(seg)} frames from t={t0} at ({seg[0][1]},{seg[0][2]},{seg[0][3]}); '
              f'flight anim after {first200 if first200 is not None else "never"} frames ({(seg[first200][0] - t0) if first200 is not None else "-"} ms); '
              f'held by (genanim, flaganim) {holders}; ends at ({seg[-1][1]},{seg[-1][2]},{seg[-1][3]})')
        # the flight itself: how long, how far along the ground, how high over its start; and the car's speed coming down the far ramp
        flight = [r for r in seg if FLIGHT(r[5])]
        if flight:
            a, b = flight[0], flight[-1]
            ms = b[0] - a[0]
            along = ((b[1] - a[1]) ** 2 + (b[3] - a[3]) ** 2) ** 0.5
            peak = max(r[2] for r in flight) - a[2]
            after = rows[i:i + 40]
            v = ''
            if len(after) > 5:
                # (frame by frame, leaving out a step into the next cube: the positions are the cube's own)
                d = sum(s for s in (((q[1] - p[1]) ** 2 + (q[3] - p[3]) ** 2) ** 0.5 for p, q in zip(after, after[1:])) if s < 4000)
                v = f'; then {d / max(1, after[-1][0] - after[0][0]) * 1000 * 3.6 / 512:.0f} km/h down the ramp'
            print(f'    flight: {len(flight)} frames, {ms} ms, {along / 512:.1f} cells along the ground ({along / max(1, ms) * 1000 * 3.6 / 512:.0f} km/h), '
                  f'{peak} over its start, ends {b[2] - a[2]} from it{v}')
        jumps += 1
    else:
        i += 1
if jumps == 0: print('  no jump started')
for line in re.findall(r'\[racemod\] jump: [^\n]*', out): print('  ' + line.strip())
