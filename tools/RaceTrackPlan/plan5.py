import sys; sys.path.insert(0,'E:/dump/TEMP')
from basemap import *
import json, heapq
from scipy.ndimage import gaussian_filter, distance_transform_edt, binary_dilation
from scipy.ndimage import gaussian_filter1d
from plan3 import report
guide=np.array(json.load(open('E:/dump/TEMP/track_cells_raw.json')))
hg,has=heights_grid()
drawn=np.zeros((H,W),bool); col=np.zeros((H,W),bool)
for cz in range(Z0,Z0+NZ):
    for cx in range(X0,X0+NX):
        c=cube_at(cx,cz)
        if c is None or c.polys is None: continue
        ox=(cx-X0)*64; oz=(cz-Z0)*64
        for z in range(64):
            for x in range(64):
                p=int(c.polys[z,x*2]); q=int(c.polys[z,x*2+1])
                drawn[oz+z,ox+x]=bool(((p>>4)&3) or ((p>>6)&3) or ((q>>4)&3) or ((q>>6)&3))
                col[oz+z,ox+x]=bool(((p>>17)&1) or ((q>>17)&1))
REMOVABLE={0,1,2,4,5,6,8,9,10,11,12,13,14,15,16,17,18,23,29,30,31,32,34,35,36,37,39,41,44,45,47,48,50,51,55,56,57,58,59,60,61,62,63,72,73,74,76,77,78,79,80,81,82,83,84,85,86,87,88,89,90,91,93,98,99,101,103,104,105}
big=np.zeros((H,W),bool); small=np.zeros((H,W),bool)
for cz in range(Z0,Z0+NZ):
    for cx in range(X0,X0+NX):
        c=cube_at(cx,cz)
        if c is None: continue
        ox=(cx-X0)*64*512; oz=(cz-Z0)*64*512
        for d in c.decors:
            xmin,ymin,zmin,xmax,ymax,zmax=d[6:12]
            gx0=max(0,int((ox+xmin)/512)); gx1=min(W-1,int((ox+xmax)/512)); gz0=max(0,int((oz+zmin)/512)); gz1=min(H-1,int((oz+zmax)/512))
            body=d[0]&0xffff
            if body in (64,65,66,67,68,69,70,71): continue      # the old track's own pieces are protected by the old-track block below
            (small if body in REMOVABLE else big)[gz0:gz1+1,gx0:gx1+1]=True
old=np.zeros((H,W),bool); old[195:252,3:62]=True   # the retail race track (cube 7,10) stays as it is
slope=np.hypot(*np.gradient(hg))[:H,:W]
HW=4.5
# obstacle costs already include the corridor half-width: dilate obstacles by HW
def dil(a,r):
    d=distance_transform_edt(~a); return d<=r
cost=np.ones((H,W))
cost+=400*dil(big,HW+0.5)
cost+=400*dil(old,HW+4.0)
cost+=5*dil(small,1.5)
cost+=8*dil(col,HW-1.5)
cost+=6*(~drawn)
cost+=4*(slope>420)
# leash to the concept drawing
gm=np.zeros((H,W),bool)
for x,z in guide:
    xi,zi=int(round(x)),int(round(z))
    if 0<=xi<W and 0<=zi<H: gm[zi,xi]=True
