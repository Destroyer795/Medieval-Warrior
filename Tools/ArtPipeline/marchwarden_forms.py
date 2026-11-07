"""Marchwarden second sculpt pass. Executed in the Blender generator's namespace.

Continuous quad surfaces, shaped plate shells and draped cloth replace the first
pass's primitive solids. All coordinates remain compatible with the animation rig.
"""

def smooth_mesh(name, verts, faces, mat, uv=None, thickness=0, subdiv=0):
    o=mesh(name,verts,faces,mat)
    for p in o.data.polygons: p.use_smooth=True
    if uv:
        layer=o.data.uv_layers.new(name='Surface UV')
        for polygon in o.data.polygons:
            for li in polygon.loop_indices:
                layer.data[li].uv=uv[o.data.loops[li].vertex_index]
    # Join the mathematical wrap seams and radial poles before smoothing. Keeping
    # duplicate boundary vertices produces creases and pointed subdivision seams.
    import bmesh
    bm=bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-9)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(o.data); bm.free()
    if subdiv and mat in (2,3):
        mod=o.modifiers.new('Sculpted curvature','SUBSURF'); mod.levels=subdiv; mod.render_levels=subdiv
    if thickness:
        mod=o.modifiers.new('Material thickness','SOLIDIFY'); mod.thickness=thickness; mod.offset=-1
    return o

def patch(name,fn,nu,nv,mat,thickness=.003,subdiv=1,uvscale=(1,1)):
    verts=[fn(i/nu,j/nv) for j in range(nv+1) for i in range(nu+1)]
    faces=[(j*(nu+1)+i,j*(nu+1)+i+1,(j+1)*(nu+1)+i+1,(j+1)*(nu+1)+i) for j in range(nv) for i in range(nu)]
    uv=[(i/nu*uvscale[0],j/nv*uvscale[1]) for j in range(nv+1) for i in range(nu+1)]
    return smooth_mesh(name,verts,faces,mat,uv,thickness,subdiv)

def catmull(points,t):
    k=min(int(t*(len(points)-1)),len(points)-2); f=t*(len(points)-1)-k
    p0=Vector(points[max(0,k-1)]); p1=Vector(points[k]); p2=Vector(points[k+1]); p3=Vector(points[min(len(points)-1,k+2)])
    return .5*((2*p1)+(-p0+p2)*f+(2*p0-5*p1+4*p2-p3)*f*f+(-p0+3*p1-3*p2+p3)*f*f*f)

def sweep(name,points,radius,mat,sides=8,steps=None,flatten=1):
    # One continuous skin along a spline; no separate capped cylinders or kinks.
    steps=steps or max(12,(len(points)-1)*8)
    verts=[]; uv=[]
    for j in range(steps+1):
        t=j/steps; c=catmull(points,t)
        d=(catmull(points,min(1,t+.001))-catmull(points,max(0,t-.001))).normalized()
        a=d.cross(Vector((0,0,1)) if abs(d.z)<.85 else Vector((0,1,0))).normalized(); b=d.cross(a)
        r=radius(t) if callable(radius) else radius
        for i in range(sides):
            angle=i*math.tau/sides
            verts.append(c+a*math.cos(angle)*r+b*math.sin(angle)*r*flatten)
            uv.append((i/sides,t*2))
    faces=[(j*sides+i,j*sides+(i+1)%sides,(j+1)*sides+(i+1)%sides,(j+1)*sides+i) for j in range(steps) for i in range(sides)]
    faces += [tuple(reversed(range(sides))),tuple(range(steps*sides,(steps+1)*sides))]
    return smooth_mesh(name,verts,faces,mat,uv)

def body_loft(name,profile,mat,segments=48,steps=24):
    # Smooth interpolation through an anatomical cross-section profile.
    verts=[]; uv=[]
    for j in range(steps+1):
        y,w,d,z=catmull(profile,j/steps)
        for i in range(segments+1):
            a=i*math.tau/segments
            verts.append((math.cos(a)*w,y,z+math.sin(a)*d))
            uv.append((i/segments*2,j/steps*2))
    row=segments+1
    faces=[(j*row+i,j*row+i+1,(j+1)*row+i+1,(j+1)*row+i) for j in range(steps) for i in range(segments)]
    faces += [tuple(reversed(range(segments))),tuple(range(steps*row,steps*row+segments))]
    o=smooth_mesh(name,verts,faces,mat,uv)
    return o

