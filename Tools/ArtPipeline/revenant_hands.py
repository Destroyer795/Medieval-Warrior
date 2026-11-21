"""CC0 anatomical hands fitted to this rig's actual MCP and thumb landmarks.

Native MCP/PIP/DIP posing, opposed thumb, and a continuous 10cm forearm tail.
The old fitted hands are removed beneath anatomical linen wrist wrappings.
"""
if 'HAND_LIBRARY' not in globals():
    marker={n[2:]:v for n,v in markers.items() if n.startswith('r-')}
    hand_origin=marker['hand'];hand_distal=(sum((marker['finger-%d-1'%f] for f in range(2,6)),Vector())/4-hand_origin).normalized()
    hand_across=marker['finger-2-1']-marker['finger-5-1'];hand_across=(hand_across-hand_distal*hand_across.dot(hand_distal)).normalized()
    hand_dorsal=-hand_across.cross(hand_distal).normalized()
    def HC(p):
        d=p-hand_origin;return Vector((d.dot(hand_across)*.095,d.dot(hand_distal)*.095,d.dot(hand_dorsal)*.095))
    chains={f:[HC(marker['finger-%d-%d'%(f,j)]) for j in range(1,5)] for f in range(1,6)}
    hf=[];huv=[]
    for f,fu in zip(body_faces,body_uv):
        if all(raw[i].x<0 and -.10<HC(raw[i]).y<.195 and abs(HC(raw[i]).x)<.095 and abs(HC(raw[i]).z)<.060 for i in f):hf.append(f);huv.append(fu)
    ids=sorted({i for f in hf for i in f});remap={i:j for j,i in enumerate(ids)}
    hand_points=[HC(raw[i]) for i in ids];hand_faces=[[remap[i] for i in f] for f in hf]
    GRIP=Vector((0,.065,-.025))
    def finger_pose(f,angles):
        q=[chains[f][0].copy()];angle=0
        for j,a in enumerate(angles):
            angle+=math.radians(a);length=(chains[f][j+1]-chains[f][j]).length
            q.append(q[-1]+Vector((0,math.cos(angle)*length,-math.sin(angle)*length)))
        return q
    def best_angles(f):
        best=None;score=float('inf')
        for a in range(30,86,5):
            for b in range(45,96,5):
                for c in range(30,81,5):
                    q=finger_pose(f,(a,b,c));cost=0
                    for j in range(3):
                        for t in (.25,.5,.75,1):
                            p=q[j].lerp(q[j+1],t)-GRIP;r=math.hypot(p.y,p.z);cost+=(r-.026)**2+max(0,.023-r)**2*45
                    cost+=(q[-1].y-(GRIP.y-.003))**2*.6
                    cost+=((a-50)**2+(b-70)**2+(c-45)**2)*.000000025
                    if cost<score:score=cost;best=(a,b,c)
        return best
    closed={f:finger_pose(f,best_angles(f)) for f in range(2,6)}
    claw_angles=((9,32,17),(14,41,21),(18,48,24),(23,54,27))
    claw={f:finger_pose(f,claw_angles[f-2]) for f in range(2,6)}
    def thumb_pose(targets):
        q=[chains[1][0].copy()]
        for j,p in enumerate(targets):q.append(q[-1]+(Vector(p)-q[-1]).normalized()*(chains[1][j+1]-chains[1][j]).length)
        return q
    closed[1]=thumb_pose(((.043,.065,-.012),(.029,.081,-.032),(.010,.073,-.042)))
    claw[1]=thumb_pose(((.047,.060,-.008),(.049,.080,-.014),(.046,.096,-.015)))
    def HF(a,b):
        y=(b-a).normalized();x=Vector((1,0,0));x=(x-y*x.dot(y)).normalized();z=x.cross(y);return Matrix((x,y,z)).transposed()
    influences=[]
    for p in hand_points:
        d,f,j,t=min(((*near(p,chain[k],chain[k+1])[:1],f,k,near(p,chain[k],chain[k+1])[1]) for f,chain in chains.items() for k in range(3)))
        chain=chains[f];lengths=[(chain[k+1]-chain[k]).length for k in range(3)];s=sum(lengths[:j])+lengths[j]*t
        along=(p-chain[0]).dot((chain[1]-chain[0]).normalized()) if j==0 else s
        strength=smooth((along+.010)/.024)*(1-smooth((d-.012)/.012));w=[0,0,0];w[j]=1
        for k in (1,2):
            boundary=sum(lengths[:k]);width=min(lengths[k-1],lengths[k])*.24
            if abs(s-boundary)<width:
                blend=smooth((s-boundary+width)/(2*width));w=[0,0,0];w[k-1]=1-blend;w[k]=blend
        influences.append((f,strength,w))
    def deform(pose):
        matrices={f:[(HF(q[j],q[j+1])@HF(chains[f][j],chains[f][j+1]).inverted(),q[j]) for j in range(3)] for f,q in pose.items()};result=[]
        for p,(f,strength,ws) in zip(hand_points,influences):
            posed=sum(((matrices[f][j][0]@(p-chains[f][j])+matrices[f][j][1])*ws[j] for j in range(3)),Vector());result.append(p.lerp(posed,strength))
        return result
    HAND_LIBRARY={'Power':deform(closed),'Claw':deform(claw)}
    hand_removed=[]
    for i in used:
        p=raw[i].copy();p.x=-abs(p.x);c=HC(p)
        hand_removed.append(abs(raw[i].x)>3.0 and c.y>-.045 and abs(c.x)<.12 and abs(c.z)<.09)

