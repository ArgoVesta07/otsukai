using System.Collections.Generic;
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

    static readonly List<RoomArea> all = new List<RoomArea>();

    Transform exit;

    // The door that leads out of this room (the first Door inside it), for the HUD arrow
    public Transform Exit
    {
        get
        {
            if (exit == null)
            {
                Door d = GetComponentInChildren<Door>();
                if (d != null) exit = d.transform;
            }
            return exit;
        }
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    public bool Contains(Vector2 p)
    {
        Vector2 c = Center, half = size / 2f;
        return p.x >= c.x - half.x && p.x <= c.x + half.x && p.y >= c.y - half.y && p.y <= c.y + half.y;
    }

    // The room at this position, or null when it is in town
    public static RoomArea At(Vector2 p)
    {
        foreach (RoomArea r in all)
            if (r.Contains(p)) return r;
        return null;
    }

    // Camera bounds for whatever is at this position: the room's, or the town's (after loading a save, for example)
    public static void ApplyCamera(Vector2 p)
    {
        CameraFollow cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (cam == null) return;
        RoomArea room = At(p);
        if (room != null) cam.SetBounds(room.Center, room.size);
        else cam.ResetBounds();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(Center, size);
    }
}
