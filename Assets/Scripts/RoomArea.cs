using UnityEngine;

// A room built inside the town scene (far outside the map). While the player is in it, the camera uses
// these bounds instead of the town's. Doors with Stay In Scene look for the RoomArea above their spawn target.
public class RoomArea : MonoBehaviour
{
    [Tooltip("Camera bounds of the room in tiles, walls included.")]
    public Vector2 size = new Vector2(22f, 12f);

    [Tooltip("Centre of the bounds, relative to this object.")]
    public Vector2 centerOffset = Vector2.zero;

    public Vector2 Center => (Vector2)transform.position + centerOffset;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(Center, size);
    }
}
