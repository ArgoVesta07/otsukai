using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    public enum QuestStatus
    {
        NotStarted,
        Accepted,
        HasEggs,
        Completed
    }

    public QuestStatus currentStatus = QuestStatus.NotStarted;

    private InventoryManager inventory;

    void Awake()
    {
        // If a QuestManager already exists, destroy the duplicate from the reloaded scene.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        inventory = GetComponent<InventoryManager>();
    }

    public void AcceptQuest()
    {
        if (currentStatus != QuestStatus.NotStarted)
            return;

        currentStatus = QuestStatus.Accepted;

        Debug.Log("Mom: Please buy eggs at the supermarket!");
    }

    public void GetEggs()
    {
        if (currentStatus != QuestStatus.Accepted)
            return;

        if (inventory == null)
        {
            Debug.LogWarning(
                "InventoryManager was not found! Make sure it is attached to the QuestManager GameObject."
            );
            return;
        }

        inventory.AddItem("Egg", 1);

        currentStatus = QuestStatus.HasEggs;

        Debug.Log("You got the eggs! Now go back to Mom.");
    }

    public void CompleteQuest()
    {
        if (currentStatus != QuestStatus.HasEggs)
            return;

        if (inventory == null || !inventory.HasItem("Egg", 1))
        {
            Debug.Log("Mom: You haven't brought the eggs!");
            return;
        }

        inventory.RemoveItem("Egg", 1);

        currentStatus = QuestStatus.Completed;

        Debug.Log("Mom: Thank you for buying the eggs!");
        Debug.Log("MISSION COMPLETE!");
    }
}