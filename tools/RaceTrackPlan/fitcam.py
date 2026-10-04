# (Mosquibees Island, 2026-09-29; run in E:\dump\TEMP\mosq with H.npy and pic_masks.npy there: the drawing's sea, lime, pink and blue masks)
"""Fit a perspective camera that turns the island's 3D ground into the user's picture: the picture's land (not the red sea) against the
land the camera would see (every ground sample above the sea), scored by IoU on a reduced picture."""
import numpy as np
from PIL import Image
from scipy.optimize import minimize
from scipy.ndimage import map_coordinates, binary_dilation

H = np.load('H.npy')                       # H[z - 448, x - 448], units
O = 448
sea, lime, pink, blue = np.load('pic_masks.npy')
over = binary_dilation(lime | pink | blue, iterations=3)
DS = 4
land_pic = ~sea[::DS, ::DS]
care = ~over[::DS, ::DS]
PH, PW = land_pic.shape

# dense ground samples (every 1/3 cell), in cells: x, height / 512, z
g = np.arange(0, 128.01, 1 / 3)
gx, gz = np.meshgrid(g, g)
Hn = np.nan_to_num(H, nan=-1)
h = map_coordinates(Hn, [gz.ravel(), gx.ravel()], order=1)
keep = h > 60
X = gx.ravel()[keep] + O; Z = gz.ravel()[keep] + O; Y = h[keep] / 512.0
P = np.stack([X, Y, Z], 1)


def rot(yaw, pitch, roll):
    cy, sy = np.cos(yaw), np.sin(yaw); cp, sp = np.cos(pitch), np.sin(pitch); cr, sr = np.cos(roll), np.sin(roll)
    Ry = np.array([[cy, 0, -sy], [0, 1, 0], [sy, 0, cy]])
    Rx = np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]])
    Rr = np.array([[cr, 0, -sr], [0, 1, 0], [sr, 0, cr]])
    return Rr @ Rx @ Ry


def project(params, pts):
    tx, tz, dist, yaw, pitch, roll, f, cx, cy, ax = params
    # camera looking at (tx, 0, tz) from `dist` cells away; pitch from straight down
    R = rot(yaw, pitch, roll)
    rel = pts - np.array([tx, 0, tz])
    c = rel @ R.T            # camera space before the push-back: x right, y up(towards camera), z forward
    # looking down: depth = dist - y'
    depth = dist - c[:, 1]
    u = cx + f * ax * c[:, 0] / depth
    v = cy + f * c[:, 2] / depth
    return u, v, depth


def render(params):
    u, v, d = project(params, P)
    m = np.zeros((PH, PW), bool)
    ok = (d > 1) & (u >= 0) & (u < PW * DS) & (v >= 0) & (v < PH * DS)
    m[(v[ok] / DS).astype(int), (u[ok] / DS).astype(int)] = True
    return binary_dilation(m, iterations=1)


def score(params):
    m = render(params)
    a = m & care; b = land_pic & care
    return 1 - (a & b).sum() / max(1, (a | b).sum())


if __name__ == '__main__':
    best = None
    # the picture keeps the render's orientation (x right, z down); z grows downwards in the picture -> v grows with z
    for pitch in (0.0, 0.4, 0.7):
        for dist in (150, 250, 400):
            f0 = 8.0 * dist
            p0 = np.array([512, 512, dist, 0.0, pitch, 0.0, f0, 570, 380, 1.0])
            r = minimize(score, p0, method='Powell', options={'maxiter': 6000, 'xtol': 1e-3, 'ftol': 1e-5})
            print('start pitch %.1f dist %d -> %.4f' % (pitch, dist, r.fun), np.round(r.x, 3))
            if best is None or r.fun < best.fun:
                best = r
    np.save('cam.npy', best.x)
    print('best', best.fun, list(best.x))
    m = render(best.x)
    out = np.zeros((PH, PW, 3), np.uint8)
    out[land_pic & m] = (220, 220, 220); out[land_pic & ~m] = (220, 60, 60); out[~land_pic & m] = (60, 60, 220)
    Image.fromarray(out).resize((PW * 2, PH * 2), Image.NEAREST).save('fit.png')
