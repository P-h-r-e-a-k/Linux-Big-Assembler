# Celebration Island's decor bodies (CELEBRA2: the statue, the temple, the columns) as triangles in cube-local units -- from
# `ScriptRoundTrip decorpoints CELEBRA2 E:\dump\TEMP\celeb\decorpoints.csv` -- a cloud of points over their surfaces, for keeping a
# planned road clear of their real shapes (their collision boxes are far bigger), and pictures of the island from the side and above.
import numpy as np, csv, os
from PIL import Image
D = 'E:/dump/TEMP/celeb/'
def triangles():
    rows = list(csv.reader(open(D + 'decorpoints.faces.csv')))[1:]
    a = np.array([[float(v) for v in r] for r in rows])
    return a[:, 0].astype(int), a[:, 1].astype(int), a[:, 2:].reshape(-1, 3, 3)   # decor, body, (n, 3 corners, xyz)

def cloud(step=64.0, bodies=None):
    """points spread over every triangle, about `step` apart: (n, 3) and each one's body"""
    cache = D + f'cloud_{int(step)}.npz'
    if os.path.exists(cache):
        z = np.load(cache); pts, body = z['pts'], z['body']
    else:
        dec, bod, tri = triangles()
        out = []; ob = []
        for b, t in zip(bod, tri):
            e1, e2 = t[1] - t[0], t[2] - t[0]
            n = int(max(np.linalg.norm(e1), np.linalg.norm(e2), np.linalg.norm(t[2] - t[1])) / step) + 1
            u, v = np.meshgrid(np.arange(n + 1) / n, np.arange(n + 1) / n)
            keep = u + v <= 1.0 + 1e-9
            p = t[0] + u[keep][:, None] * e1 + v[keep][:, None] * e2
            out.append(p); ob.append(np.full(len(p), b))
        pts = np.concatenate(out); body = np.concatenate(ob)
        np.savez(cache, pts=pts, body=body)
    if bodies is not None:
        keep = np.isin(body, bodies); return pts[keep], body[keep]
    return pts, body

def render(view, out, scale=0.04, extra=None, ymax=22000):
    """orthographic picture with a depth buffer: view 'west' (looking east: across = z), 'south' (looking north: across = x), 'top'"""
    dec, bod, tri = triangles()
    H = np.load(D + 'H.npy').astype(float)
    # the ground as triangles too
    g = []
    for z in range(64):
        for x in range(64):
            c = [(x * 512, H[z, x], z * 512), (x * 512, H[z + 1, x], (z + 1) * 512), ((x + 1) * 512, H[z + 1, x + 1], (z + 1) * 512), ((x + 1) * 512, H[z, x + 1], z * 512)]
            g.append([c[0], c[1], c[2]]); g.append([c[0], c[2], c[3]])
    g = np.array(g, float)
    alltri = np.concatenate([tri, g]); kind = np.concatenate([bod, np.full(len(g), -1)])
    if extra is not None:
        alltri = np.concatenate([alltri, extra[0]]); kind = np.concatenate([kind, extra[1]])
    W = int(32768 * scale); Hh = int(ymax * scale) if view != 'top' else W
    img = np.zeros((Hh, W, 3), np.uint8); img[:] = (24, 28, 44)
    depth = np.full((Hh, W), -1e18)
    light = np.array([0.4, 0.8, 0.45]); light /= np.linalg.norm(light)
    for t, k in zip(alltri, kind):
        n = np.cross(t[1] - t[0], t[2] - t[0]); ln = np.linalg.norm(n)
        if ln < 1e-6: continue
        n /= ln
        shade = 0.35 + 0.65 * abs(n @ light)
        base = np.array((150, 150, 160) if k >= 0 and k < 100 else (120, 95, 70) if k == -1 else (230, 60, 60) if k == 100 else (90, 200, 90) if k == 101 else (240, 200, 60))
        if k == -1 and t[:, 1].max() < 50: base = np.array((40, 70, 140))
        col = (base * shade).astype(np.uint8)
        if view == 'west': sx, sy, d = t[:, 2] * scale, (ymax - t[:, 1]) * scale, -t[:, 0]
        elif view == 'east': sx, sy, d = (32768 - t[:, 2]) * scale, (ymax - t[:, 1]) * scale, t[:, 0]
        elif view == 'south': sx, sy, d = t[:, 0] * scale, (ymax - t[:, 1]) * scale, t[:, 2]
        elif view == 'north': sx, sy, d = (32768 - t[:, 0]) * scale, (ymax - t[:, 1]) * scale, -t[:, 2]
        else: sx, sy, d = t[:, 0] * scale, t[:, 2] * scale, t[:, 1]
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
    Image.fromarray(img).save(out)

if __name__ == '__main__':
    for v in ('west', 'south', 'north', 'east', 'top'): render(v, D + f'view_{v}.png')
    p, b = cloud(); print(len(p), 'cloud points')