def limb_shell(name,a,b,profile,mat,arc=math.tau,segments=28,steps=10,phase=0):
    a,b=Vector(a),Vector(b); direction=(b-a).normalized()
    front=Vector((0,0,1)); front=(front-direction*front.dot(direction)).normalized()
    side=direction.cross(front).normalized()
    def point(u,v):
        t,w,d=catmull(profile,v); angle=(u-.5)*arc+phase
        return a.lerp(b,t)+side*math.sin(angle)*w+front*math.cos(angle)*d
    return patch(name,point,segments,steps,mat,.003 if mat==0 else 0,0,(1,1.5))

def ring_trim(name,y,w,d,z,mat,r=.003):
    points=[(math.cos(i*math.tau/32)*w,y,z+math.sin(i*math.tau/32)*d) for i in range(33)]
    return sweep(name,points,r,mat,6,128)

def cloth_panel(name,x0,x1,ytop,ybottom,zbase,mat=2,back=False):
    start=len(groups[current])
    def point(u,v):
        width=1+v*.13; x=(x0+(x1-x0)*u)*width
        y=ytop+(ybottom-ytop)*v + .016*math.sin(u*math.pi)*v*v
        z=zbase+(.006+.009*v)*math.sin(u*math.pi*5+.5*v)+.018*v*v
        return (x,y,-z if back else z)
    o=patch(name,point,18,18,mat,.004,1,(1,1.5))
    for u in (0,1): sweep('Sewn panel edge',[point(u,j/20) for j in range(21)],.002,3,6,40)
    sweep('Woven hem',[point(i/24,1) for i in range(25)],.003,5,6,48)
    for obj in groups[current][start:]: obj['clothSide']='L' if x0<0 else 'R'
    return o

def relief_crest(cx,cy,z,size=.09,plane='body'):
    # Curved rising branches around a small watchlight. Low relief, not chunky blocks.
    def p(x,y,zz):
        if plane=='shield': return (cy+y,cx+x,zz)
        return (cx+x,cy+y,zz)
    for sign in (-1,1):
        points=[p(sign*.13*size,-.52*size,z),p(sign*.33*size,-.15*size,z+.001),
            p(sign*.38*size,.18*size,z),p(sign*.24*size,.42*size,z-.001)]
        sweep('Watchlight branch',points,.0042,5,8,24,flatten=.4)
        for k in range(3):
            y=(-.16+k*.17)*size; x=sign*(.32+.03*math.sin(k))*size
            sweep('Heraldic leaf',[p(x,y,z+.001),p(x+sign*.12*size,y+.09*size,z+.002)],.0048,5,8,6,flatten=.35)
    ellipsoid('Watchlight',p(0,.19*size,z),(.011,.022,.004) if plane=='body' else (.022,.011,.004),1,24,12)

current='Body'; bone='Spine'
# Fitted quilted foundation, waist taper and convex breastplate.
body_loft('Tailored arming doublet',[(.72,.167,.113,-.045),(.84,.17,.126,-.045),(.94,.191,.142,-.048),
    (1.07,.224,.151,-.05),(1.16,.20,.123,-.055),(1.19,.14,.085,-.055)],4,40,22)
body_loft('Sculpted cuirass',[(.837,.17,.131,-.044),(.865,.171,.14,-.044),(.93,.191,.157,-.045),
    (1.035,.222,.176,-.045),(1.105,.23,.162,-.045),(1.153,.185,.125,-.05),(1.174,.125,.09,-.05)],0,56,32)
ring_trim('Cuirass lower rolled edge',.845,.175,.138,-.044,1,.003)
body_loft('Curved gorget',[(1.165,.149,.11,-.045),(1.18,.142,.108,-.045),(1.203,.112,.086,-.05),
    (1.226,.092,.076,-.05),(1.235,.092,.076,-.05)],0,48,16)
