"""The Emperor's palace from above, inside (the user, 2026-10-13: "Create a top down image of the inside of the palace track"): the
rooms as a plan -- the built island's roof and walls cut away CUT over the floor (ScriptRoundTrip islandmesh --caps: the solid boxes
through that height dark; meshshot --top --clip-y --cut: what is over it left out), the outside left out, turned a quarter round as the
user's own drawing of the roof was (north to the left, east up) -- and a copy with the race on it: the test pilot's line, the opponents'
other ways, the start, the guards, the pictures on the walls (ScriptRoundTrip palacepictures), the secret warp, the rooms' numbers.

Usage: palace_topdown.py <game folder with the track built: PALACE.ILE, PALACE.OBL, PALACE.JSON, RESS.HQR> <out folder> [plan]
(ScriptRoundTrip built in Release first.) Writes inside_top.png and inside_top_labelled.png.
"""
import json, math, os, subprocess, sys
import numpy as np
from PIL import Image
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patheffects as pe
from matplotlib.patches import FancyArrowPatch, Polygon, Rectangle, Circle

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, '..', '..'))
SRT = os.path.join(REPO, 'tools', 'ScriptRoundTrip', 'bin', 'Release', 'net10.0', 'ScriptRoundTrip.exe')
GAME, OUT = sys.argv[1], sys.argv[2]
PLAN = sys.argv[3] if len(sys.argv) > 3 else os.path.join(REPO, 'docs', 'racetrack', 'palace_track_plan.json')
L = json.load(open(os.path.join(GAME, 'PALACE.JSON')))
P = json.load(open(PLAN))
FLOOR = L['floorTop']
CUT = FLOOR + 1600                          # the cut: over the furniture a car passes, under the doors' tops
PX = 8                                      # pixels a cell
X0, Z0, X1, Z1 = 334, 316, 584, 752         # the cells drawn (the rooms x 345..568, z 337..740, and room round them for the words)
BACK = (25, 30, 40)                         # meshshot's background
W0, H0 = (X1 - X0) * PX + 20, (Z1 - Z0) * PX + 20
os.makedirs(OUT, exist_ok=True)


def run(*args):
    r = subprocess.run([SRT, *map(str, args)], capture_output=True, text=True)
    if r.returncode != 0: sys.exit(f'{args[0]} failed: {r.stdout}{r.stderr}')
    return r.stdout


# ---- the plan from above (x to the right, z down), cut, then the outside left out and turned: north to the left, east up
mesh, shot = os.path.join(OUT, '_mesh.bin'), os.path.join(OUT, '_shot.png')
box = [X0 * 512, Z0 * 512, X1 * 512, Z1 * 512]
run('islandmesh', os.path.join(GAME, 'PALACE.ILE'), os.path.join(GAME, 'PALACE.OBL'), 31, mesh, *box, '--no-terrain', '--caps', CUT)
run('meshshot', mesh, shot, '--top', '--box', *box, '--size', W0, H0, '--clip-y', CUT, '--cut')
pictures = [line.split(';') for line in run('palacepictures', os.path.join(GAME, 'PALACE.ILE'), os.path.join(GAME, 'PALACE.OBL')).splitlines() if ';' in line]
os.remove(mesh)
img = np.array(Image.open(shot).convert('RGB'))
os.remove(shot)
inside = np.zeros((H0, W0), bool)
cx = X0 + (np.arange(W0) + 0.5 - 10) / PX
cz = Z0 + (np.arange(H0) + 0.5 - 10) / PX
for r in L['rooms']:
    inside |= ((cz[:, None] >= r['z0'] - 1) & (cz[:, None] < r['z1'] + 2)) & ((cx[None, :] >= r['x0'] - 1) & (cx[None, :] < r['x1'] + 2))
img[~inside] = BACK
img = np.rot90(img)                         # (a quarter round to the left: x up, z to the right)
H, W = img.shape[:2]


def U(z): return (z - Z0) * PX + 10 - 0.5
def V(x): return W0 - ((x - X0) * PX + 10) - 0.5
def at(x, z): return U(z), V(x)
def way(dx, dz): return dz, -dx            # a way in the world (x, z) as one in the picture


OUTLINE = [pe.withStroke(linewidth=3.2, foreground=(0.05, 0.05, 0.08))]
plt.rcParams['font.family'] = ['Segoe UI', 'DejaVu Sans']


def figure():
    fig = plt.figure(figsize=(W / 100, H / 100), dpi=100)
    ax = fig.add_axes([0, 0, 1, 1])
    ax.imshow(img, interpolation='nearest')
    ax.set_xlim(-0.5, W - 0.5); ax.set_ylim(H - 0.5, -0.5); ax.axis('off')
    return fig, ax


