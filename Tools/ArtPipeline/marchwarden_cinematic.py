"""Reference-led cinematic warrior. Executed in marchwarden.py's namespace.

Continuous anatomical garments, real overlapping lamellae and linked mail. The
animation skeleton stays compatible with the game's locomotion and combat clips.
Four influences at shoulders/hips and sparse garment morphs travel with the mesh.
"""

# Retain the anatomical face and articulated grips; replace the old outfit.
keep = ('Customized CC0 anatomical head', 'Fitted anatomical eyeball', 'Hazel iris',
        'Eye pupil', 'Fitted natural eyebrow', 'Recessed lower eyelid line',
        'Anatomical palm', 'Curled gripping finger', 'Opposed gripping thumb')
for obj in list(groups['Body']):
    if not obj.name.startswith(keep):
        groups['Body'].remove(obj)
        bpy.data.objects.remove(obj, do_unlink=True)


def sample_profile(points, value):
    """Cubic Hermite interpolation in the actual, nonuniform profile coordinate."""
    if value <= points[0][0]: return tuple(points[0][1:])
    if value >= points[-1][0]: return tuple(points[-1][1:])
    k = next(i for i in range(len(points)-1) if points[i+1][0] >= value)
    p0, p1 = points[k], points[k+1]
    prev, after = points[max(0, k-1)], points[min(len(points)-1, k+2)]
    span = p1[0]-p0[0]
    t = (value-p0[0])/span
    return tuple((2*t**3-3*t*t+1)*p0[j] + (t**3-2*t*t+t)*span*(p1[j]-prev[j])/(p1[0]-prev[0])
                 + (-2*t**3+3*t*t)*p1[j] + (t**3-t*t)*span*(after[j]-p0[j])/(after[0]-p0[0])
                 for j in range(1, len(p0)))


def region(obj, name, side=None, motion=None):
    obj['region'] = name
    if side: obj['side'] = side
    if motion: obj['motion'] = motion
    return obj


def garment_patch(name, fn, nu, nv, mat, area, side=None, thickness=.002, motion=None):
    # Covered, closed garment surfaces do not need a second unseen inner shell.
    if name.startswith(('Continuous linen','Anatomically tailored','Continuous shaped',
                        'Continuous sculpted','Supple fitted')):thickness=0
    return region(patch(name, fn, nu, nv, mat, thickness, 0), area, side, motion)


def band(name, fn, width, mat, area, side=None, segments=80, motion=None):
    return garment_patch(name, lambda u, v: fn(u, (v-.5)*width), segments, 3,
                         mat, area, side, .0018, motion)


current = 'Body'
bone = 'Spine'
# A broad rib cage, pectoral volume, natural waist and an uninterrupted neckline.
torso_profile = [(.735,.160,.119,-.046), (.830,.175,.132,-.047),
                 (.940,.202,.151,-.050), (1.055,.228,.163,-.053),
                 (1.120,.231,.148,-.055), (1.165,.202,.119,-.055),
                 (1.205,.119,.082,-.053), (1.245,.068,.063,-.050)]


def torso_point(a, y, lift=0):
    w, d, z = sample_profile(torso_profile, y)
    bulk=math.exp(-((y-1.045)/.185)**4)
    w*=1+.15*bulk;d*=1+.14*bulk
    s, c = math.sin(a), math.cos(a)
    x = s*(w+lift)
    # The chest is shaped across its front, rather than a round barrel.
    pec = .009*math.exp(-((abs(x)-.107)/.071)**2-((y-1.070)/.065)**2)*max(0,c)**3
    sternum = -.004*math.exp(-(x/.020)**2-((y-1.075)/.10)**2)*max(0,c)**4
    folds = .003*math.sin(a*17+y*18)*math.exp(-((y-.84)/.11)**2)
    return Vector((x+s*folds, y, z+c*(d+lift+folds)+pec+sternum))


garment_patch('Continuous linen torso and neckline',
              lambda u,v: torso_point(u*math.tau,.748+v*.497),64,48,2,'torso')
