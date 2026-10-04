import numpy as np, json
from scipy.interpolate import RBFInterpolator
P='C:/Users/Matt/AppData/Local/Temp/claude/e--dump-LBAAssembler/f838bac2-5bd5-42f7-88e7-5a3650224b3a/images/3.png'
from PIL import Image
im=np.array(Image.open(P).convert('RGB')); r,g,b=[im[...,i].astype(int) for i in range(3)]
yellow=(r>200)&(g>200)&(b<90)
ys,xs=np.where(yellow); pts=np.c_[xs,ys].astype(float)
c=pts.mean(0); u,s,vt=np.linalg.svd(pts-c,full_matrices=False); d=vt[0]
t=(pts-c)@d; a=c+d*t.min(); bb=c+d*t.max()
print('pit lane picture endpoints',a,bb)
A=np.array(json.load(open('E:/dump/TEMP/landmarks.json')),float)
f=RBFInterpolator(A[:,:2],A[:,2:],kernel='thin_plate_spline')
def cell(p):
    m=f(np.array([p]))[0]; return [float((1023-m[0])/4),float((1279-m[1])/4)]
route=np.load('E:/dump/TEMP/plan_route.npy')
plan={'originCellX':448,'originCellZ':448,'points':route.tolist(),'pitA':cell(a),'pitB':cell(bb)}
json.dump(plan,open('E:/dump/LBA2RaceTrackBuild/track_plan.json','w'))
print('points',len(route),'pit',plan['pitA'],plan['pitB'])
