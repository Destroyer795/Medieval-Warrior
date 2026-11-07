"""Original Marchwarden character and equipment. Run with Blender --background --python.

Geometry is authored in metre-scale Unity coordinates (Y up, +Z forward), then
converted to Blender. The legacy rig JSON supplies joint locations only.
The anatomical head is customized from MakeHuman's CC0 base; outfit and equipment
are authored here. Sources and presentation renders are saved outside Assets;
the mesh export and its surface palette are imported by MarchwardenBuilder.
"""
import bpy, json, math, random, os, sys
import numpy as np
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Tools/ArtPipeline/Marchwarden'
SOURCE = ROOT / 'ArtSource/Marchwarden'
SHOTS = ROOT / 'Artifacts/Marchwarden'
TEXTURES = ROOT / 'Assets/Resources/Marchwarden/Textures'
DRAFT = '--draft' in sys.argv
for p in (OUT, SOURCE, SHOTS, TEXTURES): p.mkdir(parents=True, exist_ok=True)
rig = json.loads((OUT/'rig.json').read_text())
bone_names = [b['name'] for b in rig['bones']]
def vec(v): return Vector((v['x'],v['y'],v['z']))
joints = {b['name']:vec(b['position']) for b in rig['bones']}
def joint(n): return joints['Bip001 '+n]
def B(v): return Vector((v[0],-v[2],v[1]))
def U(v): return Vector((v[0],v[2],-v[1]))
def xyz(v): return dict(zip(('x','y','z'), (round(float(x),7) for x in v)))

bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
palette = [
    ('Steel', (.53,.57,.60),.94,.62), ('Brass',(.46,.36,.20),.80,.55),
    ('Cloth',(.55,.51,.42),0,.92), ('Leather',(.26,.16,.10),0,.78),
    ('Mail',(.18,.21,.23),.62,.57), ('Ivory',(.78,.75,.65),0,.7),
    ('Wood',(.38,.24,.13),0,.6), ('Dark',(.025,.029,.030),0,.9),
    ('Skin',(.63,.43,.31),0,.67),('Hair',(.19,.12,.075),0,.84),
    ('Accent',(.32,.075,.065),0,.83),('Eyes',(.55,.52,.45),0,.40),
    ('ForgedSteel',(.48,.51,.54),.95,.48),('Iron',(.20,.22,.23),.92,.62)]
materials=[]
random.seed(104)
rng=np.random.default_rng(104)
metal_reference=None
reference_path=SOURCE/'References/Met-Sallet-29.150.8a.jpg'
if reference_path.exists():
    image_reference=bpy.data.images.load(str(reference_path))
    w,h=image_reference.size
    pixels=np.array(image_reference.pixels[:],dtype=np.float32).reshape(h,w,4)
    crop=np.flipud(pixels)[int(h*.36):int(h*.55),int(w*.22):int(w*.45),:3].mean(axis=-1)
    iy=(np.arange(1024)*(crop.shape[0]-1)/1023).astype(int); ix=(np.arange(1024)*(crop.shape[1]-1)/1023).astype(int)
    detail=crop[iy[:,None],ix[None,:]]
    fy=np.fft.fftfreq(1024); fx=np.fft.fftfreq(1024)
    blur=np.fft.ifft2(np.fft.fft2(detail)*np.exp(-2*math.pi**2*12**2*(fy[:,None]**2+fx[None,:]**2))).real
    metal_reference=np.clip((detail-blur)*8,-1,1)
def save_texture(label, pixels, noncolor=False):
    h,w=pixels.shape[:2]
    im=bpy.data.images.new(label,width=w,height=h,alpha=True)
    if noncolor: im.colorspace_settings.name='Non-Color'
    im.pixels.foreach_set(np.asarray(pixels,dtype=np.float32).ravel())
    im.filepath_raw=str(TEXTURES/(label+'.png')); im.file_format='PNG'; im.save()
    return im

