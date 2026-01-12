using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    // Singleton: Lets zombies find this script easily
    public static GameManager instance; 

    [Header("Level Goals")]
    public int targetKills = 10; // WIN CONDITION
    public int currentKills = 0;

    [Header("UI Panels")]
    public GameObject victoryUI; 
    public GameObject pauseMenuUI; 
    public TextMeshProUGUI scoreText; // Optional: To show "Kills: 0/10"
    
    private bool isPaused = false;
    private bool gameEnded = false;

    void Awake()
    {
        // Set up the Singleton
        if (instance == null) instance = this;
    }

    void Start()
    {
        UpdateScoreUI();
    }

    void Update()
    {
        if (gameEnded) return;

        // PAUSE INPUT (ESC Key)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    // CALLED BY ZOMBIE WHEN IT DIES
    public void AddKill()
    {
        if (gameEnded) return;

        currentKills++;
        UpdateScoreUI();

        // CHECK VICTORY: Only win if we hit the target number
        if (currentKills >= targetKills)
        {
            WinGame();
        }
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Zombies Defeated: " + currentKills + " / " + targetKills;
        }
    }

    void WinGame()
    {
        gameEnded = true;
        victoryUI.SetActive(true);
        Time.timeScale = 0; 
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0;
        isPaused = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1;
        isPaused = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    
    public void RestartGame()
    {
        Time.timeScale = 1; 
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
}