using System;
using System.Collections.Generic;
using UnityEngine;

// Rigid, independently hinged lamellae. No metal vertex follows a thigh blend.
// Evaluate after linen so every plate can remain outside the current fabric.
[DefaultExecutionOrder(150)]
public sealed class MarchwardenArmor : MonoBehaviour
{
    [SerializeField] MarchwardenAsset data;
    [SerializeField] SkinnedMeshRenderer skin,body;
    [SerializeField] Transform[] joints;
    MarchwardenCloth linen;
    Transform pelvis,owner;
    const int Around=64,High=64;
    readonly List<int>[] clothCells=new List<int>[Around*High];
    Vector3[] projectedCloth,clothVertices;
    int[] clothTriangles;
    ClothPlane[] clothPlanes;
    struct ClothPlane
    {
        public Vector3 a,normal,u,v;
        public float originOffset;
        public ClothPlane(Vector3 a,Vector3 b,Vector3 c)
        {
            this.a=a;var ab=b-a;var ac=c-a;normal=Vector3.Cross(ab,ac);
            float inverse=normal.sqrMagnitude>.00000000000001f?1/normal.sqrMagnitude:0;
            if(inverse==0)normal=Vector3.zero;
            float aa=Vector3.Dot(ab,ab),bb=Vector3.Dot(ac,ac),cross=Vector3.Dot(ab,ac);
            u=(ab*bb-ac*cross)*inverse;v=(ac*aa-ab*cross)*inverse;
            originOffset=-Vector3.Dot(normal,a)-normal.z*.046f;
        }
        public float Intersect(float height,Vector3 direction)
        {
            float denominator=normal.x*direction.x+normal.z*direction.z;
            if(Mathf.Abs(denominator)<.00000001f)return float.NegativeInfinity;
            float distance=-(normal.y*height+originOffset)/denominator;
            if(distance<0)return float.NegativeInfinity;
            var delta=new Vector3(direction.x*distance,height,-.046f+direction.z*distance)-a;
            float x=Vector3.Dot(delta,u),y=Vector3.Dot(delta,v);
            return x<-.0001f||x>1.0001f||y<-.0001f||x+y>1.0001f?float.NegativeInfinity:distance;
        }
    }
    Matrix4x4 worldToCharacter;
    Matrix4x4 pelvisToCharacter;
    float uniformScale;
    Vector3[] barTangents,childOffsets;
    Vector3[] pivots;
    Quaternion[] restRotations,poseRotations,trialRotations,twistedRotations;
    Matrix4x4[] trialFrames;
    float clothBottom,clothTop;
    float[] angles,velocities,twists;
    Vector3 previousPosition,previousVelocity;
    bool ready;
    public int JointCount=>joints!=null?joints.Length:0;
    float legGap=float.MaxValue,clothGap=float.MaxValue;
    bool diagnosticsDirty;
    public float LegGap { get {UpdateDiagnostics();return legGap;} }
    public float ClothGap { get {UpdateDiagnostics();return clothGap;} }
    public float AttachmentError { get; private set; }
    public float MaximumAngle { get; private set; }
    int closestClothJoint;
    public int ClosestClothJoint { get {UpdateDiagnostics();return closestClothJoint;} }
    public int ContactQueries {get;private set;}
    public int FallbackQueries {get;private set;}

    public static MarchwardenArmor Prepare(GameObject root,SkinnedMeshRenderer body,MarchwardenAsset data,MarchwardenCloth linen)
    {
        if(data.waistArmor==null)return null;
        var item=root.GetComponentInChildren<MarchwardenArmor>(true);
        if(item==null)
        {
            var go=new GameObject("Articulated waist armor");go.transform.SetParent(body.transform,false);
            item=go.AddComponent<MarchwardenArmor>();item.skin=go.AddComponent<SkinnedMeshRenderer>();
        }
        item.data=data;item.body=body;item.linen=linen;item.owner=root.transform;
        item.pelvis=Array.Find(body.bones,b=>b.name=="Bip001 Pelvis");
        if(item.joints==null||item.joints.Length!=data.armorJoints.Length)
        {
            item.joints=new Transform[data.armorJoints.Length];
            for(int i=0;i<item.joints.Length;i++)
            {
                var spec=data.armorJoints[i];var bone=new GameObject($"Hip lamella hinge {i:00}").transform;
                bone.SetParent(spec.parent<0?item.pelvis:item.joints[spec.parent],false);
                var local=spec.parent<0?spec.pelvisFrame:data.armorJoints[spec.parent].pelvisFrame.inverse*spec.pelvisFrame;
                bone.localPosition=local.GetColumn(3);bone.localRotation=local.rotation;item.joints[i]=bone;
            }
        }
        item.skin.sharedMesh=data.waistArmor;item.skin.bones=item.joints;item.skin.rootBone=item.pelvis;
        item.skin.sharedMaterials=body.sharedMaterials;item.skin.quality=SkinQuality.Bone1;item.skin.updateWhenOffscreen=true;
        var bounds=data.waistArmor.bounds;bounds.Expand(.8f);item.skin.localBounds=bounds;
        if(Application.isPlaying&&linen!=null)item.Initialise();
        return item;
    }

