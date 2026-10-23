using System;
using UnityEngine;

public enum ClockMode { None, Free, Classic, Challenge }

// The in-game clock shared by free mode, classic mode and challenge mode (story mode has its own rounds).
// The day runs through the phases in a TimeOfDayData asset (Morning -> ... -> Night).
// A new day only starts when the player sleeps in a Bed at night.
// While it is night and no errand is running, the HUD shows "Go to bed" with an arrow to the NavTarget "Bed".
// Modes start it with Begin() and listen to its events. Put this on its own object in the game scene.
public class WorldClock : MonoBehaviour
{
    public static WorldClock Instance;

    [Header("Day")]
    [SerializeField] TimeOfDayData timeOfDay;
    [Tooltip("The clock stops here until the player sleeps (24 = midnight, 26 = 02:00).")]
    [SerializeField] float latestHour = 24f;
    [SerializeField] bool pauseWhileInteracting = true;

    [Header("Free mode")]
    [SerializeField] float freeStartHour = 6f;
    [Tooltip("In-game minutes that pass per real second in free mode.")]
    [SerializeField] float freeMinutesPerSecond = 1f;

    [Header("Sleep")]
    [Tooltip("Ticked = the bed only works at night.")]
    [SerializeField] bool sleepOnlyAtNight = true;
    [Tooltip("NavTarget id the arrow points to when it's time to sleep (put it on the house door and on the bed).")]
    [SerializeField] string bedNavId = "Bed";

    [Header("Phase message")]
    [SerializeField] bool showPhaseMessage = true;
    [Tooltip("Ticked = a new phase uses the big banner instead of a small pop-up.")]
    [SerializeField] bool phaseUsesBanner = false;
    [Tooltip("{0} = phase name")]
    [GameText] [SerializeField] string phaseMessageFormat = "{0}";

    [Header("Text")]
    [Tooltip("Banner after sleeping (free mode, or when a mode gives none).")]
    [GameText] [SerializeField] string nextDayTitle = "Next day";
    [GameText] [SerializeField] string notSleepyText = "Not sleepy yet...";
    [Tooltip("When trying to sleep with a request still running (or, in challenge mode, still waiting).")]
    [GameText] [SerializeField] string busyText = "Finish what you were asked first.";
    [GameText] [SerializeField] string goToBedText = "Go to bed";
    [GameText] [SerializeField, TextArea(2, 4)] string goToBedDetail = "It's late. Go home and sleep.";
    [SerializeField] float bannerSeconds = 2f;

    // Debug panel: makes the clock run faster (1 = normal)
    [NonSerialized] public float DebugSpeed = 1f;

    ClockMode mode = ClockMode.None;
    bool running;
    float clock;            // in-game minutes since midnight
    int phaseIndex = -1;
    float dayStartHour;
    float minutesPerSecond;
    Func<string> sleepTitle;      // set by the mode that started the clock (null = Next Day Title)
    Func<string> sleepSubtitle;

    // ---------- Events (modes listen while they run) ----------

    public event Action PhaseChanged;        // every new phase, also when a day starts
    public event Action NightStarted;        // a night phase begins (before PhaseChanged)
    public event Action<int> DayEnded;       // the player went to sleep (finished day number)
    public event Action DayStarted;          // a day begins (after Begin's banner, or after sleeping)

    // ---------- State ----------

    static bool InStory => TimeManager.Instance != null && TimeManager.Instance.InStory;

    public static bool IsActive => Instance != null && Instance.mode != ClockMode.None && !InStory;
    public ClockMode Mode => mode;
    public bool Running => running;
    public int Day { get; private set; }
    public float Clock => clock;
    public string ClockText => FormatClock(clock);
    public TimeOfDayData TimeOfDay => timeOfDay;
    public int PhaseIndex => phaseIndex;
    public TimeOfDayData.Phase CurrentPhase => timeOfDay != null ? timeOfDay.Get(phaseIndex) : null;
    public string PhaseName => CurrentPhase != null ? Loc.T(CurrentPhase.name) : "";
    public bool IsNight => CurrentPhase != null && CurrentPhase.isNight;
    public bool CanSleep => IsActive && running && (!sleepOnlyAtNight || IsNight) && !HasUnfinishedWork;

    // A request is running, or challenge errands are still waiting at NPCs: no sleeping yet
    public bool HasUnfinishedWork
    {
        get
        {
            QuestManager quest = QuestManager.Instance;
            if (quest != null && quest.Active.Count > 0) return true;
            return ChallengeManager.IsActive && ChallengeManager.Instance.OffersLeft > 0;
        }
    }
    public string BedNavId => bedNavId;
    public string GoToBedText => Loc.T(goToBedText);
    public string GoToBedDetail => Loc.T(goToBedDetail);

