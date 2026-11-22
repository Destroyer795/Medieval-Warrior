"""Blender source, skinning and PBR export for the eight Revenants.

Run with Blender 4.5 --background --python Tools/ArtPipeline/revenants.py.
The CC0 MakeHuman body supplies continuous anatomical topology, not primitives.
All outfit/equipment, silhouettes, decay, grip poses and materials are authored here.
Unity's existing animation skeleton is fitted by joint frames, in metre units.
"""
import bpy, bmesh, json, math, random, sys
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'ArtSource/Revenants'
OUT=ROOT/'Tools/ArtPipeline/Revenants'
TEXTURES=ROOT/'Assets/Resources/Revenants/Textures'
SHOTS=ROOT/'Artifacts/Revenants'
for p in (SOURCE,OUT,TEXTURES,SHOTS):p.mkdir(parents=True,exist_ok=True)
rig=json.loads((OUT/'rig.json').read_text())['bones']
names=[b['name'] for b in rig]
joints=[Vector(tuple(b['position'][k] for k in 'xyz')) for b in rig]
def jid(n):return names.index('Base Human'+n)
def J(n):return joints[jid(n)]
def B(v):return Vector((v[0],-v[2],v[1]))
def U(v):return Vector((v[0],v[2],-v[1]))
def smooth(t):t=max(0,min(1,t));return t*t*(3-2*t)
def unit(v):return Vector(v).normalized()
def frame(a,b):
    y=(b-a).normalized();z=Vector((0,0,1));z=(z-y*z.dot(y)).normalized();x=y.cross(z)
    return Matrix((x,y,z)).transposed()
def near(p,a,b):
    d=b-a;t=max(0,min(1,(p-a).dot(d)/max(d.length_squared,1e-8)))
    return (p-a-d*t).length,t
def vec(v):return dict(zip('xyz',(round(float(c),6) for c in v)))
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)

# Shared atlas: one skinned body draw (including eyes/armor), plus rigid variants.
# UV0 is a tile number + local UV; the shader filters tile coordinates explicitly.
palette=[('Necrotic skin',(.47,.50,.41),0,.88),('Burial linen',(.67,.64,.57),0,.98),
 ('Oxidised iron',(.33,.345,.35),.70,.84),('Old oxblood leather',(.25,.14,.09),0,.94),
 ('Exposed bone',(.68,.61,.43),0,.76),('Splintered oak',(.27,.18,.10),0,.85),
 ('Tarnished bronze',(.32,.25,.14),.30,.94),('Clouded eyes',(.38,.37,.31),0,.90),('Matted hair',(.16,.11,.075),0,.98),('Scalp stubble',(.12,.10,.075),0,.98),
 ('Clotted blood',(.12,.012,.009),0,.98),('Exposed muscle',(.34,.065,.048),0,.94),
 ('Trouser linen',(.59,.57,.51),0,.98),('Faded facing',(.72,.68,.57),0,.98)]
materials=[];rng=np.random.default_rng(518);tile=512;size=tile*4
albedo=np.ones((size,size,4),dtype=np.float32)
normal=np.ones_like(albedo);normal[:,:,:3]=(.5,.5,1)
surface=np.ones_like(albedo);surface[:,:,:3]=0;surface[:,:,3]=.2
for i,(name,col,metal,rough) in enumerate(palette):
    y,x=np.mgrid[0:tile,0:tile]/tile
    waves=np.sin(x*math.tau*7+np.sin(y*math.tau*5))+np.cos(y*math.tau*11+x*19)*.5
    grains=rng.normal(0,1,(tile,tile))
    height=waves*.015+grains*.004
    tone=.90+waves*.035+grains*.015
    if i==0:
        veins=np.exp(-(np.sin(x*math.tau*13+np.sin(y*math.tau*7)*2.7)*26)**2)*np.exp(-(np.cos(y*math.tau*5+x*17)*4)**2)
        creases=np.maximum(0,grains-1.4)
        height+=veins*.009-creases*.012;tone-=veins*.12+creases*.07
    elif i in (1,12,13):
        weave=np.sin(x*math.tau*112)*np.cos(y*math.tau*112)
        height=weave*.008+grains*.0018
        dirt=(np.sin(x*31+np.sin(y*19)*2)*np.sin(y*27+x*7)+1)*.5
        stains=np.maximum(0,np.sin(x*13+np.sin(y*23)*1.7)*np.cos(y*17-x*5))
        tone=.90+weave*.018+grains*.012+dirt*.04-stains*.06
    elif i in (2,6):
        pits=np.maximum(0,grains-1.2);height=grains*.002-pits*.010;tone=.91+grains*.012-pits*.045
        for k in range(30):
            a,b=rng.uniform(0,1,2);s=np.exp(-((x-a+(y-b)*.3)/.0017)**2)*np.exp(-((y-b)/.08)**8)
            height-=s*.12;tone-=s*.3
    elif i==3:height+=np.sin(x*110+np.sin(y*63))*np.sin(y*117)*.016
    elif i==5:
        grain=np.sin(x*math.tau*38+np.sin(y*math.tau*3)*2);height+=grain*.06;tone+=grain*.08
    elif i==8:
        strand=np.sin(x*math.tau*93+np.sin(y*math.tau*5)*1.4);height+=strand*.05;tone+=strand*.19
    dx=(np.roll(height,-1,1)-np.roll(height,1,1))*2
    dy=(np.roll(height,-1,0)-np.roll(height,1,0))*2
    n=np.stack((-dx,-dy,np.ones_like(dx)),-1);n/=np.linalg.norm(n,axis=-1,keepdims=True)
    yy=(i//4)*tile;xx=(i%4)*tile
    albedo[yy:yy+tile,xx:xx+tile,:3]=np.clip(tone[:,:,None]*np.array(col),0,1)
    if i in (2,6):
        field=rng.normal(0,1,(tile,tile));fy=np.fft.fftfreq(tile);fx=np.fft.fftfreq(tile)
        blur=np.fft.ifft2(np.fft.fft2(field)*np.exp(-2*math.pi**2*32**2*(fy[:,None]**2+fx[None,:]**2))).real
        field=(blur-blur.mean())/max(blur.std(),.00001);rust=np.clip((field-.4)*.10,0,.28)
        albedo[yy:yy+tile,xx:xx+tile,:3]=albedo[yy:yy+tile,xx:xx+tile,:3]*(1-rust[:,:,None])+np.array((.18,.070,.025))*rust[:,:,None]
    if i==8:
        alpha=np.zeros_like(x)
        for strand in range(12):
            start=.045+strand/11*.91+rng.uniform(-.012,.012)
            center=.5+(start-.5)*(1-y*.28)+.011*np.sin(y*math.tau*1.4+strand*.7)
            root=np.clip(y/.10,0,1);tip=np.clip((1-y)/.18,0,1)
            alpha=np.maximum(alpha,np.exp(-((x-center)/rng.uniform(.012,.021))**2)*root*tip)
        albedo[yy:yy+tile,xx:xx+tile,3]=alpha
    if i==9:albedo[yy:yy+tile,xx:xx+tile,3]=.82+.18*np.clip(grains,0,1)
    normal[yy:yy+tile,xx:xx+tile,:3]=n*.5+.5
    surface[yy:yy+tile,xx:xx+tile,:3]=metal
    surface[yy:yy+tile,xx:xx+tile,3]=np.clip(1-rough+waves*.025,.02,.85)
    m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*col,1)
    bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
    vc=m.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Decay'
    mix=m.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[1].default_value=(*col,1)
    m.node_tree.links.new(vc.outputs['Color'],mix.inputs[2]);m.node_tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
    materials.append(m)
