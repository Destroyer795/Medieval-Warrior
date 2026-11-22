using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum HordeEvent { None, BloodMoon, ToxicFog, CursedTotem }

// One wave owns an event. Scene-owned hazards never survive a draft or restart.
public sealed class HordeDirector : MonoBehaviour
{
    internal readonly struct Tier
    {
        public readonly string Name;
        public readonly Color Accent;
        public readonly float Health, Speed, Damage;

        public Tier(string name, Color accent, float health, float speed, float damage)
        {
            Name = name;
            Accent = accent;
            Health = health;
            Speed = speed;
            Damage = damage;
        }
    }

    static readonly Tier[] Tiers =
    {
        new("I · Rising", new Color(.87f, .75f, .51f), 1f, 1f, 1f),
        new("II · Relentless", new Color(.62f, .85f, .96f), 1.18f, 1.06f, 1.12f),
        new("III · Dread", new Color(.80f, .69f, .99f), 1.43f, 1.13f, 1.25f),
        new("IV · Bloodbound", new Color(.99f, .68f, .49f), 1.75f, 1.20f, 1.43f),
        new("V · Last Oath", new Color(1f, .84f, .43f), 2.18f, 1.27f, 1.63f),
        new("VI · Abyss", new Color(1f, .54f, .57f), 3.25f, 1.42f, 2.15f)
    };

    internal static Tier Pressure(int wave)
    {
        int rank = Mathf.Max(0, wave / 5);
        if (rank < Tiers.Length) return Tiers[rank];
        var abyss = Tiers[Tiers.Length - 1];
        int beyond = rank - (Tiers.Length - 1);
        return new Tier(abyss.Name, abyss.Accent, abyss.Health * (1f + .27f * beyond),
            abyss.Speed + Mathf.Min(.25f, .045f * beyond), abyss.Damage * (1f + .22f * beyond));
    }
    [Serializable] public class Lane { public Vector3 start, end; public float width; }
    [Serializable] public class Arena { public Lane[] lanes; public string[] bridgeKeys; }
    public HordeEvent Current { get; private set; }
    public HordeTotem Totem { get; private set; }
    public HordeGoblet Goblet { get; private set; }
    public bool GobletActive { get; private set; }
    public bool CanClaimGoblet => Goblet != null && health != null && !health.IsDead &&
        GameManager.instance is GameManager match && match.Mode == GameMode.EndlessHorde &&
        match.State == GameManager.MatchState.Playing && match.ReinforcementsPending && !match.IsPaused && !match.ChoosingBoon &&
        Goblet.InReach(player.position);
    public float Age { get; private set; }
    public float MoonBlend { get; private set; }
    public int FogCount=>fog.Count;
    public Vector3 Refuge { get; private set; }
    public bool HasRefuge { get; private set; }
    public bool GasActive=>Current==HordeEvent.ToxicFog&&Age>=4.5f;
    public float SpeedMultiplier=>(Current==HordeEvent.BloodMoon?1.25f:1f)*(GobletActive?2f:1f);
    public int ScoreMultiplier=>Current==HordeEvent.BloodMoon?3:1;
    public static HordeDirector Active=>GameManager.instance!=null?GameManager.instance.Horde:null;
    public static float Speed=>Active!=null?Active.SpeedMultiplier:1;
    public static float GuardHealth(EnemyController enemy,float value)
    {
        if(value<=0&&Active!=null&&Active.Protects(enemy))
        {
            Active.Totem.Deflect(enemy.HeadPosition);return .25f;
        }
        return value;
    }
    public bool Protects(EnemyController enemy)=>Current==HordeEvent.CursedTotem&&Totem!=null&&Totem.Protects(enemy.transform.position);
    public string Title=>Current==HordeEvent.BloodMoon?"BLOOD MOON":Current==HordeEvent.ToxicFog?"TOXIC FOG":Current==HordeEvent.CursedTotem?"CURSED TOTEM":"";
    public string Objective=>Current switch
    {
        HordeEvent.BloodMoon=>"Undead speed +25%  •  Kill score ×3",
        HordeEvent.ToxicFog=>Age<4.5f?"Gas is rising — reach a high stone bridge!":InGas(player!=null?player.transform.position:Vector3.zero)?"GAS EXPOSURE — CLIMB TO HIGH GROUND!":"Low alleys are poisoned • High ground is safe",
        HordeEvent.CursedTotem=>Totem!=null&&!Totem.IsBroken?$"Smash the obelisk • {Mathf.CeilToInt(Totem.HealthFraction*100)}% • Nearby dead are immortal":"Curse broken • Finish the wave",
        _=>""
    };
    readonly List<Lane> fog=new List<Lane>();
    readonly List<GameObject> visuals=new List<GameObject>();
    readonly List<Vector3> bridges=new List<Vector3>();
    Arena arena;
    Transform player;
    PlayerHealth health;
    HordeEvent previous;
    readonly List<HordeEvent> bag=new List<HordeEvent>();
    System.Random random;
    float damageTick;
    bool announced;
    Camera fogCamera;
    DepthTextureMode previousDepth;
    bool gobletUsed;
    HordeGoblet.Site gobletSite;
    bool gobletSiteFound;
    internal Vector3 OpeningSpawn { get; private set; }