def target_hand_frame(side):
    wrist=J(side+'ArmPalm')
    distal=(sum((J(side+'ArmDigit%d1'%f) for f in range(2,6)),Vector())/4-wrist).normalized()
    across=J(side+'ArmDigit21')-J(side+'ArmDigit51');across=(across-distal*across.dot(distal)).normalized()
    normal=across.cross(distal).normalized()
    # MakeHuman's right arm is on -X. The Unity rig puts the right arm on
    # +X, so its hand needs the opposite handedness in these coordinates.
    dorsal=normal*(1 if side=='R' else -1)
    arm=(wrist-J(side+'ArmForearm')).normalized()
    arm_local=Vector((arm.dot(across),arm.dot(distal),arm.dot(dorsal)))
    forearm_fit=(-HC(marker['elbow'])).normalized().rotation_difference(arm_local)
    return wrist,distal,across,dorsal,normal,forearm_fit

def fit_wrist_seams(points):
    for i,raw_index in enumerate(used):
        p=raw[raw_index].copy()
        if abs(p.x)<3:continue
        side='R' if points[i].x>0 else 'L';p.x=-abs(p.x);c=HC(p)
        if not (-.17<c.y<0 and abs(c.x)<.10 and abs(c.z)<.08):continue
        wrist,distal,across,dorsal,normal,forearm_fit=target_hand_frame(side)
        q=c.lerp(forearm_fit@c,smooth((-c.y-.005)/.060))
        target=wrist+across*q.x+distal*q.y+dorsal*q.z
        points[i]=points[i].lerp(target,smooth((c.y+.145)/.075))

def make_hands():
    result=[]
    for side in ('L','R'):
        wrist,distal,across,dorsal,normal,forearm_fit=target_hand_frame(side)
        def place(p):
            p=p.lerp(forearm_fit@p,smooth((-p.y-.005)/.060));return wrist+across*p.x+distal*p.y+dorsal*p.z
        power=side=='R' and kind in ('Swordsman','Bulwark','Warlord') or side=='L' and kind=='Bulwark'
        points=[place(p) for p in HAND_LIBRARY['Power' if power else 'Claw']];ws=[]
        for p in hand_points:
            w=smooth((p.y+.07)/.06);ws.append([(jid(side+'ArmForearm'),1-w),(jid(side+'ArmPalm'),w)])
        add_mesh('Continuous anatomical '+side+' hand and wrist',points,hand_faces,0,ws,[decay_color(p) for p in points],huv)
        # Keep the equipment mounting plane separate from the anatomical dorsum.
        result.append(dict(side=side,position=vec(place(GRIP)),along=vec(across),normal=vec(-normal),distal=vec(distal),dorsal=vec(dorsal)))
        thumb=place(chains[1][0])-wrist;target_thumb=J(side+'ArmDigit11')-wrist
        assert thumb.dot(target_thumb)>0,side+' thumb is on the wrong side of the palm'
        assert dorsal.x*(1 if side=='R' else -1)>.5,side+' hand dorsum should face out in the bind pose'
        print('HAND FRAME',kind,side,'thumb alignment',round(thumb.normalized().dot(target_thumb.normalized()),4),flush=True)
        axis=(wrist-J(side+'ArmForearm')).normalized()
        fitted_surface('Fitted ragged wrist wrapping',1,lambda p,f,wrist=wrist,axis=axis:all(any('ArmForearm' in names[b] and w>.15 for b,w in body_weights[i]) for i in f) and -.070<(p-wrist).dot(axis)<-.012,.006)
    return result
