using UnityEngine;
using UnityEngine.InputSystem;

public class NPC : MonoBehaviour
{
    bool playerNear;
    bool interactingWithMe;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
            playerNear = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMovement>() != null)
            playerNear = false;
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;

        if (!k.eKey.wasPressedThisFrame) return;

        if (interactingWithMe)
        {
            // E kedua: keluar mode interaksi
            interactingWithMe = false;
            InteractionState.IsInteracting = false;
            Debug.Log("Keluar mode interaksi");
        }
        else if (playerNear && !InteractionState.IsInteracting)
        {
            // E pertama: masuk mode interaksi
            interactingWithMe = true;
            InteractionState.IsInteracting = true;
            Debug.Log("Masuk mode interaksi");
        }
    }
}