ring_trim('Gorget edge',1.233,.094,.078,-.05,1,.0025)

# Fabric lies on the cuirass with a curved drape, not a rigid rectangular board.
def chest_fabric(u,v):
    x=(u-.5)*(.235-.03*v); y=.85+v*.3
    z=.118+math.sin(v*math.pi)*.027 - (abs(x)/.14)**2*.025
    z+=.0035*math.sin(u*math.pi*6+.6*v)*math.sin(v*math.pi)
    return (x,y,z)
patch('Draped chest surcoat',chest_fabric,24,24,2,.003,1,(1,1.5))
for u in (0,1): sweep('Surcoat bound selvedge',[chest_fabric(u,j/24) for j in range(25)],.0025,5,6,48)
relief_crest(0,1.035,.152,.115)

bone='Pelvis'
for j in range(3):
    y=.841-j*.035
    body_loft('Articulated waist lame '+str(j),[(y-.048,.191+j*.012,.137,-.042),
        (y-.027,.19+j*.012,.141,-.042),(y,.183+j*.012,.136,-.042)],0,40,6)
body_loft('Supple waist belt',[(.813,.202,.155,-.04),(.825,.204,.158,-.04),(.85,.201,.155,-.04)],3,48,6)
# Buckle is an open metal frame around the belt, with a real tongue.
for sign in (-1,1):
    sweep('Buckle bow',[(sign*.028,.819,.126),(sign*.032,.834,.13),(sign*.028,.85,.126)],.004,1,8,12)
for y in (.819,.85): sweep('Buckle rail',[(-.028,y,.126),(.028,y,.126)],.004,1,8,6)
sweep('Buckle tongue',[(-.024,.834,.137),(.014,.834,.137)],.0025,1,8,6)
cloth_panel('Left flowing skirt',-.19,-.018,.785,.48,.135)
cloth_panel('Right flowing skirt',.018,.19,.785,.49,.135)
cloth_panel('Left rear cloth tail',-.15,-.012,.787,.50,.177,back=True)
cloth_panel('Right rear cloth tail',.012,.15,.787,.51,.177,back=True)
for sign in (-1,1):
    # Rounded sewn leather bags, with curved flaps rather than cubes.
    c=(sign*.214,.766,.008)
    ellipsoid('Soft belt pouch',c,(.05,.075,.047),3,32,16)
    patch('Pouch turned flap',lambda u,v:(c[0]+(u-.5)*.09,c[1]+.06-v*.065,
        c[2]+.048+.005*math.sin(u*math.pi)-.012*v*v),12,8,3,.004,1)
    rivet((c[0],c[1]+.011,c[2]+.055),1,.005)
bone='Spine'
# Flat leather baldric follows the convex breast instead of a cylindrical cord.
def baldric(u,v):
    x=-.169+v*.23+(u-.5)*.039; y=1.163-v*.3
    z=.103+.055*math.sin(v*math.pi*.85)-.006*(u-.5)**2
    return (x,y,z)
patch('Fitted leather baldric',baldric,4,28,3,.004,1,(.25,2))
for u in (.12,.88): sweep('Baldric stitching',[Vector(baldric(u,j/32))+Vector((0,0,.003)) for j in range(33)],.0009,5,5,64)

