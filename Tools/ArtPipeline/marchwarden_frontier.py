"""Reference-led frontier warrior, based on the user's s.jpg silhouette.

Conical nasal helmet, visible sculpted face, mail hood, fitted lamellar coat,
split woven tunic, leather boots and a round shield. All geometry is authored here;
the reference supplies design cues, not vertices. Coordinates remain rig-compatible.
"""
library=Path(__file__).with_name('marchwarden_forms.py').read_text()
exec(compile(library[:library.index("current='Body'; bone='Spine'")],'surface_library','exec'),globals())

def binding(name,points,mat=10,r=.002):return sweep(name,points,r,mat,6,max(4,len(points)-1))
def torus_front(name,c,radius,wire,mat=1,segments=64):
    return sweep(name,[Vector(c)+Vector((math.cos(i*math.tau/segments)*radius,math.sin(i*math.tau/segments)*radius,0)) for i in range(segments+1)],wire,mat,8,segments*2)
def seam(name,fn,n=48,mat=10):return binding(name,[fn(i/n) for i in range(n+1)],mat)

current='Body';bone='Spine'
# The undercoat carries one continuous torso silhouette with a natural waist.
body_loft('Tailored woven undercoat',[(.73,.153,.112,-.045),(.84,.153,.115,-.045),
    (.96,.177,.133,-.047),(1.075,.200,.139,-.05),(1.145,.181,.108,-.05),
    (1.185,.107,.074,-.046)],2,48,26)
body_loft('Supple leather cuirass backing',[(.848,.157,.120,-.044),(.94,.176,.139,-.048),
    (1.065,.201,.147,-.05),(1.14,.186,.117,-.048),(1.167,.12,.082,-.048)],3,48,24)

def coat_radius(y):
    v=max(0,min(1,(y-.85)/.32))
    return .158+.041*math.sin(v*math.pi*.87),.121+.028*math.sin(v*math.pi),-.047
def coat_point(a,y,lift=0):
    w,d,z=coat_radius(y)
    return Vector((math.sin(a)*(w+lift),y,z+math.cos(a)*(d+lift)))

# Thin curved lamellae overlap the backing; lacing and shadow gaps communicate
# actual plate construction without turning the torso into a stack of bricks.
for back in (False,True):
    for row in range(6):
        top=.895+row*.044
        for column in range(8):
            a0=-math.pi/2+column*math.pi/8+(math.pi if back else 0)
            def lamella(u,v,a0=a0,top=top):
                a=a0+(.025+.95*u)*math.pi/8
                y=top-.050*v+.0015*math.sin(u*math.pi)
                return coat_point(a,y,.004+.002*math.sin(u*math.pi))
            patch('Laced curved steel lamella',lamella,8,6,0,.0016,0,(.4,.6))
            seam('Rolled lamella lower edge',lambda t:lamella(t,1),12,0)
            for u in (.16,.84):
                p=lamella(u,.16);p.z+=(-.001 if back else .001)
                rivet(p,1,.0026)
                binding('Leather lamellar tie',[lamella(u,.08),lamella(u+.02,.20),lamella(u,.32)],3,.0011)
for v in (0,1):
    y=.854+v*.29
    seam('Cuirass leather bound edge',lambda t,y=y:coat_point(t*math.tau,y,.006),96,3)

# Leather shoulder bridges connect the cuirass to the mail sleeves.
for side in (-1,1):
    patch('Soft cuirass shoulder bridge',lambda u,v,side=side:(side*(.115+u*.073),
        1.153+.022*math.sin(v*math.pi),-.13+v*.22),12,18,3,.004,1)
    binding('Shoulder bridge stitched edge',[(side*.188,1.155,-.13),(side*.19,1.176,-.04),(side*.188,1.155,.09)],5,.0011)

# A shallow sunburst plate echoes the chest medallion in s.jpg.
medal=Vector((0,1.035,.113))
ellipsoid('Sunburst chest medallion',medal,(.039,.039,.0045),1,48,20)
torus_front('Medallion chased border',medal+Vector((0,0,.005)),.034,.0018,0)
for k in range(12):
    a=k*math.tau/12
    binding('Chased medallion ray',[medal+Vector((math.cos(a)*r,math.sin(a)*r,.008)) for r in (.009,.028)],0,.0010)
