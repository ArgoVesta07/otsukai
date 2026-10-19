using UnityEngine;
using UnityEngine.InputSystem;

public enum DialogueStyle
{
    Box,      // bottom dialogue box, locks the player
    Bubble    // speech bubble next to the NPC, player can keep moving
}

// What happens once a dialogue has finished. Add new actions here later (e.g. start a timer).
public enum NpcAction
{
    None,
    AcceptQuest,
    CompleteQuest
}

[System.Serializable]
public class DialogueEntry
{
    public string note;                                // free note for yourself, not used by the game
    public bool anyQuestStatus = true;                 // ticked = matches whatever the quest status is
    public QuestManager.QuestStatus requiredStatus;    // only used when anyQuestStatus is off
    public DialogueStyle style = DialogueStyle.Box;
    [TextArea(2, 4)] public string[] lines;
    public NpcAction afterDialogue = NpcAction.None;
}

public class NPC : MonoBehaviour
{
    [Header("Name and prompt")]
    public string npcName = "NPC";
    public string promptText = "[E] Talk";

    [Header("Dialogue: the first entry that matches the quest status is used")]
    public DialogueEntry[] dialogues;
    public float secondsPerBubbleLine = 3f;

    bool playerNear;
    bool inDialogue;     // bottom box is open (player locked)
    bool bubbleActive;   // speech bubble is showing (player free)

    QuestManager questManager;

    void Start()
    {
        questManager = QuestManager.Instance;
    }

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
        if (entry.anyQuestStatus) return true;
        if (questManager == null) return false;

        return questManager.currentStatus == entry.requiredStatus;
    }

    void StartTalk()
    {
        DialogueEntry entry = FindEntry();

        if (entry == null)
        {
            Debug.Log(npcName + " has no dialogue for the current quest status.");
            return;
        }

        // Nothing to say: still run the entry's action if it has one
        if (entry.lines == null || entry.lines.Length == 0)
        {
            RunAction(entry.afterDialogue);
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

            RunAction(entry.afterDialogue);
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

            RunAction(entry.afterDialogue);
        });
    }

    void RunAction(NpcAction action)
    {
        if (action == NpcAction.None) return;

        if (questManager == null)
        {
            Debug.LogWarning("QuestManager was not found!");
            return;
        }

        switch (action)
        {
            case NpcAction.AcceptQuest:
                questManager.AcceptQuest();
                break;

            case NpcAction.CompleteQuest:
                questManager.CompleteQuest();
                break;
        }
    }
}