using UnityEngine;

public enum GameMode { DefendTheCastle, EndlessHorde, LastStand }
public enum Difficulty { Squire, Knight, Warlord }

// Everything the player can tweak, persisted in PlayerPrefs.
public static class GameSettings
{
    // ---------------- audio / controls / display
    public static float MasterVolume { get => PlayerPrefs.GetFloat("mw.master", 0.85f); set { PlayerPrefs.SetFloat("mw.master", value); AudioListener.volume = value; } }
    public static float SfxVolume { get => PlayerPrefs.GetFloat("mw.sfx", 0.9f); set => PlayerPrefs.SetFloat("mw.sfx", value); }
    public static float MusicVolume { get => PlayerPrefs.GetFloat("mw.music", 0.55f); set { PlayerPrefs.SetFloat("mw.music", value); AudioManager.RefreshMusicVolume(); } }
    public static float MouseSensitivity { get => PlayerPrefs.GetFloat("mw.sens", 3.5f); set => PlayerPrefs.SetFloat("mw.sens", value); }
    public static bool InvertY { get => PlayerPrefs.GetInt("mw.invertY", 0) == 1; set => PlayerPrefs.SetInt("mw.invertY", value ? 1 : 0); }
    public static float FieldOfView { get => PlayerPrefs.GetFloat("mw.fov", 62f); set => PlayerPrefs.SetFloat("mw.fov", value); }
    public static bool CameraShake { get => PlayerPrefs.GetInt("mw.shake", 1) == 1; set => PlayerPrefs.SetInt("mw.shake", value ? 1 : 0); }
    public static bool ShowFps { get => PlayerPrefs.GetInt("mw.fps", 0) == 1; set => PlayerPrefs.SetInt("mw.fps", value ? 1 : 0); }
    public static bool ShowDamageNumbers { get => PlayerPrefs.GetInt("mw.dmgnum", 1) == 1; set => PlayerPrefs.SetInt("mw.dmgnum", value ? 1 : 0); }
    public static int Quality { get => PlayerPrefs.GetInt("mw.quality", QualitySettings.names.Length - 1); set { PlayerPrefs.SetInt("mw.quality", value); QualitySettings.SetQualityLevel(Mathf.Clamp(value, 0, QualitySettings.names.Length - 1), true); } }
    public static bool Fullscreen { get => PlayerPrefs.GetInt("mw.fullscreen", 1) == 1; set { PlayerPrefs.SetInt("mw.fullscreen", value ? 1 : 0); Screen.fullScreen = value; } }

    // ---------------- match setup
    public static GameMode Mode { get => (GameMode)PlayerPrefs.GetInt("mw.mode", 0); set => PlayerPrefs.SetInt("mw.mode", (int)value); }
    public static Difficulty Difficulty { get => (Difficulty)PlayerPrefs.GetInt("mw.difficulty", 1); set => PlayerPrefs.SetInt("mw.difficulty", (int)value); }
    public static string HeroName { get => PlayerPrefs.GetString("mw.hero", "Sir Galahad"); set => PlayerPrefs.SetString("mw.hero", string.IsNullOrWhiteSpace(value) ? "Nameless Knight" : value.Trim()); }

    // ---------------- knight customisation (indices into the palettes below)
    public static int Tabard { get => PlayerPrefs.GetInt("mw.tabard", 1); set => PlayerPrefs.SetInt("mw.tabard", value); }
    public static int Metal { get => PlayerPrefs.GetInt("mw.metal", 0); set => PlayerPrefs.SetInt("mw.metal", value); }
    public static int Leather { get => PlayerPrefs.GetInt("mw.leather", 0); set => PlayerPrefs.SetInt("mw.leather", value); }
    public static int BowFinish { get => PlayerPrefs.GetInt("mw.bow", 0); set => PlayerPrefs.SetInt("mw.bow", value); }
    public static int Crest { get => PlayerPrefs.GetInt("mw.crest", 0); set => PlayerPrefs.SetInt("mw.crest", value); }

