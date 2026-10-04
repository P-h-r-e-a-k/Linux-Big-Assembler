import numpy as np, cv2
from PIL import Image
from skimage.morphology import skeletonize, disk, closing, remove_small_objects
from scipy import ndimage as ndi
P='C:/Users/Matt/AppData/Local/Temp/claude/e--dump-LBAAssembler/f838bac2-5bd5-42f7-88e7-5a3650224b3a/images/3.png'
im=np.array(Image.open(P).convert('RGB')); r,g,b=[im[...,i].astype(int) for i in range(3)]
lime=(g>200)&(r<140)&(b<120)
yellow=(r>200)&(g>200)&(b<90)
red=(r>200)&(g<60)&(b<60)
road=lime|red
road=closing(road,disk(3))
# yellow pixels sitting between lime pixels (holes in the road) get filled; ones outside stay out
filled=ndi.binary_fill_holes(road|yellow)
road=road|(yellow&closing(road,disk(9)))
road=remove_small_objects(road,max_size=500)
sk=skeletonize(road)
# prune short spurs iteratively
def prune(sk,n):
    sk=sk.copy(); K=np.ones((3,3),int)
    for _ in range(n):
        deg=ndi.convolve(sk.astype(int),K,mode='constant')-1
        ends=sk&(deg<=1)
        sk[ends]=False
    return sk
pr=prune(sk,12)
# regrow: dilate pruned skeleton back along original skeleton for 12 steps from the remaining ends only (keeps true ends)
def regrow(orig,pruned,n):
    cur=pruned.copy(); K=np.ones((3,3),int)
    for _ in range(n):
        deg=ndi.convolve(cur.astype(int),K,mode='constant')-1
        ends=cur&(deg<=1)
        grow=ndi.binary_dilation(ends,structure=np.ones((3,3)))&orig&~cur
        cur=cur|grow
    return cur
res=regrow(sk,pr,12)
vis=np.zeros(im.shape,np.uint8); vis[road]=(60,60,60); vis[res]=(255,255,255)
Image.fromarray(vis).save('E:/dump/TEMP/pic_skel2.png')
np.save('E:/dump/TEMP/pic_skel2.npy',res)
deg=ndi.convolve(res.astype(int),np.ones((3,3),int),mode='constant')-1
print('px',res.sum(),'junction px',int((res&(deg>=3)).sum()))
ys,xs=np.where(res&(deg<=1)); print('endpoints',list(zip(xs.tolist(),ys.tolist())))
