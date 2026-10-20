using System;
using UnityEngine;

// One errand (e.g. "Buy eggs"), made as an asset: right-click in the Project window >
// Create > Otsukai > Errand. New errands are new assets; no code changes needed.
[CreateAssetMenu(menuName = "Otsukai/Errand", fileName = "NewErrand")]
public class ErrandData : ScriptableObject
{
    [Tooltip("Unique id. Used by saves and flags, so don't change it once the errand is in use.")]
    public string errandId = "NewErrand";

    [Tooltip("Seconds to finish the errand after it starts. 0 = no timer.")]
    public float timeLimit = 180f;

    [Tooltip("Steps are done in order. The errand completes after the last one.")]
    public ErrandStep[] steps;

    [Header("Flags")]
    [Tooltip("Flags set to 1 when the errand is completed.")]
    public string[] flagsOnComplete;

    [Tooltip("Flags set to 1 when the errand fails.")]
    public string[] flagsOnFail;
}

[Serializable]
public class ErrandStep
{
    public string note;                          // free note for yourself, not used by the game

    [Header("HUD text")]
    public string shortText = "Do something";
    [TextArea(2, 4)] public string detailText;

    [Header("Where")]
    [Tooltip("Id of the ErrandSpot or NPC that completes this step.")]
    public string targetId;

    [Tooltip("NavTarget id the HUD arrow points to. Empty = same as Target Id.")]
    public string navTargetId;

    [Header("Items")]
    [Tooltip("Item the player must carry to complete this step. Empty = none.")]
    public string requiredItem;
    public int requiredAmount = 1;
    public bool consumeRequiredItem = true;

    [Tooltip("Item given to the player when this step is completed. Empty = none.")]
    public string rewardItem;
    public int rewardAmount = 1;

    public string NavId => string.IsNullOrEmpty(navTargetId) ? targetId : navTargetId;
}