def compass(ax, u, v):
    """North to the left, east up: the arrows and their letters round (u, v)."""
    r = 70
    for (du, dv), name, colour in [((-1, 0), 'N', '#ffd84d'), ((0, -1), 'E', 'white'), ((1, 0), 'S', 'white'), ((0, 1), 'W', 'white')]:
        ax.add_patch(FancyArrowPatch((u, v), (u + du * r, v + dv * r), arrowstyle='-|>', mutation_scale=22, lw=2.5, color=colour,
                                     path_effects=[pe.withStroke(linewidth=5, foreground='black')]))
        ax.text(u + du * (r + 22), v + dv * (r + 22), name, ha='center', va='center', fontsize=17, fontweight='bold', color=colour, path_effects=OUTLINE)


def title(ax):
    ax.text(40, 36, "The Emperor's palace: the inside, from above", fontsize=30, fontweight='bold', color='white', va='top', path_effects=OUTLINE)
    ax.text(42, 96, f"The roof and the walls cut away {CUT - FLOOR:,} over the floor (the doors' tops are higher, so the doorways show as gaps).\n"
                    "North is to the left and east is up, as in your drawing of the roof route.",
            fontsize=15, color=(0.86, 0.88, 0.92), va='top', linespacing=1.5, path_effects=OUTLINE)


# ---- the clean one: to draw on
fig, ax = figure()
title(ax)
compass(ax, 1440, 300)
fig.savefig(os.path.join(OUT, 'inside_top.png'), dpi=100)
plt.close(fig)

# ---- the labelled one
fig, ax = figure()
title(ax)
compass(ax, 1440, 300)
pts = np.array(P['points'], float)
start = P['start']
out = next(i for i, (x, z) in enumerate(pts) if z < 337)        # through the round window


def line(points, colour, width, dashes=None, arrows_every=36.0, arrow=2.6, z=3):
    us, vs = zip(*(at(x, z_) for x, z_ in points))
    kw = dict(color=colour, lw=width, solid_capstyle='round', zorder=z, path_effects=[pe.Stroke(linewidth=width + 3, foreground=(0.05, 0.05, 0.08)), pe.Normal()])
    if dashes: kw.update(dashes=dashes, dash_capstyle='round')
    ax.plot(us, vs, **kw)
    # (heads along it the way it goes)
    s = 0.0
    for i in range(1, len(points)):
        (xa, za), (xb, zb) = points[i - 1], points[i]
        step = math.hypot(xb - xa, zb - za)
        if step == 0: continue
        if s + step >= arrows_every:
            du, dv = way((xb - xa) / step, (zb - za) / step)
            u, v = at(xb, zb)
            size = arrow * PX
            tip = (u + du * size * 0.6, v + dv * size * 0.6)
            back = (u - du * size * 0.6, v - dv * size * 0.6)
            ax.add_patch(Polygon([tip, (back[0] - dv * size * 0.55, back[1] + du * size * 0.55), (back[0] + dv * size * 0.55, back[1] - du * size * 0.55)],
                                 closed=True, fc=colour, ec=(0.05, 0.05, 0.08), lw=1.2, zorder=z + 0.1))
            s = 0.0
        else:
            s += step


# (the opponents' other ways, under the pilot's)
OTHER = ['#4dd0ff', '#ff6ad5', '#9dff6a']
for k, route in enumerate(P.get('openRoutes', [])):
    line([tuple(p) for p in route], OTHER[k % len(OTHER)], 2.4, dashes=(4, 2.5), arrows_every=44.0, arrow=2.0, z=3 + k * 0.01)
line([tuple(p) for p in pts[:out + 6]], '#ffd84d', 4.2, arrows_every=40.0, arrow=3.0, z=4)

# (the start line across the carpet, a chequer of white and black)
sx, sz = pts[start]
for k in range(8):
    for j in range(2):
        u, v = at(sx + j * 1.0, sz - 8 + k * 2)
        ax.add_patch(Rectangle((u, v - PX), 2 * PX, PX, fc='white' if (k + j) % 2 == 0 else 'black', ec='none', zorder=5))
u, v = at(sx + 2, sz + 9)
ax.text(u + 6, v, 'START', fontsize=17, fontweight='bold', color='white', ha='left', va='bottom', path_effects=OUTLINE, zorder=6)

