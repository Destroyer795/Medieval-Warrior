"""Fitted horror details and interchangeable, palm/head-mounted medieval gear.

Executed by revenants.py in its authoring namespace. No runtime geometry build.
All sword variants share the exact grip, ricasso and blade endpoint used by combat.
"""
PANT_HEMS=dict(Walker=.37,Runner=.56,Brute=.60,Spitter=.46,Bloater=.56,
               Warlord=.35,Swordsman=.34,Bulwark=.39)
MOUTH_Y=1.578

def snarl():
    width=(.039,.041,.045,.044,.040,.040,.038,.038)[seed]
    gape=(.017,.022,.022,.027,.020,.018,.016,.018)[seed]
    return width,gape

def mouth_line(u):
    # One lip corner draws back under the cheekbone.
    return MOUTH_Y+.004*u*(1 if seed%2 else -1)+.005*u*u

def lip_depth(x,y,u):
    hit,_,_,_=injury_bvh.ray_cast(Vector((x,y,.25)),Vector((0,0,-1)),.20)
    arc=.124-.022*u*u
    return max(arc-.005,min(arc+.006,hit.z)) if hit is not None else arc

def fit_lips(points):
    width,gape=snarl();removed=set();inner=set();outer=set()
    for face in source_faces:
        p=sum((points[i] for i in face),Vector())/len(face)
        u=p.x/width;v=(p.y-mouth_line(u))/gape
        if p.z>.072 and u*u+v*v<1:
            removed.add(tuple(face));inner.update(face)
        else:outer.update(face)
    # Put the actual cut edges on the rim, including faces with long corners.
    for i in inner & outer:
        p=points[i];u=p.x/width;v=(p.y-mouth_line(u))/gape
        r=math.hypot(u,v)
        if r<.01 or p.z<.082:continue
        u/=r;v/=r;p.x=u*width;p.y=mouth_line(u)+v*gape
    return removed

def make_dread_face():
    width,gape=snarl()
    # Fit the lip margin to the sculpt and turn the surface back into the jaw.
    # The outer edge buries into the face around the irregular cut.
    rings=((.02,-.023),(.58,-.017),(.80,-.008),(.91,-.001),(1.01,.001),(1.20,-.004),(1.20,-.035))
    vertices=[];faces=[];shades=[];columns=48
    for r,offset in rings:
        for k in range(columns):
            angle=math.tau*k/columns;u=math.cos(angle)*r;x=width*u
            y=mouth_line(u)+gape*math.sin(angle)*r
            vertices.append((x,y,lip_depth(x,y,u)+offset))
            value=.46+.42*r;shades.append((value,value*.84,value*.78))
    faces.append(tuple(reversed(range(columns))))
    for row in range(len(rings)-1):
        for k in range(columns):
            a=row*columns+k;b=row*columns+(k+1)%columns;faces.append((a,b,b+columns,a+columns))
    faces.append(tuple(range((len(rings)-1)*columns,len(rings)*columns)))
    add_mesh('Asymmetric recessed snarl',vertices,faces,10,[[(jid('Head'),1)]]*len(vertices),shades)
    ellipsoid('Recessed tongue behind lower teeth',(0,MOUTH_Y-gape*.58,.105),
              (width*.70,gape*.24,.007),11,'Head',24,10,(.58,.51,.50))
    for upper in (True,False):
        sign=1 if upper else -1
        path=[]
        for k in range(19):
            u=(k-9)/10;x=u*width;y=mouth_line(u)+sign*gape*.83*math.sqrt(1-u*u)
            path.append((x,y,lip_depth(x,y,u)-.003))
        sweep('Retracted upper gum' if upper else 'Torn lower gum',path,.0025,11,'Head',8)
        for k in range(9):
            if (k+seed+(0 if upper else 2))%7==0:continue
            u=(k-4)*.196;x=u*width
            height=.006+.002*math.sin(k*2.7+seed)+(.004 if k in (1,7) else 0)
            y=mouth_line(u)+sign*gape*.84*math.sqrt(1-u*u)
            z=lip_depth(x,y,u)-.002
            # Tapered, bevelled enamel; the crown has a separate cutting edge.
            vertices=[];faces=[]
            for row,(w,d) in enumerate(((.0024,.0025),(.0030,.0027),(.0022,.0015))):
                yy=y-sign*row*.5*height
                for xx,zz in ((-w,-d),(w,-d),(w,d),(-w,d)):
                    vertices.append((x+xx+row*.0007*math.sin(seed+k),yy,z+zz-row*.0006))
            for row in range(2):
                for j in range(4):a=row*4+j;b=row*4+(j+1)%4;faces.append((a,b,b+4,a+4))
            faces.extend(((3,2,1,0),(8,9,10,11)))
            tooth=rigid('Broken yellow enamel tooth',vertices,faces,4,'Head',(.89,.86,.74))
            mod=tooth.modifiers.new('Worn enamel edges','BEVEL');mod.width=.0007;mod.segments=2
    # Small dried tear tracks and an asymmetric scalp laceration, not a red mask.
    # Patch a seam/facing onto the surviving garment with identical skin weights.
    if kind not in ('Bloater','Brute'):
        fitted_surface('Faded reinforced garment facing',13,
            lambda p,f: jerkin_region(p,f) and torso_region(p,f) and p.z>.05
            and (.018<abs(p.x)<.031 or 1.025<p.y<1.047),.021)