ellipsoid('Medallion centre',medal+Vector((0,0,.009)),(.009,.009,.003),0,24,12)

bone='Pelvis'
body_loft('Waist belt',[(.816,.164,.130,-.043),(.838,.164,.130,-.043),(.848,.160,.128,-.043)],3,48,8)
for x in (-.023,.023):binding('Belt buckle side',[(x,.815,.093),(x,.844,.093)],1,.003)
for y in (.815,.844):binding('Belt buckle bar',[(-.023,y,.093),(.023,y,.093)],1,.003)
binding('Buckle tongue',[(-.018,.83,.098),(.015,.83,.098)],0,.0018)
# Eight gores make a real wraparound coat; the front opening and seam allowances
# stay soft and deform with the thigh, rather than cutting through trousers.
for panel in range(8):
    a0=-math.pi+panel*math.tau/8
    def gore(u,v,a0=a0):
        a=a0+(.012+.976*u)*math.tau/8
        w=.163+.036*v;d=.131+.034*v
        fold=.009*math.sin(u*math.pi*3+.3*v)*v
        return (math.sin(a)*(w+fold),.814-.305*v+.007*math.sin(u*math.pi)*v,
            -.043+math.cos(a)*(d+fold))
    o=patch('Woven tunic gore',gore,16,22,2,.004,1,(.6,1.4))
    side='L' if math.sin(a0+math.pi/8)<0 else 'R';o['clothSide']=side
    for u in (0,1):
        edge=seam('Tunic tailored seam',lambda t,u=u:gore(u,t),32,10);edge['clothSide']=side
    hem=seam('Oxblood woven hem',lambda t:gore(t,1),32,10);hem['clothSide']=side
    # Embroidered stitches are small enough to read as thread, not gold piping.
    for k in range(9):
        u=(k+.5)/9;p=Vector(gore(u,.982))
        o=binding('Hem running stitch',[p,p+Vector((.002,0,.001))],5,.0006);o['clothSide']=side
ellipsoid('Belt utility pouch',(-.189,.755,-.048),(.037,.048,.030),3,32,18)
patch('Pouch flap',lambda u,v:(-.189+(u-.5)*.067,.788-v*.043,-.016+.004*math.sin(u*math.pi)),12,12,3,.003,1)
rivet((-.189,.757,-.01),1,.0035)
for side in (-1,1):binding('Scabbard suspension strap',[(side*.17,.831,.014),(side*.211,.704,.015)],3,.010)

