"""Update only existing hinge rest pivots; geometry/UVs/finish remain identical."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
path=root/'Tools/ArtPipeline/Marchwarden/meshes.json'
export=json.loads(path.read_text())
for joint in export['armorJoints']:
    if joint['parent']<0:
        joint['pivot']['y']=round(.815+.007*math.sin(joint['angle']*4+.8)*.02**2,7)
bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/Marchwarden/Marchwarden.blend'))
rig=bpy.data.objects['Marchwarden rig'];bpy.context.view_layer.objects.active=rig;rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for index,joint in enumerate(export['armorJoints']):
    if joint['parent']>=0:continue
    pivot=joint['pivot'];bone=rig.data.edit_bones['Tasset %02d'%index]
    target=Vector((pivot['x'],-pivot['z'],pivot['y']));delta=target-bone.head
    bone.head=target;bone.tail+=delta
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'ArtSource/Marchwarden/Marchwarden.blend'),compress=True)
path.write_text(json.dumps(export,separators=(',',':')))
print('Updated 20 belt hinge pivots; rigid plate geometry and baked finish preserved')
