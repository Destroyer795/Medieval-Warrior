"""Field harness study: original geometry informed by museum armor construction.

References are documented in ArtSource/Marchwarden/References.md. Thin fitted
plates, articulated lames, practical straps, mail voiders and a keeled sallet.
No museum mesh or third-party game character is used.
"""
library=(Path(__file__).with_name('marchwarden_forms.py')).read_text()
exec(compile(library[:library.index("current='Body'; bone='Spine'")],'marchwarden_surface_library','exec'),globals())

def plate(name,profile,mat=0,arc=math.pi*1.23,thickness=.0025,flute=0):
    def point(u,v):
        y,w,d,z=catmull(profile,v); a=(u-.5)*arc
        x=math.sin(a)*w
        central=.007*math.exp(-(x/.022)**2)*math.sin(v*math.pi)
        rib=flute*math.cos(a*8)*math.sin(v*math.pi)**2
        return (x,y,z+math.cos(a)*d+central+rib)
    o=patch(name,point,48,24,mat,thickness,0,(1.5,1.5))
    # The narrow rolled edge and riveted corners make sheet thickness readable.
    for v in (0,1): sweep(name+' rolled edge',[point(i/48,v) for i in range(49)],.0018,0,6,96)
    for u in (.035,.965):
        for v in (.08,.85): rivet(Vector(point(u,v))+Vector((0,0,.002)),1,.0032)
    return o

def strap(name,a,b,width=.025,mat=3):
    a,b=Vector(a),Vector(b); d=(b-a).normalized(); cross=Vector((d.y,-d.x,0)).normalized()
    return patch(name,lambda u,v:a.lerp(b,v)+cross*((u-.5)*width)+Vector((0,0,.002*math.sin(v*math.pi))),4,12,mat,.003,1,(.2,1))

def oval_cup(name,c,rx,ry,depth,wing=0,side=1):
    def point(u,v):
        a=u*math.tau; x=math.cos(a)*rx*v
        x+=side*wing*(max(0,math.cos(a)*side)**4)*v**3
        return Vector(c)+Vector((x,math.sin(a)*ry*v,depth*(1-v*v)))
    o=patch(name,point,48,14,0,.0025,0)
    sweep(name+' turned edge',[point(i/64,1) for i in range(65)],.0017,0,6,128)
    return o

current='Body'; bone='Spine'
# The foundation fits the body. Mail is visible only at joints and neck, rather
# than becoming shiny black balls between disconnected armor primitives.
body_loft('Fitted linen arming doublet',[(.72,.151,.105,-.04),(.84,.153,.11,-.045),
    (.97,.173,.119,-.047),(1.095,.197,.12,-.05),(1.16,.17,.095,-.05)],2,40,22)
plate('Convex breastplate',[(.865,.154,.122,-.044),(.925,.17,.14,-.045),
    (1.035,.207,.154,-.048),(1.105,.205,.146,-.049),(1.153,.16,.111,-.05),(1.173,.117,.081,-.05)],flute=.0017)
# Backplate is a separate shaped sheet with armhole cut-ins.
o=plate('Fitted backplate',[(.857,.157,.112,.041),(.95,.179,.125,.046),
    (1.072,.205,.13,.048),(1.148,.161,.10,.05),(1.171,.115,.078,.05)])
# Reflect this assembly to the back; all its edge fittings move with it.
back_start=groups['Body'].index(o)
for obj in groups['Body'][back_start:]:
    for v in obj.data.vertices: v.co.y=-v.co.y
    import bmesh
    bm=bmesh.new(); bm.from_mesh(obj.data); bmesh.ops.reverse_faces(bm,faces=list(bm.faces)); bm.to_mesh(obj.data); bm.free()
# Low plackart overlaps upward over the breastplate and ends at the natural waist.
plate('Overlapping plackart',[(.842,.157,.123,-.039),(.876,.158,.133,-.039),
    (.929,.174,.148,-.041),(.964,.181,.151,-.042)],arc=math.pi*1.12,flute=.0015)
for side in (-1,1):
    strap('Breastplate fastening',(side*.17,.912,.042),(side*.173,.952,.045),.024)
    rivet((side*.167,.938,.055),1,.0038)

