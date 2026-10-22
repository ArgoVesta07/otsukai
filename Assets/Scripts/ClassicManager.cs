using System.Collections.Generic;
using UnityEngine;

// Classic mode: endless random days.
// A clock runs from the start hour to the end hour. Errands appear at random with a deadline,
// incidents happen at random, and each day gets harder. A late errand is a strike;
// too many strikes = game over. Only the best score is kept (no save).
// Put this on its own object in the game scene (e.g. next to the TimeManager).
public class ClassicManager : MonoBehaviour
{
    public static ClassicManager Instance;

    [Header("Clock")]
    [SerializeField] int startHour = 8;
    [SerializeField] int endHour = 20;
    [Tooltip("In-game minutes that pass per real second.")]
    [SerializeField] float gameMinutesPerSecond = 2f;
    [SerializeField] bool pauseWhileInteracting = true;

    [Header("Rules")]
    [SerializeField] int strikesAllowed = 3;
    [Tooltip("Bonus points per remaining in-game minute when an errand is finished.")]
    [SerializeField] float bonusPerMinuteLeft = 1f;

    [Header("Errands")]
    [Tooltip("In-game minutes before the first errand of a day.")]
    [SerializeField] float firstErrandAfter = 5f;
    [Tooltip("In-game minutes between new errands on Day 1.")]
    [SerializeField] float errandInterval = 60f;
    [SerializeField] int maxErrandsDay1 = 2;
    [SerializeField] int maxErrandsCap = 4;
    [Tooltip("Every this many days, one more errand can be active at once.")]
    [SerializeField] int extraErrandEveryDays = 2;
    [Tooltip("Deadlines and intervals are multiplied by this for every day after Day 1 (0.9 = 10% shorter).")]
    [SerializeField, Range(0.5f, 1f)] float dailySpeedUp = 0.9f;
    [Tooltip("Deadlines never get shorter than this share of the original.")]
    [SerializeField, Range(0.1f, 1f)] float minDeadlineShare = 0.5f;

    [Header("Incidents")]
    [Tooltip("Incidents classic mode may use (only those with Allow In Classic ticked).")]
    [SerializeField] IncidentData[] incidentPool;
    [Tooltip("In-game minutes between incident rolls.")]
    [SerializeField] float incidentCheckEvery = 30f;
    [SerializeField, Range(0f, 1f)] float incidentChanceDay1 = 0.25f;
    [SerializeField, Range(0f, 1f)] float incidentChancePerDay = 0.1f;
    [SerializeField] int maxIncidents = 2;

    [Header("Text")]
    [GameText] [SerializeField] string dayTitleFormat = "Challenge · Day {0}";
    [GameText] [SerializeField] string dayDoneFormat = "Day {0} survived";
    [GameText] [SerializeField] string daySummaryFormat = "Score {0}   Strikes {1}/{2}";
    [GameText] [SerializeField] string gameOverTitle = "Game over";
    [GameText] [SerializeField] string gameOverFormat = "Score {0} · Reached day {1} · Best {2}";
    [GameText] [SerializeField] string newErrandFormat = "New errand: {0} (by {1})";
    [GameText] [SerializeField] string strikeFormat = "Too late! Strike {0}/{1}";
    [GameText] [SerializeField] string scoreFormat = "+{0}";
    [GameText] [SerializeField] string deadlineFormat = "by {0}";   // shown after the errand name on the HUD
    [SerializeField] float bannerSeconds = 2.5f;
    [SerializeField] float breakSeconds = 3f;

    const string BestScoreKey = "Otsukai_ClassicBestScore";
    const string BestDayKey = "Otsukai_ClassicBestDay";

    readonly Dictionary<string, float> incidentTimeLeft = new Dictionary<string, float>();

    bool running;        // a day is being played
    bool active;         // classic mode is on (includes banners and breaks)
    float clock;         // in-game minutes since midnight
    float nextErrandAt;
    float nextIncidentCheckAt;
    float breakLeft;
    bool onBreak;

    public static bool IsActive => Instance != null && Instance.active;
    public int Day { get; private set; }
    public int Score { get; private set; }
    public int Strikes { get; private set; }
    public int StrikesAllowed => strikesAllowed;
    public bool Running => running;
    public static int BestScore => PlayerPrefs.GetInt(BestScoreKey, 0);
    public static int BestDay => PlayerPrefs.GetInt(BestDayKey, 0);

    public string ClockText => FormatClock(clock);
    public float MinutesLeftInDay => endHour * 60f - clock;

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

    void OnDestroy()
    {
        Unhook();
    }

    // ---------- Start / stop ----------

