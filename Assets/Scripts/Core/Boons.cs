using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum BoonId { Vigour, SwiftDraw, KeenEdge, SecondWind, Piercing, FireArrows, Quiver, Bulwark, Vampiric, FleetFoot, Bloodlust,
    ThunderGuard, ReapersCleave, SplinterVolley, VampiricRiposte,
    ShockwaveParry, WhirlwindStrike, ExplosiveQuiver }

// Upgrades the knight earns during a match (pick one of three). They reset every match.
// Gameplay code reads the multipliers below; one-off effects happen in Apply.
public static class Boons
{
    public class Def
    {
        public BoonId id;
        public string name, description, icon;
        public int maxStacks;
    }

    static readonly Def[] all =
    {
        new Def { id = BoonId.Vigour, name = "Vigour", description = "+20 maximum health and heal 20.", icon = "heart", maxStacks = 5 },
        new Def { id = BoonId.SwiftDraw, name = "Swift Draw", description = "Draw your bow 25% faster.", icon = "bow", maxStacks = 3 },
        new Def { id = BoonId.KeenEdge, name = "Keen Edge", description = "Sword blows deal 30% more damage.", icon = "sword", maxStacks = 4 },
        new Def { id = BoonId.SecondWind, name = "Second Wind", description = "+20 stamina and 40% faster stamina recovery.", icon = "stamina", maxStacks = 3 },
        new Def { id = BoonId.Piercing, name = "Piercing Arrows", description = "Arrows pass through one more zombie.", icon = "arrow", maxStacks = 3 },
        new Def { id = BoonId.FireArrows, name = "Fire Arrows", description = "Fully drawn arrows set the undead ablaze.", icon = "arrow", maxStacks = 1 },
        new Def { id = BoonId.Quiver, name = "Great Quiver", description = "+15 arrow capacity, and the quiver is refilled.", icon = "arrow", maxStacks = 3 },
        new Def { id = BoonId.Bulwark, name = "Bulwark", description = "Blocking costs no stamina and perfect blocks are easier.", icon = "shield", maxStacks = 2 },
        new Def { id = BoonId.Vampiric, name = "Vampiric Blade", description = "Each sword kill restores 5 health.", icon = "potion", maxStacks = 3 },
        new Def { id = BoonId.FleetFoot, name = "Fleet Foot", description = "Move 12% faster.", icon = "stamina", maxStacks = 3 },
        new Def { id = BoonId.Bloodlust, name = "Bloodlust", description = "Kill streaks last 2 seconds longer.", icon = "skull", maxStacks = 3 },
        new Def { id = BoonId.ThunderGuard, name = "Thunder Guard", description = "Perfect blocks and parries stun nearby undead with a thunder shockwave.", icon = "shield", maxStacks = 1 },
        new Def { id = BoonId.ReapersCleave, name = "Reaper's Cleave", description = "The third sword strike sweeps a wider arc and burns with phantom flame.", icon = "sword", maxStacks = 1 },
        new Def { id = BoonId.SplinterVolley, name = "Splinter Volley", description = "Arrows burst into shrapnel, damaging nearby undead. Works with piercing and fire.", icon = "arrow", maxStacks = 1 },
        new Def { id = BoonId.VampiricRiposte, name = "Vampiric Riposte", description = "A successful parry or perfect block restores 6 health.", icon = "potion", maxStacks = 1 },
        new Def { id = BoonId.ShockwaveParry, name = "Shockwave Parry", description = "Perfect blocks chain lightning between undead, dealing damage and stunning them. Ranks add power and chain targets.", icon = "shield", maxStacks = int.MaxValue },
        new Def { id = BoonId.WhirlwindStrike, name = "Whirlwind Strike", description = "Your third sword cut draws nearby undead inward and makes them bleed. Ranks increase the pull and bleeding damage.", icon = "sword", maxStacks = int.MaxValue },
        new Def { id = BoonId.ExplosiveQuiver, name = "Explosive Quiver", description = "Headshots scatter delayed shrapnel bombs. Ranks increase blast damage. Combines with fire and piercing arrows.", icon = "arrow", maxStacks = int.MaxValue },
    };

    static readonly Dictionary<BoonId, int> stacks = new Dictionary<BoonId, int>();

    public static int Stacks(BoonId id) => stacks.TryGetValue(id, out int n) ? n : 0;
    public static int Total => stacks.Values.Sum();
    public static IEnumerable<(Def def, int stacks)> Owned => all.Where(d => Stacks(d.id) > 0).Select(d => (d, Stacks(d.id)));
    public static Def Get(BoonId id) => all.First(d => d.id == id);

    public static void Reset() => stacks.Clear();

