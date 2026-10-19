using UnityEngine;
using UnityEngine.InputSystem;

public class NPC : MonoBehaviour
{
   public Vector2 promptOffset = new Vector2(0f, 1.2f);   // seberapa jauh di atas objek
    public string promptText = "[E] Bicara";   // BARU: teks prompt, bisa diganti di Inspector

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
            InteractionPrompt.Hide();   // BARU

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

            Debug.Log("Keluar mode interaksi");
            return;
        }

        if (InteractionState.IsInteracting) return;

        TalkToMom();
    }

    void TalkToMom()
    {
        InteractionPrompt.Hide();   // BARU

        if (questManager == null)
        {
            Debug.LogWarning("QuestManager tidak ditemukan!");
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
                Debug.Log("Mom: Jangan lupa beli telurnya, ya!");
                break;

            case QuestManager.QuestStatus.HasEggs:
                questManager.CompleteQuest();
                break;

            case QuestManager.QuestStatus.Completed:
                Debug.Log("Mom: Terima kasih, ya!");
                break;
        }
    }
}