for name,color,metal,rough in palette:
    # Full-resolution tileable albedo, tangent normals and roughness. Surface
    # breakup is restrained and mip-filtered, avoiding speckled pixel noise.
    size=1024; yy,xx=np.mgrid[0:size,0:size]/size
    grain=rng.random((size,size))-.5
    waves=np.sin(xx*math.tau*7+np.sin(yy*math.tau*3))*.5
    scratches=np.zeros_like(xx);scuffs=np.zeros_like(xx)
    if name in ('Steel','ForgedSteel','Iron','Leather'):
        for k in range(22 if name=='ForgedSteel' else 35):
            x0,y0=rng.uniform(.05,.95,2)
            angle=rng.choice([math.pi/2,math.pi/3,-math.pi/4]) + rng.uniform(-.10,.10)
            length=rng.uniform(.020,.095) if name=='ForgedSteel' else rng.uniform(.008,.055)
            along=(xx-x0)*math.cos(angle)+(yy-y0)*math.sin(angle)
            across=-(xx-x0)*math.sin(angle)+(yy-y0)*math.cos(angle)
            width=rng.uniform(.0004,.0011) if name=='ForgedSteel' else .00065
            cut=np.exp(-(across/width)**2)*np.maximum(0,1-(along/length)**8)
            scratches=np.maximum(scratches,cut)
            scuffs=np.maximum(scuffs,np.exp(-(across/(width*4))**2)*np.maximum(0,1-(along/(length*1.1))**8))
    if name=='Cloth':
        height=.14*np.sin(xx*math.tau*128)*np.cos(yy*math.tau*128)
        height+=.025*np.sin(xx*math.tau*32+yy*math.tau*32)
        tone=.88+.035*height
        if (SOURCE/'WovenLinen-source.png').exists():
            ref=bpy.data.images.load(str(SOURCE/'WovenLinen-source.png'),check_existing=True)
            w,h=ref.size;sample=np.array(ref.pixels[:],dtype=np.float32).reshape(h,w,4)
            iy=(np.arange(size)*(h-1)/(size-1)).astype(int);ix=(np.arange(size)*(w-1)/(size-1)).astype(int)
            weave=sample[iy[:,None],ix[None,:],:3].mean(axis=-1)
            tone=np.clip(.35+weave*1.8,.45,1)
            height=(weave-.3)*.35
    elif name=='Mail':
        tx=((xx*32+(np.floor(yy*40)%2)*.5)%1-.5)/.44
        ty=((yy*40)%1-.5)/.36
        rings=np.exp(-((np.sqrt(tx*tx+ty*ty)-1)*9)**2)
        height=.6*rings; tone=.36+.53*rings
    elif name=='Leather':
        height=.055*grain+.03*waves; tone=.88+.035*waves+.025*grain
        if (SOURCE/'WornLeather-source.png').exists():
            ref=bpy.data.images.load(str(SOURCE/'WornLeather-source.png'),check_existing=True)
            w,h=ref.size;sample=np.array(ref.pixels[:],dtype=np.float32).reshape(h,w,4)
            iy=(np.arange(size)*(h-1)/(size-1)).astype(int);ix=(np.arange(size)*(w-1)/(size-1)).astype(int)
            leather=sample[iy[:,None],ix[None,:],:3].mean(axis=-1)
            tone=np.clip(.25+leather*1.9,.40,1);height=(leather-.25)*.95
    elif name=='Wood':
        height=.035*np.sin(xx*math.tau*32+np.sin(yy*math.tau*2)*2)
        tone=.86+.055*waves+.08*height
    elif name=='Skin':
        height=.008*grain+.006*waves;tone=.91+.035*waves+.018*grain
    elif name=='Hair':
        strand=np.sin(xx*math.tau*94+np.sin(yy*math.tau*3)*1.5)
        height=.035*strand+.008*grain;tone=.80+.10*strand+.06*waves
    elif name in ('Steel','ForgedSteel','Iron') and (SOURCE/'WornSteel-source.png').exists():
        reference=bpy.data.images.load(str(SOURCE/'WornSteel-source.png'),check_existing=True)
        w,h=reference.size
        source=np.array(reference.pixels[:],dtype=np.float32).reshape(h,w,4)
        iy=(np.arange(size)*(h-1)/(size-1)).astype(int);ix=(np.arange(size)*(w-1)/(size-1)).astype(int)
        steel=source[iy[:,None],ix[None,:],:3]
        tone=np.clip(.40+steel.mean(axis=-1)*.9,.44,1)
        height=(steel.mean(axis=-1)-.35)*.09+.002*grain
    else:
        height=.012*grain+.012*waves; tone=.94+.015*waves
    if name in ('Steel','ForgedSteel','Iron','Leather'):
        depth=.16 if name=='ForgedSteel' else .08
        height-=scratches*depth
        tone=np.clip(tone-scratches*(.35 if name=='ForgedSteel' else .13)+scuffs*.10,.18,1)
    rgba=np.ones((size,size,4)); rgba[:,:,:3]=np.clip(tone[:,:,None],0,1)
    im=save_texture(name,rgba)
    # Unity Standard's detail multiplier is neutral at encoded sRGB 0.5.
    # Retain the original material UVs separately from the tightly packed atlas.
    detail=np.ones((size,size,4))
    contrast=1.8 if name in ('Cloth','Leather','Mail','Wood') else .65
    detail[:,:,:3]=np.clip(.5*(1+(tone/np.mean(tone)-1)*contrast),.12,.86)[:,:,None]
    save_texture(name+'_Detail',detail)
    dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*1.3
    dy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*1.3
    norm=np.stack((-dx,-dy,np.ones_like(dx)),axis=-1); norm/=np.linalg.norm(norm,axis=-1,keepdims=True)
    rgba[:,:,:3]=norm*.5+.5; rgba[:,:,3]=1
    normal_im=save_texture(name+'_Normal',rgba,True)
    rgba[:,:,:3]=metal
    rgba[:,:,3]=np.clip(1-rough+waves*.13+grain*.06+scuffs*.19-scratches*.25,.03,.92)
    gloss_im=save_texture(name+'_Surface',rgba,True)
    m=bpy.data.materials.new('Marchwarden '+name); m.diffuse_color=(*color,1); m.use_nodes=True
    linear=lambda c: c/12.92 if c <= .04045 else ((c+.055)/1.055)**2.4
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*(linear(c) for c in color),1)
    bs.inputs['Metallic'].default_value=metal; bs.inputs['Roughness'].default_value=rough
    albedo=m.node_tree.nodes.new('ShaderNodeTexImage'); albedo.image=im
    tint=m.node_tree.nodes.new('ShaderNodeMixRGB'); tint.blend_type='MULTIPLY'; tint.inputs[0].default_value=1
    tint.inputs[1].default_value=(*(linear(c) for c in color),1)
    m.node_tree.links.new(albedo.outputs['Color'],tint.inputs[2]); m.node_tree.links.new(tint.outputs[0],bs.inputs['Base Color'])
    tex=m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=normal_im
    bump=m.node_tree.nodes.new('ShaderNodeNormalMap'); bump.inputs['Strength'].default_value=.85 if name in ('Mail','ForgedSteel') else .50
    m.node_tree.links.new(tex.outputs['Color'],bump.inputs['Color']); m.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    surface=m.node_tree.nodes.new('ShaderNodeTexImage');surface.image=gloss_im
    invert=m.node_tree.nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1
    m.node_tree.links.new(surface.outputs['Alpha'],invert.inputs[1]);m.node_tree.links.new(invert.outputs[0],bs.inputs['Roughness'])
    materials.append(m)

