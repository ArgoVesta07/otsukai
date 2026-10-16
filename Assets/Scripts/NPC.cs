using UnityEngine;
using UnityEngine.InputSystem;

public class NPC : MonoBehaviour
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
        if (!playerNear) return;

        // Jika sedang mengunci gerakan saat menerima misi,
        // tekan E lagi untuk keluar dari mode interaksi.
        if (interactingWithMe)
        {
            interactingWithMe = false;
            InteractionState.IsInteracting = false;

            Debug.Log("Keluar mode interaksi");
            return;
        }

        // Jangan memulai interaksi jika player sedang
        // terkunci oleh sistem interaksi lain.
        if (InteractionState.IsInteracting) return;

        TalkToMom();
    }

    void TalkToMom()
    {
        if (questManager == null)
        {
            Debug.LogWarning("QuestManager tidak ditemukan!");
            return;
        }

        switch (questManager.currentStatus)
        {
            case QuestManager.QuestStatus.NotStarted:

                // Kunci gerakan hanya ketika menerima misi pertama.
                interactingWithMe = true;
                InteractionState.IsInteracting = true;

                questManager.AcceptQuest();
                break;

            case QuestManager.QuestStatus.Accepted:

                // Misi sedang berjalan, jadi tidak mengunci gerakan.
                Debug.Log("Mom: Jangan lupa beli telurnya, ya!");
                break;

            case QuestManager.QuestStatus.HasEggs:

                // Player sudah membawa telur.
                // Tidak mengunci gerakan saat menyerahkannya.
                questManager.CompleteQuest();
                break;

            case QuestManager.QuestStatus.Completed:

                // Misi selesai, interaksi biasa.
                Debug.Log("Mom: Terima kasih, ya!");
                break;
        }
    }
}