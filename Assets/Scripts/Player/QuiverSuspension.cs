using System;
using UnityEngine;

// Belt-mounted spring suspension with current-pose arm and torso contacts.
[DefaultExecutionOrder(160)]
public sealed class QuiverSuspension : MonoBehaviour
{
    MarchwardenAsset data;
    Transform owner,pelvis,spine,upper,fore,hand;
    MarchwardenCloth linen;
    KnightGear gear;
    Bounds chest;
    Quaternion chestOffset;
    Quaternion restTilt,pelvisYaw=Quaternion.identity;
    Vector3 previousPosition,previousVelocity,offset,offsetVelocity;
    Vector2 swing,swingVelocity;
    const int HangerSegments=8;
    Transform hanger;
    Mesh hangerMesh;
    Material hangerMaterial,leatherSource;
    Vector3[] hangerVertices,hangerNormals;
    readonly Vector3[] beltAnchors=new Vector3[2];
    Vector3 beltOutward;
    readonly MarchwardenCloth.CapsuleShape[] armCapsules=new MarchwardenCloth.CapsuleShape[3];
    static readonly Vector2[] ContactAngles={new Vector2(25,0),new Vector2(-25,0),new Vector2(0,25),new Vector2(0,-25),
        new Vector2(45,0),new Vector2(-45,0),new Vector2(0,45),new Vector2(0,-45),
        new Vector2(32,32),new Vector2(32,-32),new Vector2(-32,32),new Vector2(-32,-32)};
    bool ready;
    public float ArmGap { get; private set; }
    public float TorsoGap { get; private set; }
    public float SuspensionOffset=>offset.magnitude;
    public float Swing=>swing.magnitude;
    public static QuiverSuspension Attach(GameObject root,Transform prop,SkinnedMeshRenderer body,MarchwardenAsset data)
    {
        var item=prop.GetComponent<QuiverSuspension>();if(item==null)item=prop.gameObject.AddComponent<QuiverSuspension>();
        item.data=data;item.owner=root.transform;item.gear=root.GetComponent<KnightGear>();
        Transform Bone(string name)=>Array.Find(body.bones,b=>b.name=="Bip001 "+name);
        item.pelvis=Bone("Pelvis");item.spine=Bone("Spine");item.upper=Bone("R UpperArm");item.fore=Bone("R Forearm");item.hand=Bone("R Hand");
        item.linen=root.GetComponent<MarchwardenVisual>().Linen;item.chestOffset=data.chestFrameRotation;
        int index=Array.IndexOf(body.bones,item.spine);var vertices=data.body.vertices;var weights=data.body.boneWeights;
        bool first=true;Bounds box=new Bounds();
        for(int i=0;i<vertices.Length;i++)if(weights[i].boneIndex0==index&&weights[i].weight0>.98f)
        {
            Vector3 point=Quaternion.Inverse(item.chestOffset)*data.body.bindposes[index].MultiplyPoint3x4(vertices[i]);
            if(first){box=new Bounds(point,Vector3.zero);first=false;}else box.Encapsulate(point);
        }
        int pelvisIndex=Array.IndexOf(body.bones,item.pelvis);
        item.restTilt=(data.meshToCharacter*data.body.bindposes[pelvisIndex].inverse*data.quiverMount).rotation;
        Matrix4x4 toPelvis=data.body.bindposes[pelvisIndex]*data.meshToCharacter.inverse;
        for(int side=0;side<2;side++)
        {
            float direction=side==0?-1:1;
            item.beltAnchors[side]=toPelvis.MultiplyPoint3x4(new Vector3(.148f,.821f,-.144f)+new Vector3(.014f,0,.014f)*direction);
        }
        item.beltOutward=toPelvis.MultiplyVector(new Vector3(.148f,0,-.098f).normalized);
        item.leatherSource=body.sharedMaterials[3];item.CreateHangers();
        item.chest=box;item.previousPosition=item.owner.position;item.ready=true;item.ApplyPose();item.UpdateHangers();return item;
    }