for label,pixels,nc in [('Albedo',albedo,False),('Normal',normal,True),('Surface',surface,True)]:
    im=bpy.data.images.new('Revenant '+label,width=size,height=size,alpha=True)
    if nc:im.colorspace_settings.name='Non-Color'
    im.pixels.foreach_set(pixels.ravel());im.filepath_raw=str(TEXTURES/(label+'.png'));im.file_format='PNG';im.save()

# Keep all MakeHuman topology (including real ears, palms, nails and toe webs).
raw=[];body_faces=[];body_uv=[];uv=[];groups={};group=''
for line in (ROOT/'ArtSource/Marchwarden/Anatomy/MakeHuman-base.obj').read_text().splitlines():
    t=line.split()
    if not t:continue
    if t[0]=='v':raw.append(Vector(tuple(map(float,t[1:4]))))
    elif t[0]=='vt':uv.append(tuple(map(float,t[1:3])))
    elif t[0]=='g':group=t[1]
    elif t[0]=='f':
        f=[int(a.split('/')[0])-1 for a in t[1:]];groups.setdefault(group,[]).append(f)
        if group=='body':
            body_faces.append(f);body_uv.append([uv[int(a.split('/')[1])-1] for a in t[1:]])
markers={}
for n,fs in groups.items():
    if n.startswith('joint-'):
        ids={i for f in fs for i in f};markers[n[6:]]=sum((raw[i] for i in ids),Vector())/len(ids)
used=sorted({i for f in body_faces for i in f});lookup={i:j for j,i in enumerate(used)}
source_points=[raw[i]*.105 for i in used]
source_faces=[[lookup[i] for i in f] for f in body_faces]
def M(n):return markers[n]*.105

# Segment-space fitting: joints land exactly on the animation skeleton. Body
# weights blend across joints, but never across opposing limbs or finger webs.
segments=[]
def segment(name,a,b,ta,tb,radius=1):
    a,b,ta,tb=map(Vector,(a,b,ta,tb));sf=frame(a,b);tf=frame(ta,tb)
    rot=tf@Matrix.Diagonal((1,(tb-ta).length/(b-a).length,1))@sf.inverted()
    segments.append(dict(name=name,a=a,b=b,ta=ta,tb=tb,rot=rot,radius=radius,index=jid(name)))
