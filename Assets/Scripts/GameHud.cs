using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Top-right HUD: timer card, navigation arrow + errand card, incident "!" card.
// Layout, colors and sizes live on the UI objects; this script only fills them in.
// The errand text and arrow target come from the tracked errand's current step (ErrandData asset).
// The timer shows the story round time when a round is running, otherwise the errand's own timer.
// In classic mode it shows the clock, and the other running errands are listed under the card.
[DefaultExecutionOrder(100)]   // runs after the player and camera have moved
public class GameHud : MonoBehaviour
{
    static GameHud instance;

    [Header("Timer")]
    [SerializeField] GameObject timerCard;
    [SerializeField] TMP_Text timerText;
    [SerializeField] Color timerNormalColor = Color.white;
    [SerializeField] Color timerWarningColor = new Color(1f, 0.55f, 0.45f);
    [SerializeField] float warningSeconds = 30f;
    [SerializeField] TMP_Text periodText;   // optional "Day 1 · Morning" label

    [Header("Errand")]
    [SerializeField] GameObject errandRow;
    [SerializeField] TMP_Text errandShortText;
    [SerializeField] GameObject errandDetail;      // the detail text object, hidden when collapsed
    [SerializeField] TMP_Text errandDetailText;
    [SerializeField] float autoCollapseSeconds = 1f;   // 0 = stays open until toggled again

    [Header("Other errands (when more than one is running)")]
    [SerializeField] Transform otherErrandsParent;     // rows are created under this
    [SerializeField] TMP_Text otherErrandRowPrefab;    // one line of text per errand
    [SerializeField] GameObject switchHint;            // optional "[Q] Switch", shown when 2+ errands run
    [SerializeField] Key switchKey = Key.Q;

    [Header("Classic mode")]
    [GameText] [SerializeField] string classicLabelFormat = "Day {0}   Score {1}   Strikes {2}/{3}";
    [Tooltip("The clock turns to the warning color when this many in-game minutes are left in the day.")]
    [SerializeField] float classicWarningMinutes = 60f;

    [Header("Failed errand text")]
    [GameText] [SerializeField] string failedShortText = "Time's up";
    [GameText] [SerializeField, TextArea(2, 4)] string failedDetailText = "You ran out of time.";

    [Header("Navigation")]
    [SerializeField] RectTransform navArrow;
    [SerializeField] string fallbackTargetId = "Exit";   // used when the real target isn't in this scene

    [Header("Incident")]
    [SerializeField] GameObject incidentCard;

    [Header("Testing")]
    [SerializeField] bool debugIncidentKey = true;   // press F9 to toggle a test incident

    RectTransform layoutRoot;
    Transform player;
    bool expanded;
    float collapseTimer;
    string shownShort;   // what the card currently shows, to avoid rebuilding every frame
    readonly List<TMP_Text> otherRows = new List<TMP_Text>();

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (errandRow != null)
            layoutRoot = errandRow.transform.parent as RectTransform;