    void CreateHangers()
    {
        if(hangerMesh!=null)return;
        var go=new GameObject("Twin leather quiver hangers");hanger=go.transform;hanger.SetParent(owner,false);
        hangerMesh=new Mesh{name="Flat suspended quiver straps"};hangerMesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh=hangerMesh;
        hangerMaterial=new Material(Shader.Find("Standard")){name="Woven leather quiver suspension"};
        hangerMaterial.mainTexture=leatherSource.GetTexture("_DetailAlbedoMap");
        hangerMaterial.color=leatherSource.color;hangerMaterial.SetFloat("_Metallic",0);hangerMaterial.SetFloat("_Glossiness",.12f);
        go.AddComponent<MeshRenderer>().sharedMaterial=hangerMaterial;
        int sections=HangerSegments+1,faceVertices=sections*2;
        hangerVertices=new Vector3[faceVertices*4];hangerNormals=new Vector3[hangerVertices.Length];
        var uv=new Vector2[hangerVertices.Length];var triangles=new int[HangerSegments*12*2];int triangle=0;
        // Separate front and back normals retain a thin, flat ribbon without
        // relying on a transparent or one-sided material.
        for(int strap=0;strap<2;strap++)for(int face=0;face<2;face++)
        {
            int first=(strap*2+face)*faceVertices;
            for(int section=0;section<sections;section++)
            {float t=section/(float)HangerSegments;uv[first+section*2]=new Vector2(0,t*3);uv[first+section*2+1]=new Vector2(1,t*3);}
            for(int section=0;section<HangerSegments;section++)
            {
                int a=first+section*2,b=a+1,c=a+2,d=a+3;
                triangles[triangle++]=a;triangles[triangle++]=face==0?b:c;triangles[triangle++]=face==0?c:b;
                triangles[triangle++]=b;triangles[triangle++]=face==0?d:c;triangles[triangle++]=face==0?c:d;
            }
        }
        hangerMesh.vertices=hangerVertices;hangerMesh.normals=hangerNormals;hangerMesh.uv=uv;hangerMesh.triangles=triangles;
    }

    void UpdateHangers()
    {
        if(hangerMesh==null)return;
        hangerMaterial.color=leatherSource.color;
        int faceVertices=(HangerSegments+1)*2;float scale=Mathf.Abs(owner.lossyScale.x);
        Vector3 beltSpan=(pelvis.TransformPoint(beltAnchors[1])-pelvis.TransformPoint(beltAnchors[0])).normalized;
        Vector3 tabSpan=transform.forward;if(Vector3.Dot(beltSpan,tabSpan)<0)tabSpan=-tabSpan;
        Vector3 outward=pelvis.TransformDirection(beltOutward).normalized;
        Matrix4x4 toLocal=hanger.worldToLocalMatrix;
        for(int strap=0;strap<2;strap++)
        {
            float side=strap==0?-1:1;
            Vector3 start=pelvis.TransformPoint(beltAnchors[strap]);
            Vector3 end=transform.TransformPoint(data.quiverSuspension)+tabSpan*(side*.014f*scale);
            float slack=Mathf.Clamp(Vector3.Distance(start,end)*.06f,.003f*scale,.014f*scale);
            Vector3 bow=outward*(.016f*scale)-Vector3.up*slack;
            for(int section=0;section<=HangerSegments;section++)
            {
                float t=section/(float)HangerSegments;
                Vector3 center=Vector3.Lerp(start,end,t)+bow*Mathf.Sin(t*Mathf.PI);
                Vector3 tangent=(end-start+bow*(Mathf.PI*Mathf.Cos(t*Mathf.PI))).normalized;
                Vector3 across=Vector3.ProjectOnPlane(Vector3.Slerp(beltSpan,tabSpan,t),tangent).normalized;
                if(across.sqrMagnitude<.5f)across=Vector3.Cross(tangent,outward).normalized;
                Vector3 normal=Vector3.Cross(across,tangent).normalized;
                for(int face=0;face<2;face++)
                {
                    int vertex=(strap*2+face)*faceVertices+section*2;
                    hangerVertices[vertex]=toLocal.MultiplyPoint3x4(center-across*(.009f*scale));
                    hangerVertices[vertex+1]=toLocal.MultiplyPoint3x4(center+across*(.009f*scale));
                    hangerNormals[vertex]=hangerNormals[vertex+1]=hanger.InverseTransformDirection(normal*(face==0?1:-1));
                }
            }
        }
        hangerMesh.vertices=hangerVertices;hangerMesh.normals=hangerNormals;hangerMesh.RecalculateBounds();
    }

