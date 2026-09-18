#!/usr/bin/env python3
"""Numpy mirror of Assets/Scripts/Connectome/LifKernel.cs for quick calibration outside Unity."""
import json, math, sys, numpy as np

P = dict(dt=0.1, tau=20.0, tau_syn=5.0, v_rest=-52.0, v_th=-45.0, v_reset=-52.0, t_ref=2.2, w_syn=0.275, noise=0.6)
T = dict(lc4=70, lc4_half=400, lplc2=70, lplc2_half=35, lplc1=45, lc6=40, lc16=25, lc22=15, lc15=10, rear=0.45, min_angle=0.5, expand_half=150, dn_gain=0.5)
KIND = {'LC4':1,'LPLC2':2,'LPLC1':3,'LC6':4,'LC16':5,'LC22':6,'LC15':7,'DNp01':10,'DNp02':11,'DNp04':12,'DNp06':13,'DNp11':14}

d = json.load(open('Assets/Resources/Connectome/escape_circuit.json'))
N = len(d['neurons'])
kind = np.array([KIND.get(n['type'],0) for n in d['neurons']])
side = np.array([-1 if n['side']=='left' else 1 if n['side']=='right' else 0 for n in d['neurons']])
rng = np.random.default_rng(12345)
hetero = rng.uniform(0.55,1.45,N)
W = np.zeros((N,N),np.float32)
for e in d['edges']:
    W[e['pre'],e['post']] += e['sign']*e['syn']*P['w_syn']*(T['dn_gain'] if kind[e['post']]>=10 else 1.0)
gf = np.where(kind==10)[0]; longdn = np.where(kind>10)[0]

def stim(kind_='approach', r=0.06, d0=0.5, vw=0.7, d1=0.3, vs=7.0, growth=0.0, vis=1.0):
    return dict(kind=kind_, r=r, d0=d0, vw=vw, d1=d1, vs=vs, growth=growth, vis=vis)
def wind_ms(s): return (s['d0']-s['d1'])/s['vw']*1000 if s['vw']>0 else 0
def impact_ms(s):
    if s['kind']=='cloud': return max(0,(s['d0']-s['r'])/s['growth'])*1000
    return wind_ms(s)+s['d1']/s['vs']*1000
def dist(s,t):
    if s['kind']=='cloud': return max(0, s['d0']-(s['r']+s['growth']*t*1e-3))
    w=wind_ms(s)
    return s['d0']-s['vw']*t*1e-3 if t<w else max(0, s['d1']-s['vs']*(t-w)*1e-3)
def radius(s,t): return s['r']+s['growth']*t*1e-3 if s['kind']=='cloud' else s['r']
def angle(s,t):
    dd=dist(s,t); return 180.0 if dd<=1e-5 else math.degrees(2*math.atan(radius(s,t)/dd))
def angvel(s,t): return max(0,(angle(s,t+0.5)-angle(s,max(0,t-0.5)))*1000)

def run(s, az=0.0, seed=1):
    rng = np.random.default_rng(seed)
    end = impact_ms(s)+10
    t0 = 0.0
    if angle(s,0) < T['min_angle']:
        lo,hi=0.0,end
        for _ in range(40):
            mid=(lo+hi)/2
            if angle(s,mid)>=T['min_angle']: hi=mid
            else: lo=mid
        t0=hi
    steps=int(math.ceil((end-t0)/P['dt']))
    azr=math.radians(az); lateral=math.sin(azr); front=math.cos(azr)
    rear = 1+(T['rear']-1)*max(0,-front)
    wr=min(1,max(0,0.6+0.4*lateral))*rear; wl=min(1,max(0,0.6-0.4*lateral))*rear
    eye = np.where(side<0,wl,np.where(side>0,wr,(wl+wr)/2))
    v=np.full(N,P['v_rest']); isyn=np.zeros(N); ref=np.zeros(N)
    decay=math.exp(-P['dt']/P['tau_syn']); dtt=P['dt']/P['tau']; ns=P['noise']*math.sqrt(P['dt'])*3.4641
    first={}; counts=np.zeros(N,int); spikes=[]
    for st in range(steps):
        t=t0+st*P['dt']; a=angle(s,t); av=angvel(s,t)
        gate=av/(av+T['expand_half']); sz=a/(a+T['lplc2_half'])*gate; ve=av/(av+T['lc4_half']); mix=0.5*(sz+ve)
        small = min(1,a/6)*(1-a/15) if a<15 else 0
        drive = {1:T['lc4']*ve,2:T['lplc2']*sz,3:T['lplc1']*mix,4:T['lc6']*mix,5:T['lc16']*mix,6:T['lc22']*small,7:T['lc15']*small}
        iext=np.zeros(N)
        for k,dv in drive.items():
            m=kind==k; iext[m]=dv*s['vis']*eye[m]*hetero[m]
        cur=isyn.copy(); isyn*=decay
        inref=ref>0
        ref[inref]-=P['dt']; v[inref]=P['v_reset']
        act=~inref
        v[act]+= (-(v[act]-P['v_rest'])+cur[act]+iext[act])*dtt + (rng.random(act.sum())-0.5)*ns
        sp=(v>=P['v_th'])&act
        if sp.any():
            idx=np.where(sp)[0]
            v[idx]=P['v_reset']; ref[idx]=P['t_ref']; counts[idx]+=1
            isyn+=W[idx].sum(0)
            for i in idx:
                if i in gf or i in longdn:
                    first.setdefault(i,t)
    return dict(t0=t0, impact=impact_ms(s), gf=[first.get(i,-1) for i in gf], long=[first.get(i,-1) for i in longdn], long_counts=[int(counts[i]) for i in longdn], vpn_rate={k:counts[kind==k].sum()/max(1,(kind==k).sum())/((end-t0)/1000) for k in range(1,8)})

def sweep(**kw):
    T.update(kw)
    global W
    W[:]=0
    for e in d['edges']: W[e['pre'],e['post']] += e['sign']*e['syn']*P['w_syn']*(T['dn_gain'] if kind[e['post']]>=10 else 1.0)

if __name__=='__main__':
    import ast
    if len(sys.argv)>1: sweep(**json.loads(sys.argv[1]))
    print(T)
    cases = {
     'hand': stim(r=0.05,d0=0.45,vw=0.5,d1=0.25,vs=3.5),
     'swatter': stim(r=0.06,d0=0.5,vw=0.7,d1=0.3,vs=7,vis=0.7),
     'racket': stim(r=0.09,d0=0.5,vw=0.5,d1=0.3,vs=3.5,vis=0.6),
     'vacuum': stim(r=0.02,d0=0.5,vw=0.3,d1=0.12,vs=0.5),
     'bullet': stim(r=0.0045,d0=3,vw=0,d1=3,vs=360),
     'rocket': stim(r=0.04,d0=10,vw=0,d1=10,vs=115),
     'spray': stim('cloud',r=0.02,d0=0.4,growth=0.7,vis=0.35),
    }
    for name,s in cases.items():
        for az in (0,180):
            r=run(s,az)
            gfmin=min([x for x in r['gf'] if x>=0], default=-1)
            lmin=min([x for x in r['long'] if x>=0], default=-1)
            print(f"{name:8s} az={az:3d} impact={r['impact']:7.1f}ms simStart={r['t0']:6.1f} GF={gfmin:7.1f} (lead {r['impact']-gfmin if gfmin>=0 else float('nan'):6.1f}ms) longDN={lmin:7.1f} cnt={sum(r['long_counts'])} rates={ {k:round(v) for k,v in r['vpn_rate'].items()} }")
