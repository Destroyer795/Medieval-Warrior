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
    static Vector3 Hermite(Vector3 a,Vector3 b,Vector3 va,Vector3 vb,float t)
    {
        float t2=t*t,t3=t2*t;
        return (2*t3-3*t2+1)*a+(t3-2*t2+t)*va+(-2*t3+3*t2)*b+(t3-t2)*vb;
    }
    static Vector3 Flight(Vector3 rest,Vector3 wind,Vector3 contact,Vector3 follow,float time,float impact,float lead)
    {
        float release=impact-lead,carry=impact+.14f,end=impact+.55f;
        if(time<release)return Vector3.Lerp(rest,wind,Ease(time/release));
        if(time<carry)
        {
            // Meet the contact key at the same clip time even when the release
            // and follow-through take different amounts of time.
            var velocity=(follow-wind)/(carry-release);
            return time<impact
                ? Hermite(wind,contact,Vector3.zero,velocity*lead,(time-release)/lead)
                : Hermite(contact,follow,velocity*(carry-impact),Vector3.zero,(time-impact)/(carry-impact));
        }
        return Vector3.Lerp(follow,rest,Ease((time-carry)/(end-carry)));
    }
    static float Lead(int style,float impact)=>Mathf.Min(impact*.58f,style switch
    {
        1=>.105f, 2=>.185f, 3=>.165f, 4=>.145f, _=>.14f
    });
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
        RevenantAnimationOptimization.Begin();
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
        var thighs=new[]{Bone("LLegThigh"),Bone("RThigh")};
        var calves=new[]{Bone("LLegCalf"),Bone("RCalf")};
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
            // The anatomical grip and wrist limiter keep the palm aligned with
            // the handle. The left shield uses its outward-facing grip frame.
            Vector3 palm=Vector3.ProjectOnPlane(h.position-f.position,blade.normalized).normalized;
            float chirality=Mathf.Sign(Vector3.Dot(Vector3.Cross(grip.along,grip.distal),grip.normal));
            Vector3 normal=shield?Vector3.forward:Vector3.Cross(blade.normalized,palm).normalized*chirality;
            PoseMath.SetWrist(f,h,PoseMath.AlongBlade(blade,normal)*Quaternion.Inverse(grip.rotation),grip.distal,wrist);
        }
        AnimationClip Bake(string form,string label,float duration,bool loop,Action<float> pose,int bladeStyle=-1)
        {
            var clip=new AnimationClip();int frames=Mathf.CeilToInt(duration*60);
            var curves=tracked.Select(t=>Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray()).ToArray();
            var previous=new Quaternion[tracked.Length];var export=new List<ActionFrame>();
            var forearms=new[]{lf,rf};var previousRoll=new Quaternion[2];var previousHands=new Quaternion[2];
            var anatomy=form!="Shared"?data.forms.First(f=>f.type.ToString()==form):null;
            bool strike=bladeStyle>=0;
            bool gait=label.EndsWith("_Walk")||label.EndsWith("_Run");var footSamples=new List<Vector3[]>();
            var bladeStart=new List<Vector3>();var bladeEnd=new List<Vector3>();
            for(int i=0;i<=frames;i++)
            {
                float t=duration*i/frames;Reset();pose(loop&&i==frames?0:t);
                if(!loop&&label.StartsWith(form+"_")&&form!="Shared")
                {
                    for(int side=0;side<2;side++)
                    {
                        var fore=forearms[side];var hand=side==0?lh:rh;
                        var grip=side==0?anatomy.left:anatomy.right;var wrist=side==0?leftRest:rightRest;
                        if(i>0)
                        {
                            // Filter the desired world orientation before the
                            // anatomical solve. A parallel blade/arm cannot turn
                            // the wrist through half a revolution in one frame.
                            var desired=Quaternion.RotateTowards(previousHands[side],hand.rotation,1800*duration/frames);
                            PoseMath.SetWrist(fore,hand,desired,grip.distal,wrist);
                            var wanted=hand.rotation;var axis=(hand.position-fore.position).normalized;
                            var localAxis=fore.InverseTransformDirection(axis);
                            var aligned=Quaternion.FromToRotation(previousRoll[side]*localAxis,axis)*previousRoll[side];
                            fore.rotation=Quaternion.RotateTowards(aligned,fore.rotation,900*duration/frames);hand.rotation=wanted;
                        }
                        previousRoll[side]=fore.rotation;
                        previousHands[side]=hand.rotation;
                    }
                }
                for(int j=0;j<tracked.Length;j++)
                {
                    var q=tracked[j].localRotation;
                    if(i>0&&Quaternion.Dot(q,previous[j])<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                    previous[j]=q;
                    for(int axis=0;axis<4;axis++)curves[j][axis].AddKey(t,q[axis]);
                    var p=tracked[j].localPosition;for(int axis=0;axis<3;axis++)curves[j][axis+4].AddKey(t,p[axis]);
                }
                if(i%2==0||i==frames)export.Add(new ActionFrame{time=t,frames=tracked.Select(b=>b.localToWorldMatrix).ToArray()});
                if(strike){bladeStart.Add(rh.TransformPoint(anatomy.bladeBase));bladeEnd.Add(rh.TransformPoint(anatomy.bladeTip));}
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
                anatomy.blades[bladeStyle]=new RevenantAsset.BladeTrack{duration=duration,start=bladeStart.ToArray(),end=bladeEnd.ToArray()};
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
            actions.Add(new ActionData{name=label,form=form,duration=duration,samples=export.ToArray()});
            clip.name=label;RevenantAnimationOptimization.ReduceVerified(clip,go,tracked);return Save(clip,label);
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
            void Claw(Transform u,Transform f,Transform h,Vector3 goal,float side,float roll,RevenantAsset.Grip grip,Quaternion wrist,Vector3? elbowHint=null)
            {
                PoseMath.ArmStable(u,f,h,goal,elbowHint??V(side*.39f,1.04f,-.095f));
                var direction=((h.position-f.position).normalized+V(0,-.055f,.10f)).normalized;
                // Follow the forearm from a thumb-up grip and roll into the
                // stroke. In guard both palms rest toward the body.
                var neutral=Quaternion.FromToRotation(Vector3.forward,direction)*V(side,.16f,0);
                var dorsal=Quaternion.AngleAxis(side*roll,direction)*neutral;
                var localDorsal=grip.dorsal.sqrMagnitude>.5f?grip.dorsal:grip.normal*side;
                var desired=Quaternion.LookRotation(dorsal,direction)*Quaternion.Inverse(Quaternion.LookRotation(localDorsal,grip.distal));
                PoseMath.SetWrist(f,h,desired,grip.distal,wrist);
            }
            void Arms(float t,int style,bool attack,float swing=0)
            {
                float lead=Lead(style,impact);
                Vector3 Arc(Vector3 rest,Vector3 wind,Vector3 contact,Vector3 follow)=>Flight(rest,wind,contact,follow,t,impact,lead);
                float spread=form.type switch {ZombieType.Brute=>.37f,ZombieType.Bloater=>.34f,_=>.285f};
                var right=V(spread,1.095f,.14f)+V(0,swing*.018f,swing*.10f);
                var left=V(-spread,1.065f,.08f)-V(0,swing*.018f,swing*.10f);
                var rolls=Vector3.zero;
                if(armed)
                {
                    var rest=V(.32f,1.21f,.20f)+V(0,.012f*swing,.018f*swing);
                    var bladeRest=V(.05f,.97f,.24f);
                    var goal=rest;var blade=bladeRest;var hint=V(.48f,1.19f,-.04f);
                    if(attack)
                    {
                        (Vector3 wind,Vector3 contact,Vector3 follow,Vector3 edgeWind,Vector3 edgeContact,Vector3 edgeFollow) stroke=style switch
                        {
                            0 => (V(.34f,1.57f,.02f), V(.07f,1.20f,.43f), V(-.19f,1.06f,.32f),
                                  V(.05f,.70f,-.70f), V(-.33f,-.12f,.94f), V(-.68f,-.64f,.40f)),
                            1 => (V(.41f,1.11f,-.04f), V(.14f,1.19f,.48f), V(.12f,1.17f,.50f),
                                  V(.14f,.16f,1), V(-.09f,-.08f,1), V(-.12f,-.11f,1)),
                            2 => (V(.11f,1.03f,.20f), V(.04f,1.17f,.46f), V(.45f,1.42f,.30f),
                                  V(-.72f,-.66f,.30f), V(-.08f,.08f,1), V(.73f,.57f,.40f)),
                            _ => (V(.22f,1.76f,-.04f), V(.13f,1.31f,.46f), V(.18f,.98f,.35f),
                                  V(.03f,.81f,-.58f), V(-.05f,-.50f,.86f), V(-.03f,-.92f,.39f))
                        };
                        if(form.type==ZombieType.Warlord)
                        {
                            // Aim down into the warrior's reach from the taller rig.
                            stroke.contact.y-=.095f;
                            if(style==1)
                            {
                                stroke.edgeWind=V(-.22f,-.52f,.86f);
                                stroke.edgeContact=V(-.22f,-.82f,.87f);
                                stroke.edgeFollow=V(-.18f,-.84f,.89f);
                            }
                            else if(style==2)
                            {
                                stroke.contact=V(.04f,1.13f,.49f);
                                stroke.edgeContact=V(-.19f,-.95f,.74f);
                                stroke.edgeFollow=V(.69f,-.09f,.55f);
                            }
                            else
                            {
                                stroke.edgeContact.y-=style==3?.20f:.52f;
                                stroke.edgeFollow.y-=.18f;
                            }
                        }
                        goal=Arc(rest,stroke.wind,stroke.contact,stroke.follow);
                        blade=Arc(bladeRest,stroke.edgeWind,stroke.edgeContact,stroke.edgeFollow);
                        hint=Arc(hint,V(.52f,1.42f,.01f),V(.34f,1.07f,.01f),V(.39f,1.08f,0));
                    }
                    ArmedArm(ru,rf,rh,goal,hint,blade,form.right,rightRest);
                    if(form.type==ZombieType.Bulwark)
                    {
                        var guard=V(-.30f,1.25f,.27f);
                        if(attack)guard=Arc(guard,V(-.30f,1.26f,.29f),V(-.40f,1.07f,.16f),V(-.40f,1.07f,.15f));
                        ArmedArm(lu,lf,lh,guard,V(-.52f,1.22f,.02f),Vector3.up,form.left,leftRest,true);
                    }
                    else
                    {
                        if(attack)left=Arc(left,V(-.30f,1.22f,.23f),V(-.37f,1.12f,.19f),V(-.43f,1.10f,.08f));
                        Claw(lu,lf,lh,left,-1,0,form.left,leftRest);
                    }
                    return;
                }
                if(attack)
                {
                    (Vector3 windR,Vector3 hitR,Vector3 carryR,Vector3 windL,Vector3 hitL,Vector3 carryL,Vector3 wristRoll) stroke;
                    if(form.type==ZombieType.Spitter)
                        stroke=style switch
                        {
                            // Throat clutch, low retch, and a side-on spit.
                            0 => (V(.12f,1.48f,.22f), V(.29f,1.32f,.33f), V(.34f,1.14f,.20f),
                                  V(-.19f,1.13f,.23f), V(-.32f,1.10f,.16f), V(-.30f,1.07f,.12f),
                                  V(15,12,0)),
                            1 => (V(.25f,1.03f,.20f), V(.43f,1.01f,.19f), V(.38f,1.07f,.15f),
                                  V(-.22f,1.08f,.17f), V(-.39f,1.07f,.25f), V(-.34f,1.03f,.20f),
                                  V(28,25,0)),
                            _ => (V(.31f,1.15f,.08f), V(.23f,1.14f,.30f), V(.30f,1.11f,.14f),
                                  V(-.14f,1.43f,.22f), V(-.38f,1.30f,.32f), V(-.44f,1.18f,.20f),
                                  V(0,40,0))
                        };
                    else if(form.type==ZombieType.Bloater)
                        stroke=style switch
                        {
                            // Clutch the belly, shudder, double over, or throw the arms wide.
                            0 => (V(.23f,1.12f,.27f), V(.18f,1.21f,.34f), V(.35f,1.08f,.25f),
                                  V(-.20f,1.11f,.25f), V(-.18f,1.11f,.32f), V(-.34f,1.07f,.22f),
                                  V(5,5,0)),
                            1 => (V(.38f,1.29f,.14f), V(.47f,1.41f,.28f), V(.46f,1.17f,.20f),
                                  V(-.24f,1.08f,.27f), V(-.27f,1.22f,.31f), V(-.34f,1.10f,.20f),
                                  V(28,12,0)),
                            2 => (V(.29f,1.09f,.34f), V(.31f,.95f,.43f), V(.38f,1.08f,.21f),
                                  V(-.29f,1.10f,.33f), V(-.31f,.98f,.43f), V(-.34f,1.07f,.23f),
                                  V(36,36,0)),
                            _ => (V(.35f,1.04f,.17f), V(.49f,1.48f,.15f), V(.47f,1.17f,.16f),
                                  V(-.31f,1.07f,.15f), V(-.49f,1.44f,.14f), V(-.46f,1.18f,.16f),
                                  V(20,20,0))
                        };
                    else
                    {
                        bool hammer=form.type==ZombieType.Brute;
                        stroke=style switch
                        {
                            // Diagonal claw, opposite backhand, grab/slam, rising rake, and left hook.
                            0 => (V(.43f,1.57f,.04f), V(.05f,1.27f,.48f), V(-.18f,1.12f,.32f),
                                  V(-.32f,1.13f,.09f), V(-.35f,1.25f,.27f), V(-.26f,1.36f,.29f),
                                  V(54,15,0)),
                            1 => (V(.29f,1.19f,.13f), V(.30f,1.31f,.24f), V(.19f,1.39f,.25f),
                                  V(.04f,1.43f,.22f), V(-.12f,1.27f,.50f), V(-.43f,1.14f,.25f),
                                  V(10,64,0)),
                            2 => (V(.34f,hammer?1.75f:1.46f,.10f), V(.17f,hammer?1.09f:1.31f,.48f), V(.11f,hammer?.98f:1.16f,.37f),
                                  V(-.34f,hammer?1.75f:1.46f,.10f), V(-.17f,hammer?1.09f:1.31f,.48f), V(-.11f,hammer?.98f:1.16f,.37f),
                                  V(hammer?51:23,hammer?51:23,0)),
                            3 => (V(.40f,1.01f,.05f), V(.10f,1.39f,.48f), V(-.05f,1.55f,.31f),
                                  V(-.30f,1.38f,.15f), V(-.31f,1.26f,.24f), V(-.32f,1.13f,.18f),
                                  V(-32,17,0)),
                            _ => (V(.24f,1.33f,.20f), V(.26f,1.15f,.23f), V(.35f,1.09f,.18f),
                                  V(-.45f,1.28f,-.09f), V(-.05f,1.15f,.49f), V(.15f,1.08f,.36f),
                                  V(9,48,0))
                        };
                    }
                    right=Arc(right,stroke.windR,stroke.hitR,stroke.carryR);
                    left=Arc(left,stroke.windL,stroke.hitL,stroke.carryL);
                    rolls=Arc(Vector3.zero,stroke.wristRoll*.3f,stroke.wristRoll,stroke.wristRoll*.65f);
                }
                Claw(ru,rf,rh,right,1,rolls.x,form.right,rightRest);
                Claw(lu,lf,lh,left,-1,rolls.y,form.left,leftRest);
            }
            void Posture()
            {
                float lean=form.type switch {ZombieType.Runner=>11,ZombieType.Spitter=>9,ZombieType.Brute=>6,_=>4};
                spine.rotation=Quaternion.Euler(lean,0,-2)*spine.rotation;
                head.rotation=Quaternion.AngleAxis(-lean*.5f,Vector3.right)*head.rotation;
            }
            void Pursuit(bool running)
            {
                // One arm leads the opposite planted leg. The ankle separation
                // also carries the source motion's actual contact and toe-off.
                float step=Mathf.Clamp((feet[0].position.z-feet[1].position.z)/(running?.73f:.23f),-1,1);
                float effort=running?1f:.47f;
                bool heavy=form.type==ZombieType.Brute||form.type==ZombieType.Bloater;
                float pitch=form.type switch
                {
                    ZombieType.Runner=>14, ZombieType.Spitter=>17, ZombieType.Bloater=>9,
                    ZombieType.Brute=>11, ZombieType.Warlord=>6, _=>9
                };
                float lean=pitch*effort;
                float turn=-step*(heavy?9f:6f)*effort;
                float list=(form.type==ZombieType.Spitter?6f:-2.5f)+step*(heavy?4f:2f)*effort;
                spine.rotation=Quaternion.Euler(lean,turn,list)*spine.rotation;
                head.rotation=Quaternion.Euler(-lean*.75f,-turn*.8f,-list*.7f)*head.rotation;

                void Arm(float side,Transform upper,Transform fore,Transform hand,RevenantAsset.Grip grip,Quaternion wrist)
                {
                    float drive=side*step*effort;
                    Vector3 shoulder=upper.position;
                    var elbow=shoulder+V(side*(heavy?.20f:.135f),-.26f,-.24f-.05f*drive);
                    if(armed&&side>0)
                    {
                        var swordHand=shoulder+V(.095f,-.235f+.043f*drive,.165f+.10f*drive);
                        var blade=V(.21f+.07f*drive,.81f-.12f*drive,.51f+.14f*drive);
                        ArmedArm(upper,fore,hand,swordHand,elbow,blade,grip,wrist);
                        return;
                    }
                    if(form.type==ZombieType.Bulwark&&side<0)
                    {
                        var shieldHand=shoulder+V(-.093f,-.19f+.018f*drive,.205f+.045f*drive);
                        ArmedArm(upper,fore,hand,shieldHand,elbow,Vector3.up,grip,wrist,true);
                        return;
                    }
                    bool runner=form.type==ZombieType.Runner;
                    float reach=runner?.24f:heavy?.145f:.195f;
                    float drop=runner?.22f:heavy?.32f:.28f;
                    float lead=side>0?.048f:-.025f;
                    if(form.type==ZombieType.Walker&&side>0){drop-=.055f;lead+=.095f*effort;}
                    if(form.type==ZombieType.Spitter&&side<0){drop=.13f;reach=.09f;lead=.085f;}
                    var goal=shoulder+V(side*(heavy?.135f:.066f),-drop+drive*.10f+lead*.38f,
                        .12f+lead+reach*drive);
                    Claw(upper,fore,hand,goal,side,13f+16f*drive,grip,wrist,elbow);
                }
                Arm(1,ru,rf,rh,form.right,rightRest);
                Arm(-1,lu,lf,lh,form.left,leftRest);
            }
            void Attack(float t,int style)
            {
                float sourceTime=t<impact?.60f*t/impact:.60f+.40f*(t-impact)/.55f;
                capture.Apply("Z_Idle",0,tracked);
                var idleRot=tracked.Select(b=>b.localRotation).ToArray();var idlePos=tracked.Select(b=>b.localPosition).ToArray();
                capture.Apply("Z_Attack",sourceTime,tracked);
                float weight=Ease(t/.11f)*(1-Ease((t-impact-.18f)/.37f));
                for(int i=0;i<tracked.Length;i++)
                {
                    tracked[i].localRotation=Quaternion.Slerp(idleRot[i],tracked[i].localRotation,weight);
                    tracked[i].localPosition=Vector3.Lerp(idlePos[i],tracked[i].localPosition,weight);
                }
                Posture();
                (Vector3 wind,Vector3 contact,Vector3 follow) body=form.type switch
                {
                    ZombieType.Spitter=>style switch
                    {
                        0=>(V(-21,-7,0),V(19,0,1),V(14,2,2)),
                        1=>(V(21,3,0),V(31,0,0),V(20,1,2)),
                        _=>(V(-8,-19,10),V(13,0,-8),V(-7,12,-12))
                    },
                    ZombieType.Bloater=>style switch
                    {
                        0=>(V(6,4,3),V(15,1,1),V(8,4,1)),
                        1=>(V(4,-15,9),V(-10,13,-7),V(11,-8,8)),
                        2=>(V(14,-3,0),V(34,0,2),V(14,1,1)),
                        _=>(V(15,0,0),V(-15,0,0),V(8,0,4))
                    },
                    _=>style switch
                    {
                        0=>(V(-5,23,-5),V(7,-9,3),V(14,-25,9)),
                        1=>(V(7,armed?18:-24,6),V(11,armed?3:10,-1),V(12,armed?2:22,-7)),
                        2=>(V(armed?12:-13,armed?-27:1,3),V(armed?9:17,0,-2),V(armed?-5:24,armed?26:0,-6)),
                        3=>(V(armed?-12:17,9,6),V(armed?11:0,-4,2),V(armed?20:-11,-13,-5)),
                        _=>(V(6,-29,8),V(14,8,-5),V(22,23,-11))
                    }
                };
                var tilt=Flight(Vector3.zero,body.wind,body.contact,body.follow,t,impact,Lead(style,impact));
                pelvis.rotation=Quaternion.Euler(0,tilt.y*.2f,tilt.z*.32f)*pelvis.rotation;
                spine.rotation=Quaternion.Euler(tilt)*spine.rotation;
                head.rotation=Quaternion.Euler(-tilt.x*.4f,-tilt.y*.55f,-tilt.z*.4f)*head.rotation;
                Arms(t,style,true);
                PlantFeet();
            }
            string[] suffixes=armed?new[]{"_Cut","_Thrust","_RisingCut","_Overhead"}
                :form.type==ZombieType.Spitter?new[]{"_Cut","_Thrust","_SideSpit"}
                :form.type==ZombieType.Bloater?new[]{"_Cut","_Thrust","_Grab","_RisingRake"}
                :new[]{"_Cut","_Thrust","_Grab","_RisingRake","_LeftHook"};
            form.attacks=new AnimationClip[suffixes.Length];
            form.blades=armed?new RevenantAsset.BladeTrack[suffixes.Length]:Array.Empty<RevenantAsset.BladeTrack>();
            for(int style=0;style<suffixes.Length;style++)
            {
                int s=style;
                form.attacks[style]=Bake(form.type.ToString(),form.type+suffixes[style],impact+.55f,false,t=>Attack(t,s),armed?s:-1);
            }
            var locomotion=new AnimationClip[3];
            for(int gait=0;gait<3;gait++)
            {
                int g=gait;string motion=g==0?"Z_Idle":g==2?"Z_Run_InPlace":form.type==ZombieType.Brute||form.type==ZombieType.Bloater||form.type==ZombieType.Spitter?"Z_Walk1_InPlace":"Z_Walk_InPlace";
                float duration=capture.Get(motion).length;float runFloor=0;
                if(g==2)duration*=.5f;
                void SampleGait(float t)
                {
                    if(g!=2){capture.Apply(motion,t,tracked);return;}
                    if(form.type==ZombieType.Runner)
                    {
                        // A fast flanking sprint spends longer in flight and
                        // sweeps the planted foot back quickly on either side.
                        const float cycle=4f*Mathf.PI,stance=.20f;
                        float phase=t/duration;
                        float offset=Mathf.Sin(cycle*(phase-stance))-Mathf.Sin(-cycle*stance);
                        t=duration*(phase+.57f*offset/cycle);
                    }
                    // The run take contains two strides, the walk one. Join the
                    // two matching run phases at the middle of the source take.
                    capture.Apply(motion,duration+t,tracked);
                    float blend=Ease(Mathf.InverseLerp(duration*.48f,duration*.86f,t));
                    if(blend>0)capture.Apply(motion,t,tracked,blend);
                }
                if(g==2)
                {
                    runFloor=float.PositiveInfinity;
                    for(int frame=0;frame<=90;frame++){SampleGait(duration*frame/90);runFloor=Mathf.Min(runFloor,SoleFloor());}
                }
                locomotion[g]=Bake(form.type.ToString(),form.type+(g==0?"_Guard":g==1?"_Walk":"_Run"),duration,true,t=>
                {
                    float phase=t/duration;float wave=Mathf.Sin(phase*Mathf.PI*2);
                    SampleGait(t);
                    if(g==2)pelvis.position-=Vector3.up*runFloor;else PlantFeet();
                    if(g==2&&(form.type==ZombieType.Brute||form.type==ZombieType.Bloater))
                    {
                        // Carry the bulk low over the supporting leg while
                        // keeping the planted foot and ankle orientation.
                        pelvis.position-=Vector3.up*Mathf.Max(0,SoleFloor())*.78f;
                        var load=V(Mathf.Clamp((feet[0].position.y-feet[1].position.y)*.15f,-.035f,.035f),-.065f,0);
                        pelvis.position+=load;
                        for(int side=0;side<2;side++)
                        {
                            var foot=feet[side];var ankle=foot.rotation;
                            var goal=foot.position-load+Vector3.right*(side==0?-.052f:.052f);
                            var knee=calves[side].position+Vector3.forward*.19f;
                            PoseMath.ArmStable(thighs[side],calves[side],foot,goal,knee);
                            foot.rotation=ankle;
                        }
                    }
                    if(g==0){Posture();Arms(0,0,false,wave*.15f);}
                    else Pursuit(g==2);
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
            var attack=machine.AddState("Attack");attack.speedParameter="AttackMul";attack.speedParameterActive=true;
            var strikes=new BlendTree{name="Revenant attacks",blendParameter="AttackStyle",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(strikes,controller);
            for(int style=0;style<form.attacks.Length;style++)strikes.AddChild(form.attacks[style],style);attack.motion=strikes;
            var start=loco.AddTransition(attack);start.AddCondition(AnimatorConditionMode.If,0,"attack");start.hasExitTime=false;start.hasFixedDuration=true;start.duration=.055f;
            var finish=attack.AddTransition(loco);finish.hasExitTime=true;finish.exitTime=.95f;finish.hasFixedDuration=true;finish.duration=.09f;
            for(int i=0;i<2;i++)
            {
                var dead=machine.AddState(i==1?"Die Forward":"Die Back");dead.motion=deathClips[i];
                var transition=machine.AddAnyStateTransition(dead);transition.AddCondition(AnimatorConditionMode.If,0,"die");transition.AddCondition(i==1?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"dieForward");transition.duration=.12f;transition.canTransitionToSelf=false;
            }
            form.controller=controller;EditorUtility.SetDirty(controller);
        }
        File.WriteAllText("Tools/ArtPipeline/Revenants/actions.json",JsonUtility.ToJson(new ActionExport{actions=actions.ToArray()}));
        RevenantAnimationOptimization.Finish();
        UnityEngine.Object.DestroyImmediate(go);EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();Debug.Log("[Revenants] Baked "+data.forms.Sum(f=>f.attacks.Length)+" attacks, 24 seamless locomotion clips and two deaths on the deformation rig");
    }
}
