"""CC0 anatomical hands, posed at their actual MCP/PIP/DIP joints.

Retains the MakeHuman hand topology, knuckle loops, nail beds and finger webs.
The three poses use the same vertices; there are no swept finger primitives.
"""
from mathutils import Matrix
from mathutils.kdtree import KDTree
import bmesh

raw=[];polygons={};source_group=''
for line in (SOURCE/'Anatomy/MakeHuman-base.obj').read_text().splitlines():
    t=line.split()
    if not t:continue
    if t[0]=='v':raw.append(Vector(tuple(map(float,t[1:4]))))
    elif t[0]=='g':source_group=' '.join(t[1:])
    elif t[0]=='f':polygons.setdefault(source_group,[]).append([int(x.split('/')[0])-1 for x in t[1:]])
markers={}
for name,faces in polygons.items():
    if name.startswith('joint-r-'):
        indices={i for f in faces for i in f}
        markers[name[8:]]=sum((raw[i] for i in indices),Vector())/len(indices)
origin=markers['hand']
source_distal=(sum((markers['finger-%d-1'%f] for f in range(2,6)),Vector())/4-origin).normalized()
source_across=markers['finger-2-1']-markers['finger-5-1']
source_across=(source_across-source_distal*source_across.dot(source_distal)).normalized()
# Right-hand dorsal surface is opposite thumb-side x finger-direction.
source_dorsal=-source_across.cross(source_distal).normalized()
def canonical(point):
    d=point-origin
    return Vector((d.dot(source_across)*.095,d.dot(source_distal)*.095,d.dot(source_dorsal)*.095))
chains={f:[canonical(markers['finger-%d-%d'%(f,j)]) for j in range(1,5)] for f in range(1,6)}
canonical_vertices=[canonical(p) for p in raw]
faces=[f for f in polygons['body'] if all(raw[i].x<0 and -.100<canonical_vertices[i].y<.195 and
       abs(canonical_vertices[i].x)<.095 and abs(canonical_vertices[i].z)<.060 for i in f)]
indices=sorted({i for face in faces for i in face});lookup={n:i for i,n in enumerate(indices)}
points=[canonical_vertices[i] for i in indices]
source_faces=[[lookup[i] for i in f] for f in faces]
assert len(points)>200,'Anatomical hand extraction is incomplete'
GRIP=Vector((0,.065,-.025))

def segment_distance(p,a,b):
    v=b-a;t=max(0,min(1,(p-a).dot(v)/v.length_squared))
    return (p-a-v*t).length,t
def finger_points(f,angles):
    old=chains[f];result=[old[0].copy()];angle=0
    for j,a in enumerate(angles):
        angle+=math.radians(a);length=(old[j+1]-old[j]).length
        result.append(result[-1]+Vector((0,math.cos(angle)*length,-math.sin(angle)*length)))
    return result
def closed_angles(f):
    best=None;score=float('inf')
    for a in range(30,86,5):
        for b in range(45,96,5):
            for c in range(30,81,5):
                q=finger_points(f,(a,b,c));cost=0
                for j in range(3):
                    for t in (.25,.5,.75,1):
                        p=q[j].lerp(q[j+1],t)-GRIP;r=math.hypot(p.y,p.z)
                        cost+=(r-.026)**2+max(0,.023-r)**2*45
                cost+=(q[-1].y-(GRIP.y-.003))**2*.6
                cost+=((a-50)**2+(b-70)**2+(c-45)**2)*.000000025
                if cost<score:score=cost;best=(a,b,c)
    return best
closed={f:finger_points(f,closed_angles(f)) for f in range(2,6)}
draw={f:finger_points(f,(12,83,48) if f!=5 else (65,90,55)) for f in range(2,6)}
release={f:finger_points(f,(5,25,12) if f!=5 else (35,60,35)) for f in range(2,6)}
def thumb_pose(targets):
    result=[chains[1][0].copy()]
    for j,target in enumerate(targets):
        direction=Vector(target)-result[-1]
        result.append(result[-1]+direction.normalized()*(chains[1][j+1]-chains[1][j]).length)
    return result