segment('Pelvis',M('pelvis'),M('spine-3'),J('Pelvis'),J('Spine1'))
segment('Spine1',M('spine-3'),M('spine-2'),J('Spine1'),J('Spine2'))
segment('Spine2',M('spine-2'),M('spine-1'),J('Spine2'),Vector((0,1.39,-.055)))
segment('Ribcage',M('spine-1'),M('neck'),Vector((0,1.39,-.055)),J('Neck'))
segment('Neck',M('neck'),M('head'),J('Neck'),J('Head'))
segment('Head',M('head'),M('head-2'),J('Head'),J('Head')+Vector((0,.155,0)))
for side,ss in [('L','r'),('R','l')]:
    thigh='LLegThigh' if side=='L' else 'RThigh'
    calf='LLegCalf' if side=='L' else 'RCalf'
    foot='LLegFoot' if side=='L' else 'RFoot'
    toe='LLegDigit11' if side=='L' else 'RDigit11'
    for name,a,b,ta,tb in [
        (thigh,'upper-leg','knee',J(thigh),J(calf)),(calf,'knee','ankle',J(calf),J(foot)),
        (foot,'ankle','foot-2',J(foot),J(toe)),
        (side+'ArmUpperarm','shoulder','elbow',J(side+'ArmUpperarm'),J(side+'ArmForearm')),
        (side+'ArmForearm','elbow','hand',J(side+'ArmForearm'),J(side+'ArmPalm')),
        (side+'ArmPalm','hand','finger-3-1',J(side+'ArmPalm'),J(side+'ArmDigit31'))]:
        if name.endswith('Palm'):
            tb=ta+(ta-J(side+'ArmForearm')).normalized()*(M(ss+'-'+b)-M(ss+'-'+a)).length
        segment(name,M(ss+'-'+a),M(ss+'-'+b),ta,tb)
    for f in range(1,6):
        count=2 if f==1 else 3
        for k in range(count):
            name=side+'ArmDigit'+str(f)+str(k+1);ta=J(name)
            tb=J(side+'ArmDigit'+str(f)+str(k+2)) if k<count-1 else ta+(ta-J(side+'ArmDigit'+str(f)+str(max(1,k))))*.7
            if (tb-ta).length<.003:tb=ta+Vector((0,-.025,.012))
            segment(name,M(ss+'-finger-%d-%d'%(f,k+1)),M(ss+'-finger-%d-%d'%(f,k+2)),ta,tb)

def candidates(p):
    # Semantic boundaries prevent the upper thigh from binding to fingertips.
    if p.y>.715:allowed=[segments[5]]
    elif p.y>.615:allowed=segments[4:6]
    elif p.y<M('pelvis').y+.105:
        prefix='L' if p.x<0 else 'R'
        allowed=[s for s in segments if (('Leg' in s['name'] or s['name'] in ('RThigh','RCalf','RFoot')) and (s['name'].startswith(prefix) or abs(p.x)<.045)) or s['name'] in ('Pelvis','Spine1')]
    else:
        allowed=segments[:6]
        if abs(p.x)>.13 and p.y<.60:
            prefix='L' if p.x<0 else 'R';allowed=allowed+[s for s in segments if s['name'].startswith(prefix+'Arm') and 'Digit' not in s['name']]
    def score(s):
        radius=.13 if s['name'] in ('Pelvis','Spine1','Spine2','Ribcage') else .045 if 'Arm' in s['name'] else .07
        if 'Digit' in s['name']:radius=.018
        if 'Palm' in s['name']:radius=.047
        return near(p,s['a'],s['b'])[0]/radius
    scored=sorted((score(s),i,s) for i,s in enumerate(allowed))
    best=scored[0][0];selected=[(d,s) for d,_,s in scored[:4] if d<best+.70]
    softness=.30 if -.06<p.y<M('pelvis').y+.105 else .13
    values=[math.exp(-(d-best)/softness) for d,s in selected];total=sum(values)
    return [(s,w/total) for (d,s),w in zip(selected,values)]
fitted=[];skin=[]
height_source=[M(n).y for n in ('pelvis','spine-3','spine-2','spine-1','neck','head','head-2')]
height_target=[.880,1.030,1.170,1.375,1.495,1.627,1.782]
center_source=[M(n).z for n in ('pelvis','spine-3','spine-2','spine-1','neck','head','head-2')]
center_target=[-.050,-.054,-.001,-.055,-.067,-.014,-.014]
def fit(p,sw):
    # Torso cross-sections retain a common anatomical forward axis. Rotating
    # the skin independently with each old spine/neck segment shears the neck
    # and facial features because that legacy rig's neck is strongly slanted.
    y=float(np.interp(p.y,height_source,height_target))
    if p.y>height_source[-1]:y=height_target[-1]+(p.y-height_source[-1])*(height_target[-1]-height_target[-2])/(height_source[-1]-height_source[-2])
    if p.y<height_source[0]:y=height_target[0]+(p.y-height_source[0])*(height_target[1]-height_target[0])/(height_source[1]-height_source[0])
    z=p.z+float(np.interp(p.y,height_source,np.array(center_target)-np.array(center_source)))
    torso=Vector((p.x,y,z))
    result=sum(((torso if s['index']<6 else s['ta']+s['rot']@(p-s['a']))*w for s,w in sw),Vector())
    if p.y<.24 and abs(p.x)<.28:result=result.lerp(torso,smooth((p.y+.02)/.16))
    return result
for p in source_points:
    sw=candidates(p);q=fit(p,sw)
    fitted.append(q);skin.append([(s['index'],w) for s,w in sw])

roster=['Walker','Runner','Brute','Spitter','Bloater','Warlord','Swordsman','Bulwark']
all_exports=[];collection=None;objects=[];kind='';seed=0
def add_mesh(name,points,faces,mat,weights=None,colors=None,uvs=None):
    data=bpy.data.meshes.new(name);data.from_pydata([B(p) for p in points],[],faces);data.update()
    o=bpy.data.objects.new(name,data);collection.objects.link(o);objects.append(o);data.materials.append(materials[mat]);o['surface']=mat
    bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
    for f in data.polygons:f.use_smooth=True
    if weights:
        for j,n in enumerate(names):o.vertex_groups.new(name=n)
        for i,ws in enumerate(weights):
            for b,w in ws:o.vertex_groups[b].add([i],w,'REPLACE')
    attr=data.color_attributes.new(name='Decay',type='FLOAT_COLOR',domain='POINT')
    for i,c in enumerate(attr.data):
        value=colors[i] if colors else (1,1,1)
        c.color=tuple(value) if len(value)==4 else (*value,1)
    layer=data.uv_layers.new(name='Surface UV')
    for f in data.polygons:
        for k,l in enumerate(f.loop_indices):
            v=points[data.loops[l].vertex_index]
            # Recalculating outward normals can reverse a reflected hand face.
            # Keep UVs attached to vertices, rather than to the old corner order.
            corner=faces[f.index].index(data.loops[l].vertex_index)
            layer.data[l].uv=uvs[f.index][corner] if uvs else (v[0]*2,v[1]*2)
    return o
