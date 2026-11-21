using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Runs a match: picks the mode from GameSettings, paces the horde, keeps score and stats,
// and handles pause / victory / defeat. The HUD (GameHUD) listens to the events here.
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    const string WorldSceneName = "Background";

    [Header("Level Goals")]
    public int targetKills = 10;          // overwritten by difficulty in Defend the Castle
    public int currentKills = 0;

    [Header("Legacy UI (replaced by GameHUD, hidden at start)")]
    public GameObject gameplayUI;
    public GameObject victoryUI;
    public GameObject gameOverUI;
    public GameObject pauseMenuUI;
    public TextMeshProUGUI scoreText;

    public enum MatchState { Intro, Playing, Intermission, Won, Lost, Dawn }

    // ---------------- readable state
    public GameMode Mode { get; private set; }
    public MatchState State { get; private set; } = MatchState.Intro;
    public bool IsPaused { get; private set; }
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public float ComboTimeLeft => Mathf.Max(0f, comboExpires - Time.time);
    public int ComboMultiplier => 1 + Mathf.Min(Combo, 20) / 5;
    public int Wave { get; private set; }
    public int WaveRemaining => Mathf.Max(0, waveToSpawn - waveSpawned) + EnemyController.All.Count;
    public float TimeLeft { get; private set; }
    public float IntermissionLeft { get; private set; }
    public float MatchTime { get; private set; }
    public EnemyController Boss { get; private set; }
    public HordeDirector Horde { get; private set; }
    public LastStandDirector LastStand { get; private set; }
    public bool NewRecord { get; private set; }

    public readonly Dictionary<ZombieType, int> killsByType = new Dictionary<ZombieType, int>();
    public int ArrowsFired { get; private set; }
    public int ArrowsHit { get; private set; }
    public int Headshots { get; private set; }
    public int BestCombo { get; private set; }
    public float DamageTaken { get; private set; }

    public event Action<EnemyController, int> Killed;       // enemy, points awarded
    public event Action<bool> MatchEnded;                   // true = victory
    public event Action<bool> PauseChanged;

    bool isPaused;
    bool gameEnded;
    float comboExpires, spawnTimer, introTimer;
    int waveToSpawn, waveSpawned;
    bool warlordSpawned;
    float nextBruteTime;
    PlayerHealth playerHealth;
    public float AtmosphereLevel {get;private set;}
    bool atmosphereCaptured,baseFog;
    int atmosphereScene;
    float baseFogDensity,baseFogEnd,baseAmbientIntensity,baseSunIntensity,lastThreatCue=-99;
    Color baseFogColor,baseAmbientSky,baseAmbientEquator,baseAmbientGround,baseSunColor;
    Light battleSun;

    // =======================
    // UNITY
    // =======================

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        CombatTime.SetScale(1f);

        // The castle, terrain and props live in the Background scene.
        // Load it here so the level also works when played straight from the editor.
        if (!SceneManager.GetSceneByName(WorldSceneName).isLoaded)
        {
            using var timing=BattleLoadMetrics.Measure("World synchronous reload");
            SceneManager.LoadScene(WorldSceneName, LoadSceneMode.Additive);
        }
    }

    void OnDestroy()
    {
        if (instance == this){if(Horde!=null)Horde.ClearWave();RestoreAtmosphere();CombatTime.SetScale(1);instance = null;}
    }

    void Start()
    {
        StartGameplay();
    }

    void Update()
    {
        if (State == MatchState.Dawn) return;
        if (GameInput.PauseDown)
        {
            if (gameEnded || ChoosingBoon) return;
            if (isPaused) Resume();
            else Pause();
        }
        if (gameEnded || isPaused) return;
        if (LastStand != null && playerHealth != null && playerHealth.IsDead) return;
        if(State==MatchState.Intro&&!BattleLoading.Ready)return;

        if (LastStand == null) MatchTime += Time.deltaTime;
        if (Combo > 0 && Time.time > comboExpires) Combo = 0;
        UpdateBattleMusic();
        if (LastStand == null) UpdateBattleAtmosphere();

        switch (State)
        {
            case MatchState.Intro:
                introTimer -= Time.deltaTime;
                if (introTimer <= 0f) BeginPlay();
                break;
            case MatchState.Playing:
                UpdateMode();
                if (State == MatchState.Playing)
                {
                    UpdateTips();
                    CheckBoonMilestones();
                }
                break;
            case MatchState.Intermission:
                IntermissionLeft -= Time.deltaTime;
                if (IntermissionLeft <= 0f) StartWave(Wave + 1);
                break;
        }
    }

    // =======================
    // GAME FLOW
    // =======================

    void StartGameplay()
    {
        using var timing=BattleLoadMetrics.Measure("GameManager.StartGameplay");
        Mode = GameSettings.Mode;
        currentKills = 0;
        Boons.Reset();
        isPaused = false;
        gameEnded = false;
        foreach (ZombieType t in Enum.GetValues(typeof(ZombieType))) killsByType[t] = 0;

        foreach (var legacy in new[] { gameplayUI, victoryUI, gameOverUI, pauseMenuUI })
            if (legacy) legacy.SetActive(false);

        var profile = GameSettings.Profile;
        targetKills = profile.defendTarget;
        if (Mode == GameMode.LastStand) TimeLeft = LastStandDirector.Duration;

        // Reset player health safely
        var playerObj = GameObject.FindWithTag("Player");
        playerHealth = playerObj != null ? playerObj.GetComponent<PlayerHealth>() : null;
        if (playerHealth != null) playerHealth.ResetHealth();
        Horde=gameObject.AddComponent<HordeDirector>();Horde.Prepare(playerHealth);

        if (FindFirstObjectByType<GameHUD>() == null) new GameObject("Game HUD").AddComponent<GameHUD>();
        BattleLoading.Prepare();
        if (Mode == GameMode.LastStand)
        {
            LastStand = gameObject.AddComponent<LastStandDirector>();
            LastStand.Prepare(playerHealth);
        }
        else
        {
            AudioManager.PlayMusic("ambient_meadow", 0.9f);
            CaptureAtmosphere();
        }

        LockCursor(true);
        CombatTime.SetScale(1f);
        State = MatchState.Intro;
        introTimer = 3.5f;
        GameHUD.Banner(GameSettings.ModeName(Mode), IntroLine(), 3.2f);
        AudioManager.Play2D("wave_horn", 0.8f);
    }

    string IntroLine() => Mode switch
    {
        GameMode.DefendTheCastle => $"Slay {targetKills} of the undead",
        GameMode.EndlessHorde => "Survive as many waves as you can",
        _ => "The gates have fallen. Fall back to the courtyard as corruption closes in.",
    };

    void BeginPlay()
    {
        if (Mode == GameMode.EndlessHorde) StartWave(1);
        else
        {
            State = MatchState.Playing;
            for (int i = 0; i < 3; i++) Spawn(ZombieType.Walker);   // the first few are already shambling in
        }
        spawnTimer = 1f;
        nextBruteTime = 70f;
    }

    // ---------------- spawning per mode

    void UpdateMode()
    {
        var p = GameSettings.Profile;
        switch (Mode)
        {
            case GameMode.DefendTheCastle:
            {
                float progress = Mathf.Clamp01(currentKills / (float)targetKills);
                int cap = Mathf.RoundToInt(Mathf.Lerp(4, p.maxAlive, progress));
                if (!warlordSpawned && progress >= 0.75f)
                {
                    warlordSpawned = true;
                    SpawnBoss(ZombieType.Warlord);
                }
                TickSpawner(cap, 2.4f / p.spawnRate, progress);
                if (currentKills >= targetKills && (Boss == null || Boss.IsDead)) WinGame();
                break;
            }
            case GameMode.EndlessHorde:
            {
                int cap = Mathf.Min(4 + Wave, p.maxAlive + 4);
                if (waveSpawned < waveToSpawn) TickSpawner(cap, Mathf.Max(0.5f, 1.8f / p.spawnRate - Wave * 0.05f), Mathf.Clamp01(Wave / 12f), true);
                else if (EnemyController.All.Count == 0) WaveCleared();
                break;
            }
            case GameMode.LastStand:
                UpdateLastStand(p);
                break;
        }
    }

    void UpdateLastStand(GameSettings.DifficultyProfile profile)
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        float previous = TimeLeft;
        TimeLeft = Mathf.Max(0f, TimeLeft - dt);
        MatchTime = LastStandDirector.Duration - TimeLeft;
        if (TimeLeft == 0f)
        {
            State = MatchState.Dawn;
            StartCoroutine(HoldUntilDawn());
            return;
        }

        LastStand.Tick(TimeLeft, dt);
        if (playerHealth != null && playerHealth.IsDead) return;
        bool surge = LastStand.IsSurging;
        if (surge && previous > LastStandDirector.SurgeSeconds) spawnTimer = 0f;
        float progress = MatchTime / LastStandDirector.Duration;
        int cap = surge ? profile.maxAlive + 7 : Mathf.RoundToInt(Mathf.Lerp(5, profile.maxAlive + 4, progress));
        float interval = (surge ? 0.43f : Mathf.Lerp(2.2f, 0.9f, progress)) / profile.spawnRate;

        if (!warlordSpawned && progress >= 0.6f)
        {
            warlordSpawned = true;
            SpawnBoss(ZombieType.Warlord);
        }
        if (MatchTime >= nextBruteTime && EnemyController.All.Count < cap)
        {
            nextBruteTime = MatchTime + Mathf.Lerp(70f, 26f, progress);
            Spawn(ZombieType.Brute);
        }
        TickSpawner(cap, interval, progress, surge: surge);
    }

    IEnumerator HoldUntilDawn()
    {
        yield return LastStand.PlayDawn();
        WinGame();
    }

    void TickSpawner(int cap, float interval, float progress, bool waveCounted = false, bool surge = false)
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f || EnemyController.All.Count >= cap) return;
        spawnTimer = interval * UnityEngine.Random.Range(0.7f, 1.3f);
        Spawn(PickType(progress, surge));
        if (waveCounted) waveSpawned++;
    }

    ZombieType PickType(float progress, bool surge = false)
    {
        // weighted mix that unlocks nastier undead as the match goes on
        float runner = progress > 0.08f ? 0.22f : 0f;
        float spitter = progress > 0.2f ? 0.14f : 0f;
        float bloater = progress > 0.32f ? 0.12f : 0f;
        float brute = progress > 0.5f ? 0.05f : 0f;
        float swordsman = progress > .14f ? .20f : 0f;
        float bulwark = progress > .35f ? .12f : 0f;
        float walker = 1f;
        if (surge)
        {
            runner *= 2.7f;
            swordsman *= 2f;
            bulwark *= 1.6f;
            bloater *= 1.5f;
        }
        float roll = UnityEngine.Random.value * (walker + runner + spitter + bloater + brute + swordsman + bulwark);
        if ((roll -= walker) < 0) return ZombieType.Walker;
        if ((roll -= runner) < 0) return ZombieType.Runner;
        if ((roll -= spitter) < 0) return ZombieType.Spitter;
        if ((roll -= bloater) < 0) return ZombieType.Bloater;
        if ((roll -= swordsman) < 0) return ZombieType.Swordsman;
        if ((roll -= bulwark) < 0) return ZombieType.Bulwark;
        return ZombieType.Brute;
    }

    readonly HashSet<ZombieType> introduced = new HashSet<ZombieType>();

    EnemyController Spawn(ZombieType type)
    {
        var spawner = ZombieSpawner.Instance;
        var e = spawner != null ? spawner.Spawn(type, false) : null;
        if (e != null && type != ZombieType.Walker && introduced.Add(type))
        {
            string introduction = $"{e.Stats.name}: {e.Stats.description}";
            Color color = new Color(1f, 0.75f, 0.45f);
            if (LastStand != null) GameHUD.Toast(introduction, color, "skull", 4f, 25f, wrap: true);
            else GameHUD.Announce(introduction, color);
            if(type==ZombieType.Brute&&Time.time-lastThreatCue>8)ThreatStinger(type);
        }
        return e;
    }

    // ---------------- boons: pick one of three at milestones
    public bool ChoosingBoon { get; private set; }
    public int BoonsOffered { get; private set; }
    float nextBoonTime = 60f;
    int defendBoonsGiven;
    Boons.Def[] offeredBoons;

    void CheckBoonMilestones()
    {
        if (ChoosingBoon || gameEnded || (playerHealth != null && playerHealth.IsDead)) return;
        if (Mode == GameMode.DefendTheCastle)
        {
            int due = currentKills >= targetKills * 2 / 3 ? 2 : currentKills >= targetKills / 3 ? 1 : 0;
            if (defendBoonsGiven < due) { defendBoonsGiven++; OfferBoon(); }
        }
        else if (Mode == GameMode.LastStand && MatchTime >= nextBoonTime)
        {
            nextBoonTime += 60f;
            OfferBoon();
        }
    }

    public void OfferBoon()
    {
        if(ChoosingBoon||gameEnded||State==MatchState.Dawn)return;
        var choices = Boons.Roll(3,Mode==GameMode.EndlessHorde);
        if (choices.Length == 0) return;
        offeredBoons=choices;
        ChoosingBoon = true;
        BoonsOffered++;
        CombatTime.SetScale(0f);
        LockCursor(false);
        AudioManager.Play2D("heal", 0.8f);
        GameHUD.ShowBoons(choices, PickBoon);
    }

    public void PickBoon(BoonId id)
    {
        if (!ChoosingBoon||offeredBoons==null||!Array.Exists(offeredBoons,d=>d.id==id)) return;
        Boons.Apply(id, playerHealth != null ? playerHealth.gameObject : GameObject.FindWithTag("Player"));
        offeredBoons=null;
        ChoosingBoon = false;
        if (!isPaused && !gameEnded) { CombatTime.SetScale(1f); LockCursor(true); }
        GameHUD.Toast(Boons.Get(id).name + " gained", UIKit.Gold, Boons.Get(id).icon, 2.5f, 32);
        AudioManager.Play2D("pickup", 1f);
    }

    void UpdateBattleMusic()
    {
        if (LastStand != null && LastStand.IsSurging) return;
        if (playerHealth == null) return;
        int near = 0;
        Vector3 p = playerHealth.transform.position;
        foreach (var e in EnemyController.All)
            if (e != null && (e.transform.position - p).sqrMagnitude < 22f * 22f) near++;
        float level = Mathf.Clamp01(near / 4f);
        if (Boss != null && !Boss.IsDead) level = 1f;
        AudioManager.SetBattleIntensity(level);
    }

    void CaptureAtmosphere()
    {
        atmosphereCaptured=true;atmosphereScene=SceneManager.GetActiveScene().handle;
        baseFog=RenderSettings.fog;baseFogDensity=RenderSettings.fogDensity;baseFogColor=RenderSettings.fogColor;baseFogEnd=RenderSettings.fogEndDistance;
        baseAmbientIntensity=RenderSettings.ambientIntensity;baseAmbientSky=RenderSettings.ambientSkyColor;
        baseAmbientEquator=RenderSettings.ambientEquatorColor;baseAmbientGround=RenderSettings.ambientGroundColor;
        battleSun=RenderSettings.sun;
        if(battleSun!=null){baseSunColor=battleSun.color;baseSunIntensity=battleSun.intensity;}
    }
    void UpdateBattleAtmosphere()
    {
        if(!atmosphereCaptured)return;
        float pressure=Mode==GameMode.EndlessHorde?Mathf.Clamp01((Wave-1)/14f):
            Mathf.Clamp01(currentKills/(float)Mathf.Max(1,targetKills));
        if(Boss!=null&&!Boss.IsDead)pressure=Mathf.Max(pressure,.65f);
        AtmosphereLevel=Mathf.MoveTowards(AtmosphereLevel,pressure,Time.deltaTime*.1f);
        float level=AtmosphereLevel;
        float moon=Horde!=null?Horde.MoonBlend:0;
        // Keep silhouettes readable with bounded darkening and cool fog/red light.
        RenderSettings.fog=baseFog||level>.15f;
        RenderSettings.fogDensity=Mathf.Lerp(baseFogDensity,Mathf.Clamp(baseFogDensity*1.8f,.0035f,.012f),level);
        RenderSettings.fogEndDistance=Mathf.Lerp(baseFogEnd,baseFogEnd*.78f,level);
        RenderSettings.fogColor=Color.Lerp(baseFogColor,new Color(.24f,.18f,.23f),level*.65f);
        RenderSettings.fogColor=Color.Lerp(RenderSettings.fogColor,new Color(.29f,.10f,.11f),moon*.65f);
        RenderSettings.ambientIntensity=baseAmbientIntensity*Mathf.Lerp(1,.82f,level);
        RenderSettings.ambientSkyColor=Color.Lerp(baseAmbientSky,baseAmbientSky*new Color(.88f,.74f,.83f),level);
        RenderSettings.ambientEquatorColor=Color.Lerp(baseAmbientEquator,baseAmbientEquator*new Color(.9f,.76f,.84f),level);
        RenderSettings.ambientGroundColor=Color.Lerp(baseAmbientGround,baseAmbientGround*.82f,level);
        if(battleSun!=null)
        {
            battleSun.color=Color.Lerp(baseSunColor,new Color(1,.34f,.28f),level*.72f);
            battleSun.color=Color.Lerp(battleSun.color,new Color(1,.19f,.13f),moon*.75f);
            battleSun.intensity=baseSunIntensity*Mathf.Lerp(1,.78f,level);
        }
    }
    void RestoreAtmosphere()
    {
        if(!atmosphereCaptured)return;
        if(battleSun!=null){battleSun.color=baseSunColor;battleSun.intensity=baseSunIntensity;}
        // Don't overwrite a newly loaded menu's scene-owned RenderSettings.
        if(SceneManager.GetActiveScene().handle!=atmosphereScene)return;
        RenderSettings.fog=baseFog;RenderSettings.fogDensity=baseFogDensity;RenderSettings.fogColor=baseFogColor;RenderSettings.fogEndDistance=baseFogEnd;
        RenderSettings.ambientIntensity=baseAmbientIntensity;RenderSettings.ambientSkyColor=baseAmbientSky;
        RenderSettings.ambientEquatorColor=baseAmbientEquator;RenderSettings.ambientGroundColor=baseAmbientGround;
    }

    void UpdateTips()
    {
        if (tipIndex >= Tips.Length || MatchTime < Tips[tipIndex].time) return;
        GameHUD.Toast(Tips[tipIndex].text, UIKit.Parchment, Tips[tipIndex].icon, 4.5f, 28);
        tipIndex++;
    }

    int tipIndex;
    static readonly (float time, string text, string icon)[] Tips =
    {
        (6f, "Press Q to swap between sword and bow", "bow"),
        (22f, "Hold right mouse to raise your shield - block as a blow lands for a Perfect Block", "shield"),
        (40f, "Arrows wait on supply crates beside the houses", "arrow"),
        (60f, "Space to dodge roll - you can't be hurt mid-roll", "stamina"),
    };

    void SpawnBoss(ZombieType type)
    {
        var spawner = ZombieSpawner.Instance;
        if (spawner == null) return;
        Boss = spawner.Spawn(type, true);
        if(Boss==null)return;
        ThreatStinger(type);
    }
    void ThreatStinger(ZombieType type)
    {
        lastThreatCue=Time.time;
        AudioManager.Play2D("boss_horn", 1f);
        FX.Shake(.55f);
        GameHUD.Banner(type == ZombieType.Warlord ? "<b>THE WARLORD APPROACHES</b>" : "<b>A BRUTE LEADS THE DEAD</b>",
            type==ZombieType.Warlord?"Break his guard. Survive his charge.":"A towering horror enters the battle.",3.2f);
    }

    float damageAtWaveStart;

    void StartWave(int n)
    {
        Wave = n;
        damageAtWaveStart = DamageTaken;
        if (n >= 10) Feats.Unlock("unbroken");
        var p = GameSettings.Profile;
        waveToSpawn = Mathf.RoundToInt((5 + n * 3) * Mathf.Lerp(0.8f, 1.2f, p.spawnRate - 0.75f));
        waveSpawned = 0;
        spawnTimer = 1.5f;
        State = MatchState.Playing;
        Horde.BeginWave(n);
        GameHUD.Banner("Wave " + n, n % 5 == 0 ? "A champion of the dead leads this assault" : "They rise again...", 2.6f);
        if(n%5!=0)AudioManager.Play2D("wave_horn", 0.9f);
        if (n % 5 == 0)
        {
            SpawnBoss(ZombieType.Brute);
            waveSpawned++;
            if(n%10==0){SpawnBoss(ZombieType.Warlord);waveSpawned++;}
        }
    }

    void WaveCleared()
    {
        if (DamageTaken <= damageAtWaveStart) Feats.Unlock("untouchable");
        State = MatchState.Intermission;
        Horde.ClearWave();
        IntermissionLeft = 9f;
        int bonus = 300 * Wave;
        Score += bonus;
        if (playerHealth != null) playerHealth.Heal(playerHealth.maxHealth * 0.25f);
        GameHUD.Banner($"Wave {Wave} Cleared", $"+{bonus} bonus  -  wounds tended", 3f);
        AudioManager.Play2D("victory", 0.6f);
        OfferBoon();
    }

    // =======================
    // GAMEPLAY
    // =======================

    public void AddKill() => AddKill(null);

    public void AddKill(EnemyController enemy)
    {
        if (gameEnded || State == MatchState.Dawn) return;

        currentKills++;
        var type = enemy != null ? enemy.type : ZombieType.Walker;
        killsByType[type] = killsByType.TryGetValue(type, out int k) ? k + 1 : 1;

        Combo++;
        BestCombo = Mathf.Max(BestCombo, Combo);
        Feats.Add("kills");
        if (type == ZombieType.Brute) Feats.Add("brutes");
        if (type == ZombieType.Warlord) Feats.Unlock("kingslayer");
        if (Combo >= 15) Feats.Unlock("butcher");
        comboExpires = Time.time + Boons.ComboWindow;
        int points = (enemy != null && enemy.Stats != null ? enemy.Stats.points : 100) * ComboMultiplier;
        if(enemy!=null&&Horde!=null)points*=Horde.ScoreMultiplier;
        if (enemy != null && enemy.isBoss) points *= 2;
        Score += points;
        UpdateScoreUI();
        Killed?.Invoke(enemy, points);
        if (enemy != null && enemy == Boss)
        {
            GameHUD.Banner(enemy.Stats.name + " Slain!", "", 2.5f);
            StartCoroutine(BossSlowMotion());
        }
    }

    IEnumerator BossSlowMotion()
    {
        CombatTime.SetScale(0.25f);
        yield return new WaitForSecondsRealtime(1.3f);
        if (!gameEnded && !isPaused && !ChoosingBoon && State != MatchState.Dawn) CombatTime.SetScale(1f);
    }

    public void RegisterArrowFired() => ArrowsFired++;

    public void RegisterArrowHit(EnemyController enemy, bool headshot)
    {
        if (State == MatchState.Dawn) return;
        ArrowsHit++;
        if (headshot) { Headshots++; Score += 25; Feats.Add("headshots"); }
    }

    public void RegisterMeleeHit(EnemyController enemy) { }
    public void RegisterExplosionHit(EnemyController enemy) { }
    public void RegisterDamageTaken(float amount) => DamageTaken += amount;

    void UpdateScoreUI()
    {
        if (scoreText)
            scoreText.text = $"Zombies Defeated: {currentKills} / {targetKills}";
    }

    public string Objective => Mode switch
    {
        GameMode.DefendTheCastle => $"Undead slain  {currentKills} / {targetKills}",
        GameMode.EndlessHorde => State == MatchState.Intermission ? $"Next wave in {Mathf.CeilToInt(IntermissionLeft)}" : $"Wave {Wave}  -  {WaveRemaining} remain",
        _ => $"Dawn in {Mathf.CeilToInt(TimeLeft) / 60}:{Mathf.CeilToInt(TimeLeft) % 60:00}",
    };

    void WinGame() => EndMatch(true);

    public void GameOver()
    {
        if (State == MatchState.Dawn) return;
        // In Endless Horde, death is the only ending: it still counts as a finished run
        EndMatch(false);
    }

    void EndMatch(bool victory)
    {
        if (gameEnded) return;
        bool dawnVictory = State == MatchState.Dawn;
        gameEnded = true;
        if(Horde!=null)Horde.ClearWave();
        State = victory ? MatchState.Won : MatchState.Lost;
        if (victory) Score += Mathf.RoundToInt(playerHealth != null ? playerHealth.currentHealth * 10f : 0f);
        NewRecord = GameSettings.SubmitScore(Mode, Score, Wave);
        if (victory && Mode == GameMode.DefendTheCastle) Feats.Unlock("defender");
        if (victory && Mode == GameMode.LastStand) Feats.Unlock("dawn");
        if (victory && GameSettings.Difficulty == Difficulty.Warlord) Feats.Unlock("legend");
        AudioManager.Play2D(victory ? "victory" : "defeat", 1f);
        AudioManager.SetBattleIntensity(0f);
        MatchEnded?.Invoke(victory);
        if (dawnVictory) CombatTime.SetScale(0f);
        else StartCoroutine(SlowToStop());
        LockCursor(false);
    }

    IEnumerator SlowToStop()
    {
        CombatTime.SetScale(0.35f);
        yield return new WaitForSecondsRealtime(1.6f);
        CombatTime.SetScale(0f);
    }

    // =======================
    // PAUSE
    // =======================

    // alt-tabbing away mid-fight shouldn't get you killed
    void OnApplicationFocus(bool focused)
    {
        if (!focused && !Application.isBatchMode && !GameSettings.TestMode && !gameEnded && !isPaused && !ChoosingBoon && State != MatchState.Intro)
            Pause();
    }

    public void Pause()
    {
        if (gameEnded || State == MatchState.Dawn) return;
        CombatTime.SetScale(0f);
        isPaused = IsPaused = true;
        LockCursor(false);
        PauseChanged?.Invoke(true);
    }

    public void Resume()
    {
        if (State == MatchState.Dawn) return;
        CombatTime.SetScale(ChoosingBoon ? 0f : 1f);
        isPaused = IsPaused = false;
        LockCursor(true);
        PauseChanged?.Invoke(false);
    }

    static void LockCursor(bool locked)
    {
        Cursor.visible = !locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
    }

    // =======================
    // BUTTON ACTIONS
    // =======================

    public void RestartGame()
    {
        CombatTime.SetScale(1f);
        BattleLoading.EnterBattle();
    }

    public void QuitGame()
    {
        CombatTime.SetScale(1f);
        LockCursor(false);
        BattleLoading.ReturnToMenu();
    }
}
