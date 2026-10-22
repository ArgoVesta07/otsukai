using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Main menu: Start (enters town in free mode), How to Play, Quit, the classic best score
// and the language switch (see LanguageToggle).
// Story and Classic are chosen in town with a ModeSelectPoint.
public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "MainScene";

    [Tooltip("Optional panel that explains the game. Hidden at start.")]
    [SerializeField] GameObject howToPlayPanel;

    [Header("Classic best score")]
    [SerializeField] TMP_Text bestScoreText;
    [SerializeField] string bestScoreFormat = "Classic best: {0} · Day {1}";
    [SerializeField] string noRecordText = "Classic best: none yet";

    void Start()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(false);

        ShowBestScore();
    }

    void ShowBestScore()
    {
        if (bestScoreText == null) return;

        int best = ClassicManager.BestScore;
        bestScoreText.text = best > 0
            ? string.Format(bestScoreFormat, best, ClassicManager.BestDay)
            : noRecordText;
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