# (the guards: the Emperor's soldiers and the pigheads, each facing where he looks)
GUARD = {0: ('#5aa0ff', 'soldier'), 1: ('#ff7aa8', 'pighead'), 2: ('#ffa64d', 'Stan')}
for kind, which, gx, gz, gy, fx, fz, leash in P.get('guards', []):
    colour = GUARD.get(kind, GUARD[0])[0]
    u, v = at(gx, gz)
    du, dv = way(fx, fz)
    ax.plot([u, u + du * 3.4 * PX], [v, v + dv * 3.4 * PX], color='white', lw=2.4, zorder=7, solid_capstyle='round',
            path_effects=[pe.Stroke(linewidth=4.5, foreground='black'), pe.Normal()])
    ax.add_patch(Circle((u, v), 1.9 * PX, fc=colour, ec='white', lw=1.8, zorder=7.1))

# (the pictures on the walls: a gold frame on the wall, the name in the room)
SHORT = {'the Dark Monk': 'Dark Monk', 'the Queen of the Mosquibees': 'Queen of the Mosquibees', 'Jerome Baldino': 'Baldino', 'Zoe': 'Zoé'}
for name, x, z, nx, nz in pictures:
    x, z, nx, nz = float(x), float(z), int(float(nx)), int(float(nz))
    du, dv = way(nx, nz)
    u, v = at(x + nx * 0.5, z + nz * 0.5)
    along = (abs(dv), abs(du))                # (the frame along the wall: two cells)
    ax.add_patch(Rectangle((u - along[0] * PX - abs(du) * 0.45 * PX, v - along[1] * PX - abs(dv) * 0.45 * PX),
                           2 * along[0] * PX + abs(du) * 0.9 * PX, 2 * along[1] * PX + abs(dv) * 0.9 * PX, fc='#e8b84a', ec='black', lw=1.0, zorder=8))
    tu, tv = u + du * 2.2 * PX, v + dv * 2.2 * PX
    ax.text(tu, tv, SHORT.get(name, name), fontsize=10.5, color='#ffe7a8', zorder=8.1, path_effects=OUTLINE,
            ha='left' if du > 0 else 'right' if du < 0 else 'center', va='bottom' if dv < 0 else 'top' if dv > 0 else 'center')

# (the secret warp: into room 151's west fireplace, out of the one between the Queen and Zoe)
for x0, z0, x1, z1, y0, y1, ex, ez, ey, beta in P.get('teleports', []):
    a, b = at((x0 + x1) / 2, (z0 + z1) / 2), at(ex, ez)
    ax.add_patch(FancyArrowPatch(a, b, connectionstyle='arc3,rad=-0.45', arrowstyle='-|>', mutation_scale=26, lw=3.0, color='#c58cff',
                                 linestyle=(0, (5, 3)), zorder=9, path_effects=[pe.withStroke(linewidth=5.5, foreground='black')]))
    ax.text(a[0] - 10, a[1] + 26, 'secret warp in', fontsize=12.5, fontweight='bold', color='#e2c6ff', ha='right', va='top', path_effects=OUTLINE, zorder=9.1)
    ax.text(b[0] - 26, b[1] - 18, 'and out', fontsize=12.5, fontweight='bold', color='#e2c6ff', ha='right', va='bottom', path_effects=OUTLINE, zorder=9.1)

# (the rooms' numbers -- the game's scenes the palace was built from. A room is the square round its carpets' crossing, its corners where
# four rooms' walls meet: the number in the tiles of one of the crossing's corners, the one furthest from the guards, the pictures and
# the lines drawn)
marks = [(g[2], g[3]) for g in P.get('guards', [])] + [(float(p[1]), float(p[2])) for p in pictures]
marks += [tuple(p) for p in pts[:out:4]] + [tuple(p) for route in P.get('openRoutes', []) for p in route[::2]]
# (where rooms meet -- the middle of the strip two rooms' grids share -- faint dots, so that each number is seen in its own room)
rooms = [r for r in L['rooms'] if r['scene'] != 80]
bx0, bx1 = min(r['x0'] for r in rooms), max(r['x1'] for r in rooms) + 1
bz0, bz1 = min(r['z0'] for r in rooms), max(r['z1'] for r in rooms) + 1
DOTS = dict(color=(1, 1, 1, 0.45), lw=1.6, dashes=(1.5, 3.5), zorder=2.5)
for x in sorted({(a['x1'] + 1 + b['x0']) / 2 for a in rooms for b in rooms if b['x0'] > a['x0'] and b['x0'] <= a['x1']}):
    ax.plot([U(bz0), U(bz1)], [V(x), V(x)], **DOTS)
for z in sorted({(a['z1'] + 1 + b['z0']) / 2 for a in rooms for b in rooms if b['z0'] > a['z0'] and b['z0'] <= a['z1']}):
    ax.plot([U(z), U(z)], [V(bx0), V(bx1)], **DOTS)