for side,label in ((-1,'L'),(1,'R')):
    upper,elbow,hand=joint(label+' UpperArm'),joint(label+' Forearm'),joint(label+' Hand')
    bone=label+' UpperArm'
    # Short, properly closed mail sleeves reveal the upper arm's anatomy.
    # Deltoid, biceps and triceps share one continuous surface, not stacked balls.
    direction=(elbow-upper).normalized()
    front=Vector((0,0,1));front=(front-direction*front.dot(direction)).normalized()
    lateral=direction.cross(front).normalized()
    def athletic_arm(u,v):
        t=-.10+v*2.10;a=u*math.tau
        centre=upper.lerp(elbow,t) if t<=1 else elbow.lerp(hand,t-1)
        radius=.039+.014*math.exp(-((t-.22)/.27)**2)+.009*math.exp(-((t-.57)/.25)**2)
        radius-=.005*math.exp(-((t-.98)/.13)**2)
        if t>1:radius=catmull([(1,.039),(1.25,.050),(1.55,.043),(1.80,.035),(2,.030)],t-1)[1]
        biceps=.007*math.exp(-((t-.56)/.22)**2)*max(0,math.cos(a))**3
        triceps=.005*math.exp(-((t-.48)/.32)**2)*max(0,-math.cos(a))**2
        return centre+lateral*math.sin(a)*radius+front*math.cos(a)*(radius+biceps+triceps)
    o=patch('Continuous athletic arm',athletic_arm,64,80,8,.0015,0,(1.2,2.2));o['elbowSide']=label
    o=limb_shell('Short draped mail shoulder',upper,elbow,[(-.22,.003,.003),(-.08,.059,.063),(.10,.068,.072),(.30,.065,.070),(.39,.061,.065)],4,math.tau,48,26);o['shoulderSide']=label
    o=limb_shell('Leather mail sleeve binding',upper,elbow,[(.36,.063,.068),(.40,.061,.065)],3,math.tau,48,5);o['shoulderSide']=label
    bone=label+' Forearm'
    limb_shell('Fitted leather bracer',elbow,hand,[(.13,.054,.061),(.40,.049,.056),(.86,.036,.043)],3,math.pi*1.65,36,16)
    # Narrow reinforcing strips and practical lacing, restrained under the mail.
    for phase in (-.62,0,.62):
        limb_shell('Bracer steel splint',elbow,hand,[(.18,.055,.063),(.80,.038,.046)],0,.09,6,10,phase)
    for t in (.24,.75):
        c=elbow.lerp(hand,t)
        limb_shell('Bracer securing tie',c-(hand-elbow)*.018,c+(hand-elbow)*.018,
            [(0,.054 if t<.5 else .040,.060 if t<.5 else .048),(1,.054 if t<.5 else .040,.060 if t<.5 else .048)],3,math.tau,28,3)
    bone=label+' Hand'
    palm=hand+Vector((side*.012,-.020,.001))
    ellipsoid('Anatomical palm',palm,(.032,.042,.022),8,40,24)
    for k in range(4):
        x=hand.x+side*(-.017+k*.013)
        sweep('Curled gripping finger',[(x,hand.y-.029,hand.z+.018),(x+side*.002,hand.y-.046,hand.z+.026),
            (x+side*.002,hand.y-.052,hand.z+.008)],lambda t:.0065-.0015*t,8,12,12)
    sweep('Opposed gripping thumb',[palm+Vector((-side*.023,.012,.004)),palm+Vector((-side*.036,-.006,.022)),
        palm+Vector((-side*.015,-.018,.027))],lambda t:.009-.002*t,8,14,18)
    hip,knee,ankle=joint(label+' Thigh'),joint(label+' Calf'),joint(label+' Foot')
    bone=label+' Thigh'
    limb_shell('Tailored wool trousers',hip,knee,[(0,.072,.079),(.28,.076,.085),(.63,.060,.069),(.92,.046,.053),(1.05,.043,.048)],2,math.tau,36,24)
    bone=label+' Calf'
    o=limb_shell('Flexible cloth knee gusset',hip.lerp(knee,.83),knee.lerp(ankle,.24),
        [(0,.052,.061),(.45,.048,.056),(1,.044,.051)],2,math.tau,36,20)
    o['kneeSide']=label
    o=limb_shell('Supple high leather boot',knee,ankle,[(.05,.047,.054),(.22,.053,.063),(.55,.045,.055),(.85,.033,.041),(1.06,.034,.040)],3,math.tau,40,24)
    o['ankleSide']=label
    for t in (.12,.25):
        c=knee.lerp(ankle,t)
        limb_shell('Boot folded top and strap',c-Vector((0,.009,0)),c+Vector((0,.009,0)),[(0,.053,.062),(1,.053,.062)],3,math.tau,32,3)
    bone=label+' Foot'
    o=body_loft('Joined boot ankle and instep',[(ankle.y-.085,.030,.036,ankle.z+.015),
        (ankle.y-.043,.035,.049,ankle.z+.017),(ankle.y+.025,.033,.038,ankle.z+.004)],3,36,16)
    o.location.x=ankle.x
    foot=ankle+Vector((0,-.075,.037))
    def last(u,v):
        a=u*math.tau;z=-.103+v*.252
        taper=max(.001,min(1,math.sin(v*math.pi)**.24*1.12))
        w=(.047+.004*math.sin(v*math.pi)-.010*v*v)*taper
        h=(.026+.021*math.exp(-((v-.25)/.25)**2))*taper
        return foot+Vector((math.cos(a)*w,-.011+math.sin(a)*(h if math.sin(a)>0 else .021),z))
    patch('Continuous leather boot foot',last,40,28,3,.003,0)
    seam('Stitched boot welt',lambda t:foot+Vector((math.sin(t*math.tau)*(.043+.004*math.cos(t*math.tau)),-.032,.017+math.cos(t*math.tau)*.125)),64,7)
    patch('Solid boot sole',lambda u,v:foot+Vector((math.sin(u*math.tau)*(.043+.004*math.cos(u*math.tau)),
        -.032+v*.007,.017+math.cos(u*math.tau)*.125)),48,3,7,.006,0)

