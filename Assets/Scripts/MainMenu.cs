using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;

// Main menu: Start (enters town in free mode), How to Play, Quit, the challenge best score
// and the language switch (see LanguageToggle).
// Story and Challenge are chosen in town with a ModeSelectPoint.
public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "MainScene";

    [Tooltip("Optional panel that explains the game. Hidden at start.")]
    [SerializeField] GameObject howToPlayPanel;

    [Header("Challenge best score")]
    [SerializeField] TMP_Text bestScoreText;
    [Tooltip("Entry text uses {0} = score, {1} = day. Example: Challenge best: {0} · Day {1}")]
    [SerializeField] LocalizedString bestScoreFormat;
    [Tooltip("Shown when there is no challenge record yet.")]
    [SerializeField] LocalizedString noRecordText;

    LocalizedString shownText;   // the one currently feeding bestScoreText

    void Start()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(false);
    }

    void OnEnable()
    {
        ShowBestScore();
    }

    void OnDisable()
    {
        StopListening();
    }

    void ShowBestScore()
    {
        StopListening();
        if (bestScoreText == null) return;

        int best = ChallengeManager.BestScore;
        LocalizedString next = best > 0 ? bestScoreFormat : noRecordText;

        if (next == null || next.IsEmpty)
        {
            Debug.LogWarning("MainMenu: best score text is not set to a table entry.");
            bestScoreText.text = "";
            return;
        }

        if (best > 0)
            next.Arguments = new object[] { best, ChallengeManager.BestDay };

        shownText = next;
        shownText.StringChanged += SetBestScoreText;   // runs now and again on every language change
    }

    void StopListening()
    {
        if (shownText != null)
            shownText.StringChanged -= SetBestScoreText;
        shownText = null;
    }

    void SetBestScoreText(string value)
    {
        if (bestScoreText != null)
            bestScoreText.text = value;
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