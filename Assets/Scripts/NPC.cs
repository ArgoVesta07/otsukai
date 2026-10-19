using UnityEngine;
using UnityEngine.InputSystem;

public enum DialogueStyle
{
    Box,      // bottom dialogue box, locks the player
    Bubble    // speech bubble above the NPC, player can keep moving
}

public class NPC : MonoBehaviour
{
    [Header("Name and prompt")]
    public string npcName = "Mom";
    public string promptText = "[E] Talk";

    [Header("Dialogue lines, one set per quest state")]
    [TextArea(2, 4)] public string[] linesNotStarted;
    [TextArea(2, 4)] public string[] linesAccepted;
    [TextArea(2, 4)] public string[] linesHasEggs;
    [TextArea(2, 4)] public string[] linesCompleted;

    [Header("Style per quest state")]
    public DialogueStyle styleNotStarted = DialogueStyle.Box;
    public DialogueStyle styleAccepted = DialogueStyle.Bubble;
    public DialogueStyle styleHasEggs = DialogueStyle.Box;
    public DialogueStyle styleCompleted = DialogueStyle.Bubble;
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

    void StartTalk()
    {
        if (questManager == null)
        {
            Debug.LogWarning("QuestManager was not found!");
            return;
        }

        InteractionPrompt.Hide();

        string[] lines;
        DialogueStyle style;
        System.Action afterDialogue = null;   // quest action that runs once the dialogue ends

        switch (questManager.currentStatus)
        {
            case QuestManager.QuestStatus.NotStarted:
                lines = linesNotStarted;
                style = styleNotStarted;
                afterDialogue = questManager.AcceptQuest;
                break;

            case QuestManager.QuestStatus.Accepted:
                lines = linesAccepted;
                style = styleAccepted;
                break;

            case QuestManager.QuestStatus.HasEggs:
                lines = linesHasEggs;
                style = styleHasEggs;
                afterDialogue = questManager.CompleteQuest;
                break;

            default:
                lines = linesCompleted;
                style = styleCompleted;
                break;
        }

        // No text set in the Inspector: skip the dialogue and run the action right away
        if (lines == null || lines.Length == 0)
        {
            afterDialogue?.Invoke();
            return;
        }

        if (style == DialogueStyle.Bubble)
            StartBubble(lines, afterDialogue);
        else
            StartBox(lines, afterDialogue);
    }

    void StartBox(string[] lines, System.Action afterDialogue)
    {
        inDialogue = true;
        InteractionState.IsInteracting = true;   // locks the player's movement

        DialogBox.Open(npcName, lines, () =>
        {
            inDialogue = false;
            InteractionState.IsInteracting = false;

            if (playerNear)
                InteractionPrompt.Show(promptText, transform);

            afterDialogue?.Invoke();
        });
    }

    void StartBubble(string[] lines, System.Action afterDialogue)
    {
        bubbleActive = true;   // the player is NOT locked

        SpeechBubble.Show(transform, lines, secondsPerBubbleLine, () =>
        {
            bubbleActive = false;

            if (playerNear)
                InteractionPrompt.Show(promptText, transform);

            afterDialogue?.Invoke();
        });
    }
}