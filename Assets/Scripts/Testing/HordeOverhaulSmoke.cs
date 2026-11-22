#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

public partial class SmokeTestDriver
{
    IEnumerator StartHordeReview()
    {
        yield return Wait(2.5f);GameSettings.Mode=GameMode.EndlessHorde;
        FindFirstObjectByType<MainController>().StartGame();
        float timeout=Time.realtimeSinceStartup+90;
        while((GameManager.instance==null||GameManager.instance.State!=GameManager.MatchState.Playing)&&Time.realtimeSinceStartup<timeout)yield return null;
        Check(GameManager.instance!=null&&GameManager.instance.Wave==1,"Horde starts through the real main menu and loading flow");
        var knight=GameObject.FindWithTag("Player");
        if(knight!=null)yield return ReviewHordeSystems(knight.GetComponent<PlayerControlSpeed>(),knight.GetComponent<PlayerHealth>());
    }
    IEnumerator ReviewHordeSystems(PlayerControlSpeed player,PlayerHealth health)
    {
        var gm=GameManager.instance;var director=gm.Horde;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var fields=new Dictionary<FieldInfo,object>();
        foreach(string name in new[]{"<Wave>k__BackingField","<State>k__BackingField","<Boss>k__BackingField","<Score>k__BackingField","<RecoveryRemaining>k__BackingField","<Combo>k__BackingField","<BestCombo>k__BackingField","<BoonsOffered>k__BackingField","<IntermissionLeft>k__BackingField","<HordeGrave>k__BackingField","comboExpires","waveToSpawn","waveSpawned","spawnTimer","damageAtWaveStart","lastThreatCue","currentKills","draftPending","graveWasActive","hordeGraveRaised","nextHordeGraveAttempt"})
        {var field=typeof(GameManager).GetField(name,flags|BindingFlags.Public);fields[field]=field.GetValue(gm);}
        bool manager=gm.enabled,control=player.enabled;var body=player.GetComponent<Rigidbody>();bool kinematic=body.isKinematic;
        Vector3 position=player.transform.position;Quaternion rotation=player.transform.rotation;
        float invulnerable=health.InvulnerableUntil,hp=health.currentHealth;
        var owned=Boons.Owned.ToDictionary(x=>x.def.id,x=>x.stacks);
        var stackField=typeof(Boons).GetField("stacks",BindingFlags.Static|BindingFlags.NonPublic);
        var stacks=(Dictionary<BoonId,int>)stackField.GetValue(null);
        var originalPickups=new HashSet<Pickup>(FindObjectsByType<Pickup>(FindObjectsSortMode.None));
        var kills=gm.killsByType.ToDictionary(x=>x.Key,x=>x.Value);
        var fixtures=new List<GameObject>();
        void ClearEnemies(){foreach(var enemy in EnemyController.All.ToArray())if(enemy!=null)Destroy(enemy.gameObject);}
        EnemyController Enemy(ZombieType type,Vector3 point,bool navigate=false)
        {
            if(NavMesh.SamplePosition(point,out var nav,3,NavMesh.AllAreas))point=nav.position;
            var go=Instantiate(BattleLoading.ZombiePrefab,point,Quaternion.LookRotation(-player.transform.forward));fixtures.Add(go);
            var enemy=go.GetComponent<EnemyController>();enemy.Init(type,false,false);
            go.GetComponent<NavMeshAgent>().enabled=navigate;return enemy;
        }
        void Wave(int n)=>typeof(GameManager).GetMethod("StartWave",flags).Invoke(gm,new object[]{n});
        void Photograph(string name,Vector3 target,Transform ignore=null,float radius=5)
        {
            for(int i=0;i<24;i++)
            {
                Vector3 view=target+Quaternion.Euler(0,i*137.5f,0)*Vector3.forward*radius+Vector3.up*1.8f;
                if(!CombatBoons.ClearLine(target,view,ignore)||!CombatBoons.ClearLine(view,target,ignore))continue;
                CaptureFrom(name,view,target);return;
            }
            Check(false,"review camera has an unobstructed view of "+name);
        }
        try
        {
            gm.enabled=false;player.enabled=false;body.linearVelocity=Vector3.zero;body.isKinematic=true;health.InvulnerableUntil=float.PositiveInfinity;
            director.ClearWave();ClearEnemies();yield return null;
            var seen=new HashSet<HordeEvent>();
            for(int n=1;n<=12;n++)
            {
                Wave(n);
                Check((n%3==0)==(director.Current!=HordeEvent.None),$"director event cadence is correct on wave {n}");
                if(n%3==0&&n<=9)seen.Add(director.Current);
                if(n%5==0)Check(EnemyController.All.Any(e=>e.type==ZombieType.Brute),$"wave {n} includes its promised Brute");
                if(n%10==0)Check(EnemyController.All.Any(e=>e.type==ZombieType.Warlord),"tenth wave adds a Warlord alongside the Brute");
                ClearEnemies();director.ClearWave();yield return null;
            }
            Check(seen.Count==3,"the first three chaos waves cover all events without repeats");
            foreach (int wave in new[] { 1, 5, 10, 15, 20, 25 })
            {
                Wave(wave);
                var walker = Enemy(ZombieType.Walker, position + player.transform.forward * 4f);
                var tier = HordeDirector.Pressure(wave);
                var profile = GameSettings.Profile;
                float damage = (float)typeof(EnemyController).GetField("damage", flags).GetValue(walker);
                float speed = (float)typeof(EnemyController).GetField("baseSpeed", flags).GetValue(walker);
                float targetSpeed = walker.Stats.speed * profile.enemySpeed * tier.Speed;
                Check(Mathf.Approximately(walker.MaxHealth, walker.Stats.health * profile.enemyHealth * tier.Health) &&
                    Mathf.Approximately(damage, walker.Stats.damage * profile.enemyDamage * tier.Damage) &&
                    speed >= targetSpeed * .899f && speed <= targetSpeed * 1.121f,
                    $"wave {wave} applies its tier's health, move speed and damage to a live Walker");
                yield return null;
                var label = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).FirstOrDefault(t => t.text == tier.Name);
                Check(label != null && (label.color - tier.Accent).maxColorComponent <= .01f,
                    $"wave {wave} presents the {tier.Name} tier in the HUD");
                if (wave == 20 || wave == 25) Capture($"horde_tier_{wave:00}");
                ClearEnemies(); director.ClearWave(); yield return null;
            }
            Check(HordeDirector.Pressure(25).Health > HordeDirector.Pressure(20).Health * 1.4f &&
                Feats.All.Any(f => f.id == "last_oath"), "wave 25 is a steep escalation past the wave 20 feat");
            for(int wave=1;wave<=3;wave++)
            {
                Wave(wave);ClearEnemies();yield return null;
                var spawned=typeof(GameManager).GetField("waveSpawned",flags).GetValue(gm);
                typeof(GameManager).GetField("waveToSpawn",flags).SetValue(gm,spawned);
                typeof(GameManager).GetMethod("UpdateMode",flags).Invoke(gm,null);
                var intermission = typeof(GameManager).GetMethod("UpdateHordeIntermission", flags);
                intermission.Invoke(gm, new object[] { 1.9f });
                Check(!gm.ChoosingBoon && gm.State == GameManager.MatchState.Intermission,
                    $"wave {wave} lets the clear settle before drafting");
                intermission.Invoke(gm, new object[] { .11f });
                var hud=FindFirstObjectByType<GameHUD>();
                var choices=(Boons.Def[])typeof(GameHUD).GetField("boonChoices",flags).GetValue(hud);
                Check(gm.ChoosingBoon&&choices.Length==3&&choices.Select(d=>d.id).Distinct().Count()==3,$"wave {wave} ends in three distinct boon cards");
                intermission.Invoke(gm, new object[] { 20f });
                Check(gm.Wave == wave && gm.ChoosingBoon, "a draft stays open without starting the next wave underneath it");
                Check(choices.Any(d=>Boons.IsSignature(d.id)),"Horde draft guarantees a signature power");
                yield return null;
                Check(!hud.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.transform.parent.name.StartsWith("Boon ")&&t.isTextOverflowing),"boon card titles and descriptions fit their panels");
                if(wave==1)Capture("horde_01_draft");
                int before=Boons.Total;GameHUD.DebugPickBoon(0);
                Check(!gm.ChoosingBoon&&Boons.Total==before+1&&Time.timeScale==1,"one choice grants exactly one boon and resumes simulation");
            }
            Boons.Reset();
            foreach(BoonId id in System.Enum.GetValues(typeof(BoonId)))stacks[id]=Boons.IsSignature(id)?1000:Boons.Get(id).maxStacks;
            var endless=Boons.Roll(3,true);
            Check(endless.Length==3&&endless.All(d=>Boons.IsSignature(d.id)),"late-run drafts never exhaust after every finite boon is capped");
            Boons.Reset();
            Wave(13);ClearEnemies();yield return null;
            var first=Enemy(ZombieType.Brute,position+player.transform.forward*2.3f);
            var second=Enemy(ZombieType.Brute,position+player.transform.forward*5.4f);
            var third=Enemy(ZombieType.Brute,position+player.transform.forward*8.5f);
            Boons.Apply(BoonId.ShockwaveParry,null);Boons.Apply(BoonId.ShockwaveParry,null);
            Physics.SyncTransforms();float beforeHealth=third.Health;
            CombatBoons.Parry(health,position+Vector3.up);
            Check(first.IsStaggered&&second.IsStaggered&&third.IsStaggered&&third.Health<beforeHealth,"parry lightning chains beyond the initial shockwave, damages and stuns");
            Photograph("horde_02_lightning",second.HeadPosition);yield return Wait(.35f);
            ClearEnemies();yield return null;Boons.Reset();
            first=Enemy(ZombieType.Brute,position+player.transform.right*2.8f,true);
            typeof(EnemyController).GetField("player",BindingFlags.Instance|BindingFlags.Public).SetValue(first,null);
            Boons.Apply(BoonId.WhirlwindStrike,null);Boons.Apply(BoonId.ReapersCleave,null);
            float distance=Vector3.Distance(first.transform.position,position+player.transform.forward*.75f);beforeHealth=first.Health;
            typeof(PlayerControlSpeed).GetMethod("CheckForHits",flags).Invoke(player,new object[]{2,2});
            Check(first.IsBleeding,"actual third sword cut applies Whirlwind bleed");
            yield return Wait(.4f);
            Check(Vector3.Distance(first.transform.position,position+player.transform.forward*.75f)<distance-.15f,"Whirlwind pulls a navigating enemy inward without teleporting");
            Check(first.Health<beforeHealth,"Whirlwind bleeding damages a moving survivor");
            ClearEnemies();yield return null;Boons.Reset();
            first=Enemy(ZombieType.Brute,position+player.transform.forward*2.3f);
            Boons.Apply(BoonId.ExplosiveQuiver,null);Boons.Apply(BoonId.FireArrows,null);
            var arrow=Arrow.Fire(position+Vector3.up*2,player.transform.forward,30,.4f,0,player.transform,0,true);fixtures.Add(arrow.gameObject);
            var hit=typeof(Arrow).GetMethod("HitEnemy",flags);
            hit.Invoke(arrow,new object[]{first,first.HeadPosition});
            int volleys=FindObjectsByType<ClusterVolley>(FindObjectsSortMode.None).Length;
            hit.Invoke(arrow,new object[]{first,first.HeadPosition});
            Check(volleys==1&&FindObjectsByType<ClusterVolley>(FindObjectsSortMode.None).Length==1,"piercing headshots trigger only one cluster volley per arrow");
            beforeHealth=first.Health;yield return Wait(.85f);
            Check(first.Health<beforeHealth&&first.IsBurning,"delayed headshot bombs deal cluster damage and inherit fire");
            ClearEnemies();yield return null;Boons.Reset();
            director.Activate(HordeEvent.BloodMoon);
            first=Enemy(ZombieType.Walker,position+player.transform.forward*3,true);
            yield return null;
            var agent=first.GetComponent<NavMeshAgent>();float baseSpeed=(float)typeof(EnemyController).GetField("baseSpeed",flags).GetValue(first);
            Check(Mathf.Abs(HordeDirector.Speed-1.25f)<.001f,"Blood Moon applies exactly 25 percent extra movement speed");
            typeof(EnemyController).GetMethod("Chase",flags).Invoke(first,new object[]{position,baseSpeed});
            Check(Mathf.Abs(agent.speed-baseSpeed*1.25f)<.001f,"Blood Moon speed reaches the live navigation agent");
            var properties=new MaterialPropertyBlock();first.GetComponent<RevenantVisual>().Body.GetPropertyBlock(properties);
            Check(properties.GetFloat("_BloodMoon")==1,"Blood Moon enables red eyes without making outfits emissive");
            int score=gm.Score,expected=first.Stats.points*gm.ComboMultiplier*3;
            // Keep combo below the next multiplier boundary for an exact score assertion.
            typeof(GameManager).GetField("<Combo>k__BackingField",flags).SetValue(gm,0);expected=first.Stats.points*3;score=gm.Score;
            first.TakeDamage(999,Vector3.forward,0);
            Check(gm.Score-score==expected,"Blood Moon awards exactly three times ordinary kill score");
            yield return Wait(.2f);director.ClearWave();
            director.Activate(HordeEvent.ToxicFog);
            Check(director.FogCount>0,"Toxic Fog populates the verified low village passages");
            var decks=(List<Vector3>)typeof(HordeDirector).GetField("bridges",flags).GetValue(director);
            // The elevated Keep bridge and the village floor occupy different islands
            // in the shipped bake. Verify the guide from both the floor and a real deck.
            var routeToDeck = new NavMeshPath();
            bool reachable = decks.Any(p => !director.InGas(p) &&
                NavMesh.CalculatePath(player.transform.position, p, NavMesh.AllAreas, routeToDeck) &&
                routeToDeck.status == NavMeshPathStatus.PathComplete);
            Check(director.HasRefuge == reachable, "the fog guide only offers decks reachable from the knight");
            Check(decks.Count > 0, "the castle bridge has walkable deck samples");
            if (decks.Count > 0)
            {
                Vector3 saved = body.position;
                player.transform.position = body.position = decks[0];
                Physics.SyncTransforms();
                director.Activate(HordeEvent.ToxicFog);
                Check(director.HasRefuge && !director.InGas(director.Refuge) &&
                    NavMesh.CalculatePath(decks[0], director.Refuge, NavMesh.AllAreas, routeToDeck) &&
                    routeToDeck.status == NavMeshPathStatus.PathComplete,
                    "the fog guide selects a safe reachable bridge deck from the elevated route");
                player.transform.position = body.position = saved;
                Physics.SyncTransforms();
                director.Activate(HordeEvent.ToxicFog);
            }
            var lanes=(List<HordeDirector.Lane>)typeof(HordeDirector).GetField("fog",flags).GetValue(director);
            Vector3 low=(lanes[0].start+lanes[0].end)*.5f;
            Check(director.InGas(low)&&!director.InGas(low+Vector3.up*2),"gas occupancy respects local alley height and leaves elevated surfaces safe");
            Check(!director.GasActive,"Toxic Fog gives a safe warning before dealing damage");
            yield return Wait(4.8f);
            player.transform.position=low;body.position=low;health.currentHealth=health.maxHealth;health.InvulnerableUntil=-1;
            float hpBefore=health.currentHealth;yield return Wait(.65f);
            Check(health.currentHealth<hpBefore,"standing in active low alley gas damages the player");
            health.InvulnerableUntil=float.PositiveInfinity;
            Photograph("horde_03_toxic_alley",low+Vector3.up*.6f,null,2.2f);
            player.transform.position=position;body.position=position;director.ClearWave();yield return null;
            Check(FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).All(p=>p.name!="Low alley gas"),"gas particles and damage volumes are removed at wave end");
            director.Activate(HordeEvent.CursedTotem);yield return Wait(3.5f);
            var totem=director.Totem;
            Check(totem!=null&&totem.IsRaised,"Cursed Totem rises on a reachable navigation surface");
            if(totem!=null)
            {
                foreach(ZombieType type in System.Enum.GetValues(typeof(ZombieType)))
                {
                    var enemy=Enemy(type,totem.transform.position+Vector3.right*3);
                    enemy.TakeDamage(999,Vector3.forward,0);enemy.Ignite(3,100);enemy.Bleed(3,100);
                    fixtures.Add(enemy.gameObject);
                    Check(!enemy.IsDead&&enemy.Health>0,$"totem prevents lethal weapon damage on {type}");
                }
                yield return Wait(.4f);
                Check(EnemyController.All.All(e=>!e.IsDead&&e.Health>0),"the curse also prevents burn and bleed deaths");
                int live=EnemyController.All.Count;
                Check(live==ZombieTypes.Count,"the objective is separate from enemy wave counts");
                Photograph("horde_04_totem",totem.Target,totem.transform);
                player.transform.position=totem.transform.position+Vector3.back*1.6f;body.position=player.transform.position;player.transform.rotation=Quaternion.identity;Physics.SyncTransforms();
                float totemHealth=totem.Health;
                typeof(PlayerControlSpeed).GetMethod("CheckForHits",flags).Invoke(player,new object[]{0,0});
                Check(totem.Health<totemHealth,"a real sword swing damages the obelisk exactly once despite overlap queries");
                var actualArrow=Arrow.Fire(totem.Target+Vector3.back*3,Vector3.forward,28,2,0,player.transform,0,false);fixtures.Add(actualArrow.gameObject);
                totemHealth=totem.Health;yield return Wait(.35f);
                Check(totem.Health<totemHealth,"a ballistic arrow physically hits and damages the obelisk collider");
                totem.ReceiveHit(999,totem.Target);yield return Wait(.35f);
                Check(totem.IsBroken&&!EnemyController.All.Any(e=>director.Protects(e)),"destroying the totem immediately removes immortality");
                foreach(var enemy in EnemyController.All.ToArray())enemy.TakeDamage(999,Vector3.forward,0);
                yield return Wait(.6f);
                Check(EnemyController.All.Count==0,"all cursed archetypes can die after the totem breaks, including Bloaters");
            }
            director.ClearWave();ClearEnemies();yield return null;
            Check(director.Current==HordeEvent.None&&director.Totem==null&&director.FogCount==0,"wave cleanup clears event state, hazard objects and protection");
            yield return ReviewGoblet(player);
            ClearEnemies();
            yield return null;
            Wave(8);
            var tick = typeof(GameManager).GetMethod("UpdateMode", flags);
            typeof(GameManager).GetField("waveSpawned", flags).SetValue(gm, typeof(GameManager).GetField("waveToSpawn", flags).GetValue(gm));
            player.enabled = true;
            float graveDeadline = Time.realtimeSinceStartup + 9f;
            tick.Invoke(gm, null);
            Check(gm.HordeGraveActive && !player.enabled, "a quiet point in wave 8 raises the grave and locks the reveal");
            bool capturedGrave = false;
            while (gm.HordeGraveActive && gm.HordeGrave.Revealing && Time.realtimeSinceStartup < graveDeadline)
            {
                tick.Invoke(gm, null);
                if (!capturedGrave && gm.HordeGrave.Age > 4.7f)
                {
                    Capture("horde_grave_reveal");
                    capturedGrave = true;
                }
                yield return null;
            }
            var duel = gm.HordeGrave != null ? gm.HordeGrave.Duelist : null;
            Check(duel != null && player.enabled && duel.CanFight && gm.Wave == 8 &&
                Mathf.Approximately(gm.AmbientSpawnRateMultiplier, .2f),
                "the live grave duel holds the round open and eases regular reinforcements");
            if (duel != null)
            {
                duel.GetComponent<EnemyController>().TakeDamage(999f, Vector3.forward, 0f);
                yield return null;
                tick.Invoke(gm, null);
                Check(gm.State == GameManager.MatchState.Intermission && gm.RecoveryRemaining > 0f,
                    "defeating the revenant resumes round completion");
            }
        }
        finally
        {
            if (gm.HordeGrave != null) Destroy(gm.HordeGrave.gameObject);
            director.ClearWave();ClearEnemies();foreach(var go in fixtures)if(go!=null)Destroy(go);
            foreach(var pickup in FindObjectsByType<Pickup>(FindObjectsSortMode.None))if(!originalPickups.Contains(pickup))Destroy(pickup.gameObject);
            Boons.Reset();foreach(var item in owned)stacks[item.Key]=item.Value;
            foreach(var item in fields)item.Key.SetValue(gm,item.Value);
            gm.killsByType.Clear();foreach(var item in kills)gm.killsByType[item.Key]=item.Value;
            health.InvulnerableUntil=invulnerable;health.currentHealth=hp;player.transform.SetPositionAndRotation(position,rotation);body.position=position;
            body.isKinematic=kinematic;player.enabled=control;gm.enabled=manager;CombatTime.SetScale(1);
        }
        yield return null;
    }
}
#endif
