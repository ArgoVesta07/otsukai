using UnityEngine;
using UnityEngine.Serialization;
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
    [GameText] [TextArea(2, 4)] public string[] lines;

    [Header("After")]
    public NpcAction afterDialogue = NpcAction.None;
    [Tooltip("Flag set to 1 after this dialogue. Empty = none.")]
    public string flagToSet;
}

public class NPC : MonoBehaviour
{
    [Header("Name and prompt")]
    [GameText] public string npcName = "NPC";
    [Tooltip("Used by errand steps that target this NPC (e.g. 'Mom').")]
    public string targetId = "NPC";
    [GameText] public string promptText = "[E] Talk";

    [Header("Dialogue: the first entry whose conditions match is used")]
    public DialogueEntry[] dialogues;
    public float secondsPerBubbleLine = 3f;

    [Header("Classic & challenge mode (random errands)")]
    [Tooltip("Can this NPC be picked to give random errands in classic and challenge mode?")]
    [FormerlySerializedAs("givesClassicErrands")] public bool givesErrands = true;

    [Tooltip("Shown while this NPC has an errand to give (e.g. a \"!\" above the head). Hidden otherwise.")]
    public GameObject offerMarker;

    [Tooltip("Said when giving an errand whose ErrandData has no Offer Lines.")]
    [GameText] [TextArea(2, 4)] public string[] offerLines = { "Could you help me with something?" };

    [Tooltip("Said when the player finishes an errand step here (e.g. brings the item back).")]
    [GameText] [TextArea(2, 4)] public string[] thanksLines = { "Thank you so much!" };

    [Tooltip("Said when the player comes back without the needed item.")]
    [GameText] [TextArea(2, 4)] public string[] needItemLines = { "Hmm, you don't have it yet." };

    [Tooltip("Said in classic mode when there's nothing to do here. Empty = use the normal dialogues above.")]
    [GameText] [TextArea(2, 4)] public string[] classicIdleLines;

    bool playerNear;
    bool inDialogue;     // bottom box is open (player locked)
    bool bubbleActive;   // speech bubble is showing (player free)

    void OnEnable()
    {
        if (givesErrands)
            NpcRegistry.Register(targetId, npcName);
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
        UpdateOfferMarker();

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

    void UpdateOfferMarker()
    {
        if (offerMarker == null) return;

        bool show = (ClassicManager.IsActive && ClassicManager.Instance.HasOffer(targetId)) ||
                    (ChallengeManager.IsActive && ChallengeManager.Instance.HasOffer(targetId));
        if (offerMarker.activeSelf != show) offerMarker.SetActive(show);
    }

    void StartTalk()
    {
        // Classic / challenge mode: finishing an errand step here, or taking an errand, comes first
        if ((ClassicManager.IsActive || ChallengeManager.IsActive) && TryErrandTalk()) return;

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

        Say(entry.lines, entry.style, () => Finish(entry));
    }

    // Classic and challenge mode. Returns true if the talk was handled here.
    bool TryErrandTalk()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest == null) return false;

        // 1. An errand step happens here (e.g. bringing the eggs back)
        ActiveErrand waiting = quest.FindWaitingFor(targetId);
        if (waiting != null)
        {
            if (!quest.HasItemsFor(waiting))
                Say(needItemLines, DialogueStyle.Bubble, null);
            else
                Say(thanksLines, DialogueStyle.Box, () => quest.TryCompleteStep(targetId));
            return true;
        }

        // 2. Challenge: this NPC has a secret errand. Talking takes it right away.
        ChallengeManager challenge = ChallengeManager.IsActive ? ChallengeManager.Instance : null;
        if (challenge != null && challenge.HasOffer(targetId))
        {
            Say(offerLines, DialogueStyle.Box, () => challenge.AcceptOffer(targetId));
            return true;
        }

        ClassicManager classic = ClassicManager.IsActive ? ClassicManager.Instance : null;
        if (classic == null) return false;

        // 3. Classic: this NPC has an errand to offer (shown on a card: Accept / Later)
        ErrandData offer = classic.OfferFor(targetId);
        if (offer != null)
        {
            if (!classic.CanTakeMore)
            {
                Toast.Show(classic.TooManyErrandsText);
                return true;
            }

            string[] lines = offer.offerLines != null && offer.offerLines.Length > 0 ? offer.offerLines : offerLines;
            Say(lines, DialogueStyle.Box, () =>
            {
                // Show the offer card; without one in the scene, accept right away
                if (!ErrandOfferPanel.Show(offer, npcName, () => classic.AcceptOffer(targetId)))
                    classic.AcceptOffer(targetId);
            });
            return true;
        }

        // 4. Nothing to do: small talk for classic mode, if set
        if (classicIdleLines != null && classicIdleLines.Length > 0)
        {
            Say(classicIdleLines, DialogueStyle.Bubble, null);
            return true;
        }

        return false;
    }

    // Shows lines in the dialogue box or a speech bubble, then runs 'after'
    void Say(string[] lines, DialogueStyle style, System.Action after)
    {
        if (lines == null || lines.Length == 0)
        {
            after?.Invoke();
            return;
        }

        InteractionPrompt.Hide();

        if (style == DialogueStyle.Bubble)
            StartBubble(lines, after);
        else
            StartBox(lines, after);
    }

    void StartBox(string[] lines, System.Action after)
    {
        inDialogue = true;
        InteractionState.IsInteracting = true;   // locks the player's movement

        DialogBox.Open(npcName, lines, () =>
        {
            inDialogue = false;
            InteractionState.IsInteracting = false;

            if (playerNear)
                InteractionPrompt.Show(promptText, transform);

            after?.Invoke();
        });
    }

    void StartBubble(string[] lines, System.Action after)
    {
        bubbleActive = true;   // the player is NOT locked

        SpeechBubble.Show(transform, lines, secondsPerBubbleLine, () =>
        {
            bubbleActive = false;

            if (playerNear)
                InteractionPrompt.Show(promptText, transform);

            after?.Invoke();
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