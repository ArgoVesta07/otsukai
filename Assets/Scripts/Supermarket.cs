using UnityEngine;
using UnityEngine.InputSystem;

public class Supermarket : MonoBehaviour
{
    bool playerNear;
    bool interactingWithMe;

    QuestManager questManager;

    void Start()
    {
        questManager = FindAnyObjectByType<QuestManager>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
        {
            playerNear = true;
            Debug.Log("Kamu berada di dekat supermarket.");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
        {
            playerNear = false;

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

        if (interactingWithMe)
        {
            interactingWithMe = false;
            InteractionState.IsInteracting = false;

            Debug.Log("Keluar dari supermarket.");
        }
        else if (playerNear && !InteractionState.IsInteracting)
        {
            interactingWithMe = true;
            InteractionState.IsInteracting = true;

            InteractWithSupermarket();
        }
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
                Debug.Log("Mom belum memberikan tugas membeli telur.");
                break;

            case QuestManager.QuestStatus.Accepted:
                questManager.GetEggs();
                break;

            case QuestManager.QuestStatus.HasEggs:
                Debug.Log("Telurnya sudah kamu ambil. Sekarang pulang!");
                break;

            case QuestManager.QuestStatus.Completed:
                Debug.Log("Misi sudah selesai.");
                break;
        }
    }
}