# A mail standard occupies the throat. A low separate bevor protects the jaw
# without the oversized gold doughnut collar from the previous design.
def aventail(u,v):
    a=u*math.tau
    width=.092+.067*v*v
    depth=.086+.032*v*v
    fold=.004*math.sin(a*11+.4*v)*math.sin(v*math.pi)
    return (math.cos(a)*(width+fold),1.262-.086*v+.008*math.sin(a*3)*v*v,
        -.044+math.sin(a)*(depth+fold))
patch('Soft draped mail aventail',aventail,64,22,4,.003,0,(2,1))
sweep('Aventail sewn lower binding',[aventail(i/96,1) for i in range(97)],.0023,3,8,144)

# A fitted surcoat has shoulder cut-ins, cloth tension at the waist and a split
# skirt. The breastplate supports it; the fabric does not float in front of it.
def surcoat(u,v,back=False):
    y=.856+v*.31
    w=.142+.015*math.sin(v*math.pi)-.033*v**5
    x=(u-.5)*2*w
    front=math.sqrt(max(0,1-(x/(.18+.025*math.sin(v*math.pi)))**2))
    z=-.044+front*(.137+.029*math.sin(v*math.pi))
    z+=.007*math.sin(u*math.tau*4+.8*v)*(1-v*.55)+.003*math.sin(v*math.pi*3)
    z+=.004*math.sin(u*10+v*12)*(1-v)*math.sin(u*math.pi)
    if back: z=-.09-(z+.044)*.72
    return (x,y,z+.004)
for back in (False,True):
    patch('Tailored heraldic surcoat'+(' back' if back else ''),lambda u,v,back=back:surcoat(u,v,back),40,28,2,.003,0)
    for u in (0,1):
        sweep('Surcoat bound arm opening',[surcoat(u,i/32,back) for i in range(33)],.0022,5,6,64)
# A small stitched field device, shaped to the cloth rather than a floating badge.
for side in (-1,1):
    def device(u,v,side=side):
        x=side*(.009+.047*v)+(u-.5)*.010
        y=1.015+.064*v
        vv=(y-.856)/.31; w=.142+.015*math.sin(vv*math.pi)-.033*vv**5
        p=Vector(surcoat(.5+x/(2*w),vv)); p.z+=.0015
        return p
    patch('Embroidered split-watch device',device,4,16,5,.0004,0)

bone='Pelvis'
body_loft('Mail skirt under fauld',[(.53,.177,.125,-.045),(.65,.177,.13,-.045),
    (.75,.17,.129,-.045),(.825,.153,.112,-.045)],4,48,20)
for j in range(4):
    top=.847-j*.033
    # Downward overlaps, thin rolled lower edges, rivets at the articulation.
    plate('Fauld lame '+str(j),[(top-.041,.171+j*.008,.127+j*.006,-.039),
        (top-.02,.167+j*.008,.131+j*.006,-.039),(top,.16+j*.007,.123+j*.005,-.04)],arc=math.pi*1.38)
body_loft('Worn waist belt',[(.821,.168,.136,-.037),(.843,.17,.137,-.037),(.851,.167,.134,-.037)],3,48,8)
for sign in (-1,1):
    sweep('Small iron buckle',[(sign*.021,.826,.106),(sign*.024,.838,.108),(sign*.021,.85,.106)],.0028,1,8,12)
for y in (.826,.85): sweep('Buckle crossbar',[(-.021,y,.106),(.021,y,.106)],.0028,1,8,5)
sweep('Buckle tongue',[(-.018,.838,.111),(.012,.838,.111)],.0017,0,6,5)
# Tassets hang from leather suspension points and cover the upper thighs.
for side in (-1,1):
    cx=side*.115
    for j in range(3):
        top=.733-j*.037
        def tasset(u,v,j=j,top=top,cx=cx,side=side):
            x=cx+(u-.5)*(.13+j*.01)
            y=top-.049*v+.009*math.cos((u-.5)*math.pi)
            z=.10+.023*v+j*.006+.017*math.sin(u*math.pi)
            return (x,y,z)
        patch('Articulated hanging tasset',tasset,24,10,0,.0025,0)
        sweep('Tasset lower rolled lip',[tasset(i/24,1) for i in range(25)],.0017,0,6,48)
        for u in (.12,.88): rivet(Vector(tasset(u,.15))+Vector((0,0,.003)),1,.0033)
    for offset in (-.035,.035): strap('Tasset suspension',(cx+offset,.757,.109),(cx+offset,.71,.133),.018)
