"""Unique, mesh-bound 4K albedo, tangent normal and metal/roughness finish.

Surface UV retains material scale; Finish UV packs the entire kit. Baking uses
the same evaluated topology as the Unity export, including plate contact shadows.
Executed in marchwarden.py's namespace before export.
"""
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.bake.margin=12;scene.render.bake.use_clear=True
objects=[o for group in groups.values() for o in group]
# Dirt is tied to garment edges and low boot surfaces, not uniformly tiled.
for o in objects:
    if o['material'] not in (2,3):continue
    attr=o.data.color_attributes.new(name='Edge dirt',type='FLOAT_COLOR',domain='CORNER')
    for polygon in o.data.polygons:
        for li in polygon.loop_indices:
            p=U(o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co)
            soil=0
            if 'tunic' in o.name.lower():soil=.46*math.exp(-((p.y-.515)/.043)**2)
            if 'boot' in o.name.lower():soil=.36*math.exp(-((p.y-.05)/.08)**2)
            soil*=.70+.30*math.sin(p.x*63+p.z*47)**2
            attr.data[li].color=(1-soil*.68,1-soil*.78,1-soil*.88,1)
# Subtle complexion variation belongs to the face, not to a repeating tile:
# warmer cheeks/nose/lips, mild orbital shading, and neutral skin on the hands.
for o in objects:
    if o['material']!=8:continue
    attr=o.data.color_attributes.new(name='Skin variation',type='FLOAT_COLOR',domain='CORNER')
    for polygon in o.data.polygons:
        for li in polygon.loop_indices:
            p=U(o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co)
            warmth=shade=0
            if p.y>1.25 and p.z>0:
                warmth=.65*math.exp(-((abs(p.x)-.041)/.023)**2-((p.y-1.349)/.021)**2)
                warmth+=.35*math.exp(-(p.x/.014)**2-((p.y-1.353)/.027)**2)
                warmth+=.80*math.exp(-(p.x/.025)**4-((p.y-1.319)/.004)**2)
                shade=.07*math.exp(-((abs(p.x)-.029)/.019)**2-((p.y-1.378)/.013)**2)
            if o.get('anatomicalHand'):
                wrist,distal,across,normal,grip=HAND_FRAMES[o['anatomicalHand']]
                local=p-wrist;y=local.dot(distal)
                warmth=.12*math.exp(-((y-.085)/.016)**2)
                attr.data[li].color=(1,1-warmth*.13,1-warmth*.17,1)
            else:attr.data[li].color=(1-shade,1-shade-warmth*.13,1-shade-warmth*.17,1)
deps=bpy.context.evaluated_depsgraph_get()
for o in objects:
    data=bpy.data.meshes.new_from_object(o.evaluated_get(deps),depsgraph=deps)
    o.modifiers.clear();o.data=data
    if not data.uv_layers:
        source=data.uv_layers.new(name='Surface UV')
        for p in data.polygons:
            for li in p.loop_indices:
                co=U(o.matrix_world@data.vertices[data.loops[li].vertex_index].co)
                source.data[li].uv=(co.x*4,co.y*4)
    else: data.uv_layers[0].name='Surface UV'
    data.uv_layers.new(name='Finish UV');data.uv_layers.active_index=1
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(70),island_margin=.0025,area_weight=.5)
bpy.ops.uv.average_islands_scale();bpy.ops.uv.pack_islands(rotate=True,margin=.0025)
bpy.ops.object.mode_set(mode='OBJECT')

# One bake receiver prevents Blender rebuilding the entire scene separately for
# every rivet. Keep the editable source pieces and copy their atlas coordinates.
bpy.ops.object.select_all(action='DESELECT')
receivers=[]
for group_index,(group,parts) in enumerate(groups.items()):
    for original in parts:
        copy=original.copy();copy.data=original.data.copy();bpy.context.collection.objects.link(copy)
        copy.location.x+=group_index*3
        copy.select_set(True);receivers.append(copy)
        original.hide_render=True