band('Sewn linen collar',lambda u,dy:torso_point(u*math.tau,1.238+dy,.002),
     .014,2,'torso')
garment_patch('Supple fitted lamellar backing',
              lambda u,v:torso_point(u*math.tau,.823+v*.351,.004),64,32,3,'torso')

# Each plate has real curvature, thickness, overlap, an irregular hammered lip,
# dark lashings and small peened fasteners. Upper rows lap over the rows below.
for row in range(6):
    top = .884+row*.055
    for col in range(28):
        a0 = (col+(row%2)*.16)*math.tau/28
        def plate(u,v,a0=a0,top=top,row=row):
            a = a0+(.032+.936*u)*math.tau/28
            y = top-.066*v+.0008*math.sin(u*math.pi)
            lift = .009+.0020*(1-v)+.0007*math.sin(u*math.pi)
            p = torso_point(a,y,lift)
            p.y += .0006*math.sin(a*31+v*5)
            return p
        garment_patch('Overlapping blackened iron lamella',plate,6,5,0,'torso',thickness=.0022)
        for u in (.17,.83):
            p = plate(u,.15)
            fastener = rivet(p,13,.0028)
            region(fastener,'torso')
            region(binding('Waxed lamellar lacing',[plate(u,.055),plate(u+.013,.23),plate(u,.38)],3,.0011),'torso')

# Continuous sleeves include deltoid/biceps/triceps volume beneath loose linen.
# Broad hanging folds alternate with compression folds at the elbow.
for sign,label in ((-1,'L'),(1,'R')):
    upper, elbow, hand = joint(label+' UpperArm'),joint(label+' Forearm'),joint(label+' Hand')
    axis = (elbow-upper).normalized()
    front = Vector((0,0,1)); front = (front-axis*front.dot(axis)).normalized()
    lateral = axis.cross(front).normalized()
    bone = label+' UpperArm'
    sleeve_profile = [(-.27,.041,.047),(-.14,.075,.081),(.04,.093,.097),
                      (.25,.092,.097),(.55,.078,.084),(.84,.062,.070),
                      (1.02,.062,.065),(1.22,.069,.072),(1.48,.059,.066),
                      (1.73,.047,.050),(1.91,.027,.029)]
    def sleeve(u,v,upper=upper,elbow=elbow,hand=hand,front=front,lateral=lateral):
        t = -.27+v*2.18
        a = u*math.tau
        centre = upper.lerp(elbow,t) if t<=1 else elbow.lerp(hand,t-1)
        w,d = sample_profile(sleeve_profile,t)
        gravity = max(0,math.cos(a+1.1))
        fold = .0038*math.sin(a*7+t*4)*math.sin(v*math.pi)
        fold += .006*math.sin(t*36+a*2)*math.exp(-((t-.96)/.26)**2)
        fold += .003*math.sin(t*51-a*3)*math.exp(-((t-1.42)/.45)**2)
        sag = Vector((0,-.010*math.sin(v*math.pi)**2,0))
        return centre+sag+lateral*math.sin(a)*(w+fold)+front*math.cos(a)*(d+fold*(.6+gravity))
    garment_patch('Anatomically tailored flowing linen sleeve',sleeve,48,72,2,'arm',label)
    # The fitted bracer follows the extensor taper, leaving a gathered elbow.
    bone = label+' Forearm'
    def bracer(u,v,elbow=elbow,hand=hand,front=front,lateral=lateral):
        t=.30+.62*v; a=(u-.5)*math.tau
        w,d=sample_profile([(0,.079,.086),(.4,.070,.077),(1,.031,.033)],v)
        flute=.0013*math.cos(a*9)*math.sin(v*math.pi)
        return elbow.lerp(hand,t)+lateral*math.sin(a)*(w+flute)+front*math.cos(a)*(d+flute)
    garment_patch('Shaped leather archery bracer',bracer,40,22,3,'forearm',label,.003)
    for v in (.07,.85):
        def strap(u,t,v=v,bracer=bracer,elbow=elbow,hand=hand):
            along=v+(t-.5)*.075;c=elbow.lerp(hand,.30+.62*along)
            return c+(bracer(u,along)-c)*1.035
        garment_patch('Flat bracer fastening strap',strap,
                      40,3,3,'forearm',label,.002)
    for u in (.34,.50,.66):
        garment_patch('Forged bracer reinforcing rib',lambda x,v,u=u,bracer=bracer:bracer(u+(x-.5)*.018,.10+.81*v)+front*.002,
                      3,16,13,'forearm',label,.0016)
    # Small curved shoulder lamellae, with a soft armpit and no rigid sleeve cap.
    bone=label+' UpperArm'
    for row in range(3):
        for col in range(5):
            def shoulder(u,v,row=row,col=col,upper=upper,elbow=elbow,front=front,lateral=lateral):
                t=-.10+row*.16+v*.21
                a=-1.67+(col+(u*.94+.03))*3.34/5
                c=upper.lerp(elbow,t)
                w,d=sample_profile(sleeve_profile,t)
                return c+lateral*math.sin(a)*(w+.009)+front*math.cos(a)*(d+.009)
            garment_patch('Articulated shoulder lamella',shoulder,5,5,0,'arm',label,.002)
            for u in (.20,.80):region(rivet(shoulder(u,.17),13,.0025),'arm',label)