# Cloth is an underlayer, not rectangular signboards hanging over the armor.
cloth_panel('Left flowing skirt',-.15,-.015,.674,.49,.094)
cloth_panel('Right flowing skirt',.015,.15,.674,.495,.094)
cloth_panel('Left rear cloth tail',-.15,-.012,.77,.52,.155,back=True)
cloth_panel('Right rear cloth tail',.012,.15,.77,.52,.155,back=True)
# One small soft pouch and a sheath hanger, with useful purpose and scale.
ellipsoid('Sewn utility pouch',(-.188,.766,-.025),(.041,.06,.035),3,32,18)
strap('Pouch flap',(-.188,.80,.011),(-.188,.76,.016),.069)
rivet((-.188,.767,.021),1,.004)
strap('Sword hanger',(-.175,.831,.053),(-.214,.705,.045),.026)

for side,label in ((-1,'L'),(1,'R')):
    upper,elbow,hand=joint(label+' UpperArm'),joint(label+' Forearm'),joint(label+' Hand')
    bone=label+' UpperArm'
    limb_shell('Mail armpit and upper arm',upper,elbow,[(0,.065,.074),(.3,.07,.077),(.7,.057,.064),(1,.052,.056)],4,math.tau,28,10)
    def spaulder_cap(u,v):
        phi=u*math.tau; theta=v*1.81
        return upper+Vector((.099*math.sin(theta)*math.cos(phi),
            .078*math.cos(theta)+.012,.112*math.sin(theta)*math.sin(phi)))
    patch('Forged spaulder crown',spaulder_cap,40,20,0,.0025,0)
    sweep('Spaulder crown turned lip',[spaulder_cap(i/64,1) for i in range(65)],.0016,0,6,128)
    # Three broad overlapping lames give the shoulder a continuous outline.
    for j in range(3):
        start=-.08+j*.155; end=start+.24
        width=.087-j*.007; depth=.098-j*.007
        limb_shell('Riveted spaulder lame '+str(j),upper,elbow,
            [(start,width*.96,depth*.95),((start+end)*.5,width,depth),(end,width*.95,depth*.96)],0,math.pi*1.68,32,7)
        point=upper.lerp(elbow,(start+end)*.5)+Vector((0,0,depth+.002))
        rivet(point,1,.0035)
    limb_shell('Upper arm rerebrace',upper,elbow,[(.54,.062,.069),(.7,.059,.067),(.88,.053,.06)],0,math.pi*1.72,32,12)
    bone=label+' Forearm'
    limb_shell('Elbow mail voider',upper.lerp(elbow,.9),elbow.lerp(hand,.15),[(0,.055,.056),(1,.052,.055)],4,math.tau,28,8)
    oval_cup('Elbow couter with lateral wing',elbow+Vector((0,-.005,.043)),.057,.066,.034,.043,side)
    limb_shell('Contoured forearm plate',elbow,hand,[(.15,.055,.063),(.32,.058,.067),(.65,.048,.06),(.92,.038,.045)],0,math.pi*1.78,36,18)
    limb_shell('Vambrace inner leather',elbow,hand,[(.12,.047,.051),(.5,.045,.05),(.98,.033,.04)],3,math.tau,24,10)
    for t in (.25,.78):
        c=elbow.lerp(hand,t)
        limb_shell('Vambrace securing strap',c-(hand-elbow)*.02,c+(hand-elbow)*.02,
            [(0,.058 if t<.5 else .044,.065 if t<.5 else .052),(1,.058 if t<.5 else .044,.065 if t<.5 else .052)],3,math.tau,24,3)
        rivet(c+Vector((0,0,.069 if t<.5 else .055)),1,.0032)
    bone=label+' Hand'
    palm=hand+Vector((side*.016,-.023,.002))
    ellipsoid('Leather glove foundation',palm,(.04,.049,.031),3,32,16)
    # Metacarpal plate, overlapping knuckles and four curled finger defenses.
    oval_cup('Gauntlet metacarpal plate',palm+Vector((0,0,.02)),.039,.043,.018)
    for k in range(4):
        x=hand.x+side*(-.018+k*.015)
        for j in range(2):
            ellipsoid('Finger defense',(x+side*.006*j,hand.y-.026-j*.02,hand.z+.035-j*.008),(.009,.014,.007),0,16,10)
    sweep('Gauntlet thumb',[palm+Vector((-side*.029,.015,.005)),palm+Vector((-side*.044,-.01,.025)),palm+Vector((-side*.021,-.022,.032))],.012,3,12,14)
    hip,knee,ankle=joint(label+' Thigh'),joint(label+' Calf'),joint(label+' Foot')
    bone=label+' Thigh'
    limb_shell('Fitted wool hose',hip,knee,[(0,.072,.077),(.35,.074,.079),(.75,.055,.062),(1,.048,.055)],2,math.tau,28,12)
    limb_shell('Thigh cuisse',hip,knee,[(.20,.077,.088),(.38,.073,.088),(.67,.063,.079),(.9,.053,.066)],0,math.pi*1.7,40,20)
    bone=label+' Calf'
    limb_shell('Knee mail voider',hip.lerp(knee,.9),knee.lerp(ankle,.12),[(0,.049,.053),(1,.047,.052)],4,math.tau,28,8)
    oval_cup('Knee poleyn with side wing',knee+Vector((0,0,.049)),.055,.065,.025,.028,side)
    for j in (-1,1):
        c=knee+Vector((0,j*.055,.042))
        oval_cup('Knee articulation lame',c,.055,.022,.019,0,side)
    limb_shell('Close-fitting shin greave',knee,ankle,[(.18,.049,.068),(.35,.057,.075),(.65,.044,.061),(.94,.034,.046)],0,math.pi*1.85,40,24)
    limb_shell('Calf plate backing',knee,ankle,[(.13,.043,.049),(.35,.052,.062),(.65,.039,.05),(.98,.029,.037)],3,math.tau,28,16)
    for t in (.28,.73):
        c=knee.lerp(ankle,t)
        limb_shell('Greave calf strap',c-Vector((0,.008,0)),c+Vector((0,.008,0)),[(0,.056 if t<.5 else .04,.068 if t<.5 else .052),(1,.056 if t<.5 else .04,.068 if t<.5 else .052)],3,math.tau,28,3)
    bone=label+' Foot'
    limb_shell('Boot ankle cuff',ankle-Vector((0,.035,0)),ankle+Vector((0,.07,0)),
        [(0,.034,.041),(.5,.037,.045),(1,.04,.047)],3,math.tau,32,12)
    boot_join=body_loft('Supple boot instep and ankle',[(ankle.y-.084,.029,.034,ankle.z+.014),
        (ankle.y-.047,.034,.047,ankle.z+.017),(ankle.y+.015,.034,.039,ankle.z+.005)],3,32,14)
    boot_join.location.x=ankle.x
    foot=ankle+Vector((0,-.076,.039))
    def last(u,v):
        a=u*math.tau; z=-.103+v*.264
        taper=min(1,max(.001,math.sin(v*math.pi)**.25*1.16))
        w=(.048+.005*math.sin(v*math.pi)-.015*v*v)*taper
        h=(.028+.022*math.exp(-((v-.25)/.26)**2))*taper
        return foot+Vector((math.cos(a)*w,-.010+math.sin(a)*(h if math.sin(a)>0 else .021),z))
    patch('Closed leather boot last',last,40,24,3,.002,0)
    for j in range(6):
        z=-.036+j*.03; width=.102-j*.005
        patch('Overlapping sabaton plate '+str(j),lambda u,v,z=z,j=j,width=width:foot+Vector(((u-.5)*width,
            .005-.0015*j+.024*math.sin(u*math.pi)+.002*math.sin(v*math.pi),z+v*.037)),24,7,0,.002,0)
    def sole(u,v):
        a=u*math.tau
        return foot+Vector((math.sin(a)*(.044+.004*math.cos(a)), -.033+v*.009,
            .025+math.cos(a)*.128))
    patch('Solid stitched boot sole',sole,48,3,7,.006,0)

