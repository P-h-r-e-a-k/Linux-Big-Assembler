import sys; sys.path.insert(0,'/e/dump/TEMP')
from ile import *
x,z=float(sys.argv[1]),float(sys.argv[2])
m,ground,objtex,cubes=load_ile('E:/dump/LBA2RaceTrackBuild/Game/DESERT.ILE')
cx,cz=int(x//64),int(z//64)
c=cubes[m[cz*16+cx]&127]
lx,lz=x-cx*64,z-cz*64
ix,iz=int(lx),int(lz); fx,fz=lx-ix,lz-iz
h=c.heights
y=h[iz,ix]*(1-fx)*(1-fz)+h[iz,ix+1]*fx*(1-fz)+h[iz+1,ix]*(1-fx)*fz+h[iz+1,ix+1]*fx*fz
scene={ (7,8):55,(7,9):56,(7,10):57,(7,11):58,(8,7):59,(8,8):60,(8,9):61,(8,10):62,(8,11):63,(9,7):64,(9,8):65,(9,9):66,(9,10):67,(9,11):68,(10,7):69,(10,8):70,(10,9):71,(10,10):72,(10,11):73}.get((cx,cz))
print(cz*0+scene, int(lx*512), int(y)+150, int(lz*512))
