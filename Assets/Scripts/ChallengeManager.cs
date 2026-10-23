using System.Collections.Generic;
using UnityEngine;

// Challenge mode: endless random days on the shared WorldClock.
// At the start of every day phase (not night), some random NPCs secretly get an errand ("!" above them).
// The HUD shows how many there are and points to the nearest one. Talking to the NPC starts the errand,
// and its deadline starts counting from then. Errands not picked up before the phase ends, late errands,
// and errands still open when night starts are strikes; too many strikes = game over.
// Each day gets harder (more errands, shorter deadlines). Then the player sleeps for the next day.
// Only the best score is kept (no save).
// Put this on its own object in the game scene (e.g. next to the WorldClock).
public class ChallengeManager : MonoBehaviour
{
    public static ChallengeManager Instance;

    [Header("Clock")]
    [Tooltip("Hour each day starts at.")]
    [SerializeField] int startHour = 8;
    [Tooltip("In-game minutes that pass per real second.")]
    [SerializeField] float gameMinutesPerSecond = 2f;

    [Header("Rules")]
    [SerializeField] int strikesAllowed = 3;
    [Tooltip("Bonus points per remaining in-game minute when an errand is finished.")]
    [SerializeField] float bonusPerMinuteLeft = 1f;

    [Header("Errands")]
    [Tooltip("Errands given out at the start of each day phase on Day 1.")]
    [SerializeField] int errandsPerPhaseDay1 = 2;
    [SerializeField] int errandsPerPhaseCap = 4;
    [Tooltip("Every this many days, one more errand per phase.")]
    [SerializeField] int extraErrandEveryDays = 2;
    [Tooltip("Deadlines are multiplied by this for every day after Day 1 (0.9 = 10% shorter).")]
    [SerializeField, Range(0.5f, 1f)] float dailySpeedUp = 0.9f;
    [Tooltip("Deadlines never get shorter than this share of the original.")]
    [SerializeField, Range(0.1f, 1f)] float minDeadlineShare = 0.5f;

    [Header("Incidents")]
    [Tooltip("Incidents challenge mode may use (only those with Allow In Challenge ticked).")]
    [SerializeField] IncidentData[] incidentPool;
    [Tooltip("In-game minutes between incident rolls.")]
    [SerializeField] float incidentCheckEvery = 30f;
    [SerializeField, Range(0f, 1f)] float incidentChanceDay1 = 0.25f;
    [SerializeField, Range(0f, 1f)] float incidentChancePerDay = 0.1f;
    [SerializeField] int maxIncidents = 2;

    [Header("Text")]
    [Tooltip("Banner on the first day. {0} = day number (optional).")]
    [GameText] [SerializeField] string dayTitleFormat = "Challenge · Day {0}";
    [Tooltip("Banner after sleeping.")]
    [GameText] [SerializeField] string nextDayTitle = "Next day";
    [Tooltip("Small text under the day banner. {0} = time of day (needs Time Of Day). Empty = none.")]
    [GameText] [SerializeField] string dayStartSubtitle = "{0}";
    [Tooltip("Under the banner after sleeping. {0} = score, {1} = strikes, {2} = strikes allowed")]
    [GameText] [SerializeField] string daySummaryFormat = "Score {0}   Strikes {1}/{2}";
    [GameText] [SerializeField] string gameOverTitle = "Game over";
    [GameText] [SerializeField] string gameOverFormat = "Score {0} · Reached day {1} · Best {2}";
    [GameText] [SerializeField] string newErrandFormat = "New errand: {0} (by {1})";
    [GameText] [SerializeField] string strikeFormat = "Too late! Strike {0}/{1}";
    [GameText] [SerializeField] string scoreFormat = "+{0}";
    [GameText] [SerializeField] string deadlineFormat = "by {0}";   // shown after the errand name on the HUD
    [Tooltip("Errand card while no errand is running. {0} = errands waiting at NPCs")]
    [GameText] [SerializeField] string findErrandsFormat = "Find who needs help ({0} left)";
    [Tooltip("Pop-up when errands weren't picked up in time. {0} = how many")]
    [GameText] [SerializeField] string missedErrandsFormat = "{0} errand(s) missed!";
    [SerializeField] float bannerSeconds = 2.5f;

    // Old key names are kept so earlier best scores aren't lost
    const string BestScoreKey = "Otsukai_ClassicBestScore";
    const string BestDayKey = "Otsukai_ClassicBestDay";

    readonly Dictionary<string, float> incidentTimeLeft = new Dictionary<string, float>();

    bool active;         // challenge mode is on
    float nextIncidentCheckAt;   // clock minutes

    readonly Dictionary<string, ErrandData> offers = new Dictionary<string, ErrandData>();   // NPC id -> errand
    readonly HashSet<string> phaseErrands = new HashSet<string>();   // errand ids given out this phase

    static WorldClock Clock => WorldClock.Instance;
    float Now => Clock != null ? Clock.Clock : 0f;

