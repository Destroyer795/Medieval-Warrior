"""Parent and orient the editable Blender armature without changing its rest mesh.

The Unity mesh import uses its exact exported bind matrices. This hierarchy makes
the corresponding Blender source convenient to pose and animate by joint name.
"""
import bpy
import sys
from pathlib import Path
from mathutils import Vector


def configure_source_rig(armature):
    bpy.context.view_layer.objects.active=armature
    armature.select_set(True)
    if armature.mode!='EDIT':bpy.ops.object.mode_set(mode='EDIT')
    bones=armature.data.edit_bones
    parents={'Spine':'Pelvis','Neck':'Spine','Head':'Neck'}
    children={'Pelvis':'Spine','Spine':'Neck','Neck':'Head'}
    for side in ('L','R'):
        for child,parent in (('Thigh','Pelvis'),('Calf',side+' Thigh'),('Foot',side+' Calf'),
                             ('Clavicle','Spine'),('UpperArm',side+' Clavicle'),
                             ('Forearm',side+' UpperArm'),('Hand',side+' Forearm')):
            parents[side+' '+child]=parent
        for parent,child in (('Thigh','Calf'),('Calf','Foot'),('Clavicle','UpperArm'),('UpperArm','Forearm'),('Forearm','Hand')):
            children[side+' '+parent]=side+' '+child
    for short,parent in parents.items():
        bone=bones['Bip001 '+short]
        bone.parent=bones['Bip001 '+parent]
        bone.use_connect=False
    for bone in bones:
        short=bone.name.removeprefix('Bip001 ')
        if short in children:bone.tail=bones['Bip001 '+children[short]].head
        elif short.endswith('Foot'):bone.tail=bone.head+Vector((0,-.13,0))
        elif short.endswith('Hand'):bone.tail=bone.head+Vector((-.055 if short.startswith('L') else .055,0,-.035))
        else:bone.tail=bone.head+Vector((0,0,.10))
    bpy.ops.object.mode_set(mode='OBJECT')


if '--repair-source-rig' in sys.argv:
    root=Path(__file__).resolve().parents[2]
    source=root/'ArtSource/Marchwarden/Marchwarden.blend'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    armature=bpy.data.objects['Marchwarden rig']
    body=list(bpy.data.collections['Marchwarden / Body'].objects)
    def evaluated_positions():
        bpy.context.view_layer.update()
        deps=bpy.context.evaluated_depsgraph_get()
        return [(obj.name,[obj.matrix_world@v.co for v in obj.evaluated_get(deps).data.vertices]) for obj in body]
    before=evaluated_positions()
    configure_source_rig(armature)
    after=dict(evaluated_positions())
    drift=max(((point-after[name][i]).length for name,points in before for i,point in enumerate(points)),default=0)
    if drift>1e-5:raise RuntimeError('Source rig changed the rest mesh: '+str(drift))
    bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
    print('MARCHWARDEN editable hierarchy: 18 joints; maximum rest-mesh drift:',drift,flush=True)
