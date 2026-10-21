using System.Collections.Generic;
using UnityEngine;

public enum PeriodRating
{
    None,       // 0 = not played (so GameFlags.Has works)
    Failed,     // no errands done
    Partial,    // some errands done
    Good,       // all errands done
    Perfect     // all errands done with plenty of time left
}

// Runs story mode: Day 1 Morning -> Afternoon -> Night -> Day 2 ...
// Each period is one round with its own timer. The round ends when all its errands are done,
// or when time runs out (the rest count as failed and the story moves on).
// After each round: a short break, then the next round (a result card is optional).
// After the last round of a day: a day recap card.
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;

    [Header("Story")]
    [SerializeField] StoryData story;
    [Tooltip("Start Day 1 automatically when the game scene opens (New Game).")]
    [SerializeField] bool startStoryOnPlay = true;
    [SerializeField] bool autoSaveAtPeriodStart = true;
    [Tooltip("Pause the round timer while the player is locked (dialogue box, loading, banners).")]
    [SerializeField] bool pauseWhileInteracting = true;

    [Header("Rating")]
    [Tooltip("Share of the time limit that must be left for a Perfect rating (0.25 = a quarter).")]
    [SerializeField, Range(0f, 1f)] float perfectTimeShare = 0.25f;

    [Header("Text")]
    [SerializeField] string dayFormat = "Day {0}";
    [SerializeField] string[] periodNames = { "Morning", "Afternoon", "Night" };
    [SerializeField] string[] ratingNames = { "", "Failed", "Partly done", "Good", "Perfect!" };
    [SerializeField] string resultFormat = "{0}  {1}/{2} errands";   // rating, done, total
    [SerializeField] string dayCompleteFormat = "Day {0} complete";   // day number
    [SerializeField] string dayRecapFormat = "{0}/{1} errands";        // done, total
    [SerializeField] string storyEndTitle = "The End";
    [SerializeField] float bannerSeconds = 2f;

    [Tooltip("Show a result card after every round. Off = only the day recap after the last round.")]
    [SerializeField] bool showRoundResults = false;

    [Header("Break")]
    [Tooltip("Free time between rounds (seconds). The timer is stopped and nothing happens.")]
    [SerializeField] float breakSeconds = 5f;

    readonly List<PeriodResult> results = new List<PeriodResult>();
    bool restoredFromSave;
    bool transitioning;   // a banner is showing between rounds
    readonly HashSet<string> debugControlled = new HashSet<string>();   // incidents the debug panel took over
    bool onBreak;         // free time after a round, before the next one starts
    float breakLeft;

    public int DayIndex { get; private set; }
    public int PeriodIndex { get; private set; }
    public float TimeLeft { get; private set; }
    public float RoundElapsed { get; private set; }   // round time passed (pauses like the timer)
    public bool Running { get; private set; }
    public bool StoryFinished { get; private set; }
    public bool OnBreak => onBreak;

    public int DayNumber => DayIndex + 1;
    public StoryData Story => story;

    // Debug panel: freezes the round timer
    [System.NonSerialized] public bool DebugFreezeTimer;
    public bool HasStory => story != null && story.days != null && story.days.Length > 0;
    public IReadOnlyList<PeriodResult> Results => results;

    public PeriodData CurrentPeriod
    {
        get
        {
            if (!HasStory || StoryFinished || DayIndex >= story.days.Length) return null;
            DayData day = story.days[DayIndex];
            if (day == null || day.periods == null || PeriodIndex >= day.periods.Length) return null;
            return day.periods[PeriodIndex];
        }
    }

    // True while a round with a time limit is counting (used by the HUD)
    public bool HasTimer => Running && CurrentPeriod != null && CurrentPeriod.timeLimit > 0f;

    public string CurrentLabel
    {
        get
        {
            PeriodData period = CurrentPeriod;
            if (period == null) return "";
            return string.Format(dayFormat, DayNumber) + " · " + PeriodName(period.period);
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (Instance != this) return;   // duplicate from a reloaded scene, about to be destroyed

        // A loaded save already set everything up (SaveSystem runs before Start)
        if (!restoredFromSave && startStoryOnPlay && HasStory)
            BeginStory();
    }

    void Update()
    {
        if (onBreak)
        {
            breakLeft -= Time.deltaTime;
            if (breakLeft <= 0f)
            {
                onBreak = false;
                NextPeriod();
            }
            return;
        }

        if (!Running || transitioning) return;

        PeriodData period = CurrentPeriod;
        if (period == null) return;

        if (AllErrandsDone(period))
        {
            EndPeriod();
            return;
        }

        if (pauseWhileInteracting && InteractionState.IsInteracting) return;
        if (DebugFreezeTimer) return;

        RoundElapsed += Time.deltaTime;
        UpdateIncidents(period);

        if (period.timeLimit <= 0f) return;

        TimeLeft -= Time.deltaTime;

        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            FailRemaining(period);
            EndPeriod();
        }
    }

    // ---------- Flow ----------

    public void BeginStory()
    {
        results.Clear();
        StoryFinished = false;
        StartPeriod(0, 0);
    }

    void StartPeriod(int dayIndex, int periodIndex)
    {
        DayIndex = dayIndex;
        PeriodIndex = periodIndex;
        Running = false;

        PeriodData period = CurrentPeriod;
        if (period == null)
        {
            Debug.LogWarning("Day " + (dayIndex + 1) + " has no period " + periodIndex + ". Check the DayData asset.");
            return;
        }

        QuestManager quest = QuestManager.Instance;
        if (quest != null)
        {
            quest.ClearLastFailed();

            // Errands that repeat (e.g. buy eggs every morning) start fresh each round
            foreach (PeriodErrand pe in Errands(period))
                quest.ForgetErrand(pe.errand.errandId);

            ApplyAvailability(period);
        }

        TimeLeft = period.timeLimit;
        RoundElapsed = 0f;
        IncidentManager.EndAll();
        debugControlled.Clear();
        transitioning = true;

        PeriodBanner.Show(CurrentLabel, "", bannerSeconds, () =>
        {
            transitioning = false;
            Running = true;

            if (quest != null)
            {
                foreach (PeriodErrand pe in Errands(period))
                {
                    if (pe.autoStart) quest.StartErrand(pe.errand);
                }
            }

            if (autoSaveAtPeriodStart)
            {
                SaveSystem.Save();
                Toast.Show("Saved");
            }

            Debug.Log("Round started: " + CurrentLabel);
        });
    }

    void EndPeriod()
    {
        Running = false;
        transitioning = true;
        IncidentManager.EndAll();   // incidents only last within their round

        PeriodData period = CurrentPeriod;
        PeriodResult result = MakeResult(period);
        results.Add(result);
        if (result.total > 0)
            GameFlags.Set("D" + result.day + "_" + result.period, (int)result.rating);

        Debug.Log("Round over: " + CurrentLabel + " -> " + result.rating);

        string title;
        string subtitle;

        if (IsLastPeriodOfDay())
        {
            // Day recap: errands done across the whole day
            int done = 0;
            int total = 0;
            foreach (PeriodResult r in results)
            {
                if (r.day != DayNumber) continue;
                done += r.completed;
                total += r.total;
            }

            title = string.Format(dayCompleteFormat, DayNumber);
            subtitle = string.Format(dayRecapFormat, done, total);
        }
        else if (!showRoundResults || result.total == 0)
        {
            // No result card (turned off, or a round without errands): go straight to the break
            transitioning = false;
            StartBreak();
            return;
        }
        else
        {
            title = CurrentLabel;
            subtitle = string.Format(resultFormat, RatingName(result.rating), result.completed, result.total);
        }

        PeriodBanner.Show(title, subtitle, bannerSeconds, () =>
        {
            transitioning = false;
            StartBreak();
        });
    }

    void StartBreak()
    {
        if (breakSeconds <= 0f)
        {
            NextPeriod();
            return;
        }

        onBreak = true;
        breakLeft = breakSeconds;
    }

    bool IsLastPeriodOfDay()
    {
        DayData day = story.days[DayIndex];
        return day == null || day.periods == null || PeriodIndex >= day.periods.Length - 1;
    }

    void NextPeriod()
    {
        if (!HasStory) return;

        int day = DayIndex;
        int period = PeriodIndex + 1;

        DayData current = story.days[day];
        if (current == null || current.periods == null || period >= current.periods.Length)
        {
            day++;
            period = 0;
        }

        if (day >= story.days.Length)
        {
            FinishStory();
            return;
        }

        StartPeriod(day, period);
    }

    void FinishStory()
    {
        StoryFinished = true;
        Running = false;
        GameFlags.Set("StoryComplete");

        QuestManager quest = QuestManager.Instance;
        if (quest != null) quest.SetAvailableErrands(new string[0]);

        PeriodBanner.Show(storyEndTitle, "", bannerSeconds, null);
        Debug.Log("Story finished.");
    }

    // ---------- Incidents ----------

    // An incident is active while: startAt <= round time < startAt + duration.
    // Because it only depends on round time, a loaded save brings back the same incidents.
    void UpdateIncidents(PeriodData period)
    {
        if (period.incidents == null) return;

        foreach (ScheduledIncident si in period.incidents)
        {
            if (si == null || si.incident == null) continue;
            if (debugControlled.Contains(si.incident.incidentId)) continue;

            float end = si.incident.duration > 0f ? si.startAt + si.incident.duration : float.MaxValue;
            bool shouldBeActive = RoundElapsed >= si.startAt && RoundElapsed < end;
            bool isActive = IncidentManager.IsActive(si.incident.incidentId);

            if (shouldBeActive && !isActive) IncidentManager.Begin(si.incident);
            else if (!shouldBeActive && isActive) IncidentManager.End(si.incident.incidentId);
        }
    }

    // ---------- Helpers ----------

    bool AllErrandsDone(PeriodData period)
    {
        QuestManager quest = QuestManager.Instance;
        if (quest == null) return false;

        int count = 0;
        foreach (PeriodErrand pe in Errands(period))
        {
            count++;
            ErrandState state = quest.GetState(pe.errand.errandId);
            if (state != ErrandState.Completed && state != ErrandState.Failed) return false;
        }

        return count > 0;   // a round without errands only ends when the timer runs out
    }

    void FailRemaining(PeriodData period)
    {
        QuestManager quest = QuestManager.Instance;
        if (quest == null) return;

        foreach (PeriodErrand pe in Errands(period))
            quest.ForceFail(pe.errand);
    }

    PeriodResult MakeResult(PeriodData period)
    {
        QuestManager quest = QuestManager.Instance;
        int total = 0;
        int completed = 0;

        foreach (PeriodErrand pe in Errands(period))
        {
            total++;
            if (quest != null && quest.GetState(pe.errand.errandId) == ErrandState.Completed)
                completed++;
        }

        PeriodRating rating;
        if (total == 0)
        {
            rating = PeriodRating.None;   // nothing to rate
        }
        else if (completed == total)
        {
            bool plentyOfTime = period.timeLimit <= 0f || TimeLeft >= period.timeLimit * perfectTimeShare;
            rating = plentyOfTime ? PeriodRating.Perfect : PeriodRating.Good;
        }
        else if (completed > 0)
        {
            rating = PeriodRating.Partial;
        }
        else
        {
            rating = PeriodRating.Failed;
        }

        return new PeriodResult
        {
            day = DayNumber,
            period = period.period.ToString(),
            completed = completed,
            total = total,
            timeLeft = TimeLeft,
            rating = rating
        };
    }

    void ApplyAvailability(PeriodData period)
    {
        QuestManager quest = QuestManager.Instance;
        if (quest == null) return;

        List<string> ids = new List<string>();
        foreach (PeriodErrand pe in Errands(period))
            ids.Add(pe.errand.errandId);

        quest.SetAvailableErrands(ids);
    }

    // The period's errands, skipping empty slots
    static IEnumerable<PeriodErrand> Errands(PeriodData period)
    {
        if (period == null || period.errands == null) yield break;

        foreach (PeriodErrand pe in period.errands)
        {
            if (pe != null && pe.errand != null) yield return pe;
        }
    }

    string PeriodName(DayPeriod period)
    {
        int i = (int)period;
        return periodNames != null && i < periodNames.Length ? periodNames[i] : period.ToString();
    }

    string RatingName(PeriodRating rating)
    {
        int i = (int)rating;
        return ratingNames != null && i < ratingNames.Length ? ratingNames[i] : rating.ToString();
    }

    // ---------- Debug ----------

    // Jumps straight to a day and period (0-based). Running errands are dropped.
    public void DebugJumpTo(int dayIndex, int periodIndex)
    {
        if (!HasStory) return;

        onBreak = false;
        transitioning = false;
        Running = false;
        StoryFinished = false;

        QuestManager quest = QuestManager.Instance;
        if (quest != null) quest.DebugClearActive();

        StartPeriod(dayIndex, periodIndex);
    }

    // Starts or ends an incident right now, ignoring the schedule until the round ends
    public void DebugSetIncident(IncidentData incident, bool on)
    {
        if (incident == null) return;

        debugControlled.Add(incident.incidentId);
        if (on) IncidentManager.Begin(incident);
        else IncidentManager.End(incident.incidentId);
    }

    public void DebugSetTimeLeft(float seconds)
    {
        TimeLeft = Mathf.Max(0f, seconds);
    }

    public void DebugEndRoundNow()
    {
        if (!Running) return;
        PeriodData period = CurrentPeriod;
        if (period != null) FailRemaining(period);
        EndPeriod();
    }

    // ---------- Save / load ----------

    public void WriteTo(SaveData save)
    {
        save.dayIndex = DayIndex;
        save.periodIndex = PeriodIndex;
        save.periodTimeLeft = TimeLeft;
        save.storyFinished = StoryFinished;
        save.betweenRounds = onBreak;
        save.roundElapsed = RoundElapsed;
        save.periodResults.AddRange(results);
    }

    public void ReadFrom(SaveData save)
    {
        // Saves from before the day system: start the story fresh instead
        if (save.version < 2) return;

        restoredFromSave = true;
        transitioning = false;

        DayIndex = save.dayIndex;
        PeriodIndex = save.periodIndex;
        TimeLeft = save.periodTimeLeft;
        RoundElapsed = save.roundElapsed;
        IncidentManager.EndAll();   // the schedule restarts the right ones on the next frame
        StoryFinished = save.storyFinished;

        results.Clear();
        results.AddRange(save.periodResults);

        PeriodData period = CurrentPeriod;
        if (period != null) ApplyAvailability(period);

        if (save.betweenRounds && !StoryFinished && HasStory)
        {
            // Saved during the break: this round is already over, continue with the break
            Running = false;
            onBreak = true;
            breakLeft = breakSeconds;
        }
        else
        {
            Running = period != null && !StoryFinished;
            onBreak = false;
        }
    }

    // Used by New Game
    public void ResetState()
    {
        results.Clear();
        Running = false;
        StoryFinished = false;
        transitioning = false;
        onBreak = false;
        restoredFromSave = false;
        DayIndex = 0;
        PeriodIndex = 0;
        TimeLeft = 0f;
    }
}