bone='Head'
# A continuous head surface includes the jaw, cheeks, orbital sockets, brows,
# nose bridge, lips and chin. Separate eyes sit inside its sockets, under lids.
profile=[(1.258,.019,.030,-.040),(1.278,.042,.049,-.034),(1.310,.061,.064,-.040),
    (1.345,.073,.074,-.043),(1.384,.075,.077,-.046),(1.424,.070,.077,-.048),
    (1.457,.051,.060,-.051),(1.475,.008,.012,-.052)]
def face_surface(u,v):
    y,w,d,z=catmull(profile,v);a=(u-.5)*math.tau
    x=math.sin(a)*w;front=max(0,math.cos(a))**7
    nose=.023*math.exp(-(x/.012)**2-((y-1.359)/.027)**2)
    nose+=.014*math.exp(-(x/.016)**2-((y-1.344)/.010)**2)
    brow=sum(.009*math.exp(-((x-sign*.029)/.021)**2-((y-1.393)/.008)**2) for sign in (-1,1))
    sockets=sum(-.009*math.exp(-((x-sign*.029)/.017)**2-((y-1.378)/.009)**2) for sign in (-1,1))
    cheek=sum(.007*math.exp(-((x-sign*.045)/.021)**2-((y-1.350)/.019)**2) for sign in (-1,1))
    mouth=.0045*math.exp(-(x/.024)**4-((y-1.317)/.006)**2)-.002*math.exp(-(x/.022)**4-((y-1.319)/.0018)**2)
    return (x,y,z+math.cos(a)*d+front*(nose+brow+sockets+cheek+mouth))
patch('Sculpted continuous human head',face_surface,96,72,8,.001,0,(1,1))
for sign in (-1,1):
    x=sign*.029
    ellipsoid('Recessed eyeball',(x,1.378,.022),(.0105,.0046,.0068),11,32,18)
    ellipsoid('Muted iris',(x,1.378,.0295),(.004,.004,.0017),9,24,12)
    ellipsoid('Eye pupil',(x,1.378,.031),(.0018,.0027,.0008),7,20,10)
    for top in (False,True):
        sweep('Sculpted eyelid',[(x-.010,1.378,.027),(x,1.378+(.0034 if top else -.0025),.030),
            (x+.010,1.378,.027)],.0018,8,10,24)
    sweep('Natural brow',[(x-sign*.013,1.392,.030),(x,1.394,.031),(x+sign*.014,1.390,.028)],.0028,9,10,20)
    ellipsoid('Sculpted ear',(sign*.074,1.354,-.036),(.010,.023,.009),8,28,18)
    ellipsoid('Ear concha',(sign*.080,1.353,-.030),(.003,.011,.003),9,18,10)
    ellipsoid('Nostril',(sign*.006,1.340,.054),(.003,.0017,.0017),9,16,10)
# A fitted beard follows the jaw and cheeks, with restrained strand grooves.
def beard(u,v):
    a=(u-.5)*2.92
    y=1.347-.099*v+.010*abs(math.sin(a))
    w=.071*(1-.57*v*v)
    z=-.041+math.cos(a)*(.078-.019*v)+.002*math.sin(u*math.pi*45)*math.sin(v*math.pi)
    return (math.sin(a)*w,y,z)
