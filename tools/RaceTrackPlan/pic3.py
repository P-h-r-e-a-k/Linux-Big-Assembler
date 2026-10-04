import numpy as np, cv2, json, sys
from PIL import Image
from scipy.interpolate import RBFInterpolator
P='C:/Users/Matt/AppData/Local/Temp/claude/e--dump-LBAAssembler/f838bac2-5bd5-42f7-88e7-5a3650224b3a/images/3.png'
pic=np.array(Image.open(P).convert('RGB'))
maprot=np.array(Image.open('E:/dump/TEMP/desert_top.png').convert('RGB').rotate(180))
pairs=json.load(open('E:/dump/TEMP/landmarks.json'))   # list of [picX,picY,mapX,mapY] in rotated-map px
A=np.array(pairs,float)
# map_rot -> picture (inverse warp, used to resample the picture into map space), and picture -> map_rot for the track
f_p2m=RBFInterpolator(A[:,:2],A[:,2:],kernel='thin_plate_spline',smoothing=float(sys.argv[1]) if len(sys.argv)>1 else 0.0)
f_m2p=RBFInterpolator(A[:,2:],A[:,:2],kernel='thin_plate_spline',smoothing=float(sys.argv[1]) if len(sys.argv)>1 else 0.0)
H,W=maprot.shape[:2]
gy,gx=np.mgrid[0:H:2,0:W:2]
q=np.c_[gx.ravel(),gy.ravel()].astype(float)
src=f_m2p(q).reshape(gy.shape+(2,)).astype(np.float32)
src=cv2.resize(src,(W,H),interpolation=cv2.INTER_LINEAR)
warped=cv2.remap(pic,src[...,0],src[...,1],cv2.INTER_LINEAR,borderMode=cv2.BORDER_CONSTANT,borderValue=(0,0,0))
Image.fromarray(warped).save('E:/dump/TEMP/pic_on_map.png')
blend=(0.5*maprot+0.5*warped).astype(np.uint8)
Image.fromarray(blend).save('E:/dump/TEMP/pic_map_blend.png')
np.save('E:/dump/TEMP/f_landmarks.npy',A)
print('landmark residuals (map px):',np.abs(f_p2m(A[:,:2])-A[:,2:]).max())