def make_helmet(style):
    # Dome envelopes the actual sculpt; tilted lower rim leaves brow and eyes open.
    head=[p for i,p in enumerate(body_points) if p.y>1.69 and not hand_removed[i]]
    top=max(p.y for p in head)+.011
    rx=max(abs(p.x) for p in head)+.009
    front=max(p.z for p in head);back=min(p.z for p in head)
    cz=(front+back)*.5;rz=(front-back)*.5+.014
    vertices=[];faces=[];cols=56;rows=16
    for row in range(rows+1):
        t=row/rows;theta=.025+t*(math.pi*.5-.025)
        for k in range(cols):
            a=k/cols*math.tau;rear=max(0,-math.cos(a))
            bottom=1.697-(.045 if style==0 else .015 if style==1 else .027)*rear
            x=rx*math.sin(theta)*math.sin(a)
            y=bottom+(top-bottom)*math.cos(theta)
            z=cz+rz*math.sin(theta)*math.cos(a)
            # Shallow raised medial ridge catches a broad, matte highlight.
            y+=.005*math.exp(-(x/.014)**2)*math.sin(theta)
            vertices.append((x,y,z))
    for row in range(rows):
        for k in range(cols):a=row*cols+k;b=row*cols+(k+1)%cols;faces.append((a,b,b+cols,a+cols))
    faces.append(tuple(reversed(range(cols))))
    helmet=rigid(('Ruined sallet','Broad kettle helm','Riveted nasal helm')[style],vertices,faces,2,'Head')
    helmet.modifiers.new('Forged helmet thickness','SOLIDIFY').thickness=.004
    bevel=helmet.modifiers.new('Softened helmet lip','BEVEL');bevel.width=.0017;bevel.segments=2
    rim=[Vector(vertices[rows*cols+k]) for k in range(cols)]
    sweep('Rolled helmet rim',rim+[rim[0]],.0038,6 if kind=='Warlord' else 2,'Head',8)
    if style==1:
        points=[];quads=[]
        for row in range(4):
            s=row/3
            for k in range(cols):
                a=k/cols*math.tau
                points.append((math.sin(a)*rx*(1+s*.40),1.697-.010*s+.009*math.cos(a),cz+math.cos(a)*rz*(1+s*.38)))
        for row in range(3):
            for k in range(cols):a=row*cols+k;b=row*cols+(k+1)%cols;quads.append((a,b,b+cols,a+cols))
        brim=rigid('Curved kettle brim',points,quads,2,'Head');brim.modifiers.new('Brim forged thickness','SOLIDIFY').thickness=.003
    if style==2:
        points=[(-.009,1.728,front+.011),(.009,1.728,front+.011),(.007,1.637,front+.028),(-.007,1.637,front+.028)]
        nasal=rigid('Tapered forged nasal',points,[(0,1,2,3)],2,'Head');nasal.modifiers.new('Nasal thickness','SOLIDIFY').thickness=.004
    if style==0:
        for side in (-1,1):
            points=[(side*rx*.89,1.707,cz+.019),(side*rx*.99,1.667,cz+.027),
                    (side*rx*.91,1.618,cz+.018),(side*rx*.72,1.651,cz-.016)]
            guard=rigid('Articulated sallet cheek plate',points,[(0,1,2,3)],2,'Head');guard.modifiers.new('Cheek plate thickness','SOLIDIFY').thickness=.003
    if kind=='Warlord':
        for k in range(7):
            a=math.tau*k/7;c=Vector((rx*.96*math.sin(a),1.746,cz+rz*.96*math.cos(a)))
            sweep('Broken royal coronet tooth',[c,c+Vector((0,.035+.015*(k%2),0)),c+Vector((0,.07+.014*(k%3),0))],
                  lambda t:.009*(1-t)+.001,6,'Head',6)
    for k in range(12):
        a=k/12*math.tau
        ellipsoid('Helmet band rivet',(rx*math.sin(a),1.706,cz+rz*math.cos(a)),(.003,.003,.003),6,'Head',10,6)

