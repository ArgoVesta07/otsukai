using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Classic mode: a relaxed mode with no score and no time pressure.
// Uses the shared WorldClock (Morning -> ... -> Night, sleep in a Bed for the next day).
// Every new phase is auto-saved. An errand only lasts for the phase it was taken in: when the phase
// ends (e.g. morning -> afternoon) it fails. At night some NPCs may offer night errands (Allow At Night);
// the player can't sleep while one is running.
// The save only remembers the day and phase (and how many errands were done), not running errands or items.
// Otsukai (main errands) come from family: 2-3 random phases a day, one family member has one (star).
// Talking to them takes it. Requests (side errands) come from town people: every phase some random
// townspeople offer one ("!"), shown on a card (Accept / Later). Both need Offer In Classic on the ErrandData.
// Put this on its own object in the game scene (e.g. next to the ChallengeManager).
public class ClassicManager : MonoBehaviour
{
    public static ClassicManager Instance;

    [Header("Clock")]
    [Tooltip("Hour the day starts at (new game and after sleeping).")]
    [SerializeField] float dayStartHour = 6f;
    [Tooltip("In-game minutes that pass per real second.")]
    [SerializeField] float gameMinutesPerSecond = 1f;

    [Header("Otsukai (from family)")]
    [Tooltip("Otsukai per day, picked at random phases (night only if an otsukai has Allow At Night).")]
    [SerializeField] int minOtsukaiPerDay = 2;
    [SerializeField] int maxOtsukaiPerDay = 3;

    [Header("Requests (from town people)")]
    [Tooltip("How many NPCs get an errand to offer at the start of each phase.")]
    [SerializeField] int offersPerPhase = 2;
    [Tooltip("The player can't take more errands than this at once.")]
    [SerializeField] int maxActiveErrands = 3;
    [Tooltip("Seconds to finish an NPC errand. 0 = no timer (it still fails when the phase ends).")]
    [SerializeField] float errandTimeLimit = 0f;

    [Header("Night errands")]
    [Tooltip("Chance that some NPCs offer an errand at night (only errands with Allow At Night).")]
    [SerializeField, Range(0f, 1f)] float nightOfferChance = 0.5f;
    [SerializeField] int nightOffersMax = 1;

    [Header("Text")]
    [Tooltip("Banner when classic mode starts. {0} = day phase.")]
    [GameText] [SerializeField] string startTitle = "Classic";
    [GameText] [SerializeField] string nextDayTitle = "Next day";
    [Tooltip("Pop-up when night starts with errands still running. {0} = how many.")]
    [GameText] [SerializeField] string errandsExpiredFormat = "{0} errand(s) expired";
    [Tooltip("Pop-up when an errand is accepted. {0} = what to do first.")]
    [GameText] [SerializeField] string newErrandFormat = "New errand: {0}";
    [GameText] [SerializeField] string tooManyErrandsText = "You have enough errands for now.";
    [Tooltip("Pop-up when an otsukai is taken. {0} = what to do first.")]
    [GameText] [SerializeField] string newOtsukaiFormat = "Errand: {0}";
    [Tooltip("Errand card while a family member is waiting with an otsukai. {0} = their name")]
    [GameText] [SerializeField] string otsukaiWaitingFormat = "{0} has an errand for you";
    [Tooltip("Errand card when there's nothing to do (optional help for town people).")]
    [GameText] [SerializeField] string helpTownText = "Help people around town";

    const string FileName = "classic.json";

    [Serializable]
    class ClassicSave
    {
        public int day = 1;
        public int phaseIndex;
        public int completed;
    }

    readonly Dictionary<string, ErrandData> offers = new Dictionary<string, ErrandData>();   // NPC id -> request
    readonly HashSet<int> otsukaiPhases = new HashSet<int>();   // phases of today that get an otsukai
    string otsukaiGiver;      // family member waiting with an otsukai (null = none)
    ErrandData otsukaiOffer;

    bool active;   // classic mode is on

    static WorldClock Clock => WorldClock.Instance;

    public static bool IsActive => Instance != null && Instance.active;
    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
    public static bool HasSave => File.Exists(SavePath);

    public int Completed { get; private set; }      // errands finished in classic mode (all days)
    public string TooManyErrandsText => Loc.T(tooManyErrandsText);
    public string HelpTownText => Loc.T(helpTownText);