def rigid(n,points,faces,mat,bone,color=(1,1,1)):
    return add_mesh(n,points,faces,mat,[[(jid(bone),1)] for _ in points],[color]*len(points))
def ellipsoid(n,c,r,mat,bone,seg=24,rings=14,color=(1,1,1)):
    c=Vector(c);v=[];f=[]
    for j in range(rings+1):
        t=math.pi*j/rings
        for k in range(seg):
            a=math.tau*k/seg;v.append(c+Vector((r[0]*math.sin(t)*math.cos(a),r[1]*math.cos(t),r[2]*math.sin(t)*math.sin(a))))
    for j in range(rings):
        for k in range(seg):a=j*seg+k;b=j*seg+(k+1)%seg;f.append((a,b,b+seg,a+seg))
    return rigid(n,v,f,mat,bone,color)
def sweep(n,path,r,mat,bone,sides=10):
    path=list(map(Vector,path));v=[];f=[]
    for j,p in enumerate(path):
        d=path[min(j+1,len(path)-1)]-path[max(0,j-1)];y=d.normalized();x=y.cross(Vector((0,0,1))).normalized()
        if x.length<.1:x=Vector((1,0,0))
        z=x.cross(y);rr=r(j/(len(path)-1)) if callable(r) else r
        for k in range(sides):a=math.tau*k/sides;v.append(p+(x*math.cos(a)+z*math.sin(a))*rr)
    for j in range(len(path)-1):
        for k in range(sides):a=j*sides+k;b=j*sides+(k+1)%sides;f.append((a,b,b+sides,a+sides))
    f +=[tuple(reversed(range(sides))),tuple((len(path)-1)*sides+k for k in range(sides))]
    return rigid(n,v,f,mat,bone)
def shell(n,c,rx,rz,y0,y1,mat,bone,arc=math.tau,phase=0,rows=9,cols=56,ragged=False):
    c=Vector(c);v=[];f=[]
    for j in range(rows+1):
        t=j/rows;y=y0+(y1-y0)*t
        for k in range(cols+1):
            a=phase+arc*k/cols;fold=(.0045*math.sin(a*13+seed)+.002*math.sin(a*27))*(.4+.6*t)
            scale=1+.04*math.sin(t*math.pi)
            hem=(math.sin(a*7+seed)*.028+math.cos(a*13)*.016)*t**9 if ragged else 0
            v.append(c+Vector((math.sin(a)*(rx*scale+fold),y+hem,math.cos(a)*(rz*scale+fold))))
    for j in range(rows):
        for k in range(cols):a=j*(cols+1)+k;f.append((a,a+1,a+cols+2,a+cols+1))
    o=rigid(n,v,f,mat,bone)
    sol=o.modifiers.new('Real sewn / forged thickness','SOLIDIFY');sol.thickness=.003 if mat==1 else .006
    return o

def body_shape(p,index):
    return sculpt_body(p,index)

def fitted_surface(name,mat,mask,offset=.012,rigid_bone=None):
    faces=[f for f in source_faces if not any(hand_removed[i] for i in f) and mask(sum((body_points[i] for i in f),Vector())/len(f),f) and not (mat==1 and torn_at_injury(sum((body_points[i] for i in f),Vector())/len(f)))]
    ids=sorted({i for f in faces for i in f});remap={i:j for j,i in enumerate(ids)}
    points=[body_points[i]+body_normals[i]*(offset+(.003*math.sin(body_points[i].y*67+body_points[i].x*90) if mat==1 else 0)) for i in ids]
    ws=[[(jid(rigid_bone),1)] if rigid_bone else body_weights[i] for i in ids]
    o=add_mesh(name,points,[[remap[i] for i in f] for f in faces],mat,ws)
    sol=o.modifiers.new('Real folded edge thickness','SOLIDIFY');sol.thickness=.006 if mat==2 else .003
    return o

def jerkin_region(p,f):
    if not torso_region(p,f):
        # Torn half sleeves follow the upper-arm weights, including the shoulder.
        for side in ('L','R'):
            a=J(side+'ArmUpperarm');b=J(side+'ArmForearm');axis=b-a
            t=(p-a).dot(axis)/axis.length_squared
            if p.x*a.x>0 and -.12<t<(.64 if kind=='Walker' else .33):return True
        return False
    if not 1.005+.014*math.sin(p.x*64+seed)<p.y<1.488:return False
    if p.y>1.458-.018*math.exp(-(p.x/.061)**4) and abs(p.x)<.079:return False
    if kind=='Runner' and p.z>.025 and p.x<-.055 and 1.12<p.y<1.39:return False
    tear=((p.x-.085)/.044)**2+((p.y-1.215)/.061)**2
    return not (p.z>.04 and tear<1+.12*math.sin(p.y*137))

def torso_region(p,f):
    return all(not any('Arm' in names[b] and w>.4 for b,w in body_weights[i]) for i in f)

def depth(x,y):
    points=[p for p in body_points if abs(p.x-x)<.018 and abs(p.y-y)<.018]
    return max((p.z for p in points),default=.10)

