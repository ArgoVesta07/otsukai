using System;
using System.Collections.Generic;

// Keeps track of which incidents are happening right now.
// TimeManager starts and ends them from the round schedule; IncidentTarget objects in the scene
// react through the Changed event; the HUD shows "!" while HasActive is true.
public static class IncidentManager
{
    // id -> data (data is null for test incidents started by id only)
    static readonly Dictionary<string, IncidentData> active = new Dictionary<string, IncidentData>();

    public static event Action Changed;

    public static bool HasActive => active.Count > 0;

    public static bool IsActive(string incidentId)
    {
        return !string.IsNullOrEmpty(incidentId) && active.ContainsKey(incidentId);
    }

    public static IEnumerable<string> ActiveIds => active.Keys;

    // Starts an incident from its asset (shows its start message)
    public static void Begin(IncidentData data, bool showMessage = true)
    {
        if (data == null || IsActive(data.incidentId)) return;

        active[data.incidentId] = data;
        if (showMessage && !string.IsNullOrEmpty(data.startMessage))
            Toast.Show(Loc.T(data.startMessage));

        Changed?.Invoke();
    }

    // Starts an incident by id only (testing / debug)
    public static void Begin(string incidentId)
    {
        if (string.IsNullOrEmpty(incidentId) || IsActive(incidentId)) return;

        active[incidentId] = null;
        Changed?.Invoke();
    }

    public static void End(string incidentId, bool showMessage = true)
    {
        if (!active.TryGetValue(incidentId, out IncidentData data)) return;

        active.Remove(incidentId);
        if (showMessage && data != null && !string.IsNullOrEmpty(data.endMessage))
            Toast.Show(Loc.T(data.endMessage));

        Changed?.Invoke();
    }

    // Ends everything quietly (round over, jump, new game)
    public static void EndAll()
    {
        if (active.Count == 0) return;

        active.Clear();
        Changed?.Invoke();
    }

    public static void Clear()
    {
        EndAll();
    }
}
