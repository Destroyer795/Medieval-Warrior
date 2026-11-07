"""Fast geometry review before the atlas bake or Unity import."""
scene=bpy.context.scene
for obj in bpy.data.objects:obj.hide_render=True
hand=next(o for o in groups['Body'] if o.get('anatomicalHand')=='R');hand.hide_render=False
wrist,distal,across,dorsal,grip=HAND_FRAMES['R'];normal=across.cross(distal).normalized();up=normal.cross(across)
for obj in groups['Sword']:
    obj.hide_render=False
    for vertex in obj.data.vertices:
        p=U(obj.matrix_world@vertex.co);vertex.co=B(grip+across*(p.x+.352)+up*p.y+normal*p.z)
    obj.matrix_world=Matrix.Identity(4)
scene.render.engine='BLENDER_EEVEE_NEXT';scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.22,.24,.28,1)
for name,offset,power in [('Key',-dorsal*.30+distal*.15,12),('Fill',dorsal*.25-across*.15,8)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=.25
    light=bpy.data.objects.new(name,data);scene.collection.objects.link(light);light.location=B(grip+offset)
    light.rotation_euler=(B(grip)-light.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Anatomical hand review');cam=bpy.data.objects.new('Anatomical hand review',data);scene.collection.objects.link(cam)
data.type='ORTHO';data.ortho_scale=.22;data.lens=70;data.clip_start=.005;scene.camera=cam
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
out=SHOTS.parent/'AcidAndGrips';out.mkdir(parents=True,exist_ok=True)
for name,offset in [('anatomical-power-palm',-dorsal*.32+distal*.12+across*.08),('anatomical-power-back',dorsal*.32+distal*.10-across*.06)]:
    cam.location=B(grip+offset);cam.rotation_euler=(B(grip)-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
for obj in groups['Sword']:obj.hide_render=True
hand.data.shape_keys.key_blocks['BowDrawHand'].value=1;bpy.context.view_layer.update()
hook=vec(next(g for g in HAND_GRIPS if g['side']=='R')['drawContact'])
current='Sword';bone='R Hand'
string=sweep('Review bowstring',[hook-across*.12,hook+across*.12],.0008,3,8,12)
cam.location=B(hook-dorsal*.32+distal*.10+across*.07);cam.rotation_euler=(B(hook)-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(out/'anatomical-string-hook.png');bpy.ops.render.render(write_still=True)
(out/'anatomical-joints.json').write_text(json.dumps(HAND_JOINT_POSES,indent=2))
