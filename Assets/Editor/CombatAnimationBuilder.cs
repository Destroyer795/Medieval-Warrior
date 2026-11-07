using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Authored, baked multi-joint attack clips, not six offsets of one old swing.
public static class CombatAnimationBuilder
{
    struct Pose
    {
        public Vector3 hand, tip, torso;
        public Pose(Vector3 hand, Vector3 tip, Vector3 torso) { this.hand=hand; this.tip=tip; this.torso=torso; }
    }
    static Vector3 V(float x,float y,float z) => new Vector3(x,y,z);
    static float Ease(float t){t=Mathf.Clamp01(t);return t*t*t*(t*(t*6-15)+10);}
    static Vector3 Hermite(Vector3 a,Vector3 b,Vector3 va,Vector3 vb,float t,float span)
    {
        t=Mathf.Clamp01(t);float s=t*t;
        return (2*s*t-3*s+1)*a+(s*t-2*s+t)*va*span+(-2*s*t+3*s)*b+(s*t-s)*vb*span;
    }
    static Vector3 Flight(Vector3 rest,Vector3 wind,Vector3 contact,Vector3 follow,float phase,SwordMoves.Move move)
    {
        float windPhase=move.windup/move.duration,contactPhase=move.contact/move.duration,followPhase=move.followThrough/move.duration;
        Vector3 through=(follow-wind)/(followPhase-windPhase);
        if(phase<windPhase)return Vector3.Lerp(rest,wind,Ease(phase/windPhase));
        if(phase<contactPhase)return Hermite(wind,contact,Vector3.zero,through,
            (phase-windPhase)/(contactPhase-windPhase),contactPhase-windPhase);
        if(phase<followPhase)return Hermite(contact,follow,through,Vector3.zero,
            (phase-contactPhase)/(followPhase-contactPhase),followPhase-contactPhase);
        return Vector3.Lerp(follow,rest,Ease((phase-followPhase)/(1-followPhase)));
    }
    static void SmoothQuaternionCurves(AnimationCurve[] curves)
    {
        foreach(var curve in curves)
        {
            var keys=curve.keys;
            for(int i=0;i<keys.Length;i++)
            {
                float tangent=i==0||i==keys.Length-1?0:(keys[i+1].value-keys[i-1].value)/(keys[i+1].time-keys[i-1].time);
                keys[i].inTangent=tangent;keys[i].outTangent=tangent;
            }
            curve.keys=keys;
        }
    }
    static void FilterRotations(AnimationCurve[] curves,float duration,float seconds=.014f)
    {
        int count=curves[0].length;
        var source=Enumerable.Range(0,count).Select(i=>new Quaternion(curves[0][i].value,curves[1][i].value,curves[2][i].value,curves[3][i].value)).ToArray();
        float sigma=seconds*(count-1)/duration;int radius=Mathf.CeilToInt(sigma*3);
        for(int i=1;i<count-1;i++)
        {
            Vector4 sum=Vector4.zero;
            for(int d=-radius;d<=radius;d++)
            {
                var q=source[Mathf.Clamp(i+d,0,count-1)];
                float weight=Mathf.Exp(-.5f*d*d/(sigma*sigma))*(Quaternion.Dot(q,source[i])<0?-1:1);
                sum+=new Vector4(q.x,q.y,q.z,q.w)*weight;
            }
            sum.Normalize();
            float time=i*duration/(count-1);
            float boundary=Mathf.Min(Ease(time/.06f),Ease((duration-time)/.06f));
            var filtered=Quaternion.Slerp(source[i],new Quaternion(sum.x,sum.y,sum.z,sum.w),boundary);
            for(int axis=0;axis<4;axis++)
            {
                var key=curves[axis][i];key.value=filtered[axis];curves[axis].MoveKey(i,key);
            }
        }
    }
    public static void GuardPose(GameObject go)
    {
        Transform Bone(string name)=>go.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 "+name);
        PoseMath.Arm(Bone("L UpperArm"),Bone("L Forearm"),Bone("L Hand"),
            go.transform.TransformPoint(V(-.31f,.86f,.31f)),go.transform.TransformPoint(V(-.49f,.79f,.12f)));
        PoseMath.Arm(Bone("R UpperArm"),Bone("R Forearm"),Bone("R Hand"),
            go.transform.TransformPoint(V(.35f,1.06f,.20f)),go.transform.TransformPoint(V(.53f,.96f,.02f)));
        var data=MarchwardenVisual.Asset;
        if(data!=null)
        {
            PoseMath.SetWrist(Bone("L Forearm"),Bone("L Hand"),go.transform.rotation*
                Quaternion.AngleAxis(-37,Vector3.up)*PoseMath.AlongBlade(Vector3.up,Vector3.back)*Quaternion.Inverse(data.leftPalm.rotation),
                data.leftPalm.rotation*Vector3.up,data.leftWristRest);
            PoseMath.SetWrist(Bone("R Forearm"),Bone("R Hand"),go.transform.rotation*
                PoseMath.AlongBlade(V(.08f,.97f,-.23f),Vector3.right)*Quaternion.Inverse(data.swordMount.rotation),
                data.rightPalm.rotation*Vector3.up,data.rightWristRest);
        }
    }
    static readonly Pose[] Windup = {
        new Pose(V(.39f,1.31f,.05f),V(.12f,.92f,.35f),V(-3,-13,-3)),
        new Pose(V(.09f,1.34f,.32f),V(-.52f,.72f,.45f),V(-3,17,3)),
        new Pose(V(.23f,1.43f,.04f),V(0,.95f,.30f),V(-8,-6,0)),
        new Pose(V(.41f,.93f,.15f),V(.35f,-.45f,.82f),V(4,-16,-4)),
        new Pose(V(.43f,1.27f,.05f),V(.72f,.10f,.55f),V(-1,-18,1)),
        new Pose(V(.32f,1.10f,.12f),V(.05f,.15f,.98f),V(2,-10,0)),
        new Pose(V(.08f,1.38f,.31f),V(-.38f,.76f,.50f),V(-4,17,4)),
        new Pose(V(.10f,.97f,.39f),V(-.55f,-.15f,.78f),V(3,16,2)),
        new Pose(V(.30f,1.29f,.08f),V(0,.20f,.98f),V(-3,-9,0)),
        new Pose(V(.43f,1.15f,.11f),V(.60f,.42f,.68f),V(0,-10,-2)),
        new Pose(V(.10f,1.29f,.40f),V(-.55f,.40f,.73f),V(0,18,2)),
        new Pose(V(-.02f,1.42f,.22f),V(-.22f,.90f,.37f),V(-6,15,3))
    };
    static readonly Pose[] Contact = {
        new Pose(V(.12f,1.04f,.48f),V(-.6f,-.45f,.65f),V(5,20,5)),
        new Pose(V(.25f,1.12f,.44f),V(.75f,.15f,.65f),V(1,-23,-4)),
        new Pose(V(.08f,1.09f,.49f),V(0,-.65f,.76f),V(15,4,0)),
        new Pose(V(.09f,1.20f,.47f),V(-.35f,.65f,.68f),V(-3,24,5)),
        new Pose(V(.03f,1.28f,.48f),V(-.8f,0,.6f),V(0,27,-3)),
        new Pose(V(.08f,1.13f,.54f),V(0,.03f,1),V(8,9,0)),
        new Pose(V(.27f,.98f,.43f),V(.6f,-.6f,.55f),V(8,-22,-5)),
        new Pose(V(.33f,.95f,.43f),V(.7f,.3f,.6f),V(-2,-22,-4)),
        new Pose(V(.08f,1.36f,.49f),V(-.03f,.15f,1),V(4,7,0)),
        new Pose(V(.20f,1.14f,.47f),V(-.45f,-.25f,.85f),V(4,17,4)),
        new Pose(V(.35f,1.11f,.37f),V(.6f,.1f,.8f),V(2,-25,-6)),
        new Pose(V(.31f,1.01f,.44f),V(.5f,-.7f,.55f),V(13,-19,-3))
    };

