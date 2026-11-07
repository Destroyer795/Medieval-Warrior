"""Entirely metal heater shield: dished plate, forged rim, iron rivets/enarmes.
Canonical X is vertical, Y is across the shield and +Z faces the opponent.
"""
for obj in groups['Shield']:bpy.data.objects.remove(obj,do_unlink=True)
groups['Shield']=[];current='Shield'
scars=[(.24,.10,.29,.43),(.61,.08,.63,.40),(.45,.30,.73,.58),(.73,.49,.39,.76),(.38,.58,.40,.85)]
def halfwidth(v):
    # Broad squared shoulders taper continuously into a rounded heater point.
    return catmull([(0,.238),(.22,.235),(.50,.190),(.78,.108),(1,.005)],v)[1]
def face(u,v):
    width=halfwidth(v);x=.285-.675*v;y=(u*2-1)*width
    z=.026+.035*(1-(u*2-1)**2)+.006*math.sin(v*math.pi)
    for u0,v0,u1,v1 in scars:
        du,dv=u1-u0,v1-v0
        t=max(0,min(1,((u-u0)*du+(v-v0)*dv)/(du*du+dv*dv)))
        distance=math.hypot(u-u0-t*du,v-v0-t*dv)
        z-=.0034*math.exp(-(distance/.009)**2)*math.sin(t*math.pi)
    return Vector((x,y,z))
patch('Dished forged steel heater plate',face,80,96,12,.007,0,(1,1))
border=[face(i/60,0) for i in range(61)]
border += [face(1,i/80) for i in range(1,81)]
border += [face(0,1-i/80) for i in range(1,81)]
border.append(border[0])
sweep('Raised reinforced iron heater border',[p+Vector((0,0,.008)) for p in border],.012,13,12,len(border)-1,flatten=.7)
sweep('Rubbed bright crown of forged rim',[p+Vector((0,0,.017)) for p in border],.0020,12,8,len(border)-1,flatten=.45)
# Inner reinforcement follows the plate, with a bright rubbed edge.
for edge in (0,1):
    seam('Inner forged rim shoulder',lambda t,edge=edge:face(.035 if edge==0 else .965,t*.97)+Vector((0,0,.008)),80,12)
for u,v in [(u,0.025) for u in (.08,.25,.42,.58,.75,.92)]+[(u,v) for u in (.035,.965) for v in (.15,.30,.45,.60,.75,.88)]:
    p=face(u,v)+Vector((0,0,.009))
    ellipsoid('Heavy peened iron rivet',p,(.0095,.0095,.0055),13,20,12)
# Dented strike scars are actual recessed/raised narrow V sections, backed by
# the uniquely mapped scratch normals and roughness, not painted white stripes.
for k,(u0,v0,u1,v1) in enumerate(scars):
    points=[face(u0+(u1-u0)*t,v0+(v1-v0)*t)+Vector((0,0,.001)) for t in np.linspace(0,1,30)]
    sweep('Deep combat gouge',points,lambda t:.00065+.0008*math.sin(t*math.pi),13,6,30,flatten=.25)
    sweep('Bruised gouge lip',[p+Vector((0,.0015,.001)) for p in points],.0007,12,6,30,flatten=.35)
for x in (-.085,.085):
    sweep('Forged iron forearm enarme',[(x,-.062,.022),(x,-.041,-.085),(x,.041,-.085),(x,.062,.022)],.009,13,14,40,flatten=.6)
sweep('Solid iron palm grip',[(-.085,-.025,-.067),(0,-.025,-.076),(.085,-.025,-.067)],.012,13,16,40)
