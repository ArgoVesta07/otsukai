using UnityEngine;
using UnityEngine.InputSystem;

// A place the player can interact with to complete an errand step (shelf, mailbox, counter, ...).
// Needs a Collider2D with Is Trigger ticked. Which errand uses it is set in the ErrandData asset.
// When no errand needs it, it can still show its name ("Postbox") and say something when E is pressed.
public class ErrandSpot : MonoBehaviour
{
    [Tooltip("Must match the Target Id of the errand step that uses this spot.")]
    public string targetId = "EggShelf";

    [GameText] public string promptText = "[E] Interact";

    [Tooltip("Ticked = the [E] prompt only shows when an active errand needs this spot.")]
    public bool onlyWhenNeeded = true;

    [Header("When no errand needs this spot")]
    [Tooltip("Name shown near the object, e.g. \"Postbox\". Empty = show nothing.")]
    [GameText] public string idleLabel = "";

    [Tooltip("Said in a speech bubble when E is pressed. Empty = E does nothing.")]
    [GameText] [TextArea(2, 4)] public string[] inspectLines;

    public float secondsPerInspectLine = 2.5f;

    bool playerNear;
    string shownText;      // what this spot is showing in the shared prompt (null = nothing)
    bool shownAsLabel;

    bool HasInspectLines => inspectLines != null && inspectLines.Length > 0;

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
        bool needed = CanUse();

        if (busy) SetPrompt(null, false);
        else if (needed) SetPrompt(promptText, false);
        else SetPrompt(idleLabel, true);

        var k = Keyboard.current;
        if (k == null || !k.eKey.wasPressedThisFrame) return;
        if (busy) return;

        if (needed)
        {
            QuestManager quest = QuestManager.Instance;
            if (quest == null)
            {
                Debug.LogWarning("QuestManager was not found!");
                return;
            }

            if (!quest.TryCompleteStep(targetId))
                Debug.Log(targetId + ": nothing to do here right now.");
        }
        else if (HasInspectLines)
        {
            SpeechBubble.Show(transform, inspectLines, secondsPerInspectLine);
        }
    }

    bool CanUse()
    {
        if (!onlyWhenNeeded) return true;

        QuestManager quest = QuestManager.Instance;
        return quest != null && quest.IsCurrentTarget(targetId);
    }

    // Only touches the shared prompt when our own state changes,
    // so we don't hide a prompt that belongs to a nearby NPC or door.
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
