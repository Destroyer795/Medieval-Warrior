#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

public partial class SmokeTestDriver
{
    [Serializable] class FeedbackRoute
    {
        public string between = "";
        public Vector2 start = default, end = default;
        public float maxGrade = 0f, maxTerrainSlope = 0f;
    }
    [Serializable] class FeedbackSlopes { public FeedbackRoute[] lanes = Array.Empty<FeedbackRoute>(); }
    IEnumerator ReviewActionFeedback(PlayerControlSpeed player,PlayerHealth health)
    {
        var gm=GameManager.instance;bool managerEnabled=gm.enabled;var body=player.GetComponent<Rigidbody>();
        Vector3 originalPosition=body.position;Quaternion originalRotation=body.rotation;float capture=Time.captureDeltaTime;
        float effectsVolume=GameSettings.SfxVolume;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        float stamina=player.Stamina;bool exhausted=(bool)typeof(PlayerControlSpeed).GetField("exhausted",flags).GetValue(player);
        GameObject creature=null;
        try
        {
            gm.enabled=false;GameSettings.SfxVolume=.9f;AudioManager.SetBattleIntensity(0,true);
            var audio=FindFirstObjectByType<AudioManager>();
            foreach(var voice in audio.GetComponentsInChildren<AudioSource>())
                if(voice.clip!=null&&voice.clip.name.StartsWith("zombie_groan",StringComparison.Ordinal))voice.Stop();
            typeof(AudioManager).GetField("nextAmbientVoice",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,0f);
            var position=player.transform.position+player.transform.right*12;
            if(NavMesh.SamplePosition(position,out var sample,3,NavMesh.AllAreas))position=sample.position;
            creature=Instantiate(ZombieSpawner.Instance.zombiePrefab,position,Quaternion.identity);
            var enemy=creature.GetComponent<EnemyController>();enemy.Init(ZombieType.Walker,false,false);
            creature.GetComponent<NavMeshAgent>().enabled=false;
            typeof(EnemyController).GetField("groanIndex",flags).SetValue(enemy,0);
            for(int i=0;i<3;i++)
            {
                if(i>0)
                {
                    yield return Wait(.42f);
                    typeof(EnemyController).GetField("groanTimer",flags).SetValue(enemy,0f);
                }
                string name="zombie_groan"+i;float until=Time.unscaledTime+2.3f;AudioSource emitted=null;
                while(Time.unscaledTime<until&&emitted==null)
                {
                    emitted=audio.GetComponentsInChildren<AudioSource>().FirstOrDefault(v=>v.isPlaying&&v.clip!=null&&v.clip.name==name);
                    if(emitted==null)yield return null;
                }
                Check(emitted!=null&&emitted.volume>.6f&&emitted.minDistance>=9&&emitted.maxDistance>=60,
                    name+" is emitted by the live AI with an audible encounter-distance profile");
                if(emitted!=null)
                {
                    var filter=emitted.GetComponent<AudioLowPassFilter>();
                    Check(filter!=null&&filter.cutoffFrequency>=6500,name+" preserves breath/rasp instead of muffling it away");
                }
            }
            foreach(string name in new[]{"zombie_attack","zombie_die","brute_roar","spit","splat","bloater_fuse","explosion","emerge"})
            {
                bool played=name.StartsWith("zombie_",StringComparison.Ordinal)||name=="brute_roar"?
                    AudioManager.PlayZombieVoice(name,position,ZombieType.Walker):AudioManager.PlayAt(name,position);
                var source=audio.GetComponentsInChildren<AudioSource>().FirstOrDefault(v=>v.isPlaying&&v.clip!=null&&v.clip.name==name);
                Check(played&&source!=null&&source.minDistance>=8,name+" plays through the creature-effects distance profile");
            }
            yield return Wait(.15f);
            var bed=audio.GetComponents<AudioSource>().FirstOrDefault(s=>s.clip!=null&&s.clip.name=="ambient_meadow");
            Check(bed!=null&&bed.volume<.9f*GameSettings.MusicVolume*.85f,"battle ambience leaves space for nearby creature voices");
            var release=Resources.Load<AudioClip>("Audio/bow_release");var samples=new float[release.samples];release.GetData(samples,0);
            double early=0,tail=0;int boundary=Mathf.RoundToInt(release.frequency*.1f);
            for(int i=0;i<samples.Length;i++)if(i<boundary)early+=samples[i]*samples[i];else tail+=samples[i]*samples[i];
            Check(release.length<=.25f&&early>tail*5,"bow release is a short string/shaft snap with a rapidly damped tail");
            Destroy(creature);creature=null;yield return null;

            Time.captureDeltaTime=1f/120;
            typeof(PlayerControlSpeed).GetField("lastAttack",flags).SetValue(player,-99f);
            player.DebugAttackPress();yield return null;int sequence=player.SwordSequence;bool complete=true;
            float follow= SwordMoves.All[player.SwordVariant].followThrough;
            while(player.SwordAge<follow+.025f)
            {
                player.DebugAttackPress();if(player.SwordSequence!=sequence)complete=false;yield return null;
            }
            Check(complete&&player.SwordLayerWeight>.95f,"rapid clicks cannot truncate the full cutting follow-through");
            float deadline=Time.time+.25f;while(player.SwordSequence==sequence&&Time.time<deadline)yield return null;
            Check(player.SwordSequence==sequence+1,"one buffered follow-up starts after the completed stroke");
            for(int i=0;i<85;i++)yield return null;
            Time.captureDeltaTime=.02f;

            var slopes=JsonUtility.FromJson<FeedbackSlopes>(File.ReadAllText("ArtSource/Buildings/slope-audit.json"));
            string[] repaired={"House (2) / House (9)","House (9) / HouseB (10)","House (3) / House (20)"};
            var selected=slopes.lanes.OrderByDescending(l=>l.maxTerrainSlope).Take(6).Concat(slopes.lanes.Where(l=>repaired.Contains(l.between))).GroupBy(l=>l.between).Select(g=>g.First()).ToArray();
            var terrain=FindFirstObjectByType<Terrain>();
            float Ground(Vector2 p)=>terrain.SampleHeight(new Vector3(p.x,0,p.y))+terrain.transform.position.y;
            foreach(var lane in selected)for(int sprint=0;sprint<(repaired.Contains(lane.between)?2:1);sprint++)for(int reverse=0;reverse<2;reverse++)
            {
                var start=reverse==0?lane.start:lane.end;var end=reverse==0?lane.end:lane.start;var direction=(end-start).normalized;
                player.DebugMove(null);body.linearVelocity=Vector3.zero;body.position=new Vector3(start.x,Ground(start)+.06f,start.y);
                body.rotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));Physics.SyncTransforms();
                typeof(PlayerControlSpeed).GetField("<Stamina>k__BackingField",flags).SetValue(player,player.maxStamina);
                typeof(PlayerControlSpeed).GetField("exhausted",flags).SetValue(player,false);
                for(int settle=0;settle<4;settle++)yield return null;
                float speed=sprint==0?player.walkSpeed:player.runSpeed;
                int ticks=Mathf.CeilToInt((Vector2.Distance(start,end)/speed+1.5f)/.02f);
                for(int tick=0;tick<ticks;tick++)
                {
                    var current=new Vector2(body.position.x,body.position.z);
                    if(Vector2.Dot(end-current,direction)<.12f)break;
                    var forward=Vector3.ProjectOnPlane(Camera.main.GetComponent<CameraFollow>().LookDirection,Vector3.up).normalized;
                    var right=Vector3.Cross(Vector3.up,forward);var heading=new Vector3(direction.x,0,direction.y);
                    player.DebugMove(new Vector2(Vector3.Dot(heading,right),Vector3.Dot(heading,forward)),sprint==1);yield return null;
                }
                player.DebugMove(null);var final=new Vector2(body.position.x,body.position.z);float remaining=Vector2.Dot(end-final,direction);
                float sideways=Mathf.Abs(Vector2.Dot(final-start,new Vector2(-direction.y,direction.x)));
                Check(remaining<.3f&&sideways<.75f,$"Rigidbody {(sprint==1?"sprint":"walk")} {(reverse==0?"forward":"reverse")} through {lane.between} ({remaining:F2}m left, {sideways:F2}m lateral)");
            }
        }
        finally
        {
            if(creature!=null)Destroy(creature);player.DebugMove(null);Time.captureDeltaTime=capture;
            body.position=originalPosition;body.rotation=originalRotation;body.linearVelocity=Vector3.zero;
            typeof(PlayerControlSpeed).GetField("<Stamina>k__BackingField",flags).SetValue(player,stamina);
            typeof(PlayerControlSpeed).GetField("exhausted",flags).SetValue(player,exhausted);
            gm.enabled=managerEnabled;GameSettings.SfxVolume=effectsVolume;
        }
        yield return Wait(.2f);
    }
}
#endif