    // The family member waiting with an otsukai (null = none), for the HUD card and arrow
    public string OtsukaiGiverId => active ? otsukaiGiver : null;
    public string OtsukaiWaitingText =>
        OtsukaiGiverId != null ? Loc.F(otsukaiWaitingFormat, Loc.T(NpcRegistry.NameOf(otsukaiGiver))) : null;

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
        ClearOtsukaiOffer();

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
        ClearOtsukaiOffer();
        Unhook();
    }

    void Hook()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest != null) quest.ErrandCompleted += OnErrandCompleted;

        if (Clock != null)
        {
            Clock.PhaseChanged += OnPhaseChanged;
            Clock.DayStarted += PlanOtsukai;
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
            Clock.DayStarted -= PlanOtsukai;
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
        ExpireErrands();   // errands from the phase that just ended
        ClearOtsukaiOffer();
        Save();
        RefreshOffers();
        if (otsukaiPhases.Contains(Clock.PhaseIndex)) GiveOtsukai();
    }

    void OnDayEnded(int day)
    {
        if (!active) return;
        ExpireErrands();   // night errands that weren't finished before sleeping
        offers.Clear();
        ClearOtsukaiOffer();
    }

    // Every running errand fails (the phase ended, or going to sleep)
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

    // ---------- Otsukai (family) ----------

    // Picks today's otsukai phases at random
    void PlanOtsukai()
    {
        otsukaiPhases.Clear();
        if (!active || Clock == null || Clock.TimeOfDay == null || Clock.TimeOfDay.phases == null) return;

        int phaseCount = Clock.TimeOfDay.phases.Length;
        int count = Mathf.Clamp(UnityEngine.Random.Range(minOtsukaiPerDay, maxOtsukaiPerDay + 1), 0, phaseCount);

        List<int> all = new List<int>();
        for (int i = 0; i < phaseCount; i++) all.Add(i);

        for (int n = 0; n < count && all.Count > 0; n++)
        {
            int pick = UnityEngine.Random.Range(0, all.Count);
            otsukaiPhases.Add(all[pick]);
            all.RemoveAt(pick);
        }

        Debug.Log("Classic: otsukai today in phase(s) " + string.Join(", ", otsukaiPhases));
    }

    void GiveOtsukai()
    {
        QuestManager quest = QuestManager.Instance;
        if (quest == null || quest.ErrandLibrary == null) return;

        bool night = Clock.IsNight;
        List<ErrandData> pool = new List<ErrandData>();
        foreach (ErrandData e in quest.ErrandLibrary)
        {
            if (e == null || e.kind != ErrandKind.Otsukai || !e.offerInClassic || e.classicWeight <= 0f) continue;
            if (night && !e.allowAtNight) continue;
            if (quest.GetState(e.errandId) == ErrandState.Active) continue;
            pool.Add(e);
        }

        List<string> family = NpcRegistry.FreeGiversShuffled(NpcRole.Family);

        if (pool.Count == 0)
        {
            if (!night) Debug.Log("Classic: no otsukai to give. Make an ErrandData with Kind = Otsukai and Offer In Classic ticked.");
            return;
        }
        if (family.Count == 0)
        {
            Debug.Log("Classic: no free family member (set Role = Family on NPCs like Mom).");
            return;
        }

        otsukaiGiver = family[0];
        otsukaiOffer = PickWeighted(pool);
        Debug.Log("Classic: otsukai " + otsukaiOffer.errandId + " from " + otsukaiGiver);
    }

    void ClearOtsukaiOffer()
    {
        otsukaiGiver = null;
        otsukaiOffer = null;
    }

    public bool HasOtsukai(string npcId)
    {
        return active && otsukaiGiver != null && otsukaiGiver == npcId;
    }

    public ErrandData OtsukaiFor(string npcId)
    {
        return HasOtsukai(npcId) ? otsukaiOffer : null;
    }

    // The player talked to the family member: the otsukai starts (no card, family doesn't take "later")
    public bool AcceptOtsukai(string npcId)
    {
        ErrandData data = OtsukaiFor(npcId);
        QuestManager quest = QuestManager.Instance;
        if (data == null || quest == null) return false;

        ClearOtsukaiOffer();
        quest.ForgetErrand(data.errandId);
        if (!quest.StartErrand(data, errandTimeLimit, null, true, npcId, NpcRegistry.NameOf(npcId)))
            return false;

        ActiveErrand started = FindActive(quest, data);
        string first = started != null && started.CurrentStep != null
            ? started.Fill(Loc.T(started.CurrentStep.shortText))
            : data.errandId;
        Toast.Show(Loc.F(newOtsukaiFormat, first));
        return true;
    }

    // ---------- Requests (town people) ----------

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

    // New random offers for this phase (at night: maybe a few night errands)
    void RefreshOffers()
    {
        offers.Clear();
        if (!active || Clock == null) return;

        bool night = Clock.IsNight;
        int wanted = night
            ? (UnityEngine.Random.value < nightOfferChance ? nightOffersMax : 0)
            : offersPerPhase;
        if (wanted <= 0) return;

        QuestManager quest = QuestManager.Instance;
        if (quest == null || quest.ErrandLibrary == null) return;

        // NPCs that are free: not already waiting for an errand they gave
        List<string> freeGivers = NpcRegistry.FreeGiversShuffled(NpcRole.Townsperson);

        // Errands that may be offered and aren't running
        List<ErrandData> pool = new List<ErrandData>();
        foreach (ErrandData e in quest.ErrandLibrary)
        {
            if (e == null || e.kind != ErrandKind.Request || !e.offerInClassic || e.classicWeight <= 0f) continue;
            if (night && !e.allowAtNight) continue;
            if (quest.GetState(e.errandId) == ErrandState.Active) continue;
            pool.Add(e);
        }

        int count = Mathf.Min(wanted, freeGivers.Count);
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            ErrandData pick = PickWeighted(pool);
            pool.Remove(pick);   // one NPC per errand
            offers[freeGivers[i]] = pick;
        }

        if (offers.Count > 0)
            Debug.Log("Classic: " + offers.Count + " NPC(s) have an errand (" + string.Join(", ", offers.Keys) + ")");
        else if (NpcRegistry.CountOf(NpcRole.Townsperson) == 0)
            Debug.Log("Classic: no town people can give requests yet (Gives Errands + Role = Townsperson on NPCs).");
        else if (pool.Count == 0 && count > 0)
            Debug.Log("Classic: no request to offer. Make ErrandData with Kind = Request and Offer In Classic ticked.");
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
