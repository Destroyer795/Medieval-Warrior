using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Generic, baked skeletal animation. Does not mix humanoid muscle curves with
// Transform curves: all joints, pelvis travel and foot contacts are explicit.
public static class RevenantAnimationBuilder
{
    const string Dir="Assets/Resources/Revenants/Animations";
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static float Ease(float x){x=Mathf.Clamp01(x);return x*x*(3-2*x);}
    static Vector3 Flight(Vector3 rest,Vector3 wind,Vector3 contact,Vector3 follow,float time,float impact)
    {
        float release=impact-.14f,carry=impact+.14f,end=impact+.55f;
        if(time<release)return Vector3.Lerp(rest,wind,Ease(time/release));
        if(time<carry)
        {
            float t=(time-release)/(carry-release);
            return t<.5f?Vector3.Lerp(wind,contact,t*2):Vector3.Lerp(contact,follow,(t-.5f)*2);
        }
        return Vector3.Lerp(follow,rest,Ease((time-carry)/(end-carry)));
    }
    static AnimationClip Save(AnimationClip clip,string label)
    {
        clip.name=label;clip.frameRate=60;clip.EnsureQuaternionContinuity();
        string path=Dir+"/"+label+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(existing==null)AssetDatabase.CreateAsset(clip,path);
        else{EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);clip=existing;}
        EditorUtility.SetDirty(clip);return clip;
    }
    [Serializable] public class ActionFrame {public float time;public Matrix4x4[] frames;}
    [Serializable] public class ActionData {public string name,form;public float duration;public ActionFrame[] samples;}
    [Serializable] public class ActionExport {public ActionData[] actions;}
    public static void Build(RevenantAsset data,RevenantBuilder.Rig rig)
    {
        Directory.CreateDirectory(Dir);AssetDatabase.Refresh();var actions=new List<ActionData>();
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RevenantBuilder.Source));
        go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=Vector3.one;
        var animator=go.GetComponent<Animator>();animator.enabled=false;animator.avatar=null;
        var all=go.GetComponentsInChildren<Transform>();
        Transform Bone(string n)=>all.First(t=>t.name=="Base Human"+n);
        var tracked=rig.bones.Select(b=>all.First(t=>t.name==b.name)).ToArray();
        var restRot=tracked.Select(t=>t.localRotation).ToArray();var restPos=tracked.Select(t=>t.localPosition).ToArray();
        var pelvis=Bone("Pelvis");var spine=Bone("Spine2");var head=Bone("Head");
        var ru=Bone("RArmUpperarm");var rf=Bone("RArmForearm");var rh=Bone("RArmPalm");
        var lu=Bone("LArmUpperarm");var lf=Bone("LArmForearm");var lh=Bone("LArmPalm");
        var feet=new[]{Bone("LLegFoot"),Bone("RFoot")};
        var rightRest=rh.localRotation;var leftRest=lh.localRotation;
        using var capture=new RevenantMotionCapture(rig);
        foreach(var name in new[]{"Z_Idle","Z_Walk_InPlace","Z_Walk1_InPlace","Z_Run_InPlace","Z_Attack","Z_FallingBack","Z_FallingForward"})capture.Get(name);
        var soleVertices=Array.Empty<Vector3>();var soleWeights=Array.Empty<BoneWeight>();
        float SoleFloor()
        {
            var matrices=tracked.Select((b,i)=>b.localToWorldMatrix*rig.bones[i].frame.inverse).ToArray();float floor=float.PositiveInfinity;
            for(int i=0;i<soleVertices.Length;i++)
            {
                var p=soleVertices[i];var w=soleWeights[i];
                var q=matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                floor=Mathf.Min(floor,q.y);
            }
            return float.IsFinite(floor)?floor:0;
        }
        void PlantFeet()=>pelvis.position-=Vector3.up*SoleFloor();
        void Reset(){for(int i=0;i<tracked.Length;i++){tracked[i].localRotation=restRot[i];tracked[i].localPosition=restPos[i];}}
        void ArmedArm(Transform u,Transform f,Transform h,Vector3 goal,Vector3 hint,Vector3 blade,RevenantAsset.Grip grip,Quaternion wrist,bool shield=false)
        {
            PoseMath.ArmStable(u,f,h,goal,hint);
            // Choose blade roll from the anatomical palm, rather than forcing
            // its distal axis backwards and letting the wrist limiter turn the
            // edge skyward. The left shield uses its outward-facing grip frame.
            Vector3 palm=Vector3.ProjectOnPlane(h.position-f.position,blade.normalized).normalized;
            float chirality=Mathf.Sign(Vector3.Dot(Vector3.Cross(grip.along,grip.distal),grip.normal));
            Vector3 normal=shield?Vector3.forward:Vector3.Cross(blade.normalized,palm).normalized*chirality;
            PoseMath.SetWrist(f,h,PoseMath.AlongBlade(blade,normal)*Quaternion.Inverse(grip.rotation),grip.distal,wrist);
        }
        AnimationClip Bake(string form,string label,float duration,bool loop,Action<float> pose)
        {
            var clip=new AnimationClip();int frames=Mathf.CeilToInt(duration*60);
            var curves=tracked.Select(t=>Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray()).ToArray();
            var previous=new Quaternion[tracked.Length];var export=new List<ActionFrame>();
            bool strike=label.EndsWith("_Cut")||label.EndsWith("_Thrust");
            bool gait=label.EndsWith("_Walk")||label.EndsWith("_Run");var footSamples=new List<Vector3[]>();
            var bladeStart=new List<Vector3>();var bladeEnd=new List<Vector3>();
            var strikeForm=strike?data.forms.First(f=>f.type.ToString()==form):null;
            for(int i=0;i<=frames;i++)
            {
                float t=duration*i/frames;Reset();pose(t);
                for(int j=0;j<tracked.Length;j++)
                {
                    var q=tracked[j].localRotation;
                    if(i>0&&Quaternion.Dot(q,previous[j])<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                    previous[j]=q;
                    for(int axis=0;axis<4;axis++)curves[j][axis].AddKey(t,q[axis]);
                    var p=tracked[j].localPosition;for(int axis=0;axis<3;axis++)curves[j][axis+4].AddKey(t,p[axis]);
                }
                if(i%2==0||i==frames)export.Add(new ActionFrame{time=t,frames=tracked.Select(b=>b.localToWorldMatrix).ToArray()});
                if(strike){bladeStart.Add(rh.TransformPoint(strikeForm.bladeBase));bladeEnd.Add(rh.TransformPoint(strikeForm.bladeTip));}
                if(gait)footSamples.Add(feet.Select(f=>f.position).ToArray());
            }
            for(int j=0;j<tracked.Length;j++)
            {
                string path=AnimationUtility.CalculateTransformPath(tracked[j],go.transform);
                for(int c=0;c<7;c++)
                {
                    var curve=curves[j][c];
                    // Curves with constant bind offsets need only two keys.
                    if(curve.keys.All(k=>Mathf.Abs(k.value-curve[0].value)<.00001f))curve=AnimationCurve.Linear(0,curve[0].value,duration,curve[0].value);
                    for(int k=0;k<curve.length;k++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.ClampedAuto);
                    }
                    string property=c<4?"m_LocalRotation."+"xyzw"[c]:"m_LocalPosition."+"xyz"[c-4];
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
                }
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;settings.startTime=0;settings.stopTime=duration;AnimationUtility.SetAnimationClipSettings(clip,settings);
            if(strike)
            {
                strikeForm.blades??=new RevenantAsset.BladeTrack[2];
                strikeForm.blades[label.EndsWith("_Cut")?0:1]=new RevenantAsset.BladeTrack{duration=duration,start=bladeStart.ToArray(),end=bladeEnd.ToArray()};
            }
            if(gait)
            {
                // Measure the backwards speed of the low, supporting foot.
                // A slow shamble and a run have very different authored strides.
                var velocities=new List<float>();float dt=duration/frames;
                for(int side=0;side<2;side++)
                {
                    float floor=footSamples.Min(p=>p[side].y);
                    for(int i=1;i<footSamples.Count;i++)
                    {
                        float footSpeed=(footSamples[i-1][side].z-footSamples[i][side].z)/dt;
                        if(footSpeed>.1f&&footSamples[i-1][side].y<floor+.045f)velocities.Add(footSpeed);
                    }
                }
                if(velocities.Count==0)throw new Exception("No stance velocity found for "+label);
                velocities.Sort();float speed=velocities[velocities.Count/2];var entry=data.forms.First(f=>f.type.ToString()==form);
                if(label.EndsWith("_Walk"))entry.walkSpeed=speed;else entry.runSpeed=speed;
            }
            actions.Add(new ActionData{name=label,form=form,duration=duration,samples=export.ToArray()});return Save(clip,label);
        }
        var deathClips=new AnimationClip[2];
        for(int n=0;n<2;n++)
        {
            string motion=n==0?"Z_FallingBack":"Z_FallingForward";
            deathClips[n]=Bake("Shared",n==0?"Revenant_DeathBack":"Revenant_DeathForward",capture.Get(motion).length,false,t=>capture.Apply(motion,t,tracked));
        }
        foreach(var form in data.forms)
        {
            bool armed=RevenantVisual.Armed(form.type);float impact=ZombieTypes.Get(form.type).windup;
            var v=form.mesh.vertices;var w=form.mesh.boneWeights;
            var sole=Enumerable.Range(0,v.Length).Where(i=>v[i].y<.11f&&(rig.bones[w[i].boneIndex0].name.Contains("Foot")||rig.bones[w[i].boneIndex0].name.EndsWith("Digit11"))).ToArray();
            soleVertices=sole.Select(i=>v[i]).ToArray();soleWeights=sole.Select(i=>w[i]).ToArray();
            if(soleVertices.Length<8)throw new Exception("Missing anatomical sole probes: "+form.type);
            void Arms(float t,int style,bool attack,float swing=0)
            {
                if(armed)
                {
                    var rest=V(.32f,1.24f,.20f);var wind=style==0?V(.34f,1.57f,.02f):V(.40f,1.22f,.02f);
                    var contact=style==0?V(.07f,1.20f,.43f):V(.16f,1.21f,.46f);var follow=style==0?V(-.16f,1.05f,.28f):V(.13f,1.19f,.49f);
                    if(form.type==ZombieType.Warlord){wind=V(.16f,1.72f,-.02f);contact=V(.12f,1.16f,.39f);follow=V(.10f,1.09f,.34f);}
                    var goal=attack?Flight(rest,wind,contact,follow,t,impact):rest+V(0,.012f*swing,.018f*swing);
                    var bladeRest=V(.05f,.97f,.24f);var bladeWind=style==0?V(.05f,.70f,-.70f):V(.10f,.10f,1);
                    var bladeContact=style==0?V(-.33f,-.12f,.94f):V(0,0,1);var bladeFollow=style==0?V(-.65f,-.64f,.40f):V(0,-.04f,1);
                    if(form.type==ZombieType.Warlord){bladeContact=V(-.10f,-.65f,.75f);bladeFollow=V(-.15f,-.80f,.55f);}
                    var blade=attack?Flight(bladeRest,bladeWind,bladeContact,bladeFollow,t,impact):bladeRest;
                    Vector3 hint=V(.55f,1.25f,.01f);
                    if(attack)
                        hint=Flight(hint,V(.52f,1.42f,.01f),form.type==ZombieType.Warlord?V(.20f,1.14f,0):V(.36f,1.08f,.01f),V(.38f,1.08f,0),t,impact);
                    ArmedArm(ru,rf,rh,goal,hint,blade,form.right,rightRest);
                    var left=V(-.30f,1.25f,.27f);
                    if(attack&&form.type==ZombieType.Bulwark)
                    {
                        float drop=Ease((t-(impact-.18f))/.15f)*(1-Ease((t-impact-.30f)/.25f));left+=V(-.10f,-.18f,-.12f)*drop;
                    }
                    if(form.type==ZombieType.Bulwark)ArmedArm(lu,lf,lh,left,V(-.52f,1.22f,.02f),Vector3.up,form.left,leftRest,true);
                }
                else if(attack&&form.type==ZombieType.Spitter)
                {
                    var right=V(.38f,1.23f,.23f)+V(0,swing*.015f,swing*.05f);var left=V(-.36f,1.25f,.24f)-V(0,swing*.015f,swing*.05f);
                    if(attack)
                    {
                        right=Flight(right,V(.41f,1.52f,.10f),V(.13f,1.20f,.46f),V(-.10f,1.09f,.35f),t,impact);
                        left=Flight(left,V(-.46f,1.39f,.02f),V(-.12f,1.25f,.44f),V(.02f,1.12f,.35f),t+.035f,impact);
                        if(style==1){var old=right;right=V(-left.x,left.y,left.z);left=V(-old.x,old.y,old.z);}
                        if(form.type==ZombieType.Spitter){right=V(.20f,1.33f,.22f);left=V(-.20f,1.33f,.22f);}
                    }
                    PoseMath.ArmStable(ru,rf,rh,right,V(.55f,1.28f,.02f));PoseMath.ArmStable(lu,lf,lh,left,V(-.55f,1.28f,.02f));
                }
            }
            void Attack(float t,int style)
            {
                float sourceTime=t<impact?.60f*t/impact:.60f+.40f*(t-impact)/.55f;
                capture.Apply("Z_Attack",sourceTime,tracked);PlantFeet();
                Arms(t,style,true);
                if(form.type==ZombieType.Spitter)head.rotation=Quaternion.AngleAxis(-18*Mathf.Sin(t/(impact+.55f)*Mathf.PI),Vector3.right)*head.rotation;
            }
            form.attacks=new[]{Bake(form.type.ToString(),form.type+"_Cut",impact+.55f,false,t=>Attack(t,0)),Bake(form.type.ToString(),form.type+"_Thrust",impact+.55f,false,t=>Attack(t,1))};
            var locomotion=new AnimationClip[3];
            for(int gait=0;gait<3;gait++)
            {
                int g=gait;string motion=g==0?"Z_Idle":g==2?"Z_Run_InPlace":form.type==ZombieType.Brute||form.type==ZombieType.Bloater||form.type==ZombieType.Spitter?"Z_Walk1_InPlace":"Z_Walk_InPlace";
                float duration=capture.Get(motion).length;float runFloor=0;
                if(g==2)
                {
                    runFloor=float.PositiveInfinity;
                    for(int frame=0;frame<=90;frame++){capture.Apply(motion,duration*frame/90,tracked);runFloor=Mathf.Min(runFloor,SoleFloor());}
                }
                locomotion[g]=Bake(form.type.ToString(),form.type+(g==0?"_Guard":g==1?"_Walk":"_Run"),duration,true,t=>
                {
                    float phase=t/duration;float wave=Mathf.Sin(phase*Mathf.PI*2);
                    capture.Apply(motion,t,tracked);
                    if(g==2)pelvis.position-=Vector3.up*runFloor;else PlantFeet();
                    if(form.type==ZombieType.Spitter)spine.rotation=Quaternion.AngleAxis(5,Vector3.right)*spine.rotation;
                    Arms(0,0,false,g==0?wave*.15f:wave);
                });
            }
            string path=Dir+"/"+form.type+".controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            else
            {
                // Preserve the controller's GUID: deleting/recreating it breaks
                // prefab references and any running enemies in another editor.
                var sm=controller.layers[0].stateMachine;
                foreach(var transition in sm.anyStateTransitions)sm.RemoveAnyStateTransition(transition);
                foreach(var state in sm.states)sm.RemoveState(state.state);
                foreach(var tree in AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>())UnityEngine.Object.DestroyImmediate(tree,true);
                controller.parameters=Array.Empty<AnimatorControllerParameter>();
            }
            foreach(string p in new[]{"Speed","MoveMul","AttackMul","AttackStyle"})controller.AddParameter(p,AnimatorControllerParameterType.Float);
            foreach(string p in new[]{"attack","die"})controller.AddParameter(p,AnimatorControllerParameterType.Trigger);
            foreach(string p in new[]{"dieForward","isMoving"})controller.AddParameter(p,AnimatorControllerParameterType.Bool);
            var parameters=controller.parameters;foreach(var p in parameters)if(p.name=="MoveMul"||p.name=="AttackMul")p.defaultFloat=1;controller.parameters=parameters;
            var machine=controller.layers[0].stateMachine;var loco=machine.AddState("Locomotion");machine.defaultState=loco;
            var blend=new BlendTree{name="Undead locomotion",blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(blend,controller);
            blend.AddChild(locomotion[0],0);blend.AddChild(locomotion[1],form.walkSpeed);blend.AddChild(locomotion[2],form.runSpeed);loco.motion=blend;loco.speedParameter="MoveMul";loco.speedParameterActive=true;
            var attack=machine.AddState("Attack");var strikes=new BlendTree{name="Cut or thrust",blendParameter="AttackStyle",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(strikes,controller);
            strikes.AddChild(form.attacks[0],0);strikes.AddChild(form.attacks[1],1);attack.motion=strikes;
            var start=loco.AddTransition(attack);start.AddCondition(AnimatorConditionMode.If,0,"attack");start.hasExitTime=false;start.duration=.035f;
            var finish=attack.AddTransition(loco);finish.hasExitTime=true;finish.exitTime=.99f;finish.duration=.035f;
            for(int i=0;i<2;i++)
            {
                var dead=machine.AddState(i==1?"Die Forward":"Die Back");dead.motion=deathClips[i];
                var transition=machine.AddAnyStateTransition(dead);transition.AddCondition(AnimatorConditionMode.If,0,"die");transition.AddCondition(i==1?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"dieForward");transition.duration=.12f;transition.canTransitionToSelf=false;
            }
            form.controller=controller;EditorUtility.SetDirty(controller);
        }
        File.WriteAllText("Tools/ArtPipeline/Revenants/actions.json",JsonUtility.ToJson(new ActionExport{actions=actions.ToArray()}));
        UnityEngine.Object.DestroyImmediate(go);EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();Debug.Log("[Revenants] Baked 16 attacks, 24 locomotion clips and two deaths on the deformation rig");
    }
}
