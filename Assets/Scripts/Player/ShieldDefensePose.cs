using System;
using UnityEngine;

// One owner for the heater defense. Applies after the animation graph, before
// equipment mounts. Feet retain their animated contact positions; the arm uses
// analytic IK with an orientation constraint and a chest separating plane.
public sealed class ShieldDefensePose
{
    readonly Transform root,pelvis,spine,head,upper,fore,hand;
    readonly Transform[] thigh=new Transform[2],calf=new Transform[2],foot=new Transform[2];
    readonly MarchwardenAsset asset;
    readonly Vector3[] shieldVertices;
    readonly Vector3[][] shieldVolumes;
    readonly Quaternion chestFrameOffset;
    readonly Bounds chest;
    readonly float rigScale;
    readonly Vector3 eyeLocal;
    readonly Transform[] posed;
    readonly Quaternion[] originalRotations;
    Vector3 pelvisBefore;
    bool applied;
    public float ChestGap { get; private set; }
    public float FootDrift { get; private set; }
    public bool SightlineClear { get; private set; }
    public bool CoversTorso { get; private set; }
    public Vector3 TorsoProbeLocal { get; private set; }
    public float ShieldTop { get; private set; }
    public float EyeHeight => root.InverseTransformPoint(head.TransformPoint(eyeLocal)).y;

    public ShieldDefensePose(GameObject go)
    {
        root=go.transform;asset=MarchwardenVisual.Asset;
        rigScale=Mathf.Abs(root.lossyScale.y);
        // Eight bounds corners conservatively contain every plate, rivet and
        // rear fitting; no per-frame mesh reads or vertex-array allocations.
        shieldVertices=new Vector3[8];var bounds=asset.shield.bounds;
        for(int i=0;i<8;i++)shieldVertices[i]=bounds.center+Vector3.Scale(bounds.extents,
            new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
        var plate=bounds;var low=plate.min;low.z=.010f;plate.SetMinMax(low,plate.max);
        Bounds rear=new Bounds();bool rearStarted=false;
        foreach(var vertex in asset.shield.vertices)if(vertex.z<.010f)
        {if(!rearStarted){rear=new Bounds(vertex,Vector3.zero);rearStarted=true;}else rear.Encapsulate(vertex);}
        rear.Expand(.010f);var high=rear.max;high.z=.035f;rear.SetMinMax(rear.min,high);
        shieldVolumes=new[]{new Vector3[8],new Vector3[8]};
        for(int volume=0;volume<2;volume++)for(int i=0;i<8;i++)
        {var box=volume==0?plate:rear;shieldVolumes[volume][i]=box.center+Vector3.Scale(box.extents,
            new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));}
        var bones=go.GetComponentInChildren<SkinnedMeshRenderer>().bones;
        Transform Find(string n)=>Array.Find(bones,b=>b.name=="Bip001 "+n);
        pelvis=Find("Pelvis");spine=Find("Spine");head=Find("Head");
        upper=Find("L UpperArm");fore=Find("L Forearm");hand=Find("L Hand");
        for(int i=0;i<2;i++){string s=i==0?"L ":"R ";thigh[i]=Find(s+"Thigh");calf[i]=Find(s+"Calf");foot[i]=Find(s+"Foot");}
        chestFrameOffset=asset.chestFrameRotation;
        int spineIndex=Array.IndexOf(bones,spine);
        var positions=asset.body.vertices;var weights=asset.body.boneWeights;
        Bounds measured=new Bounds();bool started=false;
        for(int i=0;i<positions.Length;i++)if(weights[i].boneIndex0==spineIndex&&weights[i].weight0>.98f)
        {
            Vector3 point=Quaternion.Inverse(chestFrameOffset)*asset.body.bindposes[spineIndex].MultiplyPoint3x4(positions[i]);
            if(!started){measured=new Bounds(point,Vector3.zero);started=true;}else measured.Encapsulate(point);
        }
        chest=new Bounds(measured.center*rigScale,measured.size*rigScale);
        // Independent of whether Start runs before or after the first animation
        // evaluation; both constraints are exported from the anatomical bind pose.
        eyeLocal=asset.headEyeLocal;
        posed=new[]{spine,upper,fore,thigh[0],thigh[1],calf[0],calf[1],foot[0],foot[1]};
        originalRotations=new Quaternion[posed.Length];
        Measure(spine.rotation*chestFrameOffset);
    }

    // Remove last frame's procedural changes before the Animator evaluates.
    // Some demo idle clips have no pelvis-position track; without this the
    // crouch accumulates every frame and the torso sinks behind the shield.
    public void Restore()
    {
        if(!applied)return;
        pelvis.localPosition=pelvisBefore;
        for(int i=0;i<posed.Length;i++)posed[i].localRotation=originalRotations[i];
        applied=false;
    }

