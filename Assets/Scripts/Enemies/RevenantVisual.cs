using System.Collections.Generic;
using UnityEngine;

// Shared PBR skin variants and skeleton; no instantiated materials or per-frame
// mesh generation. Optional alpha hair uses one extra draw near the camera.
public sealed class RevenantVisual : MonoBehaviour
{
    static RevenantAsset roster;
    public static RevenantAsset Roster=>roster!=null?roster:roster=Resources.Load<RevenantAsset>("Revenants/Roster");
    public SkinnedMeshRenderer Body {get;private set;}
    public RevenantAsset.Form Form {get;private set;}
    public Transform SwordHand {get;private set;}
    public Transform ShieldHand=>leftHand;
    Transform leftUpper,leftFore,leftHand;
    EnemyController brain;
    Camera view;
    float nextDistanceCheck;
    Transform chest;
    float reactionAt=-100;
    Vector3 reactionAxis;
    int level;
    MeshRenderer hair;
    public RevenantGrounding Grounding {get;private set;}
    public RevenantInjuries Injuries {get;private set;}
    public int HairStyle {get;private set;}=-1;
    public int DetailLevel=>level;
    public Vector3 BladeBase=>SwordHand.TransformPoint(Form.bladeBase);
    public Vector3 BladeTip=>SwordHand.TransformPoint(Form.bladeTip);
    public void SampleBlade(int style,float time,out Vector3 start,out Vector3 end)
    {
        Form.blades[style].Sample(time,out start,out end);
        Vector3 correction=Vector3.up*(Grounding!=null?Grounding.BodyOffset:0);
        start=transform.TransformPoint(start)+correction;end=transform.TransformPoint(end)+correction;
    }
    public static bool Armed(ZombieType type)=>type==ZombieType.Swordsman||type==ZombieType.Bulwark||type==ZombieType.Warlord;
    public static float SegmentDistanceSquared(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        Vector3 u=b-a,v=d-c,r=a-c;
        float aa=Vector3.Dot(u,u),ee=Vector3.Dot(v,v),bb=Vector3.Dot(u,v),cc=Vector3.Dot(u,r),ff=Vector3.Dot(v,r),s=0,t=0;
        if(aa<.000001f&&ee<.000001f)return r.sqrMagnitude;
        if(aa<.000001f)t=Mathf.Clamp01(ff/ee);
        else if(ee<.000001f)s=Mathf.Clamp01(-cc/aa);
        else
        {
            float denominator=aa*ee-bb*bb;
            if(denominator>.000001f)s=Mathf.Clamp01((bb*ff-cc*ee)/denominator);
            t=(bb*s+ff)/ee;
            if(t<0){t=0;s=Mathf.Clamp01(-cc/aa);}else if(t>1){t=1;s=Mathf.Clamp01((bb-cc)/aa);}
        }
        return (a+u*s-c-v*t).sqrMagnitude;
    }

