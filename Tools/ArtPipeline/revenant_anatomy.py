"""Body composition, independent forged cuirasses and skinned tissue injuries."""
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

# Chest, waist, pelvis, upper limbs and lower limbs are varied independently.
COMPOSITIONS={
 'Walker':(1.10,.95,.94,1.08,1.04), 'Runner':(.96,.79,.86,.92,.98),
 'Brute':(1.46,1.19,1.12,1.60,1.39), 'Spitter':(.98,.83,.90,.91,.99),
 'Bloater':(1.24,1.58,1.15,1.12,1.19), 'Warlord':(1.26,1.10,1.02,1.29,1.23),
 'Swordsman':(1.12,1.00,.94,1.08,1.07), 'Bulwark':(1.18,1.12,1.02,1.18,1.15)}

def sculpt_body(p,index):
    q=p.copy();chest,waist,hips,arms,legs=COMPOSITIONS[kind]
    trunk=sum(w for b,w in skin[index] if names[b] in ('Base HumanPelvis','Base HumanSpine1','Base HumanSpine2','Base HumanRibcage'))
    if trunk>0 and .90<q.y<1.55:
        region=trunk*smooth((q.y-.90)/.15)*(1-smooth((q.y-1.44)/.11))
        upper=math.exp(-((q.y-1.34)/.16)**4);middle=math.exp(-((q.y-1.08)/.15)**4)
        q.x*=1+((chest-1)*upper+(waist-1)*middle)*region
        if q.z>.018 and 1.16<q.y<1.49:
            # Shape the sternum continuously from the abdomen to the clavicles.
            depth=.059+.030*math.exp(-((q.y-1.29)/.115)**2)
            cross_section=max(0,1-(q.x/(.215*chest))**2)
            envelope=.018+(depth-.018)*cross_section*(.94+.06*chest)
            amount=smooth((q.y-1.16)/.072)*(1-smooth((q.y-1.39)/.10))*smooth((q.z-.018)/.036)*trunk
            q.z+=(envelope-q.z)*amount
        if q.z<0:q.z*=1+(chest-1)*upper*.28*region
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
    # Rounded deltoids and sloping clavicles replace the squared shoulder ridge.
    if 1.36<q.y<1.53 and .09<abs(q.x)<.34:
        shoulder=math.exp(-((abs(q.x)-.20)/.10)**4-((q.y-1.455)/.065)**4)
        q.y-=.014*shoulder*smooth((abs(q.x)-.13)/.10)
        q.z+=.009*shoulder*smooth((q.z+.025)/.06)
    if 1.46<q.y<1.60 and abs(q.x)<.09:
        neck=math.exp(-((q.y-1.525)/.065)**4)
        q.x*=1+.15*neck
        if kind=='Spitter':q.z+=.024*neck*smooth((q.z+.005)/.07)
    if q.y>1.55:
        jaw=(1.04,.91,1.20,.94,1.12,1.15,1.08,1.13)[seed]
        q.x*=1+(jaw-1)*math.exp(-((q.y-1.595)/.047)**4)
        if q.z>.025:
            q.z-=.018*math.exp(-((abs(q.x)-.05)/.025)**2-((q.y-1.632)/.036)**2)
            q.z-=.010*math.exp(-((abs(q.x)-.033)/.022)**2-((q.y-1.666)/.020)**2)
            q.z+=.008*math.exp(-((abs(q.x)-.033)/.032)**4-((q.y-1.691)/.013)**2)
            lip=math.exp(-(q.x/.040)**4-((q.y-1.596)/.022)**4)
            if q.y<1.602:q.y-=.012*lip
            # Retracted lips, a hanging jaw, hollow temples and a stronger brow.
            jaw_open=(.009,.014,.012,.017,.013,.009,.008,.010)[seed]
            q.y-=jaw_open*math.exp(-(q.x/.055)**4-((q.y-1.577)/.036)**4)*smooth((q.z-.035)/.045)
            q.z-=.006*math.exp(-((abs(q.x)-.072)/.019)**2-((q.y-1.70)/.038)**2)
            # Slant the brow toward the bridge. The outer cheek catches light
            # above a hollow that runs diagonally back toward the jaw.
            brow_y=1.680+.23*min(abs(q.x),.065)
            brow=math.exp(-((abs(q.x)-.030)/.032)**4-((q.y-brow_y)/.014)**2)
            q.z+=.012*brow;q.y-=.005*brow
            cheek_y=1.644+.23*(abs(q.x)-.047)
            q.z+=.009*math.exp(-((abs(q.x)-.053)/.020)**2-((q.y-cheek_y)/.013)**2)
            q.z-=.012*math.exp(-((abs(q.x)-.050)/.019)**2-((q.y-1.612)/.023)**2)
            q.x+=(.004 if seed%2 else -.004)*math.exp(-(q.x/.014)**2-((q.y-1.653)/.028)**2)
    return q

