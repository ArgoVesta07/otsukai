using UnityEngine;
using UnityEngine.SceneManagement;

// Main menu: Start (enters town in free mode), How to Play, Quit.
// Story and Classic are chosen in town with a ModeSelectPoint.
public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "MainScene";

    [Tooltip("Optional panel that explains the game. Hidden at start.")]
    [SerializeField] GameObject howToPlayPanel;

    void Start()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(false);
    }

    public void StartGame()
    {
        SaveSystem.ResetProgress();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenHowToPlay()
    {
        if (howToPlayPanel != null) howToPlayPanel.SetActive(true);
    }

    public void CloseHowToPlay()
    {
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
    }

    public void QuitGame()
    {
        Debug.Log("Quit");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
