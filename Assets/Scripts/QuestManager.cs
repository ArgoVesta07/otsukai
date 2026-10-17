using UnityEngine;

public class QuestManager : MonoBehaviour
{
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
        inventory = GetComponent<InventoryManager>();
    }

    public void AcceptQuest()
    {
        if (currentStatus != QuestStatus.NotStarted)
            return;

        currentStatus = QuestStatus.Accepted;

        Debug.Log("Mom: Tolong belikan telur di supermarket!");
    }

    public void GetEggs()
    {
        if (currentStatus != QuestStatus.Accepted)
            return;

        if (inventory == null)
        {
            Debug.LogWarning(
                "InventoryManager tidak ditemukan! Pastikan sudah dipasang di GameObject QuestManager."
            );
            return;
        }

        inventory.AddItem("Egg", 1);

        currentStatus = QuestStatus.HasEggs;

        Debug.Log("Kamu mendapatkan telur! Sekarang kembali ke Mom.");
    }

    public void CompleteQuest()
    {
        if (currentStatus != QuestStatus.HasEggs)
            return;

        if (inventory == null || !inventory.HasItem("Egg", 1))
        {
            Debug.Log("Mom: Kamu belum membawa telur!");
            return;
        }

        inventory.RemoveItem("Egg", 1);

        currentStatus = QuestStatus.Completed;

        Debug.Log("Mom: Terima kasih sudah membelikan telur!");
        Debug.Log("MISI SELESAI!");
    }
}