SITES={
 'Walker':[((-.057,1.620,.115),.019,.026,.010),((.46,1.22,.025),.023,.054,.010)],
 'Runner':[((-.18,1.42,.045),.038,.050,.012),((.15,.32,.035),.026,.065,.014),((.12,1.26,.10),.035,.060,.012)],
 'Brute':[((.29,1.37,.015),.048,.073,.017),((-.059,1.625,.11),.025,.031,.014)],
 'Spitter':[((0,1.505,.075),.030,.055,.012),((-.44,1.22,.025),.025,.050,.013)],
 'Bloater':[((.055,1.13,.27),.060,.105,.025),((.09,1.25,-.16),.045,.065,.015)],
 'Warlord':[((.48,1.22,.025),.030,.066,.014),((-.058,1.623,.11),.021,.028,.013)],
 'Swordsman':[((-.45,1.22,.025),.025,.062,.012),((.050,1.724,.09),.014,.017,.008)],
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
            rx=(.174*hips+.012)*(1-smooth(t))+waist_rx*smooth(t)
            rz=.132*hips*(1-smooth(t))+waist_rz*smooth(t)
            fold=.0023*math.sin(angle*15+seed)*math.sin(t*math.pi)
            center_z=-.020*(1-smooth(t))+waist_z*smooth(t)
            vertices.append(Vector((math.sin(angle)*(rx+fold),y,center_z+math.cos(angle)*(rz+fold))))
            weights.append([(jid('Pelvis'),1)])
    for row in range(rows):
        for k in range(columns):a=row*columns+k;b=row*columns+(k+1)%columns;faces.append((a,b,b+columns,a+columns))
    faces.extend([tuple(reversed(range(columns))),tuple(rows*columns+k for k in range(columns))])
    hip=add_mesh('Continuous sewn breeches',vertices,faces,12,weights);parts.append(hip)
    for sign in (-1,1):
        thigh='LLegThigh' if sign<0 else 'RThigh';calf='LLegCalf' if sign<0 else 'RCalf'
        vertices=[];faces=[];weights=[];columns=48;rows=15
        for row in range(rows+1):
            t=row/rows;y=PANT_HEMS[kind]+(.925-PANT_HEMS[kind])*t
            if y>=J(calf).y:
                leg_t=(J(thigh).y-y)/(J(thigh).y-J(calf).y);center=J(thigh).lerp(J(calf),leg_t)
            else:
                ankle=J('LLegFoot' if sign<0 else 'RFoot');center=J(calf).lerp(ankle,(J(calf).y-y)/(J(calf).y-ankle.y))
            taper=float(np.interp(y,(.30,.50,.65,.925),(.65,.75,.90,1)))
            rx=(.068+.018*t)*legs*taper+.012;rz=(.086+.020*t)*legs*taper+.010
            for k in range(columns):
                angle=k/columns*math.tau;fold=.003*math.sin(angle*11+seed)*(.7+.3*t)
                hem=.010*math.sin(angle*7+seed)*(1-t)**8
                vertices.append(Vector((center.x+math.sin(angle)*(rx+fold),y+hem,center.z+math.cos(angle)*(rz+fold))))
                pelvis=smooth((y-.80)/.12);weights.append([(jid(thigh),1-pelvis),(jid('Pelvis'),pelvis)])
        for row in range(rows):
            for k in range(columns):a=row*columns+k;b=row*columns+(k+1)%columns;faces.append((a,b,b+columns,a+columns))
        faces.extend([tuple(reversed(range(columns))),tuple(rows*columns+k for k in range(columns))])
        leg=add_mesh('Loose articulated trouser leg',vertices,faces,12,weights);parts.append(leg)
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
    hip.data.materials.clear();hip.data.materials.append(materials[12]);hip['surface']=12
    for vertex in hip.data.vertices:
        p=U(vertex.co);pelvis=max(smooth((p.y-.78)/.14),math.exp(-(p.x/.05)**2)*smooth((p.y-.72)/.12))
        right=smooth((p.x+.035)/.070)
        knee=smooth((.565-p.y)/.13)
        for bone,weight in ((jid('Pelvis'),pelvis),(jid('RThigh'),(1-pelvis)*right*(1-knee)),(jid('LLegThigh'),(1-pelvis)*(1-right)*(1-knee)),
                (jid('RCalf'),(1-pelvis)*right*knee),(jid('LLegCalf'),(1-pelvis)*(1-right)*knee)):
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
    # The breastplate follows the shallower sternum. Keep the back and side
    # clearance for the shoulder blades and the padding under the plate.
    bulk=COMPOSITIONS[kind][0];vertices=[];faces=[];ws=[];rows=20;cols=64
    def section(y,a):
        c=math.cos(a)
        width=float(np.interp(y,(1.005,1.10,1.25,1.37,1.465),(.156,.174,.218,.227,.151)))*bulk
        front=float(np.interp(y,(1.005,1.15,1.30,1.42,1.465),(.137,.144,.158,.137,.094)))
        back=float(np.interp(y,(1.005,1.15,1.30,1.42,1.465),(.137,.158,.178,.157,.100)))
        return math.sin(a)*width,-.025+c*(front if c>0 else back)*(.89+.11*bulk)
    for row in range(rows+1):
        t=row/rows
        for k in range(cols):
            a=k/cols*math.tau;side=abs(math.sin(a))
            top=1.465-.095*side**5;y=1.005+(top-1.005)*t
            x,z=section(y,a);z+=.0012*math.sin(k*1.7+row*.8)
            vertices.append(Vector((x,y,z)))
            w=smooth((y-1.12)/.29);ws.append([(jid('Spine2'),1-w),(jid('Ribcage'),w)])
    for row in range(rows):
        for k in range(cols):a=row*cols+k;b=row*cols+(k+1)%cols;faces.append((a,b,b+cols,a+cols))
    plate=add_mesh('Curved waisted forged cuirass with open armholes',vertices,faces,2,ws)
    plate.modifiers.new('Forged plate thickness','SOLIDIFY').thickness=.006
    bevel=plate.modifiers.new('Rounded forged edges','BEVEL');bevel.width=.002;bevel.segments=2;bevel.limit_method='ANGLE';bevel.angle_limit=.75
    # Separate shoulder bridges, collar and small peened fasteners.
    for sign in (-1,1):
        points=[];quads=[]
        for row in range(13):
            t=row/12
            for side in (-1,1):points.append(Vector((sign*.127*bulk+side*.021,1.443+.025*math.sin(t*math.pi),.076-t*.221)))
        for row in range(12):a=row*2;quads.append((a,a+1,a+3,a+2))
        strap=rigid('Broad riveted shoulder bridge',points,quads,2,'Ribcage');strap.modifiers.new('Bridge thickness','SOLIDIFY').thickness=.008
        for y in (1.10,1.21,1.32):
            x,z=section(y,.78*sign)
            rivet=ellipsoid('Peened side fastening',(x,y,z+.003),(.006,.006,.004),6,'Spine2',12,8)
            w=smooth((y-1.12)/.29)
            for bone,weight in (('Spine2',1-w),('Ribcage',w)):
                rivet.vertex_groups[names[jid(bone)]].add(list(range(len(rivet.data.vertices))),weight,'REPLACE')
    shell('Raised oval gorget',(0,0,-.025),.084,.091,1.446,1.485,2,'Ribcage',rows=3,cols=40)
