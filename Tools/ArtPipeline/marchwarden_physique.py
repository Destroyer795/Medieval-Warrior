"""Athletic silhouette and fitted clothing, applied before UV packing/baking.

The skeleton and grip locations stay fixed. Garments follow the same continuous
chest/waist warp so their plates, bindings and rivets remain fitted together.
"""
def smoothstep(t):
    t=max(0,min(1,t));return t*t*(3-2*t)

for obj in list(groups['Body']):
    if obj.name.startswith(('Soft cuirass shoulder bridge','Shoulder bridge stitched edge')):
        groups['Body'].remove(obj);bpy.data.objects.remove(obj,do_unlink=True);continue
    joint_name=bone_names[obj['bone']]
    for vertex in obj.data.vertices:
        p=U(obj.matrix_world@vertex.co)
        if joint_name.endswith('Spine'):
            chest=math.exp(-((p.y-1.075)/.16)**2)
            p.x*=.94
            p.z=-.047+(p.z+.047)*.87
        elif joint_name.endswith('Pelvis'):
            p.x*=.98;p.z=-.043+(p.z+.043)*.95
            if 'tunic' in obj.name.lower():
                # Broad sewn folds read at game distance; fine weave belongs in maps.
                angle=math.atan2(p.x,p.z+.043)
                f=.009*math.sin(angle*11+p.y*5)*smoothstep((.82-p.y)/.24)
                p.x+=math.sin(angle)*f;p.z+=math.cos(angle)*f
        elif any(joint_name.endswith(side+' Thigh') for side in ('L','R')):
            label='L' if ' L ' in joint_name else 'R'
            a,b=joint(label+' Thigh'),joint(label+' Calf')
            axis=(b-a).normalized();t=max(0,min(1,(p-a).dot(axis)/(b-a).length))
            c=a.lerp(b,t);p=c+(p-c)*1.02
        elif any(joint_name.endswith(side+' Forearm') for side in ('L','R')) and obj['material']!=8:
            label='L' if ' L ' in joint_name else 'R'
            a,b=joint(label+' Forearm'),joint(label+' Hand');axis=(b-a).normalized()
            c=a+axis*(p-a).dot(axis);p=c+(p-c)*.98
        elif any(joint_name.endswith(side+' Calf') for side in ('L','R')):
            label='L' if ' L ' in joint_name else 'R'
            a,b=joint(label+' Calf'),joint(label+' Foot');axis=(b-a).normalized()
            c=a+axis*(p-a).dot(axis);p=c+(p-c)*1.02
        vertex.co=obj.matrix_world.inverted()@B(p)

# Replace the disconnected ankle/foot assembly with one continuous boot upper.
for obj in list(groups['Body']):
    if obj.name.startswith(('Supple high leather boot','Joined boot ankle','Continuous leather boot foot','Solid boot sole','Stitched boot welt','Boot folded top')):
        groups['Body'].remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
current='Body'
for label in ('L','R'):
    ankle=joint(label+' Foot');knee=joint(label+' Calf');bone=label+' Calf'
    # Closed, rounded toe -> vamp -> flexible ankle -> calf. A single silhouette.
    profile=[(.012,.045,.112,.030),(.027,.057,.128,.031),(.055,.056,.120,.029),
             (.085,.046,.080,.008),(.116,.041,.049,-.004),(.16,.044,.051,-.001),
             (.23,.052,.060,.010),(.30,.061,.067,.021),(.365,.061,.067,.027)]
    o=body_loft('Continuous fitted leather boot',[(y,w,d,ankle.z+z) for y,w,d,z in profile],3,64,48)
    o.location.x=ankle.x;o['ankleSide']=label
    for vertex in o.data.vertices:
        p=U(vertex.co);p.x+=(knee.x-ankle.x)*smoothstep((p.y-.12)/.22);vertex.co=B(p)
    # Instep creases and stitched calf seam share the same flexible weights.
    for k in range(3):
        y=.10+k*.018
        o=binding('Soft boot instep crease',[(ankle.x-.032,y,ankle.z+.041),(ankle.x,y-.004,ankle.z+.051),(ankle.x+.032,y,ankle.z+.041)],3,.0011);o['ankleSide']=label
    o=binding('Boot outer stitched seam',[(ankle.x+(.046 if label=='R' else -.046),.14,ankle.z+.015),
        (ankle.x+(.060 if label=='R' else -.060),.24,ankle.z+.021),(ankle.x+(.055 if label=='R' else -.055),.335,ankle.z+.027)],5,.00065);o['ankleSide']=label
    bone=label+' Foot'
    o=body_loft('Solid layered leather sole',[(.007,.044,.111,ankle.z+.030),(.014,.056,.125,ankle.z+.031),(.024,.057,.126,ankle.z+.031)],7,64,10);o.location.x=ankle.x

# A manageable shield with its entire rim below the face in neutral guard.
for obj in groups['Shield']:
    for vertex in obj.data.vertices:
        p=U(obj.matrix_world@vertex.co);p.x*=.86;p.y*=.86
        vertex.co=obj.matrix_world.inverted()@B(p)
