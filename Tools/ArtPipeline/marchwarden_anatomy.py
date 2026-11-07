"""Customized head from MakeHuman's explicitly CC0 hm08 anatomy base.

The source OBJ and CC0 text are retained in ArtSource/Marchwarden/Anatomy.
Only head/neck faces from its body group are used. Fit and facial modifications
are authored here; eyes, beard, helmet and all outfit geometry are separate.
"""
source_path=SOURCE/'Anatomy/MakeHuman-base.obj'
if not source_path.exists():raise RuntimeError('Missing CC0 anatomy source: '+str(source_path))
source_vertices=[];head_faces=[];group=''
for line in source_path.read_text().splitlines():
    tokens=line.split()
    if not tokens:continue
    if tokens[0]=='v':source_vertices.append(Vector(tuple(map(float,tokens[1:4]))))
    elif tokens[0]=='g':group=' '.join(tokens[1:])
    elif tokens[0]=='f' and group=='body':
        face=[int(t.split('/')[0])-1 for t in tokens[1:]]
        if all(source_vertices[i].y>5.72 for i in face):head_faces.append(face)

replace=('Sculpted continuous human head','Recessed eyeball','Muted iris','Eye pupil',
    'Sculpted eyelid','Natural brow','Sculpted ear','Ear concha','Nostril',
    'Natural lower lip','Recessed mouth line')
for obj in list(groups['Body']):
    if obj.name.startswith(replace):groups['Body'].remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
indices=sorted({i for face in head_faces for i in face});lookup={n:i for i,n in enumerate(indices)}
fitted=[]
for i in indices:
    p=source_vertices[i]
    x=p.x*.094
    if abs(x)>.075:x=math.copysign(.075+(abs(x)-.075)*.30,x)
    y=1.378+(p.y-7.28415)*.090
    z=.021+(p.z-1.24535)*.090
    if abs(x)<.028 and 1.308<y<1.331 and z>.015:
        influence=math.exp(-(x/.028)**4-((y-1.319)/.012)**4)
        y=1.319+(y-1.319)*(1-.40*influence)
    # A broader jaw and brow, a restrained nasal bridge, and a small asymmetric
    # healed brow mark give the neutral base an individual, mature face.
    jaw=math.exp(-((y-1.296)/.022)**2)
    x*=1+.07*jaw
    if z>0:
        z+=.0022*math.exp(-(x/.013)**2-((y-1.364)/.024)**2)
        z+=.0015*math.exp(-((abs(x)-.028)/.018)**2-((y-1.398)/.010)**2)
        z-=.0008*math.exp(-((x+.028)/.003)**2-((y-1.400)/.011)**2)
    fitted.append(Vector((x,y,z)))
current='Body';bone='Head'
head=mesh('Customized CC0 anatomical head',fitted,[[lookup[i] for i in face] for face in head_faces],8)
for face in head.data.polygons:face.use_smooth=True
mod=head.modifiers.new('Facial anatomical subdivision','SUBSURF');mod.levels=1;mod.render_levels=1

from mathutils.kdtree import KDTree
head_front_points=[p for p in fitted if p.z>-.005]
tree=KDTree(len(head_front_points))
for i,p in enumerate(head_front_points):tree.insert((p.x,p.y,0),i)
tree.balance()
from mathutils.bvhtree import BVHTree
bpy.context.view_layer.update()
evaluated=head.evaluated_get(bpy.context.evaluated_depsgraph_get());head_mesh=evaluated.to_mesh()
surface=BVHTree.FromPolygons([U(head.matrix_world@v.co) for v in head_mesh.vertices],[tuple(p.vertices) for p in head_mesh.polygons])
evaluated.to_mesh_clear()
def face_depth(x,y):
    location,normal,face,distance=surface.ray_cast(Vector((x,y,.30)),Vector((0,0,-1)))
    if location is not None:return location.z
    neighbours=tree.find_n((x,y,0),5)
    values=[(head_front_points[i].z,1/max(.0001,d)**2) for _,i,d in neighbours]
    return sum(z*w for z,w in values)/sum(w for _,w in values)
for sign in (-1,1):
    x=sign*.02893
    ellipsoid('Fitted anatomical eyeball',(x,1.378,.021),(.0127,.0124,.0127),11,40,24)
    ellipsoid('Hazel iris',(x,1.378,.0332),(.0037,.0037,.0012),9,28,16)
    ellipsoid('Eye pupil',(x,1.378,.0343),(.0016,.0021,.0006),7,24,14)
    points=[]
    for k in range(7):
        xx=x+sign*(-.013+k/6*.027);yy=1.397-.004*(k/6)**2
        points.append((xx,yy,face_depth(xx,yy)+.002))
    sweep('Fitted natural eyebrow',points,lambda t:.0015+.0005*math.sin(t*math.pi),9,8,24)
    # A subtle tear-line avoids bright, doll-like eye outlines.
    points=[(x+dx,1.373+.001*(abs(dx)/.01),face_depth(x+dx,1.373)+.0007) for dx in (-.009,0,.009)]
    sweep('Recessed lower eyelid line',points,.0007,9,6,16)

# Fit the beard and moustache to actual cheek/jaw topology. The strands retain
# their original silhouette below the chin and the lips remain in negative space.
for obj in groups['Body']:
    if obj.name.startswith(('Sculpted tapered beard','Swept moustache','Beard tapered edge wisp')):
        for vertex in obj.data.vertices:
            p=U(obj.matrix_world@vertex.co)
            if 1.278<p.y<1.36 and abs(p.x)<.073:
                ridge=.0017*math.sin(p.x/.007*math.pi+p.y*9) if 'beard' in obj.name.lower() else 0
                p.z=face_depth(p.x,p.y)+(.0045 if 'moustache' in obj.name.lower() else .006)+ridge
                vertex.co=obj.matrix_world.inverted()@B(p)
print('MARCHWARDEN customized anatomical head:',len(fitted),'vertices',flush=True)