closed[1]=thumb_pose(((.043,.065,-.012),(.029,.081,-.032),(.010,.073,-.042)))
draw[1]=thumb_pose(((.046,.062,.002),(.045,.082,-.001),(.033,.095,-.004)))
release[1]=thumb_pose(((.050,.060,.003),(.054,.082,.005),(.052,.101,.005)))

def bone_frame(a,b):
    y=(b-a).normalized();x=Vector((1,0,0));x=(x-y*x.dot(y)).normalized();z=x.cross(y)
    return Matrix((x,y,z)).transposed()
def transforms(pose):
    result={}
    for f,q in pose.items():
        result[f]=[]
        for j in range(3):
            rotation=bone_frame(q[j],q[j+1])@bone_frame(chains[f][j],chains[f][j+1]).inverted()
            result[f].append((rotation,q[j]-rotation@chains[f][j]))
    return result
weights=[]
for p in points:
    candidates=[]
    for f,chain in chains.items():
        for j in range(3):
            distance,t=segment_distance(p,chain[j],chain[j+1]);candidates.append((distance,f,j,t))
    distance,f,j,t=min(candidates)
    chain=chains[f];lengths=[(chain[k+1]-chain[k]).length for k in range(3)]
    s=sum(lengths[:j])+lengths[j]*t
    along=(p-chain[0]).dot((chain[1]-chain[0]).normalized()) if j==0 else s
    influence=smoothstep((along+.010)/.024)*(1-smoothstep((distance-.012)/.012))
    w=[0,0,0];w[j]=1
    for joint_index in (1,2):
        boundary=sum(lengths[:joint_index]);width=min(lengths[joint_index-1],lengths[joint_index])*.24
        if abs(s-boundary)<width:
            blend=smoothstep((s-boundary+width)/(2*width));w=[0,0,0];w[joint_index-1]=1-blend;w[joint_index]=blend
    weights.append((f,influence,w))
def deform(pose):
    matrices=transforms(pose);result=[]
    for p,(f,influence,w) in zip(points,weights):
        posed=sum(((matrices[f][j][0]@p+matrices[f][j][1])*w[j] for j in range(3)),Vector())
        result.append(p.lerp(posed,influence))
    return result
poses={'Power':deform(closed),'BowDrawHand':deform(draw),'BowReleaseHand':deform(release)}
HAND_GRIPS=[];HAND_FRAMES={};HAND_MOTION_FIELDS={};HAND_JOINT_POSES={}
for obj in list(groups['Body']):
    if obj.name.startswith(('Anatomical palm','Curled gripping finger','Opposed gripping thumb')):
        groups['Body'].remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