# A wide worn belt lies over the coat, with a real open-frame buckle.
bone='Pelvis'
garment_patch('Wide waist belt',lambda u,v:torso_point(u*math.tau,.803+v*.035,.012),64,6,3,'waist',thickness=.003)
for x in (-.028,.028):region(binding('Iron buckle upright',[(x,.803,.102),(x,.838,.102)],13,.003),'waist')
for y in (.803,.838):region(binding('Iron buckle bar',[(-.028,y,.102),(.028,y,.102)],13,.003),'waist')
region(binding('Iron buckle tongue',[(-.023,.821,.108),(.022,.821,.108)],12,.0018),'waist')

# Ten separate sewn gores open at the front/back and follow the appropriate leg.
def skirt_point(a,v,lift=0):
    width=.180+.049*math.sin(v*math.pi*.72)
    depth=.141+.034*v
    fold=(.008*math.sin(a*14+.8*v)+.003*math.sin(a*29-1.6*v))*smoothstep(v/.25)
    return Vector((math.sin(a)*(width+fold+lift),.801-.300*v+.007*math.sin(a*4+.8)*v*v,
                   -.046+math.cos(a)*(depth+fold+lift)))

for panel in range(10):
    a0=panel*math.tau/10
    label='R' if math.sin(a0+math.pi/10)>0 else 'L'
    def gore(u,v,a0=a0):return skirt_point(a0+(.014+.972*u)*math.tau/10,v)
    garment_patch('Folded split linen tunic gore',gore,14,28,2,'skirt',label,.0025,'hem')
    region(binding('Turned and stitched linen hem',[gore(k/36,.995) for k in range(37)],2,.0012),'skirt',label,'hem')
    # A shorter articulated skirt of metal scales protects the upper hip.
    for row in range(3):
        for col in range(2):
            def tasset(u,v,a0=a0,col=col,row=row):
                a=a0+(.02+(col+u*.93)*.49)*math.tau/10
                return skirt_point(a,.035+row*.155+v*.195,.012)
            garment_patch('Overlapping articulated hip lamella',tasset,5,5,0,'skirt',label,.002,'hem')
            for u in (.18,.82):region(rivet(tasset(u,.12),13,.0026),'skirt',label,'hem')