for side,label in ((-1,'L'),(1,'R')):
    upper,elbow,hand=joint(label+' UpperArm'),joint(label+' Forearm'),joint(label+' Hand')
    direction=(elbow-upper).normalized()
    bone=label+' UpperArm'
    limb_shell('Fitted mail sleeve',upper,elbow,[(0,.073,.085),(.3,.08,.083),(.7,.066,.075),(1,.059,.064)],4,math.tau,24,10)
    # Curved ellipsoidal cap with a descending front/back edge; no bucket-like flat top.
    def shoulder(u,v):
        phi=u*math.tau; theta=v*1.755
        return (upper.x+side*.008 + .127*math.sin(theta)*math.cos(phi),
            upper.y+.015+.12*math.cos(theta)-.018*math.cos(phi)*side,
            upper.z+.151*math.sin(theta)*math.sin(phi))
    patch('Swept anatomical pauldron',shoulder,40,14,0,.005,1,(2,1))
    sweep('Pauldron rolled rim',[shoulder(i/48,1) for i in range(49)],.0037,1,8,96)
    for k in range(2):
        c=upper.lerp(elbow,.23+k*.19)
        limb_shell('Curved shoulder overlap '+str(k),c-direction*.025,c+direction*.036,
            [(0,.104-k*.012,.118-k*.01),(.4,.106-k*.012,.119-k*.01),(1,.097-k*.011,.109-k*.01)],0,math.pi*1.6,28,6)
    for zsign in (-1,1): rivet((upper.x+side*.07,upper.y+.06,upper.z+zsign*.108),1,.006)
    bone=label+' Forearm'
    ellipsoid('Padded elbow joint',elbow,(.071,.066,.067),4,28,14)
    # Domed elbow wing, closely fitted rather than a metal ball.
    patch('Sculpted elbow couter',lambda u,v: elbow+Vector((math.cos(u*math.tau)*.078*v,
        math.sin(u*math.tau)*.067*v,.045+.036*(1-v*v))),36,10,0,.004,0)
    limb_shell('Leather forearm sleeve',elbow,hand,[(0,.057,.065),(.35,.062,.07),(.8,.047,.053),(1,.043,.049)],3,math.tau,24,10)
    limb_shell('Contoured vambrace',elbow,hand,[(.18,.062,.077),(.36,.067,.081),(.65,.058,.067),(.91,.047,.057)],0,math.pi*1.82,32,14)
    for t in (.28,.82):
        c=elbow.lerp(hand,t)
        limb_shell('Vambrace binding',c-(hand-elbow)*.025,c+(hand-elbow)*.025,
            [(0,.065 if t<.5 else .049,.081 if t<.5 else .06),(1,.065 if t<.5 else .049,.081 if t<.5 else .06)],3,math.tau,24,3)
    bone=label+' Hand'
    # Fist with four curled digits, thumb and separate rounded knuckle lames.
    palm=hand+Vector((side*.018,-.023,.0))
    ellipsoid('Fitted leather palm',palm,(.048,.055,.037),3,32,18)
    for k in range(4):
        x=hand.x+side*(-.021+k*.018)
        sweep('Curled glove finger',[(x,hand.y-.025,.031+hand.z),(x+side*.014,hand.y-.053,.025+hand.z),
            (x+side*.011,hand.y-.059,hand.z-.004)],.012,3,10,12)
        ellipsoid('Rounded knuckle plate',(x,hand.y-.03,hand.z+.043),(.011,.023,.008),0,16,8)
    sweep('Folded thumb',[palm+Vector((-side*.035,.018,.01)),palm+Vector((-side*.052,-.01,.029)),palm+Vector((-side*.028,-.022,.039))],.017,3,12,16)
    patch('Gauntlet back plate',lambda u,v: palm+Vector(((u-.5)*.089,(v-.5)*.073,.033+.014*math.sin(u*math.pi)*math.sin(v*math.pi))),18,14,0,.003,1)
    hip,knee,ankle=joint(label+' Thigh'),joint(label+' Calf'),joint(label+' Foot')
    bone=label+' Thigh'
    limb_shell('Fitted mail chausses',hip,knee,[(0,.079,.085),(.32,.081,.09),(.75,.061,.067),(1,.059,.062)],4,math.tau,24,12)
    limb_shell('Sculpted cuisse',hip,knee,[(.24,.084,.095),(.4,.083,.099),(.65,.077,.088),(.86,.065,.077)],0,math.pi*1.62,32,16)
    bone=label+' Calf'
    ellipsoid('Knee arming pad',knee,(.069,.063,.061),4,28,14)
    def knee_plate(u,v):
        angle=u*math.tau; r=v
        # Rounded teardrop with a modest lower point and a domed centre.
        return knee+Vector((math.cos(angle)*.081*r,
            math.sin(angle)*(.071 if math.sin(angle)>0 else .086)*r,
            .052+.039*(1-r*r)))
    patch('Rounded knee poleyn',knee_plate,40,10,0,.004,1)
    rivet(knee+Vector((0,.043,.08)),1,.005)
    limb_shell('Calf leather',knee,ankle,[(0,.058,.063),(.25,.069,.079),(.55,.057,.067),(1,.043,.052)],3,math.tau,28,12)
    limb_shell('Anatomical greave',knee,ankle,[(.17,.065,.079),(.32,.071,.086),(.6,.059,.076),(.91,.047,.063)],0,math.pi*1.85,36,18)
    sweep('Subtle greave flute',[knee.lerp(ankle,t)+Vector((0,0,.087 if t<.5 else .076)) for t in (.22,.38,.58,.81)],.0018,1,6,24)
    bone=label+' Foot'
    # Rounded last follows the toe, instep and heel. Closed smooth surface.
    foot=ankle+Vector((0,-.057,.045))
    def shoe(u,v):
        z=-.111+v*.271; angle=u*math.tau
        taper=min(1,max(.001,math.sin(v*math.pi)**.22*1.15))
        width=(.057+.009*math.sin(v*math.pi)-.012*v*v)*taper
        top=.033+.043*math.exp(-((v-.27)/.24)**2)
        return foot+Vector((math.cos(angle)*width,math.sin(angle)*(top if math.sin(angle)>0 else .037)*taper,z))
    patch('Shaped leather riding boot',shoe,32,20,3,.003,0)
    def sole(u,v):
        angle=u*math.tau; z=-.111+v*.271
        taper=min(1,max(.001,math.sin(v*math.pi)**.22*1.15))
        return foot+Vector((math.cos(angle)*(.061+.008*math.sin(v*math.pi)-.01*v*v)*taper,-.038+math.sin(angle)*.009*taper,z))
    patch('Stitched boot welt',sole,24,20,7,.002,0)
    # Four curved overlapping toe lames, shaped over the boot rather than boxes.
    for k in range(4):
        zstart=-.003+k*.034
        patch('Curved sabaton lame '+str(k),lambda u,v,k=k,zstart=zstart: foot+Vector(((u-.5)*(.129-k*.006),
            .029-.004*k+.02*math.sin(u*math.pi)+.005*math.sin(v*math.pi),zstart+v*.044)),18,5,0,.003,1)