    void Initialise()
    {
        if(ready)return;
        angles=new float[joints.Length];velocities=new float[joints.Length];twists=new float[joints.Length];
        projectedCloth=new Vector3[data.tunic.vertexCount];clothTriangles=data.tunic.triangles;
        clothPlanes=new ClothPlane[clothTriangles.Length/3];
        clothVertices=new Vector3[projectedCloth.Length];
        pelvisToCharacter=data.meshToCharacter*data.body.bindposes[Array.IndexOf(body.bones,pelvis)].inverse;
        barTangents=new Vector3[joints.Length];childOffsets=new Vector3[joints.Length];
        pivots=new Vector3[joints.Length];restRotations=new Quaternion[joints.Length];poseRotations=new Quaternion[joints.Length];trialRotations=new Quaternion[joints.Length];trialFrames=new Matrix4x4[joints.Length];
        twistedRotations=new Quaternion[joints.Length];
        for(int i=0;i<joints.Length;i++)restRotations[i]=data.armorJoints[i].pelvisFrame.rotation;
        for(int i=0;i<joints.Length-1;i++)if(data.armorJoints[i+1].parent==i)childOffsets[i]=joints[i+1].localPosition;
        for(int i=0;i<clothCells.Length;i++)clothCells[i]=new List<int>(8);
        previousPosition=owner.position;ready=true;
    }

