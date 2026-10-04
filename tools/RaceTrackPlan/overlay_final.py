import numpy as np, json
from PIL import Image, ImageDraw
from scipy.interpolate import RBFInterpolator
P='C:/Users/Matt/AppData/Local/Temp/claude/e--dump-LBAAssembler/f838bac2-5bd5-42f7-88e7-5a3650224b3a/images/3.png'
pic=Image.open(P).convert('RGBA')
A=np.array(json.load(open('E:/dump/TEMP/landmarks.json')),float)
f_m2p=RBFInterpolator(A[:,2:],A[:,:2],kernel='thin_plate_spline')
route=np.load('E:/dump/TEMP/plan_route.npy')            # block cells
mp=np.c_[1023-route[:,0]*4,1279-route[:,1]*4]           # rotated-map px
pp=f_m2p(mp)
lay=Image.new('RGBA',pic.size,(0,0,0,0)); d=ImageDraw.Draw(lay)
pts=[tuple(p) for p in pp]
d.line(pts+[pts[0]],fill=(0,255,255,255),width=5)
d.line(pts+[pts[0]],fill=(0,0,0,255),width=1)
out=Image.alpha_composite(pic,lay).convert('RGB')
d2=ImageDraw.Draw(out)
d2.text((30,20),'lime = the proposed track (your picture), cyan = the track as built on the island (same map, mapped back onto the picture)',fill=(255,255,255))
out.save('E:/dump/TEMP/built_over_concept.png')
print('ok')
