import json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
vertices=[];faces={};group=''
for line in (root/'ArtSource/Marchwarden/Anatomy/MakeHuman-base.obj').read_text().splitlines():
    t=line.split()
    if not t:continue
    if t[0]=='v':vertices.append(Vector(tuple(map(float,t[1:4]))))
    elif t[0]=='g':group=' '.join(t[1:])
    elif t[0]=='f':faces.setdefault(group,[]).append([int(v.split('/')[0])-1 for v in t[1:]])
for name,polygons in faces.items():
    if name.startswith(('joint-r-hand','joint-r-finger','joint-r-elbow')):
        indices={i for face in polygons for i in face};centre=sum((vertices[i] for i in indices),Vector())/len(indices)
        print(name,tuple(round(v,5) for v in centre))
