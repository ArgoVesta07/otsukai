using System.Collections.Generic;
using UnityEngine;

public enum ErrandState
{
    NotStarted,
    Active,
    Completed,
    Failed
}

// An errand that is currently running
public class ActiveErrand
{
    public ErrandData data;
    public int stepIndex;
    public float timeLeft;

    public bool HasTimer => data.timeLimit > 0f;
    public ErrandStep CurrentStep =>
        data.steps != null && stepIndex < data.steps.Length ? data.steps[stepIndex] : null;
}

// Runs errands from ErrandData assets. Knows nothing about eggs or Mom: all of that lives in the assets.
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    [Tooltip("Stop errand timers while the player is locked (dialogue box, loading screen).")]
    [SerializeField] bool pauseTimerWhileInteracting = true;

    [Tooltip("Every ErrandData asset in the game. Needed to restore running errands from a save.")]
    [SerializeField] ErrandData[] errandLibrary;

    readonly List<ActiveErrand> active = new List<ActiveErrand>();
    readonly HashSet<string> completed = new HashSet<string>();
    readonly HashSet<string> failed = new HashSet<string>();

    InventoryManager inventory;

    // The errand shown on the HUD (the oldest active one)
    public ActiveErrand Tracked => active.Count > 0 ? active[0] : null;

    // The last errand that failed, so the HUD can show "Time's up". Cleared when a new errand starts.
    public ErrandData LastFailed { get; private set; }

    public IReadOnlyList<ActiveErrand> Active => active;
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
        if (data == null)
        {
            Debug.LogWarning("StartErrand: no ErrandData assigned.");
            return false;
        }

        ErrandState state = GetState(data.errandId);
        if (state == ErrandState.Active || state == ErrandState.Completed)
            return false;

        if (data.steps == null || data.steps.Length == 0)
        {
            Debug.LogWarning("Errand '" + data.errandId + "' has no steps.");
            return false;
        }

        failed.Remove(data.errandId);   // a failed errand can be retried
        LastFailed = null;

        active.Add(new ActiveErrand
        {
            data = data,
            stepIndex = 0,
            timeLeft = data.timeLimit
        });

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
            if (step == null || step.targetId != targetId) continue;

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

        foreach (ActiveErrand errand in active)
        {
            ErrandStep step = errand.CurrentStep;
            if (step != null && step.targetId == targetId) return true;
        }

        return false;
    }

    // ---------- Queries ----------

    public ErrandState GetState(string errandId)
    {
        if (FindActive(errandId) != null) return ErrandState.Active;
        if (completed.Contains(errandId)) return ErrandState.Completed;
        if (failed.Contains(errandId)) return ErrandState.Failed;
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
    }
}