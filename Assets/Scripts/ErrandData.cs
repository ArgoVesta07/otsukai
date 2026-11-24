using System;
using UnityEngine;
using UnityEngine.Serialization;

// Otsukai = main errand from family (star marker). Request = side errand from people in town ("!").
public enum ErrandKind { Request, Otsukai }

// One errand (e.g. "Buy eggs"), made as an asset: right-click in the Project window >
// Create > Otsukai > Errand. New errands are new assets; no code changes needed.
[CreateAssetMenu(menuName = "Otsukai/Errand", fileName = "NewErrand")]
public class ErrandData : ScriptableObject
{
    [Tooltip("Unique id. Used by saves and flags, so don't change it once the errand is in use.")]
    public string errandId = "NewErrand";

    [Tooltip("Seconds to finish the errand after it starts. 0 = no timer.")]
    public float timeLimit = 180f;

    [Tooltip("Otsukai = main errand from family (star). Request = side errand from people in town (!).")]
    public ErrandKind kind = ErrandKind.Request;

    [Tooltip("Steps are done in order. The errand completes after the last one.")]
    public ErrandStep[] steps;

    [Tooltip("Items handed to the player when the errand starts (e.g. the book to return). Taken back if it fails.")]
    public ItemAmount[] startItems;

    [Tooltip("Who walks along while the errand runs (needs a Follower on them): \"Giver\" = whoever asked, " +
             "or a Follower id such as \"Sibling\" or \"Pet\". Empty = nobody.")]
    public string companionId;

    [Header("Flags")]
    [Tooltip("Flags set to 1 when the errand is completed.")]
    public string[] flagsOnComplete;

    [Tooltip("Flags set to 1 when the errand fails.")]
    public string[] flagsOnFail;

    [Header("On fail")]
    [Tooltip("Take back the reward items this errand already gave the player when it fails.")]
    public bool takeBackRewardsOnFail = true;

    [Header("When it can be given (classic / challenge)")]
    [Tooltip("Earliest hour (0-24) an NPC can hand it out.")]
    public float earliestHour = 0f;

    [Tooltip("Hand it out only before this hour, e.g. 10 = mornings only (taking out the trash). 24 = any time.")]
    public float latestHour = 24f;

    [Tooltip("Days to wait after it was started before it can be given again. 1 = never two days in a row. 0 = any time.")]
    public int cooldownDays = 0;

    [Header("Night")]
    [Tooltip("Classic / challenge: can NPCs give this errand at night? (Errands running when night starts fail.)")]
    public bool allowAtNight = false;

    [Header("Classic mode (NPC errands)")]
    [Tooltip("Can this errand be given in classic mode (otsukai by family, request by town people)? Tip: a step with " +
             "Target Id \"Giver\" sends the player back to whoever asked, and \"{giver}\" in a text becomes their name.")]
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

    // True if the clock time (minutes since midnight) is inside Earliest / Latest Hour
    public bool FitsHour(float clockMinutes)
    {
        float hour = clockMinutes / 60f;
        return hour >= earliestHour && hour < latestHour;
    }
}

// An item and how many of it (shopping lists)
[Serializable]
public class ItemAmount
{
    public string itemId;
    public int amount = 1;

    // 0 or less in the Inspector counts as 1, so a forgotten amount can't make the item free
    public int Amount => Mathf.Max(1, amount);
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

    [Header("Shopping list")]
    [Tooltip("Items to collect for this step, in any order (from ShopItem shelves). With an empty Target Id the step " +
             "is done as soon as everything is in the bag. Shown on the shopping memo.")]
    public ItemAmount[] shoppingList;

    [Tooltip("More items needed to complete this step, besides Required Item (e.g. everything from the shopping list). " +
             "Consume Required Item also applies to these.")]
    public ItemAmount[] requiredItems;

    public bool IsShopping => shoppingList != null && shoppingList.Length > 0;

    // 0 or less in the Inspector counts as 1, so a forgotten amount can't make the item free
    public int RequiredAmount => Mathf.Max(1, requiredAmount);

    public string NavId => string.IsNullOrEmpty(navTargetId) ? targetId : navTargetId;
}