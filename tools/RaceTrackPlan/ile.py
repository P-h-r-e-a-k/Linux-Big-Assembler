import struct, numpy as np
def hqr_entries(path):
    d=open(path,'rb').read()
    n=struct.unpack('<I',d[:4])[0]//4
    offs=struct.unpack('<%dI'%n,d[:n*4])
    def read(i):
        p=offs[i]
        if i>=n-1 or p==0: return None
        size,packed,method=struct.unpack('<IIH',d[p:p+10]); p+=10
        if method==0: return d[p:p+size]
        out=bytearray(); 
        while len(out)<size:
            flags=d[p]; p+=1
            for bit in range(8):
                if len(out)>=size: break
                if flags&(1<<bit): out.append(d[p]); p+=1
                else:
                    lo,hi=d[p],d[p+1]; p+=2
                    L=(lo&15)+method+1; dist=((hi<<4)|(lo>>4))+1
                    for j in range(L):
                        if len(out)>=size: break
                        out.append(out[-dist])
        return bytes(out)
    return read, n-1
class Cube: pass
def load_ile(path):
    read,count=hqr_entries(path)
    m=read(0)[:256]; ground=np.frombuffer(read(1)[:65536],dtype=np.uint8).reshape(256,256); objtex=np.frombuffer(read(2)[:65536],dtype=np.uint8).reshape(256,256)
    cubes={}
    for cid in range(1,128):
        s=3+6*(cid-1)
        if s+5>=count: break
        h=read(s+4)
        if h is None or len(h)<65*65*2: continue
        c=Cube(); c.id=cid
        c.heights=np.frombuffer(h[:65*65*2],dtype='<i2').reshape(65,65)
        inf=read(s); c.info=list(struct.unpack('<%di'%(min(10,len(inf)//4)),inf[:min(10,len(inf)//4)*4])) if inf else []
        dob=read(s+1); c.decors=[]
        if dob and c.info:
            n=c.info[2]; stride=len(dob)//n if n and len(dob)%n==0 else 48; stride=max(stride,48)
            for i in range(0,len(dob)-stride+1,stride): c.decors.append(struct.unpack('<12i',dob[i:i+48]))
        g=read(s+2); c.polys=np.frombuffer(g[:64*64*2*4],dtype='<u4').reshape(64,128) if g and len(g)>=64*64*8 else None   # [z][x*2+half]
        t=read(s+3); c.texdefs=np.frombuffer(t[:len(t)//2*2],dtype='<u2') if t else np.zeros(0,dtype='<u2')
        l=read(s+5); c.light=np.frombuffer(l[:65*65],dtype=np.uint8).reshape(65,65)&15 if l and len(l)>=65*65 else np.full((65,65),15,np.uint8)
        cubes[cid]=c
    return m,ground,objtex,cubes
def palette(ress,index):
    read,_=hqr_entries(ress); x=read(index); off=struct.unpack('<i',x[4:8])[0]; return np.frombuffer(x[off:off+768],dtype=np.uint8).reshape(256,3)
