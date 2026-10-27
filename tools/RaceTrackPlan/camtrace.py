"""The camera along a lap, from the engine's camtrace lines ([cam] ... eye x,y,z look x,y,z hero x,y,z cube cx,cz own n who s):
per frame, in world units -- how far the eye moved, how sharply (its acceleration), how far the view turned, where the car is in the
picture (degrees off the view's middle, across and up), how far the eye is from the car, and which camera placed it. Prints the worst
moments and a summary by camera; with a plan, where along the lap each is (its nearest point); with RINGS=<car file>, the frames with
one of its loops' rings between the eye and the car. Writes <run.log>.npz with the per-frame numbers.

The engine writes the lines after the console's `camtrace 1` (FOLLOWCAM.CPP), each with the run's tick (CONTROL.CPP's tick hook).

Usage: [RINGS=<car file>] camtrace.py <run.log> [plan.json] [first frame tick]"""
import json, math, re, sys
import numpy as np

LOG = sys.argv[1]
PLAN = sys.argv[2] if len(sys.argv) > 2 else None
TICK0 = int(sys.argv[3]) if len(sys.argv) > 3 else 85
rt = re.compile(r'\[cam\] tick (-?\d+)')
rx = re.compile(r'\[cam\].*?alpha\s+(-?\d+) dist\s+(-?\d+) eye (-?\d+),(-?\d+),(-?\d+) look (-?\d+),(-?\d+),(-?\d+) hero (-?\d+),(-?\d+),(-?\d+) cube (-?\d+),(-?\d+) own (\d+) who (\w+)')
rows = []
for line in open(LOG, encoding='utf-8', errors='replace'):
    if not line.startswith('[INFO] [cam]'): continue   # (not the copies the control socket echoes, '[cam]' and '[control] ...': out of order)
    m = rx.search(line)
    if not m: continue
    a, dist, ex, ey, ez, lx, ly, lz, hx, hy, hz, cx, cz, own = map(int, m.groups()[:14])
    who = m.group(15)
    ox, oz = cx * 32768, cz * 32768
    mt = rt.search(line)
    rows.append((ex + ox, ey, ez + oz, lx + ox, ly, lz + oz, hx + ox, hy, hz + oz, own, who, cx, cz, int(mt.group(1)) if mt else -1))
if not rows: print('no [cam] lines'); sys.exit(1)
E = np.array([r[0:3] for r in rows], float); Lk = np.array([r[3:6] for r in rows], float); Hh = np.array([r[6:9] for r in rows], float)
who = [r[10] for r in rows]; own = np.array([r[9] for r in rows]); ticks = np.array([r[13] for r in rows])
n = len(rows)
print(f'{n} frames traced')

dE = np.r_[0, np.linalg.norm(np.diff(E, axis=0), axis=1)]
dH = np.r_[0, np.linalg.norm(np.diff(Hh, axis=0), axis=1)]
acc = np.r_[0, 0, np.linalg.norm(np.diff(E, 2, axis=0), axis=1)]
V = Lk - E
Vn = V / np.maximum(1e-6, np.linalg.norm(V, axis=1))[:, None]
turn = np.r_[0, np.degrees(np.arccos(np.clip((Vn[1:] * Vn[:-1]).sum(1), -1, 1)))]
# (the car in the picture: its direction from the eye against the view's, across -- yaw -- and up -- pitch)
C = Hh + np.array([0, 300, 0]) - E
def yaw(v): return np.degrees(np.arctan2(v[:, 0], v[:, 2]))
def pitch(v): return np.degrees(np.arctan2(v[:, 1], np.hypot(v[:, 0], v[:, 2])))
dyaw = (yaw(C) - yaw(V) + 180) % 360 - 180
dpitch = pitch(C) - pitch(V)
dist = np.linalg.norm(C, axis=1)

plan = None
if PLAN:
    p = json.load(open(PLAN))
    P = (np.array(p['points']) + [p.get('originCellX', 0), p.get('originCellZ', 0)]) * 512
    def where(i):
        d = np.hypot(P[:, 0] - Hh[i, 0], P[:, 1] - Hh[i, 2]); k = int(np.argmin(d)); return k
else:
    def where(i): return -1

def T(i): return int(ticks[i]) if ticks[i] >= 0 else TICK0 + i

def show(title, idx, val, fmt):
    print(title)
    shown = []
    for i in idx:
        if any(abs(i - j) < 25 for j in shown): continue
        shown.append(i)
        print(f'  tick {T(i):5d} point {where(i):5d} {who[i]:6s} own {own[i]:2d} ' + fmt.format(val[i]) +
              f'  eye-step {dE[i]:6.0f} car-step {dH[i]:5.0f} turn {turn[i]:5.1f} car at {dyaw[i]:6.1f},{dpitch[i]:6.1f} deg, {dist[i]:6.0f} away')
        if len(shown) >= 12: break

