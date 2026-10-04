# (Citadel Island in the storm, 2026-09-29) -- the drawing fitted to the island's render: land against land, the picture being the map
#  turned a quarter turn clockwise and squashed (IoU 0.84); writes fit.npy (scale across, scale down, offsets)
import numpy as np
from PIL import Image
from scipy.ndimage import map_coordinates, binary_opening
from scipy.optimize import minimize
sea, lime, pink, orange = np.load('pic_masks.npy')
drawn = lime | pink | orange
PH, PW = sea.shape
ren = np.asarray(Image.open('terrain.png').convert('RGB')).astype(int)
R, G, B = ren[..., 0], ren[..., 1], ren[..., 2]
rsea = (B > R + 40) & (B > G + 20)
rvoid = (R < 40) & (G < 45) & (B < 50) & (np.abs(R - B) < 20)
rland = ~rsea & ~rvoid
rland = binary_opening(rland, iterations=1)
Image.fromarray((rland * 255).astype(np.uint8)).save('render_land.png')
# picture pixels (subsampled) that are not drawn over
ys, xs = np.mgrid[0:PH:3, 0:PW:3]
keep = ~drawn[ys, xs]
ys, xs = ys[keep].astype(float), xs[keep].astype(float)
pland = ~sea[ys.astype(int), xs.astype(int)]
# picture (u, v) = (tu + su * (1024 - py), tv + sv * px)  <=>  py = 1024 - (u - tu) / su, px = (v - tv) / sv
def iou(p):
    su, sv, tu, tv = p
    py = 1024 - (xs - tu) / su; px = (ys - tv) / sv
    inside = (px >= 0) & (px < 1023) & (py >= 0) & (py < 1023)
    rl = np.zeros_like(pland)
    rl[inside] = map_coordinates(rland.astype(float), [py[inside], px[inside]], order=0) > 0.5
    return (rl & pland).sum() / max(1, (rl | pland).sum())
best = None
for su in np.arange(1.2, 1.7, 0.05):
    for sv in np.arange(0.7, 1.2, 0.05):
        # land centroid match for the offsets
        for tu in np.arange(-300, 100, 20):
            for tv in np.arange(-400, 0, 20):
                s = iou((su, sv, tu, tv))
                if best is None or s > best[0]: best = (s, (su, sv, tu, tv))
print('grid best', best)
res = minimize(lambda p: -iou(p), best[1], method='Nelder-Mead', options={'xatol': 0.01, 'fatol': 1e-5, 'maxiter': 2000, 'initial_simplex': None})
print('refined', -res.fun, res.x)
np.save('fit.npy', res.x)
