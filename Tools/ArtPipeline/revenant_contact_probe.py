"""Print authored sword endpoints in skeleton space for contact review."""
import json
from pathlib import Path
from mathutils import Matrix,Vector
root=Path(__file__).resolve().parents[2]/'Tools/ArtPipeline/Revenants'
rig=json.loads((root/'rig.json').read_text())['bones'];actions=json.loads((root/'actions.json').read_text())['actions'];parts=json.loads((root/'meshes.json').read_text())['parts']
def M(m):return Matrix([[m['e%d%d'%(i,j)] for j in range(4)] for i in range(4)])
def V(v):return Vector(tuple(v[k] for k in 'xyz'))
index=next(i for i,b in enumerate(rig) if b['name']=='Base HumanRArmPalm');inverse=M(rig[index]['frame']).inverted()
for kind,time,scale,reach in [('Warlord',.8,1.85,1.9),('Swordsman',.48,1.06,1.65),('Bulwark',.62,1.08,1.65)]:
    p=next(p for p in parts if p['name']==kind);g=next(g for g in p['grips'] if g['side']=='R')
    base=inverse@(V(g['position'])+V(g['along'])*.075);tip=inverse@V(g['bladeTip'])
    for a in [a for a in actions if a['form']==kind and a['name'].endswith(('_Cut','_Thrust'))]:
        sample=min(a['samples'],key=lambda s:abs(s['time']-time));frame=M(sample['frames'][index]);b=frame@base;t=frame@tip
        print(a['name'],'hand',tuple(round(v,3) for v in frame.translation),'base',tuple(round(v,3) for v in b),'tip',tuple(round(v,3) for v in t),'target',(0,round(1.1/scale,3),round(reach*.9/scale,3)),flush=True)
