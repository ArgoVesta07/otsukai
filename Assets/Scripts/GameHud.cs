using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Top-right HUD: timer card, navigation arrow + errand card, incident "!" card.
// Layout, colors and sizes live on the UI objects; this script only fills them in.
// The errand text and arrow target come from the tracked errand's current step (ErrandData asset).
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

    [Header("Errand")]
    [SerializeField] GameObject errandRow;
    [SerializeField] TMP_Text errandShortText;
    [SerializeField] GameObject errandDetail;      // the detail text object, hidden when collapsed
    [SerializeField] TMP_Text errandDetailText;
    [SerializeField] float autoCollapseSeconds = 1f;   // 0 = stays open until toggled again

    [Header("Failed errand text")]
    [SerializeField] string failedShortText = "Time's up";
    [SerializeField, TextArea(2, 4)] string failedDetailText = "You ran out of time.";

    [Header("Navigation")]
    [SerializeField] RectTransform navArrow;
    [SerializeField] string fallbackTargetId = "Exit";   // used when the real target isn't in this scene

    [Header("Incident")]
    [SerializeField] GameObject incidentCard;

    [Header("Testing")]
    [SerializeField] bool debugIncidentKey = true;   // press I to toggle a test incident

    RectTransform layoutRoot;
    Transform player;
    bool expanded;
    float collapseTimer;
    string shownShort;   // what the card currently shows, to avoid rebuilding every frame

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

        if (debugIncidentKey && k.iKey.wasPressedThisFrame)
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
        bool running = tracked != null && tracked.HasTimer;

        SetActive(timerCard, running || showFailed);
        if (!running && !showFailed) return;

        float timeLeft = running ? tracked.timeLeft : 0f;
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
            shortText = tracked.CurrentStep.shortText;
            detailText = tracked.CurrentStep.detailText;
        }
        else if (showFailed)
        {
            shortText = failedShortText;
            detailText = failedDetailText;
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