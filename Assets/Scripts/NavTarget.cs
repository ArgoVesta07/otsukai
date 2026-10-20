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

    // Returns the first active target with this id in the loaded scene, or null.
    public static NavTarget Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (NavTarget t in all)
        {
            if (t.targetId == id) return t;
        }

        return null;
    }
}
