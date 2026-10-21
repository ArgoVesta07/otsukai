using UnityEngine;
using UnityEngine.InputSystem;

// A checkpoint the player can use to save (bed, desk, ...). Only works in story mode.
// Needs a Collider2D with Is Trigger ticked. Results are shown with a Toast pop-up.
public class SavePoint : MonoBehaviour
{
    public string promptText = "[E] Save";
    public string savedText = "Game saved";
    public string blockedText = "Can't save during an errand";
    public string failedText = "Save failed";

    [Tooltip("Off = saving is only allowed when no errand is running.")]
    public bool allowDuringErrand = false;

    bool playerNear;
    bool promptShown;

    static bool InStory => TimeManager.Instance != null && TimeManager.Instance.InStory;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
            playerNear = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
        {
            playerNear = false;
            SetPrompt(false);
        }
    }

    void Update()
    {
        if (!playerNear) return;

        SetPrompt(InStory && !InteractionState.IsInteracting);

        if (!InStory) return;
        if (InteractionState.IsInteracting) return;

        var k = Keyboard.current;
        if (k == null || !k.eKey.wasPressedThisFrame) return;

        QuestManager quest = QuestManager.Instance;
        bool errandRunning = quest != null && quest.Active.Count > 0;

        if (errandRunning && !allowDuringErrand)
        {
            Toast.Show(blockedText);
            return;
        }

        Toast.Show(SaveSystem.Save() ? savedText : failedText);
    }

    // Only touches the shared prompt when our own state changes
    void SetPrompt(bool show)
    {
        if (show == promptShown) return;
        promptShown = show;

        if (show) InteractionPrompt.Show(promptText, transform);
        else InteractionPrompt.Hide();
    }
}