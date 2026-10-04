# (Mosquibees Island, 2026-09-29; run in E:\dump\TEMP\mosq after fitcam.py: cam.npy, H.npy, pic_masks.npy and terrain.png there)
"""The user's route traced on the picture (snake through crossings) and put on the ground: each picture pixel's visible ground point comes
from a depth buffer of the island seen through the fitted camera."""
import json, warnings
import numpy as np
from PIL import Image, ImageDraw
from skimage.morphology import skeletonize
from scipy.ndimage import binary_closing, label, map_coordinates
from scipy.spatial import cKDTree
from fitcam import project, H, O
warnings.filterwarnings('ignore')

cam = np.load('cam.npy')
sea, lime, pink, blue = np.load('pic_masks.npy')
PH, PW = sea.shape

# the visible ground behind every pixel: dense samples, nearest wins
g = np.arange(0, 128.01, 1 / 6)
gx, gz = np.meshgrid(g, g)
h = map_coordinates(np.nan_to_num(H, nan=0), [gz.ravel(), gx.ravel()], order=1)
X = gx.ravel() + O; Z = gz.ravel() + O; Y = h / 512.0
u, v, d = project(cam, np.stack([X, Y, Z], 1))
ok = (u >= 0) & (u < PW) & (v >= 0) & (v < PH)
ui, vi = u[ok].astype(int), v[ok].astype(int)
order = np.argsort(-d[ok])                     # far first, near overwrites
pix = vi[order] * PW + ui[order]
wx = np.full(PH * PW, np.nan); wz = np.full(PH * PW, np.nan); wh = np.full(PH * PW, np.nan)
wx[pix] = X[ok][order]; wz[pix] = Z[ok][order]; wh[pix] = h[ok][order]
filled = ~np.isnan(wx)
fi = np.where(filled)[0]
ftree = cKDTree(np.stack([fi % PW, fi // PW], 1))


def ground(px, py):
    _, k = ftree.query([px, py])
    j = fi[k]
    return wx[j], wz[j], wh[j]


def biggest(m):
    lab, n = label(m)
    sz = np.bincount(lab.ravel()); sz[0] = 0
    return lab == sz.argmax()


def prune(sk, rounds=40):
    pts = set(zip(*np.where(sk)))
    for _ in range(rounds):
        deg = {p: sum(1 for a in (-1, 0, 1) for b in (-1, 0, 1) if (a or b) and (p[0] + a, p[1] + b) in pts) for p in pts}
        ends = {p for p in pts if deg[p] <= 1}
        if not ends:
            break
        pts -= ends
    return pts


route_mask = lime | pink | blue
sk = skeletonize(biggest(binary_closing(route_mask, np.ones((9, 9)))))
pts = np.array(sorted(prune(sk)), dtype=float)          # (y, x)
tree = cKDTree(pts)
print('skeleton', len(pts))

# start at the pink bar's lower end heading into the bridge? no: start on the bottom straight, heading west (the lap's direction as drawn:
# along the bottom to the mountain, round it twice, over the bridge, north with the jump, round the mesa, down the east side)
start = pts[np.argmin(np.hypot(pts[:, 1] - 700, pts[:, 0] - 690))]
dirv = np.array([0.0, -1.0])                            # (dy, dx): west
STEP = 4.0
route = [start.copy()]
pos = start.copy()
closed = False
for _ in range(20000):
    best = None
    for reach in (3.5, 6, 9, 12):
        target = pos + dirv * max(STEP, reach)
        idx = tree.query_ball_point(target, reach)
        bd = 1e9
        for i in idx:
            q = pts[i]; w = q - pos; n = np.linalg.norm(w)
            if n < 1e-6 or (w @ dirv) / n < 0.5:
                continue
            dd = np.linalg.norm(q - target)
            if dd < bd:
                bd, best = dd, q
        if best is not None:
            break
    if best is None:                                    # a crossing the skeleton merged: carry on to the straightest line a little further
        ring = [q for q in pts[tree.query_ball_point(pos, 45)] if 25 <= np.linalg.norm(q - pos)]
        if ring:
            cosv = [((q - pos) @ dirv) / np.linalg.norm(q - pos) for q in ring]
            k = int(np.argmax(cosv))
            if cosv[k] > 0.6:
                best = ring[k]
    if best is None:
        print('lost at', pos, 'dir', dirv, 'after', [tuple(map(int, r)) for r in route[-6:]]); break
    nd = (best - pos) / np.linalg.norm(best - pos)
    dirv = 0.5 * dirv + 0.5 * nd; dirv /= np.linalg.norm(dirv)
    pos = best
    route.append(pos.copy())
    if len(route) > 60 and np.linalg.norm(pos - start) < STEP * 1.2:
        closed = True; break
route = np.array(route)
print('snake %d points closed %s' % (len(route), closed))

# what each point is: bridge (pink), jump (blue) or road
def kind(p):
    y, x = int(p[0]), int(p[1])
    win = (slice(max(0, y - 8), y + 9), slice(max(0, x - 8), x + 9))
    if pink[win].sum() > 20: return 'bridge'
    if blue[win].sum() > 20: return 'jump'
    return 'road'

cells = []
for p in route:
    x, z, hh = ground(p[1], p[0])
    cells.append([float(x), float(z), float(hh), kind(p), float(p[1]), float(p[0])])
json.dump({'loop': cells, 'closed': closed}, open('trace.json', 'w'))

# draw it on the top-down render
img = Image.open('terrain.png').convert('RGB')
dr = ImageDraw.Draw(img)
col = {'road': (180, 255, 0), 'bridge': (255, 80, 200), 'jump': (0, 160, 255)}
for a, b in zip(cells, cells[1:]):
    dr.line([((a[0] - O) * 4, (a[1] - O) * 4), ((b[0] - O) * 4, (b[1] - O) * 4)], fill=col[a[3]], width=3)
dr.ellipse([((cells[0][0] - O) * 4 - 6, (cells[0][1] - O) * 4 - 6), ((cells[0][0] - O) * 4 + 6, (cells[0][1] - O) * 4 + 6)], outline=(255, 255, 255), width=2)
img.save('route_on_island.png')
print('kinds', {k: sum(1 for c in cells if c[3] == k) for k in col})
