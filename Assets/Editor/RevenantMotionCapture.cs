using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Evaluate the original authored humanoid motion through its real Avatar, then
// retain ordinary local transforms. Never mix muscle and generic bone curves.
public sealed class RevenantMotionCapture : IDisposable
{
    public sealed class Frame {public Vector3[] position;public Quaternion[] rotation;}
    public sealed class Motion {public string name;public float length;public Frame[] frames;}
    readonly GameObject source;
    readonly Animator animator;
    readonly Transform[] bones;
    readonly Dictionary<string,Motion> motions=new Dictionary<string,Motion>();
    readonly Dictionary<string,AnimationClip> clips;
    public RevenantMotionCapture(RevenantBuilder.Rig rig)
    {
        source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RevenantBuilder.Source));
        source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);source.transform.localScale=Vector3.one;
        animator=source.GetComponent<Animator>();animator.enabled=true;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.fireEvents=false;
        if(animator.avatar==null||!animator.avatar.isHuman)throw new Exception("The source animation Avatar is missing");
        var all=source.GetComponentsInChildren<Transform>();bones=rig.bones.Select(b=>all.First(t=>t.name==b.name)).ToArray();
        clips=Directory.GetFiles("Assets/Zombie/Animations","*.anim").Select(AssetDatabase.LoadAssetAtPath<AnimationClip>).ToDictionary(c=>c.name);
    }
    public Motion Get(string name)
    {
        if(motions.TryGetValue(name,out var cached))return cached;
        var clip=clips[name];var graph=PlayableGraph.Create("Capture "+name);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        try
        {
            var input=AnimationClipPlayable.Create(graph,clip);input.SetApplyFootIK(true);input.SetSpeed(0);
            var output=AnimationPlayableOutput.Create(graph,"Source motion",animator);output.SetSourcePlayable(input);graph.Play();
            int count=Mathf.CeilToInt(clip.length*60);var frames=new Frame[count+1];
            for(int i=0;i<=count;i++)
            {
                input.SetTime(Mathf.Min(clip.length-.00001f,i*clip.length/count));graph.Evaluate(0);
                source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                frames[i]=new Frame{position=bones.Select(b=>b.localPosition).ToArray(),rotation=bones.Select(b=>b.localRotation).ToArray()};
            }
            var result=new Motion{name=name,length=clip.length,frames=frames};motions[name]=result;
            int foot=Array.FindIndex(bones,b=>b.name=="Base HumanRFoot");
            Debug.Log("[Revenant capture] "+name+" "+frames.Length+" frames; pelvis "+frames[0].position[3]+"; foot rotation span "+Quaternion.Angle(frames[0].rotation[foot],frames[count/3].rotation[foot]));
            return result;
        }
        finally{graph.Destroy();}
    }
    public void Apply(string name,float time,Transform[] target,float weight=1f)
    {
        var motion=Get(name);float frame=Mathf.Clamp01(time/motion.length)*(motion.frames.Length-1);int a=Mathf.FloorToInt(frame),b=Mathf.Min(a+1,motion.frames.Length-1);float t=frame-a;
        for(int i=0;i<target.Length;i++)
        {
            var position=Vector3.Lerp(motion.frames[a].position[i],motion.frames[b].position[i],t);
            var rotation=Quaternion.Slerp(motion.frames[a].rotation[i],motion.frames[b].rotation[i],t);
            target[i].localPosition=weight>=1f?position:Vector3.Lerp(target[i].localPosition,position,weight);
            target[i].localRotation=weight>=1f?rotation:Quaternion.Slerp(target[i].localRotation,rotation,weight);
        }
    }
    public void Dispose()=>UnityEngine.Object.DestroyImmediate(source);
    public static void Probe()
    {
        var rig=JsonUtility.FromJson<RevenantBuilder.Rig>(File.ReadAllText("Tools/ArtPipeline/Revenants/rig.json"));
        using(var capture=new RevenantMotionCapture(rig))foreach(var name in new[]{"Z_Idle","Z_Walk_InPlace","Z_Walk1_InPlace","Z_Run_InPlace","Z_Attack","Z_FallingBack","Z_FallingForward"})capture.Get(name);
    }
}
