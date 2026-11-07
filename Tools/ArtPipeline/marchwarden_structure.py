"""Rigid articulated tassets and a compact belt quiver, before the atlas bake."""
from mathutils import Matrix

# Metal must not inherit the thigh's blended skin deformation or cloth morphs.
for obj in list(groups['Body']):
    if obj.get('region')=='skirt' and obj['material']!=2:
        groups['Body'].remove(obj);bpy.data.objects.remove(obj,do_unlink=True)

groups['WaistArmor']=[];current='WaistArmor';bone='Pelvis';ARMOR_JOINTS=[]
for column in range(20):
    angle=(column+.5)*math.tau/20
    for row in range(3):
        index=column*3+row;top=.02+row*.145
        def lamella(u,v,column=column,top=top):
            a=(column+.04+u*.92)*math.tau/20
            return skirt_point(a,top+.18*v,.027)
        first=len(groups['WaistArmor'])
        patch('Hinged hip lamella %02d.%d'%(column,row),lamella,6,6,0,.002,0)
        for u in (.18,.82):rivet(lamella(u,.12),13,.0026)
        for obj in groups['WaistArmor'][first:]:
            obj['tasset']=index;obj['bone']=bone_names.index('Bip001 Pelvis');obj['region']='tasset'
        probes=[]
        outward=Vector((math.sin(angle),0,math.cos(angle)))
        for u in (0,.5,1):
            for v in (0,.33,.67,1):
                for offset in (-.003,.004):probes.append(xyz(lamella(u,v)+outward*offset))
        ARMOR_JOINTS.append(dict(parent=index-1 if row else -1,angle=angle,
                                 pivot=xyz(skirt_point(angle,top,.027)+Vector((0,.020 if row==0 else 0,0))),probes=probes))

# Reduce bag volume by about 70%, retaining full-size arrow shafts and fletching.
compact=Matrix.Diagonal(Vector((.65,.65,.70,1)))
for obj in groups['Quiver']:obj.matrix_world=compact@obj.matrix_world
print('MARCHWARDEN structure: 60 rigid hip hinges; compact 36 cm quiver; arched heater',flush=True)
