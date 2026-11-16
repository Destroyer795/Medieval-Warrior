"""Body composition, independent forged cuirasses and skinned tissue injuries."""
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

# Chest, waist, pelvis, upper limbs and lower limbs are varied independently.
COMPOSITIONS={
 'Walker':(1.00,.94,.89,.96,.95), 'Runner':(.86,.80,.82,.80,.89),
 'Brute':(1.38,1.17,1.10,1.48,1.32), 'Spitter':(.91,.85,.85,.84,.90),
 'Bloater':(1.14,1.48,1.13,1.08,1.12), 'Warlord':(1.23,1.10,1.02,1.24,1.20),
 'Swordsman':(1.12,1.00,.94,1.08,1.07), 'Bulwark':(1.18,1.12,1.02,1.18,1.15)}

def sculpt_body(p,index):
    q=p.copy();chest,waist,hips,arms,legs=COMPOSITIONS[kind]
    trunk=sum(w for b,w in skin[index] if names[b] in ('Base HumanPelvis','Base HumanSpine1','Base HumanSpine2','Base HumanRibcage'))
    if trunk>0 and .90<q.y<1.55:
        region=trunk*smooth((q.y-.90)/.15)*(1-smooth((q.y-1.44)/.11))
        upper=math.exp(-((q.y-1.34)/.16)**4);middle=math.exp(-((q.y-1.08)/.15)**4)
        q.x*=1+((chest-1)*upper+(waist-1)*middle)*region
        if q.z>.025 and 1.25<q.y<1.46:
            amount=smooth((q.y-1.25)/.035)*(1-smooth((q.y-1.41)/.05))*smooth((q.z-.025)/.025)*trunk
            # One broad rib-cage plane; do not retain the neutral base's breast cups.
            envelope=(.115-.045*(q.x/max(.18,.22*chest))**2)*(.90+.10*chest)
            q.z=q.z*(1-amount)+envelope*amount
        q.z*=1+(chest-1)*upper*.55*region
        if kind=='Bloater' and q.z>-.035:q.z+=.125*middle*smooth((q.z+.035)/.08)*region
        if kind in ('Runner','Spitter') and q.z>.025:
            q.z+=.003*math.sin(q.y*105)*math.exp(-((abs(q.x)-.11)/.05)**4)*upper*region
    # Smooth, shallower gluteal mass with a continuous waist-to-thigh transition.
    hip=math.exp(-((q.y-.875)/.125)**4)*smooth((.28-abs(q.x))/.06)
    if hip>0:
        q.x*=1+(hips-1)*hip
        if q.z<-.055:
            rear=-.118-.020*math.exp(-((abs(q.x)-.080)/.060)**2)
            blend=hip*.72*smooth((-q.z-.055)/.055)
            q.z=q.z*(1-blend)+rear*blend
    for bone,weight in skin[index]:
        segment=next((s for s in segments if s['index']==bone and any(a in s['name'] for a in ('Upperarm','Forearm','Thigh','Calf'))),None)
        if segment is None:continue
        axis=segment['tb']-segment['ta'];t=max(0,min(1,(q-segment['ta']).dot(axis)/axis.length_squared))
        center=segment['ta']+axis*t;radial=q-center;radial-=axis.normalized()*radial.dot(axis.normalized())
        factor=arms if 'Arm' in segment['name'] else legs
        q+=radial*(factor-1)*math.sin(t*math.pi)**.75*weight
    if 1.425<q.y<1.51 and .075<abs(q.x)<.27:
        q.y-=.018*math.exp(-((abs(q.x)-.17)/.075)**4)*math.exp(-((q.y-1.48)/.045)**4)
    if q.y>1.55:
        jaw=(1.04,.91,1.20,.94,1.12,1.15,1.08,1.13)[seed]
        q.x*=1+(jaw-1)*math.exp(-((q.y-1.595)/.047)**4)
        if q.z>.025:
            q.z-=.018*math.exp(-((abs(q.x)-.05)/.025)**2-((q.y-1.632)/.036)**2)
            q.z-=.010*math.exp(-((abs(q.x)-.033)/.022)**2-((q.y-1.666)/.020)**2)
            q.z+=.008*math.exp(-((abs(q.x)-.033)/.032)**4-((q.y-1.691)/.013)**2)
            lip=math.exp(-(q.x/.040)**4-((q.y-1.596)/.022)**4)
            if q.y<1.602:q.y-=.012*lip
            q.x+=(.004 if seed%2 else -.004)*math.exp(-(q.x/.014)**2-((q.y-1.653)/.028)**2)
    return q

