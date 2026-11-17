"""Dry medieval house surfaces and geometry-derived AO; called by buildings.py."""
import bpy, json, math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

MATTE=(2,3,4,7)

def noise2(x,y,frequency,rng):
    grid=rng.random((frequency+2,frequency+2)).astype(np.float32)
    xx=x*frequency;yy=y*frequency;ix=xx.astype(np.int32);iy=yy.astype(np.int32)
    fx=xx-ix;fy=yy-iy;fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy)
    return (grid[iy,ix]*(1-fx)+grid[iy,ix+1]*fx)*(1-fy)+(grid[iy+1,ix]*(1-fx)+grid[iy+1,ix+1]*fx)*fy

def finish_surface(i,x,y,noise,cloud,col,height,tone,rng,rough):
    ao=np.clip(.93+cloud*.06,0,1);smooth=np.clip(1-rough+cloud*.025,.015,.8)
    rgb=np.broadcast_to(np.array(col),(len(x),len(x),3)).copy()
    if i not in MATTE:return height,tone,ao,smooth,rgb
    broad=noise2(x,y,7,rng);medium=noise2(x,y,29,rng)
    if i==3:
        # Hand-applied lime: trowel undulations, coarse aggregate, sandy grit and pinholes.
        aggregate=noise2(x,y,151,rng);sand=noise2(x,y,337,rng)
        pits=np.clip((noise-1.30)*.75,0,1)
        ridges=np.maximum(0,medium-.48)**1.7
        height=(broad-.5)*.65+(aggregate-.5)*.25+(sand-.5)*.13+noise*.025+ridged(ridges)-pits*.16
        tone=.83+broad*.18+(aggregate-.5)*.055+noise*.015-pits*.10
        ao=np.clip(.97-pits*.22-ridges*.10,.64,1)
        stain=np.clip((.40-broad)*1.2,0,.22)
        rgb=rgb*(1-stain[:,:,None])+np.array((.43,.40,.31))*stain[:,:,None]
    elif i in (2,7):
        # U is the long chart direction. Warp across V; cracks run with the fibres.
        long=noise2(x,y*.10,23,rng)
        warp=y+.012*np.sin(x*17)+.017*(long-.5)
        fibres=np.sin(warp*840+np.sin(x*13)*2+np.sin(warp*120)*.8)
        growth=np.sin(warp*192+np.sin(x*9)*1.5)
        cracks=np.zeros_like(x)
        for crack in range(18):
            cy=rng.uniform(.015,.985);cx=rng.uniform(-.1,1.1);length=rng.uniform(.08,.42)
            center=cy+.003*np.sin(x*rng.uniform(22,65)+crack)+.0015*np.sin(x*167+crack)
            width=rng.uniform(.0007,.0023)
            taper=np.clip(1-((x-cx)/length)**2,0,1)
            split=np.exp(-((y-center)/(width*taper+.00008))**2)*taper
            cracks=np.maximum(cracks,split)
        knot=np.sqrt(((y-.43)*4.5)**2+((x-.40)*1.0)**2)
        rings=np.sin(knot*135)*np.exp(-knot*7)
        height=fibres*.035+growth*.08+rings*.07-cracks*.65+noise*.006
        tone=.76+growth*.085+fibres*.035+rings*.07+broad*.17-cracks*.36
        # Bleached ridges and irregular water-darkened patches replace an oiled finish.
        age=np.clip((medium-.28)*.68,0,.43)
        rgb=rgb*(1-age[:,:,None])+np.array((.25,.225,.18))*age[:,:,None]
        ao=np.clip(.98-cracks*.57-np.maximum(0,-growth)*.055,.36,1)
    elif i==4:
        # Unglazed fired clay: mineral mottling, granular body, erosion and soot/lichen.
        pores=np.clip((noise-1.15)*.7,0,1);grain=noise2(x,y,293,rng)
        height=(medium-.5)*.18+(grain-.5)*.15+noise*.018-pores*.10
        weather=np.clip((.46-broad)*1.8,0,.45)
        tone=.86+broad*.12+(grain-.5)*.055-pores*.09
        rgb=rgb*(1-weather[:,:,None])+np.array((.20,.17,.115))*weather[:,:,None]
        mineral=np.clip((medium-.72)*.8,0,.13)
        rgb=rgb*(1-mineral[:,:,None])+np.array((.48,.34,.23))*mineral[:,:,None]
        ao=np.clip(.98-pores*.20-weather*.10,.69,1)
    # Uncoated surfaces have no smooth patches. Specular is also disabled on the house material.
    smooth=np.zeros_like(x)
    return height,tone,ao,smooth,rgb

def ridged(field):return field*.22

def save_image(name,array,path):
    h,w=array.shape[:2];im=bpy.data.images.get(name) or bpy.data.images.new(name,width=w,height=h,alpha=True)
    if tuple(im.size)!=(w,h):im.scale(w,h)
    im.colorspace_settings.name='Non-Color';im.alpha_mode='CHANNEL_PACKED'
    im.pixels.foreach_set(array.ravel());im.filepath_raw=str(path);im.file_format='PNG';im.save();im.pack()
    return im

