using UnityEngine;

public enum ZombieType { Walker, Runner, Brute, Spitter, Bloater, Warlord }

public class ZombieStats
{
    public string name;
    public string description;
    public float health;
    public float speed;              // m/s when chasing
    public float damage;             // per hit, before difficulty
    public float attackRange;
    public float windup;             // seconds from swing start to impact
    public float cooldown;
    public float scale;
    public Color tint;
    public Color glow;               // emissive eye/skin tint
    public int points;
    public float knockbackResist;    // 0 = flies back, 1 = immovable
    public float animSpeed = 1f;
}

public static class ZombieTypes
{
    static readonly ZombieStats[] table =
    {
        new ZombieStats { name = "Walker", description = "The shambling dead. Slow, but never alone.", health = 3, speed = 2.6f, damage = 10, attackRange = 1.5f, windup = 0.55f, cooldown = 1.4f, scale = 1f, tint = Color.white, glow = Color.black, points = 100, knockbackResist = 0f },
        new ZombieStats { name = "Runner", description = "Fresh and furious. Sprints in and lunges from range.", health = 2, speed = 5.6f, damage = 7, attackRange = 1.4f, windup = 0.35f, cooldown = 1.0f, scale = 0.93f, tint = new Color(1f, 0.78f, 0.72f), glow = new Color(0.6f, 0.05f, 0f), points = 150, knockbackResist = 0f, animSpeed = 1.25f },
        new ZombieStats { name = "Brute", description = "A walking siege engine. Its slam throws knights across the yard.", health = 14, speed = 2.1f, damage = 26, attackRange = 2.4f, windup = 0.85f, cooldown = 2.2f, scale = 1.45f, tint = new Color(0.7f, 0.66f, 0.72f), glow = new Color(0.25f, 0f, 0.35f), points = 500, knockbackResist = 0.75f, animSpeed = 0.8f },
        new ZombieStats { name = "Spitter", description = "Keeps its distance and hurls burning bile. Close in fast.", health = 3, speed = 3.0f, damage = 9, attackRange = 13f, windup = 0.6f, cooldown = 3.2f, scale = 0.98f, tint = new Color(0.72f, 1f, 0.55f), glow = new Color(0.15f, 0.45f, 0f), points = 200, knockbackResist = 0f },
        new ZombieStats { name = "Bloater", description = "Swollen with rot. Bursts when close - or when slain. Use it.", health = 4, speed = 2.2f, damage = 34, attackRange = 2.4f, windup = 1.1f, cooldown = 99f, scale = 1.18f, tint = new Color(0.85f, 0.95f, 0.45f), glow = new Color(0.35f, 0.4f, 0f), points = 250, knockbackResist = 0.3f, animSpeed = 0.85f },
        new ZombieStats { name = "Warlord", description = "The crowned dead. Commands the horde and enrages when wounded.", health = 45, speed = 2.5f, damage = 32, attackRange = 2.8f, windup = 0.8f, cooldown = 1.8f, scale = 1.85f, tint = new Color(0.55f, 0.35f, 0.35f), glow = new Color(0.7f, 0.08f, 0f), points = 2500, knockbackResist = 0.92f, animSpeed = 0.8f },
    };

    public static ZombieStats Get(ZombieType t) => table[(int)t];
    public static int Count => table.Length;
}
