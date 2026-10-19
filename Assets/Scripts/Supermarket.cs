using UnityEngine;
using UnityEngine.InputSystem;

public class Supermarket : MonoBehaviour
{
    public Vector2 promptOffset = new Vector2(0f, 1.2f); // how far above the object
    public string promptText = "[E] Get eggs";   // NEW

    private bool playerNear;
    private QuestManager questManager;

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
        }
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null) return;
        if (!playerNear) return;
        if (!keyboard.eKey.wasPressedThisFrame) return;

        InteractionPrompt.Hide();   // NEW
        InteractWithSupermarket();
    }

    void InteractWithSupermarket()
    {
        if (questManager == null)
        {
            Debug.LogWarning("QuestManager was not found!");
            return;
        }

        switch (questManager.currentStatus)
        {
            case QuestManager.QuestStatus.NotStarted:
                Debug.Log("Supermarket: You haven't received a task from Mom yet.");
                break;

            case QuestManager.QuestStatus.Accepted:
                questManager.GetEggs();
                break;

            case QuestManager.QuestStatus.HasEggs:
                Debug.Log("Supermarket: You've already gotten the eggs. Now go home!");
                break;

            case QuestManager.QuestStatus.Completed:
                Debug.Log("Supermarket: The mission is complete.");
                break;
        }
    }
}