SITES={
 'Walker':[((-.052,1.634,.115),.025,.038,.012),((.46,1.22,.025),.023,.054,.010)],
 'Runner':[((-.18,1.42,.045),.038,.050,.012),((.15,.32,.035),.026,.065,.014),((.12,1.26,.10),.035,.060,.012)],
 'Brute':[((.29,1.37,.015),.048,.073,.017),((-.052,1.635,.11),.035,.045,.014)],
 'Spitter':[((0,1.505,.075),.030,.055,.012),((-.44,1.22,.025),.025,.050,.013)],
 'Bloater':[((.055,1.13,.27),.060,.105,.025),((.09,1.25,-.16),.045,.065,.015)],
 'Warlord':[((.48,1.22,.025),.030,.066,.014),((-.050,1.637,.11),.028,.042,.013)],
 'Swordsman':[((-.45,1.22,.025),.025,.062,.012),((.040,1.686,.09),.020,.028,.010)],
 'Bulwark':[((-.15,.51,.04),.030,.050,.012),((.046,1.631,.11),.027,.040,.013)]}

def prepare_wounds(points,weights):
    global injuries,injury_bvh,injury_tree,injury_weights
    injury_weights=weights
    retained=[face for face in source_faces if not any(hand_removed[i] for i in face)]
    injury_bvh=BVHTree.FromPolygons(points,retained,all_triangles=False)
    retained_vertices={i for face in retained for i in face};injury_tree=KDTree(len(retained_vertices))
    for i in retained_vertices:injury_tree.insert(points[i],i)
    injury_tree.balance();injuries=[]
    for center,rx,ry,depth in SITES[kind]:
        p,n,_,_=injury_bvh.find_nearest(Vector(center));up=Vector((0,1,0));v=up-n*up.dot(n)
        if v.length<.15:v=Vector((0,0,1))-n*n.z
        v.normalize();u=v.cross(n).normalized()
        injuries.append(dict(p=p,n=n,u=u,v=v,rx=rx,ry=ry,depth=depth))
    for i,p in enumerate(points):
        for wound in injuries:
            r=injury_radius(p,wound)
            if r<1:points[i]-=wound['n']*wound['depth']*(1-r*r)**2

def injury_radius(p,w):
    d=p-w['p']
    if abs(d.dot(w['n']))>.055:return 99
    return math.sqrt((d.dot(w['u'])/w['rx'])**2+(d.dot(w['v'])/w['ry'])**2)

def torn_at_injury(p):return any(injury_radius(p,w)<.94 for w in injuries)

def make_wounds():
    for number,w in enumerate(injuries):
        for layer in range(2):
            vertices=[];faces=[];ws=[];colors=[];columns=40
            rings=(0,.2,.45,.70,.86) if layer==0 else (.79,.90,1.01)
            for radius in rings:
                for k in range(columns):
                    theta=k/columns*math.tau
                    ragged=1+.075*math.sin(theta*7+number)+.04*math.sin(theta*13+seed)
                    p=w['p']+w['u']*(math.cos(theta)*w['rx']*radius*ragged)+w['v']*(math.sin(theta)*w['ry']*radius*ragged)
                    surface,n,_,_=injury_bvh.find_nearest(p)
                    r=injury_radius(surface,w)
                    incision=w['depth']*max(0,1-r*r)**2
                    fibre=.0008*math.sin(theta*19+radius*42)*(1-radius)
                    vertices.append(surface-w['n']*incision+n*(.0014+fibre))
                    _,index,_=injury_tree.find(surface);ws.append(injury_weights[index])
                    shade=.70+.25*math.sin(theta*9+radius*35)**2
                    colors.append((shade,shade*.83,shade*.78))
            for row in range(len(rings)-1):
                for k in range(columns):a=row*columns+k;b=row*columns+(k+1)%columns;faces.append((a,a+columns,b+columns,b))
            tissue=add_mesh(('Recessed muscle fibres' if layer==0 else 'Ragged clotted wound margin')+' '+str(number),vertices,faces,11 if layer==0 else 10,ws,colors)
            tissue.modifiers.new('Thin exposed tissue surface','SOLIDIFY').thickness=.0008

