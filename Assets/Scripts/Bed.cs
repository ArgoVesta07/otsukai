using UnityEngine;
using UnityEngine.InputSystem;

// A bed. In classic mode the player sleeps here to start the next day.
// In other modes it only shows its name (or nothing). Needs a Collider2D with Is Trigger ticked.
public class Bed : MonoBehaviour
{
    [GameText] public string promptText = "[E] Sleep";

    [Tooltip("Shown when sleeping isn't possible here (other modes). Empty = nothing.")]
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
        bool classic = ClassicManager.IsActive;

        if (busy) SetPrompt(null, false);
        else if (classic) SetPrompt(promptText, false);
        else SetPrompt(idleLabel, true);

        if (busy || !classic) return;

        var k = Keyboard.current;
        if (k != null && k.eKey.wasPressedThisFrame)
        {
            if (ClassicManager.Instance.Sleep())
                SetPrompt(null, false);
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
