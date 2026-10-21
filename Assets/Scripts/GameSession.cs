using UnityEngine;

// Ends the running game so the main menu starts clean: destroys every object kept alive
// with DontDestroyOnLoad (managers, HUD, dialogue, loading screen, pause menu) and resets static state.
public static class GameSession
{
    public static void End()
    {
        Time.timeScale = 1f;
        InteractionState.IsInteracting = false;
        GameFlags.Clear();
        IncidentManager.Clear();
        SaveSystem.ForgetLastSave();

        // Objects marked DontDestroyOnLoad live in a hidden scene. A temporary object put there
        // lets us find all of them. Destroy happens at the end of the frame.
        GameObject probe = new GameObject("DontDestroyOnLoad probe");
        Object.DontDestroyOnLoad(probe);

        foreach (GameObject root in probe.scene.GetRootGameObjects())
            Object.Destroy(root);   // includes the probe itself
    }
}