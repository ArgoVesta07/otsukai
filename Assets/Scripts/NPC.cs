using UnityEngine;
using UnityEngine.InputSystem;

public enum DialogueStyle
{
    Box,      // bottom dialogue box, locks the player
    Bubble    // speech bubble next to the NPC, player can keep moving
}

// What happens once a dialogue has finished. New actions get added at the END of this list.
public enum NpcAction
{
    None,
    StartErrand,        // starts the entry's errand
    CompleteStepHere    // completes the current errand step if it targets this NPC
}

// When a dialogue entry is allowed to play, based on its errand
public enum ErrandCondition
{
    Any,          // ignore the errand
    NotStarted,
    Active,       // any step
    AtStep,       // a specific step (see Step)
    Completed,
    Failed,
    Unavailable   // not part of the current story round
}

[System.Serializable]
public class DialogueEntry
{
    public string note;   // free note for yourself, not used by the game

    [Header("When")]
    public ErrandData errand;
    public ErrandCondition condition = ErrandCondition.Any;
    [Tooltip("Only used with AtStep. 0 = first step.")]
    public int step;
    [Tooltip("Only plays if this flag is set. Empty = ignore.")]
    public string requiredFlag;

    [Header("What")]
    public DialogueStyle style = DialogueStyle.Box;
    [TextArea(2, 4)] public string[] lines;

    [Header("After")]
    public NpcAction afterDialogue = NpcAction.None;
    [Tooltip("Flag set to 1 after this dialogue. Empty = none.")]
    public string flagToSet;
}

public class NPC : MonoBehaviour
{
    [Header("Name and prompt")]
    public string npcName = "NPC";
    [Tooltip("Used by errand steps that target this NPC (e.g. 'Mom').")]
    public string targetId = "NPC";
    public string promptText = "[E] Talk";

    [Header("Dialogue: the first entry whose conditions match is used")]
    public DialogueEntry[] dialogues;
    public float secondsPerBubbleLine = 3f;

    bool playerNear;
    bool inDialogue;     // bottom box is open (player locked)
    bool bubbleActive;   // speech bubble is showing (player free)

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
        {
            playerNear = true;

            if (!bubbleActive)
                InteractionPrompt.Show(promptText, transform);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
        {
            playerNear = false;
            InteractionPrompt.Hide();

            if (bubbleActive)
            {
                bubbleActive = false;
                SpeechBubble.Hide();
            }

            // Safety net: player left while the box was open (e.g. teleported)
            if (inDialogue)
            {
                inDialogue = false;
                InteractionState.IsInteracting = false;
                DialogBox.Cancel();
            }
        }
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;
        if (!k.eKey.wasPressedThisFrame) return;

        if (inDialogue)
        {
            DialogBox.Next();
            return;
        }

        if (!playerNear) return;

        if (bubbleActive)
        {
            SpeechBubble.Skip();
            return;
        }

        if (InteractionState.IsInteracting) return;

        StartTalk();
    }

    DialogueEntry FindEntry()
    {
        if (dialogues == null) return null;

        foreach (DialogueEntry entry in dialogues)
        {
            if (Matches(entry)) return entry;
        }

        return null;
    }

    bool Matches(DialogueEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.requiredFlag) && !GameFlags.Has(entry.requiredFlag))
            return false;

        if (entry.condition == ErrandCondition.Any) return true;

        QuestManager quest = QuestManager.Instance;
        if (quest == null || entry.errand == null) return false;

        ErrandState state = quest.GetState(entry.errand.errandId);

        switch (entry.condition)
        {
            case ErrandCondition.NotStarted: return state == ErrandState.NotStarted;
            case ErrandCondition.Active:     return state == ErrandState.Active;
            case ErrandCondition.AtStep:     return quest.GetStep(entry.errand.errandId) == entry.step;
            case ErrandCondition.Completed:  return state == ErrandState.Completed;
            case ErrandCondition.Failed:     return state == ErrandState.Failed;
            case ErrandCondition.Unavailable: return state == ErrandState.Unavailable;
            default:                         return false;
        }
    }

    void StartTalk()
    {
        DialogueEntry entry = FindEntry();

        if (entry == null)
        {
            Debug.Log(npcName + " has no dialogue that matches right now.");
            return;
        }

        // Nothing to say: still run the entry's action if it has one
        if (entry.lines == null || entry.lines.Length == 0)
        {
            Finish(entry);
            return;
        }

        InteractionPrompt.Hide();

        if (entry.style == DialogueStyle.Bubble)
            StartBubble(entry);
        else
            StartBox(entry);
    }

    void StartBox(DialogueEntry entry)
    {
        inDialogue = true;
        InteractionState.IsInteracting = true;   // locks the player's movement

        DialogBox.Open(npcName, entry.lines, () =>
        {
            inDialogue = false;
            InteractionState.IsInteracting = false;

            if (playerNear)
                InteractionPrompt.Show(promptText, transform);

            Finish(entry);
        });
    }

    void StartBubble(DialogueEntry entry)
    {
        bubbleActive = true;   // the player is NOT locked

        SpeechBubble.Show(transform, entry.lines, secondsPerBubbleLine, () =>
        {
            bubbleActive = false;

            if (playerNear)
                InteractionPrompt.Show(promptText, transform);

            Finish(entry);
        });
    }

    void Finish(DialogueEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.flagToSet))
            GameFlags.Set(entry.flagToSet);

        if (entry.afterDialogue == NpcAction.None) return;

        QuestManager quest = QuestManager.Instance;
        if (quest == null)
        {
            Debug.LogWarning("QuestManager was not found!");
            return;
        }

        switch (entry.afterDialogue)
        {
            case NpcAction.StartErrand:
                quest.StartErrand(entry.errand);
                break;

            case NpcAction.CompleteStepHere:
                quest.TryCompleteStep(targetId);
                break;
        }
    }
}