    public static bool IsActive => Instance != null && Instance.active;
    public int Day => Clock != null ? Clock.Day : 1;
    public int Score { get; private set; }
    public int Strikes { get; private set; }
    public int StrikesAllowed => strikesAllowed;
    public int PhaseTotal { get; private set; }   // errands given out this phase
    public int PhaseDone { get; private set; }    // ...and finished
    public int OffersLeft => offers.Count;
    public string FindErrandsText => Loc.F(findErrandsFormat, offers.Count);
    public bool Running => active && WorldClock.IsActive && Clock.Running;
    public static int BestScore => PlayerPrefs.GetInt(BestScoreKey, 0);
    public static int BestDay => PlayerPrefs.GetInt(BestDayKey, 0);

    public float MinutesLeftInDay => Clock != null ? Clock.MinutesUntilNight : 0f;

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

    public void StartChallenge()
    {
        if (Clock == null || Clock.TimeOfDay == null)
        {
            Debug.LogWarning("Challenge: needs a WorldClock with a Time Of Day asset in the scene.");
            return;
        }

        SaveSystem.ResetProgress();   // clear errands, items and flags; challenge never saves

        TimeManager time = TimeManager.Instance;
        if (time != null) time.EnterFreeMode();   // make sure story mode is off

        QuestManager quest = QuestManager.Instance;
        if (quest != null)
            quest.SetAvailableErrands(new string[0]);   // NPCs can't hand out errands; this manager does

        Unhook();
        Hook();

        active = true;
        Score = 0;
        Strikes = 0;
        offers.Clear();
        phaseErrands.Clear();
        PhaseTotal = 0;
        PhaseDone = 0;
        EndAllIncidents();

        float start = startHour * 60f;
        TimeOfDayData.Phase phase = Clock.TimeOfDay.Get(Clock.TimeOfDay.IndexAt(start));
        string phaseName = phase != null ? Loc.T(phase.name) : "";
        string subtitle = string.IsNullOrEmpty(dayStartSubtitle) ? "" : Loc.F(dayStartSubtitle, phaseName);

        Clock.Begin(ClockMode.Challenge, 1, start, startHour, gameMinutesPerSecond,
                    Loc.F(dayTitleFormat, 1), subtitle,
                    () => Loc.T(nextDayTitle),
                    () => Loc.F(daySummaryFormat, Score, Strikes, strikesAllowed));
    }