    void ApplyPose()
    {
        // The belt carries the attachment point through the roll, while the
        // hanging bag can pivot under gravity. Inheriting the pelvis's full
        // pitch/roll drives the entire arrow bundle through the rib cage.
        Quaternion frame=Quaternion.Inverse(owner.rotation)*pelvis.rotation*data.quiverMount.rotation*Quaternion.Inverse(restTilt);
        float twistLength=Mathf.Sqrt(frame.y*frame.y+frame.w*frame.w);
        if(twistLength>.001f)
        {
            var twist=new Quaternion(0,frame.y/twistLength,0,frame.w/twistLength);
            if(Quaternion.Dot(twist,pelvisYaw)<0)twist=new Quaternion(-twist.x,-twist.y,-twist.z,-twist.w);
            pelvisYaw=twist;
        }
        Quaternion rotation=owner.rotation*pelvisYaw*restTilt*Quaternion.Euler(swing.x,0,swing.y);
        Vector3 anchor=pelvis.TransformPoint(data.quiverMount.GetColumn(3))+offset;
        transform.SetPositionAndRotation(anchor-rotation*Vector3.Scale(data.quiverSuspension,transform.lossyScale),rotation);
    }

    static Vector3 SegmentDelta(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        Vector3 u=b-a,v=d-c,w=a-c;float aa=Vector3.Dot(u,u),bb=Vector3.Dot(u,v),cc=Vector3.Dot(v,v),dd=Vector3.Dot(u,w),ee=Vector3.Dot(v,w);
        float determinant=aa*cc-bb*bb;
        float s=determinant>.0000001f?Mathf.Clamp01((bb*ee-cc*dd)/determinant):0;
        float t=cc>.0000001f?(bb*s+ee)/cc:0;
        if(t<0){t=0;s=aa>.0000001f?Mathf.Clamp01(-dd/aa):0;}
        else if(t>1){t=1;s=aa>.0000001f?Mathf.Clamp01((bb-dd)/aa):0;}
        return a+u*s-c-v*t;
    }

    void Volume(bool arrows,out Vector3 start,out Vector3 end,out float radius)
    {
        float scale=Mathf.Abs(transform.lossyScale.x),height=data.quiver.bounds.max.y;
        start=transform.TransformPoint(new Vector3(0,arrows?height-.015f:.04f,0));
        end=transform.TransformPoint(new Vector3(0,arrows?height+.235f:height-.035f,0));
        radius=(arrows?.060f:Mathf.Max(data.quiver.bounds.extents.x,data.quiver.bounds.extents.z)+.004f)*scale;
    }

    float BoxContact(Vector3 start,Vector3 end,float radius,Matrix4x4 frame,Bounds box,bool resolve)
    {
        float gap=float.MaxValue,scale=Mathf.Abs(owner.lossyScale.x);var inverse=frame.inverse;
        for(int i=0;i<=12;i++)
        {
            Vector3 world=Vector3.Lerp(start,end,i/12f),point=inverse.MultiplyPoint3x4(world),closest=box.ClosestPoint(point);
            float distance=Vector3.Distance(point,closest)*scale;
            if(box.Contains(point))distance=-Mathf.Min(Mathf.Min(point.x-box.min.x,box.max.x-point.x),
                Mathf.Min(Mathf.Min(point.y-box.min.y,box.max.y-point.y),Mathf.Min(point.z-box.min.z,box.max.z-point.z)))*scale;
            gap=Mathf.Min(gap,distance-radius);
            if(resolve&&distance<radius+.012f)
            {
                // Exit through the shortest outward horizontal face. Always
                // choosing the rear face unnecessarily translated the quiver
                // by a full torso depth during a dodge, although the bag was
                // only a few centimetres from the right side of the box.
                Vector3 preferred=(owner.right*.8f-owner.forward).normalized;
                Vector3 correction=Vector3.zero;float shortest=float.MaxValue;
                for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
                {
                    Vector3 normal=Vector3.zero;normal[axis]=sign;
                    Vector3 worldNormal=frame.MultiplyVector(normal).normalized;
                    Vector3 horizontal=Vector3.ProjectOnPlane(worldNormal,owner.up);
                    float horizontalLength=horizontal.magnitude;
                    if(horizontalLength<.15f)continue;
                    horizontal/=horizontalLength;
                    if(Vector3.Dot(horizontal,preferred)<.15f)continue;
                    float push=(sign>0?box.max[axis]-point[axis]:point[axis]-box.min[axis])*scale+radius+.012f;
                    push/=horizontalLength;
                    if(push>=0&&push<shortest){shortest=push;correction=horizontal*(push+.0005f);}
                }
                if(shortest<float.MaxValue)offset+=correction;
                break;
            }
        }
        return gap;
    }

