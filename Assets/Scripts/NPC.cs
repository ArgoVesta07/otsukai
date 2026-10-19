using UnityEngine;
using UnityEngine.InputSystem;

public class NPC : MonoBehaviour
{
   public Vector2 promptOffset = new Vector2(0f, 1.2f);   // how far above the object
    public string promptText = "[E] Talk";   // NEW: prompt text; can be changed in the Inspector

    bool playerNear;
    bool interactingWithMe;

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
            InteractionPrompt.Show(promptText, transform, promptOffset);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
        {
            playerNear = false;
            InteractionPrompt.Hide();   // NEW

            if (interactingWithMe)
            {
                interactingWithMe = false;
                InteractionState.IsInteracting = false;
            }
        }
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;

        if (!k.eKey.wasPressedThisFrame) return;
        if (!playerNear) return;

        if (interactingWithMe)
        {
            interactingWithMe = false;
            InteractionState.IsInteracting = false;

            Debug.Log("Exited interaction mode");
            return;
        }

        if (InteractionState.IsInteracting) return;

        TalkToMom();
    }

    void TalkToMom()
    {
        InteractionPrompt.Hide();   // NEW

        if (questManager == null)
        {
            Debug.LogWarning("QuestManager was not found!");
            return;
        }

        switch (questManager.currentStatus)
        {
            case QuestManager.QuestStatus.NotStarted:
                interactingWithMe = true;
                InteractionState.IsInteracting = true;

                questManager.AcceptQuest();
                break;

            case QuestManager.QuestStatus.Accepted:
                Debug.Log("Mom: Don't forget to buy the eggs!");
                break;

            case QuestManager.QuestStatus.HasEggs:
                questManager.CompleteQuest();
                break;

            case QuestManager.QuestStatus.Completed:
                Debug.Log("Mom: Thank you!");
                break;
        }
    }
}