using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Classic mode: a relaxed mode with no score and no time pressure.
// Uses the shared WorldClock (Morning -> ... -> Night, sleep in a Bed for the next day).
// Every new phase is auto-saved. When night starts, running errands fail.
// The save only remembers the day and phase (and how many errands were done), not running errands or items.
// Errands come from NPCs: every phase (not at night) some random NPCs get a random errand to offer
// (ErrandData with Offer In Classic ticked). Talking to them accepts it.
// Put this on its own object in the game scene (e.g. next to the ChallengeManager).
public class ClassicManager : MonoBehaviour
{
    public static ClassicManager Instance;

    [Header("Clock")]
    [Tooltip("Hour the day starts at (new game and after sleeping).")]
    [SerializeField] float dayStartHour = 6f;
    [Tooltip("In-game minutes that pass per real second.")]
    [SerializeField] float gameMinutesPerSecond = 1f;

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
    [Tooltip("Pop-up when night starts with errands still running. {0} = how many.")]
    [GameText] [SerializeField] string errandsExpiredFormat = "{0} errand(s) expired";
    [Tooltip("Pop-up when an errand is accepted. {0} = what to do first.")]
    [GameText] [SerializeField] string newErrandFormat = "New errand: {0}";
    [GameText] [SerializeField] string tooManyErrandsText = "You have enough errands for now.";

    const string FileName = "classic.json";

    [Serializable]
    class ClassicSave
    {
        public int day = 1;
        public int phaseIndex;
        public int completed;
    }

    readonly Dictionary<string, ErrandData> offers = new Dictionary<string, ErrandData>();   // NPC id -> errand

    bool active;   // classic mode is on

    static WorldClock Clock => WorldClock.Instance;

    public static bool IsActive => Instance != null && Instance.active;
    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
    public static bool HasSave => File.Exists(SavePath);

    public int Completed { get; private set; }      // errands finished in classic mode (all days)
    public string TooManyErrandsText => Loc.T(tooManyErrandsText);

    public bool CanTakeMore
    {
        get
        {
            QuestManager quest = QuestManager.Instance;
            return quest != null && quest.Active.Count < maxActiveErrands;
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

    void OnDestroy()
    {
        Unhook();
    }

    // ---------- Start / stop ----------

    public void StartClassic()
    {
        if (Clock == null || Clock.TimeOfDay == null)
        {
            Debug.LogWarning("Classic: needs a WorldClock with a Time Of Day asset in the scene.");
            return;
        }

        SaveSystem.ResetProgress();   // clear errands, items and flags from earlier play

        TimeManager time = TimeManager.Instance;
        if (time != null) time.EnterFreeMode();   // make sure story mode is off

        Unhook();
        Hook();

        int day = 1;
        float start = dayStartHour * 60f;
        Completed = 0;

        ClassicSave save = Load();
        if (save != null)
        {
            day = save.day;
            Completed = save.completed;
            TimeOfDayData.Phase phase = Clock.TimeOfDay.Get(save.phaseIndex);
            if (phase != null) start = phase.startHour * 60f;
        }

        active = true;
        offers.Clear();

        int phaseIndex = Clock.TimeOfDay.IndexAt(start);
        TimeOfDayData.Phase startPhase = Clock.TimeOfDay.Get(phaseIndex);
        string phaseName = startPhase != null ? Loc.T(startPhase.name) : "";

        Debug.Log("Classic: start (day " + day + ", " + phaseName + ")");

        Clock.Begin(ClockMode.Classic, day, start, dayStartHour, gameMinutesPerSecond,
                    Loc.F(startTitle, phaseName), phaseName,
                    () => Loc.T(nextDayTitle), null);
    }

    // Leaves classic mode (e.g. back to free mode)
    public void Stop()
    {
        active = false;
        offers.Clear();
        Unhook();
    }

    void Hook()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest != null) quest.ErrandCompleted += OnErrandCompleted;

        if (Clock != null)
        {
            Clock.PhaseChanged += OnPhaseChanged;
            Clock.NightStarted += ExpireErrands;
            Clock.DayEnded += OnDayEnded;
        }
    }

    void Unhook()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest != null) quest.ErrandCompleted -= OnErrandCompleted;

        if (Clock != null)
        {
            Clock.PhaseChanged -= OnPhaseChanged;
            Clock.NightStarted -= ExpireErrands;
            Clock.DayEnded -= OnDayEnded;
        }
    }

    void OnErrandCompleted(ActiveErrand errand)
    {
        if (active) Completed++;
    }

    void OnPhaseChanged()
    {
        if (!active) return;
        Save();
        RefreshOffers();
    }

    void OnDayEnded(int day)
    {
        if (!active) return;
        ExpireErrands();   // night errands that weren't finished before sleeping
        offers.Clear();
    }

    // Every running errand fails (night starts, or going to sleep)
    void ExpireErrands()
    {
        if (!active) return;

        QuestManager quest = QuestManager.Instance;
        if (quest == null || quest.Active.Count == 0) return;

        ErrandData[] list = new ErrandData[quest.Active.Count];
        for (int i = 0; i < list.Length; i++) list[i] = quest.Active[i].data;

        foreach (ErrandData data in list)
            quest.ForceFail(data);

        Toast.Show(Loc.F(errandsExpiredFormat, list.Length));
    }

    // ---------- NPC errands ----------

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
        string npcName = NpcRegistry.NameOf(npcId);

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
        if (!active || Clock == null || Clock.IsNight) return;

        QuestManager quest = QuestManager.Instance;
        if (quest == null || quest.ErrandLibrary == null) return;

        // NPCs that are free: not already waiting for an errand they gave
        List<string> freeGivers = NpcRegistry.FreeGiversShuffled();

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
        else if (NpcRegistry.Count == 0)
            Debug.Log("Classic: no NPC can give errands yet (tick Gives Errands on NPCs).");
        else if (pool.Count == 0 && count > 0)
            Debug.Log("Classic: no errand to offer. Tick 'Offer In Classic' on ErrandData assets in the Errand Library.");
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

    // ---------- Save ----------

    void Save()
    {
        if (Clock == null) return;

        ClassicSave save = new ClassicSave { day = Clock.Day, phaseIndex = Clock.PhaseIndex, completed = Completed };

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
}
