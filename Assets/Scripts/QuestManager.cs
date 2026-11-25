using System;
using System.Collections.Generic;
using UnityEngine;

public enum ErrandState
{
    NotStarted,
    Active,
    Completed,
    Failed,
    Unavailable   // not part of the current story round, so it can't be started now
}

// An errand that is currently running
public class ActiveErrand
{
    public ErrandData data;
    public int stepIndex;
    public float timeLimit;      // seconds this errand got when it started (0 = no timer)
    public float timeLeft;
    public string deadlineLabel; // optional text for the HUD, e.g. "by 10:30" (challenge mode)
    public string giverId;       // NPC that gave this errand (classic mode). Steps with Target Id "Giver" go there.
    public string giverName;     // that NPC's name, for "{giver}" in errand texts
    public int startedDay;       // WorldClock day it started (0 = no clock running)

    public bool HasTimer => timeLimit > 0f;
    public bool IsOtsukai => data != null && data.kind == ErrandKind.Otsukai;
    public ErrandStep CurrentStep =>
        data.steps != null && stepIndex < data.steps.Length ? data.steps[stepIndex] : null;

    // Target and arrow id of the current step, with "Giver" replaced by the real NPC id
    public string CurrentTargetId => CurrentStep != null ? Resolve(CurrentStep.targetId) : null;
    public string CurrentNavId => CurrentStep != null ? Resolve(CurrentStep.NavId) : null;

    // Who walks along (Follower id), with "Giver" replaced by the real NPC id. Null = nobody.
    public string CompanionId => string.IsNullOrEmpty(data.companionId) ? null : Resolve(data.companionId);

    string Resolve(string id)
    {
        return id == QuestManager.GiverTarget && !string.IsNullOrEmpty(giverId) ? giverId : id;
    }

    // Puts the giver's name into a (translated) text: "Bring it back to {giver}"
    public string Fill(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{giver}")) return text;
        return text.Replace("{giver}", string.IsNullOrEmpty(giverName) ? "" : Loc.T(giverName));
    }
}

