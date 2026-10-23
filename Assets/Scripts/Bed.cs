using UnityEngine;
using UnityEngine.InputSystem;

// A bed. Free, classic and challenge mode: sleep here at night to start the next day (see WorldClock).
// Story mode: sleep here after the day's last round (shows the day recap, then the next day).
// Needs a Collider2D with Is Trigger ticked.
// Tip: add a NavTarget with the id "Bed" to this object (and to the house door outside) so the arrow finds it.
public class Bed : MonoBehaviour
{
    [GameText] public string promptText = "[E] Sleep";

    [Tooltip("Shown when sleeping isn't possible (story mode during the day). Empty = nothing.")]
    [GameText] public string idleLabel = "Bed";

    bool playerNear;
    string shownText;
    bool shownAsLabel;

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
            SetPrompt(null, false);
        }
    }

    void OnDisable()
    {
        playerNear = false;
        SetPrompt(null, false);
    }

    void Update()
    {
        if (!playerNear) return;

        bool busy = InteractionState.IsInteracting;
        bool story = TimeManager.Instance != null && TimeManager.Instance.AwaitingSleep;
        bool clock = WorldClock.IsActive;
        bool usable = story || clock;

        if (busy) SetPrompt(null, false);
        else if (usable) SetPrompt(promptText, false);
        else SetPrompt(idleLabel, true);

        if (busy || !usable) return;

        var k = Keyboard.current;
        if (k != null && k.eKey.wasPressedThisFrame)
        {
            bool slept = story ? TimeManager.Instance.Sleep() : WorldClock.Instance.TrySleep();
            if (slept) SetPrompt(null, false);
        }
    }

    // Only touches the shared prompt when our own state changes
    void SetPrompt(string text, bool asLabel)
    {
        if (string.IsNullOrEmpty(text)) text = null;
        if (text == shownText && asLabel == shownAsLabel) return;

        bool wasShown = shownText != null;
        shownText = text;
        shownAsLabel = asLabel;

        if (text == null)
        {
            if (wasShown) InteractionPrompt.Hide();
        }
        else if (asLabel)
        {
            InteractionPrompt.ShowLabel(text, transform);
        }
        else
        {
            InteractionPrompt.Show(text, transform);
        }
    }
}
