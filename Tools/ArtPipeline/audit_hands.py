"""Inspect hand topology in the saved Blender source."""
import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/Marchwarden/Marchwarden.blend'))
report=[]
for obj in bpy.data.objects:
    if not obj.name.startswith('Anatomical articulated gripping hand'):continue
    bm=bmesh.new();bm.from_mesh(obj.data)
    rig=json.loads((root/'Tools/ArtPipeline/Marchwarden/rig.json').read_text())
    wrist=Vector(tuple(next(b for b in rig['bones'] if b['name']=='Bip001 '+obj['anatomicalHand']+' Hand')['position'].values()))
    remaining=set(bm.verts);components=[]
    while remaining:
        todo=[remaining.pop()];count=0;points=[]
        while todo:
            v=todo.pop();count+=1;points.append(Vector((v.co.x,v.co.z,-v.co.y)))
            for edge in v.link_edges:
                other=edge.other_vert(v)
                if other in remaining:remaining.remove(other);todo.append(other)
        components.append(count)
        centre=sum(points,Vector())/len(points)
        print('COMPONENT',count,'offset',(centre-wrist)[:],flush=True)
    print('HAND_AUDIT',obj.name,'vertices',len(bm.verts),'components',sorted(components,reverse=True),
          'boundary edges',sum(e.is_boundary for e in bm.edges),'nonmanifold',sum(not e.is_manifold for e in bm.edges),flush=True)
    result=dict(side=obj['anatomicalHand'],vertices=len(bm.verts),components=len(components),
                boundaryEdges=sum(e.is_boundary for e in bm.edges),nonmanifoldEdges=sum(not e.is_manifold for e in bm.edges),
                inconsistentEdges=sum(not e.is_contiguous for e in bm.edges),volume=bm.calc_volume(signed=True))
    assert result['components']==1 and result['nonmanifoldEdges']==0 and result['inconsistentEdges']==0 and result['volume']>0,result
    report.append(result)
    bm.free()
output=root/'Artifacts/AcidAndGrips';output.mkdir(parents=True,exist_ok=True)
(output/'source-topology.json').write_text(json.dumps(report,indent=2))