jump = np.where(dE > np.maximum(3 * dH, 400))[0]
show(f'eye jumps (moved over 3x the car and over 400 in a frame): {len(jump)} frames', jump[np.argsort(-dE[jump])], dE, 'step {:6.0f}')
sharp = np.where(acc > 250)[0]
show(f'sharp eye changes (acceleration over 250 a frame^2): {len(sharp)} frames', sharp[np.argsort(-acc[sharp])], acc, 'acc {:6.0f}')
swing = np.where(turn > 2.5)[0]
show(f'view swings (over 2.5 degrees a frame): {len(swing)} frames', swing[np.argsort(-turn[swing])], turn, 'turn {:5.1f}')
out = np.where((np.abs(dyaw) > 20) | (dpitch > 12) | (dpitch < -17))[0]
show(f'the car far off the middle (over 20 across, 12 up, 17 down): {len(out)} frames', out[np.argsort(-(np.abs(dyaw[out]) + np.abs(dpitch[out])))], np.abs(dyaw) + np.abs(dpitch), 'off {:5.1f}')
near = np.where(dist < 2600)[0]
show(f'the eye nearer the car than 2,600 (the near plane 3,000): {len(near)} frames', near[np.argsort(dist[near])], dist, 'dist {:6.0f}')
# (a loop's band between the eye and the car: RINGS=<car file> gives the rings, its loop= lines -- cube x z, X Y Z, way, R, Shift,
# Gap, Band; the band is the road's surface at R from the ring's middle, its shell 160 outside it, its rails 150 high inside it)
import os
hid = np.zeros(n)
if os.environ.get('RINGS'):
    rings = []
    for line in open(os.environ['RINGS']):
        if not line.startswith('loop='): continue
        v = [float(x) for x in line[5:].split()]
        dx, dz = v[5], v[6]; ln = math.hypot(dx, dz)
        rings.append((v[0] * 32768 + v[2], v[3], v[1] * 32768 + v[4], dx / ln, dz / ln, v[7], v[8], v[9], v[10]))
    s = np.linspace(0, 1, 240)[:, None]
    for i in range(n):
        tgt = Hh[i] + [0, 300, 0]
        # (a car riding a ring: its middle is 300 towards the ring's middle, not 300 up)
        for (X, Y, Z, DX, DZ, R, SH, GAP, BAND) in rings:
            c = Hh[i] - [X, Y + R, Z]
            al, up = c[0] * DX + c[2] * DZ, c[1]
            if abs(math.hypot(al, up) - R) < 500 and abs(al) < R + 500 and up > -R + 200:
                k = math.hypot(al, up); tgt = Hh[i] - np.array([DX * al, up, DZ * al]) / k * 300; break
        seg = tgt + (E[i] - tgt) * s
        keep = np.linalg.norm(seg - tgt, axis=1) > 500
        for (X, Y, Z, DX, DZ, R, SH, GAP, BAND) in rings:
            rel = seg - [X, Y + R, Z]
            al = rel[:, 0] * DX + rel[:, 2] * DZ; ac = -rel[:, 0] * DZ + rel[:, 2] * DX; up = rel[:, 1]
            rho = np.hypot(al, up); th = np.arctan2(al, -up) % (2 * math.pi)
            off = np.abs(ac - (-SH / 2 + SH * th / (2 * math.pi)))
            # (RaceTrackLoopBody's profile: the road surface at R, the green shell 160 outside it; the rails 90 wide past the band's edge,
            # 150 high towards the middle -- 40 to spare all round)
            slab = (rho > R - 40) & (rho < R + 200) & (off < BAND + 40)
            rail = (rho > R - 190) & (rho < R + 200) & (off > BAND - 40) & (off < BAND + 130)
            if (keep & (slab | rail)).any(): hid[i] = 1; break
    hx = np.where(hid > 0)[0]
    show(f'a ring\'s band between the eye and the car: {len(hx)} frames', hx, hid, 'hid {:3.0f}')
    for w in sorted(set(who)):
        k = np.array([x == w for x in who])
        print(f'  {w:6s} {int(hid[k].sum()):5d} of {k.sum()} frames hidden')
    runs, i = [], 0
    while i < n:
        if hid[i]:
            j = i
            while j + 1 < n and hid[j + 1]: j += 1
            runs.append((T(i), T(j), who[i], who[j])); i = j + 1
        else: i += 1
    print('  hidden stretches (ticks, camera at each end): ' + '; '.join(f'{a}-{b} {w0}/{w1}' for a, b, w0, w1 in runs))
# handovers: who changes
print('handovers:')
for i in range(1, n):
    if who[i] != who[i - 1]:
        print(f'  tick {T(i):5d} point {where(i):5d} {who[i - 1]} -> {who[i]}  eye-step {dE[i]:6.0f} turn {turn[i]:5.1f} (next 5: {", ".join(f"{dE[j]:.0f}" for j in range(i, min(n, i + 5)))})')
print('by camera:')
for w in sorted(set(who)):
    k = np.array([x == w for x in who])
    print(f'  {w:6s} {k.sum():5d} frames: eye-step mean {dE[k].mean():6.0f} max {dE[k].max():6.0f}; turn mean {turn[k].mean():4.2f} max {turn[k].max():5.1f}; '
          f'car across {np.abs(dyaw[k]).mean():4.1f} (max {np.abs(dyaw[k]).max():5.1f}), up {dpitch[k].mean():5.1f} ({dpitch[k].min():5.1f}..{dpitch[k].max():5.1f}); dist {dist[k].mean():6.0f} ({dist[k].min():6.0f}..{dist[k].max():6.0f})')
# (per engine frame: a traced line now and then covers two -- the car moved twice its usual step)
med = np.median(dH[dH > 10]) if (dH > 10).any() else 1
frames = np.maximum(1, np.round(dH / med))
print('per engine frame:')
for w in sorted(set(who)):
    k = np.array([x == w for x in who])
    per, tp = dE[k] / frames[k], turn[k] / frames[k]
    idx = np.where(k)[0]
    top = idx[np.argsort(-per)[:3]]
    print(f'  {w:6s} eye step max {per.max():6.0f} (99th {np.percentile(per, 99):6.0f}), turn max {tp.max():4.1f} (99th {np.percentile(tp, 99):4.1f}); worst at ticks {", ".join(str(T(i)) + " point " + str(where(i)) for i in top)}')
np.savez(LOG + '.npz', ticks=ticks, E=E, L=Lk, H=Hh, own=own, who=np.array(who), dE=dE, acc=acc, turn=turn, dyaw=dyaw, dpitch=dpitch, dist=dist)