    void ResolveContacts(MarchwardenCloth.CapsuleShape[] arm,Matrix4x4 torso)
    {
        Vector3 preferred=(owner.right*.30f-owner.forward).normalized;
        for(int pass=0;pass<12;pass++)
        {
            Vector3 before=offset;ApplyPose();
            for(int volume=0;volume<2;volume++)
            {
                bool arrows=volume==1;
                Volume(arrows,out var start,out var end,out float radius);
                foreach(var capsule in arm)
                {
                    Vector3 delta=SegmentDelta(start,end,capsule.start,capsule.end);float distance=delta.magnitude;
                    float penetration=radius+capsule.radius+.015f-distance;
                    if(penetration>0)
                    {
                        Vector3 normal=Vector3.ProjectOnPlane(delta,owner.up).normalized;
                        if(Vector3.Dot(normal,preferred)<.1f)normal=preferred;
                        offset+=normal*(penetration+.002f);
                        ApplyPose();Volume(arrows,out start,out end,out radius);
                    }
                }
                BoxContact(start,end,radius,torso,chest,true);
                ApplyPose();Volume(arrows,out start,out end,out radius);
                if(gear!=null&&gear.StowedShield!=null&&gear.StowedShield.gameObject.activeInHierarchy)
                {BoxContact(start,end,radius,gear.StowedShield.localToWorldMatrix,data.shield.bounds,true);ApplyPose();Volume(arrows,out start,out end,out radius);}
                if(linen!=null)foreach(var capsule in linen.CollisionShapes)
                {
                    Vector3 delta=SegmentDelta(start,end,capsule.start,capsule.end);float penetration=radius+capsule.radius+.010f-delta.magnitude;
                    if(penetration>0){offset+=preferred*(penetration+.002f);ApplyPose();Volume(arrows,out start,out end,out radius);}
                }
            }
            if((offset-before).sqrMagnitude<.00000001f)break;
            offsetVelocity=Vector3.zero;
        }
        ApplyPose();
    }

    float Measure(MarchwardenCloth.CapsuleShape[] arm,Matrix4x4 torso)
    {
        ArmGap=TorsoGap=float.MaxValue;float limbGap=float.MaxValue;
        for(int volume=0;volume<2;volume++)
        {
            bool arrows=volume==1;
            Volume(arrows,out var start,out var end,out float radius);
            foreach(var capsule in arm)ArmGap=Mathf.Min(ArmGap,SegmentDelta(start,end,capsule.start,capsule.end).magnitude-radius-capsule.radius);
            TorsoGap=Mathf.Min(TorsoGap,BoxContact(start,end,radius,torso,chest,false));
            if(linen!=null)foreach(var capsule in linen.CollisionShapes)
                limbGap=Mathf.Min(limbGap,SegmentDelta(start,end,capsule.start,capsule.end).magnitude-radius-capsule.radius);
        }
        return Mathf.Min(Mathf.Min(ArmGap-.008f,TorsoGap-.008f),limbGap-.002f);
    }

