using UnityEngine;

// Resets static values every time Play starts.
// Needed when Unity's "Enter Play Mode Options" skips the domain reload: without this,
// values like "player locked" or flags can survive from the previous Play session.
public static class StaticReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        InteractionState.IsInteracting = false;
        GameFlags.Clear();
        IncidentManager.Clear();
        Time.timeScale = 1f;
    }
}