o=patch('Sculpted tapered beard',beard,72,36,9,.003,0,(1,1))
import bmesh
bm=bmesh.new();bm.from_mesh(o.data)
mouth_faces=[]
for f in bm.faces:
    p=U(f.calc_center_median())
    if (p.x/.017)**2+((p.y-1.318)/.0055)**2<1:mouth_faces.append(f)
bmesh.ops.delete(bm,geom=mouth_faces,context='FACES');bm.to_mesh(o.data);bm.free()
ellipsoid('Natural lower lip',(0,1.315,.041),(.014,.0022,.002),8,28,12)
binding('Recessed mouth line',[(-.014,1.319,.042),(0,1.318,.044),(.014,1.319,.042)],9,.0010)
for k in range(42):
    u=.04+.92*k/41
    points=[Vector(beard(u,v))+Vector((0,0,.001)) for v in (.52,.72,.90,.99)]
    sweep('Beard tapered edge wisp',points,lambda t:.0007*(1-t)+.00012,9,5,10)
for sign in (-1,1):
    sweep('Swept moustache',[(sign*.004,1.333,.050),(sign*.020,1.327,.047),(sign*.035,1.322,.034)],
        lambda t:.0055-.0025*t,9,14,28,flatten=.60)

# Mail is open around the face, with a shoulder drape that folds rather than
# forming a rigid cone. The helmet band seats on the forehead above the eyes.
def coif(u,v):
    close=max(0,min(1,(v-.43)/.25));close=close*close*(3-2*close)
    opening=.82*(1-close)
    a=opening+u*(math.tau-2*opening)
    w=.083-.007*math.sin(v*math.pi)+.048*v**5
    d=.090-.010*math.sin(v*math.pi)+.027*v**5
    fold=.003*math.sin(a*13+v*.5)*math.sin(v*math.pi)
    return (math.sin(a)*(w+fold),1.420-.208*v+.009*math.sin(a*3)*v*v,-.047+math.cos(a)*(d+fold))
o=patch('Draped open-face mail hood',coif,72,40,4,.003,0,(2,1.5));o['coif']=True
for u in (0,1):
    o=seam('Mail face opening leather binding',lambda t,u=u:coif(u,t*.68),36,3);o['coif']=True
o=seam('Mail hood hem',lambda t:coif(t,1),96,3);o['coif']=True
# Forged conical skull, a subtle medial seam and a small finial.
def helmet(u,v):
    a=u*math.tau
    r=.092*(1-v)**.58
    return (math.sin(a)*r,1.418+.142*v,-.048+math.cos(a)*r*1.08)
patch('Forged conical helmet',helmet,72,40,0,.0025,0,(2,1))
body_loft('Conical helmet brow band',[(1.410,.094,.101,-.048),(1.429,.093,.100,-.048)],0,72,5)
for y in (1.411,1.428):ring_trim('Helmet band chased edge',y,.0945,.1015,-.048,1,.0018)
for a in (0,math.pi/2,math.pi,math.pi*1.5):
    seam('Forged helmet panel seam',lambda t,a=a:helmet((a%math.tau)/math.tau,t),40,0)
for k in range(12):
    a=k*math.tau/12;rivet((math.sin(a)*.095,1.419,-.048+math.cos(a)*.102),1,.0029)
patch('Tapered nasal guard',lambda u,v:((u-.5)*(.014-.005*v),1.425-.092*v,
    .054+.011*math.sin(v*math.pi)),8,24,0,.003,0)
seam('Nasal guard central ridge',lambda t:(0,1.425-.092*t,.056+.011*math.sin(t*math.pi)),28,1)
sweep('Helmet small forged finial',[(0,1.550,-.048),(0,1.571,-.048),(0,1.584,-.048)],lambda t:.008*(1-t)+.0005,0,20,20)

# Preserve the bow/quiver/arrow contracts, then replace the sword and shield.
exec(compile(library[library.index("current='Sword'"):],'equipment_library','exec'),globals())
for name in ('Sword','Shield'):
    for obj in groups[name]:bpy.data.objects.remove(obj,do_unlink=True)
    groups[name]=[]
