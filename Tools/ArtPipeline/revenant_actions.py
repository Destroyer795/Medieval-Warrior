"""Attach the reviewed Unity skeletal actions to the editable Blender rigs.

Unity's exact bind -> animated frame deltas are conjugated into Blender axes.
This preserves the armature's editable roll and the mesh's rest shape. Every
action is local to its own rig; the eight-character lineup stays in place.
"""
import bpy,json,math,sys
from pathlib import Path
from mathutils import Matrix,Vector
ROOT=Path(__file__).resolve().parents[2]
path=ROOT/'ArtSource/Revenants/Revenants.blend'
bpy.ops.wm.open_mainfile(filepath=str(path))
for action in list(bpy.data.actions):
    if ' / ' in action.name:bpy.data.actions.remove(action,do_unlink=True)
for name in ('Large warm softbox','Cool fill','Rim','Studio ground','Roster presentation'):
    if name in bpy.data.objects:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
rig=json.loads((ROOT/'Tools/ArtPipeline/Revenants/rig.json').read_text())['bones']
actions=json.loads((ROOT/'Tools/ArtPipeline/Revenants/actions.json').read_text())['actions']
def matrix(m):return Matrix([[m['e%d%d'%(i,j)] for j in range(4)] for i in range(4)])
axis=Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
inverse=axis.inverted();bind=[matrix(b['frame']).inverted() for b in rig]
order=[]
def visit(i):
    p=rig[i]['parent']
    if p>=0 and p not in order:visit(p)
    if i not in order:order.append(i)
for i in range(len(rig)):visit(i)
bpy.context.scene.render.fps=30
for kind in ['Walker','Runner','Brute','Spitter','Bloater','Warlord','Swordsman','Bulwark']:
    ao=bpy.data.objects[kind+' rig'];ao.animation_data_create()
    for pb in ao.pose.bones:pb.rotation_mode='QUATERNION'
    for action in actions:
        if action['form'] not in (kind,'Shared'):continue
        a=bpy.data.actions.new(kind+' / '+action['name']);a.use_fake_user=True;ao.animation_data.action=a
        for sample in action['samples']:
            frame=sample['time']*30
            desired={i:axis@matrix(sample['frames'][i])@bind[i]@inverse@ao.data.bones[rig[i]['name']].matrix_local for i in order}
            for i in order:
                pb=ao.pose.bones[rig[i]['name']]
                rest=ao.data.bones[pb.name].matrix_local;p=rig[i]['parent']
                pb.matrix_basis=rest.inverted()@desired[i] if p<0 else rest.inverted()@ao.data.bones[rig[p]['name']].matrix_local@desired[p].inverted()@desired[i]
                for prop in ('location','rotation_quaternion','scale'):pb.keyframe_insert(prop,frame=frame,group=pb.name)
        for curve in a.fcurves:
            for key in curve.keyframe_points:key.interpolation='LINEAR'
    ao.animation_data.action=next(a for a in bpy.data.actions if a.name==kind+' / '+kind+'_Guard')

# Match Unity's anatomical skin maps and cutout strands in the editable source.
texture_dir=ROOT/'Assets/Resources/Revenants/Textures'
images={}
for label in ('Albedo','Normal','Surface','Skin0','Skin1','Skin2','Skin3','SkinNormal'):
    im=bpy.data.images.load(str(texture_dir/(label+'.png')),check_existing=False);im.name='Revenant reviewed '+label
    if label in ('Normal','Surface','SkinNormal'):im.colorspace_settings.name='Non-Color'
    im.pack();images[label]=im
