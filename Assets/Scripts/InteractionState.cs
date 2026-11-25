using UnityEngine;

public static class InteractionState
{
    public static bool IsInteracting;

    static int interactUsedFrame = -1;

    // The interact key (E) does one thing per press. Many objects read E on their own (NPCs, doors,
    // shelves, ...); the first one that acts this frame takes the press, the others see false.
    // Call it only right before actually doing something, after all other checks.
    public static bool TryUseInteract()
    {
        if (interactUsedFrame == Time.frameCount) return false;
        interactUsedFrame = Time.frameCount;
        return true;
    }

    // For StaticReset (Play mode without domain reload)
    public static void Reset()
    {
        IsInteracting = false;
        interactUsedFrame = -1;
    }
}