bone='Head'
# A pointed bascinet with a tapered visor and two recessed eye openings. The
# brow and cheeks follow one head volume; there is no separate cylindrical mask.
def skull(u,v):
    phi=u*math.tau; theta=v*2.22; s=math.sin(theta)
    return (.105*s*math.sin(phi),1.404+.119*math.cos(theta)+.017*max(0,math.cos(theta))**5,
        -.048+.119*s*math.cos(phi)-.012*max(0,-math.cos(phi))*v*v)
patch('Forged pointed bascinet skull',skull,64,32,0,.0027,0,(2,1.5))
sweep('Helmet stitched lining edge',[skull(i/96,1) for i in range(97)],.0021,3,8,144)
def visor(u,v):
    a=(u-.5)*2.55
    w=.102*(1-.22*v)
    nose=.050*(1-abs(math.sin(a))**.65)*math.sin(v*math.pi*.88)
    return (math.sin(a)*w,1.408-.113*v+.006*math.cos(a),
        -.025+math.cos(a)*(.122-.018*v)+nose)
# Split the plate at the sight line, leaving real negative space for both eyes.
patch('Visor upper brow',lambda u,v:visor(u,v*.10),48,6,0,.0025,0)
patch('Tapered cheek and breathing visor',lambda u,v:visor(u,.18+.82*v),48,24,0,.0025,0)
sweep('Visor lower rolled border',[visor(i/64,1) for i in range(65)],.002,0,8,128)
patch('Recessed dark eye slit',lambda u,v:Vector(visor(u,.105+.075*v))-Vector((0,0,.005)),48,4,7,.001,0)
sweep('Visor central nose bridge',[visor(.5,.08),visor(.5,.15),visor(.5,.23)],.0032,0,8,8)
for side in (-1,1):
    rivet((side*.098,1.405,.008),1,.0055)
    for row in range(2):
        for k in range(4):
            a=side*(.21+k*.13)
            p=Vector(visor((a/2.55)+.5,.5+row*.15))
            ellipsoid('Visor breathing vent',p+Vector((0,0,.001)),(.0027,.0034,.001),7,12,6)
    for a in (.9,1.45,2.1,2.7):
        p=Vector(skull((a*side%math.tau)/math.tau,.94)); rivet(p,1,.0028)

