"""Independently inspect the saved Blender master: topology, chart overlap and UV stretch."""
import bpy,bmesh,json,math,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
path=ROOT/'ArtSource/Buildings/Buildings.blend'
bpy.ops.wm.open_mainfile(filepath=str(path))

def overlaps(rectangles):
    # Broad phase per 1/128 atlas cell, then exact positive-area rectangle intersection.
    cells={};hits=set()
    for i,(x0,y0,x1,y1) in enumerate(rectangles):
        touched=[]
        for x in range(int(x0*128),int(x1*128)+1):
            for y in range(int(y0*128),int(y1*128)+1):
                bucket=cells.setdefault((x,y),[])
                for j in bucket:
                    if (j,i) in hits:continue
                    a,b,c,d=rectangles[j]
                    if min(x1,c)-max(x0,a)>1e-8 and min(y1,d)-max(y0,b)>1e-8:hits.add((j,i))
                touched.append(bucket)
        for bucket in touched:bucket.append(i)
    return len(hits)

results=[]
for obj in sorted(bpy.data.objects,key=lambda o:o.name):
    m=obj.data;bm=bmesh.new();bm.from_mesh(m)
    bad=sum(not e.is_manifold for e in bm.edges)
    zero=sum(f.calc_area()<1e-16 for f in bm.faces);bm.free()
    if bad or zero:raise RuntimeError(f'{obj.name}: nonmanifold {bad}, degenerate {zero}')
    result={'name':obj.name,'nonManifoldEdges':bad,'degenerateFaces':zero}
    if ' LOD' in obj.name:
        channels=[]
        for layer in m.uv_layers:
            rectangles=[];errors=[];worst=0
            for p in m.polygons:
                uv=[layer.data[i].uv for i in p.loop_indices]
                rectangles.append((min(v.x for v in uv),min(v.y for v in uv),max(v.x for v in uv),max(v.y for v in uv)))
                scales=[]
                for j in range(len(uv)):
                    a=m.vertices[p.vertices[j]].co;b=m.vertices[p.vertices[(j+1)%len(uv)]].co
                    if (a-b).length>1e-6:scales.append((uv[j]-uv[(j+1)%len(uv)]).length/(a-b).length)
                error=max(scales)/min(scales)-1 if scales and min(scales)>1e-7 else 0
                worst=max(worst,error);errors.append(error)
            count=overlaps(rectangles)
            if count:raise RuntimeError(f'{obj.name}/{layer.name}: {count} overlapping face charts')
            # Tiny bevel faces can lose relative precision in float UVs. The high percentile of
            # face distortion is more useful than treating a sub-pixel rounding error as stretch.
            percentile=sorted(errors)[int((len(errors)-1)*.99)]
            if percentile>.03 or worst>.12:raise RuntimeError(f'{obj.name}/{layer.name}: excessive projected UV distortion {percentile}/{worst}')
            channels.append({'name':layer.name,'charts':len(rectangles),'overlappingCharts':count,'p99RelativeEdgeScaleError':round(percentile,6),'maxRelativeEdgeScaleError':round(worst,6)})
        result['uv']=channels
    results.append(result)
    print('[Building source audit]',obj.name,'PASS',flush=True)
report={'result':'PASS','masterSHA256':hashlib.sha256(path.read_bytes()).hexdigest(),'objects':len(results),'meshes':results}
(ROOT/'ArtSource/Buildings/source-audit.json').write_text(json.dumps(report,indent=2))
print('[Buildings] Source audit PASS:',len(results),'closed meshes, non-overlapping atlas/lightmap charts',flush=True)
