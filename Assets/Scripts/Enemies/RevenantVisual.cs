using System.Collections.Generic;
using UnityEngine;

// Shared PBR skin variants and skeleton; no instantiated materials or per-frame
// mesh generation. Hair or a fitted helmet adds one rigid draw near the camera;
// armed forms add one palm-mounted sword draw.
public sealed class RevenantVisual : MonoBehaviour
{
    static RevenantAsset roster;
    public static void CacheRoster(RevenantAsset asset)
    {
        if(asset==null||asset.forms==null||asset.forms.Length!=ZombieTypes.Count)
            throw new System.InvalidOperationException("Import the complete Revenant roster before preparing battle");
        roster=asset;
    }
    public static RevenantAsset Roster
    {
        get
        {
            if(roster==null)
            {
                using var timing=BattleLoadMetrics.Measure("Revenant roster synchronous load");
                roster=Resources.Load<RevenantAsset>("Revenants/Roster");
            }
            return roster;
        }
    }
    public SkinnedMeshRenderer Body {get;private set;}
    public RevenantAsset.Form Form {get;private set;}
    public Transform SwordHand {get;private set;}
    public Transform ShieldHand=>leftHand;
    Transform leftUpper,leftFore,leftHand;
    EnemyController brain;
    Animator rigAnimator;
    Camera view;
    float nextDistanceCheck;
    Transform chest;
    float reactionAt=-100;
    Vector3 reactionAxis;
    int level;
    MeshRenderer hair,helmet,sword;
    bool hidden;
    Transform headMount;
    public RevenantGrounding Grounding {get;private set;}
    public RevenantInjuries Injuries {get;private set;}
    public int HairStyle {get;private set;}=-1;
    public int HelmetStyle {get;private set;}=-1;
    public int SwordStyle {get;private set;}=-1;
    public int OutfitPalette {get;private set;}
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
        using var timing=BattleLoadMetrics.Measure(BattleLoadMetrics.Enabled?"Revenant install "+type:null);
        var data=Roster;
        if(data==null)throw new System.InvalidOperationException("Import the Blender Revenant roster before spawning enemies");
        var visual=root.GetComponent<RevenantVisual>()??root.AddComponent<RevenantVisual>();
        bool refresh=visual.Form!=data.Get(type)||visual.Body==null;
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
            visual.headMount=transforms["Base HumanHead"];
            visual.leftUpper=transforms["Base HumanLArmUpperarm"];visual.leftFore=transforms["Base HumanLArmForearm"];visual.leftHand=transforms["Base HumanLArmPalm"];
            visual.brain=root.GetComponent<EnemyController>();
            visual.rigAnimator=root.GetComponent<Animator>();
            visual.chest=transforms["Base HumanRibcage"];
            visual.Grounding=root.GetComponent<RevenantGrounding>()??root.AddComponent<RevenantGrounding>();
            visual.Grounding.Bind(visual,transforms);
            visual.Injuries=root.GetComponent<RevenantInjuries>()??root.AddComponent<RevenantInjuries>();
            visual.Injuries.Bind(visual,transforms);
        }
        if(refresh)visual.Dress(data,type);
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

    // Paired historical dyes remain readable under night lighting. Primary,
    // trousers and facings have independent atlas tiles, never multiply to black.
    static readonly Color[,] outfits={
        {new Color(.47f,.62f,.73f),new Color(.59f,.36f,.24f),new Color(.81f,.72f,.52f)},
        {new Color(.72f,.32f,.24f),new Color(.48f,.49f,.39f),new Color(.82f,.69f,.43f)},
        {new Color(.72f,.58f,.28f),new Color(.38f,.43f,.49f),new Color(.72f,.43f,.27f)},
        {new Color(.43f,.57f,.35f),new Color(.70f,.62f,.47f),new Color(.57f,.29f,.22f)},
        {new Color(.59f,.43f,.57f),new Color(.51f,.50f,.43f),new Color(.79f,.72f,.53f)},
        {new Color(.80f,.74f,.57f),new Color(.37f,.47f,.57f),new Color(.62f,.34f,.22f)},
        {new Color(.66f,.39f,.24f),new Color(.46f,.55f,.41f),new Color(.75f,.65f,.44f)},
        {new Color(.45f,.54f,.58f),new Color(.66f,.46f,.35f),new Color(.76f,.42f,.31f)}
    };
    void Dress(RevenantAsset data,ZombieType type)
    {
        hidden=false;Body.enabled=true;
        Body.sharedMaterial=data.skinSurfaces[Random.Range(0,data.skinSurfaces.Length)];
        OutfitPalette=Random.Range(0,outfits.GetLength(0));
        var block=new MaterialPropertyBlock();
        block.SetColor("_ClothTint",outfits[OutfitPalette,0]);
        block.SetColor("_TrouserTint",outfits[OutfitPalette,1]);
        block.SetColor("_FacingTint",outfits[OutfitPalette,2]);
        block.SetColor("_SkinTint",Color.Lerp(new Color(.87f,.94f,.86f),new Color(1.06f,.92f,.86f),Random.value));
        block.SetColor("_MetalTint",Color.Lerp(new Color(.68f,.74f,.78f),new Color(1.05f,.88f,.65f),Random.value));
        Body.SetPropertyBlock(block);
        float helmetChance=type==ZombieType.Warlord?1:Armed(type)?.85f:type==ZombieType.Brute?.3f:.20f;
        HelmetStyle=Form.helmets!=null&&Form.helmets.Length>0&&Random.value<helmetChance?Random.Range(0,Form.helmets.Length):-1;
        SwordStyle=Armed(type)&&Form.swords!=null&&Form.swords.Length>0?Random.Range(0,Form.swords.Length):-1;
        Mount(ref helmet,"Fitted interchangeable helmet",headMount,HelmetStyle>=0?Form.helmets[HelmetStyle]:null,Body.sharedMaterial,block);
        Mount(ref sword,"Palm mounted interchangeable sword",SwordHand,SwordStyle>=0?Form.swords[SwordStyle]:null,Body.sharedMaterial,block);
        HairStyle=Form.hair==null||Form.hair.Length==0||Random.value<.14f?-1:Random.Range(0,Form.hair.Length);
        var hairBlock=new MaterialPropertyBlock();
        hairBlock.SetColor("_HairTint",Color.Lerp(new Color(.52f,.43f,.34f),new Color(1.50f,1.54f,1.57f),Random.value));
        Mount(ref hair,"Matted hair and beard",headMount,HairStyle>=0?Form.hair[HairStyle]:null,data.hairSurface,hairBlock);
        if(hair!=null)hair.enabled=HairStyle>=0&&HelmetStyle<0;
        level=0;
    }
    static void Mount(ref MeshRenderer renderer,string name,Transform bone,Mesh mesh,Material material,MaterialPropertyBlock block)
    {
        if(renderer==null)
        {
            var child=new GameObject(name);child.transform.SetParent(bone,false);
            child.AddComponent<MeshFilter>();renderer=child.AddComponent<MeshRenderer>();
        }
        renderer.GetComponent<MeshFilter>().sharedMesh=mesh;renderer.sharedMaterial=material;
        renderer.SetPropertyBlock(block);renderer.enabled=mesh!=null;
    }
    void LateUpdate()
    {
        if(Body==null||hidden)return;
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
                if(hair!=null)hair.enabled=level<2&&HairStyle>=0&&HelmetStyle<0;
                if(helmet!=null)helmet.enabled=level<2&&HelmetStyle>=0;
            }
        }
        if(Form.type==ZombieType.Bulwark&&brain!=null&&!brain.IsDead&&!brain.IsAttacking&&!brain.GuardRaised)
        {
            PoseMath.ArmStable(leftUpper,leftFore,leftHand,transform.TransformPoint(new Vector3(-.34f,1.00f,.22f)),transform.TransformPoint(new Vector3(-.52f,1.20f,.02f)));
            var desired=transform.rotation*PoseMath.AlongBlade(Vector3.up,Vector3.forward)*Quaternion.Inverse(Form.left.rotation);
            PoseMath.SetWrist(leftFore,leftHand,desired,Form.left.distal,Form.leftWristRest);
        }
        float phase=(Time.time-reactionAt)/.18f;
        bool freshPose=Time.timeScale>0&&Time.deltaTime>0&&rigAnimator!=null&&rigAnimator.enabled&&(Body.isVisible||rigAnimator.cullingMode==AnimatorCullingMode.AlwaysAnimate);
        if(freshPose&&chest!=null&&brain!=null&&!brain.IsDead&&phase>=0&&phase<1)
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
    public void HideSurfaces()
    {
        hidden=true;
        if(Body!=null)Body.enabled=false;
        if(hair!=null)hair.enabled=false;
        if(helmet!=null)helmet.enabled=false;
        if(sword!=null)sword.enabled=false;
    }
}
