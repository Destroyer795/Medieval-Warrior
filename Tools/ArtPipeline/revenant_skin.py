"""Bake four detailed 3D decay patterns to the native anatomical UV layout."""
if '--skip-skin' not in sys.argv or not (TEXTURES/'Skin3.png').exists():
    # Bake a complete skin body, including areas covered by one form's outfit.
    collection=bpy.data.collections.new('Skin baking source');bpy.context.scene.collection.children.link(collection);objects=[]
    bake=add_mesh('Complete anatomical UV bake',texture_points,source_faces,0,None,None,body_uv)
    visibility={obj.name:obj.hide_render for obj in bpy.context.scene.objects}
    for obj in bpy.context.scene.objects:obj.hide_render=True;obj.select_set(False)
    bake.hide_render=False;bake.hide_set(False);bake.select_set(True);bpy.context.view_layer.objects.active=bake
    material=bpy.data.materials.new('Procedural necrotic skin bake');material.use_nodes=True
    bake.data.materials.clear();bake.data.materials.append(material);nodes=material.node_tree.nodes;nodes.clear();links=material.node_tree.links
    output=nodes.new('ShaderNodeOutputMaterial');emit=nodes.new('ShaderNodeEmission');links.new(emit.outputs[0],output.inputs['Surface'])
    coord=nodes.new('ShaderNodeTexCoord')
    noise=nodes.new('ShaderNodeTexNoise');noise.noise_dimensions='4D';noise.inputs['Scale'].default_value=13;noise.inputs['Detail'].default_value=5;noise.inputs['Roughness'].default_value=.74;noise.inputs['Distortion'].default_value=.45;links.new(coord.outputs['Generated'],noise.inputs['Vector'])
    ramp=nodes.new('ShaderNodeValToRGB');cr=ramp.color_ramp
    colors=[(.18,(.12,.105,.085,1)),(.37,(.25,.245,.18,1)),(.50,(.41,.395,.29,1)),(.65,(.56,.52,.40,1)),(.83,(.66,.61,.49,1))]
    cr.elements.remove(cr.elements[1]);cr.elements[0].position=colors[0][0];cr.elements[0].color=colors[0][1]
    for position,color in colors[1:]:e=cr.elements.new(position);e.color=color
    links.new(noise.outputs['Fac'],ramp.inputs[0])
    pores=nodes.new('ShaderNodeTexNoise');pores.inputs['Scale'].default_value=260;pores.inputs['Detail'].default_value=3;pores.inputs['Roughness'].default_value=.82;links.new(coord.outputs['Generated'],pores.inputs['Vector'])
    pore_ramp=nodes.new('ShaderNodeValToRGB');pore_ramp.color_ramp.elements[0].position=.34;pore_ramp.color_ramp.elements[0].color=(.42,.38,.31,1);pore_ramp.color_ramp.elements[1].position=.63;pore_ramp.color_ramp.elements[1].color=(1,1,.95,1);links.new(pores.outputs['Fac'],pore_ramp.inputs[0])
    multiply=nodes.new('ShaderNodeMixRGB');multiply.blend_type='MULTIPLY';multiply.inputs[0].default_value=.28;links.new(ramp.outputs[0],multiply.inputs[1]);links.new(pore_ramp.outputs[0],multiply.inputs[2])
    sores=nodes.new('ShaderNodeTexNoise');sores.noise_dimensions='4D';sores.inputs['Scale'].default_value=26;sores.inputs['Detail'].default_value=4;sores.inputs['Distortion'].default_value=1.4;links.new(coord.outputs['Generated'],sores.inputs['Vector'])
    lesions=nodes.new('ShaderNodeValToRGB');lesions.color_ramp.elements[0].position=.69;lesions.color_ramp.elements[1].position=.79;links.new(sores.outputs['Fac'],lesions.inputs[0])
    tissue=nodes.new('ShaderNodeMixRGB');tissue.inputs[2].default_value=(.105,.022,.012,1);links.new(lesions.outputs[0],tissue.inputs[0]);links.new(multiply.outputs[0],tissue.inputs[1]);links.new(tissue.outputs[0],emit.inputs['Color'])
    target=nodes.new('ShaderNodeTexImage');nodes.active=target
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1;scene.render.bake.margin=12
    for variation in range(4):
        noise.inputs['W'].default_value=variation*3.91;sores.inputs['W'].default_value=variation*7.31
        im=bpy.data.images.new('Revenant Skin '+str(variation),width=2048,height=2048,alpha=False);target.image=im
        bpy.ops.object.bake(type='EMIT');im.filepath_raw=str(TEXTURES/('Skin'+str(variation)+'.png'));im.file_format='PNG';im.save();im.pack()
        print('REVENANT UV skin bake',variation,flush=True)
    bpy.data.objects.remove(bake,do_unlink=True)
    # Fine pores and dry fissures in tangent space, mip-filtered at distance.
    n=2048;y,x=np.mgrid[0:n,0:n]/n;r=np.random.default_rng(744)
    height=r.normal(0,.035,(n,n))+.025*np.sin(x*math.tau*387+np.sin(y*83))*np.sin(y*math.tau*349)
    dx=(np.roll(height,-1,1)-np.roll(height,1,1))*.8;dy=(np.roll(height,-1,0)-np.roll(height,1,0))*.8
    normal=np.stack((-dx,-dy,np.ones_like(dx)),-1);normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
    rgba=np.ones((n,n,4),dtype=np.float32);rgba[:,:,:3]=normal*.5+.5
    im=bpy.data.images.new('Revenant Skin Normal',width=n,height=n,alpha=True);im.colorspace_settings.name='Non-Color';im.pixels.foreach_set(rgba.ravel());im.filepath_raw=str(TEXTURES/'SkinNormal.png');im.file_format='PNG';im.save();im.pack()
    for name,hidden in visibility.items():
        if name in bpy.data.objects:bpy.data.objects[name].hide_render=hidden
