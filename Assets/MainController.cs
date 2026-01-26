using UnityEngine;
using UnityEngine.SceneManagement;

public class MainController : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("Semi-Final", LoadSceneMode.Single);
        SceneManager.LoadScene("Background", LoadSceneMode.Additive);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
