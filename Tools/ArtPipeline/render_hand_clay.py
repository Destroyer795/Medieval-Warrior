"""Untextured source close-up to distinguish surface artifacts from topology."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/Marchwarden/Marchwarden.blend'))
def B(v):return Vector((v[0],-v[2],v[1]))
source=json.loads((root/'Tools/ArtPipeline/Marchwarden/rig.json').read_text())
joints={b['name']:Vector(tuple(b['position'].values())) for b in source['bones']}
wrist=joints['Bip001 R Hand'];distal=(wrist-joints['Bip001 R Forearm']).normalized()
across=Vector((0,-1,0));across=(across-distal*across.dot(distal)).normalized();normal=across.cross(distal).normalized()
if normal.z<0:normal=-normal
centre=B((wrist+distal*.061-normal*.008)*1.1)
for obj in bpy.data.objects:obj.hide_render=True
hand=next(o for o in bpy.data.objects if o.name.startswith('Anatomical articulated gripping hand R'))
hand.hide_render=False
clay=bpy.data.materials.new('Untextured hand review');clay.use_nodes=True
shader=clay.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(.42,.26,.18,1);shader.inputs['Roughness'].default_value=.72
hand.data.materials.clear();hand.data.materials.append(clay)
for p in hand.data.polygons:p.material_index=0
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE_NEXT'
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.25,.25,.25,1)
for name,offset,power in [('Key',normal*-.30+distal*.16,18),('Fill',normal*.15-across*.20,8)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=.35
    obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=centre+B(offset);obj.rotation_euler=(centre-obj.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Hand clay camera');cam=bpy.data.objects.new('Hand clay camera',data);scene.collection.objects.link(cam)
cam.location=centre+B(-normal*.32+distal*.10-across*.06);cam.rotation_euler=(centre-cam.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=.18
scene.camera=cam;scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.filepath=str(root/'Artifacts/AcidAndGrips/hand-clay.png');bpy.ops.render.render(write_still=True)