    void Hook()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest != null)
        {
            quest.ErrandCompleted += OnErrandCompleted;
            quest.ErrandFailed += OnErrandFailed;
        }

        if (Clock != null)
        {
            Clock.DayStarted += OnDayStarted;
            Clock.NightStarted += OnNightStarted;
            Clock.PhaseChanged += OnPhaseChanged;
        }
    }

    void OnDayStarted()
    {
        if (!active) return;

        nextIncidentCheckAt = Now + incidentCheckEvery;
        EndAllIncidents();
    }

    // New phase: errands nobody picked up are strikes, then new errands go to random NPCs (not at night)
    void OnPhaseChanged()
    {
        if (!active) return;

        MissLeftoverOffers();
        if (!active) return;   // the strikes ended the run

        phaseErrands.Clear();
        PhaseTotal = 0;
        PhaseDone = 0;

        if (!Clock.IsNight) GiveOutErrands();
    }

    // Night: no new errands; the ones still open count as late
    void OnNightStarted()
    {
        if (!active) return;

        EndAllIncidents();

        QuestManager quest = QuestManager.Instance;
        if (quest == null) return;

        List<ActiveErrand> open = new List<ActiveErrand>(quest.Active);
        foreach (ActiveErrand e in open)
            quest.ForceFail(e.data);   // fires OnErrandFailed -> strikes (may end the run)
    }

    void GameOver()
    {
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

    // Leaves challenge mode without a result (e.g. back to the main menu)
    public void Stop()
    {
        active = false;
        offers.Clear();
        EndAllIncidents();
        Unhook();
    }

    void Unhook()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest != null)
        {
            quest.ErrandCompleted -= OnErrandCompleted;
            quest.ErrandFailed -= OnErrandFailed;
        }

        if (Clock != null)
        {
            Clock.DayStarted -= OnDayStarted;
            Clock.NightStarted -= OnNightStarted;
            Clock.PhaseChanged -= OnPhaseChanged;
        }
    }

    // ---------- Update ----------

    void Update()
    {
        if (!Running || Clock.IsNight) return;
        if (InteractionState.IsInteracting) return;   // the clock is paused too

        TickIncidents(Time.deltaTime);

        if (Now >= nextIncidentCheckAt)
        {
            TryStartIncident();
            nextIncidentCheckAt = Now + incidentCheckEvery;
        }
    }

    // ---------- Errands ----------

    float SpeedFactor()
    {
        return Mathf.Pow(dailySpeedUp, Day - 1);
    }

    int ErrandsThisPhase()
    {
        int extra = extraErrandEveryDays > 0 ? (Day - 1) / extraErrandEveryDays : 0;
        return Mathf.Min(errandsPerPhaseDay1 + extra, errandsPerPhaseCap);
    }

    // Hands this phase's errands to random free NPCs (secret until the player talks to them)
    void GiveOutErrands()
    {
        offers.Clear();

        QuestManager quest = QuestManager.Instance;
        if (quest == null) return;

        List<string> givers = NpcRegistry.FreeGiversShuffled();
        List<ErrandData> used = new List<ErrandData>();
        int count = Mathf.Min(ErrandsThisPhase(), givers.Count);

        for (int i = 0; i < count; i++)
        {
            ErrandData pick = PickErrand(quest, used);
            if (pick == null) break;

            used.Add(pick);
            offers[givers[i]] = pick;
            phaseErrands.Add(pick.errandId);
        }

        PhaseTotal = offers.Count;

        if (offers.Count > 0)
            Debug.Log("Challenge: " + offers.Count + " errand(s) at " + string.Join(", ", offers.Keys));
        else if (NpcRegistry.Count == 0)
            Debug.LogWarning("Challenge: no NPC can give errands (tick Gives Errands on NPCs).");
        else
            Debug.LogWarning("Challenge: no errand to hand out. Check the QuestManager's Errand Library, " +
                             "and 'Allow In Challenge' / 'Min Day' / 'Weight' on the ErrandData assets.");
    }

    // Offers nobody picked up before the phase ended: one strike each
    void MissLeftoverOffers()
    {
        int missed = offers.Count;
        offers.Clear();
        if (missed == 0) return;

        Toast.Show(Loc.F(missedErrandsFormat, missed));
        for (int i = 0; i < missed && active; i++)
            AddStrike();
    }

    public bool HasOffer(string npcId)
    {
        return active && !string.IsNullOrEmpty(npcId) && offers.ContainsKey(npcId);
    }

    // The player talked to an NPC with an errand: start it now, the deadline counts from here
    public bool AcceptOffer(string npcId)
    {
        QuestManager quest = QuestManager.Instance;
        if (quest == null || !HasOffer(npcId)) return false;

        ErrandData pick = offers[npcId];
        offers.Remove(npcId);

        float minutes = pick.challengeDeadlineMinutes * Mathf.Max(SpeedFactor(), minDeadlineShare);
        float seconds = minutes / gameMinutesPerSecond;
        string by = FormatClock(Now + minutes);

        quest.ForgetErrand(pick.errandId);   // the same errand can come back later
        if (!quest.StartErrand(pick, seconds, Loc.F(deadlineFormat, by), true, npcId, NpcRegistry.NameOf(npcId)))
        {
            Debug.LogWarning("Challenge: couldn't start errand " + pick.errandId + ".");
            return false;
        }

        ActiveErrand started = null;
        foreach (ActiveErrand e in quest.Active)
        {
            if (e.data == pick) started = e;
        }

        string name = started != null && started.CurrentStep != null
            ? started.Fill(Loc.T(started.CurrentStep.shortText))
            : pick.errandId;
        Toast.Show(Loc.F(newErrandFormat, name, by));
        Debug.Log("Challenge: errand " + pick.errandId + " from " + npcId + " (by " + by + ")");
        return true;
    }

    // NavTarget id of the closest NPC with an errand (in this scene), or null
    public string NearestOfferNavId(Vector3 from)
    {
        string best = null;
        float bestDistance = float.MaxValue;

        foreach (string id in offers.Keys)
        {
            NavTarget target = NavTarget.Find(id);
            if (target == null) continue;

            float d = (target.transform.position - from).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = id;
            }
        }

        // None in this scene (e.g. inside a house): any id, so the arrow falls back to the exit
        if (best == null)
        {
            foreach (string id in offers.Keys) return id;
        }

        return best;
    }

    ErrandData PickErrand(QuestManager quest, List<ErrandData> exclude)
    {
        if (quest.ErrandLibrary == null) return null;

        List<ErrandData> options = new List<ErrandData>();
        float total = 0f;

        foreach (ErrandData e in quest.ErrandLibrary)
        {
            if (e == null || !e.allowInChallenge || e.minDay > Day || e.weight <= 0f) continue;
            if (quest.GetState(e.errandId) == ErrandState.Active) continue;   // already running
            if (exclude != null && exclude.Contains(e)) continue;             // already given out this phase
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
        int points = errand.data.challengeScore + Mathf.RoundToInt(minutesLeft * bonusPerMinuteLeft);
        Score += points;
        if (phaseErrands.Contains(errand.data.errandId)) PhaseDone++;
        Toast.Show(Loc.F(scoreFormat, points));
    }

    void OnErrandFailed(ActiveErrand errand)
    {
        if (!active) return;
        AddStrike();
    }

    void AddStrike()
    {
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
            if (inc == null || !inc.allowInChallenge || inc.minDay > Day || inc.weight <= 0f) continue;
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
        if (Clock != null) Clock.DebugAddMinutes(minutes);
    }
}