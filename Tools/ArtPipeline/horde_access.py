"""Editable, bevelled stone bridge approach. Blender --background --python this_file."""
import bpy
import json
import math
import random
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
rng = random.Random(73219)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
parts = []
material = bpy.data.materials.new('Weathered bridge limestone')
material.diffuse_color = (.28, .30, .25, 1)
material.use_nodes = True
material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value = .96


def block(name, location, scale, bevel=.022, angle=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.rotation_euler.x = angle
    modifier = obj.modifiers.new('Hand dressed chamfers', 'BEVEL')
    modifier.width = bevel
    modifier.segments = 3
    obj.data.materials.append(material)
    parts.append(obj)


rise, run, count = 7., 18., 44
for step in range(count):
    y = (step + .5) * run / count
    top = (step + 1) * rise / count
    for stone in range(3):
        block(f'Tread {step:02d} stone {stone}', ((stone-1)*.98, y, top-.11),
              (.97, run/count-.008, .22), .016)
# Dressed block retaining cheeks. The solid courses close the stair's underside.
for row in range(22):
    z = (row+.5)*.32
    begin = max(0, (z-.1)/rise*run)
    n = max(1, math.ceil((run-begin)/.86))
    length = (run-begin)/n
    for i in range(n):
        for side in [-1, 1]:
            block(f'Retaining course {row} / {i} / {side}', (side*1.48, begin+(i+.5)*length, z),
                  (.24, max(.04,length-.012), .307), .015)
for i in range(24):
    y = (i+.5)*run/24
    z = y/run*rise
    for side in [-1, 1]:
        block(f'Parapet {side} / {i}', (side*1.55, y, z+.47), (.27, run/24-.012, .78), .025)
        block(f'Coping {side} / {i}', (side*1.55, y, z+.91), (.37, run/24+.035, .15), .028,
              math.atan(rise/run))

vertices, normals, uvs, triangles, colors = [], [], [], [], []
depsgraph = bpy.context.evaluated_depsgraph_get()
for obj in parts:
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    tint = rng.uniform(.85, 1.)
    for triangle in mesh.loop_triangles:
        indices = []
        for index in [0, 2, 1]:
            v = obj.matrix_world @ mesh.vertices[triangle.vertices[index]].co
            n = obj.matrix_world.to_3x3() @ triangle.normal
            indices.append(len(vertices))
            vertices.append([v.x, v.z, v.y])
            normals.append([n.x, n.z, n.y])
            if abs(n.z) > .65:
                uv = [v.x, v.y]
            elif abs(n.x) > .65:
                uv = [v.y, v.z]
            else:
                uv = [v.x, v.z]
            uvs.append(uv)
            colors.append([tint, tint, tint, 1])
        triangles.extend(indices)
    evaluated.to_mesh_clear()
output = ROOT / 'Tools/ArtPipeline/Horde'
output.mkdir(parents=True, exist_ok=True)
# JsonUtility handles flat float arrays consistently across Unity versions.
flat = lambda values: [round(component, 6) for value in values for component in value]
(output/'BridgeStairs.json').write_text(json.dumps(dict(vertices=flat(vertices), normals=flat(normals),
    uv=flat(uvs), colors=flat(colors), triangles=triangles)))
source = ROOT / 'ArtSource/Buildings/HordeAccess.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(source), compress=True)
print(f'HORDE ACCESS: {len(parts)} bevelled stones, {len(vertices)} vertices; saved {source}')
