using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Level Goals")]
    public int targetKills = 10;
    public int currentKills = 0;

    [Header("UI")]
    public GameObject gameplayUI;   // Health bar + score
    public GameObject victoryUI;
    public GameObject gameOverUI;
    public GameObject pauseMenuUI;
    public TextMeshProUGUI scoreText;

    bool isPaused;
    bool gameEnded;

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
    }

    void Start()
    {
        StartGameplay();
    }

    void Update()
    {
        if (gameEnded) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    // =======================
    // GAME FLOW
    // =======================

    void StartGameplay()
    {
        currentKills = 0;
        isPaused = false;
        gameEnded = false;

        if (gameplayUI) gameplayUI.SetActive(true);
        if (victoryUI) victoryUI.SetActive(false);
        if (gameOverUI) gameOverUI.SetActive(false);
        if (pauseMenuUI) pauseMenuUI.SetActive(false);

        UpdateScoreUI();

        // Reset player health safely
        PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
        if (player != null)
            player.ResetHealth();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Time.timeScale = 1f;
    }

    // =======================
    // GAMEPLAY
    // =======================

    public void AddKill()
    {
        if (gameEnded) return;

        currentKills++;
        UpdateScoreUI();

        if (currentKills >= targetKills)
            WinGame();
    }

    void UpdateScoreUI()
    {
        if (scoreText)
            scoreText.text = $"Zombies Defeated: {currentKills} / {targetKills}";
    }

    void WinGame()
    {
        gameEnded = true;
        Time.timeScale = 0f;

        if (victoryUI) victoryUI.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void GameOver()
    {
        if (gameEnded) return;

        gameEnded = true;
        Time.timeScale = 0f;

        if (gameOverUI) gameOverUI.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // =======================
    // PAUSE
    // =======================

    public void Pause()
    {
        if (gameEnded) return;

        if (pauseMenuUI) pauseMenuUI.SetActive(true);

        Time.timeScale = 0f;
        isPaused = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume()
    {
        if (pauseMenuUI) pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // =======================
    // BUTTON ACTIONS
    // =======================

    public void RestartGame()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene("Semi-Final", LoadSceneMode.Single);
        SceneManager.LoadScene("Background", LoadSceneMode.Additive);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }
}