# Rounded quadriceps, a flexible knee and generous gathered wool/linen trousers.
for sign,label in ((-1,'L'),(1,'R')):
    hip,knee,ankle=joint(label+' Thigh'),joint(label+' Calf'),joint(label+' Foot')
    axis=(knee-hip).normalized();front=Vector((0,0,1));front=(front-axis*front.dot(axis)).normalized()
    lateral=axis.cross(front).normalized();bone=label+' Thigh'
    leg_profile=[(-.10,.080,.090),(.13,.098,.104),(.38,.095,.108),(.65,.078,.089),
                 (.90,.057,.064),(1.02,.055,.060),(1.22,.059,.064),(1.63,.047,.052),(1.98,.034,.040)]
    def trouser(u,v,hip=hip,knee=knee,ankle=ankle,front=front,lateral=lateral):
        t=-.10+v*1.29;a=u*math.tau
        c=hip.lerp(knee,t) if t<=1 else knee.lerp(ankle,t-1)
        w,d=sample_profile(leg_profile,t)
        taper=1-.13*smoothstep((t-.86)/.20);w*=taper;d*=taper
        fold=.004*math.sin(a*9+t*4)*math.sin(v*math.pi)
        fold+=.006*math.sin(t*40+a*2)*math.exp(-((t-.95)/.24)**2)
        return c+lateral*math.sin(a)*(w+fold)+front*math.cos(a)*(d+fold)
    garment_patch('Continuous shaped and gathered trousers',trouser,40,64,2,'leg',label,.0018)

    # One continuous leather upper from rounded toe, via vamp/ankle, to calf.
    bone=label+' Calf'
    boot_profile=[(.014,.049,.111,.035),(.034,.057,.126,.033),(.060,.055,.114,.027),
                  (.089,.043,.075,.004),(.120,.037,.045,-.006),(.165,.040,.048,-.004),
                  (.225,.054,.066,.009),(.295,.067,.079,.026),(.365,.067,.080,.034)]
    def boot(u,v,ankle=ankle,knee=knee,sign=sign):
        y=.014+v*.351;a=u*math.tau
        w,d,z=sample_profile(boot_profile,y)
        centre=Vector((ankle.x+(knee.x-ankle.x)*smoothstep((y-.12)/.23),y,ankle.z+z))
        # Off-centre toe, Achilles taper and soft compression wrinkles.
        asym=1+.08*sign*math.sin(a)*math.exp(-((y-.044)/.025)**2)
        crease=.0045*math.sin(y*192+a*2)*math.exp(-((y-.135)/.054)**2)
        crease+=.0026*math.sin(y*113-a*3)*math.exp(-((y-.292)/.075)**2)
        top_sag=.006*math.sin(a*3+.5)*smoothstep((y-.33)/.035)
        return centre+Vector((math.sin(a)*(w+crease)*asym,top_sag,math.cos(a)*(d+crease)))
    garment_patch('Continuous sculpted charred leather boot',boot,64,60,3,'boot',label,.003)
    for height in (.285,.337):
        v=(height-.014)/.351
        garment_patch('Flat distressed boot binding',lambda u,t,v=v,boot=boot:boot(u,v+(t-.5)*.026)+Vector((0,0,.001)),
                      48,3,3,'boot',label,.002)
    seampts=[boot(.25,.36+k/40*.61)+Vector((sign*.0013,0,0)) for k in range(41)]
    region(binding('Fitted boot side seam',seampts,3,.0011),'boot',label)
    bone=label+' Foot'
    sole=body_loft('Layered leather outsole',[(.005,.047,.108,ankle.z+.035),(.012,.057,.124,ankle.z+.033),
                    (.020,.058,.125,ankle.z+.033)],7,64,8)
    sole.location.x=ankle.x
    for k in range(2):
        region(binding('Recessed boot welt stitch',[(ankle.x+math.sin(a)*.057,.020+k*.002,ankle.z+.033+math.cos(a)*.124)
                      for a in np.linspace(0,math.tau,80)],3,.0008),'foot',label)

# The mail opens around the cheeks and drapes down onto the chest/shoulders.
bone='Head'
def aventail(u,v):
    close=smoothstep((v-.55)/.34)
    opening=.86*(1-close)
    a=opening+u*(math.tau-2*opening)
    widen=smoothstep((v-.43)/.57)
    w=.084+.140*widen
    d=.091+.060*widen
    fold=.004*math.sin(a*11+v*2)*math.sin(v*math.pi)
    y=1.420-.252*v+.009*math.cos(a)*v*v
    return Vector((math.sin(a)*(w+fold),y,-.049+math.cos(a)*(d+fold)))
