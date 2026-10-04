"""Proposal picture: where a longer retail-style buggy jump could go on the Desert island race track."""
import sys, json, math, struct
sys.path.insert(0, r'E:\dump\TEMP')
import numpy as np
from ile import load_ile, palette, hqr_entries
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Polygon as MPoly, FancyArrowPatch, Rectangle
from PIL import Image

BUILT = r'E:\dump\TEMP\rt4_game\DESERT.ILE'
PRISTINE = r'E:\dump\LBA2RaceTrackBuild\Pristine\DESERT.ILE'
RESS = r'E:\dump\TEMP\rt4_game\RESS.HQR'
ANIM = r'E:\dump\TEMP\rt4_game\ANIM.HQR'
PLAN = r'E:\dump\LBAAssembler\docs\racetrack\track_plan.json'
OUT = r'E:\dump\LBAAssembler\docs\racetrack\build\jump_proposal.png'
X0, Z0, NX, NZ = 7, 7, 4, 5
SCALE_LEN, SCALE_H, SCALE_T = 1.3, 1.25, 1.15

pal = palette(RESS, 29).astype(int)
if pal.max() <= 63: pal = pal * 4
pal = pal.clip(0, 255).astype(np.uint8)
SEA = np.array([28, 62, 110], np.uint8)

def load(path):
    m, ground, _, cubes = load_ile(path)
    return m, ground, cubes

