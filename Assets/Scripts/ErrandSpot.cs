using UnityEngine;
using UnityEngine.InputSystem;

// A place the player can interact with to complete an errand step (shelf, mailbox, counter, ...).
// Needs a Collider2D with Is Trigger ticked. Which errand uses it is set in the ErrandData asset.
public class ErrandSpot : MonoBehaviour
{
    [Tooltip("Must match the Target Id of the errand step that uses this spot.")]
    public string targetId = "EggShelf";

    [GameText] public string promptText = "[E] Interact";

    [Tooltip("Ticked = the prompt only shows when an active errand needs this spot.")]
    public bool onlyWhenNeeded = true;

    bool playerNear;
    bool promptShown;

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

        SetPrompt(CanUse() && !InteractionState.IsInteracting);

        var k = Keyboard.current;
        if (k == null || !k.eKey.wasPressedThisFrame) return;
        if (InteractionState.IsInteracting) return;
        if (!CanUse()) return;

        QuestManager quest = QuestManager.Instance;
        if (quest == null)
        {
            Debug.LogWarning("QuestManager was not found!");
            return;
        }

        if (!quest.TryCompleteStep(targetId))
            Debug.Log(targetId + ": nothing to do here right now.");
    }

    bool CanUse()
    {
        if (!onlyWhenNeeded) return true;

        QuestManager quest = QuestManager.Instance;
        return quest != null && quest.IsCurrentTarget(targetId);
    }

    // Only touches the shared prompt when our own state changes,
    // so we don't hide a prompt that belongs to a nearby NPC or door.
    void SetPrompt(bool show)
    {
        if (show == promptShown) return;
        promptShown = show;

        if (show)
            InteractionPrompt.Show(promptText, transform);
        else
            InteractionPrompt.Hide();
    }
}