# Anatomical power grips: retain the native palm, nail beds, MCP/PIP/DIP loops.
# Finger transforms are continuous and stop at the wrist; forearms stay intact.
def closed_hand(points,weights,side,armed=True):
    ss='r' if side=='L' else 'l';sgn=-1 if side=='L' else 1
    wrist=M(ss+'-hand');dist=(M(ss+'-finger-3-1')-wrist).normalized()
    across=(M(ss+'-finger-2-1')-M(ss+'-finger-5-1')).normalized();across=(across-dist*across.dot(dist)).normalized()
    dorsal=across.cross(dist)*(-1 if ss=='r' else 1)
    target=J(side+'ArmPalm');td=(target-J(side+'ArmForearm')).normalized()
    ta=Vector((0,-1,0));ta=(ta-td*ta.dot(td)).normalized();tn=ta.cross(td)
    # Physical palm axes are explicitly mirrored once for Unity's handedness.
    dorsal_target=tn*(1 if side=='R' else -1)
    def C(v):d=v-wrist;return Vector((d.dot(across),d.dot(dist),d.dot(dorsal)))
    def place(v):return target+ta*v.x+td*v.y+dorsal_target*v.z
    chains={f:[C(M(ss+'-finger-%d-%d'%(f,j))) for j in range(1,5)] for f in range(1,6)}
    transforms=[]
    for f,chain in chains.items():
        pose=[chain[0]];angle=0
        for j,ang in enumerate(((48,78,49) if armed else (18,32,22)) if f!=1 else (22,35,25)):
            angle+=math.radians(ang);length=(chain[j+1]-chain[j]).length
            direction=Vector((0,math.cos(angle),-math.sin(angle))) if f!=1 else unit((-.45,.5,-.65))
            pose.append(pose[-1]+direction*length)
        for j in range(3):
            rot=frame(chain[j],chain[j+1]);rot2=frame(pose[j],pose[j+1]);R=rot2@rot.inverted()
            transforms.append((f,j,chain[j],chain[j+1],R,pose[j]))
    for i,p in enumerate(source_points):
        if p.x*sgn<.32 or p.y<.10 or p.y>.35:continue
        c=C(p)
        if c.y<-.025:continue
        cs=sorted((near(c,a,b)[0],k) for k,(f,j,a,b,R,t) in enumerate(transforms))[:2]
        ds=[math.exp(-(d-cs[0][0])/.005) for d,k in cs];total=sum(ds)
        out=Vector()
        for (d,k),w in zip(cs,ds):f,j,a,b,R,t=transforms[k];out+=(t+R@(c-a))*w/total
        influence=smooth((c.y-.018)/.035);wrist_blend=smooth((c.y+.025)/.030)
        points[i]=points[i].lerp(place(c.lerp(out,influence)),wrist_blend)
        ws={b:w*(1-wrist_blend) for b,w in weights[i]};ws[jid(side+'ArmPalm')]=ws.get(jid(side+'ArmPalm'),0)+wrist_blend
        weights[i]=sorted(ws.items(),key=lambda bw:-bw[1])[:4];total=sum(w for b,w in weights[i]);weights[i]=[(b,w/total) for b,w in weights[i]]
    return dict(side=side,position=vec(place(Vector((0,.069,-.026)))),along=vec(ta),normal=vec(tn if side=='R' else -tn),distal=vec(td))

def boot_last(side):
    s=-1 if side=='L' else 1;foot='LLegFoot' if side=='L' else 'RFoot';toe='LLegDigit11' if side=='L' else 'RDigit11'
    origin=J(foot);forward=J(toe)-origin;forward.y=0;forward.normalize();right=Vector((0,1,0)).cross(forward)
    cloud=[Vector(((p-origin).dot(right),(p-origin).dot(forward),p.y)) for p in body_points if s*p.x>.025 and p.y<.125]
    lo=min(p.y for p in cloud)-.010;hi=max(p.y for p in cloud)+.015;bottom=min(p.z for p in cloud)-.003
    # A shoe last encloses all toes in one toe box, with a broad heel and flat sole.
    profiles=[]
    for row in range(19):
        t=row/18;z=lo+(hi-lo)*t;nearby=[p for p in cloud if abs(p.y-z)<.022]
        if not nearby:nearby=sorted(cloud,key=lambda p:abs(p.y-z))[:15]
        center=(max(p.x for p in nearby)+min(p.x for p in nearby))*.5
        width=max(.018,(max(p.x for p in nearby)-min(p.x for p in nearby))*.5+.008)
        taper=max(.12,min(1,math.sin(math.pi*t)*3.2));width*=taper
        top=max(bottom+.025,max(p.z for p in nearby)+.010)
        profiles.append((z,center,width,top))
    def shoe(name,sole=False):
        vertices=[];faces=[];cols=24
        for z,center,width,top in profiles:
            for k in range(cols):
                a=math.tau*k/cols;arch=max(0,math.sin(a))**.68
                y=bottom+(.009 if sole else .004)+(top-bottom-.004)*arch if not sole else bottom-.009+(.012*max(0,math.sin(a)))
                vertices.append(origin+forward*z+right*(center+width*math.cos(a))+Vector((0,y-origin.y,0)))
        for row in range(18):
            for k in range(cols):a=row*cols+k;b=row*cols+(k+1)%cols;faces.append((a,b,b+cols,a+cols))
        faces.extend([tuple(reversed(range(cols))),tuple(18*cols+k for k in range(cols))])
        rigid(name,vertices,faces,3,foot,(.58,.48,.38) if sole else (1,1,1))
    shoe('Closed leather toe box and heel');shoe('Worn welted leather sole',True)

