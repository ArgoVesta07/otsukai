using UnityEngine;
using UnityEngine.InputSystem;

public class Door : MonoBehaviour
{
    bool playerNear;

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
        }
    }

    void Update()
    {
        var keyboard = Keyboard.current;

        if (keyboard == null) return;
        if (!playerNear) return;
        if (InteractionState.IsInteracting) return;

        if (keyboard.eKey.wasPressedThisFrame)
        {
            Debug.Log("Masuk rumah!");
        }
    }
}