garment_patch('Draped aventail underlining',aventail,64,38,4,'aventail',thickness=.002)
for u in (0,1):
    region(binding('Aventail face binding',[aventail(u,k/36*.82)+Vector((0,0,.001)) for k in range(37)],3,.0012),'aventail')

# Actual interlocked links are combined into one mesh, avoiding hundreds of
# renderers. Alternating link inclination and the open centres catch real light.
verts=[];faces=[];uvs=[]
for row in range(23):
    v=.018+row*.0415
    width=.084+.140*smoothstep((v-.43)/.57)
    close=smoothstep((v-.55)/.34);opening=.86*(1-close)
    columns=max(24,int((math.tau-2*opening)*width/.0108))
    for col in range(columns):
        u=(col+.50+(row%2)*.5)/columns
        if u>=1:continue
        c=aventail(u,v)
        tangent=(aventail(min(.9999,u+.0005),v)-aventail(max(.0001,u-.0005),v)).normalized()
        down=(aventail(u,min(.9999,v+.0005))-aventail(u,max(.0001,v-.0005))).normalized()
        normal=tangent.cross(down).normalized()
        outward=Vector((c.x,0,c.z+.049)).normalized()
        if normal.dot(outward)<0:normal=-normal
        c+=normal*.0022
        incline=.50 if (row+col)%2 else -.50
        axis_down=(down*math.cos(incline)+normal*math.sin(incline)).normalized()
        start=len(verts)
        for k in range(12):
            a=k*math.tau/12
            centre=c+tangent*math.cos(a)*.0045+axis_down*math.sin(a)*.0055
            radial=(tangent*math.cos(a)+axis_down*math.sin(a)).normalized()
            for j in range(4):
                b=j*math.tau/4
                verts.append(centre+radial*math.cos(b)*.00085+normal*math.sin(b)*.00085)
                uvs.append((k/12,j/4))
        for k in range(12):
            for j in range(4):
                faces.append((start+k*4+j,start+(k+1)%12*4+j,
                              start+(k+1)%12*4+(j+1)%4,start+k*4+(j+1)%4))
region(smooth_mesh('Interlinked iron aventail rings',verts,faces,4,uvs),'aventail')

# Conical helmet with a low rolled brow, subtle four-panel seams and peened rivets.
def skull(u,v):
    a=u*math.tau;r=.093*(1-v)**.64
    ridge=.0009*math.cos(a*4)**12*math.sin(v*math.pi)
    return Vector((math.sin(a)*(r+ridge),1.418+.141*v,-.049+math.cos(a)*(r*1.08+ridge)))
patch('Forged four panel conical helmet',skull,80,44,0,.0025,0)
band('Weathered iron helmet brow',lambda u,dy:(math.sin(u*math.tau)*.0945,1.420+dy,
          -.049+math.cos(u*math.tau)*.102),.019,0,'head')
for height in (1.411,1.429):ring_trim('Rolled steel helmet edge',height,.095,.1025,-.049,13,.0015)
for i in range(14):
    a=i*math.tau/14;rivet((math.sin(a)*.096,1.420,-.049+math.cos(a)*.103),13,.0026)

# A fitted, irregular beard follows the actual anatomical cheeks and jaw. The
# moustache and lip opening remain visible rather than a straight mask edge.
def full_beard(u,v):
    a=(u-.5)*2.85
    x=math.sin(a)*(.074-.031*v**1.5)
    top=1.338+.031*abs(math.sin(a))**2
    bottom=1.247+.032*abs(math.sin(a))**1.5
    y=top+(bottom-top)*v
    z=face_depth(x,max(1.285,y))+.005-.012*smoothstep((1.285-y)/.041)
    z+=.0015*math.sin(u*math.tau*37+v*8)*math.sin(v*math.pi)
    return Vector((x,y,z))
beard=patch('Fitted full warrior beard',full_beard,64,36,9,.0014,0)
import bmesh
bm=bmesh.new();bm.from_mesh(beard.data)
remove=[]
for face in bm.faces:
    p=U(face.calc_center_median())
    if (p.x/.021)**2+((p.y-1.319)/.006)**2<1:remove.append(face)
