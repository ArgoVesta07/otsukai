using UnityEngine;
using UnityEngine.InputSystem;

public class Supermarket : MonoBehaviour
{
    public Vector2 promptOffset = new Vector2(0f, 1.2f); // seberapa jauh di atas objek
    public string promptText = "[E] Ambil telur";   // BARU

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
            Debug.Log("Kamu berada di dekat supermarket.");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
        {
            playerNear = false;
            InteractionPrompt.Hide();   // BARU
        }
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null) return;
        if (!playerNear) return;
        if (!keyboard.eKey.wasPressedThisFrame) return;

        InteractionPrompt.Hide();   // BARU
        InteractWithSupermarket();
    }

    void InteractWithSupermarket()
    {
        if (questManager == null)
        {
            Debug.LogWarning("QuestManager tidak ditemukan!");
            return;
        }

        switch (questManager.currentStatus)
        {
            case QuestManager.QuestStatus.NotStarted:
                Debug.Log("Supermarket: Kamu belum menerima tugas dari Mom.");
                break;

            case QuestManager.QuestStatus.Accepted:
                questManager.GetEggs();
                break;

            case QuestManager.QuestStatus.HasEggs:
                Debug.Log("Supermarket: Kamu sudah mengambil telur. Sekarang pulang!");
                break;

            case QuestManager.QuestStatus.Completed:
                Debug.Log("Supermarket: Misi sudah selesai.");
                break;
        }
    }
}