using UnityEngine;
using UnityEngine.InputSystem;

// Takes the player to another scene. Where they appear: the NavTarget named Spawn At Target in that scene
// (e.g. "Entrance" inside a building, or the building's id like "Supermarket" in town) plus Spawn Offset,
// so moving a building or a room needs no new numbers. Spawn Position is only used when no target is set or found.
public class Door : MonoBehaviour
{
    public string sceneToLoad;

    [Tooltip("NavTarget id in the scene being loaded where the player appears, e.g. \"Entrance\" or \"Supermarket\".")]
    public string spawnAtTarget;

    [Tooltip("Added to the target's position, e.g. (0, -1) = one tile in front of a door in town.")]
    public Vector2 spawnOffset = Vector2.zero;

    [Tooltip("Fallback when Spawn At Target is empty or not found in the loaded scene.")]
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

        if (Keyboard.current.eKey.wasPressedThisFrame && InteractionState.TryUseInteract())
        {
            InteractionPrompt.Hide();   // NEW: required; see the note below

            if (SceneTransition.Instance != null)
            {
                SceneTransition.Instance.GoToScene(sceneToLoad, spawnAtTarget, spawnOffset, spawnPosition);
            }
            else
            {
                Debug.LogWarning("SceneTransitionManager was not found!");
            }
        }
    }
}