    void BuildClothEnvelope()
    {
        worldToCharacter=pelvisToCharacter*pelvis.worldToLocalMatrix;
        foreach(var cell in clothCells)cell.Clear();clothBottom=float.MaxValue;clothTop=float.MinValue;
        for(int i=0;i<projectedCloth.Length;i++)
        {
            Vector3 p=worldToCharacter.MultiplyPoint3x4(linen.SurfaceVertexPosition(i));
            clothVertices[i]=p;
            clothBottom=Mathf.Min(clothBottom,p.y);clothTop=Mathf.Max(clothTop,p.y);
            projectedCloth[i]=new Vector3((Mathf.Atan2(p.x,p.z+.046f)+Mathf.PI)*Around/(2*Mathf.PI),(p.y-.25f)*High/.65f,new Vector2(p.x,p.z+.046f).magnitude);
        }
        for(int t=0;t<clothTriangles.Length;t+=3)
        {
            clothPlanes[t/3]=new ClothPlane(clothVertices[clothTriangles[t]],clothVertices[clothTriangles[t+1]],clothVertices[clothTriangles[t+2]]);
            Vector3 a=projectedCloth[clothTriangles[t]],b=projectedCloth[clothTriangles[t+1]],c=projectedCloth[clothTriangles[t+2]];
            if(b.x-a.x>Around/2)b.x-=Around;else if(b.x-a.x<-Around/2)b.x+=Around;
            if(c.x-a.x>Around/2)c.x-=Around;else if(c.x-a.x<-Around/2)c.x+=Around;
            int left=Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),right=Mathf.FloorToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x)));
            if(right-left>Around/2){left=0;right=Around-1;}
            int bottom=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y)))),top=Mathf.Min(High-1,Mathf.FloorToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))));
            for(int y=bottom;y<=top;y++)for(int x=left;x<=right;x++)
            {
                int cell=y*Around+(x%Around+Around)%Around;
                clothCells[cell].Add(t);
            }
        }
    }

    float FabricGap(Vector3 world)
    {
        Vector3 p=worldToCharacter.MultiplyPoint3x4(world);
        if(p.y<clothBottom-.003f||p.y>clothTop+.003f)return float.MaxValue;
        float x=(Mathf.Atan2(p.x,p.z+.046f)+Mathf.PI)*Around/(2*Mathf.PI),y=(p.y-.25f)*High/.65f;
        int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);float radius=float.NegativeInfinity;
        if(iy<0||iy>=High)return float.MaxValue;
        float length=new Vector2(p.x,p.z+.046f).magnitude;
        if(length<.000001f)return float.MaxValue;
        Vector3 direction=new Vector3(p.x/length,0,(p.z+.046f)/length);
        foreach(int triangle in clothCells[iy*Around+(ix%Around+Around)%Around])
        {
            radius=Mathf.Max(radius,clothPlanes[triangle/3].Intersect(p.y,direction));
        }
        if(float.IsNegativeInfinity(radius))return float.MaxValue;
        return (length-radius-.002f)*uniformScale;
    }

    float Clearance(int index,out float leg,out float fabric,bool complete=false)
    {
        ContactQueries++;
        var spec=data.armorJoints[index];var frame=trialFrames[index];
        leg=float.MaxValue;fabric=float.MaxValue;
        for(int p=0;p<spec.probes.Length;p++)
        {
            Vector3 point=frame.MultiplyPoint3x4(spec.probes[p]);
            foreach(var capsule in linen.CollisionShapes)leg=Mathf.Min(leg,capsule.Gap(point));
            fabric=Mathf.Min(fabric,FabricGap(point));
            if(!complete&&(leg<.006f||fabric<.006f))return Mathf.Min(leg-.006f,fabric-.006f);
        }
        float gap=Mathf.Min(leg-.006f,fabric-.006f);
        if(index+1<joints.Length&&data.armorJoints[index+1].parent==index)
        {
            // A parent's pose must leave its child's attachment bar outside
            // the cloth too. The child cannot rotate an intersecting pivot out
            // of contact by itself, regardless of its available hinge angle.
            Vector3 pivot=frame.MultiplyPoint3x4(childOffsets[index]);
            Vector3 tangent=barTangents[index];
            for(int edge=-1;edge<=1;edge++)
            {
                Vector3 point=pivot+tangent*edge;
                gap=Mathf.Min(gap,FabricGap(point)-.012f);
                foreach(var capsule in linen.CollisionShapes)gap=Mathf.Min(gap,capsule.Gap(point)-.010f);
                if(!complete&&gap<0)return gap;
            }
        }
        return gap;
    }

    void SetAngle(int index,float angle)
    {
        float half=-angle*Mathf.Deg2Rad*.5f;
        var q=twistedRotations[index]*new Quaternion(Mathf.Sin(half),0,0,Mathf.Cos(half));trialRotations[index]=q;
        // The same uniform-scale matrix, without native TRS calls for trials.
        float xx=2*q.x*q.x,yy=2*q.y*q.y,zz=2*q.z*q.z,xy=2*q.x*q.y,xz=2*q.x*q.z,yz=2*q.y*q.z;
        float wx=2*q.w*q.x,wy=2*q.w*q.y,wz=2*q.w*q.z;var p=pivots[index];float s=uniformScale;
        trialFrames[index]=new Matrix4x4(new Vector4((1-yy-zz)*s,(xy+wz)*s,(xz-wy)*s,0),
            new Vector4((xy-wz)*s,(1-xx-zz)*s,(yz+wx)*s,0),new Vector4((xz+wy)*s,(yz-wx)*s,(1-xx-yy)*s,0),new Vector4(p.x,p.y,p.z,1));
    }
    void SetTwist(int index,float angle)
    {
        twists[index]=angle;float half=angle*Mathf.Deg2Rad*.5f;
        twistedRotations[index]=poseRotations[index]*new Quaternion(0,Mathf.Sin(half),0,Mathf.Cos(half));
    }

    public double LastStepMilliseconds { get; private set; }
    void LateUpdate()
    {
        long start=System.Diagnostics.Stopwatch.GetTimestamp();
        Step();
        LastStepMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
    }
    void Step()
    {
        if(!ready||Time.deltaTime<=0)return;
        ContactQueries=FallbackQueries=0;
        float dt=Mathf.Min(Time.deltaTime,.05f);
        Vector3 velocity=(owner.position-previousPosition)/Mathf.Max(Time.deltaTime,.001f);
        Vector3 acceleration=(velocity-previousVelocity)/Mathf.Max(Time.deltaTime,.001f);
        if((owner.position-previousPosition).sqrMagnitude>.64f){velocity=Vector3.zero;acceleration=Vector3.zero;Array.Clear(velocities,0,velocities.Length);}
        previousPosition=owner.position;previousVelocity=velocity;
        BuildClothEnvelope();
        uniformScale=Mathf.Abs(owner.lossyScale.x);
        Quaternion pelvisRotation=pelvis.rotation;
        for(int i=0;i<joints.Length;i++){poseRotations[i]=pelvisRotation*restRotations[i];barTangents[i]=poseRotations[i]*Vector3.right*(.04f*uniformScale);}
        AttachmentError=MaximumAngle=0;
        for(int i=0;i<joints.Length;i++)
        {
            pivots[i]=joints[i].position;
            var spec=data.armorJoints[i];Vector3 outward=poseRotations[i]*Vector3.forward;
            float target=Mathf.Clamp(-Vector3.Dot(acceleration,outward)*.28f,0,12);
            velocities[i]+=(100*(target-angles[i])-20*velocities[i])*dt;
            SetTwist(i,Mathf.MoveTowards(twists[i],0,dt*24));
            float free=Mathf.Clamp(angles[i]+velocities[i]*dt,0,115);SetAngle(i,free);
            float initialGap=Clearance(i,out _,out _);
            if(initialGap<0)
            {
                float low=free,high=free;bool found=false;
                float best=free,bestGap=initialGap;
                for(float candidate=free+5;candidate<=120;candidate+=5)
                {SetAngle(i,candidate);high=candidate;float gap=Clearance(i,out _,out _);
                    if(gap>bestGap){bestGap=gap;best=candidate;}if(gap>=0){found=true;break;}low=candidate;}
                if(found)for(int pass=0;pass<4;pass++)
                {float mid=(low+high)*.5f;SetAngle(i,mid);if(Clearance(i,out _,out _)>=0)high=mid;else low=mid;}
                free=found?high:best;
                if(!found)
                {
                    // Leather suspension loops also permit a small side pivot.
                    // Use that freedom before forcing a plate above the belt.
                    float bestTwist=twists[i],bestPitch=free,bestScore=float.MaxValue;
                    for(int side=0;side<4;side++)
                    {
                        SetTwist(i,side==0?-6:side==1?6:side==2?-12:12);
                        for(float pitch=0;pitch<=120;pitch+=5)
                        {
                            float score=pitch*pitch+4*twists[i]*twists[i];
                            if(score>=bestScore)break;
                            FallbackQueries++;SetAngle(i,pitch);float gap=Clearance(i,out _,out _);
                            if(gap>=0&&score<bestScore){bestScore=score;bestTwist=twists[i];bestPitch=pitch;}
                        }
                    }
                    SetTwist(i,bestTwist);free=bestPitch;
                }
                velocities[i]=0;SetAngle(i,free);
            }
            angles[i]=free;MaximumAngle=Mathf.Max(MaximumAngle,free);
            // Constraint trials stay in local math. Commit only the accepted
            // pose, avoiding hundreds of native hierarchy rebuilds per frame.
            joints[i].rotation=trialRotations[i];
            if(spec.parent<0)AttachmentError=Mathf.Max(AttachmentError,Vector3.Distance(joints[i].position,pelvis.TransformPoint(spec.pelvisFrame.GetColumn(3))));
        }
        diagnosticsDirty=true;
    }
    void UpdateDiagnostics()
    {
        if(!diagnosticsDirty)return;
        diagnosticsDirty=false;legGap=clothGap=float.MaxValue;
        // Full-mesh clearance reporting is an inspection operation. Constraint
        // trials above already resolve every contact during ordinary gameplay.
        for(int i=0;i<joints.Length;i++)
        {
            Clearance(i,out float leg,out float fabric,true);legGap=Mathf.Min(legGap,leg);
            if(fabric<clothGap){clothGap=fabric;closestClothJoint=i;}
        }
    }
}
