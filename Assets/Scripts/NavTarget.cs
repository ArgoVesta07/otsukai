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

    // Returns the active target with this id for where the player is, or null. The same id can be in town (a shop
    // door) and inside a room (its counter): inside that room the arrow goes to the counter, anywhere else in town
    // to the door. Order: a target in the player's room, then one in town, then any; the closest of those.
    public static NavTarget Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        PlayerMovement player = PlayerMovement.Current;
        if (player == null) return FindExact(id);

        Vector2 p = player.transform.position;
        RoomArea playerRoom = RoomArea.At(p);
        NavTarget best = null;
        int bestRank = int.MaxValue;
        float bestDistance = float.MaxValue;

        foreach (NavTarget t in all)
        {
            if (t.targetId != id) continue;

            RoomArea room = RoomArea.At(t.transform.position);
            int rank = room == playerRoom ? 0 : room == null ? 1 : 2;
            float d = ((Vector2)t.transform.position - p).sqrMagnitude;
            if (rank < bestRank || (rank == bestRank && d < bestDistance))
            {
                bestRank = rank;
                bestDistance = d;
                best = t;
            }
        }

        return best;
    }
}
