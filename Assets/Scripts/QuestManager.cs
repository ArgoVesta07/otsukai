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

    public bool HasTimer => timeLimit > 0f;
    public bool IsOtsukai => data != null && data.kind == ErrandKind.Otsukai;
    public ErrandStep CurrentStep =>
        data.steps != null && stepIndex < data.steps.Length ? data.steps[stepIndex] : null;

    // Target and arrow id of the current step, with "Giver" replaced by the real NPC id
    public string CurrentTargetId => CurrentStep != null ? Resolve(CurrentStep.targetId) : null;
    public string CurrentNavId => CurrentStep != null ? Resolve(CurrentStep.NavId) : null;

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

    // Story mode: only these errands can be started this round. Off when no story is running.
    readonly HashSet<string> available = new HashSet<string>();
    bool restrictToAvailable;

    InventoryManager inventory;

    // The errand shown on the HUD with the arrow. Change it with CycleTracked().
    public ActiveErrand Tracked
    {
        get
        {
            if (active.Count == 0) return null;
            if (trackedIndex >= active.Count) trackedIndex = 0;
            return active[trackedIndex];
        }
    }

    int trackedIndex;

    // Fired when an errand finishes (classic mode uses these for score and strikes)
    public event Action<ActiveErrand> ErrandCompleted;
    public event Action<ActiveErrand> ErrandFailed;

    // Switch which active errand the HUD card and arrow follow
    public void CycleTracked()
    {
        if (active.Count <= 1) return;
        trackedIndex = (trackedIndex + 1) % active.Count;
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

        active.Add(new ActiveErrand
        {
            data = data,
            stepIndex = 0,
            timeLimit = limit,
            timeLeft = limit,
            deadlineLabel = deadlineLabel,
            giverId = giverId,
            giverName = giverName
        });

        // An otsukai becomes the errand the HUD and arrow follow
        if (data.kind == ErrandKind.Otsukai)
            trackedIndex = active.Count - 1;

        Debug.Log("Errand started: " + data.errandId);
        return true;
    }

    // ---------- Progress ----------

    // Called by ErrandSpots and NPCs when the player interacts with them.
    // Returns true if a step was completed.
    public bool TryCompleteStep(string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return false;

        foreach (ActiveErrand errand in active)
        {
            ErrandStep step = errand.CurrentStep;
            if (step == null || errand.CurrentTargetId != targetId) continue;

            // Check the required item
            if (!string.IsNullOrEmpty(step.requiredItem))
            {
                if (inventory == null || !inventory.HasItem(step.requiredItem, step.requiredAmount))
                {
                    Debug.Log("You need " + step.requiredItem + " x" + step.requiredAmount + ".");
                    return false;
                }

                if (step.consumeRequiredItem)
                    inventory.RemoveItem(step.requiredItem, step.requiredAmount);
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

        foreach (ActiveErrand errand in active)
        {
            if (errand.CurrentStep != null && errand.CurrentTargetId == targetId) return errand;
        }

        return null;
    }

    // True if the player carries what the current step of this errand needs
    public bool HasItemsFor(ActiveErrand errand)
    {
        ErrandStep step = errand != null ? errand.CurrentStep : null;
        if (step == null || string.IsNullOrEmpty(step.requiredItem)) return true;
        return inventory != null && inventory.HasItem(step.requiredItem, step.requiredAmount);
    }

    // ---------- Queries ----------

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
            if (string.IsNullOrEmpty(step.rewardItem)) continue;

            // Never remove more than the player still has (they may have used some already)
            int amount = Mathf.Min(step.rewardAmount, inventory.GetItemCount(step.rewardItem));
            if (amount > 0)
                inventory.RemoveItem(step.rewardItem, amount);
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
                timeLeft = errand.timeLeft
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
                timeLimit = data.timeLimit,
                timeLeft = saved.timeLeft
            });
        }
    }

    // Forgets all errand progress (used by New Game and before loading)
    public void ResetState()
    {
        active.Clear();
        completed.Clear();
        failed.Clear();
        LastFailed = null;
        available.Clear();
        restrictToAvailable = false;
    }
}