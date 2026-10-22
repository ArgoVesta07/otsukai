using UnityEngine;
using UnityEngine.InputSystem;

// Object in town that opens the mode menu (Story / Challenge). Only usable in free mode.
// Needs a Collider2D with Is Trigger ticked.
public class ModeSelectPoint : MonoBehaviour
{
    [GameText] public string promptText = "[E] Choose mode";

    bool playerNear;
    bool promptShown;

    static bool InFreeMode =>
        (TimeManager.Instance == null || !TimeManager.Instance.InStory) && !ChallengeManager.IsActive;

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

        bool usable = InFreeMode && !InteractionState.IsInteracting;
        SetPrompt(usable);
        if (!usable) return;

        var k = Keyboard.current;
        if (k != null && k.eKey.wasPressedThisFrame)
        {
            SetPrompt(false);
            ModeSelectUI.Open();
        }
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
