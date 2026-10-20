using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    public enum QuestStatus
    {
        NotStarted,
        Accepted,
        HasEggs,
        Completed,
        Failed      // added at the end so existing Inspector values stay the same
    }

    public QuestStatus currentStatus = QuestStatus.NotStarted;

    [Header("Timer")]
    [Tooltip("Seconds the player has after accepting the quest.")]
    public float timeLimit = 180f;

    public float TimeLeft { get; private set; }
    public bool TimerRunning { get; private set; }

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

    void Update()
    {
        if (!TimerRunning) return;

        TimeLeft -= Time.deltaTime;

        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            TimerRunning = false;
            FailQuest();
        }
    }

    public void AcceptQuest()
    {
        if (currentStatus != QuestStatus.NotStarted)
            return;

        currentStatus = QuestStatus.Accepted;

        TimeLeft = timeLimit;
        TimerRunning = true;

        Debug.Log("Quest accepted. Timer started: " + timeLimit + " seconds.");
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
        TimerRunning = false;

        Debug.Log("MISSION COMPLETE! Time left: " + Mathf.CeilToInt(TimeLeft) + " seconds.");
    }

    // Called when the timer runs out. Win/lose screens can hook in here later.
    public void FailQuest()
    {
        if (currentStatus != QuestStatus.Accepted && currentStatus != QuestStatus.HasEggs)
            return;

        currentStatus = QuestStatus.Failed;
        TimerRunning = false;

        Debug.Log("MISSION FAILED: time ran out.");
    }
}
