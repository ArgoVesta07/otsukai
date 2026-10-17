using UnityEngine;
using UnityEngine.InputSystem;

public class Door : MonoBehaviour
{
    public string sceneToLoad;
    public Vector3 spawnPosition;

    private bool playerNear;

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
        if (Keyboard.current == null) return;
        if (!playerNear) return;
        if (InteractionState.IsInteracting) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (SceneTransition.Instance != null)
            {
                SceneTransition.Instance.GoToScene(
                    sceneToLoad,
                    spawnPosition
                );
            }
            else
            {
                Debug.LogWarning("SceneTransitionManager tidak ditemukan!");
            }
        }
    }
}