bpy.context.view_layer.objects.active=receivers[0]
bpy.ops.object.join();receiver=bpy.context.object;receiver.name='Atlas bake receiver'

def node(m,kind):return m.node_tree.nodes.new(kind)
def link(m,a,b):m.node_tree.links.new(a,b)
def scalar(m,op,a,b):
    n=node(m,'ShaderNodeMath');n.operation=op
    for i,x in enumerate((a,b)):
        if isinstance(x,(float,int)):n.inputs[i].default_value=x
        else:link(m,x,n.inputs[i])
    return n.outputs[0]
targets=[];originals=[];finishes=[]
for i,m in enumerate(materials):
    nt=m.node_tree;bs=nt.nodes.get('Principled BSDF');output=nt.nodes.get('Material Output')
    originals.append(output.inputs['Surface'].links[0].from_socket)
    source=node(m,'ShaderNodeUVMap');source.uv_map='Surface UV'
    for n in list(nt.nodes):
        if n.type=='TEX_IMAGE':link(m,source.outputs['UV'],n.inputs['Vector'])
        if n.type=='NORMAL_MAP':n.uv_map='Surface UV'
    albedo=next(n for n in nt.nodes if n.type=='TEX_IMAGE' and n.image.name==palette[i][0])
    albedo_socket=albedo.outputs['Color']
    if i in (2,3):
        attribute=node(m,'ShaderNodeVertexColor');attribute.layer_name='Edge dirt'
        dirt=node(m,'ShaderNodeMixRGB');dirt.blend_type='MULTIPLY';dirt.inputs[0].default_value=1
        link(m,albedo_socket,dirt.inputs[1]);link(m,attribute.outputs['Color'],dirt.inputs[2]);albedo_socket=dirt.outputs[0]
    if i==8:
        attribute=node(m,'ShaderNodeVertexColor');attribute.layer_name='Skin variation'
        skin=node(m,'ShaderNodeMixRGB');skin.blend_type='MULTIPLY';skin.inputs[0].default_value=1
        link(m,albedo_socket,skin.inputs[1]);link(m,attribute.outputs['Color'],skin.inputs[2]);albedo_socket=skin.outputs[0]
    geo=node(m,'ShaderNodeNewGeometry')
    noise=node(m,'ShaderNodeTexNoise');noise.inputs['Scale'].default_value=42;noise.inputs['Detail'].default_value=4
    link(m,geo.outputs['Position'],noise.inputs['Vector'])
    coarse=node(m,'ShaderNodeTexNoise');coarse.inputs['Scale'].default_value=9;coarse.inputs['Detail'].default_value=3
    link(m,geo.outputs['Position'],coarse.inputs['Vector'])
    ao=node(m,'ShaderNodeAmbientOcclusion');ao.inputs['Distance'].default_value=.085;ao.samples=16
    shade=scalar(m,'ADD',scalar(m,'MULTIPLY',ao.outputs['AO'],.42),.58)
    mottling=scalar(m,'ADD',scalar(m,'MULTIPLY',noise.outputs['Fac'],.33 if i in (0,1,3,6) else .16),.77 if i in (0,1,3,6) else .88)
    mottling=scalar(m,'MULTIPLY',mottling,scalar(m,'ADD',scalar(m,'MULTIPLY',coarse.outputs['Fac'],.45 if i==2 else .20),.65 if i==2 else .86))
    curvature=node(m,'ShaderNodeValToRGB')
    curvature.color_ramp.elements[0].position=.43;curvature.color_ramp.elements[0].color=(.68,.68,.68,1)
    curvature.color_ramp.elements[1].position=.55;curvature.color_ramp.elements[1].color=(1.22,1.22,1.22,1)
    link(m,geo.outputs['Pointiness'],curvature.inputs[0])
    finish=node(m,'ShaderNodeMixRGB');finish.blend_type='MULTIPLY';finish.inputs[0].default_value=1
    link(m,albedo_socket,finish.inputs[1]);link(m,scalar(m,'MULTIPLY',shade,mottling),finish.inputs[2])
    wear=node(m,'ShaderNodeMixRGB');wear.blend_type='MULTIPLY';wear.inputs[0].default_value=.50 if i in (0,1,3,6) else 0
    link(m,finish.outputs[0],wear.inputs[1]);link(m,curvature.outputs['Color'],wear.inputs[2])
    emission=node(m,'ShaderNodeEmission');link(m,wear.outputs[0],emission.inputs['Color']);finishes.append(emission.outputs[0])
    target=node(m,'ShaderNodeTexImage');target.name='Baked mesh finish';targets.append(target)
