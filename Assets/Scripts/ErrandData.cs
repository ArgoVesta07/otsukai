using System;
using UnityEngine;
using UnityEngine.Serialization;

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

    [Header("On fail")]
    [Tooltip("Take back the reward items this errand already gave the player when it fails.")]
    public bool takeBackRewardsOnFail = true;

    [Header("Night")]
    [Tooltip("Classic / challenge: can NPCs give this errand at night? (Errands running when night starts fail.)")]
    public bool allowAtNight = false;

    [Header("Classic mode (NPC errands)")]
    [Tooltip("Can NPCs offer this errand in classic mode? Tip: a step with Target Id \"Giver\" sends the player " +
             "back to whoever asked, and \"{giver}\" in a text becomes that NPC's name.")]
    public bool offerInClassic = false;

    [Tooltip("Higher = offered more often in classic mode.")]
    public float classicWeight = 1f;

    [Tooltip("What the NPC says when giving this errand. Empty = the NPC's own Offer Lines.")]
    [GameText] [TextArea(2, 4)] public string[] offerLines;

    [Header("Challenge mode (random days)")]
    [Tooltip("Can the challenge mode hand out this errand?")]
    [FormerlySerializedAs("allowInClassic")] public bool allowInChallenge = true;

    [Tooltip("Higher = picked more often.")]
    public float weight = 1f;

    [Tooltip("Earliest day it may appear in challenge mode.")]
    public int minDay = 1;

    [Tooltip("In-game minutes the player gets on Day 1 (gets shorter on later days).")]
    [FormerlySerializedAs("classicDeadlineMinutes")] public float challengeDeadlineMinutes = 60f;

    [Tooltip("Points for finishing it in challenge mode (plus a bonus for time left).")]
    [FormerlySerializedAs("classicScore")] public int challengeScore = 100;
}

[Serializable]
public class ErrandStep
{
    public string note;                          // free note for yourself, not used by the game

    [Header("HUD text")]
    [GameText] public string shortText = "Do something";
    [GameText] [TextArea(2, 4)] public string detailText;

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