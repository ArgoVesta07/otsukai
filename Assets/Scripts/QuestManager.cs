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

        currentStatus = QuestStatus.HasEggs;
        Debug.Log("Kamu mendapatkan telur! Sekarang kembali ke Mom.");
    }

    public void CompleteQuest()
    {
        if (currentStatus != QuestStatus.HasEggs)
            return;

        currentStatus = QuestStatus.Completed;
        Debug.Log("Mom: Terima kasih sudah membelikan telur!");
        Debug.Log("MISI SELESAI!");
    }
}