# Fully curved sallet: a dome, rounded cheek plates and a continuous brow slit.
bone='Head'
body_loft('Sculpted sallet dome',[(1.284,.091,.087,-.026),(1.32,.105,.106,-.03),(1.38,.12,.122,-.036),
    (1.445,.118,.124,-.04),(1.505,.093,.105,-.045),(1.54,.05,.065,-.045),(1.551,.004,.007,-.045)],0,64,36)
ring_trim('Helmet lower bead',1.29,.099,.099,-.028,1,.0025)
# Swept neck guard with a genuinely curved silhouette.
patch('Curved sallet tail',lambda u,v: ((u-.5)*(.19+.06*v),1.35-.069*v,
    -.115-.10*v-.018*math.sin(u*math.pi)),28,12,0,.004,1)
def visor_point(u,v,upper=False):
    theta=(u-.5)*2.15
    x=math.sin(theta)*.119
    if upper: y=1.408+v*.023+.006*math.cos(theta); z=.011+math.cos(theta)*(.124+.01*v)
    else: y=1.39-v*.086+.005*math.cos(theta); z=.008+math.cos(theta)*(.132-.019*v)
    return (x,y,z)
patch('Swept visor brow',lambda u,v:visor_point(u,v,True),36,8,0,.004,1)
patch('Rounded lower visor',visor_point,36,18,0,.004,1)
patch('Recessed continuous eye slit',lambda u,v:(math.sin((u-.5)*2.15)*.117,1.399+v*.009,
    .006+math.cos((u-.5)*2.15)*.122),36,3,7,.002,1)
for side in (-1,1):
    for k in range(4):
        theta=side*(.25+k*.15); x=math.sin(theta)*.12
        z=.01+math.cos(theta)*.123
        # Rounded perforation recesses, rather than cut-out-looking square blocks.
        ellipsoid('Visor breathing perforation',(x,1.352-k*.001,z),(.0029,.008,.0018),7,16,10)
    rivet((side*.109,1.407,.061),1,.008)