def bake_house_occlusion(root,palette):
    """Cosine-weighted BVH contact rays, rasterised into each LOD's existing UV0 charts.

    Only the target house mesh enters the BVH: neighbouring source collections never
    contaminate the bake. Render/collision geometry and lightmap UVs are untouched.
    """
    directory=root/'Assets/Resources/Buildings/Textures/HouseAO';directory.mkdir(parents=True,exist_ok=True)
    packed=np.empty(4096*4096*4,np.float32);bpy.data.images['Buildings Masks'].pixels.foreach_get(packed)
    micro=packed.reshape(4096,4096,4)[:,:,1].copy();del packed
    stats=[]
    for form in ('House','HouseB'):
        for lod in range(3):
            obj=bpy.data.objects[form+f' LOD{lod}'];m=obj.data;m.calc_loop_triangles()
            positions=[v.co.copy() for v in m.vertices];tree=BVHTree.FromPolygons(positions,[tuple(p.vertices) for p in m.polygons],all_triangles=False,epsilon=0)
            samples=20;radius=.065;values=np.ones(len(positions),np.float32)
            def contact(point,normal,seed):
                n=normal.normalized();axis=n.cross(Vector((0,0,1)))
                if axis.length<.1:axis=n.cross(Vector((0,1,0)))
                axis.normalize();other=n.cross(axis);origin=point+n*.00035
                blocked=0;phase=(seed*.61803398875%1)*math.tau
                for ray in range(samples):
                    t=(ray+.5)/samples;r=math.sqrt(t);a=ray*2.39996322973+phase
                    direction=axis*(r*math.cos(a))+other*(r*math.sin(a))+n*math.sqrt(1-t)
                    hit,normal,index,distance=tree.ray_cast(origin,direction,radius)
                    if hit is not None:blocked+=(1-(distance/radius)**.5)
                return max(.26,1-.86*blocked/samples)
            for vertex in m.vertices:values[vertex.index]=contact(vertex.co,vertex.normal,vertex.index)
            size=(2048,1024,512)[lod];ao=np.ones((size,size),np.float32);covered=np.zeros((size,size),bool)
            uv=m.uv_layers[0].data
            # Sample face centres as well as corners, so open plaster/beam/tile faces
            # stay bright while the occluded perimeter darkens. Corner-only baking
            # would incorrectly stain an entire wall panel with its frame's contact shadow.
            triangles=[]
            for face in m.polygons:
                coords=[np.array(uv[j].uv[:])*size for j in face.loop_indices]
                center=np.mean(coords,axis=0);middle=contact(face.center,face.normal,face.index+len(positions))
                for j in range(len(coords)):
                    triangles.append((np.array((center,coords[j],coords[(j+1)%len(coords)])),(middle,values[face.vertices[j]],values[face.vertices[(j+1)%len(coords)]])))
            for p,corners in triangles:
                lo=np.maximum(0,np.floor(p.min(0)).astype(int));hi=np.minimum(size-1,np.ceil(p.max(0)).astype(int))
                if np.any(hi<lo):continue
                xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
                d=(p[1,1]-p[2,1])*(p[0,0]-p[2,0])+(p[2,0]-p[1,0])*(p[0,1]-p[2,1])
                if abs(d)<1e-10:continue
                a=((p[1,1]-p[2,1])*(xx-p[2,0])+(p[2,0]-p[1,0])*(yy-p[2,1]))/d
                b=((p[2,1]-p[0,1])*(xx-p[2,0])+(p[0,0]-p[2,0])*(yy-p[2,1]))/d;c=1-a-b
                inside=(a>=-1e-6)&(b>=-1e-6)&(c>=-1e-6)
                data=a*corners[0]+b*corners[1]+c*corners[2]
                region=ao[lo[1]:hi[1]+1,lo[0]:hi[0]+1];region[inside]=data[inside]
                covered[lo[1]:hi[1]+1,lo[0]:hi[0]+1]|=inside
            # Dilate contact values into chart padding for stable mip filtering.
            for step in range(3):
                new=ao.copy();filled=covered.copy()
                for dy,dx in ((-1,0),(1,0),(0,-1),(0,1)):
                    valid=np.roll(covered,(dy,dx),(0,1))&~covered
                    new[valid]=np.minimum(new[valid],np.roll(ao,(dy,dx),(0,1))[valid]);filled|=valid
                ao,covered=new,filled
            ao*=micro[::4096//size,::4096//size]
            pixels=np.ones((size,size,4),np.float32);pixels[:,:,:3]=ao[:,:,None]
            label=form+f'_LOD{lod}';image=save_image('House contact AO '+label,pixels,directory/(label+'.png'))
            # The editable Blender preview uses the same contact bake and dry palette.
            for i,(name,*rest) in enumerate(palette):
                base=bpy.data.materials[name];material=bpy.data.materials.get(label+' | '+name)
                if material is not None:bpy.data.materials.remove(material,do_unlink=True)
                material=base.copy();material.name=label+' | '+name;nt=material.node_tree
                bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED');link=bs.inputs['Base Color'].links[0];color=link.from_socket
                tex=nt.nodes.new('ShaderNodeTexImage');tex.image=image;mix=nt.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
                nt.links.new(color,mix.inputs[1]);nt.links.new(tex.outputs['Color'],mix.inputs[2]);nt.links.new(mix.outputs['Color'],bs.inputs['Base Color'])
                m.materials[i]=material
            stats.append({'name':label,'size':size,'raysPerVertex':samples,'radiusLocalMetres':radius,'min':round(float(values.min()),4),'mean':round(float(values.mean()),4),'p10':round(float(np.quantile(values,.1)),4)})
            print('[House AO]',label,stats[-1],flush=True)
    return stats