def equipment(grips):
    armored=kind in ('Swordsman','Bulwark','Warlord','Brute')
    if armored:
        bulk=1.27 if kind=='Brute' else 1.13 if kind=='Warlord' else 1
        fitted_surface('Quilted collar and sleeves',1,lambda p,f:((1.40<p.y<1.51 and torso_region(p,f)) or all(any('ArmUpperarm' in names[b] and w>.25 for b,w in body_weights[i]) for i in f)),.010)
        # Trace actual anatomy: pectoral shaping, curved rib cage, waist taper
        # and open armholes. Never put a cylindrical shell over the torso.
        make_cuirass()
        chest=COMPOSITIONS[kind][0]
        shell('Broad leather sword belt',(0,0,-.02),.180*chest,.170*(.80+.20*chest),.990,1.045,3,'Spine2',rows=3,cols=48)
        for s in (-1,1):
            side='L' if s<0 else 'R';shoulder=J(side+'ArmUpperarm')
            for j in range(3):
                upper=J(side+'ArmUpperarm');fore=J(side+'ArmForearm');axis=(fore-upper);length=axis.length;axis.normalize()
                fitted_surface('Articulated fitted shoulder lame',2,lambda p,f,s=s,j=j,upper=upper,axis=axis,length=length,side=side:s*p.x>.12 and -.08+j*.14<(p-upper).dot(axis)/length<.13+j*.14 and all(any(names[b]=='Base Human'+side+'ArmUpperarm' and w>.25 for b,w in body_weights[i]) for i in f),.027+j*.002,side+'ArmUpperarm')
        # Thin overlapped fauld tabs follow their own thigh, leaving room for
        # flexion instead of sharing one rigid barrel with both legs.
        for s in (-1,1):
            thigh='LLegThigh' if s<0 else 'RThigh'
            fitted_surface('Fitted thigh fauld plate',2,lambda p,f,s=s:s*p.x>.025 and .83<p.y<.94 and p.z>.04,.023,thigh)
        if kind=='Warlord':
                verts=[];faces=[];ws=[]
                for j in range(17):
                    t=j/16
                    for k in range(25):
                        x=-.24+k/24*.48;y=1.43-t*.58+(math.sin(k*1.7)*.026)*t**7
                        z=-.205-.030*t+.014*math.sin(k/24*math.tau*5)
                        verts.append((x,y,z));w=smooth(t)
                        ws.append([(jid('Ribcage'),1-w),(jid('Pelvis'),w)])
                for j in range(16):
                    for k in range(24):a=j*25+k;faces.append((a,a+1,a+26,a+25))
                cape=add_mesh('Tattered crimson burial mantle',verts,faces,1,ws,[(.85,.25,.20)]*len(verts));cape.modifiers.new('Linen thickness','SOLIDIFY').thickness=.003
    # Garments are offset sections of the actual anatomical topology. The
    # fabric keeps its own thigh/calf weights and has a visibly tapered cut.
    make_breeches()
    for s in (-1,1):
        side='L' if s<0 else 'R';thigh='LLegThigh' if s<0 else 'RThigh';calf='LLegCalf' if s<0 else 'RCalf'
        if kind in ('Swordsman','Bulwark','Warlord'):
            fitted_surface('Fitted tapered iron greave',2,lambda p,f,s=s:s*p.x>.035 and .17<p.y<.45 and p.z>-.07,.014,calf)
            fitted_surface('Distressed fitted boot shaft',3,lambda p,f,s=s:s*p.x>.025 and .105<p.y<.24,.010)
            boot_last(side)
    # Ragged torso vest follows ribcage; each hanging tab has its own thigh skin.
    if not armored and kind!='Bloater':
        fitted_surface('Ragged sewn burial jerkin',1,jerkin_region,.015)
        fitted_surface('Skinned flat oxhide baldric',3,lambda p,f:torso_region(p,f) and p.z>.03 and abs(p.x)<.175 and abs(p.y-(1.25-p.x*1.25))<.012,.025)
    if kind=='Brute':
        for side in ('L','R'):
            fore=J(side+'ArmForearm');palm=J(side+'ArmPalm')
            fitted_surface('Anatomical siege bracer',2,lambda p,f,side=side:all(any(names[b]=='Base Human'+side+'ArmForearm' and w>.5 for b,w in body_weights[i]) for i in f),.016,side+'ArmForearm')
    if kind in ('Swordsman','Bulwark','Warlord'):
        mount=next(g for g in grips if g['side']=='R');c=Vector(tuple(mount['position'][k] for k in 'xyz'))
        along=Vector(tuple(mount['along'][k] for k in 'xyz'));forward=Vector(tuple(mount['normal'][k] for k in 'xyz'))
        # Weapon axes are local to an actual closed palm, not the fingertips.
        blade=along;edge=unit(blade.cross(forward));root=c+blade*.075;tip=c+blade*(.79 if kind=='Warlord' else .72)
        mount['bladeTip']=vec(tip)
        if kind=='Bulwark':
            g=next(g for g in grips if g['side']=='L');c=Vector(tuple(g['position'][k] for k in 'xyz'))
            n=Vector(tuple(g['normal'][k] for k in 'xyz'));up=Vector(tuple(g['along'][k] for k in 'xyz'));right=up.cross(n).normalized()
            # Bowed heater with an elegant arched top, separated rim and planks.
            center=c+n*.055;verts=[center+n*.055];outline=[]
            for k in range(17):
                x=-.29+k/16*.58;outline.append((x,.35+.065*(1-(x/.29)**2)))
            outline +=[(.29,.10),(.22,-.17),(.12,-.35),(0,-.47),(-.12,-.35),(-.22,-.17),(-.29,.10)]
            for x,y in outline:verts.append(center+right*x+up*y+n*(.055*(1-(x/.31)**2)))
            fs=[(0,i+1,(i+1)%len(outline)+1) for i in range(len(outline))]
            o=rigid('Bowed oak heater shield',verts,fs,5,'LArmPalm');sol=o.modifiers.new('Solid oak boards','SOLIDIFY');sol.thickness=.022
            border=[verts[i+1]+n*.012 for i in range(len(outline))];sweep('Rolled iron arched shield rim',border+[border[0]],.014,2,'LArmPalm',12)
            for x in (-.20,-.10,0,.10,.20):
                sweep('Shield board seam',[center+right*x+up*y+n*(.059*(1-(x/.31)**2)) for y in np.linspace(-.28+abs(x)*1.3,.36,10)],.0024,3,'LArmPalm',6)
            ellipsoid('Shield boss',center+n*.070,(.072,.072,.034),2,'LArmPalm',24,14)
            sweep('Shield rear enarme',[c-up*.055,c+up*.055],.018,3,'LArmPalm',12)

