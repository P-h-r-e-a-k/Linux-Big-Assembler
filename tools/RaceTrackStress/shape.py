import re, math, json, os
keys=[l.split("'")[1] for l in open('stress.py',encoding='utf-8') if l.strip().startswith("('") and "ILE" in l]
out={}
for k in keys:
    race=open(f'{k}/race.txt',encoding='utf-8').read()
    m=re.search(r'^(?:opponent|guide)_path=(.*)$',race,re.M)
    pts=[list(map(int,l.split())) for l in open(m.group(1).strip()) if len(l.split())>=5]
    n=len(pts); L=0; seg=[]
    for i in range(n):
        a=pts[i]; b=pts[(i+1)%n]
        d=math.hypot(b[0]-a[0],b[1]-a[1]); L+=d; seg.append((d,a[4]))
    cell=512
    straight=sum(d for d,r in seg if r>=40*cell)/L
    tight=sum(d for d,r in seg if r<8*cell)/L
    # bends: runs of points with radius < 12 cells, counted once each; the tightest radius in each
    bends=[]; cur=None
    for d,r in seg:
        if r<12*cell:
            cur=min(cur,r) if cur is not None else r
        elif cur is not None: bends.append(cur); cur=None
    if cur is not None: bends.append(cur)
    # the longest stretch with no bend under 40 cells
    best=run=0
    for d,r in seg+seg:
        run=run+d if r>=40*cell else 0; best=max(best,run)
    best=min(best,L)
    ys=[p[2] for p in pts]
    jumps=len(re.findall(r'^arcjump=',race,re.M)); loops=len(re.findall(r'^loop=',race,re.M))
    out[k]=dict(length_cells=round(L/cell), straight=round(straight*100), tight=round(tight*100), bends=len(bends),
               hairpins=sum(1 for r in bends if r<5*cell), min_r=round(min(r for d,r in seg)/cell,1), longest_straight=round(best/cell),
               climb=round((max(ys)-min(ys))/cell,1), jumps=jumps, loops=loops)
    o=out[k]
    print(f"{k:12} {o['length_cells']:5} cells  straight {o['straight']:3}%  tight {o['tight']:3}%  bends {o['bends']:3} (hairpins {o['hairpins']:2}, min R {o['min_r']:5})  longest straight {o['longest_straight']:4}  climb {o['climb']:5}  jumps {jumps} loops {loops}")
json.dump(out,open('shape.json','w'),indent=1)