    public void StartClassic()
    {
        SaveSystem.ResetProgress();   // clear errands, items and flags; classic never saves

        TimeManager time = TimeManager.Instance;
        if (time != null) time.EnterFreeMode();   // make sure story mode is off

        QuestManager quest = QuestManager.Instance;
        if (quest != null)
        {
            quest.SetAvailableErrands(new string[0]);   // NPCs can't hand out errands; this manager does
            Unhook();
            quest.ErrandCompleted += OnErrandCompleted;
            quest.ErrandFailed += OnErrandFailed;
        }

        active = true;
        Day = 1;
        Score = 0;
        Strikes = 0;
        StartDay();
    }

    void StartDay()
    {
        running = false;
        onBreak = false;
        clock = startHour * 60f;
        nextErrandAt = clock + firstErrandAfter;
        nextIncidentCheckAt = clock + incidentCheckEvery;
        EndAllIncidents();

        PeriodBanner.Show(Loc.F(dayTitleFormat, Day), "", bannerSeconds, () =>
        {
            running = true;
        });
    }

    void EndDay()
    {
        running = false;
        EndAllIncidents();

        // Errands still open at closing time count as late
        QuestManager quest = QuestManager.Instance;
        if (quest != null)
        {
            List<ActiveErrand> open = new List<ActiveErrand>(quest.Active);
            foreach (ActiveErrand e in open)
                quest.ForceFail(e.data);   // fires OnErrandFailed -> strikes
        }

        if (!active) return;   // the last strike already ended the run

        string subtitle = Loc.F(daySummaryFormat, Score, Strikes, strikesAllowed);
        PeriodBanner.Show(Loc.F(dayDoneFormat, Day), subtitle, bannerSeconds, () =>
        {
            onBreak = true;
            breakLeft = breakSeconds;
        });
    }

    void GameOver()
    {
        running = false;
        active = false;
        EndAllIncidents();

        QuestManager quest = QuestManager.Instance;
        if (quest != null)
        {
            Unhook();
            quest.DebugClearActive();
        }

        // Keep the best result
        int best = BestScore;
        if (Score > best)
        {
            best = Score;
            PlayerPrefs.SetInt(BestScoreKey, Score);
        }
        if (Day > PlayerPrefs.GetInt(BestDayKey, 0)) PlayerPrefs.SetInt(BestDayKey, Day);
        PlayerPrefs.Save();

        string subtitle = Loc.F(gameOverFormat, Score, Day, best);
        PeriodBanner.Show(Loc.T(gameOverTitle), subtitle, bannerSeconds + 1f, () =>
        {
            TimeManager time = TimeManager.Instance;
            if (time != null) time.EnterFreeMode();
        });
    }

    // Leaves classic mode without a result (e.g. back to the main menu)
    public void Stop()
    {
        running = false;
        active = false;
        onBreak = false;
        EndAllIncidents();
        Unhook();
    }

