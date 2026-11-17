"""4K recessed masonry, baked architectural vertex AO, and an editable supply crate.

Run after buildings.py. Existing building geometry/UVs/pivots are never changed.
"""
import bpy,bmesh,numpy as np,json,math,struct,ast,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
sys.path.insert(0,str(Path(__file__).resolve().parent))
ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'Tools/ArtPipeline/Buildings';SOURCE=ROOT/'ArtSource/Buildings'
OUT=ROOT/'Assets/Resources/Buildings/Textures/Masonry';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Buildings.blend'))

def write_image(name,array,path,color=False):
    h,w=array.shape[:2];im=bpy.data.images.get(name) or bpy.data.images.new(name,width=w,height=h,alpha=True)
    im.colorspace_settings.name='sRGB' if color else 'Non-Color';im.alpha_mode='CHANNEL_PACKED'
    im.pixels.foreach_set(array.ravel());im.filepath_raw=str(path);im.file_format='PNG';im.save()
    # Keep these large auxiliary maps external; the original packed master stays compact.
    im.filepath=bpy.path.relpath(str(path));return im

def scanned_surfaces():
    """Use photographed PBR detail, with deterministic packing for the Built-in shader."""
    manifest=json.loads((DATA/'scanned-materials.json').read_text());cache=ROOT/'Artifacts/Buildings/Scans'
    for asset,prefix,label,n in [('castle_wall_slates','Masonry 4K ','',4096),('rock_wall_02','Foundation Rock ','Rubble',2048)]:
        def read_map(kind,color=False):
            entry=next(e for e in manifest if e['asset']==asset and e['map']==kind)
            path=cache/(asset+'_'+kind+'.'+entry['format'])
            if not path.exists():raise RuntimeError('Fetch CC0 PBR sources first with download_building_scans.py: '+str(path))
            image=bpy.data.images.load(str(path),check_existing=False);image.colorspace_settings.name='sRGB' if color else 'Non-Color'
            if tuple(image.size)!=(n,n):image.scale(n,n)
            data=np.empty(n*n*4,np.float32);image.pixels.foreach_get(data);bpy.data.images.remove(image);return data.reshape(n,n,4)
        diffuse=read_map('Diffuse',True);normal=read_map('nor_gl')
        vector=normal[:,:,:3]*2-1;vector/=np.maximum(np.linalg.norm(vector,axis=-1,keepdims=True),1e-6);normal[:,:,:3]=vector*.5+.5;normal[:,:,3]=1
        height=read_map('Displacement')[:,:,0];ao=read_map('AO')[:,:,0];rough=read_map('Rough')[:,:,0]
        lo,hi=np.quantile(height,(.005,.995));height=np.clip((height-lo)/max(hi-lo,.001),0,1)
        surface=np.zeros_like(diffuse);surface[:,:,1]=np.clip(ao,.46,1);surface[:,:,2]=height;surface[:,:,3]=np.clip(1-rough,0,.065)
        diffuse[:,:,3]=1
        write_image(prefix+'Albedo',diffuse,OUT/(label+'Albedo.png'),True);write_image(prefix+'Normal',normal,OUT/(label+'Normal.png'));write_image(prefix+'Surface',surface,OUT/(label+'Surface.png'))
        print('[Scanned PBR]',asset,n,'px: diffuse, renormalised GL normal, AO, height and dry roughness',flush=True)

