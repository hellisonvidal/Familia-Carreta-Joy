import struct, sys, json, numpy as np, matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection, LineCollection
d=open('/home/user/familia-carreta-joy/unity/Assets/Resources/Map/pg_map.bytes','rb').read()
o=0
def R(fmt):
    global o; v=struct.unpack_from(fmt,d,o); o+=struct.calcsize(fmt); return v
def S():
    global o; n=d[o]; o+=1; s=d[o:o+n].decode(); o+=n; return s
assert d[:4]==b'PGM1'; o=4
CH,E0,_,n=R('<fffI'); idx=[R('<hhII') for _ in range(n)]
L=[(R('<B'),S(),S(),R('<fff')) for _ in range(R('<I')[0])]
labels=[(S(),R('<ff')) for _ in range(R('<I')[0])]
route=[R('<ff') for _ in range(R('<I')[0])]
stops=[(S(),R('<ff')) for _ in range(R('<I')[0])]
base=o
cx,cz,rad=[float(a) for a in sys.argv[1:4]]; out=sys.argv[4]
AC={0:'#9bc77a',1:'#4f9a45',2:'#2f6b2f',3:'#3b7dd8',4:'#5fb85a',5:'#999',6:'#8a9a7a',7:'#7fb36f',8:'#d9cbb0'}
BC={0:'#e9a46a',1:'#b9b9c9',2:'#e5d38a',3:'#cfa',4:'#999',5:'#ff60c0',6:'#7ac',7:'#fff'}
polys=[];pc=[];bp=[];bc=[];rl=[];rw=[];tr=[];wl=[]
cnt=dict(r=0,b=0,a=0,t=0)
for ci,cj,off,ln in idx:
    if abs((ci+.5)*CH-cx)>rad+CH or abs((cj+.5)*CH-cz)>rad+CH: continue
    o=base+off
    for _ in range(R('<H')[0]):
        t,w,unp,np_=R('<BfBH'); sw,=R('<B'); pts=[R('<ff') for _ in range(np_)]; rl.append(pts); rw.append(w); cnt['r']+=1
    for _ in range(R('<H')[0]):
        w,np_=R('<fH'); wl.append([R('<ff') for _ in range(np_)])
    for _ in range(R('<H')[0]):
        k,h,roof,col,np_=R('<BfBBH'); pts=[R('<ff') for _ in range(np_)]; ni,=R('<H'); o+=2*ni; bp.append(pts); bc.append(BC.get(k,'#ccc')); cnt['b']+=1
    for _ in range(R('<H')[0]):
        k,nv=R('<BH'); v=np.frombuffer(d,'<f4',nv*2,o).reshape(-1,2); o+=nv*8; ni,=R('<I'); o+=ni*(2 if nv<65535 else 4); polys.append(v); pc.append(AC[k]); cnt['a']+=1
    for _ in range(R('<H')[0]):
        x,z,k=R('<ffB'); tr.append((x,z,k)); cnt['t']+=1
print(cnt, 'route km', round(sum(np.hypot(np.diff([p[0] for p in route]),np.diff([p[1] for p in route])))/1000,1))
fig,ax=plt.subplots(figsize=(12,12),dpi=100); ax.set_facecolor('#c8d8b0')
ax.add_collection(PolyCollection(polys,facecolors=pc,edgecolors='none'))
ax.add_collection(LineCollection(wl,colors='#3b7dd8',linewidths=1.5))
ax.add_collection(LineCollection(rl,colors='#555',linewidths=[w*0.55*600/rad for w in rw]))
ax.add_collection(PolyCollection(bp,facecolors=bc,edgecolors='#333',linewidths=0.2))
if tr: t=np.array(tr); ax.scatter(t[:,0],t[:,1],s=6*600/rad,c=np.where(t[:,2]==0,'#1d4d1d','#2f8f2f'),marker='^')
r=np.array(route); ax.plot(r[:,0],r[:,1],color='#ff00aa',lw=2.5,alpha=0.8)
for n,(x,z) in stops: ax.plot(x,z,'*',color='yellow',ms=16,mec='k')
for (_,),k,n,(x,z,rr) in L: ax.annotate(n,(x,z),fontsize=10,weight='bold',bbox=dict(fc='white',alpha=0.8))
for n,(x,z) in labels: ax.text(x,z,n,fontsize=14,weight='bold',color='navy',ha='center',alpha=0.7)
ax.set_xlim(cx-rad,cx+rad); ax.set_ylim(cz-rad,cz+rad); ax.set_aspect('equal'); plt.tight_layout(); plt.savefig(out)