def bake(label,kind,noncolor=False):
    im=bpy.data.images.new(label,width=4096,height=4096,alpha=True)
    if noncolor:im.colorspace_settings.name='Non-Color'
    for m,target in zip(materials,targets):target.image=im;m.node_tree.nodes.active=target
    print('MARCHWARDEN baking',label,flush=True);bpy.ops.object.bake(type=kind)
    im.filepath_raw=str(TEXTURES/(label+'.png'));im.file_format='PNG';im.save();return im
for m,finish in zip(materials,finishes):link(m,finish,m.node_tree.nodes.get('Material Output').inputs['Surface'])
atlas=bake('Finish','EMIT')
for m,original in zip(materials,originals):link(m,original,m.node_tree.nodes.get('Material Output').inputs['Surface'])
normal_atlas=bake('Finish_Normal','NORMAL',True)
for i,m in enumerate(materials):
    rgb=node(m,'ShaderNodeCombineColor');rgb.mode='RGB';rgb.inputs[0].default_value=palette[i][2];rgb.inputs[2].default_value=1-palette[i][3]
    rough_source=next(n for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image is not None and n.image.name==palette[i][0]+'_Surface')
    link(m,rough_source.outputs['Alpha'],rgb.inputs[2])
    emission=node(m,'ShaderNodeEmission');link(m,rgb.outputs[0],emission.inputs['Color'])
    link(m,emission.outputs[0],m.node_tree.nodes.get('Material Output').inputs['Surface'])
surface=bake('Finish_Surface','EMIT',True)
pixels=np.array(surface.pixels[:],dtype=np.float32).reshape(4096,4096,4)
pixels[:,:,3]=pixels[:,:,2];pixels[:,:,1]=pixels[:,:,0];pixels[:,:,2]=pixels[:,:,0]
surface.pixels.foreach_set(pixels.ravel());surface.save()
for i,m in enumerate(materials):
    bs=m.node_tree.nodes.get('Principled BSDF');uv=node(m,'ShaderNodeUVMap');uv.uv_map='Finish UV'
    tex=node(m,'ShaderNodeTexImage');tex.image=atlas;link(m,uv.outputs['UV'],tex.inputs['Vector'])
    tint=node(m,'ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1
    linear=lambda c:c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
    tint.inputs[1].default_value=(*(linear(c) for c in palette[i][1]),1)
    link(m,tex.outputs['Color'],tint.inputs[2]);link(m,tint.outputs[0],bs.inputs['Base Color'])
    tex=node(m,'ShaderNodeTexImage');tex.image=normal_atlas;link(m,uv.outputs['UV'],tex.inputs['Vector'])
    normal=node(m,'ShaderNodeNormalMap');normal.uv_map='Finish UV';normal.inputs['Strength'].default_value=.85
    link(m,tex.outputs['Color'],normal.inputs['Color']);link(m,normal.outputs['Normal'],bs.inputs['Normal'])
    link(m,bs.outputs[0],m.node_tree.nodes.get('Material Output').inputs['Surface'])
print('MARCHWARDEN mesh-bound 4K finish ready',flush=True)
bpy.data.objects.remove(receiver,do_unlink=True)
for original in objects:original.hide_render=False