sweep('Sallet crown ridge',[(0,1.43,-.158),(0,1.515,-.111),(0,1.555,-.045),(0,1.519,.036),(0,1.445,.092)],.0025,1,8,48)
# A curved cloth pennant with folds and a natural tapered tail.
patch('Folded helmet pennant',lambda u,v: ((u-.5)*(.045*(1-v)+.008),1.549+.049*math.sin(v*math.pi)-.015*v,
    -.045-.19*v+.007*math.sin(u*math.pi*3)*math.sin(v*math.pi)),12,24,2,.002,1)

current='Sword'
# A long continuous blade with a narrow flat and softened bevel, kept sharp at the edge.
profile=[(-.241,.033),(-.21,.034),(.13,.028),(.29,.022),(.385,.012),(.457,.0003)]
verts=[]; faces=[]
for x,w in profile:
    for y,z in [(-w,0),(-w*.63,.0055),(0,.007),(w*.63,.0055),(w,0),(w*.63,-.0055),(0,-.007),(-w*.63,-.0055)]: verts.append((x,y,z))
for j in range(len(profile)-1):
    for i in range(8): faces.append((j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i))
faces += [tuple(reversed(range(8))),tuple(range((len(profile)-1)*8,len(profile)*8))]
o=mesh('Refined tempered blade',verts,faces,0,.0007)
sweep('Blade fuller',[(-.205,0,.0072),(.06,0,.0072),(.265,0,.0072)],.0018,4,6,24,flatten=.4)
sweep('Swept steel crossguard',[(-.229,-.126,0),(-.252,-.083,0),(-.271,0,0),(-.252,.083,0),(-.229,.126,0)],lambda t:.011+.005*math.sin(t*math.pi),0,16,40,flatten=1.25)
for sign in (-1,1): ellipsoid('Guard terminal',(-.23,sign*.123,0),(.016,.012,.016),1,24,12)
sweep('Leather sword grip',[(-.416,0,0),(-.354,0,0),(-.289,0,0)],lambda t:.019+.0015*math.sin(t*math.pi),3,20,18)
# True continuous spiral binding instead of thick rings.
gripwrap=[(-.407+i/180*.108,math.cos(i/180*math.tau*9)*.020,math.sin(i/180*math.tau*9)*.020) for i in range(181)]
sweep('Spiral grip seam',gripwrap,.0012,7,6,180)
ellipsoid('Lenticular pommel',(-.443,0,0),(.029,.033,.018),0,40,20)
ellipsoid('Inlaid pommel seal',(-.443,0,.018),(.012,.015,.002),1,28,12)

current='Shield'
# Smoothly bowed heater with a rounded perimeter. No giant triangular face fan.
outline=[(.34,-.205),(.35,-.12),(.35,.12),(.34,.205),(.25,.239),(.05,.217),(-.17,.135),(-.35,0),(-.17,-.135),(.05,-.217),(.25,-.239),(.34,-.205)]
edge=[catmull([(x,y,0) for x,y in outline],i/96) for i in range(97)]
def shield_z(x,y): return .018+.035*(1-(y/.26)**2)+.006*(1-(x/.38)**2)
def shield_surface(scale,zoffset,mat,name):
    def point(u,v):
        p=catmull([(p.x,p.y,0) for p in edge],u)*v*scale
        return (p.x,p.y,shield_z(p.x,p.y)+zoffset)
    return patch(name,point,96,12,mat,.012,0,(2,2))
shield_surface(1,0,0,'Bowed steel shield shell')
shield_surface(.935,.005,2,'Smooth dyed shield facing')
sweep('Rounded shield perimeter',[(p.x,p.y,shield_z(p.x,p.y)+.001) for p in edge],.006,0,10,192)
sweep('Shield inner brass inlay',[(p.x*.939,p.y*.939,shield_z(p.x*.939,p.y*.939)+.008) for p in edge],.0015,1,6,192)
for i in range(0,96,8):
    p=edge[i]*.974; ellipsoid('Shield border rivet',(p.x,p.y,shield_z(p.x,p.y)+.005),(.005,.005,.003),1,16,8)