    public static readonly (string name, Color color)[] TabardColors =
    {
        ("Weathered Tan", new Color(0.72f, 0.48f, 0.24f)),
        ("Royal Blue", new Color(0.12f, 0.27f, 0.72f)),
        ("Crimson", new Color(0.72f, 0.08f, 0.1f)),
        ("Forest Green", new Color(0.12f, 0.48f, 0.2f)),
        ("Royal Purple", new Color(0.42f, 0.16f, 0.6f)),
        ("Sun Gold", new Color(0.9f, 0.66f, 0.12f)),
        ("Raven Black", new Color(0.12f, 0.12f, 0.14f)),
        ("Ivory", new Color(0.9f, 0.86f, 0.76f)),
    };
    public static readonly (string name, Color tint)[] MetalTints =
    {
        ("Polished Steel", new Color(1f, 1f, 1f)),
        ("Royal Gold", new Color(1.25f, 0.95f, 0.45f)),
        ("Bronze", new Color(1.1f, 0.72f, 0.42f)),
        ("Blackened Iron", new Color(0.42f, 0.42f, 0.46f)),
        ("Mythril", new Color(0.78f, 0.95f, 1.2f)),
        ("Rose Gold", new Color(1.2f, 0.8f, 0.72f)),
    };
    public static readonly (string name, Color tint)[] LeatherTints =
    {
        ("Saddle Brown", new Color(1f, 1f, 1f)),
        ("Charred", new Color(0.5f, 0.45f, 0.42f)),
        ("Oxblood", new Color(1.15f, 0.55f, 0.5f)),
        ("Sand", new Color(1.5f, 1.35f, 1.1f)),
    };
    public static readonly (string name, Color wood, Color trim)[] BowFinishes =
    {
        ("Yew", new Color(0.55f, 0.33f, 0.16f), new Color(0.85f, 0.65f, 0.3f)),
        ("Ebony", new Color(0.12f, 0.1f, 0.1f), new Color(0.85f, 0.8f, 0.75f)),
        ("Elven Ash", new Color(0.92f, 0.88f, 0.78f), new Color(0.95f, 0.75f, 0.25f)),
        ("Dragonbone", new Color(0.6f, 0.12f, 0.1f), new Color(0.2f, 0.2f, 0.2f)),
    };
    public static readonly string[] Crests = { "Cross", "Plain", "Chevron", "Saltire" };

    // ---------------- difficulty tuning
    public struct DifficultyProfile
    {
        public float enemyHealth, enemyDamage, enemySpeed, spawnRate, aggression;
        public int defendTarget, maxAlive;
        public float lastStandMinutes;
    }

    public static DifficultyProfile Profile => Profiles[(int)Difficulty];

    static readonly DifficultyProfile[] Profiles =
    {
        new DifficultyProfile { enemyHealth = 0.8f, enemyDamage = 0.6f, enemySpeed = 0.9f, spawnRate = 0.75f, aggression = 0.6f, defendTarget = 20, maxAlive = 7, lastStandMinutes = 3f },
        new DifficultyProfile { enemyHealth = 1f, enemyDamage = 1f, enemySpeed = 1f, spawnRate = 1f, aggression = 0.8f, defendTarget = 30, maxAlive = 10, lastStandMinutes = 4f },
        new DifficultyProfile { enemyHealth = 1.35f, enemyDamage = 1.4f, enemySpeed = 1.12f, spawnRate = 1.35f, aggression = 1f, defendTarget = 45, maxAlive = 14, lastStandMinutes = 5f },
    };

    public static string ModeName(GameMode m) => m switch
    {
        GameMode.DefendTheCastle => "Defend the Castle",
        GameMode.EndlessHorde => "Endless Horde",
        _ => "Last Stand",
    };

    public static string ModeBlurb(GameMode m) => m switch
    {
        GameMode.DefendTheCastle => "Cut down the undead tide before it overruns the keep. A Warlord leads the final assault.",
        GameMode.EndlessHorde => "Wave after wave, each deadlier than the last. Every fifth wave brings a Brute. How long will your legend last?",
        _ => "The gates have fallen. Hold your ground until the reinforcements arrive at dawn.",
    };

    // ---------------- records
    public static int HighScore(GameMode m) => PlayerPrefs.GetInt("mw.best." + m, 0);
    public static int BestWave => PlayerPrefs.GetInt("mw.bestwave", 0);

    public static bool TestMode;   // automated tests never write records

    public static bool SubmitScore(GameMode m, int score, int wave)
    {
        if (TestMode) return false;
        if (wave > BestWave) PlayerPrefs.SetInt("mw.bestwave", wave);
        bool record = score > HighScore(m);
        if (record) PlayerPrefs.SetInt("mw.best." + m, score);
        PlayerPrefs.Save();
        return record;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnBoot()
    {
        AudioListener.volume = MasterVolume;
        if (PlayerPrefs.HasKey("mw.quality")) QualitySettings.SetQualityLevel(Mathf.Clamp(Quality, 0, QualitySettings.names.Length - 1), true);
    }

    public static void Save() => PlayerPrefs.Save();
}