def vertex_ao(obj,samples):
    mesh=obj.data;positions=[v.co.copy() for v in mesh.vertices]
    tree=BVHTree.FromPolygons(positions,[tuple(p.vertices) for p in mesh.polygons],all_triangles=False)
    radius=.075;values=np.ones(len(positions),np.float32)
    for v in mesh.vertices:
        normal=v.normal.normalized();t=normal.cross(Vector((0,0,1)))
        if t.length<.1:t=normal.cross(Vector((0,1,0)))
        t.normalize();b=normal.cross(t);origin=v.co+normal*.0004;blocked=0
        for i in range(samples):
            z=(i+.5)/samples;a=i*2.3999632297+(v.index*.381966%1)*math.tau
            direction=t*(math.sqrt(z)*math.cos(a))+b*(math.sqrt(z)*math.sin(a))+normal*math.sqrt(1-z)
            hit,_,_,distance=tree.ray_cast(origin,direction,radius)
            if hit is not None:blocked+=1-math.sqrt(distance/radius)
        values[v.index]=max(.52,1-.64*blocked/samples)
    return np.array([values[l.vertex_index] for l in mesh.loops],np.float32)

def store_ao(obj,values,filename):
    mesh=obj.data;attr=mesh.color_attributes.get('BakedEnvironmentAO') or mesh.color_attributes.new(name='BakedEnvironmentAO',type='BYTE_COLOR',domain='CORNER')
    rgba=np.ones((len(values),4),np.float32);rgba[:,3]=values;attr.data.foreach_set('color',rgba.ravel())
    mesh.color_attributes.active_color=attr
    data=(np.clip(values,0,1)*255+.5).astype(np.uint8)
    with (DATA/filename).open('wb') as file:file.write(struct.pack('<i',len(values)));file.write(data.tobytes())

scanned_surfaces()
for material in bpy.data.materials:
    if not material.name.endswith('Foundation rubble') or not material.use_nodes:continue
    for node in material.node_tree.nodes:
        if node.type=='TEX_IMAGE' and node.image is not None and node.image.name.startswith('Masonry 4K '):node.image=bpy.data.images['Foundation Rock '+node.image.name.replace('Masonry 4K ','')]
