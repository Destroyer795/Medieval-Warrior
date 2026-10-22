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

    public enum MatchState { Intro, Playing, Intermission, Won, Lost }

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
        Time.timeScale = 1f;

        // The castle, terrain and props live in the Background scene.
        // Load it here so the level also works when played straight from the editor.
        if (!SceneManager.GetSceneByName(WorldSceneName).isLoaded)
            SceneManager.LoadScene(WorldSceneName, LoadSceneMode.Additive);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        StartGameplay();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (gameEnded) return;
            if (isPaused) Resume();
            else Pause();
        }
        if (gameEnded || isPaused) return;

        MatchTime += Time.deltaTime;
        if (Combo > 0 && Time.time > comboExpires) Combo = 0;

        switch (State)
        {
            case MatchState.Intro:
                introTimer -= Time.deltaTime;
                if (introTimer <= 0f) BeginPlay();
                break;
            case MatchState.Playing:
                UpdateMode();
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
        Mode = GameSettings.Mode;
        currentKills = 0;
        isPaused = false;
        gameEnded = false;
        foreach (ZombieType t in Enum.GetValues(typeof(ZombieType))) killsByType[t] = 0;

        foreach (var legacy in new[] { gameplayUI, victoryUI, gameOverUI, pauseMenuUI })
            if (legacy) legacy.SetActive(false);

        var profile = GameSettings.Profile;
        targetKills = profile.defendTarget;
        TimeLeft = profile.lastStandMinutes * 60f;

        // Reset player health safely
        var playerObj = GameObject.FindWithTag("Player");
        playerHealth = playerObj != null ? playerObj.GetComponent<PlayerHealth>() : null;
        if (playerHealth != null) playerHealth.ResetHealth();

        if (FindFirstObjectByType<GameHUD>() == null) new GameObject("Game HUD").AddComponent<GameHUD>();
        AudioManager.PlayMusic("ambient_meadow", 0.9f);

        LockCursor(true);
        Time.timeScale = 1f;
        State = MatchState.Intro;
        introTimer = 3.5f;
        GameHUD.Banner(GameSettings.ModeName(Mode), IntroLine(), 3.2f);
        AudioManager.Play2D("wave_horn", 0.8f);
    }

    string IntroLine() => Mode switch
    {
        GameMode.DefendTheCastle => $"Slay {targetKills} of the undead",
        GameMode.EndlessHorde => "Survive as many waves as you can",
        _ => $"Hold out for {Mathf.RoundToInt(TimeLeft / 60f)} minutes until dawn",
    };

    void BeginPlay()
    {
        if (Mode == GameMode.EndlessHorde) StartWave(1);
        else State = MatchState.Playing;
        spawnTimer = 1f;
        nextBruteTime = MatchTime + 70f;
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
            {
                TimeLeft -= Time.deltaTime;
                float total = p.lastStandMinutes * 60f;
                float progress = 1f - Mathf.Clamp01(TimeLeft / total);
                int cap = Mathf.RoundToInt(Mathf.Lerp(5, p.maxAlive + 4, progress));
                TickSpawner(cap, Mathf.Lerp(2.2f, 0.7f, progress) / p.spawnRate, progress);
                if (!warlordSpawned && progress >= 0.6f) { warlordSpawned = true; SpawnBoss(ZombieType.Warlord); }
                if (MatchTime > nextBruteTime) { nextBruteTime = MatchTime + Mathf.Lerp(70f, 35f, progress); Spawn(ZombieType.Brute); }
                if (TimeLeft <= 0f) { TimeLeft = 0f; WinGame(); }
                break;
            }
        }
    }

    void TickSpawner(int cap, float interval, float progress, bool waveCounted = false)
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f || EnemyController.All.Count >= cap) return;
        spawnTimer = interval * UnityEngine.Random.Range(0.7f, 1.3f);
        Spawn(PickType(progress));
        if (waveCounted) waveSpawned++;
    }

    ZombieType PickType(float progress)
    {
        // weighted mix that unlocks nastier undead as the match goes on
        float runner = progress > 0.08f ? 0.22f : 0f;
        float spitter = progress > 0.2f ? 0.14f : 0f;
        float bloater = progress > 0.32f ? 0.12f : 0f;
        float brute = progress > 0.5f ? 0.05f : 0f;
        float walker = 1f;
        float roll = UnityEngine.Random.value * (walker + runner + spitter + bloater + brute);
        if ((roll -= walker) < 0) return ZombieType.Walker;
        if ((roll -= runner) < 0) return ZombieType.Runner;
        if ((roll -= spitter) < 0) return ZombieType.Spitter;
        if ((roll -= bloater) < 0) return ZombieType.Bloater;
        return ZombieType.Brute;
    }

    EnemyController Spawn(ZombieType type)
    {
        var spawner = ZombieSpawner.Instance;
        return spawner != null ? spawner.Spawn(type, false) : null;
    }

    void SpawnBoss(ZombieType type)
    {
        var spawner = ZombieSpawner.Instance;
        if (spawner == null) return;
        Boss = spawner.Spawn(type, true);
        AudioManager.Play2D("boss_horn", 1f);
        GameHUD.Banner(type == ZombieType.Warlord ? "The Warlord Approaches" : "A Brute Leads the Wave", "Bring it down!", 3f);
    }

    void StartWave(int n)
    {
        Wave = n;
        var p = GameSettings.Profile;
        waveToSpawn = Mathf.RoundToInt((5 + n * 3) * Mathf.Lerp(0.8f, 1.2f, p.spawnRate - 0.75f));
        waveSpawned = 0;
        spawnTimer = 1.5f;
        State = MatchState.Playing;
        GameHUD.Banner("Wave " + n, n % 5 == 0 ? "A champion of the dead leads this assault" : "They rise again...", 2.6f);
        AudioManager.Play2D("wave_horn", 0.9f);
        if (n % 5 == 0)
        {
            SpawnBoss(n % 10 == 0 ? ZombieType.Warlord : ZombieType.Brute);
            waveSpawned++;
        }
    }

    void WaveCleared()
    {
        State = MatchState.Intermission;
        IntermissionLeft = 9f;
        int bonus = 300 * Wave;
        Score += bonus;
        if (playerHealth != null) playerHealth.Heal(playerHealth.maxHealth * 0.25f);
        GameHUD.Banner($"Wave {Wave} Cleared", $"+{bonus} bonus  -  wounds tended", 3f);
        AudioManager.Play2D("victory", 0.6f);
    }

    // =======================
    // GAMEPLAY
    // =======================

    public void AddKill() => AddKill(null);

    public void AddKill(EnemyController enemy)
    {
        if (gameEnded) return;

        currentKills++;
        var type = enemy != null ? enemy.type : ZombieType.Walker;
        killsByType[type] = killsByType.TryGetValue(type, out int k) ? k + 1 : 1;

        Combo++;
        BestCombo = Mathf.Max(BestCombo, Combo);
        comboExpires = Time.time + 4f;
        int points = (enemy != null && enemy.Stats != null ? enemy.Stats.points : 100) * ComboMultiplier;
        if (enemy != null && enemy.isBoss) points *= 2;
        Score += points;
        UpdateScoreUI();
        Killed?.Invoke(enemy, points);
        if (enemy != null && enemy == Boss) GameHUD.Banner(enemy.Stats.name + " Slain!", "", 2.5f);
    }

    public void RegisterArrowFired() => ArrowsFired++;

    public void RegisterArrowHit(EnemyController enemy, bool headshot)
    {
        ArrowsHit++;
        if (headshot) { Headshots++; Score += 25; }
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
        _ => $"Dawn in {Mathf.FloorToInt(TimeLeft / 60f)}:{Mathf.FloorToInt(TimeLeft % 60f):00}",
    };

    void WinGame() => EndMatch(true);

    public void GameOver()
    {
        // In Endless Horde, death is the only ending: it still counts as a finished run
        EndMatch(false);
    }

    void EndMatch(bool victory)
    {
        if (gameEnded) return;
        gameEnded = true;
        State = victory ? MatchState.Won : MatchState.Lost;
        if (victory) Score += Mathf.RoundToInt(playerHealth != null ? playerHealth.currentHealth * 10f : 0f);
        NewRecord = GameSettings.SubmitScore(Mode, Score, Wave);
        AudioManager.Play2D(victory ? "victory" : "defeat", 1f);
        MatchEnded?.Invoke(victory);
        StartCoroutine(SlowToStop());
        LockCursor(false);
    }

    IEnumerator SlowToStop()
    {
        Time.timeScale = 0.35f;
        yield return new WaitForSecondsRealtime(1.6f);
        Time.timeScale = 0f;
    }

    // =======================
    // PAUSE
    // =======================

    public void Pause()
    {
        if (gameEnded) return;
        Time.timeScale = 0f;
        isPaused = IsPaused = true;
        LockCursor(false);
        PauseChanged?.Invoke(true);
    }

    public void Resume()
    {
        Time.timeScale = 1f;
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
        Time.timeScale = 1f;
        SceneManager.LoadScene("Semi-Final", LoadSceneMode.Single);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        LockCursor(false);
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }
}