// Runs errands from ErrandData assets. Knows nothing about eggs or Mom: all of that lives in the assets.
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    // Step Target Id meaning "the NPC who gave this errand" (classic mode)
    public const string GiverTarget = "Giver";

    [Tooltip("Stop errand timers while the player is locked (dialogue box, loading screen).")]
    [SerializeField] bool pauseTimerWhileInteracting = true;

    [Tooltip("Every ErrandData asset in the game. Needed to restore running errands from a save.")]
    [SerializeField] ErrandData[] errandLibrary;

    readonly List<ActiveErrand> active = new List<ActiveErrand>();
    readonly HashSet<string> completed = new HashSet<string>();
    readonly HashSet<string> failed = new HashSet<string>();
    readonly Dictionary<string, int> lastStartedDay = new Dictionary<string, int>();   // for Cooldown Days

    // Story mode: only these errands can be started this round. Off when no story is running.
    readonly HashSet<string> available = new HashSet<string>();
    bool restrictToAvailable;

    InventoryManager inventory;

    // The errand shown on the HUD with the arrow. Change it with CycleTracked().
    // Kept as a reference (not an index) so it doesn't jump to another errand when one ends.
    public ActiveErrand Tracked
    {
        get
        {
            if (active.Count == 0) return null;
            if (tracked == null || !active.Contains(tracked)) tracked = active[0];
            return tracked;
        }
    }

    ActiveErrand tracked;

    // Fired when an errand finishes (classic mode uses these for score and strikes)
    public event Action<ActiveErrand> ErrandCompleted;
    public event Action<ActiveErrand> ErrandFailed;

    // Switch which active errand the HUD card and arrow follow
    public void CycleTracked()
    {
        if (active.Count <= 1) return;
        int index = active.IndexOf(Tracked);
        tracked = active[(index + 1) % active.Count];
    }

    // The last errand that failed, so the HUD can show "Time's up". Cleared when a new errand starts.
    public ErrandData LastFailed { get; private set; }

    public IReadOnlyList<ActiveErrand> Active => active;
    public ErrandData[] ErrandLibrary => errandLibrary;
    public InventoryManager Inventory => inventory;

    void Awake()
    {
        // If a QuestManager already exists, destroy the duplicate from the reloaded scene.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        inventory = GetComponent<InventoryManager>();
        if (inventory == null)
            Debug.LogWarning("InventoryManager was not found! Attach it to the QuestManager GameObject.");
    }

    void Update()
    {
        if (pauseTimerWhileInteracting && InteractionState.IsInteracting) return;

        // Loop backwards because Fail() removes errands from the list
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ActiveErrand errand = active[i];
            if (!errand.HasTimer) continue;

            errand.timeLeft -= Time.deltaTime;

            if (errand.timeLeft <= 0f)
            {
                errand.timeLeft = 0f;
                Fail(errand);
            }
        }

        // Shopping steps without a target finish by themselves once everything is in the bag
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ActiveErrand errand = active[i];
            ErrandStep step = errand.CurrentStep;
            if (step == null || !step.IsShopping || !string.IsNullOrEmpty(step.targetId)) continue;
            if (!HasItemsFor(errand)) continue;   // the items stay in the bag for the next step

            Debug.Log("Errand '" + errand.data.errandId + "' shopping done: " + step.shortText);
            errand.stepIndex++;
            if (errand.stepIndex >= errand.data.steps.Length)
                Complete(errand);
        }
    }

    // ---------- Items ----------
    // Every running errand "holds" the items its current step needs. Items in the bag go to the errands
    // in the order they were started, so one egg can't count for two errands at once.

    int Count(string itemId)
    {
        return inventory != null ? inventory.GetItemCount(itemId) : 0;
    }

    // Items the current step of this errand needs to hold: item id -> amount.
    // A shopping list and the Required Items often list the same things, so the larger amount counts.
    static Dictionary<string, int> Claims(ActiveErrand errand)
    {
        Dictionary<string, int> required = new Dictionary<string, int>();
        ErrandStep step = errand != null ? errand.CurrentStep : null;
        if (step == null) return required;

        if (!string.IsNullOrEmpty(step.requiredItem))
            AddAmount(required, step.requiredItem, step.RequiredAmount);

        if (step.requiredItems != null)
        {
            foreach (ItemAmount item in step.requiredItems)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemId))
                    AddAmount(required, item.itemId, item.Amount);
            }
        }

        if (step.IsShopping)
        {
            Dictionary<string, int> shopping = new Dictionary<string, int>();
            foreach (ItemAmount item in step.shoppingList)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemId))
                    AddAmount(shopping, item.itemId, item.Amount);
            }

            foreach (var pair in shopping)
            {
                required.TryGetValue(pair.Key, out int have);
                required[pair.Key] = Mathf.Max(have, pair.Value);
            }
        }

        return required;
    }

    static void AddAmount(Dictionary<string, int> into, string itemId, int amount)
    {
        into.TryGetValue(itemId, out int have);
        into[itemId] = have + amount;
    }

    static int ClaimOf(ActiveErrand errand, string itemId)
    {
        return Claims(errand).TryGetValue(itemId, out int amount) ? amount : 0;
    }

    // How many of this item are held by the running errands started before this one (all of them if null)
    int ClaimedBefore(ActiveErrand errand, string itemId)
    {
        int claimed = 0;
        foreach (ActiveErrand other in active)
        {
            if (other == errand) break;
            claimed += ClaimOf(other, itemId);
        }
        return claimed;
    }

    // How many of this item in the bag belong to this errand (for the shopping memo)
    public int HeldFor(ActiveErrand errand, string itemId)
    {
        if (errand == null || string.IsNullOrEmpty(itemId)) return 0;
        return Mathf.Max(0, Count(itemId) - ClaimedBefore(errand, itemId));
    }

    // How many more of this item the running shopping lists still need (0 = none)
    public int ShoppingNeed(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;

        bool onAList = false;
        int need = 0;
        foreach (ActiveErrand errand in active)
        {
            ErrandStep step = errand.CurrentStep;
            if (step == null) continue;

            int claim = ClaimOf(errand, itemId);
            need += claim;   // errands on their way back hold their items too
            if (step.IsShopping && claim > 0) onAList = true;
        }

        if (!onAList) return 0;
        return Mathf.Max(0, need - Count(itemId));
    }

    // Removes up to this many, but never items another running errand still holds
    void RemoveUnclaimed(string itemId, int amount)
    {
        if (inventory == null || string.IsNullOrEmpty(itemId)) return;

        int free = Count(itemId) - ClaimedBefore(null, itemId);
        int remove = Mathf.Min(amount, free);
        if (remove > 0) inventory.RemoveItem(itemId, remove);
    }

    // ---------- Starting ----------

    public bool StartErrand(ErrandData data)
    {
        return StartErrand(data, -1f, null, false);
    }

    // timeLimitOverride < 0 = use the asset's Time Limit. ignoreRound = start even if it isn't part of
    // the current story round (classic mode hands out errands itself).
    public bool StartErrand(ErrandData data, float timeLimitOverride, string deadlineLabel, bool ignoreRound)
    {
        return StartErrand(data, timeLimitOverride, deadlineLabel, ignoreRound, null, null);
    }

    // giverId / giverName: the NPC that handed it out (classic mode), used by steps that target "Giver"
    public bool StartErrand(ErrandData data, float timeLimitOverride, string deadlineLabel, bool ignoreRound,
                            string giverId, string giverName)
    {
        if (data == null)
        {
            Debug.LogWarning("StartErrand: no ErrandData assigned.");
            return false;
        }

        ErrandState state = GetState(data.errandId);
        if (state == ErrandState.Active || state == ErrandState.Completed)
            return false;

        if (state == ErrandState.Unavailable && !ignoreRound)
        {
            Debug.Log("Errand '" + data.errandId + "' isn't part of this round.");
            return false;
        }

        if (data.steps == null || data.steps.Length == 0)
        {
            Debug.LogWarning("Errand '" + data.errandId + "' has no steps.");
            return false;
        }

        failed.Remove(data.errandId);   // a failed errand can be retried
        LastFailed = null;

        float limit = timeLimitOverride >= 0f ? timeLimitOverride : data.timeLimit;
        int day = WorldClock.IsActive ? WorldClock.Instance.Day : 0;

        active.Add(new ActiveErrand
        {
            data = data,
            stepIndex = 0,
            timeLimit = limit,
            timeLeft = limit,
            deadlineLabel = deadlineLabel,
            giverId = giverId,
            giverName = giverName,
            startedDay = day
        });

        if (day > 0) lastStartedDay[data.errandId] = day;

        if (data.startItems != null && inventory != null)
        {
            foreach (ItemAmount item in data.startItems)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemId))
                    inventory.AddItem(item.itemId, item.Amount);
            }
        }

        // An otsukai becomes the errand the HUD and arrow follow
        if (data.kind == ErrandKind.Otsukai)
            tracked = active[active.Count - 1];

        Debug.Log("Errand started: " + data.errandId);
        return true;
    }

    // ---------- Progress ----------

    // Called by ErrandSpots and NPCs when the player interacts with them.
    // Returns true if a step was completed.
    public bool TryCompleteStep(string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return false;

        // Copy: Complete() changes the list
        foreach (ActiveErrand errand in new List<ActiveErrand>(active))
        {
            ErrandStep step = errand.CurrentStep;
            if (step == null || errand.CurrentTargetId != targetId) continue;

            // Check the required items (and the shopping list, if this step has one and a target).
            // Missing items here: another errand waiting at the same target may still be ready.
            if (!HasItemsFor(errand))
            {
                Debug.Log("Errand '" + errand.data.errandId + "': missing items for " + step.shortText + ".");
                continue;
            }

            // Uses up everything this step holds (required items, and the shopping list if it has a target)
            if (step.consumeRequiredItem && inventory != null)
            {
                foreach (var pair in Claims(errand))
                    inventory.RemoveItem(pair.Key, pair.Value);
            }

            // Give the reward item
            if (!string.IsNullOrEmpty(step.rewardItem) && inventory != null)
                inventory.AddItem(step.rewardItem, step.rewardAmount);

            errand.stepIndex++;
            Debug.Log("Errand '" + errand.data.errandId + "' step done: " + step.shortText);

            if (errand.stepIndex >= errand.data.steps.Length)
                Complete(errand);

            return true;   // one step per interaction
        }

        return false;
    }

    // True if any active errand is waiting for this target right now
    public bool IsCurrentTarget(string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return false;

        return FindWaitingFor(targetId) != null;
    }

    // The first active errand whose current step happens at this target, or null
    public ActiveErrand FindWaitingFor(string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return null;

        // Prefer one the player already has the items for, so the NPC doesn't say "you're missing something"
        ActiveErrand first = null;
        foreach (ActiveErrand errand in active)
        {
            if (errand.CurrentStep == null || errand.CurrentTargetId != targetId) continue;
            if (HasItemsFor(errand)) return errand;
            if (first == null) first = errand;
        }

        return first;
    }

    // True if the player carries what the current step of this errand needs
    public bool HasItemsFor(ActiveErrand errand)
    {
        if (errand == null || errand.CurrentStep == null) return true;

        foreach (var pair in Claims(errand))
        {
            if (HeldFor(errand, pair.Key) < pair.Value) return false;
        }

        return true;
    }

    // ---------- Queries ----------

    // Classic / challenge: may an NPC hand this out now? (Earliest / Latest Hour and Cooldown Days)
    public bool CanGiveNow(ErrandData data)
    {
        if (data == null) return false;

        WorldClock clock = WorldClock.Instance;
        if (clock == null || !WorldClock.IsActive) return true;
        if (!data.FitsHour(clock.Clock)) return false;

        return data.cooldownDays <= 0 ||
               !lastStartedDay.TryGetValue(data.errandId, out int last) ||
               clock.Day - last > data.cooldownDays;
    }

    // True while a running errand needs this Follower to walk along
    public bool IsCompanion(string followerId)
    {
        if (string.IsNullOrEmpty(followerId)) return false;

        foreach (ActiveErrand errand in active)
        {
            if (errand.CompanionId == followerId) return true;
        }
        return false;
    }

    public ErrandState GetState(string errandId)
    {
        if (FindActive(errandId) != null) return ErrandState.Active;
        if (completed.Contains(errandId)) return ErrandState.Completed;
        if (failed.Contains(errandId)) return ErrandState.Failed;
        if (restrictToAvailable && !available.Contains(errandId)) return ErrandState.Unavailable;
        return ErrandState.NotStarted;
    }

    // Current step index of an active errand, or -1 if it isn't active
    public int GetStep(string errandId)
    {
        ActiveErrand errand = FindActive(errandId);
        return errand != null ? errand.stepIndex : -1;
    }

    ActiveErrand FindActive(string errandId)
    {
        foreach (ActiveErrand errand in active)
        {
            if (errand.data.errandId == errandId) return errand;
        }

        return null;
    }

    // ---------- Ending ----------

    void Complete(ActiveErrand errand)
    {
        active.Remove(errand);
        completed.Add(errand.data.errandId);
        SetFlags(errand.data.flagsOnComplete);

        Debug.Log("ERRAND COMPLETE: " + errand.data.errandId);
        ErrandCompleted?.Invoke(errand);
    }

    void Fail(ActiveErrand errand)
    {
        active.Remove(errand);
        failed.Add(errand.data.errandId);
        SetFlags(errand.data.flagsOnFail);
        LastFailed = errand.data;

        if (errand.data.takeBackRewardsOnFail)
            TakeBackRewards(errand);

        Debug.Log("ERRAND FAILED: " + errand.data.errandId + " (time ran out)");
        ErrandFailed?.Invoke(errand);
    }

    // Removes the items this errand gave the player in the steps already done,
    // so a retry starts clean and leftovers can't be used for other errands.
    void TakeBackRewards(ActiveErrand errand)
    {
        if (inventory == null || errand.data.steps == null) return;

        int doneSteps = Mathf.Min(errand.stepIndex, errand.data.steps.Length);

        for (int i = 0; i < doneSteps; i++)
        {
            ErrandStep step = errand.data.steps[i];
            if (step == null || string.IsNullOrEmpty(step.rewardItem)) continue;

            // Never more than the player still has, nor items another running errand holds
            RemoveUnclaimed(step.rewardItem, step.rewardAmount);
        }

        // Items handed out when the errand started (whatever wasn't used up yet)
        if (errand.data.startItems != null)
        {
            foreach (ItemAmount item in errand.data.startItems)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemId))
                    RemoveUnclaimed(item.itemId, item.Amount);
            }
        }

        // Shopping lists of the steps done so far (and the current one, partly bought)
        int shoppedSteps = Mathf.Min(errand.stepIndex + 1, errand.data.steps.Length);
        for (int i = 0; i < shoppedSteps; i++)
        {
            ErrandStep step = errand.data.steps[i];
            if (step == null || !step.IsShopping) continue;

            foreach (ItemAmount item in step.shoppingList)
            {
                if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
                RemoveUnclaimed(item.itemId, item.Amount);
            }
        }
    }

    static void SetFlags(string[] flags)
    {
        if (flags == null) return;

        foreach (string flag in flags)
            GameFlags.Set(flag);
    }

    // ---------- Story rounds (used by TimeManager) ----------

    // Limits which errands can be started. Called at the start of every round.
    public void SetAvailableErrands(IEnumerable<string> errandIds)
    {
        available.Clear();
        available.UnionWith(errandIds);
        restrictToAvailable = true;
    }

    // Removes any record of this errand so it can be done again (repeating errands)
    public void ForgetErrand(string errandId)
    {
        ActiveErrand errand = FindActive(errandId);
        if (errand != null) active.Remove(errand);

        completed.Remove(errandId);
        failed.Remove(errandId);
    }

    // Fails an errand that is running or was never started. Completed errands stay completed.
    public void ForceFail(ErrandData data)
    {
        if (data == null) return;

        ActiveErrand errand = FindActive(data.errandId);
        if (errand != null)
        {
            Fail(errand);
            return;
        }

        if (completed.Contains(data.errandId) || failed.Contains(data.errandId)) return;

        failed.Add(data.errandId);
        SetFlags(data.flagsOnFail);
    }

    public void ClearLastFailed()
    {
        LastFailed = null;
    }

    // ---------- Debug ----------

    // Completes the current step without checking items (rewards are still given)
    public void DebugCompleteStep(ActiveErrand errand)
    {
        if (errand == null || !active.Contains(errand)) return;

        ErrandStep step = errand.CurrentStep;
        if (step != null && !string.IsNullOrEmpty(step.rewardItem) && inventory != null)
            inventory.AddItem(step.rewardItem, step.rewardAmount);

        errand.stepIndex++;
        if (errand.stepIndex >= errand.data.steps.Length)
            Complete(errand);
    }

    public void DebugCompleteErrand(ActiveErrand errand)
    {
        if (errand == null || !active.Contains(errand)) return;
        Complete(errand);
    }

    // Starts an errand even if it isn't part of the current round
    public void DebugForceStart(ErrandData data)
    {
        if (data == null) return;

        bool wasRestricted = restrictToAvailable;
        restrictToAvailable = false;
        ForgetErrand(data.errandId);
        StartErrand(data);
        restrictToAvailable = wasRestricted;
    }

    public void DebugClearActive()
    {
        active.Clear();
        LastFailed = null;
    }

    // ---------- Save / load ----------

    public ErrandData FindErrandData(string errandId)
    {
        if (errandLibrary == null) return null;

        foreach (ErrandData data in errandLibrary)
        {
            if (data != null && data.errandId == errandId) return data;
        }

        return null;
    }

    public void WriteTo(SaveData save)
    {
        save.completedErrands.AddRange(completed);
        save.failedErrands.AddRange(failed);

        foreach (ActiveErrand errand in active)
        {
            save.activeErrands.Add(new SavedErrand
            {
                errandId = errand.data.errandId,
                stepIndex = errand.stepIndex,
                timeLeft = errand.timeLeft,
                timeLimit = errand.timeLimit,
                deadlineLabel = errand.deadlineLabel,
                giverId = errand.giverId,
                giverName = errand.giverName
            });
        }
    }

    public void ReadFrom(SaveData save)
    {
        ResetState();

        completed.UnionWith(save.completedErrands);
        failed.UnionWith(save.failedErrands);

        foreach (SavedErrand saved in save.activeErrands)
        {
            ErrandData data = FindErrandData(saved.errandId);
            if (data == null)
            {
                Debug.LogWarning("Errand '" + saved.errandId + "' is in the save but not in the Errand Library. Skipped.");
                continue;
            }

            int lastStep = data.steps != null ? data.steps.Length - 1 : 0;

            active.Add(new ActiveErrand
            {
                data = data,
                stepIndex = Mathf.Clamp(saved.stepIndex, 0, Mathf.Max(lastStep, 0)),
                // Saves older than version 3 have no time limit: use the asset's
                timeLimit = saved.timeLimit > 0f ? saved.timeLimit : data.timeLimit,
                timeLeft = saved.timeLeft,
                deadlineLabel = saved.deadlineLabel,
                giverId = saved.giverId,
                giverName = saved.giverName
            });
        }
    }

    // Forgets all errand progress (used by New Game and before loading)
    public void ResetState()
    {
        active.Clear();
        tracked = null;
        lastStartedDay.Clear();
        completed.Clear();
        failed.Clear();
        LastFailed = null;
        available.Clear();
        restrictToAvailable = false;
    }
}