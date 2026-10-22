using UnityEngine;
using UnityEngine.SceneManagement;

// Main menu entry point. Loads the castle world as a live backdrop and builds the menu UI.
public class MainController : MonoBehaviour
{
    void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (!SceneManager.GetSceneByName("Background").isLoaded)
            SceneManager.LoadScene("Background", LoadSceneMode.Additive);

        // the old hand-made canvas is replaced by MainMenuUI
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.GetComponentInParent<MainMenuUI>() == null) c.gameObject.SetActive(false);

        if (FindFirstObjectByType<MainMenuUI>() == null) new GameObject("Main Menu UI").AddComponent<MainMenuUI>();
    }

    public void StartGame()
    {
        // GameManager in Semi-Final pulls in the Background (world) scene
        SceneManager.LoadScene("Semi-Final", LoadSceneMode.Single);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
