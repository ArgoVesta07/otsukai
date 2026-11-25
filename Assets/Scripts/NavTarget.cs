using System.Collections.Generic;
using UnityEngine;

// Marks an object the HUD arrow can point to. Give objects the same id in different scenes
// (e.g. "Supermarket" on the shop door outside and on the egg shelf inside).
public class NavTarget : MonoBehaviour
{
    public string targetId = "Supermarket";

    static readonly List<NavTarget> all = new List<NavTarget>();

    void OnEnable()
    {
        all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
    }

    // First active target with this id, wherever the player is (spawn points; their ids are unique)
    public static NavTarget FindExact(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (NavTarget t in all)
            if (t.targetId == id) return t;
        return null;
    }

    // Returns the active target with this id that is closest to the player (the first one without a player), or null.
    // Closest matters when the same id is in town and inside a room: in town the arrow goes to the shop door,
    // inside the shop it goes to the counter.
    public static NavTarget Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        PlayerMovement player = PlayerMovement.Current;
        NavTarget best = null;
        float bestDistance = float.MaxValue;

        foreach (NavTarget t in all)
        {
            if (t.targetId != id) continue;
            if (player == null) return t;

            float d = (t.transform.position - player.transform.position).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = t;
            }
        }

        return best;
    }
}