    public double LastStepMilliseconds { get; private set; }
    void LateUpdate()
    {
        long start=System.Diagnostics.Stopwatch.GetTimestamp();Step();
        LastStepMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
    }
    void Step()
    {
        if(!ready||Time.deltaTime<=0)return;
        float dt=Mathf.Min(Time.deltaTime,.04f),scale=Mathf.Abs(owner.lossyScale.x);
        Vector3 velocity=(owner.position-previousPosition)/Mathf.Max(Time.deltaTime,.001f),acceleration=(velocity-previousVelocity)/Mathf.Max(Time.deltaTime,.001f);
        if((owner.position-previousPosition).sqrMagnitude>.64f){swing=swingVelocity=Vector2.zero;offset=offsetVelocity=Vector3.zero;acceleration=velocity=Vector3.zero;}
        previousPosition=owner.position;previousVelocity=velocity;
        Vector3 local=owner.InverseTransformDirection(acceleration);
        Vector2 target=new Vector2(Mathf.Clamp(-local.z*.45f,-6,6),Mathf.Clamp(local.x*.45f,-6,6));
        swingVelocity+=(65*(target-swing)-16*swingVelocity)*dt;swing+=swingVelocity*dt;
        swing=Vector2.ClampMagnitude(swing,45);
        offsetVelocity+=(-40*offset-13*offsetVelocity)*dt;offset+=offsetVelocity*dt;
        armCapsules[0]=new MarchwardenCloth.CapsuleShape(upper.position,fore.position,.104f*scale);
        armCapsules[1]=new MarchwardenCloth.CapsuleShape(fore.position,hand.position,.085f*scale);
        armCapsules[2]=new MarchwardenCloth.CapsuleShape(hand.position,hand.position,.055f*scale);
        Matrix4x4 torso=Matrix4x4.TRS(spine.position,spine.rotation*chestOffset,Vector3.one*scale);
        Vector2 freeSwing=swing;Vector3 freeOffset=offset;
        ResolveContacts(armCapsules,torso);
        if(offset.magnitude>.10f*scale)
        {
            // A hanging quiver should pivot on its suspension before stretching
            // the belt away from the body. Search a bounded contact cone only
            // when normal sway plus positional separation needs too much travel.
            Vector2 bestSwing=swing;Vector3 bestOffset=offset;
            float bestScore=Measure(armCapsules,torso)>=0?offset.sqrMagnitude:float.MaxValue;
            foreach(var delta in ContactAngles)
            {
                swing=Vector2.ClampMagnitude(freeSwing+delta,45);offset=freeOffset;
                ResolveContacts(armCapsules,torso);
                float rotationCost=(swing-freeSwing).sqrMagnitude*Mathf.Deg2Rad*Mathf.Deg2Rad*.0025f;
                float score=offset.sqrMagnitude+rotationCost;
                if(score<bestScore&&Measure(armCapsules,torso)>=0)
                {bestScore=score;bestSwing=swing;bestOffset=offset;}
            }
            float budget=Mathf.Max(.08f*scale,bestOffset.magnitude+.0005f);
            if((bestSwing-freeSwing).sqrMagnitude>.0001f)
            {
                // Refine the least rotation needed for the compact suspension.
                float low=0,high=1;
                for(int pass=0;pass<6;pass++)
                {
                    float amount=(low+high)*.5f;swing=Vector2.Lerp(freeSwing,bestSwing,amount);offset=freeOffset;
                    ResolveContacts(armCapsules,torso);
                    if(offset.magnitude<=budget&&Measure(armCapsules,torso)>=0)high=amount;else low=amount;
                }
                swing=Vector2.Lerp(freeSwing,bestSwing,high);offset=freeOffset;ResolveContacts(armCapsules,torso);
            }
            else{swing=bestSwing;offset=bestOffset;ApplyPose();}
            swingVelocity=Vector2.zero;offsetVelocity=Vector3.zero;
        }
        Measure(armCapsules,torso);
        UpdateHangers();
    }

    void OnDestroy()
    {
        if(hangerMesh!=null)Destroy(hangerMesh);if(hangerMaterial!=null)Destroy(hangerMaterial);
        if(hanger!=null)Destroy(hanger.gameObject);
    }
}
