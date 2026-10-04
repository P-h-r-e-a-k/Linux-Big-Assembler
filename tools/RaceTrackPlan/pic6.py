import numpy as np, json, heapq
from PIL import Image, ImageDraw
from scipy import ndimage as ndi
P='C:/Users/Matt/AppData/Local/Temp/claude/e--dump-LBAAssembler/f838bac2-5bd5-42f7-88e7-5a3650224b3a/images/3.png'
pic=Image.open(P).convert('RGB')
sk=np.load('E:/dump/TEMP/pic_skel2.npy')
road=None
W=[(430,275),(560,245),(700,215),(800,195),(900,178),(985,215),(940,255),(830,275),(760,305),(690,335),(620,360),(560,350),(510,335),(455,375),(480,405),(700,408),(900,408),(1050,418),(1100,450),(1115,520),(1130,600),(1145,633),(1100,600),(1070,530),(1030,490),(940,470),(870,470),(760,468),(700,460),(640,447),(600,452),(570,462),(480,490),(420,505),(340,520),(290,535),(275,550),(300,580),(400,610),(480,640),(515,665),(500,700),(430,725),(340,750),(280,790),(270,820),(310,850),(360,845),(430,800),(520,720),(620,620),(690,540),(700,510),(660,490),(610,483),(560,475),(470,458),(400,440),(360,420),(350,395),(375,365),(395,335),(400,310),(430,275)]
ys,xs=np.where(sk); pts=np.c_[xs,ys]
idx={ (int(x),int(y)):i for i,(x,y) in enumerate(pts)}
def snap(p):
    d=((pts-np.array(p))**2).sum(1); i=int(d.argmin()); return i,float(d[i])**.5
nodes=[snap(p) for p in W]
print('snap distances',[round(d) for _,d in nodes])
def nbrs(i):
    x,y=pts[i]
    for dx in (-1,0,1):
        for dy in (-1,0,1):
            if dx or dy:
                j=idx.get((int(x+dx),int(y+dy)))
                if j is not None: yield j,(1.414 if dx and dy else 1.0)
def path(a,b,banned=()):
    dist={a:0}; prev={}; q=[(0,a)]
    while q:
        d,i=heapq.heappop(q)
        if i==b: break
        if d>dist.get(i,1e9): continue
        for j,w in nbrs(i):
            nd=d+w
            if nd<dist.get(j,1e9): dist[j]=nd; prev[j]=i; heapq.heappush(q,(nd,j))
    out=[b]
    while out[-1]!=a: out.append(prev[out[-1]])
    return out[::-1]
full=[]
for (a,_),(b,_) in zip(nodes[:-1],nodes[1:]):
    p=path(a,b); full+= p if not full else p[1:]
poly=[tuple(pts[i]) for i in full]
print('path px',len(poly))
json.dump([[int(x),int(y)] for x,y in poly],open('E:/dump/TEMP/pic_centerline_px.json','w'))
d=ImageDraw.Draw(pic)
d.line(poly,fill=(255,0,0),width=2)
for k,p in enumerate(W): d.ellipse([p[0]-3,p[1]-3,p[0]+3,p[1]+3],outline=(255,255,0))
pic.save('E:/dump/TEMP/pic_centerline.png')