groups={name:[] for name in ('Body','Sword','Shield','Bow','Quiver','Arrow')}
current='Body'; bone='Spine'
def register(o,mat,bevel=0):
    o.data.materials.append(materials[mat]); o['material']=mat
    o['bone']=bone_names.index('Bip001 '+bone) if current in ('Body','Tunic') else -1
    if bevel:
        mod=o.modifiers.new('Forged edge', 'BEVEL'); mod.width=bevel; mod.segments=2
        mod=o.modifiers.new('Face normals', 'WEIGHTED_NORMAL'); mod.keep_sharp=True; mod.weight=35
    groups[current].append(o)
    return o

def mesh(name,verts,faces,mat,bevel=0):
    d=bpy.data.meshes.new(name); d.from_pydata([B(v) for v in verts],[],faces); d.update()
    o=bpy.data.objects.new(name,d); bpy.context.collection.objects.link(o)
    # Correct arbitrary authoring loops to face out from the local solid.
    import bmesh
    bm=bmesh.new(); bm.from_mesh(d); bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); bm.to_mesh(d); bm.free()
    return register(o,mat,bevel)

def box(name,c,size,mat,bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1,location=B(c)); o=bpy.context.object; o.name=name
    o.scale=(size[0],size[2],size[1]); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return register(o,mat,bevel)

