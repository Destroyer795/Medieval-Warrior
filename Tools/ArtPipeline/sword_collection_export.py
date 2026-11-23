"""Export the collection in Unity mount space and optionally save its editable Blender master."""
import json
from math import cos, radians
from pathlib import Path
from sword_collection_geometry import cross, dot, sub, unit

ROOT = Path(__file__).resolve().parents[2]


def model_data(model):
    vertices, normals = [], []
    slots = [{"indices": []} for _ in model.finishes]
    for part in model.parts:
        adjacent = [[] for _ in part.vertices]
        faces = []
        for face in part.faces:
            for i in range(1, len(face) - 1):
                tri = (face[0], face[i], face[i + 1])
                a, b, c = (part.vertices[j] for j in tri)
                vector = cross(sub(b, a), sub(c, a))
                if dot(vector, vector) < 1e-23:
                    continue
                n = unit(vector)
                faces.append((tri, n))
                for j in tri:
                    adjacent[j].append(n)
        lookup = {}
        threshold = cos(radians(part.smooth)) - 1e-7
        for tri, n in faces:
            for j in tri:
                cluster = [v for v in adjacent[j] if dot(v, n) >= threshold]
                normal = unit(tuple(map(sum, zip(*cluster))))
                key = (j, tuple(round(v, 6) for v in normal))
                if key not in lookup:
                    lookup[key] = len(vertices)
                    vertices.append(dict(zip("xyz", part.vertices[j])))
                    normals.append(dict(zip("xyz", normal)))
                slots[part.slot]["indices"].append(lookup[key])
    return dict(id=model.id, vertices=vertices, normals=normals, slots=slots, finishes=model.finishes)


def export(models, finishes):
    directory = ROOT / "Artifacts/SwordCollection"
    directory.mkdir(parents=True, exist_ok=True)
    data = dict(models=[model_data(m) for m in models],
                materials=[dict(id=k, color=v[0], metallic=v[1], smoothness=v[2]) for k, v in finishes.items()])
    (directory / "geometry.json").write_text(json.dumps(data, separators=(",", ":")), encoding="utf8")
    try:
        import bpy
    except ImportError:
        return
    bpy.ops.wm.read_factory_settings(use_empty=True)
    materials = {}
    for key, (color, metallic, gloss) in finishes.items():
        mat = bpy.data.materials.new(key)
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get("Principled BSDF")
        srgb = [int(color[i:i+2], 16)/255 for i in (0, 2, 4)]
        rgba = tuple(((v+.055)/1.055)**2.4 if v>.04045 else v/12.92 for v in srgb) + (1,)
        mat.diffuse_color = rgba
        shader.inputs["Base Color"].default_value = rgba
        shader.inputs["Metallic"].default_value = metallic
        shader.inputs["Roughness"].default_value = 1-gloss
        materials[key] = mat
    for i, model in enumerate(models):
        group = bpy.data.collections.new(model.name)
        bpy.context.scene.collection.children.link(group)
        for part in model.parts:
            mesh = bpy.data.meshes.new(part.name)
            mesh.from_pydata(part.vertices, [], part.faces)
            mesh.update()
            mesh.materials.append(materials[model.finishes[part.slot]])
            obj = bpy.data.objects.new(part.name, mesh)
            obj.location.y = (3-i)*.32
            group.objects.link(obj)
    dest = ROOT / "ArtSource/Marchwarden/SwordCollection.blend"
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(dest), compress=True)
