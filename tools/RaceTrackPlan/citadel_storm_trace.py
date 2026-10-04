# (Citadel Island in the storm, 2026-09-29) -- the lime line (the pink dots joined) skeletonised and walked into island cells
#  (route_cells.npy), the orange pit lane's cells (pit_cells.npy) and the pink dots' cells
import json, numpy as np
from PIL import Image, ImageDraw
from skimage.morphology import skeletonize
from scipy.ndimage import label, binary_dilation
su, sv, tu, tv = np.load('fit.npy')
sea, lime, pink, orange = np.load('pic_masks.npy')
def cell(u, v):  # picture pixel -> island cell
    py = 1024 - (u - tu) / su; px = (v - tv) / sv
    return 384 + px / 4, 448 + py / 4
# the route: lime plus the gap between the pink dots bridged
lab, n = label(pink); cents = [np.argwhere(lab == k + 1).mean(0) for k in range(n)]
cents = sorted([c for c in cents if (lab == 0).sum() > 0], key=lambda c: c[1])
print('pink dots (v,u):', [tuple(np.round(c, 1)) for c in cents])
route = lime | pink
img = Image.fromarray(route.astype(np.uint8) * 255); d = ImageDraw.Draw(img)
(v0, u0), (v1, u1) = cents[0], cents[-1]
d.line([(u0, v0), (u1, v1)], fill=255, width=14)
route = np.asarray(img) > 0
lab, n = label(route); sz = np.bincount(lab.ravel()); sz[0] = 0; route = lab == sz.argmax()
sk = skeletonize(route)
pts = set(zip(*np.where(sk)))
# prune spurs
for _ in range(30):
    deg = {p: sum((p[0] + a, p[1] + b) in pts for a in (-1, 0, 1) for b in (-1, 0, 1) if a or b) for p in pts}
    ends = [p for p in pts if deg[p] <= 1]
    if not ends: break
    for e in ends: pts.discard(e)
# walk the loop
start = min(pts, key=lambda p: (p[1], p[0]))
order = [start]; seen = {start}; cur = start
while True:
    nb = [(cur[0] + a, cur[1] + b) for a in (-1, 0, 1) for b in (-1, 0, 1) if (a or b) and (cur[0] + a, cur[1] + b) in pts and (cur[0] + a, cur[1] + b) not in seen]
    if not nb: break
    nb.sort(key=lambda p: abs(p[0] - cur[0]) + abs(p[1] - cur[1]))
    cur = nb[0]; order.append(cur); seen.add(cur)
print('skeleton', len(pts), 'walked', len(order))
cells = np.array([cell(u, v) for v, u in order])
np.save('route_cells.npy', cells)
L = np.sum(np.hypot(*np.diff(cells, axis=0).T))
print('route length cells', round(L, 1), 'x range', cells[:, 0].min().round(1), cells[:, 0].max().round(1), 'z range', cells[:, 1].min().round(1), cells[:, 1].max().round(1))
ov, ou = np.where(orange); oc = np.array([cell(u, v) for v, u in zip(ov, ou)])
print('pit lane cells x', oc[:, 0].min().round(1), oc[:, 0].max().round(1), 'z', oc[:, 1].min().round(1), oc[:, 1].max().round(1))
np.save('pit_cells.npy', oc)
print('pink dots cells', [tuple(np.round(cell(c[1], c[0]), 1)) for c in cents])
