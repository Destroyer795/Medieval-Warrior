#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

public partial class SmokeTestDriver
{
    IEnumerator ReviewCombatOverhaul(PlayerControlSpeed player,PlayerHealth health)
    {
        var gm=GameManager.instance;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var body=player.GetComponent<Rigidbody>();bool kinematic=body.isKinematic,controlEnabled=player.enabled;
        bool managerEnabled=gm.enabled;float invulnerable=health.InvulnerableUntil,hp=health.currentHealth;
        float maxHealth=health.maxHealth,maxStamina=player.maxStamina;int maxArrows=player.maxArrows,arrows=player.Arrows;
        float fixedStep=Time.fixedDeltaTime;var fixtures=new List<GameObject>();
        var originalPickups=new HashSet<Pickup>(FindObjectsByType<Pickup>(FindObjectsSortMode.None));
        var originalKills=gm.killsByType.ToDictionary(p=>p.Key,p=>p.Value);
        var savedFields=new Dictionary<FieldInfo,object>();
        foreach(string name in new[]{"<Mode>k__BackingField","<Wave>k__BackingField","<State>k__BackingField","<Boss>k__BackingField",
            "<Score>k__BackingField","<ArrowsHit>k__BackingField","<Headshots>k__BackingField","<AtmosphereLevel>k__BackingField",
            "<Combo>k__BackingField","<BestCombo>k__BackingField","<BoonsOffered>k__BackingField","comboExpires",
            "waveToSpawn","waveSpawned","spawnTimer","damageAtWaveStart","lastThreatCue","currentKills"})
        {
            var field=typeof(GameManager).GetField(name,flags|BindingFlags.Public);savedFields[field]=field.GetValue(gm);
        }
        EnemyController Enemy(ZombieType type,Vector3 position)
        {
            if(NavMesh.SamplePosition(position,out var sample,2,NavMesh.AllAreas))position=sample.position;
            var go=Instantiate(ZombieSpawner.Instance.zombiePrefab,position,Quaternion.LookRotation(-player.transform.forward));
            fixtures.Add(go);var enemy=go.GetComponent<EnemyController>();enemy.Init(type,false,false);
            // Keep the brain enabled: OnDisable unregisters it from the live horde.
            // Disable only navigation to hold a stationary, damageable fixture.
            go.GetComponent<NavMeshAgent>().enabled=false;return enemy;
        }
        try
        {
            gm.enabled=false;player.enabled=false;body.linearVelocity=Vector3.zero;body.isKinematic=true;
            health.InvulnerableUntil=-99;health.currentHealth=50;Boons.Reset();
            int clips=0;
            foreach(string name in new[]{"zombie_groan0","zombie_groan1","zombie_groan2","zombie_attack","zombie_die","brute_roar",
                "spit","splat","bloater_fuse","explosion","emerge","sword_swing","sword_hit","shield_block","arrow_hit","arrow_flesh","dodge",
                "parry","thunder_guard","phantom_cleave","splinter","boss_horn","music_menu"})
            {
                var clip=Resources.Load<AudioClip>("Audio/"+name);if(clip!=null&&clip.samples>0&&clip.channels==1)clips++;
            }
            Check(clips==23,"all horror, combat, synergy and protected menu audio clips load in play mode");
            var pitches=new List<float>();
            for(int i=0;i<6;i++)
            {
                AudioManager.PlayAt("sword_swing",player.transform.position);
                pitches.AddRange(FindFirstObjectByType<AudioManager>().GetComponentsInChildren<AudioSource>().Where(a=>a.clip!=null&&a.clip.name=="sword_swing"&&a.isPlaying).Select(a=>a.pitch));
            }
            Check(pitches.Count>=6&&pitches.All(p=>p>=.92f&&p<=1.08f)&&pitches.Distinct().Count()>1,"positional combat voices vary within 0.92–1.08 pitch");
            AudioManager.Play2D("ui_click",.2f,1);
            Check(FindFirstObjectByType<AudioManager>().GetComponentsInChildren<AudioSource>().Any(a=>a.clip!=null&&a.clip.name=="ui_click"&&a.pitch==1),"UI audio retains its requested pitch");

            var front=Enemy(ZombieType.Brute,player.transform.position+player.transform.forward*1.6f);
            var nearby=Enemy(ZombieType.Walker,player.transform.position+player.transform.forward*2+player.transform.right*1.15f);
            var covered=Enemy(ZombieType.Walker,player.transform.position-player.transform.right*3);
            var cover=new GameObject("Boon visibility cover");fixtures.Add(cover);
            cover.transform.position=player.transform.position-player.transform.right*1.5f+Vector3.up;
            cover.transform.rotation=player.transform.rotation;cover.AddComponent<BoxCollider>().size=new Vector3(.2f,2.4f,1);
            Physics.SyncTransforms();
            float before=front.Health;
            typeof(PlayerControlSpeed).GetMethod("CheckForHits",flags).Invoke(player,new object[]{2,2});
            Check(front.Health<before&&CombatTime.IsHitStopped&&Time.timeScale==0,"a confirmed heavy sword finisher triggers micro hit-stop");
            yield return Wait(.12f);
            Check(Time.timeScale==1&&!CombatTime.IsHitStopped&&Time.fixedDeltaTime==fixedStep,"hit-stop expires on unscaled time without altering physics timestep");
            CombatTime.SetScale(.25f);FX.HitStop(.05f);yield return Wait(.12f);
            Check(Time.timeScale==.25f,"hit-stop restores underlying cinematic slow motion");CombatTime.SetScale(1);
            FX.HitStop(.08f);gm.Pause();yield return Wait(.12f);
            Check(Time.timeScale==0&&gm.IsPaused,"hit-stop completion cannot unpause the pause menu");gm.Resume();
            FX.HitStop(.08f);gm.OfferBoon();yield return Wait(.12f);
            Check(Time.timeScale==0&&gm.ChoosingBoon,"hit-stop completion cannot unpause boon selection");
            GameHUD.DebugPickBoon(0);Boons.Reset();
            health.maxHealth=maxHealth;player.maxStamina=maxStamina;player.maxArrows=maxArrows;player.AddArrows(arrows-player.Arrows);health.currentHealth=50;
            Check(Time.timeScale==1&&!gm.ChoosingBoon,"choosing a boon restores normal time after a cancelled hit-stop");

            Boons.Apply(BoonId.ThunderGuard,player.gameObject);Boons.Apply(BoonId.VampiricRiposte,player.gameObject);
            health.ActivateParry();health.TakeDamage(10,front.transform,0);
            Check(front.IsStaggered&&nearby.IsStaggered,$"parry staggers its attacker and Thunder Guard stuns nearby undead ({front.IsStaggered}/{nearby.IsStaggered}, {Vector3.Distance(player.transform.position,nearby.transform.position):F2}m)");
            Check(!covered.IsStaggered,"Thunder Guard respects physical cover instead of stunning through walls");
            Check(Mathf.Abs(health.currentHealth-56)<.01f,"Vampiric Riposte restores six health on a successful parry");
            Check(GameObject.Find("Perfect parry flash")!=null,"successful parry produces a distinct bright flash");
            yield return Wait(1);
            health.isShieldBlocking=true;yield return null;before=health.currentHealth;
            health.TakeDamage(10,front.transform,0);
            Check(front.IsStaggered&&health.currentHealth==Mathf.Min(health.maxHealth,before+6),"perfect shield block prevents damage, staggers and triggers riposte healing");
            health.isShieldBlocking=false;yield return Wait(.13f);

            Boons.Apply(BoonId.ReapersCleave,player.gameObject);
            var side=Enemy(ZombieType.Walker,player.transform.position+Quaternion.AngleAxis(80,Vector3.up)*player.transform.forward*1.45f);
            before=side.Health;typeof(PlayerControlSpeed).GetMethod("CheckForHits",flags).Invoke(player,new object[]{2,2});
            Check(side.Health<before&&side.IsBurning,"Reaper's third strike reaches outside the normal arc and ignites phantom flame");
            yield return Wait(.12f);

            Boons.Apply(BoonId.SplinterVolley,player.gameObject);Boons.Apply(BoonId.SplinterVolley,player.gameObject);
            Check(Boons.Stacks(BoonId.SplinterVolley)==1,"transformative boons respect their stack cap");
            var cluster=Enemy(ZombieType.Walker,front.transform.position+player.transform.right*1.1f-player.transform.forward*.2f);
            var shieldedCluster=Enemy(ZombieType.Walker,front.transform.position-player.transform.right*1.4f-player.transform.forward*.3f);
            var splinterCover=new GameObject("Shrapnel visibility cover");fixtures.Add(splinterCover);
            splinterCover.transform.position=front.transform.position-player.transform.right*.7f-player.transform.forward*.15f+Vector3.up*1.5f;
            splinterCover.transform.rotation=player.transform.rotation;splinterCover.AddComponent<BoxCollider>().size=new Vector3(.2f,3,.9f);
            Physics.SyncTransforms();
            var arrow=Arrow.Fire(player.transform.position+Vector3.up*2.2f,player.transform.forward,30,.3f,0,player.transform,2,true);
            fixtures.Add(arrow.gameObject);before=cluster.Health;
            typeof(Arrow).GetMethod("HitEnemy",flags).Invoke(arrow,new object[]{front,front.transform.position+Vector3.up*2.6f});
            Check(cluster.Health<before,"Splinter Volley damages a nearby cluster through an actual arrow contact");
            Check(shieldedCluster.Health==shieldedCluster.MaxHealth,"arrow shrapnel respects physical wall cover");
            Check(CombatTime.IsHitStopped,"a confirmed arrow critical triggers micro hit-stop");
            before=cluster.Health;typeof(Arrow).GetMethod("BurstSplinters",flags).Invoke(arrow,new object[]{front.transform.position+Vector3.up,front});
            Check(cluster.Health==before&&arrow.IsFlaming&&cluster.IsBurning,"piercing arrows trigger shrapnel once and fire splinters ignite nearby enemies");Destroy(arrow.gameObject);
            yield return Wait(.12f);
            Check(Time.timeScale==1,"arrow critical hit-stop restores gameplay time");

            typeof(GameManager).GetField("<Mode>k__BackingField",flags).SetValue(gm,GameMode.EndlessHorde);
            var startWave = typeof(GameManager).GetMethod("StartWave", flags);
            startWave.Invoke(gm, new object[] { 2 });
            Check(HearCue("wave_horn"), "ordinary waves play the recorded trumpet call at its original pitch");
            startWave.Invoke(gm, new object[] { 5 });
            if(gm.Boss!=null){fixtures.Add(gm.Boss.gameObject);gm.Boss.enabled=false;}
            Check(gm.Boss!=null&&gm.Boss.type==ZombieType.Brute&&gm.Boss.isBoss,"fifth wave creates its Brute champion");
            Check(HearCue("boss_horn"), "champion waves play the recorded boss call at its original pitch");
            Check(FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None).Any(t=>t.text.Contains("A BRUTE LEADS THE DEAD")),"champion wave displays a bold threat banner");
            float fog=RenderSettings.fogDensity,level=gm.AtmosphereLevel;
            typeof(GameManager).GetField("<Wave>k__BackingField",flags).SetValue(gm,15);
            for(int i=0;i<240;i++)typeof(GameManager).GetMethod("UpdateBattleAtmosphere",flags).Invoke(gm,null);
            Check(gm.AtmosphereLevel>level&&gm.AtmosphereLevel<=1&&RenderSettings.fogDensity>fog,"later waves progressively deepen fog and Blood Moon atmosphere");
            CaptureFrom("combat_overhaul_blood_moon",player.transform.position-player.transform.forward*5+Vector3.up*3,player.transform.position+Vector3.up);
            Check(Boons.Roll(99).All(d=>Boons.Stacks(d.id)<d.maxStacks),"boon offers exclude fully owned transformations");
        }
        finally
        {
            CombatTime.SetScale(1);Boons.Reset();foreach(var go in fixtures)if(go!=null)Destroy(go);
            foreach(var pickup in FindObjectsByType<Pickup>(FindObjectsSortMode.None))if(!originalPickups.Contains(pickup))Destroy(pickup.gameObject);
            gm.killsByType.Clear();foreach(var pair in originalKills)gm.killsByType[pair.Key]=pair.Value;
            typeof(GameManager).GetMethod("RestoreAtmosphere",flags).Invoke(gm,null);
            foreach(var pair in savedFields)pair.Key.SetValue(gm,pair.Value);
            health.isShieldBlocking=false;health.InvulnerableUntil=invulnerable;health.maxHealth=maxHealth;health.currentHealth=hp;
            player.maxStamina=maxStamina;player.maxArrows=maxArrows;player.AddArrows(arrows-player.Arrows);
            body.isKinematic=kinematic;player.enabled=controlEnabled;gm.enabled=managerEnabled;GameHUD.Banner("","",.01f);
        }
        yield return null;
    }
}
#endif
