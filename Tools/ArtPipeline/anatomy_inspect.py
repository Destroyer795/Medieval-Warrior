import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
verts=[];groups={};group=''
for line in (root/'ArtSource/Marchwarden/Anatomy/MakeHuman-base.obj').read_text().splitlines():
    fields=line.split()
    if not fields:continue
    if fields[0]=='v':verts.append(tuple(map(float,fields[1:4])))
    elif fields[0]=='g':group=' '.join(fields[1:])
    elif fields[0]=='f':groups.setdefault(group,[]).extend(int(f.split('/')[0])-1 for f in fields[1:])
for group,indices in groups.items():
    points=[verts[i] for i in set(indices)]
    bounds=[(min(p[a] for p in points),max(p[a] for p in points)) for a in range(3)]
    print(group,len(set(indices)),bounds)