bmesh.ops.delete(bm,geom=remove,context='FACES');bm.to_mesh(beard.data);bm.free()
for i in range(48):
    u=.015+i/47*.970
    points=[full_beard(u,v)+Vector((0,0,.0015)) for v in (.24,.48,.74,.98)]
    sweep('Tapered irregular beard lock',points,lambda t:.0009*(1-t)+.00012,9,5,12)
for sign in (-1,1):
    pts=[]
    for k in range(9):
        x=sign*(.004+k/8*.034);y=1.333-.011*(k/8)**.8
        pts.append((x,y,face_depth(x,y)+.0035))
    sweep('Natural swept moustache',pts,lambda t:.0037-.0018*t,9,10,28,flatten=.65)

# Replace the scratched web-like heater with a cleanly forged, gently ridged
# shell, a broad fitted border and restrained localized strike damage.
for obj in groups['Shield']:bpy.data.objects.remove(obj,do_unlink=True)
groups['Shield']=[];current='Shield'
shield_scars=[(.30,.20,.35,.39),(.68,.46,.50,.64),(.46,.69,.49,.82)]
def shield_width(v):
    return sample_profile([(0,.231),(.10,.238),(.30,.222),(.55,.173),(.80,.094),(1,.006)],v)[0]
def heater(u,v):
    s=u*2-1;w=shield_width(v)
    x=.245-.635*v+.065*max(0,1-s*s)**.90*(1-v)**6
    y=s*w
    z=.026+.032*(1-s*s)+.006*math.exp(-(s/.13)**2)*math.sin(v*math.pi)
    for u0,v0,u1,v1 in shield_scars:
        du,dv=u1-u0,v1-v0;t=max(0,min(1,((u-u0)*du+(v-v0)*dv)/(du*du+dv*dv)))
        dist=math.hypot(u-u0-t*du,v-v0-t*dv)
        z-=.0028*math.exp(-(dist/.007)**2)*math.sin(t*math.pi)
    return Vector((x,y,z))
patch('Forged bowed heater steel shell',heater,64,80,12,.007,0)
# Broad curved reinforcement bands have a flat section and a rolled edge.
for edge in (0,1):
    patch('Fitted iron side reinforcement',lambda u,v,edge=edge:heater((.004+u*.048) if edge==0 else (.996-u*.048),v*.991)+Vector((0,0,.003)),
          4,64,13,.003,0)
patch('Fitted iron top reinforcement',lambda u,v:heater(u,.004+v*.041)+Vector((0,0,.003)),64,4,13,.003,0)
border=[heater(i/64,0) for i in range(65)]+[heater(1,i/80) for i in range(1,81)]+[heater(0,1-i/80) for i in range(1,81)]
border.append(border[0])
sweep('Rolled and rubbed steel perimeter',[p+Vector((0,0,.005)) for p in border],.0032,12,8,len(border)-1)
for u,v in [(u,.025) for u in (.10,.30,.50,.70,.90)]+[(u,v) for u in (.027,.973) for v in (.16,.34,.52,.70,.85)]:
    ellipsoid('Flush iron shield peen',heater(u,v)+Vector((0,0,.006)),(.005,.005,.0025),13,12,8)
# A restrained shallow raised central spine emphasizes the forged construction.
sweep('Forged central shield ridge',[heater(.5,v)+Vector((0,0,.001)) for v in np.linspace(.10,.88,48)],
      lambda t:.0027*math.sin(t*math.pi)**.5+.0004,12,6,48,flatten=.45)
# Rear strap anchors, padded enarmes and a hand grip sit behind the plate.
for x in (-.085,.085):
    for y in (-.060,.060):box('Peened rear mounting lug',(x,y,.013),(.036,.025,.008),13,.003)
    patch('Shaped leather forearm enarme',lambda u,v,x=x:(x+(u-.5)*.026,
          -.060+.120*v,.019-.136*math.sin(v*math.pi)**.65),8,32,3,.004,0)
    patch('Forged enarme reinforcement',lambda u,v,x=x:(x+(u-.5)*.009,
          -.060+.120*v,.015-.136*math.sin(v*math.pi)**.65),3,32,13,.002,0)
