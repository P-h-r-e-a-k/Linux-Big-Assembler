# (Citadel Island in the storm, 2026-09-29; run in E:\dump\TEMP\cstorm with drawing.png = citadelTrack.png, heights.csv from
#  `ScriptRoundTrip islandheights CITADEL heights.csv` and terrain.png from `islandrender x CITADEL Terrain terrain.png 4`, both with
#  LBA2_DIR=<the pristine files>) -- masks of the drawing (sea, lime, pink, orange) and the island's height grid H.npy
import numpy as np
from PIL import Image
from scipy.ndimage import binary_opening, binary_closing, map_coordinates
from scipy.optimize import minimize
pic = np.asarray(Image.open('drawing.png').convert('RGB')).astype(int)
PH, PW, _ = pic.shape
r, g, b = pic[..., 0], pic[..., 1], pic[..., 2]
lime = (g > 170) & (r > 120) & (r < 230) & (b < 90)
pink = (r > 220) & (g > 150) & (g < 210) & (b > 180)
orange = (r > 200) & (g > 80) & (g < 170) & (b < 80)
drawn = lime | pink | orange
# sea: the grey-blue water texture; sample the corners
print('corner colours', pic[5, 5], pic[650, 1440], pic[5, 1440], pic[600, 700])
d = np.stack([r, g, b], -1)
sea_ref = np.array([pic[0:40, 0:40].reshape(-1, 3).mean(0), pic[620:660, 1400:1440].reshape(-1, 3).mean(0)]).mean(0)
print('sea ref', sea_ref)
# sea: bluish grey, low saturation, b > r
sea = (b > r + 8) & (np.abs(g - r) < 30) & (np.abs(d - sea_ref).sum(-1) < 90)
sea = binary_opening(binary_closing(sea, iterations=2), iterations=2)
land = ~sea & ~drawn
np.save('pic_masks.npy', np.stack([sea, lime, pink, orange]))
Image.fromarray((np.stack([land * 255, lime * 255, (pink | orange) * 255], -1)).astype(np.uint8)).save('pic_masks.png')
# island heights
rows = np.genfromtxt('heights.csv', delimiter=',', skip_header=1)
x0, z0 = int(rows[:, 0].min()), int(rows[:, 1].min())
W = int(rows[:, 0].max()) - x0 + 1; Hh = int(rows[:, 1].max()) - z0 + 1
H = np.full((Hh, W), np.nan)
H[(rows[:, 1] - z0).astype(int), (rows[:, 0] - x0).astype(int)] = rows[:, 2]
np.save('H.npy', H); print('H', H.shape, 'origin', x0, z0, 'range', np.nanmin(H), np.nanmax(H))
vals = H[~np.isnan(H)]
print('height percentiles', np.percentile(vals, [5, 20, 40, 50, 60, 80]))