        SetExpanded(false);
    }

    void Update()
    {
        // Auto-collapse the expanded errand card after a delay
        if (expanded && autoCollapseSeconds > 0f)
        {
            collapseTimer -= Time.deltaTime;
            if (collapseTimer <= 0f)
                SetExpanded(false);
        }

        var k = Keyboard.current;
        if (k == null) return;

        if (k.tabKey.wasPressedThisFrame)
            ToggleDetail();

        if (k[switchKey].wasPressedThisFrame && QuestManager.Instance != null)
        {
            QuestManager.Instance.CycleTracked();
            SetExpanded(false);
        }

        if (debugIncidentKey && k.f9Key.wasPressedThisFrame)
        {
            if (IncidentManager.IsActive("Test"))
                IncidentManager.End("Test");
            else
                IncidentManager.Begin("Test");
        }
    }

    void LateUpdate()
    {
        QuestManager quest = QuestManager.Instance;
        ActiveErrand tracked = quest != null ? quest.Tracked : null;
        bool showFailed = tracked == null && quest != null && quest.LastFailed != null;

        UpdateTimer(tracked, showFailed);
        UpdateErrand(tracked, showFailed);
        UpdateArrow(tracked);
        UpdateOtherErrands(quest, tracked);

        if (incidentCard != null)
            SetActive(incidentCard, IncidentManager.HasActive);
    }

    // Hook this to the errand card's Button OnClick so the mouse can expand it too.
    public void ToggleDetail()
    {
        SetExpanded(!expanded);
    }

    void SetExpanded(bool value)
    {
        expanded = value;
        collapseTimer = autoCollapseSeconds;   // restart the countdown every time it opens

        if (errandDetail != null)
            errandDetail.SetActive(expanded);

        // Swap: show either the short text or the detail text, never both
        if (errandShortText != null)
            errandShortText.gameObject.SetActive(!expanded);

        RebuildLayout();
    }

    void UpdateTimer(ActiveErrand tracked, bool showFailed)
    {
        // Classic mode: the card shows the clock
        if (ClassicManager.IsActive)
        {
            ClassicManager classic = ClassicManager.Instance;
            SetActive(timerCard, true);
            timerText.text = classic.ClockText;
            timerText.color = classic.MinutesLeftInDay <= classicWarningMinutes ? timerWarningColor : timerNormalColor;

            if (periodText != null)
                periodText.text = Loc.F(classicLabelFormat,
                    classic.Day, classic.Score, classic.Strikes, classic.StrikesAllowed);
            return;
        }

        TimeManager time = TimeManager.Instance;
        bool inRound = time != null && time.Running;
        bool roundTimer = time != null && time.HasTimer;
        bool errandTimer = !roundTimer && tracked != null && tracked.HasTimer;

        bool show = inRound || errandTimer || showFailed;
        SetActive(timerCard, show);

        if (periodText != null)
            periodText.text = inRound ? time.CurrentLabel : "";

        if (!show) return;

        if (!roundTimer && !errandTimer && !showFailed)
        {
            timerText.text = "";   // a round without a time limit: only the label is shown
            return;
        }

        float timeLeft = roundTimer ? time.TimeLeft : errandTimer ? tracked.timeLeft : 0f;
        int seconds = Mathf.CeilToInt(timeLeft);
        timerText.text = string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60);
        timerText.color = timeLeft <= warningSeconds ? timerWarningColor : timerNormalColor;
    }

    void UpdateErrand(ActiveErrand tracked, bool showFailed)
    {
        string shortText = null;
        string detailText = null;

        if (tracked != null && tracked.CurrentStep != null)
        {
            shortText = Loc.T(tracked.CurrentStep.shortText);
            if (!string.IsNullOrEmpty(tracked.deadlineLabel))
                shortText += "  " + tracked.deadlineLabel;
            detailText = Loc.T(tracked.CurrentStep.detailText);
        }
        else if (showFailed)
        {
            shortText = Loc.T(failedShortText);
            detailText = Loc.T(failedDetailText);
        }

        bool show = !string.IsNullOrEmpty(shortText);
        SetActive(errandRow, show);
        if (!show) return;

        if (shownShort != shortText)
        {
            shownShort = shortText;
            errandShortText.text = shortText;
            errandDetailText.text = detailText;
            RebuildLayout();
        }
    }

    // Lists the running errands that aren't the tracked one
    void UpdateOtherErrands(QuestManager quest, ActiveErrand tracked)
    {
        int count = quest != null ? quest.Active.Count : 0;

        if (switchHint != null)
            SetActive(switchHint, count > 1);

        if (otherErrandsParent == null || otherErrandRowPrefab == null) return;

        int others = Mathf.Max(0, count - (tracked != null ? 1 : 0));

        // Make the number of rows match
        while (otherRows.Count < others)
        {
            otherRows.Add(Instantiate(otherErrandRowPrefab, otherErrandsParent));
            RebuildLayout();
        }
        while (otherRows.Count > others)
        {
            Destroy(otherRows[otherRows.Count - 1].gameObject);
            otherRows.RemoveAt(otherRows.Count - 1);
            RebuildLayout();
        }

        int row = 0;
        for (int i = 0; i < count && row < otherRows.Count; i++)
        {
            ActiveErrand e = quest.Active[i];
            if (e == tracked) continue;

            string text = e.CurrentStep != null ? Loc.T(e.CurrentStep.shortText) : e.data.errandId;
            if (!string.IsNullOrEmpty(e.deadlineLabel))
            {
                text += "  " + e.deadlineLabel;
            }
            else if (e.HasTimer)
            {
                int s = Mathf.CeilToInt(e.timeLeft);
                text += string.Format("  {0:00}:{1:00}", s / 60, s % 60);
            }

            if (otherRows[row].text != text) otherRows[row].text = text;
            row++;
        }
    }

    void UpdateArrow(ActiveErrand tracked)
    {
        if (navArrow == null) return;

        if (player == null)
        {
            var p = FindAnyObjectByType<PlayerMovement>();
            if (p != null) player = p.transform;
        }

        NavTarget target = null;
        ErrandStep step = tracked != null ? tracked.CurrentStep : null;

        if (step != null && !string.IsNullOrEmpty(step.NavId))
        {
            target = NavTarget.Find(step.NavId);
            if (target == null) target = NavTarget.Find(fallbackTargetId);
        }

        bool show = target != null && player != null;
        SetActive(navArrow.gameObject, show);
        if (!show) return;

        // The arrow sprite points up, so subtract 90 degrees from the world angle
        Vector2 dir = target.transform.position - player.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        navArrow.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    void SetActive(GameObject go, bool value)
    {
        if (go != null && go.activeSelf != value)
        {
            go.SetActive(value);
            RebuildLayout();
        }
    }

    void RebuildLayout()
    {
        if (layoutRoot != null)
            LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
    }
}