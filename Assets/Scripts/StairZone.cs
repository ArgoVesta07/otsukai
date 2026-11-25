using UnityEngine;

// Stairs (e.g. up to the school hill): the player walks slower while inside this zone.
// Needs a Collider2D with Is Trigger ticked covering the steps.
public class StairZone : MonoBehaviour
{
    [Tooltip("Player speed on the stairs, as a share of normal speed (0.6 = 60%).")]
    [SerializeField, Range(0.1f, 1f)] float speedFactor = 0.6f;

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null) player.SetSpeedFactor(this, speedFactor);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null) player.ClearSpeedFactor(this);
    }

    void OnDisable()
    {
        GameObject p = PlayerMovement.FindPlayer();
        if (p != null) p.GetComponent<PlayerMovement>().ClearSpeedFactor(this);
    }
}
