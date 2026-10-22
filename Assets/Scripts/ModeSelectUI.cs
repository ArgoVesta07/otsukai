using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The mode menu opened by a ModeSelectPoint.
//   Main panel:    Story / Classic / Close
//   Story panel:   Continue (Day 2 · Afternoon) / New story / Back   (only when a story save exists)
//   Confirm panel: "Your saved story will be overwritten" Yes / No
// Put this on a root canvas (e.g. HudCanvas). Hook the buttons to the public methods below.
[DefaultExecutionOrder(100)]   // after PauseMenu, so Esc closing this menu doesn't also open the pause menu
public class ModeSelectUI : MonoBehaviour
{
    static ModeSelectUI instance;

    [Header("Panels")]
    [SerializeField] GameObject mainPanel;
    [SerializeField] GameObject storyPanel;
    [SerializeField] GameObject confirmPanel;

    [Header("Story panel")]
    [SerializeField] GameObject continueButton;
    [SerializeField] TMP_Text continueLabel;
    [SerializeField] TMP_Text storyInfoText;      // optional

    [Header("Text")]
    [SerializeField] string continueFormat = "Continue  ({0})";   // {0} = "Day 2 · Afternoon"
    [SerializeField] string storyCompleteText = "You finished the story.";
    [SerializeField] string classicComingSoonText = "Classic mode is coming soon";

    [SerializeField] string gameSceneName = "MainScene";

    SaveData peek;   // the story save, read when the story panel opens

    bool IsOpen =>
        (mainPanel != null && mainPanel.activeSelf) ||
        (storyPanel != null && storyPanel.activeSelf) ||
        (confirmPanel != null && confirmPanel.activeSelf);

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ShowOnly(null);
    }

    void Update()
    {
        if (!IsOpen) return;

        // Esc goes back one step
        var k = Keyboard.current;
        if (k == null || !k.escapeKey.wasPressedThisFrame) return;

        if (confirmPanel != null && confirmPanel.activeSelf) CancelNewStory();
        else if (storyPanel != null && storyPanel.activeSelf) BackToMain();
        else Close();
    }

    // ---------- Opening and closing ----------

    public static void Open()
    {
        if (instance == null)
        {
            Debug.LogWarning("ModeSelectUI was not found!");
            return;
        }

        InteractionState.IsInteracting = true;   // lock the player while the menu is open
        instance.ShowOnly(instance.mainPanel);
    }

    public void Close()
    {
        ShowOnly(null);
        InteractionState.IsInteracting = false;
    }

    void ShowOnly(GameObject panel)
    {
        if (mainPanel != null) mainPanel.SetActive(panel == mainPanel);
        if (storyPanel != null) storyPanel.SetActive(panel == storyPanel);
        if (confirmPanel != null) confirmPanel.SetActive(panel == confirmPanel);
    }

    // ---------- Main panel ----------

    public void ChooseStory()
    {
        peek = SaveSystem.PeekSave();

        // No save yet: start right away
        if (peek == null)
        {
            StartNewStory();
            return;
        }

        bool canContinue = !peek.storyFinished;
        if (continueButton != null) continueButton.SetActive(canContinue);

        if (canContinue && continueLabel != null)
        {
            string label = TimeManager.Instance != null
                ? TimeManager.Instance.LabelFor(peek.dayIndex, peek.periodIndex)
                : "";
            continueLabel.text = string.Format(continueFormat, label);
        }

        if (storyInfoText != null)
            storyInfoText.text = canContinue ? "" : storyCompleteText;

        ShowOnly(storyPanel);
    }

    public void ChooseClassic()
    {
        if (ClassicManager.Instance == null)
        {
            Toast.Show(classicComingSoonText);
            return;
        }

        Close();
        ClassicManager.Instance.StartClassic();
    }

    // ---------- Story panel ----------

    public void ContinueStory()
    {
        Close();
        SaveSystem.LoadGame(gameSceneName);
    }

    public void AskNewStory()
    {
        // Only ask when an unfinished story would be overwritten
        if (confirmPanel != null && peek != null && !peek.storyFinished)
            ShowOnly(confirmPanel);
        else
            StartNewStory();
    }

    public void StartNewStory()
    {
        Close();

        SaveSystem.ResetProgress();   // clear errands, items and flags from earlier play

        if (TimeManager.Instance != null)
            TimeManager.Instance.BeginStory();   // the old save is replaced by the first auto-save
    }

    public void BackToMain()
    {
        ShowOnly(mainPanel);
    }

    // ---------- Confirm panel ----------

    public void CancelNewStory()
    {
        ShowOnly(storyPanel);
    }
}