def ellipsoid(name,c,r,mat,seg=16,rings=8):
    # Avoid a dependency-graph rebuild for every small fastener/chain fitting.
    # This is the same UV-sphere topology, created directly in object data.
    verts=[(0,0,r[1]),(0,0,-r[1])]
    for j in range(1,rings):
        phi=j*math.pi/rings
        for i in range(seg):
            a=i*math.tau/seg
            verts.append(tuple(B((r[0]*math.sin(phi)*math.cos(a),r[1]*math.cos(phi),r[2]*math.sin(phi)*math.sin(a)))))
    faces=[]
    for i in range(seg):faces.append((0,2+i,2+(i+1)%seg))
    for j in range(rings-2):
        for i in range(seg):faces.append((2+j*seg+i,2+(j+1)*seg+i,2+(j+1)*seg+(i+1)%seg,2+j*seg+(i+1)%seg))
    last=2+(rings-2)*seg
    for i in range(seg):faces.append((1,last+(i+1)%seg,last+i))
    faces=[tuple(reversed(face)) for face in faces]
    d=bpy.data.meshes.new(name);d.from_pydata(verts,[],faces);d.update()
    o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=B(c)
    for p in d.polygons:p.use_smooth=True
    return register(o,mat)

def tube(name,a,b,r0,r1,mat,sides=12):
    a,b=Vector(a),Vector(b); axis=(b-a).normalized()
    u=axis.cross(Vector((0,0,1)) if abs(axis.z)<.9 else Vector((0,1,0))).normalized(); v=axis.cross(u)
    verts=[]
    for c,r in ((a,r0),(b,r1)):
        for i in range(sides): verts.append(c+r*(u*math.cos(i*math.tau/sides)+v*math.sin(i*math.tau/sides)))
    faces=[tuple(reversed(range(sides))),tuple(range(sides,2*sides))]
    faces += [(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]
    o=mesh(name,verts,faces,mat)
    for p in list(o.data.polygons)[2:]: p.use_smooth=True
    return o

def loft(name,rings,mat,sides=16,bevel=.003):
    # rings are (height, half width, half depth, centre depth)
    verts=[]
    for y,w,d,z in rings:
        for i in range(sides):
            a=i*math.tau/sides; verts.append((math.cos(a)*w,y, z+math.sin(a)*d))
    faces=[tuple(reversed(range(sides))),tuple(range((len(rings)-1)*sides,len(rings)*sides))]
    for j in range(len(rings)-1):
        faces += [(j*sides+i,j*sides+(i+1)%sides,(j+1)*sides+(i+1)%sides,(j+1)*sides+i) for i in range(sides)]
    o=mesh(name,verts,faces,mat,bevel)
    for p in list(o.data.polygons)[2:]: p.use_smooth=True
    return o

def line(name,pts,r,mat):
    for i,(a,b) in enumerate(zip(pts,pts[1:])): tube(name+str(i),a,b,r,r,mat,6)

def rivet(c,mat=1,r=.009): return ellipsoid('Peened rivet',c,(r,r,r*.6),mat,8,4)

def badge(cx,cy,z,scale=1):
    # An original split-watchtower device: two tapered uprights and a floating diamond.
    # Small geometry, so the silhouette survives at gameplay distance.
    for side in (-1,1):
        pts=[(cx+side*.018*scale,cy-.045*scale,z),(cx+side*.052*scale,cy+.015*scale,z),(cx+side*.033*scale,cy+.036*scale,z),(cx+side*.010*scale,cy-.010*scale,z)]
        mesh('Watchtower crest',pts+[(x,y,zz-.003) for x,y,zz in pts],[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],5,.001)
    ellipsoid('Crest keystone',(cx,cy+.044*scale,z+.001),(.013*scale,.022*scale,.004),1,4,4)

# The sculpted surface library builds the character and equipment.
exec(compile((Path(__file__).with_name('marchwarden_frontier.py')).read_text(), 'marchwarden_frontier.py', 'exec'), globals())
exec(compile((Path(__file__).with_name('marchwarden_anatomy.py')).read_text(), 'marchwarden_anatomy.py', 'exec'), globals())
exec(compile((Path(__file__).with_name('marchwarden_physique.py')).read_text(), 'marchwarden_physique.py', 'exec'), globals())
exec(compile((Path(__file__).with_name('marchwarden_heater.py')).read_text(), 'marchwarden_heater.py', 'exec'), globals())
exec(compile((Path(__file__).with_name('marchwarden_cinematic.py')).read_text(), 'marchwarden_cinematic.py', 'exec'), globals())
exec(compile((Path(__file__).with_name('marchwarden_gladiator.py')).read_text(), 'marchwarden_gladiator.py', 'exec'), globals())
exec(compile((Path(__file__).with_name('marchwarden_cloth.py')).read_text(), 'marchwarden_cloth.py', 'exec'), globals())
exec(compile((Path(__file__).with_name('marchwarden_structure.py')).read_text(), 'marchwarden_structure.py', 'exec'), globals())
exec(compile((Path(__file__).with_name('marchwarden_hands.py')).read_text(), 'marchwarden_hands.py', 'exec'), globals())
if '--hand-study' in sys.argv:
    exec(compile((Path(__file__).with_name('study_anatomical_grip.py')).read_text(),'study_anatomical_grip.py','exec'),globals())
    raise SystemExit(0)

if not DRAFT:
    exec(compile((Path(__file__).with_name('marchwarden_finish.py')).read_text(), 'marchwarden_finish.py', 'exec'), globals())

# Finishing evaluates subdivision. Restore the joint-authored finger poses on
# that final topology so the editable source and exported morphs agree.
for hand in (o for o in groups['Body'] if o.get('anatomicalHand')):
    if hand.data.shape_keys is not None:continue
    tree,deltas=HAND_MOTION_FIELDS[hand['anatomicalHand']]
    hand.shape_key_add(name='Basis')
    for name,values in deltas.items():
        key=hand.shape_key_add(name=name)
        for v,target in zip(hand.data.vertices,key.data):
            _,index,distance=tree.find(U(hand.matrix_world@v.co))
            if distance>.0001:raise RuntimeError('Final hand topology differs from finger poses')
            target.co=v.co+B(values[index])

# Export evaluated meshes, preserving hard edges and material slots.
def skin_weights(o,pos):
    primary=o['bone']; secondary=primary; blend=0.
    if 'clothSide' in o:
        secondary=bone_names.index('Bip001 '+o['clothSide']+' Thigh')
        blend=max(0,min(1,(.78-pos.y)/.30))*.85
    if 'ankleSide' in o:
        secondary=bone_names.index('Bip001 '+o['ankleSide']+' Foot')
        blend=max(0,min(.80,(.21-pos.y)/.11))
    if 'shoulderSide' in o:
        secondary=bone_names.index('Bip001 Spine')
        upper=joint(o['shoulderSide']+' UpperArm');fore=joint(o['shoulderSide']+' Forearm')
        t=(pos-upper).dot((fore-upper).normalized())/(fore-upper).length
        blend=smoothstep((.04-t)/.24)
    if 'coif' in o:
        secondary=bone_names.index('Bip001 Spine');blend=max(0,min(1,(1.33-pos.y)/.15))
    if 'kneeSide' in o:
        secondary=bone_names.index('Bip001 '+o['kneeSide']+' Thigh')
        knee=joint(o['kneeSide']+' Calf');blend=max(0,min(1,(pos.y-knee.y+.035)/.09))
    if 'elbowSide' in o:
        label=o['elbowSide'];secondary=bone_names.index('Bip001 '+label+' Forearm')
        a,b=joint(label+' UpperArm'),joint(label+' Forearm');axis=(b-a).normalized()
        t=(pos-a).dot(axis)/(b-a).length
        blend=smoothstep((t-.76)/.42)
        if t<.04:
            secondary=bone_names.index('Bip001 Spine');blend=smoothstep((.04-t)/.24)
    return primary,secondary,round(blend,5)

deps=bpy.context.evaluated_depsgraph_get(); exported=[]
for name,objects in ([] if DRAFT else groups.items()):
    part=dict(name=name,vertices=[],normals=[],uv=[],detailUV=[],triangles=[],materials=[],bones=[],bones2=[],blends=[],skinIndices=[],skinWeights=[],shapes=[],pieceIndices=[])
    shapes={}
    lookup={}
    for o in objects:
        ev=o.evaluated_get(deps); me=ev.to_mesh(); me.calc_loop_triangles()
        normal_matrix=o.matrix_world.to_3x3().inverted().transposed()
        for tri in me.loop_triangles:
            if tri.area < 1e-12: continue
            indices=[]
            for li in tri.loops:
                loop=me.loops[li]; vert=me.vertices[loop.vertex_index]
                pos=U(o.matrix_world@vert.co); normal=U(normal_matrix@me.corner_normals[li].vector).normalized()
                # World-scale projection avoids stretched or missing UVs on the many small plates.
                if me.uv_layers.active:
                    uv=me.uv_layers.active.data[li].uv
                    uvpair=(round(float(uv.x),6),round(float(uv.y),6))
                else:
                    axes=sorted(range(3),key=lambda i:abs(normal[i]))[:2]
                    uvpair=(round(float(pos[axes[0]]*4),6),round(float(pos[axes[1]]*4),6))
                duv=me.uv_layers['Surface UV'].data[li].uv
                detailpair=(round(float(duv.x),6),round(float(duv.y),6))
                weights=skin_weights4(o,pos)
                b1=weights[0][0];b2=weights[1][0] if len(weights)>1 else b1
                blend=weights[1][1] if len(weights)>1 else 0
                motions=garment_motion(o,pos)
                key=tuple(round(float(x),6) for x in pos)+tuple(round(float(x),5) for x in normal)+uvpair+detailpair+tuple(weights)+(o['material'],tuple(n for n,_ in motions),o.get('tasset',-1))
                if key not in lookup:
                    lookup[key]=len(part['vertices'])
                    part['vertices'].append(xyz(pos)); part['normals'].append(xyz(normal))
                    part['uv'].append({'x':uvpair[0],'y':uvpair[1]}); part['bones'].append(b1)
                    part['detailUV'].append({'x':detailpair[0],'y':detailpair[1]})
                    part['bones2'].append(b2); part['blends'].append(blend)
                    part['pieceIndices'].append(o.get('tasset',-1))
                    packed=weights+[(b1,0.)]*(4-len(weights))
                    part['skinIndices'].extend(i for i,w in packed)
                    part['skinWeights'].extend(w for i,w in packed)
                    for shape_name,delta in motions:
                        shape=shapes.setdefault(shape_name,dict(name=shape_name,indices=[],deltas=[]))
                        shape['indices'].append(lookup[key]);shape['deltas'].append(xyz(delta))
                indices.append(lookup[key])
            part['triangles'] += indices; part['materials'].append(o['material'])
        ev.to_mesh_clear()
    part['shapes']=list(shapes.values())
    exported.append(part)
if not DRAFT:(OUT/'meshes.json').write_text(json.dumps({'statureScale':STATURE_SCALE,'armorJoints':ARMOR_JOINTS,'handGrips':HAND_GRIPS,'parts':exported,'surfaces':[
    {'name':name,'color':dict(zip(('r','g','b','a'),(*color,1))),
     'metal':metal,'smoothness':1-rough} for name,color,metal,rough in palette]},separators=(',',':')))
print('MARCHWARDEN meshes:',[(p['name'],len(p['triangles'])//3) for p in exported])

# Editable source collections: character and each item separated and labelled.
for name,objects in groups.items():
    collection=bpy.data.collections.new('Marchwarden / '+name); bpy.context.scene.collection.children.link(collection)
    for o in objects:
        for old in list(o.users_collection): old.objects.unlink(o)
        collection.objects.link(o)

# Rig the original geometry to an editable reference armature as well as exporting
# the Unity-native weights. This rig is a working source aid, not a copied mesh.
arm=bpy.data.armatures.new('Animation reference'); rig_obj=bpy.data.objects.new('Marchwarden rig',arm); bpy.context.collection.objects.link(rig_obj)
bpy.context.view_layer.objects.active=rig_obj; rig_obj.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for b in rig['bones']:
    eb=arm.edit_bones.new(b['name']); eb.head=B(vec(b['position'])); eb.tail=eb.head+Vector((0,0,.08))
exec(compile((Path(__file__).with_name('marchwarden_source_rig.py')).read_text(),'marchwarden_source_rig.py','exec'),globals())
configure_source_rig(rig_obj)
bpy.ops.object.mode_set(mode='EDIT')
for index,spec in enumerate(ARMOR_JOINTS):
    eb=arm.edit_bones.new('Tasset %02d'%index);eb.head=B(vec(spec['pivot']));eb.tail=eb.head+Vector((0,0,-.05))
    eb.parent=arm.edit_bones['Tasset %02d'%spec['parent']] if spec['parent']>=0 else arm.edit_bones['Bip001 Pelvis']
bpy.ops.object.mode_set(mode='OBJECT')
for o in groups['Body']+groups['Tunic']:
    for v in o.data.vertices:
        for index,weight in skin_weights4(o,U(o.matrix_world@v.co)):
            vg=o.vertex_groups.get(bone_names[index]) or o.vertex_groups.new(name=bone_names[index])
            vg.add([v.index],weight,'REPLACE')
    mod=o.modifiers.new('Animation skin','ARMATURE'); mod.object=rig_obj
for o in groups['WaistArmor']:
    vg=o.vertex_groups.new(name='Tasset %02d'%o['tasset']);vg.add(list(range(len(o.data.vertices))),1,'REPLACE')
    mod=o.modifiers.new('Rigid articulated armor','ARMATURE');mod.object=rig_obj
rig_obj.show_in_front=True

# Keep exported geometry in the canonical animation scale; present the editable
# body and its complete armature at the same uniform stature used in the game.
stature=bpy.data.objects.new('Marchwarden stature',None);bpy.context.collection.objects.link(stature)
for o in [rig_obj]+groups['Body']+groups['Tunic']+groups['WaistArmor']:o.parent=stature
stature.scale=(STATURE_SCALE,)*3

# Presentation layout and soft studio lighting (separate from exported coordinates).
for name in ('Sword','Shield','ShieldHandle','Bow','Quiver','Arrow'):
    for o in groups[name]: o.hide_render=True; o.hide_set(True)

def show_prop(name,location,rotation=None):
    parent=bpy.data.objects.new(name+' presentation',None); bpy.context.collection.objects.link(parent)
    parent.location=B(location)
    if rotation: parent.rotation_euler=rotation
    for original in groups[name]:
        o=original.copy(); o.data=original.data; bpy.context.collection.objects.link(o)
        o.hide_render=False; o.hide_set(False); o.parent=parent
    return parent
show_prop('Sword',(.97,.99,.0),(0,-math.pi/2,0))
show_prop('Shield',(-.90,.85,.0),(0,-math.pi/2,0))
show_prop('Bow',(1.26,.73,-.05))
show_prop('Quiver',(-1.3,.11,.0))
show_prop('Arrow',(1.48,.37,.0),(math.pi/2,0,0))

scene=bpy.context.scene; scene.render.engine='BLENDER_EEVEE_NEXT' if DRAFT else 'CYCLES'; scene.cycles.samples=64; scene.cycles.use_denoising=True
scene.world.color=(.18,.18,.18)
def area(name,pos,power,size,color):
    d=bpy.data.lights.new(name,'AREA'); d.energy=power; d.shape='DISK'; d.size=size; d.color=color
    o=bpy.data.objects.new(name,d); bpy.context.collection.objects.link(o); o.location=B(pos)
    o.rotation_euler=(B((0,.85,0))-o.location).to_track_quat('-Z','Y').to_euler()
area('Key softbox',(-3,4,4),480,4,(1,.87,.72)); area('Cool fill',(3,2,2),270,3,(.62,.78,1)); area('Rim',(0,3,-3),600,3,(.74,.85,1))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.012)); floor=bpy.context.object; floor.name='Studio floor'
m=bpy.data.materials.new('Backdrop'); m.diffuse_color=(.045,.064,.075,1); floor.data.materials.append(m)
cam_data=bpy.data.cameras.new('Presentation'); cam=bpy.data.objects.new('Presentation',cam_data); bpy.context.collection.objects.link(cam); scene.camera=cam
cam.location=B((2.7,1.85,5.3)); cam.rotation_euler=(B((.05,.82,0))-cam.location).to_track_quat('-Z','Y').to_euler()
cam_data.type='ORTHO'; cam_data.ortho_scale=3.9
scene.render.resolution_x=2400; scene.render.resolution_y=1600; scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'; scene.render.image_settings.file_format='PNG'
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(SHOTS/'Cinematic/Draft.blend' if DRAFT else SOURCE/'Marchwarden.blend'),compress=True)
if DRAFT:scene.render.resolution_percentage=45
scene.render.filepath=str(SHOTS/('Cinematic/draft-studio.png' if DRAFT else 'marchwarden-studio.png')); bpy.ops.render.render(write_still=True)
# Character-only close-up.
for o in list(bpy.data.objects):
    if 'presentation' in o.name.lower(): o.hide_render=True
    if o.parent and 'presentation' in o.parent.name.lower(): o.hide_render=True
cam.location=B((2,1.70,4.5)); cam.rotation_euler=(B((0,.94,0))-cam.location).to_track_quat('-Z','Y').to_euler(); cam_data.ortho_scale=2.18
scene.render.resolution_x=1600; scene.render.resolution_y=1920
scene.render.filepath=str(SHOTS/('Cinematic/draft-character.png' if DRAFT else 'marchwarden-character.png')); bpy.ops.render.render(write_still=True)
