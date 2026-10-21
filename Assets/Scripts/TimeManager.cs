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
    [SerializeField] string storyEndTitle = "The End";
    [SerializeField] float bannerSeconds = 2f;

    readonly List<PeriodResult> results = new List<PeriodResult>();
    bool restoredFromSave;
    bool transitioning;   // a banner is showing between rounds

    public int DayIndex { get; private set; }
    public int PeriodIndex { get; private set; }
    public float TimeLeft { get; private set; }
    public bool Running { get; private set; }
    public bool StoryFinished { get; private set; }

    public int DayNumber => DayIndex + 1;
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
        if (!Running || transitioning) return;

        PeriodData period = CurrentPeriod;
        if (period == null) return;

        if (AllErrandsDone(period))
        {
            EndPeriod();
            return;
        }

        if (period.timeLimit <= 0f) return;
        if (pauseWhileInteracting && InteractionState.IsInteracting) return;

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

        PeriodData period = CurrentPeriod;
        PeriodResult result = MakeResult(period);
        results.Add(result);
        GameFlags.Set("D" + result.day + "_" + result.period, (int)result.rating);

        string subtitle = string.Format(resultFormat,
            RatingName(result.rating), result.completed, result.total);

        Debug.Log("Round over: " + CurrentLabel + " -> " + result.rating);

        PeriodBanner.Show(CurrentLabel, subtitle, bannerSeconds, () =>
        {
            transitioning = false;
            NextPeriod();
        });
    }

    void NextPeriod()
    {
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
        if (total == 0 || completed == total)
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

    // ---------- Save / load ----------

    public void WriteTo(SaveData save)
    {
        save.dayIndex = DayIndex;
        save.periodIndex = PeriodIndex;
        save.periodTimeLeft = TimeLeft;
        save.storyFinished = StoryFinished;
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
        StoryFinished = save.storyFinished;

        results.Clear();
        results.AddRange(save.periodResults);

        PeriodData period = CurrentPeriod;
        Running = period != null && !StoryFinished;

        if (period != null) ApplyAvailability(period);
    }

    // Used by New Game
    public void ResetState()
    {
        results.Clear();
        Running = false;
        StoryFinished = false;
        transitioning = false;
        restoredFromSave = false;
        DayIndex = 0;
        PeriodIndex = 0;
        TimeLeft = 0f;
    }
}
