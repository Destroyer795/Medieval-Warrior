#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Unity.Profiling;

public partial class SmokeTestDriver
{
    [Serializable] sealed class StartupCapture
    {
        public string label,device;
        public double clickMilliseconds,maximumFrameMilliseconds,p95FrameMilliseconds;
        public long allocatedBytes;
        public int frames,enemies;
        public BattleLoadMetrics.Sample[] stages;
        public SlowStartupFrame[] slowFrames;
        public SteadyBattleCapture steady;
    }
    [Serializable] sealed class SteadyBattleCapture
    {
        public int enemies,width,height,batches,triangles;
        public string quality;
        public double meanFrameMilliseconds,p95FrameMilliseconds,maximumFrameMilliseconds,mainThreadP95Milliseconds;
        public double armorP95Milliseconds,clothP95Milliseconds,quiverP95Milliseconds;
        public long meanGcBytesPerFrame;
        public int armorContactQueries,armorFallbackQueries;
        public RendererGroup[] largestVisibleMeshGroups;
    }
    [Serializable] sealed class RendererGroup {public string mesh;public int objects;public long triangles;public bool instancing;}
    [Serializable] sealed class SlowStartupFrame {public double milliseconds;public string phase,nextPhase;public int frame;}
    IEnumerator ReviewBattleStartup()
    {
        // Wall-clock frames: captureDeltaTime would conceal real stalls.
        Time.captureDeltaTime=0;
        yield return Wait(2);
        var frames=new List<double>();
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var controller=FindFirstObjectByType<MainController>();
        int world=SceneManager.GetSceneByName("Background").handle;
        Check(controller!=null,"startup fixture begins on the real main menu");
        GameSettings.Mode=GameMode.DefendTheCastle;
        double before=watch.Elapsed.TotalMilliseconds;
        if(controller!=null)controller.StartGame();
        double click=watch.Elapsed.TotalMilliseconds-before;
        frames.Add(click);
        double previous=before;
        var slow=new List<SlowStartupFrame>();string phase=BattleLoading.Phase;
        while(watch.Elapsed.TotalSeconds<15)
        {
            yield return null;
            double now=watch.Elapsed.TotalMilliseconds;frames.Add(now-previous);
            if(now-previous>50)slow.Add(new SlowStartupFrame{milliseconds=now-previous,phase=phase,nextPhase=BattleLoading.Phase,frame=Time.frameCount});
            previous=now;phase=BattleLoading.Phase;
            var player=GameObject.FindWithTag("Player");
            if(player!=null&&player.TryGetComponent<PlayerHealth>(out var health))health.InvulnerableUntil=float.PositiveInfinity;
        }
        Check(GameManager.instance!=null&&GameManager.instance.State==GameManager.MatchState.Playing,"startup reaches a playable battle");
        Check(EnemyController.All.Count>=3,"startup first wave spawns normally");
        Check(BattleLoading.Ready&&SceneManager.GetSceneByName("Background").handle==world,"startup reuses its resident world and combat bank");
        Check(!BattleLoadMetrics.Samples.Any(s=>s.stage=="Revenant roster synchronous load"||s.stage=="Combat audio synchronous warmup"),"gameplay performs no cold roster/audio load");
        Check(slow.All(f=>f.phase!="Playing"||f.milliseconds<250),"early gameplay has no quarter-second loading stall");
        frames.Sort();
        var capture=new StartupCapture{label=File.ReadAllText("Artifacts/StartupReviewOnly.txt").Trim(),
            device=SystemInfo.graphicsDeviceName,clickMilliseconds=click,maximumFrameMilliseconds=frames.Max(),
            p95FrameMilliseconds=frames[Mathf.FloorToInt((frames.Count-1)*.95f)],frames=frames.Count,
            enemies=EnemyController.All.Count,allocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),stages=BattleLoadMetrics.Samples.ToArray(),slowFrames=slow.ToArray()};
        if(GameManager.instance!=null&&GameManager.instance.State==GameManager.MatchState.Playing)
            yield return CaptureSteadyBattle(value=>capture.steady=value);
        Directory.CreateDirectory(shotDir);File.WriteAllText(Path.Combine(shotDir,"startup.json"),JsonUtility.ToJson(capture,true));
        Debug.Log("[STARTUP] "+JsonUtility.ToJson(capture));
        BattleLoadMetrics.Enabled=false;
    }
    IEnumerator CaptureSteadyBattle(Action<SteadyBattleCapture> complete)
    {
        var player=GameObject.FindWithTag("Player").GetComponent<PlayerControlSpeed>();
        foreach(ZombieType type in Enum.GetValues(typeof(ZombieType)))ZombieSpawner.Instance.Spawn(type,false);
        yield return Wait(1);
        var camera=Camera.main;var previousTarget=camera.targetTexture;
        var target=new RenderTexture(1920,1080,24,RenderTextureFormat.DefaultHDR){antiAliasing=Mathf.Max(1,QualitySettings.antiAliasing)};
        target.Create();camera.targetTexture=target;
        using var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);
        using var batches=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1);
        using var triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1);
        using var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
        var wall=new List<double>();var cpu=new List<double>();var armor=new List<double>();var cloth=new List<double>();var quiver=new List<double>();long allocated=0;
        var visual=player.GetComponent<MarchwardenVisual>();
        var yaw=typeof(CameraFollow).GetField("currentYaw",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var orbit=camera.GetComponent<CameraFollow>();
        var clock=System.Diagnostics.Stopwatch.StartNew();double last=0;int frames=0;
        try
        {
            while(clock.Elapsed.TotalSeconds<8)
            {
                float t=(float)clock.Elapsed.TotalSeconds;
                player.DebugMove(new Vector2(Mathf.Sin(t),Mathf.Cos(t))*.6f);
                yaw.SetValue(orbit,t*45);
                yield return null;
                double now=clock.Elapsed.TotalMilliseconds;
                if(t>.5f)
                {
                    wall.Add(now-last);if(main.Valid)cpu.Add(main.LastValue/1000000d);
                    armor.Add(visual.WaistArmor.LastStepMilliseconds);cloth.Add(visual.Linen.LastStepMilliseconds);quiver.Add(player.Gear.QuiverMotion.LastStepMilliseconds);
                    if(gc.Valid)allocated+=gc.LastValue;frames++;
                }
                last=now;
            }
            double P95(List<double> values){if(values.Count==0)return 0;values.Sort();return values[(int)((values.Count-1)*.95f)];}
            var groups=FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r.isVisible&&r.GetComponent<MeshFilter>()?.sharedMesh!=null)
                .GroupBy(r=>r.GetComponent<MeshFilter>().sharedMesh).Select(group=>new RendererGroup{mesh=group.Key.name,objects=group.Count(),
                    triangles=(long)Enumerable.Range(0,group.Key.subMeshCount).Sum(s=>(long)group.Key.GetIndexCount(s)/3)*group.Count(),
                    instancing=group.All(r=>r.sharedMaterial!=null&&r.sharedMaterial.enableInstancing)}).OrderByDescending(g=>g.triangles).Take(8).ToArray();
            complete(new SteadyBattleCapture{enemies=EnemyController.All.Count,width=target.width,height=target.height,quality=QualitySettings.names[QualitySettings.GetQualityLevel()],
                batches=batches.Valid?(int)batches.LastValue:0,triangles=triangles.Valid?(int)triangles.LastValue:0,
                meanFrameMilliseconds=wall.Average(),p95FrameMilliseconds=P95(wall),maximumFrameMilliseconds=wall.Max(),mainThreadP95Milliseconds=P95(cpu),
                armorP95Milliseconds=P95(armor),clothP95Milliseconds=P95(cloth),quiverP95Milliseconds=P95(quiver),meanGcBytesPerFrame=allocated/Math.Max(1,frames),largestVisibleMeshGroups=groups,
                armorContactQueries=visual.WaistArmor.ContactQueries,armorFallbackQueries=visual.WaistArmor.FallbackQueries});
        }
        finally{player.DebugMove(null);camera.targetTexture=previousTarget;target.Release();Destroy(target);}
    }
}
#endif