    // In-game minutes until the first night phase of the day (0 when it's already night)
    public float MinutesUntilNight
    {
        get
        {
            if (timeOfDay == null || timeOfDay.phases == null) return 0f;
            foreach (TimeOfDayData.Phase p in timeOfDay.phases)
            {
                if (p != null && p.isNight) return Mathf.Max(0f, p.startHour * 60f - clock);
            }
            return latestHour * 60f - clock;
        }
    }

    // Night and nothing left to do: the player should go to bed
    public bool WantsBed
    {
        get
        {
            if (!IsActive || !running || !IsNight) return false;
            QuestManager quest = QuestManager.Instance;
            return quest == null || quest.Active.Count == 0;
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

    // ---------- Starting ----------

    // Free mode: day 1, morning, no banner, nothing saved
    public void StartFree()
    {
        Begin(ClockMode.Free, 1, freeStartHour * 60f, freeStartHour, freeMinutesPerSecond, null, null, null, null);
    }

    // Starts the clock for a mode. bannerTitle null = no banner (the day starts right away).
    // sleepTitle / sleepSubtitle give the banner text after sleeping (null = Next Day Title / nothing).
    public void Begin(ClockMode newMode, int day, float startMinutes, float startHourAfterSleep, float speed,
                      string bannerTitle, string bannerSubtitle, Func<string> titleAfterSleep, Func<string> subtitleAfterSleep)
    {
        if (timeOfDay == null)
            Debug.LogWarning("WorldClock: no Time Of Day asset assigned.");

        mode = newMode;
        Day = Mathf.Max(1, day);
        clock = startMinutes;
        dayStartHour = startHourAfterSleep;
        minutesPerSecond = speed;
        sleepTitle = titleAfterSleep;
        sleepSubtitle = subtitleAfterSleep;
        phaseIndex = timeOfDay != null ? timeOfDay.IndexAt(clock) : -1;

        if (string.IsNullOrEmpty(bannerTitle))
        {
            StartDayNow();
            return;
        }

        running = false;
        PeriodBanner.Show(bannerTitle, bannerSubtitle, bannerSeconds, StartDayNow);
    }

    public void Stop()
    {
        mode = ClockMode.None;
        running = false;
        sleepTitle = null;
        sleepSubtitle = null;
    }

    void StartDayNow()
    {
        running = true;
        DayStarted?.Invoke();
        PhaseChanged?.Invoke();
    }

    // ---------- Update ----------

    void Update()
    {
        if (!IsActive || !running || timeOfDay == null) return;
        if (pauseWhileInteracting && InteractionState.IsInteracting) return;

        clock = Mathf.Min(clock + Time.deltaTime * minutesPerSecond * DebugSpeed, latestHour * 60f);

        int index = timeOfDay.IndexAt(clock);
        if (index != phaseIndex)
            EnterPhase(index);
    }

    void EnterPhase(int index)
    {
        bool wasNight = IsNight;
        phaseIndex = index;
        Debug.Log("Clock: " + PhaseName + " (" + ClockText + ")");

        if (IsNight && !wasNight) NightStarted?.Invoke();

        ShowPhaseMessage();
        PhaseChanged?.Invoke();
    }

    void ShowPhaseMessage()
    {
        if (!showPhaseMessage) return;

        string message = Loc.F(phaseMessageFormat, PhaseName);
        if (phaseUsesBanner)
        {
            running = false;
            PeriodBanner.Show(message, "", bannerSeconds, () => running = true);
        }
        else
        {
            Toast.Show(message);
        }
    }

    // ---------- Sleep ----------

    // Called by a Bed. Returns false when the player can't sleep now.
    public bool TrySleep()
    {
        if (!IsActive || !running) return false;

        if (!CanSleep)
        {
            bool sleepyTime = !sleepOnlyAtNight || IsNight;
            Toast.Show(Loc.T(sleepyTime && HasUnfinishedWork ? busyText : notSleepyText));
            return false;
        }

        running = false;
        DayEnded?.Invoke(Day);
        if (!IsActive) return true;   // the mode ended itself (e.g. game over)

        string title = sleepTitle != null ? sleepTitle() : Loc.T(nextDayTitle);
        string subtitle = sleepSubtitle != null ? sleepSubtitle() : "";

        PeriodBanner.Show(title, subtitle, bannerSeconds, () =>
        {
            Day++;
            clock = dayStartHour * 60f;
            phaseIndex = timeOfDay != null ? timeOfDay.IndexAt(clock) : -1;
            running = true;
            DayStarted?.Invoke();
            ShowPhaseMessage();
            PhaseChanged?.Invoke();
        });

        return true;
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
        clock = Mathf.Min(clock + minutes, latestHour * 60f);
    }

    // Debug: jump to a minute before the first night phase
    public void DebugJumpToNight()
    {
        if (timeOfDay == null || timeOfDay.phases == null) return;

        foreach (TimeOfDayData.Phase p in timeOfDay.phases)
        {
            if (p != null && p.isNight)
            {
                clock = Mathf.Max(clock, p.startHour * 60f - 1f);
                return;
            }
        }
    }
}