for obj in groups['Quiver']:obj.scale.x*=.76;obj.scale.y*=.76
current='Sword'
profile=[(-.245,.027),(-.21,.028),(.10,.024),(.28,.018),(.37,.010),(.43,.0003)]
verts=[];faces=[]
for x,w in profile:
    for y,z in [(-w,0),(-w*.60,.0045),(0,.006),(w*.60,.0045),(w,0),(w*.60,-.0045),(0,-.006),(-w*.60,-.0045)]:verts.append((x,y,z))
for j in range(len(profile)-1):
    for i in range(8):faces.append((j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i))
faces.extend([tuple(reversed(range(8))),tuple(range((len(profile)-1)*8,len(profile)*8))])
mesh('Forged straight tapered blade',verts,faces,0,.0006)
for side in (-1,1):
    binding('Shallow blade fuller',[(-.204,0,side*.0062),(.07,0,side*.0062),(.29,0,side*.0048)],4,.0015)
sweep('Forged straight crossguard',[(-.258,-.12,0),(-.269,-.06,0),(-.272,0,0),(-.269,.06,0),(-.258,.12,0)],.009,0,18,40)
sweep('Bound leather sword grip',[(-.416,0,0),(-.350,0,0),(-.288,0,0)],.0175,3,20,20)
binding('Sword grip spiral seam',[(-.406+i/200*.110,math.cos(i/200*math.tau*10)*.018,math.sin(i/200*math.tau*10)*.018) for i in range(201)],7,.0009)
ellipsoid('Disc sword pommel',(-.441,0,0),(.025,.029,.016),0,40,24)
ellipsoid('Pommel chased brass centre',(-.441,0,.016),(.009,.011,.002),1,24,12)

current='Shield'
radius=.322
def round_surface(u,v,z=0):
    a=u*math.tau;r=radius*v
    return (math.cos(a)*r,math.sin(a)*r,.014+.014*(1-v*v)+z)
patch('Round shield wooden core',round_surface,96,20,6,.015,0,(2,2))
patch('Painted woven shield facing',lambda u,v:round_surface(u,v,.003),96,20,2,.002,0,(2,2))
seam('Round shield leather rolled rim',lambda t:round_surface(t,1,.001),128,3)
for r,mat in ((.310,10),(.296,1)):
    seam('Painted shield border',lambda t,r=r:(math.cos(t*math.tau)*r,math.sin(t*math.tau)*r,.019),128,mat)
def boss(u,v):
    a=u*math.tau;r=.068*v
    return (math.cos(a)*r,math.sin(a)*r,.030+.070*(1-v*v)**.65)
patch('Forged domed round shield boss',boss,64,24,0,.002,0)
seam('Boss chased mounting flange',lambda t:(math.cos(t*math.tau)*.077,math.sin(t*math.tau)*.077,.032),96,1)
for k in range(16):
    a=k*math.tau/16;ellipsoid('Shield rim nail',(math.cos(a)*.310,math.sin(a)*.310,.024),(.0035,.0035,.0025),1,14,8)
for k in range(6):
    a=k*math.tau/6;ellipsoid('Shield boss flange rivet',(math.cos(a)*.077,math.sin(a)*.077,.036),(.0035,.0035,.0025),0,14,8)
for k in range(8):
    a=k*math.tau/8
    binding('Painted shield sunray',[(math.cos(a+.03)*r,math.sin(a+.03)*r,.034-.012*(r/radius)**2) for r in (.105,.145,.225)],5,.0017)
# A round shield retains enarmes for gameplay: both hands remain behind the boss.
for side in (-1,1):
    sweep('Round shield forearm enarme',[(-.062,side*.095,.006),(-.036,side*.095,-.085),
        (.036,side*.095,-.085),(.062,side*.095,.006)],.010,3,12,36,flatten=.45)
binding('Shield back palm grip',[(-.025,-.085,-.060),(-.025,.085,-.060)],3,.014)
