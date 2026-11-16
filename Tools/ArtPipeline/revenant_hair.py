"""Area-sampled, scalp-following grooms, batched into one mesh per hair style."""
from mathutils.bvhtree import BVHTree
import bisect

base_objects=objects
head_faces=[f for f in source_faces if all(body_points[i].y>1.535 for i in f)]
scalp_bvh=BVHTree.FromPolygons(body_points,head_faces,all_triangles=False)

for style in range(3):
    objects=[];random_hair=random.Random(285+style*71+seed)
    def hairline(p):
        forehead=smooth((p.z-.012)/.075)
        return 1.647+forehead*(.078 if style==0 else .063 if style==1 else .080)
    def density(p):
        edge=smooth((p.y-hairline(p))/.018)
        patch=.78+.22*math.sin(p.x*57+p.z*32+seed)*math.sin(p.y*87)
        return edge*patch
    cap=fitted_surface(['Patchy cropped scalp','Swept cropped scalp','Receding matted scalp'][style],9,
        lambda p,f:p.y>hairline(p)-.012 and abs(p.x)<.12,.0016,'Head')
    cap.modifiers.clear()
    for v,c in zip(cap.data.vertices,cap.data.color_attributes['Decay'].data):
        c.color=(1,1,1,density(U(v.co))*(.70 if style==0 else 1.0))

    vertices=[];faces=[];uvs=[];colors=[]
    def groom(root,length,width,beard=False):
        p,n,_,_=scalp_bvh.find_nearest(root)
        if p is None:return
        if n.dot(p-Vector((0,1.67,0)))<0:n=-n
        side=1 if p.x>0 else -1
        comb=Vector((.70 if style==1 else side*.30,-.72,-.50)) if not beard else Vector((side*.10,-1,.06))
        guide=[];steps=9;delta=length/(steps-1)
        for j in range(steps):
            t=j/(steps-1);flow=comb-n*comb.dot(n)
            if flow.length<.1:flow=Vector((.7,0,-.7))-n*n.dot(Vector((.7,0,-.7)))
            flow.normalize();lift=.0018+(.0025 if beard else .006 if style==0 else .012)*math.sin(t*math.pi)
            center=p+n*lift
            across=n.cross(flow).normalized();w=width*(.55+.45*math.sin(t*math.pi))*(1-.80*t**2)
            fade=smooth(t/.19)*(1-smooth((t-.78)/.22))
            for sign in (-1,1):vertices.append(center+across*w*sign);colors.append((1,1,1,fade))
            guide.append(center)
            nearest,normal,_,_=scalp_bvh.find_nearest(p+flow*delta)
            if nearest is None or (nearest-p).length<delta*.18:break
            p=nearest;n=normal
            if n.dot(p-Vector((0,1.67,0)))<0:n=-n
        base=len(vertices)-len(guide)*2
        for j in range(len(guide)-1):
            a=base+j*2;faces.append((a,a+1,a+3,a+2));uvs.append([(0,j/(steps-1)),(1,j/(steps-1)),(1,(j+1)/(steps-1)),(0,(j+1)/(steps-1))])

    triangles=[];areas=[];total=0
    for face in head_faces:
        for j in range(1,len(face)-1):
            triangle=[body_points[i] for i in (face[0],face[j],face[j+1])]
            center=sum(triangle,Vector())/3
            if center.y<hairline(center)-.006 or abs(center.x)>.115:continue
            area=(triangle[1]-triangle[0]).cross(triangle[2]-triangle[0]).length*.5
            total+=area;triangles.append(triangle);areas.append(total)
    for k in range((230,470,620)[style]):
        a,b,c=triangles[bisect.bisect_left(areas,random_hair.random()*total)]
        u=math.sqrt(random_hair.random());v=random_hair.random();p=a*(1-u)+b*u*(1-v)+c*u*v
        if random_hair.random()>max(.2,density(p)):continue
        if kind in ('Swordsman','Bulwark','Warlord') and p.y>1.69:continue
        length=random_hair.uniform(.010,.025) if style==0 else random_hair.uniform(.035,.070) if style==1 else random_hair.uniform(.065,.125)
        groom(p,length,random_hair.uniform(.003,.006))
    if style==2:
        beard_triangles=[];beard_areas=[];beard_total=0
        for face in head_faces:
            for j in range(1,len(face)-1):
                triangle=[body_points[i] for i in (face[0],face[j],face[j+1])];center=sum(triangle,Vector())/3
                if not (1.55<center.y<1.62 and abs(center.x)<.078 and center.z>.065):continue
                beard_total+=(triangle[1]-triangle[0]).cross(triangle[2]-triangle[0]).length*.5
                beard_triangles.append(triangle);beard_areas.append(beard_total)
        for k in range(190):
            a,b,c=beard_triangles[bisect.bisect_left(beard_areas,random_hair.random()*beard_total)]
            u=math.sqrt(random_hair.random());v=random_hair.random();p=a*(1-u)+b*u*(1-v)+c*u*v
            groom(p,random_hair.uniform(.008,.023),random_hair.uniform(.0018,.0035),True)
    add_mesh('Groomed tapered scalp and beard strands',vertices,faces,8,[[(jid('Head'),1)]]*len(vertices),colors,uvs)
    all_exports.append(export_objects(kind+'_Hair'+str(style),objects,[]))
    for o in objects:
        mod=o.modifiers.new('Head animation','ARMATURE');mod.object=ao;o.parent=ao
        o.hide_render=style!=seed%3 or seed%5==0;o.hide_set(o.hide_render)
objects=base_objects