    public static Def[] Roll(int count, bool synergistic = false)
    {
        var pool = all.Where(d => Stacks(d.id) < d.maxStacks).ToList();
        var result = new List<Def>();
        while(result.Count<count && pool.Count>0)
        {
            // The first card is always a transformative power in Horde. The
            // remaining cards favour existing builds without locking out pivots.
            var eligible=synergistic&&result.Count==0 ? pool.Where(d=>IsSignature(d.id)).ToList() : pool;
            if(eligible.Count==0)eligible=pool;
            float total=eligible.Sum(d=>synergistic?DraftWeight(d.id):1f), roll=Random.value*total;
            var chosen=eligible[eligible.Count-1];
            foreach(var d in eligible){roll-=synergistic?DraftWeight(d.id):1f;if(roll<=0){chosen=d;break;}}
            result.Add(chosen);pool.Remove(chosen);
        }
        return result.ToArray();
    }

    public static bool IsSignature(BoonId id)=>id==BoonId.ShockwaveParry||id==BoonId.WhirlwindStrike||id==BoonId.ExplosiveQuiver;
    static float DraftWeight(BoonId id)=>1f+(Stacks(id)>0?1.3f:0f)+(HasSynergy(id)?2f:0f);
    public static bool HasSynergy(BoonId id)
    {
        bool guard=Stacks(BoonId.ShockwaveParry)+Stacks(BoonId.ThunderGuard)+Stacks(BoonId.VampiricRiposte)+Stacks(BoonId.Bulwark)>0;
        bool blade=Stacks(BoonId.WhirlwindStrike)+Stacks(BoonId.ReapersCleave)+Stacks(BoonId.KeenEdge)+Stacks(BoonId.Vampiric)>0;
        bool bow=Stacks(BoonId.ExplosiveQuiver)+Stacks(BoonId.SplinterVolley)+Stacks(BoonId.FireArrows)+Stacks(BoonId.Piercing)>0;
        switch(id)
        {
            case BoonId.ShockwaveParry:case BoonId.ThunderGuard:case BoonId.VampiricRiposte:case BoonId.Bulwark:return guard;
            case BoonId.WhirlwindStrike:case BoonId.ReapersCleave:case BoonId.KeenEdge:case BoonId.Vampiric:return blade;
            case BoonId.ExplosiveQuiver:case BoonId.SplinterVolley:case BoonId.FireArrows:case BoonId.Piercing:case BoonId.SwiftDraw:case BoonId.Quiver:return bow;
            default:return false;
        }
    }

    public static void Apply(BoonId id, GameObject player)
    {
        if(Stacks(id)>=Get(id).maxStacks)return;
        stacks[id] = Stacks(id) + 1;
        if (Total >= 6) Feats.Unlock("blessed");
        if (player == null) return;
        var health = player.GetComponent<PlayerHealth>();
        var control = player.GetComponent<PlayerControlSpeed>();
        switch (id)
        {
            case BoonId.Vigour:
                if (health != null) { health.maxHealth += 20; health.Heal(20); }
                break;
            case BoonId.Quiver:
                if (control != null) { control.maxArrows += 15; control.AddArrows(control.maxArrows); }
                break;
            case BoonId.SecondWind:
                if (control != null) control.maxStamina += 20;
                break;
        }
    }

    // ---- multipliers read by gameplay code
    public static float SwordDamage => 1f + 0.3f * Stacks(BoonId.KeenEdge);
    public static float DrawSpeed => Mathf.Pow(1f / 0.75f, Stacks(BoonId.SwiftDraw));
    public static float StaminaRegen => 1f + 0.4f * Stacks(BoonId.SecondWind);
    public static int Pierce => Stacks(BoonId.Piercing);
    public static bool FireArrows => Stacks(BoonId.FireArrows) > 0;
    public static bool FreeBlocking => Stacks(BoonId.Bulwark) > 0;
    public static float PerfectBlockBonus => 0.12f * Stacks(BoonId.Bulwark);
    public static float LifeSteal => 5f * Stacks(BoonId.Vampiric);
    public static float MoveSpeed => 1f + 0.12f * Stacks(BoonId.FleetFoot);
    public static float ComboWindow => 4f + 2f * Stacks(BoonId.Bloodlust);
    public static bool ThunderGuard => Stacks(BoonId.ThunderGuard)>0;
    public static bool ReapersCleave => Stacks(BoonId.ReapersCleave)>0;
    public static bool SplinterVolley => Stacks(BoonId.SplinterVolley)>0;
    public static float RiposteHeal => Stacks(BoonId.VampiricRiposte)>0 ? 6f : 0f;
}
