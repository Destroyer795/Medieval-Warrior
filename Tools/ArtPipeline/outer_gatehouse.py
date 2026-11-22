"""Blender background authoring pass. Units are metres, local -Y faces the road."""
import json
import math
import random
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'ArtSource/Buildings/Foregate'
TEX = ROOT / 'Assets/Resources/Buildings/Textures'
rng = random.Random(749)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
kit = bpy.data.collections.new('Outer Gatehouse')
scene.collection.children.link(kit)
parts = {}


def material(name, color, texture=None, tile=None):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    shader = nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value = .86
    shader.inputs['Base Color'].default_value = (*color, 1)
    if texture:
        image = nodes.new('ShaderNodeTexImage')
        image.image = bpy.data.images.load(str(TEX / texture), check_existing=True)
        uv = nodes.new('ShaderNodeTexCoord').outputs['UV']
        if tile is not None:
            mapping = nodes.new('ShaderNodeVectorMath')
            mapping.operation = 'FRACTION'
            links.new(uv, mapping.inputs[0])
            atlas = nodes.new('ShaderNodeVectorMath')
            atlas.operation = 'MULTIPLY_ADD'
            atlas.inputs[1].default_value = (.245, .245, 1)
            atlas.inputs[2].default_value = ((tile % 4)*.25+.002, (tile//4)*.25+.002, 0)
            links.new(mapping.outputs[0], atlas.inputs[0])
            uv = atlas.outputs[0]
        links.new(uv, image.inputs['Vector'])
        links.new(image.outputs['Color'], shader.inputs['Base Color'])
    return mat


stone = material('Wall | existing castle slates', (.41,.38,.31), 'Masonry/Albedo.png')
rubble = material('Footings | existing rubble', (.30,.29,.24), 'Masonry/RubbleAlbedo.png')
trim = material('Carved limestone', (.62,.57,.46), 'Albedo.png', 10)
oak = material('Old oak boards', (.30,.185,.085), 'Albedo.png', 7)
beam = material('Oiled oak', (.18,.13,.085), 'Albedo.png', 2)
iron = material('Forged iron', (.13,.14,.14), 'Albedo.png', 6)
feather = material('Linen fletching', (.60,.54,.39))
red = material('Oxide red field', (.25,.043,.023))
gold = material('Ochre embroidery', (.61,.39,.13))

# Each part is one separately editable mesh. The doors pivot on their hinge lines.
def solid(part, mat, coords, faces):
    verts, polygons, slots, indices = parts.setdefault(part, ([], [], [], []))
    if mat not in slots:
        slots.append(mat)
    start = len(verts)
    verts.extend(coords)
    polygons.extend(tuple(start+i for i in face) for face in faces)
    indices.extend([slots.index(mat)] * len(faces))


faces_box = [(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
def box(part, mat, x,y,z, w,d,h):
    solid(part, mat, [(x+a*w/2,y+b*d/2,z+c*h/2) for c in (-1,1)
          for a,b in ((-1,-1),(1,-1),(1,1),(-1,1))], faces_box)


def panel(part, mat, points, front, back):
    # Counterclockwise polygon seen from the road.
    n = len(points)
    solid(part, mat, [(x,y,z) for y in (front,back) for x,z in points],
          [tuple(range(n)), tuple(reversed(range(n,2*n)))] +
          [((i+1)%n,i,i+n,(i+1)%n+n) for i in range(n)])


for side, label in ((-1,'West'),(1,'East')):
    wing = label+' curtain'
    box(wing,rubble,side*9.7,0,-.45,9.6,3.3,3.0)
    box(wing,stone,side*9.7,0,3.53,9.6,3.05,5.10)
    box(wing,trim,side*9.7,0,6.12,9.7,3.3,.22)
    for i in range(8):
        x = side*(5.45+i*1.19)
        box(wing,stone,x,-1.38,6.65,1.19,.46,.9)
        box(wing,stone,x,-1.38,7.30,.72,.50,.68)
        box(wing,trim,x,-1.38,7.67,.79,.57,.12)
    tower = label+' gate pier'
    x = side*4.27
    box(tower,rubble,x,0,-.45,3.9,5.5,3.0)
    box(tower,stone,x,0,3.78,3.6,5.15,5.58)
    for z,w,h in ((.65,4.05,.27),(5.35,3.85,.18),(6.70,4.03,.29)):
        box(tower,trim,x,0,z,w,5.55,h)
    # Open upper landing connects the stairs and curtain. Towers terminate at the front.
    for j in range(3):
        box(tower,stone,x+(j-1)*1.22,-2.47,7.38,.83,.55,1.13)
        box(tower,trim,x+(j-1)*1.22,-2.47,7.96,.91,.64,.12)
    for j in (0, 1):
        box(tower,stone,x+side*1.58,-2.04+j*.67,7.32,.55,.52,.96)
    for j in range(7):
        box(tower,trim,x+side*1.52,-2.6,1.12+j*.85,.56,.17,.37)
    # Bifurcated stair access, with a continuous clear landing onto the curtain.
    stair = label+' stair'
    for j in range(34):
        top = 6.23*(j+1)/34
        y = 10.77-j*.28
        box(stair,stone,side*7.18,y,top/2,2.23,.284,top)
        box(stair,trim,side*7.18,y,top-.055,2.30,.32,.11)
        box(stair,stone,side*8.43,y,top/2+.3,.30,.29,top+.60)
    for j in range(7):
        top=6.23+(j+1)*.615/7
        box(tower,trim,side*(7.60-j*.22),.28,(top+6.15)/2,.24,1.65,top-6.15)
    # Return to the old curtain behind both stairs. West meets Wall (22), east Wall (24)/(25).
    ret=label+' courtyard return'
    length=20.2 if side<0 else 18.2
    cx=side*14.06
    box(ret,rubble,cx,length/2,-.29,1.65,length,3.20)
    box(ret,stone,cx,length/2,3.13,1.47,length,3.74)
    box(ret,trim,cx,length/2,5.03,1.63,length,.16)
    for j in range(math.ceil(length/1.25)):
        cy=.54+j*1.25
        box(ret,stone,cx,cy,5.44,1.34,.77,.72)
        box(ret,trim,cx,cy,5.83,1.49,.85,.12)
    for j in range(1,4):
        cy=length*j/4
        box(ret,stone,cx+side*.68,cy,1.68,.77,1.08,5.55)
        box(ret,trim,cx+side*.68,cy,4.53,.85,1.17,.18)
    leaf = label+' gate leaf'
    for j in range(8):
        edge = .035+j*.285
        cap = 3.45+math.sqrt(max(0,2.36**2-(edge+.265)**2))
        box(leaf,oak,side*(edge+.135),.42,cap/2,.263,.29,cap)
    for z in (.63,2.05,3.56):
        box(leaf,iron,side*1.30,.23,z,1.94,.09,.14)
        for j in range(4):
            box(leaf,iron,side*(.56+j*.45),.16,z,.085,.06,.085)
    # Heraldic fork-tail pennon.
    x = side*4.25
    panel(tower,red,[(x-.52,5.96),(x-.52,3.82),(x,4.12),(x+.52,3.82),(x+.52,5.96)],-2.73,-2.69)
    box(tower,gold,x,-2.757,5.10,.095,.03,1.47)
    box(tower,gold,x,-2.757,5.35,.54,.03,.09)
    box(tower,iron,x,-2.76,6.0,1.26,.10,.10)
    rack = label+' arrow shelter'
    x = side*11.3
    for dx in (-1.03,1.03):
        box(rack,beam,x+dx,.58,7.02,.15,.15,1.64)
    box(rack,beam,x,.55,7.87,2.50,1.12,.15)
    box(rack,oak,x,.47,6.54,1.86,.62,.55)
    for j in range(18):
        a=x + (j%9-4)*.171
        b=.35+(j//9)*.25
        tip=7.43+rng.uniform(-.1,.07)
        box(rack,oak,a,b,(tip+6.76)/2,.028,.028,tip-6.76)
        panel(rack,feather,[(a-.04,tip-.18),(a+.04,tip-.12),(a+.04,tip),(a-.04,tip-.05)],b-.015,b+.015)

# An actual open channel lies immediately outside the doors, visible from the gallery.
for i in range(17):
    a,b = math.pi*i/17+.006, math.pi*(i+1)/17-.006
    pts=[(r*math.cos(t),3.44+r*math.sin(t)) for r,t in ((2.34,a),(2.88,a),(2.88,b),(2.34,b))]
    panel('Arch and gallery',trim,pts,-2.52,-2.02)
    panel('Arch and gallery',trim,pts,1.20,1.68)
for side in (-1,1):
    for i in range(7):
        box('Arch and gallery',trim,side*2.60,-2.25,(i+.5)*.49,.51,.58,.47)
    box('Arch and gallery',beam,side*1.86,-.52,6.51,1.66,4.30,.36)
    for j in range(6):
        box('Arch and gallery',oak,side*1.86,-2.53+j*.38,6.77,1.67,.365,.15)
for j in range(8):
    box('Arch and gallery',oak,0,.06+j*.26,6.77,5.52,.25,.15)
for j in range(6):
    x=(j-2.5)*.92
    box('Arch and gallery',stone,x,-2.61,7.12,.93,.53,.67)
    box('Arch and gallery',trim,x,-2.61,7.65,.51,.56,.41)
    box('Arch and gallery',trim,x,-2.62,6.45,.47,.83,.54)

# Visible stock and a separate presentation lever beside the open mouth.
for j,(x,y,z) in enumerate([(-1.53,-.73,7.49),(-1.85,.50,7.23),(1.85,.56,7.22)]):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=.47,location=(x,y,z))
    obj=bpy.context.object
    obj.name='Drop stone '+str(j+1)
    for v in obj.data.vertices:
        v.co *= rng.uniform(.90,1.08)
    obj.scale=(1.13,.97,.84)
    obj.data.materials.append(rubble)
    bpy.context.collection.objects.unlink(obj)
    kit.objects.link(obj)
# Hinged scoop on the west lip, with an axle on two brackets and a release handle.
# The moving basket and lever share a pivot; the centre remains visibly open.
for y in (-1.23,-.25):
    box('Cradle brackets',iron,-1.06,y,7.01,.16,.12,.46)
for j in range(5):
    box('Tipping cradle',oak,-1.47,-1.12+j*.18,7.03,.92,.168,.13)
for y in (-1.23,-.25):
    panel('Tipping cradle',iron,[(-1.99,6.97),(-1.04,6.97),(-.95,7.10),(-1.85,7.29)],y-.04,y+.04)
box('Tipping cradle',iron,-1.06,-.62,7.12,.105,1.56,.105)
panel('Tipping cradle',iron,[(-1.10,7.11),(-1.01,7.11),(-.72,7.78),(-.82,7.82)],.10,.20)
box('Tipping cradle',oak,-.77,.16,7.81,.16,.41,.13)
for x in (-1.06,1.06):
    box('Drop channel',iron,x,-1.43,6.85,.08,2.50,.10)
for name,(verts,faces,slots,indices) in parts.items():
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    kit.objects.link(obj)
    for mat in slots:
        mesh.materials.append(mat)
    uv=mesh.uv_layers.new()
    for poly,idx in zip(mesh.polygons,indices):
        poly.material_index=idx
        axis=max(range(3),key=lambda i:abs(poly.normal[i]))
        for li in poly.loop_indices:
            x,y,z=mesh.vertices[mesh.loops[li].vertex_index].co
            uv.data[li].uv=((y,z) if axis==0 else (x,z) if axis==1 else (x,y))
            uv.data[li].uv /= 3.1 if slots[idx] in (stone,rubble) else 1.45
    bevel=obj.modifiers.new('Worn edges','BEVEL')
    bevel.width=.032
    bevel.segments=2
    obj.modifiers.new('Face normals','WEIGHTED_NORMAL')
    if name=='Tipping cradle':
        pivot=Vector((-1.06,-.62,7.12))
        mesh.transform(Matrix.Translation(-pivot))
        obj.location=pivot
    if name.endswith('gate leaf'):
        pivot=Vector((-2.32 if name.startswith('West') else 2.32,.42,0))
        mesh.transform(Matrix.Translation(-pivot))
        obj.location=pivot

height=bpy.data.images.load(str(TEX/'GroundHeight.png'))
height.colorspace_settings.name='Non-Color'
pixels=np.empty(height.size[0]*height.size[1]*4,dtype=np.float32)
height.pixels.foreach_get(pixels)
pixels=pixels.reshape(height.size[1],height.size[0],4)
def ground_height(x,z):
    ix=round((x+368.89597)/700*(height.size[0]-1))
    iz=round((z+339.92484)/700*(height.size[1]-1))
    r,g=pixels[iz,ix,:2]
    return -16+120*(r+g/255)

# The approach is a separate ground-conforming asset, flared at the road end.
cobbles=material('Approach | existing worn cobbles',(.36,.32,.25),'../../../Environment/Textures/Cobble_D.jpg')
road_v=[]
road_f=[]
for j in range(99):
    y=-22+j*.45
    width=2.13+max(0,-y-2)*.053
    centre=.32*math.sin(y*.18)
    for i in range(13):
        x=centre+width*(i/6-1)
        world_x=14+x*math.cos(.39)-y*math.sin(.39)
        world_z=75-x*math.sin(.39)-y*math.cos(.39)
        road_v.append((x,y,ground_height(world_x,world_z)+.54+.055))
        a=j*13+i
        if i and j:
            road_f.append((a-14,a-13,a,a-1))
mesh=bpy.data.meshes.new('Cobbled approach')
mesh.from_pydata(road_v,[],road_f)
mesh.update()
road=bpy.data.objects.new(mesh.name,mesh)
kit.objects.link(road)
mesh.materials.append(cobbles)
uv=mesh.uv_layers.new()
for poly in mesh.polygons:
    for li in poly.loop_indices:
        v=mesh.vertices[mesh.loops[li].vertex_index].co
        uv.data[li].uv=(v.x/2.5,v.y/2.5)

# A clean portable source; site reconstruction is exclusively a review scene.
scene.render.engine='CYCLES'
bpy.context.preferences.filepaths.save_version=0
for img in bpy.data.images:
    if img.filepath:
        img.filepath=bpy.path.relpath(img.filepath,start=str(OUT))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'OuterGatehouse.blend'),compress=True,relative_remap=False)

# Game import uses the same weathered atlas as the castle. Each face stays
# inside one atlas cell; the masonry shader supplies metre-scale stone relief.
runtime_dir=ROOT/'Assets/Resources/Foregate'
runtime_dir.mkdir(parents=True,exist_ok=True)
runtime=bpy.data.collections.new('Unity export')
scene.collection.children.link(runtime)
surface=bpy.data.materials.new('Castle fabric')
tile_for={stone:0,rubble:1,trim:10,beam:2,oak:7,iron:6,feather:3}
for original in list(kit.objects):
    obj=original.copy()
    obj.data=original.data.copy()
    original_name=original.name
    original.name='Source '+original_name
    obj.name=original_name
    runtime.objects.link(obj)
    mesh=obj.data
    uv=mesh.uv_layers.active or mesh.uv_layers.new()
    new_materials=[]
    new_indices=[]
    for poly in mesh.polygons:
        mat=mesh.materials[poly.material_index]
        target=surface if mat in tile_for else mat
        if target not in new_materials:
            new_materials.append(target)
        new_indices.append(new_materials.index(target))
        if mat not in tile_for:
            continue
        tile=tile_for[mat]
        coords=[uv.data[i].uv.copy() for i in poly.loop_indices]
        low=Vector((min(v.x for v in coords),min(v.y for v in coords)))
        scale=max(1,max(v.x-low.x for v in coords),max(v.y-low.y for v in coords))
        offset=Vector((tile%4+.012,tile//4+.012))
        for i,v in zip(poly.loop_indices,coords):
            uv.data[i].uv=(offset+(v-low)*(.976/scale))/4
    mesh.materials.clear()
    for mat in new_materials:
        mesh.materials.append(mat)
    for poly,index in zip(mesh.polygons,new_indices):
        poly.material_index=index
for name,position in [('East axis',(1,0,0)),('Outward axis',(0,-1,0)),('Up axis',(0,0,1))]:
    marker=bpy.data.objects.new(name,None)
    marker.location=position
    runtime.objects.link(marker)
bpy.ops.object.select_all(action='DESELECT')
for obj in runtime.objects:
    obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(runtime_dir/'OuterGatehouse.fbx'),use_selection=True,
                         object_types={'MESH','EMPTY'},apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,
                         use_custom_props=False,path_mode='STRIP',mesh_smooth_type='FACE')
for obj in list(runtime.objects):
    bpy.data.objects.remove(obj,do_unlink=True)
for obj in kit.objects:
    obj.name=obj.name.removeprefix('Source ')
if '--runtime-only' in sys.argv:
    sys.exit(0)

# Reconstruct nearby architecture from the measured world matrices and LOD1 originals.
context=bpy.data.collections.new('Measured site')
scene.collection.children.link(context)
ref=json.loads((ROOT/'Tools/ArtPipeline/Buildings/reference.json').read_text())
nodes=ref['scenes'][-1]['nodes']
kit_to_world=Matrix.Translation((14,-75,-.54)) @ Matrix.Rotation(.39,4,'Z')
basis=Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
source=ROOT/'ArtSource/Buildings/Buildings.blend'
with bpy.data.libraries.load(str(source),link=True) as (src,dst):
    dst.objects=[name for name in src.objects if name.endswith(' LOD1')]
templates={obj.get('Unity source key'):obj for obj in dst.objects if obj}
moves={item['id']:item for item in json.loads((ROOT/'Tools/ArtPipeline/Buildings/path-layout.json').read_text())['moves']}
placement={p['path']:p for p in ref['scenes'][-1]['placements']}
old_tower=None
for node in nodes:
    template=templates.get(node['key'])
    if not template:
        continue
    m=node['matrix']
    mat=Matrix([[m[f'e{i}{j}'] for j in range(4)] for i in range(4)])
    # Match children to their moved house by the original world position.
    house=min((p for p in placement.values() if p['id'] in moves),
              key=lambda p:(p['matrix']['e03']-m['e03'])**2+(p['matrix']['e23']-m['e23'])**2,default=None)
    if house and (node['path'].startswith('House') or node['path'] in ('Door','DoorFrame')):
        if math.hypot(house['matrix']['e03']-m['e03'],house['matrix']['e23']-m['e23'])<6:
            move=moves[house['id']]
            for i,k in enumerate('xyz'):
                mat[i][3]+=move['to'][k]-move['from'][k]
    obj=bpy.data.objects.new(node['path'],template.data)
    context.objects.link(obj)
    if node['path']=='TowerB (8)':
        old_tower=obj
    obj.matrix_world=kit_to_world.inverted() @ basis @ mat @ basis.inverted()

points=[]
indices=[]
side=111
for j in range(side):
    for i in range(side):
        x,z=-110+i*2,-55+j*2
        points.append(kit_to_world.inverted() @ Vector((x,-z,ground_height(x,z))))
        if i and j:
            a=j*side+i
            indices.append((a,a-side,a-side-1,a-1))
mesh=bpy.data.meshes.new('Existing height field')
mesh.from_pydata(points,[],indices)
mesh.update()
obj=bpy.data.objects.new(mesh.name,mesh)
context.objects.link(obj)
grass=material('Meadow | site study',(.17,.195,.102), '../../../Environment/Textures/Grass_Meadow_D.jpg')
uv=mesh.uv_layers.new()
for poly in mesh.polygons:
    for li in poly.loop_indices:
        v=mesh.vertices[mesh.loops[li].vertex_index].co
        uv.data[li].uv=(v.x/7,v.y/7)
grass.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.15,.17,.085,1)
mesh.materials.append(grass)
for p in mesh.polygons:
    p.use_smooth=True

scene.world=bpy.data.worlds.new('Overcast morning')
scene.world.use_nodes=True
bg=scene.world.node_tree.nodes.get('Background')
bg.inputs[0].default_value=(.60,.70,.85,1)
bg.inputs[1].default_value=.70
bpy.ops.object.light_add(type='SUN')
sun=bpy.context.object
sun.rotation_euler=(.52,-.57,-.34)
sun.data.energy=3.2
sun.data.angle=.18
sun.data.color=(1,.86,.70)
bpy.ops.object.camera_add()
camera=bpy.context.object
scene.camera=camera
scene.render.resolution_x=1120
scene.render.resolution_y=730
scene.render.resolution_percentage=100
scene.cycles.samples=5
scene.cycles.use_denoising=True
def shot(name,at,target,lens):
    camera.location=at
    camera.rotation_euler=(Vector(target)-Vector(at)).to_track_quat('-Z','Y').to_euler()
    camera.data.lens=lens
    scene.render.filepath=str(OUT/(name+'.png'))
    bpy.ops.render.render(write_still=True)
kit.hide_render=True
shot('01-existing-front',(21,-37,14),(0,1,3.8),40)
kit.hide_render=False
if old_tower:
    old_tower.hide_render=True
shot('02-proposed-front',(21,-37,14),(0,1,3.8),40)
shot('03-wall-walk',(-14,16,14),(0,-.5,5.2),34)
shot('04-drop-gallery',(5.2,5.7,12.3),(0,-1,6.4),36)
