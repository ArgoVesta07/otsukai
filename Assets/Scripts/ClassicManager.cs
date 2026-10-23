using System;
using System.IO;
using UnityEngine;

// Classic mode: a relaxed mode with no score and no time pressure.
// A clock runs through the day (Morning -> Afternoon -> Evening -> Night, see TimeOfDayData).
// Every new phase: a short message and an auto-save. When Night starts, running errands fail.
// A new day only starts when the player sleeps in a Bed. The save only remembers the day phase
// (and how many errands were done), not running errands or items.
// Errands come from NPCs (added in a later step).
// Put this on its own object in the game scene (e.g. next to the ChallengeManager).
public class ClassicManager : MonoBehaviour
{
    public static ClassicManager Instance;

    [Header("Clock")]
    [SerializeField] TimeOfDayData timeOfDay;
    [Tooltip("Hour the clock starts at after sleeping.")]
    [SerializeField] float dayStartHour = 6f;
    [Tooltip("The clock stops here until the player sleeps.")]
    [SerializeField] float latestHour = 24f;
    [Tooltip("In-game minutes that pass per real second.")]
    [SerializeField] float gameMinutesPerSecond = 1f;
    [SerializeField] bool pauseWhileInteracting = true;

    [Header("Sleep")]
    [Tooltip("Ticked = the bed only works at night.")]
    [SerializeField] bool sleepOnlyAtNight = true;

    [Header("Text")]
    [Tooltip("Banner when classic mode starts. {0} = day phase.")]
    [GameText] [SerializeField] string startTitle = "Classic";
    [GameText] [SerializeField] string nextDayTitle = "Next day";
    [Tooltip("Pop-up when a new phase starts. {0} = phase name.")]
    [GameText] [SerializeField] string phaseMessageFormat = "{0}";
    [Tooltip("Pop-up when night starts with errands still running. {0} = how many.")]
    [GameText] [SerializeField] string errandsExpiredFormat = "{0} errand(s) expired";
    [GameText] [SerializeField] string notSleepyText = "Not sleepy yet...";
    [Tooltip("Ticked = a new phase uses the big banner instead of a small pop-up.")]
    [SerializeField] bool phaseUsesBanner = false;
    [SerializeField] float bannerSeconds = 2f;

    const string FileName = "classic.json";

    [Serializable]
    class ClassicSave
    {
        public int day = 1;
        public int phaseIndex;
        public int completed;
    }

    bool active;      // classic mode is on
    bool running;     // the clock is moving (not during banners)
    float clock;      // in-game minutes since midnight
    int phaseIndex = -1;

    public static bool IsActive => Instance != null && Instance.active;
    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
    public static bool HasSave => File.Exists(SavePath);

    public int Day { get; private set; }            // counted, but not shown to the player
    public int Completed { get; private set; }      // errands finished in classic mode (all days)
    public bool Running => running;
    public string ClockText => FormatClock(clock);
    public TimeOfDayData.Phase CurrentPhase => timeOfDay != null ? timeOfDay.Get(phaseIndex) : null;
    public string PhaseName => CurrentPhase != null ? Loc.T(CurrentPhase.name) : "";
    public bool IsNight => CurrentPhase != null && CurrentPhase.isNight;
    public bool CanSleep => active && running && (!sleepOnlyAtNight || IsNight);

    // Fired when a new phase starts (also after waking up). Used later for NPC errands.
    public event Action PhaseChanged;

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
        if (timeOfDay == null)
        {
            Debug.LogWarning("Classic: no Time Of Day asset on the ClassicManager.");
            return;
        }

        SaveSystem.ResetProgress();   // clear errands, items and flags from earlier play

        TimeManager time = TimeManager.Instance;
        if (time != null) time.EnterFreeMode();   // make sure story mode is off

        QuestManager quest = QuestManager.Instance;
        if (quest != null)
        {
            Unhook();
            quest.ErrandCompleted += OnErrandCompleted;
        }

        ClassicSave save = Load();
        if (save != null)
        {
            Day = Mathf.Max(1, save.day);
            Completed = save.completed;
            TimeOfDayData.Phase phase = timeOfDay.Get(save.phaseIndex);
            clock = (phase != null ? phase.startHour : dayStartHour) * 60f;
        }
        else
        {
            Day = 1;
            Completed = 0;
            clock = dayStartHour * 60f;
        }

        phaseIndex = timeOfDay.IndexAt(clock);
        active = true;
        running = false;

        Debug.Log("Classic: start (day " + Day + ", " + PhaseName + ")");

        PeriodBanner.Show(Loc.F(startTitle, PhaseName), PhaseName, bannerSeconds, () =>
        {
            running = true;
            Save();
            PhaseChanged?.Invoke();
        });
    }

    // Leaves classic mode (e.g. another mode is chosen)
    public void Stop()
    {
        active = false;
        running = false;
        Unhook();
    }

    void Unhook()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest == null) return;

        quest.ErrandCompleted -= OnErrandCompleted;
    }

    void OnErrandCompleted(ActiveErrand errand)
    {
        if (active) Completed++;
    }

    // ---------- Update ----------

    void Update()
    {
        if (!active || !running) return;
        if (pauseWhileInteracting && InteractionState.IsInteracting) return;

        clock = Mathf.Min(clock + Time.deltaTime * gameMinutesPerSecond, latestHour * 60f);

        int index = timeOfDay.IndexAt(clock);
        if (index != phaseIndex)
            EnterPhase(index);
    }

    void EnterPhase(int index)
    {
        phaseIndex = index;
        Debug.Log("Classic: " + PhaseName);

        if (IsNight) ExpireErrands();

        Save();

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

        PhaseChanged?.Invoke();
    }

    // Every running errand fails (night, or going to sleep)
    void ExpireErrands()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest == null || quest.Active.Count == 0) return;

        ErrandData[] list = new ErrandData[quest.Active.Count];
        for (int i = 0; i < list.Length; i++) list[i] = quest.Active[i].data;

        foreach (ErrandData data in list)
            quest.ForceFail(data);

        Toast.Show(Loc.F(errandsExpiredFormat, list.Length));
    }

    // ---------- Sleep ----------

    // Called by a Bed. Returns false when the player can't sleep yet.
    public bool Sleep()
    {
        if (!active || !running) return false;

        if (!CanSleep)
        {
            Toast.Show(Loc.T(notSleepyText));
            return false;
        }

        ExpireErrands();
        running = false;
        Day++;

        PeriodBanner.Show(Loc.T(nextDayTitle), "", bannerSeconds, () =>
        {
            clock = dayStartHour * 60f;
            phaseIndex = timeOfDay.IndexAt(clock);
            running = true;
            Save();
            Toast.Show(Loc.F(phaseMessageFormat, PhaseName));
            PhaseChanged?.Invoke();
        });

        return true;
    }

    // ---------- Save ----------

    void Save()
    {
        ClassicSave save = new ClassicSave { day = Day, phaseIndex = phaseIndex, completed = Completed };

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(save, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Classic: save failed. " + e.Message);
        }
    }

    static ClassicSave Load()
    {
        if (!HasSave) return null;

        try
        {
            return JsonUtility.FromJson<ClassicSave>(File.ReadAllText(SavePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Classic: couldn't read the save. " + e.Message);
            return null;
        }
    }

    public static void DeleteSave()
    {
        if (HasSave) File.Delete(SavePath);
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
}
