using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Keep the dense, editable Blender actions; ship error-bounded runtime curves.
public static class RevenantAnimationOptimization
{
    [Serializable] public sealed class ClipAudit
    {
        public string clip;
        public int originalKeys,reducedKeys;
        public float maximumPositionError,maximumAngleError;
    }
    [Serializable] public sealed class Audit
    {
        public long serializedBytes;
        public int originalKeys,reducedKeys;
        public ClipAudit[] clips;
    }
    static readonly List<ClipAudit> current=new List<ClipAudit>();
    public static void Begin()
    {
        current.Clear();
    }
    public static void Finish()
    {
        AssetDatabase.SaveAssets();
        var result=new Audit{serializedBytes=Directory.GetFiles("Assets/Resources/Revenants/Animations","*.anim").Sum(p=>new FileInfo(p).Length),
            originalKeys=current.Sum(a=>a.originalKeys),reducedKeys=current.Sum(a=>a.reducedKeys),clips=current.ToArray()};
        ValidateBudget(result);WriteManifest();
        File.WriteAllText("ArtSource/Revenants/animation-budget.json",JsonUtility.ToJson(result,true));
        Debug.Log($"[Animation budget] PASS {result.originalKeys} -> {result.reducedKeys} keys, {result.serializedBytes} serialized bytes");
    }
    static int Keys(AnimationClip clip)=>AnimationUtility.GetCurveBindings(clip).Sum(b=>AnimationUtility.GetEditorCurve(clip,b).length);
    public static void Reduce(AnimationClip clip,float scale=1)
    {
        foreach(var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var source=AnimationUtility.GetEditorCurve(clip,binding);if(source.length<=2)continue;
            var keys=source.keys;var samples=new List<Keyframe>(keys.Length*2);
            for(int i=0;i<keys.Length;i++)
            {
                samples.Add(new Keyframe(keys[i].time,keys[i].value));
                if(i+1<keys.Length)
                {
                    float t=(keys[i].time+keys[i+1].time)*.5f;samples.Add(new Keyframe(t,source.Evaluate(t)));
                }
            }
            float tolerance=(binding.propertyName.StartsWith("m_LocalRotation")?.00030f:.00008f)*scale;
            var keep=new bool[samples.Count];keep[0]=keep[keep.Length-1]=true;
            var stack=new Stack<(int start,int end)>();stack.Push((0,samples.Count-1));
            while(stack.Count>0)
            {
                var segment=stack.Pop();var a=samples[segment.start];var b=samples[segment.end];int worst=-1;float error=tolerance;
                for(int i=segment.start+1;i<segment.end;i++)
                {
                    float t=(samples[i].time-a.time)/(b.time-a.time);
                    float distance=Mathf.Abs(samples[i].value-Mathf.Lerp(a.value,b.value,t));
                    if(distance>error){error=distance;worst=i;}
                }
                if(worst>=0){keep[worst]=true;stack.Push((segment.start,worst));stack.Push((worst,segment.end));}
            }
            var reduced=new AnimationCurve(samples.Where((_,i)=>keep[i]).ToArray());
            if(reduced.length>=source.length)continue;
            for(int i=0;i<reduced.length;i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(reduced,i,AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(reduced,i,AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip,binding,reduced);
        }
        clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);
    }
    sealed class Pose {public Vector3[] positions;public Quaternion[] rotations;}
    static Pose[] Capture(AnimationClip clip,GameObject actor,Transform[] bones)
    {
        int frames=Mathf.CeilToInt(clip.length*120);var poses=new Pose[frames+1];float motion=0;
        for(int frame=0;frame<=frames;frame++)
        {
            clip.SampleAnimation(actor,clip.length*frame/frames);
            poses[frame]=new Pose{positions=bones.Select(b=>b.position).ToArray(),rotations=bones.Select(b=>b.rotation).ToArray()};
            if(frame>0)for(int i=0;i<bones.Length;i++)motion=Mathf.Max(motion,Vector3.Distance(poses[0].positions[i],bones[i].position));
        }
        if(motion<.0001f)throw new InvalidOperationException("The generic animation comparison fixture did not move: "+clip.name);
        return poses;
    }
    static void Compare(Pose[] poses,AnimationClip reduced,GameObject actor,Transform[] bones,ClipAudit audit)
    {
        audit.maximumPositionError=0;audit.maximumAngleError=0;
        for(int frame=0;frame<poses.Length;frame++)
        {
            reduced.SampleAnimation(actor,reduced.length*frame/(poses.Length-1));
            for(int i=0;i<bones.Length;i++)
            {
                audit.maximumPositionError=Mathf.Max(audit.maximumPositionError,Vector3.Distance(poses[frame].positions[i],bones[i].position));
                audit.maximumAngleError=Mathf.Max(audit.maximumAngleError,Angle(poses[frame].rotations[i],bones[i].rotation));
            }
        }
    }
    static float Angle(Quaternion a,Quaternion b)
    {
        var q=Quaternion.Inverse(a)*b;
        return (float)(2*Math.Atan2(Math.Sqrt((double)q.x*q.x+(double)q.y*q.y+(double)q.z*q.z),Math.Abs(q.w))*Mathf.Rad2Deg);
    }
    static void ValidateBudget(Audit audit)
    {
        var roster=AssetDatabase.LoadAssetAtPath<RevenantAsset>("Assets/Resources/Revenants/Roster.asset");
        var expected=roster.forms.SelectMany(form=>form.controller.animationClips).Select(clip=>clip.name).ToHashSet();
        if(audit.clips==null||audit.clips.Length!=expected.Count||!expected.SetEquals(audit.clips.Select(entry=>entry.clip)))
            throw new InvalidOperationException("Incomplete runtime animation bank");
        foreach(var entry in audit.clips)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/Revenants/Animations/"+entry.clip+".anim");
            if(clip==null||clip.name!=entry.clip||entry.maximumPositionError>=.001f||entry.maximumAngleError>=.2f)
                throw new InvalidOperationException("Runtime animation budget invalid: "+entry.clip);
        }
    }
    static void WriteManifest()
    {
        string ResourcePath(string path)
        {
            path=path.Replace('\\','/');return path.Substring("Assets/Resources/".Length,path.Length-"Assets/Resources/".Length-Path.GetExtension(path).Length);
        }
        var manifest=new BattleLoading.Manifest{clips=Directory.GetFiles("Assets/Resources/Revenants/Animations","*.anim").OrderBy(p=>p).Select(ResourcePath).ToArray(),
            meshes=Directory.GetFiles("Assets/Resources/Revenants","*.asset").Where(p=>AssetDatabase.GetMainAssetTypeAtPath(p)==typeof(Mesh)).OrderBy(p=>p).Select(ResourcePath).ToArray()};
        File.WriteAllText("Assets/Resources/Revenants/Preload.json",JsonUtility.ToJson(manifest,true));AssetDatabase.ImportAsset("Assets/Resources/Revenants/Preload.json");
    }
    public static void VerifySavedBank()
    {
        var audit=JsonUtility.FromJson<Audit>(File.ReadAllText("ArtSource/Revenants/animation-budget.json"));
        audit.serializedBytes=Directory.GetFiles("Assets/Resources/Revenants/Animations","*.anim").Sum(p=>new FileInfo(p).Length);
        ValidateBudget(audit);WriteManifest();File.WriteAllText("ArtSource/Revenants/animation-budget.json",JsonUtility.ToJson(audit,true));
        Debug.Log("[Animation budget] PASS: dense-pose audit valid and asynchronous preload manifest refreshed");
    }
    public static void ReduceVerified(AnimationClip clip,GameObject actor,Transform[] bones)
    {
        clip.EnsureQuaternionContinuity();
        var bindings=AnimationUtility.GetCurveBindings(clip);
        var originals=bindings.Select(b=>AnimationUtility.GetEditorCurve(clip,b).keys).ToArray();
        var poses=Capture(clip,actor,bones);var audit=new ClipAudit{clip=clip.name,originalKeys=Keys(clip)};
        for(int attempt=0;attempt<7;attempt++)
        {
            for(int i=0;i<bindings.Length;i++)AnimationUtility.SetEditorCurve(clip,bindings[i],new AnimationCurve(originals[i]));
            Reduce(clip,Mathf.Pow(.5f,attempt));Compare(poses,clip,actor,bones,audit);
            if(audit.maximumPositionError<.001f&&audit.maximumAngleError<.2f)break;
            if(attempt==6)
            {
                for(int i=0;i<bindings.Length;i++)AnimationUtility.SetEditorCurve(clip,bindings[i],new AnimationCurve(originals[i]));
                throw new InvalidOperationException("Animation reduction exceeded pose error budget: "+clip.name);
            }
        }
        audit.reducedKeys=Keys(clip);current.Add(audit);
        Debug.Log($"[Animation budget] {clip.name}: {audit.originalKeys} -> {audit.reducedKeys} keys; {audit.maximumPositionError:F6} m / {audit.maximumAngleError:F4} degrees");
    }
    [MenuItem("Medieval Warrior/Revenants/Optimize runtime animation curves")]
    public static void Run()
    {
        const string dir="Assets/Resources/Revenants/Animations";
        var paths=Directory.GetFiles(dir,"*.anim");Begin();
        var actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RevenantBuilder.Source));actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);actor.transform.localScale=Vector3.one;
        actor.GetComponent<Animator>().enabled=false;actor.GetComponent<Animator>().avatar=null;foreach(var renderer in actor.GetComponentsInChildren<Renderer>())renderer.enabled=false;
        var roster=AssetDatabase.LoadAssetAtPath<RevenantAsset>("Assets/Resources/Revenants/Roster.asset");
        var all=actor.GetComponentsInChildren<Transform>();var bones=roster.bones.Select(name=>all.First(b=>b.name==name)).ToArray();
        try
        {
            foreach(string path in paths)
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);clip.name=Path.GetFileNameWithoutExtension(path);ReduceVerified(clip,actor,bones);
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);AssetDatabase.SaveAssets();}
        Finish();
        RevenantReview.Run();
    }
}
