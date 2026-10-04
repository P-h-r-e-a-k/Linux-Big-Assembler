import sys; sys.path.insert(0,'E:/dump/TEMP')
from ile import *
from PIL import Image, ImageDraw
G='E:/GOG Games/Little Big Adventure 2 - Level viewer/'
X0,Z0,NX,NZ=7,7,4,5      # cube range of the Desert island
S=4                        # px per cell
m,ground,objtex,cubes=load_ile('E:/dump/LBA2RaceTrackBuild/Pristine/DESERT.ILE')   # the untouched island: the Level viewer install may have a track built into it
pal=palette(G+'RESS.HQR',29)
if pal.max()<=63: pal=(pal.astype(int)*4).clip(0,255).astype(np.uint8)
W=NX*64; H=NZ*64
def cube_at(cx,cz):
    cid=m[cz*16+cx]&127
    return cubes.get(cid)
def heights_grid():
    hg=np.zeros((H+1,W+1),np.float32); has=np.zeros((H+1,W+1),bool)
    for cz in range(Z0,Z0+NZ):
        for cx in range(X0,X0+NX):
            c=cube_at(cx,cz)
            if c is None: continue
            ox=(cx-X0)*64; oz=(cz-Z0)*64
            hg[oz:oz+65,ox:ox+65]=c.heights; has[oz:oz+65,ox:ox+65]=True
    return hg,has
def render(hillshade=True,decors=True):
    img=np.zeros((H*S,W*S,3),np.uint8); img[:]=(20,50,90)
    hg,has=heights_grid()
    for cz in range(Z0,Z0+NZ):
        for cx in range(X0,X0+NX):
            c=cube_at(cx,cz)
            if c is None or c.polys is None: continue
            ox=(cx-X0)*64; oz=(cz-Z0)*64
            for z in range(64):
                for x in range(64):
                    p=int(c.polys[z,x*2]); tex=(p>>4)&3; poly=(p>>6)&3; idx=(p>>19)&0x1FFF; bank=p&15
                    if tex and idx*6+6<=len(c.texdefs):
                        t=c.texdefs[idx*6:idx*6+6].astype(int); u=(t[0]+t[2]+t[4])/3/256; v=(t[1]+t[3]+t[5])/3/256
                        col=pal[ground[int(min(255,v)),int(min(255,u))]]
                    elif poly: col=pal[(bank<<4)+11]
                    else: col=(20,50,90)
                    px=(ox+x)*S; pz=(oz+z)*S
                    img[pz:pz+S,px:px+S]=col
    if hillshade:
        gy,gx=np.gradient(hg)
        sh=np.clip(1+ (gx*0.5 - gy*0.5)/120.0,0.6,1.4)
        shc=np.kron(sh[:H,:W],np.ones((S,S)))[...,None]
        img=np.clip(img*shc,0,255).astype(np.uint8)
    im=Image.fromarray(img)
    if decors:
        d=ImageDraw.Draw(im)
        for cz in range(Z0,Z0+NZ):
            for cx in range(X0,X0+NX):
                c=cube_at(cx,cz)
                if c is None: continue
                ox=(cx-X0)*64*512; oz=(cz-Z0)*64*512
                for dcr in c.decors:
                    xmin,ymin,zmin,xmax,ymax,zmax=dcr[6:12]
                    a=((ox+xmin)/512*S,(oz+zmin)/512*S); b=((ox+xmax)/512*S,(oz+zmax)/512*S)
                    d.rectangle([a[0],a[1],b[0],b[1]],outline=(255,0,255))
    return im
if __name__=='__main__':
    im=render(); im.save('E:/dump/TEMP/base_desert.png'); print(im.size)