    public static RevenantVisual Install(GameObject root,ZombieType type)
    {
        var data=Roster;
        if(data==null)throw new System.InvalidOperationException("Import the Blender Revenant roster before spawning enemies");
        var visual=root.GetComponent<RevenantVisual>()??root.AddComponent<RevenantVisual>();
        visual.Form=data.Get(type);
        if(visual.Body==null)
        {
            foreach(var old in root.GetComponentsInChildren<Renderer>())old.enabled=false;
            var transforms=new Dictionary<string,Transform>();
            foreach(var t in root.GetComponentsInChildren<Transform>())transforms[t.name]=t;
            var bones=new Transform[data.bones.Length];
            for(int i=0;i<bones.Length;i++)bones[i]=transforms[data.bones[i]];
            var child=new GameObject("Blender Revenant");child.transform.SetParent(root.transform,false);
            visual.Body=child.AddComponent<SkinnedMeshRenderer>();
            visual.Body.bones=bones;visual.Body.rootBone=transforms["Base HumanPelvis"];
            visual.Body.sharedMaterial=data.skinSurfaces!=null&&data.skinSurfaces.Length>0?data.skinSurfaces[Random.Range(0,data.skinSurfaces.Length)]:data.surface;visual.Body.quality=SkinQuality.Bone4;
            visual.Body.updateWhenOffscreen=false;
            visual.SwordHand=transforms["Base HumanRArmPalm"];
            visual.leftUpper=transforms["Base HumanLArmUpperarm"];visual.leftFore=transforms["Base HumanLArmForearm"];visual.leftHand=transforms["Base HumanLArmPalm"];
            visual.brain=root.GetComponent<EnemyController>();
            visual.chest=transforms["Base HumanRibcage"];
            visual.Grounding=root.GetComponent<RevenantGrounding>()??root.AddComponent<RevenantGrounding>();
            visual.Grounding.Bind(visual,transforms);
            visual.Injuries=root.GetComponent<RevenantInjuries>()??root.AddComponent<RevenantInjuries>();
            visual.Injuries.Bind(visual,transforms);
            var block=new MaterialPropertyBlock();
            var cloth=new[]{Color.white,new Color(.86f,.50f,.34f),new Color(.52f,.64f,.43f),new Color(.50f,.47f,.43f),new Color(.65f,.32f,.28f)};
            block.SetColor("_ClothTint",type==ZombieType.Warlord?Color.white:cloth[Random.Range(0,cloth.Length)]);
            block.SetColor("_SkinTint",Color.Lerp(new Color(.88f,.91f,.89f),new Color(1.03f,.94f,.86f),Random.value));visual.Body.SetPropertyBlock(block);
            int hairCount=visual.Form.hair!=null?visual.Form.hair.Length:0;
            visual.HairStyle=hairCount==0||Random.value<.18f?-1:Random.Range(0,hairCount);
            if(visual.HairStyle>=0)
            {
                var head=transforms["Base HumanHead"];var hairObject=new GameObject("Matted hair and beard");hairObject.transform.SetParent(head,false);
                hairObject.AddComponent<MeshFilter>().sharedMesh=visual.Form.hair[visual.HairStyle];visual.hair=hairObject.AddComponent<MeshRenderer>();visual.hair.sharedMaterial=data.hairSurface!=null?data.hairSurface:data.surface;
                var tint=new[]{new Color(.42f,.43f,.46f),Color.white,new Color(1.3f,1.5f,1.8f),new Color(.85f,.64f,.40f)};
                var hairBlock=new MaterialPropertyBlock();hairBlock.SetColor("_HairTint",tint[Random.Range(0,tint.Length)]);visual.hair.SetPropertyBlock(hairBlock);
            }
        }
        visual.Body.sharedMesh=visual.Form.mesh;
        visual.Body.localBounds=new Bounds(new Vector3(0,1,0),new Vector3(2.8f,2.8f,2.8f));
        var animator=root.GetComponent<Animator>();
        if(animator!=null&&visual.Form.controller!=null)
        {
            animator.avatar=data.avatar;
            animator.runtimeAnimatorController=visual.Form.controller;
            animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
            animator.Rebind();
            animator.Play("Locomotion",0,Random.value);
            animator.Update(0);
        }
        return visual;
    }
    void LateUpdate()
    {
        if(Body==null)return;
        if(Time.time>=nextDistanceCheck)
        {
            nextDistanceCheck=Time.time+.23f;
            if(view==null)view=Camera.main;
            if(view!=null)
            {
                float distance=(view.transform.position-transform.position).sqrMagnitude;
                // Hysteresis avoids switching repeatedly at a threshold.
                int wanted=distance>(level==2?34*34:39*39)?2:distance>(level>0?16*16:20*20)?1:0;
                var mesh=wanted==2?Form.lod2:wanted==1?Form.lod1:Form.mesh;
                if(mesh!=null&&Body.sharedMesh!=mesh){Body.sharedMesh=mesh;level=wanted;}
                if(hair!=null)hair.enabled=level<2;
            }
        }
        if(Form.type==ZombieType.Bulwark&&brain!=null&&!brain.IsDead&&!brain.IsAttacking&&!brain.GuardRaised)
        {
            PoseMath.ArmStable(leftUpper,leftFore,leftHand,transform.TransformPoint(new Vector3(-.34f,1.00f,.22f)),transform.TransformPoint(new Vector3(-.52f,1.20f,.02f)));
            var desired=transform.rotation*PoseMath.AlongBlade(Vector3.up,Vector3.forward)*Quaternion.Inverse(Form.left.rotation);
            PoseMath.SetWrist(leftFore,leftHand,desired,Form.left.distal,Form.leftWristRest);
        }
        float phase=(Time.time-reactionAt)/.18f;
        if(chest!=null&&brain!=null&&!brain.IsDead&&phase>=0&&phase<1)
        {
            float recoil=Mathf.Sin(phase*Mathf.PI)*Mathf.Exp(-phase*1.25f)*8;
            chest.rotation=Quaternion.AngleAxis(recoil,reactionAxis)*chest.rotation;
        }
    }
    public void React(Vector3 direction)
    {
        reactionAt=Time.time;
        reactionAxis=Vector3.Cross(Vector3.up,Vector3.ProjectOnPlane(direction,Vector3.up)).normalized;
        if(reactionAxis.sqrMagnitude<.1f)reactionAxis=transform.right;
    }
}