def make_breeches():
    # A tailored fabric envelope has no skin cleft or genital topology.
    chest,waist,hips,arms,legs=COMPOSITIONS[kind]
    vertices=[];faces=[];weights=[];columns=64;rows=12;parts=[]
    waist_points=[p for i,p in enumerate(body_points) if .975<p.y<1.035 and not hand_removed[i] and not any('Arm' in names[b] and w>.3 for b,w in body_weights[i])]
    waist_rx=float(np.quantile([abs(p.x) for p in waist_points],.98))+.012
    waist_front=max(p.z for p in waist_points)+.012;waist_back=min(p.z for p in waist_points)-.012
    waist_z=(waist_front+waist_back)*.5;waist_rz=(waist_front-waist_back)*.5
    for row in range(rows+1):
        t=row/rows
        for k in range(columns):
            angle=k/columns*math.tau;bottom=.790+.060*math.sin(angle)**2
            y=bottom+(1.005-bottom)*t
            rx=(.192*hips+.012)*(1-smooth(t))+waist_rx*smooth(t)
            rz=.148*hips*(1-smooth(t))+waist_rz*smooth(t)
            fold=.0023*math.sin(angle*15+seed)*math.sin(t*math.pi)
            center_z=-.020*(1-smooth(t))+waist_z*smooth(t)
            vertices.append(Vector((math.sin(angle)*(rx+fold),y,center_z+math.cos(angle)*(rz+fold))))
            weights.append([(jid('Pelvis'),1)])
    for row in range(rows):
        for k in range(columns):a=row*columns+k;b=row*columns+(k+1)%columns;faces.append((a,b,b+columns,a+columns))
    faces.extend([tuple(reversed(range(columns))),tuple(rows*columns+k for k in range(columns))])
    hip=add_mesh('Continuous sewn breeches',vertices,faces,1,weights);parts.append(hip)
    for sign in (-1,1):
        thigh='LLegThigh' if sign<0 else 'RThigh';calf='LLegCalf' if sign<0 else 'RCalf'
        vertices=[];faces=[];weights=[];columns=48;rows=15
        for row in range(rows+1):
            t=row/rows;y=.605+( .925-.605)*t
            leg_t=(J(thigh).y-y)/(J(thigh).y-J(calf).y);center=J(thigh).lerp(J(calf),leg_t)
            rx=(.068+.018*t)*legs+.012;rz=(.086+.020*t)*legs+.010
            for k in range(columns):
                angle=k/columns*math.tau;fold=.003*math.sin(angle*11+seed)*(.7+.3*t)
                hem=.010*math.sin(angle*7+seed)*(1-t)**8
                vertices.append(Vector((center.x+math.sin(angle)*(rx+fold),y+hem,center.z+math.cos(angle)*(rz+fold))))
                pelvis=smooth((y-.80)/.12);weights.append([(jid(thigh),1-pelvis),(jid('Pelvis'),pelvis)])
        for row in range(rows):
            for k in range(columns):a=row*columns+k;b=row*columns+(k+1)%columns;faces.append((a,b,b+columns,a+columns))
        faces.extend([tuple(reversed(range(columns))),tuple(rows*columns+k for k in range(columns))])
        leg=add_mesh('Loose articulated trouser leg',vertices,faces,1,weights);parts.append(leg)
    # Fuse the seat and fork into one fabric surface, rather than overlapping a
    # hip apron and two legs. Rebuild skin weights after the voxel union.
    bpy.ops.object.select_all(action='DESELECT')
    for part in parts:objects.remove(part);part.select_set(True)
    bpy.context.view_layer.objects.active=hip;bpy.ops.object.join();objects.append(hip)
    remesh=hip.modifiers.new('Continuous cloth fork','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.007
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    relax=hip.modifiers.new('Relax sewn fabric','SMOOTH');relax.factor=.70;relax.iterations=5
    bpy.ops.object.modifier_apply(modifier=relax.name)
    reduce=hip.modifiers.new('Cloth topology budget','DECIMATE');reduce.ratio=.48
    bpy.ops.object.modifier_apply(modifier=reduce.name)
    hip.vertex_groups.clear()
    for name in names:hip.vertex_groups.new(name=name)
    hip.data.materials.clear();hip.data.materials.append(materials[1])
    for vertex in hip.data.vertices:
        p=U(vertex.co);pelvis=max(smooth((p.y-.78)/.14),math.exp(-(p.x/.05)**2)*smooth((p.y-.72)/.12))
        right=smooth((p.x+.035)/.070)
        for bone,weight in ((jid('Pelvis'),pelvis),(jid('RThigh'),(1-pelvis)*right),(jid('LLegThigh'),(1-pelvis)*(1-right))):
            if weight>0:hip.vertex_groups[bone].add([vertex.index],weight,'REPLACE')
    for polygon in hip.data.polygons:polygon.use_smooth=True
    for layer in list(hip.data.uv_layers):hip.data.uv_layers.remove(layer)
    uv=hip.data.uv_layers.new(name='Surface UV')
    for loop in hip.data.loops:
        p=U(hip.data.vertices[loop.vertex_index].co);uv.data[loop.index].uv=(p.x*2,p.y*2)
    for attribute in list(hip.data.color_attributes):hip.data.color_attributes.remove(attribute)
    colors=hip.data.color_attributes.new(name='Decay',type='FLOAT_COLOR',domain='POINT')
    for color in colors.data:color.color=(1,1,1,1)

def make_cuirass():
    bulk=COMPOSITIONS[kind][0];vertices=[];faces=[];ws=[];rows=18;cols=28
    for back in (False,True):
        start=len(vertices)
        for j in range(rows+1):
            t=j/rows
            for k in range(cols+1):
                s=-1+k/cols*2;top=1.463-.028*abs(s)**3;y=1.015+(top-1.015)*t
                width=float(np.interp(y,(1.015,1.16,1.32,1.46),(.158,.202,.228,.166)))*bulk
                x=s*width
                profile=float(np.interp(y,(1.015,1.12,1.28,1.40,1.463),(.145,.185,.205,.178,.135)))
                curvature=math.sqrt(max(0,1-s*s)*.91+.09)
                z=(-.020-.160*curvature if back else -.020+profile*curvature+.007*math.exp(-(s/.18)**2))*(.80+.20*bulk)
                z+=.0015*math.sin(k*1.77+j*.53)*math.sin(j*.81)
                vertices.append(Vector((x,y,z)))
                w=smooth((y-1.12)/.30);ws.append([(jid('Spine2'),1-w),(jid('Ribcage'),w)])
        for j in range(rows):
            for k in range(cols):a=start+j*(cols+1)+k;f=(a,a+1,a+cols+2,a+cols+1);faces.append(tuple(reversed(f)) if back else f)
    rear=(rows+1)*(cols+1)
    for side in (0,cols):
        for j in range(rows):
            a=j*(cols+1)+side;b=(j+1)*(cols+1)+side
            faces.append((a,b,rear+b,rear+a) if side==0 else (b,a,rear+a,rear+b))
    shell=add_mesh('Forged closed front and back cuirass',vertices,faces,2,ws)
    shell.modifiers.new('Forged plate thickness','SOLIDIFY').thickness=.008
    bevel=shell.modifiers.new('Rounded forged edges','BEVEL');bevel.width=.0018;bevel.segments=2;bevel.limit_method='ANGLE';bevel.angle_limit=.75
    # Wide shoulder bridges and a raised central neck guard replace the scoop neck.
    for sign in (-1,1):
        points=[];quads=[]
        for row in range(13):
            t=row/12
            for side in (-1,1):points.append(Vector((sign*.135*bulk+side*.023,1.456+.026*math.sin(t*math.pi),.115-t*.275)))
        for row in range(12):a=row*2;quads.append((a,a+1,a+3,a+2))
        strap=rigid('Broad riveted shoulder bridge',points,quads,2,'Ribcage');strap.modifiers.new('Bridge thickness','SOLIDIFY').thickness=.008
    points=[];quads=[]
    for row in range(4):
        for k in range(21):
            s=-1+k/10;points.append(Vector((s*.077,1.455+row*.014,.059+.023*(1-s*s))))
    for row in range(3):
        for k in range(20):a=row*21+k;quads.append((a,a+1,a+22,a+21))
    collar=rigid('Raised forged gorget',points,quads,2,'Ribcage');collar.modifiers.new('Gorget thickness','SOLIDIFY').thickness=.006