hall = next(r for r in L['rooms'] if r['scene'] == 80)
ax.plot([U((hall['z1'] + 1 + bz0) / 2)] * 2, [V(hall['x0']), V(hall['x1'] + 1)], **DOTS)
for r in L['rooms']:
    mx, mz = (r['x0'] + r['x1']) / 2, (r['z0'] + r['z1']) / 2
    if r['scene'] == 80:
        tries = [(mx + sx * 20, mz + dz) for sx in (1, -1) for dz in range(-70, 71, 10)]
        label = '80 (the long hall)'
    else:
        # (the same corner of the crossing in every room -- up and to the left of it here -- when nothing is near it)
        tries = [(mx + sx * 14, mz + sz * 14) for sx, sz in [(1, -1), (-1, -1), (1, 1), (-1, 1)]]
        label = str(r['scene'])
    room = lambda p: min(math.hypot(p[0] - m[0], p[1] - m[1]) for m in marks)
    best = next((p for p in tries if room(p) >= 7), max(tries, key=room))
    u, v = at(*best)
    ax.text(u, v, label, fontsize=20, fontweight='bold', color=(1, 1, 1, 0.82), ha='center', va='center', path_effects=OUTLINE, zorder=10)

# (the ways in and out)
u, v = at(565, 709)
ax.text(u, v - 3.5 * PX, 'entrance doors (shut)', fontsize=13, fontweight='bold', color='white', ha='center', va='bottom', path_effects=OUTLINE)
w = L['window']
u, v = at((w['x0'] + w['x1']) / 2, w['z0'])
ax.add_patch(FancyArrowPatch((u + 4 * PX, v - 30 * PX), (u - 2, v - 4 * PX), arrowstyle='-|>', mutation_scale=22, lw=2.5, color='white',
                             path_effects=[pe.withStroke(linewidth=5, foreground='black')]))
ax.text(u + 4 * PX, v - 31 * PX, 'round window:\nout to the roof road', fontsize=13, fontweight='bold', color='white', ha='left', va='bottom',
        linespacing=1.3, path_effects=OUTLINE)

# (the key)
key = [('line', '#ffd84d', 4.2, None, 'the racing line (the test pilot\'s)'),
       ('line', OTHER[0], 2.4, (4, 2.5), 'the opponents\' other ways (one each)'),
       ('line', '#c58cff', 3.0, (5, 3), 'the secret warp, fireplace to fireplace'),
       ('dot', GUARD[0][0], 0, None, 'a soldier (the tick: where he looks)'),
       ('dot', GUARD[1][0], 0, None, 'a pighead (throws things)'),
       ('frame', '#e8b84a', 0, None, 'a picture on the wall'),
       ('text', 'white', 0, None, 'room numbers: the game\'s scenes'),
       ('arrow', '#e6e1d8', 0, None, 'white arrows on the floor: ways on')]
ku, kv = 44, 196
ax.add_patch(Rectangle((ku - 18, kv - 22), 590, len(key) * 42 + 28, fc=(0.06, 0.07, 0.1, 0.78), ec=(1, 1, 1, 0.25), lw=1.2, zorder=11))
for i, (kind, colour, width, dashes, words) in enumerate(key):
    v = kv + i * 42 + 10
    if kind == 'line':
        kw = dict(color=colour, lw=width, zorder=12, solid_capstyle='round', path_effects=[pe.Stroke(linewidth=width + 3, foreground='black'), pe.Normal()])
        if dashes: kw['dashes'] = dashes
        ax.plot([ku, ku + 64], [v, v], **kw)
    elif kind == 'dot':
        ax.add_patch(Circle((ku + 32, v), 1.9 * PX, fc=colour, ec='white', lw=1.8, zorder=12))
    elif kind == 'frame':
        ax.add_patch(Rectangle((ku + 24, v - 5), 16, 10, fc=colour, ec='black', lw=1.0, zorder=12))
    elif kind == 'text':
        ax.text(ku + 32, v, '151', fontsize=15, fontweight='bold', color='white', ha='center', va='center', path_effects=OUTLINE, zorder=12)
    else:
        ax.add_patch(FancyArrowPatch((ku + 10, v), (ku + 56, v), arrowstyle='-|>', mutation_scale=18, lw=3, color=colour, zorder=12))
    ax.text(ku + 84, v, words, fontsize=14, color='white', va='center', zorder=12, path_effects=OUTLINE)
fig.savefig(os.path.join(OUT, 'inside_top_labelled.png'), dpi=100)
plt.close(fig)
print(f'{OUT}: inside_top.png, inside_top_labelled.png ({W} x {H}); {len(pictures)} pictures, {len(P.get("guards", []))} guards, the line to point {out + 5}')