def render(m, ground, cubes, x0, z0, x1, z1, S, sea_cells=None):
    """Top-down picture of island cells x0..x1, z0..z1 (exclusive), S px per cell, triangles drawn per half cell, hill-shaded."""
    W, H = (x1 - x0) * S, (z1 - z0) * S
    img = np.zeros((H, W, 3), np.float32); img[:] = SEA
    hg = np.zeros((z1 - z0 + 1, x1 - x0 + 1), np.float32)
    yy, xx = np.mgrid[0:S, 0:S]; u = (xx + 0.5) / S; v = (yy + 0.5) / S
    for gz in range(z0, z1):
        for gx in range(x0, x1):
            c = cubes.get(m[(gz // 64) * 16 + gx // 64] & 127)
            if c is None: continue
            lx, lz = gx % 64, gz % 64
            hg[gz - z0, gx - x0] = c.heights[lz, lx]
            if c.polys is None: continue
            diag = (int(c.polys[lz, lx * 2]) >> 16) & 1
            mask0 = (u < v) if not diag else (u + v < 1)
            for half in range(2):
                w = int(c.polys[lz, lx * 2 + half]); tex = (w >> 4) & 3; poly = (w >> 6) & 3; idx = w >> 19; bank = w & 15
                if sea_cells is not None and (gx, gz) in sea_cells: col = SEA
                elif tex and idx * 6 + 6 <= len(c.texdefs):
                    t = c.texdefs[idx * 6: idx * 6 + 6].astype(int)
                    col = pal[ground[min(255, (t[1] + t[3] + t[5]) // 768), min(255, (t[0] + t[2] + t[4]) // 768)]]
                elif poly: col = pal[(bank << 4) + 11]
                else: col = SEA
                msk = mask0 if half == 0 else ~mask0
                img[(gz - z0) * S:(gz - z0 + 1) * S, (gx - x0) * S:(gx - x0 + 1) * S][msk] = col
    gy, gx_ = np.gradient(hg)
    sh = np.clip(1 + (gx_ * 0.5 - gy * 0.5) / 160.0, 0.65, 1.35)[:z1 - z0, :x1 - x0]
    img *= np.kron(sh, np.ones((S, S)))[..., None]
    return np.clip(img, 0, 255).astype(np.uint8)

def heights(m, cubes):
    hg = np.zeros((NZ * 64 + 1, NX * 64 + 1), np.float32)
    for cz in range(Z0, Z0 + NZ):
        for cx in range(X0, X0 + NX):
            c = cubes.get(m[cz * 16 + cx] & 127)
            if c is None: continue
            hg[(cz - Z0) * 64:(cz - Z0) * 64 + 65, (cx - X0) * 64:(cx - X0) * 64 + 65] = c.heights
    return hg
def sample(hg, x, z):
    gx = x - X0 * 64; gz = z - Z0 * 64; i = int(math.floor(gx)); j = int(math.floor(gz)); fx = gx - i; fz = gz - j
    return hg[j, i] * (1 - fx) * (1 - fz) + hg[j, i + 1] * fx * (1 - fz) + hg[j + 1, i] * (1 - fx) * fz + hg[j + 1, i + 1] * fx * fz
def drawn(m, cubes, x, z):
    gx, gz = int(math.floor(x)), int(math.floor(z)); c = cubes.get(m[(gz // 64) * 16 + gx // 64] & 127)
    if c is None or c.polys is None: return False
    w = int(c.polys[gz % 64, (gx % 64) * 2]); return ((w >> 4) & 3) != 0 or ((w >> 6) & 3) != 0

bm, bground, bcubes = load(BUILT)
pm, pground, pcubes = load(PRISTINE)
bh, ph = heights(bm, bcubes), heights(pm, pcubes)

# the lap (0.5-cell samples)
plan = json.load(open(PLAN))
P = np.array(plan['points'], float) + [plan['originCellX'], plan['originCellZ']]
seg = np.hypot(*np.diff(np.vstack([P, P[:1]]), axis=0).T); cum = np.concatenate([[0], np.cumsum(seg)]); L = cum[-1]
S_ = np.arange(0, L, 0.5); C = np.vstack([P, P[:1]])
R = np.column_stack([np.interp(S_, cum, C[:, 0]), np.interp(S_, cum, C[:, 1])]); n = len(R)
def near(pt): return int(np.argmin(np.hypot(*(R - np.array(pt)).T)))

# the retail flight (ANIM.HQR 51): cumulative forward/up per keyframe
read, _ = hqr_entries(ANIM); a = read(51)
nf, nb = struct.unpack('<2H', a[:4])
zs, ys, ts = [0.0], [0.0], [0.0]
for f in range(nf):
    p = 8 + f * (8 + nb * 8); ft, sx, sy, sz = struct.unpack('<H3h', a[p:p + 8])
    zs.append(zs[-1] + sz); ys.append(ys[-1] + sy); ts.append(ts[-1] + ft)
zs, ys, ts = np.array(zs) / 512, np.array(ys), np.array(ts)
RET_LEN, RET_PEAK, RET_T = zs[-1], ys.max(), ts[-1]
NEW_LEN, NEW_PEAK, NEW_T = RET_LEN * SCALE_LEN, RET_PEAK * SCALE_H, RET_T * SCALE_T

# ---- option A: the harbour. Sea under the lap, centre the flight over it
a0 = near([594.3, 514.9]); a1 = near([578.9, 539.3])
idx = [k % n for k in range(a0 - 40, a1 + 40)]
sea_idx = [k for k in idx if not drawn(pm, pcubes, *R[k])]
s_first, s_last = S_[sea_idx[0]], S_[sea_idx[-1]]
mid = (s_first + s_last) / 2
s_to, s_land = mid - NEW_LEN / 2, mid + NEW_LEN / 2
TO = R[near_s := int(round(s_to / 0.5)) % n]; LAND = R[int(round(s_land / 0.5)) % n]
d = LAND - TO; flight_dir = d / np.hypot(*d)
beta = (math.degrees(math.atan2(flight_dir[0], flight_dir[1])) / 360 * 4096) % 4096
water = s_last - s_first
# the causeway cells to show as open water again: pristine sea cells within 7 cells of the lap between the shores
sea_cells = set()
for k in idx:
    if s_first - 1 <= S_[k] <= s_last + 1:
        cx, cz = R[k]
        for gz in range(int(cz) - 7, int(cz) + 8):
            for gx in range(int(cx) - 7, int(cx) + 8):
                if math.hypot(gx + 0.5 - cx, gz + 0.5 - cz) <= 7 and not drawn(pm, pcubes, gx + 0.5, gz + 0.5): sea_cells.add((gx, gz))

# ---- option B: the dry stretch in the start cube (from jumpsite.py), a canyon dug under the flight
sites = json.load(open(r'E:\dump\TEMP\jumpsites.json'))['sites']
B = next(s for s in sites if s['cube'] == [9, 10])
B_TO, B_LAND = np.array(B['start']), np.array(B['land'])

# ================= figure
fig = plt.figure(figsize=(19, 12.6), dpi=100)
fig.patch.set_facecolor('#F4F7FB')
fig.suptitle('Proposed jump for the Desert island race track: the "harbour leap"  (a longer version of the retail car jump)', fontsize=17, fontweight='bold', x=0.02, ha='left', y=0.985, color='#12324F')

# overview
ax0 = fig.add_axes([0.01, 0.05, 0.40, 0.88])
ov = np.array(Image.open(r'E:\dump\TEMP\rt4_built.png').convert('RGB'))
ax0.imshow(ov, extent=(448, 704, 768, 448))
ax0.set_xlim(448, 704); ax0.set_ylim(768, 448); ax0.set_xticks([]); ax0.set_yticks([])
ax0.set_title('The built lap (top-down, north up)', fontsize=12, loc='left', color='#12324F')
def mark(ax, to, land, colour, label, lw=3):
    ax.add_patch(FancyArrowPatch(to, land, arrowstyle='-|>', mutation_scale=18, lw=lw, color=colour, linestyle='--'))
    ax.annotate(label, xy=((to[0] + land[0]) / 2, (to[1] + land[1]) / 2), xytext=(12, -14), textcoords='offset points', color='white', fontsize=11, fontweight='bold',
                bbox=dict(boxstyle='round,pad=0.3', fc=colour, ec='none'))
mark(ax0, TO, LAND, '#E8A200', 'A  harbour leap (proposed)')
mark(ax0, B_TO, B_LAND, '#7A5AA8', 'B  canyon jump (alternative)', lw=2.5)
ax0.plot(*R[near([605.8, 692.2])], 'w^', ms=10, mec='k'); ax0.annotate('start', R[near([605.8, 692.2])], xytext=(6, 8), textcoords='offset points', fontsize=9, color='k', fontweight='bold')
ax0.plot(617.6, 617.4, 's', ms=10, mfc='none', mec='#D02020', mew=2.5); ax0.annotate('road bridge', (617.6, 617.4), xytext=(8, -12), textcoords='offset points', fontsize=9, color='#D02020', fontweight='bold')
for c in range(448, 705, 64): ax0.axvline(c, color='white', lw=0.4, alpha=0.5)
for c in range(448, 769, 64): ax0.axhline(c, color='white', lw=0.4, alpha=0.5)

# close-up of the harbour
cx0, cz0, cx1, cz1 = 562, 500, 612, 552
Sp = 12
close = render(bm, bground, bcubes, cx0, cz0, cx1, cz1, Sp, sea_cells)
ax1 = fig.add_axes([0.42, 0.40, 0.30, 0.53])
ax1.imshow(close, extent=(cx0, cx1, cz1, cz0), interpolation='nearest')
ax1.set_xlim(cx0, cx1); ax1.set_ylim(cz1, cz0); ax1.set_xticks([]); ax1.set_yticks([])
ax1.set_title(f'A: close-up (cube (9,8), scene 65 "near Esmer Shuttle")', fontsize=12, loc='left', color='#12324F')
# take-off strip: 3 cells deep across the road, starting at the take-off point (the flight starts where the car enters it, as
# RaceTrackBuilder.PlanJump lays it out)
perp = np.array([-flight_dir[1], flight_dir[0]])
strip = [TO + perp * 4.5, TO + flight_dir * 3 + perp * 4.5, TO + flight_dir * 3 - perp * 4.5, TO - perp * 4.5]
ax1.add_patch(MPoly(strip, closed=True, fc=(1, 0.85, 0.1, 0.55), ec='#B07800', lw=2))
ax1.annotate('take-off strip\n(scenario zone, 3 cells)', TO + flight_dir * 1.5, xytext=(-175, 30), textcoords='offset points', fontsize=9.5, color='#5A3C00', fontweight='bold', ha='left',
             bbox=dict(boxstyle='round,pad=0.25', fc=(1, 0.93, 0.6, 0.9), ec='none'), arrowprops=dict(arrowstyle='->', color='#6A4800'))
# the lane that reaches the far quay's top (the quays cross the flight at a slant)
for side in (-1.5, 1.5):
    ax1.plot(*zip(TO + perp * side, LAND + perp * side), color='#E8A200', lw=1.2, ls=':')
ax1.annotate('lands on the quay only\nwithin ~1.5 cells of this line', LAND - flight_dir * 6 - perp * 1.5, xytext=(-190, -40), textcoords='offset points', fontsize=8.5,
             color='#6A4800', fontweight='bold', bbox=dict(boxstyle='round,pad=0.2', fc=(1, 0.93, 0.6, 0.85), ec='none'), arrowprops=dict(arrowstyle='->', color='#6A4800'))
land_box = [LAND - flight_dir * 2 + perp * 3.5, LAND + flight_dir * 3 + perp * 3.5, LAND + flight_dir * 3 - perp * 3.5, LAND - flight_dir * 2 - perp * 3.5]
ax1.add_patch(MPoly(land_box, closed=True, fc=(0.2, 0.8, 0.3, 0.45), ec='#1E7A30', lw=2))
ax1.annotate('landing', LAND + flight_dir * 3.5 + perp * 1, fontsize=9, color='#0E4A1A', fontweight='bold')
ax1.add_patch(FancyArrowPatch(TO, LAND, arrowstyle='-|>', mutation_scale=22, lw=3, color='#E8A200', linestyle='--'))
ret_land = TO + flight_dir * RET_LEN
ax1.plot(*ret_land, 'x', color='#D02020', ms=13, mew=3)
ax1.annotate(f'retail length ({RET_LEN:.1f} cells)\nwould land here, in the sea', ret_land, xytext=(-150, 20), textcoords='offset points', fontsize=9, color='#D02020', fontweight='bold',
             arrowprops=dict(arrowstyle='->', color='#D02020'))
ax1.annotate(f'causeway (water bridge) removed:\n{water:.0f} cells of open water', (TO + LAND) / 2 + perp * 6, fontsize=9.5, color='white', fontweight='bold', ha='center',
             bbox=dict(boxstyle='round,pad=0.3', fc='#1F4E80', ec='none', alpha=0.85))

# side profile along the flight line
ax2 = fig.add_axes([0.45, 0.06, 0.53, 0.29])
fs = np.linspace(-10, NEW_LEN + 10, 400)
pts = [TO + flight_dir * f for f in fs]
nat = np.array([sample(ph, *p) if drawn(pm, pcubes, *p) else np.nan for p in pts])
built = np.array([sample(bh, *p) for p in pts])
# the take-off, on the same (natural) ground as the rest of the view: the approach raised so the flight, which ends 251 below its
# start, comes down on the far quay's top
y_land = sample(ph, *LAND)
y_to = y_land - ys[-1] * SCALE_H
ax2.axhspan(-400, 0, color='#3A6EA5', alpha=0.9); ax2.text(NEW_LEN / 2, -250, 'sea', color='white', ha='center', fontsize=10, fontweight='bold')
ax2.fill_between(fs, -400, np.nan_to_num(nat, nan=-400), where=~np.isnan(nat), color='#C8A878', alpha=0.9, label='natural ground (retail)')
causeway = np.where(np.isnan(nat), built, np.nan)
ax2.plot(fs, causeway, color='#666', lw=2, ls=':', label='today\'s causeway (removed)')
ax2.plot([fs[0], -6, 0], [nat[0], nat[np.searchsorted(fs, -6)], y_to], color='#8A5A20', lw=2, ls='--', label=f'approach raised {y_to - y_land:.0f} for the take-off')
ax2.plot(zs * SCALE_LEN, y_to + ys * SCALE_H, color='#E8A200', lw=3.2, label=f'proposed flight: {NEW_LEN:.1f} cells, peak +{NEW_PEAK:.0f}')
ax2.plot(zs, y_to + ys, color='#D02020', lw=2, ls='--', label=f'retail flight: {RET_LEN:.1f} cells, peak +{RET_PEAK:.0f}')
ax2.plot([0], [y_to], 'o', color='#B07800', ms=8); ax2.plot([NEW_LEN], [y_to + ys[-1] * SCALE_H], 'o', color='#1E7A30', ms=8)
ax2.set_xlim(fs[0], fs[-1]); ax2.set_ylim(-400, y_to + NEW_PEAK + 700)
ax2.set_xlabel('cells along the flight (0 = take-off)'); ax2.set_ylabel('height (game units)')
ax2.set_title('A: side view along the flight line', fontsize=12, loc='left', color='#12324F')
ax2.legend(loc='upper right', fontsize=9, framealpha=0.9); ax2.grid(alpha=0.3)

# the facts
txt = (
    f'How it works (as the retail car jump, scene 62)\n'
    f'- A scripted flight, not physics. In the take-off\n'
    f'  strip, heading within ~55 deg of the road, a small\n'
    f'  controller actor switches Twinsen to MOVE_BUGGY\n'
    f'  and plays a flight animation; at its end the\n'
    f'  player drives on.\n'
    f'- Longer: a new ANIM.HQR entry, a copy of entry 51\n'
    f'  with the forward steps x{SCALE_LEN}, the climb x{SCALE_H} and\n'
    f'  the timing x{SCALE_T}:\n'
    f'     length  {RET_LEN:.1f} -> {NEW_LEN:.1f} cells ({RET_LEN*512:.0f} -> {NEW_LEN*512:.0f})\n'
    f'     peak    +{RET_PEAK:.0f} -> +{NEW_PEAK:.0f}\n'
    f'     time    {RET_T/1000:.2f} s -> {NEW_T/1000:.2f} s\n'
    f'  Given to Twinsen as a new animation number, so\n'
    f'  the retail jump in scene 62 stays as it is.\n'
    f'\nA: harbour leap (proposed)\n'
    f'- take-off ({TO[0]:.1f}, {TO[1]:.1f}), landing ({LAND[0]:.1f}, {LAND[1]:.1f})\n'
    f'- {water:.0f} cells of open water; on the centre line 1.7\n'
    f'  cells of quay before it and 0.8 of the far quay\'s top\n'
    f'  to spare. The quays cross the flight at a slant, so\n'
    f'  the strip is narrowed to the middle 3 cells (rock\n'
    f'  walls), or the far quay widened\n'
    f'- the approach is raised ~250: the flight ends 251\n'
    f'  below its start, on the far quay; all in one cube\n'
    f'- the causeway goes; a miss (on foot, or reversing)\n'
    f'  ends in the sea -- keep a footbridge beside the\n'
    f'  flight line, or keep the causeway and fly alongside\n'
    f'\nB: canyon jump (alternative, start cube)\n'
    f'- ({B_TO[0]:.1f}, {B_TO[1]:.1f}) -> ({B_LAND[0]:.1f}, {B_LAND[1]:.1f}), heading east,\n'
    f'  landing {B["drop"]:.0f} lower, road already nearly straight;\n'
    f'  a gap dug across the road under the flight.\n'
    f'  Safer, less spectacular.')
fig.text(0.745, 0.925, txt, fontsize=10, va='top', ha='left', family='DejaVu Sans', color='#1B2B3A',
         bbox=dict(boxstyle='round,pad=0.6', fc='white', ec='#B8C8DA'))
fig.savefig(OUT, dpi=100, facecolor=fig.get_facecolor())
print('saved', OUT)
print(f'A: water {water:.1f} cells (s {s_first:.1f}..{s_last:.1f}), take-off {TO} landing {LAND} dir {flight_dir} beta {beta:.0f} take-off Y {y_to:.0f}')
print(f'retail {RET_LEN:.2f} cells peak {RET_PEAK} {RET_T} ms; proposed {NEW_LEN:.2f} cells peak {NEW_PEAK:.0f} {NEW_T:.0f} ms')