# The aventail meets the jaw directly, giving the head a short, human neck.
for obj in groups['Body']:
    if obj['bone']==bone_names.index('Bip001 Head'):obj.location.z-=.055

# Equipment retains its functional coordinate contracts, but with narrower blade
# and quiver proportions and restrained hardware.
exec(compile(library[library.index("current='Sword'"):],'marchwarden_equipment','exec'),globals())
for obj in groups['Sword']:
    if obj.name.startswith('Refined tempered blade'):
        for vertex in obj.data.vertices: vertex.co.z*=.72 # Unity blade width is Blender Z
for obj in groups['Quiver']:
    obj.scale.x*=.76; obj.scale.y*=.76
# Replace the raised decorative branches with a thin painted field chevron.
for obj in list(groups['Shield']):
    if obj.name.startswith(('Watchlight','Heraldic','Shield inner brass','Shield leather enarme')):
        groups['Shield'].remove(obj); bpy.data.objects.remove(obj,do_unlink=True)
current='Shield'
# Enarmes cross the forearm rather than running parallel above and below it.
for side in (-1,1):
    sweep('Fitted forearm enarme',[(-.064,side*.10,.020),(-.038,side*.10,-.085),
        (.038,side*.10,-.085),(.064,side*.10,.020)],.010,3,12,36,flatten=.45)
for side in (-1,1):
    patch('Painted field chevron',lambda u,v,side=side:(-.08+v*.16+(u-.5)*.019,side*.13*(1-v),
        shield_z(-.08+v*.16+(u-.5)*.019,side*.13*(1-v))+.0058),4,24,5,.0002,0)
