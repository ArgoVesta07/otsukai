using UnityEngine;
using UnityEngine.InputSystem;

public class Door : MonoBehaviour
{
    public string sceneToLoad;
    public Vector3 spawnPosition;
    public Vector2 promptOffset = new Vector2(0f, 1.2f); // seberapa jauh di atas objek
    public string promptText = "[E] Masuk";   // BARU

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
            InteractionPrompt.Hide();   // BARU
        }
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        if (!playerNear) return;
        if (InteractionState.IsInteracting) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            InteractionPrompt.Hide();   // BARU: wajib, lihat catatan di bawah

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