palette=['Necrotic skin','Burial linen','Oxidised iron','Old oxblood leather','Exposed bone','Splintered oak','Tarnished bronze','Clouded eyes','Matted hair','Scalp stubble','Clotted blood','Exposed muscle']
for index,name in enumerate(palette):
    if name not in bpy.data.materials:continue
    mat=bpy.data.materials[name];nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
    bs=nodes.new('ShaderNodeBsdfPrincipled');output=nodes.new('ShaderNodeOutputMaterial');links.new(bs.outputs[0],output.inputs['Surface'])
    bs.inputs['Specular IOR Level'].default_value=0
    vc=nodes.new('ShaderNodeVertexColor');vc.layer_name='Decay'
    texcoord=nodes.new('ShaderNodeTexCoord');scale=nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=3;links.new(texcoord.outputs['UV'],scale.inputs[0])
    fract=nodes.new('ShaderNodeVectorMath');fract.operation='FRACTION';links.new(scale.outputs[0],fract.inputs[0])
    factor=nodes.new('ShaderNodeVectorMath');factor.operation='SCALE';factor.inputs[3].default_value=.2435;links.new(texcoord.outputs['UV'] if index==8 else fract.outputs[0],factor.inputs[0])
    offset=nodes.new('ShaderNodeVectorMath');offset.operation='ADD';offset.inputs[1].default_value=((index%4+.013)/4,(index//4+.013)/4,0);links.new(factor.outputs[0],offset.inputs[0])
    maps={}
    for label in ('Albedo','Normal','Surface'):
        node=nodes.new('ShaderNodeTexImage');node.image=images['Skin0' if label=='Albedo' else 'SkinNormal'] if index==0 and label!='Surface' else images[label]
        links.new(texcoord.outputs['UV'] if index==0 and label!='Surface' else offset.outputs[0],node.inputs['Vector']);maps[label]=node
    mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
    links.new(vc.outputs['Color'],mix.inputs[1]);links.new(maps['Albedo'].outputs['Color'],mix.inputs[2]);links.new(mix.outputs[0],bs.inputs['Base Color'])
    norm=nodes.new('ShaderNodeNormalMap');norm.inputs['Strength'].default_value=.65;links.new(maps['Normal'].outputs['Color'],norm.inputs['Color']);links.new(norm.outputs[0],bs.inputs['Normal'])
    bs.inputs['Metallic'].default_value=0;bs.inputs['Roughness'].default_value=.98
    if index>=8:
        alpha=nodes.new('ShaderNodeMath');alpha.operation='MULTIPLY';links.new(maps['Albedo'].outputs['Alpha'],alpha.inputs[0]);links.new(vc.outputs['Alpha'],alpha.inputs[1])
        links.new(alpha.outputs[0],bs.inputs['Alpha'])
        mat.surface_render_method='DITHERED'
skin_materials=[]
for i in range(4):
    mat=bpy.data.materials['Necrotic skin'].copy();mat.name='Anatomical decayed skin '+str(i)
    for node in mat.node_tree.nodes:
        if node.type=='TEX_IMAGE' and node.image==images['Skin0']:node.image=images['Skin'+str(i)]
    skin_materials.append(mat)
for index,kind in enumerate(['Walker','Runner','Brute','Spitter','Bloater','Warlord','Swordsman','Bulwark']):
    for ob in bpy.data.objects[kind+' rig'].children:
        if ob.type!='MESH':continue
        for slot in ob.material_slots:
            if slot.material==bpy.data.materials['Necrotic skin']:slot.material=skin_materials[index%4]
bpy.context.scene.frame_set(0);bpy.context.scene.frame_end=math.ceil(max(a['duration'] for a in actions)*30)
for kind,scale in zip(['Walker','Runner','Brute','Spitter','Bloater','Warlord','Swordsman','Bulwark'],[1,.93,1.45,.98,1.18,1.85,1.06,1.08]):bpy.data.objects[kind+' rig'].scale=(scale,)*3

# Verify editable action playback against the exported Unity joint frames.
# Compare rig-local matrices, excluding each character's presentation placement.
audit=[]
for kind in ('Walker','Swordsman','Warlord'):
    ao=bpy.data.objects[kind+' rig']
    for suffix in ('_Guard','_Walk','_Run','_Cut','Revenant_DeathBack'):
        name=suffix if suffix.startswith('Revenant_') else kind+suffix
        data=next(a for a in actions if a['name']==name)
        ao.animation_data.action=bpy.data.actions[kind+' / '+name];error=0
        for index in (0,len(data['samples'])//2,len(data['samples'])-1):
            sample=data['samples'][index];frame=sample['time']*30
            bpy.context.scene.frame_set(math.floor(frame),subframe=frame%1);bpy.context.view_layer.update()
            for i,b in enumerate(rig):
                expected=axis@matrix(sample['frames'][i])@bind[i]@inverse@ao.data.bones[b['name']].matrix_local
                actual=ao.pose.bones[b['name']].matrix
                error=max(error,max(abs(actual[r][c]-expected[r][c]) for r in range(4) for c in range(4)))
        audit.append(dict(rig=kind,action=name,max_matrix_error=error))
    ao.animation_data.action=bpy.data.actions[kind+' / '+kind+'_Guard']
validation=dict(rigs=8,actions=len(bpy.data.actions),samples=audit,passed=all(a['max_matrix_error']<.003 for a in audit))
(ROOT/'Artifacts/Revenants/blender-validation.json').write_text(json.dumps(validation,indent=2))
if not validation['passed']:raise RuntimeError('Blender pose transfer failed; see Artifacts/Revenants/blender-validation.json')
bpy.context.scene.frame_set(0)
def B(v):return Vector((v[0],-v[2],v[1]))
def area(name,position,power,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
    o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=B(position);o.rotation_euler=(B((0,1,0))-o.location).to_track_quat('-Z','Y').to_euler()
area('Large warm softbox',(-4,6,5),1800,7,(1,.87,.73));area('Cool fill',(4,3,4),1150,6,(.65,.78,1));area('Rim',(0,5,-4),2000,6,(.8,.87,1))
bpy.ops.mesh.primitive_plane_add(size=150,location=(0,0,-.014));floor=bpy.context.object;floor.name='Studio ground'
mat=bpy.data.materials.new('Studio charcoal');mat.diffuse_color=(.055,.06,.065,1);floor.data.materials.append(mat)
d=bpy.data.cameras.new('Roster presentation');cam=bpy.data.objects.new('Roster presentation',d);bpy.context.collection.objects.link(cam)
cam.location=B((.2,2.5,14));cam.rotation_euler=(B((0,1.5,0))-cam.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=13
scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=2400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(ROOT/'Artifacts/Revenants/blender-roster.png')
bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
print('REVENANTS editable actions attached:',len(bpy.data.actions),flush=True)
if '--skip-render' not in sys.argv:bpy.ops.render.render(write_still=True)