if '--textures-only' in sys.argv:
    reportPath=SOURCE/'weathering-audit.json'
    report=json.loads(reportPath.read_text());report.update(repeatMetres=2.5,foundationRepeatMetres=2,sources=['https://polyhaven.com/a/castle_wall_slates','https://polyhaven.com/a/rock_wall_02'],license='CC0')
    reportPath.write_text(json.dumps(report,indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Buildings.blend'),compress=True);sys.exit(0)
statistics=[]
for obj in sorted(bpy.data.objects,key=lambda o:o.name):
    if obj.type!='MESH' or ' LOD' not in obj.name:continue
    lod=int(obj['LOD']);house=obj.name.startswith('House')
    values=np.ones(len(obj.data.loops),np.float32) if house else vertex_ao(obj,(12,8,6)[lod])
    store_ao(obj,values,obj['Unity source key']+f'_LOD{lod}.ao.bin')
    statistics.append({'name':obj.name,'corners':len(values),'minimum':round(float(values.min()),4),'mean':round(float(values.mean()),4)})
    print('[Baked vertex AO]',obj.name,statistics[-1]['mean'],flush=True)

# Authoring preview: triplanar/object-space stone maps, with all images linked relatively.
for material in list(bpy.data.materials):
    if not any(material.name.endswith(s) for s in ('Ashlar limestone','Foundation rubble')):continue
    if not material.use_nodes:continue
    nt=material.node_tree;bs=next((n for n in nt.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if bs is None:continue
    coord=nt.nodes.new('ShaderNodeTexCoord');scale=nt.nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=1.5
    prefix='Foundation Rock ' if material.name.endswith('Foundation rubble') else 'Masonry 4K '
    nt.links.new(coord.outputs['Object'],scale.inputs[0]);color=nt.nodes.new('ShaderNodeTexImage');color.image=bpy.data.images[prefix+'Albedo'];color.projection='BOX';color.projection_blend=.15
    nt.links.new(scale.outputs[0],color.inputs['Vector']);nt.links.new(color.outputs['Color'],bs.inputs['Base Color'])
    normal=nt.nodes.new('ShaderNodeTexImage');normal.image=bpy.data.images[prefix+'Normal'];normal.projection='BOX';normal.projection_blend=.15
    nt.links.new(scale.outputs[0],normal.inputs['Vector']);bump=nt.nodes.new('ShaderNodeNormalMap');bump.inputs['Strength'].default_value=1.2
    nt.links.new(normal.outputs['Color'],bump.inputs['Color']);nt.links.new(bump.outputs[0],bs.inputs['Normal']);bs.inputs['Roughness'].default_value=.96

(SOURCE/'weathering-audit.json').write_text(json.dumps({'masonryResolution':4096,'repeatMetres':2.5,'foundationRepeatMetres':2,'mortarDepthMetres':.035,'sources':['https://polyhaven.com/a/castle_wall_slates','https://polyhaven.com/a/rock_wall_02'],'license':'CC0','vertexAO':statistics},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Buildings.blend'),compress=True)

# Reuse the construction/topology/export helpers without executing the building generator.
script=ROOT/'Tools/ArtPipeline/buildings.py';syntax=ast.parse(script.read_text(encoding='utf-8-sig'))
helpers=ast.Module(body=[node for node in syntax.body if isinstance(node,(ast.ClassDef,ast.FunctionDef)) and node.name in {'Kit','mesh_object','pack_rectangles','unwrap','audit','export'}],type_ignores=[])
materials=[bpy.data.materials[name] for name in ('Ashlar limestone','Foundation rubble','Oiled dark oak','Warm lime plaster','Oxblood clay tiles','Blue grey slate','Forged iron','Old oak planks','Smoky leaded glass','Recessed mortar','Carved pale limestone','Deep openings')];authored=[]
exec(compile(helpers,str(script),'exec'))
for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
collection=bpy.data.collections.new('Arrow supply crate');bpy.context.scene.collection.children.link(collection)
k=Kit(0)
for side in (-1,1):
    for row in range(4):
        k.box((0,.075+row*.15,side*.345),(.98,.139,.035),2)
        k.box((side*.475,.075+row*.15,0),(.035,.139,.66),2)
for plank in range(6):k.box((-.416+plank*.1664,.598,0),(.157,.038,.69),7)
for x in (-.458,.458):
    for z in (-.32,.32):k.box((x,.305,z),(.075,.63,.075),6)
for x in (-.34,.34):k.box((x,.621,0),(.055,.027,.725),6)
for z in (-.368,.368):
    for x in (-.453,.453):
        for y in (.10,.50):k.prism([(x+math.cos(a*math.tau/8)*.014,y+math.sin(a*math.tau/8)*.014) for a in range(8)],.008,offset=z,mat=6)
obj=mesh_object(k,'SupplyCrate',collection,True,.004)
v=np.array([tuple(p.co) for p in obj.data.vertices]);v=(v-v.min(0))/(v.max(0)-v.min(0))*np.array((1.02,.65,.73))+np.array((-.51,-.01,-.365))
for vertex,p in zip(obj.data.vertices,v):vertex.co=p
obj.data.update();audit(obj.data);unwrap(obj.data);export(obj,DATA/'SupplyCrate.bin')
values=vertex_ao(obj,20);store_ao(obj,values,'SupplyCrate.ao.bin')
for vertex in obj.data.vertices:x,y,z=vertex.co;vertex.co=(x,-z,y)
obj.data.update()
# A small standalone editable prop; textures resolve to the committed resource maps.
for image in bpy.data.images:
    if image.packed_file:
        try:image.unpack(method='USE_ORIGINAL')
        except RuntimeError:pass
    if image.filepath:image.filepath=bpy.path.relpath(bpy.path.abspath(image.filepath))
propSource=ROOT/'ArtSource/Props';propSource.mkdir(exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(propSource/'SupplyCrate.blend'),compress=True)
print('[Environment finish] Bevelled, UV-mapped crate exported; original 1.02 x .65 x .73 envelope retained',flush=True)
