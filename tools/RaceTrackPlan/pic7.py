import numpy as np, json
from PIL import Image, ImageDraw
from scipy.interpolate import RBFInterpolator
from scipy.ndimage import gaussian_filter1d
A=np.array(json.load(open('E:/dump/TEMP/landmarks.json')),float)
f=RBFInterpolator(A[:,:2],A[:,2:],kernel='thin_plate_spline')
px=np.array(json.load(open('E:/dump/TEMP/pic_centerline_px.json')),float)
# resample uniformly in picture space, smooth (periodic)
d=np.r_[0,np.cumsum(np.hypot(*np.diff(px,axis=0).T))]
n=int(d[-1]/2)
t=np.linspace(0,d[-1],n,endpoint=False)
rs=np.c_[np.interp(t,d,px[:,0]),np.interp(t,d,px[:,1])]
sm=np.c_[gaussian_filter1d(rs[:,0],7,mode='wrap'),gaussian_filter1d(rs[:,1],7,mode='wrap')]
mp=f(sm)                       # rotated-map px
mx=1023-mp[:,0]; my=1279-mp[:,1]   # unrotated map px (4 px per cell)
cells=np.c_[mx/4.0,my/4.0]       # cell coords inside the 4x5 cube block (0..256, 0..320)
json.dump(cells.tolist(),open('E:/dump/TEMP/track_cells_raw.json','w'))
print('points',len(cells),'length in cells',np.hypot(*np.diff(np.r_[cells,cells[:1]],axis=0).T).sum())
base=Image.open('E:/dump/TEMP/base_desert.png').convert('RGB'); dr=ImageDraw.Draw(base)
pts=[(x*4,y*4) for x,y in cells]
dr.line(pts+[pts[0]],fill=(255,255,0),width=3)
base.save('E:/dump/TEMP/base_with_track.png')