    [MenuItem("Medieval Warrior/Rebuild sword animations")]
    public static void Build()
    {
        const string dir="Assets/Resources/Animations/Sword";
        Directory.CreateDirectory(dir);
        AssetDatabase.Refresh();
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(MarchwardenBuilder.Source);
        var go=UnityEngine.Object.Instantiate(asset); go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/KnightController.controller");
        var idle=controller.animationClips.First(c=>c.name.Contains("05_combat_idle"));
        Transform Find(string name) => go.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 "+name);
        var spine=Find("Spine"); var upper=Find("R UpperArm"); var fore=Find("R Forearm"); var hand=Find("R Hand");
        var lUpper=Find("L UpperArm"); var lFore=Find("L Forearm"); var lHand=Find("L Hand");
        var head=Find("Head");
        var tracked=new[]{spine, upper,fore,hand,lUpper,lFore,lHand,head};
        var data=MarchwardenVisual.Asset;
        Quaternion gripOffset=data.swordMount.rotation;
        idle.SampleAnimation(go,0);GuardPose(go);
        var guard=new AnimationClip {name="Shield guard",frameRate=60,wrapMode=WrapMode.Loop};
        foreach(var bone in new[]{lUpper,lFore,lHand,upper,fore,hand})
            for(int axis=0;axis<4;axis++)
                AnimationUtility.SetEditorCurve(guard,EditorCurveBinding.FloatCurve(
                    AnimationUtility.CalculateTransformPath(bone,go.transform),typeof(Transform),"m_LocalRotation."+"xyzw"[axis]),
                    AnimationCurve.Constant(0,1,bone.localRotation[axis]));
        string guardPath=dir+"/Guard.anim";
        var oldGuard=AssetDatabase.LoadAssetAtPath<AnimationClip>(guardPath);
        if(oldGuard!=null){EditorUtility.CopySerialized(guard,oldGuard);UnityEngine.Object.DestroyImmediate(guard);EditorUtility.SetDirty(oldGuard);}
        else AssetDatabase.CreateAsset(guard,guardPath);
        for(int variant=0;variant<SwordMoves.Count;variant++)
        {
            var move=SwordMoves.All[variant];
            var curves=tracked.Select(_=>new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()}).ToArray();
            idle.SampleAnimation(go,0);GuardPose(go);Vector3 restHand=hand.position;
            Quaternion restHandRot=hand.rotation; Vector3 restLeft=lHand.position;
            var previous=tracked.Select(t=>t.localRotation).ToArray();
            for(int frame=0;frame<=96;frame++)
            {
                float phase=frame/96f,time=phase*move.duration;
                idle.SampleAnimation(go,0);
                GuardPose(go);
                Quaternion restHead=head.rotation;
                float contactPhase=move.contact/move.duration;
                float windPhase=move.windup/move.duration,followPhase=move.followThrough/move.duration;
                float rise=Ease(phase/windPhase);
                float recover=Ease(Mathf.InverseLerp(followPhase,1,phase));
                Pose start=Windup[variant], hit=Contact[variant];
                Vector3 endHand=hit.hand+(hit.hand-start.hand).normalized*(move.thrust?.065f:.095f);
                Vector3 handTarget=Flight(restHand,start.hand,hit.hand,endHand,phase,move);
                Vector3 torso=Flight(Vector3.zero,start.torso,hit.torso*.80f,hit.torso*.85f,phase,move);
                spine.rotation=Quaternion.Euler(torso)*spine.rotation;
                head.rotation=Quaternion.Slerp(restHead,head.rotation,.35f);
                // Smoothly approach full extension without locking the elbow
                // against a hard IK reach clamp at the fastest part of the cut.
                Vector3 reach=handTarget-upper.position;
                float maximum=(Vector3.Distance(upper.position,fore.position)+Vector3.Distance(fore.position,hand.position))*.985f;
                float soft=maximum*.88f;
                if(reach.magnitude>soft)handTarget=upper.position+reach.normalized*(soft+(maximum-soft)*(1-Mathf.Exp(-(reach.magnitude-soft)/(maximum-soft))));
                Vector3 elbowHint=Vector3.Lerp(fore.position,V(.55f,1.13f,-.11f),rise);
                PoseMath.ArmStable(upper,fore,hand,handTarget,elbowHint);
                Vector3 plane=move.thrust?Vector3.right:Vector3.Cross(start.tip,hit.tip).normalized;
                if(plane.sqrMagnitude<.1f)plane=Vector3.right;
                // A double-edged blade has equivalent +/- face normals. Keep
                // the one nearest the ready guard to avoid an unnecessary
                // half-turn of the wrist when changing cutting direction.
                if(Vector3.Dot(plane,Vector3.right)<0)plane=-plane;
                Quaternion windRotation=PoseMath.AlongBlade(start.tip,plane)*Quaternion.Inverse(gripOffset);
                Quaternion contactRotation=PoseMath.AlongBlade(hit.tip,plane)*Quaternion.Inverse(gripOffset);
                Vector3 followBlade=move.thrust?hit.tip:Quaternion.AngleAxis(18,plane)*hit.tip;
                Quaternion followRotation=PoseMath.AlongBlade(followBlade,plane)*Quaternion.Inverse(gripOffset);
                if(phase<windPhase)hand.rotation=Quaternion.Slerp(restHandRot,windRotation,rise);
                else if(phase<contactPhase)hand.rotation=Quaternion.Slerp(windRotation,contactRotation,Mathf.InverseLerp(windPhase,contactPhase,phase));
                else if(phase<followPhase)hand.rotation=Quaternion.Slerp(contactRotation,followRotation,Mathf.InverseLerp(contactPhase,followPhase,phase));
                else hand.rotation=Quaternion.Slerp(followRotation,restHandRot,recover);
                PoseMath.SetWrist(fore,hand,hand.rotation,gripOffset*Vector3.up,data.rightWristRest);
                // Open the shield to the left before the cut begins. Returning
                // cuts wind up above/in front of its rim, not behind its face.
                float shieldOpen=Ease(Mathf.InverseLerp(0,.14f,phase))*(1-Ease(Mathf.InverseLerp(.82f,1,phase)));
                Vector3 shieldTarget=restLeft+V(-.22f,-.035f,-.13f)*shieldOpen;
                Vector3 leftHint=Vector3.Lerp(lFore.position,V(-.58f,1.00f,-.18f),shieldOpen);
                PoseMath.ArmStable(lUpper,lFore,lHand,shieldTarget,leftHint);
                for(int i=0;i<tracked.Length;i++)
                {
                    Quaternion q=tracked[i].localRotation;
                    if(Quaternion.Dot(q,previous[i])<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                    previous[i]=q;
                    for(int axis=0;axis<4;axis++) curves[i][axis].AddKey(time,q[axis]);
                }
            }
            string path=$"{dir}/Strike_{variant:00}.anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
            clip.ClearCurves(); clip.name=$"Strike_{variant:00}"; clip.frameRate=60; clip.wrapMode=WrapMode.Once;
            for(int i=0;i<tracked.Length;i++)
            {
                FilterRotations(curves[i],move.duration,tracked[i]==upper||tracked[i]==fore?.020f:.014f);
                SmoothQuaternionCurves(curves[i]);
                for(int axis=0;axis<4;axis++)
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(
                        AnimationUtility.CalculateTransformPath(tracked[i],go.transform),typeof(Transform),"m_LocalRotation."+"xyzw"[axis]),curves[i][axis]);
            }
            clip.EnsureQuaternionContinuity();
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
            EditorUtility.SetDirty(clip);
        }
        UnityEngine.Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[CombatArt] Baked 12 distinct multi-joint sword attacks with authored anticipation and recovery");
    }
}
