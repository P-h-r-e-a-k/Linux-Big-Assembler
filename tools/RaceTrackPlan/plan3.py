import sys; sys.path.insert(0,'E:/dump/TEMP')
from basemap import *
import json
from scipy.ndimage import distance_transform_edt
REM={0,1,2,4,5,6,8,9,10,11,12,13,14,15,16,17,18,23,29,30,31,32,34,35,36,37,39,41,44,45,47,48,50,51,55,56,57,58,59,60,61,62,63,72,73,74,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90,91,93,98,99,101,103,104,105}
def report(P,label,hw=4.5):
    mask=np.zeros((H+1,W+1),bool)
    for x,z in P:
        xi,zi=int(round(x)),int(round(z))
        if 0<=xi<=W and 0<=zi<=H: mask[zi,xi]=True
    dist=distance_transform_edt(~mask)
    out=[]
    for cz in range(Z0,Z0+NZ):
        for cx in range(X0,X0+NX):
            c=cube_at(cx,cz)
            if c is None: continue
            ox=(cx-X0)*64*512; oz=(cz-Z0)*64*512
            for i,d in enumerate(c.decors):
                xmin,ymin,zmin,xmax,ymax,zmax=d[6:12]
                gx0=max(0,int((ox+xmin)/512)); gx1=min(W-1,int((ox+xmax)/512)); gz0=max(0,int((oz+zmin)/512)); gz1=min(H-1,int((oz+zmax)/512))
                sub=dist[gz0:gz1+1,gx0:gx1+1]
                if sub.size and sub.min()<=hw:
                    area=(gx1-gx0+1)*(gz1-gz0+1)
                    if (d[0]&0xffff) in REM: continue
                    out.append((area,cx,cz,i,d[0]&0xffff,gx0,gz0,gx1,gz1,d[10]-d[7]))
    out.sort(reverse=True)
    print(label,'decors in corridor:',len(out),' solid decors:',len(out))
    for o in out:
        print('   area',o[0],'cube',o[1:3],'idx',o[3],'body',o[4],'cells',o[5:9],'height',o[9])
if __name__=='__main__':
    g=np.array(json.load(open('E:/dump/TEMP/track_cells_raw.json')))
    report(g,'guide')
    report(np.load('E:/dump/TEMP/plan_snake.npy'),'snake')
