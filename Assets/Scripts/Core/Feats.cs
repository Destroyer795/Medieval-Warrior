using System.Linq;
using UnityEngine;

// Achievements that persist between sessions (PlayerPrefs). Gameplay code reports stats
// (Feats.Add) and one-off events (Feats.Unlock); the menu lists progress.
public static class Feats
{
    public class Def
    {
        public string id, name, description, icon, stat;
        public int goal;   // for stat-based feats; 0 = event-based
    }

    public static readonly Def[] All =
    {
        new Def { id = "first_blood", name = "First Blood", description = "Slay your first zombie.", icon = "skull", stat = "kills", goal = 1 },
        new Def { id = "centurion", name = "Centurion", description = "Slay 100 of the undead.", icon = "skull", stat = "kills", goal = 100 },
        new Def { id = "thousands", name = "Slayer of Thousands", description = "Slay 1,000 of the undead.", icon = "horde", stat = "kills", goal = 1000 },
        new Def { id = "eagle_eye", name = "Eagle Eye", description = "Land 25 arrow headshots.", icon = "bow", stat = "headshots", goal = 25 },
        new Def { id = "giant_felled", name = "Giant-Felled", description = "Bring down 10 Brutes.", icon = "swords", stat = "brutes", goal = 10 },
        new Def { id = "wall_of_iron", name = "Wall of Iron", description = "Make 20 perfect blocks.", icon = "shield", stat = "perfectblocks", goal = 20 },
        new Def { id = "kingslayer", name = "Kingslayer", description = "Defeat the Warlord.", icon = "crown" },
        new Def { id = "chain_reaction", name = "Chain Reaction", description = "Kill 3 zombies with a single Bloater blast.", icon = "skull" },
        new Def { id = "butcher", name = "Butcher", description = "Reach a 15 kill streak.", icon = "sword" },
        new Def { id = "defender", name = "Defender of the Realm", description = "Win Defend the Castle.", icon = "swords" },
        new Def { id = "dawn", name = "Dawn Breaks", description = "Survive the Last Stand.", icon = "hourglass" },
        new Def { id = "unbroken", name = "Unbroken", description = "Reach wave 10 of the Endless Horde.", icon = "horde" },
        new Def { id = "untouchable", name = "Untouchable", description = "Clear an Endless wave without taking damage.", icon = "shield" },
        new Def { id = "blessed", name = "Blessed", description = "Hold 6 boons in a single match.", icon = "potion" },
        new Def { id = "legend", name = "Legend", description = "Win any battle on Warlord difficulty.", icon = "crown" },
    };

    public static bool IsUnlocked(string id) => PlayerPrefs.GetInt("mw.feat." + id, 0) == 1;
    public static int Stat(string stat) => PlayerPrefs.GetInt("mw.stat." + stat, 0);
    public static int UnlockedCount => All.Count(f => IsUnlocked(f.id));

    public static void Add(string stat, int amount = 1)
    {
        if (GameSettings.TestMode) return;
        int value = Stat(stat) + amount;
        PlayerPrefs.SetInt("mw.stat." + stat, value);
        foreach (var f in All)
            if (f.stat == stat && value >= f.goal) Unlock(f.id);
    }

    public static void Unlock(string id)
    {
        if (GameSettings.TestMode || IsUnlocked(id)) return;
        var f = All.FirstOrDefault(d => d.id == id);
        if (f == null) return;
        PlayerPrefs.SetInt("mw.feat." + id, 1);
        PlayerPrefs.Save();
        GameHUD.Toast("Feat unlocked: " + f.name, UIKit.Gold, "crown", 3.5f, 34);
        AudioManager.Play2D("pickup", 1f, 0.8f);
    }

    public static string Progress(Def f)
    {
        if (IsUnlocked(f.id)) return "Unlocked";
        return f.goal > 0 ? $"{Mathf.Min(Stat(f.stat), f.goal):N0} / {f.goal:N0}" : "Locked";
    }
}