def decay_color(p):
    # Low-frequency mottling + sparse vessels, eye sockets, lips and open sores.
    noise=math.sin(p.x*48+math.sin(p.y*31))*math.sin(p.z*59+p.y*23)
    tone=.94+noise*.14
    base=np.array((tone,tone,tone))
    if kind=='Runner':base*=np.array((1.02,.79,.77))
    elif kind=='Spitter':base*=np.array((.90,1.04,.73))
    elif kind=='Bloater':base*=np.array((1.04,.99,.73))
    elif kind=='Warlord':base*=np.array((.97,.89,.85))
    socket=math.exp(-((abs(p.x)-.033)/.025)**2-((p.y-1.659)/.021)**2)*smooth((p.z-.03)/.05)
    mouth=math.exp(-(p.x/.042)**4-((p.y-1.596)/.009)**2)*smooth((p.z-.05)/.05)
    base*=1-.78*socket-.65*mouth
    if 1.55<p.y<1.72 and p.z>.045:
        front=smooth((p.z-.045)/.04)
        cheek=math.exp(-((abs(p.x)-.051)/.022)**2-((p.y-1.614)/.022)**2)*front
        orbit=math.exp(-((abs(p.x)-.031)/.029)**4-((p.y-1.652)/.018)**2)*front
        crease_x=.019+(1.624-p.y)*.52
        fold=math.exp(-((abs(p.x)-crease_x)/.005)**2-((p.y-1.607)/.021)**4)*front
        base*=np.array((1-.32*cheek,1-.44*cheek,1-.25*cheek))*(1-.50*orbit-.36*fold)
    # Deliberately bounded lesions, not overall noisy red skin.
    lesions=[(-.10,1.29,.075,.031),(.075,1.11,.10,.029),(-.049,1.647,.092,.022),(.035,1.60,.105,.015),(.14,.44,.025,.020)]
    for x,y,z,r in lesions:
        d=math.hypot(p.x-x,p.y-y)/r
        w=math.exp(-d**4)*.87*smooth((p.z-.015)/.06);base=base*(1-w)+np.array((.48,.12,.085))*w
    return tuple(np.clip(base,.035,1.15))

exec((ROOT/'Tools/ArtPipeline/revenant_neck.py').read_text(),globals())