    public void Apply(float weight)
    {
        Restore();pelvisBefore=pelvis.localPosition;
        for(int i=0;i<posed.Length;i++)originalRotations[i]=posed[i].localRotation;
        applied=true;
        weight=Mathf.Clamp01(weight);
        Vector3 left=foot[0].position,right=foot[1].position;
        Vector3 leftKnee=calf[0].position,rightKnee=calf[1].position;
        Quaternion leftRot=foot[0].rotation,rightRot=foot[1].rotation;
        pelvis.position-=root.up*(.027f*weight*rigScale);
        spine.rotation=root.rotation*Quaternion.Euler(7*weight,-11*weight,0)*Quaternion.Inverse(root.rotation)*spine.rotation;
        for(int i=0;i<2;i++)
        {
            Vector3 target=i==0?left:right;
            Vector3 hint=Vector3.Lerp(i==0?leftKnee:rightKnee,target+root.forward*(.40f*rigScale),weight);
            PoseMath.Arm(thigh[i],calf[i],foot[i],target,hint);
            foot[i].rotation=i==0?leftRot:rightRot;
        }
        FootDrift=Mathf.Max(Vector3.Distance(left,foot[0].position),Vector3.Distance(right,foot[1].position));
        var initial=fore.localToWorldMatrix*asset.shieldMount;
        Vector3 center=Vector3.Lerp(initial.GetColumn(3),root.TransformPoint(new Vector3(-.10f,EyeHeight-.32f,.35f)),weight);
        // Cant the outer corner up toward the head while the inner corner stays
        // below the eyes. The broad plate covers the sternum, not empty space.
        Quaternion desired=root.rotation*Quaternion.Euler(-5,-8,-24)*PoseMath.AlongBlade(Vector3.up,Vector3.forward);
        Quaternion rotation=Quaternion.Slerp(initial.rotation,desired,weight);
        Quaternion chestFrame=spine.rotation*chestFrameOffset;
        // Adjust the IK target, never detach or independently slide the shield.
        // Separate conservative volumes for the plate and rear fittings permit
        // an enarme beside the chest, while still rejecting any armor overlap.
        for(int pass=0;pass<12;pass++)
        {
            SolveArm(center,rotation);
            Measure(chestFrame);
            bool clear=ChestGap>=.018f&&SightlineClear;
            if(clear)break;
            if(ChestGap<.018f)center+=chestFrame*Vector3.forward*(.018f-ChestGap+.006f);
            if(!SightlineClear)center-=root.right*.018f;
        }
    }

    void SolveArm(Vector3 center,Quaternion shieldRotation)
    {
        Quaternion foreRotation=shieldRotation*Quaternion.Inverse(asset.shieldMount.rotation);
        Vector3 elbowGoal=center-foreRotation*Vector3.Scale(asset.shieldMount.GetColumn(3),fore.lossyScale);
        Vector3 axis=fore.position-upper.position;
        Vector3 delta=elbowGoal-upper.position;
        if(delta.sqrMagnitude>.00001f)
            upper.rotation=Quaternion.FromToRotation(axis,delta.normalized*axis.magnitude)*upper.rotation;
        fore.rotation=foreRotation;
    }

    void Measure(Quaternion chestFrame)
    {
        var shield=fore.localToWorldMatrix*asset.shieldMount;
        Quaternion inverse=Quaternion.Inverse(chestFrame);
        ChestGap=float.MaxValue;ShieldTop=float.MinValue;
        foreach(var vertex in shieldVertices)
        {
            Vector3 world=shield.MultiplyPoint3x4(vertex);
            ShieldTop=Mathf.Max(ShieldTop,root.InverseTransformPoint(world).y);
        }
        foreach(var volume in shieldVolumes)
        {
            Bounds box=new Bounds();bool first=true;
            foreach(var vertex in volume)
            {Vector3 p=inverse*(shield.MultiplyPoint3x4(vertex)-spine.position);
                if(first){box=new Bounds(p,Vector3.zero);first=false;}else box.Encapsulate(p);}
            float x=Mathf.Max(chest.min.x-box.max.x,box.min.x-chest.max.x);
            float y=Mathf.Max(chest.min.y-box.max.y,box.min.y-chest.max.y);
            float z=box.min.z-chest.max.z;
            ChestGap=Mathf.Min(ChestGap,Mathf.Max(x,Mathf.Max(y,z)));
        }
        // Both eyes must see past the plate, with a small peripheral margin.
        SightlineClear=true;
        for(int eyeIndex=0;eyeIndex<2;eyeIndex++)
        {
            float offset=eyeIndex==0?-.029f:.029f;
            Vector3 eye=head.TransformPoint(eyeLocal)+root.right*(offset*rigScale);
            Vector3 o=shield.inverse.MultiplyPoint3x4(eye),d=shield.inverse.MultiplyVector(root.forward);
            if(Mathf.Abs(d.z)<.001f)continue;
            float t=(.045f-o.z)/d.z;if(t<=0)continue;
            Vector3 p=o+d*t;
            float v=(.285f-p.x)/.675f;
            // A bounding rectangle is stricter than the tapered heater outline.
            if(v>=-.025f&&v<=1.025f&&Mathf.Abs(p.y)<.255f)SightlineClear=false;
        }
        Vector3 sternum=spine.position+chestFrame*new Vector3(0,chest.center.y+.04f*rigScale,chest.max.z);
        Vector3 origin=shield.inverse.MultiplyPoint3x4(sternum);
        Vector3 direction=shield.inverse.MultiplyVector(root.forward);
        float hit=Mathf.Abs(direction.z)>.001f?(.045f-origin.z)/direction.z:-1;
        Vector3 point=origin+direction*hit;float along=(.285f-point.x)/.675f;
        TorsoProbeLocal=point;
        float width=along<.25f?.235f:along<.5f?Mathf.Lerp(.235f,.190f,(along-.25f)*4):along<.75f?Mathf.Lerp(.190f,.108f,(along-.5f)*4):Mathf.Lerp(.108f,.005f,(along-.75f)*4);
        CoversTorso=hit>0&&along>=0&&along<=1&&Mathf.Abs(point.y)<width-.012f;
    }
}
