"""Forged gladiator-inspired helmet and a modest increase in stature.

Authored at the shared skeleton's native scale. A uniform stature multiplier is
exported separately so Unity and the editable Blender rig grow together.
"""
STATURE_SCALE=1.10
current='Body';bone='Head'
for obj in list(groups['Body']):
    p=U(obj.matrix_world.translation)
    old=obj.name.startswith(('Forged four panel conical helmet','Weathered iron helmet brow','Rolled steel helmet edge'))
    old|=obj.name.startswith('Peened rivet') and obj['bone']==bone_names.index('Bip001 Head') and p.y>1.40
    if old:
        groups['Body'].remove(obj);bpy.data.objects.remove(obj,do_unlink=True)


def helmet_piece(name,fn,nu,nv,mat=0,thickness=.0025):
    return region(patch(name,fn,nu,nv,mat,thickness,0),'head')


# A broad low dome with a forged medial ridge replaces the pointed cone.
def crown(u,v):
    a=u*math.tau
    phi=v*math.pi/2
    w=.1025*math.cos(phi)
    d=.113*math.cos(phi)
    # Raised sagittal ridge strengthens the long crown profile.
    ridge=.006*math.exp(-(math.sin(a)/.20)**2)*math.sin(phi*2)**2
    return Vector((math.sin(a)*w,1.405+.137*math.sin(phi)+ridge,-.050+math.cos(a)*d))
helmet_piece('Gladiator ridged forged skull',crown,80,40)

# The brow slopes down at the middle and extends over the eye slit in a strong
# shallow V. Its lower surface is genuinely recessed over the eyes.
def brow(u,v):
    a=(u-.5)*3.15
    top=1.421+.008*abs(math.sin(a))
    bottom=1.394+.009*abs(math.sin(a))
    lift=.010+.009*math.sin(v*math.pi)
    return Vector((math.sin(a)*(.103+lift),top+(bottom-top)*v,-.049+math.cos(a)*(.115+lift)))
helmet_piece('V shaped overhanging gladiator brow',brow,64,7,0,.0035)
region(binding('Brow rubbed forged edge',[brow(k/80,1)+Vector((0,0,.001)) for k in range(81)],12,.0018),'head')

# One curved, closed-face visor with real openings. Long cheek planes converge
# on a protected chin, while a defined nasal spine gives the mask its expression.
def visor(u,v):
    a=(u-.5)*2.98
    ytop=1.402+.004*abs(math.sin(a))
    ybottom=1.270+.025*abs(math.sin(a))**1.5
    y=ytop+(ybottom-ytop)*v
    w=.099-.009*v
    d=.132-.013*v
    nose=.018*math.exp(-(a/.16)**2)*math.sin(v*math.pi)**.7
    cheek=.006*math.exp(-((abs(a)-.72)/.27)**2)*math.sin(v*math.pi)
    return Vector((math.sin(a)*w,y,-.047+math.cos(a)*d+nose+cheek))
mask=helmet_piece('Gladiator perforated face and cheek guard',visor,144,100,0,.003)
import bmesh
bm=bmesh.new();bm.from_mesh(mask.data)
cuts=[]
for face in bm.faces:
    p=U(face.calc_center_median());x=abs(p.x)
    # Eyes remain in one continuous narrow band on either side of the nasal rib.
    slit_low=1.370+.004*(x/.060)
    slit_high=1.387+.003*(x/.060)
    eye=.013<x<.065 and slit_low<p.y<slit_high
    # Small staggered vertical breathing apertures, opened through actual steel.
    vent=False
    for column in range(5):
        cx=.017+column*.012
        for row in range(2):
            cy=1.329-row*.023+column*.0015
            if abs(x-cx)<.0020 and abs(p.y-cy)<.0075:vent=True
    if eye or vent:cuts.append(face)
bmesh.ops.delete(bm,geom=cuts,context='FACES');bm.to_mesh(mask.data);bm.free()

# Thick angular cheek framing and a restrained polished nasal keel catch light
# against the blackened shell without a painted-on face or decoration.
for sign in (-1,1):
    side=.5+sign*.39
    region(binding('Swept gladiator cheek edge',[visor(side,v)+Vector((sign*.001,0,.001))
           for v in np.linspace(.08,.99,50)],13,.0027),'head')
    # Subtle reinforcement below the eye, tapering toward the jaw.
    def cheek_plate(u,v,sign=sign):
        a=sign*(.61+u*.32)
        raw=visor(.5+a/2.98,.30+.62*v)
        raw.z+=.0025*math.sin(u*math.pi)
        return raw
    helmet_piece('Swept tapered cheek reinforcement',cheek_plate,12,24,13,.0018)
region(sweep('Forged nasal keel',[visor(.5,v)+Vector((0,0,.002)) for v in np.linspace(.03,.79,44)],
             lambda t:.0022-.001*t,12,7,48,flatten=.45),'head')
region(binding('Rolled gladiator chin edge',[visor(k/90,.99) for k in range(91)],13,.0021),'head')

# Flared nape plate continues the skull over the mail without clipping the neck.
def nape(u,v):
    a=1.34+u*(math.tau-2.68)
    r=.103+.031*v
    return Vector((math.sin(a)*r,1.411-.074*v-.007*math.sin(u*math.pi)*v,
                   -.051+math.cos(a)*(.113+.038*v)))
helmet_piece('Flared gladiator nape protection',nape,64,20,0,.0025)
region(binding('Nape rolled perimeter',[nape(k/80,1) for k in range(81)],13,.0021),'head')

# A swept, solid steel crest gives a recognizable gladiator silhouette. It is a
# low forged blade with a broad foot, rather than an oversized feathered plume.
def crest(u,v):
    z=.080-.250*v
    meridian=max(-1,min(1,(z+.050)/.113))
    base=1.405+.137*math.sqrt(max(0,1-meridian*meridian))+.001
    height=.030+.069*math.sin(v*math.pi)**.8
    return Vector(((u-.5)*(.014-.007*math.sin(v*math.pi)),base+height*math.sin(u*math.pi),z))
helmet_piece('Swept steel gladiator crest',crest,16,64,0,.004)
region(binding('Crest worn steel spine',[crest(.5,v)+Vector((0,.001,0)) for v in np.linspace(.02,.98,80)],12,.0017),'head')
for sign in (-1,1):
    region(binding('Crest forged mounting foot',[crest(.04 if sign<0 else .96,v)+Vector((sign*.004,-.004,0))
                    for v in np.linspace(.02,.98,64)],13,.004),'head')

# Real flush rivets at brow pivots and the crest foot.
for sign in (-1,1):
    for y,z in ((1.405,-.016),(1.368,-.038)):
        region(ellipsoid('Gladiator visor hinge', (sign*.102,y,z),(.0035,.006,.006),12,16,8),'head')
    for z in (.040,-.072,-.140):
        p=crest(0 if sign<0 else 1,(.080-z)/.250)+Vector((sign*.003,-.0005,0))
        region(ellipsoid('Crest mounting peen',p,(.0022,.0020,.0022),13,12,6),'head')

print('MARCHWARDEN gladiator helmet: narrow eye slits, forged grille, swept crest; stature scale',STATURE_SCALE,flush=True)
