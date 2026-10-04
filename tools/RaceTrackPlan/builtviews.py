# Pictures of a built island -- its ground and every decor body, a raised road's pieces and piers among them -- from the sides and from
# above, without the engine:
#   builtviews.py <game folder> <ISLAND> <scratch folder> <first new body> [view ...] [--scale 0.06] [--top 16000]
# It runs `ScriptRoundTrip decorpoints` and `islandheights` on the game folder, then draws <scratch>\built_<view>.png for each view
# (west, east, south, north, top; an `iso` view looks down from the south-west). Bodies from <first new body> on are drawn as the road
# (dark on top, light at the sides), the six before them as the start gantry (red), the rest as the island's own (grey).
import csv, os, subprocess, sys
import numpy as np
from PIL import Image

args = [a for i, a in enumerate(sys.argv[1:], 1) if not a.startswith('--') and not sys.argv[i - 1].startswith('--')]
game, island, D, first_new = args[0], args[1], args[2].rstrip('/\\') + '/', int(args[3])
views = args[4:] or ['west', 'south', 'top', 'iso']
def option(name, default):
    return float(sys.argv[sys.argv.index(name) + 1]) if name in sys.argv else default
SCALE, YMAX = option('--scale', 0.06), option('--top', 16000)
SR = 'E:/dump/LBAAssembler/tools/ScriptRoundTrip/bin/Debug/net10.0/ScriptRoundTrip.exe'
env = dict(os.environ, LBA2_DIR=game)
subprocess.run([SR, 'decorpoints', island, D + 'built_points.csv'], env=env, capture_output=True, check=True)
subprocess.run([SR, 'islandheights', island, D + 'built_heights.csv'], env=env, capture_output=True, check=True)

rows = list(csv.reader(open(D + 'built_points.faces.csv')))[1:]
a = np.array([[float(v) for v in r] for r in rows])
bod = a[:, 1].astype(int); tri = a[:, 2:].reshape(-1, 3, 3)
H = np.zeros((65, 65))
for r in list(csv.reader(open(D + 'built_heights.csv')))[1:]:
    x, z = int(r[0]) - 448, int(r[1]) - 448
    if 0 <= x <= 64 and 0 <= z <= 64 and r[2] != '': H[z, x] = float(r[2])
g = []
for z in range(64):
    for x in range(64):
        c = [(x * 512, H[z, x], z * 512), (x * 512, H[z + 1, x], (z + 1) * 512), ((x + 1) * 512, H[z + 1, x + 1], (z + 1) * 512), ((x + 1) * 512, H[z, x + 1], z * 512)]
        g.append([c[0], c[1], c[2]]); g.append([c[0], c[2], c[3]])
g = np.array(g, float)
alltri = np.concatenate([tri, g]); kind = np.concatenate([bod, np.full(len(g), -1)])

def render(view, out):
    iso = view == 'iso'
    if iso:
        # looking north-east and down: across = (x - z) / sqrt 2, up the picture = height and distance
        W = int(32768 * 1.42 * SCALE); Hh = int((YMAX * 0.82 + 32768 * 1.42 * 0.5) * SCALE)
    else:
        W = int(32768 * SCALE); Hh = int(YMAX * SCALE) if view != 'top' else W
    img = np.zeros((Hh, W, 3), np.uint8); img[:] = (176, 190, 210)
    depth = np.full((Hh, W), -1e18)
    light = np.array([0.35, 0.8, 0.5]); light /= np.linalg.norm(light)
    for t, k in zip(alltri, kind):
        n = np.cross(t[1] - t[0], t[2] - t[0]); ln = np.linalg.norm(n)
        if ln < 1e-6: continue
        n /= ln
        shade = 0.45 + 0.55 * abs(n @ light)
        if k == -1: base = np.array((70, 100, 160)) if t[:, 1].max() < 60 else np.array((128, 100, 74))
        elif k < first_new - 6: base = np.array((158, 158, 168))
        elif k < first_new: base = np.array((200, 60, 50))
        else: base = np.array((76, 78, 84)) if abs(n[1]) > 0.5 else np.array((176, 176, 170))
        col = (base * shade).astype(np.uint8)
        if view == 'west': sx, sy, d = t[:, 2] * SCALE, (YMAX - t[:, 1]) * SCALE, -t[:, 0]
        elif view == 'east': sx, sy, d = (32768 - t[:, 2]) * SCALE, (YMAX - t[:, 1]) * SCALE, t[:, 0]
        elif view == 'south': sx, sy, d = t[:, 0] * SCALE, (YMAX - t[:, 1]) * SCALE, t[:, 2]
        elif view == 'north': sx, sy, d = (32768 - t[:, 0]) * SCALE, (YMAX - t[:, 1]) * SCALE, -t[:, 2]
        elif iso:
            u = (t[:, 0] + t[:, 2]) / 1.4142; v = (t[:, 2] - t[:, 0]) / 1.4142          # u: across, v: towards the eye
            sx = u * SCALE; sy = Hh - (t[:, 1] * 0.82 - v * 0.5 + 32768 * 0.71 * 0.5) * SCALE; d = v * 0.82 + t[:, 1] * 0.5
        else: sx, sy, d = t[:, 0] * SCALE, t[:, 2] * SCALE, t[:, 1]
        x0, x1 = int(max(0, np.floor(sx.min()))), int(min(W - 1, np.ceil(sx.max())))
        y0, y1 = int(max(0, np.floor(sy.min()))), int(min(Hh - 1, np.ceil(sy.max())))
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        den = (sy[1] - sy[2]) * (sx[0] - sx[2]) + (sx[2] - sx[1]) * (sy[0] - sy[2])
        if abs(den) < 1e-9: continue
        w0 = ((sy[1] - sy[2]) * (xs - sx[2]) + (sx[2] - sx[1]) * (ys - sy[2])) / den
        w1 = ((sy[2] - sy[0]) * (xs - sx[2]) + (sx[0] - sx[2]) * (ys - sy[2])) / den
        w2 = 1 - w0 - w1
        inside = (w0 >= -1e-3) & (w1 >= -1e-3) & (w2 >= -1e-3)
        dd = w0 * d[0] + w1 * d[1] + w2 * d[2]
        sub = depth[y0:y1 + 1, x0:x1 + 1]; simg = img[y0:y1 + 1, x0:x1 + 1]
        win = inside & (dd > sub)
        sub[win] = dd[win]; simg[win] = col
    Image.fromarray(img).save(out, optimize=True)
    print(out)

for v in views: render(v, D + f'built_{v}.png')
