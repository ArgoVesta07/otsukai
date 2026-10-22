using UnityEngine;
using UnityEngine.InputSystem;

public class Door : MonoBehaviour
{
    public string sceneToLoad;
    public Vector3 spawnPosition;
    public Vector2 promptOffset = new Vector2(0f, 1.2f); // how far above the object
    [GameText] public string promptText = "[E] Enter";   // NEW

    private bool playerNear;

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
            InteractionPrompt.Hide();   // NEW
        }
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        if (!playerNear) return;
        if (InteractionState.IsInteracting) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            InteractionPrompt.Hide();   // NEW: required; see the note below

            if (SceneTransition.Instance != null)
            {
                SceneTransition.Instance.GoToScene(
                    sceneToLoad,
                    spawnPosition
                );
            }
            else
            {
                Debug.LogWarning("SceneTransitionManager was not found!");
            }
        }
    }
}