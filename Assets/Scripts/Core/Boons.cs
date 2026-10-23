using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum BoonId { Vigour, SwiftDraw, KeenEdge, SecondWind, Piercing, FireArrows, Quiver, Bulwark, Vampiric, FleetFoot, Bloodlust }

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
    };

    static readonly Dictionary<BoonId, int> stacks = new Dictionary<BoonId, int>();

    public static int Stacks(BoonId id) => stacks.TryGetValue(id, out int n) ? n : 0;
    public static int Total => stacks.Values.Sum();
    public static IEnumerable<(Def def, int stacks)> Owned => all.Where(d => Stacks(d.id) > 0).Select(d => (d, Stacks(d.id)));
    public static Def Get(BoonId id) => all.First(d => d.id == id);

    public static void Reset() => stacks.Clear();

    public static Def[] Roll(int count)
    {
        return all.Where(d => Stacks(d.id) < d.maxStacks)
                  .OrderBy(_ => Random.value)
                  .Take(count)
                  .ToArray();
    }

    public static void Apply(BoonId id, GameObject player)
    {
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
}