relief_crest(0,.015,.068,.25,'shield')
for x in (-.12,.12): sweep('Shield leather enarme',[(x,-.1,.001),(x,-.07,-.033),(x,.07,-.033),(x,.1,.001)],.012,3,10,30,flatten=.45)

current='Bow'
def bow_pos(t):
    y=-.62+1.24*t; a=y/.62
    return Vector((0,y,.2*(1-a*a)-.03*a**4))
bowpoints=[bow_pos(i/32) for i in range(33)]
sweep('Continuous yew bow limb',bowpoints,lambda t:.025-.0125*abs(t*2-1),6,16,128,flatten=.68)
sweep('Bow laminated backing',[p+Vector((0,0,.013)) for p in bowpoints],lambda t:.011-.004*abs(t*2-1),5,10,128,flatten=.35)
sweep('Leather bow grip',[bow_pos(.43),bow_pos(.5),bow_pos(.57)],.031,3,20,18,flatten=.8)
wrap=[]
for i in range(201):
    t=.437+i/200*.126; a=i/200*math.tau*10
    wrap.append(bow_pos(t)+Vector((math.cos(a)*.032,0,math.sin(a)*.026)))
sweep('Bow spiral grip binding',wrap,.0013,7,6,200)
for t in (0,1): ellipsoid('Horn string nock',bow_pos(t),(.015,.019,.014),1,24,12)

current='Quiver'
# Slight oval leather body, turned open rim, saddle seam, straps and clasp.
segments=48; verts=[]; uv=[]
for j,(y,r) in enumerate(((0,.092),(.018,.096),(.16,.101),(.37,.116),(.505,.125),(.52,.124),(.52,.112),(.502,.112),(.04,.081),(.02,.081))):
    for i in range(segments):
        a=i*math.tau/segments
        verts.append((math.cos(a)*r,y,math.sin(a)*r*.83)); uv.append((i/segments*2,y*4))
faces=[(j*segments+i,j*segments+(i+1)%segments,(j+1)*segments+(i+1)%segments,(j+1)*segments+i) for j in range(9) for i in range(segments)]
faces.append(tuple(range(9*segments,10*segments)))
smooth_mesh('Supple open quiver',verts,faces,3,uv,0,1)
for y,r in ((.035,.10),(.415,.12),(.513,.125)):
    points=[(math.cos(i*math.tau/48)*r,y,math.sin(i*math.tau/48)*r*.83) for i in range(49)]
    sweep('Turned quiver leather rim',points,.006 if y==.513 else .004,3,8,96)
sweep('Quiver saddle seam',[(.007,.035,.084),(.007,.26,.091),(.007,.499,.102)],.002,5,6,32)
for i in range(19):
    y=.047+i*.023; z=.084+y*.037
    sweep('Quiver stitch',[(-.005,y,z),(.001,y+.004,z+.001)],.0009,5,5,4)
patch('Quiver suspension tab',lambda u,v:(-.108+(u-.5)*.026,.35+v*.13,(v-.5)*.026),6,12,3,.004,1)
ellipsoid('Quiver strap clasp',(-.12,.446,.017),(.009,.019,.005),1,24,12)

current='Arrow'
sweep('Turned arrow shaft',[(0,0,0),(0,0,.4),(0,0,.785)],.005,6,10,12)
sweep('Broadhead socket',[(0,0,.766),(0,0,.804)],.008,0,12,4)
mesh('Steel hunting broadhead',[(0,-.019,.795),(0,.019,.795),(0,0,.9),(-.004,0,.804),(.004,0,.804)],[(0,3,2),(3,1,2),(1,4,2),(4,0,2),(0,4,1,3)],0,.0003)
for k in range(3):
    angle=k*math.tau/3
    def feather(u,v):
        r=.005+v*.025*math.sin(u*math.pi)**.55
        a=angle+.25*u
        return (math.cos(a)*r,math.sin(a)*r,.023+u*.16)
    patch('Curved feather fletching',feather,14,3,5,.0008,0)
sweep('Horn arrow nock',[(0,0,0),(0,0,.02)],.007,5,10,3)