def export_objects(label,objects,grips):
    vertices=[];normals=[];colors=[];uvs=[];tiles=[];indices=[];bw=[]
    deps=bpy.context.evaluated_depsgraph_get()
    neck_joints=tuple(jid(name) for name in ('Ribcage','Neck','Head'))
    for o in objects:
        fix_neck=label in ('Brute','Brute_LOD1','Brute_LOD2') and o.name=='Brute continuous anatomical body'
        ev=o.evaluated_get(deps);d=ev.to_mesh();d.calc_loop_triangles()
        remap={};attrs=d.color_attributes.get('Decay');uvl=d.uv_layers.active.data
        for tri in d.loop_triangles:
            corners=[]
            for li in tri.loops:
                vi=d.loops[li].vertex_index;v=d.vertices[vi];uv0=uvl[li].uv
                key=(vi,round(uv0.x,6),round(uv0.y,6))
                if key not in remap:
                    remap[key]=len(vertices);vertices.append(vec(U(o.matrix_world@v.co)));normals.append(vec(U(v.normal)))
                    colors.append(list(attrs.data[vi].color) if attrs else [1,1,1,1]);uvs.append(dict(x=float(uv0.x),y=float(uv0.y)));tiles.append(o['surface'])
                    ws=[(g.group,g.weight) for g in v.groups]
                    if fix_neck:ws=brute_neck_weights(U(o.matrix_world@v.co),ws,neck_joints)
                    ws=sorted(ws,key=lambda pair:-pair[1])[:4]
                    if not ws:ws=[(jid('Pelvis'),1)]
                    total=sum(w for b,w in ws);bw.append([(b,w/total) for b,w in ws]+[(0,0)]*(4-len(ws)))
                corners.append(remap[key])
            indices.extend(corners)
        ev.to_mesh_clear()
    print('REVENANT',label,len(vertices),'vertices',len(indices)//3,'triangles',flush=True)
    return dict(name=label,vertices=vertices,normals=normals,uv=uvs,colors=[dict(r=c[0],g=c[1],b=c[2],a=c[3]) for c in colors],tiles=tiles,triangles=indices,
        skinIndices=[b for ws in bw for b,w in ws],skinWeights=[round(w,7) for ws in bw for b,w in ws],grips=grips)

exec((ROOT/'Tools/ArtPipeline/revenant_hands.py').read_text(),globals())
exec((ROOT/'Tools/ArtPipeline/revenant_anatomy.py').read_text(),globals())
exec((ROOT/'Tools/ArtPipeline/revenant_dread.py').read_text(),globals())
for seed,kind in enumerate(roster):
    collection=bpy.data.collections.new('Revenants / '+kind);bpy.context.scene.collection.children.link(collection);objects=[]
    pts=[body_shape(p,i) for i,p in enumerate(fitted)];weights=[ws.copy() for ws in skin];grips=[]
    fit_wrist_seams(pts)
    mouth_faces=fit_lips(pts)
    prepare_wounds(pts,weights)
    if kind=='Walker':texture_points=[p.copy() for p in pts]
    body_points=pts;body_weights=weights;body_normals=[Vector() for _ in pts]
    for f in source_faces:
        a,b,c=(pts[i] for i in f[:3]);n=(b-a).cross(c-a)
        for i in f:body_normals[i]+=n
    body_normals=[n.normalized() for n in body_normals]
    hand_grips=make_hands()
    grips=hand_grips
    # Clouded anatomical eyeballs sit inside the recessed sockets.
    for side in ('L','R'):
        p=M(('r' if side=='L' else 'l')+'-eye');sw=candidates(p)
        e=fit(p,sw)
        ellipsoid('Clouded recessed eye',e-Vector((0,0,.0085)),(.012,.0112,.008),7,'Head',24,14)
        if side=='R' or kind in ('Swordsman','Bulwark'):
            ellipsoid('Contracted dark pupil',e-Vector((0,0,.0007)),(.0020,.0026,.0006),3,'Head',16,10,(.13,.16,.12))
    equipment(grips)
    make_dread_face()
    make_wounds()
    # Remove skin strictly inside the fitted clothing. The safety margin at
    # each opening retains a continuous joint but prevents coincident surfaces.
    def covered(p,f):
        if any(hand_removed[i] for i in f):return True
        if tuple(f) in mouth_faces:return True
        if any(injury_radius(p,w)<.90 for w in injuries):return True
        arm=any('Arm' in names[b] and w>.4 for i in f for b,w in weights[i])
        armored=kind in ('Brute','Warlord','Swordsman','Bulwark')
        if not arm and PANT_HEMS[kind]+.025<p.y<.94:return True
        if armored and torso_region(p,f) and 1.045<p.y<1.415:return True
        if armored and torso_region(p,f) and .945<p.y<1.015:return True
        if kind in ('Warlord','Swordsman','Bulwark') and p.y<.22:return True
        if not armored and kind!='Bloater' and jerkin_region(p,f):return True
        return False
    kept=[i for i,f in enumerate(source_faces) if not covered(sum((pts[j] for j in f),Vector())/len(f),f)]
    body=add_mesh(kind+' continuous anatomical body',pts,[source_faces[i] for i in kept],0,weights,[decay_color(p) for p in pts],[body_uv[i] for i in kept])
    # An actual editable Blender deformation rig per archetype.
    arm=bpy.data.armatures.new(kind+' deformation rig');ao=bpy.data.objects.new(kind+' rig',arm);collection.objects.link(ao)
    bpy.context.view_layer.objects.active=ao;ao.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    for i,b in enumerate(rig):
        eb=arm.edit_bones.new(b['name']);eb.head=B(joints[i]);children=[k for k,c in enumerate(rig) if c['parent']==i]
        tail=joints[children[0]] if children else joints[i]+Vector((0,.035,0))
        if (tail-joints[i]).length<.005:tail=joints[i]+Vector((0,.035,0))
        eb.tail=B(tail)
    for i,b in enumerate(rig):
        if b['parent']>=0:arm.edit_bones[i].parent=arm.edit_bones[b['parent']]
    bpy.ops.object.mode_set(mode='OBJECT');ao.select_set(False)
    fit_crowd_budget()
    all_exports.append(export_objects(kind,objects,grips))
    # Distance meshes retain materials, UVs and skin groups. Decimate separate
    # pieces so armor cannot collapse into skin and finger webs stay separate.
    for lod,ratio in [(1,.42),(2,.16)]:
        for o in objects:
            mod=o.modifiers.get('Crowd distance LOD') or o.modifiers.new('Crowd distance LOD','DECIMATE');mod.ratio=ratio
        bpy.context.view_layer.update();all_exports.append(export_objects(kind+'_LOD'+str(lod),objects,[]))
    for o in objects:
        o.modifiers.remove(o.modifiers['Crowd distance LOD'])
        mod=o.modifiers.new('Animation skeleton','ARMATURE');mod.object=ao;o.parent=ao
    export_dread_equipment(grips,ao)
    exec((ROOT/'Tools/ArtPipeline/revenant_hair.py').read_text(),globals())

exec((ROOT/'Tools/ArtPipeline/revenant_skin.py').read_text(),globals())
(OUT/'meshes.json').write_text(json.dumps(dict(parts=all_exports),separators=(',',':')))
# Store a tidy eight-character lineup in the source, each with its full rig.
for i,k in enumerate(roster):
    ao=bpy.data.objects[k+' rig'];ao.location.x=(i-3.5)*1.4
for im in bpy.data.images:
    if im.filepath:im.pack()
world=bpy.context.scene.world or bpy.data.worlds.new('Revenant studio');bpy.context.scene.world=world;world.color=(.13,.13,.13)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Revenants.blend'),compress=True)
print('REVENANTS source and exports complete',flush=True)