current='Body'
for label in ('L','R'):
    bone=label+' Hand';wrist=joint(bone);distal=(wrist-joint(label+' Forearm')).normalized()
    across=Vector((0,-1,0));across=(across-distal*across.dot(distal)).normalized()
    frame_normal=across.cross(distal).normalized()
    # MakeHuman and the Unity character use opposite handed coordinate frames.
    # Reflect the anatomical hand once; preserving its source winding here had
    # put the left-hand surface on the right wrist and vice versa.
    dorsal=frame_normal*(1 if label=='R' else -1)
    source_forearm=(-canonical(markers['elbow'])).normalized()
    forearm_fit=source_forearm.rotation_difference(Vector((0,1,0)))
    def place(p):
        proximal=smoothstep((-p.y-.005)/.060)
        p=p.lerp(forearm_fit@p,proximal)
        return wrist+across*p.x+distal*p.y+dorsal*p.z
    grip=place(GRIP)
    # Nock between the index and middle finger, against the hooked pads.
    hook=(draw[2][1]+draw[3][1])*.5+Vector((0,-.006,-.004))
    HAND_GRIPS.append(dict(side=label,position=xyz(grip),along=xyz(across),normal=xyz(frame_normal),drawContact=xyz(place(hook))))
    HAND_FRAMES[label]=(wrist.copy(),distal.copy(),across.copy(),dorsal.copy(),grip.copy())
    hand=mesh('Anatomical articulated gripping hand '+label,[place(p) for p in poses['Power']],source_faces,8)
    hand['anatomicalHand']=label
    bm=bmesh.new();bm.from_mesh(hand.data);bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=0)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(hand.data);bm.free()
    # Native quad topology retains the nails and dorsal knuckle structure.
    hand.shape_key_add(name='Basis')
    for name in ('BowDrawHand','BowReleaseHand'):
        key=hand.shape_key_add(name=name)
        for v,p in zip(key.data,poses[name]):v.co=B(place(p))
    mod=hand.modifiers.new('Anatomical hand surface','SUBSURF');mod.levels=1;mod.render_levels=1
    for p in hand.data.polygons:p.use_smooth=True
    bpy.context.view_layer.update()
    deps=bpy.context.evaluated_depsgraph_get()
    def evaluated_positions():
        ev=hand.evaluated_get(deps);data=ev.to_mesh();result=[U(v.co) for v in data.vertices];ev.to_mesh_clear();return result
    basis=evaluated_positions();deltas={}
    for name in ('BowDrawHand','BowReleaseHand'):
        hand.data.shape_keys.key_blocks[name].value=1;bpy.context.view_layer.update()
        target=evaluated_positions();assert len(target)==len(basis)
        deltas[name]=[q-p for p,q in zip(basis,target)]
        hand.data.shape_keys.key_blocks[name].value=0;bpy.context.view_layer.update()
    tree=KDTree(len(basis))
    for i,p in enumerate(basis):tree.insert(p,i)
    tree.balance();HAND_MOTION_FIELDS[label]=(tree,deltas)
    HAND_JOINT_POSES[label]={str(f):[xyz(place(p)) for p in closed[f]] for f in closed}

# Preserve the equipment contracts and fit the heater bar to the real palm.
for obj in list(groups['Shield']):
    if obj.name.startswith('Leather bound palm grip'):
        groups['Shield'].remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
groups['ShieldHandle']=[];current='ShieldHandle'
sweep('Palm fitted leather heater handle',[(-.052,0,0),(0,0,0),(.052,0,0)],.0155,3,18,28,flatten=.92)
for x in (-.053,.053):sweep('Heater handle leather anchor',[(x,0,0),(x,0,.035),(x,0,.075)],.008,3,12,16,flatten=.65)
for obj in groups['Bow']:
    if obj.name.startswith('Continuous yew bow limb'):
        for v in obj.data.vertices:
            p=U(v.co);p.x*=1-.25*(1-smoothstep((abs(p.y)-.085)/.080));v.co=B(p)
    if obj.name.startswith(('Leather bow grip','Bow spiral grip binding')):
        for v in obj.data.vertices:
            p=U(v.co);centre=bow_pos((p.y+.62)/1.24);p.x*=.62;p.z=centre.z+(p.z-centre.z)*.72;v.co=B(p)
previous_skin_weights4=skin_weights4
def skin_weights4(obj,pos):
    side=obj.get('anatomicalHand')
    if not side:return previous_skin_weights4(obj,pos)
    wrist,distal,_,_,_=HAND_FRAMES[side];w=smoothstep(((pos-wrist).dot(distal)+.020)/.045)
    return sorted([(bone_names.index('Bip001 '+side+' Hand'),round(w,6)),(bone_names.index('Bip001 '+side+' Forearm'),round(1-w,6))],key=lambda pair:-pair[1])
previous_garment_motion=garment_motion
def garment_motion(obj,pos):
    if obj.get('anatomicalHand')!='R':return previous_garment_motion(obj,pos)
    tree,deltas=HAND_MOTION_FIELDS['R'];_,index,distance=tree.find(pos)
    assert distance<.0001,'Finger pose topology changed during finishing'
    return tuple((name,values[index]) for name,values in deltas.items())
print('MARCHWARDEN anatomical hands:',len(points),'CC0 control vertices;',len(basis),'surface vertices; MCP/PIP/DIP poses',flush=True)
