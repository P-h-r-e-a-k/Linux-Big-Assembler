"""Find candidate spots on the built Desert race track for a scripted buggy jump like the retail one (ANIM.HQR 51), but longer.

Writes E:/dump/TEMP/jumpsites.json (ranked candidates) and prints them."""
import sys, json, math
sys.path.insert(0, r'E:\dump\TEMP')
import numpy as np
from ile import load_ile

BUILT = r'E:\dump\TEMP\rt4_game\DESERT.ILE'
PRISTINE = r'E:\dump\LBA2RaceTrackBuild\Pristine\DESERT.ILE'
PLAN = r'E:\dump\LBAAssembler\docs\racetrack\track_plan.json'
X0, Z0, NX, NZ = 7, 7, 4, 5

SCALE_LEN, SCALE_H = 1.3, 1.25          # the proposed jump: 30% further, 25% higher
RETAIL_LEN = 8990 / 512                 # 17.56 cells of steps (measured in game: 17.5)
FLIGHT = RETAIL_LEN * SCALE_LEN          # ~22.8 cells
RUNUP, AFTER = 12, 10                    # straight road wanted before take-off and after landing (cells)

def grid(path):
    m, _, _, cubes = load_ile(path)
    hg = np.zeros((NZ * 64 + 1, NX * 64 + 1), np.float32)
    for cz in range(Z0, Z0 + NZ):
        for cx in range(X0, X0 + NX):
            c = cubes.get(m[cz * 16 + cx] & 127)
            if c is None: continue
            ox = (cx - X0) * 64; oz = (cz - Z0) * 64
            hg[oz:oz + 65, ox:ox + 65] = c.heights
    return hg

def sample(hg, x, z):
    gx = x - X0 * 64; gz = z - Z0 * 64
    i = int(math.floor(gx)); j = int(math.floor(gz))
    i = min(max(i, 0), hg.shape[1] - 2); j = min(max(j, 0), hg.shape[0] - 2)
    fx = gx - i; fz = gz - j
    return (hg[j, i] * (1 - fx) * (1 - fz) + hg[j, i + 1] * fx * (1 - fz) + hg[j + 1, i] * (1 - fx) * fz + hg[j + 1, i + 1] * fx * fz)

built = grid(BUILT); pristine = grid(PRISTINE)
plan = json.load(open(PLAN))
P = np.array(plan['points'], float) + [plan['originCellX'], plan['originCellZ']]
# resample the closed centre line every 0.5 cells
seg = np.hypot(*np.diff(np.vstack([P, P[:1]]), axis=0).T)
cum = np.concatenate([[0], np.cumsum(seg)]); L = cum[-1]
S = np.arange(0, L, 0.5)
C = np.vstack([P, P[:1]])
R = np.column_stack([np.interp(S, cum, C[:, 0]), np.interp(S, cum, C[:, 1])])
n = len(R)
def at(i): return i % n
def heading(i, span=6):
    a = R[at(i - span)]; b = R[at(i + span)]; d = b - a; return d / np.hypot(*d)
H = np.array([sample(built, x, z) for x, z in R])

# places to keep away from (arc-length windows, cells)
def nearest(pt):
    return int(np.argmin(np.hypot(*(R - pt).T)))
cross = np.array([617.6, 617.4])
d = np.hypot(*(R - cross).T)
near_cross = np.where(d < 3)[0]
avoid = []
for k in near_cross: avoid.append((S[k], 95))                   # both roads at the bridge: deck 34 + landing 2 + ramp 50 + margin
avoid.append((S[nearest([605.8, 692.2])], 35))                 # start line and gantry
for p in (plan['pitA'], plan['pitB']):
    avoid.append((S[nearest(np.array(p) + [plan['originCellX'], plan['originCellZ']])], 20))   # pit lane junctions
for p in ([594.3, 514.9], [578.9, 539.3]): avoid.append((S[nearest(np.array(p))], 25))   # the water bridge
def avoided(s):
    for c, w in avoid:
        dd = abs(s - c); dd = min(dd, L - dd)
        if dd < w: return True
    return False

cands = []
for i in range(n):
    s0 = S[i]
    t = heading(i - int(RUNUP), span=int(RUNUP))                 # the run-up's heading: the flight goes this way
    start = R[i]; land = start + t * FLIGHT
    j = nearest(land)
    lateral = float(np.hypot(*(R[j] - land)))
    arc = (S[j] - s0) % L
    if lateral > 2.5 or not (FLIGHT - 2.5 < arc < FLIGHT + 4.5): continue
    # straight enough: the road's own heading along the run-up, the flight and past the landing
    hs = [heading(k, 2) for k in range(i - 2 * RUNUP, i + int(2 * (FLIGHT + AFTER)), 4)]
    worst = max(math.degrees(math.acos(max(-1, min(1, float(h @ t))))) for h in hs)
    if worst > 25: continue
    # all within one cube
    pts = [start + t * f for f in np.linspace(-3, FLIGHT + 2, 40)]
    cubes = {(int(p[0] // 64), int(p[1] // 64)) for p in pts}
    if len(cubes) != 1: continue
    if any(avoided(S[at(k)]) for k in range(i - 2 * RUNUP, i + int(2 * (FLIGHT + AFTER)), 4)): continue
    # the ground under the flight (natural = pristine; road = built)
    fs = np.linspace(0, FLIGHT, 60)
    line = [H[i] + (H[j] - H[i]) * f / FLIGHT for f in fs]
    nat = [sample(pristine, *(start + t * f)) for f in fs]
    road = [sample(built, *(start + t * f)) for f in fs]
    mid = slice(10, 50)
    dip_nat = max(l - g for l, g in zip(line[mid], nat[mid]))
    drop = H[i] - H[j]
    cands.append(dict(i=i, s=float(s0), start=start.tolist(), land=land.tolist(), dir=t.tolist(), cube=list(cubes)[0],
                      lateral=lateral, worstTurn=worst, takeoffY=float(H[i]), landY=float(H[j]), drop=float(drop),
                      dipNatural=float(dip_nat), roadProfile=[float(v) for v in road], naturalProfile=[float(v) for v in nat]))

# keep the best of each stretch (candidates within 15 cells of arc are the same site)
cands.sort(key=lambda c: -(min(c['dipNatural'], 2500) - 2 * abs(c['drop'] - 250) - 30 * c["worstTurn"] - 150 * c["lateral"]))
sites = []
for c in cands:
    if all(min(abs(c['s'] - o['s']), L - abs(c['s'] - o['s'])) > 15 for o in sites): sites.append(c)
for k, c in enumerate(sites[:8]):
    print(f"#{k+1} take-off cell ({c['start'][0]:.1f},{c['start'][1]:.1f}) -> land ({c['land'][0]:.1f},{c['land'][1]:.1f}) cube {c['cube']} "
          f"heading ({c['dir'][0]:.2f},{c['dir'][1]:.2f}) worst turn {c['worstTurn']:.1f} deg, lateral {c['lateral']:.2f}, "
          f"take-off Y {c['takeoffY']:.0f} land Y {c['landY']:.0f} (drop {c['drop']:.0f}), natural dip under the flight {c['dipNatural']:.0f}, lap s {c['s']:.0f}")
json.dump(dict(flight=FLIGHT, scaleLen=SCALE_LEN, scaleH=SCALE_H, lapLength=L, sites=sites[:8]), open(r'E:\dump\TEMP\jumpsites.json', 'w'), indent=1)
print(len(cands), 'raw candidates,', len(sites), 'sites')
