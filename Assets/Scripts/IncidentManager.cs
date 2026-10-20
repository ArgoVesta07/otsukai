using System.Collections.Generic;

// Keeps track of which incidents are happening right now.
// Incident objects (road blocks, delays, ...) call Begin/End; the HUD only reads HasActive.
public static class IncidentManager
{
    static readonly HashSet<string> active = new HashSet<string>();

    public static bool HasActive => active.Count > 0;

    public static void Begin(string incidentId)
    {
        active.Add(incidentId);
    }

    public static void End(string incidentId)
    {
        active.Remove(incidentId);
    }

    public static bool IsActive(string incidentId)
    {
        return active.Contains(incidentId);
    }

    public static void Clear()
    {
        active.Clear();
    }
}