    public void Prepare(PlayerHealth knight)
    {
        health=knight;player=knight!=null?knight.transform:null;
        if (player != null) OpeningSpawn = player.position;
        random=new System.Random(Environment.TickCount);
        var json=Resources.Load<TextAsset>("Horde/Arena");
        if(json!=null)arena=JsonUtility.FromJson<Arena>(json.text);
        CacheBridgeDecks();
    }
    public void BeginWave(int wave)
    {
        ClearWave();
        if (!gobletUsed && GameManager.instance != null && GameManager.instance.Mode == GameMode.EndlessHorde && player != null)
        {
            if (!gobletSiteFound) gobletSiteFound = HordeGoblet.TryFindSite(OpeningSpawn, out gobletSite);
            if (gobletSiteFound) Goblet = HordeGoblet.Create(gobletSite, transform);
        }
        if(wave%3!=0)return;
        if(bag.Count==0)bag.AddRange(new[]{HordeEvent.BloodMoon,HordeEvent.ToxicFog,HordeEvent.CursedTotem});
        int index=random.Next(bag.Count);
        if(bag.Count>1&&bag[index]==previous)index=(index+1)%bag.Count;
        var next=bag[index];bag.RemoveAt(index);previous=next;
        Activate(next);
    }
    // Also used by the gameplay validation driver to cover every director event.
    public void Activate(HordeEvent next)
    {
        ClearEvent();Current=next;
        if(next==HordeEvent.ToxicFog)BuildFog();
        if(next==HordeEvent.CursedTotem)
        {
            if(TryTotemPosition(out var at))Totem=HordeTotem.Create(at,transform);
            else { Current=HordeEvent.BloodMoon; } // No unreachable immortal objective on a missing NavMesh.
        }
    }
    void Update()
    {
        var gm=GameManager.instance;
        if(gm==null||gm.IsPaused||gm.ChoosingBoon||Time.deltaTime<=0)return;
        if (Goblet != null && GameInput.DrinkDown) TryClaimGoblet();
        MoonBlend=Mathf.MoveTowards(MoonBlend,Current==HordeEvent.BloodMoon?1:0,Time.deltaTime*.45f);
        if(Current==HordeEvent.None||gm.State!=GameManager.MatchState.Playing)return;
        Age+=Time.deltaTime;
        if(!announced&&Age>=.3f)
        {
            announced=true;
            // A toast leaves the wave/boss banner readable on coinciding waves.
            GameHUD.Toast(Title+" — "+Objective,EventColor,Current==HordeEvent.CursedTotem?"skull":"shield",5f,29);
            AudioManager.Play2D("boss_horn",.55f);
        }
        if(GasActive&&health!=null&&!health.IsDead)
        {
            damageTick+=Time.deltaTime;
            if(damageTick>=.5f)
            {
                damageTick=0;
                if(InGas(player.position))health.TakeDamage(7f,null,0,false);
            }
        }
    }
    public Color EventColor=>Current==HordeEvent.BloodMoon?new Color(1,.27f,.22f):Current==HordeEvent.ToxicFog?new Color(.65f,.92f,.33f):new Color(.76f,.5f,1);
    public bool InGas(Vector3 position)
    {
        foreach(var lane in fog)
        {
            Vector3 a=lane.start,b=lane.end,d=b-a,p=position-a;d.y=p.y=0;
            float t=Mathf.Clamp01(Vector3.Dot(p,d)/Mathf.Max(.001f,d.sqrMagnitude));
            Vector3 surface=Vector3.Lerp(a,b,t);Vector3 delta=position-surface;delta.y=0;
            if(delta.sqrMagnitude<lane.width*lane.width*.25f&&position.y>=surface.y-.5f&&position.y<surface.y+1.45f)return true;
        }
        return false;
    }
    void BuildFog()
    {
        if(arena?.lanes==null)return;
        fogCamera=Camera.main;
        if(fogCamera!=null){previousDepth=fogCamera.depthTextureMode;fogCamera.depthTextureMode|=DepthTextureMode.Depth;}
        // Authored passage data, sampled onto the current navigation surface.
        // Limit coverage to the low half of the village. Elevated decks remain safe.
        foreach(var source in arena.lanes)
        {
            if(!NavMesh.SamplePosition(source.start,out var a,2.5f,NavMesh.AllAreas)||
               !NavMesh.SamplePosition(source.end,out var b,2.5f,NavMesh.AllAreas))continue;
            if(Mathf.Max(a.position.y,b.position.y)>-.25f)continue;
            var lane=new Lane{start=a.position,end=b.position,width=Mathf.Clamp(source.width+.25f,2,5)};
            fog.Add(lane);
            visuals.Add(HordeVFX.Gas(lane,transform));
        }
        FindRefuge();
    }
    void FindRefuge()
    {
        float best=float.PositiveInfinity;
        var path=new NavMeshPath();
        foreach(var point in bridges)
        {
            if(InGas(point)||player!=null&&(!NavMesh.CalculatePath(player.position,point,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete))continue;
            float distance=player!=null?(point-player.position).sqrMagnitude:0;
            if(distance>=best)continue;
            best=distance;Refuge=point;HasRefuge=true;
        }
    }
    void CacheBridgeDecks()
    {
        if(arena?.bridgeKeys==null)return;
        foreach(var filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if(filter.sharedMesh==null)continue;
            // Static batching can replace the mesh name; modular node names survive.
            bool bridge=filter.name.StartsWith("Bridge",StringComparison.OrdinalIgnoreCase);
            foreach(string key in arena.bridgeKeys)if(filter.sharedMesh.name.StartsWith(key+"_LOD0",StringComparison.Ordinal)){bridge=true;break;}
            if(!bridge)continue;
            if(!filter.TryGetComponent<Renderer>(out var renderer))continue;
            var bounds=renderer.bounds;
            Vector3 probe=new Vector3(bounds.center.x,bounds.max.y+.3f,bounds.center.z);
            if(!Physics.Raycast(probe,Vector3.down,out var hit,bounds.size.y+2,~0,QueryTriggerInteraction.Ignore))continue;
            if(!NavMesh.SamplePosition(hit.point,out var sample,1.3f,NavMesh.AllAreas)||Mathf.Abs(sample.position.y-hit.point.y)>.5f)continue;
            bridges.Add(sample.position);
        }
    }
    bool TryTotemPosition(out Vector3 result)
    {
        result=default;if(player==null)return false;
        var path=new NavMeshPath();
        for(int i=0;i<24;i++)
        {
            Vector3 probe=player.position+Quaternion.Euler(0,i*137.5f,0)*Vector3.forward*(i<12?9:6);
            if(!NavMesh.SamplePosition(probe,out var sample,3,NavMesh.AllAreas))continue;
            if(!NavMesh.FindClosestEdge(sample.position,out var edge,NavMesh.AllAreas)||edge.distance<1.1f)continue;
            if(!NavMesh.CalculatePath(player.position,sample.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            result=sample.position;return true;
        }
        return false;
    }
    public void ClearWave()
    {
        GobletActive = false;
        RemoveGoblet();
        ClearEvent();
    }

    public bool TryClaimGoblet()
    {
        if (!CanClaimGoblet) return false;
        GobletActive = gobletUsed = true;
        FX.Sparkle(Goblet.Target, HordeGoblet.Tint, 20);
        AudioManager.PlayAt("boss_horn", Goblet.Target, .55f);
        RemoveGoblet();
        return true;
    }

    void RemoveGoblet()
    {
        if (Goblet == null) return;
        Goblet.gameObject.SetActive(false);
        Destroy(Goblet.gameObject);
        Goblet = null;
    }

    void ClearEvent()
    {
        Current=HordeEvent.None;Age=0;damageTick=0;announced=false;HasRefuge=false;fog.Clear();
        if(Totem!=null){Totem.gameObject.SetActive(false);Destroy(Totem.gameObject);Totem=null;}
        foreach(var go in visuals)if(go!=null){go.SetActive(false);Destroy(go);}
        visuals.Clear();
        if(fogCamera!=null){fogCamera.depthTextureMode=previousDepth;fogCamera=null;}
    }
    void OnDisable(){ClearWave();MoonBlend=0;}
}
