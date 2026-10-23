using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Classic mode: a relaxed mode with no score and no time pressure.
// A clock runs through the day (Morning -> Afternoon -> Evening -> Night, see TimeOfDayData).
// Every new phase: a short message and an auto-save. When Night starts, running errands fail.
// A new day only starts when the player sleeps in a Bed. The save only remembers the day phase
// (and how many errands were done), not running errands or items.
// Errands come from NPCs: every phase (not at night) some random NPCs get a random errand to offer
// (ErrandData with Offer In Classic ticked). Talking to them accepts it.
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

    [Header("NPC errands")]
    [Tooltip("How many NPCs get an errand to offer at the start of each phase.")]
    [SerializeField] int offersPerPhase = 2;
    [Tooltip("The player can't take more errands than this at once.")]
    [SerializeField] int maxActiveErrands = 3;
    [Tooltip("Seconds to finish an NPC errand. 0 = no timer (it still fails when night starts).")]
    [SerializeField] float errandTimeLimit = 0f;

    [Header("Text")]
    [Tooltip("Banner when classic mode starts. {0} = day phase.")]
    [GameText] [SerializeField] string startTitle = "Classic";
    [GameText] [SerializeField] string nextDayTitle = "Next day";
    [Tooltip("Pop-up when a new phase starts. {0} = phase name.")]
    [GameText] [SerializeField] string phaseMessageFormat = "{0}";
    [Tooltip("Pop-up when night starts with errands still running. {0} = how many.")]
    [GameText] [SerializeField] string errandsExpiredFormat = "{0} errand(s) expired";
    [GameText] [SerializeField] string notSleepyText = "Not sleepy yet...";
    [Tooltip("Pop-up when an errand is accepted. {0} = what to do first.")]
    [GameText] [SerializeField] string newErrandFormat = "New errand: {0}";
    [GameText] [SerializeField] string tooManyErrandsText = "You have enough errands for now.";
    [GameText] [SerializeField] string missingItemText = "You don't have what's needed yet.";
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

    // NPCs that can give errands (id -> name). Filled by NPCs as they appear, kept across scenes.
    static readonly Dictionary<string, string> givers = new Dictionary<string, string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        givers.Clear();
    }

    readonly Dictionary<string, ErrandData> offers = new Dictionary<string, ErrandData>();   // NPC id -> errand

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
    public string TooManyErrandsText => Loc.T(tooManyErrandsText);
    public string MissingItemText => Loc.T(missingItemText);

    public bool CanTakeMore
    {
        get
        {
            QuestManager quest = QuestManager.Instance;
            return quest != null && quest.Active.Count < maxActiveErrands;
        }
    }

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
            RefreshOffers();
            PhaseChanged?.Invoke();
        });
    }

    // Leaves classic mode (e.g. another mode is chosen)
    public void Stop()
    {
        active = false;
        running = false;
        offers.Clear();
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

        RefreshOffers();
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
            RefreshOffers();
            PhaseChanged?.Invoke();
        });

        return true;
    }

    // ---------- NPC errands ----------

    // NPCs call this when they appear, so they can be picked to give errands
    public static void RegisterGiver(string npcId, string npcName)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        givers[npcId] = npcName;
    }

    public bool HasOffer(string npcId)
    {
        return active && !string.IsNullOrEmpty(npcId) && offers.ContainsKey(npcId);
    }

    public ErrandData OfferFor(string npcId)
    {
        return HasOffer(npcId) ? offers[npcId] : null;
    }

    // The player talked to an NPC with an offer: start the errand, with that NPC as the giver
    public bool AcceptOffer(string npcId)
    {
        ErrandData data = OfferFor(npcId);
        QuestManager quest = QuestManager.Instance;
        if (data == null || quest == null) return false;

        if (!CanTakeMore)
        {
            Toast.Show(TooManyErrandsText);
            return false;
        }

        offers.Remove(npcId);
        givers.TryGetValue(npcId, out string npcName);

        quest.ForgetErrand(data.errandId);   // classic errands can be done again later
        if (!quest.StartErrand(data, errandTimeLimit, null, true, npcId, npcName))
            return false;

        ActiveErrand started = FindActive(quest, data);
        string first = started != null && started.CurrentStep != null
            ? started.Fill(Loc.T(started.CurrentStep.shortText))
            : data.errandId;
        Toast.Show(Loc.F(newErrandFormat, first));
        return true;
    }

    static ActiveErrand FindActive(QuestManager quest, ErrandData data)
    {
        foreach (ActiveErrand e in quest.Active)
        {
            if (e.data == data) return e;
        }
        return null;
    }

    // New random offers for this phase (none at night)
    void RefreshOffers()
    {
        offers.Clear();
        if (!active || IsNight) return;

        QuestManager quest = QuestManager.Instance;
        if (quest == null || quest.ErrandLibrary == null) return;

        // NPCs that are free: not already waiting for an errand they gave
        List<string> freeGivers = new List<string>();
        foreach (string id in givers.Keys)
        {
            if (!IsGiverBusy(quest, id)) freeGivers.Add(id);
        }
        Shuffle(freeGivers);

        // Errands that may be offered and aren't running
        List<ErrandData> pool = new List<ErrandData>();
        foreach (ErrandData e in quest.ErrandLibrary)
        {
            if (e == null || !e.offerInClassic || e.classicWeight <= 0f) continue;
            if (quest.GetState(e.errandId) == ErrandState.Active) continue;
            pool.Add(e);
        }

        int count = Mathf.Min(offersPerPhase, freeGivers.Count);
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            ErrandData pick = PickWeighted(pool);
            pool.Remove(pick);   // one NPC per errand
            offers[freeGivers[i]] = pick;
        }

        if (offers.Count > 0)
            Debug.Log("Classic: " + offers.Count + " NPC(s) have an errand (" + string.Join(", ", offers.Keys) + ")");
        else if (givers.Count == 0)
            Debug.Log("Classic: no NPC can give errands yet (tick Gives Classic Errands on NPCs).");
        else if (pool.Count == 0 && count > 0)
            Debug.Log("Classic: no errand to offer. Tick 'Offer In Classic' on ErrandData assets in the Errand Library.");
    }

    static bool IsGiverBusy(QuestManager quest, string npcId)
    {
        foreach (ActiveErrand e in quest.Active)
        {
            if (e.giverId == npcId) return true;
        }
        return false;
    }

    static ErrandData PickWeighted(List<ErrandData> pool)
    {
        float total = 0f;
        foreach (ErrandData e in pool) total += e.classicWeight;

        float roll = UnityEngine.Random.value * total;
        foreach (ErrandData e in pool)
        {
            roll -= e.classicWeight;
            if (roll <= 0f) return e;
        }
        return pool[pool.Count - 1];
    }

    static void Shuffle(List<string> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            string tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
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