gd=distance_transform_edt(~gm)
cost+=np.where(gd>3,((gd-3)/3.0)**2,0)
cost[gd>28]=1e6
np.save('E:/dump/TEMP/plan_cost2.npy',cost)
def dijkstra(a,b):
    INF=1e18; dist=np.full((H,W),INF); prev=-np.ones((H,W),int)
    dist[a[1],a[0]]=0; q=[(0.0,a[0],a[1])]
    while q:
        d,x,z=heapq.heappop(q)
        if d>dist[z,x]: continue
        if (x,z)==b: break
        for dx in (-1,0,1):
            for dz in (-1,0,1):
                if not(dx or dz): continue
                nx,nz=x+dx,z+dz
                if not(0<=nx<W and 0<=nz<H): continue
                w=(1.414 if dx and dz else 1.0)*0.5*(cost[z,x]+cost[nz,nx])
                nd=d+w
                if nd<dist[nz,nx]: dist[nz,nx]=nd; prev[nz,nx]=z*W+x; heapq.heappush(q,(nd,nx,nz))
    out=[b]
    while out[-1]!=a:
        p=prev[out[-1][1],out[-1][0]]; out.append((int(p%W),int(p//W)))
    return out[::-1]
def cheap_near(p,r=6):
    x,z=int(round(p[0])),int(round(p[1])); best=None
    for dx in range(-r,r+1):
        for dz in range(-r,r+1):
            xx,zz=x+dx,z+dz
            if 0<=xx<W and 0<=zz<H:
                c=cost[zz,xx]+ (dx*dx+dz*dz)*0.05
                if best is None or c<best[0]: best=(c,xx,zz)
    return (best[1],best[2])
# anchors along the guide (uniform in index)
n=len(guide); step=max(1,n//60)
anch=[cheap_near(guide[i]) for i in range(0,n,step)]
anch.append(anch[0])
path=[]
for a,b in zip(anch[:-1],anch[1:]):
    if a==b: continue
    p=dijkstra(a,b); path+= p if not path else p[1:]
P=np.array(path,float)
# resample + smooth (periodic)
d=np.r_[0,np.cumsum(np.hypot(*np.diff(P,axis=0).T))]
m=int(d[-1]/1.0); t=np.linspace(0,d[-1],m,endpoint=False)
R=np.c_[np.interp(t,d,P[:,0]),np.interp(t,d,P[:,1])]
S1=np.c_[gaussian_filter1d(R[:,0],5,mode='wrap'),gaussian_filter1d(R[:,1],5,mode='wrap')]
from scipy.spatial import cKDTree
from scipy.ndimage import map_coordinates
def resample_closed(P,step):
    d=np.r_[0,np.cumsum(np.hypot(*np.diff(np.r_[P,P[:1]],axis=0).T))]
    n=max(8,int(round(d[-1]/step))); t=np.linspace(0,d[-1],n,endpoint=False)
    Q=np.r_[P,P[:1]]
    return np.c_[np.interp(t,d,Q[:,0]),np.interp(t,d,Q[:,1])]
R0=resample_closed(S1,1.5); N=len(R0)
guideR=resample_closed(guide,1.5)
tg=cKDTree(guideR); exempt=set()
for i,j in tg.query_pairs(13.0):
    if min(abs(i-j),len(guideR)-abs(i-j))>40: exempt.add((round(guideR[i][0]/8),round(guideR[i][1]/8)))
gy_,gx_=np.gradient(gaussian_filter(np.where(cost>1e5,0,np.minimum(cost,400)),1.5))
P=R0.copy(); SEP=14.0
def near_exempt(p): return (round(p[0]/8),round(p[1]/8)) in exempt or any((round(p[0]/8)+a,round(p[1]/8)+b) in exempt for a in (-2,-1,0,1,2) for b in (-2,-1,0,1,2))
for it in range(500):
    lap=(np.roll(P,1,0)+np.roll(P,-1,0))/2-P
    force=0.3*lap+0.01*(R0-P)
    force-=0.02*np.c_[map_coordinates(gx_,[P[:,1],P[:,0]],order=1,mode='nearest'),map_coordinates(gy_,[P[:,1],P[:,0]],order=1,mode='nearest')]
    tr=cKDTree(P)
    for i,j in tr.query_pairs(SEP):
        if min(abs(i-j),N-abs(i-j))<=40: continue
        if near_exempt(P[i]) and near_exempt(P[j]): continue
        v=P[i]-P[j]; L=np.hypot(*v)+1e-6; push=(SEP-L)*0.2*v/L
        force[i]+=push; force[j]-=push
    P+=np.clip(force,-0.6,0.6)
S1=P
np.save('E:/dump/TEMP/plan_route.npy',S1)
report(S1,'route')
mask=np.zeros((H+1,W+1),bool)
for x,z in S1:
    xi,zi=int(round(x)),int(round(z))
    if 0<=xi<=W and 0<=zi<=H: mask[zi,xi]=True
dist=distance_transform_edt(~mask); corr=dist[:H,:W]<=HW
print('corridor cells',corr.sum(),'sea',(corr&~drawn).sum(),'Col',(corr&col).sum(),'small decor cells',(corr&small).sum(),'big',(corr&big).sum())
im=Image.open('E:/dump/TEMP/base_desert.png').convert('RGB'); dr=ImageDraw.Draw(im)
dr.line([(x*4,y*4) for x,y in np.r_[guide,guide[:1]]],fill=(255,255,0),width=2)
dr.line([(x*4,y*4) for x,y in np.r_[S1,S1[:1]]],fill=(0,255,255),width=3)
im.save('E:/dump/TEMP/plan_route.png')