    void Unhook()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest == null) return;

        quest.ErrandCompleted -= OnErrandCompleted;
        quest.ErrandFailed -= OnErrandFailed;
    }

    // ---------- Update ----------

    void Update()
    {
        if (!active) return;

        if (onBreak)
        {
            breakLeft -= Time.deltaTime;
            if (breakLeft <= 0f)
            {
                onBreak = false;
                Day++;
                StartDay();
            }
            return;
        }

        if (!running) return;
        if (pauseWhileInteracting && InteractionState.IsInteracting) return;

        float dt = Time.deltaTime;
        clock += dt * gameMinutesPerSecond;

        TickIncidents(dt);

        if (clock >= nextErrandAt)
        {
            TrySpawnErrand();
            nextErrandAt = clock + errandInterval * SpeedFactor();
        }

        if (clock >= nextIncidentCheckAt)
        {
            TryStartIncident();
            nextIncidentCheckAt = clock + incidentCheckEvery;
        }

        if (clock >= endHour * 60f)
            EndDay();
    }

    // ---------- Errands ----------

    float SpeedFactor()
    {
        return Mathf.Pow(dailySpeedUp, Day - 1);
    }

    int MaxErrands()
    {
        int extra = extraErrandEveryDays > 0 ? (Day - 1) / extraErrandEveryDays : 0;
        return Mathf.Min(maxErrandsDay1 + extra, maxErrandsCap);
    }

    void TrySpawnErrand()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest == null)
        {
            Debug.LogWarning("Classic: no QuestManager found.");
            return;
        }

        if (quest.Active.Count >= MaxErrands()) return;   // already busy enough

        ErrandData pick = PickErrand(quest);
        if (pick == null)
        {
            Debug.LogWarning("Classic: no errand to hand out. Check the QuestManager's Errand Library, " +
                             "and 'Allow In Classic' / 'Min Day' / 'Weight' on the ErrandData assets.");
            return;
        }

        float minutes = pick.classicDeadlineMinutes * Mathf.Max(SpeedFactor(), minDeadlineShare);
        float seconds = minutes / gameMinutesPerSecond;
        string by = FormatClock(clock + minutes);

        quest.ForgetErrand(pick.errandId);   // the same errand can come back later
        if (quest.StartErrand(pick, seconds, Loc.F(deadlineFormat, by), true))
        {
            string name = pick.steps != null && pick.steps.Length > 0 ? pick.steps[0].shortText : pick.errandId;
            Toast.Show(Loc.F(newErrandFormat, Loc.T(name), by));
            Debug.Log("Classic: new errand " + pick.errandId + " (by " + by + ")");
        }
        else
        {
            Debug.LogWarning("Classic: couldn't start errand " + pick.errandId + ".");
        }
    }

    ErrandData PickErrand(QuestManager quest)
    {
        if (quest.ErrandLibrary == null) return null;

        List<ErrandData> options = new List<ErrandData>();
        float total = 0f;

        foreach (ErrandData e in quest.ErrandLibrary)
        {
            if (e == null || !e.allowInClassic || e.minDay > Day || e.weight <= 0f) continue;
            if (quest.GetState(e.errandId) == ErrandState.Active) continue;   // already running
            options.Add(e);
            total += e.weight;
        }

        if (options.Count == 0) return null;

        float roll = Random.value * total;
        foreach (ErrandData e in options)
        {
            roll -= e.weight;
            if (roll <= 0f) return e;
        }

        return options[options.Count - 1];
    }

    void OnErrandCompleted(ActiveErrand errand)
    {
        if (!active) return;

        float minutesLeft = errand.timeLeft * gameMinutesPerSecond;
        int points = errand.data.classicScore + Mathf.RoundToInt(minutesLeft * bonusPerMinuteLeft);
        Score += points;
        Toast.Show(Loc.F(scoreFormat, points));
    }

    void OnErrandFailed(ActiveErrand errand)
    {
        if (!active) return;

        Strikes++;
        Toast.Show(Loc.F(strikeFormat, Strikes, strikesAllowed));

        if (Strikes >= strikesAllowed)
            GameOver();
    }

    // ---------- Incidents ----------

    void TryStartIncident()
    {
        if (incidentPool == null || incidentTimeLeft.Count >= maxIncidents) return;

        float chance = Mathf.Clamp01(incidentChanceDay1 + incidentChancePerDay * (Day - 1));
        if (Random.value > chance) return;

        List<IncidentData> options = new List<IncidentData>();
        float total = 0f;

        foreach (IncidentData inc in incidentPool)
        {
            if (inc == null || !inc.allowInClassic || inc.minDay > Day || inc.weight <= 0f) continue;
            if (IncidentManager.IsActive(inc.incidentId)) continue;
            options.Add(inc);
            total += inc.weight;
        }

        if (options.Count == 0) return;

        float roll = Random.value * total;
        IncidentData pick = options[options.Count - 1];
        foreach (IncidentData inc in options)
        {
            roll -= inc.weight;
            if (roll <= 0f) { pick = inc; break; }
        }

        IncidentManager.Begin(pick);
        // Duration is in real seconds, like in story mode. 0 = until the day ends.
        incidentTimeLeft[pick.incidentId] = pick.duration > 0f ? pick.duration : float.MaxValue;
    }

    void TickIncidents(float dt)
    {
        if (incidentTimeLeft.Count == 0) return;

        List<string> ids = new List<string>(incidentTimeLeft.Keys);
        foreach (string id in ids)
        {
            float left = incidentTimeLeft[id] - dt;
            if (left <= 0f)
            {
                incidentTimeLeft.Remove(id);
                IncidentManager.End(id);
            }
            else
            {
                incidentTimeLeft[id] = left;
            }
        }
    }

    void EndAllIncidents()
    {
        incidentTimeLeft.Clear();
        IncidentManager.EndAll();
    }

    // ---------- Helpers ----------

    static string FormatClock(float minutes)
    {
        int total = Mathf.FloorToInt(minutes);
        int h = (total / 60) % 24;
        int m = total % 60;
        return h.ToString("00") + ":" + m.ToString("00");
    }

    // Debug: jump the clock forward
    public void DebugAddMinutes(float minutes)
    {
        clock += minutes;
    }
}