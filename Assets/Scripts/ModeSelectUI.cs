using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;

// The mode menu opened by a ModeSelectPoint.
//   Main panel:    Story / Classic / Challenge / Close   (free mode only)
//   Story panel:   Continue (Day 2 · Afternoon) / New story / Back   (only when a story save exists)
//   Confirm panel: a question with Yes / No (overwrite the story, or go back to free mode)
// Modes are only chosen in free mode. While a mode runs, opening the menu asks "Back to free mode?".
// Put this on a root canvas (e.g. HudCanvas). Hook the buttons to the public methods below.
[DefaultExecutionOrder(100)]   // after PauseMenu, so Esc closing this menu doesn't also open the pause menu
public class ModeSelectUI : MonoBehaviour
{
    static ModeSelectUI instance;

    [Header("Panels")]
    [SerializeField] GameObject mainPanel;
    [SerializeField] GameObject storyPanel;
    [SerializeField] GameObject confirmPanel;

    [Header("Confirm panel")]
    [Tooltip("The question text on the confirm panel (filled by script).")]
    [SerializeField] TMP_Text confirmText;

    [Header("Story")]
    [Tooltip("Off = the Story button only shows \"coming soon\" (while story mode is being made).")]
    [SerializeField] bool storyAvailable = false;
    [GameText] [SerializeField] string storyComingSoonText = "Story mode is coming soon";

    [Header("Story panel")]
    [SerializeField] GameObject continueButton;
    [SerializeField] TMP_Text continueLabel;
    [SerializeField] TMP_Text storyInfoText;      // optional

    [Header("Text")]
    [GameText] [SerializeField] string continueFormat = "Continue  ({0})";   // {0} = "Day 2 · Afternoon"
    [GameText] [SerializeField] string storyCompleteText = "You finished the story.";
    [GameText] [SerializeField, FormerlySerializedAs("classicComingSoonText")] string challengeComingSoonText = "Challenge mode is coming soon";

    [GameText] [SerializeField] string overwriteStoryText = "Your saved story will be overwritten. Start a new story?";
    [GameText] [SerializeField] string backToFreeText = "Go back to free mode?";
    [GameText] [SerializeField] string leaveStoryText = "Unsaved story progress will be lost. Continue?";
    [GameText] [SerializeField] string leaveChallengeText = "Your challenge run will end. Continue?";
    [GameText] [SerializeField] string freeModeText = "Free mode";

    [SerializeField] string gameSceneName = "MainScene";

    SaveData peek;           // the story save, read when the story panel opens
    Action pendingAction;    // runs when Yes is pressed on the confirm panel
    GameObject backPanel;    // shown again when No is pressed

    static bool InStory => TimeManager.Instance != null && TimeManager.Instance.InStory;
    static bool ModeRunning => InStory || ChallengeManager.IsActive || ClassicManager.IsActive;

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

        if (confirmPanel != null && confirmPanel.activeSelf) ConfirmNo();
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

        // Inside a mode: only offer the way back to free mode
        if (ModeRunning && instance.confirmPanel != null)
        {
            string question = instance.LeaveQuestion() ?? instance.backToFreeText;
            instance.AskConfirm(question, instance.GoToFreeMode, null);
            return;
        }

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
        if (!storyAvailable)
        {
            Toast.Show(Loc.T(storyComingSoonText));
            return;
        }

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
            continueLabel.text = Loc.F(continueFormat, label);
        }

        if (storyInfoText != null)
            storyInfoText.text = canContinue ? "" : Loc.T(storyCompleteText);

        ShowOnly(storyPanel);
    }

    public void ChooseClassic()
    {
        if (ClassicManager.Instance == null)
        {
            Debug.LogWarning("ClassicManager was not found in the scene.");
            return;
        }

        SwitchMode(() => ClassicManager.Instance.StartClassic());
    }

    public void ChooseChallenge()
    {
        if (ChallengeManager.Instance == null)
        {
            Toast.Show(Loc.T(challengeComingSoonText));
            return;
        }

        SwitchMode(() => ChallengeManager.Instance.StartChallenge());
    }

    // Leaves the running mode and goes back to free mode (walk around, nothing running)
    public void ChooseFreeMode()
    {
        SwitchMode(EnterFreeMode);
    }

    // Used by the confirm panel (the question was already asked)
    void GoToFreeMode()
    {
        Close();
        LeaveCurrentMode();
        EnterFreeMode();
    }

    void EnterFreeMode()
    {
        SaveSystem.ResetProgress();   // drop errands, items and flags from the mode
        Toast.Show(Loc.T(freeModeText));
    }

    // ---------- Switching modes ----------

    // Leaves the running mode (asking first if progress would be lost), then runs 'start'
    void SwitchMode(Action start)
    {
        Action go = () =>
        {
            Close();
            LeaveCurrentMode();
            start?.Invoke();
        };

        string question = LeaveQuestion();
        if (question != null && confirmPanel != null)
            AskConfirm(question, go, mainPanel);
        else
            go();
    }

    // What to ask before leaving the running mode, or null when nothing would be lost
    string LeaveQuestion()
    {
        if (ChallengeManager.IsActive) return leaveChallengeText;
        if (InStory && SaveSystem.HasUnsavedProgress()) return leaveStoryText;
        return null;   // classic mode saves by itself; free mode has nothing to lose
    }

    static void LeaveCurrentMode()
    {
        if (ChallengeManager.Instance != null && ChallengeManager.IsActive) ChallengeManager.Instance.Stop();
        if (ClassicManager.Instance != null && ClassicManager.IsActive) ClassicManager.Instance.Stop();
        if (TimeManager.Instance != null) TimeManager.Instance.EnterFreeMode();   // also ends story mode
    }

    // ---------- Story panel ----------

    public void ContinueStory()
    {
        SwitchMode(() => SaveSystem.LoadGame(gameSceneName));
    }

    public void AskNewStory()
    {
        // Ask when an unfinished story would be overwritten (that also covers leaving the running mode)
        if (confirmPanel != null && peek != null && !peek.storyFinished)
        {
            AskConfirm(overwriteStoryText, () =>
            {
                Close();
                LeaveCurrentMode();
                BeginNewStory();
            }, storyPanel);
        }
        else
        {
            SwitchMode(BeginNewStory);
        }
    }

    // Kept for old button links: starts a new story without asking
    public void StartNewStory()
    {
        Close();
        LeaveCurrentMode();
        BeginNewStory();
    }

    static void BeginNewStory()
    {
        SaveSystem.ResetProgress();   // clear errands, items and flags from earlier play

        if (TimeManager.Instance != null)
            TimeManager.Instance.BeginStory();   // the old save is replaced by the first auto-save
    }

    public void BackToMain()
    {
        ShowOnly(mainPanel);
    }

    // ---------- Confirm panel ----------

    void AskConfirm(string question, Action yes, GameObject back)
    {
        pendingAction = yes;
        backPanel = back;
        if (confirmText != null) confirmText.text = Loc.T(question);
        ShowOnly(confirmPanel);
    }

    public void ConfirmYes()
    {
        Action action = pendingAction;
        pendingAction = null;
        action?.Invoke();
    }

    public void ConfirmNo()
    {
        pendingAction = null;

        if (backPanel != null) ShowOnly(backPanel);
        else Close();   // asked straight from the world (back to free mode?): just close
    }

    // Kept for old button links
    public void CancelNewStory()
    {
        ConfirmNo();
    }
}