sweep('Leather bound palm grip',[(-.090,-.025,-.063),(0,-.025,-.075),(.090,-.025,-.063)],.011,3,12,32,flatten=.8)

# Recolor ebony and charcoal in the Blender source as well as the runtime preset.
for i,tint in ((0,(.42,.42,.46)),(3,(.50,.45,.42)),(6,(.23,.20,.18)),(9,(.55,.60,.65))):
    name,color,metal,rough=palette[i]
    palette[i]=(name,tuple(c*t for c,t in zip(color,tint)),metal,rough)
    linear=lambda c:c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
    m=materials[i]
    for n in m.node_tree.nodes:
        if n.type=='MIX_RGB' and n.blend_type=='MULTIPLY':
            n.inputs[1].default_value=(*(linear(c) for c in palette[i][1]),1)
            break


def skin_weights4(obj,pos):
    """Normalized, sorted four-influence weights in the shared bind skeleton."""
    area=obj.get('region','');side=obj.get('side','R')
    weights={}
    def add(name,w):
        if w>1e-6:
            i=bone_names.index('Bip001 '+name);weights[i]=weights.get(i,0)+w
    if area in ('arm','forearm'):
        a,b=joint(side+' UpperArm'),joint(side+' Forearm')
        t=(pos-a).dot((b-a).normalized())/(b-a).length
        if area=='forearm':add(side+' Forearm',1)
        else:
            lower=smoothstep((t-.76)/.42)
            shoulder=1-smoothstep((t+.18)/.40)
            add(side+' Forearm',lower)
            add(side+' UpperArm',(1-lower)*(1-shoulder*.72))
            add(side+' Clavicle',(1-lower)*shoulder*.43)
            add('Spine',(1-lower)*shoulder*.23)
            add('Neck',(1-lower)*shoulder*.06)
    elif area in ('torso','waist'):
        pelvis=1-smoothstep((pos.y-.79)/.16)
        add('Pelvis',pelvis);add('Spine',1-pelvis)
    elif area=='skirt':
        thigh=smoothstep((.79-pos.y)/.30)*.72
        side_blend=smoothstep(abs(pos.x)/.065)
        add('Pelvis',1-thigh)
        add(side+' Thigh',thigh*(.5+.5*side_blend))
        add(('L' if side=='R' else 'R')+' Thigh',thigh*(.5-.5*side_blend))
    elif area=='leg':
        a,b=joint(side+' Thigh'),joint(side+' Calf')
        t=(pos-a).dot((b-a).normalized())/(b-a).length
        lower=smoothstep((t-.82)/.34)
        hip=(1-smoothstep((t+.08)/.29))*.35
        add(side+' Calf',lower)
        add(side+' Thigh',(1-lower)*(1-hip));add('Pelvis',(1-lower)*hip)
    elif area=='boot':
        foot=(1-smoothstep((pos.y-.085)/.10))*.98
        add(side+' Foot',foot);add(side+' Calf',1-foot)
    elif area=='foot':add(side+' Foot',1)
    elif area=='aventail':
        head=smoothstep((pos.y-1.190)/.155)
        neck=(1-head)*smoothstep((pos.y-1.185)/.14)*.35
        add('Head',head);add('Neck',neck);add('Spine',1-head-neck)
    elif area=='head':add('Head',1)
    else:weights[obj['bone']]=1
    ordered=sorted(weights.items(),key=lambda item:item[1],reverse=True)[:4]
    total=sum(w for _,w in ordered)
    return [(i,round(w/total,6)) for i,w in ordered]


def garment_motion(obj,pos):
    if obj.get('motion')!='hem':return ()
    weight=smoothstep((.80-pos.y)/.30)
    side=obj.get('side','R')
    return (('HemForward'+side,Vector((0,0,.021*weight))),
            ('HemSide'+side,Vector((.018*weight,0,0))))

print('MARCHWARDEN cinematic outfit:',len(groups['Body']),'source pieces; real mail links:',len(verts)//48,flush=True)
