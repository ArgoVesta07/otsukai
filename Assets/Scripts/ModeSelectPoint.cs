using UnityEngine;
using UnityEngine.InputSystem;

// Object in town for choosing a mode. Needs a Collider2D with Is Trigger ticked.
//   Free mode:      opens the mode menu (Story / Classic / Challenge).
//   Inside a mode:  asks to go back to free mode (modes are only chosen from free mode).
public class ModeSelectPoint : MonoBehaviour
{
    [GameText] public string promptText = "[E] Choose mode";

    [Tooltip("Prompt while a mode is running. Empty = the object does nothing during modes.")]
    [GameText] public string promptTextInMode = "[E] Back to free mode";

    bool playerNear;
    string shownText;   // what this object shows in the shared prompt (null = nothing)

    static bool InFreeMode =>
        (TimeManager.Instance == null || !TimeManager.Instance.InStory) && !ChallengeManager.IsActive && !ClassicManager.IsActive;

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
            SetPrompt(null);
        }
    }

    void OnDisable()
    {
        playerNear = false;
        SetPrompt(null);
    }

    void Update()
    {
        if (!playerNear) return;

        string text = InteractionState.IsInteracting ? null
                    : InFreeMode ? promptText
                    : promptTextInMode;
        SetPrompt(text);
        if (string.IsNullOrEmpty(text)) return;

        var k = Keyboard.current;
        if (k != null && k.eKey.wasPressedThisFrame)
        {
            SetPrompt(null);
            ModeSelectUI.Open();   // the menu itself decides: mode list, or "back to free mode?"
        }
    }

    // Only touches the shared prompt when our own state changes
    void SetPrompt(string text)
    {
        if (string.IsNullOrEmpty(text)) text = null;
        if (text == shownText) return;

        bool wasShown = shownText != null;
        shownText = text;

        if (text != null) InteractionPrompt.Show(text, transform);
        else if (wasShown) InteractionPrompt.Hide();
    }
}