def make_sword(grips,style):
    g=next(g for g in grips if g['side']=='R')
    c=Vector(tuple(g['position'][k] for k in 'xyz'));blade=Vector(tuple(g['along'][k] for k in 'xyz'))
    normal=Vector(tuple(g['normal'][k] for k in 'xyz'));edge=unit(blade.cross(normal))
    root=c+blade*.075;tip=Vector(tuple(g['bladeTip'][k] for k in 'xyz'))
    sweep('Worn cord-bound power grip',[c-blade*.065,c+blade*.065],.0165,3,'RArmPalm',12)
    for j in range(10):
        p=c+blade*(-.057+j*.012)
        path=[p+(normal*math.cos(a)+edge*math.sin(a))*.017 for a in np.linspace(0,math.tau,17)]
        sweep('Individual grip binding',path,.0013,13,'RArmPalm',5)
    ellipsoid(('Wheel pommel','Lobed pommel','Scent stopper pommel')[style],c-blade*.088,
              ((.023,.023,.015),(.027,.020,.019),(.020,.031,.020))[style],2,'RArmPalm',20,12)
    path=[root+edge*x+blade*((-.015 if style==0 else .026 if style==1 else -.035)*(abs(x)/.108)**2) for x in np.linspace(-.108,.108,17)]
    sweep('Forged swept quillons',path,.0095,2,'RArmPalm',10)
    vertices=[];faces=[];rings=19
    for row in range(rings):
        t=row/(rings-1);p=root.lerp(tip,t)
        width=(.029*(1-t*.58) if style==0 else .036*(1-t*.65) if style==1 else .026+.010*math.sin(t*math.pi))
        width*=1-smooth((t-.83)/.17)
        width=max(.0005,width)
        notch=.003 if style==1 and row in (5,11) else 0
        for x,z in ((-width+notch,0),(-width*.27,.0038),(width*.27,.0038),(width,0),(width*.27,-.0038),(-width*.27,-.0038)):
            vertices.append(p+edge*x+normal*z)
    for row in range(rings-1):
        for k in range(6):a=row*6+k;b=row*6+(k+1)%6;faces.append((a,b,b+6,a+6))
    faces.extend((tuple(reversed(range(6))),tuple((rings-1)*6+k for k in range(6))))
    rigid(('Tapered arming sword','Notched broad sword','Leaf profile war sword')[style],vertices,faces,2,'RArmPalm')

def export_dread_equipment(grips,armature):
    global objects
    body_objects=objects
    for category in ('Helmet','Sword'):
        if category=='Sword' and kind not in ('Swordsman','Bulwark','Warlord'):continue
        for option in range(3):
            objects=[]
            if category=='Helmet':make_helmet(option)
            else:make_sword(grips,option)
            all_exports.append(export_objects(kind+'_'+category+str(option),objects,[]))
            for obj in objects:
                mod=obj.modifiers.new('Equipment bone mount','ARMATURE');mod.object=armature;obj.parent=armature
                obj.hide_render=option!=seed%3 or category=='Helmet' and kind in ('Walker','Runner','Brute','Spitter','Bloater')
                obj.hide_set(obj.hide_render)
    objects=body_objects

def fit_crowd_budget():
    # Spend topology on faces, hands and silhouette. Reduce hidden fabric folds
    # before adding distance LODs; the editable cloth remains beneath modifiers.
    deps=bpy.context.evaluated_depsgraph_get();total=0;cloth=0;candidates=[]
    for obj in objects:
        ev=obj.evaluated_get(deps);mesh=ev.to_mesh();mesh.calc_loop_triangles();count=len(mesh.loop_triangles);ev.to_mesh_clear()
        total+=count
        if obj['surface'] in (1,12,13) and count>300:
            candidates.append(obj);cloth+=count
    if total>63500:
        ratio=max(.20,min(1,(63500-(total-cloth))/max(1,cloth)))
        for obj in candidates:
            mod=obj.modifiers.new('Authored crowd fabric budget','DECIMATE');mod.ratio=ratio
        print('REVENANT fabric budget',kind,total,'cloth ratio',round(ratio,3),flush=True)
